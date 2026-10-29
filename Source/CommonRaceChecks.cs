using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  static readonly string[] commonRaces={"LP_HalfOrc","LP_Elf","LP_Dwarf"};
  static string orcSaveId,elfSaveId,dwarfSaveId;static int orcEnduranceLeft,elfSavedAsleep,dwarfSavedHangover,orcGeneCount,elfGeneCount,dwarfGeneCount;
  Pawn RacePawn(string xenotype,int age=25){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:DefDatabase<XenotypeDef>.GetNamed(xenotype),mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:age,fixedChronologicalAge:age));
   foreach(var implant in p.health.hediffSet.hediffs.OfType<Hediff_AddedPart>().ToList())p.health.RestorePart(implant.Part);
   foreach(var injury in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(injury);
   foreach(var missing in p.health.hediffSet.GetMissingPartsCommonAncestors().ToList())p.health.RestorePart(missing.Part);
   p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();
   GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);return p;
  }
  // TryGetMeleeVerb 随机挑选，可能挑到非伤害类 verb：取第一个造成伤害的近战 verb。
  static Verb_MeleeAttackDamage DamageVerb(Pawn p,Pawn target)=>p.meleeVerbs.TryGetMeleeVerb(target) as Verb_MeleeAttackDamage ?? p.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().FirstOrDefault();
  static float MeleeSample(Verb_MeleeAttackDamage verb,Pawn target,int samples,int seed){
   var method=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply");float sum=0;
   Rand.PushState(seed);try{for(int i=0;i<samples;i++)sum+=((IEnumerable<DamageInfo>)method.Invoke(verb,new object[]{new LocalTargetInfo(target)})).Sum(d=>d.Amount);}finally{Rand.PopState();}
   return sum/samples;
  }
  static HediffDef TestHediff(string name,HediffStage stage){var def=DefDatabase<HediffDef>.GetNamedSilentFail(name);if(def!=null)return def;def=new HediffDef{defName=name,label=name,hediffClass=typeof(Hediff),stages=new List<HediffStage>{stage}};DefDatabase<HediffDef>.Add(def);return def;}
  // 1.57.0：这一组改名 CommonRaces116（-Only CommonRaces116）；-Only CommonRaces 跑 1.57.0 的普通种族检查（CommonRaceSubraceChecks.cs）。
  void CheckCommonRaces116(){CheckCommonRaceDefs();CheckHalfOrc();CheckElf();CheckDwarf();CheckCommonRaceRelations();}

  void CheckCommonRaceDefs(){
   foreach(var name in commonRaces){
    var x=DefDatabase<XenotypeDef>.GetNamed(name);
    Check(!x.ConfigErrors().Any() && x.inheritable && x.factionlessGenerationWeight==0 && x.genes.All(g=>g!=null),name+" is an inheritable xenotype with resolved genes and no random generation");
    Check(x.genes.All(g=>g.biostatArc==0),name+" uses no archite genes");
    int met=x.genes.Sum(g=>g.biostatMet);Check(met>=-2 && met<=1,name+" metabolism total "+met);
    Check(ContentFinder<Texture2D>.Get(x.iconPath,false)!=null && x.genes.Where(g=>g.defName.StartsWith("LP_")).All(g=>ContentFinder<Texture2D>.Get(g.iconPath,false)!=null),name+" xenotype and custom gene icons load");
    foreach(var g in x.genes.Where(g=>g.defName.StartsWith("LP_")))Check(!g.ConfigErrors().Any() && (g.modExtensions.NullOrEmpty() || g.modExtensions.All(e=>!e.ConfigErrors().Any())),name+" gene validates: "+g.defName);
    var p=RacePawn(name);
    Check(p.genes.Xenotype==x && x.genes.All(g=>p.genes.GetGene(g)!=null) && x.genes.Where(g=>g.defName.StartsWith("LP_")).All(g=>p.genes.GetGene(g).Active),name+" generates with every xenotype gene and active custom genes");
    p.Destroy();
   }
  }

  void CheckHalfOrc(){
   var relentless=DefDatabase<GeneDef>.GetNamed("LP_HalfOrcRelentless");var ext=relentless.GetModExtension<OncePerCooldownExtension>();
   Check(ext.Worker is OncePerCooldownWorker_Relentless && ext.onDowned && !ext.onDeath && ext.CooldownTicks==60000 && ext.grantedHediff==CommonRaces.Endurance,"relentless uses the once-per-cooldown framework: downed only, one day cooldown");
   // 只致倒不致死：预先给接近休克阈值的疼痛，再一刀让人本该疼痛休克倒地。
   // 随机生成的角色可能带特性/伤病：用实际疼痛总量把疼痛调到阈值下方 0.04，不合适就重新生成。
   var pain=TestHediff("LPTest_NearShockPain",new HediffStage{painOffset=.01f});Pawn orc=null;float threshold=0;
   for(int attempt=0;attempt<10;attempt++){orc?.Destroy();pain.stages[0].painOffset=.01f;orc=RacePawn("LP_HalfOrc");threshold=orc.GetStatValue(StatDefOf.PainShockThreshold);var ph=orc.health.AddHediff(pain);for(int k=0;k<200 && orc.health.hediffSet.PainTotal<threshold-.04f;k++){pain.stages[0].painOffset+=.005f;orc.health.hediffSet.DirtyCache();}if(!orc.Downed && !orc.health.InPainShock && orc.health.hediffSet.PainTotal<threshold)break;}
   Check(!orc.Downed && !orc.health.InPainShock,"half-orc fixture stands just below pain shock: pain="+orc.health.hediffSet.PainTotal+" threshold="+threshold+" downed="+orc.Downed);
   var arm=orc.RaceProps.body.AllParts.First(b=>b.def.defName=="Arm");
   Check(orc.health.WouldBeDownedAfterAddingHediff(HediffDefOf.Cut,arm,8f) && !orc.health.WouldDieAfterAddingHediff(HediffDefOf.Cut,arm,8f),"fixture hit would down without killing");
   orc.TakeDamage(new DamageInfo(DamageDefOf.Cut,8,999,-1,null,arm));
   var cd=OncePerCooldown.Cooldown(orc,ext);var endure=orc.health.hediffSet.GetFirstHediffOfDef(CommonRaces.Endurance);
   Check(!orc.Dead && !orc.Downed && endure!=null && cd!=null,"relentless keeps the half-orc standing instead of going down");
   Check(endure.TryGetComp<HediffComp_Disappears>().ticksToDisappear==30000 && cd.TryGetComp<HediffComp_Disappears>().ticksToDisappear==60000,"relentless lasts 12 hours with a one day visible cooldown");
   var agony=orc.health.AddHediff(TestHediff("LPTest_Agony",new HediffStage{painOffset=1f}));Check(orc.health.hediffSet.PainTotal>=.99f && !orc.health.InPainShock && !orc.health.ShouldBeDowned(),"enduring ignores pain shock even at 100% pain");orc.health.RemoveHediff(agony);
   float baseConsciousness=PawnCapacityUtility.CalculateCapacityLevel(orc.health.hediffSet,PawnCapacityDefOf.Consciousness);var dazed=TestHediff("LPTest_Dazed",new HediffStage{capMods=new List<PawnCapacityModifier>{new PawnCapacityModifier{capacity=PawnCapacityDefOf.Consciousness,offset=-.6f}}});dazed.stages[0].capMods[0].offset=-Math.Max(.1f,baseConsciousness-.2f);var daze=orc.health.AddHediff(dazed);float rawConsciousness=PawnCapacityUtility.CalculateCapacityLevel(orc.health.hediffSet,PawnCapacityDefOf.Consciousness);
   Check(orc.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness)>=.3f && orc.health.capacities.CanBeAwake && !orc.Downed,"enduring keeps consciousness at the 30% floor: raw "+rawConsciousness+" -> "+orc.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness));
   orc.health.RemoveHediff(daze);
   endure.TryGetComp<HediffComp_Disappears>().ticksToDisappear=1;Step(70);
   Check(orc.health.hediffSet.GetFirstHediffOfDef(CommonRaces.Endurance)==null && orc.Downed,"after the endurance ends the wounds settle normally and the half-orc goes down");
   // 用原版处决的割喉伤害：一处伤势、必死。普通切割会拆到头、颅骨、大脑几处，大脑不一定死。
   var orc2=RacePawn("LP_HalfOrc");var execution=new DamageInfo(DamageDefOf.ExecutionCut,99999,999,-1,null,orc2.RaceProps.body.AllParts.First(b=>b.def.defName=="Neck"));execution.SetIgnoreInstantKillProtection(true);orc2.TakeDamage(execution);
   Check(orc2.Dead && orc2.health.hediffSet.GetFirstHediffOfDef(ext.cooldownHediff)==null,"relentless does not stop lethal damage and does not spend its cooldown on it: dead="+orc2.Dead+" cooldown="+(orc2.health.hediffSet.GetFirstHediffOfDef(ext.cooldownHediff)!=null)+" injuries="+string.Join(",",orc2.health.hediffSet.hediffs.Select(h=>h.def.defName+"@"+h.Part?.def.defName+":"+h.Severity.ToString("0.#"))));
   orc2.Corpse?.Destroy();
   // 野蛮攻击：同一把近战 verb，有基因时平均伤害约为 0.9+0.1×1.5=1.05 倍。
   Check(Near(CommonRaces.MeleeFactor(orc,null,.05f,out bool hit),1.5f) && hit && Near(CommonRaces.MeleeFactor(orc,null,.5f,out hit),1f) && !hit,"savage strike multiplies melee damage by 150% only on a 10% roll");
   var dummy=RacePawn("LP_HalfOrc");dummy.genes.RemoveGene(dummy.genes.GetGene(CommonRaces.SavageGene));
   var fighter=RacePawn("LP_HalfOrc");var verb=DamageVerb(fighter,dummy);
   Check(verb!=null,"half-orc has a damaging melee verb");
   float with=MeleeSample(verb,dummy,3000,91);fighter.genes.RemoveGene(fighter.genes.GetGene(CommonRaces.SavageGene));float without=MeleeSample(verb,dummy,3000,92);
   Check(with/without>1.03f && with/without<1.07f,"savage strike patch raises real melee damage by about 5% on average: "+(with/without).ToString("0.000"));
   orc.Destroy();dummy.Destroy();fighter.Destroy();
  }

  void CheckElf(){
   var elf=RacePawn("LP_Elf");var human=Person();GenSpawn.Spawn(human,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);
   Check(Near(DNDMind.Resistance(elf),.5f),"elf bloodline gives 50% mind resistance");
   var longevity=DefDatabase<GeneDef>.GetNamed("LP_ElfLongevity");
   Check(Near(longevity.biologicalAgeTickFactorFromAgeCurve.Evaluate(10),1f) && Near(longevity.biologicalAgeTickFactorFromAgeCurve.Evaluate(30),.25f),"elf childhood grows normally and adults age at 25%");
   Check(Math.Abs(elf.ageTracker.BiologicalTicksPerTick/human.ageTracker.BiologicalTicksPerTick-.25f)<.01f,"adult elf biological ticks per tick are a quarter of a human's: "+elf.ageTracker.BiologicalTicksPerTick+" vs "+human.ageTracker.BiologicalTicksPerTick);
   long e0=elf.ageTracker.AgeBiologicalTicks,h0=human.ageTracker.AgeBiologicalTicks;elf.ageTracker.AgeTickInterval(6000);human.ageTracker.AgeTickInterval(6000);
   float ratio=(elf.ageTracker.AgeBiologicalTicks-e0)/(float)Math.Max(1,human.ageTracker.AgeBiologicalTicks-h0);
   // 完整模组列表里有其他模组改原版的衰老推进（实测比例约0.28），只在核心环境里要求精确的四分之一。
   Check(FullModList?ratio>.15f && ratio<.4f:Math.Abs(ratio-.25f)<.02f,"native aging tick advances the elf four times slower: "+ratio);
   var trance=elf.genes.GetFirstGeneOfType<Gene_ElfTrance>();
   if(elf.GetPsylinkLevel()<1)elf.ChangePsylinkLevel(1,false);elf.psychicEntropy.OffsetPsyfocusDirectly(-1);
   elf.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown,elf.Position),JobCondition.InterruptForced);elf.jobs.curDriver.asleep=true;
   Check(!elf.Awake(),"elf fixture is asleep");
   for(int i=0;i<80;i++)trance.Process(Gene_ElfTrance.Interval);
   Check(Math.Abs(elf.psychicEntropy.CurrentPsyfocus-.2f)<.02f && trance.asleepTicks==20000,"a psylinked elf regains about 20% psyfocus over 8 hours of sleep: "+elf.psychicEntropy.CurrentPsyfocus);
   elf.jobs.curDriver.asleep=false;elf.jobs.StopAll();trance.Process(Gene_ElfTrance.Interval);
   Check(elf.needs.mood.thoughts.memories.GetFirstMemoryOfDef(Gene_ElfTrance.Rested)!=null && trance.asleepTicks==0,"waking after a long sleep gives the +3 lucid trance mood");
   var plain=RacePawn("LP_Elf");var plainTrance=plain.genes.GetFirstGeneOfType<Gene_ElfTrance>();float focus=plain.psychicEntropy?.CurrentPsyfocus??0;
   plain.jobs.StartJob(JobMaker.MakeJob(JobDefOf.LayDown,plain.Position),JobCondition.InterruptForced);plain.jobs.curDriver.asleep=true;
   for(int i=0;i<10;i++)plainTrance.Process(Gene_ElfTrance.Interval);
   Check(plain.HasPsylink || Near(plain.psychicEntropy?.CurrentPsyfocus??0,focus),"elf without a psylink gains no psyfocus from trance");
   plain.jobs.curDriver.asleep=false;plain.jobs.StopAll();plainTrance.Process(Gene_ElfTrance.Interval);
   Check(plain.needs.mood.thoughts.memories.GetFirstMemoryOfDef(Gene_ElfTrance.Rested)==null,"a short nap does not give the lucid trance mood");
   elf.Destroy();human.Destroy();plain.Destroy();
  }

  void CheckDwarf(){
   var dwarf=RacePawn("LP_Dwarf");var human=Person();GenSpawn.Spawn(human,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);
   Check(Near(dwarf.BodySize,human.BodySize*.8f) && Near(RootScale(dwarf),RootScale(human)*.85f),"dwarf is 80% real size and drawn at 85%");
   // 与同环境的普通人比：完整模组列表里其他模组可能改了搬运量基础值。
   float dwarfCarry=dwarf.GetStatValue(StatDefOf.CarryingCapacity)/dwarf.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation),humanCarry=human.GetStatValue(StatDefOf.CarryingCapacity)/human.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
   // 1.20.0：完整模组列表里有模组给搬运量加了不随体型缩放的固定值，总量比例不再是 72%；精确检查只看本模组自己的 ×90% 搬运倍率（体型 80% 上面已查）。
   Check(Near(SmallBody.Carry(dwarf),.9f) && Near(SmallBody.Carry(human),1f) && StatDefOf.CarryingCapacity.parts.Any(p=>p is StatPart_SmallBodyCarry) && StatDefOf.CarryingCapacity.Worker.GetExplanationFull(StatRequest.For(dwarf),ToStringNumberSense.Absolute,dwarf.GetStatValue(StatDefOf.CarryingCapacity)).Contains("小体型搬运"),"dwarf stature applies its own x90% carrying factor on top of the 80% body size");
   Check(dwarfCarry<humanCarry*.85f && (Math.Abs(dwarfCarry/humanCarry-.8f*.9f)<.01f || FullModList),"dwarf carrying capacity is about 72% of a human's before manipulation: "+dwarfCarry+" vs "+humanCarry);
   Check(dwarf.genes.AddictionChanceFactor(ChemicalDefOf.Alcohol)==0 && human.genes.AddictionChanceFactor(ChemicalDefOf.Alcohol)>0,"dwarven liver makes alcohol addiction impossible");
   var liver=dwarf.genes.GetFirstGeneOfType<Gene_DwarfLiver>();var hang=dwarf.health.AddHediff(Gene_DwarfLiver.Hangover);hang.Severity=1f;
   liver.HalveHangover();liver.HalveHangover();
   Check(Near(hang.Severity,.5f),"hangover is halved exactly once");
   dwarf.health.RemoveHediff(hang);
   var beer=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Beer"));GenSpawn.Spawn(beer,dwarf.Position,map);beer.Ingested(dwarf,0);
   Check(dwarf.needs.mood.thoughts.memories.GetFirstMemoryOfDef(Gene_DwarfLiver.Courage)!=null,"drinking beer gives the dwarf liquid courage");
   foreach(var h in dwarf.health.hediffSet.hediffs.Where(h=>h.def.defName=="AlcoholHigh" || h.def.defName=="AlcoholTolerance").ToList())dwarf.health.RemoveHediff(h);
   var stone=dwarf.genes.GetFirstGeneOfType<Gene_DwarfStoneEndurance>();var cell=dwarf.Position;var oldRoof=cell.GetRoof(map);
   map.roofGrid.SetRoof(cell,null);stone.Sync();float mining=dwarf.GetStatValue(StatDefOf.MiningSpeed),labor=dwarf.GetStatValue(DefDatabase<StatDef>.GetNamed("GeneralLaborSpeed"));
   Check(dwarf.health.hediffSet.GetFirstHediffOfDef(Gene_DwarfStoneEndurance.Under)==null,"no stone endurance under open sky");
   map.roofGrid.SetRoof(cell,RoofDefOf.RoofRockThick);stone.Sync();
   Check(dwarf.health.hediffSet.GetFirstHediffOfDef(Gene_DwarfStoneEndurance.Under)!=null && DefDatabase<ThoughtDef>.GetNamed("LP_DwarfUnderMountain").Worker.CurrentState(dwarf).Active,"thick rock roof gives the under-mountain mood");
   Check(Near(dwarf.GetStatValue(StatDefOf.MiningSpeed),mining*1.1f) && Near(dwarf.GetStatValue(DefDatabase<StatDef>.GetNamed("GeneralLaborSpeed")),labor*1.1f),"under the mountain mining and labor speed rise by 10%");
   // 出生点本身可能就在山体下：先改成无顶验证，再恢复原屋顶。
   map.roofGrid.SetRoof(cell,null);stone.Sync();
   Check(dwarf.health.hediffSet.GetFirstHediffOfDef(Gene_DwarfStoneEndurance.Under)==null,"leaving the thick roof removes the bonus");
   map.roofGrid.SetRoof(cell,oldRoof);
   // 矮人打巨魔 +10%：同一 verb 打巨魔与打普通人的平均伤害比。
   var troll=RacePawn("LP_Troll");var victim=Person();GenSpawn.Spawn(victim,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);
   Check(CommonRaces.IsTroll(troll) && Near(CommonRaces.MeleeFactor(dwarf,troll,1f,out bool _),1.1f) && Near(CommonRaces.MeleeFactor(dwarf,victim,1f,out bool _),1f),"dwarf melee factor is 110% only against trolls");
   var verb=DamageVerb(dwarf,victim);Check(verb!=null,"dwarf has a damaging melee verb");
   float vsTroll=MeleeSample(verb,troll,1000,93),vsHuman=MeleeSample(verb,victim,1000,93);
   Check(Math.Abs(vsTroll/vsHuman-1.1f)<.02f,"dwarf deals 10% more real melee damage to trolls: "+(vsTroll/vsHuman).ToString("0.000"));
   dwarf.Destroy();human.Destroy();troll.Destroy();victim.Destroy();
  }

  void CheckCommonRaceRelations(){
   var tracker=RacialRelations.Tracker;var elfDwarf=DefDatabase<RacialRelationDef>.GetNamed("LP_Relation_ElfDwarf");var dwarfElf=DefDatabase<RacialRelationDef>.GetNamed("LP_Relation_DwarfElf");var dwarfGiant=DefDatabase<RacialRelationDef>.GetNamed("LP_Relation_DwarfFireGiant");
   Check(new[]{elfDwarf,dwarfElf,dwarfGiant}.All(r=>r.enabled && !r.ConfigErrors().Any()) && elfDwarf.daysToAccept==60 && dwarfGiant.daysToAccept<=0,"common race relations are enabled and valid");
   var elf=RacePawn("LP_Elf");var dwarf=RacePawn("LP_Dwarf");tracker.Census(map).Rebuild(map);
   var s1=elfDwarf.thought.Worker.CurrentState(elf);var s2=dwarfElf.thought.Worker.CurrentState(dwarf);
   Check(s1.Active && s1.StageIndex==0 && s2.Active && s2.StageIndex==0 && elfDwarf.thought.stages[0].baseMoodEffect==-2,"elf and dwarf colonists start at -2 toward each other");
   Check(!dwarfGiant.thought.Worker.CurrentState(dwarf).Active,"no fire giant, no grudge mood");
   tracker.Accumulate(elfDwarf.AcceptTicks);
   Check(elfDwarf.thought.Worker.CurrentState(elf).StageIndex==1 && dwarfElf.thought.Worker.CurrentState(dwarf).StageIndex==1 && elfDwarf.thought.stages[1].baseMoodEffect==2,"after 60 days together they become old comrades at +2");
   var giant=RacePawn("LP_FireGiant");tracker.Census(map).Rebuild(map);
   Check(dwarfGiant.thought.Worker.CurrentState(dwarf).Active && dwarfGiant.thought.stages[0].baseMoodEffect==-3,"a dwarf living with a fire giant gets the -3 ancient grudge");
   var other=Find.FactionManager.AllFactions.FirstOrDefault(f=>f!=Faction.OfPlayer && !f.Hidden && f.def.humanlikeFaction);
   if(other!=null){giant.SetFaction(other);tracker.Census(map).Rebuild(map);Check(!dwarfGiant.thought.Worker.CurrentState(dwarf).Active,"a fire giant visitor from another faction does not count");}
   elf.Destroy();dwarf.Destroy();giant.Destroy();
  }

  void PrepareCommonRaces116Save(){
   var orc=RacePawn("LP_HalfOrc");var e=orc.health.AddHediff(CommonRaces.Endurance);e.TryGetComp<HediffComp_Disappears>().ticksToDisappear=22222;orcSaveId=orc.ThingID;orcEnduranceLeft=22222;orcGeneCount=orc.genes.GenesListForReading.Count;
   var elf=RacePawn("LP_Elf");elf.genes.GetFirstGeneOfType<Gene_ElfTrance>().asleepTicks=3333;elfSaveId=elf.ThingID;elfSavedAsleep=3333;elfGeneCount=elf.genes.GenesListForReading.Count;
   var dwarf=RacePawn("LP_Dwarf");dwarf.genes.GetFirstGeneOfType<Gene_DwarfLiver>().halvedHangover=777;dwarfSaveId=dwarf.ThingID;dwarfSavedHangover=777;dwarfGeneCount=dwarf.genes.GenesListForReading.Count;
  }
  void CheckCommonRaces116AfterLoad(){
   map=Find.CurrentMap;Pawn Find1(string id)=>map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==id);
   var orc=Find1(orcSaveId);var elf=Find1(elfSaveId);var dwarf=Find1(dwarfSaveId);
   Check(orc.genes.Xenotype.defName=="LP_HalfOrc" && elf.genes.Xenotype.defName=="LP_Elf" && dwarf.genes.Xenotype.defName=="LP_Dwarf" && orc.genes.GenesListForReading.Count==orcGeneCount && elf.genes.GenesListForReading.Count==elfGeneCount && dwarf.genes.GenesListForReading.Count==dwarfGeneCount,"common race xenotypes and genes survive save/load");
   var e=orc.health.hediffSet.GetFirstHediffOfDef(CommonRaces.Endurance);
   Check(e!=null && Math.Abs(e.TryGetComp<HediffComp_Disappears>().ticksToDisappear-orcEnduranceLeft)<=5,"half-orc endurance timer survives save/load");
   Check(elf.genes.GetFirstGeneOfType<Gene_ElfTrance>().asleepTicks==elfSavedAsleep && dwarf.genes.GetFirstGeneOfType<Gene_DwarfLiver>().halvedHangover==dwarfSavedHangover,"elf trance and dwarf liver state survive save/load");
   var human=Person();GenSpawn.Spawn(human,dwarf.Position,map);
   Check(Near(dwarf.BodySize,human.BodySize*.8f) && Near(DNDMind.Resistance(elf),.5f),"loaded dwarf keeps small body and loaded elf keeps mind resistance");
   orc.Destroy();elf.Destroy();dwarf.Destroy();human.Destroy();
  }
 }
}
