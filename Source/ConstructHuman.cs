using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public partial class Hediff_Construct {
  public string legacyThingID;
  public static readonly string[] LegacyAppearanceGenes={"Body_Thin","Skin_SheerWhite","Eyes_Red"};
  static readonly string[] skeletonAppearance={"Body_Thin","Skin_SheerWhite","Eyes_Red","Hair_BaldOnly","Beard_NoBeardOnly"};
  static readonly string[] golemAppearance={"Body_Hulk","Skin_PaleRed","Eyes_Red","Jaw_Heavy","Brow_Heavy","Hair_BaldOnly","Beard_NoBeardOnly"};
  static readonly string[] scholarAppearance={"Body_Thin","Skin_LightGray","Eyes_Red","Hair_BaldOnly","Beard_NoBeardOnly"};
  static readonly string[] stoneAppearance={"Body_Hulk","Skin_SlateGray","Eyes_Red","Jaw_Heavy","Brow_Heavy","Hair_BaldOnly","Beard_NoBeardOnly"};
  public static string[] AppearanceGenesFor(string role)=>role=="Golem"?golemAppearance:role=="StoneGolem"?stoneAppearance:role=="Scholar"?scholarAppearance:skeletonAppearance;
  // 1.58.6：BodySize / HealthScale 每次都要用，原来每次拼 "LP_"+kind 再 GetNamed；按 kind 缓存（kind 变了重查）。
  string legacyKind;ThingDef legacyRace;
  public ThingDef LegacyRace{get{var k=kind;if(legacyRace==null || legacyKind!=k){legacyRace=Necro.Def<ThingDef>(k);legacyKind=k;}return legacyRace;}}
  public static void AddOffset(HediffStage stage,StatDef stat,float value){
   if(stage.statOffsets==null)stage.statOffsets=new List<StatModifier>();
   var existing=stage.statOffsets.FirstOrDefault(m=>m.stat==stat);
   if(existing==null)stage.statOffsets.Add(new StatModifier{stat=stat,value=value});else existing.value+=value;
  }
  public void UpgradeHuman(){
   if(pawn.def==ThingDefOf.Human)return;
   if(pawn.def!=LegacyRace)return;
   // Change the existing pawn in place after save references have resolved.
   // Despawn/re-register so map thing lists never keep it indexed by the old race.
   var map=pawn.Map;var at=pawn.Position;var rotation=pawn.Rotation;bool drafted=pawn.Drafted;
   legacyThingID=pawn.ThingID;
   if(map!=null)pawn.DeSpawn();
   pawn.def=ThingDefOf.Human;
   if(map!=null){GenSpawn.Spawn(pawn,at,map,rotation);if(pawn.drafter!=null)pawn.drafter.Drafted=drafted;}
   Necro.Refresh(pawn);
  }
  public void SeedAppearanceGenes(){
   if(appearanceVersion>=3 || pawn.genes==null)return;
   var legacy=LegacyAppearanceGenes.Select(n=>DefDatabase<GeneDef>.GetNamed(n)).ToList();
   // Version 2 already seeded the shared look. Upgrade only untouched defaults;
   // player-edited cosmetic genes remain authoritative after this version bump.
   var watchedTags=new HashSet<string>(legacy.SelectMany(d=>d.exclusionTags??new List<string>())){"HairStyle","BeardStyle","Jaw"};
   if(appearanceVersion==2 && (!legacy.All(pawn.genes.HasActiveGene) || pawn.genes.GenesListForReading.Any(g=>!legacy.Contains(g.def) && (g.def.exclusionTags?.Any(watchedTags.Contains)==true || g.def.defName.StartsWith("Brow_",StringComparison.Ordinal))))){appearanceVersion=3;return;}
   var defaults=AppearanceGenesFor(kind).Select(n=>DefDatabase<GeneDef>.GetNamed(n)).ToList();
   var tags=defaults.SelectMany(d=>d.exclusionTags??new List<string>()).ToHashSet();
   // Only cosmetic conflicts are replaced during this one-time role migration.
   foreach(var g in pawn.genes.GenesListForReading.ToList())
    if(legacy.Contains(g.def) || defaults.Contains(g.def) || g.def.bodyType!=null || g.def.exclusionTags?.Any(tags.Contains)==true)pawn.genes.RemoveGene(g);
   foreach(var d in defaults)pawn.genes.AddGene(d,true);
   pawn.story.bodyType=kind=="Golem" || kind=="StoneGolem"?BodyTypeDefOf.Hulk:BodyTypeDefOf.Thin;
   if(kind=="Skeleton")pawn.story.headType=HeadTypeDefOf.Skull;
   appearanceVersion=3;
   Necro.Refresh(pawn);
  }
  public void ApplyHumanRole(HediffStage stage){
   if(pawn.def!=ThingDefOf.Human)return;
   if(stage.statOffsets==null)stage.statOffsets=new List<StatModifier>();
   foreach(var stat in new[]{StatDefOf.MoveSpeed,StatDefOf.ArmorRating_Sharp,StatDefOf.ArmorRating_Blunt,StatDefOf.MarketValue,StatDefOf.ComfyTemperatureMin,StatDefOf.ComfyTemperatureMax}){
    float offset=LegacyRace.GetStatValueAbstract(stat)-ThingDefOf.Human.GetStatValueAbstract(stat);
    if(Math.Abs(offset)>.0001f)stage.statOffsets.Add(new StatModifier{stat=stat,value=offset});
   }
   if(kind=="Golem")stage.statFactors.Add(new StatModifier{stat=StatDefOf.MeleeDamageFactor,value=5f});
   if(kind=="StoneGolem")stage.statFactors.Add(new StatModifier{stat=StatDefOf.MeleeDamageFactor,value=4f});
  }
 }
 [HarmonyPatch(typeof(Thing),"get_ThingID")]
 public static class ConstructStableId {
  static void Postfix(Thing __instance,ref string __result){if(__instance is Pawn p && Necro.Construct(p)?.legacyThingID is string old && !string.IsNullOrEmpty(old))__result=old;}
 }
 [HarmonyPatch(typeof(Pawn),"get_BodySize")]
 public static class ConstructHumanBodySize {
  static void Postfix(Pawn __instance,ref float __result){if(__instance.def!=ThingDefOf.Human)return;var h=Necro.Construct(__instance);if(h!=null)__result*=h.LegacyRace.race.baseBodySize;}
 }
}
