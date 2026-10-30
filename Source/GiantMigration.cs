using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.28.0 巨人迁徙：一队饿着的巨人从地图一边进来，在中段扎营 1 天，找殖民地讨吃的，然后从对边离开；扎营结束时可能有一名巨人想留下。
 // 队伍（1.34.1 起挂在隐藏派系“巨人游群”下，好感记到原派系）：余烬氏族火巨人（48%）、（1.30.2 起归余烬氏族）食人魔一家（24%）、泰坦与追随者（8%）、（1.30.0）同盟的霜巨人战帮（20%，冬季×2）。
 public static class GiantMigration {
  public const int CampTicks=GenDate.TicksPerDay,CampMinColonyDistance=15,FedGoodwill=12,TakenKinGoodwill=-5;
  public const float NutritionPerDay=1.6f,DaysAsked=2f,TheftChance=.15f,StayChance=.35f,StayKinChance=.6f,StayFedBonus=.2f;
  public static float? TestRoll;
  public static float Roll=>TestRoll??Rand.Value;
  public static EventState State=>PortalEvent.State;
  static PawnKindDef K(string n)=>DefDatabase<PawnKindDef>.GetNamedSilentFail(n);

  // ---------- 队伍 ----------
  public static bool Usable(Faction f)=>f!=null && !f.defeated && Factions.Enabled(f) && !f.HostileTo(Faction.OfPlayer);
  public static bool IsEmberKind(string kind)=>kind=="ember" || kind=="ogre";
  public static Faction FactionFor(string kind)=>IsEmberKind(kind)?Factions.Ember:Factions.Colony;
  // 1.44.0：双头巨人（ettin）、独眼巨人牧群（cyclops）只在启用 HAR 时出现，各占 8%，其余队伍按原来的比例压缩。
  public static readonly string[] Kinds={"ember","ogre","titan","frost","storm","cloud","ettin","cyclops"};
  public static bool HARKind(string kind)=>kind=="ettin" || kind=="cyclops";
  public static bool Available(string kind)=>Usable(FactionFor(kind)) && (!IsEmberKind(kind) || LichMod.Settings.factionEmber) && (!HARKind(kind) || K(kind=="ettin"?"LP_FC_EttinWanderer":"LP_FC_CyclopsShepherd")!=null);
  public static bool CanFire(Map map)=>LichMod.Settings.giantMigration && LichMod.Settings.factionEmber && map!=null && map.IsPlayerHome && Kinds.Any(Available);
  public const float FrostWeight=.2f,FrostWinterFactor=2f,StormWeight=.05f,ProphetStayChance=.25f,CloudWeight=.1f,CloudBedroomBonus=.2f,HARShare=.08f;
  public static bool Winter(Map map){if(map==null)return false;var s=GenLocalDate.Season(map);return s==Season.Winter || s==Season.PermanentWinter;}
  public static float WeightOf(string kind,Map map){
   switch(kind){case "ember":return .48f;case "ogre":return .24f;case "titan":return .08f;case "frost":return FrostWeight*(Winter(map)?FrostWinterFactor:1f);case "storm":return StormWeight;case "cloud":return CloudWeight;}
   if(!HARKind(kind))return 0f;
   // 新队伍各占 8%：权重 = 8% ÷（1 − 两种新队伍的份额）× 其余可用队伍的权重和。
   float others=Kinds.Where(k=>!HARKind(k) && Available(k)).Sum(k=>WeightOf(k,map));
   return HARShare/(1f-HARShare*Kinds.Count(k=>HARKind(k) && Available(k)))*others;
  }
  public static string PickKind(Map map=null){
   var options=Kinds.Where(Available).Select(k=>(k,WeightOf(k,map))).ToList();
   if(options.Count==0)return null;
   float total=options.Sum(o=>o.Item2),r=Rand.Value*total;
   foreach(var o in options){if(r<o.Item2)return o.Item1;r-=o.Item2;}
   return options.Last().Item1;
  }
  public static List<Pawn> MakeGroup(string kind,Faction f,int tile){
   var list=new List<Pawn>();
   void Add(string name,int n){var k=K(name);if(k==null)return;for(int i=0;i<n;i++){var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(k,f,PawnGenerationContext.NonPlayer,tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false));list.Add(p);}}
   switch(kind){
    case "ember":Add("LP_EC_FireGiant",Rand.RangeInclusive(1,2));for(int i=Rand.RangeInclusive(2,3);i>0;i--)Add(Rand.Bool?"LP_EC_HumanSlave":"LP_EC_OrcSlave",1);foreach(var p in list)FactionGroups.EquipGiant(p);break;
    case "ogre":Add("LP_FC_OgreWanderer",Rand.RangeInclusive(2,3));break;
    case "titan":Add("LP_FC_TitanWanderer",1);Add("LP_FC_Villager",2);break;
    // 1.30.0 霜巨人战帮：霜巨人1～2、冬狼2～3（同一派系的动物）。
    case "frost":Add("LP_FC_FrostGiantWanderer",Rand.RangeInclusive(1,2));Add(FrostGiant.WinterWolf,Rand.RangeInclusive(2,3));break;
    // 1.34.0 风暴巨人先知：风暴巨人1、人类水手2。
    case "storm":Add("LP_FC_StormGiantProphet",1);Add("LP_FC_Sailor",2);break;
    // 1.34.1 云巨人使节团：云巨人贵族1、仆从2～3。
    case "cloud":Add(CloudGiant.NobleKind,1);Add("LP_FC_Servant",Rand.RangeInclusive(2,3));break;
    // 1.44.0 双头巨人 1（两张嘴，讨食营养 ×2）；独眼巨人牧群：独眼巨人 1、绵羊 3～5（同一派系的动物）。
    case "ettin":Add("LP_FC_EttinWanderer",1);break;
    case "cyclops":Add("LP_FC_CyclopsShepherd",1);Add("Sheep",Rand.RangeInclusive(3,5));break;
   }
   return list;
  }
  // 巨人：巨人血统（火巨人、霜巨人……）、食人魔、泰坦（奴兵、追随者、冬狼不算）。
  public static bool IsGiant(Pawn p)=>p!=null && (GiantKin.Is(p) || Races124.IsOgre(p) || Races124.IsTitan(p));
  public static float Appetite(Pawn p)=>Ettin.Is(p)?Ettin.Appetite:GiantKin.Is(p)?2f:Races124.IsTitan(p)?2.5f:Races124.IsOgre(p)?1.6f:1f;
  // 讨要的营养值：每人每天 1.6 × 体型食量倍率，要两天的量（只算人）；冬狼另要肉，每只 1.0。
  public const float WolfMeat=1f;
  public static float Need(IEnumerable<Pawn> pawns)=>pawns.Where(p=>p!=null && !p.Dead && p.RaceProps.Humanlike).Sum(p=>NutritionPerDay*Appetite(p))*DaysAsked;
  public static float MeatNeed(IEnumerable<Pawn> pawns)=>pawns.Count(p=>p!=null && !p.Dead && FrostGiant.IsWinterWolf(p))*WolfMeat;

  // ---------- 开始 ----------
  public static GiantGroup Start(Map map,string kind=null){
   kind=kind??PickKind(map);if(kind==null || !Available(kind))return null;
   var f=FactionFor(kind);
   if(!CellFinder.TryFindRandomEdgeCellWith(c=>c.Standable(map) && !c.Fogged(map) && map.reachability.CanReachColony(c),map,CellFinder.EdgeRoadChance_Neutral,out var entry))return null;
   if(!RCellFinder.TryFindRandomCellOutsideColonyNearTheCenterOfTheMap(entry,map,CampMinColonyDistance,out var camp))camp=CellFinder.RandomClosewalkCellNear(map.Center,map,10);
   var mirror=new IntVec3(map.Size.x-1-entry.x,0,map.Size.z-1-entry.z);
   if(!CellFinder.TryFindRandomEdgeCellNearWith(mirror,40,map,c=>c.Standable(map) && map.reachability.CanReach(camp,c,PathEndMode.OnCell,TraverseMode.PassDoors,Danger.Deadly),out var exit))exit=entry;
   var pawns=MakeGroup(kind,f,map.Tile);if(pawns.Count==0)return null;
   // 1.34.1：队伍挂在隐藏派系“巨人游群”下（对所有派系中立，被玩家攻击才转敌对）；好感、礼物、留下仍记到原来的派系。
   var band=Wanderers();if(band!=null)foreach(var p in pawns)p.SetFaction(band);
   foreach(var p in pawns)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,5),map);
   var lord=LordMaker.MakeNewLord(band??f,new LordJob_GiantMigration(camp,exit),map,pawns);
   // 1.34.0：风暴巨人先知走到哪里，雨就下到哪里。
   if(kind=="storm"){var rain=StormGiant.W("Rain");if(rain!=null && map.weatherManager.curWeather!=rain && !StormGiant.Rainy(map))map.weatherManager.TransitionTo(rain);}
   var g=new GiantGroup{lord=lord,map=map,camp=camp,kind=kind,pawns=pawns.ToList(),origin=f};
   State?.migrations.Add(g);
   return g;
  }
  public static GiantGroup GroupOf(Lord lord)=>State?.migrations.FirstOrDefault(g=>g.lord==lord);
  // ---------- 1.34.1 隐藏派系“巨人游群” ----------
  static FactionDef wanderersDef;
  public static FactionDef WanderersDef=>wanderersDef??(wanderersDef=DefDatabase<FactionDef>.GetNamedSilentFail("LP_GiantWanderers"));
  public static Faction Wanderers(){
   if(WanderersDef==null)return null;bool had=Factions.Of(WanderersDef)!=null;
   var f=Factions.EnsureHidden(WanderersDef);if(f==null)return null;
   if(!had)Factions.SetRelation(f,Faction.OfPlayer,0);
   f.factionHostileOnHarmByPlayer=true;KeepNeutral(f);
   return f;
  }
  // 对所有非玩家派系永久中立（原版会按派系定义给初始关系，永久敌对的派系，比如 VRE 猪人，会打路过的巨人）。
  public static void KeepNeutral(Faction band){
   if(band==null || Find.FactionManager==null)return;
   foreach(var o in Find.FactionManager.AllFactionsListForReading){
    if(o==band || o.IsPlayer)continue;
    if(band.RelationWith(o,true)==null)band.TryMakeInitialRelationsWith(o);
    if(band.RelationKindWith(o)!=FactionRelationKind.Neutral || o.RelationKindWith(band)!=FactionRelationKind.Neutral)Factions.SetRelation(band,o,0);
   }
  }
  // ---------- 1.34.1 被救的迁徙巨人：移出队伍的 Lord，交给原版“被救的陌生人”流程（治好后离开，或者请求加入） ----------
  // 原版救援（JobDriver_TakeToBed / CarryDownedPawn 的 CheckMakeTakeeGuest）只把他设为玩家的客人，不会把他移出原来的 Lord；
  // Lord 的职责在思考树里排在“客人：养伤→离开地图”之前，所以他醒来后一直被迁徙的 Lord 管着，躺在床上不动。
  public static bool OnRescued(Pawn p){
   var g=GroupOf(p);if(g==null)return false;
   g.pawns.Remove(p);if(g.stayer==p)g.stayer=null;
   var lord=p.GetLord();if(lord!=null && lord==g.lord)lord.Notify_PawnLost(p,PawnLostCondition.ForcedByPlayerAction);
   return true;
  }
  public static GiantGroup GroupOf(Pawn p)=>p==null?null:State?.migrations.FirstOrDefault(g=>g.pawns.Contains(p));
  public static string KindLabel(string kind)=>kind=="ember"?"余烬氏族的火巨人":kind=="ogre"?"一家食人魔":kind=="frost"?"一支霜巨人战帮":kind=="storm"?"一位风暴巨人先知和两名水手":kind=="cloud"?"一支云巨人使节团":kind=="ettin"?"一个流浪的双头巨人":kind=="cyclops"?"一位赶着羊群的独眼巨人":"一位泰坦和追随者";

  // 扎营：篝火 + 每人一个睡袋，然后发讨食信。
  public static void OnCamp(GiantGroup g){
   if(g==null || g.camped)return;g.camped=true;g.campTick=Find.TickManager.TicksGame;
   var map=g.map;
   // 篝火放在营地点附近一块空地上（营地点本身可能有草丛以外的东西）；营地就以篝火为中心。
   bool Clear(IntVec3 x)=>x.InBounds(map) && x.Standable(map) && x.GetEdifice(map)==null && x.GetFirstItem(map)==null && x.GetFirstPawn(map)==null && !x.GetTerrain(map).IsWater;
   if(Clear(g.camp) || CellFinder.TryFindRandomCellNear(g.camp,map,6,Clear,out g.camp)){var fire=ThingMaker.MakeThing(ThingDefOf.Campfire);fire.SetFaction(g.lord?.faction);GenSpawn.Spawn(fire,g.camp,map);g.campThings.Add(fire);fire.TryGetComp<CompRefuelable>()?.Refuel(30f);}
   int beds=Math.Min(5,g.Alive.Count());
   for(int i=0;i<beds;i++){
    // 1.63.0：睡袋占 1×2 格；原来只查了一格，转向后另一格可能压在篝火 / 别的睡袋上，生成时把它们抹掉（Events 扎营检查时好时坏的根因）。整块都要空。
    var rot=Rot4.Random;
    if(!CellFinder.TryFindRandomCellNear(g.camp,map,4,c=>c!=g.camp && GenAdj.OccupiedRect(c,rot,ThingDefOf.Bedroll.size).Cells.All(x=>Clear(x) && x!=g.camp),out var c2))break;
    var bed=ThingMaker.MakeThing(ThingDefOf.Bedroll,ThingDefOf.Cloth);bed.SetFaction(g.lord?.faction);GenSpawn.Spawn(bed,c2,map,rot);g.campThings.Add(bed);
   }
   // 1.34.0：风暴巨人先知不讨食，发“先知的预言”。
   if(g.kind=="storm"){g.answered=true;SendProphetLetter(g);return;}
   g.need=Need(g.Alive);g.meatNeed=MeatNeed(g.Alive);
   SendFoodLetter(g);
  }
  public static Letter_GiantMigration SendFoodLetter(GiantGroup g){
   var l=(Letter_GiantMigration)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_GiantChoiceLetter"));
   l.Label="饥饿的巨人";l.kind="food";l.group=g;l.lookTargets=new LookTargets(g.Alive.Cast<Thing>());l.relatedFaction=g.Origin;
   l.Text=KindLabel(g.kind)+"在殖民地外扎了营，一共"+g.Alive.Count()+"人。他们饿坏了，派人来讨吃的：想要能吃两天的食物，营养值合计 "+g.need.ToString("0.#")+"（巨人的胃口大：火巨人、霜巨人等巨人×2，双头巨人两张嘴×4，泰坦×2.5，食人魔×1.6）"+(g.meatNeed>0?"，另外冬狼要生肉，营养值 "+g.meatNeed.ToString("0.#"):"")+"。"+(FineOnly(g)?"云巨人贵族很挑剔：只收精致饭或奢华饭（营养值照算）。":"")+"\n\n给：从储存区里扣除食物（"+(FineOnly(g)?"只扣精致饭、奢华饭":"优先干肉饼、简餐、生肉")+"），"+(g.Origin?.Name??"他们的派系")+"好感 +"+FedGoodwill+"，离开时会在营地留下礼物。\n不给：不会翻脸，但饿肚子的巨人晚上可能派人来偷粮。\n\n他们会在这里待一天，然后从地图另一边离开。";
   Find.LetterStack.ReceiveLetter(l);return l;
  }
  // 殖民地储存区里的食物（不含尸体、药物、被禁止的），按优先顺序排好。
  public static int FoodRank(Thing t)=>t.def==ThingDefOf.Pemmican?0:t.def==ThingDefOf.MealSimple?1:t.def.IsMeat?2:3;
  // 1.34.1 云巨人使节团只收精致饭、奢华饭（含素食、肉食版本）。
  public static bool FineOnly(GiantGroup g)=>g?.kind=="cloud";
  public static bool FineMeal(ThingDef d)=>d!=null && (d.defName.StartsWith("MealFine") || d.defName.StartsWith("MealLavish"));
  public static List<Thing> StoredFood(Map map,bool fine)=>StoredFood(map).Where(t=>!fine || FineMeal(t.def)).ToList();
  public static float StoredNutrition(Map map,bool fine)=>StoredFood(map,fine).Sum(t=>t.GetStatValue(StatDefOf.Nutrition)*t.stackCount);
  public static List<Thing> StoredFood(Map map)=>map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).Where(t=>!(t is Corpse) && t.def.IsNutritionGivingIngestible && t.def.ingestible.HumanEdible && !t.def.IsDrug && t.IsInAnyStorage() && !t.IsForbidden(Faction.OfPlayer) && t.Spawned).OrderBy(FoodRank).ThenByDescending(t=>t.stackCount).ToList();
  public static float StoredNutrition(Map map)=>StoredFood(map).Sum(t=>t.GetStatValue(StatDefOf.Nutrition)*t.stackCount);
  public static float StoredMeat(Map map)=>StoredFood(map).Where(t=>t.def.IsMeat).Sum(t=>t.GetStatValue(StatDefOf.Nutrition)*t.stackCount);
  // 扣除食物；返回实际扣掉的营养值（不够时不扣，返回 0）。meatOnly：只扣生肉（冬狼的份）。
  public static float TakeFood(Map map,float need,bool meatOnly=false,bool fine=false){
   var food=StoredFood(map,fine);if(meatOnly)food=food.Where(t=>t.def.IsMeat).ToList();if(food.Sum(t=>t.GetStatValue(StatDefOf.Nutrition)*t.stackCount)+.001f<need)return 0f;
   float left=need,taken=0;
   foreach(var t in food){
    if(left<=.001f)break;float per=t.GetStatValue(StatDefOf.Nutrition);if(per<=0)continue;
    int n=Math.Min(t.stackCount,Mathf.CeilToInt(left/per-.0001f));if(n<=0)continue;
    var part=t.SplitOff(n);taken+=n*per;left-=n*per;part.Destroy();
   }
   return taken;
  }
  public static bool Feed(GiantGroup g){
   if(g==null || g.fed || g.answered)return false;
   // 先扣冬狼的生肉，再扣人的份；任何一份不够都不扣。
   if(g.meatNeed>0 && (StoredMeat(g.map)+.001f<g.meatNeed || StoredNutrition(g.map)+.001f<g.need+g.meatNeed))return false;
   if(g.meatNeed>0 && TakeFood(g.map,g.meatNeed,true)<=0)return false;
   if(TakeFood(g.map,g.need,false,FineOnly(g))<=0)return false;
   g.fed=true;g.answered=true;
   Factions.ChangeGoodwillExact(g.Origin,FedGoodwill,"给饥饿的巨人送了食物");
   foreach(var p in g.Alive)p.needs?.food?.SetInitialLevel();
   Messages.Message("巨人们收下了食物，大口吃了起来（好感 +"+FedGoodwill+"）。",new LookTargets(g.Alive.Cast<Thing>()),MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  public static void Refuse(GiantGroup g){
   if(g==null || g.answered)return;g.answered=true;
   if(Roll<TheftChance){
    // 当晚 23 点（已经过了就在明晚）派人去偷。
    int now=Find.TickManager.TicksGame;int hour=GenLocalDate.HourOfDay(g.map);
    int wait=((23-hour+24)%24)*GenDate.TicksPerHour;if(wait==0)wait=GenDate.TicksPerHour/2;
    g.theftTick=now+wait;
   }
  }
  // 偷粮：1～2 名队员各搬走一堆食物回营地。玩家可以拦（打断或者打倒他们）。
  public static int Steal(GiantGroup g){
   g.theftTick=-1;var map=g.map;int n=0;
   var food=StoredFood(map);
   foreach(var p in g.Alive.Where(p=>p.Spawned && !p.Downed && !p.InMentalState).InRandomOrder().Take(Rand.RangeInclusive(1,2)).ToList()){
    var target=food.FirstOrDefault(t=>!t.Destroyed && t.Spawned && p.CanReserveAndReach(t,PathEndMode.ClosestTouch,Danger.Some));if(target==null)break;
    food.Remove(target);
    // 搬到篝火旁边的空地（篝火那一格放不下东西）。
    if(!CellFinder.TryFindRandomCellNear(g.camp,map,3,x=>x!=g.camp && x.Standable(map) && x.GetEdifice(map)==null && x.GetFirstItem(map)==null && p.CanReach(x,PathEndMode.OnCell,Danger.Some),out var drop))drop=g.camp;
    var job=JobMaker.MakeJob(JobDefOf.HaulToCell,target,drop);job.count=target.stackCount;job.haulMode=HaulMode.ToCellNonStorage;job.ignoreForbidden=true;
    p.jobs.StartJob(job,JobCondition.InterruptForced);g.stolen.Add(target);n++;
   }
   if(n>0)Messages.Message("饥饿的巨人趁夜派了"+n+"人来偷粮！",new LookTargets(g.Alive.Cast<Thing>()),MessageTypeDefOf.NegativeEvent,false);
   return n;
  }

  // 拔营：收走篝火和睡袋、偷来的食物，给过食物就留下礼物；再看有没有人想留下。
  public static void OnLeave(GiantGroup g){
   if(g==null || g.left)return;g.left=true;var map=g.map;
   foreach(var t in g.campThings)if(t!=null && !t.Destroyed)t.Destroy();g.campThings.Clear();
   foreach(var t in g.stolen)if(t!=null && !t.Destroyed && t.Spawned && !t.IsInAnyStorage() && t.Position.InHorDistOf(g.camp,6))t.Destroy();
   if(g.fed){var gifts=Gifts(g.kind);foreach(var t in gifts){if(t is Pawn pup)GenSpawn.Spawn(pup,CellFinder.RandomClosewalkCellNear(g.camp,map,3),map);else GenPlace.TryPlaceThing(t,g.camp,map,ThingPlaceMode.Near);}if(gifts.Count>0)Messages.Message("巨人们拔营离开，在营地留下了谢礼："+string.Join("、",gifts.Select(t=>t.LabelCap))+"。",new LookTargets(g.camp,map),MessageTypeDefOf.PositiveEvent,false);}
   if(!g.stayAsked && g.kind!="storm")TryAskStay(g);
  }
  // ---------- 1.34.0 风暴巨人先知 ----------
  public static Pawn Prophet(GiantGroup g)=>g?.Alive.FirstOrDefault(StormGiant.Active);
  public static Letter_GiantMigration SendProphetLetter(GiantGroup g){
   var seer=Prophet(g);if(seer==null)return null;
   var l=(Letter_GiantMigration)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_GiantChoiceLetter"));
   l.Label="先知的预言";l.kind="prophet";l.group=g;l.lookTargets=new LookTargets(seer);l.relatedFaction=g.Origin;
   l.Text=seer.LabelShort+"是一位风暴巨人先知，跟着他的两名水手说，他从海渊一路走来，是为了把看见的东西告诉该听的人。他不讨吃的，只想在殖民地外歇一天，和你们说几句话。\n\n听他说完：先知讲出他在风暴里看到的预兆。之后30天内，第一次点数≥1000的敌对袭击会提前1天预警（与竖琴手、风暴预兆叠加）。\n请他留下：如果殖民地已经有别的巨人、他在阶序里也能有个好位置（阶序心情开启），他有25%的可能留下；否则他会礼貌地拒绝，照常离开。";
   Find.LetterStack.ReceiveLetter(l);return l;
  }
  public static void ListenToProphet(GiantGroup g){
   if(g==null || g.stayAsked)return;g.stayAsked=true;
   StormGiantState.Get?.Listen();
   Messages.Message("先知说完了他的预言：接下来30天里，第一场大型袭击到来之前，你们会提前一天得到警告。",new LookTargets(g.Alive.Cast<Thing>()),MessageTypeDefOf.PositiveEvent,false);
  }
  // 殖民地已有别的巨人、阶序心情开启（风暴巨人地位最高，阶序心情一定是正的）时 25% 加入。
  public static bool ProphetWelcome(Map map)=>Ordning.On && GiantKin.ColonyGiants(map).Count>0;
  public static bool AskProphetStay(GiantGroup g){
   if(g==null || g.stayAsked)return false;g.stayAsked=true;
   var seer=Prophet(g);if(seer==null || !seer.Spawned)return false;
   if(ProphetWelcome(g.map) && Roll<ProphetStayChance){g.stayer=seer;return Join(g);}
   Messages.Message(seer.LabelShort+"谢过你们的好意，"+(ProphetWelcome(g.map)?"但他的路还没有走完。":"可这里没有能和他说巨人语言、在阶序里安放他的人。")+"他会照常离开。",seer,MessageTypeDefOf.NeutralEvent,false);
   return false;
  }
  public static List<Thing> Gifts(string kind){
   var list=new List<Thing>();
   Thing Make(string name,int n=1,ThingDef stuff=null){var d=Items.Thing(name);if(d==null)return null;var t=ThingMaker.MakeThing(d,d.MadeFromStuff?(stuff??GenStuff.DefaultStuffFor(d)):null);t.stackCount=n;t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Outsider);return t;}
   switch(kind){
    case "ember":list.Add(Rand.Bool?Make("LP_LavaCrystal",3):Make(new[]{"LP_FireGiantMaul","LP_FireGiantArmor","LP_FireGiantHelmet"}.RandomElement()));break;
    case "ogre":list.Add(Make("LP_OgreClub",1,ThingDefOf.WoodLog));break;
    case "titan":list.Add(Make(new[]{"LP_ScrollTeleport","LP_ScrollRevivify","LP_ScrollProtectionEvil","LP_ScrollFireball","LP_ScrollTrueSeeing","LP_ScrollFleshToStone"}.RandomElement()));list.Add(Make("LP_MiracleShard"));break;
    // 1.30.0：已驯服的冬狼幼崽（70%）或霜巨人斧（30%）。
    case "frost":list.Add(Rand.Chance(.7f)?WolfPup():Make(FrostGiant.Axe,1,ThingDefOf.Steel));break;
    // 1.34.1：云纹长袍（60%）或天空琥珀（40%）。
    case "cloud":list.Add(Rand.Chance(.6f)?Make(CloudGiant.Robe):Make(CloudGiant.Amber));break;
    // 1.44.0：双头巨人送一顶双盔；独眼巨人送 2 只绵羊（已归殖民地）或一把独眼巨锤（钢）。
    case "ettin":list.Add(Make(Ettin.Helm));break;
    case "cyclops":if(Rand.Bool && K("Sheep")!=null){for(int i=0;i<2;i++)list.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(K("Sheep"),Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true)));}else list.Add(Make(Cyclops.Maul,1,ThingDefOf.Steel));break;
   }
   list.RemoveAll(t=>t==null);return list;
  }
  public static Pawn WolfPup(){
   var k=DefDatabase<PawnKindDef>.GetNamedSilentFail(FrostGiant.WinterWolf);if(k==null)return null;
   return PawnGenerator.GeneratePawn(new PawnGenerationRequest(k,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,fixedBiologicalAge:.1f,fixedChronologicalAge:.1f));
  }
  // 1.30.0：同族按巨人血统（火巨人、霜巨人……各算各的）。
  public static bool KinInColony(Pawn p)=>ColonyCensus.People().Any(c=>c.Faction==Faction.OfPlayer && ((GiantKin.Is(p) && GiantKin.Of(c)?.element==GiantKin.Of(p).element) || (Races124.IsOgre(p) && Races124.IsOgre(c)) || (Races124.IsTitan(p) && Races124.IsTitan(c))));
  // 1.34.1：云巨人使节团看殖民地卧室的平均印象度，≥“非常好”时留下概率再 +20%。
  public static float StayChanceFor(GiantGroup g,Pawn p)=>(KinInColony(p)?StayKinChance:StayChance)+(g.fed?StayFedBonus:0f)+(FineOnly(g) && CloudGiant.ColonyBedrooms(g.map)>=CloudGiant.VeryImpressive?CloudBedroomBonus:0f);
  public static Letter_GiantMigration TryAskStay(GiantGroup g){
   g.stayAsked=true;
   var p=g.Alive.Where(x=>IsGiant(x) && x.Spawned && !x.Downed).RandomElementWithFallback();if(p==null)return null;
   if(Roll>=StayChanceFor(g,p))return null;
   g.stayer=p;
   var l=(Letter_GiantMigration)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_GiantChoiceLetter"));
   l.Label="想留下的巨人";l.kind="stay";l.group=g;l.lookTargets=new LookTargets(p);l.relatedFaction=g.Origin;
   l.Text=p.LabelShort+"在拔营时留了下来，说"+(g.fed?"吃了你们的东西，":"")+"想在这里住下，不想再跟着队伍走了。\n\n接受："+p.LabelShort+"加入殖民地"+(g.Origin?.def==Factions.EmberDef?"（余烬氏族不高兴族人被带走：好感 "+TakenKinGoodwill+"）":"")+"，其余人继续上路。\n拒绝：全队照常离开，没有后果。";
   Find.LetterStack.ReceiveLetter(l);return l;
  }
  public static bool Join(GiantGroup g){
   var p=g?.stayer;if(p==null || p.Dead || p.Destroyed || !p.Spawned || p.Faction==Faction.OfPlayer){Messages.Message("那位巨人已经走远了。",MessageTypeDefOf.RejectInput,false);return false;}
   var old=g.Origin;
   p.SetFaction(Faction.OfPlayer);g.pawns.Remove(p);
   if(old!=null && old.def==Factions.EmberDef)Factions.ChangeGoodwillExact(old,TakenKinGoodwill,"带走了族人");
   Find.LetterStack.ReceiveLetter("巨人加入",p.LabelShort+"放下行囊，成了殖民地的一员。",LetterDefOf.PositiveEvent,p);
   return true;
  }
  public static void Tick(EventState s){
   int now=Find.TickManager.TicksGame;
   var band=Factions.Of(WanderersDef);
   if(band!=null){
    KeepNeutral(band);
    // 队伍都走了：被玩家打成敌对的关系复原为中立（同 1.31.0 龙过境的隐藏派系）。
    if(s.migrations.Count==0 && band.HostileTo(Faction.OfPlayer))Factions.SetRelation(band,Faction.OfPlayer,0);
   }
   // 旧存档里已经被救进床的迁徙巨人：同样移出 Lord。
   foreach(var g in s.migrations.ToList())foreach(var p in g.pawns.ToList())if(p!=null && p.Spawned && p.HostFaction==Faction.OfPlayer && !p.IsPrisoner && !p.IsSlave)OnRescued(p);
   for(int i=s.migrations.Count-1;i>=0;i--){
    var g=s.migrations[i];
    if(g.lord==null || g.map==null || !Find.Maps.Contains(g.map) || !g.map.lordManager.lords.Contains(g.lord)){if(!g.left)foreach(var t in g.campThings)if(t!=null && !t.Destroyed)t.Destroy();s.migrations.RemoveAt(i);continue;}
    if(g.theftTick>=0 && now>=g.theftTick && !g.left)Steal(g);
   }
  }
 }

 public class GiantGroup : IExposable {
  public Lord lord;public Map map;public IntVec3 camp;public string kind;
  public bool camped,answered,fed,left,stayAsked;public int campTick=-1,theftTick=-1;public float need,meatNeed;
  public List<Pawn> pawns=new List<Pawn>();public List<Thing> campThings=new List<Thing>(),stolen=new List<Thing>();public Pawn stayer;
  // 1.34.1：队伍原来所属的派系（好感、礼物、留下记到这里）；1.34.1 以前的存档没有，用 Lord 的派系。
  public Faction origin;public Faction Origin=>origin??lord?.faction;
  public IEnumerable<Pawn> Alive=>pawns.Where(p=>p!=null && !p.Dead && !p.Destroyed && p.Faction!=Faction.OfPlayer);
  public void ExposeData(){
   Scribe_References.Look(ref lord,"lord");Scribe_References.Look(ref map,"map");Scribe_Values.Look(ref camp,"camp");Scribe_Values.Look(ref kind,"kind");
   Scribe_Values.Look(ref camped,"camped");Scribe_Values.Look(ref answered,"answered");Scribe_Values.Look(ref fed,"fed");Scribe_Values.Look(ref left,"left");Scribe_Values.Look(ref stayAsked,"stayAsked");
   Scribe_Values.Look(ref campTick,"campTick",-1);Scribe_Values.Look(ref theftTick,"theftTick",-1);Scribe_Values.Look(ref need,"need");Scribe_Values.Look(ref meatNeed,"meatNeed");
   if(Scribe.mode==LoadSaveMode.Saving){pawns.RemoveAll(p=>p==null || p.Destroyed || p.Discarded);campThings.RemoveAll(t=>t==null || t.Destroyed);stolen.RemoveAll(t=>t==null || t.Destroyed);}
   Scribe_Collections.Look(ref pawns,"pawns",LookMode.Reference);Scribe_Collections.Look(ref campThings,"campThings",LookMode.Reference);Scribe_Collections.Look(ref stolen,"stolen",LookMode.Reference);
   Scribe_References.Look(ref stayer,"stayer");Scribe_References.Look(ref origin,"origin");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){pawns=pawns?.Where(p=>p!=null).ToList()??new List<Pawn>();campThings=campThings?.Where(t=>t!=null).ToList()??new List<Thing>();stolen=stolen?.Where(t=>t!=null).ToList()??new List<Thing>();}
  }
 }

 // 走到营地 → 扎营 1 天 → 走到对边 → 离开。被玩家打成敌对就整队进攻。
 public class LordJob_GiantMigration : LordJob {
  public IntVec3 camp,exit;
  public LordJob_GiantMigration(){}
  public LordJob_GiantMigration(IntVec3 camp,IntVec3 exit){this.camp=camp;this.exit=exit;}
  public override StateGraph CreateGraph(){
   var g=new StateGraph();
   var travel=new LordToil_Travel(camp);g.StartingToil=travel;
   var camping=new LordToil_DefendPoint(camp,6f,null);g.AddToil(camping);
   var leaving=new LordToil_Travel(exit);g.AddToil(leaving);
   var exitMap=new LordToil_ExitMap(LocomotionUrgency.Walk,false,true);g.AddToil(exitMap);
   var fight=new LordToil_AssaultColony(false,false);g.AddToil(fight);
   var arrive=new Transition(travel,camping);arrive.AddTrigger(new Trigger_Memo("TravelArrived"));arrive.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerDay/2));arrive.AddPreAction(new TransitionAction_Custom(()=>GiantMigration.OnCamp(GiantMigration.GroupOf(lord))));g.AddTransition(arrive);
   var pack=new Transition(camping,leaving);pack.AddTrigger(new Trigger_TicksPassed(GiantMigration.CampTicks));pack.AddPreAction(new TransitionAction_Custom(()=>GiantMigration.OnLeave(GiantMigration.GroupOf(lord))));g.AddTransition(pack);
   var go=new Transition(leaving,exitMap);go.AddTrigger(new Trigger_Memo("TravelArrived"));go.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerDay/3));g.AddTransition(go);
   var hostile=new Transition(travel,fight);hostile.AddSources(camping,leaving,exitMap);hostile.AddTrigger(new Trigger_BecamePlayerEnemy());hostile.AddPreAction(new TransitionAction_Custom(()=>{var gr=GiantMigration.GroupOf(lord);if(gr!=null){gr.left=true;foreach(var t in gr.campThings)if(t!=null && !t.Destroyed)t.Destroy();gr.campThings.Clear();}}));g.AddTransition(hostile);
   return g;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref camp,"camp");Scribe_Values.Look(ref exit,"exit");}
 }

 public class Letter_GiantMigration : LP_EventLetter {
  public string kind;public GiantGroup group;
  // 1.69.0 事件小人入口：讨食 / 预言等扎营队伍答复，想留下的巨人还没走。
  protected override bool Live=>group!=null && GiantMigration.State?.migrations.Contains(group)==true && (kind=="food"?!group.answered && !group.left:kind=="prophet"?!group.stayAsked && !group.left:kind=="stay" && Here(group.stayer) && group.stayer.Faction!=Faction.OfPlayer && group.pawns.Contains(group.stayer));
  protected override bool StateTracked=>kind!="stay";
  public override IEnumerable<Thing> EventThings=>group==null?Enumerable.Empty<Thing>():kind=="stay"?new Thing[]{group.stayer}:kind=="prophet"?new Thing[]{GiantMigration.Prophet(group)}:group.Alive.Cast<Thing>();
  public override IEnumerable<DiaOption> Choices{get{
   if(Closed || group==null){yield return Option_Close;yield break;}
   if(kind=="food"){
    float have=GiantMigration.StoredNutrition(group.map,GiantMigration.FineOnly(group)),meat=GiantMigration.StoredMeat(group.map);
    var give=Choice("给他们食物（营养值 "+group.need.ToString("0.#")+(group.meatNeed>0?"，另加生肉 "+group.meatNeed.ToString("0.#"):"")+"）",()=>GiantMigration.Feed(group));
    if(group.answered)give.Disable("已经答复过了");else if(have+.001f<group.need+group.meatNeed)give.Disable((GiantMigration.FineOnly(group)?"储存区里的精致饭、奢华饭不够":"储存区里的食物不够")+"，还差营养值 "+(group.need+group.meatNeed-have).ToString("0.#"));else if(meat+.001f<group.meatNeed)give.Disable("储存区里的生肉不够冬狼吃，还差营养值 "+(group.meatNeed-meat).ToString("0.#"));
    yield return give;
    var no=Choice("不给",()=>GiantMigration.Refuse(group));if(group.answered)no.Disable("已经答复过了");yield return no;
   }else if(kind=="prophet"){
    var listen=Choice("听他说完",()=>GiantMigration.ListenToProphet(group));if(group.stayAsked)listen.Disable("已经答复过了");yield return listen;
    var stay=Choice("请他留下",()=>GiantMigration.AskProphetStay(group));if(group.stayAsked)stay.Disable("已经答复过了");yield return stay;
   }else if(kind=="stay"){
    yield return Choice("接受",()=>GiantMigration.Join(group));
    yield return Choice("拒绝",null);
   }
   yield return Option_Postpone;
  }}
  DiaOption Choice(string text,Action act)=>new DiaOption(text){action=()=>{act?.Invoke();Resolve();},resolveTree=true};
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref kind,"kind");Scribe_Deep.Look(ref group,"lpGiantGroupCopy");
   // 信件里存的是同一个队伍的引用：读档后换成状态组件里的那一份。
   if(Scribe.mode==LoadSaveMode.PostLoadInit && group?.lord!=null){var live=GiantMigration.GroupOf(group.lord);if(live!=null)group=live;}
  }
 }

 public class IncidentWorker_GiantMigration : IncidentWorker {
  protected override bool CanFireNowSub(IncidentParms parms)=>GiantMigration.CanFire(parms.target as Map);
  // 1.31.0：受“DND 事件频率”影响。
  public override float ChanceFactorNow(IIncidentTarget target)=>base.ChanceFactorNow(target)*Events2.Frequency;
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;if(!GiantMigration.CanFire(map))return false;
   var g=GiantMigration.Start(map);if(g==null)return false;
   SendStandardLetter("巨人迁徙",GiantMigration.KindLabel(g.kind)+"（"+(g.Origin?.Name??g.lord.faction.Name)+"）正在穿过这片土地。他们看上去走了很久，饿得厉害，打算在殖民地外扎营歇一天。",LetterDefOf.NeutralEvent,parms,new LookTargets(g.Alive.Cast<Thing>()));
   return true;
  }
 }

 public partial class EventState {
  public List<GiantGroup> migrations=new List<GiantGroup>();
  void ExposeMigration(){
   Scribe_Collections.Look(ref migrations,"lpGiantMigrations",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit)migrations=migrations?.Where(g=>g!=null).ToList()??new List<GiantGroup>();
  }
  public override void GameComponentTick(){
   if(Find.TickManager.TicksGame%250!=77)return;
   // 1.34.1：没有队伍时也要跑（巨人游群的敌对复原、保持中立）。
   GiantMigration.Tick(this);
   if(pendingCalms.Count>0)PortalEvent.TickCalms(this);
  }
 }

 // 1.34.1：迁徙巨人被玩家救援（成为玩家的客人）时移出迁徙的 Lord。
 [HarmonyPatch(typeof(Pawn_GuestTracker),nameof(Pawn_GuestTracker.SetGuestStatus))]
 public static class GiantMigrationRescued {
  static void Postfix(Pawn ___pawn,Faction newHost,GuestStatus guestStatus){if(newHost!=null && newHost==Faction.OfPlayer && guestStatus==GuestStatus.Guest && ___pawn!=null)GiantMigration.OnRescued(___pawn);}
 }

 // ---------- 种族心情 ----------
 // 余烬火巨人扎营期间、殖民地有巨魔：队里的火巨人“厌恶巨魔”。
 public class ThoughtWorker_GiantHatesTrolls : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(p.Faction==Faction.OfPlayer || !FireGiant.Active(p) || !p.Spawned)return false;
   var g=GiantMigration.GroupOf(p);return g!=null && g.kind=="ember" && g.camped && !g.left && ColonyCensus.HasTroll;
  }
 }
 // 殖民地的火巨人看到余烬氏族的队伍在扎营：+3“氏族路过”。
 public class ThoughtWorker_ClanPassing : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(p.Faction!=Faction.OfPlayer || !FireGiant.Active(p) || !p.Spawned)return false;
   var s=PortalEvent.State;return s!=null && s.migrations.Any(g=>g.kind=="ember" && g.camped && !g.left && g.map==p.Map);
  }
 }
}
