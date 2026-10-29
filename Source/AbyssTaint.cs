using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.54.0 深渊腐化（规格第二节）：每个人形一条 LP_AbyssalTaint，severity 0～1。四个阶段的属性写在 XML 的 stages 里；
 // 这里管来源（乘设置的腐化速度和种族系数表 LP_TaintFactors）、每小时结算（自然消退、结界睡眠、深渊狂乱）、化魔和各处去除钩子。
 public class TaintFactor {public string race;public float factor=1f;public bool mood=true;}
 public class LP_TaintFactorDef : Def {public List<TaintFactor> factors=new List<TaintFactor>();}

 public static class AbyssTaint {
  public const float Whisper=.1f,Shadow=.3f,Mark=.6f,Fallen=.9f,DailyDecay=.005f,TurnReset=.6f,AasimarLeaderMin=.5f;
  public const float PrayerCleanse=.05f,BlessedWaterCleanse=.1f,HealingCleanse=.08f,WardSleepDaily=.01f,FrenzyPerSeason=.1f,ShadowFavor=.5f;
  public const float BloodGain=.08f,MortgageGain=.3f,RefineGain=.05f,ReadHourly=.005f,DreamGain=.03f,QuasitDaily=.003f,FangDaily=.002f;
  public static readonly float[] RitualGain={.05f,.1f,.2f,.35f};
  static HediffDef def,favored,tainted;static LP_TaintFactorDef table;static MentalStateDef frenzy;
  public static HediffDef Def=>def??(def=Items.Hediff("LP_AbyssalTaint"));
  public static HediffDef FavoredDef=>favored??(favored=Items.Hediff("LP_AbyssFavored"));
  public static HediffDef TaintedPhylacteryDef=>tainted??(tainted=Items.Hediff("LP_TaintedPhylactery"));
  public static LP_TaintFactorDef Table=>table??(table=DefDatabase<LP_TaintFactorDef>.GetNamedSilentFail("LP_TaintFactors"));
  public static MentalStateDef Frenzy=>frenzy??(frenzy=DefDatabase<MentalStateDef>.GetNamedSilentFail("LP_AbyssFrenzy"));
  public static Hediff_AbyssalTaint Get(Pawn p){var list=p?.health?.hediffSet?.hediffs;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i] is Hediff_AbyssalTaint h)return h;return null;}
  public static float Severity(Pawn p)=>Get(p)?.Severity??0f;
  // 0 无（<10%）、1 低语、2 阴影、3 恶魔之印、4 堕落。
  public static int Stage(float s)=>s>=Fallen?4:s>=Mark?3:s>=Shadow?2:s>=Whisper?1:0;
  public static int Stage(Pawn p)=>Stage(Severity(p));
  public static bool Eligible(Pawn p)=>p?.RaceProps!=null && p.RaceProps.Humanlike && !p.RaceProps.IsMechanoid && !p.Dead && p.health!=null && Necro.Construct(p)==null;

  // ---------- 种族系数（XML 表，按顺序取第一条匹配的） ----------
  public static bool RaceIs(Pawn p,string key){
   switch(key){
    case "Marilith":return Marilith.Is(p);
    case "Tiefling":return FaithRaces.IsTiefling(p);
    case "Aasimar":return FaithRaces.IsAasimar(p);
    case "Cambion":return AbyssRaces.IsCambion(p);
    case "Succubus":return AbyssRaces.IsSuccubus(p);
    case "HalfIllithid":return HalfIllithid.Is(p);
   }
   return Items.Is(p,key);
  }
  static TaintFactor Entry(Pawn p){var t=Table;if(t==null || p==null)return null;foreach(var f in t.factors)if(RaceIs(p,f.race))return f;return null;}
  public static float RaceFactor(Pawn p){
   if(!Eligible(p))return 0f;
   if(Outsiders.Of(p)?.taintImmune==true)return 0f;
   return Entry(p)?.factor??1f;
  }
  public static bool MoodImmune(Pawn p)=>Entry(p)?.mood==false;

  // 加腐化：正数乘设置的腐化速度和种族系数；负数（去除）原样。返回实际变化量。到 100% 化魔。
  public static float Add(Pawn p,float amount,string reason=null){
   if(p==null || amount==0f || Def==null || !Eligible(p))return 0f;
   var h=Get(p);
   if(amount>0f){amount*=AbyssSettings.TaintSpeed*RaceFactor(p)*PactRaces.TaintFactor(p);if(amount<=0f)return 0f;} // 1.55.0：天界羽披 ×0.5
   else if(h==null)return 0f;
   if(h==null){h=(Hediff_AbyssalTaint)HediffMaker.MakeHediff(Def,p);h.Severity=0.0001f;p.health.AddHediff(h);h=Get(p);if(h==null)return 0f;}
   float before=h.Severity,after=Mathf.Clamp(before+amount,0f,1f);
   if(h.Locked && after<before)after=before; // 化魔后停在 100%（深渊宠儿 / 命匣受污），去除也不降
   h.Severity=after;
   int s0=Stage(before),s1=Stage(after);
   if(s0!=s1){if(p.Spawned)p.Drawer?.renderer?.SetAllGraphicsDirty();if(s1>s0 && s1>=2 && p.Faction?.IsPlayer==true)Messages.Message(p.LabelShort+"的深渊腐化加深了："+StageLabel(s1)+"。",p,MessageTypeDefOf.NegativeEvent,false);}
   if(after>=1f && before<1f)Turn(p);
   if(h.Severity<=0.0001f && !h.Locked)p.health.RemoveHediff(h);
   return after-before;
  }
  public static float Cleanse(Pawn p,float amount)=>-Add(p,-Mathf.Abs(amount));
  public static string StageLabel(int s){switch(s){case 1:return "低语";case 2:return "阴影";case 3:return "恶魔之印";case 4:return "堕落";}return "无";}

  // ---------- 每小时（hediff 间隔 tick 2500） ----------
  public static void Hourly(Pawn p,Hediff_AbyssalTaint h){
   if(p==null || p.Dead)return;
   float s=h.Severity;
   if(s<Mark && !h.Locked)Add(p,-DailyDecay/24f);
   if(p.Spawned && p.InBed()){var reg=MapComponent_Outsiders.Of(p.Map);if(reg!=null && reg.glyphs.Count>0 && reg.Warded(p.Position))Add(p,-WardSleepDaily/24f);}
   if(Stage(p)>=3 && p.Spawned && !p.Downed && !p.InMentalState && Frenzy!=null && Rand.Chance(1f-Mathf.Pow(1f-FrenzyPerSeason,1f/(15f*24f))))
    p.mindState.mentalStateHandler.TryStartMentalState(Frenzy,"深渊腐化",forced:true,forceWake:true);
   if(FaithRaces.IsAasimar(p) && Stage(p)>=2)foreach(var g in p.genes.GenesListForReading.OfType<Gene_RadiantConsumption>())g.on=false;
  }
  // 阿斯莫到“阴影”：天界启示（启示基因带的能力）不可用。
  static HashSet<AbilityDef> revelation;
  public static bool RevelationBlocked(Pawn p,AbilityDef a){
   if(a==null || !FaithRaces.IsAasimar(p) || Stage(p)<2)return false;
   if(revelation==null)revelation=new HashSet<AbilityDef>(FaithRaces.Revelations.Select(n=>DefDatabase<GeneDef>.GetNamedSilentFail(n)).Where(g=>g?.abilities!=null).SelectMany(g=>g.abilities));
   return revelation.Contains(a);
  }

  // ---------- 化魔（规格 2.2） ----------
  public static string Turn(Pawn p){
   var h=Get(p);if(h==null)return null;
   if(Items.IsLich(p)){Give(p,TaintedPhylacteryDef);h.Locked=true;h.Severity=1f;Letter(p,"命匣受污",p.LabelShort+"的灵魂被深渊浸透，连命匣也染上了黑色：复活所需的时间延长一半。");return "lich";}
   if(AbyssRaces.IsCambion(p) || FaithRaces.IsTiefling(p) || AbyssRaces.IsSuccubus(p) || FaithRaces.CannotConvert(p)!=null || AbyssRaces.CambionXeno==null){
    Give(p,FavoredDef);h.Locked=true;h.Severity=1f;Letter(p,"深渊宠儿",p.LabelShort+"彻底拥抱了深渊：成了“深渊宠儿”，伤害+10%。阿斯莫看见它会不舒服。");return "favored";
   }
   AbyssRaces.MakeCambion(p);
   h=Get(p);if(h!=null)h.Severity=TurnReset;
   Letter(p,"化魔",p.LabelShort+"的身体终于向深渊屈服了：皮肤下透出暗红，额头顶出一对角——"+p.LabelShort+"成了坎比恩。腐化回落到 60%，但这一步无法逆转。");
   return "cambion";
  }
  static void Give(Pawn p,HediffDef d){if(d!=null && !Items.Has(p,d))p.health.AddHediff(d);}
  static void Letter(Pawn p,string title,string text){if(p.Faction?.IsPlayer==true)Find.LetterStack.ReceiveLetter(title+"："+p.LabelShort,text,LetterDefOf.NegativeEvent,p);}

  // ---------- 每天：殖民地每有一个“恶魔之印”以上的人，费伦拓荒同盟好感 -1 ----------
  public static int DailyGoodwill(){
   var f=Factions.Colony;if(f==null || f.HostileTo(Faction.OfPlayer))return 0;
   int n=0;foreach(var m in Find.Maps)if(m.IsPlayerHome)foreach(var p in m.mapPawns.FreeColonistsSpawned)if(Stage(p)>=3)n++;
   if(n>0)Factions.ChangeGoodwillExact(f,-n,"殖民地里有身带恶魔之印的人");
   return n;
  }
  // 命匣受污：复活时间 ×1.5。
  public static void ExtendRevival(Phylactery box){
   if(box?.soul==null || box.revivalTick<0 || !Items.Has(box.soul,TaintedPhylacteryDef))return;
   if(box.revivalTick-Find.TickManager.TicksGame==Phylactery.RevivalDelay)box.revivalTick+=Phylactery.RevivalDelay/2;
  }
 }

 public class Hediff_AbyssalTaint : HediffWithComps {
  int acc;public bool Locked;
  public override bool Visible=>Severity>=AbyssTaint.Whisper;
  public override string SeverityLabel=>Severity.ToStringPercent("0");
  public override bool ShouldRemove=>!Locked && Severity<=0.0001f;
  public override void TickInterval(int delta){base.TickInterval(delta);if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;AbyssTaint.Hourly(pawn,this);}
  public override string TipStringExtra{get{
   var s=base.TipStringExtra;var lines=new List<string>();if(!s.NullOrEmpty())lines.Add(s);
   lines.Add("阶段："+AbyssTaint.StageLabel(AbyssTaint.Stage(Severity))+"。种族系数 ×"+AbyssTaint.RaceFactor(pawn).ToString("0.##"));
   lines.Add(Severity<AbyssTaint.Mark?"每天自然消退 0.5%。":"到了“恶魔之印”以后不再自然消退。");
   lines.Add("去除：向善神祈祷 -5%、喝祝圣圣水 -10%、阿斯莫的治疗之手 -8%、睡在驱魔结界里每天 -1%。");
   return string.Join("\n",lines);
  }}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref acc,"lpAcc");Scribe_Values.Look(ref Locked,"lpLocked");}
 }
 // 恶魔之印起的小角和红眼（挂在腐化状态上的头部附件，阶段不够或变形伪装时不画）。
 public class PawnRenderNodeWorker_TaintMark : PawnRenderNodeWorker_FlipWhenCrawling {
  public override bool CanDrawNow(PawnRenderNode node,PawnDrawParms parms){
   if(!base.CanDrawNow(node,parms))return false;var p=parms.pawn;
   if(AbyssRaces.Guised(p) || AbyssRaces.IsCambion(p) || AbyssRaces.IsSuccubus(p) || FaithRaces.IsTiefling(p))return false;
   return AbyssTaint.Severity(p)>=AbyssTaint.Mark;
  }
 }
 // 喝下：腐化变化（恶魔血 +8%）。
 public class IngestionOutcomeDoer_AbyssTaint : IngestionOutcomeDoer {
  public float offset;
  protected override void DoIngestionOutcomeSpecial(Pawn pawn,Thing ingested,int ingestedCount){AbyssTaint.Add(pawn,offset,ingested?.LabelNoCount);}
 }

 // ---------- 心情 ----------
 public class ThoughtWorker_AbyssWhisper : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>AbyssTaint.Severity(p)>=AbyssTaint.Whisper && !AbyssTaint.MoodImmune(p);
 }
 // 阿斯莫看到同地图上的深渊宠儿。
 public class ThoughtWorker_AasimarSeesFavored : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!p.Spawned || !FaithRaces.IsAasimar(p) || AbyssTaint.FavoredDef==null)return false;
   foreach(var o in p.Map.mapPawns.FreeColonistsSpawned)if(o!=p && Items.Has(o,AbyssTaint.FavoredDef))return true;
   return false;
  }
 }

 // ---------- Harmony：去除钩子 ----------
 [HarmonyPatch(typeof(Faith),nameof(Faith.PrayerDone))]
 public static class TaintPrayerCleanse {static void Postfix(Pawn p){if(p!=null && !p.Dead && p.Faction?.IsPlayer==true && FaithUtility.Patron?.Good==true)AbyssTaint.Cleanse(p,AbyssTaint.PrayerCleanse);}}
 // “阴影”以上：祈祷给善神的神眷 ×0.5。
 [HarmonyPatch(typeof(Faith),nameof(Faith.PrayerReward))]
 public static class TaintPrayerScope {
  [ThreadStatic] public static Pawn praying;
  static void Prefix(Pawn p,out Pawn __state){__state=praying;praying=p;}
  static void Finalizer(Pawn __state){praying=__state;}
 }
 [HarmonyPatch(typeof(Faith),nameof(Faith.Gain))]
 public static class TaintFavorFactor {
  static void Prefix(LP_DeityDef god,ref float amount){var p=TaintPrayerScope.praying;if(p!=null && amount>0f && god!=null && god.Good && AbyssTaint.Stage(p)>=2)amount*=AbyssTaint.ShadowFavor;}
 }
 [HarmonyPatch(typeof(CompUseEffect_BlessedWater),nameof(CompUseEffect_BlessedWater.DoEffect))]
 public static class TaintBlessedWater {static void Postfix(Pawn usedBy){AbyssTaint.Cleanse(usedBy,AbyssTaint.BlessedWaterCleanse);}}
 [HarmonyPatch(typeof(FaithRaces),nameof(FaithRaces.HealingHands))]
 public static class TaintHealingHands {static void Postfix(Pawn t){if(t!=null)AbyssTaint.Cleanse(t,AbyssTaint.HealingCleanse);}}
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.GizmoDisabled))]
 public static class TaintRevelationBlocked {
  static void Postfix(RimWorld.Ability __instance,ref string reason,ref bool __result){if(!__result && AbyssTaint.RevelationBlocked(__instance.pawn,__instance.def)){reason="深渊腐化遮住了天界的光（“阴影”阶段以上不能用天界启示）。";__result=true;}}
 }
 [HarmonyPatch(typeof(Phylactery),nameof(Phylactery.ScheduleRevival))]
 public static class TaintedPhylacteryDelay {static void Postfix(Phylactery __instance){AbyssTaint.ExtendRevival(__instance);}}
}
