using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.56.0「文化之神」：所有对文化类型的补丁。不用 [HarmonyPatch] 属性自动打，只在 IdeoBridge.Active 时由 IdeoStartup 调 Apply。
 public static class IdeoPatches {
  public static readonly List<string> Applied=new List<string>();
  public static void Apply(Harmony h){
   void P(Type t,string method,Type[] args,string prefix=null,string postfix=null){
    var m=args==null?AccessTools.Method(t,method):AccessTools.Method(t,method,args);
    if(m==null){Log.Warning("[DND边缘大陆] 文化补丁找不到 "+t.Name+"."+method+"，这一条跳过。");return;}
    h.Patch(m,prefix==null?null:new HarmonyMethod(typeof(IdeoPatches),prefix),postfix==null?null:new HarmonyMethod(typeof(IdeoPatches),postfix));
    Applied.Add(t.Name+"."+method);
   }
   P(typeof(IdeoFoundation_Deity),"GenerateDeities",null,postfix:nameof(DeitiesPost));
   P(typeof(RitualObligationTrigger_Date),"Init",null,postfix:nameof(DatePost));
   P(typeof(Precept_Ritual),"RemoveObligation",null,postfix:nameof(RemoveObligationPost));
   P(typeof(InteractionWorker_ConvertIdeoAttempt),"CertaintyReduction",new[]{typeof(Pawn),typeof(Pawn)},postfix:nameof(CertaintyPost));
   P(typeof(Pawn_IdeoTracker),"SetIdeo",new[]{typeof(Ideo)},prefix:nameof(SetIdeoPre),postfix:nameof(SetIdeoPost));
   P(typeof(IdeoGenerator),"GenerateIdeo",new[]{typeof(IdeoGenerationParms)},postfix:nameof(GenerateIdeoPost));
   P(typeof(PregnancyUtility),"ApplyBirthOutcome",null,postfix:nameof(BirthPost));
   P(typeof(Pawn_IdeoTracker),"TryJoinIdeoFromExposures",null,postfix:nameof(JoinPost));
   P(typeof(Precept_Relic),"Notify_ThingLost",null,postfix:nameof(RelicLostPost));
   P(typeof(Pawn),"Kill",null,prefix:nameof(KillPre),postfix:nameof(KillPost));
   P(typeof(RimWorld.Ability),"Activate",new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)},postfix:nameof(ActivatePost));
   P(typeof(RimWorld.Ability),"StartCooldown",new[]{typeof(int)},prefix:nameof(CooldownPre));
   P(typeof(Pawn_InteractionsTracker),"SocialFightChance",null,postfix:nameof(SocialFightPost));
  }

  // 2.2 固定神名
  static void DeitiesPost(IdeoFoundation_Deity __instance){try{IdeoSync.FixDeities(__instance.ideo);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 文化神名："+e,8815611);}}
  // 5.1 庆典日期固定成模因扩展里的季和日
  static readonly AccessTools.FieldRef<RitualObligationTrigger_Date,int> dayRef=AccessTools.FieldRefAccess<RitualObligationTrigger_Date,int>("triggerDaysSinceStartOfYear");
  static void DatePost(RitualObligationTrigger_Date __instance){var e=IdeoBridge.FestivalExt(__instance.ritual?.def);if(e!=null)dayRef(__instance)=FestivalDay(e);}
  public static int FestivalDay(LP_IdeoDeityExtension e)=>(int)e.festivalQuadrum*GenDate.DaysPerQuadrum+Mathf.Clamp(e.festivalDay,1,GenDate.DaysPerQuadrum)-1;
  public static int TriggerDay(RitualObligationTrigger_Date t)=>dayRef(t);
  // 5.2 错过义务：主神庆典过期没办 -5。
  static void RemoveObligationPost(Precept_Ritual __instance,RitualObligation __0,bool __1){
   if(__1 || __0==null || !__0.expires || __0.TicksUntilExpiration>0 || !IdeoBridge.Linked)return;
   var ext=RitualOutcomeComp_LPFestival.ExtOf(__instance);var god=ext?.deity;if(god==null || __instance.ideo!=IdeoBridge.PlayerIdeo || IdeoBridge.DeityOf(__instance.ideo)!=god)return;
   FaithUtility.AddFavor(god,-5f,"错过了"+__instance.LabelCap);
  }
  // 4.3 改宗修正
  static void CertaintyPost(Pawn __0,Pawn __1,ref float __result){if(__result>0f)__result*=ConvertFactor(__0,__1);}
  public static float ConvertFactor(Pawn initiator,Pawn recipient){
   if(!IdeoBridge.Linked || initiator==null || recipient==null)return 1f;float f=1f;
   var ii=IdeoBridge.OfPawn(initiator);var ri=IdeoBridge.OfPawn(recipient);var rg=FaithUtility.DevoteeGod(recipient);
   if(rg!=null && ii.deity==rg)f*=1.5f;
   if(rg!=null && ri.deity==rg)f*=.5f;
   if(FaithUtility.IsAtheist(recipient)){if(ii.Theist)f*=.5f;if(ii.defiant)f*=1.5f;}
   if(FaithUtility.IsChosen(initiator,out _))f*=1.3f;
   var dg=Demigods.Of(initiator);if(dg!=null)f*=1f+.1f*dg.Level;
   if(IdeoBridge.IsCleric(initiator))f*=1.2f;
   return f;
  }
  // 4.4 信徒特性跟着改宗（静默）
  static void SetIdeoPre(Pawn_IdeoTracker __instance,out Ideo __state){__state=__instance.Ideo;}
  static void SetIdeoPost(Pawn ___pawn,Ideo __0,Ideo __state){var ideo=__0;
   try{
    var p=___pawn;if(!IdeoBridge.Linked || p==null || ideo==null || ideo==__state || !p.RaceProps.Humanlike || PawnGenerator.IsBeingGenerated(p) || Faith.Devotee==null)return;
    var cur=FaithUtility.DevoteeGod(p);if(cur==null)return;var info=IdeoBridge.Of(ideo);int seed=p.thingIDNumber^GenTicks.TicksGame;
    var t=p.story?.traits?.GetTrait(Faith.Devotee);if(t==null)return;
    if(info.defiant){if(Rand.ChanceSeeded(.2f,seed))p.story.traits.RemoveTrait(t);return;}
    var y=info.deity;if(y==null || y==cur || y.devoteeDegree<=0)return;
    if(Rand.ChanceSeeded(cur.EnemyOf(y)?.15f:.3f,seed)){p.story.traits.RemoveTrait(t);p.story.traits.GainTrait(new Trait(Faith.Devotee,y.devoteeDegree,true));}
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 改宗信徒特性："+e,8815612);}
  }
  // 8.2 派系戒律、5.4 灰袍葬礼、7.3 圣物
  static void GenerateIdeoPost(IdeoGenerationParms parms,Ideo __result){
   if(__result==null)return;
   try{IdeoBridge.Invalidate();IdeoFactions.ApplyPrecepts(__result,parms.forFaction);IdeoSync.GreyRobeFuneral(__result);var g=IdeoBridge.DeityOf(__result);if(g!=null)IdeoSync.SwapRelic(__result,g);IdeoBridge.Invalidate();}
   catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 生成文化后处理："+e,8815613);}
  }
  // 9.1 新生儿补文化：母亲 → 父亲 → 玩家主文化。
  static void BirthPost(Thing __result,Pawn __4,Thing __5,Pawn __6){
   try{if(__result is Pawn baby)NewbornIdeo(baby,__4??__5 as Pawn,__6);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 新生儿文化："+e,8815614);}
  }
  // 原版不给婴儿文化（SetIdeo 对婴儿直接 return），长成儿童时按「接触过的文化」加权随机加入；接触为空就一直没有文化。
  // 所以：出生时给婴儿一个月份量的父母文化接触；长成儿童后仍没有文化的，补上父母 / 玩家主文化。
  static float exposurePerHour=-1f;
  public static float NewbornExposure{get{if(exposurePerHour<0f){try{exposurePerHour=(float)AccessTools.Field(typeof(Pawn_IdeoTracker),"ExposurePointsPerHour").GetValue(null);}catch{exposurePerHour=1f;}}return exposurePerHour*24f*30f;}}
  public static Ideo ParentIdeo(Pawn p,Pawn mother=null,Pawn father=null)=>(mother??p?.GetMother())?.Ideo??(father??p?.GetFather())?.Ideo??(p?.Faction?.IsPlayer==true?Faction.OfPlayer.ideos?.PrimaryIdeo:null);
  public static void NewbornIdeo(Pawn baby,Pawn mother,Pawn father){
   if(baby?.ideo==null || baby.Dead || baby.ideo.Ideo!=null)return;
   var ideo=ParentIdeo(baby,mother,father);if(ideo==null)return;
   if(baby.DevelopmentalStage.Baby())baby.ideo.IncreaseIdeoExposureIfBaby(ideo,NewbornExposure);else baby.ideo.SetIdeo(ideo);
  }
  static void JoinPost(Pawn ___pawn,ref bool __result){
   try{if(__result || ___pawn?.ideo==null || ___pawn.Ideo!=null || ___pawn.DevelopmentalStage.Baby() || !IdeoBridge.Active)return;var i=ParentIdeo(___pawn);if(i!=null){___pawn.ideo.SetIdeo(i);__result=___pawn.Ideo!=null;}}
   catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 儿童补文化："+e,8815616);}
  }
  // 7.3 圣物被毁 / 丢失：该神 -20。
  static void RelicLostPost(Precept_Relic __instance,Thing __0){
   if(!IdeoBridge.Linked || __0==null || __instance.ideo!=IdeoBridge.PlayerIdeo)return;
   var god=FaithUtility.AllDeities.FirstOrDefault(d=>d.relic==__0.def);if(god!=null)FaithUtility.AddFavor(god,-20f,"圣物"+__0.LabelNoCount+"失落");
  }
  // 1.3 杀死巨人；血祭
  static void KillPre(Pawn __instance,out bool __state){__state=!__instance.Dead;}
  static void KillPost(Pawn __instance,DamageInfo? dinfo,bool __state){
   if(!__state || !__instance.Dead || !IdeoBridge.Active)return;
   try{
    IdeoAltar.OnKilled(__instance);
    if(dinfo?.Instigator is Pawn k && k.RaceProps.Humanlike && IdeoBridge.IsGiant(__instance))IdeoEvents.Record("LP_HE_GiantKilled",k);
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 文化击杀钩子："+e,8815615);}
  }
  // 1.3 施放法术（模组自己的神术、巫妖法术、半神能力；原版灵能也算）
  static void ActivatePost(RimWorld.Ability __instance,bool __result){if(__result && __instance?.pawn!=null && (IdeoBridge.IsSpell(__instance) || __instance.def.IsPsycast))IdeoEvents.Record("LP_HE_SpellCast",__instance.pawn);}
  // 2.4 魔网至上：模组施法能力冷却 ×0.95（VPE 路径能力是另一个类，不改）。
  static void CooldownPre(RimWorld.Ability __instance,ref int __0){if(__0>0 && IdeoBridge.Linked && IdeoBridge.IsSpell(__instance) && IdeoBridge.OfPawn(__instance.pawn).weave)__0=Mathf.RoundToInt(__0*.95f);}
  // 5.2 提尔「立下誓言」：社交冲突几率 ×0.5
  static HediffDef swore;
  static void SocialFightPost(Pawn ___pawn,ref float __result){if(__result<=0f || ___pawn==null)return;if(swore==null)swore=DefDatabase<HediffDef>.GetNamedSilentFail("LP_SworeOath");if(swore!=null && ___pawn.health.hediffSet.HasHediff(swore))__result*=.5f;}
 }
}
