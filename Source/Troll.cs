using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 public static class Troll {
  static GeneDef regeneration;
  static HediffDef metabolism,regrowth,weakness;
  public static GeneDef RegenerationGene=>regeneration??(regeneration=DefDatabase<GeneDef>.GetNamed("LP_TrollRegeneration"));
  public static HediffDef Metabolism=>metabolism??(metabolism=DefDatabase<HediffDef>.GetNamed("LP_TrollMetabolism"));
  public static HediffDef Regrowth=>regrowth??(regrowth=DefDatabase<HediffDef>.GetNamed("LP_TrollRegrowth"));
  public static HediffDef Weakness=>weakness??(weakness=DefDatabase<HediffDef>.GetNamed("LP_TrollRevivalWeakness"));
  static BodyPartDef brain;static HediffDef burn;
  public static BodyPartDef Brain=>brain??(brain=DefDatabase<BodyPartDef>.GetNamed("Brain"));
  public static HediffDef Burn=>burn??(burn=DefDatabase<HediffDef>.GetNamed("Burn"));
  public static Gene_TrollRegeneration Gene(Pawn p)=>p?.genes?.GetGene(RegenerationGene) as Gene_TrollRegeneration;
  // 1.20.0：所有“是不是巨魔”的判断（手术、斩首、普查、并肩、矮人克制）都按激活的巨魔再生基因，自定义异种型也算。
  public static bool Is(Pawn p)=>p?.genes!=null && p.genes.HasActiveGene(RegenerationGene);
  public static TrollRevivals Revivals=>GameComp<TrollRevivals>.Get();
  // 火焰、烧伤和酸液都会阻断再生。除原版火焰外，按伤害及其伤势的 defName 判断，其他模组的燃烧、酸蚀武器也能接入。
  public static bool Burns(DamageDef def)=>def!=null && (def==DamageDefOf.Flame || def.HasModExtension<TrollBane>() || def.hediff==Troll.Burn || Mentions(def.defName) || (def.hediff!=null && Mentions(def.hediff.defName)));
  public static bool Burns(Hediff culprit)=>culprit!=null && (culprit.def==Troll.Burn || Mentions(culprit.def.defName));
  static bool Mentions(string name)=>name!=null && (name.IndexOf("Acid",StringComparison.OrdinalIgnoreCase)>=0 || name.IndexOf("Flame",StringComparison.OrdinalIgnoreCase)>=0 || name.IndexOf("Burn",StringComparison.OrdinalIgnoreCase)>=0 || name.IndexOf("Incendiary",StringComparison.OrdinalIgnoreCase)>=0);
 }
 // 给任意 DamageDef 加上 <modExtensions><li Class="LichPhylactery.TrollBane" /></modExtensions>，它就会阻断巨魔再生和复活。
 public class TrollBane : DefModExtension {}
 public class Gene_TrollRegeneration : Gene {
  public const int FireSuppressionTicks=15000;
  public const int LimbRegrowthTicks=180000;
  public const int ScarTicks=5*60000;
  public const int RevivalDelay=60000;
  public const int RegeneratingWindow=2500;
  public const float FoodPerHeal=.005f;
  int accumulated;
  public int suppressedUntilTick;
  public int regrowthTicks;
  public BodyPartRecord regrowing;
  public int lastHealTick=-99999;
  public int scarTicks;
  public int revivals;
  public int reviveTick=-1;
  public bool CanRegenerate=>Active && pawn?.Dead==false && pawn.needs?.food?.CurLevelPercentage>=0.5f && Find.TickManager.TicksGame>=suppressedUntilTick;
  public bool Regenerating=>Find.TickManager.TicksGame-lastHealTick<RegeneratingWindow;
  public override void PostAdd(){base.PostAdd();EnsureMetabolism();}
  public override void PostRemove(){
   foreach(var def in new[]{Troll.Metabolism,Troll.Regrowth}){var h=pawn?.health?.hediffSet.GetFirstHediffOfDef(def);if(h!=null)pawn.health.RemoveHediff(h);}
   reviveTick=-1;
   base.PostRemove();
  }
  void EnsureMetabolism(){
   if(pawn?.health!=null && pawn.health.hediffSet.GetFirstHediffOfDef(Troll.Metabolism)==null)pawn.health.AddHediff(Troll.Metabolism);
  }
  public void Suppress(){
   if(!Active || pawn==null)return;
   if(pawn.Dead){CancelRevival(true);return;}
   suppressedUntilTick=Math.Max(suppressedUntilTick,Find.TickManager.TicksGame+FireSuppressionTicks);
  }
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(!Active || pawn?.Dead!=false)return;
   EnsureMetabolism();
   accumulated+=delta;
   if(accumulated<250)return;
   int elapsed=accumulated;accumulated=0;
   // 护甲挡下了火焰伤害也算：只要身上还在烧，再生就停。
   if(pawn.Spawned && pawn.IsBurning())Suppress();
   Regenerate(elapsed);
  }
  // 体外部位都能长回（手指、眼睛、耳朵、鼻子、下颌等）；内脏和大脑不长，避免反复摘取器官。
  public static bool CanRegrow(BodyPartRecord part)=>part!=null && part.depth!=BodyPartDepth.Inside && part.def!=Troll.Brain;
  // 手臂、腿约3天；手、脚按部位生命值缩短；手指、眼睛等小部位约1天。
  public static int RegrowthTicks(BodyPartRecord part)=>(int)(LimbRegrowthTicks*Mathf.Clamp(part.def.hitPoints/30f,1f/3f,1f));
  public void Regenerate(int elapsed){
   if(!CanRegenerate){SyncRegrowthHediff();return;}
   var set=pawn.health.hediffSet;
   float healed=0,budget=0.5f*elapsed/250f;
   // 先修正在流血的伤口，再按严重度。
   var injuries=set.hediffs.OfType<Hediff_Injury>().Where(h=>!h.IsPermanent()).OrderByDescending(h=>h.BleedRate>0).ThenByDescending(h=>h.Severity).ToList();
   foreach(var injury in injuries){float heal=Math.Min(injury.Severity,budget);injury.Heal(heal);budget-=heal;healed+=heal;if(budget<=0)break;}
   var blood=set.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
   if(blood!=null){float restore=Math.Min(blood.Severity,.01f*elapsed/250f);blood.Severity-=restore;healed+=restore*100f;}
   var scars=set.hediffs.OfType<Hediff_Injury>().Where(h=>h.IsPermanent() && h.Part?.def!=Troll.Brain).ToList();
   if(scars.Count==0)scarTicks=0;
   else if((scarTicks+=elapsed)>=ScarTicks){
    var scar=scars.RandomElement();scarTicks=0;pawn.health.RemoveHediff(scar);healed+=10;
    if(pawn.Faction==Faction.OfPlayer)Messages.Message(pawn.LabelShort+"的旧伤（"+scar.LabelCap+"）在巨魔再生下消退了。",pawn,MessageTypeDefOf.PositiveEvent,false);
   }
   var missing=Necro.Missing(pawn).Where(CanRegrow).ToList();
   if(regrowing!=null && !missing.Contains(regrowing)){regrowing=null;regrowthTicks=0;}
   if(regrowing==null){regrowing=missing.FirstOrDefault();regrowthTicks=0;}
   if(regrowing!=null){
    int total=RegrowthTicks(regrowing);
    regrowthTicks+=elapsed;healed+=50f*elapsed/total;
    if(regrowthTicks>=total){
     var part=regrowing;regrowing=null;regrowthTicks=0;pawn.health.RestorePart(part);
     if(pawn.Faction==Faction.OfPlayer)Messages.Message(pawn.LabelShort+"的"+part.Label+"长回来了。",pawn,MessageTypeDefOf.PositiveEvent,false);
    }
   }
   SyncRegrowthHediff();
   if(healed>0){
    lastHealTick=Find.TickManager.TicksGame;
    var food=pawn.needs?.food;if(food!=null)food.CurLevel=Math.Max(0,food.CurLevel-healed*FoodPerHeal);
   }
  }
  // 在残肢的上一级部位显示重长进度。
  void SyncRegrowthHediff(){
   if(pawn?.health==null)return;
   var shown=pawn.health.hediffSet.GetFirstHediffOfDef(Troll.Regrowth) as Hediff_TrollRegrowth;
   var host=regrowing?.parent;
   if(host==null || pawn.health.hediffSet.PartIsMissing(host)){if(shown!=null)pawn.health.RemoveHediff(shown);return;}
   if(shown!=null && shown.Part!=host){pawn.health.RemoveHediff(shown);shown=null;}
   if(shown==null){shown=(Hediff_TrollRegrowth)HediffMaker.MakeHediff(Troll.Regrowth,pawn,host);pawn.health.AddHediff(shown,host);}
   shown.target=regrowing.Label;
   shown.Severity=Mathf.Clamp(regrowthTicks/(float)RegrowthTicks(regrowing),.01f,1f);
  }
  // 不烧就会爬起来：死前6小时内没被火焰或酸液伤到、大脑还在，尸体保持新鲜约1天后原地复活。
  public void Notify_Died(DamageInfo? dinfo,Hediff culprit){
   reviveTick=-1;
   if(!Active || !LichMod.Settings.trollRevival || Necro.Undead(pawn))return;
   if(Find.TickManager.TicksGame<suppressedUntilTick || (dinfo.HasValue && Troll.Burns(dinfo.Value.Def)) || Troll.Burns(culprit))return;
   // 再生只对付伤口：饿死、病死、冻死、中毒等不是伤口造成的死亡不会复活，也避免复活后立刻再死。
   if(culprit!=null && !(culprit is Hediff_Injury) && !(culprit is Hediff_MissingPart) && culprit.def!=HediffDefOf.BloodLoss)return;
   if(revivals>=LichMod.Settings.trollRevivalLimit || pawn.health.hediffSet.GetBrain()==null || pawn.Corpse==null)return;
   reviveTick=Find.TickManager.TicksGame+RevivalDelay;
   Troll.Revivals?.Add(pawn);
   var corpse=pawn.Corpse;
   if(pawn.Faction==Faction.OfPlayer)Messages.Message(pawn.LabelShort+"倒下了，但巨魔血肉仍在重生：约1天后会复活。火焰、火化或腐烂会打断它。",corpse,MessageTypeDefOf.NeutralEvent,false);
   else if(pawn.HostileTo(Faction.OfPlayer) && corpse.MapHeld?.IsPlayerHome==true)Messages.Message("倒下的敌方巨魔"+pawn.LabelShort+"仍在再生：约1天后会重新站起来。烧掉或火化尸体可以阻止。",corpse,MessageTypeDefOf.ThreatSmall,false);
  }
  public void CancelRevival(bool burned){
   if(reviveTick<0)return;
   reviveTick=-1;
   if(burned && pawn.Corpse?.MapHeld?.IsPlayerHome==true)Messages.Message(pawn.LabelShort+"的尸体被火焰灼烧，巨魔再生彻底中断。",pawn.Corpse,MessageTypeDefOf.NeutralEvent,false);
  }
  public string RevivalStatus=>reviveTick<0?null:"巨魔再生："+Math.Max(0,(reviveTick-Find.TickManager.TicksGame)/2500f).ToString("0.0")+"小时后复活（烧掉或火化尸体可阻止）";
  // 返回 true 表示这次复活已经结束（成功或取消），可以移出待复活列表。
  public bool ProcessRevival(){
   if(reviveTick<0)return true;
   if(!pawn.Dead){reviveTick=-1;return true;}
   if(!LichMod.Settings.trollRevival){CancelRevival(false);return true;}
   var corpse=pawn.Corpse;
   if(corpse==null || corpse.Destroyed){reviveTick=-1;return true;}
   if(corpse.IsBurning()){CancelRevival(true);return true;}
   if(corpse.GetRotStage()!=RotStage.Fresh || pawn.health.hediffSet.GetBrain()==null){CancelRevival(false);return true;}
   if(Find.TickManager.TicksGame<reviveTick)return false;
   return TryRevive();
  }
  bool TryRevive(){
   var corpse=pawn.Corpse;Map map=corpse.MapHeld;
   if(map==null)return false; // 在商队或世界中：等它回到地图上。
   if(!corpse.Spawned){
    IntVec3 cell=corpse.PositionHeld;
    if(corpse.holdingOwner==null || !corpse.holdingOwner.TryDrop(corpse,cell,map,ThingPlaceMode.Near,out Thing _))return false;
   }
   if(!ResurrectionUtility.TryResurrect(pawn,new ResurrectionParams{removeDiedThoughts=true}))return false;
   reviveTick=-1;revivals++;
   pawn.health.AddHediff(Troll.Weakness);
   if(pawn.needs?.food!=null)pawn.needs.food.CurLevel=0;
   if(pawn.Spawned && pawn.HostileTo(Faction.OfPlayer) && !pawn.IsPrisoner && pawn.GetLord()==null)LordMaker.MakeNewLord(pawn.Faction,new LordJob_AssaultColony(pawn.Faction),map,new[]{pawn});
   else if(pawn.Spawned && pawn.Faction!=null && pawn.Faction!=Faction.OfPlayer && !pawn.IsPrisoner && pawn.GetLord()==null)LordMaker.MakeNewLord(pawn.Faction,new LordJob_ExitMapBest(),map,new[]{pawn}); // 访客、商队成员复活后自行离开，不会呆站在地图上。
   if(pawn.Faction==Faction.OfPlayer)Messages.Message(pawn.LabelShort+"的血肉重新长合，从死亡中站了起来。",pawn,MessageTypeDefOf.PositiveEvent,false);
   else if(pawn.HostileTo(Faction.OfPlayer))Messages.Message("敌方巨魔"+pawn.LabelShort+"重新站了起来！",pawn,MessageTypeDefOf.ThreatBig,false);
   return true;
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref accumulated,"trollHealingTicks",0);
   Scribe_Values.Look(ref suppressedUntilTick,"trollSuppressedUntilTick",0);
   Scribe_Values.Look(ref regrowthTicks,"trollRegrowthTicks",0);
   Scribe_BodyParts.Look(ref regrowing,"trollRegrowing");
   Scribe_Values.Look(ref lastHealTick,"trollLastHealTick",-99999);
   Scribe_Values.Look(ref scarTicks,"trollScarTicks",0);
   Scribe_Values.Look(ref revivals,"trollRevivals",0);
   Scribe_Values.Look(ref reviveTick,"trollReviveTick",-1);
  }
 }
 public class Hediff_TrollMetabolism : Hediff {
  static readonly List<StatModifier> immunity=new List<StatModifier>{new StatModifier{stat=StatDefOf.ImmunityGainSpeed,value=1.5f}};
  static readonly HediffStage regenerating=new HediffStage{hungerRateFactor=2f,statFactors=immunity};
  static readonly HediffStage resting=new HediffStage{hungerRateFactor=1.2f,statFactors=immunity};
  static readonly HediffStage inactive=new HediffStage{hungerRateFactor=1f};
  Gene_TrollRegeneration Gene=>Troll.Gene(pawn);
  public override HediffStage CurStage{get{var gene=Gene;return gene?.Active!=true?inactive:gene.Regenerating?regenerating:resting;}}
  public override string LabelInBrackets{get{var gene=Gene;if(gene?.Active!=true)return null;if(Find.TickManager.TicksGame<gene.suppressedUntilTick)return "被火焰阻断";if(pawn.needs?.food!=null && pawn.needs.food.CurLevelPercentage<.5f)return "饥饿，再生暂停";return gene.Regenerating?"再生中":"静息";}}
 }
 public class Hediff_TrollRegrowth : Hediff {
  public string target;
  public override bool ShouldRemove=>base.ShouldRemove || Troll.Gene(pawn)?.regrowing==null;
  public override string LabelInBrackets=>(target??"")+" "+Severity.ToStringPercent();
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref target,"trollRegrowTarget");}
 }
 public class TrollRevivals : GameComponent {
  public List<Pawn> pending=new List<Pawn>();
  public TrollRevivals(Game game){}
  public void Add(Pawn pawn){if(pawn!=null && !pending.Contains(pawn))pending.Add(pawn);}
  public override void GameComponentTick(){if(pending.Count>0 && Find.TickManager.TicksGame%250==0)Process();}
  public void Process(){
   for(int i=pending.Count-1;i>=0;i--){
    var pawn=pending[i];var gene=Troll.Gene(pawn);
    if(pawn==null || pawn.Discarded || gene==null || gene.ProcessRevival())pending.RemoveAt(i);
   }
  }
  public override void ExposeData(){
   if(Scribe.mode==LoadSaveMode.Saving)pending.RemoveAll(p=>p==null || p.Discarded || p.Corpse==null || p.Corpse.Destroyed || Troll.Gene(p)?.reviveTick<0);
   Scribe_Collections.Look(ref pending,"trollRevivals",LookMode.Reference);
   if(Scribe.mode==LoadSaveMode.PostLoadInit)pending=pending?.Where(p=>p!=null).Distinct().ToList()??new List<Pawn>();
  }
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.PostApplyDamage))]
 public static class TrollFireSuppression {
  static void Postfix(Pawn ___pawn,DamageInfo dinfo,float totalDamageDealt){
   if(totalDamageDealt<=0 || ___pawn?.genes==null || !Troll.Burns(dinfo.Def))return;
   Troll.Gene(___pawn)?.Suppress();
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class TrollDeath {
  static void Postfix(Pawn __instance,DamageInfo? __0,Hediff __1){ // 1.58.6：按位置绑定参数，不再用 object[] __args（每次调用都分配）
   if(__instance?.Dead!=true || __instance.genes==null)return;
   var gene=Troll.Gene(__instance);if(gene==null)return;
   gene.Notify_Died(__0,__1);
  }
 }
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class TrollCorpseFire {
  static void Postfix(Thing __instance,DamageInfo __0){ // 1.58.6：所有东西每次受伤都会经过；原来的 object[] __args 每次都分配数组并装箱
   if(!(__instance is Corpse corpse) || !Troll.Burns(__0.Def))return;
   Troll.Gene(corpse.InnerPawn)?.CancelRevival(true);
  }
 }
 [HarmonyPatch(typeof(Corpse),nameof(Corpse.GetInspectString))]
 public static class TrollCorpseInspect {
  static void Postfix(Thing __instance,ref string __result){
   if(!(__instance is Corpse corpse))return;
   var status=Troll.Gene(corpse.InnerPawn)?.RevivalStatus;
   if(status!=null)__result=string.IsNullOrEmpty(__result)?status:__result+"\n"+status;
  }
 }
}
