using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 public static class ForceDragon {
  static GeneDef heart,claws,scales,body,carnivore,arrogance,hoarder,stomach;
  static HediffDef age,clawWeapon,sphere,invisibility;
  static DamageDef force;
  public static GeneDef HeartGene=>heart??(heart=DefDatabase<GeneDef>.GetNamedSilentFail("LP_ForceDragonHeart"));
  public static GeneDef ClawsGene=>claws??(claws=DefDatabase<GeneDef>.GetNamedSilentFail("LP_DragonClaws"));
  public static GeneDef ScalesGene=>scales??(scales=DefDatabase<GeneDef>.GetNamedSilentFail("LP_DragonScales"));
  public static GeneDef BodyGene=>body??(body=DefDatabase<GeneDef>.GetNamedSilentFail("LP_DragonBody"));
  public static GeneDef CarnivoreGene=>carnivore??(carnivore=DefDatabase<GeneDef>.GetNamedSilentFail("LP_DragonCarnivore"));
  public static GeneDef ArroganceGene=>arrogance??(arrogance=DefDatabase<GeneDef>.GetNamedSilentFail("LP_DragonArrogance"));
  public static GeneDef HoarderGene=>hoarder??(hoarder=DefDatabase<GeneDef>.GetNamedSilentFail("LP_DragonHoarder"));
  // 1.23.2：原版“铁胃”（不会食物中毒）。
  public static GeneDef StomachGene=>stomach??(stomach=DefDatabase<GeneDef>.GetNamedSilentFail("StrongStomach"));
  public static HediffDef AgeHediff=>age??(age=DefDatabase<HediffDef>.GetNamed("LP_DragonAge"));
  public static HediffDef ClawWeapon=>clawWeapon??(clawWeapon=DefDatabase<HediffDef>.GetNamed("LP_DragonClawWeapon"));
  public static HediffDef Sphere=>sphere??(sphere=DefDatabase<HediffDef>.GetNamed("LP_ForceSphere"));
  public static HediffDef Invisibility=>invisibility??(invisibility=DefDatabase<HediffDef>.GetNamed("LP_DragonInvisibility"));
  public static DamageDef Force=>force??(force=DefDatabase<DamageDef>.GetNamed("LP_Force"));
  public static readonly string[] AgeLabels={"幼龙","少年龙","成年龙","古龙","伟龙"};
  // 晋升到下一级所需的累计天数与宝库价值（默认值，按模组设置的百分比缩放）。
  public static readonly float[] AgeDays={30,60,120,240};
  public static readonly float[] AgeHoard={5000,20000,60000,150000};
  public static readonly (string name,int age)[] Abilities={("LP_ForceBreath",1),("LP_DevourShield",1),("LP_ResilientSphere",2),("LP_WallOfForce",3),("LP_ForceCage",4),("LP_TelekineticSphere",5)};
  public const float DeflectMinimum=.2f,RegenPerDay=.05f,MeatForce=1f,ShieldForce=25f,BeltForce=40f,DeviceForce=30f;
  public const int SphereTicks=600,WallTicks=1200,CageTicks=60000,MaxAge=5;
  public static bool Has(Pawn p,GeneDef def)=>def!=null && p?.genes!=null && p.genes.HasActiveGene(def);
  public static bool Active(Pawn p)=>Has(p,HeartGene);
  public static Gene_ForceDragonHeart Heart(Pawn p){var gene=p?.genes?.GetGene(HeartGene) as Gene_ForceDragonHeart;return gene!=null && gene.Active?gene:null;}
  // 体型、部位血量、伤害判定都会频繁查询龙龄：直接遍历现有列表，不分配迭代器。
  public static Hediff_DragonAge AgeOf(Pawn p)=>HediffIndex<Hediff_DragonAge>.Get(p); // 1.58.6：按小人登记，不再每次扫 hediff
  public static int Age(Pawn p){var h=AgeOf(p);return h==null?0:h.Stage;}
  public static string AgeLabel(int age)=>AgeLabels[Mathf.Clamp(age,1,MaxAge)-1];
  // 1.23.2：旧存档里的力能龙补上铁胃，每条龙只补一次（之后玩家自己移除不会再加回来）。
  public static void Migrate(Pawn p){
   var heart=Heart(p);
   if(heart==null || heart.stomachMigrated || p.Dead)return;
   heart.stomachMigrated=true;
   if(p.genes.Xenotype?.defName=="LP_ForceDragon" && StomachGene!=null && p.genes.GetGene(StomachGene)==null)p.genes.AddGene(StomachGene,!p.genes.Endogenes.Contains(heart));
  }
  public static void MigrateLoadedGame(){
   if(Find.Maps!=null)foreach(var pawn in Find.Maps.SelectMany(m=>m.mapPawns.AllPawns).Distinct().ToList())Migrate(pawn);
   if(Find.WorldPawns!=null)foreach(var pawn in Find.WorldPawns.AllPawnsAlive.ToList())Migrate(pawn);
  }
  public static float MaxForce(int age)=>.5f+.1f*Mathf.Clamp(age,1,MaxAge);
  public static float DeflectChance(int age)=>age<=0?0:Math.Min(.35f,.1f+.05f*age);
  public static float EvadeChance(int age)=>age>=4?.5f:age>=2?.2f:0f;
  public static float BreathRange(int age)=>new[]{4f,5f,6f,7.5f,9f}[Mathf.Clamp(age,1,MaxAge)-1];
  public static float BreathDamage(int age)=>10f+3f*Mathf.Clamp(age,1,MaxAge);
  public static float ClawFactor(int age)=>1f+.25f*(Mathf.Clamp(age,1,MaxAge)-1);
  public static float ArmorSharp(int age)=>.2f+.1f*Mathf.Clamp(age,1,MaxAge);
  public static float ArmorBlunt(int age)=>.075f+.075f*Mathf.Clamp(age,1,MaxAge);
  // 1.27.1 龙之长眠：成年龙的总饥饿速率（已抵消基因代谢率），从500%饿到225%约3天，见 Satiety。
  public static float Hunger(int age)=>Satiety.DragonHunger(age);
  // 1.23.1 幼崽食量：成年前抵消基因代谢率带来的食物消耗（原版按未被覆盖基因的代谢率总和查曲线），总倍率保持100%。
  public static bool Juvenile(Pawn p)=>p?.ageTracker!=null && !p.DevelopmentalStage.Adult();
  public static int Metabolism(Pawn p){int sum=0;var list=p?.genes?.GenesListForReading;if(list!=null)for(int i=0;i<list.Count;i++)if(!list[i].Overridden)sum+=list[i].def.biostatMet;return sum;}
  public static float MetabolismFactor(int metabolism)=>GeneTuning.MetabolismToFoodConsumptionFactorCurve.Evaluate(metabolism);
  public static float SizeFactor(int age)=>age<=0?1f:1f+.05f*(Mathf.Clamp(age,1,MaxAge)-1);
  public static float MeleeHitOffset(int age)=>.03f*(Mathf.Clamp(age,1,MaxAge)-1);
  public static int WallLength(int age)=>age>=5?9:age>=4?7:5;
  public static float DaysNeeded(int age)=>age<1 || age>=MaxAge?float.PositiveInfinity:AgeDays[age-1]*LichMod.Settings.dragonAgePercent/100f;
  public static float HoardNeeded(int age)=>age<1 || age>=MaxAge?float.PositiveInfinity:AgeHoard[age-1]*LichMod.Settings.dragonAgePercent/100f;
  public static float Points(Pawn p)=>(Heart(p)?.Value??0)*100f;
  public static void Gain(Pawn p,float points){var h=Heart(p);if(h!=null)h.Value=Mathf.Min(h.Max,h.Value+points/100f);}
  public static bool Pay(Pawn p,float points){var h=Heart(p);if(h==null || h.Value*100f+.0001f<points)return false;h.Value=Mathf.Max(0,h.Value-points/100f);return true;}
  public static void Refresh(Pawn p){p?.health?.capacities?.Notify_CapacityLevelsDirty();p?.Drawer?.renderer?.SetAllGraphicsDirty();}

  // 伤害判定：力场法球吸收一切；偏折只挡远程；朦胧/错位挡近战和远程。爆炸、环境伤害、自伤不触发闪避。EMP 不会让任何效果失效。
  public static bool IsAttack(Pawn p,DamageInfo dinfo)=>dinfo.Def!=null && dinfo.Def.harmsHealth && !dinfo.Def.isExplosive && dinfo.Def.ExternalViolenceFor(p) && dinfo.Instigator!=null && dinfo.Instigator!=p;
  public static bool IsRanged(DamageInfo dinfo)=>dinfo.Def.isRanged || dinfo.Weapon?.IsRangedWeapon==true;
  public static string Avoid(Pawn p,DamageInfo dinfo){
   if(p?.health?.hediffSet==null)return null;
   if(p.health.hediffSet.HasHediff(Sphere))return "力场法球";
   if(Items.Dispelled(p))return null; // 1.22.0 驱散粉尘：偏折、朦胧、错位全部失效
   var heart=Heart(p);if(heart==null || !IsAttack(p,dinfo))return null;
   int a=Age(p);float points=heart.Value*100f;
   // 1.45.0：被龙杀手砍中后5秒偏折失效。
   if(IsRanged(dinfo) && points>=DeflectMinimum*100f && !Items.Has(p,"LP_DeflectSuppressed") && Rand.Chance(DeflectChance(a)))return "偏折";
   if(points>0 && Rand.Chance(EvadeChance(a)))return a>=4?"错位":"朦胧";
   return null;
  }
  public static bool IsDragonFood(Thing food)=>food?.def?.ingestible!=null && food.def.IsNutritionGivingIngestible && !food.def.IsDrug;
 }

 public class Gene_ForceDragonHeart : Gene_Resource {
  public float daysOnHoard,hoardValue,hoardBeauty,nestDays;
  public bool hasHoard,stomachMigrated,binging; // binging：1.27.1 暴食，开吃后吃到95%
  public int nextClutchTick=-1;
  int regenTicks,hourTicks;
  public const int HourTicks=2500;
  public override float InitialResourceMax=>ForceDragon.MaxForce(1);
  public override float MinLevelForAlert=>ForceDragon.DeflectMinimum;
  protected override Color BarColor=>new Color(.45f,.7f,1f);
  protected override Color BarHighlightColor=>new Color(.7f,.88f,1f);
  public int Age=>Math.Max(1,ForceDragon.Age(pawn));
  public override void PostAdd(){
   base.PostAdd();EnsureAge();SyncMax();Value=Max*.5f;SyncAbilities(false);stomachMigrated=true;
  }
  public override void PostRemove(){
   foreach(var def in new[]{ForceDragon.AgeHediff,ForceDragon.Invisibility}){var h=pawn?.health?.hediffSet.GetFirstHediffOfDef(def);if(h!=null)pawn.health.RemoveHediff(h);}
   if(pawn?.abilities!=null)foreach(var a in ForceDragon.Abilities){var def=DefDatabase<AbilityDef>.GetNamedSilentFail(a.name);if(def!=null && pawn.abilities.GetAbility(def)!=null)pawn.abilities.RemoveAbility(def);}
   base.PostRemove();ForceDragon.Refresh(pawn);
  }
  public Hediff_DragonAge EnsureAge(){
   if(pawn?.health==null)return null;
   var h=ForceDragon.AgeOf(pawn);
   if(h==null){h=(Hediff_DragonAge)pawn.health.AddHediff(ForceDragon.AgeHediff);h.Severity=1;}
   return h;
  }
  public void SyncMax(){float target=ForceDragon.MaxForce(Age);if(Math.Abs(Max-target)>.0001f)SetMax(target);}
  public void SyncAbilities(bool announce){
   if(pawn?.abilities==null)return;
   int a=Age;
   foreach(var entry in ForceDragon.Abilities){
    var def=DefDatabase<AbilityDef>.GetNamedSilentFail(entry.name);if(def==null)continue;
    bool has=pawn.abilities.GetAbility(def)!=null;
    if(a>=entry.age && !has){pawn.abilities.GainAbility(def);if(announce && pawn.Faction==Faction.OfPlayer && !pawn.Dead)Messages.Message(pawn.LabelShort+"领悟了"+def.label+"。",pawn,MessageTypeDefOf.PositiveEvent,false);}
    else if(a<entry.age && has)pawn.abilities.RemoveAbility(def);
   }
  }
  // 伟龙常驻隐形；力能归零或不是伟龙时移除。
  public void SyncInvisibility(){
   if(pawn?.health==null || pawn.Dead)return;
   var h=pawn.health.hediffSet.GetFirstHediffOfDef(ForceDragon.Invisibility);
   bool want=Age>=ForceDragon.MaxAge && Value>0;
   if(want && h==null)pawn.health.AddHediff(ForceDragon.Invisibility);
   else if(!want && h!=null)pawn.health.RemoveHediff(h);
  }
  public void SetAge(int value){
   var h=EnsureAge();if(h==null)return;
   value=Mathf.Clamp(value,1,ForceDragon.MaxAge);
   h.Severity=value;
   pawn.health.Notify_HediffChanged(h);BodySize.Notify_SizeChanged(pawn);
   SyncMax();SyncAbilities(true);SyncInvisibility();ForceDragon.Refresh(pawn);
  }
  public bool TryAdvance(){
   int a=Age;if(a>=ForceDragon.MaxAge)return false;
   if(daysOnHoard+.0001f<ForceDragon.DaysNeeded(a) || hoardValue+.01f<ForceDragon.HoardNeeded(a))return false;
   SetAge(a+1);
   if(pawn.Faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("龙龄晋升",pawn.LabelShort+"在宝库中熬过了漫长的岁月，晋升为"+ForceDragon.AgeLabel(a+1)+"。体型、鳞甲、龙爪和力能上限都提高了。",LetterDefOf.PositiveEvent,pawn);
   return true;
  }
  public void RefreshHoard(){var room=DragonHoard.RoomOf(pawn);hasHoard=room!=null;hoardValue=room==null?0:DragonHoard.Value(room);hoardBeauty=room==null?0:DragonHoard.Beauty(room);}
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(!Active || pawn?.Dead!=false)return;
   regenTicks+=delta;hourTicks+=delta;
   if(regenTicks>=250){Value=Mathf.Min(Max,Value+ForceDragon.RegenPerDay*regenTicks/60000f);regenTicks=0;}
   if(hourTicks<HourTicks)return;
   int elapsed=hourTicks;hourTicks=0;
   Hourly(elapsed);
  }
  public void Hourly(int elapsed){
   EnsureAge();SyncMax();RefreshHoard();
   var room=DragonHoard.RoomOf(pawn);
   if(room!=null && pawn.Spawned && pawn.Map==room.Map)daysOnHoard+=elapsed/60000f;
   TryAdvance();
   DragonHoard.TickNest(this,room,elapsed);
   SyncAbilities(true);SyncInvisibility();
  }
  public string Status{get{
   int a=Age;var s="龙龄："+ForceDragon.AgeLabel(a)+"（"+a+"/"+ForceDragon.MaxAge+"）\n宝库："+(hasHoard?hoardValue.ToString("0")+"银，美观度"+hoardBeauty.ToString("0.#"):"没有（为它分配一间独占卧室）")+"\n守着宝库的天数："+daysOnHoard.ToString("0.#");
   if(a<ForceDragon.MaxAge)s+="\n晋升"+ForceDragon.AgeLabel(a+1)+"需要："+ForceDragon.DaysNeeded(a).ToString("0.#")+"天、宝库"+ForceDragon.HoardNeeded(a).ToString("0")+"银";
   s+="\n偏折："+ForceDragon.DeflectChance(a).ToStringPercent()+(Value*100f<ForceDragon.DeflectMinimum*100f?"（力能不足，已关闭）":"")+"；"+(a>=4?"错位":"朦胧")+"："+ForceDragon.EvadeChance(a).ToStringPercent()+(Value<=0?"（力能耗尽）":"");
   if(nestDays>0)s+="\n筑巢："+nestDays.ToString("0.#")+"/"+DragonHoard.NestDays+"天";
   return s;
  }}
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref daysOnHoard,"dragonDaysOnHoard",0);
   Scribe_Values.Look(ref hoardValue,"dragonHoardValue",0);
   Scribe_Values.Look(ref hoardBeauty,"dragonHoardBeauty",0);
   Scribe_Values.Look(ref hasHoard,"dragonHasHoard",false);
   Scribe_Values.Look(ref nestDays,"dragonNestDays",0);
   Scribe_Values.Look(ref nextClutchTick,"dragonNextClutchTick",-1);
   Scribe_Values.Look(ref regenTicks,"dragonRegenTicks",0);
   Scribe_Values.Look(ref hourTicks,"dragonHourTicks",0);
   Scribe_Values.Look(ref stomachMigrated,"dragonStomachMigrated",false);
   Scribe_Values.Look(ref binging,"dragonBinging",false);
  }
 }

 public class GeneGizmo_ForceResource : GeneGizmo_Resource {
  static bool dragging;
  readonly Gene_Resource resource;
  public GeneGizmo_ForceResource(Gene_Resource gene,List<IGeneResourceDrain> drainGenes,Color barColor,Color barHighlightColor):base(gene,drainGenes,barColor,barHighlightColor){resource=gene;}
  protected override bool DraggingBar{get=>dragging;set=>dragging=value;}
  protected override bool IsDraggable=>false;
  protected override string Title=>"力能";
  protected override string GetTooltip(){
   var heart=resource as Gene_ForceDragonHeart;
   string s="力能："+(resource.Value*100f).ToString("0")+" / "+(resource.Max*100f).ToString("0")+"\n每天自然恢复"+(ForceDragon.RegenPerDay*100f).ToString("0")+"点；打碎护盾或吞噬护盾能大量回复。";
   if(heart!=null)s+="\n\n"+heart.Status;
   return s;
  }
 }

 public class Hediff_DragonAge : Hediff,IIndexedHediff {
  // 1.58.6：按小人登记（BodySize 后缀对每个小人都要查龙龄）。
  public bool IndexLive{get;set;}
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);HediffIndex<Hediff_DragonAge>.Add(this);}
  public override void PostRemoved(){base.PostRemoved();HediffIndex<Hediff_DragonAge>.Removed(this);}
  public override void ExposeData(){base.ExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit)HediffIndex<Hediff_DragonAge>.Add(this);}
  HediffStage cached;int cachedAge,cachedMetabolism;bool cachedScales,cachedJuvenile;
  int keyTick=int.MinValue,keyGenes=-1,keyMet;bool keyScales;Pawn keyPawn;
  public int Stage=>Mathf.Clamp(Mathf.RoundToInt(Severity),1,ForceDragon.MaxAge);
  public override bool ShouldRemove=>base.ShouldRemove || pawn?.genes?.GetGene(ForceDragon.HeartGene)==null;
  public override string LabelInBrackets=>ForceDragon.AgeLabel(Stage);
  public override string TipStringExtra=>ForceDragon.Heart(pawn)?.Status;
  public override HediffStage CurStage{get{
   int a=Stage;
   // 1.58.6：两次基因表扫描同一刻、基因没变时只做一次（GeneVersion 在任何小人 Notify_GenesChanged 时递增）。
   int now=Find.TickManager?.TicksGame??0,gv=GeneVersion.Value;
   if(keyTick!=now || keyGenes!=gv || keyPawn!=pawn){keyScales=ForceDragon.Has(pawn,ForceDragon.ScalesGene);keyMet=ForceDragon.Metabolism(pawn);keyTick=now;keyGenes=gv;keyPawn=pawn;}
   bool scales=keyScales,juvenile=ForceDragon.Juvenile(pawn);int met=keyMet;
   if(cached!=null && cachedAge==a && cachedScales==scales && cachedJuvenile==juvenile && cachedMetabolism==met)return cached;
   cachedAge=a;cachedScales=scales;cachedJuvenile=juvenile;cachedMetabolism=met;
   var offsets=new List<StatModifier>{new StatModifier{stat=StatDefOf.MeleeHitChance,value=ForceDragon.MeleeHitOffset(a)}};
   if(scales){offsets.Add(new StatModifier{stat=StatDefOf.ArmorRating_Sharp,value=ForceDragon.ArmorSharp(a)});offsets.Add(new StatModifier{stat=StatDefOf.ArmorRating_Blunt,value=ForceDragon.ArmorBlunt(a)});}
   // 幼崽：不吃龙龄食量，基因 × 龙龄 = 100%。1.27.1 成年：同样除掉基因代谢率倍率，基因 × 龙龄 = 龙之长眠速率 H(龙龄)。
   return cached=new HediffStage{hungerRateFactor=(juvenile?1f:ForceDragon.Hunger(a))/ForceDragon.MetabolismFactor(met),statOffsets=offsets};
  }}
 }

 public class Gene_DragonClaws : Gene {
  public override void PostAdd(){base.PostAdd();Ensure();}
  public void Ensure(){if(pawn?.health!=null && pawn.health.hediffSet.GetFirstHediffOfDef(ForceDragon.ClawWeapon)==null)pawn.health.AddHediff(ForceDragon.ClawWeapon);}
  public override void PostRemove(){var h=pawn?.health?.hediffSet.GetFirstHediffOfDef(ForceDragon.ClawWeapon);if(h!=null)pawn.health.RemoveHediff(h);base.PostRemove();}
  int ticks;
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<2500)return;ticks=0;if(Active)Ensure();}
 }
 public class Hediff_DragonClaws : HediffWithComps {
  public override bool ShouldRemove=>base.ShouldRemove || pawn?.genes?.GetGene(ForceDragon.ClawsGene)==null;
  public override string LabelInBrackets=>"伤害×"+ForceDragon.ClawFactor(Math.Max(1,ForceDragon.Age(pawn))).ToStringPercent();
 }

 // ==================== Harmony 补丁 ====================

 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class ForceDragonAvoidance {
  static bool Prefix(Pawn __instance,ref DamageInfo __0,ref bool __1){
   string label=ForceDragon.Avoid(__instance,__0);
   if(label==null)return true;
   __1=true;
   if(__instance.Spawned)MoteMaker.ThrowText(__instance.DrawPos,__instance.Map,label,new Color(.6f,.8f,1f));
   return false;
  }
 }
 // 不受 EMP 影响：EMP 命中力能龙不会造成眩晕。
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.Notify_DamageApplied))]
 public static class ForceDragonEmpImmunity {
  static bool Prefix(Pawn ___parentPawn,DamageInfo __0)=>__0.Def!=DamageDefOf.EMP || !ForceDragon.Active(___parentPawn);
 }
 [HarmonyPatch(typeof(Pawn),"get_BodySize")]
 public static class ForceDragonBodySize {
  static void Postfix(Pawn __instance,ref float __result){int a=ForceDragon.Age(__instance);if(a>1)__result*=ForceDragon.SizeFactor(a);}
 }
 [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.ScaleFor))]
 public static class ForceDragonRenderSize {
  static void Postfix(PawnRenderNode node,PawnDrawParms parms,ref Vector3 __result){if(!RenderTags.IsRoot(node?.Props?.tagDef))return;int a=ForceDragon.Age(parms.pawn);if(a>1)__result*=ForceDragon.SizeFactor(a);}
 }
 // 龙爪伤害随龙龄提高：只放大龙爪本身，不影响其他武器。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply")]
 public static class ForceDragonClawDamage {
  static IEnumerable<DamageInfo> Postfix(IEnumerable<DamageInfo> values,Verb_MeleeAttackDamage __instance){
   float factor=__instance.HediffCompSource?.parent?.def==ForceDragon.ClawWeapon?ForceDragon.ClawFactor(Math.Max(1,ForceDragon.Age(__instance.CasterPawn))):1f;
   foreach(var d in values){if(factor!=1f){var copy=d;copy.SetAmount(d.Amount*factor);yield return copy;}else yield return d;}
  }
 }
 // 傲慢威压：近战技能不如力能龙的敌人对它近战命中×80%。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class ForceDragonPride {
  public const float Factor=.8f;
  public static bool Applies(Pawn attacker,Pawn dragon)=>attacker!=null && dragon!=null && attacker!=dragon && ForceDragon.Has(dragon,ForceDragon.ArroganceGene) && attacker.HostileTo(dragon) && (attacker.skills?.GetSkill(SkillDefOf.Melee)?.Level??0)<(dragon.skills?.GetSkill(SkillDefOf.Melee)?.Level??0);
  static void Postfix(Verb_MeleeAttack __instance,LocalTargetInfo __0,ref float __result){if(Applies(__instance.CasterPawn,__0.Thing as Pawn))__result*=Factor;}
 }
 [HarmonyPatch(typeof(InteractionWorker_Insult),nameof(InteractionWorker_Insult.RandomSelectionWeight))]
 public static class ForceDragonInsults {
  static void Postfix(Pawn __0,Pawn __1,ref float __result){if(ForceDragon.Has(__0,ForceDragon.ArroganceGene) && !ForceDragon.Active(__1))__result*=3f;}
 }
 // 肉食者：不挨饿时只愿意吃含肉的食物。
 public static class ForceDragonDiet {
  public static bool Refuses(Pawn p,ThingDef def,Thing food){
   // 婴儿照常喝奶、吃婴儿食品。
   if(def?.ingestible==null || !def.IsNutritionGivingIngestible || def.IsDrug || !ForceDragon.Has(p,ForceDragon.CarnivoreGene) || (p.DevelopmentalStage&(DevelopmentalStage.Newborn|DevelopmentalStage.Baby))!=0 || FoodUtility.Starving(p))return false;
   return food!=null?!FoodUtility.AcceptableCarnivore(food):FoodUtility.UnacceptableCarnivore(def);
  }
 }
 [HarmonyPatch(typeof(FoodUtility),nameof(FoodUtility.WillEat),new[]{typeof(Pawn),typeof(Thing),typeof(Pawn),typeof(bool),typeof(bool)})]
 public static class ForceDragonDietThing {
  static void Postfix(Pawn __0,Thing __1,ref bool __result){if(__result && ForceDragonDiet.Refuses(__0,__1?.def,__1))__result=false;}
 }
 [HarmonyPatch(typeof(FoodUtility),nameof(FoodUtility.WillEat),new[]{typeof(Pawn),typeof(ThingDef),typeof(Pawn),typeof(bool),typeof(bool)})]
 public static class ForceDragonDietDef {
  static void Postfix(Pawn __0,ThingDef __1,ref bool __result){if(__result && ForceDragonDiet.Refuses(__0,__1,null))__result=false;}
 }
 [HarmonyPatch(typeof(Thing),nameof(Thing.Ingested))]
 public static class ForceDragonIngest {
  static void Prefix(Thing __instance,out bool __state){__state=ForceDragon.IsDragonFood(__instance) && FoodUtility.AcceptableCarnivore(__instance);}
  static void Postfix(Thing __instance,Pawn __0,bool __state){
   if(__0==null || !ForceDragon.IsDragonFood(__instance))return;
   if(__state){if(ForceDragon.Active(__0))ForceDragon.Gain(__0,ForceDragon.MeatForce);}
   else if(ForceDragon.Has(__0,ForceDragon.CarnivoreGene))__0.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("LP_DragonAteNonMeat"));
  }
 }
 // 伟龙攻击后短暂现形。
 [HarmonyPatch(typeof(Verb),"TryCastNextBurstShot")]
 public static class ForceDragonRevealOnAttack {
  static void Postfix(Verb __instance){
   var p=__instance.CasterPawn;if(p==null || !ForceDragon.Active(p))return;
   p.health.hediffSet.GetFirstHediffOfDef(ForceDragon.Invisibility)?.TryGetComp<HediffComp_Invisibility>()?.DisruptInvisibility();
  }
 }
}
