using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // 1.38.0 流浆人检查（Test.ps1 -Only Oozefolk）。对应需求第六节 1、4、5、6、9、10、13，另加可选的 2（伤害倍率）、11（蝌蚪）、12（屠宰）。
 // 剧本、兵种、配方、贴图不自动测。
 public partial class LichChecks {
  readonly List<Thing> oMade=new List<Thing>();
  static string oSaveId;static float oSavePoints;static int oSaveCooldown,oSaveSluggish;
  const int OFakeSplitId=987654321;
  Pawn OozePawn(){var p=RacePawn("LP_Oozefolk");oMade.Add(p);return p;}
  Pawn OHuman(IntVec3 near){var p=Person();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(near,map,3),map);oMade.Add(p);return p;}
  static float OInjury(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity);
  static void OHeal(Pawn p){foreach(var h in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(h);}
  static float OSpeed(Pawn p)=>StatDefOf.MoveSpeed.Worker.GetValue(StatRequest.For(p));
  // 5×3 的空地中心（放墙和门用）。
  IntVec3 OOpenArea()=>map.AllCells.Where(c=>c.x>15 && c.z>15 && c.x<map.Size.x-15 && c.z<map.Size.z-15).Where(c=>CellRect.CenteredOn(c,3).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map)==null && x.GetTerrain(map).passability==Traversability.Standable && !x.GetTerrain(map).IsWater && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth))).OrderBy(c=>c.DistanceToSquared(map.Center)).First();

  void CheckOozefolk(){
   var before=new HashSet<Thing>(map.listerThings.AllThings);
   try{OozeGeneration();OozeDamage();OozeEngulf();OozeFission();OozeSeep();OozeManipulation();OozeChill();OozeTadpoleAndButcher();OozeScenarioPart();}
   finally{
    foreach(var t in oMade)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    oMade.Clear();
    foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList())if(!t.Destroyed)t.Destroy();
   }
  }

  // 1 生成：本源、四种肤色之一、瘦/标准体型、光头，代谢 -2～0；原生质 30/100；五个能力；基因都可提取。
  void OozeGeneration(){
   // 本版新增的定义都没有配置错误（完整套 Checks.cs 第 43、44 行对 LP_ 物品和配方做同样的检查）。
   var newDefs=new List<Def>();
   foreach(var n in new[]{"LP_ProtoplasmGel","LP_GelDressing","LP_StabilizerBand"})newDefs.Add(DefDatabase<ThingDef>.GetNamed(n));
   foreach(var n in new[]{"LP_MakeGelDressing","LP_MakeAcidVialGel","LP_MakeTanglefootBagGel"})newDefs.Add(DefDatabase<RecipeDef>.GetNamed(n));
   newDefs.AddRange(DefDatabase<RecipeDef>.AllDefs.Where(r=>r.ProducedThingDef==Oozefolk.Band));
   newDefs.AddRange(DefDatabase<GeneDef>.AllDefs.Where(g=>g.defName.StartsWith("LP_Ooze") || Oozefolk.SkinNames.Contains(g.defName)));
   newDefs.AddRange(DefDatabase<AbilityDef>.AllDefs.Where(a=>a.defName.StartsWith("LP_Ooze")));
   newDefs.AddRange(DefDatabase<HediffDef>.AllDefs.Where(h=>h.defName.StartsWith("LP_Ooze")));
   newDefs.Add(Oozefolk.Xenotype);newDefs.Add(DefDatabase<PawnKindDef>.GetNamed("LP_FC_OozeWanderer"));newDefs.Add(DefDatabase<ScenarioDef>.GetNamed("LP_OozeScenario"));newDefs.Add(DefDatabase<ThoughtDef>.GetNamed("LP_WitnessedEngulf"));newDefs.Add(Oozefolk.Acid);
   var errors=newDefs.SelectMany(d=>d.ConfigErrors().Select(e=>d.defName+": "+e)).ToList();
   var scen=DefDatabase<ScenarioDef>.GetNamed("LP_OozeScenario");errors.AddRange(scen.scenario.AllParts.SelectMany(p=>p.ConfigErrors()));
   Check(newDefs.Count>=25 && errors.Count==0,"all "+newDefs.Count+" new oozefolk defs pass ConfigErrors()"+(errors.Count==0?"":": "+string.Join("; ",errors)));
   var paras=scen.description.Split(new[]{"\n\n"},StringSplitOptions.RemoveEmptyEntries);
   Check(paras.Length==3 && paras[2].StartsWith("注意") && scen.scenario.AllParts.Where(p=>p.def.defName.StartsWith("LP_")).All(p=>p.def.selectionWeight==0 && !p.Summary(scen.scenario).NullOrEmpty()),"scenario LP_OozeScenario follows the race-scenario format");
   var xeno=Oozefolk.Xenotype;
   Check(xeno!=null && xeno.inheritable && Near(xeno.factionlessGenerationWeight,.5f) && xeno.genes.Contains(Oozefolk.Core),"oozefolk xenotype is inheritable, factionless weight 0.5, with ooze core");
   var skins=new HashSet<GeneDef>();
   for(int i=0;i<12;i++){
    var p=OozePawn();var mine=p.genes.GenesListForReading.Where(g=>Oozefolk.Skins.Contains(g.def)).ToList();
    var body=p.story.bodyType;
    Check(Oozefolk.Is(p) && p.genes.Xenotype==xeno && mine.Count==1 && (body==BodyTypeDefOf.Thin || body==BodyTypeDefOf.Male || body==BodyTypeDefOf.Female) && p.story.hairDef==HairDefOf.Bald,"generated oozefolk "+i+" has the core, one of four skins ("+(mine.FirstOrDefault()?.def.defName??"none")+"), thin/standard body ("+body.defName+") and no hair");
    skins.Add(mine[0].def);
   }
   Check(skins.Count>=2,"oozefolk skins vary at birth ("+skins.Count+" of 4 in 12 pawns)");
   int defMet=xeno.genes.Sum(g=>g.biostatMet);var p0=(Pawn)oMade[0];int met=p0.genes.GenesListForReading.Where(g=>!g.Overridden).Sum(g=>g.def.biostatMet);
   Check(defMet>=-2 && defMet<=0 && met==defMet,"oozefolk metabolism is "+met+" (xenotype genes "+defMet+")");
   Check(Near(Oozefolk.Points(p0),30f) && Near(Oozefolk.MaxPoints(p0),100f),"protoplasm starts at 30 of 100: "+Oozefolk.Points(p0)+"/"+Oozefolk.MaxPoints(p0));
   Check(new[]{"LP_OozeEngulf","LP_OozeFission","LP_OozeSeep","LP_OozeReshape","LP_OozeExtractGel"}.All(n=>p0.abilities.GetAbility(Races124.Ability(n))!=null),"oozefolk have engulf, fission, seep, reshape and extract gel");
   var custom=xeno.genes.Where(g=>g.defName.StartsWith("LP_Ooze") && g.defName!="LP_OozeSheen").ToList();
   Check(custom.Count==8 && custom.All(g=>g.selectionWeight==0 && GeneWorkbench.Extractable(g)),"all 8 oozefolk genes are non-random and extractable");
  }

  // 2 伤害：钝击×0.5、强酸×0.25、火焰×1.5、寒冷×1.5（实际伤害对比普通人）；出血×0.5。
  void OozeDamage(){
   var ooze=(Pawn)oMade[0];var human=OHuman(ooze.Position);
   // 去掉特质（坚韧等会改受伤倍率）和衣服（护甲），只比较种族本身。
   foreach(var p in new[]{ooze,human}){foreach(var t in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(t);p.apparel?.DestroyAll();}
   Check(Near(Oozefolk.DamageFactor(ooze,DamageDefOf.Blunt),.5f) && Near(Oozefolk.DamageFactor(ooze,DamageDefOf.AcidBurn),.25f) && Near(Oozefolk.DamageFactor(ooze,ItemThrowables.Acid),.25f) && Near(Oozefolk.DamageFactor(ooze,DamageDefOf.Flame),1.5f) && Near(Oozefolk.DamageFactor(ooze,DamageDefOf.Frostbite),1.5f) && Near(Oozefolk.DamageFactor(ooze,DamageDefOf.Cut),1f),"ooze damage factors: blunt 0.5, acid 0.25, flame 1.5, frostbite 1.5, cut 1");
   float Ratio(DamageDef def,float amount){
    foreach(var p in new[]{ooze,human}){OHeal(p);p.TakeDamage(new DamageInfo(def,amount,0f,-1f,null,p.RaceProps.body.corePart));}
    float a=OInjury(ooze),b=OInjury(human);OHeal(ooze);OHeal(human);return b<=0?0:a/b;
   }
   // 钝击会随机转成内部骨骼的伤害（原版），总量不稳定，只查上面的倍率；强酸和烧伤直接比实际伤势。
   float acid=Ratio(DamageDefOf.AcidBurn,8f),burn=Ratio(DamageDefOf.Burn,6f);
   Check(Math.Abs(acid-.25f)<.05f && Math.Abs(burn-1.5f)<.08f,"real damage vs a human: acid x"+acid.ToString("0.00")+", burn x"+burn.ToString("0.00"));
   // 同一处、同样大小的割伤直接加上去（割伤伤害会随机分到别的部位）。
   foreach(var p in new[]{ooze,human}){var cut=HediffMaker.MakeHediff(HediffDefOf.Cut,p,p.RaceProps.body.corePart);cut.Severity=8f;p.health.AddHediff(cut,p.RaceProps.body.corePart);}
   float bo=ooze.health.hediffSet.BleedRateTotal,bh=human.health.hediffSet.BleedRateTotal;OHeal(ooze);OHeal(human);
   Check(bh>0 && Near(bo/bh,.5f),"ooze bleeds at half rate: "+bo.ToString("0.000")+" vs "+bh.ToString("0.000"));
   Check(ooze.GetStatValue(StatDefOf.ToxicResistance)>=.5f-.001f && Near(ooze.GetStatValue(StatDefOf.Flammability)-human.GetStatValue(StatDefOf.Flammability),.25f),"ooze toxic resistance +50% and flammability +0.25");
  }

  // 4 吞没：人形尸体+25、饱食+50%、尸体消失、衣服掉在原地、看见的殖民者心情-3；动物按体型；同族尸体不能吞。
  void OozeEngulf(){
   var ooze=OozePawn();Oozefolk.SetPoints(ooze,10f);ooze.needs.food.CurLevelPercentage=.2f;
   var victim=OHuman(ooze.Position);victim.SetFactionDirect(null);
   var shirt=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_BasicShirt"),ThingDefOf.Cloth);victim.apparel.Wear(shirt,false);
   var witness=OHuman(ooze.Position);foreach(var t in witness.story.traits.allTraits.ToList())witness.story.traits.RemoveTrait(t);
   var gear=victim.apparel.WornApparel.Cast<Thing>().ToList();bool shirtWorn=gear.Contains(shirt);victim.Kill(null);var corpse=victim.Corpse;var at=corpse.Position;
   float got=Oozefolk.Engulf(ooze,corpse);
   Check(Near(got,25f) && Near(Oozefolk.Points(ooze),35f) && corpse.Destroyed && Near(ooze.needs.food.CurLevelPercentage,.7f),"engulfing a human corpse: +"+got+" protoplasm (now "+Oozefolk.Points(ooze)+"), food "+ooze.needs.food.CurLevelPercentage.ToStringPercent()+", corpse gone");
   Check(shirtWorn && gear.All(a=>a.Spawned && a.Position.InHorDistOf(at,4f)),"the corpse's clothes ("+gear.Count+") drop where it lay: "+string.Join(", ",gear.Select(a=>a.def.defName+(a.Spawned?"@"+a.Position.DistanceTo(at).ToString("0.0"):a.Destroyed?" destroyed":" held by "+a.ParentHolder))));
   Check(witness.needs.mood.thoughts.memories.GetFirstMemoryOfDef(Oozefolk.Witnessed)!=null,"a colonist watching gets the engulf mood");
   var muffalo=PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Muffalo"));GenSpawn.Spawn(muffalo,CellFinder.RandomClosewalkCellNear(ooze.Position,map,3),map);oMade.Add(muffalo);muffalo.Kill(null);
   Oozefolk.SetPoints(ooze,10f);ooze.needs.food.CurLevelPercentage=.1f;
   float animal=Oozefolk.Engulf(ooze,muffalo.Corpse);
   Check(Near(animal,Mathf.Min(30f,muffalo.BodySize*15f)) && Near(ooze.needs.food.CurLevelPercentage,.1f+Mathf.Min(.6f,muffalo.BodySize*.3f)),"engulfing an animal (size "+muffalo.BodySize+") gives "+animal+" protoplasm");
   var kin=OozePawn();kin.Kill(null);
   Check(Oozefolk.CannotEngulf(ooze,kin.Corpse)!=null,"oozefolk corpses cannot be engulfed");
  }

  // 5 分裂：-80 原生质、-25% 饱食、新生儿同异种型同内源基因（外源不复制）、父体是唯一亲代、父体分裂虚弱、冷却按设置。
  void OozeFission(){
   var parent=OozePawn();Oozefolk.SetPoints(parent,90f);parent.needs.food.CurLevelPercentage=.8f;
   var pretty=DefDatabase<GeneDef>.GetNamedSilentFail("Beauty_Pretty");if(pretty!=null)parent.genes.AddGene(pretty,true);
   var baby=Oozefolk.Fission(parent);
   Check(baby!=null && baby.Spawned && Near(Oozefolk.Points(parent),10f) && Near(parent.needs.food.CurLevelPercentage,.55f) && Items.Has(parent,"LP_OozeSplitWeak"),"fission costs 80 protoplasm and 25% food and leaves the parent weakened");
   oMade.Add(baby);
   var pe=parent.genes.Endogenes.Select(g=>g.def).Where(d=>!Oozefolk.Skins.Contains(d)).OrderBy(d=>d.defName).ToList();
   var be=baby.genes.Endogenes.Select(g=>g.def).Where(d=>!Oozefolk.Skins.Contains(d)).OrderBy(d=>d.defName).ToList();
   Check(baby.genes.Xenotype==parent.genes.Xenotype && pe.SequenceEqual(be) && baby.genes.Endogenes.Count(g=>Oozefolk.Skins.Contains(g.def))==1 && !baby.genes.Xenogenes.Any() && Oozefolk.Is(baby),"the newborn has the same xenotype and endogenes ("+be.Count+"), a random ooze skin and no xenogenes");
   Check((baby.DevelopmentalStage==DevelopmentalStage.Newborn || baby.DevelopmentalStage==DevelopmentalStage.Baby) && baby.ageTracker.AgeBiologicalYearsFloat<.01f && baby.Faction==parent.Faction && baby.relations.DirectRelations.Count(r=>r.def==PawnRelationDefOf.Parent)==1 && baby.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Parent)==parent,"the newborn is in the parent's faction with the parent as its only parent: stage "+baby.DevelopmentalStage+", faction "+baby.Faction+"/"+parent.Faction+", relations "+string.Join(",",baby.relations.DirectRelations.Select(r=>r.def.defName+":"+r.otherPawn?.LabelShort)));
   Check(OozeState.SplitBorn(baby) && !baby.story.traits.allTraits.Any(t=>BackgroundTraits.IsBackground(t.def)),"split-born babies get no background trait");
   Check(Oozefolk.CannotFission(parent)!=null && Oozefolk.CannotFission(baby)!=null,"fission needs 80 protoplasm and an adult");
   var ab=parent.abilities.GetAbility(Oozefolk.Fissioning);ab.StartCooldown(ab.def.cooldownTicksRange.RandomInRange);
   Check(ab.CooldownTicksRemaining==LichMod.Settings.oozeSplitDays*GenDate.TicksPerDay,"fission cooldown follows the setting ("+LichMod.Settings.oozeSplitDays+" days)");
   Check(parent.GetStatValue(StatDefOf.Fertility)<=.001f,"oozefolk cannot get pregnant the ordinary way");
  }

  // 6 渗过：敌方（锁着的）门另一侧；不能穿墙；点门本身落到另一侧。
  void OozeSeep(){
   var c=OOpenArea();var hostile=Find.FactionManager.AllFactions.FirstOrDefault(f=>!f.IsPlayer && !f.Hidden && f.HostileTo(Faction.OfPlayer));
   Building_Door door=null;
   for(int x=-2;x<=2;x++){
    var cell=new IntVec3(c.x+x,0,c.z);
    if(x==0){door=(Building_Door)ThingMaker.MakeThing(ThingDefOf.Door,ThingDefOf.WoodLog);GenSpawn.Spawn(door,cell,map,WipeMode.Vanish);if(hostile!=null)door.SetFaction(hostile);}
    else GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog),cell,map,WipeMode.Vanish);
   }
   var ooze=OozePawn();Place(ooze,c+IntVec3.South);Oozefolk.SetPoints(ooze,25f);
   Check(hostile==null || !door.PawnCanOpen(ooze),"the hostile door is locked for the ooze");
   bool ok=Oozefolk.Seep(ooze,new LocalTargetInfo(c+IntVec3.North));
   Check(ok && ooze.Position==c+IntVec3.North && Near(Oozefolk.Points(ooze),15f),"seep through a locked door to the other side (10 protoplasm)");
   Place(ooze,new IntVec3(c.x+1,0,c.z-1));string wall=Oozefolk.CannotSeep(ooze,new LocalTargetInfo(new IntVec3(c.x+1,0,c.z+1)),out _);
   Check(wall!=null && wall.Contains("墙"),"seep cannot pass a wall: "+wall);
   Place(ooze,c+IntVec3.North);
   Check(Oozefolk.Seep(ooze,new LocalTargetInfo(door)) && ooze.Position==c+IntVec3.South && Near(Oozefolk.Points(ooze),5f),"targeting the door itself lands on its far side");
   Check(Oozefolk.CannotSeep(ooze,new LocalTargetInfo(c+IntVec3.North),out _)==null && !Oozefolk.Seep(ooze,new LocalTargetInfo(c+IntVec3.North)),"no seep without 10 protoplasm");
  }

  // 9 伪足：双手缺失时操作≥60%（普通人更低）；重塑长回一只手。
  void OozeManipulation(){
   var ooze=OozePawn();var human=OHuman(ooze.Position);
   foreach(var p in new[]{ooze,human})foreach(var hand in p.RaceProps.body.AllParts.Where(x=>x.def.defName=="Hand").ToList())p.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,p,hand),hand);
   float lo=ooze.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation),lh=human.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
   Check(lo>=Oozefolk.ManipulationFloor-.001f && lh<Oozefolk.ManipulationFloor,"pseudopod keeps manipulation at "+lo.ToStringPercent()+" with both hands lost (human "+lh.ToStringPercent()+")");
   var lash=ooze.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("LP_OozeLash"))?.TryGetComp<HediffComp_VerbGiver>();var tool=lash?.Props.tools?.FirstOrDefault();
   Check(tool!=null && Near(tool.power,9f) && tool.extraMeleeDamages?.Any(d=>d.def==Oozefolk.Acid && d.amount==4)==true,"acid pseudopod: 9 blunt + 4 ooze acid");
   Oozefolk.SetPoints(ooze,50f);string fixedPart=Oozefolk.Reshape(ooze);
   int left=ooze.health.hediffSet.GetMissingPartsCommonAncestors().Count(h=>h.Part.def.defName=="Hand");
   Check(fixedPart!=null && left==1 && Near(Oozefolk.Points(ooze),10f),"reshape regrows one hand ("+fixedPart+") for 40 protoplasm");
  }

  // 10 凝滞：-5°C 凝滞（移速×75%），护带把阈值降到 -15°C、上限 120；寒霜迟缓时长×150%。
  void OozeChill(){
   var ooze=OozePawn();float speed=OSpeed(ooze);
   Check(!Oozefolk.SyncChill(ooze,5f) && Oozefolk.SyncChill(ooze,-5f) && Items.Has(ooze,"LP_OozeSluggish") && Items.TicksLeft(ooze,"LP_OozeSluggish")==Oozefolk.SluggishTicks,"ooze turns sluggish at -5 C for 1 hour");
   float slow=OSpeed(ooze);
   Check(Near(slow/speed,.75f),"sluggish move speed x"+(slow/speed).ToString("0.00"));
   var banded=OozePawn();var band=(Apparel)ThingMaker.MakeThing(Oozefolk.Band,DefDatabase<ThingDef>.GetNamed("Leather_Plain"));banded.apparel.Wear(band,false);
   Check(Oozefolk.BandWorn(banded) && !Oozefolk.SyncChill(banded,-5f) && Oozefolk.SyncChill(banded,-20f) && Near(Oozefolk.MaxPoints(banded),120f),"stabilizer band: sluggish only below -15 C, protoplasm max 120");
   var human=OHuman(ooze.Position);Items.Give(human,"LP_FrostSlow",240);Items.Give(ooze,"LP_FrostSlow",240);
   Check(Items.TicksLeft(ooze,"LP_FrostSlow")==360 && Items.TicksLeft(human,"LP_FrostSlow")==240,"winter wolf frost slow lasts 150% on oozefolk");
  }

  // 11 蝌蚪无效；12 屠宰得 3 份凝胶，不出肉和皮。
  void OozeTadpoleAndButcher(){
   var ooze=OozePawn();
   Check(Tadpoles.Immunity(ooze)!=null && Tadpoles.Implant(ooze,null,null,out _)==null && Tadpoles.Of(ooze)==null,"mind flayer tadpoles cannot parasitize oozefolk");
   var butcher=OozePawn();ooze.Kill(null);
   var products=ooze.Corpse.ButcherProducts(butcher,1f).ToList();foreach(var t in products)oMade.Add(t);
   Check(products.Count==1 && products[0].def==Oozefolk.Gel && products[0].stackCount==Oozefolk.ButcherGel,"butchering an ooze corpse gives "+string.Join(",",products.Select(t=>t.def.defName+"x"+t.stackCount)));
  }

  // 剧本组件：第一名流浆人原生质 60；第 6 天塞伊远征军 300 点（不按威胁点数）。只排进队列，不让它落地。
  void OozeScenarioPart(){
   var ooze=OozePawn();var ev=Tribute.Events;int n=ev.scheduledRaids.Count;int now=Find.TickManager.TicksGame;
   var raid=ScenPart_OozeEscape.Apply(map,new List<Pawn>{ooze},60f,6f,300f,"实验池的追捕者");
   try{
    var f=Factions.Of(raid?.faction);
    Check(raid!=null && ev.scheduledRaids.Count==n+1 && raid.faction.defName=="LP_FaerunHostile" && raid.fireTick==now+6*GenDate.TicksPerDay && Near(Oozefolk.Points(ooze),60f) && (f==null || Math.Abs(raid.Points(map,f)-Math.Max(300f,f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)*1.05f))<1f),"ooze escape: starter protoplasm 60, Thay hunters queued for day 6 at fixed 300 points"+(f==null?"":" ("+raid.Points(map,f).ToString("0")+")"));
   }finally{if(raid!=null)ev.scheduledRaids.Remove(raid);}
  }

  // 13 存读档：原生质、渗过冷却、凝滞剩余时间、分裂出生记录都保留。
  void PrepareOozefolkSave(){
   var p=RacePawn("LP_Oozefolk");Oozefolk.SetPoints(p,47f);
   var seep=p.abilities.GetAbility(Oozefolk.Seeping);seep.StartCooldown(1234);
   Oozefolk.SyncChill(p,-5f);var timer=(p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_OozeSluggish")) as HediffWithComps)?.TryGetComp<HediffComp_Disappears>();if(timer!=null)timer.ticksToDisappear=1777;
   OozeState.Get.splitBorn.Add(OFakeSplitId);
   oSaveId=p.ThingID;oSavePoints=Oozefolk.Points(p);oSaveCooldown=seep.CooldownTicksRemaining;oSaveSluggish=Items.TicksLeft(p,"LP_OozeSluggish");
  }
  void CheckOozefolkAfterLoad(){
   var p=map.mapPawns.AllPawns.FirstOrDefault(x=>x.ThingID==oSaveId);
   Check(p!=null && Oozefolk.Is(p),"saved oozefolk reloads");
   int cd=p.abilities.GetAbility(Oozefolk.Seeping).CooldownTicksRemaining,slug=Items.TicksLeft(p,"LP_OozeSluggish");
   Check(Near(Oozefolk.Points(p),oSavePoints) && cd==oSaveCooldown && slug==oSaveSluggish && OozeState.Get.splitBorn.Contains(OFakeSplitId),"save/load keeps protoplasm "+Oozefolk.Points(p)+"/"+oSavePoints+", seep cooldown "+cd+"/"+oSaveCooldown+", sluggish "+slug+"/"+oSaveSluggish+" and split-born records");
   OozeState.Get.splitBorn.Remove(OFakeSplitId);p.Destroy();
  }
 }
}
