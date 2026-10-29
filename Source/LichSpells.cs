using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // A complete humanlike corpse feeds the phylactery by skill total and psychic sensitivity.
 public static class Souls {
  public const float BaseValue=50f,PerSkillLevel=5f,MinSensitivity=.2f,MaxSensitivity=4f;
  public static string Unfit(Thing thing){
   if(!(thing is Corpse corpse) || corpse.InnerPawn==null)return "需要人形尸体。";
   Pawn p=corpse.InnerPawn;
   if(!p.RaceProps.Humanlike)return "需要人形尸体。";
   if(!p.RaceProps.IsFlesh || Necro.Undead(p) || NecromancyGenes.HasBody(p))return "死灵躯体没有可汲取的灵魂。";
   if(SpiritStones.Holds(p))return "灵魂被魂石收走了，没法汲取。"; // 1.45.0
   if(corpse.GetRotStage()==RotStage.Dessicated)return "尸体已化为枯骨，灵魂早已散去。";
   if(p.health.hediffSet.GetMissingPartsCommonAncestors().Count>0)return "尸体缺少身体部位，灵魂不完整。";
   return null;
  }
  public static int SkillTotal(Pawn p){int sum=0;var list=p?.skills?.skills;if(list!=null)for(int i=0;i<list.Count;i++)sum+=list[i].Level;return sum;}
  public static float Sensitivity(Pawn p)=>Mathf.Clamp(p.GetStatValue(StatDefOf.PsychicSensitivity),MinSensitivity,MaxSensitivity);
  public static float Value(Pawn p)=>p==null?0:(BaseValue+PerSkillLevel*SkillTotal(p))*Sensitivity(p);
 }
 [HarmonyPatch]
 public static class SoulValueInspect {
  static MethodBase TargetMethod()=>AccessTools.DeclaredMethod(typeof(Corpse),nameof(Corpse.GetInspectString));
  static bool Prepare()=>TargetMethod()!=null;
  static void Postfix(Corpse __instance,ref string __result){
   if(Necro.Registry==null || Necro.Registry.boxes.Count==0)return;
   Pawn p=__instance.InnerPawn;if(p==null || !p.RaceProps.Humanlike)return;
   string unfit=Souls.Unfit(__instance);
   string line=unfit==null?"灵魂价值："+Souls.Value(p).ToString("0")+"献祭经验（技能总和"+Souls.SkillTotal(p)+"，心灵敏感度"+Souls.Sensitivity(p).ToStringPercent()+"）":"无法汲取灵魂："+unfit;
   __result=string.IsNullOrEmpty(__result)?line:__result.TrimEnd()+"\n"+line;
  }
 }
 public static class LichSpells {
  public static readonly (string name,string label,int level)[] Unlocks={("FearGaze","恐惧凝视",3),("NecroticBlast","死灵冲击",6),("PowerWordKill","死亡律令",15)};
  public const int DrainLevel=10,DrainInterval=60,FearStunTicks=300;
  public const float DrainRadius=6.9f,DrainDamage=3f,DrainEntropyPerTarget=1.5f;
  public static DamageDef Necrotic=>Necro.Def<DamageDef>("Necrotic");
  public static int UnlockedLevel(Phylactery box)=>box==null || box.Destroyed?0:Math.Min(box.level,LichMod.Settings.levelCap);
  public static bool Enemy(Pawn caster,Pawn p)=>p!=null && p!=caster && !p.Dead && p.Spawned && p.Map==caster.Map && p.HostileTo(caster);
  public static bool Frighten(Pawn caster,Pawn p){
   // 1.44.0：独眼巨人本源免疫凝视类效果（巫妖法术、卷轴、半巫妖和拉洛克化身都走这里）。
   if(p.RaceProps.IsMechanoid || Necro.Undead(p) || p.GetStatValue(StatDefOf.PsychicSensitivity)<=0 || Cyclops.GazeImmune(p))return false;
   if(p.RaceProps.Humanlike)return p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"恐惧凝视",forced:true,forceWake:true);
   p.stances?.stunner.StunFor(FearStunTicks,caster,false,true,false);return true;
  }
 }
 public partial class Hediff_Lich {
  public bool drainAura;
  int drainTicks;
  public bool DrainUnlocked=>Active && LichSpells.UnlockedLevel(box)>=LichSpells.DrainLevel;
  public void SyncSpells(){
   if(pawn?.abilities==null)return;
   int level=LichSpells.UnlockedLevel(box);
   foreach(var spell in LichSpells.Unlocks){
    var def=Necro.Def<AbilityDef>(spell.name);bool has=pawn.abilities.GetAbility(def)!=null;
    if(level>=spell.level && !has){pawn.abilities.GainAbility(def);if(pawn.Faction==Faction.OfPlayer && !pawn.Dead)Messages.Message(pawn.LabelShort+"的命匣达到"+spell.level+"级，领悟法术："+spell.label+"。",pawn,MessageTypeDefOf.PositiveEvent,false);}
    else if(level<spell.level && has)pawn.abilities.RemoveAbility(def);
   }
   if(drainAura && level<LichSpells.DrainLevel)drainAura=false;
  }
  public string SpellStatus {get{
   int level=LichSpells.UnlockedLevel(box);
   var parts=LichSpells.Unlocks.Select(s=>(label:s.label,level:s.level)).Concat(new[]{(label:"生命汲取光环",level:LichSpells.DrainLevel)}).OrderBy(s=>s.Item2).Select(s=>s.Item1+"（"+s.Item2+"级"+(level>=s.Item2?"，已解锁":"")+"）");
   return "\n命匣法术："+string.Join("、",parts)+(drainAura?"\n生命汲取光环：开启":"");
  }}
  public void DrainTick(int delta){if(!drainAura)return;drainTicks+=delta;if(drainTicks<LichSpells.DrainInterval)return;drainTicks=0;DrainPulse();}
  // Drains every non-downed hostile living creature in range; costs no psyfocus, only entropy per target.
  public int DrainPulse(){
   if(!drainAura)return 0;
   if(!DrainUnlocked){drainAura=false;return 0;}
   if(!pawn.Spawned || pawn.Dead || pawn.Downed || pawn.psychicEntropy==null)return 0;
   Map map=pawn.Map;int hits=0;float healed=0;
   var targets=map.mapPawns.AllPawnsSpawned.Where(p=>LichSpells.Enemy(pawn,p) && !p.Downed && NecromancyGenes.IsLivingTarget(p) && p.Position.InHorDistOf(pawn.Position,LichSpells.DrainRadius)).ToList();
   foreach(var target in targets){
    if(MagicItems.ScarabResists(target))continue; // 1.45.0 圣甲虫护符
    if(!pawn.psychicEntropy.TryAddEntropy(LichSpells.DrainEntropyPerTarget,pawn,true,false)){drainAura=false;Messages.Message(pawn.LabelShort+"的心灵熵已满，生命汲取光环关闭。",pawn,MessageTypeDefOf.NeutralEvent,false);break;}
    healed+=target.TakeDamage(new DamageInfo(LichSpells.Necrotic,LichSpells.DrainDamage,0f,-1f,pawn)).totalDamageDealt;hits++;
    FleckMaker.Static(target.Position,map,FleckDefOf.PsycastAreaEffect,.6f);
   }
   foreach(var injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(i=>i.Severity).ToList()){if(healed<=0)break;float heal=Math.Min(healed,injury.Severity);injury.Heal(heal);healed-=heal;}
   return hits;
  }
  IEnumerable<Gizmo> SpellGizmos(){
   if(!DrainUnlocked && !drainAura)yield break;
   var toggle=new Command_Toggle{defaultLabel="生命汲取光环",defaultDesc="持续汲取"+LichSpells.DrainRadius.ToString("0.#")+"格内所有未倒地的敌对活物：每秒对每个目标造成"+LichSpells.DrainDamage.ToString("0")+"点死灵伤害，并按实际伤害修复自身伤口。不消耗精神力，每命中一个目标增加"+LichSpells.DrainEntropyPerTarget.ToString("0.#")+"心灵熵；心灵熵将满时自动关闭。命匣"+LichSpells.DrainLevel+"级解锁。",icon=Necro.Icon("Icons/NecroticBody"),isActive=()=>drainAura,toggleAction=()=>{drainAura=!drainAura;drainTicks=0;}};
   if(!DrainUnlocked)toggle.Disable("需要有效绑定的"+LichSpells.DrainLevel+"级以上命匣。");
   yield return toggle;
  }
  void ExposeSpells(){Scribe_Values.Look(ref drainAura,"drainAura");Scribe_Values.Look(ref drainTicks,"drainTicks");}
 }
 public class CompProperties_LichSpell : CompProperties_AbilityEffect {public string effect;public float radius;public float damage;public CompProperties_LichSpell(){compClass=typeof(CompLichSpell);}}
 public class CompLichSpell : CompAbilityEffect {
  CompProperties_LichSpell Props=>(CompProperties_LichSpell)props;
  Pawn Caster=>parent.pawn;
  // 1.45.1 骸骨王冠：巫妖戴着时恐惧凝视范围+1格。
  public float Radius=>Props.radius+(Props.effect=="fear" && Items.Worn(Caster,"LP_CrownOfBone")!=null?1f:0f);
  public List<Pawn> Affected(IntVec3 center){var map=Caster.Map;float r=Radius;return map==null?new List<Pawn>():map.mapPawns.AllPawnsSpawned.Where(p=>LichSpells.Enemy(Caster,p) && p.Position.InHorDistOf(center,r)).ToList();}
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   string reason=null;
   if(Props.effect=="kill"){Pawn p=target.Pawn;if(p==null || p.Dead || !p.Spawned || p.Map!=Caster.Map)reason="需要地图上的存活角色。";else if(p==Caster)reason="不能对自己使用。";else if(p.Faction==Faction.OfPlayer)reason="不能对己方殖民者或造物使用。";}
   else if(Caster.Map==null || !target.Cell.IsValid || !target.Cell.InBounds(Caster.Map))reason="需要地图上的目标位置。";
   if(reason!=null && throwMessages)Messages.Message(reason,MessageTypeDefOf.RejectInput,false);
   return reason==null && base.Valid(target,throwMessages);
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   if(!Valid(target))return;Map map=Caster.Map;IntVec3 cell=target.Cell;
   if(Props.effect=="fear")foreach(var p in Affected(cell))LichSpells.Frighten(Caster,p);
   else if(Props.effect=="blast"){float amount=Props.damage*Necro.Mentoring(Caster);foreach(var p in Affected(cell))p.TakeDamage(new DamageInfo(LichSpells.Necrotic,amount,0f,-1f,Caster));}
   else if(Props.effect=="kill"){if(!MagicItems.ScarabResists(target.Pawn))target.Pawn.Kill(new DamageInfo(LichSpells.Necrotic,9999f,999f,-1f,Caster));}
   FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,Math.Max(1f,Props.radius));
  }
  public override void DrawEffectPreview(LocalTargetInfo target){if(Props.radius>0 && target.Cell.IsValid)GenDraw.DrawRadiusRing(target.Cell,Props.radius);}
 }
}
