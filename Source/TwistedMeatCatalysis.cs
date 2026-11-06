using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LichPhylactery {
 // 1.119.0 扭曲肉资源化 E 节「血肉催转」（Peter 明确要求）：在灵能仪式点、深渊法阵、契约讲台、信仰圣坛上献祭扭曲肉，缩短正在冷却的仪式。
 // 每 25 份扭曲肉 -1 天（1 畸变膏 = 10 扭曲肉）；同一冷却周期每缩短 1 天，下一天价格 +20%；最多缩短完整冷却的 60%，至少剩 1 天。
 // 选仪式、选人都在对话窗口里（不从信件弹 FloatMenu）；选中的殖民者到建筑旁工作 1 小时完成。
 public abstract class CatalysisSource {
  public string key,label;
  public abstract int Remaining{get;}
  public abstract int Full{get;}
  public abstract void Reduce(int ticks);
 }
 public class CatalysisSource_Psychic : CatalysisSource {
  public PsychicRitualDef def;
  static readonly AccessTools.FieldRef<GameComponent_PsychicRitualManager,Dictionary<PsychicRitualDef,int>> cds=AccessTools.FieldRefAccess<GameComponent_PsychicRitualManager,Dictionary<PsychicRitualDef,int>>("ritualCooldowns");
  public static GameComponent_PsychicRitualManager Manager=>GameComp<GameComponent_PsychicRitualManager>.Get(); // 按钮每帧都会查，缓存组件
  public CatalysisSource_Psychic(PsychicRitualDef d){def=d;key="psy:"+d.defName;label="灵能仪式："+d.LabelCap;}
  public override int Remaining{get{var m=Manager;return m==null?0:Mathf.Max(0,m.GetAvailableTick(def)-TwistedMeat.Now);}}
  public override int Full=>def.cooldownHours*GenDate.TicksPerHour;
  public override void Reduce(int ticks){var m=Manager;if(m==null)return;var d=cds(m);if(d!=null && d.ContainsKey(def))d[def]-=ticks;}
 }
 public class CatalysisSource_Patron : CatalysisSource {
  public CatalysisSource_Patron(){key="faith:patron";label="更换守护神";}
  public override int Remaining=>Faith.Comp==null?0:Mathf.Max(0,Faith.Comp.patronCooldownUntil-Faith.Now);
  public override int Full=>(int)(Faith.ChangeCooldownDays*GenDate.TicksPerDay*IdeoBridge.PatronCooldownFactor);
  public override void Reduce(int ticks){if(Faith.Comp!=null)Faith.Comp.patronCooldownUntil-=ticks;}
 }
 public class CatalysisSource_Planetar : CatalysisSource {
  public CatalysisSource_Planetar(){key="faith:planetar";label="祈唤行星使";}
  public override int Remaining=>Pacts.Comp==null?0:Mathf.Max(0,Pacts.Comp.planetarReadyTick-Pacts.Now);
  public override int Full=>GenDate.TicksPerYear;
  public override void Reduce(int ticks){if(Pacts.Comp!=null)Pacts.Comp.planetarReadyTick-=ticks;}
 }
 public class CatalysisSource_PactPatron : CatalysisSource {
  public string patron;
  public CatalysisSource_PactPatron(string patron){this.patron=patron;key="pact:"+patron;var d=DefDatabase<LP_PatronDef>.GetNamedSilentFail(patron);label="契约宗主再次来访："+(d?.label??patron);}
  public override int Remaining{get{var c=Pacts.Comp;return c!=null && c.patronCooldown.TryGetValue(patron,out int t)?Mathf.Max(0,t-Pacts.Now):0;}}
  public override int Full=>Pacts.RefuseCooldownDays*GenDate.TicksPerDay;
  public override void Reduce(int ticks){var c=Pacts.Comp;if(c!=null && c.patronCooldown.ContainsKey(patron))c.patronCooldown[patron]-=ticks;}
 }
 public class CatalysisSource_Refine : CatalysisSource {
  public Building_AbyssCircle circle;
  public CatalysisSource_Refine(Building_AbyssCircle c){circle=c;key="abyss:refine:"+c.thingIDNumber;label="深渊法阵：炼魂";}
  public override int Remaining=>Mathf.Max(0,circle.refineReady-TwistedMeat.Now);
  public override int Full=>AbyssRitual.RefineCooldown;
  public override void Reduce(int ticks){circle.refineReady-=ticks;}
 }

 public static class FleshCatalysis {
  public const int BasePrice=25,WorkTicks=GenDate.TicksPerHour;public const float Growth=1.2f,MaxFraction=.6f,Radius=12f,WhisperChance=.1f;
  public const int MinLeft=GenDate.TicksPerDay;
  public static int Price(int step)=>Mathf.RoundToInt(BasePrice*Mathf.Pow(Growth,step));
  public static bool IsHost(Thing b)=>b!=null && (b.def.defName=="PsychicRitualSpot" || b is Building_AbyssCircle || b is Building_PactLectern || b is Building_FaithAltar);
  public static List<CatalysisSource> AllFor(Thing b){
   var list=new List<CatalysisSource>();if(b==null)return list;
   if(b.def.defName=="PsychicRitualSpot" && CatalysisSource_Psychic.Manager!=null)foreach(var d in DefDatabase<PsychicRitualDef>.AllDefs)if(d.cooldownHours>0)list.Add(new CatalysisSource_Psychic(d));
   if(b is Building_AbyssCircle c)list.Add(new CatalysisSource_Refine(c));
   if(b is Building_PactLectern && Pacts.Comp!=null)foreach(var k in Pacts.Comp.patronCooldown.Keys.ToList())list.Add(new CatalysisSource_PactPatron(k));
   if(b is Building_FaithAltar){if(Faith.Comp!=null)list.Add(new CatalysisSource_Patron());if(Pacts.Comp!=null)list.Add(new CatalysisSource_Planetar());}
   return list;
  }
  public static List<CatalysisSource> Cooling(Thing b)=>AllFor(b).Where(s=>s.Remaining>0).ToList();
  public static CatalysisSource Find(Thing b,string key)=>AllFor(b).FirstOrDefault(s=>s.key==key);

  // 冷却周期：originalEnd|full|reduced|steps。剩余 + 已缩短 ≠ 原结束时刻 → 新的周期。
  public class Cycle{public int originalEnd,full,reduced,steps;}
  public static Cycle CycleOf(CatalysisSource s){
   var c=TwistedMeat.Comp;int end=TwistedMeat.Now+s.Remaining;
   if(c!=null && c.cycles.TryGetValue(s.key,out var v)){var p=v.Split('|');if(p.Length==4 && int.TryParse(p[0],out int oe) && int.TryParse(p[1],out int f) && int.TryParse(p[2],out int r) && int.TryParse(p[3],out int st) && Math.Abs(oe-(end+r))<=2)return new Cycle{originalEnd=oe,full=f,reduced=r,steps=st};}
   return new Cycle{originalEnd=end,full=Mathf.Max(s.Full,s.Remaining),reduced=0,steps=0};
  }
  static void Save(CatalysisSource s,Cycle cy){var c=TwistedMeat.Comp;if(c!=null)c.cycles[s.key]=cy.originalEnd+"|"+cy.full+"|"+cy.reduced+"|"+cy.steps;}
  // 还能缩短多少（刻）：完整冷却的 60% 减去本周期已缩短的；且至少剩 1 天。
  public static int Allowance(CatalysisSource s,Cycle cy)=>Mathf.Max(0,Mathf.Min(Mathf.FloorToInt(cy.full*MaxFraction)-cy.reduced,s.Remaining-MinLeft));
  public static int MaxDays(CatalysisSource s){var cy=CycleOf(s);int a=Allowance(s,cy);return (a+GenDate.TicksPerDay-1)/GenDate.TicksPerDay;}
  // days 天的价格（扭曲肉当量）和实际缩短的刻数（最后一天可能不足一整天）。
  public static int Cost(CatalysisSource s,int days,out int ticks)=>Cost(s,days,out ticks,out _);
  public static int Cost(CatalysisSource s,int days,out int ticks,out int steps){
   var cy=CycleOf(s);int a=Allowance(s,cy);int cost=0;ticks=0;steps=0;
   for(int i=0;i<days && a>0;i++){int step=Mathf.Min(GenDate.TicksPerDay,a);a-=step;ticks+=step;cost+=Price(cy.steps+i);steps++;}
   return cost;
  }
  public static List<Thing> Supplies(Thing b)=>TwistedMeat.Nearby(b.Map,b.Position,Radius);
  public static string PawnBlocked(Thing b,Pawn p){
   if(p==null)return "先选一名殖民者";
   if(p.Downed || p.Dead || !p.Spawned || p.Map!=b.Map)return p.LabelShort+"现在不能去";
   if(p.WorkTagIsDisabled(WorkTags.ManualSkilled) && p.WorkTagIsDisabled(WorkTags.Intellectual))return p.LabelShort+"不做这类工作";
   if(b is Building_FaithAltar){var g=FaithUtility.DevoteeGod(p);if(g!=null && g.key!="Shar")return "圣坛上的血肉催转只有莎尔的信徒和无神者能做（"+p.LabelShort+"信奉"+g.label+"）";}
   if(!p.CanReach(b,PathEndMode.Touch,Danger.Deadly))return p.LabelShort+"走不到那里";
   return null;
  }
  public static string Blocked(Thing b,CatalysisSource s,int days,Pawn p,bool checkPawn=true){
   if(!TwistedMeat.Active)return "需要异象";
   if(b==null || !b.Spawned)return "建筑不在地图上";
   if(s==null || s.Remaining<=0)return "这个仪式没有在冷却";
   if(MaxDays(s)<=0)return "本周期已经缩短到上限（完整冷却的 60%，至少剩 1 天）";
   if(days<=0)return "至少缩短 1 天";
   int cost=Cost(s,days,out _);if(TwistedMeat.MeatEquivalent(Supplies(b))<cost)return "建筑 12 格内的扭曲肉不够（要 "+cost+" 份当量）";
   if(checkPawn){string w=PawnBlocked(b,p);if(w!=null)return w;}
   return null;
  }
  // 完成：扣肉、缩短冷却、记周期、远域回响、10% 远域低语。返回实际缩短的刻数（0 = 失败）。
  public static int Apply(Thing b,string key,int days,Pawn p){
   var s=Find(b,key);if(Blocked(b,s,days,p,false)!=null)return 0;
   if(b is Building_FaithAltar && p!=null){var g=FaithUtility.DevoteeGod(p);if(g!=null && g.key!="Shar")return 0;}
   var cy=CycleOf(s);int cost=Cost(s,days,out int ticks,out int steps);
   int took=TwistedMeat.Take(Supplies(b),cost);if(took<cost)return 0;
   s.Reduce(ticks);
   cy.reduced+=ticks;cy.steps+=steps;Save(s,cy);
   TwistedMeat.Consumed(took);if(p!=null)TwistedMeat.MaybeWhisper(p,WhisperChance);
   Messages.Message((p?.LabelShort??"殖民地")+"献上 "+took+" 份扭曲肉：“"+s.label+"”的冷却缩短了 "+(ticks/(float)GenDate.TicksPerDay).ToString("0.#")+" 天。",new LookTargets(b),MessageTypeDefOf.PositiveEvent,false);
   return ticks;
  }
  // 窗口里点“开始献祭”：记下订单，派人去做。
  public static readonly Dictionary<int,(int building,string key,int days)> pending=new Dictionary<int,(int,string,int)>();
  public static bool Order(Thing b,string key,int days,Pawn p){
   var s=Find(b,key);if(Blocked(b,s,days,p)!=null)return false;
   var jd=DefCache<JobDef>.Get("LP_FleshCatalysis");if(jd==null)return false;
   pending[p.thingIDNumber]=(b.thingIDNumber,key,days);
   var job=JobMaker.MakeJob(jd,b);job.count=days;p.jobs.TryTakeOrderedJob(job,JobTag.Misc);return true;
  }
 }

 public class JobDriver_FleshCatalysis : JobDriver {
  string key;int days;
  Thing Building=>job.targetA.Thing;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  public override void Notify_Starting(){
   base.Notify_Starting();
   if(key==null && FleshCatalysis.pending.TryGetValue(pawn.thingIDNumber,out var o) && o.building==Building?.thingIDNumber){key=o.key;days=o.days;FleshCatalysis.pending.Remove(pawn.thingIDNumber);}
  }
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOn(()=>key==null || Building==null || !Building.Spawned);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
   var work=Toils_General.Wait(FleshCatalysis.WorkTicks,TargetIndex.A);
   // 2026-11-06 审查：这项检查要扫地图上的扭曲肉并排序、解析周期字符串；原来 1 小时的工作里每刻都跑一遍，改成每 60 刻一次（完成时 Apply 还会再查一遍）。
   work.WithProgressBarToilDelay(TargetIndex.A);work.FailOn(()=>pawn.IsHashIntervalTick(60) && FleshCatalysis.Blocked(Building,FleshCatalysis.Find(Building,key),days,pawn,false)!=null);
     yield return work;
   yield return Toils_General.Do(()=>FleshCatalysis.Apply(Building,key,days,pawn));
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref key,"lpKey");Scribe_Values.Look(ref days,"lpDays");}
 }

 // 宿主建筑上的「血肉催转」按钮。子类的 GetGizmos 都先 yield base 的，所以挂在 Building 上。
 [HarmonyPatch(typeof(Building),nameof(Building.GetGizmos))]
 public static class FleshCatalysisGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos,Building __instance){
   foreach(var g in gizmos)yield return g;
   if(!TwistedMeat.Active || __instance.Faction!=Faction.OfPlayer || !__instance.Spawned || !FleshCatalysis.IsHost(__instance))yield break;
   var b=__instance;var cmd=new Command_Action{defaultLabel="血肉催转",defaultDesc="献祭扭曲肉，缩短这里一个正在冷却的仪式：每 25 份扭曲肉 -1 天（1 份畸变膏算 10 份），同一冷却周期里每缩短 1 天，下一天价格 +20%；最多缩短完整冷却的 60%，至少剩 1 天。\n\n扭曲肉要放在建筑 12 格内；选一名殖民者在旁边工作 1 小时完成。献祭者有 10% 可能听见远域低语。"+(b is Building_FaithAltar?"\n\n圣坛上只有莎尔的信徒和无神者能做。":""),icon=TwistedMeat.Icon("TwistedMeat/CatalysisGizmo"),action=()=>Find.WindowStack.Add(new Dialog_FleshCatalysis(b))};
   if(FleshCatalysis.Cooling(b).Count==0)cmd.Disable("这里没有正在冷却的仪式");
   yield return cmd;
  }
 }

 public class Dialog_FleshCatalysis : Window {
  readonly Thing building;string selectedKey;int days=1;Pawn selectedPawn;Vector2 scrollA,scrollB;string result;
  public override Vector2 InitialSize=>new Vector2(720f,540f);
  public Dialog_FleshCatalysis(Thing b){building=b;doCloseX=true;forcePause=true;absorbInputAroundWindow=true;draggable=true;closeOnClickedOutside=false;}
  // 2026-11-06 审查：原来每帧对每个殖民者算三遍 PawnBlocked（含 CanReach 寻路可达）、扫两遍地图上的扭曲肉并排序。
  // 现在每 30 帧（或改了仪式 / 天数 / 人选）重算一次；点“开始献祭”时 Order 还会完整再查一遍。
  int cacheFrame=-999;List<CatalysisSource> srcCache;List<Pawn> pawnCache;readonly Dictionary<Pawn,string> whyCache=new Dictionary<Pawn,string>();int haveCache;
  string blockCache;(string key,int days,Pawn pawn) blockKey;
  bool Stale=>Time.frameCount-cacheFrame>30;
  void Refresh(){
   cacheFrame=Time.frameCount;srcCache=FleshCatalysis.Cooling(building);haveCache=TwistedMeat.MeatEquivalent(FleshCatalysis.Supplies(building));
   whyCache.Clear();foreach(var p in building.Map.mapPawns.FreeColonistsSpawned)whyCache[p]=FleshCatalysis.PawnBlocked(building,p);
   pawnCache=whyCache.Keys.OrderBy(p=>whyCache[p]==null?0:1).ThenBy(p=>p.Position.DistanceToSquared(building.Position)).ToList();
   blockKey=default;
  }
  string Why(Pawn p)=>p!=null && whyCache.TryGetValue(p,out var w)?w:FleshCatalysis.PawnBlocked(building,p);
  public override void DoWindowContents(Rect r){
   if(building==null || !building.Spawned){Close();return;}
   if(Stale || srcCache==null)Refresh();
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(r.x,r.y,r.width,32f),"血肉催转 · "+building.LabelCap);Text.Font=GameFont.Small;
   var sources=srcCache;
   if(selectedKey==null || !sources.Any(s=>s.key==selectedKey))selectedKey=sources.FirstOrDefault()?.key;
   var left=new Rect(r.x,r.y+38f,300f,r.height-38f);var right=new Rect(left.xMax+8f,left.y,r.xMax-left.xMax-8f,left.height);
   Widgets.DrawMenuSection(left);Widgets.DrawMenuSection(right);
   // 左：正在冷却的仪式
   var li=left.ContractedBy(4f);var view=new Rect(0f,0f,li.width-16f,Mathf.Max(li.height-4f,sources.Count*48f));
   Widgets.BeginScrollView(li,ref scrollA,view);
   for(int i=0;i<sources.Count;i++){
    var s=sources[i];var row=new Rect(0f,i*48f,view.width,48f);
    if(s.key==selectedKey)Widgets.DrawHighlightSelected(row);else Widgets.DrawHighlightIfMouseover(row);
    Widgets.Label(new Rect(row.x+4f,row.y+2f,row.width-8f,22f),s.label.Truncate(row.width-8f));
    var cy=FleshCatalysis.CycleOf(s);Text.Font=GameFont.Tiny;GUI.color=Color.gray;
    Widgets.Label(new Rect(row.x+4f,row.y+24f,row.width-8f,20f),"剩 "+s.Remaining.ToStringTicksToPeriod()+(cy.reduced>0?"，本周期已缩短 "+(cy.reduced/(float)GenDate.TicksPerDay).ToString("0.#")+" 天":""));
    GUI.color=Color.white;Text.Font=GameFont.Small;
    if(Widgets.ButtonInvisible(row)){selectedKey=s.key;days=1;result=null;}
   }
   if(sources.Count==0){GUI.color=Color.gray;Widgets.Label(new Rect(4f,4f,view.width,60f),"这里没有正在冷却的仪式。");GUI.color=Color.white;}
   Widgets.EndScrollView();
   // 右：天数、价格、殖民者
   var ri=right.ContractedBy(8f);float y=ri.y;var src=sources.FirstOrDefault(s=>s.key==selectedKey);
   if(src==null){if(result!=null)Widgets.Label(new Rect(ri.x,y,ri.width,60f),result);return;}
   int max=FleshCatalysis.MaxDays(src);days=Mathf.Clamp(days,max>0?1:0,Mathf.Max(0,max));
   Widgets.Label(new Rect(ri.x,y,ri.width,24f),src.label);y+=26f;
   GUI.color=Color.gray;Widgets.Label(new Rect(ri.x,y,ri.width,22f),"完整冷却 "+(FleshCatalysis.CycleOf(src).full/(float)GenDate.TicksPerDay).ToString("0.#")+" 天，本周期还能缩短 "+max+" 天");GUI.color=Color.white;y+=26f;
   if(Widgets.ButtonText(new Rect(ri.x,y,32f,28f),"-") && days>1)days--;
   Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(new Rect(ri.x+36f,y,100f,28f),"缩短 "+days+" 天");Text.Anchor=TextAnchor.UpperLeft;
   if(Widgets.ButtonText(new Rect(ri.x+140f,y,32f,28f),"+") && days<max)days++;
   y+=34f;
   int cost=FleshCatalysis.Cost(src,days,out int ticks);int have=haveCache;
   var cy2=FleshCatalysis.CycleOf(src);var prices=new List<string>();for(int i=0;i<days;i++)prices.Add(FleshCatalysis.Price(cy2.steps+i).ToString());
   GUI.color=have>=cost?Color.white:new Color(.9f,.3f,.3f);
   Widgets.Label(new Rect(ri.x,y,ri.width,22f),"要扭曲肉 "+cost+" 份当量（"+string.Join(" + ",prices)+"），12 格内有 "+have);GUI.color=Color.white;y+=24f;
   GUI.color=Color.gray;Text.Font=GameFont.Tiny;Widgets.Label(new Rect(ri.x,y,ri.width,32f),"实际缩短 "+(ticks/(float)GenDate.TicksPerDay).ToString("0.#")+" 天。1 份畸变膏算 10 份扭曲肉。献祭者有 10% 可能听见远域低语。");Text.Font=GameFont.Small;GUI.color=Color.white;y+=34f;
   Widgets.Label(new Rect(ri.x,y,ri.width,22f),"谁去献祭（在建筑旁工作 1 小时）：");y+=24f;
   var pawns=pawnCache;
   if(selectedPawn!=null && !pawns.Contains(selectedPawn))selectedPawn=null;
   if(selectedPawn==null)selectedPawn=pawns.FirstOrDefault(p=>Why(p)==null);
   var pr=new Rect(ri.x,y,ri.width,ri.yMax-y-80f);var pv=new Rect(0f,0f,pr.width-16f,pawns.Count*28f);
   Widgets.BeginScrollView(pr,ref scrollB,pv);
   for(int i=0;i<pawns.Count;i++){
    var p=pawns[i];var row=new Rect(0f,i*28f,pv.width,28f);string why=Why(p);
    if(p==selectedPawn)Widgets.DrawHighlightSelected(row);else Widgets.DrawHighlightIfMouseover(row);
    GUI.color=why==null?Color.white:Color.gray;Widgets.Label(new Rect(row.x+4f,row.y+3f,row.width-8f,24f),(p==selectedPawn?"● ":"○ ")+p.LabelShort+(why!=null?"（不行）":""));GUI.color=Color.white;
    if(why!=null)TooltipHandler.TipRegion(row,why);
    if(why==null && Widgets.ButtonInvisible(row))selectedPawn=p;
   }
   Widgets.EndScrollView();
   y=ri.yMax-76f;
   var bk=(src.key,days,selectedPawn);if(!bk.Equals(blockKey)){blockKey=bk;blockCache=FleshCatalysis.Blocked(building,src,days,selectedPawn);}
   string block=blockCache;var btn=new Rect(ri.x,y,ri.width,34f);
   if(block!=null){GUI.color=Color.gray;Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(btn,block);Text.Anchor=TextAnchor.UpperLeft;GUI.color=Color.white;}
   else if(Widgets.ButtonText(btn,"开始献祭（"+cost+" 份）")){
    if(FleshCatalysis.Order(building,src.key,days,selectedPawn)){SoundDefOf.Click.PlayOneShotOnCamera();Close();Messages.Message(selectedPawn.LabelShort+"去"+building.LabelShort+"献祭扭曲肉了。",selectedPawn,MessageTypeDefOf.SilentInput,false);return;}
    result="没能派人去";
   }
   if(result!=null){GUI.color=new Color(.9f,.5f,.3f);Widgets.Label(new Rect(ri.x,y+38f,ri.width,36f),result);GUI.color=Color.white;}
  }
 }
}
