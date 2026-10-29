using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 public static class Remolding {
  public const int Duration=60000,Fee=50;
  public static ConstructDesign Current(Hediff_Construct h){var p=h.design?.Copy()??new ConstructDesign();p.specialty=ConstructDesign.Specialties.Contains(h.specialty)?h.specialty:"大脑";foreach(var s in h.pawn.skills.skills)p.passions[s.def]=s.passion;return p;}
  public static ConstructDesign Cost(Hediff_Construct h,ConstructDesign plan){var old=h.design??new ConstructDesign();return new ConstructDesign{meat=Fee+Math.Max(0,plan.meat-old.meat),steel=Math.Max(0,plan.steel-old.steel),plasteel=Math.Max(0,plan.plasteel-old.plasteel)};}
  public static bool Valid(ConstructDesign p)=>p!=null && p.meat>=0 && p.meat<=600 && p.steel>=0 && p.steel<=300 && p.plasteel>=0 && p.plasteel<=150 && ConstructDesign.Specialties.Contains(p.specialty) && (p.passions==null || p.passions.All(k=>k.Key!=null && (int)k.Value>=0 && (int)k.Value<=2));
  public static bool Changed(Hediff_Construct h,ConstructDesign p){var old=Current(h);return old.meat!=p.meat || old.steel!=p.steel || old.plasteel!=p.plasteel || (h.kind=="Scholar" && old.specialty!=p.specialty) || h.pawn.skills.skills.Any(s=>s.passion!=p.PassionFor(s.def,h.kind));}
  public static List<Thing> Supplies(Thing anchor)=>!anchor.Spawned?new List<Thing>():anchor.Map.listerThings.AllThings.Where(t=>t.Spawned && !t.Destroyed && !t.Position.Fogged(anchor.Map) && !t.IsForbidden(Faction.OfPlayer) && !anchor.Map.reservationManager.IsReserved(t) && t.Position.DistanceTo(anchor.Position)<=12 && (ConstructSeed.RawMeat(t) || t.def==ThingDefOf.Steel || t.def==ThingDefOf.Plasteel) && anchor.Map.reachability.CanReach(anchor.Position,t,PathEndMode.ClosestTouch,TraverseMode.PassDoors,Danger.Deadly)).OrderBy(t=>t.Position.DistanceTo(anchor.Position)).ThenBy(t=>t.thingIDNumber).ToList();
  public static string Missing(List<Thing> things,ConstructDesign cost){if(things.Where(ConstructSeed.RawMeat).Sum(t=>t.stackCount)<cost.meat)return "重塑池12格内可用生肉不足。";if(things.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount)<cost.steel)return "重塑池12格内可用钢铁不足。";if(things.Where(t=>t.def==ThingDefOf.Plasteel).Sum(t=>t.stackCount)<cost.plasteel)return "重塑池12格内可用玻璃钢不足。";return null;}
  public static void Apply(Hediff_Construct h,ConstructDesign target){
   // Keep the exact pawn and all skill levels/XP, implants, relationships and names.
   var plan=target.Copy();plan.LimitPassions(h.kind);var old=h.design;var wounds=h.pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>i.Part!=null).ToList();
   var maximums=wounds.ToDictionary(i=>i,i=>PartHealth.Maximum(i.Part,h.pawn));
   h.design=plan;var ratios=wounds.ToDictionary(i=>i,i=>PartHealth.Maximum(i.Part,h.pawn)/Math.Max(1,maximums[i]));h.design=old;
   bool increase=plan.Health>=(old?.Health??1f);if(increase)h.design=plan;
   foreach(var injury in wounds)injury.Severity*=ratios[injury];
   h.design=plan;if(h.kind=="Scholar")h.specialty=plan.specialty;
   foreach(var skill in h.pawn.skills.skills)skill.passion=plan.PassionFor(skill.def,h.kind);
   h.remoldDraft=null;h.pawn.health.Notify_HediffChanged(h);Necro.Refresh(h.pawn);
  }
  public static void Open(Hediff_Construct h){
   if(h.pawn?.Spawned!=true){Messages.Message("请在地图上选择造物进行重塑。",MessageTypeDefOf.RejectInput,false);return;}
   var pools=h.pawn.Map.listerThings.ThingsOfDef(Necro.Def<ThingDef>("RemoldingPool")).OfType<Building_RemoldingPool>().Where(p=>p.Occupant==null && (p.queued==null || p.queued==h.pawn) && h.pawn.CanReserveAndReach(p,PathEndMode.InteractionCell,Danger.Deadly)).OrderBy(p=>p.Position.DistanceTo(h.pawn.Position)).ToList();
   if(pools.Count==0){Messages.Message("需要一座空闲、可到达的死灵重塑池。",MessageTypeDefOf.RejectInput,false);return;}
   Find.WindowStack.Add(new Dialog_ConstructDesign(h,pools[0]));
  }
 }
 public partial class Hediff_Construct {public ConstructDesign remoldDraft;}
 public class Building_RemoldingPool : Building,IThingHolder {
  public ThingOwner<Pawn> occupants;public Pawn queued;public ConstructDesign pending;public int progress;
  public Pawn Occupant=>occupants.Count>0?occupants[0]:null;
  public Building_RemoldingPool(){occupants=new ThingOwner<Pawn>(this,true,LookMode.Deep,false);}
  public ThingOwner GetDirectlyHeldThings()=>occupants;
  public void GetChildHolders(List<IThingHolder> children)=>ThingOwnerUtility.AppendThingHoldersFromThings(children,occupants);
  public string Blocked(Hediff_Construct h,ConstructDesign plan,List<Thing> supplies){
   if(!Spawned || Destroyed || Faction!=Faction.OfPlayer)return "需要殖民地已建成的重塑池。";
   if(h?.pawn==null || h.pawn.Dead || !h.pawn.Spawned || h.pawn.Map!=Map || h.pawn.Faction!=Faction.OfPlayer || h.pawn.Downed || h.pawn.InMentalState)return "造物必须在同地图、能够行动且不处于精神崩溃。";
   if(Occupant!=null || (queued!=null && queued!=h.pawn))return "重塑池已被其他造物占用。";
   if(!h.HasControl)return "需要有效统御和主人精神力。";
   if(!Remolding.Valid(plan))return "配置超出允许范围。";
   if(plan.MajorCount(h.kind)>ConstructDesign.MajorPassionLimit)return ConstructDesign.PassionLimitReason;
   if(!Remolding.Changed(h,plan))return "配置没有变化。";
   if(!h.pawn.CanReach(this,PathEndMode.InteractionCell,Danger.Deadly))return "造物无法到达重塑池。";
   return Remolding.Missing(supplies,Remolding.Cost(h,plan));
  }
  public bool Order(Hediff_Construct h,ConstructDesign plan,out string reason){
   reason=Blocked(h,plan,Remolding.Supplies(this));if(reason!=null)return false;
   if(!h.pawn.CanReserve(this)){reason="重塑池已被预订。";return false;}
   queued=h.pawn;pending=plan.Copy();
   queued.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Necro.Def<JobDef>("EnterRemolding"),this),JobTag.Misc);
   return true;
  }
  public bool Enter(Pawn pawn,out string reason){
   var h=Necro.Construct(pawn);var things=Remolding.Supplies(this);reason=Blocked(h,pending,things);if(reason!=null)return false;
   if(queued!=pawn || pawn.Position!=InteractionCell){reason="造物需要先走到重塑池入口。";return false;}
   var cost=Remolding.Cost(h,pending);var taken=new List<Thing>();var original=pawn.Position;
   try{
    ConstructSeed.Take(things.Where(ConstructSeed.RawMeat),cost.meat,taken);ConstructSeed.Take(things.Where(t=>t.def==ThingDefOf.Steel),cost.steel,taken);ConstructSeed.Take(things.Where(t=>t.def==ThingDefOf.Plasteel),cost.plasteel,taken);
    pawn.DeSpawn();if(!occupants.TryAdd(pawn,false))throw new InvalidOperationException("Could not hold remolding pawn");
   }catch(Exception e){if(occupants.Contains(pawn))occupants.Remove(pawn);if(!pawn.Spawned)GenSpawn.Spawn(pawn,original,Map);foreach(var t in taken)if(!t.Destroyed)GenPlace.TryPlaceThing(t,Position,Map,ThingPlaceMode.Near);reason="入池失败，材料已退回。";Log.Error("[Lich] Remolding entry failed: "+e);return false;}
   foreach(var t in taken)t.Destroy();queued=null;progress=0;return true;
  }
  public override void TickRare(){base.TickRare();Process(250);}
  public void Process(int elapsed){
   if(!Spawned || Destroyed)return;
   if(Occupant==null){if(queued!=null && (queued.Dead || !queued.Spawned || queued.CurJob?.def!=Necro.Def<JobDef>("EnterRemolding") || queued.CurJob.targetA.Thing!=this)){queued=null;pending=null;}return;}
   var h=Necro.Construct(Occupant);if(h==null || Occupant.Dead){Release(false);return;}
   // The holder pauses normal pawn jobs. Maintain its existing flesh/control upkeep explicitly.
   h.Maintain(Math.Max(0,elapsed));if(!h.HasControl)return;
   progress=Math.Min(Remolding.Duration,progress+Math.Max(0,elapsed));if(progress>=Remolding.Duration)Release(true);
  }
  public bool Release(bool complete){
   var pawn=Occupant;if(pawn==null){CancelQueue();return false;}if(!Spawned)return false;
   // Prefer the entrance; a random standable cell can be inside a sealed room.
   IntVec3 cell=InteractionCell;
   if(!cell.Standable(Map) && !CellFinder.TryFindRandomCellNear(Position,Map,8,c=>c.Standable(Map) && Map.reachability.CanReach(InteractionCell,c,PathEndMode.OnCell,TraverseParms.For(pawn)),out cell))cell=Position;
   if(complete){var h=Necro.Construct(pawn);if(h==null || !Remolding.Valid(pending))return false;Remolding.Apply(h,pending);}
   occupants.Remove(pawn);GenSpawn.Spawn(pawn,cell,Map);queued=null;pending=null;progress=0;
   Messages.Message(pawn.LabelShort+(complete?"已完成死灵重塑。":"已离开重塑池，保留原配置；已投入材料不返还。"),pawn,MessageTypeDefOf.NeutralEvent,false);return true;
  }
  void CancelQueue(){var p=queued;queued=null;pending=null;if(p?.CurJob?.def==Necro.Def<JobDef>("EnterRemolding") && p.CurJob.targetA.Thing==this)p.jobs.EndCurrentJob(JobCondition.InterruptForced);}
  public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish){if(Occupant!=null)Release(false);else CancelQueue();base.DeSpawn(mode);}
  public override string GetInspectString()=>base.GetInspectString()+"\n"+(Occupant==null?(queued==null?"空闲：选择造物并配置重塑。":"等待 "+queued.LabelShort+" 入池；尚未扣除材料。"):("重塑中："+Occupant.LabelShort+"\n进度 "+(progress/(float)Remolding.Duration).ToString("P0")+"；剩余 "+((Remolding.Duration-progress)/2500f).ToString("0.#")+" 小时。\n"+(Necro.Construct(Occupant)?.HasControl==true?"统御与维护继续生效。":"统御失效，进度暂停。")));
  public override IEnumerable<Gizmo> GetGizmos(){foreach(var g in base.GetGizmos())yield return g;
   if(Occupant!=null){yield return new Command_Action{defaultLabel="取消重塑并出池",defaultDesc="保留原角色和原配置，已消耗材料不返还。",icon=Necro.Icon("Phylactery"),action=()=>Release(false)};yield break;}
   if(queued!=null){yield return new Command_Action{defaultLabel="取消入池",defaultDesc="取消尚未开始的重塑，不扣除材料。",icon=Necro.Icon("Phylactery"),action=CancelQueue};yield break;}
   yield return new Command_Action{defaultLabel="配置死灵重塑",defaultDesc="选择已有造物，修改材料、兴趣与专长。",icon=Necro.Icon("Altar"),action=()=>Find.WindowStack.Add(new FloatMenu(Map.mapPawns.FreeColonistsSpawned.Where(p=>Necro.Construct(p)!=null).Select(p=>new FloatMenuOption(p.LabelShort,()=>Find.WindowStack.Add(new Dialog_ConstructDesign(Necro.Construct(p),this)))).DefaultIfEmpty(new FloatMenuOption("地图上没有可用造物",null)).ToList()))};
  }
  public override void ExposeData(){base.ExposeData();Scribe_Deep.Look(ref occupants,"remoldingOccupants",this);Scribe_References.Look(ref queued,"queuedPawn");Scribe_Deep.Look(ref pending,"pendingDesign");Scribe_Values.Look(ref progress,"remoldingProgress");if(Scribe.mode==LoadSaveMode.PostLoadInit && occupants==null)occupants=new ThingOwner<Pawn>(this,true,LookMode.Deep,false);}
 }
 public class JobDriver_EnterRemolding : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){this.FailOnDestroyedNullOrForbidden(TargetIndex.A);yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.InteractionCell);yield return Toils_General.Do(()=>{var pool=(Building_RemoldingPool)job.targetA.Thing;if(!pool.Enter(pawn,out var error)){Messages.Message(error,MessageTypeDefOf.RejectInput,false);EndJobWith(JobCondition.Incompletable);}});}
 }
}

