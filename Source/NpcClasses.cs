using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // NPC 职业（见 ArtSource/WarlockNpc-spec.md）：不属于玩家的人形冒险者第一次 250 刻时（新生成、旧存档里已经在地图上的都一样，懒处理）按派系风格 + 最高技能拿职业：
 // 一个职业（20% 两个，第二个分 1/3 等级），职业等级合计 = 冒险者等级（最多 20）；到子职业等级随机选子职业和分支；冒险者 25 级起学传奇职业能力；资源满。
 // 以后冒险者等级涨了，新的职业点自动加到主职业上。战斗中每 250 刻最多放一个职业能力（敌人施放职业能力），治疗职业优先救倒地、受伤的友军。
 [StaticConstructorOnStartup]
 public static class NpcClasses {
  public const float SecondChance=.2f,AllyRadius=1.9f;public const int CombatGap=600,SelfRange=8;
  public static bool TestSuppressed; // 自测进程：只有职业检查组自己临时打开（别的检查组里的测试敌人不会长职业、放技能）
  static NpcClasses(){if(GenCommandLine.CommandLineArgPassed("lichtest"))TestSuppressed=true;}
  public static bool On=>Adventurer.Enabled && (LichMod.Settings?.npcClasses??true) && !TestSuppressed;
  public static bool CastOn=>(LichMod.Settings?.npcClassCasting??true) && !TestSuppressed;
  public static bool Eligible(Pawn p)=>p!=null && !p.Dead && p.RaceProps.Humanlike && (p.Faction==null || !p.Faction.IsPlayer) && !p.IsPrisonerOfColony && !p.IsSlaveOfColony;

  // ClassSystem.Tick250 的第一步（玩家的人、已经处理过又没有新职业点的人直接返回）。
  public static void Tick(Hediff_Adventurer h){
   var p=h.pawn;if(p==null || p.Dead || p.Faction!=null && p.Faction.IsPlayer)return;
   var d=h.classes;
   if(d==null || !d.npc && d.levels.Count==0){if(On && Eligible(p))Assign(h);}
   else if(d.npc && On && ClassSystem.Points(h)>0)Grow(h);
   if(h.classes!=null && h.classes.levels.Count>0 && CastOn)TryCast(p);
  }

  // ---------- 选职业 ----------
  static LP_ClassDef C(string n)=>DefDatabase<LP_ClassDef>.GetNamedSilentFail("LP_Class_"+n);
  // 派系风格：部落 → 野蛮人 / 德鲁伊 / 游侠；海盗、亡命之徒 → 游荡者 / 战士 / 邪术师；帝国 → 圣武士 / 战士 / 法师 / 牧师；塞伊、亡灵 → 法师 / 邪术师 / 牧师；其他按最高技能。
  public static List<LP_ClassDef> Flavor(Faction f,PawnKindDef kind=null){
   string[] names=null;var n=f?.def?.defName??"";var k=kind?.defName??"";
   if(k.Contains("Thay") || n.Contains("Thay") || n.Contains("Larloch") || n.Contains("Undead") || n.Contains("Lich") || n.Contains("Necro"))names=new[]{"Wizard","Warlock","Cleric"};
   else if(f==null)return null;
   else if(n.Contains("Empire"))names=new[]{"Paladin","Fighter","Wizard","Cleric"};
   else if(n.Contains("Pirate") || n.Contains("Outlaw") || n.Contains("Bandit") || n=="LP_BlackNetwork")names=new[]{"Rogue","Fighter","Warlock"};
   else if(f.def.techLevel<=TechLevel.Neolithic || n.Contains("Tribe"))names=new[]{"Barbarian","Druid","Ranger"};
   return names?.Select(C).Where(c=>c!=null).ToList();
  }
  public static LP_ClassDef Pick(Pawn p,LP_ClassDef exclude=null){
   bool Ok(LP_ClassDef c)=>c!=exclude && ClassSystem.Forbidden(c,p)==null;
   var flavor=Flavor(p.Faction,p.kindDef)?.Where(Ok).ToList();
   if(flavor!=null && flavor.Count>0)return flavor.RandomElementByWeight(c=>1f+c.SkillLevel(p)/4f);
   var all=ClassSystem.All.Where(Ok).ToList();if(all.Count==0)return null;
   int best=all.Max(c=>c.SkillLevel(p));
   return all.Where(c=>c.SkillLevel(p)==best).RandomElement();
  }
  // 两个职业时第二个分 1/3（至少 1）。
  public static (int first,int second) Split(int budget,bool two)=>!two || budget<2?(budget,0):(budget-Mathf.Max(1,budget/3),Mathf.Max(1,budget/3));
  public static bool Assign(Hediff_Adventurer h,float roll=-1f){
   var p=h.pawn;if(h.classes==null)h.classes=new ClassData();var d=h.classes;
   d.npc=true;int budget=ClassSystem.Budget(h);if(budget<=0)return false;
   var first=Pick(p);if(first==null)return false;
   if(roll<0f)roll=Rand.Value;
   var second=budget>=2 && roll<SecondChance?Pick(p,first):null;
   var (l1,l2)=Split(budget,second!=null);
   ClassSystem.SetLevel(h,first,l1);if(second!=null)ClassSystem.SetLevel(h,second,l2);
   Finish(h);
   return true;
  }
  // 新的职业点加到主职业（主职业满 20 就加到别的已学职业）。
  public static void Grow(Hediff_Adventurer h){
   int pts=ClassSystem.Points(h);var main=ClassSystem.MainClass(h.pawn);
   for(int i=0;i<pts;i++){var c=ClassSystem.Level(h,main)<ClassSystem.CapLevel?main:ClassSystem.Taken(h).FirstOrDefault(x=>ClassSystem.Level(h,x)<ClassSystem.CapLevel);if(c==null)break;ClassSystem.SetLevel(h,c,ClassSystem.Level(h,c)+1);}
   Finish(h);
  }
  // 子职业、分支、宿敌随机选；25 级起学传奇职业能力；资源回满。
  public static void Finish(Hediff_Adventurer h){
   foreach(var c in ClassSubclass.NeedPick(h).ToList()){var opt=ClassSubclass.Options(c).ToList();if(opt.Count>0)ClassSubclass.Pick(h,opt.RandomElement());}
   for(int guard=0;guard<8;guard++){
    var pend=ClassSubclass.PendingChoices(h).FirstOrDefault();if(pend.ch==null)break;
    var opts=Enumerable.Range(0,pend.ch.options.Count).Where(i=>ClassSubclass.CanChoose(h,pend.ch.key,i)==null).ToList();
    if(opts.Count==0 || !ClassSubclass.Choose(h,pend.ch.key,opts.RandomElement()))break;
   }
   var r=ClassMartial2.Ranger;
   if(r!=null && ClassSystem.Level(h,r)>0)for(int guard=0;guard<4 && ClassMartial2.OpenFoeSlots(h)>0;guard++)ClassMartial2.PickFoe(h,ClassMartial2.FoeTypes.Where(t=>!h.classes.favored.Contains(t)).RandomElement());
   if(h.level>=ClassLegendary.Thresholds[0]){
    ClassLegendary.SyncPoints(h);
    for(int guard=0;guard<4 && ClassLegendary.Points(h)>0;guard++){
     var cand=ClassSystem.Taken(h).SelectMany(c=>c.legendary.Select(a=>(c,a))).Where(t=>ClassLegendary.CanLearn(h,t.c,t.a)==null).ToList();
     if(cand.Count==0)break;var pick=cand.RandomElement();ClassLegendary.Learn(h,pick.c,pick.a);
    }
   }
   foreach(var c in ClassSystem.Taken(h).ToList())if(c.resource!=ClassResource.None)h.classes.res[ClassSystem.PoolKey(c)]=ClassSystem.MaxResource(h,c);
   if(ClassSubclass.ManaMax(h)>0)ClassSubclass.Data(h).mana=ClassSubclass.ManaMax(h);
   ClassSystem.Sync(h);if(h.pawn!=null)Martial.Refresh(h.pawn);
  }
  public static string Summary(Hediff_Adventurer h)=>h?.classes==null?"":string.Join("、",ClassSystem.Taken(h).Select(c=>c.label+" "+ClassSystem.Level(h,c)+(ClassSubclass.Picked(h,c)!=null?"（"+ClassSubclass.Picked(h,c).label+"）":"")));

  // ---------- AI 施放 ----------
  // 这些效果 AI 不用：要开窗口、要特殊目标、放建筑 / 陷阱、自我位移、招募倒地的人。
  static readonly HashSet<string> Skip=new HashSet<string>{"Passive","ControlWeather","Raise","PlantGrowth","AnimalFriendship","Teleport","DragonWings","WallOfStone","RuneTurret","WarGolem","ThornTrap","MageHand",
   "Training","Intercept","WLThrall","WLMaster","QuickRepair","TotemAttune","RageCharge","CharmPerson","Pilfer","SuppressingFire","DivineIntervention"};
  static int Now=>Find.TickManager.TicksGame;
  public static bool InCombat(Pawn p){var m=p.mindState;if(m==null)return false;return m.enemyTarget!=null || Now-m.lastHarmTick<CombatGap || Now-p.LastAttackTargetTick<CombatGap;}
  static bool Busy(Pawn p){var j=p.CurJob;return j?.ability!=null || p.Downed || p.InMentalState || p.stances?.stunner?.Stunned==true || p.Drafted;}
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.Spawned && t.HostileTo(a);
  static float RangeOf(Ability_Class a)=>a.Props?.effect=="EldritchBlast"?Warlock.Range(a.pawn):a.verb?.verbProps?.range??0f;
  // AI 会用的职业能力（玩家的自动施放开关也只给这些，ClassAutoCast.cs）。
  public static bool AiUsable(Ability_Class a){var e=a?.Props;return e!=null && !e.passive && !Skip.Contains(e.effect) && ClassSystem.ClassOf(a.def)!=null;}
  // 选一个能放的职业能力并开始施放（instant：直接生效，检查用）。返回用了的能力。
  public static Ability TryCast(Pawn p,bool instant=false){
   if(p==null || !p.Spawned || p.Map==null || Busy(p) || !InCombat(p))return null;
   return Cast(p,instant,null);
  }
  // only：只考虑这些能力（玩家的自动施放）；时机由调用方判断。
  public static Ability Cast(Pawn p,bool instant,Func<Ability_Class,bool> only){
   var list=p.abilities?.abilities;if(list==null || list.Count==0)return null;
   int n=list.Count,start=Rand.Range(0,n);
   for(int pass=0;pass<3;pass++) // 先治疗，再增益友军，最后攻击
    for(int k=0;k<n;k++){
     var a=list[(start+k)%n] as Ability_Class;if(!AiUsable(a) || only!=null && !only(a))continue;
     if(Pass(a)!=pass)continue;
     if(a.OnCooldown || !a.CanCast.Accepted)continue;
     var target=TargetFor(p,a);if(!target.IsValid)continue;
     if(instant){if(a.Activate(target,target))return a;continue;}
     var job=a.GetJob(target,target);if(job==null)continue;
     p.jobs.StartJob(job,JobCondition.InterruptForced);return a;
    }
   return null;
  }
  static readonly HashSet<string> Heals=new HashSet<string>{"Heal","LayOnHands","MassHeal"};
  static int Pass(Ability_Class a)=>Heals.Contains(a.Props.effect)?0:Friendly(a)?1:2;
  static bool Friendly(Ability_Class a)=>a.def.targetRequired && !a.def.hostile && a.def.verbProperties?.targetParams?.canTargetPawns!=false && a.def.verbProperties?.targetParams?.canTargetLocations!=true;
  public static LocalTargetInfo TargetFor(Pawn p,Ability_Class a){
   var def=a.def;var e=a.Props;var map=p.Map;float range=RangeOf(a);
   if(!def.targetRequired){ // 自身：身边有敌人时放（增益、范围震慑）；群体治疗要身边有伤员
    float r=Mathf.Max(SelfRange,e.radius);
    var own=e.hediff.NullOrEmpty()?null:DefDatabase<HediffDef>.GetNamedSilentFail(e.hediff);if(own!=null && p.health.hediffSet.GetFirstHediffOfDef(own)!=null)return LocalTargetInfo.Invalid; // 自己身上的增益还在就不重复放
    if(Heals.Contains(e.effect))return Races124.PawnsNear(map,p.Position,Mathf.Max(e.radius,1.9f)).Any(x=>!x.Dead && !x.HostileTo(p) && x.RaceProps.Humanlike && (x.Downed || ClassMelee.InjurySum(x)>5f))?(LocalTargetInfo)p:LocalTargetInfo.Invalid;
    return Races124.PawnsNear(map,p.Position,r).Any(x=>Hostile(p,x) && !x.Downed)?(LocalTargetInfo)p:LocalTargetInfo.Invalid;
   }
   if(Friendly(a) && !Heals.Contains(e.effect) && !e.hediff.NullOrEmpty()){ // 增益：在打仗（征召 / 战斗中）、还没有这个状态的友军（含自己），近的优先
    var hd=DefDatabase<HediffDef>.GetNamedSilentFail(e.hediff);
    var buddy=Races124.PawnsNear(map,p.Position,Mathf.Max(range,1.9f)).Where(x=>!x.Dead && x.Spawned && !x.Downed && !x.HostileTo(p) && x.RaceProps.Humanlike && (x.Drafted || InCombat(x)) && (hd==null || x.health.hediffSet.GetFirstHediffOfDef(hd)==null))
     .OrderBy(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault(x=>Usable(a,x));
    return buddy==null?LocalTargetInfo.Invalid:(LocalTargetInfo)buddy;
   }
   if(Friendly(a)){ // 治疗 / 增益：倒地或受伤最重的友军（含自己）
    var ally=Races124.PawnsNear(map,p.Position,Mathf.Max(range,1.9f)).Where(x=>!x.Dead && x.Spawned && !x.HostileTo(p) && x.RaceProps.Humanlike && (x.Downed || ClassMelee.InjurySum(x)>5f))
     .OrderByDescending(x=>x.Downed?1000f:ClassMelee.InjurySum(x)).FirstOrDefault(x=>Usable(a,x));
    return ally==null?LocalTargetInfo.Invalid:(LocalTargetInfo)ally;
   }
   if(!def.hostile)return LocalTargetInfo.Invalid; // 其他不敌对的定点能力（传送、造墙……）不用
   var foe=p.mindState?.enemyTarget as Pawn;
   if(!Hostile(p,foe) || foe.Downed || p.Position.DistanceTo(foe.Position)>range)foe=Races124.PawnsNear(map,p.Position,range).Where(x=>Hostile(p,x) && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault(x=>Usable(a,x));
   if(foe==null)return LocalTargetInfo.Invalid;
   bool loc=def.verbProperties?.targetParams?.canTargetLocations==true;
   if(loc){ // 范围法术：不炸到自己人
    float r=Mathf.Max(e.radius,AllyRadius)+1f;
    if(Races124.PawnsNear(map,foe.Position,r).Any(x=>x!=p && !x.Dead && !x.HostileTo(p) && (x.RaceProps.Humanlike || x.Faction!=null && x.Faction==p.Faction)) || p.Position.DistanceTo(foe.Position)<=r)return LocalTargetInfo.Invalid;
    var c=new LocalTargetInfo(foe.Position);return Usable(a,c)?c:LocalTargetInfo.Invalid;
   }
   return Usable(a,foe)?(LocalTargetInfo)foe:LocalTargetInfo.Invalid;
  }
  static bool Usable(Ability_Class a,LocalTargetInfo t){
   var p=a.pawn;if(t.Thing is Pawn x && x!=p && p.Position.DistanceTo(x.Position)>RangeOf(a))return false;
   if(ClassEffects.Invalid(p,a.Props,t)!=null)return false;
   if(t.Thing!=p && a.def.verbProperties?.requireLineOfSight!=false && !GenSight.LineOfSight(p.Position,t.Cell,p.Map))return false;
   return true;
  }

  // ---------- 设置（冒险者分组里，职业设置下面） ----------
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.CheckboxLabeled("敌人拥有职业",ref s.npcClasses,"不属于玩家的人形冒险者（袭击者、访客、商队、剧情敌人）第一次出现时按派系拿 DND 职业：部落多是野蛮人 / 德鲁伊 / 游侠，海盗多是游荡者 / 战士 / 邪术师，帝国多是圣武士 / 战士 / 法师 / 牧师，塞伊和亡灵多是法师 / 邪术师 / 牧师，其他按最高技能。一个职业（20% 两个），职业等级合计 = 冒险者等级（最多 20）；子职业随机；冒险者 25 级起有传奇职业能力。旧存档里已经在地图上的人也会补上。关闭后不再给新的 NPC 职业，已有的保留。");
   l.CheckboxLabeled("敌人施放职业能力",ref s.npcClassCasting,"有职业的 NPC 在战斗中每 4 秒最多放一个职业能力（看冷却和资源）：攻击能力打身边的敌人，范围法术不炸到自己人，治疗职业先救倒地、受伤的友军。关闭后 NPC 只有职业的被动效果。");
  }
 }
 public partial class Settings {
  public bool npcClasses=true,npcClassCasting=true;
  void ExposeNpcClasses(){Scribe_Values.Look(ref npcClasses,"lpNpcClasses",true);Scribe_Values.Look(ref npcClassCasting,"lpNpcClassCasting",true);}
 }
}
