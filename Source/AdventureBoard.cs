using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.134.0 冒险委托板（Defs/AdventureBoard.xml）。冒险者告示板每 N 天（设置 3 / 5 / 10，默认 5）贴出 3 条委托，同时最多接 2 条；
 // 接下后世界地图 2～8 格出现地点（原版 Site + GenStep_AdventureSite），7 天内完成，失败名声 -3。
 // 推荐等级 = 殖民地冒险者等级最高 3 人的平均 ±2（低 -2、中 ±0～1、高 +1、致命 +2）；威胁点数按推荐等级算再乘难度倍率（不按财富）。
 // 委托类型是 Def + Worker（AdventureKindDef / AdventureWorker），以后的组织声望、闲暇活动、神祇任务可以往里加。
 public class AdventureKindDef : Def {
  public Type workerClass;public float weight=1f;public bool legendary;
  [Unsaved] AdventureWorker worker;
  public AdventureWorker Worker{get{if(worker==null && workerClass!=null){worker=(AdventureWorker)Activator.CreateInstance(workerClass);worker.def=this;}return worker;}}
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(workerClass==null || !typeof(AdventureWorker).IsAssignableFrom(workerClass))yield return defName+": workerClass must be an AdventureWorker";}
 }

 // 一条委托。
 public class AdventureContract : IExposable {
  public int id,danger,recLevel,offerExpires=-1,deadline=-1,mapTick=-1;public float points;public AdventureKindDef kind;public PlanetTile tile=PlanetTile.Invalid;
  public bool lake,done,sawEnemies,captured,choiceAsked;public int silver;public List<string> items=new List<string>();
  public Faction faction;public string lead;public List<string> kinds=new List<string>();
  public Quest quest;public Site site;public Pawn target;public Thing objective;public List<Pawn> participants=new List<Pawn>();
  public AdventureWorker Worker=>kind?.Worker;
  public void ExposeData(){
   Scribe_Values.Look(ref id,"id");Scribe_Values.Look(ref danger,"danger");Scribe_Values.Look(ref recLevel,"recLevel");Scribe_Values.Look(ref offerExpires,"offerExpires",-1);Scribe_Values.Look(ref deadline,"deadline",-1);Scribe_Values.Look(ref mapTick,"mapTick",-1);
   Scribe_Values.Look(ref points,"points");Scribe_Defs.Look(ref kind,"kind");Scribe_Values.Look(ref tile,"tile",PlanetTile.Invalid);
   Scribe_Values.Look(ref lake,"lake");Scribe_Values.Look(ref done,"done");Scribe_Values.Look(ref sawEnemies,"sawEnemies");Scribe_Values.Look(ref captured,"captured");Scribe_Values.Look(ref choiceAsked,"choiceAsked");
   Scribe_Values.Look(ref silver,"silver");Scribe_Collections.Look(ref items,"items",LookMode.Value);
   Scribe_References.Look(ref faction,"faction");Scribe_Values.Look(ref lead,"lead");Scribe_Collections.Look(ref kinds,"kinds",LookMode.Value);
   Scribe_References.Look(ref quest,"quest");Scribe_References.Look(ref site,"site");Scribe_References.Look(ref target,"target");Scribe_References.Look(ref objective,"objective");Scribe_Collections.Look(ref participants,"participants",LookMode.Reference);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(items==null)items=new List<string>();if(kinds==null)kinds=new List<string>();if(participants==null)participants=new List<Pawn>();participants.RemoveAll(p=>p==null);}
  }
 }

 public static class AdventureBoard {
  public const int Offers=3,MaxActive=2,SiteMin=2,SiteMax=8,Days=7,FailRenown=3,TickInterval=250;
  public static readonly string[] DangerNames={"低","中","高","致命"};
  public static readonly Color[] DangerColors={new Color(.55f,.85f,.55f),new Color(.95f,.85f,.45f),new Color(1f,.6f,.35f),new Color(1f,.35f,.35f)};
  public static readonly int[] RenownGain={2,3,5,6};
  public static readonly int[] RenownSteps={0,20,45,70,90};
  public static readonly string[] RenownNames={"无名","小有名气","知名","威名远扬","传奇"};
  public static int? TestRoll;
  public static GameComponent_AdventureBoard State=>Current.Game?.GetComponent<GameComponent_AdventureBoard>();
  public static int Now=>Find.TickManager.TicksGame;
  public static bool Enabled=>LichMod.Settings?.adventureBoard??true;
  public static int IntervalDays=>Mathf.Clamp(LichMod.Settings?.adventureInterval??5,1,30);
  public static float Difficulty=>LichMod.Settings?.adventureDifficulty??1f;
  public static ThingDef BoardDef=>DefCache<ThingDef>.Get("LP_AdventurerBoard");
  public static SitePartDef SiteDef=>DefDatabase<SitePartDef>.GetNamedSilentFail("LP_AB_Site");
  public static QuestScriptDef RootDef=>DefDatabase<QuestScriptDef>.GetNamedSilentFail("LP_AdventureContract");
  public static Map Home=>Find.Maps.Where(m=>m.IsPlayerHome).OrderByDescending(m=>BoardDef!=null && m.listerBuildings.ColonistsHaveBuilding(BoardDef)).ThenByDescending(m=>m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();
  public static bool AnyBoard=>BoardDef!=null && Find.Maps.Any(m=>m.IsPlayerHome && m.listerBuildings.ColonistsHaveBuilding(BoardDef));
  public static List<AdventureKindDef> Kinds=>DefDatabase<AdventureKindDef>.AllDefsListForReading;

  // ---------- 名声 ----------
  public static int RenownLevel(int r){int l=0;for(int i=0;i<RenownSteps.Length;i++)if(r>=RenownSteps[i])l=i;return l;}
  public static string RenownName(int r)=>RenownNames[RenownLevel(r)];
  public static void AddRenown(int d){var st=State;if(st==null)return;st.renown=Mathf.Clamp(st.renown+d,0,100);}

  // ---------- 等级与点数 ----------
  public static int ColonyAverage(){
   var lv=PawnsFinder.AllMaps_FreeColonists.Select(p=>Adventurer.Get(p)?.level??0).OrderByDescending(x=>x).Take(3).ToList();
   if(lv.Count==0)return 1;return Mathf.Max(1,Mathf.RoundToInt((float)lv.Average()));
  }
  public static int Offset(int danger)=>danger==0?-2:danger==1?Rand.RangeInclusive(0,1)*(Rand.Bool?1:-1):danger==2?1:2;
  public static float PointsFor(int rec,int danger)=>(200f+90f*Mathf.Max(1,rec))*(danger==3?1.2f:1f)*Difficulty;
  public static int PickDanger(int renown){
   float w0=3f,w1=3f,w2=1.5f+renown/25f,w3=.5f+renown/40f;float r=(TestRoll.HasValue?TestRoll.Value/100f:Rand.Value)*(w0+w1+w2+w3);
   return r<w0?0:r<w0+w1?1:r<w0+w1+w2?2:3;
  }

  // ---------- 报酬 ----------
  public static float RewardMult(AdventureContract c)=>(c.danger==0?.8f:c.danger==1?1f:c.danger==2?1.3f:1.7f)*(1f+(State?.renown??0)/200f);
  public static List<ThingDef> Scrolls=>DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.tradeTags!=null && d.tradeTags.Contains("LP_Scroll") && d.tradeability!=Tradeability.None).ToList();
  public static void RollReward(AdventureContract c){
   c.silver=Mathf.RoundToInt(c.points*.7f*RewardMult(c)/10f)*10;c.items=new List<string>();
   if(Rand.Chance(.4f+.15f*c.danger)){var s=Scrolls.RandomElementWithFallback();if(s!=null)c.items.Add(s.defName);}
   if(Rand.Chance(.05f) || c.lake){var seal=LakeSeals.Crystals.Where(d=>LakeSeals.Ext(LakeSeals.AbilityOf(d))?.tier==1 || !c.lake && LakeSeals.Ext(LakeSeals.AbilityOf(d))?.tier==2).RandomElementWithFallback();if(seal!=null)c.items.Add(seal.defName);}
   if(c.danger>=2 && Rand.Chance(.01f)){var arc=LakeArcana.Crystals.Where(d=>{var e=LakeArcana.Ext(LakeArcana.AbilityOf(d));return e!=null && e.Universal && e.ring==6;}).RandomElementWithFallback();if(arc!=null)c.items.Add(arc.defName);}
  }
  public static string RewardText(AdventureContract c){
   var parts=new List<string>{c.silver+" 银"};foreach(var n in c.items){var d=DefDatabase<ThingDef>.GetNamedSilentFail(n);if(d!=null)parts.Add(d.label);}
   parts.Add("名声 +"+RenownGain[c.danger]);if(c.lake)parts.Add("魔力湖泊好感 +10～15");
   parts.Add("参与者冒险者经验约 "+Mathf.RoundToInt(XpFor(c)*Adventurer.XpScale));
   return string.Join("、",parts);
  }
  public static float XpFor(AdventureContract c)=>c.points*Adventurer.QuestShare;

  // ---------- 刷新 ----------
  public static AdventureContract MakeOffer(AdventureKindDef kind,int danger,Map home,bool lake=false){
   var st=State;if(st==null || home==null || kind?.Worker==null)return null;
   PlanetTile tile=PlanetTile.Invalid;
   for(int i=0;i<8;i++){if(!TileFinder.TryFindNewSiteTile(out var t,home.Tile,SiteMin,SiteMax))continue;int d=Find.WorldGrid.TraversalDistanceBetween(home.Tile,t);if(d>=SiteMin && d<=SiteMax){tile=t;break;}if(!tile.Valid)tile=t;} // 原版找地点按直线范围，按行走距离再筛一次
   if(!tile.Valid)return null;
   int avg=ColonyAverage();
   var c=new AdventureContract{id=st.nextId++,kind=kind,danger=danger,tile=tile,offerExpires=Now+IntervalDays*GenDate.TicksPerDay,lake=lake};
   c.recLevel=Mathf.Clamp(avg+Offset(danger),1,40);c.points=PointsFor(c.recLevel,danger);
   if(!kind.Worker.CanOffer(c))return null;
   kind.Worker.Setup(c);
   RollReward(c);
   return c;
  }
  public static void Refresh(){
   var st=State;var home=Home;if(st==null)return;
   st.offers.Clear();st.nextRefresh=Now+IntervalDays*GenDate.TicksPerDay;
   if(!Enabled || home==null)return;
   var kinds=Kinds.Where(k=>!k.legendary).ToList();
   bool legend=RenownLevel(st.renown)>=4 && Rand.Chance(.25f);
   int guard=0;
   while(st.offers.Count<Offers && guard++<30){
    bool wantLegend=legend && st.offers.Count==0;
    var kind=wantLegend?Kinds.FirstOrDefault(k=>k.legendary):kinds.RandomElementByWeight(k=>k.weight);
    int danger=wantLegend?3:PickDanger(st.renown);
    // 三条里至少一条低或中
    if(st.offers.Count==Offers-1 && !st.offers.Any(o=>o.danger<=1))danger=Rand.RangeInclusive(0,1);
    bool lake=!wantLegend && !st.offers.Any(o=>o.lake) && LakeClient() && Rand.Chance(.25f);
    var c=MakeOffer(kind,danger,home,lake);if(wantLegend)legend=false;
    if(c!=null)st.offers.Add(c);
   }
  }
  public static bool LakeClient(){var f=ManaLake.Faction;return f!=null && !f.defeated && !f.HostileTo(Faction.OfPlayer);}
  public static void EnsureFresh(){var st=State;if(st!=null && (st.nextRefresh<0 || Now>=st.nextRefresh))Refresh();}

  // ---------- 文字 ----------
  public static string Title(AdventureContract c)=>(c.lake?"湖畔委托·":"")+c.kind.label+"："+c.Worker.Enemies(c);
  public static int Distance(AdventureContract c){var home=Home;if(home==null || !c.tile.Valid)return -1;return Find.WorldGrid.TraversalDistanceBetween(home.Tile,c.tile);}
  public static string Describe(AdventureContract c){
   return (c.lake?"委托方：魔力湖泊（湖民请冒险者帮忙，完成后好感 +10～15，另给一枚一阶湖印结晶）。\n\n":"")+c.kind.description+"\n\n地点：离殖民地约 "+Distance(c)+" 格（接下后出现在世界地图上，"+Days+" 天内完成，失败名声 -"+FailRenown+"）\n推荐等级："+c.recLevel+"　危险度："+DangerNames[c.danger]+"\n敌人："+c.Worker.Enemies(c)+"\n威胁点数："+Mathf.RoundToInt(c.points)+"\n报酬："+RewardText(c);
  }

  // ---------- 接受 ----------
  public static AcceptanceReport CanAccept(AdventureContract c){
   var st=State;if(st==null || c==null)return false;
   if(!Enabled)return "冒险委托板已在设置里关闭";
   if(!st.offers.Contains(c))return "委托已经下架";
   if(st.active.Count>=MaxActive)return "同时最多接 "+MaxActive+" 条委托";
   if(Home==null)return "没有殖民地";
   return true;
  }
  public static Quest Accept(AdventureContract c){
   var ok=CanAccept(c);if(!ok.Accepted){if(!ok.Reason.NullOrEmpty() && !Adventurer.TestSuppressed)Messages.Message(ok.Reason,MessageTypeDefOf.RejectInput,false);return null;}
   var st=State;var home=Home;
   if(!MakeSite(c,home)){if(!Adventurer.TestSuppressed)Messages.Message("委托的地点已经找不到了，换一条委托试试。",MessageTypeDefOf.RejectInput,false);return null;}
   c.deadline=Now+Days*GenDate.TicksPerDay;
   var q=Quest.MakeRaw();q.name=Title(c);q.description=Describe(c);q.root=RootDef;q.challengeRating=Mathf.Clamp(c.danger,1,3);q.points=0f;q.appearanceTick=Now; // 经验由委托板按参与者给，不走 Adventurer.QuestDone
   q.AddPart(new QuestPart_Adventure{contractId=c.id});
   Find.QuestManager.Add(q);q.Accept(null);
   c.quest=q;st.offers.Remove(c);st.active.Add(c);
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("接下委托："+Title(c),"冒险者告示板上的委托已经接下，地点标在世界地图上，"+Days+" 天内完成。\n\n"+Describe(c),LetterDefOf.NeutralEvent,new LookTargets(c.site),null,q);
   return q;
  }
  static bool MakeSite(AdventureContract c,Map home){
   var tile=c.tile;
   if(!tile.Valid || Find.WorldObjects.AnyWorldObjectAt(tile)){if(!TileFinder.TryFindNewSiteTile(out tile,home.Tile,SiteMin,SiteMax))return false;c.tile=tile;}
   if(SiteDef==null)return false;
   var site=SiteMaker.MakeSite(SiteDef,tile,null,true,c.points);if(site==null)return false;
   foreach(var p in site.parts){if(p.parms==null)p.parms=new SitePartParams();p.parms.threatPoints=c.points;}
   site.customLabel=c.kind.label;
   site.GetComponent<TimeoutComp>()?.StartTimeout(Days*GenDate.TicksPerDay);
   Find.WorldObjects.Add(site);c.site=site;return true;
  }

  // ---------- 推进 ----------
  public static void Tick(AdventureContract c){
   var q=c.quest;if(q==null || q.State!=QuestState.Ongoing){Drop(c);return;}
   if(c.site!=null && c.site.HasMap){
    var map=c.site.Map;if(c.mapTick<0)c.mapTick=Now;
    foreach(var p in map.mapPawns.FreeColonistsSpawned)if(!c.participants.Contains(p))c.participants.Add(p);
    if(QuestBoard.Hostiles(map))c.sawEnemies=true;
   }
   if(c.Worker.Done(c)){Complete(c);return;}
   if(Now>c.deadline){Fail(c,Days+" 天期限已到。");return;}
   if((c.site==null || c.site.Destroyed) && !c.Worker.SurvivesSite(c))Fail(c,"委托地点已经消失。");
  }
  public static List<Pawn> Rewarded(AdventureContract c){
   var list=c.participants.Where(p=>p!=null && !p.Dead && p.Faction==Faction.OfPlayer).Distinct().ToList();
   if(list.Count==0 && c.site?.Map!=null)list=c.site.Map.mapPawns.FreeColonistsSpawned.ToList();
   return list;
  }
  public static void Complete(AdventureContract c){
   var st=State;var home=Home;var q=c.quest;c.done=true;st?.active.Remove(c);
   float mult=c.captured?1.25f:1f;
   var reward=new List<Thing>();int silver=Mathf.RoundToInt(c.silver*mult);
   while(silver>0){var s=ThingMaker.MakeThing(ThingDefOf.Silver);s.stackCount=Mathf.Min(silver,ThingDefOf.Silver.stackLimit);silver-=s.stackCount;reward.Add(s);}
   foreach(var n in c.items){var d=DefDatabase<ThingDef>.GetNamedSilentFail(n);if(d!=null)reward.Add(ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null));}
   if(home!=null && reward.Count>0)DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(home),home,reward,110,false,false,true,false);
   var who=Rewarded(c);float xp=XpFor(c)*mult;foreach(var p in who)Adventurer.GainXP(p,xp);
   int gain=RenownGain[c.danger];AddRenown(gain);
   if(c.lake){var f=ManaLake.Faction;if(f!=null && !f.HostileTo(Faction.OfPlayer))f.TryAffectGoodwillWith(Faction.OfPlayer,Rand.RangeInclusive(10,15),true,!Adventurer.TestSuppressed);}
   c.Worker.OnComplete(c);
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("委托完成："+Title(c),"冒险者告示板上的委托「"+Title(c)+"」完成了"+(c.captured?"（俘获首领，报酬 ×1.25）":"")+"。\n\n报酬（空投送到殖民地）："+RewardText(c)+"\n冒险者经验："+(who.Count>0?string.Join("、",who.Select(p=>p.LabelShort))+" 各约 "+Mathf.RoundToInt(xp*Adventurer.XpScale)+" 点":"没有参与者")+"\n殖民地名声 +"+gain+"（现在 "+(st?.renown??0)+"，"+RenownName(st?.renown??0)+"）。",LetterDefOf.PositiveEvent,home!=null?new LookTargets(new TargetInfo(DropCellFinder.TradeDropSpot(home),home)):LookTargets.Invalid,null,q);
   if(q!=null && q.State==QuestState.Ongoing)q.End(QuestEndOutcome.Success,false);
  }
  public static void Fail(AdventureContract c,string why){
   var st=State;var q=c.quest;st?.active.Remove(c);c.done=true;AddRenown(-FailRenown);
   if(c.site!=null && !c.site.Destroyed && !c.site.HasMap)c.site.Destroy();
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("委托失败："+Title(c),why+"冒险者告示板上的委托「"+Title(c)+"」失败了，殖民地名声 -"+FailRenown+"（现在 "+(st?.renown??0)+"）。",LetterDefOf.NegativeEvent,LookTargets.Invalid,null,q);
   if(q!=null && q.State==QuestState.Ongoing)q.End(QuestEndOutcome.Fail,false);
  }
  static void Drop(AdventureContract c){var st=State;st?.active.Remove(c);c.done=true;if(c.site!=null && !c.site.Destroyed && !c.site.HasMap)c.site.Destroy();}
  public static string Status(AdventureContract c){
   string s=c.Worker.Status(c);
   return s+"，剩余 "+Mathf.Max(0f,(c.deadline-Now)/(float)GenDate.TicksPerDay).ToString("0.#")+" 天";
  }
  public static AdventureContract ForSite(Site s)=>s==null?null:State?.active.FirstOrDefault(c=>c.site==s);

  // ---------- 地图工具 ----------
  public static IntVec3 Center(Map map){
   if(!CellFinder.TryFindRandomCellNear(map.Center,map,Mathf.Min(map.Size.x,map.Size.z)/5,c=>c.Standable(map) && !c.Fogged(map) && !c.Roofed(map),out var center) &&
      !CellFinder.TryFindRandomCellNear(map.Center,map,Mathf.Min(map.Size.x,map.Size.z)/3,c=>c.Standable(map),out center))center=CellFinder.RandomCell(map);
   return center;
  }
  public static Faction EnemyFaction(float points)=>QuestBoard.PickFaction(points,false)??Find.FactionManager.RandomEnemyFaction(false,false,true,TechLevel.Undefined);
  public static List<Pawn> SpawnBeasts(Map map,IntVec3 at,List<string> kinds){
   var list=new List<Pawn>();
   foreach(var g in kinds.GroupBy(n=>n)){
    var k=DefDatabase<PawnKindDef>.GetNamedSilentFail(g.Key);if(k==null)continue;
    foreach(var p in AggressiveAnimalIncidentUtility.GenerateAnimals(k,map.Tile,k.combatPower*g.Count(),g.Count())){GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(at,map,8),map);p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true);list.Add(p);}
   }
   return list;
  }
  public static List<Pawn> SpawnGroup(Map map,IntVec3 at,Faction f,float points,bool defend){
   var list=new List<Pawn>();if(f==null)return list;
   try{list=PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,tile=map.Tile,faction=f,points=Mathf.Max(points,f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat))},false).ToList();}
   catch(Exception e){Log.Warning("[DND边缘大陆] 冒险委托生成敌人失败："+e.Message);}
   foreach(var p in list)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(at,map,7),map);
   if(list.Count>0)LordMaker.MakeNewLord(f,defend?(LordJob)new LordJob_DefendPoint(at,12f):new LordJob_AssaultColony(f,false,false,false,false,false,false,false),map,list);
   return list;
  }
  public static readonly string[] MonsterKinds=QuestBoardMonsters.BeastKinds;
 }

 // ==================== 委托类型 ====================
 public abstract class AdventureWorker {
  public AdventureKindDef def;
  public virtual bool CanOffer(AdventureContract c)=>true;
  public abstract void Setup(AdventureContract c);
  public abstract string Enemies(AdventureContract c);
  public abstract void Generate(Map map,AdventureContract c);
  public abstract bool Done(AdventureContract c);
  public virtual bool SurvivesSite(AdventureContract c)=>false; // 地点消失后委托还在（救援、寻回：人 / 东西已经带走）
  public virtual string Status(AdventureContract c)=>c.site==null || c.site.Destroyed?"地点已经消失":c.site.HasMap?(c.sawEnemies?"已到达，还有敌人":"已到达"):"前往世界地图上的「"+c.site.Label+"」";
  public virtual void OnComplete(AdventureContract c){}
  protected static string Roster(List<string> kinds)=>string.Join("、",kinds.GroupBy(n=>n).Select(g=>(DefDatabase<PawnKindDef>.GetNamedSilentFail(g.Key)?.label??g.Key)+(g.Count()>1?" ×"+g.Count():"")));
  protected static bool Clear(AdventureContract c)=>c.site!=null && c.site.HasMap && !QuestBoard.Hostiles(c.site.Map) && (c.sawEnemies || AdventureBoard.Now-c.mapTick>GenDate.TicksPerHour);
 }
 // 1. 清剿怪物巢穴：DND 野生怪物 / 野兽，按地形优先（当地生物群系里会出现的），全清即完成。
 public class AdventureWorker_Lair : AdventureWorker {
  public static List<string> Pool(PlanetTile tile){
   var all=AdventureBoard.MonsterKinds.Select(n=>DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k=>k!=null).ToList();
   var biome=tile.Valid?Find.WorldGrid[tile]?.PrimaryBiome:null;
   var local=biome==null?new List<PawnKindDef>():all.Where(k=>biome.CommonalityOfAnimal(k)>0f).ToList();
   return (local.Count>=2?local:all).Select(k=>k.defName).ToList();
  }
  public override bool CanOffer(AdventureContract c)=>Pool(c.tile).Count>0;
  public override void Setup(AdventureContract c){c.kinds=QuestBoardMonsters.Compose(Pool(c.tile).ToArray(),c.points,true);}
  public override string Enemies(AdventureContract c)=>Roster(c.kinds);
  public override void Generate(Map map,AdventureContract c){AdventureBoard.SpawnBeasts(map,AdventureBoard.Center(map),c.kinds);}
  public override bool Done(AdventureContract c)=>Clear(c);
 }
 // 2. 讨伐通缉犯：一名带职业的敌对首领 + 护卫；击杀或俘获首领即完成，俘获报酬 ×1.25。
 public class AdventureWorker_Bounty : AdventureWorker {
  public override bool CanOffer(AdventureContract c){c.faction=Factions.Hostile!=null && !Factions.Hostile.defeated && Factions.Hostile.HostileTo(Faction.OfPlayer)?Factions.Hostile:AdventureBoard.EnemyFaction(c.points);return c.faction!=null;}
  public override void Setup(AdventureContract c){var names=new[]{"「血手」","「独眼」","「灰袍」","「毒牙」","「铁颚」","「无名者」"};c.lead=names.RandomElement();}
  public override string Enemies(AdventureContract c)=>"通缉犯"+c.lead+"（"+(c.faction?.Name??"敌对派系")+"，带职业，约 "+(c.recLevel+2)+" 级）和护卫";
  public override void Generate(Map map,AdventureContract c){
   var f=c.faction!=null && !c.faction.defeated?c.faction:AdventureBoard.EnemyFaction(c.points);if(f==null)return;var at=AdventureBoard.Center(map);
   var guards=AdventureBoard.SpawnGroup(map,at,f,c.points*.7f,true);
   var boss=PawnGenerator.GeneratePawn(new PawnGenerationRequest(f.RandomPawnKind()??PawnKindDefOf.Colonist,f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true,canGeneratePawnRelations:false));
   if(boss.Name is NameTriple nt)boss.Name=new NameTriple(nt.First,c.lead.Trim('「','」'),nt.Last);
   GenSpawn.Spawn(boss,at,map);
   var h=Adventurer.Get(boss)??Adventurer.TryAdd(boss,Mathf.Clamp(c.recLevel+2,1,40),true);if(h!=null){h.level=Mathf.Clamp(c.recLevel+2,1,40);h.Dirty();if(h.classes==null || h.classes.levels.Count==0)NpcClasses.Assign(h,0f);}
   var lord=guards.Count>0?guards[0].GetLord():null;if(lord!=null)lord.AddPawn(boss);else LordMaker.MakeNewLord(f,new LordJob_DefendPoint(at,12f),map,new List<Pawn>{boss});
   c.target=boss;
  }
  public override bool Done(AdventureContract c){
   var t=c.target;if(t==null)return Clear(c);
   if(t.IsPrisonerOfColony || t.Faction==Faction.OfPlayer){c.captured=true;return true;}
   return t.Dead;
  }
  public override bool SurvivesSite(AdventureContract c)=>c.target!=null && (c.target.Dead || c.target.IsPrisonerOfColony);
  public override string Status(AdventureContract c){var t=c.target;if(t==null)return base.Status(c);if(t.Downed && !t.Dead)return "通缉犯已倒下：俘获（关进囚室）报酬 ×1.25，或者击杀";return base.Status(c);}
 }
 // 3. 救援被困冒险者：倒地的冒险者 + 围攻的怪物；救回殖民地后可以招募或领赏。
 public class AdventureWorker_Rescue : AdventureWorker {
  public override bool CanOffer(AdventureContract c)=>AdventureWorker_Lair.Pool(c.tile).Count>0 && Friendly()!=null;
  public static Faction Friendly()=>Factions.Colony!=null && !Factions.Colony.defeated && !Factions.Colony.HostileTo(Faction.OfPlayer)?Factions.Colony:Find.FactionManager.AllFactionsListForReading.Where(f=>!f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer) && !f.temporary).RandomElementWithFallback();
  public override void Setup(AdventureContract c){c.kinds=QuestBoardMonsters.Compose(AdventureWorker_Lair.Pool(c.tile).ToArray(),c.points*.8f,true);c.faction=Friendly();}
  public override string Enemies(AdventureContract c)=>"围攻的"+Roster(c.kinds);
  public override void Generate(Map map,AdventureContract c){
   var at=AdventureBoard.Center(map);var f=c.faction!=null && !c.faction.defeated && !c.faction.HostileTo(Faction.OfPlayer)?c.faction:Friendly();
   var npc=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true,canGeneratePawnRelations:false,fixedBiologicalAge:Rand.Range(20f,40f)));
   GenSpawn.Spawn(npc,at,map);
   var h=Adventurer.Get(npc)??Adventurer.TryAdd(npc,Mathf.Clamp(c.recLevel,1,20),true);if(h!=null && (h.classes==null || h.classes.levels.Count==0))NpcClasses.Assign(h,0f);
   HealthUtility.DamageUntilDowned(npc,false);
   c.target=npc;
   var ring=CellFinder.RandomClosewalkCellNear(at,map,10);AdventureBoard.SpawnBeasts(map,ring,c.kinds);
  }
  public override bool Done(AdventureContract c){var t=c.target;return t!=null && !t.Dead && t.MapHeld!=null && t.MapHeld.IsPlayerHome;}
  public override bool SurvivesSite(AdventureContract c)=>c.target!=null && !c.target.Dead && c.target.MapHeld!=null && (c.site==null || c.target.MapHeld!=c.site.Map);
  public override string Status(AdventureContract c){var t=c.target;if(t!=null && t.Dead)return "被困的冒险者已经死了";if(t!=null && t.MapHeld!=null && c.site?.Map!=t.MapHeld && !t.MapHeld.IsPlayerHome)return "把冒险者带回殖民地";return base.Status(c);}
  public override void OnComplete(AdventureContract c){
   var t=c.target;if(t==null || t.Dead)return;
   if(Adventurer.TestSuppressed)return;
   Find.WindowStack.Add(new Dialog_SpellChoice("救援成功：「"+t.LabelShort+"」","被救回的冒险者向你道谢。要招募他加入殖民地，还是让他按委托付一笔赏金（当前报酬的一半）后自行离开？",
    new List<(string,string,Action)>{("招募 "+t.LabelShort,"他会加入殖民地。",()=>Recruit(t)),("领赏（+"+(c.silver/2)+" 银）","他会在养好伤后离开。",()=>Bounty(c))},()=>{}));
  }
  public static void Recruit(Pawn t){if(t.Faction!=Faction.OfPlayer){t.SetFaction(Faction.OfPlayer);t.guest?.SetGuestStatus(null);}Messages.Message(t.LabelShort+"加入了殖民地。",t,MessageTypeDefOf.PositiveEvent,false);}
  public static void Bounty(AdventureContract c){var home=AdventureBoard.Home;if(home==null)return;var s=ThingMaker.MakeThing(ThingDefOf.Silver);s.stackCount=Mathf.Max(1,c.silver/2);DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(home),home,new List<Thing>{s},110,false,false,true,false);}
 }
 // 4. 寻回失物：被守卫的遗物，带回殖民地即完成。
 public class AdventureWorker_Recover : AdventureWorker {
  public override bool CanOffer(AdventureContract c){c.faction=AdventureBoard.EnemyFaction(c.points);return c.faction!=null || AdventureWorker_Lair.Pool(c.tile).Count>0;}
  public override void Setup(AdventureContract c){if(c.faction==null)c.kinds=QuestBoardMonsters.Compose(AdventureWorker_Lair.Pool(c.tile).ToArray(),c.points,true);}
  public override string Enemies(AdventureContract c)=>c.faction!=null?c.faction.Name+"的看守":"守着遗物的"+Roster(c.kinds);
  public override void Generate(Map map,AdventureContract c){
   var at=AdventureBoard.Center(map);var relic=ThingMaker.MakeThing(DefCache<ThingDef>.Get("LP_AB_LostRelic"));GenPlace.TryPlaceThing(relic,at,map,ThingPlaceMode.Near);c.objective=relic;
   if(c.faction!=null && !c.faction.defeated)AdventureBoard.SpawnGroup(map,at,c.faction,c.points,true);else AdventureBoard.SpawnBeasts(map,at,c.kinds);
  }
  public override bool Done(AdventureContract c){var o=c.objective;return o!=null && !o.Destroyed && o.MapHeld!=null && o.MapHeld.IsPlayerHome;}
  public override bool SurvivesSite(AdventureContract c)=>c.objective!=null && !c.objective.Destroyed && c.objective.MapHeld!=null && (c.site==null || c.objective.MapHeld!=c.site.Map) || c.objective!=null && !c.objective.Destroyed && c.objective.ParentHolder is Pawn_InventoryTracker inv && inv.pawn.GetCaravan()!=null;
  public override string Status(AdventureContract c){var o=c.objective;if(o==null || o.Destroyed)return "遗物已经毁了";if(o.MapHeld==null || o.MapHeld!=c.site?.Map)return "把遗物带回殖民地";return base.Status(c);}
  public override void OnComplete(AdventureContract c){var o=c.objective;if(o!=null && !o.Destroyed)o.Destroy();}
 }
 // 5. 调查闹鬼废墟：亡灵 + 可摧毁的亡灵之源，摧毁即完成。
 public class AdventureWorker_Haunted : AdventureWorker {
  public override void Setup(AdventureContract c){c.kinds=QuestBoardMonsters.Compose(QuestBoardMonsters.UndeadKinds,c.points,false);c.faction=Factions.Hostile??AdventureBoard.EnemyFaction(c.points);}
  public override bool CanOffer(AdventureContract c)=>QuestBoardMonsters.UndeadKinds.Any(n=>DefDatabase<PawnKindDef>.GetNamedSilentFail(n)!=null) && (Factions.Hostile??AdventureBoard.EnemyFaction(c.points))!=null;
  public override string Enemies(AdventureContract c)=>Roster(c.kinds)+"，以及亡灵之源";
  public override void Generate(Map map,AdventureContract c){
   var at=AdventureBoard.Center(map);var src=ThingMaker.MakeThing(DefCache<ThingDef>.Get("LP_AB_UndeadSource"));GenSpawn.Spawn(src,at,map);c.objective=src;
   var f=c.faction!=null && !c.faction.defeated?c.faction:Factions.Hostile??AdventureBoard.EnemyFaction(c.points);var list=new List<Pawn>();
   foreach(var n in c.kinds){var p=AbyssRaces.Make(n,f,map.Tile);if(p==null)continue;GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(at,map,6),map);list.Add(p);}
   if(list.Count>0)LordMaker.MakeNewLord(f,new LordJob_DefendPoint(at,10f),map,list);
  }
  public override bool Done(AdventureContract c)=>c.objective!=null && c.objective.Destroyed && c.site!=null && c.site.HasMap;
  public override string Status(AdventureContract c)=>c.site!=null && c.site.HasMap && c.objective!=null && !c.objective.Destroyed?"摧毁亡灵之源":base.Status(c);
 }
 // 6. 讨伐传奇魔物（名声「传奇」）：贝希尔 / 死亡骑士 / 五色真龙。
 public class AdventureWorker_Legend : AdventureWorker {
  public override bool CanOffer(AdventureContract c)=>Dragons.On || Monsters.On && (Monsters.Available || Monsters.DeathKnightOn);
  public override void Setup(AdventureContract c){
   if(Dragons.On && (!(Monsters.On && (Monsters.Available || Monsters.DeathKnightOn)) || Rand.Bool)){c.lead=Dragons.PickColor(AdventureBoard.Home,false);c.kinds=new List<string>();return;}
   c.lead=Monsters.Available && (!Monsters.DeathKnightOn || Rand.Bool)?Monsters.Behir:Monsters.DeathKnight;
   c.kinds=c.lead==Monsters.Behir?QuestBoardMonsters.Compose(QuestBoardMonsters.BeastKinds,c.points*QuestBoardMonsters.EscortShare,true):QuestBoardMonsters.Compose(QuestBoardMonsters.UndeadKinds,c.points*QuestBoardMonsters.EscortShare,false);
  }
  bool Dragon(AdventureContract c)=>c.lead!=null && Dragons.Info.ContainsKey(c.lead);
  public override string Enemies(AdventureContract c)=>Dragon(c)?Dragons.TitleFor(c.lead,1)+"和本色护卫":Monsters.Label(c.lead)+(c.kinds.Count>0?"和"+Roster(c.kinds):"");
  public override void Generate(Map map,AdventureContract c){
   var at=AdventureBoard.Center(map);
   if(Dragon(c)){Dragons.Raid(map,c.lead,1,c.points);return;}
   var f=Factions.Hostile??Monsters.Beasts();var boss=Monsters.Make(c.lead,f,map.Tile);if(boss!=null){GenSpawn.Spawn(boss,at,map);if(boss.Faction!=null)LordMaker.MakeNewLord(boss.Faction,new LordJob_DefendPoint(at,12f),map,new List<Pawn>{boss});}
   if(c.lead==Monsters.Behir)AdventureBoard.SpawnBeasts(map,at,c.kinds);
   else{var list=new List<Pawn>();foreach(var n in c.kinds){var p=AbyssRaces.Make(n,boss?.Faction??f,map.Tile);if(p==null)continue;GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(at,map,6),map);list.Add(p);}if(list.Count>0 && boss?.GetLord()!=null)foreach(var p in list)boss.GetLord().AddPawn(p);}
  }
  public override bool Done(AdventureContract c)=>Clear(c);
 }

 // ==================== 组件、站点、建筑、窗口 ====================
 public class GameComponent_AdventureBoard : GameComponent {
  public List<AdventureContract> offers=new List<AdventureContract>(),active=new List<AdventureContract>();
  public int nextRefresh=-1,nextId=1,renown;
  public GameComponent_AdventureBoard(Game game){}
  public AdventureContract ById(int id)=>active.FirstOrDefault(c=>c.id==id)??offers.FirstOrDefault(c=>c.id==id);
  public override void GameComponentTick(){
   if(Find.TickManager.TicksGame%AdventureBoard.TickInterval!=0)return;
   if(AdventureBoard.Enabled && AdventureBoard.AnyBoard && (nextRefresh<0 || AdventureBoard.Now>=nextRefresh))AdventureBoard.Refresh();
   for(int i=active.Count-1;i>=0;i--){if(i>=active.Count)continue;try{AdventureBoard.Tick(active[i]);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 冒险委托出错："+e,0x1340+i);}}
  }
  public override void ExposeData(){
   Scribe_Collections.Look(ref offers,"offers",LookMode.Deep);Scribe_Collections.Look(ref active,"active",LookMode.Deep);
   Scribe_Values.Look(ref nextRefresh,"nextRefresh",-1);Scribe_Values.Look(ref nextId,"nextId",1);Scribe_Values.Look(ref renown,"renown");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(offers==null)offers=new List<AdventureContract>();if(active==null)active=new List<AdventureContract>();offers.RemoveAll(c=>c?.kind==null);active.RemoveAll(c=>c?.kind==null || c.quest==null);}
  }
 }
 public class QuestPart_Adventure : QuestPart {
  public int contractId;
  public override IEnumerable<GlobalTargetInfo> QuestLookTargets{get{
   var c=AdventureBoard.State?.ById(contractId);if(c==null)yield break;
   if(c.site!=null && !c.site.Destroyed)yield return c.site;
   if(c.target!=null && c.target.Spawned && !c.target.Dead)yield return c.target;
   if(c.objective!=null && c.objective.Spawned)yield return c.objective;
  }}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref contractId,"contractId");}
 }
 public class GenStep_AdventureSite : GenStep {
  public override int SeedPart=>1340001;
  public override void Generate(Map map,GenStepParams parms){
   try{var c=AdventureBoard.ForSite(parms.sitePart?.site);if(c==null)return;c.Worker.Generate(map,c);}
   catch(Exception e){Log.Error("[DND边缘大陆] 生成委托地点出错："+e);}
  }
 }
 public class PlaceWorker_OnePerMap : PlaceWorker {
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef,IntVec3 loc,Rot4 rot,Map map,Thing thingToIgnore=null,Thing thing=null){
   if(map==null)return true;var td=checkingDef as ThingDef??(checkingDef as ThingDef);
   bool any=map.listerThings.AllThings.Any(t=>t!=thingToIgnore && t!=thing && (t.def==checkingDef || t.def.entityDefToBuild==checkingDef || t is MinifiedThing m && m.InnerThing?.def==checkingDef && m.Spawned));
   return any?(AcceptanceReport)"每张地图只能有一块冒险者告示板":true;
  }
 }
 public class Building_AdventurerBoard : Building {
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(Faction!=Faction.OfPlayer)yield break;
   yield return new Command_Action{defaultLabel="查看委托",defaultDesc="打开冒险者告示板：查看当前 3 条委托、接下委托、看已接委托的进度和殖民地名声。",icon=def.uiIcon,action=()=>Find.WindowStack.Add(new Window_AdventureBoard())};
  }
  public override string GetInspectString(){
   var s=base.GetInspectString();var st=AdventureBoard.State;if(st==null)return s;
   string mine=!AdventureBoard.Enabled?"冒险委托板已在设置里关闭":"委托 "+st.offers.Count+" 条，已接 "+st.active.Count+"/"+AdventureBoard.MaxActive+"，名声 "+st.renown+"（"+AdventureBoard.RenownName(st.renown)+"）";
   return s.NullOrEmpty()?mine:s+"\n"+mine;
  }
 }
 public class Window_AdventureBoard : Window {
  Vector2 scroll;string armed;
  public Window_AdventureBoard(){doCloseX=true;doCloseButton=true;forcePause=true;absorbInputAroundWindow=true;}
  public override Vector2 InitialSize=>LPUI.Fit(860f,680f);
  public override void PreOpen(){base.PreOpen();AdventureBoard.EnsureFresh();}
  public override void DoWindowContents(Rect r){
   try{
    var st=AdventureBoard.State;if(st==null)return;
    float y=LPUI.Title(r,"冒险者告示板");
    // 名声条
    int lvl=AdventureBoard.RenownLevel(st.renown);var bar=new Rect(r.x,y,r.width,26f);
    Widgets.FillableBar(bar,st.renown/100f,SolidColorMaterials.NewSolidColorTexture(new Color(.75f,.6f,.3f,.8f)));
    var a=Text.Anchor;Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(bar,"殖民地名声 "+st.renown+" / 100 · "+AdventureBoard.RenownNames[lvl]+(lvl<4?"（"+AdventureBoard.RenownNames[lvl+1]+"需要 "+AdventureBoard.RenownSteps[lvl+1]+"）":""));Text.Anchor=a;
    TooltipHandler.TipRegion(bar,"名声：无名 0 / 小有名气 20 / 知名 45 / 威名远扬 70 / 传奇 90。\n完成委托 +2～+6（按危险度），失败 -3。名声越高，危险度高的委托越多、报酬越好；「传奇」时偶尔出现讨伐传奇魔物的委托。");
    y+=30f;
    string head=AdventureBoard.Enabled?"殖民地冒险者等级最高 3 人平均 "+AdventureBoard.ColonyAverage()+" 级。委托每 "+AdventureBoard.IntervalDays+" 天换一批（约 "+Mathf.Max(0f,(st.nextRefresh-AdventureBoard.Now)/(float)GenDate.TicksPerDay).ToString("0.#")+" 天后），没接的到时消失；同时最多接 "+AdventureBoard.MaxActive+" 条，接下后 "+AdventureBoard.Days+" 天内完成。":"冒险委托板已在设置（冒险者分组）里关闭。";
    GUI.color=Color.gray;y+=LPUI.Para(r.x,y,r.width,head)+LPUI.Gap;GUI.color=Color.white;
    var outer=new Rect(r.x,y,r.width,r.yMax-y-48f);const float rowH=112f;
    var view=new Rect(0f,0f,outer.width-LPUI.ScrollBarW,LPUI.RowH*2+LPUI.Gap*2+Mathf.Max(1,st.offers.Count)*(rowH+6f)+Mathf.Max(1,st.active.Count)*62f+20f);
    Widgets.BeginScrollView(outer,ref scroll,view);
    try{
     float yy=LPUI.Bar(new Rect(0f,0f,view.width,LPUI.RowH),"当前委托",LPUI.Gold);
     if(st.offers.Count==0){LPUI.Unavailable(new Rect(0f,yy,view.width,LPUI.RowH),"现在没有委托。");yy+=LPUI.RowH+LPUI.Gap;}
     foreach(var c in st.offers.ToList()){DrawOffer(new Rect(0f,yy,view.width,rowH),c);yy+=rowH+6f;}
     yy=LPUI.Bar(new Rect(0f,yy,view.width,LPUI.RowH),"已接委托（"+st.active.Count+" / "+AdventureBoard.MaxActive+"）");
     if(st.active.Count==0)LPUI.Unavailable(new Rect(0f,yy,view.width,LPUI.RowH),"还没有接下委托。");
     foreach(var c in st.active.ToList()){DrawActive(new Rect(0f,yy,view.width,56f),c);yy+=62f;}
    }finally{Widgets.EndScrollView();}
   }finally{LPUI.Reset();}
  }
  void DrawOffer(Rect r,AdventureContract c){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(6f);
   var badge=new Rect(inner.x,inner.y,58f,24f);Widgets.DrawBoxSolid(badge,AdventureBoard.DangerColors[c.danger]);var a=Text.Anchor;Text.Anchor=TextAnchor.MiddleCenter;GUI.color=Color.black;Widgets.Label(badge,"危险 "+AdventureBoard.DangerNames[c.danger]);GUI.color=Color.white;Text.Anchor=a;
   float tw=inner.width-200f;
   LPUI.LabelFit(new Rect(inner.x+66f,inner.y+2f,tw-66f,24f),AdventureBoard.Title(c));
   GUI.color=new Color(.85f,.85f,.85f);
   LPUI.LabelFit(new Rect(inner.x,inner.y+28f,tw,22f),"推荐等级 "+c.recLevel+" · 距离约 "+AdventureBoard.Distance(c)+" 格 · 威胁 "+Mathf.RoundToInt(c.points)+" · 剩余 "+Mathf.Max(0f,(c.offerExpires-AdventureBoard.Now)/(float)GenDate.TicksPerDay).ToString("0.#")+" 天");
   LPUI.LabelFit(new Rect(inner.x,inner.y+50f,tw,22f),"敌人："+c.Worker.Enemies(c));
   LPUI.LabelFit(new Rect(inner.x,inner.y+72f,tw,22f),"报酬："+AdventureBoard.RewardText(c));
   GUI.color=Color.white;
   var tip=new Rect(inner.x,inner.y,tw,inner.height);if(Mouse.IsOver(tip))TooltipHandler.TipRegion(tip,AdventureBoard.Describe(c));
   var ok=AdventureBoard.CanAccept(c);var btn=new Rect(inner.xMax-180f,inner.y+30f,180f,LPUI.MainButtonH);string key="o"+c.id;
   if(!ok.Accepted)LPUI.Unavailable(btn,ok.Reason??"");
   else if(LPUI.Button(btn,armed==key?"确定接受？":"接受",true,"点两次确认")){if(armed==key){armed=null;AdventureBoard.Accept(c);}else armed=key;}
  }
  void DrawActive(Rect r,AdventureContract c){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(6f);
   var badge=new Rect(inner.x,inner.y+8f,58f,24f);Widgets.DrawBoxSolid(badge,AdventureBoard.DangerColors[c.danger]);var a=Text.Anchor;Text.Anchor=TextAnchor.MiddleCenter;GUI.color=Color.black;Widgets.Label(badge,AdventureBoard.DangerNames[c.danger]);GUI.color=Color.white;Text.Anchor=a;
   LPUI.LabelFit(new Rect(inner.x+66f,inner.y,inner.width-260f,22f),AdventureBoard.Title(c));
   GUI.color=new Color(.85f,.85f,.85f);LPUI.LabelFit(new Rect(inner.x+66f,inner.y+22f,inner.width-260f,22f),AdventureBoard.Status(c));GUI.color=Color.white;
   var btn=new Rect(inner.xMax-180f,inner.y+4f,180f,LPUI.ButtonH);
   if(c.site!=null && !c.site.Destroyed){if(LPUI.Button(btn,"定位地点")){Close();CameraJumper.TryJumpAndSelect(c.site);}}
   else if(c.target!=null && c.target.Spawned){if(LPUI.Button(btn,"定位目标")){Close();CameraJumper.TryJumpAndSelect(c.target);}}
  }
 }

 // ---------- 设置（冒险者分组） ----------
 public partial class Settings {
  public bool adventureBoard=true;public int adventureInterval=5;public float adventureDifficulty=1f;
  void ExposeAdventureBoard(){ExposeConvenience();Scribe_Values.Look(ref adventureBoard,"lpAdventureBoard",true);Scribe_Values.Look(ref adventureInterval,"lpAdventureInterval",5);Scribe_Values.Look(ref adventureDifficulty,"lpAdventureDifficulty",1f);}
 }
 public static class AdventureBoardSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   ConvenienceSettings.Draw(l); // 1.136.0 头像资源条
   l.CheckboxLabeled("冒险委托板",ref s.adventureBoard,"1.134.0：建筑「冒险者告示板」（杂项，木材或石料 50）每几天贴出 3 条委托（清剿怪物巢穴、讨伐通缉犯、救援被困冒险者、寻回失物、调查闹鬼废墟），显示推荐等级和危险度；接下后世界地图上出现地点，7 天内完成。关闭后不再刷新，已接的照常进行。");
   if(!s.adventureBoard)return;
   Row(l,"　刷新间隔：每 "+s.adventureInterval+" 天",new[]{3f,5f,10f},s.adventureInterval,v=>s.adventureInterval=Mathf.RoundToInt(v),v=>v.ToString("0")+" 天");
   Row(l,"　难度倍率：×"+s.adventureDifficulty.ToString("0.#"),new[]{.5f,1f,1.5f,2f},s.adventureDifficulty,v=>s.adventureDifficulty=v,v=>"×"+v.ToString("0.#"));
  }
  static void Row(Listing_Standard l,string label,float[] vals,float cur,Action<float> set,Func<float,string> text){
   var r=l.GetRect(28f);Widgets.Label(r.LeftPart(.5f),label);var b=LPUI.Row(r.RightPart(.5f),vals.Length,4f);
   for(int i=0;i<vals.Length;i++)if(Widgets.ButtonText(b[i],text(vals[i]),true,true,Mathf.Abs(cur-vals[i])>.001f))set(vals[i]);
  }
 }
}
