using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using UnityEngine;
namespace LichPhylactery {
 // 1.39.0 信仰检查（Test.ps1 -Only Faith），按需求第十一节挑最关键的 15 条 + 1 条存读档。
 public partial class LichChecks {
  readonly List<Thing> fMade=new List<Thing>();
  static Dictionary<string,float> fSaveFavor;static string fSavePatron;static int fSaveCooldown;
  Building_FaithAltar FAltar(){var a=(Building_FaithAltar)ThingMaker.MakeThing(Faith.AltarDef,ThingDefOf.WoodLog);a.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(a,OpenCell(),map);fMade.Add(a);return a;}
  Pawn FPawn(Faction f=null){var p=EvPawn(null,f??Faction.OfPlayer);fMade.Add(p);foreach(var t in p.story.traits.allTraits.Where(t=>t.def==Faith.Devotee || t.def==Faith.Atheist).ToList())p.story.traits.RemoveTrait(t);return p;}
  static void FDevotee(Pawn p,LP_DeityDef g){foreach(var t in p.story.traits.allTraits.Where(t=>t.def==Faith.Devotee || t.def==Faith.Atheist).ToList())p.story.traits.RemoveTrait(t);if(g!=null)p.story.traits.GainTrait(new Trait(Faith.Devotee,g.devoteeDegree,true));}
  static void FReset(){var c=Faith.Comp;c.favor.Clear();c.patron=null;c.patronCooldownUntil=-1;c.history.Clear();c.prayDay=-1;c.prayToday=0;c.manifestGod=null;c.manifestUntil=-1;c.trialGod=null;}
  static void FSet(LP_DeityDef g,float v){Faith.Comp.favor[g]=v;}

  void CheckFaith(){
   var before=new HashSet<Thing>(map.listerThings.AllThings);var s=LichMod.Settings;bool sys=s.faithSystem,man=s.faithManifest;int fav=s.faithFavorPercent,quest=s.faithQuestPercent;
   Faith.TestSuppressed=false;
   try{FaithDefs();FaithFavor();FaithAltarAndPrayer();FaithMiraclesCheck();FaithWater();FaithManifestCheck();FaithPilgrimsCheck();FaithChosen();FaithRacesCheck();FaithSettingsOff();}
   finally{
    s.faithSystem=sys;s.faithManifest=man;s.faithFavorPercent=fav;s.faithQuestPercent=quest;Faith.TestSuppressed=true;Faith.ApplySettings();
    foreach(var l in map.lordManager.lords.Where(l=>l.LordJob is LordJob_Pilgrims).ToList())map.lordManager.RemoveLord(l);
    foreach(var t in fMade)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    fMade.Clear();
    foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList())if(!t.Destroyed)t.Destroy();
    foreach(var c in map.gameConditionManager.ActiveConditions.Where(c=>c.def==Faith.ManifestDef).ToList())c.End();
    var mc=map.GetComponent<MapComponent_Faith>();mc?.zones.Clear();
    FReset();
   }
  }

