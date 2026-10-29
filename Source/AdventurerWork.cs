using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.61.0 热修（日常工作经验）：干活学到的技能经验按 50:1 转成冒险者经验（乘“日常工作经验倍率”），每人每个本地日前 80 点全额，之后 ×0.5，不封顶。
 // 不满 1 点的部分攒在 workXpRem，满 1 点才走 GainXP（再乘冒险者经验倍率和凡人之志，升级信件照常）。direct（技能训练、神经增强等直接加经验）不算。
 // 只看殖民地的人（玩家阵营或奴隶）；传奇等级照常。按原始技能经验算：在 AdventurerLearn 的属性加成前缀之前取值。
 public static class AdventurerWork {
  public const float SkillPerXp=50f,DailyFull=80f,OverFactor=.5f;
  public static float Mult=>LichMod.Settings?.adventurerWorkXp??1f;
  public static bool Counts(Pawn p)=>p!=null && !p.Dead && (p.Faction!=null && p.Faction.IsPlayer || p.IsSlaveOfColony);
  // 今天已计入的量（倍率之后、折算之前；换日后为 0）。
  public static float Today(Hediff_Adventurer h)=>h.workXpDay==Feats.LocalDay(h.pawn)?h.workXpToday:0f;
  // amount（倍率之后）按今天剩下的全额额度折算，并记入今日计数。
  public static float Diminish(Hediff_Adventurer h,float amount){
   int d=Feats.LocalDay(h.pawn);if(d!=h.workXpDay){h.workXpDay=d;h.workXpToday=0f;}
   float full=Mathf.Clamp(DailyFull-h.workXpToday,0f,amount);h.workXpToday+=amount;
   return full+(amount-full)*OverFactor;
  }
  // 返回这次折算出的冒险者经验（倍率前的基础值，含攒着不满 1 点的部分）。
  public static float Learned(Pawn p,SkillDef sDef,float skillXp){
   float m=Mult;if(m<=0f || skillXp<=0f || !Adventurer.Enabled || !Counts(p))return 0f;
   var h=Adventurer.Get(p);if(h==null)return 0f;
   var rec=p.skills?.GetSkill(sDef);if(rec==null || rec.TotallyDisabled)return 0f;
   float got=Diminish(h,skillXp/SkillPerXp*m);h.workXpRem+=got;h.workXpTotal+=got;
   if(h.workXpRem>=1f){float whole=Mathf.Floor(h.workXpRem);h.workXpRem-=whole;Adventurer.GainXP(p,whole);}
   return got;
  }
 }
 [HarmonyPatch(typeof(Pawn_SkillTracker),nameof(Pawn_SkillTracker.Learn))]
 public static class AdventurerWorkLearn {
  [HarmonyPriority(Priority.First)]
  static void Prefix(Pawn ___pawn,SkillDef sDef,float xp,bool direct){if(xp>0f && !direct)AdventurerWork.Learned(___pawn,sDef,xp);}
 }

 public partial class Settings {
  public float adventurerWorkXp=1f;
  void ExposeWork(){Scribe_Values.Look(ref adventurerWorkXp,"adventurerWorkXp",1f);adventurerWorkXp=Mathf.Clamp(Mathf.Round(adventurerWorkXp*10f)/10f,0f,3f);}
 }
}
