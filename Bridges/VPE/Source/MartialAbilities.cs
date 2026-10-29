using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;
using MartialCore=LichPhylactery.VPEBridge.Martial; // ClassAbility 里的 Martial 是颜色

namespace LichPhylactery.VPEBridge {
 // 1.53.0 战士补完的新能力：格斗支 冲锋、缴械击；射击支 稳固瞄准（Ability_SelfBuff）、压制射击、精准射击、连射与死神之眼（Ability_SelfBuff）。
 // 射击支的子弹都是施法者当前武器自己的子弹（launcher = 施法者，equipment = 武器），倍率登记在 MartialCore.Shots。

 // 冲锋：跳到 8 格内敌人身边（PawnFlyer），落地立刻一次近战结算，伤害 ×(1 + 0.3 × 战技强度)；体型 ≤ 1 的目标击晕 1 秒。
 public class Ability_Charge : ClassAbility {
  public const float PerPower=.3f;public const int KnockTicks=60;
  public float Factor=>1f+PerPower*Strength;
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(!base.ValidateTarget(target,showMessages) || !TargetPawnOk(target,false,showMessages,out var t))return false;
   if(!t.Position.AdjacentTo8WayOrInside(pawn.Position) && !LandingCell(pawn,t).IsValid){if(showMessages)Messages.Message("目标身边没有能落脚的格子。",MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
  // 目标身边离施法者最近、能站立、没人的格子。
  public static IntVec3 LandingCell(Pawn caster,Pawn t){
   var map=t.Map;if(map==null)return IntVec3.Invalid;
   IntVec3 best=IntVec3.Invalid;float bd=float.MaxValue;
   foreach(var c in GenAdj.CellsAdjacent8Way(t)){if(!c.InBounds(map) || !c.Standable(map) || c.GetFirstPawn(map)!=null)continue;float d=c.DistanceToSquared(caster.Position);if(d<bd){bd=d;best=c;}}
   return best;
  }
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets){var t=PawnOf(g);if(Hostile(pawn,t))Charge(pawn,t,Factor);}}
  public static bool Charge(Pawn caster,Pawn t,float factor){
   if(caster?.Map==null || t==null || t.Dead)return false;
   if(t.Position.AdjacentTo8WayOrInside(caster.Position)){Land(caster,t,factor);return true;}
   var cell=LandingCell(caster,t);if(!cell.IsValid)return false;
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("LP_ChargeFlyer");
   var flyer=def==null?null:PawnFlyer.MakeFlyer(def,caster,cell,null,null) as LP_ChargeFlyer;
   if(flyer==null){Races124.Blink(caster,cell);Land(caster,t,factor);return true;}
   flyer.target=t;flyer.factor=factor;
   GenSpawn.Spawn(flyer,cell,caster.Map);
   Items.Mote(caster,"冲锋",MartialCore.Color);
   return true;
  }
  // 落地：冲锋那一击（LP_ChargeStrike 只在这一下存在）+ 撞倒。
  public static bool Land(Pawn caster,Pawn t,float factor){
   if(caster==null || caster.Dead || !caster.Spawned || t==null || t.Dead || !t.Spawned || t.Map!=caster.Map || !t.Position.AdjacentTo8WayOrInside(caster.Position))return false;
   Items.Give(caster,"LP_ChargeStrike",60,factor);
   try{ClassMelee.Strike(caster,t);}finally{Items.Remove(caster,"LP_ChargeStrike");}
   if(!t.Dead && t.BodySize<=1f){t.stances?.stunner?.StunFor(KnockTicks,caster);Items.Mote(t,"撞倒",MartialCore.Color);}
   return true;
  }
 }
 // 冲锋的飞行：原版跳跃同款（PawnFlyerBase），落地时结算那一击。
 public class LP_ChargeFlyer : PawnFlyer {
  public Pawn target;public float factor=1f;
  protected override void RespawnPawn(){
   var p=FlyingPawn;base.RespawnPawn();
   if(p!=null)Ability_Charge.Land(p,target,factor);
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref target,"lpChargeTarget");Scribe_Values.Look(ref factor,"lpChargeFactor",1f);}
 }

 // 缴械击：相邻敌人一次近战结算，然后判定打落武器（成功率见 MartialCore.DisarmChance，力量调整值）。机械体、动物、没拿武器的只吃那一击。
 public class Ability_DisarmingStrike : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(!Hostile(pawn,t))continue;
    ClassMelee.Strike(pawn,t);
    if(!t.Dead && MartialCore.CanDisarm(t))MartialCore.Disarm(pawn,t,MartialCore.DisarmChance(pawn,t,Strength,Adventurer.Str));
   }
  }
 }

 // 射击支公共：需要远程武器，射程 = 当前武器射程。
 public abstract class MarksmanAbility : ClassAbility {
  public override float GetRangeForPawn(){var v=MartialCore.RangedVerb(pawn);return v==null?0f:v.EffectiveRange;}
  public override bool IsEnabledForPawn(out string reason){
   if(!base.IsEnabledForPawn(out reason))return false;
   if(MartialCore.RangedVerb(pawn)?.Projectile==null){reason="需要远程武器";return false;}
   return true;
  }
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(MartialCore.RangedVerb(pawn)?.Projectile==null){if(showMessages)Messages.Message("需要远程武器。",MessageTypeDefOf.RejectInput,false);return false;}
   return base.ValidateTarget(target,showMessages);
  }
 }
 // 压制射击：目标格半径 3，连射 6 发当前武器的子弹（每 6 刻一发，落点区域内随机格，伤害 ×0.5）；区域内敌人被压制 6 秒。
 public class Ability_SuppressingFire : MarksmanAbility {
  public const int Shots=6,Interval=6,SuppressTicks=360;public const float Area=3f,DamageFactor=.5f;
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)if(g.Cell.IsValid)Start(pawn,g.Cell);}
  public static int Start(Pawn caster,IntVec3 cell){
   var map=caster.Map;if(map==null)return 0;int n=0;
   foreach(var t in Races124.PawnsNear(map,cell,Area).ToList())if(t!=caster && !t.Dead && t.HostileTo(caster)){Items.Give(t,"LP_Suppressed",SuppressTicks);n++;}
   var v=(Hediff_SuppressingVolley)Items.Give(caster,"LP_SuppressingVolley");
   if(v!=null){v.cell=cell;v.left=Shots;v.acc=Interval;}
   Items.Mote(caster,"压制射击",MartialCore.Color);
   return n;
  }
  public static Projectile FireOne(Pawn caster,IntVec3 center){
   var map=caster.Map;if(map==null)return null;
   var cells=GenRadial.RadialCellsAround(center,Area,true).Where(c=>c.InBounds(map)).ToList();
   var cell=cells.Count==0?center:cells.RandomElement();var target=(LocalTargetInfo)cell;
   var victim=cell.GetFirstPawn(map);if(victim!=null && victim.HostileTo(caster))target=victim;
   return MartialCore.Fire(caster,target,new ShotMod{damage=DamageFactor,caster=caster},false);
  }
 }
 // 压制射击的连射：挂在施法者身上，每 6 刻打一发，打完自己移除（只在这个人的状态刻里走，不新开循环）。
 public class Hediff_SuppressingVolley : HediffWithComps {
  public IntVec3 cell=IntVec3.Invalid;public int left,acc;
  public override bool ShouldRemove=>base.ShouldRemove || left<=0 || !cell.IsValid;
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(left<=0 || pawn==null || !pawn.Spawned || pawn.Downed || pawn.Dead){left=0;return;}
   acc+=delta;
   while(acc>=Ability_SuppressingFire.Interval && left>0){acc-=Ability_SuppressingFire.Interval;left--;Ability_SuppressingFire.FireOne(pawn,cell);}
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref cell,"lpVolleyCell",IntVec3.Invalid);Scribe_Values.Look(ref left,"lpVolleyLeft");Scribe_Values.Look(ref acc,"lpVolleyAcc");}
 }
 // 精准射击：立刻对目标打一发必中的子弹，伤害 ×(1.5 + 0.5 × 射术强度)，穿甲 +0.3；模式（征召时的按钮）：要害 / 打腿 ×0.7 + 跛行 / 打手 ×0.5 + 缴械判定。
 public class Ability_PrecisionShot : MarksmanAbility {
  public const float Base=1.5f,PerPower=.5f,Penetration=.3f,LegFactor=.7f,HandFactor=.5f;
  public static string ModeLabel(PrecisionMode m)=>m==PrecisionMode.Leg?"打腿":m==PrecisionMode.Hand?"打手":"要害";
  public static float DamageFor(float power,PrecisionMode m)=>(Base+PerPower*power)*(m==PrecisionMode.Leg?LegFactor:m==PrecisionMode.Hand?HandFactor:1f);
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   var mode=MartialCore.State?.Mode(pawn)??PrecisionMode.Vital;
   foreach(var g in targets){var t=PawnOf(g);if(Hostile(pawn,t))Shoot(pawn,t,Strength,mode);}
  }
  public static Projectile Shoot(Pawn caster,Pawn t,float power,PrecisionMode mode){
   var proj=MartialCore.Fire(caster,t,new ShotMod{damage=DamageFor(power,mode),ap=Penetration,power=power,mode=mode,precision=true,caster=caster},true);
   if(proj!=null)Items.Mote(caster,"精准射击·"+ModeLabel(mode),MartialCore.Color);
   return proj;
  }
 }
}
