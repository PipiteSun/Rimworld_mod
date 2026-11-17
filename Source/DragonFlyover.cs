using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using UnityEngine;

namespace LichPhylactery {
 // 1.31.0 龙过境。
 // 阶段一：地图条件“龙过境”1 天，天色略暗；每 2～4 小时龙影掠过一次（大轮廓 + 远处雷声），龙影 20 格内的野生动物惊逃离图。
 //   殖民者 -3“巨龙的阴影”。20% 在条件期间吐息一次：离殖民者 ≥6 格的点，半径 3 内建筑与物品受 60 力场伤害、人只被推开，留下力场弹坑。
 // 过境结束：25% 五色龙降落勒索（见 ChromaticDragons）。1.167.0 移除了力能龙的古龙降落、决斗、联姻。
 public static class DragonFlyover {
  public const int DurationTicks=GenDate.TicksPerDay,PushCells=2;
  public const float FleeRadius=20f,BreathRadius=3f,BreathMinColonistDistance=6f,BreathChance=.2f,BreathDamage=60f,ShadowSpeed=40f,ShadowScale=24f;
  static GameConditionDef condition;static FleckDef fleck;static ThingDef crater;static DamageDef force;
  public static GameConditionDef ConditionDef=>condition??(condition=DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_DragonShadow"));
  public static FleckDef ShadowFleck=>fleck??(fleck=DefDatabase<FleckDef>.GetNamedSilentFail("LP_DragonShadowFleck"));
  public static ThingDef CraterDef=>crater??(crater=DefDatabase<ThingDef>.GetNamedSilentFail("LP_ForceCrater"));
  public static DamageDef Force=>force??(force=DefDatabase<DamageDef>.GetNamed("LP_Force"));
  public static bool Active(Map map)=>map!=null && ConditionDef!=null && map.gameConditionManager.ConditionIsActive(ConditionDef);
  public static GameCondition_DragonShadow Condition(Map map)=>map?.gameConditionManager?.GetActiveCondition(ConditionDef) as GameCondition_DragonShadow;
  public static bool CanFire(Map map)=>LichMod.Settings.dragonFlyover && map!=null && map.IsPlayerHome && ConditionDef!=null && !Active(map);

  // ---------- 阶段一 ----------
  public static GameCondition_DragonShadow Start(Map map){
   var c=(GameCondition_DragonShadow)GameConditionMaker.MakeCondition(ConditionDef,DurationTicks);
   map.gameConditionManager.RegisterCondition(c);
   return c;
  }
  // 龙影：从地图一边横穿到另一边，行进的那一行上下 20 格内的野生动物惊逃。返回惊逃的数量。
  public static int Pass(Map map,int row=-1,bool? east=null){
   if(map==null)return 0;
   bool e=east??Rand.Bool;if(row<0)row=Rand.Range(10,Math.Max(11,map.Size.z-10));
   float dist=map.Size.x+30f;
   if(ShadowFleck!=null){
    var data=FleckMaker.GetDataStatic(new Vector3(e?-15f:map.Size.x+15f,0f,row),map,ShadowFleck,ShadowScale);
    data.velocityAngle=e?90f:270f;data.velocitySpeed=ShadowSpeed;data.rotation=e?0f:180f;data.solidTimeOverride=dist/ShadowSpeed;
    map.flecks.CreateFleck(data);
   }
   SoundDefOf.Thunder_OffMap?.PlayOneShotOnCamera(map);
   return ScareAnimals(map,row);
  }
  public static int ScareAnimals(Map map,int row){
   int n=0;
   foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()){
    if(p.Faction!=null || !p.RaceProps.Animal || p.Dead || p.Downed || p.InMentalState || Math.Abs(p.Position.z-row)>FleeRadius)continue;
    if(p.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.PanicFlee,"龙影".Loc(),true)==true)n++;
   }
   return n;
  }
  public static IntVec3 BreathCell(Map map){
   var colonists=map.mapPawns.FreeColonistsSpawned.ToList();
   bool Ok(IntVec3 c)=>c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && colonists.All(p=>p.Position.DistanceTo(c)>=BreathMinColonistDistance);
   var anchor=map.listerBuildings.allBuildingsColonist.Count>0?map.listerBuildings.allBuildingsColonist.RandomElement().Position:map.Center;
   if(CellFinder.TryFindRandomCellNear(anchor,map,30,Ok,out var cell) || CellFinder.TryFindRandomCell(map,Ok,out cell))return cell;
   return IntVec3.Invalid;
  }
  public static bool Breath(Map map){
   var cell=BreathCell(map);if(!cell.IsValid)return false;
   var r=Breathe(map,cell);
   Find.LetterStack.ReceiveLetter("巨龙的吐息".Loc(),"盘旋的巨龙朝地面喷出了一道力场吐息！落点附近的建筑和物品被撕裂了".Loc()+(r.hit>0?"（".Loc()+r.hit+"件）".Loc():"")+"，人只是被冲开".Loc()+(r.pushed>0?"（".Loc()+r.pushed+"人）".Loc():"")+"，地上留下了一个力场弹坑。".Loc(),LetterDefOf.NegativeEvent,new LookTargets(cell,map));
   return true;
  }
  // 吐息：半径 3 内建筑与物品受 60 点力场伤害（建筑抵消力场伤害对建筑的倍率，实际也是 60），人被推开 2 格不受伤。
  public static (int hit,int pushed) Breathe(Map map,IntVec3 center){
   int hit=0,pushed=0;var force=Force;
   foreach(var t in GenRadial.RadialDistinctThingsAround(center,map,BreathRadius,true).ToList()){
    if(t.Destroyed)continue;
    if(t is Pawn p){
     var dir=(p.Position-center).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(Rand.Range(-1f,1f),0f,Rand.Range(-1f,1f));
     var dest=DNDPush.Destination(map,p.Position,dir,PushCells);if(dest!=p.Position){DNDPush.Move(p,dest);pushed++;}
     continue;
    }
    if(t.def.category!=ThingCategory.Building && t.def.category!=ThingCategory.Item)continue;
    if(!t.def.useHitPoints || t is Filth)continue;
    float amount=BreathDamage;
    if(t.def.category==ThingCategory.Building && force.buildingDamageFactor>0f)amount/=force.buildingDamageFactor;
    t.TakeDamage(new DamageInfo(force,amount,0f,-1f));hit++;
   }
   // 弹坑：落点那一格不收污物（水、沼泽等）就放到 6 格内最近能放的格子；全是沼泽就不留。
   if(CraterDef!=null){foreach(var c in GenRadial.RadialCellsAround(center,6f,true).Where(x=>x.InBounds(map)))if(FilthMaker.TryMakeFilth(c,map,CraterDef,3))break;foreach(var c in GenAdj.CellsAdjacent8Way(new TargetInfo(center,map)).Where(c=>c.InBounds(map) && Rand.Chance(.5f)))FilthMaker.TryMakeFilth(c,map,CraterDef,1);}
   FleckMaker.Static(center,map,FleckDefOf.PsycastAreaEffect,6f);
   for(int i=0;i<6;i++)FleckMaker.ThrowDustPuffThick(center.ToVector3Shifted(),map,2.5f,new Color(.7f,.8f,1f));
   return (hit,pushed);
  }

