using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 夺心魔蝌蚪：植入、五阶段寄生、各种族抗性、化形。
 public static class Tadpoles {
  public const float TrollFactor=.5f,TrollExpelChance=.2f,DeafDeathChance=.3f,SkillKept=.5f,ButcherChance=.5f,StartingCerebrum=.5f;
  static HediffDef parasite;static ThingDef item;static BackstoryDef ceremorphed;static PawnRelationDef relation;
  public static HediffDef Parasite=>parasite??(parasite=DefDatabase<HediffDef>.GetNamed("LP_TadpoleParasite"));
  public static ThingDef Item=>item??(item=DefDatabase<ThingDef>.GetNamed("LP_Tadpole"));
  public static BackstoryDef Ceremorphed=>ceremorphed??(ceremorphed=DefDatabase<BackstoryDef>.GetNamed("LP_Ceremorphed"));
  public static PawnRelationDef Relation=>relation??(relation=DefDatabase<PawnRelationDef>.GetNamed("LP_Ceremorph"));
  public static Hediff_Tadpole Of(Pawn p){var list=p?.health?.hediffSet?.hediffs;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i] is Hediff_Tadpole h)return h;return null;}
  // 不能寄生的原因；null 表示可以。
  public static string Immunity(Pawn p){
   if(p==null || p.Dead)return "宿主必须活着。";
   if(p.RaceProps.IsMechanoid)return "机械体没有大脑，蝌蚪死了。";
   if(!p.RaceProps.Humanlike)return "蝌蚪只能寄生人形生物。";
   if(!p.DevelopmentalStage.Adult())return "蝌蚪只能寄生成年人。";
   if(MindFlayer.IsMindFlayer(p))return "目标已经是夺心魔了。";
   if(Necro.Undead(p))return "巫妖与死灵造物没有活着的大脑，蝌蚪死了。";
   if(FireGiant.Active(p))return "火巨人体内的熔火之热把蝌蚪烫死了。";
   // 1.38.0：流浆人没有固定的大脑位置，蝌蚪钻进去就被原生质溶解了。
   if(Oozefolk.Is(p))return "流浆人的原生质把蝌蚪溶解了。";
   if(MindFlayer.Brain(p)==null)return "目标没有大脑。";
   return null;
  }
  // 化形速度倍率：心智抗性按比例减速，巨魔减半，带 TadpoleProgressExtension 的基因（吉斯洋基宿敌等）再相乘。
  public static float SpeedFactor(Pawn p)=>(1f-DNDMind.Resistance(p))*(CommonRaces.IsTroll(p)?TrollFactor:1f)*GeneFactor(p);
  static List<GeneDef> slowGenes;static int slowGeneCount=-1;
  public static float GeneFactor(Pawn p){
   if(slowGenes==null || slowGeneCount!=DefDatabase<GeneDef>.DefCount){slowGeneCount=DefDatabase<GeneDef>.DefCount;slowGenes=DefDatabase<GeneDef>.AllDefsListForReading.Where(g=>g.GetModExtension<TadpoleProgressExtension>()!=null).ToList();}
   float f=1f;if(p?.genes==null)return f;
   foreach(var g in slowGenes)if(p.genes.HasActiveGene(g))f*=g.GetModExtension<TadpoleProgressExtension>().progressFactor;
   return f;
  }
  public static float DailyProgress(Pawn p)=>SpeedFactor(p)/Mathf.Clamp(LichMod.Settings.ceremorphDays,1,15);
  public static Hediff_Tadpole Implant(Pawn p,Pawn implanter,Faction faction,out string failure){
   failure=Of(p)!=null?"目标已经被寄生了。":Immunity(p);
   if(failure!=null){
    if(p!=null && (p.Faction==Faction.OfPlayer || p.HostFaction==Faction.OfPlayer || faction==Faction.OfPlayer))Messages.Message("蝌蚪没能寄生"+p.LabelShort+"："+failure,p,MessageTypeDefOf.NeutralEvent,false);
    return null;
   }
   var brain=MindFlayer.Brain(p);
   var h=(Hediff_Tadpole)HediffMaker.MakeHediff(Parasite,p,brain);h.implanter=implanter;h.implanterFaction=faction;
   p.health.AddHediff(h,brain);
   return h;
  }
  // 化形：内源基因保留，异种型改为夺心魔；特质重置；成年背景换成"化形者"；技能经验保留一半；加入植入者的阵营。
  public static void Ceremorph(Pawn p,Pawn implanter,Faction faction){
   var endogenes=p.genes.Endogenes.Select(g=>g.def).ToList();
   // 先记下自身经验：异种型改变后天赋基因会改变显示等级。
   var xp=p.skills?.skills.ToDictionary(s=>s.def,MindFlayer.OwnXp);
   // 先重置特质再换异种型：新基因（例如体弱）会压制旧特质，被压制的特质删不掉。
   if(p.story!=null)foreach(var t in p.story.traits.allTraits.Where(t=>t.sourceGene==null).ToList())p.story.traits.RemoveTrait(t,true);
   p.genes.SetXenotype(MindFlayer.Xenotype);
   foreach(var def in endogenes)if(!p.genes.Endogenes.Any(g=>g.def==def))p.genes.AddGene(def,false);
   if(p.story!=null)p.story.Adulthood=Ceremorphed;
   if(xp!=null)foreach(var s in p.skills.skills){s.levelInt=0;s.xpSinceLastLevel=0;if(xp.TryGetValue(s.def,out float total))MindFlayer.GrantXp(p,s.def,total*SkillKept);}
   if(faction!=null && p.Faction!=faction){if(p.guest!=null && p.guest.HostFaction!=null)p.guest.SetGuestStatus(null,GuestStatus.Guest);p.SetFaction(faction,implanter);}
   if(implanter!=null && implanter!=p && !implanter.Destroyed && p.relations!=null && !p.relations.DirectRelationExists(Relation,implanter))p.relations.AddDirectRelation(Relation,implanter);
   var g=MindFlayer.Cerebrum(p);if(g!=null){g.Value=StartingCerebrum;g.SyncHunger();}
   p.Drawer?.renderer?.SetAllGraphicsDirty();Necro.Refresh(p);
   if(p.Faction==Faction.OfPlayer || implanter?.Faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("化形完成",p.LabelShort+"脑中的蝌蚪完成了化形：现在是一个夺心魔了。性格被彻底改写，旧日的技能只剩下一半，背景变成了\"化形者\"。"+(faction!=null?"\n\n它效忠于"+faction.Name+"。":""),LetterDefOf.NeutralEvent,p);
  }
 }

 // 挂在 GeneDef 上：带这个基因的宿主，蝌蚪化形速度×progressFactor（与心智抗性、巨魔减半相乘）。
 public class TadpoleProgressExtension : DefModExtension {
  public float progressFactor=1f;
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(progressFactor<0 || progressFactor>1)yield return "TadpoleProgressExtension.progressFactor must be in [0,1]";}
 }
 public partial class Hediff_Tadpole : HediffWithComps {
  public Pawn implanter;public Faction implanterFaction;
  int ticks,dayTicks;
  public float Progress=>Severity;
  public override bool ShouldRemove=>base.ShouldRemove || (pawn!=null && !pawn.Dead && (MindFlayer.IsMindFlayer(pawn) || Tadpoles.Immunity(pawn)!=null));
  public override string LabelInBrackets=>(CurStage?.label??"")+" "+Severity.ToStringPercent("0")+(Frozen?"，已抑制":"");
  public override string TipStringExtra=>"化形进度："+Severity.ToStringPercent("0")+"\n每天约+"+(Tadpoles.DailyProgress(pawn)*100f).ToString("0.#")+"%（基础"+LichMod.Settings.ceremorphDays+"天"+(DNDMind.Resistance(pawn)>0?"，心智抗性减速":"")+(CommonRaces.IsTroll(pawn)?"，巨魔减半":"")+(Tadpoles.GeneFactor(pawn)<1f?"，血脉抗拒×"+Tadpoles.GeneFactor(pawn).ToStringPercent():"")+"）"+(Frozen?"\n抑灵剂正压制着蝌蚪，化形暂停。":"")+(implanterFaction!=null?"\n化形后将效忠："+implanterFaction.Name:"");
  // 抑灵剂冻结（第5阶段实现）。
  public bool Frozen{get{bool frozen=false;CheckFrozen(ref frozen);return frozen;}}
  partial void CheckFrozen(ref bool frozen);
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(pawn==null || pawn.Dead)return;
   if((ticks+=delta)<250)return;
   int elapsed=ticks;ticks=0;
   if((dayTicks+=elapsed)>=GenDate.TicksPerDay){dayTicks-=GenDate.TicksPerDay;if(DailyRoll(Rand.Value))return;}
   Advance(elapsed);
  }
  public void Advance(int elapsed){
   if(Frozen)return;
   Severity=Mathf.Min(1f,Severity+Tadpoles.DailyProgress(pawn)*elapsed/GenDate.TicksPerDay);
   if(Severity>=1f)Complete();
  }
  // 每天一次：巨魔20%排出蝌蚪；心灵失聪者30%让蝌蚪饿死。返回 true 表示蝌蚪已经没了。
  public bool DailyRoll(float roll){
   if(CommonRaces.IsTroll(pawn) && roll<Tadpoles.TrollExpelChance){End(pawn.LabelShort+"的巨魔再生把脑中的蝌蚪排了出来。");return true;}
   if(MindFlayer.PsychicallyDeaf(pawn) && roll<Tadpoles.DeafDeathChance){End(pawn.LabelShort+"的大脑一片寂静，蝌蚪找不到可吃的念头，饿死了。");return true;}
   return false;
  }
  void End(string message){
   if(pawn.Faction==Faction.OfPlayer || pawn.HostFaction==Faction.OfPlayer)Messages.Message(message,pawn,MessageTypeDefOf.PositiveEvent,false);
   pawn.health.RemoveHediff(this);
  }
  public void Complete(){
   var p=pawn;var who=implanter;var faction=implanterFaction;
   p.health.RemoveHediff(this);
   Tadpoles.Ceremorph(p,who,faction);
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref implanter,"tadpoleImplanter");Scribe_References.Look(ref implanterFaction,"tadpoleFaction");Scribe_Values.Look(ref ticks,"tadpoleTicks",0);Scribe_Values.Look(ref dayTicks,"tadpoleDayTicks",0);}
 }

 public class Recipe_ImplantTadpole : Recipe_Surgery {
  public override bool AvailableOnNow(Thing thing,BodyPartRecord part=null){
   if(!(thing is Pawn p) || !base.AvailableOnNow(thing,part))return false;
   return p.RaceProps.Humanlike && p.DevelopmentalStage.Adult() && Tadpoles.Of(p)==null && !MindFlayer.IsMindFlayer(p);
  }
  public override void ApplyOnPawn(Pawn pawn,BodyPartRecord part,Pawn billDoer,List<Thing> ingredients,Bill bill){
   if(billDoer!=null && CheckSurgeryFail(billDoer,pawn,ingredients,part,bill))return;
   bool violation=IsViolationOnPawn(pawn,part,Faction.OfPlayer);
   Tadpoles.Implant(pawn,billDoer,billDoer?.Faction,out _);
   if(billDoer!=null && violation)ReportViolation(pawn,billDoer,pawn.HomeFaction,-70,null);
  }
 }

 // 夺心魔尸体被屠宰时有50%概率取出一条蝌蚪。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.ButcherProducts))]
 public static class MindFlayerButcher {
  public static bool Yields(Pawn p)=>MindFlayer.CerebrumGene!=null && p?.genes?.GetGene(MindFlayer.CerebrumGene)!=null;
  static IEnumerable<Thing> Postfix(IEnumerable<Thing> values,Pawn __instance){
   foreach(var t in values)yield return t;
   if(Yields(__instance) && Rand.Chance(Tadpoles.ButcherChance))yield return ThingMaker.MakeThing(Tadpoles.Item);
  }
 }
}
