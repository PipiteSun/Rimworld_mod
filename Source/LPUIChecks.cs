using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.124.0 UI 整理：Test.ps1 -Only LPUI。
 // 检查：LPUI.Fit 把窗口夹进屏幕（大屏不变、小屏缩小、有下限）、按钮行均分；各窗口在当前测试分辨率下的初始尺寸不出屏。
 // 自测通过后（-lichuitest）依次打开 8 个窗口，各截一张 UI-<序号>-<名字>.png（画窗口时出错会在日志里留红字，Test.ps1 会报）。
 public partial class LichChecks {
  void CheckLPUI(){
   Check(LPUI.FitTo(840f,710f,1200f,800f)==new Vector2(840f,710f),"[ui] Fit: a window that fits the screen keeps its size (840×710 on 1200×800)");
   Check(LPUI.FitTo(940f,660f,1024f,600f)==new Vector2(940f,560f),"[ui] Fit: too tall for 1024×600 → height clamped to 560, width kept");
   Check(LPUI.FitTo(800f,580f,640f,480f)==new Vector2(600f,440f),"[ui] Fit: 640×480 → 600×440");
   Check(LPUI.FitTo(800f,580f,100f,100f)==new Vector2(LPUI.MinWindow,LPUI.MinWindow),"[ui] Fit: never smaller than "+LPUI.MinWindow);
   var row=LPUI.Row(new Rect(10f,0f,100f,30f),3,5f);
   Check(row.Length==3 && Near(row[0].x,10f) && Near(row[2].xMax,110f) && Near(row[1].x-row[0].xMax,5f) && Near(row[0].width,row[2].width),"[ui] Row: three equal buttons with 5 px gaps");
   var p=Person();
   var wins=new Window[]{new Dialog_Ascension(p),new Dialog_UsurperShards(p),new Dialog_ControlWeather(map),new Dialog_ChooseFavored(p),new Window_GearEnhance(altar),new Window_QuestBoard(altar),new SystemHostWindow(p),new Dialog_FleshCatalysis(altar),new Window_FleshRegen(altar)};
   var big=wins.Where(w=>w.InitialSize.x>UI.screenWidth-LPUI.ScreenMargin+.5f || w.InitialSize.y>UI.screenHeight-LPUI.ScreenMargin+.5f).Select(w=>w.GetType().Name+" "+w.InitialSize).ToList();
   Check(big.Count==0,"[ui] all "+wins.Length+" windows fit the "+UI.screenWidth+"×"+UI.screenHeight+" screen ("+string.Join(", ",big)+")");
   Check(new SystemHostWindow(p).InitialSize==new Vector2(840f,710f),"[ui] system host window keeps 840×710 on the test screen");
   Check(new Dialog_ControlWeather(map).InitialSize.y>=330f,"[ui] weather window is at least as tall as before (330)");
   p.Destroy();
  }

  static int lpuiFrames;static Pawn lpuiPawn;static Thing lpuiCharm;
  static readonly string[] LpuiNames={"Ascension","UsurperShards","ControlWeather","ChooseFavored","GearEnhance","QuestBoard","SystemHost","FateRewrite"};
  static Window LpuiWindow(int i){
   var p=lpuiPawn;var map=Find.CurrentMap;var bench=map.listerThings.AllThings.OfType<Building_Altar>().FirstOrDefault();
   switch(i){
    case 0:return new Dialog_Ascension(p);
    case 1:return new Dialog_UsurperShards(p);
    case 2:return new Dialog_ControlWeather(map);
    case 3:return new Dialog_ChooseFavored(p);
    case 4:{if(bench==null)return null;var w=new Window_GearEnhance(bench);var first=GearEnhanceBench.Candidates(map).FirstOrDefault();if(first!=null)AccessTools.Field(typeof(Window_GearEnhance),"selected").SetValue(w,first);return w;}
    case 5:return bench==null?null:new Window_QuestBoard(bench);
    case 6:SystemHost.State?.For(p);return new SystemHostWindow(p);
    case 7:return lpuiCharm==null?null:new Dialog_FateRewrite(lpuiCharm,p);
   }
   return null;
  }
  static bool CaptureLPUI(){
   if(lpuiFrames==0)return false;lpuiFrames++;
   string dir=GenFilePaths.SaveDataFolderPath;
   if(lpuiFrames==10){
    var map=Find.CurrentMap;lpuiPawn=map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
    if(lpuiPawn!=null){
     Adventurer.TestSuppressed=false;Adventurer.TryAdd(lpuiPawn,5,true);
     if(SystemHost.CharmDef!=null){lpuiCharm=ThingMaker.MakeThing(SystemHost.CharmDef);lpuiCharm.stackCount=2;GenPlace.TryPlaceThing(lpuiCharm,lpuiPawn.Position,map,ThingPlaceMode.Near);}
     var weapon=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle"),GenStuff.DefaultStuffFor(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle")));weapon.HitPoints=weapon.MaxHitPoints/2;GenPlace.TryPlaceThing(weapon,lpuiPawn.Position,map,ThingPlaceMode.Near);weapon.SetForbidden(false,false);
    }
    Messages.Clear();
   }
   for(int i=0;i<LpuiNames.Length;i++){
    int start=20+i*40;
    if(lpuiFrames==start && lpuiPawn!=null){var w=LpuiWindow(i);if(w!=null)Find.WindowStack.Add(w);}
    if(lpuiFrames==start+25)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"UI-"+i+"-"+LpuiNames[i]+".png"));
    if(lpuiFrames==start+35)foreach(var w in Find.WindowStack.Windows.Where(w=>w.GetType().Namespace==typeof(LPUI).Namespace).ToList())w.Close(false);
   }
   if(lpuiFrames>=20+LpuiNames.Length*40+10)Application.Quit();
   return true;
  }
 }
}
