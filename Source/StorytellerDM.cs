using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.153.0 叙事者「地下城主 塔莉亚」（LP_DungeonMaster）。
 // 两个专属组件：
 //  1. StorytellerComp_DmCampaign：平均每 mtbDays 天从本模组的事件里挑一个（巨人迁徙、吟游诗人、龙过境、亵渎者……）。
 //  2. StorytellerComp_DmFateRoll：每 periodDays 天掷一次「命运之骰」d20（事件 LP_DmFateRoll），按点数追加好坏事件。
 // 其余（威胁曲线、商队、任务）照卡桑德拉经典。
 public static class DungeonMaster {
  public const string PackageId="local.lich.phylactery";
  // 掷出好运时会挑的正面事件。
  public static readonly string[] GoodEvents={"LP_E3_Bards","LP_E3_Starfall","LP_E3_SilverGuest","LP_E3_Ward","LP_E3_FallenParty","LP_E3_GhostWish","LP_E3_WeaveSurge","LP_E3_Tourney","LP_E3_Beggar","LP_GiantMigration","LP_WanderingMentor","LP_Faith_Pilgrims","LP_FallenDeva","LP_Faith_Trial"};
  public static bool IsLP(IncidentDef d)=>d?.modContentPack!=null && d.modContentPack.PackageId.ToLowerInvariant()==PackageId;
  // 会被「战役」组件挑中的事件：本模组自己的、正常有概率出现的（baseChance 0 的是 C# 专门触发的，不碰）。
  public static IEnumerable<IncidentDef> CampaignPool(bool allowBig)=>DefDatabase<IncidentDef>.AllDefs.Where(d=>IsLP(d) && d.baseChance>0f && d.category!=null && d.defName!="LP_DmFateRoll" &&
   (d.category==IncidentCategoryDefOf.Misc || d.category==IncidentCategoryDefOf.ThreatSmall || d.category.defName=="FactionArrival" || (allowBig && d.category==IncidentCategoryDefOf.ThreatBig)));
  public static IncidentParms Parms(IncidentDef d,IIncidentTarget t){var p=StorytellerUtility.DefaultParmsNow(d.category,t);p.forced=false;return p;}
  public static bool TryPick(IEnumerable<IncidentDef> pool,IIncidentTarget t,out IncidentDef def,out IncidentParms parms){
   var list=pool.Where(d=>d.TargetAllowed(t)).ToList();
   while(list.Count>0){
    var d=list.RandomElementByWeight(x=>System.Math.Max(0.05f,x.baseChance));list.Remove(d);
    var p=Parms(d,t);
    if(d.Worker.CanFireNow(p)){def=d;parms=p;return true;}
   }
   def=null;parms=null;return false;
  }
  public static bool TryFire(IEnumerable<IncidentDef> pool,Map map,out IncidentDef fired){
   fired=null;
   if(!TryPick(pool,map,out var d,out var p))return false;
   if(!d.Worker.TryExecute(p))return false;
   fired=d;return true;
  }
  public static IEnumerable<IncidentDef> Bad(bool big)=>DefDatabase<IncidentDef>.AllDefs.Where(d=>IsLP(d) && d.baseChance>0f && d.category==(big?IncidentCategoryDefOf.ThreatBig:IncidentCategoryDefOf.ThreatSmall));
  public static IEnumerable<IncidentDef> Good()=>GoodEvents.Select(n=>DefDatabase<IncidentDef>.GetNamedSilentFail(n)).Where(d=>d!=null);
  // 测试用：下一次掷骰的固定点数（0 = 随机）。
  public static int ForcedRoll;
 }

 public class StorytellerCompProperties_DmCampaign : StorytellerCompProperties {
  public float mtbDays=3f;
  public float bigThreatAfterDays=30f;
  public StorytellerCompProperties_DmCampaign(){compClass=typeof(StorytellerComp_DmCampaign);}
 }
 public class StorytellerComp_DmCampaign : StorytellerComp {
  StorytellerCompProperties_DmCampaign P=>(StorytellerCompProperties_DmCampaign)props;
  public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target){
   if(!(target is Map map) || !map.IsPlayerHome)yield break;
   if(!Rand.MTBEventOccurs(P.mtbDays,60000f,1000f))yield break;
   bool big=GenDate.DaysPassedSinceSettleFloat>=P.bigThreatAfterDays && Rand.Chance(0.15f);
   if(DungeonMaster.TryPick(DungeonMaster.CampaignPool(big).Where(d=>!big || d.category==IncidentCategoryDefOf.ThreatBig || Rand.Chance(0.5f)),target,out var def,out var parms))
    yield return new FiringIncident(def,this,parms);
  }
  public override string ToString()=>base.ToString()+" (DM campaign)";
 }

 public class StorytellerCompProperties_DmFateRoll : StorytellerCompProperties {
  public float periodDays=5f;
  public IncidentDef incident;
  public StorytellerCompProperties_DmFateRoll(){compClass=typeof(StorytellerComp_DmFateRoll);}
 }
 public class StorytellerComp_DmFateRoll : StorytellerComp {
  StorytellerCompProperties_DmFateRoll P=>(StorytellerCompProperties_DmFateRoll)props;
  public override IEnumerable<FiringIncident> MakeIntervalIncidents(IIncidentTarget target){
   if(P.incident==null || !(target is Map map) || !map.IsPlayerHome)yield break;
   // 多张家园地图时只在第一张掷骰。
   if(Find.Maps.FirstOrDefault(m=>m.IsPlayerHome)!=map)yield break;
   int period=System.Math.Max(1,(int)(P.periodDays*60)); // 以 1000 tick 为单位
   if((Find.TickManager.TicksGame/1000)%period!=0)yield break;
   var parms=StorytellerUtility.DefaultParmsNow(P.incident.category,target);
   yield return new FiringIncident(P.incident,this,parms);
  }
 }

 public class IncidentWorker_DmFateRoll : IncidentWorker {
  public static int LastRoll;public static string LastFired;
  protected override bool CanFireNowSub(IncidentParms parms)=>parms.target is Map m && m.mapPawns.FreeColonistsSpawnedCount>0;
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;
   int roll=DungeonMaster.ForcedRoll>0?DungeonMaster.ForcedRoll:Rand.RangeInclusive(1,20);DungeonMaster.ForcedRoll=0;
   LastRoll=roll;LastFired=null;
   string label,text;LetterDef ld;IncidentDef fired=null;var extra=new List<string>();
   var colonists=map.mapPawns.FreeColonistsSpawned.ToList();
   if(roll==1){
    label="命运之骰：1！大失败";ld=LetterDefOf.ThreatBig;
    text="屏风后传来一声闷响。塔莉亚低头看了看骰子，然后慢慢露出笑容。\n\n「一点。很遗憾，诸位。」";
    if(!DungeonMaster.TryFire(DungeonMaster.Bad(true),map,out fired))DungeonMaster.TryFire(DungeonMaster.Bad(false),map,out fired);
    if(fired==null){var raid=IncidentDefOf.RaidEnemy;var p=StorytellerUtility.DefaultParmsNow(raid.category,map);if(raid.Worker.CanFireNow(p) && raid.Worker.TryExecute(p))fired=raid;}
   }else if(roll<=5){
    label="命运之骰："+roll+"（厄运）";ld=LetterDefOf.NegativeEvent;
    text="骰子滚到桌角停下。塔莉亚翻了一页笔记：「运气不太好啊……路上遇到点麻烦吧。」";
    DungeonMaster.TryFire(DungeonMaster.Bad(false),map,out fired);
   }else if(roll<=15){
    // 平常点数只发一条消息，不占信件栏。
    Messages.Message("命运之骰掷出 "+roll+"：塔莉亚瞥了一眼骰子，继续往下讲故事。",MessageTypeDefOf.NeutralEvent,false);
    return true;
   }else if(roll<20){
    label="命运之骰："+roll+"（好运）";ld=LetterDefOf.PositiveEvent;
    text="骰子的数字闪着微光。塔莉亚点点头：「不错的检定。这一段旅程，诸神站在你们那边。」";
    DungeonMaster.TryFire(DungeonMaster.Good(),map,out fired);
    var who=colonists.Where(p=>!p.Inspired && !p.InMentalState).InRandomOrder().FirstOrDefault();
    if(who!=null && Inspire(who))extra.Add(who.LabelShort+" 受到了启发。");
   }else{
    label="命运之骰：20！大成功";ld=LetterDefOf.PositiveEvent;
    text="骰子落定的一刻，屏风后的烛火全都亮了一下。塔莉亚拍桌而起：「自然二十！」";
    DungeonMaster.TryFire(DungeonMaster.Good(),map,out fired);
    int n=0;foreach(var p in colonists.Where(p=>!p.Inspired && !p.InMentalState).InRandomOrder().Take(3))if(Inspire(p)){n++;extra.Add(p.LabelShort+" 受到了启发。");}
    float xp=0f;foreach(var p in colonists){xp=System.Math.Max(xp,Adventurer.GainXP(p,300f));}
    if(xp>0f)extra.Add("所有冒险者获得了经验（每人约 "+(int)xp+" 点）。");
    var defName=IsekaiLoot.MagicLoot[Rand.Range(0,IsekaiLoot.MagicLoot.Length)];var td=DefDatabase<ThingDef>.GetNamedSilentFail(defName);
    if(td!=null){var item=ThingMaker.MakeThing(td,GenStuff.DefaultStuffFor(td));var cell=DropCellFinder.TradeDropSpot(map);DropPodUtility.DropThingsNear(cell,map,new[]{item},110,false,false,true);extra.Add("一件魔法物品从天而降："+item.LabelCap+"。");}
   }
   if(fired!=null){LastFired=fired.defName;text+="\n\n接下来："+fired.LabelCap+"。";}
   if(extra.Count>0)text+="\n\n"+string.Join("\n",extra);
   Find.LetterStack.ReceiveLetter(label,text,ld,new LookTargets(colonists.FirstOrDefault()));
   return true;
  }
  static bool Inspire(Pawn p){
   var h=p.mindState?.inspirationHandler;if(h==null)return false;
   var d=h.GetRandomAvailableInspirationDef();
   return d!=null && h.TryStartInspiration(d,"命运之骰",false);
  }
 }
}
