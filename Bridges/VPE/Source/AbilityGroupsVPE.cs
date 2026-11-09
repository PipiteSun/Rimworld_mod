using System;
using System.Collections.Generic;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.139.0 技能分组也管 VPE 灵能（Peter）：VEF 的能力按钮（VEF.Abilities.Command_Ability，不是原版那个）用键 "VEF:" + defName，
 // 设置窗口里列出这个人学会的 VEF 能力（标签写「灵能·路径名」），选了分组时按钮栏一起过滤。只是挂钩子，逻辑都在主程序集 AbilityGroups.cs。
 [StaticConstructorOnStartup]
 public static class AbilityGroupsVPE {
  static AbilityGroupsVPE(){
   AbilityGroupsExt.GizmoKey=Key;
   AbilityGroupsExt.Entries=Entries;
   AbilityGroupsExt.Count=Count;
  }
  public static string KeyOf(VEF.Abilities.AbilityDef d)=>d==null?null:AbilityGroupsExt.VefPrefix+d.defName;
  static string Key(Gizmo g)=>g is Command_Ability c?KeyOf(c.ability?.def):null;
  static int Count(Pawn p)=>p.GetComp<CompAbilities>()?.LearnedAbilities?.Count??0;
  static IEnumerable<AbilityGroupEntry> Entries(Pawn p){
   var comp=p.GetComp<CompAbilities>();if(comp?.LearnedAbilities==null)yield break;
   var seen=new HashSet<VEF.Abilities.AbilityDef>();
   foreach(Ability a in comp.LearnedAbilities){
    var d=a?.def;if(d==null || !seen.Add(d))continue;
    string tag="灵能";
    try{var path=d.Psycast()?.path;if(path!=null)tag="灵能·"+path.label;}catch{}
    int lv=0;try{lv=d.Psycast()?.level??0;}catch{}
    yield return new AbilityGroupEntry{key=KeyOf(d),label=d.LabelCap,desc=d.description,icon=d.icon,tag=tag,sort=lv};
   }
  }
 }
}
