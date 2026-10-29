using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.22.0 炼金桌：配方速度按智识与医疗的平均等级算；经验在两项技能间平分。
 public class AlchemyRecipeExtension : DefModExtension {
  public bool potion;   // 药水配方（句芒联动：草药消耗减半）
 }
 public static class Alchemy {
  public const float BaseValue=.3f,PerLevel=.0875f;
  public static float AverageSkill(Pawn p){if(p?.skills==null)return 0;return (p.skills.GetSkill(SkillDefOf.Intellectual).Level+p.skills.GetSkill(SkillDefOf.Medicine).Level)/2f;}
  public static float SkillFactor(Pawn p)=>BaseValue+PerLevel*AverageSkill(p);
  public static bool IsAlchemy(RecipeDef r)=>r?.GetModExtension<AlchemyRecipeExtension>()!=null;
 }
 public class StatPart_AlchemySkill : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && p.skills!=null)val*=Alchemy.SkillFactor(p);}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && p.skills!=null?"智识与医疗平均等级"+Alchemy.AverageSkill(p).ToString("0.#")+"：×"+Alchemy.SkillFactor(p).ToStringPercent():null;
 }
 // 做炼金配方时，智识经验的一半转给医疗。
 [HarmonyPatch(typeof(Pawn_SkillTracker),nameof(Pawn_SkillTracker.Learn))]
 public static class AlchemySplitXp {
  [ThreadStatic] static bool busy;
  static void Prefix(Pawn_SkillTracker __instance,SkillDef __0,ref float __1,bool __2,bool __3,Pawn ___pawn){
   if(busy || __2 || __1<=0 || __0!=SkillDefOf.Intellectual || !Alchemy.IsAlchemy(___pawn?.CurJob?.bill?.recipe))return;
   float half=__1*.5f;__1=half;busy=true;try{__instance.Learn(SkillDefOf.Medicine,half,false,__3);}finally{busy=false;}
  }
 }
}
