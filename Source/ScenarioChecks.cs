using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 public partial class LichChecks {
  void CheckScenarios(){
   foreach(var name in new[]{"LP_LichScenario","LP_FireGiantScenario","LP_TrollScenario","LP_ForceDragonScenario"}){
    var def=DefDatabase<ScenarioDef>.GetNamedSilentFail(name);
    Check(def!=null && !def.ConfigErrors().Any(),"race scenario loads cleanly: "+name+" "+(def==null?"missing":string.Join(";",def.ConfigErrors())));
    Check(def.scenario.AllParts.All(p=>p.def!=null),"race scenario parts resolve: "+name);
   }
   var lichScen=DefDatabase<ScenarioDef>.GetNamed("LP_LichScenario").scenario;
   var ascension=lichScen.AllParts.OfType<ScenPart_LichAscension>().Single();
   Check(ascension.level==3 && ascension.skeletons==2 && ascension.golems==1 && !string.IsNullOrEmpty(ascension.Summary(lichScen)),"lich scenario starts at phylactery level 3 with two skeletons and a golem");
   Check(lichScen.AllParts.OfType<ScenPart_TimedCondition>().Single().condition==GameConditionDefOf.Eclipse,"lich scenario darkens the sun");
   var trollScen=DefDatabase<ScenarioDef>.GetNamed("LP_TrollScenario").scenario;
   Check(trollScen.AllParts.OfType<ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes>().Any() && Traverse.Create(trollScen).Field("playerFaction").Field("factionDef").GetValue<FactionDef>()==FactionDefOf.PlayerTribe,"troll scenario is a tribal troll start");
   var giantScen=DefDatabase<ScenarioDef>.GetNamed("LP_FireGiantScenario").scenario;
   Check(giantScen.AllParts.OfType<ScenPart_FireGiantClan>().Any() && giantScen.AllParts.OfType<ScenPart_StartingResearch>().Count()>=2,"fire giant scenario has forge clan, smithing and giant forging research");
   Check(ScenPart_LichAscension.PickLich(map)!=null,"lich scenario can pick a starting colonist");

   // 巫妖开局的实际效果
   Pawn lich=Person();
   for(int attempt=0;attempt<20 && lich.GetStatValue(StatDefOf.PsychicSensitivity)<=0;attempt++){lich.Destroy();lich=Person();}
   GenSpawn.Spawn(lich,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);
   var box=ScenPart_LichAscension.Apply(lich,3,2,1);
   var hLich=Necro.Lich(lich);
   Check(hLich!=null && hLich.Active && box.Spawned && box.soul==lich && box.level==3,"lich start binds a spawned level 3 phylactery: lich="+(hLich!=null)+" active="+(hLich?.Active)+" spawned="+box?.Spawned+" soul="+(box?.soul==lich)+" level="+box?.level+" cap="+LichMod.Settings.levelCap);
   Check(lich.abilities.GetAbility(Necro.Def<AbilityDef>("FearGaze"))!=null,"lich start already knows fear gaze");
   Check(hLich.Points>1,"lich start begins with psyfocus");
   var startAltar=map.listerThings.ThingsOfDef(Necro.Def<ThingDef>("Altar")).OfType<Building_Altar>().FirstOrDefault(a=>a.selected==box);
   Check(startAltar!=null && startAltar.Faction==Faction.OfPlayer,"lich start places a player altar serving the phylactery");
   var minions=Necro.LivingConstructs(lich).ToList();
   Check(minions.Count==3 && minions.Count(p=>Necro.Construct(p).kind=="Skeleton")==2 && minions.Count(p=>Necro.Construct(p).kind=="Golem")==1,"lich start awakens two skeletons and a golem");
   foreach(var p in minions)Necro.Construct(p).Maintain(250);
   Check(minions.All(p=>!Necro.Construct(p).dormant && p.Faction==Faction.OfPlayer && p.Spawned),"starting constructs are controlled and awake");
   foreach(var p in minions){Necro.Registry.constructs.Remove(p);p.Destroy();}
   Necro.Registry.boxes.Remove(box);startAltar.Destroy();box.Destroy();lich.Destroy();

   // 火巨人开局的实际效果
   var giantDef=DefDatabase<XenotypeDef>.GetNamed("LP_FireGiant");
   var giant=Unscathed(PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:giantDef,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25,fixedChronologicalAge:25)));
   GenSpawn.Spawn(giant,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);
   var forge=ScenPart_FireGiantClan.Apply(map,30);
   Check(forge!=null && forge.Spawned && forge.Faction==Faction.OfPlayer && Math.Abs(forge.TryGetComp<CompRefuelable>().Fuel-30)<.01f,"fire giant start places a fuelled forge");
   Check(giant.apparel.WornApparel.Any(a=>a.def.defName=="LP_FireGiantTunic"),"fire giant start dresses giants in forge tunics");
   Check(giant.equipment.Primary?.def.defName=="LP_FireGiantMaul","fire giant start arms the best melee giant with a maul");
   Check(map.mapPawns.FreeColonistsSpawned.Where(p=>!FireGiant.Active(p)).All(p=>p.apparel.WornApparel.All(a=>a.def.defName!="LP_FireGiantTunic")),"fire giant start leaves other colonists alone");
   forge.Destroy();giant.Destroy();

   // 龙蛋开局的实际效果
   var dragonScen=DefDatabase<ScenarioDef>.GetNamed("LP_ForceDragonScenario").scenario;
   var clutch=dragonScen.AllParts.OfType<ScenPart_ForceDragonClutch>().Single();
   Check(clutch.age==4 && clutch.eggs==1 && !string.IsNullOrEmpty(clutch.Summary(dragonScen)) && dragonScen.AllParts.OfType<ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes>().Any(),"dragon egg scenario starts an ancient dragon with one egg");
   var dragonDef=DefDatabase<XenotypeDef>.GetNamed("LP_ForceDragon");
   var elder=GenerateDragon(dragonDef);
   GenSpawn.Spawn(elder,OpenArea(5,altar.Position),map);
   var hoard=ScenPart_ForceDragonClutch.Apply(map,clutch.age,clutch.eggs,clutch.eggProgress,clutch.gold,clutch.silver,out var hoardInterior);
   var hoardRoom=DragonHoard.RoomOf(elder);
   var clutchEggs=hoard.Where(t=>t.def==DragonHoard.EggDef).ToList();
   Check(ForceDragon.Age(elder)==4 && hoardRoom!=null && hoardInterior.Area==25,"dragon egg start makes the dragon ancient and builds its roofed hoard");
   Check(clutchEggs.Count==1 && clutchEggs.All(e=>e.GetRoom()==hoardRoom && Math.Abs(e.TryGetComp<CompDragonEgg>().progressDays-.9f*LichMod.Settings.dragonEggHatchDays)<.01f),"dragon egg start lays one nearly hatched egg in the hoard");
   Check(ForceDragon.Heart(elder).hoardValue>=3000 && Near(ForceDragon.Heart(elder).Value,ForceDragon.Heart(elder).Max),"dragon egg start stocks the hoard with gold and silver and fills force");
   foreach(var t in hoard)if(!t.Destroyed)t.Destroy();
   foreach(var c in hoardInterior.Cells)map.roofGrid.SetRoof(c,null);
   elder.Destroy();
   // 开局限时状况
   var cond=ScenPart_TimedCondition.Apply(map,GameConditionDefOf.Eclipse,1);
   Check(map.gameConditionManager.ConditionIsActive(GameConditionDefOf.Eclipse) && cond.TicksLeft<=60000 && cond.TicksLeft>59000,"timed scenario condition runs for the configured days");
   cond.End();
   CheckRaceScenarios();
  }

  // ==================== 1.20.0 六个新种族开局 ====================
  static readonly string[] newScenarios={"LP_HalfOrcScenario","LP_ElfScenario","LP_DwarfScenario","LP_MindFlayerScenario","LP_MinotaurScenario","LP_GithyankiScenario"};
  static ScenarioDef Scen(string name)=>DefDatabase<ScenarioDef>.GetNamed(name);
  static T Part<T>(string scen) where T:ScenPart=>Scen(scen).scenario.AllParts.OfType<T>().Single();
  static int ScenThing(string scen,string thing)=>Scen(scen).scenario.AllParts.OfType<ScenPart_ThingCount>().Where(p=>Traverse.Create(p).Field("thingDef").GetValue<ThingDef>()?.defName==thing).Sum(p=>Math.Max(1,Traverse.Create(p).Field("count").GetValue<int>()));
  static int GoodwillPart(string scen,FactionDef f)=>Scen(scen).scenario.AllParts.OfType<ScenPart_FactionGoodwill>().Single(p=>p.faction==f).goodwill;
  static bool HasGoodwillPart(string scen,FactionDef f)=>Scen(scen).scenario.AllParts.OfType<ScenPart_FactionGoodwill>().Any(p=>p.faction==f);
  // 按剧本的异种型配置生成开局人物（成年、玩家阵营、站在地图中央附近）。
  List<Pawn> ScenarioStarters(string scen,Dictionary<string,int> expect){
   var cfg=Part<ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes>(scen);var pawns=new List<Pawn>();var seen=new Dictionary<string,int>();
   foreach(var xc in cfg.xenotypeCounts){int n=Traverse.Create(xc).Field("count").GetValue<int>();seen[xc.xenotype.defName]=n;Check(Traverse.Create(xc).Field("requiredAtStart").GetValue<bool>() && xc.allowedDevelopmentalStages==DevelopmentalStage.Adult,"scenario "+scen+" forces adult "+xc.xenotype.defName);for(int i=0;i<n;i++)pawns.Add(RacePawn(xc.xenotype.defName));}
   Check(expect.Count==seen.Count && expect.All(kv=>seen.TryGetValue(kv.Key,out int n) && n==kv.Value),"scenario "+scen+" starts with "+string.Join(", ",expect.Select(kv=>kv.Value+" "+kv.Key)));
   Check(pawns.All(p=>expect.ContainsKey(p.genes.Xenotype.defName) && p.DevelopmentalStage.Adult()),"generated starters of "+scen+" have the forced xenotypes");
   return pawns;
  }
  void CleanScenarioRun(HashSet<Thing> before,HashSet<Pawn> world){
   foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p)).ToList()){
    if(Find.FactionManager.AllFactionsListForReading.Any(f=>f.leader==p)){p.DeSpawn();if(!Find.WorldPawns.Contains(p))Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);continue;}
    Necro.Registry?.constructs.Remove(p);p.Destroy();
   }
   foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Filth) && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList())if(!t.Destroyed)t.Destroy();
   foreach(var p in Find.WorldPawns.AllPawnsAliveOrDead.Where(p=>!world.Contains(p) && !Find.FactionManager.AllFactionsListForReading.Any(f=>f.leader==p)).ToList()){Find.WorldPawns.RemovePawn(p);if(!p.Destroyed)p.Destroy();}
   Decapitation.Tracker?.Prune();ColonyCensus.Dirty();
  }
  // 排队一次限时袭击：检查落地时间；先确认没到时间不来，再把时间拨到期，确认按时落地、派系和来信标题都对。
  List<Pawn> CheckTimedRaid(string scen,FactionDef faction,float minDay,float maxDay,PawnsArrivalModeDef arrival,string label){
   var part=Part<ScenPart_TimedRaid>(scen);var ev=Tribute.Events;int now=Find.TickManager.TicksGame;
   Check(part.faction==faction && Math.Abs(part.days.min-minDay)<.01f && Math.Abs(part.days.max-maxDay)<.01f && part.arrival==arrival && part.letterLabel==label && !part.ConfigErrors().Any() && part.Summary(Scen(scen).scenario).Contains(label),"scenario "+scen+" schedules a "+faction.defName+" raid on day "+minDay+"-"+maxDay+": "+part.Summary(Scen(scen).scenario));
   ev.scheduledRaids.Clear();var entry=part.Schedule();
   Check(ev.scheduledRaids.Count==1 && entry.fireTick>=now+(int)(minDay*GenDate.TicksPerDay)-1 && entry.fireTick<=now+(int)(maxDay*GenDate.TicksPerDay)+1 && entry.arrival==arrival,"the "+scen+" raid is queued for day "+((entry.fireTick-now)/(float)GenDate.TicksPerDay).ToString("0.#"));
   var spawned=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
   Check(ScheduledRaid.Process(ev)==0 && ev.scheduledRaids.Count==1,"the "+scen+" raid does not land early");
   entry.fireTick=Find.TickManager.TicksGame;
   var f=Factions.Of(faction);float points=entry.Points(map,f),expected=Math.Max(StorytellerUtility.DefaultThreatPointsNow(map)*part.pointsFactor,faction.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)*1.05f);
   Check(ScheduledRaid.Process(ev)==1 && ev.scheduledRaids.Count==0,"the "+scen+" raid lands on time");
   var raiders=map.mapPawns.AllPawnsSpawned.Where(p=>!spawned.Contains(p) && p.Faction==f).ToList();
   Check(raiders.Count>0,"the "+scen+" raid brings "+faction.defName+" ("+raiders.Count+" pawns, "+points.ToString("0")+" points)");
   Check(Near(points,expected),"the "+scen+" raid uses "+part.pointsFactor+" x the normal threat");
   var letter=Find.LetterStack.LettersListForReading.LastOrDefault();
   Check(letter!=null && letter.Label.ToString().Contains(label),"the "+scen+" raid letter is titled "+label+": "+(letter==null?"none":letter.Label.ToString()));
   return raiders;
  }
  void CheckRaceScenarios(){
   var all=DefDatabase<ScenarioDef>.AllDefsListForReading;int lastVanilla=all.FindLastIndex(d=>d.modContentPack?.IsOfficialMod==true);int firstOurs=all.FindIndex(d=>d.defName=="LP_LichScenario");
   Check(newScenarios.All(n=>all.FindIndex(d=>d.defName==n)>Math.Max(lastVanilla,firstOurs)),"the six new race scenarios are listed after the vanilla and existing race scenarios");
   foreach(var n in newScenarios){
    var def=DefDatabase<ScenarioDef>.GetNamedSilentFail(n);
    Check(def!=null && !def.ConfigErrors().Any() && def.scenario.AllParts.All(p=>p.def!=null && !p.ConfigErrors().Any()),"race scenario loads cleanly: "+n+" "+(def==null?"missing":string.Join(";",def.ConfigErrors().Concat(def.scenario.AllParts.SelectMany(p=>p.ConfigErrors())))));
    var paras=def.description.Split(new[]{"\n\n"},StringSplitOptions.RemoveEmptyEntries);
    Check(paras.Length==3 && paras[2].StartsWith("注意") && def.label.Any(ch=>ch>0x4e00),"scenario "+n+" description has background, play tips and a difficulty line");
    Check(def.scenario.AllParts.Where(p=>p.def.defName.StartsWith("LP_")).All(p=>p.def.selectionWeight==0 && !p.Summary(def.scenario).NullOrEmpty()),"scenario "+n+" uses only non-random LP parts with summaries");
   }
   foreach(var name in new[]{"LP_TimedRaid","LP_MountainCamp","LP_Labyrinth","LP_StarterKit","LP_StarterBoon","LP_StarterBackstory","LP_FlayerEscape"}){var d=DefDatabase<ScenPartDef>.GetNamed(name);Check(d.selectionWeight==0 && d.label.Any(ch=>ch>0x4e00) && typeof(ScenPart).IsAssignableFrom(d.scenPartClass) && d.scenPartClass.GetMethod("DoEditInterface").DeclaringType!=typeof(ScenPart),"scenario part "+name+" is editor-only and never random");}
   CheckLabyrinthLayout();
   var goodwill=Find.FactionManager.AllFactions.Where(f=>!f.IsPlayer && f.HasGoodwill).ToDictionary(f=>f,f=>f.PlayerGoodwill);
   var letters=new HashSet<Letter>(Find.LetterStack.LettersListForReading);var world=new HashSet<Pawn>(Find.WorldPawns.AllPawnsAliveOrDead);var ev=Tribute.Events;
   try {
    foreach(var run in new Action[]{CheckHalfOrcScenario,CheckElfScenario,CheckDwarfScenario,CheckMindFlayerScenario,CheckMinotaurScenario,CheckGithyankiScenario}){
     var before=new HashSet<Thing>(map.listerThings.AllThings);
     try {run();} finally {CleanScenarioRun(before,world);foreach(var kv in goodwill)if(kv.Key.PlayerGoodwill!=kv.Value)Factions.SetRelation(kv.Key,Faction.OfPlayer,kv.Value);}
    }
   } finally {
    ev.scheduledRaids.Clear();
    foreach(var l in Find.LetterStack.LettersListForReading.Where(l=>!letters.Contains(l)).ToList())Find.LetterStack.RemoveLetter(l);
   }
  }
  void CheckGoodwillParts(string scen,params (FactionDef f,int gw)[] expect){
   foreach(var (f,gw) in expect){
    Check(HasGoodwillPart(scen,f) && GoodwillPart(scen,f)==gw,"scenario "+scen+" starts "+f.defName+" at "+gw);
    Factions.SetRelation(Factions.Of(f),Faction.OfPlayer,13);
    Check(Scen(scen).scenario.AllParts.OfType<ScenPart_FactionGoodwill>().Single(p=>p.faction==f).Apply() && Factions.Of(f).PlayerGoodwill==gw,"scenario "+scen+" goodwill applies to "+f.defName+": "+Factions.Of(f).PlayerGoodwill);
   }
  }

  void CheckHalfOrcScenario(){
   const string s="LP_HalfOrcScenario";
   var pawns=ScenarioStarters(s,new Dictionary<string,int>{{"LP_HalfOrc",4}});
   Check(Traverse.Create(Scen(s).scenario).Field("playerFaction").Field("factionDef").GetValue<FactionDef>()==FactionDefOf.PlayerTribe,"the half-orc scenario is a tribal start");
   var bs=Part<ScenPart_StarterBackstory>(s);
   foreach(var p in pawns){p.story.Adulthood=DefDatabase<BackstoryDef>.AllDefs.First(b=>b.slot==BackstorySlot.Adulthood && b.spawnCategories.Contains("Tribal"));bs.Notify_PawnGenerated(p,PawnGenerationContext.PlayerStarter,false);}
   Check(pawns.All(p=>p.story.Adulthood.defName=="LP_ChainbreakerExile" && p.story.Adulthood.title=="断链流亡者"),"half-orc starters are chainbreaker exiles");
   var other=Person();bs.Notify_PawnGenerated(other,PawnGenerationContext.NonPlayer,false);Check(other.story.Adulthood?.defName!="LP_ChainbreakerExile","the exile backstory only touches starting pawns");other.Destroy();
   CheckGoodwillParts(s,(Factions.HostileDef,-100),(Factions.ColonyDef,20));
   Check(Factions.Hostile.PlayerRelationKind==FactionRelationKind.Hostile && !Factions.HostileDef.permanentEnemy,"Thay is hostile at -100 but not a permanent enemy");
   Part<ScenPart_StarterKit>(s).Apply(pawns);
   Check(pawns.All(p=>(p.equipment.Primary?.def.defName=="MeleeWeapon_Mace" || p.equipment.Primary?.def.defName=="MeleeWeapon_Spear") && p.equipment.Primary.Stuff==ThingDefOf.Steel),"each half-orc carries a steel mace or spear: "+string.Join(",",pawns.Select(p=>p.equipment.Primary?.def.defName)));
   Check(ScenThing(s,"Pemmican")==200 && ScenThing(s,"MedicineHerbal")==15 && ScenThing(s,"WoodLog")==300,"half-orc supplies: 200 pemmican, 15 herbal medicine, 300 wood");
   CheckTimedRaid(s,Factions.HostileDef,6,6,null,"红袍的追兵");
  }

  void CheckElfScenario(){
   const string s="LP_ElfScenario";
   var pawns=ScenarioStarters(s,new Dictionary<string,int>{{"LP_Elf",3}});
   CheckGoodwillParts(s,(Factions.ColonyDef,65));
   Check(Harper.Active() && Scen(s).description.Contains("竖琴手") && Scen(s).description.Contains("森林"),"elves start above the harper threshold and the description says so and suggests a forest");
   var target=Part<ScenPart_StarterBoon>(s).Apply(pawns).Single();
   Check(target.GetPsylinkLevel()==1 && pawns.All(p=>p==target || p.GetPsylinkLevel()==0) && target.skills.GetSkill(SkillDefOf.Shooting).Level==pawns.Max(p=>p.skills.GetSkill(SkillDefOf.Shooting).Level),"the best elven archer gets a level 1 psylink for trance meditation");
   Check(target.genes.GetGene(DefDatabase<GeneDef>.GetNamed("LP_ElfTrance"))?.Active==true,"the psylinked elf has trance meditation");
   Part<ScenPart_StarterKit>(s).Apply(pawns);
   Check(pawns.All(p=>p.equipment.Primary?.def.defName=="Bow_Recurve"),"each elf carries a recurve bow");
   Check(ScenThing(s,"Bow_Great")==1 && ScenThing(s,"MedicineHerbal")==30 && ScenThing(s,"Cloth")==150 && (!ModsConfig.BiotechActive || ScenThing(s,"PoluxSeed")==1),"elf supplies: one great bow, 30 herbal medicine, 150 cloth and a polux seed");
  }

  void CheckDwarfScenario(){
   const string s="LP_DwarfScenario";
   var pawns=ScenarioStarters(s,new Dictionary<string,int>{{"LP_Dwarf",3}});
   CheckGoodwillParts(s,(Factions.ColonyDef,50),(Factions.EmberDef,-60));
   foreach(var p in pawns)p.skills.GetSkill(SkillDefOf.Crafting).passion=Passion.None;
   var miner=Part<ScenPart_StarterBoon>(s).Apply(pawns).Single();
   Check(miner.skills.GetSkill(SkillDefOf.Mining).Level==pawns.Max(p=>p.skills.GetSkill(SkillDefOf.Mining).Level) && (miner.skills.GetSkill(SkillDefOf.Crafting).TotallyDisabled || miner.skills.GetSkill(SkillDefOf.Crafting).passion==Passion.Minor) && pawns.All(p=>p==miner || p.skills.GetSkill(SkillDefOf.Crafting).passion==Passion.None),"the best dwarven miner gets a minor crafting passion");
   var camp=Part<ScenPart_MountainCamp>(s);
   var want=new Dictionary<string,int>{{"Beer",40},{"Steel",400},{"Plasteel",60},{"BlocksGranite",100},{"Apparel_PlateArmor",1}};if(ModsConfig.RoyaltyActive)want["MeleeWeapon_Warhammer"]=1;
   Check(want.All(kv=>camp.things.Any(t=>t.thing.defName==kv.Key && t.count==kv.Value)) && camp.things.Count==want.Count,"the mountain camp holds the beer and dwarven supplies: "+string.Join(",",camp.things.Select(t=>t.Label)));
   // 厚岩顶优先：在开局点外做一块厚岩顶空地，开局点本身不盖顶。
   var center=OpenArea(3,altar.Position);var pocket=OpenArea(3,center);
   Check(pocket.DistanceTo(center)<35,"the rock-roof fixture lies within the camp search radius ("+pocket.DistanceTo(center).ToString("0")+")");
   var roofed=CellRect.CenteredOn(pocket,3).Cells.Where(c=>!c.Roofed(map)).ToList();
   var restoreRoofs=new Dictionary<IntVec3,RoofDef>();
   foreach(var c in roofed)map.roofGrid.SetRoof(c,RoofDefOf.RoofRockThick);
   try {
    var placed=ScenPart_MountainCamp.Apply(map,center,camp.things,40,out bool under);
    Check(under && placed.Count>0 && placed.All(t=>t.Spawned && ScenPart_MountainCamp.UnderRock(t.Position,map)),"the mountain camp puts every supply under a thick rock roof");
    Check(placed.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount)==400 && placed.Where(t=>t.def.defName=="Beer").Sum(t=>t.stackCount)==40 && placed.Any(t=>t.def.defName=="Apparel_PlateArmor" && t.Stuff==ThingDefOf.Steel),"the camp really holds 400 steel, 40 beer and steel plate armor");
    Check(!ScenPart_MountainCamp.UnderRock(center,map),"the start spot itself is open ground, so the camp chose the rock roof on purpose");
    foreach(var t in placed)t.Destroy();
    int lettersBefore=Find.LetterStack.LettersListForReading.Count;
    var open=OpenArea(3,pocket);foreach(var c in CellRect.CenteredOn(open,2).Cells)if(c.Roofed(map)){restoreRoofs[c]=c.GetRoof(map);map.roofGrid.SetRoof(c,null);}var fallback=ScenPart_MountainCamp.Apply(map,open,new List<CampThing>{new CampThing{thing=ThingDefOf.Steel,count=10}},1,out bool under2);
    Check(!under2 && fallback.Count==1 && fallback[0].Position.DistanceTo(open)<6 && Find.LetterStack.LettersListForReading.Count==lettersBefore+1 && Find.LetterStack.LettersListForReading.Last().Label.ToString().Contains("没找到山体") && Scen(s).description.Contains("山地"),"without a mountain the supplies go to the usual spot and a letter suggests hills or mountains");
   } finally {foreach(var c in roofed)map.roofGrid.SetRoof(c,null);foreach(var kv in restoreRoofs)map.roofGrid.SetRoof(kv.Key,kv.Value);}
  }

  void CheckMindFlayerScenario(){
   const string s="LP_MindFlayerScenario";
   var pawns=ScenarioStarters(s,new Dictionary<string,int>{{"LP_MindFlayer",1},{"Baseliner",1}});
   Check(!HasGoodwillPart(s,Factions.IllithidDef) && !HasGoodwillPart(s,Factions.ColonyDef) && Factions.IllithidDef.permanentEnemy,"the flayer scenario leaves the hive hostile and adds no extra Faerun penalty");
   var part=Part<ScenPart_FlayerEscape>(s);
   Check(Math.Abs(part.cerebrum-60)<.01f && Math.Abs(part.tadpole-.3f)<.001f && part.prisoners==2,"the flayer scenario: cerebrum 60, tadpole 30%, two prisoners");
   var flayer=pawns.First(MindFlayer.IsMindFlayer);var human=pawns.First(p=>p!=flayer);
   var prisoners=ScenPart_FlayerEscape.Apply(map,pawns,part.cerebrum,part.tadpole,part.prisoners);
   Check(Math.Abs(MindFlayer.Points(flayer)-60)<.5f,"the escaped flayer starts with 60 cerebrum: "+MindFlayer.Points(flayer));
   var t=Tadpoles.Of(human);
   Check(t!=null && Math.Abs(t.Severity-.3f)<.01f && t.Frozen && HalfIllithid.Suppressed(human) && HalfIllithid.Is(human) && t.implanterFaction==Faction.OfPlayer,"the human servant carries a 30% tadpole under suppressant as a half-illithid");
   t.Advance(GenDate.TicksPerDay);Check(Math.Abs(t.Severity-.3f)<.01f,"the suppressant keeps the tadpole frozen");
   Check(prisoners.Count==2 && prisoners.All(p=>p.IsPrisonerOfColony && p.Downed && !p.Dead && p.genes.Xenotype==XenotypeDefOf.Baseliner && p.health.hediffSet.HasHediff(ScenPart_FlayerEscape.Bound) && p.equipment.Primary==null),"two bound human prisoners lie ready as the first brains");
   Check(MindFlayer.CannotDevour(flayer,prisoners[0])==null || MindFlayer.PsychicallyDeaf(prisoners[0]),"the flayer can devour a bound prisoner");
   Check(ScenThing(s,"LP_Tadpole")==2 && ScenThing(s,"LP_PsionicSuppressant")==3 && ScenThing(s,"MealSimple")==20 && ScenThing(s,"MedicineIndustrial")==10 && ScenThing(s,"Steel")==300 && ScenThing(s,"WoodLog")==300,"flayer supplies: 2 tadpoles, 3 suppressants, 20 simple meals, 10 medicine, 300 steel and wood");
   Check(Scen(s).description.Contains("猎巫狂热分子"),"the flayer scenario warns about witch hunters");
   CheckTimedRaid(s,Factions.IllithidDef,10,15,DefDatabase<PawnsArrivalModeDef>.GetNamed("LP_Burrow"),"殖巢的追捕");
  }

  void CheckMinotaurScenario(){
   const string s="LP_MinotaurScenario";
   var pawns=ScenarioStarters(s,new Dictionary<string,int>{{"LP_Minotaur",2},{"LP_HalfOrc",1}});
   CheckGoodwillParts(s,(Factions.HostileDef,-100));
   foreach(var r in new[]{"LP_Relation_MinotaurHalfOrc","LP_Relation_HalfOrcMinotaur"}){var d=DefDatabase<RacialRelationDef>.GetNamed(r);Check(d.enabled && d.thought.stages[0].baseMoodEffect>=2,"minotaurs and half-orcs like each other: "+r);}
   Part<ScenPart_StarterKit>(s).Apply(pawns);
   Check(pawns.All(p=>(p.equipment.Primary?.def.defName=="MeleeWeapon_Warhammer" || p.equipment.Primary?.def.defName=="MeleeWeapon_LongSword") && p.equipment.Primary.Stuff==ThingDefOf.Steel),"each maze dweller carries a steel warhammer or longsword");
   var lab=Part<ScenPart_Labyrinth>(s);
   Check(lab.Size==17 && lab.roof && lab.bedrolls==2 && lab.things.Any(t=>t.thing.defName=="Pemmican" && t.count==150) && lab.things.Any(t=>t.thing.defName=="MedicineHerbal" && t.count==10) && ScenThing(s,"WoodLog")==200,"the labyrinth part: 17x17, roofed, two bedrolls, 150 pemmican and 10 herbal medicine; 200 wood nearby");
   var center=OpenArea(3,altar.Position);
   var spot=Labyrinth.FindSpot(map,center,17);
   // 随机测试地图可能太挤（山多、前面的测试留下建筑）：先清出一块 17×17 的夹具地（屋顶事后恢复）；还不行就检查“跳过并来信”。
   var unroofed=new Dictionary<IntVec3,RoofDef>();
   if(spot==null){try{var c=OpenArea(9,altar.Position);foreach(var x in CellRect.CenteredOn(c,8).Cells){unroofed[x]=x.GetRoof(map);map.roofGrid.SetRoof(x,null);}center=c;spot=Labyrinth.FindSpot(map,center,17);}catch(InvalidOperationException){}}
   if(spot==null){
    int lettersBefore=Find.LetterStack.LettersListForReading.Count;
    Check(Labyrinth.Build(map,center,17,true,2,lab.things)==null && Find.LetterStack.LettersListForReading.Count==lettersBefore+1 && Find.LetterStack.LettersListForReading.Last().Label.ToString().Contains("没有地方放迷宫"),"with no flat 17x17 spot the labyrinth is skipped with a letter");
    foreach(var kv in unroofed)map.roofGrid.SetRoof(kv.Key,kv.Value);return;
   }
   Check(Labyrinth.CanPlace(spot.Value,map),"a flat 17x17 spot without buildings or water is found");
   var res=Labyrinth.Build(map,center,17,true,2,lab.things);
   try {
    Check(res!=null && res.rect==spot.Value,"the labyrinth is built where the free spot was");
    Check(res.rect.Cells.All(c=>c.GetRoof(map)==RoofDefOf.RoofConstructed),"every labyrinth cell is roofed");
    var walls=res.rect.Cells.Where(c=>c.GetEdifice(map)?.def==ThingDefOf.Wall).ToList();
    Check(walls.Count>100 && walls.All(c=>c.GetEdifice(map).Stuff?.defName=="BlocksGranite" && c.GetEdifice(map).Faction==Faction.OfPlayer),"the labyrinth walls are granite ("+walls.Count+")");
    var border=res.rect.EdgeCells.Where(c=>c.GetEdifice(map)==null).ToList();
    Check(border.Count==1 && border[0]==res.entrance && res.outside.Standable(map),"the labyrinth has exactly one entrance");
    Check(res.room.Cells.All(c=>c.GetEdifice(map)==null || c.GetEdifice(map).def.defName=="Bedroll") && res.room.Area==25,"the central room is an open 5x5");
    var trav=TraverseParms.For(TraverseMode.PassDoors);
    Check(map.reachability.CanReach(res.outside,res.roomCenter,PathEndMode.OnCell,trav),"the central room is reachable from the entrance");
    Check(res.rect.Cells.Where(c=>c.GetEdifice(map)==null).All(c=>map.reachability.CanReach(res.outside,c,PathEndMode.OnCell,trav)),"every corridor of the labyrinth is connected");
    var beds=res.things.Where(t=>t.def.defName=="Bedroll").ToList();
    Check(beds.Count==2 && beds.All(b=>b.OccupiedRect().Cells.All(c=>res.room.Contains(c)) && b.Faction==Faction.OfPlayer),"two bedrolls lie in the central room");
    Check(res.things.Where(t=>t.def.defName=="Pemmican").Sum(t=>t.stackCount)==150 && res.things.Where(t=>t.def.defName=="MedicineHerbal").Sum(t=>t.stackCount)==10 && res.things.Where(t=>t.def.category==ThingCategory.Item).All(t=>res.room.Contains(t.Position)),"pemmican and herbal medicine wait in the central room");
    Check(!Labyrinth.CanPlace(res.rect,map) && !Labyrinth.CanPlace(CellRect.CenteredOn(altar.Position,8),map) && !Labyrinth.CanPlace(new CellRect(-3,-3,17,17),map),"the labyrinth never overlaps buildings or leaves the map");
    var roofless=Labyrinth.Build(map,OpenArea(3,res.rect.CenterCell),11,false,0,null);
    if(roofless!=null){Check(roofless.rect.Cells.All(c=>!c.Roofed(map)),"the roof option can be switched off");foreach(var c in roofless.rect.Cells)foreach(var x in c.GetThingList(map).Where(x=>x.def.category==ThingCategory.Building).ToList())x.Destroy();}
   } finally {if(res!=null)foreach(var c in res.rect.Cells){foreach(var x in c.GetThingList(map).Where(x=>x.def.category==ThingCategory.Building).ToList())x.Destroy();map.roofGrid.SetRoof(c,null);}foreach(var kv in unroofed)map.roofGrid.SetRoof(kv.Key,kv.Value);}
  }
  // 迷宫布局：各种尺寸都只有一片连通的通道，中央 5×5 全开、外圈全是墙。
  void CheckLabyrinthLayout(){
   bool ok=true;string why="";
   for(int n=Labyrinth.MinSize;n<=Labyrinth.MaxSize;n+=2)for(int rep=0;rep<5;rep++){
    var open=Labyrinth.Layout(n);int c=n/2;int total=0;for(int x=0;x<n;x++)for(int z=0;z<n;z++)if(open[x,z])total++;
    var seen=new bool[n,n];var q=new Queue<IntVec2>();q.Enqueue(new IntVec2(1,1));seen[1,1]=true;int reach=0;
    while(q.Count>0){var cur=q.Dequeue();reach++;foreach(var d in new[]{new IntVec2(1,0),new IntVec2(-1,0),new IntVec2(0,1),new IntVec2(0,-1)}){int x=cur.x+d.x,z=cur.z+d.z;if(x<0 || z<0 || x>=n || z>=n || seen[x,z] || !open[x,z])continue;seen[x,z]=true;q.Enqueue(new IntVec2(x,z));}}
    bool room=true;for(int x=c-2;x<=c+2;x++)for(int z=c-2;z<=c+2;z++)room&=open[x,z];
    bool edge=true;for(int k=0;k<n;k++)edge&=!open[k,0] && !open[k,n-1] && !open[0,k] && !open[n-1,k];
    if(reach!=total || !room || !edge){ok=false;why="size "+n+": reach "+reach+"/"+total+" room "+room+" edge "+edge;}
   }
   Check(ok,"labyrinth layouts of every size are one connected maze around an open 5x5 room "+why);
  }

  void CheckGithyankiScenario(){
   const string s="LP_GithyankiScenario";
   var pawns=ScenarioStarters(s,new Dictionary<string,int>{{"LP_Githyanki",3}});
   CheckGoodwillParts(s,(Factions.ColonyDef,0));
   Check(!HasGoodwillPart(s,Factions.IllithidDef) && Factions.Illithid.HostileTo(Faction.OfPlayer),"the illithid colony stays hostile to the githyanki");
   var champ=Part<ScenPart_StarterKit>(s).Apply(pawns);
   Check(champ!=null && champ.equipment.Primary?.def.defName=="LP_SilverSword" && champ.equipment.Primary.TryGetComp<CompQuality>()?.Quality==QualityCategory.Excellent && champ.skills.GetSkill(SkillDefOf.Melee).Level==pawns.Max(p=>p.skills.GetSkill(SkillDefOf.Melee).Level),"the best githyanki swordsman wields an excellent silver sword");
   Check(pawns.Where(p=>p!=champ).All(p=>p.equipment.Primary?.def.defName=="MeleeWeapon_LongSword" && p.equipment.Primary.Stuff==ThingDefOf.Steel),"the other two carry steel longswords");
   Check(pawns.All(p=>p.apparel.WornApparel.Any(a=>a.def.defName=="Apparel_PlateArmor" && a.Stuff==ThingDefOf.Steel)),"every githyanki wears steel plate armor");
   var boon=Part<ScenPart_StarterBoon>(s);Check(boon.bestAt==null && boon.Apply(pawns).Count==3 && pawns.All(p=>p.GetPsylinkLevel()==1),"every githyanki has a level 1 psylink");
   Check(ScenThing(s,"MealSimple")==20 && ScenThing(s,"MedicineIndustrial")==10 && ScenThing(s,"Steel")==300,"githyanki supplies: 20 simple meals, 10 medicine, 300 steel");
   CheckTimedRaid(s,Factions.IllithidDef,8,8,DefDatabase<PawnsArrivalModeDef>.GetNamed("LP_Burrow"),"触须在地下蠕动");
  }

  // ---------- 存读档 ----------
  static int savedRaidTick,savedPactTick;
  // 1.63.0：-Only Scenarios 找的是 PrepareScenariosSave（组名带 s），原来读档后检查找不到排好的袭击。
  void PrepareScenariosSave()=>PrepareScenarioSave();
  void PrepareScenarioSave(){
   var ev=Tribute.Events;ev.scheduledRaids.Clear();
   savedRaidTick=Find.TickManager.TicksGame+654321;ev.scheduledRaids.Add(new ScheduledRaid{faction=Factions.IllithidDef,fireTick=savedRaidTick,pointsFactor=.6f,arrival=DefDatabase<PawnsArrivalModeDef>.GetNamed("LP_Burrow"),letterLabel="殖巢的追捕"});
   savedPactTick=Find.TickManager.TicksGame+777777;ev.oldPactFaction=Factions.HostileDef;ev.oldPactUntilTick=savedPactTick;
  }
  void CheckScenariosAfterLoad(){
   var ev=Tribute.Events;
   Check(ev.scheduledRaids.Count==1 && ev.scheduledRaids[0].fireTick==savedRaidTick && ev.scheduledRaids[0].faction==Factions.IllithidDef && Near(ev.scheduledRaids[0].pointsFactor,.6f) && ev.scheduledRaids[0].arrival?.defName=="LP_Burrow" && ev.scheduledRaids[0].letterLabel=="殖巢的追捕","a queued scenario raid survives save/load");
   Check(ev.oldPactFaction==Factions.HostileDef && ev.oldPactUntilTick==savedPactTick && Pact.OldPact(Factions.Hostile),"the old pact survives save/load");
   ev.scheduledRaids.Clear();ev.oldPactFaction=null;ev.oldPactUntilTick=-1;Pact.Recalculate();
  }
 }
}
