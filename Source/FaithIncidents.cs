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
 // 1.39.0 信仰任务（Defs/Faith_Incidents.xml）：朝圣者、神谕试炼、寻回圣物、亵渎者。
 // 只有信仰开着、建了圣坛、选了守护神才会出现；权重 × DND 事件频率（IncidentWorker_DND）× 信仰任务频率。
 public abstract class IncidentWorker_Faith : IncidentWorker_DND {
  public override float ChanceFactorNow(IIncidentTarget target)=>base.ChanceFactorNow(target)*Faith.QuestFactor;
  protected override bool CanFireNowSub(IncidentParms parms){var map=parms.target as Map;return Faith.Enabled && Faith.QuestFactor>0f && map!=null && map.IsPlayerHome && FaithUtility.Patron!=null && FaithUtility.Altars(map).Any() && Faith.Comp!=null && Ready(map);}
  protected virtual bool Ready(Map map)=>true;
 }

 // ==================== 朝圣者 ====================
 public static class Pilgrims {
  public const float Reward=8f,PatronReward=12f,Refuse=5f,Harmed=15f,JoinChance=.2f;
  public const int PrayDays=1;
  public static bool Is(Pawn p)=>p?.GetLord()?.LordJob is LordJob_Pilgrims;
  public static void Prayed(Pawn p){if(p?.GetLord()?.LordJob is LordJob_Pilgrims lj)lj.prayers++;}
  public static Faction PickFaction(){
   var colony=Factions.Colony;if(colony!=null && !colony.HostileTo(Faction.OfPlayer) && !colony.defeated)return colony;
   return Find.FactionManager.AllFactionsVisible.Where(f=>!f.IsPlayer && !f.HostileTo(Faction.OfPlayer) && !f.defeated && f.def.humanlikeFaction && !f.Hidden && f.def.basicMemberKind!=null).RandomElementWithFallback();
  }
  public static List<Pawn> Make(LP_DeityDef god,Faction f,int tile,int n){
   var list=new List<Pawn>();var kind=f?.def?.basicMemberKind??PawnKindDefOf.Villager;
   for(int i=0;i<n;i++){
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,f,PawnGenerationContext.NonPlayer,tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:false));
    if(p.story?.traits!=null){foreach(var t in p.story.traits.allTraits.Where(t=>t.def==Faith.Devotee || t.def==Faith.Atheist).ToList())p.story.traits.RemoveTrait(t);if(god.devoteeDegree>0)p.story.traits.GainTrait(new Trait(Faith.Devotee,god.devoteeDegree,true));}
    list.Add(p);
   }
   return list;
  }
  public static Lord Arrive(Map map,List<Pawn> pawns,LP_DeityDef god){
   var altar=FaithUtility.Altars(map).OfType<Building_FaithAltar>().FirstOrDefault();if(altar==null || pawns.Count==0)return null;
   if(!RCellFinder.TryFindRandomPawnEntryCell(out var edge,map,CellFinder.EdgeRoadChance_Friendly))edge=CellFinder.RandomEdgeCell(map);
   foreach(var p in pawns){if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(edge,map,4),map);}
   return LordMaker.MakeNewLord(pawns[0].Faction,new LordJob_Pilgrims(god,altar),map,pawns);
  }
 }
 public class IncidentWorker_FaithPilgrims : IncidentWorker_Faith {
  protected override bool Ready(Map map)=>Pilgrims.PickFaction()!=null && !map.lordManager.lords.Any(l=>l.LordJob is LordJob_Pilgrims);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;var patron=FaithUtility.Patron;
   var god=Rand.Chance(.5f)?patron:FaithUtility.Worshippable.RandomElement();
   var f=Pilgrims.PickFaction();if(f==null || god==null)return false;
   var pawns=Pilgrims.Make(god,f,map.Tile,Rand.RangeInclusive(3,5));
   foreach(var p in pawns)Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);
   var letter=(ChoiceLetter_FaithPilgrims)LetterMaker.MakeLetter("朝圣者",pawns.Count+"名来自"+f.Name+"的"+god.label+"信徒想来殖民地的圣坛前祈祷1天。\n\n接待他们："+god.label+"神眷+"+Pilgrims.Reward+(god==patron?"（守护神+"+Pilgrims.PatronReward+"）":"")+"，可能有一人请求留下。拒绝："+god.label+"神眷-"+Pilgrims.Refuse+"。\n\n注意：朝圣期间圣坛被毁或有朝圣者被打，神眷-"+Pilgrims.Harmed+"，他们会愤然离开。",DefDatabase<LetterDef>.GetNamed("LP_FaithPilgrimsLetter"),new LookTargets(FaithUtility.Altars(map).First()),f);
   letter.pawns=pawns;letter.god=god;letter.map=map;letter.StartTimeout(GenDate.TicksPerDay);
   Find.LetterStack.ReceiveLetter(letter);
   return true;
  }
 }
 public class ChoiceLetter_FaithPilgrims : ChoiceLetter {
  public List<Pawn> pawns=new List<Pawn>();public LP_DeityDef god;public Map map;
  public override bool CanDismissWithRightClick=>false;
  public override IEnumerable<DiaOption> Choices{get{
   if(ArchivedOnly){yield return Option_Close;yield break;}
   yield return new DiaOption("接待"){action=()=>{Accept();Find.LetterStack.RemoveLetter(this);},resolveTree=true,disabled=map==null || !FaithUtility.Altars(map).Any(),disabledReason="没有圣坛了"};
   yield return new DiaOption("拒绝"){action=()=>{Refuse();Find.LetterStack.RemoveLetter(this);},resolveTree=true};
   yield return Option_Postpone;
  }}
  public Lord Accept()=>map==null?null:Pilgrims.Arrive(map,pawns.Where(p=>p!=null && !p.Dead && !p.Destroyed).ToList(),god);
  public void Refuse(){Faith.Gain(god,-Pilgrims.Refuse,"拒绝了朝圣者");foreach(var p in pawns)if(p!=null && !p.Destroyed && !p.Spawned)Find.WorldPawns.RemoveAndDiscardPawnViaGC(p);}
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref pawns,"lpPawns",LookMode.Reference);Scribe_Defs.Look(ref god,"lpGod");Scribe_References.Look(ref map,"lpMap");if(Scribe.mode==LoadSaveMode.PostLoadInit)pawns=pawns?.Where(p=>p!=null).ToList()??new List<Pawn>();}
 }
 public class LordJob_Pilgrims : LordJob {
  public LP_DeityDef god;public Building_FaithAltar altar;public int prayers;public bool done;
  public LordJob_Pilgrims(){}
  public LordJob_Pilgrims(LP_DeityDef god,Building_FaithAltar altar){this.god=god;this.altar=altar;}
  public override bool AddFleeToil=>false;
  public override StateGraph CreateGraph(){
   var g=new StateGraph();
   var travel=new LordToil_Travel(altar?.InteractionCell??IntVec3.Zero);g.StartingToil=travel;
   var pray=new LordToil_PilgrimPray();g.AddToil(pray);
   var leave=new LordToil_ExitMap(LocomotionUrgency.Walk,true);g.AddToil(leave);
   var arrive=new Transition(travel,pray);arrive.AddTrigger(new Trigger_Memo("TravelArrived"));arrive.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerHour*6));g.AddTransition(arrive);
   var finish=new Transition(pray,leave);finish.AddTrigger(new Trigger_TicksPassed(Pilgrims.PrayDays*GenDate.TicksPerDay));finish.AddPreAction(new TransitionAction_Custom(()=>Finish(true,null)));g.AddTransition(finish);
   var harmed=new Transition(travel,leave);harmed.AddSource(pray);harmed.AddTrigger(new Trigger_PawnHarmed(1f,true,Faction.OfPlayer));harmed.AddPreAction(new TransitionAction_Custom(()=>Finish(false,"有朝圣者被殖民地的人打伤了")));g.AddTransition(harmed);
   var lost=new Transition(travel,leave);lost.AddSource(pray);lost.AddTrigger(new Trigger_TickCondition(()=>altar==null || altar.Destroyed,60));lost.AddPreAction(new TransitionAction_Custom(()=>Finish(false,"圣坛被毁了")));g.AddTransition(lost);
   return g;
  }
  public void Finish(bool ok,string why){
   if(done)return;done=true;var patron=FaithUtility.Patron;
   if(ok){
    float r=god==patron?Pilgrims.PatronReward:Pilgrims.Reward;Faith.Gain(god,r,"接待了朝圣者");
    string join="";
    var stay=lord?.ownedPawns.Where(p=>!p.Dead && !p.Downed && p.RaceProps.Humanlike).RandomElementWithFallback();
    if(stay!=null && Rand.Chance(Pilgrims.JoinChance)){lord.RemovePawn(stay);stay.SetFaction(Faction.OfPlayer);join="\n\n"+stay.LabelShort+"请求留在殖民地，侍奉圣坛：现在是你的殖民者了。";}
    Find.LetterStack.ReceiveLetter("朝圣结束","朝圣者在圣坛前祈祷了一天，满意地离开了："+god.label+"神眷+"+(r*Faith.GainFactor).ToString("0.#")+"。"+join,LetterDefOf.PositiveEvent,stay);
   }else{
    Faith.Gain(god,-Pilgrims.Harmed,"朝圣者受辱："+why);
    Find.LetterStack.ReceiveLetter("朝圣者愤然离开",why+"。朝圣者愤然离开："+god.label+"神眷-"+Pilgrims.Harmed+"。",LetterDefOf.NegativeEvent);
   }
  }
  public override void ExposeData(){Scribe_Defs.Look(ref god,"lpGod");Scribe_References.Look(ref altar,"lpAltar");Scribe_Values.Look(ref prayers,"lpPrayers");Scribe_Values.Look(ref done,"lpDone");}
 }
 // 在圣坛前一个接一个地祈祷（LP_Pray），其余时间在附近待着。
 public class LordToil_PilgrimPray : LordToil {
  Building_FaithAltar Altar=>(lord.LordJob as LordJob_Pilgrims)?.altar;
  public override void UpdateAllDuties(){var a=Altar;foreach(var p in lord.ownedPawns)p.mindState.duty=new PawnDuty(DutyDefOf.Defend,a!=null?a.Position:p.Position,6f);}
  public override void LordToilTick(){
   if(Find.TickManager.TicksGame%120!=7)return;var a=Altar;if(a==null || a.Destroyed)return;
   foreach(var p in lord.ownedPawns.ToList()){
    if(p.Dead || p.Downed || p.InMentalState || p.CurJobDef==Faith.PrayJob)continue;
    var job=Faith.PrayJobFor(p,a);if(job!=null)p.jobs.StartJob(job,JobCondition.InterruptForced);
   }
  }
 }

 // ==================== 神谕试炼 ====================
 public class IncidentWorker_FaithTrial : IncidentWorker_Faith {
  protected override bool Ready(Map map)=>FaithUtility.GetFavor(FaithUtility.Patron)>=FaithQuests.TrialThreshold && Faith.Comp.trialGod==null && FaithQuests.TrialTarget(FaithUtility.Patron.key)>0;
  protected override bool TryExecuteWorker(IncidentParms parms)=>FaithQuests.StartTrial((Map)parms.target);
 }
 // ==================== 寻回圣物 ====================
 public class IncidentWorker_FaithLostRelic : IncidentWorker_Faith {
  protected override bool Ready(Map map)=>FaithUtility.GetFavor(FaithUtility.Patron)>=FaithQuests.LostRelicThreshold && Faith.Comp.lostRelicGod==null && FaithUtility.Patron.relic!=null && FaithQuests.GuardFaction(FaithUtility.Patron)!=null;
  protected override bool TryExecuteWorker(IncidentParms parms)=>FaithQuests.StartLostRelic((Map)parms.target,parms.points)!=null;
 }
 // ==================== 亵渎者 ====================
 public class IncidentWorker_FaithDesecrators : IncidentWorker_Faith {
  protected override bool Ready(Map map)=>Faith.Comp.desecrators==null && FaithQuests.Desecrator(FaithUtility.Patron,out _,out _);
  protected override bool TryExecuteWorker(IncidentParms parms)=>FaithQuests.StartDesecrators((Map)parms.target,parms.points)!=null;
 }

 public static class FaithQuests {
  public const float TrialThreshold=30f,TrialReward=15f,TrialFail=10f,TrialRelicChance=.3f,LostRelicThreshold=40f,LostRelicReward=20f,LostRelicFail=5f,DesecratorFactor=.6f,DesecratorWin=10f,DesecratorAltarLost=20f;
  public const int TrialDays=10,LostRelicDays=15,RelicOfferRadius=4;
  public static float TrialTarget(string key){switch(key){case "Lathander":return 200f;case "Tyr":return 1f;case "Mystra":return 10f;case "Kelemvor":return 3f;case "Malar":return 5f;case "Talos":return 1f;case "Bane":return 1f;case "Shar":return 5f;case "Surtr":return 3f;}return 0f;}
  public static string TrialText(string key){switch(key){case "Lathander":return "收获200份作物";case "Tyr":return "释放1名囚犯，或举行一次审判仪式";case "Mystra":return "施放10次神术或法术";case "Kelemvor":return "安葬3具尸体";case "Malar":return "狩猎5只动物";case "Talos":return "熬过一场雷暴（风暴马上就来）";case "Bane":return "奴役1名囚犯";case "Shar":return "在夜里（22点～5点）祈祷5次";case "Surtr":return "在锻造台上锻造3件武器或护甲";}return "";}
  public static bool StartTrial(Map map){
   var c=Faith.Comp;var god=FaithUtility.Patron;if(c==null || god==null || TrialTarget(god.key)<=0f)return false;
   c.trialGod=god;c.trialUntil=Faith.Now+TrialDays*GenDate.TicksPerDay;c.trialProgress=0f;c.trialTarget=TrialTarget(god.key);
   if(god.key=="Talos")TwilightSkills.Storm(map);
   Find.LetterStack.ReceiveLetter("神谕试炼："+god.label,"祭司们得到了"+god.FullLabel+"的神谕：在"+TrialDays+"天内"+TrialText(god.key)+"。\n\n成功：神眷+"+TrialReward+"，30%的几率得到"+god.label+"的神赐遗物（已经有的话换成2片神迹残片）。失败：神眷-"+TrialFail+"。",LetterDefOf.NeutralEvent);
   return true;
  }
  public static void TrialSucceeded(WorldComponent_Faith c){
   var god=c.trialGod;if(god==null)return;c.trialGod=null;c.trialUntil=-1;
   Faith.Gain(god,TrialReward,"神谕试炼成功");
   string extra="";var map=Faith.HomeMaps.FirstOrDefault();
   if(map!=null && Rand.Chance(TrialRelicChance)){
    bool have=c.relicGranted.Contains(god) || (god.relic!=null && Find.Maps.Any(m=>m.listerThings.ThingsOfDef(god.relic).Any()));
    if(!have && god.relic!=null){var r=Faith.MakeRelic(god);c.relicGranted.Add(god);Faith.DropNearAltar(r,map);extra="\n\n"+god.label+"降下了神赐遗物："+r.LabelNoCount+"。";}
    else if(Faith.ShardDef!=null){var s=ThingMaker.MakeThing(Faith.ShardDef);s.stackCount=2;Faith.DropNearAltar(s,map);extra="\n\n圣坛旁出现了2片神迹残片。";}
   }
   Find.LetterStack.ReceiveLetter("神谕试炼完成",god.FullLabel+"的试炼完成了：神眷+"+(TrialReward*Faith.GainFactor).ToString("0.#")+"。"+extra,LetterDefOf.PositiveEvent);
  }
  // 寻回圣物
  public static Faction GuardFaction(LP_DeityDef god){
   if(god==null)return null;
   foreach(var e in god.Enemies)foreach(var f in Find.FactionManager.AllFactionsListForReading.Where(f=>!f.IsPlayer && !f.defeated && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction))if(f.def.GetModExtension<FaithExtension>()?.deities.Contains(e)==true)return f;
   return Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f=>f.def.defName=="Pirate" && !f.defeated && f.HostileTo(Faction.OfPlayer));
  }
  public static Site StartLostRelic(Map map,float points){
   var c=Faith.Comp;var god=FaithUtility.Patron;var f=GuardFaction(god);if(c==null || god?.relic==null || f==null)return null;
   if(!TileFinder.TryFindNewSiteTile(out var tile,map.Tile,2,10))return null;
   var stash=DefDatabase<SitePartDef>.GetNamedSilentFail("ItemStash");var outpost=DefDatabase<SitePartDef>.GetNamedSilentFail("Outpost")??SitePartDefOf.BanditCamp;if(stash==null)return null;
   float pts=Mathf.Max(points,StorytellerUtility.DefaultSiteThreatPointsNow());
   var site=SiteMaker.MakeSite(new[]{new SitePartDefWithParams(stash,new SitePartParams{threatPoints=pts}),new SitePartDefWithParams(outpost,new SitePartParams{threatPoints=pts})},tile,f,true,null);
   var relic=Faith.MakeRelic(god);
   var part=site.parts.FirstOrDefault(p=>p.def==stash);if(part.things==null)part.things=new ThingOwner<Thing>(part);part.things.TryAdd(relic);
   site.GetComponent<TimeoutComp>()?.StartTimeout(LostRelicDays*GenDate.TicksPerDay);
   Find.WorldObjects.Add(site);
   c.lostRelicGod=god;c.lostRelic=relic;c.lostRelicUntil=Faith.Now+LostRelicDays*GenDate.TicksPerDay;c.lostRelicSite=site;
   Find.LetterStack.ReceiveLetter("寻回圣物："+relic.LabelNoCount,god.FullLabel+"的神赐遗物"+relic.LabelNoCount+"落在了"+f.Name+"手里，被藏在附近的一处据点里。\n\n"+LostRelicDays+"天内把它带回来、放到圣坛旁边（"+RelicOfferRadius+"格内）就算供奉：神眷+"+LostRelicReward+"，圣物可以留着用。过期没带回来：神眷-"+LostRelicFail+"。",LetterDefOf.PositiveEvent,new LookTargets(site));
   return site;
  }
  // 亵渎者：守护神的敌对神有对应派系、且这个派系敌对。
  public static bool Desecrator(LP_DeityDef patron,out LP_DeityDef enemy,out Faction faction){
   enemy=null;faction=null;if(patron==null)return false;
   foreach(var e in patron.Enemies)foreach(var f in Find.FactionManager.AllFactionsListForReading.Where(f=>!f.IsPlayer && !f.defeated && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction && f.def.pawnGroupMakers!=null && f.def.pawnGroupMakers.Any(g=>g.kindDef==PawnGroupKindDefOf.Combat)))if(f.def.GetModExtension<FaithExtension>()?.deities.Contains(e)==true){enemy=e;faction=f;return true;}
   return false;
  }
  public static Lord StartDesecrators(Map map,float points){
   var c=Faith.Comp;if(c==null || !Desecrator(FaithUtility.Patron,out var enemy,out var f))return null;
   var altar=FaithUtility.Altars(map).FirstOrDefault();if(altar==null)return null;
   var parms=new IncidentParms{target=map,faction=f,points=Mathf.Max(f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat),(points>0?points:StorytellerUtility.DefaultThreatPointsNow(map))*DesecratorFactor),raidStrategy=RaidStrategyDefOf.ImmediateAttack,raidArrivalMode=PawnsArrivalModeDefOf.EdgeWalkIn};
   var pawns=PawnGroupMakerUtility.GeneratePawns(IncidentParmsUtility.GetDefaultPawnGroupMakerParms(PawnGroupKindDefOf.Combat,parms,true)).ToList();if(pawns.Count==0)return null;
   var leader=pawns.FirstOrDefault(p=>p.kindDef.factionLeader && Twilight.Eligible(p))??pawns.FirstOrDefault(Twilight.Eligible);
   if(leader!=null && enemy.degree>0)Twilight.Infuse(leader,enemy);
   parms.raidArrivalMode.Worker.Arrive(pawns,parms);
   var lord=LordMaker.MakeNewLord(f,new LordJob_AssaultThings(f,new List<Thing>{altar},1f,false),map,pawns);
   c.desecrators=lord;c.desecratorMap=map;c.desecratorStart=Faith.Now;
   Find.LetterStack.ReceiveLetter("亵渎者",f.Name+"的"+enemy.label+"信徒来亵渎"+FaithUtility.Patron.label+"的圣坛！领队"+(leader!=null?leader.LabelShort+"身上带着"+enemy.label+"的神力":"")+"。\n\n圣坛被毁：神眷-"+DesecratorAltarLost+"。打退他们：神眷+"+DesecratorWin+"，掉一片神迹残片。",LetterDefOf.ThreatSmall,new LookTargets(pawns.Cast<Thing>()),f);
   return lord;
  }
  public static void AltarDestroyed(Map map){
   var c=Faith.Comp;if(c==null || map==null)return;
   if(c.desecrators!=null && c.desecratorMap==map && !FaithUtility.Altars(map).Any()){Faith.Gain(FaithUtility.Patron,-DesecratorAltarLost,"亵渎者毁了圣坛");c.desecrators=null;c.desecratorMap=null;}
  }
  public static void HourlyCheck(WorldComponent_Faith c){
   // 亵渎者：袭击结束（领主没了或没人还站着）且圣坛还在 → 打退了。
   if(c.desecrators!=null){
    var lord=c.desecrators;var map=c.desecratorMap;
    bool over=map==null || !map.lordManager.lords.Contains(lord) || lord.ownedPawns.All(p=>p.Dead || p.Downed || !p.Spawned);
    if(over){
     c.desecrators=null;c.desecratorMap=null;
     if(map!=null && FaithUtility.Altars(map).Any()){Faith.Gain(FaithUtility.Patron,DesecratorWin,"打退了亵渎者");if(Faith.ShardDef!=null){var s=ThingMaker.MakeThing(Faith.ShardDef);Faith.DropNearAltar(s,map);}Find.LetterStack.ReceiveLetter("打退了亵渎者","圣坛保住了："+FaithUtility.Patron?.label+"神眷+"+(DesecratorWin*Faith.GainFactor).ToString("0.#")+"，圣坛旁留下了一片神迹残片。",LetterDefOf.PositiveEvent);}
    }
   }
   // 寻回圣物：放到圣坛旁边就算供奉。
   if(c.lostRelic!=null && c.lostRelic.Spawned && c.lostRelic.Map.IsPlayerHome && FaithUtility.Altars(c.lostRelic.Map).Any(a=>a.Position.InHorDistOf(c.lostRelic.Position,RelicOfferRadius+1))){
    var god=c.lostRelicGod;var relic=c.lostRelic;c.lostRelic=null;c.lostRelicGod=null;c.lostRelicUntil=-1;c.lostRelicSite=null;
    if(god!=null && !c.relicGranted.Contains(god))c.relicGranted.Add(god);
    Faith.Gain(god,LostRelicReward,"供奉了寻回的"+relic.LabelNoCount);
    Find.LetterStack.ReceiveLetter("圣物归来",relic.LabelNoCount+"回到了"+god?.label+"的圣坛：神眷+"+(LostRelicReward*Faith.GainFactor).ToString("0.#")+"。圣物可以留着用。",LetterDefOf.PositiveEvent,relic);
   }
   // 塔洛斯的试炼：风暴过去了（开始 6 小时后地图上不再是雷暴）。
   if(c.trialGod?.key=="Talos" && c.trialUntil>0 && Faith.Now-(c.trialUntil-TrialDays*GenDate.TicksPerDay)>GenDate.TicksPerHour*6 && !Faith.HomeMaps.Any(Faith.Storm))c.TrialProgress("Talos",1f);
  }
  public static void DailyCheck(WorldComponent_Faith c){
   if(c.trialGod!=null && Faith.Now>=c.trialUntil){var god=c.trialGod;c.trialGod=null;c.trialUntil=-1;Faith.Gain(god,-TrialFail,"神谕试炼失败");Find.LetterStack.ReceiveLetter("神谕试炼失败",god.FullLabel+"的试炼没能完成："+god.label+"神眷-"+TrialFail+"。",LetterDefOf.NegativeEvent);}
   if(c.lostRelicGod!=null && Faith.Now>=c.lostRelicUntil){
    var r=c.lostRelic;bool held=r!=null && !r.Destroyed && ((r.MapHeld!=null && r.MapHeld.IsPlayerHome) || r.ParentHolder is Pawn_InventoryTracker || r.ParentHolder is Pawn_EquipmentTracker || r.ParentHolder is Pawn_ApparelTracker);
    if(!held){var god=c.lostRelicGod;c.lostRelicGod=null;c.lostRelic=null;c.lostRelicUntil=-1;c.lostRelicSite=null;Faith.Gain(god,-LostRelicFail,"没能寻回圣物");Find.LetterStack.ReceiveLetter("圣物失落",god.label+"的圣物没能寻回：神眷-"+LostRelicFail+"。",LetterDefOf.NegativeEvent);}
   }
   var dead=c.appeals.Where(kv=>Faith.Now>=kv.Value).Select(kv=>kv.Key).ToList();foreach(var k in dead)c.appeals.Remove(k);
  }
 }
}
