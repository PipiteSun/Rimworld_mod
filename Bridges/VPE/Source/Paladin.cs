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
 // 1.36.0 圣武士（主 格斗 / 副 社交）：左支格斗（至圣斩、誓仇、神圣武器、援护换位），右支社交（圣疗、守护灵光、勇气灵光、放逐邪恶），终极 圣光化身。
 // 武器那一下走原版近战（Isekai 的力量、精炼照常生效）；追加的神圣伤害和治疗是神术，乘心灵敏感度（这些能力 psychicMode=Yes）。
 public static class Paladin {
  public static readonly Color Holy=new Color(1f,.92f,.6f);
  public static float Holy2(Pawn t,float amount)=>Ability_SacredFlame.Amount(t,amount);
  public static bool AuraTarget(Pawn source,Pawn ally)=>ally!=null && !ally.Dead && (ally==source || ally.Faction!=null && ally.Faction==source.Faction && !ally.HostileTo(source));
  // 近战命中：至圣斩（一次性）与神圣武器（每次）追加神圣伤害。返回追加的总伤害。
  public static float OnMeleeHit(Pawn caster,Pawn t){
   float total=0f;var radiant=Ability_SacredFlame.Radiant??DamageDefOf.Burn;
   var smite=SmiteReady.Def;var h=smite==null?null:caster.health?.hediffSet?.GetFirstHediffOfDef(smite);
   if(h!=null){float a=Holy2(t,h.Severity);caster.health.RemoveHediff(h);t.TakeDamage(new DamageInfo(radiant,a,.4f,-1f,caster));total+=a;if(t.Map!=null)FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,1.5f);}
   var sw=SacredWeapon.Def;var w=sw==null?null:caster.health?.hediffSet?.GetFirstHediffOfDef(sw);
   if(w!=null && !t.Dead){float a=Holy2(t,w.Severity);t.TakeDamage(new DamageInfo(radiant,a,.4f,-1f,caster));total+=a;}
   return total;
  }
  // 誓仇：施法者对被标记的目标伤害 ×(1 + 0.25 × 战技强度)。
  public static void ApplyEnmity(Pawn victim,ref DamageInfo dinfo){
   Hediff_Enmity mark=null;var list=victim.health?.hediffSet?.hediffs;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i] is Hediff_Enmity e){mark=e;break;} // 1.58.6：每次受伤都会走到，不用 LINQ
   if(mark==null || mark.caster==null || dinfo.Instigator!=mark.caster)return;
   dinfo.SetAmount(dinfo.Amount*EnmityFactor(mark.Severity));
  }
  public static float EnmityFactor(float strength)=>1f+.25f*strength;
  // 1.58.6：每一刀都会查，用静态 DefRef。
  static readonly DefRef<HediffDef> SmiteReady=new DefRef<HediffDef>("LP_SmiteReady"),SacredWeapon=new DefRef<HediffDef>("LP_SacredWeapon");
  public static void OnKilled(Pawn dead,Pawn killer){
   var mark=dead.health?.hediffSet?.hediffs.OfType<Hediff_Enmity>().FirstOrDefault();
   var c=mark?.caster;if(c==null || c.Dead)return;
   var thought=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_EnmityAvenged");
   if(thought!=null)c.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
  }
 }

 // 至圣斩 / 神圣武器：给自己挂状态，严重度 = 每次追加的神圣伤害。
 public class Ability_DivineSmite : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){Give(pawn,def.defName=="LP_DivineSmite"?"LP_SmiteReady":"LP_SacredWeapon",Duration,Power);Mote(pawn,def.LabelCap,Paladin.Holy);}
 }
 // 圣疗：治疗 25 × 神恩强度；没有伤口时清除毒素积累或食物中毒。
 public class Ability_LayOnHands : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,true,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets){var t=PawnOf(g);if(Friendly(pawn,t))Mote(t,Touch(t,Power),Paladin.Holy);}}
  public static string Touch(Pawn t,float amount){
   if(InjurySum(t)>0f)return "圣疗 "+Heal(t,amount).ToString("0");
   return Cleanse(t,.3f)??"圣疗";
  }
  public static string Cleanse(Pawn t,float toxic){
   var food=t.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.FoodPoisoning);
   if(food!=null){t.health.RemoveHediff(food);return "解除食物中毒";}
   var tox=t.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);
   if(tox!=null){tox.Severity-=toxic;if(tox.Severity<=0f)t.health.RemoveHediff(tox);return "清除毒素";}
   return null;
  }
 }
 // 誓仇：标记一个敌人 1 天。
 public class Ability_VowOfEnmity : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets){var t=PawnOf(g);if(Hostile(pawn,t))Mark(pawn,t,Duration,Strength);}}
  public static Hediff_Enmity Mark(Pawn caster,Pawn t,int ticks,float strength){
   foreach(var old in t.health.hediffSet.hediffs.OfType<Hediff_Enmity>().ToList())t.health.RemoveHediff(old);
   var h=(Hediff_Enmity)Items.Give(t,"LP_Enmity",ticks,strength);
   if(h!=null){h.caster=caster;h.Severity=strength;}
   Items.Mote(t,"誓仇",Paladin.Holy);
   return h;
  }
 }
 public class Hediff_Enmity : HediffWithComps {
  public Pawn caster;
  public override string LabelInBrackets=>caster==null?base.LabelInBrackets:caster.LabelShort;
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref caster,"lpEnmityCaster");}
 }
 // 援护换位：和友军瞬间换位，友军 10 秒受伤 ×0.5。
 public class Ability_Intercept : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(!base.ValidateTarget(target,showMessages) || !TargetPawnOk(target,true,showMessages,out var t))return false;
   if(t==pawn){if(showMessages)Messages.Message("需要选择另一名友军。",MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets){var t=PawnOf(g);if(t!=null && t!=pawn && Friendly(pawn,t))Swap(pawn,t);}}
  public static bool Swap(Pawn a,Pawn b){
   if(!a.Spawned || !b.Spawned || a.Map!=b.Map)return false;
   var pa=a.Position;var pb=b.Position;
   a.pather?.StopDead();b.pather?.StopDead();
   a.Position=pb;b.Position=pa;
   a.Notify_Teleported(false,true);b.Notify_Teleported(false,true);
   Items.Give(b,"LP_Sheltered",600,1f);
   FleckMaker.ThrowLightningGlow(a.DrawPos,a.Map,1f);FleckMaker.ThrowLightningGlow(b.DrawPos,b.Map,1f);
   Items.Mote(b,"受庇护",Paladin.Holy);
   return true;
  }
 }
 // 放逐邪恶：半径内敌对的亡灵、邪魔、夺心魔受神圣伤害并击晕 2 秒；敌对召唤物直接驱散。
 public class Ability_DispelEvil : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){Dispel(pawn,Radius,Power);Mote(pawn,"放逐邪恶",Paladin.Holy);}
  public static int Dispel(Pawn caster,float radius,float amount){
   if(caster.Map==null)return 0;int n=0;var radiant=Ability_SacredFlame.Radiant??DamageDefOf.Burn;
   foreach(var t in Races124.PawnsNear(caster.Map,caster.Position,radius).ToList()){
    if(t==caster || !t.HostileTo(caster))continue;
    if(ClassRules.Conjured(t)){HediffComp_Conjured.Vanish(t);n++;continue;}
    if(!Classes.HolyVulnerable(t))continue;
    t.TakeDamage(new DamageInfo(radiant,amount,.4f,-1f,caster));
    if(!t.Dead)t.stances?.stunner?.StunFor(120,caster);
    FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,1.5f);n++;
   }
   return n;
  }
 }
}
