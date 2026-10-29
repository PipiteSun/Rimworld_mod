using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 巨魔食性：使用《生物科技》原版“强健消化”基因，生食不掉心情。
 // 原版 defName 先按常见写法查找，找不到时在官方内容中按 defName 含 Digestion 兜底，避免硬编码导致交叉引用报错。
 [StaticConstructorOnStartup]
 public static class TrollDiet {
  static readonly string[] candidates={"RobustDigestion","Robust_Digestion","Digestion_Robust"};
  public static readonly GeneDef RawFoodGene;
  static TrollDiet(){
   RawFoodGene=candidates.Select(n=>DefDatabase<GeneDef>.GetNamedSilentFail(n)).FirstOrDefault(d=>d!=null)
    ??DefDatabase<GeneDef>.AllDefs.FirstOrDefault(d=>d.modContentPack?.IsOfficialMod==true && d.defName.IndexOf("Digestion",StringComparison.OrdinalIgnoreCase)>=0);
   if(RawFoodGene==null){Log.Warning("[DND边缘大陆] 未找到原版强健消化基因，巨魔不会获得生食免心情效果。");return;}
   var troll=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_Troll");
   if(troll!=null && !troll.genes.Contains(RawFoodGene))troll.genes.Add(RawFoodGene);
  }
 }
}
