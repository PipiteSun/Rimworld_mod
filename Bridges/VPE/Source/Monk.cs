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
 // 1.36.0 武僧（格斗，战技强度）：左支 武艺（被动）→ 拨挡箭矢 → 乘风步 → 金刚之躯，右支 疾风连击 → 震慑拳 → 开掌技 → 颤心掌，终极 空灵之身。
 // 拳脚走原版近战结算（Isekai 的力量、武器精通照常生效）；“气”的效果（击晕时长、拨挡量、治疗、颤心爆发）乘心灵敏感度。
 public static class Monk {
  public static readonly Color Ki=new Color(.95f,.8f,.4f);
  // 徒手：没拿武器，或者拿的是短棍。
  public static readonly HashSet<string> MonkWeapons=new HashSet<string>{"MeleeWeapon_Club"};
  public static bool Unarmed(Pawn p)=>p?.equipment!=null && (p.equipment.Primary==null || MonkWeapons.Contains(p.equipment.Primary.def.defName));
  // 无甲：没穿利器护甲 ≥ 0.4 的衣服。
  public static bool Unarmored(Pawn p)=>p?.apparel==null || !p.apparel.WornApparel.Any(a=>a.GetStatValue(StatDefOf.ArmorRating_Sharp)>=.4f);
  public static void RefreshStance(Pawn p){
   float m=Classes.Power(p,SkillDefOf.Melee);
   if(Unarmed(p))Items.Give(p,"LP_MonkStance",MapComponent_ClassAuras.AuraTicks,m);else Items.Remove(p,"LP_MonkStance");
   if(Unarmored(p))Items.Give(p,"LP_MonkUnarmored",MapComponent_ClassAuras.AuraTicks,m);else Items.Remove(p,"LP_MonkUnarmored");
   // 严重度要跟着当前格斗走（Give 只会取较大值）。
   foreach(var name in new[]{"LP_MonkStance","LP_MonkUnarmored"}){var def=Items.Hediff(name);var h=def==null?null:p.health.hediffSet.GetFirstHediffOfDef(def);if(h!=null)h.Severity=m;}
  }
  // 拨挡箭矢：远程伤害减去状态严重度，减到 0 时整发拨开。
  static readonly DefRef<HediffDef> Deflecting=new DefRef<HediffDef>("LP_Deflecting"); // 1.58.6：每次受伤都会查
  public static bool Deflect(Pawn victim,ref DamageInfo dinfo){
   var def=Deflecting.Def;var h=def==null?null:victim.health?.hediffSet?.GetFirstHediffOfDef(def);
   if(h==null || !ForceDragon.IsRanged(dinfo))return false;
   float left=dinfo.Amount-h.Severity;
   if(left<=0f){Items.Mote(victim,"拨开",Ki);dinfo.SetAmount(0f);return true;}
   dinfo.SetAmount(left);return false;
  }
 }

 // 疾风连击：相邻目标连挨 2 拳（强度 ≥ 1.25 时 3 拳）。
 public class Ability_FlurryOfBlows : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  public static int Hits(float strength)=>strength>=1.25f?3:2;
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets){var t=PawnOf(g);if(!Hostile(pawn,t))continue;for(int i=0;i<Hits(Strength) && !t.Dead;i++)ClassMelee.Strike(pawn,t);Mote(t,"疾风连击",Monk.Ki);}}
 }
 // 拨挡箭矢：状态严重度 = 每发减伤量（10 × 战技强度 × 心灵）。
 public class Ability_DeflectMissiles : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){Give(pawn,"LP_Deflecting",Duration,Power);Mote(pawn,"拨挡箭矢",Monk.Ki);}
 }
 // 震慑拳：一拳 + 击晕 2 秒 × 强度（体型 2 以上减半），机械体也有效。
 public class Ability_StunningStrike : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  public static int StunTicks(Pawn t,float strength)=>Mathf.RoundToInt(120f*strength*(t.BodySize>=2f?.5f:1f));
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets){var t=PawnOf(g);if(!Hostile(pawn,t))continue;ClassMelee.Strike(pawn,t);if(!t.Dead)t.stances?.stunner?.StunFor(StunTicks(t,Strength),pawn);Mote(t,"震慑拳",Monk.Ki);}}
 }
 // 开掌技：一拳 + 击退 3 格（体型 2 以上 1 格）+ 倒地 1 秒；落点旁的敌人各受 6 × 战技强度 钝器。
 public class Ability_OpenHand : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets){var t=PawnOf(g);if(Hostile(pawn,t))Push(pawn,t,Strength);}}
  public static IntVec3 Push(Pawn caster,Pawn t,float strength){
   ClassMelee.Strike(caster,t);if(t.Dead || !t.Spawned)return IntVec3.Invalid;
   var dest=DNDPush.Destination(t.Map,t.Position,(t.Position-caster.Position).ToVector3(),t.BodySize>=2f?1:3);
   DNDPush.Move(t,dest);
   t.stances?.stunner?.StunFor(60,caster);
   foreach(var o in Races124.PawnsNear(t.Map,t.Position,1.5f).ToList())if(o!=t && o!=caster && o.HostileTo(caster))o.TakeDamage(new DamageInfo(DamageDefOf.Blunt,6f*strength,.1f,-1f,caster));
   Items.Mote(t,"开掌技",Monk.Ki);
   return t.Position;
  }
 }
 // 金刚之躯：立刻治疗（并清一份毒素/食物中毒），之后受伤 ×0.7、免疫击晕、精神崩溃阈值 -10%。
 public class Ability_DiamondSoul : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){
   Heal(pawn,Power);Ability_LayOnHands.Cleanse(pawn,.2f);
   Give(pawn,"LP_DiamondSoul",Duration,1f);Mote(pawn,"金刚之躯",Monk.Ki);
  }
 }
 // 颤心掌：一拳并种下颤心，3 秒后躯干受 40 × 战技强度 × 心灵 钝器（无视一半护甲）；巨人或体型 3 以上减半。
 public class Ability_QuiveringPalm : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets){var t=PawnOf(g);if(!Hostile(pawn,t))continue;ClassMelee.Strike(pawn,t);if(!t.Dead)Plant(pawn,t,Power);}}
  public static Hediff_QuiveringPalm Plant(Pawn caster,Pawn t,float amount){
   var h=(Hediff_QuiveringPalm)Items.Give(t,"LP_QuiveringPalm",-1,amount);
   if(h!=null){h.caster=caster;h.Severity=amount;}
   Items.Mote(t,"颤心",Monk.Ki);
   return h;
  }
  public static float BurstAmount(Pawn t,float amount)=>amount*(t.BodySize>=3f || GiantKin.Is(t)?.5f:1f);
 }
 public class Hediff_QuiveringPalm : HediffWithComps {
  public Pawn caster;public int ticksLeft=180;
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   ticksLeft-=delta;
   if(ticksLeft<=0)Burst();
  }
  public float Burst(){
   var p=pawn;float amount=Ability_QuiveringPalm.BurstAmount(p,Severity);
   p.health.RemoveHediff(this);
   if(p.Dead)return 0f;
   var torso=p.RaceProps.body.corePart;
   p.TakeDamage(new DamageInfo(DamageDefOf.Blunt,amount,.5f,-1f,caster,torso));
   if(p.Spawned)FleckMaker.ThrowLightningGlow(p.DrawPos,p.Map,1.5f);
   return amount;
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref caster,"lpPalmCaster");Scribe_Values.Look(ref ticksLeft,"lpPalmTicks",180);}
 }
}
