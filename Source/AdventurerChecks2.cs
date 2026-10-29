using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.48.1：Test.ps1 -Only Adventurer 的第二阶段部分（敌人等级、专长）。由 CheckAdventurer 在同一个 try 里调用（冒险者生成已临时打开）。
 public partial class LichChecks {
  static string advSavedFeats;static int advSavedLuckyLeft;
  Pawn AdvKind(PawnKindDef kind,Faction f){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,f,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true));
   advMade.Add(p);return p;
  }
  void CheckAdventurerStage2(){
   var s=LichMod.Settings;bool oldEnemy=s.adventurerEnemyLevels,oldCap=s.adventurerEnemyCap;int oldOff=s.adventurerEnemyCapOffset;
   s.adventurerEnemyLevels=true;s.adventurerEnemyCap=true;s.adventurerEnemyCapOffset=3;
   try{
    // 定义
    var feats=Feats.All.Where(f=>!Feats2.Is(f)).ToList(); // 1.58.1 起另有第二批（Feats2 组检查）
    Check(feats.Count==12 && feats.All(f=>f.Icon!=null && f.Icon!=BaseContent.BadTex) && Feats.InspireAbility!=null,"12 feat defs with 128px icons and the inspire ability");
    var wizKind=DefDatabase<PawnKindDef>.GetNamed("LP_FH_RedWizard");var wiz=wizKind.GetModExtension<LP_AdventurerKindExt>();
    Check(wiz!=null && wiz.levelBonus==5 && wiz.Primary==Adventurer.Int && DefDatabase<PawnKindDef>.GetNamed("LP_EC_FireGiant").GetModExtension<LP_AdventurerKindExt>()?.levelBonus==4 && DefDatabase<PawnKindDef>.GetNamed("LP_FH_GithMercenary").GetModExtension<LP_AdventurerKindExt>()?.levelBonus==3,"elite kind extensions: red wizard +5 (Int), fire giant +4, githyanki +3");
    // 1. 点数换算：1000 点 / 10 人 → 3 级 ±2
    int lo=99,hi=0;for(int i=0;i<300;i++){int l=AdventurerEnemies.LevelFor(1000f/10f,0,20,Rand.RangeInclusive(-2,2));lo=Math.Min(lo,l);hi=Math.Max(hi,l);}
    Check(AdventurerEnemies.BaseLevel(100f)==3 && lo==1 && hi==5,"enemy level: 1000 points / 10 humanlikes → level 3 ± 2 (seen "+lo+"～"+hi+")");
    // 2. 领队/精英加成与上限
    var enemy=AdvEnemy;var hostile=Factions.Hostile??enemy;
    var red=AdvKind(wizKind,hostile);
    Check(AdventurerEnemies.Bonus(red)==5 && AdventurerEnemies.LevelFor(100f,5,20,0)==8 && AdventurerEnemies.LevelFor(100f,5,4,0)==4 && AdventurerEnemies.LevelFor(5000f,0,99,2)==20,"elite bonus (red wizard +5, larger of leader +3 / kind bonus) and the cap");
    Check(AdventurerEnemies.Cap()==Math.Min(20,AdventurerEnemies.ColonyMaxLevel()+3),"enemy cap = colony max level ("+AdventurerEnemies.ColonyMaxLevel()+") + 3");
    // 3. 同点数：等级高的袭击人数更少（战斗力 × (1 + 0.03 × (等级 − 1)) 折算）
    var gunner=DefDatabase<PawnKindDef>.GetNamedSilentFail("Mercenary_Gunner")??PawnKindDefOf.Drifter;
    var raid=new List<Pawn>();for(int i=0;i<20;i++)raid.Add(AdvKind(gunner,enemy));
    var low=raid.ToList();int cutLow=AdventurerEnemies.Trim(low,4000f,1);
    var high=raid.ToList();int cutHigh=AdventurerEnemies.Trim(high,4000f,20);
    Check(cutLow==0 && cutHigh>0 && high.Count<low.Count,"same 4000 points: level-1 raid keeps "+low.Count+", level "+AdventurerEnemies.LevelFor(4000f/20f,0,20,0)+" raid trimmed to "+high.Count);
    var gen=PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,tile=map.Tile,faction=enemy,points=2000f},false).ToList();advMade.AddRange(gen);
    var genPeople=gen.Where(p=>p.RaceProps.Humanlike && Adventurer.Get(p)!=null).ToList();
    Check(genPeople.Count>0 && genPeople.All(p=>Adventurer.Get(p).level>=1 && Adventurer.Get(p).level<=AdventurerEnemies.Cap()),"a generated 2000-point raid: every humanlike raider has a level within the cap ("+string.Join(",",genPeople.Select(p=>Adventurer.Get(p).level))+")");
    var neutral=Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f=>!f.IsPlayer && !f.Hidden && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer));
    if(neutral!=null){var vis=AdvKind(neutral.def.basicMemberKind??PawnKindDefOf.Villager,neutral);var hv=Adventurer.Get(vis);Check(hv!=null && hv.level>=1 && hv.level<=3,"visitors of a non-hostile faction get level 1～3 ("+hv?.level+")");}
    // 剧情等级：不受上限、不被袭击公式覆盖
    var boss=AdvKind(wizKind,hostile);var hb=AdventurerEnemies.Story(boss,15);AdventurerEnemies.LevelGroup(new List<Pawn>{boss}.Where(p=>!Adventurer.Get(p).storyLevel).ToList(),100f,4);
    Check(hb!=null && hb.storyLevel && hb.level==15,"story bosses: SetLevel 15 ignores the cap and is flagged");
    // 4. 敌人主属性最高
    var hr=Adventurer.Get(red);var bas=Adventurer.BaseArray(red);hr.RaiseTo(16);
    Check(bas[Adventurer.Int]==15 && bas[Adventurer.Con]==14 && hr.scores[Adventurer.Int]==hr.scores.Max() && hr.pendingASI==0 && hr.level==16,"red wizard template: Int 15 / Con 14 in the base array, Int is highest after auto ASI to 16 ("+Join(hr.scores)+", feats "+string.Join(",",hr.feats.Select(f=>f.label))+")");
    // 5. 专长前置与改选消耗
    var c=AdvPawn();var hc=Adventurer.Get(c);hc.level=8;hc.pendingASI=2;c.skills.GetSkill(SkillDefOf.Melee).Level=3;hc.scores[Adventurer.Str]=12;
    Check(hc.CanTakeFeat(Feats.GreatWeapon)!=null && hc.CanTakeFeat(Feats.HeavyArmor)!=null && !hc.TakeFeat(Feats.GreatWeapon) && hc.pendingASI==2,"feat prerequisites: great weapon master needs melee 8, heavy armor master needs Str 13");
    c.skills.GetSkill(SkillDefOf.Melee).Level=10;
    Check(hc.TakeFeat(Feats.GreatWeapon) && hc.pendingASI==1 && hc.HasFeat(Feats.GreatWeapon) && !hc.TakeFeat(Feats.GreatWeapon) && hc.pendingASI==1,"taking a feat uses one ability score improvement and cannot be taken twice");
    // 6. 幸运 3 次与午夜重置
    hc.AddFeat(Feats.Lucky);
    var foe=AdvPawn(null,enemy);int hits=0;Rand.PushState(4242);try{for(int i=0;i<30;i++)if(Feats.LuckyRoll(foe,c,.5f))hits++;}finally{Rand.PopState();}
    Check(hc.UsesLeft(Feats.Lucky)==0 && !hc.TryUse(Feats.Lucky),"lucky: proficiency ("+hc.Proficiency+") rerolls per day, then no more ("+hits+"/30 hits at 50%)");
    hc.featDay--;
    Check(hc.UsesLeft(Feats.Lucky)==hc.Proficiency && hc.Proficiency==3,"lucky uses reset at local midnight; level 8 = 3 uses (1.67.0: uses = proficiency)");
    // 7. 巨武器大师追加攻击
    var heavy=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.IsMeleeWeapon && !d.MadeFromStuff && d.equipmentType==EquipmentType.Primary && d.GetStatValueAbstract(StatDefOf.Mass)>=Feats.TwoHandedMass && d.tradeability!=Tradeability.None && !d.destroyOnDrop).OrderBy(d=>d.GetStatValueAbstract(StatDefOf.Mass)).FirstOrDefault();
    Check(heavy!=null,"a two-handed melee weapon exists for the great weapon master check ("+heavy?.defName+")");
    GenSpawn.Spawn(c,altar.InteractionCell+IntVec3.North*6,map);c.equipment.DestroyAllEquipment();
    Check(!Feats.TwoHanded(c) && !Feats.GreatWeaponTrigger(c),"great weapon master needs a two-handed melee weapon");
    c.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(heavy));
    var v2=AdvPawn(null,enemy);GenSpawn.Spawn(v2,c.Position+IntVec3.East,map);
    bool t1=Feats.GreatWeaponTrigger(c),t2=Feats.GreatWeaponTrigger(c);var next=Feats.GreatWeaponFollowUp(c);
    Check(t1 && !t2 && next==v2,"great weapon master: a takedown triggers an immediate attack on the next adjacent enemy, cooldown 2 s");
    // 8. 重甲大师减伤
    var vest=DefDatabase<ThingDef>.GetNamedSilentFail("Apparel_FlakVest");
    hc.scores[Adventurer.Str]=14;hc.pendingASI=1;Check(hc.TakeFeat(Feats.HeavyArmor),"heavy armor master can be taken at Str 14");
    var d0=new DamageInfo(DamageDefOf.Cut,10f);c.PreApplyDamage(ref d0,out bool ab0);
    if(vest!=null)c.apparel.Wear((Apparel)ThingMaker.MakeThing(vest),false);
    var d1=new DamageInfo(DamageDefOf.Cut,10f);c.PreApplyDamage(ref d1,out bool ab1);var d2=new DamageInfo(DamageDefOf.Blunt,10f);c.PreApplyDamage(ref d2,out bool ab2);var d3=new DamageInfo(DamageDefOf.Burn,10f);c.PreApplyDamage(ref d3,out bool ab3);
    float armRed=Feats.HeavyArmorReduction(c);
    Check(vest!=null && Feats.WearsHeavyArmor(c) && Near(armRed,3f+hc.Proficiency) && Near(d0.Amount,10f) && Near(d1.Amount,10f-armRed) && Near(d2.Amount,10f-armRed) && Near(d3.Amount,10f),"heavy armor master: in a flak vest sharp/blunt −(3 + proficiency) = −"+armRed+" (10 → "+d1.Amount+"/"+d2.Amount+"), burn unchanged, no armor no reduction");
    // 其他专长
    var shooter=AdvPawn();var hs=Adventurer.Get(shooter);var rifle=DefDatabase<ThingDef>.GetNamedSilentFail("Gun_AssaultRifle");
    GenSpawn.Spawn(shooter,altar.InteractionCell+IntVec3.West*6,map);shooter.equipment.DestroyAllEquipment();shooter.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(rifle));
    var verb=shooter.equipment.PrimaryEq.PrimaryVerb;float range0=verb.EffectiveRange;hs.AddFeat(Feats.Sharpshooter);float range1=verb.EffectiveRange;
    Check(rifle!=null && range0>0f && Near(range1,range0*1.4f),"sharpshooter: weapon range ×1.4 ("+range0+" → "+range1+")");
    hs.AddFeat(Feats.Alert);shooter.drafter.Drafted=true;int warm=Feats.AlertWarmup(verb,100);shooter.drafter.Drafted=false;
    Check(warm==50,"alert: shooting warmup halved within 10 s of being drafted (100 → "+warm+")");
    var tough=AdvPawn();var ht=Adventurer.Get(tough);ht.AddFeat(Feats.Tough);var bleed=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,tough,tough.RaceProps.body.AllParts.First(x=>x.def.defName=="Torso"));bleed.Severity=6f;tough.health.AddHediff(bleed);
    bool first=Feats.ToughDowned(tough);bool stopped=!bleed.Bleeding || bleed.Severity<=0f || !tough.health.hediffSet.hediffs.Contains(bleed);bool second=Feats.ToughDowned(tough),third=Feats.ToughDowned(tough),fourth=Feats.ToughDowned(tough);
    Check(first && second && third && !fourth && stopped && tough.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_FeatToughResolve"))!=null,"tough: the first 3 downings of the day stop the bleeding, heal 20 and halve pain for 1 hour, the 4th does nothing");
    var healer=AdvPawn();var hh=Adventurer.Get(healer);healer.skills.GetSkill(SkillDefOf.Medicine).Level=2;
    float q0=TendUtility.CalculateBaseTendQuality(healer,c,.5f,1f);hh.AddFeat(Feats.Healer);hh.featScore.Clear();hh.Dirty();float q1=TendUtility.CalculateBaseTendQuality(healer,c,.5f,1f);
    var patient=AdvPawn();var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,patient,patient.RaceProps.body.AllParts.First(x=>x.def.defName=="Torso"));cut.Severity=3f;patient.health.AddHediff(cut);
    Feats.HealerExtra(healer,patient);
    Check(q0<.85f && Near(q1,Mathf.Min(1f,q0*1.3f)) && (cut.Severity<=.01f || !patient.health.hediffSet.hediffs.Contains(cut)),"healer: tend quality ×1.3 ("+q0+" → "+q1+"), each tend heals the 3 smallest wounds by 6 (3 → "+cut.Severity+")");
    var lead=AdvPawn();var hl=Adventurer.Get(lead);hl.scores[Adventurer.Cha]=16;hl.AddFeat(Feats.Leader);GenSpawn.Spawn(lead,altar.InteractionCell+IntVec3.South*6,map);var ally=AdvPawn();GenSpawn.Spawn(ally,lead.Position+IntVec3.West*2,map);
    int n=Feats.Inspire(lead);int again=Feats.Inspire(lead);var ins=ally.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_FeatInspired"));
    var hit=new DamageInfo(DamageDefOf.Cut,5f);ally.PreApplyDamage(ref hit,out bool absorbed);
    int thirdInspire=Feats.Inspire(lead);float amt=Feats.InspireAmount(lead);
    Check(lead.abilities.GetAbility(Feats.InspireAbility)!=null && n>=2 && again>=2 && thirdInspire==0 && Near(amt,20f+3f*hl.level+5f*hl.Mod(Adventurer.Cha)) && ins!=null && Near(ins.Severity,amt-5f) && absorbed,"inspiring leader: ability granted, allies within 10 cells get a 20 + 3×level + 5×Cha mod = "+amt+" shield (5 absorbed → "+ins?.Severity+"), twice per day");
    // 存读档专长次数在 PrepareAdventurerSave 里准备
   }finally{s.adventurerEnemyLevels=oldEnemy;s.adventurerEnemyCap=oldCap;s.adventurerEnemyCapOffset=oldOff;}
   // 10. 共存模式敌人等级默认关
   var fresh=new Settings();
   Check(fresh.adventurerEnemyLevels && AdventurerEnemies.OnFor(fresh,false) && AdventurerEnemies.OnFor(fresh,true),"enemy levels default on, one toggle for both normal and Isekai coexistence (1.63.0)");
  }
 }
}
