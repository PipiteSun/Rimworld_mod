using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.22.0 种族特色装备（1.143.0 起投掷锤、符文盾、长弓、角盔、巨斧、星界战甲、蜕鳞甲已移除）。race：只有该种族能在工作台制作（按 1.20.0 的基因规则）；raceStatOffsets：只有该种族穿戴/手持时生效的属性加成。
 public class RaceGearExtension : DefModExtension {
  public string race;
  public List<StatModifier> raceStatOffsets=new List<StatModifier>();
 }
 public static class RaceGear {
  public static Pawn Owner(Thing gear)=>(gear as Apparel)?.Wearer ?? (gear?.ParentHolder as Pawn_EquipmentTracker)?.pawn;
  public static string CannotCraft(RecipeDef recipe,Pawn p){
   var ext=recipe?.ProducedThingDef?.GetModExtension<RaceGearExtension>()??recipe?.GetModExtension<RaceGearExtension>();
   if(ext?.race==null || Items.Is(p,ext.race))return null;
   return "只有"+Items.RaceLabel(ext.race)+"才能制作"+(recipe.ProducedThingDef?.label??recipe.label)+"。";
  }
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
    case "speed":return null;
   }
   return "未知的能力。";
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){string reason=Invalid(target);if(reason!=null && throwMessages)Messages.Message(reason,MessageTypeDefOf.RejectInput,false);return reason==null && base.Valid(target,throwMessages);}
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   if(Invalid(target)!=null)return;
   switch(Props.effect){
    case "speed":Items.Give(Caster,"LP_SpeedBurst");break;
   }
  }
 }
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
