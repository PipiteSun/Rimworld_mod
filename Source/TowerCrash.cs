using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.31.0 法师塔坠落：一座塞伊法师塔的 3×3 残骸照原版飞船残骸的方式坠落在离殖民地 ≥20 格的空地，落点附近受撞击伤害。
 // 残骸周围 3～5 个红袍骷髅守着（绑定在一名不在场的红袍法师名下）；半径 15 格野魔法四溢。信件三选一：自己拆 / 等塞伊回收 / 通知同盟。
 public static class TowerCrash {
  public const int MinColonyDistance=20,EdgeMargin=10,GuardsMin=3,GuardsMax=5,ThayDays=5,AllyDays=3,AllyGoodwillMin=20,AllyGoodwill=10,SalvageWorkTicks=GenDate.TicksPerHour,AllyPoints=300;
  public const int InvisibleTicks=1800,HasteTicks=3600,TeleportCells=5;
  public const float WildMagicRadius=15f,WildMagicChance=.25f,WondrousChance=.5f,StudyScrollChance=.15f;
  public static readonly string[] ScrollPool={"LP_ScrollTeleport","LP_ScrollRevivify","LP_ScrollProtectionEvil","LP_ScrollFireball","LP_ScrollTrueSeeing","LP_ScrollFleshToStone"};
  public static readonly string[] WondrousPool={"LP_BagOfHolding","LP_CloakElvenkind","LP_BootsElvenkind","LP_BootsSpeed","LP_IntellectFortress","LP_RingResistanceFire","LP_RingResistanceCold","LP_RingResistanceAcid","LP_RingResistanceMind","LP_AmuletHealth","LP_WandDetection"};
  public static readonly string[] Effects={"invisible","haste","teleport","bloom","hiccup","hue"};
  static ThingDef wreck,incoming;
  public static ThingDef WreckDef=>wreck??(wreck=DefDatabase<ThingDef>.GetNamedSilentFail("LP_TowerWreck"));
  public static ThingDef IncomingDef=>incoming??(incoming=DefDatabase<ThingDef>.GetNamedSilentFail("LP_TowerWreckIncoming"));
  public static bool ThayPresent{get{var f=Factions.Hostile;return f!=null && !f.defeated && Factions.Enabled(f);}}
  public static IEnumerable<Building_TowerWreck> Wrecks(Map map)=>map==null || WreckDef==null?Enumerable.Empty<Building_TowerWreck>():map.listerThings.ThingsOfDef(WreckDef).OfType<Building_TowerWreck>();
  public static bool CanFire(Map map)=>LichMod.Settings.towerCrash && map!=null && map.IsPlayerHome && WreckDef!=null && ThayPresent && TryFindSpot(map,out _);

  // 3×3 全可站立、无屋顶、没有建筑物品人物、不在水里，离地图边 ≥10 格，离殖民地建筑 ≥20 格。
  public static bool SpotOk(IntVec3 c,Map map,List<IntVec3> colony){
   if(c.x<EdgeMargin || c.z<EdgeMargin || c.x>map.Size.x-EdgeMargin || c.z>map.Size.z-EdgeMargin)return false;
   foreach(var x in CellRect.CenteredOn(c,1)){if(!x.InBounds(map) || !x.Standable(map) || x.Roofed(map) || x.Fogged(map) || x.GetEdifice(map)!=null || x.GetFirstItem(map)!=null || x.GetFirstPawn(map)!=null || x.GetTerrain(map).IsWater)return false;}
   foreach(var b in colony)if(b.DistanceToSquared(c)<MinColonyDistance*MinColonyDistance)return false;
   return true;
  }
  public static bool TryFindSpot(Map map,out IntVec3 cell){
   var colony=map.listerBuildings.allBuildingsColonist.Select(b=>b.Position).ToList();
   return CellFinderLoose.TryGetRandomCellWith(c=>SpotOk(c,map,colony),map,2000,out cell);
  }
  public static Building_TowerWreck Crash(Map map,IntVec3 cell){
   var w=(Building_TowerWreck)ThingMaker.MakeThing(WreckDef);
   SkyfallerMaker.SpawnSkyfaller(IncomingDef,w,cell,map);
   return w;
  }

  // ---------- 产出 ----------
  static List<Thing> Stack(ThingDef def,int count){var list=new List<Thing>();if(def==null)return list;while(count>0){var t=ThingMaker.MakeThing(def);t.stackCount=Math.Min(count,def.stackLimit);count-=t.stackCount;list.Add(t);}return list;}
  static Thing Make(string name){var d=Items.Thing(name);if(d==null)return null;var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Outsider);return t;}
  public static Thing MakeWondrous(){var names=WondrousPool.Where(n=>Items.Thing(n)!=null).ToList();return names.Count==0?null:Make(names.RandomElement());}
  public static Thing MakeScroll(){var names=ScrollPool.Where(n=>Items.Thing(n)!=null).ToList();return names.Count==0?null:Make(names.RandomElement());}
  // 自己拆（或打赢回收队后拆）：卷轴 2～4、冒险者手册 1（只在有 VPE 桥接时存在）、50% 伊奥恩石或奇物、钢 150、玻璃钢 40。
  // 同盟拆走后留下一半：卷轴 1～2、钢 75、玻璃钢 20，没有手册和奇物。
  public static List<Thing> Loot(bool half){
   var list=new List<Thing>();
   int scrolls=half?Rand.RangeInclusive(1,2):Rand.RangeInclusive(2,4);
   for(int i=0;i<scrolls;i++)list.Add(MakeScroll());
   if(!half){
    list.Add(Make("LP_AdventurersManual"));
    if(Rand.Chance(WondrousChance))list.Add(Rand.Bool?Make("LP_IounStone"):MakeWondrous());
    if(Rand.Chance(StudyScrollChance))list.Add(Netheril.MakeStudy()); // 1.37.0
   }
   list.AddRange(Stack(ThingDefOf.Steel,half?75:150));
   list.AddRange(Stack(ThingDefOf.Plasteel,half?20:40));
   list.RemoveAll(t=>t==null);
   return list;
  }
  public static void Drop(List<Thing> loot,IntVec3 cell,Map map){foreach(var t in loot)GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);}

  // ---------- 野魔法 ----------
  public static bool Eligible(Pawn p,Building_TowerWreck w)=>p!=null && !p.Dead && p.Spawned && !w.guards.Contains(p) && Necro.Construct(p)==null && (p.RaceProps.Animal || (p.RaceProps.Humanlike && p.Faction==Faction.OfPlayer));
  public static int WildMagicTick(Building_TowerWreck w){
   var map=w.Map;if(map==null)return 0;int now=Events2.Now,n=0;
   foreach(var p in GenRadial.RadialDistinctThingsAround(w.Position,map,WildMagicRadius,true).OfType<Pawn>().ToList()){
    if(!Eligible(p,w))continue;
    int id=p.thingIDNumber;
    if(!w.nextRoll.TryGetValue(id,out int t)){w.nextRoll[id]=now+NextInterval();continue;}
    if(now<t)continue;
    w.nextRoll[id]=now+NextInterval();
    if(Events2.Roll<WildMagicChance && WildMagic(p)!=null)n++;
   }
   if(w.nextRoll.Count>64)foreach(var key in w.nextRoll.Keys.ToList())if(w.nextRoll[key]<now-GenDate.TicksPerDay)w.nextRoll.Remove(key);
   return n;
  }
  static int NextInterval()=>(int)(Rand.Range(2f,4f)*GenDate.TicksPerHour);
  // 随机小效果（都不致命）。返回实际生效的效果名；动物不会变色（换成别的效果）。
  public static string WildMagic(Pawn p,string effect=null){
   if(p==null || p.Dead)return null;
   effect=effect??Effects.Where(e=>e!="hue" || p.story!=null).RandomElement();
   var color=new Color(.75f,.6f,1f);
   switch(effect){
    case "invisible":Items.Give(p,"LP_PotionInvisibility",InvisibleTicks);Items.Mote(p,"隐形了！",color);break;
    case "haste":Items.Give(p,"LP_SpeedBurst",HasteTicks);Items.Mote(p,"加速！",color);break;
    case "teleport":{
     var map=p.Map;if(map==null)return null;
     var cells=GenRadial.RadialCellsAround(p.Position,TeleportCells,true).Where(c=>c.InBounds(map) && c.DistanceTo(p.Position)>=3f && c.Standable(map) && c.GetFirstPawn(map)==null && p.CanReach(c,PathEndMode.OnCell,Danger.Deadly)).ToList();
     if(cells.Count==0)return null;
     Races124.Blink(p,cells.RandomElement());Items.Mote(p,"被传送了！",color);break;}
    case "bloom":Items.Give(p,"LP_WildMagicBloom");p.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("LP_WildMagicBloomed"));Items.Mote(p,"头上开花了",new Color(1f,.7f,.85f));break;
    case "hiccup":p.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("LP_WildMagicHiccup"));Items.Mote(p,"嗝！",color);break;
    case "hue":if(p.story==null)return null;Items.Give(p,"LP_WildMagicHue");Items.Mote(p,"变色了！",color);break;
    default:return null;
   }
   return effect;
  }

  // ---------- 选择与回收 ----------
  public static bool CommsReady(Map map)=>map!=null && map.listerBuildings.allBuildingsColonist.Any(b=>b.def==ThingDefOf.CommsConsole && (b.TryGetComp<CompPowerTrader>()?.PowerOn??true));
  public static string AllyBlocked(Map map){
   var c=Factions.Colony;
   if(c==null || c.defeated || !Factions.Enabled(c))return "没有费伦拓荒同盟";
   if(c.HostileTo(Faction.OfPlayer))return "同盟和你敌对";
   if(c.PlayerGoodwill<AllyGoodwillMin)return "同盟好感不足 "+AllyGoodwillMin+"（现在 "+c.PlayerGoodwill+"）";
   if(!CommsReady(map))return "需要一台通电的通讯台";
   return null;
  }
  public static bool Choose(Building_TowerWreck w,string choice){
   if(w==null || w.Destroyed || w.choice!="none")return false;
   if(choice=="ally" && AllyBlocked(w.Map)!=null)return false;
   w.choice=choice;
   if(choice=="thay"){w.salvageBy="thay";w.salvageTick=Events2.Now+ThayDays*GenDate.TicksPerDay;}
   if(choice=="ally"){w.salvageBy="ally";w.salvageTick=Events2.Now+AllyDays*GenDate.TicksPerDay;}
   return true;
  }
  public static bool GuardsStanding(Building_TowerWreck w)=>w.guards.Any(g=>g!=null && !g.Dead && !g.Downed && g.Spawned);
  // 定时：到点派回收队；没选、5 天后守卫还在，塞伊自己来回收。
  public static void ScheduleTick(Building_TowerWreck w){
   int now=Events2.Now;
   if(w.salvageTick>=0 && !w.salvageSent && now>=w.salvageTick){SendSalvage(w);return;}
   if(w.choice=="none" && w.salvageTick<0 && !w.salvageSent && now>=w.crashTick+ThayDays*GenDate.TicksPerDay && GuardsStanding(w)){w.salvageBy="thay";w.autoThay=true;SendSalvage(w);}
  }
  public static Lord SendSalvage(Building_TowerWreck w){
   w.salvageSent=true;var map=w.Map;if(map==null)return null;
   bool ally=w.salvageBy=="ally";var f=ally?Factions.Colony:Factions.Hostile;
   if(f==null || f.defeated || (ally && f.HostileTo(Faction.OfPlayer))){Messages.Message((ally?"同盟的拆除队":"塞伊的回收队")+"没有来。",new LookTargets(w),MessageTypeDefOf.NeutralEvent,false);return null;}
   float points=ally?AllyPoints:StorytellerUtility.DefaultThreatPointsNow(map);
   points=Math.Max(points,f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)*1.05f);
   var pawns=PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,tile=map.Tile,faction=f,points=points},false).ToList();
   if(pawns.Count==0 || !Events2.EdgeCell(map,out var entry,w.Position)){foreach(var p in pawns)p.Destroy();return null;}
   foreach(var p in pawns)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,6),map);
   var stand=CellFinder.RandomClosewalkCellNear(w.Position,map,4);
   w.salvageLord=LordMaker.MakeNewLord(f,new LordJob_TowerSalvage(w,stand,ally),map,pawns);
   if(ally)Find.LetterStack.ReceiveLetter("同盟拆除队",f.Name+"派来了一队人拆走法师塔残骸。拆完以后他们会把一半东西留给你。",LetterDefOf.PositiveEvent,new LookTargets(pawns.Cast<Thing>()),f);
   else Find.LetterStack.ReceiveLetter("塞伊回收队",f.Name+"派来了一支回收队，要把法师塔残骸搬回塞伊。"+(w.autoThay?"守卫一直守着残骸，你也没表态，他们就自己来了。":"")+"\n\n让他们待满一小时，残骸就会被搬走；打退他们，残骸就还是你的。",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f);
   return w.salvageLord;
  }
  // 回收队在残骸边待满时间：还有能动的人就搬走残骸（同盟留一半产出、好感 +10）。
  public static bool Salvaged(Building_TowerWreck w,bool ally,Lord lord){
   if(w==null || w.Destroyed || !w.Spawned)return false;
   if(lord!=null && !lord.ownedPawns.Any(p=>!p.Dead && !p.Downed))return false;
   var map=w.Map;var pos=w.Position;
   w.salvaged=true;w.Destroy(DestroyMode.Vanish);
   if(ally){
    var loot=Loot(true);Drop(loot,pos,map);
    Factions.ChangeGoodwillExact(Factions.Colony,AllyGoodwill,"帮忙拆走法师塔残骸");
    Find.LetterStack.ReceiveLetter("残骸拆走了","同盟的拆除队拆走了法师塔残骸，留下了一半东西："+string.Join("、",loot.Select(t=>t.LabelCap))+"。同盟好感 +"+AllyGoodwill+"。",LetterDefOf.PositiveEvent,new LookTargets(pos,map));
   }else Find.LetterStack.ReceiveLetter("残骸被回收","塞伊的回收队把法师塔残骸搬走了。",LetterDefOf.NeutralEvent,new LookTargets(pos,map));
   return true;
  }
 }

 public class Building_TowerWreck : Building {
  public int crashTick=-1,salvageTick=-1;public string choice="none",salvageBy;public bool setup,salvageSent,autoThay,salvaged;
  public Pawn master;public List<Pawn> guards=new List<Pawn>();public Lord salvageLord;
  public Dictionary<int,int> nextRoll=new Dictionary<int,int>();
  public override void SpawnSetup(Map map,bool respawningAfterLoad){base.SpawnSetup(map,respawningAfterLoad);if(crashTick<0)crashTick=Find.TickManager.TicksGame;}
  public override void TickRare(){
   base.TickRare();if(!Spawned)return;
   if(!setup)Setup();
   TowerCrash.WildMagicTick(this);TowerCrash.ScheduleTick(this);
  }
  // 落地后：守卫（绑定在一名不在场的红袍法师名下）和信件。
  public void Setup(bool letter=true){
   setup=true;var map=Map;var f=Factions.Hostile;
   if(f!=null){
    master=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_FH_RedWizard"),f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
    AdventurerEnemies.Story(master,13); // 1.48.1 法师塔主人
    Find.WorldPawns.PassToWorld(master,PawnDiscardDecideMode.KeepForever);
    int n=Rand.RangeInclusive(TowerCrash.GuardsMin,TowerCrash.GuardsMax);
    for(int i=0;i<n;i++){var g=FactionConstructs.MakeBound("Skeleton",master,f);GenSpawn.Spawn(g,CellFinder.RandomClosewalkCellNear(Position,map,4),map);guards.Add(g);}
    LordMaker.MakeNewLord(f,new LordJob_DefendPoint(Position,5f,8f,false,false),map,guards);
   }
   if(!letter)return;
   var l=(Letter_TowerCrash)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_TowerChoiceLetter"));
   l.Label="法师塔坠落";l.wreck=this;l.lookTargets=new LookTargets(this);l.relatedFaction=f;
   l.Text="一座塞伊法师塔从天上摔了下来，砸在殖民地外！残骸周围有"+guards.Count+"个红袍骷髅守着，半径"+TowerCrash.WildMagicRadius+"格内野魔法四溢：附近的人和动物会时不时隐形、加速、被传送、头上开花、打嗝或者变色（都不致命）。\n\n"
    +"自己拆：打掉守卫后拆除残骸，能得到卷轴、钢、玻璃钢，可能还有奇物。\n等塞伊回收：5天后塞伊派回收队来搬走它（会和你交战，打退他们残骸就还是你的）。\n通知同盟：需要通电的通讯台和同盟好感≥"+TowerCrash.AllyGoodwillMin+"。3天后同盟来人拆走，留下一半东西，同盟好感+"+TowerCrash.AllyGoodwill+"。\n\n不选就当作自己处理；但守卫5天后还在的话，塞伊会自己来回收。";
   Find.LetterStack.ReceiveLetter(l);
  }
  public override void Destroy(DestroyMode mode=DestroyMode.Vanish){
   var map=Map;var pos=Position;bool deconstructed=mode==DestroyMode.Deconstruct && Spawned;
   base.Destroy(mode);
   if(deconstructed && map!=null){TowerCrash.Drop(TowerCrash.Loot(false),pos,map);Adventurer.MilestoneMap(map,"tower",Adventurer.TowerXP);} // 1.48.0 冒险者里程碑：自己拆掉残骸
   // 塔没了：绑定在塔主名下的骷髅随之崩解，塔主（不在场的世界角色）也一并移除。
   foreach(var g in guards)if(g!=null && !g.Dead && !g.Destroyed && g.Faction!=Faction.OfPlayer){if(g.Spawned)FleckMaker.ThrowDustPuffThick(g.DrawPos,g.Map,1.5f,Color.gray);Necro.Registry?.constructs.Remove(g);g.Destroy();}
   if(master!=null && !master.Destroyed){if(Find.WorldPawns.Contains(master))Find.WorldPawns.RemovePawn(master);master.Destroy();}
  }
  // 1.69.0：选中残骸也能重新打开“法师塔坠落”的选项。
  public override IEnumerable<Gizmo> GetGizmos(){foreach(var g in base.GetGizmos())yield return g;foreach(var g in EventPawnAccess.Gizmos(this))yield return g;}
  public override string GetInspectString(){
   var s=base.GetInspectString();var lines=new List<string>();
   if(!s.NullOrEmpty())lines.Add(s);
   lines.Add("野魔法范围："+TowerCrash.WildMagicRadius+"格");
   if(choice=="thay" || choice=="ally" || autoThay){if(!salvageSent && salvageTick>=0)lines.Add((salvageBy=="ally"?"同盟拆除队":"塞伊回收队")+"还有 "+((salvageTick-Find.TickManager.TicksGame)/(float)GenDate.TicksPerDay).ToString("0.#")+" 天到达");else if(salvageSent)lines.Add((salvageBy=="ally"?"同盟拆除队":"塞伊回收队")+"已经出发");}
   return string.Join("\n",lines);
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref crashTick,"lpCrashTick",-1);Scribe_Values.Look(ref salvageTick,"lpSalvageTick",-1);Scribe_Values.Look(ref choice,"lpChoice","none");Scribe_Values.Look(ref salvageBy,"lpSalvageBy");
   Scribe_Values.Look(ref setup,"lpSetup");Scribe_Values.Look(ref salvageSent,"lpSalvageSent");Scribe_Values.Look(ref autoThay,"lpAutoThay");
   Scribe_References.Look(ref master,"lpMaster",true);Scribe_References.Look(ref salvageLord,"lpSalvageLord");ScribePawns.Look(ref guards,"lpGuards");
   Scribe_Collections.Look(ref nextRoll,"lpWildMagic",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit && nextRoll==null)nextRoll=new Dictionary<int,int>();
  }
 }

 // 走到残骸边 → 守在旁边干活 1 小时 → 搬走（或拆走）残骸 → 离开。
 public class LordJob_TowerSalvage : LordJob {
  public Building_TowerWreck wreck;public IntVec3 cell;public bool ally;
  public LordJob_TowerSalvage(){}
  public LordJob_TowerSalvage(Building_TowerWreck wreck,IntVec3 cell,bool ally){this.wreck=wreck;this.cell=cell;this.ally=ally;}
  public override StateGraph CreateGraph(){
   var g=new StateGraph();
   var travel=new LordToil_Travel(cell);g.StartingToil=travel;
   var work=new LordToil_DefendPoint(cell,4f,null);g.AddToil(work);
   var leave=new LordToil_ExitMap(LocomotionUrgency.Walk,false,true);g.AddToil(leave);
   var arrive=new Transition(travel,work);arrive.AddTrigger(new Trigger_Memo("TravelArrived"));arrive.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerDay/2));g.AddTransition(arrive);
   var done=new Transition(work,leave);done.AddTrigger(new Trigger_TicksPassed(TowerCrash.SalvageWorkTicks));done.AddPreAction(new TransitionAction_Custom(()=>TowerCrash.Salvaged(wreck,ally,lord)));g.AddTransition(done);
   if(ally){var fight=new LordToil_AssaultColony(false,false);g.AddToil(fight);var hostile=new Transition(travel,fight);hostile.AddSources(work,leave);hostile.AddTrigger(new Trigger_BecamePlayerEnemy());g.AddTransition(hostile);}
   return g;
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref wreck,"wreck");Scribe_Values.Look(ref cell,"cell");Scribe_Values.Look(ref ally,"ally");}
 }

 public class Letter_TowerCrash : LP_EventLetter {
  public Building_TowerWreck wreck;
  // 1.69.0 事件小人入口：残骸还没决定怎么处理（选中残骸点按钮，或右键残骸）。
  protected override bool Live=>wreck!=null && !wreck.Destroyed && wreck.choice=="none";
  protected override bool StateTracked=>true;
  public override IEnumerable<Thing> EventThings{get{yield return wreck;}}
  public override IEnumerable<DiaOption> Choices{get{
   if(Closed || wreck==null || wreck.Destroyed){yield return Option_Close;yield break;}
   bool done=wreck.choice!="none";
   var self=Choice("自己拆",()=>TowerCrash.Choose(wreck,"self"));if(done)self.Disable("已经决定了");yield return self;
   var thay=Choice("等塞伊回收（"+TowerCrash.ThayDays+"天后）",()=>TowerCrash.Choose(wreck,"thay"));if(done)thay.Disable("已经决定了");yield return thay;
   var why=TowerCrash.AllyBlocked(wreck.Map);
   var ally=Choice("通知同盟（"+TowerCrash.AllyDays+"天后）",()=>TowerCrash.Choose(wreck,"ally"));if(done)ally.Disable("已经决定了");else if(why!=null)ally.Disable(why);yield return ally;
   yield return Option_Postpone;
  }}
  DiaOption Choice(string text,Action act)=>new DiaOption(text){action=()=>{act?.Invoke();Resolve();},resolveTree=true};
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref wreck,"lpWreck");}
 }

 // 野魔法变色：皮肤随机染色 1 天，结束时换回原来的颜色。
 public class Hediff_WildMagicHue : HediffWithComps {
  public bool applied,hadOverride;public Color original,hue;
  public override void PostAdd(DamageInfo? dinfo){
   base.PostAdd(dinfo);
   if(pawn?.story==null)return;
   hadOverride=pawn.story.skinColorOverride.HasValue;original=pawn.story.skinColorOverride??Color.white;
   hue=Color.HSVToRGB(Rand.Value,.55f,.9f);pawn.story.skinColorOverride=hue;applied=true;
   pawn.Drawer?.renderer?.SetAllGraphicsDirty();
  }
  public override void PostRemoved(){
   base.PostRemoved();
   if(!applied || pawn?.story==null)return;
   pawn.story.skinColorOverride=hadOverride?original:(Color?)null;
   pawn.Drawer?.renderer?.SetAllGraphicsDirty();
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref applied,"lpApplied");Scribe_Values.Look(ref hadOverride,"lpHadOverride");Scribe_Values.Look(ref original,"lpOriginal");Scribe_Values.Look(ref hue,"lpHue");}
 }

 public class IncidentWorker_TowerCrash : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms)=>TowerCrash.CanFire(parms.target as Map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;if(!TowerCrash.CanFire(map) || !TowerCrash.TryFindSpot(map,out var cell))return false;
   TowerCrash.Crash(map,cell);
   return true;
  }
 }
}
