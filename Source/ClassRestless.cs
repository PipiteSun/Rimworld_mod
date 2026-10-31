using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.99.0（Peter：巫妖、构装体等不睡觉的种族回不了职业资源）。
 // 斗志 / 气 / 神恩 / 法力 / 诡术师法力原本靠睡觉（每 250 刻 +0.5），契约法力靠短休（含睡觉）；吃饭本来就不回资源。
 // 没有休息需求（needs.rest == null）的人：醒着、未征召、没倒地、10 秒内没攻防时，每 250 刻回上限的 1/240（一天回满，每 6 小时 1/4），
 // 外加按钮“冥想调息”：原地静坐 2 小时，所有职业资源回满（相当于一夜长休）。有休息需求的人照旧，不会多回。
 public static class ClassRestless {
  public const int AttuneTicks=5000,TrickleDiv=240;
  public static bool NoSleep(Pawn p)=>p!=null && (p.needs==null || p.needs.rest==null);
  public static bool Calm(Pawn p)=>p!=null && p.Spawned && !p.Dead && !p.Downed && !p.Drafted && p.Awake() && ClassSubclass.NotInCombat(p);
  // 睡觉会回的资源（怒气不靠睡觉，不算）。
  static bool SleepResource(ClassResource r)=>r==ClassResource.Fury || r==ClassResource.Ki || r==ClassResource.Grace || r==ClassResource.Mana || r==ClassResource.Pact;
  static IEnumerable<LP_ClassDef> Pools(Hediff_Adventurer h){
   var done=new HashSet<LP_ClassDef>();
   if(h?.classes?.levels==null)yield break;
   foreach(var kv in h.classes.levels.ToList())if(kv.Key!=null && kv.Value>0 && SleepResource(kv.Key.resource) && done.Add(ClassSystem.PoolKey(kv.Key)))yield return kv.Key;
  }
  public static bool HasPools(Hediff_Adventurer h)=>Pools(h).Any() || ClassSubclass.ManaMax(h)>0;
  // 每 250 刻（ClassSystem.Tick250 调用）。
  public static void Tick250(Hediff_Adventurer h){
   var p=h?.pawn;if(!NoSleep(p) || !Calm(p))return;
   foreach(var c in Pools(h).ToList()){int max=ClassSystem.MaxResource(h,c);if(max>0)ClassSystem.AddResource(h,c,(float)max/TrickleDiv);}
   int tm=ClassSubclass.ManaMax(h);if(tm>0)ClassSubclass.AddMana(h,(float)tm/TrickleDiv);
  }
  // 冥想调息结束：全部回满。
  public static void Refill(Hediff_Adventurer h){
   if(h==null)return;
   foreach(var c in Pools(h).ToList())ClassSystem.AddResource(h,c,ClassSystem.MaxResource(h,c));
   int tm=ClassSubclass.ManaMax(h);if(tm>0)ClassSubclass.AddMana(h,tm);
  }
  static readonly DefRef<JobDef> attune=new DefRef<JobDef>("LP_Attune");
  public static JobDef AttuneJob=>attune.Def;
  public static bool Attuning(Pawn p)=>p?.CurJobDef!=null && p.CurJobDef==AttuneJob;
  public static string CannotAttune(Pawn p){
   if(p.Drafted)return "征召中不能冥想调息";if(p.Downed)return "倒地了";if(!ClassSubclass.NotInCombat(p))return "正在战斗";if(Attuning(p))return "正在冥想调息";
   return null;
  }
 }

 public class JobDriver_Attune : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  protected override IEnumerable<Toil> MakeNewToils(){
   var sit=ToilMaker.MakeToil("LP_Attune");
   sit.defaultCompleteMode=ToilCompleteMode.Delay;sit.defaultDuration=ClassRestless.AttuneTicks;
   sit.WithProgressBarToilDelay(TargetIndex.A);
   yield return sit;
   yield return Toils_General.Do(()=>{ClassRestless.Refill(Adventurer.Get(pawn));if(pawn.Faction!=null && pawn.Faction.IsPlayer)Items.Mote(pawn,"冥想调息：职业资源回满",new Color(.7f,.8f,1f));});
  }
 }

 // 按钮：只给没有休息需求、有会靠睡觉恢复的职业资源的殖民者。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class ClassRestlessGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   foreach(var g in __result)yield return g;
   var p=__instance;if(p.Faction!=Faction.OfPlayer || !Adventurer.Enabled || p.Dead || !p.Spawned || !ClassRestless.NoSleep(p))yield break;
   var h=Adventurer.Get(p);if(h==null || !ClassRestless.HasPools(h) || ClassRestless.AttuneJob==null)yield break;
   var cmd=new Command_Action{
    defaultLabel="冥想调息",
    defaultDesc="不需要睡觉的身体用静坐代替长休：原地静坐 "+(ClassRestless.AttuneTicks/2500)+" 小时，结束时所有职业资源（斗志、气、神恩、法力、契约法力）回满。中途被打断就没有效果。\n平时不在战斗中、未征召时，每 6 小时也会自动回上限的 1/4。",
    icon=ContentFinder<Texture2D>.Get("LP/Classes/Monk/DiamondSoul",false)??BaseContent.BadTex,
    action=()=>p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(ClassRestless.AttuneJob,p.Position),JobTag.Misc)
   };
   string why=ClassRestless.CannotAttune(p);if(why!=null)cmd.Disable(why);
   yield return cmd;
  }
 }
}
