using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.63.0 清理版：Test.ps1 -Only Cleanup（警报、巨魔尸体过滤 / 标记焚烧、五色龙地形、法术抗体、宝箱、刺坑、魅魔识破、眼柄配方）。
 public partial class LichChecks {
  readonly List<Thing> cuMade=new List<Thing>();
  Pawn CuPawn(Faction f=null,int dx=0,int dz=0){
   Pawn p;Rand.PushState(16302+cuMade.Count);try{p=Person();}finally{Rand.PopState();}
   if(f!=null && p.Faction!=f)p.SetFaction(f);p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();
   GenSpawn.Spawn(p,cuBase+new IntVec3(dx,0,dz),map);cuMade.Add(p);return p;
  }
  IntVec3 cuBase;
  static bool CuHas(Alert_LP a,Thing t){a.Refresh();return a.Targets.Any(x=>x.Thing==t);}
  static bool CuEmpty(Alert_LP a){a.Refresh();return a.Targets.Count==0;}
  void CheckCleanup(){
   bool oldAlerts=LichMod.Settings.dndAlerts;LichMod.Settings.dndAlerts=true;
   cuBase=OpenCell();
   try{CleanupAlerts();CleanupTroll();CleanupDragons();CleanupSmall();}
   finally{
    LichMod.Settings.dndAlerts=oldAlerts;CleanupRules.EggRateHook=null;DragonTerrain.TestDark=null;AncientCrateLoot.TestRoll=null;
    foreach(var t in cuMade)if(t is Pawn p){if(p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();else if(!p.Destroyed)p.Destroy();}else if(!t.Destroyed)t.Destroy();
    cuMade.Clear();
   }
  }

  void CleanupAlerts(){
   // 1 巨魔尸体将会复活
   var troll=RacePawn("LP_Troll");cuMade.Add(troll);troll.Kill(null);var corpse=troll.Corpse;var a1=new Alert_LP_TrollRevive();
   bool shown=CuHas(a1,corpse) && a1.Priority==AlertPriority.High && a1.GetExplanation().ToString().Contains("火化");
   Troll.Gene(troll).CancelRevival(false);
   Check(shown && CuEmpty(a1),"alert 巨魔尸体将会复活: shows the reviving corpse (High, says burn/cremate) and clears once the revival is cancelled");
   // 2 蝌蚪化形中
   var host=CuPawn(Faction.OfPlayer,2,0);var tad=Tadpoles.Implant(host,null,null,out _);var a2=new Alert_LP_Ceremorph();
   bool tShown=tad!=null && CuHas(a2,host) && a2.GetExplanation().ToString().Contains("天后完成");
   if(tad!=null)host.health.RemoveHediff(tad);
   Check(tShown && CuEmpty(a2),"alert 蝌蚪化形中: shows the host with days left and how to stop it, clears when the tadpole is removed");
   // 3 龙蛋即将孵化（不搭宝库：用测试钩子给速度 1）
   var egg=ThingMaker.MakeThing(DragonHoard.EggDef);GenSpawn.Spawn(egg,cuBase+IntVec3.North*3,map);cuMade.Add(egg);var ec=egg.TryGetComp<CompDragonEgg>();
   CleanupRules.EggRateHook=e=>1f;var a3=new Alert_LP_DragonEgg();
   ec.progressDays=ec.HatchDays-2f;bool eShown=CuHas(a3,egg);ec.progressDays=ec.HatchDays-5f;bool eHidden=CuEmpty(a3);
   CleanupRules.EggRateHook=null;
   Check(eShown && eHidden,"alert 龙蛋即将孵化: an egg 2 days from hatching is shown, 5 days is not");
   // 4 巫妖复生中 / 5 命匣不在殖民地
   var soul=CuPawn(Faction.OfPlayer,-2,0);soul.Kill(null);
   var box=(Phylactery)ThingMaker.MakeThing(Necro.Def<ThingDef>("Phylactery"));box.soul=soul;box.revivalTick=Find.TickManager.TicksGame+50000;
   var at=cuBase+IntVec3.South*3;map.areaManager.Home[at]=true;GenSpawn.Spawn(box,at,map);cuMade.Add(box);var a4=new Alert_LP_LichRevival();var a5=new Alert_LP_PhylacteryAway();
   bool rShown=CuHas(a4,box) && a4.GetExplanation().ToString().Contains("小时后复生");bool homeOk=CuEmpty(a5);
   box.revivalTick=-1;bool rHidden=CuEmpty(a4);
   Check(rShown && rHidden,"alert 巫妖复生中: shows the phylactery with time left, clears when the revival timer is gone");
   map.areaManager.Home[at]=false;bool away=CuHas(a5,box) && a5.Priority==AlertPriority.High && a5.GetExplanation().ToString().Contains("家园区外");
   map.areaManager.Home[at]=true;
   Check(homeOk && away && CuEmpty(a5),"alert 命匣不在殖民地: quiet inside the home area, High alert targeting the phylactery outside it, clears when it is back");
   // 6 脑髓不足
   var flayer=RacePawn("LP_MindFlayer");cuMade.Add(flayer);var g=MindFlayer.Cerebrum(flayer);var a6=new Alert_LP_LowBrain();
   bool bShown=false,bHidden=false;
   if(g!=null){g.Value=g.Max*.1f;bShown=CuHas(a6,flayer);g.Value=g.Max;bHidden=!CuHas(a6,flayer);}
   Check(g!=null && bShown && bHidden,"alert 脑髓不足: a colony mind flayer at 10% cerebrum is shown, at full it is not");
  }

  void CleanupTroll(){
   // 7 巨魔尸体过滤
   var troll=RacePawn("LP_Troll");cuMade.Add(troll);troll.Kill(null);var tc=troll.Corpse;Troll.Gene(troll).CancelRevival(false);
   var human=CuPawn(Faction.OfPlayer,0,2);human.Kill(null);var hc=human.Corpse;
   var trollF=new SpecialThingFilterWorker_TrollCorpse();var otherF=new SpecialThingFilterWorker_NonTrollCorpse();
   var defs=DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("LP_AllowTrollCorpses")!=null && TrollCremation.NonTroll!=null;
   Check(defs && trollF.Matches(tc) && !trollF.Matches(hc) && otherF.Matches(hc) && !otherF.Matches(tc) && trollF.CanEverMatch(tc.def),"troll corpse filters: 巨魔尸体 / 非巨魔尸体 split corpses by the troll regeneration gene (same test as revival)");
   // 8 标记焚烧：没有火葬场只提示；有火葬场时最前面一张只收巨魔的火化账单
   foreach(var old in TrollCremation.Crematoria(map).ToList())old.Destroy();
   var none=TrollCremation.Mark(tc,false);
   var cd=DefDatabase<ThingDef>.GetNamed("ElectricCrematorium");var crem=(Building_WorkTable)ThingMaker.MakeThing(cd,cd.MadeFromStuff?GenStuff.DefaultStuffFor(cd):null);crem.SetFaction(Faction.OfPlayer);
   GenSpawn.Spawn(crem,cuBase+new IntVec3(6,0,0),map);cuMade.Add(crem);
   crem.BillStack.AddBill(TrollCremation.Cremate.MakeNewBill());
   tc.SetForbidden(true,false);var bill=TrollCremation.Mark(tc,false);var again=TrollCremation.Mark(tc,false);
   Check(none==null && bill!=null && again==bill && crem.BillStack.Bills.First()==bill && bill.IsFixedOrAllowedIngredient(tc) && !bill.IsFixedOrAllowedIngredient(hc) && !tc.IsForbidden(Faction.OfPlayer) && bill.ShouldDoNow(),"mark for burning: no crematorium → null; otherwise one troll-only cremation bill at the top that accepts the troll corpse (now unforbidden), not other corpses");
  }

  void CleanupDragons(){
   var cost=AccessTools.Method(typeof(Pawn_PathFollower),"CostToMoveIntoCell",new[]{typeof(Pawn),typeof(IntVec3)});
   float Cost(Pawn p,IntVec3 c)=>(float)cost.Invoke(null,new object[]{p,c});
   Pawn Dragon(string color,int dx,int dz){var d=Dragons.Make(color,0,Faction.OfPlayer,map.Tile);GenSpawn.Spawn(d,cuBase+new IntVec3(dx,0,dz),map);cuMade.Add(d);return d;}
   void Ground(IntVec3 c,string terrain,float snow){map.terrainGrid.SetTerrain(c,DefDatabase<TerrainDef>.GetNamed(terrain));map.snowGrid.SetDepth(c,snow);}
   // 9 白龙：冰面 + 厚雪，消耗只算基础步长；普通人更慢
   var white=Dragon("White",-6,-6);var man=CuPawn(Faction.OfPlayer,-6,-4);
   var wc=white.Position+IntVec3.East;var mc=man.Position+IntVec3.East;Ground(wc,"Ice",1f);Ground(mc,"Ice",1f);
   float wCost=Cost(white,wc),mCost=Cost(man,mc);
   Check(Near(wCost,DragonTerrain.BaseCost(white,wc)) && mCost>DragonTerrain.BaseCost(man,mc)+.5f,"white dragon on ice with deep snow moves at base cost ("+wCost.ToString("0.#")+" = "+DragonTerrain.BaseCost(white,wc).ToString("0.#")+"), a human pays more ("+mCost.ToString("0.#")+")");
   // 10 黑龙：沼泽消耗按 1；在水里被远程命中 ×0.7；深水仍不可走
   var black=Dragon("Black",-6,-9);var bc=black.Position+IntVec3.East;var hc2=man.Position+IntVec3.South;Ground(bc,"Marsh",0f);Ground(hc2,"Marsh",0f);
   float bCost=Cost(black,bc),hCost=Cost(man,hc2);
   var stand=black.Position;Ground(stand,"Marsh",0f);float inWater=CleanupShotReport.TargetFactor(black);Ground(stand,"Soil",0f);float dry=CleanupShotReport.TargetFactor(black);
   bool deep=!DragonTerrain.BogTerrain(TerrainDefOf.WaterDeep);
   Check(Near(bCost,DragonTerrain.BaseCost(black,bc)) && hCost>DragonTerrain.BaseCost(man,hc2)+.5f && Near(inWater,.7f) && Near(dry,1f) && deep,"black dragon in a marsh moves at base cost ("+bCost.ToString("0.#")+" vs human "+hCost.ToString("0.#")+"), ranged hits on it in water ×"+inWater.ToString("0.##")+", deep water stays impassable");
   // 11 黑龙巢穴：那张地图永久日食；黑龙在黑暗中命中 +10%
   var patched=Harmony.GetPatchInfo(AccessTools.Method(typeof(GenStep_DragonLair),nameof(GenStep_DragonLair.Build)))?.Postfixes.Any(x=>x.owner=="local.lich.phylactery")==true;
   BlackLairEclipse.Apply(map);var ecl=map.gameConditionManager.GetActiveCondition(GameConditionDefOf.Eclipse);
   bool permanent=ecl!=null && ecl.Permanent && !Find.World.gameConditionManager.ConditionIsActive(GameConditionDefOf.Eclipse);ecl?.End();
   DragonTerrain.TestDark=true;float darkHit=CleanupShotReport.ShooterFactor(black),whiteDark=CleanupShotReport.ShooterFactor(white);DragonTerrain.TestDark=false;float light=CleanupShotReport.ShooterFactor(black);DragonTerrain.TestDark=null;
   Check(patched && permanent && Near(darkHit,1.1f) && Near(whiteDark,1f) && Near(light,1f),"black dragon lair: permanent eclipse on that map only (patched "+patched+"), black dragon hits ×"+darkHit.ToString("0.##")+" in darkness");
  }

  // 19 敌人冒险者等级 / 敌人传奇等级合并：只有旧键的老设置一律重置为开（包括两份都关）；有新键时按新键；新设置默认开。
  void CleanupMergedToggles(){
   Settings L(string body)=>ExLoadSettings(body);
   var coexOnly=L("<adventurerEnemyLevels>False</adventurerEnemyLevels><adventurerEnemyLevelsCoexist>True</adventurerEnemyLevelsCoexist><adventurerEnemyLegend>False</adventurerEnemyLegend><adventurerEnemyLegendCoexist>True</adventurerEnemyLegendCoexist>");
   var normalOnly=L("<adventurerEnemyLevelsCoexist>False</adventurerEnemyLevelsCoexist><adventurerEnemyLegendCoexist>False</adventurerEnemyLegendCoexist>");
   var bothOff=L("<adventurerEnemyLevels>False</adventurerEnemyLevels><adventurerEnemyLegend>False</adventurerEnemyLegend>");
   var newOff=L("<lpEnemyLevels163>0</lpEnemyLevels163><adventurerEnemyLevelsCoexist>True</adventurerEnemyLevelsCoexist><lpEnemyLegend163>1</lpEnemyLegend163>");
   var fresh=new Settings();
   Check(coexOnly!=null && coexOnly.adventurerEnemyLevels && coexOnly.adventurerEnemyLegend && normalOnly.adventurerEnemyLevels && normalOnly.adventurerEnemyLegend && bothOff!=null && bothOff.adventurerEnemyLevels && bothOff.adventurerEnemyLegend
    && !newOff.adventurerEnemyLevels && newOff.adventurerEnemyLegend && fresh.adventurerEnemyLevels && fresh.adventurerEnemyLegend && AdventurerEnemies.OnFor(fresh,true)==AdventurerEnemies.OnFor(fresh,false) && Legend.EnemyOnFor(fresh,true)==Legend.EnemyOnFor(fresh,false),
    "敌人冒险者等级 / 敌人传奇等级 merged into one toggle each: a legacy config (only old keys) is reset to on even with both copies off, a saved new key wins (0 → off), default on in both modes");
  }
  // 20～25 第二批遗留小项
  void CleanupLeftovers(){
   // 20 半巫妖谈判：成功率公式与上限
   var lich=CuPawn(Faction.OfPlayer,12,0);var soc=lich.skills.GetSkill(SkillDefOf.Social);int lv=Adventurer.Get(lich)?.level??0;
   soc.Level=10;float c10=DemilichParley.Chance(lich);soc.Level=20;float c20=DemilichParley.Chance(lich);soc.Level=0;float c0=DemilichParley.Chance(lich);soc.Level=10;
   Check(Near(c10,Math.Min(.9f,.8f+.01f*lv)) && Near(c20,.9f) && Near(c0,Math.Min(.9f,.5f+.01f*lv)),"demilich parley: 50% + social×3% + level×1%, capped at 90% (social 10 → "+c10.ToStringPercent()+", 20 → "+c20.ToStringPercent()+")");
   // 21 谈判成功 / 失败两个分支
   var foe=Find.FactionManager.AllFactions.FirstOrDefault(f=>!f.IsPlayer && f.HostileTo(Faction.OfPlayer) && !f.def.hidden)??Factions.Hostile;
   var d1=Monsters.Make(Monsters.Demilich,foe,map.Tile);var d2=Monsters.Make(Monsters.Demilich,foe,map.Tile);
   bool parleyOk=false;string parleyWhy="no demilich (HAR off?)";
   if(d1!=null && d2!=null){
    GenSpawn.Spawn(d1,lich.Position+IntVec3.East*4,map);GenSpawn.Spawn(d2,lich.Position+IntVec3.West*4,map);cuMade.Add(d1);cuMade.Add(d2);
    bool both=DemilichParley.CanParley(lich,d1) && DemilichParley.CanParley(lich,d2);
    var at=d1.Position;int arcane0=map.listerThings.AllThings.Count(t=>t.def.defName==Netheril.ArcaneDef || t.def==Faith.ShardDef);
    bool won=DemilichParley.Resolve(lich,d1,0f);int arcane1=map.listerThings.AllThings.Count(t=>t.def.defName==Netheril.ArcaneDef || t.def==Faith.ShardDef);
    bool lost=!DemilichParley.Resolve(lich,d2,.99f);
    parleyOk=both && won && !d1.Spawned && arcane1==arcane0+1 && lost && d2.Spawned && DemilichParley.Body(d2).parleyed && !DemilichParley.CanParley(lich,d2) && !DemilichParley.Resolve(lich,d2,0f);
    parleyWhy="both "+both+" won "+won+" gone "+!d1.Spawned+" gift "+(arcane1-arcane0)+" lost "+lost+" once "+!DemilichParley.CanParley(lich,d2);
   }
   Check(parleyOk,"demilich parley: success sends it away and leaves an arcane fragment or miracle shard; failure keeps it hostile; only once per demilich ("+parleyWhy+")");
   // 22 吞眼魔脑髓
   var flayer=RacePawn("LP_MindFlayer");cuMade.Add(flayer);var g=MindFlayer.Cerebrum(flayer);var bh=Monsters.Make(Monsters.Beholder,foe,map.Tile);bool brainOk=false;string brainWhy="";
   if(g!=null && bh!=null){
    GenSpawn.Spawn(bh,flayer.Position+IntVec3.East,map);cuMade.Add(bh);bh.Kill(null);var bc=bh.Corpse;g.Value=g.Max*.2f;
    float got=MindFlayer.Devour(flayer,bc);var ins=flayer.health.hediffSet.GetFirstHediffOfDef(BeholderBrain.Insight);
    brainOk=got>0f && Near(g.ValuePercent,1f) && ins!=null && Near(ins.CurStage.statOffsets.GetStatOffsetFromList(StatDefOf.PsychicSensitivity),.2f) && ins.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear==600000;
    brainWhy="got "+got+" brain "+g.ValuePercent.ToStringPercent()+" insight "+(ins!=null)+" why "+MindFlayer.CannotDevour(flayer,bc);
   }
   Check(brainOk,"mind flayer devouring a beholder: cerebrum refilled, 10 days of 眼魔之识 (+20% psychic sensitivity, -20% cooldowns) ("+brainWhy+")");
   // 23 拉洛克情报：每次来访一次、钱不够不能买
   var st=Netheril.State;var oldEnvoy=st.envoy;int oldId=st.intelEnvoyId;var envoy=CuPawn(null,14,0);
   try{st.envoy=envoy;st.intelEnvoyId=envoy.thingIDNumber;string once=LarlochIntel.Blocked(st,map);st.intelEnvoyId=-1;string other=LarlochIntel.Blocked(st,map);
    Check(once!=null && once.Contains("买过") && other!=null && (other.Contains("白银") || other.Contains("等级")) && LarlochIntel.Price==1500,"Larloch intel: 1500 silver, once per visit ('"+once+"'), refused without enough silver or level ('"+other+"')");}
   finally{st.envoy=oldEnvoy;st.intelEnvoyId=oldId;}
   // 24 纳巴苏：离群目标
   var nab=AbyssRaces.Make("LP_AH_Nabassu",AbyssRaces.Horde(),map.Tile);bool nabOk=false;string nabWhy="no nabassu";
   if(nab!=null){
    var pair1=CuPawn(Faction.OfPlayer,-14,0);var pair2=CuPawn(Faction.OfPlayer,-14,1);var lone=CuPawn(Faction.OfPlayer,-14,12);
    GenSpawn.Spawn(nab,cuBase+new IntVec3(-14,0,-4),map);cuMade.Add(nab);
    var best=AttackTargetFinder.BestAttackTarget(nab,TargetScanFlags.NeedThreat|TargetScanFlags.NeedAutoTargetable,null,0f,40f)?.Thing;
    // 地图上别处可能还有更近的落单殖民者，所以只要求选中的是落单的、不是那一对。
    nabOk=NabassuTactics.Isolated(lone) && !NabassuTactics.Isolated(pair1) && NabassuTactics.IsolatedTarget(nab) is Pawn it && NabassuTactics.Isolated(it) && best is Pawn bp && NabassuTactics.Isolated(bp) && bp!=pair1 && bp!=pair2;
    nabWhy="best "+best?.LabelShort+" lone "+lone.LabelShort;
   }
   Check(nabOk,"nabassu prefers an isolated target (no ally within 5 cells) ("+nabWhy+")");
   // 25 敌方卓尔黑暗术：AI 只对对方密集处放
   var drow=RacePawn("LP_Drow");cuMade.Add(drow);drow.SetFaction(foe);var ad=DefDatabase<AbilityDef>.GetNamed("LP_DrowDarkness");if(drow.abilities.GetAbility(ad)==null)drow.abilities.GainAbility(ad);
   var comp=drow.abilities.GetAbility(ad).comps.OfType<CompAbilityEffect_DrowDarkness>().First();
   var crowd=CuPawn(Faction.OfPlayer,16,8);CuPawn(Faction.OfPlayer,17,8);CuPawn(Faction.OfPlayer,16,9);var single=CuPawn(Faction.OfPlayer,16,-12);
   Check(ad.aiCanUse && comp.AICanTargetNow(new LocalTargetInfo(crowd)) && !comp.AICanTargetNow(new LocalTargetInfo(single)) && ad.cooldownTicksRange.min>=60000,"hostile drow cast darkness on a crowd of 3 (not on a lone target); 1-day cooldown = once per fight");
   // 26 地精埋伏：带地精劫掠者的边缘走入袭击 25% 改成分路包抄
   var gk=DefDatabase<PawnKindDef>.GetNamedSilentFail(GoblinAmbush.RaiderKind);var gf=Factions.Hostile??foe;
   List<Pawn> Band(PawnKindDef k){var l=new List<Pawn>();for(int i=0;i<3;i++){var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(k,gf,forceGenerateNewPawn:true,canGeneratePawnRelations:false));cuMade.Add(p);l.Add(p);}return l;}
   IncidentParms Parms(){var ip=new IncidentParms{target=map,faction=gf,raidArrivalMode=PawnsArrivalModeDefOf.EdgeWalkIn,points=500f};PawnsArrivalModeDefOf.EdgeWalkIn.Worker.TryResolveRaidSpawnCenter(ip);return ip;}
   int fired=GoblinAmbush.Fired;bool gOk=false;string gWhy="no goblin kind";
   if(gk!=null){
    try{
     GoblinAmbush.TestRoll=.1f;var b1=Band(gk);var p1=Parms();PawnsArrivalModeDefOf.EdgeWalkIn.Worker.Arrive(b1,p1);
     GoblinAmbush.TestRoll=.5f;var b2=Band(gk);var p2=Parms();PawnsArrivalModeDefOf.EdgeWalkIn.Worker.Arrive(b2,p2);
     GoblinAmbush.TestRoll=.1f;var b3=Band(DefDatabase<PawnKindDef>.GetNamed("LP_FH_OrcWarrior"));var p3=Parms();PawnsArrivalModeDefOf.EdgeWalkIn.Worker.Arrive(b3,p3);
     gOk=b1.All(p=>p.Spawned) && p1.raidArrivalMode==PawnsArrivalModeDefOf.EdgeWalkInGroups && b2.All(p=>p.Spawned) && p2.raidArrivalMode==PawnsArrivalModeDefOf.EdgeWalkIn && b3.All(p=>p.Spawned) && p3.raidArrivalMode==PawnsArrivalModeDefOf.EdgeWalkIn && GoblinAmbush.Fired==fired+1;
     gWhy="modes "+p1.raidArrivalMode?.defName+"/"+p2.raidArrivalMode?.defName+"/"+p3.raidArrivalMode?.defName+" fired "+(GoblinAmbush.Fired-fired);
    }finally{GoblinAmbush.TestRoll=null;}
   }
   Check(gOk,"goblin ambush: a walk-in raid with goblin raiders splits into groups from several edges at roll 10% (25% chance), not at 50%, and never without goblins ("+gWhy+")");
  }
  void CleanupSmall(){
   CleanupMergedToggles();CleanupLeftovers();
   // 12 / 13 法术抗体：友方增益不挡；敌方法术照挡
   var wa=CuPawn(Faction.OfPlayer,3,3);HeroTraits.Give(wa,HeroDefOf.LP_SpellWarded);var wi=HeroTraits.Info(wa);
   var friend=CuPawn(Faction.OfPlayer,4,3);var foe=CuPawn(Factions.Hostile??Find.FactionManager.RandomEnemyFaction(),5,3);
   Check(wi!=null && wi.warded && !HeroWardedBuff.Blocks(friend,wa,0f) && Near(HeroCombat.InFactor(wa,wi,friend,new DamageInfo(Pacts.Eldritch,10f,0f,-1f,friend)),1f),"法术抗体 does not block friendly buffs or friendly spells");
   Check(HeroWardedBuff.Blocks(foe,wa,.2f) && !HeroWardedBuff.Blocks(foe,wa,.8f) && Near(HeroCombat.InFactor(wa,wi,foe,new DamageInfo(Pacts.Eldritch,10f,0f,-1f,foe)),.65f),"法术抗体 still resists hostile casters (status 50%, spell damage ×0.65)");
   // 14 / 15 宝箱额外掉落
   var crate=ThingMaker.MakeThing(ThingDefOf.Steel);cuMade.Add(crate);
   AncientCrateLoot.TestRoll=.1f;var att=AncientCrateLoot.Roll(crate,true);var noAtt=AncientCrateLoot.Roll(crate,false);cuMade.AddRange(att);cuMade.AddRange(noAtt);
   Check(att.Count==1 && att[0].def.HasModExtension<AccessorySlot>() && noAtt.Count==0,"ancient crate: an attuner present gives a magic accessory at 15% (roll 10%), nobody else does");
   AncientCrateLoot.TestRoll=.04f;var tome=AncientCrateLoot.Roll(crate,false);cuMade.AddRange(tome);AncientCrateLoot.TestRoll=.06f;var none=AncientCrateLoot.Roll(crate,false);AncientCrateLoot.TestRoll=null;
   Check(tome.Count==1 && LichPhylactery.Growth.TomeIndex(tome[0].def)>=0 && none.Count==0 && Harmony.GetPatchInfo(AccessTools.Method(typeof(Building_Crate),nameof(Building_Crate.Open)))?.Postfixes.Any(x=>x.owner=="local.lich.phylactery")==true,"ancient crate: 5% attribute tome (roll 4% yes, 6% no), hooked on opening");
   // 16 狗头人刺坑
   var pit=(Building_KoboldSpikePit)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_KoboldSpikePit"));pit.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(pit,cuBase+new IntVec3(-3,0,4),map);cuMade.Add(pit);
   var victim=CuPawn(Faction.OfPlayer,-3,6);float small=Building_KoboldSpikePit.DamageTo(victim,pit);var big=Dragons.Make("Red",2,Faction.OfPlayer,map.Tile);cuMade.Add(big);float large=Building_KoboldSpikePit.DamageTo(big,pit);
   var spring=AccessTools.Method(typeof(Building_KoboldSpikePit),"SpringSub");
   spring.Invoke(pit,new object[]{null});spring.Invoke(pit,new object[]{null});Flush();bool alive=!pit.Destroyed;spring.Invoke(pit,new object[]{null});Flush();
   Check(Near(small,20f) && big.BodySize>1.5f && Near(large,10f) && alive && pit.Destroyed && !pit.def.building.trapDestroyOnSpring,"kobold spike pit: 20 sharp (×0.5 against body size > 1.5: "+large+"), survives 2 springs and collapses on the 3rd");
   // 17 魅魔被识破：周围的人「被欺骗」-5，5 天
   var s=CuPawn(Faction.OfPlayer,8,-3);var near=CuPawn(Faction.OfPlayer,9,-3);var far=CuPawn(Faction.OfPlayer,8,-3-(int)SuccubusDeceit.Radius-4);
   int n=SuccubusDeceit.Apply(s);var mem=near.needs.mood.thoughts.memories.GetFirstMemoryOfDef(SuccubusDeceit.Deceived);
   Check(n>=1 && mem!=null && mem.otherPawn==s && Near(SuccubusDeceit.Deceived.stages[0].baseMoodEffect,-5f) && Near(SuccubusDeceit.Deceived.durationDays,5f) && far.needs.mood.thoughts.memories.GetFirstMemoryOfDef(SuccubusDeceit.Deceived)==null,"succubus revealed: colonists nearby get 被欺骗 (-5, 5 days), far ones do not");
   // 18 眼柄配方
   var fb=DefDatabase<RecipeDef>.GetNamedSilentFail("LP_WriteScrollFireball"+EyestalkRecipes.Suffix);var desk=DefDatabase<ThingDef>.GetNamed("LP_ScribeDesk");
   int Count(RecipeDef r,string d)=>(int)(r?.ingredients.FirstOrDefault(i=>i.filter.AllowedThingDefs.Any(x=>x.defName==d))?.GetBaseCount()??-1);
   Check(fb!=null && Count(fb,"LP_LavaCrystal")==1 && Count(fb,"LP_BeholderStalk")==1 && Count(fb,"Cloth")==10 && desk.AllRecipes.Contains(fb) && fb.products.Any(p=>p.thingDef.defName=="LP_ScrollFireball") && EyestalkRecipes.Made.Count>=4,"eyestalk recipes: fireball scroll with 1 eyestalk instead of half the lava crystals (2 → 1) is on the scribe desk ("+EyestalkRecipes.Made.Count+" variants)");
  }
 }
}
