using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.57.0 普通种族的战斗与属性补丁。数值与判断都在 Subraces（CommonRaceSubraces.cs）。
 // 远程命中：改 ShotReport 的"射手与距离"系数（原版用它决定实际命中），同时影响瞄准提示；近战命中：改 Verb_MeleeAttack.GetNonMissChance。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class SubraceShotReport {
  static readonly AccessTools.StructFieldRef<ShotReport,float> factor=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromShooterAndDist");
  static void Postfix(Thing caster,LocalTargetInfo target,ref ShotReport __result){
   float f=Subraces.HitFactor(caster as Pawn,target.Thing,false)*Subraces.TargetFactor(target.Thing as Pawn);
   if(f!=1f){ref float v=ref factor(ref __result);v=Mathf.Clamp01(v*f);}
  }
 }
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class SubraceMeleeHit {
  static void Postfix(Verb_MeleeAttack __instance,LocalTargetInfo target,ref float __result){
   float f=Subraces.HitFactor(__instance.CasterPawn,target.Thing,true);
   if(f!=1f)__result=Mathf.Clamp01(__result*f);
  }
 }
 // 卓尔：蛛后之毒（近战命中后 15%，机械体无效，重复中毒只刷新时间）。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class DrowPoisonStrike {
  public static float? TestRoll;
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){
   var caster=__instance.CasterPawn;if(!(target.Thing is Pawn t) || t.Dead || !Subraces.Has(caster,"LP_DrowPoison"))return;
   if((TestRoll??Rand.Value)>=Subraces.PoisonChance)return;
   Poison(t);
  }
  public static bool Poison(Pawn t){
   var def=Subraces.H("LP_DrowVenom");if(def==null || t==null || t.Dead || t.RaceProps.IsMechanoid)return false;
   var h=t.health.hediffSet.GetFirstHediffOfDef(def)??t.health.AddHediff(def);
   var c=h.TryGetComp<HediffComp_Disappears>();if(c!=null)c.ticksToDisappear=c.disappearsAfterTicks;
   return true;
  }
 }
 // 地精：小个子的怒火（远程）——子弹打体型更大的目标伤害 ×1.15。
 [HarmonyPatch(typeof(Projectile),"get_DamageAmount")]
 public static class GoblinFuryProjectile {
  static void Postfix(Projectile __instance,Thing ___launcher,ref int __result){
   if(!(___launcher is Pawn p) || p.genes==null)return;
   float f=Subraces.DamageFactor(p,__instance.intendedTarget.Thing,false);
   if(f!=1f)__result=Mathf.Max(1,Mathf.RoundToInt(__result*f));
  }
 }
 // 地精：灵巧逃脱——被近战命中后 2 秒移速 ×1.3（刷新不叠加）。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.PostApplyDamage))]
 public static class GoblinNimbleEscape {
  static void Postfix(Pawn ___pawn,DamageInfo dinfo,float totalDamageDealt){
   if(totalDamageDealt<=0 || ___pawn==null || ___pawn.Dead || !Subraces.Has(___pawn,"LP_GoblinNimble"))return;
   if(dinfo.Def==null || dinfo.Def.isExplosive || dinfo.Def.isRanged || dinfo.Weapon?.IsRangedWeapon==true || !(dinfo.Instigator is Pawn))return;
   Escape(___pawn);
  }
  public static void Escape(Pawn p){
   var def=Subraces.H("LP_GoblinEscape");if(def==null)return;
   var h=p.health.hediffSet.GetFirstHediffOfDef(def)??p.health.AddHediff(def);
   var c=h.TryGetComp<HediffComp_Disappears>();if(c!=null)c.ticksToDisappear=Subraces.EscapeTicks;
  }
 }
 // 大地精：铁律——征召中不会因心情精神崩溃。
 [HarmonyPatch(typeof(MentalBreaker),nameof(MentalBreaker.TryDoRandomMoodCausedMentalBreak))]
 public static class HobgoblinDiscipline {
  static bool Prefix(Pawn ___pawn,ref bool __result){
   if(___pawn?.Drafted!=true || !Subraces.Has(___pawn,"LP_HobgoblinDiscipline"))return true;
   __result=false;return false;
  }
 }
 // 半身人勇敢（恐惧类精神状态不发生）；森林侏儒驯服小动物失败时小动物不反扑。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class SubraceMentalState {
  static bool Prefix(MentalStateDef __0,Pawn ___pawn,ref bool __result){
   if(__0==null || ___pawn==null)return true;
   if(Subraces.calmTaming!=null && ___pawn==Subraces.calmTaming && (__0==MentalStateDefOf.Manhunter || __0==MentalStateDefOf.ManhunterPermanent)){__result=false;return false;}
   if(HeroismFearImmunity.Fear(__0) && Subraces.IsHalfling(___pawn)){Items.Mote(___pawn,"勇敢！",new Color(1f,.85f,.45f));__result=false;return false;}
   return true;
  }
 }
 [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt),nameof(InteractionWorker_RecruitAttempt.Interacted))]
 public static class GnomeCalmTaming {
  static void Prefix(Pawn initiator,Pawn recipient){if(recipient!=null && recipient.RaceProps.Animal && Subraces.TameFactor(initiator,recipient)>1f)Subraces.calmTaming=recipient;}
  static void Finalizer(){Subraces.calmTaming=null;}
 }
 // 半身人勇敢：恐惧类负面心情 ×0.5。
 [HarmonyPatch(typeof(Thought),nameof(Thought.MoodOffset))]
 public static class HalflingBraveMood {
  static void Postfix(Thought __instance,ref float __result){
   if(__result>=0f || !Subraces.FearThought(__instance.def) || !Subraces.IsHalfling(__instance.pawn))return;
   __result*=.5f;
  }
 }
 // 狗头人陷阱匠：记下狗头人亲手建成的陷阱（存档保存 thingIDNumber）。
 [HarmonyPatch(typeof(Frame),nameof(Frame.CompleteConstruction))]
 public static class KoboldTrapBuilt {
  static void Prefix(Frame __instance,Pawn worker,out (Map map,IntVec3 cell,ThingDef def) __state){
   __state=default;var td=__instance.def.entityDefToBuild as ThingDef;
   if(td?.building?.isTrap==true && Subraces.Has(worker,"LP_KoboldTrapsmith"))__state=(__instance.Map,__instance.Position,td);
  }
  static void Postfix((Map map,IntVec3 cell,ThingDef def) __state){
   if(__state.map==null)return;var t=__state.cell.GetThingList(__state.map).FirstOrDefault(x=>x.def==__state.def);
   if(t!=null)Subraces.State?.koboldTraps.Add(t.thingIDNumber);
  }
 }

 // ---------- 属性 ----------
 // 矮人护甲训练：衣甲（和手持装备）的负面移速修正只算一半。
 public class StatPart_DwarfArmorTraining : StatPart {
  public static float Penalty(Pawn p){
   float s=0f;if(p.apparel!=null)foreach(var a in p.apparel.WornApparel){float o=StatWorker.StatOffsetFromGear(a,StatDefOf.MoveSpeed);if(o<0f)s+=o;}
   var eq=p.equipment?.Primary;if(eq!=null){float o=StatWorker.StatOffsetFromGear(eq,StatDefOf.MoveSpeed);if(o<0f)s+=o;}
   return s;
  }
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Subraces.Has(p,Subraces.ArmorTraining))val-=.5f*Penalty(p);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p) || !Subraces.Has(p,Subraces.ArmorTraining))return null;float b=-.5f*Penalty(p);return b<=0f?null:"矮人护甲训练：+"+b.ToString("0.##");}
 }
 // 大地精群体之助：全局工作速度 ×(1+3%×身边本方人形数)，最多 ×109%。
 public class StatPart_HobgoblinNumbers : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Subraces.Has(p,"LP_HobgoblinNumbers"))val*=Subraces.HobFactor(p);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p) || !Subraces.Has(p,"LP_HobgoblinNumbers"))return null;float f=Subraces.HobFactor(p);return f==1f?null:"群体之助：×"+f.ToStringPercent();}
 }
 // 狗头人陷阱匠：正在建陷阱时建造速度 ×2。
 public class StatPart_KoboldTrapBuilding : StatPart {
  public static bool BuildingTrap(Pawn p)=>p.CurJob?.targetA.Thing is Frame f && (f.def.entityDefToBuild as ThingDef)?.building?.isTrap==true;
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Subraces.Has(p,Subraces.Trapsmith) && BuildingTrap(p))val*=Subraces.TrapBuildFactor;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Subraces.Has(p,Subraces.Trapsmith) && BuildingTrap(p)?"陷阱匠（建陷阱）：×200%":null;
 }
 // 狗头人建成的陷阱：陷阱伤害 ×1.3。
 public class StatPart_KoboldTrap : StatPart {
  public static bool Marked(Thing t)=>t!=null && Subraces.State?.koboldTraps.Contains(t.thingIDNumber)==true;
  public override void TransformValue(StatRequest req,ref float val){if(Marked(req.Thing))val*=Subraces.TrapFactor;}
  public override string ExplanationPart(StatRequest req)=>Marked(req.Thing)?"狗头人建造：×130%":null;
 }
 // 岩侏儒修补匠：做发条玩具时一般劳动速度再 ×2。
 public class StatPart_TinkerToy : StatPart {
  public static bool MakingToy(Pawn p)=>p.CurJob?.bill?.recipe?.defName=="LP_MakeClockworkToy";
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Subraces.Has(p,Subraces.Tinker) && MakingToy(p))val*=Subraces.TinkerToyFactor;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Subraces.Has(p,Subraces.Tinker) && MakingToy(p)?"修补匠（发条玩具）：×200%":null;
 }

 // ---------- 卓尔：黑暗术 ----------
 public class CompProperties_DrowDarkness : CompProperties_AbilityEffect {
  public float radius=3f;public int durationTicks=720;
  public CompProperties_DrowDarkness(){compClass=typeof(CompAbilityEffect_DrowDarkness);}
 }
 public class CompAbilityEffect_DrowDarkness : CompAbilityEffect {
  public new CompProperties_DrowDarkness Props=>(CompProperties_DrowDarkness)props;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){base.Apply(target,dest);Subraces.CastDarkness(parent.pawn,target.Cell,Props.radius,Props.durationTicks);}
  // 1.63.0：敌方卓尔会自己放（AI）：只对它的敌人密集处（目标格 3 格内至少 3 人）。冷却 1 天，所以每人每战最多一次。
  public override bool AICanTargetNow(LocalTargetInfo target)=>parent.pawn?.Faction!=Faction.OfPlayer && DrowDarknessAI.Cluster(parent.pawn,target.Cell,Props.radius)>=DrowDarknessAI.MinCluster;
 }
}
