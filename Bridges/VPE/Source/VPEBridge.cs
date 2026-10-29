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
 // 三条路径：战士（格斗）、法师（智识）、牧师（社交）。解锁路径和学习每个能力都要达到技能要求；能力强度按“职业强度”属性（0.5 + 技能/20）缩放。
 [StaticConstructorOnStartup]
 public static class VPEBridge {
  static VPEBridge(){
   new Harmony("local.lich.phylactery.vpebridge").PatchAll(typeof(VPEBridge).Assembly);
   // 1.28.0：法师路径殖民者攻击位面裂口伤害 ×3（主程序集只认这个登记）。
   PortalEvent.WizardHook=p=>Classes.HasPath(p,Classes.Path(Classes.WizardPath));
   AdventurerVPE.Register(); // 1.48.0 冒险者等级
   Martial.Register(); // 1.53.0 武人之道（战斗风格槽、职业特性行、瞄准系数）
  }
 }
 // 1.28.0 提尔“审判之庭”：VEF 的能力不继承原版 RimWorld.Ability，这里另挂一次。
 [HarmonyPatch(typeof(Ability),nameof(Ability.IsEnabledForPawn))]
 public static class TwilightCourtVEF {
  static void Postfix(Ability __instance,ref bool __result,ref string reason){if(__result && TwilightSkills.Silenced(__instance.pawn)){__result=false;reason="提尔的审判之庭：10格内不能使用任何能力。";}}
 }

 public static class Classes {
  public const string FighterPath="LP_Path_Fighter",WizardPath="LP_Path_Wizard",ClericPath="LP_Path_Cleric";
  // 1.36.0 德鲁伊、圣武士、术士、武僧。
  public const string DruidPath="LP_Path_Druid",PaladinPath="LP_Path_Paladin",SorcererPath="LP_Path_Sorcerer",MonkPath="LP_Path_Monk";
  public const float XPPerSkillXP=.01f,XPDailyCap=50f;
  static readonly Dictionary<string,StatDef> stats=new Dictionary<string,StatDef>();
  public static StatDef Stat(string name){if(!stats.TryGetValue(name,out var d) || d==null)stats[name]=d=DefDatabase<StatDef>.GetNamedSilentFail(name);return d;}
  public static LP_ClassPathDef Path(string name)=>DefDatabase<PsycasterPathDef>.GetNamedSilentFail(name) as LP_ClassPathDef;
  // 1.36.0 按 order 排序：同分时手册、经验都优先老路径（法师先于术士、战士先于武僧）。
  public static IEnumerable<LP_ClassPathDef> Paths=>DefDatabase<PsycasterPathDef>.AllDefsListForReading.OfType<LP_ClassPathDef>().OrderBy(p=>p.order);
  public static int Level(Pawn p,SkillDef skill){if(p?.skills==null || skill==null)return 0;var r=p.skills.GetSkill(skill);return r==null || r.TotallyDisabled?0:r.Level;}
  // 职业强度公式。动物、机械体等没有技能的目标按 0.5 算。
  public static float Power(int level)=>.5f+level/20f;
  // 1.48.0：再加冒险者的主属性调整值与熟练加值（AdventurerVPE）。
  // 1.53.0：格斗、射击另加 VPE 灵能等级加成（物理路径者，取自 Martial 缓存）。
  public static float Power(Pawn p,SkillDef skill)=>Power(Level(p,skill))+AdventurerVPE.SkillBonus(p,skill)+MartialBonus(p,skill);
  // 1.36.0：能力的强度看它绑定的技能；术士社交支另加血脉加成。1.53.0：bestSkill 的能力看主副技能里较高的一项。
  public static float Power(Pawn p,LP_ClassAbilityExt e){if(e?.ClassPath==null)return 1f;var s=e.SkillFor(p);return Power(Level(p,s))+AdventurerVPE.PathBonus(p,e.ClassPath)+ClassRules.PowerBonus(e,p)+MartialBonus(p,s);}
  public static float MartialBonus(Pawn p,SkillDef s)=>s==SkillDefOf.Melee || s==SkillDefOf.Shooting?Martial.VpeBonus(p):0f;
  // 1.30.1：法师、牧师能力的伤害与治疗再乘施法者心灵敏感度（下限 0.2，不封顶）；战士、器械师不乘。持续时间、半径、射程不受影响。
  // 1.36.0：改成数据——路径的 classPsychic，能力可用 psychicMode 单独覆盖（圣武士的神术、武僧的气功）。
  public const float PsychicMin=.2f;
  public static bool UsesPsychic(LP_ClassPathDef path)=>path!=null && path.classPsychic;
  public static bool UsesPsychic(LP_ClassAbilityExt e)=>e!=null && (e.psychicMode==ClassPsychicMode.Yes || e.psychicMode==ClassPsychicMode.Path && UsesPsychic(e.ClassPath));
  public static float PsychicScale(float sensitivity)=>Mathf.Max(PsychicMin,sensitivity);
  public static float PsychicFactor(Pawn p,LP_ClassPathDef path)=>p==null || !UsesPsychic(path)?1f:PsychicScale(p.GetStatValue(StatDefOf.PsychicSensitivity));
  public static float PsychicFactor(Pawn p,LP_ClassAbilityExt e)=>p==null || !UsesPsychic(e)?1f:PsychicScale(p.GetStatValue(StatDefOf.PsychicSensitivity));
  public static bool Undead(Pawn p)=>Items.Undead(p);
  // 神圣伤害翻倍的目标：亡灵（巫妖、死灵造物）、邪魔（寒冰魔、炎魔）、夺心魔。
  public static bool HolyVulnerable(Pawn p)=>p!=null && (Undead(p) || Fiends.Is(p) || MindFlayer.IsMindFlayer(p));
  public static Hediff_PsycastAbilities Psycasts(Pawn p)=>p?.Psycasts();
  public static bool HasPath(Pawn p,LP_ClassPathDef path){var h=Psycasts(p);return h!=null && path!=null && h.unlockedPaths!=null && h.unlockedPaths.Contains(path);}
  public static ClassState State=>GameComp<ClassState>.Get();
  public static string SkillLabel(SkillDef s)=>s==null?"":s.LabelCap.Resolve();
 }

 // 路径：在 VPE 原条件之上加职业技能要求。
 public class LP_ClassPathDef : PsycasterPathDef {
  public SkillDef requiredSkill;
  // 1.36.0：副技能（可空）。双技能路径主、副技能任一达到门槛即可解锁；每个能力看自己绑定的那门技能。
  public SkillDef alternateSkill;
  public int requiredSkillLevel=6;
  // 1.36.0：能力的伤害、治疗是否乘心灵敏感度（法师、牧师、德鲁伊、术士为 true）。
  public bool classPsychic;
  // 1.53.0：物理路径（战士、圣武士、武僧，以后的野蛮人、游侠、游荡者）：斗志、额外攻击、战斗风格、VPE 等级加成。
  public bool martial;
  public bool UsesSkill(SkillDef s)=>s!=null && (s==requiredSkill || s==alternateSkill);
  public int Level(Pawn p)=>Mathf.Max(Classes.Level(p,requiredSkill),alternateSkill==null?0:Classes.Level(p,alternateSkill));
  public float Power(Pawn p)=>Classes.Power(p,requiredSkill);
  public int Threshold(Pawn p)=>ClassRules.Threshold(this,p);
  // 1.48.0：或者对应的冒险者主属性 ≥ 13（DND 兼职条件）。
  public bool SkillOk(Pawn p)=>Level(p)>=Threshold(p) || AdventurerVPE.ScoreOk(this,p);
  public override bool CanPawnUnlock(Pawn pawn)=>base.CanPawnUnlock(pawn) && SkillOk(pawn) && ClassRules.Forbidden(this,pawn)==null;
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(requiredSkill==null)yield return defName+": requiredSkill is null";}
 }
 public enum ClassPsychicMode{Path,Yes,No}

 // 能力：在 VPE 的前置能力之上加本能力自己的技能要求（只卡学习，不卡施放）。
 public class LP_ClassAbilityExt : AbilityExtension_Psycast {
  public int requiredSkillLevel;
  // 1.36.0：本能力绑定的技能（空 = 路径主技能），学习门槛和强度都看它；psychicMode 覆盖路径的心灵敏感度设定。
  public SkillDef skill;
  public ClassPsychicMode psychicMode=ClassPsychicMode.Path;
  // 1.53.0：true 时看路径主副技能里当前较高的一项（战士的回气、战吼：格斗或射击）。
  public bool bestSkill;
  public LP_ClassPathDef ClassPath=>path as LP_ClassPathDef;
  public SkillDef Skill=>skill??ClassPath?.requiredSkill;
  public SkillDef SkillFor(Pawn p){
   var main=Skill;if(!bestSkill || p==null)return main;var alt=ClassPath?.alternateSkill;
   return alt!=null && Classes.Level(p,alt)>Classes.Level(p,main)?alt:main;
  }
  public bool SkillOk(Pawn p)=>ClassPath==null || Classes.Level(p,SkillFor(p))+ClassRules.SkillBonus(this,p)>=requiredSkillLevel;
  public string Requirement=>ClassPath==null?"":"需要"+(bestSkill && ClassPath.alternateSkill!=null?Classes.SkillLabel(Skill)+"或"+Classes.SkillLabel(ClassPath.alternateSkill):Classes.SkillLabel(Skill))+" "+requiredSkillLevel+" 级";
  public override string GetDescription(Ability ability){var s=base.GetDescription(ability);return string.IsNullOrEmpty(Requirement)?s:(s+"\n"+Requirement);}
 }

 // VPE 的 PrereqsCompleted 不是虚方法：用后缀把技能要求并进去。灵能页签的解锁按钮、神经成型器和自动学习都经过这里。
 [HarmonyPatch(typeof(AbilityExtension_Psycast),nameof(AbilityExtension_Psycast.PrereqsCompleted),new[]{typeof(CompAbilities)})]
 public static class PrereqsCompleted_Comp_Patch {
  static void Postfix(AbilityExtension_Psycast __instance,CompAbilities compAbilities,ref bool __result){
   if(__result && __instance is LP_ClassAbilityExt e && (!e.SkillOk(compAbilities?.parent as Pawn) || !ClassRules.AllPrereqsLearned(e,compAbilities)))__result=false;
  }
 }
 [HarmonyPatch(typeof(AbilityExtension_Psycast),nameof(AbilityExtension_Psycast.PrereqsCompleted),new[]{typeof(Pawn)})]
 public static class PrereqsCompleted_Pawn_Patch {
  static void Postfix(AbilityExtension_Psycast __instance,Pawn pawn,ref bool __result){
   if(__result && __instance is LP_ClassAbilityExt e && !e.SkillOk(pawn))__result=false;
  }
 }
 // 神经成型器：技能不够时不能用。
 [HarmonyPatch(typeof(CompPsytrainer),nameof(CompPsytrainer.CanBeUsedBy))]
 public static class Psytrainer_CanBeUsedBy_Patch {
  static void Postfix(CompPsytrainer __instance,Pawn p,ref AcceptanceReport __result){
   if(!__result.Accepted)return;
   var ability=(__instance.props as CompProperties_UseEffectGiveAbility)?.ability;
   if(ability?.Psycast() is LP_ClassAbilityExt e && !e.SkillOk(p))__result=e.Requirement;
  }
 }

 // 职业强度属性：0.5 + 技能/20。
 public class StatPart_ClassSkill : StatPart {
  public SkillDef skill;
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p)val=Classes.Power(p,skill);}
  public override string ExplanationPart(StatRequest req){
   if(!(req.Thing is Pawn p))return null;int l=Classes.Level(p,skill);float a=AdventurerVPE.SkillBonus(p,skill),v=Classes.MartialBonus(p,skill);
   return Classes.SkillLabel(skill)+" "+l+" 级：0.5 + "+l+"/20 = "+Classes.Power(l).ToStringPercent()+(a!=0f?"\n冒险者（主属性调整值 × 0.04 + (熟练加值 − 2) × 0.02）："+(a>0f?"+":"")+a.ToStringPercent():"")
    +(v!=0f?"\nVPE 灵能等级 "+Martial.VpeLevel(p)+"：+"+v.ToStringPercent():""); // 1.53.0

  }
 }

 // 职业经验：已解锁某条路径的人，职业技能每 100 点技能经验给 1 点 VPE 灵能经验，每人每天上限 50。神经成型器等直接给的经验（direct）不算。
 [HarmonyPatch(typeof(Pawn_SkillTracker),nameof(Pawn_SkillTracker.Learn))]
 public static class ClassExperience {
  static void Postfix(SkillDef __0,float __1,bool __2,Pawn ___pawn){
   if(__1<=0 || __2 || ___pawn==null || __0==null || !ClassSkill(__0))return;
   Grant(___pawn,__0,__1);
  }
  // 1.58.6：技能经验（做工时每个小人每刻）原来对所有人都先扫一遍 VPE 灵能 hediff。没有任何职业路径用到的技能（建造、采矿、烹饪等）直接返回：
  // 这时 Grant 本来也一定返回 0。路径定义数量变了（自检注册新路径）就重建。
  static HashSet<SkillDef> classSkills;static int pathCount=-1;
  static bool ClassSkill(SkillDef s){
   var set=classSkills;
   if(set==null || pathCount!=DefDatabase<PsycasterPathDef>.DefCount){
    set=new HashSet<SkillDef>();foreach(var d in DefDatabase<PsycasterPathDef>.AllDefsListForReading)if(d is LP_ClassPathDef c){if(c.requiredSkill!=null)set.Add(c.requiredSkill);if(c.alternateSkill!=null)set.Add(c.alternateSkill);}
    classSkills=set;pathCount=DefDatabase<PsycasterPathDef>.DefCount;
   }
   return set.Contains(s);
  }
  public static float Grant(Pawn p,SkillDef skill,float skillXp){
   var h=Classes.Psycasts(p);if(h==null || h.unlockedPaths.NullOrEmpty())return 0f;
   LP_ClassPathDef matched=null;foreach(var path in h.unlockedPaths)if(path is LP_ClassPathDef c && c.UsesSkill(skill)){matched=c;break;}
   if(matched==null)return 0f;
   var state=Classes.State;float allowed=state==null?skillXp*Classes.XPPerSkillXP:state.Allow(p,skillXp*Classes.XPPerSkillXP);
   if(allowed<=0f)return 0f;
   // 1.32.0 背景特性：侍僧的牧师路径、贤者的法师路径经验 ×1.25（加在每日上限之后，上限仍按原经验计）。
   allowed*=BackgroundTraits.ClassXpFactor(p,matched.defName)*ClassRules.ExtraXpFactor(p,matched.defName);
   h.GainExperience(allowed);
   return allowed;
  }
 }
 public class ClassState : GameComponent {
  public Dictionary<int,float> givenToday=new Dictionary<int,float>();
  public int day=-1;
  public ClassState(Game game){}
  public float Allow(Pawn p,float want){
   int today=GenDate.DaysPassed;if(today!=day){day=today;givenToday.Clear();}
   givenToday.TryGetValue(p.thingIDNumber,out float given);
   float allowed=Mathf.Max(0f,Mathf.Min(want,Classes.XPDailyCap-given));
   if(allowed>0f)givenToday[p.thingIDNumber]=given+allowed;
   return allowed;
  }
  public float GivenToday(Pawn p){if(GenDate.DaysPassed!=day)return 0f;givenToday.TryGetValue(p.thingIDNumber,out float g);return g;}
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref day,"classXpDay",-1);
   Scribe_Collections.Look(ref givenToday,"classXpGiven",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit && givenToday==null)givenToday=new Dictionary<int,float>();
  }
 }
}
