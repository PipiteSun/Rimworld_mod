using System;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public partial class LichChecks {
  static string moltenSavedId;static int moltenSavedActive,moltenSavedFatigue,moltenSavedCooldown;
  void CheckMoltenGuard(Pawn p){
   var research=DefDatabase<ResearchProjectDef>.GetNamed("LP_MoltenForging");
   Check(!research.ConfigErrors().Any() && research.baseCost==4000 && research.prerequisites.Any(r=>r.defName=="AdvancedFabrication"),"molten forging research is valid with advanced prerequisite");
   foreach(var name in new[]{MoltenGuard.Maul,MoltenGuard.Helmet,MoltenGuard.Armor})Check(DefDatabase<RecipeDef>.GetNamed("Make_"+name).researchPrerequisite==research,"molten recipe is gated by research: "+name);
   Check(MoltenGuard.Complete(p),"complete molten guard set recognized");
   p.stances.stunner.StopStun();p.stances.stunner.StunFor(100,null,false,false);Check(p.stances.stunner.StunTicksLeft==70,"molten helmet reduces actual native stun by exactly 30 percent");p.stances.stunner.StopStun();
   var helmet=p.apparel.WornApparel.First(a=>a.def.defName==MoltenGuard.Helmet);p.apparel.Remove(helmet);
   p.stances.stunner.StunFor(100,null,false,false);Check(p.stances.stunner.StunTicksLeft==100,"no helmet means no stun resistance");p.stances.stunner.StopStun();
   Check(!MoltenGuard.Activate(p),"incomplete set cannot activate overdrive");p.apparel.Wear(helmet);
   float damage=p.GetStatValue(StatDefOf.MeleeDamageFactor,true,-1),move=p.GetStatValue(StatDefOf.MoveSpeed,true,-1);int start=Find.TickManager.TicksGame;
   Check(MoltenGuard.Activate(p),"full set activates overdrive");var state=MoltenGuard.State(p);
   Check(state.activeUntil==start+600 && state.fatigueUntil==start+1200 && state.cooldownUntil==start+5400,"exact 10 second boost, 10 second fatigue and 90 second cooldown");
   Check(Near(p.GetStatValue(StatDefOf.MeleeDamageFactor,true,-1),damage*1.25f) && Near(p.GetStatValue(StatDefOf.MoveSpeed,true,-1),move*1.15f),"overdrive applies real damage and move stat multipliers: damage "+damage+" -> "+p.GetStatValue(StatDefOf.MeleeDamageFactor,true,-1)+", move "+move+" -> "+p.GetStatValue(StatDefOf.MoveSpeed,true,-1));
   Check(!MoltenGuard.Activate(p),"overdrive cannot stack or restart its cooldown");
   Find.TickManager.DebugSetTicksGame(start+600);state.Sync();Check(state.Phase==2 && Near(p.GetStatValue(StatDefOf.MeleeDamageFactor,true,-1),damage) && Near(p.GetStatValue(StatDefOf.MoveSpeed,true,-1),move*.85f),"boost ends at 600 ticks and applies 15 percent fatigue slowdown");
   Find.TickManager.DebugSetTicksGame(start+1200);state.Sync();Check(state.Phase==0 && Near(p.GetStatValue(StatDefOf.MoveSpeed,true,-1),move) && !MoltenGuard.Activate(p),"fatigue ends at 1200 ticks while cooldown remains");
   Find.TickManager.DebugSetTicksGame(start+5400);state.Sync();Check(MoltenGuard.Activate(p),"overdrive becomes available again exactly at 90 seconds");
   int expiry=state.cooldownUntil;p.apparel.Remove(helmet);state.Sync();Check(state.Phase==2 && state.fatigueUntil==Find.TickManager.TicksGame+600 && state.cooldownUntil==expiry,"unequipping ends boost, starts fatigue and preserves cooldown");p.apparel.Wear(helmet);
   Check(!MoltenGuard.Activate(p) && Near(p.GetStatValue(StatDefOf.MeleeDamageFactor,true,-1),damage),"reequipping does not restore boost or bypass cooldown");
   p.health.RemoveHediff(state);Find.TickManager.DebugSetTicksGame(start);
  }
  void PrepareMoltenSave(){
   var p=Person();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);p.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_FireGiantCore"),true);p.equipment.DestroyAllEquipment();p.apparel.DestroyAll();
   WearVisual(p,MoltenGuard.Helmet);WearVisual(p,MoltenGuard.Armor);p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(MoltenGuard.Maul)));
   Check(MoltenGuard.Activate(p),"save fixture starts active overdrive");var h=MoltenGuard.State(p);moltenSavedId=p.ThingID;moltenSavedActive=h.activeUntil;moltenSavedFatigue=h.fatigueUntil;moltenSavedCooldown=h.cooldownUntil;
  }
  void CheckMoltenAfterLoad(){
   var p=Find.CurrentMap.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==moltenSavedId);var h=MoltenGuard.State(p);
   Check(MoltenGuard.Complete(p) && h!=null && h.activeUntil==moltenSavedActive && h.fatigueUntil==moltenSavedFatigue && h.cooldownUntil==moltenSavedCooldown,"full native save/load preserves set and all overdrive deadlines");
   Check(h.Phase==1 && !MoltenGuard.Activate(p),"loaded active overdrive retains boost and blocks reuse");
   p.apparel.Remove(p.apparel.WornApparel.First(a=>a.def.defName==MoltenGuard.Helmet));h.Sync();Check(h.Phase==2 && h.cooldownUntil==moltenSavedCooldown,"loaded cooldown survives unequipping");
   p.Destroy();
  }
 }
}
