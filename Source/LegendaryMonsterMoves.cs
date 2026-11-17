using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.161.0 传奇怪招牌连招（Peter：像龙那样飞起突袭再吐息）。由 Hediff_MonsterBody 每 60 刻调用；跳跃用原版 PawnFlyer，落地效果在本体 hediff 的 landing 字段里，落地后下一刻结算。
 //  贝希尔　钻地伏击：钻进土里，从 5～25 格外的目标脚边钻出 → 缠绕重压（身边 2 格钝击、力量豁免失败震晕 2 秒）→ 立刻闪电吐息。
 //  眼魔　　念动拖拽：把视线内最远的敌人拖到身前（力量豁免）→ 所有完好眼柄一齐射线。
 //  半巫妖　骨尘闪现：敌人贴身时炸开骨尘（3 格死灵伤害，体质豁免减半）→ 闪到 8～14 格外 → 诅咒 15 格内最多 3 人。
 //  死亡骑士 冥火冲锋：跃到 4～16 格外的敌人身边 → 落地震击（2 格钝击、力量豁免失败击退 2 格并震晕 1.5 秒）→ 落点燃起地狱火。
 public static class MonsterMoves {
  public const int LungeTicks=900,PullTicks=1200,BlinkTicks=900,ChargeTicks=900;
  public const float LungeMin=5f,LungeMax=25f,LandRadius=1.9f,LungeDamage=18f,PullMin=4f,BlinkTrigger=3f,BlinkMin=8f,BlinkMax=14f,BurstRadius=3f,BurstDamage=12f,ChargeMin=4f,ChargeMax=16f,ChargeDamage=22f;
  public const int LandStun=120,PullStun=60,ChargeStun=90,MaxCurses=3;
  public static int Leaps,Landings,Pulls,Blinks; // 自检计数
  public static bool TestNoFlyer; // 自检：直接瞬移，不生成 PawnFlyer

  static Hediff_MonsterBody Body(Pawn p)=>p?.health?.hediffSet?.hediffs.OfType<Hediff_MonsterBody>().FirstOrDefault();
  static IEnumerable<Pawn> Foes(Pawn p,float r)=>Monsters.Foes(p,r);
  static Pawn Target(Pawn p,float min,float max)=>Foes(p,max).Where(x=>x.Position.DistanceTo(p.Position)>=min).OrderByDescending(x=>Dragons.Is(x)?1:0).ThenBy(x=>x.Position.DistanceTo(p.Position)).FirstOrDefault();

  // 落到目标身边的空格（优先离出发点近的一侧）。
  public static IntVec3 LandCell(Pawn p,Pawn target){
   var map=p.Map;
   return GenAdj.CellsAdjacent8Way(target).Concat(GenRadial.RadialCellsAround(target.Position,2.9f,false))
    .Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null && !c.Fogged(map))
    .OrderBy(c=>c.DistanceTo(target.Position)).ThenBy(c=>c.DistanceTo(p.Position)).FirstOrDefault(IntVec3.Invalid);
  }
  public static bool Leap(Pawn p,IntVec3 dest,string landing){
   if(p?.Map==null || !p.Spawned || !dest.IsValid)return false;var body=Body(p);if(body==null)return false;
   var map=p.Map;body.landing=landing;Leaps++;
   if(TestNoFlyer){p.pather?.StopDead();p.Position=dest;p.Notify_Teleported(true,false);return true;}
   var flyer=DefDatabase<ThingDef>.GetNamedSilentFail("PawnFlyer");if(flyer==null){body.landing=null;return false;}
   try{p.pather?.StopDead();var f=PawnFlyer.MakeFlyer(flyer,p,dest,null,null);if(f==null){body.landing=null;return false;}GenSpawn.Spawn(f,p.Position,map);return true;} // MakeFlyer 先把小人移出地图：用事先记下的 map
   catch(Exception e){body.landing=null;Log.WarningOnce("[DND边缘大陆] 传奇怪跳跃失败：".Loc()+e.Message,74152301);return false;}
  }

  // ---------- 每 60 刻：各自的连招 ----------
  public static void Tick(Pawn p,Hediff_MonsterBody b,int now){
   if(b.landing!=null)return; // 还在空中
   switch(b.Kind){
    case Monsters.Behir:if(now>=b.moveTick && b.swallowed==null){var t=Target(p,LungeMin,LungeMax);if(t!=null && BehirLunge(p,t))b.moveTick=now+LungeTicks;}break;
    case Monsters.Beholder:if(now>=b.moveTick){var t=Foes(p,Monsters.RayRange).Where(x=>x.Position.DistanceTo(p.Position)>=PullMin && GenSight.LineOfSight(p.Position,x.Position,p.Map,true)).OrderByDescending(x=>x.Position.DistanceTo(p.Position)).FirstOrDefault();if(t!=null){b.moveTick=now+PullTicks;BeholderPull(p,t);b.rayTick=now+Monsters.RayTicks;}}break;
    case Monsters.Demilich:if(now>=b.moveTick && Foes(p,BlinkTrigger).Any()){if(DemilichBlink(p))b.moveTick=now+BlinkTicks;}break;
    case Monsters.DeathKnight:if(now>=b.moveTick){var t=Target(p,ChargeMin,ChargeMax);if(t!=null && KnightCharge(p,t))b.moveTick=now+ChargeTicks;}break;
   }
  }

  // ---------- 贝希尔：钻地伏击 ----------
  public static bool BehirLunge(Pawn p,Pawn t){
   var dest=LandCell(p,t);if(!dest.IsValid)return false;
   SpellVfx2.Monster("BehirBurrow",p.Map,p.DrawPos,dest.ToVector3Shifted());
   Items.Mote(p,"钻地伏击！".Loc(),new Color(.6f,.8f,1f));
   return Leap(p,dest,Monsters.Behir);
  }
  // ---------- 死亡骑士：冥火冲锋 ----------
  public static bool KnightCharge(Pawn p,Pawn t){
   var dest=LandCell(p,t);if(!dest.IsValid)return false;
   SpellVfx2.Monster("KnightLeap",p.Map,p.DrawPos,dest.ToVector3Shifted());Items.Mote(p,"冥火冲锋！".Loc(),new Color(1f,.4f,.2f));
   return Leap(p,dest,Monsters.DeathKnight);
  }
  // 落地：本体 hediff 每刻检查（不等 60 刻）。返回受影响的人数。
  public static int Land(Pawn p,Hediff_MonsterBody b){
   string k=b.landing;b.landing=null;if(p?.Map==null || !p.Spawned || k==null)return 0;Landings++;
   var map=p.Map;int n=0;
   if(k==Monsters.Behir){
    SpellVfx2.Monster("BehirSurface",map,p.DrawPos,p.DrawPos,LandRadius+.5f);
    int dc=Legendary.DC(p,Adventurer.Str);
    foreach(var v in Foes(p,LandRadius).ToList()){bool s=Legendary.Save(v,Adventurer.Str,dc,p);v.TakeDamage(new DamageInfo(DamageDefOf.Blunt,(s?.5f:1f)*LungeDamage*Dragons.DamageMult,.3f,-1f,p));if(!s && !v.Dead)v.stances?.stunner?.StunFor(LandStun,p,false,true,false);n++;}
    b.charged=true; // 钻出来马上吐息
    var t=Monsters.BreathTarget(p,Monsters.BehirBreathRange);if(t!=null){b.charged=false;Monsters.BehirBreath(p,t.Position);}
    Items.Mote(p,"缠绕重压！".Loc(),new Color(.6f,.8f,1f));
   }else if(k==Monsters.DeathKnight){
    SpellVfx2.Monster("KnightSlam",map,p.DrawPos,p.DrawPos,LandRadius+.5f);
    int dc=Legendary.DC(p,Adventurer.Str);
    foreach(var v in Foes(p,LandRadius).ToList()){
     bool s=Legendary.Save(v,Adventurer.Str,dc,p);v.TakeDamage(new DamageInfo(DamageDefOf.Blunt,(s?.5f:1f)*ChargeDamage*Dragons.DamageMult,.4f,-1f,p));n++;
     if(!s && !v.Dead && v.Spawned){var dest=DNDPush.Destination(map,v.Position,(v.Position-p.Position).ToVector3(),2);DNDPush.Move(v,dest);v.stances?.stunner?.StunFor(ChargeStun,p,false,true,false);}
    }
    foreach(var c in GenRadial.RadialCellsAround(p.Position,1.5f,false).Where(c=>c.InBounds(map)))if(Rand.Chance(.5f))FireUtility.TryStartFireIn(c,map,.3f,p);
    Items.Mote(p,"落地震击！".Loc(),new Color(1f,.4f,.2f));
   }
   return n;
  }

  // ---------- 眼魔：念动拖拽 + 眼柄齐射 ----------
  public static bool BeholderPull(Pawn p,Pawn t){
   if(p?.Map==null || t==null || !t.Spawned)return false;var map=p.Map;bool pulled=false;
   Items.Mote(p,"念动拖拽！".Loc(),new Color(.9f,.6f,1f));SpellVfx2.Monster("BeholderPull",map,p.DrawPos,t.DrawPos);
   if(!Legendary.Save(t,Adventurer.Str,Legendary.DC(p,Adventurer.Int),p)){
    int steps=Mathf.Max(0,Mathf.FloorToInt(t.Position.DistanceTo(p.Position))-2);
    var dest=DNDPush.Destination(map,t.Position,(p.Position-t.Position).ToVector3(),steps);
    if(dest!=t.Position){DNDPush.Move(t,dest);pulled=true;}
    t.stances?.stunner?.StunFor(PullStun,p,false,true,false);
   }
   Pulls++;
   int all=Mathf.Max(3,Monsters.LiveRays(p).Count/2); // 齐射：至少 3 道（完好眼柄一半以上）
   SpellVfx2.Monster("BeholderVolley",map,p.DrawPos,p.DrawPos);Monsters.FireRays(p,all);
   return pulled;
  }

  // ---------- 半巫妖：骨尘闪现 ----------
  public static bool DemilichBlink(Pawn p){
   if(p?.Map==null || !p.Spawned)return false;var map=p.Map;
   var near=Foes(p,BurstRadius).ToList();int dc=Legendary.DC(p,Adventurer.Int);
   SpellVfx2.Monster("DemilichBurst",map,p.DrawPos,p.DrawPos,BurstRadius);
   foreach(var v in near){bool s=Legendary.Save(v,Adventurer.Con,dc,p);v.TakeDamage(new DamageInfo(Monsters.Necrotic,(s?.5f:1f)*BurstDamage*Dragons.DamageMult,.3f,-1f,p));}
   var foes=Foes(p,30f).ToList();
   var dest=GenRadial.RadialCellsAround(p.Position,BlinkMax,false).Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null && !c.Fogged(map) && c.DistanceTo(p.Position)>=BlinkMin)
    .OrderByDescending(c=>foes.Count==0?0f:foes.Min(f=>f.Position.DistanceTo(c))+Rand.Value).FirstOrDefault(IntVec3.Invalid);
   if(!dest.IsValid)return false;
   p.pather?.StopDead();p.Position=dest;p.Notify_Teleported(true,false);Blinks++;
   SpellVfx2.Monster("DemilichArrive",map,p.DrawPos,p.DrawPos);Items.Mote(p,"骨尘闪现！".Loc(),new Color(.7f,.9f,.6f));
   int cursed=0;
   foreach(var v in Foes(p,15f).Where(x=>x.RaceProps.Humanlike && !Items.Has(x,"LP_DemilichCurse")).OrderBy(x=>x.Position.DistanceTo(p.Position)).Take(MaxCurses).ToList())
    {SpellVfx2.Monster("DemilichCurse",map,p.DrawPos,v.DrawPos);if(!Legendary.Save(v,Adventurer.Wis,dc,p)){Items.Give(v,"LP_DemilichCurse");cursed++;}}
   return true;
  }
 }
}
