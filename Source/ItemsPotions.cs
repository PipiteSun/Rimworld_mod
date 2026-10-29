using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.22.0 药水：可被药物政策管理的药物类物品；巫妖和死灵造物不能喝，也不会受效果。
 public class IngestionOutcomeDoer_LPPotion : IngestionOutcomeDoer {
  public string effect="give";
  public HediffDef hediff;
  // 1.28.0 强化药水：持续时间倍率。
  public float durationFactor=1f;
  protected override void DoIngestionOutcomeSpecial(Pawn pawn,Thing ingested,int ingestedCount)=>ItemPotions.Apply(pawn,ingested.def,effect,hediff,durationFactor);
 }
 public static class ItemPotions {
  public const float TonicAddictionChance=.1f,TonicHealPerInterval=.125f,MindShieldOffset=.4f,MindShieldCap=.95f,GiantRenderScale=1.1f;
  // 1.58.6：食物搜索里对每个候选食物都会问；结果只取决于定义，按定义记住（原来每次 LINQ 分配枚举器）。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<ThingDef,bool> potions=new System.Collections.Concurrent.ConcurrentDictionary<ThingDef,bool>();
  public static bool IsPotion(ThingDef def)=>def!=null && potions.GetOrAdd(def,d=>d.ingestible?.outcomeDoers?.Any(o=>o is IngestionOutcomeDoer_LPPotion)==true);
  // 返回 false 表示没有生效（巫妖、造物）。
  public static bool Apply(Pawn pawn,ThingDef potion,string effect,HediffDef hediff,float durationFactor=1f){
   if(pawn?.health==null || pawn.Dead)return false;
   if(Items.Undead(pawn)){if(pawn.Faction==Faction.OfPlayer)Messages.Message(pawn.LabelShort+"是不死之躯，"+potion.label+"对它毫无作用。",pawn,MessageTypeDefOf.RejectInput,false);return false;}
   float potency=Items.PotionPotency(pawn,potion);
   switch(effect){
    case "heal":Heal(pawn,potency,false,durationFactor);break;
    case "greaterheal":Heal(pawn,potency,true,durationFactor);break;
    default:if(hediff!=null)Items.Give(pawn,hediff.defName,Ticks(hediff,durationFactor));break;
   }
   SatisfyNeed(pawn,potion);
   return true;
  }
  // 原版只在成瘾性大于0的药物上满足化学需求；巨魔再生剂本身不成瘾（只在药效结束时掷骰），这里手动满足巨魔血瘾。
  public static void SatisfyNeed(Pawn pawn,ThingDef potion){
   var drug=potion?.GetCompProperties<CompProperties_Drug>();var addiction=drug?.chemical?.addictionHediff;if(drug==null || drug.Addictive || addiction?.chemicalNeed==null)return;
   var need=pawn.needs?.TryGetNeed(addiction.chemicalNeed);if(need!=null)need.CurLevel+=drug.needLevelOffset;
   var h=pawn.health.hediffSet.GetFirstHediffOfDef(addiction);if(h!=null)h.Severity+=drug.existingAddictionSeverityOffset;
  }
  // 治疗药水：所有出血立即止住（视为简单包扎），6小时内自然愈合×3（饕餮×6）。强效版另外长回一处缺失的手指或脚趾。
  // 定义里的持续时间 × 倍率；倍率为 1 时用定义原值（-1）。
  public static int Ticks(HediffDef def,float factor){if(factor==1f || def==null)return -1;var d=def.CompProps<HediffCompProperties_Disappears>();return d==null?-1:Mathf.RoundToInt(d.disappearsAfterTicks.max*factor);}
  public static void Heal(Pawn pawn,float potency,bool greater,float durationFactor=1f){
   foreach(var h in pawn.health.hediffSet.hediffs.Where(h=>h.Bleeding).ToList())h.Tended(.5f,1f);
   Items.Give(pawn,"LP_PotionHealing",Ticks(Items.Hediff("LP_PotionHealing"),durationFactor),Mathf.Clamp(potency,1f,2f));
   if(!greater)return;
   var part=Necro.Missing(pawn).FirstOrDefault(SmallPart);
   if(part!=null){pawn.health.RestorePart(part);if(pawn.Faction==Faction.OfPlayer)Messages.Message(pawn.LabelShort+"的"+part.Label+"在强效治疗药水作用下长了回来。",pawn,MessageTypeDefOf.PositiveEvent,false);}
  }
  public static bool SmallPart(BodyPartRecord b)=>b?.def!=null && (b.def.defName.IndexOf("Finger",StringComparison.OrdinalIgnoreCase)>=0 || b.def.defName.IndexOf("Toe",StringComparison.OrdinalIgnoreCase)>=0);
  // 巨魔再生剂结束时，roll<10% 得巨魔血瘾。
  public static bool TryAddict(Pawn p,float roll){
   if(p==null || p.Dead || roll>=TonicAddictionChance)return false;
   var chem=DefDatabase<ChemicalDef>.GetNamedSilentFail("LP_TrollBloodChem");if(chem?.addictionHediff==null)return false;
   if(p.health.hediffSet.HasHediff(chem.addictionHediff))return false;
   p.health.AddHediff(chem.addictionHediff);p.needs?.AddOrRemoveNeedsAsAppropriate();
   if(p.Faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("巨魔血瘾",p.LabelShort+"的身体习惯了巨魔血的再生力，再生剂药效一过就开始渴求它。需要定期喝巨魔再生剂，否则会出现戒断反应；长时间不喝可以戒掉。",LetterDefOf.NegativeEvent,p);
   return true;
  }
 }
 // 巨魔再生剂：饱食度不低于50%时缓慢修复伤口（巨魔的四分之一），不长断肢、不给复活；着火或被火、酸阻断时暂停。
 public class Hediff_TrollTonic : HediffWithComps {
  int accumulated;
  public bool Blocked=>pawn==null || pawn.Dead || (pawn.Spawned && pawn.IsBurning()) || Items.Has(pawn,"LP_TrollBurned") || Items.Has(pawn,"LP_TrollAcid") || (pawn.needs?.food!=null && pawn.needs.food.CurLevelPercentage<.5f);
  public override void TickInterval(int delta){base.TickInterval(delta);if((accumulated+=delta)<250)return;int e=accumulated;accumulated=0;Heal(e);}
  public float Heal(int elapsed){
   if(Blocked)return 0;
   float budget=ItemPotions.TonicHealPerInterval*elapsed/250f,healed=0;
   foreach(var injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h=>!h.IsPermanent()).OrderByDescending(h=>h.BleedRate>0).ThenByDescending(h=>h.Severity).ToList()){float heal=Math.Min(injury.Severity,budget);injury.Heal(heal);budget-=heal;healed+=heal;if(budget<=0)break;}
   return healed;
  }
  public override string LabelInBrackets=>Blocked?"暂停":base.LabelInBrackets;
  public override void PostRemoved(){base.PostRemoved();if(pawn!=null && !pawn.Dead && (this.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear??1)<=0)ItemPotions.TryAddict(pawn,Rand.Value);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref accumulated,"tonicTicks",0);}
 }
 // 心智护盾药水：在其他来源之上 +40%，与种族抗性相加后最多 95%（原本就超过 95% 的不降低）。
 public class StatPart_MindShield : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Items.Has(p,"LP_PotionMindShield") && val<ItemPotions.MindShieldCap)val=Mathf.Min(ItemPotions.MindShieldCap,val+ItemPotions.MindShieldOffset);}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Items.Has(p,"LP_PotionMindShield")?"心智护盾药水：+40%（合计最多95%）":null;
 }
 [StaticConstructorOnStartup]
 public static class ItemPotionStartup {
  static ItemPotionStartup(){var stat=DNDMind.Stat;if(stat==null)return;if(stat.parts==null)stat.parts=new List<StatPart>();if(!stat.parts.Any(p=>p is StatPart_MindShield))stat.parts.Add(new StatPart_MindShield{parentStat=stat});}
 }
 // 巫妖与死灵造物不会去喝药水（手动、药物政策都不行）。
 [HarmonyPatch(typeof(FoodUtility),nameof(FoodUtility.WillEat),new[]{typeof(Pawn),typeof(Thing),typeof(Pawn),typeof(bool),typeof(bool)})]
 public static class PotionUndeadThing {static void Postfix(Pawn __0,Thing __1,ref bool __result){if(__result && ItemPotions.IsPotion(__1?.def) && Items.Undead(__0))__result=false;}}
 [HarmonyPatch(typeof(FoodUtility),nameof(FoodUtility.WillEat),new[]{typeof(Pawn),typeof(ThingDef),typeof(Pawn),typeof(bool),typeof(bool)})]
 public static class PotionUndeadDef {static void Postfix(Pawn __0,ThingDef __1,ref bool __result){if(__result && ItemPotions.IsPotion(__1) && Items.Undead(__0))__result=false;}}
 [HarmonyPatch(typeof(Pawn_DrugPolicyTracker),nameof(Pawn_DrugPolicyTracker.ShouldTryToTakeScheduledNow))]
 public static class PotionUndeadPolicy {static void Postfix(Pawn_DrugPolicyTracker __instance,ThingDef __0,ref bool __result){if(__result && ItemPotions.IsPotion(__0) && Items.Undead(__instance.pawn))__result=false;}}
 [HarmonyPatch(typeof(Pawn_DrugPolicyTracker),nameof(Pawn_DrugPolicyTracker.AllowedToTakeToInventory))]
 public static class PotionUndeadInventory {static void Postfix(Pawn_DrugPolicyTracker __instance,ThingDef __0,ref bool __result){if(__result && ItemPotions.IsPotion(__0) && Items.Undead(__instance.pawn))__result=false;}}
 // 英雄药水：免疫恐慌逃跑等恐惧类精神状态（巫妖恐惧凝视）。精神崩溃由状态阶段的 blocksMentalBreaks 屏蔽。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class HeroismFearImmunity {
  public static bool Fear(MentalStateDef def)=>def!=null && (def==MentalStateDefOf.PanicFlee || def.defName.IndexOf("Panic",StringComparison.OrdinalIgnoreCase)>=0 || def.defName.IndexOf("Terror",StringComparison.OrdinalIgnoreCase)>=0);
  static bool Prefix(MentalStateDef __0,Pawn ___pawn,ref bool __result){
   if(!Fear(__0) || !Items.Has(___pawn,"LP_PotionHeroism"))return true;
   Items.Mote(___pawn,"无所畏惧！",new Color(1f,.85f,.4f));__result=false;return false;
  }
 }
 // 隐形药水：攻击就现形（药效直接结束）。
 [HarmonyPatch(typeof(Verb),"TryCastNextBurstShot")]
 public static class PotionInvisibilityBreak {
  static void Postfix(Verb __instance){var p=__instance.CasterPawn;if(p!=null && __instance.verbProps?.violent!=false && Items.Has(p,"LP_PotionInvisibility"))Items.Remove(p,"LP_PotionInvisibility");}
 }
 // 巨人之力药水：整个人物渲染 ×1.1。
 [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.ScaleFor))]
 public static class GiantStrengthRender {
  static void Postfix(PawnRenderNode node,PawnDrawParms parms,ref Vector3 __result){if(RenderTags.IsRoot(node?.Props?.tagDef) && Items.Has(parms.pawn,"LP_PotionGiantStrength"))__result*=ItemPotions.GiantRenderScale;}
 }
 // 巨魔被火焰或酸液伤到时，显示“被火烧”/“酸蚀”标记（6小时，与再生阻断同步）。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.PostApplyDamage))]
 public static class TrollBurnMarker {
  static void Postfix(Pawn ___pawn,DamageInfo dinfo,float totalDamageDealt){
   if(totalDamageDealt<=0 || ___pawn==null || ___pawn.Dead || !Troll.Burns(dinfo.Def) || !Troll.Is(___pawn))return;
   bool acid=dinfo.Def.defName.IndexOf("Acid",StringComparison.OrdinalIgnoreCase)>=0 || (dinfo.Def.hediff?.defName.IndexOf("Acid",StringComparison.OrdinalIgnoreCase)??-1)>=0;
   Items.Give(___pawn,acid?"LP_TrollAcid":"LP_TrollBurned",Gene_TrollRegeneration.FireSuppressionTicks);
  }
 }
}
