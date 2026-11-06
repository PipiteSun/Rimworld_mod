using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.117.0 第三批事件（16 个，ArtSource 里没有规格，构思页见项目记忆）：公共部分。
 // 每个事件是一条 E3Record（kind 区分），存在 Events3State 里，每 250 刻推进一次；要玩家选的都走 Letter_E3。
 // 第一批（宝箱怪、神祇的乞丐、吟游诗人、银龙旅人、亡魂遗愿）在 Events3a.cs，第二批在 Events3b.cs，第三批在 Events3c.cs。
 // 所有事件最早第 15 天，间隔都比旧事件短（Peter 的要求）。
 public static class Events3 {
  public const int EarliestDay=15;
  public static Events3State State=>GameComp<Events3State>.Get();
  public static int Now=>Find.TickManager.TicksGame;
  public static float Roll=>Events2.Roll;
  public static Settings S=>LichMod.Settings;

  static FactionDef wayfarers;
  public static FactionDef WayfarersDef=>wayfarers??(wayfarers=DefDatabase<FactionDef>.GetNamedSilentFail("LP_Wayfarers"));
  // 隐藏的中立派系“旅人”：乞丐、吟游诗人、银龙化身、树人都挂在它下面（照云游导师的学者派系）。
  public static Faction Wayfarers(){
   if(WayfarersDef==null)return null;bool had=Factions.Of(WayfarersDef)!=null;
   var f=Factions.EnsureHidden(WayfarersDef);if(f!=null && !had)Factions.SetRelation(f,Faction.OfPlayer,0);
   return f;
  }
  public static Map Home=>Find.Maps.Where(m=>m.IsPlayerHome).OrderByDescending(m=>m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();
  public static List<Pawn> Colonists(Map map)=>map==null?new List<Pawn>():map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Dead).ToList();

  // ---------- 记录 ----------
  public static E3Record Add(string kind,Map map){var s=State;if(s==null)return null;var r=new E3Record{kind=kind,id=++s.nextId,map=map,start=Now};s.recs.Add(r);return r;}
  public static E3Record Rec(int id)=>State?.recs.FirstOrDefault(r=>r.id==id);
  public static IEnumerable<E3Record> Active(string kind)=>State==null?Enumerable.Empty<E3Record>():State.recs.Where(r=>r.kind==kind && !r.done);
  public static E3Record Of(Pawn p)=>p==null || State==null?null:State.recs.FirstOrDefault(r=>!r.done && (r.a==p || r.pawns.Contains(p)));
  public static void Finish(E3Record r){if(r!=null)r.done=true;}

  // ---------- 访客 ----------
  public static Pawn MakePawn(PawnKindDef kind,Faction f,Map map,float? age=null,Gender? gender=null){
   if(kind==null)return null;
   try{return PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,f,PawnGenerationContext.NonPlayer,map?.Tile??-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:age,fixedGender:gender));}
   catch(Exception e){Log.Warning("[DND边缘大陆] 第三批事件生成角色失败（"+kind.defName+"）："+e.Message);return null;}
  }
  // 从地图边走进来，在殖民地外落脚闲逛；kind 是访客标记（被殖民者打时回调 Harmed）。
  public static Lord Visit(Map map,List<Pawn> pawns,Faction f,string kind,IntVec3? spot=null){
   pawns=pawns.Where(p=>p!=null).ToList();if(pawns.Count==0)return null;
   if(!Events2.EdgeCell(map,out var entry)){foreach(var p in pawns)p.Destroy();return null;}
   foreach(var p in pawns){GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,4),map);if(kind!=null)Events2.MarkVisitor(p,kind);}
   IntVec3 at=spot??IntVec3.Invalid;
   if(!at.IsValid && !RCellFinder.TryFindRandomSpotJustOutsideColony(pawns[0],out at))at=CellFinder.RandomClosewalkCellNear(map.Center,map,10);
   return LordMaker.MakeNewLord(f,new LordJob_E3Visit(at),map,pawns);
  }
  public static void Leave(Lord lord){lord?.ReceiveMemo(LordJob_E3Visit.LeaveMemo);}
  public static void Vanish(Pawn p,bool flash=false){
   if(p==null || p.Destroyed)return;
   if(p.Spawned){var map=p.Map;for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(p.DrawPos,map,1.6f);if(flash)FleckMaker.Static(p.Position,map,FleckDefOf.PsycastAreaEffect,3f);p.DeSpawn();}
   if(!Find.WorldPawns.Contains(p))Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.Discard);
  }
  // 访客被殖民者打（Events2.Harmed 转过来）。
  public static void Harmed(Pawn victim,Pawn attacker,string kind){
   var r=Of(victim);if(r==null)return;
   switch(r.kind){
    case Beggar.Kind:Beggar.OnHarmed(r,attacker);break;
    case SilverGuest.Kind:SilverGuest.OnHarmed(r,attacker,"打了他");break;
    case Bards.Kind:Bards.OnHarmed(r);break;
   }
  }

  // ---------- 物品 ----------
  // 殖民地储存区或家园区里没被禁止的东西（照云游导师的写法）。
  public static List<Thing> Owned(Map map,Func<Thing,bool> pred)=>map==null?new List<Thing>():map.listerThings.AllThings.Where(t=>t.Spawned && t.def.category==ThingCategory.Item && pred(t) && !t.IsForbidden(Faction.OfPlayer) && (t.IsInAnyStorage() || map.areaManager.Home[t.Position])).ToList();
  public static int Count(Map map,Func<Thing,bool> pred)=>Owned(map,pred).Sum(t=>t.stackCount);
  public static bool Take(Map map,Func<Thing,bool> pred,int n){
   if(Count(map,pred)<n)return false;
   int left=n;foreach(var t in Owned(map,pred).OrderByDescending(x=>x.stackCount)){if(left<=0)break;int k=Math.Min(left,t.stackCount);t.SplitOff(k).Destroy();left-=k;}
   return true;
  }
  public static bool IsSilver(Thing t)=>t.def==ThingDefOf.Silver;
  public static Thing Make(string def,int n=1,QualityCategory? q=null){
   var d=Items.Thing(def);if(d==null)return null;
   var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.stackCount=Math.Max(1,Math.Min(n,d.stackLimit));
   if(q.HasValue)t.TryGetComp<CompQuality>()?.SetQuality(q.Value,ArtGenerationContext.Outsider);
   return t;
  }
  public static Thing Silver(int n){var t=ThingMaker.MakeThing(ThingDefOf.Silver);t.stackCount=n;return t;}
  public static List<Thing> Drop(Map map,IntVec3 at,IEnumerable<Thing> things){
   var list=things.Where(t=>t!=null).ToList();if(map==null)return list;
   if(!at.IsValid)at=DropCellFinder.TradeDropSpot(map);
   foreach(var t in list){GenPlace.TryPlaceThing(t,at,map,ThingPlaceMode.Near);t.SetForbidden(false,false);}
   return list;
  }
  public static List<Thing> DropPods(Map map,IEnumerable<Thing> things){
   var list=things.Where(t=>t!=null).ToList();if(map==null || list.Count==0)return list;
   DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map),map,list,110,false,false,true);
   return list;
  }
  public static string Labels(IEnumerable<Thing> things)=>string.Join("、",things.Where(t=>t!=null).Select(t=>t.LabelCap.ToString()));
  // 一件随机的普通武器（优秀品质），给逃亡贵族的谢礼用。
  public static Thing GoodWeapon(){
   var defs=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.IsWeapon && d.tradeability!=Tradeability.None && !d.destroyOnDrop && d.techLevel<=TechLevel.Industrial && d.techLevel>=TechLevel.Medieval && d.BaseMarketValue>=150 && d.HasComp(typeof(CompQuality)) && !d.IsStuff && d.weaponTags!=null && !d.weaponTags.Contains("TurretGun")).ToList();
   if(defs.Count==0)return null;var w=defs.RandomElement();
   var t=ThingMaker.MakeThing(w,w.MadeFromStuff?GenStuff.RandomStuffFor(w):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Outsider);return t;
  }

  // ---------- 心情 ----------
  public static void Thought(Pawn p,string def){var d=DefDatabase<ThoughtDef>.GetNamedSilentFail(def);if(d!=null)p?.needs?.mood?.thoughts?.memories?.TryGainMemory(d);}
  public static void ThoughtAll(Map map,string def){foreach(var c in Colonists(map))Thought(c,def);}

  // ---------- 信件 ----------
  public static Letter_E3 Ask(E3Record r,string label,string text,LookTargets look=null,Faction f=null){
   var l=(Letter_E3)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_E3ChoiceLetter"));
   l.Label=label;l.Text=text;l.rid=r.id;l.lookTargets=look??LookTargets.Invalid;l.relatedFaction=f;
   Find.LetterStack.ReceiveLetter(l);return l;
  }
  public static IEnumerable<DiaOption> Options(Letter_E3 l,E3Record r){
   switch(r.kind){
    case GhostWish.Kind:return GhostWish.Options(l,r);
    case Noble.Kind:return Noble.Options(l,r);
    case Ward.Kind:return Ward.Options(l,r);
    case FallenParty.Kind+"Join":return FallenParty.Options(l,r);
    case Calling.Kind:return Calling.Options(l,r);
    case Tourney.Kind:return Tourney.Options(l,r);
    case RivalLich.Kind:return RivalLich.Options(l,r);
    case Tarrasque.Kind:return Tarrasque.Options(l,r);
   }
   return Enumerable.Empty<DiaOption>();
  }

  // ---------- 世界站点 ----------
  public static SitePartDef SiteDef=>DefDatabase<SitePartDef>.GetNamedSilentFail("LP_E3Site");
  public static Site MakeSite(E3Record r,Faction f,float points,int min,int max,int days){
   var home=r.map??Home;if(home==null || SiteDef==null)return null;
   if(!TileFinder.TryFindNewSiteTile(out PlanetTile tile,home.Tile,min,max))return null;
   var site=SiteMaker.MakeSite(SiteDef,tile,f,true,points);if(site==null)return null;
   foreach(var p in site.parts){if(p.parms==null)p.parms=new SitePartParams();p.parms.threatPoints=points;}
   site.GetComponent<TimeoutComp>()?.StartTimeout(days*GenDate.TicksPerDay);
   Find.WorldObjects.Add(site);r.site=site;r.until=Now+days*GenDate.TicksPerDay;
   return site;
  }
  public static bool SiteCleared(E3Record r){
   if(!(r.site is Site s) || !s.HasMap)return false;
   return !s.Map.mapPawns.AllPawnsSpawned.Any(p=>!p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer) && !p.IsPrisoner);
  }
  public static void RemoveSite(E3Record r){if(r.site!=null && !r.site.Destroyed && !(r.site is MapParent mp && mp.HasMap))r.site.Destroy();}

  // ---------- 推进 ----------
  public static void Tick(Events3State s){
   for(int i=s.recs.Count-1;i>=0;i--){
    var r=s.recs[i];
    if(r.done){if(Now-r.start>120*GenDate.TicksPerDay || r.kind==Calling.Kind && Now-r.start>5*GenDate.TicksPerDay)s.recs.RemoveAt(i);continue;}
    try{
     switch(r.kind){
      case Mimic.Kind:Mimic.Tick(r);break;
      case Beggar.Kind:Beggar.Tick(r);break;
      case Bards.Kind:Bards.Tick(r);break;
      case SilverGuest.Kind:SilverGuest.Tick(r);break;
      case GhostWish.Kind:GhostWish.Tick(r);break;
      case Noble.Kind:Noble.Tick(r);break;
      case Ward.Kind:Ward.Tick(r);break;
      case FallenParty.Kind:FallenParty.Tick(r);break;
      case WeaveSurge.Kind:WeaveSurge.Tick(r);break;
      case Thief.Kind:Thief.Tick(r);break;
      case Calling.Kind:Calling.Tick(r);break;
      case RivalLich.Kind:RivalLich.Tick(r);break;
      case Treant.Kind:Treant.Tick(r);break;
      case Tarrasque.Kind:Tarrasque.Tick(r);break;
      default:if(r.until>0 && Now>r.until)r.done=true;break;
     }
    }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 第三批事件（"+r.kind+"）出错："+e,74120700+r.id%1000);r.done=true;}
   }
   if(Now%2500<250)Calling.Scan(s);
   // 2026-11-06 审查：砍树记录原来只在树人事件判定时清理，从不触发时整局都在涨（每砍一棵树一条，全进存档）。每天清掉 60 天以前的。
   if(Now%GenDate.TicksPerDay<250 && s.treeCuts.Count>0)Treant.RecentCuts();
  }
 }

 // ---------- 存档 ----------
 public class E3Record : IExposable {
  public string kind,text;public int id;public Map map;
  public List<Pawn> pawns=new List<Pawn>();public Pawn a,b;public Lord lord;public Thing thing;public WorldObject site;public Faction faction;
  public int start,until,stage,count,score,next;public float val,val2;public bool done,answered,flag;
  public SkillDef skill;public LP_DeityDef god;
  public Dictionary<Pawn,int> tally=new Dictionary<Pawn,int>();List<Pawn> tk;List<int> tv;
  public void ExposeData(){
   Scribe_Values.Look(ref kind,"kind");Scribe_Values.Look(ref text,"text");Scribe_Values.Look(ref id,"id");
   Scribe_References.Look(ref map,"map");ScribePawns.Look(ref pawns,"pawns");
   Scribe_References.Look(ref a,"a");Scribe_References.Look(ref b,"b");ScribeLord.Look(ref lord,"lord");
   Scribe_References.Look(ref thing,"thing");Scribe_References.Look(ref site,"site");Scribe_References.Look(ref faction,"faction");
   Scribe_Values.Look(ref start,"start");Scribe_Values.Look(ref until,"until");Scribe_Values.Look(ref stage,"stage");Scribe_Values.Look(ref count,"count");
   Scribe_Values.Look(ref score,"score");Scribe_Values.Look(ref next,"next");Scribe_Values.Look(ref val,"val");Scribe_Values.Look(ref val2,"val2");
   Scribe_Values.Look(ref done,"done");Scribe_Values.Look(ref answered,"answered");Scribe_Values.Look(ref flag,"flag");
   Scribe_Defs.Look(ref skill,"skill");Scribe_Defs.Look(ref god,"god");
   // 计数存成两张并排的表：引用解析不出来的人读档时直接跳过（字典的键不能是 null）。
   if(Scribe.mode==LoadSaveMode.Saving){tk=new List<Pawn>();tv=new List<int>();foreach(var kv in tally)if(kv.Key!=null && !kv.Key.Destroyed && !kv.Key.Discarded){tk.Add(kv.Key);tv.Add(kv.Value);}}
   Scribe_Collections.Look(ref tk,"tallyPawns",LookMode.Reference);Scribe_Collections.Look(ref tv,"tallyCounts",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    pawns=pawns??new List<Pawn>();tally=new Dictionary<Pawn,int>();
    if(tk!=null && tv!=null)for(int i=0;i<tk.Count && i<tv.Count;i++)if(tk[i]!=null)tally[tk[i]]=tv[i];
    tk=null;tv=null;
   }
  }
 }
 public class Events3State : GameComponent,IThingHolder {
  public List<E3Record> recs=new List<E3Record>();public int nextId;
  public ThingOwner<Thing> hold;                      // 被偷走的命匣（命匣窃贼）
  public List<string> deeds=new List<string>();       // 吟游诗人能唱的功绩
  public List<int> treeCuts=new List<int>();          // 殖民者砍树的时刻（树人苏醒看最近 60 天）
  public List<string> callingDone=new List<string>(); // 冒险者天命：已经判过的“人 + 等级档”
  public bool callingInit;
  public Dictionary<int,Faction> killers=new Dictionary<int,Faction>(); // 殖民者 thingID → 杀死他的派系（亡魂的遗愿“报仇”）
  List<int> kk;List<Faction> kv;
  public Events3State(Game game){hold=new ThingOwner<Thing>(this,false,LookMode.Deep);}
  public IThingHolder ParentHolder=>null;
  public ThingOwner GetDirectlyHeldThings()=>hold;
  public void GetChildHolders(List<IThingHolder> outChildren){ThingOwnerUtility.AppendThingHoldersFromThings(outChildren,hold);}
  public override void GameComponentTick(){if(Find.TickManager.TicksGame%250==197)Events3.Tick(this);}
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref recs,"lpE3Recs",LookMode.Deep);Scribe_Values.Look(ref nextId,"lpE3NextId");
   Scribe_Deep.Look(ref hold,"lpE3Hold",this);
   Scribe_Collections.Look(ref deeds,"lpE3Deeds",LookMode.Value);Scribe_Collections.Look(ref treeCuts,"lpE3TreeCuts",LookMode.Value);
   Scribe_Collections.Look(ref callingDone,"lpE3CallingDone",LookMode.Value);Scribe_Values.Look(ref callingInit,"lpE3CallingInit");
   if(Scribe.mode==LoadSaveMode.Saving)foreach(var k in killers.Where(x=>x.Value==null).Select(x=>x.Key).ToList())killers.Remove(k);
   Scribe_Collections.Look(ref killers,"lpE3Killers",LookMode.Value,LookMode.Reference,ref kk,ref kv);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    recs=recs?.Where(r=>r!=null).ToList()??new List<E3Record>();if(hold==null)hold=new ThingOwner<Thing>(this,false,LookMode.Deep);
    deeds=deeds??new List<string>();treeCuts=treeCuts??new List<int>();callingDone=callingDone??new List<string>();killers=killers??new Dictionary<int,Faction>();
   }
  }
  public void Deed(string d){if(!d.NullOrEmpty() && !deeds.Contains(d))deeds.Add(d);}
 }

 // ---------- 选择信件 ----------
 public class Letter_E3 : ChoiceLetter {
  public int rid;
  public override IEnumerable<DiaOption> Choices{get{
   var r=Events3.Rec(rid);
   if(ArchivedOnly || r==null || r.done || r.answered){yield return Option_Close;yield break;}
   foreach(var o in Events3.Options(this,r))yield return o;
   yield return Option_Postpone;
  }}
  public DiaOption Choice(string text,Action act,string disabled=null){
   var o=new DiaOption(text){action=()=>{act?.Invoke();Find.LetterStack.RemoveLetter(this);},resolveTree=true};
   if(disabled!=null)o.Disable(disabled);return o;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref rid,"lpE3Rid");}
 }

 // ---------- 访客小队：走到落脚点，在附近闲逛，收到离开信号就走 ----------
 public class LordJob_E3Visit : LordJob {
  public const string LeaveMemo="LP_E3Leave";
  public IntVec3 spot;
  public LordJob_E3Visit(){}
  public LordJob_E3Visit(IntVec3 spot){this.spot=spot;}
  public override StateGraph CreateGraph(){
   var g=new StateGraph();
   var travel=new LordToil_Travel(spot);g.StartingToil=travel;
   var stay=new LordToil_DefendPoint(spot,8f,8f);g.AddToil(stay);
   var leave=new LordToil_ExitMap(LocomotionUrgency.Walk,false,true);g.AddToil(leave);
   var arrive=new Transition(travel,stay);arrive.AddTrigger(new Trigger_Memo("TravelArrived"));arrive.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerDay/4));g.AddTransition(arrive);
   var go=new Transition(stay,leave);go.AddSource(travel);go.AddTrigger(new Trigger_Memo(LeaveMemo));go.AddTrigger(new Trigger_BecamePlayerEnemy());g.AddTransition(go);
   return g;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref spot,"spot");}
 }

 // ---------- 交互工作：开宝箱、鉴定宝箱、施舍乞丐 ----------
 public class JobDriver_E3Interact : JobDriver {
  public static int Ticks(JobDef d)=>d.defName=="LP_E3_IdentifyChest"?GenDate.TicksPerHour:d.defName=="LP_E3_OpenChest"?180:90;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>!(job.targetA.Thing is Pawn) ? pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed) : true;
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
   var wait=Toils_General.Wait(Ticks(job.def),TargetIndex.A);wait.WithProgressBarToilDelay(TargetIndex.A);yield return wait;
   yield return Toils_General.Do(()=>{
    var t=job.targetA.Thing;
    switch(job.def.defName){
     case "LP_E3_OpenChest":Mimic.Open(pawn,t as Building_MimicChest);break;
     case "LP_E3_IdentifyChest":Mimic.Identify(pawn,t as Building_MimicChest);break;
     case "LP_E3_Offer":Beggar.Offer(pawn,t as Pawn);break;
    }
   });
  }
 }
 public class FloatMenuOptionProvider_E3 : FloatMenuOptionProvider {
  protected override bool Drafted=>true;protected override bool Undrafted=>true;protected override bool Multiselect=>false;
  public override IEnumerable<FloatMenuOption> GetOptionsFor(Thing clickedThing,FloatMenuContext context){
   var p=context.FirstSelectedPawn;if(p==null || clickedThing==null || !p.IsColonistPlayerControlled)yield break;
   if(clickedThing is Building_MimicChest chest)foreach(var o in Mimic.MenuFor(p,chest))yield return o;
  }
  public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn,FloatMenuContext context){
   var p=context.FirstSelectedPawn;if(p==null || clickedPawn==null || !p.IsColonistPlayerControlled || clickedPawn.Dead)yield break;
   var r=Events3.Of(clickedPawn);
   if(r!=null && r.kind==Beggar.Kind && r.a==clickedPawn)foreach(var o in Beggar.MenuFor(p,r))yield return o;
  }
 }
 public static class E3Jobs {
  public static void Give(Pawn p,string jobDef,Thing target){var d=DefDatabase<JobDef>.GetNamedSilentFail(jobDef);if(d==null || p==null || target==null)return;p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(d,target),JobTag.Misc);}
 }

 // ---------- 所有事件的基类：设置开关 + 最早天数（XML 里写 15）+ DND 事件频率 ----------
 public abstract class IncidentWorker_E3 : IncidentWorker_DND {
  protected abstract bool On{get;}
  protected abstract bool CanFire(Map map,IncidentParms parms);
  protected override bool CanFireNowSub(IncidentParms parms)=>On && parms.target is Map map && map.IsPlayerHome && CanFire(map,parms);
 }

 // ==================== Harmony ====================
 // 击杀：宝箱怪、塔拉斯克的掉落；天命/遗愿的击杀计数；殖民者被谁杀死（报仇用）。
 public class E3KillState {public Pawn killer;public Map map;public IntVec3 at;public bool colonist;}
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class E3Kill {
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out E3KillState __state){
   __state=null;if(__instance.Dead)return;
   __state=new E3KillState{killer=dinfo?.Instigator as Pawn,map=__instance.MapHeld,at=__instance.PositionHeld,colonist=__instance.Faction!=null && __instance.Faction.IsPlayer && __instance.RaceProps.Humanlike};
  }
  static void Postfix(Pawn __instance,E3KillState __state){
   if(__state==null || !__instance.Dead)return;
   var s=Events3.State;if(s==null)return;
   try{
    if(__state.colonist && __state.killer?.Faction!=null && !__state.killer.Faction.IsPlayer)s.killers[__instance.thingIDNumber]=__state.killer.Faction;
    Mimic.OnDeath(__instance,__state);Tarrasque.OnDeath(__instance,__state);
    if(__state.killer!=null && __state.killer.Faction==Faction.OfPlayer && __instance.HostileTo(Faction.OfPlayer)){Calling.OnKill(__state.killer,__instance);GhostWish.OnKill(__instance);}
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 第三批事件击杀处理出错："+e,74120790);}
  }
 }
 // 魔网潮汐：施放能力时 20% 野魔法。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class E3AbilityCast {
  static void Postfix(RimWorld.Ability __instance,bool __result){if(__result)try{WeaveSurge.OnCast(__instance);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 魔网潮汐出错："+e,74120791);}}
 }
 // 天才学徒心情崩溃时野魔法爆发。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class E3MentalBreak {
  static readonly AccessTools.FieldRef<MentalStateHandler,Pawn> pawnRef=AccessTools.FieldRefAccess<MentalStateHandler,Pawn>("pawn");
  static void Postfix(MentalStateHandler __instance,bool __result){if(__result)try{Ward.OnBreak(pawnRef(__instance));}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 天才学徒出错："+e,74120792);}}
 }
 // 树人：记下殖民者砍的树；森林之心让周围作物长得快。
 [HarmonyPatch(typeof(Plant),nameof(Plant.PlantCollected))]
 public static class E3TreeCut {
  static void Postfix(Plant __instance,Pawn by){if(by!=null && by.Faction==Faction.OfPlayer && __instance.def.plant!=null && __instance.def.plant.IsTree)Events3.State?.treeCuts.Add(Find.TickManager.TicksGame);}
 }
 [HarmonyPatch(typeof(Plant),nameof(Plant.GrowthRate),MethodType.Getter)]
 public static class E3HeartTreeGrowth {
  static void Postfix(Plant __instance,ref float __result){if(__result>0f && Treant.NearHeart(__instance))__result*=Treant.HeartGrowth;}
 }

 // ==================== 设置 ====================
 public partial class Settings {
  public bool e3Mimic=true,e3Beggar=true,e3Bard=true,e3Silver=true,e3Ghost=true,e3Noble=true,e3Ward=true,e3Starfall=true;
  public bool e3Party=true,e3Surge=true,e3Thief=true,e3Calling=true,e3Tourney=true,e3Rival=true,e3Treant=true,e3Tarrasque=true;
  void ExposeEvents3(){
   Scribe_Values.Look(ref e3Mimic,"lpE3Mimic",true);Scribe_Values.Look(ref e3Beggar,"lpE3Beggar",true);Scribe_Values.Look(ref e3Bard,"lpE3Bard",true);Scribe_Values.Look(ref e3Silver,"lpE3Silver",true);
   Scribe_Values.Look(ref e3Ghost,"lpE3Ghost",true);Scribe_Values.Look(ref e3Noble,"lpE3Noble",true);Scribe_Values.Look(ref e3Ward,"lpE3Ward",true);Scribe_Values.Look(ref e3Starfall,"lpE3Starfall",true);
   Scribe_Values.Look(ref e3Party,"lpE3Party",true);Scribe_Values.Look(ref e3Surge,"lpE3Surge",true);Scribe_Values.Look(ref e3Thief,"lpE3Thief",true);Scribe_Values.Look(ref e3Calling,"lpE3Calling",true);
   Scribe_Values.Look(ref e3Tourney,"lpE3Tourney",true);Scribe_Values.Look(ref e3Rival,"lpE3Rival",true);Scribe_Values.Look(ref e3Treant,"lpE3Treant",true);Scribe_Values.Look(ref e3Tarrasque,"lpE3Tarrasque",true);
  }
 }
 public static class Events3Settings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.Label("1.117.0 新事件（都受“DND 事件频率”影响，最早第 15 天）：");
   l.CheckboxLabeled("宝箱怪",ref s.e3Mimic,"地图上出现一只无主的宝箱：七成是真宝箱，三成是宝箱怪。可以先让智识≥8的人鉴定。");
   l.CheckboxLabeled("神祇的乞丐",ref s.e3Beggar,"有守护神时，一个乞丐来讨东西（看守护神善恶）。施舍了，他第二天现出神的化身：神眷+15，施舍的人得到“蒙神一顾”。");
   l.CheckboxLabeled("吟游诗人巡演",ref s.e3Bard,"2～4名吟游诗人来演一晚：全殖民地心情+，冒险者经验；把殖民地最大的功绩写成歌谣，各派系好感+5。恶名在外会被写成讽刺歌。");
   l.CheckboxLabeled("化身旅人的银龙",ref s.e3Silver,"一位老旅人借宿3天，按吃住和陪伴打分；走时现出银龙真身，好的招待换龙鳞、“银龙之友”，极好的有几率留下龙蛋。");
   l.CheckboxLabeled("亡魂的遗愿",ref s.e3Ghost,"死去3～15天的殖民者托付最后一件事（安葬、照顾亲人或报仇）。完成后受托人继承他一半的技能经验。");
   l.CheckboxLabeled("逃亡的同盟贵族",ref s.e3Noble,"受伤的同盟贵族被塞伊追杀，逃到殖民地：庇护、交给塞伊或卖给黑网。");
   l.CheckboxLabeled("托付的天才学徒（需要 Biotech）",ref s.e3Ward,"同盟的法师把一个魔法天才托付给你，定期寄来礼物；成年时智识够了就成为法师。");
   l.CheckboxLabeled("秘银陨星",ref s.e3Starfall,"一晚流星雨，天亮前落下秘银陨石；一成几率带着坠星之心，会引来抢夺。");
   l.CheckboxLabeled("全灭的冒险小队",ref s.e3Party,"世界地图上出现一支全灭冒险小队的营地，杀死他们的怪物还在。装备、冒险日志，复活他们就能招募。");
   l.CheckboxLabeled("魔网潮汐",ref s.e3Surge,"5天极光：每次施放能力 20% 触发野魔法（好坏都有），施法职业每天额外得冒险者经验。");
   l.CheckboxLabeled("命匣窃贼",ref s.e3Thief,"隐身的盗贼夜里潜入偷巫妖的命匣。偷走了要在15天内去藏身处抢回来，否则命匣被卖到九狱掮客手里（可赎回）。");
   l.CheckboxLabeled("冒险者的天命",ref s.e3Calling,"冒险者第一次到5、10、15级时，各有一半几率接到个人任务；完成后三选一：属性+1、武艺天赋、传承武器。");
   l.CheckboxLabeled("骑士比武大会",ref s.e3Tourney,"同盟邀请你参加比武：剑术、长枪、射箭、摔角各派一人，按技能和属性掷骰。赢了拿奖杯和经验，三项以上得冠军称号。");
   l.CheckboxLabeled("敌对巫妖的战书",ref s.e3Rival,"殖民地有命匣5级以上的巫妖时，一名塞伊巫妖下战书：法术决斗、命匣狩猎或拒绝。");
   l.CheckboxLabeled("树人苏醒",ref s.e3Treant,"林木多的地图上，最老的树站了起来：砍树少就送一棵森林之心（周围作物生长+20%），砍得多就带着苏醒的树来报复。");
   l.CheckboxLabeled("塔拉斯克苏醒",ref s.e3Tarrasque,"传说中的巨兽醒了，约20天后到。可以请援军、设陷阱或献肉引开。打死它得到甲壳和獠牙（顶级材料）。");
  }
 }
}
