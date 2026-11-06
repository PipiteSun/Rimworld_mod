using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.119.0 扭曲肉资源化 D 节：异象 × DND 主题。

 // ---------- D3 凯兰沃净化焚化（信仰圣坛）：每 30 份 +1 神眷，每天最多 +3，只对凯兰沃 / 拉山德 / 莎尔有效 ----------
 // 1.59.0 教训：原版先 ConsumeIngredients（Destroy 把 stackCount 清零）再 Notify_IterationCompleted，所以在消耗时累计。
 public class RecipeWorker_TwistedOffering : RecipeWorker {
  public const int Per=30,DailyMax=3;
  public static readonly string[] Gods={"Kelemvor","Lathander","Shar"};
  float pending;
  public static bool GodOk(LP_DeityDef g)=>g!=null && Gods.Contains(g.key);
  public static int LeftToday=>DailyMax-(TwistedMeat.Comp?.Today("favor")??DailyMax);
  public override bool AvailableOnNow(Thing thing,BodyPartRecord part=null)=>Faith.Enabled && GodOk(FaithUtility.Patron) && LeftToday>0 && base.AvailableOnNow(thing,part);
  public override void ConsumeIngredient(Thing ingredient,RecipeDef recipe,Map map){if(TwistedMeat.IsMeat(ingredient))pending+=ingredient.stackCount;base.ConsumeIngredient(ingredient,recipe,map);}
  public override void Notify_IterationCompleted(Pawn billDoer,List<Thing> ingredients){
   base.Notify_IterationCompleted(billDoer,ingredients);int units=Mathf.FloorToInt(pending/Per+.001f);pending=0f;Grant(billDoer,units);
  }
  public static int Grant(Pawn doer,int units){
   var god=FaithUtility.Patron;var c=TwistedMeat.Comp;if(!GodOk(god) || c==null)return 0;
   int n=Mathf.Clamp(units,0,LeftToday);if(n<=0)return 0;c.AddToday("favor",n);
   Faith.Gain(god,n,(doer?.LabelShort??"殖民地")+"焚化扭曲肉");if(doer!=null)Items.Mote(doer,"+"+(n*Faith.GainFactor).ToString("0.#")+" 神眷",god.color);
   return n;
  }
 }

 // ---------- D4 远域解剖：异象知识（基础）+ 累计 500 份完成「异怪学」；解剖者 5% 远域低语 ----------
 public class RecipeWorker_FarDissection : RecipeWorker {
  public const float KnowledgePer50=1f,Unlock=500f,WhisperChance=.05f;
  float pending;
  public override void ConsumeIngredient(Thing ingredient,RecipeDef recipe,Map map){if(TwistedMeat.IsMeat(ingredient))pending+=ingredient.stackCount;base.ConsumeIngredient(ingredient,recipe,map);}
  public override void Notify_IterationCompleted(Pawn billDoer,List<Thing> ingredients){base.Notify_IterationCompleted(billDoer,ingredients);float n=pending;pending=0f;Dissect(billDoer,n);}
  public static ResearchProjectDef Aberrology=>DefCache<ResearchProjectDef>.Get("LP_Research_Aberrology");
  public static void Dissect(Pawn doer,float n){
   var c=TwistedMeat.Comp;if(c==null || n<=0f)return;c.dissected+=n;
   var cat=DefCache<KnowledgeCategoryDef>.Get("Basic");if(cat!=null && ModsConfig.AnomalyActive)try{Find.ResearchManager.ApplyKnowledge(cat,KnowledgePer50*n/50f);}catch(Exception e){Log.Warning("[DND边缘大陆] 远域解剖知识失败："+e.Message);}
   var proj=Aberrology;
   if(proj!=null && !proj.IsFinished && c.dissected>=Unlock){Find.ResearchManager.FinishProject(proj,false,doer,true);c.aberrologyDone=true;}
   TwistedMeat.MaybeWhisper(doer,WhisperChance);
  }
 }
 // 「异怪学」不能直接研究；「血肉塑形术」装了异象时要先有一项异象血肉研究。
 [HarmonyPatch(typeof(ResearchProjectDef),nameof(ResearchProjectDef.CanStartNow),MethodType.Getter)]
 public static class TwistedResearchGate {
  public static readonly string[] AnomalyFlesh={"SummonFleshbeastsPlayer","GhoulInfusion","MutationWeaponry"};
  static void Postfix(ResearchProjectDef __instance,ref bool __result){
   if(!__result)return;var n=__instance.defName;
   if(n=="LP_Research_Aberrology")__result=false;
   else if(n=="LP_Research_FleshShaping" && ModsConfig.AnomalyActive && !AnomalyFleshDone)__result=false;
  }
  public static bool AnomalyFleshDone{get{foreach(var a in AnomalyFlesh){var d=DefCache<ResearchProjectDef>.Get(a);if(d!=null && d.IsFinished)return true;}return false;}}
 }
 // 异怪学 +10%、哈达之眼 +15%：对异象实体和异怪类（眼魔、夺心魔、拟形怪……）的伤害。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class AberrologyDamage {
  public const float ResearchBonus=.1f,EyeBonus=.15f;
  static readonly ConcurrentDictionary<Def,bool> cache=new ConcurrentDictionary<Def,bool>();
  static readonly string[] Keys={"Beholder","MindFlayer","Illithid","Mimic","Aboleth","Gibbering","Nothic"};
  static bool Named(Def d)=>d!=null && cache.GetOrAdd(d,x=>Keys.Any(k=>x.defName.IndexOf(k,StringComparison.OrdinalIgnoreCase)>=0));
  public static bool IsAberration(Pawn p){
   if(p==null)return false;
   if(p.RaceProps.IsAnomalyEntity)return true;
   if(ModsConfig.AnomalyActive && p.Faction!=null && p.Faction==Faction.OfEntities)return true;
   return Named(p.def) || Named(p.kindDef) || Named(p.genes?.Xenotype);
  }
  public static float Factor(Pawn attacker,Pawn victim){
   if(attacker==null || !IsAberration(victim))return 1f;float f=1f;
   if(attacker.Faction==Faction.OfPlayer){var proj=RecipeWorker_FarDissection.Aberrology;if(proj!=null && proj.IsFinished)f+=ResearchBonus;}
   if(attacker.apparel!=null)foreach(var a in attacker.apparel.WornApparel)if(a.def.defName=="LP_EyeOfHadar"){f+=EyeBonus;break;}
   return f;
  }
  static void Prefix(Thing __instance,ref DamageInfo dinfo){
   if(!(__instance is Pawn victim) || !(dinfo.Instigator is Pawn attacker) || dinfo.Amount<=0f)return;
   float f=Factor(attacker,victim);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }

 // ---------- D1 生物铁析出：每个殖民地每天最多 30 份 ----------
 public class RecipeWorker_Bioferrite : RecipeWorker {
  public const int DailyMax=30,Per=10;
  public static int MadeToday=>TwistedMeat.Comp?.Today("bioferrite")??0;
  public override bool AvailableOnNow(Thing thing,BodyPartRecord part=null)=>MadeToday+Per<=DailyMax && base.AvailableOnNow(thing,part);
  public override void Notify_IterationCompleted(Pawn billDoer,List<Thing> ingredients){base.Notify_IterationCompleted(billDoer,ingredients);TwistedMeat.Comp?.AddToday("bioferrite",Per);}
 }
 [HarmonyPatch(typeof(Bill_Production),nameof(Bill_Production.ShouldDoNow))]
 public static class BioferriteDailyCap {
  static void Postfix(Bill_Production __instance,ref bool __result){if(__result && __instance.recipe?.Worker is RecipeWorker_Bioferrite && RecipeWorker_Bioferrite.MadeToday+RecipeWorker_Bioferrite.Per>RecipeWorker_Bioferrite.DailyMax)__result=false;}
 }

 // ---------- D2 噬尸筵席：食尸鬼吃扭曲肉营养 ×3 + 远域饱食 6 小时；死灵之躯不得「吃了扭曲肉」心情 ----------
 [HarmonyPatch(typeof(Thing),nameof(Thing.Ingested))]
 public static class TwistedIngest {
  public const float GhoulFactor=3f;
  static void Prefix(Thing __instance,out int __state){__state=TwistedMeat.IsMeat(__instance)?__instance.stackCount:-1;}
  static void Postfix(Thing __instance,Pawn ingester,int __state,ref float __result){
   if(__state<0 || ingester==null)return;
   int eaten=__instance.Destroyed?__state:__state-__instance.stackCount;
   if(ingester.IsGhoul){
    __result*=GhoulFactor;var h=DefCache<HediffDef>.Get("LP_FarRealmSated");
    if(h!=null){var old=ingester.health.hediffSet.GetFirstHediffOfDef(h);if(old!=null)ingester.health.RemoveHediff(old);ingester.health.AddHediff(h);}
    TwistedMeat.Consumed(eaten);
   }
   if((ingester.IsGhoul || Necro.Undead(ingester)) && TwistedMeat.AteTwisted!=null)ingester.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(TwistedMeat.AteTwisted);
  }
 }

 // ---------- D6 远域献飨（Anomaly 灵能仪式） ----------
 public class PsychicRitualDef_FarRealmFeast : PsychicRitualDef_InvocationCircle {
  public const float WarlockBonus=.25f,FailFactor=.25f,AdamantineFrom=.6f,EyeFrom=.9f,EyeChance=.3f;
  public static float FailChance(float q)=>(1f-Mathf.Clamp01(q))*FailFactor;
  public static bool GreatOldOne(Pawn p)=>p!=null && ClassSubclass.Has(p,Warlock.GreatOldOne);
  public override List<PsychicRitualToil> CreateToils(PsychicRitual psychicRitual,PsychicRitualGraph parent){
   var list=base.CreateToils(psychicRitual,parent);list.Add(new PsychicRitualToil_FarRealmFeast(InvokerRole));return list;
  }
  public override void CalculateMaxPower(PsychicRitualRoleAssignments assignments,List<QualityFactor> powerFactorsOut,out float power){
   base.CalculateMaxPower(assignments,powerFactorsOut,out power);
   var inv=assignments?.FirstAssignedPawn(InvokerRole);bool goo=GreatOldOne(inv);
   powerFactorsOut?.Add(new QualityFactor{label="旧日支配者邪术师主持",count=goo?"是":"否",qualityChange=goo?"+25%":"0%",quality=goo?WarlockBonus:0f,positive=goo,present=goo,priority=5f,toolTip="主持人是旧日支配者宗主的邪术师时，仪式质量 +25%。"});
   if(goo)power+=WarlockBonus;power=Mathf.Clamp01(power);
  }
  public override TaggedString OutcomeDescription(FloatRange qualityRange,string qualityNumber,PsychicRitualRoleAssignments assignments){
   float q=qualityRange.min;
   string reward=q>=EyeFrom?"精金 "+Adamantine(q)+" 份，"+EyeChance.ToStringPercent()+" 机会改为遗物“哈达之眼”":q>=AdamantineFrom?"精金 "+Adamantine(q)+" 份":"秘银 "+Mithral(q)+" 份";
   return "献上 150 份扭曲肉。以现在的质量：奖励"+reward+"；失败率 "+FailChance(q).ToStringPercent()+"（失败会引来血肉兽群或心灵哭嚎）。\n质量 60% 起换成精金，90% 起有机会得到哈达之眼。";
  }
  public static int Mithral(float q)=>Mathf.RoundToInt(Mathf.Lerp(15f,40f,Mathf.Clamp01(q/AdamantineFrom)));
  public static int Adamantine(float q)=>Mathf.RoundToInt(Mathf.Lerp(8f,20f,Mathf.Clamp01((q-AdamantineFrom)/(1f-AdamantineFrom))));
  // 结算：返回 "fail-beasts" / "fail-wail" / "mithral" / "adamantine" / "eye"。roll 传负数用随机。
  public static string Resolve(float q,Map map,IntVec3 cell,Pawn invoker,float roll=-1f,float pick=-1f){
   if(roll<0f)roll=Rand.Value;if(pick<0f)pick=Rand.Value;
   if(roll<FailChance(q)){
    bool beasts=pick<.5f;
    if(beasts){float pts=Mathf.Max(300f,StorytellerUtility.DefaultThreatPointsNow(map)*.5f);try{FleshbeastUtility.SpawnFleshbeastsFromPitBurrowEmergence(cell,map,pts,new IntRange(300,600),new IntRange(60,180),true);}catch(Exception e){Log.Warning("[DND边缘大陆] 远域献飨血肉兽失败："+e.Message);beasts=false;}}
    if(!beasts){var cond=GameConditionMaker.MakeCondition(GameConditionDefOf.PsychicDrone,GenDate.TicksPerDay);if(cond is GameCondition_PsychicEmanation pe){pe.gender=Rand.Bool?Gender.Male:Gender.Female;pe.level=PsychicDroneLevel.BadHigh;}map.gameConditionManager.RegisterCondition(cond);}
    Find.LetterStack.ReceiveLetter("远域献飨失败","远域吃下了扭曲肉，却没有回礼。"+(beasts?"地底传来翻搅声：一群血肉兽要钻出来了！":"一股心灵哭嚎从仪式点扩散开来，持续一天。"),LetterDefOf.ThreatBig,new LookTargets(cell,map));
    return beasts?"fail-beasts":"fail-wail";
   }
   string kind;Thing reward;
   if(q>=EyeFrom && pick<EyeChance && DefCache<ThingDef>.Get("LP_EyeOfHadar") is ThingDef eye){kind="eye";reward=ThingMaker.MakeThing(eye);}
   else if(q>=AdamantineFrom && DefCache<ThingDef>.Get("LP_Adamantine") is ThingDef ad){kind="adamantine";reward=ThingMaker.MakeThing(ad);reward.stackCount=Adamantine(q);}
   else{kind="mithral";var mi=DefCache<ThingDef>.Get("LP_Mithral");reward=mi!=null?ThingMaker.MakeThing(mi):ThingMaker.MakeThing(ThingDefOf.Silver);reward.stackCount=mi!=null?Mithral(q):Mithral(q)*20;}
   GenPlace.TryPlaceThing(reward,cell,map,ThingPlaceMode.Near);
   Find.LetterStack.ReceiveLetter("远域献飨","远域收下了扭曲肉。仪式点上多了："+reward.LabelCap+"。",LetterDefOf.PositiveEvent,new LookTargets(reward));
   return kind;
  }
 }
 public class PsychicRitualToil_FarRealmFeast : PsychicRitualToil {
  PsychicRitualRoleDef invokerRole;
  public PsychicRitualToil_FarRealmFeast(){}
  public PsychicRitualToil_FarRealmFeast(PsychicRitualRoleDef invokerRole){this.invokerRole=invokerRole;}
  public override void Start(PsychicRitual psychicRitual,PsychicRitualGraph parent){
   base.Start(psychicRitual,parent);
   var map=psychicRitual.Map;var inv=psychicRitual.assignments.FirstAssignedPawn(invokerRole);
   IntVec3 cell=psychicRitual.assignments.Target.Cell;if(!cell.IsValid)cell=inv?.Position??IntVec3.Invalid;if(map==null || !cell.IsValid)return;
   PsychicRitualDef_FarRealmFeast.Resolve(psychicRitual.PowerPercent,map,cell,inv);
   TwistedMeat.Consumed(150f);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref invokerRole,"invokerRole");}
 }

 // ---------- D5 鬼婆集市：每季至多一次；离开时 10% 留下一件带诅咒的礼物 ----------
 public class IncidentWorker_HagMarket : IncidentWorker {
  public static int Quadrum=>TwistedMeat.Day/15;
  protected override bool CanFireNowSub(IncidentParms parms)=>Allowed(parms.target as Map,parms.forced);
  public static bool Allowed(Map map,bool forced){
   var c=TwistedMeat.Comp;if(!TwistedMeat.Active || c==null || map==null || !map.IsPlayerHome || PactEvents.CourtDef==null)return false;
   if(c.hagPawns.Count>0)return false;return forced || c.hagQuadrum!=Quadrum;
  }
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;var c=TwistedMeat.Comp;var f=PactEvents.Court();var kind=DefCache<TraderKindDef>.Get("LP_HagMarketTrader");if(f==null || kind==null || c==null)return false;
   var made=PactEvents.SpawnCaravan(map,f,kind,parms.points);if(made.Count==0)return false;
   c.hagQuadrum=Quadrum;c.hagPawns=made.ToList();c.hagMap=map;c.hagCell=made[0].Position;
   Find.LetterStack.ReceiveLetter("鬼婆集市","雾里走来一支古怪的小商队：几个裹着破斗篷、牙齿太尖的老妇人，推着一车冒泡的罐子。她们卖药水、诅咒物和黑暗的书，最想要的是扭曲肉（3 银一份）和畸变膏（18 银一份）。\n\n据说鬼婆走的时候偶尔会留下“礼物”——最好别收。",LetterDefOf.PositiveEvent,new LookTargets(made.Cast<Thing>()));
   return true;
  }
 }
 public static class HagMarket {
  public const float GiftChance=.1f;public static bool ForceGift;
  public static readonly string[] Gifts={"LP_MarkBlood","LP_MarkDecay","LP_MarkChange","LP_MarkRapture","LP_SwordVengeance"};
  public const string TraderKind="LP_HagMarketTrader";
  static HashSet<string> buyers;
  // 收畸变膏的商人：九狱掮客、妖精集市、鬼婆集市，以及深渊 / 契约阵营自己的商队。
  public static HashSet<string> Buyers{get{
   if(buyers==null){var s=new HashSet<string>{"LP_InfernalCaravanTrader","LP_FeyMarketTrader",TraderKind};
    foreach(var n in new[]{"LP_HellsBrokers","LP_FeyCourt","LP_AbyssHorde","LP_HellsLegion"}){var f=DefCache<FactionDef>.Get(n);if(f==null)continue;foreach(var k in f.caravanTraderKinds.Concat(f.baseTraderKinds).Concat(f.visitorTraderKinds))if(k!=null)s.Add(k.defName);}
    buyers=s;}
   return buyers;}}
  public static void Tick(GameComponent_TwistedMeat c){
   if(c.hagPawns.Count==0)return;
   var here=c.hagPawns.Where(p=>p!=null && p.Spawned && p.Map==c.hagMap).ToList();
   if(here.Count>0){c.hagCell=here[0].Position;return;}
   bool left=c.hagPawns.Any(p=>p!=null && !p.Dead && !p.Destroyed);
   var map=c.hagMap;var cell=c.hagCell;c.hagPawns.Clear();c.hagMap=null;
   if(left && map!=null && cell.IsValid && cell.InBounds(map))MaybeGift(map,cell);
  }
  public static Thing MaybeGift(Map map,IntVec3 cell){
   if(!ForceGift && !Rand.Chance(GiftChance))return null;
   var def=Gifts.Select(n=>DefCache<ThingDef>.Get(n)).Where(d=>d!=null).RandomElementWithFallback();if(def==null)return null;
   var t=ThingMaker.MakeThing(def,def.MadeFromStuff?GenStuff.DefaultStuffFor(def):null);GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);
   Find.LetterStack.ReceiveLetter("鬼婆的礼物","鬼婆集市走了，原地留下一件用黑布包着的“礼物”："+t.LabelCap+"。上面的诅咒还没散……",LetterDefOf.NeutralEvent,new LookTargets(t));
   return t;
  }
 }
 [HarmonyPatch(typeof(TradeUtility),nameof(TradeUtility.GetPricePlayerSell))]
 public static class TwistedSellPrice {
  public const float MeatPrice=3f,PastePrice=18f,PasteFactor=1.5f;
  static void Postfix(Thing thing,ref float __result){
   if(thing==null || TwistedMeat.Meat==null)return;bool meat=TwistedMeat.IsMeat(thing),paste=TwistedMeat.IsPaste(thing);if(!meat && !paste)return;
   var k=TradeSession.trader?.TraderKind;if(k==null)return;
   if(k.defName==HagMarket.TraderKind){if(meat)__result=Mathf.Max(__result,MeatPrice);else __result=Mathf.Max(__result,PastePrice);}
   else if(paste && HagMarket.Buyers.Contains(k.defName))__result*=PasteFactor;
  }
 }
 [HarmonyPatch(typeof(TraderKindDef),nameof(TraderKindDef.WillTrade))]
 public static class PasteWillTrade {
  static void Postfix(TraderKindDef __instance,ThingDef td,ref bool __result){if(td!=null && TwistedMeat.Paste!=null && td==TwistedMeat.Paste)__result=HagMarket.Buyers.Contains(__instance.defName);}
 }
}
