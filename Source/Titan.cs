using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.24.0 泰坦（天穹神裔）：神威资源、神罚、神裔之息、天候随心、不可动摇、神裔的傲慢。血量只走巨躯规则。
 public static class Titan {
  public const float Max=100f,DailyGain=10f,HappyGain=10f,KillGain=5f,SmiteCost=30f,BreathCost=40f,HappyMood=.8f,SadMood=.3f,BreathHeal=10f;
  public const int SmiteRange=10,BreathRadius=5,DawnHour=6;
  public static Gene_TitanDivinity Divinity(Pawn p)=>p?.genes?.GetFirstGeneOfType<Gene_TitanDivinity>() is Gene_TitanDivinity g && g.Active?g:null;
  public static float Points(Pawn p)=>(Divinity(p)?.Value??0f)*Max;
  public static bool CanPay(Pawn p,float cost)=>Divinity(p)!=null && Points(p)+.001f>=cost;
  public static bool Pay(Pawn p,float cost){var g=Divinity(p);if(g==null || !CanPay(p,cost))return false;g.Value=Mathf.Clamp01(g.Value-cost/Max);return true;}
  public static void Gain(Pawn p,float points){var g=Divinity(p);if(g!=null)g.Value=Mathf.Clamp01(g.Value+points/Max);}
  public static float Mood(Pawn p)=>p?.needs?.mood?.CurLevelPercentage??.5f;

  // ---------- 神罚：10格内一格落一道闪电（原版落雷同款：火焰爆炸+火），外加一圈 EMP ----------
  public static bool Smite(Pawn caster,IntVec3 cell){
   var map=caster.Map;if(map==null || !Pay(caster,SmiteCost))return false;
   map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map,cell));
   GenExplosion.DoExplosion(cell,map,1.9f,DamageDefOf.EMP,caster,10,doVisualEffects:false,doSoundEffects:false);
   return true;
  }
  // ---------- 神裔之息：5格内友方每处伤口-10严重度，止住出血 ----------
  public static int Breath(Pawn caster){
   var map=caster.Map;if(map==null || !Pay(caster,BreathCost))return 0;int healed=0;
   foreach(var p in Races124.PawnsNear(map,caster.Position,BreathRadius).ToList()){
    if(p!=caster && (p.Faction==null || p.Faction!=caster.Faction))continue;
    bool any=false;
    foreach(var h in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList()){
     any=true;
     if(h.Severity<=BreathHeal){p.health.RemoveHediff(h);continue;}
     h.Severity-=BreathHeal;if(h.Bleeding)h.Tended(.6f,1f);
    }
    if(any){healed++;FleckMaker.ThrowMetaIcon(p.Position,map,FleckDefOf.HealingCross);}
   }
   FleckMaker.Static(caster.Position,map,FleckDefOf.PsycastAreaEffect,BreathRadius);
   return healed;
  }

  // ---------- 天候随心：泰坦所在地图，每天黎明看一次心情 ----------
  public static WeatherDef WeatherFor(Pawn titan){
   float m=Mood(titan);
   if(m>=HappyMood)return DefDatabase<WeatherDef>.GetNamedSilentFail("Clear");
   if(m<=SadMood)return DefDatabase<WeatherDef>.GetNamedSilentFail("RainyThunderstorm");
   return null;
  }
  public static WeatherDef ApplyWeather(Pawn titan){
   if(!LichMod.Settings.titanWeather || titan?.Map==null)return null;
   var w=WeatherFor(titan);if(w!=null && titan.Map.weatherManager.curWeather!=w)titan.Map.weatherManager.TransitionTo(w);
   return w;
  }
  public static void WeatherTick(){
   if(!LichMod.Settings.titanWeather || Find.Maps==null)return;var state=Races124.State;if(state==null)return;
   foreach(var map in Find.Maps){
    if(GenLocalDate.HourOfDay(map)!=DawnHour)continue;
    int day=GenLocalDate.DayOfYear(map)+GenLocalDate.Year(map)*60;
    if(state.weatherDay.TryGetValue(map.uniqueID,out int last) && last==day)continue;
    var titan=map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p=>Divinity(p)!=null);if(titan==null)continue;
    state.weatherDay[map.uniqueID]=day;ApplyWeather(titan);
   }
  }
 }
 public class Gene_TitanDivinity : Gene_Resource {
  int ticks;
  public override float InitialResourceMax=>1f;
  public override float MinLevelForAlert=>0f;
  protected override Color BarColor=>new Color(.92f,.78f,.36f);
  protected override Color BarHighlightColor=>new Color(1f,.92f,.6f);
  public float Points=>Value*Titan.Max;
  public override void PostAdd(){base.PostAdd();Value=.5f;}
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(!Active || pawn?.Dead!=false)return;
   if((ticks+=delta)<250)return;int n=ticks;ticks=0;Accrue(n);
  }
  // 每天自然+10；心情≥80%时每天再+10。
  public void Accrue(int elapsed){float daily=Titan.DailyGain+(Titan.Mood(pawn)>=Titan.HappyMood?Titan.HappyGain:0f);Value=Mathf.Clamp01(Value+daily/Titan.Max*elapsed/60000f);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticks,"divinityTicks",0);}
 }
 public class GeneGizmo_DivinityResource : GeneGizmo_Resource {
  static bool dragging;
  readonly Gene_Resource resource;
  public GeneGizmo_DivinityResource(Gene_Resource gene,List<IGeneResourceDrain> drainGenes,Color barColor,Color barHighlightColor):base(gene,drainGenes,barColor,barHighlightColor){resource=gene;}
  protected override bool DraggingBar{get=>dragging;set=>dragging=value;}
  protected override bool IsDraggable=>false;
  protected override string Title=>"神威";
  protected override string GetTooltip()=>"神威："+(resource.Value*Titan.Max).ToString("0")+" / "+(resource.Max*Titan.Max).ToString("0")+"\n每天自然恢复10点；心情≥80%时每天再恢复10点；每击杀一个敌人+5。\n神罚消耗30，神裔之息消耗40。";
 }
 // 击杀敌人+5神威。敌对关系在死前判断。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class TitanKillDivinity {
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out Pawn __state){__state=null;if(!__instance.Dead && dinfo?.Instigator is Pawn a && Titan.Divinity(a)!=null && a.HostileTo(__instance))__state=a;}
  static void Postfix(Pawn __instance,Pawn __state){if(__state!=null && __instance.Dead)Titan.Gain(__state,Titan.KillGain);}
 }
 // 不可动摇：不会被眩晕（含 EMP）。
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.StunFor))]
 public static class TitanStunImmunity {
  static bool Prefix(Pawn ___parentPawn){if(!Races124.Has(___parentPawn,"LP_TitanUnshakable"))return true;Items.Mote(___parentPawn,"不可动摇",new Color(1f,.9f,.55f));return false;}
 }
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.Notify_DamageApplied))]
 public static class TitanEmpImmunity {
  static bool Prefix(Pawn ___parentPawn)=>!Races124.Has(___parentPawn,"LP_TitanUnshakable");
 }
 // 神裔的傲慢：殖民地同一地图上有体型比自己大的人，心情-3。
 public class ThoughtWorker_TitanPride : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!p.Spawned || !Races124.IsTitan(p) || p.Faction==null)return ThoughtState.Inactive;
   float size=p.BodySize;
   foreach(var o in p.Map.mapPawns.SpawnedPawnsInFaction(p.Faction))if(o!=p && o.RaceProps.Humanlike && !o.Dead && o.BodySize>size+.001f)return ThoughtState.ActiveAtStage(0);
   return ThoughtState.Inactive;
  }
 }
 // 剧本“坠落的神裔”：泰坦近战、社交至少8级并有兴趣，手持天穹之剑。
 public class ScenPart_FallenEmpyrean : ScenPart {
  public QualityCategory bladeQuality=QualityCategory.Good;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref bladeQuality,"bladeQuality",QualityCategory.Good);}
  public override string Summary(Scenario scen)=>"开局的泰坦近战与社交都很出色，手持一把"+bladeQuality.GetLabel()+"品质的天穹之剑；开局就认下一位父神（七位可信奉的神里随机），泰坦觉醒之路已经开启。";
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map==null)return;var titan=Apply(ScenEdit.Starters(map).ToList());if(titan!=null)Awaken(titan);}
  // 1.41.0：父神存进成神进度（AscensionPaths.Father），开泰坦觉醒之路，开局信件写出父神。
  public static AscensionProgress Awaken(Pawn titan){
   var father=AscensionPaths.Father(titan,true);
   AscensionProgress pr=null;
   if(Ascension.Enabled && Ascension.Tracker?.Of(titan)==null)pr=Ascension.Start(titan,Ascension.Titan,false);
   string text=titan.LabelShort+"从天穹坠落时，还记得父神的名字："+(father?.FullLabel??"（没有可信奉的神）")+(pr?.portfolio!=null?"，神职“"+pr.portfolio.label+"”":"")+"。";
   if(pr!=null)text+="\n\n泰坦觉醒之路已经开启（推荐路线）。选中"+titan.LabelShort+"，点“成神之路”查看进度：\n\n"+AscensionPaths.StageLabel(pr)+"："+AscensionPaths.StageText(pr)+"\n\n之后是巨人之战和天穹之门，完成后直接觉醒为半神。不想走这条路，可以在窗口里放弃。";
   else text+="\n\n成神任务线在设置里关着：父神已经记下，打开后再踏上泰坦觉醒之路时还是这位。";
   Ascension.Letter("坠落的神裔：父神"+(father?.label??""),text,LetterDefOf.PositiveEvent,titan);
   return pr;
  }
  public Pawn Apply(List<Pawn> pawns){
   var titan=pawns.FirstOrDefault(Races124.IsTitan);if(titan==null)return null;
   foreach(var skill in new[]{SkillDefOf.Melee,SkillDefOf.Social}){var s=titan.skills?.GetSkill(skill);if(s==null || s.TotallyDisabled)continue;s.Level=Math.Max(s.Level,Rand.RangeInclusive(8,12));if(s.passion==Passion.None)s.passion=Passion.Minor;}
   var def=Items.Thing("LP_EmpyreanBlade");
   if(def!=null && titan.equipment!=null && !titan.WorkTagIsDisabled(WorkTags.Violent)){
    var w=(ThingWithComps)ThingMaker.MakeThing(def);w.TryGetComp<CompQuality>()?.SetQuality(bladeQuality,ArtGenerationContext.Colony);
    if(titan.equipment.Primary!=null)titan.equipment.TryDropEquipment(titan.equipment.Primary,out _,titan.Position,false);
    titan.equipment.AddEquipment(w);
   }
   return titan;
  }
 }
}
