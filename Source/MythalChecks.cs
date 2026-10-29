using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
namespace LichPhylactery {
 // 1.60.0：Test.ps1 -Only Mythal（迷锁地牢）：定义与贴图、数值公式、放门、沉睡→开启、楼层生成、规则、停留时间、通关奖励与回程门、坍塌与封闭、
 // 碎片兑换与重铸、封闭结束、撤退回卷、全员倒地驱逐、里程碑首领、迷锁泉、Isekai 桥、存读档（含口袋地图）。
 public partial class LichChecks {
  static string myGateId,mySaved;
  static string MySummary(Building_MythalGate g)=>g.state+"/"+g.floor+"/"+g.best+"/"+g.theme?.defName+"/"+g.rule?.defName+"/"+g.guardians.Count+"/"+(g.PocketMap!=null)+"/"+g.timeInside.Count+"x"+g.timeInside.Values.Sum();

  void CheckMythal(){
   int Now()=>Find.TickManager.TicksGame;
   // 1. 定义、贴图
   var defs=new[]{Mythal.GateDef.Def,Mythal.ExitDef.Def,Mythal.WallDef.Def,Mythal.LampDef.Def,Mythal.FountainDef.Def,Mythal.ShardDef.Def};
   var themes=DefDatabase<MythalThemeDef>.AllDefsListForReading;var rules=DefDatabase<MythalRuleDef>.AllDefsListForReading;
   bool tex=new[]{"GateOpen","GateSealed","Exit","Lamp","Fountain","Shard","Wall_Atlas"}.All(n=>ContentFinder<Texture2D>.Get("LP/Mythal/"+n,false)!=null);
   Check(defs.All(d=>d!=null && !d.ConfigErrors().Any()) && Mythal.FloorGen.Def!=null && tex && themes.Count>=8 && themes.Count(t=>t.Usable && t.minFloor<=1)>=2 && rules.Count>=7 && rules.Where(r=>r.defName!="LP_MythalRule_None").All(r=>r.hediff!=null) && Mythal.GateDef.Def.portal?.pocketMapGenerator==Mythal.FloorGen.Def && Mythal.ShardDef.Def.tradeability==Tradeability.None,
    "mythal defs load: gate (portal → LP_MythalFloor), exit, wall atlas, lamp, fountain, shard (not tradeable); "+themes.Count+" themes ("+themes.Count(t=>t.Usable)+" usable here), "+rules.Count+" rules; 7 textures");
   Check(LichMod.SettingsRight.Any(g=>g.name=="迷锁地牢" && g.draw==MythalSettings.Draw),"settings window has the 迷锁地牢 group");
   // 2. 公式
   bool iso=!Mythal.IsekaiActive;
   Check(Near(Mythal.BasePoints(10),500f) && Near(Mythal.BasePoints(30),1200f) && Mythal.LevelFloor(1)==1 && Mythal.LevelFloor(30)==10 && Mythal.LevelFloor(200)==40 && Near(Mythal.ClearXP(10),250f) && Mythal.Shards(10)==7 && Mythal.Shards(70)==38
    && Mythal.Milestone(10) && !Mythal.Milestone(5) && Mythal.Empowered(5) && !Mythal.Empowered(10) && Mythal.MapSize(29)==70 && Mythal.MapSize(30)==90 && (!iso || Near(Mythal.Points(10,map),500f)) && Mythal.Points(1,map)<=Mythal.BasePoints(1)+.01f,
    "danger points (150 + 35×floor: floor 10 = 500, 30 = 1200), enemy level floor ÷3 (cap 40), clear XP = points × 0.5, shards 5 + floor/5 (×2 from 70), milestone every 10, empowered every 5, map 70 → 90 at floor 30");
   Check(Mythal.RollRule(1)?.defName=="LP_MythalRule_None" && Mythal.RollRule(4)?.defName=="LP_MythalRule_None" && Enumerable.Range(0,30).All(_=>{var r=Mythal.RollRule(6,DefDatabase<MythalRuleDef>.GetNamed("LP_MythalRule_Fury"));return r!=null && r.defName!="LP_MythalRule_Fury" && r.minFloor<=6;}) && Mythal.RollTheme(1)?.minFloor<=1,
    "floors 1–4 have no rule; reroll never returns the excluded rule; floor-1 theme fits its floor window");
   // 3. 放门
   int before=Mythal.Gates.Count();
   var gate=Mythal.TryPlaceGate(map,map.Center);
   float dist=gate==null?-1:gate.Position.DistanceTo(map.Center);
   Check(before==0 && gate!=null && dist>=8f && dist<=45f && Mythal.TryPlaceGate(map,map.Center)==gate && Mythal.Gates.Count()==1 && CellRect.CenteredOn(gate.Position,3).Cells.All(c=>c.GetFirstPawn(map)==null),
    "no gate auto-placed in the test process; TryPlaceGate puts one 3×3 gate "+dist.ToString("0")+" cells from the colony and never a second one");
   // 4. 沉睡 → 开启
   bool en1=gate.IsEnterable(out string why1);bool dormant=gate.state==Mythal.StateDormant && !en1 && why1.Contains("苏醒");
   gate.openTick=Now();gate.MythalTick();
   Check(dormant && gate.state==Mythal.StateOpen && gate.floor==1 && gate.theme!=null && gate.rule?.defName=="LP_MythalRule_None" && gate.IsEnterable(out _),"gate sleeps until its open day (\""+why1+"\"), then opens floor 1 ("+gate.theme?.label+", rule "+gate.rule?.label+")");
   // 5. 生成第 1 层
   var pm=gate.GetOtherMap();var rooms=GenStep_MythalFloor.lastRooms;
   var hostiles=pm?.mapPawns.AllPawnsSpawned.Where(p=>p.HostileTo(Faction.OfPlayer)).ToList()??new List<Pawn>();
   bool reach=pm!=null && gate.entryCell.IsValid && gate.rewardCell.IsValid && pm.reachability.CanReach(gate.entryCell,gate.rewardCell,PathEndMode.OnCell,TraverseParms.For(TraverseMode.PassDoors));
   Check(pm!=null && pm.IsPocketMap && Mythal.IsFloorMap(pm) && pm.Size.x==70 && gate.exit!=null && gate.exit.Map==pm && gate.exit.def==Mythal.ExitDef.Def && rooms?.Count==9 && rooms.Count(r=>r.type=="guardian")==1 && rooms.Count(r=>r.type=="entry")==1
    && gate.guardians.Count>0 && gate.guardians.All(g=>g.Spawned && g.Map==pm && g.HostileTo(Faction.OfPlayer)) && hostiles.Count>=2 && hostiles.All(p=>p.GetLord()!=null) && reach
    && pm.listerThings.ThingsOfDef(Mythal.WallDef.Def).Count>1000 && pm.listerThings.ThingsOfDef(Mythal.LampDef.Def).Count>=8 && pm.roofGrid.Roofed(gate.rewardCell),
    "floor 1 pocket map: 70×70, 9 rooms (1 entry, 1 guardian), exit linked to the gate, "+hostiles.Count+" hostile monsters in room lords ("+string.Join(",",hostiles.Select(p=>p.kindDef.defName).Distinct())+"), guardian "+gate.guardians[0].kindDef.defName+", guardian room reachable through 3-wide corridors, walls / lamps / roof");
   // 6. 规则、停留时间
   var w=HtPawn();var h=Adventurer.Get(w)??Adventurer.TryAdd(w,1,true);
   Mythal.MoveTo(w,pm,gate.entryCell);
   var fury=DefDatabase<MythalRuleDef>.GetNamed("LP_MythalRule_Fury");gate.rule=fury;gate.MythalTick();
   var enemy=hostiles.First(p=>!p.Dead);
   bool ruled=w.health.hediffSet.HasHediff(fury.hediff) && enemy.health.hediffSet.HasHediff(fury.hediff) && gate.timeInside.TryGetValue(w,out int t1) && t1==Mythal.TickInterval;
   var bless=DefDatabase<MythalRuleDef>.GetNamed("LP_MythalRule_Blessing");gate.rule=bless;gate.ApplyRule(enemy);gate.ApplyRule(w);
   bool blessOk=!enemy.health.hediffSet.HasHediff(bless.hediff) && w.health.hediffSet.HasHediff(bless.hediff);
   Check(ruled && blessOk && w.GetStatValue(StatDefOf.MeleeHitChance)>0f,"rule state goes on everyone on the floor (狂暴 on colonist and monster), 祝福 only on our side; time inside counted per 250 ticks");
   gate.rule=fury;
   var home=HtCell(0,0);Mythal.MoveTo(w,map,home);Step(600);
   Check(!w.health.hediffSet.HasHediff(fury.hediff) && !w.health.hediffSet.HasHediff(bless.hediff),"rule states drop off within a few hundred ticks of leaving the floor");
   Mythal.MoveTo(w,pm,gate.entryCell);
   // 7. 通关
   float xp0=h?.xp??0f;int shards0=Mythal.ShardsOn(pm);var exit0=gate.exit;
   foreach(var g in gate.guardians.ToList())if(!g.Dead)g.Kill(null);
   gate.MythalTick();
   int exits=pm.listerThings.ThingsOfDef(Mythal.ExitDef.Def).Count;float gained=(h?.xp??0f)-xp0;
   Check(gate.state==Mythal.StateCleared && gate.best==1 && Mythal.ShardsOn(pm)==shards0+Mythal.Shards(1) && exits==2 && gate.exit==exit0 && (h==null || !Adventurer.Enabled || gained>=Mythal.ClearXP(1)*.5f),
    "killing the guardian clears floor 1: +"+gained.ToString("0")+" adventurer XP, "+Mythal.Shards(1)+" shards and loot in the guardian room, a return portal (2 exits), main exit link kept");
   // 8. 坍塌、封闭
   Mythal.MoveTo(w,map,home);gate.MythalTick();
   Check(gate.PocketMap==null && !Find.Maps.Contains(pm) && !Find.World.pocketMaps.Contains(pm.Parent) && gate.state==Mythal.StateSealed && gate.floor==2 && gate.sealUntil-Now()==Mythal.SealTicks && !gate.IsEnterable(out string why2) && (why2??"").Contains("重组") && gate.timeInside.Count==0,
    "everyone left: the cleared floor collapses, gate sealed "+(Mythal.SealTicks/GenDate.TicksPerDay)+" days, next is floor 2");
   // 9. 碎片兑换、重铸
   var sh=ThingMaker.MakeThing(Mythal.ShardDef.Def);sh.stackCount=20;GenPlace.TryPlaceThing(sh,gate.InteractionCell,map,ThingPlaceMode.Near);
   int s0=Mythal.ShardsOn(map);int med0=map.listerThings.ThingsOfDef(ThingDefOf.MedicineIndustrial).Sum(t=>t.stackCount);
   bool bought=gate.Buy(Mythal.Offers.First(o=>o.key=="medicine"));
   int med1=map.listerThings.ThingsOfDef(ThingDefOf.MedicineIndustrial).Sum(t=>t.stackCount);
   bool noReroll=gate.ExchangeOptions().All(o=>!o.Label.Contains("重铸下一层"));
   gate.floor=6;var oldRule=gate.rule=fury;bool rerolled=gate.ExchangeOptions().Any(o=>o.Label.Contains("重铸下一层")) && gate.Buy(Mythal.Offers.First(o=>o.key=="reroll")) && gate.rule!=oldRule;
   bool poor=!Mythal.Pay(map,999);
   Check(bought && Mythal.ShardsOn(map)==s0-4-5 && med1==med0+5 && noReroll && rerolled && poor,"shard exchange at the gate: 5 industrial medicine for 4 shards, rule reroll (5 shards, only from floor 5, never the same rule), can't pay without enough shards");
   gate.floor=2;gate.rule=Mythal.RollRule(2);
   // 10. 封闭结束
   gate.sealUntil=Now();gate.MythalTick();
   Check(gate.state==Mythal.StateOpen && gate.floor==2 && gate.IsEnterable(out _),"seal ends: floor 2 opens");
   // 11. 撤退回卷
   var pm2=gate.GetOtherMap();gate.lastVisitTick=Now()-Mythal.RewindTicks-1;gate.MythalTick();
   Check(pm2!=null && gate.PocketMap==null && !Find.Maps.Contains(pm2) && gate.state==Mythal.StateOpen && gate.floor==2,"floor left empty for 5 days rewinds (map destroyed, same floor regenerates on next entry)");
   // 12. 全员倒地驱逐
   var pm3=gate.GetOtherMap();Mythal.MoveTo(w,pm3,gate.entryCell);
   HealthUtility.DamageUntilDowned(w,false);gate.MythalTick();
   bool marked=w.Downed && gate.downedSince!=-1;
   gate.downedSince=Now()-Mythal.ExpelTicks-1;gate.MythalTick();
   Check(marked && w.Map==map && !w.Dead && w.needs.mood.thoughts.memories.GetFirstMemoryOfDef(Mythal.ExpelledThought.Def)!=null && gate.PocketMap==null && !Find.Maps.Contains(pm3) && gate.state==Mythal.StateOpen && gate.floor==2,
    "everyone down on the floor for 6 hours: spat out alive at the gate with 被迷锁驱逐, the floor rewinds"+(marked && w.Map==map && gate.PocketMap==null?"":" [marked="+marked+" downed="+w.Downed+" since="+gate.downedSince+" dead="+w.Dead+" map="+(w.MapHeld==map)+" pocket="+(gate.PocketMap!=null)+" thought="+(w.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(Mythal.ExpelledThought.Def)!=null)+" state="+gate.state+"]"));
   foreach(var inj in w.health.hediffSet.hediffs.Where(x=>x is Hediff_Injury || x is Hediff_MissingPart).ToList())w.health.RemoveHediff(inj);
   // 13. 里程碑首领
   gate.floor=10;gate.theme=null;gate.rule=null;
   var pm4=gate.GetOtherMap();var allBoss=Mythal.Bosses[0].Where(n=>n!="|").ToList();
   Check(pm4!=null && gate.guardians.Count==1 && allBoss.Contains(gate.guardians[0].kindDef.defName) && gate.FloorSummary().Contains(Mythal.BossTitles[0]),"floor 10 guardian is the milestone boss: "+gate.guardians.FirstOrDefault()?.kindDef.defName);
   gate.Rewind("test");
   // 14. 迷锁泉
   gate.floor=3;gate.theme=null;gate.rule=null;
   var pm5=gate.GetOtherMap();var fountain=pm5.listerThings.ThingsOfDef(Mythal.FountainDef.Def).FirstOrDefault();
   bool healed=false;
   if(fountain!=null){
    var fc=GenAdj.CellsAdjacent8Way(fountain).First(c=>c.InBounds(pm5) && c.Standable(pm5) && c.GetFirstPawn(pm5)==null);if(w.Spawned)w.DeSpawn();GenSpawn.Spawn(w,fc,pm5);
    var part=w.health.hediffSet.GetNotMissingParts().First(p=>p.def==BodyPartDefOf.Torso);
    var cut=HediffMaker.MakeHediff(HediffDefOf.Cut,w,part);cut.Severity=3f;w.health.AddHediff(cut);
    gate.MythalTick();
    healed=gate.healed.Contains(w) && !w.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(i=>!i.IsPermanent());
   }
   Check(fountain!=null && healed,"every 3rd floor has a 迷锁泉 rest room; walking up heals fresh wounds once per floor"+(fountain==null?" [no fountain]":" [healedList="+gate.healed.Contains(w)+" injuries="+w.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count()+"]"));
   Mythal.MoveTo(w,map,home);gate.Rewind("test");gate.floor=2;gate.theme=null;gate.rule=null;
   // 15. Isekai 桥
   Check(Mythal.IsekaiActive || !Mythal.GiveIsekaiXP(w,100f),"Isekai XP bridge does nothing without Isekai");
   foreach(var p in htMade)if(!p.Destroyed)p.Destroy();htMade.Clear();
  }
  // 16. 存读档（门的状态 + 正在进行的口袋地图）
  void PrepareMythalSave(){
   var gate=Mythal.Gates.First();
   gate.Open(false);var pm=gate.GetOtherMap();gate.timeInside.Clear();var col=map.mapPawns.FreeColonistsSpawned.FirstOrDefault();if(col!=null)gate.timeInside[col]=1234;
   myGateId=gate.ThingID;mySaved=MySummary(gate);
  }
  void CheckMythalAfterLoad(){
   var gate=map.listerThings.AllThings.OfType<Building_MythalGate>().FirstOrDefault(g=>g.ThingID==myGateId);
   string now=gate==null?"missing":MySummary(gate);
   var exit=gate?.exit as PocketMapExit;
   Check(now==mySaved && gate.PocketMap!=null && Mythal.IsFloorMap(gate.PocketMap) && exit!=null && exit.Map==gate.PocketMap && exit.entrance==gate && gate.guardians.All(g=>g.MapHeld==gate.PocketMap),"mythal gate keeps state, floor, theme, rule, guardians and its live pocket map through save/load ("+mySaved+" → "+now+" exit="+(exit!=null)+" exitMap="+(exit?.Map==gate?.PocketMap)+" entrance="+(exit?.entrance==gate)+" guards="+gate?.guardians.All(g=>g.MapHeld==gate.PocketMap)+")");
   gate.Rewind("test");
  }
 }
}