  // 1 神祇定义完整；2 研究在 DND 页签；新定义没有配置错误、贴图都能读到。
  void FaithDefs(){
   var all=FaithUtility.AllDeities;string[] order={"Lathander","Tyr","Mystra","Kelemvor","Malar","Talos","Bane","Shar","Surtr","Ilsvi","Vlaakith"};
   bool ok=all.Count==11 && Enumerable.Range(1,11).All(i=>FaithUtility.DegreeToDeity(i)?.key==order[i-1] && FaithUtility.DeityToDegree(FaithUtility.DegreeToDeity(i))==i)
    && FaithUtility.Worshippable.Count()==9 && !Faith.God("Ilsvi").worshippable && !Faith.God("Vlaakith").worshippable
    && FaithUtility.Worshippable.All(d=>d.domainAbility!=null && d.relic!=null && d.BlessingWorker!=null && d.CurseWorker!=null && (d.LikedThings.Count+d.LikedCategories.Count)>0 && d.Icon!=BaseContent.BadTex)
    && Twilight.Deities.Count==8 && Faith.God("Shar").EnemyOf(Faith.God("Lathander")) && Faith.God("Lathander").Enemies.Count()==3 && Faith.God("Kelemvor").alignment==DeityAlignment.Neutral
    && Faith.Devotee.degreeDatas.Count==11 && Faith.Devotee.DataAtDegree(1).label=="信徒（洛山达）" && Faith.Devotee.DataAtDegree(10).commonality==0f;
   Check(ok,"11 deities: devotee degrees 1-11 in spec order, 9 worshippable with domain/relic/blessing/offerings/icon, old 8 stay the twilight gods ("+string.Join(",",all.Select(d=>d.defName+"="+d.devoteeDegree))+")");
   var defs=new List<Def>();
   defs.AddRange(DefDatabase<ThingDef>.AllDefs.Where(d=>d.defName=="LP_FaithAltar" || d.defName=="LP_HolySymbol" || d.defName=="LP_BlessedWater" || d.defName=="LP_Proj_BlessedWater" || d.defName.StartsWith("LP_Relic_")));
   defs.AddRange(DefDatabase<RecipeDef>.AllDefs.Where(r=>r.defName.StartsWith("LP_Offer_") || r.ProducedThingDef==Faith.SymbolDef || r.ProducedThingDef==Faith.WaterDef));
   defs.AddRange(Miracles.All);defs.AddRange(all);
   defs.AddRange(DefDatabase<GeneDef>.AllDefs.Where(g=>g.defName.StartsWith("LP_Gene_") || g.defName.Contains("Aasimar")));
   defs.AddRange(DefDatabase<AbilityDef>.AllDefs.Where(a=>new[]{"LP_HealingHands","LP_RadiantSoulLeap","LP_NecroticShroud","LP_InfernalPact"}.Contains(a.defName)));
   defs.AddRange(DefDatabase<IncidentDef>.AllDefs.Where(i=>i.defName.StartsWith("LP_Faith_")));defs.Add(FaithRaces.Aasimar);defs.Add(FaithRaces.Tiefling);defs.Add(Faith.Devotee);defs.Add(Faith.Atheist);defs.Add(Faith.ChosenDef);
   var errors=defs.SelectMany(d=>d.ConfigErrors().Select(e=>d.defName+": "+e)).ToList();
   var tex=new List<string>{"Faith/HolySymbol","Faith/BlessedWater","Faith/Altar_south","Faith/Altar_east","Faith/Halo/Halo_south","Faith/Xenotypes/Aasimar","Faith/Xenotypes/Tiefling"};
   tex.AddRange(Miracles.All.Select(a=>a.iconPath));tex.AddRange(all.Select(d=>d.iconPath));
   var missing=tex.Where(t=>ContentFinder<Texture2D>.Get(t,false)==null).ToList();
   Check(defs.Count>=50 && errors.Count==0 && missing.Count==0 && Miracles.All.Count==12,"all "+defs.Count+" new faith defs pass ConfigErrors() and "+tex.Count+" textures load"+(errors.Count==0?"":": "+string.Join("; ",errors))+(missing.Count==0?"":"; missing "+string.Join(",",missing)));
   var rf=Faith.Research;var rp=Faith.Planetouched;var tab=DefDatabase<ResearchTabDef>.GetNamed(ResearchTab.Tab);
   Check(rf!=null && rp!=null && rf.tab==tab && rp.tab==tab && Near(rf.baseCost,800f) && Near(rp.baseCost,1200f) && (rf.prerequisites==null || rf.prerequisites.Count==0) && rp.prerequisites.Contains(rf) && Near(rf.ResearchViewX,0f) && Near(rp.ResearchViewX,1f) && Near(rf.ResearchViewY,2.8f) && Near(rp.ResearchViewY,2.8f)
    && Faith.AltarDef.researchPrerequisites.Contains(rf) && DefDatabase<RecipeDef>.AllDefs.Where(r=>r.ProducedThingDef==Faith.SymbolDef || r.ProducedThingDef==Faith.WaterDef).All(r=>r.researchPrerequisite==rf),"research 信仰与神术 (800, X0 Y2.8) and 天界与地狱血统 (1200, X1 Y2.8) sit on the DND tab and gate the altar, symbol and water");
  }

