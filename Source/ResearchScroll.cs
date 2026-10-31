using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.99.2 热修：研究窗口可以上下滚动。
 // 原版 MainTabWindow_Research.DrawRightRect 的滚动区域高度写死成窗口高度（rect.height − 16），只能左右滚。
 // “原版扩展”（VanillaExpanded）页被几十个 VE 模组共用，研究排到第 7 行以下（Vanilla Books Expanded 的写作、印刷等），
 // 屏幕矮或页签行多时就被截掉，够不着。这里把滚动区域高度改成至少能装下这一页最低的研究，并让滚轮能纵向滚：
 // 这一页不需要左右滚时滚轮直接上下滚；两个方向都放不下时，按住 Shift 用滚轮上下滚（不按照旧左右滚），右侧滚动条也可以拖。
 public static class ResearchScroll {
  static readonly Func<MainTabWindow_Research,ResearchTabDef,Vector2> View=AccessTools.MethodDelegate<Func<MainTabWindow_Research,ResearchTabDef,Vector2>>(AccessTools.Method(typeof(MainTabWindow_Research),"ViewSize"));
  static readonly Func<MainTabWindow_Research,ResearchTabDef> Tab=AccessTools.MethodDelegate<Func<MainTabWindow_Research,ResearchTabDef>>(AccessTools.PropertyGetter(typeof(MainTabWindow_Research),"CurTab"));
  // 研究列表放在滚动区域 ContractedBy(20) 的分组里，上下各留 20。
  public const float Margin=40f;
  public static int Patched;
  static ResearchTabDef lastTab;static float lastHeight;static int lastFrame=-9999;
  // 这一页内容需要的高度；ViewSize 每次都要量所有研究的标签，按页缓存，每 2 秒（120 帧）重算一次（研究解锁后可能多出新的）。
  public static float Content(MainTabWindow_Research w){
   var tab=Tab(w);
   if(tab!=lastTab || Time.frameCount-lastFrame>120 || Time.frameCount<lastFrame){
    lastTab=tab;lastFrame=Time.frameCount;
    try{lastHeight=tab==null?0f:View(w,tab).y+Margin;}catch(Exception e){lastHeight=0f;Log.ErrorOnce("[DND边缘大陆] 研究窗口高度计算失败："+e,0x5E5C011);}
   }
   return lastHeight;
  }
  public static void Forget(){lastTab=null;lastFrame=-9999;}
  // 原来的高度（窗口高 − 16）→ 取它和内容高度的较大值。
  public static float Height(float h,MainTabWindow_Research w)=>Mathf.Max(h,Content(w));
  // 替换 Widgets.ScrollHorizontal（它把所有滚轮都改成左右滚）。
  public static void Wheel(Rect outRect,ref Vector2 pos,Rect viewRect,float speed){
   var e=Event.current;
   float maxY=viewRect.height-outRect.height+(viewRect.width>outRect.width+1f?16f:0f);
   if(e!=null && e.type==EventType.ScrollWheel && maxY>1f && Mouse.IsOver(outRect) && (e.shift || viewRect.width<=outRect.width+1f)){
    float d=Mathf.Abs(e.delta.y)>Mathf.Abs(e.delta.x)?e.delta.y:e.delta.x;
    pos.y=Mathf.Clamp(pos.y+d*speed,0f,maxY);e.Use();return;
   }
   Widgets.ScrollHorizontal(outRect,ref pos,viewRect,speed);
  }
 }
 [HarmonyPatch(typeof(MainTabWindow_Research),"DrawRightRect")]
 public static class ResearchScrollPatch {
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code){
   var list=code.ToList();
   var width=AccessTools.Field(typeof(MainTabWindow_Research),"rightViewWidth");
   var ctor=AccessTools.Constructor(typeof(Rect),new[]{typeof(float),typeof(float),typeof(float),typeof(float)});
   var hScroll=AccessTools.Method(typeof(Widgets),nameof(Widgets.ScrollHorizontal));
   bool afterWidth=false,height=false,wheel=false;
   for(int i=0;i<list.Count;i++){
    if(list[i].LoadsField(width))afterWidth=true;
    // new Rect(0, 0, Max(rightViewWidth + 20, outRect.width), rect.height − 16)：在构造前把高度换掉。
    else if(afterWidth && !height && list[i].opcode==OpCodes.Call && Equals(list[i].operand,ctor)){
     list.InsertRange(i,new[]{new CodeInstruction(OpCodes.Ldarg_0),new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(ResearchScroll),nameof(ResearchScroll.Height)))});
     i+=2;height=true;
    }
    else if(!wheel && list[i].Calls(hScroll)){list[i].operand=AccessTools.Method(typeof(ResearchScroll),nameof(ResearchScroll.Wheel));wheel=true;}
   }
   ResearchScroll.Patched=(height?1:0)+(wheel?2:0);
   if(ResearchScroll.Patched!=3)Log.Warning("[DND边缘大陆] 研究窗口纵向滚动补丁没有完全生效（"+ResearchScroll.Patched+"），游戏版本可能改了研究窗口。");
   return list;
  }
 }
}
