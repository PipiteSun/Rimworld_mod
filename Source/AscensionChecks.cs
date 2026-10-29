using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using UnityEngine;
namespace LichPhylactery {
 // 1.40.0 成神检查（Test.ps1 -Only Ascension），只做需求第六节点名的 10 项：1、7、8、9、12、15、16、19、21、24（存读档）。
 public partial class LichChecks {
  readonly List<Thing> aMade=new List<Thing>();
  static string aSavePawn,aSaveDemigod,aSaveShrine;static int aSaveStage,aSaveSouls,aSaveLevel;static float aSavePower;

  Pawn APawn(){var p=Person();if(p.Spawned)p.DeSpawn();p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);aMade.Add(p);foreach(var t in p.story.traits.allTraits.Where(t=>t.def==Faith.Devotee || t.def==Faith.Atheist).ToList())p.story.traits.RemoveTrait(t);return p;}
  Building_FaithAltar AAltar(){var a=(Building_FaithAltar)ThingMaker.MakeThing(Faith.AltarDef,ThingDefOf.WoodLog);a.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(a,OpenCell(),map);aMade.Add(a);return a;}
  static void AChosen(Pawn p,LP_DeityDef g){var h=(Hediff_Chosen)HediffMaker.MakeHediff(Faith.ChosenDef,p);h.deity=g;p.health.AddHediff(h);}
  Thing ASpark(Pawn owner,LP_DivinePortfolioDef pf,string path,bool usurped=false){var s=Ascension.MakeSpark(owner,pf,path,usurped);GenPlace.TryPlaceThing(s,owner.Position,map,ThingPlaceMode.Near);aMade.Add(s);return s;}
  static void AReset(){
   var t=Ascension.Tracker;t.progress.Clear();t.demigods.Clear();t.ascended.Clear();t.blessings.Clear();t.firstAscendTick=-1;t.shrines.Clear();t.usurped.Clear();t.kelemvorFixed.Clear();t.restUntil.Clear();t.zones.Clear();t.githBetrayed=false;t.fathers.Clear();
   Demigods.RefreshCache();
  }

