using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 阅读进度（2026-10-28）：所有“读了给奖励”的东西统一显示进度——物品检视面板（按人列出前 3 名）、信息卡、读者的工作描述 / 检视面板、读者头顶进度条。
 // 覆盖：6 本属性典籍、耐瑟研习卷 / 秘术残卷 / 卡苏斯之卷、恶魔学典籍、契约书。只读现有存档数据，不改奖励和要求；
 // 唯一新增的存档数据是契约书的阅读进度（原来 4 小时必须一口气读完，中断就从头来，现在按人按书累计）。
 // 组件 CompReadingProgress 在启动时挂到这些物品定义上（不改 XML），检视字符串按 2 秒 / 120 刻缓存。
 public struct ReadInfo {
  public bool has;      // 这个人有进度或已经拿过奖励（物品检视面板只列这样的人）
  public bool done;     // 奖励已经拿到（或已读完）
  public float frac;    // 0～1；<0 = 没有终点（只显示文字，不画进度条）
  public string text;   // 不带人名的进度文字
 }
 // 阅读速度（2026-10-28 续）：这 11 种读物的奖励进度按读者的研究速度累计（最低 ×0.25，没有上限），所需小时数不变。
 // 按人缓存 250 刻，不在每刻算属性。典籍 / 恶魔学典籍 / 卡苏斯之卷把刻数乘上速度（零头留到下次），研习卷 / 残卷乘在经验和进度上，契约书改读 toil 的倒计时。
 // 速度很大时：典籍 / 恶魔学典籍按越过门槛结算，卡苏斯按整遍循环，残卷进度封顶 24，契约书倒计时最少留 1 刻。
 public static class ReadSpeed {
  public const float Min=.25f;public const int CacheTicks=250;
  public const string Note="阅读速度 = 读者的研究速度（最低 ×0.25，无上限）。";
  static readonly Dictionary<int,int> cacheTick=new Dictionary<int,int>();static readonly Dictionary<int,float> cache=new Dictionary<int,float>();
  public static float Raw(Pawn p){if(p==null)return 1f;float v=p.GetStatValue(StatDefOf.ResearchSpeed);return float.IsNaN(v) || float.IsInfinity(v)?1f:Mathf.Max(Min,v);}
  public static float Factor(Pawn p){
   if(p==null)return 1f;int id=p.thingIDNumber,now=Find.TickManager?.TicksGame??0;
   if(cacheTick.TryGetValue(id,out int t) && now>=t && now-t<CacheTicks && cache.TryGetValue(id,out float f))return f;
   f=Raw(p);cache[id]=f;cacheTick[id]=now;return f;
  }
  public static void Forget(Pawn p){if(p!=null){cache.Remove(p.thingIDNumber);cacheTick.Remove(p.thingIDNumber);}}
  // raw 刻 × 速度 → 整数刻，零头按人记在 rem 里（每次只读 1 刻也不会被取整吃掉）。
  public static int Scale(Pawn p,float raw,Dictionary<int,float> rem){
   if(p==null)return Mathf.FloorToInt(raw);int id=p.thingIDNumber;rem.TryGetValue(id,out float r);
   float x=raw*Factor(p)+r;int n=(int)x;rem[id]=x-n;return n;
  }
 }
 public static class ReadingProgress {
  public enum Kind{None,Tome,Study,Arcane,Karsus,Demonomicon,Pact}
  public const int CacheTicks=120,PawnCacheTicks=60,TopPawns=3;
  static readonly Dictionary<ThingDef,Kind> kinds=new Dictionary<ThingDef,Kind>();
  public static Kind KindOf(ThingDef d){
   if(d==null)return Kind.None;if(kinds.TryGetValue(d,out var k))return k;
   k=Kind.None;
   if(Growth.TomeIndex(d)>=0)k=Kind.Tome;
   else if(d.defName==Netheril.StudyDef)k=Kind.Study;
   else if(d.defName==Netheril.ArcaneDef)k=Kind.Arcane;
   else if(d.defName==Netheril.KarsusDef)k=Kind.Karsus;
   else if(d.thingClass!=null && typeof(Book_Demonomicon).IsAssignableFrom(d.thingClass))k=Kind.Demonomicon;
   else if(d.GetCompProperties<CompProperties_PactScroll>()!=null)k=Kind.Pact;
   kinds[d]=k;return k;
  }
  public static bool Tracks(ThingDef d)=>KindOf(d)!=Kind.None;
  // 进度属于这本书本身（谁读都一样）。
  public static bool PerBook(Kind k)=>k==Kind.Demonomicon;
  static string H(int ticks)=>(ticks/(float)GenDate.TicksPerHour).ToString("0.#");
  static string Pct(float f)=>Mathf.Clamp01(f).ToStringPercent("0");
  // “，阅读速度 ×1.35（研究速度），约还需 11.1 小时”：left = 还差的量，rate = 这个人每小时实际推进的量（已乘阅读速度）。
  static string Spd(Pawn p,float left,float rate){
   var s="，阅读速度 ×"+ReadSpeed.Factor(p).ToString("0.##")+"（研究速度）";
   if(left>0f && rate>0f)s+="，约还需 "+(left/rate).ToString("0.#")+" 小时";return s;
  }

  public static ReadInfo Get(Thing book,Pawn p){
   var r=new ReadInfo{frac=-1f};if(book==null)return r;
   switch(KindOf(book.def)){
    case Kind.Tome:{
     if(p==null)return r;int a=Growth.TomeIndex(book.def);var h=Adventurer.Get(p);if(h==null)return r;
     if(h.tomes!=null && a>=0 && a<h.tomes.Length && h.tomes[a]){r.has=r.done=true;r.frac=1f;r.text="已获得奖励（"+Adventurer.Names[a]+"已从典籍得到过）";return r;}
     var d=Netheril.Doer<BookOutcome_Tome>(book);int t=d?.Ticks(p)??0;
     r.has=t>0;r.frac=t/(float)Growth.TomeTicks;r.text="阅读进度："+H(t)+" / "+H(Growth.TomeTicks)+" 小时（"+Pct(r.frac)+"）"+Spd(p,Netheril.Hours(Growth.TomeTicks-t),ReadSpeed.Factor(p)*Exalted.ReadFactor(p));return r;}
    case Kind.Study:{
     if(p==null)return r;
     if(NetherilHooks.VPEActive){var d=Netheril.Doer<BookOutcome_NetherilStudy>(book);float x=d?.SessionXP(p)??0f;r.has=x>0f;r.text="灵能经验持续累积（没有终点）"+(x>0f?"，本次 +"+x.ToString("0.##"):"")+Spd(p,0f,0f);return r;}
     var st=Netheril.State;if(st==null)return r;float sp=st.StudyPoints(p);r.has=sp>0f;
     int next=-1;foreach(int th in Netheril.StudyThresholds)if(sp<th){next=th;break;}
     if(next<0 || p.GetPsylinkLevel()>=Netheril.StudyMaxPsylink){r.done=true;r.frac=1f;r.text="研习度 "+sp.ToString("0.#")+"：心灵链接奖励已全部获得（仍可继续累积）";return r;}
     var sd=Netheril.Doer<BookOutcome_NetherilStudy>(book);
     r.frac=sp/next;r.text="研习度 "+sp.ToString("0.#")+" / "+next+"（下一级心灵链接，"+Pct(r.frac)+"）"+Spd(p,next-sp,Netheril.StudyXP*(sd?.Q??1f)*IdeoBridge.ScrollXpFactor(p)*ReadSpeed.Factor(p));return r;}
    case Kind.Arcane:{
     if(p==null)return r;var d=Netheril.Doer<BookOutcome_NetherilArcane>(book);if(d==null)return r;
     int n=d.Learned(p);float pr=d.Progress(p);r.has=n>0 || pr>0f;
     if(d.LearnedOut(p)){r.done=true;r.frac=1f;r.text="已获得奖励：已领悟 "+n+" / "+Netheril.MaxSpellsPerScroll+" 个法术（再读只剩研习余效）";return r;}
     r.frac=pr/Netheril.ArcaneGoal;r.text="残卷进度 "+pr.ToString("0.0")+" / "+Netheril.ArcaneGoal.ToString("0")+"（"+Pct(r.frac)+"），已领悟 "+n+" / "+Netheril.MaxSpellsPerScroll+" 个"+Spd(p,Netheril.ArcaneGoal-pr,d.ProgressRate(p)*ReadSpeed.Factor(p));
     if(d.Stuck(p))r.text+="（灵能等级不够，暂时领悟不了）";
     return r;}
    case Kind.Karsus:{
     if(p==null)return r;var st=Netheril.State;if(st==null)return r;
     int n=st.Passes(p),t=st.KarsusTicks(p);r.has=n>0 || t>0;
     if(n>=Netheril.MaxEcho){r.done=true;r.frac=1f;r.text="已读完 "+n+" 遍：卡苏斯的回响已满（仍可研读）";return r;}
     r.frac=t/(float)Netheril.KarsusPassTicks;r.text="第 "+(n+1)+" 遍："+H(t)+" / "+Netheril.KarsusPassHours+" 小时（"+Pct(r.frac)+"），已读完 "+n+" 遍"+(n>=Netheril.AscensionPasses?"（已满 "+Netheril.AscensionPasses+" 遍）":"")+Spd(p,Netheril.Hours(Netheril.KarsusPassTicks-t),ReadSpeed.Factor(p));
     return r;}
    case Kind.Demonomicon:{
     if(!(book is Book_Demonomicon b))return r;int t=b.readTicks;r.has=t>0;
     bool known=b.demon!=null && AbyssState.Get?.Known(b.demon)==true;
     if(t>=AbyssItems.BookTicks){r.has=r.done=true;r.frac=1f;r.text="已读完（殖民地已学会召唤"+AbyssRitual.DemonLabel(b.demon)+"）";return r;}
     r.frac=t/(float)AbyssItems.BookTicks;r.text="阅读进度："+H(t)+" / "+H(AbyssItems.BookTicks)+" 小时（"+Pct(r.frac)+"）"+(p!=null?Spd(p,Netheril.Hours(AbyssItems.BookTicks-t),ReadSpeed.Factor(p)):"，读得多快看读者的研究速度");
     if(known){r.done=true;r.text+="，已获得奖励（殖民地已经知道"+AbyssRitual.DemonLabel(b.demon)+"）";}
     return r;}
    case Kind.Pact:{
     if(p==null)return r;int t=PactRead.Ticks(p,book);r.has=t>0;
     r.frac=t/(float)Pacts.ReadTicks;r.text="阅读进度："+H(t)+" / "+H(Pacts.ReadTicks)+" 小时（"+Pct(r.frac)+"）"+Spd(p,Netheril.Hours(Pacts.ReadTicks-t),ReadSpeed.Factor(p));return r;}
   }
   return r;
  }

  // 这个人正在读这本书吗（原版阅读 job 已经翻开书，或契约书正在讲台上读）。
  public static bool IsReading(Pawn p,Thing book){
   var d=p?.jobs?.curDriver;if(d==null || book==null)return false;
   if(d is JobDriver_Reading jr)return jr.IsReading && jr.job?.targetA.Thing==book;
   if(d is JobDriver_ReadPact)return d.job?.targetA.Thing==book && PactRead.InReadToil(d);
   return false;
  }
  public static Book ReadingBook(Pawn p){var d=p?.jobs?.curDriver as JobDriver_Reading;return d!=null && d.IsReading?d.job?.targetA.Thing as Book:null;}

  // 物品检视面板的几行：按人列出（正在读的排前面，其次进度高的），最多 3 人。
  static readonly List<Pawn> tmpPawns=new List<Pawn>();
  public static string ItemLines(Thing book){
   var k=KindOf(book?.def);if(k==Kind.None)return null;
   if(PerBook(k)){var i=Get(book,null);return i.text;}
   tmpPawns.Clear();
   if(Find.Selector!=null)foreach(var o in Find.Selector.SelectedObjectsListForReading)if(o is Pawn sp && sp.RaceProps?.Humanlike==true && !tmpPawns.Contains(sp))tmpPawns.Add(sp);
   var map=book.MapHeld;if(map!=null)foreach(var c in map.mapPawns.FreeColonists)if(!tmpPawns.Contains(c))tmpPawns.Add(c);
   var holder=(book.ParentHolder as Pawn_InventoryTracker)?.pawn??(book.ParentHolder as Pawn_CarryTracker)?.pawn;if(holder!=null && !tmpPawns.Contains(holder))tmpPawns.Add(holder);
   var rows=new List<(Pawn p,ReadInfo i,bool reading)>();
   foreach(var p in tmpPawns){var i=Get(book,p);bool rd=IsReading(p,book);if(i.has || rd)rows.Add((p,i,rd));}
   tmpPawns.Clear();
   if(rows.Count==0)return "阅读进度：还没有人读过";
   var sorted=rows.OrderByDescending(x=>x.reading).ThenBy(x=>x.i.done).ThenByDescending(x=>x.i.frac).ToList();
   var lines=new List<string>{"阅读进度："};
   foreach(var x in sorted.Take(TopPawns))lines.Add("  "+x.p.LabelShort+(x.reading?"（正在读）":"")+"："+x.i.text);
   if(sorted.Count>TopPawns)lines.Add("  另有 "+(sorted.Count-TopPawns)+" 人有进度");
   return string.Join("\n",lines);
  }

  // 读者的工作描述后缀 / 检视面板一行，按人缓存 60 刻。
  sealed class PawnLine{public int tick=-99999;public Thing book;public string suffix,line;public float frac=-1f;}
  static readonly Dictionary<int,PawnLine> pawnCache=new Dictionary<int,PawnLine>();
  static PawnLine LineFor(Pawn p,Thing book){
   if(!pawnCache.TryGetValue(p.thingIDNumber,out var c)){c=new PawnLine();pawnCache[p.thingIDNumber]=c;}
   int now=Find.TickManager?.TicksGame??0;
   if(c.book!=book || now-c.tick>=PawnCacheTicks || now<c.tick){
    c.book=book;c.tick=now;var i=Get(book,p);
    c.frac=i.done?-1f:i.frac;
    c.suffix=i.done?"（已获得奖励）":i.frac>=0f?"（进度 "+Pct(i.frac)+"）":null;
    c.line=i.text.NullOrEmpty()?null:"阅读"+book.def.label+"："+i.text;
   }
   return c;
  }
  public static void Forget(Pawn p){if(p!=null)pawnCache.Remove(p.thingIDNumber);}
  public static string ReportSuffix(Pawn p,Thing book)=>p==null || !Tracks(book?.def)?null:LineFor(p,book).suffix;
  public static string PawnLineFor(Pawn p,Thing book)=>p==null || !Tracks(book?.def)?null:LineFor(p,book).line;
  public static float BarFrac(Pawn p,Thing book)=>p==null || !Tracks(book?.def)?-1f:LineFor(p,book).frac;
  // 耐瑟秘术残卷和卡苏斯之卷自己的“研读中：…”已经写了进度，检视面板不再重复一行。
  public static bool OwnInspectLine(Thing book){var k=KindOf(book?.def);return k==Kind.Arcane || k==Kind.Karsus;}

  // 读者头顶的进度条（原版进度条特效，阅读 tick 里维持；阅读结束时清掉）。
  static readonly Dictionary<int,Effecter> bars=new Dictionary<int,Effecter>();
  public static int BarCount=>bars.Count;
  public static void TickBar(Pawn p,Thing book){
   if(p?.Map==null || p.Faction!=Faction.OfPlayer)return;
   float f=BarFrac(p,book);
   if(f<0f){ClearBar(p);return;}
   if(!bars.TryGetValue(p.thingIDNumber,out var e) || e==null){e=EffecterDefOf.ProgressBar.Spawn();bars[p.thingIDNumber]=e;}
   e.EffectTick(p,TargetInfo.Invalid);
   var mote=(e.children.Count>0?e.children[0] as SubEffecter_ProgressBar:null)?.mote;
   if(mote!=null){mote.progress=Mathf.Clamp01(f);mote.offsetZ=-.5f;}
  }
  public static void ClearBar(Pawn p){if(p==null)return;if(bars.TryGetValue(p.thingIDNumber,out var e)){e?.Cleanup();bars.Remove(p.thingIDNumber);}}
 }

 // 物品上的组件：检视面板和信息卡。
 public class CompProperties_ReadingProgress : CompProperties {public CompProperties_ReadingProgress(){compClass=typeof(CompReadingProgress);}}
 public class CompReadingProgress : ThingComp {
  string cached;int cachedTick=-99999;float cachedReal=-99f;
  public void Invalidate(){cached=null;}
  public override string CompInspectStringExtra(){
   int now=Find.TickManager?.TicksGame??0;float real=Time.realtimeSinceStartup;
   if(cached==null || now-cachedTick>=ReadingProgress.CacheTicks || now<cachedTick || real-cachedReal>=2f){cached=ReadingProgress.ItemLines(parent);cachedTick=now;cachedReal=real;}
   return cached;
  }
  public override IEnumerable<StatDrawEntry> SpecialDisplayStats(){
   var s=ReadingProgress.ItemLines(parent);if(s.NullOrEmpty())yield break;
   var first=s.Split('\n');string value=first.Length>1?first[1].Trim():first[0].Replace("阅读进度：","");
   yield return new StatDrawEntry(StatCategoryDefOf.Basics,"阅读进度",value,"读这件物品获得奖励的进度（按人累计，存档保留）。\n"+ReadSpeed.Note+"所需小时数不变，读得快的人更早读完。\n\n"+s,4980);
  }
 }

 // 契约书：每人每卷累计读了多少刻（原来中断就从头读）。
 public class GameComponent_ReadingProgress : GameComponent {
  public Dictionary<string,int> pact=new Dictionary<string,int>();
  public GameComponent_ReadingProgress(Game game){}
  public static GameComponent_ReadingProgress Get=>GameComp<GameComponent_ReadingProgress>.Get();
  public override void ExposeData(){
   base.ExposeData();Scribe_Collections.Look(ref pact,"lpPactRead",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit && pact==null)pact=new Dictionary<string,int>();
  }
 }
 public static class PactRead {
  static string Key(Pawn p,Thing scroll)=>scroll.thingIDNumber+":"+p.thingIDNumber;
  static int Saved(Pawn p,Thing scroll){var c=GameComponent_ReadingProgress.Get;if(c==null || p==null || scroll==null)return 0;c.pact.TryGetValue(Key(p,scroll),out int t);return t;}
  public static void Set(Pawn p,Thing scroll,int t){
   var c=GameComponent_ReadingProgress.Get;if(c==null || p==null || scroll==null)return;
   if(t<=0 || t>=Pacts.ReadTicks)c.pact.Remove(Key(p,scroll));else c.pact[Key(p,scroll)]=t;
  }
  public static bool IsReadToil(Toil t)=>t!=null && t.defaultCompleteMode==ToilCompleteMode.Delay && t.defaultDuration==Pacts.ReadTicks;
  static readonly Func<JobDriver,Toil> curToil=AccessTools.MethodDelegate<Func<JobDriver,Toil>>(AccessTools.PropertyGetter(typeof(JobDriver),"CurToil"));
  public static bool InReadToil(JobDriver d)=>d is JobDriver_ReadPact && d.CurToilIndex>=0 && IsReadToil(curToil(d));
  // 正在读：存档里的 + 这一次读了的；没在读：存档里的。
  public static int Ticks(Pawn p,Thing scroll){
   var d=p?.jobs?.curDriver;
   if(d is JobDriver_ReadPact && d.job?.targetA.Thing==scroll && InReadToil(d))return Mathf.Clamp(Pacts.ReadTicks-d.ticksLeftThisToil,0,Pacts.ReadTicks);
   return Saved(p,scroll);
  }
  // 开始读：剩余时间扣掉以前读过的；读完清零，中途停下记住进度。
  public static void OnStart(JobDriver d){var s=d.job?.targetA.Thing;int had=Saved(d.pawn,s);if(had>0)d.ticksLeftThisToil=Mathf.Max(1,Pacts.ReadTicks-had);}
  // 阅读速度：每过 dt 刻多扣（或少扣）(速度 − 1) × dt 刻的剩余时间，零头留在 rem 里；剩余至少留 1 刻让原版的倒计时收尾。
  public static void Tick(JobDriver d,int dt,ref float rem){
   if(d?.pawn==null || dt<=0 || d.ticksLeftThisToil<=1)return;
   rem+=(ReadSpeed.Factor(d.pawn)-1f)*dt;int n=(int)rem;if(n==0)return;rem-=n;
   d.ticksLeftThisToil=Mathf.Clamp(d.ticksLeftThisToil-n,1,Pacts.ReadTicks);
  }
  public static void OnFinish(JobDriver d){
   var s=d.job?.targetA.Thing;if(s==null || d.pawn==null)return;int left=d.ticksLeftThisToil;
   Set(d.pawn,s,left<=0?0:Pacts.ReadTicks-left);
  }
 }
 [HarmonyPatch(typeof(JobDriver_ReadPact),"MakeNewToils")]
 public static class ReadingProgressPactToils {
  static IEnumerable<Toil> Postfix(IEnumerable<Toil> toils,JobDriver_ReadPact __instance){
   foreach(var t in toils){
    if(PactRead.IsReadToil(t)){
     var d=__instance;var init=t.initAction;var tick=t.tickIntervalAction;float rem=0f;
     t.initAction=()=>{init?.Invoke();PactRead.OnStart(d);};
     t.tickIntervalAction=dt=>{tick?.Invoke(dt);PactRead.Tick(d,dt,ref rem);};
     t.AddFinishAction(()=>PactRead.OnFinish(d));
    }
    yield return t;
   }
  }
 }

 [StaticConstructorOnStartup]
 public static class ReadingProgressStartup {
  public static readonly List<ThingDef> Defs=new List<ThingDef>();
  static ReadingProgressStartup(){
   foreach(var def in DefDatabase<ThingDef>.AllDefsListForReading){
    if(!ReadingProgress.Tracks(def))continue;
    if(def.comps==null)def.comps=new List<CompProperties>();
    if(!def.comps.Any(c=>c is CompProperties_ReadingProgress))def.comps.Add(new CompProperties_ReadingProgress());
    Defs.Add(def);
   }
  }
 }

 // 读者的工作描述：“正在阅读 X（进度 62%）”。
 [HarmonyPatch(typeof(JobDriver),nameof(JobDriver.GetReport))]
 public static class ReadingProgressReport {
  static void Postfix(JobDriver __instance,ref string __result){
   Thing book=null;
   if(__instance is JobDriver_Reading jr){if(!jr.IsReading)return;book=jr.job?.targetA.Thing;}
   else if(__instance is JobDriver_ReadPact)book=__instance.job?.targetA.Thing;
   else return;
   var s=ReadingProgress.ReportSuffix(__instance.pawn,book);if(!s.NullOrEmpty())__result=(__result??"").TrimEnd('.','。')+s;
  }
 }
 // 读者的检视面板多一行完整进度（秘术残卷、卡苏斯之卷已有“研读中”行，不重复）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetInspectString))]
 public static class ReadingProgressPawnInspect {
  static void Postfix(Pawn __instance,ref string __result){
   var b=ReadingProgress.ReadingBook(__instance);if(b==null || ReadingProgress.OwnInspectLine(b))return;
   var line=ReadingProgress.PawnLineFor(__instance,b);if(line.NullOrEmpty())return;
   __result=__result.NullOrEmpty()?line:__result+"\n"+line;
  }
 }
 // 读者头顶的进度条：原版阅读 tick（Book.OnBookReadTick）里维持。契约书的读 toil 本来就有进度条。
 [HarmonyPatch(typeof(Book),nameof(Book.OnBookReadTick))]
 public static class ReadingProgressBarTick {
  static void Postfix(Book __instance,Pawn pawn){if(pawn!=null && ReadingProgress.Tracks(__instance.def))ReadingProgress.TickBar(pawn,__instance);}
 }
 [HarmonyPatch(typeof(JobDriver),nameof(JobDriver.Cleanup))]
 public static class ReadingProgressCleanup {
  static void Postfix(JobDriver __instance){
   if(!(__instance is JobDriver_Reading) && !(__instance is JobDriver_ReadPact))return;
   var p=__instance.pawn;ReadingProgress.ClearBar(p);ReadingProgress.Forget(p);
   (__instance.job?.targetA.Thing as ThingWithComps)?.GetComp<CompReadingProgress>()?.Invalidate();
  }
 }
}
