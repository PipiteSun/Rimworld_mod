using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 2026-10-28：Test.ps1 -Only MindControl：原版 / VPE 灵能失控（causedByPsycast）按心智抗性抵抗与缩短。
 public partial class LichChecks {
  bool McPanic(Pawn p,bool psycast,bool mood=false)=>p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"测试",true,false,mood,null,true,false,psycast);
  static void McClear(Pawn p){p.MentalState?.RecoverFromState();ExternalControl.pending.Clear();}

  void CheckMindControl(){
   var stat=DNDMind.Stat;var ward=TestGene("LPTest_MindWard",null,stat,.6f);var ward2=TestGene("LPTest_MindWardStrong",null,stat,.6f);
   try{
    // 1. 没有抗性：灵能失控照常发生，不缩短
    var p=HtPawn(spawn:true);
    bool started=McPanic(p,true);var ms=p.MentalState;if(ms!=null)ms.forceRecoverAfterTicks=6000;
    Check(started && ms!=null && ms.causedByPsycast && ExternalControl.pending.Count==0 && ExternalControl.ApplyPending()==0 && ms.forceRecoverAfterTicks==6000,"no mind resistance: psycast panic starts and is not shortened");
    McClear(p);

    // 2. 60% 抗性：约 60% 抵抗，没抵抗住的剩余时间 ×0.4（VPE 之后写入的时长也会被缩短）
    p.genes.AddGene(ward,true);
    int resisted=0,shortened=0,wrong=0;Rand.PushState(2810);
    try{
     for(int i=0;i<400;i++){
      if(!McPanic(p,true)){resisted++;continue;}
      var s=p.MentalState;s.forceRecoverAfterTicks=6000;
      if(ExternalControl.ApplyPending()==1 && s.forceRecoverAfterTicks==2400)shortened++;else wrong++;
      McClear(p);
     }
    }finally{Rand.PopState();}
    Check(resisted>200 && resisted<280 && wrong==0 && shortened==400-resisted,"60% mind resistance resists psycast panic "+resisted+"/400; every failure shortened 6000 → 2400 ticks ("+shortened+", wrong "+wrong+")");

    // 3. 心情崩溃 / 非灵能的强制状态不受影响
    p.genes.AddGene(ward2,true);
    bool moodOk=McPanic(p,false,true);McClear(p);bool plainOk=McPanic(p,false);McClear(p);
    bool allResisted=Enumerable.Range(0,50).All(i=>!McPanic(p,true));
    Check(moodOk && plainOk && allResisted && !p.InMentalState,"100% resistance: mood breaks and non-psycast forced states still start, psycast panic always resisted");

    // 4. 缩短上限 75%（抵抗判定之外，直接按状态算）
    p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"测试",true);var cap=p.MentalState;
    if(cap!=null){cap.forceRecoverAfterTicks=4000;ExternalControl.Shorten(cap);}
    Check(cap!=null && cap.forceRecoverAfterTicks==1000,"duration cut is capped at 75%: "+cap?.forceRecoverAfterTicks);
    McClear(p);

    // 5. 敌对单位不受保护
    var hostile=Find.FactionManager.AllFactions.FirstOrDefault(f=>!f.IsPlayer && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction);
    if(hostile==null){Check(false,"test map has a hostile humanlike faction");return;}
    var foe=HtPawn(null,hostile,true,3,0);
    foe.genes.AddGene(ward,true);foe.genes.AddGene(ward2,true);
    bool foePanic=McPanic(foe,true);
    Check(!ExternalControl.Covers(foe) && foePanic && ExternalControl.pending.Count==0,"hostile pawn with 100% resistance is not protected from psycast panic");
    McClear(foe);
   }finally{
    ExternalControl.pending.Clear();
    foreach(var x in htMade){if(x.Dead && x.Corpse!=null && !x.Corpse.Destroyed)x.Corpse.Destroy();else if(!x.Destroyed)x.Destroy();}htMade.Clear();
   }
  }
 }
}
