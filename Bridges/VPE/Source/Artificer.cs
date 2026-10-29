using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.29.0 器械师（手工）：被动能力控制“符文火器”“奥术法杖”武器的制作权限、品质与魔力共鸣；主动能力是快速维修、过载射击、魔力充能、符文炮台。
 // 主程序集的魔力系数、制作品质留了登记表（Arcana.SensitivityFloorHooks / QualityBonusHooks），这里往里登记。
 [StaticConstructorOnStartup]
 public static class Artificer {
  public const string PathName="LP_Path_Artificer";
  public const string Workshop="LP_ArcaneWorkshop",Inscription="LP_RuneInscription",MasterCraft="LP_MasterCraft",MountainForge="LP_MountainForge",Resonance="LP_ArcaneResonance";
  public const float RepairFraction=.25f,OverchargeWear=.05f,ResonanceFloor=1.5f;
  public static LP_ClassPathDef Path=>Classes.Path(PathName);
  // 制作权限：被动能力 → 能做的武器。
  public static readonly Dictionary<string,string> RequiredAbility=new Dictionary<string,string>{
   {"LP_MissileStaff",Workshop},{"LP_FireStaff",Workshop},{"LP_FrostStaff",Workshop},{"LP_RunicPistol",Workshop},{"LP_RunicMusket",Workshop},
   {"LP_SilverPistol",Inscription},{"LP_LavaPiercer",Inscription},{"LP_ThunderGun",Inscription},{"LP_HammerCannon",Inscription},{"LP_RunicRepeater",Inscription},
   {"LP_MountainWrath",MountainForge},
   // 1.43.0 DND 近战武器和精灵长弓（传说级、不可制作的和投掷物不进来）。大师工艺只认符文枪械和法杖（CraftedQuality 要求 CompProperties_ArcaneWeapon），这些不加品质。
   {"LP_OgreClub",Workshop},{"LP_DwarvenThrowingHammer",Workshop},{"LP_ElvenLongbow",Workshop},{"LP_OrcishGreataxe",Workshop},{"LP_SilverSword",Workshop},{"LP_FrostGiantAxe",Workshop},
   {"LP_FrostSpear",Inscription},{"LP_EmpyreanBlade",Inscription},{"LP_CloudMorningstar",Inscription},{"LP_StormTrident",Inscription},{"LP_FireGiantMaul",Inscription},{"LP_MoltenMaul",Inscription},
   // 1.44.0 独眼巨锤（只在启用 HAR 时存在）：同霜巨人斧（钢/木的巨人专用近战），奥术工坊。双盔、透镜是衣服/饰品，不设门槛。
   {"LP_CyclopsMaul",Workshop},
   // 1.45.0 魔化兵器：火焰舌、霜寒之刃、龙杀手、守护者之剑走符文铭刻，闪电标枪走奥术工坊（锐锋剑、复仇之剑不能制作）。
   {"LP_FlameTongue",Inscription},{"LP_FrostBrand",Inscription},{"LP_DragonSlayer",Inscription},{"LP_DefenderSword",Inscription},{"LP_JavelinLightning",Workshop},
   // 1.61.0 制伏武器：五件都走奥术工坊。
   {"LP_SubduingCudgel",Workshop},{"LP_NetCrossbow",Workshop},{"LP_SleepBow",Workshop},{"LP_SleepStaff",Workshop},{"LP_SleepDustPouch",Workshop}};
  public const string NeedPrefix="制作需要：器械师";
  static Artificer(){
   Arcana.SensitivityFloorHooks.Add((p,g)=>Arcana.Magic(g) && Knows(p,Resonance)?ResonanceFloor:0f);
   Arcana.QualityBonusHooks.Add((p,d)=>Knows(p,MasterCraft)?1:0);
   AnnotateDescriptions();
  }
  // 武器和产出它的配方说明末尾写明需要哪个被动能力（已经写过就不重复）。
  static void AnnotateDescriptions(){
   foreach(var kv in RequiredAbility){
    var thing=DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key);var ability=DefDatabase<AbilityDef>.GetNamedSilentFail(kv.Value);if(thing==null || ability==null)continue;
    var line=NeedPrefix+"「"+ability.label+"」。";
    thing.description=Append(thing.description,line);
    foreach(var r in DefDatabase<RecipeDef>.AllDefsListForReading)if(r.ProducedThingDef==thing)r.description=Append(r.description,line);
   }
  }
  static string Append(string text,string line)=>text.NullOrEmpty()?line:text.Contains(line)?text:text+"\n\n"+line;
  public static bool Knows(Pawn p,string ability){
   var comp=p?.GetComp<CompAbilities>();var def=DefDatabase<AbilityDef>.GetNamedSilentFail(ability);
   return comp!=null && def!=null && comp.HasAbility(def);
  }
  public static string Needed(RecipeDef r){var product=r?.ProducedThingDef;return product!=null && RequiredAbility.TryGetValue(product.defName,out var a)?a:null;}
  // 1.43.0：设置“DND武器需要器械师”关掉时谁都能做。
  public static bool MayCraft(Pawn p,RecipeDef r){if(LichMod.Settings!=null && !LichMod.Settings.dndWeaponsNeedArtificer)return true;var need=Needed(r);return need==null || Knows(p,need);}
  // 快速维修：主武器与每件服装各恢复 25% 最大耐久。返回修了几件。
  public static int Repair(Pawn t){
   int n=0;var items=new List<Thing>();
   if(t?.equipment?.Primary!=null)items.Add(t.equipment.Primary);
   if(t?.apparel!=null)items.AddRange(t.apparel.WornApparel);
   foreach(var item in items){
    if(!item.def.useHitPoints || item.HitPoints>=item.MaxHitPoints)continue;
    item.HitPoints=Math.Min(item.MaxHitPoints,item.HitPoints+Math.Max(1,Mathf.RoundToInt(item.MaxHitPoints*RepairFraction)));n++;
   }
   return n;
  }
  // 过载射击：远程命中时伤害 ×(1+器械强度)，消耗状态，武器掉 5% 耐久。返回倍率。
  public static float Overcharge(ref DamageInfo dinfo){
   var shooter=dinfo.Instigator as Pawn;if(shooter==null || dinfo.Weapon==null || !dinfo.Weapon.IsRangedWeapon || dinfo.Amount<=0f)return 1f;
   var def=Items.Hediff("LP_Overcharge");var h=def==null?null:shooter.health?.hediffSet?.GetFirstHediffOfDef(def);if(h==null)return 1f;
   float f=1f+h.Severity;dinfo.SetAmount(dinfo.Amount*f);
   shooter.health.RemoveHediff(h);
   var gun=shooter.equipment?.Primary;
   if(gun!=null && gun.def.useHitPoints){gun.HitPoints=Math.Max(1,gun.HitPoints-Math.Max(1,Mathf.RoundToInt(gun.MaxHitPoints*OverchargeWear)));}
   return f;
  }
 }
 [HarmonyPatch(typeof(RecipeDef),nameof(RecipeDef.PawnSatisfiesSkillRequirements))]
 public static class ArtificerRecipeGate {
  static void Postfix(RecipeDef __instance,Pawn pawn,ref bool __result){if(__result && pawn!=null && !Artificer.MayCraft(pawn,__instance))__result=false;}
 }
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class OverchargeHit {
  static void Prefix(ref DamageInfo __0){Artificer.Overcharge(ref __0);}
 }

 // 被动能力：没有按钮，学会即生效。
 public class Ability_ArtificerPassive : ClassAbility {
  public override bool ShowGizmoOnPawn()=>false;
  public override void Apply(GlobalTargetInfo[] targets){}
 }
 public class Ability_QuickRepair : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,true,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(!Friendly(pawn,t))continue;int n=Artificer.Repair(t);Mote(t,"修好 "+n+" 件",Artisan);}
  }
  public static readonly Color Artisan=new Color(.95f,.8f,.45f);
 }
 public class Ability_ArcaneCharge : ClassAbility {
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true)=>base.ValidateTarget(target,showMessages) && TargetPawnOk(target,true,showMessages,out _);
  public override void Apply(GlobalTargetInfo[] targets){
   foreach(var g in targets){var t=PawnOf(g);if(!Friendly(pawn,t))continue;Give(t,"LP_ArcaneCharged",Duration,1f);Mote(t,"魔力充能",Arcane);}
  }
 }
 public class Ability_RuneTurret : ClassAbility {
  public static string Cannot(Map map,IntVec3 c){
   if(map==null || !c.IsValid || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map))return "需要一个能站立的空格子。";
   if(c.GetEdifice(map)!=null || c.GetFirstPawn(map)!=null)return "这个格子被占着。";
   return null;
  }
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(!base.ValidateTarget(target,showMessages))return false;
   var why=Cannot(pawn.Map,target.Cell);if(why!=null){if(showMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)Place(pawn,g.Cell,Strength);}
  public static Building_RuneTurret Place(Pawn caster,IntVec3 cell,float power){
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("LP_RuneTurret");var map=caster?.Map;if(def==null || Cannot(map,cell)!=null)return null;
   var t=(Building_RuneTurret)ThingMaker.MakeThing(def);t.power=power;t.SetFaction(caster.Faction);
   GenSpawn.Spawn(t,cell,map);
   FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,2f,new Color(.6f,.8f,1f));
   return t;
  }
 }
 // 符文炮台：伤害倍率 = 放置时的器械强度（存档）。
 public class Building_RuneTurret : Building_TurretGun, IArcanePowerSource {
  public float power=1f;
  public float ArcanePower=>power;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref power,"lpPower",1f);}
  public override string GetInspectString(){var s=base.GetInspectString();return (s.NullOrEmpty()?"":s+"\n")+"器械强度 ×"+power.ToString("0.##");}
 }
}
