using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.81.0 热修（Peter：智力品质加成取消，改成器械师职业等级加成）：每个器械师等级期望 +0.1 档（10 级 +1、20 级 +2）；
 // 整数部分必升，小数部分按几率再升一档，最高传说。和大师工艺（DND 武器 +1 档）、熔岩之握等照常叠加。
 [HarmonyPatch]
 public static class ArtificerCraftQuality {
  public const float PerLevel=.1f;
  public static float? TestRoll;
  static System.Reflection.MethodBase TargetMethod()=>AccessTools.GetDeclaredMethods(typeof(QualityUtility)).Find(m=>m.Name==nameof(QualityUtility.GenerateQualityCreatedByPawn) && m.GetParameters().Length>=2 && m.GetParameters()[0].ParameterType==typeof(Pawn) && m.GetParameters()[1].ParameterType==typeof(SkillDef));
  public static float Tiers(Pawn p)=>p==null || !Adventurer.Enabled?0f:ClassSystem.Level(p,Artificer.Class)*PerLevel;
  public static QualityCategory Boost(Pawn p,QualityCategory q){
   if(q>=QualityCategory.Legendary)return q;float up=Tiers(p);if(up<=0f)return q;
   int n=Mathf.FloorToInt(up+.0001f);if((TestRoll??Rand.Value)<up-n)n++;
   return n<=0?q:(QualityCategory)Mathf.Min((int)QualityCategory.Legendary,(int)q+n);
  }
  static void Postfix(Pawn __0,ref QualityCategory __result){__result=Boost(__0,__result);}
 }
}
