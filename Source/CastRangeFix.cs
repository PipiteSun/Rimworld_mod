using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 2026-10-28 热修（1.64.0）：下令攻击时按加成后的射程找开枪位置。
 // 原版“走到开枪位置”（Toils_Combat.GotoCastPosition、CastPositionFinder）读的是武器原始射程 verbProps.range，
 // 不算任何射程加成（本模组的神射手 / 鹰眼 / 箭术、Isekai 的敏捷射程等）：信息卡和瞄准圈显示的射程是对的，但小人会先走近到原始射程才开枪。
 // 这里把这两处的原始射程换成 Verb.EffectiveRange（原版其余地方——能否命中、自动索敌——本来就用它）。
 public static class CastRange {
  public static float Of(Verb v)=>v==null?0f:v.EffectiveRange;
  static readonly FieldInfo VerbProps=AccessTools.Field(typeof(Verb),nameof(Verb.verbProps)),Range=AccessTools.Field(typeof(VerbProperties),nameof(VerbProperties.range));
  public static int Replaced;
  // verb.verbProps.range → CastRange.Of(verb)
  public static IEnumerable<CodeInstruction> Swap(IEnumerable<CodeInstruction> code){
   var list=code.ToList();
   for(int i=0;i<list.Count-1;i++){
    if(list[i].LoadsField(VerbProps) && list[i+1].LoadsField(Range)){
     list[i].opcode=OpCodes.Call;list[i].operand=AccessTools.Method(typeof(CastRange),nameof(Of));
     list[i+1].opcode=OpCodes.Nop;list[i+1].operand=null;Replaced++;
    }
   }
   return list;
  }
 }
 [HarmonyPatch]
 public static class CastRangeGoto {
  // 编译器生成的 lambda，按名字在内部类里找；找不到（游戏更新改了结构）就跳过这条，不影响模组加载。
  static MethodBase Target()=>typeof(Toils_Combat).GetNestedTypes(BindingFlags.NonPublic).SelectMany(t=>AccessTools.GetDeclaredMethods(t)).FirstOrDefault(m=>m.Name.StartsWith("<GotoCastPosition>"));
  static bool Prepare()=>Target()!=null;
  static MethodBase TargetMethod()=>Target();
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)=>CastRange.Swap(code);
 }
 [HarmonyPatch(typeof(CastPositionFinder),nameof(CastPositionFinder.TryFindCastPosition))]
 public static class CastRangeFinder {
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)=>CastRange.Swap(code);
 }
}
