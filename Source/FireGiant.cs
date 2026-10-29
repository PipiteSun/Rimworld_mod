using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public static class FireGiant {
  static GeneDef coreGene;
  static readonly string[] appearanceGenes={"Body_Hulk","Skin_DeepRed"};
  public static bool Active(Pawn pawn) {
   if(pawn?.genes==null)return false;
   if(coreGene==null)coreGene=DefDatabase<GeneDef>.GetNamedSilentFail("LP_FireGiantCore");
   return coreGene!=null && pawn.genes.HasActiveGene(coreGene);
  }
  // 1.30.0：穿衣规则改走 GiantGear（GiantApparelExtension）；这张表只剩“熔火锻炉出品的衣甲”的含义（力能龙宝库计价用）。
  public static bool ForgeApparel(ThingDef apparel) => apparel?.defName=="LP_FireGiantTunic" || apparel?.defName=="LP_FireGiantHelmet" || apparel?.defName=="LP_FireGiantArmor" || apparel?.defName==MoltenGuard.Helmet || apparel?.defName==MoltenGuard.Armor;
  public static bool GiantWeapon(ThingDef weapon) => weapon?.defName=="LP_FireGiantMaul" || weapon?.defName==MoltenGuard.Maul;
  public static void ReleaseWeapon(Pawn pawn) {
   if(pawn?.equipment==null)return;
   foreach(var item in pawn.equipment.AllEquipmentListForReading.ToArray()) {
    if(!GiantWeapon(item.def))continue;
    if(pawn.Spawned)pawn.equipment.TryDropEquipment(item,out _,pawn.Position);
    else if(pawn.inventory!=null)pawn.equipment.GetDirectlyHeldThings().TryTransferToContainer(item,pawn.inventory.innerContainer);
   }
  }
  public static bool CanWear(Pawn pawn,ThingDef apparel) => GiantGear.CanWear(pawn,apparel);
  public static void EnsureAppearance(Pawn pawn){
   if(pawn?.genes==null)return;
   bool changed=false;
   foreach(var name in appearanceGenes){
    var def=DefDatabase<GeneDef>.GetNamedSilentFail(name);
    if(def!=null && pawn.genes.GetGene(def)==null){pawn.genes.AddGene(def,true);changed=true;}
   }
   if(changed)pawn.Drawer?.renderer?.SetAllGraphicsDirty();
  }
  public static void Migrate(Pawn pawn){
   if(pawn?.genes==null)return;
   if(coreGene==null)coreGene=DefDatabase<GeneDef>.GetNamedSilentFail("LP_FireGiantCore");
   var core=coreGene==null?null:pawn.genes.GetGene(coreGene) as Gene_FireGiant;
   if(core==null || core.appearanceGenesInitialized)return;
   EnsureAppearance(pawn);
   core.appearanceGenesInitialized=true;
  }
  public static void MigrateLoadedGame(){
   if(Find.Maps!=null)foreach(var pawn in Find.Maps.SelectMany(m=>m.mapPawns.AllPawns.Concat(m.listerThings.AllThings.OfType<Corpse>().Select(c=>c.InnerPawn))).Distinct())Migrate(pawn);
   if(Find.WorldPawns!=null)foreach(var pawn in Find.WorldPawns.AllPawnsAliveOrDead.ToList())Migrate(pawn);
   if(Necro.Registry?.boxes!=null)foreach(var box in Necro.Registry.boxes)Migrate(box?.soul);
  }
  public const float QualityChance=.5f,ForgeFuelFactor=.5f;
  public static QualityCategory ImproveQuality(Pawn pawn,SkillDef skill,QualityCategory quality){
   if(skill!=SkillDefOf.Crafting || quality>=QualityCategory.Masterwork || !Active(pawn) || !Rand.Chance(QualityChance))return quality;
   return quality+1;
  }
  public static bool ForgeUserIsGiant(Thing forge){
   if(forge?.Map==null || !forge.def.hasInteractionCell)return false;
   foreach(var thing in forge.Map.thingGrid.ThingsListAtFast(forge.InteractionCell))
    if(thing is Pawn p && p.CurJob!=null && p.CurJob.targetA.Thing==forge && Active(p))return true;
   return false;
  }
  // 1.30.0：随年龄长大的公式抽到 GiantKin.Size；结果与 1.29.0 相同（成年×2.0）。
  public static float Size(Pawn pawn) => Active(pawn)?GiantKin.Size(pawn,2f):1f;
 }
 public class Gene_FireGiant : Gene {
  public bool appearanceGenesInitialized;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref appearanceGenesInitialized,"fireGiantAppearanceGenesInitialized",false);}
  public override void PostRemove() {
   FireGiant.ReleaseWeapon(pawn);
   GiantGear.Release(pawn);
   base.PostRemove();
  }  public override void PostAdd() {
   base.PostAdd();
   FireGiant.EnsureAppearance(pawn);
   appearanceGenesInitialized=true;
   if(pawn?.apparel==null)return;
   GiantKin.Invalidate(pawn);
   foreach(var item in pawn.apparel.WornApparel.ToArray())
    if(!FireGiant.CanWear(pawn,item.def)) pawn.apparel.TryDrop(item);
  }
 }
 [HarmonyPatch]
 public static class FireGiantWeaponEligibility {
  static System.Reflection.MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(EquipmentUtility)).Find(m=>m.Name==nameof(EquipmentUtility.CanEquip) && m.GetParameters().Length>=3 && m.GetParameters()[2].ParameterType==typeof(string).MakeByRefType());
  static void Postfix(Thing __0,Pawn __1,ref string __2,ref bool __result) {
   if(FireGiant.GiantWeapon(__0?.def) && !FireGiant.Active(__1)){__result=false;__2="只有火巨人能够使用这件武器。";}
   else if(GiantGear.Weapon(__0?.def) && !GiantKin.Is(__1)){__result=false;__2="只有巨人能够使用这件武器。";}
   else if(__result && __0?.def!=null && __0.def.IsApparel && !GiantGear.CanWear(__1,__0.def)){__result=false;__2=GiantGear.WearReason(__1,__0.def);}
  }
 }
 [HarmonyPatch(typeof(Pawn_EquipmentTracker),nameof(Pawn_EquipmentTracker.AddEquipment))]
 public static class FireGiantWeaponEquip {
  static bool Prefix(Pawn ___pawn,ThingWithComps __0) => (!FireGiant.GiantWeapon(__0?.def) || FireGiant.Active(___pawn)) && (!GiantGear.Weapon(__0?.def) || GiantKin.Is(___pawn));
 }
 // 1.30.0：体型来源从火巨人改为所有巨人血统（GiantKin），与巨魔取较大者；火巨人数值不变。
 [HarmonyPatch(typeof(Pawn),"get_BodySize")]
 public static class FireGiantBodySize {
  static void Postfix(Pawn __instance,ref float __result) { __result*=Mathf.Max(GiantKin.Size(__instance),TrollAppearance.Size(__instance)); }
 }
 [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.ScaleFor))]
 public static class FireGiantRenderSize {
  static void Postfix(PawnRenderNode node,PawnDrawParms parms,ref Vector3 __result) {
   if(RenderTags.IsRoot(node?.Props?.tagDef)) __result*=Mathf.Max(Mathf.Max(GiantKin.RenderSize(parms.pawn),TrollAppearance.Size(parms.pawn)),ConstructAppearance.Size(parms.pawn));
  }
 }
 [HarmonyPatch(typeof(ApparelUtility),nameof(ApparelUtility.HasPartsToWear),new Type[]{typeof(Pawn),typeof(ThingDef)})]
 public static class FireGiantApparelEligibility {
  static void Postfix(Pawn __0,ThingDef __1,ref bool __result) {
   if(!FireGiant.CanWear(__0,__1))__result=false;
  }
 }
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Wear),new Type[]{typeof(Apparel),typeof(bool),typeof(bool)})]
 public static class FireGiantApparelWear {
  static bool Prefix(Pawn ___pawn,Apparel __0) => FireGiant.CanWear(___pawn,__0.def);
 }
 [HarmonyPatch(typeof(WorkGiver_DoBill),nameof(WorkGiver_DoBill.JobOnThing),new Type[]{typeof(Pawn),typeof(Thing),typeof(bool)})]
 public static class FireGiantForgeWorker {
  static bool Prefix(Pawn __0,Thing __1,ref Verse.AI.Job __result) {
   if(__1?.def?.defName!="LP_FireGiantForge" || FireGiant.Active(__0))return true;
   __result=null;
   return false;
  }
 }
 [HarmonyPatch]
 public static class FireGiantCraftQuality {
  static System.Reflection.MethodBase TargetMethod() => AccessTools.GetDeclaredMethods(typeof(QualityUtility)).Find(m=>m.Name==nameof(QualityUtility.GenerateQualityCreatedByPawn) && m.GetParameters().Length>=2 && m.GetParameters()[0].ParameterType==typeof(Pawn) && m.GetParameters()[1].ParameterType==typeof(SkillDef));
  static void Postfix(Pawn __0,SkillDef __1,ref QualityCategory __result) { __result=FireGiant.ImproveQuality(__0,__1,__result); }
 }
 [HarmonyPatch(typeof(ThoughtWorker),nameof(ThoughtWorker.CurrentState))]
 public static class FireGiantNakedMood {
  static HashSet<ThoughtDef> nakedThoughts;
  public static bool IsNakedThought(ThoughtDef def){
   if(nakedThoughts==null)nakedThoughts=new HashSet<ThoughtDef>(DefDatabase<ThoughtDef>.AllDefs.Where(t=>t.defName=="Naked" || (t.workerClass!=null && (t.workerClass.Name.Contains("Naked") || t.workerClass.Name.Contains("Uncovered")))));
   return def!=null && nakedThoughts.Contains(def);
  }
  static void Postfix(ThoughtWorker __instance,Pawn __0,ref ThoughtState __result) {
   // 1.34.0：所有巨人血统（火、霜、风暴、云）都不在乎裸体。
   if(__result.Active && IsNakedThought(__instance.def) && (FireGiant.Active(__0) || GiantKin.Is(__0)))__result=ThoughtState.Inactive;
  }
 }
 [HarmonyPatch(typeof(CompRefuelable),nameof(CompRefuelable.Notify_UsedThisTick))]
 public static class FireGiantForgeFuel {
  static readonly AccessTools.FieldRef<CompRefuelable,float> fuel=AccessTools.FieldRefAccess<CompRefuelable,float>("fuel");
  static void Prefix(CompRefuelable __instance,out float __state) { __state=__instance.Fuel; }
  static void Postfix(CompRefuelable __instance,float __state) {
   float used=__state-__instance.Fuel;
   if(used>0f && __instance.parent?.def?.defName=="LP_FireGiantForge" && FireGiant.ForgeUserIsGiant(__instance.parent))fuel(__instance)+=used*(1f-FireGiant.ForgeFuelFactor);
  }
 }
}