  // 过境结束：1.52.0 起 25% 五色龙降落勒索（1.167.0 移除力能龙后不再有古龙降落）。
  public static void OnFlyoverEnd(Map map){if(map!=null && Find.Maps.Contains(map))Dragons.OnFlyoverEnd(map);}
 }

 // 地图条件：天色略暗，按时放龙影；20% 安排一次吐息；结束时掷五色龙降落。
 public class GameCondition_DragonShadow : GameCondition {
  public int nextShadowTick=-1,breathTick=-1,passes;public bool breathed,landingRolled;
  static readonly SkyColorSet Sky=new SkyColorSet(new Color(.55f,.58f,.66f),new Color(.30f,.32f,.38f),new Color(.60f,.62f,.70f),.9f);
  public override int TransitionTicks=>1200;
  public override float SkyTargetLerpFactor(Map map)=>GameConditionUtility.LerpInOutValue(this,TransitionTicks,.35f);
  public override SkyTarget? SkyTarget(Map map)=>new SkyTarget(.75f,Sky,1f,1f);
  public Map Map=>SingleMap;
  public override void Init(){
   base.Init();
   nextShadowTick=startTick+GenDate.TicksPerHour;
   if(Events2.Roll<DragonFlyover.BreathChance)breathTick=startTick+(int)(Rand.Range(.1f,.9f)*Duration);
  }
  public override void GameConditionTick(){
   base.GameConditionTick();
   var map=SingleMap;if(map==null)return;int now=Find.TickManager.TicksGame;
   if(nextShadowTick>=0 && now>=nextShadowTick){DragonFlyover.Pass(map);passes++;nextShadowTick=now+(int)(Rand.Range(2f,4f)*GenDate.TicksPerHour);}
   if(breathTick>=0 && !breathed && now>=breathTick){breathed=true;DragonFlyover.Breath(map);}
  }
  public override void End(){
   var map=SingleMap;base.End();
   if(!landingRolled){landingRolled=true;DragonFlyover.OnFlyoverEnd(map);}
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref nextShadowTick,"lpNextShadow",-1);Scribe_Values.Look(ref breathTick,"lpBreathTick",-1);Scribe_Values.Look(ref passes,"lpPasses");
   Scribe_Values.Look(ref breathed,"lpBreathed");Scribe_Values.Look(ref landingRolled,"lpLandingRolled");
  }
 }
 public class ThoughtWorker_DragonShadow : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.Faction==Faction.OfPlayer && p.Spawned && DragonFlyover.Active(p.Map);
 }
 public class IncidentWorker_DragonFlyover : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms)=>DragonFlyover.CanFire(parms.target as Map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;if(!DragonFlyover.CanFire(map))return false;
   DragonFlyover.Start(map);
   SendStandardLetter("龙过境".Loc(),"一条巨龙在这片天空上盘旋，接下来一天里它的影子会不时掠过大地。龙影经过的地方，野兽会惊慌逃离。\n\n殖民者看着头顶的巨龙心里发毛（心情-3）。\n\n它偶尔可能朝地面喷一口力场吐息。".Loc(),LetterDefOf.NeutralEvent,parms,LookTargets.Invalid);
   return true;
  }
 }
}
