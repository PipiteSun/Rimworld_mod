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
 // 1.55.0 异界契约（ArtSource/PlanarPact-1.55.0-spec.md 第一节）：宗主定义 LP_PatronDef（Defs/Pact_Patrons.xml）、契约数据、签约 / 偿契 / 违约 / 解约、契约书、契约讲台、魔能爆。
 // 恩赐在 PactBoons.cs，新单位在 PactRaces.cs，事件在 PactEvents.cs，圣坛祈唤与破契仪式在 PactCelestial.cs。
 public enum PactTerm{Year,Life,Soul}
 public class LP_PactClause {
  public string kind,label,description,category;public int count;public float intervalDays;public bool onExpiry;
  public PactClauseState Make(bool hidden)=>new PactClauseState{kind=kind,label=label,description=description,category=category,count=count,intervalDays=intervalDays,onExpiry=onExpiry,hidden=hidden};
 }
 public class LP_PactBoon {
  public string key,label,description;public Type workerClass;public HediffDef hediff;public AbilityDef ability;public PawnKindDef kind;public ThingDef thing;
  public int count=1;public float days;public float clauseFactor=1f;
  [Unsaved] PactBoonWorker worker;
  public PactBoonWorker Worker{get{if(worker==null && workerClass!=null){worker=(PactBoonWorker)Activator.CreateInstance(workerClass);worker.boon=this;}return worker;}}
 }
 public class LP_PatronDef : Def {
  public OutsiderPlane plane=OutsiderPlane.Hells;public PactTerm term=PactTerm.Life;
  public List<LP_PactBoon> boons=new List<LP_PactBoon>();public int boonChoices=3;
  public List<LP_PactClause> visibleClauses=new List<LP_PactClause>(),hiddenClausePool=new List<LP_PactClause>(),demands=new List<LP_PactClause>();
  public int hiddenCount;public IntRange demandInterval=new IntRange(15,15);public int reviewDC=15;public bool negotiable;public float mutation,taintOnSign;
  public string iconPath,agentLabel;public Color color=Color.white;
  [Unsaved] Texture2D icon;
  public Texture2D Icon=>icon??(icon=(iconPath.NullOrEmpty()?null:ContentFinder<Texture2D>.Get(iconPath,false))??BaseContent.BadTex);
  public bool Hells=>plane==OutsiderPlane.Hells;public bool Fey=>plane==OutsiderPlane.Feywild;
  public string PlaneLabel=>Hells?"九狱":Fey?"妖精荒野":"异界";
  public string TermLabel=>term==PactTerm.Year?"一年":term==PactTerm.Soul?"灵魂":"终身";
  public LP_PactBoon Boon(string key)=>boons.FirstOrDefault(b=>b.key==key);
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(boons.Count==0)yield return defName+": patron has no boons";
   foreach(var b in boons){
    if(b.key.NullOrEmpty())yield return defName+": boon without key";
    if(b.workerClass==null || !typeof(PactBoonWorker).IsAssignableFrom(b.workerClass))yield return defName+": boon "+b.key+" needs a PactBoonWorker workerClass";
   }
   foreach(var c in visibleClauses.Concat(hiddenClausePool).Concat(demands))if(!Pacts.KnownKinds.Contains(c.kind))yield return defName+": unknown clause kind "+c.kind;
   if(hiddenCount>hiddenClausePool.Count)yield return defName+": hiddenCount larger than the pool";
  }
 }

 // ---------- 契约数据 ----------
 public class PactClauseState : IExposable {
  public string kind,label,description,category;public int count;public float intervalDays;public bool onExpiry,hidden,revealed,consumed;public int nextTick=-1;
  public bool Visible=>!hidden || revealed || !Pacts.HiddenClauses;
  public bool IsDemand=>Pacts.DemandKinds.Contains(kind) && intervalDays<=0f && !onExpiry;
  public string Line=>label+(description.NullOrEmpty()?"":"：“"+description+"”");
  public void ExposeData(){
   Scribe_Values.Look(ref kind,"kind");Scribe_Values.Look(ref label,"label");Scribe_Values.Look(ref description,"desc");Scribe_Values.Look(ref category,"cat");Scribe_Values.Look(ref count,"count");
   Scribe_Values.Look(ref intervalDays,"interval");Scribe_Values.Look(ref onExpiry,"onExpiry");Scribe_Values.Look(ref hidden,"hidden");Scribe_Values.Look(ref revealed,"revealed");Scribe_Values.Look(ref consumed,"consumed");Scribe_Values.Look(ref nextTick,"next",-1);
  }
 }
 public class PlanarPact : IExposable {
  public int id;public Pawn signer;public LP_PatronDef patron,boonPatron;public string boonKey;public List<PactClauseState> clauses=new List<PactClauseState>();
  public int debt,signTick,expireTick=-1,nextDemandTick=-1,demandClause=-1,demandStart=-1,demandDeadline=-1,nextBoonTick=-1,lastCollectorTick=-1,nextMutationTick=-1,lastWinterYear=-1,resaleTick=-1;
  public bool ended,lichMortgage,soulGiven,finalDemand,demandPaid;public float periodFactor=1f,boonFactor=1f;public string endReason;
  public Pawn bound;public List<Pawn> guards=new List<Pawn>();
  public LP_PactBoon Boon=>(boonPatron??patron)?.Boon(boonKey);
  public bool Active=>!ended;
  public string Label=>(lichMortgage?"命匣抵押：":"契约：")+patron.label;
  public PactClauseState Demand=>demandClause>=0 && demandClause<clauses.Count?clauses[demandClause]:null;
  public bool Has(string kind)=>clauses.Any(c=>c.kind==kind && !c.consumed);
  public PactClauseState Clause(string kind)=>clauses.FirstOrDefault(c=>c.kind==kind && !c.consumed);
  public void ExposeData(){
   Scribe_Values.Look(ref id,"id");Scribe_References.Look(ref signer,"signer",true);Scribe_Defs.Look(ref patron,"patron");Scribe_Defs.Look(ref boonPatron,"boonPatron");Scribe_Values.Look(ref boonKey,"boon");
   Scribe_Collections.Look(ref clauses,"clauses",LookMode.Deep);
   Scribe_Values.Look(ref debt,"debt");Scribe_Values.Look(ref signTick,"signTick");Scribe_Values.Look(ref expireTick,"expire",-1);Scribe_Values.Look(ref nextDemandTick,"nextDemand",-1);
   Scribe_Values.Look(ref demandClause,"demandClause",-1);Scribe_Values.Look(ref demandStart,"demandStart",-1);Scribe_Values.Look(ref demandDeadline,"demandDeadline",-1);Scribe_Values.Look(ref demandPaid,"demandPaid");
   Scribe_Values.Look(ref nextBoonTick,"nextBoon",-1);Scribe_Values.Look(ref lastCollectorTick,"lastCollector",-1);Scribe_Values.Look(ref nextMutationTick,"nextMutation",-1);Scribe_Values.Look(ref lastWinterYear,"lastWinter",-1);Scribe_Values.Look(ref resaleTick,"resale",-1);
   Scribe_Values.Look(ref ended,"ended");Scribe_Values.Look(ref lichMortgage,"lichMortgage");Scribe_Values.Look(ref soulGiven,"soulGiven");Scribe_Values.Look(ref finalDemand,"finalDemand");
   Scribe_Values.Look(ref periodFactor,"periodFactor",1f);Scribe_Values.Look(ref boonFactor,"boonFactor",1f);Scribe_Values.Look(ref endReason,"endReason");
   Scribe_References.Look(ref bound,"bound");Scribe_Collections.Look(ref guards,"guards",LookMode.Reference);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(clauses==null)clauses=new List<PactClauseState>();guards=guards?.Where(g=>g!=null).ToList()??new List<Pawn>();}
  }
 }
 // 被强制执行拖进位面的人（可以赎回）。
 public class TakenPawn : IExposable {
  public Pawn pawn;public OutsiderPlane plane;public int tick;
  public void ExposeData(){Scribe_References.Look(ref pawn,"pawn",true);Scribe_Values.Look(ref plane,"plane");Scribe_Values.Look(ref tick,"tick");}
 }

 // ---------- 存档组件 ----------
 public class GameComponent_Pacts : GameComponent, IThingHolder {
  public List<PlanarPact> pacts=new List<PlanarPact>();public int nextId=1;
  public Dictionary<string,int> patronCooldown=new Dictionary<string,int>();
  public int lastExecutionTick=-1,lastGatheringTick=-1;
  public List<TakenPawn> taken=new List<TakenPawn>();
  public ThingOwner<Thing> seized;public List<Pawn> seizedOwners=new List<Pawn>(); // 被收走的命匣（照拉洛克“以命匣换卷”的写法放在组件里）
  public bool brokersRobbed;public int planetarReadyTick=-1,lastMarketQuadrum=-1;
  public List<PactHunt> hunts=new List<PactHunt>();public PactMarket market;public PactBloodWar bloodWar;
  public int hourTick=-1,dayTick=-1;
  public GameComponent_Pacts(Game game){seized=new ThingOwner<Thing>(this,false,LookMode.Deep);}
  public static GameComponent_Pacts Get=>GameComp<GameComponent_Pacts>.Get();
  public IEnumerable<PlanarPact> Active=>pacts.Where(p=>p.Active);
  public ThingOwner GetDirectlyHeldThings()=>seized;
  public void GetChildHolders(List<IThingHolder> outChildren){ThingOwnerUtility.AppendThingHoldersFromThings(outChildren,GetDirectlyHeldThings());}
  public IThingHolder ParentHolder=>null;
  public override void FinalizeInit(){base.FinalizeInit();Pacts.Refresh(this);}
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;if(now%250!=73)return;
   if(hunts.Count>0 || market!=null || bloodWar!=null)PactEvents.Tick250(this,now);
   if(now-hourTick>=GenDate.TicksPerHour){hourTick=now;PactEvents.Hourly(this,now);}
   if(now-dayTick>=GenDate.TicksPerDay){dayTick=now;PactCelestial.Daily(now);}
   if(now%2500!=73)return;
   if(pacts.Count==0 && taken.Count==0)return; // 第八节：契约检查只遍历契约列表，没有契约直接返回
   Pacts.CheckAll(this,now);
  }
  public override void ExposeData(){
   Scribe_Collections.Look(ref pacts,"lpPacts",LookMode.Deep);Scribe_Values.Look(ref nextId,"lpPactNextId",1);
   Scribe_Collections.Look(ref patronCooldown,"lpPatronCooldown",LookMode.Value,LookMode.Value);
   Scribe_Values.Look(ref lastExecutionTick,"lpLastExecution",-1);Scribe_Values.Look(ref lastGatheringTick,"lpLastGathering",-1);
   Scribe_Collections.Look(ref taken,"lpTaken",LookMode.Deep);
   Scribe_Deep.Look(ref seized,"lpSeized",this);Scribe_Collections.Look(ref seizedOwners,"lpSeizedOwners",LookMode.Reference);
   Scribe_Values.Look(ref brokersRobbed,"lpBrokersRobbed");Scribe_Values.Look(ref planetarReadyTick,"lpPlanetarReady",-1);Scribe_Values.Look(ref lastMarketQuadrum,"lpLastMarket",-1);
   Scribe_Collections.Look(ref hunts,"lpHunts",LookMode.Deep);Scribe_Deep.Look(ref market,"lpMarket");Scribe_Deep.Look(ref bloodWar,"lpBloodWar");
   Scribe_Values.Look(ref hourTick,"lpPactHour",-1);Scribe_Values.Look(ref dayTick,"lpPactDay",-1);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    pacts=pacts?.Where(p=>p?.patron!=null).ToList()??new List<PlanarPact>();if(patronCooldown==null)patronCooldown=new Dictionary<string,int>();
    taken=taken?.Where(t=>t?.pawn!=null).ToList()??new List<TakenPawn>();if(seized==null)seized=new ThingOwner<Thing>(this,false,LookMode.Deep);seizedOwners=seizedOwners??new List<Pawn>();
    hunts=hunts?.Where(h=>h!=null).ToList()??new List<PactHunt>();
    Pacts.Refresh(this);
   }
  }
 }

 // ---------- 规则 ----------
 public static class Pacts {
  public const int ReadTicks=GenDate.TicksPerHour*4,DemandDays=3,MaxReviews=2,NegotiateDC=16,LawyerBonus=0,CollectorDays=15,RefuseCooldownDays=60,MaxDebt=3;
  public const float AffinityReview=2f,AffinityPeriod=1.33f,EvilPeriod=1.15f,GoodFavorLoss=20f,LichBoonFactor=1.5f,RansomSilver=3000f,RansomTaint=.2f;
  public const int YearTicks=GenDate.TicksPerYear,QuadrumTicks=GenDate.TicksPerQuadrum;
  public static readonly HashSet<string> DemandKinds=new HashSet<string>{"SacrificePrisoner","Silver","Item","Gathering","Secret","Name","Year"};
  public static readonly HashSet<string> AutoKinds=new HashSet<string>{"Secret","Name","Year"};
  public static readonly HashSet<string> KnownKinds=new HashSet<string>{"SacrificePrisoner","Silver","Item","Gathering","Secret","Name","Year","SoulTax","Interest","Firstborn","Hellfire","LongWinter","IceHeart","Collateral","Resale","NoTreeCut","WinterFirstborn","WinterKiss","YearOrEye"};
  public static float? TestRoll;public static float Roll=>TestRoll??Rand.Value;public static bool TestNoCap;
  public static int activeCount; // 快速返回用：现在有几份生效的契约
  public static bool Any=>activeCount>0;
  public static int Now=>Find.TickManager?.TicksGame??0;
  static ThingDef scroll,lectern,forge;static HediffDef signerDef;static AbilityDef blast;static ResearchProjectDef research;static LetterDef letter;
  public static ThingDef ScrollDef=>scroll??(scroll=DefDatabase<ThingDef>.GetNamedSilentFail("LP_PactScroll"));
  public static ThingDef LecternDef=>lectern??(lectern=DefDatabase<ThingDef>.GetNamedSilentFail("LP_PactLectern"));
  public static ThingDef ForgeDef=>forge??(forge=DefDatabase<ThingDef>.GetNamedSilentFail("LP_InfernalForge"));
  public static HediffDef SignerDef=>signerDef??(signerDef=DefDatabase<HediffDef>.GetNamedSilentFail("LP_PactSigner"));
  public static AbilityDef BlastDef=>blast??(blast=DefDatabase<AbilityDef>.GetNamedSilentFail("LP_EldritchBlast"));
  public static ResearchProjectDef Research=>research??(research=DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LP_Research_PlanarPacts"));
  public static LetterDef Letter=>letter??(letter=DefDatabase<LetterDef>.GetNamedSilentFail("LP_PactLetter"));
  public static IEnumerable<LP_PatronDef> Patrons=>DefDatabase<LP_PatronDef>.AllDefsListForReading;
  public static LP_PatronDef Patron(string name)=>DefDatabase<LP_PatronDef>.GetNamedSilentFail(name);
  public static GameComponent_Pacts Comp=>GameComponent_Pacts.Get;

  // ---------- 设置 ----------
  public static bool Enabled=>LichMod.Settings?.pactEnabled??true;
  public static bool HiddenClauses=>LichMod.Settings?.pactHiddenClauses??true;
  public static float DemandFrequency=>(LichMod.Settings?.pactDemandPercent??100)/100f;
  public static float CollectorStrength=>(LichMod.Settings?.pactCollectorPercent??100)/100f;
  public static float ChangelingChance=>(LichMod.Settings?.pactChangelingPercent??3)/100f;
  public static bool Celestial=>LichMod.Settings?.pactCelestial??true;

  public static void Refresh(GameComponent_Pacts c){activeCount=c?.pacts.Count(p=>p.Active)??0;PactBoons.RefreshMaps(c);}
  public static PlanarPact Of(Pawn p){if(activeCount==0 || p==null)return null;var c=Comp;if(c==null)return null;/* 1.58.6：先看计数 */foreach(var x in c.pacts)if(x.Active && x.signer==p)return x;return null;}
  public static PlanarPact ById(int id){var c=Comp;if(c==null)return null;var l=c.pacts;for(int i=0;i<l.Count;i++)if(l[i].id==id)return l[i];return null;} // 1.58.6：不用 LINQ 闭包（签约状态每个健康刻都会问）
  public static int Cap()=>1+AdventurerEnemies.ColonyMaxLevel()/8;
  public static int ActiveCount()=>Comp?.pacts.Count(p=>p.Active)??0;
  public static Map HomeOf(PlanarPact p)=>p?.signer?.MapHeld??Find.Maps.FirstOrDefault(m=>m.IsPlayerHome);

  // ---------- 种族与信仰修正（规格 1.5 的表） ----------
  public static bool Affinity(Pawn p,LP_PatronDef d)=>d!=null && p!=null && (d.Hells && (FaithRaces.IsTiefling(p) || AbyssRaces.IsCambion(p)) || d.Fey && Items.IsElf(p));
  public static int ReviewDC(Pawn p,LP_PatronDef d)=>d.reviewDC-(Affinity(p,d)?(int)AffinityReview:0);
  public static float IntervalFactor(Pawn p,LP_PatronDef d){
   float f=1f;if(Affinity(p,d))f*=AffinityPeriod;
   var god=FaithUtility.DevoteeGod(p);if(god!=null && god.Evil && d.Hells)f*=EvilPeriod;
   return f;
  }
  public static string CannotSign(Pawn p,LP_PatronDef d){
   if(p==null || d==null)return "没有签约人";
   if(p.Dead || !p.RaceProps.Humanlike)return p.LabelShort+"不能签约";
   if(Outsiders.Is(p))return "外来者不能和宗主签约";
   if(Of(p)!=null)return p.LabelShort+"已经有一份契约了（一人一份）";
   if(!TestNoCap && ActiveCount()>=Cap())return "殖民地的契约已满（上限 "+Cap()+" = 1 + 最高冒险者等级 / 8）";
   if(d.Hells && FaithRaces.IsAasimar(p) && AbyssTaint.Severity(p)<AbyssTaint.AasimarLeaderMin)return p.LabelShort+"拒绝了：阿斯莫不和九狱签约";
   return null;
  }

  // ---------- 签约 ----------
  public static PlanarPact Sign(Pawn p,CompPactScroll s,string boonKey){
   var d=s?.patron;if(d==null || CannotSign(p,d)!=null)return null;var boon=d.Boon(boonKey);if(boon==null)return null;
   var c=Comp;int now=Now;
   var pact=new PlanarPact{id=c.nextId++,signer=p,patron=d,boonPatron=d,boonKey=boonKey,signTick=now};
   foreach(var x in s.Draft())pact.clauses.Add(Copy(x));
   pact.periodFactor=IntervalFactor(p,d);
   if(d.term==PactTerm.Year)pact.expireTick=now+YearTicks;
   if(d.term==PactTerm.Soul){if(Items.IsLich(p)){pact.lichMortgage=true;pact.boonFactor=LichBoonFactor;}else{var m=Items.Hediff("LP_SoulMortgaged");if(m!=null && !Items.Has(p,m)){p.health.AddHediff(m);pact.soulGiven=true;}}}
   if(d.taintOnSign>0f)AbyssTaint.Add(p,d.taintOnSign,"与"+d.label+"签约");
   var god=FaithUtility.DevoteeGod(p);if(god!=null && god.Good && d.Hells)FaithUtility.AddFavor(god,-GoodFavorLoss,p.LabelShort+"和"+d.label+"签了契约");
   ScheduleDemand(pact,now);
   foreach(var cl in pact.clauses)if(cl.intervalDays>0f)cl.nextTick=now+Ticks(pact,cl.intervalDays);
   if(d.mutation>0f)pact.nextMutationTick=now+QuadrumTicks;
   if(pact.Has("Resale"))pact.resaleTick=now+YearTicks;
   c.pacts.Add(pact);Refresh(c);
   SyncSigner(pact);
   if(p.abilities!=null && BlastDef!=null && p.abilities.GetAbility(BlastDef)==null)p.abilities.GainAbility(BlastDef);
   boon.Worker?.OnSign(pact);
   ApplyStatic(pact);
   if(s.parent!=null && !s.parent.Destroyed)s.parent.Destroy();
   Find.LetterStack.ReceiveLetter("签下契约："+d.label,p.LabelShort+"在"+d.label+"的契约上签了名（"+(pact.lichMortgage?"命匣抵押":d.TermLabel)+"）。\n\n恩赐："+boon.label+"——"+boon.description+"\n\n"+ClauseText(pact)+"\n\n"+NextDemandText(pact)+"\n\n签约人获得了能力“魔能爆”。",LetterDefOf.NeutralEvent,p);
   if(pact.Has("YearOrEye"))PactLetters.Send("hags",pact,p,null,"巫婆集会的代价",p.LabelShort+"签下了巫婆集会的契约。三位巫婆咯咯笑着伸出手：先付一笔定金——一年寿命，还是一只眼睛？");
   return pact;
  }
  static PactClauseState Copy(PactClauseState x)=>new PactClauseState{kind=x.kind,label=x.label,description=x.description,category=x.category,count=x.count,intervalDays=x.intervalDays,onExpiry=x.onExpiry,hidden=x.hidden,revealed=x.revealed};
  // 隐藏条款里带状态的（地狱之火、冰心、冬之吻）签约时就生效，不管有没有揭开。
  public static void ApplyStatic(PlanarPact p){
   var s=p.signer;if(s==null || s.Dead)return;
   if(p.Has("Hellfire"))Items.Give(s,"LP_Clause_Hellfire");
   if(p.Has("IceHeart"))Items.Give(s,"LP_Clause_IceHeart");
   if(p.Has("WinterKiss"))Items.Give(s,"LP_Clause_WinterKiss");
  }
  static void RemoveStatic(Pawn s){foreach(var n in new[]{"LP_Clause_Hellfire","LP_Clause_IceHeart","LP_Clause_WinterKiss"})Items.Remove(s,n);}
  public static int Ticks(PlanarPact p,float days)=>Mathf.Max(GenDate.TicksPerHour,Mathf.RoundToInt(days*GenDate.TicksPerDay*p.periodFactor/Mathf.Max(.1f,DemandFrequency)));
  public static void ScheduleDemand(PlanarPact p,int now){
   if(!p.clauses.Any(c=>c.IsDemand)){p.nextDemandTick=-1;return;}
   p.nextDemandTick=now+Ticks(p,p.patron.demandInterval.RandomInRange);
  }
  public static void SyncSigner(PlanarPact p){
   var s=p.signer;if(s?.health==null || SignerDef==null)return;
   var h=s.health.hediffSet.GetFirstHediffOfDef(SignerDef) as Hediff_PactSigner;
   if(p.ended){if(h!=null)s.health.RemoveHediff(h);return;}
   if(h==null){h=(Hediff_PactSigner)HediffMaker.MakeHediff(SignerDef,s);s.health.AddHediff(h);h=s.health.hediffSet.GetFirstHediffOfDef(SignerDef) as Hediff_PactSigner;}
   if(h!=null){h.pactId=p.id;h.Severity=Mathf.Max(.01f,p.debt);}
  }
  public static string ClauseText(PlanarPact p)=>ClauseText(p.clauses);
  public static string ClauseText(List<PactClauseState> list){
   var lines=new List<string>();
   foreach(var c in list){if(c.consumed)continue;lines.Add(c.Visible?"· "+c.label+(c.hidden?"（已揭开的隐藏条款）":"")+"："+c.description:"· ？？？（隐藏条款）");}
   return "条款：\n"+(lines.Count==0?"（无）":string.Join("\n",lines));
  }
  public static string NextDemandText(PlanarPact p){
   if(p.nextDemandTick<0)return p.expireTick>=0?"契约在 "+(p.expireTick-Now).ToStringTicksToPeriod()+"后到期。":"这份契约没有周期性的偿契要求。";
   return "下一次偿契在 "+Mathf.Max(0,p.nextDemandTick-Now).ToStringTicksToPeriod()+"后。"+(p.expireTick>=0?"契约在 "+(p.expireTick-Now).ToStringTicksToPeriod()+"后到期。":"");
  }

  // ---------- 审阅与谈判 ----------
  public static Pawn Lawyer(Map map,Pawn except)=>map?.mapPawns.FreeColonistsSpawned.Where(c=>c!=except && !c.Downed && !Outsiders.Is(c)).OrderByDescending(c=>Mathf.Max(c.skills?.GetSkill(SkillDefOf.Social)?.Level??0,c.skills?.GetSkill(SkillDefOf.Intellectual)?.Level??0)).FirstOrDefault();
  // 审阅：签约人（第二次是律师，再加一次熟练加值）做智力豁免；成功揭开一条隐藏条款。
  public static bool Review(Pawn signer,CompPactScroll s,Pawn lawyer=null){
   if(s==null || s.reviews>=MaxReviews)return false;
   var who=lawyer??signer;int dc=ReviewDC(signer,s.patron);if(lawyer!=null)dc-=Legendary.Proficiency(Legendary.LevelOf(lawyer));
   s.reviews++;
   bool ok=Legendary.Save(who,Adventurer.Int,dc);
   if(ok){var h=s.Draft().FirstOrDefault(c=>c.hidden && !c.revealed);if(h!=null)h.revealed=true;}
   return ok;
  }
  // 谈判（只有九狱，一次）：魅力豁免 DC 16。成功由第二封信删一条可见代价；失败从隐藏池再抽一条可见代价。
  public static bool Negotiate(Pawn signer,CompPactScroll s){
   if(s==null || s.negotiated || !s.patron.negotiable)return false;s.negotiated=true;
   bool ok=Legendary.Save(signer,Adventurer.Cha,NegotiateDC);
   if(!ok){var pool=s.patron.hiddenClausePool.Where(c=>!s.Draft().Any(x=>x.kind==c.kind)).ToList();if(pool.Count>0)s.Draft().Add(pool.RandomElement().Make(false));}
   return ok;
  }
  public static bool Strike(CompPactScroll s,int index){var d=s.Draft();if(index<0 || index>=d.Count || d[index].hidden)return false;d.RemoveAt(index);return true;}
  public static void Refuse(Pawn signer,CompPactScroll s){
   var c=Comp;if(c!=null && s?.patron!=null)c.patronCooldown[s.patron.defName]=Now+RefuseCooldownDays*GenDate.TicksPerDay;
   var t=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_PactMissed");if(t!=null)signer?.needs?.mood?.thoughts?.memories?.TryGainMemory(t);
   if(s?.parent!=null && !s.parent.Destroyed)s.parent.Destroy();
  }
  public static void OnRead(Pawn p,Thing scroll){
   var s=scroll?.TryGetComp<CompPactScroll>();if(s==null || p==null)return;s.Draft();
   PactLetters.Send("offer",null,p,scroll,"契约书："+s.patron.label,OfferText(p,s));
  }
  public static string OfferText(Pawn p,CompPactScroll s){
   var d=s.patron;var lines=new List<string>();
   lines.Add(p.LabelShort+"读完了"+d.label+"的契约书（"+d.PlaneLabel+"，期限："+(d.term==PactTerm.Soul && Items.IsLich(p)?"命匣抵押":d.TermLabel)+"）。");
   if(!d.description.NullOrEmpty())lines.Add(d.description);
   lines.Add("\n可选的恩赐（三选一）：");foreach(var b in s.Offered())lines.Add("· "+b.label+"："+b.description);
   lines.Add("\n"+ClauseText(s.Draft()));
   var dem=s.Draft().Where(c=>c.IsDemand && c.Visible).ToList();
   if(dem.Count>0)lines.Add("偿契周期：约每 "+(d.demandInterval.Average*IntervalFactor(p,d)/Mathf.Max(.1f,DemandFrequency)).ToString("0.#")+" 天一次，每次给 "+DemandDays+" 天。");
   if(d.term==PactTerm.Year)lines.Add("一年后到期。");
   if(d.term==PactTerm.Soul)lines.Add(Items.IsLich(p)?"巫妖签灵魂契约：改为“命匣抵押”，恩赐数值 ×1.5；违约到底时被收走的是命匣。":"灵魂契约：签约人得到“灵魂抵押”，死后不能复活、尸体消失。");
   if(d.taintOnSign>0)lines.Add("签约时签约人深渊腐化 +"+d.taintOnSign.ToStringPercent()+"。");
   if(Affinity(p,d))lines.Add(p.LabelShort+"的血脉和这位宗主相近：审阅 +2，偿契周期 ×1.33。");
   var god=FaithUtility.DevoteeGod(p);if(god!=null && god.Good && d.Hells)lines.Add(god.label+"不会高兴：签下后神眷 -20。");
   if(god!=null && god.Evil && d.Hells)lines.Add(god.label+"的信徒：九狱代价周期 ×1.15。");
   lines.Add("\n审阅 "+s.reviews+" / "+MaxReviews+"（难度 "+ReviewDC(p,d)+"，"+Legendary.SaveChanceText(p,Adventurer.Int,ReviewDC(p,d))+"）"+(d.negotiable?"；谈判"+(s.negotiated?"已用":"可用（难度 "+NegotiateDC+"，"+Legendary.SaveChanceText(p,Adventurer.Cha,NegotiateDC)+"）"):"；这位宗主不谈判")+"。");
   var why=CannotSign(p,d);if(why!=null)lines.Add("\n现在签不了："+why+"。");
   return string.Join("\n",lines);
  }

  // ---------- 每 2500 刻的契约检查 ----------
  public static void CheckAll(GameComponent_Pacts c,int now){
   for(int i=0;i<c.pacts.Count;i++){var p=c.pacts[i];if(p.Active){try{Check(p,now);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 契约检查出错："+e,8814555+p.id);}}}
   if(!c.brokersRobbed && PactEvents.Brokers!=null && PactEvents.Brokers.HostileTo(Faction.OfPlayer)){c.brokersRobbed=true;foreach(var p in c.Active.Where(x=>x.patron.Hells).ToList())AddDebt(p,2,"抢劫了九狱掮客的商队");}
   if(c.pacts.Count>40)c.pacts.RemoveAll(p=>p.ended && now-p.signTick>YearTicks*2);
  }
  public static void Check(PlanarPact p,int now){
   var s=p.signer;
   if(s==null || s.Destroyed && !s.Dead){End(p,"签约人不在了",false);return;}
   if(s.Dead){End(p,s.LabelShort+"死了",true);return;}
   if(s.Faction==null || !s.Faction.IsPlayer)return; // 被俘、被拖走：契约暂停
   // 到期（一年契约）：迪斯帕特的到期代价先结清
   if(p.expireTick>=0 && now>=p.expireTick && !p.finalDemand){Expire(p,now);if(p.ended)return;}
   // 偿契
   if(p.demandClause<0 && p.nextDemandTick>=0 && now>=p.nextDemandTick)StartDemand(p,now,-1);
   if(p.demandClause>=0){
    var d=p.Demand;
    if(d==null){p.demandClause=-1;}
    else if(Satisfied(p,d)){Resolved(p,d);}
    else if(now>=p.demandDeadline){Failed(p,d);}
   }
   // 带周期的隐藏条款（灵魂税、利息、一年寿命）
   foreach(var cl in p.clauses){if(cl.consumed || cl.intervalDays<=0f || cl.nextTick<0 || now<cl.nextTick)continue;cl.nextTick=now+Ticks(p,cl.intervalDays);Periodic(p,cl);}
   if(p.nextMutationTick>=0 && now>=p.nextMutationTick){p.nextMutationTick=now+QuadrumTicks;if(Roll<p.patron.mutation)Mutate(p);}
   if(p.resaleTick>=0 && now>=p.resaleTick){p.resaleTick=-1;Resell(p);}
   if(p.Has("LongWinter"))LongWinter(p);
   p.Boon?.Worker?.Tick(p,now);
   if(p.debt>=2 && (p.lastCollectorTick<0 || now-p.lastCollectorTick>=CollectorDays*GenDate.TicksPerDay))SendCollectors(p);
   SyncSigner(p);
  }

  // ---------- 偿契 ----------
  public static void StartDemand(PlanarPact p,int now,int index){
   var list=p.clauses.Select((c,i)=>(c,i)).Where(x=>x.c.IsDemand && !x.c.consumed).ToList();
   ScheduleDemand(p,now);
   if(list.Count==0)return;
   var pick=index>=0?list.FirstOrDefault(x=>x.i==index):list.RandomElement();if(pick.c==null)return;
   var c=pick.c;
   if(AutoKinds.Contains(c.kind)){AutoExecute(p,c);return;}
   p.demandClause=pick.i;p.demandStart=now;p.demandDeadline=now+DemandDays*GenDate.TicksPerDay;p.demandPaid=false;
   PactLetters.Send("demand",p,p.signer,null,p.patron.label+"要求偿契",DemandText(p,c));
  }
  public static string DemandText(PlanarPact p,PactClauseState c){
   string what;
   switch(c.kind){
    case "SacrificePrisoner":what="在 "+DemandDays+" 天内处决一名囚犯（原版处决、深渊法阵炼魂都算）。";break;
    case "Silver":what="交出 "+c.count+" 银（信里的“交付”按钮直接从地图的白银里扣）。";break;
    case "Item":what="交出"+ItemLabel(c)+"（信里的“交付”按钮直接从地图上扣）。";break;
    case "Gathering":what="在 "+DemandDays+" 天内举办一次派对或婚礼。";break;
    default:what=c.label;break;
   }
   return p.signer.LabelShort+"的宗主"+p.patron.label+"派人捎来了话：偿契的时候到了。\n\n要求："+what+"\n\n期限 "+DemandDays+" 天。完不成，债务 +1（现在 "+p.debt+"）。";
  }
  public static string ItemLabel(PactClauseState c){switch(c.category){case "BookOrScroll":return "一本书或一张卷轴";case "Art":return "一件品质优秀以上的艺术品";case "SoulCoin":return " "+Mathf.Max(1,c.count)+" 枚灵魂币";}return "一件东西";}
  public static bool Satisfied(PlanarPact p,PactClauseState c){
   var comp=Comp;
   switch(c.kind){
    case "SacrificePrisoner":return comp!=null && comp.lastExecutionTick>=p.demandStart;
    case "Gathering":return comp!=null && comp.lastGatheringTick>=p.demandStart;
    case "Silver":case "Item":return p.demandPaid;
   }
   return false;
  }
  static void Resolved(PlanarPact p,PactClauseState c){
   p.demandClause=-1;p.demandPaid=false;
   Messages.Message(p.patron.label+"收下了"+p.signer.LabelShort+"的偿契。"+NextDemandText(p),p.signer,MessageTypeDefOf.NeutralEvent,false);
   if(p.finalDemand)End(p,"契约到期，代价已经付清",true);
  }
  static void Failed(PlanarPact p,PactClauseState c){
   p.demandClause=-1;p.demandPaid=false;
   if(p.finalDemand){
    if(p.Has("Collateral"))SeizeCollateral(p);
    AddDebt(p,1,"到期的代价没有付清");
    if(!p.ended){if(p.debt>=2 && p.lastCollectorTick<0)SendCollectors(p);End(p,"契约到期（代价没付清）",true);}
    return;
   }
   AddDebt(p,1,"没有按时偿契（"+c.label+"）");
  }
  // 交付白银 / 物品（信件按钮、补偿宗主都走这里）。
  public static string CannotDeliver(PlanarPact p,PactClauseState c,int times=1){
   var map=HomeOf(p);if(map==null)return "没有地图";
   switch(c.kind){
    case "Silver":return SilverOn(map)>=c.count*times?null:"地图上的白银不到 "+(c.count*times);
    case "Item":return ItemsFor(map,c).Count()>=(c.category=="SoulCoin"?0:times) && (c.category!="SoulCoin" || AbyssRitual.Count(map,AbyssItems.SoulCoin)>=Mathf.Max(1,c.count)*times)?null:"地图上没有"+ItemLabel(c)+(times>1?"（要 "+times+" 份）":"");
   }
   return "这一条不能交付";
  }
  public static bool Deliver(PlanarPact p,PactClauseState c,int times=1){
   if(CannotDeliver(p,c,times)!=null)return false;var map=HomeOf(p);
   switch(c.kind){
    case "Silver":TakeSilver(map,c.count*times);break;
    case "Item":
     if(c.category=="SoulCoin")AbyssRitual.TakeItems(map,AbyssItems.SoulCoin,Mathf.Max(1,c.count)*times);
     else foreach(var t in ItemsFor(map,c).Take(times).ToList())t.Destroy();
     break;
   }
   return true;
  }
  public static int SilverOn(Map map)=>map==null?0:map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t=>!t.IsForbidden(Faction.OfPlayer)).Sum(t=>t.stackCount);
  public static bool TakeSilver(Map map,int n){
   if(n<=0)return true;if(SilverOn(map)<n)return false;
   foreach(var t in map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t=>!t.IsForbidden(Faction.OfPlayer)).ToList()){if(n<=0)break;int k=Math.Min(n,t.stackCount);t.SplitOff(k).Destroy();n-=k;}
   return true;
  }
  public static IEnumerable<Thing> ItemsFor(Map map,PactClauseState c){
   if(map==null)yield break;
   switch(c.category){
    case "BookOrScroll":
     foreach(var t in map.listerThings.AllThings)if(t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.def!=ScrollDef && t.def.category==ThingCategory.Item && (t is Book || t.def.defName.Contains("Scroll")))yield return t;
     break;
    case "Art":
     foreach(var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Art))if(t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.def.category==ThingCategory.Item && t.TryGetQuality(out var q) && q>=QualityCategory.Excellent)yield return t;
     break;
   }
  }
  // 自动执行的代价（秘密、名字、一年寿命）：不产生债务。
  public static string AutoExecute(PlanarPact p,PactClauseState c){
   var map=HomeOf(p);string text=null;
   switch(c.kind){
    case "Secret":{
     var pool=map?.mapPawns.FreeColonistsSpawned.Where(x=>x.RaceProps.Humanlike && x.relations!=null && !Outsiders.Is(x)).InRandomOrder().Take(2).ToList();
     if(pool==null || pool.Count<2){text="殖民地里没有能拿来交换秘密的两个人。";break;}
     var op=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_SecretRevealedOpinion");var md=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_SecretRevealed");
     if(op!=null){pool[0].needs?.mood?.thoughts?.memories?.TryGainMemory(op,pool[1]);pool[1].needs?.mood?.thoughts?.memories?.TryGainMemory(op,pool[0]);}
     if(md!=null)foreach(var x in pool)x.needs?.mood?.thoughts?.memories?.TryGainMemory(md);
     text=p.patron.label+"把"+pool[0].LabelShort+"和"+pool[1].LabelShort+"之间的一件秘密抖了出来：两人互相意见 -20，各自心情 -5（5 天）。";break;
    }
    case "Name":{
     var who=map?.mapPawns.FreeColonistsSpawned.Where(x=>x.RaceProps.Humanlike && !Outsiders.Is(x)).RandomElementWithFallback();
     if(who==null){text="没有人可以交出名字。";break;}
     string old=who.LabelShort;
     try{who.Name=PawnBioAndNameGenerator.GeneratePawnName(who,NameStyle.Full,null,false,who.genes?.Xenotype);}catch(Exception e){Log.Warning("[DND边缘大陆] 改名失败："+e.Message);}
     var md=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_NameStolen");if(md!=null)who.needs?.mood?.thoughts?.memories?.TryGainMemory(md);
     Items.Give(who,"LP_NameStolenHediff");
     text=p.patron.label+"收走了"+old+"的名字。现在大家只能叫"+who.gender.GetPronoun()+"“"+who.LabelShort+"”：心情 -6、社交影响 -20%（10 天）。";break;
    }
    case "Year":{
     if(p.signer?.ageTracker!=null)p.signer.ageTracker.AgeBiologicalTicks+=GenDate.TicksPerYear;
     text=p.signer.LabelShort+"付出了一年寿命：生物年龄 +1 年。";break;
    }
   }
   if(text!=null)Find.LetterStack.ReceiveLetter(p.patron.label+"收取代价："+c.label,text,LetterDefOf.NegativeEvent,p.signer);
   return text;
  }
  static void Periodic(PlanarPact p,PactClauseState c){
   var map=HomeOf(p);
   switch(c.kind){
    case "SoulTax":if(!AbyssRitual.TakeItems(map,AbyssItems.SoulCoin,Mathf.Max(1,c.count)))AddDebt(p,1,"灵魂税交不出 "+c.count+" 枚灵魂币");else Messages.Message(p.patron.label+"收走了 "+c.count+" 枚灵魂币（灵魂税）。",p.signer,MessageTypeDefOf.NegativeEvent,false);break;
    case "Interest":if(!TakeSilver(map,c.count))AddDebt(p,1,"利息交不出 "+c.count+" 银");else Messages.Message(p.patron.label+"收走了 "+c.count+" 银利息。",p.signer,MessageTypeDefOf.NegativeEvent,false);break;
    case "Year":AutoExecute(p,c);break;
   }
  }
  // 到期：迪斯帕特的到期代价（白银）先发要求；其他直接结束。
  static void Expire(PlanarPact p,int now){
   p.finalDemand=true;
   int idx=p.clauses.FindIndex(c=>c.onExpiry && !c.consumed);
   if(idx<0){End(p,"契约到期",true);return;}
   var c=p.clauses[idx];var b=p.Boon;if(b!=null && b.clauseFactor!=1f && c.kind=="Silver")c.count=Mathf.RoundToInt(c.count*b.clauseFactor); // 铁城之金：到期白银 ×2
   p.demandClause=idx;p.demandStart=now;p.demandDeadline=now+DemandDays*GenDate.TicksPerDay;p.demandPaid=false;
   PactLetters.Send("demand",p,p.signer,null,p.patron.label+"：契约到期",p.signer.LabelShort+"和"+p.patron.label+"的契约到期了。\n\n到期的代价："+(c.kind=="Silver"?c.count+" 银":c.label)+"。期限 "+DemandDays+" 天；付清后契约结束，付不清"+(p.Has("Collateral")?"，殖民地价值最高的一栋建筑会被收走（抵押物）":"，债务 +1")+"。");
  }
  static void SeizeCollateral(PlanarPact p){
   var map=HomeOf(p);var b=map?.listerBuildings.allBuildingsColonist.Where(x=>x.def.building!=null && x.def.building.isNaturalRock==false && x.def.BuildableByPlayer).OrderByDescending(x=>x.MarketValue).FirstOrDefault();
   if(b==null)return;string label=b.LabelCap;var cell=b.Position;
   for(int i=0;i<5;i++)FleckMaker.ThrowSmoke(cell.ToVector3Shifted(),map,2f);
   b.Destroy(DestroyMode.Vanish);
   Find.LetterStack.ReceiveLetter("抵押物被收走",p.patron.label+"的收账人按“抵押物”条款收走了殖民地价值最高的建筑："+label+"。",LetterDefOf.NegativeEvent,new TargetInfo(cell,map));
  }
  static void Mutate(PlanarPact p){
   var vis=p.clauses.Select((c,i)=>(c,i)).Where(x=>x.c.IsDemand && !x.c.hidden && !x.c.consumed).ToList();if(vis.Count==0)return;
   var pool=p.patron.demands.Where(d=>!p.clauses.Any(c=>c.kind==d.kind && !c.consumed)).ToList();if(pool.Count==0)return;
   var old=vis.RandomElement();var nw=pool.RandomElement().Make(false);p.clauses[old.i]=nw;if(p.demandClause==old.i){p.demandClause=-1;p.demandPaid=false;}
   Find.LetterStack.ReceiveLetter("妖精的条款变了",p.patron.label+"改了主意：契约里的“"+old.c.label+"”变成了“"+nw.label+"”（"+nw.description+"）。妖精的契约就是这样。",LetterDefOf.NeutralEvent,p.signer);
  }
  static void Resell(PlanarPact p){
   var others=Patrons.Where(d=>d.Hells && d!=p.patron).ToList();if(others.Count==0)return;
   var nw=others.RandomElement();var old=p.patron;
   p.clauses.RemoveAll(c=>!c.hidden);foreach(var c in nw.visibleClauses)p.clauses.Add(c.Make(false));
   var r=p.Clause("Resale");if(r!=null){r.consumed=true;r.revealed=true;}
   p.patron=nw;if(nw.term==PactTerm.Year && p.expireTick<0)p.expireTick=Now+YearTicks;
   ScheduleDemand(p,Now);ApplyStatic(p);
   Find.LetterStack.ReceiveLetter("契约被转卖了",old.label+"把"+p.signer.LabelShort+"的契约转卖给了"+nw.label+"（隐藏条款“转卖”）。恩赐照旧，代价换成了新宗主的：\n\n"+ClauseText(p),LetterDefOf.NegativeEvent,p.signer);
  }
  static void LongWinter(PlanarPact p){
   var map=HomeOf(p);if(map==null)return;var season=GenLocalDate.Season(map);if(season!=Season.Winter && season!=Season.PermanentWinter)return;
   int year=GenLocalDate.Year(map);if(p.lastWinterYear==year)return;p.lastWinterYear=year;
   var cond=GameConditionMaker.MakeCondition(GameConditionDefOf.ColdSnap,GenDate.TicksPerDay*10);map.gameConditionManager.RegisterCondition(cond);
   var c=p.Clause("LongWinter");if(c!=null)c.revealed=true;
   Find.LetterStack.ReceiveLetter("长冬",p.patron.label+"的冰狱之风吹到了殖民地：寒潮 10 天（隐藏条款“长冬”）。",LetterDefOf.NegativeEvent,p.signer);
  }

  // ---------- 债务 ----------
  public static void AddDebt(PlanarPact p,int delta,string why){
   if(p==null || p.ended || delta==0)return;int old=p.debt;p.debt=Mathf.Clamp(p.debt+delta,0,MaxDebt);if(p.debt==old)return;
   SyncSigner(p);
   if(p.debt>old){
    string eff=p.debt==1?"违约的阴影：签约人心情 -6，恩赐给的能力冷却 ×2。":p.debt==2?"宗主派出了"+(p.patron.Fey?"狂猎":"讨债者")+"，之后每 "+CollectorDays+" 天一次，直到债务降下来。":"强制执行！";
    Find.LetterStack.ReceiveLetter("契约债务："+p.signer.LabelShort,p.signer.LabelShort+"欠"+p.patron.label+"的债务 "+old+" → "+p.debt+"（"+why+"）。\n\n"+eff+"\n\n还债：在契约讲台“补偿宗主”（交付一次当期要求的双倍，债务 -1）。",p.debt>=2?LetterDefOf.ThreatBig:LetterDefOf.NegativeEvent,p.signer);
    if(p.debt>=2 && old<2)SendCollectors(p);
    if(p.debt>=MaxDebt)Enforce(p);
   }else Messages.Message(p.signer.LabelShort+"欠"+p.patron.label+"的债务降到了 "+p.debt+"（"+why+"）。",p.signer,MessageTypeDefOf.PositiveEvent,false);
  }
  public static void SendCollectors(PlanarPact p){
   p.lastCollectorTick=Now;var map=HomeOf(p);if(map==null || !map.IsPlayerHome)return;
   if(p.patron.Fey)PactEvents.StartWildHunt(map,p.signer,p.id,StorytellerUtility.DefaultThreatPointsNow(map)*CollectorStrength,true);
   else PactEvents.SpawnCollectors(map,p,StorytellerUtility.DefaultThreatPointsNow(map)*CollectorStrength);
  }
  // 强制执行：签约人被拖进位面（巫妖：命匣被收走）。契约结束。
  public static void Enforce(PlanarPact p){
   var s=p.signer;if(s==null)return;
   if(p.lichMortgage){SeizePhylactery(s,p);End(p,"命匣被收走",false);return;}
   DragAway(s,p.patron.plane,p.patron.label);
   End(p,s.LabelShort+"被拖进了"+p.patron.PlaneLabel,false);
  }
  public static void DragAway(Pawn s,OutsiderPlane plane,string by){
   var c=Comp;if(c==null || s==null)return;var cell=s.PositionHeld;var map=s.MapHeld;
   if(s.Spawned){for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(s.DrawPos,s.Map,2f);s.DeSpawn();}
   if(s.GetCaravan() is Caravan car)car.RemovePawn(s);
   var f=plane==OutsiderPlane.Feywild?PactEvents.FeyHunt():PactEvents.Legion();if(f!=null)s.SetFaction(f);
   if(!Find.WorldPawns.Contains(s))Find.WorldPawns.PassToWorld(s,PawnDiscardDecideMode.KeepForever);
   c.taken.Add(new TakenPawn{pawn=s,plane=plane,tick=Now});
   Find.LetterStack.ReceiveLetter("强制执行："+s.LabelShort,by+"的债务到了 3：一团"+(plane==OutsiderPlane.Feywild?"冰冷的雾":"硫磺味的黑烟")+"裹住了"+s.LabelShort+"，把"+s.gender.GetPronoun()+"拖进了"+(plane==OutsiderPlane.Feywild?"妖精荒野":"九狱")+"。\n\n可以赎回："+(plane==OutsiderPlane.Feywild?"妖精集市的代价交易“赎回被拖走的人”":"九狱商队“赎人”（"+RansomSilver.ToString("0")+" 银）")+"。赎回后在地图边缘出现，深渊腐化 +20%。",LetterDefOf.Death,map==null?LookTargets.Invalid:new LookTargets(new TargetInfo(cell,map)));
  }
  public static void SeizePhylactery(Pawn lich,PlanarPact p){
   var box=Necro.Lich(lich)?.box;var c=Comp;if(box==null || c==null || box.Destroyed){Items.Give(lich,"LP_PhylacteryLost");return;}
   var map=box.MapHeld;
   if(box.Spawned)box.DeSpawn(DestroyMode.Vanish);else box.holdingOwner?.Remove(box);
   c.seized.TryAdd(box,false);c.seizedOwners.Add(lich);
   Items.Give(lich,"LP_PhylacteryLost");
   if(map!=null)foreach(var altar in map.listerThings.ThingsOfDef(Necro.Def<ThingDef>("Altar")).OfType<Building_Altar>())if(altar.selected==box)altar.selected=null;
   Find.LetterStack.ReceiveLetter("命匣被收走",p.patron.label+"的收账人取走了"+lich.LabelShort+"的命匣（命匣抵押）。"+lich.LabelShort+"的力量还在，但死后再也不会复活。\n\n可以从九狱商队赎回（"+RansomSilver.ToString("0")+" 银）。",LetterDefOf.NegativeEvent,lich);
  }
  public static bool RansomPhylactery(Pawn lich,Map map,bool pay){
   var c=Comp;if(c==null)return false;int i=c.seizedOwners.IndexOf(lich);if(i<0)return false;
   if(pay && !TakeSilver(map,(int)RansomSilver))return false;
   var box=c.seized.Count>i?c.seized[i]:c.seized.FirstOrDefault();if(box==null)return false;
   c.seized.Remove(box);c.seizedOwners.RemoveAt(i);
   GenPlace.TryPlaceThing(box,DropCellFinder.TradeDropSpot(map),map,ThingPlaceMode.Near);
   Items.Remove(lich,"LP_PhylacteryLost");if(box is Phylactery ph)ph.Register();
   Find.LetterStack.ReceiveLetter("赎回命匣",lich.LabelShort+"的命匣回来了。",LetterDefOf.PositiveEvent,box);
   return true;
  }
  public static Pawn Ransom(TakenPawn t,Map map,bool pay){
   var c=Comp;if(c==null || t?.pawn==null || map==null)return null;
   if(pay && !TakeSilver(map,(int)RansomSilver))return null;
   var p=t.pawn;c.taken.Remove(t);
   if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);
   if(p.Dead){Find.LetterStack.ReceiveLetter("赎回失败",p.LabelShort+"已经死在了另一个位面。",LetterDefOf.Death);return null;}
   p.SetFaction(Faction.OfPlayer);
   if(!CellFinder.TryFindRandomEdgeCellWith(x=>x.Standable(map) && !x.Fogged(map) && map.reachability.CanReachColony(x),map,CellFinder.EdgeRoadChance_Neutral,out var cell))cell=CellFinder.RandomEdgeCell(map);
   GenSpawn.Spawn(p,cell,map);
   AbyssTaint.Add(p,RansomTaint,"从异界被赎回");
   Find.LetterStack.ReceiveLetter("赎回："+p.LabelShort,p.LabelShort+"从"+(t.plane==OutsiderPlane.Feywild?"妖精荒野":"九狱")+"回来了，出现在地图边缘。那边发生的事"+p.gender.GetPronoun()+"不愿多说（深渊腐化 +20%）。",LetterDefOf.PositiveEvent,p);
   return p;
  }

  // ---------- 还债 ----------
  public static PactClauseState CompensationClause(PlanarPact p)=>p.Demand??p.clauses.FirstOrDefault(c=>c.IsDemand && !c.consumed && (c.kind=="Silver" || c.kind=="Item"))??p.clauses.FirstOrDefault(c=>c.IsDemand && !c.consumed);
  public static string CannotCompensate(PlanarPact p){
   if(p.debt<=0)return "没有欠债";var c=CompensationClause(p);if(c==null)return "没有可以补偿的要求";
   switch(c.kind){
    case "Silver":case "Item":return CannotDeliver(p,c,2);
    case "SacrificePrisoner":return AbyssRitual.Prisoners(HomeOf(p)).Count()>=2?null:"需要地图上有 2 名囚犯";
    case "Gathering":return SilverOn(HomeOf(p))>=1000?null:"派对没法“双倍”：改交 1000 银，地图上的白银不够";
   }
   return AbyssRitual.Count(HomeOf(p),AbyssItems.SoulCoin)>=3?null:"自动执行的要求补偿改交 3 枚灵魂币：地图上不够";
  }
  public static bool Compensate(PlanarPact p){
   if(CannotCompensate(p)!=null)return false;var c=CompensationClause(p);var map=HomeOf(p);
   switch(c.kind){
    case "Silver":case "Item":Deliver(p,c,2);break;
    case "SacrificePrisoner":foreach(var pr in AbyssRitual.Prisoners(map).Take(2).ToList())ExecutionUtility.DoExecutionByCut(p.signer,pr,8,true);break;
    case "Gathering":TakeSilver(map,1000);break;
    default:AbyssRitual.TakeItems(map,AbyssItems.SoulCoin,3);break;
   }
   AddDebt(p,-1,"在契约讲台补偿了宗主");return true;
  }

  // ---------- 解约 / 到期 ----------
  public static void End(PlanarPact p,string why,bool letter){
   if(p==null || p.ended)return;p.ended=true;p.endReason=why;p.demandClause=-1;
   var s=p.signer;
   try{p.Boon?.Worker?.OnEnd(p);}catch(Exception e){Log.Error("[DND边缘大陆] 移除恩赐出错："+e);}
   if(s!=null){
    if(s.abilities!=null && BlastDef!=null && s.abilities.GetAbility(BlastDef)!=null)s.abilities.RemoveAbility(BlastDef);
    RemoveStatic(s);
    if(p.soulGiven && !s.Dead)Items.Remove(s,"LP_SoulMortgaged");
   }
   SyncSigner(p);Refresh(Comp);
   if(letter && s!=null)Find.LetterStack.ReceiveLetter("契约结束："+p.patron.label,s.LabelShort+"和"+p.patron.label+"的契约结束了（"+why+"）。恩赐、魔能爆和条款都随之消失。",LetterDefOf.NeutralEvent,s.Dead?LookTargets.Invalid:new LookTargets(s));
  }
  public static void Unbind(PlanarPact p,string how){End(p,how,true);}

  // ---------- 魔能爆 ----------
  public static int Beams(Pawn p){int l=Mathf.Max(1,Legendary.LevelOf(p));return l>=17?4:l>=11?3:l>=5?2:1;}
  public const float BlastDamage=12f,BlastPen=.3f,HellIgnite=.2f,MarkIgnite=.4f,FeyStun=.15f;public const int FeyStunTicks=120;
  static DamageDef eldritch;public static DamageDef Eldritch=>eldritch??(eldritch=DefDatabase<DamageDef>.GetNamedSilentFail("LP_Eldritch")??DamageDefOf.Blunt);
  public static int Blast(Pawn caster,Pawn target){
   if(caster?.Map==null || target==null)return 0;var map=caster.Map;var p=Of(caster);int n=Beams(caster),hits=0;var t=target;
   for(int i=0;i<n;i++){
    if(t==null || t.Dead || t.Downed || !t.Spawned){t=GenRadial.RadialDistinctThingsAround(target.PositionHeld,map,6f,true).OfType<Pawn>().Where(x=>x!=caster && !x.Dead && !x.Downed && x.HostileTo(caster)).OrderBy(x=>x.Position.DistanceToSquared(target.PositionHeld)).FirstOrDefault();if(t==null)break;}
    var a=caster.DrawPos;var b=t.DrawPos;for(int k=1;k<=4;k++)FleckMaker.ThrowLightningGlow(a+(b-a)*(k/5f),map,.6f);
    FleckMaker.Static(t.Position,map,FleckDefOf.PsycastAreaEffect,.8f);
    t.TakeDamage(new DamageInfo(Eldritch,BlastDamage,BlastPen,-1f,caster,null,null,DamageInfo.SourceCategory.ThingOrUnknown,t));hits++;
    if(p!=null && !t.Dead){
     if(p.patron.Hells && Roll<(Items.Has(caster,"LP_Boon_InfernalMark")?MarkIgnite:HellIgnite))FireUtility.TryAttachFire(t,.3f,caster);
     if(p.patron.Fey && Roll<FeyStun)t.stances?.stunner?.StunFor(FeyStunTicks,caster,false,true,false);
    }
   }
   return hits;
  }
 }

 // ---------- 签约人状态 ----------
 public class Hediff_PactSigner : HediffWithComps {
  public int pactId=-1;
  public PlanarPact PlanarPact=>Pacts.ById(pactId);
  public override bool ShouldRemove=>PlanarPact==null || PlanarPact.ended;
  public override string LabelInBrackets{get{var p=PlanarPact;if(p==null)return null;return p.patron.label+(p.debt>0?"，债务 "+p.debt:"");}}
  public override string TipStringExtra{get{
   var p=PlanarPact;if(p==null)return null;var b=p.Boon;
   return "宗主："+p.patron.label+"（"+p.patron.PlaneLabel+"，"+(p.lichMortgage?"命匣抵押":p.patron.TermLabel)+"）\n恩赐："+b?.label+"\n"+Pacts.ClauseText(p)+"\n"+Pacts.NextDemandText(p)+(p.Demand!=null?"\n当前要求："+p.Demand.label+"（还剩 "+Mathf.Max(0,p.demandDeadline-Pacts.Now).ToStringTicksToPeriod()+"）":"");
  }}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref pactId,"lpPactId",-1);}
 }
 public class ThoughtWorker_PactDebt : ThoughtWorker {protected override ThoughtState CurrentStateInternal(Pawn p){if(!Pacts.Any)return false;var x=Pacts.Of(p);return x!=null && x.debt>=1;}}

 // ---------- 契约书 ----------
 public class CompProperties_PactScroll : CompProperties {public CompProperties_PactScroll(){compClass=typeof(CompPactScroll);}}
 public class CompPactScroll : ThingComp {
  public LP_PatronDef patron;public List<PactClauseState> draft;public List<string> offered;public int reviews;public bool negotiated;
  public void SetPatron(LP_PatronDef d){patron=d;draft=null;offered=null;reviews=0;negotiated=false;}
  void Ensure(){if(patron==null)patron=PactEvents.RandomPatron(null);}
  public List<PactClauseState> Draft(){
   Ensure();if(draft!=null)return draft;draft=new List<PactClauseState>();if(patron==null)return draft;
   foreach(var c in patron.visibleClauses)draft.Add(c.Make(false));
   foreach(var c in patron.hiddenClausePool.InRandomOrder().Take(patron.hiddenCount))draft.Add(c.Make(true));
   return draft;
  }
  public IEnumerable<LP_PactBoon> Offered(){
   Ensure();if(patron==null)yield break;
   if(offered==null)offered=patron.boons.Count<=patron.boonChoices?patron.boons.Select(b=>b.key).ToList():patron.boons.InRandomOrder().Take(patron.boonChoices).Select(b=>b.key).ToList();
   foreach(var k in offered){var b=patron.Boon(k);if(b!=null)yield return b;}
  }
  public override string TransformLabel(string label){Ensure();return patron==null?label:label+"："+patron.label;}
  public override bool AllowStackWith(Thing other)=>false;
  public override string CompInspectStringExtra(){Ensure();return patron==null?null:patron.PlaneLabel+"宗主"+patron.label+"的契约书（期限："+patron.TermLabel+"）。拿到契约讲台上读 4 小时才能看清条款。";}
  public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn){
   foreach(var o in base.CompFloatMenuOptions(selPawn))yield return o;
   if(selPawn?.Faction?.IsPlayer!=true || parent.MapHeld==null)yield break;Ensure();
   var job=DefDatabase<JobDef>.GetNamedSilentFail("LP_ReadPact");if(job==null)yield break;
   var lec=Pacts.LecternDef==null?null:parent.MapHeld.listerThings.ThingsOfDef(Pacts.LecternDef).Where(l=>l.Faction==Faction.OfPlayer && selPawn.CanReserveAndReach(l,PathEndMode.InteractionCell,Danger.Some)).OrderBy(l=>l.Position.DistanceToSquared(selPawn.Position)).FirstOrDefault();
   if(lec==null){yield return new FloatMenuOption("在契约讲台阅读（地图上没有能用的契约讲台）",null);yield break;}
   var why=Pacts.CannotSign(selPawn,patron);
   if(why!=null && (Pacts.Of(selPawn)!=null || Outsiders.Is(selPawn))){yield return new FloatMenuOption("在契约讲台阅读（"+why+"）",null);yield break;}
   yield return new FloatMenuOption("在契约讲台阅读"+patron?.label+"的契约书（4 小时）",()=>{var j=JobMaker.MakeJob(job,parent,lec);j.count=1;selPawn.jobs.TryTakeOrderedJob(j,JobTag.Misc);});
  }
  public override void PostExposeData(){
   base.PostExposeData();Scribe_Defs.Look(ref patron,"lpPatron");Scribe_Collections.Look(ref draft,"lpDraft",LookMode.Deep);Scribe_Collections.Look(ref offered,"lpOffered",LookMode.Value);
   Scribe_Values.Look(ref reviews,"lpReviews");Scribe_Values.Look(ref negotiated,"lpNegotiated");
  }
 }
 public class JobDriver_ReadPact : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed) && pawn.Reserve(job.targetB,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDestroyedOrNull(TargetIndex.A);this.FailOnDespawnedOrNull(TargetIndex.B);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
   yield return Toils_Haul.StartCarryThing(TargetIndex.A);
   yield return Toils_Goto.GotoThing(TargetIndex.B,PathEndMode.InteractionCell);
   var read=Toils_General.Wait(Pacts.ReadTicks).WithProgressBarToilDelay(TargetIndex.B);
   read.handlingFacing=true;read.tickIntervalAction=d=>pawn.rotationTracker.FaceTarget(TargetB);
   yield return read;
   yield return Toils_General.Do(()=>{var s=pawn.carryTracker.CarriedThing;if(s!=null)pawn.carryTracker.TryDropCarriedThing(pawn.Position,ThingPlaceMode.Near,out s);Pacts.OnRead(pawn,s??TargetA.Thing);});
  }
 }

 // ---------- 契约讲台 ----------
 public class Building_PactLectern : Building {
  public override string GetInspectString(){
   var s=base.GetInspectString();var lines=new List<string>();if(!s.NullOrEmpty())lines.Add(s);
   var c=GameComponent_Pacts.Get;
   if(c!=null)lines.Add("殖民地的契约："+Pacts.ActiveCount()+" / "+Pacts.Cap());
   if(c!=null)foreach(var p in c.Active)lines.Add(p.signer.LabelShort+"："+p.Label+(p.debt>0?"（债务 "+p.debt+"）":""));
   if(c!=null && c.taken.Count>0)lines.Add("被拖走的人："+string.Join("、",c.taken.Select(t=>t.pawn.LabelShort)));
   return string.Join("\n",lines);
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(Faction!=Faction.OfPlayer)yield break;var c=GameComponent_Pacts.Get;if(c==null)yield break;
   foreach(var p in c.Active.Where(x=>x.debt>0).ToList()){
    var pp=p;var cl=Pacts.CompensationClause(p);
    var cmd=new Command_Action{defaultLabel="补偿宗主："+p.signer.LabelShort,defaultDesc="交付一次当期要求的双倍，"+p.signer.LabelShort+"欠"+p.patron.label+"的债务 -1（现在 "+p.debt+"）。\n\n要交的："+(cl==null?"—":cl.kind=="Silver"?(cl.count*2)+" 银":cl.kind=="Item"?"两份"+Pacts.ItemLabel(cl):cl.kind=="SacrificePrisoner"?"处决 2 名囚犯（签约人亲手）":cl.kind=="Gathering"?"1000 银":"3 枚灵魂币"),icon=p.patron.Icon,action=()=>{if(!Pacts.Compensate(pp))Messages.Message("补偿不了："+Pacts.CannotCompensate(pp),MessageTypeDefOf.RejectInput,false);}};
    var why=Pacts.CannotCompensate(p);if(why!=null)cmd.Disable(why);yield return cmd;
   }
   foreach(var p in c.Active.ToList()){
    var sc=AgentScroll(Map,p.patron);if(sc==null)continue;var pp=p;
    yield return new Command_Action{defaultLabel="用代理人的真名解约："+p.signer.LabelShort,defaultDesc="念出"+p.patron.label+"代理人的真名，当场撕毁"+p.signer.LabelShort+"的契约：没有任何代价。用掉这张真名卷。",icon=ContentFinder<Texture2D>.Get("Items/Abyss/TrueNameScroll",false)??BaseContent.BadTex,action=()=>{if(!sc.Destroyed)sc.Destroy();Pacts.Unbind(pp,"用"+pp.patron.label+"代理人的真名卷撕毁了契约");}};
   }
  }
  public static Thing AgentScroll(Map map,LP_PatronDef d)=>TrueNames.On(map).FirstOrDefault(x=>x.kind==d.defName)?.parent;
 }

 // ---------- 魔能爆（原版能力） ----------
 public class CompProperties_EldritchBlast : CompProperties_AbilityEffect {public CompProperties_EldritchBlast(){compClass=typeof(CompAbilityEffect_EldritchBlast);}}
 public class CompAbilityEffect_EldritchBlast : CompAbilityEffect {
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){base.Apply(target,dest);if(target.Pawn!=null)Pacts.Blast(parent.pawn,target.Pawn);}
  public override bool AICanTargetNow(LocalTargetInfo target){var t=target.Pawn;return t!=null && !t.Dead && !t.Downed && t.HostileTo(parent.pawn);}
  public override string ExtraLabelMouseAttachment(LocalTargetInfo target)=>"魔能爆 ×"+Pacts.Beams(parent.pawn);
 }

 // ---------- 信件（全部用选择信件） ----------
 public static class PactLetters {
  public static ChoiceLetter_Pact Send(string kind,PlanarPact p,Pawn pawn,Thing thing,string label,string text,int extra=-1){
   var l=(ChoiceLetter_Pact)LetterMaker.MakeLetter(Pacts.Letter);l.kind=kind;l.pactId=p?.id??-1;l.pawn=pawn;l.thing=thing;l.extra=extra;
   l.Label=label;l.Text=text;l.lookTargets=thing!=null && thing.Spawned?new LookTargets(thing):pawn!=null && pawn.Spawned?new LookTargets(pawn):LookTargets.Invalid;
   Find.LetterStack.ReceiveLetter(l);return l;
  }
 }
 public class ChoiceLetter_Pact : ChoiceLetter {
  public string kind;public int pactId=-1,extra=-1;public Pawn pawn;public Thing thing;
  PlanarPact PlanarPact=>Pacts.ById(pactId);
  DiaOption Opt(string text,Action act,string disabled=null){var o=new DiaOption(text){action=()=>{Find.LetterStack.RemoveLetter(this);act?.Invoke();},resolveTree=true};if(disabled!=null)o.Disable(disabled);return o;}
  public override IEnumerable<DiaOption> Choices{get{
   if(ArchivedOnly){yield return Option_Close;yield break;}
   switch(kind){
    case "offer":{
     var s=thing?.TryGetComp<CompPactScroll>();if(s==null || thing.Destroyed || pawn==null || pawn.Dead){yield return Option_Close;yield break;}
     var why=Pacts.CannotSign(pawn,s.patron);
     foreach(var b in s.Offered().ToList()){var bb=b;yield return Opt("签下：恩赐“"+b.label+"”",()=>Pacts.Sign(pawn,s,bb.key),why);}
     if(Pacts.HiddenClauses && s.reviews<Pacts.MaxReviews && s.Draft().Any(c=>c.hidden && !c.revealed)){
      var law=s.reviews==0?null:Pacts.Lawyer(pawn.MapHeld,pawn);
      yield return Opt(s.reviews==0?"审阅（"+pawn.LabelShort+"智力豁免，难度 "+Pacts.ReviewDC(pawn,s.patron)+"，"+Legendary.SaveChanceText(pawn,Adventurer.Int,Pacts.ReviewDC(pawn,s.patron))+"）":"请律师再审阅一遍（"+law?.LabelShort+"，加熟练加值"+(law!=null?"，"+Legendary.SaveChanceText(law,Adventurer.Int,Pacts.ReviewDC(pawn,s.patron)-Legendary.Proficiency(Legendary.LevelOf(law))):"")+"）",()=>{bool ok=Pacts.Review(pawn,s,law);Resend(s,(ok?"审阅成功：揭开了一条隐藏条款。":"审阅失败：条款的字迹在眼前游移，什么也没看出来。")+"\n\n");},s.reviews>0 && law==null?"没有别的殖民者能当律师":null);
     }
     if(s.patron.negotiable && !s.negotiated)yield return Opt("谈判（"+pawn.LabelShort+"魅力豁免，难度 "+Pacts.NegotiateDC+"，"+Legendary.SaveChanceText(pawn,Adventurer.Cha,Pacts.NegotiateDC)+"）",()=>{bool ok=Pacts.Negotiate(pawn,s);if(ok)PactLetters.Send("strike",null,pawn,thing,"谈判成功","魔鬼律师皱着眉同意删掉一条。删哪一条？");else Resend(s,"谈判失败：对方冷笑着加了一条代价。\n\n");});
     yield return Opt("拒绝（书会烧掉，"+pawn.LabelShort+"心情 -3）",()=>Pacts.Refuse(pawn,s));
     yield return Option_Postpone;break;
    }
    case "strike":{
     var s=thing?.TryGetComp<CompPactScroll>();if(s==null || thing.Destroyed){yield return Option_Close;yield break;}
     var d=s.Draft();for(int i=0;i<d.Count;i++){if(d[i].hidden)continue;int ii=i;yield return Opt("删掉："+d[i].label,()=>{Pacts.Strike(s,ii);Resend(s,"删掉了一条代价。\n\n");});}
     break;
    }
    case "demand":{
     var p=PlanarPact;var c=p?.Demand;if(p==null || p.ended || c==null || (c.kind!="Silver" && c.kind!="Item")){yield return Option_Close;yield break;}
     yield return Opt("交付",()=>{if(Pacts.Deliver(p,c)){p.demandPaid=true;Messages.Message("交付了偿契的东西。",MessageTypeDefOf.NeutralEvent,false);}},Pacts.CannotDeliver(p,c));
     yield return Option_Postpone;break;
    }
    case "hags":{
     var p=PlanarPact;if(p==null || p.ended || pawn==null || pawn.Dead){yield return Option_Close;yield break;}
     yield return Opt("付一年寿命",()=>{pawn.ageTracker.AgeBiologicalTicks+=GenDate.TicksPerYear;var c=p.Clause("YearOrEye");if(c!=null)c.consumed=true;});
     var eye=PactBoons.Eye(pawn);
     yield return Opt("付一只眼睛",()=>{var e=PactBoons.Eye(pawn);if(e!=null)pawn.health.AddHediff(HediffDefOf.MissingBodyPart,e);var c=p.Clause("YearOrEye");if(c!=null)c.consumed=true;},eye==null?"没有眼睛可以付":null);
     break;
    }
    case "guest":{
     var p=PlanarPact;if(p==null || p.ended){yield return Option_Close;yield break;}
     yield return Opt("萨特",()=>PactBoons.SummerGuest(p,"LP_FY_Satyr"));
     yield return Opt("林仙",()=>PactBoons.SummerGuest(p,"LP_FY_Dryad"));
     break;
    }
    case "unbind":{
     var p=PlanarPact;var c=GameComponent_Pacts.Get;if(p==null || c==null){yield return Option_Close;yield break;}
     foreach(var o in c.Active.Where(x=>x!=p).ToList()){var oo=o;yield return Opt("解开 "+o.signer.LabelShort+" 的契约（"+o.patron.label+"）",()=>{Pacts.Unbind(oo,"格拉西娅的恩赐“解开契约”");p.periodFactor*=.75f;});}
     yield return Option_Postpone;break;
    }
    case "renegotiate":{
     if(pawn==null || pawn.Dead || !Outsiders.Bound(pawn)){yield return Option_Close;yield break;}
     var map=pawn.MapHeld;
     yield return Opt("给它 300 银",()=>PactRaces.Renegotiated(pawn,"300 银"),Pacts.SilverOn(map)>=300?null:"白银不够");
     yield return Opt("给它 2 枚灵魂币",()=>PactRaces.Renegotiated(pawn,"2 枚灵魂币"),AbyssRitual.Count(map,AbyssItems.SoulCoin)>=2?null:"灵魂币不够");
     yield return Opt("让它离开",()=>PactRaces.LetLeave(pawn));
     break;
    }
    default:foreach(var o in PactEvents.LetterChoices(this))yield return o;break;
   }
  }}
  void Resend(CompPactScroll s,string prefix){PactLetters.Send("offer",null,pawn,thing,"契约书："+s.patron.label,prefix+Pacts.OfferText(pawn,s));}
  public DiaOption Choice(string text,Action act,string disabled=null)=>Opt(text,act,disabled);
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref kind,"lpKind");Scribe_Values.Look(ref pactId,"lpPactId",-1);Scribe_Values.Look(ref extra,"lpExtra",-1);Scribe_References.Look(ref pawn,"lpPawn");Scribe_References.Look(ref thing,"lpThing");}
 }

 // ---------- 设置（“异界契约”分组：画在“深渊”分组下面，见 AbyssSettings.Draw 末尾） ----------
 public partial class Settings {
  public bool pactEnabled=true,pactHiddenClauses=true,pactCelestial=true;public int pactDemandPercent=100,pactCollectorPercent=100,pactChangelingPercent=3;
  void ExposePact(){
   Scribe_Values.Look(ref pactEnabled,"pactEnabled",true);Scribe_Values.Look(ref pactHiddenClauses,"pactHiddenClauses",true);Scribe_Values.Look(ref pactCelestial,"pactCelestial",true);
   Scribe_Values.Look(ref pactDemandPercent,"pactDemandPercent",100);Scribe_Values.Look(ref pactCollectorPercent,"pactCollectorPercent",100);Scribe_Values.Look(ref pactChangelingPercent,"pactChangelingPercent",3);
   pactDemandPercent=Math.Max(50,Math.Min(200,pactDemandPercent));pactCollectorPercent=Math.Max(50,Math.Min(200,pactCollectorPercent));pactChangelingPercent=Math.Max(0,Math.Min(10,pactChangelingPercent));
  }
 }
 public static class PactSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.GapLine();l.Label("异界契约");
   l.CheckboxLabeled("启用异界契约",ref s.pactEnabled,"（1.55.0）位面契约学研究、宗主的低语、九狱商队、妖精集市和随机狂猎。关掉后研究隐藏、这些事件不再随机触发；已经签下的契约照常运作。");
   l.CheckboxLabeled("隐藏条款",ref s.pactHiddenClauses,"关掉后契约书上的所有条款直接可见，信件里不再有“审阅”按钮。");
   l.Label("偿契频率："+s.pactDemandPercent+"%（越高越频繁）");s.pactDemandPercent=(int)l.Slider(s.pactDemandPercent,50,200);
   l.Label("讨债强度："+s.pactCollectorPercent+"%（讨债小队、狂猎的点数）");s.pactCollectorPercent=(int)l.Slider(s.pactCollectorPercent,50,200);
   l.Label("调换儿概率："+s.pactChangelingPercent+"%（每次出生）");s.pactChangelingPercent=(int)l.Slider(s.pactChangelingPercent,0,10);
   l.CheckboxLabeled("天界祈唤",ref s.pactCelestial,"善神做守护神时，圣坛上多一组“祈唤天界援军”（狄瓦援军、守护天使、行星使降临）。");
  }
 }

 // ---------- Harmony：事件钩子只记时间戳（第八节第 2 条） ----------
 [HarmonyPatch(typeof(ExecutionUtility),nameof(ExecutionUtility.DoExecutionByCut))]
 public static class PactExecutionHook {static void Postfix(){if(!Pacts.Any)return;var c=Pacts.Comp;if(c!=null)c.lastExecutionTick=Pacts.Now;}}
 [HarmonyPatch(typeof(GatheringWorker),nameof(GatheringWorker.TryExecute))]
 public static class PactGatheringHook {static void Postfix(bool __result,Map __0){if(!__result)return;PactRaces.Revelry(__0);if(!Pacts.Any)return;var c=Pacts.Comp;if(c!=null)c.lastGatheringTick=Pacts.Now;}}
 [HarmonyPatch(typeof(MarriageCeremonyUtility),nameof(MarriageCeremonyUtility.Married))]
 public static class PactWeddingHook {static void Postfix(Pawn __0){if(__0?.MapHeld!=null)PactRaces.Revelry(__0.MapHeld);if(!Pacts.Any)return;var c=Pacts.Comp;if(c!=null)c.lastGatheringTick=Pacts.Now;}}
 // 砍树：不伐之誓（每次债务 +1）；林仙的树被殖民者砍倒 → 狂猎。
 [HarmonyPatch(typeof(Plant),nameof(Plant.PlantCollected))]
 public static class PactTreeCutHook {
  static void Prefix(Plant __instance,Pawn __0,PlantDestructionMode __1){
   if(!Pacts.Any && PactRaces.dryads==0)return;
   if(__0==null || __0.Faction?.IsPlayer!=true || __instance?.def?.plant==null || !__instance.def.plant.IsTree || __1!=PlantDestructionMode.Chop && __1!=PlantDestructionMode.Cut)return;
   if(Pacts.Any)foreach(var p in Pacts.Comp.Active.Where(x=>x.Has("NoTreeCut")).ToList()){var c=p.Clause("NoTreeCut");c.revealed=true;Pacts.AddDebt(p,1,__0.LabelShort+"砍了一棵树（隐藏条款“不伐之誓”）");}
   if(PactRaces.dryads>0)PactRaces.TreeCut(__instance,__0);
  }
 }
 // 签约人 / 束缚者死亡、狂猎猎物、红袍法师掉契约书。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class PactKilledHook {
  static void Prefix(Pawn __instance,out bool __state){__state=!__instance.Dead;}
  static void Postfix(Pawn __instance,bool __state){
   if(!__state || !__instance.Dead)return;
   if(__instance.kindDef?.defName=="LP_FH_RedWizard" && Pacts.Enabled && Pacts.ScrollDef!=null && Pacts.Roll<PactEvents.WizardScrollChance && __instance.MapHeld!=null)GenPlace.TryPlaceThing(ThingMaker.MakeThing(Pacts.ScrollDef),__instance.PositionHeld,__instance.MapHeld,ThingPlaceMode.Near);
   if(!Pacts.Any && Pacts.Comp?.hunts.Count==0)return;
   var p=Pacts.Of(__instance);if(p!=null)Pacts.End(p,__instance.LabelShort+"死了",true);
   PactEvents.OnDeath(__instance);
  }
 }
 // 位面契约学：关掉异界契约时隐藏。
 [HarmonyPatch(typeof(ResearchProjectDef),nameof(ResearchProjectDef.IsHidden),MethodType.Getter)]
 public static class PactResearchHidden {static void Postfix(ResearchProjectDef __instance,ref bool __result){if(!__result && __instance==Pacts.Research && !Pacts.Enabled)__result=true;}}
}
