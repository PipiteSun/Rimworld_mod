using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 2026-11-04 热修：敌对野兽 / 怪物进入「仓皇逃窜」后，征召小人和炮塔仍会自动攻击它。
 // 原版两处让它“消失”：①野生发狂动物的敌意只来自发狂状态，切成逃窜后 HostileTo 变 false；②GenHostility.IsPotentialThreat 直接把逃窜者排除，近战待命不砍。
 // 只管非人形、非玩家阵营、开始逃窜那一刻仍敌对玩家的单位；被打跑的猎物、逃跑的人形袭击者保持原版。标记按 thingIDNumber 存档。
 public static class FleeingBeastTarget {
  internal static GameComponent_FleeingBeasts Inst;
  static GameComponent_FleeingBeasts Comp=>Inst!=null && Inst.game==Current.Game ? Inst : (Inst=Current.Game?.GetComponent<GameComponent_FleeingBeasts>());
  public static bool Marked(Pawn p){var c=Comp;return c!=null && p!=null && p.MentalStateDef==MentalStateDefOf.PanicFlee && c.fled.Contains(p.thingIDNumber);}
  public static void Note(Pawn p){
   var c=Comp;if(c==null || p==null)return;
   c.fled.Remove(p.thingIDNumber);
   if(!p.RaceProps.Humanlike && p.Faction!=Faction.OfPlayer && Faction.OfPlayer!=null && p.HostileTo(Faction.OfPlayer))c.fled.Add(p.thingIDNumber);
  }
  public static bool PlayerSide(Thing t)=>t?.Faction!=null && t.Faction.IsPlayer;
 }
 public class GameComponent_FleeingBeasts : GameComponent {
  public HashSet<int> fled=new HashSet<int>(); // 开始逃窜时仍敌对玩家的单位
  internal Game game;
  public GameComponent_FleeingBeasts(Game game){this.game=game;FleeingBeastTarget.Inst=this;}
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref fled,"fled",LookMode.Value);if(Scribe.mode==LoadSaveMode.PostLoadInit && fled==null)fled=new HashSet<int>();}
 }
 // 开始逃窜前记下是否敌对（此时发狂状态还在）；原版随后会刷新索敌缓存。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class FleeingBeastStartPatch {
  static void Prefix(MentalStateDef __0,Pawn ___pawn){if(__0==MentalStateDefOf.PanicFlee)FleeingBeastTarget.Note(___pawn);}
 }
 [HarmonyPatch(typeof(MentalState),nameof(MentalState.ForceHostileTo),new[]{typeof(Thing)})]
 public static class FleeingBeastHostileThingPatch {
  static void Postfix(MentalState __instance,Thing __0,ref bool __result){if(!__result && FleeingBeastTarget.PlayerSide(__0) && FleeingBeastTarget.Marked(__instance.pawn))__result=true;}
 }
 [HarmonyPatch(typeof(MentalState),nameof(MentalState.ForceHostileTo),new[]{typeof(Faction)})]
 public static class FleeingBeastHostileFactionPatch {
  static void Postfix(MentalState __instance,Faction __0,ref bool __result){if(!__result && __0!=null && __0.IsPlayer && FleeingBeastTarget.Marked(__instance.pawn))__result=true;}
 }
 // 原版：逃窜者不算潜在威胁（近战待命、部分炮塔逻辑因此不打）。被标记的仍算——前提是它站着、醒着、没被别的原因判为无威胁。
 [HarmonyPatch(typeof(GenHostility),nameof(GenHostility.IsPotentialThreat))]
 public static class FleeingBeastThreatPatch {
  static void Postfix(IAttackTarget __0,ref bool __result){
   if(__result || !(__0?.Thing is Pawn p) || !FleeingBeastTarget.Marked(p))return;
   __result=p.Spawned && !p.Downed && RestUtility.Awake(p) && !p.ThreatDisabled(null);
  }
 }
}
