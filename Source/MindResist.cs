using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 共享组件：心智抗性 LP_MindResist（0~1，默认0）。基因/伤病/装备用 statOffsets 叠加。
 // 供以后的夺心魔心灵震爆、蝌蚪寄生、魅惑类效果调用；本模组现有内容都不读取它。
 public static class DNDMind {
  static StatDef stat;
  public static StatDef Stat=>stat??(stat=DefDatabase<StatDef>.GetNamedSilentFail("LP_MindResist"));
  // 当前抵抗率（0~1）。没有基因/装备时为0。
  public static float Resistance(Pawn pawn){
   if(pawn==null || Stat==null || pawn.Dead)return 0f;
   // 直接走 StatWorker：原版会把"没有任何定义引用"的属性标为不可变并永久缓存（StatDef.SetImmutability），
   // 运行时加入的基因/效果因此读不到；这里绕过缓存，调用频率很低（技能命中、心灵类心情）。
   return Mathf.Clamp01(Stat.Worker.GetValue(StatRequest.For(pawn)));
  }
  // 掷骰：抵抗成功返回 true。chanceFactor 为效果自身的强度修正（例如强力震爆传0.5，抵抗率减半）。
  public static bool TryResist(Pawn pawn,float chanceFactor=1f){
   if(HeroCombat.MindResist(pawn))return true; // 1.58.0 心智堡垒免疫、法术抗体 50%
   float chance=Mathf.Clamp01(Resistance(pawn)*chanceFactor);
   return chance>0f && Rand.Chance(chance);
  }
  // 按抗性削弱一个数值（伤害、严重度、心情惩罚）：value×(1-抗性)。
  public static float Attenuate(Pawn pawn,float value)=>value*(1f-Resistance(pawn));
 }
 // 心灵类负面心情（原版以心灵敏感度缩放的那类：心灵嗡鸣等）按心智抗性减弱。正面心情不变。
 // 抗性为0时不改任何数值，现有存档无变化。
 [HarmonyPatch(typeof(Thought),nameof(Thought.MoodOffset))]
 public static class MindResistPsychicMood {
  static void Postfix(Thought __instance,ref float __result){
   if(__result>=0f || __instance.def?.effectMultiplyingStat!=StatDefOf.PsychicSensitivity)return;
   float r=DNDMind.Resistance(__instance.pawn);if(r>0f)__result*=1f-r;
  }
 }
}
