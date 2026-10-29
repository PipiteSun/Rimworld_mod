using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.24.0 种族物品：元素符（Genasi.cs）、象牙号角、食人魔大棒（Ogre.cs）、风羽披风、霜刺长枪（Fiends.cs）、炎魔火鞭、天穹之剑、神灯（Genie.cs）。
 public static class RaceItems124 {
  public const int HornRange=8,LoxodonHornRange=12,HornTicks=1800,WhipPull=2,WhipSelfBurn=2;
  public const float CloakMoveFactor=1.08f,BladeExtraChance=.25f,BladeEmp=10f,BladeFlame=6f;
  // 象牙号角：使用者8格内（象人吹12格）的友方30秒恐惧免疫、移速×1.1。
  public static int HornRadius(Pawn p)=>Races124.IsLoxodon(p)?LoxodonHornRange:HornRange;
  public static int Horn(Pawn caster){
   var map=caster.Map;if(map==null)return 0;int n=0;
   foreach(var p in Races124.PawnsNear(map,caster.Position,HornRadius(caster)).ToList())if(p==caster || (p.Faction!=null && p.Faction==caster.Faction)){Items.Give(p,"LP_HornCourage",HornTicks);n++;}
   FleckMaker.Static(caster.Position,map,FleckDefOf.PsycastAreaEffect,HornRadius(caster));
   return n;
  }
  // 风羽披风：非鸟人穿着时移速×1.08（鸟人的加成在翱翔上）。
  static readonly DefRef<ThingDef> feather=new DefRef<ThingDef>("LP_FeatherCloak");
  public static float CloakFactor(Pawn p,StatDef stat)=>stat==StatDefOf.MoveSpeed && Items.Worn(p,feather.Def)!=null && !Races124.IsAarakocra(p)?CloakMoveFactor:1f; // 1.58.6：静态 Def
  // 炎魔火鞭：拉近2格（击退的反向）。
  public static IntVec3 Pull(Pawn victim,Thing launcher){
   if(victim?.Map==null || launcher==null || victim.Dead)return victim?.Position??IntVec3.Invalid;
   var dir=(launcher.Position-victim.Position).ToVector3();if(dir.sqrMagnitude<.01f)return victim.Position;
   int cells=Mathf.Min(WhipPull,Mathf.Max(0,Mathf.FloorToInt(dir.magnitude)-1));
   var dest=DNDPush.Destination(victim.Map,victim.Position,dir,cells);DNDPush.Move(victim,dest);return dest;
  }
  // 非火免者用火鞭：每次挥鞭手上受2点烧伤。
  public static bool BurnHand(Pawn p){
   if(p==null || p.Dead || Races124.FireProof(p))return false;
   var hand=p.health.hediffSet.GetNotMissingParts().FirstOrDefault(x=>x.def.defName=="Hand") ?? p.health.hediffSet.GetNotMissingParts().FirstOrDefault(x=>x.def.defName=="Arm");
   p.TakeDamage(new DamageInfo(DamageDefOf.Burn,WhipSelfBurn,0f,-1f,null,hand,Items.Thing("LP_BalorWhip")));
   return true;
  }
 }
 public class Projectile_BalorLash : Bullet {
  protected override void Impact(Thing hitThing,bool blockedByShield=false){
   var l=launcher;base.Impact(hitThing,blockedByShield);
   if(blockedByShield || !(hitThing is Pawn p) || p.Dead || !p.Spawned)return;
   FireUtility.TryAttachFire(p,.5f,l);
   RaceItems124.Pull(p,l);
  }
 }
 [HarmonyPatch(typeof(Verb),"TryCastNextBurstShot")]
 public static class BalorWhipBurn {
  static void Postfix(Verb __instance){if(__instance.EquipmentSource?.def.defName=="LP_BalorWhip")RaceItems124.BurnHand(__instance.CasterPawn);}
 }
 // 天穹之剑：命中有25%概率追加一次雷霆（EMP 10 + 火焰 6）。先于“太重的武器”打折运行，所以雷霆也跟着打折。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply")]
 [HarmonyPriority(Priority.High)]
 public static class EmpyreanBladeThunder {
  static IEnumerable<DamageInfo> Postfix(IEnumerable<DamageInfo> values,Verb_MeleeAttackDamage __instance){
   foreach(var d in values)yield return d;
   var w=__instance.EquipmentSource;if(w?.def.defName!="LP_EmpyreanBlade" || !Rand.Chance(RaceItems124.BladeExtraChance))yield break;
   var caster=__instance.CasterPawn;
   yield return new DamageInfo(DamageDefOf.EMP,RaceItems124.BladeEmp,0f,-1f,caster,null,w.def);
   yield return new DamageInfo(DamageDefOf.Flame,RaceItems124.BladeFlame,0f,-1f,caster,null,w.def);
  }
 }
}
