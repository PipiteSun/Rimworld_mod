using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // DND 怪物第二批：锈蚀怪（锈蚀触碰）、凝胶怪（吞没）、石化蜥（石化凝视）、蝎尾狮（尾刺齐射）、掘地兽（致命跃击）、腐尸爬虫（麻痹触须）。
 // 写法照 1.25.0 的 Beasts：每种怪的 ThingDef 上挂一个 CompWildMonster（kind 区分），冷却都记在它上面、随角色存档。
 public static class WildMonsters {
  public const float RustFactor=.1f,RustBackFactor=.05f,EngulfChance=.35f,GazeRadius=7f,GazeStep=.2f,GazeMinSight=.3f,SpikeMin=3f,SpikeMax=15f,SpikeMissChance=.35f,LeapMin=3f,LeapMax=9f,LeapRadius=1.9f,ToxinStep=.34f;
  public const int EngulfTicks=360,EngulfCooldown=600,EngulfAcid=3,PetrifiedTicks=30000,GazeDC=12,SpikeMax_=24,SpikeVolley=3,SpikeCooldown=480,SpikeRegrowTicks=2500,LeapCooldown=900,LeapDamage=16,LeapStunTicks=90,LeapDC=13,ToxinDC=13,ParalysisTicks=600;
  public static readonly string[] KindDefs={"LP_RustMonster","LP_GelatinousCube","LP_Basilisk","LP_Manticore","LP_Bulette","LP_CarrionCrawler"};
  static readonly Color RustColor=new Color(.85f,.5f,.3f),GelColor=new Color(.55f,.9f,.6f),StoneColor=new Color(.75f,.75f,.7f),SpikeColor=new Color(.95f,.85f,.6f),CrawlerColor=new Color(.9f,.6f,.65f);
  // 测试用：设了就代替随机数（吞没、尾刺偏移的概率判定）。豁免另用 Legendary.TestD20。
  public static float? TestRoll;
  public static float Roll=>TestRoll??Rand.Value;
  public static CompWildMonster Comp(Pawn p)=>p?.def?.GetCompProperties<CompProperties_WildMonster>()==null?null:p.GetComp<CompWildMonster>();
  public static string Kind(Pawn p)=>Comp(p)?.Props.kind;
  public static bool IsMonsterKind(PawnKindDef k)=>k?.race?.GetCompProperties<CompProperties_WildMonster>()!=null;
  static DamageDef acid;
  public static DamageDef Acid=>acid??(acid=DefDatabase<DamageDef>.GetNamedSilentFail("AcidBurn"));
  public static bool IsAcid(DamageDef d)=>d!=null && d==Acid;

  // ---------- 锈蚀 ----------
  // 金属：材料是金属类；没有材料的（枪械等）看造价里有没有金属。
  public static bool IsMetal(Thing t){
   if(t?.def==null)return false;
   if(t.Stuff!=null)return t.Stuff.stuffProps?.categories?.Contains(StuffCategoryDefOf.Metallic)==true;
   return t.def.costList!=null && t.def.costList.Any(c=>c?.thingDef?.stuffProps?.categories?.Contains(StuffCategoryDefOf.Metallic)==true);
  }
  // 掉最大耐久的一定比例（至少1点）；掉光就锈成废铁。返回是否锈掉了。
  public static bool Corrode(Thing t,float factor,Pawn owner){
   if(t==null || t.Destroyed || !t.def.useHitPoints || !IsMetal(t))return false;
   int loss=Mathf.Max(1,Mathf.RoundToInt(t.MaxHitPoints*factor));
   t.HitPoints-=loss;
   if(t.HitPoints>0){Items.Mote(owner,"锈蚀",RustColor);return false;}
   if(owner!=null && owner.Faction==Faction.OfPlayer)Messages.Message(owner.LabelShort+"的"+t.LabelNoCount+"被锈蚀成了一堆废铁。",owner,MessageTypeDefOf.NegativeEvent,false);
   t.Destroy();
   return true;
  }
  public static void RustTouch(Pawn target){
   if(target==null || target.Dead)return;
   Corrode(target.equipment?.Primary,RustFactor,target);
   var metal=target.apparel?.WornApparel?.Where(IsMetal).ToList();
   if(metal!=null && metal.Count>0)Corrode(metal[Mathf.Min(metal.Count-1,(int)(Roll*metal.Count))],RustFactor,target);
  }

  // ---------- 战斗目标 ----------
  // 正在攻击 / 捕猎的对象；有阵营的怪（驯服的、敌对派系的）另外找附近活跃的敌人。
  public static Pawn CombatTarget(Pawn p,float range){
   if(p?.Map==null || !p.Spawned || p.Dead || p.Downed)return null;
   Pawn t=p.mindState?.enemyTarget as Pawn;
   if(!Valid(p,t)){var job=p.CurJob;t=job!=null && (job.def==JobDefOf.AttackMelee || job.def==JobDefOf.PredatorHunt)?job.targetA.Thing as Pawn:null;}
   if(!Valid(p,t) && p.Faction!=null)t=Races124.PawnsNear(p.Map,p.Position,range).Where(o=>Valid(p,o) && o.HostileTo(p) && (o.Faction!=null || o.InAggroMentalState)).OrderBy(o=>o.Position.DistanceToSquared(p.Position)).FirstOrDefault();
   return Valid(p,t)?t:null;
  }
  static bool Valid(Pawn p,Pawn t)=>t!=null && t!=p && t.Spawned && !t.Dead && !t.Downed && t.Map==p.Map;

  // ---------- 石化凝视 ----------
  public static bool CanBeGazed(Pawn basilisk,Pawn t){
   if(t==null || t==basilisk || t.Dead || !t.Spawned || t.def==basilisk.def || t.RaceProps==null || !t.RaceProps.IsFlesh)return false;
   if(Items.Has(t,"LP_BasiliskPetrified") || !t.Awake())return false;
   if(t.health.capacities.GetLevel(PawnCapacityDefOf.Sight)<GazeMinSight)return false;
   return GenSight.LineOfSight(basilisk.Position,t.Position,basilisk.Map,true);
  }
  // 一次凝视：石化中 +0.2；满了做一次体质豁免，成功退回一半，失败变成石头。返回是否石化。
  public static bool Gaze(Pawn basilisk,Pawn t){
   var def=Items.Hediff("LP_BasiliskGaze");if(def==null)return false;
   var h=t.health.hediffSet.GetFirstHediffOfDef(def);
   if(h==null){h=HediffMaker.MakeHediff(def,t);h.Severity=GazeStep;t.health.AddHediff(h);}
   else h.Severity=Mathf.Min(1f,h.Severity+GazeStep);
   if(h.Severity<1f)return false;
   if(Legendary.Save(t,Adventurer.Con,GazeDC,basilisk)){h.Severity=.5f;return false;}
   t.health.RemoveHediff(h);
   Items.Give(t,"LP_BasiliskPetrified",PetrifiedTicks);
   Items.Mote(t,"石化！",StoneColor);
   return true;
  }
  public static int GazeAll(Pawn basilisk){
   var target=CombatTarget(basilisk,GazeRadius);if(target==null)return 0;
   int n=0;
   foreach(var t in Races124.PawnsNear(basilisk.Map,basilisk.Position,GazeRadius).ToList())
    if((t==target || t.HostileTo(basilisk)) && CanBeGazed(basilisk,t)){Gaze(basilisk,t);n++;}
   return n;
  }

  // ---------- 吞没 ----------
  public static bool TryEngulf(Pawn cube,Pawn target,float roll){
   var comp=Comp(cube);
   if(comp==null || comp.Props.kind!="GelatinousCube" || cube.Dead || cube.Downed || target==null || target.Dead || !target.Spawned)return false;
   if(!comp.EngulfReady || roll>=EngulfChance || target.BodySize>cube.BodySize || Items.Has(target,"LP_Engulfed"))return false;
   var def=Items.Hediff("LP_Engulfed");if(def==null)return false;
   var h=(Hediff_Engulfed)HediffMaker.MakeHediff(def,target);h.cube=cube;h.left=EngulfTicks;
   target.health.AddHediff(h);
   target.stances?.stunner?.StunFor(90,cube,false,false);
   comp.engulfReadyTick=Find.TickManager.TicksGame+EngulfCooldown;
   Items.Mote(target,"被吞没",GelColor);
   return true;
  }

  // ---------- 麻痹触须 ----------
  public static bool IsTentacle(Tool tool)=>tool?.label=="tentacles";
  // 返回是否麻痹。
  public static bool Toxin(Pawn crawler,Pawn t){
   if(t==null || t.Dead || t.RaceProps==null || !t.RaceProps.IsFlesh || Items.Has(t,"LP_CrawlerParalysis"))return false;
   if(Legendary.Save(t,Adventurer.Con,ToxinDC,crawler))return false;
   var def=Items.Hediff("LP_CrawlerToxin");if(def==null)return false;
   var h=t.health.hediffSet.GetFirstHediffOfDef(def);
   if(h==null){h=HediffMaker.MakeHediff(def,t);h.Severity=ToxinStep;t.health.AddHediff(h);}
   else h.Severity=Mathf.Min(1f,h.Severity+ToxinStep);
   if(h.Severity<.99f)return false;
   t.health.RemoveHediff(h);
   Items.Give(t,"LP_CrawlerParalysis",ParalysisTicks);
   Items.Mote(t,"全身麻痹",CrawlerColor);
   return true;
  }

  // ---------- 尾刺齐射 ----------
  static ThingDef spikeDef;
  public static ThingDef SpikeDef=>spikeDef??(spikeDef=DefDatabase<ThingDef>.GetNamedSilentFail("LP_Proj_ManticoreSpike"));
  public static bool InSpikeRange(Pawn p,Pawn t){float d=p.Position.DistanceTo(t.Position);return d>=SpikeMin && d<=SpikeMax && GenSight.LineOfSight(p.Position,t.Position,p.Map,true);}
  // 返回射出的根数。
  public static int Volley(Pawn p,Pawn t,CompWildMonster comp){
   if(SpikeDef==null || p.Map==null)return 0;
   int n=Mathf.Min(SpikeVolley,comp.spikes);
   for(int i=0;i<n;i++){
    var proj=(Projectile)GenSpawn.Spawn(SpikeDef,p.Position,p.Map);
    LocalTargetInfo used=t;
    if(Roll<SpikeMissChance){var cells=GenRadial.RadialCellsAround(t.Position,2f,false).Where(c=>c.InBounds(p.Map)).ToList();if(cells.Count>0)used=cells.RandomElement();}
    proj.Launch(p,p.DrawPos,used,t,used.HasThing?ProjectileHitFlags.IntendedTarget:ProjectileHitFlags.All);
   }
   comp.spikes-=n;
   if(n>0)Items.Mote(p,"尾刺齐射",SpikeColor);
   return n;
  }

  // ---------- 致命跃击 ----------
  public static bool InLeapRange(Pawn p,Pawn t){float d=p.Position.DistanceTo(t.Position);return d>=LeapMin && d<=LeapMax;}
  public static bool Leap(Pawn p,Pawn t){
   var cell=Charge.LandingCell(p,t);if(!cell.IsValid)return false;var map=p.Map;
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("LP_BuletteFlyer");
   Items.Mote(p,"跃起",StoneColor);
   PawnFlyer flyer=null;
   try{flyer=def==null?null:PawnFlyer.MakeFlyer(def,p,cell,null,null);}
   catch(Exception e){Log.WarningOnce("[DND边缘大陆] 掘地兽跃击失败："+e.Message,74177001);}
   for(int i=0;i<3;i++)FleckMaker.ThrowDustPuffThick(p.Position.ToVector3Shifted(),map,1.6f,new Color(.6f,.55f,.45f));
   if(flyer==null){Races124.Blink(p,cell);Land(p);return true;}
   GenSpawn.Spawn(flyer,cell,map); // MakeFlyer 已经把掘地兽从地图上拿走了
   return true;
  }
  // 落地：1.9 格内的其他人受到钝击；敏捷豁免成功伤害减半、不被撞倒。同阵营的不打。返回被撞倒的人数。
  public static int Land(Pawn p){
   if(p?.Map==null || !p.Spawned || p.Dead)return 0;
   int knocked=0;var blunt=DamageDefOf.Blunt;
   for(int i=0;i<4;i++)FleckMaker.ThrowDustPuffThick(p.Position.ToVector3Shifted(),p.Map,2f,new Color(.6f,.55f,.45f));
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,LeapRadius).ToList()){
    if(t==p || t.Dead || (p.Faction!=null && t.Faction==p.Faction))continue;
    bool saved=Legendary.Save(t,Adventurer.Dex,LeapDC,p);
    t.TakeDamage(new DamageInfo(blunt,saved?LeapDamage/2:LeapDamage,0.2f,-1f,p));
    if(!saved && !t.Dead){t.stances?.stunner?.StunFor(LeapStunTicks,p,false,false);knocked++;}
   }
   Items.Mote(p,"致命跃击",StoneColor);
   return knocked;
  }

  // ---------- 设置：野外刷新与狂暴兽群 ----------
  static readonly Dictionary<PawnKindDef,bool> manhunterDefault=new Dictionary<PawnKindDef,bool>();
  public static IEnumerable<PawnKindDef> AllKinds(){foreach(var n in KindDefs){var k=DefDatabase<PawnKindDef>.GetNamedSilentFail(n);if(k!=null)yield return k;}}
  public static void ApplySettings(){
   bool on=LichMod.Settings?.dndMonstersWild!=false;
   foreach(var k in AllKinds()){
    if(!manhunterDefault.TryGetValue(k,out bool d))manhunterDefault[k]=d=k.canArriveManhunter;
    k.canArriveManhunter=d && on;
   }
  }
 }

 public class CompProperties_WildMonster : CompProperties {
  public string kind;
  public CompProperties_WildMonster(){compClass=typeof(CompWildMonster);}
 }
 public class CompWildMonster : ThingComp {
  public int engulfReadyTick=-1,volleyReadyTick=-1,leapReadyTick=-1,spikes=WildMonsters.SpikeMax_,spikeGrow;
  int tickAcc;
  public CompProperties_WildMonster Props=>(CompProperties_WildMonster)props;
  Pawn Pawn=>parent as Pawn;
  static int Now=>Find.TickManager.TicksGame;
  public bool EngulfReady=>Now>=engulfReadyTick;
  public bool VolleyReady=>Now>=volleyReadyTick;
  public bool LeapReady=>Now>=leapReadyTick;

  public void Notify_MeleeHit(Pawn target,Tool tool){
   if(target==null || target.Dead)return;
   switch(Props.kind){
    case "RustMonster":WildMonsters.RustTouch(target);break;
    case "GelatinousCube":WildMonsters.TryEngulf(Pawn,target,WildMonsters.Roll);break;
    case "CarrionCrawler":if(WildMonsters.IsTentacle(tool))WildMonsters.Toxin(Pawn,target);break;
   }
  }

  public override void PostPreApplyDamage(ref DamageInfo dinfo,out bool absorbed){
   absorbed=false;var p=Pawn;if(p==null || p.Dead)return;
   switch(Props.kind){
    case "GelatinousCube":if(WildMonsters.IsAcid(dinfo.Def))absorbed=true;break;
    // 用金属近战武器砍锈蚀怪：武器也会锈掉一点。
    case "RustMonster":
     if(dinfo.Instigator is Pawn a && a.equipment?.Primary is ThingWithComps w && w.def.IsMeleeWeapon && dinfo.Weapon==w.def && a.Position.AdjacentTo8WayOrInside(p.Position))WildMonsters.Corrode(w,WildMonsters.RustBackFactor,a);
     break;
   }
  }

  public override void CompTickInterval(int delta){
   base.CompTickInterval(delta);
   if(Props.kind=="Manticore" && spikes<WildMonsters.SpikeMax_ && (spikeGrow+=delta)>=WildMonsters.SpikeRegrowTicks){spikeGrow=0;spikes++;}
   if((tickAcc+=delta)<60)return;tickAcc=0;
   var p=Pawn;if(p==null || p.Dead || p.Downed || !p.Spawned || !p.Awake())return;
   switch(Props.kind){
    case "Basilisk":WildMonsters.GazeAll(p);break;
    case "Manticore":
     if(!VolleyReady || spikes<=0)break;
     var t=WildMonsters.CombatTarget(p,WildMonsters.SpikeMax);
     if(t!=null && WildMonsters.InSpikeRange(p,t) && WildMonsters.Volley(p,t,this)>0)volleyReadyTick=Now+WildMonsters.SpikeCooldown;
     break;
    case "Bulette":
     if(!LeapReady)break;
     var u=WildMonsters.CombatTarget(p,WildMonsters.LeapMax);
     if(u!=null && WildMonsters.InLeapRange(p,u) && WildMonsters.Leap(p,u))leapReadyTick=Now+WildMonsters.LeapCooldown;
     break;
   }
  }

  public override string CompInspectStringExtra(){
   int now=Now;
   switch(Props.kind){
    case "Manticore":return "尾刺："+spikes+" / "+WildMonsters.SpikeMax_+(now<volleyReadyTick?"（齐射冷却 "+((volleyReadyTick-now)/60f).ToString("0.#")+" 秒）":"");
    case "Bulette":return now<leapReadyTick?"跃击冷却："+((leapReadyTick-now)/60f).ToString("0.#")+"秒":null;
    case "GelatinousCube":return now<engulfReadyTick?"吞没冷却："+((engulfReadyTick-now)/60f).ToString("0.#")+"秒":null;
   }
   return null;
  }
  public override void PostExposeData(){
   base.PostExposeData();
   Scribe_Values.Look(ref engulfReadyTick,"lpEngulfReady",-1);
   Scribe_Values.Look(ref volleyReadyTick,"lpVolleyReady",-1);
   Scribe_Values.Look(ref leapReadyTick,"lpLeapReady",-1);
   Scribe_Values.Look(ref spikes,"lpSpikes",WildMonsters.SpikeMax_);
   Scribe_Values.Look(ref spikeGrow,"lpSpikeGrow",0);
  }
 }

 // 被凝胶怪吞没：人困在胶体里、跟着凝胶怪走，每秒酸蚀一次、保持昏迷；凝胶怪倒下或死掉就脱身，最多 6 秒。
 public class Hediff_Engulfed : HediffWithComps {
  public Pawn cube;public int left=WildMonsters.EngulfTicks;int acc;
  public bool Holding=>cube!=null && !cube.Dead && !cube.Downed && cube.Spawned && pawn.Spawned && cube.Map==pawn.Map;
  public override bool ShouldRemove=>base.ShouldRemove || left<=0 || !Holding;
  public override string LabelInBrackets=>(Mathf.Max(0,left)/60f).ToString("0")+"秒";
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);Drag();}
  void Drag(){if(Holding && pawn.Position!=cube.Position)Races124.Blink(pawn,cube.Position);}
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   left-=delta;if(left<=0 || !Holding)return;
   Drag();
   if((acc+=delta)<60)return;acc=0;
   pawn.stances?.stunner?.StunFor(90,cube,false,false);
   if(WildMonsters.Acid!=null)pawn.TakeDamage(new DamageInfo(WildMonsters.Acid,WildMonsters.EngulfAcid,0f,-1f,cube));
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_References.Look(ref cube,"lpCube");
   Scribe_Values.Look(ref left,"lpLeft",WildMonsters.EngulfTicks);
   Scribe_Values.Look(ref acc,"lpAcc",0);
  }
 }

 public class LP_BuletteFlyer : PawnFlyer {
  protected override void RespawnPawn(){var p=FlyingPawn;base.RespawnPawn();if(p!=null)WildMonsters.Land(p);}
 }

 // 命中后：锈蚀、吞没、麻痹毒素（只在原版命中/闪避判定之后到这里，每次攻击一次）。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class WildMonsterMeleeHit {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){WildMonsters.Comp(__instance.CasterPawn)?.Notify_MeleeHit(target.Pawn,__instance.tool);}
 }
 // 关闭“野外刷新 DND 怪物”时，这 6 种在所有生态区的野外权重为 0。
 [HarmonyPatch(typeof(BiomeDef),nameof(BiomeDef.CommonalityOfAnimal))]
 public static class WildMonsterWildToggle {
  static void Postfix(PawnKindDef animalDef,ref float __result){if(__result>0f && LichMod.Settings?.dndMonstersWild==false && WildMonsters.IsMonsterKind(animalDef))__result=0f;}
 }

 public partial class Settings {
  public bool dndMonstersWild=true;
  void ExposeWildMonsters(){Scribe_Values.Look(ref dndMonstersWild,"dndMonstersWild",true);}
 }
 public static class WildMonsterSettings {
  public static void Draw(Listing_Standard l){l.CheckboxLabeled("野外刷新 DND 怪物",ref LichMod.Settings.dndMonstersWild,"锈蚀怪、凝胶怪、石化蜥、蝎尾狮、掘地兽、腐尸爬虫会在各自的生态区偶尔刷新，也会以狂暴兽群出现。关闭后不再刷新、也不会以狂暴兽群出现（迷锁地牢的「幽暗地穴」不受影响）；保存设置后立即生效，已经在地图上的怪物不受影响。");}
 }
}
