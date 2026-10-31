using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.62.0 DND 性格特性与诅咒之力（ArtSource/Traits-1.62.0-spec.md；定义 Defs/DndTraits.xml，派系加权 Patches/DndTraits.xml）。
 // 性格特性 commonality 0，生成后缀（DndGenerate，排在 1.58.0 TalentGenerate 之后）自己掷：只用原版 3 个特性名额里的空位，每人最多 2 个、最多 1 个阵营；读档不补发。
 // 诅咒之力（红）占 LP_PowerTrait 名额，由 HeroTraits.OnGenerated 的同一次随机给（KindCurse）；首领 / 冠军的强力特性 15% 换成红色（不给复仇亡魂）。
 // 热路径先查按 pawn 缓存的 DndInfo（没有这些特性 = null）；存档数据在隐藏状态 Hediff_DndRecord；周期逻辑在 GameComponent_DndTraits（250 / 2500 刻）。
 [DefOf]
 public static class DndTraitDefOf {
  public static TraitDef LP_Order,LP_Morality,LP_DragonStance,LP_GiantStance,LP_UndeadStance,LP_RaceAttitude,LP_HiveSurvivor,LP_DragonServant,LP_GithExile;
  public static TraitDef LP_Tactician,LP_ArcaneGift,LP_WildMagic,LP_Tinkerer,LP_NatureBond,LP_Magephobe,LP_ArcaneCraving,LP_TreasureHunter,LP_PlaneTouched;
  public static TraitDef LP_Revenant,LP_Lycanthrope,LP_FarRealmTouched;
  public static HediffDef LP_DndRecord,LP_RevenantBody,LP_Beastform_Wolf,LP_Beastform_Bear,LP_Beastform_Tiger;
  public static AbilityDef LP_DT_PsychicShock,LP_DT_Beastshape;
  static DndTraitDefOf(){DefOfHelper.EnsureInitializedInCtor(typeof(DndTraitDefOf));}
 }

 // 挂在 FactionDef / ThingDef / XenotypeDef 上：特性 + 程度 → 出现倍率。
 public class LP_TraitWeightExtension : DefModExtension {
  public List<TraitWeight> weights=new List<TraitWeight>();
  public float Factor(TraitDef d,int degree){if(weights!=null)foreach(var w in weights)if(w.trait==d && w.degree==degree)return w.factor;return 1f;}
 }
 public class TraitWeight {public TraitDef trait;public int degree;public float factor=1f;}

 // 一个人身上 1.62 特性的汇总（缓存，不存档）。光谱特性存程度（0 = 没有）；revenant -1 = 没有、0 = 复仇亡魂、1 = 安息之魂。
 public sealed class DndInfo {
  public int order,morality,dragon,giant,undead,race,lycan,revenant=-1,personality;
  public bool hive,servant,exile,tactician,arcane,wild,tinker,nature,phobe,craving,treasure,plane,far;
 }

 public static class DndTraits {
  public const int VanillaCap=3,MaxPersonality=2;
  public const float FirstChance=.4f,SecondChance=.15f,BossCurseChance=.15f;
  public const float TypeDragon=.2f,TypeGiant=.2f,TypeUndead=.2f,TypeRevenant=.3f,TypeAtRest=.15f,GiantSize=1.5f;
  public const float OrderWork=1.05f,ServantWork=1.1f,TinkerRepair=1.2f,ArcaneSensitivity=.1f,NatureTame=1.15f,ServantHaul=1.3f,FarEntropy=20f;
  public const float AuraHitBonus=.05f,AuraShot=1.05f,GiantDodge=.1f,WildSpell=1.1f,HolyBonus=1.15f,PhobeSpell=.9f,BeastResist=.5f,ArcaneEntropy=.9f,TinkerCooldown=.95f,ClassXpBonus=1.25f;
  public const float WildChance=.05f,BiteChance=.05f,BlastChance=.05f,ScenarioFarChance=.05f,InsightChance=.01f,FleeChance=.1f,BerserkChance=.1f,TreasureLoot=.1f,TreasureRuins=.2f,LowHealth=.3f;
  public const int AuraRadius=4,DragonFleeRadius=10,ServantRadius=3,WhisperRadius=6,RestrainedTicks=6*GenDate.TicksPerHour,BeastTicks=6*GenDate.TicksPerHour,RiseTicks=12*GenDate.TicksPerHour;
  public const int RevenantCooldown=60*GenDate.TicksPerDay,CravingTicks=5*GenDate.TicksPerDay,TameLevel=10,BeastHealEvery=250,WhisperTicks=2*GenDate.TicksPerHour,PlaneDazeTicks=GenDate.TicksPerHour;
  public static float? TestRoll;public static float Roll=>TestRoll??Rand.Value;

  // ---------- 设置 ----------
  public static bool TestSuppressed; // 自测进程：除本组外不随机生成
  public static bool PersonalityOn=>LichMod.Settings?.dndPersonality??true;
  public static float Rate=>Mathf.Clamp((LichMod.Settings?.dndPersonalityRate??100)/100f,0f,2f);
  public static bool CurseOn=>LichMod.Settings?.dndCurses??true;

  // ---------- 分类 ----------
  static HashSet<TraitDef> personality,curses;
  static void Sets(){
   if(personality!=null)return;var D=typeof(DndTraitDefOf);
   personality=new HashSet<TraitDef>{DndTraitDefOf.LP_Order,DndTraitDefOf.LP_Morality,DndTraitDefOf.LP_DragonStance,DndTraitDefOf.LP_GiantStance,DndTraitDefOf.LP_UndeadStance,DndTraitDefOf.LP_RaceAttitude,DndTraitDefOf.LP_HiveSurvivor,DndTraitDefOf.LP_DragonServant,DndTraitDefOf.LP_GithExile,
    DndTraitDefOf.LP_Tactician,DndTraitDefOf.LP_ArcaneGift,DndTraitDefOf.LP_WildMagic,DndTraitDefOf.LP_Tinkerer,DndTraitDefOf.LP_NatureBond,DndTraitDefOf.LP_Magephobe,DndTraitDefOf.LP_ArcaneCraving,DndTraitDefOf.LP_TreasureHunter,DndTraitDefOf.LP_PlaneTouched};
   curses=new HashSet<TraitDef>{DndTraitDefOf.LP_Revenant,DndTraitDefOf.LP_Lycanthrope,DndTraitDefOf.LP_FarRealmTouched};
   foreach(var d in MoreTraits.Curses)curses.Add(d); // 1.79.0 吸血鬼之咒、厄运缠身、空心之人、狱火烙印（HeroTraitsMore.cs）
   personality.Remove(null);curses.Remove(null);
  }
  public static bool IsPersonality(TraitDef d){if(d==null)return false;Sets();return personality.Contains(d);}
  public static bool IsCurse(TraitDef d){if(d==null)return false;Sets();return curses.Contains(d);}
  public static bool IsAlignment(TraitDef d)=>d!=null && (d==DndTraitDefOf.LP_Order || d==DndTraitDefOf.LP_Morality);
  public static bool Is(TraitDef d)=>IsPersonality(d) || IsCurse(d);
  public static IEnumerable<TraitDef> Personality{get{Sets();return personality;}}
  public static IEnumerable<TraitDef> Curses{get{Sets();return curses;}}
  public static int PersonalityCount(Pawn p)=>p?.story?.traits==null?0:p.story.traits.allTraits.Count(t=>IsPersonality(t.def));
  public static int Degree(Pawn p,TraitDef d){var t=p?.story?.traits?.GetTrait(d);return t==null?0:t.Degree;}
  public static bool Has(Pawn p,TraitDef d)=>d!=null && p?.story?.traits?.HasTrait(d)==true;

  // ---------- 缓存 ----------
  static readonly Dictionary<Pawn,DndInfo> cache=new Dictionary<Pawn,DndInfo>();
  public static DndInfo Info(Pawn p){
   if(p==null)return null;
   if(cache.TryGetValue(p,out var i))return i;
   i=Build(p);cache[p]=i;return i;
  }
  public static void Invalidate(Pawn p){if(p!=null)cache.Remove(p);}
  public static void ClearCache(){cache.Clear();}
  public static int Cleanup(){var dead=cache.Keys.Where(p=>p==null || p.Destroyed || p.Discarded).ToList();foreach(var p in dead)cache.Remove(p);return dead.Count;}
  static DndInfo Build(Pawn p){
   var list=p.story?.traits?.allTraits;if(list==null)return null;DndInfo i=null;
   for(int k=0;k<list.Count;k++){
    var t=list[k];var d=t.def;if(!Is(d))continue;
    if(i==null)i=new DndInfo();int g=t.Degree;
    if(IsPersonality(d))i.personality++;
    if(d==DndTraitDefOf.LP_Order)i.order=g;else if(d==DndTraitDefOf.LP_Morality)i.morality=g;
    else if(d==DndTraitDefOf.LP_DragonStance)i.dragon=g;else if(d==DndTraitDefOf.LP_GiantStance)i.giant=g;
    else if(d==DndTraitDefOf.LP_UndeadStance)i.undead=g;else if(d==DndTraitDefOf.LP_RaceAttitude)i.race=g;
    else if(d==DndTraitDefOf.LP_HiveSurvivor)i.hive=true;else if(d==DndTraitDefOf.LP_DragonServant)i.servant=true;else if(d==DndTraitDefOf.LP_GithExile)i.exile=true;
    else if(d==DndTraitDefOf.LP_Tactician)i.tactician=true;else if(d==DndTraitDefOf.LP_ArcaneGift)i.arcane=true;else if(d==DndTraitDefOf.LP_WildMagic)i.wild=true;
    else if(d==DndTraitDefOf.LP_Tinkerer)i.tinker=true;else if(d==DndTraitDefOf.LP_NatureBond)i.nature=true;else if(d==DndTraitDefOf.LP_Magephobe)i.phobe=true;
    else if(d==DndTraitDefOf.LP_ArcaneCraving)i.craving=true;else if(d==DndTraitDefOf.LP_TreasureHunter)i.treasure=true;else if(d==DndTraitDefOf.LP_PlaneTouched)i.plane=true;
    else if(d==DndTraitDefOf.LP_Revenant)i.revenant=Mathf.Clamp(g,0,1);else if(d==DndTraitDefOf.LP_Lycanthrope)i.lycan=Mathf.Clamp(g,1,3);else if(d==DndTraitDefOf.LP_FarRealmTouched)i.far=true;
   }
   return i;
  }

  // ---------- 记录（隐藏状态） ----------
  public static Hediff_DndRecord Record(Pawn p){var list=p?.health?.hediffSet?.hediffs;if(list!=null)for(int k=0;k<list.Count;k++)if(list[k] is Hediff_DndRecord r)return r;return null;}
  public static Hediff_DndRecord EnsureRecord(Pawn p){
   var r=Record(p);if(r!=null || p?.health==null || DndTraitDefOf.LP_DndRecord==null)return r;
   r=(Hediff_DndRecord)HediffMaker.MakeHediff(DndTraitDefOf.LP_DndRecord,p);p.health.AddHediff(r);return r;
  }
  static bool NeedsRecord(DndInfo i)=>i!=null && (i.revenant>=0 || i.lycan>0 || i.far || i.craving || i.wild || i.plane);
  public static int Now=>Current.Game?.tickManager?.TicksGame??0;
  public static int Days(float a,float b)=>(int)(Rand.Range(a,b)*GenDate.TicksPerDay);

  // ---------- 生成 ----------
  public class Entry {public TraitDef def;public int degree;public float weight;public int group;} // group：0 阵营、1 种族互动、2 职业天赋、3 怪癖
  static List<Entry> table;
  public static List<Entry> Table{get{
   if(table!=null)return table;var t=new List<Entry>();
   void A(TraitDef d,int g,float w,int grp){if(d!=null)t.Add(new Entry{def=d,degree=g,weight=w,group=grp});}
   A(DndTraitDefOf.LP_Order,1,1.5f,0);A(DndTraitDefOf.LP_Order,-1,1.5f,0);A(DndTraitDefOf.LP_Morality,1,1.5f,0);A(DndTraitDefOf.LP_Morality,-1,1.5f,0);
   A(DndTraitDefOf.LP_DragonStance,-1,1f,1);A(DndTraitDefOf.LP_DragonStance,1,1f,1);A(DndTraitDefOf.LP_GiantStance,1,1f,1);A(DndTraitDefOf.LP_GiantStance,-1,1f,1);
   A(DndTraitDefOf.LP_UndeadStance,1,1f,1);A(DndTraitDefOf.LP_UndeadStance,-1,1f,1);A(DndTraitDefOf.LP_RaceAttitude,1,1f,1);A(DndTraitDefOf.LP_RaceAttitude,-1,1f,1);
   A(DndTraitDefOf.LP_HiveSurvivor,0,.5f,1);A(DndTraitDefOf.LP_DragonServant,0,.6f,1);A(DndTraitDefOf.LP_GithExile,0,3f,1);
   foreach(var d in new[]{DndTraitDefOf.LP_Tactician,DndTraitDefOf.LP_ArcaneGift,DndTraitDefOf.LP_WildMagic,DndTraitDefOf.LP_Tinkerer,DndTraitDefOf.LP_NatureBond})A(d,0,1f,2);
   foreach(var d in new[]{DndTraitDefOf.LP_Magephobe,DndTraitDefOf.LP_ArcaneCraving,DndTraitDefOf.LP_TreasureHunter,DndTraitDefOf.LP_PlaneTouched})A(d,0,1f,3);
   return table=t;
  }}
  public static bool Eligible(Pawn p)=>HeroTraits.Eligible(p) && Necro.Construct(p)==null;
  // 原版名额里还剩几个空位：强力特性、传说事迹不算（它们在名额以外），背景和性格特性都算。
  public static int Room(Pawn p)=>p?.story?.traits==null?0:VanillaCap-p.story.traits.allTraits.Count(t=>!HeroTraits.IsSlot(t.def) && !HeroTraits.IsDeed(t.def));
  public static bool Dragonborn(Pawn p)=>p!=null && (Affinity.For(p).key=="LP_ForceDragon" || ForceDragon.Active(p));
  public static bool GiantRace(Pawn p)=>IdeoBridge.IsGiant(p);
  public static bool Gith(Pawn p)=>p?.genes?.Xenotype!=null && p.genes.Xenotype==Githyanki.Xenotype;
  public static bool Allowed(Pawn p,TraitDef d,int degree){
   if(p?.story?.traits==null || d==null)return false;
   if(d==DndTraitDefOf.LP_DragonStance && (Dragonborn(p) || DndCreatureType.Is(p,DndType.Dragon)))return false;
   if(d==DndTraitDefOf.LP_GiantStance && degree==-1 && GiantRace(p))return false;
   if(d==DndTraitDefOf.LP_UndeadStance && degree==-1 && Items.Undead(p))return false;
   if(d==DndTraitDefOf.LP_HiveSurvivor && (MindFlayer.IsMindFlayer(p) || HalfIllithid.Is(p)))return false;
   if(d==DndTraitDefOf.LP_DragonServant && Dragonborn(p))return false;
   if(d==DndTraitDefOf.LP_GithExile && !Gith(p))return false;
   var l=p.story.traits.allTraits;var probe=new Trait(d,degree,true);
   for(int k=0;k<l.Count;k++){var e=l[k];if(e.def==d || e.def.ConflictsWith(d) || d.ConflictsWith(e.def) || Clash(probe,e) || Clash(e,probe))return false;}
   return true;
  }
  // 同一特性不同程度的互斥（TraitDef 的互斥不分程度）。
  public static bool Clash(Trait a,Trait b){
   if(a?.def==null || b?.def==null)return false;
   if(a.def==DndTraitDefOf.LP_Morality && a.Degree==1 && (b.def==TraitDefOf.Psychopath || b.def==TraitDefOf.Bloodlust || b.def==DndTraitDefOf.LP_UndeadStance && b.Degree==1))return true;
   if(a.def==DndTraitDefOf.LP_Morality && a.Degree==-1 && (b.def==TraitDefOf.Kind || b.def==DndTraitDefOf.LP_UndeadStance && b.Degree==-1))return true;
   if(a.def==DndTraitDefOf.LP_WildMagic && (b.def==DndTraitDefOf.LP_ArcaneGift || b.def==DndTraitDefOf.LP_Magephobe))return true;
   return false;
  }
  public static bool DegreeConflict(Pawn p,Trait t){var l=p?.story?.traits?.allTraits;if(l==null || t==null || !(Is(t.def) || t.def==TraitDefOf.Psychopath || t.def==TraitDefOf.Bloodlust || t.def==TraitDefOf.Kind || t.def==DndTraitDefOf.LP_ArcaneGift))return false;for(int k=0;k<l.Count;k++)if(Clash(t,l[k]) || Clash(l[k],t))return true;return false;}
  static float Ext(Def d,TraitDef t,int degree)=>d?.GetModExtension<LP_TraitWeightExtension>()?.Factor(t,degree)??1f;
  public static float Weight(Pawn p,Entry e)=>e.weight*Ext(p?.Faction?.def,e.def,e.degree)*Ext(p?.def,e.def,e.degree)*Ext(p?.genes?.Xenotype,e.def,e.degree);
  public static List<Entry> Candidates(Pawn p){
   var res=new List<Entry>();if(p?.story?.traits==null)return res;
   bool aligned=Has(p,DndTraitDefOf.LP_Order) || Has(p,DndTraitDefOf.LP_Morality);
   foreach(var e in Table){if(e.group==0 && aligned)continue;if(Allowed(p,e.def,e.degree))res.Add(e);}
   return res;
  }
  public static Entry PickEntry(Pawn p){var c=Candidates(p);return c.Count==0?null:c.RandomElementByWeight(e=>Weight(p,e));}
  public static Trait GiveEntry(Pawn p,Entry e){if(e==null)return null;var t=new Trait(e.def,e.degree,true);p.story.traits.GainTrait(t,true);return p.story.traits.allTraits.Contains(t)?t:null;}
  // 生成后缀：有空位时 40% 给一个，再有空位时 15% 给第二个（都 × 出现率）。返回给出的个数。
  public static int OnGenerated(Pawn p){
   if(TestSuppressed || !PersonalityOn || !Eligible(p))return 0;
   int n=0,have=PersonalityCount(p),room=Room(p);float r=Rate;
   if(room<=0 || have>=MaxPersonality || !Rand.Chance(Mathf.Min(1f,FirstChance*r)))return 0;
   if(GiveEntry(p,PickEntry(p))!=null){n++;have++;room--;}
   if(n>0 && room>0 && have<MaxPersonality && Rand.Chance(Mathf.Min(1f,SecondChance*r)) && GiveEntry(p,PickEntry(p))!=null)n++;
   return n;
  }
  // 诅咒之力：HeroTraits.Choices(KindCurse) 用。敌方首领不给复仇亡魂。
  public static float CurseChance(HeroTraits.Tier t)=>t==HeroTraits.Tier.Common?.015f:t==HeroTraits.Tier.Medium?.01f:.005f;
  public static void AddCurseChoices(Pawn p,Action<TraitDef,IEnumerable<int>> add,bool revenant=true){
   if(revenant)add(DndTraitDefOf.LP_Revenant,new[]{0});
   add(DndTraitDefOf.LP_Lycanthrope,new[]{1,2,3});add(DndTraitDefOf.LP_FarRealmTouched,new[]{0});
   foreach(var d in MoreTraits.Curses)if(MoreTraits.CurseAllowed(p,d))add(d,new[]{0}); // 1.79.0
  }
  public static Trait BossCurse(Pawn p){
   var c=HeroTraits.Choices(p,HeroTraits.KindCurse).Where(x=>x.def!=DndTraitDefOf.LP_Revenant).ToList();if(c.Count==0)return null;
   var ch=c.RandomElement();return HeroTraits.Give(p,new Trait(ch.def,ch.degrees.RandomElement(),true));
  }
  // 染上诅咒（咬伤、心灵震爆、开局、耐瑟启迪）：受名额限制（1.66.0 热修：强力特性没满、身上还没有诅咒就行）。
  public static Trait Infect(Pawn p,TraitDef d,int degree){
   if(!CurseOn || d==null || !HeroTraits.Eligible(p) || HeroTraits.Full(p) || p.story.traits.allTraits.Any(x=>IsCurse(x.def)) || !Allowed(p,d,degree))return null;
   var t=HeroTraits.Give(p,d,degree);
   if(t!=null && p.Faction!=null && p.Faction.IsPlayer && !TestSuppressed)Messages.Message(p.LabelShort+"染上了「"+HeroTraits.Plain(t)+"」。",p,MessageTypeDefOf.NegativeEvent,false);
   return t;
  }

  // ---------- 获得 / 失去 ----------
  public static void Gained(Pawn p,Trait t){
   Invalidate(p);var i=Info(p);if(i==null)return;
   if(NeedsRecord(i)){
    var r=EnsureRecord(p);int now=Now;
    if(i.lycan>0 && r.lycanNext==-1)r.lycanNext=now+Days(10f,15f);
    if(i.far && r.whisperNext==-1)r.whisperNext=now+Days(5f,8f);
    if(i.plane && r.dazeNext==-1)r.dazeNext=now+Days(10f,15f);
    if(i.wild && r.surgeNext==-1)r.surgeNext=now+Days(.5f,1.5f);
    if(i.craving && r.lastMagic==-1)r.lastMagic=now;
   }
   SyncAbilities(p);GameComponent_DndTraits.Get?.MarkDirty();
  }
  public static void Lost(Pawn p,Trait t){Invalidate(p);SyncAbilities(p);if(t?.def==DndTraitDefOf.LP_Lycanthrope)Form(p)?.End();GameComponent_DndTraits.Get?.MarkDirty();}
  public static void SyncAbilities(Pawn p){
   if(p?.abilities==null)return;var i=Info(p);var r=Record(p);
   void Sync(AbilityDef d,bool want){if(d==null)return;bool has=p.abilities.GetAbility(d)!=null;if(want && !has)p.abilities.GainAbility(d);else if(!want && has)p.abilities.RemoveAbility(d);}
   Sync(DndTraitDefOf.LP_DT_PsychicShock,i!=null && i.far);
   Sync(DndTraitDefOf.LP_DT_Beastshape,i!=null && i.lycan>0 && r!=null && r.tamed);
  }
  // 仁善者成为巫妖：失去仁善。
  public static bool LichConverted(Pawn p){
   if(Degree(p,DndTraitDefOf.LP_Morality)!=1)return false;
   var t=p.story.traits.GetTrait(DndTraitDefOf.LP_Morality);p.story.traits.RemoveTrait(t);
   if(p.Faction!=null && p.Faction.IsPlayer && !TestSuppressed)Find.LetterStack.ReceiveLetter("仁善消逝",p.LabelShort+"把灵魂封进了命匣。曾经那颗见不得别人受苦的心，随着最后一口活人的气息一起散了。\n\n"+p.LabelShort+"失去了「仁善」特性。",LetterDefOf.NeutralEvent,p);
   LichConversions++;return true;
  }
  public static int LichConversions; // 自检计数

  // ---------- 判断（心情、好感共用；殖民地级的按 250 刻缓存） ----------
  static int colonyStamp=-1;static bool ordered,dragonbornColonist;static readonly Dictionary<string,int> raceCounts=new Dictionary<string,int>();static int colonists;
  public static void ForgetColony(){colonyStamp=-1;mapStamp.Clear();}
  static void Colony(){
   int now=Now;if(colonyStamp>=0 && now>=colonyStamp && now-colonyStamp<250)return;colonyStamp=now;
   ordered=dragonbornColonist=false;raceCounts.Clear();colonists=0;
   foreach(var c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists){
    if(!c.RaceProps.Humanlike)continue;colonists++;
    var k=Affinity.For(c).key;raceCounts[k]=raceCounts.TryGetValue(k,out int n)?n+1:1;
    if(Dragonborn(c))dragonbornColonist=true;
    if(!ordered && (c.royalty?.MostSeniorTitle!=null || ModsConfig.IdeologyActive && c.Ideo?.GetRole(c)?.def==PreceptDefOf.IdeoRole_Leader))ordered=true;
   }
  }
  public static bool ColonyOrdered{get{Colony();return ordered;}}
  public static bool DragonbornColonist{get{Colony();return dragonbornColonist;}}
  public static int RaceCount{get{Colony();return raceCounts.Count;}}
  public static bool Majority(Pawn p){Colony();if(colonists==0)return true;return raceCounts.TryGetValue(Affinity.For(p).key,out int n) && n*2>colonists;}
  // 每张地图：龙、夺心魔（250 刻）。
  struct MapInfo{public int tick;public List<Pawn> dragons,flayers;}
  static readonly Dictionary<int,MapInfo> mapStamp=new Dictionary<int,MapInfo>();
  static MapInfo MapOf(Map m){
   int now=Now;if(mapStamp.TryGetValue(m.uniqueID,out var mi) && now>=mi.tick && now-mi.tick<250)return mi;
   mi=new MapInfo{tick=now,dragons=new List<Pawn>(),flayers=new List<Pawn>()};
   foreach(var o in m.mapPawns.AllPawnsSpawned){if(o.Dead)continue;if(DndCreatureType.Is(o,DndType.Dragon))mi.dragons.Add(o);if(MindFlayer.IsMindFlayer(o))mi.flayers.Add(o);}
   mapStamp[m.uniqueID]=mi;return mi;
  }
  public static Pawn NearestDragon(Pawn p,float radius){if(!p.Spawned)return null;Pawn best=null;float bd=radius*radius;foreach(var d in MapOf(p.Map).dragons)if(d!=p && d.Spawned){float x=d.Position.DistanceToSquared(p.Position);if(x<=bd){bd=x;best=d;}}return best;}
  public static bool DragonOnMap(Pawn p)=>p.Spawned && MapOf(p.Map).dragons.Any(d=>d!=p);
  public static bool FlayerOnMap(Pawn p,bool hostile=false)=>p.Spawned && MapOf(p.Map).flayers.Any(f=>f!=p && (!hostile || f.HostileTo(p)));
  static bool Colonist(Pawn p)=>p.Faction!=null && p.Faction.IsPlayer && !p.IsPrisoner;

  // 心情：返回阶段（-1 = 不显示）。
  public static int MoodStage(Pawn p,ThoughtDef def){
   var i=Info(p);if(i==null)return -1;int now=Now;
   switch(def.defName){
    case "LP_DT_Orderly":return i.order==1 && Colonist(p) && ColonyOrdered?0:-1;
    case "LP_DT_NoSchedule":return i.order==1 && Colonist(p) && p.timetable?.times!=null && p.timetable.times.All(x=>x==TimeAssignmentDefOf.Anything)?0:-1;
    case "LP_DT_Restrained":{var c=GameComponent_DndTraits.Get;return i.order==-1 && c!=null && p.Drafted && c.DraftedFor(p,now)>=RestrainedTicks?0:-1;}
    case "LP_DT_UndeadUnease":return i.morality==1 && Colonist(p) && ColonyCensus.Constructs>0?0:-1;
    case "LP_DT_DarkShelter":return i.morality==-1 && Colonist(p) && ColonyCensus.HasLich?0:-1;
    case "LP_DT_DragonDread":return i.dragon==-1 && DragonOnMap(p)?0:-1;
    case "LP_DT_LichDevotee":return i.undead==1 && Colonist(p) && ColonyCensus.HasLich?0:-1;
    case "LP_DT_UndeadAbhor":return i.undead==-1 && Colonist(p) && (ColonyCensus.HasLich || ColonyCensus.Constructs>0)?0:-1;
    case "LP_DT_Cosmopolitan":{if(i.race!=1 || !Colonist(p))return -1;int n=RaceCount-1;return n>=1?Math.Min(n,4)-1:-1;}
    case "LP_DT_PureMajority":return i.race==-1 && Colonist(p)?(Majority(p)?0:1):-1;
    case "LP_DT_FlayerNear":return i.hive && FlayerOnMap(p)?0:-1;
    case "LP_DT_DragonPatron":return i.servant && Colonist(p) && DragonbornColonist?0:-1;
    case "LP_DT_Outdoors":return i.nature && p.Spawned && (p.GetRoom()?.PsychologicallyOutdoors??true)?0:-1;
    case "LP_DT_ArcaneCraving":{var r=Record(p);return i.craving && r!=null && r.lastMagic!=-1 && now-r.lastMagic>CravingTicks?0:-1;}
   }
   return -1;
  }
  // 好感：p 看 other。返回阶段（-1 = 没有）。
  public static int OpinionStage(Pawn p,Pawn other,ThoughtDef def){
   if(other==null || other==p || other.RaceProps?.Humanlike!=true)return -1;
   if(def.defName=="LP_DT_RevenantDread")return RevenantBodies.Count>0 && RevenantBodies.Contains(other) && Colonist(p) && !RevenantBodies.Contains(p)?(Degree(p,DndTraitDefOf.LP_Morality)==1?1:0):-1;
   var i=Info(p);if(i==null)return -1;
   switch(def.defName){
    case "LP_DT_DragonbornDistrust":return i.dragon==1 && other.Faction!=null && other.Faction==p.Faction && Dragonborn(other)?0:-1;
    case "LP_DT_GiantDevotion":return i.giant==1 && other.Faction!=null && other.Faction==p.Faction && other.BodySize>=GiantSize?0:-1;
    case "LP_DT_GiantHatred":return i.giant==-1 && GiantRace(other)?0:-1;
    case "LP_DT_PureBlood":return i.race==-1?(Affinity.For(other).key==Affinity.For(p).key?0:1):-1;
    case "LP_DT_GithKinship":return i.hive && Gith(other)?0:-1;
    case "LP_DT_ExileLich":return i.exile && Items.IsLich(other)?0:-1;
    case "LP_DT_MagicDistrust":return i.phobe && IdeoBridge.IsCaster(other)?0:-1;
   }
   return -1;
  }
  // 世界公民：种族关系的负面心情不显示。
  public static ThoughtState FilterRacial(Pawn p,ThoughtDef def,ThoughtState s){
   if(!s.Active || def?.stages==null || Info(p)?.race!=1)return s;int st=s.StageIndex;
   return st>=0 && st<def.stages.Count && def.stages[st].baseMoodEffect<0f?ThoughtState.Inactive:s;
  }
  // 处决、摘器官、奴役一类的坏心情（按 defName；仁善只算处决和摘器官）。
  static readonly Dictionary<ThoughtDef,int> atrocity=new Dictionary<ThoughtDef,int>();
  public static int Atrocity(ThoughtDef d){
   if(d==null)return 0;if(atrocity.TryGetValue(d,out int a))return a;var n=d.defName;a=0;
   if(!n.StartsWith("LP_DT_") && d.stages!=null && d.stages.Any(s=>s!=null && s.baseMoodEffect<0f)){
    if(n.Contains("Execut") || n.Contains("OrganHarvest") || n.Contains("Harvested"))a=2;
    else if(n.Contains("Enslav") || n.Contains("Slavery"))a=1;
   }
   return atrocity[d]=a;
  }
  public static bool FoodJoy(ThoughtDef d)=>d!=null && d.defName.StartsWith("Ate") && d.stages!=null && d.stages.Any(s=>s!=null && s.baseMoodEffect>0f);
  public static void Memory(Pawn p,string def){var t=DefDatabase<ThoughtDef>.GetNamedSilentFail(def);if(t!=null)p?.needs?.mood?.thoughts?.memories?.TryGainMemory(t);}
  public static bool HasMemory(Pawn p,string def){var t=DefDatabase<ThoughtDef>.GetNamedSilentFail(def);return t!=null && p?.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(t)!=null;}

  // ---------- 战斗 ----------
  // 类型伤害：和 1.58.0 的类型加成合计封顶 +60%（HeroCombat 已经乘过 1 + 英雄部分，这里只补差）。
  public static float TypeBonus(Pawn a,DndInfo i,Pawn v,bool melee){
   if(i==null || v==null)return 0f;float b=0f;
   if(i.dragon==1 && DndCreatureType.Is(v,DndType.Dragon))b+=TypeDragon;
   if(i.giant==-1 && melee && v.BodySize>=GiantSize)b+=TypeGiant;
   if(i.undead==-1 && DndCreatureType.Is(v,DndType.Undead))b+=TypeUndead;
   if(i.revenant>=0){var r=Record(a);if(r?.killerFaction!=null && v.Faction==r.killerFaction)b+=i.revenant==0?TypeRevenant:TypeAtRest;}
   return b;
  }
  public static float OutFactor(Pawn a,DndInfo i,Pawn v,DamageInfo d){
   float f=1f;bool melee=HeroCombat.Melee(d);
   float mine=TypeBonus(a,i,v,melee);
   if(mine>0f){float hero=HeroCombat.TypeBonus(a,HeroTraits.Info(a),v,melee);f*=(1f+Mathf.Min(HeroTraits.TypeCap,hero+mine))/(1f+hero);}
   if(i.wild && HeroDestiny.VPEActive && HeroCombat.Spell(d))f*=WildSpell;
   if(i.morality==1 && HeroDestiny.VPEActive && d.Def!=null && d.Def==Faith.Radiant)f*=HolyBonus;
   return f;
  }
  public static float InFactor(Pawn v,DndInfo i,Pawn a,DamageInfo d){
   float f=1f;if(i==null)return f;
   if(i.phobe && a!=null && a!=v && a.HostileTo(v) && HeroCombat.Spell(d))f*=PhobeSpell;
   if(i.lycan>0 && Beasts.Contains(v) && BeastResisted(a,d))f*=BeastResist;
   return f;
  }
  // 兽形：只有武器伤害减半；爪牙、法术、神术、法杖、魔法武器、银武器（材料白银或名字带 Silver）不减。
  public static bool BeastResisted(Pawn a,DamageInfo d){
   var w=d.Weapon;if(w==null || !w.IsWeapon)return false;
   if(HeroCombat.MagicThing(w) || Arcana.Group(w)=="G" || w.defName.IndexOf("Silver",StringComparison.OrdinalIgnoreCase)>=0)return false;
   var eq=a?.equipment?.Primary;if(eq!=null && eq.def==w && eq.Stuff==ThingDefOf.Silver)return false;
   return true;
  }
  // 战术家光环：被征召的战术家 4 格内的同派系友军（不含自己），250 刻刷新；多个战术家不叠加。
  public static readonly HashSet<Pawn> Aura=new HashSet<Pawn>();
  public static float AuraHit(Pawn p)=>Aura.Count>0 && p!=null && Aura.Contains(p)?AuraHitBonus:0f;
  public static void RefreshAura(IEnumerable<Pawn> tacticians){
   Aura.Clear();
   foreach(var t in tacticians){
    if(!t.Spawned || t.Dead || t.Downed || !t.Drafted || t.Faction==null)continue;
    foreach(var o in t.Map.mapPawns.SpawnedPawnsInFaction(t.Faction))if(o!=t && !o.Dead && o.Position.DistanceToSquared(t.Position)<=AuraRadius*AuraRadius)Aura.Add(o);
   }
  }
  // 巨人仆从：3 格内有体型 1.5 以上的殖民者（250 刻刷新）。
  public static readonly HashSet<Pawn> NearGiant=new HashSet<Pawn>();
  // 兽形 / 亡魂之躯 / 位面错乱的人（登记表，热路径先看是不是空的）。
  public static readonly HashSet<Pawn> Beasts=new HashSet<Pawn>(),RevenantBodies=new HashSet<Pawn>(),PlaneTouched=new HashSet<Pawn>();
  public static readonly HashSet<string> KillerIds=new HashSet<string>();
  public static void Reset(){Aura.Clear();NearGiant.Clear();Beasts.Clear();RevenantBodies.Clear();PlaneTouched.Clear();KillerIds.Clear();ForgetColony();}

  // ---------- 复仇亡魂 ----------
  public static bool OnRevenantDeath(Pawn v,Pawn killer){
   var i=Info(v);if(i==null || i.revenant!=0 || killer==null || killer==v || killer.Dead || killer.Faction==null || v.Faction==null || !killer.Faction.HostileTo(v.Faction))return false;
   var r=EnsureRecord(v);int now=Now;if(r==null || r.revenantCooldown!=-1 && now<r.revenantCooldown)return false;
   if(v.Corpse==null || v.Corpse.Destroyed)return false;
   r.killerId=killer.ThingID;r.killerFaction=killer.Faction;r.killerName=killer.LabelShort;
   var c=GameComponent_DndTraits.Get;if(c==null)return false;c.pending.Add(new RevenantPending{pawn=v,tick=now+RiseTicks});
   if(v.Faction.IsPlayer && !TestSuppressed)Messages.Message(v.LabelShort+"的亡魂不肯离去：12 小时后，"+v.LabelShort+"会从尸体里爬回来找"+killer.LabelShort+"。别让尸体被烧掉。",v.Corpse,MessageTypeDefOf.NeutralEvent,false);
   return true;
  }
  public static bool Rise(Pawn p){
   if(p==null || !p.Dead)return false;
   if(!ResurrectionUtility.TryResurrect(p,null) || p.Dead)return false;
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_MissingPart && !(h.Part!=null && p.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(h.Part))).ToList())p.health.RemoveHediff(h);
   if(DndTraitDefOf.LP_RevenantBody!=null && !Items.Has(p,DndTraitDefOf.LP_RevenantBody))p.health.AddHediff(DndTraitDefOf.LP_RevenantBody);
   RevenantBodies.Add(p);
   var r=EnsureRecord(p);r.revenantCooldown=Now+RevenantCooldown;
   if(p.Faction!=null && p.Faction.IsPlayer && !TestSuppressed)Find.LetterStack.ReceiveLetter("复仇亡魂归来",p.LabelShort+"从死亡里爬了回来，伤势全愈，身上带着坟土的寒气。\n\n"+p.LabelShort+"只想找到"+(r.killerName??"凶手")+"。下一次复活要等 60 天。",LetterDefOf.NeutralEvent,p);
   GameComponent_DndTraits.Get?.MarkDirty();
   return true;
  }
  // 亲手杀死凶手：下一级所需经验的 50%，特性变为安息之魂。
  public static bool OnKillerSlain(Pawn revenant,Pawn victim){
   var i=Info(revenant);if(i==null || i.revenant!=0 || victim==null)return false;var r=Record(revenant);if(r==null || r.killerId==null || r.killerId!=victim.ThingID)return false;
   float xp=NextLevelSpan(Adventurer.Get(revenant))*.5f;if(xp>0f)Adventurer.GainXP(revenant,xp,false);
   var fac=r.killerFaction;r.killerId=null;r.killerName=null;
   HeroTraits.Give(revenant,DndTraitDefOf.LP_Revenant,1);
   var rr=Record(revenant);if(rr!=null)rr.killerFaction=fac;
   var body=revenant.health.hediffSet.GetFirstHediffOfDef(DndTraitDefOf.LP_RevenantBody);if(body!=null)revenant.health.RemoveHediff(body);
   RevenantBodies.Remove(revenant);
   if(revenant.Faction!=null && revenant.Faction.IsPlayer && !TestSuppressed)Find.LetterStack.ReceiveLetter("血仇已报",revenant.LabelShort+"亲手杀死了"+victim.LabelShort+"。亡魂终于安息——"+revenant.LabelShort+"成了「安息之魂」，获得 "+xp.ToString("0")+" 点冒险者经验。",LetterDefOf.PositiveEvent,revenant);
   GameComponent_DndTraits.Get?.MarkDirty();
   return true;
  }
  public static float NextLevelSpan(Hediff_Adventurer h){
   if(h==null || h.level>=h.LevelCap)return 0f;int l=h.level;
   if(l<Adventurer.MaxLevel)return Adventurer.XpTable[l]-Adventurer.XpTable[l-1];
   return Legend.XpFor(l+1)-Legend.XpFor(l);
  }
  public static void OnKilled(Pawn v,Pawn killer){
   if(Info(v)?.revenant==0)OnRevenantDeath(v,killer);
   if(killer!=null && killer!=v){
    var ki=Info(killer);
    if(ki!=null){
     if(ki.revenant==0)OnKillerSlain(killer,v);
     if(ki.dragon==1 && DndCreatureType.Is(v,DndType.Dragon))Memory(killer,"LP_DT_Dragonslain");
    }
   }
   // 殖民地的龙死去：龙之眷从 -15。
   if(v.Faction!=null && v.Faction.IsPlayer && DndCreatureType.Is(v,DndType.Dragon))foreach(var c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)if(c!=v && Info(c)?.servant==true)Memory(c,"LP_DT_DragonLost");
  }

  // ---------- 兽化症 ----------
  public static HediffDef FormDef(int degree)=>degree==1?DndTraitDefOf.LP_Beastform_Wolf:degree==2?DndTraitDefOf.LP_Beastform_Bear:DndTraitDefOf.LP_Beastform_Tiger;
  public static readonly string[] FormNames={"","狼","熊","虎"};
  public static Hediff_Beastform Form(Pawn p){var l=p?.health?.hediffSet?.hediffs;if(l!=null)for(int k=0;k<l.Count;k++)if(l[k] is Hediff_Beastform b)return b;return null;}
  public static bool Shapeshift(Pawn p,bool forced){
   var i=Info(p);if(i==null || i.lycan<=0 || p.Dead || Form(p)!=null)return false;
   var def=FormDef(i.lycan);if(def==null)return false;
   var h=(Hediff_Beastform)HediffMaker.MakeHediff(def,p);h.forced=forced;p.health.AddHediff(h);
   var r=EnsureRecord(p);int now=Now;r.lastForm=now;if(forced)r.lycanNext=now+Days(10f,15f);
   Items.Mote(p,(forced?"兽性发作：":"化身")+FormNames[i.lycan]+"形",new Color(.85f,.35f,.3f));
   if(forced && p.Faction!=null && p.Faction.IsPlayer && !TestSuppressed)Messages.Message(p.LabelShort+"的兽化症发作了，化身"+FormNames[i.lycan]+"形，6 小时内会扑向最近的敌人。",p,MessageTypeDefOf.ThreatSmall,false);
   return true;
  }
  // 化身期间的失控：扑向 25 格内最近的敌人；附近没有敌人时（每次化身判定一次）10% 狂暴。
  public static void Rage(Pawn p,Hediff_Beastform h){
   if(!h.forced || !p.Spawned || p.Downed || p.InMentalState || Record(p)?.tamed==true)return;
   Pawn best=null;float bd=25f*25f;
   foreach(var o in p.Map.mapPawns.AllPawnsSpawned){if(o==p || o.Dead || o.Downed || !o.HostileTo(p))continue;float d=o.Position.DistanceToSquared(p.Position);if(d<bd){bd=d;best=o;}}
   if(best!=null){
    if(p.CurJobDef==JobDefOf.AttackMelee && p.CurJob.targetA.Thing==best)return;
    var job=JobMaker.MakeJob(JobDefOf.AttackMelee,best);job.killIncappedTarget=false;job.expiryInterval=600;job.checkOverrideOnExpire=true;
    p.jobs.StartJob(job,JobCondition.InterruptForced,null,false,true);return;
   }
   if(h.berserkRolled)return;h.berserkRolled=true;
   if(Roll<BerserkChance)p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk,"兽化症失控",true,false,false,null,false,false,false);
  }
  public static void LycanTick(Pawn p,DndInfo i,int now){
   var r=EnsureRecord(p);if(r==null)return;
   if(r.tamed || p.Dead)return;
   var form=Form(p);if(form!=null){Rage(p,form);return;}
   if(r.lycanNext==-1)r.lycanNext=now+Days(10f,15f);
   bool low=p.health.summaryHealth.SummaryHealthPercent<LowHealth && (r.lastForm==-1 || now-r.lastForm>=GenDate.TicksPerDay);
   if(now>=r.lycanNext || low)Shapeshift(p,true);
  }
  public static bool CheckTamed(Pawn p){
   var r=Record(p);if(r==null || r.tamed)return false;var h=Adventurer.Get(p);if(h==null || h.level<TameLevel)return false;
   r.tamed=true;Form(p)?.Calm();SyncAbilities(p);
   if(p.Faction!=null && p.Faction.IsPlayer && !TestSuppressed)Find.LetterStack.ReceiveLetter("驯服野性",p.LabelShort+"终于学会了和体内的野兽共处：兽化症不再强制发作、不再失控，"+p.LabelShort+"可以每天主动化身一次。",LetterDefOf.PositiveEvent,p);
   return true;
  }
  public static void Bite(Pawn v,Pawn a,DamageInfo d){
   if(Beasts.Count==0 || !Beasts.Contains(a) || v.Dead || !v.RaceProps.Humanlike || !(d.Weapon==null || d.Weapon.race!=null))return;
   var ai=Info(a);if(ai==null || ai.lycan<=0 || Roll>=BiteChance)return;
   Infect(v,DndTraitDefOf.LP_Lycanthrope,ai.lycan);
  }
  public static bool HolyWeak(Pawn p)=>p!=null && Info(p)?.lycan>0;

  // ---------- 远域之触 ----------
  public static void StartDaze(Pawn p,int ticks,string text){
   if(!p.Spawned || p.Dead || p.Downed || p.InMentalState || p.jobs==null)return;
   var job=JobMaker.MakeJob(JobDefOf.Wait_MaintainPosture);job.expiryInterval=ticks;
   p.jobs.StartJob(job,JobCondition.InterruptForced,null,false,true);Items.Mote(p,text,new Color(.6f,.5f,.85f));
  }
  public static void Whisper(Pawn p){
   StartDaze(p,WhisperTicks,"远域低语……");
   if(!p.Spawned)return;
   foreach(var o in p.Map.mapPawns.AllHumanlikeSpawned)if(o!=p && !o.Dead && o.Position.DistanceToSquared(p.Position)<=WhisperRadius*WhisperRadius && o.needs?.mood!=null)Memory(o,"LP_DT_FarWhisper");
  }
  // 心灵冲击：前方 5 格、60° 锥形内的敌人。
  public static int PsychicShock(Pawn caster,IntVec3 target){
   if(caster?.Map==null)return 0;var map=caster.Map;int n=0;
   Vector3 dir=(target.ToVector3Shifted()-caster.Position.ToVector3Shifted());dir.y=0f;if(dir.sqrMagnitude<.01f)dir=caster.Rotation.FacingCell.ToVector3();dir.Normalize();
   foreach(var o in GenRadial.RadialDistinctThingsAround(caster.Position,map,5.9f,true).OfType<Pawn>().ToList()){
    if(o==caster || o.Dead || !o.HostileTo(caster))continue;
    Vector3 to=o.Position.ToVector3Shifted()-caster.Position.ToVector3Shifted();to.y=0f;if(to.sqrMagnitude<.01f || Vector3.Angle(dir,to)>30f)continue;
    o.stances?.stunner?.StunFor(120,caster,false,true,false);
    o.TakeDamage(new DamageInfo(Githyanki.Psychic??DamageDefOf.Blunt,15f,1f,-1f,caster));n++;
   }
   FleckMaker.Static(caster.Position,map,FleckDefOf.PsycastAreaEffect,2f);
   return n;
  }

  // ---------- 狂野魔法浪涌（12 种，都不致命） ----------
  public static readonly string[] Surges={"invisible","flowers","fireball","heal","chicken","entropy","haste","inspire","stun","burn","green","daze"};
  public static int SurgeCount;
  public static string Surge(Pawn p,string which=null){
   if(p==null || p.Dead)return null;
   if(which==null){foreach(var s in Surges.InRandomOrder()){var got=Surge(p,s);if(got!=null)return got;}return null;}
   var color=new Color(.75f,.6f,1f);var map=p.MapHeld;
   switch(which){
    case "invisible":Items.Give(p,"LP_PotionInvisibility",600);Items.Mote(p,"浪涌：隐身",color);break;
    case "flowers":{
     if(map==null || !p.Spawned)return null;var plant=new[]{"Plant_Rose","Plant_Daylily","Plant_Dandelion","Plant_Grass"}.Select(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)).FirstOrDefault(d=>d?.plant!=null);
     if(plant!=null)foreach(var c in GenAdj.CellsAdjacent8Way(p).Where(c=>c.InBounds(map) && c.GetPlant(map)==null && c.GetEdifice(map)==null && map.fertilityGrid.FertilityAt(c)>=plant.plant.fertilityMin).Take(4)){var pl=(Plant)GenSpawn.Spawn(plant,c,map);pl.Growth=1f;}
     Items.Mote(p,"浪涌：脚下开满了花",new Color(1f,.7f,.85f));break;}
    case "fireball":{
     if(map==null || !p.Spawned)return null;
     var foe=p.Map.mapPawns.AllPawnsSpawned.Where(o=>!o.Dead && !o.Downed && o.HostileTo(p) && o.Position.DistanceTo(p.Position)<=20f).OrderBy(o=>o.Position.DistanceToSquared(p.Position)).FirstOrDefault();
     if(foe==null)return null;
     var spare=GenRadial.RadialDistinctThingsAround(foe.Position,map,3f,true).OfType<Pawn>().Where(o=>!o.HostileTo(p)).Cast<Thing>().ToList();
     GenExplosion.DoExplosion(foe.Position,map,1.5f,DamageDefOf.Flame,p,damAmount:6,chanceToStartFire:0f,ignoredThings:spare);
     Items.Mote(foe,"浪涌：小火球",color);break;}
    case "heal":{
     if(map==null)return null;var team=p.Faction==null?new List<Pawn>{p}:map.mapPawns.SpawnedPawnsInFaction(p.Faction).Where(o=>!o.Dead).ToList();if(!team.Contains(p))team.Add(p);
     foreach(var o in team)Heal(o,5f);Items.Mote(p,"浪涌：全队治疗",new Color(.6f,1f,.6f));break;}
    case "chicken":{
     if(map==null || !p.Spawned)return null;var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Chicken");if(kind==null)return null;
     var ch=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,p.Faction!=null && p.Faction.IsPlayer?p.Faction:null));GenSpawn.Spawn(ch,CellFinder.RandomClosewalkCellNear(p.Position,map,2),map);
     Items.Mote(p,"浪涌：咕咕？",color);break;}
    case "entropy":p.psychicEntropy?.RemoveAllEntropy();Items.Mote(p,"浪涌：心灵熵清空",color);break;
    case "haste":Items.Give(p,"LP_DT_SurgeHaste");Items.Mote(p,"浪涌：疾行",color);break;
    case "inspire":{
     var ih=p.mindState?.inspirationHandler;if(ih==null || ih.Inspired)return null;
     var defs=DefDatabase<InspirationDef>.AllDefsListForReading.Where(d=>d.Worker!=null && d.Worker.InspirationCanOccur(p)).ToList();if(defs.Count==0)return null;
     if(!ih.TryStartInspiration(defs.RandomElement(),"狂野魔法浪涌",false))return null;Items.Mote(p,"浪涌：灵感",color);break;}
    case "stun":{
     if(map==null || !p.Spawned)return null;
     foreach(var o in GenRadial.RadialDistinctThingsAround(p.Position,map,6f,true).OfType<Pawn>().ToList())if(o!=p && !o.Dead && o.HostileTo(p))o.stances?.stunner?.StunFor(120,p,false,true,false);
     Items.Mote(p,"浪涌：震荡",color);break;}
    case "burn":if(!p.Spawned)return null;FireUtility.TryAttachFire(p,.1f,p);Items.Mote(p,"浪涌：着火了！",new Color(1f,.5f,.3f));break;
    case "green":{
     if(p.story==null)return null;var h=Items.Give(p,"LP_WildMagicHue",GenDate.TicksPerDay) as Hediff_WildMagicHue;
     if(h!=null){var g=new Color(.45f,.75f,.4f);h.hue=g;p.story.skinColorOverride=g;p.Drawer?.renderer?.SetAllGraphicsDirty();}
     Items.Mote(p,"浪涌：皮肤变绿了",new Color(.5f,.85f,.45f));break;}
    case "daze":p.stances?.stunner?.StunFor(300,p,false,false,false);Items.Mote(p,"浪涌：发呆……",color);break;
    default:return null;
   }
   SurgeCount++;return which;
  }
  public static void Heal(Pawn p,float amount){
   foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>x.CanHealNaturally() || x.CanHealFromTending()).ToList()){
    if(amount<=0f)break;float take=Mathf.Min(amount,inj.Severity);inj.Heal(take);amount-=take;
   }
  }
  public static void OnCast(Pawn p){var i=Info(p);if(i==null || !i.wild || !HeroDestiny.VPEActive || Roll>=WildChance)return;Surge(p);}

  // ---------- 怪癖 ----------
  public static bool MagicConsumable(ThingDef d)=>d!=null && d.defName.StartsWith("LP_") && (d.defName.Contains("Potion") || d.defName.Contains("Scroll") || d.defName.Contains("Elixir"));
  public static void ArcaneFix(Pawn p){
   if(Info(p)?.craving!=true)return;var r=EnsureRecord(p);if(r!=null)r.lastMagic=Now;Memory(p,"LP_DT_ArcaneFix");
  }
  static readonly Dictionary<AbilityDef,bool> teleport=new Dictionary<AbilityDef,bool>();
  public static bool Teleport(AbilityDef d){
   if(d==null)return false;if(teleport.TryGetValue(d,out bool t))return t;var n=d.defName;
   t=d.comps!=null && d.comps.Any(c=>c?.compClass!=null && (c.compClass.Name.Contains("Teleport") || c.compClass.Name.Contains("Skip") || c.compClass.Name.Contains("Blink")))
     || new[]{"Skip","Teleport","Blink","Step","Soar","Jump"}.Any(k=>n.IndexOf(k,StringComparison.Ordinal)>=0);
   return teleport[d]=t;
  }
  public static bool TreasureInDungeon(){foreach(var m in Find.Maps)if(Mythal.IsFloorMap(m))foreach(var p in m.mapPawns.FreeColonistsSpawned)if(Info(p)?.treasure==true)return true;return false;}
  public static void TreasureCall(Map map){if(map==null)return;foreach(var p in map.mapPawns.FreeColonistsSpawned)if(Info(p)?.treasure==true)Memory(p,"LP_DT_CallOfAdventure");}
  public static bool IsHoard(Room room)=>DragonHoard.Valid(room) && DragonHoard.Owners(room).Count>0;
  public static bool HaulingToHoard(Pawn p){
   if(p?.carryTracker?.CarriedThing==null || !p.Spawned)return false;var job=p.CurJob;if(job==null)return false;
   var c=job.targetB.Cell;if(!job.targetB.IsValid || !c.InBounds(p.Map))return false;
   return IsHoard(c.GetRoom(p.Map));
  }
  // VPE 路径经验：职业天赋 ×1.25 与背景、神之勇士等已有的倍率取较高，不叠加。
  public static float ClassXp(Pawn p,string path,float current){
   var i=Info(p);if(i==null)return current;
   bool hit=path=="LP_Path_Fighter" && i.tactician || path=="LP_Path_Wizard" && i.arcane || path=="LP_Path_Artificer" && i.tinker || path=="LP_Path_Druid" && i.nature;
   return hit?Math.Max(current,ClassXpBonus):current;
  }
  public static float VpeEntropyFactor(Pawn p)=>Info(p)?.arcane==true?ArcaneEntropy:1f;
  public static string ExtraTip(Pawn p,Trait t){
   if(p==null || t==null)return null;var r=Record(p);var sb=new StringBuilder();int now=Now;
   if(t.def==DndTraitDefOf.LP_Revenant && r!=null){
    if(t.Degree==0)sb.Append("凶手："+(r.killerName??"（还没有）"));
    if(r.killerFaction!=null)sb.Append("\n仇敌派系："+r.killerFaction.Name);
    if(t.Degree==0)sb.Append("\n复活："+(r.revenantCooldown==-1 || now>=r.revenantCooldown?"可用":"冷却中，还要 "+(r.revenantCooldown-now).ToStringTicksToPeriod()));
   }
   if(t.def==DndTraitDefOf.LP_Lycanthrope && r!=null)sb.Append(r.tamed?"已驯服野性：可以每天主动化身一次":"野性未驯（冒险者 10 级驯服）"+(r.lycanNext>now?"；下次发作约在 "+(r.lycanNext-now).ToStringTicksToPeriod()+" 后":""));
   return sb.Length==0?null:sb.ToString().Trim();
  }
 }

 // ---------- 存档数据 ----------
 public class Hediff_DndRecord : HediffWithComps {
  public string killerId,killerName;public Faction killerFaction;public int revenantCooldown=-1;
  public int lycanNext=-1,lastForm=-1;public bool tamed;
  public int whisperNext=-1,flayerWarn=-1,lastMagic=-1,surgeNext=-1,dazeNext=-1;public bool inDungeon;
  public override bool Visible=>false;
  public override bool ShouldRemove=>false;
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref killerId,"lpKillerId");Scribe_Values.Look(ref killerName,"lpKillerName");Scribe_References.Look(ref killerFaction,"lpKillerFaction");Scribe_Values.Look(ref revenantCooldown,"lpRevenantCooldown",-1);
   Scribe_Values.Look(ref lycanNext,"lpLycanNext",-1);Scribe_Values.Look(ref lastForm,"lpLastForm",-1);Scribe_Values.Look(ref tamed,"lpTamed");
   Scribe_Values.Look(ref whisperNext,"lpWhisperNext",-1);Scribe_Values.Look(ref flayerWarn,"lpFlayerWarn",-1);Scribe_Values.Look(ref lastMagic,"lpLastMagic",-1);Scribe_Values.Look(ref surgeNext,"lpSurgeNext",-1);Scribe_Values.Look(ref dazeNext,"lpDazeNext",-1);Scribe_Values.Look(ref inDungeon,"lpInDungeon");
  }
 }
 // 兽形：近战 ×2、移速 +1（XML），每 250 刻恢复 1 点伤势（每小时 10 点）；登记在 DndTraits.Beasts。
 public class Hediff_Beastform : HediffWithComps {
  public bool forced,berserkRolled;
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);if(pawn!=null){DndTraits.Beasts.Add(pawn);pawn.Drawer?.renderer?.SetAllGraphicsDirty();}}
  public override void PostRemoved(){base.PostRemoved();if(pawn!=null){DndTraits.Beasts.Remove(pawn);pawn.Drawer?.renderer?.SetAllGraphicsDirty();if(pawn.InMentalState && pawn.MentalStateDef==MentalStateDefOf.Berserk && forced)pawn.mindState.mentalStateHandler.Reset();}}
  public override void TickInterval(int delta){
   base.TickInterval(delta);if(pawn==null || pawn.Dead)return;DndTraits.Beasts.Add(pawn); // 读档后重新登记
   if(pawn.IsHashIntervalTick(DndTraits.BeastHealEvery,delta))DndTraits.Heal(pawn,1f);
  }
  public void Calm(){forced=false;}
  public void End(){if(pawn?.health!=null && pawn.health.hediffSet.hediffs.Contains(this))pawn.health.RemoveHediff(this);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref forced,"lpForced");Scribe_Values.Look(ref berserkRolled,"lpBerserkRolled");}
 }
 public class RevenantPending : IExposable {
  public Pawn pawn;public int tick;
  public void ExposeData(){Scribe_References.Look(ref pawn,"pawn");Scribe_Values.Look(ref tick,"tick");}
 }

 // ---------- 能力 ----------
 public class CompProperties_DndAbility : CompProperties_AbilityEffect {public string effect;public CompProperties_DndAbility(){compClass=typeof(CompAbilityEffect_Dnd);}}
 public class CompAbilityEffect_Dnd : CompAbilityEffect {
  public new CompProperties_DndAbility Props=>(CompProperties_DndAbility)props;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   base.Apply(target,dest);var p=parent.pawn;
   if(Props.effect=="shock")DndTraits.PsychicShock(p,target.Cell);
   else if(Props.effect=="shape")DndTraits.Shapeshift(p,false);
  }
  public override bool GizmoDisabled(out string reason){
   if(Props.effect=="shape" && DndTraits.Form(parent.pawn)!=null){reason="已经是兽形了。";return true;}
   return base.GizmoDisabled(out reason);
  }
 }

 // ---------- 心情 / 好感 ----------
 public class ThoughtWorker_DndMood : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){int s=DndTraits.MoodStage(p,def);return s<0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(s);}
 }
 public class ThoughtWorker_DndOpinion : ThoughtWorker {
  protected override ThoughtState CurrentSocialStateInternal(Pawn p,Pawn other){int s=DndTraits.OpinionStage(p,other,def);return s<0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(s);}
 }

 // ---------- 属性：守序 / 巨人仆从工作速度、机械迷修理、奥术天赋（无 VPE）心灵敏感度、自然之子驯兽、龙之眷从搬运、远域之触心灵熵上限 ----------
 public class StatPart_DndTraits : StatPart {
  float Factor(Pawn p,out float offset,out string why){
   offset=0f;why=null;var i=DndTraits.Info(p);if(i==null)return 1f;var s=parentStat;float f=1f;
   if(s==StatDefOf.WorkSpeedGlobal){
    if(i.order==1 && p.timetable?.CurrentAssignment==TimeAssignmentDefOf.Work){f*=DndTraits.OrderWork;why="守序（工作时段）";}
    if(i.giant==1 && DndTraits.NearGiant.Contains(p)){f*=DndTraits.ServantWork;why=(why==null?"":why+"、")+"巨人仆从（3 格内有巨人）";}
   }
   else if(s==StatDefOf.ConstructionSpeed){if(i.tinker && p.CurJobDef==JobDefOf.Repair){f*=DndTraits.TinkerRepair;why="机械迷（修理）";}}
   else if(s==StatDefOf.PsychicSensitivity){if(i.arcane && !HeroDestiny.VPEActive){offset=DndTraits.ArcaneSensitivity;why="奥术天赋";}}
   else if(s==StatDefOf.TameAnimalChance || s==StatDefOf.TrainAnimalChance){if(i.nature){f*=DndTraits.NatureTame;why="自然之子";}}
   else if(s==StatDefOf.MoveSpeed){if(i.servant && DndTraits.HaulingToHoard(p)){f*=DndTraits.ServantHaul;why="龙之眷从（搬进宝库）";}}
   else if(s==DndStartup.EntropyMax){if(i.far){offset=DndTraits.FarEntropy;why="远域之触";}}
   return f;
  }
  public override void TransformValue(StatRequest req,ref float val){if(!(req.Thing is Pawn p))return;float f=Factor(p,out float o,out _);val=val*f+o;}
  public override string ExplanationPart(StatRequest req){
   if(!(req.Thing is Pawn p))return null;float f=Factor(p,out float o,out string why);if(why==null)return null;
   return o!=0f?why+": +"+(parentStat==DndStartup.EntropyMax?o.ToString("0"):o.ToStringPercent()):why+": x"+f.ToStringPercent();
  }
 }
 [StaticConstructorOnStartup]
 public static class DndStartup {
  public static StatDef EntropyMax;
  static DndStartup(){
   if(GenCommandLine.CommandLineArgPassed("lichtest"))DndTraits.TestSuppressed=true; // 自测进程：性格特性检查组自己临时打开
   EntropyMax=DefDatabase<StatDef>.GetNamedSilentFail("PsychicEntropyMax");
   foreach(var stat in new[]{StatDefOf.WorkSpeedGlobal,StatDefOf.ConstructionSpeed,StatDefOf.PsychicSensitivity,StatDefOf.TameAnimalChance,StatDefOf.TrainAnimalChance,StatDefOf.MoveSpeed,EntropyMax}){
    if(stat==null)continue;if(stat.parts==null)stat.parts=new List<StatPart>();
    if(!stat.parts.Any(x=>x is StatPart_DndTraits))stat.parts.Add(new StatPart_DndTraits{parentStat=stat});
    stat.immutable=false;
   }
  }
 }

 // ---------- 周期：250 刻（光环、巨人邻近、征召计时、兽化、惧龙、夺心魔警告、地牢）、2500 刻（名单、低语、出神、浪涌、驯服） ----------
 public class GameComponent_DndTraits : GameComponent {
  public List<RevenantPending> pending=new List<RevenantPending>();
  readonly List<Pawn> holders=new List<Pawn>();bool dirty=true;
  readonly Dictionary<Pawn,int> draftStart=new Dictionary<Pawn,int>();
  public GameComponent_DndTraits(Game game){}
  public static GameComponent_DndTraits Get=>GameComp<GameComponent_DndTraits>.Get();
  public void MarkDirty(){dirty=true;}
  public int DraftedFor(Pawn p,int now)=>draftStart.TryGetValue(p,out int s)?now-s:0;
  public void SetDraftStart(Pawn p,int tick){draftStart[p]=tick;}
  public override void FinalizeInit(){base.FinalizeInit();DndTraits.ClearCache();DndTraits.Reset();draftStart.Clear();Refresh();}
  public List<Pawn> Holders{get{if(dirty)Refresh();return holders;}}
  public void Refresh(){
   dirty=false;holders.Clear();DndTraits.RevenantBodies.Clear();DndTraits.PlaneTouched.Clear();DndTraits.KillerIds.Clear();
   var seen=new HashSet<Pawn>();
   void Add(Pawn p){if(p==null || p.Dead || !seen.Add(p))return;var i=DndTraits.Info(p);if(i==null)return;holders.Add(p);if(i.plane)DndTraits.PlaneTouched.Add(p);var r=DndTraits.Record(p);if(i.revenant==0 && r?.killerId!=null)DndTraits.KillerIds.Add(r.killerId);if(Items.Has(p,DndTraitDefOf.LP_RevenantBody))DndTraits.RevenantBodies.Add(p);}
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawnsSpawned)Add(p);
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)Add(p);
  }
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;
   if(now%250==137)Tick250(now);
   if(now%2500==1337)Tick2500(now);
  }
  public void Tick250(int now){
   if(pending.Count>0)ProcessRevenants(now);
   var list=Holders;if(list.Count==0){DndTraits.Aura.Clear();DndTraits.NearGiant.Clear();draftStart.Clear();return;}
   var tacticians=new List<Pawn>();DndTraits.NearGiant.Clear();
   foreach(var p in list.ToList()){
    if(p.Dead || p.Destroyed)continue;var i=DndTraits.Info(p);if(i==null)continue;
    try{
     if(i.tactician)tacticians.Add(p);
     if(i.order==-1){if(p.Drafted){if(!draftStart.ContainsKey(p))draftStart[p]=now;}else draftStart.Remove(p);}
     if(i.giant==1 && p.Spawned && p.Faction!=null)foreach(var o in p.Map.mapPawns.SpawnedPawnsInFaction(p.Faction))if(o!=p && o.RaceProps.Humanlike && o.BodySize>=DndTraits.GiantSize && o.Position.DistanceToSquared(p.Position)<=DndTraits.ServantRadius*DndTraits.ServantRadius){DndTraits.NearGiant.Add(p);break;}
     if(i.lycan>0)DndTraits.LycanTick(p,i,now);
     if(i.dragon==-1 && p.Spawned && !p.Downed && !p.Drafted && !p.InMentalState && p.CurJobDef!=JobDefOf.Flee){
      var d=DndTraits.NearestDragon(p,DndTraits.DragonFleeRadius);
      if(d!=null && DndTraits.Roll<DndTraits.FleeChance){var dest=CellFinderLoose.GetFleeDest(p,new List<Thing>{d},16f);if(dest.IsValid && dest!=p.Position){p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Flee,dest,d),JobCondition.InterruptOptional,null,false,true);Items.Mote(p,"龙！快跑！",new Color(.9f,.6f,.4f));}}
     }
     if(i.far && p.Spawned && p.Faction!=null && p.Faction.IsPlayer && DndTraits.FlayerOnMap(p,true)){var r=DndTraits.EnsureRecord(p);if(r.flayerWarn==-1 || now-r.flayerWarn>=GenDate.TicksPerDay || now<r.flayerWarn){r.flayerWarn=now;if(!DndTraits.TestSuppressed)Messages.Message(p.LabelShort+"听见远域里有什么在低语：附近有敌对的夺心魔！",p,MessageTypeDefOf.ThreatBig,false);}}
     if(i.treasure && p.Faction!=null && p.Faction.IsPlayer){var r=DndTraits.EnsureRecord(p);bool inside=p.Spawned && Mythal.IsFloorMap(p.Map);if(inside && !r.inDungeon)DndTraits.Memory(p,"LP_DT_CallOfAdventure");r.inDungeon=inside;}
    }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 性格特性 250 刻出错："+e,76200001);}
   }
   foreach(var k in draftStart.Keys.Where(k=>k==null || k.Dead || !k.Drafted).ToList())draftStart.Remove(k);
   DndTraits.RefreshAura(tacticians);
  }
  public void Tick2500(int now){
   Refresh();
   foreach(var p in holders.ToList()){
    var i=DndTraits.Info(p);if(i==null || p.Dead)continue;
    try{
     if(i.lycan>0)DndTraits.CheckTamed(p);
     if(i.far){var r=DndTraits.EnsureRecord(p);if(r.whisperNext==-1)r.whisperNext=now+DndTraits.Days(5f,8f);else if(now>=r.whisperNext && p.Spawned && !p.Drafted){r.whisperNext=now+DndTraits.Days(5f,8f);DndTraits.Whisper(p);}}
     if(i.plane){var r=DndTraits.EnsureRecord(p);if(r.dazeNext==-1)r.dazeNext=now+DndTraits.Days(10f,15f);else if(now>=r.dazeNext && p.Spawned && !p.Drafted){r.dazeNext=now+DndTraits.Days(10f,15f);DndTraits.StartDaze(p,DndTraits.PlaneDazeTicks,"位面错乱……");}}
     if(i.wild && !HeroDestiny.VPEActive){var r=DndTraits.EnsureRecord(p);if(r.surgeNext==-1)r.surgeNext=now+DndTraits.Days(.5f,1.5f);else if(now>=r.surgeNext && p.Spawned){r.surgeNext=now+DndTraits.Days(.5f,1.5f);DndTraits.Surge(p);}}
     if(i.craving){var r=DndTraits.EnsureRecord(p);if(r.lastMagic==-1)r.lastMagic=now;}
    }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 性格特性 2500 刻出错："+e,76200002);}
   }
   if(now%60000<2500)DndTraits.Cleanup();
  }
  public int ProcessRevenants(int now){
   int n=0;
   for(int k=pending.Count-1;k>=0;k--){
    var e=pending[k];var p=e.pawn;
    if(p==null || !p.Dead){pending.RemoveAt(k);continue;}
    var corpse=p.Corpse;
    if(corpse==null || corpse.Destroyed){pending.RemoveAt(k);if(p.Faction!=null && p.Faction.IsPlayer && !DndTraits.TestSuppressed)Messages.Message(p.LabelShort+"的尸体不在了，亡魂无处可归。",MessageTypeDefOf.NegativeEvent,false);continue;}
    if(now<e.tick || corpse.MapHeld==null)continue;
    pending.RemoveAt(k);if(DndTraits.Rise(p))n++;
   }
   return n;
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref pending,"lpRevenantPending",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(pending==null)pending=new List<RevenantPending>();pending.RemoveAll(x=>x==null || x.pawn==null);}
  }
 }

 // ==================== Harmony ====================
 // 生成：在 1.58.0 TalentGenerate（Priority.High）之后。
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class DndGenerate {static void Postfix(Pawn __result){if(__result!=null && !__result.Dead && __result.RaceProps?.Humanlike==true)DndTraits.OnGenerated(__result);}}
 // 同一特性不同程度的互斥：仁善 × 心理变态 / 嗜血 / 巫妖信徒，邪恶 × 善良 / 不死猎人，狂野魔法 × 奥术天赋 / 魔法恐惧。
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.GainTrait))]
 public static class DndGainBlock {static bool Prefix(Pawn ___pawn,Trait trait)=>!DndTraits.DegreeConflict(___pawn,trait);}
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.GainTrait))]
 public static class DndGain {static void Postfix(Pawn ___pawn,Trait trait){if(trait==null || !DndTraits.Is(trait.def) || ___pawn?.story?.traits?.allTraits.Contains(trait)!=true)return;DndTraits.Gained(___pawn,trait);}}
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.RemoveTrait))]
 public static class DndLose {static void Postfix(Pawn ___pawn,Trait trait){if(trait==null || !DndTraits.Is(trait.def))return;DndTraits.Lost(___pawn,trait);}}
 [HarmonyPatch(typeof(Trait),nameof(Trait.TipString))]
 public static class DndTip {static void Postfix(Trait __instance,Pawn pawn,ref string __result){if(__instance==null || !DndTraits.IsCurse(__instance.def))return;var x=DndTraits.ExtraTip(pawn,__instance);if(!x.NullOrEmpty())__result=__result+"\n\n"+x;}}
 // 伤害：类型加成（与 1.58.0 合计封顶）、狂野魔法法术、仁善神圣、魔法恐惧、兽形减伤。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class DndDamage {
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   var def=dinfo.Def;if(def==null || !def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return true;
   var a=dinfo.Instigator as Pawn;var vi=DndTraits.Info(__instance);var ai=a==null || a==__instance?null:DndTraits.Info(a);
   if(vi==null && ai==null)return true;
   float f=1f;
   if(ai!=null && a.HostileTo(__instance))f*=DndTraits.OutFactor(a,ai,__instance,dinfo);
   if(vi!=null)f*=DndTraits.InFactor(__instance,vi,a,dinfo);
   if(Math.Abs(f-1f)<.0001f)return true;
   dinfo.SetAmount(dinfo.Amount*f);if(dinfo.Amount<=.001f){absorbed=true;return false;}
   return true;
  }
 }
 // 兽形咬伤传染。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class DndAfterHit {
  static void Postfix(Thing __instance,DamageInfo dinfo,DamageWorker.DamageResult __result){
   if(DndTraits.Beasts.Count==0 || !(__instance is Pawn v) || !(dinfo.Instigator is Pawn a) || a==v || __result==null || __result.totalDamageDealt<=0f)return;
   DndTraits.Bite(v,a,dinfo);
  }
 }
 // 击杀：复仇亡魂（死亡 / 杀死凶手）、屠龙志士、龙之眷从。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class DndKill {
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out Pawn __state){__state=__instance.Dead?null:dinfo?.Instigator as Pawn;if(__instance.Dead)__state=__instance;}
  static void Postfix(Pawn __instance,Pawn __state){
   if(!__instance.Dead || __state==__instance)return;
   try{DndTraits.OnKilled(__instance,__state);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 性格特性击杀结算出错："+e,76200003);}
  }
 }
 // 战术家光环：近战命中 +5%；巨人杀手躲闪体型 1.5 以上敌人 +10%（在决斗者的掷骰之前）；射击 ×1.05。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class DndMeleeHit {static void Postfix(Verb_MeleeAttack __instance,ref float __result){float b=DndTraits.AuraHit(__instance.CasterPawn);if(b!=0f)__result=Mathf.Clamp01(__result+b);}}
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetDodgeChance")]
 public static class DndDodge {
  [HarmonyPriority(Priority.High)]
  static void Postfix(Verb_MeleeAttack __instance,LocalTargetInfo __0,ref float __result){
   if(!(__0.Thing is Pawn t))return;var a=__instance.CasterPawn;if(a==null || a.BodySize<DndTraits.GiantSize || DndTraits.Info(t)?.giant!=-1)return;
   __result=Mathf.Clamp01(__result+DndTraits.GiantDodge);
  }
 }
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class DndShot {
  static readonly AccessTools.StructFieldRef<ShotReport,float> Shooter=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromShooterAndDist");
  static void Postfix(Thing caster,Verb verb,ref ShotReport __result){if(DndTraits.Aura.Count==0 || !(caster is Pawn p) || verb==null || verb.IsMeleeAttack || !DndTraits.Aura.Contains(p))return;Shooter(ref __result)*=DndTraits.AuraShot;}
 }
 // 守序：不会纵火、乱跑、出走。
 // （挡在 TryStartMentalState：各精神崩溃 Worker 可能重写 BreakCanOccur，挂在基类上拦不全。）
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class DndOrderBreaks {
  public static readonly HashSet<string> Blocked=new HashSet<string>{"FireStartingSpree","RunWild","GiveUpExit","Wander_Psychotic","Wander_Sad","Wander_OwnRoom"};
  static bool Prefix(MentalStateDef stateDef,bool causedByMood,Pawn ___pawn,ref bool __result){
   if(!causedByMood || stateDef==null || !Blocked.Contains(stateDef.defName) || DndTraits.Info(___pawn)?.order!=1)return true;
   __result=false;return false;
  }
 }
 // 混乱：灵感平均间隔 ÷1.5（几率 ×1.5）。
 [HarmonyPatch(typeof(InspirationHandler),"get_StartInspirationMTBDays")]
 public static class DndChaosInspiration {static void Postfix(InspirationHandler __instance,ref float __result){if(__result>0f && DndTraits.Info(__instance.pawn)?.order==-1)__result/=1.5f;}}
 // 仁善：给囚犯包扎 +2；亡魂之躯：治疗效果 -50%。
 [HarmonyPatch(typeof(TendUtility),nameof(TendUtility.DoTend))]
 public static class DndTendDeed {static void Postfix(Pawn doctor,Pawn patient){if(doctor!=null && patient!=null && doctor!=patient && patient.IsPrisonerOfColony && DndTraits.Info(doctor)?.morality==1)DndTraits.Memory(doctor,"LP_DT_GoodDeed");}}
 [HarmonyPatch(typeof(TendUtility),nameof(TendUtility.CalculateBaseTendQuality),new[]{typeof(Pawn),typeof(Pawn),typeof(float),typeof(float)})]
 public static class DndRevenantTend {static void Postfix(Pawn patient,ref float __result){if(DndTraits.RevenantBodies.Count>0 && patient!=null && DndTraits.RevenantBodies.Contains(patient))__result*=.5f;}}
 // 仁善：把倒地的人救回床上 +2。
 [HarmonyPatch(typeof(JobDriver_TakeToBed),"MakeNewToils")]
 public static class DndRescueDeed {
  static void Postfix(JobDriver_TakeToBed __instance){
   var d=__instance;if(d?.job?.def!=JobDefOf.Rescue || DndTraits.Info(d.pawn)?.morality!=1)return;var p=d.pawn;
   d.AddFinishAction(c=>{if(c==JobCondition.Succeeded)DndTraits.Memory(p,"LP_DT_GoodDeed");});
  }
 }
 // 记忆：邪恶不吃处决 / 摘器官 / 奴役的坏心情；仁善吃了再加「良心不安」；亡魂之躯吃东西没有好心情。
 [HarmonyPatch(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)})]
 public static class DndMemory {
  static bool Prefix(MemoryThoughtHandler __instance,Thought_Memory __0){
   var def=__0?.def;var p=__instance?.pawn;if(def==null || p==null)return true;
   if(DndTraits.RevenantBodies.Count>0 && DndTraits.RevenantBodies.Contains(p) && DndTraits.FoodJoy(def))return false;
   var i=DndTraits.Info(p);if(i==null || i.morality==0)return true;
   int a=DndTraits.Atrocity(def);if(a==0)return true;
   if(i.morality==-1)return false;
   if(a==2)DndTraits.Memory(p,"LP_DT_Conscience");
   return true;
  }
 }
 // 邪恶：压制奴隶 +20%。
 [HarmonyPatch(typeof(SlaveRebellionUtility),nameof(SlaveRebellionUtility.IncrementSuppression))]
 public static class DndEvilSuppression {static void Prefix(Pawn initiator,ref float suppressionIncrementPct){if(suppressionIncrementPct>0f && DndTraits.Info(initiator)?.morality==-1)suppressionIncrementPct*=1.2f;}}
 // 成为巫妖：失去仁善；被心灵震爆打倒：5% 远域之触。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.AddHediff),new[]{typeof(Hediff),typeof(BodyPartRecord),typeof(DamageInfo?),typeof(DamageWorker.DamageResult)})]
 public static class DndHediffAdded {
  static void Postfix(Hediff hediff,Pawn ___pawn){
   if(hediff==null || ___pawn==null)return;
   if(hediff is Hediff_Lich){DndTraits.LichConverted(___pawn);return;}
   if(hediff.def==MindFlayer.Blasted && DndTraits.Roll<DndTraits.BlastChance)DndTraits.Infect(___pawn,DndTraitDefOf.LP_FarRealmTouched,0);
  }
 }
 // 世界公民：种族关系的负面心情无效；吉斯流亡者：不再因殖民地的巫妖高兴。
 [HarmonyPatch(typeof(ThoughtWorker_DNDRacialRelation),"CurrentStateInternal")]
 public static class DndCosmopolitan {static void Postfix(ThoughtWorker_DNDRacialRelation __instance,Pawn p,ref ThoughtState __result){__result=DndTraits.FilterRacial(p,__instance.def,__result);}}
 [HarmonyPatch(typeof(ThoughtWorker_GithSeesLich),"CurrentStateInternal")]
 public static class DndGithExile {static void Postfix(Pawn p,ref ThoughtState __result){if(__result.Active && DndTraits.Info(p)?.exile==true)__result=ThoughtState.Inactive;}}
 // 复仇亡魂：凶手在同一张地图上时不能隐身。
 [HarmonyPatch(typeof(InvisibilityUtility),nameof(InvisibilityUtility.IsPsychologicallyInvisible))]
 public static class DndKillerVisible {
  static void Postfix(Pawn pawn,ref bool __result){
   if(!__result || DndTraits.KillerIds.Count==0 || pawn==null || !pawn.Spawned || !DndTraits.KillerIds.Contains(pawn.ThingID))return;
   foreach(var o in pawn.Map.mapPawns.AllPawnsSpawned)if(!o.Dead && DndTraits.Info(o)?.revenant==0 && DndTraits.Record(o)?.killerId==pawn.ThingID){__result=false;return;}
  }
 }
 // 位面错乱：裂隙野兽不把他当目标。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.ThreatDisabled))]
 public static class DndPlaneThreat {
  static void Postfix(Pawn __instance,IAttackTargetSearcher disabledFor,ref bool __result){
   if(__result || DndTraits.PlaneTouched.Count==0 || !DndTraits.PlaneTouched.Contains(__instance) || !(disabledFor?.Thing is Pawn s))return;
   var l=s.health?.hediffSet?.hediffs;if(l!=null)for(int k=0;k<l.Count;k++)if(l[k] is Hediff_PlanarSpawn){__result=true;return;}
  }
 }
 // 位面错乱：传送类能力冷却 -20%。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.StartCooldown))]
 public static class DndTeleportCooldown {static void Prefix(RimWorld.Ability __instance,ref int ticks){if(ticks>0 && DndTraits.Info(__instance.pawn)?.plane==true && DndTraits.Teleport(__instance.def))ticks=Mathf.RoundToInt(ticks*.8f);}}
 // 狂野魔法（装了 VPE）：施放本模组 / 原版能力 5% 浪涌；VPE 施法在联动程序集里。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class DndWildCast {static void Postfix(RimWorld.Ability __instance,bool __result){if(__result)DndTraits.OnCast(__instance.pawn);}}
 // 魔法恐惧：被友方施加增益法术 -3。
 [HarmonyPatch(typeof(CompAbilityEffect_GiveHediff),nameof(CompAbilityEffect_GiveHediff.Apply))]
 public static class DndPhobeBuff {
  static void Postfix(CompAbilityEffect_GiveHediff __instance,LocalTargetInfo __0){
   var t=__0.Pawn;var c=__instance.parent?.pawn;if(t==null || c==null || t==c || DndTraits.Info(t)?.phobe!=true)return;
   var d=(__instance.props as CompProperties_AbilityGiveHediff)?.hediffDef;if(d==null || d.isBad || c.HostileTo(t))return;
   DndTraits.Memory(t,"LP_DT_UnwantedBuff");
  }
 }
 // 秘法上瘾：喝下魔法药水、用掉魔法卷轴。
 [HarmonyPatch(typeof(Thing),nameof(Thing.Ingested))]
 public static class DndArcaneIngest {static void Prefix(Thing __instance,Pawn ingester){if(ingester!=null && DndTraits.MagicConsumable(__instance?.def))DndTraits.ArcaneFix(ingester);}}
 [HarmonyPatch(typeof(CompUsable),nameof(CompUsable.UsedBy))]
 public static class DndArcaneUse {static void Prefix(CompUsable __instance,Pawn p){if(p!=null && DndTraits.MagicConsumable(__instance?.parent?.def))DndTraits.ArcaneFix(p);}}
 // 寻宝狂：法师塔坠落、地牢宝物、拆古代遗迹。
 [HarmonyPatch(typeof(TowerCrash),nameof(TowerCrash.Crash))]
 public static class DndTowerTreasure {static void Postfix(Map map,Building_TowerWreck __result){if(__result!=null)DndTraits.TreasureCall(map);}}
 [HarmonyPatch(typeof(Mythal),nameof(Mythal.RollLoot))]
 public static class DndMythalTreasure {
  static void Postfix(ref List<Thing> __result){
   if(__result==null || !DndTraits.TreasureInDungeon() || DndTraits.Roll>=DndTraits.TreasureLoot)return;
   var src=__result.Where(t=>t!=null && t.def!=Mythal.ShardDef.Def).ToList();Thing extra=null;
   if(src.Count>0){var o=src.RandomElement();extra=ThingMaker.MakeThing(o.def,o.Stuff);extra.stackCount=1;}
   else{var n=Mythal.RandomMagicItem();var d=Mythal.Thing(n);if(d!=null)extra=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);}
   if(extra!=null)__result.Add(extra);
  }
 }
 public class DndDeconState {public Map map;public IntVec3 cell;public List<ThingDefCountClass> costs;}
 [HarmonyPatch(typeof(JobDriver_Deconstruct),"FinishedRemoving")]
 public static class DndRuinsTreasure {
  static void Prefix(JobDriver_Deconstruct __instance,out DndDeconState __state){
   __state=null;var p=__instance.pawn;if(DndTraits.Info(p)?.treasure!=true)return;
   if(!(__instance.job?.targetA.Thing is Building b) || b.Faction!=null || b.Map==null || !b.Map.IsPlayerHome || b.def.resourcesFractionWhenDeconstructed<=0f)return;
   var costs=b.def.CostListAdjusted(b.Stuff);if(costs.NullOrEmpty())return;
   __state=new DndDeconState{map=b.Map,cell=b.Position,costs=costs};
  }
  static void Postfix(DndDeconState __state){
   if(__state==null)return;
   foreach(var c in __state.costs){int n=Mathf.RoundToInt(c.count*DndTraits.TreasureRuins);if(n<=0 || c.thingDef==null || c.thingDef.category!=ThingCategory.Item)continue;var t=ThingMaker.MakeThing(c.thingDef);t.stackCount=n;GenPlace.TryPlaceThing(t,__state.cell,__state.map,ThingPlaceMode.Near);}
  }
 }
 // 龙之眷从：不从龙宝库往外搬东西。
 [HarmonyPatch(typeof(HaulAIUtility),nameof(HaulAIUtility.PawnCanAutomaticallyHaulFast))]
 public static class DndServantHoard {static void Postfix(Pawn p,Thing t,bool forced,ref bool __result){if(!__result || forced || t==null || !t.Spawned || DndTraits.Info(p)?.servant!=true || !DragonHoard.Counts(t))return;if(DndTraits.IsHoard(t.GetRoom()))__result=false;}}
 // 逃离殖巢开局：人类俘虏必带殖巢幸存者，5% 远域之触。
 [HarmonyPatch(typeof(ScenPart_FlayerEscape),nameof(ScenPart_FlayerEscape.Apply))]
 public static class DndHiveScenario {
  static void Postfix(List<Pawn> starters,List<Pawn> __result){
   foreach(var p in (starters??new List<Pawn>()).Concat(__result??new List<Pawn>())){
    if(p?.story?.traits==null || !p.RaceProps.Humanlike || MindFlayer.IsMindFlayer(p) || HalfIllithid.Is(p))continue;
    if(!DndTraits.Has(p,DndTraitDefOf.LP_HiveSurvivor))p.story.traits.GainTrait(new Trait(DndTraitDefOf.LP_HiveSurvivor,0,true),true);
    if(DndTraits.Roll<DndTraits.ScenarioFarChance)DndTraits.Infect(p,DndTraitDefOf.LP_FarRealmTouched,0);
   }
  }
 }
 // 获得耐瑟启迪：1% 远域之触。
 [HarmonyPatch(typeof(NetherilInsight),nameof(NetherilInsight.Grant))]
 public static class DndInsightFar {static void Postfix(Pawn p,Hediff __result){if(__result!=null && DndTraits.Roll<DndTraits.InsightChance)DndTraits.Infect(p,DndTraitDefOf.LP_FarRealmTouched,0);}}
 // 机械迷（装了 VPE）：工业以上的远程武器射击冷却 -5%。
 [HarmonyPatch(typeof(VerbProperties),nameof(VerbProperties.AdjustedCooldown),new[]{typeof(Verb),typeof(Pawn)})]
 public static class DndTinkerCooldown {
  static void Postfix(Verb ownerVerb,Pawn attacker,ref float __result){
   if(__result<=0f || attacker==null || !HeroDestiny.VPEActive || DndTraits.Info(attacker)?.tinker!=true)return;
   var w=ownerVerb?.EquipmentSource?.def;if(w==null || !w.IsRangedWeapon || w.techLevel<TechLevel.Industrial)return;__result*=DndTraits.TinkerCooldown;
  }
 }
 // 仁善：用制伏武器打倒敌人 +2「手下留情」（1.61.0）。
 [HarmonyPatch(typeof(Nonlethal),nameof(Nonlethal.Subdue))]
 public static class DndMercy {static void Postfix(Pawn p,DamageInfo dinfo,float __result){if(__result>0f && p!=null && p.Downed && dinfo.Instigator is Pawn a && a!=p && a.HostileTo(p) && DndTraits.Info(a)?.morality==1)DndTraits.Memory(a,"LP_DT_Mercy");}}

 // ---------- 设置（左栏“DND 特性”分组，强力特性下面） ----------
 public partial class Settings {
  public bool dndPersonality=true,dndCurses=true;public int dndPersonalityRate=100;
  void ExposeDndTraits(){
   Scribe_Values.Look(ref dndPersonality,"lpDndPersonality",true);Scribe_Values.Look(ref dndCurses,"lpDndCurses",true);Scribe_Values.Look(ref dndPersonalityRate,"lpDndPersonalityRate",100);
   dndPersonalityRate=Math.Max(0,Math.Min(200,dndPersonalityRate));
  }
 }
 public static class DndTraitSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.CheckboxLabeled("DND 性格特性",ref s.dndPersonality,"（1.62.0）新生成的人形有机会带 1～2 个 DND 性格特性：阵营（守序 / 混乱、仁善 / 邪恶，最多一个）、种族互动（惧龙、屠龙志士、巨人仆从、巨人杀手、巫妖信徒、不死猎人、世界公民、纯血至上、殖巢幸存者、龙之眷从、吉斯流亡者）、职业天赋（战术家、奥术天赋、狂野魔法、机械迷、自然之子）和怪癖（魔法恐惧、秘法上瘾、寻宝狂、位面错乱）。\n只用原版 3 个特性名额里的空位：有空位时 40% 带一个，再有空位时 15% 带第二个，默认约四分之一的人形至少带一个。塞伊多邪恶与守序、费伦拓荒同盟多仁善、余烬氏族多混乱、猎巫狂热分子多守序，狗头人多龙之眷从。\n关闭后不再生成，已有的保留；读档不会给老殖民者补发。");
   l.Label("性格特性出现率："+s.dndPersonalityRate+"%");s.dndPersonalityRate=Mathf.RoundToInt(l.Slider(s.dndPersonalityRate,0,200)/5f)*5;
   l.CheckboxLabeled("诅咒之力",ref s.dndCurses,"（1.62.0）红色强力特性：复仇亡魂、兽化症（狼 / 熊 / 虎）是史诗，远域之触是稀有。和其他强力特性一起算数量，在同一次随机里出现：普通种族 1.5%、中等 1%、传奇种族 0.5%（× 强力特性出现率）；剧情首领、宿敌冠军带强力特性时 15% 是红色（不会是复仇亡魂）。兽化症能通过咬伤传染，远域之触能被心灵震爆、逃离殖巢开局、耐瑟启迪染上。\n关闭后不再随机生成、也不会传染，已有的保留。");
  }
 }
}
