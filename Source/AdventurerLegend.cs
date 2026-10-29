using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.50.0 传奇等级 21～40 与传奇属性点（见 ArtSource/Leveling-1.50.0-spec.md）。
 // 20 级的人经验照常累积；成为传奇冒险者后上限变成 40，攒下的经验立即结算。21～40 级没有属性提升和专长，每级 2 个传奇属性点，VPE 路径点每 2 级 1 点。
 // 八项传奇属性没有上限、不乘“属性效果强度”（Isekai 共存时照样生效），只看冒险者总开关和“传奇等级”开关。存档字段在 Hediff_Adventurer 里（legendPoints、legendSpent）。
 public static class Legend {
  public const int MaxLevel=40,PointsPerLevel=2,KillBonus=3,NemesisBonus=2,Count=8;
  public const int Health=0,Melee=1,Ranged=2,Psychic=3,Recovery=4,Tough=5,Work=6,Learn=7;
  public static readonly string[] Names={"血量","近战","远程","心灵","回复","承受","工作","学习"};
  // 每点：部位生命 +2%、近战伤害 +3%、射击命中 +2%（25 格处）、心灵敏感度 +3%、伤口愈合与免疫速度各 +5%、受伤 −1.5%（直接相加，最低 5%）、工作速度 +2%、全局学习 +3%。
  public const float HealthPer=.02f,MeleePer=.03f,RangedPer=.02f,PsychicPer=.03f,RecoveryPer=.05f,ToughPer=.015f,WorkPer=.02f,LearnPer=.03f;
  // 射击命中按原版公式折算：命中率 ≈ 每格精度^距离，所以每格精度乘 (1 + 2% × 点数)^(1/25)，25 格（原版“中距离”）处命中率正好 ×(1 + 2% × 点数)。
  public const float RangedRefCells=25f;
  // 敌人：20 级门槛以上每 60 点 +1 级；剧情首领按当前威胁点数的 20% 算“每人点数”。
  public const float PointsPerLegendLevel=60f,StoryShare=.2f,StoneDropChance=.3f;
  public const string StoneDef="LP_LegendRespecStone";

  // ---------- 经验表 ----------
  // 21 级所需 = 19→20 差值 × 1.12（18592，不会比 20 级便宜），之后每级 ×1.063，使 40 级累计 ≈ 20 级的 7 倍（原规格的 ×1.12 会到 12.3 倍，见报告）。
  public const float Growth=1.063f,FirstStep=18592f;
  static int[] table;
  public static int[] Table{get{
   if(table!=null)return table;
   var t=new int[MaxLevel];for(int i=0;i<Adventurer.MaxLevel;i++)t[i]=Adventurer.XpTable[i];
   double step=FirstStep,sum=t[Adventurer.MaxLevel-1];
   for(int l=Adventurer.MaxLevel+1;l<=MaxLevel;l++){sum+=step;t[l-1]=(int)Math.Round(sum/10.0)*10;step*=Growth;}
   return table=t;
  }}
  // n 级的经验下限（1～40）。
  public static float XpFor(int level)=>Table[Mathf.Clamp(level,1,MaxLevel)-1];
  public static float Step(int level)=>level<=1?0f:XpFor(level)-XpFor(level-1);

  // ---------- 设置 ----------
  public static bool On=>Adventurer.Enabled && (LichMod.Settings?.adventurerLegend??true);
  // 敌人传奇等级：先跟随敌人等级开关；Isekai 共存模式下读另一份（默认关）。
  public static bool EnemyOnFor(Settings s,bool coexist)=>s==null || s.adventurerLegend && AdventurerEnemies.OnFor(s,coexist) && s.adventurerEnemyLegend; // 1.63.0：合并为一个开关
  public static bool EnemyOn=>Adventurer.Enabled && EnemyOnFor(LichMod.Settings,AdventurerEnemies.Coexisting);
  public static bool AutoFor(Pawn p)=>p.Faction==null || !p.Faction.IsPlayer || (LichMod.Settings?.adventurerLegendAuto??false);

  // ---------- 效果 ----------
  static int N(Hediff_Adventurer h,int i)=>h==null || h.legendSpent==null || i>=h.legendSpent.Length?0:h.legendSpent[i];
  // 1.51.0：有效点数 = 已投点数 × 种族亲和 / 凡人之志倍率（数值、门槛不变）。
  public static float E(Hediff_Adventurer h,int i)=>N(h,i)*Affinity.LegendMult(h,i);
  public static float HealthFactor(Hediff_Adventurer h)=>On?1f+HealthPer*E(h,Health):1f;
  public static float RangedFactor(float n)=>Mathf.Pow(1f+RangedPer*n,1f/RangedRefCells);
  // 受伤倍率：每点 −1.5%，直接相加；受到的伤害最低为原值的 5%（与 Isekai 体质同一下限，约 64 点封顶）。
  public const float ToughFloor=.05f;
  public static float DamageTaken(float n)=>n<=0f?1f:Mathf.Max(1f-ToughPer*n,ToughFloor);
  public static void AddStats(Hediff_Adventurer h,List<StatModifier> f,List<StatModifier> o){
   if(!On || h?.legendSpent==null)return;
   void F(string s,float v){var d=DefDatabase<StatDef>.GetNamedSilentFail(s);if(d!=null && Math.Abs(v-1f)>.0001f)f.Add(new StatModifier{stat=d,value=v});}
   void O(string s,float v){var d=DefDatabase<StatDef>.GetNamedSilentFail(s);if(d!=null && Math.Abs(v)>.0001f)o.Add(new StatModifier{stat=d,value=v});}
   F("MeleeDamageFactor",1f+MeleePer*E(h,Melee));
   F("ShootingAccuracyPawn",RangedFactor(E(h,Ranged)));
   O("PsychicSensitivity",PsychicPer*E(h,Psychic));
   F("InjuryHealingFactor",1f+RecoveryPer*E(h,Recovery));F("ImmunityGainSpeed",1f+RecoveryPer*E(h,Recovery));
   O("WorkSpeedGlobal",WorkPer*E(h,Work));
   O("GlobalLearningFactor",LearnPer*E(h,Learn));
  }
  static string Pct(float v)=>(v>=0f?"+":"")+(v*100f).ToString("0.#")+"%";
  // k = 种族亲和倍率：按有效点数 n × k 显示。
  public static string EffectText(int i,int pts,float k=1f){
   float n=pts*k;string per=Math.Abs(k-1f)>.0001f?"（亲和 ×"+k.ToString("0.##")+"）":"";
   return EffectCore(i,n)+per;
  }
  static string EffectCore(int i,float n){
   switch(i){
    case Health:return "部位生命 "+Pct(HealthPer*n)+"（与等级成长、体质、巨躯相乘）";
    case Melee:return "近战伤害 "+Pct(MeleePer*n);
    case Ranged:return "射击命中 "+Pct(RangedPer*n)+"（25 格处；每格精度 ×"+RangedFactor(n).ToString("0.####")+"）";
    case Psychic:return "心灵敏感度 "+Pct(PsychicPer*n);
    case Recovery:return "伤口愈合 "+Pct(RecoveryPer*n)+"，免疫速度 "+Pct(RecoveryPer*n);
    case Tough:return "受到伤害 "+Pct(DamageTaken(n)-1f)+"（每有效点 −1.5% 直接相加，最低承受原伤害的 5%）";
    case Work:return "全局工作速度 "+Pct(WorkPer*n);
    case Learn:return "全局学习 "+Pct(LearnPer*n);
   }
   return "";
  }
  public static string Summary(Hediff_Adventurer h){
   var sb=new StringBuilder();
   for(int i=0;i<Count;i++)if(N(h,i)>0)sb.AppendLine(Names[i]+" "+N(h,i)+"："+EffectText(i,N(h,i),Affinity.LegendMult(h,i)));
   if(sb.Length==0)sb.Append("还没有投点。");
   if(!On)sb.AppendLine().Append("“传奇等级”已在设置里关闭：传奇属性点暂时没有效果。");
   return sb.ToString().TrimEnd();
  }

  // ---------- 获得与花费 ----------
  public static void Grant(Hediff_Adventurer h,int n){if(h==null || n<=0)return;h.legendPoints+=n;if(AutoFor(h.pawn))AutoSpend(h);}
  // 成为传奇时：之前达到的 500/1000 击杀里程碑补发传奇属性点，然后结算攒下的经验。
  // 1.58.0 传奇之胚：成为传奇时 +10，500/1000 杀的奖励翻倍。
  public static int KillBonusFor(Hediff_Adventurer h)=>KillBonus*(HeroTraits.Seed(h?.pawn)?2:1);
  public static void Became(Hediff_Adventurer h){
   int bonus=HeroTraits.Seed(h.pawn)?HeroTraits.SeedLegendPoints:0;foreach(int t in new[]{500,1000})if(h.milestonesDone.Contains("kills"+t))bonus+=KillBonusFor(h);
   if(bonus>0)Grant(h,bonus);
   h.Settle();
  }
  // 自动投点：偏好的几项轮流 +1（点数少的先加）。近战职业/近战兵 近战+血量，远程兵 远程+血量，施法者 心灵+血量，其他人 工作+学习；领队再加伤害承受。
  public static Func<Pawn,string> RoleHook; // VPE 桥：殖民者的 DND 职业（"melee" / "caster"）
  public static int[] Prefs(Pawn p){
   bool npc=p.Faction==null || !p.Faction.IsPlayer;string role;
   if(npc){var r=Feats.EnemyRoles(p);role=r.Contains("caster")?"caster":r.Contains("ranged")?"ranged":"melee";}
   else role=RoleHook?.Invoke(p);
   var list=new List<int>();
   switch(role){
    case "melee":list.Add(Melee);list.Add(Health);break;
    case "ranged":list.Add(Ranged);list.Add(Health);break;
    case "caster":list.Add(Psychic);list.Add(Health);break;
    default:list.Add(Work);list.Add(Learn);break;
   }
   if(npc && AdventurerEnemies.IsLeader(p))list.Add(Tough);
   return list.ToArray();
  }
  public static int AutoSpend(Hediff_Adventurer h){
   if(h==null || h.legendPoints<=0)return 0;
   var prefs=Prefs(h.pawn);int n=0;
   while(h.legendPoints>0){int best=prefs.OrderBy(i=>h.legendSpent[i]).First();h.legendSpent[best]++;h.legendPoints--;n++;}
   h.Dirty();return n;
  }
  // 洗点：全部退回。部位生命变少时按比例缩小伤口（同设置变化的做法），免得洗点让受伤的部位直接归零。
  public static int Respec(Hediff_Adventurer h){
   if(h==null)return 0;int back=h.legendSpent.Sum();if(back<=0)return 0;
   float before=HealthFactor(h);
   for(int i=0;i<Count;i++)h.legendSpent[i]=0;h.legendPoints+=back;
   float after=HealthFactor(h);if(after<before)ScaleWounds(h.pawn,after/before);
   h.Dirty();
   if(AutoFor(h.pawn))AutoSpend(h);
   return back;
  }
  public static void ScaleWounds(Pawn p,float k){if(p?.health==null || k>=1f)return;foreach(var i in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>x.Part!=null).ToList())i.Severity*=k;}
  // 设置里关掉“传奇等级”：血量点的加成消失，伤口同样按比例缩小。
  public static bool appliedOn=true;
  public static void ApplySettings(IEnumerable<Pawn> pawns){
   bool now=On;
   if(appliedOn && !now && pawns!=null)foreach(var p in pawns.ToList()){var h=Adventurer.Get(p);if(h!=null && N(h,Health)>0)ScaleWounds(p,1f/(1f+HealthPer*E(h,Health)));}
   appliedOn=now;
  }

  // ---------- 敌人 ----------
  // 20 级门槛：1 + 点数 ÷ 45 + 加成 ≥ 20；超出部分每 60 点 +1 级。
  public static float PointsFor20(int bonus)=>Mathf.Max(0,Adventurer.MaxLevel-1-bonus)*AdventurerEnemies.PointsPerLevel;
  public static int ExtraLevels(float perPawn,int bonus)=>Mathf.Max(0,Mathf.FloorToInt((perPawn-PointsFor20(bonus))/PointsPerLegendLevel));
  // 上限：开了敌人等级上限时 = 殖民地最高等级 + N（至少 20），否则 40。
  public static int EnemyCap(){var s=LichMod.Settings;if(s==null || !s.adventurerEnemyCap)return MaxLevel;return Mathf.Clamp(AdventurerEnemies.ColonyMaxLevel()+s.adventurerEnemyCapOffset,Adventurer.MaxLevel,MaxLevel);}
  // 领队：已经到 20 级（含上限）时按超出的点数继续升。
  public static int LeaderLevel(int baseLevel,float perPawn,int bonus,int cap)=>baseLevel<Adventurer.MaxLevel?baseLevel:Mathf.Clamp(Adventurer.MaxLevel+ExtraLevels(perPawn,bonus),Adventurer.MaxLevel,Mathf.Clamp(cap,Adventurer.MaxLevel,MaxLevel));
  // 剧情首领：按当前威胁点数的 20% 当“每人点数”，加领队加成；不受上限。
  public static float StoryPoints(){var map=Find.AnyPlayerHomeMap;return map==null?0f:StorytellerUtility.DefaultThreatPointsNow(map)*StoryShare;}
  public static int StoryLevel(int baseLevel,float perPawn){
   int pl=AdventurerEnemies.BaseLevel(perPawn)+AdventurerEnemies.LeaderBonus;if(pl<Adventurer.MaxLevel)return baseLevel;
   return Mathf.Max(baseLevel,Mathf.Min(MaxLevel,Adventurer.MaxLevel+ExtraLevels(perPawn,AdventurerEnemies.LeaderBonus)));
  }
  // 敌人升到传奇等级：标为传奇（不需要里程碑），点数自动投。
  public static void RaiseEnemy(Hediff_Adventurer h,int level){
   if(h==null)return;
   if(level>Adventurer.MaxLevel && EnemyOn){h.legendary=true;h.RaiseTo(level);}
   else h.RaiseTo(Mathf.Min(level,Adventurer.MaxLevel));
  }

  // ---------- 重铸之石 ----------
  public static Thing MakeStone(){var d=DefDatabase<ThingDef>.GetNamedSilentFail(StoneDef);return d==null?null:ThingMaker.MakeThing(d);}
  public static string CannotRespec(Pawn p){
   var h=Adventurer.Get(p);if(h==null || !Adventurer.Enabled)return "没有冒险者等级。";
   if(!h.legendary)return "只有传奇冒险者能用。";
   if(h.legendSpent.Sum()<=0)return "还没有投过传奇属性点。";
   return null;
  }
 }

 public class CompUseEffect_LegendRespec : CompUseEffect {
  public override AcceptanceReport CanBeUsedBy(Pawn p){var r=Legend.CannotRespec(p);return r!=null?(AcceptanceReport)r:base.CanBeUsedBy(p);}
  public override void DoEffect(Pawn usedBy){
   base.DoEffect(usedBy);var h=Adventurer.Get(usedBy);int n=Legend.Respec(h);
   if(usedBy.Faction?.IsPlayer==true && !Adventurer.TestSuppressed)Messages.Message(usedBy.LabelShort+"握碎了传奇重铸之石：退回 "+n+" 个传奇属性点"+(h!=null && h.legendPoints>0?"，在“冒险者”分页重新投点。":"，已按设置自动重投。"),usedBy,MessageTypeDefOf.PositiveEvent,false);
  }
 }

 // 伤害承受：受伤乘系数（同半神伤害修正的写法）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class LegendToughness {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f)return;
   var h=Adventurer.Get(__instance);if(h==null || h.legendSpent==null || h.legendSpent[Legend.Tough]<=0 || !Legend.On)return;
   dinfo.SetAmount(dinfo.Amount*Legend.DamageTaken(Legend.E(h,Legend.Tough)));
  }
 }

 // ---------- 设置（画在“冒险者”里） ----------
 public partial class Settings {
  public bool adventurerLegend=true,adventurerLegendAuto=false,adventurerEnemyLegend=true;
  void ExposeLegend(){
   Scribe_Values.Look(ref adventurerLegend,"adventurerLegend",true);Scribe_Values.Look(ref adventurerLegendAuto,"adventurerLegendAuto",false);
   adventurerEnemyLegend=ExposeMergedToggle(adventurerEnemyLegend,"lpEnemyLegend163","adventurerEnemyLegend","adventurerEnemyLegendCoexist");
  }
 }
 public static class LegendSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.CheckboxLabeled("传奇等级 21～40",ref s.adventurerLegend,"20 级的冒险者经验照常累积；成为传奇冒险者（20 级 + DND 里程碑）后可以继续升到 40 级，攒下的经验立即结算。21～40 级不给属性提升和专长，每级 2 个传奇属性点（八项，无上限）；VPE 路径点每 2 级 1 点。\n传奇属性点不乘“属性效果强度”，Isekai 共存模式下照样生效。关闭后不再升传奇等级，传奇属性点暂时没有效果（点数保留）。");
   l.CheckboxLabeled("殖民者自动投传奇属性点",ref s.adventurerLegendAuto,"开启后殖民者的传奇属性点自动投：近战职业 近战 + 血量，施法职业 心灵 + 血量，其他人 工作 + 学习（需要 VPE 的 DND 职业来区分）。关闭时在“冒险者”分页手动投。敌人永远自动。");
   l.CheckboxLabeled("敌人可达传奇等级",ref s.adventurerEnemyLegend,"剧情首领、宿敌冠军和袭击领队可以到 21～40 级（自动成为传奇、自动投点）：领队的每人点数超出 20 级门槛的部分每 60 点 +1 级；剧情首领按当前威胁点数的 20% 算；宿敌冠军 = 殖民地最高等级 + 2。需要同时打开“敌人冒险者等级”。\n开不开 Isekai 共存模式都用这一个开关（1.63.0 起不再分两份）。");
  }
 }
}
