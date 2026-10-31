using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.99.2 热修：Test.ps1 -Only ResearchScroll：研究窗口滚动区域高度能装下整页研究，滚轮能上下滚。
 // Core 档没有 VE 模组，用本模组的“DND边缘大陆”页验证；-Full 档里另外确认 Vanilla Books Expanded 的写作研究（原版扩展页第 7 行）在滚动范围里。
 public partial class LichChecks {
  void CheckResearchScroll(){
   Check(ResearchScroll.Patched==3,"research window transpiler replaced both the scroll-view height and the wheel handler (flags "+ResearchScroll.Patched+")");
   var w=(MainTabWindow_Research)MainButtonDefOf.Research.TabWindow;
   var field=AccessTools.Field(typeof(MainTabWindow_Research),"curTabInt");var old=field.GetValue(w);
   try{
    float Bottom(ResearchTabDef t)=>DefDatabase<ResearchProjectDef>.AllDefs.Where(p=>p.tab==t).Select(p=>p.ResearchViewY*100f+50f).DefaultIfEmpty(0f).Max();
    var tab=DefDatabase<ResearchTabDef>.GetNamed(ResearchTab.Tab);
    field.SetValue(w,tab);ResearchScroll.Forget();float h=ResearchScroll.Content(w),low=Bottom(tab);
    Check(h>=low+ResearchScroll.Margin,"DND边缘大陆 tab content height "+h+" covers the lowest project bottom "+low+" plus margins");
    Check(Mathf.Approximately(ResearchScroll.Height(300f,w),h) && Mathf.Approximately(ResearchScroll.Height(h+500f,w),h+500f),"scroll view height = max(window height, content): 300 → "+ResearchScroll.Height(300f,w)+", "+(h+500f)+" → "+ResearchScroll.Height(h+500f,w));
    var ve=DefDatabase<ResearchProjectDef>.GetNamedSilentFail("VBE_Writing");
    if(ve!=null){field.SetValue(w,ve.tab);ResearchScroll.Forget();float hv=ResearchScroll.Content(w);Check(hv>=ve.ResearchViewY*100f+50f+ResearchScroll.Margin,"原版扩展 tab content height "+hv+" reaches VBE_Writing at row "+ve.ResearchViewY);}
   }finally{field.SetValue(w,old);ResearchScroll.Forget();}
   // 滚轮：不需要左右滚时直接上下滚；需要左右滚时普通滚轮照旧左右滚，Shift + 滚轮上下滚；都不越界。
   var oldEvent=Event.current;
   try{
    Rect outRect=new Rect(0f,0f,800f,600f);
    Vector2 Spin(Rect view,bool shift,float dy,Vector2 start){var pos=start;Event.current=new Event{type=EventType.ScrollWheel,delta=new Vector2(0f,dy),mousePosition=new Vector2(400f,300f),shift=shift};ResearchScroll.Wheel(outRect,ref pos,view,20f);return pos;}
    var tall=new Rect(0f,0f,800f,1200f);var both=new Rect(0f,0f,2000f,1200f);
    var a=Spin(tall,false,3f,Vector2.zero);var b=Spin(both,false,3f,Vector2.zero);var c=Spin(both,true,3f,Vector2.zero);var d=Spin(tall,false,100f,Vector2.zero);var e=Spin(tall,false,-3f,new Vector2(0f,10f));
    Check(Mathf.Approximately(a.y,60f) && a.x==0f,"tall-only tab: wheel scrolls down 60 px "+a);
    Check(b.y==0f && b.x!=0f,"wide and tall tab: plain wheel still scrolls sideways "+b);
    Check(Mathf.Approximately(c.y,60f) && c.x==0f,"wide and tall tab: Shift + wheel scrolls down "+c);
    Check(Mathf.Approximately(d.y,600f) && e.y==0f,"vertical scroll is clamped to [0, view − window] ("+d.y+", "+e.y+")");
   }finally{Event.current=oldEvent;}
  }
 }
}
