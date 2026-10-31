using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.84.0 职业能力自动施放（Test.ps1 -Only AutoCast，4 条 + 读档 1 条）。
 public partial class LichChecks {
  static string autoSavedId;
  void CheckAutoCast(){
   var made=new List<Thing>();
   try{
    // 1. 按钮：AI 会用的职业能力都换成带开关的按钮；殖民者有开关、默认关，敌人没有；开 / 关能切换。
    var defs=DefDatabase<AbilityDef>.AllDefsListForReading.Where(d=>d.abilityClass==typeof(Ability_Class)).ToList();
    var b=WlPawn(5,5);var hb=Adventurer.Get(b);var blast=b.abilities.GetAbility(Ab("LP_WL_EldritchBlast")) as Ability_Class;
    var npc=NpcPawn(AdvEnemy,5,0,10);ClassSystem.SetLevel(Adventurer.Get(npc),WL,5);var npcBlast=npc.abilities.GetAbility(Ab("LP_WL_EldritchBlast")) as Ability_Class;
    bool def0=ClassAutoCast.IsOn(blast);var gizmo=blast?.GetGizmos().FirstOrDefault();
    ClassAutoCast.Toggle(blast);bool on=ClassAutoCast.IsOn(blast);ClassAutoCast.Toggle(blast);bool off=!ClassAutoCast.IsOn(blast);
    Check(defs.Count>50 && defs.All(d=>d.gizmoClass==typeof(Command_ClassAbility)) && blast!=null && gizmo is Command_ClassAbility && ClassAutoCast.Supports(blast) && npcBlast!=null && !ClassAutoCast.Supports(npcBlast)
     && !def0 && on && off,"class ability buttons carry the autocast switch ("+defs.Count+" defs); colonists only, default off, toggles on/off");
    // 2. 征召的邪术师身边有敌人：开着魔能爆 → 自己开始施放；关着、没征召又不在战斗中都不放。
    GenSpawn.Spawn(b,ScCell(-12,12),map);made.Add(b);var foe=ScFoe(b);made.Add(foe);
    b.mindState.lastHarmTick=-99999;b.mindState.lastAttackTargetTick=-99999;b.mindState.enemyTarget=null;
    var calmOff=ClassAutoCast.Tick(hb);ClassAutoCast.Set(b,blast.def,true);var calmOn=ClassAutoCast.Tick(hb);
    b.drafter.Drafted=true;var used=ClassAutoCast.Tick(hb);
    Check(calmOff==null && calmOn==null && used==blast && b.CurJob?.ability==blast,"a drafted colonist warlock with 魔能爆 on starts casting it at the adjacent enemy; nothing while off or undrafted out of combat");
    // 3. 不打断走位：征召后玩家让他走路时不放。
    b.jobs.EndCurrentJob(JobCondition.InterruptForced);var dest=CellFinder.RandomClosewalkCellNear(b.Position,map,6,c=>c!=b.Position && ScFree(c));
    var go=JobMaker.MakeJob(JobDefOf.Goto,dest);go.playerForced=true;b.jobs.StartJob(go,JobCondition.InterruptForced);var moving=ClassAutoCast.Tick(hb);
    Check(b.CurJobDef==JobDefOf.Goto && moving==null,"autocast never interrupts a player move order");
    // 4. 治疗：征召的牧师开着治愈术 → 救旁边倒地的殖民者。
    var cl=ClsPawn(5,0,10);var hc=Adventurer.Get(cl);ClassSystem.SetLevel(hc,Cls("Cleric"),5);hc.classes.res[ClassSystem.PoolKey(Cls("Cleric"))]=ClassSystem.MaxResource(hc,Cls("Cleric"));
    GenSpawn.Spawn(cl,ScCell(12,12),map);made.Add(cl);var ally=ClsPawn(1);GenSpawn.Spawn(ally,NextTo(cl),map);made.Add(ally);HealthUtility.DamageUntilDowned(ally,false);
    var heals=cl.abilities.abilities.OfType<Ability_Class>().Where(a=>a.Props?.effect=="Heal" || a.Props?.effect=="MassHeal").ToList();foreach(var a in heals)ClassAutoCast.Set(cl,a.def,true);
    cl.drafter.Drafted=true;float inj=ClassMelee.InjurySum(ally);var heal=ally.Dead?null:ClassAutoCast.Tick(hc,true);
    Check(heals.Count>0 && !ally.Dead && heal!=null && ClassMelee.InjurySum(ally)<inj,"a drafted colonist cleric with healing on heals the downed colonist next to them ("+heal?.def.label+", "+inj.ToString("0")+" → "+ClassMelee.InjurySum(ally).ToString("0")+")");
   }finally{foreach(var t in made)if(!t.Destroyed)t.Destroy();foreach(var p in advMade)if(!p.Destroyed)p.Destroy();advMade.Clear();}
  }
  void PrepareAutoCastSave(){
   var p=WlPawn(5,5);advMade.Remove(p);ClassAutoCast.Set(p,Ab("LP_WL_EldritchBlast"),true);GenSpawn.Spawn(p,ScCell(-14,14),map);autoSavedId=p.ThingID;
  }
  void CheckAutoCastAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==autoSavedId);var h=Adventurer.Get(p);
   Check(h?.classes!=null && h.classes.autocast.Count==1 && h.classes.autocast[0].defName=="LP_WL_EldritchBlast","the autocast switch survives save/load");
   p?.Destroy();
  }
 }
}
