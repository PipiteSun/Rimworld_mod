using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public partial class Hediff_Lich {
  // 1.33.0：六臂蛇人的蛇尾代替双腿，也能强化。1.44.0：双头巨人的第二个头、独眼巨人的独眼同样算主要部位。
  static readonly HashSet<string> MajorParts=new HashSet<string>{"Torso","Head","Brain","Spine","Heart","Lung","Liver","Kidney","Stomach","Eye","Ear","Arm","Leg","LP_SerpentTail","LP_EttinHead","LP_CyclopsEye"};
  public static bool IsMajorPart(BodyPartRecord part)=>part!=null && MajorParts.Contains(part.def.defName);
  public bool CanStrengthenPart(BodyPartRecord part)=>IsMajorPart(part) && pawn.RaceProps.body.AllParts.Contains(part) && !part.def.conceptual && !pawn.health.hediffSet.PartIsMissing(part);
  public IEnumerable<BodyPartRecord> StrengthenableParts=>pawn.RaceProps.body.AllParts.Where(CanStrengthenPart);
  public string StrengthenLabel(BodyPartRecord part){var h=pawn.health.hediffSet.hediffs.OfType<Hediff_Limb>().FirstOrDefault(x=>x.Part==part);return part.Label+"（"+(h?.Severity??0).ToString("0")+"/5层；生命 "+pawn.health.hediffSet.GetPartHealth(part).ToString("0")+"/"+PartHealth.Maximum(part,pawn).ToString("0")+"）：强化一层";}
  // Migrate retired small-part upgrades once. Scale wounds with the reduced maximum
  // so loading an old save cannot amputate a wounded finger or fully heal it.
  public int RemoveObsoleteEnhancements(){
   var set=pawn?.health?.hediffSet;if(set==null)return 0;
   List<Hediff_Limb> obsolete=null;foreach(var h in set.hediffs)if(h is Hediff_Limb limb && !IsMajorPart(limb.Part)){if(obsolete==null)obsolete=new List<Hediff_Limb>();obsolete.Add(limb);}if(obsolete==null)return 0;
   foreach(var group in obsolete.Where(h=>h.Part!=null).GroupBy(h=>h.Part)){
    var part=group.Key;float oldMax=PartHealth.Maximum(part,pawn),newMax=PartHealth.WithoutReinforcement(part,pawn);
    if(oldMax>newMax && !set.PartIsMissing(part))foreach(var injury in set.hediffs.OfType<Hediff_Injury>().Where(h=>h.Part==part).ToList())injury.Severity*=newMax/oldMax;
   }
   foreach(var h in obsolete)pawn.health.RemoveHediff(h);
   if(obsolete.Count>0)pawn.health.capacities.Notify_CapacityLevelsDirty();
   return obsolete.Count;
  }
  public bool Release(Hediff_Limb limb){
   if(limb==null || limb.pawn!=pawn || !pawn.health.hediffSet.hediffs.Contains(limb))return false;
   // Keep existing injuries unchanged: removing bonus health must never be a free heal or a lethal UI action.
   if(limb.Part!=null && !pawn.health.hediffSet.PartIsMissing(limb.Part) && pawn.health.hediffSet.GetPartHealth(limb.Part)<=PartHealth.Maximum(limb.Part,pawn)-PartHealth.WithoutReinforcement(limb.Part,pawn))return false;
   pawn.health.RemoveHediff(limb);Necro.Refresh(pawn);pawn.health.CheckForStateChange(null,null);return true;
  }
 }
 public static class PartHealth {
  // 1.23.0：火巨人与龙体的生命加成改由巨躯规则放进 HealthScale（BodySize.cs），原版 GetMaxHealth 已含在内。
  // 这里的旧系数只在迁移时测量 1.22.1 的旧最大值（火巨人、龙体各×1.5，相乘，只取整一次）。
  // 1.48.0：冒险者等级与体质的部位生命成长也乘在这里（与巫妖强化、巨躯规则相乘，只取整一次）。
  public static float WithoutReinforcement(BodyPartRecord part,Pawn pawn){float health=TemperingCompatibility.Apply(part.def.GetMaxHealth(pawn),part,pawn);float factor=BodySize.LegacyPartFactor(pawn)*Adventurer.PartHealthFactor(pawn)*Subraces.PartHealthFactor(pawn)*BeastLevels.PartHealthFactor(pawn);return factor!=1f?Mathf.Ceil(health*factor):health;}
  public static float Maximum(BodyPartRecord part,Pawn pawn){
   float original=WithoutReinforcement(part,pawn);
   var enhancements=pawn.health?.hediffSet?.hediffs;
   if(enhancements==null)return original;
   float layers=0;bool lich=false;
   for(int i=0;i<enhancements.Count;i++){var h=enhancements[i];if(h is Hediff_Lich)lich=true;else if(h is Hediff_Limb && h.Part==part)layers+=Math.Max(0,Math.Min(5,h.Severity));}
   return !lich || layers<=0?original:Mathf.Ceil(original*(1f+.2f*layers));
  }
 }
 // Vanilla max-health queries accept BodyPartDef, which is shared by the left and right limb.
 // Preserve the actual BodyPartRecord at each relevant native call site, keeping wounds,
 // capacity calculations, combat thresholds and the health UI on the same per-part maximum.
 [HarmonyPatch]
 public static class ReinforcedPartHealth {
  static IEnumerable<MethodBase> TargetMethods(){
   var targets=new[]{
    Tuple.Create(typeof(HediffSet),"GetPartHealth"),
    Tuple.Create(typeof(PawnCapacityUtility),"CalculatePartEfficiency"),
    Tuple.Create(typeof(PawnCapacityUtility.CapacityImpactorBodyPartHealth),"Readable"),
    Tuple.Create(typeof(HealthCardUtility),"GetTooltip"),
    Tuple.Create(typeof(HealthUtility),"GetPartConditionLabel"),
    Tuple.Create(typeof(HealthUtility),"GiveRandomSurgeryInjuries"),
    Tuple.Create(typeof(HealthUtility),"DamageLegsUntilIncapableOfMoving"),
    Tuple.Create(typeof(HealthUtility),"DamageLimbsUntilIncapableOfManipulation"),
    Tuple.Create(typeof(DamageWorker_AddInjury),"ReduceDamageToPreserveOutsideParts"),
    Tuple.Create(typeof(DamageWorker_Blunt),"ApplySpecialEffectsToPart"),
    Tuple.Create(typeof(HediffGiver_BrainInjury),"OnHediffAdded"),
    Tuple.Create(typeof(Hediff_MissingPart),"get_BleedRate"),
    Tuple.Create(typeof(Hediff_MissingPart),"get_PainOffset"),
    Tuple.Create(typeof(Hediff_Pregnant),"get_IsSeverelyWounded")};
   foreach(var t in targets)yield return AccessTools.Method(t.Item1,t.Item2)??throw new MissingMethodException(t.Item1.FullName,t.Item2);
  }
  // Let the optional provider retain the record first, then compose its result.
  // Harmony reruns this ordered chain when a provider patches later at startup.
  [HarmonyPriority(Priority.Last)]
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,MethodBase __originalMethod){
   var codes=instructions.ToList();var max=AccessTools.Method(typeof(BodyPartDef),nameof(BodyPartDef.GetMaxHealth));var field=AccessTools.Field(typeof(BodyPartRecord),nameof(BodyPartRecord.def));int replaced=0;
   for(int i=0;i<codes.Count;i++){
    if(codes[i].operand is MethodInfo method && TemperingCompatibility.IsMaximum(method)){
     codes[i].opcode=OpCodes.Call;codes[i].operand=AccessTools.Method(typeof(PartHealth),nameof(PartHealth.Maximum));replaced++;continue;
    }
    if(!codes[i].Calls(max))continue;
    int j=i-1;while(j>=0 && i-j<=8 && !codes[j].LoadsField(field))j--;
    if(j<0 || i-j>8)throw new InvalidOperationException("Cannot locate body-part record in "+__originalMethod);
    codes[j].opcode=OpCodes.Nop;codes[j].operand=null;
    codes[i].opcode=OpCodes.Call;codes[i].operand=AccessTools.Method(typeof(PartHealth),nameof(PartHealth.Maximum));replaced++;
   }
   if(replaced==0)throw new InvalidOperationException("No body-part maximum found in "+__originalMethod);
   return codes;
  }
 }
}
