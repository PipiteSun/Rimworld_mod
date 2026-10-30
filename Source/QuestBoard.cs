using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.71.0 探索公告板（见 ArtSource/QuestBoard-1.71.0-report.md）：建筑 LP_QuestBoard 每天贴出一批分等阶的委托，接取后生成一个原版任务（任务分页可见），
 // 由 GameComponent_QuestBoard 每 250 刻推进：守卫家园（数小时后一队敌人来袭，打退即完成）、远征讨伐（世界地图上的怪物巢穴，消灭所有怪物）、清剿据点（敌对派系的前哨）。
 // 完成时任务以 Success 结束，Adventurer.QuestDone（AdventurerQuest 补丁）照“任务点数 × 0.5”给接取人经验、殖民地其他人 ×0.25；报酬用空投舱送到殖民地。
 // 做法参考 Isekai 公会公告板（每日刷新的委托 + 手工拼的任务），但不引用 Isekai，没装也照常工作。
 public static class QuestBoard {
  public const string Defense="defense",Hunt="hunt",Camp="camp";
  public static readonly string[] Kinds={Defense,Hunt,Camp};
  public static readonly string[] TierNames={"青铜","白银","黄金","秘银","传说"};
  public static readonly Color[] TierColors={new Color(.72f,.48f,.28f),new Color(.75f,.78f,.82f),new Color(.9f,.75f,.3f),new Color(.55f,.8f,.9f),new Color(.82f,.45f,.9f)};
  public static readonly float[] TierPoints={300f,700f,1400f,2500f,4000f};
  public static readonly int[] TierMinLevel={1,5,9,13,17};
  public static readonly int[] TierSilver={200,450,800,1400,2400};
  public const int MaxTier=5,OffersPerDay=6,MaxActive=3,RefreshTicks=GenDate.TicksPerDay,TickInterval=250;
  public const float HuntPoints=.8f,HuntSilver=1.2f,CampSilver=1.3f;
  public static int SiteMin(int tier)=>2+tier;
  public static int SiteMax(int tier)=>5+tier*2;
  public static int SiteDays(int tier)=>12+tier*2;

  public static bool Enabled=>LichMod.Settings?.questBoard??true;
  public static ThingDef BoardDef=>DefCache<ThingDef>.Get("LP_QuestBoard");
  public static SitePartDef LairDef=>DefDatabase<SitePartDef>.GetNamedSilentFail("LP_QB_MonsterLair");
  public static SitePartDef CampDef=>DefDatabase<SitePartDef>.GetNamedSilentFail("Outpost")??SitePartDefOf.BanditCamp;
  public static QuestScriptDef RootDef=>DefDatabase<QuestScriptDef>.GetNamedSilentFail("LP_QuestBoardContract");
  public static GameComponent_QuestBoard State=>GameComp<GameComponent_QuestBoard>.Get();
  public static int Now=>Find.TickManager.TicksGame;

  // 等阶门槛看殖民地最高冒险者等级（关掉冒险者等级时五阶全开）。
  public static int? TestLevel;
  public static int ColonyLevel=>TestLevel??AdventurerEnemies.ColonyMaxLevel();
  public static int UnlockedTier(){if(!Adventurer.Enabled)return MaxTier;int lv=ColonyLevel,t=1;while(t<MaxTier && lv>=TierMinLevel[t])t++;return t;}
  public static string TierLabel(int tier)=>TierNames[Mathf.Clamp(tier,1,MaxTier)-1];
  public static string KindLabel(string kind)=>kind==Defense?"守卫家园":kind==Hunt?"远征讨伐":"清剿据点";
  public static float XpFor(float points)=>points*Adventurer.QuestShare;

  public static Map Home=>Find.Maps.Where(m=>m.IsPlayerHome).OrderByDescending(m=>m.listerThings.ThingsOfDef(BoardDef).Any()).ThenByDescending(m=>m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();
  public static bool AnyBoard=>BoardDef!=null && Find.Maps.Any(m=>m.IsPlayerHome && m.listerThings.ThingsOfDef(BoardDef).Any(t=>t.Faction==Faction.OfPlayer));

  // ---------- 敌人 ----------
  public static bool RaidCapable(Faction f,float points)=>f!=null && !f.IsPlayer && !f.defeated && !f.Hidden && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction && f.def.pawnGroupMakers!=null
   && f.def.pawnGroupMakers.Any(g=>g.kindDef==PawnGroupKindDefOf.Combat) && f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)<=points;
  public static bool CampCapable(Faction f,float points)=>RaidCapable(f,points) && f.def.pawnGroupMakers.Any(g=>g.kindDef==PawnGroupKindDefOf.Settlement) && f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Settlement)<=points;
  public static Faction PickFaction(float points,bool camp)=>Find.FactionManager.AllFactionsListForReading.Where(f=>camp?CampCapable(f,points):RaidCapable(f,points)).RandomElementWithFallback();
  // 讨伐目标不要麻雀、松鼠：战斗力至少 MinBeastPower（点数 ÷ 8，30～180），至少两只；优先当地生物群系的野兽，没有再从所有能狂暴的野兽里挑，最后才退回原版的挑法。
  public static float MinBeastPower(float points)=>Mathf.Clamp(points/8f,30f,180f);
  public static PawnKindDef PickAnimal(float points,PlanetTile tile){
   float min=MinBeastPower(points);
   Func<PawnKindDef,bool> ok=x=>x.RaceProps!=null && x.RaceProps.Animal && x.canArriveManhunter && x.combatPower>=min && x.combatPower<=points/2f;
   var local=new List<PawnKindDef>();
   try{var biome=tile.Valid?Find.WorldGrid[tile]?.PrimaryBiome:null;if(biome!=null)local=biome.AllWildAnimals.Where(ok).ToList();}catch{}
   var pool=local.Count>0?local:DefDatabase<PawnKindDef>.AllDefs.Where(ok).ToList();
   if(pool.Count>0)return pool.RandomElementByWeight(x=>AggressiveAnimalIncidentUtility.AnimalWeight(x,points)+.01f);
   return AggressiveAnimalIncidentUtility.TryFindAggressiveAnimalKind(points,tile,out var k)?k:null;
  }

  // ---------- 报酬 ----------
  public static List<string> RollItems(int tier){
   var l=new List<string>();
   Action<string[]> add=pool=>{var ok=pool.Where(n=>Items.Thing(n)!=null).ToList();if(ok.Count>0)l.Add(ok.RandomElement());};
   int common=tier==1?1:tier==2?2:tier==3?1:tier==4?0:1,rare=tier==3?1:tier>=4?2:0,magic=tier>=4?1:0;
   for(int i=0;i<common;i++)add(IsekaiLoot.CommonLoot);
   for(int i=0;i<rare;i++)add(IsekaiLoot.RareLoot);
   for(int i=0;i<magic;i++)add(IsekaiLoot.MagicLoot);
   return l;
  }
  public static List<Thing> MakeReward(BoardContract c){
   var list=new List<Thing>();int silver=c.silver;
   while(silver>0){var s=ThingMaker.MakeThing(ThingDefOf.Silver);s.stackCount=Math.Min(silver,ThingDefOf.Silver.stackLimit);silver-=s.stackCount;list.Add(s);}
   foreach(var n in c.items){var d=Items.Thing(n);if(d==null)continue;var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityReward(),ArtGenerationContext.Outsider);list.Add(t);}
   return list;
  }
  public static string RewardText(BoardContract c){
   var parts=new List<string>{c.silver+" 银"};
   foreach(var n in c.items){var d=Items.Thing(n);if(d!=null)parts.Add(d.label);}
   return string.Join("、",parts);
  }

  // ---------- 委托 ----------
  public static BoardContract MakeOffer(string kind,int tier,Map home){
   var st=State;if(st==null || home==null)return null;
   var c=new BoardContract{id=st.nextId++,kind=kind,tier=tier,offerExpires=Now+RefreshTicks};
   float pts=TierPoints[tier-1];
   if(kind==Defense){c.points=pts;c.faction=PickFaction(pts,false);if(c.faction==null){var a=PickAnimal(pts,home.Tile);if(a==null)return null;c.animal=a.defName;}}
   else if(kind==Hunt){c.points=pts*HuntPoints;var a=PickAnimal(c.points,home.Tile);if(a==null)return null;c.animal=a.defName;}
   else{c.points=pts;c.faction=PickFaction(pts,true);if(c.faction==null)return null;}
   c.silver=Mathf.RoundToInt(TierSilver[tier-1]*(kind==Hunt?HuntSilver:kind==Camp?CampSilver:1f)/10f)*10;
   c.items=RollItems(tier);
   return c;
  }
  public static void Refresh(){
   var st=State;var home=Home;if(st==null)return;
   st.offers.Clear();st.nextRefresh=Now+RefreshTicks;
   if(!Enabled || home==null)return;
   int top=UnlockedTier();
   var pairs=new List<(string kind,int tier,float w)>();
   foreach(var k in Kinds)for(int t=1;t<=top;t++)pairs.Add((k,t,t==top?3f:t==top-1?2f:1f));
   int guard=0;
   while(st.offers.Count<OffersPerDay && pairs.Count>0 && guard++<40){
    var pick=pairs.RandomElementByWeight(p=>p.w);pairs.Remove(pick);
    var c=MakeOffer(pick.kind,pick.tier,home);if(c!=null)st.offers.Add(c);
   }
   // 每个已解锁的最高等阶至少有一份（有敌人可找时）。
   if(!st.offers.Any(o=>o.tier==top))foreach(var k in Kinds){var c=MakeOffer(k,top,home);if(c!=null){st.offers.Add(c);break;}}
   st.offers.SortBy(o=>o.tier,o=>Array.IndexOf(Kinds,o.kind));
  }
  public static void EnsureFresh(){var st=State;if(st!=null && (Now>=st.nextRefresh || st.nextRefresh==-1))Refresh();}

  public static string Title(BoardContract c){
   if(c.kind==Defense)return "守卫家园："+(c.faction!=null?c.faction.Name:AnimalLabel(c)+"兽群");
   if(c.kind==Hunt)return "远征讨伐："+AnimalLabel(c)+"群";
   return "清剿据点："+(c.faction?.Name??"敌对据点");
  }
  static string AnimalLabel(BoardContract c)=>DefDatabase<PawnKindDef>.GetNamedSilentFail(c.animal??"")?.label??"野兽";
  public static string Describe(BoardContract c){
   string body;
   if(c.kind==Defense)body="冒险者公会得到消息："+(c.faction!=null?c.faction.Name+"的一伙人":"一群狂暴的"+AnimalLabel(c))+"正朝殖民地逼近。接下委托后 6～12 小时内来袭，打退所有来犯者（杀死、打倒、俘虏或赶走）就算完成。";
   else if(c.kind==Hunt)body="荒野里出现了一群狂暴的"+AnimalLabel(c)+"，威胁着过往的商旅。接下委托后世界地图上会出现「怪物巢穴」（离殖民地 "+SiteMin(c.tier)+"～"+SiteMax(c.tier)+" 格），组织远征队前往，消灭所有怪物（杀死或打倒）。"+SiteDays(c.tier)+" 天内不去，巢穴就会散去，委托失败。";
   else body=(c.faction?.Name??"敌对派系")+"在附近建了一处前哨据点。接下委托后据点会出现在世界地图上（离殖民地 "+SiteMin(c.tier)+"～"+SiteMax(c.tier)+" 格），前往并消灭所有守军。"+SiteDays(c.tier)+" 天内不去，据点就会撤走，委托失败。";
   return body+"\n\n等阶："+TierLabel(c.tier)+"（推荐冒险者等级 "+TierMinLevel[c.tier-1]+"+）\n威胁点数："+Mathf.RoundToInt(c.points)+"\n报酬："+RewardText(c)+"\n冒险者经验：接取人约 "+Mathf.RoundToInt(XpFor(c.points)*Adventurer.XpScale)+" 点，殖民地其他人各约 "+Mathf.RoundToInt(XpFor(c.points)*Adventurer.QuestHomeShare*Adventurer.XpScale)+" 点。";
  }

  public static AcceptanceReport CanAccept(BoardContract c){
   var st=State;if(st==null || c==null)return false;
   if(!Enabled)return "探索公告板已在设置里关闭";
   if(!st.offers.Contains(c))return "委托已经下架";
   if(st.active.Count>=MaxActive)return "同时最多接 "+MaxActive+" 份委托";
   if(c.tier>UnlockedTier())return "需要殖民地有冒险者达到 "+TierMinLevel[c.tier-1]+" 级";
   if(Home==null)return "没有殖民地";
   return true;
  }
  public static Quest Accept(BoardContract c,Pawn by){
   var ok=CanAccept(c);if(!ok.Accepted){if(!ok.Reason.NullOrEmpty())Messages.Message(ok.Reason,MessageTypeDefOf.RejectInput,false);return null;}
   var st=State;var home=Home;
   if(c.kind!=Defense && !MakeSite(c,home)){Messages.Message("附近找不到合适的地点，换一份委托试试。",MessageTypeDefOf.RejectInput,false);return null;}
   if(c.kind==Defense)c.raidTick=Now+Mathf.RoundToInt(Rand.Range(.25f,.5f)*GenDate.TicksPerDay);
   else c.deadline=Now+SiteDays(c.tier)*GenDate.TicksPerDay;
   var q=Quest.MakeRaw();
   q.name="【"+TierLabel(c.tier)+"】"+Title(c);q.description=Describe(c);q.root=RootDef;
   q.challengeRating=Mathf.Clamp((c.tier+1)/2,1,3);q.points=c.points;q.appearanceTick=Now;
   q.AddPart(new QuestPart_QuestBoard{contractId=c.id});
   Find.QuestManager.Add(q);q.Accept(by);
   c.quest=q;st.offers.Remove(c);st.active.Add(c);
   string where=c.kind==Defense?"敌人将在 6～12 小时内到达。":"目标已经标在世界地图上，"+SiteDays(c.tier)+" 天内完成。";
   Find.LetterStack.ReceiveLetter("接下委托："+Title(c),(by!=null?by.LabelShort+"在探索公告板上揭下了这份委托。":"")+where+"\n\n"+Describe(c),c.kind==Defense?LetterDefOf.ThreatSmall:LetterDefOf.NeutralEvent,c.site!=null?new LookTargets(c.site):LookTargets.Invalid,null,q);
   return q;
  }
  static bool MakeSite(BoardContract c,Map home){
   if(!TileFinder.TryFindNewSiteTile(out PlanetTile tile,home.Tile,SiteMin(c.tier),SiteMax(c.tier)))return false;
   Site site;
   if(c.kind==Hunt){
    if(LairDef==null)return false;
    site=SiteMaker.MakeSite(LairDef,tile,null,true,c.points);
   }else{
    if(c.faction==null || !CampCapable(c.faction,c.points))c.faction=PickFaction(c.points,true);
    if(c.faction==null)return false;
    site=SiteMaker.MakeSite(CampDef,tile,c.faction,true,c.points);
   }
   if(site==null)return false;
   foreach(var p in site.parts){if(p.parms==null)p.parms=new SitePartParams();p.parms.threatPoints=c.points;if(c.kind==Hunt)p.parms.animalKind=DefDatabase<PawnKindDef>.GetNamedSilentFail(c.animal??"");}
   site.GetComponent<TimeoutComp>()?.StartTimeout(SiteDays(c.tier)*GenDate.TicksPerDay);
   Find.WorldObjects.Add(site);c.site=site;
   return true;
  }

  // ---------- 推进 ----------
  public static void Tick(BoardContract c){
   var q=c.quest;
   if(q==null || q.State!=QuestState.Ongoing){Drop(c);return;}
   if(c.kind==Defense){
    var home=Home;if(home==null){Fail(c,"殖民地已经不在了。");return;}
    if(!c.raidSent){if(Now>=c.raidTick)SendRaid(c,home);return;}
    if(c.raiders.All(Gone))Complete(c);
    return;
   }
   var site=c.site;
   if(site==null || site.Destroyed){Fail(c,c.kind==Hunt?"怪物巢穴已经散去。":"据点已经撤走了。");return;}
   if(!site.HasMap)return;
   var map=site.Map;
   if(c.mapTick==-1)c.mapTick=Now;
   if(Hostiles(map))c.sawEnemies=true;
   else if(c.sawEnemies || Now-c.mapTick>GenDate.TicksPerHour)Complete(c);
  }
  public static bool Gone(Pawn p)=>p==null || p.Dead || p.Downed || !p.Spawned || p.Faction==Faction.OfPlayer || p.IsPrisoner || !p.HostileTo(Faction.OfPlayer) || p.MapHeld==null || !p.MapHeld.IsPlayerHome;
  public static bool Hostiles(Map map)=>map.mapPawns.AllPawnsSpawned.Any(p=>!p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer) && !p.IsPrisoner && (p.RaceProps.Animal || p.RaceProps.Humanlike || p.RaceProps.IsMechanoid));

  public static List<Pawn> SendRaid(BoardContract c,Map home){
   c.raidSent=true;var pawns=new List<Pawn>();
   var f=c.faction!=null && RaidCapable(c.faction,c.points)?c.faction:(c.animal==null?PickFaction(c.points,false):null);
   if(!Events2.EdgeCell(home,out var entry)){Complete(c);return pawns;}
   if(f!=null){
    pawns=PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,tile=home.Tile,faction=f,points=c.points},false).ToList();
    foreach(var p in pawns)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,home,6),home);
    if(pawns.Count>0)LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,true,false,false,false,false,false),home,pawns);
   }else{
    var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail(c.animal??"")??PickAnimal(c.points,home.Tile);
    if(kind!=null)pawns=AggressiveAnimalIncidentUtility.GenerateAnimals(kind,home.Tile,c.points,0);
    foreach(var p in pawns){GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,home,8),home);p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true);}
   }
   c.raiders=pawns;
   if(pawns.Count==0){Complete(c);return pawns;}
   Find.LetterStack.ReceiveLetter("委托：敌人来袭","探索公告板上的委托「"+Title(c)+"」：敌人到了！打退所有来犯者就算完成。",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f,c.quest);
   return pawns;
  }

  public static void Complete(BoardContract c){
   var st=State;var home=Home;var q=c.quest;
   var reward=MakeReward(c);
   if(home!=null && reward.Count>0)DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(home),home,reward,110,false,false,true,false);
   c.done=true;st?.active.Remove(c);
   var by=q?.AccepterPawn;
   Find.LetterStack.ReceiveLetter("委托完成："+Title(c),"探索公告板上的【"+TierLabel(c.tier)+"】委托「"+Title(c)+"」完成了。\n\n报酬（空投送到殖民地）："+RewardText(c)+"\n冒险者经验："+(by!=null?by.LabelShort+"约 "+Mathf.RoundToInt(XpFor(c.points)*Adventurer.XpScale)+" 点，":"")+"殖民地其他人各约 "+Mathf.RoundToInt(XpFor(c.points)*Adventurer.QuestHomeShare*Adventurer.XpScale)+" 点。",LetterDefOf.PositiveEvent,home!=null?new LookTargets(new TargetInfo(DropCellFinder.TradeDropSpot(home),home)):LookTargets.Invalid,null,q);
   if(q!=null && q.State==QuestState.Ongoing)q.End(QuestEndOutcome.Success,false);
  }
  public static void Fail(BoardContract c,string why){
   var st=State;var q=c.quest;st?.active.Remove(c);c.done=true;
   if(c.site!=null && !c.site.Destroyed && !c.site.HasMap)c.site.Destroy();
   Find.LetterStack.ReceiveLetter("委托失败："+Title(c),why+"探索公告板上的委托「"+Title(c)+"」失败了。",LetterDefOf.NegativeEvent,LookTargets.Invalid,null,q);
   if(q!=null && q.State==QuestState.Ongoing)q.End(QuestEndOutcome.Fail,false);
  }
  // 任务被别的东西结束了（开发者工具等）：只收拾站点。
  static void Drop(BoardContract c){var st=State;st?.active.Remove(c);c.done=true;if(c.site!=null && !c.site.Destroyed && !c.site.HasMap)c.site.Destroy();}

  public static string Status(BoardContract c){
   if(c.kind==Defense){
    if(!c.raidSent)return "敌人约 "+Mathf.Max(1,Mathf.CeilToInt((c.raidTick-Now)/(float)GenDate.TicksPerHour))+" 小时后到达";
    return "敌人来袭中，还剩 "+c.raiders.Count(p=>!Gone(p))+" 名";
   }
   if(c.site==null || c.site.Destroyed)return "目标已经消失";
   if(c.site.HasMap)return "远征队已到达，还有敌人"+(c.sawEnemies?"":"（搜索中）");
   return "前往世界地图上的「"+c.site.Label+"」，剩余 "+((c.deadline-Now)/(float)GenDate.TicksPerDay).ToString("0.#")+" 天";
  }
 }

 // 一份委托（公告板上的或已接下的）。
 public class BoardContract : IExposable {
  public int id;public string kind;public int tier=1;public float points;public int silver;public List<string> items=new List<string>();
  public Faction faction;public string animal;public int offerExpires=-1;
  public Quest quest;public Site site;public List<Pawn> raiders=new List<Pawn>();
  public int raidTick=-1,deadline=-1,mapTick=-1;public bool raidSent,sawEnemies,done;
  public void ExposeData(){
   Scribe_Values.Look(ref id,"id");Scribe_Values.Look(ref kind,"kind");Scribe_Values.Look(ref tier,"tier",1);Scribe_Values.Look(ref points,"points");Scribe_Values.Look(ref silver,"silver");
   Scribe_Collections.Look(ref items,"items",LookMode.Value);Scribe_References.Look(ref faction,"faction");Scribe_Values.Look(ref animal,"animal");Scribe_Values.Look(ref offerExpires,"offerExpires",-1);
   Scribe_References.Look(ref quest,"quest");Scribe_References.Look(ref site,"site");Scribe_Collections.Look(ref raiders,"raiders",LookMode.Reference);
   Scribe_Values.Look(ref raidTick,"raidTick",-1);Scribe_Values.Look(ref deadline,"deadline",-1);Scribe_Values.Look(ref mapTick,"mapTick",-1);
   Scribe_Values.Look(ref raidSent,"raidSent");Scribe_Values.Look(ref sawEnemies,"sawEnemies");Scribe_Values.Look(ref done,"done");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(items==null)items=new List<string>();if(raiders==null)raiders=new List<Pawn>();raiders.RemoveAll(p=>p==null);}
  }
 }

 public class GameComponent_QuestBoard : GameComponent {
  public List<BoardContract> offers=new List<BoardContract>(),active=new List<BoardContract>();
  public int nextRefresh=-1,nextId=1;
  public GameComponent_QuestBoard(Game game){}
  public BoardContract ById(int id)=>active.FirstOrDefault(c=>c.id==id)??offers.FirstOrDefault(c=>c.id==id);
  public override void GameComponentTick(){
   if(Verse.Find.TickManager.TicksGame%QuestBoard.TickInterval!=0)return;
   if(QuestBoard.Enabled && QuestBoard.Now>=nextRefresh && QuestBoard.AnyBoard)QuestBoard.Refresh();
   for(int i=active.Count-1;i>=0;i--){if(i>=active.Count)continue;try{QuestBoard.Tick(active[i]);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 探索公告板委托出错："+e,0x51B0A+i);}}
  }
  public override void ExposeData(){
   Scribe_Collections.Look(ref offers,"offers",LookMode.Deep);Scribe_Collections.Look(ref active,"active",LookMode.Deep);
   Scribe_Values.Look(ref nextRefresh,"nextRefresh",-1);Scribe_Values.Look(ref nextId,"nextId",1);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(offers==null)offers=new List<BoardContract>();if(active==null)active=new List<BoardContract>();active.RemoveAll(c=>c==null || c.quest==null);offers.RemoveAll(c=>c==null);}
  }
 }

 // 任务分页里的“查看目标”：站点或来袭的敌人。
 public class QuestPart_QuestBoard : QuestPart {
  public int contractId;
  public override IEnumerable<GlobalTargetInfo> QuestLookTargets{get{
   var c=QuestBoard.State?.ById(contractId);if(c==null)yield break;
   if(c.site!=null && !c.site.Destroyed)yield return c.site;
   foreach(var p in c.raiders)if(p!=null && p.Spawned && !p.Dead)yield return p;
  }}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref contractId,"contractId");}
 }

 // 怪物巢穴：一群永久狂暴的野兽（野性等级照常由 BeastLevels 的 GenerateAnimals 补丁给）。
 public class GenStep_QuestBoardLair : GenStep {
  public override int SeedPart=>717100;
  public override void Generate(Map map,GenStepParams parms){
   try{
    float points=parms.sitePart?.parms?.threatPoints??300f;if(points<=0f)points=300f;
    var kind=parms.sitePart?.parms?.animalKind??QuestBoard.PickAnimal(points,map.Tile);if(kind==null)return;
    var pawns=AggressiveAnimalIncidentUtility.GenerateAnimals(kind,map.Tile,points,0);
    if(!CellFinder.TryFindRandomCellNear(map.Center,map,Mathf.Min(map.Size.x,map.Size.z)/4,c=>c.Standable(map) && !c.Roofed(map),out var center))center=CellFinder.RandomCell(map);
    foreach(var p in pawns){GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(center,map,8),map);p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true);}
   }catch(Exception e){Log.Error("[DND边缘大陆] 生成怪物巢穴出错："+e);}
  }
 }

 public class Building_QuestBoard : Building {
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(Faction!=Faction.OfPlayer)yield break;
   yield return new Command_Action{defaultLabel="查看委托",defaultDesc="打开探索公告板：查看今天的委托、接下委托、看已接委托的进度。",icon=def.uiIcon,action=()=>Verse.Find.WindowStack.Add(new Window_QuestBoard(this))};
  }
  public override string GetInspectString(){
   var s=base.GetInspectString();var st=QuestBoard.State;if(st==null)return s;
   if(!QuestBoard.Enabled)return (s.NullOrEmpty()?"":s+"\n")+"探索公告板已在设置里关闭";
   string mine="今日委托 "+st.offers.Count+" 份，已接 "+st.active.Count+"/"+QuestBoard.MaxActive+"，可接等阶：青铜～"+QuestBoard.TierLabel(QuestBoard.UnlockedTier());
   return s.NullOrEmpty()?mine:s+"\n"+mine;
  }
 }

 public class Window_QuestBoard : Window {
  readonly Building board;Vector2 scroll;
  public Window_QuestBoard(Building b){board=b;doCloseX=true;doCloseButton=false;absorbInputAroundWindow=false;forcePause=true;closeOnClickedOutside=true;}
  public override Vector2 InitialSize=>new Vector2(820f,640f);
  public override void PreOpen(){base.PreOpen();QuestBoard.EnsureFresh();}
  public override void DoWindowContents(Rect inRect){
   var st=QuestBoard.State;if(st==null)return;
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(0f,0f,inRect.width,34f),"探索公告板");Text.Font=GameFont.Small;
   int top=QuestBoard.UnlockedTier();
   string head=QuestBoard.Enabled?"殖民地最高冒险者等级 "+QuestBoard.ColonyLevel+"，可接等阶：青铜～"+QuestBoard.TierLabel(top)+(top<QuestBoard.MaxTier?"（"+QuestBoard.TierLabel(top+1)+"需要 "+QuestBoard.TierMinLevel[top]+" 级）":"")+"。委托每天刷新，约 "+Mathf.Max(1,Mathf.CeilToInt((st.nextRefresh-QuestBoard.Now)/(float)GenDate.TicksPerHour))+" 小时后换一批；同时最多接 "+QuestBoard.MaxActive+" 份。":"探索公告板已在设置（冒险者分组）里关闭。";
   GUI.color=Color.gray;Widgets.Label(new Rect(0f,36f,inRect.width,44f),head);GUI.color=Color.white;
   var outer=new Rect(0f,84f,inRect.width,inRect.height-84f);
   float rowH=96f;float h=30f+st.offers.Count*rowH+40f+Mathf.Max(1,st.active.Count)*56f+20f;
   var view=new Rect(0f,0f,outer.width-18f,h);
   Widgets.BeginScrollView(outer,ref scroll,view);
   float y=0f;
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(0f,y,view.width,30f),"今日委托");Text.Font=GameFont.Small;y+=30f;
   if(st.offers.Count==0){GUI.color=Color.gray;Widgets.Label(new Rect(8f,y,view.width,24f),"今天没有委托。");GUI.color=Color.white;y+=28f;}
   foreach(var c in st.offers.ToList()){DrawOffer(new Rect(0f,y,view.width,rowH-6f),c);y+=rowH;}
   y+=10f;Text.Font=GameFont.Medium;Widgets.Label(new Rect(0f,y,view.width,30f),"已接委托");Text.Font=GameFont.Small;y+=30f;
   if(st.active.Count==0){GUI.color=Color.gray;Widgets.Label(new Rect(8f,y,view.width,24f),"还没有接下委托。");GUI.color=Color.white;}
   foreach(var c in st.active.ToList()){DrawActive(new Rect(0f,y,view.width,50f),c);y+=56f;}
   Widgets.EndScrollView();
  }
  void Badge(Rect r,int tier){Widgets.DrawBoxSolid(r,QuestBoard.TierColors[tier-1]*new Color(1f,1f,1f,.85f));var a=Text.Anchor;Text.Anchor=TextAnchor.MiddleCenter;GUI.color=Color.black;Widgets.Label(r,QuestBoard.TierLabel(tier));GUI.color=Color.white;Text.Anchor=a;}
  void DrawOffer(Rect r,BoardContract c){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(6f);
   Badge(new Rect(inner.x,inner.y,52f,24f),c.tier);
   Widgets.Label(new Rect(inner.x+60f,inner.y+2f,inner.width-200f,24f),QuestBoard.KindLabel(c.kind)+" · "+QuestBoard.Title(c));
   GUI.color=new Color(.8f,.8f,.8f);
   Widgets.Label(new Rect(inner.x,inner.y+28f,inner.width-150f,24f),"推荐等级 "+QuestBoard.TierMinLevel[c.tier-1]+"+ · 威胁点数 "+Mathf.RoundToInt(c.points)+" · 经验约 "+Mathf.RoundToInt(QuestBoard.XpFor(c.points)*Adventurer.XpScale)+(c.kind==QuestBoard.Defense?" · 在殖民地":" · 要远征"));
   Widgets.Label(new Rect(inner.x,inner.y+52f,inner.width-150f,24f),"报酬："+QuestBoard.RewardText(c));
   GUI.color=Color.white;
   TooltipHandler.TipRegion(new Rect(inner.x,inner.y,inner.width-150f,inner.height),QuestBoard.Describe(c));
   var ok=QuestBoard.CanAccept(c);var btn=new Rect(inner.xMax-130f,inner.y+20f,130f,36f);
   if(!ok.Accepted){GUI.color=Color.gray;Widgets.ButtonText(btn,"接取",true,true,false);GUI.color=Color.white;TooltipHandler.TipRegion(btn,ok.Reason??"");}
   else if(Widgets.ButtonText(btn,"接取"))Verse.Find.WindowStack.Add(new FloatMenu(Accepters(c)));
  }
  List<FloatMenuOption> Accepters(BoardContract c){
   var map=board?.Map??QuestBoard.Home;var list=new List<FloatMenuOption>();
   var pawns=map==null?new List<Pawn>():map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Dead).OrderByDescending(p=>Adventurer.Get(p)?.level??0).ToList();
   foreach(var p in pawns){
    var pp=p;int lv=Adventurer.Get(p)?.level??0;
    string label=p.LabelShort+(lv>0?"（"+lv+" 级）":"")+(lv>0 && lv<QuestBoard.TierMinLevel[c.tier-1]?" 低于推荐等级":"");
    list.Add(new FloatMenuOption("由 "+label+" 接取",()=>QuestBoard.Accept(c,pp)));
   }
   if(list.Count==0)list.Add(new FloatMenuOption("直接接取（没有接取人）",()=>QuestBoard.Accept(c,null)));
   return list;
  }
  void DrawActive(Rect r,BoardContract c){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(6f);
   Badge(new Rect(inner.x,inner.y+6f,52f,24f),c.tier);
   Widgets.Label(new Rect(inner.x+60f,inner.y,inner.width-200f,22f),QuestBoard.Title(c)+(c.quest?.AccepterPawn!=null?"（"+c.quest.AccepterPawn.LabelShort+"）":""));
   GUI.color=new Color(.8f,.8f,.8f);Widgets.Label(new Rect(inner.x+60f,inner.y+20f,inner.width-200f,22f),QuestBoard.Status(c));GUI.color=Color.white;
   var btn=new Rect(inner.xMax-130f,inner.y+3f,130f,32f);
   if(c.site!=null && !c.site.Destroyed){if(Widgets.ButtonText(btn,"定位目标")){Close();CameraJumper.TryJumpAndSelect(c.site);}}
   else if(c.raiders.Any(p=>!QuestBoard.Gone(p)) && Widgets.ButtonText(btn,"定位敌人")){Close();CameraJumper.TryJumpAndSelect(c.raiders.First(p=>!QuestBoard.Gone(p)));}
  }
 }

 // 1.71.0 设置（放在“冒险者”分组里）。
 public partial class Settings {
  public bool questBoard=true;
  void ExposeQuestBoard(){Scribe_Values.Look(ref questBoard,"questBoard",true);}
 }
}
