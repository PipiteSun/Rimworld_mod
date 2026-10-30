using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.72.0 武人之道（1.53.0 起在 VPE 桥的 Martial.cs，现在移到主程序集，读新的职业等级）：物理职业（战士、圣武士、武僧）共用的斗志、强度加成、额外攻击，
 // 战士射击支的“射击状态位”（ShotFlags）和登记过的子弹（Shots），圣武士、武僧的命中 / 受伤 / 击杀效果。
 // 性能：Cache 只在职业者自己的冒险者状态每 250 刻（ClassSystem.Tick250）和加点时更新；所有命中、伤害、击杀挂钩第一步都是 Is(p) 或空字典判断。
 public class MartialInfo {public bool fighter,paladin,monk;public int tier,fighterLevel,best,total;public float bonus;}
 public enum PrecisionMode{Vital,Leg,Hand}
 public class ShotMod {public float damage=1f,ap,power=1f;public PrecisionMode mode;public bool precision;public Pawn caster;}

 [StaticConstructorOnStartup]
 public static class ClassStartup {
  static ClassStartup(){
   Styles.SlotsHook=Martial.Slots;
   ITab_Adventurer.ClassLinesHook=Martial.Lines;
   Feats.AimHook=Martial.Aim;
   PortalEvent.WizardHook=p=>ClassSystem.Level(p,DefCache<LP_ClassDef>.Get("LP_Class_Wizard"))>0; // 1.73.0：法师职业的殖民者吸引位面裂口（原来在 VPE 桥）
  }
 }

 public static class Martial {
  public const float FuryHit=.004f,FuryKill=.03f,FuryCap=.15f,FuryResourcePer=20f;
  public const int HourTicks=2500;
  public static readonly int[] TierLevels={5,11,20};
  public static readonly float[] FollowUp={0f,.15f,.25f,.35f},MeleeCooldown={1f,.8f,.7f,.6f},RangedCooldown={1f,.85f,.75f,.65f};
  public static readonly Color Color=new Color(.95f,.6f,.4f);

  // ---------- 物理职业缓存 ----------
  public static readonly HashSet<int> Cache=new HashSet<int>();
  static readonly Dictionary<int,MartialInfo> infos=new Dictionary<int,MartialInfo>();
  public static bool Is(Pawn p)=>p!=null && Cache.Contains(p.thingIDNumber);
  public static MartialInfo Info(Pawn p){if(p==null)return null;infos.TryGetValue(p.thingIDNumber,out var i);return i;}
  public static float Bonus(Pawn p){if(p==null || !Cache.Contains(p.thingIDNumber))return 0f;return infos.TryGetValue(p.thingIDNumber,out var i)?i.bonus:0f;}
  public static int Tier(Pawn p)=>Is(p)?Info(p)?.tier??0:0;
  public static int TierFor(MartialInfo i){
   if(i==null)return 0;
   if(i.fighterLevel>=TierLevels[2])return 3;
   if(i.fighterLevel>=TierLevels[1])return 2;
   return i.best>=TierLevels[0]?1:0;
  }
  public static bool MonkOnly(MartialInfo i)=>i!=null && i.monk && !i.fighter && !i.paladin;
  public static void Reset(){Cache.Clear();infos.Clear();Shots.Clear();ShotFlags.Clear();Hediff_MartialFlag.generation++;}
  public static void Refresh(Pawn p){
   if(p==null)return;int id=p.thingIDNumber;
   var h=p.Dead?null:Adventurer.Get(p);int f=0,pa=0,mo=0;
   if(h?.classes!=null){f=ClassSystem.Level(h,ClassSystem.Fighter);pa=ClassSystem.Level(h,ClassSystem.Paladin);mo=ClassSystem.Level(h,ClassSystem.Monk);}
   if(f+pa+mo<=0){if(Cache.Remove(id)){infos.Remove(id);if(!p.Dead)Items.Remove(p,"LP_ExtraAttack");}return;}
   if(!infos.TryGetValue(id,out var i))infos[id]=i=new MartialInfo();
   i.fighter=f>0;i.paladin=pa>0;i.monk=mo>0;i.fighterLevel=f;i.best=Mathf.Max(f,Mathf.Max(pa,mo));i.total=f+pa+mo;i.bonus=ClassSystem.MartialBonusFor(i.total);
   int tier=TierFor(i);
   if(MonkOnly(i) && !MonkCore.Unarmed(p))tier=0; // 只有武僧：徒手时才有
   i.tier=tier;Cache.Add(id);
   if(tier>0){var x=Items.Give(p,"LP_ExtraAttack");if(x!=null && x.Severity!=tier)x.Severity=tier;}
   else Items.Remove(p,"LP_ExtraAttack");
  }
  public static void RefreshStance(Pawn p)=>MonkCore.RefreshStance(p);

  // ---------- 斗志 ----------
  public static GameComponent_Classes State=>GameComp<GameComponent_Classes>.Get();
  // 斗志：有心灵链接时照旧加精神力；战士另加斗志资源（× FuryResourcePer）。每小时上限按精神力的量算。
  public static float Fury(Pawn p,float amount){
   if(p==null || amount<=0f)return 0f;
   var st=State;float allowed=st==null?amount:st.AllowFury(p,amount);
   if(allowed<=0f)return 0f;
   if(p.psychicEntropy!=null && p.HasPsylink)p.psychicEntropy.OffsetPsyfocusDirectly(allowed);
   var h=Adventurer.Get(p);if(h!=null)ClassSystem.AddResource(h,ClassSystem.Fighter,allowed*FuryResourcePer);
   return allowed;
  }
  public static bool Conjured(Pawn p)=>Items.Has(p,"LP_Conjured");
  public static bool FuryTarget(Pawn attacker,Pawn victim,bool hit)=>victim!=null && victim!=attacker && (!hit || !victim.Downed) && !victim.IsPrisoner && victim.HostileTo(attacker) && !Conjured(victim);
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
  public static float Aim(Pawn p){
   if(ShotFlags.Count==0 || p==null || !ShotFlags.TryGetValue(p,out byte b))return 1f;
   if((b & FlagDeadEye)!=0)return Feats.MustHit;
   float f=1f;if((b & FlagSteady)!=0)f*=SteadyAim;if((b & FlagSuppressed)!=0)f*=SuppressedAim;
   return f;
  }
  public static bool Ranged(DamageInfo d)=>d.Weapon!=null && d.Weapon.IsRangedWeapon;
  public static float RangedFactor(Pawn a,byte b){
   float f=1f;
   if((b & FlagMastery)!=0){var def=Items.Hediff("LP_WeaponMastery");var h=def==null?null:a.health.hediffSet.GetFirstHediffOfDef(def);var st=h?.CurStage?.statFactors;if(st!=null)for(int i=0;i<st.Count;i++)if(st[i].stat==StatDefOf.MeleeDamageFactor)f*=st[i].value;}
   if((b & FlagDeadEye)!=0)f*=DeadEyeDamage;
   return f;
  }

  // ---------- 登记的子弹 ----------
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
  public static Verb_LaunchProjectile RangedVerb(Pawn p)=>p?.equipment?.PrimaryEq?.PrimaryVerb as Verb_LaunchProjectile;
  public static Projectile Fire(Pawn caster,LocalTargetInfo target,ShotMod mod,bool mustHit){
   var verb=RangedVerb(caster);var def=verb?.Projectile;var map=caster?.Map;if(def==null || map==null)return null;
   var proj=(Projectile)GenSpawn.Spawn(def,caster.Position,map);
   if(mod!=null)Shots[proj]=mod;
   proj.Launch(caster,caster.DrawPos,target,target,mustHit?ProjectileHitFlags.IntendedTarget:ProjectileHitFlags.All,false,verb.EquipmentSource,null);
   return proj;
  }

  // ---------- 缴械 ----------
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

  // ---------- 冒险者分页 ----------
  // 战斗风格槽：有物理职业 1 个；战士 10 级 2 个。
  public static int Slots(Pawn p){var i=Is(p)?Info(p):null;if(i==null)return 0;return i.fighterLevel>=10?2:1;}
  public static string TierText(int tier)=>"近战冷却 ×"+MeleeCooldown[tier].ToString("0.00")+"，追击 "+FollowUp[tier].ToStringPercent()+"，射击冷却 ×"+RangedCooldown[tier].ToString("0.00");
  const string TierTable="额外攻击（物理职业）：\n第 1 档：任一物理职业 5 级 — 近战冷却 ×0.80，追击 15%，射击冷却 ×0.85\n第 2 档：战士 11 级 — 近战冷却 ×0.70，追击 25%，射击冷却 ×0.75\n第 3 档：战士 20 级 — 近战冷却 ×0.60，追击 35%，射击冷却 ×0.65\n\n只有武僧的人只在徒手时生效。\n追击：普通近战命中后再按原版近战结算打一下（Isekai 力量、武器品质照常）；追击不再触发追击。";
  public static List<string> Lines(Pawn p){
   var i=Is(p)?Info(p):null;if(i==null)return null;
   var list=new List<string>();
   if(i.tier>0)list.Add("额外攻击 第 "+i.tier+" 档："+TierText(i.tier)+"|"+TierTable);
   else list.Add((MonkOnly(i) && i.best>=TierLevels[0]?"额外攻击：只在徒手时生效":"额外攻击：物理职业 5 级解锁")+"|"+TierTable);
   float got=State?.FuryThisHour(p)??0f;
   list.Add("斗志：本小时 +"+(got*FuryResourcePer).ToString("0.#")+" / "+(FuryCap*FuryResourcePer).ToString("0")+"|"+ClassSystem.ResourceTip(ClassSystem.Fighter)+"\n召唤物、友军、囚犯不算。");
   list.Add("物理职业等级 "+i.total+"：战技强度、射术强度 +"+(i.bonus*100f).ToString("0")+"%|物理职业（战士、武僧、圣武士）等级合计每比 1 高 1 级 +2%，最多 +40%。只加格斗、射击的职业强度。");
   return list;
  }

  // ---------- 追击 ----------
  public static float? FollowUpOverride;
  public static int FollowUps;
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
  // 战意再临 / 死神之眼：击杀后所有战士能力冷却完。
  public static int ResetFighter(Pawn killer){
   var list=killer?.abilities?.abilities;if(list==null)return 0;int n=0;var f=ClassSystem.Fighter;
   foreach(var a in list)if(ClassSystem.ClassOf(a.def)==f && a.OnCooldown){a.ResetCooldown();n++;}
   return n;
  }
 }

 // 武僧：徒手、无甲、架势、拨挡、颤心。
 public static class MonkCore {
  public static readonly Color Ki=new Color(.95f,.8f,.4f);
  public static readonly HashSet<string> MonkWeapons=new HashSet<string>{"MeleeWeapon_Club"};
  public static bool Unarmed(Pawn p)=>p?.equipment!=null && (p.equipment.Primary==null || MonkWeapons.Contains(p.equipment.Primary.def.defName));
  public static bool Unarmored(Pawn p)=>p?.apparel==null || !p.apparel.WornApparel.Any(a=>a.GetStatValue(StatDefOf.ArmorRating_Sharp)>=.4f);
  public const int AuraTicks=300;
  public static float Strength(Pawn p){var s=DefCache<StatDef>.Get("LP_MartialPower");return s==null?1f:p.GetStatValue(s);}
  public static void RefreshStance(Pawn p){
   float m=Strength(p);
   if(Unarmed(p))Items.Give(p,"LP_MonkStance",AuraTicks,m);else Items.Remove(p,"LP_MonkStance");
   if(Unarmored(p))Items.Give(p,"LP_MonkUnarmored",AuraTicks,m);else Items.Remove(p,"LP_MonkUnarmored");
   foreach(var name in new[]{"LP_MonkStance","LP_MonkUnarmored"}){var def=Items.Hediff(name);var h=def==null?null:p.health.hediffSet.GetFirstHediffOfDef(def);if(h==null)continue;if(!(h is Hediff_ScaledBuff)){p.health.RemoveHediff(h);continue;}h.Severity=m;} // 旧存档里桥的旧类：拿掉，下一次刷新重给
  }
  static readonly DefRef<HediffDef> Deflecting=new DefRef<HediffDef>("LP_Deflecting");
  public static bool Deflect(Pawn victim,ref DamageInfo dinfo){
   var def=Deflecting.Def;var h=def==null?null:victim.health?.hediffSet?.GetFirstHediffOfDef(def);
   if(h==null || !ForceDragon.IsRanged(dinfo))return false;
   float left=dinfo.Amount-h.Severity;
   if(left<=0f){Items.Mote(victim,"拨开",Ki);dinfo.SetAmount(0f);return true;}
   dinfo.SetAmount(left);return false;
  }
  public static int FlurryHits(float strength)=>strength>=1.25f?3:2;
  public static int StunTicks(Pawn t,float strength)=>Mathf.RoundToInt(120f*strength*(t.BodySize>=2f?.5f:1f));
  public static IntVec3 Push(Pawn caster,Pawn t,float strength){
   ClassMelee.Strike(caster,t);if(t.Dead || !t.Spawned)return IntVec3.Invalid;
   var dest=DNDPush.Destination(t.Map,t.Position,(t.Position-caster.Position).ToVector3(),t.BodySize>=2f?1:3);
   DNDPush.Move(t,dest);
   t.stances?.stunner?.StunFor(60,caster);
   foreach(var o in Races124.PawnsNear(t.Map,t.Position,1.5f).ToList())if(o!=t && o!=caster && o.HostileTo(caster))o.TakeDamage(new DamageInfo(DamageDefOf.Blunt,6f*strength,.1f,-1f,caster));
   Items.Mote(t,"开掌技",Ki);
   return t.Position;
  }
  public static Hediff_QuiveringPalm Plant(Pawn caster,Pawn t,float amount){
   var h=Items.Give(t,"LP_QuiveringPalm",-1,amount) as Hediff_QuiveringPalm;
   if(h!=null){h.caster=caster;h.Severity=amount;}
   Items.Mote(t,"颤心",Ki);
   return h;
  }
  public static float BurstAmount(Pawn t,float amount)=>amount*(t.BodySize>=3f || GiantKin.Is(t)?.5f:1f);
 }
 public class Hediff_QuiveringPalm : HediffWithComps {
  public Pawn caster;public int ticksLeft=180;
  public override void TickInterval(int delta){base.TickInterval(delta);ticksLeft-=delta;if(ticksLeft<=0)Burst();}
  public float Burst(){
   var p=pawn;float amount=MonkCore.BurstAmount(p,Severity);
   p.health.RemoveHediff(this);
   if(p.Dead)return 0f;
   p.TakeDamage(new DamageInfo(DamageDefOf.Blunt,amount,.5f,-1f,caster,p.RaceProps.body.corePart));
   if(p.Spawned)FleckMaker.ThrowLightningGlow(p.DrawPos,p.Map,1.5f);
   return amount;
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref caster,"lpPalmCaster");Scribe_Values.Look(ref ticksLeft,"lpPalmTicks",180);}
 }

 // 圣武士：至圣斩 / 神圣武器、誓仇、圣疗、换位、放逐邪恶、灵光。
 public static class PaladinCore {
  public static readonly Color Holy=new Color(1f,.92f,.6f);
  public const float AuraRadius=5f;
  static readonly DefRef<DamageDef> radiant=new DefRef<DamageDef>("LP_Radiant");
  public static DamageDef Radiant=>radiant.Def??DamageDefOf.Burn;
  public static bool HolyVulnerable(Pawn p)=>p!=null && (Items.Undead(p) || Fiends.Is(p) || MindFlayer.IsMindFlayer(p));
  public static float HolyAmount(Pawn t,float amount)=>amount*(HolyVulnerable(t)?2f:1f)*(Twilight.Vessel(t)!=null?1.5f:1f);
  public static bool AuraTarget(Pawn source,Pawn ally)=>ally!=null && !ally.Dead && (ally==source || ally.Faction!=null && ally.Faction==source.Faction && !ally.HostileTo(source));
  static readonly DefRef<HediffDef> SmiteReady=new DefRef<HediffDef>("LP_SmiteReady"),SacredWeapon=new DefRef<HediffDef>("LP_SacredWeapon");
  public static float OnMeleeHit(Pawn caster,Pawn t){
   float total=0f;
   var smite=SmiteReady.Def;var h=smite==null?null:caster.health?.hediffSet?.GetFirstHediffOfDef(smite);
   if(h!=null){float a=HolyAmount(t,h.Severity);caster.health.RemoveHediff(h);t.TakeDamage(new DamageInfo(Radiant,a,.4f,-1f,caster));total+=a;if(t.Map!=null)FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,1.5f);}
   var sw=SacredWeapon.Def;var w=sw==null?null:caster.health?.hediffSet?.GetFirstHediffOfDef(sw);
   if(w!=null && !t.Dead){float a=HolyAmount(t,w.Severity);t.TakeDamage(new DamageInfo(Radiant,a,.4f,-1f,caster));total+=a;}
   return total;
  }
  public static void ApplyEnmity(Pawn victim,ref DamageInfo dinfo){
   Hediff_Enmity mark=null;var list=victim.health?.hediffSet?.hediffs;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i] is Hediff_Enmity e){mark=e;break;}
   if(mark==null || mark.caster==null || dinfo.Instigator!=mark.caster)return;
   dinfo.SetAmount(dinfo.Amount*EnmityFactor(mark.Severity));
  }
  public static float EnmityFactor(float strength)=>1f+.25f*strength;
  public static void OnKilled(Pawn dead){
   Hediff_Enmity mark=null;var list=dead.health?.hediffSet?.hediffs;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i] is Hediff_Enmity e){mark=e;break;}
   var c=mark?.caster;if(c==null || c.Dead)return;
   var thought=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_EnmityAvenged");
   if(thought!=null)c.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
  }
  public static Hediff_Enmity Mark(Pawn caster,Pawn t,int ticks,float strength){
   foreach(var old in t.health.hediffSet.hediffs.OfType<Hediff_Enmity>().ToList())t.health.RemoveHediff(old);
   var h=Items.Give(t,"LP_Enmity",ticks,strength) as Hediff_Enmity;
   if(h!=null){h.caster=caster;h.Severity=strength;}
   Items.Mote(t,"誓仇",Holy);
   return h;
  }
  public static string Touch(Pawn t,float amount){
   if(ClassMelee.InjurySum(t)>0f)return "圣疗 "+ClassMelee.Heal(t,amount).ToString("0");
   return Cleanse(t,.3f)??"圣疗";
  }
  public static string Cleanse(Pawn t,float toxic){
   var food=t.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.FoodPoisoning);
   if(food!=null){t.health.RemoveHediff(food);return "解除食物中毒";}
   var tox=t.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);
   if(tox!=null){tox.Severity-=toxic;if(tox.Severity<=0f)t.health.RemoveHediff(tox);return "清除毒素";}
   return null;
  }
  public static bool Swap(Pawn a,Pawn b){
   if(!a.Spawned || !b.Spawned || a.Map!=b.Map)return false;
   var pa=a.Position;var pb=b.Position;
   a.pather?.StopDead();b.pather?.StopDead();
   a.Position=pb;b.Position=pa;
   a.Notify_Teleported(false,true);b.Notify_Teleported(false,true);
   Items.Give(b,"LP_Sheltered",600,1f);
   FleckMaker.ThrowLightningGlow(a.DrawPos,a.Map,1f);FleckMaker.ThrowLightningGlow(b.DrawPos,b.Map,1f);
   Items.Mote(b,"受庇护",Holy);
   return true;
  }
  public static int Dispel(Pawn caster,float radius,float amount){
   if(caster.Map==null)return 0;int n=0;
   foreach(var t in Races124.PawnsNear(caster.Map,caster.Position,radius).ToList()){
    if(t==caster || !t.HostileTo(caster))continue;
    if(Martial.Conjured(t)){if(t.Spawned)FleckMaker.ThrowDustPuffThick(t.DrawPos,t.Map,2f,new Color(.6f,.9f,.6f));t.Destroy();n++;continue;}
    if(!HolyVulnerable(t))continue;
    t.TakeDamage(new DamageInfo(Radiant,amount,.4f,-1f,caster));
    if(!t.Dead)t.stances?.stunner?.StunFor(120,caster);
    FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,1.5f);n++;
   }
   return n;
  }
  // 灵光（每 250 刻，圣武士自己的冒险者状态里）：给半径 5 内友军挂 300 刻的状态（多个来源取最强，不叠加）。
  public static int Auras(Pawn p){
   bool prot=ClassSystem.Knows(p,"LP_AuraOfProtection"),courage=ClassSystem.Knows(p,"LP_AuraOfCourage");
   if(!prot && !courage)return 0;
   var s=DefCache<StatDef>.Get("LP_DivinePower");float d=s==null?1f:p.GetStatValue(s);int n=0;
   foreach(var ally in Races124.PawnsNear(p.Map,p.Position,AuraRadius)){
    if(!AuraTarget(p,ally))continue;n++;
    if(prot)Items.Give(ally,"LP_ProtectionAura",MonkCore.AuraTicks,d);
    if(courage){Items.Give(ally,"LP_CourageAura",MonkCore.AuraTicks,1f);Items.Remove(ally,"LP_Shaken");}
   }
   return n;
  }
  public static bool NoPanic(Pawn p,MentalStateDef def)=>def!=null && (def==MentalStateDefOf.PanicFlee || def.defName.Contains("Panic") || def.defName.Contains("Flee")) && (Items.Has(p,"LP_CourageAura") || Items.Has(p,"LP_HolyAvatar"));
 }
 public class Hediff_Enmity : HediffWithComps {
  public Pawn caster;
  public override string LabelInBrackets=>caster==null?base.LabelInBrackets:caster.LabelShort;
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref caster,"lpEnmityCaster");}
 }

 // ---------- 状态类 ----------
 // 射击状态位的来源：稳固瞄准、被压制、死神之眼、连射、武器专精。
 public class Hediff_MartialFlag : HediffWithComps {
  public static int generation;int seen=-1;
  public byte Flag{get{switch(def.defName){case "LP_SteadyAim":return Martial.FlagSteady;case "LP_Suppressed":return Martial.FlagSuppressed;case "LP_DeadEye":return Martial.FlagDeadEye;case "LP_RapidFire":return Martial.FlagRapid;case "LP_WeaponMastery":return Martial.FlagMastery;default:return 0;}}}
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);Martial.SetFlag(pawn,Flag,true);seen=generation;}
  public override void PostRemoved(){base.PostRemoved();Martial.SetFlag(pawn,Flag,false);}
  public override void TickInterval(int delta){base.TickInterval(delta);if(seen!=generation){seen=generation;if(!ShouldRemove)Martial.SetFlag(pawn,Flag,true);}}
 }
 // 旋风之势 / 冲锋之势：近战伤害（原版 MeleeDamageFactor）= 严重度，只在那一下存在。
 public class Hediff_StrikeFactor : HediffWithComps {
  HediffStage stage;float built=-1f;
  public override HediffStage CurStage{get{
   if(stage==null || built!=Severity){built=Severity;stage=new HediffStage{statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.MeleeDamageFactor,value=Severity}}};}
   return stage;
  }}
 }
 // 按严重度换算数值的状态（武僧架势、守护灵光）：值 = base + perSeverity × 严重度，夹在 min～max。
 public class ScaledBuffStat {
  public StatDef stat;public float baseValue;public float perSeverity;public float min=-999f;public float max=999f;
  public float Value(float severity)=>Mathf.Clamp(baseValue+perSeverity*severity,min,max);
 }
 public class ScaledBuffExtension : DefModExtension {
  public List<ScaledBuffStat> scaledFactors=new List<ScaledBuffStat>();
  public List<ScaledBuffStat> scaledOffsets=new List<ScaledBuffStat>();
 }
 public class Hediff_ScaledBuff : HediffWithComps {
  HediffStage stage;float built=-1f;ScaledBuffExtension ext;HediffDef extDef;
  public override HediffStage CurStage{get{
   if(extDef!=def){ext=def.GetModExtension<ScaledBuffExtension>();extDef=def;}
   if(ext==null)return base.CurStage;
   if(stage==null || built!=Severity){
    built=Severity;var src=def.stages?.FirstOrDefault();
    stage=new HediffStage{statFactors=new List<StatModifier>(),statOffsets=new List<StatModifier>()};
    if(src!=null){stage.painFactor=src.painFactor;stage.label=src.label;if(src.statFactors!=null)stage.statFactors.AddRange(src.statFactors);if(src.statOffsets!=null)stage.statOffsets.AddRange(src.statOffsets);if(src.capMods!=null)stage.capMods=src.capMods;}
    foreach(var s in ext.scaledFactors)if(s.stat!=null)stage.statFactors.Add(new StatModifier{stat=s.stat,value=s.Value(Severity)});
    foreach(var s in ext.scaledOffsets)if(s.stat!=null)stage.statOffsets.Add(new StatModifier{stat=s.stat,value=s.Value(Severity)});
   }
   return stage;
  }}
  public float Factor(StatDef st){var m=CurStage?.statFactors?.FirstOrDefault(x=>x.stat==st);return m==null?1f:m.value;}
 }
 // 回气：600 刻内分 10 次治疗（总量 = 严重度）。
 public class HediffCompProperties_ClassHeal : HediffCompProperties {
  public int interval=60,ticksTotal=600;
  public HediffCompProperties_ClassHeal(){compClass=typeof(HediffComp_ClassHeal);}
 }
 public class HediffComp_ClassHeal : HediffComp {
  int ticks;
  public HediffCompProperties_ClassHeal Props=>(HediffCompProperties_ClassHeal)props;
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){base.CompPostTickInterval(ref severityAdjustment,delta);ticks+=delta;while(ticks>=Props.interval){ticks-=Props.interval;Pulse();}}
  public float Pulse()=>ClassMelee.Heal(Pawn,parent.Severity*Props.interval/Props.ticksTotal);
  public override void CompExposeData(){base.CompExposeData();Scribe_Values.Look(ref ticks,"regenTicks");}
 }
 // 每隔一段时间的脉冲：圣光化身（伤敌、治友）、空灵之身（治疗自己）。每次的量 = 状态严重度。
 public class HediffCompProperties_ClassAura : HediffCompProperties {
  public int interval=60;public float radius;public bool damageEnemies;public float healFactor;public bool healAllies;public string allyHediff; // 1.74.0：友军另挂的状态（神圣化身）
  public HediffCompProperties_ClassAura(){compClass=typeof(HediffComp_ClassAura);}
 }
 public class HediffComp_ClassAura : HediffComp {
  int ticks;
  public HediffCompProperties_ClassAura Props=>(HediffCompProperties_ClassAura)props;
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){base.CompPostTickInterval(ref severityAdjustment,delta);ticks+=delta;while(ticks>=Props.interval){ticks-=Props.interval;Pulse();}}
  public int Pulse(){
   var p=Pawn;if(p==null || p.Dead)return 0;int n=0;float amount=parent.Severity;
   if(Props.healFactor>0f && !Props.healAllies)ClassMelee.Heal(p,amount*Props.healFactor);
   if(!p.Spawned || Props.radius<=0f)return 0;
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,Props.radius).ToList()){
    if(Props.damageEnemies && t!=p && t.HostileTo(p)){t.TakeDamage(new DamageInfo(PaladinCore.Radiant,amount*(PaladinCore.HolyVulnerable(t)?2f:1f),.3f,-1f,p));n++;}
    else if(Props.healAllies && !t.HostileTo(p)){ClassMelee.Heal(t,amount*Props.healFactor);if(Props.allyHediff!=null)Items.Give(t,Props.allyHediff,Props.interval*5);}
   }
   return n;
  }
  public override void CompExposeData(){base.CompExposeData();Scribe_Values.Look(ref ticks,"lpPulseTicks");}
 }

 // ==================== Harmony ====================
 // 近战命中后：至圣斩、神圣武器追加神圣伤害；额外攻击的追击。快速返回：不是物理职业者。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class ClassMeleeHit {
  public static int Hits;
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){
   Hits++;
   var caster=__instance.CasterPawn;if(caster==null || !Martial.Is(caster))return;
   var t=target.Thing as Pawn;if(t==null || t.Dead)return;
   PaladinCore.OnMeleeHit(caster,t);
   if(!Martial.inFollowUp && !t.Dead)Martial.TryFollowUp(__instance,caster,t);
  }
 }
 // 受伤前：守护姿态分担（前缀）；誓仇、武器专精 / 死神之眼远程、拨挡箭矢、斗志（后缀）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class ClassDamageMods {
  public const float GuardShare=.3f,GuardRadius=3f;
  static readonly DefRef<HediffDef> Stance=new DefRef<HediffDef>("LP_GuardStance"),Enmity=new DefRef<HediffDef>("LP_Enmity");
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(Martial.Cache.Count==0 || __instance.Dead || dinfo.Def==null || dinfo.Amount<=0f || !dinfo.Def.ExternalViolenceFor(__instance) || __instance.Faction==null || !__instance.Spawned)return;
   var stance=Stance.Def;if(stance==null || __instance.health.hediffSet.HasHediff(stance))return;
   var guardian=Guardian(__instance);if(guardian==null)return;
   float moved=dinfo.Amount*GuardShare;
   dinfo.SetAmount(dinfo.Amount-moved);
   guardian.TakeDamage(new DamageInfo(dinfo.Def,moved,dinfo.ArmorPenetrationInt,-1f,dinfo.Instigator,null,dinfo.Weapon));
  }
  public static Pawn Guardian(Pawn victim){
   var stance=Stance.Def;if(stance==null || victim?.Map==null)return null;
   var all=victim.Map.mapPawns.AllPawnsSpawned;
   for(int i=0;i<all.Count;i++){var g=all[i];if(!g.Dead && g.Position.InHorDistOf(victim.Position,GuardRadius) && g!=victim && !g.Downed && g.Faction==victim.Faction && g.health.hediffSet.HasHediff(stance))return g;}
   return null;
  }
  static void Postfix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(absorbed || __instance.Dead || dinfo.Def==null || dinfo.Amount<=0f)return;
   var a=dinfo.Instigator as Pawn;
   if(a!=null && Martial.Is(a))PaladinCore.ApplyEnmity(__instance,ref dinfo);
   if(a!=null && Martial.ShotFlags.Count>0){byte b=Martial.Flags(a);if((b & (Martial.FlagMastery|Martial.FlagDeadEye))!=0 && Martial.Ranged(dinfo))dinfo.SetAmount(dinfo.Amount*Martial.RangedFactor(a,b));}
   if(Martial.Cache.Count>0 && MonkCore.Deflect(__instance,ref dinfo))absorbed=true;
   if(!absorbed && dinfo.Amount>0f && a!=null && Martial.Is(a))Martial.OnHit(__instance,a);
  }
 }
 // 死亡后：誓仇得报、斗志、战意再临 / 死神之眼重置冷却。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class ClassKillHooks {
  static void Postfix(Pawn __instance,DamageInfo? dinfo){
   if(!__instance.Dead || Martial.Cache.Count==0)return;
   PaladinCore.OnKilled(__instance);
   var killer=dinfo?.Instigator as Pawn;
   if(killer==null || killer==__instance || !Martial.Is(killer))return;
   Martial.OnKill(__instance,killer);
   if(dinfo.Value.Def==null)return;
   bool ranged=dinfo.Value.Def.isRanged || Martial.Ranged(dinfo.Value);
   if(!ranged && Items.Has(killer,"LP_SurgeOfBattle") || ranged && (Martial.Flags(killer) & Martial.FlagDeadEye)!=0)Martial.ResetFighter(killer);
  }
 }
 // 勇气灵光、圣光化身：不会恐慌逃跑。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class ClassCourage {
  static bool Prefix(Pawn ___pawn,MentalStateDef stateDef,ref bool __result){
   if(!PaladinCore.NoPanic(___pawn,stateDef) && !(stateDef!=null && Items.Has(___pawn,"LP_LegSanctified")))return true; // 1.74.0 神圣化身：不会精神崩溃
   __result=false;return false;
  }
 }
 // 不屈意志 / 战意再临 / 圣光化身 / 金刚之躯：免疫击晕。
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.StunFor))]
 public static class ClassStunImmunity {
  static readonly AccessTools.FieldRef<StunHandler,Thing> Parent=AccessTools.FieldRefAccess<StunHandler,Thing>("parent");
  public static bool Immune(Pawn p)=>p!=null && (Items.Has(p,"LP_Indomitable") || Items.Has(p,"LP_SurgeOfBattle") || Items.Has(p,"LP_HolyAvatar") || Items.Has(p,"LP_DiamondSoul") || Items.Has(p,"LP_LegFormlessBuff")); // 1.74.0 无相之身
  static bool Prefix(StunHandler __instance)=>!Immune(Parent(__instance) as Pawn);
 }
 [HarmonyPatch(typeof(Projectile),nameof(Projectile.DamageAmount),MethodType.Getter)]
 public static class ClassShotDamage {
  static void Postfix(Projectile __instance,ref int __result){if(Martial.Shots.Count==0 || !Martial.Shots.TryGetValue(__instance,out var m))return;__result=Mathf.Max(1,Mathf.RoundToInt(__result*m.damage));}
 }
 [HarmonyPatch(typeof(Projectile),nameof(Projectile.ArmorPenetration),MethodType.Getter)]
 public static class ClassShotPenetration {
  static void Postfix(Projectile __instance,ref float __result){if(Martial.Shots.Count==0 || !Martial.Shots.TryGetValue(__instance,out var m))return;__result+=m.ap;}
 }
 // 子弹（Bullet）先调 base.Impact 再结算伤害，所以子弹在 Bullet.Impact 之后移除，其他投射物在 Projectile.Impact 之后。
 [HarmonyPatch(typeof(Bullet),"Impact")]
 public static class ClassShotBulletImpact {
  static void Postfix(Bullet __instance,Thing hitThing){if(Martial.Shots.Count>0)Martial.Impacted(__instance,hitThing);}
 }
 [HarmonyPatch(typeof(Projectile),"Impact")]
 public static class ClassShotImpact {
  static void Postfix(Projectile __instance,Thing hitThing){if(Martial.Shots.Count>0 && !(__instance is Bullet))Martial.Impacted(__instance,hitThing);}
 }
 // 连射：连发武器每轮多 1 发（burstShotsLeft 在 WarmupComplete 里才设）。
 [HarmonyPatch(typeof(Verb),"WarmupComplete")]
 public static class ClassRapidFireBurst {
  static readonly AccessTools.FieldRef<Verb,int> ShotsLeft=AccessTools.FieldRefAccess<Verb,int>("burstShotsLeft");
  static void Postfix(Verb __instance){
   if(Martial.ShotFlags.Count==0 || !(__instance is Verb_LaunchProjectile) || __instance.state!=VerbState.Bursting)return;
   var p=__instance.CasterPawn;if(p==null || (Martial.Flags(p) & Martial.FlagRapid)==0 || __instance.BurstShotCount<=1)return;
   ShotsLeft(__instance)++;
  }
 }
 // 按钮：选择职业（有职业点的殖民者）；精准射击的模式（征召、学会精准射击）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class ClassGizmos {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   foreach(var g in __result)yield return g;
   var p=__instance;if(p.Faction!=Faction.OfPlayer || !Adventurer.Enabled)yield break;
   var h=Adventurer.Get(p);if(h==null)yield break;
   int pts=ClassSystem.Points(h);int lp=ClassLegendary.Points(h);
   if(lp>0 && !p.Dead)yield return new Command_Action{
    defaultLabel="传奇职业（"+lp+"）",
    defaultDesc="还有 "+lp+" 点传奇职业点：打开职业窗口学一个传奇职业能力（那个职业 9 级，同职业第二个 17 级）。",
    icon=ContentFinder<Texture2D>.Get("LP/Classes/Legendary/Unyielding",false)??BaseContent.BadTex,
    action=()=>Find.WindowStack.Add(new Dialog_ChooseClass(p))
   };
   if(pts>0 && !p.Dead)yield return new Command_Action{
    defaultLabel="选择职业（"+pts+"）",
    defaultDesc="还有 "+pts+" 点职业点：打开职业窗口，给战士、武僧或圣武士加 1 级。也可以在“冒险者”分页的“职业”页里加点。",
    icon=ClassSystem.Fighter?.Background??BaseContent.BadTex,
    action=()=>Find.WindowStack.Add(new Dialog_ChooseClass(p))
   };
   if(!Martial.Is(p) || !p.Drafted || !ClassSystem.Knows(p,"LP_PrecisionShot"))yield break;
   var st=Martial.State;if(st==null)yield break;var mode=st.Mode(p);
   yield return new Command_Action{
    defaultLabel="精准射击："+PrecisionShot.ModeLabel(mode),
    defaultDesc="切换精准射击的模式：\n要害：伤害 ×(1.5 + 0.5 × 射术强度)，穿甲 +30%。\n打腿：伤害 ×0.7，目标移动 ×0.5 持续 10 秒。\n打手：伤害 ×0.5，有机会打落武器：成功率 = 50% + 10% ×（自己的敏捷调整值 + 熟练加值 − 目标的力量调整值 − 熟练加值）+ 20% ×（射术强度 − 1），限 15%～90%，体型 2 以上减半。",
    icon=ContentFinder<Texture2D>.Get("LP/Classes/Fighter/PrecisionShot",false)??BaseContent.BadTex,
    action=()=>st.SetMode(p,(PrecisionMode)(((int)st.Mode(p)+1)%3))
   };
  }
 }
}
