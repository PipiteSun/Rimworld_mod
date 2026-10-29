using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 public partial class LichChecks {
  static List<string> lifecycleIds,omittedIds;
  static Dictionary<string,Dictionary<WorkTypeDef,int>> lifecycleWork;
  static Dictionary<string,string> lifecycleNames;
  static Dictionary<string,Dictionary<WorkTypeDef,int>> omittedWork;
  void CheckConstructLifecycles(){
   var owner=Person();GenSpawn.Spawn(owner,CellFinder.RandomClosewalkCellNear(altar.Position,map,5),map);
   var box=Rituals.Ascend(owner);box.level=10;GenPlace.TryPlaceThing(box,altar.Position,map,ThingPlaceMode.Near);
   owner.psychicEntropy.OffsetPsyfocusDirectly(1);
   lifecycleIds=new List<string>();lifecycleWork=new Dictionary<string,Dictionary<WorkTypeDef,int>>();lifecycleNames=new Dictionary<string,string>();
   foreach(var kind in new[]{"Skeleton","Golem","Scholar"}){
    var p=Constructs.Create(kind,owner,CellFinder.RandomClosewalkCellNear(altar.Position,map,5),map,"大脑");
    var h=Necro.Construct(p);var registry=Necro.Registry;
    foreach(var work in DefDatabase<WorkTypeDef>.AllDefsListForReading)p.workSettings.SetPriority(work,0);
    p.workSettings.SetPriority(WorkTypeDefOf.Cleaning,1);p.workSettings.SetPriority(WorkTypeDefOf.Crafting,2);p.workSettings.SetPriority(WorkTypeDefOf.Research,4);
    for(int hour=0;hour<24;hour++)p.timetable.SetAssignment(hour,TimeAssignmentDefOf.Work);
    var workBefore=WorkSnapshot(p);
    var rename=h.GetGizmos().OfType<Command_Action>().Single(c=>c.defaultLabel=="重命名造物");rename.action();
    var dialog=Find.WindowStack.Windows.OfType<Dialog_NamePawn>().Single();
    AccessTools.PropertySetter(typeof(Dialog_NamePawn),"CurPawnNick").Invoke(dialog,new object[]{"复生测试·"+kind});
    p.Name=(Name)AccessTools.Method(typeof(Dialog_NamePawn),"BuildName").Invoke(dialog,null);dialog.Close();
    Check(p.Name.ToStringShort=="复生测试·"+kind,kind+" rename command opens native name editor and builds new name");
    lifecycleNames[p.ThingID]=p.Name.ToStringFull;
    registry.constructs.Remove(p);h.dormant=true;p.health.Notify_HediffChanged(h);p.health.CheckForStateChange(null,h);
    Check(p.Downed,kind+" lost registry fixture is downed");
    registry.ReconcileConstructs();h.Maintain(0);
    Check(h.HasControl && !h.dormant && !p.Downed,kind+" registry rebuild wakes omitted pawn");
    registry.constructs.Remove(p);h.Maintain(0);h.Maintain(0);
    Check(registry.constructs.Count(x=>x==p)==1 && h.HasControl,kind+" maintenance restores membership without duplication");
    owner.psychicEntropy.OffsetPsyfocusDirectly(-1);h.Maintain(0);Check(h.dormant && p.Downed,kind+" still sleeps on actual focus shortage");
    owner.psychicEntropy.OffsetPsyfocusDirectly(1);h.Maintain(0);Check(!h.dormant && !p.Downed,kind+" wakes after focus recovery");
    p.Kill(null);registry.constructs.Remove(p);
    Check(h.savedWorkPriorities!=null && workBefore.All(k=>h.savedWorkPriorities[k.Key]==k.Value),kind+" captures enabled and disabled work before death clears tracker");
    Check(ResurrectionUtility.TryResurrect(p,new ResurrectionParams{restoreMissingParts=true,removeDiedThoughts=true}),kind+" native resurrection succeeds");
    Check(h.HasControl && !h.dormant && !p.Downed && registry.constructs.Count(x=>x==p)==1,kind+" resurrection restores control immediately");
    Check(SameWork(p,workBefore) && h.savedWorkPriorities==null,kind+" resurrection retains exact player work priorities");
    Check(p.Name.ToStringFull==lifecycleNames[p.ThingID],kind+" custom name survives resurrection");
    CheckConstructCleaning(p,kind+" after resurrection");
    lifecycleIds.Add(p.ThingID);lifecycleWork[p.ThingID]=WorkSnapshot(p);
    p.Kill(null);registry.constructs.Remove(p);p.Corpse.SetForbidden(true,false);h.appearanceVersion=1;p.story.bodyType=BodyTypeDefOf.Fat;h.legacyThingID=p.ThingID;p.def=h.LegacyRace;
   }
  }
  static Dictionary<WorkTypeDef,int> WorkSnapshot(Pawn p)=>DefDatabase<WorkTypeDef>.AllDefsListForReading.ToDictionary(w=>w,w=>p.workSettings.GetPriority(w));
  static bool SameWork(Pawn p,Dictionary<WorkTypeDef,int> expected)=>expected.All(k=>p.workSettings.GetPriority(k.Key)==k.Value);
  void CheckConstructCleaning(Pawn p,string context){
   foreach(var other in map.mapPawns.FreeColonistsSpawned.Where(x=>x!=p).ToList())if(other.drafter!=null)other.drafter.Drafted=true;
   p.drafter.Drafted=false;p.jobs.StopAll();
   var at=CellFinder.RandomClosewalkCellNear(p.Position,map,3);map.areaManager.Home[at]=true;
   var filth=(Filth)ThingMaker.MakeThing(ThingDefOf.Filth_Dirt);GenSpawn.Spawn(filth,at,map);
   var giver=new WorkGiver_CleanFilth{def=DefDatabase<WorkGiverDef>.AllDefsListForReading.First(w=>w.giverClass==typeof(WorkGiver_CleanFilth))};
   Check(p.workSettings.GetPriority(WorkTypeDefOf.Cleaning)>0 && !p.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning),context+" cleaning remains enabled");
   var job=giver.JobOnThing(p,filth,true);Check(job!=null,context+" native cleaning work giver returns a job");
   p.jobs.StartJob(job,JobCondition.InterruptForced);
   for(int ticks=0;ticks<1200 && !filth.Destroyed;ticks++)Step(1);
   Check(filth.Destroyed,context+" actually completes cleaning");p.jobs.StopAll();
  }
  void OmitConstructsBeforeSave(){
   var alive=Necro.Registry.constructs.Where(p=>!p.Dead && !p.Destroyed).ToList();
   omittedIds=alive.Select(p=>p.ThingID).ToList();omittedWork=alive.ToDictionary(p=>p.ThingID,WorkSnapshot);
   Necro.Registry.constructs.Clear();
  }
  void CheckConstructsAfterLoad(){
   var registry=Necro.Registry;
   foreach(var id in omittedIds){
    var p=registry.constructs.Single(x=>x.ThingID==id);var h=Necro.Construct(p);
    // The migration fixture intentionally initializes work during its one-time upgrade.
    if(id!=designedPawnId)Check(SameWork(p,omittedWork[id]),p.def.defName+" read-load recovery preserves work choices");
    h.master.psychicEntropy.OffsetPsyfocusDirectly(1);h.Maintain(0);
    Check(h.HasControl && !h.dormant,p.def.defName+" omitted live/contained pawn regains control after owner returns");
   }
   Check(registry.constructs.Any(p=>p.ThingID==remoldPawnId && !p.Spawned),"load recovers missing registry entry inside remolding pool");
  }
  void CheckConstructCorpsesAfterLoad(){
   var registry=Necro.Registry;
   foreach(var id in lifecycleIds){
    var p=map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Select(c=>c.InnerPawn).Single(x=>x.ThingID==id);
    var h=Necro.Construct(p);h.master.psychicEntropy.OffsetPsyfocusDirectly(1);
    Check(p.Dead && h.savedWorkPriorities!=null,id+" dead save retains work snapshot");
    Check(ResurrectionUtility.TryResurrect(p,new ResurrectionParams{restoreMissingParts=true,removeDiedThoughts=true}),id+" resurrects after corpse save/load");
    Check(h.HasControl && !h.dormant && !p.Downed && registry.constructs.Count(x=>x==p)==1,id+" corpse load and resurrection restore control");Check(h.appearanceVersion==3 && p.def==ThingDefOf.Human && p.ThingID==id && p.story.bodyType==(h.kind=="Golem"?BodyTypeDefOf.Hulk:BodyTypeDefOf.Thin) && Hediff_Construct.AppearanceGenesFor(h.kind).All(n=>p.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed(n))),id+" old corpse migrates role appearance on resurrection");
    Check(SameWork(p,lifecycleWork[id]) && p.Name.ToStringFull==lifecycleNames[id],id+" corpse load and resurrection preserve name and all priorities");
    CheckConstructCleaning(p,p.def.defName+" after corpse load and resurrection");
   }
  }
 }
}
