using System.Collections.Concurrent;
using RimWorld;
using RimWorld.Planet;
using Verse;
namespace LichPhylactery {
 // 1.58.6 性能：热路径共用的缓存（见 ArtSource/Performance-1.58.4-audit.md 第〇节 R1 / R3）。
 // 全部只在“结果不会变”的前提下缓存：Def 一旦查到就不变；查不到时记下当时的 DefCount，Def 数量变了（自测运行时注册新 Def）才重查。
 // 组件以 Game / World / Map 对象本身作键，读档或换地图后自动失效；查不到组件时不缓存 null。引用写入是原子的，多线程读到旧值也只是多查一次。

 // 按名字的单个 Def：静态字段里放一个，热路径上只是一次字段读和空判断。
 public sealed class DefRef<T> where T:Def {
  readonly string name;T def;int stamp=-1;
  public DefRef(string name){this.name=name;}
  public T Def{get{
   var d=def;if(d!=null)return d;
   int n=DefDatabase<T>.DefCount;if(stamp==n)return null;
   d=DefDatabase<T>.GetNamedSilentFail(name);def=d;stamp=n;return d;
  }}
  public static implicit operator T(DefRef<T> r)=>r?.Def;
 }
 // 按名字（运行时拼出来的名字）查 Def：查到的永久缓存，查不到的按 DefCount 缓存。
 public static class DefCache<T> where T:Def {
  static readonly ConcurrentDictionary<string,T> found=new ConcurrentDictionary<string,T>();
  static readonly ConcurrentDictionary<string,int> missing=new ConcurrentDictionary<string,int>();
  public static T Get(string name){
   if(name==null)return null;
   if(found.TryGetValue(name,out var d))return d;
   int n=DefDatabase<T>.DefCount;
   if(missing.TryGetValue(name,out var stamp) && stamp==n)return null;
   d=DefDatabase<T>.GetNamedSilentFail(name);
   if(d!=null){found[name]=d;missing.TryRemove(name,out _);}else missing[name]=n;
   return d;
  }
 }
 // 获得记忆时（每次社交、吃饭、睡觉、看到尸体……）要判断是不是“看到尸体”的想法：结果只取决于定义，按定义记住（原来每次 4 次文化相关的 StartsWith）。
 public static class MemoryDefs {
  static readonly ConcurrentDictionary<ThoughtDef,bool> corpse=new ConcurrentDictionary<ThoughtDef,bool>();
  public static bool ObservedCorpse(ThoughtDef d)=>d!=null && corpse.GetOrAdd(d,x=>x.defName.StartsWith("ObservedLayingCorpse") || x.defName.StartsWith("ObservedLayingRottingCorpse"));
 }
 // 任何小人的基因变化都会递增（按刻记忆基因派生值时用来失效）。
 public static class GeneVersion {public static int Value;}
 [HarmonyLib.HarmonyPatch(typeof(Pawn_GeneTracker),"Notify_GenesChanged")]
 public static class GeneVersionBump {static void Postfix(){System.Threading.Interlocked.Increment(ref GeneVersion.Value);}}
 // 渲染节点缩放后缀原来每个节点每帧都比较一次 defName 字符串。
 public static class RenderTags {static readonly DefRef<PawnRenderNodeTagDef> root=new DefRef<PawnRenderNodeTagDef>("Root");public static bool IsRoot(PawnRenderNodeTagDef t)=>t!=null && t==root.Def;}
 // 读档时组件列表会整个换成新的（同一个 Game / World / Map 对象），所以同时比对列表引用。
 public static class GameComp<T> where T:GameComponent {
  sealed class Entry{public Game game;public object list;public T comp;}
  static Entry cache;
  public static T Get(){
   var g=Current.Game;if(g==null)return null;
   var e=cache;if(e!=null && ReferenceEquals(e.game,g) && ReferenceEquals(e.list,g.components))return e.comp;
   var c=g.GetComponent<T>();if(c!=null)cache=new Entry{game=g,list=g.components,comp=c};
   return c;
  }
 }
 public static class WorldComp<T> where T:WorldComponent {
  sealed class Entry{public World world;public object list;public T comp;}
  static Entry cache;
  public static T Get(){
   var w=Find.World;if(w==null)return null;
   var e=cache;if(e!=null && ReferenceEquals(e.world,w) && ReferenceEquals(e.list,w.components))return e.comp;
   var c=w.GetComponent<T>();if(c!=null)cache=new Entry{world=w,list=w.components,comp=c};
   return c;
  }
 }
 // 按小人（thingIDNumber）登记的单例状态：PostAdd / 读档时登记，PostRemoved 时标记失效。
 // Get：没登记过的小人直接返回 null（原来每次扫一遍 hediff 列表）；登记失效或不是同一个小人时回落到原来的扫描。
 public interface IIndexedHediff {bool IndexLive{get;set;}}
 public static class HediffIndex<T> where T:Hediff,IIndexedHediff {
  static readonly ConcurrentDictionary<int,T> map=new ConcurrentDictionary<int,T>();
  public static void Add(T h){if(h?.pawn==null)return;h.IndexLive=true;map[h.pawn.thingIDNumber]=h;}
  public static void Removed(T h){if(h!=null)h.IndexLive=false;}
  public static T Get(Pawn p){
   if(p==null || !map.TryGetValue(p.thingIDNumber,out var r))return null;
   if(r.IndexLive && r.pawn==p)return r;
   var list=p.health?.hediffSet?.hediffs;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i] is T h){Add(h);return h;}
   return null;
  }
 }
 public partial class Hediff_Construct : IIndexedHediff {
  public bool IndexLive{get;set;}
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);HediffIndex<Hediff_Construct>.Add(this);}
  public override void PostRemoved(){base.PostRemoved();HediffIndex<Hediff_Construct>.Removed(this);}
 }
 public partial class Hediff_Lich : IIndexedHediff {
  public bool IndexLive{get;set;}
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);HediffIndex<Hediff_Lich>.Add(this);}
  public override void PostRemoved(){base.PostRemoved();HediffIndex<Hediff_Lich>.Removed(this);}
 }
 public static class MapComp<T> where T:MapComponent {
  sealed class Entry{public Map map;public object list;public T comp;}
  static readonly ConcurrentDictionary<int,Entry> comps=new ConcurrentDictionary<int,Entry>();
  public static T Get(Map map){
   if(map==null)return null;
   if(comps.TryGetValue(map.uniqueID,out var e) && ReferenceEquals(e.map,map) && ReferenceEquals(e.list,map.components))return e.comp;
   var c=map.GetComponent<T>();if(c!=null)comps[map.uniqueID]=new Entry{map=map,list=map.components,comp=c};
   return c;
  }
 }
}
