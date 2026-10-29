using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 public partial class LichChecks {
  static int savedRevivalTick;
  static Pawn coldSubject;
  static bool ColdTemperature(Thing __instance,ref float __result){if(__instance!=coldSubject)return true;__result=-400f;return false;}
  void CheckNecromancyGenes(){
   Check(NecromancyGenes.HasBody(worker) && NecromancyGenes.HasTouch(worker),"ritual automatically grants both new active archite genes");
   Check(Necro.Def<GeneDef>("NecroticBody").biostatArc>0 && Necro.Def<GeneDef>("NecroticTouch").biostatArc>0,"both new genes are archite genes");
   Check(worker.needs.food==null && worker.needs.rest==null && worker.needs.joy==null,"dead body gene removes food, sleep and recreation needs");
   var ordinary=Person();GenSpawn.Spawn(ordinary,CellFinder.RandomClosewalkCellNear(altar.Position,map,5),map);
   Check(ordinary.needs.food!=null && ordinary.needs.rest!=null && ordinary.needs.joy!=null,"ordinary humans retain their needs");
   var probe=new Harmony("local.lich.coldtest");
   var ambient=AccessTools.PropertyGetter(typeof(Thing),"AmbientTemperature");
   try{
    probe.Patch(ambient,prefix:new HarmonyMethod(typeof(LichChecks),nameof(ColdTemperature)));
    var cold=new HediffGiver_Hypothermia{hediff=HediffDefOf.Hypothermia};
    coldSubject=worker;for(int i=0;i<20;i++)cold.OnIntervalPassed(worker,null);
    Check(!worker.health.hediffSet.HasHediff(HediffDefOf.Hypothermia),"native hypothermia giver cannot affect dead body at -400 C");
    coldSubject=ordinary;cold.OnIntervalPassed(ordinary,null);
    Check(ordinary.health.hediffSet.HasHediff(HediffDefOf.Hypothermia),"ordinary pawn still suffers native hypothermia");
   }finally{coldSubject=null;probe.Unpatch(ambient,HarmonyPatchType.Prefix,probe.Id);}
   float health=worker.health.hediffSet.GetPartHealth(worker.RaceProps.body.corePart);
   var frost=worker.TakeDamage(new DamageInfo(DamageDefOf.Frostbite,10,999,hitPart:worker.RaceProps.body.corePart));
   Check(frost.totalDamageDealt==0 && Near(health,worker.health.hediffSet.GetPartHealth(worker.RaceProps.body.corePart)),"native frostbite damage is fully resisted");
   if(ModsConfig.OdysseyActive){Check(Near(worker.GetStatValue(StatDefOf.VacuumResistance,true,-1),1),"vacuum resistance is exactly 100 percent without equipment");Check(!worker.HarmedByVacuum,"native vacuum harm gate excludes dead body");}

   // Invoke the actual successful-hit damage method with a native unarmed verb.
   ordinary.apparel.DestroyAll();ordinary.stances.stunner.StopStun();
   var verb=worker.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().First();
   var hit=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget");
   hit.Invoke(verb,new object[]{new LocalTargetInfo(ordinary)});
   // 1.20.0：完整模组列表里有模组会弹开这一拳或改眩晕时长；那里允许重试几次、时长放宽，核心环境仍要求正好2秒。
   for(int retry=0;FullModList && retry<5 && !ordinary.Dead && ordinary.stances.stunner.StunTicksLeft==0;retry++)hit.Invoke(verb,new object[]{new LocalTargetInfo(ordinary)});
   int stun=ordinary.stances.stunner.StunTicksLeft;
   Check(!ordinary.Dead && (FullModList?stun>0 && stun<=240:stun==120),"native unarmed melee hit stuns living human for two seconds: "+stun);
   ordinary.stances.stunner.StopStun();
   ordinary.TakeDamage(new DamageInfo(DamageDefOf.Bullet,1,0,instigator:worker));
   Check(!ordinary.stances.stunner.Stunned,"ranged bullet damage does not trigger necrotic touch");
   ordinary.TakeDamage(new DamageInfo(DamageDefOf.Burn,1,0,instigator:worker));
   Check(!ordinary.stances.stunner.Stunned,"spell damage does not trigger necrotic touch");
   var animal=PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Muffalo"));GenSpawn.Spawn(animal,CellFinder.RandomClosewalkCellNear(altar.Position,map,5),map);
   var club=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Club"),ThingDefOf.WoodLog) as ThingWithComps;
   var held=worker.equipment.Primary;if(held!=null)worker.equipment.Remove(held);worker.equipment.AddEquipment(club);
   var weaponVerb=club.GetComp<CompEquippable>().AllVerbs.OfType<Verb_MeleeAttackDamage>().First();
   hit.Invoke(weaponVerb,new object[]{new LocalTargetInfo(animal)});
   Check(!animal.Dead && animal.stances.stunner.StunTicksLeft==120,"native weapon melee hit stuns living animal");
   worker.equipment.Remove(club);club.Destroy();if(held!=null)worker.equipment.AddEquipment(held);
   var mech=PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther"));GenSpawn.Spawn(mech,CellFinder.RandomClosewalkCellNear(altar.Position,map,5),map);
   hit.Invoke(verb,new object[]{new LocalTargetInfo(mech)});
   mech.stances.stunner.StopStun();NecromancyGenes.ApplyTouch(worker,mech);
   Check(!mech.stances.stunner.Stunned,"necrotic touch excludes mechanoid independently of native impact stun");
   Check(!NecromancyGenes.IsLivingTarget(worker),"lich is excluded from living targets");
   foreach(var p in new[]{ordinary,animal,mech}){if(p.Spawned)p.DeSpawn();p.Destroy();}

   var bodyOnly=Person();bodyOnly.genes.AddGene(Necro.Def<GeneDef>("NecroticBody"),true);
   Check(bodyOnly.needs.food==null && bodyOnly.needs.rest==null && bodyOnly.needs.joy==null,"body gene itself removes needs without a lich hediff");
   bodyOnly.genes.RemoveGene(bodyOnly.genes.GetGene(Necro.Def<GeneDef>("NecroticBody")));bodyOnly.needs.AddOrRemoveNeedsAsAppropriate();
   Check(bodyOnly.needs.food!=null && bodyOnly.needs.rest!=null && bodyOnly.needs.joy!=null,"removing standalone body gene restores ordinary needs");
   bodyOnly.Destroy();
  }
  void CheckRevivalAfterLoad(Phylactery box,Pawn lich){
   Check(box.revivalTick==savedRevivalTick && box.revivalTick>Find.TickManager.TicksGame,"three-day timer survives full save/load without restart");
   Check(NecromancyGenes.HasBody(lich) && NecromancyGenes.HasTouch(lich),"existing lich migration restores missing new genes once");
   var h=Necro.Lich(lich);h.UpgradeNecromancyGenes();
   Check(lich.genes.GenesListForReading.Count(g=>g.def==Necro.Def<GeneDef>("NecroticTouch"))==1,"gene migration is idempotent");
   Find.TickManager.DebugSetTicksGame(savedRevivalTick-1);box.ProcessRevival();
   Check(lich.Dead && box.souls.Contains(lich),"no resurrection one tick before three days");
   altar.DeSpawn();
   Find.TickManager.DebugSetTicksGame(savedRevivalTick);box.ProcessRevival();
   Check(!lich.Dead && lich.Spawned && box.soul==lich && !box.souls.Contains(lich),"automatic resurrection at three days needs no table, material or worker");
   Check(Near(lich.psychicEntropy.CurrentPsyfocus,0) && box.revivalTick==-1,"revival starts with zero focus and clears timer");
   var id=lich.ThingID;box.ProcessRevival();Check(lich.ThingID==id && map.mapPawns.AllPawnsSpawned.Count(p=>p.ThingID==id)==1,"repeated revival check never duplicates pawn");
   GenSpawn.Spawn(altar,CellFinder.RandomClosewalkCellNear(box.Position,map,5),map);
  }
  void CheckDestroyedRevival(){
   var doomed=Person();GenSpawn.Spawn(doomed,CellFinder.RandomClosewalkCellNear(altar.Position,map,5),map);
   var box=Rituals.Ascend(doomed);GenPlace.TryPlaceThing(box,altar.Position,map,ThingPlaceMode.Near);
   doomed.Kill(null);box.ProcessRevival();int due=box.revivalTick;
   Check(due==Find.TickManager.TicksGame+Phylactery.RevivalDelay,"new death receives fresh three-day timer");
   box.Destroy();Find.TickManager.DebugSetTicksGame(due);box.ProcessRevival();
   Check(doomed.Dead && !box.TryAutoRevive(),"destroyed phylactery cannot resurrect its dead owner");
  }
 }
}
