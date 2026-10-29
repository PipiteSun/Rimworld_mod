using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.34.0 风暴巨人：本源（巨人血统 rank 1、2.4 体型、水中行动、雷霆一击）、雷电之躯、风暴之子、忧郁先知、风暴预兆，闪电束与召唤风暴。
 public static class StormGiant {
  public const string Core="LP_StormGiantCore",BodyGene="LP_StormBody",ChildGene="LP_StormChild",Melancholy="LP_StormMelancholy",OmenGene="LP_StormOmen";
  public const string Trident="LP_StormTrident",Orb="LP_StormOrb",Armor="LP_AbyssCoralArmor",Shackled="LP_Shackled";
  public const float ThunderEmp=15f,TridentFactor=1.5f,ChildMelee=1.15f,ChildMove=1.1f,BoltFlame=12f,BoltEmp=20f,OmenPoints=1000f,OmenDays=.5f;
  public const int BoltRange=15,StrikeInterval=2*GenDate.TicksPerHour,StrikeRadius=3,MelancholyRadius=5;
  public static readonly string[] SkinGenes={"LP_SkinStormViolet","LP_SkinStormTeal"},HairGenes={"LP_HairStormSilver","LP_HairStormBlue"};
  public static bool Active(Pawn p)=>Races124.Has(p,Core);
  public static bool Has(Pawn p,string gene)=>Races124.Has(p,gene);
  public static bool IsShackled(Pawn p)=>Items.Has(p,Shackled);
  public static bool Adult(Pawn p)=>p?.ageTracker!=null && p.ageTracker.AgeBiologicalYearsFloat>=GiantKin.AdultAge;

  // ---------- 外观：肤色、发色各两种，生成时随机一种作为内源基因（照 1.33.0 六臂蛇人鳞色的写法），遗传时随父母 ----------
  public static void EnsureAppearance(Pawn p){
   if(p?.genes==null || !Active(p))return;bool changed=false;
   foreach(var set in new[]{SkinGenes,HairGenes}){
    var defs=set.Select(Races124.Gene).ToArray();if(defs.Any(d=>d==null))continue;
    if(defs.Any(d=>p.genes.GetGene(d)!=null))continue;
    p.genes.AddGene(defs.RandomElement(),false);changed=true;
   }
   if(changed)p.Drawer?.renderer?.SetAllGraphicsDirty();
  }

  // ---------- 风暴之子、忧郁先知 ----------
  static readonly HashSet<string> stormWeathers=new HashSet<string>{"Rain","RainyThunderstorm","DryThunderstorm","SnowHard"};
  public static bool Stormy(Map map)=>map?.weatherManager?.curWeather!=null && stormWeathers.Contains(map.weatherManager.curWeather.defName);
  public static bool Rainy(Map map)=>(map?.weatherManager?.curWeather?.rainRate??0f)>0f;
  public static bool ChildActive(Pawn p)=>p!=null && p.Spawned && Stormy(p.Map) && Has(p,ChildGene); // 1.58.6：先看天气（便宜），再查基因
  public static bool Alone(Pawn p){
   foreach(var o in Races124.PawnsNear(p.Map,p.Position,MelancholyRadius))if(o!=p && o.RaceProps.Humanlike)return false;
   return true;
  }
  // 常驻；雨天或5格内没有别人时不生效。
  public static bool Foreboding(Pawn p)=>p!=null && p.Spawned && Has(p,Melancholy) && !Rainy(p.Map) && !Alone(p);

  // ---------- 雷电之躯：EMP 全免；落雷（WeatherEvent_LightningStrike 的爆炸）不伤自己 ----------
  [ThreadStatic] public static bool striking,inLightning;
  static readonly ConditionalWeakTable<Explosion,object> strikes=new ConditionalWeakTable<Explosion,object>();
  public static void TagStrike(Explosion e){if(e!=null)strikes.GetValue(e,_=>new object());}
  public static bool IsStrike(Explosion e)=>e!=null && strikes.TryGetValue(e,out _);
  public static bool Grounded(Pawn p,DamageInfo d)=>d.Def!=null && Has(p,BodyGene) && (d.Def==DamageDefOf.EMP || inLightning);

  // ---------- 雷霆一击：每次近战命中追加 EMP 15（手持风暴三叉戟 ×1.5） ----------
  public static float ThunderAmount(Pawn p)=>ThunderEmp*(Items.Holds(p,Trident)?TridentFactor:1f);
  public static bool Thunder(Pawn caster,Thing target){
   if(!Active(caster) || IsShackled(caster) || target==null || target.Destroyed || !target.Spawned)return false;
   target.TakeDamage(new DamageInfo(DamageDefOf.EMP,ThunderAmount(caster),0f,-1f,caster));
   if(target.Spawned)FleckMaker.ThrowLightningGlow(target.DrawPos,target.Map,.8f);
   return true;
  }

  // ---------- 闪电束：直线15格，路径上每个目标火焰12 + EMP 20 ----------
  public static IntVec3 BoltEnd(Pawn caster,IntVec3 aim){
   var map=caster.Map;var dir=(aim-caster.Position).ToVector3();dir.y=0;if(dir.sqrMagnitude<.01f)return IntVec3.Invalid;dir.Normalize();
   var end=caster.Position;
   for(int i=1;i<=BoltRange;i++){var c=(caster.Position.ToVector3Shifted()+dir*i).ToIntVec3();if(!c.InBounds(map))break;end=c;}
   return end==caster.Position?IntVec3.Invalid:end;
  }
  // 路径上的格子（不含起点）；遇到墙这样的不可通行建筑时连同那一格一起打，然后停下。
  public static List<IntVec3> BoltCells(Map map,IntVec3 from,IntVec3 to){
   var list=new List<IntVec3>();
   foreach(var c in GenSight.BresenhamCellsBetween(from,to)){
    if(c==from || !c.InBounds(map))continue;list.Add(c);
    var e=c.GetEdifice(map);if(e!=null && e.def.passability==Traversability.Impassable)break;
   }
   return list;
  }
  public static int BoltHit(Thing caster,Map map,IntVec3 from,IntVec3 to){
   if(map==null)return 0;int n=0;
   foreach(var c in BoltCells(map,from,to)){
    FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),map,1.2f);
    foreach(var t in c.GetThingList(map).ToList()){
     if(t==caster || t.Destroyed || !(t is Pawn || t is Building))continue;
     t.TakeDamage(new DamageInfo(DamageDefOf.Flame,BoltFlame,0f,-1f,caster));
     if(!t.Destroyed)t.TakeDamage(new DamageInfo(DamageDefOf.EMP,BoltEmp,0f,-1f,caster));
     n++;
    }
   }
   return n;
  }
  public static bool LaunchBolt(Pawn caster,IntVec3 aim){
   var map=caster?.Map;if(map==null)return false;var end=BoltEnd(caster,aim);if(!end.IsValid)return false;
   var def=Items.Thing("LP_StormBolt");
   if(def==null){BoltHit(caster,map,caster.Position,end);return true;}
   var bolt=(Projectile)GenSpawn.Spawn(def,caster.Position,map);
   bolt.Launch(caster,caster.DrawPos,end,end,ProjectileHitFlags.None,false,null,null);
   return true;
  }

  // ---------- 召唤风暴：地图转雷雨并锁定1天，期间每2小时在随机一名敌人3格内落雷 ----------
  public static WeatherDef W(string name)=>DefDatabase<WeatherDef>.GetNamedSilentFail(name);
  public static GameCondition_LPWeather CallWeather(Map map,WeatherDef weather,float days,bool strikes,Faction side=null){
   if(map==null || weather==null)return null;
   var def=DefDatabase<GameConditionDef>.GetNamedSilentFail(strikes?"LP_StormCalled":"LP_WeatherCalled");if(def==null)return null;
   // 同一张地图只留一个召来的天气（后来的覆盖先来的）。
   foreach(var old in map.gameConditionManager.ActiveConditions.OfType<GameCondition_LPWeather>().ToList())old.End();
   var cond=(GameCondition_LPWeather)GameConditionMaker.MakeCondition(def,(int)(days*GenDate.TicksPerDay));
   cond.weather=weather;cond.strikes=strikes;cond.side=side??Faction.OfPlayer;
   map.gameConditionManager.RegisterCondition(cond);
   if(map.weatherManager.curWeather!=weather)map.weatherManager.TransitionTo(weather);
   return cond;
  }
  public static GameCondition_LPWeather SummonStorm(Pawn caster)=>CallWeather(caster?.Map,W("RainyThunderstorm"),1f,true,caster?.Faction);
  // 随机一名敌对角色3格内的露天格子落一道雷。
  public static IntVec3 StrikeEnemy(Map map,Faction side){
   if(map==null)return IntVec3.Invalid;side=side??Faction.OfPlayer;
   var foe=map.mapPawns.AllPawnsSpawned.Where(p=>!p.Dead && p.HostileTo(side)).RandomElementWithFallback();if(foe==null)return IntVec3.Invalid;
   if(!CellFinder.TryFindRandomCellNear(foe.Position,map,StrikeRadius,c=>c.InBounds(map) && !c.Roofed(map),out var cell))return IntVec3.Invalid;
   map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map,cell));
   return cell;
  }

  // ---------- 风暴预兆 ----------
  public static bool OmenOn=>LichMod.Settings?.stormOmen!=false;
  public static Pawn OmenGiant(Map map)=>map?.mapPawns.FreeColonistsSpawned.FirstOrDefault(p=>Has(p,OmenGene) && Adult(p));
 }

 public class Gene_StormGiant : Gene {
  public override void PostAdd(){
   base.PostAdd();GiantKin.Invalidate(pawn);StormGiant.EnsureAppearance(pawn);
   if(pawn?.apparel!=null)foreach(var a in pawn.apparel.WornApparel.ToArray())if(!GiantGear.CanWear(pawn,a.def))pawn.apparel.TryDrop(a);
  }
  public override void PostRemove(){GiantGear.Release(pawn);base.PostRemove();}
 }
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class StormGiantGenerated {
  static void Postfix(Pawn __result){if(__result!=null && StormGiant.Active(__result))StormGiant.EnsureAppearance(__result);}
 }

 // 召来的天气：原版“强制天气”状况，另存天气、是否落雷和站在哪一边（原版这个类不存 weather）。
 public class GameCondition_LPWeather : GameCondition_ForceWeather {
  public bool strikes;public Faction side;
  public override void GameConditionTick(){
   base.GameConditionTick();
   if(!strikes || TicksPassed<=0 || TicksPassed%StormGiant.StrikeInterval!=0)return;
   foreach(var map in AffectedMaps)StormGiant.StrikeEnemy(map,side);
  }
  public override void ExposeData(){
   base.ExposeData();Scribe_Defs.Look(ref weather,"lpWeather");Scribe_Values.Look(ref strikes,"lpStrikes");Scribe_References.Look(ref side,"lpSide");
  }
 }

 // 风暴之子：风雨天近战伤害 +15%、移速 +10%。
 public class StatPart_StormChild : StatPart {
  float Factor=>parentStat==StatDefOf.MoveSpeed?StormGiant.ChildMove:StormGiant.ChildMelee;
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && StormGiant.ChildActive(p))val*=Factor;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && StormGiant.ChildActive(p)?"风暴之子（风雨天）：×"+Factor.ToStringPercent():null;
 }
 [StaticConstructorOnStartup]
 public static class StormGiantStartup {
  static StormGiantStartup(){
   foreach(var stat in new[]{StatDefOf.MeleeDamageFactor,StatDefOf.MoveSpeed}){if(stat.parts==null)stat.parts=new List<StatPart>();if(!stat.parts.Any(p=>p is StatPart_StormChild))stat.parts.Add(new StatPart_StormChild{parentStat=stat});}
  }
 }
 public class ThoughtWorker_StormSinging : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>StormGiant.ChildActive(p)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }
 public class ThoughtWorker_StormForeboding : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>StormGiant.Foreboding(p)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }

 // 雷电之躯：落雷的爆炸在 DoStrike 里生成，给它做个记号；爆炸逐格结算时（AffectCell）标记“正在落雷”。
 [HarmonyPatch(typeof(WeatherEvent_LightningStrike),nameof(WeatherEvent_LightningStrike.DoStrike))]
 public static class StormLightningStrike {
  static void Prefix(out bool __state){__state=StormGiant.striking;StormGiant.striking=true;}
  static void Finalizer(bool __state){StormGiant.striking=__state;}
 }
 [HarmonyPatch(typeof(Explosion),nameof(Explosion.StartExplosion))]
 public static class StormLightningTag {
  static void Prefix(Explosion __instance){if(StormGiant.striking)StormGiant.TagStrike(__instance);}
 }
 [HarmonyPatch(typeof(Explosion),"AffectCell")]
 public static class StormLightningCell {
  static void Prefix(Explosion __instance,out bool __state){__state=StormGiant.inLightning;StormGiant.inLightning=StormGiant.IsStrike(__instance);}
  static void Finalizer(bool __state){StormGiant.inLightning=__state;}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class StormBodyGrounded {
  static bool Prefix(Pawn __instance,ref DamageInfo __0,ref bool __1){
   if(!StormGiant.Grounded(__instance,__0))return true;
   __1=true;Items.Mote(__instance,__0.Def==DamageDefOf.EMP?"雷电之躯":"落雷无伤",new Color(.72f,.78f,1f));return false;
  }
 }
 // 雷霆一击（同霜巨人斧的寒霜迟缓，在近战结算之后追加）。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class StormThunderStrike {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){var p=__instance.CasterPawn;if(p!=null && StormGiant.Active(p))StormGiant.Thunder(p,target.Thing);}
 }
 // 镣铐：不能用任何能力。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.GizmoDisabled))]
 public static class ShackledAbilityGizmo {
  static void Postfix(RimWorld.Ability __instance,ref string reason,ref bool __result){if(!__result && StormGiant.IsShackled(__instance.pawn)){reason="戴着镣铐，用不了能力。";__result=true;}}
 }
 [HarmonyPatch(typeof(RimWorld.Ability),"get_CanCast")]
 public static class ShackledAbilityCast {
  static void Postfix(RimWorld.Ability __instance,ref AcceptanceReport __result){if(__result.Accepted && StormGiant.IsShackled(__instance.pawn))__result="戴着镣铐，用不了能力。";}
 }
 // 镣铐：被俘或加入殖民地后自动除去。
 public class Hediff_Shackled : HediffWithComps {
  public override bool ShouldRemove=>base.ShouldRemove || pawn==null || pawn.Faction==Faction.OfPlayer || pawn.IsPrisonerOfColony || pawn.HostFaction==Faction.OfPlayer;
 }

 // ---------- 能力 ----------
 public class CompProperties_StormAbility : CompProperties_AbilityEffect {
  public string effect;
  public CompProperties_StormAbility(){compClass=typeof(CompStormAbility);}
 }
 public class CompStormAbility : CompAbilityEffect {
  public CompProperties_StormAbility Props=>(CompProperties_StormAbility)props;
  Pawn Caster=>parent.pawn;
  public override bool GizmoDisabled(out string reason){
   if(Props.effect=="storm" && !StormGiant.Adult(Caster)){reason="只有成年的风暴巨人能召唤风暴。";return true;}
   return base.GizmoDisabled(out reason);
  }
  public string Invalid(LocalTargetInfo target){
   var map=Caster?.Map;if(map==null)return "施法者不在地图上。";
   if(Props.effect=="bolt")return !target.Cell.IsValid || !target.Cell.InBounds(map)?"需要地图上的目标位置。":target.Cell==Caster.Position?"要朝一个方向放出闪电。":null;
   return null;
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   string reason=Invalid(target);
   if(reason!=null && throwMessages)Messages.Message(reason,MessageTypeDefOf.RejectInput,false);
   return reason==null && base.Valid(target,throwMessages);
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   if(Invalid(target)!=null)return;
   switch(Props.effect){
    case "bolt":StormGiant.LaunchBolt(Caster,target.Cell);break;
    case "storm":StormGiant.SummonStorm(Caster);Messages.Message(Caster.LabelShort+"召唤了风暴：雷雨将持续一天，雷电会劈向敌人。",Caster,MessageTypeDefOf.NeutralEvent,false);break;
   }
  }
  public override void DrawEffectPreview(LocalTargetInfo target){
   if(Props.effect!="bolt" || Caster?.Map==null || !target.Cell.IsValid)return;
   var end=StormGiant.BoltEnd(Caster,target.Cell);if(end.IsValid)GenDraw.DrawFieldEdges(StormGiant.BoltCells(Caster.Map,Caster.Position,end));
  }
 }
 // 闪电束的投射物：飞越不被拦截，落点结算时打整条路径（照 1.24.0 炎魔火鞭的投射物写法）。
 public class Projectile_StormBolt : Projectile {
  protected override void Impact(Thing hitThing,bool blockedByShield=false){
   var map=Map;var from=origin.ToIntVec3();var to=Position;var l=launcher;
   StormGiant.BoltHit(l,map,from,to);
   base.Impact(hitThing,blockedByShield);
  }
 }

 // 风暴宝珠：捏碎后地图转雷雨1天（不落雷）。
 public class CompUseEffect_StormOrb : CompUseEffect {
  public override AcceptanceReport CanBeUsedBy(Pawn p)=>p?.Map==null?"要在地图上使用。":base.CanBeUsedBy(p);
  public override void DoEffect(Pawn usedBy){
   base.DoEffect(usedBy);
   if(StormGiant.CallWeather(usedBy.Map,StormGiant.W("RainyThunderstorm"),1f,false)!=null)Messages.Message(usedBy.LabelShort+"捏碎了风暴宝珠：乌云翻滚，雷雨将持续一天。",usedBy,MessageTypeDefOf.NeutralEvent,false);
  }
 }

 // 塞伊远征军 ≥4000 点的战斗袭击 10% 带一名被奴役的风暴巨人（戴镣铐，顶掉3名普通随从）。
 public static class StormGiantRaid {
  public const float MinPoints=4000f,Chance=.1f;
  public static float? TestRoll;
  static PawnKindDef kind;
  public static PawnKindDef Kind=>kind??(kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FH_EnslavedStormGiant"));
  public static int Apply(List<Pawn> list,PawnGroupMakerParms parms){
   var k=Kind;if(k==null || list==null || parms==null)return 0;
   if(parms.groupKind!=PawnGroupKindDefOf.Combat || parms.points<MinPoints || list.Any(x=>x.kindDef==k) || (TestRoll??Rand.Value)>=Chance)return 0;
   var g=PawnGenerator.GeneratePawn(new PawnGenerationRequest(k,parms.faction,PawnGenerationContext.NonPlayer,parms.tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true,inhabitant:parms.inhabitants));
   Items.Give(g,StormGiant.Shackled);
   list.Insert(0,g);FrostGiantRaid.Spare(list,3);
   return 1;
  }
 }

 // 风暴巨人先知的预言（巨人迁徙）：“听他说完”后30天内，第一次≥1000点的敌对袭击提前1天预警（Harper.TryDelay 里用掉）。
 public class StormGiantState : GameComponent {
  public const int ProphecyDays=30;
  public int prophecyUntil=-1;
  public StormGiantState(Game game){}
  public static StormGiantState Get=>GameComp<StormGiantState>.Get();
  public bool ProphecyPending=>prophecyUntil>0 && Find.TickManager.TicksGame<prophecyUntil;
  public void Listen(){prophecyUntil=Find.TickManager.TicksGame+ProphecyDays*GenDate.TicksPerDay;}
  public void UseProphecy(){prophecyUntil=-1;}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref prophecyUntil,"lpStormProphecyUntil",-1);}
 }

 // 1.34.0 设置：风暴预兆。
 public partial class Settings {
  public bool stormOmen=true;
 }
}
