using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;
namespace LichPhylactery {
 // 2026-11-04 热修：Ducks' Insane Skills（ducks.insaneskills，只支持 1.2 / 1.3）的 SkillRecord.LearnRateFactor 前缀调用了 1.7 已经没有的
 // StatExtension.GetStatValue(Thing,StatDef,bool)，JIT 时抛 MissingMethodException：所有技能学习都崩，小人整个 tick 中断
 // （社交斗殴永远打不完、工作半途报错）。启动时把这个模组里编译不过的前缀 / 后缀 / 终结器摘掉（能编译的照常保留），日志里写一条警告。
 [StaticConstructorOnStartup]
 public static class DucksSkillsCompat {
  public const string Owner="net.ducks.rimworld.mod.ducksskills";
  public static readonly List<string> Removed=new List<string>();
  static DucksSkillsCompat(){
   try{
    Sweep(Owner,Removed);
    if(Removed.Count>0)Log.Warning("[LichPhylactery] Ducks' Insane Skills 是 1.3 的旧模组，有 "+Removed.Count+" 个补丁在当前版本编译不过（每次调用都会抛异常，技能学习、交易会崩），已摘掉："+string.Join("、",Removed)+"。建议在模组列表里停用它。");
   }catch(Exception e){Log.Warning("[LichPhylactery] Ducks' Insane Skills 兼容检查失败："+e);}
  }
  // 摘掉 owner 的补丁里编译不过的那些；返回摘掉的个数，名字记进 names。
  public static int Sweep(string owner,List<string> names){
   var h=new Harmony("local.lich.phylactery.duckscompat");int n=0;
   foreach(var m in Harmony.GetAllPatchedMethods().ToList()){
    var info=Harmony.GetPatchInfo(m);if(info==null)continue;
    foreach(var p in info.Prefixes.Concat(info.Postfixes).Concat(info.Finalizers).Where(x=>x.owner==owner).ToList()){
     if(Compiles(p.PatchMethod))continue;
     h.Unpatch(m,p.PatchMethod);n++;names?.Add(m.DeclaringType?.Name+"."+m.Name);
    }
   }
   return n;
  }
  public static bool Compiles(MethodInfo mi){try{RuntimeHelpers.PrepareMethod(mi.MethodHandle);return true;}catch{return false;}}
 }
}
