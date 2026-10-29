using System;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public partial class LichChecks {
  void CheckPassionLimit(){
   foreach(var kind in new[]{"Skeleton","Golem","Scholar"}){
    var plan=new ConstructDesign();var skills=new[]{SkillDefOf.Intellectual,SkillDefOf.Crafting,SkillDefOf.Construction,SkillDefOf.Medicine};
    foreach(var s in skills.Take(3))Check(plan.TrySetPassion(s,Passion.Major,kind),"accept first three major passions: "+kind+" "+s.defName);
    Check(plan.MajorCount(kind)==3 && !plan.TrySetPassion(skills[3],Passion.Major,kind),"reject fourth major passion: "+kind);
    Check(plan.TrySetPassion(skills[0],Passion.Minor,kind) && plan.TrySetPassion(skills[3],Passion.Major,kind),"lowering one passion frees a slot: "+kind);
    foreach(var s in DefDatabase<SkillDef>.AllDefsListForReading.Where(s=>plan.PassionFor(s,kind)!=Passion.Major))Check(plan.TrySetPassion(s,Passion.Minor,kind),"minor passions remain unrestricted: "+kind+" "+s.defName);
    plan.passions[skills[0]]=Passion.Major;
    int before=Necro.Registry.constructs.Count;bool rejected=false;try{Constructs.Create(kind,worker,worker.Position,map,plan.specialty,plan);}catch(ArgumentException){rejected=true;}
    Check(rejected && Necro.Registry.constructs.Count==before,"direct creation cannot bypass cap or create extra pawn: "+kind);
    plan.LimitPassions(kind);Check(plan.MajorCount(kind)==3,"legacy draft caps effective passions: "+kind);
    var pawn=Necro.Registry.constructs.First(p=>!p.Dead && Necro.Construct(p).kind==kind);var h=Necro.Construct(pawn);
    CheckConstructInfoAndMood(pawn);
    var old=pawn.skills.skills.ToDictionary(s=>s,s=>s.passion);var xp=SkillSnapshot(pawn);var design=h.design?.Copy();var draft=h.remoldDraft?.Copy();
    try{
     foreach(var s in pawn.skills.skills)s.passion=Passion.Major;
     h.UpgradeColonist();
     Check(pawn.skills.skills.Count(s=>s.passion==Passion.Major)==3 && SameExperience(pawn,xp),"old pawn capped without changing levels or XP: "+kind);
     Check(kind!="Scholar" || pawn.skills.GetSkill(new ConstructDesign{specialty=h.specialty}.SpecialtySkill).passion==Passion.Major,"existing scholar specialty retained in migration: "+kind);
     h.EnforcePassionLimit();Check(pawn.skills.skills.Count(s=>s.passion==Passion.Major)==3,"cap enforcement is idempotent: "+kind);
    }finally{foreach(var s in old)s.Key.passion=s.Value;h.design=design;h.remoldDraft=draft;}
   }
   var specialty=new ConstructDesign();specialty.passions[SkillDefOf.Crafting]=Passion.Major;specialty.passions[SkillDefOf.Construction]=Passion.Major;
   Check(specialty.MajorCount("Scholar")==3,"implicit specialty double flame consumes one slot");
   specialty.passions[SkillDefOf.Intellectual]=Passion.Major;
   Check(!specialty.TrySetSpecialty("心脏","Scholar",true) && specialty.specialty=="大脑" && specialty.PassionFor(SkillDefOf.Medicine,"Scholar")==Passion.None,"specialty change cannot silently create a fourth major passion");
  }
 }
}
