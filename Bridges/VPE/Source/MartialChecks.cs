using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.72.0：战士 / 武僧 / 圣武士移到主程序集后，装了 VPE 时的检查。
 // Test.ps1 -VPE -Only Martial：旧路径隐藏、物理职业被动读新职业等级、斗志、导师、冒险者手册。
 // Test.ps1 -VPE -Only Classes：旧存档迁移（假的 VPE 学习记录 → 职业等级）与存读档。没有 VPE 的部分在主程序集 -Only Classes。
 public static partial class BridgeChecks {
  static string savedMigrateId;
  static Hediff_Adventurer Adv(Pawn p,int level){var h=Adventurer.Get(p)??Adventurer.TryAdd(p,level,true);h.level=level;h.xp=Adventurer.XpTable[Mathf.Clamp(level,1,20)-1];h.scores=new[]{10,10,10,10,10,10};h.Dirty();return h;} // 属性固定 10：主属性 13 的门槛不会意外放行

  static void MartialChecks(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=false;
   try{
    // 1. 三条旧路径：不在 DND职业 页签、不能解锁；旧能力是空壳；新能力是原版能力。
    var hidden=new[]{Classes.FighterPath,Classes.PaladinPath,Classes.MonkPath,Classes.WizardPath,Classes.ClericPath,Classes.DruidPath,Classes.SorcererPath,Classes.ArtificerPath};
    var tab=Activator.CreateInstance(AccessTools.TypeByName("VanillaPsycastsExpanded.UI.ITab_Pawn_Psycasts")??AccessTools.TypeByName("VanillaPsycastsExpanded.ITab_Pawn_Psycasts"));
    var byTab=Traverse.Create(tab).Field("pathsByTab").GetValue<Dictionary<string,List<PsycasterPathDef>>>();
    var strong=Make(20,20,20);
    Check(hidden.All(n=>Classes.Path(n)!=null && !Classes.Paths.Contains(Classes.Path(n)) && !Classes.Path(n).CanPawnUnlock(strong)) && byTab!=null && !byTab.ContainsKey("DND职业") && !byTab.Values.SelectMany(l=>l).Any(ClassMigration.Hidden) && !Classes.Paths.Any(),"all 8 DND paths are hidden and cannot be unlocked; the empty DND职业 tab is gone (1.73.0)");
    var legacy=DefDatabase<AbilityDef>.AllDefsListForReading.Where(ClassMigration.Hidden).ToList();
    Check(legacy.Count==79 && legacy.All(d=>d.abilityClass==typeof(Ability_ClassLegacy) && DefDatabase<RimWorld.AbilityDef>.GetNamedSilentFail(d.defName)?.abilityClass==typeof(Ability_Class)),"79 legacy VPE ability shells, each with a vanilla class ability of the same name");
    // 2. 物理职业被动读新的职业等级：战士 11 → 额外攻击第 2 档、两个风格槽、战技 / 射术强度 +0.2（Classes.Power 与主程序集一致）。
    var f=Make(10);var hf=Adv(f,12);ClassSystem.SetLevel(hf,ClassSystem.Fighter,11);
    var x=f.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ExtraAttack"));
    Check(Martial.Is(f) && Martial.Tier(f)==2 && x!=null && Near(x.Severity,2f) && Styles.Slots(f)==2 && Near(Martial.Bonus(f),.2f) && Near(Classes.Power(f,SkillDefOf.Melee),ClassSystem.Power(f,SkillDefOf.Melee)) && Near(f.GetStatValue(Classes.Stat("LP_MartialPower"))-ClassSystem.Power(10)-Adventurer.ClassPowerBonus(f,ClassSystem.SkillAbility(SkillDefOf.Melee,f)),.2f),"fighter 11: extra attack tier 2, two style slots, +0.2 martial power from class levels");
    // 3. 斗志：命中敌人加精神力（有心灵链接）和斗志资源。
    var foe=MakeEnemy(center+IntVec3.East*2);var st=Martial.State;st.furyGiven.Clear();
    hf.classes.res[ClassSystem.Fighter]=0f;f.psychicEntropy.OffsetPsyfocusDirectly(-.5f);float pf=f.psychicEntropy.CurrentPsyfocus;Martial.OnHit(foe,f);
    Check(Near(ClassSystem.Resource(hf,ClassSystem.Fighter),Martial.FuryHit*Martial.FuryResourcePer) && f.psychicEntropy.CurrentPsyfocus>pf,"fury: a hit gives psyfocus and "+(Martial.FuryHit*Martial.FuryResourcePer)+" fury");
    // 4. 冒险者手册：门槛豁免让技能为 0 的人学圣武士；武僧只在徒手时有额外攻击。
    var novice=Make(0,0,0,false);var hn=Adv(novice,5);
    var manual=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_AdventurersManual"));var use=manual.TryGetComp<CompUseEffect_Manual>();
    bool refused=ClassSystem.CanAdd(hn,ClassSystem.Paladin)!=null;bool usable=use.CanBeUsedBy(novice).Accepted;CompUseEffect_Manual.Apply(novice);
    bool took=hn.classes.freeUnlocks==1 && ClassSystem.AddLevel(hn,ClassSystem.Paladin) && hn.classes.freeUnlocks==0;manual.Destroy();
    var m=Make(10);m.equipment.DestroyAllEquipment();var hm=Adv(m,6);ClassSystem.SetLevel(hm,ClassSystem.Monk,5);int unarmed=Martial.Tier(m);
    m.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife"),ThingDefOf.Steel));Martial.Refresh(m);
    Check(refused && usable && took && unarmed==1 && Martial.Tier(m)==0,"adventurer's manual waives the class gate once (paladin at social 0); monk-only extra attack only while unarmed");
    // 5. 导师：圣武士导师的学生看职业等级，上课给冒险者经验（25% 下一级 + 50），不动 VPE 经验。
    var pal=Classes.Path(Classes.PaladinPath);float xp0=hn.xp,vpe0=Classes.Psycasts(novice)?.experience??0f;float share=Mentor.AdventurerShare(novice);
    var mentor=Mentor.MakeMentor(map,pal);made.Add(mentor);
    var ms=Mentor.Students(map,pal).ToList();
    var visit=new MentorVisit{map=map,mentor=mentor,path=pal};
    int taught=Mentor.Teach(visit); // Make 已经放到地图上
    Check(Mentor.ClassFor(pal)==ClassSystem.Paladin && ClassSystem.Level(mentor,ClassSystem.Paladin)>=1 && taught>=1 && Mentor.Students(map,pal).Contains(novice) && hn.xp>xp0 && Near(hn.xp-xp0,(share+Adventurer.LessonXP)*Adventurer.XpScale*Talents.XpFactor(hn),1f) && Near(Classes.Psycasts(novice)?.experience??0f,vpe0),"a paladin mentor teaches by class level and gives adventurer XP only (+"+(hn.xp-xp0).ToString("0")+")");
   }finally{Adventurer.TestSuppressed=sup;}
  }

  // 旧存档迁移：冒险者 5 级，学过战士 7 个、圣武士 3 个旧能力。
  static void ClassesChecks(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=false;
   try{
    var p=Make(12,0,12);var h=Adv(p,5);h.classes=new ClassData();
    var psy=Classes.Psycasts(p);psy.points=0;var comp=p.GetComp<CompAbilities>();
    psy.UnlockPath(Fighter);psy.UnlockPath(Classes.Path(Classes.PaladinPath));
    var fNames=new[]{"LP_SecondWind","LP_BattleCry","LP_Charge","LP_Trip","LP_ActionSurge","LP_WeaponMastery","LP_Whirlwind"};var pNames=new[]{"LP_DivineSmite","LP_LayOnHands","LP_VowOfEnmity"};
    foreach(var n in fNames.Concat(pNames))comp.GiveAbility(Def(n));
    Items.Give(p,"LP_GuardStance",600,1f);
    int moved=ClassMigration.Migrate(p);
    var has=new Func<string,bool>(n=>p.abilities.GetAbility(DefDatabase<RimWorld.AbilityDef>.GetNamed(n))!=null);
    Check(moved==2 && ClassSystem.Level(h,ClassSystem.Fighter)==4 && ClassSystem.Level(h,ClassSystem.Paladin)==1 && ClassSystem.Points(h)==0 && h.classes.migrated,"migration: 7 fighter abilities → fighter 4, 3 paladin abilities → paladin 1 (capped by the 5 adventurer points)");
    Check(fNames.Concat(pNames).All(has) && h.classes.kept.Any(d=>d.defName=="LP_WeaponMastery") && h.classes.kept.Any(d=>d.defName=="LP_Whirlwind") && h.classes.kept.Any(d=>d.defName=="LP_VowOfEnmity"),"every learned ability is kept as a vanilla class ability, even above the new class level");
    Check(!comp.LearnedAbilities.Any(a=>ClassMigration.Hidden(a.def)) && !psy.unlockedPaths.Any(ClassMigration.Hidden) && psy.points==12 && ClassMigration.Migrate(p)==0,"old VPE abilities and paths removed, 12 VPE points refunded, a second pass does nothing");
    made.Remove(p);savedMigrateId=p.ThingID; // Make 已经放到地图上
    // 1.73.0：1.72.0 已经迁过战士的人，再迁法师（第二遍只动剩下的旧路径）。
    var q=Make(0,12,12);var hq=Adv(q,6);hq.classes=new ClassData();ClassSystem.SetLevel(hq,ClassSystem.Fighter,2);hq.classes.migrated=true;
    var cq=q.GetComp<CompAbilities>();foreach(var n in new[]{"LP_MagicMissile","LP_MageArmor","LP_Fireball"})cq.GiveAbility(Def(n));Classes.Psycasts(q).UnlockPath(Classes.Path(Classes.WizardPath));
    var wiz=DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Wizard");int moved2=ClassMigration.Migrate(q);
    Check(moved2==1 && ClassSystem.Level(hq,wiz)==2 && ClassSystem.Level(hq,ClassSystem.Fighter)==2 && q.abilities.GetAbility(DefDatabase<RimWorld.AbilityDef>.GetNamed("LP_Fireball"))!=null && hq.classes.kept.Any(d=>d.defName=="LP_Fireball") && !cq.LearnedAbilities.Any(a=>ClassMigration.Hidden(a.def)) && GameComponent_ClassMigration.Version==3,"second migration pass (1.73.0): a pawn already migrated in 1.72.0 keeps fighter 2 and gets wizard 2 from 3 old wizard spells");
    // 1.74.0 热修：老存档里已经删掉的旧能力类名读成 Ability_ClassLegacy（不再退回抽象 Ability 让整个人读坏）；别的类名不受影响。
    var oldT=BackCompatibility.GetBackCompatibleType(typeof(Ability),"LichPhylactery.VPEBridge.Ability_MagicMissile",null);var oldT2=BackCompatibility.GetBackCompatibleType(typeof(Ability),"LichPhylactery.VPEBridge.Ability_SecondWind",null);
    var keepT=BackCompatibility.GetBackCompatibleType(typeof(Ability),typeof(Ability_ClassLegacy).FullName,null);var otherBase=LegacyAbilityClassNames.Map(typeof(Hediff),"LichPhylactery.VPEBridge.Ability_MagicMissile");
    Check(oldT==typeof(Ability_ClassLegacy) && oldT2==typeof(Ability_ClassLegacy) && keepT==typeof(Ability_ClassLegacy) && otherBase==null && !oldT.IsAbstract,"removed 1.72/1.73 ability class names load as the inert legacy ability ("+oldT+", "+oldT2+")");
   }finally{Adventurer.TestSuppressed=sup;}
  }
  static void ClassesAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==savedMigrateId);var h=Adventurer.Get(p);
   Check(h!=null && ClassSystem.Level(h,ClassSystem.Fighter)==4 && ClassSystem.Level(h,ClassSystem.Paladin)==1 && h.classes.kept.Count==5 && h.classes.migrated && p.abilities.GetAbility(DefDatabase<RimWorld.AbilityDef>.GetNamed("LP_Whirlwind"))!=null && !p.GetComp<CompAbilities>().LearnedAbilities.Any(a=>ClassMigration.Hidden(a.def)),"migrated class levels, kept abilities and the migrated flag survive save/load");
   p.Destroy();
  }
 }
}
