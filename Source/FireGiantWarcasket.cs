using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 public static class FireGiantWarcasket {
  public const string Reason="火巨人体型与熔火血脉无法适配战棺，请使用火巨人专属装备。";
  public static Type Foundry=>AccessTools.TypeByName("VFEPirates.Building_WarcasketFoundry");
  public static Type Driver=>AccessTools.TypeByName("VFEPirates.JobDriver_EntombIn");
  public static Type Project=>AccessTools.TypeByName("VFEPirates.WarcasketProject");
 }
 [HarmonyPatch]
 public static class FireGiantFoundryReason {
  static bool Prepare()=>FireGiantWarcasket.Foundry!=null;
  static MethodBase TargetMethod()=>AccessTools.Method(FireGiantWarcasket.Foundry,"CannotUseNowReason",new[]{typeof(Pawn)});
  static bool Prefix(Pawn __0,ref string __result){if(!FireGiant.Active(__0))return true;__result=FireGiantWarcasket.Reason;return false;}
 }
 [HarmonyPatch]
 public static class FireGiantEntombReservation {
  static bool Prepare()=>FireGiantWarcasket.Driver!=null;
  static MethodBase TargetMethod()=>AccessTools.Method(FireGiantWarcasket.Driver,"TryMakePreToilReservations",new[]{typeof(bool)});
  static bool Prefix(JobDriver __instance,ref bool __result){if(!FireGiant.Active(__instance.pawn))return true;__result=false;return false;}
 }
 [HarmonyPatch]
 public static class FireGiantEntombToils {
  static bool Prepare()=>FireGiantWarcasket.Driver!=null;
  static MethodBase TargetMethod()=>AccessTools.Method(FireGiantWarcasket.Driver,"MakeNewToils");
  static void Postfix(JobDriver __instance){__instance.FailOn(()=>FireGiant.Active(__instance.pawn));}
 }
 [HarmonyPatch]
 public static class FireGiantFoundryOccupant {
  static bool Prepare()=>FireGiantWarcasket.Foundry!=null;
  static MethodBase TargetMethod()=>AccessTools.Method(FireGiantWarcasket.Foundry,"RegisterOccupant",new[]{typeof(Pawn)});
  static bool Prefix(Pawn __0)=>!FireGiant.Active(__0);
 }
 [HarmonyPatch]
 public static class FireGiantWarcasketInstall {
  static bool Prepare()=>FireGiantWarcasket.Project!=null;
  static MethodBase TargetMethod()=>AccessTools.Method(FireGiantWarcasket.Project,"ApplyOn",new[]{typeof(Pawn)});
  static bool Prefix(Pawn __0){if(!FireGiant.Active(__0))return true;Messages.Message(FireGiantWarcasket.Reason,__0,MessageTypeDefOf.RejectInput,false);return false;}
 }
}
