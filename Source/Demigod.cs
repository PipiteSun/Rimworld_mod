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
 // 1.40.0 半神（LP_Demigod）：等级 1～5、神职、神力；神灵议会升级；升入神域。数值见需求第一节第 5～7 条。
 public class HediffCompProperties_Demigod : HediffCompProperties {public HediffCompProperties_Demigod(){compClass=typeof(HediffComp_Demigod);}}
 public class HediffComp_Demigod : HediffComp {
  public int level=1;public LP_DivinePortfolioDef portfolio;public float divinePower;public int lastTrialTick=-9999999;
  public string path;
  // 神灵议会
  public bool trialActive,trialDowned;public int trialRaidTick=-1;public Lord trialLord;public Map trialMap;
  public override void CompExposeData(){
   base.CompExposeData();
   Scribe_Values.Look(ref level,"lpLevel",1);Scribe_Defs.Look(ref portfolio,"lpPortfolio");Scribe_Values.Look(ref divinePower,"lpDivinePower");Scribe_Values.Look(ref lastTrialTick,"lpLastTrial",-9999999);Scribe_Values.Look(ref path,"lpPath");
   Scribe_Values.Look(ref trialActive,"lpTrialActive");Scribe_Values.Look(ref trialDowned,"lpTrialDowned");Scribe_Values.Look(ref trialRaidTick,"lpTrialRaid",-1);
   Scribe_References.Look(ref trialLord,"lpTrialLord");Scribe_References.Look(ref trialMap,"lpTrialMap");
  }
 }
 public class Hediff_Demigod : HediffWithComps,IIndexedHediff {
  public HediffComp_Demigod Comp=>this.TryGetComp<HediffComp_Demigod>();
  public int Level=>Comp?.level??1;
  public LP_DivinePortfolioDef Portfolio=>Comp?.portfolio;
  public float Power{get=>Comp?.divinePower??0f;set{var c=Comp;if(c!=null)c.divinePower=Mathf.Clamp(value,0f,MaxPower);}}
  public float MaxPower=>Demigods.MaxPower(Level);
  public float Strength=>Demigods.LevelStrength(Level);
  public override string Label=>Demigods.StageName(Level)+"（"+(Portfolio?.label??"?")+"）";
  public override string LabelBase=>Label;
  public override Color LabelColor=>Portfolio?.color??new Color(1f,.85f,.4f);
  public override string LabelInBrackets=>"神力 "+Power.ToString("0")+" / "+MaxPower.ToString("0");
  public override bool ShouldRemove=>false;
  HediffStage stage;int stageLevel=-1;LP_DivinePortfolioDef stagePortfolio;
  public override HediffStage CurStage{get{
   if(stage!=null && stageLevel==Level && stagePortfolio==Portfolio)return stage;
   stageLevel=Level;stagePortfolio=Portfolio;
   stage=new HediffStage{hungerRateFactor=Demigods.Hunger,makeImmuneTo=Demigods.Diseases,
    statOffsets=new List<StatModifier>{new StatModifier{stat=StatDefOf.PsychicSensitivity,value=Demigods.Sensitivity(Level)},new StatModifier{stat=StatDefOf.MentalBreakThreshold,value=-Demigods.BreakOffset}},
    statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.IncomingDamageFactor,value=Demigods.DamageTaken(Level)}}};
   if(Portfolio?.key=="Fire")stage.statFactors.Add(new StatModifier{stat=StatDefOf.Flammability,value=0f});
   return stage;
  }}
  public override string TipStringExtra{get{
   var sb=new System.Text.StringBuilder(base.TipStringExtra);
   sb.AppendLine("神性等级 "+Level+" / 5："+Demigods.StageName(Level)+"；神职能力强度 ×"+Strength.ToString("0.##"));
   sb.AppendLine("神力 "+Power.ToString("0.#")+" / "+MaxPower.ToString("0")+"；每天 +"+Demigods.DailyGain(pawn).ToString("0.#")+"（2 + 0.5×信徒 + 0.2×好感≥20 的殖民者）");
   if(Portfolio!=null){sb.AppendLine("被动："+(Portfolio.passive??"无"));sb.AppendLine("神域赐福（升入神域后）："+(Portfolio.blessing??"无"));}
   var c=Comp;if(c!=null){if(c.trialActive)sb.AppendLine("神灵议会的试炼进行中"+(c.trialDowned?"（已经倒下过）":""));else if(Level<5)sb.AppendLine("下次议会："+(Demigods.TrialReady(this)?"可以":Demigods.TrialBlocked(this)));}
   return sb.ToString().TrimEnd();
  }}
  int acc;
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if((acc+=delta)<60)return;acc=0;
   var c=Comp;if(c!=null && c.trialActive && pawn.Downed)c.trialDowned=true;
  }
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);HediffIndex<Hediff_Demigod>.Add(this);Demigods.SyncAbilities(pawn);pawn.Drawer?.renderer?.SetAllGraphicsDirty();}
  public override void PostRemoved(){base.PostRemoved();HediffIndex<Hediff_Demigod>.Removed(this);Demigods.SyncAbilities(pawn);}
  // 1.58.6：按小人登记（衰老、移速等热路径要判断是不是半神）。
  public bool IndexLive{get;set;}
  public override void ExposeData(){base.ExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit)HediffIndex<Hediff_Demigod>.Add(this);}
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos()??Enumerable.Empty<Gizmo>())yield return g;
   if(pawn.Faction?.IsPlayer!=true || pawn.Dead)yield break;
   yield return new Gizmo_DivinePower(this);
   var c=Comp;if(c==null)yield break;
   if(Level<5){
    var t=new Command_Action{defaultLabel="接受众神的试炼",defaultDesc="神灵议会：1天后“众神的勇士”来袭（与"+(Portfolio?.label??"")+"对立的神的派系，首领被神明附体）。整场试炼里"+pawn.LabelShort+"一次都没倒下 → 神性等级+1；倒下过 → 神力清零、30天后才能再试，等级不降。\n\n要求：神力满、距上次试炼至少30天、等级低于5。",icon=Demigods.CouncilIcon,action=()=>Demigods.StartTrial(this)};
    string why=Demigods.TrialBlocked(this);if(why!=null)t.Disable(why);
    yield return t;
   }else{
    yield return new Command_Action{defaultLabel="升入神域",defaultDesc=pawn.LabelShort+"已是神域之主。升入神域后会化作光柱离开这个世界（不再回来），释放半神名额；殖民地永久获得“"+(Portfolio?.label??"")+"”的神域赐福："+(Portfolio?.blessing??"")+"。",icon=Demigods.RealmIcon,action=()=>Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(pawn.LabelShort+"将离开这个世界，永远不会回来。殖民地会得到神域赐福，全员心情+15（30天），之后永久+3。确定吗？",()=>Demigods.AscendToRealm(pawn),true,"升入神域"))};
   }
  }
 }
 // 金色神力条（照巫妖精神力条：一条可读的进度条，鼠标悬停看说明）。
 [StaticConstructorOnStartup]
 public class Gizmo_DivinePower : Gizmo {
  static readonly Texture2D Fill=SolidColorMaterials.NewSolidColorTexture(new Color(.95f,.76f,.28f));
  static readonly Texture2D Empty=SolidColorMaterials.NewSolidColorTexture(new Color(.12f,.1f,.06f));
  readonly Hediff_Demigod h;
  public Gizmo_DivinePower(Hediff_Demigod h){this.h=h;Order=-90f;}
  public override float GetWidth(float maxWidth)=>140f;
  public override GizmoResult GizmoOnGUI(Vector2 topLeft,float maxWidth,GizmoRenderParms parms){
   var r=new Rect(topLeft.x,topLeft.y,GetWidth(maxWidth),75f);Widgets.DrawWindowBackground(r);
   var inner=r.ContractedBy(6f);
   Text.Font=GameFont.Small;GUI.DrawTexture(new Rect(inner.x,inner.y,22f,22f),Demigods.DemigodIcon);Widgets.Label(new Rect(inner.x+26f,inner.y,inner.width-26f,22f),"神力");
   var bar=new Rect(inner.x,inner.y+24f,inner.width,inner.height-24f);
   Widgets.FillableBar(bar,h.MaxPower<=0?0:h.Power/h.MaxPower,Fill,Empty,true);
   Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(bar,h.Power.ToString("0")+" / "+h.MaxPower.ToString("0"));Text.Anchor=TextAnchor.UpperLeft;
   if(Mouse.IsOver(r))TooltipHandler.TipRegion(r,"神力："+h.Power.ToString("0.#")+" / "+h.MaxPower.ToString("0")+"\n每天 +"+Demigods.DailyGain(h.pawn).ToString("0.#")+"。神职能力消耗神力；神力满时可以接受众神的试炼。");
   return new GizmoResult(GizmoState.Clear);
  }
 }

 public static class Demigods {
  public const float Hunger=.3f,BreakOffset=.1f,BasePower=100f,PowerPerLevel=50f,DailyBase=2f,DevoteeGain=.5f,FriendGain=.2f,FriendOpinion=20f,UsurpDaily=.1f,MiracleFactor=.5f;
  public const int TrialCooldownDays=30,TrialDelay=GenDate.TicksPerDay,RealmMoodDays=30;
  static HediffDef def;
  public static HediffDef Def=>def??(def=DefDatabase<HediffDef>.GetNamedSilentFail("LP_Demigod"));
  public static Hediff_Demigod Of(Pawn p)=>HediffIndex<Hediff_Demigod>.Get(p); // 1.58.6：按小人登记
  public static bool Is(Pawn p)=>Of(p)!=null;
  public static string StageName(int level)=>level<=1?"新生神祇":level==2?"次级神祇":level==3?"中等神祇":level==4?"高等神祇":"神域之主";
  public static float Sensitivity(int level)=>.2f+.05f*(level-1);
  public static float DamageTaken(int level)=>Mathf.Pow(.95f,level);
  public static float MaxPower(int level)=>BasePower+PowerPerLevel*(level-1);
  public static float LevelStrength(int level)=>1f+.25f*(level-1);
  public static Texture2D CouncilIcon=>ContentFinder<Texture2D>.Get("Ascension/UI/Council",false)??BaseContent.BadTex;
  public static Texture2D DemigodIcon=>ContentFinder<Texture2D>.Get("Ascension/UI/Demigod",false)??BaseContent.BadTex;
  public static Texture2D RealmIcon=>ContentFinder<Texture2D>.Get("Ascension/UI/Realm",false)??BaseContent.BadTex;
  static List<HediffDef> diseases;
  public static List<HediffDef> Diseases=>diseases??(diseases=DefDatabase<HediffDef>.AllDefs.Where(d=>d.isBad && (d.makesSickThought || d.CompProps<HediffCompProperties_Immunizable>()!=null) && !typeof(Hediff_Injury).IsAssignableFrom(d.hediffClass) && d.defName!="LP_Demigod").ToList());

  // ---------- 授予 ----------
  public static Hediff_Demigod Grant(Pawn p,LP_DivinePortfolioDef portfolio,string path){
   if(p==null || Def==null)return null;
   var h=Of(p);
   if(h==null){h=(Hediff_Demigod)HediffMaker.MakeHediff(Def,p);var c=h.Comp;if(c!=null){c.level=1+HeroDestiny.DemigodStartBonus(p);c.portfolio=portfolio;c.path=path;c.divinePower=0f;c.lastTrialTick=Find.TickManager.TicksGame;}p.health.AddHediff(h);}
   else{var c=h.Comp;if(c!=null && c.portfolio==null)c.portfolio=portfolio;}
   var t=Ascension.Tracker;if(t!=null && !t.demigods.Contains(p))t.demigods.Add(p);
   SyncAbilities(p);RefreshCache();
   Adventurer.Get(p)?.SyncDemigod(); // 1.49.0
   if(path!=Ascension.Arcane){
    if(p.Spawned)FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,4f);
    Ascension.Letter("新神诞生："+p.LabelShort,p.LabelShort+"吸收了神性火花，成为“"+(portfolio?.label??"")+"”的半神（新生神祇）。\n\n不老、饥饿×0.3、免疫疾病、心灵敏感度+20%、受到伤害×0.95、精神崩溃阈值-10%。神职能力："+string.Join("、",(portfolio?.abilities??new List<AbilityDef>()).Select(a=>a.label))+"；被动："+(portfolio?.passive??"无")+"。\n\n神力每天恢复，神力满时可以“接受众神的试炼”提升神性等级。",LetterDefOf.PositiveEvent,p);
   }
   return h;
  }
  public static void GrantArcane(Pawn p){
   if(p==null || !NetherilAscension.IsArcaneDemigod(p) || Of(p)!=null)return;
   Grant(p,Ascension.Portfolio("Arcane"),Ascension.Arcane);
  }
  public static void SyncArcane(){
   if(Find.World==null || Find.FactionManager==null || !Find.FactionManager.AllFactionsListForReading.Any(f=>f.IsPlayer))return;
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.ToList())if(NetherilAscension.IsArcaneDemigod(p) && Of(p)==null)GrantArcane(p);
   var t=Ascension.Tracker;if(t==null)return;
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction)if(Of(p)!=null && !t.demigods.Contains(p) && !t.ascended.Contains(p))t.demigods.Add(p);
  }
  public static void SyncAbilities(Pawn p){
   if(p?.abilities==null)return;var h=Of(p);
   var want=h?.Portfolio?.abilities??new List<AbilityDef>();
   foreach(var d in DemigodAbilities.All)if(!want.Contains(d) && p.abilities.GetAbility(d,true)!=null)p.abilities.RemoveAbility(d);
   foreach(var d in want)if(p.abilities.GetAbility(d,true)==null)p.abilities.GainAbility(d);
  }

  // ---------- 神力 ----------
  public static float DailyGain(Pawn p){
   if(p==null)return 0f;var h=Of(p);var gods=h?.Portfolio?.Gods.ToList()??new List<LP_DeityDef>();
   int dev=0,friends=0;
   foreach(var c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists){
    if(c==p)continue;var g=FaithUtility.DevoteeGod(c);
    if(g!=null && gods.Contains(g))dev++;
    else if(c.relations!=null && c.relations.OpinionOf(p)>=FriendOpinion)friends++;
   }
   return DailyBase+DevoteeGain*dev+FriendGain*friends+IdeoBridge.ExtraDevoteeGain(p); // 1.56.0 成神观「神圣使命」：同文化成员也算信徒
  }
  public static void Daily(LP_AscensionTracker t){
   foreach(var p in t.demigods.Where(x=>x!=null && !x.Destroyed && !t.ascended.Contains(x)).ToList()){
    var h=Of(p);if(h==null)continue;
    if(!p.Dead)h.Power+=DailyGain(p);
   }
   // 僭越：殖民地有半神时，除守护神外每位神每天 -0.1；被篡的神、半神巫妖夺走神位的凯兰沃固定 -100。
   if(Faith.Enabled && t.demigods.Any(p=>p!=null && !p.Dead && Of(p)!=null && !t.ascended.Contains(p))){
    var patron=FaithUtility.Patron;
    foreach(var g in FaithUtility.AllDeities)if(g!=patron)FaithUtility.AddFavor(g,-UsurpDaily,"殖民地的半神僭越神位");
   }
   foreach(var g in t.usurped.Concat(t.kelemvorFixed).Distinct())FixFavor(g);
  }
  public static void FixFavor(LP_DeityDef g){if(g==null)return;float v=FaithUtility.GetFavor(g);if(v>-100f)FaithUtility.AddFavor(g,-100f-v,"神位被夺");}
  public static void Hourly(LP_AscensionTracker t){
   foreach(var p in t.demigods.Where(x=>x!=null && !x.Destroyed).ToList()){
    var h=Of(p);var c=h?.Comp;if(c==null || !c.trialActive)continue;
    if(c.trialLord==null){
     if(Find.TickManager.TicksGame<c.trialRaidTick)continue;
     var map=p.MapHeld;if(map==null || !map.IsPlayerHome){c.trialRaidTick=Find.TickManager.TicksGame+GenDate.TicksPerHour*6;continue;}
     SpawnChampions(h,map);
     if(c.trialLord==null){c.trialActive=false;Messages.Message("众神的勇士没能到来（找不到可以派来的派系），试炼取消。",p,MessageTypeDefOf.NeutralEvent,false);}
     continue;
    }
    if(Ascension.LordOver(c.trialLord,c.trialMap))ResolveTrial(h,c.trialDowned);
   }
  }

  // ---------- 神灵议会 ----------
  public static string TrialBlocked(Hediff_Demigod h){
   var c=h?.Comp;if(c==null)return "不是半神";
   if(!Ascension.Enabled)return "成神任务线已关闭。";
   if(c.trialActive)return "试炼正在进行。";
   if(h.Level>=5)return "已经是神域之主。";
   if(h.Power+.01f<h.MaxPower)return "神力没满（"+h.Power.ToString("0")+" / "+h.MaxPower.ToString("0")+"）。";
   int wait=c.lastTrialTick+TrialCooldownDays*GenDate.TicksPerDay-Find.TickManager.TicksGame;if(wait>0)return "距上次试炼不到"+TrialCooldownDays+"天（还要 "+(wait/(float)GenDate.TicksPerDay).ToString("0.#")+" 天）。";
   if(h.pawn.MapHeld==null || !h.pawn.MapHeld.IsPlayerHome)return "要在殖民地里接受试炼。";
   return null;
  }
  public static bool TrialReady(Hediff_Demigod h)=>TrialBlocked(h)==null;
  public static void StartTrial(Hediff_Demigod h){
   if(TrialBlocked(h)!=null)return;var c=h.Comp;
   c.trialActive=true;c.trialDowned=false;c.trialRaidTick=Find.TickManager.TicksGame+TrialDelay;c.trialLord=null;c.trialMap=null;
   Ascension.Letter("众神的试炼",h.pawn.LabelShort+"向神灵议会请求更高的神位。议会回应了：1天后，与“"+h.Portfolio?.label+"”对立的神会派来他们的勇士。\n\n整场试炼里"+h.pawn.LabelShort+"一次都不能倒下。",LetterDefOf.ThreatSmall,h.pawn);
  }
  public static float ChampionPoints(Hediff_Demigod h,Map map)=>Mathf.Max(map==null?0f:StorytellerUtility.DefaultThreatPointsNow(map),2000f+1000f*h.Level)*Ascension.Difficulty;
  public static Lord SpawnChampions(Hediff_Demigod h,Map map){
   var c=h.Comp;var f=Ascension.FactionOfGods(h.Portfolio?.Opposed??Enumerable.Empty<LP_DeityDef>(),out var god);if(f==null)return null;
   var pawns=Ascension.RaidPawns(f,map,ChampionPoints(h,map));if(pawns.Count==0)return null;
   var leader=Ascension.Leader(pawns);var vg=Ascension.VesselGod(god,f,leader);if(leader!=null && vg!=null)Twilight.MakeVessel(leader,vg);
   var lord=Ascension.Arrive(map,f,pawns);c.trialLord=lord;c.trialMap=map;
   Ascension.Letter("众神的勇士",f.Name+"的勇士奉"+(god?.label??"众神")+"之命来考验"+h.pawn.LabelShort+"。"+(leader!=null?"领队"+leader.LabelShort+"被神明附体。":"")+"\n\n这是神灵议会的试炼："+h.pawn.LabelShort+"不能倒下。",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f);
   return lord;
  }
  // 结算（测试直接调用）。
  public static bool ResolveTrial(Hediff_Demigod h,bool downed){
   var c=h?.Comp;if(c==null)return false;
   c.trialActive=false;c.trialLord=null;c.trialMap=null;c.lastTrialTick=Find.TickManager.TicksGame;
   var p=h.pawn;
   if(downed || p.Dead){
    h.Power=0f;
    Ascension.Letter("试炼失败",p.LabelShort+"在众神的勇士面前倒下了：神力清零，"+TrialCooldownDays+"天后才能再请求试炼。神性等级不变。",LetterDefOf.NegativeEvent,p);
    return false;
   }
   c.level=Math.Min(5,c.level+1);
   Adventurer.Milestone(p,"council"+c.level,Adventurer.CouncilXP); // 1.48.0 冒险者里程碑：每个神性等级的试炼各一次
   Adventurer.Get(p)?.SyncDemigod(); // 1.49.0 属性上限 +2、送一次属性提升
   var thought=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_NewGodRises");
   if(thought!=null)foreach(var col in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)col.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
   p.health.Notify_HediffChanged(h);
   IdeoEvents.Record("LP_HE_DemigodRisen",p); // 1.56.0 文化：成神观
   Ascension.Letter("新神崛起",p.LabelShort+"通过了神灵议会的试炼，神性等级提升为"+StageName(c.level)+"（"+c.level+" / 5）。\n\n神力上限 "+h.MaxPower.ToString("0")+"，神职能力强度 ×"+h.Strength.ToString("0.##")+"。全体殖民者心情+8（10天）。"+(c.level>=5?"\n\n"+p.LabelShort+"可以升入神域了。":""),LetterDefOf.PositiveEvent,p);
   return true;
  }

  // ---------- 升入神域 ----------
  public static void AscendToRealm(Pawn p){
   var h=Of(p);var t=Ascension.Tracker;if(h==null || t==null || h.Level<5)return;
   var pf=h.Portfolio;
   if(pf!=null && !t.blessings.Contains(pf))t.blessings.Add(pf);
   if(!t.ascended.Contains(p))t.ascended.Add(p);t.demigods.Remove(p);
   if(t.firstAscendTick<0)t.firstAscendTick=Find.TickManager.TicksGame;
   var thought=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_GodWatches");
   if(thought!=null)foreach(var col in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)if(col!=p)col.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
   var map=p.Map;var cell=p.Position;
   if(p.Spawned){
    for(int i=0;i<6;i++)FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(),map,3f);
    FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,6f);
    map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningFlash(map));
    p.DeSpawn();
   }
   if(p.Faction!=null && p.Faction.IsPlayer){var caravan=p.GetCaravan();caravan?.RemovePawn(p);}
   if(!Find.WorldPawns.Contains(p))Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);
   RefreshCache();
   Ascension.Letter("升入神域",p.LabelShort+"化作一道光柱升上天穹，成为了真正的神。\n\n殖民地永久获得“"+pf?.label+"”的神域赐福："+pf?.blessing+"\n\n全体殖民者“我们的神在看着我们”心情+15（30天），之后永久+3。",LetterDefOf.PositiveEvent,new LookTargets(cell,map));
  }
  public static bool Blessed(string key){var t=Ascension.Tracker;if(t==null)return false;for(int i=0;i<t.blessings.Count;i++)if(t.blessings[i]?.key==key)return true;return false;}

  // ---------- 缓存（被动用：250 刻刷新一次） ----------
  public static List<Pawn> active=new List<Pawn>();
  public static bool colonyMind,colonyDeath;
  public static void RefreshCache(){
   active.Clear();colonyMind=false;colonyDeath=false;var t=Ascension.Tracker;if(t==null)return;
   foreach(var p in t.demigods){if(p==null || p.Dead || p.Destroyed || t.ascended.Contains(p))continue;var h=Of(p);if(h==null)continue;active.Add(p);if(p.Faction?.IsPlayer==true){if(h.Portfolio?.key=="Mind")colonyMind=true;if(h.Portfolio?.key=="Death")colonyDeath=true;}}
  }
  public static bool Near(Pawn p,string key,float radius){
   if(p?.Map==null)return false;
   for(int i=0;i<active.Count;i++){var d=active[i];if(d.Map==p.Map && Of(d)?.Portfolio?.key==key && d.Position.InHorDistOf(p.Position,radius))return true;}
   return false;
  }
  public static Pawn NearDemigod(Pawn p,string key,float radius,Func<Pawn,bool> rel){
   if(p?.Map==null)return null;
   for(int i=0;i<active.Count;i++){var d=active[i];if(d!=p && d.Spawned && d.Map==p.Map && Of(d)?.Portfolio?.key==key && rel(d) && d.Position.InHorDistOf(p.Position,radius))return d;}
   return null;
  }
  public static string Key(Pawn p)=>Of(p)?.Portfolio?.key;
 }

 // ==================== 属性：被动与神域赐福 ====================
 public class StatPart_Ascension : StatPart {
  public string kind;
  float Factor(Pawn p,out string why){
   why=null;float f=1f;if(p==null)return 1f;bool player=p.Faction?.IsPlayer==true;
   switch(kind){
    case "heal":if(player && Demigods.Blessed("Dawn")){f*=1.2f;why="神域赐福（黎明）×1.2";}break;
    case "aim":
     if(Demigods.active.Count>0 && TyrannyNear(p)){f*=.85f;why="暴政半神的威压×0.85";}
     if(Items.Has(p,OblivionFog.Def)){f*=.6f;why=(why==null?"":why+"；")+"遗忘之雾×0.6";}
     break;
    case "suppress":if(player && Demigods.Blessed("Tyranny")){f*=1.25f;why="神域赐福（暴政）×1.25";}break;
    case "work":if(player && Demigods.Blessed("Fire") && StatPart_Faith.Smithing(p)){f*=1.15f;why="神域赐福（火焰，锻造）×1.15";}break;
    case "tame":if(player && Demigods.Blessed("Wild")){f*=1.2f;why="神域赐福（野性）×1.2";}break;
    case "research":if(player && Demigods.Blessed("Mind")){f*=1.1f;why="神域赐福（心灵）×1.1";}break;
    case "focus":if(player && Demigods.Blessed("Magic")){f*=1.1f;why="神域赐福（魔法）×1.1";}break;
    case "move":
     if(Demigods.Key(p)=="Dark" && Faith.Night(p.MapHeld)){f*=1.15f;why="黑暗半神（夜间）×1.15";}
     if(Items.Has(p,WindHaste.Def)){f*=1.1f;why=(why==null?"":why+"；")+"唤风×1.1";}
     break;
   }
   return f;
  }
  float Offset(Pawn p,out string why){
   why=null;if(p==null)return 0f;
   if(kind=="sensitivity" && p.Faction?.IsPlayer==true && Demigods.colonyMind && p.RaceProps.Humanlike){why="心灵半神的共鸣+5%";return .05f;}
   if(kind=="surgery" && p.Faction?.IsPlayer==true && Demigods.Blessed("Death")){why="神域赐福（死亡）+5%";return .05f;}
   return 0f;
  }
  // 1.58.6：原来 Factor 里的 lambda 捕获参数 p，10 个属性对每个小人的每次计算都要分配一次闭包；现在只在附近确实有半神时才进这个方法。
  static bool TyrannyNear(Pawn p)=>Demigods.NearDemigod(p,"Tyranny",10f,d=>d.HostileTo(p))!=null;
  static readonly DefRef<HediffDef> OblivionFog=new DefRef<HediffDef>("LP_OblivionFog"),WindHaste=new DefRef<HediffDef>("LP_CallWindHaste");
  public override void TransformValue(StatRequest req,ref float val){if(!(req.Thing is Pawn p))return;val=val*Factor(p,out _)+Offset(p,out _);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;Factor(p,out var a);Offset(p,out var b);return a==null && b==null?null:"成神："+string.Join("；",new[]{a,b}.Where(s=>s!=null));}
  public static void Register(){
   void Add(string stat,string kind){var d=DefDatabase<StatDef>.GetNamedSilentFail(stat);if(d==null)return;if(d.parts==null)d.parts=new List<StatPart>();if(d.parts.Any(x=>x is StatPart_Ascension f && f.kind==kind))return;d.parts.Add(new StatPart_Ascension{kind=kind,parentStat=d});}
   Add("InjuryHealingFactor","heal");Add("ShootingAccuracyPawn","aim");Add("SuppressionPower","suppress");Add("WorkSpeedGlobal","work");Add("TameAnimalChance","tame");Add("ResearchSpeed","research");Add("MeditationFocusGain","focus");Add("MoveSpeed","move");Add("PsychicSensitivity","sensitivity");Add("MedicalSurgerySuccessChance","surgery");
   // 圣徒：装 VPE 时神恩强度 +0.15。
   var divine=DefDatabase<StatDef>.GetNamedSilentFail("LP_DivinePower");if(divine!=null){if(divine.parts==null)divine.parts=new List<StatPart>();divine.parts.Add(new StatPart_SaintDivine{parentStat=divine});}
  }
 }
 public class StatPart_SaintDivine : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && AscensionPaths.IsSaint(p))val+=.15f;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && AscensionPaths.IsSaint(p)?"圣徒：+0.15":null;
 }
 // 命匣神龛：生命 ×2（物品的生命上限是缓存的属性，StatPart 不生效，直接改 Thing.MaxHitPoints）。
 [HarmonyPatch(typeof(Thing),"get_MaxHitPoints")]
 public static class ShrineHitPoints {static void Postfix(Thing __instance,ref int __result){if(__instance is Phylactery b && AscensionPaths.IsShrine(b))__result*=2;}}
 // ==================== Harmony ====================
 // 不老（同奥术半神：成年后生理衰老速度为 0）。
 [HarmonyPatch(typeof(Pawn_AgeTracker),"get_BiologicalTicksPerTick")]
 public static class DemigodAgelessHook {static void Postfix(Pawn ___pawn,ref float __result){if(__result>0f && ___pawn?.ageTracker!=null && ___pawn.ageTracker.Adult && Demigods.Is(___pawn))__result=0f;}}
 // 半神施放守护神的神术：消耗 ×0.5。
 [HarmonyPatch(typeof(FaithUtility),nameof(FaithUtility.MiracleCost))]
 public static class DemigodMiracleCost {static void Postfix(Pawn p,ref float __result){if(__result>0f && Demigods.Is(p))__result*=Demigods.MiracleFactor;}}
 // 心灵法术冷却：神性震荡 ×1.5、魔法半神 ×0.85、奥术赐福 ×0.9（原版与 VPE 都走 NetherilAscension.CooldownFactor）。
 [HarmonyPatch(typeof(NetherilAscension),nameof(NetherilAscension.CooldownFactor))]
 public static class AscensionCooldownFactor {
  static void Postfix(Pawn p,ref float __result){
   if(p==null)return;
   if(Ascension.TremorDef!=null && NetherilAscension.Active(p.MapHeld,Ascension.TremorDef))__result*=1.5f;
   if(Demigods.Key(p)=="Magic")__result*=.85f;
   if(p.Faction?.IsPlayer==true && Demigods.Blessed("Arcane"))__result*=.9f;
  }
 }
 // 神域赐福（风暴）：雷不劈殖民地建筑。
 [HarmonyPatch(typeof(WeatherEvent_LightningStrike),nameof(WeatherEvent_LightningStrike.FireEvent))]
 public static class StormBlessingLightning {
  static readonly AccessTools.FieldRef<WeatherEvent_LightningStrike,IntVec3> strikeLoc=AccessTools.FieldRefAccess<WeatherEvent_LightningStrike,IntVec3>("strikeLoc");
  static readonly AccessTools.FieldRef<WeatherEvent,Map> mapRef=AccessTools.FieldRefAccess<WeatherEvent,Map>("map");
  static void Prefix(WeatherEvent_LightningStrike __instance){
   if(!Demigods.Blessed("Storm"))return;var map=mapRef(__instance);if(map==null || !map.IsPlayerHome)return;
   var loc=strikeLoc(__instance);if(!loc.IsValid || !loc.InBounds(map))return;
   if(loc.GetEdifice(map)?.Faction?.IsPlayer==true || map.areaManager.Home[loc]){if(CellFinder.TryFindRandomCellNear(loc,map,30,x=>x.InBounds(map) && !x.Roofed(map) && !map.areaManager.Home[x],out var away))strikeLoc(__instance)=away;}
  }
 }
 // 神域赐福（正义）：囚犯越狱 ×0.5（平均间隔 ×2）。
 [HarmonyPatch(typeof(PrisonBreakUtility),nameof(PrisonBreakUtility.InitiatePrisonBreakMtbDays))]
 public static class JusticeBlessingPrison {static void Postfix(ref float __result){if(__result>0f && Demigods.Blessed("Justice"))__result*=2f;}}
 // 神域赐福（黑暗）：夜里不会“在黑暗中”。
 [HarmonyPatch(typeof(ThoughtWorker_Dark),"CurrentStateInternal")]
 public static class DarkBlessing {static void Postfix(Pawn p,ref ThoughtState __result){if(__result.Active && p.Faction?.IsPlayer==true && Demigods.Blessed("Dark") && Faith.Night(p.MapHeld))__result=false;}}
 // 神域赐福（不死）：死灵造物维护费 ×0.8。
 [HarmonyPatch(typeof(Hediff_Construct),"get_Upkeep")]
 public static class UndeathBlessingUpkeep {static void Postfix(Hediff_Construct __instance,ref float __result){if(__result>0f && __instance.pawn?.Faction?.IsPlayer==true && Demigods.Blessed("Undeath"))__result*=.8f;}}
 // 死亡半神：本人看尸体不难受；殖民地有死亡半神时，殖民者死亡的心情惩罚 ×0.5。
 [HarmonyPatch(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)})]
 public static class DeathDemigodMemory {
  static readonly HashSet<string> deaths=new HashSet<string>{"KnowColonistDied","KnowPrisonerDiedInnocent","ColonistLost","KnowColonistExecuted","KnowGuestExecuted"};
  static bool Prefix(MemoryThoughtHandler __instance,Thought_Memory newThought){
   var p=__instance.pawn;var d=newThought?.def;if(p==null || d==null)return true;
   if(MemoryDefs.ObservedCorpse(d) && Demigods.Key(p)=="Death")return false; // 1.58.6：按定义记住
   if(Demigods.colonyDeath && p.Faction?.IsPlayer==true && deaths.Contains(d.defName))newThought.moodPowerFactor*=.5f;
   return true;
  }
 }
 // 正义半神：20 格内的敌方隐形单位现形。
 [HarmonyPatch(typeof(InvisibilityUtility),nameof(InvisibilityUtility.IsPsychologicallyInvisible))]
 public static class JusticeReveal {static void Postfix(Pawn pawn,ref bool __result){if(__result && Demigods.active.Count>0 && pawn.Faction!=null && pawn.HostileTo(Faction.OfPlayer) && JusticeNear(pawn))__result=false;}static bool JusticeNear(Pawn pawn)=>Demigods.NearDemigod(pawn,"Justice",20f,d=>d.HostileTo(pawn))!=null;} // 1.58.6：闭包移进单独方法，隐身判断不再每次分配
 // 野性半神：狂暴的野生动物不选他当目标。
 [HarmonyPatch(typeof(JobGiver_Manhunter),"FindPawnTarget")]
 public static class WildDemigodIgnored {static void Postfix(Pawn pawn,ref Pawn __result){if(__result!=null && pawn?.Faction==null && Demigods.Key(__result)=="Wild")__result=null;}}
 // 伤害：火焰半神免火；黑暗半神夜里伤害 +10%；风暴半神反电近战者；弑神者对灌注/附体/半神 ×1.3、被附体首领技能命中 ×0.7；诸神之祸对它们 ×2。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class AscensionDamage {
  public static bool Divine(Pawn p)=>p!=null && (Twilight.Infused(p)!=null || Twilight.Vessel(p)!=null || Demigods.Is(p));
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   var victim=__instance;var a=dinfo.Instigator as Pawn;float f=1f;
   if(Demigods.active.Count>0){
    if(Demigods.Key(victim)=="Fire" && (dinfo.Def==DamageDefOf.Flame || dinfo.Def==DamageDefOf.Burn))f=0f;
    if(a!=null && Demigods.Key(a)=="Dark" && Faith.Night(a.MapHeld))f*=1.1f;
   }
   if(a!=null){
    if(AscensionPaths.IsGodslayer(a) && Divine(victim))f*=1.3f;
    if(AscensionPaths.IsGodslayer(victim) && Twilight.Vessel(a)!=null && dinfo.Weapon==null)f*=.7f;
    if(dinfo.Weapon!=null && dinfo.Weapon.defName=="LP_Godsbane" && Divine(victim))f*=2f;
   }
   if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.PostApplyDamage))]
 public static class AscensionPostDamage {
  static bool busy;
  static void Postfix(Pawn ___pawn,DamageInfo dinfo,float totalDamageDealt){
   if(busy || ___pawn==null)return;
   try{
    busy=true;var a=dinfo.Instigator as Pawn;
    if(a!=null && totalDamageDealt>0f)AscensionPaths.OnDamage(___pawn,a,dinfo,totalDamageDealt);
    if(a!=null && !a.Dead && Demigods.active.Count>0 && Demigods.Key(___pawn)=="Storm" && !dinfo.Def.isRanged && dinfo.Def!=DamageDefOf.EMP && a!=___pawn && a.Position.AdjacentTo8WayOrInside(___pawn.Position)){
     var h=Demigods.Of(___pawn);a.TakeDamage(new DamageInfo(DemigodAbilities.Electric,8f*DemigodAbilities.Strength(___pawn,false),.3f,-1f,___pawn));
    }
   }finally{busy=false;}
  }
 }
 // 风暴赐福以外，被篡的神对应派系袭击频率 ×1.5。
 [HarmonyPatch(typeof(FactionDef),nameof(FactionDef.RaidCommonalityFromPoints))]
 public static class UsurpedRaidWeight {static void Postfix(FactionDef __instance,ref float __result){var t=Ascension.Tracker;if(__result<=0f || t==null || t.usurped.Count==0)return;var ext=__instance.GetModExtension<FaithExtension>();if(ext!=null && ext.deities.Any(t.usurped.Contains))__result*=1.5f;}}
 public class ThoughtWorker_GodWatchesForever : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){var t=Ascension.Tracker;return t!=null && p.Faction?.IsPlayer==true && t.firstAscendTick>=0 && Find.TickManager.TicksGame>=t.firstAscendTick+Demigods.RealmMoodDays*GenDate.TicksPerDay;}
 }
 public class ThoughtWorker_DawnPresence : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.Faction?.IsPlayer==true && p.Spawned && Demigods.active.Count>0 && Demigods.NearDemigod(p,"Dawn",15f,d=>!d.HostileTo(p))!=null;
 }
}
