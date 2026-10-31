using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 // 1.70.0 野兽强化的检查（.\Test.ps1 -Only BeastLevels）。
 public partial class LichChecks {
  readonly List<Thing> blMade=new List<Thing>();
  static string blSavedId;static int blSavedLevel,blSavedElite,blSavedPack;
  static readonly string[] blBanned={"d20","D20","掷骰","检定","DC","豁免"};
  Pawn BlAnimal(string kind="Wolf_Timber",Faction faction=null,bool spawn=true,IntVec3? at=null){
   var req=new PawnGenerationRequest(PawnKindDef.Named(kind),faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,fixedBiologicalAge:4f,fixedChronologicalAge:4f);
   var p=PawnGenerator.GeneratePawn(req);
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h.def==HediffDefOf.Pregnant).ToList())p.health.RemoveHediff(h);
   blMade.Add(p);if(spawn)GenSpawn.Spawn(p,at??CellFinder.RandomClosewalkCellNear(map.Center,map,12),map);return p;
  }
  static float BlFac(Hediff h,StatDef s)=>h?.CurStage?.statFactors?.FirstOrDefault(m=>m.stat==s)?.value??1f;
  static float BlOff(Hediff h,StatDef s)=>h?.CurStage?.statOffsets?.FirstOrDefault(m=>m.stat==s)?.value??0f;
  static bool BlClean(string s)=>s==null || !blBanned.Any(b=>s.Contains(b));

  void CheckBeastLevels(){
   var s=LichMod.Settings;bool oOn=s.beastLevels,oAdv=s.adventurer,oSup=BeastLevels.TestSuppressed,oAsup=Adventurer.TestSuppressed;float oChance=s.beastEliteChance;
   BeastLevels.TestSuppressed=false;s.beastLevels=true;s.beastEliteChance=BeastLevels.DefaultElite;s.adventurer=true;BeastLevels.ForgetColony();
   try{BlDefs();BlFormula();BlLevelOne();BlElites();BlPack();BlGrowth();BlMisc();}
   finally{
    s.beastLevels=oOn;s.adventurer=oAdv;s.beastEliteChance=oChance;BeastLevels.TestSuppressed=oSup;Adventurer.TestSuppressed=oAsup;
    BeastLevels.TestRoll=null;BeastLevels.TestVariant=null;BeastLevels.TestNoise=null;BeastLevels.ForgetColony();
    foreach(var t in blMade)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    blMade.Clear();
    foreach(var t in map.listerThings.ThingsOfDef(BeastLevels.Heart).ToList())t.Destroy();
   }
  }

  // 1. 定义、中文名、贴图、配方。
  void BlDefs(){
   var d=BeastLevels.Def;var e=BeastLevels.EliteDef;var heart=BeastLevels.Heart;var amulet=BeastLevels.Amulet;
   Check(d!=null && d.hediffClass==typeof(Hediff_BeastLevel) && e!=null && e.hediffClass==typeof(Hediff_BeastElite) && d.maxSeverity>=BeastLevels.MaxLevel,"[beast levels] LP_BeastLevel / LP_BeastElite load with their classes");
   Check(Chinese(d.label) && Chinese(d.description) && Chinese(e.label) && Chinese(e.description) && Chinese(heart?.label) && Chinese(amulet?.label) && Chinese(amulet.description),"[beast levels] hediffs and items have Chinese labels");
   Check(heart!=null && Near(heart.BaseMarketValue,600f) && heart.stackLimit>1 && heart.graphicData!=null && ContentFinder<Texture2D>.Get(heart.graphicData.texPath,false)!=null && heart.graphicData.color!=Color.white,"[beast levels] 兽王之心 is a stackable ~600 silver item on a tinted vanilla texture");
   var recipe=DefDatabase<RecipeDef>.AllDefs.FirstOrDefault(r=>r.ProducedThingDef==amulet);var table=DefDatabase<ThingDef>.GetNamedSilentFail("LP_EnchantingTable");
   Check(recipe!=null && table!=null && recipe.AllRecipeUsers.Contains(table) && amulet.CostList.Any(c=>c.thingDef==heart && c.count==1) && amulet.apparel.layers.Contains(ApparelLayerDefOf.Belt) && Near(amulet.equippedStatOffsets.GetStatOffsetFromList(StatDefOf.MeleeDamageFactor),.1f) && ContentFinder<Texture2D>.Get(amulet.graphicData.texPath,false)!=null,"[beast levels] 兽王护符 is a belt apparel made at the enchanting table from one 兽王之心 (+10% melee)");
   var texts=new List<string>{d.description,e.description,heart.description,amulet.description};texts.AddRange(BeastLevels.EliteEffects);texts.AddRange(BeastLevels.EliteNames);
   Check(texts.All(BlClean),"[beast levels] new texts carry no dice wording");
  }

  // 2. 等级公式与倍率。
  void BlFormula(){
   Check(Near(BeastLevels.Score(1),10f) && Near(BeastLevels.Score(20),20f) && Near(BeastLevels.Score(30),24f) && Near(BeastLevels.Mult(1),1f) && Near(BeastLevels.Mult(20),4f) && Near(BeastLevels.Mult(30),7f),"[beast levels] virtual STR/CON: level 1=10 ×1, 20=20 ×4, 30=24 ×7");
   Check(Near(BeastLevels.MeleeFactor(1,0),1f) && Near(BeastLevels.PartFactor(1,0),1f) && Near(BeastLevels.SpeedFactor(1),1f) && Near(BeastLevels.SpeedFactor(40),1.3f) && Near(BeastLevels.PartFactor(20,0),1.95f*1.75f) && Near(BeastLevels.PartFactor(40,0),1.95f*2.35f),"[beast levels] multipliers are ×1 at level 1; speed caps at +30%; level 20 part HP ×3.4125 (1.95 × Con 20 ×1.75), level 40 ×(1.95 × 2.35) (1.81.0 hotfix3)");
   Check(BeastLevels.PackLevel(450f,40,0)==11 && BeastLevels.PackLevel(450f,40,2)==13 && BeastLevels.PackLevel(100000f,8,0)==8 && BeastLevels.PackLevel(0f,10,-2)==1,"[beast levels] pack level = 1 + points per pawn ÷45 ± noise, clamped 1..cap");
   Check(BeastLevels.AmbientMax(10)==7 && BeastLevels.AmbientMax(2)==1 && BeastLevels.AmbientMax(1)==1,"[beast levels] ambient wild animals roll 1..max(1, colony max − 3)");
   Check(BeastLevels.Cap()==Mathf.Clamp(AdventurerEnemies.ColonyMaxLevel()+3,1,BeastLevels.MaxLevel),"[beast levels] hostile cap = colony max adventurer level + 3 (legendary range only through this cap)");
  }

  // 3. 1 级野兽与原版完全一样；高等级加部位生命、近战伤害。
  void BlLevelOne(){
   BeastLevels.TestSuppressed=true;var plain=BlAnimal();BeastLevels.TestSuppressed=false;
   BeastLevels.TestNoise=0;BeastLevels.TestRoll=1f;var one=BlAnimal();
   var h=BeastLevels.Get(one);var torso=one.RaceProps.body.corePart;
   Check(BeastLevels.Get(plain)==null && h!=null && h.level==1 && h.elite==0 && one.health.hediffSet.hediffs.Contains(h),"[beast levels] a wild animal gets 野性等级 on its first spawn (suppressed spawn gets none)");
   Check(h.Label.Contains("野性等级 1") && h.Visible && one.GetInspectString().Contains("野性等级 1"),"[beast levels] the hediff label and inspect panel read 野性等级 1: "+h.Label);
   float m0=plain.GetStatValue(StatDefOf.MeleeDamageFactor),m1=one.GetStatValue(StatDefOf.MeleeDamageFactor),hp0=PartHealth.Maximum(torso,plain),hp1=PartHealth.Maximum(torso,one),v0=plain.GetStatValue(StatDefOf.MoveSpeed),v1=one.GetStatValue(StatDefOf.MoveSpeed);
   Check(Near(m0,m1) && Near(hp0,hp1) && Near(v0,v1),"[beast levels] level 1 is vanilla: melee "+m0+"/"+m1+", torso "+hp0+"/"+hp1+", speed "+v0+"/"+v1);
   h.level=20;h.Dirty();
   float m20=one.GetStatValue(StatDefOf.MeleeDamageFactor),hp20=PartHealth.Maximum(torso,one),v20=one.GetStatValue(StatDefOf.MoveSpeed);
   Check(Math.Abs(m20-m0*1.5f)<.05f && Math.Abs(hp20-Mathf.Ceil(hp0*1.95f*1.75f))<=1f && v20>v0*1.15f && Near(h.Severity,20f) && h.Label.Contains("20"),"[beast levels] level 20: melee ×1.5 (1.81.0 linear) ("+m20+"), torso "+hp20+" (≈"+hp0+"×3.4125), faster");
   BlToggleOff(one,m0);
  }
  void BlToggleOff(Pawn one,float m0){
   var s=LichMod.Settings;s.beastLevels=false;
   float off=one.GetStatValue(StatDefOf.MeleeDamageFactor);var fresh=BlAnimal();
   Check(Near(off,m0) && BeastLevels.Get(fresh)==null && BeastLevels.PartHealthFactor(one)==1f,"[beast levels] setting 野兽野性等级 off: no new levels, existing ones give no effect");
   s.beastLevels=true;
  }

  // 4. 精英：概率设置与四种变种。
  void BlElites(){
   var s=LichMod.Settings;
   Check(Near(BeastLevels.DefaultElite,.05f) && Near(BeastLevels.EliteMax,.2f),"[beast levels] 精英野兽概率 defaults to 5%, slider 0–20%");
   s.beastEliteChance=0f;BeastLevels.TestRoll=.01f;int none=BeastLevels.RollElite(false);
   s.beastEliteChance=.2f;BeastLevels.TestRoll=.1f;int some=BeastLevels.RollElite(false);BeastLevels.TestRoll=.5f;int miss=BeastLevels.RollElite(false);
   s.beastEliteChance=0f;int forced=BeastLevels.RollElite(true);s.beastEliteChance=.9f;float clamped=BeastLevels.EliteChance;
   Check(none==0 && some>0 && miss==0 && forced>0 && Near(clamped,.2f),"[beast levels] elite chance follows the setting (0% none, 20% hits at roll .1), leaders are forced, chance clamps at 20%");
   s.beastEliteChance=BeastLevels.DefaultElite;BeastLevels.TestRoll=1f;BeastLevels.TestNoise=0;
   Pawn Elite(int v){BeastLevels.TestSuppressed=true;var p=BlAnimal();BeastLevels.TestSuppressed=false;BeastLevels.Give(p,1,v);return p;}
   var dread=Elite(BeastLevels.Dread);var hd=BeastLevels.Get(dread);var torso=dread.RaceProps.body.corePart;
   BeastLevels.TestSuppressed=true;var plain=BlAnimal();BeastLevels.TestSuppressed=false;
   var eliteHediff=dread.health.hediffSet.hediffs.OfType<Hediff_BeastElite>().FirstOrDefault();
   Check(eliteHediff!=null && eliteHediff.Label.Contains("恐怖") && eliteHediff.LabelColor!=Color.white && Math.Abs(PartHealth.Maximum(torso,dread)-PartHealth.Maximum(torso,plain)*3f)<=1f && Near(BlFac(hd,StatDefOf.MeleeDamageFactor),1.5f),"[beast levels] 恐怖: colored label, part HP ×3, melee ×1.5");
   var frenzy=Elite(BeastLevels.Frenzy);var hf=BeastLevels.Get(frenzy);
   Check(Near(BlFac(hf,DefDatabase<StatDef>.GetNamed("MeleeCooldownFactor")),.67f) && frenzy.mindState.mentalStateHandler.neverFleeIndividual,"[beast levels] 狂暴: melee cooldown ×0.67 and never flees");
   var ancient=Elite(BeastLevels.Ancient);var ha=BeastLevels.Get(ancient);
   Check(ha.level==1+BeastLevels.AncientBonus && Near(BlOff(ha,StatDefOf.ArmorRating_Sharp),.4f) && Near(BlOff(ha,StatDefOf.ArmorRating_Blunt),.4f) && ha.Label.Contains("6"),"[beast levels] 远古: level +5, sharp/blunt armor +40%");
   var abyss=Elite(BeastLevels.Abyssal);var victim=BlAnimal("Muffalo",null,true,abyss.Position+IntVec3.East);
   var verb=BeastVerb(abyss,false);var res=MeleeHit(verb,victim);
   bool burned=victim.health.hediffSet.hediffs.Any(x=>x.def.defName=="Burn");
   Check(verb!=null && res.totalDamageDealt>0f && burned,"[beast levels] 深渊腐化: a melee hit adds burn damage ("+res.totalDamageDealt+" dealt)");
   // 死后掉兽王之心。
   int before=BeastLevels.HeartsDropped;var pos=ancient.Position;ancient.Kill(null);
   Check(BeastLevels.HeartsDropped==before+1 && map.listerThings.ThingsOfDef(BeastLevels.Heart).Any(t=>t.Position.InHorDistOf(pos,4f)),"[beast levels] an elite drops 兽王之心 on death");
   var tips=new[]{hd,hf,ha,BeastLevels.Get(abyss)}.Select(h=>h.TipStringExtra).Concat(new[]{eliteHediff.TipStringExtra});
   Check(tips.All(t=>!t.NullOrEmpty() && Chinese(t) && BlClean(t)),"[beast levels] elite tooltips are Chinese without dice wording");
  }

  // 5. 狂暴兽群：首领、首领倒下后其余散开。
  void BlPack(){
   BeastLevels.TestRoll=1f;BeastLevels.TestNoise=0;BeastLevels.TestVariant=BeastLevels.Frenzy;
   var list=new List<Pawn>();for(int i=0;i<4;i++)list.Add(BlAnimal("Wolf_Timber",null,false));
   list.Add(BlAnimal("Bear_Grizzly",null,false));
   int id=BeastLevels.Pack(list,900f);int cap=BeastLevels.Cap();
   var leader=list.Single(p=>BeastLevels.Get(p)?.leader==true);var hl=BeastLevels.Get(leader);
   int expect=BeastLevels.PackLevel(900f/5f,cap,0);
   Check(id!=0 && leader.def.defName=="Bear_Grizzly" && hl.elite>0 && hl.level==Mathf.Min(BeastLevels.MaxLevel,expect+BeastLevels.LeaderBonus) && list.All(p=>BeastLevels.Get(p)?.pack==id) && list.Where(p=>p!=leader).All(p=>BeastLevels.Get(p).level==expect && !BeastLevels.Get(p).leader),"[beast levels] pack: strongest animal leads (elite, level +3 = "+hl.level+"), others level "+expect);
   Check(leader.Name!=null && leader.LabelShort.StartsWith("首领") && leader.health.hediffSet.hediffs.OfType<Hediff_BeastElite>().First().Label.StartsWith("首领"),"[beast levels] the leader is labeled 首领: "+leader.LabelShort);
   BeastLevels.TestVariant=null;
   var c=CellFinder.RandomClosewalkCellNear(map.Center,map,15);
   foreach(var p in list){GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(c,map,4),map);p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true);}
   var comp=MapComp<MapComponent_BeastPacks>.Get(map);var pk=comp?.Find(id);
   Check(pk!=null && pk.leader==leader && pk.members.Count==4 && list.Where(p=>p!=leader).All(p=>p.MentalStateDef==MentalStateDefOf.ManhunterPermanent),"[beast levels] the pack is registered on the map with 4 manhunter followers");
   int broken=BeastLevels.PacksBroken,now=Find.TickManager.TicksGame;
   leader.Kill(null);
   var rest=list.Where(p=>p!=leader).ToList();
   Check(BeastLevels.PacksBroken==broken+1 && comp.Find(id)==null && rest.All(p=>!p.Dead && p.MentalStateDef!=MentalStateDefOf.ManhunterPermanent && p.MentalStateDef!=MentalStateDefOf.Manhunter && p.mindState.exitMapAfterTick<=now) && rest.All(p=>!p.mindState.mentalStateHandler.neverFleeIndividual),"[beast levels] when the leader dies the rest stop hunting and flee the map ("+string.Join(",",rest.Select(p=>p.MentalStateDef?.defName??"none"))+")");
  }

  // 6. 驯兽成长：经验、上限、动物伙伴、超上限保留。
  void BlGrowth(){
   BeastLevels.TestRoll=1f;BeastLevels.TestNoise=0;Adventurer.TestSuppressed=false;
   var master=SharedPawn();blMade.Add(master);var hm=Adventurer.TryAdd(master,5,true);if(hm!=null)hm.level=5;
   var pet=BlAnimal("Wolf_Timber",Faction.OfPlayer);var hp=BeastLevels.Get(pet);
   pet.training.Train(TrainableDefOf.Obedience,master,true);pet.playerSettings.Master=master;
   Check(hm!=null && hm.level==5 && hp!=null && hp.level==1 && BeastLevels.GrowthCap(pet)==5,"[beast levels] a player animal starts at level 1; its cap is the master's adventurer level (5): master "+hm?.level+" pet "+hp?.level+" cap "+BeastLevels.GrowthCap(pet));
   bool up=hp.GainXp(100f);
   Check(up && hp.level==2 && Near(hp.xp,0f) && Near(BeastLevels.XpToNext(2),200f),"[beast levels] 100×N XP per level: 100 XP takes level 1 → 2");
   hp.GainXp(100000f);
   Check(hp.level==5 && !hp.GainXp(1000f) && hp.level==5,"[beast levels] growth stops at the master's level");
   int old=hm.level;hm.level=30;int hard=BeastLevels.GrowthCap(pet);hm.level=old;
   Check(hard==BeastLevels.TameCap,"[beast levels] hard cap 20 even under a legendary master");
   pet.playerSettings.Master=null;BeastLevels.ForgetColony();
   Check(BeastLevels.GrowthCap(pet)==Mathf.Clamp(AdventurerEnemies.ColonyMaxLevel(),1,20),"[beast levels] without a master the cap is the colony max adventurer level");
   pet.playerSettings.Master=master;
   // 战斗经验：近战伤害多少给多少。
   hm.level=20;float x0=hp.xp;var dummy=BlAnimal("Muffalo",null,true,pet.Position+IntVec3.North);var res=MeleeHit(BeastVerb(pet,false),dummy);
   Check(res.totalDamageDealt>0f && hp.level==5 && Near(hp.xp-x0,res.totalDamageDealt),"[beast levels] melee damage dealt by a player animal becomes XP ("+res.totalDamageDealt+")");
   Check(Near(BeastLevels.JobXp(JobDefOf.HaulToCell),BeastLevels.HaulXp) && Near(BeastLevels.JobXp(JobDefOf.Rescue),BeastLevels.RescueXp) && BeastLevels.JobXp(JobDefOf.Wait)==0f,"[beast levels] hauling 5 XP, rescue 20 XP, idle jobs none");
   // 动物伙伴：游侠皮甲。
   float f0=BeastLevels.XpFactor(pet);var armor=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_RangerLeathers"),GenStuff.DefaultStuffFor(DefDatabase<ThingDef>.GetNamed("LP_RangerLeathers")));master.apparel.Wear((Apparel)armor,false,true);
   float f1=BeastLevels.XpFactor(pet);
   Check(Near(f0,1f) && Near(f1,1.25f) && hp.TipStringExtra.Contains("动物伙伴"),"[beast levels] 动物伙伴: a ranger master gives +25% animal XP and the tooltip says so");
   // 驯服的高等级野兽保留等级，超过上限不再成长。
   hm.level=5;var tamed=BlAnimal();var ht=BeastLevels.Get(tamed);ht.level=12;ht.Dirty();tamed.SetFaction(Faction.OfPlayer);tamed.training.Train(TrainableDefOf.Obedience,master,true);tamed.playerSettings.Master=master;
   Check(ht.level==12 && !ht.GainXp(5000f) && ht.level==12 && ht.TipStringExtra.Contains("追上来"),"[beast levels] a tamed level-12 animal keeps its level above the cap and waits for the cap");
   var born=BlAnimal("Wolf_Timber",Faction.OfPlayer);
   Check(BeastLevels.Get(born)?.level==1 && BeastLevels.Get(born).elite==0,"[beast levels] a player-faction animal (born in the colony) starts at level 1 with no elite");
   // 兽王护符：驯兽 +2。
   int apt0=master.skills.GetSkill(SkillDefOf.Animals).Aptitude;master.apparel.Wear((Apparel)ThingMaker.MakeThing(BeastLevels.Amulet),false,true);int apt1=master.skills.GetSkill(SkillDefOf.Animals).Aptitude;
   Check(apt1==apt0+2,"[beast levels] 兽王护符 gives 驯兽 +2 ("+apt0+" → "+apt1+")");
  }

  // 7. Isekai 接管、旧存档补等级。
  void BlMisc(){
   BeastLevels.TestRoll=1f;BeastLevels.TestNoise=0;
   var w=BlAnimal();var h=BeastLevels.Get(w);h.level=10;h.Dirty();
   Check(!BeastLevels.IsekaiRanked(w) && h.Applies,"[beast levels] without Isekai our multipliers apply");
   Traverse.Create(h).Field("isekai").SetValue(1);h.Dirty();
   Check(!h.Applies && BeastLevels.PartHealthFactor(w)==1f && BlFac(h,StatDefOf.MeleeDamageFactor)==1f && h.TipStringExtra.Contains("已由异世界等级接管"),"[beast levels] with Isekai mob ranks: display only, tooltip says 已由异世界等级接管");
   BeastLevels.TestSuppressed=true;var old=BlAnimal();BeastLevels.TestSuppressed=false;
   var comp=MapComp<MapComponent_BeastPacks>.Get(map);var tr=Traverse.Create(comp).Field("initVersion");tr.SetValue(0);comp.FinalizeInit();
   int after=tr.GetValue<int>();var ho=BeastLevels.Get(old);
   tr.SetValue(0);comp.FinalizeInit();int count=old.health.hediffSet.hediffs.Count(x=>x is Hediff_BeastLevel);
   Check(ho!=null && after==MapComponent_BeastPacks.InitVersion && count==1,"[beast levels] old saves: animals without a level get one once when the map loads");
  }

  void PrepareBeastLevelsSave(){
   map=Find.CurrentMap;var s=LichMod.Settings;bool oSup=BeastLevels.TestSuppressed,oOn=s.beastLevels;BeastLevels.TestSuppressed=false;s.beastLevels=true;
   try{
    BeastLevels.TestRoll=1f;BeastLevels.TestNoise=0;BeastLevels.TestVariant=BeastLevels.Dread;
    var list=new List<Pawn>{BlAnimal("Wolf_Timber",null,false),BlAnimal("Wolf_Timber",null,false)};
    int id=BeastLevels.Pack(list,450f);foreach(var p in list)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,12),map);
    var leader=list.First(p=>BeastLevels.Get(p).leader);var h=BeastLevels.Get(leader);
    blSavedId=leader.ThingID;blSavedLevel=h.level;blSavedElite=h.elite;blSavedPack=id;
   }finally{BeastLevels.TestSuppressed=oSup;s.beastLevels=oOn;BeastLevels.TestRoll=null;BeastLevels.TestNoise=null;BeastLevels.TestVariant=null;blMade.Clear();}
  }
  void CheckBeastLevelsAfterLoad(){
   map=Find.CurrentMap;var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==blSavedId);var h=BeastLevels.Get(p);
   var pk=MapComp<MapComponent_BeastPacks>.Get(map)?.Find(blSavedPack);
   Check(h!=null && h.level==blSavedLevel && h.elite==blSavedElite && h.leader && h.pack==blSavedPack && pk!=null && pk.leader==p && pk.members.Count==1,"[beast levels] level, elite, leader and pack survive save/load");
   foreach(var m in pk.members.ToList())m.Destroy();p.Destroy();
  }
 }
}