  // 3 AddFavor 夹值 + FavorChanged + 历史；4 选/换守护神。
  void FaithFavor(){
   FReset();var lath=Faith.God("Lathander");var tyr=Faith.God("Tyr");var mys=Faith.God("Mystra");
   var seen=new List<(LP_DeityDef,float,string)>();Action<LP_DeityDef,float,string> hook=(g,a,r)=>seen.Add((g,a,r));FaithUtility.FavorChanged+=hook;
   try{FaithUtility.AddFavor(lath,500f,"测试");FaithUtility.AddFavor(tyr,-300f,"测试");}finally{FaithUtility.FavorChanged-=hook;}
   Check(Near(FaithUtility.GetFavor(lath),100f) && Near(FaithUtility.GetFavor(tyr),-100f) && seen.Count==2 && Near(seen[0].Item2,100f) && seen[0].Item3=="测试" && Faith.Comp.history.Count==2,"AddFavor clamps to -100..100, raises FavorChanged with the real delta and logs history");
   FReset();FSet(lath,40f);
   FaithUtility.SetPatron(lath);bool first=FaithUtility.Patron==lath && Near(FaithUtility.GetFavor(lath),40f);
   FaithUtility.SetPatron(tyr);bool changed=FaithUtility.Patron==tyr && Near(FaithUtility.GetFavor(lath),10f) && Faith.Comp.patronCooldownUntil>Faith.Now+14*GenDate.TicksPerDay;
   FaithUtility.SetPatron(mys);bool blocked=FaithUtility.Patron==tyr && FaithUtility.CannotSetPatron(mys)!=null;
   FaithUtility.SetPatron(mys,true);bool forced=FaithUtility.Patron==mys;
   Check(first && changed && blocked && forced && FaithUtility.CannotSetPatron(Faith.God("Ilsvi"))!=null,"patron: first pick is free, changing costs the old god 30 and starts a 15-day cooldown, ignoreCooldown bypasses it, Ilsvi cannot be chosen");
  }

  // 5 圣坛与祈祷：娱乐会挑祈祷；+0.5，信徒 ×2，每天 +5 封顶；无神者不祈祷。献祭神迹残片 +10。
  void FaithAltarAndPrayer(){
   FReset();var lath=Faith.God("Lathander");FaithUtility.SetPatron(lath);
   var altar=FAltar();var p=FPawn();
   var giver=new JoyGiver_Pray{def=DefDatabase<JoyGiverDef>.GetNamed("LP_PrayAtAltar")};var job=giver.TryGiveJob(p);
   float a=Faith.PrayerReward(p);FDevotee(p,lath);float b=Faith.PrayerReward(p);
   for(int i=0;i<10;i++)Faith.PrayerReward(p);float capped=FaithUtility.GetFavor(lath);
   FDevotee(p,null);p.story.traits.GainTrait(new Trait(Faith.Atheist));bool atheistBlocked=!Faith.CanPray(p,out _) && giver.TryGiveJob(p)==null;
   FReset();FaithUtility.SetPatron(lath);
   float offered=FOffer("LP_Offer_Shard",Faith.ShardDef,1,altar.Map);Offerings.RefreshLiked();var liked=Offerings.Liked;
   Check(job!=null && job.def==Faith.PrayJob && job.targetA.Thing==altar && Near(a,.5f) && Near(b,1f) && Near(capped,5f) && atheistBlocked && Near(offered,10f) && liked.fixedIngredientFilter.Allows(DefDatabase<ThingDef>.GetNamed("RawPotatoes")) && !liked.fixedIngredientFilter.Allows(ThingDefOf.Steel),"prayer: joy picks the altar, +0.5 (devotee x2), 5/day cap ("+capped+"); atheists don't pray; shard offering +10 ("+offered+"); liked offerings follow Lathander");
   // 热修：按原版顺序（先 ConsumeIngredient 销毁原料、stackCount 归零，再 Notify_IterationCompleted）献祭白银和喜爱的祭品。
   var potato=DefDatabase<ThingDef>.GetNamed("RawPotatoes");float potatoValue=potato.BaseMarketValue*60f;
   var ist=IdeoBridge.Active?IdeoState.Get:null;int oldTick=ist?.lastOfferingTick??0;if(ist!=null)ist.lastOfferingTick=-1;
   float silver=FOffer("LP_Offer_Silver",ThingDefOf.Silver,100,altar.Map);float likedGain=FOffer("LP_Offer_Liked",potato,60,altar.Map);
   bool ideoMarked=ist==null || ist.lastOfferingTick==Faith.Now;if(ist!=null)ist.lastOfferingTick=oldTick;
   var hist=Faith.Comp.history;
   Check(Near(silver,1f*IdeoBridge.OfferingFactor) && Near(likedGain,potatoValue/Faith.SilverPerFavor*Faith.LikedFactor*IdeoBridge.OfferingFactor) && hist.Count(h=>h.reason.Contains("献上"))==3 && ideoMarked,"offerings in vanilla order (consume destroys first): silver 100 -> +"+silver.ToString("0.##")+", 60 potatoes -> +"+likedGain.ToString("0.##")+", history logged, IdeoState offering tick updated");
  }
  // 按原版 Toils_Recipe 的顺序跑一次献祭：ConsumeIngredient（销毁）→ Notify_IterationCompleted；返回守护神神眷变化。
  static float FOffer(string recipe,ThingDef def,int count,Map map){
   var r=DefDatabase<RecipeDef>.GetNamed(recipe);var t=ThingMaker.MakeThing(def);t.stackCount=count;
   var god=FaithUtility.Patron;float before=FaithUtility.GetFavor(god);var list=new List<Thing>{t};
   foreach(var i in list)r.Worker.ConsumeIngredient(i,r,map);
   r.Worker.Notify_IterationCompleted(null,list);
   return FaithUtility.GetFavor(god)-before;
  }

