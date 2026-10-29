using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.50.0：Test.ps1 -Only Adventurer 的传奇等级部分（20 级门槛与结算、经验表、传奇属性点与效果、重铸之石、传奇敌人、共存默认值）。由 CheckAdventurer 在同一个 try 里调用。
 public partial class LichChecks {
  static string advSavedLegend;
  static string LegendSummary(Hediff_Adventurer h)=>h.level+"|"+h.legendary+"|"+h.legendPoints+"|"+string.Join(",",h.legendSpent);
  void CheckAdventurerStage4(){
   var s=LichMod.Settings;bool oL=s.adventurerLegend,oA=s.adventurerLegendAuto,oE=s.adventurerEnemyLegend,oLv=s.adventurerEnemyLevels,oCap=s.adventurerEnemyCap;int oK=s.adventurerKillMilestones;bool oAff=s.adventurerAffinity;
   s.adventurerAffinity=false; // 1.51.0：这一组按 ×1 的数值写（人类传奇 ×1.1 在 AdventurerChecks5 里查）
   s.adventurerLegend=true;s.adventurerLegendAuto=false;s.adventurerEnemyLegend=true;s.adventurerEnemyLevels=true;s.adventurerEnemyCap=false;s.adventurerKillMilestones=1;
   var enemy=AdvEnemy;Pawn oldLeader=enemy?.leader;
   try{
    Check(DefDatabase<ThingDef>.GetNamedSilentFail(Legend.StoneDef)?.GetCompProperties<CompProperties_UseEffect>()?.compClass==typeof(CompUseEffect_LegendRespec) && !DefDatabase<RecipeDef>.AllDefsListForReading.Any(r=>r.products!=null && r.products.Any(x=>x.thingDef?.defName==Legend.StoneDef)) && DefDatabase<TraderKindDef>.GetNamedSilentFail("LP_Caravan_BlackNetwork")?.stockGenerators.Any(g=>g.HandlesThingDef(DefDatabase<ThingDef>.GetNamed(Legend.StoneDef)))==true,"legend respec stone: usable, not craftable, sold by the black network");
    // 1. 20 级非传奇不升；成为传奇后结算（之前的 500 杀补 3 点）
    var a=AdvPawn();var h=Adventurer.Get(a);
    Adventurer.GainXP(a,Legend.XpFor(23)+50f-h.xp);
    bool held=h.level==20 && h.CappedAt20 && !h.legendary && h.legendPoints==0 && h.xp>=Legend.XpFor(23) && Epic.NextLevelXp(h)>0f;
    h.milestonesDone.Add("kills500");Adventurer.Milestone(a,"portal",1f);
    Check(held && h.legendary && h.level==23 && h.legendPoints==3*Legend.PointsPerLevel+Legend.KillBonus,"level 20 without the legend title keeps XP but does not level; becoming a legend settles it at once → 23 with 6 + 3 legend points (held "+held+", level "+h.level+", points "+h.legendPoints+")");
    // 2. 经验表
    bool ratio=Legend.Step(21)>=Legend.Step(20);for(int l=22;l<=Legend.MaxLevel;l++)if(Legend.Step(l)<Legend.Step(l-1))ratio=false;
    float mult=Legend.XpFor(40)/Legend.XpFor(20);
    Check(ratio && mult>=6f && mult<=8f && Legend.Step(21)>0f && Legend.XpFor(20)==Adventurer.XpTable[19],"legend XP table: step 21 ≥ step 20 and rising, level 40 total = "+mult.ToString("0.00")+"× level 20 (21: "+Legend.Step(21)+", 40: "+Legend.Step(40)+")");
    // 3. 每级 2 点，传奇 500 杀 +3
    var b=AdvPawn();var hb=Adventurer.Get(b);hb.level=20;hb.xp=Legend.XpFor(20);hb.legendary=true;
    Adventurer.GainXP(b,Legend.Step(21));int p21=hb.legendPoints;
    b.records.AddTo(RecordDefOf.KillsHumanlikes,500);int got=Epic.CheckKills(b);
    Check(hb.level==21 && p21==2 && got==500 && hb.legendPoints==5,"each legend level gives 2 points; a legend's 500-kill milestone gives +3 (level "+hb.level+", points "+p21+" → "+hb.legendPoints+")");
    // 4. 效果：近战 10 点 = +30%（共存 / 属性效果 0% 也生效）、血量 +2%/点、远程按 25 格折算、伤害承受线性相加、最低 5%
    var c=AdvPawn();var hc=Adventurer.Get(c);hc.scores=new[]{10,10,10,10,10,10};hc.legendary=true;hc.legendPoints=60;hc.Dirty();
    float part0=Adventurer.PartHealthFactor(c);
    hc.SpendLegend(Legend.Melee,10);hc.SpendLegend(Legend.Health,10);hc.SpendLegend(Legend.Tough,40);
    float melee=hc.BuildStage(1f).statFactors.GetStatFactorFromList(StatDefOf.MeleeDamageFactor),melee0=hc.BuildStage(0f).statFactors.GetStatFactorFromList(StatDefOf.MeleeDamageFactor);
    Check(Near(melee,1.3f) && Near(melee0,1.3f) && Near(Adventurer.PartHealthFactor(c)/part0,1.2f) && Near(Mathf.Pow(Legend.RangedFactor(10),Legend.RangedRefCells),1.2f),"legend points: melee 10 = +30% (also with effects handed to Isekai), health 10 = part health ×1.2, ranged 10 = ×1.2 hit chance at 25 cells");
    float t40=Legend.DamageTaken(40),t20=Legend.DamageTaken(20);
    var hit=new DamageInfo(DamageDefOf.Cut,10f);c.PreApplyDamage(ref hit,out _);
    Check(Near(1f-t40,.6f) && Near(t20,.7f) && Near(Legend.DamageTaken(60),.1f) && Near(Legend.DamageTaken(64),.05f) && Near(Legend.DamageTaken(100),.05f) && Near(Legend.DamageTaken(100000),.05f) && Near(hit.Amount,10f*t40),"toughness: 40 points = "+((1f-t40)*100f).ToString("0.#")+"% less damage (linear −1.5%/point), 100 points still take 5% (floor); a real hit 10 → "+hit.Amount);
    // 5. 重铸之石
    var torso=c.RaceProps.body.AllParts.First(x=>x.def.defName=="Torso");var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,c,torso);cut.Severity=12f;c.health.AddHediff(cut);
    var stone=Legend.MakeStone();var use=stone.TryGetComp<CompUseEffect_LegendRespec>();var plain=AdvPawn();
    bool can=use.CanBeUsedBy(c).Accepted,cannot=!use.CanBeUsedBy(plain).Accepted;
    use.DoEffect(c);stone.Destroy();
    Check(can && cannot && hc.legendSpent.Sum()==0 && hc.legendPoints==60 && Near(cut.Severity,10f) && Near(hc.BuildStage(1f).statFactors?.GetStatFactorFromList(StatDefOf.MeleeDamageFactor)??1f,1f),"respec stone: only legends can use it; all 60 points come back, wounds shrink with the lost health (12 → "+cut.Severity+")");
    // 6. 传奇敌人：领队超出 20 级门槛每 60 点 +1 级、自动投点（远程兵 远程/血量 + 领队 承受）；剧情首领；宿敌冠军
    var gunner=DefDatabase<PawnKindDef>.GetNamedSilentFail("Mercenary_Gunner")??PawnKindDefOf.Drifter;
    var lead=AdvKind(gunner,enemy);var other=AdvKind(gunner,enemy);enemy.leader=lead;
    if(lead.equipment?.Primary==null || !lead.equipment.Primary.def.IsRangedWeapon){var gun=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Autopistol"));lead.equipment.DestroyAllEquipment();lead.equipment.AddEquipment((ThingWithComps)gun);}
    AdventurerEnemies.LevelGroup(new List<Pawn>{lead},900f,20);var hl=Adventurer.Get(lead);
    Check(Legend.PointsFor20(3)==720f && AdventurerEnemies.Target(other,900f,20,0)==20 && hl.level==23 && hl.legendary && hl.legendPoints==0 && hl.legendSpent[Legend.Ranged]==2 && hl.legendSpent[Legend.Health]==2 && hl.legendSpent[Legend.Tough]==2,"legend enemies: a leader at 900 points/pawn → 20 + (900 − 720)/60 = "+hl.level+", auto-spent "+string.Join(",",hl.legendSpent)+" (ranged/health + leader toughness); a non-leader stays 20");
    var caster=AdvKind(gunner,enemy);var hcst=Adventurer.Get(caster);caster.equipment.DestroyAllEquipment();var role=Feats.EnemyRoles(caster);
    Check(Legend.StoryLevel(15,900f)==23 && Legend.StoryLevel(15,300f)==15 && Legend.Prefs(caster).Contains(role.Contains("caster")?Legend.Psychic:Legend.Melee) && !Legend.Prefs(caster).Contains(Legend.Tough),"story bosses: 20% of threat points past the level-20 line → legend levels (900 → 23, 300 keeps 15); unarmed soldiers spend melee/health");
    var top=AdvPawn();GenSpawn.Spawn(top,altar.InteractionCell+IntVec3.South*11,map);var ht=Adventurer.Get(top);ht.level=25;ht.legendary=true;
    var champ=AdvKind(gunner,enemy);var hch=Epic.MakeChampion(champ,Epic.NemesisLevel());
    Check(AdventurerEnemies.ColonyMaxLevel()==25 && hch!=null && hch.level==27 && hch.legendary && hch.legendSpent.Sum()==14,"nemesis champion can be a legend: colony max 25 + 2 = "+hch?.level+", 14 points spent");
    s.adventurerEnemyLegend=false;var champ2=AdvKind(gunner,enemy);var hch2=Epic.MakeChampion(champ2,Epic.NemesisLevel());s.adventurerEnemyLegend=true;
    Check(hch2!=null && hch2.level==20 && !hch2.legendary,"with enemy legend levels off the champion stops at 20");
    // 7. 共存默认值
    var fresh=new Settings();var noLv=new Settings{adventurerEnemyLevels=false};
    Check(fresh.adventurerLegend && !fresh.adventurerLegendAuto && fresh.adventurerEnemyLegend && Legend.EnemyOnFor(fresh,false) && Legend.EnemyOnFor(fresh,true) && !Legend.EnemyOnFor(noLv,false) && !Legend.EnemyOnFor(noLv,true),"defaults: legend levels on, colonist auto-spend off, enemy legends on in both modes (1.63.0: one toggle), off whenever enemy levels are off");
   }finally{
    s.adventurerLegend=oL;s.adventurerLegendAuto=oA;s.adventurerEnemyLegend=oE;s.adventurerEnemyLevels=oLv;s.adventurerEnemyCap=oCap;s.adventurerKillMilestones=oK;s.adventurerAffinity=oAff;
    if(enemy!=null)enemy.leader=oldLeader;Epic.State?.RefreshLegend();
   }
  }
  void PrepareLegendSave(Hediff_Adventurer h){h.legendPoints=3;h.legendSpent[Legend.Tough]=4;h.legendSpent[Legend.Work]=1;advSavedLegend=LegendSummary(h);}
  void CheckLegendAfterLoad(Hediff_Adventurer h){Check(h!=null && LegendSummary(h)==advSavedLegend,"legend points survive save/load ("+advSavedLegend+")");}
 }
}
