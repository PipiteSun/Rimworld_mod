using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.58.0 魔网之子的 VPE 部分：灵能熵 ×0.75（魔网涌动期间 0），显现后施法 15% 不进冷却（涌动期间都不进）。没有魔网之子的人第一步就返回。
 // 1.58.1：仪式施法者的“仪式余韵”（下一次 VPE 施法不进冷却、不加熵），魔法学徒学会所选路径的第一个能力。
 [HarmonyPatch(typeof(AbilityExtension_Psycast),nameof(AbilityExtension_Psycast.GetEntropyUsedByPawn))]
 public static class HeroWeaveEntropy {
  static void Postfix(Pawn __0,ref float __result){if(__result<=0f || __0==null)return;float f=HeroDestiny.VpeEntropyFactor(__0)*DndTraits.VpeEntropyFactor(__0);if(f!=1f)__result*=f;} // 1.62.0 奥术天赋 ×0.9
 }
 [HarmonyPatch(typeof(Ability),nameof(Ability.Cast),new[]{typeof(RimWorld.Planet.GlobalTargetInfo[])})]
 public static class HeroWeaveCooldown {
  static void Postfix(Ability __instance){
   var p=__instance?.pawn;if(p==null || __instance.def?.Psycast()==null || __instance.cooldown<=Find.TickManager.TicksGame)return;
   var i=HeroTraits.Info(p);if(i==null)return;
   if(i.ritual && Feats2.Primed(p)){__instance.cooldown=0;Items.Remove(p,"LP_RitualPrimed");Items.Mote(p,"仪式余韵：不进冷却",new UnityEngine.Color(.6f,.55f,.85f));return;}
   if(HeroDestiny.VpeSkipCooldown(p)){__instance.cooldown=0;Items.Mote(p,"魔网之子：不进冷却",new UnityEngine.Color(.5f,.7f,1f));}
  }
 }
 [StaticConstructorOnStartup]
 public static class Feats2VPE {
  static Feats2VPE(){Feats2.RitualResetHook=ResetLongest;Feats2.RitualCooldownHook=p=>Longest(p)!=null;} // 1.73.0：魔法学徒不再学 VPE 路径的第一个能力（职业都移到主程序集），一律给魔能飞弹
  // 魔法学徒：智识 → 法师，社交 → 牧师，种植 / 驯兽 → 德鲁伊（取最高的一项），学会该路径等级最低的能力。
  public static LP_ClassPathDef PathFor(Pawn p){
   int intel=Classes.Level(p,SkillDefOf.Intellectual),soc=Classes.Level(p,SkillDefOf.Social),nat=System.Math.Max(Classes.Level(p,SkillDefOf.Plants),Classes.Level(p,SkillDefOf.Animals));
   string n=intel>=soc && intel>=nat?Classes.WizardPath:soc>=nat?Classes.ClericPath:Classes.DruidPath;
   return Classes.Path(n);
  }
  public static AbilityDef FirstAbility(LP_ClassPathDef path)=>path?.abilities?.Where(d=>d.Psycast()!=null).OrderBy(d=>d.Psycast().level).ThenBy(d=>path.abilities.IndexOf(d)).FirstOrDefault();
  public static string Initiate(Pawn p){
   var comp=p?.GetComp<CompAbilities>();var d=FirstAbility(PathFor(p));if(comp==null || d==null)return null;
   if(!comp.HasAbility(d))comp.GiveAbility(d);return d.LabelCap.Resolve();
  }
  static Ability Longest(Pawn p){var comp=p?.GetComp<CompAbilities>();int now=Find.TickManager.TicksGame;return comp?.LearnedAbilities.Where(a=>a.cooldown>now).OrderByDescending(a=>a.cooldown).FirstOrDefault();}
  public static string ResetLongest(Pawn p){var a=Longest(p);if(a==null)return null;a.cooldown=0;return a.def.LabelCap.Resolve();}
 }
}
