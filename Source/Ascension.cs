using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.40.0 成神任务线（ArtSource/Ascension-1.40.0-spec.md）：统一框架——设置、神职、追踪组件、“成神之路”按钮与窗口、神性火花、升神仪式、试炼袭击。
 // 四条路线的步骤和计数钩子在 AscensionPaths.cs，半神状态/神力/议会/神域在 Demigod.cs，神职能力在 DemigodAbilities.cs。
 // 只读信仰的神眷值和圣坛；加减神眷一律走 FaithUtility.AddFavor。

 // ---------- 设置（左栏“成神”，在信仰下面） ----------
 public partial class Settings {
  public bool ascension=true;public int demigodCap=1,ascensionDifficulty=100;
  void ExposeAscension(){
   Scribe_Values.Look(ref ascension,"lpAscension",true);Scribe_Values.Look(ref demigodCap,"lpDemigodCap",1);Scribe_Values.Look(ref ascensionDifficulty,"lpAscensionDifficulty",100);
   demigodCap=Math.Max(1,Math.Min(3,demigodCap));ascensionDifficulty=Math.Max(50,Math.Min(200,ascensionDifficulty));
  }
 }
 public static class AscensionSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;l.GapLine();l.Label("成神");
   l.CheckboxLabeled("成神任务线",ref s.ascension,"圣徒、半神巫妖、泰坦觉醒、篡火者四条成神之路，以及半神、神灵议会和升入神域。关闭后不再有“成神之路”按钮、不再开启新路线、试炼袭击不再到来；已经成为半神的人保留能力。");
   l.Label("半神名额上限："+s.demigodCap+"（升神那一刻检查；升入神域的不占名额）");s.demigodCap=(int)l.Slider(s.demigodCap,1,3);
   l.Label("试炼难度倍率："+(s.ascensionDifficulty/100f).ToString("0.0#")+"（所有成神试炼袭击和神灵议会的点数）");s.ascensionDifficulty=Mathf.RoundToInt(l.Slider(s.ascensionDifficulty,50,200)/10f)*10;
  }
 }

 // ---------- 神职 ----------
 public class LP_DivinePortfolioDef : Def {
  public string key;public Color color=Color.white;public string iconPath;
  public List<AbilityDef> abilities=new List<AbilityDef>();
  public string passive,blessing;           // 被动、神域赐福的说明
  public string deed;public int deedTarget; // 事迹种类与数量（泰坦试炼一、篡火者试炼二）；空 = 改为击杀神力灌注者 10 名
  public string deedLabel;
  public List<LP_DeityDef> opposed=new List<LP_DeityDef>(); // 没有对应神时（不死、奥术）的对立神
  [Unsaved] Texture2D icon;
  public Texture2D Icon=>icon??(icon=(iconPath.NullOrEmpty()?null:ContentFinder<Texture2D>.Get(iconPath,false))??BaseContent.BadTex);
  public IEnumerable<LP_DeityDef> Gods=>FaithUtility.AllDeities.Where(d=>d.GetModExtension<LP_PortfolioExtension>()?.portfolio==this);
  // 对立神：神职对应神的敌对神，加上 XML 里写的。
  public IEnumerable<LP_DeityDef> Opposed=>Gods.SelectMany(g=>g.Enemies).Concat(opposed).Distinct();
  public string DeedKey=>deed.NullOrEmpty()?"infused":deed;
  public int DeedTarget=>deed.NullOrEmpty()?10:deedTarget;
  public string DeedText=>deed.NullOrEmpty()?"击杀神力灌注者 10 名":deedLabel+" "+deedTarget;
 }
 public class LP_PortfolioExtension : DefModExtension {public LP_DivinePortfolioDef portfolio;}

 // ---------- 每人一条进度 ----------
 public class AscensionProgress : IExposable {
  public Pawn pawn;public string path;public int stage;public Dictionary<string,int> counters=new Dictionary<string,int>();
  public LP_DeityDef patronGod;public int stageStartTick=-1;public bool done;
  public LP_DivinePortfolioDef portfolio;
  public Lord lord;public Map lordMap;public Pawn leader;public int wave,nextWaveTick=-1,awayTicks;public bool waiting;
  public Thing box;public float kingDamage;
  public int Get(string k)=>counters.TryGetValue(k,out int v)?v:0;
  public void Add(string k,int n=1){counters[k]=Get(k)+HeroDestiny.AscensionScale(pawn,n);} // 1.58.0 神之勇士显现后进度 ×1.5（只放大正数）
  public void Set(string k,int n){counters[k]=n;}
  public void NextStage(){stage++;stageStartTick=Find.TickManager.TicksGame;lord=null;lordMap=null;leader=null;wave=0;nextWaveTick=-1;}
  public float DaysInStage=>(Find.TickManager.TicksGame-stageStartTick)/(float)GenDate.TicksPerDay;
  public void ExposeData(){
   Scribe_References.Look(ref pawn,"pawn",true);Scribe_Values.Look(ref path,"path");Scribe_Values.Look(ref stage,"stage");
   Scribe_Collections.Look(ref counters,"counters",LookMode.Value,LookMode.Value);
   Scribe_Defs.Look(ref patronGod,"patronGod");Scribe_Values.Look(ref stageStartTick,"stageStart",-1);Scribe_Values.Look(ref done,"done");
   Scribe_Defs.Look(ref portfolio,"portfolio");
   Scribe_References.Look(ref lord,"lord");Scribe_References.Look(ref lordMap,"lordMap");Scribe_References.Look(ref leader,"leader");
   Scribe_Values.Look(ref wave,"wave");Scribe_Values.Look(ref nextWaveTick,"nextWave",-1);Scribe_Values.Look(ref awayTicks,"away");Scribe_Values.Look(ref waiting,"waiting");
   Scribe_References.Look(ref box,"box");Scribe_Values.Look(ref kingDamage,"kingDamage");
   if(Scribe.mode==LoadSaveMode.PostLoadInit && counters==null)counters=new Dictionary<string,int>();
  }
 }
 public class AntimagicZone : IExposable {
  public Map map;public IntVec3 center;public float radius;public int until;
  public void ExposeData(){Scribe_References.Look(ref map,"map");Scribe_Values.Look(ref center,"center");Scribe_Values.Look(ref radius,"radius");Scribe_Values.Look(ref until,"until");}
 }

 // ---------- 存档组件 ----------
 public class LP_AscensionTracker : WorldComponent {
  public List<AscensionProgress> progress=new List<AscensionProgress>();
  public List<Pawn> demigods=new List<Pawn>(),ascended=new List<Pawn>();
  public List<LP_DivinePortfolioDef> blessings=new List<LP_DivinePortfolioDef>();
  public int firstAscendTick=-1;
  public List<string> shrines=new List<string>();               // 神龛命匣（ThingID）
  public List<LP_DeityDef> usurped=new List<LP_DeityDef>(),kelemvorFixed=new List<LP_DeityDef>(); // 神眷固定 -100 的神
  public Dictionary<string,int> restUntil=new Dictionary<string,int>(); // 灵魂安息：不能复生/吞脑（ThingID → 到期）
  public List<AntimagicZone> zones=new List<AntimagicZone>();
  public bool githBetrayed;
  public int lastDay=-1;
  public Dictionary<string,LP_DeityDef> fathers=new Dictionary<string,LP_DeityDef>(); // 1.41.0：泰坦的父神（ThingID → 神），开路时优先用它
  public LP_DeityDef FatherOf(Pawn p)=>p!=null && fathers.TryGetValue(p.ThingID,out var g)?g:null;
  public LP_AscensionTracker(World world):base(world){}
  public AscensionProgress Of(Pawn p)=>p==null?null:progress.FirstOrDefault(x=>x.pawn==p && !x.done);
  public override void FinalizeInit(bool fromLoad){base.FinalizeInit(fromLoad);if(fromLoad)Demigods.SyncArcane();} // 世界生成时还没有玩家派系
  public override void WorldComponentTick(){
   int now=Find.TickManager.TicksGame;
   if(now%250==37)Demigods.RefreshCache();
   if(now%GenDate.TicksPerHour!=137)return;
   zones.RemoveAll(z=>z==null || z.map==null || now>=z.until);
   foreach(var k in restUntil.Where(kv=>kv.Value<now).Select(kv=>kv.Key).ToList())restUntil.Remove(k);
   try{AscensionPaths.Hourly(this);Demigods.Hourly(this);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 成神每小时结算出错："+e,77310021);}
   int day=GenDate.DaysPassed;if(day==lastDay)return;bool first=lastDay<0;lastDay=day;if(first)return;
   try{Demigods.Daily(this);AscensionPaths.Daily(this);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 成神每日结算出错："+e,77310022);}
  }
  public override void ExposeData(){
   base.ExposeData();
   if(Scribe.mode==LoadSaveMode.Saving){progress.RemoveAll(x=>x==null || x.pawn==null || x.pawn.Destroyed && !x.pawn.Dead);demigods.RemoveAll(p=>p==null);ascended.RemoveAll(p=>p==null);}
   Scribe_Collections.Look(ref progress,"lpAscProgress",LookMode.Deep);
   Scribe_Collections.Look(ref demigods,"lpDemigods",LookMode.Reference);Scribe_Collections.Look(ref ascended,"lpAscended",LookMode.Reference);
   Scribe_Collections.Look(ref blessings,"lpBlessings",LookMode.Def);Scribe_Values.Look(ref firstAscendTick,"lpFirstAscend",-1);
   Scribe_Collections.Look(ref shrines,"lpShrines",LookMode.Value);
   Scribe_Collections.Look(ref usurped,"lpUsurped",LookMode.Def);Scribe_Collections.Look(ref kelemvorFixed,"lpFixedGods",LookMode.Def);
   Scribe_Collections.Look(ref restUntil,"lpRestUntil",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref zones,"lpAntimagic",LookMode.Deep);
   Scribe_Values.Look(ref githBetrayed,"lpGithBetrayed");Scribe_Values.Look(ref lastDay,"lpAscLastDay",-1);
   Scribe_Collections.Look(ref fathers,"lpTitanFathers",LookMode.Value,LookMode.Def);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    fathers=fathers?.Where(kv=>kv.Key!=null && kv.Value!=null).ToDictionary(kv=>kv.Key,kv=>kv.Value)??new Dictionary<string,LP_DeityDef>();
    progress=progress?.Where(x=>x?.pawn!=null).ToList()??new List<AscensionProgress>();
    demigods=demigods?.Where(p=>p!=null).Distinct().ToList()??new List<Pawn>();ascended=ascended?.Where(p=>p!=null).Distinct().ToList()??new List<Pawn>();
    blessings=blessings?.Where(b=>b!=null).Distinct().ToList()??new List<LP_DivinePortfolioDef>();
    if(shrines==null)shrines=new List<string>();if(usurped==null)usurped=new List<LP_DeityDef>();if(kelemvorFixed==null)kelemvorFixed=new List<LP_DeityDef>();
    usurped.RemoveAll(d=>d==null);kelemvorFixed.RemoveAll(d=>d==null);
    if(restUntil==null)restUntil=new Dictionary<string,int>();zones=zones?.Where(z=>z?.map!=null).ToList()??new List<AntimagicZone>();
   }
  }
 }

 // ==================== 公共规则 ====================
 public static class Ascension {
  public const float SaintFavor=60f,SparkMood=4f;
  public const int RiteTicks=12*GenDate.TicksPerHour,AwayDays=15;
  public const string Saint="Saint",Lich="Lich",Titan="Titan",Usurper="Usurper",Arcane="Arcane";
  public static bool Enabled=>LichMod.Settings?.ascension!=false;
  public static float Difficulty=>(LichMod.Settings?.ascensionDifficulty??100)/100f;
  public static int Cap=>LichMod.Settings?.demigodCap??1;
  public static int Now=>Find.TickManager?.TicksGame??0;
  public static LP_AscensionTracker Tracker=>WorldComp<LP_AscensionTracker>.Get(); // 1.58.6：缓存（属性部件里每次都查）
  static ResearchProjectDef research;static ThingDef spark,usurpedSpark;static GameConditionDef tremor,wrath;static JobDef rite,skyGate;
  public static ResearchProjectDef Research=>research??(research=DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LP_Research_Theogony"));
  public static bool Researched=>Research==null || Research.IsFinished;
  public static ThingDef SparkDef=>spark??(spark=DefDatabase<ThingDef>.GetNamedSilentFail("LP_DivineSpark"));
  public static ThingDef UsurpedSparkDef=>usurpedSpark??(usurpedSpark=DefDatabase<ThingDef>.GetNamedSilentFail("LP_UsurpedSpark"));
  public static GameConditionDef TremorDef=>tremor??(tremor=DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_DivineTremor"));
  public static GameConditionDef WrathDef=>wrath??(wrath=DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_WrathStorm"));
  public static JobDef RiteJob=>rite??(rite=DefDatabase<JobDef>.GetNamedSilentFail("LP_AscensionRite"));
  public static JobDef SkyGateJob=>skyGate??(skyGate=DefDatabase<JobDef>.GetNamedSilentFail("LP_SkyGate"));
  public static LP_DivinePortfolioDef Portfolio(string key)=>DefDatabase<LP_DivinePortfolioDef>.AllDefsListForReading.FirstOrDefault(d=>d.key==key);
  public static LP_DivinePortfolioDef PortfolioOf(LP_DeityDef god)=>god?.GetModExtension<LP_PortfolioExtension>()?.portfolio;
  public static string PathLabel(string path)=>path==Saint?"圣徒之路":path==Lich?"半神巫妖之路":path==Titan?"泰坦觉醒":path==Usurper?"篡火者之路":path==Arcane?"奥术飞升":path;
  public static bool Colonist(Pawn p)=>p!=null && !p.Dead && p.Faction?.IsPlayer==true && p.RaceProps.Humanlike && !p.IsPrisoner && !p.IsSlave;
  public static void Letter(string label,string text,LetterDef def,LookTargets look=null,Faction faction=null)=>Find.LetterStack.ReceiveLetter(label,text,def,look??LookTargets.Invalid,faction);

  // ---------- 名额 ----------
  public static IEnumerable<Pawn> LivingDemigods{get{var t=Tracker;if(t==null)return Enumerable.Empty<Pawn>();return t.demigods.Where(p=>p!=null && !p.Destroyed && !t.ascended.Contains(p) && (!p.Dead || Items.IsLich(p)) && Demigods.Of(p)!=null);}}
  public static int DemigodCount=>LivingDemigods.Count();
  public static string QuotaBlocked=>DemigodCount>=Cap?"世上已经有 "+DemigodCount+" 位半神，名额上限是 "+Cap+"（设置里可以调；升入神域的不占名额）。":null;

  // ---------- 开路 ----------
  public static string CannotStart(Pawn p,string path){
   if(!Enabled)return "成神任务线已关闭。";
   if(!Researched)return "需要研究“神格学”。";
   if(!Colonist(p))return "只有殖民者能走成神之路。";
   if(Demigods.Of(p)!=null)return p.LabelShort+"已经是半神了。";
   var t=Tracker;if(t?.Of(p)!=null)return p.LabelShort+"已经在走"+PathLabel(t.Of(p).path)+"。";
   switch(path){
    case Saint:{
     if(FaithUtility.IsAtheist(p))return "无神者不能成为圣徒。";
     if(!FaithUtility.IsChosen(p,out var g) || g==null)return "需要是神选者。";
     if(FaithUtility.GetFavor(g)<SaintFavor)return g.label+"的神眷不到"+SaintFavor+"（现在 "+FaithUtility.GetFavor(g).ToString("0.#")+"）。";
     return null;}
    case Lich:{
     var h=Necro.Lich(p);if(h?.box==null || h.box.Destroyed)return "需要是有命匣的巫妖。";
     int lv=LichSpells.UnlockedLevel(h.box);if(lv<AscensionPaths.LichLevel)return "命匣等级 "+lv+" / "+AscensionPaths.LichLevel+"。";
     return null;}
    case Titan:{
     if(!AscensionPaths.IsPlainTitan(p))return "需要是天穹神裔（泰坦）。";
     if(!p.DevelopmentalStage.Adult())return "需要成年。";
     return null;}
    case Usurper:return "篡火者之路只能在黄昏之年里击败神明附体的首领时自动开启。";
   }
   return "未知路线";
  }
  public static IEnumerable<string> StartablePaths(Pawn p)=>new[]{Saint,Lich,Titan}.Where(x=>CannotStart(p,x)==null);
  // letter=false：开局剧本自己发一封说明信（1.41.0）。
  public static AscensionProgress Start(Pawn p,string path,bool letter=true){
   var t=Tracker;if(t==null)return null;
   var pr=new AscensionProgress{pawn=p,path=path,stage=0,stageStartTick=Now};
   t.progress.Add(pr);
   AscensionPaths.OnStart(pr,letter);
   return pr;
  }

  // ---------- 按钮 ----------
  public static Texture2D PathIcon=>ContentFinder<Texture2D>.Get("Ascension/UI/PathOfAscension",false)??BaseContent.BadTex;
  public static Command_Action PathGizmo(Pawn p){
   if(!Enabled || !Researched || !Colonist(p) || Demigods.Of(p)!=null)return null;
   var pr=Tracker?.Of(p);
   if(pr==null && !StartablePaths(p).Any())return null;
   return new Command_Action{defaultLabel="成神之路",defaultDesc=pr==null?"踏上一条成神之路：查看可以走的路线和它们的代价。":PathLabel(pr.path)+"："+AscensionPaths.StageLabel(pr)+"\n\n"+AscensionPaths.StageText(pr),icon=PathIcon,action=()=>Find.WindowStack.Add(new Dialog_Ascension(p))};
  }

  // ---------- 神性火花 ----------
  public static Thing MakeSpark(Pawn owner,LP_DivinePortfolioDef portfolio,string path,bool usurped=false){
   var def=usurped?UsurpedSparkDef:SparkDef;if(def==null)return null;
   var t=ThingMaker.MakeThing(def);var c=t.TryGetComp<CompDivineSpark>();if(c!=null){c.owner=usurped?null:owner;c.portfolio=portfolio;c.path=path;c.usurped=usurped;}
   return t;
  }
  public static IEnumerable<Thing> SparksOn(Map map)=>map==null?Enumerable.Empty<Thing>():map.listerThings.ThingsOfDef(SparkDef).Concat(UsurpedSparkDef==null?Enumerable.Empty<Thing>():map.listerThings.ThingsOfDef(UsurpedSparkDef));
  public static IEnumerable<Thing> AllSparks(Map map){if(map==null)yield break;foreach(var t in SparksOn(map))yield return t;foreach(var p in map.mapPawns.FreeColonistsSpawned)foreach(var t in p.inventory?.innerContainer??(IEnumerable<Thing>)new List<Thing>())if(t.TryGetComp<CompDivineSpark>()!=null)yield return t;}
  public static Thing SparkFor(Pawn p){if(p?.MapHeld==null)return null;return AllSparks(p.MapHeld).FirstOrDefault(t=>t.TryGetComp<CompDivineSpark>()?.CanAbsorb(p)==true);}

  // ---------- 升神仪式 ----------
  public static bool RiteStage(AscensionProgress pr)=>pr!=null && ((pr.path==Saint && pr.stage==AscensionPaths.SaintRite) || (pr.path==Lich && pr.stage==AscensionPaths.LichRite) || (pr.path==Usurper && pr.stage==AscensionPaths.UsurperRite));
  public static string RiteBlocked(Pawn p){
   if(!Enabled)return "成神任务线已关闭。";
   if(!Researched)return "需要研究“神格学”。";
   var pr=Tracker?.Of(p);if(pr==null)return p.LabelShort+"没有在走成神之路。";
   if(!RiteStage(pr))return "还没到升神仪式这一步："+AscensionPaths.StageLabel(pr)+"。";
   if(pr.path==Saint && pr.lord!=null)return "夺火者还没打完。";
   var q=QuotaBlocked;if(q!=null)return q;
   if(SparkFor(p)==null)return "地图上没有"+p.LabelShort+"能吸收的神性火花。";
   if(pr.path==Lich){var box=Necro.Lich(p)?.box;if(box==null || box.Destroyed || box.MapHeld!=p.MapHeld)return "命匣要和"+p.LabelShort+"在同一张地图上。";}
   else if(!FaithUtility.Altars(p.MapHeld).Any())return "需要一座圣坛。";
   return null;
  }
  public static Thing RiteFocus(Pawn p){var pr=Tracker?.Of(p);if(pr?.path==Lich)return Necro.Lich(p)?.box;return FaithUtility.Altars(p.MapHeld).OrderBy(a=>a.Position.DistanceToSquared(p.Position)).FirstOrDefault();}
  public static bool BeginRite(Pawn p,int shards=0){
   if(RiteBlocked(p)!=null || RiteJob==null)return false;
   var focus=RiteFocus(p);if(focus==null || !focus.Spawned)return false;
   var cell=CellFinder.StandableCellNear(focus.Position,focus.Map,3,c=>c!=focus.Position && p.CanReach(c,PathEndMode.OnCell,Danger.Deadly));if(!cell.IsValid)return false;
   var job=JobMaker.MakeJob(RiteJob,focus,cell);job.count=Mathf.Clamp(shards,0,AscensionPaths.UsurperShardsMax);
   return p.jobs.TryTakeOrderedJob(job,JobTag.Misc);
  }
  public static void RiteStarted(Pawn p){
   var map=p.Map;if(map!=null && TremorDef!=null && !map.gameConditionManager.ConditionIsActive(TremorDef))map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(TremorDef,RiteTicks+GenDate.TicksPerHour));
   var pr=Tracker?.Of(p);if(pr?.path==Lich)AscensionPaths.LichRiteRaid(pr);
   Messages.Message(p.LabelShort+"开始引导神性火花（12小时，可以打断，打断不消耗火花）。",p,MessageTypeDefOf.NeutralEvent,false);
  }
  public static void RiteEnded(Pawn p){var map=p?.MapHeld;if(map==null || TremorDef==null)return;foreach(var c in map.gameConditionManager.ActiveConditions.Where(c=>c.def==TremorDef).ToList())c.End();}
  // 仪式完成（测试直接调用）：roll 为空时掷骰。返回是否成功。
  public static bool CompleteRite(Pawn p,int shards=0,float? roll=null){
   var t=Tracker;var pr=t?.Of(p);if(pr==null)return false;
   var spark=SparkFor(p);var comp=spark?.TryGetComp<CompDivineSpark>();
   var portfolio=comp?.portfolio??pr.portfolio;
   if(pr.path==Usurper){
    float chance=AscensionPaths.UsurperChance(p,shards);
    if(shards>0)Faith.TakeShards(p.MapHeld,Math.Min(shards,Faith.ShardsOn(p.MapHeld)));
    bool ok=(roll??Rand.Value)<chance;
    if(!ok){AscensionPaths.UsurperFailed(p,spark);return false;}
   }
   if(spark!=null && !spark.Destroyed){if(spark.stackCount>1)spark.SplitOff(1).Destroy();else spark.Destroy();}
   Demigods.Grant(p,portfolio,pr.path);
   AscensionPaths.Rewards(pr);
   pr.done=true;t.progress.Remove(pr);
   return true;
  }

  // ---------- 试炼袭击（原版 RaidEnemy 的参数；点数 × 难度倍率） ----------
  public static Faction FallbackEnemy(){var f=Factions.Hostile;if(f!=null && !f.defeated && f.HostileTo(Faction.OfPlayer))return f;return Find.FactionManager.AllFactionsListForReading.Where(x=>!x.IsPlayer && !x.Hidden && !x.defeated && x.HostileTo(Faction.OfPlayer) && x.def.humanlikeFaction && x.def.pawnGroupMakers!=null && x.def.pawnGroupMakers.Any(g=>g.kindDef==PawnGroupKindDefOf.Combat)).RandomElementWithFallback();}
  // 与某些神对立的派系（派系固定信仰 FaithExtension），找不到 → 塞伊远征军。
  public static Faction FactionOfGods(IEnumerable<LP_DeityDef> gods,out LP_DeityDef god){
   god=null;var list=gods?.ToList()??new List<LP_DeityDef>();
   foreach(var g in list)foreach(var f in Find.FactionManager.AllFactionsListForReading.Where(f=>!f.IsPlayer && !f.defeated && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction && f.def.pawnGroupMakers!=null && f.def.pawnGroupMakers.Any(x=>x.kindDef==PawnGroupKindDefOf.Combat)))
    if(f.def.GetModExtension<FaithExtension>()?.deities.Contains(g)==true){god=g;return f;}
   god=list.FirstOrDefault();
   return FallbackEnemy();
  }
  // 附体用的神：必须是黄昏之年的 8 位灌注神之一。
  public static LP_DeityDef VesselGod(LP_DeityDef wanted,Faction f,Pawn leader){
   if(wanted!=null && wanted.degree>0)return wanted;
   var d=Twilight.DeityFor(leader,f);if(d!=null && d.degree>0)return d;
   var ext=f?.def?.GetModExtension<FaithExtension>();var e=ext?.deities.FirstOrDefault(x=>x.degree>0);if(e!=null)return e;
   return Twilight.ByKey("Bane")??Twilight.Deities.FirstOrDefault();
  }
  public static float Points(Map map,float min,float factor=1f)=>Mathf.Max(min,map==null?min:StorytellerUtility.DefaultThreatPointsNow(map))*factor*Difficulty;
  public static List<Pawn> RaidPawns(Faction f,Map map,float points){
   var parms=new IncidentParms{target=map,faction=f,points=Mathf.Max(f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat),points),raidStrategy=RaidStrategyDefOf.ImmediateAttack,raidArrivalMode=PawnsArrivalModeDefOf.EdgeWalkIn};
   return PawnGroupMakerUtility.GeneratePawns(IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat,parms,true)).ToList();
  }
  public static List<Pawn> KindPawns(PawnKindDef kind,Faction f,Map map,float points,int min=1){
   var list=new List<Pawn>();if(kind==null)return list;int n=Mathf.Max(min,Mathf.FloorToInt(points/Mathf.Max(50f,kind.combatPower)));
   for(int i=0;i<n;i++)list.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true)));
   if(f!=null && f.HostileTo(Faction.OfPlayer)){var people=list.Where(p=>Adventurer.Get(p)!=null).ToList();if(people.Count>0 && AdventurerEnemies.On)AdventurerEnemies.LevelGroup(people,points,AdventurerEnemies.Cap());} // 1.48.1 敌人等级
   return list;
  }
  // 放进地图并建领主：有目标时直扑目标（照 LordJob_AssaultThings），否则突袭殖民地。
  public static Lord Arrive(Map map,Faction f,List<Pawn> pawns,Thing target=null){
   if(pawns==null || pawns.Count==0)return null;
   var parms=new IncidentParms{target=map,faction=f,raidArrivalMode=PawnsArrivalModeDefOf.EdgeWalkIn,raidStrategy=RaidStrategyDefOf.ImmediateAttack};
   foreach(var p in pawns)if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);
   if(!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms)){if(!RCellFinder.TryFindRandomPawnEntryCell(out var edge,map,CellFinder.EdgeRoadChance_Hostile))edge=CellFinder.RandomEdgeCell(map);parms.spawnCenter=edge;}
   parms.raidArrivalMode.Worker.Arrive(pawns,parms);
   LordJob job=target!=null?(LordJob)new LordJob_AssaultThings(f,new List<Thing>{target},1f,false):new LordJob_AssaultColony(f,false,false,false,false,false,false,false);
   return LordMaker.MakeNewLord(f,job,map,pawns);
  }
  public static Pawn Leader(List<Pawn> pawns)=>pawns.FirstOrDefault(p=>p.kindDef.factionLeader && Twilight.Eligible(p))??pawns.Where(Twilight.Eligible).OrderByDescending(p=>p.kindDef.combatPower).FirstOrDefault();
  public static bool LordOver(Lord lord,Map map)=>lord==null || map==null || !map.lordManager.lords.Contains(lord) || lord.ownedPawns.All(p=>p.Dead || p.Downed || !p.Spawned || p.IsPrisonerOfColony);
  public static bool Beaten(Pawn p)=>p==null || p.Dead || p.Downed || p.IsPrisonerOfColony || p.Faction==Faction.OfPlayer;
 }

 // ---------- 神性火花 ----------
 public class CompProperties_DivineSpark : CompProperties {public CompProperties_DivineSpark(){compClass=typeof(CompDivineSpark);}}
 public class CompDivineSpark : ThingComp {
  public Pawn owner;public LP_DivinePortfolioDef portfolio;public string path;public bool usurped;
  public bool CanAbsorb(Pawn p)=>p!=null && (usurped || owner==null || owner==p);
  public override string CompInspectStringExtra()=>"神职："+(portfolio?.label??"未定")+"\n来源："+Ascension.PathLabel(path)+(usurped?"（任何殖民者都可以吸收）":owner!=null?"\n只有"+owner.LabelShort+"能吸收":"");
  public override void PostExposeData(){base.PostExposeData();Scribe_References.Look(ref owner,"lpOwner",true);Scribe_Defs.Look(ref portfolio,"lpPortfolio");Scribe_Values.Look(ref path,"lpPath");Scribe_Values.Look(ref usurped,"lpUsurped");}
  public override bool AllowStackWith(Thing other)=>false;
 }
 public class ThoughtWorker_DivineRadiance : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>Ascension.Enabled && p.Faction?.IsPlayer==true && p.Spawned && AnySpark(p.Map);
  // 1.58.6：“地图上有没有神火”与是哪个殖民者无关；原来每个殖民者每次刷新想法都翻一遍所有殖民者的背包（殖民者数的平方），现在同一张地图同一刻只算一次。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<int,(int tick,Map map,bool any)> memo=new System.Collections.Concurrent.ConcurrentDictionary<int,(int,Map,bool)>();
  static bool AnySpark(Map map){int now=Find.TickManager.TicksGame;if(memo.TryGetValue(map.uniqueID,out var m) && m.tick==now && m.map==map)return m.any;bool any=Ascension.AllSparks(map).Any();memo[map.uniqueID]=(now,map,any);return any;}
 }

 // ---------- 窗口 ----------
 public class Dialog_Ascension : Window {
  readonly Pawn pawn;
  public override Vector2 InitialSize=>new Vector2(620f,520f);
  public Dialog_Ascension(Pawn p){pawn=p;doCloseX=true;forcePause=true;absorbInputAroundWindow=true;closeOnClickedOutside=true;}
  public override void DoWindowContents(Rect r){
   var l=new Listing_Standard();l.Begin(r);
   Text.Font=GameFont.Medium;l.Label(pawn.LabelShort+" · 成神之路");Text.Font=GameFont.Small;l.GapLine();
   var pr=Ascension.Tracker?.Of(pawn);
   if(pr==null){
    foreach(var path in new[]{Ascension.Saint,Ascension.Lich,Ascension.Titan}){
     string why=Ascension.CannotStart(pawn,path);
     l.Label(Ascension.PathLabel(path)+(why==null?"":"（"+why+"）"));
     l.Label("   "+AscensionPaths.PathSummary(path));
     if(why==null && l.ButtonText("踏上"+Ascension.PathLabel(path))){
      string p2=path;Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(AscensionPaths.StartWarning(pawn,p2),()=>{Ascension.Start(pawn,p2);Close();},true,"踏上此路"));
     }
     l.Gap(6f);
    }
    l.Label("篡火者之路："+AscensionPaths.PathSummary(Ascension.Usurper));
   }else{
    l.Label("路线："+Ascension.PathLabel(pr.path));
    l.Label("当前试炼："+AscensionPaths.StageLabel(pr));
    l.Label(AscensionPaths.StageText(pr));
    float f=AscensionPaths.StageProgress(pr);
    var bar=l.GetRect(22f);Widgets.FillableBar(bar,Mathf.Clamp01(f),SolidColorMaterials.NewSolidColorTexture(new Color(.86f,.7f,.3f)),BaseContent.GreyTex,true);Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(bar,f.ToStringPercent());Text.Anchor=TextAnchor.UpperLeft;
    l.Gap();l.Label("下一步："+AscensionPaths.Hint(pr));
    l.Gap();
    foreach(var b in AscensionPaths.Buttons(pr)){
     if(b.disabled!=null){l.Label(b.label+"（"+b.disabled+"）");continue;}
     if(l.ButtonText(b.label)){b.action();Close();}
    }
   }
   l.End();
  }
 }
 public class AscensionButton {public string label,disabled;public Action action;}
 public class Dialog_UsurperShards : Window {
  readonly Pawn pawn;int shards;
  public override Vector2 InitialSize=>new Vector2(480f,260f);
  public Dialog_UsurperShards(Pawn p){pawn=p;doCloseX=true;forcePause=true;absorbInputAroundWindow=true;shards=Math.Min(AscensionPaths.UsurperShardsMax,Faith.ShardsOn(p.MapHeld));}
  public override void DoWindowContents(Rect r){
   var l=new Listing_Standard();l.Begin(r);
   int have=Faith.ShardsOn(pawn.MapHeld);
   l.Label("篡火仪式：献上神迹残片提高成功率（每片 +8%，最多 "+AscensionPaths.UsurperShardsMax+" 片；地图上有 "+have+" 片）。");
   l.Label("献上："+shards+" 片");shards=(int)l.Slider(shards,0,Math.Min(AscensionPaths.UsurperShardsMax,have));
   l.Label("成功率："+AscensionPaths.UsurperChance(pawn,shards).ToStringPercent()+"。失败时"+pawn.LabelShort+"会死去，火花碎成 5 片神迹残片。");
   if(l.ButtonText("开始仪式")){if(!Ascension.BeginRite(pawn,shards))Messages.Message(Ascension.RiteBlocked(pawn)??"到不了仪式的位置。",MessageTypeDefOf.RejectInput,false);Close();}
   l.End();
  }
 }

 // ---------- 升神仪式（12 小时）与天穹之门（室外 6 小时） ----------
 public class JobDriver_AscensionRite : JobDriver {
  bool finished;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetB,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   this.FailOn(()=>Ascension.RiteBlocked(pawn)!=null);
   AddFinishAction(c=>{if(!finished)Ascension.RiteEnded(pawn);});
   yield return Toils_Goto.GotoCell(TargetIndex.B,PathEndMode.OnCell);
   var start=ToilMaker.MakeToil("LP_RiteStart");start.initAction=()=>Ascension.RiteStarted(pawn);start.defaultCompleteMode=ToilCompleteMode.Instant;yield return start;
   var wait=ToilMaker.MakeToil("LP_AscensionRite");
   wait.defaultCompleteMode=ToilCompleteMode.Delay;wait.defaultDuration=Ascension.RiteTicks;
   wait.tickIntervalAction=delta=>{pawn.rotationTracker.FaceTarget(TargetA);if(pawn.IsHashIntervalTick(240,delta))FleckMaker.ThrowLightningGlow(pawn.DrawPos,pawn.Map,1.1f);};
   wait.WithProgressBarToilDelay(TargetIndex.B);wait.handlingFacing=true;
   yield return wait;
   yield return Toils_General.Do(()=>{finished=true;Ascension.RiteEnded(pawn);Ascension.CompleteRite(pawn,job.count);});
  }
 }
 public class JobDriver_SkyGate : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOn(()=>AscensionPaths.SkyGateBlocked(pawn)!=null);
   AddFinishAction(c=>AscensionPaths.SkyGateEnded(pawn,c==JobCondition.Succeeded));
   yield return Toils_Goto.GotoCell(TargetIndex.A,PathEndMode.OnCell);
   var start=ToilMaker.MakeToil("LP_SkyGateStart");start.initAction=()=>AscensionPaths.SkyGateStarted(pawn);start.defaultCompleteMode=ToilCompleteMode.Instant;yield return start;
   var wait=ToilMaker.MakeToil("LP_SkyGate");
   wait.defaultCompleteMode=ToilCompleteMode.Delay;wait.defaultDuration=AscensionPaths.SkyGateTicks;
   wait.tickIntervalAction=delta=>{if(pawn.IsHashIntervalTick(250,delta))AscensionPaths.SkyGateTick(pawn);if(pawn.IsHashIntervalTick(300,delta))FleckMaker.ThrowLightningGlow(pawn.DrawPos,pawn.Map,1.4f);};
   wait.WithProgressBarToilDelay(TargetIndex.A);
   yield return wait;
   yield return Toils_General.Do(()=>AscensionPaths.SkyGateDone(pawn));
  }
 }
 // 神性震荡：心灵法术冷却 ×1.5（仪式期间）；神怒风暴：天色变暗、偶尔落雷（天穹之门期间）。
 public class GameCondition_DivineTremor : GameCondition {
  static readonly SkyColorSet Sky=new SkyColorSet(new Color(.95f,.86f,.6f),new Color(.45f,.4f,.3f),new Color(.95f,.9f,.7f),.9f);
  public override int TransitionTicks=>1200;
  public override float SkyTargetLerpFactor(Map map)=>GameConditionUtility.LerpInOutValue(this,TransitionTicks,.3f);
  public override SkyTarget? SkyTarget(Map map)=>new SkyTarget(.9f,Sky,1f,1f);
 }
 public class GameCondition_WrathStorm : GameCondition {
  static readonly SkyColorSet Sky=new SkyColorSet(new Color(.5f,.5f,.62f),new Color(.2f,.2f,.28f),new Color(.6f,.6f,.72f),.8f);
  public override int TransitionTicks=>1200;
  public override float SkyTargetLerpFactor(Map map)=>GameConditionUtility.LerpInOutValue(this,TransitionTicks,.55f);
  public override SkyTarget? SkyTarget(Map map)=>new SkyTarget(.45f,Sky,1f,1f);
  public override void GameConditionTick(){
   base.GameConditionTick();
   if(Find.TickManager.TicksGame%600!=0)return;
   foreach(var map in AffectedMaps)if(Rand.Chance(.35f)){var c=CellFinderLoose.RandomCellWith(x=>!x.Roofed(map) && x.Standable(map),map,100);if(c.IsValid && Ascension.Tracker!=null && map.mapPawns.FreeColonistsSpawned.All(p=>p.Position.DistanceToSquared(c)>36))map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map,c));}
  }
 }

 // ==================== Harmony：按钮、圣坛按钮 ====================
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class AscensionPawnGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   foreach(var g in __result)yield return g;
   Gizmo extra=null;
   try{if(__instance.IsColonistPlayerControlled && Find.Selector.NumSelected==1)extra=Ascension.PathGizmo(__instance);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 成神之路按钮出错："+e,77310023);}
   if(extra!=null)yield return extra;
  }
 }
 [StaticConstructorOnStartup]
 public static class AscensionStartup {
  static AscensionStartup(){
   Building_FaithAltar.ExtraGizmos.Add(AltarGizmos);
   NetherilAscension.ArcaneDemigodGranted+=p=>{try{Demigods.GrantArcane(p);}catch(Exception e){Log.Error("[DND边缘大陆] 奥术半神接入出错："+e);}};
   StatPart_Ascension.Register();
  }
  static IEnumerable<Gizmo> AltarGizmos(Building_FaithAltar altar){
   if(!Ascension.Enabled || !Ascension.Researched)yield break;
   var t=Ascension.Tracker;if(t==null)yield break;
   foreach(var pr in t.progress.Where(x=>!x.done && x.pawn!=null && x.pawn.MapHeld==altar.Map && (x.path==Ascension.Saint || x.path==Ascension.Usurper) && Ascension.RiteStage(x)).ToList()){
    var p=pr.pawn;string why=Ascension.RiteBlocked(p);
    var c=new Command_Action{defaultLabel="升神仪式："+p.LabelShort,defaultDesc=AscensionPaths.RiteDesc(pr),icon=Ascension.PathIcon,action=()=>{if(pr.path==Ascension.Usurper)Find.WindowStack.Add(new Dialog_UsurperShards(p));else if(!Ascension.BeginRite(p))Messages.Message("到不了圣坛旁边。",MessageTypeDefOf.RejectInput,false);}};
    if(why!=null)c.Disable(why);
    yield return c;
   }
  }
 }
}
