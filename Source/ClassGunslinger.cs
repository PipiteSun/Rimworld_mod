using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 2026-11-19 枪手（Peter，设计文档 https://claude.ai/artifact/16RyAa8tNCuDLgMGBoJbAF；照 Pathfinder 1 版枪手的胆识与绝技、PF2 的枪道、5e 默瑟枪手的技巧射击）：
 // 物理职业（额外攻击、战斗风格，算进物理职业等级合计），资源「胆识」（上限 2 + 等级/2，睡觉回）；用火器击杀 +1、打倒 +1，3 格内有敌人时开枪命中每 30 秒 +1。
 // 火器 = 科技等级 ≥ 工业的远程武器（另加本模组的魔法枪、矮人枪械），手雷、弓、弩、法杖不算；按射程 / 重量分短枪、长枪、重火器（子职业看这个）。
 // 卡壳只在暴力射击上：每多花 1 点胆识 +8%，卡壳 = 3 秒瞄准极慢（快速排障立刻解除）。相依之枪存在子职业数据的 ticks（gsBond = 那把枪的 thingIDNumber）。
 public static class Gunslinger {
  static readonly DefRef<LP_ClassDef> cls=new DefRef<LP_ClassDef>("LP_Class_Gunslinger");
  public static LP_ClassDef Class=>cls.Def;
  public const string Drifter="LP_Sub_Drifter",Pistolero="LP_Sub_Pistolero",Sniper="LP_Sub_Sniper",Vanguard="LP_Sub_Vanguard";
  public const string BondKey="gsBond",DodgeKey="gsDodge",DaringKey="gsDaring",LuckKey="gsLuck",CheatKey="gsCheat",StillX="gsSX",StillZ="gsSZ",AimKey="gsAim";
  public const float JamPer=.08f,DodgeChance=.3f,DaringRange=3f;
  public static readonly Color Gun=new Color(.85f,.7f,.45f);
  public enum Kind{None,Short,Long,Heavy}
  public static int Level(Pawn p)=>ClassSystem.Level(p,Class);
  public static int Level(Hediff_Adventurer h)=>ClassSystem.Level(h,Class);
  public static string ResourceTip=>"胆识：用火器击杀敌人 +1、打倒敌人 +1，3 格内有敌人时开枪命中每 30 秒 +1；睡觉时每 250 刻 +".Loc()+ClassSystem.SleepRegen+"。召唤物、友军、囚犯、已经倒地的目标不算。很多被动只要有 1 点胆识就生效，不扣点。".Loc();

  // ---------- 火器 ----------
  static readonly Dictionary<ThingDef,Kind> kinds=new Dictionary<ThingDef,Kind>();
  public static Kind KindOf(ThingDef d){
   if(d==null)return Kind.None;if(kinds.TryGetValue(d,out var k))return k;
   k=Kind.None;
   if(d.IsRangedWeapon && d.Verbs!=null && d.Verbs.Count>0){
    var v=d.Verbs[0];bool tagged=d.weaponTags!=null && d.weaponTags.Contains("LP_ArcaneGun") && (d.tradeTags==null || !d.tradeTags.Contains("LP_Staff"));
    bool grenade=d.defName.Contains("Grenade") || d.defName.Contains("Molotov") || d.weaponTags!=null && d.weaponTags.Any(t=>t.Contains("Grenade"));
    bool bow=d.defName.Contains("Bow") || d.defName.Contains("Pila") || d.defName.Contains("Javelin");
    if(!grenade && !bow && v.defaultProjectile!=null && (d.techLevel>=TechLevel.Industrial || tagged)){
     float range=v.range,mass=d.GetStatValueAbstract(StatDefOf.Mass);
     k=range<26f && mass<=3f?Kind.Short:range>=30f && v.burstShotCount<=3?Kind.Long:Kind.Heavy;
    }
   }
   kinds[d]=k;return k;
  }
  public static bool Firearm(ThingDef d)=>KindOf(d)!=Kind.None;
  public static Thing GunOf(Pawn p){var g=p?.equipment?.Primary;return g!=null && Firearm(g.def)?g:null;}
  public static Kind Holding(Pawn p)=>KindOf(GunOf(p)?.def);
  public static string KindLabel(Kind k)=>k==Kind.Short?"短枪".Loc():k==Kind.Long?"长枪".Loc():k==Kind.Heavy?"重火器".Loc():"非火器".Loc();
  public static Kind SubKind(Pawn p)=>ClassSubclass.Has(p,Drifter) || ClassSubclass.Has(p,Pistolero)?Kind.Short:ClassSubclass.Has(p,Sniper)?Kind.Long:ClassSubclass.Has(p,Vanguard)?Kind.Heavy:Kind.None;
  public static bool SubGun(Pawn p)=>SubKind(p)!=Kind.None && Holding(p)==SubKind(p);

  // ---------- 胆识 ----------
  public static float Grit(Pawn p)=>ClassSystem.Resource(p,Class);
  public static bool HasGrit(Pawn p,float n=1f)=>Class!=null && Grit(p)+.001f>=n;
  public static void Gain(Pawn p,float n,string why){var h=Adventurer.Get(p);if(h==null || Level(h)<=0)return;if(ClassSystem.AddResource(h,Class,n)>0f && p.Faction==Faction.OfPlayer)Items.Mote(p,"胆识 +".Loc()+n.ToString("0")+"（".Loc()+why+"）".Loc(),Gun);}
  public static bool Pay(Pawn p,float n){var h=Adventurer.Get(p);if(h==null || Grit(p)+.001f<n)return false;ClassSystem.AddResource(h,Class,-n);return true;}
  static bool Countable(Pawn killer,Pawn v)=>killer!=null && v!=null && v!=killer && v.HostileTo(killer) && !v.IsPrisoner && !Martial.Conjured(v);
  public static SubclassData Data(Pawn p)=>ClassSubclass.Data(p);
  static int Now=>Find.TickManager.TicksGame;
  static bool Ready(SubclassData d,string key,int ticks){if(d==null)return false;d.ticks.TryGetValue(key,out int t);int now=Now;return t<=0 || now-t>=ticks || now<t;}
  static void Use(SubclassData d,string key){if(d!=null)d.ticks[key]=Math.Max(1,Now);}

  // ---------- 被动 ----------
  public static readonly HashSet<int> Users=new HashSet<int>();
  public static bool Is(Pawn p)=>p!=null && Users.Contains(p.thingIDNumber);
  static void Passive(Pawn p,string hediff,bool on,float severity=1f){
   if(on){var x=Items.Has(p,hediff)?p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff(hediff)):Items.Give(p,hediff);if(x!=null && Mathf.Abs(x.Severity-severity)>.001f)x.Severity=severity;}
   else if(Items.Has(p,hediff))Items.Remove(p,hediff);
  }
  static readonly string[] PassiveHediffs={"LP_GS_Bonded","LP_GS_GritEdge","LP_GS_Gunsmith","LP_GS_CloseQuarters","LP_GS_HeavyStance","LP_GS_Concealed"};
  public static bool Knows(Pawn p,string a)=>ClassSystem.Knows(p,a);
  public static int BondTier(int lv)=>lv>=15?4:lv>=10?3:lv>=5?2:1;
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;if(p==null)return;int lv=Level(h);
   if(lv<=0){if(Users.Remove(p.thingIDNumber) && !p.Dead)foreach(var n in PassiveHediffs)Items.Remove(p,n);return;}
   if(p.Dead)return;Users.Add(p.thingIDNumber);var d=ClassSubclass.Data(h);var gun=GunOf(p);bool grit=HasGrit(p);
   d.ticks.TryGetValue(BondKey,out int bond);
   Passive(p,"LP_GS_Bonded",gun!=null && bond!=0 && gun.thingIDNumber==bond,BondTier(lv));
   Passive(p,"LP_GS_GritEdge",gun!=null && grit && (Knows(p,"LP_GS_Deadeye") || Knows(p,"LP_GS_QuickDraw")),Knows(p,"LP_GS_QuickDraw")?2f:1f);
   Passive(p,"LP_GS_Gunsmith",Knows(p,"LP_GS_Gunsmith"));
   Passive(p,"LP_GS_CloseQuarters",Knows(p,"LP_GS_CloseQuarters") && gun!=null);
   Passive(p,"LP_GS_HeavyStance",Knows(p,"LP_GS_HeavyStance") && Holding(p)==Kind.Heavy);
   // 隐蔽位置（狙击手 3 级）：拿长枪原地不动 250 刻以上就进入隐蔽，第一发伤害 ×2；挪动就没了。
   if(Knows(p,"LP_GS_Hidden") && p.Spawned){
    bool still=d.ticks.TryGetValue(StillX,out int x) && d.ticks.TryGetValue(StillZ,out int z) && x==p.Position.x && z==p.Position.z && !p.pather.Moving;
    d.ticks[StillX]=p.Position.x;d.ticks[StillZ]=p.Position.z;
    if(still && Holding(p)==Kind.Long){if(!Items.Has(p,"LP_GS_Concealed"))Items.Give(p,"LP_GS_Concealed");}
    else Items.Remove(p,"LP_GS_Concealed");
   }
  }

  // ---------- 施放 ----------
  static readonly HashSet<string> Known=new HashSet<string>{"GSBond","GSViolent","GSClear","GSWhip","GSUtility","GSDeadShot","GSTargeting","GSAimMode","GSStartle","GSBleed","GSReload","GSMenace","GSStun","GSDeath",
   "GSWake","GSFan","GSNoon","GSPierce","GSSpot","GSYard","GSBlast","GSCover","GSStorm","GSBulletStorm","GSArcRunes","GSArcRain","GSArcStop","GSArcFate"};
  static readonly HashSet<string> NeedGun=new HashSet<string>{"GSViolent","GSUtility","GSDeadShot","GSTargeting","GSStartle","GSBleed","GSReload","GSMenace","GSStun","GSDeath","GSWake","GSFan","GSNoon","GSPierce","GSYard","GSBlast","GSStorm","GSBulletStorm","GSArcRain","GSArcStop","GSArcFate"};
  // 「只要有 1 点胆识」的绝技：不扣点，但胆识是 0 时用不了。
  static readonly HashSet<string> NeedOne=new HashSet<string>{"GSClear","GSUtility","GSStartle","GSReload","GSSpot"};
  static readonly Dictionary<string,Kind> SubNeeds=new Dictionary<string,Kind>{{"GSWake",Kind.Short},{"GSFan",Kind.Short},{"GSPierce",Kind.Long},{"GSYard",Kind.Long},{"GSBlast",Kind.Heavy},{"GSStorm",Kind.Heavy}};
  public static string CannotCast(Pawn p,AbilityDef def,CompProperties_ClassEffect e){
   if(e?.effect==null || e.passive || !Known.Contains(e.effect))return null;
   if(NeedGun.Contains(e.effect) && GunOf(p)==null)return "需要火器（现在拿的不是火器）".Loc();
   if(SubNeeds.TryGetValue(e.effect,out var k) && Holding(p)!=k)return "需要".Loc()+KindLabel(k);
   if(NeedOne.Contains(e.effect) && !HasGrit(p))return "需要至少 1 点胆识".Loc();
   if(e.effect=="GSViolent" && !HasGrit(p))return "胆识不足（至少 1 点）".Loc();
   if(e.effect=="GSClear" && !Items.Has(p,"LP_GS_Jammed"))return "枪没有卡壳".Loc();
   if(e.effect=="GSBond" && GunOf(p)==null)return "先拿起一把火器".Loc();
   if(e.effect=="GSBond"){var d=Data(p);if(d!=null && d.ticks.TryGetValue(BondKey,out int b) && b!=0 && !Ready(d,"gsBondT",3*GenDate.TicksPerDay) && GunOf(p).thingIDNumber!=b)return "换绑要等 3 天".Loc();}
   return null;
  }
  static bool Hostile(Pawn a,Pawn t)=>AnyTarget.Hostile(a,t);
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e?.effect==null || !Known.Contains(e.effect))return null;var t=target.Thing as Pawn;
   switch(e.effect){
    case "GSViolent":case "GSDeadShot":case "GSTargeting":case "GSStartle":case "GSStun":case "GSDeath":case "GSNoon":case "GSPierce":case "GSSpot":case "GSWhip":case "GSWake":case "GSBlast":case "GSArcFate":
     if(t==null || t.Dead)return "需要选择一个活着的目标。".Loc();if(!t.HostileTo(caster))return "只能对敌人使用。".Loc();
     if(NeedGun.Contains(e.effect) && e.effect!="GSWake" && e.effect!="GSBlast"){var v=Martial.RangedVerb(caster);if(v==null)return "需要火器。".Loc();if(CastApproach.TooFar(caster.Position.DistanceTo(t.Position),v.EffectiveRange))return "超出当前武器的射程（".Loc()+v.EffectiveRange.ToString("0")+" 格）。".Loc();}
     return null;
    case "GSUtility":if(target.Thing==null)return "需要选择一扇门、一面墙、一件东西，或者一名流血的友军。".Loc();
     if(t!=null && t.HostileTo(caster))return "实用射击不打人：选门、墙、地上的东西或流血的友军。".Loc();return null;
    case "GSFan":case "GSArcRain":{var map=caster.Map;if(map==null || !target.Cell.IsValid || !target.Cell.InBounds(map))return "需要选择一个位置。".Loc();return null;}
   }
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e?.effect==null || !Known.Contains(e.effect))return false;
   var t=target.Thing as Pawn;float s=ClassEffects.Strength(p,e);int dur=ClassEffects.Duration(p,e);var d=Data(p);
   switch(e.effect){
    case "GSBond":{var g=GunOf(p);if(g!=null && d!=null){d.ticks[BondKey]=g.thingIDNumber;Use(d,"gsBondT");Items.Mote(p,"相依之枪：".Loc()+g.LabelShort,Gun);}break;}
    case "GSViolent":if(Hostile(p,t)){int n=Mathf.Clamp(Mathf.FloorToInt(Grit(p)+.001f),1,3);Pay(p,n);Shot(p,t,1f+.5f*n*s,.1f*n,true);if(Rand.Chance(JamPer*n*(Knows(p,"LP_GS_GunTraining")?.5f:1f)) && !Feats.Has(p,DefDatabase<LP_FeatDef>.GetNamedSilentFail("LP_Feat_Class_GunExpert"))){Items.Give(p,"LP_GS_Jammed",180,1f);Items.Mote(p,"卡壳！".Loc(),Color.red);}Items.Mote(p,"暴力射击 ×".Loc()+n,Gun);}break;
    case "GSClear":Items.Remove(p,"LP_GS_Jammed");Items.Mote(p,"快速排障".Loc(),Gun);break;
    case "GSWhip":if(Hostile(p,t)){float a=(Holding(p)==Kind.Short?10f:14f)*s;t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,a,.15f,-1f,p));if(!t.Dead && Rand.Chance(.4f))t.stances?.stunner?.StunFor(60,p);Items.Mote(t,"枪托猛击".Loc(),Gun);}break;
    case "GSUtility":Utility(p,target.Thing,s);break;
    case "GSDeadShot":if(Hostile(p,t)){int burst=Martial.RangedVerb(p)?.verbProps?.burstShotCount??1;Shot(p,t,Mathf.Max(2.5f,1f+.5f*burst)*s,.2f,true);Items.Mote(p,"致命一击".Loc(),Gun);}break;
    case "GSAimMode":{int m=(AimMode(p)+1)%5;if(d!=null)d.ticks[AimKey]=m;Items.Mote(p,"瞄准：".Loc()+AimLabel(m),Gun);break;}
    case "GSTargeting":if(Hostile(p,t))Targeting(p,t,s);break;
    case "GSStartle":if(Hostile(p,t)){Items.Give(t,"LP_GS_Startled",240,1f);Items.Mote(t,"惊骇".Loc(),Gun);}break;
    case "GSBleed":Items.Give(p,"LP_GS_BleedReady",300,1f);Items.Mote(p,"放血伤".Loc(),Gun);break;
    case "GSReload":case "GSCover":case "GSStorm":case "GSYard":case "GSBulletStorm":
     if(e.effect=="GSCover"){foreach(var o in Races124.PawnsNear(p.Map,p.Position,8f).ToList())if(o.Faction==p.Faction && !o.Dead)Items.Give(o,"LP_GS_Covered",dur,1f);}
     else Items.Give(p,e.hediff,dur,1f);
     Items.Mote(p,def.LabelCap,Gun);break;
    case "GSMenace":foreach(var o in Races124.PawnsNear(p.Map,p.Position,ClassEffects.Radius(p,e)).Where(x=>Hostile(p,x) && !x.Downed).ToList()){if(DNDMind.TryResist(o)){Items.Mote(o,"抵抗！".Loc(),Gun);continue;}if(ClassSubclass.Fear(o,dur,"威慑射击".Loc()) && o.RaceProps.Humanlike && o.carryTracker?.CarriedThing!=null)o.carryTracker.TryDropCarriedThing(o.Position,ThingPlaceMode.Near,out _);}Items.Mote(p,"威慑射击".Loc(),Gun);break;
    case "GSStun":if(Hostile(p,t)){var vt=t;Shot(p,t,1f,0f,true,(hit,c)=>{if(hit is Pawn x && !x.Dead && !Rand.Chance(ClassSubclass.ResistChance(x)))x.stances?.stunner?.StunFor(ClassSubclass.Bossy(x)?90:180,p);});Items.Mote(p,"击晕射击".Loc(),Gun);}break;
    case "GSDeath":if(Hostile(p,t))DeathShot(p,t,s);break;
    case "GSWake":if(Hostile(p,t))Wake(p,t,s);break;
    case "GSFan":Fan(p,target.Cell,s);break;
    case "GSNoon":if(Hostile(p,t)){ClassMartial2.MarkTarget(p,t,"LP_GS_Dueled",dur,s,Gun,"正午决斗".Loc());}break;
    case "GSPierce":if(Hostile(p,t))Pierce(p,t,s);break;
    case "GSSpot":if(Hostile(p,t)){var inv=DefCache<HediffDef>.Get("PsychicInvisibility");if(inv!=null){var x=t.health.hediffSet.GetFirstHediffOfDef(inv);if(x!=null)t.health.RemoveHediff(x);}Items.Give(t,"LP_GS_Spotted",dur,1f);Items.Mote(t,"观测".Loc(),Gun);}break;
    case "GSArcRunes":Items.Give(p,"LP_GS_RunesBuff",dur,1f);Items.Mote(p,def.LabelCap,Gun);break;
    case "GSArcRain":{var map=p.Map;var foes=Races124.PawnsNear(map,target.Cell,5f).Where(x=>Hostile(p,x) && !x.Downed).ToList();for(int i=0;i<20;i++){LocalTargetInfo tg=foes.Count>0?(LocalTargetInfo)foes.RandomElement():(LocalTargetInfo)target.Cell;Shot(p,tg,.6f*s,0f,false);}Items.Mote(p,"弹雨".Loc(),Gun);break;}
    case "GSArcStop":{int n=0;foreach(var o in Races124.PawnsNear(p.Map,p.Position,ClassEffects.Radius(p,e)).Where(x=>Hostile(p,x) && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).ToList()){o.stances?.stunner?.StunFor(180,p,false);if(n<8 && GenSight.LineOfSight(p.Position,o.Position,p.Map)){Shot(p,o,1f*s,.2f,true);n++;}}Items.Mote(p,"时停一枪 ×".Loc()+n,Gun);break;}
    case "GSArcFate":if(Hostile(p,t)){Shot(p,t,5f*s,1f,true);Items.Mote(p,"命运之弹".Loc(),Gun);}break;
    case "GSBlast":if(Hostile(p,t)){Shot(p,t,1f,0f,true);if(t.Spawned && p.Spawned){var dir=(t.Position-p.Position).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(1f,0f,0f);DNDPush.Move(t,DNDPush.Destination(t.Map,t.Position,dir,2));}Items.Mote(t,"顿射".Loc(),Gun);}break;
   }
   return true;
  }
  public static Projectile Shot(Pawn p,LocalTargetInfo t,float damage,float ap,bool mustHit,Action<Thing,IntVec3> onHit=null)=>Martial.Fire(p,t,new ShotMod{damage=damage,ap=ap,caster=p,onHit=onHit},mustHit);
  // 部位瞄准：手（缴械）、头（晕 2 秒）、眼（伤害 ×0.6，伤到眼睛）、腿（打倒、跛行）、躯干（穿甲 +30%）。
  public static int AimMode(Pawn p){var d=Data(p);if(d==null)return 0;d.ticks.TryGetValue(AimKey,out int m);return Mathf.Clamp(m,0,4);}
  public static string AimLabel(int m)=>m==1?"头".Loc():m==2?"眼".Loc():m==3?"腿".Loc():m==4?"躯干".Loc():"手".Loc();
  public static void Targeting(Pawn p,Pawn t,float s){
   int m=AimMode(p);float dmg=m==2?.6f:1f,ap=m==4?.3f:0f;
   Shot(p,t,dmg*s,ap,true,(hit,c)=>{
    if(!(hit is Pawn x) || x.Dead)return;
    switch(m){
     case 0:if(Martial.CanDisarm(x))Martial.Disarm(p,x,Martial.DisarmChance(p,x,s,Adventurer.Dex));break;
     case 1:x.stances?.stunner?.StunFor(ClassSubclass.Bossy(x)?60:120,p);break;
     case 2:{var eye=x.health.hediffSet.GetNotMissingParts().FirstOrDefault(bp=>bp.def.defName.Contains("Eye"));if(eye!=null)x.TakeDamage(new DamageInfo(DamageDefOf.Cut,8f*s,1f,-1f,p,eye));break;}
     case 3:Items.Give(x,"LP_Hobbled",600);if(x.BodySize<=1.5f)x.stances?.stunner?.StunFor(60,p);break;
    }
   });
   Items.Mote(p,"部位瞄准·".Loc()+AimLabel(m),Gun);
  }
  public static void Utility(Pawn p,Thing thing,float s){
   if(thing is Pawn ally){
    var wound=ally.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>i.Bleeding).OrderByDescending(i=>i.BleedRate).FirstOrDefault();
    if(wound!=null){wound.Tended(1f,1f);ally.TakeDamage(new DamageInfo(DamageDefOf.Burn,2f,0f,-1f,p,wound.Part));Items.Mote(ally,"烙伤止血".Loc(),Gun);}
    else Items.Mote(ally,"没有流血的伤口".Loc(),Gun);
    return;
   }
   if(thing is Building b && b.def.useHitPoints){Shot(p,b,5f,0f,true);Items.Mote(b,"破锁".Loc(),Gun);return;}
   if(thing.def.category==ThingCategory.Item && thing.Spawned){var map=thing.Map;var dir=(thing.Position-p.Position).ToVector3();var dest=DNDPush.Destination(map,thing.Position,dir.sqrMagnitude<.01f?new Vector3(1f,0f,0f):dir,3);if(dest!=thing.Position){thing.DeSpawn();GenPlace.TryPlaceThing(thing,dest,map,ThingPlaceMode.Near);}Items.Mote(thing,"挤移".Loc(),Gun);}
  }
  public static void DeathShot(Pawn p,Pawn t,float s){
   Shot(p,t,3f*s,.3f,true,(hit,c)=>{
    if(!(hit is Pawn x) || x.Dead)return;
    if(ClassSubclass.Bossy(x) || ClassMartial2.Boss(x) || x.BodySize>=3f){Items.Mote(x,"死亡射击".Loc(),Gun);return;}
    if(Rand.Chance(ClassSubclass.ResistChance(x))){Items.Mote(x,"挺住了！".Loc(),Gun);return;}
    DeathShots.Add(x.thingIDNumber);try{x.Kill(new DamageInfo(DamageDefOf.Bullet,99f,1f,-1f,p));}finally{DeathShots.Remove(x.thingIDNumber);}
    Items.Mote(p,"死亡射击：一枪毙命".Loc(),Gun);
   });
  }
  public static readonly HashSet<int> DeathShots=new HashSet<int>();
  public static void Wake(Pawn p,Pawn t,float s){
   if(t.Position.AdjacentTo8WayOrInside(p.Position))ClassMelee.Strike(p,t);
   for(int i=0;i<2;i++){if(t==null || t.Dead || t.Downed){t=Races124.PawnsNear(p.Map,p.Position,8f).Where(x=>Hostile(p,x) && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault();if(t==null)break;}Shot(p,t,1f*s,0f,true);}
   Items.Mote(p,"流浪者余波".Loc(),Gun);
  }
  public static int Fan(Pawn p,IntVec3 c,float s){
   var map=p.Map;if(map==null)return 0;var foes=Races124.PawnsNear(map,c,4f).Where(x=>Hostile(p,x) && !x.Downed).ToList();int n=0;
   for(int i=0;i<6;i++){LocalTargetInfo tg=foes.Count>0?(LocalTargetInfo)foes.RandomElement():(LocalTargetInfo)c;if(Shot(p,tg,.8f*s,0f,false)!=null)n++;}
   Items.Mote(p,"扇击 ×".Loc()+n,Gun);return n;
  }
  public static int Pierce(Pawn p,Pawn t,float s){
   var map=p.Map;var v=Martial.RangedVerb(p);if(map==null || v==null)return 0;float range=v.EffectiveRange;var from=p.Position.ToVector3Shifted();var dir=(t.Position.ToVector3Shifted()-from).normalized;
   var line=Races124.PawnsNear(map,p.Position,range).Where(x=>Hostile(p,x) && !x.Downed).Where(x=>{var o=x.Position.ToVector3Shifted()-from;float along=Vector3.Dot(o,dir);return along>0f && (o-dir*along).magnitude<=1.2f;}).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).ToList();
   if(!line.Contains(t))line.Insert(0,t);float f=1f;int n=0;
   foreach(var x in line){Shot(p,x,f*s,.2f,true);f*=.8f;n++;}
   Items.Mote(p,"穿透射击 ×".Loc()+n,Gun);return n;
  }
  public static void AfterCast(Ability_Class a,LocalTargetInfo target){}

  // ---------- 挂钩 ----------
  [ThreadStatic] public static bool inExtra;
  // 双持短枪（35%）、子弹风暴（每次）：多开一枪。
  public static void AfterShot(Verb_LaunchProjectile v){
   if(inExtra || Users.Count==0)return;var p=v?.CasterPawn;if(!Is(p) || v.EquipmentSource==null || !Firearm(v.EquipmentSource.def))return;
   var tg=v.CurrentTarget;if(!tg.IsValid)return;
   bool storm=Items.Has(p,"LP_LegBulletStormBuff");
   if(!storm && !(Knows(p,"LP_GS_TwinPistols") && Holding(p)==Kind.Short && Rand.Chance(.35f)))return;
   inExtra=true;try{Shot(p,tg,storm?1f:.7f,0f,false);}finally{inExtra=false;}
  }
  public static void Damage(Pawn v,ref DamageInfo d){
   var a=d.Instigator as Pawn;
   if(a!=null && a!=v && Is(a) && v.HostileTo(a)){
    bool fire=d.Weapon!=null && Firearm(d.Weapon);
    if(fire){
     var ah=Adventurer.Get(a);
     if(Knows(a,"LP_GS_GunTraining"))d.SetAmount(d.Amount+1.5f*Mathf.Max(0,Adventurer.Mod(Adventurer.Score(a,Adventurer.Dex))));
     if(Items.Has(a,"LP_GS_Bonded") && Level(ah)>=15)d.SetAmount(d.Amount*1.1f);
     if(Items.Has(a,"LP_GS_Concealed")){d.SetAmount(d.Amount*2f);Items.Remove(a,"LP_GS_Concealed");Items.Mote(a,"隐蔽射击".Loc(),Gun);}
     var duel=ClassMartial2.Mark(v,"LP_GS_Dueled");if(duel!=null && duel.caster==a)d.SetAmount(d.Amount*1.5f);
     if(Items.Has(a,"LP_GS_YardBuff") && a.Position.DistanceTo(v.Position)>=30f)d.SetAmount(d.Amount*2f);
     if(Knows(a,"LP_GS_Breach") && Holding(a)==Kind.Heavy){if(v.RaceProps.IsMechanoid)d.SetAmount(d.Amount*1.4f);else if(v.RaceProps.Humanlike && v.Spawned && Rand.Chance(.15f)){var dir=(v.Position-a.Position).ToVector3();if(dir.sqrMagnitude>.01f)DNDPush.Move(v,DNDPush.Destination(v.Map,v.Position,dir,1));}}
     if(Items.Has(a,"LP_GS_BleedReady") && v.RaceProps.IsFlesh)Bleed.Add(v.thingIDNumber);
     if(Items.Has(a,"LP_GS_RunesBuff"))Runes[v.thingIDNumber]=d.Amount*.5f;
     if(Knows(a,"LP_GS_BladeAndGun"))Items.Give(a,"LP_GS_DrifterBlade",300,1f);
     var ad=Data(a);if(ad!=null && Ready(ad,DaringKey,1800) && a.Position.DistanceTo(v.Position)<=DaringRange && !v.Downed){Use(ad,DaringKey);Gain(a,1f,"胆大包天".Loc());}
    }
    else if(ClassMartial2.Melee(d) && Knows(a,"LP_GS_BladeAndGun"))Items.Give(a,"LP_GS_DrifterShot",300,1f);
    if(Items.Has(v,"LP_GS_Spotted"))d.SetAmount(d.Amount*1.15f);
   }
   if(!Is(v) || v.Dead || a==null || a==v || !a.HostileTo(v) || d.Amount<=0f)return;
   bool ranged=AttackKind.IsRanged(d);
   if(ranged && Items.Has(v,"LP_GS_Covered"))d.SetAmount(d.Amount*.85f);
   if(Items.Has(v,"LP_GS_StormBuff"))d.SetAmount(d.Amount*.8f);
   if(d.Def.isExplosive && Knows(v,"LP_GS_Evasive") && HasGrit(v))d.SetAmount(d.Amount*.5f);
   if(!ranged && !d.Def.isExplosive && Knows(v,"LP_GS_HeavyStance") && Holding(v)==Kind.Heavy)d.SetAmount(d.Amount*.85f);
   // 枪手闪避：被远程攻击打中时（每 5 秒一次）有 30% 闪开，花 1 点胆识；穿重甲不行。双枪客的以枪还枪：闪开就还它一枪。
   if(ranged && Knows(v,"LP_GS_Dodge") && HasGrit(v) && MonkCore.Unarmored(v)){var vd=Data(v);if(Ready(vd,DodgeKey,300)){Use(vd,DodgeKey);if(Rand.Chance(DodgeChance) && Pay(v,1f)){d.SetAmount(0f);Items.Mote(v,"闪避".Loc(),Gun);if(Knows(v,"LP_GS_Retort") && GunOf(v)!=null && a.Spawned && v.Spawned){inExtra=true;try{Shot(v,a,1f,0f,false);}finally{inExtra=false;}}}}}
  }
  public static readonly HashSet<int> Bleed=new HashSet<int>();public static readonly Dictionary<int,float> Runes=new Dictionary<int,float>();
  public static void PostDamage(Pawn v,DamageInfo d){
   if(Runes.Count>0 && Runes.TryGetValue(v.thingIDNumber,out float fire)){Runes.Remove(v.thingIDNumber);if(!v.Dead)v.TakeDamage(new DamageInfo(DamageDefOf.Flame,fire,.1f,-1f,d.Instigator));}
   if(Bleed.Count==0 || !Bleed.Remove(v.thingIDNumber) || v.Dead)return;var a=d.Instigator as Pawn;
   var part=d.HitPart??v.health.hediffSet.GetNotMissingParts().Where(bp=>bp.depth==BodyPartDepth.Outside).RandomElementWithFallback();
   if(part!=null)v.TakeDamage(new DamageInfo(DamageDefOf.Cut,4f,0f,-1f,a,part));
  }
  public static void Killed(Pawn victim,Pawn killer,DamageInfo? dinfo){
   if(!Is(killer) || killer.Dead || DeathShots.Contains(victim.thingIDNumber))return;
   var w=dinfo?.Weapon;if(w==null || !Firearm(w) || !Countable(killer,victim))return;
   Gain(killer,1f,"击杀".Loc());
   var duel=ClassMartial2.Mark(victim,"LP_GS_Dueled");if(duel!=null && duel.caster==killer){var h=Adventurer.Get(killer);ClassSystem.AddResource(h,Class,99f);Items.Mote(killer,"决斗胜利：胆识回满".Loc(),Gun);}
   if(Items.Has(killer,"LP_LegBulletStormBuff"))Gain(killer,1f,"子弹风暴".Loc());
   if(Knows(killer,"LP_GS_Skirmish"))Items.Give(killer,"LP_GS_SkirmishBuff",300,1f);
  }
  public static void Downed(Pawn victim,DamageInfo? dinfo){
   var killer=dinfo?.Instigator as Pawn;if(!Is(killer) || killer.Dead)return;
   var w=dinfo?.Weapon;if(w==null || !Firearm(w) || !Countable(killer,victim))return;
   Gain(killer,1f,"打倒".Loc());
   if(Knows(killer,"LP_GS_Skirmish"))Items.Give(killer,"LP_GS_SkirmishBuff",300,1f);
  }
  // 枪手好运（13 级）：心智抗性没挺住时花 2 点胆识重来一次，每天 3 次。
  public static bool Luck(Pawn p){
   if(Users.Count==0 || !Is(p) || !Knows(p,"LP_GS_SlingerLuck") || !HasGrit(p,2f))return false;var d=Data(p);if(d==null)return false;
   int day=Now/GenDate.TicksPerDay;d.ticks.TryGetValue(LuckKey,out int v);if(v/10!=day)v=day*10;if(v%10>=3)return false;
   d.ticks[LuckKey]=v+1;Pay(p,2f);Items.Mote(p,"枪手好运".Loc(),Gun);return true;
  }
  // 欺骗死神（17 级）：本该倒地时花光胆识（至少 1 点）撑住，治好四成伤势，每天一次。
  public static bool TryCheatDeath(Pawn p){
   if(Users.Count==0 || p==null || p.Dead || p.Downed || !Is(p) || !Knows(p,"LP_GS_CheatDeath") || !HasGrit(p) || !ClassCasters.WouldDown(p))return false;
   var d=Data(p);if(!Ready(d,CheatKey,GenDate.TicksPerDay))return false;Use(d,CheatKey);
   Pay(p,Grit(p));ClassMelee.Heal(p,ClassMelee.InjurySum(p)*.4f);
   Items.Mote(p,"欺骗死神".Loc(),Gun);if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"欺骗了死神：本该倒下，却又站稳了。".Loc(),p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  // 传奇·真胆识：枪手能力里消耗 2 点以上的少 1 点。
  public static bool TrueGrit(Pawn p,AbilityDef def)=>Users.Count>0 && Is(p) && ClassSystem.ClassOf(def)==Class && ClassLegendary.HasLeg(p,"LP_LegTrueGrit");
  public static bool CanQualityBoost(Pawn worker,Thing product)=>worker!=null && product!=null && Is(worker) && Knows(worker,"LP_GS_Gunsmith") && Firearm(product.def);
 }

 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class GunslingerDamage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(Gunslinger.Users.Count==0)return;
   if(dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   Gunslinger.Damage(__instance,ref dinfo);
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PostApplyDamage))]
 public static class GunslingerBleed {
  static void Postfix(Pawn __instance,DamageInfo dinfo){if(Gunslinger.Bleed.Count>0 || Gunslinger.Runes.Count>0)Gunslinger.PostDamage(__instance,dinfo);}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class GunslingerKill {
  static void Postfix(Pawn __instance,DamageInfo? dinfo){if(Gunslinger.Users.Count==0 || !__instance.Dead)return;if(dinfo?.Instigator is Pawn k && k!=__instance)Gunslinger.Killed(__instance,k,dinfo);}
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class GunslingerDowned {
  static void Postfix(Pawn ___pawn,DamageInfo? dinfo){if(Gunslinger.Users.Count>0 && ___pawn!=null && ___pawn.Downed)Gunslinger.Downed(___pawn,dinfo);}
 }
 [HarmonyPatch(typeof(Verb_LaunchProjectile),"TryCastShot")]
 public static class GunslingerExtraShot {
  static void Postfix(Verb_LaunchProjectile __instance,bool __result){if(__result && Gunslinger.Users.Count>0)Gunslinger.AfterShot(__instance);}
 }
 // 枪匠：枪手做火器时 25% 品质高一档。
 [HarmonyPatch(typeof(GenRecipe),"PostProcessProduct")]
 public static class GunslingerGunsmith {
  static void Postfix(Thing __result,Pawn worker){
   if(Gunslinger.Users.Count==0 || !Gunslinger.CanQualityBoost(worker,__result))return;
   var q=__result.TryGetComp<CompQuality>();if(q==null || q.Quality>=QualityCategory.Legendary || !Rand.Chance(.25f))return;
   q.SetQuality(q.Quality+1,ArtGenerationContext.Colony);Items.Mote(worker,"枪匠：品质 +1".Loc(),Gunslinger.Gun);
  }
 }
}
