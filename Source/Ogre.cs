using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.24.0 食人魔：体型与蠢笨都在 XML（SmallBodyExtension、属性加成）里；这里是食人魔大棒和“太重的武器”规则。
 // HeavyWeaponExtension 挂在武器上：使用者体型（含巨躯规则来源的实际体型）低于 minBodySize 时伤害×damageFactor、移速加 moveOffset。
 public class HeavyWeaponExtension : DefModExtension {
  public float minBodySize=1.4f,damageFactor=1f,moveOffset;
 }
 public static class HeavyWeapons {
  public static HeavyWeaponExtension Ext(Thing weapon)=>weapon?.def.GetModExtension<HeavyWeaponExtension>();
  public static bool TooSmall(Pawn p,HeavyWeaponExtension e)=>p!=null && e!=null && p.BodySize<e.minBodySize-.0001f;
  public static float DamageFactor(Pawn p,Thing weapon){var e=Ext(weapon);return TooSmall(p,e)?e.damageFactor:1f;}
 }
 public static class Ogre {
  public const float ClubKnockChance=.2f;
  public static bool IsClub(ThingDef def)=>def!=null && def.defName=="LP_OgreClub";
  public static bool TryKnock(Pawn victim,Pawn attacker,float roll){
   if(roll>=ClubKnockChance || victim?.Map==null || attacker==null)return false;
   var dest=DNDPush.Destination(victim.Map,victim.Position,(victim.Position-attacker.Position).ToVector3(),1);
   if(dest==victim.Position)return false;
   DNDPush.Move(victim,dest);Items.Mote(victim,"被砸飞",new Color(.9f,.75f,.5f));return true;
  }
 }
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply")]
 public static class HeavyWeaponDamage {
  static IEnumerable<DamageInfo> Postfix(IEnumerable<DamageInfo> values,Verb_MeleeAttackDamage __instance){
   float f=HeavyWeapons.DamageFactor(__instance.CasterPawn,__instance.EquipmentSource);
   foreach(var d in values){if(f!=1f){var copy=d;copy.SetAmount(d.Amount*f);yield return copy;}else yield return d;}
  }
 }
 [HarmonyPatch(typeof(StatWorker),nameof(StatWorker.StatOffsetFromGear))]
 public static class HeavyWeaponMove {
  static void Postfix(Thing gear,StatDef stat,ref float __result){
   if(stat!=StatDefOf.MoveSpeed)return;var e=HeavyWeapons.Ext(gear);if(e==null || e.moveOffset==0f)return;
   if(HeavyWeapons.TooSmall(RaceGear.Owner(gear),e))__result+=e.moveOffset;
  }
 }
 // 食人魔大棒：钝击命中有20%概率把目标击退1格。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class OgreClubKnockback {
  static void Postfix(Thing __instance,DamageInfo __0){
   if(!(__instance is Pawn victim) || victim.Dead || !victim.Spawned || !Ogre.IsClub(__0.Weapon) || !(__0.Instigator is Pawn a) || !a.Spawned || __0.Def!=DamageDefOf.Blunt)return;
   Ogre.TryKnock(victim,a,Rand.Value);
  }
 }
}
