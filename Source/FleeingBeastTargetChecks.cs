using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 2026-11-04：Test.ps1 -Only FleeingBeast：发狂野兽切成仓皇逃窜后仍敌对、仍是威胁、征召近战会砍；普通受惊野兽保持原版。
 public partial class LichChecks {
  void CheckFleeingBeast(){
   var wolfDef=PawnKindDef.Named("Warg");
   var beast=PawnGenerator.GeneratePawn(wolfDef,null);var prey=PawnGenerator.GeneratePawn(wolfDef,null);
   try{
    GenSpawn.Spawn(beast,HtCell(1,0),map);GenSpawn.Spawn(prey,HtCell(0,4),map);
    var fighter=HtPawn(spawn:true,dx:0);fighter.equipment.DestroyAllEquipment();

    // 1. 发狂 → 逃窜：仍敌对、仍算威胁
    beast.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter,forced:true);
    bool hostileBefore=beast.HostileTo(fighter);
    beast.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,forced:true,forceWake:true);
    Check(beast.MentalStateDef==MentalStateDefOf.PanicFlee && hostileBefore,"warg went manhunter → panic flee (state "+beast.MentalStateDef?.defName+", hostile before "+hostileBefore+")");
    Check(beast.HostileTo(fighter) && beast.HostileTo(Faction.OfPlayer) && fighter.HostileTo(beast),"fleeing ex-manhunter stays hostile to colonists");
    Check(GenHostility.IsPotentialThreat(beast) && GenHostility.IsActiveThreatTo(beast,Faction.OfPlayer),"fleeing ex-manhunter still counts as a threat");
    Check(map.attackTargetsCache.GetPotentialTargetsFor(fighter).Contains(beast),"fleeing ex-manhunter is in the colonist's attack-target cache");

    // 2. 征召近战待命：贴身的逃窜野兽会被自动攻击
    TaDraft(fighter);TaTick(fighter);
    Check(TaAttacking(fighter,beast) || fighter.LastAttackedTarget.Thing==beast,"drafted melee pawn auto-attacks the adjacent fleeing beast (stance "+fighter.stances.curStance?.GetType().Name+")");
    fighter.drafter.Drafted=false;

    // 3. 普通野兽直接受惊逃跑：保持原版（不敌对、不算威胁）
    prey.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,forced:true,forceWake:true);
    Check(prey.MentalStateDef==MentalStateDefOf.PanicFlee && !prey.HostileTo(fighter) && !GenHostility.IsPotentialThreat(prey),"plain wild animal panic-fleeing stays non-hostile (vanilla)");

    // 4. 逃窜结束后敌意随之结束
    beast.mindState.mentalStateHandler.Reset();
    Check(!beast.HostileTo(fighter),"after the flee ends the wild warg is no longer hostile");
   }finally{
    foreach(var b in new[]{beast,prey})if(!b.Destroyed)b.Destroy();
    foreach(var x in htMade){if(x.Dead && x.Corpse!=null && !x.Corpse.Destroyed)x.Corpse.Destroy();else if(!x.Destroyed)x.Destroy();}htMade.Clear();
   }
  }
 }
}
