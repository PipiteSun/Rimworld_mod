using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld.Planet;
using UnityEngine;

namespace LichPhylactery {
 // 1.30.0 霜巨人：本源（巨人血统 rank 4、2.0 体型、极寒之躯）、雪原行者、畏火、冬狼之主、战利品之心，以及冬之号角。
 public static class FrostGiant {
  public const string Core="LP_FrostGiantCore",SnowWalker="LP_FrostSnowWalker",WolfLordGene="LP_FrostWolfLord",LootHeart="LP_FrostLootHeart";
  public const string Axe="LP_FrostGiantAxe",Cloak="LP_WinterWolfCloak",Horn="LP_FrostGiantHorn",WinterWolf="LP_WinterWolf";
  public const float WolfTame=3f,OtherWolfTame=1.5f,WolfMelee=1.15f,WolfMove=1.1f,HornHit=.15f,LootPer=1000f;
  public const int HornRange=15,HornTicks=3600,AxeSlowTicks=240,SpoilsCheck=2500,LootMax=4;
  public static bool Active(Pawn p)=>Races124.Has(p,Core);
  public static bool Has(Pawn p,string gene)=>Races124.Has(p,gene);
  // 寒气侵体（寒冰魔）与寒霜迟缓（冬狼咬、霜巨人斧）对霜巨人无效。法杖的寒霜缠身 LP_FrostStaffChill 照常生效。
  public static bool ChillImmune(Pawn p,string hediff)=>(hediff=="LP_Chilled" || hediff=="LP_FrostSlow") && Active(p);
  public static bool IsWinterWolf(Pawn p)=>p?.def?.defName==WinterWolf;
  public static bool WolfLord(Pawn p)=>Has(p,WolfLordGene);
  public static float TameFactor(Pawn tamer,Pawn animal){
   if(animal==null || !WolfLord(tamer))return 1f;
   switch(animal.def.defName){case WinterWolf:return WolfTame;case "Wolf_Arctic":case "Wolf_Timber":return OtherWolfTame;}
   return 1f;
  }
  // 主人有冬狼之主的冬狼：近战伤害×115%、移速×110%。
  public static bool LordedWolf(Pawn p)=>IsWinterWolf(p) && WolfLord(p.playerSettings?.Master);

  // ---------- 冬之号角 ----------
  public static bool HasHorn(Pawn p)=>Items.Worn(p,Horn)!=null;
  // full：霜巨人戴号角吹奏，15格内友方得到“冬之号角”（近战命中+15%、免疫恐惧60秒）；冬狼另得“狂猎”（移速×130%，60秒）。非霜巨人只给冬狼加成。
  public static int Blow(Pawn caster,bool full){
   var map=caster?.Map;if(map==null)return 0;int n=0;
   foreach(var p in Races124.PawnsNear(map,caster.Position,HornRange).ToList()){
    if(p!=caster && (p.Faction==null || p.Faction!=caster.Faction))continue;
    if(full && p.RaceProps.Humanlike){Items.Give(p,"LP_WinterHorn",HornTicks);n++;}
    if(IsWinterWolf(p)){Items.Give(p,"LP_WildHunt",HornTicks);n++;}
   }
   FleckMaker.Static(caster.Position,map,FleckDefOf.PsycastAreaEffect,6f);
   Items.Mote(caster,"冬之号角！",new Color(.75f,.9f,1f));
   return n;
  }

