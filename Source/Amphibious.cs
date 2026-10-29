using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 共享组件（1.24.0）：水中行动。挂在 GeneDef、服装 ThingDef 或 HediffDef 的 modExtensions 上。
 // 带它的角色走进浅水、深水边缘、沼泽、浅海时没有地形减速。走的是原版 1.7 的“按角色的水格成本”（Pawn.WaterCellCost）：
 // 移动成本（Pawn_PathFollower）和寻路（PathFinderCostTuning，含征召时的感知成本）都读它，所以既不减速、寻路也不再绕开水。
 // 深水原版不可通行，这里不改通行性。以后特里同、海精灵直接挂这个扩展即可。
 public class AmphibiousExtension : DefModExtension {
  public int waterCellCost=0; // 水格的地形成本（原版浅水约为 30 左右的额外成本；0 = 与平地相同）
 }
 public static class Amphibious {
  static List<GeneDef> genes;static int geneCount=-1;
  static List<GeneDef> Genes{get{
   if(genes==null || geneCount!=DefDatabase<GeneDef>.DefCount){var l=DefDatabase<GeneDef>.AllDefsListForReading.Where(g=>g.GetModExtension<AmphibiousExtension>()!=null).ToList();geneSet=new HashSet<GeneDef>(l);genes=l;geneCount=DefDatabase<GeneDef>.DefCount;}
   return genes;
  }}
  // 生效的扩展（取最小的水格成本）；没有时返回 null。
  public static AmphibiousExtension For(Pawn p){
   if(p==null)return null;AmphibiousExtension best=null;
   void Take(AmphibiousExtension e){if(e!=null && (best==null || e.waterCellCost<best.waterCellCost))best=e;}
   // 1.58.6：寻路每一步都会问。基因只扫一遍小人自己的基因表；没有任何服装 / 状态定义带这个扩展时跳过对应的循环。取最小水格成本，结果不变。
   if(p.genes!=null && Genes.Count>0){var own=p.genes.GenesListForReading;for(int i=0;i<own.Count;i++){var g=own[i];if(g.Active && geneSet.Contains(g.def))Take(g.def.GetModExtension<AmphibiousExtension>());}}
   Sets();
   var worn=p.apparel?.WornApparel;if(worn!=null && anyApparel)for(int i=0;i<worn.Count;i++)Take(worn[i].def.GetModExtension<AmphibiousExtension>());
   var hs=p.health?.hediffSet?.hediffs;if(hs!=null && anyHediff)for(int i=0;i<hs.Count;i++)Take(hs[i].def.GetModExtension<AmphibiousExtension>());
   return best;
  }
  static HashSet<GeneDef> geneSet=new HashSet<GeneDef>();static bool anyApparel=true,anyHediff=true;static int thingCount=-1,hediffCount=-1;
  static void Sets(){
   if(thingCount!=DefDatabase<ThingDef>.DefCount){anyApparel=DefDatabase<ThingDef>.AllDefsListForReading.Any(d=>d.GetModExtension<AmphibiousExtension>()!=null);thingCount=DefDatabase<ThingDef>.DefCount;}
   if(hediffCount!=DefDatabase<HediffDef>.DefCount){anyHediff=DefDatabase<HediffDef>.AllDefsListForReading.Any(d=>d.GetModExtension<AmphibiousExtension>()!=null);hediffCount=DefDatabase<HediffDef>.DefCount;}
  }
  public static bool Has(Pawn p)=>For(p)!=null;
  public static bool IsWaterTerrain(TerrainDef t)=>t!=null && (t.IsWater || t.defName.IndexOf("Marsh",System.StringComparison.OrdinalIgnoreCase)>=0);
 }
 [HarmonyPatch(typeof(Pawn),"get_WaterCellCost")]
 public static class AmphibiousWaterCost {
  static void Postfix(Pawn __instance,ref int? __result){
   if(!__instance.RaceProps.Humanlike)return;
   var e=Amphibious.For(__instance);if(e==null)return;
   if(__result==null || e.waterCellCost<__result.Value)__result=e.waterCellCost;
  }
 }
}
