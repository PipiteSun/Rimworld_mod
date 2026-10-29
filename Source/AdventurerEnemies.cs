using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.48.1 冒险者等级第二阶段：敌人等级（见 ArtSource/Leveling-1.48.1-spec.md 第一节）。
 // 袭击组：PawnGroupMakerUtility.GeneratePawns 最外层后缀（其他派系/黄昏之年处理都做完之后）。先按“成本 × (1 + 0.03 × (预计等级 − 1))”裁人，再按每人点数定等级。
 // 剧情敌人：剧情代码调 Story(p,level)，标记 storyLevel，不再被袭击公式改动、不受上限。
 public class LP_AdventurerKindExt : DefModExtension {
  public int levelBonus;public string primary,secondary,role;
  public int Primary=>Adventurer.AbilityIndex(primary);
  public int Secondary=>Adventurer.AbilityIndex(secondary);
 }

 public static class AdventurerEnemies {
  public const float PointsPerLevel=45f,CostPerLevel=.03f;public const int Noise=2,LeaderBonus=3;
  public static bool Coexisting=>Adventurer.EffectsHandedToIsekai;
  // 1.63.0：开关合并为一个，共存模式与否都读它（coexist 参数保留，不再区分）。Process / LevelLoose / Story 都经 On 判断。
  public static bool OnFor(Settings s,bool coexist)=>s==null || s.adventurer && s.adventurerEnemyLevels;
  public static bool On=>OnFor(LichMod.Settings,Coexisting);

  public static int ColonyMaxLevel(){
   int best=1;
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists){var h=Adventurer.Get(p);if(h!=null && h.level>best)best=h.level;}
   return best;
  }
  public static int Cap(){var s=LichMod.Settings;if(s==null || !s.adventurerEnemyCap)return Adventurer.MaxLevel;return Mathf.Clamp(ColonyMaxLevel()+s.adventurerEnemyCapOffset,1,Adventurer.MaxLevel);}
  // 1 + 每人点数 ÷ 45（取整）+ 加成 + 浮动，限 1～cap。
  public static int BaseLevel(float perPawn)=>1+Mathf.FloorToInt(Mathf.Max(0f,perPawn)/PointsPerLevel);
  public static int LevelFor(float perPawn,int bonus,int cap,int noise)=>Mathf.Clamp(BaseLevel(perPawn)+bonus+noise,1,Mathf.Clamp(cap,1,Adventurer.MaxLevel));
  public static bool IsLeader(Pawn p)=>p?.kindDef!=null && (p.kindDef.factionLeader || p.Faction!=null && p.Faction.leader==p);
  // 领队 +3；精英兵种按扩展（取较大的一个，不叠加）。
  public static int Bonus(Pawn p){int b=IsLeader(p)?LeaderBonus:0;var e=Adventurer.KindExt(p);if(e!=null)b=Math.Max(b,e.levelBonus);return b;}
  public static float CostFactor(int level)=>1f+CostPerLevel*(level-1);
  // 1.50.0：领队到 20 级后，每人点数超出 20 级门槛的部分每 60 点 +1 级（上限见 Legend.EnemyCap），成为传奇。noise 只影响 20 级以内。
  public static int Target(Pawn p,float per,int cap,int noise){
   int bonus=Bonus(p),l=LevelFor(per,bonus,cap,noise);
   if(Legend.EnemyOn && IsLeader(p) && LevelFor(per,bonus,cap,0)>=Adventurer.MaxLevel)l=Legend.LeaderLevel(Adventurer.MaxLevel,per,bonus,Legend.EnemyCap());
   return l;
  }
  static float Cp(Pawn p)=>p?.kindDef?.combatPower??0f;

  // 组里有冒险者状态、不是剧情等级的人。
  static List<Pawn> People(List<Pawn> list)=>list.Where(p=>p!=null && !p.Dead && Adventurer.Get(p) is Hediff_Adventurer h && !h.storyLevel).ToList();

  public static IEnumerable<Pawn> Process(IEnumerable<Pawn> source,PawnGroupMakerParms parms){
   var list=source?.ToList()??new List<Pawn>();
   if(list.Count==0 || parms?.faction==null || !On)return list;
   var g=parms.groupKind;
   bool fighting=g==PawnGroupKindDefOf.Combat || g==PawnGroupKindDefOf.Settlement || g==PawnGroupKindDefOf.Settlement_RangedOnly;
   if(!fighting || !parms.faction.HostileTo(Faction.OfPlayer)){foreach(var p in People(list))Adventurer.Get(p).RaiseTo(Rand.RangeInclusive(1,3));return list;}
   int cap=Cap();
   if(g==PawnGroupKindDefOf.Combat)Trim(list,parms.points,cap);
   LevelGroup(People(list),parms.points,cap);
   return list;
  }
  public static void LevelGroup(List<Pawn> people,float points,int cap){
   if(people.Count==0)return;float per=points/people.Count;
   foreach(var p in people){var h=Adventurer.Get(p);Legend.RaiseEnemy(h,Target(p,per,cap,Rand.RangeInclusive(-Noise,Noise)));Styles.EnemyRoll(h);if(IsLeader(p))HeroTraits.EnemyLeader(p);} // 1.53.0 敌人 50% 战斗风格；1.58.0 领队 10% 紫色强力特性
  }
  // 事件自己生成的敌人（没有走袭击组）：每人点数按自己的战斗力。
  public static void LevelLoose(IEnumerable<Pawn> pawns){
   if(!On || pawns==null)return;int cap=Cap();
   foreach(var p in pawns){var h=Adventurer.Get(p);if(h==null || h.storyLevel)continue;Legend.RaiseEnemy(h,Target(p,Cp(p),cap,Rand.RangeInclusive(-Noise,Noise)));Styles.EnemyRoll(h);}
  }
  // 剧情首领：直接定等级（12～18），不受上限；1.50.0 起当前威胁点数 × 20% 超过 20 级门槛时按传奇规则升到 21～40。自测进程里被抑制时返回 null。
  public static Hediff_Adventurer Story(Pawn p,int level){
   if(!On || p==null)return null;
   var h=Adventurer.Get(p)??Adventurer.TryAdd(p);if(h==null)return null;
   h.storyLevel=true;Legend.RaiseEnemy(h,Legend.EnemyOn?Legend.StoryLevel(level,Legend.StoryPoints()):level);Styles.EnemyRoll(h);HeroTraits.EnemyBoss(p);return h; // 1.50.0：威胁点数够高时到传奇等级；1.58.0 剧情首领 50% 强力特性
  }

  // 战斗力折算：组的总成本按“战斗力 × (1 + 0.03 × (预计等级 − 1))”重算，比原来多就从最便宜的普通人形开始删，直到最接近原来的总战斗力。
  // 预计等级不带浮动；领队、精英、剧情、神明附体者、商人不删；至少留 1 个人形。
  public static int Trim(List<Pawn> list,float points,int cap){
   int removed=0;
   float target=list.Sum(Cp);
   for(int guard=0;guard<200;guard++){
    var people=People(list);if(people.Count<=1)break;
    float cost=Cost(list,people,points,cap);if(cost<=target)break;
    var cand=people.Where(Removable).OrderBy(Cp).FirstOrDefault();if(cand==null)break;
    var rest=list.Where(p=>p!=cand).ToList();
    float after=Cost(rest,people.Where(p=>p!=cand).ToList(),points,cap);
    if(Math.Abs(after-target)>=Math.Abs(cost-target))break;
    list.Remove(cand);Discard(cand);removed++;
   }
   return removed;
  }
  public static float Cost(List<Pawn> list,List<Pawn> people,float points,int cap){
   float per=people.Count==0?0f:points/people.Count;float sum=0f;
   foreach(var p in list)sum+=people.Contains(p)?Cp(p)*CostFactor(Target(p,per,cap,0))*Exalted.CostFactor(p):Cp(p); // 1.59.0 超凡 +4% / 神话 +8%
   return sum;
  }
  static bool Removable(Pawn p)=>!IsLeader(p) && (Adventurer.KindExt(p)?.levelBonus??0)<=0 && !p.kindDef.trader && Twilight.Vessel(p)==null && Necro.Construct(p)==null;
  static void Discard(Pawn p){if(p==null || p.Destroyed)return;if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}
 }

 // 最外层：派系后处理（FactionGroupPostProcess）、黄昏之年（Low）都包在里面先跑。
 [HarmonyPatch(typeof(PawnGroupMakerUtility),nameof(PawnGroupMakerUtility.GeneratePawns))]
 public static class AdventurerEnemyGroup {
  [HarmonyPriority(Priority.Last)]
  static void Postfix(PawnGroupMakerParms parms,ref IEnumerable<Pawn> __result){if(__result!=null && parms?.faction!=null && Adventurer.Enabled)__result=AdventurerEnemies.Process(__result,parms);}
 }
}
