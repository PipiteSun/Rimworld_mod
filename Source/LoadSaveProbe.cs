using System;
using System.IO;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.119.0 自检工具：Test.ps1 -Full -LoadSave <存档名> —— 在隔离档里读取玩家存档（脚本先把存档复制进隔离档），
 // 正常速度跑约 1500 帧，写 LichResult.txt 后退出；日志红字由 Test.ps1 照常扫描。只在命令行带 -lichloadsave 时生效。
 [StaticConstructorOnStartup]
 public static class LoadSaveProbe {
  public static string Name;static int frames;static bool started,done;
  static LoadSaveProbe(){
   if(!GenCommandLine.TryGetCommandLineArg("lichloadsave",out Name) || Name.NullOrEmpty())return;
   LongEventHandler.ExecuteWhenFinished(()=>{started=true;GameDataSaveLoader.LoadGame(Name);});
  }
  public static void Frame(){
   if(!started || done || Current.ProgramState!=ProgramState.Playing)return;
   frames++;
   if(frames%100==0)Log.ResetMessageCount();
   if(frames==60){Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;}
   if(frames==120){ // 渲染诊断：镜头对准殖民者并截图，逐个检查渲染树
    var map=Find.CurrentMap;Pawn focus=null;
    if(map!=null)foreach(var p in map.mapPawns.AllPawnsSpawned){
     if(focus==null && p.IsColonist)focus=p;
     try{var tree=p.Drawer?.renderer?.renderTree;
      if(tree==null){Log.Warning("[LICH-RENDER] "+p+" no render tree");continue;}
      if(!tree.Resolved){tree.SetDirty();tree.EnsureInitialized(PawnRenderFlags.None);}
      Log.Message("[LICH-RENDER] "+p+" resolved="+tree.Resolved+" root="+(tree.rootNode!=null));
     }catch(Exception e){Log.Warning("[LICH-RENDER] "+p+" threw "+e);}
    }
    if(focus!=null){Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;Find.CameraDriver.JumpToCurrentMapLoc(focus.Position);Find.CameraDriver.SetRootPosAndSize(focus.DrawPos,12f);}
   }
   if(frames==200)UnityEngine.ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath,"LoadSave-pawns.png"));
   if(frames==260)Find.TickManager.CurTimeSpeed=TimeSpeed.Superfast;
   if(frames<1500)return;done=true;
   string res="PASS: loaded save "+Name+", ran "+Find.TickManager.TicksGame+" ticks game time, maps "+Find.Maps.Count;
   File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"LichResult.txt"),res);Log.Message("[LICH-TEST] "+res);
   Root.Shutdown();
  }
 }
 [HarmonyPatch(typeof(Root_Play),nameof(Root_Play.Update))]
 public static class LoadSaveProbeFrame {static void Postfix(){if(LoadSaveProbe.Name!=null)LoadSaveProbe.Frame();}}
}
