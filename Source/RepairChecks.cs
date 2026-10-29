using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public partial class LichChecks {
  void CheckRepairScaling(Hediff_Lich lich){
   var pawn=lich.pawn;
   float baseline=lich.RepairMultiplier;
   var boostDef=new HediffDef{defName="LP_TestRepairSensitivity",hediffClass=typeof(Hediff),stages=new List<HediffStage>{new HediffStage{statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.PsychicSensitivity,value=2}}}}};
   var boost=pawn.health.AddHediff(boostDef);
   Check(Near(lich.RepairMultiplier,baseline*2),"repair reads increased live sensitivity");
   var injury=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,pawn,pawn.RaceProps.body.corePart);injury.Severity=10;pawn.health.AddHediff(injury);
   pawn.psychicEntropy.OffsetPsyfocusDirectly(1);lich.Maintain(250);
   Check(Near(injury.Severity,10-baseline*2),"doubled sensitivity doubles wound healing");
   pawn.health.RemoveHediff(injury);
   var arm=pawn.RaceProps.body.AllParts.First(p=>p.def.defName=="Arm");pawn.health.AddHediff(HediffDefOf.MissingBodyPart,arm);lich.regrowing=arm;lich.regrowth=0;
   lich.Maintain(250);Check(Near(lich.regrowth,250*baseline*2),"doubled sensitivity doubles regrowth progress");
   lich.regrowth=59999;float before=lich.Points;lich.Maintain(250);
   Check(!pawn.health.hediffSet.PartIsMissing(arm) && lich.Points>before-.01f,"last regrowth step charges only remaining progress");
   pawn.health.RemoveHediff(boost);
   Check(Near(lich.RepairMultiplier,baseline),"repair responds when sensitivity bonus is removed");
   CheckMultiWoundRepair(lich);CheckMendScaling();
  }
  void CheckMultiWoundRepair(Hediff_Lich lich){
   var pawn=lich.pawn;
   var boostDef=new HediffDef{defName="LP_TestMultiRepair",hediffClass=typeof(Hediff),stages=new List<HediffStage>{new HediffStage{statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.PsychicSensitivity,value=10}}}}};
   var boost=pawn.health.AddHediff(boostDef);
   float budget=lich.RepairMultiplier;
   var parts=new[]{pawn.RaceProps.body.corePart,pawn.RaceProps.body.AllParts.First(p=>p.def.defName=="Arm"),pawn.RaceProps.body.AllParts.First(p=>p.def.defName=="Leg")};
   var wounds=new List<Hediff_Injury>();
   for(int i=0;i<3;i++){var wound=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,pawn,parts[i]);wound.Severity=budget*(.6f-.1f*i);pawn.health.AddHediff(wound);wounds.Add(wound);}
   pawn.psychicEntropy.OffsetPsyfocusDirectly(1);float before=lich.Points;lich.Maintain(250);
   Check(wounds[0].Severity<=.001f && Near(wounds[1].Severity,budget*.1f) && Near(wounds[2].Severity,budget*.4f),"repair carries remaining budget from healed wound to next largest wound: budget="+budget+" wounds="+string.Join(",",wounds.Select(w=>w.Severity)));
   Check(Near(pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity),budget*.5f),"high sensitivity repair spends full budget across wounds");
   Check(Near(before-lich.Points,.5f),"multi-wound repair charges only one normal interval cost");
   pawn.psychicEntropy.OffsetPsyfocusDirectly(-1);lich.Maintain(250);
   Check(Near(wounds[1].Severity,budget*.1f) && Near(wounds[2].Severity,budget*.4f),"insufficient focus pauses whole multi-wound repair without free healing");
   pawn.psychicEntropy.OffsetPsyfocusDirectly(1);before=lich.Points;lich.Maintain(250);
   Check(pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity)<=.001f,"one repair interval finishes all remaining wounds");
   Check(Near(before-lich.Points,.25f),"finishing wounds charges only actual total healing");
   pawn.health.RemoveHediff(boost);
  }
  void CheckMendScaling(){
   var target=Necro.LivingConstructs(worker).First(p=>Necro.Construct(p).kind=="Golem");
   var ability=worker.abilities.GetAbility(Necro.Def<AbilityDef>("MendConstruct"));
   var effect=ability.EffectComps.OfType<CompNecromancy>().Single();
   float cost=ability.FinalPsyfocusCost(target),baseline=worker.GetStatValue(StatDefOf.PsychicSensitivity);
   foreach(float factor in new[]{1f,2f,3f,10f}){
    var boostDef=new HediffDef{defName="LP_TestMendSensitivity",hediffClass=typeof(Hediff),stages=new List<HediffStage>{new HediffStage{statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.PsychicSensitivity,value=factor}}}}};
    var boost=worker.health.AddHediff(boostDef);
    foreach(var old in target.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())target.health.RemoveHediff(old);
    for(int i=0;i<3;i++){var wound=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,target,target.RaceProps.body.corePart);wound.Severity=20;target.health.AddHediff(wound);}
    Check(Near(worker.GetStatValue(StatDefOf.PsychicSensitivity),baseline*factor),"mend reads live caster sensitivity x"+factor);
    Check(Near(ability.FinalPsyfocusCost(target),cost),"mend focus cost stays unchanged x"+factor);
    effect.Apply(target,LocalTargetInfo.Invalid);
    float remaining=target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity);
    Check(Near(60-remaining,System.Math.Min(60,15*baseline*factor)),"mend heals linearly across wounds without old cap x"+factor);
    worker.health.RemoveHediff(boost);
   }
   foreach(var wound in target.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())target.health.RemoveHediff(wound);
  }
 }
}
