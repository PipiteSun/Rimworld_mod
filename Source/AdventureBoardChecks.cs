using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.134.0 冒险委托板：Test.ps1 -Only AdventureBoard。
 // 定义与每图一块、名声等级、刷新 3 条（至少一条低或中、推荐等级 = 平均 ±2、距离 2～8）、点数与难度倍率、接受后生成地点与上限、
 // 5 种类型各生成一次地图并完成（巢穴全清、击杀 / 俘获通缉犯、救回冒险者、带回遗物、摧毁亡灵之源）、参与者经验、过期失败、湖畔委托、传奇委托、存读档。
 public partial class LichChecks {
  static readonly List<Thing> akbMade=new List<Thing>();
  static int akbSavedRenown,akbSavedOffers,akbSavedActiveId;
  Building akbBoard;
  AdventureKindDef AK(string n)=>DefDatabase<AdventureKindDef>.GetNamed("LP_AK_"+n);
  void AbEnsureBoard(){
   if(akbBoard!=null && akbBoard.Spawned)return;var def=AdventureBoard.BoardDef;
   var cell=map.AllCells.Where(c=>c.x>15 && c.z>15 && c.x<map.Size.x-15 && c.z<map.Size.z-15 && CellRect.CenteredOn(c,2).Cells.All(x=>x.InBounds(map) && x.Standable(map) && x.GetFirstBuilding(map)==null)).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
   akbBoard=(Building)ThingMaker.MakeThing(def,ThingDefOf.WoodLog);akbBoard.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(akbBoard,cell,map);
  }
  // 接下一条指定类型的委托并生成它的地图，跑 body（参数：委托、地点地图），最后清理地图和站点。
  void AbRun(AdventureKindDef kind,int danger,Action<AdventureContract,Map> body,bool lake=false){
   var st=AdventureBoard.State;var c=AdventureBoard.MakeOffer(kind,danger,map,lake);
   if(c==null){Check(false,"[1.134.0] "+kind.label+": could not make an offer");return;}
   st.offers.Add(c);var q=AdventureBoard.Accept(c);
   if(q==null || c.site==null){Check(false,"[1.134.0] "+kind.label+": accept failed");return;}
   Map siteMap=null;
   try{siteMap=GetOrGenerateMapUtility.GetOrGenerateMap(c.site.Tile,new IntVec3(80,1,80),null);body(c,siteMap);}
   finally{if(siteMap!=null && Find.Maps.Contains(siteMap))Current.Game.DeinitAndRemoveMap(siteMap,false);if(c.site!=null && !c.site.Destroyed)c.site.Destroy();st.active.Remove(c);}
  }
  static void AbKillHostiles(Map m){foreach(var p in m.mapPawns.AllPawnsSpawned.Where(p=>!p.Dead && p.HostileTo(Faction.OfPlayer)).ToList())p.Kill(null);}

  void CheckAdventureBoard(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool on=s.adventurer,ab=s.adventureBoard;float diff=s.adventureDifficulty;int iv=s.adventureInterval;
   s.adventurer=true;s.adventureBoard=true;s.adventureDifficulty=1f;s.adventureInterval=5;
   Rand.PushState(113401);
   try{CheckAdventureBoardInner();}
   finally{Rand.PopState();Adventurer.TestSuppressed=sup;s.adventurer=on;s.adventureBoard=ab;s.adventureDifficulty=diff;s.adventureInterval=iv;AdventureBoard.TestRoll=null;foreach(var t in akbMade)if(t!=null && !t.Destroyed)t.Destroy();akbMade.Clear();}
  }
  void CheckAdventureBoardInner(){
   var st=AdventureBoard.State;AbEnsureBoard();
   // 1. 定义：告示板 1×2、木材或石料 50、没有研究；每张地图只能一块；6 种委托类型都有 Worker。
   {var def=AdventureBoard.BoardDef;var pw=new PlaceWorker_OnePerMap();var second=pw.AllowsPlacing(def,map.Center,Rot4.South,map);
    Check(def.size==new IntVec2(1,2) && def.costStuffCount==50 && def.stuffCategories.Contains(StuffCategoryDefOf.Woody) && def.stuffCategories.Contains(StuffCategoryDefOf.Stony) && def.researchPrerequisites.NullOrEmpty() && !second.Accepted
     && AdventureBoard.Kinds.Count==6 && AdventureBoard.Kinds.All(k=>k.Worker!=null) && AdventureBoard.SiteDef!=null && AdventureBoard.RootDef!=null,"[1.134.0] adventurer board: 1×2, 50 wood or stone, no research, one per map ("+second.Reason+"), 6 contract kinds with workers");}
   // 2. 名声：0～100，无名 0 / 小有名气 20 / 知名 45 / 威名远扬 70 / 传奇 90；完成 +2～+6，失败 -3（夹在 0～100）。
   {st.renown=0;AdventureBoard.AddRenown(-5);int a=st.renown;st.renown=99;AdventureBoard.AddRenown(6);int b=st.renown;
    Check(a==0 && b==100 && AdventureBoard.RenownName(0)=="无名" && AdventureBoard.RenownName(20)=="小有名气" && AdventureBoard.RenownName(44)=="小有名气" && AdventureBoard.RenownName(45)=="知名" && AdventureBoard.RenownName(70)=="威名远扬" && AdventureBoard.RenownName(90)=="传奇" && AdventureBoard.RenownGain.SequenceEqual(new[]{2,3,5,6}),"[1.134.0] renown 0–100 with the five tiers, +2/+3/+5/+6 by danger, clamped");
    st.renown=0;}
   // 3. 刷新：每次 3 条、至少一条低或中、推荐等级 = 平均 ±2、地点距离 2～8 格；没接的到期消失。
   {var bad=new List<string>();int avg=AdventureBoard.ColonyAverage();
    for(int i=0;i<8;i++){
     st.renown=i*12;AdventureBoard.Refresh();
     if(st.offers.Count!=3)bad.Add("条数 "+st.offers.Count);
     if(!st.offers.Any(o=>o.danger<=1))bad.Add("没有低 / 中");
     foreach(var o in st.offers){if(Math.Abs(o.recLevel-avg)>2 && o.recLevel>1)bad.Add("等级 "+o.recLevel+" 平均 "+avg);int d=AdventureBoard.Distance(o);if(d<AdventureBoard.SiteMin-1 || d>AdventureBoard.SiteMax+1)bad.Add("距离 "+d);if(o.offerExpires<=AdventureBoard.Now)bad.Add("到期");}
    }
    st.renown=0;
    Check(bad.Count==0,"[1.134.0] refresh: 3 offers, at least one low/mid danger, recommended level = top-3 average ("+avg+") ±2, sites 2–8 tiles"+(bad.Count>0?": "+string.Join("；",bad.Distinct().Take(6)):""));}
   // 4. 点数按推荐等级（不按财富），再乘难度倍率；危险度越高报酬倍率越高。
   {float p5=AdventureBoard.PointsFor(5,1),p10=AdventureBoard.PointsFor(10,1);LichMod.Settings.adventureDifficulty=2f;float p10x=AdventureBoard.PointsFor(10,1);LichMod.Settings.adventureDifficulty=1f;
    var lo=new AdventureContract{danger=0,points=500f};var hi=new AdventureContract{danger=3,points=500f};
    Check(p10>p5 && Near(p10x,p10*2f) && AdventureBoard.RewardMult(hi)>AdventureBoard.RewardMult(lo),"[1.134.0] threat points scale with recommended level and the difficulty setting ("+p5+" → "+p10+", ×2 = "+p10x+"); higher danger pays more");}
   // 5. 接受：生成地点（2～8 格、带超时），进入已接；同时最多 2 条。
   {AdventureBoard.Refresh();var offs=st.offers.ToList();var q1=AdventureBoard.Accept(offs[0]);var q2=AdventureBoard.Accept(offs[1]);var third=AdventureBoard.CanAccept(offs[2]);
    var c=st.active.FirstOrDefault();int d=c?.site==null?-1:Find.WorldGrid.TraversalDistanceBetween(map.Tile,c.site.Tile);
    Check(q1!=null && q2!=null && st.active.Count==2 && !third.Accepted && c.site!=null && Find.WorldObjects.Contains(c.site) && d>=AdventureBoard.SiteMin-1 && d<=AdventureBoard.SiteMax+1 && c.site.GetComponent<TimeoutComp>()?.Active==true && c.deadline>AdventureBoard.Now,
     "[1.134.0] accepting puts the site on the world map ("+d+" tiles, with a timeout); at most 2 active ("+third.Reason+")");
    foreach(var a in st.active.ToList()){if(a.site!=null && !a.site.Destroyed)a.site.Destroy();a.quest?.End(QuestEndOutcome.Unknown,false);}st.active.Clear();}
   // 6. 清剿怪物巢穴：地图上有永久狂暴的怪物；全清 → 完成，名声 +，参与者（在地点地图上的殖民者）得到经验。
   AbRun(AK("Lair"),1,(c,m)=>{
    var beasts=m.mapPawns.AllPawnsSpawned.Where(p=>p.RaceProps.Animal && p.HostileTo(Faction.OfPlayer)).ToList();
    var hero=Person();GenSpawn.Spawn(hero,CellFinder.RandomClosewalkCellNear(m.Center,m,10),m);akbMade.Add(hero);var h=Adventurer.TryAdd(hero,3,true);float xp0=h.xp;
    AdventureBoard.Tick(c);bool seen=c.sawEnemies && c.participants.Contains(hero);int r0=st.renown;bool mad=beasts.Any(p=>p.MentalStateDef==MentalStateDefOf.ManhunterPermanent);
    AbKillHostiles(m);AdventureBoard.Tick(c);
    Check(beasts.Count>0 && mad && seen && c.done && c.quest.State==QuestState.EndedSuccess && st.renown==r0+3 && h.xp>xp0,"[1.134.0] lair: "+beasts.Count+" manhunting monsters ("+string.Join(",",beasts.Select(b=>b.kindDef.label).Distinct())+"), cleared → success, renown +3, participant XP "+xp0+"→"+h.xp+" ["+beasts.All(p=>p.MentalStateDef==MentalStateDefOf.ManhunterPermanent)+" "+seen+" "+c.done+" "+c.quest.State+" "+st.renown+"/"+r0+"]");
    if(hero.Spawned)hero.DeSpawn();});
   // 7. 讨伐通缉犯：首领带职业；俘获（关进囚室）算完成并 ×1.25。
   AbRun(AK("Bounty"),2,(c,m)=>{
    var boss=c.target;var h=boss==null?null:Adventurer.Get(boss);bool classed=h?.classes!=null && h.classes.levels.Count>0;
    boss.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);AdventureBoard.Tick(c);
    Check(boss!=null && boss.Spawned && classed && c.done && c.captured && c.quest.State==QuestState.EndedSuccess,"[1.134.0] bounty: the wanted leader has classes ("+(h==null?"":NpcClasses.Summary(h))+"), capturing completes with ×1.25");});
   // 8. 救援被困冒险者：倒地的友方冒险者 + 围攻的怪物；带回殖民地 → 完成。
   AbRun(AK("Rescue"),1,(c,m)=>{
    var npc=c.target;bool downed=npc!=null && npc.Downed && !npc.Dead && !npc.HostileTo(Faction.OfPlayer);bool besieged=m.mapPawns.AllPawnsSpawned.Any(p=>p.RaceProps.Animal && p.HostileTo(Faction.OfPlayer));
    AdventureBoard.Tick(c);bool notYet=!c.done;
    npc.DeSpawn();GenSpawn.Spawn(npc,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);akbMade.Add(npc);AdventureBoard.Tick(c);
    Check(downed && besieged && notYet && c.done && c.quest.State==QuestState.EndedSuccess,"[1.134.0] rescue: a downed friendly adventurer besieged by monsters; brought home → success");});
   // 9. 寻回失物：遗物在地点上；带回殖民地 → 完成（遗物交还）。
   AbRun(AK("Recover"),1,(c,m)=>{
    var relic=c.objective;bool there=relic!=null && relic.Spawned && relic.Map==m;bool guarded=m.mapPawns.AllPawnsSpawned.Any(p=>p.HostileTo(Faction.OfPlayer));
    relic.DeSpawn();GenPlace.TryPlaceThing(relic,map.Center,map,ThingPlaceMode.Near);AdventureBoard.Tick(c);
    Check(there && guarded && c.done && relic.Destroyed && c.quest.State==QuestState.EndedSuccess,"[1.134.0] recover: a guarded relic; carried home → success and the relic is returned");});
   // 10. 调查闹鬼废墟：亡灵 + 亡灵之源；摧毁亡灵之源 → 完成。
   AbRun(AK("Haunted"),1,(c,m)=>{
    var src=c.objective;bool spawned=src!=null && src.Spawned;bool undead=m.mapPawns.AllPawnsSpawned.Any(p=>p.HostileTo(Faction.OfPlayer) && QuestBoardMonsters.UndeadKinds.Contains(p.kindDef.defName));
    AdventureBoard.Tick(c);bool notYet=!c.done;src.Destroy();AdventureBoard.Tick(c);
    Check(spawned && undead && notYet && c.done && c.quest.State==QuestState.EndedSuccess,"[1.134.0] haunted ruins: undead and an undead source; destroying the source → success");});
   // 11. 过期：7 天期限一过 → 失败，名声 -3，地点撤掉。
   {var c=AdventureBoard.MakeOffer(AK("Lair"),0,map);st.offers.Add(c);AdventureBoard.Accept(c);st.renown=10;var site=c.site;c.deadline=AdventureBoard.Now-1;AdventureBoard.Tick(c);
    Check(c.done && c.quest.State==QuestState.EndedFailed && st.renown==7 && site.Destroyed && !st.active.Contains(c),"[1.134.0] expiry: past the 7-day deadline → fail, renown -3, site removed");}
   // 12. 湖畔委托（委托方魔力湖泊）：报酬里有一阶湖印结晶，完成后好感 +10～15。
   {var f=ManaLake.Faction;if(f!=null && f.HostileTo(Faction.OfPlayer))f.TryAffectGoodwillWith(Faction.OfPlayer,50,false,false);
    var c=AdventureBoard.MakeOffer(AK("Lair"),1,map,true);bool seal=c!=null && c.items.Any(n=>LakeSeals.Ext(LakeSeals.AbilityOf(DefDatabase<ThingDef>.GetNamedSilentFail(n)))?.tier==1);
    int gw0=f==null?0:f.PlayerGoodwill;st.offers.Add(c);AdventureBoard.Accept(c);var site=c.site;
    try{c.objective=null;c.mapTick=AdventureBoard.Now-GenDate.TicksPerHour*2;AdventureBoard.Complete(c);}finally{if(site!=null && !site.Destroyed)site.Destroy();}
    int gw1=f==null?0:f.PlayerGoodwill;
    Check(f==null || seal && gw1-gw0>=10 && gw1-gw0<=15 && AdventureBoard.Title(c).StartsWith("湖畔委托"),"[1.134.0] lakeside contract: a tier-1 seal crystal in the reward and mana lake goodwill +"+(gw1-gw0));}
   // 13. 传奇委托：名声「传奇」时可以生成（贝希尔 / 死亡骑士 / 真龙其一），危险度致命。
   {st.renown=95;var c=AdventureBoard.MakeOffer(AK("Legend"),3,map);bool ok=c==null?!AK("Legend").Worker.CanOffer(new AdventureContract()):c.lead!=null && c.danger==3 && !c.Worker.Enemies(c).NullOrEmpty();
    int legends=0;for(int i=0;i<20;i++){AdventureBoard.Refresh();legends+=st.offers.Count(o=>o.kind.legendary);}st.renown=0;
    Check(ok && legends>0,"[1.134.0] legendary hunts appear at renown ≥ 90 ("+(c==null?"none":c.Worker.Enemies(c))+", "+legends+" in 20 refreshes)");}
  }

  // ---------- 存读档 ----------
  void PrepareAdventureBoardSave(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;
   try{
    AbEnsureBoard();var st=AdventureBoard.State;st.renown=37;AdventureBoard.Refresh();
    var c=st.offers[0];AdventureBoard.Accept(c);akbSavedActiveId=c.id;akbSavedOffers=st.offers.Count;akbSavedRenown=st.renown;
   }finally{Adventurer.TestSuppressed=sup;}
  }
  void CheckAdventureBoardAfterLoad(){
   var st=AdventureBoard.State;var c=st.active.FirstOrDefault(x=>x.id==akbSavedActiveId);
   Check(st.renown==akbSavedRenown && st.offers.Count==akbSavedOffers && c!=null && c.site!=null && !c.site.Destroyed && c.quest!=null && c.quest.State==QuestState.Ongoing && c.kind!=null && c.deadline>0,
    "[1.134.0] save/reload keeps renown ("+st.renown+"), offers ("+st.offers.Count+") and the accepted contract with its site");
  }
 }
}
