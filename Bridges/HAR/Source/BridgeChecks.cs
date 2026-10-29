using System;
using System.Collections.Generic;
using System.Linq;
using AlienRace;
using RimWorld;
using Verse;

namespace LichPhylactery.HARBridge {
 // 1.33.0 HAR 联动程序集（Bridges/HAR/Assemblies，只在启用 HAR 时加载）：只放需要 AlienRace.dll 类型的检查。
 // 主程序集按名字反射调用 Run（见 Source/HARBridgeHook.cs），不引用 AlienRace.dll。
 public static class BridgeChecks {
  static int n;
  static void Check(bool ok,string message){if(!ok)throw new Exception(message);n++;Log.Message("[LICH-TEST] OK "+message);}
  public static int Run(){
   n=0;
   var race=DefDatabase<ThingDef>.GetNamed(Marilith.Race) as ThingDef_AlienRace;
   Check(race!=null && race.alienRace.generalSettings.maleGenderProbability<.16f && race.alienRace.generalSettings.maleGenderProbability>.14f && race.alienRace.generalSettings.humanRecipeImport && race.alienRace.generalSettings.allowHumanBios,"HAR race LP_Marilith: 85% female, human recipes imported, human backstories allowed");
   var addons=race.alienRace.generalSettings.alienPartGenerator.bodyAddons;
   var arms=addons.Where(a=>a.path.Contains("/Arm")).ToList();var tails=addons.Where(a=>a.path.Contains("/Tail")).ToList();
   Check(addons.Count==10 && arms.Count==8 && tails.Count==2 && arms.All(a=>a.conditions.OfType<AlienRace.ExtendedGraphics.ConditionBodyPart>().Any(c=>c.bodyPart==BodyPartDefOf.Arm && !c.bodyPartLabel.NullOrEmpty())) && tails.All(a=>a.conditions.OfType<AlienRace.ExtendedGraphics.ConditionBodyPart>().Any(c=>c.bodyPart?.defName=="LP_SerpentTail")),"HAR body addons: four extra arms bound to their arm parts and a serpent tail bound to LP_SerpentTail (adult and child sets)");
   var kin=DefDatabase<XenotypeDef>.GetNamed("LP_MarilithKin");
   Check(RaceRestrictionSettings.CanUseXenotype(kin,race) && !RaceRestrictionSettings.CanUseXenotype(XenotypeDefOf.Baseliner,race) && !RaceRestrictionSettings.CanUseXenotype(kin,ThingDefOf.Human),"only marilith kin for the race, and the xenotype is race-restricted");
   // 1.32.0 背景特性与其他模组的特性：HAR 不限制这个种族的特性。
   var traits=DefDatabase<TraitDef>.AllDefsListForReading.Where(t=>t.defName.StartsWith("LP_Background_") || t.defName.StartsWith("IKM_") || t.defName.StartsWith("Isekai")).ToList();
   Check(traits.Count>0 && traits.All(t=>RaceRestrictionSettings.CanGetTrait(t,race,t.degreeDatas?.FirstOrDefault()?.degree??0,null)),"HAR does not restrict background/Isekai traits for the race ("+traits.Count+" traits)");
   var settings=DefDatabase<RaceSettings>.GetNamed("LP_MarilithRaceSettings");
   Check(settings.pawnKindSettings.startingColonists.Any(e=>e.factionDefs.Contains(FactionDefOf.PlayerColony) && e.pawnKindEntries.Any(k=>k.kindDefs.Any(d=>d.race==race))) && settings.pawnKindSettings.alienwandererkinds.Any() && settings.pawnKindSettings.alienrefugeekinds.Any(),"marilith colonists are offered at game start, as wanderers and as refugees");
   Check(RaceRestrictionSettings.CanReproduce(race,ThingDefOf.Human) && RaceRestrictionSettings.CanReproduce(ThingDefOf.Human,race),"marilith and humans can have children together");
   return n;
  }
  // 1.44.0 双头巨人、独眼巨人（GiantKinHAR 组调用）：一条合并检查。
  public static int RunGiants(){
   n=0;
   var ettin=DefDatabase<ThingDef>.GetNamed(Ettin.Race) as ThingDef_AlienRace;var cyclops=DefDatabase<ThingDef>.GetNamed(Cyclops.Race) as ThingDef_AlienRace;
   var ea=ettin.alienRace.generalSettings.alienPartGenerator.bodyAddons;var ca=cyclops.alienRace.generalSettings.alienPartGenerator.bodyAddons;
   bool boundHead=ea.All(a=>a.conditions.OfType<AlienRace.ExtendedGraphics.ConditionBodyPart>().Any(c=>c.bodyPart?.defName==Ettin.SecondHead));
   bool boundEye=ca.Count==1 && ca[0].alignWithHead && ca[0].conditions.OfType<AlienRace.ExtendedGraphics.ConditionBodyPart>().Any(c=>c.bodyPart?.defName==Cyclops.Eye);
   var ek=DefDatabase<XenotypeDef>.GetNamed("LP_EttinKin");var ck=DefDatabase<XenotypeDef>.GetNamed("LP_CyclopsKin");
   var settings=DefDatabase<RaceSettings>.GetNamed("LP_HARGiantsRaceSettings");
   bool offered=new[]{ettin,cyclops}.All(r=>settings.pawnKindSettings.startingColonists.Any(e=>e.factionDefs.Contains(FactionDefOf.PlayerColony) && e.pawnKindEntries.Any(k=>k.chance==3f && k.kindDefs.Any(d=>d.race==r)))) && settings.pawnKindSettings.alienwandererkinds.Any() && settings.pawnKindSettings.alienrefugeekinds.Count==2;
   var traits=DefDatabase<TraitDef>.AllDefsListForReading.Where(t=>t.defName.StartsWith("LP_Background_") || t.defName.StartsWith("IKM_") || t.defName.StartsWith("Isekai")).ToList();
   bool traitsOk=traits.Count>0 && traits.All(t=>RaceRestrictionSettings.CanGetTrait(t,ettin,t.degreeDatas?.FirstOrDefault()?.degree??0,null) && RaceRestrictionSettings.CanGetTrait(t,cyclops,t.degreeDatas?.FirstOrDefault()?.degree??0,null));
   Check(ettin!=null && cyclops!=null && ea.Count==8 && boundHead && boundEye && traitsOk && ettin.alienRace.generalSettings.allowHumanBios && cyclops.alienRace.generalSettings.allowHumanBios && ettin.alienRace.generalSettings.humanRecipeImport && cyclops.alienRace.generalSettings.humanRecipeImport
    && RaceRestrictionSettings.CanUseXenotype(ek,ettin) && RaceRestrictionSettings.CanUseXenotype(ck,cyclops) && !RaceRestrictionSettings.CanUseXenotype(XenotypeDefOf.Baseliner,ettin) && !RaceRestrictionSettings.CanUseXenotype(ck,ThingDefOf.Human)
    && offered && RaceRestrictionSettings.CanReproduce(ettin,ThingDefOf.Human) && RaceRestrictionSettings.CanReproduce(ThingDefOf.Human,cyclops),
    "HAR ettin and cyclops: second-head add-ons bound to LP_EttinHead (8), cyclops eye add-on bound to LP_CyclopsEye; race-restricted xenotypes; background/Isekai traits and human backstories allowed; offered at start/as wanderers/refugees at weight 3; can have children with humans");
   return n;
  }
  // 1.52.0 五色真龙（ChromaticDragons 组调用）：种族存在、只用自带攻击（不穿衣服不拿武器）、不用人类背景、龙头型、身体贴图路径、不染肤色。
  public static int RunDragons(){
   n=0;
   var races=new[]{"Red","Blue","Green","Black","White"}.Select(c=>DefDatabase<ThingDef>.GetNamedSilentFail("LP_"+c+"Dragon") as ThingDef_AlienRace).ToList();
   bool ok=races.All(r=>r!=null && r.alienRace.raceRestriction.onlyUseRaceRestrictedApparel && r.alienRace.raceRestriction.onlyUseRaceRestrictedWeapons && !r.alienRace.generalSettings.allowHumanBios
    && r.alienRace.generalSettings.alienPartGenerator.headTypes.Count==1 && r.alienRace.generalSettings.alienPartGenerator.headTypes[0].defName=="LP_DragonHead_"+r.defName.Substring(3,r.defName.Length-9)
    && r.race.body.defName=="LP_DragonBody" && r.race.lifeStageAges.Count==3);
   bool gear=races.All(r=>r!=null && !RaceRestrictionSettings.CanWear(DefDatabase<ThingDef>.GetNamed("Apparel_Parka"),r) && !RaceRestrictionSettings.CanEquip(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword"),r));
   Check(ok && gear,"HAR chromatic dragons: 5 races with LP_DragonBody and three life stages, one dragon head type each, no human backstories, cannot wear apparel or wield weapons (natural attacks only)");
   return n;
  }
  // 1.52.1 传奇怪（LegendaryMonsters 组调用）：贝希尔、眼魔、半巫妖三个种族存在、只用自带攻击、不用人类背景、各自一个透明头型和身体。
  public static int RunMonsters(){
   n=0;
   var races=new[]{"LP_Behir","LP_Beholder","LP_Demilich"}.Select(d=>DefDatabase<ThingDef>.GetNamedSilentFail(d) as ThingDef_AlienRace).ToList();
   bool ok=races.All(r=>r!=null && r.alienRace.raceRestriction.onlyUseRaceRestrictedApparel && r.alienRace.raceRestriction.onlyUseRaceRestrictedWeapons && !r.alienRace.generalSettings.allowHumanBios
    && r.alienRace.generalSettings.alienPartGenerator.headTypes.Count==1 && r.alienRace.generalSettings.alienPartGenerator.headTypes[0].defName=="LP_MonsterHead_"+r.defName && r.race.lifeStageAges.Count==1
    && !RaceRestrictionSettings.CanWear(DefDatabase<ThingDef>.GetNamed("Apparel_Parka"),r) && !RaceRestrictionSettings.CanEquip(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword"),r));
   Check(ok,"HAR legendary monsters: behir / beholder / demilich races with one life stage and one transparent head type each, no human backstories, natural attacks only");
   return n;
  } }
}
