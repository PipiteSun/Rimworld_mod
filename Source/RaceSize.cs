using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 种族体型：巨躯规则、整体渲染缩放、胃容量按它算；儿童随年龄长大（同巨人血统的成长曲线）。
 // 1.102.0：六臂蛇人是原版人类 + 异种型，扩展挂在本源基因上（蛇身本源），体型由 get_BodySize 补丁乘上（RaceSizeBodySize）。
 // 种族定义上的扩展仍然认（那时体型来自种族 baseBodySize，补丁不再乘）。
 // 1.171.0：双头巨人、独眼巨人整族移除；原来的 Source/HARGiants.cs 只留下这部分通用代码，改名为本文件。
 public class RaceSizeExtension : DefModExtension {public float adultSize=1f,renderScale=1f,stomach=1f;}
 public static class RaceSize {
  static readonly ConcurrentDictionary<ThingDef,RaceSizeExtension> exts=new ConcurrentDictionary<ThingDef,RaceSizeExtension>();
  public static RaceSizeExtension Ext(ThingDef d){if(d==null)return null;return exts.GetOrAdd(d,x=>x.GetModExtension<RaceSizeExtension>());}
  static List<GeneDef> genes;
  public static List<GeneDef> Genes=>genes??(genes=DefDatabase<GeneDef>.AllDefsListForReading.Where(g=>g.GetModExtension<RaceSizeExtension>()!=null).ToList());
  // 按角色缓存（同 GiantKin.Of）：基因数量变化或 Notify_GenesChanged 时重算。
  sealed class Entry {public RaceSizeExtension ext;public Gene_EndlessGrowth grow;public int genes=-1;public bool valid;}
  static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Pawn,Entry> cache=new System.Runtime.CompilerServices.ConditionalWeakTable<Pawn,Entry>();
  public static void Invalidate(Pawn p){if(p!=null && cache.TryGetValue(p,out var e))e.valid=false;}
  public static RaceSizeExtension GeneExt(Pawn p){
   if(p?.genes==null)return null;var list=Genes;if(list.Count==0)return null;
   var e=cache.GetOrCreateValue(p);int n=p.genes.GenesListForReading.Count;
   if(e.valid && e.genes==n)return e.ext;
   RaceSizeExtension best=null;for(int i=0;i<list.Count;i++)if(p.genes.HasActiveGene(list[i])){best=list[i].GetModExtension<RaceSizeExtension>();break;}
   e.ext=best;e.grow=best is EndlessGrowthExtension?p.genes.GetFirstGeneOfType<Gene_EndlessGrowth>():null;e.genes=n;e.valid=true;return best;
  }
  public static RaceSizeExtension Of(Pawn p)=>p==null?null:Ext(p.def)??GeneExt(p);
  static float Grow(Pawn p,float adult)=>1f+(adult-1f)*GiantKin.Growth(p);
  // 1.173.0 无限生长（古巨兽后裔，Source/Behemoth.cs）：基因自己记的体型倍率，没有上限；真实体型和胃容量 ×S，画面 ×√S（同泰坦战士的画法）。
  static float Endless(Pawn p,RaceSizeExtension e)=>e is EndlessGrowthExtension && cache.TryGetValue(p,out var c) && c.grow!=null?c.grow.Size:1f;
  public static float SizeFactor(Pawn p){var e=Of(p);return e==null?1f:Grow(p,e.adultSize)*Endless(p,e);}
  public static float RenderFactor(Pawn p){var e=Of(p);return e==null?1f:Grow(p,e.renderScale)*Mathf.Sqrt(Endless(p,e));}
  public static float Stomach(Pawn p){var e=Of(p);return e==null?1f:Grow(p,e.stomach)*Endless(p,e);}
  // get_BodySize 补丁只乘基因带来的那一项（种族的已经在 baseBodySize 里）。
  public static float GeneSizeFactor(Pawn p){if(p==null || Ext(p.def)!=null)return 1f;var e=GeneExt(p);return e==null?1f:Grow(p,e.adultSize)*Endless(p,e);}
 }
 [HarmonyPatch(typeof(Pawn),"get_BodySize")]
 public static class RaceSizeBodySize {
  static void Postfix(Pawn __instance,ref float __result){__result*=RaceSize.GeneSizeFactor(__instance);}
 }
 [HarmonyPatch(typeof(Pawn_GeneTracker),"Notify_GenesChanged")]
 public static class RaceSizeGeneChanged {
  static void Postfix(Pawn_GeneTracker __instance){RaceSize.Invalidate(__instance.pawn);}
 }
}
