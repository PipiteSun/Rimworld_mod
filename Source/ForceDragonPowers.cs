using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 力能龙的主动能力：全部消耗力能，不用精神力，也不受心灵敏感度影响。
 public static class ForceDragonPowers {
  public const float BreathHalfAngle=30f;
  static readonly System.Reflection.MethodInfo breakShield=AccessTools.Method(typeof(CompShield),"Break");
  static readonly System.Reflection.MethodInfo breakInterceptor=AccessTools.Method(typeof(CompProjectileInterceptor),"BreakShieldHitpoints");
  public static List<IntVec3> BreathCells(Pawn caster,IntVec3 target){
   var cells=new List<IntVec3>();var map=caster?.Map;if(map==null)return cells;
   var origin=caster.Position;float range=ForceDragon.BreathRange(Math.Max(1,ForceDragon.Age(caster)));
   Vector3 dir=(target-origin).ToVector3();if(dir.sqrMagnitude<.01f)dir=caster.Rotation.FacingCell.ToVector3();
   foreach(var c in GenRadial.RadialCellsAround(origin,range,false)){
    if(!c.InBounds(map))continue;
    var v=(c-origin).ToVector3();
    if(Vector3.Angle(dir,v)>BreathHalfAngle || !GenSight.LineOfSight(origin,c,map,true))continue;
    cells.Add(c);
   }
   return cells;
  }
  // 打碎一个角色身上的护盾腰带（或自带的护盾），返回打碎的数量。
  public static int BreakShields(Pawn p){
   int broken=0;
   var shields=new List<CompShield>();
   if(p.apparel!=null)foreach(var a in p.apparel.WornApparel){var s=a.GetComp<CompShield>();if(s!=null)shields.Add(s);}
   var own=p.GetComp<CompShield>();if(own!=null)shields.Add(own);
   foreach(var s in shields)if(s.ShieldState==ShieldState.Active && s.Energy>0){breakShield.Invoke(s,null);broken++;}
   return broken;
  }
  public static bool BreakInterceptor(Thing t,Pawn caster){
   var ic=t.TryGetComp<CompProjectileInterceptor>();
   if(ic==null || !ic.Active)return false;
   breakInterceptor.Invoke(ic,new object[]{new DamageInfo(ForceDragon.Force,ic.HitPointsMax,0f,-1f,caster)});
   return true;
  }
  public static int Breath(Pawn caster,IntVec3 target){
   var map=caster.Map;int age=Math.Max(1,ForceDragon.Age(caster));float damage=ForceDragon.BreathDamage(age);
   int broken=0;var hit=new HashSet<Thing>();
   foreach(var c in BreathCells(caster,target)){
    foreach(var t in c.GetThingList(map).ToList()){
     if(t==caster || t.Destroyed || !hit.Add(t))continue;
     if(t is Pawn p){
      broken+=BreakShields(p);
      p.TakeDamage(new DamageInfo(ForceDragon.Force,damage,.3f,(c-caster.Position).AngleFlat,caster));
     }
     else if(BreakInterceptor(t,caster))broken++;
     else if(t.def.category==ThingCategory.Building && t.def.useHitPoints)t.TakeDamage(new DamageInfo(ForceDragon.Force,damage,0f,-1f,caster));
    }
    FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,.6f);
   }
   if(broken>0){
    ForceDragon.Gain(caster,broken*ForceDragon.ShieldForce);
    if(caster.Faction==Faction.OfPlayer)Messages.Message(caster.LabelShort+"的力场吐息打碎了"+broken+"面护盾，吞下了"+(broken*ForceDragon.ShieldForce).ToString("0")+"点力能。",caster,MessageTypeDefOf.PositiveEvent,false);
   }
   return broken;
  }
  public static float DevourValue(Thing t){
   if(t==null || !t.Spawned || t.def.category!=ThingCategory.Item)return 0;
   if(t.TryGetComp<CompShield>()!=null)return ForceDragon.BeltForce;
   if(t.TryGetComp<CompProjectileInterceptor>()!=null)return ForceDragon.DeviceForce;
   return 0;
  }
  public static float Devour(Pawn caster,Thing t){
   float value=DevourValue(t);if(value<=0)return 0;
   if(t.stackCount>1)t.SplitOff(1).Destroy();else t.Destroy();
   ForceDragon.Gain(caster,value);
   if(caster.Spawned)FleckMaker.Static(caster.Position,caster.Map,FleckDefOf.PsycastAreaEffect,1f);
   return value;
  }
  public static void Sphere(Pawn target,Thing instigator){
   if(target?.health==null || target.Dead)return;
   var old=target.health.hediffSet.GetFirstHediffOfDef(ForceDragon.Sphere);if(old!=null)target.health.RemoveHediff(old);
   target.health.AddHediff(ForceDragon.Sphere);
   target.stances?.stunner?.StunFor(ForceDragon.SphereTicks,instigator,false,false,false);
   target.pather?.StopDead();
   if(target.Spawned)FleckMaker.Static(target.Position,target.Map,FleckDefOf.PsycastAreaEffect,1.5f);
  }
  public static bool Telekinetic(Pawn caster,Pawn target){
   if(target==null || target.Dead || !target.Spawned || caster?.Map==null || target.Map!=caster.Map)return false;
   Sphere(target,caster);
   var map=caster.Map;
   var dest=GenAdj.CellsAdjacent8Way(caster).Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null).OrderBy(c=>c.DistanceToSquared(target.Position)).DefaultIfEmpty(IntVec3.Invalid).First();
   if(!dest.IsValid)return false;
   target.Position=dest;target.Notify_Teleported(true,false);
   return true;
  }
  public static List<IntVec3> WallCells(Pawn caster,IntVec3 center){
   int length=ForceDragon.WallLength(Math.Max(1,ForceDragon.Age(caster)));
   var d=center-caster.Position;
   var step=Math.Abs(d.x)>=Math.Abs(d.z)?IntVec3.North:IntVec3.East;
   var cells=new List<IntVec3>();
   for(int i=-length/2;i<=length/2;i++)cells.Add(center+step*i);
   return cells;
  }
  public static bool CanHoldWall(IntVec3 c,Map map)=>c.InBounds(map) && c.Walkable(map) && c.GetEdifice(map)==null && c.GetFirstPawn(map)==null && c.GetFirstItem(map)==null;
  public static List<ForceWall> SpawnWalls(IEnumerable<IntVec3> cells,Map map,int ticks){
   var walls=new List<ForceWall>();var def=DefDatabase<ThingDef>.GetNamed("LP_ForceWall");
   foreach(var c in cells){
    if(!CanHoldWall(c,map))continue;
    var wall=(ForceWall)ThingMaker.MakeThing(def);wall.ticksLeft=ticks;
    GenSpawn.Spawn(wall,c,map);walls.Add(wall);
   }
   return walls;
  }
  public static List<ForceWall> Wall(Pawn caster,IntVec3 center)=>SpawnWalls(WallCells(caster,center),caster.Map,ForceDragon.WallTicks);
  public static List<ForceWall> Cage(Pawn target){
   // 1.45.0 自由行动戒指：力场囚笼困不住。
   if(Items.Worn(target,"LP_RingFreeAction")!=null){Items.Mote(target,"挣脱力场囚笼",new UnityEngine.Color(.7f,.9f,1f));return new List<ForceWall>();}
   target.pather?.StopDead();
   return SpawnWalls(GenAdj.CellsAdjacent8Way(target).ToList(),target.Map,ForceDragon.CageTicks);
  }
 }

 public class ForceWall : Building {
  public int ticksLeft=ForceDragon.WallTicks;
  protected override void Tick(){base.Tick();if(--ticksLeft<=0 && !Destroyed)Destroy();}
  public override string GetInspectString(){var s=base.GetInspectString();var line="将在"+(ticksLeft>=2500?(ticksLeft/2500f).ToString("0.#")+"小时":(ticksLeft/60f).ToString("0")+"秒")+"后消散。";return string.IsNullOrEmpty(s)?line:s+"\n"+line;}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticksLeft,"forceWallTicksLeft",ForceDragon.WallTicks);}
 }

 public class CompProperties_ForceDragonAbility : CompProperties_AbilityEffect {
  public string effect;public float cost;
  public CompProperties_ForceDragonAbility(){compClass=typeof(CompForceDragonAbility);}
 }
 public class CompForceDragonAbility : CompAbilityEffect {
  CompProperties_ForceDragonAbility Props=>(CompProperties_ForceDragonAbility)props;
  Pawn Caster=>parent.pawn;
  public override bool GizmoDisabled(out string reason){
   if(!ForceDragon.Active(Caster)){reason="需要力能龙之心。";return true;}
   if(Props.cost>0 && ForceDragon.Points(Caster)+.001f<Props.cost){reason="力能不足：需要"+Props.cost.ToString("0")+"点，当前"+ForceDragon.Points(Caster).ToString("0")+"点。";return true;}
   return base.GizmoDisabled(out reason);
  }
  public string Invalid(LocalTargetInfo target){
   var map=Caster?.Map;if(map==null)return "施法者不在地图上。";
   switch(Props.effect){
    case "breath":
     if(!target.Cell.IsValid || !target.Cell.InBounds(map))return "需要地图上的目标位置。";
     if(target.Cell.DistanceTo(Caster.Position)>ForceDragon.BreathRange(Math.Max(1,ForceDragon.Age(Caster)))+.5f)return "超出当前龙龄的吐息射程（"+ForceDragon.BreathRange(Math.Max(1,ForceDragon.Age(Caster))).ToString("0.#")+"格）。";
     return null;
    case "devour":
     return ForceDragonPowers.DevourValue(target.Thing)>0?null:"只能吞噬地上未装备的护盾腰带、护盾背包等力场装置。";
    case "wall":
     return target.Cell.IsValid && target.Cell.InBounds(map)?null:"需要地图上的目标位置。";
    default:
     var p=target.Pawn;
     if(p==null || p.Dead || !p.Spawned || p.Map!=map)return "需要地图上的存活角色。";
     if(p==Caster && Props.effect!="sphere")return "不能对自己使用。";
     return null;
   }
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   string reason=Invalid(target);
   if(reason!=null && throwMessages)Messages.Message(reason,MessageTypeDefOf.RejectInput,false);
   return reason==null && base.Valid(target,throwMessages);
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   if(Invalid(target)!=null || !ForceDragon.Pay(Caster,Props.cost))return;
   switch(Props.effect){
    case "breath":ForceDragonPowers.Breath(Caster,target.Cell);break;
    case "devour":ForceDragonPowers.Devour(Caster,target.Thing);break;
    case "sphere":ForceDragonPowers.Sphere(target.Pawn,Caster);break;
    case "wall":ForceDragonPowers.Wall(Caster,target.Cell);break;
    case "cage":ForceDragonPowers.Cage(target.Pawn);break;
    case "telekinetic":ForceDragonPowers.Telekinetic(Caster,target.Pawn);break;
   }
  }
  public override void DrawEffectPreview(LocalTargetInfo target){
   if(Caster?.Map==null || !target.Cell.IsValid)return;
   if(Props.effect=="breath")GenDraw.DrawFieldEdges(ForceDragonPowers.BreathCells(Caster,target.Cell));
   else if(Props.effect=="wall")GenDraw.DrawFieldEdges(ForceDragonPowers.WallCells(Caster,target.Cell));
  }
  public override string ExtraLabelMouseAttachment(LocalTargetInfo target)=>Props.cost>0?"力能 "+Props.cost.ToString("0")+" / "+ForceDragon.Points(Caster).ToString("0"):null;
 }
}
