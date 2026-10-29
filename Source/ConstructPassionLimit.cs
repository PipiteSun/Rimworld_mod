using System;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public partial class ConstructDesign {
  public const int MajorPassionLimit=3;
  public const string PassionLimitReason="双火最多选择3项，请先将其他双火调整为单火或无兴趣。";
  public SkillDef SpecialtySkill=>specialty=="大脑"?SkillDefOf.Intellectual:specialty=="眼睛"?SkillDefOf.Crafting:specialty=="手臂"?SkillDefOf.Construction:SkillDefOf.Medicine;
  public int MajorCount(string kind)=>DefDatabase<SkillDef>.AllDefsListForReading.Count(s=>PassionFor(s,kind)==Passion.Major);
  public bool TrySetPassion(SkillDef skill,Passion value,string kind){if(value==Passion.Major && PassionFor(skill,kind)!=Passion.Major && MajorCount(kind)>=MajorPassionLimit)return false;passions[skill]=value;return true;}
  public bool TrySetSpecialty(string value,string kind,bool forceMajor){var next=Copy();next.specialty=value;if(forceMajor)next.passions[next.SpecialtySkill]=Passion.Major;if(next.MajorCount(kind)>MajorPassionLimit)return false;specialty=next.specialty;passions=next.passions;return true;}
  public void LimitPassions(string kind){foreach(var skill in DefDatabase<SkillDef>.AllDefsListForReading.Where(s=>PassionFor(s,kind)==Passion.Major).OrderByDescending(s=>kind=="Scholar" && s==SpecialtySkill).ThenBy(s=>s.listOrder).ThenBy(s=>s.defName).Skip(MajorPassionLimit).ToList())passions[skill]=Passion.Minor;}
 }
 public partial class Hediff_Construct {
  public void EnforcePassionLimit(){
   if(pawn?.skills==null)return;
   remoldDraft?.LimitPassions(kind);
   int major=0;foreach(var skill in pawn.skills.skills)if(skill.passion==Passion.Major)major++;
   if(major<=ConstructDesign.MajorPassionLimit)return;
   var main=kind=="Scholar"?new ConstructDesign{specialty=specialty}.SpecialtySkill:null;
   var excess=pawn.skills.skills.Where(s=>s.passion==Passion.Major).OrderByDescending(s=>s.def==main).ThenByDescending(s=>s.Level).ThenBy(s=>s.def.listOrder).ThenBy(s=>s.def.defName).Skip(ConstructDesign.MajorPassionLimit).ToList();
   foreach(var skill in excess){skill.passion=Passion.Minor;if(design!=null)design.passions[skill.def]=Passion.Minor;}
  }
 }
}
