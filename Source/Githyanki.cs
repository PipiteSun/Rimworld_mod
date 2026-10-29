using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.18.0 吉斯洋基：银剑传承、星界跳跃、巫妖女王的信徒、夺心魔宿敌。
 public static class Githyanki {
  public const float SwordPsychic=8f,FlayerFactor=2f,NemesisDamage=1.25f,NemesisHit=.1f,RecruitFactor=1.3f,ArmChance=.25f;
  public const int StepRange=8;
  static XenotypeDef xenotype;static GeneDef sword,step,devotee,nemesis;static ThingDef swordDef;static DamageDef psychic;
  public static XenotypeDef Xenotype=>xenotype??(xenotype=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_Githyanki"));
  public static GeneDef SwordGene=>sword??(sword=DefDatabase<GeneDef>.GetNamedSilentFail("LP_GithSilverSword"));
  public static GeneDef StepGene=>step??(step=DefDatabase<GeneDef>.GetNamedSilentFail("LP_GithAstralStep"));
  public static GeneDef DevoteeGene=>devotee??(devotee=DefDatabase<GeneDef>.GetNamedSilentFail("LP_GithLichDevotee"));
  public static GeneDef NemesisGene=>nemesis??(nemesis=DefDatabase<GeneDef>.GetNamedSilentFail("LP_GithNemesis"));
  public static ThingDef Sword=>swordDef??(swordDef=DefDatabase<ThingDef>.GetNamedSilentFail("LP_SilverSword"));
  public static DamageDef Psychic=>psychic??(psychic=DefDatabase<DamageDef>.GetNamedSilentFail("LP_SilverPsychic"));
  public static bool Has(Pawn p,GeneDef gene)=>gene!=null && p?.genes?.HasActiveGene(gene)==true;
  public static bool IsLich(Pawn p)=>p!=null && !p.Dead && Necro.Lich(p)!=null;

  // ---------- 银剑 ----------
  public static bool HoldsSword(Pawn p)=>Sword!=null && p?.equipment?.Primary?.def==Sword;
  // 只有银剑传承基因持有者用银剑才有灵能；其他人是普通钢剑。
  public static bool Attuned(Pawn p)=>HoldsSword(p) && Has(p,SwordGene);
  // 附加灵能伤害：8×目标心灵敏感度（0～2.5），夺心魔×2；心灵失聪、机械体为0。
  public static float SwordPsychicDamage(Pawn caster,Pawn target){
   if(!Attuned(caster) || target==null || target.Dead || target.RaceProps.IsMechanoid)return 0f;
   float sens=Mathf.Clamp(target.GetStatValue(StatDefOf.PsychicSensitivity),0f,2.5f);if(sens<=.001f)return 0f;
   // 1.28.0：维拉基斯的神力灌注者 +20%。
   return SwordPsychic*sens*(MindFlayer.IsMindFlayer(target)?FlayerFactor:1f)*(Twilight.InfusedBy(caster,"Vlaakith")?TwilightSkills.VlaakithSword:1f);
  }
  // 非玩家阵营的吉斯洋基战士生成时有25%概率手持银剑（缴获来源）。返回是否换上了银剑。
  public static bool MaybeArm(Pawn p,float roll){
   if(roll>=ArmChance || Sword==null || p?.equipment==null || p.Faction==null || p.Faction.IsPlayer || !Has(p,SwordGene) || p.WorkTagIsDisabled(WorkTags.Violent) || !p.DevelopmentalStage.Adult())return false;
   p.equipment.DestroyAllEquipment();
   var s=(ThingWithComps)ThingMaker.MakeThing(Sword);s.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityTraderItem(),ArtGenerationContext.Outsider);
   p.equipment.AddEquipment(s);return true;
  }
  public static string CannotForge(Pawn p)=>Has(p,SwordGene)?null:"只有带“银剑传承”基因的吉斯洋基才能锻造银剑。";

  // ---------- 星界跳跃 ----------
  public static bool CanPayStep(Pawn p,float cost)=>cost<=0 || p==null || p.GetPsylinkLevel()<=0 || p.psychicEntropy==null || p.psychicEntropy.CurrentPsyfocus+.0001f>=cost;
  public static string CannotStep(Pawn caster,IntVec3 c){
   var map=caster?.Map;if(map==null)return "施法者不在地图上。";
   if(!c.IsValid || !c.InBounds(map))return "需要地图上的目标位置。";
   if(c==caster.Position)return "已经站在这里了。";
   if(c.DistanceTo(caster.Position)>StepRange+.5f)return "超出星界跳跃距离（8格）。";
   if(c.Fogged(map))return "不能跳进未探索的地方。";
   if(!c.Standable(map))return "那里没有可以站立的地方。";
   if(c.GetFirstPawn(map)!=null)return "那里已经有人了。";
   return null;
  }
  public static bool Step(Pawn caster,IntVec3 c,float cost){
   if(CannotStep(caster,c)!=null || !CanPayStep(caster,cost))return false;
   var map=caster.Map;var from=caster.Position;
   if(cost>0 && caster.GetPsylinkLevel()>0 && caster.psychicEntropy!=null)caster.psychicEntropy.OffsetPsyfocusDirectly(-cost);
   FleckMaker.Static(from,map,FleckDefOf.PsycastAreaEffect,1.2f);
   caster.pather?.StopDead();caster.Position=c;caster.Notify_Teleported(false,true);
   FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,1.2f);
   return true;
  }

  // ---------- 巫妖女王的信徒 ----------
  // 同一地图上有本殖民地（成员或囚犯）的巫妖。
  public static bool SeesLich(Pawn p){
   if(!Has(p,DevoteeGene) || !p.Spawned)return false;
   foreach(var o in p.Map.mapPawns.AllHumanlikeSpawned)if(o!=p && (o.Faction==Faction.OfPlayer || o.IsPrisonerOfColony) && IsLich(o))return true;
   return false;
  }

  // ---------- 宿敌 ----------
  public static bool Nemesis(Pawn attacker,Pawn target)=>target!=null && attacker!=null && attacker!=target && Has(attacker,NemesisGene) && MindFlayer.IsMindFlayer(target);
 }

 public class ThoughtWorker_SilverSwordRejects : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>Githyanki.HoldsSword(p) && !Githyanki.Has(p,Githyanki.SwordGene)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }
 public class ThoughtWorker_GithSeesLich : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>Githyanki.SeesLich(p)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }

 // 银剑命中：在原有伤害之后追加一段灵能伤害。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply")]
 public static class SilverSwordPsychic {
  static IEnumerable<DamageInfo> Postfix(IEnumerable<DamageInfo> values,Verb_MeleeAttackDamage __instance,LocalTargetInfo __0){
   foreach(var d in values)yield return d;
   var caster=__instance.CasterPawn;
   if(__instance.EquipmentSource?.def!=Githyanki.Sword || Githyanki.Psychic==null)yield break;
   float amount=Githyanki.SwordPsychicDamage(caster,__0.Thing as Pawn);
   if(amount>0)yield return new DamageInfo(Githyanki.Psychic,amount,0f,-1f,caster,null,Githyanki.Sword);
  }
 }
 // 宿敌：对夺心魔的一切伤害×125%。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class GithNemesisDamage {
  static void Prefix(Pawn __instance,ref DamageInfo __0){if(__0.Instigator is Pawn a && Githyanki.Nemesis(a,__instance))__0.SetAmount(__0.Amount*Githyanki.NemesisDamage);}
 }
 // 宿敌：对夺心魔近战命中率+10%。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class GithNemesisHit {
  static void Postfix(Verb_MeleeAttack __instance,LocalTargetInfo __0,ref float __result){if(Githyanki.Nemesis(__instance.CasterPawn,__0.Thing as Pawn))__result=Mathf.Min(1f,__result+Githyanki.NemesisHit);}
 }
 // 银剑只能由银剑传承基因持有者锻造。
 [HarmonyPatch(typeof(Bill),nameof(Bill.PawnAllowedToStartAnew))]
 public static class SilverSwordForge {
  static void Postfix(Bill __instance,Pawn p,ref bool __result){
   if(!__result || Githyanki.Sword==null || __instance.recipe?.ProducedThingDef!=Githyanki.Sword)return;
   string reason=Githyanki.CannotForge(p);if(reason==null)return;
   __result=false;JobFailReason.Is(reason);
  }
 }
 // 巫妖劝降吉斯洋基俘虏：这次谈话降低的抵抗×130%。
 [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt),nameof(InteractionWorker_RecruitAttempt.Interacted))]
 public static class GithLichRecruit {
  static void Prefix(Pawn __1,out float __state){__state=__1?.guest?.resistance??-1f;}
  static void Postfix(Pawn __0,Pawn __1,float __state){
   if(__state<0 || __1?.guest==null || !Githyanki.IsLich(__0) || !Githyanki.Has(__1,Githyanki.DevoteeGene))return;
   float drop=__state-__1.guest.resistance;if(drop<=0)return;
   __1.guest.resistance=Mathf.Max(0f,__1.guest.resistance-drop*(Githyanki.RecruitFactor-1f));
  }
 }
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class GithArmOnGeneration {
  static void Postfix(Pawn __result){if(__result!=null && Githyanki.Has(__result,Githyanki.SwordGene))Githyanki.MaybeArm(__result,Rand.Value);}
 }
}
