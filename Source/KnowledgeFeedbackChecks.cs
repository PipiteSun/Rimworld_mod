using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public partial class LichChecks {
  static string feedbackScholarId;static double feedbackSaved;
  static SkillRecord echoOwner,echoSource;static bool echoOnce;
  static void EchoLearning(SkillRecord __instance){if(!echoOnce || __instance!=echoOwner)return;echoOnce=false;echoSource.Learn(20,false,true);}
  class LearningState {
   SkillRecord skill;int level;float xp,today;Passion passion;
   public LearningState(SkillRecord s){skill=s;level=s.levelInt;xp=s.xpSinceLastLevel;today=s.xpSinceMidnight;passion=s.passion;}
   public void Restore(){skill.levelInt=level;skill.xpSinceLastLevel=xp;skill.xpSinceMidnight=today;skill.passion=passion;}
  }
  static void ResetLearning(SkillRecord s,int level=4,float xp=100){s.levelInt=level;s.xpSinceLastLevel=xp;s.xpSinceMidnight=0;s.passion=Passion.Minor;}
  // 1.63.0：原来这组时好时坏，根因是 Exalted.cs 的举一反三后缀（见那里的注释），不是检查本身。
  void CheckKnowledgeFeedback(){
   var scholars=Necro.LivingConstructs(worker).Where(p=>Necro.Construct(p).kind=="Scholar").Take(2).ToList();
   var pawn=scholars[0];var h=Necro.Construct(pawn);var skill=pawn.skills.GetSkill(SkillDefOf.Crafting);var owner=worker.skills.GetSkill(SkillDefOf.Crafting);var other=worker.skills.GetSkill(SkillDefOf.Medicine);var second=scholars[1].skills.GetSkill(SkillDefOf.Crafting);
   var skeleton=Necro.LivingConstructs(worker).First(p=>Necro.Construct(p).kind=="Skeleton").skills.GetSkill(SkillDefOf.Crafting);
   var golem=Necro.LivingConstructs(worker).First(p=>Necro.Construct(p).kind=="Golem").skills.GetSkill(SkillDefOf.Crafting);
   var states=new[]{skill,owner,second,skeleton,golem}.Select(s=>new LearningState(s)).ToList();
   float focus=worker.psychicEntropy.CurrentPsyfocus;worker.psychicEntropy.OffsetPsyfocusDirectly(1);h.Maintain(0);Necro.Construct(scholars[1]).Maintain(0);
   try{
    ResetLearning(skill);ResetLearning(owner);owner.passion=Passion.Major;double sourceBefore=KnowledgeFeedback.Experience(skill),before=KnowledgeFeedback.Experience(owner),unrelated=KnowledgeFeedback.Experience(other);float ownRate=skill.LearnRateFactor(false);
    skill.Learn(100);
    Check(KnowledgeFeedback.Experience(skill)==sourceBefore && skill.levelInt==4,"knowledge undead skill stays fixed like a mechanoid");
    Check(ownRate>0 && Math.Abs(KnowledgeFeedback.Experience(owner)-before-100*ownRate)<.05,"owner receives the scholar's would-be XP without multiplying its own double passion");
    Check(KnowledgeFeedback.Experience(other)==unrelated,"feedback changes only matching owner skill (medicine "+unrelated.ToString("0.#")+" → "+KnowledgeFeedback.Experience(other).ToString("0.#")+", level "+other.levelInt+", exalted mask "+Exalted.MaskOf(worker)+")");
    ResetLearning(second);before=KnowledgeFeedback.Experience(owner);sourceBefore=KnowledgeFeedback.Experience(second);float secondRate=second.LearnRateFactor(false);second.Learn(75);
    Check(KnowledgeFeedback.Experience(second)==sourceBefore && Math.Abs(KnowledgeFeedback.Experience(owner)-before-75*secondRate)<.05,"another scholar independently feeds the same master");
    ResetLearning(skill);skill.passion=Passion.None;float noneRate=skill.LearnRateFactor(false);skill.passion=Passion.Major;float majorRate=skill.LearnRateFactor(false);before=KnowledgeFeedback.Experience(owner);skill.Learn(100);double majorGain=KnowledgeFeedback.Experience(owner)-before;
    // 完整模组列表里有 Vanilla Skills Expanded 改兴趣与学习倍率的算法，只在核心环境里要求精确值。
    Check(FullModList?majorGain>0:majorRate>noneRate && Math.Abs(majorGain-100*majorRate)<.05,"scholar passion only scales XP forwarded to the master: "+majorGain.ToString("0.##")+" vs "+(100*majorRate).ToString("0.##"));
    ResetLearning(skill,4,SkillRecord.XpRequiredToLevelUpFrom(4)-10);ResetLearning(owner,4,SkillRecord.XpRequiredToLevelUpFrom(4)-5);sourceBefore=KnowledgeFeedback.Experience(skill);before=KnowledgeFeedback.Experience(owner);skill.Learn(60,false,true);
    Check(skill.levelInt==4 && KnowledgeFeedback.Experience(skill)==sourceBefore && owner.levelInt==5 && Math.Abs(KnowledgeFeedback.Experience(owner)-before-60)<.05,"scholar stays at its level while owner crosses a level with exact feedback XP");
    before=KnowledgeFeedback.Experience(owner);sourceBefore=KnowledgeFeedback.Experience(skill);skill.Learn(-20,false,true);skill.Learn(0);
    Check(KnowledgeFeedback.Experience(owner)==before && KnowledgeFeedback.Experience(skill)==sourceBefore,"negative and zero XP change neither scholar nor owner");
    ResetLearning(skill,15,1000);sourceBefore=KnowledgeFeedback.Experience(skill);skill.Interval();
    Check(KnowledgeFeedback.Experience(skill)==sourceBefore,"scholar skills do not decay");
    ResetLearning(skill,20,SkillRecord.XpRequiredToLevelUpFrom(20)-1);ResetLearning(owner);before=KnowledgeFeedback.Experience(owner);skill.Learn(100,false,true);
    Check(skill.levelInt==20 && Math.Abs(KnowledgeFeedback.Experience(owner)-before-100)<.05,"maxed scholar still forwards its work XP");
    ResetLearning(skill);ResetLearning(owner,20,SkillRecord.XpRequiredToLevelUpFrom(20)-1);double returned=h.knowledgeReturned;skill.Learn(100,false,true);
    Check(owner.levelInt==20 && Near(owner.xpSinceLastLevel,SkillRecord.XpRequiredToLevelUpFrom(20)-1) && h.knowledgeReturned==returned,"owner respects native cap and counts only retained experience (level "+owner.levelInt+", xp "+owner.xpSinceLastLevel.ToString("0.#")+" / cap "+(SkillRecord.XpRequiredToLevelUpFrom(20)-1)+", returned "+returned.ToString("0.##")+" → "+h.knowledgeReturned.ToString("0.##")+")");
    ResetLearning(owner);h.dormant=true;before=KnowledgeFeedback.Experience(owner);sourceBefore=KnowledgeFeedback.Experience(skill);skill.Learn(100,false,true);
    Check(KnowledgeFeedback.Experience(owner)==before && KnowledgeFeedback.Experience(skill)==sourceBefore,"dormant scholar cannot feed owner and still does not learn");h.dormant=false;
    worker.psychicEntropy.OffsetPsyfocusDirectly(-1);skill.Learn(100,false,true);
    Check(KnowledgeFeedback.Experience(owner)==before,"loss of control at zero focus pauses feedback immediately");worker.psychicEntropy.OffsetPsyfocusDirectly(1);
    var original=h.master;h.master=pawn;skill.Learn(100,false,true);Check(KnowledgeFeedback.Experience(owner)==before,"invalid self-master link cannot feed experience");h.master=original;
    ResetLearning(skeleton);ResetLearning(golem);skeleton.Learn(100,false,true);golem.Learn(100,false,true);
    Check(KnowledgeFeedback.Experience(owner)==before,"skeleton and golem learning do not feed owner");
    var probe=new Harmony("local.lich.feedbacktest");var learn=AccessTools.Method(typeof(SkillRecord),nameof(SkillRecord.Learn));
    try{
     echoOwner=owner;echoSource=skill;echoOnce=true;probe.Patch(learn,postfix:new HarmonyMethod(typeof(LichChecks),nameof(EchoLearning)));
     before=KnowledgeFeedback.Experience(owner);sourceBefore=KnowledgeFeedback.Experience(skill);skill.Learn(100,false,true);
     Check(!echoOnce && Math.Abs(KnowledgeFeedback.Experience(owner)-before-100)<.05 && KnowledgeFeedback.Experience(skill)==sourceBefore,"nested learning callback cannot recursively feed the owner or teach the scholar");
    }finally{echoOnce=false;echoOwner=null;echoSource=null;probe.Unpatch(learn,HarmonyPatchType.Postfix,probe.Id);}
   }finally{foreach(var state in states)state.Restore();h.dormant=false;worker.psychicEntropy.OffsetPsyfocusDirectly(focus-worker.psychicEntropy.CurrentPsyfocus);}
   feedbackScholarId=pawn.ThingID;feedbackSaved=h.knowledgeReturned;
   Check(feedbackSaved>0 && h.FeedbackDescription.Contains("100%"),"scholar health description reports feedback rule and cumulative XP");
  }
  void CheckFeedbackAfterLoad(){var pawn=Necro.Registry.constructs.Single(p=>p.ThingID==feedbackScholarId);Check(Math.Abs(Necro.Construct(pawn).knowledgeReturned-feedbackSaved)<.05,"feedback counter survives full save/load without retroactive awards");}
 }
}
