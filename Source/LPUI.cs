using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.124.0 UI 整理：自定义窗口共用的排版常量和小工具。只管排版与可读性，不碰任何数值。
 // 用法：
 //  · InitialSize=>LPUI.Fit(宽,高)：小分辨率 / 大界面缩放时不超出屏幕；
 //  · DoWindowContents 里 try{…}finally{LPUI.Reset();}：中途出错也不会把字号、对齐、颜色带到别的界面；
 //  · LPUI.Title：窗口标题（中号字，右边让出关闭按钮），返回下一行的 y；
 //  · LPUI.LabelFit：放不下时截断成一行，悬停看全文；
 //  · LPUI.BeginScroll / EndScroll：可滚动的 Listing_Standard（高度用上一帧量出来的值）；
 //  · LPUI.ScrollText：固定框里的长文字，超出时出现滚动条。
 public static class LPUI {
  public const float Gap=6f;          // 行与行之间
  public const float SectionGap=10f;  // 区块之间、左右两栏之间
  public const float RowH=24f;        // 单行文字
  public const float ListRowH=28f;    // 列表行（可点选）
  public const float ButtonH=32f;     // 普通按钮
  public const float MainButtonH=36f; // 主要操作按钮（开始 / 确认）
  public const float TitleH=34f;      // 标题行（中号字）
  public const float CloseXRoom=30f;  // 标题右边给关闭按钮让出的宽度
  public const float ScrollBarW=16f;
  public const float ScreenMargin=40f;
  public const float MinWindow=240f;

  // 窗口初始尺寸夹在屏幕里（UI.screenWidth / screenHeight 已经算上界面缩放）。
  public static Vector2 Fit(float w,float h)=>FitTo(w,h,UI.screenWidth,UI.screenHeight);
  public static Vector2 FitTo(float w,float h,float screenW,float screenH)=>new Vector2(Mathf.Min(w,Mathf.Max(MinWindow,screenW-ScreenMargin)),Mathf.Min(h,Mathf.Max(MinWindow,screenH-ScreenMargin)));

  // 恢复默认文字状态。
  public static void Reset(){Text.Font=GameFont.Small;Text.Anchor=TextAnchor.UpperLeft;Text.WordWrap=true;GUI.color=Color.white;}

  // 窗口标题：中号字一行；closeX=true 时右边让出关闭按钮。返回标题下面一行的 y。
  public static float Title(Rect r,string title,bool closeX=true){
   var tr=new Rect(r.x,r.y,r.width-(closeX?CloseXRoom:0f),TitleH);
   Text.Font=GameFont.Medium;LabelFit(tr,title);Text.Font=GameFont.Small;
   return r.y+TitleH+Gap;
  }

  // 一行标签：放得下照常画；放不下就截断成一行（带富文本的不截，只加悬停），悬停显示全文。tip 另外附在悬停里。
  public static void LabelFit(Rect r,string s,string tip=null){
   if(s.NullOrEmpty()){if(!tip.NullOrEmpty())TooltipHandler.TipRegion(r,tip);return;}
   bool fits=Text.CalcHeight(s,r.width)<=r.height+1f;
   if(fits || s.IndexOf('<')>=0)Widgets.Label(r,s);
   else Widgets.Label(r,s.Truncate(r.width));
   if(!fits)TooltipHandler.TipRegion(r,tip.NullOrEmpty()?s:s+"\n\n"+tip);
   else if(!tip.NullOrEmpty())TooltipHandler.TipRegion(r,tip);
  }

  // 多行文字：按宽度量高、画出，返回占用的高度。
  public static float Para(float x,float y,float width,string s){
   if(s.NullOrEmpty())return 0f;
   float h=Text.CalcHeight(s,width);Widgets.Label(new Rect(x,y,width,h),s);return h;
  }

  // 固定框里的长文字：放得下直接画，放不下就在框里滚动。
  public static void ScrollText(Rect r,string s,ref Vector2 scroll){
   if(s.NullOrEmpty())return;
   float h=Text.CalcHeight(s,r.width);
   if(h<=r.height){Widgets.Label(r,s);return;}
   var view=new Rect(0f,0f,r.width-ScrollBarW,Text.CalcHeight(s,r.width-ScrollBarW));
   Widgets.BeginScrollView(r,ref scroll,view);Widgets.Label(view,s);Widgets.EndScrollView();
  }

  // 可滚动的 Listing_Standard：contentH 传上一帧 EndScroll 的返回值（第一帧随便给个数）。
  // Listing 用一个很高的矩形，避免内容超出时自动换到看不见的第二列（1.52.0 设置页踩过的坑）。
  public static Listing_Standard BeginScroll(Rect outRect,ref Vector2 scroll,float contentH){
   var view=new Rect(0f,0f,outRect.width-ScrollBarW,Mathf.Max(contentH,outRect.height-1f));
   Widgets.BeginScrollView(outRect,ref scroll,view);
   var l=new Listing_Standard();l.Begin(new Rect(0f,0f,view.width,100000f));return l;
  }
  public static float EndScroll(Listing_Standard l){float h=l.CurHeight+4f;l.End();Widgets.EndScrollView();return h;}

  // 按钮：文字放不下时截断，悬停看全文；active=false 时画成灰色、点了不算。
  public static bool Button(Rect r,string label,bool active=true,string tip=null,bool background=true){
   string shown=label??"";float room=r.width-10f;
   if(Text.CalcSize(shown).x>room && shown.IndexOf('<')<0)shown=shown.Truncate(room);
   bool hit=Widgets.ButtonText(r,shown,background,true,active);
   if(shown!=label)TooltipHandler.TipRegion(r,tip.NullOrEmpty()?label:label+"\n\n"+tip);
   else if(!tip.NullOrEmpty())TooltipHandler.TipRegion(r,tip);
   return hit && active;
  }

  // 代替禁用按钮的灰色说明（居中，放不下就截断 + 悬停）。
  public static void Unavailable(Rect r,string reason){
   var c=GUI.color;var a=Text.Anchor;GUI.color=Color.gray;Text.Anchor=TextAnchor.MiddleCenter;
   try{LabelFit(r,reason);}finally{Text.Anchor=a;GUI.color=c;}
  }

  // 按绝对坐标排版的窗口：窗口被 Fit 压矮到 minH 以下时，整块内容放进滚动区（rect 换成滚动区里的坐标）。
  // 返回 true 时调用方画完要 Widgets.EndScrollView()。
  public static bool BeginMinHeight(ref Rect rect,float minH,ref Vector2 scroll){
   if(rect.height>=minH)return false;
   var view=new Rect(0f,0f,rect.width-ScrollBarW,minH);Widgets.BeginScrollView(rect,ref scroll,view);rect=view;return true;
  }

  // 一行均分成 n 个按钮位。
  public static Rect[] Row(Rect r,int n,float gap=SectionGap){
   var res=new Rect[Math.Max(1,n)];float w=(r.width-gap*(res.Length-1))/res.Length;
   for(int i=0;i<res.Length;i++)res[i]=new Rect(r.x+i*(w+gap),r.y,w,r.height);
   return res;
  }
 }
}
