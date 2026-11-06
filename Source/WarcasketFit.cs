using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.115.0 热修（Peter）：战棺（VFE 海盗，可选）适配不同体型。
 // A. 巨人血统（含火巨人）可以进战棺铸造厂、穿战棺：战棺不受「巨人可穿普通衣物」限制（GiantKin.CanWear 里的例外），画面照常随根节点按体型缩放。
 // B. 战棺贴图只有一套（VEF isUnifiedApparel），五种身体类型画出来的战棺外壳大小本来就一样（自检截图对照过，不用缩放）；
 //    不一样的是头的位置（侧面 x 偏移 0.04～0.10），头盔跟着头走就会偏离外壳，所以装着战棺时头统一放到参考体型（男性）的位置。
 // C. 装着战棺时隐藏六臂蛇人的额外手臂、蛇尾和双头巨人的第二个头（XenoParts.Visible），主头也不再往一侧让位。
 // 没装 VFE 海盗时 CasketType 为空，所有补丁第一步就返回。
 public static class WarcasketFit {
  static Type casketType;static bool looked;
  public static Type CasketType{get{if(!looked){looked=true;casketType=AccessTools.TypeByName("VFEPirates.WarcasketDef");}return casketType;}}
  // 0 = 不是战棺，1 = 战棺身体件（躯干外层），2 = 其他战棺件（头盔、肩甲）。按定义缓存。
  // 2026-11-06 热修：BaseHeadOffsetAt 在 1.7 的并行渲染任务里被多线程调用；原来的普通 Dictionary 被同时写坏后每次读都抛异常，
  // 所有小人的渲染中断（地图上只剩名字）。改成 ConcurrentDictionary。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<ThingDef,byte> kinds=new System.Collections.Concurrent.ConcurrentDictionary<ThingDef,byte>();
  public static byte Kind(ThingDef d){
   if(d==null || CasketType==null)return 0;
   if(kinds.TryGetValue(d,out byte k))return k;
   k=0;if(CasketType.IsInstanceOfType(d) && d.apparel!=null)k=(byte)(d.apparel.layers.Contains(ApparelLayerDefOf.Shell) && d.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)?1:2);
   kinds[d]=k;return k;
  }
  public static bool IsCasket(ThingDef d)=>Kind(d)!=0;
  // 身上有战棺身体件。
  public static bool Encased(Pawn p){
   if(CasketType==null || p?.apparel==null)return false;var list=p.apparel.WornApparel;
   for(int i=0;i<list.Count;i++)if(Kind(list[i].def)==1)return true;return false;
  }
  public static bool Enabled=true; // 检查里对照用
  // 参考体型：战棺贴图按男性体型画（头盔的位置也按男性的头）。
  public static BodyTypeDef Reference=>BodyTypeDefOf.Male;
  public static bool Adult(Pawn p){var b=p?.story?.bodyType;return b!=null && b!=BodyTypeDefOf.Child && b!=BodyTypeDefOf.Baby;}
 }

 // B. 装着战棺时头（和头盔）放到参考体型的位置，跟战棺贴图对齐；C. 双头巨人不再让位（第二个头已隐藏）。排在其他头部偏移补丁之后。
 [HarmonyPatch(typeof(PawnRenderer),nameof(PawnRenderer.BaseHeadOffsetAt))]
 public static class WarcasketHeadOffset {
  [HarmonyPriority(Priority.Last)]
  static void Postfix(Rot4 rotation,Pawn ___pawn,ref Vector3 __result){
   if(WarcasketFit.CasketType==null || !WarcasketFit.Enabled || !WarcasketFit.Adult(___pawn) || !WarcasketFit.Encased(___pawn))return;
   var own=___pawn.story.bodyType;var refB=WarcasketFit.Reference;if(Ettin.BothHeads(___pawn))__result-=XenoParts.EttinHeadOffset(rotation);
   if(own==refB)return;
   float k=Mathf.Sqrt(___pawn.ageTracker.CurLifeStage.bodySizeFactor);Vector2 d=(refB.headOffset-own.headOffset)*k;
   __result+=rotation==Rot4.East?new Vector3(d.x,0f,d.y):rotation==Rot4.West?new Vector3(-d.x,0f,d.y):new Vector3(0f,0f,d.y);
  }
 }
}
