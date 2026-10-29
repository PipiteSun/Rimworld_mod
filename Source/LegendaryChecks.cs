using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.52.0 通用传奇层（组名 Legendary：Test.ps1 -Only Legendary；不需要 HAR，-NoHAR 也跑）。
 // 用一个代码里造的“测试传奇兵种”（人类 + LP_LegendaryExtension），不依赖五色龙。
 public partial class LichChecks {
  readonly List<Pawn> legMade=new List<Pawn>();static int legKinds;
  class LA_TestAction : LegendaryActionWorker {public int done;public override string Label=>"测试动作";public override bool Do(Pawn l,Pawn e){done++;return true;}}
  PawnKindDef LegTestKind(LP_LegendaryExtension e){
   var k=new PawnKindDef{defName="LPTest_LegendKind"+(legKinds++),label="测试传奇",race=ThingDefOf.Human,combatPower=500f,modExtensions=new List<DefModExtension>{e}};
   DefDatabase<PawnKindDef>.Add(k); // 自检进程里注册，存读档时死去的测试单位还能找到兵种
   return k;
  }
  Pawn LegPawn(Faction f,PawnKindDef kind=null,int age=30){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,f,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,mustBeCapableOfViolence:true,fixedBiologicalAge:age,fixedChronologicalAge:age));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h.def.isBad || h is Hediff_AddedPart).ToList())p.health.RemoveHediff(h);
   if(kind!=null){p.kindDef=kind;Legendary.Setup(p);}
   legMade.Add(p);return p;
  }
  Pawn LegSpawn(Pawn p,IntVec3 near,int r=3){GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(near,map,r),map);return p;}
  static Faction LegEnemy()=>Find.FactionManager.AllFactions.FirstOrDefault(f=>!f.IsPlayer && !f.Hidden && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction)??Find.FactionManager.AllFactions.First(f=>!f.IsPlayer && f.HostileTo(Faction.OfPlayer));

  void CheckLegendary(){
   bool oSup=Adventurer.TestSuppressed;
   try{CheckLegendaryInner();}
   finally{
    Legendary.TestD20=null;Legendary.TestRoll=null;Adventurer.TestSuppressed=oSup;
    Find.TaleManager.AllTalesListForReading.RemoveAll(x=>x.def.defName=="LP_SlewLegend");
    foreach(var p in legMade){if(p.kindDef?.defName.StartsWith("LPTest_LegendKind")==true)p.kindDef=PawnKindDefOf.Colonist;if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!p.Destroyed){if(p.Spawned)p.DeSpawn();if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}}
    legMade.Clear();map.GetComponent<MapComponent_Legendary>()?.Forget();
   }
  }
  void CheckLegendaryInner(){
   bool har=ModsConfig.IsActive(HARId);
   // 1 加载：传奇层定义在主模组；五色龙只在有 HAR 时存在；没有 HAR 时真龙事件不触发
   var ext=Items.Hediff("LP_LegendaryResistance");
   var extortion=DefDatabase<IncidentDef>.GetNamed("LP_DragonExtortion");var raid=DefDatabase<IncidentDef>.GetNamed("LP_DragonRaid");
   var parms=new IncidentParms{target=map,points=20000f};
   bool firesWithout=har || !extortion.Worker.CanFireNow(parms) && !raid.Worker.CanFireNow(parms);
   Check(ext!=null && Items.Hediff("LP_LegendaryActions")!=null && Items.Hediff("LP_Frightened")!=null && Legendary.Rules!=null && Legendary.Rules.controlHediffs.Count>=10
    && Dragons.Available==har && Dragons.On==har && firesWithout && Dragons.FactionDef!=null && Items.Thing("LP_DragonScale_Red")!=null,
    "legendary layer defs load without HAR; chromatic dragons exist exactly when HAR is active ("+har+"); without HAR the dragon events never fire and the settings group is greyed out");

   // 2 豁免公式：熟练加值（含 21～40 级）、DC；同一 DC 下属性/等级越高成功越多；没有等级的人按 0
   Check(Legendary.Proficiency(1)==2 && Legendary.Proficiency(5)==3 && Legendary.Proficiency(17)==6 && Legendary.Proficiency(20)==6 && Legendary.Proficiency(28)==7 && Legendary.Proficiency(36)==8 && Legendary.Proficiency(0)==0,"proficiency bonus 2+(L-1)/4 up to 20, then 6+(L-20)/8 (L28 = 7, L36 = 8)");
   var weak=LegPawn(Faction.OfPlayer);var strong=LegPawn(Faction.OfPlayer);
   var hw=Adventurer.Get(weak)??Adventurer.TryAdd(weak,1,true);var hs=Adventurer.Get(strong)??Adventurer.TryAdd(strong,1,true);
   hw.level=1;hw.scores=new[]{10,10,10,10,10,10};hw.Dirty();hs.level=17;hs.scores=new[]{10,18,10,10,10,10};hs.Dirty();
   int Successes(Pawn p,int a,int dc){int n=0;for(int r=1;r<=20;r++){Legendary.TestD20=r;if(Legendary.Save(p,a,dc))n++;}Legendary.TestD20=null;return n;}
   var animal=PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Muffalo"));legMade.Add(animal);
   int sw=Successes(weak,Adventurer.Dex,15),ss=Successes(strong,Adventurer.Dex,15),sa=Successes(animal,Adventurer.Dex,15);
   Check(sw==8 && ss==16 && sa==6 && Legendary.SaveBonus(animal,Adventurer.Dex,null)==0,"DC 15 Dex save over d20=1..20: level-1 Dex 10 succeeds "+sw+"×, level-17 Dex 18 (+4 +6) "+ss+"×, an animal (no level → +0) "+sa+"×");
   // 1.59.0 热修（去 d20 措辞）：显示的成功率和实际判定一致
   float cw=Legendary.SaveChance(weak,Adventurer.Dex,15),cs=Legendary.SaveChance(strong,Adventurer.Dex,15),ca=Legendary.SaveChance(animal,Adventurer.Dex,15);
   Check(Near(Legendary.SaveChance(0,2,0,15),.40f) && Near(Legendary.SaveChance(5,6,0,15),.85f) && Near(Legendary.SaveChance(0,0,0,30),0f) && Near(Legendary.SaveChance(10,8,0,2),1f) && Near(cw,sw/20f) && Near(cs,ss/20f) && Near(ca,sa/20f),
    "save chance shown as a percentage: DC 15 base (mod 0, prof +2) 40%, +5/+6 85%, clamped 0..100%; matches the judged faces (weak "+cw.ToStringPercent()+", strong "+cs.ToStringPercent()+", animal "+ca.ToStringPercent()+")");

   // 3 传奇单位的统一处理：固定等级、固定六维、种族亲和中性、CR 显示
   var e=new LP_LegendaryExtension{resistance=3,lairBonus=1,actionPoints=3,cr=17,legendPoints=2,presenceRadius=18f,level=new IntRange(24,24),scores=new List<int>{27,10,25,16,13,21},milestone=true,deathOnDowned=false,
    actions=new List<LegendaryActionEntry>{new LegendaryActionEntry{action="LPTest",cost=1}},title="测试传奇"};
   var test=new LA_TestAction();Legendary.Register("LPTest",test);
   var kind=LegTestKind(e);
   var foe=LegEnemy();
   var leg=LegPawn(foe,kind);
   var lh=Adventurer.Get(leg);
   bool fixedScores=lh!=null && lh.scores.SequenceEqual(new[]{27,10,25,16,13,21}) && Legendary.Score(leg,Adventurer.Str)==27 && Legendary.Mod(leg,Adventurer.Con)==7;
   Check(lh!=null && lh.level==24 && lh.legendary && lh.storyLevel && fixedScores && lh.legendSpent.Sum()==lh.legendSpent[Legend.Melee] && Legendary.DC(leg,Adventurer.Cha)==8+6+5 && Adventurer.PartHealthFactor(leg)==1f
    && Legendary.InspectLine(leg)=="挑战等级 CR 17 · 冒险者等级 24" && !leg.story.traits.allTraits.Any(t=>t.def.exclusionTags?.Contains("LP_PointTalent")==true),
    "[lv "+lh?.level+" leg "+lh?.legendary+" story "+lh?.storyLevel+" fixed "+fixedScores+" spent "+string.Join("/",lh?.legendSpent??new int[0])+" dc "+Legendary.DC(leg,Adventurer.Cha)+" part "+Adventurer.PartHealthFactor(leg)+"] legendary unit: fixed level 24 (story level, legendary, legend points all on melee), fixed scores 27/10/25/16/13/21 (Con mod +7), Cha DC 19 (8 + prof 6 + Cha 5), no adventurer part-health bonus, inspect line '"+Legendary.InspectLine(leg)+"', no point talents");

   // 4 传奇抗性：眩晕、外力精神状态、控制类状态各拦一次并扣次数；名字显示次数；0 次后不再拦
   LegSpawn(leg,altar.Position+IntVec3.East*8);
   var res=Legendary.Resistance(leg);res.Refill();
   string full=res.LabelBase;
   leg.stances.stunner.StunFor(120,null,false,false,false);bool stunBlocked=!leg.stances.stunner.Stunned && res.charges==2;
   bool mental=leg.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk,"测试",forced:true);bool mentalBlocked=!mental && !leg.InMentalState && res.charges==1;
   var slow=Items.Hediff("LP_FrostSlow");leg.health.AddHediff(HediffMaker.MakeHediff(slow,leg));bool controlBlocked=!Items.Has(leg,"LP_FrostSlow") && res.charges==0;
   leg.stances.stunner.StunFor(120,null,false,false,false);bool stunAfter=leg.stances.stunner.Stunned;
   string empty=res.LabelBase;
   Check(full=="传奇抗性 3/3" && stunBlocked && mentalBlocked && controlBlocked && stunAfter && empty=="传奇抗性耗尽","legendary resistance 3/3: stun, forced berserk and LP_FrostSlow each blocked once (3→0), label becomes '传奇抗性耗尽' and the next stun lands");
   leg.stances.stunner.StopStun();

   // 5 疼痛倒地：消耗一次改为不倒；部位缺失造成的不能移动照常倒地；每日恢复；巢穴 +1
   // 1.63.0：固定种子生成；割伤只落在不影响移动、不致命的外部部位（原来脚、脚趾也会被割掉，移动能力降到 0 时按规则照常倒地、不扣次数，检查就随机失败）。
   Rand.PushState(6307);Pawn tough;try{tough=LegPawn(foe,kind);}finally{Rand.PopState();}
   LegSpawn(tough,altar.Position+IntVec3.East*10);var tr=Legendary.Resistance(tough);tr.Refill();tr.charges=1;
   // 传奇单位固定体质 25 → 超凡体质“硬撑”也会拦倒地（还不看移动能力），和传奇抗性谁先拦取决于补丁顺序，检查就时好时坏。这里先把今天的硬撑用掉，只测传奇抗性。
   void SpendHold(){var ah=Adventurer.Get(tough);if(ah!=null){ah.holdStart=Find.TickManager.TicksGame;ah.holdUses=99;ah.holdUntil=0;}}
   SpendHold();
   bool Moves(BodyPartRecord x){for(var q=x;q!=null;q=q.parent)if(q.def.tags.Any(tg=>tg==BodyPartTagDefOf.MovingLimbCore || tg==BodyPartTagDefOf.MovingLimbSegment || tg==BodyPartTagDefOf.MovingLimbDigit))return true;return false;}
   List<BodyPartRecord> CutParts()=>tough.health.hediffSet.GetNotMissingParts().Where(x=>x.depth==BodyPartDepth.Outside && x.parent!=null && x.def!=BodyPartDefOf.Head && x.def.defName!="Neck" && !Moves(x) && !x.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource) && !x.def.tags.Contains(BodyPartTagDefOf.BreathingSource)).ToList();
   for(int i=0;i<60 && !tough.health.InPainShock;i++){var parts=CutParts();if(parts.Count==0)break;var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,tough,parts[i%parts.Count]);cut.Severity=4f;tough.health.AddHediff(cut);}
   bool pain=tough.health.InPainShock;
   AccessTools.Method(typeof(Pawn_HealthTracker),"CheckForStateChange").Invoke(tough.health,new object[]{null,null});
   bool upAfterPain=!tough.Downed && tr.charges==0;
   foreach(var h in tough.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())tough.health.RemoveHediff(h);Legendary.painWindow.Remove(tough);
   tr.charges=2;SpendHold();
   foreach(var leg2 in tough.health.hediffSet.GetNotMissingParts().Where(x=>x.def.defName=="Leg").ToList())tough.health.AddHediff(HediffDefOf.MissingBodyPart,leg2);
   bool downedByLegs=tough.Downed && tr.charges==2;
   tr.lastRefill=Find.TickManager.TicksGame-Legendary.ResistDayTicks-1;tr.charges=0;tr.TickInterval(250);for(int i=0;i<250 && tr.charges==0;i++)tr.TickInterval(1);
   bool daily=tr.charges==3;
   var oldHook=Legendary.InOwnLairHook;Legendary.InOwnLairHook=p=>p==tough;int lairMax=tr.Max;Legendary.InOwnLairHook=oldHook;
   Check(pain && upAfterPain && downedByLegs && daily && lairMax==4,"pain shock: one charge keeps the legend standing (pain "+pain+", up "+upAfterPain+", legs "+downedByLegs+", daily "+daily+"); losing both legs still downs it without using a charge; charges refill after a day; +1 in its own lair (max "+lairMax+")");

   // 6 传奇动作：敌人攻击后触发、扣点、按时间回点
   var acts=Legendary.Actions(leg);acts.points=3;
   var colonist=LegSpawn(LegPawn(Faction.OfPlayer),leg.Position+IntVec3.West*5,1);
   var mc=map.GetComponent<MapComponent_Legendary>();mc.Refresh();
   var used=Legendary.React(leg,colonist);
   float after=acts.points;for(int i=0;i<Legendary.ActionRegenTicks;i++)acts.TickInterval(1);
   var far=LegSpawn(LegPawn(Faction.OfPlayer),leg.Position+IntVec3.West*20,1);var none=Legendary.React(leg,far);
   Check(mc.Count>=1 && mc.legends.Contains(leg) && used!=null && test.done==1 && Near(after,2f) && Near(acts.points,3f) && none==null && acts.LabelBase=="传奇点 3/3","legendary actions: an enemy attack within 15 cells triggers one (3→2 points), +1 point after 4 s, nothing for an attacker 20 cells away; map component counts the legend");

   // 7 尾击 / 振翼看部位：人类没有尾巴和翅膀
   Check(!Legendary.HasTail(leg) && !Legendary.CanFly(leg) && !Legendary.Worker("Tail").CanDo(leg,colonist) && !Legendary.Worker("Wing").CanDo(leg,colonist),"tail strike / wing buffet need a tail / at least one wing (a human legend has neither)");

   // 8 骇人威仪：失败给惊惧并 24 小时内不再判；成功 24 小时免疫；免疫恐惧的人不受影响
   var a=LegSpawn(LegPawn(Faction.OfPlayer),leg.Position+IntVec3.North*4,1);var b=LegSpawn(LegPawn(Faction.OfPlayer),leg.Position+IntVec3.South*4,1);var c=LegSpawn(LegPawn(Faction.OfPlayer),leg.Position+IntVec3.West*3,1);
   Items.Give(c,"LP_PotionHeroism");
   Legendary.TestRoll=.99f;
   Legendary.TestD20=1;bool fa=Legendary.Presence(leg,a,mc);
   Legendary.TestD20=20;bool fb=Legendary.Presence(leg,b,mc);
   Legendary.TestD20=1;bool fc=Legendary.Presence(leg,c,mc);
   mc.Forget();mc.Immune(leg,colonist,Legendary.FearImmuneTicks);mc.Immune(leg,a,Legendary.FearTicks+Legendary.FearImmuneTicks);mc.Immune(leg,b,Legendary.FearImmuneTicks);
   int again=mc.PresenceTick();
   Legendary.TestD20=null;
   Check(fa && Items.Has(a,"LP_Frightened") && Items.TicksLeft(a,"LP_Frightened")>3500 && !fb && !Items.Has(b,"LP_Frightened") && !fc && !Items.Has(c,"LP_Frightened") && Legendary.FearImmune(c) && mc.ImmuneTo(leg,b) && again==0
    && Near(Items.Hediff("LP_Frightened").stages[0].statFactors.GetStatFactorFromList(StatDefOf.MoveSpeed),.85f),"frightful presence: failed Wis save → frightened 60 s (move ×0.85), success → 24 h immune, heroism potion = fear immune; nobody is re-checked inside the window");

   // 9 击杀奖励：里程碑、传奇属性点给伤害 ≥10% 的传奇冒险者
   var hero=LegSpawn(LegPawn(Faction.OfPlayer),leg.Position+IntVec3.West*2,1);var hh=Adventurer.Get(hero)??Adventurer.TryAdd(hero,1,true);hh.level=Adventurer.MaxLevel;hh.legendary=true;hh.legendPoints=0;
   var helper=LegSpawn(LegPawn(Faction.OfPlayer),leg.Position+IntVec3.West*3,1);var hp2=Adventurer.Get(helper)??Adventurer.TryAdd(helper,1,true);hp2.legendary=true;hp2.legendPoints=0;
   Adventurer.RecordDamage(leg,hero,95f);Adventurer.RecordDamage(leg,helper,5f);
   int before=Legendary.Rewarded;
   lh.fateTick=Find.TickManager.TicksGame;leg.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,hero));
   Check(leg.Dead && hh.legendPoints==2 && hp2.legendPoints==0 && Legendary.Rewarded==before+1 && hh.milestonesDone.Any(k=>k.StartsWith("lp_slay_")),"[dead "+leg.Dead+" hero "+hh.legendPoints+" helper "+hp2.legendPoints+" rewarded "+(Legendary.Rewarded-before)+" ms "+string.Join(",",hh.milestonesDone)+"] killing a legend: DND milestone for the colonists on the map; +2 legend points only for the legend who dealt ≥10% of the damage");

   // 10 倒地即死
   var e2=new LP_LegendaryExtension{resistance=0,cr=5,level=new IntRange(5,5),deathOnDowned=true,milestone=false};
   var mortal=LegSpawn(LegPawn(foe,LegTestKind(e2)),altar.Position+IntVec3.South*8);
   mortal.health.AddHediff(HediffDefOf.Anesthetic);bool wasDowned=mortal.Downed || mortal.Dead;Step(2);Items.State?.Flush();
   Check(wasDowned && mortal.Dead,"[downed "+wasDowned+" now "+mortal.Downed+" dead "+mortal.Dead+" ext "+Legendary.Ext(mortal)?.deathOnDowned+" cons "+mortal.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness)+" anes "+Items.Has(mortal,HediffDefOf.Anesthetic)+"] legendary units die when downed (cannot be captured)");

   // 11 传奇种族不会食物中毒（1.52.0 热修）：传奇层异种 / 传奇单位因子 0、加中毒被拦；普通种族照常
   var meal=ThingMaker.MakeThing(ThingDefOf.MealSimple);
   var plain=LegPawn(Faction.OfPlayer);var giant=LegPawn(Faction.OfPlayer);var elf=LegPawn(Faction.OfPlayer);var unit=LegPawn(foe,LegTestKind(new LP_LegendaryExtension{level=new IntRange(5,5)}));
   giant.genes.SetXenotypeDirect(DefDatabase<XenotypeDef>.GetNamed("LP_FireGiant"));elf.genes.SetXenotypeDirect(DefDatabase<XenotypeDef>.GetNamed("LP_Elf"));
   bool Poisoned(Pawn p){FoodUtility.AddFoodPoisoningHediff(p,meal,FoodPoisonCause.Rotten);var h=p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.FoodPoisoning);if(h!=null)p.health.RemoveHediff(h);return h!=null;}
   var legXenos=Affinity.Table.Values.Where(x=>x.legendary).Select(x=>DefDatabase<XenotypeDef>.GetNamedSilentFail(x.key)).Where(x=>x!=null).ToList();
   var probe=LegPawn(Faction.OfPlayer);var missedX=legXenos.Where(x=>{probe.genes.SetXenotypeDirect(x);return !LegendaryFoodPoison.Immune(probe);}).Select(x=>x.defName).ToList();probe.genes.SetXenotypeDirect(XenotypeDefOf.Baseliner);
   float fp=FoodUtility.GetFoodPoisonChanceFactor(plain),fg=FoodUtility.GetFoodPoisonChanceFactor(giant),fe=FoodUtility.GetFoodPoisonChanceFactor(elf),fu=FoodUtility.GetFoodPoisonChanceFactor(unit);
   bool pp=Poisoned(plain),pg=Poisoned(giant),pe=Poisoned(elf),pu=Poisoned(unit);
   Check(fp>0f && fe>0f && fg==0f && fu==0f && pp && pe && !pg && !pu && legXenos.Count>=8 && missedX.Count==0 && !LegendaryFoodPoison.Immune(plain),
    "[factor human "+fp+" elf "+fe+" fire giant "+fg+" unit "+fu+"; poisoned "+pp+"/"+pe+"/"+pg+"/"+pu+"; legendary xenotypes "+legXenos.Count+(missedX.Count>0?" missed "+string.Join(",",missedX):"")+"] legendary races (affinity legendary tier, legendary units) never get food poisoning; common races still do");
   meal.Destroy();
  }
 }
}