  // 6 圣徽增删神术；7 P 倍率、信徒半价、神眷不足不能放；8 驱逐邪祟对骷髅 ×2；9 召雷 instigator。
  void FaithMiraclesCheck(){
   FReset();var lath=Faith.God("Lathander");FaithUtility.SetPatron(lath);FSet(lath,15f);
   var p=FPawn();var sym=(Apparel)ThingMaker.MakeThing(Faith.SymbolDef);p.apparel.Wear(sym);
   var have=p.abilities.AllAbilitiesForReading.Where(x=>x is Ability_Miracle).Select(x=>x.def.defName).ToList();
   bool four=have.Count==4 && have.Contains("LP_Miracle_Dawn") && Miracles.Generic.All(have.Contains);
   p.apparel.Remove(sym);bool gone=!p.abilities.AllAbilitiesForReading.Any(x=>x is Ability_Miracle);
   p.story.traits.GainTrait(new Trait(Faith.Atheist));p.apparel.Wear(sym);bool atheistNone=!p.abilities.AllAbilitiesForReading.Any(x=>x is Ability_Miracle);
   FDevotee(p,null);Miracles.Sync(p);
   Check(four && gone && atheistNone,"holy symbol grants the 3 generic miracles + the patron's domain miracle, takes them back when removed, gives nothing to atheists");
   int social=p.skills.GetSkill(SkillDefOf.Social).Level;float sens=Mathf.Max(.2f,p.GetStatValue(StatDefOf.PsychicSensitivity));
   float expected=(.5f+social/20f+Miracles.SymbolBonus)*sens;float basic=Miracles.Power(p);
   var sanct=DefDatabase<AbilityDef>.GetNamed("LP_Miracle_Sanctuary");var dawn=DefDatabase<AbilityDef>.GetNamed("LP_Miracle_Dawn");
   float cost=FaithUtility.MiracleCost(p,sanct);FDevotee(p,lath);float half=FaithUtility.MiracleCost(p,sanct);float devP=Miracles.Power(p);
   var ab=p.abilities.GetAbility(sanct);bool domainBlocked=Miracles.Blocked(p,dawn)!=null;FSet(lath,1f);bool poor=ab.GizmoDisabled(out string why) && why.Contains("神眷不足");
   FSet(lath,50f);bool fine=Miracles.Blocked(p,sanct)==null && Miracles.Blocked(p,dawn)==null;
   Check(Near(basic,expected) && Near(cost,3f) && Near(half,1.5f) && Near(devP,(expected/sens+Miracles.DevoteeBonus)*sens) && domainBlocked && poor && fine,"P = (0.5 + social/20 + 0.1 symbol) x sensitivity = "+basic.ToString("0.##")+"; devotee halves the cost (3 -> 1.5) and adds 0.15; too little favor greys it out ("+why+"), domain needs 20");
   var enemy=Factions.Hostile??Find.FactionManager.AllFactions.First(f=>f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction);
   Pawn skel=null;try{skel=EvKind("LP_FH_Skeleton",enemy);fMade.Add(skel);}catch{}
   var foe=FPawn(enemy);
   if(skel!=null && Necro.Construct(skel)!=null)Check(Near(Miracles.BanishAmount(skel,25f),50f) && Near(Miracles.BanishAmount(foe,25f),25f) && Faith.HolyVulnerable(skel),"banish evil: 25 radiant to a living foe, x2 to a skeleton construct");
   else Check(Near(Miracles.BanishAmount(foe,25f),25f),"banish evil: 25 radiant to a living foe (no skeleton kind in this world)");
   Miracles.lastStrikeInstigator=null;var cells=Miracles.Thunder(p,foe.Position,25f*Miracles.Power(p));
   Check(Miracles.lastStrikeInstigator==p && cells.Count==3 && cells.All(c=>c.DistanceTo(foe.Position)<=2.9f),"call lightning: three strikes within 2 cells, instigator is the caster");
  }