  // ---------- 战利品之心 ----------
  // 打退袭击：3天“战利品”+5。地图上非殖民地制造的武器、护甲（散落或入库，不含穿在身上的）市值每1000银+1，最多+4，每2500刻重算。
  public static int GiveSpoils(Map map){
   var def=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_FrostSpoils");if(map==null || def==null)return 0;int n=0;
   foreach(var p in map.mapPawns.FreeColonistsSpawned)if(Has(p,LootHeart) && p.needs?.mood?.thoughts?.memories!=null){p.needs.mood.thoughts.memories.TryGainMemory(def);n++;}
   return n;
  }
  public static bool RaidJob(LordJob job)=>job is LordJob_AssaultColony || job is LordJob_StageThenAttack || job is LordJob_Siege || job is LordJob_AssaultThings;
  static readonly Dictionary<int,(int tick,float value)> loot=new Dictionary<int,(int,float)>();
  public static float LootValue(Map map,bool refresh=false){
   if(map==null)return 0f;int now=Find.TickManager.TicksGame;
   if(!refresh && loot.TryGetValue(map.uniqueID,out var c) && now-c.tick<SpoilsCheck && now>=c.tick)return c.value;
   var crafted=GiantSpoilsState.Get;float v=0f;
   foreach(var group in new[]{ThingRequestGroup.Weapon,ThingRequestGroup.Apparel})
    foreach(var t in map.listerThings.ThingsInGroup(group))if(t.Spawned && (crafted==null || !crafted.Crafted(t)))v+=t.MarketValue*t.stackCount;
   loot[map.uniqueID]=(now,v);return v;
  }
  public static int LootStage(Map map)=>Mathf.Min(LootMax,Mathf.FloorToInt(LootValue(map)/LootPer));
 }

