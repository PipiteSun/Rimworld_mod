using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.37.0 耐瑟瑞尔卷轴的 VPE 部分：研习卷经验直接进 VPE（Hediff_PsycastAbilities.GainExperience，不绕道精神力），
 // 秘术残卷从 VPE 心灵法术里按路径归学派抽法术（不扣路径点、不要求解锁路径，等级对比读者 VPE 等级），排除所有 DND 职业路径 LP_Path_*。
 // 另挂两处：魔网震颤/奥术半神改 VPE 法术冷却，魔网崩溃让 VPE 法术（含 DND 职业法术）施放不出来。
 [StaticConstructorOnStartup]
 public static class NetherilVPE {
  public static readonly Dictionary<string,string> PathSchool=new Dictionary<string,string>{
   {"VPE_Conflagrator","Evocation"},{"VPE_Staticlord","Evocation"},{"VPE_Frostshaper","Evocation"},
   {"VPE_Protector","Abjuration"},
   {"VPE_Puppeteer","Enchantment"},{"VPE_Warlord","Enchantment"},{"VPE_Empath","Enchantment"},
   {"VPE_Nightstalker","Illusion"},
   {"VPE_Chronopath","Divination"},{"VPE_Harmonist","Divination"},
   {"VPE_Skipmaster","Conjuration"},{"VPE_Archotechist","Conjuration"},
   {"VPE_Wildspeaker","Transmutation"},{"VPE_Technomancer","Transmutation"},
   {"VPE_Necropath","Necromancy"}};
  static NetherilVPE(){
   NetherilHooks.VPELevel=p=>Classes.Psycasts(p)?.level??0;
   NetherilHooks.VPEGainXP=(p,x)=>Classes.Psycasts(p)?.GainExperience(x,true);
   NetherilHooks.VPECandidates=Candidates;
   NetherilHooks.VPELearn=Learn;
  }
  // 表外路径（含其他模组的）归入变化；DND 职业路径不进任何学派。
  public static string School(PsycasterPathDef path){if(path==null || path.defName.StartsWith("LP_Path_"))return null;return PathSchool.TryGetValue(path.defName,out var s)?s:"Transmutation";}
  public static IEnumerable<AbilityDef> Pool(string school)=>DefDatabase<AbilityDef>.AllDefsListForReading.Where(d=>d.Psycast() is AbilityExtension_Psycast e && e.path!=null && e.path.abilities!=null && e.path.abilities.Contains(d) && School(e.path)==school);
  public static List<string> Candidates(Pawn p,string school){
   var h=Classes.Psycasts(p);var comp=p?.GetComp<CompAbilities>();if(h==null || comp==null)return new List<string>();
   return Pool(school).Where(d=>d.Psycast().level<=h.level && !comp.HasAbility(d)).Select(d=>d.defName).ToList();
  }
  public static string Learn(Pawn p,string name){
   var d=DefDatabase<AbilityDef>.GetNamedSilentFail(name);var comp=p?.GetComp<CompAbilities>();if(d==null || comp==null)return null;
   if(!comp.HasAbility(d))comp.GiveAbility(d);return d.LabelCap.Resolve();
  }
  public static string Table()=>string.Join("\n",DefDatabase<PsycasterPathDef>.AllDefsListForReading.OrderBy(x=>x.defName).Select(x=>x.defName+" → "+(School(x)==null?"（排除）":Netheril.SchoolLabel(School(x)))));
 }
 [HarmonyPatch(typeof(Ability),nameof(Ability.GetCooldownForPawn))]
 public static class NetherilVEFCooldown {static void Postfix(Ability __instance,ref int __result){if(__result<=0 || __instance?.def?.Psycast()==null)return;float f=NetherilAscension.CooldownFactor(__instance.pawn);if(f!=1f)__result=Math.Max(1,(int)Math.Round(__result*f));}}
 [HarmonyPatch(typeof(Ability),nameof(Ability.IsEnabledForPawn))]
 public static class NetherilVEFCollapse {static void Postfix(Ability __instance,ref bool __result,ref string reason){if(__result && __instance?.def?.Psycast()!=null && NetherilAscension.WeaveCollapsed(__instance.pawn?.MapHeld) && !HeroDestiny.WeaveImmune(__instance.pawn)){__result=false;reason=NetherilAscension.CollapseReason;}}}

 // 1.37.0 检查（Test.ps1 -VPE）：研习卷经验进 VPE 且精神力不变；残卷按学派学会 VPE 法术、不扣路径点；职业路径不进学派。
 public static partial class BridgeChecks {
  static void NetherilChecks(){
   Check(NetherilHooks.VPEActive && NetherilVPE.School(Classes.Path(Classes.WizardPath))==null && NetherilVPE.School(DefDatabase<PsycasterPathDef>.GetNamedSilentFail("VPE_Conflagrator"))=="Evocation" && NetherilVPE.Pool("Evocation").Any() && !Netheril.Schools.Any(s=>NetherilVPE.Pool(s).Any(d=>d.Psycast().path.defName.StartsWith("LP_Path_"))),"netheril: VPE hooks registered, LP_Path_* excluded from every school, conflagrator is evocation");
   var p=Make(0,10,0);var h=Classes.Psycasts(p);float xp=h.experience;int lvl=h.level;float focus=p.psychicEntropy.CurrentPsyfocus;
   var study=Netheril.MakeStudy(QualityCategory.Normal);var doer=Netheril.Doer<BookOutcome_NetherilStudy>(study);
   Netheril.TestRoll=1f;try{doer.ReadHour(p);}finally{Netheril.TestRoll=null;}
   Check((h.level>lvl || Near(h.experience-xp,Netheril.StudyXP)) && Near(p.psychicEntropy.CurrentPsyfocus,focus),"netheril: one hour of the study scroll adds "+Netheril.StudyXP+" VPE experience directly, psyfocus unchanged ("+xp+" -> "+h.experience+", focus "+focus+" -> "+p.psychicEntropy.CurrentPsyfocus+")");
   var arcane=Netheril.MakeArcane("Evocation",QualityCategory.Normal);var a=Netheril.Doer<BookOutcome_NetherilArcane>(arcane);
   int points=h.points;var before=p.GetComp<CompAbilities>().LearnedAbilities.Count;
   a.progress[p.thingIDNumber]=Netheril.ArcaneGoal;bool ok=a.Settle(p);
   var learned=p.GetComp<CompAbilities>().LearnedAbilities.Select(x=>x.def).Where(d=>d.Psycast()!=null && NetherilVPE.School(d.Psycast().path)=="Evocation").ToList();
   Check(ok && learned.Count>0 && learned.All(d=>d.Psycast().level<=h.level) && h.points==points && p.GetComp<CompAbilities>().LearnedAbilities.Count==before+1 && !h.unlockedPaths.Any(x=>x.defName=="VPE_Conflagrator" || x.defName=="VPE_Staticlord" || x.defName=="VPE_Frostshaper"),"netheril: arcane scroll teaches a level-"+h.level+" evocation psycast ("+a.LastLearned+") without spending path points or unlocking a path");
   study.Destroy();arcane.Destroy();
   Log.Message("[LICH-TEST] netheril VPE path → school table:\n"+NetherilVPE.Table());
  }
 }
}