  // 10 祝圣圣水：神圣伤害（骷髅 ×2）、灭火、巨魔再生停 1 小时；可以喝。
  void FaithWater(){
   var cell=OpenCell();var human=FPawn(Factions.Hostile);human.Position=cell;human.Notify_Teleported();
   var troll=RacePawn("LP_Troll");fMade.Add(troll);troll.Position=cell+IntVec3.North;troll.Notify_Teleported();
   var fireThing=(Fire)ThingMaker.MakeThing(ThingDefOf.Fire);fireThing.fireSize=.5f;GenSpawn.Spawn(fireThing,cell+IntVec3.East,map);bool hadFire=(cell+IntVec3.East).GetFirstThing<Fire>(map)!=null;
   float hp=human.health.summaryHealth.SummaryHealthPercent;
   int hit=BlessedWater.Burst(map,cell,null);var g=Troll.Gene(troll);
   bool fireOut=(cell+IntVec3.East).GetFirstThing<Fire>(map)==null;
   var drinker=FPawn();var flu=drinker.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Flu"));flu.Severity=.5f;var eased=BlessedWater.Drink(drinker);
   Check(hit>=2 && human.health.summaryHealth.SummaryHealthPercent<hp && hadFire && fireOut && g!=null && g.suppressedUntilTick>=Faith.Now+BlessedWater.TrollTicks-1 && eased==flu && Near(flu.Severity,.45f) && drinker.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_DrankBlessedWater"))!=null,"blessed water: 12 radiant in 1.9 cells, puts out fire, stops troll regeneration for an hour; drinking eases flu by 10% and +2 mood [hit="+hit+" hp="+hp.ToString("0.###")+"->"+human.health.summaryHealth.SummaryHealthPercent.ToString("0.###")+" fire="+hadFire+"/"+fireOut+" troll="+(g!=null)+"/"+(g==null?0:g.suppressedUntilTick-Faith.Now)+" flu="+flu.Severity.ToString("0.###")+" eased="+(eased==flu)+"]");
  }

  // 11 季度显圣一例：洛山达 ≥50 → 赐福 3 天（地图条件、作物 ×1.25、殖民者 +4）；≤-30 → 降罚。
  void FaithManifestCheck(){
   FReset();var lath=Faith.God("Lathander");FaithUtility.SetPatron(lath);FSet(lath,60f);
   var c=Faith.Comp;FaithManifest.Quarterly(c);
   var cond=map.gameConditionManager.ActiveConditions.OfType<GameCondition_FaithManifest>().FirstOrDefault();
   var col=FPawn();var thought=DefDatabase<ThoughtDef>.GetNamed("LP_DawnFavor");
   bool bless=c.Blessed("Lathander") && cond!=null && cond.god==lath && cond.blessing && Math.Abs(cond.Duration-3*GenDate.TicksPerDay)<10 && thought.Worker.CurrentState(col).Active;
   FSet(lath,-40f);FaithManifest.Quarterly(c);bool curse=c.Cursed("Lathander") && !c.Blessed("Lathander");
   Check(bless && curse,"quarterly manifestation: Lathander at 60 blesses for 3 days (map condition, +4 mood); at -40 curses");
  }

  // 12 朝圣者事件生成并接待。
  void FaithPilgrimsCheck(){
   FReset();var lath=Faith.God("Lathander");FaithUtility.SetPatron(lath);FSet(lath,10f);var altar=FAltar();
   var def=DefDatabase<IncidentDef>.GetNamed("LP_Faith_Pilgrims");var parms=StorytellerUtility.DefaultParmsNow(def.category,map);
   bool can=def.Worker.CanFireNow(parms);int letters=Find.LetterStack.LettersListForReading.Count;bool fired=def.Worker.TryExecute(parms);
   var letter=Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_FaithPilgrims>().LastOrDefault();
   var lord=letter?.Accept();if(letter!=null)Find.LetterStack.RemoveLetter(letter);
   var lj=lord?.LordJob as LordJob_Pilgrims;
   bool ok=can && fired && letter!=null && letter.pawns.Count>=3 && letter.pawns.Count<=5 && lord!=null && lj.altar!=null && lj.altar.Spawned && lord.ownedPawns.All(x=>x.Spawned && FaithUtility.DevoteeGod(x)==letter.god && Pilgrims.Is(x));
   float before=FaithUtility.GetFavor(letter?.god);lj?.Finish(true,null);float gained=FaithUtility.GetFavor(letter?.god)-before;
   if(lord!=null){foreach(var x in lord.ownedPawns.ToList())fMade.Add(x);map.lordManager.RemoveLord(lord);}
   foreach(var x in map.mapPawns.FreeColonistsSpawned.Where(x=>letter!=null && letter.pawns.Contains(x)).ToList())fMade.Add(x);
   Check(ok && Near(gained,letter.god==lath?12f:8f),"pilgrims: fires with an altar and patron, 3-5 devotees of one god arrive to pray at the altar; hosting them gives +"+gained+" favor [can="+can+" fired="+fired+" letter="+(letter!=null)+" n="+letter?.pawns.Count+" lord="+(lord!=null)+" pawns="+string.Join(",",lord?.ownedPawns.Select(x=>x.Spawned+"/"+FaithUtility.DevoteeGod(x)?.key+"/"+Pilgrims.Is(x))??new string[0])+"]");
  }

  // 13 神选仪式 + 转阿斯莫保留内生基因；巫妖不能转。
  void FaithChosen(){
   FReset();var lath=Faith.God("Lathander");FaithUtility.SetPatron(lath);FSet(lath,100f);FAltar();
   var p=FPawn();FDevotee(p,lath);var shards=ThingMaker.MakeThing(Faith.ShardDef);shards.stackCount=3;GenPlace.TryPlaceThing(shards,p.Position,map,ThingPlaceMode.Near);fMade.Add(shards);
   bool planet=Faith.Planetouched.IsFinished;if(!planet)Find.ResearchManager.FinishProject(Faith.Planetouched,false,null,false);
   var endo=p.genes.Endogenes.Select(g=>g.def).ToList();
   bool done=Faith.CompleteChosen(p,true);
   bool chosen=FaithUtility.IsChosen(p,out var god) && god==lath && p.health.hediffSet.GetFirstHediffOfDef(Faith.ChosenDef).Label=="神选者（洛山达）";
   bool aasimar=p.genes.Xenotype==FaithRaces.Aasimar && FaithRaces.IsAasimar(p) && p.genes.Xenogenes.Any(g=>g.def==FaithRaces.Lineage) && p.genes.Endogenes.Select(g=>g.def).SequenceEqual(endo);
   bool paid=Near(FaithUtility.GetFavor(lath),50f) && Faith.ShardsOn(map)==0 && Near(FaithUtility.MiracleCost(p,DefDatabase<AbilityDef>.GetNamed("LP_Miracle_Sanctuary")),.75f);
   var lich=FPawn();lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);FDevotee(lich,lath);
   bool lichNo=FaithRaces.CannotConvert(lich)!=null && FaithRaces.ConversionBlocked(lich,lath)!=null && !FaithRaces.Convert(lich,FaithRaces.Aasimar);
   if(!planet)Find.ResearchManager.ResetAllProgress();
   Check(done && chosen && aasimar && paid && lichNo,"chosen ritual: costs 50 favor + 3 shards, gives 神选者（洛山达）(cost x0.25 with devotee), turns the devotee aasimar via xenogenes keeping "+endo.Count+" endogenes; liches cannot convert");
  }

  // 14 阿斯莫/提夫林：基因、代谢、抗性、遗传。
  void FaithRacesCheck(){
   var a=RacePawn("LP_Aasimar");var t=RacePawn("LP_Tiefling");fMade.Add(a);fMade.Add(t);
   int Met(Pawn p)=>p.genes.GenesListForReading.Where(g=>!g.Overridden).Sum(g=>g.def.biostatMet);
   bool genes=FaithRaces.IsAasimar(a) && FaithRaces.IsTiefling(t) && a.genes.GenesListForReading.Count(g=>FaithRaces.Revelations.Contains(g.def.defName))==1 && a.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_HealingHands"))!=null && t.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_InfernalPact"))!=null;
   float rad=a.genes.FactorForDamage(new DamageInfo(Faith.Radiant,10f)),fire=t.genes.FactorForDamage(new DamageInfo(DamageDefOf.Flame,10f));
   var a2=RacePawn("LP_Aasimar");fMade.Add(a2);var kids=PregnancyUtility.GetInheritedGeneSet(a,a2);
   bool inherit=FaithRaces.Aasimar.inheritable && FaithRaces.Tiefling.inheritable && kids.GenesListForReading.Contains(FaithRaces.Lineage);
   var undead=FPawn(Factions.Hostile);undead.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);
   float bane=FaithDamage.DamageFactor(undead,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,a));
   Check(genes && Met(a)==-1 && Met(t)==0 && Near(rad,.5f) && Near(fire,.5f) && inherit && Near(bane,1.25f),"aasimar (metabolism "+Met(a)+") and tiefling ("+Met(t)+"): core genes, one revelation, abilities, radiant/fire x0.5, aasimar x1.25 vs liches, genes inherit [genes="+genes+" rad="+rad+" fire="+fire+" inherit="+inherit+" bane="+bane+"]");
  }

  // 15 设置关闭不报错。
  void FaithSettingsOff(){
   FReset();var lath=Faith.God("Lathander");FaithUtility.SetPatron(lath);FSet(lath,30f);FAltar();var p=FPawn();
   LichMod.Settings.faithSystem=false;Faith.ApplySettings();
   var def=DefDatabase<IncidentDef>.GetNamed("LP_Faith_Trial");var parms=StorytellerUtility.DefaultParmsNow(def.category,map);
   Exception err=null;try{Faith.Comp.Daily();FaithQuests.HourlyCheck(Faith.Comp);Faith.Deed("Lathander",5f,"测试");}catch(Exception e){err=e;}
   bool off=err==null && !def.Worker.CanFireNow(parms) && Miracles.Blocked(p,DefDatabase<AbilityDef>.GetNamed("LP_Miracle_Sanctuary"))!=null && Faith.PrayerReward(p)==0f && !Faith.CanPray(p,out _) && Faith.Commonality(Faith.Devotee)==0f && Near(FaithUtility.GetFavor(lath),30f);
   LichMod.Settings.faithSystem=true;LichMod.Settings.faithQuestPercent=0;bool noQuest=!def.Worker.CanFireNow(parms);LichMod.Settings.faithQuestPercent=100;
   Faith.ApplySettings();bool back=Near(Faith.Commonality(Faith.Devotee),Faith.DevoteeCommonality) && Near(Faith.Commonality(Faith.Atheist),Faith.AtheistCommonality);
   Check(off && noQuest && back,"faith off: no deeds, quests, prayer or miracles and nothing throws; quest frequency 0 stops quests; trait weights 0.3/0.1 come back"+(err==null?"":": "+err));
  }

  void PrepareFaithSave(){
   FReset();var c=Faith.Comp;FaithUtility.SetPatron(Faith.God("Tyr"));FaithUtility.AddFavor(Faith.God("Tyr"),42.5f,"存档测试");FaithUtility.AddFavor(Faith.God("Shar"),-12f,"存档测试");FaithUtility.SetPatron(Faith.God("Bane"));
   fSaveFavor=c.favor.ToDictionary(kv=>kv.Key.defName,kv=>kv.Value);fSavePatron=c.patron.defName;fSaveCooldown=c.patronCooldownUntil;
  }
  void CheckFaithAfterLoad(){
   var c=Faith.Comp;
   Check(c.patron?.defName==fSavePatron && c.patronCooldownUntil==fSaveCooldown && fSaveFavor.All(kv=>Near(c.Get(DefDatabase<LP_DeityDef>.GetNamed(kv.Key)),kv.Value)) && c.history.Count>=3,"faith save/reload keeps patron, cooldown, favor ("+string.Join(",",fSaveFavor.Select(kv=>kv.Key+"="+kv.Value))+") and history");
   FReset();
  }
 }
}
