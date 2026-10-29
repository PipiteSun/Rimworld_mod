using HarmonyLib;
using RimWorld.Planet;
using Verse;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.52.1 眼魔的反魔法锥（和反魔法护符）也压住 VPE / VEF 的施法：技能按钮灰掉，强行施放时失败。
 // 判定在主程序集 Monsters.InAntiMagic（只读 MapComponent_Legendary 的缓存，地图上没有眼魔 / 护符就直接返回）。
 [HarmonyPatch(typeof(Ability),nameof(Ability.IsEnabledForPawn))]
 public static class AntiMagicVPEEnabled {
  static void Postfix(Ability __instance,ref bool __result,ref string reason){
   if(!__result || !(__instance?.Caster is Pawn p) || !Monsters.InAntiMagic(p))return;
   __result=false;reason="被反魔法压制（眼魔主眼正面或反魔法护符附近）";
  }
 }
 [HarmonyPatch(typeof(Ability),nameof(Ability.Cast),new[]{typeof(GlobalTargetInfo[])})]
 public static class AntiMagicVPECast {
  static bool Prefix(Ability __instance)=>!(__instance?.Caster is Pawn p) || !Monsters.BlockCast(p);
 }
}
