using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 共享组件：沿方向把一个角色推开最多 cells 格（夺心魔念力投掷、牛头人蛮牛冲撞的击退）。
 // 遇到不可站立的格子、其他角色或视线被挡就停在前一格，不破坏建筑。
 public static class DNDPush {
  public static IntVec3 Destination(Map map,IntVec3 from,Vector3 dir,int cells){
   dir.y=0;if(dir.sqrMagnitude<.0001f)return from;dir.Normalize();
   var dest=from;
   for(int i=1;i<=cells;i++){var c=(from.ToVector3Shifted()+dir*i).ToIntVec3();if(!c.InBounds(map) || !c.Standable(map) || (c.GetFirstPawn(map)!=null && c!=from) || !GenSight.LineOfSight(from,c,map,true))break;dest=c;}
   return dest;
  }
  public static void Move(Pawn p,IntVec3 dest){if(dest==p.Position || !p.Spawned)return;p.pather?.StopDead();p.Position=dest;p.Notify_Teleported(true,false);}
 }
}
