using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.75.0 非战斗经验：治疗与手术、交易、建筑完工、研究完成、重伤倒地生还。每项一个开关（默认开），共用“非战斗经验倍率”（0～3，默认 1）。
 // 全部走 Adventurer.GainXP（再乘冒险者经验倍率、凡人之志，升级信件照常）；只给殖民地的人（玩家阵营或奴隶）且有 LP_Adventurer。
 // 每日上限按倍率之前的基础值算（倍率 ×2 时上限对应的实得经验也 ×2）；本地日换日清零，计数存在冒险者 hediff 上。
 public static class AdventurerExtraXp {
  public const int Heal=0,Trade=1,Build=2,Research=3,Survive=4,Count=5;
  public const float SurgeryXp=30f,TendXp=15f,BleedHours=6f,TradePer=100f,BuildPer=1000f,BuildMin=2f,ResearchShare=.05f,SurviveBase=50f,SurvivePower=.5f;
  public const int SurviveCooldown=180000; // 3 天
  public static readonly float[] Cap={150f,100f,100f,0f,0f}; // 0 = 不封顶
  public static readonly string[] Names={"治疗与手术","交易","建筑完工","研究完成","重伤倒地生还"};
  public static float Mult=>LichMod.Settings?.adventurerNonCombatXp??1f;
  public static bool On(int src){
   var s=LichMod.Settings;if(s==null)return true;
   switch(src){case Heal:return s.adventurerXpHeal;case Trade:return s.adventurerXpTrade;case Build:return s.adventurerXpBuild;case Research:return s.adventurerXpResearch;default:return s.adventurerXpSurvive;}
  }
  static Hediff_Adventurer Target(Pawn p,int src){
   if(!Adventurer.Enabled || !On(src) || Mult<=0f || !AdventurerWork.Counts(p))return null;
   return Adventurer.Get(p);
  }
  static void RefreshDay(Hediff_Adventurer h){int d=Feats.LocalDay(h.pawn);if(d!=h.exDay){h.exDay=d;for(int i=0;i<Count;i++)h.exToday[i]=0f;}}
  // 今天已计入的基础值（换日后为 0）。
  public static float Today(Hediff_Adventurer h,int src)=>h.exDay==Feats.LocalDay(h.pawn)?h.exToday[src]:0f;
  // 基础值 amount 按今日剩余额度截断，乘非战斗倍率后走 GainXP；返回实际入账的冒险者经验（GainXP 的返回值）。
  public static float Award(Pawn p,int src,float amount){
   if(amount<=0f)return 0f;var h=Target(p,src);if(h==null)return 0f;
   RefreshDay(h);
   if(Cap[src]>0f){amount=Mathf.Min(amount,Cap[src]-h.exToday[src]);if(amount<=0f)return 0f;}
   h.exToday[src]+=amount;h.exTotal[src]+=amount;
   return Adventurer.GainXP(p,amount*Mult);
  }

  // 1. 治疗与手术
  public static float Surgery(Pawn surgeon,Pawn patient,bool failed)=>failed || surgeon==null?0f:Award(surgeon,Heal,SurgeryXp);
  public static bool Critical(Pawn patient)=>patient!=null && !patient.Dead && patient.health.hediffSet.BleedRateTotal>0f && HealthUtility.TicksUntilDeathDueToBloodLoss(patient)<BleedHours*GenDate.TicksPerHour;
  public static float Tended(Pawn doctor,bool critical)=>critical?Award(doctor,Heal,TendXp):0f;
  // 2. 交易：双方货物（不含银币）的成交总价，每 100 银 1 点。
  public static float DealValue(TradeDeal deal){
   float v=0f;if(deal==null)return 0f;
   foreach(var t in deal.AllTradeables){
    if(t==null || t.IsCurrency)continue;int n=t.CountToTransfer;if(n==0)continue;
    v+=Mathf.Abs(n)*t.GetPriceFor(t.ActionToDo);
   }
   return v;
  }
  public static float Traded(Pawn negotiator,float value)=>Award(negotiator,Trade,Mathf.Abs(value)/TradePer);
  // 3. 建筑完工：工作量 / 1000，至少 2。地板（地形框架）和零工作量的不算。
  public static float BuildBase(float work)=>work<=0f?0f:Mathf.Max(BuildMin,work/BuildPer);
  public static float Built(Pawn worker,float work)=>Award(worker,Build,BuildBase(work));
  // 4. 研究完成：按贡献比例分 研究费用 × 5%。
  public static int ResearchFinished(ResearchProjectDef proj){
   var st=ResearchShares.State;if(st==null || proj==null)return 0;
   var mine=st.shares.Where(e=>e.proj==proj).ToList();st.shares.RemoveAll(e=>e.proj==proj);
   float total=mine.Sum(e=>e.amount);if(total<=0f)return 0;
   int n=0;float pool=proj.Cost*ResearchShare;
   foreach(var e in mine)if(e.pawn!=null && e.amount>0f && Award(e.pawn,Research,pool*e.amount/total)>0f)n++;
   return n;
  }
  // 5. 重伤倒地生还：被敌对的人（不是自己）打倒时记下对方的战斗力；站起来、还活着时 +50 + 0.5 × 战斗力，每人 3 天最多一次。
  public static void Downed(Pawn p,DamageInfo? dinfo){
   var h=Adventurer.Get(p);if(h==null)return;
   var by=dinfo?.Instigator as Pawn;
   h.exDownPower=by!=null && by!=p && by.HostileTo(p)?Mathf.Max(0f,by.kindDef?.combatPower??0f):-1f;
  }
  public static float Undowned(Pawn p){
   var h=Adventurer.Get(p);if(h==null || h.exDownPower<0f)return 0f;
   float power=h.exDownPower;h.exDownPower=-1f;
   if(p.Dead || p.Downed)return 0f;
   int now=Find.TickManager.TicksGame;if(now-h.exSurviveTick<SurviveCooldown || Target(p,Survive)==null)return 0f;
   float got=Award(p,Survive,SurviveBase+SurvivePower*power);if(got>0f)h.exSurviveTick=now;
   return got;
  }

  // 悬停经验条的说明行。
  public static void Tip(System.Text.StringBuilder sb,Hediff_Adventurer h,float mult){
   float m=Mult;string Off(int i)=>"· "+Names[i]+"：已关闭";
   string Day(int i)=>"今日 "+Today(h,i).ToString("0.#")+" / "+Cap[i].ToString("0")+"；累计 "+(h.exTotal[i]*m*mult).ToString("0");
   string x(float v)=>(v*m*mult).ToString("0.##");
   bool all=m>0f;
   sb.AppendLine(!all || !On(Heal)?Off(Heal):"· 治疗与手术：手术成功 +"+x(SurgeryXp)+"，救治 "+BleedHours.ToString("0")+" 小时内会失血而死的病人 +"+x(TendXp)+"（"+Day(Heal)+"）");
   sb.AppendLine(!all || !On(Trade)?Off(Trade):"· 交易：谈判者按成交总价（买卖双向）每 "+TradePer.ToString("0")+" 银 +"+x(1f)+"（"+Day(Trade)+"）");
   sb.AppendLine(!all || !On(Build)?Off(Build):"· 建筑完工：工作量 ÷ "+BuildPer.ToString("0")+"，至少 "+BuildMin.ToString("0")+"，× "+x(1f)+"（地板不算；"+Day(Build)+"）");
   sb.AppendLine(!all || !On(Research)?Off(Research):"· 研究完成：研究费用 × "+(ResearchShare*100f).ToString("0")+"% × "+x(1f)+"，按出力比例分给参与的人（不封顶；累计 "+(h.exTotal[Research]*m*mult).ToString("0")+"）");
   int left=h.exSurviveTick<0?0:SurviveCooldown-(Find.TickManager.TicksGame-h.exSurviveTick);
   sb.AppendLine(!all || !On(Survive)?Off(Survive):"· 重伤倒地生还：被敌人打倒后站起来 +"+x(SurviveBase)+" + 敌人战斗力 × "+x(SurvivePower)+"（每 3 天一次"+(left>0?"，还要 "+(left/(float)GenDate.TicksPerDay).ToString("0.#")+" 天":"")+"；累计 "+(h.exTotal[Survive]*m*mult).ToString("0")+"）");
  }

  public static void Expose(Hediff_Adventurer h){
   var td=h.exToday?.ToList();Scribe_Collections.Look(ref td,"lpExXpToday",LookMode.Value);
   var tt=h.exTotal?.ToList();Scribe_Collections.Look(ref tt,"lpExXpTotal",LookMode.Value);
   Scribe_Values.Look(ref h.exDay,"lpExXpDay",-1);Scribe_Values.Look(ref h.exDownPower,"lpExDownPower",-1f);Scribe_Values.Look(ref h.exSurviveTick,"lpExSurviveTick",-9999999);
   if(Scribe.mode==LoadSaveMode.LoadingVars){h.exToday=td!=null && td.Count==Count?td.ToArray():new float[Count];h.exTotal=tt!=null && tt.Count==Count?tt.ToArray():new float[Count];}
  }
 }

 // 研究出力：每个项目每个人累计的研究量（存档）。项目完成时结算并清掉。
 public class ResearchShare : IExposable {
  public ResearchProjectDef proj;public Pawn pawn;public float amount;
  public void ExposeData(){Scribe_Defs.Look(ref proj,"proj");Scribe_References.Look(ref pawn,"pawn");Scribe_Values.Look(ref amount,"amount");}
 }
 public class ResearchShares : GameComponent {
  public List<ResearchShare> shares=new List<ResearchShare>();
  public ResearchShares(Game game){}
  public static ResearchShares State=>GameComp<ResearchShares>.Get();
  public void Add(ResearchProjectDef proj,Pawn p,float amount){
   if(proj==null || p==null || amount<=0f)return;
   for(int i=0;i<shares.Count;i++){var e=shares[i];if(e.proj==proj && e.pawn==p){e.amount+=amount;return;}}
   shares.Add(new ResearchShare{proj=proj,pawn=p,amount=amount});
  }
  public float Of(ResearchProjectDef proj,Pawn p)=>shares.FirstOrDefault(e=>e.proj==proj && e.pawn==p)?.amount??0f;
  public override void ExposeData(){
   base.ExposeData();Scribe_Collections.Look(ref shares,"lpResearchShares",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(shares==null)shares=new List<ResearchShare>();shares.RemoveAll(e=>e==null || e.proj==null || e.pawn==null || e.proj.IsFinished);}
  }
 }

 // ---------- Harmony ----------
 [HarmonyPatch(typeof(Recipe_Surgery),"CheckSurgeryFail")]
 public static class ExtraXpSurgery {
  static void Postfix(Pawn surgeon,Pawn patient,bool __result){try{AdventurerExtraXp.Surgery(surgeon,patient,__result);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 手术经验："+e,0x1A75001);}}
 }
 [HarmonyPatch(typeof(TendUtility),nameof(TendUtility.DoTend))]
 public static class ExtraXpTend {
  static void Prefix(Pawn doctor,Pawn patient,out bool __state){__state=doctor!=null && AdventurerExtraXp.On(AdventurerExtraXp.Heal) && AdventurerWork.Counts(doctor) && AdventurerExtraXp.Critical(patient);}
  static void Postfix(Pawn doctor,bool __state){if(__state)AdventurerExtraXp.Tended(doctor,true);}
 }
 [HarmonyPatch(typeof(TradeDeal),nameof(TradeDeal.TryExecute))]
 public static class ExtraXpTrade {
  static void Prefix(TradeDeal __instance,out float __state){
   __state=0f;try{if(!TradeSession.giftMode && AdventurerExtraXp.On(AdventurerExtraXp.Trade) && AdventurerWork.Counts(TradeSession.playerNegotiator))__state=AdventurerExtraXp.DealValue(__instance);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 交易经验："+e,0x1A75002);}
  }
  static void Postfix(bool __result,bool actuallyTraded,float __state){if(__result && actuallyTraded && __state>0f)AdventurerExtraXp.Traded(TradeSession.playerNegotiator,__state);}
 }
 [HarmonyPatch(typeof(Frame),nameof(Frame.CompleteConstruction))]
 public static class ExtraXpBuild {
  static void Prefix(Frame __instance,Pawn worker,out float __state){
   __state=0f;if(worker==null || __instance.def?.entityDefToBuild is TerrainDef || !AdventurerExtraXp.On(AdventurerExtraXp.Build) || !AdventurerWork.Counts(worker))return;
   try{__state=__instance.WorkToBuild;}catch{}
  }
  static void Postfix(Pawn worker,float __state){if(__state>0f)AdventurerExtraXp.Built(worker,__state);}
 }
 [HarmonyPatch(typeof(ResearchManager),nameof(ResearchManager.ResearchPerformed))]
 public static class ExtraXpResearchPerformed {
  static void Prefix(ResearchManager __instance,float amount,Pawn researcher){
   if(amount<=0f || researcher==null || !AdventurerWork.Counts(researcher))return;
   var proj=__instance.GetProject(null);if(proj!=null && !proj.IsFinished)ResearchShares.State?.Add(proj,researcher,amount);
  }
 }
 [HarmonyPatch(typeof(ResearchManager),nameof(ResearchManager.FinishProject))]
 public static class ExtraXpResearchFinish {
  static void Prefix(ResearchProjectDef proj,out bool __state){__state=proj!=null && !proj.IsFinished;}
  static void Postfix(ResearchProjectDef proj,bool __state){if(__state && proj.IsFinished)try{AdventurerExtraXp.ResearchFinished(proj);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 研究经验："+e,0x1A75003);}}
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class ExtraXpDowned {
  static void Postfix(Pawn ___pawn,DamageInfo? dinfo){if(___pawn!=null && ___pawn.Downed)AdventurerExtraXp.Downed(___pawn,dinfo);}
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeUndowned")]
 public static class ExtraXpUndowned {
  static void Postfix(Pawn ___pawn){if(___pawn!=null)AdventurerExtraXp.Undowned(___pawn);}
 }

 public partial class Settings {
  public bool adventurerXpHeal=true,adventurerXpTrade=true,adventurerXpBuild=true,adventurerXpResearch=true,adventurerXpSurvive=true;public float adventurerNonCombatXp=1f;
  void ExposeExtraXp(){
   Scribe_Values.Look(ref adventurerXpHeal,"adventurerXpHeal",true);Scribe_Values.Look(ref adventurerXpTrade,"adventurerXpTrade",true);Scribe_Values.Look(ref adventurerXpBuild,"adventurerXpBuild",true);
   Scribe_Values.Look(ref adventurerXpResearch,"adventurerXpResearch",true);Scribe_Values.Look(ref adventurerXpSurvive,"adventurerXpSurvive",true);
   Scribe_Values.Look(ref adventurerNonCombatXp,"adventurerNonCombatXp",1f);adventurerNonCombatXp=Mathf.Clamp(Mathf.Round(adventurerNonCombatXp*10f)/10f,0f,3f);
  }
  public static void DrawExtraXp(Listing_Standard l){
   var s=LichMod.Settings;
   TooltipHandler.TipRegion(l.Label("非战斗经验倍率："+(s.adventurerNonCombatXp<=0f?"关闭":"×"+s.adventurerNonCombatXp.ToString("0.0"))),"治疗与手术、交易、建筑完工、研究完成、重伤倒地生还这五项的冒险者经验都乘这个倍率（之后再乘冒险者经验倍率）。每日上限按倍率之前算。0 = 五项全关。");
   s.adventurerNonCombatXp=Mathf.Round(l.Slider(s.adventurerNonCombatXp,0f,3f)*10f)/10f;
   l.CheckboxLabeled("经验：治疗与手术",ref s.adventurerXpHeal,"手术成功（不是失败）主刀医生 +30；救治 6 小时内会失血而死的病人 +15。每人每天最多 150。");
   l.CheckboxLabeled("经验：交易",ref s.adventurerXpTrade,"每完成一笔交易，谈判者按成交总价（买和卖都算，不含银币本身）每 100 银 +1。每人每天最多 100。赠礼不算。");
   l.CheckboxLabeled("经验：建筑完工",ref s.adventurerXpBuild,"建好一个建筑，完工的人得到 工作量 ÷ 1000（至少 2）。地板不算。每人每天最多 100。");
   l.CheckboxLabeled("经验：研究完成",ref s.adventurerXpResearch,"研究项目完成时，研究费用 × 5% 按每人出的研究量比例分给参与过的人（还活着、还在殖民地）。不封顶。");
   l.CheckboxLabeled("经验：重伤倒地生还",ref s.adventurerXpSurvive,"被敌人打倒在地、之后活着站起来：+50 + 打倒你的敌人战斗力 × 0.5。每人每 3 天最多一次；自己弄伤、友军误伤不算。");
  }
 }
}
