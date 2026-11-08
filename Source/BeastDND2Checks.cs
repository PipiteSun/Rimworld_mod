using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 // 1.127.0 DND 野兽第二批的检查：-Only BeastsDND2。每种动物一两条：能生成（贴图、组件、野性等级）、野外权重、招牌机制。
 // 生成 / 清理借用 WildMonsterChecks 的 WmMonster / WmHuman / WmDone。
 public partial class LichChecks {
  static readonly Dictionary<string,(float size,string train,bool predator,bool manhunter,string biome)> bdStats=new Dictionary<string,(float,string,bool,bool,string)>{
   {"LP_Pseudodrake",(.35f,"Intermediate",true,false,"TropicalRainforest")},
   {"LP_Tressym",(.32f,"None",true,false,"TemperateForest")},
   {"LP_FireBeetle",(.6f,"None",false,false,"Desert")},
   {"LP_Rothe",(1.8f,"None",false,true,"Tundra")},
   {"LP_Hippogriff",(2f,"Intermediate",false,true,"BorealForest")},
   {"LP_AxeBeak",(1.1f,"None",false,true,"AridShrubland")},
   {"LP_Griffon",(2.2f,"Advanced",true,true,"TemperateForest")},
   {"LP_HookHorror",(1.8f,"Intermediate",true,true,"Tundra")},
   {"LP_PhaseSpider",(1.1f,"Intermediate",true,true,"TropicalRainforest")},
   {"LP_Darkmantle",(.5f,"None",true,true,"BorealForest")},
   {"LP_Stirge",(.2f,"None",true,true,"TemperateSwamp")}};

  Pawn BdEnemy(IntVec3 at){
   var enemy=Find.FactionManager.AllFactions.FirstOrDefault(f=>!f.IsPlayer && !f.Hidden && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer));if(enemy==null)return null;
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(enemy.def.basicMemberKind??PawnKindDefOf.Villager,enemy,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forceNoBackstory:true,fixedBiologicalAge:30,fixedChronologicalAge:30));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_MissingPart).ToList())p.health.RemoveHediff(h);
   GenSpawn.Spawn(p,at,map);wmMade.Add(p);return p;
  }

  void CheckBeastsDND2(){
   bool wild=LichMod.Settings.dndBeastsWild;
   try{BdDefs();BdSettings();BdPseudodrake();BdTressym();BdRothe();BdHippogriff();BdAxeBeak();BdGriffon();BdHookHorror();BdPhaseSpider();BdDarkmantle();BdStirge();}
   finally{LichMod.Settings.dndBeastsWild=wild;Beasts.ApplySettings();BeastsDND2.TestRoll=null;Legendary.TestD20=null;WmDone();}
  }

  void BdDefs(){
   foreach(var pair in bdStats){
    var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail(pair.Key);var race=DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);var s=pair.Value;
    bool ok=kind!=null && race!=null && kind.race==race && race.race.Animal;
    Check(ok && !kind.ConfigErrors().Any() && !race.ConfigErrors().Any() && Chinese(race.label) && Chinese(kind.label) && Chinese(race.description),pair.Key+" loads as an animal with a Chinese label and clean definitions: "+(ok?string.Join(";",kind.ConfigErrors().Concat(race.ConfigErrors())):"missing"));
    if(!ok)continue;
    bool tex=kind.lifeStages.All(st=>st.bodyGraphicData.texPath.StartsWith("Things/Pawn/Animal/LP_BeastsDND2/") && new[]{"_south","_east","_north"}.All(r=>ContentFinder<Texture2D>.Get(st.bodyGraphicData.texPath+r,false)!=null) && new[]{"_south","_east","_north"}.All(r=>ContentFinder<Texture2D>.Get(st.dessicatedBodyGraphicData.texPath+r,false)!=null));
    float w=race.GetStatValueAbstract(StatDefOf.Wildness);
    Check(tex && Near(race.race.baseBodySize,s.size) && race.race.trainability?.defName==s.train && race.race.predator==s.predator && w<1f && BeastsDND2.DefaultManhunter(kind)==s.manhunter && BiomeDef.Named(s.biome).CommonalityOfAnimal(kind)>0f,
     pair.Key+": own textures "+tex+", size "+race.race.baseBodySize+", trainability "+race.race.trainability?.defName+", wildness "+w+", manhunter "+kind.canArriveManhunter+", wild in "+s.biome);
    var p=WmMonster(pair.Key,OpenCell());p.Drawer.renderer.EnsureGraphicsInitialized();
    bool gfx=p.Drawer.renderer.BodyGraphic!=null && new[]{Rot4.North,Rot4.East,Rot4.South,Rot4.West}.All(r=>p.Drawer.renderer.BodyGraphic.MatAt(r)!=BaseContent.BadMat);
    Check(gfx && BeastsDND2.Kind(p)==pair.Key.Substring(3) && BeastLevels.Eligible(p) && !Dragons.Is(p) && !Beasts.IsBeastKind(kind) && !WildMonsters.IsMonsterKind(kind),
     pair.Key+" spawns with its graphic ("+gfx+"), component ("+BeastsDND2.Kind(p)+") and is eligible for wild levels ("+BeastLevels.Eligible(p)+")");
    p.Destroy();
   }
   var beetle=WmMonster("LP_FireBeetle",OpenCell());
   Check(beetle.GetComp<CompGlower>()?.Glows==true && beetle.def.GetCompProperties<CompProperties_Milkable>()?.milkDef==ThingDefOf.Chemfuel && DefDatabase<ThingDef>.GetNamedSilentFail("LP_Hippogriff").GetStatValueAbstract(StatDefOf.CaravanRidingSpeedFactor)>1f && DefDatabase<ThingDef>.GetNamedSilentFail("LP_Hippogriff").race.packAnimal,"fire beetle glows and yields chemfuel; hippogriff is a rideable pack animal");
   beetle.Destroy();
   foreach(var k in bdStats.Keys.Select(n=>DefDatabase<PawnKindDef>.GetNamed(n)).Where(k=>k.race.race.packAnimal))Check(new[]{"_south","_east","_north"}.All(r=>ContentFinder<Texture2D>.Get(k.lifeStages.Last().bodyGraphicData.texPath+"Pack"+r,false)!=null),k.defName+" pack animal has its saddlebag texture");
   foreach(var h in new[]{"LP_PseudoSleep","LP_Hooked","LP_Darkmantled","LP_AxeBeakSprint","LP_RotheRage"})Check(Items.Hediff(h)!=null && Chinese(Items.Hediff(h).label) && !Items.Hediff(h).ConfigErrors().Any(),h+" hediff loads");
  }

  void BdSettings(){
   var rothe=DefDatabase<PawnKindDef>.GetNamed("LP_Rothe");var griffon=DefDatabase<PawnKindDef>.GetNamed("LP_Griffon");var forest=BiomeDef.Named("TemperateForest");
   LichMod.Settings.dndBeastsWild=false;Beasts.ApplySettings();
   bool off=forest.CommonalityOfAnimal(rothe)==0f && !griffon.canArriveManhunter;
   LichMod.Settings.dndBeastsWild=true;Beasts.ApplySettings();
   Check(off && forest.CommonalityOfAnimal(rothe)>0f && griffon.canArriveManhunter && !DefDatabase<PawnKindDef>.GetNamed("LP_Pseudodrake").canArriveManhunter,"「野外刷新 DND 野兽」 off zeroes wild weight and manhunter packs for the new beasts, on restores them");
  }

  void BdPseudodrake(){
   var cell=OpenCell();var pd=WmMonster("LP_Pseudodrake",cell);var man=WmHuman(cell+IntVec3.East);
   Legendary.TestD20=1;MeleeHit(WmVerb(pd,"tail sting"),man);
   bool slept=Items.Has(man,"LP_PseudoSleep") && man.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness)<=.11f && BeastsDND2.Comp(pd).stingReadyTick>Find.TickManager.TicksGame;
   var big=WmMonster("LP_Griffon",cell+IntVec3.West);
   Check(slept && !BeastsDND2.Sting(pd,big) && !Items.Has(big,"LP_PseudoSleep"),"pseudodragon tail sting puts a failed-save human to sleep (cooldown starts) but not a griffon-sized target: verb "+(WmVerb(pd,"tail sting")!=null)+", hediff "+Items.Has(man,"LP_PseudoSleep")+", consciousness "+man.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness)+", cd "+BeastsDND2.Comp(pd).stingReadyTick+", tools "+string.Join("/",pd.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb.tool?.label)));
   Legendary.TestD20=null;
  }

  void BdTressym(){
   var cell=OpenCell();var cat=WmMonster("LP_Tressym",cell,Faction.OfPlayer);var foe=BdEnemy(cell+IntVec3.East*4);
   if(foe==null){Check(false,"tressym check needs a hostile faction");return;}
   foe.health.AddHediff(DefDatabase<HediffDef>.GetNamed("PsychicInvisibility"));
   bool hidden=InvisibilityUtility.IsPsychologicallyInvisible(foe);
   BeastsDND2.Comp(cat).CompTickInterval(BeastsDND2.TressymInterval);
   Check(hidden && !InvisibilityUtility.IsPsychologicallyInvisible(foe),"tamed tressym reveals an invisible enemy 4 cells away (was hidden "+hidden+")");
  }

  void BdRothe(){
   var cell=OpenCell();var a=WmMonster("LP_Rothe",cell);var b=WmMonster("LP_Rothe",cell+IntVec3.North*2);var c=WmMonster("LP_Rothe",cell+IntVec3.South*2);var man=WmHuman(cell+IntVec3.East*3);
   a.TakeDamage(new DamageInfo(DamageDefOf.Blunt,3,0f,-1f,man));
   Check(Items.Has(b,"LP_RotheRage") && Items.Has(c,"LP_RotheRage") && b.MentalStateDef==MentalStateDefOf.Manhunter && c.MentalStateDef==MentalStateDefOf.Manhunter,"hurting a wild rothé enrages its herd (群牛之怒, manhunter)");
  }

  void BdHippogriff(){
   var cell=OpenCell();var h=WmMonster("LP_Hippogriff",cell);var man=WmHuman(cell+IntVec3.East);
   BeastsDND2.TestRoll=0f;h.TakeDamage(new DamageInfo(DamageDefOf.Blunt,3,0f,-1f,man));BeastsDND2.TestRoll=null;
   int d=man.Position.x-cell.x;
   Check(d>=2 && man.stances.stunner.Stunned && BeastsDND2.Comp(h).buffetReadyTick>Find.TickManager.TicksGame,"hippogriff wing buffet knocks a melee attacker back (now "+d+" cells) and stuns it");
  }

  void BdAxeBeak(){
   var b=WmMonster("LP_AxeBeak",OpenCell());
   b.TakeDamage(new DamageInfo(DamageDefOf.Cut,1,0f));bool early=Items.Has(b,"LP_AxeBeakSprint");
   var torso=b.RaceProps.body.corePart;
   for(int i=0;i<30 && !b.Dead && b.health.summaryHealth.SummaryHealthPercent>=BeastsDND2.SprintHealth;i++)b.TakeDamage(new DamageInfo(DamageDefOf.Cut,4,0f,-1f,null,torso));
   Check(!early && !b.Dead && Items.Has(b,"LP_AxeBeakSprint") && b.GetStatValue(StatDefOf.MoveSpeed)>0f,"axe beak sprints only once badly hurt (health "+b.health.summaryHealth.SummaryHealthPercent.ToStringPercent()+")");
  }

  void BdGriffon(){
   var cell=OpenCell();var g=WmMonster("LP_Griffon",cell);var man=WmHuman(cell+IntVec3.East);var far=WmHuman(cell+IntVec3.West*6);
   Legendary.TestD20=1;float before=man.health.summaryHealth.SummaryHealthPercent;bool stun=BeastsDND2.DiveLand(g,man);Legendary.TestD20=null;
   Check(stun && man.stances.stunner.Stunned && man.health.summaryHealth.SummaryHealthPercent<before && BeastsDND2.InDiveRange(g,far) && !BeastsDND2.InDiveRange(g,man) && DefDatabase<ThingDef>.GetNamedSilentFail("LP_GriffonFlyer")?.thingClass==typeof(LP_GriffonFlyer),"griffon dive lands with a cut and a stun; dive range 4–10 cells");
  }

  void BdHookHorror(){
   var cell=OpenCell();var hh=WmMonster("LP_HookHorror",cell);var man=WmHuman(cell+IntVec3.East);
   float speed=man.GetStatValue(StatDefOf.MoveSpeed);
   Legendary.TestD20=1;MeleeHit(WmVerb(hh,"hooks"),man);Legendary.TestD20=null;
   Check(Items.Has(man,"LP_Hooked") && man.GetStatValue(StatDefOf.MoveSpeed)<speed*.5f,"hook horror hooks a failed-save target (move "+speed.ToString("0.0")+"→"+man.GetStatValue(StatDefOf.MoveSpeed).ToString("0.0")+")");
  }

  void BdPhaseSpider(){
   var cell=OpenCell();var sp=WmMonster("LP_PhaseSpider",cell);var man=WmHuman(cell+IntVec3.East*6);
   Check(BeastsDND2.InPhaseRange(sp,man) && BeastsDND2.Phase(sp,man) && sp.Position.AdjacentTo8WayOrInside(man.Position),"phase spider steps through the ethereal plane next to a target 6 cells away");
  }

  void BdDarkmantle(){
   var cell=OpenCell();var dm=WmMonster("LP_Darkmantle",cell);var man=WmHuman(cell+IntVec3.East);var other=WmHuman(cell+IntVec3.West);
   Legendary.TestD20=1;BeastsDND2.TestRoll=0f;MeleeHit(WmVerb(dm,"tentacles"),man);
   bool miss=BeastsDND2.Shroud(dm,other,.9f);
   BeastsDND2.TestRoll=null;Legendary.TestD20=null;
   Check(Items.Has(man,"LP_Darkmantled") && man.health.capacities.GetLevel(PawnCapacityDefOf.Sight)<=.21f && !miss && !Items.Has(other,"LP_Darkmantled"),"darkmantle shrouds a target's head (sight "+man.health.capacities.GetLevel(PawnCapacityDefOf.Sight).ToStringPercent()+"); a failed 35% roll does nothing");
  }

  void BdStirge(){
   var cell=OpenCell();var st=WmMonster("LP_Stirge",cell);var man=WmHuman(cell+IntVec3.East);
   if(st.needs?.food!=null)st.needs.food.CurLevel=.1f;
   var verb=WmVerb(st,"proboscis");
   for(int i=0;i<BeastsDND2.DrainsToFull;i++)MeleeHit(verb,man);
   var loss=man.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
   Check(loss!=null && loss.Severity>=BeastsDND2.DrainBlood*2.5f && BeastsDND2.Comp(st).drains==0 && (st.needs?.food==null || st.needs.food.CurLevelPercentage>.95f),"stirge drains blood three times ("+(loss?.Severity??0f).ToString("0.00")+" blood loss) and is then full");
  }
 }
}
