using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.22.0 奇物。戒指、护符、伊奥恩石用自建的“饰品”服装层 LP_Accessory（本模组环境下的 VEF 没有饰品层），不占其他装备位置。
 public static class ItemWondrous {
  public const float BagBurstChance=.02f,CloakFactor=.85f,CloakElfFactor=.7f,IounLearn=.5f;
  public const int MaxIoun=2;
  public static readonly SkillDef[] IounSkills={SkillDefOf.Shooting,SkillDefOf.Melee,SkillDefOf.Construction,SkillDefOf.Medicine,SkillDefOf.Intellectual,SkillDefOf.Social,SkillDefOf.Crafting};
  // 探测魔杖：放在地图储存区（库存区、货架等）里就会揭示整张地图的隐形者。
  public static bool WandStored(Map map){var d=Items.Thing("LP_WandDetection");if(d==null || map==null)return false;foreach(var t in map.listerThings.ThingsOfDef(d))if(t.Spawned && t.IsInAnyStorage())return true;return false;}
  // 次元袋：受到火焰或锐器伤害时 roll<2% 爆开，袋子毁掉，背包里的东西掉一地。
  public static bool BurstDamage(DamageDef d)=>d!=null && (d==DamageDefOf.Flame || d==DamageDefOf.Burn || d.armorCategory==DamageArmorCategoryDefOf.Sharp);
  public static bool TryBurst(Pawn p,DamageInfo dinfo,float roll){
   var bag=Items.Worn(p,"LP_BagOfHolding");if(bag==null || !BurstDamage(dinfo.Def) || roll>=BagBurstChance)return false;
   p.apparel.Remove(bag);bag.Destroy();
   if(p.Spawned)p.inventory?.DropAllNearPawn(p.Position,false,true);
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"的次元袋被撕破，里面的东西撒了一地。",p,MessageTypeDefOf.NegativeEvent,false);
   return true;
  }
  // 精灵斗篷：站着不动时被远程命中率 -15%（精灵 -30%）。
  static readonly DefRef<ThingDef> ElvenCloak=new DefRef<ThingDef>("LP_CloakElvenkind");
  public static float CloakFactorFor(Pawn target){
   if(target==null || Items.Worn(target,ElvenCloak.Def)==null || target.pather?.MovingNow==true)return 1f;
   return Items.IsElf(target)?CloakElfFactor:CloakFactor;
  }
  // 健康护符：免疫疾病（会产生免疫力的病和会让人“生病”的病）。
  public static bool Disease(HediffDef d)=>d!=null && !typeof(Hediff_Addiction).IsAssignableFrom(d.hediffClass) && (d.makesSickThought || d.HasComp(typeof(HediffComp_Immunizable)));
  // 伊奥恩石：对应技能学习速度+50%，戴两颗同技能就是+100%。
  public static float LearnFactor(Pawn p,SkillDef skill){
   float f=1f;var worn=p?.apparel?.WornApparel;if(worn==null)return f;
   for(int i=0;i<worn.Count;i++){var c=worn[i].GetComp<CompIounStone>();if(c!=null && c.skill==skill)f+=IounLearn;}
   return f;
  }
  public static bool IsIoun(ThingDef d)=>d?.defName=="LP_IounStone";
 }
 public class CompProperties_IounStone : CompProperties {public CompProperties_IounStone(){compClass=typeof(CompIounStone);}}
 public class CompIounStone : ThingComp {
  public SkillDef skill;
  static Material glow;
  public override void PostPostMake(){base.PostPostMake();if(skill==null)skill=ItemWondrous.IounSkills.RandomElement();}
  public override string TransformLabel(string label)=>skill==null?label:label+"（"+skill.label+"）";
  public override string CompInspectStringExtra()=>skill==null?null:"环绕头顶时"+skill.label+"学习速度+50%。";
  public override void PostExposeData(){base.PostExposeData();Scribe_Defs.Look(ref skill,"iounSkill");if(Scribe.mode==LoadSaveMode.PostLoadInit && skill==null)skill=ItemWondrous.IounSkills.RandomElement();}
  // 头顶环绕的小光点。
  public override void CompDrawWornExtras(){
   base.CompDrawWornExtras();var p=(parent as Apparel)?.Wearer;if(p==null || !p.Spawned)return;
   if(glow==null)glow=MaterialPool.MatFrom("Items/Wondrous/IounGlow",ShaderDatabase.MoteGlow,new Color(1f,1f,1f,.85f));
   int idx=p.apparel.WornApparel.Where(a=>ItemWondrous.IsIoun(a.def)).ToList().IndexOf((Apparel)parent);
   float t=(Find.TickManager.TicksGame+idx*60)*.05f;var pos=p.DrawPos+new Vector3(Mathf.Cos(t)*.35f,.06f,.65f+Mathf.Sin(t)*.12f);pos.y=AltitudeLayer.MoteOverhead.AltitudeFor();
   Graphics.DrawMesh(MeshPool.plane025,Matrix4x4.TRS(pos,Quaternion.identity,Vector3.one*.9f),glow,0);
  }
 }
 // 次元袋爆开。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.PostApplyDamage))]
 public static class BagOfHoldingBurst {
  static void Postfix(Pawn ___pawn,DamageInfo dinfo,float totalDamageDealt){if(totalDamageDealt>0 && ___pawn?.apparel!=null && !___pawn.Dead)ItemWondrous.TryBurst(___pawn,dinfo,Rand.Value);}
 }
 // 精灵斗篷：降低被远程命中的概率。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class ElvenCloakEvasion {
  static readonly AccessTools.StructFieldRef<ShotReport,float> size=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromTargetSize");
  static void Postfix(LocalTargetInfo target,ref ShotReport __result){float f=ItemWondrous.CloakFactorFor(target.Thing as Pawn);if(f!=1f)size(ref __result)*=f;}
 }
 // 精灵靴：走动不会吵醒睡着的人或动物。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.HearClamor))]
 public static class ElvenBootsSilence {
  static readonly DefRef<ThingDef> Boots=new DefRef<ThingDef>("LP_BootsElvenkind");
  static bool Prefix(Thing __0,ClamorDef __1)=>!(__1==ClamorDefOf.Movement && __0 is Pawn walker && Items.Worn(walker,Boots.Def)!=null);
 }
 // 心灵屏障头环：夺心魔读心时反噬施法者。
 [HarmonyPatch(typeof(MindFlayerPowers),nameof(MindFlayerPowers.ReadMind))]
 public static class IntellectFortressBacklash {
  static void Postfix(Pawn caster,Pawn target){
   if(caster==null || caster.Dead || Items.Worn(target,"LP_IntellectFortress")==null)return;
   caster.stances?.stunner?.StunFor(120,target,false,true,false);
   var old=caster.health.hediffSet.GetFirstHediffOfDef(MindFlayer.Blasted);if(old!=null)caster.health.RemoveHediff(old);caster.health.AddHediff(MindFlayer.Blasted);
   Items.Mote(caster,"心灵反噬！",new Color(.85f,.6f,1f));
  }
 }
 // 健康护符：不会染上疾病。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.AddHediff),new[]{typeof(Hediff),typeof(BodyPartRecord),typeof(DamageInfo?),typeof(DamageWorker.DamageResult)})]
 public static class AmuletOfHealth {
  static bool Prefix(Hediff __0,Pawn ___pawn)=>!(__0!=null && ItemWondrous.Disease(__0.def) && Items.Worn(___pawn,"LP_AmuletHealth")!=null);
 }
 // 伊奥恩石：学习加速；可以同时戴两颗，第三颗会顶掉最早的那颗。
 [HarmonyPatch(typeof(Pawn_SkillTracker),nameof(Pawn_SkillTracker.Learn))]
 public static class IounLearning {
  static void Prefix(SkillDef __0,ref float __1,Pawn ___pawn){if(__1>0 && ___pawn?.apparel!=null)__1*=ItemWondrous.LearnFactor(___pawn,__0);}
 }
 [HarmonyPatch(typeof(ApparelUtility),nameof(ApparelUtility.CanWearTogether))]
 public static class IounTogether {
  static void Postfix(ThingDef __0,ThingDef __1,ref bool __result){if(!__result && ItemWondrous.IsIoun(__0) && ItemWondrous.IsIoun(__1))__result=true;}
 }
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Wear),new Type[]{typeof(Apparel),typeof(bool),typeof(bool)})]
 public static class IounLimit {
  static void Prefix(Pawn ___pawn,Apparel __0){
   if(!ItemWondrous.IsIoun(__0?.def) || ___pawn?.apparel==null)return;
   var stones=___pawn.apparel.WornApparel.Where(a=>ItemWondrous.IsIoun(a.def) && a!=__0).ToList();
   for(int i=0;i<=stones.Count-ItemWondrous.MaxIoun;i++){var old=stones[i];if(___pawn.Spawned)___pawn.apparel.TryDrop(old,out _,___pawn.PositionHeld,false);else{___pawn.apparel.Remove(old);___pawn.inventory?.innerContainer.TryAdd(old);}}
  }
 }
}
