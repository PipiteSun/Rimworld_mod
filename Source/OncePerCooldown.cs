using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 共享组件：冷却保命。挂在 GeneDef 的 modExtensions 上：
 // 某次受伤会让人倒地（onDowned）或死亡（onDeath），且身上没有对应冷却 hediff 时，调用 worker 的 OnTriggered，
 // 再加上可见的冷却 hediff（时长由 cooldownDays 配置，原版 HediffComp_Disappears 计时，存读档安全）。
 // 同一刻同一次伤害后续部位（霰弹、爆炸多部位）交给 OnFollowUpInjury，不再重新判定。
 public class OncePerCooldownExtension : DefModExtension {
  public Type workerClass;
  public HediffDef cooldownHediff;
  public float cooldownDays=3f;
  public bool onDowned=true,onDeath=true;
  public float damageFactor=.1f;   // 降伤示例用：伤势×该值
  public HediffDef grantedHediff;  // 触发时给予的状态（半兽人"咬牙硬撑"等），时长 grantedHours
  public float grantedHours=12f;
  [MustTranslate] public string moteText;
  OncePerCooldownWorker worker;
  public OncePerCooldownWorker Worker{get{if(worker==null && workerClass!=null){worker=(OncePerCooldownWorker)Activator.CreateInstance(workerClass);worker.ext=this;}return worker;}}
  public int CooldownTicks=>Mathf.Max(1,Mathf.RoundToInt(cooldownDays*GenDate.TicksPerDay));
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(workerClass==null || !typeof(OncePerCooldownWorker).IsAssignableFrom(workerClass) || workerClass.IsAbstract)yield return "OncePerCooldownExtension.workerClass must be a concrete OncePerCooldownWorker";
   if(cooldownHediff==null)yield return "OncePerCooldownExtension.cooldownHediff is required";
   else if(!typeof(Hediff_OncePerCooldown).IsAssignableFrom(cooldownHediff.hediffClass))yield return "OncePerCooldownExtension.cooldownHediff must use LichPhylactery.Hediff_OncePerCooldown";
   if(cooldownDays<=0)yield return "OncePerCooldownExtension.cooldownDays must be positive";
   if(!onDowned && !onDeath)yield return "OncePerCooldownExtension needs onDowned or onDeath";
  }
 }
 public abstract class OncePerCooldownWorker {
  public OncePerCooldownExtension ext;
  // 触发的那一处伤势：可以改 injury.Severity、加 hediff、冒字等。冷却 hediff 由基类在之后添加。
  public abstract void OnTriggered(Pawn pawn,Hediff_Injury injury,DamageInfo dinfo);
  // 同一刻（同一次伤害）的后续伤势；默认不处理。
  public virtual void OnFollowUpInjury(Pawn pawn,Hediff_Injury injury,DamageInfo dinfo){}
  protected void Mote(Pawn pawn){if(!ext.moteText.NullOrEmpty() && pawn.Spawned)MoteMaker.ThrowText(pawn.DrawPos,pawn.Map,ext.moteText,new Color(1f,.9f,.4f),3.5f);}
 }
 // XML 可配置的降伤示例：这一次伤害的伤势×damageFactor，并冒字（例如半身人的"真走运！"）。
 public class OncePerCooldownWorker_ReduceDamage : OncePerCooldownWorker {
  public override void OnTriggered(Pawn pawn,Hediff_Injury injury,DamageInfo dinfo){injury.Severity=Mathf.Max(.01f,injury.Severity*ext.damageFactor);Mote(pawn);}
  public override void OnFollowUpInjury(Pawn pawn,Hediff_Injury injury,DamageInfo dinfo){injury.Severity=Mathf.Max(.01f,injury.Severity*ext.damageFactor);}
 }
 // 冷却 hediff：记录来源与触发刻，健康页显示剩余时间（来自 HediffComp_Disappears）。
 public class Hediff_OncePerCooldown : HediffWithComps {
  public GeneDef sourceGene;
  public int triggeredTick=-1;
  public override string LabelInBrackets{get{var b=base.LabelInBrackets;return sourceGene==null?b:(b.NullOrEmpty()?sourceGene.label:sourceGene.label+"，"+b);}}
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref sourceGene,"sourceGene");Scribe_Values.Look(ref triggeredTick,"triggeredTick",-1);}
 }
 public static class OncePerCooldown {
  static List<GeneDef> genes;static int geneCount=-1;
  public static List<GeneDef> Genes{get{
   if(genes==null || geneCount!=DefDatabase<GeneDef>.DefCount){geneCount=DefDatabase<GeneDef>.DefCount;genes=DefDatabase<GeneDef>.AllDefsListForReading.Where(g=>g.GetModExtension<OncePerCooldownExtension>()?.Worker!=null && typeof(Hediff_OncePerCooldown).IsAssignableFrom(g.GetModExtension<OncePerCooldownExtension>().cooldownHediff?.hediffClass)).ToList();}
   return genes;
  }}
  public static Hediff_OncePerCooldown Cooldown(Pawn pawn,OncePerCooldownExtension ext)=>pawn?.health?.hediffSet?.GetFirstHediffOfDef(ext.cooldownHediff) as Hediff_OncePerCooldown;
  public static bool Ready(Pawn pawn,GeneDef gene){var ext=gene?.GetModExtension<OncePerCooldownExtension>();return ext!=null && pawn?.genes?.HasActiveGene(gene)==true && pawn.health.hediffSet.GetFirstHediffOfDef(ext.cooldownHediff)==null;}
  public static Hediff_OncePerCooldown StartCooldown(Pawn pawn,GeneDef gene){
   var ext=gene.GetModExtension<OncePerCooldownExtension>();
   var h=(Hediff_OncePerCooldown)HediffMaker.MakeHediff(ext.cooldownHediff,pawn);h.sourceGene=gene;h.triggeredTick=Find.TickManager.TicksGame;
   int ticks=ext.CooldownTicks;
   if(gene.defName=="LP_HalflingLuck" && Feats2.On(pawn)?.bountiful==true){ticks=ticks/3;Feats2.ShareLuck(pawn);} // 1.58.1 好运连连，分一次给身边的友军；1.67.0 冷却 3 天 → 1 天
   pawn.health.AddHediff(h);h.TryGetComp<HediffComp_Disappears>()?.SetDuration(ticks);
   return h;
  }
  // 返回是否处理了这处伤势。
  public static bool Process(Pawn pawn,Hediff_Injury injury,DamageInfo dinfo){
   var list=Genes;if(list.Count==0 || pawn?.genes==null || pawn.Dead || injury==null)return false;
   int now=Find.TickManager.TicksGame;
   for(int i=0;i<list.Count;i++){
    var gene=list[i];if(!pawn.genes.HasActiveGene(gene))continue;
    var ext=gene.GetModExtension<OncePerCooldownExtension>();var cd=Cooldown(pawn,ext);
    if(cd!=null){if(cd.triggeredTick==now && cd.sourceGene==gene){ext.Worker.OnFollowUpInjury(pawn,injury,dinfo);return true;}continue;}
    // 致死伤害只交给 onDeath；只挡倒地的 worker 不会把冷却浪费在必死的一击上。
    bool lethal=pawn.health.WouldDieAfterAddingHediff(injury);
    bool trigger=lethal?ext.onDeath:ext.onDowned && !pawn.Downed && pawn.health.WouldBeDownedAfterAddingHediff(injury);
    if(!trigger)continue;
    ext.Worker.OnTriggered(pawn,injury,dinfo);StartCooldown(pawn,gene);return true;
   }
   return false;
  }
 }
 // 挂在"伤势即将加到身上"的那一刻：此时伤势严重度已定，可以准确判断是否致倒/致死。
 [HarmonyPatch(typeof(DamageWorker_AddInjury),"FinalizeAndAddInjury",new Type[]{typeof(Pawn),typeof(Hediff_Injury),typeof(DamageInfo),typeof(DamageWorker.DamageResult)})]
 public static class OncePerCooldownInjury {
  static void Prefix(Pawn __0,Hediff_Injury __1,DamageInfo __2){if(Feats2.borrowed.Count>0 && Feats2.UseBorrowedLuck(__0,__1))return;if(OncePerCooldown.Genes.Count>0)OncePerCooldown.Process(__0,__1,__2);}
 }
}
