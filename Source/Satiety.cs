using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.27.1 胃容量与“龙之长眠”。
 // 胃容量倍率 F 乘在原版最大营养上（原版 MaxNutrition = 体型 × 生命阶段 foodMaxFactor，已含原版体型）：
 // 成年力能龙裔 F=5；其他角色 F=max(1, max(火巨人,巨魔)×小体型扩展)，不算龙龄那一项，构造体为1。
 // 原版 Need_Food.MaxLevel 在游戏中读 MaxNutrition 属性（15刻缓存），不在游戏中（生成开局角色、读档）直接算 体型×foodMaxFactor，这里两边都乘 F。
 // 原版阈值：想吃 = FoodLevelPercentageWantEat（杂食/肉食 30%），饥饿 = ×0.8，非常饿 = ×0.4，≤0 挨饿；饥饿/非常饿/挨饿时消耗×0.5/×0.25/×0。
 // 大胃种族阈值仍按原版百分比，所以一顿撑得更久；成年龙按“常人百分比”P=CurLevel/(MaxLevel/5)判定：P<225% 想吃（仍是吃饱），P<100% 饥饿，P<40% 非常饿。
 public static class Satiety {
  public const float DragonStomach=5f,WantEatP=2.25f,HungryP=1f,UrgentP=.4f,BingeFull=.95f,LongSleepDays=3f,FoodPerDay=1.6f,Epsilon=.0001f;
  public const int DragonMealsAtOnce=3; // 成年龙一次最多吃下3份有单次上限的食物（原版饭菜一次1份）
  public static float DragonWantEat=>WantEatP/DragonStomach;
  public static float DragonHungry=>HungryP/DragonStomach;
  public static float DragonUrgent=>UrgentP/DragonStomach;
  // 成年龙总饥饿速率：从500%饿到225%用 LongSleepDays 天。原版每天营养 = 1.6 × 饥饿速率，常人胃容量 = 龙龄体型（1.0~1.2）。
  public static float DragonHunger(int age)=>(DragonStomach-WantEatP)*ForceDragon.SizeFactor(Mathf.Clamp(age,1,ForceDragon.MaxAge))/(FoodPerDay*LongSleepDays);
  public static bool AdultDragon(Pawn p)=>ForceDragon.Active(p) && !ForceDragon.Juvenile(p);
  public static float Compute(Pawn p){
   if(p?.genes==null || Necro.Construct(p)!=null)return 1f;
   if(AdultDragon(p))return DragonStomach;
   // 1.30.0：火巨人一项改为所有巨人血统（GiantKin）：成年霜巨人也是×200%，儿童随年龄长大。
   // 1.44.0：HAR 巨人种族的胃容量写在种族扩展上（双头巨人 180%、独眼巨人 170%，儿童随年龄），与巨人血统、巨魔取较大者。
   return Mathf.Max(1f,Mathf.Max(Mathf.Max(GiantKin.Size(p),TrollAppearance.Size(p)),RaceSize.Stomach(p))*SmallBody.Size(p));
  }

  // 饥饿分类每刻都会查很多次：按角色缓存60刻；基因、龙龄、生命阶段变化时立即失效。
  sealed class State {public float f=1f;public bool dragon;public int tick=int.MinValue;}
  static readonly ConditionalWeakTable<Pawn,State> states=new ConditionalWeakTable<Pawn,State>();
  const int StaleTicks=60;
  static State Get(Pawn p){
   var s=states.GetOrCreateValue(p);var tm=Current.Game?.tickManager;int now=tm?.TicksGame??0;
   if(tm==null || s.tick==int.MinValue || now<s.tick || now-s.tick>=StaleTicks){s.f=Compute(p);s.dragon=s.f==DragonStomach && AdultDragon(p);s.tick=tm!=null?now:int.MinValue;}
   return s;
  }
  public static void Invalidate(Pawn p){if(p!=null && states.TryGetValue(p,out var s))s.tick=int.MinValue;}
  public static float Factor(Pawn p)=>p?.genes==null?1f:Get(p).f;
  public static bool Dragon(Pawn p)=>p?.genes!=null && Get(p).dragon;
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
  // 暴食：成年龙低于225%开始进食后一直吃到95%；不再是成年龙时退出。
  public static bool Binging(Pawn p,Need_Food food){
   var heart=ForceDragon.Heart(p);if(heart==null)return false;
   if(!Dragon(p)){heart.binging=false;return false;}
   float pct=food.CurLevelPercentage;
   if(pct>=BingeFull)heart.binging=false;else if(pct<DragonWantEat)heart.binging=true;
   return heart.binging;
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
   if(scaled>0)Log.Message("[DND边缘大陆] 胃容量迁移：按胃容量倍率放大了"+scaled+"个角色的饱食度。");
   return scaled;
  }
  // 读档时 FinalizeInit 还不在游戏状态，等载入结束再迁移；新开局直接记为已记录。
  void FinalizeSatiety(){if(satietyVersion==0)LongEventHandler.ExecuteWhenFinished(()=>MigrateSatiety());else satietyVersion=1;}
 }

 public class StatPart_Stomach : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p)val*=Satiety.Factor(p);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;float f=Satiety.Factor(p);return f==1f?null:(Satiety.Dragon(p)?"龙之胃：×":"巨胃：×")+f.ToStringPercent();}
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
 // 每150刻：兜底同步 F（儿童随年龄长大），并更新龙的暴食状态。
 [HarmonyPatch(typeof(Need_Food),nameof(Need_Food.NeedInterval))]
 public static class SatietyInterval {
  static void Postfix(Need_Food __instance,Pawn ___pawn){if(___pawn?.genes==null)return;Satiety.Sync(___pawn,false);Satiety.Binging(___pawn,__instance);}
 }
 // 儿童成年（力能龙 F 从1跳到5）时立即同步。
 [HarmonyPatch(typeof(Pawn_AgeTracker),"RecalculateLifeStageIndex")]
 public static class SatietyLifeStage {
  static void Postfix(Pawn ___pawn){if(___pawn?.genes!=null && Current.ProgramState==ProgramState.Playing)Satiety.Sync(___pawn,true);}
 }
 // 成年龙的饥饿分类与阈值按常人百分比。
 [HarmonyPatch(typeof(Need_Food),"get_CurCategory")]
 public static class SatietyCategory {
  static bool Prefix(Need_Food __instance,Pawn ___pawn,ref HungerCategory __result){
   if(!Satiety.Dragon(___pawn))return true;
   float pct=__instance.CurLevelPercentage;
   __result=pct<=0f?HungerCategory.Starving:pct<Satiety.DragonUrgent?HungerCategory.UrgentlyHungry:pct<Satiety.DragonHungry?HungerCategory.Hungry:HungerCategory.Fed;
   return false;
  }
 }
 [HarmonyPatch(typeof(Need_Food),"get_PercentageThreshHungry")]
 public static class SatietyThreshHungry {
  static void Postfix(Pawn ___pawn,ref float __result){if(Satiety.Dragon(___pawn))__result=Satiety.DragonHungry;}
 }
 [HarmonyPatch(typeof(Need_Food),"get_PercentageThreshUrgentlyHungry")]
 public static class SatietyThreshUrgent {
  static void Postfix(Pawn ___pawn,ref float __result){if(Satiety.Dragon(___pawn))__result=Satiety.DragonUrgent;}
 }
 // 原版：CurLevelPercentage < FoodLevelPercentageWantEat 时优先级9.5。成年龙低于225%或暴食中也给9.5；征召、精神崩溃、倒地时不强制。
 [HarmonyPatch(typeof(JobGiver_GetFood),nameof(JobGiver_GetFood.GetPriority))]
 public static class SatietyGetFood {
  public const float Priority=9.5f;
  static void Postfix(Pawn __0,ref float __result,HungerCategory ___minCategory,float ___maxLevelPercentage){
   if(__result!=0f)return;
   var food=__0?.needs?.food;if(food==null || !Satiety.Dragon(__0) || !Satiety.Binging(__0,food))return;
   if(__0.Drafted || __0.InMentalState || __0.Downed)return;
   var category=food.CurCategory;
   if((category<HungerCategory.Starving && FoodUtility.ShouldBeFedBySomeone(__0)) || category<___minCategory || food.CurLevelPercentage>___maxLevelPercentage)return;
   __result=Priority;
  }
 }
 // 一次吃很多：有单次上限的食物（原版饭菜1份）成年龙一次最多吃3份，仍受想要的营养量限制。
 [HarmonyPatch(typeof(FoodUtility),nameof(FoodUtility.WillIngestStackCountOf))]
 public static class SatietyStackCount {
  static void Postfix(Pawn __0,ThingDef __1,float __2,ref int __result){
   int cap=__1?.ingestible?.maxNumToIngestAtOnce??0;
   if(cap<=0 || __result<cap || __1.IsDrug || __2<=0f || !Satiety.Dragon(__0))return;
   __result=Mathf.Max(1,Mathf.Min(FoodUtility.StackCountForNutrition(__0.needs.food.NutritionWanted,__2),cap*Satiety.DragonMealsAtOnce));
  }
 }
 [HarmonyPatch(typeof(Thing),"IngestedCalculateAmounts")]
 public static class SatietyIngestAmount {
  static void Postfix(Thing __instance,Pawn __0,float __1,ref int __2,ref float __3){
   int cap=__instance.def.ingestible?.maxNumToIngestAtOnce??0;
   if(cap<=0 || __2<cap || __instance.def.IsDrug || !Satiety.Dragon(__0))return;
   float each=FoodUtility.NutritionForEater(__0,__instance);if(each<=0f)return;
   int n=Mathf.Clamp(Mathf.CeilToInt(__1/each),1,Mathf.Min(__instance.stackCount,cap*Satiety.DragonMealsAtOnce));
   if(n>__2){__2=n;__3=n*each;}
  }
 }
 [HarmonyPatch(typeof(Need_Food),nameof(Need_Food.GetTipString))]
 public static class SatietyTip {
  static void Postfix(Need_Food __instance,Pawn ___pawn,ref string __result){
   float f=Satiety.Factor(___pawn);if(Math.Abs(f-1f)<Satiety.Epsilon)return;
   __result+="\n\n相当于常人饱食度"+Satiety.CommonPercent(__instance,___pawn).ToStringPercent()+"（上限"+f.ToStringPercent()+"）";
   if(Satiety.Dragon(___pawn))__result+="\n龙之长眠：低于"+Satiety.WantEatP.ToStringPercent()+"才去找吃的，低于"+Satiety.HungryP.ToStringPercent()+"才会饥饿；开吃后一直吃到"+(Satiety.BingeFull*Satiety.DragonStomach).ToStringPercent()+(ForceDragon.Heart(___pawn)?.binging==true?"（正在暴食）":"")+"。";
  }
 }
}
