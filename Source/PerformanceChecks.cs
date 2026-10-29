using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 public partial class LichChecks {
  static readonly bool visualTestEnabled=GenCommandLine.CommandLineArgPassed("lichvisualcompat");
  static readonly bool testEnabled=visualTestEnabled || GenCommandLine.CommandLineArgPassed("lichtest");
  static object performanceSink;
  static void Measure(string name,int count,Action action){
   for(int i=0;i<100;i++)action();
   var times=new double[5];var allocations=new long[5];
   for(int round=0;round<5;round++){
    long bytes=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
    for(int i=0;i<count;i++)action();
    watch.Stop();times[round]=watch.Elapsed.TotalMilliseconds;allocations[round]=GC.GetAllocatedBytesForCurrentThread()-bytes;
   }
   Array.Sort(times);Array.Sort(allocations);
   File.AppendAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"LichPerformance.txt"),$"{name}: {count} calls, median {times[2]:F3} ms, {allocations[2]} bytes\n");
  }
  void CheckPerformance(){
   File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"LichPerformance.txt"),"Five warmed rounds; allocations are current-thread managed bytes. Microbenchmarks, not colony TPS.\n");
   var scholar=Necro.LivingConstructs(worker).First(p=>Necro.Construct(p).kind=="Scholar");var h=Necro.Construct(scholar);
   Measure("Scholar stage",10000,()=>performanceSink=h.CurStage);
   Measure("Lich stage",10000,()=>performanceSink=Necro.Lich(worker).CurStage);
   Measure("Control check",10000,()=>{if(h.HasControl)performanceSink=h;});
   var ordinary=map.mapPawns.FreeColonistsSpawned.First(p=>!Necro.Undead(p));
   Measure("Ordinary pawn implant lookup",100000,()=>{if(FleshImplants.Has(ordinary,"ShadowLeg"))performanceSink=ordinary;});
   Measure("Ordinary pawn construct lookup",100000,()=>performanceSink=Necro.Construct(ordinary));
   Check(Necro.Registry.constructs.Count==Necro.Registry.constructs.Distinct().Count(),"construct registry has no duplicate upkeep entries");
   foreach(var p in Necro.LivingConstructs(worker)){
    int cost=0;foreach(var candidate in Necro.LivingConstructs(worker).OrderBy(c=>c.thingIDNumber)){cost+=Necro.Construct(candidate).Cost;if(candidate==p)break;}
    bool expected=Necro.Lich(worker).Active && worker.Faction==p.Faction && Necro.Lich(worker).Points>.01f && cost<=Necro.Lich(worker).box.Capacity;
    Check(Necro.Construct(p).HasControl==expected,"linear control allocation matches sorted allocation: "+p.ThingID);
   }
   var stage=h.CurStage;Check(ReferenceEquals(stage,h.CurStage),"unchanged construct stage is reused");
   var savedDesign=h.design;h.design=savedDesign?.Copy()??new ConstructDesign();h.design.steel+=60;
   var changed=h.CurStage;Check(!ReferenceEquals(stage,changed) && Near(changed.statOffsets.First(m=>m.stat==StatDefOf.ArmorRating_Sharp).value-(stage.statOffsets.FirstOrDefault(m=>m.stat==StatDefOf.ArmorRating_Sharp)?.value??0),.1f),"material edit immediately rebuilds stage without double armor");
   h.design=savedDesign;Check(!ReferenceEquals(changed,h.CurStage),"restoring material configuration invalidates stage");
   bool sleeping=h.dormant;h.dormant=!sleeping;Check(h.CurStage.capMods.Any(m=>m.capacity==PawnCapacityDefOf.Consciousness)==h.dormant,"dormancy immediately updates cached capacity");h.dormant=sleeping;
   var master=h.master;stage=h.CurStage;h.master=null;Check(!ReferenceEquals(stage,h.CurStage),"mentoring change immediately invalidates cached stage");h.master=master;
   var lich=Necro.Lich(worker);stage=lich.CurStage;int level=lich.box.level;lich.box.level++;Check(!ReferenceEquals(stage,lich.CurStage),"phylactery level immediately invalidates cached stage");lich.box.level=level;
   var walker=map.mapPawns.AllPawnsSpawned.First(p=>p.ThingID==implantPawnId);
   Check(ShadowWalking.CanReach(walker,map,implantOutside,implantRoomCenter,PathEndMode.OnCell,Danger.Deadly),"pooled reachability crosses sealed walls");
   Measure("Shadow sealed-room reachability",1000,()=>{if(ShadowWalking.CanReach(walker,map,implantOutside,implantRoomCenter,PathEndMode.OnCell,Danger.Deadly))performanceSink=walker;});
   var terrain=implantRoomCenter.GetTerrain(map);map.terrainGrid.SetTerrain(implantRoomCenter,TerrainDefOf.WaterDeep);
   Check(!ShadowWalking.CanReach(walker,map,implantOutside,implantRoomCenter,PathEndMode.OnCell,Danger.Deadly),"same-tick terrain edit invalidates reachability");
   map.terrainGrid.SetTerrain(implantRoomCenter,terrain);
   Check(ShadowWalking.CanReach(walker,map,implantOutside,implantRoomCenter,PathEndMode.OnCell,Danger.Deadly),"same-tick terrain restore restores reachability");
   var route=ShadowWalking.Route(walker,map,implantOutside,implantRoomCenter,PathEndMode.OnCell,Danger.Deadly);
   Check(route!=null && route.First()==implantRoomCenter && route.Last()==implantOutside,"pooled full path retains ordered endpoints after reachability searches");
   Check(!ShadowWalking.CanReach(walker,map,implantOutside,new IntVec3(-1,0,-1),PathEndMode.OnCell,Danger.Deadly),"out-of-bounds path target rejected");
  }
 }
}
