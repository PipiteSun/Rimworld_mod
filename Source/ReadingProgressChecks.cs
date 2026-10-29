using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
using GR = LichPhylactery.Growth;
namespace LichPhylactery {
 // 2026-10-28：Test.ps1 -Only ReadingProgress（阅读进度）：每种读了给奖励的物品都有进度组件和进度文字，读者的工作描述 / 检视行，契约书进度按人累计并存档。
 public partial class LichChecks {
  static string rpScrollId,rpPawnId;const int RpPactSaved=1800;
  static Thing RpMake(string def)=>ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(def));

  void CheckReadingProgress(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oSup=Adventurer.TestSuppressed;
   Adventurer.TestSuppressed=false;s.adventurer=true;
   var made=new List<Thing>();var st=Netheril.State;
   try{
    // 1. 6 本典籍、3 种耐瑟卷轴、恶魔学典籍、契约书都挂上了进度组件，各归各的类型
    var names=Enumerable.Range(0,6).Select(i=>GR.TomeDef(i)?.defName).Concat(new[]{Netheril.StudyDef,Netheril.ArcaneDef,Netheril.KarsusDef,"LP_Demonomicon","LP_PactScroll"}).ToList();
    var defs=names.Select(n=>n==null?null:DefDatabase<ThingDef>.GetNamedSilentFail(n)).ToList();
    bool comps=defs.All(d=>d!=null && d.comps.Count(c=>c is CompProperties_ReadingProgress)==1 && ReadingProgressStartup.Defs.Contains(d));
    var kinds=defs.Select(d=>ReadingProgress.KindOf(d)).ToList();
    bool kindOk=kinds.Take(6).All(k=>k==ReadingProgress.Kind.Tome) && kinds[6]==ReadingProgress.Kind.Study && kinds[7]==ReadingProgress.Kind.Arcane && kinds[8]==ReadingProgress.Kind.Karsus && kinds[9]==ReadingProgress.Kind.Demonomicon && kinds[10]==ReadingProgress.Kind.Pact;
    bool others=!ReadingProgress.Tracks(ThingDefOf.Steel) && DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.HasComp(typeof(CompBook)) && d.GetCompProperties<CompProperties_Book>()?.doers?.Any(x=>x.DoerClass?.Namespace=="LichPhylactery")==true).All(ReadingProgress.Tracks);
    Check(comps && kindOk && others,"reading progress component on all "+defs.Count+" reward reads ("+string.Join(", ",names)+"); every mod book outcome is covered, steel is not");

    // 2. 典籍：读 15 小时 → 15 / 30 小时（50%），检视面板列出读者；读者的工作描述后缀和检视行
    var r=HtPawn(spawn:true);var h=GrAdv(r);var tome=GR.MakeTome(0);made.Add(tome);GenSpawn.Spawn(tome,HtCell(2,0),map);
    Netheril.Doer<BookOutcome_Tome>(tome).Feed(r,GR.TomeTicks/2,true);
    var ti=ReadingProgress.Get(tome,r);string insp=tome.GetInspectString();
    Check(ti.has && !ti.done && Near(ti.frac,.5f) && ti.text.Contains("15 / 30 小时") && ti.text.Contains("50%") && insp.Contains("阅读进度") && insp.Contains(r.LabelShort) && insp.Contains("15 / 30"),"tome after 15 h: \""+ti.text+"\"; inspect lists the reader: "+insp.Replace("\n"," | "));
    ReadingProgress.Forget(r);string suf=ReadingProgress.ReportSuffix(r,tome),line=ReadingProgress.PawnLineFor(r,tome);float bar=ReadingProgress.BarFrac(r,tome);
    var stats=tome.TryGetComp<CompReadingProgress>()?.SpecialDisplayStats().ToList();
    Check(suf=="（进度 50%）" && line!=null && line.StartsWith("阅读"+tome.def.label) && Near(bar,.5f) && stats!=null && stats.Count==1 && stats[0].LabelCap.Contains("阅读进度"),"reader job report suffix "+suf+", inspect line \""+line+"\", progress bar 50%, info card entry");
    h.tomes[0]=true;h.Dirty();ReadingProgress.Forget(r);var td=ReadingProgress.Get(tome,r);
    Check(td.done && td.text.Contains("已获得奖励") && ReadingProgress.ReportSuffix(r,tome)=="（已获得奖励）" && ReadingProgress.BarFrac(r,tome)<0f,"tome whose ability was already granted: \""+td.text+"\", no progress bar");

    // 3. 研习卷：没 VPE 时显示到下一级心灵链接的研习度；有 VPE 时没有终点
    var study=Netheril.MakeStudy();made.Add(study);
    string studyText;bool studyOk;
    if(NetherilHooks.VPEActive){var si=ReadingProgress.Get(study,r);studyText=si.text;studyOk=si.frac<0f && si.text.Contains("没有终点");}
    else{st.studyPoints[r.thingIDNumber]=50f;var si=ReadingProgress.Get(study,r);studyText=si.text;studyOk=si.has && Near(si.frac,.5f) && si.text.Contains("50 / 100");st.studyPoints.Remove(r.thingIDNumber);}
    Check(studyOk,"study scroll: \""+studyText+"\"");

    // 4. 秘术残卷：进度 12 / 24、已领悟 1 个；学满 2 个 → 已获得奖励
    var arc=Netheril.MakeArcane("Evocation");made.Add(arc);var ad=Netheril.Doer<BookOutcome_NetherilArcane>(arc);
    ad.progress[r.thingIDNumber]=12f;ad.learned[r.thingIDNumber]=1;var ai=ReadingProgress.Get(arc,r);
    ad.learned[r.thingIDNumber]=2;var ai2=ReadingProgress.Get(arc,r);
    Check(Near(ai.frac,.5f) && ai.text.Contains("12.0 / 24") && ai.text.Contains("1 / 2") && ai2.done && ai2.text.Contains("已获得奖励") && ReadingProgress.OwnInspectLine(arc),"arcane fragment: \""+ai.text+"\" → \""+ai2.text+"\"");

    // 5. 卡苏斯之卷：第 2 遍 20 / 40 小时；读满 10 遍 → 回响已满
    var kar=Netheril.Make(Netheril.KarsusDef);made.Add(kar);int id=r.thingIDNumber;
    st.karsusPasses[id]=1;st.karsusTicks[id]=Netheril.KarsusPassTicks/2;var ki=ReadingProgress.Get(kar,r);
    st.karsusPasses[id]=Netheril.MaxEcho;var ki2=ReadingProgress.Get(kar,r);
    st.karsusPasses.Remove(id);st.karsusTicks.Remove(id);
    Check(Near(ki.frac,.5f) && ki.text.Contains("第 2 遍") && ki.text.Contains("20 / 40") && ki2.done,"Karsus scroll: \""+ki.text+"\" → \""+ki2.text+"\"");

    // 6. 恶魔学典籍：进度属于书本身，2 / 4 小时；读满 → 已读完
    var demo=(Book_Demonomicon)RpMake("LP_Demonomicon");made.Add(demo);demo.demon="barlgura";
    demo.readTicks=AbyssItems.BookTicks/2;var di=ReadingProgress.Get(demo,null);string dl=ReadingProgress.ItemLines(demo);
    demo.readTicks=AbyssItems.BookTicks;var di2=ReadingProgress.Get(demo,r);
    Check(Near(di.frac,.5f) && di.text.Contains("2 / 4 小时") && dl==di.text && di2.done && di2.text.Contains("已读完"),"demonomicon (per book): \""+di.text+"\" → \""+di2.text+"\"");

    // 7. 契约书：读 toil 被接管（开头扣掉以前读过的，中途停下记住，读完清零）
    var scroll=RpMake("LP_PactScroll");made.Add(scroll);
    PactRead.Set(r,scroll,GenDate.TicksPerHour);var pi=ReadingProgress.Get(scroll,r);
    var job=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("LP_ReadPact"),scroll,scroll);var drv=(JobDriver_ReadPact)job.MakeDriver(r);
    var toils=((IEnumerable<Toil>)AccessTools.Method(typeof(JobDriver_ReadPact),"MakeNewToils").Invoke(drv,null)).ToList();
    var readToil=toils.FirstOrDefault(PactRead.IsReadToil);bool wrapped=readToil?.initAction?.Method.DeclaringType?.FullName?.Contains("ReadingProgressPactToils")==true;
    drv.ticksLeftThisToil=Pacts.ReadTicks;PactRead.OnStart(drv);int left=drv.ticksLeftThisToil;
    drv.ticksLeftThisToil=Pacts.ReadTicks-3000;PactRead.OnFinish(drv);int kept=ReadingProgress.Get(scroll,r).text.Contains("1.2 / 4")?1:0;
    drv.ticksLeftThisToil=0;PactRead.OnFinish(drv);var pi3=ReadingProgress.Get(scroll,r);
    Check(pi.text.Contains("1 / 4 小时（25%）") && wrapped && left==Pacts.ReadTicks-GenDate.TicksPerHour && kept==1 && !pi3.has,"pact scroll: \""+pi.text+"\"; read toil resumes at "+left+" ticks left, interrupted read kept, finished read cleared");

