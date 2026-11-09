using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
namespace LichPhylactery {
 // 1.138.0 奴隶麻醉项圈（Test.ps1 -Only SlaveCollar，8 条 + 读档 1 条）。
 public partial class LichChecks {
  static string scSavedId;
  static ThingDef ScCollarDef=>DefDatabase<ThingDef>.GetNamedSilentFail("LP_AnestheticCollar");
  Pawn ScSlave(int dx,int dz,bool collar,bool slave=true){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forceGenerateNewPawn:true,mustBeCapableOfViolence:true));
   GenSpawn.Spawn(p,ScCell(dx,dz),map);
   if(slave)p.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Slave);
   if(collar)p.apparel.Wear((Apparel)ThingMaker.MakeThing(ScCollarDef),false);
   p.health.hediffSet.hediffs.RemoveAll(h=>h is Hediff_Injury || h is Hediff_MissingPart);p.health.Notify_HediffChanged(null);
   return p;
  }
  static bool ScOut(Pawn p)=>p.health.hediffSet.HasHediff(SlaveCollar.Hediff) && p.Downed && !p.InMentalState;
  void CheckSlaveCollar(){
   var made=new List<Pawn>();
   try{
    // 1. 定义：项圈（奴隶衣物、颈部、压制 +15%、组件）、机械加工台配方要 DND炼金术、麻醉 hediff 意识上限 10% 半天。
    var d=ScCollarDef;var h=SlaveCollar.Hediff;
    var recipe=DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.ProducedThingDef==d);
    var cp=d?.GetCompProperties<CompProperties_AnestheticCollar>();
    Check(d!=null && !d.ConfigErrors().Any() && d.apparel.slaveApparel && d.apparel.bodyPartGroups.Any(g=>g.defName=="Neck") && !d.MadeFromStuff && d.label=="奴隶麻醉项圈"
     && Mathf.Approximately(d.equippedStatOffsets.GetStatOffsetFromList(DefDatabase<StatDef>.GetNamed("SlaveSuppressionOffset")),.15f) && cp!=null && cp.rechargeTicks==120000
     && recipe!=null && recipe.AllRecipeUsers.Any(u=>u.defName=="TableMachining") && !recipe.AllRecipeUsers.Any(u=>u.defName=="HandTailoringBench") && recipe.researchPrerequisite?.defName=="LP_DNDAlchemy"
     && h!=null && h.stages[0].capMods.Any(c=>c.capacity==PawnCapacityDefOf.Consciousness && Mathf.Approximately(c.setMax,.1f)) && h.CompProps<HediffCompProperties_Disappears>()?.disappearsAfterTicks.min==30000,
     "collar def (slave apparel, neck, +15% suppression, recharge 2 days), machining-table recipe behind DND炼金术, LP_CollarAnesthetic caps consciousness at 10% for half a day");
    // 2. 戴项圈的奴隶开始煽动叛乱：立刻结束心理状态、昏倒、压制 +30%、项圈进入充填。
    var a=ScSlave(-12,12,true);made.Add(a);var need=a.needs.TryGetNeed<Need_Suppression>();need.CurLevel=.2f;
    var rebel=DefDatabase<MentalStateDef>.GetNamed("Rebellion");
    bool started=a.mindState.mentalStateHandler.TryStartMentalState(rebel,null,true);
    var comp=SlaveCollar.Collar(a);
    Check(started && ScOut(a) && Mathf.Abs(need.CurLevel-.5f)<.01f && !comp.Ready && comp.TicksLeft>119000 && comp.timesFired==1,
     "collared slave starting a rebellion is knocked out at once (no mental state, downed, suppression .2→"+need.CurLevel.ToString("0.00")+"), collar now recharging "+comp.TicksLeft);
    // 3. 充填中不再触发；充填好之后发狂也会被放倒。
    a.health.RemoveHediff(a.health.hediffSet.GetFirstHediffOfDef(SlaveCollar.Hediff));
    var berserk=MentalStateDefOf.Berserk;
    a.mindState.mentalStateHandler.TryStartMentalState(berserk,null,true);bool stillBerserk=a.InMentalState && !a.health.hediffSet.HasHediff(SlaveCollar.Hediff);
    a.MentalState?.RecoverFromState();comp.lastFired=-999999;
    a.mindState.mentalStateHandler.TryStartMentalState(berserk,null,true);
    Check(stillBerserk && ScOut(a) && comp.timesFired==2,"while recharging the collar does nothing (berserk stays); once recharged a berserk slave is knocked out too");
    // 4. 不是奴隶（殖民者戴着）不触发；没戴项圈的奴隶不触发；非攻击性心理状态（悲伤游荡）不触发。
    var col=ScSlave(-10,12,true,false);made.Add(col);col.mindState.mentalStateHandler.TryStartMentalState(berserk,null,true);
    var bare=ScSlave(-8,12,false);made.Add(bare);bare.mindState.mentalStateHandler.TryStartMentalState(rebel,null,true);
    var sad=ScSlave(-6,12,true);made.Add(sad);sad.mindState.mentalStateHandler.TryStartMentalState(DefDatabase<MentalStateDef>.GetNamed("Wander_Sad"),null,true);
    Check(col.InMentalState && !col.Downed && bare.InMentalState && !bare.Downed && sad.InMentalState && !sad.Downed && SlaveCollar.Collar(sad).Ready,
     "no trigger for a colonist wearing it, a slave without it, or a non-aggressive break (sad wander)");
    foreach(var p in new[]{col,bare,sad})p.MentalState?.RecoverFromState();
    // 5. 一场奴隶叛乱：叛乱小队里戴项圈的奴隶被放倒并移出小队，没戴的照常叛乱。
    foreach(var p in made.ToList()){if(!p.Destroyed)p.Destroy();made.Remove(p);}
    var r1=ScSlave(-12,8,true);var r2=ScSlave(-11,8,true);var r3=ScSlave(-10,8,false);made.AddRange(new[]{r1,r2,r3});
    foreach(var p in made)p.needs.TryGetNeed<Need_Suppression>().CurLevel=0f;
    bool reb=SlaveRebellionUtility.StartSlaveRebellion(r3,true);
    var rebels=map.lordManager.lords.Where(l=>l.LordJob is LordJob_SlaveRebellion).SelectMany(l=>l.ownedPawns).ToList();
    Check(reb && ScOut(r1) && ScOut(r2) && !rebels.Contains(r1) && !rebels.Contains(r2) && rebels.Contains(r3) && !r3.Downed,
     "slave rebellion: both collared slaves knocked out and dropped from the rebel lord, the uncollared one still rebels (started "+reb+", rebels "+rebels.Count+")");
    foreach(var l in map.lordManager.lords.Where(l=>l.LordJob is LordJob_SlaveRebellion).ToList())map.lordManager.RemoveLord(l);
    // 6. 半天后自己醒来。
    var hd=r1.health.hediffSet.GetFirstHediffOfDef(SlaveCollar.Hediff) as HediffWithComps;var dis=hd.TryGetComp<HediffComp_Disappears>();
    Check(dis!=null && dis.ticksToDisappear>29000 && dis.ticksToDisappear<=30000,"anesthetic wears off in half a day ("+dis?.ticksToDisappear+" ticks)");
    dis.ticksToDisappear=1;Step(300);
    Check(!r1.health.hediffSet.HasHediff(SlaveCollar.Hediff) && !r1.Downed,"after it wears off the slave wakes up");
    // 7. 状态按钮：玩家奴隶身上有「麻醉项圈：充填中」按钮。
    var gz=r1.GetGizmos().OfType<Command_Action>().FirstOrDefault(g=>g.defaultLabel!=null && g.defaultLabel.StartsWith("麻醉项圈"));
    Check(gz!=null && gz.defaultLabel=="麻醉项圈：充填中" && SlaveCollar.Collar(r1).CompInspectStringExtra().Contains("重新充填中"),"worn gizmo + inspect line show the recharge state");
   }finally{foreach(var p in made)if(!p.Destroyed)p.Destroy();}
  }
  void PrepareSlaveCollarSave(){
   var p=ScSlave(-14,10,true);scSavedId=p.ThingID;var c=SlaveCollar.Collar(p);c.lastFired=Find.TickManager.TicksGame;c.timesFired=3;
  }
  void CheckSlaveCollarAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==scSavedId);var c=SlaveCollar.Collar(p);
   Check(p!=null && p.IsSlave && c!=null && c.timesFired==3 && !c.Ready,"collar recharge state and trigger count survive save/load");
   p?.Destroy();
  }
 }
}
