using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 模组设置界面的派系一栏。
 public static class FactionSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.Label("1.19.0 派系（关掉后新世界不再生成；已有存档不删派系，只停相关事件）");
   l.CheckboxLabeled("费伦拓荒同盟",ref s.factionColony,"友善的费伦殖民者：人类、矮人、精灵、半兽人，四种商队。关掉同时停用猎巫狂热分子和竖琴手预警。");
   l.CheckboxLabeled("塞伊远征军",ref s.factionHostile,"红袍法师、符印巨魔、兽人与牛头人的敌对派系。关掉同时停用招揽和进贡来信。");
   l.CheckboxLabeled("余烬氏族",ref s.factionEmber,"火巨人领队带着人类和半兽人奴兵。");
   l.CheckboxLabeled("幽暗殖巢",ref s.factionIllithid,"夺心魔和被支配的奴仆，会从地下钻出。");
   l.CheckboxLabeled("黑网",ref s.factionBlackNet,"隐藏的走私商队，大约每季来一次。");
   l.CheckboxLabeled("红鹰商业联盟",ref s.factionRedHawk,"（1.64.0）中立的贸易联盟：奴隶商队（十几个不同种族）、魔法材料商队、契约卷轴商队，还有据点和飞空商船。");
   l.GapLine();
   l.CheckboxLabeled("竖琴手袭击预警",ref s.harperWarning,"与费伦拓荒同盟好感≥60时，大型敌对袭击推迟1～2天落地，并提前来信说明来自哪个派系。");
   l.CheckboxLabeled("斩首：符印巨魔狂暴",ref s.decapTrollBerserk,"红袍法师倒下时，他的符印巨魔失控狂暴、攻击所有人。关闭后改为逃跑。");
   l.CheckboxLabeled("猎巫狂热分子",ref s.zealotRaids,"殖民地有巫妖或夺心魔时，同盟里的狂热分子偶尔前来猎巫。");
   l.CheckboxLabeled("红袍招揽巫妖",ref s.redRobeRecruit,"有巫妖时，塞伊远征军来信招揽效忠。");
   l.CheckboxLabeled("向红袍进贡",ref s.tributeDemand,"没有巫妖且与塞伊远征军敌对时，对方来信索要贡银。");
   l.GapLine();
   if(Current.ProgramState==ProgramState.Playing && l.ButtonText("把已开启的新派系加入当前存档")){int n=Factions.AddMissingToCurrentGame();Messages.Message(n>0?"已加入"+n+"个派系，并在世界地图上为每个派系放下3个据点。":"当前存档已经有全部开启的派系。",MessageTypeDefOf.NeutralEvent,false);}
  }
 }

 // ==================== 殖民地人口普查（种族好感、狂热分子、招揽共用） ====================
 // 只数本殖民地成员（含奴隶）和囚犯，同 1.16.0 的种族关系；死灵造物按模组注册表数。
 public static class ColonyCensus {
  static int tick=-1;static bool lich,flayer,orc,giant,troll,friend124,ogre;static int constructs;
  static void Refresh(){
   int now=Find.TickManager?.TicksGame??0;if(now==tick)return;tick=now;
   lich=flayer=orc=giant=troll=friend124=ogre=false;constructs=0;
   foreach(var p in People()){
    if(Necro.Lich(p)!=null)lich=true;
    if(MindFlayer.IsMindFlayer(p))flayer=true;
    if(Necro.Construct(p)!=null)continue;
    if(CommonRaces.IsHalfOrc(p))orc=true;
    if(Troll.Is(p))troll=true;
    if(FireGiant.Active(p))giant=true;
    // 1.24.0：象人、元素裔、鸟人让同盟更亲近；食人魔让同盟疏远。
    if(Races124.IsLoxodon(p) || Genasi.IsGenasi(p) || Races124.IsAarakocra(p))friend124=true;
    if(Races124.IsOgre(p))ogre=true;
   }
   var list=Necro.Registry?.constructs;
   if(list!=null)foreach(var p in list)if(p!=null && !p.Dead && !p.Destroyed && (p.Faction==Faction.OfPlayer || p.IsPrisonerOfColony))constructs++;
  }
  public static IEnumerable<Pawn> People(){
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_Colonists)if(p.RaceProps.Humanlike)yield return p;
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_PrisonersOfColony)if(p.RaceProps.Humanlike)yield return p;
  }
  public static void Dirty(){tick=-1;}
  public static bool HasLich{get{Refresh();return lich;}}
  public static bool HasLichOrFlayer{get{Refresh();return lich || flayer;}}
  public static bool HasHalfOrc{get{Refresh();return orc;}}
  public static bool HasFireGiant{get{Refresh();return giant;}}
  public static bool HasTroll{get{Refresh();return troll;}}
  public static int Constructs{get{Refresh();return constructs;}}
  public static bool Holds(string condition){
   switch(condition){case "lichOrFlayer":return HasLichOrFlayer;case "halfOrc":return HasHalfOrc;case "fireGiant":return HasFireGiant;case "troll":return HasTroll;case "faerunFriend124":Refresh();return friend124;case "ogre":Refresh();return ogre;default:return false;}
  }
 }

 // ==================== 种族好感 ====================
 // 可配置条目：派系、条件、数值；perUnit>0 时按数量计（目前只有死灵造物），cap 封顶。
 public class RacialGoodwillExtension : DefModExtension {
  public FactionDef faction;public string condition;public int offset,perUnit;public int cap=-1;
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(faction==null)yield return "faction is required";if(condition!="lichOrFlayer" && condition!="halfOrc" && condition!="fireGiant" && condition!="troll" && condition!="constructs" && condition!="faerunFriend124" && condition!="ogre")yield return "unknown condition "+condition;}
  public int Value(){
   if(!Factions.Enabled(faction))return 0;
   if(condition=="constructs"){int v=ColonyCensus.Constructs*perUnit;return cap>=0?Math.Max(-cap,Math.Min(cap,v)):v;}
   return ColonyCensus.Holds(condition)?offset:0;
  }
 }
 public class GoodwillSituationWorker_Racial : GoodwillSituationWorker {
  RacialGoodwillExtension Ext=>def.GetModExtension<RacialGoodwillExtension>();
  public override int GetNaturalGoodwillOffset(Faction other){var e=Ext;return e==null || other?.def!=e.faction?0:e.Value();}
  public override string GetPostProcessedLabel(Faction other){var e=Ext;return e?.condition=="constructs"?def.label+"（"+ColonyCensus.Constructs+"具）":def.label;}
 }
 // 矮人血债：每笔让同盟好感上限 -10，最多 -50。
 public class GoodwillSituationWorker_BloodDebt : GoodwillSituationWorker {
  public override int GetMaxGoodwill(Faction other)=>other?.def==Factions.ColonyDef?BloodDebt.MaxGoodwill:100;
  public override string GetPostProcessedLabel(Faction other)=>def.label+"（"+BloodDebt.Debts+"笔）";
 }
 // 1.20.0 效忠塞伊 / 旧日盟约：原版会把好感慢慢拉回自然好感（塞伊远征军是天生敌人，自然好感为负）。
 // 生效期间用自然好感修正抵消其余所有负值，让自然好感≥0；效忠解除或盟约到期后修正自动消失，好感照常回落。
 public static class Pact {
  public static bool Pledged(Faction f)=>f!=null && f.def==Factions.HostileDef && Tribute.Events?.pledged==true && ColonyCensus.HasLich;
  public static bool OldPact(Faction f){var e=Tribute.Events;return e!=null && f!=null && e.oldPactFaction!=null && e.oldPactFaction==f.def && Find.TickManager.TicksGame<e.oldPactUntilTick;}
  public static float OldPactDaysLeft(){var e=Tribute.Events;return e==null?0:Math.Max(0,(e.oldPactUntilTick-Find.TickManager.TicksGame)/(float)GenDate.TicksPerDay);}
  public static void StartOldPact(FactionDef faction,float days){var e=Tribute.Events;if(e==null || faction==null || days<=0)return;e.oldPactFaction=faction;e.oldPactUntilTick=Find.TickManager.TicksGame+(int)(days*GenDate.TicksPerDay);Recalculate();}
  public static int OthersSum(Faction f){int sum=0;foreach(var d in DefDatabase<GoodwillSituationDef>.AllDefsListForReading)if(!(d.Worker is GoodwillSituationWorker_Pact))sum+=d.Worker.GetNaturalGoodwillOffset(f);return sum;}
  public static int Offset(Faction f)=>Math.Max(0,-OthersSum(f));
  public static void Recalculate(){Find.FactionManager?.goodwillSituationManager?.RecalculateAll(true);}
 }
 public abstract class GoodwillSituationWorker_Pact : GoodwillSituationWorker {
  protected abstract bool Applies(Faction f);
  public override int GetNaturalGoodwillOffset(Faction other)=>other!=null && !other.IsPlayer && Applies(other)?Pact.Offset(other):0;
 }
 public class GoodwillSituationWorker_ThayPledge : GoodwillSituationWorker_Pact {protected override bool Applies(Faction f)=>Pact.Pledged(f);}
 public class GoodwillSituationWorker_OldPact : GoodwillSituationWorker_Pact {
  protected override bool Applies(Faction f)=>Pact.OldPact(f) && !Pact.Pledged(f);
  public override string GetPostProcessedLabel(Faction other)=>def.label+"（还剩"+Pact.OldPactDaysLeft().ToString("0.#")+"天）";
 }
 public static class BloodDebt {
  public const int PerDebt=10,MaxDebts=5,Price=300;
  public static int Debts=>Tribute.Events?.dwarfDebts??0;
  public static int MaxGoodwill=>100-PerDebt*Math.Min(MaxDebts,Debts);
  public static void Add(){var e=Tribute.Events;if(e==null)return;e.dwarfDebts=Math.Min(MaxDebts,e.dwarfDebts+1);Notify();}
  public static bool Repay(Map map){
   var e=Tribute.Events;if(e==null || e.dwarfDebts<=0 || map==null || !TradeUtility.ColonyHasEnoughSilver(map,Price))return false;
   TradeUtility.LaunchSilver(map,Price);e.dwarfDebts--;Notify();return true;
  }
  static void Notify(){var c=Factions.Colony;if(c!=null)Find.FactionManager.goodwillSituationManager.RecalculateAll(true);}
  public static bool CountsAsDebt(Pawn victim,DamageInfo? dinfo)=>CountsAsDebt(victim,dinfo?.Instigator);
  public static bool CountsAsDebt(Pawn victim,Thing killer){
   if(victim?.Faction==null || victim.Faction.def!=Factions.ColonyDef || !CommonRaces.IsDwarf(victim))return false;
   return killer!=null && killer.Faction==Faction.OfPlayer;
  }
  public static void Record(Pawn victim){Add();Messages.Message("你杀死了费伦拓荒同盟的矮人"+victim.LabelShort+"：矮人记下了一笔血债（同盟好感上限 -"+PerDebt+"）。可以在通讯台上偿还。",MessageTypeDefOf.NegativeEvent,false);}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class BloodDebtOnKill {
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out bool __state){__state=!__instance.Dead && BloodDebt.CountsAsDebt(__instance,dinfo);}
  static void Postfix(Pawn __instance,bool __state){if(__state && __instance.Dead)BloodDebt.Record(__instance);}
 }
 // 通讯台：偿还矮人血债。
 [HarmonyPatch(typeof(FactionDialogMaker),nameof(FactionDialogMaker.FactionDialogFor))]
 public static class BloodDebtDialog {
  static void Postfix(Pawn negotiator,Faction faction,DiaNode __result){
   if(faction?.def!=Factions.ColonyDef || BloodDebt.Debts<=0 || __result?.options==null)return;
   var map=negotiator?.MapHeld;
   var opt=new DiaOption("偿还一笔矮人血债（"+BloodDebt.Price+"银，还剩"+BloodDebt.Debts+"笔）");
   if(map==null || !TradeUtility.ColonyHasEnoughSilver(map,BloodDebt.Price))opt.Disable("信标附近的白银不足"+BloodDebt.Price);
   var done=new DiaNode("矮人长老收下了偿金，在账本上划掉了一笔。"){};
   done.options.Add(new DiaOption("好"){resolveTree=true});
   opt.action=()=>BloodDebt.Repay(map);opt.link=done;
   __result.options.Insert(Math.Max(0,__result.options.Count-1),opt);
  }
 }

 // ==================== 剧本联动 ====================
 public class ScenPart_FactionGoodwill : ScenPart {
  public FactionDef faction;public int goodwill;
  // 1.20.0：旧日盟约天数（>0 时这段时间里自然好感不会把开局值拉回去）。
  public float pactDays;
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref faction,"faction");Scribe_Values.Look(ref goodwill,"goodwill");Scribe_Values.Look(ref pactDays,"pactDays");}
  public override string Summary(Scenario scen)=>faction==null?null:"开局时与"+(faction.fixedName??faction.label)+"的好感为"+goodwill+(pactDays>0?"；“旧日盟约”让这个好感维持"+pactDays.ToString("0")+"天，之后照常回落":"")+"。";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(faction==null)yield return "faction is null";if(pactDays<0)yield return "pactDays must not be negative";}
  string gwBuf,pactBuf;
  public override void DoEditInterface(Listing_ScenEdit listing){
   var r=listing.GetScenPartRect(this,RowHeight*3f);
   ScenEdit.DefButton(ScenEdit.Row(r,0,3),faction,Factions.Managed.Where(d=>!d.hidden),x=>faction=x);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,1,3),"好感 ",ref goodwill,ref gwBuf,-100,100);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,2,3),"旧日盟约天数 ",ref pactDays,ref pactBuf,0,120);
  }
  public override void PostGameStart(){if(Apply() && pactDays>0)Pact.StartOldPact(faction,pactDays);}
  public bool Apply(){var f=Factions.Of(faction);if(f==null || f.def.permanentEnemy)return false;Factions.SetRelation(f,Faction.OfPlayer,goodwill);return true;}
 }

 // ==================== 竖琴手袭击预警 ====================
 public class HarperEntry : IExposable {
  public IncidentDef def;public IncidentParms parms;public int fireTick;
  public void ExposeData(){Scribe_Defs.Look(ref def,"def");Scribe_Deep.Look(ref parms,"parms");Scribe_Values.Look(ref fireTick,"fireTick");}
 }
 public static class Harper {
  public const float PointsThreshold=1500f,MinGoodwill=60f;
  public static bool Bypass;
  public static bool BigRaid(Faction f,float points)=>f!=null && (points>=PointsThreshold || f.def==Factions.HostileDef || f.def==Factions.EmberDef || f.def==Factions.IllithidDef);
  public static bool Active(){
   var c=Factions.Colony;
   return LichMod.Settings.harperWarning && c!=null && Factions.Enabled(c) && !c.defeated && !c.HostileTo(Faction.OfPlayer) && c.PlayerGoodwill>=MinGoodwill;
  }
  // 返回 true 表示已推迟（已入队并发信）。
  // 1.34.0：风暴预兆（家园有成年风暴巨人，≥1000点推迟12小时）和风暴先知的预言（30天内第一次≥1000点的袭击提前1天预警）与竖琴手叠加，推迟时间相加。
  public static bool TryDelay(IncidentWorker_RaidEnemy worker,IncidentParms parms){
   if(Bypass || parms==null || parms.forced || !(parms.target is Map map) || !map.IsPlayerHome || parms.quest!=null)return false;
   bool harper=Active();var seer=StormGiant.OmenOn?StormGiant.OmenGiant(map):null;bool prophecy=StormGiantState.Get?.ProphecyPending==true;
   if(!harper && seer==null && !prophecy)return false;
   Traverse.Create(worker).Method("ResolveRaidPoints",parms).GetValue();
   if(!Traverse.Create(worker).Method("TryResolveRaidFaction",parms).GetValue<bool>())return false;
   var f=parms.faction;if(f==null || f==Factions.Colony || !f.HostileTo(Faction.OfPlayer))return false;
   harper=harper && BigRaid(f,parms.points);bool omen=seer!=null && parms.points>=StormGiant.OmenPoints;prophecy=prophecy && parms.points>=StormGiant.OmenPoints;
   if(!harper && !omen && !prophecy)return false;
   var events=Tribute.Events;if(events==null)return false;
   float days=(harper?Rand.Range(1f,2f):0f)+(omen?StormGiant.OmenDays:0f)+(prophecy?1f:0f);
   events.harperQueue.Add(new HarperEntry{def=worker.def,parms=parms,fireTick=Find.TickManager.TicksGame+(int)(days*GenDate.TicksPerDay)});
   string when=days<1f?"大约"+(days*24f).ToString("0")+"小时后":"大约"+days.ToString("0.#")+"天后";
   if(harper)Find.LetterStack.ReceiveLetter("竖琴手的警告","费伦拓荒同盟的竖琴手送来密信：\n\n"+f.NameColored+"正在集结人手，"+when+"会袭击"+map.Parent.LabelCap+"。\n\n趁现在做好准备。",LetterDefOf.ThreatSmall,new LookTargets(map.Parent),f);
   if(omen)Find.LetterStack.ReceiveLetter("风暴预兆",seer.LabelShort+"望着天边的云，脸色沉了下来：\n\n“风从"+Direction(map,parms)+"吹来，带着"+f.NameColored+"的气味。他们"+when+"就到。”"+(harper?"\n\n（竖琴手的密信和风暴巨人的预兆都应验了同一件事，袭击推迟的时间叠加。）":"")+"\n\n点数≥1000的敌对袭击，因风暴预兆推迟12小时落地。",LetterDefOf.ThreatSmall,new LookTargets(seer),f);
   if(prophecy){StormGiantState.Get.UseProphecy();Find.LetterStack.ReceiveLetter("先知的预言应验","风暴巨人先知说过的话应验了：\n\n"+f.NameColored+"正在逼近，"+when+"会袭击"+map.Parent.LabelCap+"。预言让你们多出了一天准备时间。",LetterDefOf.ThreatSmall,new LookTargets(map.Parent),f);}
   return true;
  }
  // 袭击来的方向（东南西北）：还没定落点时先挑一个地图边缘的进场点写进 parms，袭击落地时从那里来（空投到中心的袭击仍按原版落点）。
  public static string Direction(Map map,IncidentParms parms){
   if(!parms.spawnCenter.IsValid && RCellFinder.TryFindRandomPawnEntryCell(out var entry,map,CellFinder.EdgeRoadChance_Hostile))parms.spawnCenter=entry;
   var c=parms.spawnCenter.IsValid?parms.spawnCenter:CellFinder.RandomEdgeCell(map);
   int dx=c.x-map.Center.x,dz=c.z-map.Center.z;
   return Math.Abs(dx)>Math.Abs(dz)?(dx>0?"东边":"西边"):(dz>0?"北边":"南边");
  }
  public static int Process(FactionEvents events,bool force=false){
   int fired=0;
   for(int i=events.harperQueue.Count-1;i>=0;i--){
    var e=events.harperQueue[i];if(!force && Find.TickManager.TicksGame<e.fireTick)continue;
    events.harperQueue.RemoveAt(i);
    if(e.def==null || e.parms==null || !(e.parms.target is Map m) || !Find.Maps.Contains(m))continue;
    Bypass=true;try{if(e.def.Worker.TryExecute(e.parms))fired++;}finally{Bypass=false;}
   }
   return fired;
  }
 }
 [HarmonyPatch(typeof(IncidentWorker_RaidEnemy),"TryExecuteWorker")]
 public static class HarperRaidDelay {
  static bool Prefix(IncidentWorker_RaidEnemy __instance,IncidentParms parms,ref bool __result){
   if(__instance is IncidentWorker_ZealotRaid)return true;
   if(!Harper.TryDelay(__instance,parms))return true;
   __result=true;return false;
  }
 }

 // ==================== 地下钻出（只用于幽暗殖巢） ====================
 public class PawnsArrivalModeWorker_Burrow : PawnsArrivalModeWorker {
  public override bool CanUseWith(IncidentParms parms)=>parms.faction?.def==Factions.IllithidDef && base.CanUseWith(parms);
  public static bool Burrowable(IntVec3 c,Map map)=>c.InBounds(map) && c.Standable(map) && c.GetEdifice(map)==null && !c.Fogged(map) && !c.GetTerrain(map).IsWater && c.GetFirstPawn(map)==null && !c.Roofed(map);
  public static IntVec3 FindCenter(Map map){
   var anchors=map.mapPawns.FreeColonistsSpawned;
   IntVec3 home=anchors.Count>0?anchors.RandomElement().Position:map.Center;
   for(int radius=12;radius<=60;radius+=12)
    if(CellFinder.TryFindRandomCellNear(home,map,radius,c=>Burrowable(c,map) && c.DistanceTo(home)>=6 && GenRadial.RadialCellsAround(c,2.9f,true).Count(x=>Burrowable(x,map))>=12,out var cell))return cell;
   return CellFinder.TryFindRandomCell(map,c=>Burrowable(c,map),out var any)?any:IntVec3.Invalid;
  }
  public override bool TryResolveRaidSpawnCenter(IncidentParms parms){
   var map=parms.target as Map;if(map==null)return false;
   if(!parms.spawnCenter.IsValid || !Burrowable(parms.spawnCenter,map))parms.spawnCenter=FindCenter(map);
   parms.spawnRotation=Rot4.Random;
   return parms.spawnCenter.IsValid;
  }
  public override void Arrive(List<Pawn> pawns,IncidentParms parms){
   var map=(Map)parms.target;
   if(!parms.spawnCenter.IsValid)TryResolveRaidSpawnCenter(parms);
   var center=parms.spawnCenter;
   foreach(var p in pawns){
    if(!CellFinder.TryFindRandomCellNear(center,map,6,c=>Burrowable(c,map),out var cell))cell=center.IsValid?center:CellFinder.RandomCell(map);
    GenSpawn.Spawn(p,cell,map,parms.spawnRotation);
    Emerge(p,cell,map);
   }
  }
  public static void Emerge(Pawn p,IntVec3 cell,Map map){
   FilthMaker.TryMakeFilth(cell,map,ThingDefOf.Filth_Dirt,2);
   for(int i=0;i<3;i++)FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,Rand.Range(1.5f,2.5f),new Color(.55f,.45f,.35f));
   p.stances?.stunner?.StunFor(90,null,false,false,false);
  }
 }

 // ==================== 猎巫狂热分子 ====================
 public class IncidentWorker_ZealotRaid : IncidentWorker_RaidEnemy {
  public const string Label="猎巫狂热分子";
  public static bool Allowed(Map map){
   var c=Factions.Colony;
   return LichMod.Settings.zealotRaids && map!=null && map.IsPlayerHome && c!=null && Factions.Enabled(c) && !c.defeated && !c.HostileTo(Faction.OfPlayer) && ColonyCensus.HasLichOrFlayer && Factions.ZealotsDef!=null;
  }
  // 好感越低越频繁：好感100时×0.3，0时×1，-74时×2。
  public static float Frequency(int goodwill)=>Mathf.Clamp(1f-goodwill/100f*.7f+(goodwill<0?-goodwill/74f*.3f:0f),.3f,2f);
  protected override bool CanFireNowSub(IncidentParms parms)=>Allowed(parms.target as Map);
  public override float ChanceFactorNow(IIncidentTarget target)=>base.ChanceFactorNow(target)*(Factions.Colony==null?0f:Frequency(Factions.Colony.PlayerGoodwill));
  protected override bool TryResolveRaidFaction(IncidentParms parms){parms.faction=Factions.EnsureHidden(Factions.ZealotsDef);return parms.faction!=null;}
  public static List<Thing> Targets(Map map)=>map.mapPawns.AllHumanlikeSpawned.Where(p=>(p.Faction==Faction.OfPlayer || p.IsPrisonerOfColony) && (Necro.Lich(p)!=null || MindFlayer.IsMindFlayer(p))).Cast<Thing>().Concat(map.listerThings.ThingsOfDef(Necro.Def<ThingDef>("Phylactery"))).ToList();
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;if(!Allowed(map) || !TryResolveRaidFaction(parms))return false;
   parms.points=Math.Max(250f,parms.points*.7f);
   parms.raidStrategy=RaidStrategyDefOf.ImmediateAttack;parms.raidArrivalMode=PawnsArrivalModeDefOf.EdgeWalkIn;
   var targets=Targets(map);if(targets.Count>0)parms.attackTargets=targets;
   if(!base.TryExecuteWorker(parms))return false;
   Tribute.Events?.zealotRaid.AddRange(map.mapPawns.SpawnedPawnsInFaction(parms.faction).Where(p=>!p.Downed));
   return true;
  }
  protected override string GetLetterLabel(IncidentParms parms)=>Label;
  protected override string GetLetterText(IncidentParms parms,List<Pawn> pawns)=>"费伦拓荒同盟里的狂热分子不顾高层反对前来猎巫！一群圣武士和牧师听说这里藏着巫妖或夺心魔，要把它们烧成灰。"+(parms.attackTargets.NullOrEmpty()?"":"\n\n他们会优先冲向巫妖、夺心魔和命匣。")+"\n\n杀死他们不会影响你和同盟的关系；如果抓住了再放走，同盟高层会感激你的克制。";
 }
 // 释放俘虏的狂热分子：同盟好感 +5。
 [HarmonyPatch(typeof(GenGuest),nameof(GenGuest.PrisonerRelease))]
 public static class ZealotRelease {
  public const int Goodwill=5;
  static void Prefix(Pawn p,out bool __state){__state=p?.Faction?.def==Factions.ZealotsDef;}
  static void Postfix(Pawn p,bool __state){if(!__state)return;var c=Factions.Colony;if(c!=null && !c.HostileTo(Faction.OfPlayer) && c.TryAffectGoodwillWith(Faction.OfPlayer,Goodwill,false,false))Messages.Message("你放走了狂热分子"+p.LabelShort+"：费伦拓荒同盟的高层感激你的克制（好感 +"+Goodwill+"）。",MessageTypeDefOf.PositiveEvent,false);}
 }

 // ==================== 黑网商队 ====================
 public class IncidentWorker_BlackNetworkCaravan : IncidentWorker_TraderCaravanArrival {
  public override bool FactionCanBeGroupSource(Faction f,IncidentParms parms,bool desperate=false)=>f!=null && f.def==Factions.BlackNetDef && Factions.Enabled(f);
  protected override bool CanFireNowSub(IncidentParms parms){if(!Factions.Enabled(Factions.BlackNetDef))return false;Factions.EnsureHidden(Factions.BlackNetDef);return base.CanFireNowSub(parms);}
  protected override bool TryExecuteWorker(IncidentParms parms){
   if(!Factions.Enabled(Factions.BlackNetDef))return false;
   parms.faction=Factions.EnsureHidden(Factions.BlackNetDef);if(parms.faction==null)return false;
   parms.traderKind=DefDatabase<TraderKindDef>.GetNamed("LP_Caravan_BlackNetwork");
   return base.TryExecuteWorker(parms);
  }
 }

 // ==================== 朝贡：红袍招揽巫妖、向红袍进贡 ====================
 public static class Tribute {
  public const int SeasonTicks=15*GenDate.TicksPerDay,SoulDemand=150,RefuseSoulsGoodwill=-30,RefusePledge=-10;
  public static FactionEvents Events=>GameComp<FactionEvents>.Get();
  public static bool RaidHalved=>Events!=null && Find.TickManager.TicksGame<Events.tributeUntilTick;
  public static int Amount(Map map){float wealth=map?.wealthWatcher?.WealthTotal??0;return Mathf.Clamp(Mathf.RoundToInt(wealth*.02f/50f)*50,200,3000);}
  static IEnumerable<Phylactery> PlayerBoxes()=>Necro.Registry?.boxes.Where(b=>b!=null && !b.Destroyed && b.soul?.Faction==Faction.OfPlayer)??Enumerable.Empty<Phylactery>();
  public static float StoredSouls()=>PlayerBoxes().Sum(b=>b.experience);
  // 1.20.0：经验不够时可以倒扣等级（降一级返还那一级的经验）。能付出的总量 = 当前经验 + 1 级以上各级的经验。
  public static float PayableSouls()=>PlayerBoxes().Sum(b=>b.experience+Enumerable.Range(1,Math.Max(0,b.level-1)).Sum(l=>Necro.Requirement(l)));
  public static bool CanPaySouls()=>PayableSouls()+.001f>=SoulDemand;
  // 模拟上缴：先扣所有命匣的当前经验（多的先扣），还不够再从等级最高的命匣倒扣。返回每个命匣的结果等级与经验。
  public static List<(Phylactery box,int level,float exp)> PlanSouls(){
   var plan=PlayerBoxes().Select(b=>(box:b,level:b.level,exp:b.experience)).ToList();
   float left=SoulDemand;
   foreach(var i in Enumerable.Range(0,plan.Count).OrderByDescending(i=>plan[i].exp).ToList()){var e=plan[i];float take=Math.Min(left,e.exp);e.exp-=take;left-=take;plan[i]=e;if(left<=.001f)break;}
   while(left>.001f){
    int idx=-1;for(int i=0;i<plan.Count;i++)if(plan[i].level>1 && (idx<0 || plan[i].level>plan[idx].level))idx=i;
    if(idx<0)break;
    var e=plan[idx];e.level--;e.exp+=Necro.Requirement(e.level);float take=Math.Min(left,e.exp);e.exp-=take;left-=take;plan[idx]=e;
   }
   return plan;
  }
  public static string DemotionText(){
   if(!CanPaySouls())return null;
   var drops=PlanSouls().Where(e=>e.level<e.box.level).ToList();
   return drops.Count==0?null:string.Join("，",drops.Select(e=>"会使"+(drops.Count>1?e.box.soul.LabelShort+"的":"")+"命匣从"+e.box.level+"级降到"+e.level+"级"));
  }
  public static bool CanRecruit()=>LichMod.Settings.redRobeRecruit && Factions.Hostile!=null && Factions.Enabled(Factions.HostileDef) && !Factions.Hostile.defeated && ColonyCensus.HasLich;
  public static bool CanDemand()=>LichMod.Settings.tributeDemand && Factions.Hostile!=null && Factions.Enabled(Factions.HostileDef) && !Factions.Hostile.defeated && !ColonyCensus.HasLich && Factions.Hostile.HostileTo(Faction.OfPlayer);
  public static void Pledge(){var e=Events;var h=Factions.Hostile;if(e==null || h==null)return;e.pledged=true;e.nextSoulTick=Find.TickManager.TicksGame+SeasonTicks;Factions.SetRelation(h,Faction.OfPlayer,0);Pact.Recalculate();}
  public static void RefusePledgeOffer(){Factions.ChangeGoodwillExact(Factions.Hostile,RefusePledge,"拒绝红袍的招揽");}
  // 从命匣的献祭经验里扣，不够就倒扣等级；降级后收回超出等级的法术和统御容量（超出容量的造物按原规则休眠）。
  public static bool PaySouls(){
   if(!CanPaySouls())return false;
   foreach(var (box,lvl,exp) in PlanSouls()){
    bool dropped=lvl<box.level;box.level=lvl;box.experience=Math.Max(0,exp);
    if(!dropped)continue;
    var h=Necro.Lich(box.soul);h?.SyncSpells();h?.ClampFocus();
    foreach(var p in Necro.LivingConstructs(box.soul).ToList())Necro.Construct(p)?.Maintain(0);
   }
   return true;
  }
  // 拒交灵魂同时解除效忠。
  public static void RefuseSouls(){var e=Events;if(e!=null)e.pledged=false;Factions.ChangeGoodwillExact(Factions.Hostile,RefuseSoulsGoodwill,"拒交灵魂");Pact.Recalculate();}
  public static bool PayTribute(Map map,int amount){
   var e=Events;if(e==null || map==null || !TradeUtility.ColonyHasEnoughSilver(map,amount))return false;
   TradeUtility.LaunchSilver(map,amount);e.tributeUntilTick=Find.TickManager.TicksGame+SeasonTicks;return true;
  }
 }
 public class Letter_FactionChoice : ChoiceLetter {
  public string kind;public int amount;public Map map;
  public override IEnumerable<DiaOption> Choices{get{
   if(ArchivedOnly){yield return Option_Close;yield break;}
   if(kind=="recruit"){
    yield return Choice("宣誓效忠塞伊",()=>{Tribute.Pledge();Messages.Message("你向红袍法师宣誓效忠：塞伊远征军不再敌视你（好感设为0）。每季他们会来收取灵魂。",MessageTypeDefOf.NeutralEvent,false);});
    yield return Choice("拒绝",()=>Tribute.RefusePledgeOffer());
   }else if(kind=="souls"){
    var demote=Tribute.DemotionText();
    var pay=Choice("上缴"+Tribute.SoulDemand+"点献祭经验"+(demote==null?"":"（"+demote+"）"),()=>Tribute.PaySouls());
    if(!Tribute.CanPaySouls())pay.Disable("命匣只有1级，献祭经验也不足"+Tribute.SoulDemand);
    yield return pay;
    yield return Choice("拒交（好感 "+Tribute.RefuseSoulsGoodwill+"）",()=>Tribute.RefuseSouls());
   }else if(kind=="tribute"){
    var pay=Choice("交出"+amount+"白银",()=>Tribute.PayTribute(map,amount));
    if(map==null || !TradeUtility.ColonyHasEnoughSilver(map,amount))pay.Disable("信标附近的白银不足"+amount);
    yield return pay;
    yield return Choice("拒绝",null);
   }
   yield return Option_Postpone;
  }}
  DiaOption Choice(string text,Action act)=>new DiaOption(text){action=()=>{act?.Invoke();Find.LetterStack.RemoveLetter(this);},resolveTree=true};
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref kind,"kind");Scribe_Values.Look(ref amount,"amount");Scribe_References.Look(ref map,"map");}
 }

 public class FactionEvents : GameComponent {
  public int tributeUntilTick=-1,nextRecruitTick=-1,nextSoulTick=-1,nextTributeTick=-1,nextBlackNetTick=-1,dwarfDebts,zealotsRepelled,oldPactUntilTick=-1;
  public bool pledged;public FactionDef oldPactFaction;
  public List<ScheduledRaid> scheduledRaids=new List<ScheduledRaid>();
  public List<HarperEntry> harperQueue=new List<HarperEntry>();
  public List<Pawn> zealotRaid=new List<Pawn>();
  // 审判官接口：每打退一次狂热分子触发一次，参数是累计次数。
  public static event Action<int> ZealotsRepelled;
  public FactionEvents(Game game){}
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;
   if(harperQueue.Count>0 && now%250==0)Harper.Process(this);
   if(scheduledRaids.Count>0 && now%250==125)ScheduledRaid.Process(this);
   if(now%2500!=1250)return;
   CheckZealots();
   if(now%GenDate.TicksPerDay==1250)Daily(now);
  }
  public void Daily(int now){
   if(nextRecruitTick<0)nextRecruitTick=now+Rand.Range(1,3)*Tribute.SeasonTicks/2+Rand.Range(0,Tribute.SeasonTicks);
   if(nextTributeTick<0)nextTributeTick=now+Tribute.SeasonTicks;
   if(pledged && !ColonyCensus.HasLich){pledged=false;Pact.Recalculate();}
   if(oldPactFaction!=null && now>=oldPactUntilTick){oldPactFaction=null;Pact.Recalculate();}
   if(!pledged && now>=nextRecruitTick){nextRecruitTick=now+Rand.Range(Tribute.SeasonTicks,2*Tribute.SeasonTicks);if(Tribute.CanRecruit())SendRecruit();}
   if(pledged && now>=nextSoulTick && Tribute.CanRecruit()){nextSoulTick=now+Tribute.SeasonTicks;SendSouls();}
   if(now>=nextTributeTick){nextTributeTick=now+Tribute.SeasonTicks;if(Tribute.CanDemand() && Rand.Chance(.5f))SendTribute(Find.AnyPlayerHomeMap);}
   if(nextBlackNetTick<0)nextBlackNetTick=now+Rand.Range(10,20)*GenDate.TicksPerDay;
   if(now>=nextBlackNetTick){nextBlackNetTick=now+Rand.Range(12,18)*GenDate.TicksPerDay;if(Factions.Enabled(Factions.BlackNetDef) && Rand.Chance(.8f))FireBlackNet(Find.AnyPlayerHomeMap);}
  }
  // 黑网商队：大约每季一次（12～18天检查一次，80%概率来）。
  public static bool FireBlackNet(Map map){
   if(map==null)return false;var def=DefDatabase<IncidentDef>.GetNamed("LP_BlackNetworkCaravan");
   var parms=StorytellerUtility.DefaultParmsNow(def.category,map);
   return def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms);
  }
  static Letter_FactionChoice Make(string label,string text,string kind){var l=(Letter_FactionChoice)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_FactionChoiceLetter"));l.Label=label;l.Text=text;l.kind=kind;l.relatedFaction=Factions.Hostile;return l;}
  public static Letter_FactionChoice SendRecruit(){var l=Make("红袍的招揽","一位红袍法师派信使送来一封盖着骷髅火漆的信：\n\n“塞伊欢迎掌握不死之道的人。向大法师宣誓效忠，远征军就不再与你为敌。我们只要一点点回报——每季一些灵魂。”\n\n效忠：与塞伊远征军的好感设为0（中立），之后每季要上缴"+Tribute.SoulDemand+"点命匣献祭经验，拒交好感"+Tribute.RefuseSoulsGoodwill+"。\n拒绝：好感"+Tribute.RefusePledge+"。","recruit");Find.LetterStack.ReceiveLetter(l);return l;}
  public static Letter_FactionChoice SendSouls(){var l=Make("上缴灵魂","红袍法师的信使来收这一季的灵魂了：上缴"+Tribute.SoulDemand+"点命匣献祭经验。\n\n命匣里现有献祭经验："+Tribute.StoredSouls().ToString("0")+"。当前经验不够时会倒扣命匣等级（降一级返还那一级的经验）。","souls");Find.LetterStack.ReceiveLetter(l);return l;}
  public static Letter_FactionChoice SendTribute(Map map){if(map==null)return null;int amount=Tribute.Amount(map);var l=Make("进贡要求","塞伊远征军的使者带着一队兽人来到殖民地外：\n\n“交出"+amount+"白银，远征军这一季就少来打扰你。”\n\n交钱后一季（15天）内，塞伊远征军来袭的概率减半。拒绝不会有别的后果。","tribute");l.amount=amount;l.map=map;Find.LetterStack.ReceiveLetter(l);return l;}
  public void CheckZealots(){
   if(zealotRaid.Count==0)return;
   zealotRaid.RemoveAll(p=>p==null || p.Destroyed);
   if(zealotRaid.Any(p=>!p.Dead && !p.Downed && p.Spawned && !p.IsPrisoner && p.Map.IsPlayerHome && p.HostileTo(Faction.OfPlayer)))return;
   zealotRaid.Clear();zealotsRepelled++;
   Messages.Message("猎巫狂热分子被打退了（累计"+zealotsRepelled+"次）。",MessageTypeDefOf.PositiveEvent,false);
   ZealotsRepelled?.Invoke(zealotsRepelled);
  }
  public override void ExposeData(){
   Scribe_Values.Look(ref tributeUntilTick,"lpTributeUntil",-1);Scribe_Values.Look(ref nextRecruitTick,"lpNextRecruit",-1);Scribe_Values.Look(ref nextSoulTick,"lpNextSoul",-1);Scribe_Values.Look(ref nextTributeTick,"lpNextTribute",-1);Scribe_Values.Look(ref nextBlackNetTick,"lpNextBlackNet",-1);
   Scribe_Values.Look(ref dwarfDebts,"lpDwarfDebts");Scribe_Values.Look(ref zealotsRepelled,"lpZealotsRepelled");Scribe_Values.Look(ref pledged,"lpPledged");
   Scribe_Values.Look(ref oldPactUntilTick,"lpOldPactUntil",-1);Scribe_Defs.Look(ref oldPactFaction,"lpOldPactFaction");
   Scribe_Collections.Look(ref scheduledRaids,"lpScheduledRaids",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit)scheduledRaids=scheduledRaids?.Where(r=>r!=null).ToList()??new List<ScheduledRaid>();
   if(Scribe.mode==LoadSaveMode.Saving)zealotRaid.RemoveAll(p=>p==null || p.Destroyed || p.Discarded || !p.SpawnedOrAnyParentSpawned);
   Scribe_Collections.Look(ref harperQueue,"lpHarperQueue",LookMode.Deep);
   Scribe_Collections.Look(ref zealotRaid,"lpZealotRaid",LookMode.Reference);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){harperQueue=harperQueue?.Where(e=>e?.def!=null && e.parms!=null).ToList()??new List<HarperEntry>();zealotRaid=zealotRaid?.Where(p=>p!=null).ToList()??new List<Pawn>();}
  }
  public override void FinalizeInit(){base.FinalizeInit();if(Current.ProgramState==ProgramState.Playing || Find.FactionManager!=null){foreach(var def in Factions.Managed.Where(d=>d.hidden).ToList())Factions.EnsureHidden(def);Factions.EnsureEnemyPairs();}}
 }
}
