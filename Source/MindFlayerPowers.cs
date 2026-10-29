using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 夺心魔灵能：读心、支配人类、心灵束缚（仅意识形态）、念力投掷、灵能隐匿、心灵风暴。
 public static class MindFlayerPowers {
  public const float ResistanceDrop=3f,BindCertainty=.4f,StormRadius=8f,StormResistFactor=.5f,ThrowDamage=10f;
  public const int ThrowCells=4,StormStunTicks=360,DominateTicks=1800,DazeTicks=30000,UnseenTicks=2500;
  static MentalStateDef dominated;static HediffDef dominatedHediff,unseen;static AbilityDef bind;
  public static MentalStateDef Dominated=>dominated??(dominated=DefDatabase<MentalStateDef>.GetNamed("LP_Dominated"));
  public static HediffDef DominatedHediff=>dominatedHediff??(dominatedHediff=DefDatabase<HediffDef>.GetNamed("LP_Dominated"));
  // 原版核心就有心灵隐形（PsychicInvisibility）：有就用原版，没有才用自己的 LP_Unseen。
  public static HediffDef Unseen=>unseen??(unseen=DefDatabase<HediffDef>.GetNamedSilentFail("PsychicInvisibility")??DefDatabase<HediffDef>.GetNamed("LP_Unseen"));
  public static AbilityDef Bind=>bind??(bind=DefDatabase<AbilityDef>.GetNamed("LP_PsychicBind"));
  public static bool Living(Pawn caster,Pawn p)=>p!=null && p!=caster && !p.Dead && p.Spawned && p.Map==caster.Map;
  // 心灵束缚只在意识形态加载时出现。
  public static void SyncBind(Pawn p){
   if(p?.abilities==null)return;bool want=ModsConfig.IdeologyActive && MindFlayer.IsMindFlayer(p);bool has=p.abilities.GetAbility(Bind)!=null;
   if(want && !has)p.abilities.GainAbility(Bind);else if(!want && has)p.abilities.RemoveAbility(Bind);
  }

  public static string ReadMind(Pawn caster,Pawn target){
   var lines=new List<string>();
   if(target.story!=null)lines.Add("特质："+(target.story.traits.allTraits.Count==0?"无":string.Join("、",target.story.traits.allTraits.Select(t=>t.LabelCap))));
   var best=target.skills?.skills.Where(s=>!s.TotallyDisabled).OrderByDescending(s=>s.Level).Take(3).ToList();
   if(best!=null && best.Count>0)lines.Add("擅长："+string.Join("、",best.Select(s=>s.def.label+" "+s.Level)));
   var mood=target.needs?.mood;
   if(mood!=null){
    lines.Add("心情："+mood.CurLevelPercentage.ToStringPercent());
    var thoughts=new List<Thought>();mood.thoughts.GetAllMoodThoughts(thoughts);
    var worst=thoughts.Where(t=>t.MoodOffset()!=0).OrderBy(t=>t.MoodOffset()).Take(3).ToList();
    if(worst.Count>0)lines.Add("最在意："+string.Join("、",worst.Select(t=>t.LabelCap+"（"+t.MoodOffset().ToString("+0;-0")+"）")));
   }
   if(target.IsPrisonerOfColony && target.guest!=null){float before=target.guest.resistance;target.guest.resistance=Mathf.Max(0,target.guest.resistance-ResistanceDrop);lines.Add("抵抗："+before.ToString("0.#")+" → "+target.guest.resistance.ToString("0.#"));}
   string text=string.Join("\n",lines);
   if(caster.Faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("读心："+target.LabelShort,caster.LabelShort+"窥探了"+target.LabelShort+"的心思。\n\n"+text,LetterDefOf.NeutralEvent,target);
   return text;
  }
  public static string CannotDominate(Pawn caster,Pawn p){
   if(!Living(caster,p) || !p.RaceProps.Humanlike)return "需要地图上活着的人形生物。";
   if(p.Faction==caster.Faction || p.Faction==Faction.OfPlayer)return "不能支配自己阵营的人。";
   if(p.Downed)return "目标已经倒地。";
   if(MindFlayer.PsychicallyDeaf(p))return "心灵失聪者无法被支配。";
   if(MindFlayer.IsMindFlayer(p))return "夺心魔的心智无法被支配。";
   return null;
  }
  // 支配：通过 DNDMind.TryResist 判定；成功后30秒内只攻击原来的同伴，时间到精疲力竭倒地。
  public static bool Dominate(Pawn caster,Pawn p){
   if(CannotDominate(caster,p)!=null)return false;
   if(DNDMind.TryResist(p)){MindFlayer.Mote(p,"抵抗！",new Color(.7f,.85f,1f));return false;}
   var h=p.health.AddHediff(DominatedHediff);
   if(!p.mindState.mentalStateHandler.TryStartMentalState(Dominated,"被"+caster.LabelShort+"支配",true,true,false,null,false,false,false))MindFlayer.Mote(p,"被支配（无人可攻击）",new Color(.75f,.4f,.95f));
   return true;
  }
  // 被支配者的攻击对象：最近的、还能动的原阵营同伴。
  public static Pawn DominationTarget(Pawn p){
   if(p?.Map==null || p.Faction==null)return null;
   return p.Map.mapPawns.SpawnedPawnsInFaction(p.Faction).Where(o=>o!=p && !o.Dead && !o.Downed && p.CanReach(o,PathEndMode.Touch,Danger.Deadly)).OrderBy(o=>o.Position.DistanceToSquared(p.Position)).FirstOrDefault();
  }
  public static void EndDomination(Pawn p){
   if(p==null || p.Dead)return;
   if(p.MentalStateDef==Dominated)p.MentalState.RecoverFromState();
   var old=p.health.hediffSet.GetFirstHediffOfDef(ElderBrain.Daze);if(old!=null)p.health.RemoveHediff(old);
   var daze=p.health.AddHediff(ElderBrain.Daze);daze.TryGetComp<HediffComp_Disappears>()?.SetDuration(DazeTicks);
  }
  public static string CannotBind(Pawn caster,Pawn p){
   if(!ModsConfig.IdeologyActive)return "需要《意识形态》。";
   if(!Living(caster,p) || !p.RaceProps.Humanlike || p.ideo==null)return "需要地图上有信仰的人形生物。";
   if(caster.Ideo==null)return "施法者没有意识形态。";
   if(p.Ideo==caster.Ideo)return "目标已经信仰同一意识形态。";
   if(MindFlayer.PsychicallyDeaf(p))return "心灵失聪者不受影响。";
   return null;
  }
  public static bool PsychicBind(Pawn caster,Pawn p){
   if(CannotBind(caster,p)!=null)return false;
   if(DNDMind.TryResist(p)){MindFlayer.Mote(p,"抵抗！",new Color(.7f,.85f,1f));return false;}
   p.ideo.OffsetCertainty(-BindCertainty);
   if(p.ideo.Certainty<=.001f){p.ideo.SetIdeo(caster.Ideo);if(p.Faction==Faction.OfPlayer || caster.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"的信仰被群脑改写，改信了"+caster.Ideo.name+"。",p,MessageTypeDefOf.NeutralEvent,false);}
   return true;
  }
  // 念力投掷：沿施法者到目标的方向把目标甩出最多4格。
  public static IntVec3 ThrowDestination(Pawn caster,Pawn p){
   Vector3 dir=(p.Position-caster.Position).ToVector3();if(dir.sqrMagnitude<.01f)dir=caster.Rotation.FacingCell.ToVector3();
   return DNDPush.Destination(p.Map,p.Position,dir,ThrowCells);
  }
  public static IntVec3 Throw(Pawn caster,Pawn p)=>Throw(caster,p,out _);
  public static IntVec3 Throw(Pawn caster,Pawn p,out DamageWorker.DamageResult hit){
   var dest=ThrowDestination(caster,p);
   DNDPush.Move(p,dest);
   hit=p.TakeDamage(new DamageInfo(DamageDefOf.Blunt,ThrowDamage,0f,-1f,caster));
   if(!p.Dead)p.stances?.stunner?.StunFor(60,caster,false,false,false);
   FleckMaker.Static(dest,p.MapHeld??caster.Map,FleckDefOf.PsycastAreaEffect,1f);
   return dest;
  }
  public static Hediff Hide(Pawn caster){
   var old=caster.health.hediffSet.GetFirstHediffOfDef(Unseen);if(old!=null)caster.health.RemoveHediff(old);
   var h=caster.health.AddHediff(Unseen);h.TryGetComp<HediffComp_Disappears>()?.SetDuration(UnseenTicks);
   return h;
  }
  public static int Storm(Pawn caster){
   int hit=0;
   foreach(var p in caster.Map.mapPawns.AllPawnsSpawned.Where(p=>p!=caster && p.Position.InHorDistOf(caster.Position,StormRadius) && p.HostileTo(caster)).ToList())if(MindFlayer.PsychicStrike(caster,p,StormStunTicks,StormResistFactor))hit++;
   foreach(var c in GenRadial.RadialCellsAround(caster.Position,StormRadius,true))if(c.InBounds(caster.Map) && Rand.Chance(.25f))FleckMaker.Static(c,caster.Map,FleckDefOf.PsycastAreaEffect,.7f);
   return hit;
  }
 }

 public partial class CompMindFlayerAbility {
  partial void InvalidPowerImpl(LocalTargetInfo target,ref string reason){
   var caster=parent.pawn;var p=target.Pawn;
   switch(Props.effect){
    case "readmind":reason=MindFlayerPowers.Living(caster,p) && p.RaceProps.Humanlike?null:"需要地图上活着的人形生物。";break;
    case "dominate":reason=MindFlayerPowers.CannotDominate(caster,p);break;
    case "bind":reason=MindFlayerPowers.CannotBind(caster,p);break;
    case "throw":reason=MindFlayerPowers.Living(caster,p)?null:"需要地图上活着的生物。";break;
    case "unseen":case "storm":reason=null;break;
   }
  }
  partial void ApplyPowerImpl(LocalTargetInfo target){
   var caster=parent.pawn;
   switch(Props.effect){
    case "readmind":MindFlayerPowers.ReadMind(caster,target.Pawn);break;
    case "dominate":MindFlayerPowers.Dominate(caster,target.Pawn);break;
    case "bind":MindFlayerPowers.PsychicBind(caster,target.Pawn);break;
    case "throw":MindFlayerPowers.Throw(caster,target.Pawn);break;
    case "unseen":MindFlayerPowers.Hide(caster);break;
    case "storm":MindFlayerPowers.Storm(caster);break;
   }
  }
 }

 public class Hediff_Dominated : HediffWithComps {
  public override void PostRemoved(){base.PostRemoved();if(pawn!=null && !pawn.Dead){MindFlayerPowers.EndDomination(pawn);HeroDeeds.DominationEnded(pawn);}} // 1.58.0 心智堡垒
 }
 // 被支配者沿用原版"杀意狂暴"的行为，但目标换成原阵营的同伴。
 [HarmonyPatch(typeof(MentalState_MurderousRage),"TryFindNewTarget")]
 public static class DominationTarget {
  static bool Prefix(MentalState_MurderousRage __instance,ref bool __result){
   if(__instance.def!=MindFlayerPowers.Dominated)return true;
   __instance.target=MindFlayerPowers.DominationTarget(__instance.pawn);__result=__instance.target!=null;return false;
  }
 }

 // ---------- 半夺心魔 ----------
 public static class HalfIllithid {
  public const float FailChance=.25f,FreezeLimit=.5f;public const int MaxPowers=3;
  public static readonly string[] Unlocks={"LP_MindBlast","LP_TelekineticThrow","LP_Unseen"};
  static GeneDef gene;static HediffDef suppression;
  public static GeneDef Gene=>gene??(gene=DefDatabase<GeneDef>.GetNamed("LP_HalfIllithidGene"));
  public static HediffDef Suppression=>suppression??(suppression=DefDatabase<HediffDef>.GetNamed("LP_PsionicSuppression"));
  public static Gene_HalfIllithid Of(Pawn p)=>p?.genes?.GetGene(Gene) as Gene_HalfIllithid;
  public static bool Is(Pawn p)=>Of(p)?.Active==true;
  public static bool Suppressed(Pawn p)=>p?.health?.hediffSet?.HasHediff(Suppression)==true;
  // 服药：刷新3天药效；化形不到50%时成为半夺心魔。
  public static void Dose(Pawn p){
   var old=p.health.hediffSet.GetFirstHediffOfDef(Suppression);if(old!=null)p.health.RemoveHediff(old);
   p.health.AddHediff(Suppression);
   var t=Tadpoles.Of(p);
   if(t!=null && t.Severity<FreezeLimit && p.genes!=null && Of(p)==null){
    p.genes.AddGene(Gene,true);
    if(p.Faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("半夺心魔",p.LabelShort+"服下抑灵剂，脑中的蝌蚪被压制住了。"+p.LabelShort+"保住了自我，却得到了夺心魔的一部分力量：能读心、能吞噬大脑，每吞噬一个大脑领悟一项灵能。\n\n要一直服药：断药3天后化形会恢复；每吞噬一个大脑也有25%概率让抑制失效。",LetterDefOf.NeutralEvent,p);
   }
  }
  public static void Fail(Pawn p){
   var h=p.health.hediffSet.GetFirstHediffOfDef(Suppression);if(h!=null)p.health.RemoveHediff(h);
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"吞下的大脑冲破了抑灵剂的压制：脑中的蝌蚪又开始生长了。",p,MessageTypeDefOf.NegativeEvent,false);
  }
 }
 public partial class Hediff_Tadpole {
  partial void CheckFrozen(ref bool frozen){frozen=Severity<HalfIllithid.FreezeLimit && HalfIllithid.Suppressed(pawn);}
 }
 public static partial class MindFlayerHooks {
  static partial void OnHalfIllithidDevoured(Pawn caster,Pawn victim,bool humanlike){
   var g=HalfIllithid.Of(caster);if(g==null || !g.Active || !humanlike)return;
   g.Unlock();
   if(HalfIllithid.Suppressed(caster) && Rand.Chance(HalfIllithid.FailChance))HalfIllithid.Fail(caster);
  }
  static partial void CheckWitnessImmunity(Pawn p,ref bool immune){immune=HalfIllithid.Is(p);}
 }
 public class Gene_HalfIllithid : Gene {
  public int unlocked;int ticks;
  public override void PostAdd(){base.PostAdd();SyncPowers();}
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<GenDate.TicksPerHour)return;ticks=0;CheckHost();}
  // 蝌蚪被摘除（或死掉）后，半夺心魔的力量随之消失。
  public bool CheckHost(){
   if(pawn==null || pawn.Dead || Tadpoles.Of(pawn)!=null)return false;
   if(pawn.Faction==Faction.OfPlayer)Messages.Message(pawn.LabelShort+"脑中的蝌蚪没了，半夺心魔的力量也随之消散。",pawn,MessageTypeDefOf.NeutralEvent,false);
   pawn.genes.RemoveGene(this);return true;
  }
  public void SyncPowers(){if(pawn?.abilities==null)return;for(int i=0;i<Math.Min(unlocked,HalfIllithid.MaxPowers);i++){var def=DefDatabase<AbilityDef>.GetNamed(HalfIllithid.Unlocks[i]);if(pawn.abilities.GetAbility(def)==null)pawn.abilities.GainAbility(def);}}
  public AbilityDef Unlock(){
   if(unlocked>=HalfIllithid.MaxPowers)return null;
   var def=DefDatabase<AbilityDef>.GetNamed(HalfIllithid.Unlocks[unlocked]);unlocked++;SyncPowers();
   if(pawn.Faction==Faction.OfPlayer)Messages.Message(pawn.LabelShort+"从吞下的大脑里领悟了"+def.label+"。",pawn,MessageTypeDefOf.PositiveEvent,false);
   return def;
  }
  public override void PostRemove(){
   if(pawn?.abilities!=null)foreach(var name in HalfIllithid.Unlocks){var def=DefDatabase<AbilityDef>.GetNamed(name);if(pawn.abilities.GetAbility(def)!=null && !pawn.genes.GenesListForReading.Any(g=>g!=this && g.Active && g.def.abilities?.Contains(def)==true))pawn.abilities.RemoveAbility(def);}
   base.PostRemove();
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref unlocked,"halfIllithidUnlocked",0);if(Scribe.mode==LoadSaveMode.PostLoadInit)unlocked=Mathf.Clamp(unlocked,0,HalfIllithid.MaxPowers);}
 }
 public class IngestionOutcomeDoer_PsionicSuppress : IngestionOutcomeDoer {
  protected override void DoIngestionOutcomeSpecial(Pawn pawn,Thing ingested,int ingestedCount)=>HalfIllithid.Dose(pawn);
 }
}
