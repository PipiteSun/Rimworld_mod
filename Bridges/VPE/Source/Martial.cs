using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using VanillaPsycastsExpanded.UI;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.53.0 武人之道（见 ArtSource/MartialClasses-1.53.0-spec.md 第二节）：物理路径（LP_ClassPathDef.martial：战士、圣武士、武僧）共用的斗志、VPE 等级加成、额外攻击，
 // 以及战士射击支用到的“射击状态位”（ShotFlags）和登记过的子弹（Shots）。
 // 性能：Cache（HashSet<int>）只在 MapComponent_ClassAuras.Refresh 那一次遍历里（每 250 刻）和解锁路径时更新；所有命中、伤害、击杀挂钩第一步都是 Is(p)。
 public class MartialInfo {public bool fighter,paladin,monk;public int tier,vpeLevel;public float vpeBonus;}
 public enum PrecisionMode{Vital,Leg,Hand}
 public class ShotMod {public float damage=1f,ap,power=1f;public PrecisionMode mode;public bool precision;public Pawn caster;}

 public static class Martial {
  public const float VpePer=.02f,VpeMax=.4f,FuryHit=.004f,FuryKill=.03f,FuryCap=.15f;
  public const int HourTicks=2500;
  public static readonly int[] TierLevels={5,11,20};
  public static readonly float[] FollowUp={0f,.15f,.25f,.35f},MeleeCooldown={1f,.8f,.7f,.6f},RangedCooldown={1f,.85f,.75f,.65f};
  public static readonly Color Color=new Color(.95f,.6f,.4f);

  // ---------- 物理路径缓存 ----------
  public static readonly HashSet<int> Cache=new HashSet<int>();
  static readonly Dictionary<int,MartialInfo> infos=new Dictionary<int,MartialInfo>();
  public static bool Is(Pawn p)=>p!=null && Cache.Contains(p.thingIDNumber);
  public static MartialInfo Info(Pawn p){if(p==null)return null;infos.TryGetValue(p.thingIDNumber,out var i);return i;}
  public static float VpeBonus(Pawn p){if(p==null || !Cache.Contains(p.thingIDNumber))return 0f;return infos.TryGetValue(p.thingIDNumber,out var i)?i.vpeBonus:0f;}
  public static int VpeLevel(Pawn p)=>Info(p)?.vpeLevel??0;
  public static int Tier(Pawn p)=>Is(p)?Info(p)?.tier??0:0;
  public static float BonusFor(int vpeLevel)=>Mathf.Clamp(VpePer*(vpeLevel-1),0f,VpeMax);
  // 档位用的等级：冒险者等级；冒险者总开关关闭或没有冒险者状态时用 VPE 灵能等级。
  public static int TierLevel(Pawn p,int vpeLevel){var a=Adventurer.Enabled?Adventurer.Get(p):null;return a!=null?a.level:vpeLevel;}
  public static int TierFor(MartialInfo i,int level){
   if(i==null)return 0;
   if(i.fighter && level>=TierLevels[2])return 3;
   if(i.fighter && level>=TierLevels[1])return 2;
   return level>=TierLevels[0]?1:0;
  }
  public static bool MonkOnly(MartialInfo i)=>i!=null && i.monk && !i.fighter && !i.paladin;
  public static void Reset(){Cache.Clear();infos.Clear();Shots.Clear();ShotFlags.Clear();Hediff_MartialFlag.generation++;}

  // 每 250 刻（MapComponent_ClassAuras.Refresh 的同一次遍历）与解锁路径后：重算这个人的物理路径、VPE 加成、额外攻击档位。
  public static void Refresh(Pawn p){
   if(p==null)return;int id=p.thingIDNumber;
   var h=p.Dead?null:Classes.Psycasts(p);bool any=false,fighter=false,paladin=false,monk=false;
   if(h?.unlockedPaths!=null)for(int k=0;k<h.unlockedPaths.Count;k++){
    if(!(h.unlockedPaths[k] is LP_ClassPathDef c) || !c.martial)continue;any=true;
    if(c.defName==Classes.FighterPath)fighter=true;else if(c.defName==Classes.PaladinPath)paladin=true;else if(c.defName==Classes.MonkPath)monk=true;
   }
   if(!any){if(Cache.Remove(id)){infos.Remove(id);if(!p.Dead)Items.Remove(p,"LP_ExtraAttack");}return;}
   if(!infos.TryGetValue(id,out var i))infos[id]=i=new MartialInfo();
   i.fighter=fighter;i.paladin=paladin;i.monk=monk;i.vpeLevel=h.level;i.vpeBonus=BonusFor(h.level);
   int tier=TierFor(i,TierLevel(p,h.level));
   if(MonkOnly(i) && !Monk.Unarmed(p))tier=0; // 只有武僧：徒手时才有
   i.tier=tier;Cache.Add(id);
   if(tier>0){var x=Items.Give(p,"LP_ExtraAttack");if(x!=null && x.Severity!=tier)x.Severity=tier;}
   else Items.Remove(p,"LP_ExtraAttack");
   if(Items.Has(p,"LP_WeaponMastery"))SetFlag(p,FlagMastery,true); // 1.53.0 以前存档里的武器专精状态（旧类）
  }

  // ---------- 斗志 ----------
  public static MartialState State=>GameComp<MartialState>.Get();
  public static float Fury(Pawn p,float amount){
   if(p?.psychicEntropy==null || !p.HasPsylink || amount<=0f)return 0f;
   var st=State;float allowed=st==null?amount:st.AllowFury(p,amount);
   if(allowed>0f)p.psychicEntropy.OffsetPsyfocusDirectly(allowed);
   return allowed;
  }
  // 斗志算的目标：敌对、未倒地（命中时）、不是召唤物或囚犯。建筑不走 Pawn.PreApplyDamage。
  public static bool FuryTarget(Pawn attacker,Pawn victim,bool hit)=>victim!=null && victim!=attacker && (!hit || !victim.Downed) && !victim.IsPrisoner && victim.HostileTo(attacker) && !ClassRules.Conjured(victim);
  public static float OnHit(Pawn victim,Pawn attacker)=>FuryTarget(attacker,victim,true)?Fury(attacker,FuryHit):0f;
  public static float OnKill(Pawn victim,Pawn killer)=>FuryTarget(killer,victim,false)?Fury(killer,FuryKill):0f;

  // ---------- 射击状态位 ----------
  public const byte FlagSteady=1,FlagSuppressed=2,FlagDeadEye=4,FlagRapid=8,FlagMastery=16;
  public const float SteadyAim=1.15f,SuppressedAim=.75f,DeadEyeDamage=1.5f;
  public static readonly Dictionary<Pawn,byte> ShotFlags=new Dictionary<Pawn,byte>();
  public static byte Flags(Pawn p){if(p==null || ShotFlags.Count==0)return 0;ShotFlags.TryGetValue(p,out byte b);return b;}
  public static void SetFlag(Pawn p,byte flag,bool on){
   if(p==null || flag==0)return;ShotFlags.TryGetValue(p,out byte b);
   byte n=on?(byte)(b|flag):(byte)(b&~flag);
   if(n==0)ShotFlags.Remove(p);else ShotFlags[p]=n;
  }
  // Feats.AimHook：稳固瞄准 ×1.15、被压制 ×0.75；死神之眼 = 必中（≥ 100）。
  public static float Aim(Pawn p){
   if(ShotFlags.Count==0 || p==null || !ShotFlags.TryGetValue(p,out byte b))return 1f;
   if((b & FlagDeadEye)!=0)return Feats.MustHit;
   float f=1f;if((b & FlagSteady)!=0)f*=SteadyAim;if((b & FlagSuppressed)!=0)f*=SuppressedAim;
   return f;
  }
  public static bool Ranged(DamageInfo d)=>d.Weapon!=null && d.Weapon.IsRangedWeapon;
  // 远程伤害倍率：武器专精（同近战那一档）、死神之眼 ×1.5。调用前已确认 ShotFlags 有这个人。
  public static float RangedFactor(Pawn a,byte b){
   float f=1f;
   if((b & FlagMastery)!=0){var def=Items.Hediff("LP_WeaponMastery");var h=def==null?null:a.health.hediffSet.GetFirstHediffOfDef(def);var st=h?.CurStage?.statFactors;if(st!=null)for(int i=0;i<st.Count;i++)if(st[i].stat?.defName=="VEF_MeleeAttackDamageFactor")f*=st[i].value;}
   if((b & FlagDeadEye)!=0)f*=DeadEyeDamage;
   return f;
  }

  // ---------- 登记的子弹（压制射击、精准射击） ----------
  public static readonly Dictionary<Projectile,ShotMod> Shots=new Dictionary<Projectile,ShotMod>();
  public static void Cleanup(){
   if(Shots.Count>0)foreach(var k in Shots.Keys.Where(x=>x==null || x.Destroyed).ToList())Shots.Remove(k);
   if(ShotFlags.Count>0)foreach(var k in ShotFlags.Keys.Where(x=>x==null || x.Destroyed || x.Dead).ToList())ShotFlags.Remove(k);
  }
  public static void Impacted(Projectile proj,Thing hit){
   if(!Shots.TryGetValue(proj,out var m))return;Shots.Remove(proj);
   if(!m.precision || !(hit is Pawn t) || t.Dead)return;
   if(m.mode==PrecisionMode.Leg){Items.Give(t,"LP_Hobbled",600);Items.Mote(t,"打腿",Color);}
   else if(m.mode==PrecisionMode.Hand && CanDisarm(t))Disarm(m.caster,t,DisarmChance(m.caster,t,m.power,Adventurer.Dex));
  }
  public static ThingWithComps RangedWeapon(Pawn p){var w=p?.equipment?.Primary;return w!=null && w.def.IsRangedWeapon?w:null;}
  public static Verb_LaunchProjectile RangedVerb(Pawn p)=>p?.equipment?.PrimaryEq?.PrimaryVerb as Verb_LaunchProjectile;
  // 用当前武器的子弹打一发：launcher = 施法者，equipment = 武器（Isekai 等远程加成照常），倍率登记到 Shots。
  public static Projectile Fire(Pawn caster,LocalTargetInfo target,ShotMod mod,bool mustHit){
   var verb=RangedVerb(caster);var def=verb?.Projectile;var map=caster?.Map;if(def==null || map==null)return null;
   var proj=(Projectile)GenSpawn.Spawn(def,caster.Position,map);
   if(mod!=null)Shots[proj]=mod;
   var hit=mustHit?ProjectileHitFlags.IntendedTarget:ProjectileHitFlags.All;
   proj.Launch(caster,caster.DrawPos,target,target,hit,false,verb.EquipmentSource,null);
   return proj;
  }

  // ---------- 缴械（缴械击、精准射击·打手） ----------
  static int ModProf(Pawn p,int a){var h=Adventurer.Enabled?Adventurer.Get(p):null;return h==null?2:h.Mod(a)+h.Proficiency;}
  public static float DisarmChance(Pawn caster,Pawn t,float power,int ability){
   float c=Mathf.Clamp(.5f+.1f*(ModProf(caster,ability)-ModProf(t,Adventurer.Str))+.2f*(power-1f),.15f,.9f);
   return t.BodySize>=2f?c*.5f:c;
  }
  public static bool CanDisarm(Pawn t)=>t!=null && !t.Dead && t.RaceProps.Humanlike && t.equipment?.Primary!=null;
  public static bool Disarm(Pawn caster,Pawn t,float chance){
   if(!CanDisarm(t) || !Rand.Chance(chance))return false;
   var map=t.Map;var at=t.Position;
   if(!t.equipment.TryDropEquipment(t.equipment.Primary,out var dropped,at,false) || dropped==null)return false;
   if(map!=null){
    var cells=GenRadial.RadialCellsAround(at,2.5f,false).Where(c=>c.InBounds(map) && c.DistanceTo(at)>=1.9f && c.Standable(map)).ToList();
    if(cells.Count>0){if(dropped.Spawned)dropped.DeSpawn();GenPlace.TryPlaceThing(dropped,cells.RandomElement(),map,ThingPlaceMode.Near);}
   }
   Items.Mote(t,"缴械",Color);
   return true;
  }

  // ---------- 主程序集的登记 ----------
  public static void Register(){
   Styles.SlotsHook=Slots;
   ITab_Adventurer.ClassLinesHook=Lines;
   ITab_Adventurer.LevelUpLinesHook=LevelUpLines;
   Feats.AimHook=Aim;
  }
  // 战斗风格槽：有物理路径 1 个；战士且冒险者 ≥ 10（冒险者关时 VPE 灵能 ≥ 10）2 个。
  public static int Slots(Pawn p){var i=Is(p)?Info(p):null;if(i==null)return 0;return i.fighter && TierLevel(p,i.vpeLevel)>=10?2:1;}
  public static string TierText(int tier)=>"近战冷却 ×"+MeleeCooldown[tier].ToString("0.00")+"，追击 "+FollowUp[tier].ToStringPercent()+"，射击冷却 ×"+RangedCooldown[tier].ToString("0.00");
  const string TierTable="额外攻击（物理路径）：\n第 1 档：任一物理路径 + 冒险者 5 级 — 近战冷却 ×0.80，追击 15%，射击冷却 ×0.85\n第 2 档：战士 + 冒险者 11 级 — 近战冷却 ×0.70，追击 25%，射击冷却 ×0.75\n第 3 档：战士 + 冒险者 20 级 — 近战冷却 ×0.60，追击 35%，射击冷却 ×0.65\n\n冒险者总开关关闭或没有冒险者等级时改看 VPE 灵能等级。只有武僧路径的人只在徒手时生效。\n追击：普通近战命中后再按原版近战结算打一下（Isekai 力量、武器品质照常）；追击不再触发追击。";
  public static List<string> Lines(Pawn p){
   var i=Is(p)?Info(p):null;if(i==null)return null;
   var list=new List<string>();
   if(i.tier>0)list.Add("额外攻击 第 "+i.tier+" 档："+TierText(i.tier)+"|"+TierTable);
   else list.Add((MonkOnly(i) && TierLevel(p,i.vpeLevel)>=TierLevels[0]?"额外攻击：只在徒手时生效":"额外攻击：冒险者 5 级解锁")+"|"+TierTable);
   float got=State?.FuryThisHour(p)??0f;
   list.Add("斗志：本小时 +"+(got*100f).ToString("0.#")+"% / "+(FuryCap*100f).ToString("0")+"%|斗志：对敌对且未倒地的目标造成伤害时精神力 +0.4%，击杀敌人 +3%；每人每游戏小时最多 +15%。没有心灵链接时不加；召唤物、友军、囚犯不算。");
   list.Add("VPE 灵能等级 "+i.vpeLevel+"：战技强度、射术强度 +"+(i.vpeBonus*100f).ToString("0")+"%|每比 1 级高 1 级 +2%，最多 +40%（21 级）。只加格斗、射击的职业强度；施法职业的智识、社交、种植、驯兽不加。");
   return list;
  }
  public static IEnumerable<string> LevelUpLines(Pawn p,int level){
   var i=Is(p)?Info(p):null;if(i==null)yield break;
   if(level==TierLevels[0])yield return "额外攻击 第 1 档："+TierText(1)+(MonkOnly(i)?"（武僧：徒手时）":"");
   if(i.fighter && level==10)yield return "可以再选一个战斗风格（战斗风格 ② 槽位）";
   if(i.fighter && level==TierLevels[1])yield return "额外攻击 第 2 档："+TierText(2);
   if(i.fighter && level==TierLevels[2])yield return "额外攻击 第 3 档："+TierText(3);
  }

  // ---------- 追击 ----------
  public static float? FollowUpOverride; // 自检用
  public static int FollowUps; // 自检用：追击次数
  [ThreadStatic] public static bool inFollowUp;
  public static bool TryFollowUp(Verb_MeleeAttackDamage verb,Pawn caster,Pawn t){
   if(inFollowUp)return false;
   int tier=Tier(caster);if(tier<=0)return false;
   var src=verb.EquipmentSource;if(src!=null && src!=caster.equipment?.Primary)return false; // 副手那一刀不追击
   if(!Rand.Chance(FollowUpOverride??FollowUp[tier]))return false;
   inFollowUp=true;FollowUps++;
   try{ClassMelee.Strike(caster,t);}finally{inFollowUp=false;}
   Items.Mote(caster,"追击",Color);
   return true;
  }
 }

 // 斗志的每小时计数、精准射击模式。
 public class MartialState : GameComponent {
  public Dictionary<int,float> furyGiven=new Dictionary<int,float>();
  public int furyHour=-1;
  public Dictionary<int,int> precisionMode=new Dictionary<int,int>();
  public MartialState(Game game){}
  int Hour=>Find.TickManager.TicksGame/Martial.HourTicks;
  public float AllowFury(Pawn p,float want){
   int h=Hour;if(h!=furyHour){furyHour=h;furyGiven.Clear();}
   furyGiven.TryGetValue(p.thingIDNumber,out float given);
   float allowed=Mathf.Max(0f,Mathf.Min(want,Martial.FuryCap-given));
   if(allowed>0f)furyGiven[p.thingIDNumber]=given+allowed;
   return allowed;
  }
  public float FuryThisHour(Pawn p){if(p==null || Hour!=furyHour)return 0f;furyGiven.TryGetValue(p.thingIDNumber,out float g);return g;}
  public PrecisionMode Mode(Pawn p){precisionMode.TryGetValue(p.thingIDNumber,out int m);return (PrecisionMode)Mathf.Clamp(m,0,2);}
  public void SetMode(Pawn p,PrecisionMode m){if(m==PrecisionMode.Vital)precisionMode.Remove(p.thingIDNumber);else precisionMode[p.thingIDNumber]=(int)m;}
  public override void FinalizeInit(){base.FinalizeInit();Martial.Reset();}
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref furyHour,"lpFuryHour",-1);
   Scribe_Collections.Look(ref furyGiven,"lpFuryGiven",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref precisionMode,"lpPrecisionMode",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(furyGiven==null)furyGiven=new Dictionary<int,float>();if(precisionMode==null)precisionMode=new Dictionary<int,int>();}
  }
 }

 // 射击状态位的来源：稳固瞄准、被压制、死神之眼、连射、武器专精。加上 / 移除时维护 Martial.ShotFlags（Reset 后在下一次刻里重新登记）。
 public class Hediff_MartialFlag : HediffWithComps {
  public static int generation;int seen=-1;
  public byte Flag{get{switch(def.defName){case "LP_SteadyAim":return Martial.FlagSteady;case "LP_Suppressed":return Martial.FlagSuppressed;case "LP_DeadEye":return Martial.FlagDeadEye;case "LP_RapidFire":return Martial.FlagRapid;case "LP_WeaponMastery":return Martial.FlagMastery;default:return 0;}}}
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);Martial.SetFlag(pawn,Flag,true);seen=generation;}
  public override void PostRemoved(){base.PostRemoved();Martial.SetFlag(pawn,Flag,false);}
  public override void TickInterval(int delta){base.TickInterval(delta);if(seen!=generation){seen=generation;if(!ShouldRemove)Martial.SetFlag(pawn,Flag,true);}}
 }

 // ==================== Harmony ====================
 // 解锁路径后立刻刷新这个人（不等 250 刻）。
 [HarmonyPatch(typeof(Hediff_PsycastAbilities),nameof(Hediff_PsycastAbilities.UnlockPath))]
 public static class MartialUnlockPath {
  static void Postfix(Hediff_PsycastAbilities __instance,PsycasterPathDef path){if(path is LP_ClassPathDef c && c.martial && __instance?.pawn!=null)Martial.Refresh(__instance.pawn);}
 }
 // 登记过的子弹：伤害倍率、穿甲加值。快速返回：Shots 为空。
 [HarmonyPatch(typeof(Projectile),nameof(Projectile.DamageAmount),MethodType.Getter)]
 public static class MartialShotDamage {
  static void Postfix(Projectile __instance,ref int __result){if(Martial.Shots.Count==0 || !Martial.Shots.TryGetValue(__instance,out var m))return;__result=Mathf.Max(1,Mathf.RoundToInt(__result*m.damage));}
 }
 [HarmonyPatch(typeof(Projectile),nameof(Projectile.ArmorPenetration),MethodType.Getter)]
 public static class MartialShotPenetration {
  static void Postfix(Projectile __instance,ref float __result){if(Martial.Shots.Count==0 || !Martial.Shots.TryGetValue(__instance,out var m))return;__result+=m.ap;}
 }
 // 命中后移除：子弹（Bullet）先调 base.Impact 再结算伤害，所以子弹在 Bullet.Impact 之后移除，其他投射物在 Projectile.Impact 之后。快速返回：Shots 为空。
 [HarmonyPatch(typeof(Bullet),"Impact")]
 public static class MartialShotBulletImpact {
  static void Postfix(Bullet __instance,Thing hitThing){if(Martial.Shots.Count>0)Martial.Impacted(__instance,hitThing);}
 }
 [HarmonyPatch(typeof(Projectile),"Impact")]
 public static class MartialShotImpact {
  static void Postfix(Projectile __instance,Thing hitThing){if(Martial.Shots.Count>0 && !(__instance is Bullet))Martial.Impacted(__instance,hitThing);}
 }
 // 连射：连发武器每轮多 1 发。原版在 WarmupComplete 里才设 burstShotsLeft（TryStartCastOn 时设了会被覆盖），所以挂在它之后：第一发打完、仍在连发中时 +1。
 // 快速返回：ShotFlags 为空。
 [HarmonyPatch(typeof(Verb),"WarmupComplete")]
 public static class MartialRapidFireBurst {
  static readonly AccessTools.FieldRef<Verb,int> ShotsLeft=AccessTools.FieldRefAccess<Verb,int>("burstShotsLeft");
  static void Postfix(Verb __instance){
   if(Martial.ShotFlags.Count==0 || !(__instance is Verb_LaunchProjectile) || __instance.state!=VerbState.Bursting)return;
   var p=__instance.CasterPawn;if(p==null || (Martial.Flags(p) & Martial.FlagRapid)==0 || __instance.BurstShotCount<=1)return;
   ShotsLeft(__instance)++;
  }
 }
 // 精准射击的模式切换按钮（征召、学会精准射击的人）。快速返回：不是物理路径者或没征召。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class MartialPrecisionGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   foreach(var g in __result)yield return g;
   if(!Martial.Is(__instance) || !__instance.Drafted || __instance.Faction!=Faction.OfPlayer || !ClassRules.Knows(__instance,"LP_PrecisionShot"))yield break;
   var st=Martial.State;if(st==null)yield break;
   var p=__instance;var mode=st.Mode(p);
   yield return new Command_Action{
    defaultLabel="精准射击："+Ability_PrecisionShot.ModeLabel(mode),
    defaultDesc="切换精准射击的模式：\n要害：伤害 ×(1.5 + 0.5 × 射术强度)，穿甲 +30%。\n打腿：伤害 ×0.7，目标移动 ×0.5 持续 10 秒。\n打手：伤害 ×0.5，按缴械击的公式判定打落武器。",
    icon=ContentFinder<Texture2D>.Get("LP/Classes/Fighter/PrecisionShot",false)??BaseContent.BadTex,
    action=()=>st.SetMode(p,(PrecisionMode)(((int)st.Mode(p)+1)%3))
   };
  }
 }
 // VPE 的灵能树一行最多画 3 个能力（abilityTreeXOffsets）。战士 1.53.0 起第 1、3 环各 4 个：DND 路径有超过 3 个的行时由这里按同样的规则画（间距按宽度收紧）。
 [HarmonyPatch(typeof(PsycastsUIUtility),nameof(PsycastsUIUtility.DoPathAbilities))]
 public static class MartialWideTree {
  static readonly Dictionary<PsycasterPathDef,bool> wide=new Dictionary<PsycasterPathDef,bool>();
  public static bool Wide(PsycasterPathDef path){if(!wide.TryGetValue(path,out bool w))wide[path]=w=path.abilityLevelsInOrder!=null && path.abilityLevelsInOrder.Any(r=>r!=null && r.Length>3);return w;}
  public static float Offset(int n,int pos,float width){
   if(n<=1)return -18f;if(n==2)return pos==0?-47f:11f;if(n==3)return new[]{-69f,-18f,33f}[pos];
   float s=Mathf.Min(42f,(width-40f)/(n-1));return -18f+(pos-(n-1)/2f)*s;
  }
  static bool Prefix(Rect inRect,PsycasterPathDef path,Dictionary<AbilityDef,Vector2> abilityPos,Action<Rect,AbilityDef> doAbility){
   if(!(path is LP_ClassPathDef) || !Wide(path))return true;
   var comp=PsycastsUIUtility.CompAbilities;if(comp==null || PsycastsUIUtility.Hediff==null)return true;
   foreach(var def in path.abilities)
    if(def.Psycast()?.prerequisites is List<AbilityDef> pre && abilityPos.ContainsKey(def))
     foreach(var a in pre)if(abilityPos.ContainsKey(a))Widgets.DrawLine(abilityPos[def],abilityPos[a],comp.HasAbility(a)?Color.white:Color.grey,2f);
   for(int level=0;level<path.abilityLevelsInOrder.Length;level++){
    var levelRect=new Rect(inRect.x,inRect.y+(path.MaxLevel-1-level)*inRect.height/path.MaxLevel+10f,inRect.width,inRect.height/5f);
    var row=path.abilityLevelsInOrder[level];
    for(int pos=0;pos<row.Length;pos++){
     var rect=new Rect(levelRect.x+levelRect.width/2f+Offset(row.Length,pos,levelRect.width),levelRect.y,36f,36f);
     var def=row[pos];if(def==PsycasterPathDef.Blank)continue;
     abilityPos[def]=rect.center;doAbility(rect,def);
    }
   }
   return false;
  }
 }
}
