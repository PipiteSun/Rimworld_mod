using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.27.1 胃容量（1.167.0 移除力能龙后去掉了“龙之长眠”）。
 // 胃容量倍率 F 乘在原版最大营养上（原版 MaxNutrition = 体型 × 生命阶段 foodMaxFactor，已含原版体型）：
 // F=max(1, max(巨人血统,巨魔,HAR 种族)×小体型扩展)，构造体为1。
 // 原版 Need_Food.MaxLevel 在游戏中读 MaxNutrition 属性（15刻缓存），不在游戏中（生成开局角色、读档）直接算 体型×foodMaxFactor，这里两边都乘 F。
 // 原版阈值：想吃 = FoodLevelPercentageWantEat（杂食/肉食 30%），饥饿 = ×0.8，非常饿 = ×0.4，≤0 挨饿；饥饿/非常饿/挨饿时消耗×0.5/×0.25/×0。
 // 大胃种族阈值仍按原版百分比，所以一顿撑得更久。
 public static class Satiety {
  public const float Epsilon=.0001f;
  public static float Compute(Pawn p){
   if(p?.genes==null || Necro.Construct(p)!=null)return 1f;
   // 1.30.0：火巨人一项改为所有巨人血统（GiantKin）：成年霜巨人也是×200%，儿童随年龄长大。
   // 1.44.0：HAR 巨人种族的胃容量写在种族扩展上（儿童随年龄），与巨人血统、巨魔取较大者。
   return Mathf.Max(1f,Mathf.Max(Mathf.Max(GiantKin.Size(p),TrollAppearance.Size(p)),RaceSize.Stomach(p))*SmallBody.Size(p));
  }

  // 饥饿分类每刻都会查很多次：按角色缓存60刻；基因、生命阶段变化时立即失效。
  sealed class State {public float f=1f;public int tick=int.MinValue;}
  static readonly ConditionalWeakTable<Pawn,State> states=new ConditionalWeakTable<Pawn,State>();
  const int StaleTicks=60;
  static State Get(Pawn p){
   var s=states.GetOrCreateValue(p);var tm=Current.Game?.tickManager;int now=tm?.TicksGame??0;
   if(tm==null || s.tick==int.MinValue || now<s.tick || now-s.tick>=StaleTicks){s.f=Compute(p);s.tick=tm!=null?now:int.MinValue;}
   return s;
  }
  public static void Invalidate(Pawn p){if(p!=null && states.TryGetValue(p,out var s))s.tick=int.MinValue;}
  public static float Factor(Pawn p)=>p?.genes==null?1f:Get(p).f;
  // 常人百分比：相对于没有本规则时的原版最大营养。
  public static float CommonPercent(Need_Food food,Pawn p)=>food.CurLevel/(food.MaxLevel/Factor(p));

  // F 变化时按“新F/旧F”缩放饱食，显示的百分比不突降；旧存档等待迁移（版本0）和读档过程中不动。
  public static void Sync(Pawn p,bool invalidate){
   var food=p?.needs?.food;var registry=Necro.Registry;
   if(food==null || registry==null || registry.satietyVersion==0 || Scribe.mode!=LoadSaveMode.Inactive)return;
   if(invalidate)Invalidate(p);
   float f=Factor(p),old=registry.RecordedStomach(p);
   if(Math.Abs(f-old)<Epsilon)return;
   StatDefOf.MaxNutrition.Worker.ClearCacheForThing(p);
   food.CurLevel=food.CurLevel*f/old;
   registry.RecordStomach(p,f);
  }
 }
 public partial class Registry {
  // -1：新开局（构造时）；0：1.27.0 及更早的存档（读档时没有这个字段）；1：已记录。只记 F≠1 的角色，键是 thingIDNumber。
  public int satietyVersion=-1;public Dictionary<int,float> stomachFactors=new Dictionary<int,float>();
  void ExposeSatiety(){
   Scribe_Values.Look(ref satietyVersion,"satietyVersion",0);
   Scribe_Collections.Look(ref stomachFactors,"stomachFactors",LookMode.Value,LookMode.Value);
   if(stomachFactors==null)stomachFactors=new Dictionary<int,float>();
  }
  public float RecordedStomach(Pawn p)=>stomachFactors.TryGetValue(p.thingIDNumber,out var f)?f:1f;
  public void RecordStomach(Pawn p,float f){if(Math.Abs(f-1f)<Satiety.Epsilon)stomachFactors.Remove(p.thingIDNumber);else stomachFactors[p.thingIDNumber]=f;}
  // 旧存档首次载入：F≠1 的角色按 F 放大饱食一次（不超过新上限）。pawns 为空时处理所有角色；检查只传测试角色。
  public int MigrateSatiety(IEnumerable<Pawn> pawns=null){
   if(satietyVersion!=0){satietyVersion=1;return 0;}
   satietyVersion=1;int scaled=0;
   foreach(var p in (pawns??BodySize.AllPawns()).ToList()){
    var food=p?.needs?.food;if(food==null)continue;
    Satiety.Invalidate(p);float f=Satiety.Compute(p);RecordStomach(p,f);if(Math.Abs(f-1f)<Satiety.Epsilon)continue;
    StatDefOf.MaxNutrition.Worker.ClearCacheForThing(p);food.CurLevel=Mathf.Min(food.CurLevel*f,food.MaxLevel);scaled++;
   }
   if(scaled>0)Log.Message("[DND边缘大陆] 胃容量迁移：按胃容量倍率放大了".Loc()+scaled+"个角色的饱食度。".Loc());
   return scaled;
  }
  // 读档时 FinalizeInit 还不在游戏状态，等载入结束再迁移；新开局直接记为已记录。
  void FinalizeSatiety(){if(satietyVersion==0)LongEventHandler.ExecuteWhenFinished(()=>MigrateSatiety());else satietyVersion=1;}
 }

 public class StatPart_Stomach : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p)val*=Satiety.Factor(p);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;float f=Satiety.Factor(p);return f==1f?null:"巨胃：×".Loc()+f.ToStringPercent();}
 }
 [StaticConstructorOnStartup]
 public static class SatietyStartup {
  static SatietyStartup(){
   var stat=StatDefOf.MaxNutrition;if(stat.parts==null)stat.parts=new List<StatPart>();
   if(!stat.parts.Any(p=>p is StatPart_Stomach))stat.parts.Add(new StatPart_Stomach{parentStat=stat});
  }
 }

 // ==================== Harmony 补丁 ====================

 // 不在游戏中时原版不读属性，补乘 F，使生成开局角色、读档时的上限与游戏中一致。
 [HarmonyPatch(typeof(Need_Food),"get_MaxLevel")]
 public static class SatietyMaxLevel {
  static void Postfix(Pawn ___pawn,ref float __result){if(Current.ProgramState!=ProgramState.Playing && ___pawn?.genes!=null)__result*=Satiety.Compute(___pawn);}
 }
 // 新需求按当前 F 记录（初始饱食已按含 F 的上限设置），以后只缩放变化的部分。
 [HarmonyPatch(typeof(Need_Food),nameof(Need_Food.SetInitialLevel))]
 public static class SatietyInitialLevel {
  static void Postfix(Pawn ___pawn){var registry=Necro.Registry;if(___pawn?.genes!=null && registry!=null && registry.satietyVersion!=0){Satiety.Invalidate(___pawn);registry.RecordStomach(___pawn,Satiety.Compute(___pawn));}}
 }
 // 每150刻：兜底同步 F（儿童随年龄长大）。
 [HarmonyPatch(typeof(Need_Food),nameof(Need_Food.NeedInterval))]
 public static class SatietyInterval {
  static void Postfix(Need_Food __instance,Pawn ___pawn){if(___pawn?.genes==null)return;Satiety.Sync(___pawn,false);}
 }
 // 儿童成年时立即同步。
 [HarmonyPatch(typeof(Pawn_AgeTracker),"RecalculateLifeStageIndex")]
 public static class SatietyLifeStage {
  static void Postfix(Pawn ___pawn){if(___pawn?.genes!=null && Current.ProgramState==ProgramState.Playing)Satiety.Sync(___pawn,true);}
 }

 [HarmonyPatch(typeof(Need_Food),nameof(Need_Food.GetTipString))]
 public static class SatietyTip {
  static void Postfix(Need_Food __instance,Pawn ___pawn,ref string __result){
   float f=Satiety.Factor(___pawn);if(Math.Abs(f-1f)<Satiety.Epsilon)return;
   __result+="\n\n相当于常人饱食度".Loc()+Satiety.CommonPercent(__instance,___pawn).ToStringPercent()+"（上限".Loc()+f.ToStringPercent()+"）".Loc();
  }
 }
}
