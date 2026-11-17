using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.167.0：自检用，在附近空地建一间 5×5 带屋顶、木门、单人床的卧室（原为力能龙宝库的建造代码）。
 public static class TestRooms {
  public static List<Thing> Bedroom(Map map,IntVec3 near,Pawn owner,out CellRect interior){
   interior=CellRect.Empty;var built=new List<Thing>();
   var wallDef=ThingDefOf.Wall;var stone=DefDatabase<ThingDef>.GetNamed("BlocksGranite");var wood=ThingDefOf.WoodLog;
   foreach(var c in GenRadial.RadialCellsAround(near,40f,true)){
    var outer=CellRect.CenteredOn(c,3);
    if(!outer.InBounds(map) || outer.ExpandedBy(1).Cells.Any(x=>!x.InBounds(map)))continue;
    if(!outer.Cells.All(x=>x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map)==null && x.GetFirstPawn(map)==null && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth) && GenConstruct.CanBuildOnTerrain(wallDef,x,map,Rot4.North)))continue;
    var door=new IntVec3(c.x,0,c.z-3);
    foreach(var e in outer.EdgeCells){
     var t=e==door?ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Door"),wood):ThingMaker.MakeThing(wallDef,stone);
     t.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(t,e,map,WipeMode.Vanish);built.Add(t);
    }
    interior=CellRect.CenteredOn(c,2);
    foreach(var x in interior.Cells)map.roofGrid.SetRoof(x,RoofDefOf.RoofConstructed);
    var bed=(Building_Bed)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Bed"),wood);bed.SetFactionDirect(Faction.OfPlayer);
    GenSpawn.Spawn(bed,new IntVec3(c.x-2,0,c.z),map,Rot4.North,WipeMode.Vanish);built.Add(bed);
    owner?.ownership?.ClaimBedIfNonMedical(bed);
    return built;
   }
   return built;
  }
 }
}
