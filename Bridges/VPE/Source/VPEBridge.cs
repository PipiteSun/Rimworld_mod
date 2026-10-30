using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.27.0 DND 职业路径（VPE 联动）：只在启用 Vanilla Psycasts Expanded 时由 LoadFolders 加载；主程序集不引用本程序集、VEF.dll 或 VanillaPsycastsExpanded.dll。
 // 1.73.0：八个 DND 职业都移到主程序集（冒险者职业等级 + 原版能力）。这里只剩：旧路径 / 旧能力的定义类型（老存档读得进来）、隐藏与迁移（Martial.cs）、
 // 云游导师（Mentor.cs）、冒险者等级给 VPE 路径点（AdventurerVPE.cs，给 VPE 自己的路径用）、秘术残卷与 VEF 能力的各种联动。
 [StaticConstructorOnStartup]
 public static class VPEBridge {
  static VPEBridge(){
   new Harmony("local.lich.phylactery.vpebridge").PatchAll(typeof(VPEBridge).Assembly);
   AdventurerVPE.Register(); // 1.48.0 冒险者等级
  }
 }
 // 1.28.0 提尔“审判之庭”：VEF 的能力不继承原版 RimWorld.Ability，这里另挂一次。
 [HarmonyPatch(typeof(Ability),nameof(Ability.IsEnabledForPawn))]
 public static class TwilightCourtVEF {
  static void Postfix(Ability __instance,ref bool __result,ref string reason){if(__result && TwilightSkills.Silenced(__instance.pawn)){__result=false;reason="提尔的审判之庭：10格内不能使用任何能力。";}}
 }

 public static class Classes {
  public const string FighterPath="LP_Path_Fighter",WizardPath="LP_Path_Wizard",ClericPath="LP_Path_Cleric";
  public const string DruidPath="LP_Path_Druid",PaladinPath="LP_Path_Paladin",SorcererPath="LP_Path_Sorcerer",MonkPath="LP_Path_Monk",ArtificerPath="LP_Path_Artificer";
  static readonly Dictionary<string,StatDef> stats=new Dictionary<string,StatDef>();
  public static StatDef Stat(string name){if(!stats.TryGetValue(name,out var d) || d==null)stats[name]=d=DefDatabase<StatDef>.GetNamedSilentFail(name);return d;}
  public static LP_ClassPathDef Path(string name)=>DefDatabase<PsycasterPathDef>.GetNamedSilentFail(name) as LP_ClassPathDef;
  // 还能在 VPE 页签里用的 DND 路径（1.73.0 起没有了）。
  public static IEnumerable<LP_ClassPathDef> Paths=>DefDatabase<PsycasterPathDef>.AllDefsListForReading.OfType<LP_ClassPathDef>().Where(p=>!ClassMigration.Hidden(p)).OrderBy(p=>p.order);
  public static int Level(Pawn p,SkillDef skill)=>ClassSystem.Skill(p,skill);
  public static float Power(int level)=>ClassSystem.Power(level);
  public static float Power(Pawn p,SkillDef skill)=>ClassSystem.Power(p,skill);
  public static float MartialBonus(Pawn p,SkillDef s)=>ClassSystem.MartialBonus(p,s);
  public static Hediff_PsycastAbilities Psycasts(Pawn p)=>p?.Psycasts();
  public static bool HasPath(Pawn p,LP_ClassPathDef path){var h=Psycasts(p);return h!=null && path!=null && h.unlockedPaths!=null && h.unlockedPaths.Contains(path);}
  public static string SkillLabel(SkillDef s)=>s==null?"":s.LabelCap.Resolve();
 }

 // 旧路径（定义保留给老存档）：不显示、不能解锁。
 public class LP_ClassPathDef : PsycasterPathDef {
  public SkillDef requiredSkill,alternateSkill;
  public int requiredSkillLevel=6;
  public bool classPsychic,martial;
  public override bool CanPawnUnlock(Pawn pawn)=>false; // 1.72.0 / 1.73.0：职业在主程序集
 }
 public enum ClassPsychicMode{Path,Yes,No}
 // 旧能力的扩展（只为读 XML）。
 public class LP_ClassAbilityExt : AbilityExtension_Psycast {
  public int requiredSkillLevel;public SkillDef skill;public ClassPsychicMode psychicMode=ClassPsychicMode.Path;public bool bestSkill;
  public LP_ClassPathDef ClassPath=>path as LP_ClassPathDef;
 }
 // 旧路径能力的神经成型器（VPE 自动生成，还会出现）：不能用。
 [HarmonyPatch(typeof(CompPsytrainer),nameof(CompPsytrainer.CanBeUsedBy))]
 public static class Psytrainer_CanBeUsedBy_Patch {
  static void Postfix(CompPsytrainer __instance,Pawn p,ref AcceptanceReport __result){
   if(!__result.Accepted)return;
   var ability=(__instance.props as CompProperties_UseEffectGiveAbility)?.ability;
   if(ClassMigration.Hidden(ability))__result="这项能力已改为冒险者职业能力（在“冒险者”分页的“职业”页加点获得）";
  }
 }
}
