using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.120.0 战士第四个子职业「奥术射手」（Defs/Subclass_ArcaneArcher.xml，战士 3 / 7 / 11 / 15 级）。
 // 和泰坦战士一样不改共享分派表：Tick250、能力检查、能力效果用 Harmony 挂在 ClassSystem.Tick250 / ClassEffects.Invalid / ClassEffects.Apply 上（效果名前缀 AA）。
 // 奥术射击都用当前远程武器打出一发必中的子弹（Martial.Fire），命中时由 ShotMod.onHit 结算附加效果；被动按缓存（Users）在伤害前缀里乘。
 public static class ArcaneArcher {
  public const string Sub="LP_Sub_ArcaneArcher";
  public static readonly Color ArcherColor=new Color(.4f,.75f,.95f);
  public const float LorePer=.015f,FarFactor=1.15f,FarRange=12f,BurstRadius=2f,StormRadius=4f;
  public const int GraspTicks=600,ShadowTicks=900,StormMax=10;
  public const int FLore=1,FKeenEye=2;
  static readonly Dictionary<string,int> FlagOf=new Dictionary<string,int>{{"LP_AA_ArcaneLore",FLore},{"LP_AA_KeenEye",FKeenEye}};
  public sealed class Entry {public int flags,level;}
  public static readonly Dictionary<int,Entry> Users=new Dictionary<int,Entry>();
  public static Entry Of(Pawn p){if(p==null || Users.Count==0)return null;Users.TryGetValue(p.thingIDNumber,out var e);return e;}
  static readonly DefRef<LP_ClassDef> fighter=new DefRef<LP_ClassDef>("LP_Class_Fighter");
  public static int Level(Pawn p)=>ClassSystem.Level(p,fighter.Def);
  static bool Hostile(Pawn a,Pawn t)=>AnyTarget.Hostile(a,t); // 1.136.0 热修：手动选的目标不分敌我
  public static bool Busy;public static int LastBurst; // LastBurst：上一次爆裂箭打中几个（自检用）

  public static int Refresh(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return 0;int f=0;
   if(!p.Dead && ClassSubclass.Has(h,Sub)){var list=p.abilities?.abilities;if(list!=null)for(int k=0;k<list.Count;k++)if(FlagOf.TryGetValue(list[k].def.defName,out int b))f|=b;}
   if(f==0){Users.Remove(p.thingIDNumber);return 0;}
   int lv=Level(p);Users.TryGetValue(p.thingIDNumber,out var e);
   if(e==null)Users[p.thingIDNumber]=e=new Entry();
   e.flags=f;e.level=lv;return f;
  }
  public static void Refresh(Pawn p){var h=Adventurer.Get(p);if(h!=null)Refresh(h);}
  public static void Tick250(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null || Users.Count==0 && !ClassSubclass.Has(h,Sub))return;
   if(p.Dead){Users.Remove(p.thingIDNumber);return;}
   Refresh(h);
  }

  // ---------- 被动：奥术箭术 / 鹰眼 ----------
  // 奥术箭术：拿远程武器射击（含奥术射击）伤害 +1.5% × 战士等级（20 级 +30%）。鹰眼：目标离自己 12 格以上时再 ×1.15。
  public static float LoreFactor(int level)=>1f+LorePer*Mathf.Clamp(level,0,Adventurer.MaxLevel);
  public static bool RangedShot(DamageInfo d)=>d.Weapon!=null && d.Weapon.IsRangedWeapon && !ClassMartial2.Melee(d);
  public static float HitFactor(Pawn v,DamageInfo d){
   var a=d.Instigator as Pawn;if(a==null || a==v || !a.HostileTo(v) || !RangedShot(d))return 1f;
   var e=Of(a);if(e==null)return 1f;float g=1f;
   if((e.flags & FLore)!=0)g*=LoreFactor(e.level);
   if((e.flags & FKeenEye)!=0 && a.Spawned && v.Spawned && a.Map==v.Map && a.Position.DistanceTo(v.Position)>=FarRange)g*=FarFactor;
   return g;
  }

  // ---------- 奥术射击 ----------
  static void Extra(Pawn a,Pawn t,DamageDef def,float amount){
   if(t==null || t.Dead || !t.Spawned || amount<=0f)return;Busy=true;
   try{t.TakeDamage(new DamageInfo(def,amount,.2f,-1f,a));}finally{Busy=false;}
  }
  public static Projectile Shot(Pawn p,Pawn t,string label,Action<Thing,IntVec3> onHit){
   var proj=Martial.Fire(p,t,new ShotMod{damage=1f,caster=p,onHit=onHit},true);
   if(proj!=null)Items.Mote(p,label,ArcherColor);
   return proj;
  }
  // 束缚箭：命中后荆棘缠身，划伤 + 移动 ×0.5 10 秒。
  public static void Grasp(Pawn p,Pawn t,float power){
   if(t==null || t.Dead)return;Extra(p,t,DamageDefOf.Cut,power);
   if(!t.Dead){Items.Give(t,"LP_AA_Grasped",GraspTicks,1f);Items.Mote(t,"束缚箭",ArcherColor);}
  }
  // 爆裂箭：命中点半径 2 内的敌人（含目标）受力场伤害。
  public static int Burst(Pawn p,Map map,IntVec3 c,float power){
   if(map==null || !c.IsValid)return 0;int n=0;
   foreach(var x in Races124.PawnsNear(map,c,BurstRadius).ToList())if(Hostile(p,x) && !x.Downed){Extra(p,x,Warlock.ForceDamage,power);n++;}
   FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,BurstRadius*1.5f);
   LastBurst=n;return n;
  }
  // 暗影箭：心灵伤害 + 视觉 −60% 15 秒。
  public static void Shadow(Pawn p,Pawn t,float power){
   if(t==null || t.Dead)return;Extra(p,t,Warlock.PsychicDamage,power);
   if(!t.Dead){Items.Give(t,"LP_AA_Shadowed",ShadowTicks,1f);Items.Mote(t,"暗影箭",ArcherColor);}
  }
  // 箭雨：目标点半径 4 内最多 10 个敌人各中一发必中的子弹。
  public static int Storm(Pawn p,IntVec3 c){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var t in Races124.PawnsNear(map,c,StormRadius).Where(x=>Hostile(p,x) && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(c)).Take(StormMax).ToList())
    if(Martial.Fire(p,t,new ShotMod{damage=1f,caster=p},true)!=null)n++;
   Items.Mote(p,"箭雨",ArcherColor);return n;
  }

  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   switch(e.effect){
    case "AAGrasp":case "AABurst":case "AAShadow":{
     var t=target.Thing as Pawn;if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";
     return Martial.RangedVerb(caster)?.Projectile==null?"需要远程武器。":null;}
    case "AAStorm":{var map=caster.Map;if(map==null || !target.Cell.IsValid || !target.Cell.InBounds(map))return "需要选择一个位置。";return Martial.RangedVerb(caster)?.Projectile==null?"需要远程武器。":null;}
   }
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;float pw=ClassEffects.Power(p,e);var me=p;
   switch(e.effect){
    case "AAGrasp":if(Hostile(p,t))Shot(p,t,"束缚箭",(hit,c)=>Grasp(me,hit as Pawn,pw));return true;
    case "AABurst":if(Hostile(p,t)){var map=p.Map;Shot(p,t,"爆裂箭",(hit,c)=>Burst(me,map,c,pw));}return true;
    case "AAShadow":if(Hostile(p,t))Shot(p,t,"暗影箭",(hit,c)=>Shadow(me,hit as Pawn,pw));return true;
    case "AAStorm":Storm(p,target.Cell);return true;
   }
   return false;
  }
  public static void Reset(){Users.Clear();Busy=false;}
 }

 public class GameComponent_ArcaneArcher : GameComponent {
  public GameComponent_ArcaneArcher(Game game){}
  public override void FinalizeInit(){
   base.FinalizeInit();ArcaneArcher.Reset();
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawns){var h=Adventurer.Get(p);if(h!=null && ClassSubclass.Has(h,ArcaneArcher.Sub))ArcaneArcher.Refresh(h);}
  }
 }

 // ==================== Harmony ====================
 [HarmonyPatch(typeof(ClassSystem),nameof(ClassSystem.Tick250))]
 public static class ArcaneArcherTick {static void Postfix(Hediff_Adventurer h)=>ArcaneArcher.Tick250(h);}
 [HarmonyPatch(typeof(ClassEffects),nameof(ClassEffects.Invalid))]
 public static class ArcaneArcherInvalid {static void Postfix(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target,ref string __result){if(__result==null && e?.effect!=null && e.effect.StartsWith("AA"))__result=ArcaneArcher.Invalid(caster,e,target);}}
 [HarmonyPatch(typeof(ClassEffects),nameof(ClassEffects.Apply))]
 public static class ArcaneArcherApply {static bool Prefix(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target)=>p==null || e?.effect==null || !e.effect.StartsWith("AA") || !ArcaneArcher.Apply(p,def,e,target);}
 // 奥术箭术 / 鹰眼：远程命中。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class ArcaneArcherDamage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(ArcaneArcher.Users.Count==0 || ArcaneArcher.Busy || dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   float f=ArcaneArcher.HitFactor(__instance,dinfo);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
}
