using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.36.0 德鲁伊、圣武士、术士、武僧的核心自检（规格里标 ★ 的 10 条 + 每个能力施放一次不报错）。
 public static partial class BridgeChecks {
  static string savedShapeId;
  static LP_ClassPathDef Druid=>Classes.Path(Classes.DruidPath);
  static LP_ClassPathDef Paladin=>Classes.Path(Classes.PaladinPath);
  static LP_ClassPathDef Sorcerer=>Classes.Path(Classes.SorcererPath);
  static LP_ClassPathDef Monk=>Classes.Path(Classes.MonkPath);
  static void Skill(Pawn p,SkillDef s,int level){var r=p.skills.GetSkill(s);r.Level=level;}

  static void Classes2Checks(){
   Classes2Definitions();Classes2Unlocking();Classes2Psychic();Classes2WildShape();Classes2Smite();Classes2Smoke();
  }
  // ★1 每个能力的技能属于路径主副技能之一，门槛与环数对应（数量在 Definitions 里查）。
  static void Classes2Definitions(){
   foreach(var path in new[]{Druid,Paladin,Sorcerer,Monk}){
    Check(path!=null && path.abilities.All(a=>a.Psycast() is LP_ClassAbilityExt e && (e.Skill==path.requiredSkill || e.Skill==path.alternateSkill)),path?.defName+" abilities each key on the path's main or alternate skill");
    var top=path.abilities.Select(a=>(LP_ClassAbilityExt)a.Psycast()).Single(e=>e.level==5);
    Check(top.Skill==path.requiredSkill && top.requiredSkillLevel==18,path.defName+" capstone needs main skill 18");
   }
   Check(Druid.requiredSkill==SkillDefOf.Plants && Druid.alternateSkill==SkillDefOf.Animals && Paladin.requiredSkill==SkillDefOf.Melee && Paladin.alternateSkill==SkillDefOf.Social && Sorcerer.requiredSkill==SkillDefOf.Social && Sorcerer.alternateSkill==SkillDefOf.Intellectual && Monk.requiredSkill==SkillDefOf.Melee && Monk.alternateSkill==null,"druid plants/animals, paladin melee/social, sorcerer social/intellectual, monk melee");
  }
  // ★2 ★4 ★5 ★6
  static void Classes2Unlocking(){
   var d=Make(0,0,0);
   Skill(d,SkillDefOf.Plants,5);Skill(d,SkillDefOf.Animals,5);
   Check(!Druid.CanPawnUnlock(d),"druid locked at plants 5 / animals 5");
   Skill(d,SkillDefOf.Plants,6);
   Check(Druid.CanPawnUnlock(d),"druid unlockable at plants 6 / animals 5");
   Skill(d,SkillDefOf.Plants,0);Skill(d,SkillDefOf.Animals,6);
   Check(Druid.CanPawnUnlock(d),"druid unlockable at plants 0 / animals 6");
   // 终极：两支 4 环都学会且种植 18。
   Classes.Psycasts(d).UnlockPath(Druid);var comp=d.GetComp<CompAbilities>();
   Skill(d,SkillDefOf.Plants,18);Skill(d,SkillDefOf.Animals,15);
   var weather=Def("LP_ControlWeather").Psycast();
   comp.GiveAbility(Def("LP_BeastCommand"));
   Check(!weather.PrereqsCompleted(comp),"control weather blocked with only beast command learned");
   comp.GiveAbility(Def("LP_CallLightning"));
   Check(weather.PrereqsCompleted(comp),"control weather learnable with both level-4 abilities and plants 18");
   Skill(d,SkillDefOf.Plants,17);
   Check(!weather.PrereqsCompleted(comp),"control weather blocked at plants 17");
   // 巫妖不能当圣武士、德鲁伊，术士可以。
   var lich=Make(10,10,10);Skill(lich,SkillDefOf.Plants,10);
   lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);
   Check(Items.IsLich(lich) && !Paladin.CanPawnUnlock(lich) && !Druid.CanPawnUnlock(lich) && Sorcerer.CanPawnUnlock(lich) && Monk.CanPawnUnlock(lich),"a lich cannot take the paladin or druid path but can take sorcerer and monk");
   // 术士血脉。
   var human=Make(0,0,4);var dragon=Make(0,0,4);dragon.genes.SetXenotype(DefDatabase<XenotypeDef>.GetNamed("LP_ForceDragon"));Skill(dragon,SkillDefOf.Social,4);
   Check(ClassRules.Bloodline(dragon) && !ClassRules.Bloodline(human),"force dragonborn has the sorcerer bloodline");
   Check(Sorcerer.CanPawnUnlock(dragon) && !Sorcerer.CanPawnUnlock(human),"bloodline sorcerer unlocks at social 4, a human does not");
   var innate=Classes.Stat("LP_InnatePower");
   // 力能龙裔有社交天赋差（−4）、智识天赋强（+4），所以按它自己的有效社交等级算基准。
   float dragonBase=Classes.Power(dragon,SkillDefOf.Social);
   Check(Near(dragon.GetStatValue(innate),dragonBase+ClassRules.BloodlineBonus) && Near(human.GetStatValue(innate),Classes.Power(4)),"bloodline adds 0.15 innate power ("+dragon.GetStatValue(innate)+" = "+dragonBase+" + 0.15; human "+human.GetStatValue(innate)+")");
  }
  // ★8 ★9 心灵敏感度：德鲁伊、圣武士神术翻倍；武僧拳脚、战士不变；法师、牧师与 1.30.1 一致。
  static void Classes2Psychic(){
   var p=Make(20,20,20);Skill(p,SkillDefOf.Plants,20);Skill(p,SkillDefOf.Animals,20);
   var bolt=Learn<Ability_CallLightning>(p,"LP_CallLightning");var flurry=Learn<Ability_FlurryOfBlows>(p,"LP_FlurryOfBlows");var smite=Learn<Ability_DivineSmite>(p,"LP_DivineSmite");
   var fb=Learn<ClassAbility>(p,"LP_Fireball");var cure=Learn<ClassAbility>(p,"LP_CureWounds");var sw=Learn<ClassAbility>(p,"LP_SecondWind");
   float b1=bolt.Power,f1=flurry.Strength,s1=smite.Power;
   var h=SetSensitivity(p,2f);
   Check(Near(bolt.Power,b1*2f) && Near(flurry.Strength,f1) && Near(smite.Power,s1*2f),"sensitivity 2: call lightning and holy smite double, flurry unchanged ("+b1+"->"+bolt.Power+", "+f1+"->"+flurry.Strength+", "+s1+"->"+smite.Power+")");
   Check(Near(fb.Psychic,Classes.PsychicScale(2f)) && Near(cure.Psychic,Classes.PsychicScale(2f)) && Near(sw.Psychic,1f),"wizard and cleric keep the psychic factor, fighter stays 1");
   if(h!=null)p.health.RemoveHediff(h);
  }
  // ★11 荒野变形：远程武器进背包，近战伤害系数 = 野性强度，结束后装回。
  static void Classes2WildShape(){
   var p=Make(0,0,0);Skill(p,SkillDefOf.Animals,20);
   var gun=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Autopistol"));p.equipment.AddEquipment(gun);
   var ws=Learn<Ability_WildShape>(p,"LP_WildShape");ws.Apply(T(p));
   var h=p.health.hediffSet.hediffs.OfType<Hediff_WildShape>().FirstOrDefault();
   Check(h!=null && p.equipment.Primary==null && p.inventory.innerContainer.Contains(gun) && h.stowed==gun,"wild shape stows the pistol in the inventory");
   Check(Near(h.Factor("VEF_MeleeAttackDamageFactor"),ws.Strength) && Near(ws.Strength,1.5f),"bear form melee factor equals wild strength 1.5 at animals 20");
   p.health.RemoveHediff(h);
   Check(p.equipment.Primary==gun,"ending wild shape re-equips the pistol");
  }
  // ★14 至圣斩：对亡灵的神圣伤害 ×2，命中后状态移除。
  static void Classes2Smite(){
   var p=Make(20,0,0);var foe=MakeEnemy(center+new IntVec3(3,0,0));var lich=MakeEnemy(center+new IntVec3(-3,0,0));
   lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);
   Items.Give(p,"LP_SmiteReady",600,10f);float a=Paladin_Hit(p,foe);
   Check(Near(a,10f) && !Items.Has(p,"LP_SmiteReady"),"smite adds 10 radiant to a living foe and is spent");
   Items.Give(p,"LP_SmiteReady",600,10f);float b=Paladin_Hit(p,lich);
   Check(Near(b,20f),"smite deals double radiant to undead ("+b+")");
  }
  static float Paladin_Hit(Pawn p,Pawn t)=>LichPhylactery.VPEBridge.Paladin.OnMeleeHit(p,t);
  // 每个新能力施放一次不报错（只看异常，不看数值）。
  static void Classes2Smoke(){
   var friendOnly=new HashSet<string>{"LP_Barkskin","LP_LayOnHands","LP_Intercept"};
   var enemyOnly=new HashSet<string>{"LP_VowOfEnmity","LP_ChaosBolt","LP_CharmPerson","LP_FlurryOfBlows","LP_StunningStrike","LP_OpenHand","LP_QuiveringPalm"};
   var atLoc=new HashSet<string>{"LP_Entangle","LP_PlantGrowth","LP_CallLightning","LP_BurningHands","LP_IceStorm","LP_MassSuggestion","LP_DragonWings"};
   var skip=new HashSet<string>{"LP_ControlWeather"}; // 只弹选择菜单
   var errors=new List<string>();int n=0;
   foreach(var path in new[]{Druid,Paladin,Sorcerer,Monk}){
    foreach(var ab in path.abilities.OrderBy(a=>a.defName=="LP_DragonWings"?1:0)){
     if(skip.Contains(ab.defName))continue;
     var caster=Make(20,20,20,true,null,center);Skill(caster,SkillDefOf.Plants,20);Skill(caster,SkillDefOf.Animals,20);
     var foe=MakeEnemy(center+new IntVec3(1,0,0));var ally=Make(0,0,0,false,null,center+new IntVec3(0,0,1));Wound(ally,5,2);
     var animal=PawnGenerator.GeneratePawn(PawnKindDef.Named("Muffalo"));GenSpawn.Spawn(animal,center+new IntVec3(-1,0,0),map);made.Add(animal);
     GlobalTargetInfo[] tg;
     if(ab.defName=="LP_AnimalFriendship")tg=T(animal);
     else if(friendOnly.Contains(ab.defName))tg=T(ally);
     else if(enemyOnly.Contains(ab.defName))tg=T(foe);
     else if(atLoc.Contains(ab.defName))tg=T(ab.defName=="LP_DragonWings"?center+new IntVec3(0,0,-4):foe.Position);
     else tg=T(caster);
     try{((ClassAbility)Learn(caster,ab.defName)).Apply(tg);n++;}
     catch(Exception e){errors.Add(ab.defName+": "+e.GetType().Name+" "+e.Message);}
    }
   }
   foreach(var t in map.mapPawns.AllPawnsSpawned.Where(ClassRules.Conjured).ToList())HediffComp_Conjured.Vanish(t);
   foreach(var f in map.listerThings.AllThings.OfType<PawnFlyer>().ToList())f.Destroy();
   Check(errors.Count==0,n+" new class abilities applied without errors"+(errors.Count==0?"":": "+string.Join(" | ",errors)));
   // 颤心掌爆发。
   var monk=Make(20,0,0);var target=MakeEnemy(center+new IntVec3(2,0,2));
   var palm=Ability_QuiveringPalm.Plant(monk,target,40f);
   Check(palm!=null && Near(palm.Burst(),40f) && !target.health.hediffSet.hediffs.OfType<Hediff_QuiveringPalm>().Any(),"quivering palm bursts for 40 and clears itself");
  }
  // ★22 存读档：熊形（含收起的武器）、誓仇标记、魔涌、空灵之身。
  static void Classes2PrepareSave(){
   var p=Make(10,10,10);Skill(p,SkillDefOf.Animals,10);savedShapeId=p.ThingID;made.Remove(p);
   p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Autopistol")));
   Ability_WildShape.Shift(p,30000,1.2f);
   Items.Give(p,"LP_SorcerousSurge",30000,1f);Items.Give(p,"LP_EmptyBody",30000,3f);
   var marker=map.mapPawns.AllPawnsSpawned.First(x=>x.ThingID==savedPawnId);
   Ability_VowOfEnmity.Mark(marker,p,30000,1.4f);
  }
  static void Classes2AfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==savedShapeId);
   Check(p!=null,"1.36.0 class pawn survives save/load");
   var ws=p.health.hediffSet.hediffs.OfType<Hediff_WildShape>().FirstOrDefault();
   Check(ws!=null && ws.stowed!=null && p.inventory.innerContainer.Contains(ws.stowed) && Near(ws.Factor("VEF_MeleeAttackDamageFactor"),1.2f),"bear form with its stowed pistol survives save/load");
   var mark=p.health.hediffSet.hediffs.OfType<Hediff_Enmity>().FirstOrDefault();
   Check(mark!=null && mark.caster!=null && mark.caster.ThingID==savedPawnId,"vow of enmity mark keeps its paladin across save/load");
   Check(Items.Has(p,"LP_SorcerousSurge") && Items.Has(p,"LP_EmptyBody") && Near(p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_EmptyBody")).Severity,3f),"sorcerous surge and empty body survive save/load");
   ws.stowed?.Destroy();p.Destroy();
  }
 }
}
