using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.51.0 种族加点亲和（见 ArtSource/Leveling-1.51.0-spec.md）：每个种族一张表。
 // 六维：擅长 ×1.5（传奇种族 ×1.75）、笨拙 ×0.5，乘属性倍率（1.66.0 热修起整个倍率相乘；属性低于 10 时不乘，笨拙不压到 ×1 以下）；传奇属性点：擅长 ×1.5（传奇种族 ×1.75），人类所有项 ×1.1。
 // 倍率只改效果，不改属性数值、上限和专长门槛。查表顺序：巫妖 → 半夺心魔 → HAR 种族（pawn.def）→ 异种；查不到按人类（普通种族）。
 // xenotype / 种族 ThingDef 上的 LP_AdventurerAffinityExt 覆盖字典里的同名条目。
 // 四个稀有天赋特性（只在普通种族身上随机）：天赋异禀、大器晚成、百炼成钢、凡人之志。天赋和种族倍率作用在同一项上时取较大的一个，不相乘。
 public class LP_AdventurerAffinityExt : DefModExtension {
  public List<string> strongScores,weakScores,strongLegend;public float legendAll=1f;public bool legendary;
  // 1.58.0：挂在 TraitDef 上的特性加点（HeroTraits 汇总；种族条目不读这些）。strongLegend 在特性上按 legendStrongFactor 算。
  public int scoreCapOffset,legendPointsPerLevelOffset,asiBonusPoints,capHigherBonus;public List<int> extraAsiLevels;public float legendAllFactor=1f,legendStrongFactor=1.5f,legendFloorFactor=1f;
  public bool paragonScore,giftedScore,removeWeak;public List<string> legendFloor,capHigherOf;
 }
 public class AdventurerAffinity {
  public string key,label;public bool legendary;public readonly bool[] strong=new bool[6],weak=new bool[6],legend=new bool[Legend.Count];public float legendAll=1f;
  public const float Strong=1.5f,LegendaryStrong=1.75f,Weak=.5f,HumanLegend=1.1f;
  public float StrongMult=>legendary?LegendaryStrong:Strong;
  public float ScoreMult(int a)=>a<0 || a>5?1f:strong[a]?StrongMult:weak[a]?Weak:1f;
  public float LegendMult(int i)=>i<0 || i>=Legend.Count?1f:legend[i]?StrongMult:legendAll;
  public string Tier=>legendary?"传奇种族":"普通种族";
  public string Summary{get{
   string Join(bool[] f,string[] n)=>string.Concat(Enumerable.Range(0,f.Length).Where(i=>f[i]).Select(i=>n[i].Substring(0,1)));
   string s=Join(strong,Adventurer.Names),w=Join(weak,Adventurer.Names),l=string.Join("、",Enumerable.Range(0,Legend.Count).Where(i=>legend[i]).Select(i=>Legend.Names[i]));
   return label+"（"+Tier+"，擅长 ×"+StrongMult.ToString("0.##")+"）：六维擅长 "+(s==""?"无":s)+"，笨拙 "+(w==""?"无":w)+"；传奇擅长 "+(l==""?"无":l)+(Math.Abs(legendAll-1f)>.0001f?"（所有传奇项 ×"+legendAll.ToString("0.##")+"）":"");
  }}
 }
 public static class Affinity {
  public const string HumanKey="Human",LichKey="Lich",HalfIllithidKey="LP_HalfIllithid";
  // "key|名字|六维擅长|六维笨拙|传奇擅长|L = 传奇种族"，属性取首字（力敏体智感魅），传奇用中文名。
  static readonly string[] Rows={
   "Human|人类||||",
   "LP_Elf|高等精灵|智敏||远程 学习|","LP_Dwarf|山地矮人|力体||工作 承受|","LP_HalfOrc|半兽人|力|智|近战 血量|","LP_ForceDragon|力能龙裔|力魅||血量 承受|L",
   "Lich|巫妖|智|魅|心灵 学习|L",
   "LP_FireGiant|火巨人|体力|敏|血量 近战|L","LP_FrostGiant|霜巨人|体力|敏|血量 承受|L","LP_StormGiant|风暴巨人|力感||血量 心灵|L","LP_CloudGiant|云巨人|魅力||血量 心灵|L",
   "LP_Ettin|双头巨人|体|智|血量 近战|L","LP_EttinKin|双头巨人|体|智|血量 近战|L","LP_Cyclops|独眼巨人|力|敏|血量 工作|L","LP_CyclopsKin|独眼巨人|力|敏|血量 工作|L",
   "LP_Titan|泰坦|力魅||血量 心灵|L","LP_AwakenedTitan|觉醒泰坦|力魅||血量 心灵|L",
   "LP_Troll|巨魔|体|智|回复 血量|","LP_Ogre|食人魔|力|智|血量 近战|","LP_Minotaur|牛头人|力体||近战 承受|","LP_Loxodon|象人|感|敏|承受 工作|",
   "LP_MindFlayer|夺心魔|智|力|心灵 学习|L","LP_HalfIllithid|半夺心魔|智|力|心灵 学习|","LP_Githyanki|吉斯洋基|敏智||心灵 近战|",
   "LP_GenasiFire|火元素裔|体||近战 心灵|","LP_GenasiWater|水元素裔|体感||回复 心灵|","LP_GenasiEarth|土元素裔|体力||承受 血量|","LP_GenasiAir|风元素裔|体敏||远程 心灵|",
   "LP_Aarakocra|鸟人|敏|力|远程 回复|","LP_Genie|巨灵|魅智||心灵 回复|L","LP_Oozefolk|流浆人|体|魅|回复 承受|","LP_Aasimar|阿斯莫|魅感||心灵 回复|L",
   "LP_Tiefling|提夫林|魅智||心灵 近战|","LP_Marilith|六臂蛇人|敏力||近战 血量|L","LP_MarilithKin|六臂蛇人|敏力||近战 血量|L",
   "LP_IceDevil|寒冰魔|力||近战 承受|L","LP_Balor|炎魔|力||近战 承受|L",
   // 1.54.0 深渊仪式：坎比恩（可玩、可遗传）、魅魔，以及能被束缚的恶魔。
   "LP_Cambion|坎比恩|魅敏|感|心灵 近战|","LP_Succubus|魅魔|魅敏||心灵 回复|","LP_Dretch|劣魔|体|智|承受 工作|","LP_Barlgura|巴路古拉|力|智|近战 血量|",
   "LP_Hezrou|哈兹魔|体力|敏|承受 血量|","LP_Nabassu|纳巴苏|力体||近战 血量|","LP_ShadowDemon|影魔|敏||近战 心灵|",
   // 1.57.0 普通种族：半精灵固定 魅/敏（"魅力+两项随机"不便按人随机，见报告）。
   "LP_WoodElf|木精灵|敏感||远程 回复|","LP_Drow|卓尔精灵|敏魅||近战 心灵|","LP_HillDwarf|丘陵矮人|体感||承受 血量|",
   "LP_HalflingLightfoot|轻足半身人|敏魅||回复 工作|","LP_HalflingStout|敦实半身人|敏体||承受 回复|",
   "LP_RockGnome|岩侏儒|智体||工作 学习|","LP_ForestGnome|森林侏儒|智敏||学习 回复|","LP_DeepGnome|地侏|智力||承受 学习|",
   "LP_HalfElf|半精灵|魅敏||学习 远程|","LP_Kobold|狗头人|敏智|力|工作 远程|",
   "LP_Goblin|地精|敏体||远程 回复|","LP_Hobgoblin|大地精|体智||远程 承受|","LP_Bugbear|熊地精|力敏||近战 血量|"};
  static Dictionary<string,AdventurerAffinity> table;
  public static Dictionary<string,AdventurerAffinity> Table{get{
   if(table!=null)return table;
   table=new Dictionary<string,AdventurerAffinity>();
   foreach(var row in Rows){
    var c=row.Split('|');var a=new AdventurerAffinity{key=c[0],label=c[1],legendary=c[5]=="L"};
    foreach(char ch in c[2]){int i=Adventurer.AbilityIndex(Long(ch));if(i>=0)a.strong[i]=true;}
    foreach(char ch in c[3]){int i=Adventurer.AbilityIndex(Long(ch));if(i>=0)a.weak[i]=true;}
    foreach(var n in c[4].Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)){int i=LegendIndex(n);if(i>=0)a.legend[i]=true;}
    if(a.key==HumanKey)a.legendAll=AdventurerAffinity.HumanLegend;
    table[a.key]=a;
   }
   return table;
  }}
  static string Long(char c)=>Adventurer.Names.FirstOrDefault(n=>n[0]==c);
  public static int LegendIndex(string s){
   if(s.NullOrEmpty())return -1;s=s.Trim();
   int i=Array.IndexOf(Legend.Names,s);if(i>=0)return i;
   switch(s.ToLowerInvariant()){case "health":return Legend.Health;case "melee":return Legend.Melee;case "ranged":return Legend.Ranged;case "psychic":return Legend.Psychic;
    case "recovery":return Legend.Recovery;case "tough":case "toughness":return Legend.Tough;case "work":return Legend.Work;case "learn":case "learning":return Legend.Learn;}
   return -1;
  }
  // modExtension → 亲和表（缓存，按 def）。
  static readonly Dictionary<Def,AdventurerAffinity> extCache=new Dictionary<Def,AdventurerAffinity>();
  public static AdventurerAffinity FromExt(Def d){
   if(d==null)return null;if(extCache.TryGetValue(d,out var got))return got;
   var e=d.GetModExtension<LP_AdventurerAffinityExt>();AdventurerAffinity a=null;
   if(e!=null){
    a=new AdventurerAffinity{key=d.defName,label=d.label??d.defName,legendAll=e.legendAll,legendary=e.legendary};
    if(e.strongScores!=null)foreach(var s in e.strongScores){int i=Adventurer.AbilityIndex(s);if(i>=0)a.strong[i]=true;}
    if(e.weakScores!=null)foreach(var s in e.weakScores){int i=Adventurer.AbilityIndex(s);if(i>=0)a.weak[i]=true;}
    if(e.strongLegend!=null)foreach(var s in e.strongLegend){int i=LegendIndex(s);if(i>=0)a.legend[i]=true;}
   }
   return extCache[d]=a;
  }
  public static AdventurerAffinity Human=>Table[HumanKey];
  // 查表（不看开关）。
  public static AdventurerAffinity For(Pawn p){
   if(p==null)return Human;
   if(Items.IsLich(p))return Table[LichKey];
   if(HalfIllithid.Is(p))return Table[HalfIllithidKey];
   if(p.def!=null && p.def!=ThingDefOf.Human){var e=FromExt(p.def);if(e!=null)return e;if(Table.TryGetValue(p.def.defName,out var r))return r;}
   var x=p.genes?.Xenotype;
   if(x!=null){var e=FromExt(x);if(e!=null)return e;if(Table.TryGetValue(x.defName,out var r))return r;}
   return Human;
  }
  public static bool LegendaryRace(Pawn p)=>For(p).legendary;

  // ---------- 开关与倍率 ----------
  public static bool? forcedOn; // 自检、设置变化时比较开关前后
  public static bool On=>forcedOn??(LichMod.Settings?.adventurerAffinity??true);
  // 六维：种族倍率（开关关掉时 1）与天赋异禀 ×1.5 取较大。
  public static float ScoreMult(Hediff_Adventurer h,int a){
   if(h==null)return 1f;float m=On?h.Affinity.ScoreMult(a):1f;
   if(h.Talent==Talents.Gifted)m=Math.Max(m,Talents.GiftedMult);
   return HeroTraits.ScoreMult(h.pawn,a,m); // 1.58.0 天赋·某项、极致、反骨（同样取较大）
  }
  // 传奇：种族倍率与凡人之志 ×1.5 取较大。
  public static float LegendMult(Hediff_Adventurer h,int i){
   if(h==null)return 1f;float m=On?h.Affinity.LegendMult(i):1f;
   if(h.Talent==Talents.MortalWill)m=Math.Max(m,Talents.MortalLegendMult);
   return HeroTraits.LegendMult(h.pawn,i,m); // 1.58.0 英雄之资、极致、金刚之躯、锋芒、灵光、事迹的下限
  }
  // 调整值的有效值：正调整值乘倍率，负调整值不乘。
  public static float Apply(int mod,float mult)=>mod>0?mod*mult:mod;
  // 界面：属性后面的“擅长/笨拙”、悬停说明（天赋拉高的项也标“擅长”）。
  public static string Tag(Hediff_Adventurer h,int a){var m=ScoreMult(h,a);return m>1.0001f?" 擅长":m<.9999f?" 笨拙":"";}
  public static string LegendTag(Hediff_Adventurer h,int i){var m=LegendMult(h,i);return m>=AdventurerAffinity.Strong-.0001f?" 擅长":"";}
  static string Source(Hediff_Adventurer h,bool legend){
   var t=h.Talent;string race=On?h.Affinity.label+"，"+h.Affinity.Tier:"种族亲和已关闭";
   if(!legend && t==Talents.Gifted || legend && t==Talents.MortalWill)race+="；"+t.degreeDatas[0].label+" ×1.5 与种族倍率取较大";
   return race;
  }
  static string HeroNote(Hediff_Adventurer h,int a,bool legend){var n=HeroTraits.MultNote(h.pawn,a,legend);return n==null?"":"；特性："+n+"，与种族倍率取较大";}
  public static string Tip(Hediff_Adventurer h,int a){
   if(h==null)return "";float m=ScoreMult(h,a);if(Math.Abs(m-1f)<.0001f)return "";
   return "种族亲和（"+Source(h,false)+HeroNote(h,a,false)+"）：这项属性倍率 ×"+m.ToString("0.##")+"（属性低于 10 时不乘；笨拙不会让倍率低于 ×1）";
  }
  public static string LegendTip(Hediff_Adventurer h,int i){
   if(h==null)return "";float m=LegendMult(h,i);if(Math.Abs(m-1f)<.0001f)return "";
   return "种族亲和（"+Source(h,true)+HeroNote(h,i,true)+"）：这项每点收益 ×"+m.ToString("0.##");
  }
 }

 // ---------- 稀有天赋特性 ----------
 [DefOf]
 public static class TalentDefOf {
  public static TraitDef LP_TalentGifted,LP_TalentLateBloomer,LP_TalentTempered,LP_TalentMortalWill;
  static TalentDefOf(){DefOfHelper.EnsureInitializedInCtor(typeof(TalentDefOf));}
 }
 public static class Talents {
  public static TraitDef Gifted=>TalentDefOf.LP_TalentGifted;public static TraitDef LateBloomer=>TalentDefOf.LP_TalentLateBloomer;public static TraitDef Tempered=>TalentDefOf.LP_TalentTempered;public static TraitDef MortalWill=>TalentDefOf.LP_TalentMortalWill;
  public const string ExclusionTag="LP_PointTalent";
  public const float GiftedMult=1.5f,MortalLegendMult=1.5f,MortalXp=1.25f;
  public const int TemperedPoints=3,LateBloomerLegendBonus=1,LateBloomerEvery=4;
  public static IEnumerable<TraitDef> All{get{yield return Gifted;yield return LateBloomer;yield return Tempered;yield return MortalWill;}}
  public static bool Is(TraitDef d)=>d!=null && (d==Gifted || d==LateBloomer || d==Tempered || d==MortalWill);
  public static TraitDef Of(Pawn p){var list=p?.story?.traits?.allTraits;if(list!=null)for(int i=0;i<list.Count;i++)if(Is(list[i].def))return list[i].def;return null;}
  public static float XpFactor(Hediff_Adventurer h)=>h?.Talent==MortalWill?MortalXp:1f;
  // 传奇种族不带天赋：生成后去掉。自测进程（-lichtest）里所有人都去掉，免得随机天赋改动其他检查的经验和属性；天赋检查临时关掉 StripAll。
  public static bool StripAll;
  public static int Strip(Pawn p){
   if(p?.story?.traits==null || !StripAll && !Affinity.LegendaryRace(p))return 0;int n=0;
   foreach(var t in p.story.traits.allTraits.Where(t=>Is(t.def)).ToList()){p.story.traits.RemoveTrait(t);n++;}
   return n;
  }
 }
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class TalentGenerate {
  [HarmonyPriority(Priority.High)]
  static void Postfix(Pawn __result){if(__result!=null && !__result.Dead && __result.RaceProps?.Humanlike==true){Talents.Strip(__result);HeroTraits.OnGenerated(__result);}} // 1.58.0：天赋和强力特性在这里一起掷（commonality 0）
 }
 // 特性变化时刷新冒险者效果（天赋、生成时的特性）。
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.GainTrait))]
 public static class TalentGain {static void Postfix(Pawn ___pawn,Trait trait){if(Talents.Is(trait?.def))Adventurer.Get(___pawn)?.Dirty();}}
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.RemoveTrait))]
 public static class TalentLose {static void Postfix(Pawn ___pawn,Trait trait){if(Talents.Is(trait?.def))Adventurer.Get(___pawn)?.Dirty();}}

 // ---------- 设置（画在“冒险者”里） ----------
 public partial class Settings {
  public bool adventurerAffinity=true;
  void ExposeAffinity(){Scribe_Values.Look(ref adventurerAffinity,"adventurerAffinity",true);}
 }
}
