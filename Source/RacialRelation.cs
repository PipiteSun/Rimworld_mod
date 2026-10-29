using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 共享组件：种族关系心情。XML 里配置"异种型 observer 看到同地图的异种型 target → thought"（target 只算本殖民地成员和囚犯）。
 // thought 的第0阶段是初始心情；daysToAccept>0 时，observer 与 target 同处一张地图累计满这么多天后改用第1阶段（例如 -2 → +2）。
 // halvedBy 中任一异种型作为自由殖民者在同一地图时，心情×50%（半精灵的"两族之桥"）。
 public class RacialRelationDef : Def {
  public XenotypeDef observer,target;
  // 1.57.0：observers/targets 再列出更多异种型（精灵任一亚种看卓尔等）；targetLich = 本殖民地的巫妖也算目标。
  public List<XenotypeDef> observers=new List<XenotypeDef>(),targets=new List<XenotypeDef>();
  public bool targetLich;
  public ThoughtDef thought;
  public float daysToAccept=-1f;
  public List<XenotypeDef> halvedBy=new List<XenotypeDef>();
  public bool enabled=true;
  public int AcceptTicks=>daysToAccept>0?(int)(daysToAccept*GenDate.TicksPerDay):-1;
  public bool Observes(XenotypeDef x)=>x!=null && (observer==x || observers.Contains(x));
  public bool Targets(XenotypeDef x)=>x!=null && (target==x || targets.Contains(x));
  // 同地图（不含自己）的目标人数。
  public int TargetCount(MapCensus c,Pawn self){
   var x=RacialRelations.Xeno(self);int n=0;
   if(target!=null)n+=c.Count(target)-(target==x?1:0);
   for(int i=0;i<targets.Count;i++)if(targets[i]!=target)n+=c.Count(targets[i])-(targets[i]==x?1:0);
   if(targetLich)n+=c.liches-(Items.IsLich(self)?1:0);
   return n;
  }
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(observer==null && observers.NullOrEmpty() || target==null && targets.NullOrEmpty() && !targetLich || thought==null)yield return "observer, target and thought are required";
   else{
    if(thought.stages.NullOrEmpty())yield return "thought needs at least one stage";
    else if(daysToAccept>0 && thought.stages.Count<2)yield return "daysToAccept needs a second thought stage";
    if(thought.workerClass==null || !typeof(ThoughtWorker_DNDRacialRelation).IsAssignableFrom(thought.workerClass))yield return "thought workerClass must be LichPhylactery.ThoughtWorker_DNDRacialRelation";
   }
  }
 }
 public class ThoughtWorker_DNDRacialRelation : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   int stage=-1;
   foreach(var r in RacialRelations.Active(p,def)){int s=RacialRelations.Stage(p,r);if(s>stage)stage=s;}
   return stage<0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(stage);
  }
  // 1.57.0：两族之桥只减半负面心情（已成老战友的正面心情不变）。
  public override float MoodMultiplier(Pawn p){
   float m=base.MoodMultiplier(p);
   foreach(var r in RacialRelations.Active(p,def))if(RacialRelations.Halved(p,r) && def.stages[RacialRelations.Stage(p,r)].baseMoodEffect<0f)return m*.5f;
   return m;
  }
 }
 public static class RacialRelations {
  static Dictionary<ThoughtDef,List<RacialRelationDef>> byThought;static int defCount=-1;
  static List<RacialRelationDef> For(ThoughtDef thought){
   if(byThought==null || defCount!=DefDatabase<RacialRelationDef>.DefCount){defCount=DefDatabase<RacialRelationDef>.DefCount;byThought=new Dictionary<ThoughtDef,List<RacialRelationDef>>();foreach(var r in DefDatabase<RacialRelationDef>.AllDefsListForReading)if(r.thought!=null){if(!byThought.TryGetValue(r.thought,out var l))byThought[r.thought]=l=new List<RacialRelationDef>();l.Add(r);}}
   return byThought.TryGetValue(thought,out var list)?list:null;
  }
  public static RacialRelationTracker Tracker=>GameComp<RacialRelationTracker>.Get();
  public static XenotypeDef Xeno(Pawn p)=>p?.genes?.Xenotype;
  // 同地图（不含自己）能看到 target 的已启用关系。
  public static IEnumerable<RacialRelationDef> Active(Pawn p,ThoughtDef thought){
   var list=For(thought);if(list==null || p?.MapHeld==null || !p.Spawned)yield break;
   var x=Xeno(p);if(x==null)yield break;
   MapCensus census=null;
   foreach(var r in list){
    if(!r.enabled || !r.Observes(x))continue;
    if(census==null)census=Tracker?.Census(p.Map);if(census==null)yield break;
    if(r.TargetCount(census,p)>0)yield return r;
   }
  }
  public static int Stage(Pawn p,RacialRelationDef r){int need=r.AcceptTicks;return need>0 && (Tracker?.Ticks(p,r)??0)>=need?1:0;}
  public static bool Halved(Pawn p,RacialRelationDef r){
   if(r.halvedBy.NullOrEmpty())return false;var census=Tracker?.Census(p.Map);if(census==null)return false;
   foreach(var x in r.halvedBy)if(census.ColonistCount(x)-(Xeno(p)==x && p.IsFreeColonist?1:0)>0)return true;
   return false;
  }
 }
 // 每张地图的异种型人口普查：最多每250刻重建一次，思绪读取不遍历地图。
 public class MapCensus {
  public int tick=-99999,liches;
  public readonly Dictionary<XenotypeDef,int> all=new Dictionary<XenotypeDef,int>(),colonists=new Dictionary<XenotypeDef,int>();
  public int Count(XenotypeDef x)=>x!=null && all.TryGetValue(x,out int n)?n:0;
  public int ColonistCount(XenotypeDef x)=>x!=null && colonists.TryGetValue(x,out int n)?n:0;
  public void Rebuild(Map map){
   tick=Find.TickManager.TicksGame;all.Clear();colonists.Clear();liches=0;
   foreach(var p in map.mapPawns.AllHumanlikeSpawned){
    // 只算本殖民地成员（含奴隶）和囚犯：访客、商队、敌人不算"同住"。
    if(p.Dead || (p.Faction!=Faction.OfPlayer && !p.IsPrisonerOfColony))continue;if(Items.IsLich(p))liches++;var x=RacialRelations.Xeno(p);if(x==null)continue;
    all[x]=Count(x)+1;if(p.IsFreeColonist)colonists[x]=ColonistCount(x)+1;
   }
  }
 }
 public class RacialRelationTracker : GameComponent {
  public const int Interval=250;
  const int ForgetTicks=60*GenDate.TicksPerDay;
  Dictionary<string,int> together=new Dictionary<string,int>(),lastSeen=new Dictionary<string,int>();
  readonly Dictionary<int,MapCensus> census=new Dictionary<int,MapCensus>();
  public RacialRelationTracker(Game game){}
  static string Key(Pawn p,RacialRelationDef r)=>p.GetUniqueLoadID()+"|"+r.defName;
  public int Ticks(Pawn p,RacialRelationDef r)=>together.TryGetValue(Key(p,r),out int t)?t:0;
  public void SetTicks(Pawn p,RacialRelationDef r,int ticks){string k=Key(p,r);together[k]=ticks;lastSeen[k]=Find.TickManager.TicksGame;}
  public MapCensus Census(Map map){
   if(map==null)return null;
   if(!census.TryGetValue(map.uniqueID,out var c))census[map.uniqueID]=c=new MapCensus();
   if(Find.TickManager.TicksGame-c.tick>=Interval || Find.TickManager.TicksGame<c.tick)c.Rebuild(map);
   return c;
  }
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;if(now%Interval!=37)return;
   var defs=DefDatabase<RacialRelationDef>.AllDefsListForReading;bool any=false;
   for(int i=0;i<defs.Count;i++)if(defs[i].enabled && defs[i].AcceptTicks>0){any=true;break;}
   if(!any)return;
   Accumulate(Interval);
   if(now%(GenDate.TicksPerDay)<Interval)Forget(now);
  }
  // 给每个满足关系的殖民者累计同处时间（只遍历自由殖民者）。
  public void Accumulate(int ticks){
   foreach(var map in Find.Maps){
    var c=Census(map);
    foreach(var p in map.mapPawns.FreeColonistsSpawned){
     var x=RacialRelations.Xeno(p);if(x==null)continue;
     foreach(var r in DefDatabase<RacialRelationDef>.AllDefsListForReading){
      if(!r.enabled || r.AcceptTicks<=0 || !r.Observes(x))continue;
      if(r.TargetCount(c,p)<=0)continue;
      string k=Key(p,r);together[k]=(together.TryGetValue(k,out int t)?t:0)+ticks;lastSeen[k]=Find.TickManager.TicksGame;
     }
    }
   }
   census.Keys.Where(id=>!Find.Maps.Any(m=>m.uniqueID==id)).ToList().ForEach(id=>census.Remove(id));
  }
  void Forget(int now){foreach(var k in lastSeen.Where(e=>now-e.Value>ForgetTicks).Select(e=>e.Key).ToList()){lastSeen.Remove(k);together.Remove(k);}}
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref together,"racialTogether",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref lastSeen,"racialLastSeen",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(together==null)together=new Dictionary<string,int>();if(lastSeen==null)lastSeen=new Dictionary<string,int>();}
  }
 }
}
