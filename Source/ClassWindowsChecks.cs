using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.132.0 剩下 4 个窗口改版：Test.ps1 -Only ClassWindows。
 // 检查：13 个职业各生成一次「法术与能力一览」（能力 / 子职业 / 法术环位 / 秘法玄奥 / 秘法奥秘 / 职业水晶 / 传奇 / 秘传槽），已获得与未获得按等级区分；
 // 4 个窗口初始尺寸在屏幕内、可以构造。自测通过后（-lichuitest）依次真的打开 4 个窗口画几十帧（职业窗口把 13 个职业逐个选一遍），
 // 把滚动条拉到底看能不能滚动、截图 CW-<序号>.png；画窗口出错会在日志留红字（Test.ps1 会报），不能滚动也写红字。
 public partial class LichChecks {
  void CheckClassWindows(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=false;var s=LichMod.Settings;bool oldOn=s.adventurer;s.adventurer=true;
   try{
    var all=ClassSystem.All;
    Check(all.Count==12,"[1.148.0] 12 classes for the overview ("+all.Count+")");
    // 1. 13 个职业各生成一次一览：每个职业 10 级（冒险者 20 级）。
    var bad=new List<string>();var counts=new List<string>();
    foreach(var c in all){
     var p=ClsPawn(20);var h=Adventurer.Get(p);ClassSystem.SetLevel(h,c,10);
     List<OverviewRow> rows=null;try{rows=ClassOverview.Build(h,c);}catch(Exception e){bad.Add(c.label+" 异常 "+e.Message);continue;}
     if(rows.Count==0)bad.Add(c.label+" 空");
     if(!rows.Any(r=>r.have) || !rows.Any(r=>!r.have))bad.Add(c.label+" 没有区分已获得 / 未获得");
     if(rows.Any(r=>r.group==0 && r.tag=="职业能力" && r.level<=10 && !r.have && !r.label.Contains("另要")))bad.Add(c.label+" ≤10 级的职业能力没有标成已获得");
     if(rows.Any(r=>r.group==0 && r.tag=="职业能力" && r.level>10 && r.have))bad.Add(c.label+" >10 级的能力标成了已获得");
     if(rows.Count(r=>r.group==1)!=4)bad.Add(c.label+" 秘传槽行数 "+rows.Count(r=>r.group==1));
     if(rows.Any(r=>r.label.NullOrEmpty()))bad.Add(c.label+" 有空标题");
     if(ClassOverview.Height(rows)<rows.Count*ClassOverview.RowH)bad.Add(c.label+" 高度不对");
     counts.Add(c.label+rows.Count);p.Destroy();
    }
    Check(bad.Count==0,"[1.132.0] overview builds for all 13 classes at class level 10, owned vs locked split by level, 4 arcanum-slot rows ("+string.Join(" ",counts)+")"+(bad.Count>0?": "+string.Join("；",bad.Take(8)):""));
    // 2. 施法职业有法术环位行；法师来自法术书（九环 17 级），邪术师有 4 个秘法玄奥（11/13/15/17），吟游诗人有秘法奥秘。
    {var miss=new List<string>();
     foreach(var c in all){
      var l=CasterSpells.ListOf(c);bool wiz=c==Spellbook.Wizard;if(l==null && !wiz)continue;
      var p=ClsPawn(20);var h=Adventurer.Get(p);ClassSystem.SetLevel(h,c,5);var rows=ClassOverview.Build(h,c);
      var sp=rows.Where(r=>r.tag=="法术表" || r.tag=="法术书").ToList();
      if(sp.Count==0)miss.Add(c.label+" 无法术行");
      if(wiz && !sp.Any(r=>r.level==17 && r.label.StartsWith("九环")))miss.Add("法师九环不在 17 级");
      if(sp.Any(r=>r.level>5 && r.have) || sp.Any(r=>r.level<=5 && !r.have))miss.Add(c.label+" 法术行已获得标记错");
      if(l!=null && l.Pact && rows.Count(r=>r.tag=="秘法玄奥")!=4)miss.Add("邪术师秘法玄奥 "+rows.Count(r=>r.tag=="秘法玄奥"));
      if(l!=null && l.secretLevels.Count>0 && rows.Count(r=>r.tag=="秘法奥秘")!=l.secretLevels.Count)miss.Add("吟游诗人秘法奥秘");
      p.Destroy();
     }
     Check(miss.Count==0,"[1.132.0] caster overviews list spell circles (wizard spellbook, 8 class spell lists), warlock arcanum 6–9 and bard magical secrets"+(miss.Count>0?": "+string.Join("；",miss):""));}
    // 3. 职业水晶：有水晶的职业各 4 行（11/13/15/17）；学会的标成已获得。
    {var p=ClsPawn(20);var h=Adventurer.Get(p);var W=Spellbook.Wizard;ClassSystem.SetLevel(h,W,13);
     var crystal=LakeArcana.OfClass(W).FirstOrDefault();bool learned=crystal!=null && LakeArcana.Learn(p,crystal);
     var rows=ClassOverview.Build(h,W).Where(r=>r.tag=="职业水晶").ToList();
     Check(crystal!=null && learned && rows.Count==4 && rows.Select(r=>r.level).SequenceEqual(new[]{11,13,15,17}) && rows.Count(r=>r.have)==1,
      "[1.132.0] class crystals show at class level 11/13/15/17, the learned one highlighted ("+string.Join(",",rows.Select(r=>r.level+(r.have?"✓":"")))+")");
     p.Destroy();}
    // 4. 子职业：战士 3 级没选时一览里是「待选择」，窗口列出 3 个以上可选项；选了之后列出所选子职业的能力。
    {var p=ClsPawn(20);var h=Adventurer.Get(p);var F=ClassSystem.Fighter;ClassSystem.SetLevel(h,F,3);
     var r0=ClassOverview.Build(h,F).FirstOrDefault(r=>r.tag=="子职业");var items=Dialog_ChooseSubclass.Items(h);
     var opt=ClassSubclass.Options(F).FirstOrDefault();bool picked=opt!=null && ClassSubclass.Pick(h,opt);
     var r1=ClassOverview.Build(h,F);
     Check(r0!=null && r0.pending && items.Count(i=>i.ch==null)>=3 && picked && r1.Any(r=>r.tag=="子职业" && r.have) && r1.Any(r=>r.tag==opt.label),
      "[1.132.0] subclass: pending before pick ("+items.Count+" options in the window), picked subclass abilities listed after");
     p.Destroy();}
    // 5. 4 个窗口都能构造，初始尺寸在测试屏幕内，职业 / 子职业窗口是两栏宽度。
    {var p=ClsPawn(20);
     var wins=new Window[]{new Dialog_ChooseClass(p),new Dialog_ChooseSubclass(p),new Dialog_ChooseCompanion(p),new Dialog_BardBallad(p,null)};
     var big=wins.Where(w=>w.InitialSize.x>UI.screenWidth-LPUI.ScreenMargin+.5f || w.InitialSize.y>UI.screenHeight-LPUI.ScreenMargin+.5f).Select(w=>w.GetType().Name).ToList();
     Check(big.Count==0 && wins[0].InitialSize.x>=800f && wins[1].InitialSize.x>=800f && Dialog_ChooseCompanion.Candidates(p)!=null,"[1.132.0] the 4 windows fit the "+UI.screenWidth+"×"+UI.screenHeight+" screen and the class / subclass windows are two-column ("+string.Join(", ",wins.Select(w=>w.InitialSize.ToString()))+")");
     p.Destroy();}
   }finally{s.adventurer=oldOn;Adventurer.TestSuppressed=sup;}
  }

  // ---------- 自测通过后真的画窗口 ----------
  static int cwFrames;static Pawn cwPawn;static Window cwWin;static readonly List<string> cwErrors=new List<string>();
  static bool CaptureClassWindows(){
   if(cwFrames==0)return false;cwFrames++;
   string dir=GenFilePaths.SaveDataFolderPath;var map=Find.CurrentMap;
   try{
    if(cwFrames==10){
     cwPawn=map.mapPawns.FreeColonistsSpawned.FirstOrDefault();Adventurer.TestSuppressed=false;LichMod.Settings.adventurer=true;
     if(cwPawn!=null){var h=Adventurer.Get(cwPawn)??Adventurer.TryAdd(cwPawn,20,true);h.level=20;h.Dirty();if(h.classes==null)h.classes=new ClassData();
      ClassSystem.SetLevel(h,Spellbook.Wizard,10);ClassSystem.SetLevel(h,ClassSystem.Fighter,3);ClassSystem.SetLevel(h,Bard.Class,7);}
     Messages.Clear();
    }
    if(cwPawn==null){if(cwFrames>20)Application.Quit();return true;}
    // 职业窗口：13 个职业逐个选，各画 3 帧；然后拉到底检查滚动。
    if(cwFrames==20){cwWin=new Dialog_ChooseClass(cwPawn);Find.WindowStack.Add(cwWin);}
    var cls=ClassSystem.All;
    if(cwFrames>20 && cwFrames<=20+cls.Count*3 && (cwFrames-21)%3==0 && cwWin is Dialog_ChooseClass dc)dc.selected=cls[(cwFrames-21)/3];
    int t=20+cls.Count*3;
    if(cwFrames==t+2 && cwWin is Dialog_ChooseClass d1){d1.selected=Spellbook.Wizard;d1.RightScroll=new Vector2(0f,99999f);}
    if(cwFrames==t+8 && cwWin is Dialog_ChooseClass d2){float y=d2.RightScroll.y;if(!(y>0f && y<99999f))cwErrors.Add("职业窗口右栏不能滚动 "+y);}
    if(cwFrames==t+10)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"CW-0-Class.png"));
    if(cwFrames==t+16)CloseOurs();
    // 子职业窗口（战士 3 级待选）
    int t2=t+20;
    if(cwFrames==t2){cwWin=new Dialog_ChooseSubclass(cwPawn);Find.WindowStack.Add(cwWin);}
    if(cwFrames==t2+6 && cwWin is Dialog_ChooseSubclass s1)s1.RightScroll=new Vector2(0f,99999f);
    if(cwFrames==t2+12 && cwWin is Dialog_ChooseSubclass s2){float y=s2.RightScroll.y;if(!(y>0f && y<99999f))cwErrors.Add("子职业窗口右栏不能滚动 "+y);}
    if(cwFrames==t2+14)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"CW-1-Subclass.png"));
    if(cwFrames==t2+20)CloseOurs();
    // 伙伴窗口、传颂之歌窗口（列表短时不要求滚动，只看能画）
    int t3=t2+24;
    if(cwFrames==t3){cwWin=new Dialog_ChooseCompanion(cwPawn);Find.WindowStack.Add(cwWin);}
    if(cwFrames==t3+6 && cwWin is Dialog_ChooseCompanion c1)c1.Scroll=new Vector2(0f,99999f);
    if(cwFrames==t3+10)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"CW-2-Companion.png"));
    if(cwFrames==t3+16)CloseOurs();
    int t4=t3+20;
    if(cwFrames==t4){cwWin=new Dialog_BardBallad(cwPawn,null);Find.WindowStack.Add(cwWin);}
    if(cwFrames==t4+6 && cwWin is Dialog_BardBallad b1)b1.Scroll=new Vector2(0f,99999f);
    if(cwFrames==t4+10)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"CW-3-Ballad.png"));
    if(cwFrames==t4+16)CloseOurs();
    if(cwFrames>=t4+22){
     if(cwErrors.Count>0)Log.Error("[LICH-UI] class windows: "+string.Join("；",cwErrors));
     else Log.Message("[LICH-UI] class windows: drew 4 windows (13 classes), right panels scroll");
     Application.Quit();
    }
   }catch(Exception e){Log.Error("[LICH-UI] class windows capture: "+e);Application.Quit();}
   return true;
  }
  static void CloseOurs(){foreach(var w in Find.WindowStack.Windows.Where(w=>w.GetType().Namespace==typeof(LPUI).Namespace).ToList())w.Close(false);cwWin=null;}
 }
}
