using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.52.0 热修：传奇种族不会食物中毒（见 ArtSource/LegendaryFoodPoison-hotfix-report.md）。
 // 传奇种族 = 种族亲和表里的传奇层（巫妖、巨人、泰坦/觉醒泰坦、力能龙裔、夺心魔、巨灵、阿斯莫、六臂蛇人、寒冰魔、炎魔……，含 modExtension 标了 legendary 的种族/异种），
 // 加上传奇单位（LP_LegendaryExtension，五色真龙等）和半神。每次吃东西时现查，不加基因也不存档，旧存档直接生效。
 public static class LegendaryFoodPoison {
  public static bool Immune(Pawn p)=>p!=null && (Affinity.LegendaryRace(p) || Legendary.Is(p) || Demigods.Is(p));
 }
 // 中毒概率因子（原版吃东西、腐烂、烹饪不洁都走这里）→ 0。
 [HarmonyPatch(typeof(FoodUtility),nameof(FoodUtility.GetFoodPoisonChanceFactor))]
 public static class LegendaryFoodPoisonFactor {
  static void Postfix(Pawn __0,ref float __result){if(__result>0f && LegendaryFoodPoison.Immune(__0))__result=0f;}
 }
 // 兜底：特性覆盖概率等直接加中毒的路径也拦掉。
 [HarmonyPatch(typeof(FoodUtility),nameof(FoodUtility.AddFoodPoisoningHediff))]
 public static class LegendaryFoodPoisonAdd {
  static bool Prefix(Pawn __0)=>!LegendaryFoodPoison.Immune(__0);
 }
}
