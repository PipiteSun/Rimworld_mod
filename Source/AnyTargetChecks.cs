using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.136.0 热修「技能可对任何人释放」：Test.ps1 -Only AnyTarget。
 // 寒冰射线能打殖民者、治疗伤口能治敌人、祝福能给访客；设置关掉后恢复原来的敌我限制；职业能力的目标过滤打开了人形 / 动物 / 机械体。
 public partial class LichChecks {
  void CheckAnyTarget(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool on=s.adventurer,any=s.anyTarget;s.adventurer=true;s.anyTarget=true;AnyTarget.Apply();
   try{
    var cleric=DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Cleric");
    // 1. 寒冰射线（伤害类）能对殖民者施放并且真的生效。
    {var w=MlClassPawn(Spellbook.Wizard,5,5,-12,-12);var h=Adventurer.Get(w);var ray=Ab("LP_WS_RayOfFrost");Spellbook.LearnCantrip(h,ray);ClassSystem.AddResource(h,Spellbook.Wizard,99f);
     var ally=MlPawn(Faction.OfPlayer,-10,-12);var e=ray.comps.OfType<CompProperties_ClassEffect>().First();string why=ClassEffects.Invalid(w,e,ally);
     var a=w.abilities.GetAbility(ray);bool cast=a!=null && a.Activate(new LocalTargetInfo(ally),new LocalTargetInfo(ally));
     s.anyTarget=false;string off=ClassEffects.Invalid(w,e,ally);s.anyTarget=true;
     Check(why==null && cast && Items.Has(ally,"LP_WS_Chilled") && off!=null && off.Contains("敌人"),"[1.136.0] ray of frost can target a colonist and chills them; with the setting off it is enemies-only again ("+off+")");}
    // 2. 治疗伤口（治疗类）能治敌人。
    {var c=MlClassPawn(cleric,5,5,-12,-9);ClassSystem.AddResource(Adventurer.Get(c),cleric,99f);var foe=MlPawn(MlEnemy,-10,-9);
     foe.TakeDamage(new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,null,foe.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso)));float i0=ClassMelee.InjurySum(foe);
     var cure=Ab("LP_CureWounds");string why=ClassEffects.Invalid(c,cure.comps.OfType<CompProperties_ClassEffect>().First(),foe);var a=c.abilities.GetAbility(cure);bool cast=a.Activate(new LocalTargetInfo(foe),new LocalTargetInfo(foe));float i1=ClassMelee.InjurySum(foe);
     Check(why==null && cast && i1<i0,"[1.136.0] cure wounds can heal an enemy ("+i0.ToString("0.0")+" → "+i1.ToString("0.0")+")");}
    // 3. 祝福（增益类）能给访客（中立派系）；目标过滤打开了机械体。
    {var c=MlClassPawn(cleric,5,5,-12,-6);ClassSystem.AddResource(Adventurer.Get(c),cleric,99f);
     var neutral=Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f=>!f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer) && !f.temporary);
     var guest=neutral==null?null:MlPawn(neutral,-10,-6);var bless=Ab("LP_Bless");var e=bless.comps.OfType<CompProperties_ClassEffect>().First();
     string why=guest==null?"no neutral faction":ClassEffects.Invalid(c,e,guest);bool cast=guest!=null && c.abilities.GetAbility(bless).Activate(new LocalTargetInfo(guest),new LocalTargetInfo(guest));
     var tp=Ab("LP_CureWounds").verbProperties.targetParams;
     Check(why==null && cast && Items.Has(guest,"LP_Blessed") && tp.canTargetMechs && tp.canTargetAnimals && tp.canTargetHumans,"[1.136.0] bless can be given to a visitor ("+neutral?.Name+"); class abilities can target humans, animals and mechs");}
   }finally{s.adventurer=on;s.anyTarget=any;AnyTarget.Apply();Adventurer.TestSuppressed=sup;MlClean();}
  }
 }
}
