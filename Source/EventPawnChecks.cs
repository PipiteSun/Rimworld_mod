using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.69.0：Test.ps1 -Only EventPawn（事件小人入口：信件关掉后从事件小人 / 残骸 / 签约人身上重新打开并答复）。
 public partial class LichChecks {
  static string OptLabel(DiaOption o)=>Traverse.Create(o).Field("text").GetValue<string>();
  static List<Dialog_NodeTree> NodeDialogs()=>Find.WindowStack.Windows.OfType<Dialog_NodeTree>().ToList();
  // 按按钮打开对话框，返回对话框里的选项，然后关掉对话框。
  List<DiaOption> OpenViaGizmo(Command_Action giz){
   foreach(var d in NodeDialogs())Find.WindowStack.TryRemove(d,false);
   giz?.action?.Invoke();
   var dlg=NodeDialogs().FirstOrDefault();
   var node=dlg==null?null:Traverse.Create(dlg).Field("curNode").GetValue<DiaNode>();
   var opts=node?.options.ToList()??new List<DiaOption>();
   foreach(var d in NodeDialogs())Find.WindowStack.TryRemove(d,false);
   return opts;
  }
  Command_Action EventGizmo(Thing t,LP_EventLetter l){
   Find.Selector.ClearSelection();Find.Selector.Select(t,false,false);
   var g=(t is Pawn p?p.GetGizmos():t.GetGizmos()).OfType<Command_Action>().FirstOrDefault(c=>c.defaultLabel==EventPawnAccess.Label(l));
   Find.Selector.ClearSelection();return g;
  }
  static Letter_GiantMigration FoodLetter(GiantGroup g)=>Find.LetterStack.LettersListForReading.OfType<Letter_GiantMigration>().LastOrDefault(l=>l.group==g && l.kind=="food");
  static string MigrationKind(){if(Factions.Ember!=null)Factions.SetRelation(Factions.Ember,Faction.OfPlayer,0);return new[]{"ogre","ember","titan"}.FirstOrDefault(GiantMigration.Available);}

  void CheckEventPawn(){
   // 1. 覆盖面：除了没有事件小人的三种（升级、塞伊来信、未到的朝圣者），所有选择信件都走事件信件基类，并给出事件小人 / 物件。
   var skip=new HashSet<string>{"Letter_AdventurerLevel","Letter_FactionChoice","ChoiceLetter_FaithPilgrims"};
   var types=typeof(LP_EventLetter).Assembly.GetTypes().Where(t=>typeof(ChoiceLetter).IsAssignableFrom(t) && !t.IsAbstract).ToList();
   var bad=types.Where(t=>!skip.Contains(t.Name) && (!typeof(LP_EventLetter).IsAssignableFrom(t) || t.GetProperty("EventThings",BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly)==null)).Select(t=>t.Name).ToList();
   Check(types.Count>=13 && bad.Count==0,"all "+(types.Count-skip.Count)+" event choice letters can be reopened from their event pawns"+(bad.Count>0?"; missing: "+string.Join(",",bad):""));

   // 2. 巨人迁徙：讨食信关掉后，选中巨人有按钮、右键巨人有选项，打开的是同一个对话框；在里面答复后入口消失。
   var kind=MigrationKind();
   if(kind==null){Check(false,"no migration group available for the event-pawn test");return;}
   var g=GiantMigration.Start(map,kind);GiantMigration.OnCamp(g);
   var food=FoodLetter(g);var giant=g.Alive.First();
   Check(food!=null && food.lpVer==1 && EventPawnAccess.For(giant).Contains(food),"food letter in the stack is also reachable from the giant");
   Find.LetterStack.RemoveLetter(food);
   Check(food.ArchivedOnly && !food.Closed && EventPawnAccess.Answerable(food) && g.Alive.All(p=>EventPawnAccess.For(p).Contains(food)),"after dismissing the letter, every giant in the group still opens it");
   var giz=EventGizmo(giant,food);
   Check(giz!=null && giz.defaultLabel=="事件：饥饿的巨人" && giz.icon!=null,"selected giant shows the button "+giz?.defaultLabel);
   var menu=FloatMenuOptionProvider_EventPawn.Options(giant);
   Check(menu.Count==1 && menu[0].Label=="事件：饥饿的巨人（打开对话框）" && menu[0].action!=null,"right-clicking the giant offers "+string.Join(" / ",menu.Select(m=>m.Label)));
   var opts=OpenViaGizmo(giz);var refuse=opts.FirstOrDefault(o=>OptLabel(o)=="不给");
   Check(refuse!=null && !refuse.disabled && opts.Any(o=>OptLabel(o).StartsWith("给他们食物")),"the button opens the food dialog with its real choices ("+string.Join(" / ",opts.Select(OptLabel))+")");
   GiantMigration.TestRoll=.99f;refuse.action();GiantMigration.TestRoll=null;
   Check(g.answered && food.lpResolved && food.Closed && EventPawnAccess.For(giant).Count==0 && EventGizmo(giant,food)==null && FloatMenuOptionProvider_EventPawn.Options(giant).Count==0,"answering from the reopened dialog ends the event: button and menu option are gone");
   Check(!EventPawnAccess.Open(food) && NodeDialogs().Count==0,"an answered event will not open again");
   // 档案里打开（原版历史页走 OpenLetter）：关掉的信也给真正的选项。
   var g2=GiantMigration.Start(map,kind);GiantMigration.OnCamp(g2);var food2=FoodLetter(g2);Find.LetterStack.RemoveLetter(food2);
   Check(food2.Choices.Any(o=>OptLabel(o)=="不给" && !o.disabled),"a dismissed letter opened from the archive still has its choices");

   // 3. 旧存档（1.69.0 以前的信）：事件状态能判断的（巨人讨食）照样能答；判断不了的（云巨人使节）只在信件栏里能答。
   food2.lpVer=0;Check(!food2.Closed && EventPawnAccess.For(g2.Alive.First()).Contains(food2),"old-save food letter: the giants still open it (state tracked)");
   var envoy=(Letter_CloudChoice)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_CloudChoiceLetter"));
   var noble=g2.Alive.First();envoy.kind="envoy";envoy.map=map;envoy.pawn=noble;envoy.Label="上位者的使节";envoy.Text="测试";Find.LetterStack.ReceiveLetter(envoy);
   Check(EventPawnAccess.For(noble).Contains(envoy),"envoy letter opens from the envoy");
   Find.LetterStack.RemoveLetter(envoy);bool newOk=!envoy.Closed;envoy.lpVer=0;bool oldClosed=envoy.Closed;envoy.lpVer=1;
   Check(newOk && oldClosed,"dismissed envoy letter: new letters stay open, old-save ones stay closed");
   noble.DeSpawn();bool gone=envoy.Closed;GenSpawn.Spawn(noble,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);
   Check(gone && !envoy.Closed,"the envoy letter closes while the envoy is off the map");
   envoy.lpResolved=true;

   // 4. 法师塔残骸：选中残骸有按钮，右键残骸有选项；“自己拆”以后都消失。
   TowerCrash.TryFindSpot(map,out var cell);
   var w=(Building_TowerWreck)ThingMaker.MakeThing(TowerCrash.WreckDef);GenSpawn.Spawn(w,cell,map);w.Setup(true);
   var tl=Find.LetterStack.LettersListForReading.OfType<Letter_TowerCrash>().LastOrDefault(l=>l.wreck==w);
   if(tl!=null)Find.LetterStack.RemoveLetter(tl);
   var wg=tl==null?null:EventGizmo(w,tl);var wm=FloatMenuOptionProvider_EventPawn.Options(w);
   var wo=OpenViaGizmo(wg);var self=wo.FirstOrDefault(o=>OptLabel(o)=="自己拆");
   Check(tl!=null && wg!=null && wm.Count==1 && self!=null && !self.disabled,"tower wreck: button and right-click reopen the choice ("+string.Join(" / ",wo.Select(OptLabel))+")");
   self?.action();
   Check(w.choice=="self" && tl!=null && tl.Closed && EventPawnAccess.For(w).Count==0,"choosing 自己拆 from the wreck ends the event");
   foreach(var x in w.guards.ToList())if(x!=null && !x.Destroyed)x.Destroy();if(!w.Destroyed)w.Destroy();

   // 5. 契约信（殖民者身上）：调换儿信关掉后，选中孩子有按钮；“养大它”以后消失。
   if(Pacts.Letter!=null){
    var kid=EvPawn();
    var pl=PactLetters.Send("changeling",null,kid,null,"调换儿","测试");Find.LetterStack.RemoveLetter(pl);
    var pg=EventGizmo(kid,pl);var po=OpenViaGizmo(pg);var raise=po.FirstOrDefault(o=>OptLabel(o)=="养大它");
    Check(pg!=null && raise!=null,"pact letter: the colonist reopens it ("+string.Join(" / ",po.Select(OptLabel))+")");
    raise?.action();
    Check(pl.lpResolved && EventPawnAccess.For(kid).Count==0,"choosing an option ends the pact letter");
   }else Log.Message("[LICH-TEST] skip pact event-pawn check (pacts off)");
  }

  // 存读档：关掉的讨食信读档后仍能从巨人身上打开；答复过的仍是结束状态。
  static int epOpenId=-1,epDoneId=-1;
  void PrepareEventPawnSave(){
   var kind=MigrationKind();if(kind==null)return;
   var g=GiantMigration.Start(map,kind);GiantMigration.OnCamp(g);var l=FoodLetter(g);if(l==null)return;
   Find.LetterStack.RemoveLetter(l);epOpenId=l.ID;
   epDoneId=EventPawnAccess.AllLetters().FirstOrDefault(x=>x.lpResolved)?.ID??-1;
  }
  void CheckEventPawnAfterLoad(){
   var all=EventPawnAccess.AllLetters().ToList();
   var open=all.OfType<Letter_GiantMigration>().FirstOrDefault(x=>x.ID==epOpenId);
   Check(open!=null && open.lpVer==1 && !open.Closed && open.group!=null && open.group.Alive.Any(p=>EventPawnAccess.For(p).Contains(open)),"after load: the dismissed food letter still opens from the giants");
   var done=all.FirstOrDefault(x=>x.ID==epDoneId);
   Check(epDoneId<0 || (done!=null && done.lpResolved && done.Closed),"after load: an answered event letter stays answered");
  }
 }
}
