using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.144.0 热修：Test.ps1 -Only ColonistPanic。玩家的人被吓跑最多 10 秒、走到家园地图边缘不会出图；敌人照原版。
 public partial class LichChecks {
  void CheckColonistPanic(){
   try{
    var mine=HtPawn(spawn:true,dx:0);var foe=HtPawn(null,AdvEnemy,spawn:true,dx:3);
    mine.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"测试",true,true);
    foe.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"测试",true,true);
    var ms=mine.MentalState;var fs=foe.MentalState;
    Check(ms!=null && ms.def==MentalStateDefOf.PanicFlee && ms.forceRecoverAfterTicks>0 && ms.forceRecoverAfterTicks<=ColonistPanicGuard.MaxTicks,"colonist panic flee is capped at 10 s (forceRecover "+ms?.forceRecoverAfterTicks+")");
    Check(fs!=null && fs.def==MentalStateDefOf.PanicFlee && (fs.forceRecoverAfterTicks<0 || fs.forceRecoverAfterTicks>ColonistPanicGuard.MaxTicks),"enemy panic flee keeps vanilla duration (forceRecover "+fs?.forceRecoverAfterTicks+")");

    // 走到家园地图边缘：组不成远行队 → 不出图，当场回过神
    bool home=!map.exitMapGrid.MapUsesExitGrid;
    var edge=CellFinder.RandomEdgeCell(map);
    var job=JobMaker.MakeJob(JobDefOf.Goto,edge);job.exitMapOnArrival=true;
    mine.jobs.StartJob(job,JobCondition.InterruptForced);
    var drv=mine.jobs.curDriver as JobDriver_Goto;
    if(drv!=null)AccessTools.Method(typeof(JobDriver_Goto),"TryExitMap").Invoke(drv,null);
    Check(home && drv!=null && mine.Spawned && !mine.InMentalState && mine.Faction==Faction.OfPlayer && !Find.WorldPawns.Contains(mine),"panicking colonist at the home map edge stays on the map and snaps out of it (home "+home+", spawned "+mine.Spawned+", state "+mine.MentalStateDef?.defName+")");
   }finally{
    foreach(var x in htMade){if(Find.WorldPawns.Contains(x))Find.WorldPawns.RemovePawn(x);if(x.Dead && x.Corpse!=null && !x.Corpse.Destroyed)x.Corpse.Destroy();else if(!x.Destroyed)x.Destroy();}htMade.Clear();
   }
  }
 }
}
