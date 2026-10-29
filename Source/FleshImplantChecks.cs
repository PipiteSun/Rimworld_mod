using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 public partial class LichChecks {
  static string implantPawnId;static IntVec3 implantRoomCenter,implantOutside;
  void InstallFlesh(Pawn pawn,string key){
   var recipe=Necro.Def<RecipeDef>("Install"+key);var part=recipe.Worker.GetPartsToApplyOn(pawn,recipe).First();
   Check(recipe.AvailableOnNow(pawn,part),key+" native surgery is available");
   var supplies=new List<Thing>{ThingMaker.MakeThing(Necro.Def<ThingDef>(key)),ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial)};
   var bill=new Bill_Medical(recipe,supplies);pawn.health.surgeryBills.AddBill(bill);
   var probe=new Harmony("local.lich.implanttest");var method=AccessTools.Method(typeof(Recipe_Surgery),"CheckSurgeryFail");surgerySubject=pawn;
   try{probe.Patch(method,prefix:new HarmonyMethod(typeof(LichChecks),nameof(DeterministicSurgery)));recipe.Worker.ApplyOnPawn(pawn,part,worker,supplies,bill);}
   finally{surgerySubject=null;probe.Unpatch(method,HarmonyPatchType.Prefix,probe.Id);pawn.health.surgeryBills.Delete(bill);foreach(var item in supplies)if(!item.Destroyed)item.Destroy();}
   Check(FleshImplants.Has(pawn,key),key+" installed through native surgery worker");
   var anesthetic=pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic);if(anesthetic!=null)pawn.health.RemoveHediff(anesthetic);
  }
  void CheckFleshImplants(){
   foreach(var key in new[]{"BlindSight","WitheringHand","DeadPersonality","ShadowLeg"}){
    var recipe=Necro.Def<RecipeDef>("Craft"+key);
    var ingredients=recipe.ingredients.Select(i=>Tuple.Create(i.filter.AllowedThingDefs.First(d=>!d.IsWithinCategory(ThingCategoryDefOf.MeatRaw) || d==ThingDefOf.Meat_Human),(int)i.GetBaseCount())).ToList();
    foreach(var i in ingredients)Spawn(i.Item1.defName,i.Item2);
    Execute("Craft"+key);
    Check(map.listerThings.ThingsOfDef(Necro.Def<ThingDef>(key)).Count>0,key+" ordinary bill creates implant item");
    Check(Necro.Icon("Implants/"+key)!=BaseContent.BadTex,key+" sprite loads");
    foreach(var kind in new[]{"Skeleton","Golem","Scholar"})Check(Necro.Def<ThingDef>(kind).AllRecipes.Contains(Necro.Def<RecipeDef>("Install"+key)),kind+" exposes "+key+" surgery");
   }
   var p=Person();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(altar.Position,map,6),map);p.workSettings.EnableAndInitialize();
   foreach(var trait in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(trait);
   var skill=p.skills.GetSkill(SkillDefOf.Crafting);skill.Level=15;skill.xpSinceLastLevel=1000;skill.xpSinceMidnight=0;float full=skill.LearnRateFactor(false);skill.xpSinceMidnight=10000;
   Check(skill.LearnRateFactor(false)<full,"ordinary pawn has daily learning slowdown");float oldXP=skill.xpSinceLastLevel;skill.Interval();Check(skill.xpSinceLastLevel<oldXP,"ordinary pawn naturally forgets skills");
   float speed=p.GetStatValue(StatDefOf.MoveSpeed,true,-1);
   foreach(var key in new[]{"BlindSight","WitheringHand","DeadPersonality","ShadowLeg"})InstallFlesh(p,key);
   Check(Near(skill.LearnRateFactor(false),full),"dead personality removes saturation but preserves normal learning multiplier");
   oldXP=skill.xpSinceLastLevel;skill.Interval();Check(Near(skill.xpSinceLastLevel,oldXP),"dead personality prevents natural skill decay");skill.Learn(-10,false,true);Check(skill.xpSinceLastLevel<oldXP,"dead personality does not cancel explicit negative XP");
   Check(p.GetStatValue(StatDefOf.MoveSpeed,true,-1)>speed,"shadow leg increases actual movement speed");
   var ability=p.abilities.GetAbility(Necro.Def<AbilityDef>("IceRay"));Check(ability!=null,"eye grants ice ray without a psylink");
   var enemy=Person();enemy.apparel.DestroyAll();GenSpawn.Spawn(enemy,CellFinder.RandomClosewalkCellNear(p.Position,map,3),map);
   Check(ability.Activate(enemy,LocalTargetInfo.Invalid),"implant ice ray activates through native ability");
   Check(enemy.health.hediffSet.HasHediff(HediffDefOf.Hypothermia) && ability.OnCooldown,"ice ray applies cold and starts cooldown");Check(!ability.Activate(enemy,LocalTargetInfo.Invalid),"ice ray cooldown prevents repeat casts");
   var cold=enemy.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia);float coldBefore=cold.Severity;
   var verb=p.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().First();
   var hit=(DamageWorker.DamageResult)AccessTools.Method(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget").Invoke(verb,new object[]{new LocalTargetInfo(enemy)});
   Check(hit.totalDamageDealt>0 && cold.Severity>coldBefore,"native melee hit with withering arm adds hypothermia");
   coldBefore=cold.Severity;enemy.TakeDamage(new DamageInfo(DamageDefOf.Bullet,1,0,instigator:p));Check(Near(cold.Severity,coldBefore),"ranged damage cannot trigger withering arm");enemy.Destroy();
   InstallFlesh(p,"BlindSight");var eyes=p.health.hediffSet.hediffs.Where(h=>h.def==Necro.Def<HediffDef>("BlindSight")).ToList();
   p.health.RemoveHediff(eyes[0]);Check(p.abilities.GetAbility(Necro.Def<AbilityDef>("IceRay"))!=null,"removing one of two eyes retains single ability");
   p.health.RemoveHediff(eyes[1]);Check(p.abilities.GetAbility(Necro.Def<AbilityDef>("IceRay"))==null,"removing final eye removes granted ability");InstallFlesh(p,"BlindSight");
   CheckShadowRoom(p);CheckAppearance(p);implantPawnId=p.ThingID;
  }
  void CheckShadowRoom(Pawn p){
   var center=map.AllCells.First(c=>c.DistanceTo(altar.Position)>35 && c.DistanceTo(altar.Position)<150 && CellRect.CenteredOn(c,5).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map)==null));
   implantRoomCenter=center;implantOutside=center+IntVec3.West*4;
   var walls=new List<Thing>();foreach(var c in CellRect.CenteredOn(center,2).EdgeCells){var wall=ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.Steel);wall.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(wall,c,map);walls.Add(wall);}
   p.Position=implantOutside;p.Notify_Teleported();p.drafter.Drafted=true;p.jobs.StopAll();
   Check(!worker.CanReach(center,PathEndMode.OnCell,Danger.Deadly),"sealed wall room is unreachable to ordinary pawn");
   Check(p.CanReach(center,PathEndMode.OnCell,Danger.Deadly),"shadow pawn can reach sealed room");
   var route=ShadowWalking.Route(p,map,p.Position,center,PathEndMode.OnCell,Danger.Deadly);Check(route.Any(c=>c.GetEdifice(map)?.def==ThingDefOf.Wall),"shadow route actually crosses wall cells");
   p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Goto,center),JobCondition.InterruptForced);
   for(int i=0;i<1600 && p.Position!=center;i++)Step(1);
   Check(p.Position==center,"ordinary goto job walks through wall without teleporting; pos="+p.Position+" goal="+center+" job="+p.CurJob+" down="+p.Downed+" moving="+p.pather.Moving+" next="+AccessTools.Field(typeof(Pawn_PathFollower),"nextCell").GetValue(p.pather));Check(walls.All(w=>!w.Destroyed && w.HitPoints==w.MaxHitPoints),"phasing leaves wall hit points intact");
   p.Position=implantOutside;p.Notify_Teleported();p.jobs.StopAll();p.drafter.Drafted=false;
   foreach(var w in DefDatabase<WorkTypeDef>.AllDefsListForReading)p.workSettings.SetPriority(w,0);p.workSettings.SetPriority(WorkTypeDefOf.Cleaning,1);for(int i=0;i<24;i++)p.timetable.SetAssignment(i,TimeAssignmentDefOf.Work);
   var dirt=(Filth)ThingMaker.MakeThing(ThingDefOf.Filth_Dirt);GenSpawn.Spawn(dirt,center,map);map.areaManager.Home[center]=true;
   var closest=GenClosest.ClosestThingReachable(p.Position,map,ThingRequest.ForDef(ThingDefOf.Filth_Dirt),PathEndMode.Touch,TraverseParms.For(p),999,t=>t==dirt);
   Check(closest==dirt,"ordinary work target search discovers job inside sealed room");   var hiddenSteel=map.listerThings.ThingsOfDef(ThingDefOf.Steel).Where(t=>!t.IsForbidden(p)).ToList();foreach(var t in hiddenSteel)t.SetForbidden(true,false);
   var steel=ThingMaker.MakeThing(ThingDefOf.Steel);steel.stackCount=10;GenSpawn.Spawn(steel,center,map);steel.SetForbidden(false,false);
   var ingredient=new IngredientCount();ingredient.filter.SetAllow(ThingDefOf.Steel,true);ingredient.SetBaseCount(10);var chosen=new List<ThingCount>();
   Check(!WorkGiver_DoBill.TryFindBestFixedIngredients(new List<IngredientCount>{ingredient},worker,altar,chosen,999),"ordinary bill worker cannot use ingredients in sealed room");
   Check(WorkGiver_DoBill.TryFindBestFixedIngredients(new List<IngredientCount>{ingredient},p,altar,chosen,999) && chosen.Any(t=>t.Thing==steel),"shadow bill worker finds ingredients across walls");
   foreach(var t in hiddenSteel)t.SetForbidden(false,false);
   var giver=new WorkGiver_CleanFilth{def=DefDatabase<WorkGiverDef>.AllDefsListForReading.First(w=>w.giverClass==typeof(WorkGiver_CleanFilth))};var job=giver.JobOnThing(p,dirt,true);Check(job!=null,"native cleaning job available through wall");p.jobs.StartJob(job,JobCondition.InterruptForced);
   for(int i=0;i<1600 && !dirt.Destroyed;i++)Step(1);Check(dirt.Destroyed,"native cleaning work crosses wall and completes");p.jobs.StopAll();p.drafter.Drafted=true;
   var leg=p.health.hediffSet.hediffs.First(h=>h.def==Necro.Def<HediffDef>("ShadowLeg"));p.health.RemoveHediff(leg);Check(!p.CanReach(implantOutside,PathEndMode.OnCell,Danger.Deadly),"removing shadow leg restores normal wall blocking");InstallFlesh(p,"ShadowLeg");
  }
  void CheckFleshImplantsAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==implantPawnId);
   foreach(var key in new[]{"BlindSight","WitheringHand","DeadPersonality","ShadowLeg"})Check(FleshImplants.Has(p,key),key+" implant survives save/load");Check(RenderNodes(p).Count(n=>n is ImplantMarkNode)==4,"implant appearance nodes survive save/load");
   Check(p.abilities.GetAbility(Necro.Def<AbilityDef>("IceRay"))!=null && p.CanReach(implantOutside,PathEndMode.OnCell,Danger.Deadly),"eye ability and passive wall traversal survive save/load");
   var skill=p.skills.GetSkill(SkillDefOf.Crafting);skill.xpSinceMidnight=10000;float xp=skill.xpSinceLastLevel;skill.Interval();Check(!skill.LearningSaturatedToday && Near(xp,skill.xpSinceLastLevel),"memory effects survive save/load");   var wall=CellRect.CenteredOn(implantRoomCenter,2).EdgeCells.First(c=>c.GetEdifice(map)?.def==ThingDefOf.Wall);
   p.Position=wall;p.Notify_Teleported();var leg=p.health.hediffSet.hediffs.First(h=>h.def==Necro.Def<HediffDef>("ShadowLeg"));p.health.RemoveHediff(leg);
   Check(p.Position.Standable(map),"losing final shadow leg inside a wall safely moves pawn to open ground");InstallFlesh(p,"ShadowLeg");
   p.Kill(null);Check(ResurrectionUtility.TryResurrect(p,new ResurrectionParams{restoreMissingParts=true,removeDiedThoughts=true}),"implanted pawn resurrects through native utility");
   FleshImplants.SyncEye(p);Check(FleshImplants.Has(p,"DeadPersonality") && ShadowWalking.Active(p) && p.abilities.GetAbility(Necro.Def<AbilityDef>("IceRay"))!=null,"resurrection retains implants and their abilities");
  }
 }
}
