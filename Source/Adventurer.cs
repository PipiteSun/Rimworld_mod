using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.48.0 冒险者等级（第一阶段，见 ArtSource/Leveling-1.48.0-spec.md）：13 岁以上的人形带隐藏状态 LP_Adventurer，存等级 1～20、经验、六维属性、待加点次数和里程碑。
 // 六维效果合成在状态的 CurStage 里（每次属性、等级或设置变化时重建）；部位生命走 PartHealth（LimbHealth.cs），技能学习速度走 Pawn_SkillTracker.Learn 前缀。
 // VPE 桥（Bridges/VPE/Source/AdventurerVPE.cs）往 LevelUpHook / PrimaryAbilityHook 登记；主程序集不引用 VPE。
 public static class Adventurer {
  public const int Str=0,Dex=1,Con=2,Int=3,Wis=4,Cha=5,MaxLevel=20,ScoreCap=20,ScoreMin=1,MinAge=13,TrainingCapLevel=3,BackfillMaxLevel=5,BackfillKillsPerLevel=10;
  public static readonly string[] Names={"力量","敏捷","体质","智力","感知","魅力"};
  public static readonly int[] StandardArray={15,14,13,12,10,8};
  // 累计经验（5e 原表 ÷3）：XpTable[n-1] 是 n 级的下限。
  public static readonly int[] XpTable={0,100,300,900,2200,4500,7700,11300,16000,21300,28300,33300,40000,46700,55000,65000,75000,88300,101700,118300};
  public const float RaidShare=.25f,QuestShare=.5f,QuestHomeShare=.25f,StudyXP=50f,LessonXP=50f,ManualXP=200f;
  // 里程碑经验（每人每个 key 一次）。
  public const float PortalXP=1000f,VesselXP=3000f,DragonDuelXP=2000f,TowerXP=800f,LarlochXP=1500f,CouncilXP=2500f;
  public const string IsekaiId="jellycreative.isekaileveling";

  public static int Proficiency(int level)=>2+(Mathf.Clamp(level,1,MaxLevel)-1)/4;
  public static int Mod(int score)=>Mathf.FloorToInt((score-10)/2f);
  // 1.65.0 六维倍率曲线（D&D 3.5 负重表）：10 = ×1；10 以下 = 属性/10；10～19 依次 ×1 / 1.15 / 1.3 / 1.5 / 1.75 / 2 / 2.3 / 2.6 / 3 / 3.5，之后每 +10 再 ×4（20 = ×4，24 = ×7，30 = ×16）。
  // 1.66.0 热修：所有效果都乘整个倍率（见 Hediff_Adventurer.Table），超出寻常的力量（30+）照 3.5 规则按个位查 20～29 再 ×4 / ×16 / ×64。检定、豁免、专长门槛仍用调整值。
  // 体型：原版负重本来就按体型线性相乘（StatPart_BodySize），正好是 3.5 双足生物的 大型 ×2 / 超大型 ×4 / 巨型 ×8，不再另乘。
  static readonly float[] CurveTens={1f,1.15f,1.3f,1.5f,1.75f,2f,2.3f,2.6f,3f,3.5f};
  public static float Curve(int score){if(score<=0)return 0f;if(score<10)return score/10f;int k=score-10;return CurveTens[k%10]*Mathf.Pow(4f,k/10);}
  // 1.81.0 热修（Peter，照 3.5 规则）：3.5 的这张表只管负重，从来不管伤害。近战伤害改为 1 + 调整值 × 10%（力量 18 = ×1.4，20 = ×1.5，30 = ×2），最低 ×0.5；负重、采矿等仍用上面的表。
  public const float MeleePerMod=.1f,MeleeMin=.5f;
  public static float MeleeFactor(float mod)=>Mathf.Max(MeleeMin,1f+MeleePerMod*mod);
  public static int LevelForXp(float xp){int l=1;while(l<MaxLevel && xp>=XpTable[l])l++;return l;}
  public static string Signed(int v)=>v>=0?"+"+v:v.ToString();

  static HediffDef def;public static HediffDef Def=>def??(def=DefDatabase<HediffDef>.GetNamedSilentFail("LP_Adventurer"));
  // 这些查询在属性、学习、部位生命、射程、衰老的热路径上。1.58.6：按 thingIDNumber 登记（PostAdd / 读档登记，PostRemoved 标记失效），
  // 没登记过的人直接返回 null；登记失效时回落到原来的扫描。并发字典：属性钩子可能不在主线程。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<int,Hediff_Adventurer> registry=new System.Collections.Concurrent.ConcurrentDictionary<int,Hediff_Adventurer>();
  internal static void Register(Hediff_Adventurer h){if(h?.pawn==null)return;h.live=true;registry[h.pawn.thingIDNumber]=h;}
  internal static IEnumerable<Hediff_Adventurer> Registered=>registry.Values; // 1.59.0 超凡：读档后重建掩码登记
  public static Hediff_Adventurer Get(Pawn p){
   if(p==null || !registry.TryGetValue(p.thingIDNumber,out var r))return null;
   if(r.live && r.pawn==p)return r;
   var list=p.health?.hediffSet?.hediffs;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i] is Hediff_Adventurer h){Register(h);return h;}
   return null;
  }
  // 1.49.0：有效属性（超过当前上限的部分不算，例如失去神格后）。
  public static int Score(Pawn p,int ability){var h=Get(p);return h==null || ability<0 || ability>5?0:h.Eff(ability);}
  public static GameComponent_Adventurer State=>GameComp<GameComponent_Adventurer>.Get();

  // ---------- 设置 ----------
  // 自测期间（LichChecks）除冒险者检查组外不生成冒险者状态，免得属性效果改动其他检查用到的小人。
  public static bool TestSuppressed;
  public static bool Enabled=>LichMod.Settings?.adventurer??true;
  // 1.58.6：启用的模组在游戏运行中不会变；ModsConfig.IsActive 每次都 ToLower，而这里在每次属性计算的 CurStage 里。
  static bool? isekai;public static bool IsekaiActive=>isekai??(isekai=ModsConfig.IsActive(IsekaiId)).Value;
  public static bool EffectsHandedToIsekai=>IsekaiActive && (LichMod.Settings?.adventurerIsekaiCoexist??true);
  public static float EffectScale{get{var s=LichMod.Settings;if(s==null)return 1f;if(!s.adventurer || EffectsHandedToIsekai)return 0f;return s.adventurerEffectPercent/100f;}}
  public static float XpScale=>LichMod.Settings==null?1f:LichMod.Settings.adventurerXpPercent/100f;
  public static bool AutoASI(Pawn p)=>p.Faction==null || !p.Faction.IsPlayer || (LichMod.Settings?.adventurerAutoASI??false);

  // VPE 桥登记：升级给路径点、职业主属性、额外路径点说明。
  public static Action<Pawn,int> LevelUpHook;
  public static Func<Pawn,int> PrimaryAbilityHook;
  public static bool VPELoaded=>LevelUpHook!=null;
  public static int PrimaryAbility(Pawn p){int a=ClassSystem.Primary(p);return a>=0?a:PrimaryAbilityHook?.Invoke(p)??-1;} // 1.72.0：主职业优先

  // ---------- 对象 ----------
  public static bool Eligible(Pawn p)=>p!=null && p.RaceProps!=null && p.RaceProps.Humanlike && !p.RaceProps.IsMechanoid && p.ageTracker!=null && p.ageTracker.AgeBiologicalYears>=MinAge && !Excluded(p);
  // 死灵造物、召唤物（VPE 桥的 LP_Conjured）没有冒险者等级。
  public static bool Excluded(Pawn p)=>Necro.Construct(p)!=null || Items.Has(p,"LP_Conjured");
  public static LP_AdventurerKindExt KindExt(Pawn p)=>p?.kindDef?.GetModExtension<LP_AdventurerKindExt>();
  public static int AbilityIndex(string s){
   if(s.NullOrEmpty())return -1;
   switch(s.Trim().ToLowerInvariant()){case "str":case "力量":return Str;case "dex":case "敏捷":return Dex;case "con":case "体质":return Con;case "int":case "智力":return Int;case "wis":case "感知":return Wis;case "cha":case "魅力":return Cha;}
   return -1;
  }
  public static Hediff_Adventurer TryAdd(Pawn p,int startLevel=1,bool force=false){
   if(TestSuppressed && !force || !Eligible(p) || Def==null)return null;
   var h=Get(p);if(h!=null)return h;
   h=(Hediff_Adventurer)HediffMaker.MakeHediff(Def,p);
   var b=BaseArray(p);h.Setup(GenerateScores(p,b),startLevel);h.buyBase=b; // 1.76.0 购点基础值
   p.health.AddHediff(h);
   OpenPointBuy(p);
   HeroTraits.Invalidate(p);Feats2.GiveOrigin(h); // 1.58.1 起源专长
   if(h.pendingASI>0 && AutoASI(p))h.AutoAssign();
   return h;
  }

  // ---------- 六维生成 ----------
  static int Skill(Pawn p,SkillDef s){var r=p?.skills?.GetSkill(s);return r==null || r.TotallyDisabled?-1:r.Level;}
  static readonly HashSet<string> ToughGenes=new HashSet<string>{"Robust","LP_HalfOrcRelentless","LP_TrollRegeneration"};
  // 体质的排序键不看技能：基础 5（约等于中等技能），健壮体型 +3、Tough +3、坚韧/再生类基因 +2。
  public static float ConKey(Pawn p){
   float k=5f;
   if(p?.story?.bodyType==BodyTypeDefOf.Hulk)k+=3f;
   if(p?.story?.traits!=null && Has(p,"Tough"))k+=3f;
   if(p?.genes!=null)foreach(var g in p.genes.GenesListForReading)if(g.Active && (ToughGenes.Contains(g.def.defName) || g.def.defName.Contains("Regenerat"))){k+=2f;break;}
   return k;
  }
  public static float[] SortKeys(Pawn p)=>new[]{Skill(p,SkillDefOf.Melee),Skill(p,SkillDefOf.Shooting),ConKey(p),Skill(p,SkillDefOf.Intellectual),Mathf.Max(Skill(p,SkillDefOf.Medicine),Skill(p,SkillDefOf.Plants),Skill(p,SkillDefOf.Animals)),(float)Skill(p,SkillDefOf.Social)};
  // 标准数组按排序键从高到低分配；同分按力敏体智感魅的顺序。1.48.1：兵种模板（LP_AdventurerKindExt）写了 primary/secondary 时这两项放最高两档。
  public static int[] BaseArray(Pawn p){
   var key=SortKeys(p);var ext=KindExt(p);
   if(ext!=null){if(ext.Secondary>=0)key[ext.Secondary]=1000f;if(ext.Primary>=0)key[ext.Primary]=2000f;}
   var order=Enumerable.Range(0,6).OrderByDescending(i=>key[i]).ThenBy(i=>i).ToArray();
   var s=new int[6];for(int r=0;r<6;r++)s[order[r]]=StandardArray[r];return s;
  }
  // 种族加值（力敏体智感魅）。巨人类已经有巨躯规则，力量加值不比别的种族高。
  public static readonly Dictionary<string,int[]> Racial=new Dictionary<string,int[]>{
   {"LP_Elf",new[]{0,2,0,0,1,0}},{"LP_Dwarf",new[]{0,0,2,0,1,0}},{"LP_HalfOrc",new[]{2,0,1,0,0,0}},{"LP_ForceDragon",new[]{2,0,0,0,0,1}},
   {"LP_FireGiant",new[]{2,0,1,0,0,0}},{"LP_FrostGiant",new[]{2,0,1,0,0,0}},{"LP_StormGiant",new[]{2,0,0,0,1,0}},{"LP_CloudGiant",new[]{1,0,0,0,0,2}},
   {"LP_Ettin",new[]{2,0,2,-2,0,0}},{"LP_EttinKin",new[]{2,0,2,-2,0,0}},{"LP_Cyclops",new[]{2,-1,1,0,0,0}},{"LP_CyclopsKin",new[]{2,-1,1,0,0,0}},
   {"LP_Titan",new[]{2,0,0,0,0,2}},{"LP_AwakenedTitan",new[]{2,0,0,0,0,2}},{"LP_Troll",new[]{1,0,2,0,0,0}},{"LP_Ogre",new[]{3,0,0,-2,0,0}},
   {"LP_Minotaur",new[]{2,0,1,0,0,0}},{"LP_Loxodon",new[]{0,0,1,0,2,0}},{"LP_MindFlayer",new[]{0,0,0,2,0,1}},{"LP_Githyanki",new[]{0,2,0,1,0,0}},
   {"LP_GenasiFire",new[]{0,0,2,1,0,0}},{"LP_GenasiWater",new[]{0,0,2,0,1,0}},{"LP_GenasiEarth",new[]{1,0,2,0,0,0}},{"LP_GenasiAir",new[]{0,1,2,0,0,0}},
   {"LP_Aarakocra",new[]{0,2,0,0,1,0}},{"LP_Genie",new[]{0,0,0,1,0,2}},{"LP_Oozefolk",new[]{0,1,2,0,0,0}},{"LP_Aasimar",new[]{0,0,0,0,1,2}},
   {"LP_Tiefling",new[]{0,0,0,1,0,2}},{"LP_Marilith",new[]{1,2,0,0,0,0}},{"LP_MarilithKin",new[]{1,2,0,0,0,0}},
   {"LP_IceDevil",new[]{2,0,1,0,0,0}},{"LP_Balor",new[]{2,0,0,0,0,1}},
   // 1.57.0 普通种族：精灵/矮人亚种、半身人、侏儒、半精灵、狗头人、地精三族。
   {"LP_WoodElf",new[]{0,2,0,0,1,0}},{"LP_Drow",new[]{0,2,0,0,0,1}},{"LP_HillDwarf",new[]{0,0,2,0,1,0}},
   {"LP_HalflingLightfoot",new[]{0,2,0,0,0,1}},{"LP_HalflingStout",new[]{0,2,1,0,0,0}},
   {"LP_RockGnome",new[]{0,0,1,2,0,0}},{"LP_ForestGnome",new[]{0,1,0,2,0,0}},{"LP_DeepGnome",new[]{0,1,0,2,0,0}},
   {"LP_HalfElf",new[]{0,1,1,0,0,2}},{"LP_Kobold",new[]{-2,2,0,0,0,0}},
   {"LP_Goblin",new[]{0,2,1,0,0,0}},{"LP_Hobgoblin",new[]{0,0,2,1,0,0}},{"LP_Bugbear",new[]{2,1,0,0,0,0}},
   {"Lich",new[]{0,0,1,2,0,0}}};
  // 巫妖优先；HAR 种族看 pawn.def；其余看异种。查不到返回 null（按人类：最高两项各 +1）。
  public static string RaceKey(Pawn p){
   if(p==null)return null;
   if(Items.IsLich(p))return "Lich";
   if(p.def!=null && p.def!=ThingDefOf.Human && Racial.ContainsKey(p.def.defName))return p.def.defName;
   var x=p.genes?.Xenotype?.defName;
   return x!=null && Racial.ContainsKey(x)?x:null;
  }
  public static int[] RacialBonus(Pawn p,int[] baseScores){
   var key=RaceKey(p);if(key!=null)return (int[])Racial[key].Clone();
   var b=new int[6];foreach(int i in Enumerable.Range(0,6).OrderByDescending(i=>baseScores[i]).ThenBy(i=>i).Take(2))b[i]=1;return b;
  }
  // 背景特性 +1；原版特性：美貌 魅 ±1、快/慢学者 智 ±1。
  public static int[] TraitBonus(Pawn p){
   var b=new int[6];if(p?.story?.traits==null)return b;
   if(BackgroundTraits.Has(p,BackgroundTraits.Soldier))b[Str]++;
   if(BackgroundTraits.Has(p,BackgroundTraits.Criminal))b[Dex]++;
   if(BackgroundTraits.Has(p,BackgroundTraits.FolkHero))b[Con]++;
   if(BackgroundTraits.Has(p,BackgroundTraits.Sage))b[Int]++;
   if(BackgroundTraits.Has(p,BackgroundTraits.Hermit))b[Wis]++;
   if(BackgroundTraits.Has(p,BackgroundTraits.Noble))b[Cha]++;
   if(BackgroundTraits.Has(p,BackgroundTraits.Acolyte))b[Wis]++;
   if(BackgroundTraits.Has(p,BackgroundTraits.Outlander))b[Con]++;
   var beauty=DefDatabase<TraitDef>.GetNamedSilentFail("Beauty");var t=beauty==null?null:p.story.traits.GetTrait(beauty);
   if(t!=null)b[Cha]+=Math.Sign(t.Degree);
   if(Has(p,"FastLearner"))b[Int]++;
   if(Has(p,"SlowLearner"))b[Int]--;
   return b;
  }
  static bool Has(Pawn p,string trait){var d=DefDatabase<TraitDef>.GetNamedSilentFail(trait);return d!=null && p.story.traits.HasTrait(d);}
  public static int[] GenerateScores(Pawn p)=>GenerateScores(p,BaseArray(p));
  public static int[] GenerateScores(Pawn p,int[] baseArray){
   var s=(int[])baseArray.Clone();var r=RacialBonus(p,s);var t=TraitBonus(p);
   for(int i=0;i<6;i++)s[i]=Mathf.Clamp(s[i]+r[i]+t[i],ScoreMin,ScoreCap);
   return s;
  }
  // ---------- 1.76.0 初始属性购点 ----------
  // 生成时记下标准数组那一份基础值（15/14/13/12/10/8 正好 27 点）。玩家阵营的人（开局殖民者、直接加入的人、之后招募的人）在“冒险者”分页里按 5e 购点制重新分配一次，确认后锁定。
  // 基础值 8～15，费用 8:0 9:1 10:2 11:3 12:4 13:5 14:7 15:9；种族、背景加值不变。改基础值时 scores 同步加减，已有的属性提升、恩赐都保留。
  public const int BuyMin=8,BuyMax=15,BuyDefault=27;
  static readonly int[] BuyCostTable={0,1,2,3,4,5,7,9};
  public static int BuyCost(int v)=>BuyCostTable[Mathf.Clamp(v,BuyMin,BuyMax)-BuyMin];
  public static int BuyCost(int[] b){int n=0;if(b!=null)for(int i=0;i<6;i++)n+=BuyCost(b[i]);return n;}
  public static bool PointBuyOn=>LichMod.Settings?.adventurerPointBuy??true;
  public static int BuyBudget=>LichMod.Settings?.adventurerPointBuyPoints??BuyDefault;
  // 加入玩家阵营时打开分配（每人一次：确认后 buyBase 清空）。
  public static bool OpenPointBuy(Pawn p){
   var h=Get(p);if(h==null || h.buyBase==null || h.buyOpen || !PointBuyOn || p.Faction==null || !p.Faction.IsPlayer)return false;
   h.OpenBuy();return true;
  }

  // ---------- 经验 ----------
  public static float GainXP(Pawn p,float amount,bool scaled=true){
   if(!Enabled || amount<=0f)return 0f;var h=Get(p);if(h==null)return 0f;
   if(scaled)amount*=XpScale*Talents.XpFactor(h)*ClassSystem.XpFactor(p);h.AddXp(amount);return amount; // 1.51.0 凡人之志 +25%；1.73.0 背景 / 特性的职业经验加成
  }
  // 训练（冒险者手册等）：最多练到 3 级。
  public static float GainTrainingXP(Pawn p,float amount){
   if(!Enabled || amount<=0f)return 0f;var h=Get(p);if(h==null)return 0f;
   float room=XpTable[TrainingCapLevel-1]-h.xp;if(room<=0f)return 0f;
   amount=Mathf.Min(amount*XpScale*Talents.XpFactor(h),room);h.AddXp(amount);return amount;
  }
  public static float Milestone(Pawn p,string key,float xp){
   if(!Enabled)return 0f;var h=Get(p);if(h==null || h.milestonesDone.Contains(key))return 0f;
   h.milestonesDone.Add(key);float got=GainXP(p,xp);Epic.CheckLegend(h);
   if(Epic.DndMilestone(key))CombatGifts.Awaken(p,CombatGifts.MilestoneChance,"完成了一个 DND 里程碑"); // 1.66.0 战斗觉醒
   return got;
  }
  // 地图上的自由殖民者都算参与。
  public static int MilestoneMap(Map map,string key,float xp){
   if(map==null || !Enabled)return 0;int n=0;
   foreach(var p in map.mapPawns.FreeColonistsSpawned.ToList())if(Milestone(p,key,xp)>0f)n++;
   return n;
  }
  public static int MilestoneColony(string key,float xp){
   if(!Enabled)return 0;int n=0;
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())if(Milestone(p,key,xp)>0f)n++;
   return n;
  }

  // 击杀分配：记录殖民者对每个人造成的伤害，死亡时按比例分 combatPower。不存档（读档会丢掉正在进行的战斗的记录）。
  public static bool KillCounts(Pawn victim)=>Enabled && victim!=null && victim.Faction!=Faction.OfPlayer && !victim.IsPrisoner && victim.HostileTo(Faction.OfPlayer);
  public static void RecordDamage(Pawn victim,Pawn attacker,float amount){
   if(victim==null || attacker==null || attacker==victim || amount<=0f || attacker.Faction==null || !attacker.Faction.IsPlayer || attacker.RaceProps?.Humanlike!=true)return;
   var st=State;if(st==null)return;
   if(!st.damage.TryGetValue(victim,out var rec))st.damage[victim]=rec=new Dictionary<Pawn,float>();
   rec.TryGetValue(attacker,out float d);rec[attacker]=d+amount;st.damageTick[victim]=Find.TickManager.TicksGame;
   if(attacker.Map!=null && victim.Map==attacker.Map)st.NoteFighter(attacker,victim);
  }
  public static Dictionary<Pawn,float> DistributeKill(Pawn victim){
   var st=State;var gained=new Dictionary<Pawn,float>();
   if(st==null || victim==null || !st.damage.TryGetValue(victim,out var rec)){return gained;}
   st.damage.Remove(victim);st.damageTick.Remove(victim);
   float total=rec.Values.Sum(),cp=victim.kindDef?.combatPower??0f;
   if(total<=0f || cp<=0f)return gained;
   foreach(var kv in rec){float x=GainXP(kv.Key,cp*kv.Value/total);if(x>0f)gained[kv.Key]=x;}
   return gained;
  }
  public static void QuestDone(Quest q){
   if(!Enabled || q==null || q.points<=0f)return;
   var accepter=q.AccepterPawn;float full=q.points*QuestShare;
   if(accepter!=null)GainXP(accepter,full);
   foreach(var m in Find.Maps)if(m.IsPlayerHome)foreach(var p in m.mapPawns.FreeColonistsSpawned.ToList())if(p!=accepter)GainXP(p,full*QuestHomeShare);
  }

  // ---------- 效果 ----------
  public static int SkillAbility(SkillDef s){
   if(s==SkillDefOf.Melee)return Str;if(s==SkillDefOf.Shooting)return Dex;if(s==SkillDefOf.Intellectual)return Int;
   if(s==SkillDefOf.Medicine || s==SkillDefOf.Plants || s==SkillDefOf.Animals)return Wis;if(s==SkillDefOf.Social)return Cha;
   return -1;
  }
  public const float LearnMin=.5f;
  // 技能学习速度 = 对应属性的完整倍率（1.66.0 热修，16 → ×2.3，24 → ×7），最低 ×0.5。
  public static float LearnFactor(Pawn p,SkillDef s){
   int a=SkillAbility(s);if(a<0)return 1f;float scale=EffectScale;if(scale<=0f)return 1f;
   var h=Get(p);if(h==null || Legendary.Is(p))return 1f;
   return Mathf.Max(LearnMin,h.Fx(a,scale)); // 1.51.0 种族亲和
  }
  // 部位生命（1.65.0）：（1 + 等级成长）× 体质（1.81.0 热修3：1 + 每点调整值 15%，低于 10 的体质按 ×1，不会让部位生命变少）。
  public static float forcedScale=-1f;
  public static float PartHealthFactor(Pawn p){var h=Get(p);if(h==null || Legendary.Is(p))return 1f;return h.PartFactor(forcedScale>=0f?forcedScale:EffectScale)*Legend.HealthFactor(h)*Feats.ToughFactor(h);} // 1.50.0 传奇属性点“血量”相乘；1.67.0 坚韧
  // 职业强度：0.05 × 主属性有效调整值（乘属性效果强度；1.81.0 热修3 照 5e 的施法调整值，智力 20 = +0.25）+ 0.02 ×（熟练加值 − 2）。
  public const float ClassAbilityPer=.05f;
  public static float ClassPowerBonus(Pawn p,int ability){
   if(!Enabled || ability<0)return 0f;var h=Get(p);if(h==null)return 0f;
   return (Affinity.Talented(1f+ClassAbilityPer*h.AMod(ability),Affinity.TalentMult(h,ability))-1f)*EffectScale+ProfClassPer*Math.Max(0,h.Proficiency-2); // 1.81.0 热修5：天赋乘整个 (1 + 5% × 调整值)
  }
  // 1.52.0 热修：熟练加值每比 +2 高 1 点 —— 近战命中 +2%（加在最终命中率上，见 AdventurerMeleeHit）、射击精度 ×+1%（合进状态的属性系数）、VPE 职业强度 +0.02。
  // 只看等级，不乘“属性效果强度”，Isekai 共存时也生效；传奇怪物（Legendary）不算。
  public const float ProfHitPer=.02f,ProfAimPer=.01f,ProfClassPer=.02f;
  public static float ProfMeleeHit(int prof)=>ProfHitPer*Math.Max(0,prof-2);
  public static float ProfAim(int prof)=>ProfAimPer*Math.Max(0,prof-2);
  public static float MeleeHitBonus(Pawn p){if(!Enabled)return 0f;var h=Get(p);if(h==null || Legendary.Is(p))return 0f;return ProfMeleeHit(h.Proficiency);}
  // 1.81.0 热修（照 3.5 基础攻击加值）：等级每比 1 级高 1 级，近战命中 +1%、近战闪避 +1%（都加在最终几率上）；只算 1～20 级，不乘属性效果强度，Isekai 共存时也生效。
  public const float LevelMeleePer=.01f;
  public static float LevelMelee(int level)=>LevelMeleePer*(Mathf.Clamp(level,1,MaxLevel)-1);
  public static float LevelMelee(Pawn p){if(!Enabled || p==null)return 0f;var h=Get(p);if(h==null || Legendary.Is(p))return 0f;return LevelMelee(h.level);}
  // 1.81.0 热修3 等级差防御：攻击者每比目标低 1 级，命中 ×(1 − 2%)，最多 −50%（近战 AdventurerMeleeHit、远程 AdventurerShotLevelGap）。
  // 等级 = 冒险者等级（含传奇 21～40）；动物用野性等级；没有等级的（机械体、炮塔、传奇怪物）返回 0，不参与。
  public const float LevelGapPer=.02f,LevelGapMax=.5f;
  public static bool LevelGapOn=>Enabled && LichMod.Settings?.adventurerLevelGap!=false;
  public static float LevelGapFactor(int gap)=>gap<=0?1f:1f-Mathf.Min(LevelGapMax,LevelGapPer*gap);
  public static int CombatLevel(Pawn p){
   if(p==null)return 0;var h=Get(p);
   if(h!=null)return Legendary.Is(p)?0:h.level;
   if(p.RaceProps?.Animal!=true)return 0;var b=BeastLevels.Get(p);return b!=null && b.Applies?b.level:0;
  }
  // 1.81.0 热修4：伤口疼痛 ÷ 部位生命倍率（只算 ≥1 的部分），否则等级、体质给的部位生命只推迟断肢和死亡，不推迟因疼痛倒地。
  // 冒险者用 PartHealthFactor（传奇单位是 1），动物用野性等级的 PartHealthFactor。只改伤口和缺失部位的疼痛（AdventurerPainScale）。
  public static bool PainScaleOn=>LichMod.Settings?.adventurerPainScale!=false;
  public static float PainDivisor(Pawn p){
   if(p==null || !PainScaleOn)return 1f;
   if(Get(p)!=null)return Math.Max(1f,PartHealthFactor(p));
   if(p.RaceProps?.Animal==true)return Math.Max(1f,BeastLevels.PartHealthFactor(p));
   return 1f;
  }
  public static float LevelGapFactor(Pawn attacker,Thing target){
   if(!(target is Pawn t) || attacker==null || !LevelGapOn)return 1f;
   int lt=CombatLevel(t);if(lt<=1)return 1f; // 目标 1 级或没有等级：不可能更高
   int la=CombatLevel(attacker);return la<=0?1f:LevelGapFactor(lt-la);
  }
  static string Pct(float v)=>"+"+(v*100f).ToString("0.#")+"%";
  public static string ProfEffects(int prof)=>"近战命中 "+Pct(ProfMeleeHit(prof))+"、射击精度 ×"+(1f+ProfAim(prof)).ToString("0.00")+(VPELoaded?"、VPE 职业强度 +"+(ProfClassPer*Math.Max(0,prof-2)).ToString("0.00"):"");
  public static string ProficiencyText(Hediff_Adventurer h){
   int pr=h.Proficiency;var sb=new StringBuilder();
   sb.AppendLine("熟练加值 +"+pr+"（1～4 级 +2，之后每 4 级 +1，17 级 +6；传奇等级 28 级 +7、36 级 +8）");
   int next=0;for(int l=h.level+1;l<=Legend.MaxLevel;l++)if(Legendary.Proficiency(l)>pr){next=l;break;}
   if(next>0)sb.AppendLine("下一次提升："+next+" 级（+"+Legendary.Proficiency(next)+"）");
   sb.AppendLine();sb.AppendLine("每比 +2 高 1 点：");
   sb.AppendLine("· 近战命中 +2%（加在最终命中率上）：现在 "+Pct(ProfMeleeHit(pr)));
   sb.AppendLine("· 射击精度 ×+1%：现在 ×"+(1f+ProfAim(pr)).ToString("0.00"));
   if(VPELoaded)sb.AppendLine("· VPE 职业强度 +0.02：现在 +"+(ProfClassPer*Math.Max(0,pr-2)).ToString("0.00"));
   sb.AppendLine("等级（每级 +1%，到 20 级）：近战命中 "+Pct(LevelMelee(h.level))+"、近战闪避 "+Pct(LevelMelee(h.level))+"（加在最终几率上）");
   sb.AppendLine("始终生效：");
   sb.AppendLine("· 对抗传奇怪物（真龙、眼魔、恶魔等）效果的豁免：每点属性调整值和熟练加值各 +5% 豁免成功率");
   sb.AppendLine("· 与真龙交涉：每点魅力调整值和熟练加值各 +5% 成功率");
   sb.Append("只看等级，不乘“属性效果强度”，Isekai 共存模式下也生效。");
   return sb.ToString();
  }
  // 设置变化：部位生命上限变小时，按比例缩小伤口（同巨躯规则的做法），免得读档或调设置让受伤的部位直接归零。
  public static float appliedScale=-1f;
  public static void ApplySettings(){
   float now=EffectScale;
   if(Current.Game!=null && Current.ProgramState==ProgramState.Playing){
    if(appliedScale>=0f && now<appliedScale)RescaleWounds(PawnsFinder.AllMapsWorldAndTemporary_Alive,appliedScale,now);
    // 1.51.0：关掉“种族亲和”时擅长体质 / 擅长血量的部位生命变少，伤口按比例缩小。
    bool aff=Affinity.On;if(appliedAffinity && !aff)RescaleAffinity(PawnsFinder.AllMapsWorldAndTemporary_Alive);appliedAffinity=aff;
    Legend.ApplySettings(PawnsFinder.AllMapsWorldAndTemporary_Alive);
    foreach(var p in PawnsFinder.AllMapsWorldAndTemporary_Alive)Get(p)?.Dirty();
   }
   appliedScale=now;
  }
  public static int RescaleWounds(IEnumerable<Pawn> pawns,float from,float to){
   int n=0;
   foreach(var p in pawns.ToList()){
    var h=Get(p);if(h==null)continue;float a=h.PartFactor(from),b=h.PartFactor(to);if(b>=a)continue;
    foreach(var injury in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>x.Part!=null).ToList())injury.Severity*=b/a;
    n++;
   }
   return n;
  }
  // 1.81.0 热修3：旧公式（体质整条曲线、野兽每级 1% × 体质曲线）→ 新公式变小的人和野兽，伤口按比例缩小，免得超过新的部位生命。
  public static int RescaleHpFix3(IEnumerable<Pawn> pawns){
   float s=EffectScale;int n=0;
   foreach(var p in pawns.ToList()){
    if(p?.health?.hediffSet==null)continue;float a,b;var h=Get(p);
    if(h!=null){if(s<=0f || Legendary.Is(p))continue;a=(1f+h.LevelGrowth(h.level)*s)*Math.Max(1f,h.Fx(Con,s));b=h.PartFactor(s);}
    else{var bl=BeastLevels.Get(p);if(bl==null || !bl.Applies)continue;a=BeastLevels.LegacyPartFactor(bl.level,bl.elite);b=BeastLevels.PartFactor(bl.level,bl.elite);}
    if(b>=a-.0001f)continue;
    foreach(var injury in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>x.Part!=null).ToList())injury.Severity*=b/a;
    n++;
   }
   return n;
  }
  public static bool appliedAffinity=true;
  public static int RescaleAffinity(IEnumerable<Pawn> pawns){
   int n=0;
   foreach(var p in pawns.ToList()){
    if(Get(p)==null)continue;
    Affinity.forcedOn=true;float a=PartHealthFactor(p);Affinity.forcedOn=false;float b=PartHealthFactor(p);Affinity.forcedOn=null;
    if(b>=a)continue;Legend.ScaleWounds(p,b/a);n++;
   }
   return n;
  }

  // ---------- 旧存档 ----------
  // 旧小人：等级按人形击杀估（每 10 个 +1 级，最高 5 级），经验取该等级下限。
  public static int EstimateLevel(Pawn p){int kills=p?.records==null?0:p.records.GetAsInt(RecordDefOf.KillsHumanlikes);return Mathf.Clamp(1+kills/BackfillKillsPerLevel,1,BackfillMaxLevel);}
 }

 public class Hediff_Adventurer : HediffWithComps {
  public int level=1;public float xp;public int[] scores={10,10,10,10,10,10};public int pendingASI;
  public List<string> milestonesDone=new List<string>();public bool initialized;
  // VPE 桥：已经发出的额外路径点、还没有 VPE 灵能时攒着的路径点。
  public int vpeGranted,vpeBanked;
  // 1.48.1 专长（AdventurerFeats.cs）：已选的专长、今天用掉的次数（按专长 defName）、次数对应的本地日（午夜换日时清零）；剧情敌人等级不被袭击公式覆盖。
  public List<LP_FeatDef> feats=new List<LP_FeatDef>();public Dictionary<string,int> featUsed=new Dictionary<string,int>();public int featDay=-1;public bool storyLevel;
  public LP_FeatDef originFeat;public bool originFeatGiven; // 1.58.1 起源专长（免费，不占属性提升；老存档读档补发一次）
  // 1.58.3 持续成长：读过的属性典籍（每项 +2、上限 +2）、巨灵许愿的那一项（+1、上限 +1，-1 = 没许过）、实战磨砺的累计值 / 已得次数 / 上次采样的来源总量。
  public bool[] tomes=new bool[6];public int wishAbility=-1;public float[] grind=new float[6];public int[] grindGained=new int[6];public float[] grindLast;
  public int lastGreatWeaponTick=-99999,lastSentinelTick=-99999; // 冷却，不存档
  // 1.61.0 热修（日常工作经验，AdventurerWork.cs）：今天已计入的量、对应的本地日、不满 1 点的余数、累计折算值（都是冒险者经验倍率之前的基础值）。
  public float workXpToday,workXpRem,workXpTotal;public int workXpDay=-1;
  // 1.75.0 非战斗经验（AdventurerExtraXp.cs）：五项今天 / 累计的基础值、对应的本地日、被敌人打倒时记下的战斗力（-1 = 没有）、上次生还奖励的刻。
  public float[] exToday=new float[AdventurerExtraXp.Count],exTotal=new float[AdventurerExtraXp.Count];public int exDay=-1,exSurviveTick=-9999999;public float exDownPower=-1f;
  // 1.66.0 武艺天赋（CombatGifts.cs）：巨灵许愿额外升的阶数、再生之体上次长回肢体（或开始缺失）的刻；cgRage 是狂暴之血当前是否触发（不存档）。
  public int cgBonus,cgRegenTick=-1;public bool cgRage;
  // 1.49.0 史诗恩赐与传奇（AdventurerEpic.cs）：已选恩赐、待选恩赐点、19 级的点是否发过、威能之恩加在哪一项、命运之恩上次触发、传奇称号、
  // 已经按半神等级送过的属性提升（水位线，只升不降）、宿敌冠军标记。
  public List<LP_EpicBoonDef> boons=new List<LP_EpicBoonDef>();public int boonPoints,mightAbility=-1,fateTick=-9999999,demigodASI;public bool boon19,legendary,nemesis;
  int demigodLevel=-1; // 当前半神等级的缓存（0 = 不是半神），SyncDemigod 刷新
  // 1.50.0 传奇等级（AdventurerLegend.cs）：未投的传奇属性点、八项已投点数。
  public int legendPoints;public int[] legendSpent=new int[Legend.Count];
  // 等级上限：传奇且“传奇等级”开着时 40，否则 20（20 级的经验照常累积）。
  public int LevelCap=>legendary && Legend.On?Legend.MaxLevel:Adventurer.MaxLevel;
  // 1.53.0 战斗风格（AdventurerStyles.cs）：已选风格（按槽位）、改选后生效的刻、正在适应的槽位（-1 = 没有）、敌人是否掷过；styleMask 是生效风格的缓存（不存档）。
  public List<LP_FightingStyleDef> styles=new List<LP_FightingStyleDef>();public int styleReadyTick,styleAdaptSlot=-1;public bool styleRolled;
  public int styleMask;
  public ClassData classes=new ClassData(); // 1.72.0 职业等级、资源（Classes.cs）
  // 1.59.0 超凡属性特性（Exalted.cs）：掩码在 BuildStage 里算、不存档；已发过信的掩码、是否算过基准、硬撑（本轮开始的刻 / 次数 / 撑到哪一刻）、金刚不坏上次消伤疤的刻存档。
  public int exaltedMask,mythicMask,exaltedSent,mythicSent,holdUntil,holdStart=int.MinValue,holdUses,scarTick=int.MinValue;public bool exaltedInit;
  internal Verse.AI.MentalState halvedState;
  public int SpendLegend(int i,int n){n=Math.Min(n,legendPoints);if(n<=0 || i<0 || i>=Legend.Count)return 0;legendSpent[i]+=n;legendPoints-=n;Dirty();return n;}
  public override bool Visible=>false;
  public void Setup(int[] s,int startLevel){
   scores=(int[])s.Clone();initialized=true;
   level=Mathf.Clamp(startLevel,1,Adventurer.MaxLevel);xp=Adventurer.XpTable[level-1];
   for(int l=2;l<=level;l++)LevelPerks(l);
   Dirty();
  }
  // 每一级的固定收获：4/8/12/16 属性提升，19 级 1 个史诗恩赐点（每人只发一次）。
  // 1.50.0：21～40 级只给 2 个传奇属性点。
  // 1.51.0 天赋：大器晚成 传奇等级每级多 1 点、1～20 级每 4 级 1 点；百炼成钢 20 级也有属性提升。
  // 1.58.0：英雄之资 传奇等级每级再 +1 点；勤勉 6、10、14、18 级多一次属性提升。升级信件（LevelGains）用同一套算法。
  public int AsiAt(int l){
   if(l>Adventurer.MaxLevel)return 0;var t=Talent;int n=0;
   if(l%4==0 && (l<=16 || l==Adventurer.MaxLevel && t==Talents.Tempered))n++;
   if(HeroTraits.ExtraAsi(pawn,l))n++;
   return n;
  }
  public int LegendAt(int l){
   var t=Talent;
   if(l>Adventurer.MaxLevel)return Legend.PointsPerLevel+(t==Talents.LateBloomer?Talents.LateBloomerLegendBonus:0)+HeroTraits.LegendPerLevel(pawn);
   return t==Talents.LateBloomer && l%Talents.LateBloomerEvery==0?1:0;
  }
  void LevelPerks(int l){
   if(l>Adventurer.MaxLevel){legendPoints+=LegendAt(l);return;}
   pendingASI+=AsiAt(l);legendPoints+=LegendAt(l);
   if(l>=19 && !boon19){boon19=true;boonPoints++;}
  }
  // ---------- 1.51.0 种族亲和与天赋（缓存，Dirty 和每 250 刻刷新） ----------
  AdventurerAffinity affinity;TraitDef talent;bool talentKnown;
  public AdventurerAffinity Affinity=>affinity??(affinity=LichPhylactery.Affinity.For(pawn));
  public TraitDef Talent{get{if(!talentKnown){talent=Talents.Of(pawn);talentKnown=true;}return talent;}}
  public int ASIPoints=>Math.Max(Talent==Talents.Tempered?Talents.TemperedPoints:2,2+HeroTraits.AsiBonus(pawn)); // 1.58.0 英雄之资、反骨 +1 点
  // 有效调整值（种族亲和、天赋异禀；负调整值不乘）。
  public float AMod(int a)=>LichPhylactery.Affinity.Apply(Mod(a),LichPhylactery.Affinity.RaceMult(this,a)); // 1.81.0 热修5：只用种族亲和；天赋乘在整个线性效果上（TalentMult）
  // 有效倍率（1.66.0 热修）：属性倍率 × 种族亲和 / 天赋（擅长 ×1.5 时力量 24 = 7 × 1.5 = ×10.5）。属性低于 10（倍率 <1）时不乘；笨拙 ×0.5 不会把 ≥×1 的倍率压到 ×1 以下。
  public float AMult(int a){float m=Adventurer.Curve(Eff(a));if(m<1f)return m;float k=LichPhylactery.Affinity.ScoreMult(this,a);return k>=1f?m*k:Mathf.Max(1f,m*k);}
  // 某项效果的完整倍率，按“属性效果强度” s 在 ×1 和它之间插值。
  public float Fx(int a,float s)=>1f+(AMult(a)-1f)*s;
  void RefreshAffinity(){var a=LichPhylactery.Affinity.For(pawn);var t=Talents.Of(pawn);if(affinity==null || !talentKnown){affinity=a;talent=t;talentKnown=true;return;}if(a!=affinity || t!=talent || !talentKnown){Dirty();affinity=a;talent=t;talentKnown=true;}}
  public bool EpicBoonPending=>boonPoints>0;
  // ---------- 1.49.0 属性上限：20，威能之恩那一项 +4，每个半神等级 +2；超过上限的值保留，效果按上限算 ----------
  // 2026-10-29（Peter）：半神不再提高属性上限；改为传奇冒险者每 5 个传奇等级（25 / 30 / 35 / 40）所有属性上限 +2。
  public int LegendCapBonus=>legendary && level>Adventurer.MaxLevel?(level-Adventurer.MaxLevel)/5*Epic.LegendCapPer5:0;
  public int DemigodLevel{get{if(demigodLevel<0)demigodLevel=Demigods.Of(pawn)?.Level??0;return demigodLevel;}}
  public int Cap(int a)=>Adventurer.ScoreCap+(mightAbility==a && HasBoon(Epic.Might)?Epic.MightBonus:0)+LegendCapBonus+HeroTraits.CapBonus(this,a)+(a>=0 && a<6?(tomes[a]?Growth.TomeBonus:0)+(wishAbility==a?1:0):0); // 1.58.0 英雄之资 +2、极致 26、灵光 +2、弑神者 +1
  public int Eff(int a)=>Math.Min(scores[a],Cap(a))+CombatGifts.ScoreBonus(this,a)+Feats.ScoreBonus(this,a)+ClassMartial2.ScoreBonus(this,a); // 1.66.0 武艺天赋属性 +2/+3/+4、1.67.0 专长 +1 加在上限之后（上限同样提高）
  // 半神等级变化：刷新缓存；每个新的半神等级送一次属性提升（只送一次，失去神格不收回）。
  public int SyncDemigod(){
   int now=Demigods.Of(pawn)?.Level??0;int gave=0;
   if(now!=demigodLevel){demigodLevel=now;Dirty();}
   if(now>demigodASI){gave=now-demigodASI;demigodASI=now;pendingASI+=gave;if(Adventurer.AutoASI(pawn))AutoAssign();else if(pawn.Faction!=null && pawn.Faction.IsPlayer && !pawn.Dead && !Adventurer.TestSuppressed)Messages.Message(pawn.LabelShort+"的神性提升：获得 "+gave+" 次属性提升。",pawn,MessageTypeDefOf.PositiveEvent,false);Dirty();}
   return gave;
  }
  public bool HasBoon(LP_EpicBoonDef b)=>b!=null && boons.Contains(b);
  public string CanTakeBoon(LP_EpicBoonDef b){if(b==null)return "没有这个恩赐";if(HasBoon(b))return "已经有了";if(boonPoints<=0)return "没有恩赐点";return null;}
  // 选恩赐；威能之恩要指定属性（+4，上限 24）。
  public bool TakeBoon(LP_EpicBoonDef b,int ability=-1){
   if(CanTakeBoon(b)!=null || b==Epic.Might && (ability<0 || ability>5))return false;
   boonPoints--;AddBoon(b,ability);return true;
  }
  public void AddBoon(LP_EpicBoonDef b,int ability=-1){
   if(b==null || HasBoon(b))return;boons.Add(b);
   if(b==Epic.Might && ability>=0 && ability<6){mightAbility=ability;scores[ability]=Math.Max(scores[ability],Math.Min(scores[ability]+Epic.MightBonus,Cap(ability)));}
   Dirty();
  }
  // 敌人：随机一个（不朽除外），威能加在主属性。
  public void AutoBoon(){
   for(int guard=0;boonPoints>0 && guard<6;guard++){
    var pick=Epic.Boons.Where(b=>b.enemy && !HasBoon(b)).ToList();if(pick.Count==0){boonPoints=0;break;}
    var b=pick.RandomElement();int a=-1;
    if(b==Epic.Might){a=Adventurer.PrimaryAbility(pawn);if(a<0)a=Adventurer.KindExt(pawn)?.Primary??-1;if(a<0)a=Enumerable.Range(0,6).OrderByDescending(i=>scores[i]).First();}
    TakeBoon(b,a);
   }
  }
  bool Npc=>pawn.Faction==null || !pawn.Faction.IsPlayer;
  public int Mod(int a)=>Adventurer.Mod(Eff(a));
  public int Proficiency=>Legendary.Proficiency(level); // 1.52.0：传奇等级也按 28 / 36 级继续涨（与豁免检定一致）
  // 下一级的经验下限（20 级非传奇时指向 21 级：经验照常累积）。
  public float NextXp=>Legend.XpFor(Math.Min(level+1,Legend.MaxLevel));
  public bool CappedAt20=>level>=Adventurer.MaxLevel && LevelCap<=Adventurer.MaxLevel;

  public void AddXp(float amount){
   if(amount<=0f)return;xp+=amount;
   Settle();
  }
  // 按当前上限把攒下的经验结算成等级（成为传奇时也调一次）。
  public void Settle(){while(level<LevelCap && xp>=Legend.XpFor(level+1))LevelUp();}
  public void LevelUp(){
   level++;
   LevelPerks(level);
   ClassLegendary.SyncPoints(this); // 1.74.0 传奇职业点（25 / 30 / 35 / 40）
   if(level<=Adventurer.MaxLevel || level%2==0)Adventurer.LevelUpHook?.Invoke(pawn,level); // 1.50.0：传奇等级每 2 级 1 个 VPE 路径点
   if(pendingASI>0 && Adventurer.AutoASI(pawn))AutoAssign();
   if(boonPoints>0 && Npc)AutoBoon();
   if(legendPoints>0 && Legend.AutoFor(pawn))Legend.AutoSpend(this);
   Dirty();
   Epic.CheckLegend(this);
   if(pawn.Faction!=null && pawn.Faction.IsPlayer && !pawn.Dead)Adventurer.State?.QueueLetter(pawn,level);
  }
  // 1.48.1 敌人等级：直接升到目标等级（只升不降），经验取该级下限，沿途的属性提升自动分配（敌人 50% 换专长）；不发信件、不给 VPE 路径点。
  public void RaiseTo(int target){
   target=Mathf.Clamp(target,1,LevelCap);if(target<=level)return; // 1.50.0：传奇（敌人由 Legend.RaiseEnemy 标记）可到 40
   for(int l=level+1;l<=target;l++)LevelPerks(l);
   level=target;xp=Mathf.Max(xp,Legend.XpFor(level));
   if(pendingASI>0 && Adventurer.AutoASI(pawn))AutoAssign();
   if(boonPoints>0 && Npc)AutoBoon(); // 1.49.0：19 级以上的敌人随机带一个史诗恩赐（不朽除外）
   if(legendPoints>0 && Legend.AutoFor(pawn))Legend.AutoSpend(this);
   Dirty();
  }

  // ---------- 专长 ----------
  public bool HasFeat(LP_FeatDef f)=>f!=null && feats.Contains(f);
  public void RefreshDay(){int d=Feats.LocalDay(pawn);if(d!=featDay){featDay=d;featUsed.Clear();}}
  // 每日次数：没有次数限制的专长返回 -1。
  public int UsesPerDay(LP_FeatDef f)=>f==null?0:f.Worker.UsesPerDay(this); // 1.67.0 幸运 = 熟练加值
  public int UsesLeft(LP_FeatDef f){int per=UsesPerDay(f);if(per<=0)return -1;RefreshDay();featUsed.TryGetValue(f.defName,out int u);return Math.Max(0,per-u);}
  public bool TryUse(LP_FeatDef f){
   if(!HasFeat(f))return false;if(UsesPerDay(f)<=0)return true;
   if(UsesLeft(f)<=0)return false;featUsed.TryGetValue(f.defName,out int u);featUsed[f.defName]=u+1;return true;
  }
  // 能不能用一次属性提升换这个专长：null = 可以。
  // 2026-10-31 热修（照 3.5 战士奖励专长）：fighterFeats = 待选的免费专长（ClassSystem.SyncFighterFeats 发放），fighterFeatGranted = 已按战士等级发过的个数（水位线）。选专长先用免费的。
  public int fighterFeats,fighterFeatGranted;
  public int FeatPicks=>pendingASI+fighterFeats;
  public string CanTakeFeat(LP_FeatDef f){
   if(f==null)return "没有这个专长";if(HasFeat(f))return "已经有了";if(FeatPicks<=0)return "没有可用的属性提升或战士奖励专长";
   return f.Worker.Unmet(pawn,this);
  }
  public bool TakeFeat(LP_FeatDef f){if(CanTakeFeat(f)!=null)return false;if(fighterFeats>0)fighterFeats--;else pendingASI--;AddFeat(f);return true;}
  public Dictionary<string,int> featScore=new Dictionary<string,int>(); // 1.67.0 专长附带的 +1 加在哪项属性
  public void AddFeat(LP_FeatDef f){if(f==null || HasFeat(f))return;feats.Add(f);Feats.AssignScore(this,f);f.Worker.Gained(pawn,this);HeroTraits.Invalidate(pawn);Feats2.Sync(pawn);Dirty();} // 1.58.1：专长也进 1.58 的快速返回缓存
  public void RemoveFeat(LP_FeatDef f){if(!HasFeat(f))return;feats.Remove(f);featUsed.Remove(f.defName);featScore.Remove(f.defName);if(originFeat==f)originFeat=null;f.Worker.Lost(pawn,this);HeroTraits.Invalidate(pawn);Feats2.Sync(pawn);Dirty();}
  // 敌人（非玩家阵营）的属性提升：按兵种类别随机一个满足前置的专长。
  public bool TryEnemyFeat(bool bonus=false){
   var roles=Feats.EnemyRoles(pawn);
   var pick=DefDatabase<LP_FeatDef>.AllDefsListForReading.Where(f=>!HasFeat(f) && f.enemyTags!=null && f.enemyTags.Any(t=>t=="any" || roles.Contains(t)) && f.Worker.Unmet(pawn,this)==null).ToList();
   if(pick.Count==0)return false;if(bonus)fighterFeats--;else pendingASI--;AddFeat(pick.RandomElement());return true; // bonus：用战士奖励专长
  }

  // 属性提升：second<0 时 first +2（不超过该项上限，平时 20）；否则两项各 +1。
  public bool CanRaise(int a,int by)=>a>=0 && a<6 && by>0 && scores[a]+by<=Cap(a);
  // 1.52.0 热修：分页里就地加点。一次属性提升拆成 ASIPoints 个点，拆开后剩下的点记在 asiLeft（存档）。
  public int asiLeft;
  public int AvailablePoints=>pendingASI*ASIPoints+asiLeft;
  public bool SpendPoints(int a,int n){
   if(!CanRaise(a,n) || AvailablePoints<n)return false;
   while(asiLeft<n){pendingASI--;asiLeft+=ASIPoints;}
   asiLeft-=n;scores[a]+=n;Dirty();return true;
  }
  // 1.76.0 初始属性购点：buyBase = 六项基础值（null = 没有或已经确认），buyOpen = 正在分配，buyNoted = 已经发过提醒信。
  public int[] buyBase;public bool buyOpen,buyNoted;
  public int BuyLeft=>Adventurer.BuyBudget-Adventurer.BuyCost(buyBase);
  public void OpenBuy(){
   if(buyBase==null)return;buyOpen=true;
   if(BuyLeft<0){var want=(int[])buyBase.Clone();BuySet(new[]{8,8,8,8,8,8});BuySet(want);} // 购点设置比 27 少：先清空再按推荐值从高到低买到用完
  }
  // null = 可以。d = ±1。
  public string CanBuy(int a,int d){
   if(!buyOpen || buyBase==null || a<0 || a>5 || d==0)return "没有在分配初始属性";
   int v=buyBase[a]+d;
   if(v<Adventurer.BuyMin)return "基础值最低 "+Adventurer.BuyMin;
   if(v>Adventurer.BuyMax)return "基础值最高 "+Adventurer.BuyMax;
   if(d>0 && Adventurer.BuyCost(v)-Adventurer.BuyCost(buyBase[a])>BuyLeft)return "购点不够（还要 "+(Adventurer.BuyCost(v)-Adventurer.BuyCost(buyBase[a]))+"，剩 "+BuyLeft+"）";
   if(scores[a]+d<Adventurer.ScoreMin)return "属性不能低于 "+Adventurer.ScoreMin;
   if(d>0 && scores[a]+d>Cap(a))return "超过上限 "+Cap(a);
   return null;
  }
  public bool Buy(int a,int d){if(CanBuy(a,d)!=null)return false;buyBase[a]+=d;scores[a]+=d;Dirty();return true;}
  // 把基础值改成 target：先降，再按目标从高到低升到购点用完。
  public void BuySet(int[] target){
   if(buyBase==null || target==null || target.Length!=6)return;
   for(int a=0;a<6;a++)while(buyBase[a]>target[a] && Buy(a,-1)){}
   foreach(int a in Enumerable.Range(0,6).OrderByDescending(i=>target[i]).ThenBy(i=>i).ToList())while(buyBase[a]<target[a] && Buy(a,1)){}
  }
  public void BuyReset()=>BuySet(new[]{8,8,8,8,8,8});
  public void BuyRecommend()=>BuySet(Adventurer.BaseArray(pawn));
  public void ConfirmBuy(){if(!buyOpen)return;buyOpen=false;buyBase=null;Dirty();}
  // 1.51.0 百炼成钢：每次 3 点 —— second<0 时 first +3；否则 first +2、second +1。
  public bool ApplyASI(int first,int second=-1){
   if(pendingASI<=0)return false;
   int pts=ASIPoints;
   if(second<0){if(!CanRaise(first,pts))return false;scores[first]+=pts;}
   else{int a=pts-1;if(first==second || !CanRaise(first,a) || !CanRaise(second,1))return false;scores[first]+=a;scores[second]++;}
   pendingASI--;Dirty();return true;
  }
  // 自动加点：职业主属性优先，其次兵种模板的主属性，否则调整值最高的一项。敌人每次 50% 改选专长。
  public void AutoAssign(){
   bool npc=pawn.Faction==null || !pawn.Faction.IsPlayer;
   if(asiLeft>0){ // 拆开剩下的点：主属性优先，按顺序填到上限
    int pr=Adventurer.PrimaryAbility(pawn);if(pr<0)pr=Adventurer.KindExt(pawn)?.Primary??-1;
    foreach(int i in Enumerable.Range(0,6).OrderByDescending(i=>i==pr?100:scores[i]).ThenBy(i=>i).ToList()){int add=Math.Min(asiLeft,Cap(i)-scores[i]);if(add>0){scores[i]+=add;asiLeft-=add;}if(asiLeft<=0)break;}
    asiLeft=0;Dirty();
   }
   for(int guard=0;pendingASI>0 && guard<10;guard++){
    if(npc && Rand.Chance(Feats.EnemyFeatChance) && TryEnemyFeat())continue;
    int primary=Adventurer.PrimaryAbility(pawn);
    if(primary<0)primary=Adventurer.KindExt(pawn)?.Primary??-1;
    var order=Enumerable.Range(0,6).OrderByDescending(i=>i==primary?100:scores[i]).ThenBy(i=>i).Where(i=>scores[i]<Cap(i)).ToList();
    if(order.Count==0){pendingASI=0;break;}
    int a=order[0],pts=ASIPoints;
    if(CanRaise(a,pts))ApplyASI(a);
    else if(order.Count>1 && CanRaise(a,pts-1))ApplyASI(a,order[1]);
    else{int left=pts;foreach(int i in order){int add=Math.Min(left,Cap(i)-scores[i]);scores[i]+=add;left-=add;if(left<=0)break;}pendingASI--;Dirty();} // 剩下的点按顺序填到上限
   }
  }

  // ---------- 效果缓存 ----------
  HediffStage stage;float stageScale=-1f;
  public void Dirty(){stage=null;affinity=null;talentKnown=false;if(pawn?.health?.hediffSet!=null && pawn.health.hediffSet.hediffs.Contains(this))pawn.health.Notify_HediffChanged(this);}
  public override HediffStage CurStage{get{float s=Adventurer.EffectScale;if(stage==null || stageScale!=s){stage=BuildStage(s);stageScale=s;}return stage;}}
  static StatDef S(string n)=>DefDatabase<StatDef>.GetNamedSilentFail(n);
  static void Add(List<StatModifier> list,string stat,float v,float neutral){var d=S(stat);if(d!=null && Math.Abs(v-neutral)>.0001f)list.Add(new StatModifier{stat=d,value=v});}
  // 1.66.0 热修：六维效果表（对象与 Isekai 六维一致）。每项都乘整个属性倍率 M（AMult）；
  // Factor：×M，cap>0 时超过 cap 的部分按 Isekai 的 SoftCap 对数收敛（主效果不封顶）；Inverse：÷M，不低于 cap（Isekai 瞄准时间最低 ×0.05）；
  // Offset：per ×（M − 1），限在 lo～hi（照 Isekai 对应属性的上下限）。都按“属性效果强度”在 ×1 / +0 和完整效果之间插值。
  // 1.81.0 热修3：Linear：1 + per × 有效调整值（AMod），不低于 ×0.5（近战伤害、心灵敏感度；热修6 起敏捷移速）。
  // 1.81.0 热修（Peter：研究速度照 Isekai 算）：PerPoint：1 + (有效属性 − 10) × per（Isekai 是 1 + (INT − 5) × 6%，基准换成 10），
  // 正的部分乘种族亲和，整个乘天赋，最低 ×0.5，超过 cap 按 SoftCap 收敛；和 Isekai 一样另外照乘全局工作速度。
  public enum FxKind{Factor,Inverse,Offset,Linear,PerPoint}
  public const float IsekaiResearchPer=.06f;
  public class ScoreFx{public int a;public string stat,label;public FxKind kind;public float cap,per,lo,hi;public bool num;}
  static ScoreFx Fa(int a,string stat,string label,float cap=0f)=>new ScoreFx{a=a,stat=stat,label=label,kind=FxKind.Factor,cap=cap};
  static ScoreFx Li(int a,string stat,string label,float per)=>new ScoreFx{a=a,stat=stat,label=label,kind=FxKind.Linear,per=per};
  static ScoreFx Of(int a,string stat,string label,float per,float lo,float hi,bool num=false)=>new ScoreFx{a=a,stat=stat,label=label,kind=FxKind.Offset,per=per,lo=lo,hi=hi,num=num};
  public static readonly ScoreFx[] Table={
   Li(0,"MeleeDamageFactor","近战伤害",.1f),Fa(0,"CarryingCapacity","负重"),Fa(0,"VEF_MassCarryCapacity","商队负重"),Fa(0,"MiningSpeed","采矿速度"),
   Fa(1,"ShootingAccuracyPawn","射击精度"),Fa(1,"MeleeHitChance","近战命中"),new ScoreFx{a=1,stat="AimingDelayFactor",label="瞄准时间",kind=FxKind.Inverse,cap=.05f},
   Of(1,"MeleeDodgeChance","近战闪避",3f,-3f,50f,true),Li(1,"MoveSpeed","移速",.1f),Fa(1,"CaravanBonusSpeedFactor","商队速度",3f),
   Fa(2,"ImmunityGainSpeed","免疫速度",5f),Fa(2,"InjuryHealingFactor","伤口愈合",10f),Fa(2,"RestRateMultiplier","休息恢复",3f),
   Of(2,"PainShockThreshold","疼痛休克阈值",.05f,-.2f,.5f),Of(2,"ToxicResistance","毒素抗性",.05f,-.1f,.95f),
   Of(2,"ArmorRating_Sharp","利器护甲",.03f,0f,.8f),Of(2,"ArmorRating_Blunt","钝器护甲",.03f,0f,.95f),Of(2,"ArmorRating_Heat","高温护甲",.03f,0f,.7f),
   new ScoreFx{a=3,stat="ResearchSpeed",label="研究速度",kind=FxKind.PerPoint,per=IsekaiResearchPer,cap=5f},Fa(3,"GlobalLearningFactor","全局学习",100f),Fa(3,"WorkSpeedGlobal","工作速度",5f),Fa(3,"HackingSpeed","黑客速度",5f),
   Fa(4,"MedicalTendQuality","医疗品质"),Li(4,"PsychicSensitivity","心灵敏感度",.1f),Fa(4,"MedicalSurgerySuccessChance","手术成功率",4f),Fa(4,"MeditationFocusGain","冥想专注",2.5f),
   Fa(4,"TrainAnimalChance","训练动物",3f),Fa(4,"AnimalGatherYield","动物采集",3f),Fa(4,"PsychicEntropyRecoveryRate","心灵热量恢复",5f),
   Of(4,"PsychicEntropyMax","心灵热量上限",10f,-10f,150f,true),Of(4,"MentalBreakThreshold","精神崩溃阈值",-.03f,-.34f,.1f),
   Fa(5,"SocialImpact","社交影响"),Fa(5,"NegotiationAbility","谈判",4f),Fa(5,"TameAnimalChance","驯服动物",3f),Fa(5,"BondAnimalChanceFactor","结缘几率",3f),
   Of(5,"ArrestSuccessChance","逮捕成功率",.05f,-.1f,.5f),Of(5,"TradePriceImprovement","交易价格",.03f,-.05f,.3f)};
  // Isekai 的 SoftCap（只取上半段）：超过 cap 后按 cap + k·ln(1 + 超出/k) 收敛，k = max(0.5, cap/2)。
  public static float SoftCap(float v,float cap){if(cap<=0f || v<=cap)return v;float k=Mathf.Max(.5f,cap*.5f);return cap+k*Mathf.Log(1f+(v-cap)/k);}
  public float FxValue(ScoreFx e,float s){
   float m=AMult(e.a);
   switch(e.kind){
    case FxKind.Inverse:return 1f+(Mathf.Max(e.cap,1f/Mathf.Max(.0001f,m))-1f)*s;
    case FxKind.Offset:return Mathf.Clamp(e.per*(m-1f),e.lo,e.hi)*s;
    case FxKind.Linear:return 1f+(LinearValue(e.a,e.per)-1f)*s; // 1.81.0：按调整值线性，种族亲和只放大正的调整值；热修5 天赋乘整个效果
    case FxKind.PerPoint:return 1f+(PerPointValue(e)-1f)*s;
    default:{float v=1f+(SoftCap(m,e.cap)-1f)*s;return e.stat=="CarryingCapacity"?Mathf.Max(.1f,v):v;}
   }
  }
  public HediffStage BuildStage(float s){
   var st=new HediffStage{becomeVisible=false};
   var f=new List<StatModifier>();var o=new List<StatModifier>();
   int ex=Exalted.Compute(this);exaltedMask=ex&63;mythicMask=ex>>6&63;Exalted.Store(pawn,ex); // 1.59.0 超凡掩码
   Exalted.AddStats(this,st,f,o);
   // 专长的数值不乘属性效果强度（Isekai 共存时照样生效），只看总开关。
   if(Adventurer.Enabled)foreach(var feat in feats){if(feat.statOffsets!=null)o.AddRange(feat.statOffsets);if(feat.statFactors!=null)f.AddRange(feat.statFactors);}
   // 1.49.0 史诗恩赐与传奇称号：同专长，不乘属性效果强度。
   if(Adventurer.Enabled){
    foreach(var b in boons){if(b.statOffsets!=null)o.AddRange(b.statOffsets);if(b.statFactors!=null)f.AddRange(b.statFactors);}
    if(HasBoon(Epic.Immortal))st.makeImmuneTo=Demigods.Diseases;
    if(legendary)Add(f,"SocialImpact",Epic.LegendSocial,1f);
    Legend.AddStats(this,f,o); // 1.50.0 传奇属性点
    Add(f,"ShootingAccuracyPawn",1f+Adventurer.ProfAim(Proficiency),1f); // 1.52.0 熟练加值（近战命中在 AdventurerMeleeHit）
    Add(f,"MeleeDamageFactor",1f+ClassSystem.MartialDamage(this),1f); // 1.81.0 热修3：物理职业每级伤害成长（远程在 ClassDamageMods）
   }
   CombatGifts.AddStats(this,st,f,o); // 1.66.0 武艺天赋的附带效果（不乘属性效果强度）
   if(s<=0f || Legendary.Is(pawn)){Finish(st,f,o);return st;}
   foreach(var e in Table)if(e.kind==FxKind.Offset)Add(o,e.stat,FxValue(e,s),0f);else Add(f,e.stat,FxValue(e,s)/SameScoreWork(e,s),1f);
   Finish(st,f,o);
   return st;
  }
  // 1.81.0 热修（研究速度 31803%）：原版黑客速度本身就乘全局工作速度，而全局工作速度已经吃了智力倍率；
  // 这里除掉那一份，让智力只算一次（总效果仍是表里的 ×M）。研究速度改成 PerPoint（照 Isekai，和工作速度照常相乘），不走这里。
  static ScoreFx workFx;
  float SameScoreWork(ScoreFx e,float s){
   if(workFx==null)workFx=Table.First(x=>x.stat=="WorkSpeedGlobal");
   if(e==workFx || e.a!=workFx.a || e.kind!=FxKind.Factor || S(e.stat)?.statFactors?.Contains(StatDefOf.WorkSpeedGlobal)!=true)return 1f;
   return Mathf.Max(.0001f,FxValue(workFx,s)); // 低智力的减速同样只算一次
  }
  // 同一个 stat 在列表里只认第一条（原版 GetStatOffsetFromList），所以合并：偏移相加、系数相乘（例如灵活 + 敏捷的移速、心灵之恩 + 感知的心灵敏感度）。
  static void Finish(HediffStage st,List<StatModifier> f,List<StatModifier> o){
   if(f.Count>0)st.statFactors=f.GroupBy(m=>m.stat).Select(g=>new StatModifier{stat=g.Key,value=g.Aggregate(1f,(x,m)=>x*m.value)}).ToList();
   if(o.Count>0)st.statOffsets=o.GroupBy(m=>m.stat).Select(g=>new StatModifier{stat=g.Key,value=g.Sum(m=>m.value)}).ToList();
  }
  public const float PartPerLevel=.05f; // 1.81.0 热修（照 3.5 的血量随等级涨）：每级 +1% 改为 +5%，20 级 ×1.95，再乘体质倍率；热修2 起这是未分配职业点的等级
  // 1.81.0 热修2（Peter）：等级成长按职业生命骰（LP_ClassDef.PartPerLevel）。平均每级成长 × (等级−1)，第 1 级不算。
  public float LevelGrowth(int lv){
   lv=Math.Min(lv,Adventurer.MaxLevel);if(lv<=1)return 0f;float sum=0f;int n=0;
   if(classes?.levels!=null)foreach(var kv in classes.levels){if(kv.Key==null)continue;int c=Math.Min(kv.Value,lv-n);if(c<=0)continue;sum+=c*kv.Key.PartPerLevel;n+=c;}
   sum+=(lv-n)*PartPerLevel;return sum/lv*(lv-1);
  }
  // 1.81.0 热修3：体质对部位生命改成线性，每点有效调整值 +15%（14 = ×1.3，20 = ×1.75），低于 10 不减少。
  public const float ConPartPerMod=.15f;
  // 1.81.0 热修5：天赋（天赋异禀 / 天赋·某项 / 极致）乘整个体质系数，再按属性效果强度插值，最低 ×1。
  public float ConPart(float s)=>Math.Max(1f,1f+(LichPhylactery.Affinity.Talented(1f+ConPartPerMod*AMod(Adventurer.Con),LichPhylactery.Affinity.TalentMult(this,Adventurer.Con))-1f)*s);
  // 线性效果完整值（未插值）：max(0.5, 1 + per × AMod) × 天赋倍率（低于 ×1 时不乘）。
  public float LinearValue(int a,float per)=>LichPhylactery.Affinity.Talented(Mathf.Max(Adventurer.MeleeMin,1f+per*AMod(a)),LichPhylactery.Affinity.TalentMult(this,a));
  public float PerPointValue(ScoreFx e){
   int d=Eff(e.a)-10;float pts=d>0?d*LichPhylactery.Affinity.RaceMult(this,e.a):d;
   float v=LichPhylactery.Affinity.Talented(Mathf.Max(.5f,1f+e.per*pts),LichPhylactery.Affinity.TalentMult(this,e.a));
   return SoftCap(v,e.cap);
  }
  public float PartFactor(float s){
   if(s<=0f)return 1f;
   float lv=1f+LevelGrowth(level)*s; // 1.50.0：传奇等级不再加等级成长（血量走传奇属性点）
   return lv*ConPart(s);
  }
  // 悬停说明：某项属性当前给的效果。
  public string EffectText(int a){
   float s=Adventurer.EffectScale;int m=Mod(a);var sb=new StringBuilder();
   sb.AppendLine(Adventurer.Names[a]+" "+scores[a]+Feats.ScoreTag(this,a)+"（调整值 "+Adventurer.Signed(m)+"）");
   if(Feats.ScoreBonus(this,a)>0)sb.AppendLine("专长 +"+Feats.ScoreBonus(this,a)+"："+Feats.ScoreFrom(this,a)+"（加在上限之后）");
   sb.AppendLine("上限 "+Cap(a)+(Cap(a)!=Adventurer.ScoreCap?"（"+(mightAbility==a && HasBoon(Epic.Might)?"威能之恩 +4":"")+(LegendCapBonus>0?" 传奇 Lv"+level+" +"+LegendCapBonus:"")+"）":"")+(scores[a]>Cap(a)?"，超出部分保留但效果按 "+Cap(a)+" 算":""));
   if(Adventurer.EffectsHandedToIsekai){sb.AppendLine("属性效果已交给 Isekai（设置里可以打开）。");return sb.ToString().TrimEnd();}
   if(s<=0f){sb.AppendLine("属性效果已在设置里关闭。");return sb.ToString().TrimEnd();}
   if(Legendary.Is(pawn)){sb.AppendLine("传奇单位：六维不提供数值效果（强度已经算在兵种里）。");return sb.ToString().TrimEnd();}
   Func<float,string> x=v=>"×"+v.ToString("0.##");
   Func<float,string> pct=v=>(v>=0?"+":"")+(v*100f).ToString("0.#")+"%";
   sb.AppendLine("属性倍率 "+x(Adventurer.Curve(Eff(a)))+(Math.Abs(AMult(a)-Adventurer.Curve(Eff(a)))>.001f?"（种族亲和后 "+x(AMult(a))+"）":"")+"：10 = ×1，每 +5 约翻倍，每 +10 ×4"+(a==Adventurer.Str?"（近战伤害不用这张表：每点调整值 +10%）":a==Adventurer.Wis?"（心灵敏感度不用这张表：每点调整值 +10%）":a==Adventurer.Con?"（部位生命不用这张表：每点调整值 +15%）":a==Adventurer.Int?"（研究速度不用这张表：照 Isekai 每点 +6%，×5 后收敛，另乘全局工作速度）":""));
   float tm=LichPhylactery.Affinity.TalentMult(this,a); // 1.81.0 热修5
   if(tm>1.0001f)sb.AppendLine("天赋：线性效果（"+(a==Adventurer.Str?"近战伤害、":a==Adventurer.Wis?"心灵敏感度、":a==Adventurer.Con?"部位生命的体质系数、":"")+"以这项为主属性时职业强度的属性部分）整个 ×"+tm.ToString("0.##")+"，和种族亲和相乘（种族亲和只放大正的调整值）");
   var parts=new List<string>();
   if(a==Adventurer.Con)parts.Add("部位生命 "+x(PartFactor(s))+"（体质 "+x(ConPart(s))+" × 等级成长，体质低于 10 不减少）");
   foreach(var e in Table)if(e.a==a && S(e.stat)!=null){float v=FxValue(e,s);parts.Add(e.label+" "+(e.kind!=FxKind.Offset?x(v):e.num?(v>=0?"+":"")+v.ToString("0.#"):pct(v)));}
   sb.AppendLine(string.Join("，",parts));
   string skills=a==0?"格斗":a==1?"射击":a==3?"智识":a==4?"医疗、种植、驯兽":a==5?"社交":null;
   if(skills!=null)sb.AppendLine(skills+"学习速度 "+x(Mathf.Max(Adventurer.LearnMin,Fx(a,s))));
   sb.Append("学习速度最低 ×"+Adventurer.LearnMin.ToString("0.#")+"；带上限的项超出部分按 Isekai 的方式逐渐收敛。");
   var tip=LichPhylactery.Affinity.Tip(this,a);if(tip!="")sb.AppendLine().Append(tip); // 1.51.0
   return sb.ToString();
  }
  // 1.81.0 热修5：冒险者分页里每项属性展开后的效果表（效果名, 当前实际值），已含种族亲和、天赋、属性效果强度。
  // Isekai 共存 / 效果强度 0 / 传奇单位时返回 null（分页显示原来的说明）。
  public List<KeyValuePair<string,string>> EffectRows(int a){
   float s=Adventurer.EffectScale;if(Adventurer.EffectsHandedToIsekai || s<=0f || Legendary.Is(pawn) || a<0 || a>5)return null;
   var rows=new List<KeyValuePair<string,string>>();
   void Row(string k,string v)=>rows.Add(new KeyValuePair<string,string>(k,v));
   string X(float v)=>"×"+v.ToString("0.00");
   string Pct(float v)=>(v>=0?"+":"")+(v*100f).ToString("0.#")+"%";
   if(a==Adventurer.Con){
    Row("部位生命（体质）",X(ConPart(s)));Row("等级成长",X(1f+LevelGrowth(level)*s));Row("部位生命合计",X(Adventurer.PartHealthFactor(pawn)));
    if(LichMod.Settings?.adventurerPainScale??true)Row("伤口疼痛","÷"+Adventurer.PainDivisor(pawn).ToString("0.00"));
   }
   foreach(var e in Table)if(e.a==a && S(e.stat)!=null){float v=FxValue(e,s);Row(e.label,e.kind!=FxKind.Offset?X(v):e.num?(v>=0?"+":"")+v.ToString("0.#"):Pct(v));}
   string skills=a==0?"格斗":a==1?"射击":a==3?"智识":a==4?"医疗 / 种植 / 驯兽":a==5?"社交":null;
   if(skills!=null)Row(skills+"学习",X(Mathf.Max(Adventurer.LearnMin,Fx(a,s))));
   Row("职业强度（主属性）",Pct(Adventurer.ClassPowerBonus(pawn,a)-Adventurer.ProfClassPer*Math.Max(0,Proficiency-2)));
   return rows;
  }
  // 纯文本版（自检、复制用）：每行“效果名 值”。
  public string EffectSheet(int a){var r=EffectRows(a);return r==null?"":string.Join("\n",r.Select(kv=>kv.Key+" "+kv.Value));}

  // 死灵造物、召唤物身上不留（例如死后被做成骷髅）。每 250 刻查一次。
  public override bool ShouldRemove=>base.ShouldRemove || pawn!=null && pawn.IsHashIntervalTick(250) && Adventurer.Excluded(pawn);
  // 1.58.6：Adventurer.Get 的登记。移除时同时清掉强力特性 / 专长缓存（HeroInfo 里缓存了专长标志，原来变成死灵造物后专长效果会残留到下一次特性或专长变化）。
  internal bool live;
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);Adventurer.Register(this);}
  public override void PostRemoved(){base.PostRemoved();live=false;if(pawn!=null){HeroTraits.Invalidate(pawn);Feats2.Sync(pawn);Exalted.Store(pawn,0);Exalted.chaCarriers.Remove(pawn);}}
  // 1.49.0：每 250 刻同步半神等级（上限、送属性提升）和传奇称号。
  int syncAcc;
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(holdUntil>0 && GenTicks.TicksGame>=holdUntil)Exalted.HoldExpired(this); // 1.59.0 硬撑到期：重新判定倒地
   if((syncAcc+=delta)<250)return;syncAcc=0;
   Styles.Refresh(this); // 1.53.0 战斗风格的掩码与条件状态（没有风格的人直接返回）
   ClassSystem.Tick250(this); // 1.72.0 职业资源、物理职业缓存、武僧架势、圣武士灵光（没有职业的人直接返回）
   if(pawn!=null)Exalted.Tick(this); // 1.59.0 超凡：补算掩码、发信、光环 / 显形 / 封顶（掩码为 0 时只比较一次）
   if(pawn!=null && !pawn.Dead)CombatGifts.Tick250(this); // 1.66.0 狂暴之血阈值、再生之体长回肢体（没有武艺天赋的人一次缓存查找）
   if(!Adventurer.Enabled || pawn==null || pawn.Dead)return;
   if(buyBase!=null){if(!buyOpen)Adventurer.OpenPointBuy(pawn);if(buyOpen && !buyNoted && pawn.IsColonist && pawn.Spawned && !Adventurer.TestSuppressed){buyNoted=true;Adventurer.State?.QueueBuyNote(pawn);}} // 1.76.0 招募、加入后打开初始属性分配并提醒
   SyncDemigod();Epic.CheckLegend(this);RefreshAffinity(); // 1.51.0：变成巫妖、半夺心魔、换异种、得到天赋
   Legendary.SyncAdventurer(this); // 2026-10-28：传奇冒险者每 5 级 1 次传奇抗性
   if(level<LevelCap && xp>=NextXp)Settle(); // 1.50.0：旧存档里的 20 级传奇、重新打开“传奇等级”后结算攒下的经验
  }
  // 旧存档：已经 19 级但没发过恩赐点的补 1 点（1.48.x 存的 lpEpicBoon 也算在里面）。
  public void LoadFixup(){
   if(boons==null)boons=new List<LP_EpicBoonDef>();boons.RemoveAll(x=>x==null);
   if(level>=19 && !boon19){boon19=true;if(boons.Count==0)boonPoints=Math.Max(boonPoints,1);}
   if(mightAbility>5)mightAbility=-1;demigodLevel=-1;
   if(legendSpent==null || legendSpent.Length!=Legend.Count){var old=legendSpent;legendSpent=new int[Legend.Count];if(old!=null)for(int i=0;i<Math.Min(old.Length,Legend.Count);i++)legendSpent[i]=Math.Max(0,old[i]);}
   if(legendPoints<0)legendPoints=0;
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref level,"lpLevel",1);Scribe_Values.Look(ref xp,"lpXp");Scribe_Values.Look(ref pendingASI,"lpPendingASI");
   Scribe_Values.Look(ref initialized,"lpInitialized"); // 1.48.x 的 lpEpicBoon 不再读：19 级的恩赐点由 LoadFixup 按等级补
   Scribe_Collections.Look(ref boons,"lpBoons",LookMode.Def);Scribe_Values.Look(ref boonPoints,"lpBoonPoints");Scribe_Values.Look(ref boon19,"lpBoon19");
   Scribe_Values.Look(ref mightAbility,"lpMightAbility",-1);Scribe_Values.Look(ref fateTick,"lpFateTick",-9999999);Scribe_Values.Look(ref demigodASI,"lpDemigodASI");
   Scribe_Values.Look(ref legendary,"lpLegendary");Scribe_Values.Look(ref nemesis,"lpNemesis");
   Scribe_Values.Look(ref asiLeft,"lpAsiLeft");
   Scribe_Values.Look(ref fighterFeats,"lpFighterFeats");Scribe_Values.Look(ref fighterFeatGranted,"lpFighterFeatGranted"); // 2026-10-31 战士奖励专长
   Scribe_Values.Look(ref vpeGranted,"lpVpeGranted");Scribe_Values.Look(ref vpeBanked,"lpVpeBanked");
   var list=scores?.ToList();Scribe_Collections.Look(ref list,"lpScores",LookMode.Value);
   var bb=buyBase?.ToList();Scribe_Collections.Look(ref bb,"lpBuyBase",LookMode.Value);Scribe_Values.Look(ref buyOpen,"lpBuyOpen");Scribe_Values.Look(ref buyNoted,"lpBuyNoted"); // 1.76.0
   if(Scribe.mode==LoadSaveMode.LoadingVars){buyBase=bb!=null && bb.Count==6?bb.ToArray():null;if(buyBase==null)buyOpen=false;}
   Scribe_Collections.Look(ref milestonesDone,"lpMilestones",LookMode.Value);
   Scribe_Collections.Look(ref feats,"lpFeats",LookMode.Def);Scribe_Collections.Look(ref featUsed,"lpFeatUsed",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref featScore,"lpFeatScore",LookMode.Value,LookMode.Value); // 1.67.0
   Scribe_Values.Look(ref featDay,"lpFeatDay",-1);Scribe_Values.Look(ref storyLevel,"lpStoryLevel");
   Scribe_Defs.Look(ref originFeat,"lpOriginFeat");Scribe_Values.Look(ref originFeatGiven,"lpOriginFeatGiven");
   var tl=tomes?.ToList();Scribe_Collections.Look(ref tl,"lpTomes",LookMode.Value);Scribe_Values.Look(ref wishAbility,"lpWishAbility",-1);
   var gl=grind?.ToList();Scribe_Collections.Look(ref gl,"lpGrind",LookMode.Value);var gg=grindGained?.ToList();Scribe_Collections.Look(ref gg,"lpGrindGained",LookMode.Value);var glast=grindLast?.ToList();Scribe_Collections.Look(ref glast,"lpGrindLast",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.LoadingVars){tomes=tl!=null && tl.Count==6?tl.ToArray():new bool[6];grind=gl!=null && gl.Count==6?gl.ToArray():new float[6];grindGained=gg!=null && gg.Count==6?gg.ToArray():new int[6];grindLast=glast!=null && glast.Count==6?glast.ToArray():null;}
   Scribe_Values.Look(ref legendPoints,"lpLegendPoints");var spent=legendSpent?.ToList();Scribe_Collections.Look(ref spent,"lpLegendSpent",LookMode.Value);
   Scribe_Values.Look(ref exaltedSent,"lpExaltedSent");Scribe_Values.Look(ref mythicSent,"lpMythicSent");Scribe_Values.Look(ref exaltedInit,"lpExaltedInit");
   Scribe_Values.Look(ref holdUntil,"lpHoldUntil");Scribe_Values.Look(ref holdStart,"lpHoldStart",int.MinValue);Scribe_Values.Look(ref holdUses,"lpHoldUses");Scribe_Values.Look(ref scarTick,"lpScarTick",int.MinValue);
   Scribe_Collections.Look(ref styles,"lpStyles",LookMode.Def);Scribe_Values.Look(ref styleReadyTick,"lpStyleReady");Scribe_Values.Look(ref styleAdaptSlot,"lpStyleAdapt",-1);Scribe_Values.Look(ref styleRolled,"lpStyleRolled");
   Scribe_Values.Look(ref workXpToday,"lpWorkXpToday");Scribe_Values.Look(ref workXpDay,"lpWorkXpDay",-1);Scribe_Values.Look(ref workXpRem,"lpWorkXpRem");Scribe_Values.Look(ref workXpTotal,"lpWorkXpTotal");
    AdventurerExtraXp.Expose(this); // 1.75.0 非战斗经验
   Scribe_Values.Look(ref cgBonus,"lpCgBonus");Scribe_Values.Look(ref cgRegenTick,"lpCgRegen",-1); // 1.66.0 武艺天赋
   Scribe_Deep.Look(ref classes,"lpClasses"); // 1.72.0 职业
   if(Scribe.mode==LoadSaveMode.LoadingVars || Scribe.mode==LoadSaveMode.PostLoadInit){
    if(classes==null)classes=new ClassData();
    if(styles==null)styles=new List<LP_FightingStyleDef>();styles.RemoveAll(x=>x==null);styleMask=0;
    if(spent!=null)legendSpent=spent.ToArray();
    if(list!=null && list.Count==6)scores=list.ToArray();
    if(milestonesDone==null)milestonesDone=new List<string>();
    if(feats==null)feats=new List<LP_FeatDef>();feats.RemoveAll(x=>x==null);if(featUsed==null)featUsed=new Dictionary<string,int>();
    Feats.FixScores(this); // 1.67.0 旧存档的专长补选 +1 属性
    level=Mathf.Clamp(level,1,Legend.MaxLevel);stage=null;
    LoadFixup();
    if(Scribe.mode==LoadSaveMode.PostLoadInit)Adventurer.Register(this);
   }
  }
 }

 // ---------- 存档数据：旧存档补生成、袭击记录、升级信件 ----------
 public class RaidRecord : IExposable {
  public Map map;public float points;public int start;public List<Pawn> raiders=new List<Pawn>(),fighters=new List<Pawn>();
  public Pawn lastStand; // 1.58.0 绝境英雄：袭击中某一刻唯一还能战斗的殖民者（HeroDeeds.SampleRaids）
  public bool Over=>map==null || !Find.Maps.Contains(map) || raiders.All(r=>r==null || r.Dead || r.Downed || !r.Spawned || r.Map!=map || !r.HostileTo(Faction.OfPlayer));
  public void ExposeData(){Scribe_References.Look(ref map,"map");Scribe_Values.Look(ref points,"points");Scribe_Values.Look(ref start,"start");ScribePawns.Look(ref raiders,"raiders");ScribePawns.Look(ref fighters,"fighters");Scribe_References.Look(ref lastStand,"lpLastStand");}
 }
 public class GameComponent_Adventurer : GameComponent {
  public bool backfilled;
  public List<RaidRecord> raids=new List<RaidRecord>();
  public readonly Dictionary<Pawn,Dictionary<Pawn,float>> damage=new Dictionary<Pawn,Dictionary<Pawn,float>>();
  public readonly Dictionary<Pawn,int> damageTick=new Dictionary<Pawn,int>();
  readonly List<Pawn> letterPawns=new List<Pawn>();readonly List<int> letterLevels=new List<int>();
  public const int RaidTimeoutTicks=3*GenDate.TicksPerDay;
  // 1.81.0 热修3：部位生命公式变了（体质线性、野兽成长）。旧存档（没有这个字段 = 0）读档时按比例缩小一次伤口；新游戏直接是 3。
  public const int HpVersion=3;public int hpVersion=HpVersion;
  public GameComponent_Adventurer(Game game){}

  public override void FinalizeInit(){
   base.FinalizeInit();
   if(hpVersion<HpVersion){hpVersion=HpVersion;Adventurer.RescaleHpFix3(PawnsFinder.AllMapsWorldAndTemporary_Alive);}
   Adventurer.appliedScale=Adventurer.EffectScale;Legend.appliedOn=Legend.On;Adventurer.appliedAffinity=Affinity.On;
   Styles.ResetCaches(); // 1.53.0：静态缓存不跨存档，250 刻内由各人的冒险者状态重建
   Exalted.Reset(); // 1.59.0：超凡掩码登记同上
   TryBackfill();
  }
  // 只补一次：读档时给还没有冒险者状态的人补上（之后靠生成、13 岁生日）。pawns 为空时处理所有活着的人；检查只传测试角色。
  public int TryBackfill(IEnumerable<Pawn> pawns=null){
   if(backfilled || Adventurer.TestSuppressed && pawns==null)return 0;
   backfilled=true;return Backfill(pawns??PawnsFinder.AllMapsWorldAndTemporary_Alive);
  }
  public static int Backfill(IEnumerable<Pawn> pawns){
   int n=0;var colonists=new List<Pawn>();
   foreach(var p in pawns.ToList()){
    if(Adventurer.Get(p)!=null || !Adventurer.Eligible(p))continue;
    if(Adventurer.TryAdd(p,Adventurer.EstimateLevel(p),true)==null)continue;
    n++;if(p.IsFreeColonist)colonists.Add(p);
   }
   if(colonists.Count>0 && Adventurer.Enabled)Find.LetterStack.ReceiveLetter("冒险者等级","DND边缘大陆 1.48.0 加入了冒险者等级：13 岁以上的人都有 1～20 级和力量、敏捷、体质、智力、感知、魅力六项属性。\n\n这个存档里的"+colonists.Count+"名殖民者已经按技能生成了属性，等级按击杀记录估算（每 10 个人形击杀 +1 级，最高 5 级）。在小人检视面板的“冒险者”分页查看。",LetterDefOf.NeutralEvent,new LookTargets(colonists.Where(p=>p.Spawned)));
   return n;
  }

  public void QueueLetter(Pawn p,int level){
   int i=letterPawns.IndexOf(p);
   if(i>=0)letterLevels[i]=Math.Max(letterLevels[i],level);else{letterPawns.Add(p);letterLevels.Add(level);letterFrom.Add(level-1);}
  }
  readonly List<int> letterFrom=new List<int>();
  // 1.52.0 热修：信里逐项写这次升级给了什么（熟练加值和它的效果、属性提升、传奇属性点、VPE 路径点、部位生命、史诗恩赐）。
  public static string LevelGains(Pawn p,Hediff_Adventurer h,int from,int to){
   var sb=new StringBuilder();
   sb.AppendLine(p.LabelShort+" 升到了 "+(to>Adventurer.MaxLevel?"传奇 Lv "+to:to+" 级")+(to-from>1?"（从 "+from+" 级）":"")+"：");
   int pf=Legendary.Proficiency(Math.Max(1,from)),pt=Legendary.Proficiency(to);
   if(pt>pf)sb.AppendLine("· 熟练加值 +"+pf+" → +"+pt+"：现在 "+Adventurer.ProfEffects(pt)+"（相对 +2）");
   else{int next=0;for(int l=to+1;l<=Legend.MaxLevel;l++)if(Legendary.Proficiency(l)>pt){next=l;break;}sb.AppendLine("· 熟练加值仍为 +"+pt+(next>0?"（"+next+" 级升到 +"+(pt+1)+"）":""));}
   if(h==null)return sb.ToString();
   var t=h.Talent;int asi=0,legend=0,vpe=0;bool boon=false;float part=0f;float s=Adventurer.EffectScale;float con=h.ConPart(s);
   for(int l=from+1;l<=to;l++){
    legend+=h.LegendAt(l);asi+=h.AsiAt(l);
    if(l<=Adventurer.MaxLevel){
     if(l==19)boon=true;
    }
    if(l<=Adventurer.MaxLevel || l%2==0)vpe++;
   }
   part=(h.LevelGrowth(Math.Min(to,Adventurer.MaxLevel))-h.LevelGrowth(Math.Max(1,Math.Min(from,Adventurer.MaxLevel))))*s*con; // 1.65.0：等级成长 ×体质倍率；1.81.0 热修2 按职业
   if(asi>0)sb.AppendLine("· 属性提升 ×"+asi+"（每次 "+h.ASIPoints+" 点）"+(h.AvailablePoints>0?"：可用属性点 "+h.AvailablePoints+"，在“冒险者”分页里点 +1 / +2，或改选专长":"：已自动分配"));
   else if(h.AvailablePoints>0)sb.AppendLine("· 还有可用属性点 "+h.AvailablePoints);
   if(legend>0)sb.AppendLine("· 传奇属性点 +"+legend+"（余 "+h.legendPoints+"）");
   if(Adventurer.VPELoaded && vpe>0)sb.AppendLine("· VPE 路径点 +"+vpe);
   {int lc=0;foreach(var th in ClassLegendary.Thresholds)if(from<th && to>=th)lc++;if(lc>0)sb.AppendLine("· 传奇职业点 +"+lc+"（可用 "+ClassLegendary.Points(h)+"，点“传奇职业”按钮或分页的“职业”页）");} // 1.74.0
   {int cp=Math.Max(0,Math.Min(to,ClassSystem.CapLevel)-Math.Max(from,0));int left=ClassSystem.Points(h);if(cp>0 || left>0)sb.AppendLine("· 职业点 +"+cp+(left>0?"（可用 "+left+"，点“选择职业”按钮或分页的“职业”页）":""));} // 1.72.0
   if(ITab_Adventurer.LevelUpLinesHook!=null)for(int l=from+1;l<=to;l++){var extra=ITab_Adventurer.LevelUpLinesHook(p,l);if(extra!=null)foreach(var line in extra)sb.AppendLine("· "+line);} // 1.53.0 职业特性（额外攻击、战斗风格槽）
   {float m0=Adventurer.LevelMelee(from),m1=Adventurer.LevelMelee(to);if(Adventurer.Enabled && m1>m0)sb.AppendLine("· 近战命中、近战闪避各 +"+((m1-m0)*100f).ToString("0.#")+"%（现在各 +"+(m1*100f).ToString("0.#")+"%）");} // 1.81.0
   if(part>0f)sb.AppendLine("· 部位生命 "+ClassLevelInfo.PartText(ClassLevelInfo.PartAtAdventurer(h,Math.Max(1,from)))+" → "+ClassLevelInfo.PartText(Adventurer.PartHealthFactor(p))+"（按职业生命骰，未分配的等级按 +5%）"); // 1.89.0 前后对比（合计倍率，含体质、传奇、坚韧）
   else if(to<=Adventurer.MaxLevel && Adventurer.EffectsHandedToIsekai)sb.AppendLine("· 部位生命成长交给 Isekai（共存模式）");
   if(boon)sb.AppendLine("· 史诗恩赐点 +1"+(h.boonPoints>0?"（"+h.boonPoints+" 点待选）":""));
   if(h.legendary && from<Adventurer.MaxLevel && to>=Adventurer.MaxLevel)sb.AppendLine("· 成为传奇冒险者");
   if(h.CappedAt20 && !h.legendary)sb.AppendLine("· 20 级已满：完成一个 DND 事件里程碑成为传奇冒险者后才能继续升级");
   return sb.ToString();
  }
  // 1.76.0 初始属性购点：同一刻要分配的殖民者合在一封信里（信里每人一个“打开冒险者分页”）。
  readonly List<Pawn> buyNotes=new List<Pawn>();
  public void QueueBuyNote(Pawn p){if(p!=null && !buyNotes.Contains(p))buyNotes.Add(p);}
  public static string BuyNoteText(IEnumerable<Pawn> pawns)=>string.Join("、",pawns.Select(p=>p.LabelShort))+" 可以分配初始 DND 属性。\n\n在“冒险者”分页顶部按 5e 购点制分配六维：基础值 8～15，共 "+Adventurer.BuyBudget+" 点（9～13 每点 1，14、15 每点 2），种族和背景加值另算。已经按技能预填了推荐分配，可以“全部 8”后重新分配。点“确认”后锁定；不确认也按当前分配生效。";
  public void FlushBuyNotes(){
   buyNotes.RemoveAll(p=>p==null || p.Dead || Adventurer.Get(p)?.buyOpen!=true);if(buyNotes.Count==0)return;
   var def=DefDatabase<LetterDef>.GetNamedSilentFail("LP_AdventurerLevelLetter");string label=buyNotes.Count==1?buyNotes[0].LabelShort+"：分配初始属性":"分配初始属性（"+buyNotes.Count+" 人）";
   if(def!=null && LetterMaker.MakeLetter(def) is Letter_AdventurerLevel l){l.Label=label;l.Text=BuyNoteText(buyNotes);l.pawns=buyNotes.ToList();l.lookTargets=new LookTargets(buyNotes.Where(p=>p.Spawned));Find.LetterStack.ReceiveLetter(l);}
   else Find.LetterStack.ReceiveLetter(label,BuyNoteText(buyNotes),LetterDefOf.NeutralEvent);
   buyNotes.Clear();
  }
  public void FlushLetters(){
   FlushBuyNotes();
   if(letterPawns.Count==0)return;
   var sb=new StringBuilder();
   for(int i=0;i<letterPawns.Count;i++){var p=letterPawns[i];sb.AppendLine(LevelGains(p,Adventurer.Get(p),letterFrom[i],letterLevels[i]));}
   var def=DefDatabase<LetterDef>.GetNamedSilentFail("LP_AdventurerLevelLetter");
   string label=letterPawns.Count==1?letterPawns[0].LabelShort+" 升级":letterPawns.Count+" 人升级";
   if(def!=null && LetterMaker.MakeLetter(def) is Letter_AdventurerLevel l){
    l.Label=label;l.Text=sb.ToString().TrimEnd();l.pawns=letterPawns.ToList();l.lookTargets=new LookTargets(letterPawns.Where(p=>p.Spawned || p.Corpse!=null));
    Find.LetterStack.ReceiveLetter(l);
   }else Find.LetterStack.ReceiveLetter(label,sb.ToString().TrimEnd(),LetterDefOf.PositiveEvent);
   letterPawns.Clear();letterLevels.Clear();letterFrom.Clear();
  }

  // 袭击：开始时记下袭击者和已经征召的殖民者；之后征召过、或打过袭击者的殖民者也算参战。
  public RaidRecord StartRaid(Map map,float points,IEnumerable<Pawn> raiders){
   var r=new RaidRecord{map=map,points=points,start=Find.TickManager.TicksGame,raiders=raiders.Distinct().ToList()};
   if(r.raiders.Count==0)return null;
   foreach(var p in map.mapPawns.FreeColonistsSpawned)if(p.Drafted)r.fighters.Add(p);
   raids.Add(r);return r;
  }
  public void NoteDrafted(Pawn p){if(p?.Map==null)return;foreach(var r in raids)if(r.map==p.Map && !r.fighters.Contains(p))r.fighters.Add(p);}
  public void NoteFighter(Pawn attacker,Pawn victim){foreach(var r in raids)if(r.map==attacker.Map && r.raiders.Contains(victim) && !r.fighters.Contains(attacker))r.fighters.Add(attacker);}
  // 结束：袭击者都倒下、死掉、离开或不再敌对。地图还在、还有站着的殖民者就算守住；点数 × 0.25 平分给参战的人。
  public int Resolve(RaidRecord r){
   raids.Remove(r);
   if(r.map==null || !Find.Maps.Contains(r.map) || !r.map.mapPawns.FreeColonistsSpawned.Any(p=>!p.Downed))return 0;
   HeroDeeds.RaidDefended(r); // 1.58.0 绝境英雄、预言“守住 10 次袭击”
   var got=r.fighters.Where(p=>p!=null && !p.Dead && Adventurer.Get(p)!=null).Distinct().ToList();
   if(got.Count==0)return 0;
   float each=r.points*Adventurer.RaidShare/got.Count;
   foreach(var p in got)Adventurer.GainXP(p,each);
   return got.Count;
  }
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;
   if(now%60==0)FlushLetters();
   if(now%250==17)Feats.RefreshSentinels(); // 1.48.1 哨兵名单
   if(now%600==0)for(int i=raids.Count-1;i>=0;i--){var r=raids[i];if(r.Over || now-r.start>RaidTimeoutTicks)Resolve(r);}
   if(now%GenDate.TicksPerDay==0)foreach(var v in damageTick.Where(kv=>now-kv.Value>GenDate.TicksPerDay || kv.Key.Destroyed).Select(kv=>kv.Key).ToList()){damage.Remove(v);damageTick.Remove(v);}
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref backfilled,"lpAdventurerBackfilled");
   Scribe_Values.Look(ref hpVersion,"lpAdventurerHpVersion",0); // 1.81.0 热修3
   Scribe_Collections.Look(ref raids,"lpAdventurerRaids",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(raids==null)raids=new List<RaidRecord>();raids.RemoveAll(r=>r==null || r.map==null);}
  }
 }

 // ---------- 挂钩 ----------
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class AdventurerGenerate {
  [HarmonyPriority(Priority.Last)]
  static void Postfix(Pawn __result){
   if(__result==null || __result.Dead)return;
   var h=Adventurer.TryAdd(__result);
   // 1.48.1：访客、商队、难民等非敌对的外人 1～3 级随机（敌对的人由袭击组或剧情代码定等级）。
   if(h!=null && AdventurerEnemies.On && __result.Faction!=null && !__result.Faction.IsPlayer && !__result.Faction.HostileTo(Faction.OfPlayer))h.RaiseTo(Rand.RangeInclusive(1,3));
  }
 }
 // 13 岁生日（原版成长时刻）补发 1 级。
 [HarmonyPatch(typeof(Pawn_AgeTracker),"BirthdayBiological")]
 public static class AdventurerBirthday {
  static void Postfix(Pawn ___pawn,int birthdayAge){if(birthdayAge>=Adventurer.MinAge && ___pawn!=null && !___pawn.Dead)Adventurer.TryAdd(___pawn);}
 }
 // 变成死灵造物、召唤物时立刻拿掉（生成时这些状态在 GeneratePawn 之后才加上）。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.AddHediff),new[]{typeof(Hediff),typeof(BodyPartRecord),typeof(DamageInfo?),typeof(DamageWorker.DamageResult)})]
 public static class AdventurerExcluded {
  static void Postfix(Pawn_HealthTracker __instance,Hediff hediff,Pawn ___pawn){
   if(hediff==null || !(hediff is Hediff_Construct || hediff.def.defName=="LP_Conjured"))return;
   var h=Adventurer.Get(___pawn);if(h!=null)__instance.RemoveHediff(h);
  }
 }
 [HarmonyPatch(typeof(Pawn_SkillTracker),nameof(Pawn_SkillTracker.Learn))]
 public static class AdventurerLearn {
  static void Prefix(Pawn ___pawn,SkillDef sDef,ref float xp,bool direct){if(xp<=0f || direct)return;float f=Adventurer.LearnFactor(___pawn,sDef);if(f!=1f)xp*=f;}
 }
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class AdventurerDamage {
  static void Postfix(Thing __instance,DamageInfo dinfo,DamageWorker.DamageResult __result){
   if(__instance is Pawn v && !v.Dead && __result!=null && __result.totalDamageDealt>0f && dinfo.Instigator is Pawn a && Adventurer.KillCounts(v))Adventurer.RecordDamage(v,a,__result.totalDamageDealt);
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class AdventurerKill {
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out bool __state){
   __state=!__instance.Dead && Adventurer.KillCounts(__instance);
   // 致命的那一下在 TakeDamage 返回之前就触发了死亡：这里补记。
   if(__state && dinfo.HasValue && dinfo.Value.Instigator is Pawn a)Adventurer.RecordDamage(__instance,a,dinfo.Value.Amount);
  }
  static void Postfix(Pawn __instance,bool __state){if(__state && __instance.Dead)Adventurer.DistributeKill(__instance);}
 }
 // 1.52.0 热修：熟练加值的近战命中加在最终命中率上（MeleeHitChance 的属性曲线会把前面的偏移压没，同群嚎的做法）。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class AdventurerMeleeHit {
  static void Postfix(Verb_MeleeAttack __instance,LocalTargetInfo target,ref float __result){
   float b=Adventurer.MeleeHitBonus(__instance.CasterPawn)+Adventurer.LevelMelee(__instance.CasterPawn);if(b>0f)__result=Mathf.Min(1f,__result+b); // 1.81.0 加等级
   if(Styles.protectors.Count>0 && target.Thing is Pawn t)__result*=Styles.Protect(t,__instance.CasterPawn); // 1.53.0 守护风格
   float g=Adventurer.LevelGapFactor(__instance.CasterPawn,target.Thing);if(g<1f)__result*=g; // 1.81.0 热修3 等级差防御
  }
 }
 // 1.81.0 热修4 疼痛随部位生命缩放：原版 Hediff_Injury / Hediff_MissingPart 的 PainOffset 后缀。没有冒险者状态、不是动物时 PainDivisor 第一步返回 1。
 [HarmonyPatch]
 public static class AdventurerPainScale {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){
   var a=AccessTools.DeclaredPropertyGetter(typeof(Hediff_Injury),"PainOffset");if(a!=null)yield return a;
   var b=AccessTools.DeclaredPropertyGetter(typeof(Hediff_MissingPart),"PainOffset");if(b!=null)yield return b;
  }
  static bool Prepare()=>TargetMethods().Any();
  static void Postfix(Hediff __instance,ref float __result){if(__result<=0f)return;float d=Adventurer.PainDivisor(__instance.pawn);if(d>1f)__result/=d;}
 }
 // 1.81.0 热修3 等级差防御（远程）：乘 ShotReport 的“射手与距离”系数（同 SubraceShotReport），瞄准提示里也看得到。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class AdventurerShotLevelGap {
  static readonly AccessTools.StructFieldRef<ShotReport,float> factor=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromShooterAndDist");
  static void Postfix(Thing caster,LocalTargetInfo target,ref ShotReport __result){
   float g=Adventurer.LevelGapFactor(caster as Pawn,target.Thing);if(g<1f)factor(ref __result)*=g;
  }
 }
 // 1.81.0 热修：等级给的近战闪避加在最终闪避率上（同命中，属性曲线会压没偏移）。原版判定不能闪避（突袭、目标不能动、正在瞄准远程）时是 0，不加；上限 95%。
 // 方法名按原版 Verb_MeleeAttack.GetDodgeChance；找不到就不打补丁（Prepare 返回 false），不影响加载。
 [HarmonyPatch]
 public static class AdventurerMeleeDodge {
  static System.Reflection.MethodBase Method=>AccessTools.Method(typeof(Verb_MeleeAttack),"GetDodgeChance");
  static bool Prepare()=>Method!=null;
  static System.Reflection.MethodBase TargetMethod()=>Method;
  static void Postfix(LocalTargetInfo target,ref float __result){
   if(__result<=0f || !(target.Thing is Pawn t))return;
   float b=Adventurer.LevelMelee(t);if(b>0f)__result=Mathf.Min(.95f,__result+b);
  }
 }
 [HarmonyPatch(typeof(IncidentWorker_Raid),"TryExecuteWorker")]
 public static class AdventurerRaidStart {
  static void Prefix(IncidentParms parms,out HashSet<Lord> __state){__state=parms?.target is Map map?new HashSet<Lord>(map.lordManager.lords):null;}
  static void Postfix(IncidentParms parms,bool __result,HashSet<Lord> __state){
   if(!__result || __state==null || !(parms.target is Map map) || !Adventurer.Enabled)return;
   var raiders=map.lordManager.lords.Where(l=>!__state.Contains(l) && l.faction!=null && l.faction.HostileTo(Faction.OfPlayer)).SelectMany(l=>l.ownedPawns).ToList();
   Adventurer.State?.StartRaid(map,parms.points,raiders);
  }
 }
 [HarmonyPatch(typeof(Pawn_DraftController),"set_Drafted")]
 public static class AdventurerDrafted {
  static void Postfix(Pawn_DraftController __instance,bool value){if(value && __instance.pawn!=null){Adventurer.State?.NoteDrafted(__instance.pawn);Feats.NoteDrafted(__instance.pawn);}}
 }
 [HarmonyPatch(typeof(Quest),nameof(Quest.End))]
 public static class AdventurerQuest {
  static void Postfix(Quest __instance,QuestEndOutcome outcome){if(outcome==QuestEndOutcome.Success)Adventurer.QuestDone(__instance);}
 }

 // ---------- 设置 ----------
 public partial class Settings {
  public bool adventurer=true,adventurerAutoASI=false,adventurerIsekaiCoexist=true;public int adventurerXpPercent=100,adventurerEffectPercent=100;
  // 1.48.1 敌人等级，上限 = 殖民地最高等级 + N。
  // 1.63.0：原来开关分两份存（平时一份、Isekai 共存模式一份），玩家关掉其中一份后切换模式就以为“打不开”，敌人全是 1 级。合并成一个，两种模式都用，默认开。
  public bool adventurerEnemyLevels=true,adventurerEnemyCap=true;public int adventurerEnemyCapOffset=3;
  public bool adventurerPointBuy=true;public int adventurerPointBuyPoints=Adventurer.BuyDefault; // 1.76.0 初始属性购点
  public bool adventurerLevelGap=true; // 1.81.0 热修3 等级差防御
  public bool adventurerPainScale=true; // 1.81.0 热修4 疼痛随部位生命缩放
  void ExposeAdventurer(){
   Scribe_Values.Look(ref adventurerLevelGap,"adventurerLevelGap",true);Scribe_Values.Look(ref adventurerPainScale,"adventurerPainScale",true);
   Scribe_Values.Look(ref adventurerPointBuy,"adventurerPointBuy",true);Scribe_Values.Look(ref adventurerPointBuyPoints,"adventurerPointBuyPoints",Adventurer.BuyDefault);
   adventurerPointBuyPoints=Math.Max(Adventurer.BuyMin,Math.Min(54,adventurerPointBuyPoints));
   ExposeEpic(); // 1.49.0 击杀里程碑、宿敌冠军
   ExposeLegend(); // 1.50.0 传奇等级
   ExposeAffinity(); // 1.51.0 种族亲和
   ExposeExalted(); // 1.59.0 超凡属性特性
   ExposeWork(); // 1.61.0 热修：日常工作经验
   ExposeExtraXp(); // 1.75.0 非战斗经验
   ExposeQuestBoard(); // 1.71.0 探索公告板
   ExposeClasses(); // 1.72.0 职业
   adventurerEnemyLevels=ExposeMergedToggle(adventurerEnemyLevels,"lpEnemyLevels163","adventurerEnemyLevels","adventurerEnemyLevelsCoexist");
   Scribe_Values.Look(ref adventurerEnemyCap,"adventurerEnemyCap",true);Scribe_Values.Look(ref adventurerEnemyCapOffset,"adventurerEnemyCapOffset",3);
   adventurerEnemyCapOffset=Math.Max(0,Math.Min(19,adventurerEnemyCapOffset));
   Scribe_Values.Look(ref adventurer,"adventurer",true);Scribe_Values.Look(ref adventurerAutoASI,"adventurerAutoASI",false);Scribe_Values.Look(ref adventurerIsekaiCoexist,"adventurerIsekaiCoexist",true);
   Scribe_Values.Look(ref adventurerXpPercent,"adventurerXpPercent",100);Scribe_Values.Look(ref adventurerEffectPercent,"adventurerEffectPercent",100);
   adventurerXpPercent=Math.Max(25,Math.Min(400,adventurerXpPercent));adventurerEffectPercent=Math.Max(0,Math.Min(200,adventurerEffectPercent));
  }
 }
 public static class AdventurerSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;l.GapLine();l.Label("冒险者");
   l.CheckboxLabeled("冒险者等级",ref s.adventurer,"13 岁以上的人有冒险者等级 1～20 和六项属性（力量、敏捷、体质、智力、感知、魅力）。击杀、守住袭击、完成任务、DND 事件里程碑给经验，日常工作也有少量；每 4 级一次属性提升。关闭后不再获得经验、属性没有效果、隐藏“冒险者”分页；已有的等级和属性保留。");
   l.Label("冒险者经验倍率："+s.adventurerXpPercent+"%");s.adventurerXpPercent=Mathf.RoundToInt(l.Slider(s.adventurerXpPercent,25,400)/5f)*5;
   TooltipHandler.TipRegion(l.Label("日常工作经验倍率："+(s.adventurerWorkXp<=0f?"关闭":"×"+s.adventurerWorkXp.ToString("0.0"))),"每 50 点技能经验 = 1 × 倍率 冒险者经验；每天前 80 点全额，之后 ×0.5。0 = 关闭。");s.adventurerWorkXp=Mathf.Round(l.Slider(s.adventurerWorkXp,0f,3f)*10f)/10f; // 1.61.0 热修
    Settings.DrawExtraXp(l); // 1.75.0 非战斗经验
   l.CheckboxLabeled("探索公告板",ref s.questBoard,"1.71.0：建筑“探索公告板”（杂项）每天贴出 6 份分等阶的委托：守卫家园、远征讨伐（怪物巢穴）、清剿据点。等阶青铜 / 白银 / 黄金 / 秘银 / 传说，需要殖民地最高冒险者等级 1 / 5 / 9 / 13 / 17；完成给银币和物品报酬，接取人得到“任务点数 × 0.5”的冒险者经验。关闭后不再刷新委托，已接的委托照常进行。"); // 1.71.0
   l.Label("属性效果强度："+s.adventurerEffectPercent+"%（六维的数值效果、技能学习速度、部位生命成长；0% 等于只看等级）");s.adventurerEffectPercent=Mathf.RoundToInt(l.Slider(s.adventurerEffectPercent,0,200)/5f)*5;
   l.CheckboxLabeled("种族亲和",ref s.adventurerAffinity,"每个种族有擅长和笨拙的属性：擅长的六维属性每点正调整值的效果 ×1.5（传奇种族 ×1.75），笨拙的 ×0.5（负调整值不乘）；擅长的传奇属性每点 ×1.5（传奇种族 ×1.75），人类所有传奇属性 ×1.1。只改效果，不改属性数值、上限和专长门槛。在“冒险者”分页每项后面标“擅长 / 笨拙”。\n关闭后所有种族 ×1（天赋特性照常）。");
   ClassSettings.Draw(l); // 1.72.0 职业
   l.CheckboxLabeled("手动分配初始属性（购点制）",ref s.adventurerPointBuy,"开启后新的殖民者（开局、加入、招募）的六维由玩家在“冒险者”分页按 5e 购点制分配：基础值 8～15，9～13 每点 1、14 / 15 每点 2，种族和背景加值另算。预填按技能推荐的标准数组（15/14/13/12/10/8 = 27 点），确认后锁定。每人只有一次。关闭后直接用推荐分配。1.76.0 之前生成的小人没有这一项。"); // 1.76.0
   if(s.adventurerPointBuy){l.Label("初始购点："+s.adventurerPointBuyPoints+"（5e 标准 27）");s.adventurerPointBuyPoints=Mathf.RoundToInt(l.Slider(s.adventurerPointBuyPoints,15,45));}
   l.CheckboxLabeled("殖民者自动加点",ref s.adventurerAutoASI,"开启后殖民者升到 4、8、12、16 级时自动属性提升：职业主属性优先（装了 VPE 且有 DND 职业），否则加在最高的一项。关闭时在“冒险者”分页或升级信件里手动加点。敌人和访客永远自动。");
   bool isekai=Adventurer.IsekaiActive;
   var old=GUI.color;if(!isekai)GUI.color=Color.gray;
   bool v=s.adventurerIsekaiCoexist;
   l.CheckboxLabeled("Isekai 共存模式"+(isekai?"":"（未检测到 Isekai）"),ref v,"检测到 Isekai RPG Leveling 时默认开启：冒险者等级、经验、加点照常，但六维属性的数值效果关闭（交给 Isekai 自己的属性，避免叠加）。关掉这一项可以让两套属性同时生效。没有 Isekai 时这一项不起作用。");
   if(isekai)s.adventurerIsekaiCoexist=v;
   GUI.color=old;
   // 1.48.1 敌人等级
   l.CheckboxLabeled("敌人冒险者等级",ref s.adventurerEnemyLevels,"袭击和 DND 事件的人形敌人按袭击点数拿冒险者等级：每人分到的点数 ÷ 45 + 1 级，±2 浮动；领队 +3，精英兵种 +2～+5；剧情首领 12～18 级。等级越高的敌人每人占的点数越多（每级 +3%），所以人数变少。访客、商队 1～3 级。\n开不开 Isekai 共存模式都用这一个开关（1.63.0 起不再分两份）。");
   l.CheckboxLabeled("敌人等级上限：殖民地最高等级 + "+s.adventurerEnemyCapOffset,ref s.adventurerEnemyCap,"开启时袭击敌人的冒险者等级不超过殖民地最高冒险者等级 + N（剧情首领不受限）。防止开局碰到高等级敌人。");
   if(s.adventurerEnemyCap){s.adventurerEnemyCapOffset=Mathf.RoundToInt(l.Slider(s.adventurerEnemyCapOffset,0,10));}
   l.CheckboxLabeled("等级差防御",ref s.adventurerLevelGap,"攻击者每比目标低 1 级，近战和远程命中率 ×(1 − 2%)，最多 −50%（低 10 级 ×0.8，低 25 级以上 ×0.5）。等级按冒险者等级（含传奇等级 21～40），动物按野性等级；机械体、炮塔、传奇怪物没有等级，不参与。"); // 1.81.0 热修3
   l.CheckboxLabeled("疼痛随部位生命缩放",ref s.adventurerPainScale,"伤口和缺失部位的疼痛 ÷ 部位生命倍率：部位生命 ×2 的人受同样的伤只有一半疼痛，因此更晚因疼痛倒地。冒险者按等级、体质、传奇血量等的部位生命倍率，动物按野性等级；传奇怪物不变。关闭后疼痛照原版，只按伤口大小算。"); // 1.81.0 热修4
   LegendSettings.Draw(l); // 1.50.0
   ExaltedSettings.Draw(l); // 1.59.0
  }
 }
}
