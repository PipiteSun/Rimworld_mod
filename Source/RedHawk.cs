using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.64.0 红鹰商业联盟（ArtSource/RedHawk-1.64.0-spec.md）：派系与商队在 Defs/RedHawk.xml。
 // 这里是三个库存生成器（多种族奴隶、按 defName 列表的物品、指定宗主的契约书）和契约 / 派系开关联动。
 public static class RedHawk {
  public const string FactionName="LP_RedHawkLeague",PactCaravan="LP_Caravan_RHPact",SlaveCaravan="LP_Caravan_RHSlaver",MaterialCaravan="LP_Caravan_RHMaterials",Base="LP_Base_RedHawk",Orbital="LP_Orbital_RedHawk";
  public const float PremiumChance=.15f;
  public const int PremiumSkill=4;
  static FactionDef def;static PawnKindDef stock;
  public static FactionDef Def=>def??(def=DefDatabase<FactionDef>.GetNamedSilentFail(FactionName));
  public static Faction Faction=>Factions.Of(Def);
  public static PawnKindDef StockKind=>stock??(stock=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_RH_SlaveStock"));
  // 条目上的 requires：pact = 异界契约开着，abyss = 深渊开着；其余视为满足。
  public static bool Requirement(string key){
   switch(key){case "pact":return Pacts.Enabled;case "abyss":return AbyssSettings.Enabled;default:return true;}
  }
  // 契约和深渊都关掉时，契约卷轴商队不来。
  public static bool PactCaravanOff=>!Pacts.Enabled && !AbyssSettings.Enabled;

  // 一名库存奴隶：成年、没有亲属；买下后照原版成为奴隶（joinStatus）。HAR 条目用它自己的 pawnKind。
  public static Pawn MakeSlave(RHSlaveEntry e,Faction faction,PlanetTile tile,bool orbital){
   if(e==null || !e.Valid)return null;var kind=e.Kind??StockKind;if(kind==null)return null;
   var fac=faction!=null && !faction.IsPlayer?faction:Faction;
   var req=new PawnGenerationRequest(kind,fac,PawnGenerationContext.NonPlayer,tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,colonistRelationChanceFactor:0f,
    forceAddFreeWarmLayerIfNeeded:!orbital,allowPregnant:false,forcedXenotype:e.Xeno,developmentalStages:DevelopmentalStage.Adult);
   var p=PawnGenerator.GeneratePawn(req);
   if(p.guest!=null)p.guest.joinStatus=JoinStatus.JoinAsSlave;
   if(Rand.Chance(PremiumChance))MakePremium(p);
   return p;
  }
  // 精品：没有 DND 背景特性的一半机会得到一个，否则随机一项能用的技能 +4。返回是否加了背景特性。
  public static bool MakePremium(Pawn p){
   var traits=p?.story?.traits;
   if(traits!=null && Rand.Bool && BackgroundTraits.Count(p)==0){
    var d=BackgroundTraits.All.Where(t=>!traits.allTraits.Any(x=>x.def.ConflictsWith(t))).RandomElementWithFallback();
    if(d!=null){traits.GainTrait(new Trait(d,d.degreeDatas.NullOrEmpty()?0:d.degreeDatas[0].degree,true));return true;}
   }
   var s=p?.skills?.skills?.Where(x=>!x.TotallyDisabled).RandomElementWithFallback();
   if(s!=null)s.Level=Math.Min(SkillRecord.MaxLevel,s.Level+PremiumSkill);
   return false;
  }
  // 种族键：HAR 条目按种族，其余按异种型（同一次库存里互不相同）。
  public static string RaceKey(Pawn p)=>p.def!=ThingDefOf.Human?p.def.defName:p.genes?.Xenotype?.defName??"Baseliner";
 }

 public enum RHTier {Common,Rare}
 // 奴隶种族池的一条：xenotype（defName）或 pawnKind（HAR 种族），懒解析，解析不到就跳过。
 public class RHSlaveEntry {
  public string xenotype,pawnKind;public float weight=1f;public RHTier tier=RHTier.Common;
  bool resolved;XenotypeDef xeno;PawnKindDef kind;
  void Resolve(){
   if(resolved)return;resolved=true;
   if(!xenotype.NullOrEmpty())xeno=DefDatabase<XenotypeDef>.GetNamedSilentFail(xenotype);
   if(!pawnKind.NullOrEmpty()){kind=DefDatabase<PawnKindDef>.GetNamedSilentFail(pawnKind);if(kind?.RaceProps?.Humanlike!=true)kind=null;}
  }
  public XenotypeDef Xeno{get{Resolve();return xeno;}}
  public PawnKindDef Kind{get{Resolve();return kind;}}
  public bool Valid=>pawnKind.NullOrEmpty()?Xeno!=null:Kind!=null && (xenotype.NullOrEmpty() || Xeno!=null);
  public bool Rare=>tier==RHTier.Rare;
  public string Key=>pawnKind.NullOrEmpty()?xenotype:Kind?.race?.defName??pawnKind;
 }
 // 多种族奴隶：一次 countRange 名，每人不同种族；按权重无放回抽，先保证 minRare 个稀有档。
 public class StockGenerator_RHRaceSlaves : StockGenerator {
  public List<RHSlaveEntry> entries=new List<RHSlaveEntry>();
  public int minRare=0;
  public IEnumerable<RHSlaveEntry> Valid=>entries.Where(e=>e.Valid);
  public List<RHSlaveEntry> Pick(int count){
   var pool=Valid.ToList();var res=new List<RHSlaveEntry>();
   var rare=pool.Where(e=>e.Rare).ToList();
   while(res.Count<Math.Min(minRare,count) && rare.Count>0){var e=rare.RandomElementByWeight(x=>Mathf.Max(.01f,x.weight));rare.Remove(e);pool.Remove(e);res.Add(e);}
   while(res.Count<count && pool.Count>0){var e=pool.RandomElementByWeight(x=>Mathf.Max(.01f,x.weight));pool.Remove(e);res.Add(e);}
   return res.InRandomOrder().ToList();
  }
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   foreach(var e in Pick(countRange.RandomInRange)){var p=RedHawk.MakeSlave(e,faction,forTile,trader?.orbital==true);if(p!=null)yield return p;}
  }
  public override bool HandlesThingDef(ThingDef d)=>d?.category==ThingCategory.Pawn && d.race?.Humanlike==true && d.tradeability!=Tradeability.None;
 }

 // 按 defName 列表出货：缺了的、商人卖不了的（tradeability）跳过；pickRange 不为 0 时从这组里随机挑 N 种；chance 是整组出现率。
 // 关掉「异界契约」时：requires=pact 的条目不出，数量乘 pactOffFactor（灵魂币、恶魔血多一份）。列表里的东西商人也收。
 public class RHDefEntry {
  public string def;public IntRange countRange=IntRange.One;public float chance=1f;public string requires;public float pactOffFactor=1f;
  bool resolved;ThingDef thing;
  public ThingDef Def{get{if(!resolved){resolved=true;thing=DefDatabase<ThingDef>.GetNamedSilentFail(def);}return thing;}}
  public bool Sellable=>Def!=null && Def.tradeability.TraderCanSell();
 }
 public class StockGenerator_RHDefList : StockGenerator {
  public List<RHDefEntry> entries=new List<RHDefEntry>();
  public IntRange pickRange=IntRange.Zero;public float chance=1f;
  public IEnumerable<RHDefEntry> Available=>entries.Where(e=>e.Sellable && RedHawk.Requirement(e.requires));
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   if(!Rand.Chance(chance))yield break;
   var list=Available.ToList();
   if(pickRange.max>0)list=list.InRandomOrder().Take(pickRange.RandomInRange).ToList();
   foreach(var e in list){
    if(!Rand.Chance(e.chance))continue;
    int n=e.countRange.RandomInRange;if(!Pacts.Enabled)n=Mathf.RoundToInt(n*e.pactOffFactor);if(n<=0)continue;
    foreach(var t in StockGeneratorUtility.TryMakeForStock(e.Def,n,faction))yield return t;
   }
  }
  public override bool HandlesThingDef(ThingDef d){if(d==null)return false;foreach(var e in entries)if(e.Def==d)return true;return false;}
 }

 // 契约书：每张来自不同宗主（生成时就定，存在 CompPactScroll 里，阅读流程不变）。关掉「异界契约」时不卖。
 public class StockGenerator_RHPactScrolls : StockGenerator {
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   if(!Pacts.Enabled || Pacts.ScrollDef==null)yield break;
   foreach(var d in Pacts.Patrons.InRandomOrder().Take(countRange.RandomInRange).ToList()){
    var t=ThingMaker.MakeThing(Pacts.ScrollDef);t.TryGetComp<CompPactScroll>()?.SetPatron(d);yield return t;
   }
  }
  public override bool HandlesThingDef(ThingDef d)=>d!=null && d==Pacts.ScrollDef;
 }

 // 原版商队只把兵种为 Slave 的人当商品（Chattel），红鹰的库存奴隶用自己的兵种 / HAR 兵种，会被当成护卫、交易窗口里看不到。
 // 红鹰派系里兵种是奴隶池兵种（LP_RH_SlaveStock 或 HAR 条目的兵种）、买下后成为奴隶的人就是商品；护卫和商人不受影响。
 [HarmonyPatch(typeof(TraderCaravanUtility),nameof(TraderCaravanUtility.GetTraderCaravanRole))]
 public static class RedHawkChattelRole {
  static HashSet<PawnKindDef> kinds;
  public static HashSet<PawnKindDef> StockKinds{get{
   if(kinds==null){
    kinds=new HashSet<PawnKindDef>();if(RedHawk.StockKind!=null)kinds.Add(RedHawk.StockKind);
    foreach(var k in DefDatabase<TraderKindDef>.AllDefsListForReading)foreach(var g in k.stockGenerators.OfType<StockGenerator_RHRaceSlaves>())foreach(var e in g.entries)if(e.Kind!=null)kinds.Add(e.Kind);
   }
   return kinds;
  }}
  static void Postfix(Pawn p,ref TraderCaravanRole __result){
   if(__result==TraderCaravanRole.Guard && p.guest?.joinStatus==JoinStatus.JoinAsSlave && p.Faction!=null && p.Faction.def==RedHawk.Def && StockKinds.Contains(p.kindDef))__result=TraderCaravanRole.Chattel;
  }
 }

 // 契约和深渊都关掉时契约卷轴商队不来；派系开关关掉时它的飞空商船不来（已有存档里派系还在）。
 [HarmonyPatch(typeof(TraderKindDef),nameof(TraderKindDef.CalculatedCommonality),MethodType.Getter)]
 public static class RedHawkTraderGate {
  static void Postfix(TraderKindDef __instance,ref float __result){
   if(__result<=0f)return;
   if(__instance.faction!=null && !Factions.Enabled(__instance.faction))__result=0f;
   else if(__instance.defName==RedHawk.PactCaravan && RedHawk.PactCaravanOff)__result=0f;
  }
 }
}
