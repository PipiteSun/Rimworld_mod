using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.29.0 DND 枪械与法杖。D 组魔法枪、E 组高级魔法枪是普通枪；F 组魔力矮人枪械、G 组法杖按射手的心灵敏感度算“魔力系数”乘进自己投射物的伤害。
 // 魔力系数只乘在 LP_ArcaneBullet / LP_ArcaneShell 的 DamageAmount 上，并且是在原版 getter（含 Isekai 的后缀与武器精炼属性）算完之后再乘，所以 Isekai 的强化照常生效。
 public class CompProperties_ArcaneWeapon : CompProperties {
  public string group="D";      // D 魔法枪 / E 高级魔法枪 / F 魔力矮人枪械 / G 法杖
  public bool dwarfOnly;        // 山岳之怒：只有矮人能装备
  public bool silverBane;       // 圣银手枪：对死灵造物、巫妖、魔鬼 ×1.5
  public CompProperties_ArcaneWeapon(){compClass=typeof(CompArcaneWeapon);}
 }
 public class CompArcaneWeapon : ThingComp {
  public CompProperties_ArcaneWeapon Props=>(CompProperties_ArcaneWeapon)props;
  public Pawn Wielder=>(parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;
  public override IEnumerable<StatDrawEntry> SpecialDisplayStats(){
   var inherited=base.SpecialDisplayStats();if(inherited!=null)foreach(var e in inherited)yield return e;
   if(!Arcana.Magic(Props.group))yield break;
   var p=Wielder;
   string value=p==null?"按持有者计算":Arcana.FactorLine(p,parent.def);
   yield return new StatDrawEntry(StatCategoryDefOf.Weapon_Ranged,"魔力系数",value,Arcana.Explain(Props.group),5100);
  }
  public override string CompInspectStringExtra(){var p=Wielder;return p==null || !Arcana.Magic(Props.group)?null:"魔力系数 "+Arcana.FactorLine(p,parent.def);}
 }
 // 投射物附加效果：点燃、EMP 爆发 + 人形眩晕、命中状态、穿透、必中。
 public class ArcaneShotExt : DefModExtension {
  public float igniteChance;
  public float empRadius;public int stunTicks;
  public string hitHediff;public int hitHediffTicks;
  public float pierceChance;
  public bool alwaysHit;
 }
 // 符文炮台等不是小人的发射者：直接给出倍率（VPE 器械师路径的炮台用职业强度）。
 public interface IArcanePowerSource {float ArcanePower{get;}}

 public static class Arcana {
  public const float StaffMin=.2f,GunBase=.5f,GunSlope=.5f,GunMax=2f,NoLinkFactor=.7f,ChargeBonus=.5f,ResistFactor=.75f,BrandFactor=1.15f,SilverFactor=1.5f;
  public const int DispelLockTicks=600,EmpStunHumanlike=60;
  public static readonly string[] GroupD={"LP_SilverPistol","LP_LavaPiercer","LP_ThunderGun"},GroupE={"LP_Doom","LP_Skyfire"},GroupF={"LP_RunicMusket","LP_RunicPistol","LP_HammerCannon","LP_RunicRepeater","LP_MountainWrath"},GroupG={"LP_MissileStaff","LP_FireStaff","LP_FrostStaff"};
  public static IEnumerable<string> All=>GroupD.Concat(GroupE).Concat(GroupF).Concat(GroupG);
  // 联动登记：敏感度保底（VPE 魔力共鸣 1.5）、制作品质加成（VPE 大师工艺 +1）。
  public static readonly List<Func<Pawn,string,float>> SensitivityFloorHooks=new List<Func<Pawn,string,float>>();
  public static readonly List<Func<Pawn,ThingDef,int>> QualityBonusHooks=new List<Func<Pawn,ThingDef,int>>();
  public static CompProperties_ArcaneWeapon Props(ThingDef d)=>d?.GetCompProperties<CompProperties_ArcaneWeapon>();
  public static string Group(ThingDef d)=>Props(d)?.group;
  public static bool Magic(string g)=>g=="F" || g=="G";
  // 魔力系数公式（敏感度已含保底）。法杖：敏感度本身，下限 0.2、不封顶（1.30.1 去掉 2.5 上限），敏感度 0 不能用；矮人枪械：0.5 + 0.5 × 敏感度，上限 2。
  public static float Factor(string group,float sens){
   if(group=="G")return sens<=0f?0f:Mathf.Max(sens,StaffMin);
   if(group=="F")return Mathf.Min(GunMax,GunBase+GunSlope*Mathf.Max(0f,sens));
   return 1f;
  }
  public static bool RedRobe(Pawn p){var player=Faction.OfPlayerSilentFail;return p?.kindDef?.defName=="LP_FH_RedWizard" && p.Faction!=null && player!=null && p.Faction!=player && p.Faction.HostileTo(player);}
  public static float Floor(Pawn p,string group){
   float f=0f;
   if(group=="F" && CommonRaces.IsDwarf(p))f=1f;     // 矮人符文共鸣
   if(group=="G" && RedRobe(p))f=1f;                 // 敌方红袍法师
   foreach(var hook in SensitivityFloorHooks){try{f=Mathf.Max(f,hook(p,group));}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 魔力系数联动出错："+e,9129001);}}
   return f;
  }
  public static float RawSensitivity(Pawn p)=>p==null?1f:p.GetStatValue(StatDefOf.PsychicSensitivity);
  public static float Sensitivity(Pawn p,string group)=>Mathf.Max(RawSensitivity(p),Floor(p,group));
  public static bool Charged(Pawn p)=>Items.Has(p,"LP_ArcaneCharged");
  // 原版 Pawn.HasPsylink 按游戏刻缓存，刚获得心灵链接的同一刻读不到；这里直接看等级。
  public static bool Linked(Pawn p)=>p!=null && p.GetPsylinkLevel()>0;
  // 实际倍率：公式 + 魔力充能（+0.5，只对 F、G）；法杖持有者没有心灵链接时 ×0.7。
  public static float FactorFor(Pawn p,ThingDef weapon){
   var g=Group(weapon);if(!Magic(g) || p==null)return 1f;
   float f=Factor(g,Sensitivity(p,g));if(f<=0f)return 0f;
   if(Charged(p))f+=ChargeBonus;
   if(g=="G" && !Linked(p))f*=NoLinkFactor;
   if(g=="G")f*=HeroDestiny.StaffFactor(p); // 1.58.0 魔网之子 法杖伤害 +20%
   return f;
  }
  public static string FactorLine(Pawn p,ThingDef weapon){
   var g=Group(weapon);float s=Sensitivity(p,g);
   var line="×"+FactorFor(p,weapon).ToString("0.##")+"（心灵敏感度 "+s.ToStringPercent()+"）";
   if(g=="G" && !Linked(p))line+="，没有心灵链接 ×0.7";
   if(Charged(p))line+="，魔力充能 +0.5";
   return line;
  }
  public static string Explain(string g)=>g=="G"?"法杖的伤害 × 魔力系数。魔力系数 = 心灵敏感度（最低 0.2，不封顶）；敏感度为 0 时法杖毫无回应。有心灵链接的持有者每次施放增加心灵熵（模组设置），熵满时不能施放；没有心灵链接时伤害只有 70%。穿寒铁护甲或有心智抗性的目标受到的伤害 -25%。"
   :"矮人枪械的伤害 × 魔力系数。魔力系数 = 0.5 + 0.5 × 心灵敏感度，最高 2；矮人不足 100% 按 100% 算。不加心灵熵。穿寒铁护甲或有心智抗性的目标受到的伤害 -25%。";
  public static int Scaled(int raw,float factor){if(factor==1f)return raw;if(factor<=0f)return 0;return Mathf.Max(1,Mathf.RoundToInt(raw*factor));}

  // ---------- 法杖能否施放 ----------
  public static float EntropyPerShot=>LichMod.Settings?.staffEntropy??2f;
  public static bool CanFire(Pawn p,ThingDef weapon,out string reason){
   reason=null;if(p==null || Group(weapon)!="G")return true;
   if(Factor("G",Sensitivity(p,"G"))<=0f){reason="法杖毫无回应（心灵敏感度为 0）";return false;}
   if(DispelLocked(p)){reason="驱散粉尘压住了魔力，法杖暂时施放不出来";return false;}
   if(NetherilAscension.WeaveCollapsed(p.MapHeld) && !HeroDestiny.WeaveImmune(p)){reason=NetherilAscension.CollapseReason;return false;} // 1.58.0 魔网之子升华不受魔网崩溃影响
   if(Linked(p) && p.psychicEntropy!=null && EntropyPerShot>0f && p.psychicEntropy.WouldOverflowEntropy(EntropyPerShot)){reason="心灵熵已满，法杖施放不出来";return false;}
   return true;
  }
  public static bool PaysEntropy(Pawn p,ThingDef weapon)=>p!=null && Group(weapon)=="G" && Linked(p) && p.psychicEntropy!=null && EntropyPerShot>0f;
  public static void PayEntropy(Pawn p,ThingDef weapon){if(PaysEntropy(p,weapon))p.psychicEntropy.TryAddEntropy(EntropyPerShot,p,true,false);}
  public static bool DispelLocked(Pawn p){var m=MapComp<GunMap>.Get(p?.Map);return m!=null && m.Locked(p.Position);}

  // ---------- 命中后 ----------
  public static bool ResistsArcane(Pawn t){
   if(t==null)return false;
   var worn=t.apparel?.WornApparel;var cold=ItemMaterials.ColdIron;
   if(worn!=null && cold!=null)for(int i=0;i<worn.Count;i++)if(worn[i].Stuff==cold)return true;
   return DNDMind.Resistance(t)>0f;
  }
  public static float TargetFactor(Pawn t,ThingDef weapon){
   var props=Props(weapon);if(props==null || t==null)return 1f;float f=1f;
   if(Magic(props.group)){if(ResistsArcane(t))f*=ResistFactor;if(Items.Has(t,"LP_RuneBrand"))f*=BrandFactor;}
   if(props.silverBane && ItemThrowables.HolyTarget(t))f*=SilverFactor;
   return f;
  }
  // 雷鸣铳：半径内 EMP，人形眩晕 1 秒。返回被眩晕的人形数。
  public static int EmpBurst(Map map,IntVec3 cell,float radius,Thing instigator,int stunTicks){
   if(map==null || !cell.InBounds(map))return 0;
   GenExplosion.DoExplosion(cell,map,radius,DamageDefOf.EMP,instigator,damAmount:10,armorPenetration:0f,doSoundEffects:true,doVisualEffects:true);
   int n=0;
   foreach(var p in GenRadial.RadialDistinctThingsAround(cell,map,radius,true).OfType<Pawn>().ToList()){
    if(p.Dead || !p.RaceProps.Humanlike)continue;
    p.stances?.stunner?.StunFor(stunTicks,instigator,false,true,false);n++;
   }
   return n;
  }
  // 噩耗：命中头部或躯干时，沿射击方向找身后 8 格内的下一个人。
  public static bool Vital(BodyPartRecord part){for(var p=part;p!=null;p=p.parent){var n=p.def.defName;if(n=="Head" || n=="Neck")return true;}return part!=null && part.def.defName=="Torso";}
  public static Pawn PierceTarget(Map map,Vector3 from,Pawn victim,int maxDist=8){
   if(map==null || victim==null)return null;
   var dir=victim.Position.ToVector3Shifted()-from;dir.y=0f;if(dir.sqrMagnitude<.01f)return null;dir.Normalize();
   for(int i=1;i<=maxDist;i++){
    var c=(victim.Position.ToVector3Shifted()+dir*i).ToIntVec3();if(!c.InBounds(map))break;
    if(c.GetEdifice(map) is Building b && b.def.Fillage==FillCategory.Full)break;
    foreach(var t in c.GetThingList(map))if(t is Pawn p && p!=victim && !p.Dead)return p;
   }
   return null;
  }
  public static void AfterHit(ArcaneShotExt ext,Thing hit,IntVec3 cell,Map map,Thing launcher){
   if(ext==null || map==null)return;
   if(ext.igniteChance>0f && hit!=null && !hit.Destroyed && Rand.Chance(ext.igniteChance))hit.TryAttachFire(.5f,launcher);
   if(ext.empRadius>0f)EmpBurst(map,cell,ext.empRadius,launcher,ext.stunTicks>0?ext.stunTicks:EmpStunHumanlike);
   if(!ext.hitHediff.NullOrEmpty() && hit is Pawn p && !p.Dead)Items.Give(p,ext.hitHediff,ext.hitHediffTicks);
  }

  // ---------- 制作品质 ----------
  // 矮人做 F 组 +1（最多到大师级）；联动加成（VPE 大师工艺）再叠加，最多到传说。
  public static QualityCategory CraftedQuality(QualityCategory q,Pawn worker,ThingDef def){
   if(Props(def)==null)return q;
   if(Group(def)=="F" && CommonRaces.IsDwarf(worker) && q<QualityCategory.Masterwork)q++;
   int bonus=0;foreach(var hook in QualityBonusHooks){try{bonus+=hook(worker,def);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 武器品质联动出错："+e,9129002);}}
   if(bonus>0)q=(QualityCategory)Math.Min((int)QualityCategory.Legendary,(int)q+bonus);
   return q;
  }
 }

 // 驱散粉尘落点：5×5 内 10 秒不能用法杖（不存档，只是 10 秒的短效）。
 public class GunMap : MapComponent {
  readonly List<(CellRect rect,int until)> locks=new List<(CellRect,int)>();
  public GunMap(Map map):base(map){}
  public void Lock(IntVec3 center){locks.RemoveAll(l=>l.until<=Find.TickManager.TicksGame);locks.Add((CellRect.CenteredOn(center,2),Find.TickManager.TicksGame+Arcana.DispelLockTicks));}
  public bool Locked(IntVec3 c){int now=Find.TickManager.TicksGame;for(int i=0;i<locks.Count;i++)if(locks[i].until>now && locks[i].rect.Contains(c))return true;return false;}
  public void Clear()=>locks.Clear();
 }
 [HarmonyPatch(typeof(ItemThrowables),nameof(ItemThrowables.Dispel))]
 public static class DispelLocksStaves {static void Postfix(Map map,IntVec3 center){MapComp<GunMap>.Get(map)?.Lock(center);}}

 // F、G 组共用的射击动词：法杖不能施放时按钮灰掉（Available），第一发扣心灵熵；必中的投射物（魔法飞弹）直接打向目标，无视掩体和距离精度。
 public class Verb_ArcaneShoot : Verb_Shoot {
  ThingDef Weapon=>EquipmentSource?.def;
  public override bool Available()=>base.Available() && Arcana.CanFire(CasterPawn,Weapon,out _);
  protected override bool TryCastShot(){
   var p=CasterPawn;
   if(!Arcana.CanFire(p,Weapon,out var why)){if(p?.Faction==Faction.OfPlayer)Messages.Message(why,p,MessageTypeDefOf.RejectInput,false);return false;}
   bool first=burstShotsLeft>=BurstShotCount;
   bool ok=Projectile?.GetModExtension<ArcaneShotExt>()?.alwaysHit==true?LaunchSure():base.TryCastShot();
   if(ok && first)Arcana.PayEntropy(p,Weapon);
   return ok;
  }
  bool LaunchSure(){
   if(!currentTarget.IsValid || caster?.Map==null || !TryFindShootLineFromTo(caster.Position,currentTarget,out var line,false))return false;
   var proj=(Projectile)GenSpawn.Spawn(Projectile,line.Source,caster.Map);
   var origin=caster.DrawPos+new Vector3(Rand.Range(-.3f,.3f),0f,Rand.Range(-.3f,.3f));
   proj.Launch(caster,origin,currentTarget,currentTarget,ProjectileHitFlags.IntendedTarget,preventFriendlyFire,EquipmentSource);
   return true;
  }
 }
 // 武器上的命令按钮：法杖不能施放时灰掉并说明原因。
 [HarmonyPatch(typeof(VerbTracker),"CreateVerbTargetCommand")]
 public static class ArcaneCommandDisable {
  static void Postfix(Verb verb,Command_VerbTarget __result){
   if(__result==null || !(verb is Verb_ArcaneShoot))return;
   if(!Arcana.CanFire(verb.CasterPawn,verb.EquipmentSource?.def,out var why))__result.Disable(why);
  }
 }

 // 公共投射物：伤害 = 原版伤害（已含武器精炼、Isekai 敏捷加成等）× 魔力系数。D、E 组的系数恒为 1。
 public class LP_ArcaneBullet : Bullet {
  float arcane=-1f;
  public float ArcaneFactor{get{if(arcane<0f)arcane=launcher is IArcanePowerSource s?s.ArcanePower:Arcana.FactorFor(launcher as Pawn,equipmentDef);return arcane;}}
  public void ResetFactor()=>arcane=-1f;
  public int RawDamage=>base.DamageAmount;
  public override int DamageAmount=>Arcana.Scaled(base.DamageAmount,ArcaneFactor);
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref arcane,"lpArcane",-1f);}
  protected override void Impact(Thing hitThing,bool blockedByShield=false){
   var map=Map;var cell=Position;var ext=def.GetModExtension<ArcaneShotExt>();
   if(ext!=null && ext.pierceChance>0f && hitThing is Pawn victim && !blockedByShield && map!=null){Pierce(victim,ext,map);Arcana.AfterHit(ext,victim,cell,map,launcher);return;}
   base.Impact(hitThing,blockedByShield);
   if(!blockedByShield)Arcana.AfterHit(ext,hitThing,cell,map,launcher);
  }
  // 噩耗：自己选命中部位；命中头部或躯干时有概率打穿身后直线上的下一个目标（同伤害）。
  void Pierce(Pawn victim,ArcaneShotExt ext,Map map){
   int amount=DamageAmount;float pen=ArmorPenetration;float angle=ExactRotation.eulerAngles.y;var from=origin;
   var part=victim.health.hediffSet.GetRandomNotMissingPart(DamageDef,BodyPartHeight.Undefined,BodyPartDepth.Outside);
   var weaponDef=equipmentDef;var inst=launcher;var intended=intendedTarget.Thing;
   Destroy();
   victim.TakeDamage(new DamageInfo(DamageDef,amount,pen,angle,inst,part,weaponDef,DamageInfo.SourceCategory.ThingOrUnknown,intended));
   if(Arcana.Vital(part) && Rand.Chance(ext.pierceChance)){
    var next=Arcana.PierceTarget(map,from,victim);
    if(next!=null){next.TakeDamage(new DamageInfo(DamageDef,amount,pen,angle,inst,null,weaponDef));Items.Mote(next,"贯穿",new Color(.85f,.85f,.95f));}
   }
  }
 }
 // 雷锤手炮：落点爆炸也乘魔力系数；不点火、不伤己方（同阵营与不敌对的人、同阵营建筑），并眩晕范围内的敌人。
 public class LP_ArcaneShell : Projectile_Explosive {
  float arcane=-1f;
  public float ArcaneFactor{get{if(arcane<0f)arcane=launcher is IArcanePowerSource s?s.ArcanePower:Arcana.FactorFor(launcher as Pawn,equipmentDef);return arcane;}}
  public int RawDamage=>base.DamageAmount;
  public override int DamageAmount=>Arcana.Scaled(base.DamageAmount,ArcaneFactor);
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref arcane,"lpArcane",-1f);}
  public static List<Thing> Friendly(Map map,IntVec3 cell,float radius,Thing launcher){
   var list=new List<Thing>();if(map==null)return list;
   foreach(var t in GenRadial.RadialDistinctThingsAround(cell,map,radius+1f,true)){
    if(t==launcher){list.Add(t);continue;}
    if(t is Pawn p){if(launcher!=null && !p.HostileTo(launcher))list.Add(p);}
    else if(launcher?.Faction!=null && t.Faction==launcher.Faction)list.Add(t);
   }
   return list;
  }
  protected override void Explode(){
   var map=Map;var cell=Position;float r=def.projectile.explosionRadius;int dmg=DamageAmount;float pen=ArmorPenetration;
   var inst=launcher;var weaponDef=equipmentDef;var intended=intendedTarget.Thing;var stun=def.GetModExtension<ArcaneShotExt>()?.stunTicks??60;
   var ignored=Friendly(map,cell,r,inst);
   Destroy();
   if(map==null)return;
   GenExplosion.DoExplosion(cell,map,r,def.projectile.damageDef,inst,dmg,pen,def.projectile.soundExplode,weaponDef,def,intended,chanceToStartFire:0f,damageFalloff:false,ignoredThings:ignored);
   foreach(var p in GenRadial.RadialDistinctThingsAround(cell,map,r,true).OfType<Pawn>().ToList())if(!p.Dead && !ignored.Contains(p))p.stances?.stunner?.StunFor(stun,inst,false,true,false);
  }
 }

 // 目标侧倍率：寒铁护甲 / 心智抗性 -25%、符文灼印 +15%（只对 F、G）；圣银手枪对亡灵、魔鬼 ×1.5。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class ArcaneTargetFactor {
  static void Prefix(Pawn __instance,ref DamageInfo __0){if(__0.Weapon==null || __0.Def==null)return;float f=Arcana.TargetFactor(__instance,__0.Weapon);if(f!=1f)__0.SetAmount(__0.Amount*f);}
 }
 // 山岳之怒只有矮人能装备。
 [HarmonyPatch(typeof(EquipmentUtility),nameof(EquipmentUtility.CanEquip),new[]{typeof(Thing),typeof(Pawn),typeof(string),typeof(bool)},new[]{ArgumentType.Normal,ArgumentType.Normal,ArgumentType.Out,ArgumentType.Normal})]
 public static class DwarfOnlyEquip {
  static void Postfix(Thing thing,Pawn pawn,ref string cantReason,ref bool __result){
   if(!__result || thing==null || pawn==null)return;
   if(Arcana.Props(thing.def)?.dwarfOnly==true && !CommonRaces.IsDwarf(pawn)){__result=false;cantReason="只有矮人能驾驭"+thing.def.label+"";}
  }
 }
 // AI 不会主动捡起对自己毫无回应的法杖。
 [HarmonyPatch(typeof(JobGiver_PickUpOpportunisticWeapon),"ShouldEquipWeapon")]
 public static class StaffAIPickup {
  static void Postfix(Thing newWep,Pawn pawn,ref bool __result){
   if(__result && newWep!=null && Arcana.Group(newWep.def)=="G" && Arcana.Factor("G",Arcana.Sensitivity(pawn,"G"))<=0f)__result=false;
  }
 }
 // 制作品质：矮人做 F 组 +1，联动加成另算。
 [HarmonyPatch(typeof(GenRecipe),"PostProcessProduct")]
 public static class ArcaneCraftQuality {
  static void Postfix(Thing __result,Pawn worker){
   var q=__result?.TryGetComp<CompQuality>();if(q==null || Arcana.Props(__result.def)==null || worker==null)return;
   var nq=Arcana.CraftedQuality(q.Quality,worker,__result.def);if(nq!=q.Quality)q.SetQuality(nq,ArtGenerationContext.Colony);
  }
 }

 // 天火降世：伤害、穿甲、射程取原版电荷长枪，瞄准与冷却各少 30%（启动时同步，其他模组改了长枪也跟着变）。
 [StaticConstructorOnStartup]
 public static class GunStartup {
  public const float SkyfireSpeedup=.7f;
  static readonly AccessTools.FieldRef<ProjectileProperties,int> damageBase=AccessTools.FieldRefAccess<ProjectileProperties,int>("damageAmountBase");
  static readonly AccessTools.FieldRef<ProjectileProperties,float> penBase=AccessTools.FieldRefAccess<ProjectileProperties,float>("armorPenetrationBase");
  static GunStartup(){try{SyncSkyfire();}catch(Exception e){Log.Warning("[DND边缘大陆] 同步天火降世数值失败："+e.Message);}}
  public static bool SyncSkyfire(){
   var lance=DefDatabase<ThingDef>.GetNamedSilentFail("Gun_ChargeLance");var sky=Items.Thing("LP_Skyfire");
   if(lance?.Verbs.NullOrEmpty()!=false || sky?.Verbs.NullOrEmpty()!=false)return false;
   var lv=lance.Verbs[0];var sv=sky.Verbs[0];var lp=lv.defaultProjectile?.projectile;var sp=sv.defaultProjectile?.projectile;if(lp==null || sp==null)return false;
   damageBase(sp)=damageBase(lp);penBase(sp)=penBase(lp);
   sv.range=lv.range;sv.warmupTime=lv.warmupTime*SkyfireSpeedup;
   sky.SetStatBaseValue(StatDefOf.RangedWeapon_Cooldown,lance.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown)*SkyfireSpeedup);
   return true;
  }
 }

 // 敌人与盟友携带：同盟矮人护卫 30% 带符文火铳或符文手铳；塞伊红袍法师 70% 火焰法杖 / 30% 寒霜法杖。设置“敌人携带魔法火器”可关。
 public static class GunGear {
  public const float DwarfChance=.3f,FireStaffChance=.7f;
  public static string Choose(string kind,float roll){
   if(kind=="LP_FC_DwarfGuard")return roll<DwarfChance/2f?"LP_RunicMusket":roll<DwarfChance?"LP_RunicPistol":null;
   if(kind=="LP_FH_RedWizard")return roll<FireStaffChance?"LP_FireStaff":"LP_FrostStaff";
   return null;
  }
  public static string OnGenerated(Pawn p){
   if(LichMod.Settings?.enemyMagicGuns!=true || p?.kindDef==null || p.Faction==null || p.Faction.IsPlayer)return null;
   var name=Choose(p.kindDef.defName,Rand.ValueSeeded(p.thingIDNumber^0x6E55));if(name==null)return null;
   var prim=p.equipment?.Primary?.def.defName;if(prim=="LP_AlchemistFire" || prim=="LP_HolyWater")return null; // 已经是投弹手
   return ItemGear.Arm(p,name)?name:null;
  }
 }
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class GunGearOnGeneration {[HarmonyPriority(Priority.Low)] static void Postfix(Pawn __result){GunGear.OnGenerated(__result);}}

 // 黑网：约 2% 的概率进一件噩耗或天火降世。
 public class StockGenerator_RareArcane : StockGenerator {
  public List<ThingDef> thingDefs=new List<ThingDef>();
  public float chance=.02f;
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   if(thingDefs.NullOrEmpty() || !Rand.Chance(chance))yield break;
   var t=StockGeneratorUtility.TryMakeForStockSingle(thingDefs.RandomElement(),1,faction);if(t!=null)yield return t;
  }
  public override bool HandlesThingDef(ThingDef thingDef)=>thingDefs.Contains(thingDef);
 }

 // Isekai 地下城宝箱：2 级以上可能掉 D、F、G 组，4 级以上可能掉噩耗或天火降世。按宝箱编号播种，不动全局随机数。
 public static class GunLoot {
  public const float CommonChance=.25f,LegendaryChance=.08f;
  public static string[] Common=>Arcana.GroupD.Concat(Arcana.GroupF).Concat(Arcana.GroupG).ToArray();
  public static List<string> Roll(int tier,Func<float> rand){
   var list=new List<string>();var common=Common;
   if(tier>=2 && rand()<CommonChance)list.Add(common[Math.Min(common.Length-1,(int)(rand()*common.Length))]);
   if(tier>=4 && rand()<LegendaryChance)list.Add(Arcana.GroupE[Math.Min(Arcana.GroupE.Length-1,(int)(rand()*Arcana.GroupE.Length))]);
   return list;
  }
  public static List<string> ForChest(Thing chest){int seed=chest.thingIDNumber^0x4755,i=0;return Roll(IsekaiLoot.Tier(chest),()=>Rand.ValueSeeded(seed+(i++)*7919));}
 }

 // 设置：敌人携带魔法火器（默认开）、法杖每发心灵熵（默认 2）。
 public partial class Settings {
  public bool enemyMagicGuns=true;public float staffEntropy=2f;public bool dndWeaponsNeedArtificer=true;
  public int subdueDamagePercent=100; // 1.61.0 制伏伤害倍率（Source/Nonlethal.cs）
  void ExposeGuns(){Scribe_Values.Look(ref enemyMagicGuns,"enemyMagicGuns",true);Scribe_Values.Look(ref staffEntropy,"staffEntropy",2f);Scribe_Values.Look(ref dndWeaponsNeedArtificer,"dndWeaponsNeedArtificer",true);staffEntropy=Mathf.Clamp(staffEntropy,0f,20f);Scribe_Values.Look(ref subdueDamagePercent,"subdueDamagePercent",100);subdueDamagePercent=Math.Max(50,Math.Min(200,subdueDamagePercent));}
 }
 public static class GunSettings {
  public static void Draw(Listing_Standard l){
   l.GapLine();
   l.CheckboxLabeled("敌人携带魔法火器",ref LichMod.Settings.enemyMagicGuns,"费伦拓荒同盟的矮人护卫30%带符文火铳或符文手铳，塞伊远征军的红袍法师带火焰法杖（70%）或寒霜法杖（30%）；Isekai 地下城宝箱也会掉 DND 枪械与法杖。");
   l.Label("法杖每发心灵熵："+LichMod.Settings.staffEntropy.ToString("0.#")+"（有心灵链接的持有者每次施放增加；熵满时不能施放）");
   LichMod.Settings.staffEntropy=Mathf.Round(l.Slider(LichMod.Settings.staffEntropy,0f,20f)*2f)/2f;
   l.Label("制伏伤害倍率："+LichMod.Settings.subdueDamagePercent+"%（制伏短棍、缚网弩、安眠短弓、安眠法杖、安眠粉囊让目标昏迷的速度）");
   LichMod.Settings.subdueDamagePercent=Mathf.RoundToInt(l.Slider(LichMod.Settings.subdueDamagePercent,50f,200f)/5f)*5;
   l.CheckboxLabeled("DND武器需要器械师",ref LichMod.Settings.dndWeaponsNeedArtificer,"只在同时启用 Vanilla Psycasts Expanded 时有效：符文枪械、法杖、DND 近战武器和精灵长弓要会器械师对应的被动能力（奥术工坊、符文铭刻、山岳锻造）才能制作。关掉后谁都能做（研究和技能要求照旧）。不装 VPE 时没有这条限制。");
  }
 }
}
