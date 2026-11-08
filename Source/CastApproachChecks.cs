using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.129.0 热修：射程外施放（Test.ps1 -Only CastApproach，6 条）。
 public partial class LichChecks {
  IntVec3 CrFarCell(Pawn from,float min,float max){
   IntVec3 best=IntVec3.Invalid;float bd=999f;
   foreach(var c in map.AllCells){
    float d=c.DistanceTo(from.Position);if(d<min || d>max || !ScFree(c) || !from.CanReach(c,PathEndMode.OnCell,Danger.Deadly))continue;
    float k=Mathf.Abs(d-(min+max)/2f);if(k<bd){bd=k;best=c;}
   }
   return best;
  }
  Pawn CrFoe(IntVec3 c){var f=AdvPawn(null,AdvEnemy);GenSpawn.Spawn(f,c,map);f.mindState.enemyTarget=null;f.stances.stunner.StunFor(20000,null,false);return f;}
  static void CrTarget(Verb v){Find.Targeter.StopTargeting();Find.Targeter.BeginTargeting(v,null,true);}
  void CheckCastRange(){
   var made=new List<Thing>();
   try{
    // 1. 名单：本模组的指向能力大多参与；位移（传送 / 迷踪步 / 冲锋 / 次元门）、跳跃、接触、对自己的不参与；补丁都挂上了。
    string[] yes={"LP_WL_Hex","LP_WL_EldritchBlast","LP_MagicMissile","LP_FeatMagicMissile","LP_WS_ChainLightning","LP_Trip"},
     no={"LP_MistyStep","LP_Teleport","LP_WL_MistyEscape","LP_Charge","LP_WS_DimensionDoor","LP_RadiantSoulLeap","LP_OozeEngulf","LP_SecondWind","LP_EK_ArcaneCharge","LP_AC_CloudStep"};
    var yesBad=yes.Where(n=>!CastApproach.Allowed(DefDatabase<AbilityDef>.GetNamedSilentFail(n))).ToList();var noBad=no.Where(n=>DefDatabase<AbilityDef>.GetNamedSilentFail(n)==null || CastApproach.Allowed(DefDatabase<AbilityDef>.GetNamed(n))).ToList();
    bool patched=new[]{nameof(Verb_CastAbility.ValidateTarget),nameof(Verb_CastAbility.OrderForceTarget),nameof(Verb_CastAbility.OnGUI)}.All(m=>Harmony.GetPatchInfo(AccessTools.Method(typeof(Verb_CastAbility),m))?.Prefixes.Any(p=>p.owner=="local.lich.phylactery")==true);
    Check(CastApproach.AllowedCount>=150 && yesBad.Count==0 && noBad.Count==0 && patched && CastApproach.TargeterPatched==1 && CastRangeDefOf.LP_CastAbilityApproach?.driverClass==typeof(JobDriver_CastApproach),
     "approach list: "+CastApproach.AllowedCount+" abilities take far targets; movement/jump/touch/self excluded (misplaced: "+string.Join(",",yesBad.Concat(noBad))+"); patches on");
    // 2. 选目标：射程外的敌人在选目标界面里被接受（不在选目标时 / AI 仍然拒绝）；传送类（雾遁）射程外的格子仍然拒绝。
    var b=WlPawn(5,5);var hb=Adventurer.Get(b);GenSpawn.Spawn(b,ScCell(-14,14),map);made.Add(b);if(b.drafter!=null)b.drafter.Drafted=true;
    if(hb?.classes!=null)hb.classes.res[ClassSystem.PoolKey(WL)]=ClassSystem.MaxResource(hb,WL);
    if(b.abilities.GetAbility(Ab("LP_WL_Hex"))==null)b.abilities.GainAbility(Ab("LP_WL_Hex"));
    var hex=b.abilities.GetAbility(Ab("LP_WL_Hex"));var blast=b.abilities.GetAbility(Ab("LP_WL_EldritchBlast"));
    if(b.abilities.GetAbility(Ab("LP_WL_MistyEscape"))==null)b.abilities.GainAbility(Ab("LP_WL_MistyEscape"));var esc=b.abilities.GetAbility(Ab("LP_WL_MistyEscape"));
    Check(hb?.classes!=null && b.drafter!=null && hex!=null && blast!=null && esc!=null,"fixture: warlock 5 with 咒术 / 魔能爆 / 雾遁 (classes "+(hb?.classes!=null)+", drafter "+(b.drafter!=null)+", hex "+(hex!=null)+", blast "+(blast!=null)+", escape "+(esc!=null)+")");
    float hexRange=hex?.verb.EffectiveRange??24f;var farCell=CrFarCell(b,hexRange+6f,hexRange+12f);var foe=farCell.IsValid?CrFoe(farCell):null;if(foe!=null)made.Add(foe);
    bool canHit=foe!=null && hex.verb.CanHitTarget(foe),noTargeter=foe!=null && hex.verb.ValidateTarget(foe,false);
    CrTarget(hex.verb);bool accepted=foe!=null && hex.verb.ValidateTarget(foe,false),cursor=foe!=null && CastApproach.SourceCanHit(hex.verb,foe);
    var escCell=CrFarCell(b,14f,20f);CrTarget(esc.verb);bool escAccepted=escCell.IsValid && esc.verb.ValidateTarget(escCell,false);Find.Targeter.StopTargeting();
    Check(hex!=null && blast!=null && esc!=null && foe!=null && !canHit && !noTargeter && accepted && cursor && escCell.IsValid && !escAccepted,
     "咒术 at "+(foe!=null?b.Position.DistanceTo(foe.Position).ToString("0"):"?")+" cells (range "+hexRange.ToString("0")+"): accepted while targeting, refused outside the targeter; 雾遁 still refuses a far cell");
    // 3. 下命令：排上“走近以施放”工作，开始走路；契约法力和冷却都没动。
    float res0=ClassSystem.Resource(hb,WL);CrTarget(hex.verb);hex.verb.OrderForceTarget(foe);Find.Targeter.StopTargeting();
    var job=b.CurJob;Find.TickManager.DoSingleTick();bool moving=b.pather.Moving;
    Check(job?.def==CastRangeDefOf.LP_CastAbilityApproach && job.ability==hex && job.targetA.Thing==foe && moving && Mathf.Approximately(ClassSystem.Resource(hb,WL),res0) && hex.CooldownTicksRemaining==0,
     "ordering a far 咒术 queues the approach job (pawn walking), no 契约法力 ("+res0+" → "+ClassSystem.Resource(hb,WL)+") or cooldown spent yet");
    // 4. 走到射程内就换成原版施放：施放后才扣 1 点契约法力、进冷却；站位在射程内。
    bool sawCast=false;int ticks=0;
    for(;ticks<2500 && ClassSystem.Resource(hb,WL)>=res0-.01f;ticks++){Find.TickManager.DoSingleTick();if(b.CurJobDef==JobDefOf.CastAbilityOnThing && b.CurJob.ability==hex)sawCast=true;}
    float dist=b.Position.DistanceTo(foe.Position);
    Check(sawCast && ClassSystem.Resource(hb,WL)<res0-.5f && hex.CooldownTicksRemaining>0 && dist<=hexRange+.5f,
     "after "+ticks+" ticks the warlock reached "+dist.ToString("0.#")+" cells, switched to the vanilla cast job and only then paid ("+res0+" → "+ClassSystem.Resource(hb,WL)+"), cooldown "+hex.CooldownTicksRemaining);
    // 5. 目标消失：走近途中目标被摧毁 → 干净结束，魔能爆不进冷却。
    b.jobs.EndCurrentJob(JobCondition.InterruptForced);var far2=CrFarCell(b,blast.verb.EffectiveRange+6f,blast.verb.EffectiveRange+14f);var foe2=far2.IsValid?CrFoe(far2):null;if(foe2!=null)made.Add(foe2);
    if(foe2!=null){CrTarget(blast.verb);blast.verb.OrderForceTarget(foe2);Find.Targeter.StopTargeting();}
    bool approach2=b.CurJobDef==CastRangeDefOf.LP_CastAbilityApproach;foe2?.Destroy();for(int i=0;i<5;i++)Find.TickManager.DoSingleTick();
    Check(foe2!=null && approach2 && b.CurJobDef!=CastRangeDefOf.LP_CastAbilityApproach && blast.CooldownTicksRemaining==0,"the approach ends cleanly when the target is destroyed; 魔能爆 stays off cooldown");
    // 6. 射程内：照旧直接施放（没有走路这一步）。
    b.jobs.EndCurrentJob(JobCondition.InterruptForced);var near=CrFoe(NextTo(b));made.Add(near);
    CrTarget(blast.verb);blast.verb.OrderForceTarget(near);Find.Targeter.StopTargeting();
    Check(b.CurJobDef==JobDefOf.CastAbilityOnThing && b.CurJob.ability==blast,"a target already in range still gets the vanilla cast job straight away ("+b.CurJobDef?.defName+")");
   }finally{Find.Targeter?.StopTargeting();foreach(var t in made)if(!t.Destroyed)t.Destroy();foreach(var p in advMade)if(!p.Destroyed)p.Destroy();advMade.Clear();}
  }
 }
}
