using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // Optional integration: use the installed provider's natural-part rules and
 // shared HA/MB cap. Bind once; never reflect or allocate in health queries.
 public static class TemperingCompatibility {
  public static readonly MethodInfo HumanMaximum=Find("HumanAptitudes","MaxHealth");
  public static readonly MethodInfo MythicMaximum=Find("MythicBloodlines","MaxHealth");
  static readonly Func<Pawn,BodyPartRecord,float> bonus=BindBonus();
  static MethodInfo Find(string ns,string method){var type=GenTypes.GetTypeInAnyAssembly(ns+".TemperingHealth");return type==null?null:AccessTools.Method(type,method);}
  static Func<Pawn,BodyPartRecord,float> BindBonus(){var method=Find("HumanAptitudes","Bonus")??Find("MythicBloodlines","Bonus");return method==null?null:(Func<Pawn,BodyPartRecord,float>)method.CreateDelegate(typeof(Func<Pawn,BodyPartRecord,float>));}
  public static float Apply(float original,BodyPartRecord part,Pawn pawn){
   if(bonus==null)return original;
   var list=pawn?.health?.hediffSet?.hediffs;if(list==null)return original;
   for(int i=0;i<list.Count;i++)if(list[i].Part==part && (list[i].def.defName=="HA_Tempering" || list[i].def.defName=="MB_Tempering")){
    float value=bonus(pawn,part);return value>0f?Mathf.RoundToInt(original*(1f+value)):original;
   }
   return original;
  }
  public static bool IsMaximum(MethodInfo method)=>method!=null && (method==HumanMaximum || method==MythicMaximum);
 }
 // Their public/UI helper must agree with the native health and damage callers.
 // Maximum itself calls Bonus (never MaxHealth), so this cannot recurse.
 [HarmonyPatch]
 public static class TemperingMaximumCompatibility {
  static bool Prepare()=>TemperingCompatibility.HumanMaximum!=null || TemperingCompatibility.MythicMaximum!=null;
  static IEnumerable<MethodBase> TargetMethods(){if(TemperingCompatibility.HumanMaximum!=null)yield return TemperingCompatibility.HumanMaximum;if(TemperingCompatibility.MythicMaximum!=null)yield return TemperingCompatibility.MythicMaximum;}
  static bool Prefix(BodyPartRecord __0,Pawn __1,ref float __result){if(Necro.Lich(__1)==null)return true;__result=PartHealth.Maximum(__0,__1);return false;}
 }
}
