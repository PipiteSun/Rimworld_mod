using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;
namespace LichPhylactery {
 // 2026-11-02：Test.ps1 -Full -Only PatchOverlap：列出本模组和其他模组同时打了 Harmony 补丁的方法（日志前缀 [LICH-OVERLAP]），
 // 标出容易出问题的组合：别人的可跳过前缀（返回 bool）会让本模组的转译器 / 依赖原方法的逻辑失效，或两边都有转译器。只记录，不判失败。
 public partial class LichChecks {
  static bool LpOwner(string o)=>o.StartsWith("local.lich.phylactery");
  static string PatchName(Patch p)=>p.owner+":"+p.PatchMethod.DeclaringType?.Name+"."+p.PatchMethod.Name+"(prio "+p.priority+")";
  void CheckPatchOverlap(){
   int shared=0,risky=0;
   foreach(var m in Harmony.GetAllPatchedMethods().ToList()){
    var info=Harmony.GetPatchInfo(m);if(info==null)continue;
    var all=info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers).ToList();
    if(!all.Any(p=>LpOwner(p.owner)) || all.All(p=>LpOwner(p.owner)))continue;
    shared++;
    var name=m.DeclaringType?.FullName+"."+m.Name;
    var notes=new List<string>();
    var foreignSkip=info.Prefixes.Where(p=>!LpOwner(p.owner) && p.PatchMethod.ReturnType==typeof(bool)).ToList();
    var lpSkip=info.Prefixes.Where(p=>LpOwner(p.owner) && p.PatchMethod.ReturnType==typeof(bool)).ToList();
    var lpTrans=info.Transpilers.Where(p=>LpOwner(p.owner)).ToList();
    var foreignTrans=info.Transpilers.Where(p=>!LpOwner(p.owner)).ToList();
    if(foreignSkip.Any() && (lpTrans.Any() || info.Prefixes.Any(p=>LpOwner(p.owner))))notes.Add("FOREIGN-SKIP-PREFIX "+string.Join(",",foreignSkip.Select(PatchName)));
    if(lpSkip.Any() && (foreignTrans.Any() || info.Prefixes.Any(p=>!LpOwner(p.owner))))notes.Add("LP-SKIP-PREFIX "+string.Join(",",lpSkip.Select(PatchName)));
    if(lpTrans.Any() && foreignTrans.Any())notes.Add("BOTH-TRANSPILE");
    if(notes.Any())risky++;
    Log.Message("[LICH-OVERLAP] "+(notes.Any()?"RISK ":"ok   ")+name+" | "+string.Join(" ; ",all.Select(PatchName))+(notes.Any()?" || "+string.Join(" | ",notes):""));
   }
   Log.Message("[LICH-OVERLAP] shared="+shared+" risky="+risky);
   Check(true,"patch overlap scan logged ("+shared+" shared, "+risky+" flagged)");
  }
 }
}
