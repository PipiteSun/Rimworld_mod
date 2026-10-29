using System;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  void CheckExpandedLimbs(Hediff_Lich lich,BodyPartRecord leg){
   var hs=worker.health.hediffSet;var otherLeg=worker.RaceProps.body.AllParts.First(p=>p.def==leg.def && p!=leg);
   Check(Near(hs.GetPartHealth(leg),Mathf.Ceil(leg.def.GetMaxHealth(worker)*1.4f)),"two limb layers add 40 percent native part health");
   Check(Near(hs.GetPartHealth(otherLeg),otherLeg.def.GetMaxHealth(worker)),"reinforcing one leg does not change the other leg; hp="+hs.GetPartHealth(otherLeg)+" max="+PartHealth.Maximum(otherLeg,worker)+" base="+otherLeg.def.GetMaxHealth(worker)+" injuries="+string.Join(",",hs.hediffs.Where(h=>h.Part==otherLeg).Select(h=>h.def.defName+":"+h.Severity)));
   Check(!lich.Strengthen(null),"invalid part cannot be strengthened");
   foreach(string small in new[]{"Finger","Toe","Hand","Foot","Nose","Jaw","Tongue","Skull","Ribcage","Neck","Shoulder","Pelvis"}){
    var part=worker.RaceProps.body.AllParts.First(p=>p.def.defName==small);float focus=lich.Points,reservedBefore=lich.Reserved;
    Check(!lich.StrengthenableParts.Contains(part) && !lich.Strengthen(part) && Near(focus,lich.Points) && Near(reservedBefore,lich.Reserved),"small part excluded without spending focus: "+small);
   }
   var finger=worker.RaceProps.body.AllParts.First(p=>p.def.defName=="Finger");float reservedBeforeMigration=lich.Reserved;
   var legacy=(Hediff_Limb)worker.health.AddHediff(Necro.Def<HediffDef>("Limb"),finger);legacy.Severity=5;
   float original=finger.def.GetMaxHealth(worker),boosted=PartHealth.Maximum(finger,worker);
   var wound=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,worker,finger);wound.Severity=original+1;worker.health.AddHediff(wound);float woundRatio=wound.Severity/boosted;
   Check(lich.RemoveObsoleteEnhancements()==1 && !hs.hediffs.Contains(legacy) && Near(lich.Reserved,reservedBeforeMigration),"old small-part upgrade removed and reservation released");
   Check(!hs.PartIsMissing(finger) && Near(wound.Severity/original,woundRatio) && Near(PartHealth.Maximum(finger,worker),original),"migration preserves injured small-part health proportion without amputation");
   float converted=wound.Severity;Check(lich.RemoveObsoleteEnhancements()==0 && Near(wound.Severity,converted),"migration is idempotent and cannot repeatedly heal wounds");worker.health.RemoveHediff(wound);
   var brain=hs.GetBrain();float baseline=brain.def.GetMaxHealth(worker);
   foreach(string name in new[]{"Head","Heart","Torso","Eye","Spine","Lung","Liver","Kidney","Stomach","Ear","Arm"}){
    var part=worker.RaceProps.body.AllParts.First(p=>p.def.defName==name);worker.psychicEntropy.OffsetPsyfocusDirectly(1);
    Check(lich.StrengthenableParts.Contains(part) && lich.Strengthen(part),"can select and strengthen "+name);
    Check(Near(hs.GetPartHealth(part),Mathf.Ceil(part.def.GetMaxHealth(worker)*1.2f)),"native extra health applies to "+name);
    var buff=hs.hediffs.OfType<Hediff_Limb>().Single(h=>h.Part==part);Check(lich.Release(buff) && Near(hs.GetPartHealth(part),part.def.GetMaxHealth(worker)),"release restores original maximum for "+name);
   }
   worker.psychicEntropy.OffsetPsyfocusDirectly(1);worker.psychicEntropy.RemoveAllEntropy();
   for(int i=0;i<5;i++)Check(lich.Strengthen(brain),"brain reinforcement layer "+(i+1));
   Check(!lich.Strengthen(brain) && Near(hs.GetPartHealth(brain),Mathf.Ceil(baseline*2)),"brain caps at five layers and double health");
   Check(PawnCapacityUtility.CalculatePartEfficiency(hs,brain)<=1.61f,"max-health bonus does not inflate part efficiency twice");
   var damage=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,worker,brain);damage.Severity=baseline+1;worker.health.AddHediff(damage);
   Check(!worker.Dead && !hs.PartIsMissing(brain) && Near(hs.GetPartHealth(brain),Mathf.Ceil(baseline*2)-damage.Severity),"brain survives wounds exceeding its unreinforced maximum");
   var brainBuff=hs.hediffs.OfType<Hediff_Limb>().Single(h=>h.Part==brain);Check(!lich.Release(brainBuff),"unsafe release is blocked on critically injured brain");
   worker.health.RemoveHediff(damage);float reserved=lich.Reserved;Check(lich.Release(brainBuff) && Near(lich.Reserved,reserved-25) && Near(hs.GetPartHealth(brain),baseline),"release frees reserved focus and resets brain maximum");
   worker.psychicEntropy.OffsetPsyfocusDirectly(1);Check(lich.Strengthen(brain) && lich.Strengthen(brain),"brain enhancement prepared for save/load");
   var absent=worker.RaceProps.body.AllParts.First(p=>p.def.defName=="Ear");worker.health.AddHediff(HediffDefOf.MissingBodyPart,absent);
   Check(!lich.StrengthenableParts.Contains(absent) && !lich.Strengthen(absent),"missing body part is excluded and cannot be strengthened");worker.health.RestorePart(absent);
  }
 }
}