using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.96.0 温存：优侣种（原版异种扩展·优侣种 VRE Highmate）自带的“放荡交欢”(VRE_InitiateLovin) 对有伴侣的人会拆散关系。
 // 这里给同样有那个能力的人再加一个“温存”：走过去和目标亲热一会儿，结果照原版 JobDriver_Lovin 结束时那一套
 // （滚床单心情、文化事件、下次亲热冷却、生物科技的怀孕几率），不碰任何恋人/伴侣/配偶关系。
 // 没装优侣种时没人有 VRE_InitiateLovin，这个能力也就不会出现。
 public static class TenderLovin {
  public const string VreAbility="VRE_InitiateLovin";
  public const int MinAge=16;
  static AbilityDef def;static JobDef job,partnerJob;
  public static AbilityDef Def=>def??(def=DefDatabase<AbilityDef>.GetNamedSilentFail("LP_TenderLovin"));
  public static JobDef Job=>job??(job=DefDatabase<JobDef>.GetNamed("LP_TenderLovin"));
  public static JobDef PartnerJob=>partnerJob??(partnerJob=DefDatabase<JobDef>.GetNamed("LP_TenderLovinPartner"));
  public static bool HasVre(Pawn p)=>p?.abilities?.abilities!=null && p.abilities.abilities.Any(a=>a.def.defName==VreAbility);
  // 有放荡交欢就补上温存（读旧档、基因加上之后都会走到这里）
  public static void Sync(Pawn p){if(Def!=null && HasVre(p) && p.abilities.GetAbility(Def)==null)p.abilities.GainAbility(Def);}
  // 不能用的原因；null = 可以
  public static string Reason(Pawn caster,Pawn target){
   if(target==null || target==caster || !target.RaceProps.Humanlike || target.Dead)return "只能对其他人形使用。";
   if(caster.ageTracker.AgeBiologicalYears<MinAge)return caster.LabelShortCap+"还太年轻。";
   if(target.ageTracker.AgeBiologicalYears<MinAge)return target.LabelShortCap+"还太年轻。";
   if(target.Downed || !target.Awake() || target.InMentalState)return target.LabelShortCap+"现在没法回应。";
   if(target.HostileTo(caster))return "不能对敌对的人使用。";
   if(target.Faction!=caster.Faction && !target.IsPrisonerOfColony && !target.IsSlaveOfColony)return "只能对同一派系的人、本殖民地的囚犯或奴隶使用。";
   if(target.story?.traits?.HasTrait(TraitDefOf.Asexual)==true)return target.LabelShortCap+"是无性恋。";
   if(caster.relations!=null && caster.relations.FamilyByBlood.Contains(target))return "不能对血亲使用。";
   if(target.relations!=null && target.relations.OpinionOf(caster)<0 && !LovePartnerRelationUtility.LovePartnerRelationExists(caster,target))return target.LabelShortCap+"对"+caster.LabelShortCap+"的好感低于 0。";
   return null;
  }
  // 原版亲热结束时的结果（照 JobDriver_Lovin 的结束动作）；不加也不删任何关系。
  public static void Outcome(Pawn a,Pawn b){
   if(a==null || b==null)return;
   bool enhanced=Enhanced(a) || Enhanced(b);
   Give(a,b,enhanced);Give(b,a,enhanced);
   Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.InitiatedLovin,a.Named(HistoryEventArgsNames.Doer)),true);
   if(!ModsConfig.BiotechActive)return;
   Pawn father=a.gender==Gender.Male?a:(b.gender==Gender.Male?b:null),mother=a.gender==Gender.Female?a:(b.gender==Gender.Female?b:null);
   if(father==null || mother==null || TestNoPregnancy || !Rand.Chance(PregnancyChance*PregnancyUtility.PregnancyChanceForPartners(mother,father)))return;
   var genes=PregnancyUtility.GetInheritedGeneSet(father,mother,out bool success);
   if(success){var preg=(Hediff_Pregnant)HediffMaker.MakeHediff(HediffDefOf.PregnantHuman,mother);preg.SetParents(null,father,genes);mother.health.AddHediff(preg);}
   else if(PawnUtility.ShouldSendNotificationAbout(father) || PawnUtility.ShouldSendNotificationAbout(mother))Messages.Message("MessagePregnancyFailed".Translate(father.Named("FATHER"),mother.Named("MOTHER"))+": "+"CombinedGenesExceedMetabolismLimits".Translate(),new LookTargets(father,mother),MessageTypeDefOf.NegativeEvent);
  }
  public static bool TestNoPregnancy;
  // 原版两者都是 private：反射取，取不到时用原版数值（5%、约 1 天）
  static readonly System.Reflection.FieldInfo chanceField=AccessTools.Field(typeof(JobDriver_Lovin),"PregnancyChance");
  // 1.7 里它是实例方法（只用参数 pawn），借一个空的原版驱动来调；优侣种的需求补丁挂在它上面
  static readonly System.Reflection.MethodInfo nextLovinMethod=AccessTools.Method(typeof(JobDriver_Lovin),"GenerateRandomMinTicksToNextLovin",new[]{typeof(Pawn)});
  static readonly JobDriver_Lovin lovinDriver=new JobDriver_Lovin();
  static readonly System.Func<Pawn,int> nextLovin=nextLovinMethod==null?null:(System.Func<Pawn,int>)(p=>System.Convert.ToInt32(nextLovinMethod.Invoke(nextLovinMethod.IsStatic?null:lovinDriver,new object[]{p})));
  static float PregnancyChance=>chanceField?.GetValue(null) is float f?f:.05f;
  static int NextLovin(Pawn p)=>nextLovin!=null?nextLovin(p):60000;
  static bool Enhanced(Pawn p)=>p.health?.hediffSet?.hediffs!=null && p.health.hediffSet.hediffs.Any(h=>h.def==HediffDefOf.LoveEnhancer);
  static void Give(Pawn p,Pawn other,bool enhanced){
   if(p.needs?.mood!=null){var t=(Thought_Memory)ThoughtMaker.MakeThought(ThoughtDefOf.GotSomeLovin);if(enhanced)t.moodPowerFactor=1.5f;p.needs.mood.thoughts.memories.TryGainMemory(t,other);}
   Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.GotLovin,p.Named(HistoryEventArgsNames.Doer)),true);
   var ev=p.relations!=null && p.relations.DirectRelationExists(PawnRelationDefOf.Spouse,other)?HistoryEventDefOf.GotLovin_Spouse:HistoryEventDefOf.GotLovin_NonSpouse;
   Find.HistoryEventsManager.RecordEvent(new HistoryEvent(ev,p.Named(HistoryEventArgsNames.Doer)),true);
   // 优侣种的“欢爱依赖”需求挂在这个方法的后置补丁上，调用它就会补满
   p.mindState.canLovinTick=Find.TickManager.TicksGame+NextLovin(p);
  }
 }

 public class CompProperties_TenderLovin:CompProperties_AbilityEffect{public CompProperties_TenderLovin(){compClass=typeof(CompAbilityEffect_TenderLovin);}}
 public class CompAbilityEffect_TenderLovin:CompAbilityEffect{
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   base.Apply(target,dest);var p=target.Pawn;var caster=parent.pawn;if(p==null)return;
   caster.jobs.StartJob(JobMaker.MakeJob(TenderLovin.Job,p),JobCondition.InterruptForced);
  }
  public override bool CanApplyOn(LocalTargetInfo target,LocalTargetInfo dest)=>Valid(target,false);
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   string why=TenderLovin.Reason(parent.pawn,target.Pawn);
   if(why!=null){if(throwMessages)Messages.Message(why,target.Pawn,MessageTypeDefOf.RejectInput,false);return false;}
   return base.Valid(target,throwMessages);
  }
 }

 // 发起人：贴着对方亲热一会儿（时长照原版），结束时双方得到原版亲热的结果
 public class JobDriver_TenderLovin:JobDriver{
  int ticksLeft;
  Pawn Partner=>(Pawn)job.GetTarget(TargetIndex.A).Thing;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticksLeft,"ticksLeft");}
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(Partner,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   this.FailOn(()=>Partner.Downed || !Partner.Awake() || Partner.InMentalState);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
   var start=ToilMaker.MakeToil("TenderLovinStart");
   start.initAction=()=>{
    ticksLeft=(int)(2500f*Mathf.Clamp(Rand.Range(.1f,1.1f),.1f,2f));
    var pj=JobMaker.MakeJob(TenderLovin.PartnerJob,pawn);pj.expiryInterval=ticksLeft+600;
    Partner.jobs.StartJob(pj,JobCondition.InterruptForced);
   };
   start.defaultCompleteMode=ToilCompleteMode.Instant;
   yield return start;
   var hold=ToilMaker.MakeToil("TenderLovinHold");
   hold.FailOn(()=>Partner.CurJobDef!=TenderLovin.PartnerJob);
   hold.tickAction=()=>{
    pawn.rotationTracker.FaceTarget(Partner);
    if(--ticksLeft<=0){ReadyForNextToil();return;}
    if(pawn.IsHashIntervalTick(100))FleckMaker.ThrowMetaIcon(pawn.Position,pawn.Map,FleckDefOf.Heart,.42f);
   };
   hold.defaultCompleteMode=ToilCompleteMode.Never;
   hold.socialMode=RandomSocialMode.Off;
   yield return hold;
   var done=ToilMaker.MakeToil("TenderLovinDone");
   done.initAction=()=>{
    TenderLovin.Outcome(pawn,Partner);
    if(Partner.CurJobDef==TenderLovin.PartnerJob)Partner.jobs.EndCurrentJob(JobCondition.Succeeded);
   };
   done.defaultCompleteMode=ToilCompleteMode.Instant;
   yield return done;
  }
 }
 // 对方：站着陪发起人，发起人不再做温存就结束
 public class JobDriver_TenderLovinPartner:JobDriver{
  Pawn Initiator=>(Pawn)job.GetTarget(TargetIndex.A).Thing;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   this.FailOn(()=>Initiator.CurJobDef!=TenderLovin.Job);
   var t=ToilMaker.MakeToil("TenderLovinPartner");
   t.tickAction=()=>{pawn.rotationTracker.FaceTarget(Initiator);if(pawn.IsHashIntervalTick(100))FleckMaker.ThrowMetaIcon(pawn.Position,pawn.Map,FleckDefOf.Heart,.42f);};
   t.defaultCompleteMode=ToilCompleteMode.Never;
   t.socialMode=RandomSocialMode.Off;
   yield return t;
  }
 }

 [HarmonyPatch(typeof(Pawn_AbilityTracker),nameof(Pawn_AbilityTracker.GainAbility))]
 static class TenderLovin_GainAbility{static void Postfix(Pawn_AbilityTracker __instance,AbilityDef def){if(def?.defName==TenderLovin.VreAbility)TenderLovin.Sync(__instance.pawn);}}
 [HarmonyPatch(typeof(Pawn_AbilityTracker),nameof(Pawn_AbilityTracker.RemoveAbility))]
 static class TenderLovin_RemoveAbility{static void Postfix(Pawn_AbilityTracker __instance,AbilityDef def){if(def?.defName==TenderLovin.VreAbility && TenderLovin.Def!=null && !TenderLovin.HasVre(__instance.pawn) && __instance.GetAbility(TenderLovin.Def)!=null)__instance.RemoveAbility(TenderLovin.Def);}}
 // 读旧档：已经有放荡交欢的人补上温存
 public class GameComponent_TenderLovin:GameComponent{
  public GameComponent_TenderLovin(Game g){}
  public override void LoadedGame(){if(TenderLovin.Def==null)return;foreach(var p in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)TenderLovin.Sync(p);}
 }
}
