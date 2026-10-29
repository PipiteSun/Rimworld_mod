using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.45.0 魔法物品的战斗效果：魔化兵器命中、圣光冠环/殉道者念珠/接箭手套/护佑誓约、命中率与射程、黑暗、失血、减速与疾病免疫、
 // 法术转向与灵能兜帽、圣甲虫、龙杀手、霜寒之刃防火、火球项链殉爆、匠神齿轮防 EMP、欢愉印记心情上限、诅咒印记的心情。
 public class MagicWeaponExtension : DefModExtension {
  public float extraDamage,igniteChance,chill,dragonFactor=1f;public DamageDef extraDamageDef;public bool toggle,vorpal,fireproofHolder;public int deflectBreakTicks;
 }
 public class CompProperties_MagicWeapon : CompProperties {public CompProperties_MagicWeapon(){compClass=typeof(CompMagicWeapon);}}
 public class CompMagicWeapon : ThingComp {
  public bool active=true;
  public override string CompInspectStringExtra()=>"燃火："+(active?"开启":"关闭");
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref active,"lpFlameActive",true);}
 }
 public static partial class MagicEffects {
  public static bool transferring;
  public static bool IsDragon(Pawn p)=>Dragons.Is(p); // 1.52.0：统一成 Dragons.Is（力能龙裔、五色龙、非人形的“Dragon”生物；龙教徒之类的人形兵种不算）
  public static float DragonFactor(ThingDef weapon,Thing target){var e=weapon?.GetModExtension<MagicWeaponExtension>();return e!=null && e.dragonFactor>1f && target is Pawn p && IsDragon(p)?e.dragonFactor:1f;}
  public static void OnWeaponHit(Pawn caster,ThingWithComps weapon,MagicWeaponExtension ext,Thing target,DamageWorker.DamageResult hit){
   if(caster==null || target==null || target.Destroyed)return;
   var victim=target as Pawn;
   if(ext.extraDamage>0 && (!ext.toggle || weapon.GetComp<CompMagicWeapon>()?.active!=false) && !(victim!=null && victim.Dead)){
    target.TakeDamage(new DamageInfo(ext.extraDamageDef??DamageDefOf.Flame,ext.extraDamage,0f,-1f,caster,null,weapon.def));
    if(ext.igniteChance>0 && !target.Destroyed && Rand.Chance(ext.igniteChance))FireUtility.TryAttachFire(target,.4f,caster);
   }
   if(victim==null || victim.Dead)return;
   if(ext.chill>0)FleshImplants.Chill(victim,ext.chill);
   if(ext.deflectBreakTicks>0 && IsDragon(victim))Items.Give(victim,"LP_DeflectSuppressed",ext.deflectBreakTicks);
   if(ext.vorpal)TryBehead(caster,victim,hit);
  }
  // ---------- 锐锋剑 ----------
  public static BodyPartRecord Head(Pawn p)=>p?.health?.hediffSet?.GetNotMissingParts().FirstOrDefault(b=>b.def==BodyPartDefOf.Head);
  public static bool UnderHead(BodyPartRecord part,BodyPartRecord head){for(var b=part;b!=null;b=b.parent)if(b==head)return true;return false;}
  public static string BeheadBlocked(Pawn victim,bool hitHead,float health){
   if(victim==null || victim.Dead)return "目标已死。";
   if(Head(victim)==null)return "没有头。";
   if(victim.BodySize>=2f)return "体型太大。";
   if(Items.IsLich(victim))return "巫妖不靠头活着。";
   if(!hitHead)return "没有砍中头部。";
   if(health>=.25f)return "生命还高于25%。";
   return null;
  }
  public static bool TryBehead(Pawn attacker,Pawn victim,DamageWorker.DamageResult hit){
   var head=Head(victim);bool hitHead=head!=null && hit?.parts!=null && hit.parts.Any(b=>UnderHead(b,head));
   if(BeheadBlocked(victim,hitHead,victim?.health?.summaryHealth?.SummaryHealthPercent??1f)!=null)return false;
   return Behead(attacker,victim);
  }
  public static bool Behead(Pawn attacker,Pawn victim){
   var head=Head(victim);if(head==null)return false;
   bool show=victim.Spawned;var map=victim.Map;var pos=victim.DrawPos;
   victim.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,victim,head),head);
   if(!victim.Dead)victim.Kill(new DamageInfo(DamageDefOf.Cut,99f,1f,-1f,attacker));
   if(show && map!=null)MoteMaker.ThrowText(pos,map,"锐锋斩首！",new Color(1f,.4f,.35f));
   if(attacker?.Faction==Faction.OfPlayer || victim.Faction==Faction.OfPlayer)Messages.Message((attacker?.LabelShort??"锐锋剑")+"一剑斩下了"+victim.LabelShort+"的头颅。",victim.Corpse!=null?new LookTargets(victim.Corpse):LookTargets.Invalid,MessageTypeDefOf.NeutralEvent,false);
   return true;
  }
  // ---------- 防御 ----------
  public static bool HaloBlocks(Pawn p,float roll){
   var halo=Items.Worn(p,"LP_HaloOfLight");var c=halo?.GetComp<CompMagicCharges>();if(c==null || roll>=MagicItems.HaloChance || !c.TryUse())return false;
   Items.Mote(p,"圣光挡下（今天还剩"+c.charges+"次）",new Color(1f,.92f,.55f));return true;
  }
  public static bool Snares(Pawn p,DamageInfo dinfo,float roll){
   if(Items.Worn(p,"LP_GlovesMissileSnaring")==null || dinfo.Def.isExplosive || dinfo.Instigator is Building || dinfo.Weapon?.building!=null || p.Downed || !MagicItems.OneHanded(p) || roll>=MagicItems.SnareChance)return false;
   Items.Mote(p,"徒手接住！",new Color(.85f,.9f,1f));return true;
  }
  // 殉道者念珠：护盾吸收；打碎后变成碎念珠放进背包。返回 true 表示整次伤害都被吸收。
  public static bool MartyrAbsorb(Pawn p,ref DamageInfo dinfo){
   var beads=Items.Worn(p,"LP_MartyrsBeads");var c=beads?.GetComp<CompMagicCharges>();if(c==null || c.Charges<=0 || dinfo.Amount<=0)return false;
   int cost=Mathf.CeilToInt(dinfo.Amount);bool all=cost<=c.charges;
   if(all)c.charges-=cost;else{dinfo.SetAmount(dinfo.Amount-c.charges);c.charges=0;}
   Items.Mote(p,all?"念珠护盾 "+c.charges:"念珠护盾碎裂！",new Color(1f,.6f,.6f));
   if(c.charges<=0)BreakBeads(p,beads);
   return all;
  }
  public static void BreakBeads(Pawn p,Apparel beads){
   p.apparel.Remove(beads);beads.Destroy();
   var broken=ThingMaker.MakeThing(Items.Thing("LP_MartyrsBeadsBroken"));
   if(p.inventory==null || !p.inventory.innerContainer.TryAdd(broken)){if(p.MapHeld!=null)GenPlace.TryPlaceThing(broken,p.PositionHeld,p.MapHeld,ThingPlaceMode.Near);}
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"的殉道者念珠护盾被打碎了，需要在炼金桌重新祝圣。",p,MessageTypeDefOf.NegativeEvent,false);
  }
  // 护佑誓约：甲戒佩戴者受到的伤害一半转给同地图的乙戒佩戴者。返回转走的伤害。
  public static float WardingTransfer(Pawn p,ref DamageInfo dinfo){
   var partner=WardingBond.PartnerWearer(p);if(partner==null || dinfo.Amount<=0)return 0f;
   float half=dinfo.Amount/2f;dinfo.SetAmount(dinfo.Amount-half);
   transferring=true;try{partner.TakeDamage(new DamageInfo(dinfo.Def,half,dinfo.ArmorPenetrationInt,-1f,null));}finally{transferring=false;}
   Items.Mote(partner,"誓约分担",new Color(.8f,.85f,1f));
   return half;
  }
  // ---------- 法术转向 / 灵能兜帽 ----------
  static readonly string[] psychicWords={"Psych","Psion","Psy","Mind","Dominat","Fear","Gaze","Bind","Blast","Brain"};
  public static bool Psychic(AbilityDef d)=>d!=null && (d.IsPsycast || psychicWords.Any(w=>d.defName.IndexOf(w,StringComparison.OrdinalIgnoreCase)>=0));
  public static bool HoodProtects(Pawn target,float roll){
   if(target?.Map==null || roll>=MagicItems.HoodChance)return false;
   var def=Items.Thing("LP_PsychicHood");
   foreach(var o in target.Map.mapPawns.AllPawnsSpawned)if(!o.Dead && !o.HostileTo(target) && o.Position.InHorDistOf(target.Position,MagicItems.HoodRadius) && Items.Worn(o,def)!=null)return true;
   return false;
  }
  public static bool TurnSpell(Pawn target){var ring=Items.Worn(target,"LP_RingSpellTurning");return ring?.GetComp<CompMagicCharges>()?.TryUse()==true;}
  // ---------- 状态拦截 ----------
  public static bool BlocksHediff(Pawn p,HediffDef d){
   if(d==null || p==null)return false;
   if(MagicItems.FreeActionHediffs.Contains(d.defName) && Items.Worn(p,"LP_RingFreeAction")!=null)return true;
   bool lung=d.defName.IndexOf("LungRot",StringComparison.OrdinalIgnoreCase)>=0;
   if(lung && Items.Worn(p,"LP_NecklaceAdaptation")!=null)return true;
   if((ItemWondrous.Disease(d) || d==HediffDefOf.ToxicBuildup || lung) && Items.Worn(p,"LP_MarkDecay")!=null)return true;
   if(d==MindFlayerPowers.DominatedHediff && Hediff_PuritySeals.TryBreak(p,"支配"))return true;
   return false;
  }
 }
 // 魔化兵器近战命中。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class MagicWeaponHit {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target,DamageWorker.DamageResult __result){
   var weapon=__instance.EquipmentSource;var ext=weapon?.def.GetModExtension<MagicWeaponExtension>();
   if(ext==null || target.Thing==null || __result==null || __result.totalDamageDealt<=0)return;
   MagicEffects.OnWeaponHit(__instance.CasterPawn,weapon,ext,target.Thing,__result);
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class MagicDefense {
  static bool Prefix(Pawn __instance,ref DamageInfo __0,ref bool __1){
   var p=__instance;if(__1 || p?.apparel==null || __0.Def==null || MagicEffects.transferring)return true;
   bool attack=ForceDragon.IsAttack(p,__0);
   if(attack && MagicEffects.HaloBlocks(p,Rand.Value)){__1=true;return false;}
   if(attack && ForceDragon.IsRanged(__0) && MagicEffects.Snares(p,__0,Rand.Value)){__1=true;return false;}
   if(attack && MagicEffects.MartyrAbsorb(p,ref __0)){__1=true;return false;}
   if(__0.Def.harmsHealth)MagicEffects.WardingTransfer(p,ref __0);
   return true;
  }
 }
 // 命中率：箭术护腕、微观之眼、战嚎图腾、祈祷祝福；夜视护目镜不受黑暗影响。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class MagicShotReport {
  static readonly AccessTools.StructFieldRef<ShotReport,float> shooter=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromShooterAndDist");
  static readonly AccessTools.StructFieldRef<ShotReport,float> dark=AccessTools.StructFieldRefAccess<ShotReport,float>("offsetFromDarkness");
  public static float Factor(Pawn p){
   if(p?.apparel==null)return 1f;float f=1f;
   if(Items.Worn(p,Bracers.Def)!=null && MagicItems.HoldsBow(p))f*=1.1f;
   if(Items.Worn(p,MinuteEyes.Def)!=null)f*=.95f;
   if(Items.Worn(p,Totem.Def)!=null)f*=.9f;
   if(Items.Has(p,Blessed.Def))f*=1.05f;
   return f;
  }
  // 1.58.6：每发子弹、每次瞄准悬停都要查，用静态 DefRef。
  static readonly DefRef<ThingDef> Bracers=new DefRef<ThingDef>("LP_BracersArchery"),MinuteEyes=new DefRef<ThingDef>("LP_EyesMinuteSeeing"),Totem=new DefRef<ThingDef>("LP_WaaaghTotem");
  static readonly DefRef<HediffDef> Blessed=new DefRef<HediffDef>("LP_MI_Blessed");
  static void Postfix(Thing caster,ref ShotReport __result){
   if(!(caster is Pawn p) || p.apparel==null)return;
   float f=Factor(p);if(f!=1f)shooter(ref __result)=Mathf.Min(.99f,shooter(ref __result)*f);
   if(dark(ref __result)<0 && Items.Worn(p,MagicItems.NightGoggles.Def)!=null)dark(ref __result)=0;
  }
 }
 // 鹰眼：白天远程射程+3。
 [HarmonyPatch(typeof(VerbProperties),nameof(VerbProperties.AdjustedRange))]
 public static class EagleEyesRange {
  static void Postfix(VerbProperties __instance,Verb ownerVerb,Thing attacker,ref float __result){
   if(!(attacker is Pawn p) || __instance.IsMeleeAttack || __instance.range<=1.5f || ownerVerb is Verb_CastAbility || p.apparel==null || p.Map==null)return;
   if(Items.Worn(p,Eagle.Def)!=null && !MagicItems.Night(p.Map))__result+=MagicItems.EagleRange;
  }
  static readonly DefRef<ThingDef> Eagle=new DefRef<ThingDef>("LP_EyesEagle"); // 1.58.6：射程每次查询都会走到
 }
 // 夜视护目镜：工作速度等不受黑暗影响。
 [HarmonyPatch(typeof(StatPart_Glow),"ActiveFor")]
 public static class NightGogglesGlow {static void Postfix(Thing t,ref bool __result){if(__result && t is Pawn p && Items.Worn(p,MagicItems.NightGoggles.Def)!=null)__result=false;}}
 // 伤口闭合护符：失血停在90%。
 [HarmonyPatch(typeof(Hediff),nameof(Hediff.Severity),MethodType.Setter)]
 public static class WoundClosureBloodLoss {
  static void Prefix(Hediff __instance,ref float value){if(value>MagicItems.BloodLossCap && __instance.def==HediffDefOf.BloodLoss && Items.Worn(__instance.pawn,"LP_PeriaptWoundClosure")!=null)value=MagicItems.BloodLossCap;}
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.AddHediff),new[]{typeof(Hediff),typeof(BodyPartRecord),typeof(DamageInfo?),typeof(DamageWorker.DamageResult)})]
 public static class MagicHediffBlock {
  static bool Prefix(Hediff __0,Pawn ___pawn)=>__0==null || ___pawn==null || !MagicEffects.BlocksHediff(___pawn,__0.def);
 }
 // 净化封印：每天一次挣脱恐惧或支配。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class PuritySealMentalState {
  static bool Prefix(MentalStateDef stateDef,Pawn ___pawn,ref bool __result){
   if(stateDef!=MentalStateDefOf.PanicFlee && stateDef!=MindFlayerPowers.Dominated)return true;
   if(!Hediff_PuritySeals.TryBreak(___pawn,stateDef==MentalStateDefOf.PanicFlee?"恐惧":"支配"))return true;
   __result=false;return false;
  }
 }
 // 法术转向、灵能兜帽。
 [HarmonyPatch(typeof(Ability),nameof(Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class MagicAbilityGuard {
  static bool Prefix(Ability __instance,ref LocalTargetInfo __0,ref bool __result){
   var caster=__instance.pawn;var target=__0.Pawn;
   if(caster==null || target==null || target==caster || target.Dead || !caster.HostileTo(target))return true;
   if(MagicEffects.Psychic(__instance.def) && MagicEffects.HoodProtects(target,Rand.Value)){
    Items.Mote(target,"灵能兜帽：法术落空",new Color(.75f,.6f,1f));__instance.StartCooldown(__instance.def.cooldownTicksRange.RandomInRange);__result=true;return false;
   }
   if(__instance.def.hostile && MagicEffects.TurnSpell(target)){Items.Mote(target,"法术转向！",new Color(.7f,.95f,1f));__0=new LocalTargetInfo(caster);}
   return true;
  }
 }
 // 传心头盔、星界护符、念珠：冷却规则（夺心魔读心无冷却，吉斯洋基星界跳跃冷却减半，祈祷两种用法共用冷却）。
 [HarmonyPatch(typeof(Ability),nameof(Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class MagicAbilityCooldowns {
  static void Postfix(Ability __instance,bool __result){
   if(!__result)return;var p=__instance.pawn;var name=__instance.def.defName;
   if(name=="LP_MI_ReadMind" && MindFlayer.IsMindFlayer(p))__instance.ResetCooldown();
   if(name=="LP_AstralStep" && Items.Worn(p,"LP_AmuletPlanes")!=null && __instance.CooldownTicksRemaining>0)__instance.StartCooldown(__instance.CooldownTicksRemaining/2);
   var share=__instance.EffectComps?.OfType<CompMagicAbility>().FirstOrDefault()?.Props.shareCooldown;
   if(share!=null){var other=p?.abilities?.GetAbility(DefDatabase<AbilityDef>.GetNamedSilentFail(share));if(other!=null && other.CooldownTicksRemaining<__instance.CooldownTicksRemaining)other.StartCooldown(__instance.CooldownTicksRemaining);}
  }
 }
 // 防探测护符：夺心魔（和传心头盔）读心落空。
 [HarmonyPatch(typeof(MindFlayerPowers),nameof(MindFlayerPowers.ReadMind))]
 public static class ProofAgainstReadMind {
  static bool Prefix(Pawn caster,Pawn target,ref string __result){
   if(!MagicItems.HidesFromDetection(target))return true;
   __result="";Items.Mote(target,"读心落空",new Color(.7f,.7f,.75f));
   if(caster?.Faction==Faction.OfPlayer)Messages.Message(caster.LabelShort+"的读心落空了："+target.LabelShort+"的心思像被一层铅隔开。",target,MessageTypeDefOf.NeutralEvent,false);
   return false;
  }
 }
 // 霜寒之刃：持有者不会着火。火球项链：佩戴者着火时10%全部炸开。
 [HarmonyPatch(typeof(FireUtility),nameof(FireUtility.TryAttachFire))]
 public static class MagicFireAttach {
  static bool Prefix(Thing t)=>!(t is Pawn p && MagicItems.Fireproof(p));
  static void Postfix(Thing t){if(t is Pawn p && p.Spawned && p.IsBurning() && Rand.Chance(MagicItems.FireballBurstChance))MagicEffects.BurstNecklace(p);}
 }
 // 匠神齿轮：不被 EMP 震晕。
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.Notify_DamageApplied))]
 public static class CogEmpImmunity {static bool Prefix(Pawn ___parentPawn,DamageInfo __0)=>__0.Def!=DamageDefOf.EMP || Items.Worn(___parentPawn,"LP_CogOfMakers")==null;}
 // 欢愉印记：心情上限90%。
 [HarmonyPatch(typeof(Need_Mood),"get_CurInstantLevel")]
 public static class RaptureMoodCap {static void Postfix(Need_Mood __instance,Pawn ___pawn,ref float __result){if(__result>.9f && Items.Worn(___pawn,"LP_MarkRapture")!=null && !Items.IsLich(___pawn))__result=.9f;}}
 // 审判徽记：同盟好感增加×1.25；猎巫狂热分子不来。
 [HarmonyPatch(typeof(Faction),nameof(Faction.TryAffectGoodwillWith))]
 public static class InquisitorGoodwill {
  static void Prefix(Faction __instance,Faction other,ref int goodwillChange){
   if(goodwillChange<=0 || other==null)return;var a=MagicItems.Alliance;if(a==null)return;
   if(!((__instance==a && other.IsPlayer) || (__instance.IsPlayer && other==a)))return;
   if(MagicItems.AnyColonistWears(null,"LP_InquisitorSeal"))goodwillChange=Mathf.RoundToInt(goodwillChange*1.25f);
  }
 }
 [HarmonyPatch(typeof(IncidentWorker_ZealotRaid),nameof(IncidentWorker_ZealotRaid.Allowed))]
 public static class InquisitorWardsZealots {static void Postfix(Map map,ref bool __result){if(__result && MagicItems.AnyColonistWears(map,"LP_InquisitorSeal"))__result=false;}}
 // 无尽水瓶：周围3格植物长得快。
 public static class Decanters {
  static readonly Dictionary<int,HashSet<IntVec3>> cells=new Dictionary<int,HashSet<IntVec3>>();
  public const float GrowthFactor=1.2f,Radius=3f;
  public static void Refresh(){
   cells.Clear();var def=Items.Thing("LP_DecanterEndlessWater");if(def==null || Find.Maps==null)return;
   foreach(var map in Find.Maps){var list=map.listerThings.ThingsOfDef(def);if(list.Count==0)continue;var set=new HashSet<IntVec3>();foreach(var t in list)foreach(var c in GenRadial.RadialCellsAround(t.Position,Radius,true))set.Add(c);cells[map.uniqueID]=set;}
  }
  public static bool Irrigated(Map map,IntVec3 c)=>map!=null && cells.TryGetValue(map.uniqueID,out var s) && s.Contains(c);
 }
 [HarmonyPatch(typeof(Plant),"get_GrowthRate")]
 public static class DecanterGrowth {static void Postfix(Plant __instance,ref float __result){if(__result>0 && __instance.Spawned && Decanters.Irrigated(__instance.Map,__instance.Position))__result*=Decanters.GrowthFactor;}}
 // 净化封印状态：记录每天的挣脱。
 public class Hediff_PuritySeals : HediffWithComps {
  public int lastBreakDay=-1;
  public static bool TryBreak(Pawn p,string what){
   var h=p?.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_PuritySeals")) as Hediff_PuritySeals;if(h==null)return false;
   int day=Find.TickManager.TicksGame/GenDate.TicksPerDay;if(h.lastBreakDay==day)return false;h.lastBreakDay=day;
   Items.Mote(p,"净化封印：挣脱"+what,new Color(1f,.95f,.7f));return true;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref lastBreakDay,"lpLastBreakDay",-1);}
 }
 // 鲜血印记：3天不打架就嗜血（巫妖例外）。
 public class ThoughtWorker_Bloodthirst : ThoughtWorker {
  public static int Stage(Pawn p){
   var mark=Items.Worn(p,"LP_MarkBlood");if(mark==null || Items.IsLich(p))return -1;
   int since=Math.Max(p.mindState?.lastAttackTargetTick??-99999,mark.GetComp<CompCursed>()?.wornSince??0);
   float days=(Find.TickManager.TicksGame-since)/(float)GenDate.TicksPerDay;
   return days<3f?-1:days<5f?0:days<7f?1:2;
  }
  protected override ThoughtState CurrentStateInternal(Pawn p){int s=Stage(p);return s<0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(s);}
 }
 // 腐朽印记：身边5格有人戴着（非巫妖）时心情-2。
 public class ThoughtWorker_DecayAura : ThoughtWorker {
  static readonly Dictionary<int,(int tick,List<IntVec3> spots)> census=new Dictionary<int,(int,List<IntVec3>)>();
  public static bool Near(Pawn viewer){
   if(viewer?.Map==null)return false;var map=viewer.Map;int now=Find.TickManager.TicksGame;
   if(!census.TryGetValue(map.uniqueID,out var c) || now-c.tick>=250 || now<c.tick){var def=Items.Thing("LP_MarkDecay");c=(now,map.mapPawns.AllPawnsSpawned.Where(p=>Items.Worn(p,def)!=null && !Items.IsLich(p)).Select(p=>p.Position).ToList());census[map.uniqueID]=c;}
   if(c.spots.Count==0 || Items.Worn(viewer,"LP_MarkDecay")!=null)return false;
   return c.spots.Any(s=>s.InHorDistOf(viewer.Position,5f));
  }
  public static void Reset()=>census.Clear();
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.Spawned && p.RaceProps.Humanlike && Near(p)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }
}
