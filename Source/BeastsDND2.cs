using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.127.0 DND 野兽第二批：伪龙（催眠尾刺）、翼猫（看穿隐形）、巨型火甲虫（光腺）、罗斯牛（护群）、鹰头马身兽（振翼击退）、
 // 斧喙鸟（惊逃疾奔）、狮鹫（俯冲扑击）、钩爪恐兽（钩住）、相位蜘蛛（相位跳跃）、暗斗篷（蒙头致盲）、血蝠蝇（吸血）。
 // 写法照 1.25.0 的 Beasts：每种 ThingDef 上挂一个 CompBeastDND2（kind 区分），冷却记在它上面、随角色存档。
 // 野性等级 / 精英 / 兽群首领（BeastLevels）对所有动物生效，这里不用另外接。
 public static class BeastsDND2 {
  public const float SleepMaxBody=2f,ShroudChance=.35f,BuffetChance=.25f,BuffetMaxBody=3f,SprintHealth=.6f,RallyRadius=12f,TressymRadius=8f,
   PhaseMin=2.5f,PhaseMax=9f,DiveMin=4f,DiveMax=10f,DrainBlood=.04f,DrainHeal=3f;
  public const int SleepDC=11,HookDC=12,ShroudDC=11,DiveDC=12,DiveDamage=14,DiveStun=60,BuffetStun=60,BuffetDistance=2,
   StingCooldown=1800,ShroudCooldown=480,BuffetCooldown=480,SprintCooldown=1800,PhaseCooldown=600,DiveCooldown=900,DrainsToFull=3,TressymInterval=250;
  public static readonly string[] KindDefs={"LP_Pseudodrake","LP_Tressym","LP_FireBeetle","LP_Rothe","LP_Hippogriff","LP_AxeBeak","LP_Griffon","LP_HookHorror","LP_PhaseSpider","LP_Darkmantle","LP_Stirge"};
  static readonly Color SleepColor=new Color(.75f,.6f,1f),HookColor=new Color(.9f,.8f,.6f),DarkColor=new Color(.6f,.6f,.75f),BloodColor=new Color(.9f,.3f,.3f),
   WindColor=new Color(.85f,.9f,1f),PhaseColor=new Color(.55f,.75f,1f),RageColor=new Color(.95f,.55f,.45f),SprintColor=new Color(.95f,.9f,.5f);
  // 测试用：设了就代替随机数（蒙头、振翼的概率判定）。豁免另用 Legendary.TestD20。
  public static float? TestRoll;
  public static float Roll=>TestRoll??Rand.Value;
  public static CompBeastDND2 Comp(Pawn p)=>p?.def?.GetCompProperties<CompProperties_BeastDND2>()==null?null:p.GetComp<CompBeastDND2>();
  public static string Kind(Pawn p)=>Comp(p)?.Props.kind;
  public static bool IsKind(PawnKindDef k)=>k?.race?.GetCompProperties<CompProperties_BeastDND2>()!=null;
  static bool Live(Pawn t)=>t!=null && !t.Dead && t.Spawned;

  // ---------- 伪龙：催眠尾刺 ----------
  public static bool IsSting(Tool tool)=>tool?.label=="tail sting";
  public static bool Sting(Pawn pd,Pawn t){
   if(!Live(t) || t.RaceProps==null || !t.RaceProps.IsFlesh || t.BodySize>SleepMaxBody || Items.Has(t,"LP_PseudoSleep"))return false;
   if(Legendary.Save(t,Adventurer.Con,SleepDC,pd))return false;
   Items.Give(t,"LP_PseudoSleep");
   Items.Mote(t,"沉睡",SleepColor);
   return true;
  }

  // ---------- 钩爪恐兽：钩住 ----------
  public static bool IsHook(Tool tool)=>tool?.label=="hooks";
  public static bool Hook(Pawn h,Pawn t){
   if(!Live(t) || Items.Has(t,"LP_Hooked"))return false;
   if(Legendary.Save(t,Adventurer.Str,HookDC,h))return false;
   Items.Give(t,"LP_Hooked");
   Items.Mote(t,"被钩住",HookColor);
   return true;
  }

  // ---------- 暗斗篷：蒙头 ----------
  public static bool IsTentacle(Tool tool)=>tool?.label=="tentacles";
  public static bool Shroud(Pawn d,Pawn t,float roll){
   if(!Live(t) || roll>=ShroudChance || Items.Has(t,"LP_Darkmantled") || !t.health.capacities.CapableOf(PawnCapacityDefOf.Sight))return false;
   if(Legendary.Save(t,Adventurer.Dex,ShroudDC,d))return false;
   Items.Give(t,"LP_Darkmantled");
   Items.Mote(t,"蒙头",DarkColor);
   return true;
  }

  // ---------- 血蝠蝇：吸血 ----------
  public static bool CanDrain(Pawn t)=>t!=null && !t.Dead && t.RaceProps!=null && t.RaceProps.IsFlesh && t.RaceProps.BloodDef!=null;
  public static bool Drain(Pawn s,Pawn t,CompBeastDND2 comp){
   if(!CanDrain(t))return false;
   var h=t.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
   float add=DrainBlood/Mathf.Max(1f,t.BodySize);
   if(h==null){h=HediffMaker.MakeHediff(HediffDefOf.BloodLoss,t);h.Severity=add;t.health.AddHediff(h);}
   else h.Severity=Mathf.Min(1f,h.Severity+add);
   var inj=s.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(i=>i.Severity).FirstOrDefault();
   inj?.Heal(DrainHeal);
   if(comp!=null && ++comp.drains>=DrainsToFull){comp.drains=0;var food=s.needs?.food;if(food!=null)food.CurLevel=food.MaxLevel;Items.Mote(s,"吸饱了",BloodColor);}
   else Items.Mote(t,"吸血",BloodColor);
   return true;
  }

  // ---------- 罗斯牛：护群 ----------
  // 被别的生物打伤时：12格内同阵营（野生的算一群）的罗斯牛（包括挨打的这只）陷入群牛之怒；野生的冲向攻击者。群牛之怒期间不重复触发。返回被激怒的同伴数（不含挨打的这只）。
  public static int Rally(Pawn hurt,Pawn attacker){
   if(hurt?.Map==null || !hurt.Spawned || hurt.Dead || Items.Has(hurt,"LP_RotheRage") || attacker==null || attacker.def==hurt.def)return 0;
   int n=0;
   Items.Give(hurt,"LP_RotheRage");
   foreach(var o in Races124.PawnsNear(hurt.Map,hurt.Position,RallyRadius).ToList()){
    if(o==hurt || o.def!=hurt.def || o.Dead || o.Downed || o.Faction!=hurt.Faction || Items.Has(o,"LP_RotheRage"))continue;
    Items.Give(o,"LP_RotheRage");n++;
    if(o.Faction==null && !o.InMentalState)o.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Manhunter,"罗斯牛护群",true);
   }
   if(hurt.Faction==null && !hurt.InMentalState && !hurt.Downed)hurt.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Manhunter,"罗斯牛护群",true);
   if(n>0)Items.Mote(hurt,"群牛之怒",RageColor);
   return n;
  }

  // ---------- 鹰头马身兽：振翼击退 ----------
  // 把相邻的攻击者沿“鹰头马身兽→攻击者”的方向推出最多2格（找能站、没人的格子），晕眩1秒。体型大于3的不被推动。
  public static bool Buffet(Pawn h,Pawn a,float roll){
   if(!Live(h) || h.Downed || !Live(a) || a.Downed || roll>=BuffetChance || a.BodySize>BuffetMaxBody || !a.Position.AdjacentTo8WayOrInside(h.Position))return false;
   var map=h.Map;var d=a.Position-h.Position;if(d==IntVec3.Zero)d=IntVec3.North;
   d=new IntVec3(Math.Sign(d.x),0,Math.Sign(d.z));
   IntVec3 cell=IntVec3.Invalid;
   for(int i=1;i<=BuffetDistance;i++){var c=a.Position+d*i;if(!c.InBounds(map) || !c.Standable(map) || c.GetFirstPawn(map)!=null)break;cell=c;}
   if(cell.IsValid)Races124.Blink(a,cell);
   a.stances?.stunner?.StunFor(BuffetStun,h,false,false);
   for(int i=0;i<2;i++)FleckMaker.ThrowDustPuffThick(a.Position.ToVector3Shifted(),map,1.4f,WindColor);
   Items.Mote(h,"振翼",WindColor);
   return true;
  }

  // ---------- 斧喙鸟：惊逃疾奔 ----------
  public static bool Sprint(Pawn b){
   if(!Live(b) || b.Downed || b.health.summaryHealth.SummaryHealthPercent>=SprintHealth || Items.Has(b,"LP_AxeBeakSprint"))return false;
   Items.Give(b,"LP_AxeBeakSprint");
   Items.Mote(b,"惊逃疾奔",SprintColor);
   return true;
  }

  // ---------- 相位蜘蛛：相位跳跃 ----------
  public static bool InPhaseRange(Pawn p,Pawn t){float d=p.Position.DistanceTo(t.Position);return d>=PhaseMin && d<=PhaseMax;}
  public static bool Phase(Pawn p,Pawn t){
   var cell=Charge.LandingCell(p,t);if(!cell.IsValid)return false;
   var map=p.Map;var from=p.Position;
   FleckMaker.ThrowDustPuffThick(from.ToVector3Shifted(),map,1.2f,PhaseColor);
   Races124.Blink(p,cell);
   FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1.2f,PhaseColor);
   Items.Mote(p,"相位跳跃",PhaseColor);
   return true;
  }

  // ---------- 狮鹫：俯冲扑击 ----------
  public static bool InDiveRange(Pawn p,Pawn t){float d=p.Position.DistanceTo(t.Position);return d>=DiveMin && d<=DiveMax;}
  public static bool Dive(Pawn p,Pawn t){
   var cell=Charge.LandingCell(p,t);if(!cell.IsValid)return false;var map=p.Map;
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("LP_GriffonFlyer");
   Items.Mote(p,"俯冲",WindColor);
   PawnFlyer flyer=null;
   try{flyer=def==null?null:PawnFlyer.MakeFlyer(def,p,cell,null,null);}
   catch(Exception e){Log.WarningOnce("[DND边缘大陆] 狮鹫俯冲失败："+e.Message,74177124);}
   if(flyer==null){Races124.Blink(p,cell);DiveLand(p,t);return true;}
   if(flyer is LP_GriffonFlyer g)g.diveTarget=t;
   GenSpawn.Spawn(flyer,cell,map); // MakeFlyer 已经把狮鹫从地图上拿走了
   return true;
  }
  // 落地：相邻的目标受到割伤；敏捷豁免成功伤害减半、不晕眩。返回是否晕眩了目标。
  public static bool DiveLand(Pawn p,Pawn t){
   if(!Live(p) || !Live(t) || t.Map!=p.Map || !t.Position.AdjacentTo8WayOrInside(p.Position))return false;
   bool saved=Legendary.Save(t,Adventurer.Dex,DiveDC,p);
   t.TakeDamage(new DamageInfo(DamageDefOf.Cut,saved?DiveDamage/2:DiveDamage,0.2f,-1f,p));
   for(int i=0;i<3;i++)FleckMaker.ThrowDustPuffThick(p.Position.ToVector3Shifted(),p.Map,1.6f,WindColor);
   Items.Mote(p,"俯冲扑击",WindColor);
   if(saved || t.Dead)return false;
   t.stances?.stunner?.StunFor(DiveStun,p,false,false);
   return true;
  }

  // ---------- 设置：跟随“野外刷新 DND 野兽” ----------
  static readonly Dictionary<PawnKindDef,bool> manhunterDefault=new Dictionary<PawnKindDef,bool>();
  public static IEnumerable<PawnKindDef> AllKinds(){foreach(var n in KindDefs){var k=DefDatabase<PawnKindDef>.GetNamedSilentFail(n);if(k!=null)yield return k;}}
  public static void ApplySettings(){
   bool on=LichMod.Settings?.dndBeastsWild!=false;
   foreach(var k in AllKinds()){
    if(!manhunterDefault.TryGetValue(k,out bool d))manhunterDefault[k]=d=k.canArriveManhunter;
    k.canArriveManhunter=d && on;
   }
  }
  public static bool DefaultManhunter(PawnKindDef k){ApplySettings();return manhunterDefault.TryGetValue(k,out bool d) && d;}
 }

 public class CompProperties_BeastDND2 : CompProperties {
  public string kind;
  public CompProperties_BeastDND2(){compClass=typeof(CompBeastDND2);}
 }
 public class CompBeastDND2 : ThingComp {
  public int stingReadyTick=-1,shroudReadyTick=-1,buffetReadyTick=-1,sprintReadyTick=-1,phaseReadyTick=-1,diveReadyTick=-1,drains;
  int tickAcc,tressymAcc;
  IntVec3 glowCell=IntVec3.Invalid;
  public CompProperties_BeastDND2 Props=>(CompProperties_BeastDND2)props;
  Pawn Pawn=>parent as Pawn;
  static int Now=>Find.TickManager.TicksGame;
  static bool Ready(int tick)=>Now>=tick;

  public void Notify_MeleeHit(Pawn target,Tool tool){
   if(target==null || target.Dead)return;var p=Pawn;
   switch(Props.kind){
    case "Pseudodrake":if(BeastsDND2.IsSting(tool) && Ready(stingReadyTick) && BeastsDND2.Sting(p,target))stingReadyTick=Now+BeastsDND2.StingCooldown;break;
    case "HookHorror":if(BeastsDND2.IsHook(tool))BeastsDND2.Hook(p,target);break;
    case "Darkmantle":if(BeastsDND2.IsTentacle(tool) && Ready(shroudReadyTick) && BeastsDND2.Shroud(p,target,BeastsDND2.Roll))shroudReadyTick=Now+BeastsDND2.ShroudCooldown;break;
    case "Stirge":BeastsDND2.Drain(p,target,this);break;
   }
  }

  public override void PostPostApplyDamage(DamageInfo dinfo,float totalDamageDealt){
   base.PostPostApplyDamage(dinfo,totalDamageDealt);
   var p=Pawn;if(p==null || p.Dead || totalDamageDealt<=0f)return;
   var a=dinfo.Instigator as Pawn;
   switch(Props.kind){
    case "Rothe":if(a!=null && a!=p)BeastsDND2.Rally(p,a);break;
    case "Hippogriff":if(a!=null && a!=p && Ready(buffetReadyTick) && dinfo.Def!=null && dinfo.Def.isRanged==false && !dinfo.Def.isExplosive && BeastsDND2.Buffet(p,a,BeastsDND2.Roll))buffetReadyTick=Now+BeastsDND2.BuffetCooldown;break;
    case "AxeBeak":if(Ready(sprintReadyTick) && BeastsDND2.Sprint(p))sprintReadyTick=Now+BeastsDND2.SprintCooldown;break;
   }
  }

  public override void CompTickInterval(int delta){
   base.CompTickInterval(delta);
   if(Props.kind=="Tressym" && (tressymAcc+=delta)>=BeastsDND2.TressymInterval){tressymAcc=0;var t=Pawn;if(t!=null && t.Spawned && !t.Dead && !t.Downed && t.Faction!=null)Exalted.Reveal(t,BeastsDND2.TressymRadius);}
   if((tickAcc+=delta)<60)return;tickAcc=0;
   var p=Pawn;if(p==null || p.Dead || !p.Spawned)return;
   // 原版的发光组件只在登记时记下位置：火甲虫走动后重新登记（同 1.25.0 地狱犬）。
   if(Props.kind=="FireBeetle"){if(p.Position!=glowCell){var glower=parent.GetComp<CompGlower>();if(glower!=null && glower.Glows){glower.ForceRegister(p.Map);glowCell=p.Position;}}return;}
   if(p.Downed || !p.Awake())return;
   switch(Props.kind){
    case "PhaseSpider":
     if(!Ready(phaseReadyTick))break;
     var t=WildMonsters.CombatTarget(p,BeastsDND2.PhaseMax);
     if(t!=null && BeastsDND2.InPhaseRange(p,t) && BeastsDND2.Phase(p,t))phaseReadyTick=Now+BeastsDND2.PhaseCooldown;
     break;
    case "Griffon":
     if(!Ready(diveReadyTick))break;
     var u=WildMonsters.CombatTarget(p,BeastsDND2.DiveMax);
     if(u!=null && BeastsDND2.InDiveRange(p,u) && BeastsDND2.Dive(p,u))diveReadyTick=Now+BeastsDND2.DiveCooldown;
     break;
   }
  }
  public override void PostSpawnSetup(bool respawningAfterLoad){base.PostSpawnSetup(respawningAfterLoad);glowCell=IntVec3.Invalid;}

  static string Cd(string label,int tick){int now=Now;return now<tick?label+"冷却："+((tick-now)/60f).ToString("0.#")+"秒":null;}
  public override string CompInspectStringExtra(){
   switch(Props.kind){
    case "Pseudodrake":return Cd("催眠尾刺",stingReadyTick);
    case "Darkmantle":return Cd("蒙头",shroudReadyTick);
    case "Hippogriff":return Cd("振翼",buffetReadyTick);
    case "AxeBeak":return Cd("惊逃疾奔",sprintReadyTick);
    case "PhaseSpider":return Cd("相位跳跃",phaseReadyTick);
    case "Griffon":return Cd("俯冲扑击",diveReadyTick);
    case "Stirge":return drains>0?"吸血："+drains+" / "+BeastsDND2.DrainsToFull:null;
   }
   return null;
  }
  public override void PostExposeData(){
   base.PostExposeData();
   Scribe_Values.Look(ref stingReadyTick,"lpStingReady",-1);
   Scribe_Values.Look(ref shroudReadyTick,"lpShroudReady",-1);
   Scribe_Values.Look(ref buffetReadyTick,"lpBuffetReady",-1);
   Scribe_Values.Look(ref sprintReadyTick,"lpSprintReady",-1);
   Scribe_Values.Look(ref phaseReadyTick,"lpPhaseReady",-1);
   Scribe_Values.Look(ref diveReadyTick,"lpDiveReady",-1);
   Scribe_Values.Look(ref drains,"lpDrains",0);
  }
 }

 public class LP_GriffonFlyer : PawnFlyer {
  public Pawn diveTarget;
  protected override void RespawnPawn(){var p=FlyingPawn;var t=diveTarget;base.RespawnPawn();if(p!=null)BeastsDND2.DiveLand(p,t);}
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref diveTarget,"lpDiveTarget");}
 }

 // 命中后：催眠尾刺、钩住、蒙头、吸血（只在原版命中/闪避判定之后到这里，每次攻击一次）。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class BeastDND2MeleeHit {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){BeastsDND2.Comp(__instance.CasterPawn)?.Notify_MeleeHit(target.Pawn,__instance.tool);}
 }
 // 关闭“野外刷新 DND 野兽”时，这 11 种在所有生态区的野外权重也为 0。
 [HarmonyPatch(typeof(BiomeDef),nameof(BiomeDef.CommonalityOfAnimal))]
 public static class BeastDND2WildToggle {
  static void Postfix(PawnKindDef animalDef,ref float __result){if(__result>0f && LichMod.Settings?.dndBeastsWild==false && BeastsDND2.IsKind(animalDef))__result=0f;}
 }
 [StaticConstructorOnStartup]
 public static class BeastDND2Startup {static BeastDND2Startup(){BeastsDND2.ApplySettings();}}
}
