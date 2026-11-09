using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.137.0 技能分组（Test.ps1 -Only AbilityGroups，6 条 + 读档 1 条）。
 public partial class LichChecks {
  static string groupSavedId;
  static List<AbilityDef> GizmoAbilities(Pawn p)=>p.GetGizmos().OfType<Command_Ability>().Select(c=>c.Ability.def).ToList();
  void CheckAbilityGroups(){
   var made=new List<Thing>();
   try{
    var p=WlPawn(9,9);var h=Adventurer.Get(p);GenSpawn.Spawn(p,ScCell(-10,10),map);made.Add(p);p.drafter.Drafted=true;
    var blast=Ab("LP_WL_EldritchBlast");
    foreach(var d in Cls("Fighter").abilities.Concat(Cls("Barbarian").abilities).Select(e=>e.ability).Where(d=>d!=null && d.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault()?.passive!=true).Distinct().Take(6))
     if(p.abilities.GetAbility(d)==null)p.abilities.GainAbility(d); // 高等级的人能力多：再给几个战士 / 野蛮人能力
    var all=GizmoAbilities(p);var listable=AbilityGroups.Listable(p);
    // 1. 没有分组：按钮栏原样；能力够多时有切换按钮，标签「全部」。
    var sw0=p.GetGizmos().OfType<Command_AbilityGroup>().ToList();
    Check(all.Count>=AbilityGroups.MinAbilitiesForSwitch && all.Contains(blast) && sw0.Count==1 && sw0[0].Label=="技能分组：全部" && AbilityGroups.Get(p)==null,
     "no groups: all "+all.Count+" ability buttons shown, one switch button labelled 全部, nothing stored");
    // 2. 新建「战斗」只放魔能爆并设为当前：按钮栏只剩魔能爆（带自动施放角标的按钮类），征召等其他按钮还在。
    int otherBefore=p.GetGizmos().Count(g=>!(g is Command_Ability) && !(g is Command_AbilityGroup));
    var g1=AbilityGroups.Add(p,"战斗");g1.Set_(blast,true);AbilityGroups.SetActive(p,0);
    var cmds=p.GetGizmos().OfType<Command_Ability>().ToList();int otherAfter=p.GetGizmos().Count(g=>!(g is Command_Ability) && !(g is Command_AbilityGroup));
    Check(cmds.Count==1 && cmds[0].Ability.def==blast && cmds[0] is Command_ClassAbility && otherAfter==otherBefore && p.GetGizmos().OfType<Command_AbilityGroup>().Single().Label=="技能分组：战斗",
     "active group 战斗 shows only 魔能爆 (still the autocast-badge button), other buttons untouched ("+otherAfter+")");
    // 2b. 热修：没征召时，组里「要征召才显示」的能力也显示（可以放）；「全部」时照原版不显示。
    {var dOnly=all.FirstOrDefault(d=>!d.displayGizmoWhileUndrafted && d!=blast);
     p.drafter.Drafted=false;var allUndrafted=GizmoAbilities(p);
     var gU=AbilityGroups.Add(p,"未征召");gU.Set_(dOnly,true);gU.Set_(blast,true);int gu=AbilityGroups.Get(p).groups.IndexOf(gU);AbilityGroups.SetActive(p,gu);
     var shown=GizmoAbilities(p);var cmd=p.GetGizmos().OfType<Command_Ability>().FirstOrDefault(c=>c.Ability.def==dOnly);
     AbilityGroups.SetActive(p,-1);var backAll=GizmoAbilities(p);
     AbilityGroups.Remove(p,gu);AbilityGroups.SetActive(p,0);p.drafter.Drafted=true;
     Check(dOnly!=null && !allUndrafted.Contains(dOnly) && shown.Contains(dOnly) && shown.Contains(blast) && shown.Count==2 && cmd!=null && !cmd.Disabled && !backAll.Contains(dOnly),
      "[hotfix] undrafted: active group also shows its drafted-only abilities ("+dOnly?.label+", usable), 全部 still hides it");}
    // 3. 一个能力可以在多个组；切换：全部 → 战斗 → 其他 → 全部；空组什么能力都不显示。
    var other=all.First(d=>d!=blast);var g2=AbilityGroups.Add(p);g2.Set_(other,true);g2.Set_(blast,true);
    AbilityGroups.SetActive(p,-1);var seq=new List<string>();for(int i=0;i<3;i++){AbilityGroups.Cycle(p);seq.Add(AbilityGroups.ActiveLabel(p));}
    var g3=AbilityGroups.Add(p,"空");AbilityGroups.SetActive(p,2);int empty=GizmoAbilities(p).Count;
    Check(string.Join(",",seq)=="战斗,"+g2.Label+",全部" && g2.Label=="分组 2" && empty==0 && AbilityGroups.Get(p).groups.Count(x=>x.Has(blast))==2,"cycle 全部 → 战斗 → 分组 2 → 全部 ("+string.Join(",",seq)+"); empty group shows no ability; one ability in two groups");
    // 4. 删除 / 移动：删掉当前组回到「全部」；删掉前面的组时当前组下标跟着走；上移下移当前组跟着走。
    AbilityGroups.SetActive(p,1);AbilityGroups.Remove(p,0);bool follow=AbilityGroups.ActiveLabel(p)==g2.Label;
    AbilityGroups.Move(p,0,1);bool moved=AbilityGroups.Get(p).active==1 && AbilityGroups.ActiveLabel(p)==g2.Label;
    AbilityGroups.Remove(p,1);bool back=AbilityGroups.Get(p).active==-1 && GizmoAbilities(p).Count==all.Count;
    Check(follow && moved && back,"remove/move keep the active group pointer right; removing the active group falls back to 全部");
    // 5. 只影响按钮：当前组里没有魔能爆时，开着自动施放的魔能爆照样自动放；分组不碰 Spellbook.Hidden / CanCast。
    AbilityGroups.SetActive(p,0); // 「空」组
    var ab=p.abilities.GetAbility(blast) as Ability_Class;ClassAutoCast.Set(p,blast,true);
    Check(!GizmoAbilities(p).Contains(blast) && !Spellbook.Hidden(p,blast) && ab.CanCast.Accepted,"filtered-out ability stays castable (not Spellbook.Hidden, CanCast accepted)");
    var foe=ScFoe(p);made.Add(foe);var used=ClassAutoCast.Tick(h);
    Check(used==ab,"autocast still fires 魔能爆 while it is filtered out of the bar");
    ClassAutoCast.Set(p,blast,false);
    // 6. 热路径：同一个人取按钮，选了分组（3 个能力）不比「全部」慢（每帧只多一次字典查找，集合只在改分组时重建）。
    var g4=AbilityGroups.Add(p,"测速");foreach(var d in all.Take(3))g4.Set_(d,true);int gi=AbilityGroups.Get(p).groups.Count-1;
    for(int i=0;i<50;i++)p.GetGizmos().Count();
    double tf=0,tn=0;var sw=new Stopwatch();
    for(int rep=0;rep<3;rep++){
     AbilityGroups.SetActive(p,gi);sw.Restart();for(int i=0;i<300;i++)p.GetGizmos().Count();tf+=sw.Elapsed.TotalMilliseconds/900;
     AbilityGroups.SetActive(p,-1);sw.Restart();for(int i=0;i<300;i++)p.GetGizmos().Count();tn+=sw.Elapsed.TotalMilliseconds/900;
    }
    AbilityGroups.SetActive(p,gi);
    Check(GizmoAbilities(p).Count==3 && tf<tn*1.5+0.05,"gizmo pass with a filter "+tf.ToString("0.000")+" ms vs none "+tn.ToString("0.000")+" ms");
   }finally{foreach(var t in made)if(!t.Destroyed)t.Destroy();foreach(var x in advMade)if(!x.Destroyed)x.Destroy();advMade.Clear();}
  }
  // 自测通过后（-lichuitest）：选中一名 10 级牧师，建两个分组，截技能栏（当前组「治疗」）和分组设置窗口。
  static int agFrames;static Pawn agPawn;
  static bool CaptureAbilityGroups(){
   if(agFrames==0)return false;agFrames++;var map=Find.CurrentMap;string dir=GenFilePaths.SaveDataFolderPath;
   try{
    if(agFrames==10){
     agPawn=map.mapPawns.FreeColonistsSpawned.FirstOrDefault();Adventurer.TestSuppressed=false;LichMod.Settings.adventurer=true;
     if(agPawn!=null){var h=Adventurer.Get(agPawn)??Adventurer.TryAdd(agPawn,10,true);h.level=10;h.Dirty();if(h.classes==null)h.classes=new ClassData();
      ClassSystem.SetLevel(h,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Cleric"),10);ClassSystem.Sync(h);
      var all=AbilityGroups.Listable(agPawn);var heal=AbilityGroups.Add(agPawn,"治疗");var fight=AbilityGroups.Add(agPawn,"战斗");
      foreach(var a in all){if(a.def==null)continue;if(!a.def.hostile)heal.Set_(a.def,true);else fight.Set_(a.def,true);}
      var cure=DefDatabase<AbilityDef>.GetNamed("LP_CureWounds");ClassAutoCast.Set(agPawn,cure,true);
      AbilityGroups.SetActive(agPawn,0);
      if(agPawn.drafter!=null)agPawn.drafter.Drafted=true;Find.Selector.ClearSelection();Find.Selector.Select(agPawn);
      Log.Message("[LICH-UI] ability groups: "+all.Count+" abilities, 治疗 "+heal.Set.Count+", 战斗 "+fight.Set.Count);}
     Messages.Clear();
    }
    if(agFrames==40)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"AG-Bar-Heal.png"));
    if(agFrames==50 && agPawn!=null)AbilityGroups.SetActive(agPawn,-1);
    if(agFrames==80)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"AG-Bar-All.png"));
    if(agFrames==90 && agPawn!=null){var w=new Dialog_AbilityGroups(agPawn){Selected=0};Find.WindowStack.Add(w);}
    if(agFrames==120)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"AG-Window.png"));
    if(agFrames==130 && agPawn!=null){foreach(var w in Find.WindowStack.Windows.OfType<Dialog_AbilityGroups>().ToList())w.Close(false);Find.WindowStack.Add(new Dialog_Spellbook(agPawn));}
    if(agFrames==132){foreach(var w in Find.WindowStack.Windows.OfType<Dialog_Spellbook>().ToList())w.Close(false);var cs=CasterSpells.ListsOf(agPawn).FirstOrDefault();if(cs!=null)Find.WindowStack.Add(new Dialog_CasterSpells(agPawn,cs));}
    if(agFrames==160)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"AG-CasterSpells.png"));
    if(agFrames>=170){Log.Message("[LICH-UI] ability groups captured");Application.Quit();}
   }catch(Exception e){Log.Error("[LICH-UI] ability groups capture: "+e);Application.Quit();}
   return true;
  }
  void PrepareAbilityGroupsSave(){
   var p=WlPawn(5,5);advMade.Remove(p);GenSpawn.Spawn(p,ScCell(-14,-14),map);groupSavedId=p.ThingID;
   var g=AbilityGroups.Add(p,"存档组");g.Set_(Ab("LP_WL_EldritchBlast"),true);AbilityGroups.Add(p,"第二组");AbilityGroups.SetActive(p,0);
   var tmp=WlPawn(1,1);AbilityGroups.Get(tmp,true); // 空的不存
   advMade.Remove(tmp);tmp.Destroy();
  }
  void CheckAbilityGroupsAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==groupSavedId);var s=AbilityGroups.Get(p);
   var c=GameComponent_AbilityGroups.Instance;if(p!=null)p.drafter.Drafted=true; // 原版：没征召时只显示 displayGizmoWhileUndrafted 的能力
   Check(s!=null && s.groups.Count==2 && s.active==0 && s.groups[0].name=="存档组" && s.groups[0].Has(Ab("LP_WL_EldritchBlast")) && s.groups[1].Set.Count==0 && c.sets.All(x=>x.groups.Count>0)
    && GizmoAbilities(p).SequenceEqual(new[]{Ab("LP_WL_EldritchBlast")}),"groups, members and the active group survive save/load; empty sets are not saved [pawn "+(p!=null)+" sets "+c?.sets.Count+" groups "+s?.groups.Count+" active "+s?.active+" names "+(s==null?"":string.Join("|",s.groups.Select(x=>x.name+":"+string.Join("+",x.defs))))+" gizmos "+(p==null?"":string.Join(",",GizmoAbilities(p).Select(d=>d.defName)))+"]");
   p?.Destroy();
  }
 }
}
