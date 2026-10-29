using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public static class FleshImplants {
  public static bool Has(Pawn p,string name){
   var set=p?.health?.hediffSet;if(set==null)return false;
   HediffDef def=name switch {"ShadowLeg"=>ShadowLeg.Def,"DeadPersonality"=>DeadPersonality.Def,"BlindSight"=>BlindSight.Def,"WitheringHand"=>WitheringHand.Def,_=>DefCache<HediffDef>.Get("LP_"+name)};
   if(def==null)return false;
   var list=set.hediffs;for(int i=0;i<list.Count;i++){var h=list[i];if(h.def==def && (h.Part==null || !set.PartIsMissing(h.Part)))return true;}return false;
  }
  // 1.58.6：暗影之腿在寻路每一步、可达性、开门判断里都会查：按 Def 引用比较，不再逐个比较 defName 字符串。
  static readonly DefRef<HediffDef> ShadowLeg=new DefRef<HediffDef>("LP_ShadowLeg"),DeadPersonality=new DefRef<HediffDef>("LP_DeadPersonality"),BlindSight=new DefRef<HediffDef>("LP_BlindSight"),WitheringHand=new DefRef<HediffDef>("LP_WitheringHand");
  public static void SyncEye(Pawn p){
   if(p?.abilities==null)return;
   var def=Necro.Def<AbilityDef>("IceRay");bool has=Has(p,"BlindSight");
   if(has && p.abilities.GetAbility(def)==null)p.abilities.GainAbility(def);
   else if(!has && p.abilities.GetAbility(def)!=null)p.abilities.RemoveAbility(def);
  }
  public static void Chill(Pawn p,float amount){
   if(p==null || p.Dead || !p.RaceProps.IsFlesh || NecromancyGenes.HasBody(p))return;
   HealthUtility.AdjustSeverity(p,HediffDefOf.Hypothermia,amount);
  }
 }
 public class HediffCompProperties_FleshEye : HediffCompProperties {public HediffCompProperties_FleshEye(){compClass=typeof(HediffComp_FleshEye);}}
 public class HediffComp_FleshEye : HediffComp {
  int elapsed;
  public override void CompPostPostAdd(DamageInfo? dinfo){base.CompPostPostAdd(dinfo);FleshImplants.SyncEye(Pawn);}
  public override void CompPostPostRemoved(){base.CompPostPostRemoved();FleshImplants.SyncEye(Pawn);}
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){base.CompPostTickInterval(ref severityAdjustment,delta);elapsed+=delta;if(elapsed>=250){elapsed=0;FleshImplants.SyncEye(Pawn);}}
 }
 public class ImplantAbility : Ability {
  public ImplantAbility(Pawn pawn):base(pawn){}public ImplantAbility(Pawn pawn,AbilityDef def):base(pawn,def){}
  public override bool GizmoDisabled(out string reason){if(!FleshImplants.Has(pawn,"BlindSight")){reason="需要已安装的盲目之视。";return true;}return base.GizmoDisabled(out reason);}
  public override bool Activate(LocalTargetInfo target,LocalTargetInfo dest){
   if(!FleshImplants.Has(pawn,"BlindSight") || pawn.Dead || pawn.Downed || !pawn.Spawned || OnCooldown || !verb.CanHitTarget(target) || !EffectComps.All(c=>c.Valid(target,false)))return false;
   return base.Activate(target,dest);
  }
 }
 public class CompProperties_IceRay : CompProperties_AbilityEffect {public CompProperties_IceRay(){compClass=typeof(CompIceRay);}}
 public class CompIceRay : CompAbilityEffect {
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false)=>target.Pawn!=null && target.Pawn!=parent.pawn && !target.Pawn.Dead && target.Pawn.Spawned && target.Pawn.Map==parent.pawn.Map;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   if(!Valid(target))return;var p=target.Pawn;var map=p.Map;var cell=p.Position;var drawPos=p.DrawPos;
   // 目标可能被这一击冻死，先记下位置再造成伤害。
   p.TakeDamage(new DamageInfo(DamageDefOf.Frostbite,12f,0f,-1,parent.pawn));FleshImplants.Chill(p,.12f);
   GenDraw.DrawLineBetween(parent.pawn.DrawPos,drawPos,SimpleColor.Cyan);FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,1f);
  }
 }
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class WitheringArmHit {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target,DamageWorker.DamageResult __result){if(__result.totalDamageDealt>0 && FleshImplants.Has(__instance.CasterPawn,"WitheringHand"))FleshImplants.Chill(target.Pawn,.06f);}
 }
 [HarmonyPatch(typeof(SkillRecord),"get_LearningSaturatedToday")]
 public static class UndeadPersonalitySaturation {static void Postfix(SkillRecord __instance,ref bool __result){if(__result && FleshImplants.Has(__instance.Pawn,"DeadPersonality"))__result=false;}}
 [HarmonyPatch(typeof(SkillRecord),nameof(SkillRecord.Interval))]
 public static class UndeadPersonalityMemory {static bool Prefix(SkillRecord __instance)=>!FleshImplants.Has(__instance.Pawn,"DeadPersonality");}
 // Use the ordinary bill pipeline, with discrete meat counts instead of nutrition.
 public class FleshImplantRecipe : RecipeWorker { }
 [HarmonyPatch(typeof(Bill),nameof(Bill.IsFixedOrAllowedIngredient),new[]{typeof(Thing)})]
 public static class FreshImplantMeat {
  static void Postfix(Bill __instance,Thing __0,ref bool __result){if(__result && __instance.recipe.Worker is FleshImplantRecipe && __0.def.IsWithinCategory(ThingCategoryDefOf.MeatRaw))__result=ConstructSeed.RawMeat(__0);}
 }
}
