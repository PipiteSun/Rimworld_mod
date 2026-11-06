using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.119.0 扭曲肉资源化（规格 ArtSource/TwistedMeat-spec.md）。
 // 引用 Anomaly 的物品、配方、建筑、灵能仪式都在 Bridges/Anomaly（LoadFolders 按 ludeon.rimworld.anomaly 条件加载）；
 // 这里的 C# 在主程序集，所有 Def 按名字判空，没有 Anomaly 时什么都不做。
 public static class TwistedMeat {
  static readonly DefRef<ThingDef> meat=new DefRef<ThingDef>("Meat_Twisted"),paste=new DefRef<ThingDef>("LP_AberrantPaste");
  static readonly DefRef<ThoughtDef> whispers=new DefRef<ThoughtDef>("LP_FarWhispers"),ateTwisted=new DefRef<ThoughtDef>("AteTwistedMeat");
  public static ThingDef Meat=>meat.Def;
  public static ThingDef Paste=>paste.Def;
  public static ThoughtDef Whispers=>whispers.Def;
  public static ThoughtDef AteTwisted=>ateTwisted.Def;
  public static bool Active=>ModsConfig.AnomalyActive && Meat!=null;
  // A 节：1 份扭曲肉按 2 份生肉计，1 份畸变膏按 20 份生肉计。
  public const float MeatWeight=2f,PasteWeight=20f;
  // E 节 / 远域回响：1 畸变膏 = 10 扭曲肉。
  public const int PasteAsMeat=10;
  public static float FleshValue(ThingDef d)=>d==null?1f:Meat!=null && d==Meat?MeatWeight:Paste!=null && d==Paste?PasteWeight:1f;
  public static bool IsMeat(Thing t)=>t!=null && Meat!=null && t.def==Meat;
  public static bool IsPaste(Thing t)=>t!=null && Paste!=null && t.def==Paste;
  // 死灵仪式台的生肉配方：新鲜生肉（含扭曲肉）或畸变膏。
  public static bool FleshOk(Thing t)=>ConstructSeed.RawMeat(t) || IsPaste(t);
  public static float FleshUnits(IEnumerable<Thing> things){float n=0f;if(things!=null)foreach(var t in things)if(FleshOk(t))n+=t.stackCount*FleshValue(t.def);return n;}
  public static int Day=>GenDate.DaysPassed;
  public static int Now=>Find.TickManager?.TicksGame??0;
  public static GameComponent_TwistedMeat Comp=>GameComp<GameComponent_TwistedMeat>.Get();
  public static bool TestSuppressed; // 自测里关掉随机「远域低语」
  // 10% / 5% 的「远域低语」（心情 -5，3 天）。
  public static bool MaybeWhisper(Pawn p,float chance){
   if(p?.needs?.mood?.thoughts?.memories==null || Whispers==null || TestSuppressed || !Rand.Chance(chance))return false;
   p.needs.mood.thoughts.memories.TryGainMemory(Whispers);return true;
  }
  public static void GiveWhisper(Pawn p){if(p?.needs?.mood?.thoughts?.memories!=null && Whispers!=null)p.needs.mood.thoughts.memories.TryGainMemory(Whispers);}
  // 远域回响：记下殖民地消耗的扭曲肉（畸变膏按 10 份）。
  public static void Consumed(float meatUnits){var c=Comp;if(c!=null && meatUnits>0f)c.AddEcho(meatUnits);}
  // 地图上（不被禁用、没被预留）的扭曲肉和畸变膏，按「扭曲肉当量」从近到远取。
  public static List<Thing> Nearby(Map map,IntVec3 center,float radius){
   var list=new List<Thing>();if(map==null)return list;
   foreach(var d in new[]{Meat,Paste}){if(d==null)continue;foreach(var t in map.listerThings.ThingsOfDef(d))if(t.Spawned && !t.IsForbidden(Faction.OfPlayer) && (radius<=0f || t.Position.DistanceTo(center)<=radius) && !map.reservationManager.IsReservedByAnyoneOf(t,Faction.OfPlayer))list.Add(t);}
   return list.OrderBy(t=>IsPaste(t)?1:0).ThenBy(t=>t.Position.DistanceToSquared(center)).ToList();
  }
  public static int MeatEquivalent(IEnumerable<Thing> things){int n=0;foreach(var t in things)n+=IsPaste(t)?t.stackCount*PasteAsMeat:IsMeat(t)?t.stackCount:0;return n;}
  // 按扭曲肉当量扣除：先用扭曲肉，不够再用畸变膏（畸变膏不拆零：剩余不足 10 份时也整份扣）。返回实际扣掉的当量。
  public static int Take(List<Thing> things,int need){
   int took=0;
   foreach(var t in things.Where(IsMeat).ToList()){if(need<=0)break;if(t.Destroyed)continue;int k=Mathf.Min(need,t.stackCount);t.SplitOff(k).Destroy();need-=k;took+=k;}
   foreach(var t in things.Where(IsPaste).ToList()){if(need<=0)break;if(t.Destroyed)continue;int k=Mathf.Min((need+PasteAsMeat-1)/PasteAsMeat,t.stackCount);t.SplitOff(k).Destroy();need-=k*PasteAsMeat;took+=k*PasteAsMeat;}
   return took;
  }
  public static Texture2D Icon(string path)=>ContentFinder<Texture2D>.Get(path,false)??BaseContent.BadTex;
 }

 // A 节：死灵仪式台 / 血肉义体配方的计数器（扭曲肉 ×2、畸变膏 ×20，其余按份数）。
 public class FleshCount : RitualCount {
  public override float ValuePerUnitOf(ThingDef def)=>TwistedMeat.FleshValue(def);
  public override string BillRequirementsDescription(RecipeDef r,IngredientCount ing)=>ing.GetBaseCount().ToString("0")+" 份新鲜生肉（扭曲肉 1 份算 2 份，畸变膏 1 份算 20 份）";
 }

 // 共享状态：远域回响、每日上限、累计解剖、血肉催转周期、鬼婆集市、血肉再生词条、死灵滋养。
 public class GameComponent_TwistedMeat : GameComponent {
  public float echoProgress;public List<int> echoStacks=new List<int>();
  public Dictionary<string,int> daily=new Dictionary<string,int>();
  public float dissected;public bool aberrologyDone;
  public Dictionary<string,string> cycles=new Dictionary<string,string>();
  public int hagQuadrum=-1;public List<Pawn> hagPawns=new List<Pawn>();public Map hagMap;public IntVec3 hagCell=IntVec3.Invalid;
  public List<int> regenGear=new List<int>();
  public List<int> nourished=new List<int>();public int nourishDay=-1;public List<int> aberrantSeeds=new List<int>();
  public GameComponent_TwistedMeat(Game g){}

  // ---------- 远域回响 ----------
  public const float EchoPer=300f,EchoStack=.05f,EchoMax=.25f;public const int EchoDays=30;
  public void AddEcho(float units){
   echoProgress+=units;
   while(echoProgress>=EchoPer){echoProgress-=EchoPer;if(Echo+EchoStack<=EchoMax+.0001f)echoStacks.Add(TwistedMeat.Now);else{echoStacks.Sort();if(echoStacks.Count>0)echoStacks.RemoveAt(0);echoStacks.Add(TwistedMeat.Now);}}
  }
  // 每层 +5%，30 天内线性消退；合计最多 +25%。
  public float Echo{get{float sum=0f;int now=TwistedMeat.Now,span=EchoDays*GenDate.TicksPerDay;foreach(var t in echoStacks){float left=1f-(now-t)/(float)span;if(left>0f)sum+=EchoStack*left;}return Mathf.Min(EchoMax,sum);}}

  // ---------- 每日计数 ----------
  public int Today(string key){return daily.TryGetValue(key+":"+TwistedMeat.Day,out int v)?v:0;}
  public void AddToday(string key,int n){string k=key+":"+TwistedMeat.Day;daily[k]=(daily.TryGetValue(k,out int v)?v:0)+n;}

  public override void GameComponentTick(){
   if(Find.TickManager.TicksGame%GenDate.TicksPerHour!=37)return;
   if(daily.Count>20){int d=TwistedMeat.Day;foreach(var k in daily.Keys.ToList()){int i=k.LastIndexOf(':');if(i<0 || !int.TryParse(k.Substring(i+1),out int day) || day<d-1)daily.Remove(k);}}
   int now=TwistedMeat.Now;echoStacks.RemoveAll(t=>now-t>EchoDays*GenDate.TicksPerDay);
   HagMarket.Tick(this);
   FleshWorks.HourTick(this);
  }
  public override void ExposeData(){
   Scribe_Values.Look(ref echoProgress,"lpEchoProgress");Scribe_Collections.Look(ref echoStacks,"lpEchoStacks",LookMode.Value);
   Scribe_Collections.Look(ref daily,"lpDaily",LookMode.Value,LookMode.Value);
   Scribe_Values.Look(ref dissected,"lpDissected");Scribe_Values.Look(ref aberrologyDone,"lpAberrology");
   Scribe_Collections.Look(ref cycles,"lpCycles",LookMode.Value,LookMode.Value);
   Scribe_Values.Look(ref hagQuadrum,"lpHagQuadrum",-1);Scribe_Collections.Look(ref hagPawns,"lpHagPawns",true,LookMode.Reference);Scribe_References.Look(ref hagMap,"lpHagMap");Scribe_Values.Look(ref hagCell,"lpHagCell",IntVec3.Invalid);
   Scribe_Collections.Look(ref regenGear,"lpRegenGear",LookMode.Value);
   Scribe_Collections.Look(ref nourished,"lpNourished",LookMode.Value);Scribe_Values.Look(ref nourishDay,"lpNourishDay",-1);Scribe_Collections.Look(ref aberrantSeeds,"lpAberrantSeeds",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    if(echoStacks==null)echoStacks=new List<int>();if(daily==null)daily=new Dictionary<string,int>();if(cycles==null)cycles=new Dictionary<string,string>();
    if(hagPawns==null)hagPawns=new List<Pawn>();hagPawns.RemoveAll(p=>p==null);if(regenGear==null)regenGear=new List<int>();if(nourished==null)nourished=new List<int>();if(aberrantSeeds==null)aberrantSeeds=new List<int>();
   }
  }
 }

 // ---------- 远域回响：殖民地消耗扭曲肉的入口 ----------
 // 配方原料（任何工作台）：消耗前记下份数（Destroy 会把 stackCount 清零）。畸变膏不重复计（做膏时已经算过扭曲肉）。
 [HarmonyPatch(typeof(RecipeWorker),nameof(RecipeWorker.ConsumeIngredient))]
 public static class TwistedMeatConsumeEcho {
  static void Prefix(Thing ingredient){if(TwistedMeat.IsMeat(ingredient))TwistedMeat.Consumed(ingredient.stackCount);}
 }
 // Anomaly 威胁事件（ThreatBig / ThreatSmall）的基础概率 × (1 + 回响)。
 [HarmonyPatch(typeof(IncidentWorker),nameof(IncidentWorker.BaseChanceThisGame),MethodType.Getter)]
 public static class TwistedMeatEchoChance {
  static void Postfix(IncidentWorker __instance,ref float __result){
   // 2026-11-06 审查：说书人每次挑事件都会对所有事件调一遍；先看有没有回响（大多数时候没有），再比包名，且不再每次 ToLowerInvariant 分配字符串。
   if(__result<=0f || !ModsConfig.AnomalyActive)return;var c=TwistedMeat.Comp;if(c==null || c.echoStacks.Count==0)return;
   var d=__instance.def;if(d==null || !IsAnomalyThreat(d))return;__result*=1f+c.Echo;
  }
  public static bool IsAnomalyThreat(IncidentDef d)=>(d.category==IncidentCategoryDefOf.ThreatBig || d.category==IncidentCategoryDefOf.ThreatSmall) && string.Equals(d.modContentPack?.PackageId,"ludeon.rimworld.anomaly",StringComparison.OrdinalIgnoreCase);
 }
}
