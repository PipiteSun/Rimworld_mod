using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.88.0 职业专属专长（ArtSource/ClassFeats-1.88.0-report.md）：每个职业两个，职业 3 级 / 7 级可选，走原来的“可选专长”（属性提升或战士奖励专长）。
 // 定义在 Defs/Adventurer_ClassFeats.xml（LP_FeatDef.requiredClass / classLevel）。数值类效果（闪避、命中等）照旧合进冒险者状态；
 // 下面是规则类效果：职业强度、资源上限、资源获取、本职业能力冷却与消耗。职业等级掉到要求以下时这些效果暂停（专长不收回）。
 public static class ClassFeats {
  // 生效的职业专属专长：有专长、冒险者总开关开着、所属职业仍到要求等级。
  public static bool Active(Hediff_Adventurer h,LP_FeatDef f)=>f!=null && f.requiredClass!=null && Adventurer.Enabled && ClassSystem.Level(h,f.requiredClass)>=f.classLevel;
  static IEnumerable<LP_FeatDef> On(Hediff_Adventurer h){
   if(h?.feats==null || h.feats.Count==0 || h.classes==null || h.classes.levels.Count==0)yield break;
   foreach(var f in h.feats)if(f.requiredClass!=null && Active(h,f))yield return f;
  }
  public static IEnumerable<LP_FeatDef> All=>DefDatabase<LP_FeatDef>.AllDefsListForReading.Where(f=>f.requiredClass!=null).OrderBy(f=>f.order);
  public static IEnumerable<LP_FeatDef> For(LP_ClassDef c)=>All.Where(f=>f.requiredClass==c);

  // 职业强度属性（战技 / 奥术 / 契约强度等）直接加。
  public static float PowerBonus(Pawn p,StatDef stat){
   if(stat==null)return 0f;float b=0f;
   foreach(var f in On(Adventurer.Get(p)))if(f.powerBonus!=0f && f.powerStats!=null && f.powerStats.Contains(stat))b+=f.powerBonus;
   return b;
  }
  public static string PowerText(Pawn p,StatDef stat){
   if(stat==null)return "";var h=Adventurer.Get(p);var list=On(h).Where(f=>f.powerBonus!=0f && f.powerStats!=null && f.powerStats.Contains(stat)).ToList();
   return list.Count==0?"":"\n职业专长（"+string.Join("、",list.Select(f=>f.label))+"）：+"+list.Sum(f=>f.powerBonus).ToStringPercent();
  }
  // 资源上限：神恩是牧师和圣武士共用的一条，两边的专长都加在这一条上。
  public static int ResourceBonus(Hediff_Adventurer h,LP_ClassDef c){
   if(c==null)return 0;var key=ClassSystem.PoolKey(c);int n=0;
   foreach(var f in On(h))if(f.resourceBonus!=0 && ClassSystem.PoolKey(f.requiredClass)==key)n+=f.resourceBonus;
   return n;
  }
  public static float GainFactor(Hediff_Adventurer h,LP_ClassDef c){
   if(c==null)return 1f;var key=ClassSystem.PoolKey(c);float x=1f;
   foreach(var f in On(h))if(f.gainFactor!=1f && ClassSystem.PoolKey(f.requiredClass)==key)x*=f.gainFactor;
   return x;
  }
  // 本职业的能力（含子职业、传奇能力）：冷却 ×、消耗 −N。
  public static float CooldownFactor(Pawn p,AbilityDef d){
   var h=Adventurer.Get(p);if(h?.feats==null || h.feats.Count==0)return 1f;
   var c=ClassSystem.ClassOf(d);if(c==null)return 1f;float x=1f;
   foreach(var f in On(h))if(f.cooldownFactor!=1f && f.requiredClass==c)x*=f.cooldownFactor;
   return x;
  }
  public static int CostReduce(Pawn p,AbilityDef d){
   var h=Adventurer.Get(p);if(h?.feats==null || h.feats.Count==0)return 0;
   var c=ClassSystem.ClassOf(d);if(c==null)return 0;int n=0;
   foreach(var f in On(h))if(f.requiredClass==c)n+=f.costReduce;
   return n;
  }
 }

 // 冷却：本职业能力 × cooldownFactor（和其他冷却修正一样在 StartCooldown 前改 ticks）。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.StartCooldown))]
 public static class ClassFeatCooldown {
  static void Prefix(RimWorld.Ability __instance,ref int ticks){
   if(ticks<=0 || !(__instance is Ability_Class))return;
   float f=ClassFeats.CooldownFactor(__instance.pawn,__instance.def);if(f!=1f)ticks=Mathf.Max(1,Mathf.RoundToInt(ticks*f));
  }
 }
}
