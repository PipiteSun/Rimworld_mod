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
  public string Tier=>legendary?"传奇种族".Loc():"普通种族".Loc();
  public string Summary{get{
   string Join(bool[] f,string[] n)=>string.Concat(Enumerable.Range(0,f.Length).Where(i=>f[i]).Select(i=>n[i].Substring(0,1)));
   string s=Join(strong,Adventurer.Names),w=Join(weak,Adventurer.Names),l=string.Join("、".Loc(),Enumerable.Range(0,Legend.Count).Where(i=>legend[i]).Select(i=>Legend.Names[i]));
   return label+"（".Loc()+Tier+"，擅长 ×".Loc()+StrongMult.ToString("0.##")+"）：六维擅长 ".Loc()+(s==""?"无".Loc():s)+"，笨拙 ".Loc()+(w==""?"无".Loc():w)+"；传奇擅长 ".Loc()+(l==""?"无".Loc():l)+(Math.Abs(legendAll-1f)>.0001f?"（所有传奇项 ×".Loc()+legendAll.ToString("0.##")+"）".Loc():"");
  }}
 }
 public static partial class Affinity {
  public const string HumanKey="Human",LichKey="Lich",HalfIllithidKey="LP_HalfIllithid";
  // "key|名字|六维擅长|六维笨拙|传奇擅长|L = 传奇种族"，属性取首字（力敏体智感魅），传奇用中文名。
  static readonly string[] Rows={
   "Human|人类||||".Loc(),
   "LP_Elf|高等精灵|智敏||远程 学习|".Loc(),"LP_Dwarf|山地矮人|力体||工作 承受|".Loc(),"LP_HalfOrc|半兽人|力|智|近战 血量|".Loc(),
   "Lich|巫妖|智|魅|心灵 学习|L".Loc(),
   "LP_FireGiant|火巨人|体力|敏|血量 近战|L".Loc(),"LP_FrostGiant|霜巨人|体力|敏|血量 承受|L".Loc(),"LP_StormGiant|风暴巨人|力感||血量 心灵|L".Loc(),"LP_CloudGiant|云巨人|魅力||血量 心灵|L".Loc(),
   
   "LP_Titan|泰坦|力魅||血量 心灵|L".Loc(),"LP_AwakenedTitan|觉醒泰坦|力魅||血量 心灵|L".Loc(),
   "LP_Troll|巨魔|体|智|回复 血量|".Loc(),"LP_Ogre|食人魔|力|智|血量 近战|".Loc(),"LP_Minotaur|牛头人|力体||近战 承受|".Loc(),"LP_Loxodon|象人|感|敏|承受 工作|".Loc(),
   "LP_MindFlayer|夺心魔|智|力|心灵 学习|L".Loc(),"LP_HalfIllithid|半夺心魔|智|力|心灵 学习|".Loc(),"LP_Githyanki|吉斯洋基|敏智||心灵 近战|".Loc(),
   "LP_GenasiFire|火元素裔|体||近战 心灵|".Loc(),"LP_GenasiWater|水元素裔|体感||回复 心灵|".Loc(),"LP_GenasiEarth|土元素裔|体力||承受 血量|".Loc(),"LP_GenasiAir|风元素裔|体敏||远程 心灵|".Loc(),
   "LP_Aarakocra|鸟人|敏|力|远程 回复|".Loc(),"LP_Genie|巨灵|魅智||心灵 回复|L".Loc(),"LP_Oozefolk|流浆人|体|魅|回复 承受|".Loc(),"LP_Aasimar|阿斯莫|魅感||心灵 回复|L".Loc(),
   "LP_Tiefling|提夫林|魅智||心灵 近战|".Loc(),"LP_Marilith|六臂蛇人|敏力||近战 血量|L".Loc(),"LP_MarilithKin|六臂蛇人|敏力||近战 血量|L".Loc(),"LP_BehemothKin|古巨兽后裔|体力|敏|血量 近战|L".Loc(),
   "LP_IceDevil|寒冰魔|力||近战 承受|L".Loc(),"LP_Balor|炎魔|力||近战 承受|L".Loc(),
   // 1.54.0 深渊仪式：坎比恩（可玩、可遗传）、魅魔，以及能被束缚的恶魔。
   "LP_Cambion|坎比恩|魅敏|感|心灵 近战|".Loc(),"LP_Succubus|魅魔|魅敏||心灵 回复|".Loc(),"LP_Dretch|劣魔|体|智|承受 工作|".Loc(),"LP_Barlgura|巴路古拉|力|智|近战 血量|".Loc(),
   "LP_Hezrou|哈兹魔|体力|敏|承受 血量|".Loc(),"LP_Nabassu|纳巴苏|力体||近战 血量|".Loc(),"LP_ShadowDemon|影魔|敏||近战 心灵|".Loc(),
   // 1.57.0 普通种族：半精灵固定 魅/敏（"魅力+两项随机"不便按人随机，见报告）。
   "LP_WoodElf|木精灵|敏感||远程 回复|".Loc(),"LP_Drow|卓尔精灵|敏魅||近战 心灵|".Loc(),"LP_HillDwarf|丘陵矮人|体感||承受 血量|".Loc(),
   "LP_HalflingLightfoot|轻足半身人|敏魅||回复 工作|".Loc(),"LP_HalflingStout|敦实半身人|敏体||承受 回复|".Loc(),
   "LP_RockGnome|岩侏儒|智体||工作 学习|".Loc(),"LP_ForestGnome|森林侏儒|智敏||学习 回复|".Loc(),"LP_DeepGnome|地侏|智力||承受 学习|".Loc(),
   "LP_HalfElf|半精灵|魅敏||学习 远程|".Loc(),"LP_Kobold|狗头人|敏智|力|工作 远程|".Loc(),
   "LP_Goblin|地精|敏体||远程 回复|".Loc(),"LP_Hobgoblin|大地精|体智||远程 承受|".Loc(),"LP_Bugbear|熊地精|力敏||近战 血量|".Loc(),
   // 1.165.0 亡灵大军的 4 个异种（之前漏了）。
   "LP_UH_GhoulXeno||敏体|智|近战 回复|","LP_UH_WightXeno||力体|魅|近战 承受|","LP_UH_WraithXeno||敏感|力|心灵 近战|","LP_UH_VampireXeno||魅敏||心灵 回复|",
   // 1.165.0 其他 mod 的异种 / 种族：按 defName 查，没装那个 mod 时这行用不到，不报错。名字留空 = 用那个 def 自己的（已翻译的）名字。
   // 原版 / DLC（基准人不写，照旧按人类）
   "Dirtmole||体感||工作 承受|","Genie||智|力|学习 工作|","Highmate||魅|力|心灵 回复|","Hussar||力敏||近战 远程|","Impid||敏|体|远程 回复|",
   "Neanderthal||力体|智|近战 承受|","Pigskin||体|魅|承受 工作|","Sanguophage||魅力||心灵 回复|","Waster||体|智|承受 远程|","Yttakin||体力|魅|承受 近战|","Starjack||敏智||远程 工作|",
   // Vanilla Races Expanded
   "VRE_Insector||体力|魅|承受 工作|","VREA_AndroidBasic||体智|魅|工作 承受|","VREA_AndroidAwakened||智体||学习 工作|","VRE_Archon||魅智||心灵 学习|L",
   "VRE_Fungoid||体|敏|回复 工作|","VRE_Lowmate||魅|智|回复 工作|","VREH_Uhlan||力敏||近战 血量|","VRE_Lycan||力体|智|近战 回复|","VRE_Wolfman||力敏|魅|近战 回复|",
   "VRE_Animakin||感魅||心灵 回复|","VRE_Gauranlenkin||感体||回复 工作|","VRE_Poluxkin||体感||回复 承受|","VRE_Ocularkin||感智||远程 心灵|","VRE_Boarskin||力体|智|近战 承受|",
   "VRE_Bruxa||敏魅||近战 心灵|","VRE_Ekkimian||智感||心灵 学习|","VRE_Strigoi||力体|魅|近战 血量|","VRESaurids_Saurid||体感||承受 回复|",
   // Alpha Genes
   "AG_Animusen||敏感||近战 回复|","AG_Drakonori||力魅||血量 承受|L","AG_Efreet||力魅||近战 心灵|L","AG_Fleetkind||敏|力|远程 回复|","AG_Forsaken||智感|魅|学习 工作|",
   "AG_Helixien||智体||学习 回复|","AG_Hiveling||体|智|工作 承受|","AG_Lapis||体|敏|承受 血量|","AG_Malachai||智魅||心灵 学习|","AG_MindDevourer||智|魅|心灵 学习|L",
   "AG_Mycormorph||体|魅|回复 承受|","AG_Nereid||敏魅||回复 远程|","AG_RoxTouched||体力|智|承受 近战|","AG_Taukai||智敏||远程 学习|","AG_Wretch||体|魅|回复 承受|",
   // 神兽血裔（独立版 MB_ 与边缘仙路版 SSY_）：神兽血脉算传奇种族
   "MB_Taotie||体力||血量 回复|L","MB_Suanni||力魅||近战 承受|L","MB_Jumang||感体||回复 工作|L","MB_Baize||智感||学习 心灵|L",
   "SSY_Taotie||体力||血量 回复|L","SSY_Suanni||力魅||近战 承受|L","SSY_Jumang||感体||回复 工作|L","SSY_Baize||智感||学习 心灵|L",
   // RimDark 40k - Mankinds Finest
   "BEWH_SpaceMarine||力体||近战 远程|","BEWH_PrimarisSpaceMarine||力体||远程 承受|","BEWH_ThunderWarrior||力体|智|近战 血量|","BEWH_Custodes||力体||近战 承受|L",
   "BEWH_Primarch||力体魅||近战 血量|L","BEWH_LivingSaint||魅感||心灵 回复|L","BEWH_Pariah||感|魅|心灵 承受|",
   // 其他
   "DMS_AndroidSynth||智体|魅|工作 学习|","BotchJob_ProfanedXenotype||体|魅|承受 回复|","BotchJob_ProfanedCursebearerXenotype||体魅||心灵 承受|","RI_ExorcismXenotype_Necrosapiens||体|魅|承受 回复|"};
  // 1.165.0 神兽血裔可以用觉醒剂让基准人觉醒（异种还是基准人），这时按血脉基因查。
  static readonly string[][] GeneRows={new[]{"MB_TaotieGene","MB_Taotie"},new[]{"MB_SuanniGene","MB_Suanni"},new[]{"MB_JumangGene","MB_Jumang"},new[]{"MB_BaizeGene","MB_Baize"}};
  static List<(GeneDef gene,string key)> geneRows;
  static List<(GeneDef gene,string key)> GeneTable{get{
   if(geneRows!=null)return geneRows;geneRows=new List<(GeneDef,string)>();
   foreach(var g in GeneRows){var d=DefDatabase<GeneDef>.GetNamedSilentFail(g[0]);if(d!=null)geneRows.Add((d,g[1]));}
   return geneRows;
  }}
  // 名字留空的行：第一次查到时用 def 自己的名字。
  static AdventurerAffinity Named(AdventurerAffinity a,Def d){if(a.label.NullOrEmpty())a.label=d?.LabelCap.ToString()??a.key;return a;}
  static Dictionary<string,AdventurerAffinity> table;
  public static Dictionary<string,AdventurerAffinity> Table{get{
   if(table!=null)return table;
   table=new Dictionary<string,AdventurerAffinity>();
   foreach(var row in Rows){
    var c=row.Split('|');var a=new AdventurerAffinity{key=c[0],label=c[1],legendary=c[5]=="L"};
    foreach(char ch in c[2]){int i="力敏体智感魅".IndexOf(ch);if(i>=0)a.strong[i]=true;}
    foreach(char ch in c[3]){int i="力敏体智感魅".IndexOf(ch);if(i>=0)a.weak[i]=true;}
    foreach(var n in c[4].Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)){int i=LegendIndex(n);if(i>=0)a.legend[i]=true;}
    if(a.key==HumanKey)a.legendAll=AdventurerAffinity.HumanLegend;
    table[a.key]=a;
   }
   return table;
  }}
  static readonly string[] ZhLegend={"血量","近战","远程","心灵","回复","承受","工作","学习"}; // 亲和表里写的是中文原名（不随语言变）
  static string Long(char c)=>Adventurer.Names.FirstOrDefault(n=>n[0]==c);
  public static int LegendIndex(string s){
   if(s.NullOrEmpty())return -1;s=s.Trim();
   int i=Array.IndexOf(Legend.Names,s);if(i>=0)return i;i=Array.IndexOf(ZhLegend,s);if(i>=0)return i;
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
   // 1.166.0：设置里“种族亲和编辑”的覆盖优先（见 AffinityEditor.cs）。
   if(Items.IsLich(p))return Override(LichKey)??Table[LichKey];
   if(HalfIllithid.Is(p))return Override(HalfIllithidKey)??Table[HalfIllithidKey];
   if(p.def!=null && p.def!=ThingDefOf.Human){var o=Override(p.def.defName,p.def);if(o!=null)return o;var e=FromExt(p.def);if(e!=null)return e;if(Table.TryGetValue(p.def.defName,out var r))return Named(r,p.def);}
   // 1.175.0：自设异种（原版异种编辑器做的，没有 XenotypeDef）按名字查设置里的覆盖；没配过的照旧往下走（血脉基因 → 人类）。
   {var cn=CustomNameOf(p);if(cn!=null){var o=Override(CustomKey(cn));if(o!=null)return o;}}
   var x=p.genes?.Xenotype;
   if(x!=null){var o=Override(x.defName,x);if(o!=null)return o;var e=FromExt(x);if(e!=null)return e;if(Table.TryGetValue(x.defName,out var r))return Named(r,x);}
   if(p.genes!=null)foreach(var g in GeneTable)if(p.genes.HasActiveGene(g.gene) && Table.TryGetValue(g.key,out var r))return Override(g.key)??Named(r,g.gene);
   return Override(HumanKey)??Human;
  }
  public static bool LegendaryRace(Pawn p)=>For(p).legendary;

  // ---------- 开关与倍率 ----------
  public static bool? forcedOn; // 自检、设置变化时比较开关前后
  public static bool On=>forcedOn??(LichMod.Settings?.adventurerAffinity??true);
  // 1.81.0 热修5：六维倍率拆成两个。
  // RaceMult：种族亲和（开关关掉时 1），反骨把笨拙拉回 ×1。1.121.0 热修起：擅长部分走 WholeMult（整个线性效果），笨拙部分走 RaceModMult（正调整值）。
  public static float RaceMult(Hediff_Adventurer h,int a){
   if(h==null)return 1f;
   return HeroTraits.RaceMult(h.pawn,a,On?h.Affinity.ScoreMult(a):1f);
  }
  // TalentMult：天赋异禀 ×1.5、天赋·某项 ×1.5、极致 ×2.5 取最大，没有则 1；线性效果（近战伤害、心灵敏感度、部位生命、职业强度）乘在整个倍率上，和种族相乘。
  public static float TalentMult(Hediff_Adventurer h,int a){
   if(h==null || a<0 || a>5)return 1f;float m=h.Talent==Talents.Gifted?Talents.GiftedMult:1f;
   return HeroTraits.TalentMult(h.pawn,a,m);
  }
  // 3.5 表类效果（AMult：负重、采矿、研究等）照旧：种族和天赋取较大，不相乘。
  public static float ScoreMult(Hediff_Adventurer h,int a){float r=RaceMult(h,a),t=TalentMult(h,a);return t>1f?Math.Max(r,t):r;}
  // 线性效果的天赋倍率：基础值 ≥ ×1 时整个乘上（低于 ×1 不乘，和 3.5 表“属性低于 10 时不乘”一致）。
  public static float Talented(float v,float t)=>v>=1f?v*t:v;
  // 1.121.0 热修（Peter：普通属性天赋也整个倍率之后乘）：种族擅长 ×1.5（传奇种族 ×1.75）和天赋一样乘在整个线性效果上，两者相乘。
  // 笨拙（<×1）不进整体倍率，仍只压正调整值（RaceModMult），免得把近战伤害等整个砍半。
  public static float WholeMult(Hediff_Adventurer h,int a)=>TalentMult(h,a)*Math.Max(1f,RaceMult(h,a));
  // 调整值上的种族倍率：只剩笨拙 ×0.5（擅长已移到整体倍率）。
  public static float RaceModMult(Hediff_Adventurer h,int a)=>Math.Min(1f,RaceMult(h,a));
  // 传奇：种族倍率与凡人之志 ×1.5 取较大。
  public static float LegendMult(Hediff_Adventurer h,int i){
   if(h==null)return 1f;float m=On?h.Affinity.LegendMult(i):1f;
   if(h.Talent==Talents.MortalWill)m=Math.Max(m,Talents.MortalLegendMult);
   return HeroTraits.LegendMult(h.pawn,i,m); // 1.58.0 英雄之资、极致、金刚之躯、锋芒、灵光、事迹的下限
  }
  // 调整值的有效值：正调整值乘倍率，负调整值不乘。
  public static float Apply(int mod,float mult)=>mod>0?mod*mult:mod;
  // 界面：属性后面的“擅长/笨拙”、悬停说明（天赋拉高的项也标“擅长”）。
  public static string Tag(Hediff_Adventurer h,int a){var m=ScoreMult(h,a);return m>1.0001f?" 擅长".Loc():m<.9999f?" 笨拙".Loc():"";}
  public static string LegendTag(Hediff_Adventurer h,int i){var m=LegendMult(h,i);return m>=AdventurerAffinity.Strong-.0001f?" 擅长".Loc():"";}
  static string Source(Hediff_Adventurer h,bool legend){
   var t=h.Talent;string race=On?h.Affinity.label+"，".Loc()+h.Affinity.Tier:"种族亲和已关闭".Loc();
   if(!legend && t==Talents.Gifted || legend && t==Talents.MortalWill)race+="；".Loc()+t.degreeDatas[0].label+" ×1.5 与种族倍率取较大".Loc();
   return race;
  }
  static string HeroNote(Hediff_Adventurer h,int a,bool legend){var n=HeroTraits.MultNote(h.pawn,a,legend);return n==null?"":"；特性：".Loc()+n+"，与种族倍率取较大".Loc();}
  // 1.81.0 热修5：分页效果表的来源标注，例如“种族擅长：调整值 ×1.5；极致：整体 ×2.5”；没有则 ""。
  public static string SourceTag(Hediff_Adventurer h,int a){
   if(h==null)return "";var parts=new List<string>();float r=RaceMult(h,a);
   if(r>1.0001f)parts.Add("种族擅长：整体 ×".Loc()+r.ToString("0.##"));else if(r<.9999f)parts.Add("种族笨拙：调整值 ×".Loc()+r.ToString("0.##"));
   var i=HeroTraits.Info(h.pawn);
   if(i!=null && i.paragon==a)parts.Add("极致：整体 ×".Loc()+HeroTraits.ParagonMult.ToString("0.##"));
   else if(i!=null && i.gifted==a)parts.Add("天赋：整体 ×".Loc()+HeroTraits.GiftedMult.ToString("0.##"));
   else if(h.Talent==Talents.Gifted)parts.Add(h.Talent.degreeDatas[0].label+"：整体 ×".Loc()+Talents.GiftedMult.ToString("0.##"));
   return string.Join("；".Loc(),parts);
  }
  // 1.81.0 热修5：种族和天赋分开写。
  public static string Tip(Hediff_Adventurer h,int a){
   if(h==null)return "";float m=ScoreMult(h,a),r=RaceMult(h,a),t=TalentMult(h,a);if(Math.Abs(m-1f)<.0001f && Math.Abs(r-1f)<.0001f && Math.Abs(t-1f)<.0001f)return "";
   string race=On?h.Affinity.label+"，".Loc()+h.Affinity.Tier:"种族亲和已关闭".Loc();var hero=HeroTraits.MultNote(h.pawn,a,false);
   var sb=new System.Text.StringBuilder();
   string rr=race+(hero!=null && hero.Contains("反骨：笨拙按 ×1".Loc())?"；反骨：笨拙按 ×1".Loc():"");
   if(r>1.0001f)sb.Append("种族擅长（".Loc()+rr+"）：近战伤害 / 心灵敏感度 / 部位生命 / 职业强度等线性效果整个 ×".Loc()+r.ToString("0.##")+"（和天赋相乘；效果低于 ×1 时不乘）".Loc());
   else if(r<.9999f)sb.Append("种族笨拙（".Loc()+rr+"）：正的调整值 ×".Loc()+r.ToString("0.##")+"（负调整值不乘）".Loc());
   else sb.Append("种族亲和（".Loc()+rr+"）：这项 ×1".Loc());
   if(t>1.0001f){
    var src=new List<string>();if(h.Talent==Talents.Gifted)src.Add(h.Talent.degreeDatas[0].label+" ×"+Talents.GiftedMult.ToString("0.##"));if(hero!=null)foreach(var s in hero.Split('、'))if(!s.Contains("反骨"))src.Add(s);
    sb.Append("\n天赋（".Loc()+string.Join("、".Loc(),src)+"，取较大）：近战伤害 / 心灵敏感度 / 部位生命 / 职业强度的整个效果 ×".Loc()+t.ToString("0.##")+"（不和种族取较大，两者相乘；效果低于 ×1 时不乘）".Loc());
   }
   float w=WholeMult(h,a);if(r>1.0001f && t>1.0001f)sb.Append("\n线性效果合计整体 ×".Loc()+w.ToString("0.##")+"（种族 ×".Loc()+r.ToString("0.##")+" × 天赋 ×".Loc()+t.ToString("0.##")+"）".Loc());
   sb.Append("\n其他属性效果（负重、采矿、研究等）倍率 ×".Loc()+m.ToString("0.##")+"（种族和天赋取较大；属性低于 10 时不乘；笨拙不会让倍率低于 ×1）".Loc());
   return sb.ToString();
  }
  public static string LegendTip(Hediff_Adventurer h,int i){
   if(h==null)return "";float m=LegendMult(h,i);if(Math.Abs(m-1f)<.0001f)return "";
   return "种族亲和（".Loc()+Source(h,true)+HeroNote(h,i,true)+"）：这项每点收益 ×".Loc()+m.ToString("0.##");
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
  // 1.164.1：四个天赋互斥，但角色编辑器自己的特性栏直接改 allTraits、不看互斥，会出现两个天赋同时在身上（Peter 的 Ruslan：百炼成钢 + 天赋异禀），
  // 而 Of 只认排在前面的那个，后加的看着在却不生效。这里只留最后加的一个（allTraits 末尾 = 最新），其余去掉；冒险者每 250 刻刷新时调用。返回去掉的个数。
  public static int Dedupe(Pawn p){
   var list=p?.story?.traits?.allTraits;if(list==null)return 0;int c=0;
   for(int i=0;i<list.Count;i++)if(Is(list[i].def))c++;
   if(c<2)return 0;
   var keep=list.Last(t=>Is(t.def));var drop=list.Where(t=>Is(t.def) && t!=keep).ToList();
   foreach(var t in drop)p.story.traits.RemoveTrait(t);
   if(drop.Count>0 && p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)
    Messages.Message(p.LabelShort+"：稀有天赋互斥，保留后加的 ".Loc()+keep.LabelCap+"，去掉 ".Loc()+string.Join("、".Loc(),drop.Select(t=>t.LabelCap))+"。".Loc(),p,MessageTypeDefOf.NeutralEvent,false);
   return drop.Count;
  }
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
 public static class TalentGain {public static void Postfix(Pawn ___pawn,Trait trait){if(Talents.Is(trait?.def))Adventurer.Get(___pawn)?.Dirty();}}
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.RemoveTrait))]
 public static class TalentLose {public static void Postfix(Pawn ___pawn,Trait trait){if(Talents.Is(trait?.def))Adventurer.Get(___pawn)?.Dirty();}}

 // ---------- 设置（画在“冒险者”里） ----------
 public partial class Settings {
  public bool adventurerAffinity=true;
  void ExposeAffinity(){Scribe_Values.Look(ref adventurerAffinity,"adventurerAffinity",true);ExposeAffinityOverrides();}
 }
}
