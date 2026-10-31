using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // 1.86.0 截图（Test.ps1 -CE -Only CharEditor，自测通过后）：打开角色编辑器和 DND 面板，四个分页各截一张 CharEditor-0..3.png。
 public partial class LichChecks {
  static int charEditorFrames;static Pawn charEditorPawn;
  static bool CaptureCharEditor(){
   if(charEditorFrames==0)return false;charEditorFrames++;
   string dir=GenFilePaths.SaveDataFolderPath;
   if(charEditorFrames==10){
    var p=Find.CurrentMap.mapPawns.FreeColonists.FirstOrDefault();charEditorPawn=p;
    if(p!=null){
     Adventurer.TestSuppressed=false;var h=Adventurer.TryAdd(p,1,true);
     if(h!=null){CharEditorCompat.SetLevel(h,12);CharEditorCompat.SetClassLevel(h,ClassSystem.Fighter,7);var sc=ClassSystem.All.FirstOrDefault(c=>ClassSubclass.Options(c).Any());if(sc!=null){CharEditorCompat.SetClassLevel(h,sc,5);CharEditorCompat.SetSubclass(h,sc,ClassSubclass.Options(sc).First());}}
     var api=AccessTools.Property(CharEditorCompat.EditorType,"API").GetValue(null);
     AccessTools.Method(CharEditorCompat.EditorType,"StartEditor").Invoke(api,new object[]{p});
    }
    Messages.Clear();
   }
   if(charEditorFrames==40){var ui=Find.WindowStack.Windows.FirstOrDefault(w=>CharEditorCompat.UiType.IsInstanceOfType(w));if(ui!=null && charEditorPawn!=null)Find.WindowStack.Add(new Window_DndEditor(charEditorPawn,ui));}
   for(int i=0;i<4;i++){
    int start=50+i*40;
    if(charEditorFrames==start){var w=CharEditorCompat.Panel();if(w!=null)w.tab=i;}
    if(charEditorFrames==start+30)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"CharEditor-"+i+".png"));
   }
   if(charEditorFrames>=220)Application.Quit();
   return true;
  }
 }
}
