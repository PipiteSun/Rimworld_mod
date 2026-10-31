using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.52.0 通用传奇层（见 ArtSource/ChromaticDragons-1.52.0-spec.md 第二节）：豁免检定、传奇抗性、传奇动作、骇人威仪、巢穴动作框架、传奇单位的等级/显示/击杀奖励。
 // 只在主程序集，不依赖 HAR：所有配置写在 PawnKindDef（或种族 ThingDef）上的 LP_LegendaryExtension。五色真龙（Bridges/HAR）和 1.52.1 的传奇怪只写 XML。
 public class LegendaryActionEntry {public string action;public int cost=1;public int cooldown;}
 public class LP_LegendaryExtension : DefModExtension {
  public int resistance,lairBonus=1,actionPoints=3,cr,legendPoints;
  public List<LegendaryActionEntry> actions=new List<LegendaryActionEntry>();
  public float presenceRadius;
  public IntRange level=new IntRange(1,1);
  public List<int> scores;
  public bool milestone=true,deathOnDowned=true,fearImmune=true;
  public string title; // 检视面板和信件里的称号（“成年红龙”）
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(scores!=null && scores.Count!=6)yield return "LP_LegendaryExtension.scores needs 6 numbers (Str Dex Con Int Wis Cha)";
   foreach(var a in actions)if(Legendary.Worker(a.action)==null)yield return "unknown legendary action "+a.action;
  }
 }
 // 控制类 hediff 名单（传奇抗性拦截），写在 Defs/Legendary.xml。
 public class LP_LegendaryRulesDef : Def {public List<string> controlHediffs=new List<string>();}

 public static class Legendary {
  public const float ActionRadius=15f,DetectRadius=20f;
  public const int ActionRegenTicks=240,ResistDayTicks=GenDate.TicksPerDay,FearTicks=3600,FearImmuneTicks=GenDate.TicksPerDay,PanicTicks=600,PainWindowTicks=3600;
  public const float FearPanicChance=.3f;
  // 测试：设了就代替 d20。
  public static int? TestD20;public static float? TestRoll;
  public static float Roll=>TestRoll??Rand.Value;
  sealed class RefEq : IEqualityComparer<Def> {public bool Equals(Def a,Def b)=>ReferenceEquals(a,b);public int GetHashCode(Def d)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(d);}
  static readonly Dictionary<Def,LP_LegendaryExtension> cache=new Dictionary<Def,LP_LegendaryExtension>(new RefEq()); // 按引用（Def 的哈希按 shortHash，代码里造的测试兵种会撞）
  static LP_LegendaryExtension Of(Def d){if(d==null)return null;if(!cache.TryGetValue(d,out var e))cache[d]=e=d.GetModExtension<LP_LegendaryExtension>();return e;}
  public static LP_LegendaryExtension Ext(Pawn p)=>p==null?null:Of(p.kindDef)??Of(p.def);
  public static bool Is(Pawn p)=>Ext(p)!=null;
  // 2026-10-28：传奇冒险者每 5 级 1 次传奇抗性（只有抗性，不算传奇单位）。拦截补丁用 CanResist。
  public const int AdventurerLevelsPerCharge=5;
  public static int AdventurerCharges(Pawn p){var h=Adventurer.Get(p);return h!=null && h.legendary && Adventurer.Enabled?h.level/AdventurerLevelsPerCharge:0;}
  public static bool CanResist(Pawn p)=>Is(p) || p?.health!=null && Resistance(p)!=null;
  public static void SyncAdventurer(Hediff_Adventurer h){
   var p=h.pawn;if(p==null || p.Dead || Is(p) || Items.Hediff("LP_LegendaryResistance")==null)return;
   var r=Resistance(p);int max=AdventurerCharges(p);
   if(max<=0){if(r!=null)p.health.RemoveHediff(r);return;}
   if(r==null){r=(Hediff_LegendaryResistance)Items.Give(p,"LP_LegendaryResistance");r?.Refill();}
  }
  static LP_LegendaryRulesDef rules;public static LP_LegendaryRulesDef Rules=>rules??(rules=DefDatabase<LP_LegendaryRulesDef>.GetNamedSilentFail("LP_LegendaryRules"));
  public static bool IsControl(HediffDef d)=>d!=null && Rules!=null && Rules.controlHediffs.Contains(d.defName);

  // ---------- 豁免 ----------
  public static int Proficiency(int level)=>level<=0?0:level<=Adventurer.MaxLevel?2+(level-1)/4:6+(level-Adventurer.MaxLevel)/8;
  public static int LevelOf(Pawn p){var h=Adventurer.Get(p);if(h!=null)return h.level;var e=Ext(p);return e==null?0:e.level.min;}
  // 固定六维（传奇单位）优先；没有冒险者等级的人按 10（调整值 0）。
  public static int Score(Pawn p,int a){
   var e=Ext(p);if(e?.scores!=null && e.scores.Count==6 && a>=0 && a<6)return e.scores[a];
   var h=Adventurer.Get(p);return h==null || a<0 || a>5?10:h.Eff(a);
  }
  public static int Mod(Pawn p,int a)=>Adventurer.Mod(Score(p,a));
  public static int DC(Pawn caster,int a)=>8+Proficiency(LevelOf(caster))+Mod(caster,a);
  // 对某个来源的额外豁免加值（火巨人 / 吉斯洋基对红龙、霜巨人对白龙 +2，见 ChromaticDragons.SaveBonus）。
  public static Func<Pawn,Pawn,int,int> SaveBonusHook;
  public static int SaveBonus(Pawn p,int a,Pawn source)=>Mod(p,a)+Proficiency(Adventurer.Get(p)?.level??0)+(SaveBonusHook?.Invoke(p,source,a)??0)-Warlock.SaveBonusPenalty(p); // 邪术师咒术：成功率 −15%
  public static int D20()=>TestD20??Rand.RangeInclusive(1,20);
  public static int lastRoll;
  // 1.59.0 热修（去 d20 措辞）：对玩家只显示成功率。和 Save 的判定同一个概率：调整值、熟练加值、额外加值每点 +5%，难度每点 −5%，限 0～100%（不含幸运再试）。
  public static float SaveChance(int modifier,int proficiency,int bonus,int dc)=>Mathf.Clamp01((21-dc+modifier+proficiency+bonus)/20f);
  public static float SaveChance(Pawn p,int a,int dc,Pawn source=null)=>p==null?0f:Mathf.Clamp01((21-dc+SaveBonus(p,a,source))/20f);
  public static string SaveChanceText(Pawn p,int a,int dc,Pawn source=null)=>"成功率 "+SaveChance(p,a,dc,source).ToStringPercent();  // 判定：调整值 + 熟练 + 1～20 的随机数 ≥ 难度 成功；失败时「幸运」专长可以重掷一次（用掉一次幸运）。成功飘“豁免！”。
  public static bool Save(Pawn p,int a,int dc,Pawn source=null){
   if(p==null)return false;int bonus=SaveBonus(p,a,source);
   int r=lastRoll=D20();bool ok=r+bonus>=dc;
   if(!ok && Feats.Has(p,Feats.Lucky) && Feats.Use(p,Feats.Lucky)){r=lastRoll=D20();ok=r+bonus>=dc;if(ok)Items.Mote(p,"幸运！",new Color(.95f,.85f,.45f));}
   if(ok)Items.Mote(p,"豁免！",new Color(.75f,.9f,1f));
   return ok;
  }
  public static string AbilityLabel(int a)=>a>=0 && a<6?Adventurer.Names[a]:"?";

  // ---------- 恐惧免疫（统一入口） ----------
  public static bool FearImmune(Pawn p)=>p==null || p.RaceProps==null || p.RaceProps.IsMechanoid || Loxodon.FearImmune(p) || Items.Has(p,"LP_PotionHeroism") || Items.Undead(p) || Ext(p)?.fearImmune==true
   || FearImmuneHook!=null && FearImmuneHook(p);
  public static Func<Pawn,bool> FearImmuneHook;

  // ---------- 传奇单位生成后的统一处理 ----------
  public static void Setup(Pawn p){
   var e=Ext(p);if(e==null || p.health==null)return;
   if(p.story?.traits!=null)foreach(var t in p.story.traits.allTraits.Where(t=>HeroTraits.IsSlot(t.def) || t.def.exclusionTags!=null && t.def.exclusionTags.Contains("LP_PointTalent")).ToList())p.story.traits.RemoveTrait(t);
   var h=Adventurer.Get(p)??Adventurer.TryAdd(p,1,true);
   if(h!=null){
    h.storyLevel=true;h.pendingASI=0;
    int level=e.level.RandomInRange;
    if(level>Adventurer.MaxLevel){h.legendary=true;h.RaiseTo(level);}else h.RaiseTo(level); // 固定等级：不走殖民地上限，也不看“敌人传奇等级”开关
    if(e.scores!=null && e.scores.Count==6)h.scores=e.scores.ToArray();
    // 传奇属性点全投近战：生命倍率已写死在 XML（冒险者部位生命、传奇血量对传奇单位不生效），承受也不再叠在护甲上。
    if(h.legendary){int pts=h.legendSpent.Sum()+h.legendPoints;h.legendSpent=new int[Legend.Count];h.legendSpent[Legend.Melee]=pts;h.legendPoints=0;}
    h.pendingASI=0;h.Dirty();
   }
   if(e.resistance>0 && Items.Hediff("LP_LegendaryResistance")!=null && !Items.Has(p,"LP_LegendaryResistance")){var r=(Hediff_LegendaryResistance)Items.Give(p,"LP_LegendaryResistance");r?.Refill();}
   if(e.actions.Count>0 && Items.Hediff("LP_LegendaryActions")!=null && !Items.Has(p,"LP_LegendaryActions")){var a=(Hediff_LegendaryActions)Items.Give(p,"LP_LegendaryActions");if(a!=null)a.points=e.actionPoints;}
   if(e.deathOnDowned)p.health.overrideDeathOnDownedChance=1f;
   IsekaiLock(p);
  }
  // 装了 Isekai：锁住（不被它的袭击重掷等级），见 IsekaiComponent.forgeLocked。
  public static bool IsekaiLock(Pawn p){
   if(p?.AllComps==null)return false;
   foreach(var c in p.AllComps){if(c.GetType().Name!="IsekaiComponent")continue;var f=AccessTools.Field(c.GetType(),"forgeLocked");if(f==null)return false;f.SetValue(c,true);return true;}
   return false;
  }
  public static bool IsekaiLocked(Pawn p){
   if(p?.AllComps==null)return false;
   foreach(var c in p.AllComps)if(c.GetType().Name=="IsekaiComponent"){var f=AccessTools.Field(c.GetType(),"forgeLocked");return f!=null && (bool)f.GetValue(c);}
   return false;
  }

  // ---------- 传奇抗性 ----------
  public static Hediff_LegendaryResistance Resistance(Pawn p){var d=Items.Hediff("LP_LegendaryResistance");return d==null?null:p?.health?.hediffSet?.GetFirstHediffOfDef(d) as Hediff_LegendaryResistance;}
  public static Hediff_LegendaryActions Actions(Pawn p){var d=Items.Hediff("LP_LegendaryActions");return d==null?null:p?.health?.hediffSet?.GetFirstHediffOfDef(d) as Hediff_LegendaryActions;}
  [ThreadStatic] public static bool bypass; // 自己的规则（真龙之躯、测试）加状态时不拦
  public static int Resisted; // 自检计数
  public static bool Resist(Pawn p,string what){
   if(bypass || p==null || p.Dead)return false;var r=Resistance(p);if(r==null || !r.Use())return false;
   Resisted++;Items.Mote(p,"传奇抗性！（"+what+"）",new Color(1f,.75f,.3f));return true;
  }
  public static readonly Dictionary<Pawn,int> painWindow=new Dictionary<Pawn,int>();
  public static Func<Pawn,bool> InOwnLairHook;
  public static bool InOwnLair(Pawn p)=>InOwnLairHook!=null && InOwnLairHook(p);

  // ---------- 传奇动作 ----------
  static Dictionary<string,LegendaryActionWorker> workers;
  public static LegendaryActionWorker Worker(string key){
   // 1.63.0：传奇怪的六个动作也放进初始表。原来它们在 StaticConstructorOnStartup 里才登记，而定义检查（开发者模式）更早，会报“unknown legendary action”。
   if(workers==null)workers=new Dictionary<string,LegendaryActionWorker>{{"Detect",new LA_Detect()},{"Tail",new LA_Tail()},{"Wing",new LA_Wing()},{"Glance",new LA_Glance()},
    {"Ray",new LA_Ray()},{"Drift",new LA_Drift()},{"Hellfire",new LA_Hellfire()},{"UndeadCommand",new LA_UndeadCommand()},{"SoulDrain",new LA_SoulDrain()},{"Curse",new LA_Curse()}};
   return key!=null && workers.TryGetValue(key,out var w)?w:null;
  }
  public static void Register(string key,LegendaryActionWorker w){Worker("Detect");workers[key]=w;}
  public static int ActionsDone;
  // 敌人完成一次攻击：15 格内的传奇单位挑一个能用的动作立刻执行（不打断它自己的工作）。
  public static LegendaryActionEntry React(Pawn legend,Pawn enemy){
   if(legend==null || enemy==null || legend.Dead || legend.Downed || !legend.Spawned || legend.Map!=enemy.Map || !enemy.HostileTo(legend) || legend.Position.DistanceTo(enemy.Position)>ActionRadius)return null;
   if(legend.stances?.stunner?.Stunned==true)return null;
   var h=Actions(legend);var e=Ext(legend);if(h==null || e==null)return null;
   foreach(var a in e.actions){
    var w=Worker(a.action);if(w==null || h.points<a.cost || h.OnCooldown(a.action))continue;
    if(!w.CanDo(legend,enemy))continue;
    if(!w.Do(legend,enemy))continue;
    h.points-=a.cost;h.Used(a.action,a.cooldown);ActionsDone++;
    Items.Mote(legend,"传奇动作："+w.Label,new Color(1f,.6f,.3f));
    return a;
   }
   return null;
  }
  public static IEnumerable<Pawn> EnemiesNear(Pawn p,float r)=>p?.Map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(p.Map,p.Position,r).Where(x=>x!=p && !x.Dead && x.HostileTo(p));
  // 翅膀 / 尾巴：按部位标签（LP_DragonWing / Tail）。
  public static bool HasPart(Pawn p,string defName)=>p?.health?.hediffSet?.GetNotMissingParts().Any(x=>x.def.defName==defName)==true;
  public static int PartCount(Pawn p,string defName)=>p?.health?.hediffSet?.GetNotMissingParts().Count(x=>x.def.defName==defName)??0;
  public static bool CanFly(Pawn p)=>PartCount(p,"LP_DragonWing")>0;
  public static bool HasTail(Pawn p)=>HasPart(p,"Tail");

  // ---------- 骇人威仪 ----------
  public static int Frightened;
  public static bool Presence(Pawn legend,Pawn p,MapComponent_Legendary mc){
   if(FearImmune(p) || HeroCombat.FearImmuneTo(p,legend))return false; // 1.58.0 屠龙者免疫龙威、宿敌克星免疫宿敌
   int dc=DC(legend,Adventurer.Cha);
   if(Save(p,Adventurer.Wis,dc,legend)){mc?.Immune(legend,p,FearImmuneTicks);return false;}
   var h=Items.Give(p,"LP_Frightened",FearTicks);if(h==null)return false;
   mc?.Immune(legend,p,FearTicks+FearImmuneTicks);Frightened++;
   Items.Mote(p,"惊惧！",new Color(.9f,.5f,.5f));
   if(p.RaceProps.Humanlike && !p.Drafted && Roll<FearPanicChance && p.mindState?.mentalStateHandler!=null){
    if(p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"骇人威仪",forced:true,forceWake:true) && p.MentalState!=null)p.MentalState.forceRecoverAfterTicks=PanicTicks;
   }
   return true;
  }

  // ---------- 显示 ----------
  public static string InspectLine(Pawn p){
   var e=Ext(p);if(e==null)return null;
   var h=Adventurer.Get(p);
   return "挑战等级 CR "+e.cr+(h!=null?" · 冒险者等级 "+h.level:"");
  }

  // ---------- 击杀奖励 ----------
  public const float MilestoneXp=500f,LegendShare=.1f;
  public static Func<Pawn,string> TitleHook;
  public static string Title(Pawn p)=>TitleHook?.Invoke(p)??Ext(p)?.title??p?.kindDef?.label;
  public static int Rewarded;
  public static void OnKilled(Pawn victim,Dictionary<Pawn,float> dmg,Pawn killer,Map map){
   var e=Ext(victim);if(e==null)return;
   if(e.milestone && map!=null)Adventurer.MilestoneMap(map,"lp_slay_"+victim.ThingID,MilestoneXp);
   if(e.legendPoints>0 && dmg!=null){
    float total=dmg.Values.Sum();
    if(total>0f)foreach(var kv in dmg)if(kv.Value/total>=LegendShare-.0001f){var h=Adventurer.Get(kv.Key);if(h!=null && h.legendary && kv.Key.Faction!=null && kv.Key.Faction.IsPlayer){Legend.Grant(h,e.legendPoints);Rewarded++;}}
   }
   if(killer!=null && killer.Faction!=null && killer.Faction.IsPlayer && e.milestone){
    var rec=DefDatabase<RecordDef>.GetNamedSilentFail("LP_DragonsSlain");if(rec!=null && killer.records!=null && SlayRecordHook?.Invoke(victim)!=false)killer.records.Increment(rec);
    var tale=DefDatabase<TaleDef>.GetNamedSilentFail("LP_SlewLegend");if(tale!=null)try{TaleRecorder.RecordTale(tale,killer,victim);}catch(Exception x){Log.ErrorOnce("[DND边缘大陆] 传奇击杀传说："+x.Message,74152001);}
   }
  }
  public static Func<Pawn,bool> SlayRecordHook;
 }

 // ---------- 传奇抗性 hediff：名字里显示次数，每游戏日恢复满，巢穴 +1 ----------
 public class Hediff_LegendaryResistance : HediffWithComps {
  public int charges=-1,lastRefill=-1;
  public int lastMax=-1;
  public int Max{get{var e=Legendary.Ext(pawn);return e==null?Legendary.AdventurerCharges(pawn):e.resistance+(Legendary.InOwnLair(pawn)?e.lairBonus:0);}}
  public void Refill(){charges=lastMax=Max;lastRefill=Find.TickManager?.TicksGame??0;}
  public bool Use(){if(charges<0)Refill();if(charges<=0)return false;charges--;return true;}
  public override string LabelBase=>charges<=0 && charges!=-1?"传奇抗性耗尽":"传奇抗性 "+Mathf.Max(0,charges)+"/"+Max;
  public override string TipStringExtra{get{
   int left=Legendary.ResistDayTicks-(Find.TickManager.TicksGame-lastRefill);
   return (base.TipStringExtra??"")+"\n眩晕、外力造成的精神状态、控制类状态、疼痛倒地：每次消耗 1 次，效果完全不生效。用完后控制正常生效。\n恢复满还要 "+(Mathf.Max(0,left)/(float)GenDate.TicksPerHour).ToString("0.#")+" 小时。";
  }}
  public override void TickInterval(int delta){
   base.TickInterval(delta);if(pawn.IsHashIntervalTick(250,delta))Sync();
  }
  public void Sync(){
   if(charges<0 || Find.TickManager.TicksGame-lastRefill>=Legendary.ResistDayTicks)Refill();
   else{int m=Max;if(lastMax>=0 && m>lastMax)charges+=m-lastMax;lastMax=m;if(charges>m)charges=m;} // 升级多出来的次数马上可用
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref charges,"lpCharges",-1);Scribe_Values.Look(ref lastRefill,"lpLastRefill",-1);Scribe_Values.Look(ref lastMax,"lpLastMax",-1);}
 }
 // ---------- 传奇点：每 4 秒恢复 1 点 ----------
 public class Hediff_LegendaryActions : HediffWithComps {
  public float points;public Dictionary<string,int> cooldown=new Dictionary<string,int>();int regen;
  public int Max=>Legendary.Ext(pawn)?.actionPoints??0;
  public bool OnCooldown(string a)=>cooldown.TryGetValue(a,out int t) && Find.TickManager.TicksGame<t;
  public void Used(string a,int cd){if(cd>0)cooldown[a]=Find.TickManager.TicksGame+cd;}
  public override string LabelBase=>"传奇点 "+Mathf.FloorToInt(points)+"/"+Max;
  public override string TipStringExtra=>(base.TipStringExtra??"")+"\n15 格内的敌人每完成一次攻击，它就用掉传奇点做一个传奇动作（侦测、尾击、振翼……），不打断自己的攻击。每 4 秒恢复 1 点。";
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((regen+=delta)<Legendary.ActionRegenTicks)return;regen=0;
   if(points<Max)points=Mathf.Min(Max,points+1f);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref points,"lpPoints");Scribe_Collections.Look(ref cooldown,"lpCooldown",LookMode.Value,LookMode.Value);if(cooldown==null)cooldown=new Dictionary<string,int>();}
 }

 // ---------- 传奇动作 ----------
 public abstract class LegendaryActionWorker {
  public abstract string Label{get;}
  public virtual bool CanDo(Pawn legend,Pawn enemy)=>true;
  public abstract bool Do(Pawn legend,Pawn enemy);
 }
 // 侦测：揭示 20 格内隐身的敌人。
 public class LA_Detect : LegendaryActionWorker {
  public override string Label=>"侦测";
  static IEnumerable<Pawn> Hidden(Pawn l)=>Legendary.EnemiesNear(l,Legendary.DetectRadius).Where(p=>p.IsPsychologicallyInvisible());
  public override bool CanDo(Pawn l,Pawn e)=>Hidden(l).Any();
  public override bool Do(Pawn l,Pawn e){
   int n=0;
   foreach(var p in Hidden(l).ToList()){
    foreach(var name in new[]{"LP_PotionInvisibility","LP_ShadowVeil"})Items.Remove(p,name);
    p.GetInvisibilityComp()?.BecomeVisible(true);n++;Items.Mote(p,"被发现了！",new Color(1f,.6f,.3f));
   }
   return n>0;
  }
 }
 // 尾击：2 格内一个敌人；敏捷豁免失败再推开 1 格；尾巴断了不能用。
 public class LA_Tail : LegendaryActionWorker {
  public override string Label=>"尾击";
  public static Func<Pawn,float> TailDamage=p=>20f;
  public override bool CanDo(Pawn l,Pawn e)=>Legendary.HasTail(l) && Legendary.EnemiesNear(l,2.9f).Any();
  public override bool Do(Pawn l,Pawn e){
   var t=e.Position.DistanceTo(l.Position)<=2.9f?e:Legendary.EnemiesNear(l,2.9f).OrderBy(p=>p.Position.DistanceTo(l.Position)).FirstOrDefault();if(t==null)return false;
   t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,TailDamage(l),.3f,(t.Position-l.Position).AngleFlat,l));
   if(!t.Dead && t.Spawned && !Legendary.Save(t,Adventurer.Dex,Legendary.DC(l,Adventurer.Str),l)){var dest=DNDPush.Destination(t.Map,t.Position,(t.Position-l.Position).ToVector3(),1);if(dest!=t.Position)DNDPush.Move(t,dest);}
   return true;
  }
 }
 // 振翼：3 格内所有敌人敏捷豁免，失败受翼击并倒地 2 秒；然后飞到 8 格内的一点。两翼都断了不能用。
 public class LA_Wing : LegendaryActionWorker {
  public override string Label=>"振翼";
  public static Func<Pawn,float> WingDamage=p=>15f;
  public const int StunTicks=120;public const float Radius=3.9f,Hop=8f;
  public override bool CanDo(Pawn l,Pawn e)=>Legendary.CanFly(l) && Legendary.EnemiesNear(l,Radius).Any();
  public override bool Do(Pawn l,Pawn e){
   int dc=Legendary.DC(l,Adventurer.Str);
   foreach(var p in Legendary.EnemiesNear(l,Radius).ToList()){
    if(Legendary.Save(p,Adventurer.Dex,dc,l))continue;
    p.TakeDamage(new DamageInfo(DamageDefOf.Blunt,WingDamage(l),.2f,(p.Position-l.Position).AngleFlat,l));
    if(!p.Dead)p.stances?.stunner?.StunFor(StunTicks,l,false,true,false);
   }
   FleckMaker.Static(l.Position,l.Map,FleckDefOf.PsycastAreaEffect,4f);
   ChromaticFlight.Hop(l,Hop,e?.Position??l.Position);
   return true;
  }
 }
 // 骇人一瞥（远古）：对 15 格内一个已经豁免过威仪的人重新做一次威仪豁免。
 public class LA_Glance : LegendaryActionWorker {
  public override string Label=>"骇人一瞥";
  static IEnumerable<Pawn> Targets(Pawn l){var mc=MapComp<MapComponent_Legendary>.Get(l.Map);return Legendary.EnemiesNear(l,Legendary.ActionRadius).Where(p=>mc!=null && mc.Checked(l,p) && !Items.Has(p,"LP_Frightened") && !Legendary.FearImmune(p));}
  public override bool CanDo(Pawn l,Pawn e)=>Legendary.Ext(l)?.presenceRadius>0 && Targets(l).Any();
  public override bool Do(Pawn l,Pawn e){var t=Targets(l).OrderBy(p=>p.Position.DistanceTo(l.Position)).FirstOrDefault();if(t==null)return false;Legendary.Presence(l,t,MapComp<MapComponent_Legendary>.Get(l.Map));return true;}
 }
 // 飞跃 / 振翼后的落点：原版跳跃式 PawnFlyer（越过墙）。
 public static class ChromaticFlight {
  public static bool Hop(Pawn p,float range,IntVec3 toward){
   if(p?.Map==null || !p.Spawned)return false;var map=p.Map;
   var flyer=DefDatabase<ThingDef>.GetNamedSilentFail("PawnFlyer");if(flyer==null)return false;
   var cells=GenRadial.RadialCellsAround(p.Position,range,false).Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null && c.DistanceTo(p.Position)>=3f).ToList();
   if(cells.Count==0)return false;
   var dest=cells.OrderBy(c=>c.DistanceTo(toward)+Rand.Value*3f).First();
   try{var f=PawnFlyer.MakeFlyer(flyer,p,dest,null,null);if(f==null)return false;GenSpawn.Spawn(f,p.Position,map);return true;}
   catch(Exception e){Log.WarningOnce("[DND边缘大陆] 真龙飞跃失败："+e.Message,74152002);return false;}
  }
 }

 // ---------- 地图组件：传奇单位计数、骇人威仪 ----------
 public class MapComponent_Legendary : MapComponent {
  public List<Pawn> legends=new List<Pawn>(),beholders=new List<Pawn>(),amulets=new List<Pawn>(); // 1.52.1：眼魔（反魔法锥）、反魔法护符佩戴者的缓存
  // 每条龙对每个人：豁免 / 惊惧之后的免疫到期时间（不存档：读档后重新判定一次）。
  readonly Dictionary<int,Dictionary<int,int>> seen=new Dictionary<int,Dictionary<int,int>>();
  public MapComponent_Legendary(Map map):base(map){}
  public int Count=>legends.Count;
  public void Refresh(){legends.Clear();beholders.Clear();amulets.Clear();foreach(var p in map.mapPawns.AllPawnsSpawned){if(p.Dead)continue;if(Legendary.Is(p)){legends.Add(p);if(Monsters.Is(p,Monsters.Beholder))beholders.Add(p);}if(Monsters.WearsAmulet(p))amulets.Add(p);}}
  public bool Checked(Pawn l,Pawn p)=>seen.TryGetValue(l.thingIDNumber,out var d) && d.ContainsKey(p.thingIDNumber);
  public bool ImmuneTo(Pawn l,Pawn p)=>seen.TryGetValue(l.thingIDNumber,out var d) && d.TryGetValue(p.thingIDNumber,out int t) && Find.TickManager.TicksGame<t;
  public void Immune(Pawn l,Pawn p,int ticks){if(!seen.TryGetValue(l.thingIDNumber,out var d))seen[l.thingIDNumber]=d=new Dictionary<int,int>();d[p.thingIDNumber]=Find.TickManager.TicksGame+ticks;}
  public void Forget(){seen.Clear();}
  public int PresenceTick(){
   int n=0;
   foreach(var l in legends){
    var e=Legendary.Ext(l);if(e==null || e.presenceRadius<=0f || l.Dead || l.Downed || !l.Spawned || l.InMentalState && l.MentalStateDef==MentalStateDefOf.PanicFlee)continue;
    foreach(var p in Legendary.EnemiesNear(l,e.presenceRadius).ToList()){
     if(p.Faction==null || !p.Faction.IsPlayer || ImmuneTo(l,p) || Checked(l,p) && Items.Has(p,"LP_Frightened"))continue;
     if(!GenSight.LineOfSight(l.Position,p.Position,map,true))continue;
     if(Legendary.Presence(l,p,this))n++;
    }
   }
   return n;
  }
  public override void MapComponentTick(){
   int now=Find.TickManager.TicksGame;
   if((now+map.uniqueID)%60!=0)return;
   Refresh();if(legends.Count==0)return;
   try{PresenceTick();}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 骇人威仪出错："+e,74152003);}
  }
 }

 // ---------- 拦截：眩晕、外力精神状态、控制类状态、疼痛倒地 ----------
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.StunFor))]
 public static class LegendaryStun {
  static bool Prefix(StunHandler __instance,int ticks){
   var p=__instance.parent as Pawn;if(ticks<=0 || p==null || !Legendary.CanResist(p))return true;
   return !Legendary.Resist(p,"眩晕");
  }
 }
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class LegendaryMental {
  static bool Prefix(MentalStateDef stateDef,bool causedByMood,Pawn ___pawn,ref bool __result){
   if(causedByMood || stateDef==null || ___pawn==null || !Legendary.CanResist(___pawn))return true;
   if(!Legendary.Resist(___pawn,stateDef.label))return true;
   __result=false;return false;
  }
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.AddHediff),new[]{typeof(Hediff),typeof(BodyPartRecord),typeof(DamageInfo?),typeof(DamageWorker.DamageResult)})]
 public static class LegendaryControl {
  static bool Prefix(Hediff hediff,Pawn ___pawn){
   if(hediff==null || ___pawn==null || !Legendary.IsControl(hediff.def) || !Legendary.CanResist(___pawn))return true;
   return !Legendary.Resist(___pawn,hediff.def.label);
  }
 }
 // 疼痛休克倒地：只在真正的状态检查里消耗（WouldBeDowned 之类的预判不消耗），每次撑 60 秒。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.CheckForStateChange))]
 public static class LegendaryStateCheck {
  [ThreadStatic] public static bool active;
  static void Prefix(out bool __state){__state=active;active=true;}
  static void Finalizer(bool __state){active=__state;}
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.ShouldBeDowned))]
 public static class LegendaryPainDowned {
  static void Postfix(Pawn_HealthTracker __instance,Pawn ___pawn,ref bool __result){
   if(!__result || ___pawn==null || !__instance.InPainShock || !Legendary.CanResist(___pawn))return;
   // 只拦疼痛：意识过低、移动能力为 0（部位缺失）照常倒地。
   if(!__instance.capacities.CanBeAwake || !__instance.capacities.CapableOf(PawnCapacityDefOf.Moving))return;
   int now=Find.TickManager.TicksGame;
   if(Legendary.painWindow.TryGetValue(___pawn,out int until) && now<until){__result=false;return;}
   if(!LegendaryStateCheck.active)return;
   if(Legendary.Resist(___pawn,"疼痛")){Legendary.painWindow[___pawn]=now+Legendary.PainWindowTicks;__result=false;}
  }
 }
 // 倒地即死（不可俘虏、不可招募）。
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class LegendaryDeathOnDowned {
  static void Postfix(Pawn ___pawn,DamageInfo? dinfo){
   var p=___pawn;if(p==null || p.Dead || Legendary.Ext(p)?.deathOnDowned!=true)return;
   Fiends.Defer(()=>{if(!p.Dead && p.Downed)p.Kill(dinfo);});
  }
 }
 // 敌人完成一次攻击后触发传奇动作。先看地图上有没有传奇单位。
 [HarmonyPatch(typeof(Verb),"TryCastNextBurstShot")]
 public static class LegendaryActionTrigger {
  static void Postfix(Verb __instance){
   var c=__instance.CasterPawn;if(c?.Map==null || Legendary.Is(c))return;
   var mc=MapComp<MapComponent_Legendary>.Get(c.Map);if(mc==null || mc.Count==0)return;
   try{foreach(var l in mc.legends.ToList())if(l!=c && l.HostileTo(c))Legendary.React(l,c);}
   catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 传奇动作出错："+e,74152004);}
  }
 }
 // 生成：带传奇扩展的兵种（含开发者模式生成）统一处理。
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class LegendaryGenerated {
  [HarmonyPriority(Priority.Last-1)]
  static void Postfix(Pawn __result){if(__result!=null && !__result.Dead && Legendary.Is(__result))Legendary.Setup(__result);}
 }
 // 击杀：伤害分成在 AdventurerKill 后缀里会被清掉，这里先拷一份。
 public class LegendaryKillState {public Dictionary<Pawn,float> dmg;public Pawn killer;public Map map;public IntVec3 at;public bool flayer;}
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class LegendaryKill {
  [HarmonyPriority(Priority.First)]
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out LegendaryKillState __state){
   __state=null;if(__instance.Dead)return;
   var st=Adventurer.State;Dictionary<Pawn,float> copy=null;
   if(st!=null && st.damage.TryGetValue(__instance,out var rec))copy=new Dictionary<Pawn,float>(rec);
   var k=dinfo?.Instigator as Pawn;
   if(k!=null && dinfo.HasValue){if(copy==null)copy=new Dictionary<Pawn,float>();copy.TryGetValue(k,out float d);copy[k]=d+dinfo.Value.Amount;}
   __state=new LegendaryKillState{dmg=copy,killer=k,map=__instance.MapHeld,at=__instance.PositionHeld};
  }
  static void Postfix(Pawn __instance,LegendaryKillState __state){
   if(__state==null || !__instance.Dead)return;
   if(__state.flayer){Monsters.OnFlayerLeaderKilled(__instance,__state.killer);return;}
   Legendary.OnKilled(__instance,__state.dmg,__state.killer,__state.map);
   Monsters.OnKilled(__instance,__state.map,__state.at); // 1.52.1 传奇怪掉落
   if(Legendary.Is(__instance))Growth.LegendDrop(__instance,__state.map,__state.at); // 1.58.3 15% 属性典籍、10% 传承武器
  }
 }
 // Isekai 的“威压”NPC 光环：传奇单位不放（由传奇动作取代）。找不到这个方法就不补。
 [HarmonyPatch]
 public static class LegendaryIsekaiAura {
  static System.Reflection.MethodBase target;
  static bool Prepare(){var t=AccessTools.TypeByName("IsekaiLeveling.Abilities.AuraPressureAIComponent");target=t==null?null:AccessTools.Method(t,"IsEligibleCaster");return target!=null;}
  static System.Reflection.MethodBase TargetMethod()=>target;
  static void Postfix(Pawn __0,ref bool __result){if(__result && Legendary.Is(__0))__result=false;}
 }
}
