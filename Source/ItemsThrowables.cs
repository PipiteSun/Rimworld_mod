using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace LichPhylactery {
 // 1.22.0 炼金投掷物。炼金之火、强酸瓶、圣水照抄原版燃烧瓶/手雷；雷鸣石、黏胶袋、驱散粉尘用自己的投射物按方格范围生效。
 public static class ItemThrowables {
  public const int ThunderStunTicks=120,TangleTicks=600,DispelTicks=1800;
  public const float HolyDamage=30f,HolyFocusDrain=15f,AcidApparelFactor=3f;
  static DamageDef acid,holy;
  public static DamageDef Acid=>acid??(acid=DefDatabase<DamageDef>.GetNamedSilentFail("LP_AlchemyAcid"));
  public static DamageDef Holy=>holy??(holy=DefDatabase<DamageDef>.GetNamedSilentFail("LP_HolyWater"));
  public static IEnumerable<Pawn> PawnsIn(Map map,CellRect rect){foreach(var c in rect.ClipInsideMap(map).Cells)foreach(var t in c.GetThingList(map).ToList())if(t is Pawn p && !p.Dead)yield return p;}
  // 圣水只伤死灵造物与巫妖；1.24.0 起也伤魔鬼（寒冰魔、炎魔，伤害×2）。
  public static bool HolyTarget(Pawn p)=>p!=null && !p.Dead && (Items.Undead(p) || Fiends.Is(p) || Outsiders.HolyFactor(p)>1f || DndTraits.HolyWeak(p)); // 1.62.0 兽化症
  // 雷鸣石：3×3 眩晕2秒，并打断正在预热或引导的能力。
  public static int Thunder(Map map,IntVec3 center,Thing instigator){
   int n=0;
   foreach(var p in PawnsIn(map,CellRect.CenteredOn(center,1)).ToList()){
    p.stances?.CancelBusyStanceHard();
    if(p.CurJob?.ability!=null || p.CurJob?.verbToUse is Verb_CastAbility)p.jobs.EndCurrentJob(JobCondition.InterruptForced);
    p.stances?.stunner?.StunFor(ThunderStunTicks,instigator,false,true,false);n++;
   }
   FleckMaker.Static(center,map,FleckDefOf.ExplosionFlash,4f);
   foreach(var c in CellRect.CenteredOn(center,1).ClipInsideMap(map).Cells)FleckMaker.ThrowDustPuff(c.ToVector3Shifted(),map,1f);
   return n;
  }
  // 黏胶袋：3×3 移速-70%，10秒；体型超过2的只受一半效果。
  public static int Tangle(Map map,IntVec3 center){
   int n=0;
   foreach(var p in PawnsIn(map,CellRect.CenteredOn(center,1)).ToList()){Items.Give(p,"LP_Tanglefoot",TangleTicks,p.BodySize>2f?.5f:1f);n++;}
   foreach(var c in CellRect.CenteredOn(center,1).ClipInsideMap(map).Cells)if(c.Standable(map) && Rand.Chance(.6f))FilthMaker.TryMakeFilth(c,map,ThingDefOf.Filth_Slime);
   return n;
  }
  // 驱散粉尘：5×5 内的人30秒“魔法被驱散”；VPE 等护盾灵能直接消散；范围内的石像复原。
  public static int Dispel(Map map,IntVec3 center){
   int n=0;var rect=CellRect.CenteredOn(center,2);
   var ext=Items.Hediff("LP_MagicDispelled")?.GetModExtension<DispelExtension>();
   foreach(var p in PawnsIn(map,rect).ToList()){
    Items.Give(p,"LP_MagicDispelled",DispelTicks);n++;
    if(Necro.Construct(p)?.kind=="StoneGolem")p.TakeDamage(new DamageInfo(DamageDefOf.Blunt,MagicEffects.DispelGolemDamage,1f,-1f,null)); // 1.45.0 石魔像怕驱散
    if(ext!=null)foreach(var h in p.health.hediffSet.hediffs.Where(h=>ext.removeHediffs.Contains(h.def.defName)).ToList())p.health.RemoveHediff(h);
   }
   foreach(var statue in rect.ClipInsideMap(map).Cells.SelectMany(c=>c.GetThingList(map)).OfType<PetrifiedStatue>().ToList())statue.Restore();
   foreach(var c in rect.ClipInsideMap(map).Cells)if(Rand.Chance(.5f))FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,.8f);
   return n;
  }
 }
 public class DispelExtension : DefModExtension {public List<string> removeHediffs=new List<string>();}
 public class ThrowableEffectExtension : DefModExtension {public string effect;}
 // 继承原版爆炸投射物（AI 与配置检查都按爆炸物处理），但落地时不做原版爆炸，只按方格范围生效。
 public class Projectile_LPAlchemy : Projectile_Explosive {
  protected override void Explode(){
   var map=Map;var cell=Position;var effect=def.GetModExtension<ThrowableEffectExtension>()?.effect;
   Destroy();
   if(map==null)return;
   def.projectile.soundExplode?.PlayOneShot(new TargetInfo(cell,map));
   switch(effect){
    case "thunder":ItemThrowables.Thunder(map,cell,launcher);break;
    case "tangle":ItemThrowables.Tangle(map,cell);break;
    case "dispel":ItemThrowables.Dispel(map,cell);break;
    // 1.34.1 雾瓶：落点3格半径的致盲烟雾，维持30秒。
    case "fog":CloudGiant.Fog(map,cell,CloudGiant.FlaskRadius,CloudGiant.FogTicks);break;
    // 1.45.0 闪电标枪：命中点沿飞行方向3格放电；迷雾瓶：约7×7致盲烟雾2分钟。
    case "lightning":MagicEffects.Lightning(map,cell,origin,launcher);break;
    case "smoke":CloudGiant.Fog(map,cell,MagicEffects.SmokeRadius,MagicEffects.SmokeTicks);break;
    // 1.52.1 地狱火宝珠：半径 4 的地狱火球（一半火焰一半死灵）。
    case "hellfire":Monsters.Hellfire(map,cell,launcher as Pawn);break;
   }
  }
 }
 // 圣水：爆炸只作用于死灵造物与巫妖（约30点伤害，巫妖再扣15点精神力）；活人、建筑、物品都不受影响。
 public class DamageWorker_HolyWater : DamageWorker_AddInjury {
  protected override void ExplosionDamageThing(Explosion explosion,Thing t,List<Thing> damagedThings,List<Thing> ignoredThings,IntVec3 cell){if(t is Pawn p && ItemThrowables.HolyTarget(p))base.ExplosionDamageThing(explosion,t,damagedThings,ignoredThings,cell);}
  public override DamageResult Apply(DamageInfo dinfo,Thing thing){
   if(!(thing is Pawn p) || !ItemThrowables.HolyTarget(p))return new DamageResult();
   float hf=Outsiders.HolyFactor(p);if(hf!=1f)dinfo.SetAmount(dinfo.Amount*hf); // 1.54.0：倍率读外来者扩展（九狱之躯 / 深渊之躯 ×2）
   var result=base.Apply(dinfo,thing);
   var lich=Necro.Lich(p);
   if(lich!=null && p.psychicEntropy!=null){p.psychicEntropy.OffsetPsyfocusDirectly(-ItemThrowables.HolyFocusDrain/lich.Power);Items.Mote(p,"-"+ItemThrowables.HolyFocusDrain.ToString("0")+"精神力",new Color(1f,.95f,.6f));}
   return result;
  }
 }
 // 强酸瓶：被命中者身上的服装受到的伤害×3。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class ItemThingDamage {
  static void Prefix(Thing __instance,ref DamageInfo __0){
   if(__0.Def==null)return;
   float f=1f;
   if(__instance is Apparel a && a.Wearer!=null && __0.Def==ItemThrowables.Acid)f*=ItemThrowables.AcidApparelFactor;
   f*=ItemMaterials.WeaponFactor(__instance,__0);
   if(f!=1f)__0.SetAmount(__0.Amount*f);
  }
 }
 // 同盟士兵、塞伊远征军战士小概率成为投弹手（主武器换成炼金之火或圣水，比照原版投弹手）；敌对派系成员小概率随身带雷鸣石或驱散粉尘（死后掉落）。
 public static class ItemGear {
  public const float GrenadierChance=.08f,DropChance=.04f;
  public static readonly string[] AllianceSoldiers={"LP_FC_Guard","LP_FC_Archer","LP_FC_DwarfGuard","LP_FC_ElfRanger"};
  public static readonly string[] ThaySoldiers={"LP_FH_OrcWarrior","LP_FH_GithMercenary"};
  public static string GrenadeFor(Pawn p,float roll){
   var kind=p?.kindDef?.defName;if(kind==null || roll>=GrenadierChance)return null;
   if(AllianceSoldiers.Contains(kind))return roll<GrenadierChance/2?"LP_HolyWater":"LP_AlchemistFire";
   if(ThaySoldiers.Contains(kind))return "LP_AlchemistFire";
   return null;
  }
  public static bool Arm(Pawn p,string name){
   var def=Items.Thing(name);if(def==null || p?.equipment==null || p.WorkTagIsDisabled(WorkTags.Violent))return false;
   p.equipment.DestroyAllEquipment();p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(def));return true;
  }
  public static Thing Carry(Pawn p,float roll){
   if(p?.inventory==null || roll>=DropChance || p.Faction==null || p.Faction.IsPlayer || !p.RaceProps.Humanlike)return null;
   var t=ThingMaker.MakeThing(Items.Thing(roll<DropChance/2?"LP_Thunderstone":"LP_DispelDust"));p.inventory.innerContainer.TryAdd(t);return t;
  }
  public static void OnGenerated(Pawn p){
   if(p?.kindDef==null || !p.kindDef.defName.StartsWith("LP_") || p.Faction==null || p.Faction.IsPlayer)return;
   // 用按角色编号播种的随机数，不消耗全局随机数流（否则会改变同一局里其他所有随机结果的顺序）。
   var g=GrenadeFor(p,Rand.ValueSeeded(p.thingIDNumber^0x4C50)); if(g!=null)Arm(p,g);
   var player=Faction.OfPlayerSilentFail;if(player!=null && p.Faction.HostileTo(player))Carry(p,Rand.ValueSeeded(p.thingIDNumber^0x1D22));
  }
 }
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class ItemGearOnGeneration {static void Postfix(Pawn __result){ItemGear.OnGenerated(__result);}}
}
