using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
namespace LichPhylactery {
 // 1.138.0 奴隶麻醉项圈（Bridges/Ideology/Defs/SlaveCollar.xml）。戴着它的奴隶：
 //  · 进入「叛乱」心理状态（煽动叛乱的人）或任何攻击性心理状态（狂暴、杀人狂怒等）时；
 //  · 被卷进一场奴隶叛乱（SlaveRebellionUtility.StartSlaveRebellion 建出的 LordJob_SlaveRebellion）时；
 // 项圈立刻结束心理状态、把人移出叛乱小队、加 LP_CollarAnesthetic（意识上限 10%，半天）并把压制度 +30%，然后要充填 2 天。
 // 测试：Test.ps1 -Only SlaveCollar（Source/SlaveCollarChecks.cs）。
 public class CompProperties_AnestheticCollar : CompProperties {
  public int rechargeTicks=120000;public float suppressionGain=.3f;
  public CompProperties_AnestheticCollar(){compClass=typeof(CompAnestheticCollar);}
 }
 public class CompAnestheticCollar : ThingComp {
  public int lastFired=-999999;public int timesFired;
  public CompProperties_AnestheticCollar Props=>(CompProperties_AnestheticCollar)props;
  public int TicksLeft=>Math.Max(0,lastFired+Props.rechargeTicks-Find.TickManager.TicksGame);
  public bool Ready=>TicksLeft<=0;
  public override void PostExposeData(){Scribe_Values.Look(ref lastFired,"lpCollarFired",-999999);Scribe_Values.Look(ref timesFired,"lpCollarTimes",0);}
  public string Status=>Ready?"麻药囊：已充满（遇到叛乱会立刻触发）":"麻药囊：重新充填中，还要 "+TicksLeft.ToStringTicksToPeriod();
  public override string CompInspectStringExtra()=>Status;
  public override IEnumerable<Gizmo> CompGetWornGizmosExtra(){
   var p=(parent as Apparel)?.Wearer;if(p==null || p.Faction!=Faction.OfPlayer)yield break;
   yield return new Command_Action{defaultLabel=Ready?"麻醉项圈：就绪":"麻醉项圈：充填中",defaultDesc=Status+"\n\n戴着这个项圈的奴隶一开始叛乱、煽动叛乱或发狂伤人，就会被注入麻醉剂昏睡约半天，醒来后压制度+30%。只对奴隶起作用。"+(timesFired>0?"\n已经触发过 "+timesFired+" 次。":""),
    icon=parent.def.uiIcon,defaultIconColor=parent.DrawColor,action=()=>{if(DebugSettings.godMode)lastFired=-999999;}};
  }
 }

 public static class SlaveCollar {
  static HediffDef hediff;public static HediffDef Hediff=>hediff??(hediff=DefDatabase<HediffDef>.GetNamedSilentFail("LP_CollarAnesthetic"));
  static bool busy;
  public static CompAnestheticCollar Collar(Pawn p){
   var worn=p?.apparel?.WornApparel;if(worn==null)return null;
   for(int i=0;i<worn.Count;i++){var c=worn[i].GetComp<CompAnestheticCollar>();if(c!=null)return c;}
   return null;
  }
  // 是否该触发：叛乱 / 攻击性心理状态。
  public static bool Triggers(MentalStateDef d)=>d!=null && (d.defName=="Rebellion" || d.IsAggro);
  // 触发项圈：成功返回 true。silent=true 时不单独弹消息（叛乱时合并成一条）。
  public static bool TryFire(Pawn p,bool silent=false){
   if(busy || p==null || p.Dead || !p.IsSlave)return false;
   var c=Collar(p);if(c==null || !c.Ready || Hediff==null)return false;
   busy=true;
   try{
    c.lastFired=Find.TickManager.TicksGame;c.timesFired++;
    if(p.InMentalState)p.MentalState.RecoverFromState();
    var lord=p.GetLord();
    var h=HediffMaker.MakeHediff(Hediff,p);p.health.AddHediff(h);
    if(lord!=null && lord.LordJob is LordJob_SlaveRebellion && p.GetLord()==lord)lord.RemovePawn(p);
    var need=p.needs?.TryGetNeed<Need_Suppression>();if(need!=null)need.CurLevel=Mathf.Min(1f,need.CurLevel+c.Props.suppressionGain);
    p.jobs?.StopAll();
    if(p.Spawned){MoteMaker.ThrowText(p.DrawPos,p.Map,"麻醉！",new Color(.75f,.6f,1f),3.5f);FleckMaker.ThrowSmoke(p.DrawPos,p.Map,.6f);}
    if(!silent && p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+" 刚要作乱，奴隶麻醉项圈就注入了麻醉剂，"+p.LabelShort+" 昏睡过去了。",p,MessageTypeDefOf.PositiveEvent);
    return true;
   }catch(Exception e){Log.Warning("[DND边缘大陆] 奴隶麻醉项圈触发出错："+e);return false;}
   finally{busy=false;}
  }
  // 一场叛乱开始后：把地图上所有叛乱小队里戴项圈的奴隶放倒。
  public static int FireRebellion(Map map){
   if(map==null)return 0;var hit=new List<Pawn>();
   foreach(var lord in map.lordManager.lords.ToList()){
    if(!(lord.LordJob is LordJob_SlaveRebellion))continue;
    foreach(var p in lord.ownedPawns.ToList())if(TryFire(p,true))hit.Add(p);
   }
   if(hit.Count>0 && hit.Any(p=>p.Faction==Faction.OfPlayer))
    Messages.Message("奴隶叛乱：奴隶麻醉项圈放倒了 "+string.Join("、",hit.Select(p=>p.LabelShort))+"。",new LookTargets(hit),MessageTypeDefOf.PositiveEvent);
   return hit.Count;
  }
 }

 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 static class Patch_SlaveCollar_MentalState {
  static void Postfix(bool __result,MentalStateHandler __instance,MentalStateDef stateDef,Pawn ___pawn){
   if(!__result || !SlaveCollar.Triggers(stateDef) || ___pawn==null || !___pawn.IsSlave)return;
   SlaveCollar.TryFire(___pawn);
  }
 }
 [HarmonyPatch(typeof(SlaveRebellionUtility),nameof(SlaveRebellionUtility.StartSlaveRebellion),new[]{typeof(Pawn),typeof(string),typeof(string),typeof(LetterDef),typeof(LookTargets),typeof(bool)},
  new[]{ArgumentType.Normal,ArgumentType.Out,ArgumentType.Out,ArgumentType.Out,ArgumentType.Out,ArgumentType.Normal})]
 static class Patch_SlaveCollar_Rebellion {
  static void Postfix(bool __result,Pawn initiator){if(__result)SlaveCollar.FireRebellion(initiator?.MapHeld);}
 }
}
