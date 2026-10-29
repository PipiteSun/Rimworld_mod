using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.24.0 象人：象人体型、象鼻（第三只手）、沉稳（恐惧免疫）、厚皮。
 public static class Loxodon {
  public const float TrunkFloor=.5f;
  public static bool HasTrunk(Pawn p)=>Races124.Has(p,"LP_LoxodonTrunk");
  // 恐惧免疫：象人的沉稳、象牙号角的鼓舞、（1.30.0）霜巨人冬之号角。
  public static bool FearImmune(Pawn p)=>Races124.Has(p,"LP_LoxodonCalm") || Items.Has(p,"LP_HornCourage") || Items.Has(p,"LP_WinterHorn");
  // 骨质护符、触须披风这类“吓人的东西”带来的心情。
  public static readonly string[] FearThoughts={"LP_SawTendrilCloak"};
 }
 // 象鼻：双手都受伤时象鼻托住，操作能力不低于50%（且不超过意识水平，昏迷时不会凭空能动手）。
 [HarmonyPatch(typeof(PawnCapacityWorker_Manipulation),nameof(PawnCapacityWorker_Manipulation.CalculateCapacityLevel))]
 public static class LoxodonTrunkManipulation {
  static void Postfix(HediffSet diffSet,ref float __result){
   if(__result>=Loxodon.TrunkFloor)return;var p=diffSet?.pawn;if(!Loxodon.HasTrunk(p))return;
   float consciousness=PawnCapacityUtility.CalculateCapacityLevel(diffSet,PawnCapacityDefOf.Consciousness,null,false);
   __result=Mathf.Max(__result,Mathf.Min(Loxodon.TrunkFloor,consciousness));
  }
 }
 // 沉稳：恐慌逃跑、恐惧凝视等恐惧类精神状态不会发生。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class LoxodonFearImmunity {
  static bool Prefix(MentalStateDef __0,Pawn ___pawn,ref bool __result){
   if(!HeroismFearImmunity.Fear(__0) || !Loxodon.FearImmune(___pawn))return true;
   Items.Mote(___pawn,"毫不畏惧",new Color(.8f,.85f,.9f));__result=false;return false;
  }
 }
 [HarmonyPatch(typeof(ThoughtUtility),nameof(ThoughtUtility.ThoughtNullified))]
 public static class Races124ThoughtNullified {
  static void Postfix(Pawn pawn,ThoughtDef def,ref bool __result){
   if(__result || def==null || pawn==null)return;
   int k=kinds.GetOrAdd(def,d=>(Loxodon.FearThoughts.Contains(d.defName)?1:0)|(d.defName.IndexOf("Naked",StringComparison.OrdinalIgnoreCase)>=0?2:0));if(k==0)return;
   if((k&1)!=0 && Loxodon.FearImmune(pawn))__result=true;
   else if((k&2)!=0 && Fiends.Is(pawn))__result=true;
  }
  // 1.58.6：每个小人每次刷新想法时对每个情境想法定义都会问；“是不是恐惧 / 裸体想法”只取决于定义，按定义记住，其他定义直接返回。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<ThoughtDef,int> kinds=new System.Collections.Concurrent.ConcurrentDictionary<ThoughtDef,int>();
 }
}
