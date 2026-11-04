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
  // 1.102.0：六臂蛇人、双头巨人、独眼巨人不再是 HAR 种族，是原版人类 + 异种型。HAR 启用时人类也是 ThingDef_AlienRace：确认它不拦这三个异种型、背景与 Isekai 特性，旧种族定义不在了。
  static readonly string[] kins={"LP_MarilithKin","LP_EttinKin","LP_CyclopsKin"};
  static bool HumanTakes(string kin){var x=DefDatabase<XenotypeDef>.GetNamedSilentFail(kin);return x!=null && RaceRestrictionSettings.CanUseXenotype(x,ThingDefOf.Human);}
  public static int Run(){
   n=0;
   var human=ThingDefOf.Human as ThingDef_AlienRace;
   var traits=DefDatabase<TraitDef>.AllDefsListForReading.Where(t=>t.defName.StartsWith("LP_Background_") || t.defName.StartsWith("IKM_") || t.defName.StartsWith("Isekai")).ToList();
   Check(human!=null && kins.All(HumanTakes) && DefDatabase<ThingDef>.GetNamedSilentFail(Marilith.Race)==null && traits.All(t=>RaceRestrictionSettings.CanGetTrait(t,human,t.degreeDatas?.FirstOrDefault()?.degree??0,null)),"HAR on: humans (HAR alien race) can use the marilith/ettin/cyclops xenotypes and background/Isekai traits; the old HAR races are gone");
   return n;
  }
  // GiantKinHAR 组调用：同上，只看两族巨人。
  public static int RunGiants(){
   n=0;
   Check(HumanTakes("LP_EttinKin") && HumanTakes("LP_CyclopsKin") && DefDatabase<ThingDef>.GetNamedSilentFail(Ettin.Race)==null && DefDatabase<ThingDef>.GetNamedSilentFail(Cyclops.Race)==null,"HAR on: humans can use the ettin and cyclops xenotypes; the old HAR races are gone");
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
