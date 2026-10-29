using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.22.0 种族特色装备。race：只有该种族能在工作台制作（按 1.20.0 的基因规则）；raceStatOffsets：只有该种族穿戴/手持时生效的属性加成。
 public class RaceGearExtension : DefModExtension {
  public string race;
  public List<StatModifier> raceStatOffsets=new List<StatModifier>();
 }
 public static class RaceGear {
  public const float RuneShieldChance=.25f,RuneShieldMountainChance=.45f,AstralWardFactor=.5f;
  public const int AstralWardTicks=180,FrenzyTicks=300,HammerRange=8;
  public static Pawn Owner(Thing gear)=>(gear as Apparel)?.Wearer ?? (gear?.ParentHolder as Pawn_EquipmentTracker)?.pawn;
  public static string CannotCraft(RecipeDef recipe,Pawn p){
   var ext=recipe?.ProducedThingDef?.GetModExtension<RaceGearExtension>()??recipe?.GetModExtension<RaceGearExtension>();
   if(ext?.race==null || Items.Is(p,ext.race))return null;
   return "只有"+Items.RaceLabel(ext.race)+"才能制作"+(recipe.ProducedThingDef?.label??recipe.label)+"。";
  }
  // 矮人投掷锤：8格内的目标受到一次锤头伤害（按武器品质与材料），锤子随即飞回手中。
  public static float HammerDamage(Pawn caster){
   var w=caster?.equipment?.Primary;if(w==null || w.def.defName!="LP_DwarvenThrowingHammer")return 0;
   float power=w.def.tools?.Max(t=>t.power)??15f;return power*w.GetStatValue(StatDefOf.MeleeWeapon_DamageMultiplier);
  }
  public static bool HammerThrow(Pawn caster,Pawn target){
   float dmg=HammerDamage(caster);if(dmg<=0 || target==null || target.Dead)return false;
   target.TakeDamage(new DamageInfo(DamageDefOf.Blunt,dmg,.1f,(target.Position-caster.Position).AngleFlat,caster,null,caster.equipment.Primary.def));
   if(target.Spawned){FleckMaker.Static(target.Position,target.Map,FleckDefOf.ShotHit_Dirt,1.2f);Items.Mote(caster,"飞锤回手",new Color(.75f,.85f,1f));}
   return true;
  }
  // 矮人符文盾：概率格挡远程攻击；在有厚岩顶的山体下格挡率更高。
  public static float BlockChance(Pawn p)=>Items.Worn(p,"LP_DwarvenRuneShield")==null?0f:Gene_DwarfStoneEndurance.UnderThickRoof(p)?RuneShieldMountainChance:RuneShieldChance;
  // 牛头人角盔：牛头人自身蛮牛冲撞+3格；其他人得到4格、伤害减半的弱化冲撞。
  public static bool HelmBoost(Pawn p)=>Items.IsMinotaur(p) && Items.Worn(p,"LP_MinotaurHornHelm")!=null;
  // 夺心魔触须披风：同地图有人穿着它时，其他非夺心魔殖民者 -2 心情（不叠加）。按地图缓存，每250刻重算。
  static readonly Dictionary<int,(int tick,bool any)> cloakCensus=new Dictionary<int,(int,bool)>();
  public static bool CloakSeen(Pawn viewer){
   if(viewer?.Map==null)return false;var map=viewer.Map;int now=Find.TickManager.TicksGame;
   if(!cloakCensus.TryGetValue(map.uniqueID,out var c) || now-c.tick>=250 || now<c.tick){bool any=false;var def=Items.Thing("LP_IllithidTendrilCloak");foreach(var p in map.mapPawns.AllPawnsSpawned)if(Items.Worn(p,def)!=null){any=true;break;}c=(now,any);cloakCensus[map.uniqueID]=c;}
   if(!c.any)return false;
   if(Items.Worn(viewer,"LP_IllithidTendrilCloak")!=null)return map.mapPawns.AllPawnsSpawned.Any(p=>p!=viewer && Items.Worn(p,"LP_IllithidTendrilCloak")!=null);
   return true;
  }
  public static void ResetCloakCensus()=>cloakCensus.Clear();
  // 骨质护符：敌方死灵造物不会主动把佩戴者当作攻击目标；佩戴者10秒内攻击过死灵造物就照常还手。
  public static bool UndeadIgnore(Pawn wearer,Pawn construct){
   if(wearer==null || construct==null || Necro.Construct(construct)==null || Items.Worn(wearer,"LP_BoneTalisman")==null)return false;
   var ms=wearer.mindState;if(ms!=null && Find.TickManager.TicksGame-ms.lastAttackTargetTick<600 && ms.lastAttackedTarget.Thing is Pawn hit && Necro.Construct(hit)!=null)return false;
   return true;
  }
 }
 // 制作限制：种族装备只能由本族成员开工。
 [HarmonyPatch(typeof(Bill),nameof(Bill.PawnAllowedToStartAnew))]
 public static class RaceGearCrafting {
  static void Postfix(Bill __instance,Pawn p,ref bool __result){if(!__result)return;string reason=RaceGear.CannotCraft(__instance.recipe,p);if(reason==null)return;__result=false;JobFailReason.Is(reason);}
 }
 // 种族加成属性：只在本族成员穿戴或手持时计入。
 [HarmonyPatch(typeof(StatWorker),nameof(StatWorker.StatOffsetFromGear))]
 public static class RaceGearStats {
  static void Postfix(Thing gear,StatDef stat,ref float __result){
   var ext=gear?.def.GetModExtension<RaceGearExtension>();if(ext==null || ext.raceStatOffsets.Count==0)return;
   var owner=RaceGear.Owner(gear);if(owner==null || !Items.Is(owner,ext.race))return;
   __result+=ext.raceStatOffsets.GetStatOffsetFromList(stat);
  }
 }
 // 装备附带能力：穿戴或拿起时获得，卸下时失去（还有其他装备提供同一能力时保留）。
 public class CompProperties_GrantAbility : CompProperties {
  public AbilityDef ability;public string notForRace;
  public CompProperties_GrantAbility(){compClass=typeof(CompGrantAbility);}
 }
 public class CompGrantAbility : ThingComp {
  public CompProperties_GrantAbility Props=>(CompProperties_GrantAbility)props;
  public override void Notify_Equipped(Pawn pawn){base.Notify_Equipped(pawn);Sync(pawn);}
  public override void Notify_Unequipped(Pawn pawn){base.Notify_Unequipped(pawn);Sync(pawn);}
  public static bool Grants(Pawn p,AbilityDef def){
   foreach(var a in p.apparel?.WornApparel??new List<Apparel>()){var c=a.GetComp<CompGrantAbility>();if(c!=null && c.Props.ability==def && (c.Props.notForRace==null || !Items.Is(p,c.Props.notForRace)))return true;}
   var w=p.equipment?.Primary?.GetComp<CompGrantAbility>();return w!=null && w.Props.ability==def && (w.Props.notForRace==null || !Items.Is(p,w.Props.notForRace));
  }
  public void Sync(Pawn p){
   if(p?.abilities==null || Props.ability==null)return;
   bool want=Grants(p,Props.ability),has=p.abilities.GetAbility(Props.ability)!=null;
   if(want && !has)p.abilities.GainAbility(Props.ability);else if(!want && has)p.abilities.RemoveAbility(Props.ability);
  }
 }
 public class CompProperties_ItemAbility : CompProperties_AbilityEffect {
  public string effect;public int range;public float damageFactor=1f;
  public CompProperties_ItemAbility(){compClass=typeof(CompItemAbility);}
 }
 public class CompItemAbility : CompAbilityEffect {
  public CompProperties_ItemAbility Props=>(CompProperties_ItemAbility)props;
  Pawn Caster=>parent.pawn;
  public string Invalid(LocalTargetInfo target){
   var map=Caster?.Map;if(map==null)return "施法者不在地图上。";
   switch(Props.effect){
    case "hammer":if(RaceGear.HammerDamage(Caster)<=0)return "需要手持矮人投掷锤。";var p=target.Pawn;if(p==null || p.Dead || p==Caster || !p.Spawned || p.Map!=map)return "需要地图上的其他存活角色。";return p.Position.DistanceTo(Caster.Position)>RaceGear.HammerRange+.5f?"超出投掷距离（8格）。":null;
    case "helmcharge":var c=target.Cell;if(!c.IsValid || !c.InBounds(map) || c==Caster.Position)return "需要冲撞的方向。";return c.DistanceTo(Caster.Position)>Props.range+.5f?"超出冲撞距离（"+Props.range+"格）。":null;
    case "speed":return null;
   }
   return "未知的能力。";
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){string reason=Invalid(target);if(reason!=null && throwMessages)Messages.Message(reason,MessageTypeDefOf.RejectInput,false);return reason==null && base.Valid(target,throwMessages);}
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   if(Invalid(target)!=null)return;
   switch(Props.effect){
    case "hammer":RaceGear.HammerThrow(Caster,target.Pawn);break;
    case "helmcharge":Minotaur.Charge(Caster,target.Cell,Props.range,Props.damageFactor);break;
    case "speed":Items.Give(Caster,"LP_SpeedBurst");break;
   }
  }
  public override void DrawEffectPreview(LocalTargetInfo target){if(Props.effect=="helmcharge" && Caster?.Map!=null && target.Cell.IsValid)GenDraw.DrawFieldEdges(Minotaur.ChargeLine(Caster,target.Cell,Props.range));}
 }
 // 矮人投掷锤：矮人使用时没有冷却。
 [HarmonyPatch(typeof(Ability),nameof(Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class DwarfHammerNoCooldown {
  static void Postfix(Ability __instance,bool __result){if(__result && __instance.def.defName=="LP_HammerThrow" && CommonRaces.IsDwarf(__instance.pawn))__instance.ResetCooldown();}
 }
 // 矮人符文盾：格挡远程攻击。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class RuneShieldBlock {
  static bool Prefix(Pawn __instance,ref DamageInfo __0,ref bool __1){
   if(__0.Def==null || !ForceDragon.IsAttack(__instance,__0) || !ForceDragon.IsRanged(__0))return true;
   float chance=RaceGear.BlockChance(__instance);if(chance<=0 || !Rand.Chance(chance))return true;
   __1=true;Items.Mote(__instance,"符文盾格挡",new Color(.6f,.85f,1f));return false;
  }
 }
 // 精灵长弓：精灵手持时射击不受黑暗惩罚。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class ElvenLongbowDarkness {
  static readonly AccessTools.StructFieldRef<ShotReport,float> dark=AccessTools.StructFieldRefAccess<ShotReport,float>("offsetFromDarkness");
  static readonly DefRef<ThingDef> Longbow=new DefRef<ThingDef>("LP_ElvenLongbow");
  static void Postfix(Thing caster,ref ShotReport __result){if(caster is Pawn p && p.equipment?.Primary!=null && p.equipment.Primary.def==Longbow.Def && Items.IsElf(p) && dark(ref __result)<0)dark(ref __result)=0;}
 }
 // 半兽人巨斧：击倒或杀死敌人后5秒近战冷却-30%。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class GreataxeFrenzy {
  // 敌对关系在受击前判断（死人不再算敌对）。
  static void Prefix(Thing __instance,DamageInfo __0,out bool __state){__state=__instance is Pawn p && !p.Dead && !p.Downed && __0.Weapon?.defName=="LP_OrcishGreataxe" && __0.Instigator is Pawn a && a.HostileTo(p);}
  static void Postfix(Thing __instance,DamageInfo __0,bool __state){
   if(!__state || !(__instance is Pawn victim) || !(victim.Dead || victim.Downed))return;
   if(__0.Instigator is Pawn axe && Items.Holds(axe,"LP_OrcishGreataxe"))Items.Give(axe,"LP_AxeFrenzy",RaceGear.FrenzyTicks);
  }
 }
 // 星界战甲：吉斯洋基星界跳跃后3秒受伤减半。
 [HarmonyPatch(typeof(Githyanki),nameof(Githyanki.Step))]
 public static class AstralArmorWard {
  static void Postfix(Pawn caster,bool __result){if(__result && Items.IsGith(caster) && Items.Worn(caster,"LP_GithAstralArmor")!=null)Items.Give(caster,"LP_AstralWard",RaceGear.AstralWardTicks);}
 }
 // 蜕鳞甲：免疫 EMP 眩晕；力能龙穿不了。
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.Notify_DamageApplied))]
 public static class ShedArmorEmpImmunity {
  static bool Prefix(Pawn ___parentPawn,DamageInfo __0)=>__0.Def!=DamageDefOf.EMP || Items.Worn(___parentPawn,"LP_DragonShedArmor")==null;
 }
 public static class ShedArmorDragon {public static bool Forbidden(Pawn p,ThingDef def)=>def?.defName=="LP_DragonShedArmor" && ForceDragon.Active(p);}
 [HarmonyPatch(typeof(ApparelUtility),nameof(ApparelUtility.HasPartsToWear),new Type[]{typeof(Pawn),typeof(ThingDef)})]
 public static class ShedArmorDragonParts {static void Postfix(Pawn __0,ThingDef __1,ref bool __result){if(__result && ShedArmorDragon.Forbidden(__0,__1))__result=false;}}
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Wear),new Type[]{typeof(Apparel),typeof(bool),typeof(bool)})]
 public static class ShedArmorDragonWear {static bool Prefix(Pawn ___pawn,Apparel __0)=>!ShedArmorDragon.Forbidden(___pawn,__0?.def);}
 // 夺心魔触须披风：恐惧类能力更强（被吓到的人额外眩晕2秒，动物眩晕×1.5）。
 [HarmonyPatch(typeof(LichSpells),nameof(LichSpells.Frighten))]
 public static class TendrilCloakFear {
  static void Postfix(Pawn caster,Pawn p,bool __result){if(__result && Items.Worn(caster,"LP_IllithidTendrilCloak")!=null && !p.Dead)p.stances?.stunner?.StunFor(p.RaceProps.Humanlike?120:LichSpells.FearStunTicks/2,caster,false,true,false);}
 }
 public class ThoughtWorker_TendrilCloak : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.Spawned && p.IsColonist && !MindFlayer.IsMindFlayer(p) && RaceGear.CloakSeen(p)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }
 // 骨质护符。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.ThreatDisabled))]
 public static class BoneTalismanThreat {
  // 1.58.6：AI 索敌对每一对（搜索者，目标）都会调用。先查目标戴没戴骨质护符（几乎没人戴），再查敌对；UndeadIgnore 本身的判断不变。
  static readonly DefRef<ThingDef> Talisman=new DefRef<ThingDef>("LP_BoneTalisman");
  static void Postfix(Pawn __instance,IAttackTargetSearcher __0,ref bool __result){if(!__result && Items.Worn(__instance,Talisman.Def)!=null && __0?.Thing is Pawn searcher && searcher.HostileTo(__instance) && RaceGear.UndeadIgnore(__instance,searcher))__result=true;}
 }
}
