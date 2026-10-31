using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LichPhylactery {
 // 1.84.0 自动施放（Peter）：玩家的人每个职业能力按钮左上角一个小开关（默认关，存在 ClassData.autocast 里，按人按能力）。
 // 开着的能力每 250 刻检查一次：征召中（站着等 / 正在攻击，不打断走位命令）或没征召但在战斗中（没有玩家指派的工作）时，
 // 用 NPC 施放的选目标逻辑：治疗先救倒地 / 受伤的友军，增益给在打仗、还没有这个状态的友军，攻击法术打射程内的敌人（范围法术不炸到自己人）。
 // 只开放给 AI 会用的能力（NpcClasses.AiUsable：要开窗口、放建筑、自我位移之类的没有开关）。
 [StaticConstructorOnStartup]
 public static class ClassAutoCast {
  static ClassAutoCast(){
   foreach(var d in DefDatabase<AbilityDef>.AllDefsListForReading)
    if(d.abilityClass!=null && typeof(Ability_Class).IsAssignableFrom(d.abilityClass) && (d.gizmoClass==null || d.gizmoClass==typeof(Command_Ability)))d.gizmoClass=typeof(Command_ClassAbility);
  }
  static readonly HashSet<JobDef> DraftedIdle=new HashSet<JobDef>{JobDefOf.Wait_Combat,JobDefOf.Wait,JobDefOf.Wait_MaintainPosture,JobDefOf.AttackStatic,JobDefOf.AttackMelee};
  static ClassData Data(Pawn p)=>Adventurer.Get(p)?.classes;
  public static bool Supports(Ability_Class a)=>a?.pawn!=null && a.pawn.Faction!=null && a.pawn.Faction.IsPlayer && NpcClasses.AiUsable(a);
  public static bool IsOn(Ability_Class a)=>a!=null && Data(a.pawn)?.autocast.Contains(a.def)==true;
  public static void Set(Pawn p,AbilityDef def,bool on){
   var d=Data(p);if(d==null || def==null)return;
   if(on){if(!d.autocast.Contains(def))d.autocast.Add(def);}else d.autocast.Remove(def);
  }
  // 点开关：选中的其他人如果也有这个能力，一起切换成同一个状态。
  public static void Toggle(Ability_Class a){
   bool on=!IsOn(a);Set(a.pawn,a.def,on);
   if(Find.Selector==null)return;
   foreach(var p in Find.Selector.SelectedPawns)if(p!=a.pawn && p.abilities?.GetAbility(a.def) is Ability_Class o && Supports(o))Set(p,a.def,on);
  }
  public static bool Ready(Pawn p){
   if(p==null || !p.Spawned || p.Map==null || p.Dead || p.Downed || p.InMentalState || p.stances?.stunner?.Stunned==true)return false;
   var j=p.CurJob;if(j?.ability!=null)return false;
   if(p.Drafted)return j==null || DraftedIdle.Contains(j.def);
   return NpcClasses.InCombat(p) && (j==null || !j.playerForced);
  }
  // ClassSystem.Tick250 的最后一步。返回放出的能力（检查用；instant：直接生效）。
  public static Ability Tick(Hediff_Adventurer h,bool instant=false){
   var p=h?.pawn;var d=h?.classes;if(p==null || d==null || d.autocast.Count==0 || p.Faction==null || !p.Faction.IsPlayer || !Ready(p))return null;
   return NpcClasses.Cast(p,instant,a=>d.autocast.Contains(a.def));
  }
 }

 // 职业能力按钮：左上角的自动施放开关（点它只切换，不施放）；右键菜单里也能切换。
 public class Command_ClassAbility : Command_Ability {
  public Command_ClassAbility(Ability ability,Pawn pawn):base(ability,pawn){}
  Ability_Class A=>Ability as Ability_Class;
  bool Shown=>ClassAutoCast.Supports(A);
  static Rect Box(Rect r)=>new Rect(r.x+3f,r.y+3f,20f,20f);
  protected override GizmoResult GizmoOnGUIInt(Rect r,GizmoRenderParms parms){
   if(!Shown)return base.GizmoOnGUIInt(r,parms);
   var box=Box(r);
   if(Event.current.type==EventType.MouseDown && Event.current.button==0 && Mouse.IsOver(box)){
    ClassAutoCast.Toggle(A);(ClassAutoCast.IsOn(A)?SoundDefOf.Checkbox_TurnedOn:SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();Event.current.Use();
   }
   var res=base.GizmoOnGUIInt(r,parms);
   bool on=ClassAutoCast.IsOn(A);
   Widgets.DrawBoxSolid(box,new Color(0f,0f,0f,.5f));GUI.DrawTexture(box.ContractedBy(1f),on?Widgets.CheckboxOnTex:Widgets.CheckboxOffTex);
   if(Mouse.IsOver(box))TooltipHandler.TipRegion(box,"自动施放："+(on?"开":"关")+"（点击切换）\n\n开着时，征召待命或战斗中会自己放这个能力：治疗先救倒地、受伤的友军，增益给正在战斗、还没有这个效果的友军，攻击能力打射程内的敌人，范围法术不会炸到自己人。不会打断你下的移动命令。");
   return res;
  }
  public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions{get{
   foreach(var o in base.RightClickFloatMenuOptions)yield return o;
   if(Shown){var a=A;yield return new FloatMenuOption(ClassAutoCast.IsOn(a)?"关闭自动施放":"开启自动施放",()=>ClassAutoCast.Toggle(a));}
  }}
 }
}
