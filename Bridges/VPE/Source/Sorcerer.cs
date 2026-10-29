using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.36.0 术士（主 社交 / 副 智识）：社交支（混沌箭、魅惑人类、龙族之翼、群体暗示）看天赋强度（含血脉加成），智识支（燃烧之手、超魔·孪生、冰风暴、超魔·强效）看奥术强度，终极 魔涌。
 // 全部乘心灵敏感度。超魔与魔涌的处理写在 SorcererAbility 里。
 public static class Sorcerer {
  public static readonly Color Chaos=new Color(.9f,.45f,.75f);
  public static bool Is(Ability a)=>a?.def?.Psycast()?.path?.defName==Classes.SorcererPath;
  public static bool Surging(Pawn p)=>Items.Has(p,"LP_SorcerousSurge");
  // 魔涌期间每次施法 20% 触发狂野魔法：8 种结果里随机一种，前 4 好、后 4 坏。
  public static readonly string[] WildLabels={"治愈之光","隐形","灵能共鸣","繁花","自燃","乱序传送","熵涌","混乱精魂"};
  public static int WildSurge(Pawn p,int pick){
   var map=p.Map;
   switch(pick){
    case 0:ClassAbility.Heal(p,20f);break;
    case 1:Items.Give(p,"LP_WildInvisible",300,1f);break;
    case 2:if(map!=null)foreach(var a in Races124.PawnsNear(map,p.Position,6f).Where(x=>x.Faction==p.Faction))a.psychicEntropy?.OffsetPsyfocusDirectly(.05f);break;
    case 3:if(map!=null)foreach(var c in GenRadial.RadialCellsAround(p.Position,2.5f,true))if(c.InBounds(map))FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,1f,new Color(1f,.6f,.85f));break;
    case 4:p.TryAttachFire(.3f,p);break;
    case 5:if(map!=null && CellFinder.TryFindRandomCellNear(p.Position,map,5,c=>c.Standable(map) && c.GetFirstPawn(map)==null,out var cell))Races124.Blink(p,cell);break;
    case 6:p.psychicEntropy?.TryAddEntropy(20f,null,false,true);break;
    case 7:SpawnChaosSpirit(p);break;
   }
   Items.Mote(p,"狂野魔法："+WildLabels[pick],Chaos);
   return pick;
  }
  // 混乱精魂：一只敌对的弱小灵体（用原版的“混沌”生物：没有时用野生狐狸），30 秒后消失。
  static void SpawnChaosSpirit(Pawn p){
   var map=p.Map;if(map==null)return;
   var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Fox_Fennec")??DefDatabase<PawnKindDef>.GetNamedSilentFail("Rat");if(kind==null)return;
   var s=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true));
   GenSpawn.Spawn(s,CellFinder.RandomClosewalkCellNear(p.Position,map,3),map);
   var h=Items.Give(s,"LP_Conjured");var comp=(h as HediffWithComps)?.TryGetComp<HediffComp_Conjured>();if(comp!=null)comp.ticksLeft=1800;
   s.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter,"混乱精魂",true);
  }
 }
 // 术士能力：强效（下两个法术伤害 ×1.5）、孪生（下一个单体法术多打一个目标）、魔涌（冷却 ×0.25、免精神力、狂野魔法）。
 public abstract class SorcererAbility : ClassAbility {
  public virtual bool SingleTarget=>false;
  public override float GetPowerForPawn()=>base.GetPowerForPawn()*(Items.Has(pawn,"LP_Empowered")?1.5f:1f);
  public override int GetCooldownForPawn()=>Sorcerer.Surging(pawn)?Mathf.RoundToInt(base.GetCooldownForPawn()*.25f):base.GetCooldownForPawn();
  public override void Cast(GlobalTargetInfo[] targets){
   base.Cast(targets);
   if(SingleTarget && Items.Has(pawn,"LP_Twinned")){var extra=TwinTarget(targets);if(extra!=null){Apply(new[]{new GlobalTargetInfo(extra)});Items.Remove(pawn,"LP_Twinned");}}
   ConsumeEmpower();
   if(Sorcerer.Surging(pawn) && Rand.Chance(.2f))Sorcerer.WildSurge(pawn,Rand.Range(0,8));
  }
  public void ConsumeEmpower(){
   var def=Items.Hediff("LP_Empowered");var h=def==null?null:pawn.health.hediffSet.GetFirstHediffOfDef(def);
   if(h==null || this is Ability_EmpoweredSpell)return;
   h.Severity-=1f;if(h.Severity<.5f)pawn.health.RemoveHediff(h);
  }
  // 主目标 6 格内另一个敌对目标。
  public Pawn TwinTarget(GlobalTargetInfo[] targets){
   var first=targets.Select(g=>g.Thing as Pawn).FirstOrDefault(x=>x!=null);if(first==null || first.Map==null)return null;
   return Races124.PawnsNear(first.Map,first.Position,6f).Where(x=>x!=first && Hostile(pawn,x)).OrderBy(x=>x.Position.DistanceToSquared(first.Position)).FirstOrDefault();
  }
 }
 // 魔涌期间术士能力不耗精神力。
 [HarmonyPatch(typeof(AbilityExtension_Psycast),nameof(AbilityExtension_Psycast.GetPsyfocusUsedByPawn))]
 public static class SurgeFreePsyfocus {
  static void Postfix(AbilityExtension_Psycast __instance,Pawn pawn,ref float __result){
   if(__instance.path?.defName==Classes.SorcererPath && Sorcerer.Surging(pawn))__result=0f;
  }
 }

 // 混沌箭：随机伤害类型（火焰点燃、闪电击晕、奥术力、冰霜减速）；力能龙裔固定为力场；1/8 概率跳到 6 格内另一个敌人。
 public class Ability_ChaosBolt : SorcererAbility {
  public override bool SingleTarget=>true;
  public static readonly string[] Kinds={"Flame","LP_Lightning","LP_ArcaneForce","LP_Frost"};
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(!Hostile(pawn,t))continue;
    Bolt(pawn,t,Power,Rand.Range(0,Kinds.Length));
    if(Rand.Chance(.125f)){var next=Races124.PawnsNear(t.Map,t.Position,6f).FirstOrDefault(x=>x!=t && Hostile(pawn,x));if(next!=null)Bolt(pawn,next,Power,Rand.Range(0,Kinds.Length));}
   }
  }
  public static string KindFor(Pawn caster,int roll)=>caster?.genes?.Xenotype?.defName=="LP_ForceDragon" && DefDatabase<DamageDef>.GetNamedSilentFail("LP_Force")!=null?"LP_Force":Kinds[roll%Kinds.Length];
  public static string Bolt(Pawn caster,Pawn t,float amount,int roll){
   var kind=KindFor(caster,roll);var dmg=DefDatabase<DamageDef>.GetNamedSilentFail(kind)??DamageDefOf.Burn;
   if(caster.Map!=null){var line=DefDatabase<FleckDef>.GetNamedSilentFail("PsycastPsychicLine");if(line!=null)FleckMaker.ConnectingLine(caster.DrawPos,t.DrawPos,line,caster.Map);}
   t.TakeDamage(new DamageInfo(dmg,amount,.3f,-1f,caster));
   if(!t.Dead){
    if(kind=="Flame")t.TryAttachFire(.3f,caster);
    else if(kind=="LP_Lightning")t.stances?.stunner?.StunFor(30,caster);
    else if(kind=="LP_Frost")Items.Give(t,"LP_FrostStaffChill",180);
   }
   return kind;
  }
 }
 // 燃烧之手：面前 90° 扇形 6 格火焰，点燃。
 public class Ability_BurningHands : SorcererAbility {
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)if(g.Cell.IsValid)Cone(pawn,g.Cell,6f,Power);}
  public static List<IntVec3> ConeCells(IntVec3 from,IntVec3 toward,float range){
   var dir=(toward-from).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(0,0,1);dir.Normalize();
   var l=new List<IntVec3>();
   foreach(var c in GenRadial.RadialCellsAround(from,range,false)){var v=(c-from).ToVector3();if(Vector3.Angle(dir,v)<=45f)l.Add(c);}
   return l;
  }
  public static int Cone(Pawn caster,IntVec3 toward,float range,float amount){
   var map=caster.Map;if(map==null)return 0;int n=0;
   foreach(var c in ConeCells(caster.Position,toward,range)){
    if(!c.InBounds(map))continue;
    FleckMaker.ThrowFireGlow(c.ToVector3Shifted(),map,.6f);
    foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList())if(t!=caster && t.HostileTo(caster)){t.TakeDamage(new DamageInfo(DamageDefOf.Flame,amount,.2f,-1f,caster));if(!t.Dead)t.TryAttachFire(.3f,caster);n++;}
    if(Rand.Chance(.3f))FireUtility.TryStartFireIn(c,map,.3f,caster);
   }
   return n;
  }
 }
 // 魅惑人类：敌人进入恍惚（不攻击）；囚犯抵抗下降；心智抗性可抵抗。
 public class Ability_CharmPerson : SorcererAbility {
  public override bool SingleTarget=>true;
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(!base.ValidateTarget(target,showMessages))return false;
   var t=target.Thing as Pawn;
   if(t==null || t.Dead || !t.RaceProps.Humanlike){if(showMessages)Messages.Message("只能对人形使用。",MessageTypeDefOf.RejectInput,false);return false;}
   if(!t.HostileTo(pawn) && !t.IsPrisonerOfColony){if(showMessages)Messages.Message("只能对敌人或囚犯使用。",MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)Charm(pawn,PawnOf(g),Duration,Power);}
  public static string Charm(Pawn caster,Pawn t,int ticks,float resistanceCut){
   if(t==null || t.Dead || !t.RaceProps.Humanlike)return "invalid";
   if(DNDMind.TryResist(t)){Items.Mote(t,"抵抗了魅惑",Sorcerer.Chaos);return "resisted";}
   if(t.IsPrisonerOfColony && t.guest!=null){t.guest.resistance=Mathf.Max(0f,t.guest.resistance-resistanceCut);Items.Mote(t,"抵抗 -"+resistanceCut.ToString("0"),Sorcerer.Chaos);return "prisoner";}
   if(!t.HostileTo(caster))return "invalid";
   if(t.mindState?.mentalStateHandler!=null && t.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Psychotic,"被魅惑",true,true)){
    if(t.MentalState!=null)t.MentalState.forceRecoverAfterTicks=ticks;
    Items.Mote(t,"恍惚",Sorcerer.Chaos);return "charmed";
   }
   return "failed";
  }
 }
 // 超魔：孪生 / 强效，给自己挂状态。
 public class Ability_TwinSpell : SorcererAbility {
  public override void Apply(GlobalTargetInfo[] targets){Give(pawn,"LP_Twinned",1800,1f);Mote(pawn,"超魔·孪生",Sorcerer.Chaos);}
 }
 public class Ability_EmpoweredSpell : SorcererAbility {
  public override void Apply(GlobalTargetInfo[] targets){Give(pawn,"LP_Empowered",1800,2f);Mote(pawn,"超魔·强效",Sorcerer.Chaos);}
 }
 // 龙族之翼：跃空飞到目标格（原版跳跃同款飞行），落地前后 10 秒闪避 +20%。
 public class Ability_DragonWings : SorcererAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(!base.ValidateTarget(target,showMessages))return false;
   var why=Races124.BasicLanding(pawn,target.Cell,Mathf.RoundToInt(GetRangeForPawn()));
   if(why!=null){if(showMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)if(g.Cell.IsValid)Fly(pawn,g.Cell);}
  public static bool Fly(Pawn p,IntVec3 cell){
   var map=p.Map;if(map==null || !cell.InBounds(map))return false;
   Items.Give(p,"LP_DragonWingsLanded",900,1f);
   var flyer=PawnFlyer.MakeFlyer(ThingDefOf.PawnFlyer,p,cell,null,null);
   if(flyer==null){Races124.Blink(p,cell);return true;}
   GenSpawn.Spawn(flyer,cell,map);
   return true;
  }
 }
 // 冰风暴：半径 3 冰霜 + 钝器伤害，敌人减速，地面积雪。
 public class Ability_IceStorm : SorcererAbility {
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)if(g.Cell.IsValid)Storm(pawn,g.Cell,Radius,Power);}
  public static int Storm(Pawn caster,IntVec3 c,float radius,float amount){
   var map=caster.Map;if(map==null)return 0;int n=0;var frost=DefDatabase<DamageDef>.GetNamedSilentFail("LP_Frost")??DamageDefOf.Frostbite;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true)){if(!cell.InBounds(map))continue;FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1f,new Color(.75f,.9f,1f));map.snowGrid.AddDepth(cell,.4f);}
   foreach(var t in Races124.PawnsNear(map,c,radius).ToList()){
    if(t==caster || !t.HostileTo(caster))continue;
    t.TakeDamage(new DamageInfo(frost,amount,.2f,-1f,caster));
    if(!t.Dead)t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,amount*.5f,.1f,-1f,caster));
    if(!t.Dead)Items.Give(t,"LP_FrostStaffChill",360);
    n++;
   }
   return n;
  }
 }
 // 群体暗示：半径内敌对人形各自判定心智抗性，未抵抗的进入恍惚。
 public class Ability_MassSuggestion : SorcererAbility {
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){if(!g.Cell.IsValid || pawn.Map==null)continue;
    foreach(var t in Races124.PawnsNear(pawn.Map,g.Cell,Radius).ToList())if(t.RaceProps.Humanlike && Hostile(pawn,t))Ability_CharmPerson.Charm(pawn,t,Duration,0f);
   }
  }
 }
 // 魔涌：给自己挂状态。
 public class Ability_SorcerousSurge : SorcererAbility {
  public override void Apply(GlobalTargetInfo[] targets){Give(pawn,"LP_SorcerousSurge",Duration,1f);Mote(pawn,"魔涌",Sorcerer.Chaos);}
 }
}
