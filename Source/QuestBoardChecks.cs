using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.71.0 探索公告板的检查（.\Test.ps1 -Only QuestBoard）：定义与贴图、等阶门槛、每日刷新、守卫家园全流程（来袭 → 打退 → 成功、经验、空投）、
 // 远征讨伐全流程（站点 → 生成巢穴地图 → 消灭怪物 → 成功）、清剿据点失败流程（据点消失 → 失败）、同时接取上限、设置开关；存读档后已接委托和今日委托还在。
 public partial class LichChecks {
  readonly List<Thing> qbMade=new List<Thing>();
  static readonly string[] qbBanned={"d20","D20","掷骰","检定","DC"};
  static int qbSavedActive=-1;static List<int> qbSavedOffers=new List<int>();
  static bool QbChinese(string s)=>!s.NullOrEmpty() && s.Any(c=>c>=0x4e00 && c<=0x9fff);
  Pawn QbColonist(){
   var p=Person();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,6),map);qbMade.Add(p);
   var h=Adventurer.TryAdd(p,1,true);if(h!=null){h.level=9;h.xp=Adventurer.XpTable[8];}
   return p;
  }
  void QbKill(IEnumerable<Pawn> pawns){foreach(var p in pawns.ToList())if(p!=null && !p.Dead){p.Kill(null);if(p.Corpse!=null)qbMade.Add(p.Corpse);}}

  void CheckQuestBoard(){
   var s=LichMod.Settings;bool oOn=s.questBoard,oAdv=s.adventurer,oSup=Adventurer.TestSuppressed;
   s.questBoard=true;s.adventurer=true;Adventurer.TestSuppressed=false;
   Rand.PushState(17101);
   try{QbDefs();QbTiers();QbRefresh();QbDefense();QbHunt();QbCamp();QbLimits();}
   finally{
    Rand.PopState();s.questBoard=oOn;s.adventurer=oAdv;Adventurer.TestSuppressed=oSup;QuestBoard.TestLevel=null;
    var st=QuestBoard.State;if(st!=null){foreach(var c in st.active.ToList())QuestBoard.Fail(c,"检查结束。");st.offers.Clear();}
    foreach(var t in qbMade)if(t!=null && !t.Destroyed)t.Destroy();qbMade.Clear();
   }
  }

  // 1. 定义、中文、贴图、设置。
  void QbDefs(){
   var b=QuestBoard.BoardDef;var lair=QuestBoard.LairDef;var root=QuestBoard.RootDef;
   bool tex=new[]{"south","north","east"}.All(d=>ContentFinder<Texture2D>.Get("LP/QuestBoard/QuestBoard_"+d,false)!=null);
   Check(b!=null && b.thingClass==typeof(Building_QuestBoard) && b.size==new IntVec2(2,1) && b.rotatable && b.Minifiable && tex && QbChinese(b.label) && QbChinese(b.description) && (b.researchPrerequisites==null || b.researchPrerequisites.Count==0) && b.costList.Any(c=>c.thingDef==ThingDefOf.WoodLog),
    "[quest board] 探索公告板 loads: 2×1 rotatable wooden building, south/north/east textures, Chinese text, no research needed");
   Check(lair!=null && !lair.requiresFaction && lair.wantsThreatPoints && QbChinese(lair.label) && DefDatabase<GenStepDef>.AllDefs.Any(g=>g.linkWithSite==lair && g.genStep is GenStep_QuestBoardLair) && root!=null && root.isRootSpecial && QuestBoard.CampDef!=null,
    "[quest board] 怪物巢穴 site part + genstep, outpost site part and the special quest root load");
   Check(new Settings().questBoard && QuestBoard.TierNames.Length==5 && QuestBoard.TierNames.All(QbChinese) && Mathf.Approximately(Adventurer.QuestShare,.5f),"[quest board] setting on by default, five Chinese tier names, quest XP share is ×0.5");
  }

  // 2. 等阶门槛：殖民地最高冒险者等级 1 / 5 / 9 / 13 / 17。
  void QbTiers(){
   var got=new[]{1,4,5,9,12,13,16,17,20}.Select(l=>{QuestBoard.TestLevel=l;return QuestBoard.UnlockedTier();}).ToList();
   LichMod.Settings.adventurer=false;int off=QuestBoard.UnlockedTier();LichMod.Settings.adventurer=true;
   Check(got.SequenceEqual(new[]{1,1,2,3,3,4,4,5,5}) && off==5,"[quest board] tiers unlock at colony max level 1/5/9/13/17 ("+string.Join(",",got)+"); with adventurer levels off all five are open");
  }

  // 3. 每日刷新：只出已解锁的等阶，最高等阶至少一份，报酬与点数按表。
  void QbRefresh(){
   QuestBoard.TestLevel=9;QuestBoard.Refresh();var st=QuestBoard.State;var offers=st.offers.ToList();
   bool table=offers.All(o=>Near(o.points,QuestBoard.TierPoints[o.tier-1]*(o.kind==QuestBoard.Hunt?QuestBoard.HuntPoints:1f)) && o.silver>0 && o.items.Count>0 && o.items.All(n=>Items.Thing(n)!=null));
   bool text=offers.All(o=>QbChinese(QuestBoard.Title(o)) && QuestBoard.Describe(o).Contains("冒险者经验") && !qbBanned.Any(w=>QuestBoard.Describe(o).Contains(w)));
   Check(offers.Count>=3 && offers.Count<=QuestBoard.OffersPerDay && offers.All(o=>o.tier>=1 && o.tier<=3) && offers.Any(o=>o.tier==3) && table && text && st.nextRefresh>QuestBoard.Now,
    "[quest board] daily refresh at level 9: "+offers.Count+" offers ("+string.Join(" ",offers.Select(o=>o.kind+o.tier))+"), tiers 1~3 with a tier-3 one, points / silver / items by the table, Chinese text without dice words");
  }

  // 4. 守卫家园：接取 → 来袭 → 打退 → 成功，接取人拿“点数 × 0.5”经验，报酬空投。
  void QbDefense(){
   var st=QuestBoard.State;var who=QbColonist();var h=Adventurer.Get(who);
   var c=QuestBoard.MakeOffer(QuestBoard.Defense,1,map);st.offers.Add(c);
   var q=QuestBoard.Accept(c,who);
   Check(q!=null && q.State==QuestState.Ongoing && q.AccepterPawn==who && Near(q.points,300f) && st.active.Contains(c) && !st.offers.Contains(c) && Find.QuestManager.QuestsListForReading.Contains(q) && q.name.Contains("青铜") && c.raidTick>QuestBoard.Now,
    "[quest board] accepting 守卫家园 makes an ongoing vanilla quest (accepter "+who.LabelShort+", 300 points) and schedules the attack");
   QuestBoard.Tick(c);Check(!c.raidSent,"[quest board] nothing arrives before the scheduled time");
   c.raidTick=QuestBoard.Now;QuestBoard.Tick(c);
   var raiders=c.raiders.ToList();qbMade.AddRange(raiders);
   Check(c.raidSent && raiders.Count>0 && raiders.All(p=>p.Spawned && p.Map==map && p.HostileTo(Faction.OfPlayer)) && q.State==QuestState.Ongoing,"[quest board] the attack arrives: "+raiders.Count+" hostile "+(c.faction!=null?c.faction.Name:"animals")+" on the home map, quest still ongoing");
   int pods=map.listerThings.ThingsOfDef(ThingDefOf.DropPodIncoming).Count;float xp0=h?.xp??0f;
   QbKill(raiders);QuestBoard.Tick(c);
   float gained=(h?.xp??0f)-xp0;float expect=300f*Adventurer.QuestShare*Adventurer.XpScale;
   Check(q.State==QuestState.EndedSuccess && !st.active.Contains(c) && c.done && gained>=expect*.99f && map.listerThings.ThingsOfDef(ThingDefOf.DropPodIncoming).Count>pods,
    "[quest board] all attackers down → quest succeeds, "+who.LabelShort+" gains "+gained.ToString("0")+" XP (≥ 300 × 0.5 = "+expect+"), reward drop pod incoming");
  }

  // 5. 远征讨伐：站点在 2+阶～5+2×阶 格，地图上有狂暴野兽，消灭后成功。
  void QbHunt(){
   var st=QuestBoard.State;var who=QbColonist();
   var c=QuestBoard.MakeOffer(QuestBoard.Hunt,2,map);
   var beastKind=DefDatabase<PawnKindDef>.GetNamedSilentFail(c?.animal??"");
   Check(c!=null && beastKind!=null && beastKind.combatPower>=QuestBoard.MinBeastPower(c.points) && Near(c.points,700f*QuestBoard.HuntPoints),"[quest board] a tier-2 hunt offer picks a real beast ("+c?.animal+", combat power "+beastKind?.combatPower+" ≥ "+QuestBoard.MinBeastPower(c?.points??0f)+") at 700 × 0.8 points");
   st.offers.Add(c);var q=QuestBoard.Accept(c,who);var site=c.site;
   int dist=site==null?-1:Find.WorldGrid.TraversalDistanceBetween(map.Tile,site.Tile);
   var part=site?.parts?.FirstOrDefault();
   Check(q!=null && q.State==QuestState.Ongoing && site!=null && Find.WorldObjects.Contains(site) && part?.def==QuestBoard.LairDef && part.parms.animalKind!=null && Near(part.parms.threatPoints,c.points) && dist>=QuestBoard.SiteMin(2) && dist<=QuestBoard.SiteMax(2)+1 && site.GetComponent<TimeoutComp>()?.Active==true && q.PartsListForReading.OfType<QuestPart_QuestBoard>().Single().QuestLookTargets.Contains(site),
    "[quest board] accepting the hunt puts 怪物巢穴 on the world map "+dist+" tiles away (with a timeout, as the quest's look target)");
   var siteMap=GetOrGenerateMapUtility.GetOrGenerateMap(site.Tile,new IntVec3(80,1,80),null);
   try{
    var beasts=siteMap.mapPawns.AllPawnsSpawned.Where(p=>p.RaceProps.Animal && p.HostileTo(Faction.OfPlayer)).ToList();
    Check(beasts.Count>0 && beasts.All(p=>p.InMentalState && p.MentalStateDef==MentalStateDefOf.ManhunterPermanent) && beasts.Any(p=>p.kindDef==part.parms.animalKind),"[quest board] the lair map holds "+beasts.Count+" permanently manhunting "+part.parms.animalKind.label);
    QuestBoard.Tick(c);Check(c.sawEnemies && q.State==QuestState.Ongoing,"[quest board] entering the lair: monsters seen, quest still ongoing");
    QbKill(beasts);foreach(var p in siteMap.mapPawns.AllPawnsSpawned.Where(p=>!p.Dead && !p.Downed && p.HostileTo(Faction.OfPlayer)).ToList())p.Kill(null);
    QuestBoard.Tick(c);
    Check(q.State==QuestState.EndedSuccess && !st.active.Contains(c),"[quest board] all monsters dead → the hunt succeeds");
   }finally{if(Find.Maps.Contains(siteMap))Current.Game.DeinitAndRemoveMap(siteMap,false);if(!site.Destroyed)site.Destroy();}
  }

  // 6. 清剿据点：站点是敌对派系的前哨；据点消失（过期）时委托失败、站点收走。
  void QbCamp(){
   var st=QuestBoard.State;var c=QuestBoard.MakeOffer(QuestBoard.Camp,2,map);
   if(c==null){Check(QuestBoard.PickFaction(1400f,true)==null,"[quest board] no hostile faction can hold an outpost here, so no 清剿据点 offer");return;}
   st.offers.Add(c);var q=QuestBoard.Accept(c,null);var site=c.site;
   Check(q!=null && site!=null && site.Faction==c.faction && site.Faction.HostileTo(Faction.OfPlayer) && site.parts.Any(p=>p.def==QuestBoard.CampDef) && c.deadline>QuestBoard.Now,"[quest board] 清剿据点 puts a "+c.faction.Name+" outpost on the world map");
   site.Destroy();QuestBoard.Tick(c);
   Check(q.State==QuestState.EndedFailed && !st.active.Contains(c),"[quest board] the outpost vanishing (timeout) fails the contract");
  }

  // 7. 同时最多 3 份；设置关掉后不刷新、不能接。
  void QbLimits(){
   var st=QuestBoard.State;QuestBoard.Refresh();var offer=st.offers.FirstOrDefault();
   var fakes=Enumerable.Range(0,QuestBoard.MaxActive).Select(i=>new BoardContract{kind=QuestBoard.Defense}).ToList();st.active.AddRange(fakes);
   var full=QuestBoard.CanAccept(offer);foreach(var f in fakes)st.active.Remove(f);
   QuestBoard.TestLevel=1;var high=st.offers.FirstOrDefault(o=>o.tier>1);var low=high==null?true:QuestBoard.CanAccept(high).Accepted;QuestBoard.TestLevel=9;
   LichMod.Settings.questBoard=false;var off=QuestBoard.CanAccept(offer);QuestBoard.Refresh();int offCount=st.offers.Count;LichMod.Settings.questBoard=true;
   Check(offer!=null && !full.Accepted && full.Reason.Contains("最多") && !low && !off.Accepted && offCount==0,"[quest board] at most "+QuestBoard.MaxActive+" active contracts; higher tiers need the level; turning the setting off empties the board and blocks accepting");
  }

  // 存读档：一份已接的远征讨伐（站点）和今天的委托。
  void PrepareQuestBoardSave(){
   var s=LichMod.Settings;s.questBoard=true;QuestBoard.TestLevel=9;
   Rand.PushState(17102);
   try{
    QuestBoard.Refresh();var st=QuestBoard.State;
    var c=QuestBoard.MakeOffer(QuestBoard.Hunt,1,map);st.offers.Add(c);QuestBoard.Accept(c,null);
    qbSavedActive=c.id;qbSavedOffers=st.offers.Select(o=>o.id).ToList();
    Check(c.quest!=null && c.site!=null && qbSavedOffers.Count>0,"[quest board] before saving: one accepted hunt and "+qbSavedOffers.Count+" offers");
   }finally{Rand.PopState();QuestBoard.TestLevel=null;}
  }
  void CheckQuestBoardAfterLoad(){
   var st=QuestBoard.State;var c=st?.ById(qbSavedActive);
   Check(c!=null && st.active.Contains(c) && c.quest?.State==QuestState.Ongoing && c.site!=null && !c.site.Destroyed && c.quest.PartsListForReading.OfType<QuestPart_QuestBoard>().Any(p=>p.contractId==c.id) && st.offers.Select(o=>o.id).SequenceEqual(qbSavedOffers) && st.offers.All(o=>o.items.Count>0),
    "[quest board] after loading: the accepted hunt keeps its quest and site, today's offers are unchanged");
   QuestBoard.Fail(c,"检查结束。");
   Check(c.quest.State==QuestState.EndedFailed && c.site.Destroyed,"[quest board] failing it after load ends the quest and removes the site");
  }
 }
}
