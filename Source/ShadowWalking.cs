using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Unity.Collections;
namespace LichPhylactery {
 public static class ShadowWalking {
  [ThreadStatic] public static Pawn StartingPawn;
  // Scratch storage is reused, but search results are never cached: doors,
  // terrain, danger and implants can change within the same game tick.
  [ThreadStatic] static SearchBuffer searchBuffer;
  sealed class SearchBuffer {
   public readonly PriorityQueue<IntVec3,int> open=new PriorityQueue<IntVec3,int>();
   public int[] seen,closed,cost,parent;public int stamp;public bool busy;
   public void Begin(int size){
    if(seen==null || seen.Length<size){seen=new int[size];closed=new int[size];cost=new int[size];parent=new int[size];stamp=0;}
    if(stamp==int.MaxValue){Array.Clear(seen,0,seen.Length);Array.Clear(closed,0,closed.Length);stamp=0;}
    stamp++;open.Clear();busy=true;
   }
  }
  static readonly MethodInfo initializePath=AccessTools.Method(typeof(PawnPath),"Initialize");
  // 1.58.6：寻路每一步、可达性、开门判断都会问。只有装过暗影之腿的小人（HediffComp_ShadowLeg 加上 / 读档时登记）才去查 hediff，其他人直接返回。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<int,bool> everHad=new System.Collections.Concurrent.ConcurrentDictionary<int,bool>();
  static int tracked;
  public static void Track(Pawn p){if(p!=null && everHad.TryAdd(p.thingIDNumber,true))System.Threading.Interlocked.Increment(ref tracked);}
  public static bool Active(Pawn p)=>p!=null && tracked>0 && everHad.ContainsKey(p.thingIDNumber) && !p.Dead && FleshImplants.Has(p,"ShadowLeg");
  // Only the implanted pawn gets this traversal rule; map grids and other pawns
  // remain untouched. Water/void terrain stays impassable beneath any building.
  public static bool Passable(IntVec3 c,Map map){
   if(map==null || !c.InBounds(map) || c.GetTerrain(map).passability==Traversability.Impassable)return false;
   var b=c.GetEdifice(map);
   return c.Walkable(map) || b!=null && (b.def.passability==Traversability.Impassable || b is Building_Door);
  }
  static bool End(IntVec3 c,LocalTargetInfo target,Map map,PathEndMode mode,Pawn p)=>c.Standable(map) && !(c.GetEdifice(map) is Building_Door) && ReachabilityImmediate.CanReachImmediate(c,target,map,mode,p);
  // A* on cardinal cells avoids corner clipping and the native region graph,
  // which deliberately separates sealed rooms. Use the same search for reachability.
  public static List<IntVec3> Route(Pawn p,Map map,IntVec3 start,LocalTargetInfo target,PathEndMode mode,Danger danger){
   Search(p,map,start,target,mode,danger,true,out var route);return route;
  }
  public static bool CanReach(Pawn p,Map map,IntVec3 start,LocalTargetInfo target,PathEndMode mode,Danger danger)=>Search(p,map,start,target,mode,danger,false,out _);
  static bool Search(Pawn p,Map map,IntVec3 start,LocalTargetInfo target,PathEndMode mode,Danger danger,bool buildPath,out List<IntVec3> route){
   route=null;
   if(map==null || !start.InBounds(map) || !target.IsValid || !target.Cell.InBounds(map) || target.HasThing && target.Thing.Map!=map)return false;
   if(mode==PathEndMode.OnCell && (!target.Cell.Standable(map) || target.Cell.GetEdifice(map) is Building_Door))return false;
   var buffer=searchBuffer??(searchBuffer=new SearchBuffer());if(buffer.busy)buffer=new SearchBuffer();
   buffer.Begin(map.Size.x*map.Size.z);var open=buffer.open;int stamp=buffer.stamp;
   int startIndex=map.cellIndices.CellToIndex(start);open.Enqueue(start,0);buffer.seen[startIndex]=stamp;buffer.cost[startIndex]=0;
   try{while(open.Count>0){
    var at=open.Dequeue();int atIndex=map.cellIndices.CellToIndex(at);if(buffer.closed[atIndex]==stamp)continue;buffer.closed[atIndex]=stamp;
    if(End(at,target,map,mode,p)){
     if(buildPath){route=new List<IntVec3>{at};while(atIndex!=startIndex){atIndex=buffer.parent[atIndex];route.Add(map.cellIndices.IndexToCell(atIndex));}}
     return true;
    }
    foreach(var offset in GenAdj.CardinalDirections){
     var next=at+offset;if(!Passable(next,map))continue;int nextIndex=map.cellIndices.CellToIndex(next);if(buffer.closed[nextIndex]==stamp)continue;
     if(next.GetEdifice(map) is Building_Door door && door.IsForbiddenToPass(p))continue;
     if(next.GetDangerFor(p,map)>danger)continue;
     int cost=buffer.cost[atIndex]+13+Math.Max(0,next.GetTerrain(map).pathCost);
     if(buffer.seen[nextIndex]==stamp && buffer.cost[nextIndex]<=cost)continue;
     buffer.seen[nextIndex]=stamp;buffer.cost[nextIndex]=cost;if(buildPath)buffer.parent[nextIndex]=atIndex;
     int estimate=Math.Max(0,Math.Abs(next.x-target.Cell.x)+Math.Abs(next.z-target.Cell.z)-2)*13;
     open.Enqueue(next,cost+estimate);
    }
   }return false;}finally{buffer.open.Clear();buffer.busy=false;}
  }
  public static PawnPath Path(Pawn p,Map map,IntVec3 start,LocalTargetInfo target,PathEndMode mode,Danger danger){
   var route=Route(p,map,start,target,mode,danger);if(route==null)return PawnPath.NotFound;
   var result=map.pawnPathPool.GetPath();var points=new NativeList<IntVec3>(Allocator.Temp);
   try{foreach(var c in route)points.Add(c);initializePath.Invoke(result,new object[]{points,route.Count*13});}finally{points.Dispose();}return result;
  }
 }
 public class HediffCompProperties_ShadowLeg : HediffCompProperties {public HediffCompProperties_ShadowLeg(){compClass=typeof(HediffComp_ShadowLeg);}}
 public class HediffComp_ShadowLeg : HediffComp {
  public override void CompPostPostAdd(DamageInfo? dinfo){base.CompPostPostAdd(dinfo);ShadowWalking.Track(Pawn);}
  public override void CompExposeData(){base.CompExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit)ShadowWalking.Track(Pawn);}
  public override void CompPostPostRemoved(){
   base.CompPostPostRemoved();var p=Pawn;
   if(p==null || !p.Spawned || ShadowWalking.Active(p) || p.Position.Standable(p.Map))return;
   // Losing the last implanted leg mid-step must not strand the pawn in masonry.
   foreach(var offset in GenRadial.RadialPattern){var c=p.Position+offset;if(c.InBounds(p.Map) && c.Standable(p.Map)){p.Position=c;p.Notify_Teleported();break;}}
  }
 }
 [HarmonyPatch(typeof(PathFinder),nameof(PathFinder.PushRequest))]
 public static class ShadowPathRequest {
  static bool Prefix(PathRequest request){var p=request.pawn??request.TraverseParms.pawn;if(!ShadowWalking.Active(p))return true;request.Resolve(ShadowWalking.Path(p,request.map,request.Start,request.Target,request.EndMode,request.TraverseParms.maxDanger));return false;}
 }
 [HarmonyPatch]
 public static class ShadowImmediatePath {
  static MethodBase TargetMethod()=>AccessTools.GetDeclaredMethods(typeof(PathFinder)).Single(m=>m.Name=="FindPathNow" && m.GetParameters()[2].ParameterType==typeof(TraverseParms));
  static bool Prefix(Map ___map,IntVec3 start,LocalTargetInfo target,TraverseParms traverseParms,PathEndMode peMode,ref PawnPath __result){if(!ShadowWalking.Active(traverseParms.pawn))return true;__result=ShadowWalking.Path(traverseParms.pawn,___map,start,target,peMode,traverseParms.maxDanger);return false;}
 }
 [HarmonyPatch(typeof(Reachability),nameof(Reachability.CanReach),new[]{typeof(IntVec3),typeof(LocalTargetInfo),typeof(PathEndMode),typeof(TraverseParms)})]
 public static class ShadowReachability {
  static void Postfix(Map ___map,IntVec3 start,LocalTargetInfo dest,PathEndMode peMode,TraverseParms traverseParams,ref bool __result){
   if(__result && peMode!=PathEndMode.OnCell)return;
   var p=traverseParams.pawn??ShadowWalking.StartingPawn;if(!ShadowWalking.Active(p))return;
   if(peMode==PathEndMode.OnCell && (!dest.IsValid || !dest.Cell.InBounds(___map) || !dest.Cell.Standable(___map) || dest.Cell.GetEdifice(___map) is Building_Door)){__result=false;return;}
   if(!__result)__result=ShadowWalking.CanReach(p,___map,start,dest,peMode,traverseParams.maxDanger);
  }
 }
 // Native StartPath performs a pawn-less PassDoors reachability check. Scope its
 // caller explicitly so only this particular implanted pawn can pass that check.
 [HarmonyPatch(typeof(Pawn_PathFollower),nameof(Pawn_PathFollower.StartPath))]
 public static class ShadowStartScope {
  static void Prefix(Pawn ___pawn,out Pawn __state){__state=ShadowWalking.StartingPawn;ShadowWalking.StartingPawn=ShadowWalking.Active(___pawn)?___pawn:null;}
  static Exception Finalizer(Exception __exception,Pawn __state){ShadowWalking.StartingPawn=__state;return __exception;}
 }
 [HarmonyPatch(typeof(GenGrid),nameof(GenGrid.WalkableBy))]
 public static class ShadowWalkable {
  static void Postfix(IntVec3 c,Map map,Pawn pawn,ref bool __result){if(!__result && ShadowWalking.Active(pawn))__result=ShadowWalking.Passable(c,map);}
 }
 [HarmonyPatch(typeof(Pawn_PathFollower),"BuildingBlockingNextPathCell")]
 public static class ShadowNoBashing {static void Postfix(Pawn ___pawn,IntVec3 ___nextCell,ref Building __result){if(ShadowWalking.Active(___pawn) && ShadowWalking.Passable(___nextCell,___pawn.Map))__result=null;}}
 [HarmonyPatch(typeof(Pawn_PathFollower),"NextCellDoorToWaitForOrManuallyOpen")]
 public static class ShadowNoDoorWait {static void Postfix(Pawn ___pawn,ref Building_Door __result){if(ShadowWalking.Active(___pawn))__result=null;}}
 [HarmonyPatch(typeof(Building_Door),nameof(Building_Door.CanPhysicallyPass))]
 public static class ShadowDoorPass {static void Postfix(Pawn p,ref bool __result){if(ShadowWalking.Active(p))__result=true;}}
 [HarmonyPatch(typeof(Pawn_PathFollower),"PawnCanOccupy")]
 public static class ShadowOccupy {static void Postfix(Pawn ___pawn,IntVec3 c,ref bool __result){if(ShadowWalking.Active(___pawn))__result=ShadowWalking.Passable(c,___pawn.Map);}}
 [HarmonyPatch(typeof(Pawn_PathFollower),"CostToMoveIntoCell",new[]{typeof(Pawn),typeof(IntVec3)})]
 public static class ShadowWallSpeed {
  static void Postfix(Pawn pawn,IntVec3 c,ref float __result){if(ShadowWalking.Active(pawn) && c.GetEdifice(pawn.Map)?.def.passability==Traversability.Impassable)__result=pawn.TicksPerMoveCardinal+Math.Max(0,c.GetTerrain(pawn.Map).pathCost);}
 }
 [HarmonyPatch(typeof(GenClosest),nameof(GenClosest.ClosestThingReachable))]
 public static class ShadowWorkSearch {
  static void Prefix(TraverseParms traverseParams,ref int searchRegionsMin,ref int searchRegionsMax,ref bool forceAllowGlobalSearch){if(ShadowWalking.Active(traverseParams.pawn)){searchRegionsMin=0;searchRegionsMax=0;forceAllowGlobalSearch=true;}}
 }
 [HarmonyPatch(typeof(WorkGiver_DoBill),"TryFindBestIngredientsHelper")]
 public static class ShadowBillIngredients {
  // Bills have a separate region-based ingredient search. Preserve the normal
  // result (including container support), then try reachable loose items only
  // when that search failed for a shadow walker.
  static void Postfix(Predicate<Thing> thingValidator,Predicate<List<Thing>> foundAllIngredientsAndChoose,Pawn pawn,Thing billGiver,List<ThingCount> chosen,float searchRadius,ref bool __result){
   if(__result || !ShadowWalking.Active(pawn) || !pawn.Spawned)return;
   var candidates=pawn.Map.listerThings.AllThings.Where(t=>t.def.category==ThingCategory.Item && t.Spawned && t.Position.InHorDistOf(billGiver.PositionHeld,searchRadius) && !t.IsForbidden(pawn) && thingValidator(t) && pawn.CanReserveAndReach(t,PathEndMode.ClosestTouch,Danger.Some)).ToList();
   chosen.Clear();__result=foundAllIngredientsAndChoose(candidates);
  }
 }
}