    // 9. 阅读速度 = 研究速度，最低 ×0.25、没有上限（智识 20 的人和智识 0 的人），进度文字写出速度
    var fast=HtPawn(spawn:true);var slow=HtPawn(spawn:true);
    fast.skills.GetSkill(SkillDefOf.Intellectual).Level=20;slow.skills.GetSkill(SkillDefOf.Intellectual).Level=0;ReadSpeed.Forget(fast);ReadSpeed.Forget(slow);ReadSpeed.Forget(r);
    float rawF=fast.GetStatValue(StatDefOf.ResearchSpeed),rawS=slow.GetStatValue(StatDefOf.ResearchSpeed),ff=ReadSpeed.Factor(fast),fs=ReadSpeed.Factor(slow);
    PactRead.Set(fast,scroll,GenDate.TicksPerHour);var fi=ReadingProgress.Get(scroll,fast);PactRead.Set(fast,scroll,0);
    Check(Near(ff,Mathf.Max(rawF,ReadSpeed.Min)) && Near(ReadSpeed.Factor(r),Mathf.Max(r.GetStatValue(StatDefOf.ResearchSpeed),ReadSpeed.Min)) && ff>1f && fi.text.Contains("阅读速度 ×"+ff.ToString("0.##")+"（研究速度）") && fi.text.Contains("约还需"),"reading speed = clamped research speed: intellect 20 → ×"+ff.ToString("0.###")+" (stat "+rawF.ToString("0.###")+"); text \""+fi.text+"\"");

