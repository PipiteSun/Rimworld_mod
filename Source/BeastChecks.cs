using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 // 1.25.0 可驯服的 DND 野兽的检查（需求“测试”一节第 1～12 条）。
 public partial class LichChecks {
  static readonly Dictionary<string,(float size,float health,float power,float wild,string train,bool predator,bool manhunter)> beastStats=new Dictionary<string,(float,float,float,float,string,bool,bool)>{
   {"LP_Owlbear",(2.15f,2.5f,180,.85f,"Advanced",true,true)},{"LP_Worg",(1.2f,1.3f,110,.75f,"Advanced",true,true)},{"LP_WinterWolf",(1.3f,1.4f,120,.8f,"Intermediate",true,true)},
   {"LP_HellHound",(1f,1.2f,120,.85f,"Intermediate",true,true)},{"LP_DisplacerBeast",(1.3f,1.3f,130,.85f,"Intermediate",true,true)},{"LP_BlinkDog",(.8f,.9f,60,.5f,"Advanced",false,false)}};
  static readonly Dictionary<string,string[]> beastBiomes=new Dictionary<string,string[]>{
   {"LP_Owlbear",new[]{"TemperateForest","BorealForest","TemperateSwamp"}},{"LP_Worg",new[]{"BorealForest","Tundra","ColdBog"}},{"LP_WinterWolf",new[]{"Tundra","IceSheet","BorealForest"}},
   {"LP_HellHound",new[]{"AridShrubland","Desert","ExtremeDesert"}},{"LP_DisplacerBeast",new[]{"TropicalRainforest","TemperateForest","TropicalSwamp"}},{"LP_BlinkDog",new[]{"TemperateForest","BorealForest"}}};
  static string beastOwlId,beastDisplacerId,beastBlinkId;static int beastHugLeft,beastPhantomLeft,beastBlinkLeft;
  readonly List<Thing> beastMade=new List<Thing>();
  static readonly System.Reflection.MethodInfo meleeApply=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget"),nonMiss=AccessTools.Method(typeof(Verb_MeleeAttack),"GetNonMissChance");
  static bool Chinese(string s)=>!s.NullOrEmpty() && s.Any(c=>c>=0x4e00 && c<=0x9fff);
  static int BeastNow=>Find.TickManager.TicksGame;

  Pawn Beast(string kind,IntVec3? at=null,Faction faction=null,Gender gender=Gender.None,float age=3f){
   var req=new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(kind),faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,fixedBiologicalAge:age,fixedChronologicalAge:age,fixedGender:gender==Gender.None?(Gender?)null:gender);
   var p=PawnGenerator.GeneratePawn(req);
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h.def==HediffDefOf.Pregnant).ToList())p.health.RemoveHediff(h);
   GenSpawn.Spawn(p,at??CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);beastMade.Add(p);return p;
  }
  Pawn BeastHuman(){var p=SharedPawn();beastMade.Add(p);return p;}
  Pawn BeastRace(string xeno){var p=RacePawn(xeno);beastMade.Add(p);return p;}
  static Verb_MeleeAttackDamage BeastVerb(Pawn p,bool bite)=>p.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().FirstOrDefault(v=>!bite || Beasts.IsBite(v.tool));
  static DamageWorker.DamageResult MeleeHit(Verb_MeleeAttackDamage verb,Pawn target)=>(DamageWorker.DamageResult)meleeApply.Invoke(verb,new object[]{new LocalTargetInfo(target)});
  // 一块周围 7 格都能站的空地（闪现、群嚎要用）。
  IntVec3 OpenCell(){
   if(CellFinder.TryFindRandomCellNear(map.Center,map,40,c=>c.Standable(map) && c.GetFirstPawn(map)==null && GenRadial.RadialCellsAround(c,7f,true).All(x=>x.InBounds(map) && x.Standable(map) && x.GetFirstPawn(map)==null),out var cell))return cell;
   return CellFinder.RandomClosewalkCellNear(map.Center,map,10);
  }

  void CheckBeasts(){
   bool wild=LichMod.Settings.dndBeastsWild;
   try{CheckBeastDefs();CheckBeastBiomes();CheckOwlbear();CheckWorg();CheckWinterWolf();CheckHellHound();CheckDisplacer();CheckBlinkDog();CheckOwlbearEgg();CheckBeastTaming();CheckBeastIsekaiPool();}
   finally{
    LichMod.Settings.dndBeastsWild=wild;Beasts.ApplySettings();Beasts.TestRoll=null;
    foreach(var t in beastMade)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    beastMade.Clear();
   }
  }

  // 1. 定义、中文名、贴图；3. 野性、可训练、掠食、狂暴群。
  void CheckBeastDefs(){
   foreach(var pair in beastStats){
    var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail(pair.Key);var race=DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);var s=pair.Value;
    Check(kind!=null && race!=null && kind.race==race && race.race.Animal,pair.Key+" thing and pawn kind load as an animal");
    Check(!kind.ConfigErrors().Any(),pair.Key+" pawn kind definition: "+string.Join(";",kind.ConfigErrors()));
    Check(Chinese(race.label) && Chinese(kind.label) && Chinese(race.description),pair.Key+" has a Chinese label and description: "+race.label);
    Check(race.GetCompProperties<CompProperties_Beast>()!=null && Beasts.IsBeastKind(kind),pair.Key+" carries the beast component");
    foreach(var stage in kind.lifeStages){
     var g=stage.bodyGraphicData;
     Check(g!=null && ContentFinder<Texture2D>.Get(g.texPath+"_south",false)!=null && g.color!=Color.white,pair.Key+" life stage uses a recolored vanilla texture: "+g?.texPath);
     Check(ContentFinder<Texture2D>.Get(stage.dessicatedBodyGraphicData.texPath+"_south",false)!=null,pair.Key+" dessicated texture resolves: "+stage.dessicatedBodyGraphicData.texPath);
    }
    Check(Near(race.race.baseBodySize,s.size) && Near(race.race.baseHealthScale,s.health) && Near(kind.combatPower,s.power),pair.Key+" body size, health scale and combat power: "+race.race.baseBodySize+"/"+race.race.baseHealthScale+"/"+kind.combatPower);
    float w=race.GetStatValueAbstract(StatDefOf.Wildness);
    Check(Near(w,s.wild) && w<1f && race.race.trainability?.defName==s.train,pair.Key+" is tameable: wildness "+w+", trainability "+race.race.trainability?.defName);
    Check(race.race.predator==s.predator && Beasts.DefaultManhunter(kind)==s.manhunter,pair.Key+" predator and manhunter-pack flags");
    Check(race.race.leatherDef!=null && race.race.meatDef!=null && race.race.corpseDef!=null,pair.Key+" has generated meat, corpse and a leather");
    var p=Beast(pair.Key);p.Drawer.renderer.EnsureGraphicsInitialized();
    Check(p.Drawer.renderer.BodyGraphic!=null && new[]{Rot4.North,Rot4.East,Rot4.South,Rot4.West}.All(r=>p.Drawer.renderer.BodyGraphic.MatAt(r)!=BaseContent.BadMat),pair.Key+" graphic resolves in all directions");
    Check(Chinese(p.LabelShort) || Chinese(p.KindLabel),pair.Key+" spawned pawn shows a Chinese label: "+p.KindLabel);
   }
   Check(DefDatabase<ThingDef>.GetNamed("LP_DisplacerBeast").race.leatherDef?.defName=="LP_Leather_Displacer","displacer beast drops displacer leather");
   var leather=DefDatabase<ThingDef>.GetNamed("LP_Leather_Displacer");var panther=DefDatabase<ThingDef>.GetNamed("Leather_Panthera");
   Check(leather.GetStatValueAbstract(StatDefOf.StuffPower_Insulation_Cold)>panther.GetStatValueAbstract(StatDefOf.StuffPower_Insulation_Cold) && leather.stuffProps.statFactors.GetStatFactorFromList(StatDefOf.Beauty)>panther.stuffProps.statFactors.GetStatFactorFromList(StatDefOf.Beauty),"displacer leather is warmer and prettier than panthera fur");
   Check(DefDatabase<ThingDef>.GetNamed("LP_HellHound").GetStatValueAbstract(StatDefOf.Flammability)==0f && DefDatabase<ThingDef>.GetNamed("LP_HellHound").GetCompProperties<CompProperties_Glower>()!=null,"hellhound is nonflammable and glows");
   var wolf=DefDatabase<ThingDef>.GetNamed("LP_WinterWolf");
   Check(Near(wolf.GetStatValueAbstract(StatDefOf.ComfyTemperatureMin),-60) && Near(wolf.GetStatValueAbstract(StatDefOf.ComfyTemperatureMax),20) && Near(DefDatabase<ThingDef>.GetNamed("LP_HellHound").GetStatValueAbstract(StatDefOf.ComfyTemperatureMax),80),"winter wolf comfort -60..20, hellhound up to 80");
  }

  // 2. 生态区刷新与设置开关。
  void CheckBeastBiomes(){
   foreach(var pair in beastBiomes){
    var kind=DefDatabase<PawnKindDef>.GetNamed(pair.Key);
    foreach(var b in pair.Value){var biome=DefDatabase<BiomeDef>.GetNamed(b);Check(biome.CommonalityOfAnimal(kind)>0f && biome.CommonalityOfAnimal(kind)<=.07f && biome.AllWildAnimals.Contains(kind),pair.Key+" spawns wild in "+b+" ("+biome.CommonalityOfAnimal(kind)+")");}
   }
   Check(DefDatabase<PawnKindDef>.GetNamed("LP_Worg").wildGroupSize.min==3 && DefDatabase<PawnKindDef>.GetNamed("LP_Worg").wildGroupSize.max==6 && DefDatabase<PawnKindDef>.GetNamed("LP_WinterWolf").wildGroupSize.max==4 && DefDatabase<PawnKindDef>.GetNamed("LP_BlinkDog").wildGroupSize.min==2,"pack sizes for worgs, winter wolves and blink dogs");
   if(ModsConfig.OdysseyActive){var lava=DefDatabase<BiomeDef>.GetNamedSilentFail("LavaField");if(lava!=null)Check(lava.CommonalityOfAnimal(DefDatabase<PawnKindDef>.GetNamed("LP_HellHound"))>0f,"hellhound also roams Odyssey lava fields");}
   LichMod.Settings.dndBeastsWild=false;Beasts.ApplySettings();
   foreach(var pair in beastBiomes){
    var kind=DefDatabase<PawnKindDef>.GetNamed(pair.Key);
    Check(DefDatabase<BiomeDef>.AllDefs.All(b=>b.CommonalityOfAnimal(kind)==0f && !b.AllWildAnimals.Contains(kind)) && !kind.canArriveManhunter,pair.Key+" no longer spawns or arrives as a manhunter pack when the setting is off");
   }
   Check(DefDatabase<BiomeDef>.GetNamed("TemperateForest").CommonalityOfAnimal(DefDatabase<PawnKindDef>.GetNamed("Bear_Grizzly"))>0f,"turning DND beasts off keeps vanilla wildlife");
   LichMod.Settings.dndBeastsWild=true;Beasts.ApplySettings();
   Check(DefDatabase<PawnKindDef>.GetNamed("LP_Owlbear").canArriveManhunter && !DefDatabase<PawnKindDef>.GetNamed("LP_BlinkDog").canArriveManhunter && DefDatabase<BiomeDef>.GetNamed("Tundra").CommonalityOfAnimal(DefDatabase<PawnKindDef>.GetNamed("LP_Worg"))>0f,"turning the setting back on restores spawning and manhunter packs");
  }

  // 4. 熊抱。
  void CheckOwlbear(){
   var bear=Beast("LP_Owlbear");var comp=Beasts.Comp(bear);
   var a=BeastHuman();Check(comp.TryHug(a,0f) && a.stances.stunner.Stunned && comp.hugReadyTick==BeastNow+Beasts.HugCooldown,"owlbear hug stuns the target and starts a 10-second cooldown");
   var b=BeastHuman();Check(!comp.TryHug(b,0f) && !b.stances.stunner.Stunned,"owlbear hug does not trigger again during its cooldown");
   comp.hugReadyTick=BeastNow;Check(!comp.TryHug(b,.3f) && comp.TryHug(b,.2f),"owlbear hug chance is 25% per hit");
   comp.hugReadyTick=BeastNow;var c=BeastHuman();Beasts.TestRoll=0f;try{MeleeHit(BeastVerb(bear,false),c);}finally{Beasts.TestRoll=null;}
   Check(c.Dead || c.stances.stunner.Stunned,"owlbear melee hit triggers the hug through the native melee hook");
  }

  // 5. 群嚎：周围 0 / 2 / 6 只同伴。
  void CheckWorg(){
   var cell=OpenCell();var w=Beast("LP_Worg",cell);var comp=Beasts.Comp(w);var target=BeastHuman();var verb=BeastVerb(w,false);
   comp.ForgetPack();float alone=(float)nonMiss.Invoke(verb,new object[]{new LocalTargetInfo(target)});
   Check(comp.PackAllies()==0 && comp.PackBonus==0f,"lone worg has no pack bonus");
   var spots=GenRadial.RadialCellsAround(cell,3f,false).Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null).ToList();
   for(int i=0;i<2;i++)Beast("LP_Worg",spots[i]);
   Check(comp.PackAllies()==0,"pack size is cached for 250 ticks instead of scanning every time");
   comp.ForgetPack();Check(comp.PackAllies()==2 && Near(comp.PackBonus,.1f),"two nearby worgs give +10% melee hit chance");
   float two=(float)nonMiss.Invoke(verb,new object[]{new LocalTargetInfo(target)});Check(Near(two,Mathf.Min(1f,alone+.1f)),"pack bonus reaches the native melee hit chance: "+alone+" -> "+two);
   for(int i=2;i<6;i++)Beast("LP_Worg",spots[i]);
   Beast("LP_Worg",spots[6],Faction.OfPlayer);
   comp.ForgetPack();Check(comp.PackAllies()==6 && Near(comp.PackBonus,.2f),"six nearby worgs cap the bonus at +20% (other factions not counted)");
   var far=Beast("LP_Worg",CellFinder.RandomClosewalkCellNear(map.Center,map,40,c=>!c.InHorDistOf(cell,12f)));comp.ForgetPack();Check(comp.PackAllies()==6,"worgs beyond 8 cells are not counted");
  }

  // 6. 冬狼：冰咬、怕火。
  void CheckWinterWolf(){
   var wolf=Beast("LP_WinterWolf");var target=BeastHuman();
   MeleeHit(BeastVerb(wolf,true),target);
   Check(target.health.hediffSet.hediffs.Any(h=>h.def.defName=="Frostbite"),"winter wolf bite adds frostbite");
   int left=Items.TicksLeft(target,"LP_FrostSlow");var slow=Items.Hediff("LP_FrostSlow");
   Check(left>0 && left<=Beasts.FrostSlowTicks && Near(slow.stages[0].statFactors.GetStatFactorFromList(StatDefOf.MoveSpeed),.5f),"winter wolf bite applies frost slow (move speed x0.5 for 4 seconds): "+left);
   var other=BeastHuman();MeleeHit(BeastVerb(wolf,false),other);
   if(!Beasts.IsBite(BeastVerb(wolf,false).tool))Check(!Items.Has(other,"LP_FrostSlow"),"winter wolf claws do not apply frost slow");
   var dealt=wolf.TakeDamage(new DamageInfo(DamageDefOf.Burn,4f,0f,-1f,null,wolf.RaceProps.body.corePart)).totalDamageDealt;
   Check(Math.Abs(dealt-6f)<.1f,"winter wolf takes 150% fire damage: "+dealt);
   var arctic=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("Wolf_Arctic"),null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true));GenSpawn.Spawn(arctic,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);beastMade.Add(arctic);
   var plain=arctic.TakeDamage(new DamageInfo(DamageDefOf.Burn,4f,0f,-1f,null,arctic.RaceProps.body.corePart)).totalDamageDealt;
   Check(Math.Abs(plain-4f)<.1f,"vanilla arctic wolf takes the same burn unchanged: "+plain);
   // 割伤、钝伤会随机扩散到别的部位，总量不固定：直接看组件有没有改伤害量。
   var cut=new DamageInfo(DamageDefOf.Cut,4f,0f,-1f,null,wolf.RaceProps.body.corePart);Beasts.Comp(wolf).PostPreApplyDamage(ref cut,out bool cutAbsorbed);
   var flame=new DamageInfo(DamageDefOf.Flame,4f,0f,-1f,null,wolf.RaceProps.body.corePart);Beasts.Comp(wolf).PostPreApplyDamage(ref flame,out _);
   Check(!cutAbsorbed && Near(cut.Amount,4f) && Near(flame.Amount,6f),"winter wolf takes normal non-fire damage and x1.5 flame: "+cut.Amount+" / "+flame.Amount);
  }

  // 7. 地狱犬：免火、火咬阻断巨魔再生。
  void CheckHellHound(){
   var hound=Beast("LP_HellHound");
   var r=hound.TakeDamage(new DamageInfo(DamageDefOf.Flame,10f,0f,-1f,null,hound.RaceProps.body.corePart));
   Check(r.totalDamageDealt==0f && !hound.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury),"hellhound takes no fire damage");
   Check(hound.GetComp<CompGlower>()?.Glows==true,"hellhound gives off a faint glow");
   var troll=BeastRace("LP_Troll");var gene=Troll.Gene(troll);gene.suppressedUntilTick=0;
   MeleeHit(BeastVerb(hound,true),troll);
   Check(troll.health.hediffSet.hediffs.Any(h=>h.def==Troll.Burn) && gene.suppressedUntilTick>BeastNow,"hellhound fire bite burns a troll and stops its regeneration");
  }

  // 8. 魔化兽：落空、6 秒内命中、6 秒后再落空。
  void CheckDisplacer(){
   var beast=Beast("LP_DisplacerBeast");var comp=Beasts.Comp(beast);var attacker=BeastHuman();
   comp.CompTickInterval(60);Check(Items.Has(beast,"LP_DisplacementPhantom"),"displacer beast shows the displacement status");
   DamageInfo Hit()=>new DamageInfo(DamageDefOf.Cut,5f,2f,-1f,attacker,beast.RaceProps.body.corePart);
   int start=BeastNow;
   var first=beast.TakeDamage(Hit());
   Check(first.totalDamageDealt==0f && !beast.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury) && comp.phantomReadyTick==start+Beasts.PhantomCooldown,"first attack on a displacer beast misses");
   Check(!Items.Has(beast,"LP_DisplacementPhantom"),"displacement status disappears while the phantom recovers");
   Find.TickManager.DebugSetTicksGame(start+60);
   var second=beast.TakeDamage(Hit());Check(second.totalDamageDealt>0f,"second attack within 6 seconds hits");
   Find.TickManager.DebugSetTicksGame(start+Beasts.PhantomCooldown);
   var third=beast.TakeDamage(Hit());Check(third.totalDamageDealt==0f && comp.phantomReadyTick==start+2*Beasts.PhantomCooldown,"after 6 seconds the phantom makes the next attack miss again");
   var bomb=new DamageInfo(DamageDefOf.Bomb,5f,0f,-1f,attacker);comp.phantomReadyTick=BeastNow;Check(beast.TakeDamage(bomb).totalDamageDealt==0f,"phantom also absorbs one explosion");
   var noInstigator=new DamageInfo(DamageDefOf.Cut,3f,2f,-1f,null,beast.RaceProps.body.corePart);comp.phantomReadyTick=BeastNow;comp.PostPreApplyDamage(ref noInstigator,out bool noAbsorb);Check(!noAbsorb && comp.PhantomActive,"damage without an attacker does not break the phantom");
   beast.health.AddHediff(HediffDefOf.Anesthetic);if(!beast.Downed)HealthUtility.DamageUntilDowned(beast,false);
   Check(beast.Downed && !comp.PhantomActive,"downed displacer beast loses the phantom");
  }

  // 9. 闪光犬：闪现到 3～6 格、可站立，冷却内不触发。
  void CheckBlinkDog(){
   var cell=OpenCell();var dog=Beast("LP_BlinkDog",cell);var comp=Beasts.Comp(dog);
   var attacker=BeastHuman();attacker.Position=GenAdj.CellsAdjacent8Way(dog).First(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null);attacker.Notify_Teleported();
   float before=dog.Position.DistanceTo(attacker.Position);
   Check(!comp.TryBlink(attacker,.31f) && dog.Position==cell,"blink dog does not blink on a failed 30% roll");
   Check(comp.TryBlink(attacker,0f),"blink dog blinks when attacked");
   float moved=dog.Position.DistanceTo(cell);
   Check(dog.Position!=cell && moved>=Beasts.BlinkMin && moved<=Beasts.BlinkMax+.01f && dog.Position.Standable(map) && dog.Position.DistanceTo(attacker.Position)>before,"blink lands 3-6 cells away on standable ground farther from the attacker: "+moved);
   var at=dog.Position;Check(!comp.TryBlink(attacker,0f) && dog.Position==at,"blink dog does not blink again during its cooldown");
   comp.blinkReadyTick=BeastNow;Beasts.TestRoll=0f;try{dog.TakeDamage(new DamageInfo(DamageDefOf.Cut,1f,2f,-1f,attacker,dog.RaceProps.body.corePart));}finally{Beasts.TestRoll=null;}
   Check(dog.Position!=at && comp.blinkReadyTick==BeastNow+Beasts.BlinkCooldown,"being hit by an attacker triggers the blink");
  }

  // 10. 猫头鹰熊蛋：受精、产蛋、孵化，阵营正确，不会怀孕。
  void CheckOwlbearEgg(){
   var female=Beast("LP_Owlbear",null,Faction.OfPlayer,Gender.Female);var male=Beast("LP_Owlbear",null,Faction.OfPlayer,Gender.Male);
   var layer=female.TryGetComp<CompEggLayer>();
   Check(layer!=null && layer.Props.eggLayIntervalDays==15f && layer.Props.eggCountRange.max==1 && layer.Props.eggFertilizedDef.defName=="LP_EggOwlbearFertilized","female owlbear lays one egg about every 15 days");
   PawnUtility.Mated(male,female);
   Check(!female.health.hediffSet.HasHediff(HediffDefOf.Pregnant) && layer.FullyFertilized,"mating fertilizes the owlbear instead of a live pregnancy");
   var egg=layer.ProduceEgg();
   Check(egg?.def.defName=="LP_EggOwlbearFertilized" && egg.def.IsIngestible && Chinese(egg.def.label) && egg.TryGetComp<CompHatcher>().hatcheeFaction==Faction.OfPlayer,"fertilized owlbear egg is edible and belongs to the mother's faction");
   Check(Near(egg.def.GetCompProperties<CompProperties_Hatcher>().hatcherDaystoHatch,12f),"owlbear egg hatches in about 12 days");
   GenSpawn.Spawn(egg,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);
   var known=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);egg.TryGetComp<CompHatcher>().Hatch();
   var chick=map.mapPawns.AllPawnsSpawned.FirstOrDefault(p=>!known.Contains(p) && p.def.defName=="LP_Owlbear");if(chick!=null)beastMade.Add(chick);
   Check(chick!=null && chick.Faction==Faction.OfPlayer && chick.ageTracker.CurLifeStageIndex==0,"egg hatches an owlbear cub of the owner's faction");
   var wildEgg=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_EggOwlbearFertilized"));GenSpawn.Spawn(wildEgg,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);
   known=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);wildEgg.TryGetComp<CompHatcher>().Hatch();
   var wildCub=map.mapPawns.AllPawnsSpawned.FirstOrDefault(p=>!known.Contains(p) && p.def.defName=="LP_Owlbear");if(wildCub!=null)beastMade.Add(wildCub);
   Check(wildCub!=null && wildCub.Faction==null,"an ownerless egg hatches a wild cub");
  }

  // 11. 种族驯服加成。
  void CheckBeastTaming(){
   var method=AccessTools.Method(typeof(InteractionWorker_RecruitAttempt),nameof(InteractionWorker_RecruitAttempt.Interacted));
   Check(Beasts.TamePatched && Harmony.GetPatchInfo(method)?.Transpilers.Any(p=>p.owner=="local.lich.phylactery")==true,"tame bonus is applied inside the native tame chance");
   var human=BeastHuman();var orc=BeastRace("LP_HalfOrc");var giant=BeastRace("LP_FireGiant");var elf=BeastRace("LP_Elf");
   var worg=Beast("LP_Worg");var hound=Beast("LP_HellHound");var blink=Beast("LP_BlinkDog");var owl=Beast("LP_Owlbear");var wolf=Beast("LP_WinterWolf");
   void Pair(Pawn tamer,Pawn animal,string what){
    float normal=Beasts.TameChance(.3f,human,animal),bonus=Beasts.TameChance(.3f,tamer,animal);
    Check(Near(normal,.3f) && Near(bonus,.6f) && Near(Beasts.TameChance(.7f,tamer,animal),1f),what+" doubles the tame chance (capped at 100%)");
   }
   Pair(orc,worg,"half-orc taming a worg");Pair(giant,hound,"fire giant taming a hellhound");Pair(elf,blink,"elf taming a blink dog");Pair(elf,owl,"elf taming an owlbear");
   Check(Near(Beasts.TameChance(.3f,elf,worg),.3f) && Near(Beasts.TameChance(.3f,orc,hound),.3f) && Near(Beasts.TameChance(.3f,orc,wolf),.3f) && Near(Beasts.TameChance(.3f,orc,DefaultAnimal()),.3f),"other races and other animals keep the normal tame chance");
   var tagged=BeastHuman();tagged.genes.AddGene(CommonRaces.RelentlessGene,true);Check(Near(Beasts.TameChance(.3f,tagged,worg),.6f),"half-orc bonus follows the half-orc gene, not only the xenotype name");
  }
  Pawn DefaultAnimal(){var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("Warg"),null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true));GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);beastMade.Add(p);return p;}

  // Isekai 世界首领：它从 combatPower≥100 的非人形里取前 30%。这里按本次载入的定义估一下（只记录，不判定）。
  void CheckBeastIsekaiPool(){
   var pool=DefDatabase<PawnKindDef>.AllDefs.Where(k=>k.RaceProps!=null && !k.RaceProps.Humanlike && !k.RaceProps.Dryad && k.combatPower>=100f).OrderByDescending(k=>k.combatPower).ToList();
   int top=Math.Max(1,pool.Count*30/100);var topSet=new HashSet<PawnKindDef>(pool.Take(top));
   var inTop=Beasts.AllKinds().Where(topSet.Contains).Select(k=>k.defName).ToList();
   Log.Message("[LICH-BEASTS] Isekai world boss pool estimate: "+pool.Count+" kinds, top "+top+" from combatPower "+pool[top-1].combatPower+"; DND beasts in it: "+(inTop.Count==0?"none":string.Join(",",inTop)));
   Check(pool.Count>0,"Isekai world boss pool estimate computed");
  }

  // 12. 存读档：熊抱、位移幻影、闪现的冷却。
  void PrepareBeastsSave(){
   map=Find.CurrentMap;int now=BeastNow;
   var owl=Beast("LP_Owlbear");Beasts.Comp(owl).hugReadyTick=now+400;beastOwlId=owl.ThingID;beastHugLeft=400;
   var dis=Beast("LP_DisplacerBeast");var attacker=BeastHuman();dis.TakeDamage(new DamageInfo(DamageDefOf.Cut,5f,0f,-1f,attacker,dis.RaceProps.body.corePart));beastDisplacerId=dis.ThingID;beastPhantomLeft=Beasts.Comp(dis).phantomReadyTick-now;
   var dog=Beast("LP_BlinkDog");Beasts.Comp(dog).blinkReadyTick=now+500;beastBlinkId=dog.ThingID;beastBlinkLeft=500;
   attacker.Destroy();beastMade.Clear();
  }
  void CheckBeastsAfterLoad(){
   map=Find.CurrentMap;Pawn Find1(string id)=>map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==id);int now=BeastNow;
   var owl=Find1(beastOwlId);var dis=Find1(beastDisplacerId);var dog=Find1(beastBlinkId);
   Check(Math.Abs(Beasts.Comp(owl).hugReadyTick-now-beastHugLeft)<=5 && !Beasts.Comp(owl).HugReady,"owlbear hug cooldown survives save/load");
   Check(beastPhantomLeft==Beasts.PhantomCooldown && Math.Abs(Beasts.Comp(dis).phantomReadyTick-now-beastPhantomLeft)<=5 && !Beasts.Comp(dis).PhantomActive,"displacement phantom cooldown survives save/load");
   Check(Math.Abs(Beasts.Comp(dog).blinkReadyTick-now-beastBlinkLeft)<=5 && !Beasts.Comp(dog).BlinkReady,"blink cooldown survives save/load");
   foreach(var p in new[]{owl,dis,dog})p.Destroy();
  }
 }
}
