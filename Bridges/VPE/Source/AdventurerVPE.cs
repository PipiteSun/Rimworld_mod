using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using VanillaPsycastsExpanded;

namespace LichPhylactery.VPEBridge {
 // 1.48.0 冒险者等级 × VPE：每升一级 +1 路径点（没有 VPE 灵能时先攒着，第一次获得心灵链接时一次发放）；
 // DND 职业路径“技能 ≥ 6 或主属性 ≥ 13”即可解锁；职业强度另加 0.04 × 主属性调整值 + 0.02 ×（熟练加值 − 2）。
 public static class AdventurerVPE {
  public const int PathScore=13;
  public static void Register(){
   Adventurer.LevelUpHook=(p,level)=>LevelUp(p);
   Adventurer.PrimaryAbilityHook=Primary;
   Feats.HasClassHook=p=>Classes.Psycasts(p)?.unlockedPaths?.OfType<LP_ClassPathDef>().Any()==true; // 1.48.1 战争施法者/法术狙击的前置
   Legend.RoleHook=Role; // 1.50.0 传奇属性点自动投点
  }
  // 各路径的主属性：战士取力量、敏捷较高者；法师、器械师智力；牧师、德鲁伊感知；圣武士、术士魅力；武僧敏捷。
  public static int PathAbility(LP_ClassPathDef path,Pawn p){
   switch(path?.defName){
    case Classes.FighterPath:return Best(p,Adventurer.Str,Adventurer.Dex);
    case Classes.WizardPath:case Classes.ArtificerPath:return Adventurer.Int;
    case Classes.ClericPath:case Classes.DruidPath:return Adventurer.Wis;
    case Classes.PaladinPath:case Classes.SorcererPath:return Adventurer.Cha;
    case Classes.MonkPath:return Adventurer.Dex;
    default:return -1;
   }
  }
  // 按技能算的职业强度（战技/奥术/神恩强度属性、光环、武僧架势）：格斗取力量、敏捷较高者，智识、手工看智力，社交看魅力，种植、驯兽看感知。
  public static int SkillAbility(SkillDef s,Pawn p){
   if(s==SkillDefOf.Melee)return Best(p,Adventurer.Str,Adventurer.Dex);
   if(s==SkillDefOf.Shooting)return Adventurer.Dex; // 1.53.0 射术强度
   if(s==SkillDefOf.Intellectual || s==SkillDefOf.Crafting)return Adventurer.Int;
   if(s==SkillDefOf.Social)return Adventurer.Cha;
   if(s==SkillDefOf.Plants || s==SkillDefOf.Animals)return Adventurer.Wis;
   return -1;
  }
  static int Best(Pawn p,int a,int b)=>Adventurer.Score(p,b)>Adventurer.Score(p,a)?b:a;
  public static float PathBonus(Pawn p,LP_ClassPathDef path)=>p==null?0f:Adventurer.ClassPowerBonus(p,PathAbility(path,p));
  public static float SkillBonus(Pawn p,SkillDef s)=>p==null?0f:Adventurer.ClassPowerBonus(p,SkillAbility(s,p));
  public static bool ScoreOk(LP_ClassPathDef path,Pawn p){if(!Adventurer.Enabled || p==null)return false;int a=PathAbility(path,p);return a>=0 && Adventurer.Get(p)!=null && Adventurer.Score(p,a)>=PathScore;}
  // 1.50.0：第一条已解锁的 DND 职业路径——战士、武僧、圣武士算近战，其他算施法者；没有职业返回 null。
  // 1.53.0：战士射击高于格斗时算远程（传奇属性点投远程 + 血量）。
  public static string Role(Pawn p){
   var path=Classes.Psycasts(p)?.unlockedPaths?.OfType<LP_ClassPathDef>().OrderBy(x=>x.order).FirstOrDefault();if(path==null)return null;
   switch(path.defName){
    case Classes.FighterPath:return Classes.Level(p,SkillDefOf.Shooting)>Classes.Level(p,SkillDefOf.Melee)?"ranged":"melee";
    case Classes.MonkPath:case Classes.PaladinPath:return "melee";
    default:return "caster";
   }
  }
  // 有 DND 职业时自动加点给第一条已解锁路径（按 order）的主属性。
  public static int Primary(Pawn p){
   var h=Classes.Psycasts(p);if(h?.unlockedPaths==null)return -1;
   var path=h.unlockedPaths.OfType<LP_ClassPathDef>().OrderBy(x=>x.order).FirstOrDefault();
   return path==null?-1:PathAbility(path,p);
  }
  public static void LevelUp(Pawn p){
   var a=Adventurer.Get(p);if(a==null)return;
   Flush(p);
   var h=Classes.Psycasts(p);
   if(h!=null){h.points++;a.vpeGranted++;}else a.vpeBanked++;
  }
  public static int Flush(Pawn p){
   var a=Adventurer.Get(p);var h=Classes.Psycasts(p);
   if(a==null || h==null || a.vpeBanked<=0)return 0;
   int n=a.vpeBanked;h.points+=n;a.vpeGranted+=n;a.vpeBanked=0;return n;
  }
 }
 // 1.48.1 法术狙击：VPE 法术（含 DND 职业能力）射程 ×1.5。施法时受伤不打断（战争施法者）在主程序集按施法工作判断，VEF 的施法工作也算。
 [HarmonyPatch(typeof(VEF.Abilities.Ability),nameof(VEF.Abilities.Ability.GetRangeForPawn))]
 public static class AdventurerVPESpellSniper {
  static void Postfix(VEF.Abilities.Ability __instance,ref float __result){if(__result>0f && __instance?.def?.Psycast()!=null && Feats.Has(__instance.pawn,Feats.SpellSniper))__result*=Feats.SpellSniperRange;}
 }
 // 第一次获得 VPE 灵能（心灵链接）时发放攒下的路径点。
 [HarmonyPatch(typeof(Hediff_PsycastAbilities),nameof(Hediff_PsycastAbilities.InitializeFromPsylink))]
 public static class AdventurerVPEInit {
  static void Postfix(Hediff_PsycastAbilities __instance){if(__instance?.pawn!=null)AdventurerVPE.Flush(__instance.pawn);}
 }
}
