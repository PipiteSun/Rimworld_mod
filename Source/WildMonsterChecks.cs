using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 // DND 怪物第二批的检查：-Only WildMonsters。
 public partial class LichChecks {
  static readonly Dictionary<string,(float size,float health,float power,string train,bool predator,string leather,string[] biomes)> wmStats=new Dictionary<string,(float,float,float,string,bool,string,string[])>{
   {"LP_RustMonster",(1f,1.3f,85,"Intermediate",false,"Leather_Lizard",new[]{"TemperateForest","BorealForest","AridShrubland","Desert"})},
   {"LP_GelatinousCube",(2.2f,3.5f,150,"None",true,null,new[]{"TemperateSwamp","TropicalSwamp","ColdBog"})},
   {"LP_Basilisk",(1.7f,2f,170,"Intermediate",true,"Leather_Lizard",new[]{"TropicalRainforest","AridShrubland","Desert"})},
   {"LP_Manticore",(1.9f,2.2f,210,"Intermediate",true,"Leather_Panthera",new[]{"TemperateForest","AridShrubland","Desert","ExtremeDesert"})},
   {"LP_Bulette",(2.8f,3.2f,280,"None",true,"LP_Leather_Bulette",new[]{"TemperateForest","BorealForest","Tundra","AridShrubland"})},
   {"LP_CarrionCrawler",(1.4f,1.8f,130,"Intermediate",true,null,new[]{"TemperateSwamp","TropicalSwamp","ColdBog"})}};
  readonly List<Thing> wmMade=new List<Thing>();
  static string wmMantId,wmCubeId,wmVictimId;static int wmVolleyTick,wmLeft;

  Pawn WmMonster(string kind,IntVec3 at,Faction faction=null){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(kind),faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,fixedBiologicalAge:4f,fixedChronologicalAge:4f));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h.def==HediffDefOf.Pregnant).ToList())p.health.RemoveHediff(h);
   GenSpawn.Spawn(p,at,map);wmMade.Add(p);return p;
  }
  Pawn WmHuman(IntVec3 at){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:30,fixedChronologicalAge:30));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_MissingPart).ToList())p.health.RemoveHediff(h);
   p.equipment?.DestroyAllEquipment();p.apparel?.DestroyAll();
   GenSpawn.Spawn(p,at,map);if(p.drafter!=null)p.drafter.Drafted=true;wmMade.Add(p);return p;
  }
  static Verb_MeleeAttackDamage WmVerb(Pawn p,string tool)=>p.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().FirstOrDefault(v=>tool==null || v.tool?.label==tool);
  static float InjuryCount(Pawn p,string def)=>p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury && h.def.defName==def).Sum(h=>h.Severity);
  void WmDone(){foreach(var t in wmMade)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}wmMade.Clear();}
  void WmClear(Pawn p){foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury).ToList())p.health.RemoveHediff(h);p.stances?.stunner?.StopStun();}

  void CheckWildMonsters(){
   bool wild=LichMod.Settings.dndMonstersWild;
   try{WmDefs();WmBiomes();WmRust();WmCube();WmBasilisk();WmManticore();WmBulette();WmCrawler();WmMythal();}
   finally{
    LichMod.Settings.dndMonstersWild=wild;Beasts.ApplySettings();WildMonsters.TestRoll=null;Legendary.TestD20=null;
    foreach(var t in wmMade)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    wmMade.Clear();
   }
  }

  void WmDefs(){
   foreach(var pair in wmStats){
    var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail(pair.Key);var race=DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);var s=pair.Value;
    Check(kind!=null && race!=null && kind.race==race && race.race.Animal,pair.Key+" thing and pawn kind load as an animal");
    Check(!kind.ConfigErrors().Any() && !race.ConfigErrors().Any(),pair.Key+" definitions: "+string.Join(";",kind.ConfigErrors().Concat(race.ConfigErrors())));
    Check(Chinese(race.label) && Chinese(kind.label) && Chinese(race.description),pair.Key+" has a Chinese label and description: "+race.label);
    Check(race.GetCompProperties<CompProperties_WildMonster>()!=null && WildMonsters.IsMonsterKind(kind) && !Beasts.IsBeastKind(kind),pair.Key+" carries the wild monster component");
    foreach(var stage in kind.lifeStages){
     var g=stage.bodyGraphicData;
     Check(g!=null && g.texPath.StartsWith("Things/Pawn/Animal/LP_WildMonsters/") && new[]{"_south","_east","_north"}.All(r=>ContentFinder<Texture2D>.Get(g.texPath+r,false)!=null),pair.Key+" life stage uses the mod's own drawn texture: "+g?.texPath);
     Check(new[]{"_south","_east","_north"}.All(r=>ContentFinder<Texture2D>.Get(stage.dessicatedBodyGraphicData.texPath+r,false)!=null),pair.Key+" dessicated texture resolves: "+stage.dessicatedBodyGraphicData.texPath);
    }
    Check(Near(race.race.baseBodySize,s.size) && Near(race.race.baseHealthScale,s.health) && Near(kind.combatPower,s.power),pair.Key+" body size, health scale and combat power: "+race.race.baseBodySize+"/"+race.race.baseHealthScale+"/"+kind.combatPower);
    float w=race.GetStatValueAbstract(StatDefOf.Wildness);
    Check(w<1f && race.race.trainability?.defName==s.train && race.race.predator==s.predator,pair.Key+" wildness "+w+", trainability "+race.race.trainability?.defName+", predator "+race.race.predator);
    Check(race.race.leatherDef?.defName==s.leather && race.race.meatDef!=null && race.race.corpseDef!=null && kind.canArriveManhunter,pair.Key+" leather "+(race.race.leatherDef?.defName??"none")+", meat, corpse, can arrive as manhunters");
    var p=WmMonster(pair.Key,OpenCell());p.Drawer.renderer.EnsureGraphicsInitialized();
    Check(p.Drawer.renderer.BodyGraphic!=null && new[]{Rot4.North,Rot4.East,Rot4.South,Rot4.West}.All(r=>p.Drawer.renderer.BodyGraphic.MatAt(r)!=BaseContent.BadMat),pair.Key+" graphic resolves in all directions");
    Check(WildMonsters.Comp(p)!=null && WildMonsters.Kind(p)==pair.Key.Substring(3),pair.Key+" spawned pawn has its component");
    p.Destroy();
   }
   var cube=DefDatabase<PawnKindDef>.GetNamed("LP_GelatinousCube");
   Check(cube.race.race.body.defName=="LP_GelCubeBody" && cube.lifeStages.All(s=>s.bodyGraphicData.shaderType==ShaderTypeDefOf.Transparent) && !cube.race.race.hasGenders,"gelatinous cube: own body, translucent shader, no genders");
   var c=WmMonster("LP_GelatinousCube",OpenCell());
   Check(!c.Downed && c.health.capacities.GetLevel(PawnCapacityDefOf.Moving)>.9f && c.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness)>.9f && c.health.capacities.GetLevel(PawnCapacityDefOf.Breathing)>.9f,"gelatinous cube body supports moving, consciousness and breathing");
   c.Destroy();
   var leather=DefDatabase<ThingDef>.GetNamed("LP_Leather_Bulette");var plain=DefDatabase<ThingDef>.GetNamed("Leather_Plain");
   Check(leather.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Sharp)>plain.GetStatValueAbstract(StatDefOf.StuffPower_Armor_Sharp)*1.5f && Chinese(leather.label) && ContentFinder<Texture2D>.Get("Things/Item/Resource/LP_BuletteHide",false)!=null,"bulette hide is a tough armor leather with its own icon");
   Check(ContentFinder<Texture2D>.Get("Things/Projectile/LP_ManticoreSpike",false)!=null && WildMonsters.SpikeDef?.projectile?.damageDef==DamageDefOf.Stab && DefDatabase<ThingDef>.GetNamedSilentFail("LP_BuletteFlyer")?.thingClass==typeof(LP_BuletteFlyer),"manticore spike projectile and bulette flyer load");
   Check(Items.Hediff("LP_BasiliskGaze").stages.Count==2 && Items.Hediff("LP_CrawlerToxin").stages.Count==2,"stiffness and toxin have two stages ("+Items.Hediff("LP_BasiliskGaze").stages.Count+"/"+Items.Hediff("LP_CrawlerToxin").stages.Count+")");
   foreach(var h in new[]{"LP_Engulfed","LP_BasiliskGaze","LP_BasiliskPetrified","LP_CrawlerToxin","LP_CrawlerParalysis"})Check(Items.Hediff(h)!=null && Chinese(Items.Hediff(h).label) && !Items.Hediff(h).ConfigErrors().Any(),h+" hediff loads");
  }

  void WmBiomes(){
   foreach(var pair in wmStats){
    var kind=DefDatabase<PawnKindDef>.GetNamed(pair.Key);
    foreach(var b in pair.Value.biomes){var biome=DefDatabase<BiomeDef>.GetNamed(b);float c=biome.CommonalityOfAnimal(kind);Check(c>0f && c<=.03f && biome.AllWildAnimals.Contains(kind),pair.Key+" spawns rarely in "+b+" ("+c+")");}
   }
   LichMod.Settings.dndMonstersWild=false;Beasts.ApplySettings();
   foreach(var pair in wmStats){
    var kind=DefDatabase<PawnKindDef>.GetNamed(pair.Key);
    Check(pair.Value.biomes.All(b=>DefDatabase<BiomeDef>.GetNamed(b).CommonalityOfAnimal(kind)==0f) && !kind.canArriveManhunter,pair.Key+" stops spawning and manhunting when the setting is off");
   }
   Check(DefDatabase<BiomeDef>.GetNamed("TemperateForest").CommonalityOfAnimal(DefDatabase<PawnKindDef>.GetNamed("LP_Owlbear"))>0f,"turning the monsters off leaves the 1.25.0 beasts alone");
   LichMod.Settings.dndMonstersWild=true;Beasts.ApplySettings();
   Check(wmStats.Keys.All(k=>DefDatabase<PawnKindDef>.GetNamed(k).canArriveManhunter),"turning the monsters back on restores manhunter packs");
  }

  void WmRust(){
   var cell=OpenCell();var rust=WmMonster("LP_RustMonster",cell);var man=WmHuman(cell+IntVec3.East);
   var sword=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword"),ThingDefOf.Steel);man.equipment.AddEquipment(sword);
   var armor=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_PlateArmor"),ThingDefOf.Steel);man.apparel.Wear(armor);
   var shirt=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_BasicShirt"),DefDatabase<ThingDef>.GetNamed("Cloth"));man.apparel.Wear(shirt);
   Check(WildMonsters.IsMetal(sword) && WildMonsters.IsMetal(armor) && !WildMonsters.IsMetal(shirt) && WildMonsters.IsMetal(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Autopistol"))),"metal detection: steel sword, steel plate armor and stuffless gun are metal, cloth shirt is not");
   int sw=sword.HitPoints,ar=armor.HitPoints,sh=shirt.HitPoints;
   Check(!WildMonsters.Corrode(shirt,.5f,man) && shirt.HitPoints==sh,"cloth shirt does not rust");
   WildMonsters.TestRoll=0f;MeleeHit(WmVerb(rust,"antennae"),man);WildMonsters.TestRoll=null;
   Check(sword.HitPoints==sw-Mathf.RoundToInt(sword.MaxHitPoints*.1f) && armor.HitPoints<=ar-Mathf.RoundToInt(armor.MaxHitPoints*.1f),"rust touch: sword "+sw+"→"+sword.HitPoints+", armor "+ar+"→"+armor.HitPoints);
   sw=sword.HitPoints;
   rust.TakeDamage(new DamageInfo(DamageDefOf.Cut,1,0f,-1f,man,null,sword.def));
   Check(sword.HitPoints==sw-Mathf.Max(1,Mathf.RoundToInt(sword.MaxHitPoints*.05f)),"hitting a rust monster with a metal blade corrodes it: "+sw+"→"+sword.HitPoints);
   var club=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Club"),ThingDefOf.WoodLog);
   Check(!WildMonsters.Corrode(club,.5f,man) && club.HitPoints==club.MaxHitPoints,"wooden club does not rust");
   sword.HitPoints=1;MeleeHit(WmVerb(rust,"mandibles")??WmVerb(rust,null),man);
   Check(sword.Destroyed && man.equipment.Primary==null,"a nearly rusted-through sword crumbles away");
   WmDone();
  }

  void WmCube(){
   var cell=OpenCell();var cube=WmMonster("LP_GelatinousCube",cell);var man=WmHuman(cell+IntVec3.East);var comp=WildMonsters.Comp(cube);
   WildMonsters.TestRoll=.5f;MeleeHit(WmVerb(cube,null),man);
   Check(!Items.Has(man,"LP_Engulfed"),"engulf fails above 35%");
   WildMonsters.TestRoll=0f;MeleeHit(WmVerb(cube,null),man);WildMonsters.TestRoll=null;
   var h=man.health.hediffSet.hediffs.OfType<Hediff_Engulfed>().FirstOrDefault();
   Check(h!=null && h.cube==cube && man.stances.stunner.Stunned && !comp.EngulfReady,"melee hit engulfs the target, stuns it and starts the cooldown");
   float acid=InjuryCount(man,"AcidBurn");Step(130);
   Check(Items.Has(man,"LP_Engulfed") && InjuryCount(man,"AcidBurn")>acid && man.stances.stunner.Stunned,"engulfed target takes acid damage every second and stays stunned (engulfed "+Items.Has(man,"LP_Engulfed")+", acid "+acid+"→"+InjuryCount(man,"AcidBurn")+", stunned "+man.stances.stunner.Stunned+", cube "+cube.Position+"/"+cube.Downed+", man "+man.Position+")");
   var man2=WmHuman(cell+IntVec3.West);WildMonsters.TestRoll=0f;Check(!WildMonsters.TryEngulf(cube,man2,0f),"cube cannot engulf again while on cooldown");
   comp.engulfReadyTick=-1;var big=WmMonster("LP_Bulette",cell+IntVec3.North*3);
   Check(!WildMonsters.TryEngulf(cube,big,0f),"cube cannot engulf something bigger than itself");WildMonsters.TestRoll=null;
   float hp=cube.health.summaryHealth.SummaryHealthPercent;cube.TakeDamage(new DamageInfo(WildMonsters.Acid,10,0f,-1f,man));
   Check(Near(cube.health.summaryHealth.SummaryHealthPercent,hp),"gelatinous cube is immune to acid");
   Races124.Blink(cube,cell+IntVec3.West*4);Step(70);
   Check(Items.Has(man,"LP_Engulfed") && man.Position.AdjacentTo8WayOrInside(cube.Position),"the engulfed target is carried along inside the cube");
   cube.Kill(null);Step(2);
   Check(!Items.Has(man,"LP_Engulfed"),"killing the cube frees the target");
   WmDone();
  }

  void WmBasilisk(){
   var cell=OpenCell();var b=WmMonster("LP_Basilisk",cell);var man=WmHuman(cell+IntVec3.East*4);
   Check(WildMonsters.GazeAll(b)==0 && !Items.Has(man,"LP_BasiliskGaze"),"a calm basilisk gazes at no one");
   b.mindState.enemyTarget=man;
   string why="target "+(WildMonsters.CombatTarget(b,WildMonsters.GazeRadius)!=null)+", awake "+man.Awake()+", sight "+man.health.capacities.GetLevel(PawnCapacityDefOf.Sight)+", los "+GenSight.LineOfSight(b.Position,man.Position,map,true)+", basilisk downed "+b.Downed;
   for(int i=0;i<4;i++)WildMonsters.GazeAll(b);
   var h=man.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_BasiliskGaze"));
   Check(h!=null && Near(h.Severity,.8f) && h.def.stages.Count==2,"four gazes stiffen the target to 80% ("+h?.Severity+"; "+why+")");
   Legendary.TestD20=20;WildMonsters.GazeAll(b);
   Check(Near(h.Severity,.5f) && !Items.Has(man,"LP_BasiliskPetrified"),"a successful constitution save at full stiffness falls back to half");
   Legendary.TestD20=1;WildMonsters.GazeAll(b);WildMonsters.GazeAll(b);WildMonsters.GazeAll(b);Legendary.TestD20=null;
   Check(Items.Has(man,"LP_BasiliskPetrified") && !Items.Has(man,"LP_BasiliskGaze") && man.Downed && Near(Items.TicksLeft(man,"LP_BasiliskPetrified"),WildMonsters.PetrifiedTicks),"a failed save turns the target to stone for half a day");
   var blind=WmHuman(cell+IntVec3.West*3);
   foreach(var eye in blind.RaceProps.body.AllParts.Where(p=>p.def.defName=="Eye").ToList())blind.health.AddHediff(HediffDefOf.MissingBodyPart,eye);
   var other=WmMonster("LP_Basilisk",cell+IntVec3.South*3);
   Check(!WildMonsters.CanBeGazed(b,blind) && !WildMonsters.CanBeGazed(b,other) && !WildMonsters.CanBeGazed(b,man),"blind pawns, other basilisks and statues are not gazed");
   var stiff=WmHuman(cell+IntVec3.North*3);b.mindState.enemyTarget=stiff;WildMonsters.GazeAll(b);var s=stiff.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_BasiliskGaze"));float sev=s.Severity;b.mindState.enemyTarget=null;Step(450);
   Check(!stiff.health.hediffSet.HasHediff(Items.Hediff("LP_BasiliskGaze")) || s.Severity<sev,"stiffness wears off when the gaze stops");
   WmDone();
  }

  void WmManticore(){
   var cell=OpenCell();var m=WmMonster("LP_Manticore",cell);var comp=WildMonsters.Comp(m);var man=WmHuman(cell+IntVec3.East*7);
   Check(comp.spikes==WildMonsters.SpikeMax_ && comp.CompInspectStringExtra().Contains("24"),"manticore starts with 24 tail spikes");
   Check(WildMonsters.InSpikeRange(m,man) && !WildMonsters.InSpikeRange(m,WmHuman(cell+IntVec3.West)),"spike range is 3 to 15 cells");
   m.mindState.enemyTarget=man;int before=map.listerThings.ThingsOfDef(WildMonsters.SpikeDef).Count;
   WildMonsters.TestRoll=0.9f;comp.CompTickInterval(60);WildMonsters.TestRoll=null;
   Check(comp.spikes==21 && !comp.VolleyReady && map.listerThings.ThingsOfDef(WildMonsters.SpikeDef).Count==before+3,"in combat the manticore fires a volley of 3 spikes and starts the cooldown");
   int spikes=comp.spikes;comp.CompTickInterval(60);
   Check(comp.spikes==spikes,"no second volley during the cooldown");
   float hp=man.health.summaryHealth.SummaryHealthPercent;Step(90);
   Check(man.health.summaryHealth.SummaryHealthPercent<hp || man.Dead,"spikes wound the target");
   m.mindState.enemyTarget=null;comp.CompTickInterval(WildMonsters.SpikeRegrowTicks);
   Check(comp.spikes==spikes+1,"a spike regrows every hour");
   comp.spikes=0;comp.volleyReadyTick=-1;m.mindState.enemyTarget=man;comp.CompTickInterval(60);
   Check(comp.VolleyReady,"no volley without spikes");
   WmDone();
  }

  void WmBulette(){
   var cell=OpenCell();var bu=WmMonster("LP_Bulette",cell);var comp=WildMonsters.Comp(bu);var man=WmHuman(cell+IntVec3.East*6);
   bu.mindState.enemyTarget=man;Legendary.TestD20=1;
   comp.CompTickInterval(60);
   Check(!comp.LeapReady,"bulette leaps at a target 6 cells away");
   for(int i=0;i<300 && !bu.Spawned;i++)Step(1);
   Check(bu.Spawned && bu.Position.AdjacentTo8WayOrInside(man.Position),"bulette lands next to its target (spawned "+bu.Spawned+", at "+bu.Position+", target "+man.Position+")");
   Check(man.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury) &&man.stances.stunner.Stunned,"failed dexterity save: heavy blow and knocked down");
   WmClear(man);Legendary.TestD20=20;
   int knocked=WildMonsters.Land(bu);Legendary.TestD20=null;
   Check(knocked==0 && !man.stances.stunner.Stunned && man.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury),"successful save: half damage and stays on its feet");
   var near=WmHuman(cell+IntVec3.West*2);comp.leapReadyTick=-1;bu.mindState.enemyTarget=near;Races124.Blink(bu,cell);comp.CompTickInterval(60);
   Check(comp.LeapReady,"no leap at targets closer than 3 cells");
   WmDone();
  }

  void WmCrawler(){
   var cell=OpenCell();var cr=WmMonster("LP_CarrionCrawler",cell);var man=WmHuman(cell+IntVec3.East);
   var tent=WmVerb(cr,"tentacles");var bite=WmVerb(cr,"mandibles");
   Check(tent!=null && bite!=null,"carrion crawler has tentacles and mandibles");
   Legendary.TestD20=1;MeleeHit(bite,man);
   Check(!Items.Has(man,"LP_CrawlerToxin"),"mandible bites carry no toxin");
   Legendary.TestD20=20;MeleeHit(tent,man);
   Check(!Items.Has(man,"LP_CrawlerToxin"),"a successful constitution save blocks the toxin");
   Legendary.TestD20=1;MeleeHit(tent,man);
   var h=man.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_CrawlerToxin"));
   Check(h!=null && Near(h.Severity,WildMonsters.ToxinStep),"a tentacle hit injects toxin ("+h?.Severity+")");
   MeleeHit(tent,man);MeleeHit(tent,man);Legendary.TestD20=null;
   Check(Items.Has(man,"LP_CrawlerParalysis") && !Items.Has(man,"LP_CrawlerToxin") && man.Downed,"three doses paralyze the target");
   cr.Destroy();WmClear(man);Step(WildMonsters.ParalysisTicks+30);
   Check(!Items.Has(man,"LP_CrawlerParalysis") && !man.Downed,"paralysis wears off after 10 seconds");
   WmDone();
  }

  void WmMythal(){
   var theme=DefDatabase<MythalThemeDef>.GetNamedSilentFail("LP_MythalTheme_UnderdarkWarren");
   Check(theme!=null && theme.Usable && theme.Monsters.Count()==5 && theme.Guardian?.defName=="LP_Bulette" && theme.Monsters.All(k=>WildMonsters.IsMonsterKind(k)),"mythal dungeon has the Underdark warren theme with the new monsters");
  }

  void PrepareWildMonstersSave(){
   var cell=OpenCell();var m=WmMonster("LP_Manticore",cell);var comp=WildMonsters.Comp(m);comp.spikes=17;comp.volleyReadyTick=wmVolleyTick=Find.TickManager.TicksGame+400;
   var cube=WmMonster("LP_GelatinousCube",cell+IntVec3.South*4);var man=WmHuman(cell+IntVec3.South*4+IntVec3.East);
   WildMonsters.TryEngulf(cube,man,0f);var h=man.health.hediffSet.hediffs.OfType<Hediff_Engulfed>().First();h.left=wmLeft=300;
   wmMantId=m.ThingID;wmCubeId=cube.ThingID;wmVictimId=man.ThingID;wmMade.Clear();
  }
  void CheckWildMonstersAfterLoad(){
   var all=Find.CurrentMap.mapPawns.AllPawnsSpawned;
   var m=all.FirstOrDefault(p=>p.ThingID==wmMantId);var cube=all.FirstOrDefault(p=>p.ThingID==wmCubeId);var man=all.FirstOrDefault(p=>p.ThingID==wmVictimId);
   var comp=WildMonsters.Comp(m);
   Check(comp!=null && comp.spikes==17 && comp.volleyReadyTick==wmVolleyTick,"manticore spikes and volley cooldown survive save/load");
   var h=man?.health.hediffSet.hediffs.OfType<Hediff_Engulfed>().FirstOrDefault();
   Check(h!=null && h.cube==cube && h.left==wmLeft && !WildMonsters.Comp(cube).EngulfReady,"engulfed state, its cube and the engulf cooldown survive save/load");
   foreach(var p in new[]{m,cube,man})if(p!=null && !p.Destroyed)p.Destroy();
  }
 }
}
