using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.95.0（Peter：只给敌人用的种族不要出现在开局建角的异种下拉里）：按用法判断，对所有已加载模组的异种都生效。
 // 敌人专用 = 至少出现在一个派系 / 兵种的异种表里，且出现的地方全是永久敌对派系（海盗之类），
 // 并且没有自由人权重（不会以流浪者、难民身份出现）、没有任何开局剧本点名它。只出现在事件兵种（不属于任何派系）里的不算。
 // 只影响建角下拉（PactStart.Choosable），敌人生成照旧读派系 / 兵种的异种表。契约种族另由 PactStart 处理。
 public static class EnemyRaces {
  // Peter：狂猎（骑手、首领）只当敌人，连立约者开局也不列
  public static readonly HashSet<string> Always=new HashSet<string>{"LP_WildHunter","LP_WildHuntLord"};
  static Dictionary<XenotypeDef,string> why; // 敌人专用 → 出现的派系；不在表里的都可选
  public static bool TestDisable; // 测试用：关掉过滤
  static List<XenotypeChance> Chances(XenotypeSet s)=>s==null?null:Traverse.Create(s).Field("xenotypeChances").GetValue<List<XenotypeChance>>();
  static IEnumerable<PawnKindDef> Kinds(FactionDef f){
   if(f.basicMemberKind!=null)yield return f.basicMemberKind;
   if(f.fixedLeaderKinds!=null)foreach(var k in f.fixedLeaderKinds)yield return k;
   if(f.pawnGroupMakers!=null)foreach(var g in f.pawnGroupMakers)
    foreach(var l in new[]{g.options,g.traders,g.carriers,g.guards})if(l!=null)foreach(var o in l)if(o?.kind!=null)yield return o.kind;
  }
  static void Build(){
   why=new Dictionary<XenotypeDef,string>();
   var hostile=new Dictionary<XenotypeDef,HashSet<string>>();var friendly=new HashSet<XenotypeDef>();
   void Mark(XenotypeSet s,FactionDef f){var c=Chances(s);if(c==null)return;foreach(var x in c)if(x?.xenotype!=null && x.chance>0f){if(f!=null && f.permanentEnemy && !f.isPlayer){if(!hostile.TryGetValue(x.xenotype,out var h))hostile[x.xenotype]=h=new HashSet<string>();h.Add(f.LabelCap.ToString());}else friendly.Add(x.xenotype);}}
   var owners=new Dictionary<PawnKindDef,List<FactionDef>>();
   foreach(var f in DefDatabase<FactionDef>.AllDefs){
    Mark(f.xenotypeSet,f);
    foreach(var k in Kinds(f).Distinct()){if(!owners.TryGetValue(k,out var l))owners[k]=l=new List<FactionDef>();l.Add(f);}
   }
   foreach(var k in DefDatabase<PawnKindDef>.AllDefs){
    if(k.xenotypeSet==null)continue;
    var fs=owners.TryGetValue(k,out var l)?l:null;
    if(fs==null || fs.Count==0){if(k.defaultFactionDef!=null)Mark(k.xenotypeSet,k.defaultFactionDef);else Mark(k.xenotypeSet,null);continue;}
    foreach(var f in fs)Mark(k.xenotypeSet,f);
   }
   var scen=new HashSet<XenotypeDef>();
   foreach(var d in DefDatabase<ScenarioDef>.AllDefs)foreach(var p in d.scenario.AllParts.OfType<ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes>()){
    var cs=Traverse.Create(p).Field("xenotypeCounts").GetValue<List<XenotypeCount>>();if(cs!=null)foreach(var c in cs)if(c?.xenotype!=null)scen.Add(c.xenotype);
   }
   foreach(var kv in hostile){
    var x=kv.Key;
    if(Always.Contains(x.defName)){why[x]=string.Join("、",kv.Value.OrderBy(s=>s));continue;}
    if(x==XenotypeDefOf.Baseliner || PactStart.IsPactRace(x) || friendly.Contains(x) || scen.Contains(x) || x.factionlessGenerationWeight>0f)continue;
    why[x]=string.Join("、",kv.Value.OrderBy(s=>s));
   }
   AddAlways();
  }
  static void AddAlways(){foreach(var n in Always)if(DefDatabase<XenotypeDef>.GetNamedSilentFail(n) is XenotypeDef x && !why.ContainsKey(x))why[x]="狂猎";}
  public static bool IsEnemyOnly(XenotypeDef x){if(x==null)return false;if(why==null)Build();return why.ContainsKey(x);}
  public static string Factions(XenotypeDef x){if(why==null)Build();return why.TryGetValue(x,out var s)?s:null;}
  public static IEnumerable<XenotypeDef> All{get{if(why==null)Build();return why.Keys;}}
  public static void Reset()=>why=null;
  public static bool Hidden(XenotypeDef x)=>!TestDisable && IsEnemyOnly(x);
 }
}
