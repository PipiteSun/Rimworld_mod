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
 // 1.60.0 迷锁地牢（设计见 dnd-genes/DND地牢构思二.md 丙线，取舍见 ArtSource/Mythal-1.60.0-report.md）：
 // 出生点旁一座迷锁之门（原版口袋地图 MapPortal，本体功能，不需要异象），一层一层往下打，没有尽头。
 // 门的状态：沉睡 → 开启（第 N 层）→ 已解开（守护者倒下，回程门出现）→ 所有人离开后坍塌 → 封闭 3 天 → 开启第 N+1 层。
 // 状态都存在门上，门每 250 刻看一次口袋地图（规则、停留时间、守护者、撤退回卷、全员倒地驱逐）；封闭时没有口袋地图。

 public class MythalLoot {
  public string def;public IntRange count=new IntRange(1,1);public float chance=1f;
 }
 public class MythalThemeDef : Def {
  public int minFloor=1,maxFloor=9999;
  public string floorTerrain="FlagstoneGranite";
  public List<string> monsters=new List<string>(),guardians=new List<string>();
  public List<MythalLoot> loot=new List<MythalLoot>();
  public IEnumerable<PawnKindDef> Monsters=>monsters.Select(Mythal.Kind).Where(k=>k!=null);
  public PawnKindDef Guardian=>guardians.Select(Mythal.Kind).FirstOrDefault(k=>k!=null)??Monsters.OrderByDescending(k=>k.combatPower).FirstOrDefault();
  public bool Usable=>Monsters.Any();
 }
 public class MythalRuleDef : Def {
  public HediffDef hediff;public int minFloor=5;public float weight=1f;public bool playerOnly;
 }

 public static class Mythal {
  public const int StateDormant=0,StateOpen=1,StateCleared=2,StateSealed=3;
  public const int TickInterval=250,FullShareTicks=2500,RewindTicks=5*GenDate.TicksPerDay,ExpelTicks=6*GenDate.TicksPerHour,OldSaveOpenTicks=GenDate.TicksPerDay;
  public const float GuardianShare=.3f,EmpoweredExtra=.2f,FloorXpShare=.5f,PartialShare=.25f;
  public static readonly DefRef<ThingDef> GateDef=new DefRef<ThingDef>("LP_MythalGate"),ExitDef=new DefRef<ThingDef>("LP_MythalExit"),WallDef=new DefRef<ThingDef>("LP_MythalWall"),LampDef=new DefRef<ThingDef>("LP_MythalLamp"),FountainDef=new DefRef<ThingDef>("LP_MythalFountain"),ShardDef=new DefRef<ThingDef>("LP_MythalShard");
  public static readonly DefRef<MapGeneratorDef> FloorGen=new DefRef<MapGeneratorDef>("LP_MythalFloor");
  public static readonly DefRef<ThoughtDef> ExpelledThought=new DefRef<ThoughtDef>("LP_MythalExpelled");
  public static bool Enabled=>LichMod.Settings?.mythalEnabled??true;
  public static bool IsekaiActive=>ModsConfig.IsActive(Adventurer.IsekaiId);
  public static PawnKindDef Kind(string name)=>name.NullOrEmpty()?null:DefDatabase<PawnKindDef>.GetNamedSilentFail(name);
  public static bool IsFloorMap(Map m)=>m!=null && FloorGen.Def!=null && m.generatorDef==FloorGen.Def;
  public static IEnumerable<Building_MythalGate> Gates=>Find.Maps.SelectMany(m=>m.listerThings.ThingsOfDef(GateDef.Def)).OfType<Building_MythalGate>();

  // ---------- 数值 ----------
  // 危险点数 = (150 + 层数 × 35) × 迷锁难度；装了 Isekai 再 × Isekai 点数系数（它会给新生成的敌人再加一次强度）。前 5 层不超过殖民地当前袭击点数（至少 100）。
  public static float BasePoints(int floor)=>(150f+35f*Math.Max(1,floor))*(LichMod.Settings?.mythalDifficulty??100)/100f;
  public static float IsekaiFactor=>IsekaiActive?(LichMod.Settings?.mythalIsekaiPoints??60)/100f:1f;
  public static float Points(int floor,Map home){
   float p=BasePoints(floor)*IsekaiFactor;
   if(floor<=5 && home!=null){float threat=StorytellerUtility.DefaultThreatPointsNow(home);p=Math.Min(p,Math.Max(100f,threat));}
   return p;
  }
  public static int LevelFloor(int floor)=>Mathf.Clamp(floor/3,1,40);
  public static float ClearXP(int floor)=>BasePoints(floor)*FloorXpShare*(LichMod.Settings?.mythalXp??100)/100f;
  public static float MilestoneXP(int floor)=>400f+20f*floor;
  public static int Shards(int floor)=>(5+floor/5)*(floor>=70?2:1);
  public static bool Milestone(int floor)=>floor>0 && floor%10==0;
  public static bool Empowered(int floor)=>floor>0 && floor%5==0 && !Milestone(floor);
  public static int MapSize(int floor)=>floor>=30?90:70;
  public static int SealTicks=>Mathf.Clamp(LichMod.Settings?.mythalSealDays??3,1,10)*GenDate.TicksPerDay;

  // ---------- 主题、规则 ----------
  public static MythalThemeDef RollTheme(int floor){
   var all=DefDatabase<MythalThemeDef>.AllDefsListForReading.Where(t=>t.Usable).ToList();
   var fit=all.Where(t=>floor>=t.minFloor && floor<=t.maxFloor).ToList();
   if(fit.Count==0)fit=all;
   return fit.Count==0?null:fit.RandomElement();
  }
  public static MythalRuleDef RollRule(int floor,MythalRuleDef not=null){
   var none=DefDatabase<MythalRuleDef>.GetNamedSilentFail("LP_MythalRule_None");
   if(floor<5)return none;
   var pool=DefDatabase<MythalRuleDef>.AllDefsListForReading.Where(r=>floor>=r.minFloor && r!=not).ToList();
   return pool.Count==0?none:pool.RandomElementByWeight(r=>r.weight);
  }
  // 每 10 层的里程碑首领：每组按顺序找第一个存在的兵种（不装 HAR 时五色龙、眼魔不存在，退到后面的替身）；第 70 层起轮回。
  public static readonly string[][] Bosses={
   new[]{"LP_RedDragon_Young","LP_BlueDragon_Young","LP_GreenDragon_Young","LP_BlackDragon_Young","LP_WhiteDragon_Young","|","LP_WildDragon"},
   new[]{"LP_FH_RedWizard"},
   new[]{"LP_LM_DeathKnight","|","LP_FH_Skeleton"},
   new[]{"LP_RedDragon_Adult","LP_BlueDragon_Adult","LP_GreenDragon_Adult","LP_BlackDragon_Adult","LP_WhiteDragon_Adult","|","LP_WildDragon"},
   new[]{"LP_LM_Beholder","|","LP_IC_MindFlayer"},
   new[]{"LP_FH_Balor","|","LP_AH_Hezrou"},
  };
  public static readonly string[] BossTitles={"龙祭司的真龙","塞伊红袍大法师","死亡骑士","成年真龙","眼魔","深渊领主"};
  public static int BossIndex(int floor)=>(floor/10-1)%Bosses.Length;
  public static PawnKindDef BossKind(int floor){
   var g=Bosses[BossIndex(floor)];int bar=Array.IndexOf(g,"|");
   var first=(bar<0?g:g.Take(bar)).Select(Kind).Where(k=>k!=null).ToList();
   if(first.Count>0)return first.RandomElement();
   return bar<0?null:g.Skip(bar+1).Select(Kind).FirstOrDefault(k=>k!=null);
  }

  // ---------- 敌人 ----------
  public static Pawn MakeMonster(PawnKindDef kind){
   var f=Faction.OfAncientsHostile;
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,kind.RaceProps.Humanlike?f:null,PawnGenerationContext.NonPlayer,null,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:kind.RaceProps.Humanlike));
   if(p.Faction!=f)p.SetFaction(f);
   return p;
  }
  // 敌人等级：普通怪 = 层数 ÷ 3（+0～2）；守护者再 +3，里程碑首领 +6，走剧情等级（20 级以上按传奇等级）。冒险者等级关闭或自测抑制时什么都不做。
  public static void Level(Pawn p,int floor,int bonus,bool boss){
   if(!AdventurerEnemies.On || p==null || !p.RaceProps.Humanlike)return;
   int lv=LevelFloor(floor)+bonus;
   if(boss){AdventurerEnemies.Story(p,lv);return;}
   var h=Adventurer.Get(p)??Adventurer.TryAdd(p);if(h==null)return;
   h.storyLevel=true;Legend.RaiseEnemy(h,lv+Rand.RangeInclusive(0,2));
  }

  // ---------- 奖励 ----------
  static readonly string[] Scrolls={"LP_ScrollFireball","LP_ScrollTeleport","LP_ScrollProtectionEvil","LP_ScrollTrueSeeing"};
  public static ThingDef Thing(string name)=>name.NullOrEmpty()?null:DefDatabase<ThingDef>.GetNamedSilentFail(name);
  public static string RandomMagicItem(){var l=IsekaiLoot.MagicLoot.Where(n=>Thing(n)!=null).ToList();return l.Count==0?null:l.RandomElement();}
  public static List<Thing> RollLoot(MythalThemeDef theme,int floor,bool guardian){
   var list=new List<Thing>();float mult=1f+floor/10f;
   void Add(ThingDef d,int n){if(d==null || n<=0)return;while(n>0){var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.stackCount=Math.Min(n,d.stackLimit);n-=t.stackCount;list.Add(t);}}
   if(theme!=null)foreach(var l in theme.loot)if(Rand.Chance(l.chance))Add(Thing(l.def),Mathf.CeilToInt(l.count.RandomInRange*(Thing(l.def)?.stackLimit>1?mult:1f)));
   Add(ThingDefOf.Silver,Mathf.RoundToInt((guardian?60:25)*mult));
   if(guardian){
    Add(Thing(Scrolls.RandomElement()),1);
    if(Milestone(floor) || Rand.Chance(Mathf.Min(.9f,.25f+floor*.01f)))Add(Thing(RandomMagicItem()),1);
   }else if(Rand.Chance(.3f))Add(Thing("LP_PotionHealing"),1);
   return list;
  }
  public static void Drop(IEnumerable<Thing> things,IntVec3 cell,Map map){foreach(var t in things)GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);}

  // 碎片兑换（门上，任何时候都能换；重铸规则只在下一层还没生成时可用）。
  public class Offer {public string key,label,def;public int count,price;}
  public static readonly Offer[] Offers={
   new Offer{key="potion",label="治疗药水",def="LP_PotionHealing",count=1,price=3},
   new Offer{key="medicine",label="工业药品 ×5",def="MedicineIndustrial",count=5,price=4},
   new Offer{key="components",label="零部件 ×5",def="ComponentIndustrial",count=5,price=5},
   new Offer{key="greater",label="强效治疗药水",def="LP_PotionGreaterHealing",count=1,price=8},
   new Offer{key="scroll",label="随机法术卷轴",count=1,price=6},
   new Offer{key="spacer",label="高级零部件",def="ComponentSpacer",count=1,price=12},
   new Offer{key="magic",label="随机魔法物品",count=1,price=30},
   new Offer{key="stone",label="传奇重铸之石",def=Legend.StoneDef,count=1,price=40},
   new Offer{key="reroll",label="重铸下一层的迷锁规则",count=0,price=5},
  };
  public static int ShardsOn(Map map)=>map==null || ShardDef.Def==null?0:map.listerThings.ThingsOfDef(ShardDef.Def).Where(t=>t.Spawned).Sum(t=>t.stackCount);
  public static bool Pay(Map map,int price){
   if(ShardsOn(map)<price)return false;
   foreach(var t in map.listerThings.ThingsOfDef(ShardDef.Def).Where(t=>t.Spawned).ToList()){if(price<=0)break;int n=Math.Min(price,t.stackCount);t.SplitOff(n).Destroy();price-=n;}
   return true;
  }

  // ---------- Isekai 经验桥（反射；找不到方法时只给冒险者经验） ----------
  static System.Reflection.MethodInfo isekaiGain;static Type isekaiComp;static bool isekaiLooked;
  public static bool GiveIsekaiXP(Pawn p,float amount){
   if(!IsekaiActive || !(LichMod.Settings?.mythalIsekaiXp??true) || p==null || amount<1f)return false;
   if(!isekaiLooked){isekaiLooked=true;isekaiComp=GenTypes.GetTypeInAnyAssembly("IsekaiLeveling.IsekaiComponent");if(isekaiComp!=null)isekaiGain=HarmonyLib.AccessTools.Method(isekaiComp,"GainXP",new[]{typeof(int),typeof(string)});}
   if(isekaiGain==null)return false;
   var comp=p.AllComps?.FirstOrDefault(c=>isekaiComp.IsInstanceOfType(c));if(comp==null)return false;
   try{isekaiGain.Invoke(comp,new object[]{Mathf.RoundToInt(amount),"迷锁地牢"});return true;}catch(Exception e){Log.WarningOnce("[DND边缘大陆] 迷锁地牢：Isekai 经验桥失败："+e.Message,0x4D59);return false;}
  }

  // ---------- 放门 ----------
  // 新开局：殖民者附近 15～30 格，避开水和山体，周围 7×7 清成平地。找不到时放宽到 45 格。
  public static Building_MythalGate TryPlaceGate(Map map,IntVec3 near){
   if(map==null || GateDef.Def==null)return null;
   var existing=map.listerThings.ThingsOfDef(GateDef.Def).OfType<Building_MythalGate>().FirstOrDefault();if(existing!=null)return existing;
   bool Ok(IntVec3 c){
    var r=CellRect.CenteredOn(c,3);if(!r.InBounds(map) || r.minX<6 || r.minZ<6 || r.maxX>map.Size.x-7 || r.maxZ>map.Size.z-7)return false;
    foreach(var x in r){if(!x.Standable(map) && x.GetEdifice(map)==null || x.Roofed(map) || x.GetTerrain(map).IsWater || !x.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Light))return false;var b=x.GetEdifice(map);if(b!=null && !b.def.destroyable)return false;if(b!=null && b.Faction==Faction.OfPlayer)return false;foreach(var t in x.GetThingList(map))if(t is Pawn || t.def.category==ThingCategory.Building && t.Faction==Faction.OfPlayer)return false;}
    return true;
   }
   IntVec3 found=IntVec3.Invalid;
   foreach(var (lo,hi) in new[]{(15,30),(8,45)}){
    var cells=GenRadial.RadialCellsAround(near,hi,true).Where(c=>c.InBounds(map) && c.DistanceTo(near)>=lo).InRandomOrder().ToList();
    foreach(var c in cells)if(Ok(c)){found=c;break;}
    if(found.IsValid)break;
   }
   if(!found.IsValid)return null;
   foreach(var x in CellRect.CenteredOn(found,3))if(x.InBounds(map))foreach(var t in x.GetThingList(map).ToList())if(t.def.destroyable && !(t is Pawn))t.Destroy();
   var gate=(Building_MythalGate)ThingMaker.MakeThing(GateDef.Def);
   GenSpawn.Spawn(gate,found,map);
   return gate;
  }
  public static IntVec3 ColonyCenter(Map map){
   var ps=map.mapPawns.FreeColonistsSpawned;if(ps.Count==0)return map.Center;
   return new IntVec3((int)ps.Average(p=>p.Position.x),0,(int)ps.Average(p=>p.Position.z));
  }

  // ---------- 搬人 ----------
  public static void MoveTo(Pawn p,Map map,IntVec3 near){
   if(p==null || map==null)return;
   if(p.Spawned)p.DeSpawn();
   if(!CellFinder.TryFindRandomCellNear(near,map,6,c=>c.Standable(map) && !c.Fogged(map),out var cell))cell=near;
   GenSpawn.Spawn(p,cell,map);
  }
 }

 // ---------- 迷锁之门 ----------
 public class Building_MythalGate : MapPortal {
  public int state=Mythal.StateDormant,floor=1,best,openTick=-1,sealUntil=-1,lastVisitTick=-1,downedSince=-1,generatedFloor;
  public MythalThemeDef theme;public MythalRuleDef rule;
  public List<Pawn> guardians=new List<Pawn>(),healed=new List<Pawn>();
  public Dictionary<Pawn,int> timeInside=new Dictionary<Pawn,int>();
  public IntVec3 rewardCell=IntVec3.Invalid,entryCell=IntVec3.Invalid;
  List<Pawn> tmpPawns;List<int> tmpTicks;
  static Graphic sealedGraphic;

  public bool Sealed=>state==Mythal.StateSealed;
  public float Points=>Mythal.Points(floor,Map);
  public string StateLabel=>state==Mythal.StateDormant?"沉睡":state==Mythal.StateOpen?"开启":state==Mythal.StateCleared?"已解开":"封闭";

  public override void SpawnSetup(Map map,bool respawningAfterLoad){
   base.SpawnSetup(map,respawningAfterLoad);
   if(!respawningAfterLoad && openTick<0){
    int day=LichMod.Settings?.mythalFirstDay??5;
    if(Find.Scenario?.name=="冒险者小队")day=Math.Min(day,1); // 冒险者小队开局第 1 天就开
    openTick=Math.Max(Find.TickManager.TicksGame+(Find.TickManager.TicksGame>GenDate.TicksPerDay*2?Mythal.OldSaveOpenTicks:0),day*GenDate.TicksPerDay);
   }
  }
  public override Graphic Graphic{get{
   if(state==Mythal.StateOpen || state==Mythal.StateCleared)return base.Graphic;
   return sealedGraphic??(sealedGraphic=GraphicDatabase.Get<Graphic_Single>("LP/Mythal/GateSealed",ShaderDatabase.Cutout,def.graphicData.drawSize,Color.white));
  }}

  public override bool IsEnterable(out string reason){
   if(!Mythal.Enabled){reason="迷锁地牢已在设置里关闭";return false;}
   if(state==Mythal.StateDormant){reason="迷锁尚未苏醒（还剩 "+((openTick-Find.TickManager.TicksGame)/(float)GenDate.TicksPerDay).ToString("0.0")+" 天）";return false;}
   if(state==Mythal.StateSealed){reason="迷锁正在重组（还剩 "+((sealUntil-Find.TickManager.TicksGame)/(float)GenDate.TicksPerDay).ToString("0.0")+" 天）";return false;}
   return base.IsEnterable(out reason);
  }
  protected override Map GeneratePocketMapInt(){
   if(theme==null)theme=Mythal.RollTheme(floor);if(rule==null)rule=Mythal.RollRule(floor);
   guardians.Clear();healed.Clear();rewardCell=entryCell=IntVec3.Invalid;generatedFloor=floor;downedSince=-1;lastVisitTick=Find.TickManager.TicksGame;
   int s=Mythal.MapSize(floor);
   return PocketMapUtility.GeneratePocketMap(new IntVec3(s,1,s),def.portal.pocketMapGenerator,GetExtraGenSteps(),Map);
  }

  protected override void Tick(){
   base.Tick();
   if(this.IsHashIntervalTick(Mythal.TickInterval))MythalTick();
  }
  public void MythalTick(){
   if(!Mythal.Enabled || !Spawned)return;
   int now=Find.TickManager.TicksGame;
   if(state==Mythal.StateDormant){if(now>=openTick)Open(true);return;}
   if(state==Mythal.StateSealed){if(now>=sealUntil)Open(true);return;}
   var pm=PocketMap;if(pm==null)return;
   var players=pm.mapPawns.AllPawnsSpawned.Where(p=>p.Faction==Faction.OfPlayer).ToList();
   foreach(var p in players){if(p.RaceProps.Humanlike && p.IsColonist){timeInside.TryGetValue(p,out int t);timeInside[p]=t+Mythal.TickInterval;}}
   if(rule?.hediff!=null)foreach(var p in pm.mapPawns.AllPawnsSpawned)ApplyRule(p);
   Fountains(pm,players);
   if(players.Count>0)lastVisitTick=now;
   if(state==Mythal.StateOpen && guardians.Count>0 && guardians.All(g=>g==null || g.Dead || g.Downed || g.Destroyed || g.MapHeld!=pm)){Clear();return;}
   // 全员倒地：6 小时后被迷锁吐回门口（不死），这一层回卷；已解开的层照常坍塌。
   var standing=players.Where(p=>!p.Downed && !p.Dead).ToList();
   if(players.Count>0 && standing.Count==0){if(downedSince==-1)downedSince=now;else if(now-downedSince>=Mythal.ExpelTicks){Expel(players);return;}}
   else downedSince=-1;
   if(players.Count==0){
    if(state==Mythal.StateCleared){Collapse();return;}
    if(now-lastVisitTick>=Mythal.RewindTicks){Rewind("这一层无人进入已满 5 天，迷锁把它重新编织了一遍：怪物满血，布局改变。");return;}
   }
  }
  public void ApplyRule(Pawn p){
   if(rule?.hediff==null || p==null || p.Dead || p.health==null)return;
   if(rule.playerOnly && p.Faction!=Faction.OfPlayer)return;
   if(!p.health.hediffSet.HasHediff(rule.hediff))p.health.AddHediff(rule.hediff);
  }
  void Fountains(Map pm,List<Pawn> players){
   var fd=Mythal.FountainDef.Def;if(fd==null)return;
   var fs=pm.listerThings.ThingsOfDef(fd);if(fs.Count==0)return;
   foreach(var p in players)if(p.RaceProps.Humanlike && !healed.Contains(p) && fs.Any(f=>p.Position.InHorDistOf(f.TrueCenter().ToIntVec3(),3.5f))){
    healed.Add(p);
    foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>!i.IsPermanent()).ToList())p.health.RemoveHediff(inj);
    if(p.needs?.rest!=null)p.needs.rest.CurLevel=1f;
    Messages.Message(p.LabelShort+"饮下迷锁泉：伤口愈合，疲劳一扫而空（每人每层一次）。",p,MessageTypeDefOf.PositiveEvent,false);
   }
  }

  public void Open(bool letter){
   state=Mythal.StateOpen;sealUntil=-1;
   if(theme==null)theme=Mythal.RollTheme(floor);if(rule==null)rule=Mythal.RollRule(floor);
   if(letter)Find.LetterStack.ReceiveLetter("迷锁之门开启","迷锁之门的符文重新亮起：第 "+floor+" 层可以进入了。\n\n"+FloorSummary()+"\n\n选中迷锁之门，用“进入”带人和物资下去。里面可以随时进出补给；打倒守护者就算解开这一层。",LetterDefOf.NeutralEvent,this);
  }
  public string FloorSummary(){
   string s="主题："+(theme?.label??"未知")+"；迷锁规则："+(rule?.label??"无")+(rule!=null && !rule.description.NullOrEmpty() && rule.hediff!=null?"（"+rule.description+"）":"")+"\n危险度 "+Points.ToString("0");
   if(Mythal.Milestone(floor))s+="\n里程碑层：守护者是"+Mythal.BossTitles[Mythal.BossIndex(floor)]+"。";
   else if(Mythal.Empowered(floor))s+="\n强化守护者层。";
   return s;
  }

  // 守护者全部倒下：经验（按在地牢里待的时间分）、每 10 层的里程碑、碎片和守护者宝藏、回程门。
  public void Clear(){
   state=Mythal.StateCleared;best=Math.Max(best,floor);
   var pm=PocketMap;float xp=Mythal.ClearXP(floor);var got=new List<string>();
   foreach(var kv in timeInside.ToList()){
    var p=kv.Key;if(p==null || p.Dead || p.Destroyed)continue;
    bool full=kv.Value>=Mythal.FullShareTicks || p.MapHeld==pm;
    float amount=xp*(full?1f:Mythal.PartialShare);
    float g=Adventurer.GainXP(p,amount);Mythal.GiveIsekaiXP(p,amount);
    if(Mythal.Milestone(floor))g+=Adventurer.Milestone(p,"LP_Mythal"+floor,Mythal.MilestoneXP(floor));
    if(g>0)got.Add(p.LabelShort+" +"+g.ToString("0"));
   }
   if(pm!=null){
    var cell=rewardCell.IsValid && rewardCell.InBounds(pm)?rewardCell:pm.Center;
    var loot=Mythal.RollLoot(theme,floor,true);
    var sh=ThingMaker.MakeThing(Mythal.ShardDef.Def);sh.stackCount=Mythal.Shards(floor);loot.Add(sh);
    Mythal.Drop(loot,cell,pm);
    SpawnReturnPortal(cell);
   }
   Find.LetterStack.ReceiveLetter("迷锁第 "+floor+" 层已解开","守护者倒下了，迷锁把这一层的力量还给了解开它的人。"+(got.Count>0?"\n\n冒险者经验："+string.Join("，",got):"")+(Mythal.Milestone(floor)?"\n\n这是一次里程碑（迷锁·第 "+(floor/10)+" 环）。":"")+"\n\n守护者房里出现了回程门、"+Mythal.Shards(floor)+" 片迷锁碎片和守护者的宝藏。所有人离开后这一层会坍塌，迷锁封闭 "+(Mythal.SealTicks/GenDate.TicksPerDay)+" 天后重组成第 "+(floor+1)+" 层。留在里面的东西会一起消失。",LetterDefOf.PositiveEvent,new LookTargets(this));
  }
  // 回程门：同一个出口 Def，再接一扇到这座门（原出口保持不变）。
  public Thing SpawnReturnPortal(IntVec3 near){
   var pm=PocketMap;if(pm==null)return null;var old=exit;
   if(!CellFinder.TryFindRandomCellNear(near,pm,8,c=>CellRect.CenteredOn(c,1).Cells.All(x=>x.InBounds(pm) && x.Standable(pm) && x.GetFirstItem(pm)==null && x.GetFirstPawn(pm)==null),out var cell))cell=near;
   PocketMapUtility.currentlyGeneratingPortal=this;
   try{return GenSpawn.Spawn(ThingMaker.MakeThing(Mythal.ExitDef.Def),cell,pm);}
   finally{PocketMapUtility.currentlyGeneratingPortal=null;exit=old;}
  }
  public void Collapse(){
   DestroyFloor();
   floor++;theme=null;rule=null;timeInside.Clear();
   state=Mythal.StateSealed;sealUntil=Find.TickManager.TicksGame+Mythal.SealTicks;
   theme=Mythal.RollTheme(floor);rule=Mythal.RollRule(floor);
   Messages.Message("迷锁第 "+(floor-1)+" 层坍塌了。迷锁封闭，"+(Mythal.SealTicks/GenDate.TicksPerDay)+" 天后重组成第 "+floor+" 层。",this,MessageTypeDefOf.NeutralEvent,false);
  }
  public void Rewind(string why){
   DestroyFloor();
   Messages.Message("迷锁第 "+floor+" 层回卷："+why,this,MessageTypeDefOf.NeutralEvent,false);
  }
  void Expel(List<Pawn> players){
   bool cleared=state==Mythal.StateCleared;
   foreach(var p in players){Mythal.MoveTo(p,Map,Position);if(p.RaceProps.Humanlike && Mythal.ExpelledThought.Def!=null)p.needs?.mood?.thoughts?.memories?.TryGainMemory(Mythal.ExpelledThought.Def);}
   Find.LetterStack.ReceiveLetter("被迷锁驱逐","地牢里的人全部倒下了。迷锁没有吞噬他们，而是把他们吐回了门口（带着伤）。"+(cleared?"":"\n\n第 "+floor+" 层回卷：下次进入是新的布局，怪物满血。"),LetterDefOf.NegativeEvent,new LookTargets(players));
   downedSince=-1;
   if(cleared)Collapse();else DestroyFloor();
  }
  void DestroyFloor(){
   var pm=PocketMap;
   if(pm!=null){
    // 先把殖民者送出来（坍塌、回卷不吃人）
    foreach(var p in pm.mapPawns.AllPawnsSpawned.Where(p=>p.Faction==Faction.OfPlayer).ToList())Mythal.MoveTo(p,Map,Position);
    if(Find.CurrentMap==pm)Current.Game.CurrentMap=Map;
    PocketMapUtility.DestroyPocketMap(pm);
   }
   exit=null;guardians.Clear();healed.Clear();downedSince=-1;
  }

  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   int shards=Mythal.ShardsOn(Map);
   yield return new Command_Action{defaultLabel="迷锁兑换",defaultDesc="用迷锁碎片在门上换东西。碎片只能在这里用（地图上任何地方的碎片都算）。\n\n现有碎片："+shards,icon=ContentFinder<Texture2D>.Get("LP/Mythal/Shard"),action=()=>Find.WindowStack.Add(new FloatMenu(ExchangeOptions()))};
   if(Prefs.DevMode){
    yield return new Command_Action{defaultLabel="开发：开启 / 结束封闭",action=()=>Open(true)};
    yield return new Command_Action{defaultLabel="开发：解开本层",action=()=>{if(state==Mythal.StateOpen && PocketMap!=null)Clear();}};
   }
  }
  public bool CanReroll=>state==Mythal.StateSealed || state==Mythal.StateDormant || state==Mythal.StateOpen && PocketMap==null;
  public List<FloatMenuOption> ExchangeOptions(){
   var l=new List<FloatMenuOption>();int shards=Mythal.ShardsOn(Map);
   foreach(var o in Mythal.Offers){
    if(o.key=="reroll"){if(!CanReroll || floor<5)continue;}
    else if(o.def!=null && Mythal.Thing(o.def)==null)continue;
    var off=o;string label=o.label+"（"+o.price+" 片）";
    if(shards<o.price)l.Add(new FloatMenuOption(label+"：碎片不够",null));
    else l.Add(new FloatMenuOption(label,()=>Buy(off)));
   }
   return l;
  }
  public bool Buy(Mythal.Offer o){
   if(o==null || !Mythal.Pay(Map,o.price))return false;
   if(o.key=="reroll"){rule=Mythal.RollRule(floor,rule);Messages.Message("下一层的迷锁规则变成了："+rule?.label,this,MessageTypeDefOf.PositiveEvent,false);return true;}
   string name=o.key=="scroll"?new[]{"LP_ScrollFireball","LP_ScrollTeleport","LP_ScrollProtectionEvil","LP_ScrollTrueSeeing"}.Where(n=>Mythal.Thing(n)!=null).RandomElementWithFallback():o.key=="magic"?Mythal.RandomMagicItem():o.def;
   var d=Mythal.Thing(name);if(d==null)return false;
   var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.stackCount=Math.Max(1,o.count);
   GenPlace.TryPlaceThing(t,InteractionCell.IsValid?InteractionCell:Position,Map,ThingPlaceMode.Near);
   Messages.Message("迷锁兑换："+t.LabelCap,t,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }

  public override string GetInspectString(){
   var sb=new System.Text.StringBuilder();int now=Find.TickManager.TicksGame;
   if(!Mythal.Enabled)sb.Append("迷锁地牢已在设置里关闭");
   else if(state==Mythal.StateDormant)sb.Append("沉睡：还剩 "+((openTick-now)/(float)GenDate.TicksPerDay).ToString("0.0")+" 天开启第 "+floor+" 层");
   else if(state==Mythal.StateSealed)sb.Append("封闭：还剩 "+((sealUntil-now)/(float)GenDate.TicksPerDay).ToString("0.0")+" 天重组成第 "+floor+" 层");
   else sb.Append((state==Mythal.StateCleared?"已解开：":"开启：")+"第 "+floor+" 层"+(PocketMap!=null?"，里面有 "+PocketMap.mapPawns.AllPawnsSpawned.Count(p=>p.Faction==Faction.OfPlayer)+" 个我方单位":""));
   if(Mythal.Enabled && state!=Mythal.StateCleared)sb.Append("\n"+FloorSummary());
   if(best>0)sb.Append("\n最深解开：第 "+best+" 层");
   string b=base.GetInspectString();if(!b.NullOrEmpty())sb.Append("\n"+b);
   return sb.ToString();
  }

  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref state,"mythalState",Mythal.StateDormant);Scribe_Values.Look(ref floor,"mythalFloor",1);Scribe_Values.Look(ref best,"mythalBest",0);
   Scribe_Values.Look(ref openTick,"mythalOpenTick",-1);Scribe_Values.Look(ref sealUntil,"mythalSealUntil",-1);Scribe_Values.Look(ref lastVisitTick,"mythalLastVisit",-1);Scribe_Values.Look(ref downedSince,"mythalDownedSince",-1);Scribe_Values.Look(ref generatedFloor,"mythalGeneratedFloor",0);
   Scribe_Defs.Look(ref theme,"mythalTheme");Scribe_Defs.Look(ref rule,"mythalRule");
   Scribe_Values.Look(ref rewardCell,"mythalRewardCell",IntVec3.Invalid);Scribe_Values.Look(ref entryCell,"mythalEntryCell",IntVec3.Invalid);
   Scribe_Collections.Look(ref guardians,"mythalGuardians",LookMode.Reference);Scribe_Collections.Look(ref healed,"mythalHealed",LookMode.Reference);
   Scribe_Collections.Look(ref timeInside,"mythalTimeInside",LookMode.Reference,LookMode.Value,ref tmpPawns,ref tmpTicks);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    guardians=guardians?.Where(p=>p!=null).ToList()??new List<Pawn>();healed=healed?.Where(p=>p!=null).ToList()??new List<Pawn>();
    if(timeInside==null)timeInside=new Dictionary<Pawn,int>();timeInside.RemoveAll(kv=>kv.Key==null);
   }
  }
 }

 // ---------- 楼层生成 ----------
 // 网格切房间（70 格图 3×3，90 格图 4×4），随机生成树 + 几条额外通道连起来，走廊宽 3 格（巨人、龙进得去）。
 // 入口房在一角，放出口；守护者房是离入口最远（按房间图距离）的那间；每 3 层必有一间迷锁泉休息房；1～2 间宝藏房；其余是战斗房。
 public class GenStep_MythalFloor : GenStep {
  public override int SeedPart=>0x4D5954;
  public class Room {public CellRect rect;public int gx,gz;public string type="combat";public List<Room> links=new List<Room>();}
  public static List<Room> lastRooms; // 自测看布局用

  public override void Generate(Map map,GenStepParams parms){
   var gate=PocketMapUtility.currentlyGeneratingPortal as Building_MythalGate;
   int floor=gate?.floor??1;var theme=gate?.theme??Mythal.RollTheme(floor);
   var floorTerrain=DefDatabase<TerrainDef>.GetNamedSilentFail(theme?.floorTerrain??"")??TerrainDefOf.Concrete;
   var rooms=Layout(map);lastRooms=rooms;
   var open=new HashSet<IntVec3>();
   foreach(var r in rooms)foreach(var c in r.rect)open.Add(c);
   foreach(var r in rooms)foreach(var o in r.links)if(rooms.IndexOf(o)>rooms.IndexOf(r))Corridor(r.rect.CenterCell,o.rect.CenterCell,open,map);
   foreach(var c in map.AllCells){
    map.terrainGrid.SetTerrain(c,floorTerrain);map.roofGrid.SetRoof(c,RoofDefOf.RoofRockThick);
    if(!open.Contains(c))GenSpawn.Spawn(Mythal.WallDef.Def,c,map);
   }
   // 房间类型
   var entry=rooms[0];entry.type="entry";
   var dist=Distances(rooms,entry);
   var guard=rooms.Where(r=>r!=entry).OrderByDescending(r=>dist[r]).ThenByDescending(r=>r.rect.Area).First();guard.type="guardian";
   var rest=rooms.Where(r=>r.type=="combat").InRandomOrder().ToList();
   int i=0;
   if(floor%3==0 && rest.Count>3)rest[i++].type="rest";
   int treasures=Rand.RangeInclusive(1,2);for(int t=0;t<treasures && i<rest.Count-2;t++)rest[i++].type="treasure";
   // 出口、灯、泉、宝藏
   GenSpawn.Spawn(ThingMaker.MakeThing(Mythal.ExitDef.Def),entry.rect.CenterCell,map);
   foreach(var r in rooms){var lamp=new IntVec3(r.rect.minX+1,0,r.rect.maxZ-1);if(lamp.Standable(map))GenSpawn.Spawn(Mythal.LampDef.Def,lamp,map);}
   foreach(var r in rooms.Where(r=>r.type=="rest"))GenSpawn.Spawn(Mythal.FountainDef.Def,r.rect.CenterCell,map);
   foreach(var r in rooms.Where(r=>r.type=="treasure")){
    var loot=Mythal.RollLoot(theme,floor,false);var sh=ThingMaker.MakeThing(Mythal.ShardDef.Def);sh.stackCount=Rand.RangeInclusive(1,3);loot.Add(sh);
    Mythal.Drop(loot,r.rect.CenterCell,map);
   }
   if(gate!=null){gate.entryCell=entry.rect.CenterCell;gate.rewardCell=guard.rect.CenterCell;}
   // 敌人
   float points=Mythal.Points(floor,gate?.Map??Find.AnyPlayerHomeMap);
   float guardPts=points*(Mythal.GuardianShare+(Mythal.Empowered(floor)?Mythal.EmpoweredExtra:0f));
   var combat=rooms.Where(r=>r.type=="combat").ToList();
   float per=combat.Count==0?0f:(points*(1f-Mythal.GuardianShare))/combat.Count;
   foreach(var r in combat)Populate(map,r,theme,per,floor,null);
   var boss=Mythal.Milestone(floor)?Mythal.BossKind(floor):theme?.Guardian;
   var gs=Populate(map,guard,theme,guardPts,floor,boss);
   if(gate!=null)gate.guardians.AddRange(gs);
  }

  static List<Room> Layout(Map map){
   int n=map.Size.x>=90?4:3,margin=2;int cell=(map.Size.x-margin*2)/n;
   var grid=new Room[n,n];var rooms=new List<Room>();
   for(int gz=0;gz<n;gz++)for(int gx=0;gx<n;gx++){
    int maxSide=Math.Min(15,cell-4);int w=Rand.RangeInclusive(9,maxSide),h=Rand.RangeInclusive(9,maxSide);
    int x0=margin+gx*cell+Rand.RangeInclusive(2,cell-2-w),z0=margin+gz*cell+Rand.RangeInclusive(2,cell-2-h);
    var r=new Room{rect=new CellRect(x0,z0,w,h),gx=gx,gz=gz};grid[gx,gz]=r;rooms.Add(r);
   }
   // 入口固定在左下角那间（rooms[0]）；随机深度优先生成树，再多连 n 条
   var seen=new HashSet<Room>{rooms[0]};var stack=new Stack<Room>();stack.Push(rooms[0]);
   IEnumerable<Room> Nb(Room r){if(r.gx>0)yield return grid[r.gx-1,r.gz];if(r.gx<n-1)yield return grid[r.gx+1,r.gz];if(r.gz>0)yield return grid[r.gx,r.gz-1];if(r.gz<n-1)yield return grid[r.gx,r.gz+1];}
   while(stack.Count>0){var r=stack.Peek();var next=Nb(r).Where(x=>!seen.Contains(x)).ToList();if(next.Count==0){stack.Pop();continue;}var o=next.RandomElement();r.links.Add(o);o.links.Add(r);seen.Add(o);stack.Push(o);}
   for(int k=0;k<n;k++){var r=rooms.RandomElement();var o=Nb(r).Where(x=>!r.links.Contains(x)).RandomElementWithFallback();if(o!=null){r.links.Add(o);o.links.Add(r);}}
   return rooms;
  }
  static void Corridor(IntVec3 a,IntVec3 b,HashSet<IntVec3> open,Map map){
   bool xFirst=Rand.Bool;var mid=xFirst?new IntVec3(b.x,0,a.z):new IntVec3(a.x,0,b.z);
   void Line(IntVec3 p,IntVec3 q){
    int dx=Math.Sign(q.x-p.x),dz=Math.Sign(q.z-p.z);var c=p;
    for(int guard=0;guard<400;guard++){for(int ox=-1;ox<=1;ox++)for(int oz=-1;oz<=1;oz++){var x=c+new IntVec3(ox,0,oz);if(x.x>0 && x.z>0 && x.x<map.Size.x-1 && x.z<map.Size.z-1)open.Add(x);}if(c==q)break;c+=new IntVec3(dx,0,dz);}
   }
   Line(a,mid);Line(mid,b);
  }
  static Dictionary<Room,int> Distances(List<Room> rooms,Room from){
   var d=rooms.ToDictionary(r=>r,r=>int.MaxValue);d[from]=0;var q=new Queue<Room>();q.Enqueue(from);
   while(q.Count>0){var r=q.Dequeue();foreach(var o in r.links)if(d[o]==int.MaxValue){d[o]=d[r]+1;q.Enqueue(o);}}
   return d;
  }
  // 按点数往房间里放怪：守护者（或里程碑首领）先放，剩下的点数从主题怪里随机挑（每间最多 6 个）。房间里的怪守着房间（DefendPoint），有人进来就打。
  static List<Pawn> Populate(Map map,Room r,MythalThemeDef theme,float budget,int floor,PawnKindDef leader){
   var made=new List<Pawn>();var kinds=theme?.Monsters.ToList()??new List<PawnKindDef>();
   Pawn Put(PawnKindDef k){
    var p=Mythal.MakeMonster(k);
    var cells=r.rect.ContractedBy(1).Cells.Where(c=>c.Standable(map) && c.GetFirstPawn(map)==null).ToList();
    var cell=cells.Count>0?cells.RandomElement():r.rect.CenterCell;
    GenSpawn.Spawn(p,cell,map);made.Add(p);return p;
   }
   var leaders=new List<Pawn>();
   if(leader!=null){
    var p=Put(leader);leaders.Add(p);budget-=leader.combatPower;
    Mythal.Level(p,floor,Mythal.Milestone(floor)?6:3,true);
    if(Mythal.Milestone(floor)||Mythal.Empowered(floor))HeroTraits.EnemyBoss(p);
   }
   for(int guard=0;guard<6-leaders.Count && kinds.Count>0;guard++){
    var fit=kinds.Where(k=>k.combatPower<=budget).ToList();
    if(fit.Count==0){if(made.Count==0)fit=new List<PawnKindDef>{kinds.MinBy(k=>k.combatPower)};else break;}
    var k=fit.RandomElement();var p=Put(k);budget-=k.combatPower;Mythal.Level(p,floor,0,false);
   }
   if(made.Count>0){
    var byFaction=made.GroupBy(p=>p.Faction);
    foreach(var g in byFaction)if(g.Key!=null)LordMaker.MakeNewLord(g.Key,new LordJob_DefendPoint(r.rect.CenterCell,4f,Math.Max(r.rect.Width,r.rect.Height)/2f+3f,false,false),map,g.ToList());
   }
   return leaders.Count>0?leaders:made;
  }
 }

 // 迷锁规则：口袋地图上的每个人（祝福只给我方）带一个规则状态；离开迷锁地图就自己消失。
 public class Hediff_MythalRule : HediffWithComps {
  bool gone;int acc;
  public override bool ShouldRemove=>gone || base.ShouldRemove;
  public override void TickInterval(int delta){
   base.TickInterval(delta);acc+=delta;if(acc<Mythal.TickInterval)return;acc=0;
   if(!Mythal.IsFloorMap(pawn.MapHeld))gone=true;
   else{var g=Mythal.Gates.FirstOrDefault(x=>x.PocketMap==pawn.MapHeld);if(g!=null && g.rule?.hediff!=def)gone=true;}
  }
 }

 // 新开局或旧存档：第一个殖民地旁放一座迷锁之门（只一次）。自测进程不自动放（检查自己放）。
 public class GameComponent_Mythal : GameComponent {
  public bool placed;
  public GameComponent_Mythal(Game game){}
  public override void ExposeData(){Scribe_Values.Look(ref placed,"mythalGatePlaced",false);}
  public override void GameComponentTick(){
   if(placed || Find.TickManager.TicksGame%2000!=37 || !Mythal.Enabled || !(LichMod.Settings?.mythalAutoGate??true) || GenCommandLine.CommandLineArgPassed("lichtest"))return;
   if(Mythal.Gates.Any()){placed=true;return;}
   var map=Find.Maps.FirstOrDefault(m=>m.IsPlayerHome && m.IsStartingMap)??Find.AnyPlayerHomeMap;if(map==null)return;
   var gate=Mythal.TryPlaceGate(map,Mythal.ColonyCenter(map));if(gate==null)return;
   placed=true;
   bool old=Find.TickManager.TicksGame>GenDate.TicksPerDay*2;
   Find.LetterStack.ReceiveLetter(old?"地下传来低沉的嗡鸣":"迷锁之门","殖民地旁边的地面裂开，一座古老的石拱门升了起来：这是埋在地下的失落迷锁的入口。\n\n迷锁每次被解开一层就会坍塌，封闭 "+(Mythal.SealTicks/GenDate.TicksPerDay)+" 天后用新的形状重组，层数一直往下，没有尽头。每层打倒守护者就能拿到冒险者经验、迷锁碎片和宝藏；每 10 层有一个里程碑首领。\n\n门会在 "+((gate.openTick-Find.TickManager.TicksGame)/(float)GenDate.TicksPerDay).ToString("0.#")+" 天后第一次开启。",LetterDefOf.NeutralEvent,gate);
  }
 }

 // ---------- 设置（“迷锁地牢”分组） ----------
 public partial class Settings {
  public bool mythalEnabled=true,mythalAutoGate=true,mythalIsekaiXp=true;
  public int mythalSealDays=3,mythalFirstDay=5,mythalDifficulty=100,mythalXp=100,mythalIsekaiPoints=60;
  void ExposeMythal(){
   Scribe_Values.Look(ref mythalEnabled,"mythalEnabled",true);Scribe_Values.Look(ref mythalAutoGate,"mythalAutoGate",true);Scribe_Values.Look(ref mythalIsekaiXp,"mythalIsekaiXp",true);
   Scribe_Values.Look(ref mythalSealDays,"mythalSealDays",3);Scribe_Values.Look(ref mythalFirstDay,"mythalFirstDay",5);Scribe_Values.Look(ref mythalDifficulty,"mythalDifficulty",100);Scribe_Values.Look(ref mythalXp,"mythalXp",100);Scribe_Values.Look(ref mythalIsekaiPoints,"mythalIsekaiPoints",60);
   mythalSealDays=Mathf.Clamp(mythalSealDays,1,10);mythalFirstDay=Mathf.Clamp(mythalFirstDay,0,15);mythalDifficulty=Mathf.Clamp(mythalDifficulty,25,300);mythalXp=Mathf.Clamp(mythalXp,0,300);mythalIsekaiPoints=Mathf.Clamp(mythalIsekaiPoints,30,100);
  }
 }
 public static class MythalSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.GapLine();l.Label("迷锁地牢");
   l.CheckboxLabeled("启用迷锁地牢",ref s.mythalEnabled,"（1.60.0）出生点旁的迷锁之门：一层一层往下打，每层打倒守护者拿冒险者经验、迷锁碎片和宝藏，然后封闭几天重组。关掉后门停在原地不能进入。");
   l.CheckboxLabeled("开局 / 读档时自动生成迷锁之门",ref s.mythalAutoGate,"只放一次，放在第一个殖民地的殖民者附近。关掉后可以用下面的按钮手动放。");
   l.Label("第一次开启：第 "+s.mythalFirstDay+" 天（冒险者小队开局第 1 天）");s.mythalFirstDay=(int)l.Slider(s.mythalFirstDay,0,15);
   l.Label("每层解开后封闭："+s.mythalSealDays+" 天");s.mythalSealDays=(int)l.Slider(s.mythalSealDays,1,10);
   l.Label("迷锁难度："+s.mythalDifficulty+"%（危险度 = (150 + 层数 × 35) × 难度）");s.mythalDifficulty=(int)l.Slider(s.mythalDifficulty,25,300);
   l.Label("通关经验倍率："+s.mythalXp+"%");s.mythalXp=(int)l.Slider(s.mythalXp,0,300);
   l.CheckboxLabeled("通关经验也给 Isekai 等级",ref s.mythalIsekaiXp,"装了 Isekai RPG Leveling 时，通关经验同时按同样数量给 Isekai 经验。");
   l.Label("装了 Isekai 时的怪物强度："+s.mythalIsekaiPoints+"%（Isekai 会再给怪物加一次评级）");s.mythalIsekaiPoints=(int)l.Slider(s.mythalIsekaiPoints,30,100);
   if(Current.ProgramState==ProgramState.Playing && Find.CurrentMap!=null && !Find.CurrentMap.IsPocketMap && !Mythal.Gates.Any() && l.ButtonText("在当前地图放下迷锁之门")){
    var g=Mythal.TryPlaceGate(Find.CurrentMap,Mythal.ColonyCenter(Find.CurrentMap));
    if(g!=null){var c=Current.Game.GetComponent<GameComponent_Mythal>();if(c!=null)c.placed=true;Messages.Message("迷锁之门已放下。",g,MessageTypeDefOf.PositiveEvent,false);}
    else Messages.Message("附近找不到一块 7×7 的平地放迷锁之门。",MessageTypeDefOf.RejectInput,false);
   }
  }
 }
}
