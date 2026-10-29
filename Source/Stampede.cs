using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.31.0 事件（第二批）的公共部分：设置开关、“DND 事件频率”总滑条、存档组件、访客标记；以及惊逃兽群与冬狼寒潮。
 // 龙过境在 DragonFlyover.cs，法师塔坠落在 TowerCrash.cs，云游导师在 VPE 桥接（Bridges/VPE/Source/Mentor.cs）。
 public static class Events2 {
  // 测试用：设了就代替随机数（后续 30%、吐息 20%、降落 60%、联姻检定、野魔法 25%）。
  public static float? TestRoll;
  public static float Roll=>TestRoll??Rand.Value;
  public static float Frequency=>(LichMod.Settings?.dndEventFrequency??100)/100f;
  public static Events2State State=>GameComp<Events2State>.Get();
  public static int Now=>Find.TickManager.TicksGame;
  // 社交检定（照 1.28.0 巨灵命令裂口的写法）：殖民地最高社交 × per，上限 cap。
  public static int BestSocial(Map map)=>map==null?0:map.mapPawns.FreeColonistsSpawned.Where(p=>p.skills!=null && !p.skills.GetSkill(SkillDefOf.Social).TotallyDisabled).Select(p=>p.skills.GetSkill(SkillDefOf.Social).Level).DefaultIfEmpty(0).Max();
  public static float SocialChance(Map map,float per=.05f,float cap=.75f)=>Mathf.Min(cap,BestSocial(map)*per);
  // 访客被殖民者打：古龙一行按“拒绝”处理；云游导师离开（桥接程序集往这里登记）。
  public static Action<Pawn,Pawn> MentorHarmed;
  public static void Harmed(Pawn victim,Pawn attacker,string kind){
   try{if(kind=="dragon")DragonFlyover.OnVisitorHarmed(victim,attacker);else if(kind=="mentor")MentorHarmed?.Invoke(victim,attacker);}
   catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 访客受伤处理出错："+e,74120611);}
  }
  public static Hediff_EventVisitor MarkVisitor(Pawn p,string kind){var h=Items.Give(p,"LP_EventVisitor") as Hediff_EventVisitor;if(h!=null)h.kind=kind;return h;}
  // 从地图边上找一个能走到殖民地的格子。
  public static bool EdgeCell(Map map,out IntVec3 cell,IntVec3? near=null){
   bool Ok(IntVec3 c)=>c.Standable(map) && !c.Fogged(map) && map.reachability.CanReachColony(c);
   if(near.HasValue && CellFinder.TryFindRandomEdgeCellNearWith(near.Value,60,map,Ok,out cell))return true;
   return CellFinder.TryFindRandomEdgeCellWith(Ok,map,CellFinder.EdgeRoadChance_Neutral,out cell);
  }
 }

 // 所有新事件都受“DND 事件频率”影响。
 public abstract class IncidentWorker_DND : IncidentWorker {
  public override float ChanceFactorNow(IIncidentTarget target)=>base.ChanceFactorNow(target)*Events2.Frequency;
 }

 // 事件访客的隐藏标记：被殖民者打时通知对应事件。
 public class Hediff_EventVisitor : HediffWithComps {
  public string kind;
  public override bool Visible=>false;
  public override void Notify_PawnPostApplyDamage(DamageInfo dinfo,float totalDamageDealt){
   base.Notify_PawnPostApplyDamage(dinfo,totalDamageDealt);
   if(dinfo.Instigator is Pawn a && a!=pawn && a.Faction==Faction.OfPlayer)Events2.Harmed(pawn,a,kind);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref kind,"lpKind");}
 }

 // ---------- 存档组件 ----------
 public class Events2State : GameComponent {
  public List<StampedeGroup> stampedes=new List<StampedeGroup>();
  public List<StampedeFollowUp> followUps=new List<StampedeFollowUp>();
  public List<ColdSnapPack> coldSnaps=new List<ColdSnapPack>();
  public List<DragonVisit> dragonVisits=new List<DragonVisit>();
  public List<DragonRaid> dragonRaids=new List<DragonRaid>();
  public Events2State(Game game){}
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;
   if(stampedes.Count>0 && now%Stampede.CheckInterval==3)Stampede.Tick(this);
   if(dragonVisits.Count>0 && now%60==17)DragonFlyover.Tick(this);
   if(now%250!=131)return;
   if(followUps.Count>0)Stampede.TickFollowUps(this);
   if(coldSnaps.Count>0)ColdSnap.Tick(this);
   if(dragonRaids.Count>0)DragonFlyover.TickRaids(this);
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref stampedes,"lpStampedes",LookMode.Deep);
   Scribe_Collections.Look(ref followUps,"lpStampedeFollowUps",LookMode.Deep);
   Scribe_Collections.Look(ref coldSnaps,"lpColdSnaps",LookMode.Deep);
   Scribe_Collections.Look(ref dragonVisits,"lpDragonVisits",LookMode.Deep);
   Scribe_Collections.Look(ref dragonRaids,"lpDragonRaids",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    stampedes=stampedes?.Where(x=>x!=null).ToList()??new List<StampedeGroup>();
    followUps=followUps?.Where(x=>x!=null).ToList()??new List<StampedeFollowUp>();
    coldSnaps=coldSnaps?.Where(x=>x!=null).ToList()??new List<ColdSnapPack>();
    dragonVisits=dragonVisits?.Where(x=>x!=null).ToList()??new List<DragonVisit>();
    dragonRaids=dragonRaids?.Where(x=>x!=null).ToList()??new List<DragonRaid>();
   }
  }
 }
 static class ScribePawns {
  public static void Look(ref List<Pawn> list,string label){
   if(Scribe.mode==LoadSaveMode.Saving)list.RemoveAll(p=>p==null || p.Destroyed || p.Discarded);
   Scribe_Collections.Look(ref list,label,LookMode.Reference);
   if(Scribe.mode==LoadSaveMode.PostLoadInit)list=list?.Where(p=>p!=null).ToList()??new List<Pawn>();
  }
 }

 // ==================== 惊逃兽群 ====================
 // 6～14 只动物从地图一边冲向对边（原版迁徙的离图 lord，冲刺速度）。不狂暴；挡在路上的人被撞开 2 格、受 3～6 钝伤（每人每群一次）。
 // 生成时掷一次后续：30% 没有；70% 在 1～3 天后从开着的大事件里等权挑一个（龙过境、巨人迁徙、传送门失控、塞伊袭击，≥3500 点带炎魔）。
 public static class Stampede {
  public const int MinCount=6,MaxCount=14,PushCells=2,CheckInterval=10;
  public const float NoFollowUp=.3f,DamageMin=3f,DamageMax=6f,FollowUpDaysMin=1f,FollowUpDaysMax=3f;
  public static readonly string[] FollowUpKeys={"dragon","migration","portal","thay"};

  // 当前生态区的野生动物（体型 ≥0.5，权重 >0）+ DND 野兽（按 1.25.0 野外刷新开关），都要扛得住当前气温。
  public static List<PawnKindDef> Candidates(Map map){
   if(map==null)return new List<PawnKindDef>();
   var list=map.Biome.AllWildAnimals.Where(k=>k.RaceProps.Animal && k.RaceProps.baseBodySize>=.5f && map.Biome.CommonalityOfAnimal(k)>0f).ToList();
   if(LichMod.Settings.dndBeastsWild)foreach(var k in Beasts.AllKinds())if(!list.Contains(k))list.Add(k);
   return list.Where(k=>map.mapTemperature.SeasonAndOutdoorTemperatureAcceptableFor(k.race,0f)).ToList();
  }
  public static bool CanFire(Map map)=>LichMod.Settings.stampede && map!=null && map.IsPlayerHome && Candidates(map).Count>0;

  public static StampedeGroup Start(Map map,int count=-1,PawnKindDef kind=null){
   var pool=Candidates(map);if(kind==null && pool.Count==0)return null;
   if(!Events2.EdgeCell(map,out var entry))return null;
   var mirror=new IntVec3(map.Size.x-1-entry.x,0,map.Size.z-1-entry.z);
   if(!CellFinder.TryFindRandomEdgeCellNearWith(mirror,40,map,c=>c.Standable(map) && map.reachability.CanReach(entry,c,PathEndMode.OnCell,TraverseMode.PassDoors,Danger.Deadly),out var exit))exit=mirror.ClampInsideMap(map);
   int n=count>0?count:Rand.RangeInclusive(MinCount,MaxCount);
   var kinds=new List<PawnKindDef>{kind??pool.RandomElement()};
   if(kind==null && pool.Count>1 && Rand.Chance(.3f))kinds.Add(pool.Where(k=>k!=kinds[0]).RandomElement());
   var pawns=new List<Pawn>();
   for(int i=0;i<n;i++){var p=PawnGenerator.GeneratePawn(kinds[i%kinds.Count],null);GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,6),map);pawns.Add(p);}
   var lord=LordMaker.MakeNewLord(null,new LordJob_ExitMapNear(exit,LocomotionUrgency.Sprint,12f,false,false),map,pawns);
   var g=new StampedeGroup{map=map,lord=lord,pawns=pawns,dir=(exit-entry).ToVector3(),kinds=kinds.Select(k=>k.label).ToList()};
   Events2.State?.stampedes.Add(g);
   return g;
  }
  public static Vector3 Direction(Pawn a,StampedeGroup g){
   var pf=a.pather;
   if(pf!=null && pf.Moving && pf.nextCell.IsValid && pf.nextCell!=a.Position)return (pf.nextCell-a.Position).ToVector3();
   return g.dir;
  }
  // 撞人：动物身边（含所在格）站着的人形角色，没被这群撞过的，沿动物前进方向推开 2 格并受 3～6 钝伤。
  public static int Collide(StampedeGroup g){
   var map=g.map;int n=0;if(map==null)return 0;
   foreach(var a in g.pawns.ToList()){
    if(a==null || a.Dead || a.Downed || !a.Spawned || a.Map!=map)continue;
    var dir=Direction(a,g);
    foreach(var c in GenAdj.CellsAdjacent8Way(a).Concat(new[]{a.Position}).ToList()){
     if(!c.InBounds(map))continue;
     foreach(var v in c.GetThingList(map).OfType<Pawn>().ToList()){
      if(v==a || !v.RaceProps.Humanlike || v.Dead || v.Downed || g.hit.Contains(v))continue;
      Hit(g,a,v,dir);n++;
     }
    }
   }
   return n;
  }
  public static void Hit(StampedeGroup g,Pawn animal,Pawn victim,Vector3 dir){
   g.hit.Add(victim);
   DNDPush.Move(victim,DNDPush.Destination(g.map,victim.Position,dir,PushCells));
   g.lastDamage=Rand.Range(DamageMin,DamageMax);
   victim.TakeDamage(new DamageInfo(DamageDefOf.Blunt,g.lastDamage,0f,-1f,animal));
   Items.Mote(victim,"被撞开了！",new Color(.95f,.8f,.5f));
  }
  public static void Tick(Events2State s){
   for(int i=s.stampedes.Count-1;i>=0;i--){
    var g=s.stampedes[i];
    if(g.map==null || !Find.Maps.Contains(g.map) || !g.pawns.Any(p=>p!=null && p.Spawned && !p.Dead && p.Map==g.map)){s.stampedes.RemoveAt(i);continue;}
    Collide(g);
   }
  }

  // ---------- 后续 ----------
  public static bool FollowUpEnabled(string key,Map map){
   if(map==null)return false;
   switch(key){
    case "dragon":return LichMod.Settings.dragonFlyover && DragonFlyover.ConditionDef!=null && !map.gameConditionManager.ConditionIsActive(DragonFlyover.ConditionDef);
    case "migration":return GiantMigration.CanFire(map);
    case "portal":return LichMod.Settings.portalBreach && PortalEvent.RiftDef!=null && !PortalEvent.Rifts(map).Any();
    case "thay":{var f=Factions.Hostile;return f!=null && !f.defeated && Factions.Enabled(f) && f.HostileTo(Faction.OfPlayer);}
   }
   return false;
  }
  public static List<string> EnabledFollowUps(Map map)=>FollowUpKeys.Where(k=>FollowUpEnabled(k,map)).ToList();
  public static string FollowUpLabel(string key)=>key=="dragon"?"龙过境":key=="migration"?"巨人迁徙":key=="portal"?"传送门失控":key=="thay"?"塞伊袭击":key;
  public static StampedeFollowUp RollFollowUp(Map map){
   if(Events2.Roll<NoFollowUp)return null;
   var options=EnabledFollowUps(map);if(options.Count==0)return null;
   var f=new StampedeFollowUp{key=options.RandomElement(),map=map,tick=Events2.Now+(int)(Rand.Range(FollowUpDaysMin,FollowUpDaysMax)*GenDate.TicksPerDay)};
   Events2.State?.followUps.Add(f);
   return f;
  }
  public static void TickFollowUps(Events2State s){
   int now=Events2.Now;
   for(int i=s.followUps.Count-1;i>=0;i--){var f=s.followUps[i];if(now<f.tick)continue;s.followUps.RemoveAt(i);Fire(f);}
  }
  // 到点时再看一次：原定的事件被关掉了就从还开着的里面重挑，全关了就作罢。
  public static bool Fire(StampedeFollowUp f){
   var map=f.map;if(map==null || !Find.Maps.Contains(map))return false;
   var key=f.key;
   if(!FollowUpEnabled(key,map)){var options=EnabledFollowUps(map);if(options.Count==0)return false;key=options.RandomElement();}
   f.fired=key;
   if(key=="thay")return ThayRaid(map,StorytellerUtility.DefaultThreatPointsNow(map))!=null;
   var def=DefDatabase<IncidentDef>.GetNamedSilentFail(key=="dragon"?"LP_DragonFlyover":key=="migration"?"LP_GiantMigration":"LP_PortalBreach");
   if(def==null)return false;
   var parms=StorytellerUtility.DefaultParmsNow(def.category,map);parms.forced=true;
   return def.Worker.TryExecute(parms);
  }
  // 塞伊袭击：点数 ≥3500 时保证有一只炎魔（原版生成没抽到就补一只进同一个 lord）。forced：不被竖琴手推迟。
  public static Lord ThayRaid(Map map,float points){
   var f=Factions.Hostile;if(f==null || map==null)return null;
   var parms=new IncidentParms{target=map,faction=f,points=points,forced=true,raidStrategy=RaidStrategyDefOf.ImmediateAttack,raidArrivalMode=PawnsArrivalModeDefOf.EdgeWalkIn};
   var before=new HashSet<Lord>(map.lordManager.lords);
   // 走进来的落点偶尔找不到：再试一次，让原版自己挑到达方式和策略。
   if(!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms) && !IncidentDefOf.RaidEnemy.Worker.TryExecute(new IncidentParms{target=map,faction=f,points=points,forced=true}))return null;
   var lord=map.lordManager.lords.FirstOrDefault(l=>!before.Contains(l) && l.faction==f);
   if(lord!=null && points>=Fiends.BalorMinPoints && Fiends.BalorKind!=null && !lord.ownedPawns.Any(Fiends.IsBalor) && lord.ownedPawns.Count>0){
    var b=PawnGenerator.GeneratePawn(new PawnGenerationRequest(Fiends.BalorKind,f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true));AdventurerEnemies.LevelLoose(new[]{b});
    GenSpawn.Spawn(b,CellFinder.RandomClosewalkCellNear(lord.ownedPawns[0].Position,map,4),map);lord.AddPawn(b);
   }
   return lord;
  }
 }
 public class StampedeGroup : IExposable {
  public Map map;public Lord lord;public Vector3 dir;public List<Pawn> pawns=new List<Pawn>(),hit=new List<Pawn>();public List<string> kinds=new List<string>();public float lastDamage; // 只给检查用，不存档
  public void ExposeData(){
   Scribe_References.Look(ref map,"map");Scribe_References.Look(ref lord,"lord");Scribe_Values.Look(ref dir,"dir");
   ScribePawns.Look(ref pawns,"pawns");ScribePawns.Look(ref hit,"hit");
   Scribe_Collections.Look(ref kinds,"kinds",LookMode.Value);if(Scribe.mode==LoadSaveMode.PostLoadInit && kinds==null)kinds=new List<string>();
  }
 }
 public class StampedeFollowUp : IExposable {
  public string key,fired;public int tick;public Map map;
  public void ExposeData(){Scribe_Values.Look(ref key,"key");Scribe_Values.Look(ref tick,"tick");Scribe_References.Look(ref map,"map");}
 }
 public class IncidentWorker_Stampede : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms)=>Stampede.CanFire(parms.target as Map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;if(!Stampede.CanFire(map))return false;
   var g=Stampede.Start(map);if(g==null)return false;
   Stampede.RollFollowUp(map);
   SendStandardLetter("惊逃的兽群","一群"+string.Join("和",g.kinds)+"（"+g.pawns.Count+"只）惊慌失措地从地图一边冲向另一边。它们不会主动攻击，但挡在路上的人会被撞开。\n\n它们看上去不像是在迁徙，倒像是在逃避什么……",LetterDefOf.NeutralEvent,parms,new LookTargets(g.pawns.Cast<Thing>()));
   return true;
  }
 }

 // ==================== 冬狼寒潮 ====================
 // 原版寒潮 5 天 + 4～8 只狂暴冬狼（按袭击点数裁剪）。这批冬狼带“寒潮狂性”（移速 +20%，冰咬的寒霜迟缓 ×2）；寒潮结束时狂性消退、退出狂暴，变成普通野生冬狼。
 public static class ColdSnap {
  public const int Days=5,MinWolves=4,MaxWolves=8;
  public const float MaxTemp=10f;
  public const string Frenzy="LP_ColdSnapFrenzy";
  public static readonly HashSet<string> ColdBiomes=new HashSet<string>{"BorealForest","Tundra","IceSheet","SeaIce","ColdBog"};
  public static PawnKindDef Wolf=>DefDatabase<PawnKindDef>.GetNamedSilentFail(FrostGiant.WinterWolf);
  public static bool ColdEnough(float temp,string biome)=>temp<MaxTemp || (biome!=null && ColdBiomes.Contains(biome));
  public static bool ColdEnough(Map map)=>map!=null && ColdEnough(map.mapTemperature.OutdoorTemp,map.Biome?.defName);
  public static bool CanFire(Map map)=>LichMod.Settings.winterWolfColdSnap && map!=null && map.IsPlayerHome && Wolf!=null && ColdEnough(map);
  public static int WolfCount(float points,int rolled)=>Mathf.Clamp(Mathf.FloorToInt(points/Mathf.Max(1f,Wolf?.combatPower??120f)),1,rolled);
  public static ColdSnapPack Start(Map map,float points,int rolled=-1){
   var wolf=Wolf;if(wolf==null || map==null)return null;
   int n=WolfCount(points,rolled>0?rolled:Rand.RangeInclusive(MinWolves,MaxWolves));
   int dur=Days*GenDate.TicksPerDay;
   var mgr=map.gameConditionManager;var cond=mgr.GetActiveCondition(GameConditionDefOf.ColdSnap);
   if(cond==null){cond=GameConditionMaker.MakeCondition(GameConditionDefOf.ColdSnap,dur);mgr.RegisterCondition(cond);}
   else if(!cond.Permanent && cond.TicksLeft<dur)cond.Duration=cond.TicksPassed+dur;
   if(!RCellFinder.TryFindRandomPawnEntryCell(out var entry,map,CellFinder.EdgeRoadChance_Animal))entry=CellFinder.RandomEdgeCell(map);
   var wolves=new List<Pawn>();
   for(int i=0;i<n;i++){
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(wolf,null,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true));
    GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,6),map);
    p.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true,false,false,null,true);
    Items.Give(p,Frenzy);wolves.Add(p);
   }
   var pack=new ColdSnapPack{map=map,wolves=wolves,endTick=Events2.Now+dur};
   Events2.State?.coldSnaps.Add(pack);
   return pack;
  }
  public static void Tick(Events2State s){
   int now=Events2.Now;
   for(int i=s.coldSnaps.Count-1;i>=0;i--){
    var p=s.coldSnaps[i];
    bool gone=p.map==null || !Find.Maps.Contains(p.map);
    if(gone || now>=p.endTick || !p.map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ColdSnap)){if(!gone)End(p);s.coldSnaps.RemoveAt(i);}
   }
  }
  // 寒潮结束：去掉狂性，退出狂暴，变成普通野生冬狼（和 1.28.0 裂口关闭时同一套平静逻辑）。
  public static int End(ColdSnapPack p){
   foreach(var w in p.wolves)if(w!=null && !w.Destroyed)Items.Remove(w,Frenzy);
   int n=CompPlanarRift.CalmBeasts(p.wolves);
   if(n>0 && p.map!=null)Messages.Message("寒潮过去了，"+n+"只冬狼的狂性消退，变成了普通的野生冬狼，可以驯服。",new LookTargets(p.wolves.Where(w=>w!=null && w.Spawned).Cast<Thing>()),MessageTypeDefOf.NeutralEvent,false);
   return n;
  }
 }
 public class ColdSnapPack : IExposable {
  public Map map;public int endTick;public List<Pawn> wolves=new List<Pawn>();
  public void ExposeData(){Scribe_References.Look(ref map,"map");Scribe_Values.Look(ref endTick,"endTick");ScribePawns.Look(ref wolves,"wolves");}
 }
 public class IncidentWorker_WinterWolfColdSnap : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms)=>ColdSnap.CanFire(parms.target as Map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;if(!ColdSnap.CanFire(map))return false;
   var pack=ColdSnap.Start(map,parms.points);if(pack==null || pack.wolves.Count==0)return false;
   SendStandardLetter("冬狼寒潮","一股寒潮压了下来，接下来的"+ColdSnap.Days+"天里气温会骤降。寒潮里来了"+pack.wolves.Count+"只狂暴的冬狼！\n\n寒潮让它们狂性大发：移速更快，冰咬的寒霜迟缓持续更久。寒潮结束时，活下来的冬狼会恢复平静，变成可以驯服的野生冬狼。\n\n火巨人不受寒潮影响。",LetterDefOf.ThreatBig,parms,new LookTargets(pack.wolves.Cast<Thing>()));
   return true;
  }
 }

 // 1.31.0 设置：五个新事件的开关与“DND 事件频率”总滑条（在 1.28.0 的“事件”分组里）。
 public partial class Settings {
  public bool stampede=true,winterWolfColdSnap=true,dragonFlyover=true,towerCrash=true,wanderingMentor=true;
  public int dndEventFrequency=100;
  void ExposeEvents2(){
   Scribe_Values.Look(ref stampede,"lpStampede",true);Scribe_Values.Look(ref winterWolfColdSnap,"lpWinterWolfColdSnap",true);Scribe_Values.Look(ref dragonFlyover,"lpDragonFlyover",true);
   Scribe_Values.Look(ref towerCrash,"lpTowerCrash",true);Scribe_Values.Look(ref wanderingMentor,"lpWanderingMentor",true);
   Scribe_Values.Look(ref dndEventFrequency,"lpDndEventFrequency",100);dndEventFrequency=Math.Max(0,Math.Min(300,dndEventFrequency));
  }
 }
 public static class Events2Settings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.Label("DND 事件频率："+s.dndEventFrequency+"%（传送门失控、巨人迁徙和下面五个事件的出现权重；0%等于全部不出现）");
   s.dndEventFrequency=(int)l.Slider(s.dndEventFrequency,0,300);
   l.CheckboxLabeled("惊逃兽群",ref s.stampede,"6～14只动物惊慌地横穿地图，撞开挡路的人。70%的情况下1～3天后还有大事发生（龙过境、巨人迁徙、传送门失控或塞伊袭击，只从开着的里面挑）。");
   l.CheckboxLabeled("冬狼寒潮",ref s.winterWolfColdSnap,"冷的季节或寒带地图：5天寒潮，外加4～8只狂暴冬狼（按袭击点数）。寒潮结束时冬狼恢复平静，可以驯服。");
   l.CheckboxLabeled("龙过境",ref s.dragonFlyover,"一条龙在天上盘旋1天：龙影掠过时野兽逃散，心情-3（力能龙裔+3），偶尔有吐息落地。殖民地有成年力能龙裔时，另一条古龙可能降落：决斗、联姻或拒绝。");
   l.CheckboxLabeled("法师塔坠落",ref s.towerCrash,"塞伊法师塔的残骸坠落在殖民地外，骷髅守卫看着，周围野魔法四溢。可以自己拆、等塞伊来回收，或通知同盟来拆。需要塞伊远征军存在。");
   l.CheckboxLabeled("云游导师（需要 VPE）",ref s.wanderingMentor,"殖民地有DND职业路径的人时，一位同路径的导师来住3天，每天教一次（不受每日经验上限限制）；付钱或卷轴可以再留3天。只在启用 Vanilla Psycasts Expanded 时出现。");
  }
 }
}
