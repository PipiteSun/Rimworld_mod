using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public static class MoltenGuard {
  public const string Maul="LP_MoltenMaul",Helmet="LP_MoltenHelmet",Armor="LP_MoltenArmor";
  public const int Duration=600,FatigueDuration=600,Cooldown=5400;
  public static bool Wears(Pawn p,string name)=>p?.apparel?.WornApparel.Any(a=>a.def.defName==name)==true;
  public static bool Complete(Pawn p)=>FireGiant.Active(p) && p.equipment?.Primary?.def.defName==Maul && Wears(p,Helmet) && Wears(p,Armor);
  public static Hediff_MoltenOverdrive State(Pawn p)=>p?.health?.hediffSet?.hediffs.OfType<Hediff_MoltenOverdrive>().FirstOrDefault();
  public static string DisabledReason(Pawn p){
   if(!Complete(p))return "需要激活的火巨人血脉，并装备熔核破城锤、炉心战盔和熔核重甲。";
   if(p.Dead || p.Downed || !p.Spawned || !p.Awake() || p.InMentalState)return "需要清醒、可行动的角色。";
   var h=State(p);if(h!=null && h.cooldownUntil>Find.TickManager.TicksGame)return "炉心冷却中："+((h.cooldownUntil-Find.TickManager.TicksGame)/60f).ToString("0.0")+"秒。";
   return null;
  }
  public static bool Activate(Pawn p){
   if(DisabledReason(p)!=null)return false;
   var h=State(p)??(Hediff_MoltenOverdrive)p.health.AddHediff(DefDatabase<HediffDef>.GetNamed("LP_MoltenOverdrive"));
   int now=Find.TickManager.TicksGame;h.activeUntil=now+Duration;h.fatigueUntil=h.activeUntil+FatigueDuration;h.cooldownUntil=now+Cooldown;h.Sync();p.health.Notify_HediffChanged(h);return true;
  }
 }
 public class Apparel_MoltenArmor : Apparel {
  public override IEnumerable<Gizmo> GetWornGizmos(){
   foreach(var gizmo in base.GetWornGizmos())yield return gizmo;
   var p=Wearer;if(p?.Faction!=Faction.OfPlayer)yield break;
   var command=new Command_Action{defaultLabel="炉心过载",defaultDesc="全套熔核禁卫：10秒内近战伤害×125%、移动速度×115%；结束后10秒移动速度×85%。冷却90秒，从启动时计时。中途卸装或失去血脉会中止增益并进入疲劳；换装备不会重置冷却。",icon=ContentFinder<Texture2D>.Get("FireGiant/MoltenGuard/Armor"),action=()=>MoltenGuard.Activate(p)};
   string reason=MoltenGuard.DisabledReason(p);if(reason!=null)command.Disable(reason);yield return command;
  }
 }
 public class Hediff_MoltenOverdrive : Hediff {
  public int activeUntil,fatigueUntil,cooldownUntil;
  int lastPhase=-1;
  static readonly HediffStage Boost=new HediffStage{statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.MeleeDamageFactor,value=1.25f},new StatModifier{stat=StatDefOf.MoveSpeed,value=1.15f}}};
  static readonly HediffStage Fatigue=new HediffStage{statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.MoveSpeed,value=.85f}}};
  static readonly HediffStage Cooling=new HediffStage();
  public int Phase=>Find.TickManager.TicksGame<activeUntil && MoltenGuard.Complete(pawn) && !pawn.Dead?1:Find.TickManager.TicksGame<fatigueUntil?2:0;
  public override HediffStage CurStage=>Phase==1?Boost:Phase==2?Fatigue:Cooling;
  public override bool ShouldRemove=>Find.TickManager.TicksGame>=cooldownUntil && Find.TickManager.TicksGame>=fatigueUntil;
  public override string LabelInBrackets=>Phase==1?"过载中":Phase==2?"过载疲劳":"冷却中";
  public override string TipStringExtra=>"剩余冷却："+(Math.Max(0,cooldownUntil-Find.TickManager.TicksGame)/60f).ToString("0.0")+"秒。冷却与疲劳随角色保存，不因更换装备而重置。";
  public void Sync(){
   int now=Find.TickManager.TicksGame;
   if(now<activeUntil && (!MoltenGuard.Complete(pawn) || pawn.Dead)){activeUntil=now;fatigueUntil=now+MoltenGuard.FatigueDuration;}
   int phase=Phase;if(phase!=lastPhase){lastPhase=phase;pawn.health.Notify_HediffChanged(this);}
  }
  public override void TickInterval(int delta){base.TickInterval(delta);Sync();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref activeUntil,"activeUntil");Scribe_Values.Look(ref fatigueUntil,"fatigueUntil");Scribe_Values.Look(ref cooldownUntil,"cooldownUntil");}
 }
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.StunFor))]
 public static class MoltenHelmetStunResistance {
  static void Prefix(ref int __0,Pawn ___parentPawn){if(__0>0 && FireGiant.Active(___parentPawn) && MoltenGuard.Wears(___parentPawn,MoltenGuard.Helmet))__0=Math.Max(1,Mathf.CeilToInt(__0*.7f));}
 }
}
