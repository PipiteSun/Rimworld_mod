using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.129.0 热修（射程外施放）：玩家给本模组的能力选了射程外 / 看不到的目标时不再拒绝，
 // 施法者先走到射程内、看得见目标的格子再照常施放（同 VPE 心灵法术）。资源、冷却、次数都在真正施放时才扣（Ability_Class.Activate / 原版 Activate）。
 // 只管玩家阵营的施法者、只管通过选目标界面（Targeter）下的命令；AI、自动施放、NPC 职业仍按原来的射程。
 // 不参与的（保持原来的射程限制）：目标就是落点的位移能力（传送、迷踪步、冲锋、跃击、换位…，见 MoveEffects / MoveDefs）、
 // 跳跃类 / 接触类施放（原版自己会走过去）、自带施放工作的能力、世界地图目标、只能对自己放的能力、需要再点一次落点的能力。
 // 要单独改某个能力：在 AbilityDef 的 modExtensions 里加 <li Class="LichPhylactery.CastRangeExt"><approach>false</approach></li>（或 true 强制参与）。
 public class CastRangeExt : DefModExtension { public bool approach=true; }

 [DefOf]
 public static class CastRangeDefOf {
  public static JobDef LP_CastAbilityApproach;
  static CastRangeDefOf(){DefOfHelper.EnsureInitializedInCtor(typeof(CastRangeDefOf));}
 }

 // 名单在定义加载完后建（CastApproach 本身在 Harmony 打补丁时就会被碰到，那时还没有定义）。
 [StaticConstructorOnStartup]
 public static class CastApproachInit {
  static CastApproachInit(){try{CastApproach.Build();}catch(Exception e){Log.Error("[DND边缘大陆] 射程外施放列表出错："+e);}}
 }
 public static class CastApproach {
  public const string Hint="走近后施放";
  // 能力效果名（各种 Comp 的 effect 字段）：目标就是施法者要去的位置 / 施法者会冲过去。
  public static readonly HashSet<string> MoveEffects=new HashSet<string>{
   "Teleport","Charge","RageCharge","Intercept","S2Blink","S3ShadowStep","ArCloudStep","WS_DimensionDoor","DragonWings",
   "ShadowStep","leap","glide","step","windleap","soar","astral","feystep","helmcharge","charge","seep"};
  // 同上，按 defName 再兜一层（效果名以后改了也不会漏）。
  public static readonly HashSet<string> MoveDefs=new HashSet<string>{
   "LP_MistyStep","LP_Teleport","LP_WL_MistyEscape","LP_CloudMistyStep","LP_EK_ArcaneCharge","LP_PW_TeleJump","LP_SH_ShadowStep","LP_AC_CloudStep",
   "LP_WS_DimensionDoor","LP_DA_ShadowStep","LP_Charge","LP_RecklessCharge","LP_BullRush","LP_SatyrCharge","LP_HelmCharge","LP_BarlguraLeap",
   "LP_CambionGlide","LP_ElementWindLeap","LP_Soar","LP_AstralStep","LP_MI_AstralStep","LP_HeroFeyStep","LP_DragonWings","LP_RadiantSoulLeap",
   "LP_Intercept","LP_OozeSeep"};
  // 启动时建好，之后只读（渲染 / 选目标线程里不写任何共享状态）。
  static readonly HashSet<AbilityDef> allowed=new HashSet<AbilityDef>();
  static readonly Dictionary<AbilityDef,string> excluded=new Dictionary<AbilityDef,string>();
  public static int AllowedCount=>allowed.Count;
  public static bool Allowed(AbilityDef d)=>d!=null && allowed.Contains(d);
  public static string ExcludedWhy(AbilityDef d)=>d!=null && excluded.TryGetValue(d,out var s)?s:null;
  public static int TargeterPatched; // 1 = 选目标界面的射程过滤换成了我们的（检查用）
  [ThreadStatic] static int ignoreDistance;
  // 下命令时检查目标：各能力自己写的距离 / 视线检查先跳过（走到位置后施放前会照常再查）。
  public static bool IgnoreDistance=>ignoreDistance>0;
  public static bool TooFar(float dist,float range)=>ignoreDistance<=0 && dist>range;

  static readonly Action<Ability,LocalTargetInfo,Action> confirm=AccessTools.MethodDelegate<Action<Ability,LocalTargetInfo,Action>>(AccessTools.Method(typeof(Ability),"ShowCastingConfirmationIfNeeded",new[]{typeof(LocalTargetInfo),typeof(Action)}));
  static readonly MethodInfo extraLabel=AccessTools.Method(typeof(Verb_CastAbility),"DrawAttachmentExtraLabel");

  public static void Build(){
   allowed.Clear();excluded.Clear();
   var own=LoadedModManager.GetMod<LichMod>()?.Content;
   foreach(var d in DefDatabase<AbilityDef>.AllDefsListForReading){
    if(own!=null && d.modContentPack!=own)continue;
    var why=Why(d);if(why==null)allowed.Add(d);else excluded[d]=why;
   }
  }
  // null = 参与射程外施放；否则是不参与的原因。
  static string Why(AbilityDef d){
   var vp=d.verbProperties;
   var ext=d.GetModExtension<CastRangeExt>();
   if(ext!=null && !ext.approach)return "定义里关掉";
   if(d.targetWorldCell)return "世界地图目标";
   if(vp==null || vp.verbClass==null || !typeof(Verb_CastAbility).IsAssignableFrom(vp.verbClass))return "不是能力施放";
   if(typeof(Verb_CastAbilityJump).IsAssignableFrom(vp.verbClass))return "跳跃";
   if(typeof(Verb_CastAbilityTouch).IsAssignableFrom(vp.verbClass))return "接触（原版自己走过去）";
   if(vp.range<=0f)return "没有射程（对自己）";
   var tp=vp.targetParams;
   if(tp!=null && !tp.canTargetPawns && !tp.canTargetLocations && !tp.canTargetBuildings && !tp.canTargetItems && !tp.canTargetCorpses && !tp.canTargetPlants)return "只能对自己";
   if(d.jobDef!=null && d.jobDef!=JobDefOf.CastAbilityOnThing)return "自带施放工作";
   if(ext!=null && ext.approach)return null;
   if(MoveDefs.Contains(d.defName))return "位移（目标就是落点）";
   if(d.comps!=null)foreach(var c in d.comps){
    if(c is CompProperties_AbilityTeleport)return "位移（目标就是落点）";
    if(c is CompProperties_EffectWithDest w && w.destination==AbilityEffectDestination.Selected)return "需要再选落点";
    var f=AccessTools.Field(c.GetType(),"effect");
    if(f!=null && f.FieldType==typeof(string) && f.GetValue(c) is string e && MoveEffects.Contains(e))return "位移（目标就是落点）";
   }
   return null;
  }

  public static bool Eligible(Verb_CastAbility v){
   var a=v?.ability;var p=a?.pawn;
   return p!=null && allowed.Contains(a.def) && p.Spawned && p.Faction!=null && p.Faction.IsPlayer;
  }
  static bool OnMap(Verb_CastAbility v,LocalTargetInfo t){
   var map=v.ability.pawn.Map;if(!t.IsValid || map==null)return false;
   return t.HasThing?t.Thing.Spawned && t.Thing.Map==map:t.Cell.InBounds(map);
  }
  // 正在用这个能力（或多选时同一个能力）选目标。
  static bool FromTargeter(Verb_CastAbility v){
   var tg=Find.Targeter;
   return tg!=null && tg.IsTargeting && tg.targetingSource is Verb_CastAbility s && s.ability?.def==v.ability.def;
  }
  // 选目标界面里、目标在射程外 / 没视线、但可以走过去放。
  // 1.131.0 性能热修：选目标时 ValidateTarget / OnGUI / DrawHighlight 三个钩子每帧对同一个目标各问一次（CanHitTarget 要算视线，ValidateFar 要算可达），
  // 同一帧、同一个动词、同一个目标的答案相同，记住上一次的（只记 showMessages=false 的那条）。
  // 键：帧、动词对象、目标、当前选目标来源（开始 / 停止选目标会变）、施法者格子与目标格子（同一帧里被挪动也会变）。
  static Verb_CastAbility memoVerb;static LocalTargetInfo memoTarget;static object memoSource;static IntVec3 memoFrom,memoCell;static int memoFrame=-1;static bool memoApproachable;static int memoValid; // memoValid：0 没算过，1 合法，2 不合法
  static bool MemoHit(Verb_CastAbility v,LocalTargetInfo t){
   if(memoFrame!=RealTime.frameCount || !ReferenceEquals(memoVerb,v) || memoTarget!=t)return false;
   var tg=Find.Targeter;object src=tg!=null && tg.IsTargeting?tg.targetingSource:null;
   return ReferenceEquals(memoSource,src) && memoFrom==(v.ability?.pawn?.Position??IntVec3.Invalid) && memoCell==t.Cell;
  }
  public static bool Approachable(Verb_CastAbility v,LocalTargetInfo t){
   if(MemoHit(v,t))return memoApproachable;
   bool r=Eligible(v) && FromTargeter(v) && OnMap(v,t) && !v.CanHitTarget(t);
   var tg=Find.Targeter;memoSource=tg!=null && tg.IsTargeting?tg.targetingSource:null;memoFrom=v.ability?.pawn?.Position??IntVec3.Invalid;memoCell=t.Cell;
   memoFrame=RealTime.frameCount;memoVerb=v;memoTarget=t;memoApproachable=r;memoValid=0;return r;
  }
  // 同上：Approachable 为 true 时“射程外但合法”的结果也按帧记住（OnGUI 前缀和 DrawHighlight 后缀都要再问 ValidateTarget）。
  public static bool ValidFarMemo(Verb_CastAbility v,LocalTargetInfo t){
   if(MemoHit(v,t) && memoValid!=0)return memoValid==1;
   bool ok=ValidateFar(v,t,false);
   if(MemoHit(v,t))memoValid=ok?1:2;
   return ok;
  }

  // 射程外的目标：能走到、能力本身接受这个目标（距离检查除外）就算合法。
  public static bool ValidateFar(Verb_CastAbility v,LocalTargetInfo t,bool show){
   var a=v.ability;var p=a.pawn;
   if(!p.CanReach(t,PathEndMode.Touch,Danger.Deadly)){
    if(show)Messages.Message("CannotUseAbility".Translate(a.def.label)+": "+"AbilityCannotReachTarget".Translate(),new LookTargets(p,t.ToTargetInfo(p.Map)),MessageTypeDefOf.RejectInput,false);
    return false;
   }
   return ValidIgnoringDistance(a,t,show);
  }
  public static bool ValidIgnoringDistance(Ability a,LocalTargetInfo t,bool show){
   ignoreDistance++;
   try{
    if(a.verb is Verb_CastAbility v && !v.IsApplicableTo(t,show))return false;
    foreach(var c in a.EffectComps)if(!c.Valid(t,show))return false;
    return true;
   }finally{ignoreDistance--;}
  }
  // 站在这里能不能直接放：射程 + 视线 + 能力自己的检查（含距离）。
  public static bool CanCastNow(Ability a,LocalTargetInfo t){
   if(!(a.verb is Verb_CastAbility v) || !v.CanHitTarget(t))return false;
   foreach(var c in a.EffectComps)if(!c.Valid(t,false))return false;
   return true;
  }

  // 下命令：走近后施放。
  public static bool Order(Ability a,LocalTargetInfo t){
   var p=a.pawn;
   bool canApply;ignoreDistance++;try{canApply=a.CanApplyOn(t);}finally{ignoreDistance--;}
   if(!a.CanQueueCast || !canApply)return false;
   var cell=FindCastCell(p,a.verb,t,a.verb.EffectiveRange);
   if(!cell.IsValid){
    Messages.Message("CannotUseAbility".Translate(a.def.label)+": 找不到既在射程内、又看得见目标、还走得到的位置。",new LookTargets(p,t.ToTargetInfo(p.Map)),MessageTypeDefOf.RejectInput,false);
    return false;
   }
   confirm(a,t,()=>{
    var job=JobMaker.MakeJob(CastRangeDefOf.LP_CastAbilityApproach,t);
    job.ability=a;job.verbToUse=a.verb; // verbToUse 让原版“排队次数”把这条命令算进去
    p.jobs.TryTakeOrderedJob(job,null,false); // 按住 Shift 时原版会排到队尾
   });
   return true;
  }

  // 离施法者最近、在射程（range）内、看得见目标、走得到的可站立格；找不到返回 Invalid。
  public static IntVec3 FindCastCell(Pawn p,Verb verb,LocalTargetInfo t,float range){
   var map=p?.Map;if(map==null || !t.IsValid)return IntVec3.Invalid;
   var center=t.Cell;float r=Mathf.Clamp(range,1f,GenRadial.MaxRadialPatternRadius-1f);
   int n=GenRadial.NumCellsInRadius(r);var cells=new List<IntVec3>(n);var me=p.Position;
   for(int i=0;i<n;i++){var c=center+GenRadial.RadialPattern[i];if(c.InBounds(map) && c.Standable(map))cells.Add(c);}
   cells.Sort((x,y)=>x.DistanceToSquared(me).CompareTo(y.DistanceToSquared(me)));
   int tries=0;
   foreach(var c in cells){
    if(!verb.CanHitTargetFrom(c,t))continue;
    if(c!=me){if(++tries>300)break;if(!p.CanReach(c,PathEndMode.OnCell,Danger.Deadly))continue;}
    return c;
   }
   return IntVec3.Invalid;
  }

  // 选目标界面的“打得到”过滤（Targeter.CurrentTargetUnderMouse 里换上这两个）。
  public static bool SourceCanHit(ITargetingSource src,LocalTargetInfo t)=>src.CanHitTarget(t) || (src is Verb_CastAbility v && Eligible(v) && OnMap(v,t));
  public static bool VerbCanHit(Verb verb,LocalTargetInfo t)=>verb.CanHitTarget(t) || (verb is Verb_CastAbility v && Eligible(v) && OnMap(v,t));

  public static void DrawCursor(Verb_CastAbility v,LocalTargetInfo t){
   var icon=v.UIIcon!=null && v.UIIcon!=BaseContent.BadTex?v.UIIcon:TexCommand.Attack;
   GenUI.DrawMouseAttachment(icon,Hint);
   try{extraLabel?.Invoke(v,new object[]{t});}catch{}
  }
 }

 [HarmonyPatch(typeof(Verb_CastAbility),nameof(Verb_CastAbility.ValidateTarget))]
 public static class CastRange_Validate {
  static bool Prefix(Verb_CastAbility __instance,LocalTargetInfo target,bool showMessages,ref bool __result){
   try{
    if(!CastApproach.Approachable(__instance,target))return true;
    __result=showMessages?CastApproach.ValidateFar(__instance,target,true):CastApproach.ValidFarMemo(__instance,target);return false;
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 射程外施放检查出错："+e,0x4c5201);return true;}
  }
 }
 [HarmonyPatch(typeof(Verb_CastAbility),nameof(Verb_CastAbility.OrderForceTarget))]
 public static class CastRange_Order {
  static bool Prefix(Verb_CastAbility __instance,LocalTargetInfo target){
   try{
    if(!CastApproach.Approachable(__instance,target) || !CastApproach.ValidateFar(__instance,target,false))return true;
    CastApproach.Order(__instance.ability,target);return false;
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 射程外施放命令出错："+e,0x4c5202);return true;}
  }
 }
 [HarmonyPatch(typeof(Verb_CastAbility),nameof(Verb_CastAbility.OnGUI))]
 public static class CastRange_Cursor {
  static bool Prefix(Verb_CastAbility __instance,LocalTargetInfo target){
   try{
    if(!CastApproach.Approachable(__instance,target) || !CastApproach.ValidFarMemo(__instance,target))return true;
    CastApproach.DrawCursor(__instance,target);return false;
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 射程外施放光标出错："+e,0x4c5203);return true;}
  }
 }
 [HarmonyPatch(typeof(Verb_CastAbility),nameof(Verb_CastAbility.DrawHighlight))]
 public static class CastRange_Highlight {
  static void Postfix(Verb_CastAbility __instance,LocalTargetInfo target){
   try{
    if(!CastApproach.Approachable(__instance,target) || !CastApproach.ValidFarMemo(__instance,target))return;
    GenDraw.DrawTargetHighlightWithLayer(target.CenterVector3,AltitudeLayer.MetaOverlays);
    var d=__instance.ability.def;if(d.HasAreaOfEffect)GenDraw.DrawRadiusRing(target.Cell,d.EffectRadius,Verb_CastAbility.RadiusHighlightColor);
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 射程外施放高亮出错："+e,0x4c5204);}
  }
 }
 [HarmonyPatch(typeof(Targeter),"CurrentTargetUnderMouse")]
 public static class CastRange_Targeter {
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code){
   var src=AccessTools.Method(typeof(ITargetingSource),nameof(ITargetingSource.CanHitTarget));
   var verb=AccessTools.Method(typeof(Verb),nameof(Verb.CanHitTarget));
   int n=0;
   foreach(var c in code){
    if(c.Calls(src)){c.opcode=OpCodes.Call;c.operand=AccessTools.Method(typeof(CastApproach),nameof(CastApproach.SourceCanHit));n++;}
    else if(c.Calls(verb)){c.opcode=OpCodes.Call;c.operand=AccessTools.Method(typeof(CastApproach),nameof(CastApproach.VerbCanHit));n++;}
    yield return c;
   }
   CastApproach.TargeterPatched=n>=1?1:0;
   if(n==0)Log.Warning("[DND边缘大陆] 射程外施放：选目标界面补丁没找到射程过滤，游戏版本可能改了。");
  }
 }

 // 走近后施放：走到射程内、看得见目标的格子（目标移动就重新找路），到位后换成原版施放工作（同一刻开始读条）。
 public class JobDriver_CastApproach : JobDriver {
  float rangeFactor=1f;int shrinks;int lastPathTick=-99999;IntVec3 lastTargetCell=IntVec3.Invalid;
  Ability Ab=>job.ability;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  public override string GetReport()=>Ab==null?base.GetReport():"走近以施放"+Ab.def.label+"。";
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref rangeFactor,"lpCastRange",1f);Scribe_Values.Look(ref shrinks,"lpCastShrinks",0);}
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOn(()=>Ab==null || Ab.pawn!=pawn || !(Ab.verb is Verb_CastAbility));
   if(job.targetA.HasThing)this.FailOnDespawnedOrNull(TargetIndex.A);
   this.FailOn(()=>!Ab.CanCast.Accepted);
   var go=ToilMaker.MakeToil("LP_CastApproach");
   go.initAction=()=>Step(true);
   go.tickIntervalAction=delta=>Step(false);
   go.defaultCompleteMode=ToilCompleteMode.Never;
   yield return go;
  }
  void Step(bool first){
   var t=job.targetA;
   if(CastApproach.CanCastNow(Ab,t)){Swap();return;}
   if(!CastApproach.ValidIgnoringDistance(Ab,t,false)){EndJobWith(JobCondition.Incompletable);return;} // 目标不再合法（友方了、死了…）
   int now=Find.TickManager.TicksGame;
   bool moved=t.HasThing && t.Thing.Position!=lastTargetCell;
   if(first || !pawn.pather.Moving || (moved && now-lastPathTick>=30))Repath(now);
  }
  void Repath(int now){
   var t=job.targetA;
   bool arrived=!pawn.pather.Moving && lastPathTick>=0 && !(t.HasThing && t.Thing.Position!=lastTargetCell);
   if(arrived){ // 到了但还放不了：能力自己的距离比原版射程短（例如武器射程），再靠近一点
    if(++shrinks>4){EndJobWith(JobCondition.Incompletable);return;}
    rangeFactor*=.6f;
   }
   lastPathTick=now;lastTargetCell=t.Cell;
   float range=Ab.verb.EffectiveRange*rangeFactor;
   var c=range<1.5f?IntVec3.Invalid:CastApproach.FindCastCell(pawn,Ab.verb,t,range);
   if(!c.IsValid){
    if(t.HasThing && pawn.CanReach(t,PathEndMode.Touch,Danger.Deadly)){pawn.pather.StartPath(t,PathEndMode.Touch);return;}
    EndJobWith(JobCondition.Incompletable);return;
   }
   if(c==pawn.Position){if(!arrived && ++shrinks>4){EndJobWith(JobCondition.Incompletable);return;}rangeFactor*=.6f;return;}
   pawn.pather.StartPath(c,PathEndMode.OnCell);
  }
  void Swap(){
   var cast=Ab.GetJob(job.targetA,job.targetB);
   cast.playerForced=job.playerForced;
   pawn.pather.StopDead();
   pawn.jobs.jobQueue.EnqueueFirst(cast);
   EndJobWith(JobCondition.Succeeded);
  }
 }
}