    // 10. 读得快的人进度涨得快：卡苏斯之卷读 600 刻、契约书读 toil 600 刻
    var kd=Netheril.Doer<BookOutcome_Karsus>(kar);var kt=new List<int>();var pd=new List<int>();
    foreach(var x in new[]{fast,slow}){
     int xid=x.thingIDNumber;st.karsusTicks.Remove(xid);kd.OnReadingTick(x,1f);kd.Feed(x,600);kd.EndSession(x);kt.Add(st.KarsusTicks(x));st.karsusTicks.Remove(xid);st.karsusPasses.Remove(xid);
     var xd=(JobDriver_ReadPact)JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("LP_ReadPact"),scroll,scroll).MakeDriver(x);int start=Pacts.ReadTicks-1000;xd.ticksLeftThisToil=start;float rem=0f;
     for(int i=0;i<10;i++)PactRead.Tick(xd,60,ref rem);pd.Add(600+start-xd.ticksLeftThisToil); // 原版自己扣 600，Tick 只扣多出来的
    }
    Check(Math.Abs(kt[0]-600*ff)<=1f && Math.Abs(kt[1]-600*fs)<=1f && kt[0]>kt[1] && Math.Abs(pd[0]-600*ff)<=1f && Math.Abs(pd[1]-600*fs)<=1f && pd[0]>pd[1],"600 ticks of reading: Karsus "+kt[0]+" vs "+kt[1]+" ticks, pact "+pd[0]+" vs "+pd[1]+" (×"+ff.ToString("0.##")+" vs ×"+fs.ToString("0.##")+")");

    // 11. 研究速度很低（智识 0）也按 ×0.25 算，不会停住
    Check(rawS<ReadSpeed.Min && Near(fs,ReadSpeed.Min) && ReadSpeed.Scale(slow,4f,new Dictionary<int,float>())==1,"low research speed clamps at ×0.25 (stat "+rawS.ToString("0.###")+" → ×"+fs.ToString("0.##")+")");

    // 12. 研究速度 > 3（智识 20 + 研究速度 +6 的状态）：阅读速度不封顶；读 600 刻卡苏斯 / 典籍 / 契约书都按全倍数，进度不超过 100%，契约书倒计时不会变成负数
    var hd=TestHediff("LP_TestResearchBoost",new HediffStage{statOffsets=new List<StatModifier>{new StatModifier{stat=StatDefOf.ResearchSpeed,value=6f}}});
    var genius=HtPawn(spawn:true);genius.skills.GetSkill(SkillDefOf.Intellectual).Level=20;genius.health.AddHediff(hd);StatDefOf.ResearchSpeed.Worker.ClearCacheForThing(genius);ReadSpeed.Forget(genius);
    float rawG=genius.GetStatValue(StatDefOf.ResearchSpeed),fg=ReadSpeed.Factor(genius);int gid=genius.thingIDNumber;
    st.karsusTicks.Remove(gid);kd.OnReadingTick(genius,1f);kd.Feed(genius,600);kd.EndSession(genius);int gk=st.KarsusTicks(genius);st.karsusTicks.Remove(gid);st.karsusPasses.Remove(gid);
    var gd=(JobDriver_ReadPact)JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("LP_ReadPact"),scroll,scroll).MakeDriver(genius);gd.ticksLeftThisToil=200;float grem=0f;
    for(int i=0;i<10;i++)PactRead.Tick(gd,60,ref grem);int gLeft=gd.ticksLeftThisToil;
    PactRead.Set(genius,scroll,Pacts.ReadTicks/2);var gi=ReadingProgress.Get(scroll,genius);PactRead.Set(genius,scroll,0);
    Check(rawG>3f && Near(fg,rawG) && Math.Abs(gk-600*fg)<=1f && gLeft>=1 && gi.frac<=1f && gi.text.Contains("（50%）") && gi.text.Contains("阅读速度 ×"+fg.ToString("0.##")) && ReadSpeed.Note.Contains("无上限"),"research speed above 3 is not capped: stat "+rawG.ToString("0.###")+" → ×"+fg.ToString("0.##")+"; Karsus 600 ticks → "+gk+", pact toil left "+gLeft+", text \""+gi.text+"\"");
   }finally{
    foreach(var t in made)if(!t.Destroyed)t.Destroy();
    foreach(var p in htMade){ReadingProgress.ClearBar(p);ReadingProgress.Forget(p);ReadSpeed.Forget(p);if(!p.Destroyed)p.Destroy();}htMade.Clear();
    s.adventurer=oOn;Adventurer.TestSuppressed=oSup;
   }
  }
  // 8. 存读档：契约书的累计进度保留
  void PrepareReadingProgressSave(){
   var p=HtPawn(spawn:true);var scroll=RpMake("LP_PactScroll");GenSpawn.Spawn(scroll,HtCell(-2,0),map);
   PactRead.Set(p,scroll,RpPactSaved);rpScrollId=scroll.ThingID;rpPawnId=p.ThingID;htMade.Clear();
  }
  void CheckReadingProgressAfterLoad(){
   var scroll=map.listerThings.AllThings.FirstOrDefault(t=>t.ThingID==rpScrollId);var p=map.mapPawns.AllPawns.FirstOrDefault(x=>x.ThingID==rpPawnId);
   int t=p==null || scroll==null?-1:PactRead.Ticks(p,scroll);
   Check(t==RpPactSaved,"pact scroll reading progress survives save/load ("+t+" / "+RpPactSaved+" ticks)");
   if(scroll!=null){PactRead.Set(p,scroll,0);scroll.Destroy();}
   p?.Destroy();
  }
 }
}
