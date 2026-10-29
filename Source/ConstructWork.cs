using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 public partial class Hediff_Construct {
  // Death removes Pawn.workSettings. Keep the player's choices on the surviving
  // hediff so a corpse saved before resurrection retains them as well.
  public Dictionary<WorkTypeDef,int> savedWorkPriorities;
  public void RememberWork(){
   if(pawn.workSettings==null || !pawn.workSettings.EverWork)return;
   savedWorkPriorities=DefDatabase<WorkTypeDef>.AllDefsListForReading.ToDictionary(w=>w,w=>pawn.workSettings.GetPriority(w));
  }
  public void RestoreWork(){
   if(pawn.Dead || pawn.workSettings==null || savedWorkPriorities==null)return;
   foreach(var pair in savedWorkPriorities)
    if(pair.Key!=null && !pawn.WorkTypeIsDisabled(pair.Key))pawn.workSettings.SetPriority(pair.Key,pair.Value);
   savedWorkPriorities=null;
  }
 }
 [HarmonyPatch(typeof(PawnComponentsUtility),nameof(PawnComponentsUtility.RemoveComponentsOnKilled))]
 public static class ConstructRememberWork {
  static void Prefix(Pawn pawn)=>Necro.Construct(pawn)?.RememberWork();
 }
 [HarmonyPatch(typeof(Pawn_WorkSettings),nameof(Pawn_WorkSettings.EnableAndInitialize))]
 public static class ConstructRestoreWork {
  // Runs after native resurrection initializes work, not in Notify_Resurrected,
  // which fires earlier and would have its restored priorities overwritten.
  static void Postfix(Pawn ___pawn)=>Necro.Construct(___pawn)?.RestoreWork();
 }
}
