using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.17.0 夺心魔：脑髓资源、吞噬大脑、心灵震爆、群脑联络。
 // 吞噬的"杀死方式"是给大脑加 MissingBodyPart：巨魔复活检查（大脑缺失不复活）和巫妖完整尸体献祭（缺失部位降低价值）自动生效。
 public static class MindFlayer {
  public const float Max=100f,DailyDrain=8f,Low=30f,Starving=10f,HumanBase=20f,PerIntellect=3f,HumanCap=80f,AnimalMin=3f,AnimalMax=10f,CorpseFactor=1f/3f,XpShare=.2f,RageChance=.15f;
  public const float BlastRange=9f,BlastHalfAngle=35f,WitnessRange=12f;
  public const int CorpseFreshTicks=15000,BlastStunTicks=240,Goodwill=-15;
  static GeneDef cerebrum,hive;static XenotypeDef xenotype;
  static HediffDef hunger,taste,hiveLink,blasted;static ThoughtDef witnessed,hiveBroken;static HistoryEventDef devoured;
  public static GeneDef CerebrumGene=>cerebrum??(cerebrum=DefDatabase<GeneDef>.GetNamedSilentFail("LP_Cerebrum"));
  public static GeneDef HiveGene=>hive??(hive=DefDatabase<GeneDef>.GetNamedSilentFail("LP_HiveLink"));
  public static XenotypeDef Xenotype=>xenotype??(xenotype=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_MindFlayer"));
  public static HediffDef Hunger=>hunger??(hunger=DefDatabase<HediffDef>.GetNamed("LP_CerebrumHunger"));
  public static HediffDef Taste=>taste??(taste=DefDatabase<HediffDef>.GetNamed("LP_UndeadTaste"));
  public static HediffDef HiveLinkHediff=>hiveLink??(hiveLink=DefDatabase<HediffDef>.GetNamed("LP_HiveLink"));
  public static HediffDef Blasted=>blasted??(blasted=DefDatabase<HediffDef>.GetNamed("LP_MindBlasted"));
  public static ThoughtDef Witnessed=>witnessed??(witnessed=DefDatabase<ThoughtDef>.GetNamed("LP_WitnessedBrainDevour"));
  public static ThoughtDef HiveBroken=>hiveBroken??(hiveBroken=DefDatabase<ThoughtDef>.GetNamed("LP_HiveBroken"));
  public static HistoryEventDef Devoured=>devoured??(devoured=DefDatabase<HistoryEventDef>.GetNamed("LP_DevouredBrain"));
  public static Gene_Cerebrum Cerebrum(Pawn p){var g=p?.genes?.GetGene(CerebrumGene) as Gene_Cerebrum;return g!=null && g.Active?g:null;}
  public static bool IsMindFlayer(Pawn p)=>Cerebrum(p)!=null;
  public static bool HasHive(Pawn p)=>HiveGene!=null && p?.genes?.HasActiveGene(HiveGene)==true;
  public static float Points(Pawn p)=>(Cerebrum(p)?.Value??0)*Max;
  public static void Gain(Pawn p,float points){var g=Cerebrum(p);if(g!=null){g.Value=Mathf.Min(g.Max,g.Value+points/Max);g.SyncHunger();}}
  public static bool Pay(Pawn p,float points){var g=Cerebrum(p);if(g==null || g.Value*Max+.0001f<points)return false;g.Value=Mathf.Max(0,g.Value-points/Max);g.SyncHunger();return true;}
  public static BodyPartRecord Brain(Pawn p)=>p?.health?.hediffSet?.GetBrain();
  public static bool PsychicallyDeaf(Pawn p)=>p.GetStatValue(StatDefOf.PsychicSensitivity)<=.001f;
  public static Pawn Victim(Thing t)=>t as Pawn ?? (t as Corpse)?.InnerPawn;
  public static void Mote(Thing t,string text,Color color){if(t!=null && t.SpawnedOrAnyParentSpawned && t.MapHeld!=null)MoteMaker.ThrowText(t.DrawPosHeld??t.PositionHeld.ToVector3Shifted(),t.MapHeld,text,color);}

  // ---------- 吞噬大脑 ----------
  // 不能吞噬的原因；null 表示可以下口（巫妖、死灵造物可以下口，但会失败）。
  public static string CannotDevour(Pawn caster,Thing t){
   var p=Victim(t);
   if(p==null)return "需要倒地的生物或新鲜的尸体。";
   if(p==caster)return "不能吞噬自己。";
   if(p.RaceProps.IsMechanoid)return "机械体没有大脑。";
   if(!p.RaceProps.IsFlesh || Brain(p)==null)return "目标没有可吞噬的大脑。";
   // 1.31.1：尸体只要求新鲜（未开始腐烂）、人形、有大脑；不再限6小时，心灵失聪的尸体也能吞噬。
   if(t is Corpse c){if(c.GetRotStage()!=RotStage.Fresh)return "尸体已不新鲜（开始腐烂）。";if(!p.RaceProps.Humanlike)return "动物尸体的大脑不值得吞噬。";return null;}
   if(p.Dead || !p.Downed)return "目标必须倒地。";
   if(!Necro.Undead(p) && PsychicallyDeaf(p))return "心灵失聪者的大脑无法吞噬。";
   return null;
  }
  // 吞噬获得的脑髓。人形：(20+3×智识)×心灵敏感度，最多80；动物3～10；新鲜尸体三分之一。
  public static float Value(Pawn p,bool corpse){
   float v;
   if(p.RaceProps.Humanlike){int intel=p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level??0;v=Mathf.Min(HumanCap,(HumanBase+PerIntellect*intel)*Mathf.Max(0,p.GetStatValue(StatDefOf.PsychicSensitivity)));}
   else v=Mathf.Clamp(AnimalMin+3.5f*p.BodySize,AnimalMin,AnimalMax);
   // 1.28.0：伊尔斯维离体后“脑髓烧尽”的大脑多 60。
   if(p.health?.hediffSet?.hediffs.OfType<Hediff_FormerVessel>().Any(h=>h.deity?.key=="Ilsvi")==true)v+=TwilightSkills.IlsviDevourBonus;
   return corpse?v*CorpseFactor:v;
  }
  // 原版单次 Learn 有上限：分块累加，保证大量经验也能按实际数值到账。
  public static void GrantXp(Pawn p,SkillDef skill,float xp){var s=p?.skills?.GetSkill(skill);if(s==null || s.TotallyDisabled)return;for(int i=0;i<1000 && xp>.01f;i++){float chunk=Mathf.Min(xp,500f);s.Learn(chunk,true,true);xp-=chunk;}}
  // 自身学到的经验（不含基因天赋带来的等级加成）。
  public static float OwnXp(SkillRecord s){float sum=s.xpSinceLastLevel;for(int i=0;i<s.levelInt;i++)sum+=SkillRecord.XpRequiredToLevelUpFrom(i);return sum;}
  public static SkillRecord BestSkill(Pawn p)=>p?.skills?.skills.Where(s=>!s.TotallyDisabled && OwnXp(s)>0).OrderByDescending(OwnXp).FirstOrDefault();
  // 返回获得的脑髓；-1 表示不能吞噬；0 表示失败（巫妖/死灵造物）。
  public static float Devour(Pawn caster,Thing t){
   if(CannotDevour(caster,t)!=null)return -1;
   var p=Victim(t);bool corpse=t is Corpse;var map=t.MapHeld;
   if(Necro.Undead(p)){
    var old=caster.health.hediffSet.GetFirstHediffOfDef(Taste);if(old!=null)caster.health.RemoveHediff(old);
    caster.health.AddHediff(Taste);Mote(caster,"亡灵之味！",new Color(.55f,.65f,.5f));
    if(caster.Faction==Faction.OfPlayer)Messages.Message(caster.LabelShort+"试图吞噬"+p.LabelShort+"的大脑，却只尝到了腐朽的死灵之力。不死生物没有活着的大脑。",caster,MessageTypeDefOf.NegativeEvent,false);
    return 0;
   }
   float value=Value(p,corpse);
   var best=p.RaceProps.Humanlike?BestSkill(p):null;
   if(best!=null)GrantXp(caster,best.def,OwnXp(best)*XpShare);
   var faction=p.Faction;bool humanlike=p.RaceProps.Humanlike;
   var witnesses=humanlike && map!=null?Witnesses(caster,p,map):new List<Pawn>();
   var brain=Brain(p);
   // 1.20.0：吞噬活人时施法者就是凶手，我方夺心魔吞噬同盟矮人也记血债（尸体不算）。
   bool debt=!corpse && !p.Dead && BloodDebt.CountsAsDebt(p,caster);
   // 1.44.0：两个头都在的双头巨人只被吃掉一颗大脑——那个头整个毁掉，另一个头还活着（得到孤首）。
   bool ettinSurvives=!corpse && !p.Dead && Ettin.BothHeads(p);
   if(ettinSurvives){var neck=Ettin.Necks(p).FirstOrDefault(k=>Ettin.BrainOf(k)==brain);var head=Ettin.HeadOf(neck);if(head!=null)brain=head;debt=false;}
   p.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,p,brain),brain);
   if(ettinSurvives)Ettin.Sync(p);
   if(!p.Dead && !corpse && !ettinSurvives)p.Kill(null,null);
   if(debt && p.Dead)BloodDebt.Record(p);
   Gain(caster,value);
   foreach(var w in witnesses)w.needs.mood.thoughts.memories.TryGainMemory(Witnessed,caster);
   if(humanlike && caster.Faction==Faction.OfPlayer && faction!=null && !faction.IsPlayer && !faction.Hidden)faction.TryAffectGoodwillWith(Faction.OfPlayer,Goodwill,true,true,Devoured,p.Corpse);
   if(humanlike && ModsConfig.IdeologyActive)Find.HistoryEventsManager.RecordEvent(new HistoryEvent(Devoured,caster.Named(HistoryEventArgsNames.Doer),p.Named(HistoryEventArgsNames.Victim)));
   if(map!=null)FilthMaker.TryMakeFilth(t.PositionHeld,map,ThingDefOf.Filth_Blood,2);
   Mote(caster,"吞噬大脑 +"+value.ToString("0"),new Color(.8f,.55f,.95f));
   MindFlayerHooks.Devoured(caster,p,humanlike);
   return value;
  }
  // 目击者：附近看得见的清醒人形，不含夺心魔和半夺心魔。
  public static List<Pawn> Witnesses(Pawn caster,Pawn victim,Map map){
   var list=new List<Pawn>();var at=caster.PositionHeld;
   foreach(var w in map.mapPawns.AllPawnsSpawned){
    if(w==caster || w==victim || w.Dead || !w.RaceProps.Humanlike || w.needs?.mood==null || !w.Awake() || IsMindFlayer(w) || MindFlayerHooks.ImmuneToWitness(w))continue;
    if(!w.Position.InHorDistOf(at,WitnessRange) || !GenSight.LineOfSight(w.Position,at,map,true))continue;
    list.Add(w);
   }
   return list;
  }

  // ---------- 心灵震爆 ----------
  public static List<IntVec3> ConeCells(Pawn caster,IntVec3 target,float range,float halfAngle){
   var cells=new List<IntVec3>();var map=caster?.Map;if(map==null)return cells;
   var origin=caster.Position;Vector3 dir=(target-origin).ToVector3();if(dir.sqrMagnitude<.01f)dir=caster.Rotation.FacingCell.ToVector3();
   foreach(var c in GenRadial.RadialCellsAround(origin,range,false)){if(!c.InBounds(map) || Vector3.Angle(dir,(c-origin).ToVector3())>halfAngle || !GenSight.LineOfSight(origin,c,map,true))continue;cells.Add(c);}
   return cells;
  }
  // 对一个目标做心智冲击：返回 true 表示击中（未抵抗）。chanceFactor 传给 DNDMind.TryResist。
  public static bool PsychicStrike(Pawn caster,Pawn p,int stunTicks,float chanceFactor=1f){
   if(p==null || p.Dead || p.RaceProps.IsMechanoid || PsychicallyDeaf(p))return false;
   if(DNDMind.TryResist(p,chanceFactor)){Mote(p,"抵抗！",new Color(.7f,.85f,1f));return false;}
   if(Exalted.Count>0 && Exalted.BlastImmune(p))return false; // 1.59.0 先知免疫心灵震爆
   float sens=Mathf.Clamp(p.GetStatValue(StatDefOf.PsychicSensitivity),.25f,2.5f)*Exalted.PsychicDuration(p); // 1.59.0 明察：眩晕减半
   p.stances?.stunner?.StunFor(Mathf.RoundToInt(stunTicks*sens),caster,false,false,false);
   var old=p.health.hediffSet.GetFirstHediffOfDef(Blasted);if(old!=null)p.health.RemoveHediff(old);
   p.health.AddHediff(Blasted);
   return true;
  }
  // 1.22.0：夺心魔穿着触须披风时心灵震爆射程+2。
  public static float BlastRangeFor(Pawn caster)=>BlastRange+(IsMindFlayer(caster) && Items.Worn(caster,"LP_IllithidTendrilCloak")!=null?2f:0f);
  public static int MindBlast(Pawn caster,IntVec3 target){
   var map=caster.Map;int hit=0;var seen=new HashSet<Pawn>();
   foreach(var c in ConeCells(caster,target,BlastRangeFor(caster),BlastHalfAngle)){
    foreach(var t in c.GetThingList(map).ToList())if(t is Pawn p && p!=caster && seen.Add(p) && p.HostileTo(caster) && PsychicStrike(caster,p,BlastStunTicks))hit++;
    FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,.5f);
   }
   return hit;
  }

  // ---------- 脑饥狂乱 ----------
  public static bool StartRage(Pawn p){
   if(!LichMod.Settings.brainHungerRage || p==null || !p.Spawned || p.Dead || p.Downed || p.InMentalState)return false;
   return p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk,"脑饥狂乱",true,true);
  }
 }

 // 后续阶段的挂钩点（上古脑计数、半夺心魔解锁灵能）。
 public static partial class MindFlayerHooks {
  static partial void OnDevoured(Pawn caster,Pawn victim,bool humanlike);
  static partial void CheckWitnessImmunity(Pawn p,ref bool immune);
  public static void Devoured(Pawn caster,Pawn victim,bool humanlike)=>OnDevoured(caster,victim,humanlike);
  public static bool ImmuneToWitness(Pawn p){bool immune=false;CheckWitnessImmunity(p,ref immune);return immune;}
 }

 public class Gene_Cerebrum : Gene_Resource {
  int ticks,hungerTicks;
  public override float InitialResourceMax=>1f;
  public override float MinLevelForAlert=>MindFlayer.Low/MindFlayer.Max;
  protected override Color BarColor=>new Color(.72f,.45f,.85f);
  protected override Color BarHighlightColor=>new Color(.88f,.7f,1f);
  public float Points=>Value*MindFlayer.Max;
  public override void PostAdd(){base.PostAdd();Value=.6f;MindFlayerPowers.SyncBind(pawn);}
  public override void PostRemove(){var h=pawn?.health?.hediffSet.GetFirstHediffOfDef(MindFlayer.Hunger);if(h!=null)pawn.health.RemoveHediff(h);if(pawn?.abilities?.GetAbility(MindFlayerPowers.Bind)!=null)pawn.abilities.RemoveAbility(MindFlayerPowers.Bind);base.PostRemove();}
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(!Active || pawn?.Dead!=false)return;
   if((ticks+=delta)<250)return;
   int elapsed=ticks;ticks=0;Drain(elapsed);
  }
  public void Drain(int elapsed){
   MindFlayerPowers.SyncBind(pawn);
   Value=Mathf.Max(0,Value-MindFlayer.DailyDrain/MindFlayer.Max*elapsed/60000f);
   SyncHunger();
   if(Value>0){hungerTicks=0;return;}
   if((hungerTicks+=elapsed)<GenDate.TicksPerHour)return;
   hungerTicks=0;if(Rand.Chance(MindFlayer.RageChance))MindFlayer.StartRage(pawn);
  }
  // 脑髓低于30加上"脑髓不足"，严重度=1-脑髓/100，分三档（<30、<10、0）。
  public void SyncHunger(){
   if(pawn?.health==null || pawn.Dead)return;
   var h=pawn.health.hediffSet.GetFirstHediffOfDef(MindFlayer.Hunger);float points=Points;
   if(points>=MindFlayer.Low || !Active){if(h!=null)pawn.health.RemoveHediff(h);return;}
   if(h==null)h=pawn.health.AddHediff(MindFlayer.Hunger);
   h.Severity=Mathf.Clamp(1f-points/MindFlayer.Max,.7f,1f);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticks,"cerebrumTicks",0);Scribe_Values.Look(ref hungerTicks,"cerebrumHungerTicks",0);}
 }
 public class Hediff_CerebrumHunger : Hediff {
  public override bool ShouldRemove=>base.ShouldRemove || MindFlayer.Cerebrum(pawn)==null;
  public override string LabelInBrackets=>CurStage?.label+"，脑髓 "+MindFlayer.Points(pawn).ToString("0");
 }
 public class GeneGizmo_CerebrumResource : GeneGizmo_Resource {
  static bool dragging;
  readonly Gene_Resource resource;
  public GeneGizmo_CerebrumResource(Gene_Resource gene,List<IGeneResourceDrain> drainGenes,Color barColor,Color barHighlightColor):base(gene,drainGenes,barColor,barHighlightColor){resource=gene;}
  protected override bool DraggingBar{get=>dragging;set=>dragging=value;}
  protected override bool IsDraggable=>false;
  protected override string Title=>"脑髓";
  protected override string GetTooltip()=>"脑髓："+(resource.Value*MindFlayer.Max).ToString("0")+" / "+(resource.Max*MindFlayer.Max).ToString("0")+"\n每天流失"+MindFlayer.DailyDrain.ToString("0")+"点。低于30虚弱，低于10衰弱，归零陷入脑饥"+(LichMod.Settings.brainHungerRage?"（可能狂乱）":"")+"。\n吞噬倒地的人形生物：（20+3×智识）×心灵敏感度，最多80；动物3～10；未腐烂的新鲜人形尸体三分之一。";
 }

 // ---------- 群脑联络 ----------
 public class Gene_HiveLink : Gene {
  int ticks;
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<250)return;ticks=0;Sync();}
  public int Companions(){
   if(!pawn.Spawned)return 0;int n=0;
   foreach(var other in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))if(other!=pawn && !other.Dead && MindFlayer.HasHive(other) && MindFlayer.IsMindFlayer(other))n++;
   return Math.Min(3,n);
  }
  public void Sync(){
   if(pawn?.health==null || pawn.Dead)return;
   int n=Active && pawn.Faction!=null?Companions():0;var h=pawn.health.hediffSet.GetFirstHediffOfDef(MindFlayer.HiveLinkHediff);
   if(n<=0){if(h!=null)pawn.health.RemoveHediff(h);return;}
   if(h==null)h=pawn.health.AddHediff(MindFlayer.HiveLinkHediff);
   h.Severity=n;
  }
  public override void PostRemove(){var h=pawn?.health?.hediffSet.GetFirstHediffOfDef(MindFlayer.HiveLinkHediff);if(h!=null)pawn.health.RemoveHediff(h);base.PostRemove();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticks,"hiveTicks",0);}
 }
 public class Hediff_HiveLink : Hediff {
  public override bool ShouldRemove=>base.ShouldRemove || !MindFlayer.HasHive(pawn);
 }
 // 夺心魔死亡：同地图本阵营的其他夺心魔获得"群脑断裂"。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class MindFlayerDeath {
  static void Prefix(Pawn __instance,out bool __state){__state=__instance!=null && !__instance.Dead && __instance.Spawned && MindFlayer.HasHive(__instance) && MindFlayer.IsMindFlayer(__instance);}
  static void Postfix(Pawn __instance,bool __state){
   if(!__state || !__instance.Dead)return;
   var map=__instance.MapHeld;if(map==null || __instance.Faction==null)return;
   foreach(var other in map.mapPawns.SpawnedPawnsInFaction(__instance.Faction).ToList())if(other!=__instance && !other.Dead && MindFlayer.HasHive(other))other.needs?.mood?.thoughts?.memories?.TryGainMemory(MindFlayer.HiveBroken);
  }
 }

 // ---------- 能力 ----------
 public class CompProperties_MindFlayerAbility : CompProperties_AbilityEffect {
  public string effect;public float cost;
  public CompProperties_MindFlayerAbility(){compClass=typeof(CompMindFlayerAbility);}
 }
 public partial class CompMindFlayerAbility : CompAbilityEffect {
  public CompProperties_MindFlayerAbility Props=>(CompProperties_MindFlayerAbility)props;
  Pawn Caster=>parent.pawn;
  // 半夺心魔没有脑髓条：灵能免费，只受冷却限制。
  public bool Free=>!MindFlayer.IsMindFlayer(Caster);
  public override bool GizmoDisabled(out string reason){
   if(!Free && Props.cost>0 && MindFlayer.Points(Caster)+.001f<Props.cost){reason="脑髓不足：需要"+Props.cost.ToString("0")+"点，当前"+MindFlayer.Points(Caster).ToString("0")+"点。";return true;}
   return base.GizmoDisabled(out reason);
  }
  public string Invalid(LocalTargetInfo target){
   var map=Caster?.Map;if(map==null)return "施法者不在地图上。";
   switch(Props.effect){
    case "devour":return MindFlayer.CannotDevour(Caster,target.Thing);
    case "mindblast":return target.Cell.IsValid && target.Cell.InBounds(map)?null:"需要地图上的目标位置。";
    default:return InvalidPower(target);
   }
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   string reason=Invalid(target);
   if(reason!=null && throwMessages)Messages.Message(reason,MessageTypeDefOf.RejectInput,false);
   return reason==null && base.Valid(target,throwMessages);
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   if(Invalid(target)!=null)return;
   if(!Free && Props.cost>0 && !MindFlayer.Pay(Caster,Props.cost))return;
   switch(Props.effect){
    case "devour":MindFlayer.Devour(Caster,target.Thing);break;
    case "mindblast":MindFlayer.MindBlast(Caster,target.Cell);break;
    default:ApplyPower(target);break;
   }
  }
  public override void DrawEffectPreview(LocalTargetInfo target){
   if(Caster?.Map==null || !target.Cell.IsValid)return;
   if(Props.effect=="mindblast")GenDraw.DrawFieldEdges(MindFlayer.ConeCells(Caster,target.Cell,MindFlayer.BlastRangeFor(Caster),MindFlayer.BlastHalfAngle));
  }
  public override string ExtraLabelMouseAttachment(LocalTargetInfo target)=>!Free && Props.cost>0?"脑髓 "+Props.cost.ToString("0")+" / "+MindFlayer.Points(Caster).ToString("0"):null;
  partial void InvalidPowerImpl(LocalTargetInfo target,ref string reason);
  partial void ApplyPowerImpl(LocalTargetInfo target);
  string InvalidPower(LocalTargetInfo target){string reason="未知的灵能。";InvalidPowerImpl(target,ref reason);return reason;}
  void ApplyPower(LocalTargetInfo target)=>ApplyPowerImpl(target);
 }
}
