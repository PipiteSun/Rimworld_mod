using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld.Planet;
using UnityEngine;

namespace LichPhylactery {
 // 1.30.0 巨人通用层与霜巨人（组名 Giants：Test.ps1 -Only Giants）。按 Peter 的要求只留关键检查：1.30.0 16 条，1.34.0 风暴巨人 +8，1.34.1 云巨人与迁徙修复 +8，1.40.1 剧本说明 +1。
 // 剧本、迁徙队伍、物品配方不写自动检查。
 public partial class LichChecks {
  static string giantSaveId;static int giantSaveCooldown=-1;static float giantSaveSize;
  static Pawn giantColdSubject;
  static bool GiantColdTemperature(Thing __instance,ref float __result){if(__instance!=giantColdSubject)return true;__result=-80f;return false;}
  Pawn GiantTestPawn(string xenotype,Faction faction){
   var generated=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:DefDatabase<XenotypeDef>.GetNamed(xenotype),mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25,fixedChronologicalAge:25));
   foreach(var injury in generated.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())generated.health.RemoveHediff(injury);
   foreach(var missing in generated.health.hediffSet.GetMissingPartsCommonAncestors().ToList())generated.health.RestorePart(missing.Part);
   return generated;
  }
  Pawn GiantSpawn(Pawn p){GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(altar.Position,map,5),map);return p;}

  void CheckGiants(){
   worldBefore=new HashSet<Pawn>(Find.WorldPawns.AllPawnsAliveOrDead);
   var made=new List<Thing>();
   var human=GiantSpawn(Person());var fire=GiantSpawn(GiantTestPawn("LP_FireGiant",Faction.OfPlayer));var frost=GiantSpawn(GiantTestPawn("LP_FrostGiant",Faction.OfPlayer));
   made.Add(human);made.Add(fire);made.Add(frost);
   try{
    GiantKin.ForgetCensus();
    // ---------- 通用层（4） ----------
    Check(GiantKin.Rank(fire)==3 && GiantKin.Rank(frost)==4 && Near(fire.BodySize,human.BodySize*2f) && Near(BodySize.HealthFactor(fire),1.5f) && Near(BodySize.MeleeFactor(fire),1.3f) && Near(RootScale(fire),RootScale(human)*2f) && Near(Satiety.Compute(fire),2f),"fire giant body size x2, health x1.5, melee x1.3, render x2 and stomach 200% are unchanged (giant lineage rank 3)");
    fire.apparel.DestroyAll();
    var duster=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_Duster"),ThingDefOf.Cloth);fire.apparel.Wear(duster,false);
    Check(ApparelUtility.HasPartsToWear(fire,duster.def) && fire.apparel.WornApparel.Contains(duster),"fire giant can wear a vanilla duster");
    fire.apparel.DestroyAll();
    var tunic=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FireGiantTunic"),ThingDefOf.Cloth);made.Add(tunic);
    bool humanCan=EquipmentUtility.CanEquip(tunic,human,out string why,false);
    Check(!ApparelUtility.HasPartsToWear(human,tunic.def) && !humanCan && (why??"").Contains("只有巨人能穿"),"ordinary pawn cannot wear the forge tunic ("+why+")");
    var helmet=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FireGiantHelmet"));var cloak=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(FrostGiant.Cloak));
    foreach(var a in new[]{helmet,tunic,cloak})fire.apparel.Wear(a,false);
    var attire=DefDatabase<ThoughtDef>.GetNamed("LP_GiantAttire");var attireState=attire.Worker.CurrentState(fire);
    Check(fire.apparel.WornApparel.Count(a=>GiantGear.Exclusive(a.def))==3 && GiantGear.AttireMood(fire)==6 && attireState.Active && Near(attire.stages[attireState.StageIndex].baseMoodEffect,6f),"three giant-only garments (helmet, tunic, wolf cloak) give +6 mood");

    // ---------- 阶序（2） ----------
    RacialRelations.Tracker.Census(map).Rebuild(map);
    var fireSees=DefDatabase<ThoughtDef>.GetNamed("LP_RacialRelation_FireFrost");var frostSees=DefDatabase<ThoughtDef>.GetNamed("LP_RacialRelation_FrostFire");
    var s1=fireSees.Worker.CurrentState(fire);var s2=frostSees.Worker.CurrentState(frost);
    Check(s1.Active && s2.Active && Near(fireSees.stages[s1.StageIndex].baseMoodEffect,-5f) && Near(frostSees.stages[s2.StageIndex].baseMoodEffect,-5f),"fire and frost giants living together: -5 feud for both");
    GiantKin.ForgetCensus();
    bool Feels(Pawn p,string t)=>DefDatabase<ThoughtDef>.GetNamed(t).Worker.CurrentState(p).Active;
    Check(Feels(fire,"LP_OrdningBelow") && !Feels(fire,"LP_OrdningAbove") && Feels(frost,"LP_OrdningAbove") && !Feels(frost,"LP_OrdningBelow") && Feels(fire,"LP_GiantTongue") && Feels(frost,"LP_GiantTongue"),"ordning: the fire giant commands giants (+2), the frost giant keeps its place (+3), both share the giant tongue");

    // ---------- 霜巨人（8） ----------
    Check(Near(frost.BodySize,human.BodySize*2f) && Near(Satiety.Compute(frost),2f) && Near(RootScale(frost),RootScale(human)*1.4f) && Near(BodySize.HealthFactor(frost),1.5f) && Near(BodySize.MeleeFactor(frost),1.3f),"frost giant: body size x2.0 (render x1.4), stomach 200%, giant-body health x1.5 and melee x1.3");
    var probe=new Harmony("local.lich.giantcold");var ambient=AccessTools.PropertyGetter(typeof(Thing),"AmbientTemperature");
    try{
     probe.Patch(ambient,prefix:new HarmonyMethod(typeof(LichChecks),nameof(GiantColdTemperature)));
     var giver=new HediffGiver_Hypothermia{hediff=HediffDefOf.Hypothermia};
     giantColdSubject=frost;for(int i=0;i<20;i++)giver.OnIntervalPassed(frost,null);bool frostCold=frost.health.hediffSet.HasHediff(HediffDefOf.Hypothermia);
     giantColdSubject=human;giver.OnIntervalPassed(human,null);bool humanCold=human.health.hediffSet.HasHediff(HediffDefOf.Hypothermia);
     var bite=frost.TakeDamage(new DamageInfo(DamageDefOf.Frostbite,10,999,hitPart:frost.RaceProps.body.corePart));
     Check(!frostCold && humanCold && bite.totalDamageDealt<=0f && Fiends.Chill(frost,1)==null && Items.Give(frost,"LP_FrostSlow",240)==null,"frost giant at -80 C: no hypothermia or frostbite; immune to ice devil chill and frost slow");
    }finally{giantColdSubject=null;probe.Unpatch(ambient,HarmonyPatchType.Prefix,probe.Id);}
    Check(Near(frost.genes.FactorForDamage(new DamageInfo(DamageDefOf.Flame,10)),1.5f) && Near(fire.genes.FactorForDamage(new DamageInfo(DamageDefOf.Flame,10)),0f),"frost giant takes x1.5 flame damage");
    var wolf=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(FrostGiant.WinterWolf),Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true));made.Add(wolf);
    Check(Near(Beasts.TameChance(.2f,frost,wolf),.6f) && Near(Beasts.TameChance(.5f,frost,wolf),1f) && Near(Beasts.TameChance(.2f,human,wolf),.2f),"wolf lord tames winter wolves at x3 (capped at 100%)");
    // 雪原行者：同一格积雪厚/无雪，霜巨人的移动消耗不变，普通人变大。
    var cost=AccessTools.Method(typeof(Pawn_PathFollower),"CostToMoveIntoCell",new[]{typeof(Pawn),typeof(IntVec3)});
    // 找一格积上厚雪后普通人确实变慢的地面（有的地格或格上的东西不让积雪，完整测试里地图状态也和单跑不同）。
    float Cost(Pawn p,IntVec3 c)=>(float)cost.Invoke(null,new object[]{p,c});
    IntVec3 snowCell=IntVec3.Invalid;float frostSnow=0,humanSnow=0,frostClear=0,humanClear=0;
    foreach(var c in GenRadial.RadialCellsAround(frost.Position,20,false)){
     if(!c.InBounds(map) || !c.Standable(map) || c.Roofed(map) || c.GetEdifice(map)!=null)continue;
     float old=map.snowGrid.GetDepth(c);
     map.snowGrid.SetDepth(c,1f);float fs=Cost(frost,c),hs=Cost(human,c);
     map.snowGrid.SetDepth(c,0f);float fc=Cost(frost,c),hc=Cost(human,c);
     map.snowGrid.SetDepth(c,old);
     if(hs>hc+1f){snowCell=c;frostSnow=fs;humanSnow=hs;frostClear=fc;humanClear=hc;break;}
    }
    Check(SnowWalking.Patched && snowCell.IsValid && Near(frostSnow,frostClear) && SnowWalking.AddOn(WeatherBuildupCategory.Thick)>0,"snow walker: deep snow does not slow a frost giant ("+frostClear+" vs "+frostSnow+"), still slows a human ("+humanClear+" vs "+humanSnow+") at "+snowCell);    // 冬之号角：没号角按钮灰掉；戴上后吹响，人得到冬之号角（恐惧免疫），冬狼得到狂猎，进入1天冷却。
    var ability=frost.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_WinterHornBlast"));
    bool lockedWithout=ability!=null && ability.GizmoDisabled(out string noHorn) && noHorn.Contains("霜巨人号角");
    var horn=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(FrostGiant.Horn));frost.apparel.Wear(horn,false);
    GenSpawn.Spawn(wolf,CellFinder.RandomClosewalkCellNear(frost.Position,map,3),map);
    bool fired=ability!=null && !ability.GizmoDisabled(out _) && ability.Activate(frost,LocalTargetInfo.Invalid);
    Check(lockedWithout && fired && Items.Has(human,"LP_WinterHorn") && Loxodon.FearImmune(human) && Items.Has(wolf,"LP_WildHunt") && ability.OnCooldown && ability.CooldownTicksRemaining>GenDate.TicksPerDay-10,"winter horn needs the frost giant horn; it rallies allies (fear immune) and sends winter wolves on the wild hunt; one-day cooldown");
    // 霜巨人斧只有巨人能用。
    var axe=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(FrostGiant.Axe),ThingDefOf.Steel);made.Add(axe);
    bool humanAxe=EquipmentUtility.CanEquip(axe,human,out string axeWhy,false);
    Check(!humanAxe && (axeWhy??"").Contains("只有巨人") && EquipmentUtility.CanEquip(axe,fire,out _,false) && EquipmentUtility.CanEquip(axe,frost,out _,false),"frost giant axe is for giants only ("+axeWhy+")");
    // 塞伊远征军：≥800点才有霜巨人掠夺者（随行冬狼、披冬狼皮大氅）。
    var thay=Factions.Hostile;Check(thay!=null,"Thayan expedition exists for the frost giant raid check");
    FrostGiantRaid.TestRoll=0f;
    try{
     var big=FactionGroup(thay,PawnGroupKindDefOf.Combat,1000f);var small=FactionGroup(thay,PawnGroupKindDefOf.Combat,700f);
     var raiders=big.Where(p=>p.kindDef==FrostGiantRaid.RaiderKind).ToList();
     Check(raiders.Count==1 && raiders.All(FrostGiant.Active) && raiders.All(p=>Items.Worn(p,FrostGiant.Cloak)!=null) && big.Count(FrostGiant.IsWinterWolf)>=1 && !small.Any(FrostGiant.Active),"Thay 1000-point raid brings a frost giant raider in a wolf cloak with winter wolves; 700 points brings none");
    }finally{FrostGiantRaid.TestRoll=null;CleanFactionPawns();}
   }finally{
    foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    GiantKin.ForgetCensus();RacialRelations.Tracker?.Census(map)?.Rebuild(map);
   }
   CheckStormGiants();
   CheckCloudGiants();
   CheckScenarioSummaries();
  }

  // 1.40.1：选择剧本页的说明面板。原版 GetFullInformationText 遇到任何 ScenPart 抛异常只显示“Cannot read data.”；这里逐个剧本、逐个组件调一遍，把出错组件的堆栈写进日志。
  void CheckScenarioSummaries(){
   var bad=new List<string>();int count=0;
   foreach(var def in DefDatabase<ScenarioDef>.AllDefs.Where(d=>OwnDef(d)).ToList()){
    count++;var scen=def.scenario;
    foreach(var part in scen.AllParts){
     try{part.Summary(scen);}
     catch(Exception e){bad.Add(def.defName+"/"+part.GetType().Name);Log.Message("[DND边缘大陆] 剧本说明出错："+def.defName+" / "+part.GetType().Name+" ("+part.def?.defName+")\n"+e);}
    }
    string text=null;
    try{text=scen.GetFullInformationText();}catch(Exception e){Log.Message("[DND边缘大陆] "+def.defName+" GetFullInformationText: "+e);}
    if(text==null || text.Contains("Cannot read data"))bad.Add(def.defName+"（全文）");
   }
   Check(count>=8 && bad.Count==0,"all "+count+" mod scenarios show their full description on the scenario select page"+(bad.Count>0?" — broken: "+string.Join(", ",bad):""));
  }

  // ==================== 1.34.1 云巨人与迁徙修复（8 条，含存读档 1 条） ====================
  static string cloudSaveId;static int cloudSaveCooldown=-1;static float cloudSaveSize;
  void CheckCloudGiants(){
   var made=new List<Thing>();var colony=Factions.Colony;int colonyGoodwill=colony?.PlayerGoodwill??0;var state=CloudGiantState.Get;
   var human=GiantSpawn(Person());var cloud=GiantSpawn(GiantTestPawn("LP_CloudGiant",Faction.OfPlayer));
   made.Add(human);made.Add(cloud);
   try{
    // 1 踏云：同一格换成土壤/沼泽/泥地，云巨人的移动消耗不变，普通人在沼泽变慢；陷阱不会被云巨人踩响。
    var cost=AccessTools.Method(typeof(Pawn_PathFollower),"CostToMoveIntoCell",new[]{typeof(Pawn),typeof(IntVec3)});
    float Cost(Pawn p,IntVec3 c)=>(float)cost.Invoke(null,new object[]{p,c});
    var marsh=DefDatabase<TerrainDef>.GetNamedSilentFail("Marsh");var mud=DefDatabase<TerrainDef>.GetNamedSilentFail("Mud");var soil=TerrainDefOf.Soil;
    var cell=GenRadial.RadialCellsAround(cloud.Position,12,false).First(c=>c.InBounds(map) && c.Standable(map) && c.GetEdifice(map)==null && c.GetFirstThing<Thing>(map)==null && !c.GetTerrain(map).IsWater && !c.Roofed(map));
    var oldTerrain=cell.GetTerrain(map);float cs,hs,cm,hm,cu;
    try{
     map.terrainGrid.SetTerrain(cell,soil);cs=Cost(cloud,cell);hs=Cost(human,cell);
     map.terrainGrid.SetTerrain(cell,marsh);cm=Cost(cloud,cell);hm=Cost(human,cell);
     map.terrainGrid.SetTerrain(cell,mud);cu=Cost(cloud,cell);
    }finally{map.terrainGrid.SetTerrain(cell,oldTerrain);}
    var trapDef=DefDatabase<ThingDef>.GetNamed("TrapSpike");var trap=(Building_Trap)GenSpawn.Spawn(ThingMaker.MakeThing(trapDef,ThingDefOf.WoodLog),CellFinder.RandomClosewalkCellNear(cloud.Position,map,4),map);trap.SetFaction(Factions.Hostile);made.Add(trap);
    var spring=AccessTools.Method(typeof(Building_Trap),"CheckSpring");float humanChance=(float)AccessTools.Method(typeof(Building_Trap),"SpringChance").Invoke(trap,new object[]{human});
    int hurt=cloud.health.hediffSet.hediffs.Count(h=>h is Hediff_Injury);for(int i=0;i<40;i++)spring.Invoke(trap,new object[]{cloud});
    Check(marsh!=null && mud!=null && Near(cm,cs) && Near(cu,cs) && hm>hs+1f && trap.Spawned && cloud.health.hediffSet.hediffs.Count(h=>h is Hediff_Injury)==hurt && humanChance>0f,"cloud walk: marsh and mud cost the same as soil for a cloud giant ("+cs+"/"+cm+"/"+cu+"), a human slows ("+hs+" to "+hm+"); traps never spring under it (human chance "+humanChance.ToStringPercent()+")");

    // 2 贵胄之心：没有卧室 / <“非常好” -4；“非常好”～“极好”之间不生效；≥“极好” +4。
    var room=DefDatabase<ThoughtDef>.GetNamed("LP_CloudNobleRoom");var roomState=room.Worker.CurrentState(cloud);
    float very=CloudGiant.VeryImpressive,extreme=CloudGiant.ExtremelyImpressive;
    Check(CloudGiant.NobleStage(null)==0 && CloudGiant.NobleStage(very-1)==0 && CloudGiant.NobleStage((very+extreme)/2)==-1 && CloudGiant.NobleStage(extreme)==1 && roomState.Active && Near(room.stages[roomState.StageIndex].baseMoodEffect,-4f) && Near(room.stages[1].baseMoodEffect,4f),"noble heart: no bedroom or below very impressive ("+very+") -4, in between nothing, extremely impressive ("+extreme+") +4");

    // 3 赠礼：好感 = 市值/100×1.5（最多 +25）；送给同盟访客后同盟好感照算，访客收下东西、心情 +5。
    Factions.SetRelation(colony,Faction.OfPlayer,0);
    var visitor=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_FC_Villager"),colony,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true));made.Add(visitor);
    GenSpawn.Spawn(visitor,CellFinder.RandomClosewalkCellNear(cloud.Position,map,2),map);
    var silver=ThingMaker.MakeThing(ThingDefOf.Silver);silver.stackCount=500;cloud.inventory.innerContainer.TryAdd(silver);
    float value=silver.MarketValue*silver.stackCount;int expect=CloudGiant.GiftGoodwill(value);
    bool can=CloudGiant.CanReceive(visitor);int gw=CloudGiant.Give(cloud,visitor,silver);
    bool happy=visitor.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_CloudGiftReceived"))!=null;
    Check(CloudGiant.GiftGoodwill(1000f)==15 && CloudGiant.GiftGoodwill(3000f)==25 && can && gw==expect && colony.PlayerGoodwill==expect && visitor.inventory.innerContainer.Contains(silver) && happy,"cloud gift: goodwill is value/100 x1.5 capped at 25; 500 silver to an alliance visitor gives +"+gw+" (alliance now "+colony.PlayerGoodwill+"), the visitor keeps it and is pleased");

    // 4 迷踪步：瞬移到 6 格内看得见的空位，冷却 30 秒；8 格外不行。
    var step=cloud.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_CloudMistyStep"));var comp=step.CompOfType<CompCloudAbility>();
    var near=GenRadial.RadialCellsAround(cloud.Position,5f,false).Where(c=>c.DistanceTo(cloud.Position)>=3f && comp.Invalid(c)==null).RandomElementWithFallback(IntVec3.Invalid);
    var far=GenRadial.RadialCellsAround(cloud.Position,9f,false).FirstOrDefault(c=>c.DistanceTo(cloud.Position)>=8f && c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null);
    string farWhy=comp.Invalid(far);bool stepped=near.IsValid && step.Activate(near,near);
    Check(near.IsValid && stepped && cloud.Position==near && step.CooldownTicksRemaining>1790 && farWhy!=null,"misty step: blinks "+(near.IsValid?near.DistanceTo(human.Position).ToString("0"):"?")+" cells to a visible empty spot, 30-second cooldown; 8+ cells is refused ("+farWhy+")");

    // 5 同盟：奢侈品商队由云巨人贵族领队（晨星锤、云纹长袍），战斗袭击里没有云巨人。
    try{
     var caravan=FactionGroup(colony,PawnGroupKindDefOf.Trader,1000f,DefDatabase<TraderKindDef>.GetNamed(CloudGiant.LuxuryCaravan));
     var raid=FactionGroup(colony,PawnGroupKindDefOf.Combat,3000f);
     var noble=caravan.FirstOrDefault(CloudGiant.Active);
     Check(noble!=null && noble.kindDef.defName==CloudGiant.NobleTraderKind && Items.Holds(noble,CloudGiant.Morningstar) && Items.Worn(noble,CloudGiant.Robe)!=null && raid.Count>0 && !raid.Any(CloudGiant.Active),"alliance luxury caravan is led by a cloud giant noble (morningstar, cloud robe); a 3000-point alliance raid has no cloud giant ("+raid.Count+" raiders)");
    }finally{CleanFactionPawns();}

    // 6 上位者的使节（承认阶序）：殖民地只有火巨人时会来；接受后同盟好感 +20，火巨人“俯首”，15 天后“名列阶序”。
    var fire=GiantSpawn(GiantTestPawn("LP_FireGiant",Faction.OfPlayer));made.Add(fire);
    cloud.DeSpawn();GiantKin.ForgetCensus();bool wanted=CloudGiant.EnvoyWanted(map);
    GenSpawn.Spawn(cloud,CellFinder.RandomClosewalkCellNear(fire.Position,map,4),map);GiantKin.ForgetCensus();bool notWithCloud=!CloudGiant.EnvoyWanted(map);
    Factions.SetRelation(colony,Faction.OfPlayer,0);int oldAccepted=state.ordningAccepted;bool oldAck=state.ordningAcknowledged;
    try{
     CloudGiant.AcceptOrdning(map);
     bool bowed=fire.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_OrdningBowed"))!=null;
     var listed=DefDatabase<ThoughtDef>.GetNamed("LP_OrdningListed");bool listedEarly=listed.Worker.CurrentState(fire).Active;
     state.ordningAccepted-=16*GenDate.TicksPerDay;bool listedLater=listed.Worker.CurrentState(fire).Active && !listed.Worker.CurrentState(cloud).Active;
     Check(wanted && notWithCloud && colony.PlayerGoodwill==CloudGiant.EnvoyGoodwill && bowed && !listedEarly && listedLater,"ordning envoy: comes when only fire/frost giants live here; accepting gives alliance +20, the fire giant bows (-3) and after 15 days is listed in the ordning (+2) [wanted "+wanted+", not with cloud "+notWithCloud+", goodwill "+colony.PlayerGoodwill+", bowed "+bowed+", early "+listedEarly+", later "+listedLater+"]");
    }finally{state.ordningAccepted=oldAccepted;state.ordningAcknowledged=oldAck;}

    // 7 迁徙修复：队伍挂在隐藏派系“巨人游群”（对所有派系中立，好感记到原派系）；被击倒后救援（成为玩家的客人）就不再归迁徙的 Lord 管。
    var g=GiantMigration.Start(map,"frost");
    try{
     var band=GiantMigration.Wanderers();var giant=g?.pawns.FirstOrDefault(GiantKin.Is);
     bool neutral=band!=null && Find.FactionManager.AllFactionsListForReading.Where(o=>o!=band && !o.IsPlayer).All(o=>!band.HostileTo(o) && !o.HostileTo(band)) && !band.HostileTo(Faction.OfPlayer);
     bool origin=g!=null && g.lord?.faction==band && g.Origin==colony && CloudGiant.GiftFaction(giant)==colony;
     if(giant!=null){HealthUtility.DamageUntilDowned(giant,false);giant.guest.SetGuestStatus(Faction.OfPlayer);}
     Check(neutral && origin && giant!=null && giant.Faction==band && !g.lord.ownedPawns.Contains(giant) && giant.GetLord()==null && !g.pawns.Contains(giant),"giant migration: the group belongs to the hidden giant wanderers (neutral to every faction, goodwill still goes to "+g?.Origin?.Name+"); a downed giant rescued by the player leaves the migration lord");
    }finally{
     if(g!=null){foreach(var t in g.campThings)if(t!=null && !t.Destroyed)t.Destroy();var all=g.pawns.Concat(map.mapPawns.AllPawnsSpawned.Where(p=>p.Faction==GiantMigration.Wanderers())).Distinct().ToList();if(g.lord!=null && map.lordManager.lords.Contains(g.lord))map.lordManager.RemoveLord(g.lord);foreach(var p in all)if(!p.Destroyed)p.Destroy();GiantMigration.State?.migrations.Remove(g);}
    }
   }finally{
    if(colony!=null)Factions.SetRelation(colony,Faction.OfPlayer,colonyGoodwill);
    foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    GiantKin.ForgetCensus();RacialRelations.Tracker?.Census(map)?.Rebuild(map);
   }
  }
  void PrepareCloudSave(){
   var p=GiantTestPawn("LP_CloudGiant",null);
   var ability=p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_CloudFog"));ability.StartCooldown(10000);
   cloudSaveId=p.ThingID;cloudSaveCooldown=ability.CooldownTicksRemaining;cloudSaveSize=p.BodySize;
   Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);
  }
  // 8 存读档：云巨人的体型、云雾术冷却不变。
  void CheckCloudAfterLoad(){
   var p=Find.WorldPawns.AllPawnsAliveOrDead.FirstOrDefault(x=>x.ThingID==cloudSaveId);
   var ability=p?.abilities?.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_CloudFog"));
   Check(p!=null && CloudGiant.Active(p) && GiantKin.Rank(p)==2 && Near(p.BodySize,cloudSaveSize) && Near(cloudSaveSize,2.2f) && ability!=null && ability.CooldownTicksRemaining==cloudSaveCooldown,"cloud giant body size ("+p?.BodySize+") and cloud fog cooldown ("+ability?.CooldownTicksRemaining+"/"+cloudSaveCooldown+") survive save/load");
   if(p!=null){Find.WorldPawns.RemovePawn(p);p.Destroy();}
  }

  // ==================== 1.34.0 风暴巨人（8 条，含存读档 1 条） ====================
  static string stormSaveId;static int stormSaveCooldown=-1;static float stormSaveSize;static GeneDef stormSaveSkin;
  void CheckStormGiants(){
   var made=new List<Thing>();var wm=map.weatherManager;var oldWeather=wm.curWeather;
   var human=GiantSpawn(Person());var storm=GiantSpawn(GiantTestPawn("LP_StormGiant",Faction.OfPlayer));
   made.Add(human);made.Add(storm);
   try{
    // 1 雷电之躯：EMP 全免，落雷的爆炸伤不到自己。
    var emp=storm.TakeDamage(new DamageInfo(DamageDefOf.EMP,30f,0f,-1f,null));
    int hurtBefore=storm.health.hediffSet.hediffs.Count(h=>h is Hediff_Injury);
    // 1.63.0：只看这次雷击产生的爆炸（完整测试里前面的检查偶尔还留着别的爆炸，“全是雷击”就不成立）。
    var oldBooms=new HashSet<Thing>(map.listerThings.ThingsOfDef(ThingDefOf.Explosion));
    Mesh bolt=null;WeatherEvent_LightningStrike.DoStrike(storm.Position,map,ref bolt);
    var tick=AccessTools.Method(typeof(Explosion),"Tick");var booms=map.listerThings.ThingsOfDef(ThingDefOf.Explosion).OfType<Explosion>().Where(b=>!oldBooms.Contains(b)).ToList();bool tagged=booms.Count>0 && booms.All(StormGiant.IsStrike);
    for(int i=0;i<90 && booms.Any(b=>!b.Destroyed);i++)foreach(var b in booms.Where(b=>!b.Destroyed).ToList())tick.Invoke(b,null);
    foreach(var blaze in map.listerThings.ThingsOfDef(ThingDefOf.Fire).Where(f=>f.Position.InHorDistOf(storm.Position,4)).ToList())blaze.Destroy();
    int hurtAfter=storm.health.hediffSet.hediffs.Count(h=>h is Hediff_Injury);
    Check(emp.totalDamageDealt<=0f && !storm.stances.stunner.Stunned && tagged && hurtAfter==hurtBefore,"storm body: EMP has no effect and a lightning strike on its own cell leaves it unhurt (emp "+emp.totalDamageDealt+", stunned "+storm.stances.stunner.Stunned+", tagged "+tagged+", strike explosions "+booms.Count+", injuries "+hurtBefore+" to "+hurtAfter+")");

    // 2 风暴之子：下雨时近战 +15%、移速 +10%、心情“风暴在歌唱”；晴天没有。忧郁先知：晴天身边有人时 -3，雨天没有。
    float Stat(StatDef s)=>s.Worker.GetValue(StatRequest.For(storm));
    var singing=DefDatabase<ThoughtDef>.GetNamed("LP_StormSinging");var gloom=DefDatabase<ThoughtDef>.GetNamed("LP_StormForeboding");
    human.Position=CellFinder.RandomClosewalkCellNear(storm.Position,map,2);human.Notify_Teleported(true,false);
    wm.curWeather=StormGiant.W("Clear");float meleeClear=Stat(StatDefOf.MeleeDamageFactor),moveClear=Stat(StatDefOf.MoveSpeed);bool singClear=singing.Worker.CurrentState(storm).Active,gloomClear=gloom.Worker.CurrentState(storm).Active;
    wm.curWeather=StormGiant.W("Rain");float meleeRain=Stat(StatDefOf.MeleeDamageFactor),moveRain=Stat(StatDefOf.MoveSpeed);bool singRain=singing.Worker.CurrentState(storm).Active,gloomRain=gloom.Worker.CurrentState(storm).Active;
    Check(Near(meleeRain/meleeClear,1.15f) && Near(moveRain/moveClear,1.1f) && singRain && !singClear && gloomClear && !gloomRain,"storm child: rain gives melee x1.15 ("+meleeClear+" to "+meleeRain+"), move x1.1 and the storm sings; clear weather gives none, and the melancholy (-3) lifts in the rain");
    wm.curWeather=oldWeather;

    // 3 雷霆一击：近战命中机械体后追加 EMP 15，机械体被眩晕；拿风暴三叉戟时 ×1.5。
    var mechKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther")??DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Lancer");
    var mech=PawnGenerator.GeneratePawn(new PawnGenerationRequest(mechKind,null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true));made.Add(mech);
    GenSpawn.Spawn(mech,CellFinder.RandomClosewalkCellNear(storm.Position,map,1),map);
    var verb=storm.meleeVerbs.TryGetMeleeVerb(mech) as Verb_MeleeAttackDamage;
    if(verb!=null)AccessTools.Method(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget").Invoke(verb,new object[]{new LocalTargetInfo(mech)});
    float bare=StormGiant.ThunderAmount(storm);
    var trident=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(StormGiant.Trident));storm.equipment.DestroyAllEquipment();storm.equipment.AddEquipment(trident);
    Check(verb!=null && mech.stances.stunner.Stunned && Near(bare,15f) && Near(StormGiant.ThunderAmount(storm),22.5f) && !EquipmentUtility.CanEquip(trident,human,out _,false),"thunder strike: a melee hit adds EMP 15 that stuns a mechanoid; x1.5 with the storm trident, which only giants can wield");

    // 4 召唤风暴：天气转雷雨并锁定（召来的天气状况，带落雷），冷却7天。
    var call=storm.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SummonStorm"));
    bool cast=call!=null && !call.GizmoDisabled(out _) && call.Activate(storm,LocalTargetInfo.Invalid);
    var cond=map.gameConditionManager.ActiveConditions.OfType<GameCondition_LPWeather>().FirstOrDefault();
    Check(cast && wm.curWeather?.defName=="RainyThunderstorm" && cond!=null && cond.strikes && cond.ForcedWeather()?.defName=="RainyThunderstorm" && cond.TicksLeft>GenDate.TicksPerDay-10 && call.CooldownTicksRemaining>7*GenDate.TicksPerDay-10,"summon storm: the map turns to a locked thunderstorm for a day (with lightning on enemies), 7-day cooldown");
    cond?.End();wm.curWeather=oldWeather;

    // 5 风暴预兆：家园有成年风暴巨人时，≥1000 点的塞伊袭击推迟 12 小时；800 点不推迟（竖琴手关掉以单独测）。
    var thay=Factions.Hostile;var events=Tribute.Events;bool harper=LichMod.Settings.harperWarning;int queued=events.harperQueue.Count;
    try{
     LichMod.Settings.harperWarning=false;int before=events.harperQueue.Count;int now=Find.TickManager.TicksGame;
     bool delayed=Harper.TryDelay((IncidentWorker_RaidEnemy)IncidentDefOf.RaidEnemy.Worker,new IncidentParms{target=map,faction=thay,points=1200f});
     var entry=events.harperQueue.Count>before?events.harperQueue.Last():null;
     bool small=Harper.TryDelay((IncidentWorker_RaidEnemy)IncidentDefOf.RaidEnemy.Worker,new IncidentParms{target=map,faction=thay,points=800f});
     Check(thay!=null && delayed && entry!=null && entry.fireTick-now==GenDate.TicksPerDay/2 && !small && events.harperQueue.Count==before+1,"storm omen: a 1200-point Thayan raid is delayed 12 hours ("+(entry==null?-1:entry.fireTick-now)+" ticks); an 800-point raid is not");
    }finally{LichMod.Settings.harperWarning=harper;if(events.harperQueue.Count>queued)events.harperQueue.RemoveRange(queued,events.harperQueue.Count-queued);}

    // 6 塞伊 4000 点袭击：一名戴镣铐的被奴役风暴巨人（能力灰掉），3500 点没有；归顺后镣铐自动除去。
    StormGiantRaid.TestRoll=0f;
    try{
     var big=FactionGroup(thay,PawnGroupKindDefOf.Combat,4000f);var small=FactionGroup(thay,PawnGroupKindDefOf.Combat,3500f);
     var slaves=big.Where(p=>p.kindDef==StormGiantRaid.Kind).ToList();var slave=slaves.FirstOrDefault();
     var boltAbility=slave?.abilities?.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_StormLightningBolt"));
     bool locked=boltAbility!=null && boltAbility.GizmoDisabled(out string why) && why.Contains("镣铐");
     var shackle=slave?.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed(StormGiant.Shackled));
     bool keptWhileHostile=shackle!=null && !shackle.ShouldRemove;
     slave?.SetFaction(Faction.OfPlayer);
     Check(slaves.Count==1 && StormGiant.Active(slave) && keptWhileHostile && locked && shackle.ShouldRemove && !small.Any(p=>p.kindDef==StormGiantRaid.Kind),"Thay 4000-point raid brings one shackled storm giant (abilities locked); 3500 points brings none; the shackles come off once it joins");
    }finally{StormGiantRaid.TestRoll=null;CleanFactionPawns();}

    // 7 阶序：风暴＋火＋霜同住——风暴巨人统领众巨（没有更高位的），火巨人上下都有，霜巨人只有更高位；风暴巨人体型 2.4、渲染 1.55、胃 240%。
    var fire=GiantSpawn(GiantTestPawn("LP_FireGiant",Faction.OfPlayer));var frost=GiantSpawn(GiantTestPawn("LP_FrostGiant",Faction.OfPlayer));made.Add(fire);made.Add(frost);
    GiantKin.ForgetCensus();
    bool Feels(Pawn p,string t)=>DefDatabase<ThoughtDef>.GetNamed(t).Worker.CurrentState(p).Active;
    Check(GiantKin.Rank(storm)==1 && Feels(storm,"LP_OrdningBelow") && !Feels(storm,"LP_OrdningAbove") && Feels(fire,"LP_OrdningAbove") && Feels(fire,"LP_OrdningBelow") && Feels(frost,"LP_OrdningAbove") && !Feels(frost,"LP_OrdningBelow") && Near(storm.BodySize,human.BodySize*2.4f) && Near(RootScale(storm),RootScale(human)*1.55f) && Near(Satiety.Compute(storm),2.4f),"ordning with storm, fire and frost giants: storm commands (+2), fire both ways, frost keeps its place (+3); storm giant body x2.4, render x1.55, stomach 240%");
   }finally{
    wm.curWeather=oldWeather;
    foreach(var c in map.gameConditionManager.ActiveConditions.OfType<GameCondition_LPWeather>().ToList())c.End();
    foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    GiantKin.ForgetCensus();RacialRelations.Tracker?.Census(map)?.Rebuild(map);
   }
  }
  void PrepareStormSave(){
   var p=GiantTestPawn("LP_StormGiant",null);
   var ability=p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_StormLightningBolt"));ability.StartCooldown(15000);
   stormSaveId=p.ThingID;stormSaveCooldown=ability.CooldownTicksRemaining;stormSaveSize=p.BodySize;
   stormSaveSkin=StormGiant.SkinGenes.Select(n=>DefDatabase<GeneDef>.GetNamed(n)).FirstOrDefault(g=>p.genes.GetGene(g)!=null);
   Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);
  }
  // 8 存读档：风暴巨人的体型、闪电束冷却、随机到的肤色基因不变。
  void CheckStormAfterLoad(){
   var p=Find.WorldPawns.AllPawnsAliveOrDead.FirstOrDefault(x=>x.ThingID==stormSaveId);
   var ability=p?.abilities?.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_StormLightningBolt"));
   Check(p!=null && StormGiant.Active(p) && GiantKin.Rank(p)==1 && Near(p.BodySize,stormSaveSize) && ability!=null && ability.CooldownTicksRemaining==stormSaveCooldown && stormSaveSkin!=null && p.genes.GetGene(stormSaveSkin)!=null,"storm giant body size ("+p?.BodySize+"), lightning bolt cooldown ("+ability?.CooldownTicksRemaining+"/"+stormSaveCooldown+") and skin gene ("+stormSaveSkin?.defName+") survive save/load");
   if(p!=null){Find.WorldPawns.RemovePawn(p);p.Destroy();}
  }

  // ---------- 存读档（1） ----------
  void PrepareGiantsSave(){
   var p=GiantTestPawn("LP_FrostGiant",null);
   var ability=p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_WinterHornBlast"));ability.StartCooldown(GenDate.TicksPerDay);
   giantSaveId=p.ThingID;giantSaveCooldown=ability.CooldownTicksRemaining;giantSaveSize=p.BodySize;
   Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);
   PrepareStormSave();PrepareCloudSave();
  }
  void CheckGiantsAfterLoad(){
   var p=Find.WorldPawns.AllPawnsAliveOrDead.FirstOrDefault(x=>x.ThingID==giantSaveId);
   var ability=p?.abilities?.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_WinterHornBlast"));
   Check(p!=null && FrostGiant.Active(p) && GiantKin.Rank(p)==4 && Near(p.BodySize,giantSaveSize) && ability!=null && ability.CooldownTicksRemaining==giantSaveCooldown,"frost giant body size ("+p?.BodySize+") and winter horn cooldown ("+ability?.CooldownTicksRemaining+"/"+giantSaveCooldown+") survive save/load");
   if(p!=null){Find.WorldPawns.RemovePawn(p);p.Destroy();}
   CheckStormAfterLoad();CheckCloudAfterLoad();
  }
 }
}
