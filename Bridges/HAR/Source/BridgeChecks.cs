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
  // 1.116.0：五色真龙和贝希尔 / 眼魔 / 半巫妖不再是 HAR 种族，是主模组里的动物型怪物。HAR 启用时确认它们仍是普通 ThingDef（不是 ThingDef_AlienRace）、不是人形。
  // ChromaticDragons / LegendaryMonsters 组在启用 HAR 时调用。
  static bool PlainBeast(string d){var r=DefDatabase<ThingDef>.GetNamedSilentFail(d);return r!=null && !(r is ThingDef_AlienRace) && r.race!=null && !r.race.Humanlike && r.race.Animal;}
  public static int RunDragons(){
   n=0;
   Check(new[]{"Red","Blue","Green","Black","White"}.All(c=>PlainBeast("LP_"+c+"Dragon")),"HAR on: the five chromatic dragons are plain animal-style races, not HAR races");
   return n;
  }
  public static int RunMonsters(){
   n=0;
   Check(new[]{"LP_Behir","LP_Beholder","LP_Demilich"}.All(PlainBeast),"HAR on: behir / beholder / demilich are plain animal-style races, not HAR races");
   return n;
  } }
}
