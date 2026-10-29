using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.56.0「文化之神」第七节：神殿房间与神像。神像 Def 在 Bridges/Ideology/Defs/Ideo_Statues.xml。
 public static class IdeoTemple {
  public const float GrandImpressiveness=85f;
  static RoomRoleDef temple;
  public static RoomRoleDef Temple=>temple??(temple=DefDatabase<RoomRoleDef>.GetNamedSilentFail("LP_Temple"));
  public static bool IsTemple(Room r)=>r!=null && Temple!=null && r.Role==Temple;
  public static bool InTemple(Pawn p)=>p?.Spawned==true && IsTemple(p.GetRoom());
  public static bool InTemple(TargetInfo t)=>t.HasThing && t.Thing.Spawned && IsTemple(t.Thing.GetRoom());
  public static bool GrandTemple(Pawn p){var r=p?.Spawned==true?p.GetRoom():null;return IsTemple(r) && r.GetStat(RoomStatDefOf.Impressiveness)>=GrandImpressiveness;}
  // 这位神（null = 众神像）的神像是否立在任一家园地图。
  public static bool StatueAtHome(LP_DeityDef god){
   foreach(var m in Find.Maps){if(!m.IsPlayerHome)continue;var c=IdeoMapCache.For(m);if(c==null)continue;if(god==null?c.pantheonStatue:c.statues.Contains(god))return true;}
   return false;
  }
  public static LP_DeityDef StatueGod(Thing t)=>t?.def.GetModExtension<LP_StatueExtension>()?.deity;
  public static bool IsStatue(Thing t)=>t?.def.GetModExtension<LP_StatueExtension>()!=null;
 }

 // 房间里同时有圣坛和任一神像 → 神殿，分数高于原版礼拜室（原版：有文化祭坛时 max(2000, 75×祭坛数)）。
 public class RoomRoleWorker_LPTemple : RoomRoleWorker {
  public override float GetScore(Room room){
   if(!IdeoBridge.Active || room==null)return 0f;
   bool altar=false,statue=false;int n=0;var things=room.ContainedAndAdjacentThings;
   for(int i=0;i<things.Count;i++){var t=things[i];if(t.def==Faith.AltarDef)altar=true;else if(t.def.HasModExtension<LP_StatueExtension>()){statue=true;n++;}}
   return altar && statue?2500f+75f*n:0f;
  }
 }
}
