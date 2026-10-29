using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.49.0：Test.ps1 -Only Adventurer 的第三阶段部分（史诗恩赐、传奇、半神上限、击杀里程碑、宿敌冠军、百战觉醒默认值、CR）。由 CheckAdventurer 在同一个 try 里调用。
 public partial class LichChecks {
  static string advSavedEpic;
  static string EpicSummary(Hediff_Adventurer h)=>string.Join(",",h.boons.Select(b=>b.defName))+"|pts "+h.boonPoints+"|19 "+h.boon19+"|might "+h.mightAbility+"|fate "+h.fateTick+"|legend "+h.legendary+"|dg "+h.demigodASI+"|nem "+h.nemesis+"|"+string.Join(",",h.milestonesDone);
  int MagicItemsOnMap()=>map.listerThings.AllThings.Count(t=>t.def.tradeTags!=null && t.def.tradeTags.Contains("LP_MagicItem"));
  void CheckAdventurerStage3(){
   var s=LichMod.Settings;int oldK=s.adventurerKillMilestones,oldN=s.adventurerNemesis;
   s.adventurerKillMilestones=1;s.adventurerNemesis=1;
   try{
    var torsoOf=(Func<Pawn,BodyPartRecord>)(p=>p.RaceProps.body.AllParts.First(x=>x.def.defName=="Torso"));
    Check(Epic.Boons.Count()==6 && Epic.Fate!=null && Epic.Might!=null && Epic.Combat!=null && !Epic.Immortal.enemy && DefDatabase<IncidentDef>.GetNamedSilentFail("LP_NemesisChampion")?.Worker is IncidentWorker_NemesisChampion && Items.Hediff("LP_BoonFateDowned")!=null,"6 epic boon defs (immortal not for enemies), nemesis incident and fate hediff");
    // 1. 19 级恩赐点、选择、旧存档补点、不朽
    var a=AdvPawn();var h=Adventurer.Get(a);
    Adventurer.GainXP(a,Adventurer.XpTable[18]-h.xp);
    Check(h.level==19 && h.boonPoints==1 && h.EpicBoonPending,"level 19 gives one epic boon point (level "+h.level+", points "+h.boonPoints+")");
    Check(h.TakeBoon(Epic.Speed) && h.boonPoints==0 && !h.TakeBoon(Epic.Immortal) && h.HasBoon(Epic.Speed) && !h.HasBoon(Epic.Immortal),"taking a boon uses the point; a second boon needs another point");
    h.boon19=false;h.boons.Clear();h.boonPoints=0;h.Dirty();h.LoadFixup();h.LoadFixup();
    Check(h.boonPoints==1 && h.boon19,"old saves: a level 19 adventurer without the point gets exactly one on load");
    float age0=a.ageTracker.BiologicalTicksPerTick;h.TakeBoon(Epic.Immortal);float age1=a.ageTracker.BiologicalTicksPerTick;var flu=DefDatabase<HediffDef>.GetNamedSilentFail("Flu");
    Check(age0>0f && age1==0f && flu!=null && h.CurStage.makeImmuneTo!=null && h.CurStage.makeImmuneTo.Contains(flu),"immortal boon: biological aging stops ("+age0+" → "+age1+"), immune to flu");
    // 2. 威能之恩：+4、该项上限 24
    var m=AdvPawn();var hm=Adventurer.Get(m);hm.level=19;hm.boonPoints=1;hm.scores=new[]{20,18,12,10,10,10};hm.Dirty();
    Check(!hm.TakeBoon(Epic.Might) && hm.TakeBoon(Epic.Might,Adventurer.Str) && hm.scores[Adventurer.Str]==24 && hm.Cap(Adventurer.Str)==24 && hm.Cap(Adventurer.Dex)==20 && hm.Mod(Adventurer.Str)==7,"might boon: Str 20 → 24, cap 24 for that score only (mod "+hm.Mod(Adventurer.Str)+")");
    hm.pendingASI=1;
    Check(!hm.CanRaise(Adventurer.Str,1) && hm.CanRaise(Adventurer.Dex,2) && !hm.CanRaise(Adventurer.Dex,3),"ability improvements respect the per-score cap");
    // 迅捷、心灵
    var q=AdvPawn();var hq=Adventurer.Get(q);hq.scores=new[]{10,14,10,10,14,10};hq.Dirty();var st0=hq.CurStage;float mv0=q.GetStatValue(StatDefOf.MoveSpeed),o0=st0.statOffsets.GetStatOffsetFromList(StatDefOf.MoveSpeed),p0=st0.statOffsets.GetStatOffsetFromList(StatDefOf.PsychicSensitivity);
    hq.AddBoon(Epic.Speed);hq.AddBoon(Epic.Psyche);var st1=hq.CurStage;
    Check(q.GetStatValue(StatDefOf.MoveSpeed)>mv0+.3f && Near(o0,0f) && Near(st0.statFactors.GetStatFactorFromList(StatDefOf.MoveSpeed),1.75f) && Near(st1.statOffsets.GetStatOffsetFromList(StatDefOf.MoveSpeed),.6f) && Near(st1.statFactors.GetStatFactorFromList(StatDefOf.MeleeCooldownFactor),.85f) && Near(p0,0f) && Near(st0.statFactors.GetStatFactorFromList(StatDefOf.PsychicSensitivity),1.75f) && Near(st1.statOffsets.GetStatOffsetFromList(StatDefOf.PsychicSensitivity),.25f) && Near(st1.statOffsets.GetStatOffsetFromList(StatDefOf.PsychicEntropyMax)-st0.statOffsets.GetStatOffsetFromList(StatDefOf.PsychicEntropyMax),20f),"speed boon +0.6 move speed, melee cooldown ×0.85; psyche boon +25% psychic sensitivity (Dex/Wis 14 now give move and psychic ×1.75 as factors)");
    // 命运之恩
    var f=AdvPawn();GenSpawn.Spawn(f,altar.InteractionCell+IntVec3.North*8,map);var hf=Adventurer.Get(f);hf.AddBoon(Epic.Fate);
    var heart=f.RaceProps.body.AllParts.First(x=>x.def.defName=="Heart");
    f.health.AddHediff(HediffDefOf.MissingBodyPart,heart,new DamageInfo(DamageDefOf.Stab,20f,999f,-1f,null,heart));
    Check(!f.Dead && f.Downed && hf.fateTick==Find.TickManager.TicksGame && f.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_BoonFateDowned"))!=null && !f.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(i=>i.Bleeding),"fate boon: a destroyed heart becomes downed and stops bleeding (dead "+f.Dead+", downed "+f.Downed+", fate tick "+hf.fateTick+", bleeding "+f.health.hediffSet.BleedRateTotal+", hediffs "+string.Join(",",f.health.hediffSet.hediffs.Select(x=>x.def.defName+"@"+x.Part?.def.defName))+")");
    f.health.AddHediff(HediffDefOf.MissingBodyPart,heart,new DamageInfo(DamageDefOf.Stab,20f,999f,-1f,null,heart));
    Check(f.Dead,"fate boon works once per 60 days: the second lethal wound kills");
    // 战斗之恩
    var cb=AdvPawn();Adventurer.Get(cb).AddBoon(Epic.Combat);var torso=torsoOf(cb);var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,cb,torso);cut.Severity=10f;cb.health.AddHediff(cut);
    var enemy=AdvEnemy;var foe=AdvPawn(null,enemy);float heal=torso.def.GetMaxHealth(cb)*Epic.CombatHealFraction;
    Check(Epic.CombatHeal(cb,foe)==1 && Near(cut.Severity,10f-heal),"combat boon: a takedown heals each wound by 5% of the part (10 → "+cut.Severity+")");
    // 敌人 19 级以上带一个恩赐（不朽除外）
    var gunner=DefDatabase<PawnKindDef>.GetNamedSilentFail("Mercenary_Gunner")??PawnKindDefOf.Drifter;bool enemyOk=true;
    for(int i=0;i<8;i++){var e=AdvKind(gunner,enemy);var he=Adventurer.Get(e);he.RaiseTo(19);if(he.boons.Count!=1 || he.HasBoon(Epic.Immortal) || he.boonPoints!=0)enemyOk=false;}
    Check(enemyOk,"enemies at level 19 get one random epic boon, never immortal");
    // 3. 传奇：20 级 + DND 里程碑
    var L=AdvPawn();GenSpawn.Spawn(L,altar.InteractionCell+IntVec3.North*3,map);var hl=Adventurer.Get(L);var friend=AdvPawn();GenSpawn.Spawn(friend,altar.InteractionCell+IntVec3.North*3+IntVec3.East*2,map);
    float soc0=L.GetStatValue(StatDefOf.SocialImpact);
    Adventurer.GainXP(L,Adventurer.XpTable[19]-hl.xp);hl.milestonesDone.Add("kills25");Epic.CheckLegend(hl);bool before=hl.legendary;
    Adventurer.Milestone(L,"portal",1f);Epic.State.RefreshLegend();
    var companion=DefDatabase<ThoughtDef>.GetNamed("LP_LegendCompanion");var renown=DefDatabase<ThoughtDef>.GetNamed("LP_LegendRenown");var outsider=AdvPawn(null,enemy);
    Check(hl.level==20 && !before && hl.legendary && Near(L.GetStatValue(StatDefOf.SocialImpact)/soc0,1.1f) && companion.Worker.CurrentState(friend).Active && renown.Worker.CurrentSocialState(outsider,L).Active && !renown.Worker.CurrentSocialState(friend,L).Active,"legendary adventurer: level 20 + a DND milestone (kill milestones don't count) → title, social impact ×1.1, colony mood +3, outsiders' opinion +10");
    var fallen=DefDatabase<ThoughtDef>.GetNamed("LP_LegendFallen");L.Kill(null);
    Check(L.Dead && friend.needs.mood.thoughts.memories.GetFirstMemoryOfDef(fallen)!=null && fallen.stages[0].baseMoodEffect==-6f && Near(fallen.durationDays,10f),"a legend's death gives the colony −6 mood for 10 days");
    Epic.State.RefreshLegend();
    // 4. 半神：每级送一次属性提升，不再提高上限（2026-10-29）；传奇冒险者每 5 个传奇等级上限 +2，超出上限的值保留但按上限算
    var g=AdvPawn();var hg=Adventurer.Get(g);hg.scores=new[]{20,20,14,10,10,10};hg.pendingASI=0;hg.Dirty();
    var dh=(Hediff_Demigod)HediffMaker.MakeHediff(Demigods.Def,g);dh.Comp.level=3;g.health.AddHediff(dh);
    int gave=hg.SyncDemigod();
    Check(gave==3 && hg.pendingASI==3 && hg.Cap(Adventurer.Str)==20 && hg.Cap(Adventurer.Cha)==20,"demigod level 3: three ability improvements, caps stay 20");
    dh.Comp.level=4;int gave2=hg.SyncDemigod();
    Check(gave2==1 && hg.pendingASI==4 && hg.SyncDemigod()==0,"next demigod level: one more improvement, only once");
    g.health.RemoveHediff(dh);hg.SyncDemigod();
    hg.legendary=true;hg.level=24;int c24=hg.Cap(Adventurer.Str);hg.level=25;int c25=hg.Cap(Adventurer.Str);hg.level=40;int c40=hg.Cap(Adventurer.Cha);
    hg.scores[Adventurer.Str]=28;hg.level=30;hg.Dirty();int eff30=Adventurer.Score(g,Adventurer.Str);hg.legendary=false;hg.level=20;hg.Dirty();
    Check(c24==20 && c25==22 && c40==28 && eff30==24 && Adventurer.Score(g,Adventurer.Str)==20 && hg.scores[Adventurer.Str]==28 && hg.demigodASI==4,"legendary adventurer caps: Lv24 20, Lv25 22, Lv40 28; Str 28 counts as 24 at Lv30 and 20 when no longer legendary (value kept)");
    // 5. 击杀里程碑
    var k=AdvPawn();var hk=Adventurer.Get(k);float x0=hk.xp,need=Epic.NextLevelXp(hk);
    k.records.AddTo(RecordDefOf.KillsHumanlikes,25);int r1=Epic.CheckKills(k);float x1=hk.xp;int r2=Epic.CheckKills(k);
    Check(r1==25 && Near(x1-x0,need*Epic.KillMilestoneShare) && r2==0 && Near(hk.xp,x1) && hk.milestonesDone.Contains("kills25"),"25 humanlike kills: letter + 30% of the next level's XP ("+(x1-x0)+"), only once");
    k.records.AddTo(RecordDefOf.KillsHumanlikes,975);int r3=Epic.CheckKills(k);
    Check(r3==1000 && hk.boonPoints==1 && new[]{50,100,250,500,1000}.All(t=>hk.milestonesDone.Contains("kills"+t)),"1000 kills: one extra boon point; skipped thresholds are marked done");
    var k2=AdvPawn();GenSpawn.Spawn(k2,altar.InteractionCell+IntVec3.South*3,map);k2.records.AddTo(RecordDefOf.KillsHumanlikes,24);var v=AdvPawn(null,enemy);GenSpawn.Spawn(v,k2.Position+IntVec3.East,map);
    v.Kill(new DamageInfo(DamageDefOf.Cut,1f,0f,-1f,k2));
    Check(v.Dead && Adventurer.Get(k2).milestonesDone.Contains("kills25"),"a real kill by a colonist reaching 25 triggers the milestone");
    // 6. 宿敌冠军
    var top=AdvPawn();GenSpawn.Spawn(top,altar.InteractionCell+IntVec3.South*5,map);Adventurer.Get(top).RaiseTo(13);
    int lvl=Epic.NemesisLevel(),max=AdventurerEnemies.ColonyMaxLevel();
    var champ=AdvKind(gunner,enemy);var hc=Epic.MakeChampion(champ,lvl);
    Check(max>=13 && lvl==Math.Min(20,max+2) && Epic.NemesisReady() && hc!=null && hc.level==lvl && hc.storyLevel && hc.nemesis && hc.feats.Count>=1 && hc.boons.Count==1 && !hc.HasBoon(Epic.Immortal),"nemesis champion: level = colony max ("+max+") + 2 = "+lvl+", with "+string.Join("、",hc?.feats.Select(x=>x.label)??new string[0])+" and "+string.Join("、",hc?.boons.Select(x=>x.label)??new string[0]));
    var killer=AdvPawn();var hkill=Adventurer.Get(killer);float kx0=hkill.xp,kneed=Epic.NextLevelXp(hkill);int items0=MagicItemsOnMap();
    var loot=Epic.NemesisReward(killer,map,altar.InteractionCell+IntVec3.South*7);
    Check(loot!=null && loot.def.tradeTags.Contains("LP_MagicItem") && loot.Spawned && Near(hkill.xp-kx0,kneed*Epic.NemesisShare) && MagicItemsOnMap()==items0+1,"nemesis reward: 50% of the next level's XP and a magic item ("+loot?.def.defName+")");
    hc.fateTick=Find.TickManager.TicksGame; // 冠军可能随机到命运之恩：先让它在冷却中，这一击才是致命的
    GenSpawn.Spawn(champ,altar.InteractionCell+IntVec3.South*9,map);int items1=MagicItemsOnMap();champ.Kill(new DamageInfo(DamageDefOf.Cut,1f,0f,-1f,killer));
    Check(champ.Dead && MagicItemsOnMap()==items1+1,"killing the champion drops the magic item where it fell (dead "+champ.Dead+", items "+items1+" → "+MagicItemsOnMap()+", nemesis "+hc.nemesis+")");
    // 7. 百战觉醒默认值
    var fresh=new Settings();
    Check(fresh.adventurerKillMilestones==-1 && fresh.adventurerNemesis==-1 && Epic.On(-1,false) && !Epic.On(-1,true) && Epic.On(1,true) && !Epic.On(0,false) && Epic.IkmActive==ModsConfig.IsActive("local.isekai.killmilestones"),"with IsekaiKillMilestones detected, kill milestones and the nemesis default off (can be switched on)");
    s.adventurerKillMilestones=0;var k3=AdvPawn();k3.records.AddTo(RecordDefOf.KillsHumanlikes,30);
    Check(Epic.CheckKills(k3)==0 && !Adventurer.Get(k3).milestonesDone.Contains("kills25"),"kill milestones switched off give nothing");
    // 8. CR
    var wolfKind=DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(x=>x.defName.StartsWith("LP_") && x.RaceProps!=null && x.RaceProps.Animal && x.combatPower>0f);
    string insp=null;
    if(wolfKind!=null){var w=PawnGenerator.GeneratePawn(new PawnGenerationRequest(wolfKind,null));advMade.Add(w);GenSpawn.Spawn(w,altar.InteractionCell+IntVec3.West*9,map);insp=w.GetInspectString();}
    Check(Epic.CR(25f)=="1" && Epic.CR(10f)=="1/4" && Epic.CR(15f)=="1/2" && Epic.CR(200f)=="8" && Epic.CR(5000f)=="30" && insp!=null && insp.Contains("挑战等级 CR "+Epic.CR(wolfKind.combatPower)),"challenge rating: combatPower / 25, min 1/4; DND creature inspect shows it ("+wolfKind?.defName+": CR "+(wolfKind==null?"?":Epic.CR(wolfKind.combatPower))+")");
   }finally{s.adventurerKillMilestones=oldK;s.adventurerNemesis=oldN;Epic.State?.RefreshLegend();}
  }
  void PrepareEpicSave(Hediff_Adventurer h){
   h.AddBoon(Epic.Fate);h.boonPoints=1;h.legendary=true;h.fateTick=12345;h.demigodASI=2;h.milestonesDone.Add("kills25");
   advSavedEpic=EpicSummary(h);advSavedMilestones=string.Join(",",h.milestonesDone);
  }
  void CheckEpicAfterLoad(Hediff_Adventurer h){
   Check(h!=null && EpicSummary(h)==advSavedEpic,"epic boons, boon points, legendary title and kill milestones survive save/load ("+advSavedEpic+")");
  }
 }
}
