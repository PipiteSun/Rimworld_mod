using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;

namespace LichPhylactery.VPEBridge {
 // 1.139.0（-VPE -Only AbilityGroups）：技能分组也管 VPE 灵能。
 public static partial class BridgeChecks {
  static List<string> VefKeys(Pawn p)=>p.GetGizmos().OfType<VEF.Abilities.Command_Ability>().Select(c=>AbilityGroupsVPE.KeyOf(c.ability.def)).ToList();
  static void AbilityGroupsChecks(){
   var p=Make(0,12,2);p.ChangePsylinkLevel(5,false);
   var defs=DefDatabase<AbilityDef>.AllDefsListForReading.Where(d=>d.Psycast()!=null && d.Psycast().level<=2).Take(3).ToList();
   foreach(var d in defs)Learn(p,d.defName);
   p.drafter.Drafted=true;
   var all=VefKeys(p);var entries=AbilityGroups.Listable(p).Where(e=>e.def==null).ToList();
   Check(defs.Count==3 && defs.All(d=>all.Contains(AbilityGroupsVPE.KeyOf(d))) && entries.Count>=3 && entries.All(e=>e.key.StartsWith(AbilityGroupsExt.VefPrefix) && e.tag.StartsWith("灵能") && e.icon!=null),
    "ability groups: learned VPE psycasts are listed in the group window ("+string.Join("、",entries.Select(e=>e.label+"/"+e.tag))+") and their buttons map to VEF: keys");
   var g=AbilityGroups.Add(p,"灵能");g.SetKey(AbilityGroupsVPE.KeyOf(defs[0]),true);AbilityGroups.SetActive(p,0);
   var shown=VefKeys(p);bool sw=p.GetGizmos().OfType<Command_AbilityGroup>().Any();
   AbilityGroups.SetActive(p,-1);var back=VefKeys(p);
   Check(shown.Count==1 && shown[0]==AbilityGroupsVPE.KeyOf(defs[0]) && back.Count==all.Count && sw && g.Count==1,
    "ability groups: active group shows only its VPE psycast ("+defs[0].label+"), 全部 shows all "+all.Count+", switch button present");
   AbilityGroups.Remove(p,0);
  }
 }
}
