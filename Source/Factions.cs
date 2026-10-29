using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.19.0 派系配置：挂在 FactionDef 上。toggle 对应模组设置里的开关；leader/follower 用于斩首。
 public class FactionExtension : DefModExtension {
  public string toggle;
  public int initialGoodwill;
  public List<PawnKindDef> leaderKinds=new List<PawnKindDef>(),followerKinds=new List<PawnKindDef>();
  public int followersPerLeader=6;
  public IntRange constructsPerLeader=IntRange.Zero,leaderCount=IntRange.Zero;
  public List<FactionDef> enemyFactions=new List<FactionDef>();
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(!Factions.Toggles.Contains(toggle))yield return "unknown faction toggle "+toggle;
   if(followersPerLeader<0)yield return "followersPerLeader must not be negative";
  }
 }
 // 原版只在玩家买入时看 stockGenerator 的 price；玩家卖出永远按 Normal。这个扩展让“收购”也有价格档：
 // 先看收购类 stockGenerator（StockGenerator_Buy*）自己的 price，没有就用 defaultSellPrice。
 public class TraderSellPriceExtension : DefModExtension { public PriceType defaultSellPrice=PriceType.Undefined; }
 [HarmonyPatch(typeof(TraderKindDef),nameof(TraderKindDef.PriceTypeFor))]
 public static class TraderSellPrice {
  static void Postfix(TraderKindDef __instance,ThingDef thingDef,TradeAction action,ref PriceType __result){
   if(action!=TradeAction.PlayerSells || thingDef==ThingDefOf.Silver)return;
   var ext=__instance.GetModExtension<TraderSellPriceExtension>();if(ext==null)return;
   foreach(var g in __instance.stockGenerators)if(g.GetType().Name.StartsWith("StockGenerator_Buy") && g.price!=PriceType.Undefined && g.TryGetPriceType(thingDef,action,out var p)){__result=p;return;}
   if(ext.defaultSellPrice!=PriceType.Undefined)__result=ext.defaultSellPrice;
  }
 }
 // 夺心魔蝌蚪、脑浆罐原本不能交易（tradeability=None）；只有黑网收购。
 [HarmonyPatch(typeof(TradeUtility),nameof(TradeUtility.PlayerSellableNow))]
 public static class BlackMarketGoods {
  public static readonly string[] Contraband={"LP_Tadpole","LP_BrainJar"};
  static void Postfix(Thing t,ITrader trader,ref bool __result){
   if(__result || t==null || trader?.TraderKind==null || trader.TraderKind.defName!="LP_Caravan_BlackNetwork" || !Contraband.Contains(t.def.defName))return;
   __result=trader.TraderKind.WillTrade(t.def);
  }
 }
 // 商队成员按种族固定：TraderKindDef 指定只用带这个商人 PawnKind 的 Trader 组。
 public class TraderGroupExtension : DefModExtension { public PawnKindDef traderKind; }

 public static class Factions {
  public static readonly string[] Toggles={"colony","hostile","ember","illithid","blacknet","redhawk"};
  static FactionDef colony,hostile,ember,illithid,blacknet,zealots;
  public static FactionDef ColonyDef=>colony??(colony=DefDatabase<FactionDef>.GetNamedSilentFail("LP_FaerunColony"));
  public static FactionDef HostileDef=>hostile??(hostile=DefDatabase<FactionDef>.GetNamedSilentFail("LP_FaerunHostile"));
  public static FactionDef EmberDef=>ember??(ember=DefDatabase<FactionDef>.GetNamedSilentFail("LP_EmberClan"));
  public static FactionDef IllithidDef=>illithid??(illithid=DefDatabase<FactionDef>.GetNamedSilentFail("LP_IllithidColony"));
  public static FactionDef BlackNetDef=>blacknet??(blacknet=DefDatabase<FactionDef>.GetNamedSilentFail("LP_BlackNetwork"));
  public static FactionDef ZealotsDef=>zealots??(zealots=DefDatabase<FactionDef>.GetNamedSilentFail("LP_Zealots"));
  public static IEnumerable<FactionDef> Managed=>DefDatabase<FactionDef>.AllDefsListForReading.Where(d=>d.GetModExtension<FactionExtension>()!=null);
  public static FactionExtension Ext(FactionDef def)=>def?.GetModExtension<FactionExtension>();
  public static FactionExtension Ext(Faction f)=>Ext(f?.def);

  public static bool ToggleOn(string toggle){
   var s=LichMod.Settings;
   switch(toggle){case "colony":return s.factionColony;case "hostile":return s.factionHostile;case "ember":return s.factionEmber;case "illithid":return s.factionIllithid;case "blacknet":return s.factionBlackNet;case "redhawk":return s.factionRedHawk;default:return true;}
  }
  // 未受管的派系视为开启。
  public static bool Enabled(FactionDef def){var e=Ext(def);return e==null || ToggleOn(e.toggle);}
  public static bool Enabled(Faction f)=>f!=null && Enabled(f.def);
  public static Faction Of(FactionDef def)=>def==null || Find.FactionManager==null?null:Find.FactionManager.FirstFactionOfDef(def);
  public static Faction Colony=>Of(ColonyDef);
  public static Faction Hostile=>Of(HostileDef);
  public static Faction Ember=>Of(EmberDef);
  public static Faction Illithid=>Of(IllithidDef);

  // 关掉的派系不进入新世界。创建世界界面按 startingCountAtWorldCreation 列出派系（隐藏派系也在列表里，只是不显示）；
  // 不经过该界面的世界生成（快速测试）只加 requiredCountAtGameStart 的派系，所以两个都跟着开关走。
  public static void ApplyToggles(){
   foreach(var def in Managed){int n=Enabled(def)?1:0;def.startingCountAtWorldCreation=n;def.requiredCountAtGameStart=n;}
  }
  public static void FilterWorldFactions(List<FactionDef> defs){defs?.RemoveAll(d=>!Enabled(d));}

  // 已有存档：隐藏派系按需补上（没有据点，不需要世界地块）。
  public static Faction EnsureHidden(FactionDef def){
   if(def==null || !def.hidden || Find.FactionManager==null)return null;
   var f=Of(def);if(f!=null)return f;
   if(!Enabled(def))return null;
   f=FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
   Find.FactionManager.Add(f);
   return f;
  }

  public static FactionRelationKind KindFor(int goodwill)=>goodwill<=-75?FactionRelationKind.Hostile:goodwill>=75?FactionRelationKind.Ally:FactionRelationKind.Neutral;
  // 直接改双方关系（世界生成、剧本、测试用），不发信。
  public static void SetRelation(Faction a,Faction b,int goodwill){
   if(a==null || b==null || a==b)return;
   goodwill=Mathf.Clamp(goodwill,-100,100);
   foreach(var (x,y) in new[]{(a,b),(b,a)}){var r=x.RelationWith(y,true);if(r==null)continue;r.baseGoodwill=goodwill;r.kind=KindFor(goodwill);}
  }
  // 按需求里的精确数值改好感（原版 TryAffectGoodwillWith 会按当前关系缩放负值），并照常检查敌对/同盟门槛、发信。
  public static void ChangeGoodwillExact(Faction f,int delta,string reason){
   if(f==null || f.IsPlayer || !f.HasGoodwill || f.def.permanentEnemy)return;
   int target=Mathf.Clamp(f.BaseGoodwillWith(Faction.OfPlayer)+delta,-100,100);
   var a=f.RelationWith(Faction.OfPlayer,true);var b=Faction.OfPlayer.RelationWith(f,true);if(a==null || b==null)return;
   a.baseGoodwill=target;b.baseGoodwill=target;
   a.CheckKindThresholds(f,true,reason,GlobalTargetInfo.Invalid,out bool _);
   b.kind=a.kind;
  }
  public static void MakeInitialRelations(Faction self,Faction other){
   if(self==null || other==null)return;
   var es=Ext(self);var eo=Ext(other);
   if(es!=null && other.IsPlayer)SetRelation(self,other,self.def.permanentEnemy?-100:es.initialGoodwill);
   else if(eo!=null && self.IsPlayer)SetRelation(self,other,other.def.permanentEnemy?-100:eo.initialGoodwill);
   if((es!=null && es.enemyFactions.Contains(other.def)) || (eo!=null && eo.enemyFactions.Contains(self.def)))SetRelation(self,other,-100);
  }
  public static void EnsureEnemyPairs(){
   foreach(var def in Managed){var e=Ext(def);var f=Of(def);if(f==null)continue;foreach(var other in e.enemyFactions){var o=Of(other);if(o!=null && f.RelationKindWith(o)!=FactionRelationKind.Hostile)SetRelation(f,o,-100);}}
  }

  // 把开启的可见派系加入当前存档（设置界面按钮）：生成派系、关系和 3 个据点。
  public static int AddMissingToCurrentGame(){
   if(Current.ProgramState!=ProgramState.Playing || Find.World==null)return 0;
   int added=0;
   foreach(var def in Managed.Where(d=>!d.hidden && Enabled(d) && Of(d)==null).ToList()){
    var f=FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def));
    Find.FactionManager.Add(f);
    foreach(var other in Find.FactionManager.AllFactionsListForReading)if(other!=f){if(f.RelationWith(other,true)==null)f.TryMakeInitialRelationsWith(other);MakeInitialRelations(f,other);}
    for(int i=0;i<3;i++){
     var tile=TileFinder.RandomSettlementTileFor(f);if(!tile.Valid)break;
     var s=(Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);s.SetFaction(f);s.Tile=tile;s.Name=SettlementNameGenerator.GenerateSettlementName(s);Find.WorldObjects.Add(s);
    }
    added++;
   }
   EnsureEnemyPairs();
   foreach(var def in Managed.Where(d=>d.hidden))EnsureHidden(def);
   return added;
  }
 }

 [StaticConstructorOnStartup]
 public static class FactionStartup {static FactionStartup(){Factions.ApplyToggles();}}

 [HarmonyPatch(typeof(FactionGenerator),"InitializeFactions")]
 public static class FactionWorldGenFilter {static void Prefix(List<FactionDef> factions){Factions.FilterWorldFactions(factions);}}

 [HarmonyPatch(typeof(Faction),nameof(Faction.TryMakeInitialRelationsWith))]
 public static class FactionInitialRelations {static void Postfix(Faction __instance,Faction other){Factions.MakeInitialRelations(__instance,other);}}

 // 关掉的派系不再袭击；向红袍进贡期间塞伊远征军袭击权重减半。
 [HarmonyPatch(typeof(FactionDef),nameof(FactionDef.RaidCommonalityFromPoints))]
 public static class FactionRaidWeight {
  static void Postfix(FactionDef __instance,ref float __result){
   if(__result<=0 || Factions.Ext(__instance)==null)return;
   if(!Factions.Enabled(__instance)){__result=0;return;}
   if(__instance==Factions.HostileDef && Tribute.RaidHalved)__result*=.5f;
  }
 }
 // 关掉的派系不再派商队；黑网只走自己的事件。
 [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival),"FactionCanBeGroupSource")]
 public static class FactionCaravanSource {
  static void Postfix(Faction f,ref bool __result){if(__result && f!=null && Factions.Ext(f)!=null && (!Factions.Enabled(f) || f.def==Factions.BlackNetDef || f.def==Factions.ZealotsDef))__result=false;}
 }
 [HarmonyPatch(typeof(PawnGroupKindWorker_Trader),nameof(PawnGroupKindWorker_Trader.CanGenerateFrom))]
 public static class FactionTraderGroup {
  static void Postfix(PawnGroupMakerParms parms,PawnGroupMaker groupMaker,ref bool __result){
   if(!__result)return;var kind=parms?.traderKind?.GetModExtension<TraderGroupExtension>()?.traderKind;
   if(kind!=null)__result=groupMaker.traders!=null && groupMaker.traders.Any(o=>o.kind==kind);
  }
 }
 [HarmonyPatch(typeof(PawnGroupMakerUtility),nameof(PawnGroupMakerUtility.GeneratePawns))]
 public static class FactionGroupPostProcess {
  static void Postfix(PawnGroupMakerParms parms,ref IEnumerable<Pawn> __result){if(Factions.Ext(parms?.faction)!=null)__result=FactionGroups.Process(__result,parms);}
 }

 // 袭击组生成后的派系专属处理：火巨人数量、红袍造物、被支配奴仆、斩首分组。
 public static class FactionGroups {
  static PawnKindDef giantKind,skeletonKind,golemKind,flayerKind,thrallKind;static HediffDef thralled;
  public static PawnKindDef GiantKind=>giantKind??(giantKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_EC_FireGiant"));
  public static PawnKindDef SkeletonKind=>skeletonKind??(skeletonKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FH_Skeleton"));
  public static PawnKindDef GolemKind=>golemKind??(golemKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FH_Golem"));
  public static PawnKindDef FlayerKind=>flayerKind??(flayerKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_IC_MindFlayer"));
  public static PawnKindDef ThrallKind=>thrallKind??(thrallKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_IC_Thrall"));
  public static HediffDef Thralled=>thralled??(thralled=DefDatabase<HediffDef>.GetNamedSilentFail("LP_Thralled"));
  public const int MaxGiants=2;
  static bool Fighting(PawnGroupMakerParms parms)=>parms.groupKind==PawnGroupKindDefOf.Combat || parms.groupKind==PawnGroupKindDefOf.Settlement;

  public static IEnumerable<Pawn> Process(IEnumerable<Pawn> source,PawnGroupMakerParms parms){
   var list=source?.ToList()??new List<Pawn>();
   if(list.Count==0)return list;
   var ext=Factions.Ext(parms.faction);var def=parms.faction.def;
   if(def==Factions.EmberDef){EnsureLeaders(list,parms,GiantKind,LeaderTarget(ext,parms),MaxGiants);foreach(var p in list)if(p.kindDef==GiantKind || FireGiant.Active(p))EquipGiant(p);}
   if(def==Factions.IllithidDef){EnsureLeaders(list,parms,FlayerKind,LeaderTarget(ext,parms),ext.leaderCount.max);FixFlayers(list,parms);foreach(var p in list)if(p.kindDef==ThrallKind)Thrall(p);}
   if(def==Factions.HostileDef && ext.constructsPerLeader.max>0){
    foreach(var wizard in list.Where(p=>ext.leaderKinds.Contains(p.kindDef)).ToList()){
     int n=ext.constructsPerLeader.RandomInRange;
     for(int i=0;i<n;i++)list.Add(FactionConstructs.MakeBound(i==0 && parms.points>=1000 && Rand.Chance(.35f)?"Golem":"Skeleton",wizard,parms.faction));
    }
   }
   // 1.24.0：寒冰魔（≥1500点，最多2个）、炎魔（≥3500点，最多1个）。
   if(def==Factions.HostileDef)Fiends.TrimRaid(list,parms);
   // 1.30.0：霜巨人掠夺者（≥800点才有，看袭击地图的季节，随行1～2只冬狼）。
   if(def==Factions.HostileDef)FrostGiantRaid.Apply(list,parms);
   // 1.33.0：六臂蛇人剑舞者（≥1000点、15%，只在启用 HAR 时有这个兵种）。
   if(def==Factions.HostileDef)MarilithRaid.Apply(list,parms);
   // 1.34.0：被奴役的风暴巨人（≥4000点、10%，戴镣铐）。
   if(def==Factions.HostileDef)StormGiantRaid.Apply(list,parms);
   // 1.44.0（只在启用 HAR 时有这些兵种）：双头蛮兵（≥800点、10%）；同盟商队的独眼巨人护卫（10%）。
   if(def==Factions.HostileDef)EttinRaid.Apply(list,parms);
   if(def==Factions.ColonyDef)CyclopsGuard.Apply(list,parms);
   Decapitation.Assign(list,parms.faction);
   return list;
  }
  // 领队目标数：在 leaderCount 范围里随机，但每 1200 点最多多一个（小袭击只有 1 个）。非战斗组为 0（不补）。
  public static int LeaderTarget(FactionExtension ext,PawnGroupMakerParms parms){
   if(!Fighting(parms) || ext.leaderCount.max<=0)return 0;
   return Mathf.Clamp(ext.leaderCount.RandomInRange,ext.leaderCount.min,Math.Max(ext.leaderCount.min,1+(int)(parms.points/1200f)));
  }
  // 红袍：长袍固定染红（原版的 specificApparelRequirements 颜色会被染色/意识形态覆盖，这里直接设）。
  public static readonly Color Red=new Color(.70f,.07f,.07f);
  public static void RedRobe(Pawn p){
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_Robe");if(def==null || p?.apparel==null)return;
   var robe=p.apparel.WornApparel.FirstOrDefault(a=>a.def==def);
   if(robe==null){robe=(Apparel)ThingMaker.MakeThing(def,ThingDefOf.Cloth);if(!ApparelUtility.HasPartsToWear(p,def)){robe.Destroy();return;}p.apparel.Wear(robe,false);}
   robe.SetColor(Red,false);
  }
  // 领队数量限制在 [min,max]：不够就补，多了就裁掉。每补一个领队裁掉一个普通随从抵消点数。
  // 1.63.0：原版挑异种型时会把派系的异种型表和兵种自己的表合起来算，夺心魔兵种偶尔被生成成普通人（完整测试 Factions 时好时坏的根因）。
  // 这种人换成强制夺心魔异种型重新生成的一个。
  public static void FixFlayers(List<Pawn> list,PawnGroupMakerParms parms){
   var x=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_MindFlayer");if(FlayerKind==null || x==null)return;
   for(int i=0;i<list.Count;i++){
    var p=list[i];if(p.kindDef!=FlayerKind || MindFlayer.IsMindFlayer(p))continue;
    var n=PawnGenerator.GeneratePawn(new PawnGenerationRequest(FlayerKind,parms.faction,PawnGenerationContext.NonPlayer,parms.tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true,inhabitant:parms.inhabitants,forcedXenotype:x));
    list[i]=n;Discard(p);
   }
  }
  public static void EnsureLeaders(List<Pawn> list,PawnGroupMakerParms parms,PawnKindDef kind,int min,int max){
   if(kind==null)return;
   var leaders=list.Where(p=>p.kindDef==kind).ToList();
   for(int i=max;i<leaders.Count;i++){list.Remove(leaders[i]);Discard(leaders[i]);}
   for(int have=Math.Min(leaders.Count,max);have<Math.Min(min,max);have++){
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,parms.faction,PawnGenerationContext.NonPlayer,parms.tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true,inhabitant:parms.inhabitants));
    list.Insert(0,p);
    var spare=list.LastOrDefault(x=>x.kindDef!=kind && !x.kindDef.trader && Necro.Construct(x)==null);
    if(spare!=null && list.Count(x=>x.kindDef!=kind)>2){list.Remove(spare);Discard(spare);}
   }
  }
  // 被裁掉的人从没登场过：直接销毁，不进世界角色池。
  static void Discard(Pawn p){if(p==null || p.Destroyed)return;if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}
  public static void Thrall(Pawn p){if(p!=null && MindFlayer.IsMindFlayer(p))return;if(Thralled!=null && p?.health!=null && !p.health.hediffSet.HasHediff(Thralled))p.health.AddHediff(Thralled);}
  public static bool IsThralled(Pawn p)=>Thralled!=null && p?.health?.hediffSet?.HasHediff(Thralled)==true;
  // 火巨人领队：熔炉重锤、熔铁战盔、熔铁重甲。
  public static void EquipGiant(Pawn p){
   if(p?.equipment==null || p.apparel==null || !FireGiant.Active(p))return;
   var maul=DefDatabase<ThingDef>.GetNamedSilentFail("LP_FireGiantMaul");
   if(maul!=null && p.equipment.Primary?.def!=maul && !p.WorkTagIsDisabled(WorkTags.Violent)){p.equipment.DestroyAllEquipment();var w=(ThingWithComps)ThingMaker.MakeThing(maul);w.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityTraderItem(),ArtGenerationContext.Outsider);p.equipment.AddEquipment(w);}
   foreach(var name in new[]{"LP_FireGiantArmor","LP_FireGiantHelmet"}){
    var d=DefDatabase<ThingDef>.GetNamedSilentFail(name);if(d==null || p.apparel.WornApparel.Any(a=>a.def==d))continue;
    var a=(Apparel)ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);a.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Outsider);
    if(ApparelUtility.HasPartsToWear(p,d))p.apparel.Wear(a,false);else a.Destroy();
   }
  }
 }

 // 死灵造物（单名、人类种族）不能被原版生成亲属关系时选作配偶/亲属：原版给配偶改姓时假定对方是三段名，会空引用。
 [HarmonyPatch]
 public static class ConstructsHaveNoRelatives {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){
   foreach(var t in typeof(PawnRelationWorker).Assembly.GetTypes().Where(t=>typeof(PawnRelationWorker).IsAssignableFrom(t))){
    var m=AccessTools.DeclaredMethod(t,nameof(PawnRelationWorker.GenerationChance));if(m!=null)yield return m;
   }
  }
  static void Postfix(Pawn generated,Pawn other,ref float __result){if(__result>0 && (Excluded(generated) || Excluded(other)))__result=0;}
  static bool Excluded(Pawn p)=>p!=null && (Necro.Construct(p)!=null || (p.RaceProps.Humanlike && p.Name is NameSingle));
 }
 // 固定装备：圣武士板甲+原版近战武器、牧师长袍、红袍长袍染红。原版按预算随机挑装备，偶尔挑不出来，这里补齐。
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class FactionGearOnGeneration {
  static void Postfix(Pawn __result){if(__result?.kindDef!=null && __result.kindDef.defName.StartsWith("LP_"))FactionGear.Ensure(__result);}
 }
 public static class FactionGear {
  public static void Ensure(Pawn p){
   switch(p.kindDef.defName){
    // 1.45.1 派系换装：圣武士板甲 + 誓约巨盔 + 骑士披风；骑士加披风；牧师穿晨主圣袍 + 战斗牧师胸甲；余烬奴兵制服；吉斯洋基星界战甲；红袍法师穿塞伊红袍。
    case "LP_FC_Paladin":Wear(p,"LP_PaladinPlate",ThingDefOf.Steel);Wear(p,"LP_GreatHelmOath",ThingDefOf.Steel);Wear(p,"LP_KnightMantle",ThingDefOf.Cloth);Arm(p,"MeleeWeapon_LongSword",ThingDefOf.Steel);break;
    case "LP_FC_Knight":Wear(p,"Apparel_PlateArmor",ThingDefOf.Steel);Wear(p,"LP_KnightMantle",ThingDefOf.Cloth);Arm(p,"MeleeWeapon_LongSword",ThingDefOf.Steel);break;
    case "LP_FC_Priest":case "LP_FC_ChurchPriest":Wear(p,"LP_MorninglordVestments",ThingDefOf.Cloth);Wear(p,"LP_BattleClericBreastplate",ThingDefOf.Steel);break;
    case "LP_FC_ElfRanger":Wear(p,"LP_RangerLeathers",DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Plain"));Wear(p,"LP_WoodwalkerCloak",ThingDefOf.Cloth);break;
    case "LP_EC_HumanSlave":Wear(p,"LP_EmberCollarPlate",ThingDefOf.Steel);Wear(p,"LP_EmberRivetHelm",ThingDefOf.Steel);break;
    case "LP_FH_GithMercenary":Wear(p,"LP_GithAstralArmor",ThingDefOf.Steel);break;
    case "LP_FH_RedWizard":Wear(p,"LP_RedWizardRobe",null);break;
    case "LP_FH_RedTrader":FactionGroups.RedRobe(p);break;
    // 1.24.0：象人卫士重甲；食人魔打手60%拿食人魔大棒；寒冰魔30%拿霜刺长枪；炎魔必带火鞭。用按角色编号播种的随机数，不扰动全局随机流。
    case "LP_FC_LoxodonWarden":Wear(p,"LP_LoxodonWardenArmor",ThingDefOf.Steel);if(Items.Worn(p,"LP_LoxodonWardenArmor")==null)Wear(p,"Apparel_PlateArmor",ThingDefOf.Steel);Wear(p,"Apparel_SimpleHelmet",ThingDefOf.Steel);break; // 体型不到1.3（未成年）时退回原版板甲
    case "LP_FH_OgreBrute":if(Rand.ValueSeeded(p.thingIDNumber^0x0617)<.6f)Replace(p,"LP_OgreClub",ThingDefOf.WoodLog);Wear(p,"LP_OgreTrophyHide",DefDatabase<ThingDef>.GetNamedSilentFail("Leather_Plain"));break;
    case "LP_FH_IceDevil":if(Rand.ValueSeeded(p.thingIDNumber^0x1CED)<.3f)Replace(p,"LP_FrostSpear",null);else p.equipment?.DestroyAllEquipment();p.apparel?.DestroyAll();break;
    case "LP_FH_Balor":Replace(p,"LP_BalorWhip",null);p.apparel?.DestroyAll();break;
    // 1.30.0：霜巨人掠夺者60%霜巨人斧、40%钢长矛，穿冬狼皮大氅；迁徙的霜巨人战士同样打扮。
    case "LP_FH_FrostGiantRaider":case "LP_FC_FrostGiantWanderer":if(Rand.ValueSeeded(p.thingIDNumber^0x0F05)<.6f)Replace(p,FrostGiant.Axe,ThingDefOf.Steel);else Replace(p,"MeleeWeapon_Spear",ThingDefOf.Steel);Wear(p,FrostGiant.Cloak,null);break;
   }
  }
  public static void Replace(Pawn p,string name,ThingDef stuff){
   var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);if(def==null || p.equipment==null || p.WorkTagIsDisabled(WorkTags.Violent))return;
   p.equipment.DestroyAllEquipment();var w=(ThingWithComps)ThingMaker.MakeThing(def,def.MadeFromStuff?(stuff??GenStuff.DefaultStuffFor(def)):null);w.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Outsider);p.equipment.AddEquipment(w);
  }
  public static void Wear(Pawn p,string name,ThingDef stuff){
   var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);if(def==null || p.apparel==null || p.apparel.WornApparel.Any(a=>a.def==def) || !ApparelUtility.HasPartsToWear(p,def))return;
   var a=(Apparel)ThingMaker.MakeThing(def,def.MadeFromStuff?stuff:null);a.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Outsider);p.apparel.Wear(a,false);
  }
  public static void Arm(Pawn p,string name,ThingDef stuff){
   if(p.equipment==null || p.WorkTagIsDisabled(WorkTags.Violent) || (p.equipment.Primary!=null && p.equipment.Primary.def.IsMeleeWeapon))return;
   var def=DefDatabase<ThingDef>.GetNamedSilentFail(name);if(def==null)return;
   p.equipment.DestroyAllEquipment();var w=(ThingWithComps)ThingMaker.MakeThing(def,def.MadeFromStuff?stuff:null);w.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Outsider);p.equipment.AddEquipment(w);
  }
 }
 // 红袍法师名下的死灵造物：沿用模组造物（Hediff_Construct），主人是法师而不是巫妖。
 public static class FactionConstructs {
  public static Pawn MakeBound(string kind,Pawn master,Faction faction){
   var pk=kind=="Golem"?FactionGroups.GolemKind:FactionGroups.SkeletonKind;
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(pk,faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:25,fixedChronologicalAge:25,forcedXenotype:XenotypeDefOf.Baseliner));
   p.apparel?.DestroyAll();if(kind=="Golem")p.equipment?.DestroyAllEquipment();p.inventory?.innerContainer.ClearAndDestroyContents();
   foreach(var t in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(t);
   var h=(Hediff_Construct)p.health.AddHediff(Necro.Def<HediffDef>("Construct"));h.kind=kind;h.master=master;h.raidBound=true;h.flesh=kind=="Golem"?200:0;
   p.Name=new NameSingle(kind=="Golem"?"红袍血肉巨像":"红袍骷髅");
   foreach(var sk in p.skills.skills){sk.Level=0;sk.passion=Passion.None;sk.xpSinceLastLevel=0;}
   p.skills.GetSkill(SkillDefOf.Melee).Level=kind=="Golem"?10:6;
   h.UpgradeColonist();p.health.Notify_HediffChanged(h);Necro.Registry?.RegisterConstruct(p);Necro.Refresh(p);
   return p;
  }
 }
 public partial class Hediff_Construct {
  // 1.19.0：袭击里绑定在红袍法师名下的造物。法师站着它就能动；法师倒地或死亡由斩首处理（崩解）。
  public bool raidBound;
  public bool RaidControl=>master!=null && !master.Dead && !master.Downed && !master.Destroyed && master.Faction==pawn.Faction;
 }

 // 抽取巨魔血：两瓶巨魔血，造成失血。
 public class Recipe_ExtractTrollBlood : Recipe_Surgery {
  public const float BloodLoss=.3f;public const int Count=2;
  // 1.22.0：同一个巨魔抽血后要隔几天（设置页，默认3天）才能再抽。
  public static bool RecentlyBled(Pawn p)=>Items.Has(p,"LP_RecentlyBled");
  public override bool AvailableOnNow(Thing thing,BodyPartRecord part=null)=>thing is Pawn p && Troll.Is(p) && !RecentlyBled(p) && base.AvailableOnNow(thing,part);
  public override void ApplyOnPawn(Pawn pawn,BodyPartRecord part,Pawn billDoer,List<Thing> ingredients,Bill bill){
   if(billDoer!=null && CheckSurgeryFail(billDoer,pawn,ingredients,part,bill))return;
   var loss=pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
   if(loss==null){loss=HediffMaker.MakeHediff(HediffDefOf.BloodLoss,pawn);loss.Severity=BloodLoss;pawn.health.AddHediff(loss);}else loss.Severity+=BloodLoss;
   var blood=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_TrollBlood"));blood.stackCount=Count;
   if(pawn.SpawnedOrAnyParentSpawned)GenPlace.TryPlaceThing(blood,pawn.PositionHeld,pawn.MapHeld,ThingPlaceMode.Near);
   Items.Give(pawn,"LP_RecentlyBled",LichMod.Settings.trollBleedDays*GenDate.TicksPerDay);
   if(billDoer!=null)ReportViolation(pawn,billDoer,pawn.HomeFaction,-50);
  }
 }
}
