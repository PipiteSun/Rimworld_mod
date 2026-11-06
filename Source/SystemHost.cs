using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.122.0 系统宿主（LP_SystemHost，史诗）：照百战觉醒（IsekaiKillMilestones 的 ProgressStore / Rewards / MilestoneWindow）移植，不依赖那个模组。
 // 每 7 天发布 3 个任务，选 1 个接受（期限 7 天，同时只有一个）；进度 = pawn.records 的快照差值，不加 Harmony。
 // 完成 → 一次待领取奖励（三选一：+1 属性点 / 改命符 ×1 / 随机物品 ×1），排队弹窗；超时 → 系统惩罚 3 天（全局工作速度 ×0.85）；主动放弃不罚。
 // 失去特性后不再发任务（进行中的任务作废、不罚），已有的待领取奖励仍可领。
 public enum HostTaskKind { Kill, Craft, Tend, Harvest, Research, Capture }

 public sealed class HostTask : IExposable {
  public HostTaskKind kind;public int target;public float start;public int acceptTick=-1,deadline;
  public string Record=>SystemHost.RecordOf(kind);
  public string Label=>SystemHost.TaskLabel(kind,target);
  public void ExposeData(){
   Scribe_Values.Look(ref kind,"kind");Scribe_Values.Look(ref target,"target");Scribe_Values.Look(ref start,"start");
   Scribe_Values.Look(ref acceptTick,"acceptTick",-1);Scribe_Values.Look(ref deadline,"deadline");
  }
 }

 public sealed class HostReward : IExposable {
  public int number;public string task,item;public int count=1;
  public string Label=>"第 "+number+" 次任务奖励（"+task+"）";
  public void ExposeData(){
   Scribe_Values.Look(ref number,"number");Scribe_Values.Look(ref task,"task");Scribe_Values.Look(ref item,"item");Scribe_Values.Look(ref count,"count",1);
  }
 }

 public sealed class HostProgress : IExposable {
  public int pawnId,nextOfferTick=-1,completed,failed,abandoned,claimed;
  public HostTask active;
  public List<HostTask> offers=new List<HostTask>();
  public List<HostReward> rewards=new List<HostReward>();
  public List<string> history=new List<string>();
  public void Log(string line){history.Insert(0,"第 "+(GenDate.DaysPassed+1)+" 天："+line);if(history.Count>60)history.RemoveAt(history.Count-1);}
  public void ExposeData(){
   Scribe_Values.Look(ref pawnId,"pawnId");Scribe_Values.Look(ref nextOfferTick,"nextOfferTick",-1);
   Scribe_Values.Look(ref completed,"completed");Scribe_Values.Look(ref failed,"failed");Scribe_Values.Look(ref abandoned,"abandoned");Scribe_Values.Look(ref claimed,"claimed");
   Scribe_Deep.Look(ref active,"active");
   Scribe_Collections.Look(ref offers,"offers",LookMode.Deep);
   Scribe_Collections.Look(ref rewards,"rewards",LookMode.Deep);
   Scribe_Collections.Look(ref history,"history",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){offers=offers??new List<HostTask>();rewards=rewards??new List<HostReward>();history=history??new List<string>();}
  }
 }

 public static class SystemHost {
  public const int Period=GenDate.TicksPerDay*7,Deadline=GenDate.TicksPerDay*7,PenaltyTicks=GenDate.TicksPerDay*3,StatPoints=1,OfferCount=3;
  static readonly DefRef<TraitDef> def=new DefRef<TraitDef>("LP_SystemHost");
  static readonly DefRef<ThingDef> charm=new DefRef<ThingDef>("LP_FateRewriteCharm");
  static readonly DefRef<HediffDef> penalty=new DefRef<HediffDef>("LP_SystemPenalty");
  public static TraitDef Def=>def.Def;public static ThingDef CharmDef=>charm.Def;public static HediffDef PenaltyDef=>penalty.Def;
  public static GameComponent_SystemHost State=>GameComp<GameComponent_SystemHost>.Get();
  public static bool Has(Pawn p)=>Def!=null && p?.story?.traits!=null && p.story.traits.HasTrait(Def);
  public static bool Eligible(Pawn p)=>p!=null && !p.Dead && !p.Destroyed && p.Faction!=null && p.Faction.IsPlayer;
  public static bool NoPopup; // 自测：不弹窗

  // 随机物品：与百战觉醒完全相同的六种 Isekai 物品；没装 Isekai 时从 DND 物品池抽。
  public static readonly string[] IsekaiItems={"Isekai_SmallManaCore","Isekai_ManaCore","Isekai_BigManaCore","Isekai_ReinforcementCore","Isekai_ManaEssence","Isekai_StarFragment"};
  public static readonly KeyValuePair<string,int>[] DndItems={
   new KeyValuePair<string,int>("LP_PotionHealing",2),new KeyValuePair<string,int>("LP_PotionGreaterHealing",1),new KeyValuePair<string,int>("LP_PotionHeroism",1),
   new KeyValuePair<string,int>("LP_PlanarResidue",5),new KeyValuePair<string,int>("LP_Mithral",10)};
  public static bool? TestIsekai; // 自测：假装没有 Isekai（false）
  static List<ThingDef> IsekaiPool()=>IsekaiItems.Select(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)).Where(d=>d!=null && d.category==ThingCategory.Item && !d.MadeFromStuff).ToList();
  public static bool UsesIsekaiPool=>TestIsekai??IsekaiPool().Count>0;
  public static bool InPool(string defName,int count){
   if(defName==null)return false;
   if(IsekaiItems.Contains(defName))return count==1;
   return DndItems.Any(kv=>kv.Key==defName && kv.Value==count);
  }
  public static HostReward RollReward(int number,string task){
   var r=new HostReward{number=number,task=task};
   if(UsesIsekaiPool){var pool=IsekaiPool();if(pool.Count>0){r.item=pool.RandomElement().defName;r.count=1;return r;}}
   var dnd=DndItems.Where(kv=>DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key)!=null).ToList();
   if(dnd.Count>0){var kv=dnd.RandomElement();r.item=kv.Key;r.count=kv.Value;}
   return r;
  }

  // ---------- 任务 ----------
  public static string RecordOf(HostTaskKind k){
   switch(k){case HostTaskKind.Kill:return "Kills";case HostTaskKind.Craft:return "ThingsCrafted";case HostTaskKind.Tend:return "TimesTendedOther";
    case HostTaskKind.Harvest:return "PlantsHarvested";case HostTaskKind.Research:return "ResearchPointsResearched";default:return "PeopleCaptured";}
  }
  public static int TargetFor(HostTaskKind k,int L){
   switch(k){case HostTaskKind.Kill:return 3+L/2;case HostTaskKind.Craft:return 10+L;case HostTaskKind.Tend:return 6;
    case HostTaskKind.Harvest:return 15+L;case HostTaskKind.Research:return 1500+150*L;default:return 1;}
  }
  public static string TaskLabel(HostTaskKind k,int n){
   switch(k){case HostTaskKind.Kill:return "击杀 "+n+" 个敌人或野兽";case HostTaskKind.Craft:return "完成 "+n+" 次制作";case HostTaskKind.Tend:return "治疗他人 "+n+" 次";
    case HostTaskKind.Harvest:return "收获作物 "+n+" 次";case HostTaskKind.Research:return "研究 "+n+" 点";default:return "俘获 "+n+" 名囚犯";}
  }
  public static int LevelOf(Pawn p)=>Adventurer.Get(p)?.level??1;
  public static float RecordValue(Pawn p,string record){var d=DefDatabase<RecordDef>.GetNamedSilentFail(record);return d==null || p?.records==null?0f:p.records.GetValue(d);}
  public static float Done(Pawn p,HostTask t)=>t==null?0f:Mathf.Max(0f,RecordValue(p,t.Record)-t.start);
  public static List<HostTask> RollOffers(Pawn p){
   int L=LevelOf(p);
   return Enum.GetValues(typeof(HostTaskKind)).Cast<HostTaskKind>().InRandomOrder().Take(OfferCount).Select(k=>new HostTask{kind=k,target=TargetFor(k,L)}).ToList();
  }
  public static void Release(Pawn p,HostProgress e){
   e.offers=RollOffers(p);e.nextOfferTick=Find.TickManager.TicksGame+Period;
   Messages.Message("系统发布了新任务："+p.LabelShort+" 可以在「系统面板」里从 "+e.offers.Count+" 个任务中选一个接受。",p,MessageTypeDefOf.NeutralEvent,false);
   State?.Notify(p);
  }
  public static bool Accept(Pawn p,HostTask t,out string error){
   error="任务已经变化，请重新打开系统面板。";
   var e=State?.Lookup(p);if(e==null || !Has(p) || e.active!=null || t==null || !e.offers.Contains(t))return false;
   t.start=RecordValue(p,t.Record);t.acceptTick=Find.TickManager.TicksGame;t.deadline=t.acceptTick+Deadline;
   e.active=t;e.offers.Clear();e.Log("接受任务「"+t.Label+"」");error=null;return true;
  }
  public static bool Abandon(Pawn p){
   var e=State?.Lookup(p);if(e?.active==null)return false;
   e.Log("放弃任务「"+e.active.Label+"」（主动放弃，没有惩罚）");e.active=null;e.abandoned++;return true;
  }
  // 每 250 刻：推进 / 完成 / 超时；到期发布新任务。
  public static void Update(Pawn p,HostProgress e,int now){
   bool host=Has(p);
   if(!host){if(e.active!=null){e.Log("失去「系统宿主」，任务「"+e.active.Label+"」作废");e.active=null;}e.offers.Clear();return;}
   if(e.active!=null){
    var t=e.active;
    if(Done(p,t)>=t.target){
     e.active=null;e.completed++;var r=RollReward(e.completed,t.Label);e.rewards.Add(r);e.Log("完成任务「"+t.Label+"」，获得一次奖励");
     Messages.Message(p.LabelShort+" 完成了系统任务「"+t.Label+"」，可以领取一次奖励！",p,MessageTypeDefOf.PositiveEvent,false);
     State?.Notify(p);
    } else if(now>t.deadline){
     e.active=null;e.failed++;Punish(p);e.Log("任务「"+t.Label+"」超时，受到系统惩罚 3 天");
     Find.LetterStack.ReceiveLetter("系统惩罚："+p.LabelShort,p.LabelShortCap+"没能在期限内完成任务「"+t.Label+"」（完成 "+Mathf.FloorToInt(Done(p,t))+" / "+t.target+"）。\n\n脑海里的声音冷冷地宣布了惩罚：3 天内全局工作速度 ×0.85。",LetterDefOf.NegativeEvent,p);
    }
   }
   if(e.active==null && (e.nextOfferTick<0 || now>=e.nextOfferTick))Release(p,e);
  }
  public static void Punish(Pawn p){
   if(PenaltyDef==null || p?.health==null)return;
   var old=p.health.hediffSet.GetFirstHediffOfDef(PenaltyDef);if(old!=null)p.health.RemoveHediff(old);
   var h=HediffMaker.MakeHediff(PenaltyDef,p);var dis=h.TryGetComp<HediffComp_Disappears>();if(dis!=null)dis.ticksToDisappear=PenaltyTicks;
   p.health.AddHediff(h);
  }

  // ---------- 领奖（照 ProgressStore.Claim） ----------
  public static bool Claim(Pawn p,HostReward offer,int choice,out string error){
   error="角色或奖励已发生变化，请重新打开界面。";
   if(!Eligible(p) || offer==null)return false;
   var e=State?.Lookup(p);if(e==null || e.rewards.Count==0 || !ReferenceEquals(e.rewards[0],offer))return false;
   string receipt;
   switch(choice){
    case 0:{
     var h=Adventurer.Enabled?Adventurer.Get(p):null;
     if(h==null){error="此人没有冒险者等级，不能领取属性点。奖励机会仍保留。";return false;}
     h.asiLeft+=StatPoints;h.Dirty();receipt="获得 1 点冒险者属性点（在“冒险者”分页分配）";break;}
    case 1:{
     if(CharmDef==null || p.inventory==null)return false;
     var t=ThingMaker.MakeThing(CharmDef);if(!p.inventory.innerContainer.TryAdd(t,true)){t.Destroy();error="改命符未能放入背包，奖励机会仍保留。";return false;}
     receipt="获得物品：改命符 ×1（已放入背包）";break;}
    case 2:{
     var d=DefDatabase<ThingDef>.GetNamedSilentFail(offer.item??"");
     if(d==null || !InPool(d.defName,offer.count) || p.inventory==null){error="该物品当前不可用。奖励机会仍保留。";return false;}
     var t=ThingMaker.MakeThing(d);t.stackCount=Mathf.Max(1,offer.count);
     if(!p.inventory.innerContainer.TryAdd(t,true)){if(!t.Destroyed)t.Destroy();error="物品未能放入背包，奖励机会仍保留。";return false;}
     receipt="获得物品："+d.LabelCap+" ×"+offer.count+"（已放入背包）";break;}
    default:return false;
   }
   e.rewards.RemoveAt(0);e.claimed++;e.Log(offer.Label+"："+receipt);
   Messages.Message(receipt,p,MessageTypeDefOf.PositiveEvent,false);
   error=null;return true;
  }

  public static string TipLine(Pawn p){
   var e=State?.Lookup(p);if(e==null)return "系统尚未发布任务";
   string s=e.active!=null?"当前任务："+e.active.Label+"（"+Mathf.FloorToInt(Done(p,e.active))+" / "+e.active.target+"）":e.offers.Count>0?"有 "+e.offers.Count+" 个任务等待接受":"等待下一次任务发布";
   if(e.rewards.Count>0)s+="，待领取奖励 "+e.rewards.Count+" 次";
   return s+"。点人物下方的「系统面板」按钮查看。";
  }
 }

 public class GameComponent_SystemHost : GameComponent {
  List<HostProgress> entries=new List<HostProgress>();
  readonly Dictionary<int,HostProgress> byId=new Dictionary<int,HostProgress>();
  readonly Queue<Pawn> notifications=new Queue<Pawn>();
  public GameComponent_SystemHost(Game g){}
  public IReadOnlyList<HostProgress> Entries=>entries;
  public HostProgress Lookup(Pawn p){if(p==null)return null;byId.TryGetValue(p.thingIDNumber,out var e);return e;}
  public HostProgress For(Pawn p){
   var e=Lookup(p);if(e!=null)return e;
   e=new HostProgress{pawnId=p.thingIDNumber};entries.Add(e);byId[e.pawnId]=e;return e;
  }
  public void Notify(Pawn p){if(!SystemHost.NoPopup && !notifications.Contains(p))notifications.Enqueue(p);}
  public void ClearNotifications()=>notifications.Clear();
  public override void ExposeData(){
   Scribe_Collections.Look(ref entries,"lpSystemHost",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){entries=entries??new List<HostProgress>();byId.Clear();foreach(var e in entries)byId[e.pawnId]=e;notifications.Clear();}
  }
  public void Step(){
   int now=Find.TickManager.TicksGame;
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists){
    if(SystemHost.Has(p))SystemHost.Update(p,For(p),now);
    else{var e=Lookup(p);if(e!=null && (e.active!=null || e.offers.Count>0))SystemHost.Update(p,e,now);}
   }
  }
  public override void GameComponentTick(){
   if(Find.TickManager.TicksGame%250==0 && SystemHost.Def!=null)Step();
   if(notifications.Count==0 || Find.WindowStack.Windows.OfType<SystemHostWindow>().Any())return;
   var p=notifications.Dequeue();var e=Lookup(p);
   if(SystemHost.Eligible(p) && e!=null && (e.rewards.Count>0 || e.offers.Count>0 && SystemHost.Has(p)))Find.WindowStack.Add(new SystemHostWindow(p));
  }
 }

 // 小人按钮：系统面板（宿主，或还有待领取奖励的人）；改命符（背包里有改命符的殖民者）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class SystemHostGizmos {
  static Texture2D panelIcon,charmIcon;
  static Texture2D PanelIcon=>panelIcon??(panelIcon=ContentFinder<Texture2D>.Get("LP/SystemHost/SystemPanel",false)??BaseContent.BadTex);
  static Texture2D CharmIcon=>charmIcon??(charmIcon=ContentFinder<Texture2D>.Get("LP/SystemHost/FateCharm",false)??BaseContent.BadTex);
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   foreach(var g in __result)yield return g;
   var p=__instance;if(p==null || p.Faction!=Faction.OfPlayer || p.Dead || !p.RaceProps.Humanlike)yield break;
   var e=SystemHost.State?.Lookup(p);
   if(SystemHost.Has(p) || e!=null && e.rewards.Count>0){
    int n=e?.rewards.Count??0;
    yield return new Command_Action{
     defaultLabel=n>0?"系统面板（"+n+"）":"系统面板",
     defaultDesc="打开系统宿主的面板：查看和接受任务、领取完成任务的奖励。\n\n"+SystemHost.TipLine(p),
     icon=PanelIcon,action=()=>Find.WindowStack.Add(new SystemHostWindow(p))
    };
   }
   var c=FateRewrite.CharmIn(p);
   if(c!=null)yield return new Command_Action{
    defaultLabel="改命符（"+FateRewrite.CharmCount(p)+"）",
    defaultDesc="使用背包里的改命符：把自己或同一处的另一名殖民者的一个特性，改写成同一档位的另一个特性。消耗 1 个改命符。",
    icon=CharmIcon,action=()=>Find.WindowStack.Add(new Dialog_FateRewrite(c,p))
   };
  }
 }
}
