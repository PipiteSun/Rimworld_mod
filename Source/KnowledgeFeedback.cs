using System;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public static class KnowledgeFeedback {
  [ThreadStatic] static bool forwarding;
  public static double Experience(SkillRecord skill)=>(double)skill.XpTotalEarned+skill.xpSinceLastLevel;
  public static bool Connected(Hediff_Construct source)=>source?.kind=="Scholar" && source.pawn?.Dead==false && !source.pawn.Destroyed && !source.dormant && source.master!=source.pawn && source.master?.skills!=null && source.HasControl;
  public static bool Scholar(SkillRecord skill)=>Necro.Construct(skill?.Pawn)?.kind=="Scholar";
  // 知识死灵技能固定（像机械体）：从不改变自身经验。正经验按它自己的学习倍率（兴趣、学习速度、当日饱和）换算后全额传给主人。
  // 返回 true 表示拦截原版学习。等级只由制作和重塑直接设置。
  public static bool Intercept(SkillRecord skill,float amount,bool direct,bool ignoreLearnRate){
   if(!Scholar(skill))return false;
   if(forwarding || amount<=0 || float.IsNaN(amount) || float.IsInfinity(amount) || skill.TotallyDisabled)return true;
   float earned=ignoreLearnRate?amount:amount*skill.LearnRateFactor(direct);
   skill.xpSinceMidnight+=earned; // 只用于当日学习饱和，不是技能经验。
   Share(Necro.Construct(skill.Pawn),skill.def,earned);
   return true;
  }
  static void Share(Hediff_Construct source,SkillDef def,double earned){
   if(!Connected(source) || earned<=0)return;
   var target=source.master.skills.GetSkill(def);if(target==null || target.TotallyDisabled)return;
   double targetBefore=Experience(target);
   try{forwarding=true;target.Learn((float)earned,false,true);}
   finally{forwarding=false;}
   // Count only XP retained by the owner, including crossings of level boundaries.
   double received=Math.Max(0,Experience(target)-targetBefore);
   source.knowledgeReturned+=received;
  }
 }
 [HarmonyPatch(typeof(SkillRecord),nameof(SkillRecord.Learn))]
 public static class ScholarLearningFeedback {
  [HarmonyPriority(Priority.First)]
  static bool Prefix(SkillRecord __instance,float __0,bool __1,bool __2)=>!KnowledgeFeedback.Intercept(__instance,__0,__1,__2);
 }
 [HarmonyPatch(typeof(SkillRecord),nameof(SkillRecord.Interval))]
 public static class ScholarSkillDecay {static bool Prefix(SkillRecord __instance)=>!KnowledgeFeedback.Scholar(__instance);}
 public partial class Hediff_Construct {
  public double knowledgeReturned;
  public string FeedbackDescription=>kind!="Scholar"?"":"\n知识回馈：技能固定，不会因工作、学习或衰减改变；工作中本该获得的经验（按自身兴趣与学习倍率）100%传给主人对应技能，不重复乘算主人学习倍率。\n状态："+(KnowledgeFeedback.Connected(this)?"正在连接":"暂停（休眠或统御失效，经验不传递）")+"；累计回馈 "+knowledgeReturned.ToString("0.#")+" 经验。";
 }
}
