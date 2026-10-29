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
   h.Setup(GenerateScores(p),startLevel);
   p.health.AddHediff(h);
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
  public static int[] GenerateScores(Pawn p){
   var s=BaseArray(p);var r=RacialBonus(p,s);var t=TraitBonus(p);
   for(int i=0;i<6;i++)s[i]=Mathf.Clamp(s[i]+r[i]+t[i],ScoreMin,ScoreCap);
   return s;
  }

  // ---------- 经验 ----------
  public static float GainXP(Pawn p,float amount,bool scaled=true){
   if(!Enabled || amount<=0f)return 0f;var h=Get(p);if(h==null)return 0f;
   if(scaled)amount*=XpScale*Talents.XpFactor(h);h.AddXp(amount);return amount; // 1.51.0 凡人之志 +25%
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
  // 部位生命（1.65.0）：（1 + 每级 1%）× 体质倍率（整条曲线，低于 10 的体质按 ×1，不会让部位生命变少）。
  public static float forcedScale=-1f;
  public static float PartHealthFactor(Pawn p){var h=Get(p);if(h==null || Legendary.Is(p))return 1f;return h.PartFactor(forcedScale>=0f?forcedScale:EffectScale)*Legend.HealthFactor(h)*Feats.ToughFactor(h);} // 1.50.0 传奇属性点“血量”相乘；1.67.0 坚韧
  // 职业强度（VPE 桥）：0.1 ×（主属性倍率 − 1）（乘属性效果强度）+ 0.02 ×（熟练加值 − 2）。
  public const float ClassPowerPer=.1f;
  public static float ClassPowerBonus(Pawn p,int ability){
   if(!Enabled || ability<0)return 0f;var h=Get(p);if(h==null)return 0f;
   return ClassPowerPer*(h.Fx(ability,EffectScale)-1f)+ProfClassPer*Math.Max(0,h.Proficiency-2);
  }
  // 1.52.0 热修：熟练加值每比 +2 高 1 点 —— 近战命中 +2%（加在最终命中率上，见 AdventurerMeleeHit）、射击精度 ×+1%（合进状态的属性系数）、VPE 职业强度 +0.02。
  // 只看等级，不乘“属性效果强度”，Isekai 共存时也生效；传奇怪物（Legendary）不算。
  public const float ProfHitPer=.02f,ProfAimPer=.01f,ProfClassPer=.02f;
  public static float ProfMeleeHit(int prof)=>ProfHitPer*Math.Max(0,prof-2);
  public static float ProfAim(int prof)=>ProfAimPer*Math.Max(0,prof-2);
  public static float MeleeHitBonus(Pawn p){if(!Enabled)return 0f;var h=Get(p);if(h==null || Legendary.Is(p))return 0f;return ProfMeleeHit(h.Proficiency);}
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
  public float AMod(int a)=>LichPhylactery.Affinity.Apply(Mod(a),LichPhylactery.Affinity.ScoreMult(this,a));
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
  public int Eff(int a)=>Math.Min(scores[a],Cap(a))+CombatGifts.ScoreBonus(this,a)+Feats.ScoreBonus(this,a); // 1.66.0 武艺天赋属性 +2/+3/+4、1.67.0 专长 +1 加在上限之后（上限同样提高）
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
    if(b==Epic.Might){a=Adventurer.PrimaryAbilityHook?.Invoke(pawn)??-1;if(a<0)a=Adventurer.KindExt(pawn)?.Primary??-1;if(a<0)a=Enumerable.Range(0,6).OrderByDescending(i=>scores[i]).First();}
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
  public string CanTakeFeat(LP_FeatDef f){
   if(f==null)return "没有这个专长";if(HasFeat(f))return "已经有了";if(pendingASI<=0)return "没有可用的属性提升";
   return f.Worker.Unmet(pawn,this);
  }
  public bool TakeFeat(LP_FeatDef f){if(CanTakeFeat(f)!=null)return false;pendingASI--;AddFeat(f);return true;}
  public Dictionary<string,int> featScore=new Dictionary<string,int>(); // 1.67.0 专长附带的 +1 加在哪项属性
  public void AddFeat(LP_FeatDef f){if(f==null || HasFeat(f))return;feats.Add(f);Feats.AssignScore(this,f);f.Worker.Gained(pawn,this);HeroTraits.Invalidate(pawn);Feats2.Sync(pawn);Dirty();} // 1.58.1：专长也进 1.58 的快速返回缓存
  public void RemoveFeat(LP_FeatDef f){if(!HasFeat(f))return;feats.Remove(f);featUsed.Remove(f.defName);featScore.Remove(f.defName);if(originFeat==f)originFeat=null;f.Worker.Lost(pawn,this);HeroTraits.Invalidate(pawn);Feats2.Sync(pawn);Dirty();}
  // 敌人（非玩家阵营）的属性提升：按兵种类别随机一个满足前置的专长。
  public bool TryEnemyFeat(){
   var roles=Feats.EnemyRoles(pawn);
   var pick=DefDatabase<LP_FeatDef>.AllDefsListForReading.Where(f=>!HasFeat(f) && f.enemyTags!=null && f.enemyTags.Any(t=>t=="any" || roles.Contains(t)) && f.Worker.Unmet(pawn,this)==null).ToList();
   if(pick.Count==0)return false;pendingASI--;AddFeat(pick.RandomElement());return true;
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
    int pr=Adventurer.PrimaryAbilityHook?.Invoke(pawn)??-1;if(pr<0)pr=Adventurer.KindExt(pawn)?.Primary??-1;
    foreach(int i in Enumerable.Range(0,6).OrderByDescending(i=>i==pr?100:scores[i]).ThenBy(i=>i).ToList()){int add=Math.Min(asiLeft,Cap(i)-scores[i]);if(add>0){scores[i]+=add;asiLeft-=add;}if(asiLeft<=0)break;}
    asiLeft=0;Dirty();
   }
   for(int guard=0;pendingASI>0 && guard<10;guard++){
    if(npc && Rand.Chance(Feats.EnemyFeatChance) && TryEnemyFeat())continue;
    int primary=Adventurer.PrimaryAbilityHook?.Invoke(pawn)??-1;
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
  public enum FxKind{Factor,Inverse,Offset}
  public class ScoreFx{public int a;public string stat,label;public FxKind kind;public float cap,per,lo,hi;public bool num;}
  static ScoreFx Fa(int a,string stat,string label,float cap=0f)=>new ScoreFx{a=a,stat=stat,label=label,kind=FxKind.Factor,cap=cap};
  static ScoreFx Of(int a,string stat,string label,float per,float lo,float hi,bool num=false)=>new ScoreFx{a=a,stat=stat,label=label,kind=FxKind.Offset,per=per,lo=lo,hi=hi,num=num};
  public static readonly ScoreFx[] Table={
   Fa(0,"MeleeDamageFactor","近战伤害"),Fa(0,"CarryingCapacity","负重"),Fa(0,"VEF_MassCarryCapacity","商队负重"),Fa(0,"MiningSpeed","采矿速度"),
   Fa(1,"ShootingAccuracyPawn","射击精度"),Fa(1,"MeleeHitChance","近战命中"),new ScoreFx{a=1,stat="AimingDelayFactor",label="瞄准时间",kind=FxKind.Inverse,cap=.05f},
   Of(1,"MeleeDodgeChance","近战闪避",3f,-3f,50f,true),Fa(1,"MoveSpeed","移速",5f),Fa(1,"CaravanBonusSpeedFactor","商队速度",3f),
   Fa(2,"ImmunityGainSpeed","免疫速度",5f),Fa(2,"InjuryHealingFactor","伤口愈合",10f),Fa(2,"RestRateMultiplier","休息恢复",3f),
   Of(2,"PainShockThreshold","疼痛休克阈值",.05f,-.2f,.5f),Of(2,"ToxicResistance","毒素抗性",.05f,-.1f,.95f),
   Of(2,"ArmorRating_Sharp","利器护甲",.03f,0f,.8f),Of(2,"ArmorRating_Blunt","钝器护甲",.03f,0f,.95f),Of(2,"ArmorRating_Heat","高温护甲",.03f,0f,.7f),
   Fa(3,"ResearchSpeed","研究速度"),Fa(3,"GlobalLearningFactor","全局学习",100f),Fa(3,"WorkSpeedGlobal","工作速度",5f),Fa(3,"HackingSpeed","黑客速度",5f),
   Fa(4,"MedicalTendQuality","医疗品质"),Fa(4,"PsychicSensitivity","心灵敏感度",5f),Fa(4,"MedicalSurgerySuccessChance","手术成功率",4f),Fa(4,"MeditationFocusGain","冥想专注",2.5f),
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
   }
   CombatGifts.AddStats(this,st,f,o); // 1.66.0 武艺天赋的附带效果（不乘属性效果强度）
   if(s<=0f || Legendary.Is(pawn)){Finish(st,f,o);return st;}
   foreach(var e in Table)if(e.kind==FxKind.Offset)Add(o,e.stat,FxValue(e,s),0f);else Add(f,e.stat,FxValue(e,s),1f);
   Finish(st,f,o);
   return st;
  }
  // 同一个 stat 在列表里只认第一条（原版 GetStatOffsetFromList），所以合并：偏移相加、系数相乘（例如灵活 + 敏捷的移速、心灵之恩 + 感知的心灵敏感度）。
  static void Finish(HediffStage st,List<StatModifier> f,List<StatModifier> o){
   if(f.Count>0)st.statFactors=f.GroupBy(m=>m.stat).Select(g=>new StatModifier{stat=g.Key,value=g.Aggregate(1f,(x,m)=>x*m.value)}).ToList();
   if(o.Count>0)st.statOffsets=o.GroupBy(m=>m.stat).Select(g=>new StatModifier{stat=g.Key,value=g.Sum(m=>m.value)}).ToList();
  }
  public const float PartPerLevel=.01f;
  public float PartFactor(float s){
   if(s<=0f)return 1f;
   float lv=1f+(Math.Min(level,Adventurer.MaxLevel)-1)*PartPerLevel*s; // 1.50.0：传奇等级不再加等级成长（血量走传奇属性点）
   return lv*Math.Max(1f,Fx(2,s));
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
   sb.AppendLine("属性倍率 "+x(Adventurer.Curve(Eff(a)))+(Math.Abs(AMult(a)-Adventurer.Curve(Eff(a)))>.001f?"（种族亲和后 "+x(AMult(a))+"）":"")+"：10 = ×1，每 +5 约翻倍，每 +10 ×4");
   var parts=new List<string>();
   if(a==Adventurer.Con)parts.Add("部位生命 "+x(PartFactor(s))+"（含等级成长，体质低于 10 不减少）");
   foreach(var e in Table)if(e.a==a && S(e.stat)!=null){float v=FxValue(e,s);parts.Add(e.label+" "+(e.kind!=FxKind.Offset?x(v):e.num?(v>=0?"+":"")+v.ToString("0.#"):pct(v)));}
   sb.AppendLine(string.Join("，",parts));
   string skills=a==0?"格斗":a==1?"射击":a==3?"智识":a==4?"医疗、种植、驯兽":a==5?"社交":null;
   if(skills!=null)sb.AppendLine(skills+"学习速度 "+x(Mathf.Max(Adventurer.LearnMin,Fx(a,s))));
   sb.Append("学习速度最低 ×"+Adventurer.LearnMin.ToString("0.#")+"；带上限的项超出部分按 Isekai 的方式逐渐收敛。");
   var tip=LichPhylactery.Affinity.Tip(this,a);if(tip!="")sb.AppendLine().Append(tip); // 1.51.0
   return sb.ToString();
  }

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
   if(pawn!=null)Exalted.Tick(this); // 1.59.0 超凡：补算掩码、发信、光环 / 显形 / 封顶（掩码为 0 时只比较一次）
   if(pawn!=null && !pawn.Dead)CombatGifts.Tick250(this); // 1.66.0 狂暴之血阈值、再生之体长回肢体（没有武艺天赋的人一次缓存查找）
   if(!Adventurer.Enabled || pawn==null || pawn.Dead)return;
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
   Scribe_Values.Look(ref vpeGranted,"lpVpeGranted");Scribe_Values.Look(ref vpeBanked,"lpVpeBanked");
   var list=scores?.ToList();Scribe_Collections.Look(ref list,"lpScores",LookMode.Value);
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
   Scribe_Values.Look(ref cgBonus,"lpCgBonus");Scribe_Values.Look(ref cgRegenTick,"lpCgRegen",-1); // 1.66.0 武艺天赋
   if(Scribe.mode==LoadSaveMode.LoadingVars || Scribe.mode==LoadSaveMode.PostLoadInit){
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
  public GameComponent_Adventurer(Game game){}

  public override void FinalizeInit(){
   base.FinalizeInit();
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
   var t=h.Talent;int asi=0,legend=0,vpe=0;bool boon=false;float part=0f;float s=Adventurer.EffectScale;float con=Math.Max(1f,h.Fx(Adventurer.Con,s));
   for(int l=from+1;l<=to;l++){
    legend+=h.LegendAt(l);asi+=h.AsiAt(l);
    if(l<=Adventurer.MaxLevel){
     if(l==19)boon=true;
     if(l>1)part+=Hediff_Adventurer.PartPerLevel*s*con; // 1.65.0：等级成长 ×体质倍率
    }
    if(l<=Adventurer.MaxLevel || l%2==0)vpe++;
   }
   if(asi>0)sb.AppendLine("· 属性提升 ×"+asi+"（每次 "+h.ASIPoints+" 点）"+(h.AvailablePoints>0?"：可用属性点 "+h.AvailablePoints+"，在“冒险者”分页里点 +1 / +2，或改选专长":"：已自动分配"));
   else if(h.AvailablePoints>0)sb.AppendLine("· 还有可用属性点 "+h.AvailablePoints);
   if(legend>0)sb.AppendLine("· 传奇属性点 +"+legend+"（余 "+h.legendPoints+"）");
   if(Adventurer.VPELoaded && vpe>0)sb.AppendLine("· VPE 路径点 +"+vpe);
   if(ITab_Adventurer.LevelUpLinesHook!=null)for(int l=from+1;l<=to;l++){var extra=ITab_Adventurer.LevelUpLinesHook(p,l);if(extra!=null)foreach(var line in extra)sb.AppendLine("· "+line);} // 1.53.0 职业特性（额外攻击、战斗风格槽）
   if(part>0f)sb.AppendLine("· 部位生命 +"+(part*100f).ToString("0.#")+"%（现在共 +"+((h.PartFactor(s)-1f)*100f).ToString("0.#")+"%）");
   else if(to<=Adventurer.MaxLevel && Adventurer.EffectsHandedToIsekai)sb.AppendLine("· 部位生命成长交给 Isekai（共存模式）");
   if(boon)sb.AppendLine("· 史诗恩赐点 +1"+(h.boonPoints>0?"（"+h.boonPoints+" 点待选）":""));
   if(h.legendary && from<Adventurer.MaxLevel && to>=Adventurer.MaxLevel)sb.AppendLine("· 成为传奇冒险者");
   if(h.CappedAt20 && !h.legendary)sb.AppendLine("· 20 级已满：完成一个 DND 事件里程碑成为传奇冒险者后才能继续升级");
   return sb.ToString();
  }
  public void FlushLetters(){
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
   float b=Adventurer.MeleeHitBonus(__instance.CasterPawn);if(b>0f)__result=Mathf.Min(1f,__result+b);
   if(Styles.protectors.Count>0 && target.Thing is Pawn t)__result*=Styles.Protect(t,__instance.CasterPawn); // 1.53.0 守护风格
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
  void ExposeAdventurer(){
   ExposeEpic(); // 1.49.0 击杀里程碑、宿敌冠军
   ExposeLegend(); // 1.50.0 传奇等级
   ExposeAffinity(); // 1.51.0 种族亲和
   ExposeExalted(); // 1.59.0 超凡属性特性
   ExposeWork(); // 1.61.0 热修：日常工作经验
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
   l.Label("属性效果强度："+s.adventurerEffectPercent+"%（六维的数值效果、技能学习速度、部位生命成长；0% 等于只看等级）");s.adventurerEffectPercent=Mathf.RoundToInt(l.Slider(s.adventurerEffectPercent,0,200)/5f)*5;
   l.CheckboxLabeled("种族亲和",ref s.adventurerAffinity,"每个种族有擅长和笨拙的属性：擅长的六维属性每点正调整值的效果 ×1.5（传奇种族 ×1.75），笨拙的 ×0.5（负调整值不乘）；擅长的传奇属性每点 ×1.5（传奇种族 ×1.75），人类所有传奇属性 ×1.1。只改效果，不改属性数值、上限和专长门槛。在“冒险者”分页每项后面标“擅长 / 笨拙”。\n关闭后所有种族 ×1（天赋特性照常）。");
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
   LegendSettings.Draw(l); // 1.50.0
   ExaltedSettings.Draw(l); // 1.59.0
  }
 }
}
