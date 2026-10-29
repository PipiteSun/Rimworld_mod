using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 2026-10-28：Test.ps1 -Only TurretAutoAttack：征召待命的远程 / 近战小人自动攻击敌方炮塔；关机的炮塔不打。
 public partial class LichChecks {
  Pawn TaDraft(Pawn p){p.drafter.Drafted=true;p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_Combat),JobCondition.InterruptForced);p.stances.CancelBusyStanceHard();return p;}
  static void TaTick(Pawn p)=>AccessTools.Method(typeof(JobDriver_Wait),"CheckForAutoAttack").Invoke(p.jobs.curDriver,null);
  static bool TaAttacking(Pawn p,Thing t)=>p.stances.curStance is Stance_Busy b && b.focusTarg.Thing==t;

  void CheckTurretAutoAttack(){
   var hostile=Find.FactionManager.AllFactions.FirstOrDefault(f=>!f.IsPlayer && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction);
   if(hostile==null){Check(false,"test map has a hostile humanlike faction");return;}
   var turret=(Building_Turret)ThingMaker.MakeThing(ThingDef.Named("Turret_MiniTurret"),GenStuff.DefaultStuffFor(ThingDef.Named("Turret_MiniTurret")));turret.SetFactionDirect(hostile);
   try{
    GenSpawn.Spawn(turret,HtCell(3,0),map);var power=turret.GetComp<CompPowerTrader>();if(power!=null)power.PowerOn=true;

    // 1. 远程：射程内的敌方炮塔会被自动瞄准
    var gunner=HtPawn(spawn:true,dx:-3);gunner.equipment.DestroyAllEquipment();gunner.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(ThingDef.Named("Gun_AssaultRifle")));TaDraft(gunner);
    var vanilla=AttackTargetFinder.BestShootTargetFromCurrentPosition(gunner,TargetScanFlags.NeedLOSToAll|TargetScanFlags.NeedThreat|TargetScanFlags.NeedAutoTargetable)?.Thing;
    Log.Message("[LICH-TEST] turret diag: vanilla best="+(vanilla?.LabelCap??"null")+" threatDisabled="+turret.ThreatDisabled(gunner)+" autoTargetable="+AttackTargetFinder.IsAutoTargetable(turret)+" hostile="+turret.HostileTo(gunner)+" job="+gunner.CurJobDef?.defName);
    TaTick(gunner);
    Check(TaAttacking(gunner,turret),"drafted gunner auto-targets an active hostile turret in range (stance "+gunner.stances.curStance?.GetType().Name+")");

    // 2. 关火自由时不打；炮塔断电（不工作）时也不打
    TaDraft(gunner);gunner.drafter.FireAtWill=false;TaTick(gunner);bool holdFire=!TaAttacking(gunner,turret);gunner.drafter.FireAtWill=true;
    if(power!=null)power.PowerOn=false;TaDraft(gunner);TaTick(gunner);bool off=!TaAttacking(gunner,turret);if(power!=null)power.PowerOn=true;
    Check(holdFire && off,"no auto-attack with fire-at-will off ("+holdFire+") or on an unpowered turret ("+off+")");
    gunner.drafter.Drafted=false;gunner.DeSpawn();

    // 3. 近战：贴着敌方炮塔会自动砍
    var fighter=HtPawn(spawn:true,dx:2);fighter.equipment.DestroyAllEquipment();TaDraft(fighter);
    int hp=turret.HitPoints;TaTick(fighter);
    Check(TaAttacking(fighter,turret) || turret.HitPoints<hp || fighter.LastAttackedTarget.Thing==turret,"drafted melee pawn auto-attacks an adjacent hostile turret");
    fighter.drafter.Drafted=false;

    // 4. 下令攻击找开枪位置时用加成后的射程（神射手 ×1.25）
    var sniper=HtPawn(spawn:true,dx:-3);sniper.equipment.DestroyAllEquipment();sniper.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(ThingDef.Named("Gun_PumpShotgun")));
    var sv=sniper.equipment.PrimaryEq.PrimaryVerb;(Adventurer.Get(sniper)??Adventurer.TryAdd(sniper,1,true))?.AddFeat(Feats.Sharpshooter);
    Check(CastRange.Replaced>=3 && CastRange.Of(sv)>sv.verbProps.range+1f,"cast position uses boosted range: "+CastRange.Replaced+" sites, shotgun "+sv.verbProps.range+" → "+CastRange.Of(sv)+" (adventurer "+Adventurer.Enabled+"/"+(Adventurer.Get(sniper)!=null)+"/"+Feats.Has(sniper,Feats.Sharpshooter)+")");
   }finally{
    if(!turret.Destroyed)turret.Destroy();
    foreach(var x in htMade){if(x.Dead && x.Corpse!=null && !x.Corpse.Destroyed)x.Corpse.Destroy();else if(!x.Destroyed)x.Destroy();}htMade.Clear();
   }
  }
 }
}