 // 塞伊远征军的霜巨人掠夺者：不进 XML 的兵种表，由这里按点数与季节加进战斗组。
 // 点数<800 一个都没有；≥800 时出现概率 35%，袭击地图在冬季×2（70%）、夏季×0.5；≥2500 点一半概率来两个。
 // 每个霜巨人顶掉2名普通随从，每只冬狼顶掉1名（保留至少2名其他成员）。
 public static class FrostGiantRaid {
  public const float MinPoints=800f,BaseChance=.35f,TwoPoints=2500f;
  public static float? TestRoll;
  static PawnKindDef raider,wolf;
  public static PawnKindDef RaiderKind=>raider??(raider=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FH_FrostGiantRaider"));
  public static PawnKindDef WolfKind=>wolf??(wolf=DefDatabase<PawnKindDef>.GetNamedSilentFail(FrostGiant.WinterWolf));
  static float Roll=>TestRoll??Rand.Value;
  public static float SeasonFactor(PawnGroupMakerParms parms){
   if(parms==null || !parms.tile.Valid)return 1f;
   switch(GenLocalDate.Season(parms.tile)){case Season.Winter:case Season.PermanentWinter:return 2f;case Season.Summer:case Season.PermanentSummer:return .5f;}
   return 1f;
  }
  public static float Chance(PawnGroupMakerParms parms)=>Mathf.Min(1f,BaseChance*SeasonFactor(parms));
  public static int Apply(List<Pawn> list,PawnGroupMakerParms parms){
   var kind=RaiderKind;if(kind==null || list==null || parms==null)return 0;
   if(parms.points<MinPoints){foreach(var p in list.Where(x=>x.kindDef==kind).ToList())Discard(list,p);return 0;}
   if(parms.groupKind!=PawnGroupKindDefOf.Combat || list.Any(x=>x.kindDef==kind) || Roll>=Chance(parms))return 0;
   int giants=parms.points>=TwoPoints && Roll<.5f?2:1,added=0;
   for(int i=0;i<giants;i++){
    var g=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,parms.faction,PawnGenerationContext.NonPlayer,parms.tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true,inhabitant:parms.inhabitants));
    list.Insert(0,g);added++;Spare(list,2);
    if(WolfKind!=null)for(int w=Rand.RangeInclusive(1,2);w>0;w--){list.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(WolfKind,parms.faction,PawnGenerationContext.NonPlayer,parms.tile,forceGenerateNewPawn:true)));Spare(list,1);}
   }
   return added;
  }
  static bool Ordinary(Pawn x)=>x.RaceProps.Humanlike && x.kindDef!=RaiderKind && x.kindDef!=StormGiantRaid.Kind &&!x.kindDef.trader && Necro.Construct(x)==null && !Fiends.Is(x) && !(Factions.Ext(x.Faction)?.leaderKinds.Contains(x.kindDef)??false);
  public static void Spare(List<Pawn> list,int n){for(int i=0;i<n;i++){if(list.Count(Ordinary)<=2)return;Discard(list,list.Last(Ordinary));}}
  static void Discard(List<Pawn> list,Pawn p){list.Remove(p);if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}
 }

 public class Gene_FrostGiant : Gene {
  public override void PostAdd(){
   base.PostAdd();GiantKin.Invalidate(pawn);
   if(pawn?.apparel!=null)foreach(var a in pawn.apparel.WornApparel.ToArray())if(!GiantGear.CanWear(pawn,a.def))pawn.apparel.TryDrop(a);
  }
  public override void PostRemove(){GiantGear.Release(pawn);base.PostRemove();}
 }

 // 雪原行者：没有原版“寒冷”（EnvironmentCold）心情。霜巨人的裸体心情由 FireGiantNakedMood 一并处理。
 [HarmonyPatch(typeof(ThoughtWorker),nameof(ThoughtWorker.CurrentState))]
 public static class SnowWalkerNoColdMood {
  static void Postfix(ThoughtWorker __instance,Pawn __0,ref ThoughtState __result){if(__result.Active && __instance.def?.defName=="EnvironmentCold" && SnowWalking.Applies(__0))__result=ThoughtState.Inactive;}
 }
 // 极寒之躯：原版失温（含冻伤）不会发生在霜巨人身上；伤害上的冻伤由基因 damageFactors 免疫。
 [HarmonyPatch(typeof(HediffGiver_Hypothermia),nameof(HediffGiver_Hypothermia.OnIntervalPassed))]
 public static class FrostGiantNoHypothermia {
  static bool Prefix(Pawn pawn)=>!FrostGiant.Active(pawn);
 }

 // 雪原行者：原版 1.7 在 PathGrid.CalculatedCostAt 里加积雪的移动消耗（WeatherBuildupUtility.MovementTicksAddOn(snowGrid.GetCategory(c))）；
 // 走路的移动消耗（Pawn_PathFollower.CostToMoveIntoCell）会调它，这一刻把积雪那一项换成 0。寻路的预算成本不改。
 public static class SnowWalking {
  [ThreadStatic] public static bool free;
  public static bool Applies(Pawn p)=>p!=null && FrostGiant.Has(p,FrostGiant.SnowWalker);
  public static int AddOn(WeatherBuildupCategory c)=>free?0:WeatherBuildupUtility.MovementTicksAddOn(c);
  public static bool Patched;
 }
 [HarmonyPatch(typeof(Pawn_PathFollower),"CostToMoveIntoCell",new[]{typeof(Pawn),typeof(IntVec3)})]
 public static class SnowWalkerMoveCost {
  // 1.34.1：云巨人的“踏云”同样不受积雪拖累（“寒冷”心情照旧）。
  static void Prefix(Pawn pawn,out bool __state){__state=SnowWalking.free;SnowWalking.free=SnowWalking.Applies(pawn) || CloudGiant.Walks(pawn);}
  static void Finalizer(bool __state){SnowWalking.free=__state;}
 }
 [HarmonyPatch(typeof(PathGrid),"CalculatedCostAt")]
 public static class SnowWalkerCellCost {
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions){
   var code=instructions.ToList();var snow=AccessTools.Field(typeof(Map),nameof(Map.snowGrid));var addOn=AccessTools.Method(typeof(WeatherBuildupUtility),nameof(WeatherBuildupUtility.MovementTicksAddOn));
   for(int i=0;i<code.Count;i++){
    if(!code[i].LoadsField(snow))continue;
    for(int j=i+1;j<code.Count && j<i+6;j++)if(code[j].Calls(addOn)){code[j]=new CodeInstruction(OpCodes.Call,AccessTools.Method(typeof(SnowWalking),nameof(SnowWalking.AddOn))).MoveLabelsFrom(code[j]);SnowWalking.Patched=true;break;}
    if(SnowWalking.Patched)break;
   }
   if(!SnowWalking.Patched)Log.Warning("[DND边缘大陆] 没找到原版积雪移动消耗的位置，霜巨人“雪原行者”不生效。");
   return code;
  }
 }

 // 冬狼之主：主人的冬狼更强。
 public class StatPart_WolfLord : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && FrostGiant.LordedWolf(p))val*=parentStat==StatDefOf.MoveSpeed?FrostGiant.WolfMove:FrostGiant.WolfMelee;}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p) || !FrostGiant.LordedWolf(p))return null;return "冬狼之主的猎犬：×"+(parentStat==StatDefOf.MoveSpeed?FrostGiant.WolfMove:FrostGiant.WolfMelee).ToStringPercent();}
 }
 [StaticConstructorOnStartup]
 public static class FrostGiantStartup {
  static FrostGiantStartup(){
   foreach(var stat in new[]{StatDefOf.MeleeDamageFactor,StatDefOf.MoveSpeed}){if(stat.parts==null)stat.parts=new List<StatPart>();if(!stat.parts.Any(p=>p is StatPart_WolfLord))stat.parts.Add(new StatPart_WolfLord{parentStat=stat});}
  }
 }
 // 冬狼不反噬冬狼之主：驯服失败不发狂，被冬狼之主打伤也不发狂。
 [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt),nameof(InteractionWorker_RecruitAttempt.Interacted))]
 public static class WolfLordTamer {
  [ThreadStatic] public static Pawn tamer;
  static void Prefix(Pawn initiator,out Pawn __state){__state=tamer;tamer=initiator;}
  static void Finalizer(Pawn __state){tamer=__state;}
 }
 [HarmonyPatch(typeof(PawnUtility),"GetManhunterOnTameFailChance",new[]{typeof(Pawn)})]
 public static class WolfLordNoTameRevenge {
  static void Postfix(Pawn __0,ref float __result){if(__result>0 && FrostGiant.IsWinterWolf(__0) && FrostGiant.WolfLord(WolfLordTamer.tamer))__result=0f;}
 }
 [HarmonyPatch(typeof(PawnUtility),"GetManhunterOnDamageChance",new[]{typeof(Pawn),typeof(Thing),typeof(float)})]
 public static class WolfLordNoDamageRevenge {
  static void Postfix(Pawn __0,Thing __1,ref float __result){if(__result>0 && FrostGiant.IsWinterWolf(__0) && __1 is Pawn a && FrostGiant.WolfLord(a))__result=0f;}
 }

 // 冬之号角：近战命中加在最终命中率上（同 1.25.0 邪座狼群嚎的写法）。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class WinterHornHit {
  static void Postfix(Verb_MeleeAttack __instance,ref float __result){var p=__instance.CasterPawn;if(p!=null && Items.Has(p,"LP_WinterHorn"))__result=Mathf.Min(1f,__result+FrostGiant.HornHit);}
 }
 // 霜巨人斧：命中附加寒霜迟缓4秒（每次攻击一次，霜巨人自己免疫）。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class FrostAxeSlow {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){if(__instance.EquipmentSource?.def.defName==FrostGiant.Axe && target.Pawn!=null && !target.Pawn.Dead)Items.Give(target.Pawn,"LP_FrostSlow",FrostGiant.AxeSlowTicks);}
 }
 public class CompProperties_WinterHorn : CompProperties_AbilityEffect {
  public bool wolvesOnly;
  public CompProperties_WinterHorn(){compClass=typeof(CompWinterHorn);}
 }
 public class CompWinterHorn : CompAbilityEffect {
  public CompProperties_WinterHorn Props=>(CompProperties_WinterHorn)props;
  public override bool GizmoDisabled(out string reason){
   if(!Props.wolvesOnly && !FrostGiant.HasHorn(parent.pawn)){reason="需要装备霜巨人号角。";return true;}
   return base.GizmoDisabled(out reason);
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   var p=parent.pawn;if(!Props.wolvesOnly && !FrostGiant.HasHorn(p))return;
   FrostGiant.Blow(p,!Props.wolvesOnly);
  }
 }

 // ---------- 战利品之心 ----------
 // 袭击方的集团被移除（全员死亡、倒地、逃跑或离开）时，玩家主地图上的战利品之心 +5。
 [HarmonyPatch(typeof(LordManager),nameof(LordManager.RemoveLord))]
 public static class FrostSpoilsOnRaidEnd {
  static void Prefix(LordManager __instance,Lord __0){
   var lord=__0;var map=__instance.map;
   if(lord?.faction==null || map==null || !map.IsPlayerHome || !lord.faction.HostileTo(Faction.OfPlayer) || !FrostGiant.RaidJob(lord.LordJob))return;
   if(lord.ownedPawns.Any(p=>p.Spawned && !p.Dead && !p.Downed && p.Map==map && !p.InMentalState))return;
   FrostGiant.GiveSpoils(map);
  }
 }
 public class ThoughtWorker_FrostLootHoard : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!p.Spawned || !FrostGiant.Has(p,FrostGiant.LootHeart) || p.Map==null || !p.Map.IsPlayerHome)return ThoughtState.Inactive;
   int s=FrostGiant.LootStage(p.Map);return s<=0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(s-1);
  }
 }
 // 记住殖民地自己做出来的东西（品质由殖民地生成），它们不算战利品。
 public class GiantSpoilsState : GameComponent {
  public HashSet<int> crafted=new HashSet<int>();
  public GiantSpoilsState(Game game){}
  public static GiantSpoilsState Get=>GameComp<GiantSpoilsState>.Get();
  public bool Crafted(Thing t)=>crafted.Contains(t.thingIDNumber);
  public override void ExposeData(){
   base.ExposeData();
   if(Scribe.mode==LoadSaveMode.Saving)crafted.RemoveWhere(id=>id<=0);
   Scribe_Collections.Look(ref crafted,"lpColonyCrafted",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit && crafted==null)crafted=new HashSet<int>();
  }
 }
 [HarmonyPatch(typeof(CompQuality),nameof(CompQuality.SetQuality))]
 public static class ColonyCraftedTracker {
  static void Postfix(CompQuality __instance,ArtGenerationContext? __1){
   var t=__instance.parent;if(__1!=ArtGenerationContext.Colony || t==null || !(t.def.IsWeapon || t.def.IsApparel))return;
   GiantSpoilsState.Get?.crafted.Add(t.thingIDNumber);
  }
 }
 // 剧本“冰原之子”：每只开局冬狼都已驯服、学会服从，并认一名霜巨人为主人。
 public class ScenPart_IceborneKin : ScenPart {
  public int wolves=2;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref wolves,"wolves",2);}
  public override string Summary(Scenario scen)=>"开局带"+wolves+"只已驯服的冬狼，每只认一名霜巨人为主人。";
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(map,wolves);}
  public static List<Pawn> Apply(Map map,int count){
   var list=new List<Pawn>();var kind=FrostGiantRaid.WolfKind;if(kind==null || map==null)return list;
   var giants=map.mapPawns.FreeColonistsSpawned.Where(FrostGiant.Active).ToList();
   for(int i=0;i<count;i++){
    var master=giants.Count>0?giants[i%giants.Count]:null;
    var wolf=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,Faction.OfPlayer,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true));
    var near=master?.Position??map.mapPawns.FreeColonistsSpawned.FirstOrDefault()?.Position??map.Center;
    GenSpawn.Spawn(wolf,CellFinder.RandomClosewalkCellNear(near,map,4),map);
    if(wolf.training!=null && master!=null){wolf.training.Train(TrainableDefOf.Obedience,master,true);if(wolf.playerSettings!=null)wolf.playerSettings.Master=master;}
    list.Add(wolf);
   }
   return list;
  }
 }
 // 红袍商队偶尔牵来一只冬狼幼崽（chance 概率，一只）。
 public class StockGenerator_WinterWolfPup : StockGenerator {
  public float chance=.25f;
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   var k=FrostGiantRaid.WolfKind;if(k==null || !Rand.Chance(chance))yield break;
   yield return PawnGenerator.GeneratePawn(new PawnGenerationRequest(k,null,PawnGenerationContext.NonPlayer,forTile,forceGenerateNewPawn:true,fixedBiologicalAge:.1f,fixedChronologicalAge:.1f));
  }
  public override bool HandlesThingDef(ThingDef d)=>d?.defName==FrostGiant.WinterWolf;
 }
}