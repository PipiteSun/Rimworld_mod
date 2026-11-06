using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;
namespace LichPhylactery {
 // 1.117.0 第三批事件检查（-Only Events3）：16 个事件的定义、贴图，以及每个事件的主要分支直接调用一遍。
 public partial class LichChecks {
  readonly List<Thing> e3Made=new List<Thing>();
  Pawn E3Pawn(){var p=Person();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);e3Made.Add(p);return p;}
  int E3Count(string def)=>map.listerThings.AllThings.Where(t=>t.def.defName==def).Sum(t=>t.stackCount);

  void CheckEvents3(){
   var s=LichMod.Settings;var st=Events3.State;
   Check(st!=null,"1.117.0 event state component exists");
   var before=new HashSet<Thing>(map.listerThings.AllThings);var lords=new HashSet<Lord>(map.lordManager.lords);var letters=new HashSet<Letter>(Find.LetterStack.LettersListForReading);
   var goodwill=Find.FactionManager.AllFactions.Where(f=>!f.IsPlayer && f.HasGoodwill).ToDictionary(f=>f,f=>f.PlayerGoodwill);
   var raids=Tribute.Events?.scheduledRaids.ToList();var worldObjects=new HashSet<WorldObject>(Find.WorldObjects.AllWorldObjects);
   int recs=st.recs.Count;worldBefore=new HashSet<Pawn>(Find.WorldPawns.AllPawnsAliveOrDead);
   try{
    CheckEvents3Defs();CheckE3Mimic();CheckE3Beggar();CheckE3Bards();CheckE3Silver();CheckE3Ghost();
    CheckE3Noble();CheckE3Ward();CheckE3Starfall();CheckE3Party();CheckE3Surge();CheckE3Thief();CheckE3Calling();
    CheckE3Tourney();CheckE3Rival();CheckE3Treant();CheckE3Tarrasque();CheckExtraRaids();
   }finally{
    Events2.TestRoll=null;
    st.recs.RemoveRange(recs,Math.Max(0,st.recs.Count-recs));
    foreach(var c in map.gameConditionManager.ActiveConditions.Where(c=>c.def==Starfall.Def || c.def==WeaveSurge.Def).ToList())c.End();
    foreach(var l in map.lordManager.lords.Where(l=>!lords.Contains(l)).ToList())map.lordManager.RemoveLord(l);
    foreach(var t in e3Made)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    e3Made.Clear();
    foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p) && p!=worker).ToList())p.Destroy();
    foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Mote) && t.def.category!=ThingCategory.Ethereal && !(t is Phylactery)).ToList())if(!t.Destroyed)t.Destroy();
    foreach(var l in Find.LetterStack.LettersListForReading.Where(l=>!letters.Contains(l)).ToList())Find.LetterStack.RemoveLetter(l);
    foreach(var w in Find.WorldObjects.AllWorldObjects.Where(w=>!worldObjects.Contains(w)).ToList())if(!w.Destroyed && !(w is MapParent mp && mp.HasMap))w.Destroy();
    if(raids!=null){Tribute.Events.scheduledRaids.Clear();Tribute.Events.scheduledRaids.AddRange(raids);}
    foreach(var kv in goodwill)if(kv.Key.PlayerGoodwill!=kv.Value)Factions.SetRelation(kv.Key,Faction.OfPlayer,kv.Value);
    CleanFactionPawns();ColonyCensus.Dirty();ExtinguishAll();
   }
  }

  void CheckEvents3Defs(){
   foreach(var (name,cat,refire) in new[]{("LP_E3_MimicChest","Misc",10),("LP_E3_Beggar","Misc",15),("LP_E3_Bards","Misc",15),("LP_E3_SilverGuest","Misc",20),("LP_E3_GhostWish","Misc",10),("LP_E3_Noble","Misc",20),("LP_E3_Ward","Misc",30),("LP_E3_Starfall","Misc",15),("LP_E3_FallenParty","Misc",15),("LP_E3_WeaveSurge","Misc",20),("LP_E3_Thief","ThreatSmall",20),("LP_E3_Tourney","Misc",30),("LP_E3_RivalLich","Misc",30),("LP_E3_Treant","Misc",20),("LP_E3_Tarrasque","ThreatBig",60)}){
    var d=DefDatabase<IncidentDef>.GetNamedSilentFail(name);
    Check(d!=null && !d.ConfigErrors().Any() && Chinese(d.label) && d.category.defName==cat && d.minRefireDays==refire && d.earliestDay==Events3.EarliestDay && d.Worker is IncidentWorker_E3,"1.117.0 event loads: "+name+" ("+cat+", refire "+refire+", from day 15)");
   }
   foreach(var t in new[]{"LP_MimicChest","LP_HeartTree","LP_SilverDragonScale","LP_MimicTooth","LP_StarHeart","LP_AdventureLog","LP_TourneyTrophy","LP_TarrasqueShell","LP_TarrasqueTooth","LP_Mimic","LP_Treant","LP_Tarrasque"}){var d=Items.Thing(t);Check(d!=null && !d.ConfigErrors().Any() && Chinese(d.label),"1.117.0 thing loads: "+t);}
   foreach(var t in new[]{"LP_MimicChest","LP_HeartTree","LP_SilverDragonScale","LP_MimicTooth","LP_StarHeart","LP_AdventureLog","LP_TourneyTrophy","LP_TarrasqueShell","LP_TarrasqueTooth"})Check(Items.Thing(t).graphic!=null && Items.Thing(t).graphic.MatSingle.mainTexture!=BaseContent.BadTex,"1.117.0 texture loads: "+t);
   foreach(var k in new[]{"LP_Mimic","LP_Treant","LP_AwakenedTree","LP_Tarrasque"}){var d=DefDatabase<PawnKindDef>.GetNamedSilentFail(k);Check(d!=null && ContentFinder<Texture2D>.Get(d.lifeStages[0].bodyGraphicData.texPath+"_south",false)!=null,"1.117.0 creature loads with texture: "+k);}
   foreach(var h in new[]{"LP_E3_DivineGlance","LP_E3_DivineDispleasure","LP_E3_SilverFriend","LP_E3_GiftedWard","LP_E3_Shadowed","LP_E3_Wooden","LP_E3_TrapWounds","LP_E3_TarrasqueHide"})Check(Items.Hediff(h)!=null && Chinese(Items.Hediff(h).label),"1.117.0 hediff loads: "+h);
   foreach(var t in new[]{"LP_E3_BardShow","LP_E3_BalladHero","LP_E3_GhostRest","LP_E3_Haunted","LP_E3_SoulBound","LP_E3_Starfall","LP_E3_Champion"})Check(DefDatabase<ThoughtDef>.GetNamedSilentFail(t)!=null,"1.117.0 thought loads: "+t);
   Check(Events3.WayfarersDef!=null && Events3.WayfarersDef.hidden && !Events3.WayfarersDef.permanentEnemy && Events3.WayfarersDef.requiredCountAtGameStart==0,"hidden wayfarers faction");
   Check(Events3.SiteDef!=null && Starfall.Def!=null && WeaveSurge.Def!=null && DefDatabase<LetterDef>.GetNamedSilentFail("LP_E3ChoiceLetter")?.letterClass==typeof(Letter_E3),"site part, conditions and choice letter load");
   foreach(var j in new[]{"LP_E3_OpenChest","LP_E3_IdentifyChest","LP_E3_Offer"})Check(DefDatabase<JobDef>.GetNamedSilentFail(j)?.driverClass==typeof(JobDriver_E3Interact),"job loads: "+j);
   Check(Items.Thing("LP_Tarrasque").tradeTags.NullOrEmpty() && Items.Thing("LP_StarHeart").tradeability==Tradeability.Sellable,"event creatures and items are not sold by traders");
  }

  void CheckE3Mimic(){
   var a=E3Pawn();a.skills.GetSkill(SkillDefOf.Intellectual).Level=10;
   var chest=Mimic.Start(map,false);Check(chest!=null && chest.Spawned && !chest.mimic,"mimic chest: a real chest spawns outside the colony");
   Check(Mimic.MenuFor(a,chest).Count()==2 && Mimic.CanIdentify(a),"mimic chest: right-click offers identify and open");
   Mimic.Identify(a,chest);Check(chest.identified && chest.GetInspectString().Contains("真正"),"mimic chest: identify reveals a real chest");
   int silver=E3Count("Silver");Mimic.Open(a,chest);
   Check(chest.Destroyed && E3Count("Silver")-silver>=300,"mimic chest: opening a real chest drops 300+ silver and a wondrous item");
   var m=Mimic.Start(map,true);var cell=m.Position;Mimic.Open(a,m);
   var mimic=map.mapPawns.AllPawnsSpawned.FirstOrDefault(p=>p.kindDef==Mimic.MimicKind);
   Check(mimic!=null && mimic.MentalStateDef==MentalStateDefOf.ManhunterPermanent && a.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(),"mimic: unidentified opening reveals a manhunter mimic that bites the opener");
   int teeth=E3Count("LP_MimicTooth");mimic.Kill(null);
   Check(E3Count("LP_MimicTooth")-teeth==3,"mimic: killing it drops 3 mimic teeth plus its hoard");
  }

  void CheckE3Beggar(){
   if(!Faith.Enabled){Check(true,"beggar: faith system off, skipped");return;}
   var saved=FaithUtility.Patron;var god=FaithUtility.Worshippable.FirstOrDefault(g=>g.Good)??FaithUtility.Worshippable.First();
   Building altar2=null;
   try{
    FaithUtility.SetPatron(god,true);
    if(!FaithUtility.Altars(map).Any()){altar2=(Building)ThingMaker.MakeThing(Faith.AltarDef,GenStuff.DefaultStuffFor(Faith.AltarDef));altar2.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(altar2,CellFinder.RandomClosewalkCellNear(map.Center,map,10,c=>c.Standable(map) && c.GetEdifice(map)==null),map);e3Made.Add(altar2);}
    Check(Beggar.CanFire(map),"beggar: can fire with a patron god and an altar");
    var r=Beggar.Start(map);Check(r!=null && r.a.Spawned && r.a.Faction?.def==Events3.WayfarersDef,"beggar: a wayfarer beggar arrives");
    var giver=E3Pawn();
    foreach(var hc in GenRadial.RadialCellsAround(giver.Position,4f,true))if(hc.InBounds(map))map.areaManager.Home[hc]=true;var meals=ThingMaker.MakeThing(ThingDefOf.MealSimple);meals.stackCount=5;GenPlace.TryPlaceThing(meals,giver.Position,map,ThingPlaceMode.Near);meals.SetForbidden(false,false);
    float favor=FaithUtility.GetFavor(god);
    Check(Beggar.CannotPay(map,god)==null || !god.Good,"beggar: colony can pay what a good god's beggar asks");
    Beggar.Offer(giver,r.a);Check(r.stage==1 && r.b==giver,"beggar: giving the alms is accepted");
    Beggar.Reveal(r);
    Check(FaithUtility.GetFavor(god)-favor>=Beggar.Reward*Faith.GainFactor-.01f && Items.Has(giver,"LP_E3_DivineGlance"),"beggar: the avatar reveal gives +15 favor and the giver's blessing");
    var r2=Beggar.Start(map);var hitter=E3Pawn();favor=FaithUtility.GetFavor(god);Beggar.OnHarmed(r2,hitter);
    Check(FaithUtility.GetFavor(god)<favor && Items.Has(hitter,"LP_E3_DivineDispleasure"),"beggar: striking the avatar costs favor and curses the attacker");
   }finally{if(saved!=null)FaithUtility.SetPatron(saved,true);}
  }

  void CheckE3Bards(){
   var r=Bards.Start(map);Check(r!=null && r.pawns.Count>=2,"bards: 2-4 bards arrive");
   var c=map.mapPawns.FreeColonistsSpawned.First();Events3.State.deeds.Add("测试功绩");
   Bards.Perform(r);
   Check(c.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_E3_BardShow"))!=null,"bards: the show gives everyone a mood boost");
   Events3.State.deeds.Remove("测试功绩");
  }

  void CheckE3Silver(){
   var r=SilverGuest.Start(map);Check(r!=null && r.a.Spawned,"silver dragon guest: the old traveller arrives");
   var friend=E3Pawn();r.tally[friend]=50;r.score=7;r.count=3;int scales=E3Count("LP_SilverDragonScale");
   SilverGuest.Reveal(r);
   Check(E3Count("LP_SilverDragonScale")-scales==3 && Items.Has(friend,"LP_E3_SilverFriend") && r.done,"silver dragon guest: a great stay leaves 3 scales and makes the companion a dragon friend");
   var r2=SilverGuest.Start(map);var rude=E3Pawn();int hc=rude.health.hediffSet.hediffs.Count;SilverGuest.OnHarmed(r2,rude,"测试");
   Check(r2.done && rude.health.hediffSet.hediffs.Count>hc,"silver dragon guest: harming it earns a frost breath");
  }

  void CheckE3Ghost(){
   var dead=Person();GenSpawn.Spawn(dead,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);dead.skills.GetSkill(SkillDefOf.Cooking).Level=14;dead.Kill(null);
   var corpse=dead.Corpse;corpse.Age=5*GenDate.TicksPerDay;e3Made.Add(corpse);
   Check(GhostWish.Eligible(corpse) && GhostWish.CanFire(map),"ghost wish: a colonist dead 5 days is eligible");
   var r=GhostWish.Start(map,0);Check(r!=null && r.stage==0 && r.val>0 && r.skill!=null,"ghost wish: the ghost asks for a burial and offers half its skill xp");
   GhostWish.Accept(r);var t=r.a;var sk=t.skills.GetSkill(r.skill);float xp=LichPhylactery.Growth.Progress(sk);
   GhostWish.Complete(r);
   Check(LichPhylactery.Growth.Progress(sk)>xp && t.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_E3_GhostRest"))!=null,"ghost wish: completing it passes the skill xp and lays the ghost to rest");
   Check(!GhostWish.Eligible(corpse),"ghost wish: each dead colonist asks only once");
  }

  void CheckE3Noble(){
   if(!Noble.CanFire(map)){Check(true,"noble: alliance or Thay missing, skipped");return;}
   var r=Noble.Start(map);var ev=Tribute.Events;
   Check(r!=null && r.a.Spawned && ev.scheduledRaids.Any(x=>x.letterLabel==Noble.RaidLabel),"noble: a wounded noble arrives with Thay pursuers scheduled");
   int g=Factions.Hostile.PlayerGoodwill;Noble.HandOver(r);
   Check(!ev.scheduledRaids.Any(x=>x.letterLabel==Noble.RaidLabel) && (Factions.Hostile.PlayerGoodwill>g || g>=100),"noble: handing over cancels the raid and pleases Thay");
   var r2=Noble.Start(map);Noble.Shelter(r2);r2.until=Events3.Now-1;int cg=Factions.Colony.PlayerGoodwill;Noble.Tick(r2);
   Check(r2.done && (Factions.Colony.PlayerGoodwill>cg || cg>=100),"noble: sheltering until the stay ends earns alliance goodwill and gifts");
  }

  void CheckE3Ward(){
   if(!ModsConfig.BiotechActive){Check(true,"ward: Biotech off, skipped");return;}
   var r=Events3.Add(Ward.Kind,map);var child=Ward.Accept(r);
   Check(child!=null && child.DevelopmentalStage==DevelopmentalStage.Child && Items.Has(child,"LP_E3_GiftedWard") && child.skills.GetSkill(SkillDefOf.Intellectual).passion==Passion.Major,"ward: the gifted child arrives with the magic-talent trait and a passion for study");
   e3Made.Add(child);
   child.ageTracker.AgeBiologicalTicks=13L*GenDate.TicksPerYear+1000;child.skills.GetSkill(SkillDefOf.Intellectual).Level=8;Ward.Graduate(r);
   var wiz=DefDatabase<LP_ClassDef>.GetNamedSilentFail("LP_Class_Wizard");
   Check(wiz==null || ClassSystem.Level(child,wiz)>=1,"ward: graduating with intellect 8 makes a level 1 wizard");
  }

  void CheckE3Starfall(){
   var c=Starfall.Start(map,true);Check(c!=null && map.gameConditionManager.ConditionIsActive(Starfall.Def),"starfall: the meteor shower condition starts");
   int fell=Starfall.Fall(map,true);
   Check(fell>=1 && map.listerThings.AllThings.Any(t=>t.def==ThingDefOf.MeteoriteIncoming),"starfall: mithral meteorites fall ("+fell+")");
   c.End();
  }

  void CheckE3Party(){
   var r=FallenParty.Start(map);
   Check(r!=null && r.site!=null && Find.WorldObjects.Contains(r.site) && ((Site)r.site).parts.Any(p=>p.def==Events3.SiteDef),"fallen party: a site appears on the world map");
   var m=FallenParty.Monster(map);Check(m!=null,"fallen party: a legendary monster or strong beast guards the camp ("+m?.kindDef.defName+")");if(m!=null && !m.Spawned)m.Destroy();
   var log=Events3.Make("LP_AdventureLog");Check(log!=null && log.TryGetComp<CompUsable>()!=null,"fallen party: the adventure log is usable");log.Destroy();
  }

  void CheckE3Surge(){
   var r=WeaveSurge.Start(map);Check(WeaveSurge.Active(map) && r.until>Events3.Now,"weave surge: 5-day condition starts");
   var p=E3Pawn();
   Check(!WeaveSurge.Surge(p,null,1).NullOrEmpty() && !WeaveSurge.Surge(p,null,5).NullOrEmpty(),"weave surge: wild magic effects resolve");
  }

  void CheckE3Thief(){
   var box=Thief.TargetBox(map);
   if(box==null){Check(!Thief.CanFire(map),"thief: no phylactery on the map, does not fire");return;}
   Check(Thief.CanFire(map),"thief: a lich's phylactery on the map draws thieves");
   var r=Thief.Start(map);Check(r!=null && r.pawns.Count>=1 && r.pawns.All(p=>Items.Has(p,"LP_E3_Shadowed")),"thief: shadowed thieves sneak in");
   var p0=r.pawns[0];var c=map.mapPawns.FreeColonistsSpawned.First();if(p0.Spawned)p0.DeSpawn();GenSpawn.Spawn(p0,CellFinder.RandomClosewalkCellNear(c.Position,map,2),map);Thief.Tick(r);
   Check(!Items.Has(p0,"LP_E3_Shadowed"),"thief: a colonist within 5 cells reveals the thief");
   foreach(var p in r.pawns)p.Destroy();
   // 被偷走后：藏身处、过期卖给九狱掮客
   var q=Events3.Add(Thief.Kind,map);q.thing=box;q.faction=r.faction;var cell=box.Position;
   box.DeSpawn();Events3.State.hold.TryAdd(box,false);q.stage=1;q.until=Events3.Now-1;
   var owner=box.soul;Thief.Tick(q);
   bool seized=Pacts.Comp!=null && Pacts.Comp.seized.Contains(box);
   Check(seized && Items.Has(owner,"LP_PhylacteryLost"),"thief: an unrecovered phylactery is sold to the hell brokers");
   if(seized){Pacts.RansomPhylactery(owner,map,false);}
   Check(box.Spawned,"thief: the sold phylactery can be ransomed back");
  }

  void CheckE3Calling(){
   var p=E3Pawn();var h=Adventurer.TryAdd(p,5,true);Check(h!=null,"calling: test adventurer");
   var r=Calling.Start(p,5);Check(r!=null && r.until>Events3.Now,"calling: a level 5 adventurer receives a personal quest");
   if(r.stage==0)r.count=Calling.Kills;else r.val-=r.val2+1;
   Calling.Tick(r);Check(r.flag,"calling: finishing the task asks for a reward");
   var letter=Find.LetterStack.LettersListForReading.OfType<Letter_E3>().LastOrDefault(l=>l.rid==r.id);
   Check(letter!=null && Calling.Options(letter,r).Count()==3,"calling: three rewards to choose from");
   int a=Calling.Primary(p),before=h.scores[a];Calling.Options(letter,r).First().action();
   Check(h.scores[a]==before+1 && r.done,"calling: the attribute reward raises the primary ability by 1");
  }

  void CheckE3Tourney(){
   if(!Tourney.CanFire(map)){Check(true,"tourney: alliance missing, skipped");return;}
   var r=Tourney.Start(map);var text=Tourney.Resolve(r);
   Check(r.done && text!=null && text.Contains("剑术") && text.Contains("摔角"),"tourney: four bouts are resolved");
  }

  void CheckE3Rival(){
   var lich=RivalLich.Lich(map);
   if(lich==null){Check(!RivalLich.CanFire(map),"rival lich: no lich with a level 5 phylactery, does not fire");return;}
   if(!RivalLich.CanFire(map)){Check(true,"rival lich: Thay missing, skipped");return;}
   var r=RivalLich.Start(map);var box=RivalLich.Box(r);int lv=box.level;float xp=box.experience;
   Events2.TestRoll=null;bool won=RivalLich.Duel(r);
   Check(won?(box.level>lv || box.experience>xp):box.experience<=xp,"rival lich: the duel feeds or drains the phylactery (won="+won+")");
   var r2=RivalLich.Start(map);RivalLich.Hunt(r2);Check(r2.site!=null && r2.stage==1,"rival lich: the hunt puts his tower on the world map");
   var r3=RivalLich.Start(map);int n=Tribute.Events.scheduledRaids.Count;RivalLich.Refuse(r3);Check(Tribute.Events.scheduledRaids.Count==n+2,"rival lich: refusing schedules two Thay raids");
  }

  void CheckE3Treant(){
   if(!Treant.Trees(map).Any()){var tree=ThingMaker.MakeThing(ThingDefOf.Plant_TreeOak);GenSpawn.Spawn(tree,CellFinder.RandomClosewalkCellNear(map.Center,map,20,c=>c.GetPlant(map)==null && c.GetEdifice(map)==null),map);}
   var r=Treant.Start(map,false);Check(r!=null && r.a.Spawned && r.stage==0,"treant: a friendly treant wakes");
   r.next=Events3.Now;Treant.Tick(r);var heart=map.listerThings.ThingsOfDef(Treant.HeartDef).FirstOrDefault();
   Check(heart!=null,"treant: it plants a heart of the forest");
   var plant=GenRadial.RadialCellsAround(heart.Position,6f,false).Select(c=>c.InBounds(map)?c.GetPlant(map):null).FirstOrDefault(x=>x!=null);
   if(plant!=null)Check(Treant.NearHeart(plant),"treant: plants near the heart grow faster");
   if(Treant.Trees(map).Any()){var r2=Treant.Start(map,true);Check(r2!=null && r2.pawns.Count>=3 && r2.pawns.All(p=>p.MentalStateDef==MentalStateDefOf.ManhunterPermanent && Items.Has(p,"LP_E3_Wooden")),"treant: an angry treant brings awakened trees that fear fire");}
  }

  void CheckE3Tarrasque(){
   var r=Tarrasque.Start(map);Check(r!=null && r.until>Events3.Now,"tarrasque: it wakes and approaches");
   r.answered=true;r.stage=2;r.until=Events3.Now;Tarrasque.Tick(r);var t=r.a;
   Check(t!=null && t.Spawned && Items.Has(t,"LP_E3_TarrasqueHide") && Items.Has(t,"LP_E3_TrapWounds"),"tarrasque: it arrives with its hide, wounded by the traps");
   int shells=E3Count("LP_TarrasqueShell");t.Kill(null);
   Check(E3Count("LP_TarrasqueShell")-shells==Tarrasque.Shells && Events3.State.deeds.Contains("屠杀了塔拉斯克"),"tarrasque: killing it drops its shell and becomes a deed");
   Events3.State.deeds.Remove("屠杀了塔拉斯克");
  }

  // 存读档：一条待到的塔拉斯克记录、一条带计数的银龙记录。
  // 1.118.0 热修：额外袭击按设置的间隔落地。
  void CheckExtraRaids(){
   var s=LichMod.Settings;var saved=(s.extraRaids,s.extraRaidDays);var st=GameComp<ExtraRaidState>.Get();
   try{
    s.extraRaids=false;ExtraRaids.Tick(st);Check(st.next==-1,"extra raids: off by default, no timer");
    s.extraRaids=true;s.extraRaidDays=7;ExtraRaids.Tick(st);
    Check(st.next>Find.TickManager.TicksGame+6*GenDate.TicksPerDay,"extra raids: switching on schedules one raid "+s.extraRaidDays+" days out");
    var lords=map.lordManager.lords.Count;
    Check(ExtraRaids.Fire(map) && map.lordManager.lords.Count>lords,"extra raids: a hostile raid arrives");
   }finally{(s.extraRaids,s.extraRaidDays)=saved;st.next=-1;}
  }
  void PrepareEvents3Save(){   var st=Events3.State;var r=Events3.Add(Tarrasque.Kind,map);r.until=Events3.Now+5*GenDate.TicksPerDay;r.answered=true;r.stage=4;r.text="存档测试";
   var c=map.mapPawns.FreeColonistsSpawned.First();r.tally[c]=7;st.Deed("存档测试功绩");st.treeCuts.Add(Events3.Now);
  }
  void CheckEvents3AfterLoad(){
   var st=Events3.State;var r=st?.recs.FirstOrDefault(x=>x.text=="存档测试");
   Check(r!=null && r.kind==Tarrasque.Kind && r.stage==4 && r.answered && r.tally.Values.Contains(7),"1.117.0 event records survive save/load");
   Check(st.deeds.Contains("存档测试功绩") && st.treeCuts.Count>0,"1.117.0 deeds and tree cuts survive save/load");
   st.recs.Remove(r);st.deeds.Remove("存档测试功绩");
  }
 }
}
