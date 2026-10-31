using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.58.0 强力特性（见 ArtSource/TraitsFeats-1.58.0-spec.md 第一节）：金色英雄之资系、紫色加点效率与强化天赋、金色天命、青色传说事迹。
 // 名额：金 / 紫 / 1.51.0 的四个稀有天赋共用 exclusionTag LP_PowerTrait，每人最多一个；commonality 0，生成后缀（TalentGenerate）里自己掷一次。
 // 六维 / 传奇倍率走 LP_AdventurerAffinityExt（挂在 TraitDef 上），在 Affinity.ScoreMult / LegendMult 里和种族倍率取较大，不相乘。
 // 热路径（伤害、命中、闪避、射程）先查按 pawn 缓存的 HeroInfo：没有 1.58 特性的人缓存成 null，一次字典查找就返回（特性变动时失效）。
 // 规则在 HeroTraitsCombat.cs（战斗挂钩）、HeroDestiny.cs（天命）、HeroDeeds.cs（传说事迹）；存档数据在隐藏状态 Hediff_HeroRecord。
 [DefOf]
 public static class HeroDefOf {
  public static TraitDef LP_HeroicSpark,LP_Paragon,LP_GiftedScore,LP_DefyFate,LP_Hardened,LP_Edge,LP_Insight,LP_Diligent,LP_LegendSeed;
  public static TraitDef LP_TotemSpirit,LP_FavoredEnemy,LP_Attuner,LP_ElementTouched,LP_SpellWarded,LP_Duelist;
  public static TraitDef LP_Prophesied,LP_PactDestiny,LP_WeaveChild,LP_DivineChampion;
  public static TraitDef LP_DeedDragonslayer,LP_Giantbane,LP_Lichbane,LP_DeedGodslayer,LP_MindFortress,LP_DeathReturned,LP_LastStand;
  public static HediffDef LP_HeroRecord;
  static HeroDefOf(){DefOfHelper.EnsureInitializedInCtor(typeof(HeroDefOf));}
 }

 // 一个人身上 1.58 特性的汇总（缓存，不存档）。degree 0 = 没有；paragon / gifted 是属性下标（-1 = 没有）。
 public sealed class HeroInfo {
  public TraitDef power;public int degree;public TraitDef destiny;public int destinyDegree; // power = 最后一个金 / 紫（可能有多个）；destiny = 天命（最多一个）
  public bool spark,defy,hardened,edge,insight,diligent,seed,attuner,warded,duelist,prophesied,weave;
  public int paragon=-1,gifted=-1,totem,favored,element,pact,champion;
  public bool dragonslayer,giantbane,lichbane,godslayer,fortress,returned,laststand;
  public int capAll,legendPerLevel,asiBonus,capHigherBonus;public float legendAll=1f;public bool removeWeak;
  public readonly float[] legendStrong=new float[Legend.Count],legendFloor=new float[Legend.Count];public readonly string[] legendStrongBy=new string[Legend.Count];
  public HashSet<int> extraAsi;public List<int> capHigherOf;public List<KeyValuePair<List<int>,int>> capMore;
  public Hediff_HeroRecord rec;
  // 1.58.1 专长（冒险者状态里的），同样走这个缓存：专长变动时失效。
  public bool brawler,crafter,observant,musician,ritual,magma,bloodRegen,infernal,elven,dragonFear,oath,bountiful,dwarven,poisoner;public int adept;
  public bool gwm,sharp,mageSlayer;public int weaponType; // 1.67.0 巨武器大师 / 神射手伤害、斩刺碎（1/2/3）、法师杀手
  public bool Destiny=>prophesied || pact>0 || weave || champion>0;
  public bool Gold=>power!=null && HeroTraits.IsGold(power);
  public int Stage=>rec?.stage??0;
 }

 public static class HeroTraits {
  public const string PowerTag="LP_PowerTrait";
  public const float GiftedMult=1.5f,ParagonMult=2.5f,ParagonLegendMult=2f,TypeCap=.6f;
  public const int ParagonCap=26,ManifestLevel=10,SeedLegendPoints=10;
  // 极致：每项属性对应的两个传奇项（力→近战、血量；敏→远程、近战；体→血量、承受；智→学习、工作；感→心灵、回复；魅→心灵、工作）。
  public static readonly int[,] ParagonLegend={{Legend.Melee,Legend.Health},{Legend.Ranged,Legend.Melee},{Legend.Health,Legend.Tough},{Legend.Learn,Legend.Work},{Legend.Psychic,Legend.Recovery},{Legend.Psychic,Legend.Work}};
  public static readonly string[] FavoredLabels={"","龙","巨人","不死","邪魔","异怪","野兽","仇敌派系"};
  public static readonly string[] ElementLabels={"","火","冰","雷","酸"};
  public static readonly string[] PactLabels={"","邪魔","妖精","旧日"};

  // ---------- 特性分类 ----------
  static HashSet<TraitDef> gold,purple,deeds,all;
  static void Sets(){
   if(all!=null)return;
   gold=new HashSet<TraitDef>{HeroDefOf.LP_HeroicSpark,HeroDefOf.LP_Paragon,HeroDefOf.LP_Prophesied,HeroDefOf.LP_PactDestiny,HeroDefOf.LP_WeaveChild,HeroDefOf.LP_DivineChampion};
   purple=new HashSet<TraitDef>{HeroDefOf.LP_GiftedScore,HeroDefOf.LP_DefyFate,HeroDefOf.LP_Hardened,HeroDefOf.LP_Edge,HeroDefOf.LP_Insight,HeroDefOf.LP_Diligent,HeroDefOf.LP_LegendSeed,HeroDefOf.LP_TotemSpirit,HeroDefOf.LP_FavoredEnemy,HeroDefOf.LP_Attuner,HeroDefOf.LP_ElementTouched,HeroDefOf.LP_SpellWarded,HeroDefOf.LP_Duelist};
   foreach(var d in MoreTraits.Purple)purple.Add(d); // 1.79.0 新增蓝 / 紫（HeroTraitsMore.cs）
   deeds=new HashSet<TraitDef>{HeroDefOf.LP_DeedDragonslayer,HeroDefOf.LP_Giantbane,HeroDefOf.LP_Lichbane,HeroDefOf.LP_DeedGodslayer,HeroDefOf.LP_MindFortress,HeroDefOf.LP_DeathReturned,HeroDefOf.LP_LastStand};
   gold.Remove(null);purple.Remove(null);deeds.Remove(null);
   all=new HashSet<TraitDef>(gold.Concat(purple).Concat(deeds));
  }
  public static bool Is(TraitDef d){if(d==null)return false;Sets();return all.Contains(d);}
  public static bool IsGold(TraitDef d){Sets();return d!=null && gold.Contains(d);}
  public static bool IsPurple(TraitDef d){Sets();return d!=null && purple.Contains(d);}
  public static bool IsDeed(TraitDef d){Sets();return d!=null && deeds.Contains(d);}
  public static bool IsDestiny(TraitDef d)=>d!=null && (d==HeroDefOf.LP_Prophesied || d==HeroDefOf.LP_PactDestiny || d==HeroDefOf.LP_WeaveChild || d==HeroDefOf.LP_DivineChampion);
  // 占名额的：金、紫、1.51.0 天赋。
  public static bool IsSlot(TraitDef d)=>d!=null && (IsGold(d) || IsPurple(d) || Talents.Is(d) || DndTraits.IsCurse(d)); // 1.62.0 诅咒之力（红）也占名额
  public static IEnumerable<TraitDef> Gold{get{Sets();return gold;}}
  public static IEnumerable<TraitDef> Purple{get{Sets();return purple;}}
  public static IEnumerable<TraitDef> Deeds{get{Sets();return deeds;}}
  public static Trait SlotTrait(Pawn p){var l=p?.story?.traits?.allTraits;if(l!=null)for(int i=0;i<l.Count;i++)if(IsSlot(l[i].def))return l[i];return null;}
  public static bool HasSlot(Pawn p)=>SlotTrait(p)!=null;
  // 1.66.0 热修：不再只能有一个。强力特性（金 / 紫 / 红 / 四个稀有天赋）每人最多 Max 个（设置，默认 3）；天命之间、稀有天赋之间、诅咒之间仍各自互斥（exclusionTags）。
  public static int Count(Pawn p){var l=p?.story?.traits?.allTraits;int n=0;if(l!=null)for(int i=0;i<l.Count;i++)if(IsSlot(l[i].def))n++;return n;}
  public static int Max=>Mathf.Clamp(LichMod.Settings?.heroTraitMax??3,1,6);
  public static bool Full(Pawn p)=>Count(p)>=Max;
  public static float ExtraChance=>Mathf.Clamp01((LichMod.Settings?.heroTraitExtra??25)/100f);
  public static bool Has(Pawn p,TraitDef d)=>d!=null && p?.story?.traits?.HasTrait(d)==true;
  public static string Plain(Trait t)=>t==null?"":t.Label.StripTags();
  public static string Plain(TraitDef d,int degree=0){var dd=d?.DataAtDegree(degree);return dd==null?d?.defName??"":dd.label.StripTags();}

  // ---------- 缓存 ----------
  static readonly Dictionary<Pawn,HeroInfo> cache=new Dictionary<Pawn,HeroInfo>();
  public static HeroInfo Info(Pawn p){
   if(p==null)return null;
   if(cache.TryGetValue(p,out var i))return i;
   i=Build(p);cache[p]=i;return i;
  }
  public static void Invalidate(Pawn p){if(p!=null)cache.Remove(p);}
  public static void ClearCache(){cache.Clear();}
  public static int CacheCount=>cache.Count;
  public static int Cleanup(){var dead=cache.Keys.Where(p=>p==null || p.Destroyed || p.Discarded).ToList();foreach(var p in dead)cache.Remove(p);return dead.Count;}
  static HeroInfo Build(Pawn p){
   var list=p.story?.traits?.allTraits;
   HeroInfo i=Feats2.Build(p,null); // 1.58.1 专长
   if(list!=null)for(int k=0;k<list.Count;k++){
    var t=list[k];var d=t.def;if(!Is(d))continue;
    if(i==null)i=new HeroInfo();
    int g=t.Degree;
    if(IsGold(d) || IsPurple(d)){i.power=d;i.degree=g;}
    if(IsDestiny(d)){i.destiny=d;i.destinyDegree=g;}
    if(d==HeroDefOf.LP_HeroicSpark)i.spark=true;
    else if(d==HeroDefOf.LP_Paragon)i.paragon=Mathf.Clamp(g-1,0,5);
    else if(d==HeroDefOf.LP_GiftedScore)i.gifted=Mathf.Clamp(g-1,0,5);
    else if(d==HeroDefOf.LP_DefyFate)i.defy=true;
    else if(d==HeroDefOf.LP_Hardened)i.hardened=true;
    else if(d==HeroDefOf.LP_Edge)i.edge=true;
    else if(d==HeroDefOf.LP_Insight)i.insight=true;
    else if(d==HeroDefOf.LP_Diligent)i.diligent=true;
    else if(d==HeroDefOf.LP_LegendSeed)i.seed=true;
    else if(d==HeroDefOf.LP_TotemSpirit)i.totem=Mathf.Clamp(g,1,3);
    else if(d==HeroDefOf.LP_FavoredEnemy)i.favored=Mathf.Clamp(g,1,7);
    else if(d==HeroDefOf.LP_Attuner)i.attuner=true;
    else if(d==HeroDefOf.LP_ElementTouched)i.element=Mathf.Clamp(g,1,4);
    else if(d==HeroDefOf.LP_SpellWarded)i.warded=true;
    else if(d==HeroDefOf.LP_Duelist)i.duelist=true;
    else if(d==HeroDefOf.LP_Prophesied)i.prophesied=true;
    else if(d==HeroDefOf.LP_PactDestiny)i.pact=Mathf.Clamp(g,1,3);
    else if(d==HeroDefOf.LP_WeaveChild)i.weave=true;
    else if(d==HeroDefOf.LP_DivineChampion)i.champion=Mathf.Clamp(g,1,9);
    else if(d==HeroDefOf.LP_DeedDragonslayer)i.dragonslayer=true;
    else if(d==HeroDefOf.LP_Giantbane)i.giantbane=true;
    else if(d==HeroDefOf.LP_Lichbane)i.lichbane=true;
    else if(d==HeroDefOf.LP_DeedGodslayer)i.godslayer=true;
    else if(d==HeroDefOf.LP_MindFortress)i.fortress=true;
    else if(d==HeroDefOf.LP_DeathReturned)i.returned=true;
    else if(d==HeroDefOf.LP_LastStand)i.laststand=true;
    var e=d.GetModExtension<LP_AdventurerAffinityExt>();
    if(e!=null){
     i.capAll+=e.scoreCapOffset;i.legendPerLevel+=e.legendPointsPerLevelOffset;i.asiBonus=Math.Max(i.asiBonus,e.asiBonusPoints);
     i.legendAll=Math.Max(i.legendAll,e.legendAllFactor);if(e.removeWeak)i.removeWeak=true;
     if(e.extraAsiLevels!=null){if(i.extraAsi==null)i.extraAsi=new HashSet<int>();foreach(var l in e.extraAsiLevels)i.extraAsi.Add(l);}
     if(e.strongLegend!=null)foreach(var s in e.strongLegend){int x=Affinity.LegendIndex(s);if(x>=0 && e.legendStrongFactor>i.legendStrong[x]){i.legendStrong[x]=e.legendStrongFactor;i.legendStrongBy[x]=Plain(d,g);}}
     if(e.legendFloor!=null)foreach(var s in e.legendFloor){int x=Affinity.LegendIndex(s);if(x>=0)i.legendFloor[x]=Math.Max(i.legendFloor[x],e.legendFloorFactor);}
     if(e.capHigherOf!=null && e.capHigherBonus>0){var of=e.capHigherOf.Select(Adventurer.AbilityIndex).Where(x=>x>=0).ToList();if(i.capHigherOf==null){i.capHigherOf=of;i.capHigherBonus=e.capHigherBonus;}else(i.capMore??(i.capMore=new List<KeyValuePair<List<int>,int>>())).Add(new KeyValuePair<List<int>,int>(of,e.capHigherBonus));} // 1.79.0 银舌 / 神童：第二组起放进 capMore，各算各的
    }
   }
   if(i!=null && i.rec==null)i.rec=Record(p);
   return i;
  }

  // ---------- 记录（隐藏状态） ----------
  public static Hediff_HeroRecord Record(Pawn p){var list=p?.health?.hediffSet?.hediffs;if(list!=null)for(int k=0;k<list.Count;k++)if(list[k] is Hediff_HeroRecord r)return r;return null;}
  public static Hediff_HeroRecord EnsureRecord(Pawn p){
   var r=Record(p);if(r!=null || p?.health==null || HeroDefOf.LP_HeroRecord==null)return r;
   r=(Hediff_HeroRecord)HediffMaker.MakeHediff(HeroDefOf.LP_HeroRecord,p);p.health.AddHediff(r);
   Invalidate(p);return r;
  }

  // ---------- 加点规则（AdventurerAffinity / Hediff_Adventurer 调用） ----------
  // 六维倍率（1.81.0 热修5 拆开）：天赋·某项 ×1.5、极致 ×2.5 是天赋倍率（TalentMult，取较大，乘在整个效果上）；反骨把种族的笨拙拉回 ×1（RaceMult）。
  public static float TalentMult(Pawn p,int a,float m){
   var i=Info(p);if(i==null || a<0 || a>5)return m;
   if(i.gifted==a)m=Math.Max(m,GiftedMult);
   if(i.paragon==a)m=Math.Max(m,ParagonMult);
   return m;
  }
  public static float RaceMult(Pawn p,int a,float m){var i=Info(p);return i!=null && a>=0 && a<=5 && i.removeWeak && m<1f?1f:m;}
  public static float LegendMult(Pawn p,int x,float m){
   var i=Info(p);if(i==null || x<0 || x>=Legend.Count)return m;
   m=Math.Max(m,i.legendAll);
   if(i.legendStrong[x]>0f)m=Math.Max(m,i.legendStrong[x]);
   if(i.legendFloor[x]>0f)m=Math.Max(m,i.legendFloor[x]);
   if(i.paragon>=0 && (ParagonLegend[i.paragon,0]==x || ParagonLegend[i.paragon,1]==x))m=Math.Max(m,ParagonLegendMult);
   return m;
  }
  // 说明里写是谁拉高了倍率。
  public static string MultNote(Pawn p,int a,bool legend){
   var i=Info(p);if(i==null)return null;var parts=new List<string>();
   if(!legend){if(i.gifted==a)parts.Add("天赋 ×1.5");if(i.paragon==a)parts.Add("极致 ×2.5");if(i.removeWeak)parts.Add("反骨：笨拙按 ×1");}
   else{
    if(i.legendAll>1f)parts.Add("英雄之资 ×"+i.legendAll.ToString("0.##"));
    if(a>=0 && a<Legend.Count){if(i.legendStrong[a]>0f)parts.Add((i.legendStrongBy[a]??Plain(i.power,i.degree))+" ×"+i.legendStrong[a].ToString("0.##"));if(i.legendFloor[a]>0f)parts.Add("传说事迹 至少 ×"+i.legendFloor[a].ToString("0.##"));}
    if(i.paragon>=0 && a>=0 && (ParagonLegend[i.paragon,0]==a || ParagonLegend[i.paragon,1]==a))parts.Add("极致 ×2");
   }
   return parts.Count==0?null:string.Join("、",parts);
  }
  // 上限：英雄之资 +2、弑神者 +1（所有项）；极致那一项 26；灵光：智力、感知中较高的一项 +2。
  public static int CapBonus(Hediff_Adventurer h,int a){
   if(h==null || a<0 || a>5)return 0;var i=Info(h.pawn);if(i==null)return 0;
   int b=i.capAll;if(i.paragon==a)b+=ParagonCap-Adventurer.ScoreCap;
   int Higher(List<int> of,int bonus){if(of==null || bonus<=0 || !of.Contains(a))return 0;int best=-1;foreach(int x in of)if(best<0 || h.scores[x]>h.scores[best])best=x;return best==a?bonus:0;}
   b+=Higher(i.capHigherOf,i.capHigherBonus);
   if(i.capMore!=null)foreach(var g in i.capMore)b+=Higher(g.Key,g.Value);
   return b;
  }
  public static int AsiBonus(Pawn p)=>Info(p)?.asiBonus??0;
  public static bool ExtraAsi(Pawn p,int level){var i=Info(p);return i?.extraAsi!=null && i.extraAsi.Contains(level);}
  public static int LegendPerLevel(Pawn p)=>Info(p)?.legendPerLevel??0;
  public static bool Seed(Pawn p)=>Info(p)?.seed==true;

  // ---------- 设置 ----------
  public static bool TestSuppressed; // 自测进程：除强力特性检查组外不随机生成
  public static bool TestNotices;    // 自检时也发宿敌提示
  public static int? TestKind;       // 自检：代替生成时的掷骰
  public static bool On=>LichMod.Settings?.heroTraits??true;
  public static float Rate=>(LichMod.Settings?.heroTraitRate??100)/100f;
  public static bool EnemiesOn=>On && (LichMod.Settings?.heroTraitsEnemies??true);

  // ---------- 生成 ----------
  public enum Tier{Common,Medium,Legendary}
  // 普通：人类、半兽人和 1.57.0 的半身人、侏儒、地侏、半精灵、狗头人、地精三族；传奇：亲和表里的传奇种族和半神；其他 DND 种族（精灵、矮人一系等）算中等。
  public static readonly HashSet<string> CommonKeys=new HashSet<string>{Affinity.HumanKey,"LP_HalfOrc","LP_HalflingLightfoot","LP_HalflingStout","LP_RockGnome","LP_ForestGnome","LP_DeepGnome","LP_HalfElf","LP_Kobold","LP_Goblin","LP_Hobgoblin","LP_Bugbear"};
  public static Tier TierOf(Pawn p){
   if(p==null)return Tier.Common;
   if(Affinity.LegendaryRace(p) || Demigods.Is(p))return Tier.Legendary;
   return CommonKeys.Contains(Affinity.For(p).key)?Tier.Common:Tier.Medium;
  }
  public static float GoldChance(Tier t)=>t==Tier.Common?.01f:t==Tier.Medium?.004f:0f;
  public static float PurpleChance(Tier t)=>t==Tier.Common?.04f:t==Tier.Medium?.03f:.01f;
  // 1.51.0 的四个天赋并进同一次掷骰：普通 / 中等种族 2%（原来 commonality 0.15 在特性池里约这个量级），传奇种族不出（同 1.51.0）。
  public static float TalentChance(Tier t)=>t==Tier.Legendary?0f:.02f;
  public const int None=0,KindGold=1,KindPurple=2,KindTalent=3,KindCurse=4;
  public static int RollKind(Tier t,float rate,float r)=>RollKind(t,rate,r,0f);
  // 1.62.0：红色（诅咒之力）排在天赋后面，curse = 0 时不出（设置“诅咒之力”关闭）。
  public static int RollKind(Tier t,float rate,float r,float curse){
   float g=GoldChance(t)*rate,pu=PurpleChance(t)*rate,ta=TalentChance(t)*rate,cu=DndTraits.CurseChance(t)*rate*curse;
   if(r<g)return KindGold;if(r<g+pu)return KindPurple;if(r<g+pu+ta)return KindTalent;if(r<g+pu+ta+cu)return KindCurse;return None;
  }
  public static bool Eligible(Pawn p)=>p?.story?.traits!=null && p.RaceProps!=null && p.RaceProps.Humanlike && !p.RaceProps.IsMechanoid && !p.Dead && p.ageTracker!=null && p.ageTracker.AgeBiologicalYears>=Adventurer.MinAge && !Adventurer.Excluded(p) && !Legendary.Is(p);
  static bool Conflicts(Pawn p,TraitDef d){var l=p.story.traits.allTraits;for(int k=0;k<l.Count;k++)if(l[k].def==d || l[k].def.ConflictsWith(d) || d.ConflictsWith(l[k].def))return true;return false;}
  // 一项（同一特性的分支算一项）和它能选的 degree。
  public class Choice {public TraitDef def;public List<int> degrees;}
  public static List<Choice> Choices(Pawn p,int kind){
   var list=new List<Choice>();if(p?.story?.traits==null)return list;
   void Add(TraitDef d,IEnumerable<int> degs){if(d==null || Conflicts(p,d))return;var g=degs.ToList();if(g.Count>0)list.Add(new Choice{def=d,degrees=g});}
   var tier=TierOf(p);var aff=Affinity.For(p);
   switch(kind){
    case KindGold:
     Add(HeroDefOf.LP_HeroicSpark,new[]{0});Add(HeroDefOf.LP_Paragon,Enumerable.Range(1,6));
     Add(HeroDefOf.LP_Prophesied,new[]{0});Add(HeroDefOf.LP_PactDestiny,Enumerable.Range(1,3));Add(HeroDefOf.LP_WeaveChild,new[]{0});
     Add(HeroDefOf.LP_DivineChampion,ChampionDegrees(p));
     break;
    case KindPurple:
     if(tier!=Tier.Legendary){
      Add(HeroDefOf.LP_GiftedScore,Enumerable.Range(1,6).Where(g=>!aff.strong[g-1] && !HeroIsekai.StatTraitBlocks(p,g-1)));
      foreach(var d in new[]{HeroDefOf.LP_Hardened,HeroDefOf.LP_Edge,HeroDefOf.LP_Insight,HeroDefOf.LP_LegendSeed})Add(d,new[]{0});
     }
     // 传奇种族只抽反骨、勤勉和六个强化天赋。
     Add(HeroDefOf.LP_DefyFate,new[]{0});Add(HeroDefOf.LP_Diligent,new[]{0});
     Add(HeroDefOf.LP_TotemSpirit,Enumerable.Range(1,3));Add(HeroDefOf.LP_FavoredEnemy,Enumerable.Range(1,FavoredFactionFor(p)!=null?7:6));
     Add(HeroDefOf.LP_Attuner,new[]{0});Add(HeroDefOf.LP_ElementTouched,Enumerable.Range(1,4));Add(HeroDefOf.LP_SpellWarded,new[]{0});Add(HeroDefOf.LP_Duelist,new[]{0});
     foreach(var d in MoreTraits.Purple)if(tier!=Tier.Legendary || !d.HasModExtension<LP_AdventurerAffinityExt>())Add(d,new[]{0}); // 1.79.0 新增蓝 / 紫（传奇种族同样不抽改加点倍率 / 上限的那几个）
     break;
    case KindTalent:
     foreach(var d in Talents.All)Add(d,new[]{0});
     break;
    case KindCurse:
     DndTraits.AddCurseChoices(p,Add); // 1.62.0 复仇亡魂、兽化症、远域之触
     break;
   }
   return list;
  }
  // 神之勇士：已是某神信徒取该神，否则在九位可信奉的神里随机（degree = 信徒等级 1～9）。
  public static IEnumerable<int> ChampionDegrees(Pawn p){
   var god=FaithUtility.DevoteeGod(p);
   if(god!=null && god.worshippable && god.devoteeDegree>=1 && god.devoteeDegree<=9)return new[]{god.devoteeDegree};
   return FaithUtility.Worshippable.Select(d=>d.devoteeDegree).Where(d=>d>=1 && d<=9).Distinct();
  }
  public static Trait Pick(Pawn p,int kind){
   var c=Choices(p,kind);if(c.Count==0)return null;
   var ch=c.RandomElement();return new Trait(ch.def,ch.degrees.RandomElement(),true);
  }
  public static Trait Give(Pawn p,Trait t){
   if(p?.story?.traits==null || t==null)return null;
   if(IsSlot(t.def)){
    // 1.66.0 热修：同一特性（换分支）和互斥的（天命之间、稀有天赋之间、诅咒之间）先去掉；满了再去掉最早的一个。
    var l=p.story.traits.allTraits;
    foreach(var x in l.Where(x=>x.def==t.def || IsSlot(x.def) && (x.def.ConflictsWith(t.def) || t.def.ConflictsWith(x.def))).ToList()){if(x.def==t.def && x.Degree==t.Degree)return x;p.story.traits.RemoveTrait(x);}
    for(int guard=0;guard<8 && Full(p);guard++){var old=SlotTrait(p);if(old==null)break;p.story.traits.RemoveTrait(old);}
   }
   p.story.traits.GainTrait(t,true);
   return t;
  }
  // 1.66.0 热修：已经得到一个之后，每次成功率 ExtraChance 再得一个（颜色按本种族档的金 / 紫 / 天赋 / 红比例），直到失败或满 Max 个。返回多得的个数。
  public static int Extra(Pawn p){
   if(p?.story?.traits==null)return 0;var tier=TierOf(p);float curse=DndTraits.CurseOn?1f:0f;int n=0;
   float sum=GoldChance(tier)+PurpleChance(tier)+TalentChance(tier)+DndTraits.CurseChance(tier)*curse;if(sum<=0f)return 0;
   for(int guard=0;guard<8 && !Full(p) && Rand.Chance(ExtraChance);guard++){
    var t=Pick(p,RollKind(tier,1f/sum,Rand.Value*.9999f,curse));if(t!=null && Give(p,t)!=null)n++;
   }
   return n;
  }
  public static Trait Give(Pawn p,TraitDef d,int degree=0)=>d==null?null:Give(p,new Trait(d,degree,true));
  static bool Hostile(Pawn p){var pl=Faction.OfPlayerSilentFail;return p?.Faction!=null && pl!=null && p.Faction!=pl && p.Faction.HostileTo(pl);}
  // 生成后缀：掷一次。返回得到的特性（null = 没有）。
  public static Trait OnGenerated(Pawn p){
   if(TestSuppressed || !On || !Eligible(p) || HasSlot(p))return null; // 只在生成时掷：已经有的（开局编辑、事件给的）不再掷
   if(Hostile(p) && !EnemiesOn)return null;
   int k=TestKind??RollKind(TierOf(p),Rate,Rand.Value,DndTraits.CurseOn?1f:0f);if(k==None)return null;
   var t=Give(p,Pick(p,k));if(t!=null && TestKind==null)Extra(p);
   return t;
  }
  // 敌人：剧情首领、宿敌冠军 50% 带一个金 / 紫（人类优先金色，其他普通 / 中等种族各半，传奇种族只出紫色）；袭击领队 10% 带紫。
  public const float BossChance=.5f,LeaderChance=.1f,ChampionDeedChance=.3f;
  public static Trait EnemyBoss(Pawn p,float? roll=null){
   if(roll==null)CombatGifts.Boss(p); // 1.66.0 首领 / 冠军 50% 一个武艺天赋（独立名额）
   if(TestSuppressed || !EnemiesOn || !Eligible(p) || HasSlot(p))return null;
   if((roll??Rand.Value)>=BossChance)return null;
   if(DndTraits.CurseOn && Rand.Chance(DndTraits.BossCurseChance)){var c=DndTraits.BossCurse(p);if(c!=null){if(roll==null)Extra(p);return c;}} // 1.62.0：15% 红色（不给复仇亡魂）
   var tier=TierOf(p);bool gold=tier!=Tier.Legendary && (Affinity.For(p).key==Affinity.HumanKey || Rand.Chance(.5f));
   var t=Give(p,Pick(p,gold?KindGold:KindPurple)??Pick(p,KindPurple));if(t!=null && roll==null)Extra(p);
   return t;
  }
  public static Trait EnemyLeader(Pawn p){
   if(TestSuppressed || !EnemiesOn || !Eligible(p) || HasSlot(p) || !Rand.Chance(LeaderChance))return null;
   var t=Give(p,Pick(p,KindPurple));if(t!=null)Extra(p);
   return t;
  }
  // 宿敌冠军 30% 带一个传说事迹（不给奖励）。
  public static TraitDef EnemyDeed(Pawn p,float? roll=null){
   if(TestSuppressed || !EnemiesOn || !Eligible(p) || (roll??Rand.Value)>=ChampionDeedChance)return null;
   var d=Deeds.Where(x=>!Has(p,x)).RandomElementWithFallback();if(d==null)return null;
   p.story.traits.GainTrait(new Trait(d,0,true),true);return d;
  }
  public static string LetterNote(Pawn p){
   var l=p?.story?.traits?.allTraits?.Where(t=>Is(t.def) || DndTraits.IsCurse(t.def)).Select(Plain).ToList();
   return l==null || l.Count==0?"":"，特性 "+string.Join("、",l);
  }

  // ---------- 宿敌 ----------
  public static Faction FavoredFactionFor(Pawn p){
   var pl=Faction.OfPlayerSilentFail;
   if(p?.Faction!=null && pl!=null && p.Faction!=pl && p.Faction.HostileTo(pl))return pl; // 敌人的仇敌派系就是玩家
   var fm=Find.FactionManager;if(fm==null || pl==null)return null;
   return fm.AllFactionsListForReading.Where(f=>f!=pl && !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(pl)).RandomElementWithFallback();
  }
  public static bool FavoredTarget(HeroInfo i,Pawn t){
   if(i==null || i.favored<=0 || t==null)return false;
   if(i.favored==7)return i.rec?.favoredFaction!=null && t.Faction==i.rec.favoredFaction;
   return DndCreatureType.Is(t,(DndType)(1<<(i.favored-1)));
  }

  // ---------- 获得 / 失去 ----------
  public static void Gained(Pawn p,Trait t){
   if(p==null || t==null)return;
   var r=EnsureRecord(p);Invalidate(p);var i=Info(p);if(r==null || i==null)return;
   if(t.def==HeroDefOf.LP_Prophesied && r.prophecy<0)r.prophecy=Rand.RangeInclusive(0,HeroDestiny.Prophecies.Length-1);
   if(t.def==HeroDefOf.LP_FavoredEnemy && t.Degree==7 && r.favoredFaction==null)r.favoredFaction=FavoredFactionFor(p);
   if(t.def==HeroDefOf.LP_PactDestiny && r.nextDemandTick<0 && Current.ProgramState==ProgramState.Playing)r.nextDemandTick=GenTicks.TicksGame+HeroDestiny.DemandIntervalTicks();
   if(IsDestiny(t.def)){
    // 敌人按当前等级直接定阶（不发信）；殖民者从觉醒开始，之后由周期检查升阶。
    if(p.Faction==null || !p.Faction.IsPlayer){var h=Adventurer.Get(p);r.stage=h==null?0:t.def==HeroDefOf.LP_Prophesied?(h.level>=ManifestLevel?1:0):h.legendary?2:h.level>=ManifestLevel?1:0;}
    else r.stage=0;
    r.demoted=false;r.pactFails=0;
   }
   HeroDestiny.SyncAbilities(p);
  }
  public static void Lost(Pawn p,Trait t){if(p==null)return;Invalidate(p);HeroDestiny.SyncAbilities(p);}
  // 死灵造物不带强力特性（传说事迹保留）。
  public static int StripSlot(Pawn p){int n=0;Trait t;while((t=SlotTrait(p))!=null && n<5){p.story.traits.RemoveTrait(t);n++;}return n;}

  // ---------- 悬停说明里的动态内容 ----------
  public static string ExtraTip(Pawn p,Trait t){
   if(p==null || t==null)return null;var r=Record(p);var sb=new StringBuilder();
   if(IsDestiny(t.def) && r!=null)sb.Append("当前阶段："+HeroDestiny.StageLabel(r.stage)+(r.demoted?"（被宗主降阶，下次偿契成功后恢复升阶）":""));
   if(t.def==HeroDefOf.LP_Prophesied && r!=null){sb.Append("\n预言："+HeroDestiny.ProphecyText(r.prophecy)+(r.prophecyDone?"（已兑现）":""));if(r.prophecy==5)sb.Append("（已守住 "+r.raidsDefended+" / "+HeroDestiny.ProphecyRaids+" 次）");if(!r.prophecyDone)sb.Append("\n命运未尽："+(HeroDestiny.FateReady(p,r)?"可用":"冷却中"));}
   if(t.def==HeroDefOf.LP_PactDestiny && r!=null){sb.Append("\n宗主："+HeroDestiny.PatronName(t.Degree));sb.Append("\n"+HeroDestiny.DemandStatus(r));if(r.pactFails>0)sb.Append("\n连续失败 "+r.pactFails+" 次（2 次降阶）");}
   if(t.def==HeroDefOf.LP_FavoredEnemy && r!=null){if(t.Degree==7)sb.Append("仇敌派系："+(r.favoredFaction?.Name??"（没有可记的敌对派系）")+"\n");sb.Append("已击杀宿敌 "+r.favoredKills+" / "+HeroCombat.BaneKills+(r.favoredBane?"：宿敌克星":""));}
   if(t.def==HeroDefOf.LP_Giantbane && r!=null)sb.Append("近战参与击杀巨人："+r.giantKills);
   if(t.def==HeroDefOf.LP_DivineChampion){var g=HeroDestiny.ChampionGod(t.Degree);if(g!=null)sb.Append("\n侍奉："+g.FullLabel+"；敌对目标："+HeroDestiny.ChampionTargetLabel(t.Degree));if(r!=null && r.stage>=2)sb.Append("\n神明干预："+(HeroDestiny.DivineReady(r)?"可用":"冷却中"));}
   return sb.Length==0?null:sb.ToString().Trim();
  }
 }

 // ---------- DND 生物类型（宿敌猎人、屠龙者、神之勇士……共用；结果按 pawn 缓存 2500 刻） ----------
 [Flags] public enum DndType{None=0,Dragon=1,Giant=2,Undead=4,Fiend=8,Aberration=16,Beast=32,Divine=64}
 public static class DndCreatureType {
  struct Entry{public int tick;public DndType t;}
  static readonly Dictionary<int,Entry> cache=new Dictionary<int,Entry>();
  public const int CacheTicks=2500;
  public static DndType Of(Pawn p){
   if(p==null)return DndType.None;int now=GenTicks.TicksGame;
   if(cache.TryGetValue(p.thingIDNumber,out var e) && now>=e.tick && now-e.tick<CacheTicks)return e.t;
   var t=Compute(p);cache[p.thingIDNumber]=new Entry{tick=now,t=t};return t;
  }
  public static bool Is(Pawn p,DndType t)=>(Of(p)&t)!=0;
  public static void Forget(Pawn p){if(p!=null)cache.Remove(p.thingIDNumber);}
  public static void Clear(){cache.Clear();}
  public static int Trim(){int now=GenTicks.TicksGame;var old=cache.Where(kv=>now<kv.Value.tick || now-kv.Value.tick>GenDate.TicksPerDay).Select(kv=>kv.Key).ToList();foreach(var k in old)cache.Remove(k);return old.Count;}
  public static DndType Compute(Pawn p){
   var t=DndType.None;if(p?.RaceProps==null)return t;
   if(Dragons.Is(p))t|=DndType.Dragon;
   if(p.BodySize>=2f && (GiantMigration.IsGiant(p) || GiantKin.Is(p)))t|=DndType.Giant;
   if(Items.Undead(p) || Monsters.Is(p,Monsters.DeathKnight) || Monsters.Is(p,Monsters.Demilich) || p.IsMutant && p.mutant?.Def?.defName=="Ghoul")t|=DndType.Undead;
   var o=Outsiders.Of(p);if(Fiends.Is(p) || o!=null && (o.plane==OutsiderPlane.Abyss || o.plane==OutsiderPlane.Hells))t|=DndType.Fiend;
   if(MindFlayer.IsMindFlayer(p) || Monsters.Is(p,Monsters.Beholder) || Monsters.Is(p,Monsters.Behir) || Marilith.Is(p))t|=DndType.Aberration;
   if(Twilight.Vessel(p)!=null || Twilight.Infused(p)!=null || Demigods.Is(p))t|=DndType.Divine;
   if(t==DndType.None && p.RaceProps.Animal)t|=DndType.Beast;
   return t;
  }
  public static string Label(DndType t){
   var l=new List<string>();
   if((t&DndType.Dragon)!=0)l.Add("龙");if((t&DndType.Giant)!=0)l.Add("巨人");if((t&DndType.Undead)!=0)l.Add("不死");if((t&DndType.Fiend)!=0)l.Add("邪魔");
   if((t&DndType.Aberration)!=0)l.Add("异怪");if((t&DndType.Beast)!=0)l.Add("野兽");if((t&DndType.Divine)!=0)l.Add("神力");
   return l.Count==0?"无":string.Join("、",l);
  }
 }

 // ---------- 存档数据：天命阶段、预言、偿契、宿敌、事迹进度 ----------
 public class Hediff_HeroRecord : HediffWithComps {
  public int stage;public bool demoted;
  public int prophecy=-1;public bool prophecyDone;public int fateTick=int.MinValue;public bool masterArmed;
  public int nextDemandTick=-1,demandStart=int.MinValue,demandDeadline=-1,pactFails;public string demand;public bool demandPaid;public int meditateTicks,readTicks;
  public int divineTick=int.MinValue,graceTick=int.MinValue;
  public Faction favoredFaction;public int favoredKills;public bool favoredBane;public int favoredNoticeTick=int.MinValue;
  public int giantKills,blastSurvived,raidsDefended;
  public override bool Visible=>false;
  public override bool ShouldRemove=>false;
  // 动态属性：同调者放大身上魔法物品的属性（×1.25），魔网之子没装 VPE 时心灵敏感度 +15%。每 250 刻按签名重建。
  HediffStage built;int sig=int.MinValue;
  public override HediffStage CurStage=>built??(built=BuildStage());
  public void Rebuild(){built=null;sig=Signature();if(pawn?.health?.hediffSet!=null && pawn.health.hediffSet.hediffs.Contains(this))pawn.health.Notify_HediffChanged(this);}
  int Signature(){
   var i=HeroTraits.Info(pawn);if(i==null)return 0;int s=17;
   if(i.weave)s=s*31+1+(HeroDestiny.VPEActive?2:0);
   if(i.attuner){foreach(var t in HeroCombat.MagicGear(pawn))s=s*31+t.thingIDNumber;foreach(var h in pawn.health.hediffSet.hediffs)if(h is Hediff_MagicItem)s=s*31+h.CurStageIndex*7+h.def.shortHash;}
   return s;
  }
  HediffStage BuildStage(){
   var st=new HediffStage{becomeVisible=false};var i=HeroTraits.Info(pawn);if(i==null)return st;
   var o=new List<StatModifier>();var f=new List<StatModifier>();
   if(i.weave && !HeroDestiny.VPEActive)o.Add(new StatModifier{stat=StatDefOf.PsychicSensitivity,value=HeroDestiny.WeaveSensitivity});
   if(i.attuner){
    foreach(var t in HeroCombat.MagicGear(pawn))if(t.def.equippedStatOffsets!=null)foreach(var m in t.def.equippedStatOffsets)o.Add(new StatModifier{stat=m.stat,value=m.value*HeroCombat.AttunerBonus});
    foreach(var h in pawn.health.hediffSet.hediffs){
     if(!(h is Hediff_MagicItem))continue;var s=h.CurStage;if(s==null)continue;
     if(s.statOffsets!=null)foreach(var m in s.statOffsets)o.Add(new StatModifier{stat=m.stat,value=m.value*HeroCombat.AttunerBonus});
     if(s.statFactors!=null)foreach(var m in s.statFactors)f.Add(new StatModifier{stat=m.stat,value=1f+(m.value-1f)*HeroCombat.AttunerBonus});
    }
   }
   if(o.Count>0)st.statOffsets=o.GroupBy(m=>m.stat).Select(g=>new StatModifier{stat=g.Key,value=g.Sum(m=>m.value)}).ToList();
   if(f.Count>0)st.statFactors=f.GroupBy(m=>m.stat).Select(g=>new StatModifier{stat=g.Key,value=g.Aggregate(1f,(x,m)=>x*m.value)}).ToList();
   return st;
  }
  public override void TickInterval(int delta){
   base.TickInterval(delta);if(pawn==null || !pawn.IsHashIntervalTick(250,delta))return;
   int s=Signature();if(s!=sig){sig=s;built=null;pawn.health.Notify_HediffChanged(this);}
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref stage,"lpStage");Scribe_Values.Look(ref demoted,"lpDemoted");
   Scribe_Values.Look(ref prophecy,"lpProphecy",-1);Scribe_Values.Look(ref prophecyDone,"lpProphecyDone");Scribe_Values.Look(ref fateTick,"lpFateTick",int.MinValue);Scribe_Values.Look(ref masterArmed,"lpMasterArmed");
   Scribe_Values.Look(ref nextDemandTick,"lpNextDemand",-1);Scribe_Values.Look(ref demandStart,"lpDemandStart",int.MinValue);Scribe_Values.Look(ref demandDeadline,"lpDemandDeadline",-1);Scribe_Values.Look(ref pactFails,"lpPactFails");
   Scribe_Values.Look(ref demand,"lpDemand");Scribe_Values.Look(ref demandPaid,"lpDemandPaid");Scribe_Values.Look(ref meditateTicks,"lpMeditate");Scribe_Values.Look(ref readTicks,"lpRead");
   Scribe_Values.Look(ref divineTick,"lpDivineTick",int.MinValue);Scribe_Values.Look(ref graceTick,"lpGraceTick",int.MinValue);
   Scribe_References.Look(ref favoredFaction,"lpFavoredFaction");Scribe_Values.Look(ref favoredKills,"lpFavoredKills");Scribe_Values.Look(ref favoredBane,"lpFavoredBane");Scribe_Values.Look(ref favoredNoticeTick,"lpFavoredNotice",int.MinValue);
   Scribe_Values.Look(ref giantKills,"lpGiantKills");Scribe_Values.Look(ref blastSurvived,"lpBlastSurvived");Scribe_Values.Look(ref raidsDefended,"lpRaidsDefended");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){built=null;sig=int.MinValue;}
  }
 }

 // 鼓舞类的短时增益（为了预言之子）：有这个状态的人记在集合里，命中挂钩先看集合是不是空的。
 public class Hediff_HeroBuff : HediffWithComps {
  public static readonly HashSet<Pawn> Holders=new HashSet<Pawn>();
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);if(pawn!=null)Holders.Add(pawn);}
  public override void PostRemoved(){base.PostRemoved();if(pawn!=null && !pawn.health.hediffSet.hediffs.Any(h=>h!=this && h is Hediff_HeroBuff))Holders.Remove(pawn);}
  public override void TickInterval(int delta){base.TickInterval(delta);if(pawn!=null && !pawn.Dead)Holders.Add(pawn);} // 读档后重新登记
 }

 // ---------- 周期：250 刻（决斗者贴身计数、宿敌提示、偿契冥想计时、绝境英雄判定）、2500 刻（名单、天命升阶、偿契、缓存清理） ----------
 public class GameComponent_HeroTraits : GameComponent {
  public int lastExecutionTick=int.MinValue,lastGatheringTick=int.MinValue;
  public readonly List<Pawn> duelists=new List<Pawn>(),hunters=new List<Pawn>(),destinies=new List<Pawn>(),champions=new List<Pawn>(),oldOnes=new List<Pawn>(),musicians=new List<Pawn>();
  public GameComponent_HeroTraits(Game game){}
  public static GameComponent_HeroTraits Get=>GameComp<GameComponent_HeroTraits>.Get();
  public override void FinalizeInit(){
   base.FinalizeInit();HeroTraits.ClearCache();DndCreatureType.Clear();HeroCombat.Reset();
   Feats2.Observers.Clear();Feats2.disguised.Clear();Feats2.borrowed.Clear();
   var all=PawnsFinder.AllMapsWorldAndTemporary_Alive.ToList();
   Feats2.BackfillOrigins(all); // 1.58.1 老存档补发起源专长（每人一次）
   foreach(var p in all)Feats2.Sync(p);
   Refresh();
  }
  // 地图上有 1.58 特性的人；殖民者的天命（含商队里的）。
  public void Refresh(){
   duelists.Clear();hunters.Clear();destinies.Clear();champions.Clear();oldOnes.Clear();musicians.Clear();
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawnsSpawned){
    var i=HeroTraits.Info(p);if(i==null || p.Dead)continue;
    if(i.duelist)duelists.Add(p);
    if(i.musician && p.Faction!=null && p.Faction.IsPlayer)musicians.Add(p);
    if(i.favored>0 && p.Faction!=null && p.Faction.IsPlayer)hunters.Add(p);
   }
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists){
    var i=HeroTraits.Info(p);if(i==null || !i.Destiny)continue;
    destinies.Add(p);
    if(i.champion>0 && i.Stage>=2)champions.Add(p);
    if(i.pact==3 && i.rec?.demand!=null)oldOnes.Add(p);
   }
  }
  public override void GameComponentTick(){
   if(HeroCombat.counters.Count>0)HeroCombat.RunCounters();
   int now=Find.TickManager.TicksGame;
   if(now%250==113)Tick250(now);
   if(now%2500==1213)Tick2500(now);
  }
  public void Tick250(int now){
   if(duelists.Count>0)HeroCombat.RefreshDuelists(duelists);
   if(hunters.Count>0)HeroCombat.FavoredNotices(hunters,now);
   if(oldOnes.Count>0)HeroDestiny.SampleOldOnes(oldOnes,250);
   if(musicians.Count>0)Feats2.Music(musicians); // 1.58.1 音乐家
   var st=Adventurer.State;if(st!=null && st.raids.Count>0)HeroDeeds.SampleRaids(st.raids);
   HeroCombat.ExpireMarks(now);
  }
  public void Tick2500(int now){
   Refresh();
   foreach(var p in destinies.ToList()){try{HeroDestiny.Periodic(p,now);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 天命周期出错："+e,75158001);}}
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList()){if(Growth.all.Count>0)Growth.Sync(p);} // 1.58.3 传承武器阶段（实战磨砺 1.75.0 起实时累计，Growth.Grind）
   HeroDestiny.CheckColonyDemigod();
   if(now%60000<2500){HeroTraits.Cleanup();DndCreatureType.Trim();}
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref lastExecutionTick,"lpHeroExecution",int.MinValue);Scribe_Values.Look(ref lastGatheringTick,"lpHeroGathering",int.MinValue);
  }
 }

 // ---------- Harmony：特性变动、生成、成年、说明 ----------
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.GainTrait))]
 public static class HeroGain {static void Postfix(Pawn ___pawn,Trait trait){if(trait==null || !HeroTraits.Is(trait.def) || ___pawn?.story?.traits?.allTraits.Contains(trait)!=true)return;HeroTraits.Gained(___pawn,trait);Adventurer.Get(___pawn)?.Dirty();}} // 被别的前缀拦下（Isekai 互斥）时不算
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.RemoveTrait))]
 public static class HeroLose {static void Postfix(Pawn ___pawn,Trait trait){if(trait==null || !HeroTraits.Is(trait.def))return;HeroTraits.Lost(___pawn,trait);Adventurer.Get(___pawn)?.Dirty();}}
 // 13 岁生日补掷一次（和冒险者 1 级同一个时刻）。
 [HarmonyPatch(typeof(Pawn_AgeTracker),"BirthdayBiological")]
 public static class HeroBirthday {static void Postfix(Pawn ___pawn,int birthdayAge){if(birthdayAge==Adventurer.MinAge && ___pawn!=null && !___pawn.Dead)HeroTraits.OnGenerated(___pawn);}}
 [HarmonyPatch(typeof(Trait),nameof(Trait.TipString))]
 public static class HeroTip {
  static void Postfix(Trait __instance,Pawn pawn,ref string __result){
   if(__instance==null || !HeroTraits.Is(__instance.def))return;
   var x=HeroTraits.ExtraTip(pawn,__instance);if(!x.NullOrEmpty())__result=__result+"\n\n"+x;
  }
 }
 // 变成死灵造物时去掉占名额的强力特性。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.AddHediff),new[]{typeof(Hediff),typeof(BodyPartRecord),typeof(DamageInfo?),typeof(DamageWorker.DamageResult)})]
 public static class HeroHediffAdded {
  static void Postfix(Hediff hediff,Pawn ___pawn){
   if(hediff==null || ___pawn==null)return;
   if(hediff is Hediff_Construct){HeroTraits.StripSlot(___pawn);return;}
   if(hediff.def==MindFlayer.Blasted)HeroDeeds.Blasted(___pawn);
  }
 }

 // ---------- 设置（左栏“DND 特性”分组，背景特性下面） ----------
 public partial class Settings {
  public bool heroTraits=true,heroTraitsEnemies=true;public int heroTraitRate=100,heroTraitMax=3,heroTraitExtra=25;
  void ExposeHeroTraits(){
   Scribe_Values.Look(ref heroTraits,"lpHeroTraits",true);Scribe_Values.Look(ref heroTraitsEnemies,"lpHeroTraitsEnemies",true);Scribe_Values.Look(ref heroTraitRate,"lpHeroTraitRate",100);
   Scribe_Values.Look(ref heroTraitMax,"lpHeroTraitMax",3);Scribe_Values.Look(ref heroTraitExtra,"lpHeroTraitExtra",25); // 1.66.0 热修
   heroTraitRate=Math.Max(0,Math.Min(300,heroTraitRate));heroTraitMax=Math.Max(1,Math.Min(6,heroTraitMax));heroTraitExtra=Math.Max(0,Math.Min(100,heroTraitExtra));
   ExposeFeats2(); // 1.58.1 起源专长
   ExposeGrowth(); // 1.58.3 实战磨砺、派系声望
   ExposeDndTraits(); // 1.62.0 性格特性、诅咒之力
   ExposeCombatGifts(); // 1.66.0 武艺天赋
  }
 }
 public static class HeroTraitSettings {
  static string legend;
  static string TraitRarityLegend=>legend??(legend="稀有度："+string.Join(" ",Enumerable.Range(0,6).Select(i=>TraitRarity.Hexes[i]==null?TraitRarity.Names[i]:"<color="+TraitRarity.Hexes[i]+">"+TraitRarity.Names[i]+"</color>"))+"  <color="+TraitRarity.CurseHex+">诅咒</color> <color="+TraitRarity.DeedHex+">事迹</color>");
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;l.GapLine();l.Label("DND 特性");
   l.Label(TraitRarityLegend,-1f,(TipSignal)"特性名字的颜色按稀有度：普通（白）、优秀（绿）、稀有（蓝）、史诗（紫）、传说（橙）、神话（金）；诅咒之力是红色，传说事迹是青色，它们的档位写在悬停说明第一行。"); // 2026-10-29 特性稀有度
   l.CheckboxLabeled("DND 强力特性",ref s.heroTraits,"（1.58.0）新生成的人有很小的机会带一个强力特性：英雄之资系与天命（英雄之资、极致是传说，四个天命是神话）、加点与强化天赋（天赋、金刚之躯、锋芒、灵光、传奇之胚和六个强化天赋是稀有，反骨、勤勉是史诗），1.51.0 的四个稀有天赋（史诗）也并进这一次随机。得到一个之后，每次按“额外强力特性”的成功率再得一个，直到失败或达到上限（天命之间、稀有天赋之间、诅咒之间仍然互斥）；传说事迹（青色）由事件授予，不占名额。\n第一个：普通种族 英雄之资系与天命 1% / 加点与强化天赋 4%，中等 0.4% / 3%，传奇种族只有后者 1%；四个稀有天赋 2%（传奇种族没有）。\n关闭后不再生成，已有的保留。");
   l.Label("强力特性出现率："+s.heroTraitRate+"%");s.heroTraitRate=Mathf.RoundToInt(l.Slider(s.heroTraitRate,0,300)/5f)*5;
   l.Label("每人最多强力特性："+s.heroTraitMax+" 个");s.heroTraitMax=Mathf.RoundToInt(l.Slider(s.heroTraitMax,1,6));
   l.Label("额外强力特性成功率："+s.heroTraitExtra+"%（得到一个之后，每次再得一个的几率）");s.heroTraitExtra=Mathf.RoundToInt(l.Slider(s.heroTraitExtra,0,100)/5f)*5;
   l.CheckboxLabeled("起源专长",ref s.originFeats,"（1.58.1）每个 13 岁以上的人形得到冒险者等级时免费获得 1 个起源专长（不占属性提升）：有背景特性的按背景（侍僧、贤者 → 魔法学徒；罪犯、士兵 → 警觉；民间英雄 → 酒馆斗殴者；贵族 → 技能专家；隐士 → 治疗者；局外人 → 幸运），否则按背景故事里加得最多的技能；5% 改为幸运。老存档读档时补发一次。敌人也有。关闭后不再发放，已有的保留。");
   l.CheckboxLabeled("实战磨砺",ref s.growthGrind,"（1.58.3）殖民者在实战和工作里慢慢磨砺属性：每 2500 刻按经验差值累计（力量 ← 格斗 + 采矿 ×0.5，敏捷 ← 射击，体质 ← 受到的伤害 ×100，智力 ← 智识，感知 ←（医疗 + 驯兽 + 种植）×0.5，魅力 ← 社交），满 120000 该项 +1，每项一生最多 +2，不超过上限。Isekai 共存模式下照常累计但只显示。");
   l.CheckboxLabeled("派系声望",ref s.factionReputation,"（1.58.3）费伦拓荒同盟、余烬氏族、黑网各有殖民地声望 0～50：完成它发的任务 +3，送礼每 20 好感 +1，在有它的人在场时击杀敌人 +2（每天一次）；不会下降，敌对时冻结。3 / 10 / 25 / 50 四级头衔：商队卖价 -5% → 每季情报信 → 请求援兵冷却减半、商队带专属物品 → 每季一份礼物。");
   l.CheckboxLabeled("敌人携带强力特性",ref s.heroTraitsEnemies,"剧情首领、宿敌冠军 50% 带一个强力特性（人类首领优先英雄之资系与天命），宿敌冠军另有 30% 带一个传说事迹；袭击领队 10% 带加点或强化天赋。关闭后敌对派系的人也不再随机带强力特性。");
   DndTraitSettings.Draw(l); // 1.62.0 性格特性、诅咒之力
   CombatGiftSettings.Draw(l); // 1.66.0 武艺天赋
  }
 }
}
