using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 2026-10-28：原版 / 皇权 / VPE 灵能造成的失控（仓皇逃窜、狂暴等，causedByPsycast）也吃心智抗性：
 // 我方（不敌对玩家的）单位先按 LP_MindResist 判定抵抗；没抵抗住，剩余时间 ×(1−抗性)，最多缩短 75%。
 // VPE 在状态开始之后才写 forceRecoverAfterTicks，所以缩短放到下一个游戏 tick 再做。
 public static class ExternalControl {
  public const float MaxCut=.75f;
  static readonly AccessTools.FieldRef<MentalState,int> Age=AccessTools.FieldRefAccess<MentalState,int>("age");
  internal static readonly HashSet<MentalState> pending=new HashSet<MentalState>();
  public static bool Covers(Pawn p)=>p!=null && !p.Dead && (p.Faction==Faction.OfPlayer || p.Faction!=null && !p.Faction.HostileTo(Faction.OfPlayer));
  public static float Cut(Pawn p)=>Mathf.Min(DNDMind.Resistance(p),MaxCut);
  public static bool Shorten(MentalState ms){
   var p=ms?.pawn;if(p==null || p.MentalState!=ms)return false;
   float f=1f-Cut(p);if(f>=1f)return false;
   int age=Age(ms),left=ms.forceRecoverAfterTicks>0?ms.forceRecoverAfterTicks-age:ms.def.maxTicksBeforeRecovery>0 && ms.def.maxTicksBeforeRecovery<99999?ms.def.maxTicksBeforeRecovery-age:-1;
   if(left<=0)return false;
   ms.forceRecoverAfterTicks=age+Mathf.Max(1,Mathf.RoundToInt(left*f));return true;
  }
  public static int ApplyPending(){
   if(pending.Count==0)return 0;int n=0;
   foreach(var ms in pending)if(Shorten(ms))n++;
   pending.Clear();return n;
  }
 }
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class ExternalControlStart {
  // 参数顺序：stateDef, reason, forced, forceWake, causedByMood, otherPawn, transitionSilently, causedByDamage, causedByPsycast
  static bool Psycast(object[] a)=>a!=null && a.Length>8 && a[8] is bool b && b && !(a[4] is bool m && m);
  [HarmonyPriority(Priority.Low)]
  static bool Prefix(MentalStateDef stateDef,Pawn ___pawn,object[] __args,ref bool __result){
   if(stateDef==null || !Psycast(__args) || !ExternalControl.Covers(___pawn) || !DNDMind.TryResist(___pawn))return true;
   Items.Mote(___pawn,"抵抗！",new Color(.7f,.85f,1f));__result=false;return false;
  }
  static void Postfix(Pawn ___pawn,object[] __args,bool __result){
   if(__result && Psycast(__args) && ExternalControl.Covers(___pawn) && ___pawn.MentalState!=null && ExternalControl.Cut(___pawn)>0f)ExternalControl.pending.Add(___pawn.MentalState);
  }
 }
 public class GameComponent_ExternalControl:GameComponent {
  public GameComponent_ExternalControl(Game game){}
  public override void GameComponentTick(){if(ExternalControl.pending.Count>0)ExternalControl.ApplyPending();}
 }
}
