using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;
namespace LichPhylactery {
 // 1.31.0 事件检查（-Only Events2）：惊逃兽群、冬狼寒潮、龙过境、法师塔坠落；云游导师的检查在 VPE 桥接里（Test.ps1 -VPE）。
 public partial class LichChecks {
  readonly List<Thing> e2Made=new List<Thing>();
  static int e2FollowTick,e2CondBreath,e2CondPasses,e2WreckSalvage,e2WreckGuards;static string e2WreckId,e2WreckChoice;

  Pawn E2Pawn(string xeno=null){var p=xeno==null?Person():RacePawn(xeno);if(p.Spawned)p.DeSpawn();p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);e2Made.Add(p);return p;}
  Pawn E2Animal(string kind,IntVec3 at){var p=PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed(kind),null);GenSpawn.Spawn(p,at,map);e2Made.Add(p);return p;}
  float Injury(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity);
  // 一块离所有殖民者至少 minDist 格、半径 r 内都能站人且没东西的空地。
  IntVec3 E2Open(float r,float minDist){
   var colonists=map.mapPawns.AllPawnsSpawned.Where(p=>p.Faction==Faction.OfPlayer).Select(p=>p.Position).ToList();
   return map.AllCells.Where(c=>c.x>20 && c.z>20 && c.x<map.Size.x-20 && c.z<map.Size.z-20 && colonists.All(x=>x.DistanceTo(c)>=minDist)).InRandomOrder().First(c=>GenRadial.RadialCellsAround(c,r,true).All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && !x.Roofed(map) && x.GetFirstPawn(map)==null && x.GetFirstItem(map)==null && x.GetEdifice(map)==null));
  }

  void CheckEvents2(){
   var s=LichMod.Settings;
   var saved=(s.stampede,s.winterWolfColdSnap,s.dragonFlyover,s.towerCrash,s.wanderingMentor,s.dndEventFrequency,s.portalBreach,s.giantMigration);
   var before=new HashSet<Thing>(map.listerThings.AllThings);var lords=new HashSet<Lord>(map.lordManager.lords);var letters=new HashSet<Letter>(Find.LetterStack.LettersListForReading);
   worldBefore=new HashSet<Pawn>(Find.WorldPawns.AllPawnsAliveOrDead);
   var goodwill=Find.FactionManager.AllFactions.Where(f=>!f.IsPlayer && f.HasGoodwill).ToDictionary(f=>f,f=>f.PlayerGoodwill);
   var socials=map.mapPawns.FreeColonistsSpawned.ToDictionary(p=>p,p=>p.skills?.GetSkill(SkillDefOf.Social)?.Level??0);
   try{CheckEvents2Defs();CheckStampede();CheckColdSnap();CheckDragonFlyover();CheckTowerCrash();}
   finally{
    (s.stampede,s.winterWolfColdSnap,s.dragonFlyover,s.towerCrash,s.wanderingMentor,s.dndEventFrequency,s.portalBreach,s.giantMigration)=saved;
    Events2.TestRoll=null;
    foreach(var c in map.gameConditionManager.ActiveConditions.Where(c=>c.def==DragonFlyover.ConditionDef || c.def==GameConditionDefOf.ColdSnap).ToList()){if(c is GameCondition_DragonShadow d)d.landingRolled=true;c.End();}
    var st=Events2.State;if(st!=null){st.stampedes.Clear();st.followUps.Clear();st.coldSnaps.Clear();}
    foreach(var r in PortalEvent.Rifts(map).ToList())if(!r.Destroyed)r.Destroy();
    foreach(var l in map.lordManager.lords.Where(l=>!lords.Contains(l)).ToList())map.lordManager.RemoveLord(l);
    foreach(var t in e2Made)if(t!=null && !t.Destroyed){if(t is Pawn p){Necro.Registry?.constructs.Remove(p);if(p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();}if(!t.Destroyed)t.Destroy();}
    e2Made.Clear();
    foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p)).ToList()){Necro.Registry?.constructs.Remove(p);p.Destroy();}
    foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList())if(!t.Destroyed)t.Destroy();
    foreach(var l in Find.LetterStack.LettersListForReading.Where(l=>!letters.Contains(l)).ToList())Find.LetterStack.RemoveLetter(l);
    CleanFactionPawns();CleanFactionPawns();
    foreach(var kv in goodwill)if(kv.Key.PlayerGoodwill!=kv.Value)Factions.SetRelation(kv.Key,Faction.OfPlayer,kv.Value);
    foreach(var kv in socials)if(kv.Key.skills!=null)kv.Key.skills.GetSkill(SkillDefOf.Social).Level=kv.Value;
    ColonyCensus.Dirty();ExtinguishAll();
   }
  }

  void CheckEvents2Defs(){
   foreach(var (name,cat,refire) in new[]{("LP_Stampede","Misc",12),("LP_WinterWolfColdSnap","ThreatBig",30),("LP_DragonFlyover","Misc",40),("LP_TowerCrash","ThreatSmall",45)}){
    var d=DefDatabase<IncidentDef>.GetNamedSilentFail(name);
    Check(d!=null && !d.ConfigErrors().Any() && Chinese(d.label) && d.category.defName==cat && d.minRefireDays==refire && d.Worker is IncidentWorker_DND,"1.31.0 event loads: "+name+" ("+cat+", refire "+refire+" days)");
   }
   Check(Near(DefDatabase<IncidentDef>.GetNamed("LP_Stampede").baseChance,DefDatabase<IncidentDef>.GetNamed("HerdMigration").baseChance*.5f),"stampede: half as common as a vanilla herd migration");
   Check(DefDatabase<IncidentDef>.GetNamed("LP_DragonFlyover").earliestDay==30,"dragon flyover: not before year 1, quadrum 3 (day 30)");
   Check((DefDatabase<IncidentDef>.GetNamedSilentFail("LP_WanderingMentor")!=null)==ModsConfig.IsActive("vanillaexpanded.vpsycastse"),"wandering mentor event loads only with Vanilla Psycasts Expanded ("+ModsConfig.IsActive("vanillaexpanded.vpsycastse")+")");
   Check(DragonFlyover.ConditionDef?.conditionClass==typeof(GameCondition_DragonShadow) && Chinese(DragonFlyover.ConditionDef.label) && DragonFlyover.ShadowFleck!=null && DragonFlyover.CraterDef?.filth!=null,"dragon shadow condition, shadow fleck and force crater filth");
   var w=TowerCrash.WreckDef;
   Check(w!=null && w.size==new IntVec2(3,3) && w.GetStatValueAbstract(StatDefOf.MaxHitPoints)==1200 && Near(w.GetStatValueAbstract(StatDefOf.WorkToBuild),4000) && w.building.alwaysDeconstructible && !w.building.claimable && w.designationCategory==null && TowerCrash.IncomingDef?.skyfaller!=null,"tower wreck: 3x3, 1200 hp, 4000 work to deconstruct, falls as a skyfaller");
   Check(ContentFinder<Texture2D>.Get(w.graphicData.texPath,false)!=null && ContentFinder<Texture2D>.Get("Things/Mote/LP_DragonShadow",false)!=null,"1.31.0 textures load (tower wreck, dragon shadow)");
   foreach(var h in new[]{"LP_ColdSnapFrenzy","LP_WildMagicBloom","LP_WildMagicHue","LP_EventVisitor"})Check(Items.Hediff(h)!=null && Chinese(Items.Hediff(h).label),"1.31.0 hediff loads: "+h);
   foreach(var t in new[]{"LP_DragonShadowDread","LP_WildMagicBloomed","LP_WildMagicHiccup"})Check(DefDatabase<ThoughtDef>.GetNamedSilentFail(t)!=null,"1.31.0 thought loads: "+t);
   // 总滑条：倍率作用在出现权重上。
   var st=DefDatabase<IncidentDef>.GetNamed("LP_Stampede");var portal=DefDatabase<IncidentDef>.GetNamed("LP_PortalBreach");
   LichMod.Settings.dndEventFrequency=100;float a=st.Worker.ChanceFactorNow(map),pa=portal.Worker.ChanceFactorNow(map);
   LichMod.Settings.dndEventFrequency=200;float b=st.Worker.ChanceFactorNow(map),pb=portal.Worker.ChanceFactorNow(map);
   LichMod.Settings.dndEventFrequency=0;float z=st.Worker.ChanceFactorNow(map);
   LichMod.Settings.dndEventFrequency=100;
   Check(a>0 && Near(b,2*a) && z==0f && Near(pb,2*pa),"DND event frequency slider scales event weights (x"+(b/Math.Max(.0001f,a)).ToString("0.##")+"), 0% turns them off");
  }

  // ==================== 惊逃兽群 ====================
  void CheckStampede(){
   var def=DefDatabase<IncidentDef>.GetNamed("LP_Stampede");var parms=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc,map);
   Check(Stampede.Candidates(map).Count>0 && Traverse.Create(def.Worker).Method("CanFireNowSub",parms).GetValue<bool>(),"stampede can fire with "+Stampede.Candidates(map).Count+" candidate animals");
   LichMod.Settings.stampede=false;Check(!Traverse.Create(def.Worker).Method("CanFireNowSub",parms).GetValue<bool>(),"stampede setting turns the event off");LichMod.Settings.stampede=true;
   bool beastsWild=LichMod.Settings.dndBeastsWild;
   LichMod.Settings.dndBeastsWild=true;bool withBeasts=Stampede.Candidates(map).Any(Beasts.IsBeastKind) || !Beasts.AllKinds().Any(k=>map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race,0f));
   LichMod.Settings.dndBeastsWild=false;bool without=!Stampede.Candidates(map).Any(Beasts.IsBeastKind);LichMod.Settings.dndBeastsWild=beastsWild;
   Check(withBeasts && without,"DND beasts join stampedes only when wild spawning is on");
   var sizes=new List<int>();StampedeGroup g=null;
   for(int i=0;i<3;i++){g=Stampede.Start(map);Check(g!=null,"stampede group spawns");sizes.Add(g.pawns.Count);foreach(var p in g.pawns)e2Made.Add(p);}
   Check(sizes.All(n=>n>=Stampede.MinCount && n<=Stampede.MaxCount),"stampede size is 6-14 animals ("+string.Join(",",sizes)+")");
   Check(g.pawns.All(p=>p.Spawned && p.Faction==null && p.RaceProps.Animal && !p.InMentalState) && g.lord?.LordJob is LordJob_ExitMapNear && Events2.State.stampedes.Contains(g),"stampede animals are wild, not manhunting, and run for the far edge");
   // 撞人：推开 2 格、3～6 钝伤，每人每群一次。
   var a=g.pawns.First();
   var cell=GenAdj.CellsAdjacent8Way(a).FirstOrDefault(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null);
   Check(cell.IsValid,"free cell next to a stampeding animal");
   var victim=E2Pawn();victim.Position=cell;victim.Notify_Teleported();float hurt=Injury(victim);
   var dir=Stampede.Direction(a,g);var expect=DNDPush.Destination(map,cell,dir,Stampede.PushCells);
   int hits=Stampede.Collide(g);float dmg=Injury(victim)-hurt;
   Check(hits>=1 && g.hit.Contains(victim) && victim.Position==expect && dmg>0f && g.lastDamage>=Stampede.DamageMin && g.lastDamage<=Stampede.DamageMax,"an animal running into a person knocks them back (DNDPush) for 3-6 blunt damage ("+g.lastDamage.ToString("0.#")+" dealt, injury "+dmg.ToString("0.#")+", "+cell+"→"+victim.Position+")");
   victim.Position=cell;victim.Notify_Teleported();float again=Injury(victim);Stampede.Collide(g);
   Check(Near(Injury(victim),again),"each person is knocked back by a stampede only once");
   Check(g.pawns.All(p=>!p.InMentalState),"stampede animals stay calm after collisions");
   // 后续：30% 没有，70% 排一个开着的事件。
   Events2.TestRoll=.29f;Check(Stampede.RollFollowUp(map)==null,"30%: no follow-up");
   Events2.TestRoll=.31f;var f=Stampede.RollFollowUp(map);
   Check(f!=null && Stampede.EnabledFollowUps(map).Contains(f.key) && f.tick>=EvNow+GenDate.TicksPerDay-1 && f.tick<=EvNow+3*GenDate.TicksPerDay+1 && Events2.State.followUps.Contains(f),"70%: a follow-up ("+f?.key+") is queued 1-3 days later");
   Events2.State.followUps.Clear();
   LichMod.Settings.dragonFlyover=false;LichMod.Settings.giantMigration=false;LichMod.Settings.portalBreach=false;
   var only=Stampede.EnabledFollowUps(map);bool thay=Stampede.FollowUpEnabled("thay",map);
   Check(!only.Contains("dragon") && !only.Contains("migration") && !only.Contains("portal") && only.Contains("thay")==thay,"follow-ups only pick events that are switched on ("+string.Join(",",only)+")");
   for(int i=0;i<20;i++){var x=Stampede.RollFollowUp(map);Check(x==null?only.Count==0:only.Contains(x.key),"follow-up roll respects the settings");}
   Events2.State.followUps.Clear();
   // 到点时原定事件已关：从开着的里面重挑。
   LichMod.Settings.dragonFlyover=true;
   if(!thay){var fu=new StampedeFollowUp{key="portal",map=map,tick=EvNow};Events2.TestRoll=.99f;Check(Stampede.Fire(fu) && fu.fired=="dragon" && DragonFlyover.Active(map),"a follow-up whose event was switched off is replaced by one that is on");EndShadow();}
   LichMod.Settings.giantMigration=true;LichMod.Settings.portalBreach=true;
   // 塞伊袭击：≥3500 点保证有炎魔。
   if(thay && Fiends.BalorKind!=null){
    var lord=Stampede.ThayRaid(map,4000f);
    Check(lord!=null && lord.faction==Factions.Hostile && lord.ownedPawns.Count(Fiends.IsBalor)==1,"follow-up Thayan raid at 4000 points comes with a balor ("+lord?.ownedPawns.Count+" raiders)");
    foreach(var p in lord.ownedPawns.ToList()){Necro.Registry?.constructs.Remove(p);p.Destroy();}
    if(map.lordManager.lords.Contains(lord))map.lordManager.RemoveLord(lord);
   }
   Events2.TestRoll=null;
   foreach(var p in Events2.State.stampedes.SelectMany(x=>x.pawns).ToList())if(!p.Destroyed)p.Destroy();
   Stampede.Tick(Events2.State);Check(Events2.State.stampedes.Count==0,"finished stampedes are dropped from the save state");
  }
  void EndShadow(){var c=DragonFlyover.Condition(map);if(c!=null){c.landingRolled=true;c.End();}}

  // ==================== 冬狼寒潮 ====================
  void CheckColdSnap(){
   Check(ColdSnap.ColdEnough(5f,"TemperateForest") && !ColdSnap.ColdEnough(25f,"TemperateForest") && ColdSnap.ColdEnough(25f,"Tundra") && ColdSnap.ColdEnough(25f,"BorealForest") && !ColdSnap.ColdEnough(30f,"Desert"),"cold snap gate: below 10C, or a boreal/tundra/ice biome");
   var def=DefDatabase<IncidentDef>.GetNamed("LP_WinterWolfColdSnap");var parms=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig,map);
   Check(Traverse.Create(def.Worker).Method("CanFireNowSub",parms).GetValue<bool>()==ColdSnap.ColdEnough(map),"winter wolf cold snap fires only when cold enough here ("+map.mapTemperature.OutdoorTemp.ToString("0")+"C, "+map.Biome.defName+")");
   LichMod.Settings.winterWolfColdSnap=false;Check(!ColdSnap.CanFire(map),"cold snap setting turns the event off");LichMod.Settings.winterWolfColdSnap=true;
   float cp=ColdSnap.Wolf.combatPower;
   Check(ColdSnap.WolfCount(10000f,6)==6 && ColdSnap.WolfCount(cp*2.5f,8)==2 && ColdSnap.WolfCount(10f,5)==1,"wolf count is trimmed by raid points (combat power "+cp+")");
   var pack=ColdSnap.Start(map,cp*5f,5);foreach(var w in pack.wolves)e2Made.Add(w);
   var cond=map.gameConditionManager.GetActiveCondition(GameConditionDefOf.ColdSnap);
   Check(cond!=null && cond.TicksLeft>=ColdSnap.Days*GenDate.TicksPerDay-10,"cold snap condition registered for 5 days ("+(cond?.TicksLeft??0)/GenDate.TicksPerDay+")");
   Check(pack.wolves.Count==5 && pack.wolves.All(w=>w.kindDef==ColdSnap.Wolf && w.MentalStateDef==MentalStateDefOf.ManhunterPermanent && Items.Has(w,ColdSnap.Frenzy)),"5 manhunting winter wolves with cold snap frenzy");
   var wolf=pack.wolves[0];float fast=wolf.GetStatValue(StatDefOf.MoveSpeed);Items.Remove(wolf,ColdSnap.Frenzy);float slow=wolf.GetStatValue(StatDefOf.MoveSpeed);Items.Give(wolf,ColdSnap.Frenzy);
   Check(Near(fast/slow,1.2f),"frenzy: +20% move speed ("+(fast/slow).ToString("0.##")+")");
   var target=E2Pawn();var bite=wolf.def.tools.First(Beasts.IsBite);
   Beasts.Comp(wolf).Notify_MeleeHit(target,bite);int frenzied=Items.TicksLeft(target,"LP_FrostSlow");Items.Remove(target,"LP_FrostSlow");
   var plain=pack.wolves[1];Items.Remove(plain,ColdSnap.Frenzy);Beasts.Comp(plain).Notify_MeleeHit(target,bite);int normal=Items.TicksLeft(target,"LP_FrostSlow");Items.Give(plain,ColdSnap.Frenzy);
   Check(frenzied==2*Beasts.FrostSlowTicks && normal==Beasts.FrostSlowTicks,"frenzy: frost bite slow lasts twice as long ("+frenzied+" vs "+normal+")");
   Check(Events2.State.coldSnaps.Contains(pack),"cold snap pack is tracked");
   ColdSnap.Tick(Events2.State);Check(Events2.State.coldSnaps.Contains(pack) && pack.wolves.All(w=>w.InMentalState),"wolves keep raging while the cold snap lasts");
   cond.End();ColdSnap.Tick(Events2.State);
   Check(!Events2.State.coldSnaps.Contains(pack) && pack.wolves.All(w=>!w.InMentalState && !Items.Has(w,ColdSnap.Frenzy) && w.Faction==null),"when the cold snap ends the wolves lose their frenzy and calm down as wild, tameable animals");
  }

  // ==================== 龙过境 ====================
  void CheckDragonFlyover(){
   var def=DefDatabase<IncidentDef>.GetNamed("LP_DragonFlyover");var parms=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc,map);
   Check(Traverse.Create(def.Worker).Method("CanFireNowSub",parms).GetValue<bool>(),"dragon flyover can fire");
   LichMod.Settings.dragonFlyover=false;Check(!DragonFlyover.CanFire(map),"dragon flyover setting turns the event off");LichMod.Settings.dragonFlyover=true;
   Events2.TestRoll=.19f;var c=DragonFlyover.Start(map);
   Check(DragonFlyover.Active(map) && c.Duration==GenDate.TicksPerDay && c.SkyTarget(map).HasValue && c.SkyTarget(map).Value.glow<1f,"dragon shadow: a one-day map condition with a darker sky");
   Check(c.breathTick>c.startTick && c.breathTick<c.startTick+c.Duration && c.nextShadowTick==c.startTick+GenDate.TicksPerHour,"20% roll (fixed 0.19): one breath scheduled during the flyover; first shadow after an hour");
   Check(!Traverse.Create(def.Worker).Method("CanFireNowSub",parms).GetValue<bool>(),"only one flyover at a time");
   // 心情。
   var human=map.mapPawns.FreeColonistsSpawned.First(x=>!Necro.Undead(x));
   var dread=DefDatabase<ThoughtDef>.GetNamed("LP_DragonShadowDread");
   Check(dread.Worker.CurrentState(human).Active && Near(dread.stages[0].baseMoodEffect,-3f),"colonists: -3 dragon's shadow");
   // 龙影：行进行 ±20 格内的野生动物惊逃。
   int row=map.Size.z/2;var nearCell=CellFinder.RandomClosewalkCellNear(new IntVec3(map.Size.x/2,0,row),map,4);var farCell=CellFinder.TryFindRandomCellNear(new IntVec3(map.Size.x/2,0,Math.Min(map.Size.z-5,row+40)),map,5,x=>x.Standable(map) && Math.Abs(x.z-row)>DragonFlyover.FleeRadius+2,out var fc)?fc:IntVec3.Invalid;
   var hare=E2Animal("Hare",nearCell);var far=farCell.IsValid?E2Animal("Hare",farCell):null;
   int fled=DragonFlyover.Pass(map,row,true);
   Check(fled>=1 && hare.MentalStateDef==MentalStateDefOf.PanicFlee && (far==null || !far.InMentalState),"dragon shadow pass: wild animals within 20 cells flee in panic ("+fled+"), others don't");
   // 吐息：建筑/物品受伤，人只被推开，留下弹坑。
   var center=E2Open(4f,8f);
   var wall=ThingMaker.MakeThing(ThingDefOf.Wall,DefDatabase<ThingDef>.GetNamed("BlocksGranite"));GenSpawn.Spawn(wall,center+IntVec3.East,map);e2Made.Add(wall);
   var steel=ThingMaker.MakeThing(ThingDefOf.WoodLog);steel.stackCount=20;GenSpawn.Spawn(steel,center+IntVec3.West,map);e2Made.Add(steel);
   var bystander=E2Pawn();bystander.Position=center+IntVec3.North;bystander.Notify_Teleported();float bh=Injury(bystander);
   int wallHp=wall.HitPoints,steelHp=steel.HitPoints;var r=DragonFlyover.Breathe(map,center);
   Check(wall.HitPoints<wallHp && steel.HitPoints<steelHp && r.hit>=2,"breath: buildings and items within 3 cells take force damage (wall "+wallHp+"→"+wall.HitPoints+", wood "+steelHp+"→"+steel.HitPoints+")");
   Check(Near(Injury(bystander),bh) && bystander.Position!=center+IntVec3.North && r.pushed>=1,"breath: people are pushed away, not hurt");
   Check(GenRadial.RadialCellsAround(center,6f,true).Where(x=>x.InBounds(map)).All(x=>x.GetTerrain(map).filthAcceptanceMask==FilthSourceFlags.None) || GenRadial.RadialCellsAround(center,6f,true).Where(x=>x.InBounds(map)).Any(x=>x.GetThingList(map).Any(t=>t.def==DragonFlyover.CraterDef)),"breath leaves a force crater (terrain "+center.GetTerrain(map).defName+", filthAcceptance "+center.GetTerrain(map).filthAcceptanceMask+", retry "+FilthMaker.TryMakeFilth(center,map,DragonFlyover.CraterDef,1)+", things "+string.Join(",",center.GetThingList(map).Select(t=>t.def.defName))+")");
   var bc=DragonFlyover.BreathCell(map);Check(bc.IsValid && map.mapPawns.FreeColonistsSpawned.All(p=>p.Position.DistanceTo(bc)>=DragonFlyover.BreathMinColonistDistance),"breath lands at least 6 cells from every colonist");
   c.landingRolled=true;c.End();Events2.TestRoll=.21f;c=DragonFlyover.Start(map);Check(c.breathTick<0,"20% roll (fixed 0.21): no breath");c.landingRolled=true;c.End();
   Events2.TestRoll=null;
  }

  // ==================== 法师塔坠落 ====================
  void CheckTowerCrash(){
   bool thay=TowerCrash.ThayPresent;
   var def=DefDatabase<IncidentDef>.GetNamed("LP_TowerCrash");var parms=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatSmall,map);
   Check(Traverse.Create(def.Worker).Method("CanFireNowSub",parms).GetValue<bool>()==thay,"tower crash needs the Thayan expedition ("+thay+")");
   LichMod.Settings.towerCrash=false;Check(!TowerCrash.CanFire(map),"tower crash setting turns the event off");LichMod.Settings.towerCrash=true;
   if(!thay){Log.Message("[LICH-TEST] skip tower crash (no Thayan expedition)");return;}
   // 坠落：照原版飞船残骸，落地后守卫与信件。
   Check(TowerCrash.TryFindSpot(map,out var cell),"tower crash site found");
   var colony=map.listerBuildings.allBuildingsColonist.Select(b=>b.Position).ToList();
   Check(colony.All(b=>b.DistanceTo(cell)>=TowerCrash.MinColonyDistance-.01f),"crash site is at least 20 cells from the colony");
   int letters=Find.LetterStack.LettersListForReading.Count;
   Check(def.Worker.TryExecute(parms),"tower crash executes");
   Building_TowerWreck w=null;for(int i=0;i<900 && (w==null || !w.setup);i++){Step(1);w=TowerCrash.Wrecks(map).FirstOrDefault();}
   Check(w!=null && w.setup && w.def.size==new IntVec2(3,3),"the tower wreck lands from a skyfaller and sets up");
   e2Made.Add(w);e2Made.AddRange(w.guards);
   var tl=Find.LetterStack.LettersListForReading.OfType<Letter_TowerCrash>().LastOrDefault();
   Check(tl!=null && tl.wreck==w && tl.Label.ToString()=="法师塔坠落","choice letter: tower crash");
   var to=tl.Choices.Select(OptText).ToList();Check(to.Any(o=>o.StartsWith("自己拆")) && to.Any(o=>o.StartsWith("等塞伊回收")) && to.Any(o=>o.StartsWith("通知同盟")),"three choices: deconstruct, wait for Thay, tell the alliance");
   Check(w.guards.Count>=TowerCrash.GuardsMin && w.guards.Count<=TowerCrash.GuardsMax && w.guards.All(g=>g.Spawned && g.Faction==Factions.Hostile && Necro.Construct(g)?.kind=="Skeleton" && Necro.Construct(g).HasControl && g.GetLord()?.LordJob is LordJob_DefendPoint),"3-5 red robe skeletons guard the wreck ("+w.guards.Count+")");
   // 野魔法：六种效果各一次。
   var p=E2Pawn();p.Position=CellFinder.RandomClosewalkCellNear(map.Center,map,3);p.Notify_Teleported();
   Check(TowerCrash.WildMagic(p,"invisible")=="invisible" && Items.TicksLeft(p,"LP_PotionInvisibility")==TowerCrash.InvisibleTicks,"wild magic: invisible for 30 seconds");
   Check(TowerCrash.WildMagic(p,"haste")=="haste" && Items.TicksLeft(p,"LP_SpeedBurst")==TowerCrash.HasteTicks,"wild magic: haste for 1 minute");
   var from=p.Position;var tp=TowerCrash.WildMagic(p,"teleport");Check(tp=="teleport" && p.Position!=from && p.Position.DistanceTo(from)<=TowerCrash.TeleportCells+.01f,"wild magic: teleported up to 5 cells ("+tp+" "+from+"→"+p.Position+")");
   Check(TowerCrash.WildMagic(p,"bloom")=="bloom" && Items.Has(p,"LP_WildMagicBloom") && p.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_WildMagicBloomed"))!=null,"wild magic: a flower on the head (+1 mood)");
   Check(TowerCrash.WildMagic(p,"hiccup")=="hiccup" && p.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_WildMagicHiccup"))!=null,"wild magic: hiccups (-1 mood)");
   var skin=p.story.skinColorOverride;Check(TowerCrash.WildMagic(p,"hue")=="hue" && p.story.skinColorOverride.HasValue && p.story.skinColorOverride!=skin,"wild magic: skin changes color");
   Items.Remove(p,"LP_WildMagicHue");Check(p.story.skinColorOverride==skin,"the color change wears off back to the old skin");
   var hare=E2Animal("Hare",CellFinder.RandomClosewalkCellNear(map.Center,map,3));Check(TowerCrash.WildMagic(hare,"hue")==null && TowerCrash.WildMagic(hare)!=null,"animals get wild magic too, but no color change");
   // 每 2～4 小时 25%：在范围内才掷。
   var near=E2Pawn();near.Position=CellFinder.RandomClosewalkCellNear(w.Position,map,6);near.Notify_Teleported();
   TowerCrash.WildMagicTick(w);Check(w.nextRoll.TryGetValue(near.thingIDNumber,out int next) && next>=EvNow+2*GenDate.TicksPerHour && next<=EvNow+4*GenDate.TicksPerHour,"a pawn entering the wild magic zone rolls every 2-4 hours");
   w.nextRoll[near.thingIDNumber]=EvNow;Events2.TestRoll=.3f;Check(TowerCrash.WildMagicTick(w)==0,"wild magic roll above 25% does nothing");
   w.nextRoll[near.thingIDNumber]=EvNow;Events2.TestRoll=.2f;Check(TowerCrash.WildMagicTick(w)>=1,"wild magic roll under 25% triggers an effect");
   Check(!w.nextRoll.ContainsKey(w.guards[0].thingIDNumber),"the skeleton guards are not affected by wild magic");
   Events2.TestRoll=null;
   // 产出范围。
   for(int i=0;i<10;i++){
    var full=TowerCrash.Loot(false);int scrolls=full.Count(t=>t.def.defName.StartsWith("LP_Scroll"));
    Check(scrolls>=2 && scrolls<=4 && full.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount)==150 && full.Where(t=>t.def==ThingDefOf.Plasteel).Sum(t=>t.stackCount)==40 && full.Count(t=>t.def.defName=="LP_AdventurersManual")==(Items.Thing("LP_AdventurersManual")!=null?1:0) && full.Count(t=>t.def.defName=="LP_IounStone" || TowerCrash.WondrousPool.Contains(t.def.defName))<=1,"deconstruct loot: 2-4 scrolls, manual (with VPE), maybe an ioun stone or wondrous item, 150 steel, 40 plasteel ("+scrolls+" scrolls)");
    var half=TowerCrash.Loot(true);int hs=half.Count(t=>t.def.defName.StartsWith("LP_Scroll"));
    Check(hs>=1 && hs<=2 && half.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount)==75 && half.Where(t=>t.def==ThingDefOf.Plasteel).Sum(t=>t.stackCount)==20 && !half.Any(t=>t.def.defName=="LP_AdventurersManual" || t.def.defName=="LP_IounStone" || TowerCrash.WondrousPool.Contains(t.def.defName)),"alliance half: 1-2 scrolls, 75 steel, 20 plasteel, no manual or wondrous items");
    foreach(var t in full.Concat(half))t.Destroy();
   }
   // 选择：只能选一次；等塞伊 5 天。
   Check(TowerCrash.Choose(w,"thay") && w.salvageTick==EvNow+TowerCrash.ThayDays*GenDate.TicksPerDay && !TowerCrash.Choose(w,"self"),"choosing to wait for Thay schedules their team in 5 days; only one choice");
   // 自己拆：产出，守卫随塔崩解。
   int steel=Count(map,ThingDefOf.Steel),plas=Count(map,ThingDefOf.Plasteel);var guards=w.guards.ToList();var master=w.master;
   w.Destroy(DestroyMode.Deconstruct);
   Check(Count(map,ThingDefOf.Steel)-steel==150 && Count(map,ThingDefOf.Plasteel)-plas==40 && guards.All(g=>g.Destroyed) && master.Destroyed,"deconstructing gives the loot; the guards crumble and their absent master is removed");
   // 塞伊回收：到达后搬走；打退则残骸留下。
   var w2=PlaceWreck();Check(TowerCrash.Choose(w2,"thay"),"second wreck: wait for Thay");w2.salvageTick=EvNow;TowerCrash.ScheduleTick(w2);
   Check(w2.salvageSent && w2.salvageLord?.LordJob is LordJob_TowerSalvage && w2.salvageLord.faction==Factions.Hostile && w2.salvageLord.ownedPawns.Count>0,"Thay salvage team arrives ("+w2.salvageLord?.ownedPawns.Count+" pawns)");
   foreach(var x in w2.salvageLord.ownedPawns)e2Made.Add(x);
   Check(TowerCrash.Salvaged(w2,false,w2.salvageLord) && w2.Destroyed,"once they arrive the Thay team carries the wreck away");
   var w3=PlaceWreck();TowerCrash.Choose(w3,"thay");w3.salvageTick=EvNow;TowerCrash.ScheduleTick(w3);
   foreach(var x in w3.salvageLord.ownedPawns.ToList()){e2Made.Add(x);if(!x.Dead)x.Kill(null);}
   Check(!TowerCrash.Salvaged(w3,false,w3.salvageLord) && w3.Spawned,"beating the Thay team keeps the wreck");
   // 没选、5 天后守卫还在：塞伊自己来。
   var w4=PlaceWreck();w4.crashTick=EvNow-TowerCrash.ThayDays*GenDate.TicksPerDay-1;TowerCrash.ScheduleTick(w4);
   Check(w4.salvageSent && w4.autoThay && w4.salvageBy=="thay","unanswered with guards still standing after 5 days: Thay comes to collect");
   if(w4.salvageLord!=null)foreach(var x in w4.salvageLord.ownedPawns)e2Made.Add(x);
   var w5=PlaceWreck();foreach(var g in w5.guards.ToList())g.Kill(null);w5.crashTick=EvNow-TowerCrash.ThayDays*GenDate.TicksPerDay-1;TowerCrash.ScheduleTick(w5);
   Check(!w5.salvageSent,"guards cleared: Thay does not come on its own");
   // 通知同盟：要通讯台和好感 ≥20；3 天后来人拆走，留一半、好感 +10。
   var ally=Factions.Colony;
   if(ally!=null && !ally.HostileTo(Faction.OfPlayer)){
    Factions.SetRelation(ally,Faction.OfPlayer,10);var w6=PlaceWreck();
    Check(TowerCrash.AllyBlocked(map)!=null && !TowerCrash.Choose(w6,"ally"),"alliance option needs goodwill 20 ("+TowerCrash.AllyBlocked(map)+")");
    Factions.SetRelation(ally,Faction.OfPlayer,30);
    var console=map.listerBuildings.allBuildingsColonist.FirstOrDefault(b=>b.def==ThingDefOf.CommsConsole);
    if(console==null){var c=ThingMaker.MakeThing(ThingDefOf.CommsConsole);c.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(c,E2Open(2f,3f),map,WipeMode.Vanish);e2Made.Add(c);console=(Building)c;}
    var power=console.TryGetComp<CompPowerTrader>();bool was=power?.PowerOn??true;
    if(power!=null){power.PowerOn=false;Check(TowerCrash.AllyBlocked(map)?.Contains("通讯台")==true,"alliance option needs a powered comms console");power.PowerOn=true;}
    Check(TowerCrash.AllyBlocked(map)==null && TowerCrash.Choose(w6,"ally") && w6.salvageTick==EvNow+TowerCrash.AllyDays*GenDate.TicksPerDay,"telling the alliance schedules their team in 3 days");
    w6.salvageTick=EvNow;TowerCrash.ScheduleTick(w6);
    Check(w6.salvageLord?.faction==ally && w6.salvageLord.LordJob is LordJob_TowerSalvage j && j.ally,"alliance dismantling team arrives");
    foreach(var x in w6.salvageLord.ownedPawns)e2Made.Add(x);
    steel=Count(map,ThingDefOf.Steel);int gw=ally.PlayerGoodwill;
    Check(TowerCrash.Salvaged(w6,true,w6.salvageLord) && w6.Destroyed && Count(map,ThingDefOf.Steel)-steel==75 && ally.PlayerGoodwill==gw+TowerCrash.AllyGoodwill,"the alliance takes the wreck, leaves half the loot (75 steel) and goodwill +10");
    if(power!=null)power.PowerOn=was;
   }
  }
  Building_TowerWreck PlaceWreck(){
   TowerCrash.TryFindSpot(map,out var cell);
   var w=(Building_TowerWreck)ThingMaker.MakeThing(TowerCrash.WreckDef);GenSpawn.Spawn(w,cell,map);w.Setup(false);
   e2Made.Add(w);e2Made.AddRange(w.guards);return w;
  }

  // ==================== 存读档 ====================
  void PrepareEvents2Save(){
   var st=Events2.State;
   e2FollowTick=EvNow+12345;st.followUps.Add(new StampedeFollowUp{key="portal",map=map,tick=e2FollowTick});
   Events2.TestRoll=0f;var c=DragonFlyover.Start(map);Events2.TestRoll=null;
   c.breathTick=EvNow+GenDate.TicksPerDay/2;c.passes=2;c.nextShadowTick=EvNow+GenDate.TicksPerDay/3;e2CondBreath=c.breathTick;e2CondPasses=2;
   if(TowerCrash.ThayPresent && TowerCrash.TryFindSpot(map,out var cell)){
    var w=(Building_TowerWreck)ThingMaker.MakeThing(TowerCrash.WreckDef);GenSpawn.Spawn(w,cell,map);w.Setup(false);
    TowerCrash.Choose(w,"thay");w.salvageTick=EvNow+654321;e2WreckSalvage=w.salvageTick;e2WreckChoice=w.choice;e2WreckGuards=w.guards.Count;e2WreckId=w.ThingID;
   }
  }
  void CheckEvents2AfterLoad(){
   map=Find.CurrentMap;var st=Events2.State;
   Check(st.followUps.Any(f=>f.key=="portal" && f.tick==e2FollowTick && f.map==map),"stampede follow-up survives save/load");
   st.followUps.Clear();
   var c=DragonFlyover.Condition(map);
   Check(c!=null && c.breathTick==e2CondBreath && c.passes==e2CondPasses && !c.breathed,"dragon shadow condition and its breath timer survive save/load");
   if(c!=null){c.landingRolled=true;c.End();}
   if(e2WreckId!=null){
    var w=TowerCrash.Wrecks(map).FirstOrDefault(x=>x.ThingID==e2WreckId);
    Check(w!=null && w.choice==e2WreckChoice && w.salvageTick==e2WreckSalvage && w.guards.Count==e2WreckGuards && w.master!=null && w.guards.All(g=>Necro.Construct(g)?.HasControl==true),"tower wreck choice, timer, guards and their absent master survive save/load");
    w?.Destroy();
   }
  }
 }
}
