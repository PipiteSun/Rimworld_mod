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
   // 编辑器自己的特性栏加两个特性（金色强力特性 + 原版「善良」），关掉编辑器，在信息卡的人物页截一张 CharEditor-4.png。
   if(charEditorFrames==220 && charEditorPawn!=null && CharEditorCompat.TraitAdd!=null){
    foreach(var w in Find.WindowStack.Windows.Where(w=>w is Window_DndEditor || CharEditorCompat.UiType.IsInstanceOfType(w)).ToList())w.Close(false);
    var gold=HeroTraits.Gold.First();
    foreach(var d in new[]{gold,TraitDefOf.Kind})if(!charEditorPawn.story.traits.HasTrait(d))CharEditorCompat.TraitAdd.Invoke(null,new object[]{charEditorPawn,d,d.degreeDatas[0],false,false,null});
    var card=new Dialog_InfoCard(charEditorPawn);AccessTools.Field(typeof(Dialog_InfoCard),"tab").SetValue(card,Dialog_InfoCard.InfoCardTab.Character);Find.WindowStack.Add(card);
    Messages.Clear();
   }
   if(charEditorFrames==250)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"CharEditor-4.png"));
   if(charEditorFrames>=270)Application.Quit();
   return true;
  }
 }
}
