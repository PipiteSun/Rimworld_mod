using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  static string remoldPawnId,remoldPoolId,remoldName;static int remoldProgress;static float remoldInjuryRatio;
  static Dictionary<SkillDef,Tuple<int,float,float>> remoldSkills;
  Building_RemoldingPool NewPool(){var at=map.AllCells.OrderBy(c=>c.DistanceToSquared(altar.Position)).First(c=>c.DistanceTo(altar.Position)>10 && map.reachability.CanReach(altar.InteractionCell,c,PathEndMode.OnCell,TraverseMode.PassDoors,Danger.Deadly) && CellRect.CenteredOn(c,3).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth)));var pool=(Building_RemoldingPool)ThingMaker.MakeThing(Necro.Def<ThingDef>("RemoldingPool"));pool.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(pool,at,map);return pool;}
  void PoolStock(Building_RemoldingPool pool,string name,int n){var t=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(name));t.stackCount=n;GenPlace.TryPlaceThing(t,pool.InteractionCell,map,ThingPlaceMode.Near);foreach(var x in map.listerThings.ThingsOfDef(t.def).Where(x=>x.Position.DistanceTo(pool.Position)<=12))x.SetForbidden(false,false);}
  void EnterPool(Building_RemoldingPool pool,Hediff_Construct h,ConstructDesign plan){
   h.pawn.drafter.Drafted=false;h.pawn.jobs.StopAll();Check(pool.Order(h,plan,out var error),"native remolding entry order accepted: "+error);
   for(int i=0;i<3000 && pool.Occupant==null;i++)Step(1);
   Check(pool.Occupant==h.pawn && !h.pawn.Spawned,"native entry job moves exact pawn into remolding pool");
  }
  static Dictionary<SkillDef,Tuple<int,float,float>> SkillSnapshot(Pawn p)=>p.skills.skills.ToDictionary(s=>s.def,s=>Tuple.Create(s.levelInt,s.xpSinceLastLevel,s.xpSinceMidnight));
  static bool SameExperience(Pawn p,Dictionary<SkillDef,Tuple<int,float,float>> state)=>p.skills.skills.All(s=>state[s.def].Item1==s.levelInt && state[s.def].Item2==s.xpSinceLastLevel && state[s.def].Item3==s.xpSinceMidnight);
  void CheckRemolding(){
   var pool=NewPool();var old=new ConstructDesign{meat=200,steel=100,plasteel=60,specialty="眼睛"};
   var pawn=Constructs.Create("Scholar",worker,pool.InteractionCell+IntVec3.East*2,map,"眼睛",old);var h=Necro.Construct(pawn);
   pawn.Name=new NameSingle("重塑保留身份测试");pawn.relations.AddDirectRelation(PawnRelationDefOf.Parent,worker);Check(pawn.relations.DirectRelations.Any(r=>r.def==PawnRelationDefOf.Parent && r.otherPawn==worker),"remolding relationship fixture is established");
   pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed("BionicArm"),pawn.RaceProps.body.AllParts.First(p=>p.def.defName=="Arm"));pawn.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Painstopper"),pawn.health.hediffSet.GetBrain());
   var injury=(Hediff_Injury)pawn.health.AddHediff(HediffDefOf.Cut,pawn.RaceProps.body.corePart);injury.Severity=4;
   var plan=Remolding.Current(h);plan.meat=400;plan.steel=160;plan.plasteel=90;plan.specialty="心脏";plan.passions[SkillDefOf.Medicine]=Passion.Minor;plan.passions[SkillDefOf.Crafting]=Passion.None;
   var hidden=Remolding.Supplies(pool);foreach(var t in hidden)t.SetForbidden(true,false);
   Check(!pool.Order(h,plan,out var reason) && pool.queued==null && h.design.meat==200,"missing remolding materials do not enqueue or change pawn");
   PoolStock(pool,"Meat_Human",1200);PoolStock(pool,"Steel",300);PoolStock(pool,"Plasteel",200);
   var supplies=Remolding.Supplies(pool);int meat=supplies.Where(ConstructSeed.RawMeat).Sum(t=>t.stackCount),steel=supplies.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount),plast=supplies.Where(t=>t.def==ThingDefOf.Plasteel).Sum(t=>t.stackCount);
   var overCap=plan.Copy();foreach(var sk in new[]{SkillDefOf.Intellectual,SkillDefOf.Crafting,SkillDefOf.Construction,SkillDefOf.Medicine})overCap.passions[sk]=Passion.Major;Check(!pool.Order(h,overCap,out var capReason) && capReason==ConstructDesign.PassionLimitReason && pool.queued==null,"four major passions block remolding before entry or payment");
   var cost=Remolding.Cost(h,plan);Check(cost.meat==250 && cost.steel==60 && cost.plasteel==30,"remolding charges fixed fee plus positive material differences");
   int used=Necro.Used(worker),control=h.Cost;float upkeep=h.Upkeep;string id=pawn.ThingID;
   EnterPool(pool,h,plan);var skills=SkillSnapshot(pawn);float ratio=injury.Severity/PartHealth.Maximum(injury.Part,pawn);
   var rest=supplies.Where(t=>!t.Destroyed && t.Spawned).ToList();Check(rest.Where(ConstructSeed.RawMeat).Sum(t=>t.stackCount)==meat-250 && rest.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount)==steel-60 && rest.Where(t=>t.def==ThingDefOf.Plasteel).Sum(t=>t.stackCount)==plast-30,"pool entry consumes exact material delta once");
   Check(Necro.Used(worker)==used && h.Cost==control && h.Upkeep==upkeep,"contained pawn retains control allocation and upkeep");
   worker.psychicEntropy.OffsetPsyfocusDirectly(-1);int stopped=pool.progress;pool.Process(250);Check(pool.progress==stopped && pool.Occupant==pawn,"loss of owner focus pauses remolding without losing pawn");worker.psychicEntropy.OffsetPsyfocusDirectly(1);
   pool.Process(Remolding.Duration-pool.progress-1);Check(!pawn.Spawned && h.design.meat==200,"old configuration remains until full remolding duration");pool.Process(1);
   Check(pawn.Spawned && pawn.ThingID==id && pool.Occupant==null && h.design.meat==400 && h.specialty=="心脏","completion upgrades original pawn and changes specialty without replacement");
   Check(pawn.CanReach(pool,PathEndMode.InteractionCell,Danger.Deadly),"completed remolding releases pawn at reachable pool entrance, not across sealed walls");
   Check(SameExperience(pawn,skills),"changing specialty and plasteel preserves all levels and XP without awarding initial levels");
   Check(pawn.skills.GetSkill(SkillDefOf.Medicine).passion==Passion.Minor && pawn.skills.GetSkill(SkillDefOf.Crafting).passion==Passion.None,"remolding applies chosen passions");
   Check(Near(injury.Severity/PartHealth.Maximum(injury.Part,pawn),ratio),"health upgrade preserves existing wound proportion");
   Check(pawn.LabelShort.Contains("重塑保留身份") && pawn.relations.DirectRelations.Any(r=>r.def==PawnRelationDefOf.Parent && r.otherPawn==worker) && pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("BionicArm")) && pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("Painstopper")),"name, relationships, bionic arm and brain implant remain intact");
   var downgrade=Remolding.Current(h);downgrade.meat=0;downgrade.steel=0;downgrade.plasteel=0;
   cost=Remolding.Cost(h,downgrade);Check(cost.meat==50 && cost.steel==0 && cost.plasteel==0,"decreased material investment gives no refunds and retains reshaping fee");EnterPool(pool,h,downgrade);skills=SkillSnapshot(pawn);ratio=injury.Severity/PartHealth.Maximum(injury.Part,pawn);pool.Process(Remolding.Duration);
   Check(h.design.meat==0 && Near(injury.Severity/PartHealth.Maximum(injury.Part,pawn),ratio) && !pawn.Dead && SameExperience(pawn,skills),"downgrade preserves wound ratio and skill experience without killing or healing pawn");
   plan=Remolding.Current(h);plan.steel=50;EnterPool(pool,h,plan);pool.Process(500);pool.Release(false);Check(pawn.Spawned && h.design.steel==0 && pool.Occupant==null,"canceling occupied remolding pool releases same pawn with original configuration");
   var golem=Necro.LivingConstructs(worker).First(p=>Necro.Construct(p).kind=="Golem");var gh=Necro.Construct(golem);var golemPlan=Remolding.Current(gh);golemPlan.steel=20;gh.flesh=300;EnterPool(pool,gh,golemPlan);float flesh=gh.flesh;pool.Process(2500);Check(gh.flesh<flesh && gh.Cost==5 && gh.Upkeep==1,"golem flesh and control maintenance continue inside pool");pool.Release(false);
   EnterPool(pool,h,plan);pool.Destroy();Check(pawn.Spawned && !pawn.Dead && h.design.steel==0,"destroying occupied pool safely releases pawn without applying unfinished plan");
   pool=NewPool();PoolStock(pool,"Meat_Human",300);PoolStock(pool,"Steel",100);PoolStock(pool,"Plasteel",100);plan=Remolding.Current(h);plan.meat=100;plan.plasteel=30;plan.specialty="手臂";plan.passions[SkillDefOf.Construction]=Passion.Major;h.remoldDraft=plan.Copy();
   EnterPool(pool,h,plan);pool.Process(10000);remoldPawnId=pawn.ThingID;remoldPoolId=pool.ThingID;remoldProgress=pool.progress;remoldName=pawn.Name.ToStringFull;remoldSkills=SkillSnapshot(pawn);remoldInjuryRatio=injury.Severity/PartHealth.Maximum(injury.Part,pawn);
   foreach(var t in hidden)if(!t.Destroyed)t.SetForbidden(false,false);
   feedbackSaved=Necro.Construct(Necro.Registry.constructs.Single(p=>p.ThingID==feedbackScholarId)).knowledgeReturned;
  }
  void CheckRemoldingAfterLoad(){
   var pool=map.listerThings.ThingsOfDef(Necro.Def<ThingDef>("RemoldingPool")).Cast<Building_RemoldingPool>().Single(p=>p.ThingID==remoldPoolId);var pawn=pool.Occupant;var h=Necro.Construct(pawn);
   Check(pawn?.ThingID==remoldPawnId && pool.progress==remoldProgress && pool.pending.specialty=="手臂" && h.remoldDraft!=null,"save/load retains exact occupied pawn, progress, target design and draft");
   h.master.psychicEntropy.OffsetPsyfocusDirectly(1);pool.Process(Remolding.Duration-pool.progress);
   Check(pawn.Spawned && pawn.Name.ToStringFull==remoldName && h.design.meat==100 && h.specialty=="手臂" && h.remoldDraft==null,"saved remolding completes on same named pawn and clears finished draft");
   Check(SameExperience(pawn,remoldSkills) && pawn.skills.GetSkill(SkillDefOf.Construction).passion==Passion.Major,"save/load completion preserves every skill XP and applies requested passion");
   var injury=pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().First(i=>i.Part==pawn.RaceProps.body.corePart);float loadedRatio=injury.Severity/PartHealth.Maximum(injury.Part,pawn);bool hasArm=pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("BionicArm"));bool hasParent=pawn.relations.DirectRelations.Any(r=>r.def==PawnRelationDefOf.Parent && r.otherPawn==h.master);Check(Near(loadedRatio,remoldInjuryRatio) && hasArm && hasParent,"wound proportions, implants and relationships survive mid-remolding save/load; ratio="+loadedRatio+" expected="+remoldInjuryRatio+" arm="+hasArm+" parent="+hasParent+" torso="+string.Join(",",pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>i.Part==pawn.RaceProps.body.corePart).Select(i=>i.def.defName+":"+i.Severity.ToString("0.##"))));
   pool.Process(Remolding.Duration);Check(map.mapPawns.AllPawnsSpawned.Count(p=>p.ThingID==remoldPawnId)==1,"finished pool cannot duplicate its occupant");
  }
 }
}

