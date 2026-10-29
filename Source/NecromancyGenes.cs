using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace LichPhylactery {
 public static class NecromancyGenes {
  public const int StunTicks=120;
  public static bool HasBody(Pawn pawn)=>pawn?.genes?.HasActiveGene(Necro.Def<GeneDef>("NecroticBody"))==true;
  public static bool HasTouch(Pawn pawn)=>pawn?.genes?.HasActiveGene(Necro.Def<GeneDef>("NecroticTouch"))==true;
  public static bool IsLivingTarget(Pawn pawn)=>pawn!=null && !pawn.Dead && !pawn.Destroyed && pawn.RaceProps.IsFlesh && !Necro.Undead(pawn) && !HasBody(pawn);
  public static void ApplyTouch(Pawn attacker,Pawn target){
   if(attacker==target || attacker?.Dead!=false || !HasTouch(attacker) || !IsLivingTarget(target))return;
   target.stances?.stunner.StunFor(StunTicks,attacker,false,true,false);
  }
 }
 public class Gene_NecroticBody : Gene {
  public override void PostAdd(){base.PostAdd();pawn.needs?.AddOrRemoveNeedsAsAppropriate();var cold=pawn.health?.hediffSet.GetFirstHediffOfDef(HediffDefOf.Hypothermia);if(cold!=null)pawn.health.RemoveHediff(cold);}
  public override void Notify_PawnDied(DamageInfo? dinfo,Hediff culprit=null){base.Notify_PawnDied(dinfo,culprit);if(Active)Necro.Lich(pawn)?.box?.ScheduleRevival();}
 }
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class NecroticMeleeHit {
  // This hook is reached only after the native melee hit/dodge checks, once per attack.
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target)=>NecromancyGenes.ApplyTouch(__instance.CasterPawn,target.Pawn);
 }
 [HarmonyPatch(typeof(HediffGiver_Hypothermia),nameof(HediffGiver_Hypothermia.OnIntervalPassed))]
 public static class NecroticColdImmunity {
  static bool Prefix(Pawn pawn)=>!NecromancyGenes.HasBody(pawn);
 }
 public partial class Hediff_Lich {
  public int necromancyGeneVersion;
  public void UpgradeNecromancyGenes(){
   if(necromancyGeneVersion>=1 || pawn?.genes==null)return;
   foreach(var name in new[]{"NecroticTouch","NecroticBody"}){
    var gene=Necro.Def<GeneDef>(name);
    if(!pawn.genes.GenesListForReading.Any(g=>g.def==gene))pawn.genes.AddGene(gene,true);
   }
   necromancyGeneVersion=1;Necro.Refresh(pawn);
  }
 }
 public partial class Phylactery {
  public const int RevivalDelay=3*60000;
  public int revivalTick=-1;
  public void ScheduleRevival(){
   if(Destroyed || soul?.Dead!=true || !NecromancyGenes.HasBody(soul) || revivalTick>=0)return;
   revivalTick=Find.TickManager.TicksGame+RevivalDelay;
  }
  public void ProcessRevival(){
   if(Destroyed || soul==null)return;
   if(!soul.Dead){revivalTick=-1;return;}
   ScheduleRevival();Capture();
   if(revivalTick>=0 && Find.TickManager.TicksGame>=revivalTick && NecromancyGenes.HasBody(soul))TryAutoRevive();
  }
  public bool TryAutoRevive(){
   if(Destroyed || soul?.Dead!=true || !NecromancyGenes.HasBody(soul) || revivalTick<0 || Find.TickManager.TicksGame<revivalTick)return false;
   Map map=MapHeld;Caravan caravan=map==null?this.GetCaravan():null;
   IntVec3 cell=IntVec3.Invalid;
   if(map!=null){
    if(!CellFinder.TryFindRandomCellNear(PositionHeld,map,8,c=>c.Standable(map),out cell))return false;
   }else if(caravan==null)return false; // Keep the soul and elapsed timer until its container is accessible.
   if(!Capture())return false;
   Pawn pawn=soul;souls.Remove(pawn);
   if(!ResurrectionUtility.TryResurrect(pawn,new ResurrectionParams{restoreMissingParts=true,dontSpawn=true,removeDiedThoughts=true})){souls.TryAdd(pawn,false);return false;}
   if(map!=null)GenSpawn.Spawn(pawn,cell,map);else caravan.AddPawn(pawn,true);
   revivalTick=-1;pawn.psychicEntropy?.OffsetPsyfocusDirectly(-1f);Necro.Refresh(pawn);
   Messages.Message(pawn.LabelShort+"已在命匣旁重塑身体。",pawn,MessageTypeDefOf.PositiveEvent,false);
   IdeoEvents.Record("LP_HE_LichRevived",pawn); // 1.56.0 文化：亡灵观
   return true;
  }
  public string RevivalStatus=>soul?.Dead!=true?"":revivalTick<0?"\n灵魂已保存，等待复生计时。":Find.TickManager.TicksGame<revivalTick?"\n自动复生剩余："+((revivalTick-Find.TickManager.TicksGame)/2500f).ToString("0.0")+"小时。":"\n复生已就绪，等待命匣附近有可用位置。";
 }
}