  void CheckAscension(){
   var before=new HashSet<Thing>(map.listerThings.AllThings);var s=LichMod.Settings;bool on=s.ascension;int cap=s.demigodCap,diff=s.ascensionDifficulty;bool faith=s.faithSystem;
   bool researched=Ascension.Research.IsFinished;
   try{
    s.ascension=true;s.demigodCap=1;s.faithSystem=true;AReset();
    AscResearchGate();AscQuotaAndRite();AscAgeless();AscLichLevel();AscShrine();AscTitan();AscUsurper();AscCouncil();
    AscScenarios();
   }finally{
    s.ascension=on;s.demigodCap=cap;s.ascensionDifficulty=diff;s.faithSystem=faith;
    foreach(var t in aMade)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    aMade.Clear();
    foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList())if(!t.Destroyed)t.Destroy();
    foreach(var c in map.gameConditionManager.ActiveConditions.Where(c=>c.def==Ascension.TremorDef || c.def==Ascension.WrathDef).ToList())c.End();
    AReset();
    if(researched && !Ascension.Research.IsFinished)Find.ResearchManager.FinishProject(Ascension.Research,false,null,false);
   }
  }

  // 1 研究前没有“成神之路”按钮，研究后符合条件者有（神选者 + 神眷≥60），不符合的人没有。
  void AscResearchGate(){
   var lath=Faith.God("Lathander");var p=APawn();AChosen(p,lath);Faith.Comp.favor[lath]=70f;var plain=APawn();
   Find.ResearchManager.ResetAllProgress();
   bool before=Ascension.PathGizmo(p)==null && !Ascension.Researched && Ascension.CannotStart(p,Ascension.Saint)!=null;
   Find.ResearchManager.FinishProject(Ascension.Research,false,null,false);
   bool after=Ascension.PathGizmo(p)!=null && Ascension.CannotStart(p,Ascension.Saint)==null && Ascension.PathGizmo(plain)==null;
   Faith.Comp.favor[lath]=50f;bool lowFavor=Ascension.PathGizmo(p)==null;
   var r=Ascension.Research;
   Check(before && after && lowFavor && r.baseCost==3000f && r.techLevel==TechLevel.Medieval && r.prerequisites.Contains(Faith.Research) && r.tab==DefDatabase<ResearchTabDef>.GetNamed(ResearchTab.Tab),"no 成神之路 button before 神格学 (3000, medieval, after 信仰与神术); afterwards a chosen with favor 70 gets it, a plain colonist and favor 50 do not");
  }

  // 7 名额：已有 1 名半神时第二名的仪式被挡，名额改 2 放行；8 仪式完成 → 1 级半神、神职对、2 个能力。
  void AscQuotaAndRite(){
   AReset();var lath=Faith.God("Lathander");Faith.Comp.favor[lath]=95f;AAltar();
   var first=APawn();Demigods.Grant(first,Ascension.Portfolio("Storm"),Ascension.Saint);
   var saint=APawn();AChosen(saint,lath);
   var pr=Ascension.Start(saint,Ascension.Saint);pr.stage=AscensionPaths.SaintRite;pr.stageStartTick=Find.TickManager.TicksGame;
   var spark=ASpark(saint,Ascension.PortfolioOf(lath),Ascension.Saint);
   string blocked=Ascension.RiteBlocked(saint);
   LichMod.Settings.demigodCap=2;string allowed=Ascension.RiteBlocked(saint);
   Check(Ascension.DemigodCount==1 && blocked!=null && blocked.Contains("名额") && allowed==null,"quota: with one demigod the second rite is blocked ("+blocked+"), cap 2 lets it through");
   bool ok=Ascension.CompleteRite(saint);
   var h=Demigods.Of(saint);var pf=Ascension.PortfolioOf(lath);
   Check(ok && h!=null && h.Level==1 && h.Portfolio==pf && pf?.key=="Dawn" && pf.abilities.Count==2 && pf.abilities.All(a=>saint.abilities.GetAbility(a)!=null) && spark.Destroyed && Ascension.Tracker.Of(saint)==null && Ascension.DemigodCount==2,"finished rite: LP_Demigod level 1, portfolio 黎明 from Lathander, both abilities granted, spark consumed, progress closed");
   LichMod.Settings.demigodCap=1;
  }

  // 9 半神推进 1 年生理年龄不变。
  void AscAgeless(){
   var p=APawn();Demigods.Grant(p,Ascension.Portfolio("Mind"),Ascension.Saint);
   long bio=p.ageTracker.AgeBiologicalTicks;p.ageTracker.AgeTickInterval(GenDate.TicksPerYear);
   var q=APawn();long qb=q.ageTracker.AgeBiologicalTicks;q.ageTracker.AgeTickInterval(GenDate.TicksPerYear);
   Check(p.ageTracker.AgeBiologicalTicks==bio && q.ageTracker.AgeBiologicalTicks>qb,"a demigod's biological age does not move over a year (an ordinary colonist's does)");
  }

  // 12 命匣 19 级不能开半神巫妖路，20 级可以。
  void AscLichLevel(){
   var p=APawn();var box=Rituals.Ascend(p);aMade.Add(box);GenPlace.TryPlaceThing(box,p.Position,map,ThingPlaceMode.Near);
   box.level=19;string no=Ascension.CannotStart(p,Ascension.Lich);
   box.level=20;string yes=Ascension.CannotStart(p,Ascension.Lich);
   Check(no!=null && yes==null,"lich path: phylactery level 19 refused ("+no+"), level 20 allowed");
  }

  // 15 神龛：复生 1 天；神力 ≥ 50 时立即复生并扣 50。
  void AscShrine(){
   var p=APawn();var box=Rituals.Ascend(p);aMade.Add(box);GenPlace.TryPlaceThing(box,p.Position,map,ThingPlaceMode.Near);
   var h=Demigods.Grant(p,Ascension.Portfolio("Undeath"),Ascension.Lich);Ascension.Tracker.shrines.Add(box.ThingID);
   float baseHp=box.def.GetStatValueAbstract(StatDefOf.MaxHitPoints);
   h.Power=0f;int now=Find.TickManager.TicksGame;p.Kill(null);box.ProcessRevival();
   bool oneDay=box.revivalTick==now+GenDate.TicksPerDay;
   Find.TickManager.DebugSetTicksGame(box.revivalTick);box.ProcessRevival();
   bool back=!p.Dead && p.Spawned;
   h=Demigods.Of(p);h.Power=60f;now=Find.TickManager.TicksGame;p.Kill(null);box.ProcessRevival();
   bool instant=!p.Dead && Near(Demigods.Of(p).Power,10f);
   Check(oneDay && back && instant && Near(box.MaxHitPoints,baseHp*2f) && AscensionPaths.IsShrine(box),"shrine: revival in 1 day (not 3); with ≥50 divine power the lich returns at once and pays 50 (60 → "+Demigods.Of(p)?.Power+"); phylactery HP ×2 [oneDay="+oneDay+" back="+back+" instant="+instant+" hp="+box.MaxHitPoints+"/"+baseHp+"]");
  }

  // 16 泰坦觉醒：换型 LP_AwakenedTitan、其他基因保留、孩子不继承 LP_DivineBlood。
  void AscTitan(){
   var p=RacePawn("LP_Titan");if(p.Spawned)p.DeSpawn();GenSpawn.Spawn(p,OpenCell(),map);aMade.Add(p);
   var extra=DefDatabase<GeneDef>.GetNamed("DarkVision");p.genes.AddGene(extra,false);
   var genesBefore=p.genes.GenesListForReading.Select(g=>g.def).ToList();
   bool start=Ascension.CannotStart(p,Ascension.Titan)==null;
   AscensionPaths.Awaken(p);
   var after=p.genes.GenesListForReading.Select(g=>g.def).ToList();
   var mother=APawn();var kid=PregnancyUtility.GetInheritedGenes(p,mother);
   Check(start && p.genes.Xenotype==AscensionPaths.AwakenedTitan && genesBefore.All(after.Contains) && after.Contains(AscensionPaths.DivineBlood) && after.Contains(extra) && !kid.Contains(AscensionPaths.DivineBlood) && !AscensionPaths.IsPlainTitan(p),"titan awakening: xenotype LP_AwakenedTitan, all "+genesBefore.Count+" previous genes kept (incl. an extra endogene), LP_DivineBlood added and not inherited by children");
  }

  // 19 篡火者成功率公式与 95% 上限；失败：主祭死亡（尸体完整）+ 5 片神迹残片。
  void AscUsurper(){
   AReset();var p=APawn();AAltar();
   float s=p.GetStatValue(StatDefOf.PsychicSensitivity);
   float c0=AscensionPaths.UsurperChance(p,0),c3=AscensionPaths.UsurperChance(p,3),c9=AscensionPaths.UsurperChance(p,9);
   bool formula=Near(c0,Mathf.Clamp(.5f+(s-1f)*.5f,0f,.95f)) && Near(c3,Mathf.Clamp(.5f+(s-1f)*.5f+.24f,0f,.95f)) && Near(c9,Mathf.Clamp(.5f+(s-1f)*.5f+.4f,0f,.95f));
   p.story.traits.GainTrait(new Trait(AscensionPaths.GodslayerTrait,0,true));
   bool slayer=Near(AscensionPaths.UsurperChance(p,0),Mathf.Clamp(.5f+(s-1f)*.5f+.1f,0f,.95f)) && Near(AscensionPaths.UsurperChance(p,5),.95f);
   var bane=Faith.God("Bane");var pr=new AscensionProgress{pawn=p,path=Ascension.Usurper,stage=AscensionPaths.UsurperRite,stageStartTick=Find.TickManager.TicksGame,patronGod=bane,portfolio=Ascension.PortfolioOf(bane)};Ascension.Tracker.progress.Add(pr);
   var spark=ASpark(p,pr.portfolio,Ascension.Usurper,true);
   int shards=Faith.ShardsOn(map);bool can=Ascension.RiteBlocked(p)==null;
   bool ok=Ascension.CompleteRite(p,0,.99f);
   Check(formula && slayer && can && !ok && p.Dead && p.Corpse!=null && spark.Destroyed && Faith.ShardsOn(map)==shards+5 && Ascension.Tracker.Of(p)==null && Demigods.Of(p)==null,"usurper: chance = 50% + (sensitivity-1)×50% + 8%/shard (max 5) + 10% godslayer, capped at 95%; a failed rite kills the celebrant (corpse intact) and leaves 5 miracle shards");
  }

  // 21 神灵议会：倒地过 → 神力清零、等级不降、30 天冷却；没倒地 → 等级 +1。
  void AscCouncil(){
   var p=APawn();var h=Demigods.Grant(p,Ascension.Portfolio("Justice"),Ascension.Saint);
   h.Comp.level=2;h.Power=h.MaxPower;h.Comp.lastTrialTick=Find.TickManager.TicksGame-31*GenDate.TicksPerDay;
   bool ready=Demigods.TrialBlocked(h)==null;
   Demigods.StartTrial(h);bool active=h.Comp.trialActive && h.Comp.trialRaidTick==Find.TickManager.TicksGame+GenDate.TicksPerDay;
   Demigods.ResolveTrial(h,true);
   bool failed=h.Level==2 && Near(h.Power,0f) && !h.Comp.trialActive && Demigods.TrialBlocked(h)!=null;
   h.Power=h.MaxPower;Demigods.ResolveTrial(h,false);
   Check(ready && active && failed && h.Level==3 && Near(h.MaxPower,200f) && Near(h.Strength,1.5f),"council: downed during the trial → power 0, level stays 2, cooldown; not downed → level 3 (max power 200, strength ×1.5)");
  }

  // ==================== 1.41.0 成神开局剧本（5 条） ====================
  // 每个剧本生成一次：剧本里的组件按顺序作用在测试小人上；开局研究跑剧本自己的 ScenPart_StartingResearch，开局物品放到地图上数。
  static T APart<T>(ScenarioDef def) where T:ScenPart=>(T)def.scenario.AllParts.OfType<T>().First().CopyForEditing();
  static bool AScenText(ScenarioDef def){string text=null;try{text=def.scenario.GetFullInformationText();}catch{}return text!=null && !text.Contains("Cannot read data") && def.description.Contains("推荐路线") && def.description.Contains("难度");}
  static bool AResearch(ScenarioDef def,params string[] names){
   Find.ResearchManager.ResetAllProgress();
   foreach(var part in def.scenario.AllParts.OfType<ScenPart_StartingResearch>())part.PostGameStart();
   return names.All(n=>DefDatabase<ResearchProjectDef>.GetNamed(n).IsFinished);
  }
  void AItems(ScenarioDef def){foreach(var part in def.scenario.AllParts.OfType<ScenPart_StartingThing_Defined>())foreach(var t in part.PlayerStartingThings().ToList())GenPlace.TryPlaceThing(t,OpenCell(),map,ThingPlaceMode.Near);}
  bool AHas(string def,int n,QualityCategory? q=null)=>map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed(def)).Where(t=>q==null || t.TryGetComp<CompQuality>()?.Quality==q).Sum(t=>t.stackCount)>=n;
  static string ALastLetter(out string label){var l=Find.LetterStack.LettersListForReading.LastOrDefault();label=l?.Label.ToString()??"";return (l as ChoiceLetter)?.Text.ToString()??"";}

  void AscScenarios(){
   var faith=Faith.Comp;var oldPatron=faith.patron;var oldFavor=new Dictionary<LP_DeityDef,float>(faith.favor);int oldCooldown=faith.patronCooldownUntil;
   var st=NetherilQuest.State;string oldStage=st?.stage;int oldEnvoy=st?.envoyTick??-1;
   var tw=Twilight.World;int oldTwilight=tw?.startTick??-1;bool researched=Ascension.Research.IsFinished;
   try{
    AscScenNetheril();AscScenDoom();AscScenPilgrim();AscScenTwilight();AscScenEmpyrean();
   }finally{
    faith.patron=oldPatron;faith.favor.Clear();foreach(var kv in oldFavor)faith.favor[kv.Key]=kv.Value;faith.patronCooldownUntil=oldCooldown;
    if(st!=null){st.stage=oldStage;st.envoyTick=oldEnvoy;}
    if(tw!=null)tw.startTick=oldTwilight;Twilight.End();
    if(researched && !Ascension.Research.IsFinished)Find.ResearchManager.FinishProject(Ascension.Research,false,null,false);
    AReset();
   }
  }
  // 耐瑟遗徒：法师心灵链接 4（VPE 8）、智识≥10、回响 2 层；拉洛克好感 0；第 10 天使者；研究；物品。
  void AscScenNetheril(){
   AReset();var def=DefDatabase<ScenarioDef>.GetNamed("LP_NetherilHeirScenario");var part=APart<ScenPart_NetherilHeir>(def);
   var pawns=new List<Pawn>{APawn(),APawn(),APawn()};int now=Find.TickManager.TicksGame;
   var st=NetherilQuest.State;st.stage="idle";st.envoyTick=-1;
   var mage=part.Apply(pawns);
   bool psy=NetherilHooks.VPEActive?NetherilHooks.Level(mage)>=8:(!ModsConfig.RoyaltyActive || mage.GetPsylinkLevel()==4);
   var echo=mage.health.hediffSet.hediffs.OfType<Hediff_KarsusEcho>().FirstOrDefault();var larloch=NetherilQuest.Larloch;
   bool research=AResearch(def,"LP_Research_NetherilArcana","LP_Research_Theogony");AItems(def);
   bool items=AHas("LP_NetherilStudyScroll",2,QualityCategory.Excellent) && AHas("LP_NetherilArcaneScroll",1) && AHas("Gold",100) && AHas("LP_Mithral",50);
   Check(AScenText(def) && psy && mage.skills.GetSkill(SkillDefOf.Intellectual).Level>=10 && echo?.Layers==2 && larloch!=null && larloch.PlayerGoodwill==0 && st.stage=="scheduled" && st.envoyTick==now+10*GenDate.TicksPerDay && research && items,"scenario 耐瑟遗徒: mage psylink "+mage.GetPsylinkLevel()+(NetherilHooks.VPEActive?" (VPE "+NetherilHooks.Level(mage)+")":"")+", intellect ≥10, 2 layers of Karsus echo, Larloch goodwill 0, Larloch's envoy fixed on day 10, Netheril arcana + theogony researched, scrolls/gold/mithral on the map");
  }
  // 末日将至：命匣 20 级、三具骷髅、仪式台；半神巫妖路万魂 10/20，凯兰沃 -50；第 5 天先遣队不算试炼波数。
  void AscScenDoom(){
   AReset();var def=DefDatabase<ScenarioDef>.GetNamed("LP_DoomLichScenario");var lichPart=APart<ScenPart_LichAscension>(def);var part=APart<ScenPart_DoomLich>(def);
   var kel=Faith.God("Kelemvor");Faith.Comp.favor[kel]=0f;int altars=map.listerThings.ThingsOfDef(Necro.Def<ThingDef>("Altar")).Count;
   var lich=APawn();var mortal=APawn();int now=Find.TickManager.TicksGame;
   var box=ScenPart_LichAscension.Apply(lich,lichPart.level,lichPart.skeletons,lichPart.golems);aMade.Add(box);
   var pr=part.Apply(new List<Pawn>{lich,mortal});
   bool lichOk=Necro.Lich(lich)!=null && box.level==20 && LichSpells.UnlockedLevel(box)==20 && Necro.LivingConstructs(lich).Count(c=>Necro.Construct(c).kind=="Skeleton")==3 && map.listerThings.ThingsOfDef(Necro.Def<ThingDef>("Altar")).Count==altars+1;
   bool path=pr!=null && pr.path==Ascension.Lich && pr.stage==0 && pr.Get("souls")==10 && AscensionPaths.StageProgress(pr)==.5f && Near(FaithUtility.GetFavor(kel),-50f) && Ascension.PathGizmo(lich)!=null;
   bool timer=part.fireTick==now+5*GenDate.TicksPerDay;
   var raid=part.Fire(map);bool raidOk=raid!=null && raid.Count>=2 && raid.All(p=>p.Spawned && p.Faction?.def.defName=="LP_KelemvorChurch") && pr.wave==0 && pr.stage==0;
   foreach(var p in raid??new List<Pawn>())aMade.Add(p);
   bool research=AResearch(def,"LP_Research_Theogony");AItems(def);
   Check(AScenText(def) && lichOk && path && timer && raidOk && research && AHas("Plasteel",50),"scenario 末日将至: lich with a level-20 phylactery, 3 skeletons and a ritual table; lich path open at 万魂 10/20, Kelemvor favor -50; day-5 doomguard vanguard ("+raid?.Count+" doomguides) does not count as a trial wave; theogony researched, plasteel on the map");
  }
  // 朝圣之路：三人同一位神的信徒，首领神选者；守护神 = 该神、神眷 60；圣徒之路试炼一；小型化圣坛和其余物品。
  void AscScenPilgrim(){
   AReset();var def=DefDatabase<ScenarioDef>.GetNamed("LP_PilgrimageScenario");var part=APart<ScenPart_Pilgrimage>(def);
   var pawns=new List<Pawn>{APawn(),APawn(),APawn()};
   foreach(var p in pawns)part.Notify_PawnGenerated(p,PawnGenerationContext.PlayerStarter,false);
   bool rolled=pawns.All(p=>FaithUtility.DevoteeGod(p)!=null);var god=FaithUtility.DevoteeGod(pawns[0]);
   if(god==null || !god.worshippable){god=FaithUtility.Worshippable.First();ScenPart_Pilgrimage.Convert(pawns[0],god);}
   var pr=part.Apply(pawns);
   bool faithOk=pawns.All(p=>FaithUtility.IsDevoteeOf(p,god)) && FaithUtility.IsChosen(pawns[0],out var cg) && cg==god && !FaithUtility.IsChosen(pawns[1],out _) && FaithUtility.Patron==god && Near(FaithUtility.GetFavor(god),60f);
   bool path=pr!=null && pr.path==Ascension.Saint && pr.stage==0 && pr.patronGod==god && AscensionPaths.StageLabel(pr).StartsWith("试炼一") && Ascension.PathGizmo(pawns[0])!=null && AscensionPaths.Buttons(pr).Any(b=>b.label=="放弃这条路");
   bool altar=map.listerThings.ThingsInGroup(ThingRequestGroup.MinifiedThing).OfType<MinifiedThing>().Any(m=>m.InnerThing?.def==Faith.AltarDef);
   bool research=AResearch(def,"LP_Research_Faith","LP_Research_Theogony");AItems(def);
   Check(AScenText(def) && rolled && faithOk && path && altar && research && AHas("LP_HolySymbol",1) && AHas("LP_BlessedWater",5) && AHas("LP_MiracleShard",5),"scenario 朝圣之路: all three are devotees of "+god.label+", the first is its chosen, patron "+god.label+" at favor 60, saint path at 试炼一 (window shows it, can be abandoned), a minified altar, holy symbol, 5 blessed water and 5 miracle shards on the map, faith + theogony researched");
  }
  // 黄昏猎神者：老兵射击或格斗≥12、心灵敏感度≥110%；第 3 天黄昏之年（第 1 年也开始）。
  void AscScenTwilight(){
   AReset();Twilight.End();var def=DefDatabase<ScenarioDef>.GetNamed("LP_TwilightHunterScenario");var part=APart<ScenPart_TwilightHunter>(def);
   var pawns=new List<Pawn>{APawn(),APawn(),APawn(),APawn()};var w=Twilight.World;w.startTick=-1;int now=Find.TickManager.TicksGame;
   var vet=part.Apply(pawns);
   int best=Math.Max(vet.skills.GetSkill(SkillDefOf.Shooting).Level,vet.skills.GetSkill(SkillDefOf.Melee).Level);float sens=vet.GetStatValue(StatDefOf.PsychicSensitivity,true,0);
   bool timer=w.startTick==now+3*GenDate.TicksPerDay;int year=GenDate.YearsPassed;
   int tick=w.startTick;tick+=((311-tick%GenDate.TicksPerHour)+GenDate.TicksPerHour)%GenDate.TicksPerHour;Find.TickManager.DebugSetTicksGame(tick);w.WorldComponentTick();
   bool started=Twilight.Active;Twilight.End();
   bool research=AResearch(def,"LP_Research_Theogony");AItems(def);
   Check(AScenText(def) && best>=12 && sens>=1.1f-.001f && timer && started && research && AHas("LP_MiracleShard",5) && AHas("MeleeWeapon_LongSword",1,QualityCategory.Excellent) && AHas("Gun_AssaultRifle",1,QualityCategory.Excellent),"scenario 黄昏猎神者: veteran shooting/melee "+best+" (≥12), psychic sensitivity "+sens.ToStringPercent()+" (≥110%); twilight year fixed on day 3 and starts in year "+year+"; theogony researched, 5 shards, excellent longsword and assault rifle on the map");
  }
  // 坠落的神裔：父神存进追踪组件，泰坦觉醒路已开、父神就是这位，信件写出名字；放弃后重开仍是同一位父神。
  void AscScenEmpyrean(){
   AReset();var def=DefDatabase<ScenarioDef>.GetNamed("LP_FallenEmpyrean");var part=APart<ScenPart_FallenEmpyrean>(def);
   var titan=RacePawn("LP_Titan");aMade.Add(titan);var pawns=new List<Pawn>{titan,APawn(),APawn()};
   var t=part.Apply(pawns);var pr=ScenPart_FallenEmpyrean.Awaken(t);
   var father=Ascension.Tracker.FatherOf(titan);string text=ALastLetter(out var label);
   bool stored=father!=null && AscensionPaths.FatherPool.Contains(father) && pr!=null && pr.path==Ascension.Titan && pr.stage==0 && pr.patronGod==father && pr.portfolio==Ascension.PortfolioOf(father) && Ascension.PathGizmo(titan)!=null;
   bool letter=label.Contains(father?.label??"?") && text.Contains(father?.FullLabel??"?");
   AscensionPaths.Clear(pr,null);var again=Ascension.Start(titan,Ascension.Titan,false);
   bool research=AResearch(def,"LP_Research_Theogony");
   Check(AScenText(def) && stored && letter && again?.patronGod==father && research,"scenario 坠落的神裔: father "+father?.label+" stored at game start, titan path open with that father, the start letter names him ("+label+"), reopening the path keeps the same father, theogony researched");
  }

  // 24 存读档：进度、神力、等级、神龛标记、神域赐福。
  void PrepareAscensionSave(){
   AReset();var t=Ascension.Tracker;
   var lich=Person();GenSpawn.Spawn(lich,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);var box=Rituals.Ascend(lich);GenPlace.TryPlaceThing(box,lich.Position,map,ThingPlaceMode.Near);box.level=25;
   var pr=Ascension.Start(lich,Ascension.Lich);pr.Add("souls",7);
   var god=Person();GenSpawn.Spawn(god,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);
   var h=Demigods.Grant(god,Ascension.Portfolio("Fire"),Ascension.Saint);h.Comp.level=3;h.Power=77f;
   t.shrines.Add(box.ThingID);t.blessings.Add(Ascension.Portfolio("Dawn"));
   aSavePawn=lich.ThingID;aSaveDemigod=god.ThingID;aSaveShrine=box.ThingID;aSaveStage=pr.stage;aSaveSouls=pr.Get("souls");aSaveLevel=3;aSavePower=77f;
  }
  void CheckAscensionAfterLoad(){
   var t=Ascension.Tracker;
   var pr=t.progress.FirstOrDefault(x=>x.pawn?.ThingID==aSavePawn);
   var god=t.demigods.FirstOrDefault(x=>x?.ThingID==aSaveDemigod);var h=Demigods.Of(god);
   var box=Necro.Registry.boxes.FirstOrDefault(b=>b.ThingID==aSaveShrine);
   Check(pr!=null && pr.path==Ascension.Lich && pr.stage==aSaveStage && pr.Get("souls")==aSaveSouls && pr.box==box && h!=null && h.Level==aSaveLevel && Near(h.Power,aSavePower) && h.Portfolio?.key=="Fire" && god.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_DA_Meteor"))!=null && box!=null && AscensionPaths.IsShrine(box) && Demigods.Blessed("Dawn"),"ascension save/reload keeps the path progress (lich, 7 souls), demigod level 3 / power 77 / 火焰 abilities, the shrine mark and the dawn blessing");
   AReset();
  }
 }
}
