using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.101.0 秘金深钻：照原版深层钻机的玩法（通电、采矿工作、小人站在交互格钻），但不看深层矿藏，
 // 每钻满一份随机出秘银或精金。原版 CompDeepDrill / WorkGiver_DeepDrill 的方法都不是虚方法且写死原版钻机，
 // 所以这里是一套自己的组件、工作和工作驱动，数值逻辑照抄原版（钻探速度、采矿产量、每份进度）。
 public class CompProperties_MythicDrill : CompProperties {
  public float workPerPortion=15000f,adamantineChance=.25f;
  public int mithralPerPortion=7,adamantinePerPortion=3;
  public CompProperties_MythicDrill(){compClass=typeof(CompMythicDrill);}
 }
 public class CompMythicDrill : ThingComp {
  public CompProperties_MythicDrill Props=>(CompProperties_MythicDrill)props;
  CompPowerTrader power;
  public float portionProgress,portionYieldPct;
  int lastUsedTick=-99999;
  public override void PostSpawnSetup(bool respawningAfterLoad){power=parent.GetComp<CompPowerTrader>();}
  public override void PostExposeData(){
   Scribe_Values.Look(ref portionProgress,"portionProgress");
   Scribe_Values.Look(ref portionYieldPct,"portionYieldPct");
   Scribe_Values.Look(ref lastUsedTick,"lastUsedTick",-99999);
  }
  public bool CanDrillNow()=>power==null || power.PowerOn;
  public bool UsedLastTick()=>lastUsedTick>=Find.TickManager.TicksGame-1;
  public float ProgressPercent=>portionProgress/Props.workPerPortion;
  // 和原版 DrillWorkDone 一样：进度按钻探速度累加，产量按采矿产量折算；满一份就出货。
  public void DrillWorkDone(Pawn driller,int delta=1){
   float work=driller.GetStatValue(StatDefOf.DeepDrillingSpeed)*delta;
   portionProgress+=work;
   portionYieldPct+=work*driller.GetStatValue(StatDefOf.MiningYield)/Props.workPerPortion;
   lastUsedTick=Find.TickManager.TicksGame;
   if(portionProgress>Props.workPerPortion){Produce(portionYieldPct,driller,Rand.Value);portionProgress=0f;portionYieldPct=0f;}
  }
  public ThingDef Pick(float roll)=>roll<Props.adamantineChance?ItemMaterials.Adamantine:ItemMaterials.Mithral;
  // 矮人钻秘银翻倍，和秘银矿脉一致。
  public int CountFor(ThingDef metal,float yieldPct,Pawn driller){
   int n=Math.Max(1,GenMath.RoundRandom((metal==ItemMaterials.Adamantine?Props.adamantinePerPortion:Props.mithralPerPortion)*yieldPct));
   if(metal==ItemMaterials.Mithral && CommonRaces.IsDwarf(driller))n*=2;
   return n;
  }
  public Thing Produce(float yieldPct,Pawn driller,float roll){
   var metal=Pick(roll);if(metal==null || !parent.Spawned)return null;
   var t=ThingMaker.MakeThing(metal);t.stackCount=CountFor(metal,yieldPct,driller);
   GenPlace.TryPlaceThing(t,parent.InteractionCell,parent.Map,ThingPlaceMode.Near,null,p=>p!=parent.Position && p!=parent.InteractionCell);
   return t;
  }
  public override string CompInspectStringExtra(){
   if(!parent.Spawned)return null;
   return "钻进进度："+ProgressPercent.ToStringPercent("F0")+"（每份 "+(1f-Props.adamantineChance).ToStringPercent("F0")+" 秘银×"+Props.mithralPerPortion+"，"+Props.adamantineChance.ToStringPercent("F0")+" 精金×"+Props.adamantinePerPortion+"）";
  }
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   foreach(var g in base.CompGetGizmosExtra())yield return g;
   if(DebugSettings.ShowDevGizmos)yield return new Command_Action{defaultLabel="DEV: 钻完这一份",action=()=>{portionProgress=Props.workPerPortion+1f;portionYieldPct=1f;Produce(1f,null,Rand.Value);portionProgress=0f;portionYieldPct=0f;}};
  }
 }
 public class WorkGiver_MythicDrill : WorkGiver_Scanner {
  public static ThingDef Def=>DefDatabase<ThingDef>.GetNamedSilentFail("LP_MythicDrill");
  public override ThingRequest PotentialWorkThingRequest=>Def!=null?ThingRequest.ForDef(Def):ThingRequest.ForGroup(ThingRequestGroup.Nothing);
  public override PathEndMode PathEndMode=>PathEndMode.InteractionCell;
  public override Danger MaxPathDanger(Pawn pawn)=>Danger.Deadly;
  public override bool ShouldSkip(Pawn pawn,bool forced=false){
   var list=pawn.Map.listerBuildings.allBuildingsColonist;
   for(int i=0;i<list.Count;i++){var b=list[i];if(b.def!=Def)continue;var c=b.TryGetComp<CompMythicDrill>();if(c!=null && c.CanDrillNow() && b.Map.designationManager.DesignationOn(b,DesignationDefOf.Uninstall)==null)return false;}
   return true;
  }
  public override bool HasJobOnThing(Pawn pawn,Thing t,bool forced=false){
   if(t.Faction!=pawn.Faction || !(t is Building b))return false;
   if(!pawn.CanReserve(b,1,-1,null,forced))return false;
   var c=b.TryGetComp<CompMythicDrill>();if(c==null || !c.CanDrillNow())return false;
   if(b.Map.designationManager.DesignationOn(b,DesignationDefOf.Uninstall)!=null || b.IsBurning())return false;
   return true;
  }
  public override Job JobOnThing(Pawn pawn,Thing t,bool forced=false)=>JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("LP_OperateMythicDrill"),t,1500,true);
 }
 public class JobDriver_OperateMythicDrill : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
   this.FailOnBurningImmobile(TargetIndex.A);
   this.FailOnThingHavingDesignation(TargetIndex.A,DesignationDefOf.Uninstall);
   this.FailOn(()=>!(job.targetA.Thing.TryGetComp<CompMythicDrill>()?.CanDrillNow()??false));
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.InteractionCell);
   var work=ToilMaker.MakeToil("OperateMythicDrill");
   work.tickIntervalAction=delta=>{var actor=work.actor;job.targetA.Thing.TryGetComp<CompMythicDrill>().DrillWorkDone(actor,delta);actor.skills?.Learn(SkillDefOf.Mining,.065f*delta);};
   work.defaultCompleteMode=ToilCompleteMode.Never;
   work.WithEffect(EffecterDefOf.Drill,TargetIndex.A);
   work.FailOnCannotTouch(TargetIndex.A,PathEndMode.InteractionCell);
   work.activeSkill=()=>SkillDefOf.Mining;
   yield return work;
  }
 }
}
