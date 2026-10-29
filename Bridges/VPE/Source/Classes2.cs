using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.36.0 德鲁伊、圣武士、术士、武僧共用的规则：双技能路径的门槛、术士血脉、巫妖限制、背景特性经验、光环与武僧架势的定时刷新、近战命中追加伤害。
 public static class ClassRules {
  // 术士血脉：这些异种（以及六臂蛇人）学术士时所有技能门槛 −2，天赋强度 +0.15。
  public static readonly HashSet<string> Bloodlines=new HashSet<string>{"LP_ForceDragon","LP_GenasiFire","LP_GenasiWater","LP_GenasiEarth","LP_GenasiAir","LP_Genie","LP_Titan","LP_StormGiant","LP_CloudGiant","LP_Cambion"};
  public const int BloodlineSkillBonus=2;
  public const float BloodlineBonus=.15f;
  public static bool Bloodline(Pawn p)=>p!=null && (p.genes?.Xenotype!=null && Bloodlines.Contains(p.genes.Xenotype.defName) || Marilith.Is(p) || AbyssRaces.IsCambion(p)); // 1.54.0 坎比恩（深渊血统，按基因算，遗传的孩子也算）
  static bool Sorcerer(LP_ClassPathDef path)=>path?.defName==Classes.SorcererPath;
  // 路径解锁门槛（血脉术士 4 级）。
  public static int Threshold(LP_ClassPathDef path,Pawn p)=>Sorcerer(path) && Bloodline(p)?path.requiredSkillLevel-BloodlineSkillBonus:path.requiredSkillLevel;
  // 学习能力时额外算上的技能等级（血脉术士 +2）。
  public static int SkillBonus(LP_ClassAbilityExt e,Pawn p)=>Sorcerer(e?.ClassPath) && Bloodline(p)?BloodlineSkillBonus:0;
  // 术士社交支（天赋强度）的血脉加成。
  public static float PowerBonus(LP_ClassAbilityExt e,Pawn p)=>Sorcerer(e?.ClassPath) && e.Skill==SkillDefOf.Social && Bloodline(p)?BloodlineBonus:0f;
  // 不能走的路径：巫妖不能当圣武士和德鲁伊。返回 null 表示可以。
  public static string Forbidden(LP_ClassPathDef path,Pawn p){
   if(path==null || p==null || !Items.IsLich(p))return null;
   if(path.defName==Classes.PaladinPath)return "亡灵无法立下圣誓";
   if(path.defName==Classes.DruidPath)return "死亡之躯与自然相斥";
   return null;
  }
  // 1.32.0 背景特性：侍僧的圣武士、隐士的武僧路径经验 ×1.25。
  public static float ExtraXpFactor(Pawn p,string path){
   if(p?.story?.traits==null)return 1f;
   if(path==Classes.PaladinPath && HasTrait(p,"LP_Background_Acolyte"))return 1.25f;
   if(path==Classes.MonkPath && HasTrait(p,"LP_Background_Hermit"))return 1.25f;
   return 1f;
  }
  static bool HasTrait(Pawn p,string name){var d=DefDatabase<TraitDef>.GetNamedSilentFail(name);return d!=null && p.story.traits.HasTrait(d);}
  public static bool Knows(Pawn p,string ability){
   var def=DefDatabase<AbilityDef>.GetNamedSilentFail(ability);var comp=p?.GetComp<CompAbilities>();
   return def!=null && comp!=null && comp.HasAbility(def);
  }
  public static Ability Learned(Pawn p,string ability){
   var comp=p?.GetComp<CompAbilities>();if(comp==null)return null;
   foreach(var a in comp.LearnedAbilities)if(a.def.defName==ability)return a;
   return null;
  }
  public static bool Conjured(Pawn p)=>Items.Has(p,"LP_Conjured");
  // 1.36.0 新路径的终极要求两支的 4 环都学会（不管 VPE 自己是“任一”还是“全部”）。
  static readonly HashSet<string> NewPaths=new HashSet<string>{Classes.DruidPath,Classes.PaladinPath,Classes.SorcererPath,Classes.MonkPath};
  public static bool AllPrereqsLearned(LP_ClassAbilityExt e,CompAbilities comp){
   if(e?.ClassPath==null || comp==null || e.prerequisites.NullOrEmpty() || !NewPaths.Contains(e.ClassPath.defName))return true;
   return e.prerequisites.All(comp.HasAbility);
  }
 }

 // 一次真正的近战结算（不掷命中）：原版近战伤害系数、武器品质、Isekai 的力量与精炼都照常生效；没有近战动作时退回钝器。
 public static class ClassMelee {
  static readonly System.Reflection.MethodInfo applyMelee=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget");
  public static bool Strike(Pawn caster,Pawn target,float fallback=8f){
   if(caster==null || target==null || target.Dead || !target.Spawned)return false;
   var verb=caster.meleeVerbs?.TryGetMeleeVerb(target);
   if(verb is Verb_MeleeAttackDamage && applyMelee!=null)applyMelee.Invoke(verb,new object[]{new LocalTargetInfo(target)});
   else target.TakeDamage(new DamageInfo(DamageDefOf.Blunt,fallback,.1f,-1f,caster));
   caster.Drawer?.Notify_MeleeAttackOn(target);
   return true;
  }
  // 自检用：这一击按近战结算会用到的伤害量。
  public static float Amount(Pawn caster,Pawn target){var verb=caster.meleeVerbs.TryGetMeleeVerb(target);return verb.verbProps.AdjustedMeleeDamageAmount(verb,caster);}
 }

 // 近战命中后：至圣斩、神圣武器追加神圣伤害（法术部分乘心灵敏感度，已算在状态严重度里）。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class ClassMeleeHit {
  public static int Hits; // 1.53.0 自检用：经过这里的近战结算次数
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){
   Hits++;
   var caster=__instance.CasterPawn;var t=target.Thing as Pawn;
   if(caster==null || t==null || t.Dead)return;
   Paladin.OnMeleeHit(caster,t);
   if(!Martial.inFollowUp && !t.Dead && Martial.Is(caster))Martial.TryFollowUp(__instance,caster,t); // 1.53.0 额外攻击的追击（不再触发追击）
  }
 }

 // 受伤前：誓仇加成、拨挡箭矢（PreApplyDamage 之后改 dinfo 仍会生效）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class ClassDamageMods {
  static void Postfix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(absorbed || __instance.Dead || dinfo.Def==null || dinfo.Amount<=0f)return;
   Paladin.ApplyEnmity(__instance,ref dinfo);
   // 1.53.0 武器专精、死神之眼的远程伤害（快速返回：ShotFlags 为空）。
   var a=dinfo.Instigator as Pawn;
   if(a!=null && Martial.ShotFlags.Count>0){byte b=Martial.Flags(a);if((b & (Martial.FlagMastery|Martial.FlagDeadEye))!=0 && Martial.Ranged(dinfo))dinfo.SetAmount(dinfo.Amount*Martial.RangedFactor(a,b));}
   if(Monk.Deflect(__instance,ref dinfo))absorbed=true;
   if(!absorbed && dinfo.Amount>0f && a!=null && Martial.Is(a))Martial.OnHit(__instance,a); // 1.53.0 斗志（快速返回：不是物理路径者）
  }
 }

 // 死亡后：召唤物不留尸体，誓仇得报。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class ClassKillHooks {
  static void Postfix(Pawn __instance,DamageInfo? dinfo){
   if(!__instance.Dead)return;
   Paladin.OnKilled(__instance,dinfo?.Instigator as Pawn);
   if(dinfo?.Instigator is Pawn killer && Martial.Is(killer))Martial.OnKill(__instance,killer); // 1.53.0 斗志
   if(ClassRules.Conjured(__instance)){var c=__instance.Corpse;if(c!=null && !c.Destroyed){if(c.Spawned)FleckMaker.ThrowDustPuffThick(c.DrawPos,c.Map,2f,new Color(.6f,.9f,.6f));c.Destroy();}}
  }
 }

 // 勇气灵光、圣光化身：不会进入恐慌逃跑。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class CourageNoPanic {
  public static bool Blocks(Pawn p,MentalStateDef def)=>def!=null && (def==MentalStateDefOf.PanicFlee || def.defName.Contains("Panic") || def.defName.Contains("Flee")) && (Items.Has(p,"LP_CourageAura") || Items.Has(p,"LP_HolyAvatar"));
  static bool Prefix(Pawn ___pawn,MentalStateDef stateDef,ref bool __result){
   if(!Blocks(___pawn,stateDef))return true;
   __result=false;return false;
  }
 }

 // 光环与武僧架势：每 250 刻扫描一次学会了相应被动的人，给范围内的友军挂 300 刻的状态（多个来源取最强，不叠加）。
 public class MapComponent_ClassAuras : MapComponent {
  public const int Interval=250,AuraTicks=300;
  public const float AuraRadius=5f;
  public MapComponent_ClassAuras(Map map):base(map){}
  public override void MapComponentTick(){
   if(Find.TickManager.TicksGame%Interval!=0)return;
   Refresh(map);
   if(Find.TickManager.TicksGame%Martial.HourTicks==0)Martial.Cleanup(); // 1.53.0：清掉已销毁的子弹和死人的射击状态位
  }
  public static void Refresh(Map map){
   if(map==null)return;
   foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()){
    if(p.Dead || !p.RaceProps.Humanlike || p.GetComp<CompAbilities>()==null)continue;
    Martial.Refresh(p); // 1.53.0 物理路径缓存、VPE 等级加成、额外攻击（同一次遍历）
    if(ClassRules.Knows(p,"LP_MartialArts"))Monk.RefreshStance(p);
    if(p.Downed)continue;
    bool prot=ClassRules.Knows(p,"LP_AuraOfProtection"),courage=ClassRules.Knows(p,"LP_AuraOfCourage");
    if(!prot && !courage)continue;
    float d=Classes.Power(p,SkillDefOf.Social);
    foreach(var ally in Races124.PawnsNear(map,p.Position,AuraRadius)){
     if(!Paladin.AuraTarget(p,ally))continue;
     if(prot)Items.Give(ally,"LP_ProtectionAura",AuraTicks,d);
     if(courage){Items.Give(ally,"LP_CourageAura",AuraTicks,1f);Items.Remove(ally,"LP_Shaken");}
    }
   }
  }
 }

 // 按严重度换算数值的状态：XML 写 scaledFactors / scaledOffsets（值 = base + perSeverity × 严重度，再夹在 min～max），其余照第一档。
 public class ScaledStat {
  public StatDef stat;public float baseValue;public float perSeverity;public float min=-999f;public float max=999f;
  public float Value(float severity)=>Mathf.Clamp(baseValue+perSeverity*severity,min,max);
 }
 public class ScaledHediffExtension : DefModExtension {
  public List<ScaledStat> scaledFactors=new List<ScaledStat>();
  public List<ScaledStat> scaledOffsets=new List<ScaledStat>();
 }
 public class Hediff_ClassScaled : HediffWithComps {
  HediffStage stage;float built=-1f;
  ScaledHediffExtension extCache;HediffDef extDef; // 1.58.6：扩展按定义缓存（每次属性计算都会读 CurStage）
  public override HediffStage CurStage{get{
   if(extDef!=def){extCache=def.GetModExtension<ScaledHediffExtension>();extDef=def;}
   var ext=extCache;
   if(ext==null)return base.CurStage;
   if(stage==null || built!=Severity){
    built=Severity;var src=def.stages?.FirstOrDefault();
    stage=new HediffStage{statFactors=new List<StatModifier>(),statOffsets=new List<StatModifier>()};
    if(src!=null){
     stage.painFactor=src.painFactor;stage.disabledWorkTags=src.disabledWorkTags;stage.label=src.label;
     if(src.statFactors!=null)stage.statFactors.AddRange(src.statFactors);
     if(src.statOffsets!=null)stage.statOffsets.AddRange(src.statOffsets);
     if(src.capMods!=null)stage.capMods=src.capMods;
    }
    foreach(var s in ext.scaledFactors)if(s.stat!=null)stage.statFactors.Add(new StatModifier{stat=s.stat,value=s.Value(Severity)});
    foreach(var s in ext.scaledOffsets)if(s.stat!=null)stage.statOffsets.Add(new StatModifier{stat=s.stat,value=s.Value(Severity)});
   }
   return stage;
  }}
  public float Factor(string statName){var st=CurStage?.statFactors?.FirstOrDefault(m=>m.stat?.defName==statName);return st==null?1f:st.value;}
  public float Offset(string statName){var st=CurStage?.statOffsets?.FirstOrDefault(m=>m.stat?.defName==statName);return st==null?0f:st.value;}
 }

 // 召唤物的寿命：时间到就消失（不留尸体）。
 public class HediffCompProperties_Conjured : HediffCompProperties {
  public int ticks=60000;
  public HediffCompProperties_Conjured(){compClass=typeof(HediffComp_Conjured);}
 }
 public class HediffComp_Conjured : HediffComp {
  public int ticksLeft=-1;
  public HediffCompProperties_Conjured Props=>(HediffCompProperties_Conjured)props;
  public override void CompPostMake(){base.CompPostMake();ticksLeft=Props.ticks;}
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){
   base.CompPostTickInterval(ref severityAdjustment,delta);
   if(ticksLeft<0)ticksLeft=Props.ticks;
   ticksLeft-=delta;
   if(ticksLeft<=0)Vanish(Pawn);
  }
  public static void Vanish(Pawn p){
   if(p==null || p.Destroyed)return;
   if(p.Spawned)FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,2f,new Color(.6f,.9f,.6f));
   p.Destroy();
  }
  public override string CompLabelInBracketsExtra=>ticksLeft>0?ticksLeft.ToStringTicksToPeriod():null;
  public override void CompExposeData(){base.CompExposeData();Scribe_Values.Look(ref ticksLeft,"lpConjuredTicks",-1);}
 }

 // 每隔一段时间的脉冲：圣光化身（伤敌、治友）、空灵之身（治疗自己）。每次的量 = 状态严重度。
 public class HediffCompProperties_ClassPulse : HediffCompProperties {
  public int interval=60;public float radius;public bool damageEnemies;public float healFactor;public bool healAllies;
  public HediffCompProperties_ClassPulse(){compClass=typeof(HediffComp_ClassPulse);}
 }
 public class HediffComp_ClassPulse : HediffComp {
  int ticks;
  public HediffCompProperties_ClassPulse Props=>(HediffCompProperties_ClassPulse)props;
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){
   base.CompPostTickInterval(ref severityAdjustment,delta);
   ticks+=delta;while(ticks>=Props.interval){ticks-=Props.interval;Pulse();}
  }
  public int Pulse(){
   var p=Pawn;if(p==null || p.Dead)return 0;int n=0;float amount=parent.Severity;
   if(Props.healFactor>0f && !Props.healAllies)ClassAbility.Heal(p,amount*Props.healFactor);
   if(!p.Spawned || Props.radius<=0f)return 0;
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,Props.radius).ToList()){
    if(Props.damageEnemies && t!=p && t.HostileTo(p)){t.TakeDamage(new DamageInfo(Ability_SacredFlame.Radiant??DamageDefOf.Burn,amount*(Classes.HolyVulnerable(t)?2f:1f),.3f,-1f,p));n++;}
    else if(Props.healAllies && !t.HostileTo(p))ClassAbility.Heal(t,amount*Props.healFactor);
   }
   return n;
  }
  public override void CompExposeData(){base.CompExposeData();Scribe_Values.Look(ref ticks,"lpPulseTicks");}
 }

 // 术士天赋强度的血脉加成。
 public class StatPart_Bloodline : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && ClassRules.Bloodline(p))val+=ClassRules.BloodlineBonus;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && ClassRules.Bloodline(p)?"术士血脉：+"+ClassRules.BloodlineBonus.ToStringPercent():null;
 }

 // 被动能力的公共写法（器械师同款）：没有按钮，学会即生效。
 // 给自己挂同名状态，严重度 = 能力的量（圣光化身、空灵之身）。
 public class Ability_SelfPowerBuff : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){Give(pawn,def.defName,Duration,Power);Mote(pawn,def.LabelCap,Color.white);}
 }
 public class Ability_ClassPassive : ClassAbility {
  public override bool ShowGizmoOnPawn()=>false;
  public override void Apply(GlobalTargetInfo[] targets){}
 }
}
