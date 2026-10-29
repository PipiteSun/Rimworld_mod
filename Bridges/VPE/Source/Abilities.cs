using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.27.0 职业能力的公共基类：效果写在 Apply 里，Cast 先走 VEF/VPE 的流程（精神力、心灵熵、特效）再调 Apply；自检直接调 Apply。
 public abstract class ClassAbility : Ability {
  // 1.30.1：Power（含能力描述里显示的数值）与 Strength 都乘心灵敏感度系数（只对法师、牧师）。
  public float Power=>GetPowerForPawn();
  public override float GetPowerForPawn()=>base.GetPowerForPawn()*Psychic;
  public float Psychic=>Classes.PsychicFactor(pawn,Ext);
  public LP_ClassAbilityExt Ext=>def.Psycast() as LP_ClassAbilityExt;
  public int Duration=>GetDurationForPawn();
  public float Radius=>GetRadiusForPawn();
  public LP_ClassPathDef ClassPath=>def.Psycast()?.path as LP_ClassPathDef;
  // 施法者当前的职业强度（和属性一致；1.36.0 起看能力绑定的技能）。
  public float Strength=>(Ext?.ClassPath==null?1f:Classes.Power(pawn,Ext))*Psychic;
  public override void Cast(GlobalTargetInfo[] targets){base.Cast(targets);Apply(targets);}
  public abstract void Apply(GlobalTargetInfo[] targets);
  protected static Pawn PawnOf(GlobalTargetInfo t)=>t.Thing as Pawn;
  protected static bool Hostile(Pawn caster,Pawn t)=>t!=null && t!=caster && !t.Dead && t.HostileTo(caster);
  protected static bool Friendly(Pawn caster,Pawn t)=>t!=null && !t.Dead && !t.HostileTo(caster);
  protected Hediff Give(Pawn t,string hediff,int ticks,float severity=-1f)=>Items.Give(t,hediff,ticks,severity>0f?severity:Strength);
  protected IEnumerable<Pawn> Near(IntVec3 c,float radius)=>pawn.Map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(pawn.Map,c,radius);
  protected static void Mote(Thing t,string text,Color color)=>Items.Mote(t,text,color);
  protected static readonly Color Martial=new Color(.95f,.6f,.4f),Arcane=new Color(.7f,.5f,1f),Divine=new Color(1f,.9f,.5f);
  protected bool TargetPawnOk(LocalTargetInfo target,bool friendly,bool showMessages,out Pawn t){
   t=target.Thing as Pawn;
   if(t==null || t.Dead){if(showMessages)Messages.Message("需要选择一个活着的目标。",MessageTypeDefOf.RejectInput,false);return false;}
   if(friendly && t.HostileTo(pawn)){if(showMessages)Messages.Message("只能对友军使用。",MessageTypeDefOf.RejectInput,false);return false;}
   if(!friendly && !t.HostileTo(pawn)){if(showMessages)Messages.Message("只能对敌人使用。",MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
  // 治疗：先流血最多的伤口，再按严重度；返回实际治疗量。
  public static float Heal(Pawn p,float amount){
   if(p==null || p.Dead || amount<=0f)return 0f;
   var injuries=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h=>!h.IsPermanent()).OrderByDescending(h=>h.BleedRate).ThenByDescending(h=>h.Severity).ToList();
   float healed=0f;
   foreach(var inj in injuries){
    if(amount<=0f)break;
    float h=Mathf.Min(amount,inj.Severity);
    inj.Heal(h);amount-=h;healed+=h;
   }
   return healed;
  }
  public static float InjurySum(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h=>!h.IsPermanent()).Sum(h=>h.Severity);
 }

 // 对自己施加同名状态（动作如潮、武器专精、不屈意志、守护姿态、战意再临、法师护甲）。严重度 = 职业强度（分档状态按它取档）。
 public class Ability_SelfBuff : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){Give(pawn,def.defName,Duration);Mote(pawn,def.LabelCap,Martial);}
 }
 // 对友军施加状态：急速术、祝福、神导术。
 public class Ability_TargetBuff : ClassAbility {
  public string HediffName=>def.defName=="LP_Bless"?"LP_Blessed":def.defName=="LP_Guidance"?"LP_Guided":def.defName;
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,true,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(!Friendly(pawn,t))continue;
    Give(t,HediffName,Duration);
    if(def.defName=="LP_Bless")t.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_BlessedThought"));
    Mote(t,def.LabelCap,def.defName=="LP_Haste"?Arcane:Divine);
   }
  }
 }

 // ===== 战士 =====
 // 回气：状态的严重度 = 总治疗量（20 × 战技强度），HediffComp_ClassRegen 在 600 刻内分 10 次治疗。
 public class Ability_SecondWind : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){Give(pawn,"LP_SecondWind",600,Power);Mote(pawn,"回气",Martial);}
 }
 public class HediffCompProperties_ClassRegen : HediffCompProperties {
  public int interval=60,ticksTotal=600;
  public HediffCompProperties_ClassRegen(){compClass=typeof(HediffComp_ClassRegen);}
 }
 public class HediffComp_ClassRegen : HediffComp {
  int ticks;
  public HediffCompProperties_ClassRegen Props=>(HediffCompProperties_ClassRegen)props;
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){
   base.CompPostTickInterval(ref severityAdjustment,delta);
   ticks+=delta;while(ticks>=Props.interval){ticks-=Props.interval;Pulse();}
  }
  // 每次脉冲治疗 总量 × 间隔/总时长。
  public float Pulse()=>ClassAbility.Heal(Pawn,parent.Severity*Props.interval/Props.ticksTotal);
  public override void CompExposeData(){base.CompExposeData();Scribe_Values.Look(ref ticks,"regenTicks");}
 }
 // 战吼：半径内的敌人被震慑。
 public class Ability_AreaEnemies : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){
   int n=0;foreach(var t in Near(pawn.Position,Radius).ToList()){if(!Hostile(pawn,t))continue;Give(t,"LP_Shaken",Duration);n++;}
   Mote(pawn,"战吼",Martial);
  }
 }
 // 绊摔：击晕 + 击退一格；体型 2 以上只震慑。
 public class Ability_Trip : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(!Hostile(pawn,t))continue;
    if(t.BodySize>=2f){Give(t,"LP_Shaken",Duration);Mote(t,"震慑",Martial);continue;}
    t.stances?.stunner?.StunFor(Duration,pawn);
    if(t.Spawned){var dest=DNDPush.Destination(t.Map,t.Position,(t.Position-pawn.Position).ToVector3(),1);DNDPush.Move(t,dest);}
    Mote(t,"绊摔",Martial);
   }
  }
 }
 // 旋风斩：对相邻敌人各挥一次当前近战武器。
 public class Ability_Whirlwind : ClassAbility {
  // 1.30.1：每个相邻敌人吃一次真正的近战伤害结算（Verb_MeleeAttackDamage 的 ApplyMeleeDamageToTarget，不掷命中），
  // 期间挂一个“旋风之势”（VEF_MeleeAttackDamageFactor = 战技强度）。原版近战伤害系数、武器品质、Isekai 的精炼和力量加成都在这条路上照常生效。
  static readonly System.Reflection.MethodInfo applyMelee=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget");
  public override void Apply(GlobalTargetInfo[] targets){
   Sweep(pawn,Strength,Near(pawn.Position,Radius).Where(t=>Hostile(pawn,t) && t.Position.AdjacentTo8WayOrInside(pawn.Position)).ToList());
   Mote(pawn,"旋风斩",Martial);
  }
  public static int Sweep(Pawn pawn,float strength,List<Pawn> foes){
   int n=0;
   Items.Give(pawn,"LP_WhirlwindFury",60,strength);
   try{
    foreach(var t in foes){
     if(t.Dead || !t.Spawned)continue;
     var verb=pawn.meleeVerbs?.TryGetMeleeVerb(t);
     if(verb is Verb_MeleeAttackDamage && applyMelee!=null)applyMelee.Invoke(verb,new object[]{new LocalTargetInfo(t)});
     else t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,8f*strength,.1f,-1f,pawn));
     pawn.Drawer?.Notify_MeleeAttackOn(t);n++;
    }
   }finally{Items.Remove(pawn,"LP_WhirlwindFury");}
   return n;
  }
  // 自检用：旋风斩这一击按近战结算会用到的伤害量（含近战伤害系数、VEF 伤害系数与旋风之势）。
  public static float HitAmount(Pawn pawn,Pawn target,float strength){
   Items.Give(pawn,"LP_WhirlwindFury",60,strength);
   try{var verb=pawn.meleeVerbs.TryGetMeleeVerb(target);return verb.verbProps.AdjustedMeleeDamageAmount(verb,pawn);}
   finally{Items.Remove(pawn,"LP_WhirlwindFury");}
  }
 }
 // 旋风之势：VEF_MeleeAttackDamageFactor = 严重度（战技强度），只在旋风斩结算的那一下存在。
 public class Hediff_ClassStrike : HediffWithComps {
  HediffStage stage;float built=-1f;
  public override HediffStage CurStage{get{
   if(stage==null || built!=Severity){
    built=Severity;stage=new HediffStage{statFactors=new List<StatModifier>()};
    var st=DefDatabase<StatDef>.GetNamedSilentFail("VEF_MeleeAttackDamageFactor");if(st!=null)stage.statFactors.Add(new StatModifier{stat=st,value=Severity});
   }
   return stage;
  }}
 }
 // 不屈意志 / 战意再临：免疫击晕。
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.StunFor))]
 public static class StunImmunity {
  static readonly AccessTools.FieldRef<StunHandler,Thing> Parent=AccessTools.FieldRefAccess<StunHandler,Thing>("parent");
  static bool Prefix(StunHandler __instance){
   var p=Parent(__instance) as Pawn;
   return !(Items.Has(p,"LP_Indomitable") || Items.Has(p,"LP_SurgeOfBattle") || Items.Has(p,"LP_HolyAvatar") || Items.Has(p,"LP_DiamondSoul"));
  }
 }
 // 守护姿态：半径 3 内友军受到的外来伤害 30% 转到守护者身上。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class GuardStanceShare {
  public const float Share=.3f,Radius=3f;
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(__instance.Dead || dinfo.Def==null || dinfo.Amount<=0f || !dinfo.Def.ExternalViolenceFor(__instance) || __instance.Faction==null || !__instance.Spawned)return;
   var stance=Stance.Def;if(stance==null || __instance.health.hediffSet.HasHediff(stance))return;
   var guardian=Guardian(__instance);if(guardian==null)return;
   float moved=dinfo.Amount*Share;
   dinfo.SetAmount(dinfo.Amount-moved);
   guardian.TakeDamage(new DamageInfo(dinfo.Def,moved,dinfo.ArmorPenetrationInt,-1f,dinfo.Instigator,null,dinfo.Weapon));
  }
  // 1.58.6：每次有阵营的小人受伤都会走到这里。原来每次复制整张地图的小人列表；现在按同样的顺序直接遍历（选出的守护者不变），姿态 Def 用静态 DefRef。
  static readonly DefRef<HediffDef> Stance=new DefRef<HediffDef>("LP_GuardStance");
  public static Pawn Guardian(Pawn victim){
   var stance=Stance.Def;if(stance==null || victim?.Map==null)return null;
   var all=victim.Map.mapPawns.AllPawnsSpawned;
   for(int i=0;i<all.Count;i++){var g=all[i];if(!g.Dead && g.Position.InHorDistOf(victim.Position,Radius) && g!=victim && !g.Downed && g.Faction==victim.Faction && g.health.hediffSet.HasHediff(stance))return g;}
   return null;
  }
 }
 // 战意再临：近战击杀后所有战士战技立刻冷却完。1.53.0 死神之眼：远程击杀也一样。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class SurgeKillReset {
  static void Postfix(Pawn __instance,DamageInfo? dinfo){
   var killer=dinfo?.Instigator as Pawn;
   if(killer==null || killer==__instance || dinfo.Value.Def==null)return;
   bool ranged=dinfo.Value.Def.isRanged || Martial.Ranged(dinfo.Value);
   if(!ranged && Items.Has(killer,"LP_SurgeOfBattle") || ranged && (Martial.Flags(killer) & Martial.FlagDeadEye)!=0)Reset(killer);
  }
  public static int Reset(Pawn killer){
   var comp=killer.GetComp<CompAbilities>();if(comp==null)return 0;int n=0;
   foreach(var a in comp.LearnedAbilities)if(a.def.Psycast()?.path?.defName==Classes.FighterPath && a.cooldown>Find.TickManager.TicksGame){a.cooldown=0;n++;}
   return n;
  }
 }

 // ===== 法师 =====
 // 魔法飞弹：3 发投射物，每发 4 × 奥术强度。
 public class Ability_MagicMissile : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   var bullet=DefDatabase<ThingDef>.GetNamedSilentFail("LP_MagicMissileBullet");if(bullet==null)return;
   foreach(var g in targets){var t=PawnOf(g);if(t==null || !t.Spawned)continue;
    for(int i=0;i<3;i++){
     var proj=(Projectile)GenSpawn.Spawn(bullet,pawn.Position,pawn.Map);
     if(proj is MagicMissileBullet mm)mm.strength=Strength;
     var origin=pawn.DrawPos+new Vector3(Rand.Range(-.4f,.4f),0f,Rand.Range(-.4f,.4f));
     proj.Launch(pawn,origin,t,t,ProjectileHitFlags.IntendedTarget);
    }
   }
  }
 }
 // 1.7 的 Projectile 没有 weaponDamageMultiplier：命中后按 (奥术强度-1) 补一次同类伤害，总伤害约为 4 × 奥术强度。
 public class MagicMissileBullet : Bullet {
  public float strength=1f;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref strength,"lpStrength",1f);}
  protected override void Impact(Thing hitThing,bool blockedByShield=false){
   base.Impact(hitThing,blockedByShield);
   int extra=Mathf.RoundToInt(def.projectile.GetDamageAmount(equipment,null)*(strength-1f));
   if(extra>0 && !blockedByShield && hitThing!=null && !hitThing.Destroyed)hitThing.TakeDamage(new DamageInfo(def.projectile.damageDef,extra,ArmorPenetration,ExactRotation.eulerAngles.y,launcher,null,equipmentDef,DamageInfo.SourceCategory.ThingOrUnknown,intendedTarget.Thing));
  }
 }
 // 火球术：火焰爆炸，伤害 15 × 奥术强度，半径 2.5。
 public class Ability_Fireball : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){if(!g.Cell.IsValid || pawn.Map==null)continue;
    GenExplosion.DoExplosion(g.Cell,pawn.Map,Radius,DamageDefOf.Flame,pawn,damAmount:Mathf.RoundToInt(Power),armorPenetration:.3f);
   }
  }
 }
 // 睡眠术：意识降到 10%，8 秒 × 奥术强度；机械体和意识已低于 30% 的目标无效；心智抗性可抵抗。
 public class Ability_Sleep : ClassAbility {
  public static string Cannot(Pawn t){
   if(t==null || t.Dead)return "需要活着的目标。";
   if(t.RaceProps.IsMechanoid)return "睡眠术对机械体无效。";
   if(t.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness)<.3f)return "目标的意识已经太低。";
   return null;
  }
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(!base.ValidateTarget(target,showMessages) || !TargetPawnOk(target,false,showMessages,out var t))return false;
   var why=Cannot(t);if(why!=null){if(showMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(Cannot(t)!=null)continue;
    if(DNDMind.TryResist(t)){Mote(t,"抵抗了睡眠术",Arcane);continue;}
    Give(t,"LP_MagicSleep",Duration);Mote(t,"睡眠",Arcane);
   }
  }
 }
 // 闪电束：从施法者到目标的直线，线上每个敌人受 20 × 奥术强度点闪电伤害并击晕 1 秒；机械体改受电磁伤害。
 public class Ability_LightningBolt : ClassAbility {
  public static DamageDef Lightning=>DefDatabase<DamageDef>.GetNamedSilentFail("LP_Lightning");
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){if(!g.Cell.IsValid || pawn.Map==null)continue;Strike(pawn,g.Cell,Power);}
  }
  public static int Strike(Pawn caster,IntVec3 target,float power){
   var map=caster.Map;int hits=0;
   foreach(var c in GenSight.PointsOnLineOfSight(caster.Position,target).Concat(Gen.YieldSingle(target)).Distinct()){
    if(c==caster.Position || !c.InBounds(map))continue;
    FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),map,1f);
    foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList()){
     if(!Hostile(caster,t))continue;
     bool mech=t.RaceProps.IsMechanoid;
     t.TakeDamage(new DamageInfo(mech?DamageDefOf.EMP:(Lightning??DamageDefOf.Burn),power,.5f,-1f,caster));
     if(!mech)t.stances?.stunner?.StunFor(60,caster);
     hits++;
    }
   }
   return hits;
  }
 }
 // 石墙术：垂直于施法方向的 5 格石墙，耐久 200 × 奥术强度，一天后消散。
 public class Ability_WallOfStone : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){if(!g.Cell.IsValid || pawn.Map==null)continue;Conjure(pawn,g.Cell,Mathf.RoundToInt(Power));}
  }
  public static List<Thing> Conjure(Pawn caster,IntVec3 center,int hitPoints){
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("LP_ConjuredWall");var made=new List<Thing>();if(def==null)return made;
   var map=caster.Map;var dir=center-caster.Position;
   bool alongZ=Math.Abs(dir.x)>=Math.Abs(dir.z);
   for(int k=-2;k<=2;k++){
    var c=alongZ?new IntVec3(center.x,0,center.z+k):new IntVec3(center.x+k,0,center.z);
    if(!c.InBounds(map) || c.Fogged(map) || c.GetEdifice(map)!=null || !c.Walkable(map))continue;
    if(c.GetThingList(map).Any(t=>t.def.category==ThingCategory.Pawn || t.def.category==ThingCategory.Item || t.def.category==ThingCategory.Building || t.def.category==ThingCategory.Plant && t.def.plant.IsTree))continue;
    var wall=ThingMaker.MakeThing(def,ThingDefOf.BlocksGranite);
    wall.SetFaction(caster.Faction);
    GenSpawn.Spawn(wall,c,map);
    wall.HitPoints=Mathf.Clamp(hitPoints,1,wall.MaxHitPoints);
    FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,2f,new Color(.6f,.55f,.5f));
    made.Add(wall);
   }
   return made;
  }
 }
 // 传送术：传到视线内可站立的格子。
 public class Ability_Teleport : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(!base.ValidateTarget(target,showMessages))return false;
   var map=pawn.Map;var c=target.Cell;
   if(!c.IsValid || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.GetFirstPawn(map)!=null){if(showMessages)Messages.Message("需要一个空着的、能站立的格子。",MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var c=g.Cell;if(!c.IsValid || pawn.Map==null || !c.Standable(pawn.Map))continue;
    FleckMaker.ThrowDustPuffThick(pawn.DrawPos,pawn.Map,2f,Arcane);
    Races124.Blink(pawn,c);
    FleckMaker.ThrowDustPuffThick(pawn.DrawPos,pawn.Map,2f,Arcane);
   }
  }
 }
 // 解离术：60 × 奥术强度点奥术力，护甲只算一半；建筑和机械体双倍；杀死的人形不留尸体。
 public class Ability_Disintegrate : ClassAbility {
  public static DamageDef ArcaneForce=>DefDatabase<DamageDef>.GetNamedSilentFail("LP_ArcaneForce");
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(!base.ValidateTarget(target,showMessages))return false;
   if(target.Thing is Pawn t)return TargetPawnOk(target,false,showMessages,out _);
   if(target.Thing is Building b && (b.Faction==null || b.Faction!=pawn.Faction))return true;
   if(showMessages)Messages.Message("只能对敌人或别人的建筑使用。",MessageTypeDefOf.RejectInput,false);
   return false;
  }
  public override void Apply(GlobalTargetInfo[] targets){
   var dmg=ArcaneForce??DamageDefOf.Bomb;
   foreach(var g in targets){var thing=g.Thing;if(thing==null || thing.Destroyed)continue;
    var line=DefDatabase<FleckDef>.GetNamedSilentFail("PsycastPsychicLine");
    if(line!=null && pawn.Map!=null)FleckMaker.ConnectingLine(pawn.DrawPos,thing.DrawPos,line,pawn.Map);
    if(thing is Pawn t){
     var map=t.Map;var cell=t.Position;bool humanlike=t.RaceProps.Humanlike;
     t.TakeDamage(new DamageInfo(dmg,Power*(t.RaceProps.IsMechanoid?2f:1f),.5f,-1f,pawn));
     if(t.Dead && humanlike && t.Corpse!=null && !t.Corpse.Destroyed){t.Corpse.Destroy();if(map!=null)FilthMaker.TryMakeFilth(cell,map,ThingDefOf.Filth_Ash,3);}
    }else thing.TakeDamage(new DamageInfo(dmg,Power*2f,.5f,-1f,pawn));
   }
  }
 }

 // ===== 牧师 =====
 // 治疗伤口：立即治疗 20 × 神恩强度。
 public class Ability_Heal : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,true,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(!Friendly(pawn,t))continue;float h=Heal(t,Power);Mote(t,"治疗 "+h.ToString("0"),Divine);}
  }
 }
 // 次级复原术：移除最严重的一种疾病，并止血 6 小时。
 public class Ability_LesserRestoration : ClassAbility {
  public static bool IsDisease(Hediff h)=>h.def.isBad && h.def.makesSickThought && !(h is Hediff_Injury) && !(h is Hediff_MissingPart) && !(h is Hediff_Addiction);
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,true,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(!Friendly(pawn,t))continue;
    var disease=t.health.hediffSet.hediffs.Where(IsDisease).OrderByDescending(h=>h.Severity).FirstOrDefault();
    if(disease!=null){t.health.RemoveHediff(disease);Mote(t,"复原："+disease.LabelBase,Divine);}
    Give(t,"LP_StaunchBleeding",Duration);
   }
  }
 }
 // 神圣打击：20 × 神恩强度点神圣伤害，对亡灵、邪魔、夺心魔翻倍。
 public class Ability_SacredFlame : ClassAbility {
  public static DamageDef Radiant=>DefDatabase<DamageDef>.GetNamedSilentFail("LP_Radiant");
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,false,showMessages,out _);
  // 1.28.0：对神明附体者 ×1.5。
  public static float Amount(Pawn t,float power)=>power*(Classes.HolyVulnerable(t)?2f:1f)*(Twilight.Vessel(t)!=null?1.5f:1f);
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(!Hostile(pawn,t))continue;
    t.TakeDamage(new DamageInfo(Radiant??DamageDefOf.Burn,Amount(t,Power),.4f,-1f,pawn));
    if(t.Map!=null)FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,1.5f);
   }
  }
 }
 // 驱散亡灵：半径内敌对亡灵逃散；没有心智的被击晕；巫妖只被震慑。
 public class Ability_TurnUndead : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var t in Near(pawn.Position,Radius).ToList()){if(!Hostile(pawn,t) || !Classes.Undead(t))continue;Turn(pawn,t,Duration);}
   Mote(pawn,"驱散亡灵",Divine);
  }
  public static string Turn(Pawn caster,Pawn t,int ticks){
   if(Items.IsLich(t)){Items.Give(t,"LP_Shaken",ticks);return "shaken";}
   if(t.RaceProps.Humanlike && t.mindState!=null && Necro.Construct(t)==null && t.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"被神圣之光驱散",true,true))return "flee";
   t.stances?.stunner?.StunFor(ticks,caster);return "stun";
  }
 }
 // 群体治疗：半径 5 内所有友军各 15 × 神恩强度。
 public class Ability_MassHeal : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var t in Near(pawn.Position,Radius).ToList()){if(!Friendly(pawn,t))continue;float h=Heal(t,Power);if(h>0f)Mote(t,"治疗 "+h.ToString("0"),Divine);}
  }
 }
 // 死亡守卫：状态严重度 = 触发时的治疗量（30 × 神恩强度）。
 public class Ability_DeathWard : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,true,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(!Friendly(pawn,t))continue;Give(t,"LP_DeathWard",Duration,Power);Mote(t,"死亡守卫",Divine);}
  }
 }
 // 本应因伤倒地时：治疗并消耗守卫。倒地判定在 CheckForStateChange 里，所以在它之前检查。
 [HarmonyPatch(typeof(Pawn_HealthTracker),"CheckForStateChange")]
 public static class DeathWardTrigger {
  static void Prefix(Pawn ___pawn){TryTrigger(___pawn);}
  public static bool WouldDown(Pawn p)=>p.health.InPainShock || !p.health.capacities.CanBeAwake || !p.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
  public static bool TryTrigger(Pawn p){
   if(p==null || p.Dead || p.Downed)return false;
   var ward=Items.Hediff("LP_DeathWard");var h=ward==null?null:p.health.hediffSet.GetFirstHediffOfDef(ward);
   if(h==null || !WouldDown(p))return false;
   float amount=h.Severity;
   p.health.RemoveHediff(h);
   ClassAbility.Heal(p,amount);
   Items.Mote(p,"死亡守卫",new Color(1f,.9f,.5f));
   if(p.Faction==Faction.OfPlayer)Messages.Message("死亡守卫护住了"+p.LabelShort+"。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
 }
 // 复生术：死亡不超过 3 天的人形尸体；要精神力接近满。
 public class Ability_Raise : ClassAbility {
  public const int MaxAge=180000;
  public static string Cannot(Corpse c){
   var p=c?.InnerPawn;if(p==null || !p.RaceProps.Humanlike)return "需要人形尸体。";
   if(Items.Undead(p) || NecromancyGenes.HasBody(p))return "复生术对巫妖和死灵造物无效。";
   if(c.Age>MaxAge)return "死亡超过 3 天，灵魂已经走远。";
   if(p.health.hediffSet.GetBrain()==null)return "大脑已经不在了，无法复生。";
   return null;
  }
  public override bool IsEnabledForPawn(out string reason){
   if(!base.IsEnabledForPawn(out reason))return false;
   if(pawn.psychicEntropy==null || pawn.psychicEntropy.CurrentPsyfocus<.95f){reason="需要精神力接近满值。";return false;}
   return true;
  }
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(!base.ValidateTarget(target,showMessages))return false;
   var why=Cannot(target.Thing as Corpse);if(why!=null){if(showMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)Raise(pawn,g.Thing as Corpse);}
  public static bool Raise(Pawn caster,Corpse c){
   if(Cannot(c)!=null)return false;
   var p=c.InnerPawn;
   if(!ResurrectionUtility.TryResurrect(p,new ResurrectionParams{removeDiedThoughts=true}))return false;
   Items.Give(p,"LP_RevivifyWeakness");
   Messages.Message(p.LabelShort+"在复生术的光芒中重新睁开了眼睛。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
 }
 // 急速术结束后的疲乏。
 public class HediffCompProperties_GiveOnRemoved : HediffCompProperties {
  public string hediff;public int ticks=2500;
  public HediffCompProperties_GiveOnRemoved(){compClass=typeof(HediffComp_GiveOnRemoved);}
 }
 public class HediffComp_GiveOnRemoved : HediffComp {
  public HediffCompProperties_GiveOnRemoved Props=>(HediffCompProperties_GiveOnRemoved)props;
  public override void CompPostPostRemoved(){base.CompPostPostRemoved();if(Pawn!=null && !Pawn.Dead)Items.Give(Pawn,Props.hediff,Props.ticks);}
 }
}
