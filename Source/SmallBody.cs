using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 共享组件：小体型（矮人0.8、侏儒0.6、半身人0.7……）。挂在 GeneDef 的 modExtensions 上。
 // 与火巨人/巨魔（取较大者）、力能龙龙龄、构造体等已有体型来源相乘；多个小体型基因之间也相乘。
 // 不依赖 VEF：VEF 的 GeneExtension 也能改体型，但本模组核心环境不加载 VEF。两者同时使用时效果相乘。
 public class SmallBodyExtension : DefModExtension {
  public float bodySize=1f;     // 实际体型倍率（BodySize：食量、负重、被远程命中概率、尸体价值等）
  public float renderScale=-1f; // 整体渲染缩放；≤0 时跟随 bodySize
  public float carryFactor=1f;  // 额外搬运量（CarryingCapacity）倍率；原版搬运量已随体型缩放，这里是在其上再乘
  public float RenderScale=>renderScale>0?renderScale:bodySize;
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(bodySize<=0 || bodySize>4)yield return "SmallBodyExtension.bodySize must be in (0,4]";
   if(renderScale>4)yield return "SmallBodyExtension.renderScale must be at most 4";
   if(carryFactor<=0)yield return "SmallBodyExtension.carryFactor must be positive";
  }
 }
 public static class SmallBody {
  static List<GeneDef> genes;static int geneCount=-1;static Dictionary<GeneDef,SmallBodyExtension> byGene;
  // 带扩展的基因列表；DefDatabase 变化（测试注册运行时基因）时重建。列表为空时所有补丁直接返回。
  public static List<GeneDef> Genes{get{
   if(genes==null || geneCount!=DefDatabase<GeneDef>.DefCount){
    var list=DefDatabase<GeneDef>.AllDefsListForReading.Where(g=>g.GetModExtension<SmallBodyExtension>()!=null).ToList();
    byGene=list.ToDictionary(g=>g,g=>g.GetModExtension<SmallBodyExtension>());genes=list;geneCount=DefDatabase<GeneDef>.DefCount;
   }
   return genes;
  }}
  // 1.58.6：原来对 24 个带扩展的基因各做一次 HasActiveGene（每次都扫一遍小人的基因表），BodySize / 根节点缩放 / 负重每次调用约 24×基因数次比较。
  // 现在只扫一遍小人的基因表查字典；命中多个时仍按原来的定义顺序相乘（浮点结果逐位相同），同一个定义只乘一次。
  static float Product(Pawn pawn,System.Func<SmallBodyExtension,float> value){
   var list=Genes;if(list.Count==0 || pawn?.genes==null)return 1f;
   var map=byGene;var own=pawn.genes.GenesListForReading;
   GeneDef first=null;List<GeneDef> more=null;
   for(int i=0;i<own.Count;i++){
    var g=own[i];if(!g.Active || !map.ContainsKey(g.def) || g.def==first)continue;
    if(first==null)first=g.def;else if(more==null)more=new List<GeneDef>{first,g.def};else if(!more.Contains(g.def))more.Add(g.def);
   }
   if(first==null)return 1f;
   if(more==null)return 1f*value(map[first]);
   float result=1f;
   for(int i=0;i<list.Count;i++)if(more.Contains(list[i]))result*=value(map[list[i]]);
   return result;
  }
  public static float Size(Pawn pawn)=>Product(pawn,e=>e.bodySize);
  public static float Render(Pawn pawn)=>Product(pawn,e=>e.RenderScale);
  public static float Carry(Pawn pawn)=>Product(pawn,e=>e.carryFactor);
  public static void Refresh(Pawn pawn){pawn?.health?.capacities?.Notify_CapacityLevelsDirty();pawn?.Drawer?.renderer?.SetAllGraphicsDirty();}
 }
 public class StatPart_SmallBodyCarry : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p)val*=SmallBody.Carry(p);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;float f=SmallBody.Carry(p);return f==1f?null:"小体型搬运：×"+f.ToStringPercent();}
 }
 [StaticConstructorOnStartup]
 public static class SmallBodyStartup {
  static SmallBodyStartup(){
   var stat=StatDefOf.CarryingCapacity;if(stat.parts==null)stat.parts=new List<StatPart>();
   if(!stat.parts.Any(p=>p is StatPart_SmallBodyCarry))stat.parts.Add(new StatPart_SmallBodyCarry{parentStat=stat});
  }
 }
 [HarmonyPatch(typeof(Pawn),"get_BodySize")]
 public static class SmallBodySize {
  static void Postfix(Pawn __instance,ref float __result){if(SmallBody.Genes.Count>0)__result*=SmallBody.Size(__instance);}
 }
 [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.ScaleFor))]
 public static class SmallBodyRenderSize {
  static void Postfix(PawnRenderNode node,PawnDrawParms parms,ref Vector3 __result){if(RenderTags.IsRoot(node?.Props?.tagDef) && SmallBody.Genes.Count>0)__result*=SmallBody.Render(parms.pawn);}
 }
 [HarmonyPatch(typeof(Pawn_GeneTracker),"Notify_GenesChanged")]
 public static class SmallBodyGeneChanged {
  static void Postfix(Pawn_GeneTracker __instance,GeneDef __0){if(__0?.GetModExtension<SmallBodyExtension>()!=null)SmallBody.Refresh(__instance.pawn);}
 }
}
