using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.16.0 常见种族：半兽人、精灵、矮人。外貌与大部分能力来自原版基因，这里只有少量被动机制。
 public static class CommonRaces {
  public const float SavageChance=.1f,SavageFactor=1.5f,TrollBaneFactor=1.1f,ConsciousnessFloor=.31f;
  static GeneDef savage,stature;static HediffDef endurance;
  public static GeneDef SavageGene=>savage??(savage=DefDatabase<GeneDef>.GetNamedSilentFail("LP_HalfOrcSavage"));
  public static GeneDef StatureGene=>stature??(stature=DefDatabase<GeneDef>.GetNamedSilentFail("LP_DwarfStature"));
  public static HediffDef Endurance=>endurance??(endurance=DefDatabase<HediffDef>.GetNamedSilentFail("LP_RelentlessEndurance"));
  public static bool Has(Pawn p,GeneDef gene)=>gene!=null && p?.genes?.HasActiveGene(gene)==true;
  public static bool IsTroll(Pawn p)=>Troll.Is(p);
  // 1.20.0 普查与血债：异种型或标志基因任一满足即可（自定义异种型也算）。
  static GeneDef relentless;
  public static GeneDef RelentlessGene=>relentless??(relentless=DefDatabase<GeneDef>.GetNamedSilentFail("LP_HalfOrcRelentless"));
  public static bool IsHalfOrc(Pawn p)=>p?.genes?.Xenotype?.defName=="LP_HalfOrc" || Has(p,RelentlessGene);
  public static bool IsDwarf(Pawn p)=>p?.genes?.Xenotype?.defName=="LP_Dwarf" || Has(p,StatureGene);
  public static bool Enduring(Pawn p)=>Endurance!=null && p?.health?.hediffSet?.HasHediff(Endurance)==true;
  // 近战伤害倍率：矮人打巨魔 ×1.1；半兽人按 roll 判定野蛮重击 ×1.5。
  public static float MeleeFactor(Pawn caster,Pawn target,float roll,out bool savageHit){
   savageHit=false;if(caster==null)return 1f;float f=1f;
   if(Has(caster,StatureGene) && IsTroll(target))f*=TrollBaneFactor;
   // 1.22.0：手持半兽人巨斧时野蛮重击概率翻倍。
   if(Has(caster,SavageGene) && roll<SavageChance*(Items.Holds(caster,"LP_OrcishGreataxe")?2f:1f)){f*=SavageFactor;savageHit=true;}
   return f;
  }
 }

 // ---------- 半兽人：不屈坚韧 ----------
 // 本该倒地（且不致死）时给予"咬牙硬撑"：不会疼痛休克，意识保底约0.3；时间到后照常结算。
 public class OncePerCooldownWorker_Relentless : OncePerCooldownWorker {
  public override void OnTriggered(Pawn pawn,Hediff_Injury injury,DamageInfo dinfo){
   if(ext.grantedHediff==null)return;
   var h=pawn.health.hediffSet.GetFirstHediffOfDef(ext.grantedHediff)??pawn.health.AddHediff(ext.grantedHediff);
   h.TryGetComp<HediffComp_Disappears>()?.SetDuration(Mathf.RoundToInt(ext.grantedHours*GenDate.TicksPerHour));
   Mote(pawn);
  }
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),"get_InPainShock")]
 public static class RelentlessPainShock {
  static void Postfix(Pawn ___pawn,ref bool __result){if(__result && CommonRaces.Enduring(___pawn))__result=false;}
 }
 [HarmonyPatch(typeof(PawnCapacitiesHandler),nameof(PawnCapacitiesHandler.GetLevel))]
 public static class RelentlessConsciousness {
  // 只抬高 (0, 0.31) 区间：意识为0（大脑被毁）时照常死亡。
  static void Postfix(PawnCapacityDef __0,Pawn ___pawn,ref float __result){
   if(__result>0f && __result<CommonRaces.ConsciousnessFloor && __0==PawnCapacityDefOf.Consciousness && CommonRaces.Enduring(___pawn))__result=CommonRaces.ConsciousnessFloor;
  }
 }

 // ---------- 半兽人：野蛮攻击；矮人：对巨魔近战+10% ----------
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply")]
 public static class CommonRaceMeleeDamage {
  static IEnumerable<DamageInfo> Postfix(IEnumerable<DamageInfo> values,Verb_MeleeAttackDamage __instance,LocalTargetInfo __0){
   var caster=__instance.CasterPawn;bool savageHit;
   float f=CommonRaces.MeleeFactor(caster,__0.Thing as Pawn,CommonRaces.Has(caster,CommonRaces.SavageGene)?Rand.Value:1f,out savageHit);
   f*=Subraces.DamageFactor(caster,__0.Thing,true); // 1.57.0 熊地精突袭、地精小个子的怒火
   if(savageHit && caster.Spawned)MoteMaker.ThrowText(caster.DrawPos,caster.Map,"野蛮重击！",new Color(1f,.45f,.3f));
   foreach(var d in values){if(f!=1f){var copy=d;copy.SetAmount(d.Amount*f);yield return copy;}else yield return d;}
  }
 }

 // ---------- 精灵：冥想之息 ----------
 public class Gene_ElfTrance : Gene {
  public const int Interval=250,RestedTicks=5000;
  public const float FocusPerInterval=.0025f; // 8小时（20000刻）约+20%
  public int asleepTicks;int accumulated;
  static ThoughtDef rested;
  public static ThoughtDef Rested=>rested??(rested=DefDatabase<ThoughtDef>.GetNamed("LP_ElfTranceRested"));
  public override void TickInterval(int delta){base.TickInterval(delta);if((accumulated+=delta)<Interval)return;int e=accumulated;accumulated=0;Process(e);}
  public void Process(int elapsed){
   if(pawn.Dead){asleepTicks=0;return;}
   if(!pawn.Awake()){
    asleepTicks+=elapsed;
    if(pawn.HasPsylink && pawn.psychicEntropy!=null)pawn.psychicEntropy.OffsetPsyfocusDirectly(FocusPerInterval*elapsed/Interval);
    return;
   }
   if(asleepTicks>=RestedTicks)pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(Rested);
   asleepTicks=0;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref asleepTicks,"elfAsleepTicks");Scribe_Values.Look(ref accumulated,"elfTranceTicks");}
 }

 // ---------- 矮人：酒鬼之肝 ----------
 // 成瘾免疫由 GeneDef 的 chemical/addictionChanceFactor 原版字段实现；这里处理宿醉减半与"壮胆"。
 public class Gene_DwarfLiver : Gene {
  public int halvedHangover=-1;int accumulated;
  static HediffDef hangover;static ThoughtDef courage;
  public static HediffDef Hangover=>hangover??(hangover=DefDatabase<HediffDef>.GetNamedSilentFail("Hangover"));
  public static ThoughtDef Courage=>courage??(courage=DefDatabase<ThoughtDef>.GetNamed("LP_DwarfDrinkCourage"));
  public static bool IsAlcohol(Thing t)=>t?.def?.GetCompProperties<CompProperties_Drug>()?.chemical==ChemicalDefOf.Alcohol;
  public override void Notify_IngestedThing(Thing thing,int numTaken){base.Notify_IngestedThing(thing,numTaken);if(IsAlcohol(thing))pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(Courage);}
  public override void TickInterval(int delta){base.TickInterval(delta);if((accumulated+=delta)<250)return;accumulated=0;HalveHangover();}
  public void HalveHangover(){
   var h=Hangover==null?null:pawn.health.hediffSet.GetFirstHediffOfDef(Hangover);
   if(h==null || h.loadID==halvedHangover)return;
   h.Severity*=.5f;halvedHangover=h.loadID;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref halvedHangover,"halvedHangover",-1);Scribe_Values.Look(ref accumulated,"liverTicks");}
 }

 // ---------- 矮人：石之耐力 ----------
 public class Gene_DwarfStoneEndurance : Gene {
  int accumulated;
  static HediffDef under;
  public static HediffDef Under=>under??(under=DefDatabase<HediffDef>.GetNamed("LP_DwarfUnderMountain"));
  public static bool UnderThickRoof(Pawn p)=>p.Spawned && p.Position.GetRoof(p.Map)?.isThickRoof==true;
  public override void TickInterval(int delta){base.TickInterval(delta);if((accumulated+=delta)<250)return;accumulated=0;Sync();}
  public void Sync(){
   bool want=!pawn.Dead && Active && UnderThickRoof(pawn);var h=pawn.health.hediffSet.GetFirstHediffOfDef(Under);
   if(want && h==null)pawn.health.AddHediff(Under);else if(!want && h!=null)pawn.health.RemoveHediff(h);
  }
  public override void PostRemove(){base.PostRemove();var h=pawn.health.hediffSet.GetFirstHediffOfDef(Under);if(h!=null)pawn.health.RemoveHediff(h);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref accumulated,"stoneTicks");}
 }
 public class Hediff_DwarfUnderMountain : HediffWithComps {
  public override bool ShouldRemove=>base.ShouldRemove || pawn.genes?.GetFirstGeneOfType<Gene_DwarfStoneEndurance>()==null;
 }
}
