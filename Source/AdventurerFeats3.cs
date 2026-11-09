using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.140.0 三件事都在这个文件：
 // ① 5e 战斗专长 6 个（Defs/Adventurer_Feats4.xml）：双持者、盾牌大师、长柄武器大师、弩专家、冲锋者、擒抱者。规则效果走 HeroInfo 缓存的 f3 位（Feats2.Build 里设置）。
 // ② 史诗恩赐补 4 个（Defs/Adventurer_Epic.xml）：能量抗性、恢复、不可抗之攻、法术追忆（Boons2）。
 // ③ 施法职业奖励专长（CasterFeats）：法师、牧师、德鲁伊、术士、邪术师、吟游诗人的职业等级 1 / 5 / 10 / 15 各 1 个免费专长，只能换法术类专长（战争施法者、法术狙击、魔法学徒、元素使、
 //    仪式施法者、心灵感应、妖精之触、坚毅心智、治疗者、幸运、警觉、坚韧、观察者、技能专家、音乐家、演员，以及施法职业自己的职业专属专长）。
 //    和战士奖励专长一样不占属性提升；NPC 立即随机选。存档字段 Hediff_Adventurer.casterFeats / casterFeatGranted。
 public static class Feats3 {
  public const int FDual=1,FShield=2,FPolearm=4,FCrossbow=8,FCharger=16,FGrappler=32;
  public const float ShieldDodge=10f,ShieldShove=.25f,PolearmFactor=.5f,PolearmMass=2f,CrossbowWarmup=.7f,CrossbowCooldown=.8f,ChargerFactor=1.3f,ChargerPush=.25f,GrapplerChance=.25f,GrapplerFactor=1.5f,GrapplerSize=1.5f;
  public const int ShieldStun=60,PolearmInterval=180,ChargerMoved=60,GrappleTicks=180;
  public static int Flag(string defName){
   switch(defName){case "LP_Feat_DualWielder":return FDual;case "LP_Feat_ShieldMaster":return FShield;case "LP_Feat_PolearmMaster":return FPolearm;case "LP_Feat_CrossbowExpert":return FCrossbow;case "LP_Feat_Charger":return FCharger;case "LP_Feat_Grappler":return FGrappler;}
   return 0;
  }
  public static bool Has(Pawn p,int f){var i=HeroTraits.Info(p);return i!=null && (i.f3 & f)!=0;}
  public static float? TestRoll;
  static float Roll=>TestRoll??Rand.Value;
  public static int Shoved,Butts,Charged,Grappled;
  public static bool HasShield(Pawn p){var list=p?.apparel?.WornApparel;if(list==null)return false;for(int i=0;i<list.Count;i++)if(list[i].GetComp<CompShield>()!=null)return true;return false;}
  // 长柄：质量 ≥2 的近战武器，带刺击或砍击的工具。
  public static bool Polearm(Thing w){
   if(w?.def==null || !w.def.IsMeleeWeapon || w.def.tools==null || w.GetStatValue(StatDefOf.Mass)<PolearmMass)return false;
   foreach(var t in w.def.tools)if(t.capacities!=null)foreach(var c in t.capacities)if(c.defName=="Stab" || c.defName=="Cut" || c.defName=="Poke")return true;
   return false;
  }
  public static bool Polearm(Pawn p)=>Polearm(p?.equipment?.Primary);
  // 弓弩：中世纪以下科技的远程武器。
  public static bool Bow(Thing w)=>w?.def!=null && w.def.IsRangedWeapon && w.def.techLevel<=TechLevel.Medieval;
  public static bool Bow(Pawn p)=>Bow(p?.equipment?.Primary);
  public static bool MeleeHit(DamageInfo d)=>d.Weapon!=null && d.Def!=null && !d.Def.isRanged && !d.Def.isExplosive && (d.Weapon.IsMeleeWeapon || d.Weapon.race!=null);
  public static bool Helpless(Pawn v)=>v.Downed || v.stances?.stunner?.Stunned==true || Items.Has(v,"LP_FeatGrappled");
  public static bool Charging(Pawn a)=>a?.pather!=null && a.pather.MovedRecently(ChargerMoved);
  // 攻击者一侧的伤害倍率（冲锋者、擒抱者）。
  public static float OutFactor(Pawn a,HeroInfo i,Pawn v,DamageInfo d){
   if(i.f3==0 || v==null || !MeleeHit(d) || !v.HostileTo(a))return 1f;float f=1f;
   if((i.f3 & FCharger)!=0 && Charging(a))f*=ChargerFactor;
   if((i.f3 & FGrappler)!=0 && Helpless(v))f*=GrapplerFactor;
   return f;
  }
  // 命中之后（盾牌大师推开、冲锋者撞开、擒抱者抓住、长柄武器大师补一下）。
  public static void AfterHit(Pawn v,Pawn a,HeroInfo ai,DamageInfo d){
   if(ai.f3==0 || v.Dead || !v.Spawned || !v.HostileTo(a) || !MeleeHit(d))return;
   if((ai.f3 & FShield)!=0 && HasShield(a) && Roll<ShieldShove){Push(v,a);if(!v.Dead && !v.Downed)v.stances?.stunner?.StunFor(ShieldStun,a,false,true,false);Shoved++;Items.Mote(v,"盾击",new Color(.8f,.8f,.9f));}
   else if((ai.f3 & FCharger)!=0 && Charging(a) && Roll<ChargerPush){Push(v,a);Charged++;Items.Mote(v,"撞开",new Color(.95f,.7f,.4f));}
   if((ai.f3 & FGrappler)!=0 && !v.Dead && !v.Downed && v.BodySize<=a.BodySize*GrapplerSize && !Items.Has(v,"LP_FeatGrappled") && Roll<GrapplerChance){Items.Give(v,"LP_FeatGrappled",GrappleTicks);v.pather?.StopDead();Grappled++;Items.Mote(v,"被擒抱",new Color(.9f,.6f,.5f));}
   if((ai.f3 & FPolearm)!=0 && Polearm(a) && !HeroCombat.extra && !v.Dead){
    int now=GenTicks.TicksGame;buttTick.TryGetValue(a.thingIDNumber,out int last);
    if(last<=0 || now-last>=PolearmInterval || now<last){buttTick[a.thingIDNumber]=Math.Max(1,now);var tv=v;var me=a;Fiends.Defer(()=>Butt(me,tv));}
   }
  }
  static readonly Dictionary<int,int> buttTick=new Dictionary<int,int>();
  public static void ResetCaches(){buttTick.Clear();}
  static void Push(Pawn v,Pawn a){var dest=DNDPush.Destination(v.Map,v.Position,(v.Position-a.Position).ToVector3(),1);if(dest!=v.Position)DNDPush.Move(v,dest);}
  public static bool Butt(Pawn a,Pawn v){
   if(a==null || v==null || v.Dead || !v.Spawned || a.Dead || !a.Spawned || !v.Position.AdjacentTo8WayOrInside(a.Position))return false;
   Items.Give(a,"LP_WhirlwindFury",60,PolearmFactor);HeroCombat.extra=true;
   try{ClassMelee.Strike(a,v);}finally{HeroCombat.extra=false;Items.Remove(a,"LP_WhirlwindFury");}
   Butts++;Items.Mote(v,"枪尾",new Color(.85f,.75f,.55f));return true;
  }
  // 盾牌大师：戴护盾腰带时近战闪避再 +10（Verb_MeleeAttack.GetDodgeChance 后缀）。
  public static float DodgeBonus(Pawn v)=>Has(v,FShield) && HasShield(v)?ShieldDodge/100f:0f;
  // 弩专家：弓弩的预热 ×0.7（Stance_Warmup 构造）、冷却 ×0.8（VerbProperties.AdjustedCooldown）。
  public static int Warmup(Verb verb,int ticks){var p=verb?.CasterPawn;if(ticks<=0 || p==null || verb.IsMeleeAttack || verb.EquipmentSource==null || !Bow(verb.EquipmentSource) || !Has(p,FCrossbow))return ticks;return Mathf.Max(1,Mathf.RoundToInt(ticks*CrossbowWarmup));}
  public static float Cooldown(Verb verb,Pawn p,float v){if(verb==null || p==null || verb.IsMeleeAttack || verb.EquipmentSource==null || !Bow(verb.EquipmentSource) || !Has(p,FCrossbow))return v;return v*CrossbowCooldown;}
 }

 // ---------- 1.140.0 史诗恩赐补 4 个 ----------
 public static class Boons2 {
  public const float ResistFactor=.5f,OffensePen=.5f,RecoveryThreshold=30f,RecoveryTick=1f,SpellRecallCooldown=.8f;
  static LP_EpicBoonDef B(ref LP_EpicBoonDef c,string n)=>c??(c=DefDatabase<LP_EpicBoonDef>.GetNamedSilentFail(n));
  static LP_EpicBoonDef resist,recovery,offense,recall;
  public static LP_EpicBoonDef Resistance=>B(ref resist,"LP_Boon_Resistance");
  public static LP_EpicBoonDef Recovery=>B(ref recovery,"LP_Boon_Recovery");
  public static LP_EpicBoonDef Offense=>B(ref offense,"LP_Boon_Offense");
  public static LP_EpicBoonDef SpellRecall=>B(ref recall,"LP_Boon_SpellRecall");
  public static bool Energy(DamageDef d){if(d==null)return false;if(d==DamageDefOf.Flame || d==DamageDefOf.Burn || d==DamageDefOf.EMP)return true;var n=d.defName;return n=="LP_Frost" || n=="LP_Shock" || n=="AcidBurn" || n=="LP_Radiant" || n=="LP_Necrotic" || n=="LP_Acid" || n=="LP_Lightning";}
  public static float InFactor(Pawn v,DamageInfo d)=>Energy(d.Def) && Epic.Has(v,Resistance)?ResistFactor:1f;
  public static bool Offensive(Pawn a,DamageInfo d)=>a!=null && Feats3.MeleeHit(d) && Epic.Has(a,Offense);
  static readonly Dictionary<int,int> recoveryDay=new Dictionary<int,int>();
  public static void ResetCaches(){recoveryDay.Clear();}
  // 恢复之恩：每 250 刻（Hediff_Adventurer 的刷新里调用）。
  public static void Tick250(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null || p.Dead || h.boons.Count==0 || !Epic.Has(p,Recovery))return;
   float sum=ClassMelee.InjurySum(p);if(sum<=0f)return;
   int today=GenDate.DaysPassed+1;recoveryDay.TryGetValue(p.thingIDNumber,out int last); // +1：测试地图第 0 天也算没用过
   if(sum>RecoveryThreshold && last!=today){recoveryDay[p.thingIDNumber]=today;ClassMelee.Heal(p,sum*.5f);foreach(var i in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>i.Bleeding).ToList())i.Tended(.5f,1f);Items.Mote(p,"恢复之恩",new Color(1f,.8f,.3f));return;}
   ClassMelee.Heal(p,RecoveryTick);
  }
  public static int CostReduce(Pawn p,int cost)=>cost>1 && Epic.Has(p,SpellRecall)?1:0;
 }

 // ---------- 1.140.0 施法职业奖励专长 ----------
 public static class CasterFeats {
  public static readonly int[] Levels={1,5,10,15};
  static readonly HashSet<string> Names=new HashSet<string>{"LP_Feat_WarCaster","LP_Feat_SpellSniper","LP_Feat_MagicInitiate","LP_Feat_ElementalAdept_Fire","LP_Feat_ElementalAdept_Cold","LP_Feat_ElementalAdept_Lightning","LP_Feat_ElementalAdept_Acid",
   "LP_Feat_RitualCaster","LP_Feat_Telepathic","LP_Feat_FeyTouched","LP_Feat_Resilient_Mind","LP_Feat_Resilient_Toxic","LP_Feat_Resilient_Immune","LP_Feat_Healer","LP_Feat_Lucky","LP_Feat_Alert","LP_Feat_Tough","LP_Feat_Observant","LP_Feat_Skilled","LP_Feat_Musician","LP_Feat_Actor"};
  // 法术类专长：名单里的，或施法职业自己的职业专属专长。
  public static bool IsCasterFeat(LP_FeatDef f)=>f!=null && (Names.Contains(f.defName) || f.requiredClass!=null && f.requiredClass.caster);
  public static bool CasterClass(LP_ClassDef c)=>c!=null && c.caster;
  // 施法职业里最高的等级（多职业施法者只按最高的算一条水位线）。
  public static int CasterLevel(Hediff_Adventurer h){
   if(h?.classes==null)return 0;int best=0;
   foreach(var kv in h.classes.levels)if(kv.Key!=null && kv.Key.caster && kv.Value>best)best=kv.Value;
   return best;
  }
  public static int Due(int casterLevel)=>ClassSystem.FeatsDue(Levels,casterLevel);
  public static bool Pending(Hediff_Adventurer h)=>h!=null && h.casterFeatGranted<Levels.Length;
  // 补发：返回这次发了几个（SyncFighterFeats 里调用，一起发信）。
  public static int Grant(Hediff_Adventurer h,List<string> who){
   if(h==null)return 0;int lv=CasterLevel(h);int due=Due(lv);if(due<=h.casterFeatGranted)return 0;
   int gave=due-h.casterFeatGranted;h.casterFeatGranted=due;h.casterFeats+=gave;
   var c=h.classes.levels.Where(kv=>kv.Key!=null && kv.Key.caster && kv.Value==lv).Select(kv=>kv.Key).FirstOrDefault();
   who.Add((c?.label??"施法职业")+" "+lv+" 级（施法奖励）");return gave;
  }
  public static string Text=>"施法奖励专长：法师、牧师、德鲁伊、术士、邪术师、吟游诗人的职业等级 1、5、10、15 级各 1 个，免费、不占属性提升，只能换法术类专长（战争施法者、法术狙击、魔法学徒、元素使、仪式施法者、心灵感应、妖精之触、坚毅心智、治疗者、幸运、警觉、坚韧、观察者、技能专家、音乐家、演员，以及施法职业自己的职业专属专长）。";
 }

 // ==================== 挂钩 ====================
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetDodgeChance")]
 public static class Feats3Dodge {
  static void Postfix(LocalTargetInfo __0,ref float __result){if(__0.Thing is Pawn v && __result<1f){float b=Feats3.DodgeBonus(v);if(b>0f)__result=Mathf.Min(1f,__result+b);}}
 }
 [HarmonyPatch(typeof(Stance_Warmup),MethodType.Constructor,new[]{typeof(int),typeof(LocalTargetInfo),typeof(Verb)})]
 public static class Feats3Warmup {static void Prefix(ref int ticks,Verb verb){ticks=Feats3.Warmup(verb,ticks);}}
 [HarmonyPatch(typeof(VerbProperties),nameof(VerbProperties.AdjustedCooldown),new[]{typeof(Verb),typeof(Pawn)})]
 public static class Feats3Cooldown {static void Postfix(Verb ownerVerb,Pawn attacker,ref float __result){__result=Feats3.Cooldown(ownerVerb,attacker,__result);}}
 // 恩赐：能量抗性（受害者）、不可抗之攻（攻击者的穿透）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class Boons2Damage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(dinfo.Def==null || dinfo.Amount<=0f || !dinfo.Def.harmsHealth || !Adventurer.Enabled)return;
   var a=dinfo.Instigator as Pawn;
   if(a!=null && a!=__instance && Boons2.Offensive(a,dinfo))Subclass6.SetPen(ref dinfo,dinfo.ArmorPenetrationInt+Boons2.OffensePen);
   float f=Boons2.InFactor(__instance,dinfo);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.StartCooldown))]
 public static class Boons2Cooldown {
  static void Prefix(RimWorld.Ability __instance,ref int ticks){if(ticks>0 && __instance is Ability_Class && Epic.Has(__instance.pawn,Boons2.SpellRecall))ticks=Mathf.Max(1,Mathf.RoundToInt(ticks*Boons2.SpellRecallCooldown));}
 }
 [HarmonyPatch(typeof(ClassLegendary),nameof(ClassLegendary.Cost))]
 public static class Boons2Cost {static void Postfix(Pawn p,ref int __result){int r=Boons2.CostReduce(p,__result);if(r>0)__result=Math.Max(1,__result-r);}}
}
