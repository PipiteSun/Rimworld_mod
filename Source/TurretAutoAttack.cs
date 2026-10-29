using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 2026-10-28 热修（1.64.0）：本方小人原地待命（征召后站着、开火自由）时，自动攻击敌方炮塔。
 // 原版近战待命只会自动打身边的“人”，炮塔贴脸也不打；远程原版会打，但别的模组改了索敌时可能漏掉。这里在原版这一轮没出手时补一次。
 // 只打正在工作的炮塔（有电、有人操作、已苏醒、未被骇入——原版 ThreatDisabled 的判断）；关机的、还在休眠的机械集群炮塔不主动打，免得吵醒整个集群。
 public static class TurretAutoAttack {
  public static bool Live(Building_Turret t,Pawn p)=>t!=null && t.Spawned && t.Map==p.Map && t.HostileTo(p) && !t.ThreatDisabled(p) && AttackTargetFinder.IsAutoTargetable(t) && !t.Position.Fogged(t.Map);
  static System.Collections.Generic.IEnumerable<Building_Turret> Hostile(Pawn p)=>p.Map.attackTargetsCache.GetPotentialTargetsFor(p).Select(t=>t.Thing as Building_Turret).Where(t=>Live(t,p));
  public static Building_Turret RangedTarget(Pawn p,Verb v)=>Hostile(p).Where(t=>v.CanHitTarget(t)).OrderBy(t=>t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
  public static Building_Turret MeleeTarget(Pawn p)=>Hostile(p).FirstOrDefault(t=>p.Position.AdjacentTo8WayOrInside(t));
  // 返回 true = 开始攻击炮塔。
  public static bool Try(Pawn p,Job job){
   if(p==null || !p.Spawned || job?.def!=JobDefOf.Wait_Combat || p.Faction!=Faction.OfPlayer || p.Downed || p.InMentalState || p.stances.FullBodyBusy || p.WorkTagIsDisabled(WorkTags.Violent))return false;
   var v=p.CurrentEffectiveVerb;
   if(v!=null && !v.verbProps.IsMeleeAttack){
    if(p.drafter!=null && !p.drafter.FireAtWill)return false;
    var t=RangedTarget(p,v);return t!=null && p.TryStartAttack(t);
   }
   var m=MeleeTarget(p);return m!=null && p.meleeVerbs.TryMeleeAttack(m,null,false);
  }
 }
 [HarmonyPatch(typeof(JobDriver_Wait),"CheckForAutoAttack")]
 public static class TurretAutoAttackPatch {static void Postfix(JobDriver_Wait __instance){TurretAutoAttack.Try(__instance.pawn,__instance.job);}}
}
