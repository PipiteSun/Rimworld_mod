using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.48.1 冒险者专长（见 ArtSource/Leveling-1.48.1-spec.md 第二节）：12 个通用专长，用一次属性提升换一个；存在冒险者状态里（feats + 每日次数，午夜清零）。
 // 数值类效果（灵活的移速）写在 Defs/Adventurer_Feats.xml 的 statOffsets，合进冒险者状态的 CurStage；规则类效果都在下面的 Harmony 挂钩里。
 public class FeatSkillReq {public SkillDef skill;public int level;}
 public class LP_FeatDef : Def {
  public string iconPath;public int minLevel=1,order,usesPerDay;
  public List<FeatSkillReq> skillReqs;public string ability;public int abilityMin;
  public Type workerClass=typeof(FeatWorker);
  public List<StatModifier> statOffsets,statFactors;
  public List<string> enemyTags; // 敌人随机专长的类别：melee / ranged / caster / any；不写 = 敌人不选
  // 1.58.1：分类（General / Racial）、种族限制（异种型或基因任一，requireLich = 巫妖）、同组只能选一个（元素使、坚毅心智）、
  // 元素（元素使：1 火 2 冰 3 雷 4 酸）、进起源池、获得时给的能力（失去时收回）。
  // 1.67.0 专长附带 +1 属性（照 5e 2024 半专长）：可选属性列表（Str/Dex/…，写 Any = 六项都行），选专长时加在当前最高的一项，加在上限之后。
  public List<string> bonusAbilities;
  public List<int> BonusOptions{get{if(bonusIdx==null){bonusIdx=new List<int>();if(bonusAbilities!=null)foreach(var s in bonusAbilities){if(s!=null && s.Trim().ToLowerInvariant()=="any"){bonusIdx=Enumerable.Range(0,6).ToList();break;}int i=Adventurer.AbilityIndex(s);if(i>=0 && !bonusIdx.Contains(i))bonusIdx.Add(i);}}return bonusIdx;}}
  [Unsaved] List<int> bonusIdx;
  public string BonusText=>BonusOptions.Count==0?null:BonusOptions.Count==6?"任意一项属性 +1（加在当前最高的一项）":string.Join("或",BonusOptions.Select(i=>Adventurer.Names[i]))+" +1"+(BonusOptions.Count>1?"（加在当前更高的一项）":"");
  public string category="General",exclusiveGroup;public List<XenotypeDef> requiredXenotypesAny;public List<GeneDef> requiredGenesAny;public bool requireLich,origin;public int element;public AbilityDef grantAbility;
  public bool Racial=>category=="Racial";
  [Unsaved] FeatWorker worker;[Unsaved] Texture2D icon;
  public FeatWorker Worker{get{if(worker==null){worker=(FeatWorker)Activator.CreateInstance(workerClass);worker.def=this;}return worker;}}
  public int AbilityIdx=>Adventurer.AbilityIndex(ability);
  public Texture2D Icon=>icon??(icon=iconPath.NullOrEmpty()?BaseContent.BadTex:ContentFinder<Texture2D>.Get(iconPath,false)??BaseContent.BadTex);
  public string PrereqText(){
   var parts=new List<string>();
   if(minLevel>1)parts.Add(minLevel+" 级");
   if(skillReqs!=null)foreach(var r in skillReqs)parts.Add(r.skill.LabelCap+" ≥ "+r.level);
   if(AbilityIdx>=0)parts.Add(Adventurer.Names[AbilityIdx]+" ≥ "+abilityMin);
   var extra=Worker.ExtraPrereq;if(!extra.NullOrEmpty())parts.Add(extra);
   return parts.Count==0?"无":string.Join("，",parts);
  }
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(bonusAbilities!=null && BonusOptions.Count==0)yield return "bonusAbilities has no valid ability";}
 }
 public class FeatWorker {
  public LP_FeatDef def;
  public virtual string ExtraPrereq=>null;
  public virtual int UsesPerDay(Hediff_Adventurer h)=>def.usesPerDay; // 1.67.0 幸运按熟练加值
  // 不满足的前置：null = 满足。
  public virtual string Unmet(Pawn p,Hediff_Adventurer h){
   if(h==null)return "没有冒险者等级";
   if(h.level<def.minLevel)return "需要 "+def.minLevel+" 级";
   if(def.skillReqs!=null)foreach(var r in def.skillReqs){var s=p?.skills?.GetSkill(r.skill);if(s==null || s.TotallyDisabled || s.Level<r.level)return "需要"+r.skill.label+" "+r.level;}
   int a=def.AbilityIdx;if(a>=0 && h.scores[a]<def.abilityMin)return "需要"+Adventurer.Names[a]+" "+def.abilityMin;
   return Feats2.RaceUnmet(def,p)??Feats2.GroupUnmet(def,h); // 1.58.1 种族专长、同组互斥
  }
  public virtual void Gained(Pawn p,Hediff_Adventurer h){Feats2.GrantAbility(p,def.grantAbility,true);}
  public virtual void Lost(Pawn p,Hediff_Adventurer h){Feats2.GrantAbility(p,def.grantAbility,false);}
 }
 // 战争施法者：有 DND 职业路径（VPE 桥登记）或佩戴圣徽（神术）。
 public class FeatWorker_WarCaster : FeatWorker {
  public override string ExtraPrereq=>"有 DND 职业路径或佩戴圣徽";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(Feats.IsCaster(p)?null:"需要 DND 职业路径或佩戴圣徽");
 }
 // 法术狙击：同上，没有法术可放时选它没有意义。
 public class FeatWorker_SpellSniper : FeatWorker_WarCaster {}
 // 鼓舞领袖：给一个主动能力。
 public class FeatWorker_InspiringLeader : FeatWorker {
  public override void Gained(Pawn p,Hediff_Adventurer h){var d=Feats.InspireAbility;if(d!=null && p?.abilities!=null && p.abilities.GetAbility(d)==null)p.abilities.GainAbility(d);}
  public override void Lost(Pawn p,Hediff_Adventurer h){var d=Feats.InspireAbility;if(d!=null && p?.abilities?.GetAbility(d)!=null)p.abilities.RemoveAbility(d);}
 }
 // 1.67.0 幸运：每天次数 = 熟练加值（2～8）。
 public class FeatWorker_Lucky : FeatWorker {public override int UsesPerDay(Hediff_Adventurer h)=>h==null?def.usesPerDay:Math.Max(def.usesPerDay,h.Proficiency);}

 public static class Feats {
  // 1.67.0 专长加强（ArtSource/FeatsBuff-1.67.0-spec.md）：数值约翻倍，部分随等级 / 熟练加值成长。
  public const float EnemyFeatChance=.5f,AlertWarmupFactor=.5f,ToughPainFactor=.5f,SharpshooterRange=1.4f,SharpshooterCover=0f,SharpshooterDamage=1.15f,SpellSniperRange=1.75f,HealerTendFactor=1.3f,HealerHeal=6f,HeavyArmorBase=3f,HeavyArmorSharp=.6f,InspireRadius=10f,InspireBase=20f,InspirePerLevel=3f,InspirePerMod=5f,TwoHandedMass=2.5f,GreatWeaponDamage=1.25f,ToughPerLevel=.02f,ToughHeal=20f,WarCasterWarmup=.75f;
  public const int AlertTicks=600,GreatWeaponCooldown=120,SentinelCooldown=30,SentinelSlowTicks=120,SentinelStunTicks=90,HealerWounds=3,ToughMaxLevel=30;
  static LP_FeatDef F(ref LP_FeatDef c,string n)=>c??(c=DefDatabase<LP_FeatDef>.GetNamedSilentFail(n));
  static LP_FeatDef alert,tough,lucky,gwm,sharp,sentinel,warcaster,healer,ham,mobile,leader,sniper;
  public static LP_FeatDef Alert=>F(ref alert,"LP_Feat_Alert");
  public static LP_FeatDef Tough=>F(ref tough,"LP_Feat_Tough");
  public static LP_FeatDef Lucky=>F(ref lucky,"LP_Feat_Lucky");
  public static LP_FeatDef GreatWeapon=>F(ref gwm,"LP_Feat_GreatWeaponMaster");
  public static LP_FeatDef Sharpshooter=>F(ref sharp,"LP_Feat_Sharpshooter");
  public static LP_FeatDef Sentinel=>F(ref sentinel,"LP_Feat_Sentinel");
  public static LP_FeatDef WarCaster=>F(ref warcaster,"LP_Feat_WarCaster");
  public static LP_FeatDef Healer=>F(ref healer,"LP_Feat_Healer");
  public static LP_FeatDef HeavyArmor=>F(ref ham,"LP_Feat_HeavyArmorMaster");
  public static LP_FeatDef Mobile=>F(ref mobile,"LP_Feat_Mobile");
  public static LP_FeatDef Leader=>F(ref leader,"LP_Feat_InspiringLeader");
  public static LP_FeatDef SpellSniper=>F(ref sniper,"LP_Feat_SpellSniper");
  static AbilityDef inspire;public static AbilityDef InspireAbility=>inspire??(inspire=DefDatabase<AbilityDef>.GetNamedSilentFail("LP_FeatInspire"));
  public static IEnumerable<LP_FeatDef> All=>DefDatabase<LP_FeatDef>.AllDefsListForReading.OrderBy(f=>f.order);

  // ---------- 1.67.0 专长附带的 +1 属性（存在 featScore：专长 defName → 属性序号） ----------
  // 选在可选项里当前属性最高的一项（同分取列表靠前的）；读档时给没记录的老专长补选。
  public static int PickScore(Hediff_Adventurer h,LP_FeatDef f){
   var opts=f?.BonusOptions;if(opts==null || opts.Count==0)return -1;
   int best=opts[0];foreach(int i in opts)if(h.scores[i]>h.scores[best])best=i;return best;
  }
  public static void AssignScore(Hediff_Adventurer h,LP_FeatDef f){if(f==null || h.featScore.ContainsKey(f.defName))return;int a=PickScore(h,f);if(a>=0)h.featScore[f.defName]=a;}
  public static void FixScores(Hediff_Adventurer h){
   if(h.featScore==null)h.featScore=new Dictionary<string,int>();
   foreach(var k in h.featScore.Keys.ToList())if(!h.feats.Any(f=>f.defName==k) || h.featScore[k]<0 || h.featScore[k]>5)h.featScore.Remove(k);
   foreach(var f in h.feats)AssignScore(h,f);
  }
  public static int ScoreBonus(Hediff_Adventurer h,int a){
   if(h?.featScore==null || h.featScore.Count==0 || !Adventurer.Enabled)return 0;
   int n=0;foreach(var kv in h.featScore)if(kv.Value==a)n++;return n;
  }
  public static string ScoreFrom(Hediff_Adventurer h,int a)=>h?.featScore==null?"":string.Join("、",h.feats.Where(f=>h.featScore.TryGetValue(f.defName,out int i) && i==a).Select(f=>f.label));
  // 属性旁的标记：武艺天赋与专长的加值合在一起（例如“18+3”）。
  public static string ScoreTag(Hediff_Adventurer h,int a){int b=CombatGifts.ScoreBonus(h,a)+ScoreBonus(h,a);return b>0?"+"+b:"";}
  // 坚韧：部位生命 +2% × 等级（最多算到 30 级），不受属性效果强度影响。
  public static float ToughFactor(Hediff_Adventurer h)=>h!=null && Adventurer.Enabled && h.feats.Count>0 && h.feats.Contains(Tough)?1f+ToughPerLevel*Math.Min(h.level,ToughMaxLevel):1f;

  // 热路径（射程、命中、伤害）：总开关关掉时专长全部失效。
  public static bool Has(Pawn p,LP_FeatDef f){if(f==null || p==null || !Adventurer.Enabled)return false;var h=Adventurer.Get(p);return h!=null && h.feats.Count>0 && h.feats.Contains(f);}
  public static bool Use(Pawn p,LP_FeatDef f){if(!Has(p,f))return false;return Adventurer.Get(p).TryUse(f);}
  // 本地日（按地图经度换算的时区）：午夜换日。
  public static int LocalDay(Pawn p){
   float lon=0f;var map=p?.MapHeld;
   if(map!=null && Find.WorldGrid!=null)lon=Find.WorldGrid.LongLatOf(map.Tile).x;
   return (int)((GenTicks.TicksAbs+GenDate.LocalTicksOffsetFromLongitude(lon))/GenDate.TicksPerDay);
  }
  // 法系：有 DND 职业路径（VPE 桥）或佩戴圣徽。
  public static Func<Pawn,bool> HasClassHook;
  // 1.53.0 VPE 桥登记：射手的瞄准系数（稳固瞄准 ×1.15、被压制 ×0.75、死神之眼 ≥ 100 = 必中且无视掩体）。
  public static Func<Pawn,float> AimHook;
  public const float MustHit=100f;
  public static bool IsCaster(Pawn p)=>p!=null && (ClassSystem.Any(p) || HasClassHook!=null && HasClassHook(p) || Miracles.WearsSymbol(p));
  // 敌人随机专长用的类别：兵种扩展的 role，主属性是智/感/魅或有法系 → caster；拿远程武器 → ranged；否则 melee。
  public static HashSet<string> EnemyRoles(Pawn p){
   var r=new HashSet<string>();var e=Adventurer.KindExt(p);
   if(e!=null && !e.role.NullOrEmpty())r.Add(e.role);
   int pr=e?.Primary??-1;if(pr==Adventurer.Int || pr==Adventurer.Wis || pr==Adventurer.Cha || IsCaster(p))r.Add("caster");
   if(p?.equipment?.Primary!=null && p.equipment.Primary.def.IsRangedWeapon)r.Add("ranged");else r.Add("melee");
   return r;
  }
  static void Text(Pawn p,string s){if(p!=null && p.Spawned)MoteMaker.ThrowText(p.DrawPos,p.Map,s,new Color(.95f,.85f,.45f),2f);}

  // ---------- 警觉：征召后 2 秒内射击预热 −50% ----------
  static readonly Dictionary<Pawn,int> drafted=new Dictionary<Pawn,int>();
  public static void NoteDrafted(Pawn p){if(Has(p,Alert))drafted[p]=Find.TickManager.TicksGame;}
  public static bool AlertFresh(Pawn p)=>p!=null && p.Drafted && drafted.TryGetValue(p,out int t) && Find.TickManager.TicksGame-t<=AlertTicks && Has(p,Alert);
  public static int AlertWarmup(Verb verb,int ticks){
   var p=verb?.CasterPawn;if(ticks<=0 || p==null || verb.IsMeleeAttack)return ticks;
   if(verb.EquipmentSource==null){ // 1.67.0 战争施法者：能力施法预热 ×0.75（原版 / 神术 / VPE 的施法动词）
    if(verb.GetType().Name.Contains("Ability") && Has(p,WarCaster))return Mathf.Max(1,Mathf.RoundToInt(ticks*WarCasterWarmup));
    return ticks;
   }
   if(!AlertFresh(p))return ticks;
   return Mathf.Max(1,Mathf.RoundToInt(ticks*AlertWarmupFactor));
  }

  // ---------- 坚韧：每天前 3 次倒地自动止血、治 20 点伤 + 1 小时疼痛 −50%（1.67.0 起另有部位生命 +2%/级） ----------
  public static bool ToughDowned(Pawn p){
   if(p==null || p.Dead || !Use(p,Tough))return false;
   foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>i.Bleeding).ToList())inj.Tended(Rand.Range(.3f,.5f),1f);
   Feats2.Heal(p,ToughHeal);
   var d=Items.Hediff("LP_FeatToughResolve");if(d!=null){var old=p.health.hediffSet.GetFirstHediffOfDef(d);if(old!=null)p.health.RemoveHediff(old);p.health.AddHediff(d);}
   Text(p,"坚韧");return true;
  }

  // ---------- 幸运：每天熟练加值次，被命中或自己打空时重掷 ----------
  // 先掷一次；没中且攻击者有幸运 → 重掷；中了且目标有幸运 → 重掷。次数只在会改变结果的情况下（0 < p < 1）才消耗。
  public static bool LuckyRoll(Pawn attacker,Pawn defender,float chance){
   bool hit=Rand.Chance(chance);
   if(chance<=0f || chance>=1f)return hit;
   if(!hit && Use(attacker,Lucky)){hit=Rand.Chance(chance);Text(attacker,"幸运");}
   if(hit && defender!=attacker && Use(defender,Lucky)){hit=Rand.Chance(chance);Text(defender,"幸运");}
   return hit;
  }
  public static bool LuckyInvolved(Pawn a,Pawn d)=>Has(a,Lucky) && Adventurer.Get(a).UsesLeft(Lucky)>0 || Has(d,Lucky) && Adventurer.Get(d).UsesLeft(Lucky)>0;
  public static float AimChance(Pawn shooter,Thing target){
   var verb=shooter?.CurrentEffectiveVerb;if(verb==null || target==null || shooter.Map==null)return .5f;
   try{return Mathf.Clamp01(ShotReport.HitReportFor(shooter,verb,target).AimOnTargetChance_IgnoringPosture);}catch{return .5f;}
  }

  // ---------- 巨武器大师：双手近战伤害 ×1.25；击倒/击杀后立刻再攻击一次（冷却 2 秒） ----------
  public static bool TwoHanded(Pawn p){var w=p?.equipment?.Primary;return w!=null && w.def.IsMeleeWeapon && w.GetStatValue(StatDefOf.Mass)>=TwoHandedMass;}
  static readonly HashSet<Pawn> gwmPending=new HashSet<Pawn>();
  public static bool GreatWeaponTrigger(Pawn attacker){
   if(!Has(attacker,GreatWeapon) || !TwoHanded(attacker))return false;
   var h=Adventurer.Get(attacker);int now=Find.TickManager.TicksGame;
   if(now-h.lastGreatWeaponTick<GreatWeaponCooldown)return false;
   h.lastGreatWeaponTick=now;gwmPending.Add(attacker);return true;
  }
  // 这一下打完（冷却姿态已设好）之后：清掉冷却，立刻打身边下一个站着的敌人。返回新目标。
  public static Pawn GreatWeaponFollowUp(Pawn attacker){
   if(!gwmPending.Remove(attacker) || attacker==null || !attacker.Spawned || attacker.Downed || attacker.Dead)return null;
   if(attacker.stances.curStance is Stance_Busy)attacker.stances.SetStance(new Stance_Mobile());
   Text(attacker,"巨武器大师");
   var next=GenAdj.CellsAdjacent8Way(attacker).Where(c=>c.InBounds(attacker.Map)).SelectMany(c=>c.GetThingList(attacker.Map)).OfType<Pawn>().FirstOrDefault(t=>t!=attacker && !t.Dead && !t.Downed && t.HostileTo(attacker));
   if(next!=null && attacker.meleeVerbs.TryMeleeAttack(next,null,false))return next;
   return null;
  }

  // ---------- 神射手：射程 ×1.4，无视掩体，远程伤害 ×1.15；法术狙击：神术射程 ×1.75 ----------
  public static float RangeFactor(Verb verb,Thing attacker){
   if(!(attacker is Pawn p) || verb==null)return 1f;
   if(verb is Verb_CastAbility ca)return ca.ability is Ability_Miracle && Has(p,SpellSniper)?SpellSniperRange:1f;
   if(!verb.verbProps.IsMeleeAttack && verb.EquipmentSource!=null && Has(p,Sharpshooter))return SharpshooterRange;
   return 1f;
  }

  // ---------- 哨兵：近战范围内的敌人离开时自由攻击一次（冷却 0.5 秒），命中定身 1.5 秒；灵活的人不触发 ----------
  public static readonly HashSet<Pawn> sentinels=new HashSet<Pawn>();
  public static void RefreshSentinels(){
   sentinels.Clear();if(Sentinel==null || !Adventurer.Enabled)return;
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawnsSpawned)if(p.RaceProps.Humanlike && Has(p,Sentinel))sentinels.Add(p); // 1.58.6：只有人形会有冒险者状态，先判断
  }
  public static void Moved(Pawn mover,IntVec3 from){
   if(sentinels.Count==0 || mover==null || !mover.Spawned || mover.Position==from)return;
   // 1.58.6：先看有没有哨兵真的会出手（同图、离开相邻格），再查移动者是不是“灵活”；原来每个移动的小人每一步都先扫一遍 hediff，还复制一次哨兵集合。
   var buf=sentinelBuffer;if(buf==null || sentinelBusy)buf=new List<Pawn>();else sentinelBusy=true;
   try{
    buf.Clear();buf.AddRange(sentinels);bool? mobile=null;
    for(int i=0;i<buf.Count;i++){
     var s=buf[i];
     if(s.Map!=mover.Map || !s.Spawned)continue;
     if(!from.AdjacentTo8WayOrInside(s.Position) || mover.Position.AdjacentTo8WayOrInside(s.Position))continue;
     if(mobile==null)mobile=Has(mover,Mobile) || Exalted.Count>0 && Exalted.ImmuneToSentinel(mover);if(mobile.Value)return; // 1.59.0 疾影同灵活
     SentinelStrike(s,mover);
    }
   }finally{if(buf==sentinelBuffer){buf.Clear();sentinelBusy=false;}}
  }
  static readonly List<Pawn> sentinelBuffer=new List<Pawn>();static bool sentinelBusy;
  public static bool SentinelStrike(Pawn s,Pawn target){
   if(s==null || target==null || s.Dead || s.Downed || s.InMentalState && !s.InAggroMentalState || !s.Awake() || s.stances.stunner.Stunned || !s.HostileTo(target) || s.WorkTagIsDisabled(WorkTags.Violent))return false;
   var h=Adventurer.Get(s);int now=Find.TickManager.TicksGame;if(h==null || now-h.lastSentinelTick<SentinelCooldown)return false;
   if(!(s.meleeVerbs.TryGetMeleeVerb(target) is Verb_MeleeAttack verb))return false;
   h.lastSentinelTick=now;Text(s,"哨兵");
   if(!Rand.Chance(s.GetStatValue(StatDefOf.MeleeHitChance)))return false;
   try{ApplyMelee(verb,new LocalTargetInfo(target));}catch(Exception e){Log.Warning("[DND边缘大陆] 哨兵自由攻击失败："+e.Message);return false;}
   var slow=Items.Hediff("LP_FeatSentinelSlow");if(slow!=null && !target.Dead){var old=target.health.hediffSet.GetFirstHediffOfDef(slow);if(old!=null)target.health.RemoveHediff(old);target.health.AddHediff(slow);}
   if(!target.Dead && !target.Downed)target.stances?.stunner?.StunFor(SentinelStunTicks,s,false,true,false); // 1.67.0 定身
   return true;
  }
  static readonly Func<Verb_MeleeAttack,LocalTargetInfo,DamageWorker.DamageResult> ApplyMelee=(Func<Verb_MeleeAttack,LocalTargetInfo,DamageWorker.DamageResult>)Delegate.CreateDelegate(typeof(Func<Verb_MeleeAttack,LocalTargetInfo,DamageWorker.DamageResult>),AccessTools.Method(typeof(Verb_MeleeAttack),"ApplyMeleeDamageToTarget"));

  // ---------- 战争施法者：施法预热时受伤不打断（不中断工作、不被伤害眩晕） ----------
  public static bool Casting(Pawn p){
   var d=p?.jobs?.curDriver;if(d==null)return false;
   return p.CurJob?.ability!=null || d.GetType().Name.StartsWith("JobDriver_CastAbility");
  }
  public static bool CastShielded(Pawn p)=>Has(p,WarCaster) && Casting(p);

  // ---------- 重甲大师：躯干穿重甲（锐器护甲 ≥ 60%）时锐器/钝器伤害 −（3 + 熟练加值） ----------
  public static bool WearsHeavyArmor(Pawn p){
   if(p?.apparel==null)return false;
   foreach(var a in p.apparel.WornApparel)if(a.def.apparel?.bodyPartGroups!=null && a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && a.GetStatValue(StatDefOf.ArmorRating_Sharp)>=HeavyArmorSharp)return true;
   return false;
  }
  public static float HeavyArmorReduce(Pawn p,DamageDef def,float amount){
   if(amount<=0f || def==null || def.armorCategory==null || (def.armorCategory!=DamageArmorCategoryDefOf.Sharp && def.armorCategory.defName!="Blunt") || !Has(p,HeavyArmor) || !WearsHeavyArmor(p))return amount;
   return Mathf.Max(0f,amount-HeavyArmorReduction(p));
  }
  public static float HeavyArmorReduction(Pawn p)=>HeavyArmorBase+(Adventurer.Get(p)?.Proficiency??2);

  // ---------- 治疗者：包扎品质 ×1.3，每次包扎额外治疗最小的 3 处伤各 6 点 ----------
  public static Hediff_Injury HealerExtra(Pawn doctor,Pawn patient){
   if(!Has(doctor,Healer) || patient==null || patient.Dead)return null;
   var list=patient.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>!i.IsPermanent() && i.Severity>0f).OrderBy(i=>i.Severity).Take(HealerWounds).ToList();
   foreach(var inj in list)inj.Heal(HealerHeal);return list.FirstOrDefault();
  }

  // ---------- 鼓舞领袖 ----------
  public static float InspireAmount(Pawn leader){var h=Adventurer.Get(leader);return Mathf.Max(1f,InspireBase+(h==null?0:InspirePerLevel*Math.Min(h.level,Adventurer.MaxLevel)+InspirePerMod*h.Mod(Adventurer.Cha)));}
  public static int Inspire(Pawn leader){
   if(leader?.Map==null || !Use(leader,Leader))return 0;
   var d=Items.Hediff("LP_FeatInspired");if(d==null)return 0;float amount=InspireAmount(leader);int n=0;
   foreach(var p in leader.Map.mapPawns.AllPawnsSpawned.ToList()){
    if(p.Dead || !p.RaceProps.Humanlike || p.Position.DistanceTo(leader.Position)>InspireRadius || p.HostileTo(leader) || p.Faction!=leader.Faction && (p.Faction==null || leader.Faction==null || !p.Faction.AllyOrNeutralTo(leader.Faction)))continue;
    var old=p.health.hediffSet.GetFirstHediffOfDef(d);if(old!=null)p.health.RemoveHediff(old);
    var h=HediffMaker.MakeHediff(d,p);h.Severity=amount;p.health.AddHediff(h);n++;
    var mood=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_FeatInspiredMood");if(mood!=null)p.needs?.mood?.thoughts?.memories?.TryGainMemory(mood); // 1.67.0
   }
   Text(leader,"鼓舞领袖");return n;
  }
 }

 // 鼓舞：吸收伤害的护盾（Severity = 剩余吸收量），1 天后消失。
 public class Hediff_FeatInspired : HediffWithComps {
  public override string LabelInBrackets=>"剩余 "+Severity.ToString("0");
  public override bool ShouldRemove=>base.ShouldRemove || Severity<=.5f;
 }
 public class CompProperties_FeatInspire : CompProperties_AbilityEffect {public CompProperties_FeatInspire(){compClass=typeof(CompAbilityEffect_FeatInspire);}}
 public class CompAbilityEffect_FeatInspire : CompAbilityEffect {
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){base.Apply(target,dest);Feats.Inspire(parent.pawn);}
  public override bool GizmoDisabled(out string reason){
   var h=Adventurer.Get(parent.pawn);
   if(h==null || !Feats.Has(parent.pawn,Feats.Leader)){reason="没有鼓舞领袖专长。";return true;}
   if(h.UsesLeft(Feats.Leader)<=0){reason="今天的次数用完了（午夜恢复）。";return true;}
   return base.GizmoDisabled(out reason);
  }
 }

 // ==================== Harmony ====================
 // 警觉：被突袭（surpriseAttack）的目标照常闪避、不被“出手即眩晕”的攻击眩晕。
 [HarmonyPatch(typeof(Verb),nameof(Verb.TryStartCastOn),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo),typeof(bool),typeof(bool),typeof(bool),typeof(bool)})]
 public static class FeatAlertSurprise {
  [ThreadStatic] public static Thing noStun;
  static void Prefix(Verb __instance,LocalTargetInfo castTarg,ref bool surpriseAttack){
   var t=castTarg.Pawn;if(t==null || !Feats.Has(t,Feats.Alert))return;
   surpriseAttack=false;if(__instance.verbProps.stunTargetOnCastStart)noStun=t;
  }
  static void Finalizer(){noStun=null;}
 }
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.StunFor))]
 public static class FeatAlertStun {
  static bool Prefix(StunHandler __instance){return FeatAlertSurprise.noStun==null || __instance.parent!=FeatAlertSurprise.noStun;}
 }
 // 警觉：征召后 2 秒内射击预热减半。
 [HarmonyPatch(typeof(Stance_Warmup),MethodType.Constructor,new[]{typeof(int),typeof(LocalTargetInfo),typeof(Verb)})]
 public static class FeatAlertWarmup {
  static void Prefix(ref int ticks,Verb verb){ticks=Feats.AlertWarmup(verb,ticks);}
 }
 // 坚韧：倒地。
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class FeatToughDowned {
  static void Postfix(Pawn ___pawn){if(___pawn!=null && ___pawn.Downed && Feats.Has(___pawn,Feats.Tough))Feats.ToughDowned(___pawn);}
 }
 // 幸运（近战）：命中率在这里由我们掷，原版再掷 0/1。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class FeatLuckyMelee {
  static void Postfix(Verb_MeleeAttack __instance,LocalTargetInfo target,ref float __result){
   var a=__instance.CasterPawn;var d=target.Thing as Pawn;
   if(a==null || !Feats.LuckyInvolved(a,d))return;
   __result=Feats.LuckyRoll(a,d,__result)?1f:0f;
  }
 }
 // 幸运（远程）：发射时看这一发是打中还是打空，按瞄准命中率重掷，直接改落点。
 [HarmonyPatch(typeof(Projectile),nameof(Projectile.Launch),new[]{typeof(Thing),typeof(Vector3),typeof(LocalTargetInfo),typeof(LocalTargetInfo),typeof(ProjectileHitFlags),typeof(bool),typeof(Thing),typeof(ThingDef)})]
 public static class FeatLuckyRanged {
  static void Prefix(Thing launcher,ref LocalTargetInfo usedTarget,LocalTargetInfo intendedTarget,ref ProjectileHitFlags hitFlags){
   if(!(launcher is Pawn a) || !(intendedTarget.Thing is Pawn t) || a==t || !Feats.LuckyInvolved(a,t))return;
   bool hitting=usedTarget.Thing==t;float p=Feats.AimChance(a,t);
   if(p<=0f || p>=1f)return;
   if(!hitting && Feats.Use(a,Feats.Lucky) && Rand.Chance(p)){usedTarget=t;hitFlags|=ProjectileHitFlags.IntendedTarget;hitting=true;}
   if(hitting && Feats.Use(t,Feats.Lucky) && !Rand.Chance(p)){
    var map=t.Map;var miss=t.Position+GenRadial.RadialPattern[Rand.RangeInclusive(1,8)];
    if(map!=null && miss.InBounds(map)){usedTarget=miss;hitFlags=ProjectileHitFlags.NonTargetWorld;}
   }
  }
 }
 // 巨武器大师：这一下让目标倒地或死亡。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"TryCastShot")]
 public static class FeatGreatWeaponHit {
  static void Prefix(Verb_MeleeAttack __instance,out bool __state){var t=__instance.CurrentTarget.Thing as Pawn;__state=t!=null && !t.Dead && !t.Downed && Feats.Has(__instance.CasterPawn,Feats.GreatWeapon);}
  static void Postfix(Verb_MeleeAttack __instance,bool __result,bool __state){
   if(!__state || !__result)return;var t=__instance.CurrentTarget.Thing as Pawn;
   if(t!=null && (t.Dead || t.Downed))Feats.GreatWeaponTrigger(__instance.CasterPawn);
  }
 }
 [HarmonyPatch(typeof(Verb),"TryCastNextBurstShot")]
 public static class FeatGreatWeaponFollow {
  static void Postfix(Verb __instance){var p=__instance.CasterPawn;if(p!=null && __instance is Verb_MeleeAttack)Feats.GreatWeaponFollowUp(p);}
 }
 // 神射手、法术狙击（神术）：射程。
 [HarmonyPatch(typeof(VerbProperties),nameof(VerbProperties.AdjustedRange))]
 public static class FeatRange {
  static void Postfix(Verb ownerVerb,Thing attacker,ref float __result){
   if(__result>0f){float f=Feats.RangeFactor(ownerVerb,attacker);if(f!=1f)__result*=f;}
   if(__result>0f && Styles.Users.Count>0 && attacker is Pawn p && Styles.Users.Contains(p.thingIDNumber))__result=Styles.Range(ownerVerb,p,__result); // 1.53.0 箭术、投掷
  }
 }
 // 神射手：目标掩体减免减半。1.53.0：战斗风格（箭术、投掷、守护、盲斗）与 VPE 桥的瞄准系数（AimHook）也在这里。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class FeatSharpshooterCover {
  static readonly AccessTools.StructFieldRef<ShotReport,float> Cover=AccessTools.StructFieldRefAccess<ShotReport,float>("coversOverallBlockChance");
  static readonly AccessTools.StructFieldRef<ShotReport,float> Shooter=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromShooterAndDist");
  static readonly AccessTools.StructFieldRef<ShotReport,float> Equipment=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromEquipment");
  static readonly AccessTools.StructFieldRef<ShotReport,float> Weather=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromWeather");
  static readonly AccessTools.StructFieldRef<ShotReport,float> Gas=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromCoveringGas");
  static readonly AccessTools.StructFieldRef<ShotReport,float> Size=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromTargetSize");
  static readonly AccessTools.StructFieldRef<ShotReport,float> Dark=AccessTools.StructFieldRefAccess<ShotReport,float>("offsetFromDarkness");
  static void Postfix(Thing caster,Verb verb,LocalTargetInfo target,ref ShotReport __result){
   if(!(caster is Pawn p) || verb==null || verb.IsMeleeAttack)return;
   if(Feats.Has(p,Feats.Sharpshooter))Cover(ref __result)*=Feats.SharpshooterCover;
   float aim=Feats.AimHook==null?1f:Feats.AimHook(p);
   if(Styles.Users.Count>0 && Styles.Users.Contains(p.thingIDNumber)){
    aim*=Styles.Aim(verb,p);
    if(Styles.Has(p,Styles.BlindFighting))Gas(ref __result)=1f; // 盲斗：烟雾不影响射击
   }
   if(Styles.protectors.Count>0 && target.Thing is Pawn t)aim*=Styles.Protect(t,p);
   if(aim>=Feats.MustHit){Apply(ref __result);return;}
   if(aim!=1f)Shooter(ref __result)*=aim;
  }
  // 必中：瞄准的各项系数都按 1 算、没有黑暗惩罚、无视掩体。
  public static void Apply(ref ShotReport r){Shooter(ref r)=1f;Equipment(ref r)=1f;Weather(ref r)=1f;Gas(ref r)=1f;Size(ref r)=Mathf.Max(1f,Size(ref r));Dark(ref r)=Mathf.Max(0f,Dark(ref r));Cover(ref r)=0f;}
 }
 // 哨兵：敌人走出近战范围。
 [HarmonyPatch(typeof(Pawn_PathFollower),"TryEnterNextPathCell")]
 public static class FeatSentinelMove {
  static void Prefix(Pawn ___pawn,out IntVec3 __state){__state=___pawn?.Position??IntVec3.Invalid;}
  static void Postfix(Pawn ___pawn,IntVec3 __state){if(Feats.sentinels.Count>0 && __state.IsValid)Feats.Moved(___pawn,__state);}
 }
 // 战争施法者：施法中受伤不打断工作、不被伤害眩晕。
 [HarmonyPatch(typeof(Pawn_JobTracker),nameof(Pawn_JobTracker.Notify_DamageTaken))]
 public static class FeatWarCasterJob {
  static bool Prefix(Pawn ___pawn)=>!Feats.CastShielded(___pawn);
 }
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.Notify_DamageApplied))]
 public static class FeatWarCasterStun {
  static bool Prefix(StunHandler __instance)=>!(__instance.parent is Pawn p && Feats.CastShielded(p));
 }
 // 重甲大师减伤（护甲之前）、鼓舞护盾吸收。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class FeatDamage {
  static readonly DefRef<HediffDef> Inspired=new DefRef<HediffDef>("LP_FeatInspired"); // 1.58.6：每次受伤都会查
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   var def=dinfo.Def;if(def==null || !def.harmsHealth || __instance.Dead)return true;
   if(Styles.interceptors.Count>0 && Styles.Intercept(__instance,ref dinfo) && dinfo.Amount<=.001f){absorbed=true;return false;} // 1.53.0 拦截风格
   float a=Feats.HeavyArmorReduce(__instance,def,dinfo.Amount);
   if(a!=dinfo.Amount){dinfo.SetAmount(a);if(a<=.001f){absorbed=true;return false;}}
   var d=Inspired.Def;var h=d==null?null:__instance.health?.hediffSet?.GetFirstHediffOfDef(d);
   if(h!=null && dinfo.Amount>0f){
    float take=Mathf.Min(h.Severity,dinfo.Amount);h.Severity-=take;dinfo.SetAmount(dinfo.Amount-take);
    if(dinfo.Amount<=.001f){absorbed=true;return false;}
   }
   return true;
  }
 }
 // 治疗者：包扎品质、额外治疗。
 [HarmonyPatch(typeof(TendUtility),nameof(TendUtility.CalculateBaseTendQuality),new[]{typeof(Pawn),typeof(Pawn),typeof(float),typeof(float)})]
 public static class FeatHealerQuality {
  static void Postfix(Pawn doctor,float medicineQualityMax,ref float __result){if(Feats.Has(doctor,Feats.Healer))__result=Mathf.Min(Mathf.Max(__result,medicineQualityMax),__result*Feats.HealerTendFactor);}
 }
 [HarmonyPatch(typeof(TendUtility),nameof(TendUtility.DoTend))]
 public static class FeatHealerTend {
  static void Postfix(Pawn doctor,Pawn patient){Feats.HealerExtra(doctor,patient);}
 }
}
