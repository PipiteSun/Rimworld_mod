using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.69.0 事件小人入口：靠信件选项推进的 DND 事件，信件关掉了也不会错过——在事件小人（或法师塔残骸、契约书等）身上点“事件：……”按钮，
 // 或者选中殖民者右键它，就能重新打开同一个对话框答复。档案（历史 → 消息）里打开这些信件也能答复。
 // 规则：事件信件继承 LP_EventLetter；选了会结束事件的选项就 Resolve()（之后不再出现）；Live = 事件还在等答复；EventThings = 能打开它的小人和物件。
 public abstract class LP_EventLetter : ChoiceLetter {
  public bool lpResolved;public int lpVer=1;
  // 事件还在等答复（小人还在、还没答复过）。默认和原来一样：还在信件栏里。
  protected virtual bool Live=>!ArchivedOnly;
  // 事件状态本身记着答没答过（Live 已经能判断）：1.69.0 以前存档里关掉的信也能重新答。
  protected virtual bool StateTracked=>false;
  // 其余 1.69.0 以前的信件不知道答没答过：照旧只在信件栏里能答。
  public bool Closed=>lpResolved || (lpVer<1 && !StateTracked && ArchivedOnly) || !Live;
  public virtual IEnumerable<Thing> EventThings=>Enumerable.Empty<Thing>();
  public void Resolve(){lpResolved=true;Find.LetterStack.RemoveLetter(this);}
  static readonly AccessTools.FieldRef<DiaOption,string> OptText=AccessTools.FieldRefAccess<DiaOption,string>("text");
  // 还能答复：没结束，且至少有一个能点的真正选项（关闭、稍后再说不算）。
  public bool Answerable{get{
   if(Closed)return false;
   var close=OptText(Option_Close);var later=OptText(Option_Postpone);
   return Choices.Any(o=>{if(o==null || o.disabled)return false;var t=OptText(o);return t!=close && t!=later && t!="关闭" && t!="稍后再说";});
  }}
  protected static bool Here(Pawn p)=>p!=null && !p.Dead && !p.Destroyed && p.Spawned;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref lpResolved,"lpResolved");Scribe_Values.Look(ref lpVer,"lpVer",0);}
 }

 public static class EventPawnAccess {
  // 信件栏 + 档案里的事件信件（新的在前）。
  public static IEnumerable<LP_EventLetter> AllLetters(){
   var seen=new HashSet<LP_EventLetter>();
   var stack=Find.LetterStack?.LettersListForReading;
   if(stack!=null)for(int i=stack.Count-1;i>=0;i--)if(stack[i] is LP_EventLetter l && seen.Add(l))yield return l;
   var arch=Find.Archive?.ArchivablesListForReading;
   if(arch!=null)for(int i=arch.Count-1;i>=0;i--)if(arch[i] is LP_EventLetter l && seen.Add(l))yield return l;
  }
  public static bool Answerable(LP_EventLetter l){
   if(l==null)return false;
   try{return l.Answerable;}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 事件信件检查出错（"+l.GetType().Name+"）："+e,76910001);return false;}
  }
  // 这个小人 / 物件能打开的、还在等答复的事件信件。
  public static List<LP_EventLetter> For(Thing t){
   var list=new List<LP_EventLetter>();if(t==null)return list;
   foreach(var l in AllLetters()){
    bool hit=false;try{hit=l.EventThings.Contains(t);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 事件信件小人出错（"+l.GetType().Name+"）："+e,76910002);}
    if(hit && Answerable(l))list.Add(l);
   }
   return list;
  }
  public static string Label(LP_EventLetter l)=>"事件："+l.Label;
  public static bool Open(LP_EventLetter l){
   if(!Answerable(l)){Messages.Message("这件事已经了结了。",MessageTypeDefOf.RejectInput,false);return false;}
   l.OpenLetter();return true;
  }
  public static Command_Action Gizmo(LP_EventLetter l)=>new Command_Action{defaultLabel=Label(l),defaultDesc="重新打开“"+l.Label+"”的对话框，在这里答复。信件关掉了也不会错过；答复后这个按钮就消失。",icon=l.def?.Icon??BaseContent.BadTex,action=()=>Open(l)};
  public static IEnumerable<Gizmo> Gizmos(Thing t){
   List<LP_EventLetter> ls=null;
   try{if(t!=null && t.Spawned && Find.Selector.NumSelected==1)ls=For(t);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 事件按钮出错："+e,76910003);}
   return ls==null?Enumerable.Empty<Gizmo>():ls.Select(Gizmo).Cast<Gizmo>();
  }
 }

 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class EventPawnGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   foreach(var g in __result)yield return g;
   foreach(var g in EventPawnAccess.Gizmos(__instance))yield return g;
  }
 }

 // 选中殖民者，右键事件小人（或残骸）：“事件：…（打开对话框）”。
 public class FloatMenuOptionProvider_EventPawn : FloatMenuOptionProvider {
  protected override bool Drafted=>true;protected override bool Undrafted=>true;protected override bool Multiselect=>false;
  protected override bool RequiresManipulation=>false;protected override bool CanSelfTarget=>true;
  public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn,FloatMenuContext context)=>Options(clickedPawn);
  public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing,FloatMenuContext context)=>clickedThing is Pawn?Enumerable.Empty<FloatMenuOption>():Options(clickedThing);
  public static List<FloatMenuOption> Options(Thing t)=>EventPawnAccess.For(t).Select(l=>new FloatMenuOption(EventPawnAccess.Label(l)+"（打开对话框）",()=>EventPawnAccess.Open(l))).ToList();
 }
}
