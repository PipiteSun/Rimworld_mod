using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.144.0 热修（2026-11-10，Zoe 被敌方恶魔的野性怒吼吓跑后变成别的派系）：
 // 原版「仓皇逃窜」本来只给敌人用，会一直跑到出地图。玩家的人从家园地图走出边缘组不成远行队，原版 Pawn.Notify_PassedToWorld 会把他随机分给一个非玩家派系。
 // 本模组二十多种恐惧效果（野性怒吼、恐惧术、骇人威仪、君临……）由敌方 NPC 放到你的人身上时都会这样。
 // ①玩家阵营的人进入仓皇逃窜：最多 10 秒自动回过神（原本更短的保留）。
 // ②玩家阵营的人在仓皇逃窜中走到地图边缘、又组不成远行队：不出地图，当场回过神。能组远行队的地图（临时地图）照原版走。
 public static class ColonistPanicGuard {
  public const int MaxTicks=600;
  public static bool Guarded(Pawn p)=>p?.Faction!=null && p.Faction.IsPlayer;
 }
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class ColonistPanicCapPatch {
  static void Postfix(MentalStateDef __0,Pawn ___pawn,bool __result){
   if(!__result || __0!=MentalStateDefOf.PanicFlee || !ColonistPanicGuard.Guarded(___pawn))return;
   var s=___pawn.MentalState;if(s==null || s.def!=MentalStateDefOf.PanicFlee)return;
   if(s.forceRecoverAfterTicks<0 || s.forceRecoverAfterTicks>ColonistPanicGuard.MaxTicks)s.forceRecoverAfterTicks=ColonistPanicGuard.MaxTicks;
  }
 }
 [HarmonyPatch(typeof(JobDriver_Goto),"TryExitMap")]
 public static class ColonistPanicNoExitPatch {
  static bool Prefix(JobDriver_Goto __instance){
   var p=__instance.pawn;
   if(p==null || !p.Spawned || p.MentalStateDef!=MentalStateDefOf.PanicFlee || !ColonistPanicGuard.Guarded(p))return true;
   if(CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(p))return true;
   p.MentalState?.RecoverFromState();
   if(p.CurJob!=null && p.CurJob==__instance.job)__instance.EndJobWith(JobCondition.Incompletable);
   MoteMaker.ThrowText(p.DrawPos,p.Map,"回过神来",3f);
   return false;
  }
 }
}
