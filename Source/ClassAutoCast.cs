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
   var p=h?.pawn;var d=h?.classes;if(p==null || d==null || p.Faction==null || !p.Faction.IsPlayer)return null;
   if(d.autocast.Count>0 && Ready(p))return NpcClasses.Cast(p,instant,a=>d.autocast.Contains(a.def) && AutoCastPlus.ResourceOk(h,a)); // 1.136.0 资源低于设定的百分比就不自动用
   if(d.autoModes!=null && d.autoModes.Count>0 && AutoCastPlus.ReadyCalm(p))return NpcClasses.Cast(p,instant,a=>AutoCastPlus.Undrafted(p,a.def) && AutoCastPlus.Helpful(a) && AutoCastPlus.ResourceOk(h,a)); // 1.136.0 未征召时自动施放（只限治疗 / 增益 / 防御）
   return null;
  }
 }

 // 职业能力按钮：左上角的自动施放开关（点它只切换，不施放）；右键菜单里也能切换。
 public class Command_ClassAbility : Command_Ability {
  public Command_ClassAbility(Ability ability,Pawn pawn):base(ability,pawn){}
  Ability_Class A=>Ability as Ability_Class;
  bool Shown=>ClassAutoCast.Supports(A);
  // 1.136.0 热修（Peter：开关和齿轮挡住了技能图标）：只剩右上角一个 15 像素的半透明小角标。左键切换征召时自动施放，右键打开自动施放设置窗口。
  public const float BadgeSize=15f;
  public static Rect Box(Rect r)=>new Rect(r.xMax-BadgeSize-2f,r.y+2f,BadgeSize,BadgeSize);
  protected override GizmoResult GizmoOnGUIInt(Rect r,GizmoRenderParms parms){
   var box=Box(r);
   if(Shown && Event.current.type==EventType.MouseDown && Mouse.IsOver(box)){ // 先于升环右键处理，角标上的右键只开设置
    if(Event.current.button==0){ClassAutoCast.Toggle(A);(ClassAutoCast.IsOn(A)?SoundDefOf.Checkbox_TurnedOn:SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();Event.current.Use();}
    else if(Event.current.button==1){SoundDefOf.Tick_High.PlayOneShotOnCamera();Find.WindowStack.Add(new Dialog_AutoCast(A));Event.current.Use();}
   }
   Spellbook.RightClick(A,r); // 1.128.0 可升环的法师法术：右键选环位（窗口，不用浮动菜单）
   if(!Shown)return base.GizmoOnGUIInt(r,parms);
   var res=base.GizmoOnGUIInt(r,parms);
   bool on=ClassAutoCast.IsOn(A);bool extra=AutoCastPlus.Undrafted(A.pawn,A.def) || AutoCastPlus.Threshold(A.pawn,A.def)>0;
   bool over=Mouse.IsOver(box);var c=GUI.color;
   Widgets.DrawBoxSolid(box,new Color(0f,0f,0f,over?.55f:.3f));
   GUI.color=new Color(1f,1f,1f,over || on?1f:.55f);GUI.DrawTexture(box.ContractedBy(1f),on?Widgets.CheckboxOnTex:Widgets.CheckboxOffTex);GUI.color=c;
   if(extra){var dot=new Rect(box.x-1f,box.yMax-4f,5f,5f);Widgets.DrawBoxSolid(dot,new Color(1f,.82f,.35f));} // 有未征召 / 资源门槛设置时左下角一个小金点
   if(over)TooltipHandler.TipRegion(box,"自动施放："+(on?"开":"关")+"（左键切换）\n右键：自动施放设置（未征召时自动施放："+(AutoCastPlus.Undrafted(A.pawn,A.def)?"开":"关")+"，资源低于 "+AutoCastPlus.Threshold(A.pawn,A.def)+"% 不自动用）\n\n开着时，征召待命或战斗中会自己放这个能力：治疗先救倒地、受伤的友军，增益给正在战斗、还没有这个效果的友军，攻击能力打射程内的敌人，范围法术不会炸到自己人。不会打断你下的移动命令。");
   return res;
  }
  public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions{get{
   foreach(var o in base.RightClickFloatMenuOptions)yield return o;
   if(Shown){var a=A;yield return new FloatMenuOption(ClassAutoCast.IsOn(a)?"关闭自动施放":"开启自动施放",()=>ClassAutoCast.Toggle(a));}
  }}
 }
}
