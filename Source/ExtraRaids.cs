using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.118.0 热修 · 额外袭击（Peter）：不管用哪个说书人，每隔 N 天额外来一次普通敌对袭击（原版 RaidEnemy，派系和来法由原版挑，DND 派系也在里面）。
 // 点数 = 当前威胁点数 × 倍率。第 15 天以前不来；没能生成（比如没有敌对派系）就 1 小时后再试。默认关闭，不影响说书人自己的袭击。
 public static class ExtraRaids {
  public const int MinDays=1,MaxDays=60,RetryTicks=GenDate.TicksPerHour;
  public static bool On=>LichMod.Settings?.extraRaids??false;
  public static int Days=>Math.Max(MinDays,LichMod.Settings?.extraRaidDays??10);
  public static float PointsFactor=>(LichMod.Settings?.extraRaidPoints??100)/100f;
  public static bool Fire(Map map){
   if(map==null)return false;
   var parms=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig,map);
   parms.points*=PointsFactor;parms.forced=true;
   return IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
  }
  public static void Tick(ExtraRaidState s){
   int now=Find.TickManager.TicksGame;
   if(!On){s.next=-1;return;}
   if(s.next<0 || s.next>now+Days*GenDate.TicksPerDay)s.next=now+Days*GenDate.TicksPerDay; // 刚打开，或把间隔调短了
   if(now<s.next || GenDate.DaysPassed<Events3.EarliestDay)return;
   var map=Events3.Home;
   s.next=Fire(map)?now+Days*GenDate.TicksPerDay:now+RetryTicks;
  }
 }
 public class ExtraRaidState : GameComponent {
  public int next=-1;
  public ExtraRaidState(Game game){}
  public override void GameComponentTick(){if(Find.TickManager.TicksGame%250==211)ExtraRaids.Tick(this);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref next,"lpExtraRaidNext",-1);}
 }
 public partial class Settings {
  public bool extraRaids=false;public int extraRaidDays=10,extraRaidPoints=100;
  void ExposeExtraRaids(){
   Scribe_Values.Look(ref extraRaids,"lpExtraRaids",false);Scribe_Values.Look(ref extraRaidDays,"lpExtraRaidDays",10);Scribe_Values.Look(ref extraRaidPoints,"lpExtraRaidPoints",100);
   extraRaidDays=Math.Max(ExtraRaids.MinDays,Math.Min(ExtraRaids.MaxDays,extraRaidDays));extraRaidPoints=Math.Max(25,Math.Min(300,extraRaidPoints));
  }
 }
 public static class ExtraRaidSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.CheckboxLabeled("额外袭击",ref s.extraRaids,"不管用哪个说书人，每隔下面设的天数额外来一次敌对袭击（派系和来法随机，DND 派系也在里面）。第 15 天以前不来。不影响说书人自己安排的袭击。");
   if(!s.extraRaids)return;
   l.Label("额外袭击间隔："+s.extraRaidDays+" 天");s.extraRaidDays=(int)l.Slider(s.extraRaidDays,ExtraRaids.MinDays,ExtraRaids.MaxDays);
   l.Label("额外袭击强度："+s.extraRaidPoints+"%（乘当前威胁点数）");s.extraRaidPoints=(int)l.Slider(s.extraRaidPoints,25,300);
  }
 }
}
