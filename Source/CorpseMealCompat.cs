using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.176.0 兼容修正（Peter：同一个角色吃尸体不涨饱食度）。
 // 胃口很大的角色（巨胃种族、Big and Small 的大体型等）一口想要的营养比整具尸体还多，原版就让他把整具尸体吃掉、尸体随即销毁。
 // 之后别的模组在 Thing.Ingested 的后置补丁里再问“这是不是虫肉”（Alpha Memes 的 IngestedNutrientPaste → FoodUtility.IsInsectCorpseOrInsectMeatIngredient
 // → GetMeatSourceCategoryFromCorpse），原版这个函数直接读 corpse.InnerPawn.RaceProps，而销毁后的尸体 InnerPawn 是空的 → 空引用，
 // 整个“吃完”步骤中断，营养没加到饱食度上（日志：JobDriver threw exception in toil FinalizeIngest）。
 // 这里在原版函数前面挡一下：尸体已经没有内容时返回 Undefined，不再抛错。
 [HarmonyPatch(typeof(FoodUtility),nameof(FoodUtility.GetMeatSourceCategoryFromCorpse))]
 public static class CorpseMeatSourceGuard {
  public static int Guarded; // 自检用
  [HarmonyPriority(Priority.First)]
  static bool Prefix(Thing thing,ref MeatSourceCategory __result){
   if(thing is Corpse c && c.InnerPawn?.RaceProps==null){__result=default(MeatSourceCategory);Guarded++;return false;}
   return true;
  }
 }
}
