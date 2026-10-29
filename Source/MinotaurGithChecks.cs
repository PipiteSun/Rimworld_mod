using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  static string minotaurSaveId,githSaveId;static int minotaurGeneCount,githGeneCount,minotaurSavedCooldown;static QualityCategory githSavedQuality;
  Faction EnemyFaction()=>Find.FactionManager.AllFactions.Where(f=>f!=Faction.OfPlayer && !f.Hidden && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer)).OrderBy(f=>f.def.permanentEnemy?0:1).FirstOrDefault();
  // 一条向东的空直线：length+3 格都可站立、无人、无物品、无建筑、已探索。
  IntVec3 ClearLine(int length)=>map.AllCells.Where(c=>c.x>5 && c.z>5 && c.x+length+3<map.Size.x-5 && c.z<map.Size.z-5 && Enumerable.Range(0,length+3).All(i=>{var x=c+IntVec3.East*i;return x.Standable(map) && !x.Fogged(map) && x.GetFirstPawn(map)==null && x.GetFirstItem(map)==null && x.GetEdifice(map)==null;})).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
  static void Place(Pawn p,IntVec3 c){p.pather?.StopDead();p.Position=c;p.Notify_Teleported(true,true);}
  // 心灵敏感度大于0的普通人（吉斯银剑、夺心魔测试用）。
  Pawn SensitivePawn(){Pawn p=null;for(int i=0;i<20;i++){p?.Destroy();p=SharedPawn();if(p.GetStatValue(StatDefOf.PsychicSensitivity)>.5f)break;}return p;}
  void CheckMinotaurGith(){
   var before=new HashSet<Thing>(map.listerThings.AllThings);var goodwill=Find.FactionManager.AllFactions.Where(f=>!f.IsPlayer && f.def.humanlikeFaction).ToDictionary(f=>f,f=>f.PlayerGoodwill);
   CheckMinotaurGithDefs();CheckBullRush();CheckMinotaurBody();CheckSilverSword();CheckAstralStep();CheckGithNemesis();CheckMinotaurGithRelations();
   var leftovers=map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Filth) && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList();
   foreach(var t in leftovers)if(!t.Destroyed)t.Destroy();
   foreach(var kv in goodwill)if(kv.Key.PlayerGoodwill!=kv.Value)kv.Key.TryAffectGoodwillWith(Faction.OfPlayer,kv.Value-kv.Key.PlayerGoodwill,false,false);
   Check(map.listerThings.AllThings.All(t=>before.Contains(t) || t is Filth || t is Mote || t.def.category==ThingCategory.Ethereal),"minotaur and githyanki checks leave no extra things behind (cleaned "+leftovers.Count+": "+string.Join(",",leftovers.Select(t=>t.def.defName).Distinct())+")");
  }

  void CheckMinotaurGithDefs(){
   foreach(var name in new[]{"LP_Minotaur","LP_Githyanki"}){
    var x=DefDatabase<XenotypeDef>.GetNamed(name);
    Check(!x.ConfigErrors().Any() && x.inheritable && x.factionlessGenerationWeight==0 && x.genes.All(g=>g!=null),name+" is an inheritable xenotype with resolved genes and no random generation");
    Check(x.genes.All(g=>g.biostatArc==0),name+" uses no archite genes");
    int met=x.genes.Sum(g=>g.biostatMet);
    Check(name=="LP_Minotaur"?met==-1:met==-2,name+" metabolism total "+met);
    Check(ContentFinder<Texture2D>.Get(x.iconPath,false)!=null && x.genes.Where(g=>g.defName.StartsWith("LP_") && g.iconPath!=null).All(g=>ContentFinder<Texture2D>.Get(g.iconPath,false)!=null),name+" xenotype and custom gene icons load");
    foreach(var gene in x.genes.Where(q=>q.defName.StartsWith("LP_")))Check(!gene.ConfigErrors().Any() && (gene.modExtensions.NullOrEmpty() || gene.modExtensions.All(e=>!e.ConfigErrors().Any())),name+" gene validates: "+gene.defName);
    var p=RacePawn(name);
    Check(p.genes.Xenotype==x && x.genes.All(g=>p.genes.GetGene(g)!=null) && x.genes.Where(g=>g.defName.StartsWith("LP_")).All(g=>p.genes.GetGene(g).Active),name+" generates with every xenotype gene and active custom genes");
    p.Destroy();
   }
   foreach(var n in new[]{"Body_Hulk","MeleeDamage_Strong","AptitudeStrong_Melee","Furskin","Hair_DarkBrown","Learning_Slow","Aggression_Aggressive","LP_MinotaurHorns","LP_MinotaurSnout"})Check(Minotaur.Xenotype.genes.Any(g=>g.defName==n),"minotaur xenotype includes "+n);
   foreach(var n in new[]{"PsychicAbility_Enhanced","AptitudeStrong_Melee","Body_Thin","Ears_Pointed","Beard_NoBeardOnly","AptitudePoor_Social","Aggression_Aggressive","LP_GithSkin"})Check(Githyanki.Xenotype.genes.Any(g=>g.defName==n),"githyanki xenotype includes "+n);
   Check(Minotaur.HornsGene.exclusionTags.Contains("Headbone") && DefDatabase<GeneDef>.GetNamed("Headbone_MiniHorns").ConflictsWith(Minotaur.HornsGene),"minotaur horns replace vanilla headbone genes");
   Check(Githyanki.SwordGene.exclusionTags.Contains("DND_GithLineage") && Githyanki.Xenotype.genes.Count(g=>g.exclusionTags?.Contains("DND_GithLineage")==true)==1,"githyanki reserves the DND_GithLineage exclusion tag on exactly one gene");
   foreach(var a in new[]{"LP_BullRush","LP_AstralStep"}){var def=DefDatabase<AbilityDef>.GetNamed(a);Check(!def.ConfigErrors().Any() && ContentFinder<Texture2D>.Get(def.iconPath,false)!=null && def.comps.OfType<CompProperties_MinotaurGithAbility>().Any(),"ability validates with icon: "+a);}
   Check(DefDatabase<AbilityDef>.GetNamed("LP_BullRush").cooldownTicksRange.min==60000 && DefDatabase<AbilityDef>.GetNamed("LP_AstralStep").cooldownTicksRange.min==30000,"bull rush cools down for a day and astral step for 12 hours");
   foreach(var path in new[]{"MinotaurGith/Horns/BullHorns_south","MinotaurGith/Horns/BullHorns_east","MinotaurGith/Horns/BullHorns_north","MinotaurGith/Snout/BullSnout_south","MinotaurGith/Snout/BullSnout_east","MinotaurGith/Weapons/SilverSword"}){var tex=ContentFinder<Texture2D>.Get(path,false);Check(tex!=null && tex.width==128 && tex.height==128,"minotaur/githyanki art loads at vanilla 128px size: "+path);}
   var m=RacePawn("LP_Minotaur");var nodes=RenderNodes(m);
   Check(nodes.Any(n=>n.Props?.texPath=="MinotaurGith/Horns/BullHorns" && n.PrimaryGraphic!=null) && nodes.Any(n=>n.Props?.texPath=="MinotaurGith/Snout/BullSnout" && n.PrimaryGraphic!=null),"minotaur renders custom horns and snout on the head");
   Check(!nodes.Any(n=>n.Props?.texPath!=null && n.Props.texPath.Contains("PigNose")),"minotaur does not also draw the vanilla pig nose");
   Check(m.story.bodyType==BodyTypeDefOf.Hulk && m.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Furskin")),"minotaur has a hulk body with fur");
   Check(m.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_BullRush"))!=null,"minotaur gets bull rush from its gene");
   m.Destroy();
   var g=RacePawn("LP_Githyanki");
   Check(g.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_AstralStep"))!=null && g.story.bodyType==BodyTypeDefOf.Thin,"githyanki is thin and gets astral step from its gene");
   Check(Math.Abs(g.story.SkinColor.r-g.story.SkinColor.b)>.1f,"githyanki skin is yellow-green: "+g.story.SkinColor);
   g.Destroy();
  }

  void CheckBullRush(){
   var enemyFaction=EnemyFaction();Check(enemyFaction!=null,"a hostile faction exists for the charge fixture");
   var start=ClearLine(9);var m=RacePawn("LP_Minotaur");Place(m,start);
   var enemy=SharedPawn();enemy.SetFaction(enemyFaction);Place(enemy,start+IntVec3.East*5);enemy.jobs.StopAll();
   Check(enemy.HostileTo(m),"charge fixture enemy is hostile");
   int hediffsBefore=enemy.health.hediffSet.hediffs.Count;
   var r=Minotaur.Charge(m,start+IntVec3.East*8);
   Check(r.hit==enemy && m.Position==start+IntVec3.East*4,"bull rush stops in front of the first enemy in line: at "+(m.Position-start));
   Check(enemy.Dead || enemy.Position==start+IntVec3.East*7,"charged enemy is knocked back 2 cells: at "+(enemy.Position-start));
   Check(Near(r.damage,Minotaur.ChargeDamage*m.BodySize) && r.damage>19f && r.damage<20f && BodySize.MeleeFactor(m)>1.05f,"charge damage is 15 times the minotaur's body size and not multiplied again by the giant-body melee factor: "+r.damage);
   Check(enemy.Dead || enemy.health.hediffSet.hediffs.Count>hediffsBefore,"charge hurts the enemy");
   Check(enemy.Dead || enemy.stances.stunner.Stunned,"charged enemy is stunned");
   enemy.Destroy();
   // 墙挡路：停在墙前，墙不受损。
   Place(m,start);var wall=GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog),start+IntVec3.East*3,map);wall.SetFaction(Faction.OfPlayer);int hp=wall.HitPoints;
   r=Minotaur.Charge(m,start+IntVec3.East*8);
   Check(r.hit==null && m.Position==start+IntVec3.East*2 && wall.HitPoints==hp && !wall.Destroyed,"bull rush stops at a wall without damaging it");
   wall.Destroy();
   // 友方挡路：停下，不伤人。
   Place(m,start);var friend=SharedPawn();Place(friend,start+IntVec3.East*3);int friendHediffs=friend.health.hediffSet.hediffs.Count;
   r=Minotaur.Charge(m,start+IntVec3.East*8);
   Check(r.hit==null && m.Position==start+IntVec3.East*2 && friend.Position==start+IntVec3.East*3 && friend.health.hediffSet.hediffs.Count==friendHediffs,"bull rush stops before a friendly pawn and does not hit it");
   friend.Destroy();
   // 无阻挡：最多8格。
   Place(m,start);r=Minotaur.Charge(m,start+IntVec3.East*12);
   Check(m.Position==start+IntVec3.East*8,"bull rush travels at most 8 cells: "+(m.Position-start));
   // 能力入口：通过能力组件触发。
   Place(m,start);var enemy2=SharedPawn();enemy2.SetFaction(enemyFaction);Place(enemy2,start+IntVec3.East*3);enemy2.jobs.StopAll();
   var ability=m.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_BullRush"));var comp=ability.EffectComps.OfType<CompMinotaurGithAbility>().Single();
   Check(comp.Invalid(new LocalTargetInfo(start+IntVec3.East*10))!=null && comp.Invalid(new LocalTargetInfo(start+IntVec3.East*6))==null,"bull rush ability rejects targets beyond 8 cells");
   comp.Apply(new LocalTargetInfo(enemy2),LocalTargetInfo.Invalid);
   Check(m.Position==start+IntVec3.East*2 && (enemy2.Dead || enemy2.Position==start+IntVec3.East*5),"bull rush ability component charges and knocks the target back");
   enemy2.Destroy();
   // 犄角近战。
   Check(m.health.hediffSet.GetFirstHediffOfDef(Minotaur.HornWeapon)!=null,"horn gene adds the horn weapon");
   var horn=m.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).FirstOrDefault(v=>v.tool?.label=="犄角");
   var fist=ThingDefOf.Human.tools.First(t=>t.capacities.Contains(DefDatabase<ToolCapacityDef>.GetNamed("Blunt")));
   Check(horn is Verb_MeleeAttackDamage && horn.tool.capacities.Any(c=>c.defName=="Stab") && horn.tool.power>fist.power && horn.tool.cooldownTime>fist.cooldownTime,"horns give a sharp melee verb hitting harder but slower than a fist: "+horn?.tool.power+"/"+horn?.tool.cooldownTime+" vs "+fist.power+"/"+fist.cooldownTime);
   var dummy=SharedPawn();float hornDamage=MeleeSample((Verb_MeleeAttackDamage)horn,dummy,200,77);
   Check(hornDamage>11f,"horn verb deals real damage with strong melee: "+hornDamage);
   m.genes.RemoveGene(m.genes.GetGene(Minotaur.HornsGene));
   Check(m.health.hediffSet.GetFirstHediffOfDef(Minotaur.HornWeapon)==null,"removing the horn gene removes the horn weapon");
   m.Destroy();dummy.Destroy();
  }

  void CheckMinotaurBody(){
   var m=RacePawn("LP_Minotaur");var human=SharedPawn();
   Check(Near(m.BodySize,human.BodySize*1.3f) && Near(RootScale(m),RootScale(human)*1.15f),"minotaur is 130% real size and drawn at 115%");
   float mc=m.GetStatValue(StatDefOf.CarryingCapacity)/m.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation),hc=human.GetStatValue(StatDefOf.CarryingCapacity)/human.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
   // 1.20.0：完整模组列表里有模组给搬运量加了不随体型缩放的固定值；精确比例只在核心环境检查，另外直接核对本模组自己的体型×搬运倍率。
   Check(Math.Abs(SmallBody.Carry(m)*SmallBody.Size(m)-1.5f)<.02f && (FullModList?mc/hc>1.3f && mc/hc<1.7f:Math.Abs(mc/hc-1.5f)<.02f),"minotaur carrying capacity is about 150% of a human's: "+(mc/hc).ToString("0.000"));
   // 饥饿速度随当前饥饿档位变化：统一按“吃饱”档比较。
   float Fed(Pawn p)=>p.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed);
   // 随机特质（美食家等）会改食量：两人都先去掉非基因特质。原版代谢-1本身让食量×125%，体型不影响食量。
   foreach(var q in new[]{m,human})foreach(var tr in q.story.traits.allTraits.Where(x=>x.sourceGene==null).ToList())q.story.traits.RemoveTrait(tr);
   float food=Fed(m)/Fed(human);var app=m.health.hediffSet.GetFirstHediffOfDef(Minotaur.Appetite);m.health.RemoveHediff(app);float native=Fed(m)/Fed(human);Minotaur.Ensure(m,Minotaur.Appetite,true);
   Check(Math.Abs(food-1.5f)<.03f && Math.Abs(food/native-1.2f)<.01f,"minotaur eats 150% as much as a human (metabolism x1.25, appetite x1.2): "+food.ToString("0.000")+" (without appetite "+native.ToString("0.000")+")");
   var maze=m.genes.GetFirstGeneOfType<Gene_MinotaurMaze>();var cell=m.Position;var oldRoof=cell.GetRoof(map);
   map.roofGrid.SetRoof(cell,null);maze.Sync();
   Check(m.health.hediffSet.GetFirstHediffOfDef(Minotaur.MazeHediff)==null,"no maze sense under open sky");
   // 加屋顶会让格子变暗（黑暗也影响移速），所以在同一格比较有无迷宫感。
   map.roofGrid.SetRoof(cell,RoofDefOf.RoofConstructed);maze.Sync();var mh=m.health.hediffSet.GetFirstHediffOfDef(Minotaur.MazeHediff);float fast=m.GetStatValue(StatDefOf.MoveSpeed);
   if(mh!=null){m.health.RemoveHediff(mh);}float speed=m.GetStatValue(StatDefOf.MoveSpeed);maze.Sync();
   Check(mh!=null && (FullModList?fast/speed>1.05f && fast/speed<1.12f:Near(fast,speed*1.1f)) && m.health.hediffSet.GetFirstHediffOfDef(Minotaur.MazeHediff)!=null,"under a roof the minotaur moves 10% faster: "+speed+" -> "+fast);
   map.roofGrid.SetRoof(cell,null);maze.Sync();
   Check(m.health.hediffSet.GetFirstHediffOfDef(Minotaur.MazeHediff)==null,"leaving the roof removes maze sense");
   map.roofGrid.SetRoof(cell,oldRoof);
   // 巨魔并肩：征召时近战+5%。
   var troll=RacePawn("LP_Troll");RacialRelations.Tracker.Census(map).Rebuild(map);
   Check(Near(Minotaur.TrollBond(m),1f),"undrafted minotaur gets no troll bond");
   m.drafter.Drafted=true;troll.drafter.Drafted=true;
   Check(Near(Minotaur.TrollBond(m),1.05f) && Near(Minotaur.TrollBond(troll),1.05f) && Near(Minotaur.TrollBond(human),1f),"drafted minotaur and troll colonists fight 5% harder together");
   var verb=DamageVerb(m,human);float with=MeleeSample(verb,human,500,61);troll.Destroy();RacialRelations.Tracker.Census(map).Rebuild(map);float without=MeleeSample(verb,human,500,61);
   Check(Math.Abs(with/without-1.05f)<.01f,"troll bond raises real melee damage by 5%: "+(with/without).ToString("0.000"));
   m.drafter.Drafted=false;
   m.Destroy();human.Destroy();
  }

  void CheckSilverSword(){
   var sword=Githyanki.Sword;var longsword=DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword");
   Check(!sword.MadeFromStuff && sword.HasComp(typeof(CompQuality)) && sword.IsMeleeWeapon,"silver sword is a fixed-material melee weapon with quality");
   // 原版长剑是按材料缩放的：钢的锐器/钝器伤害倍率为1，所以钢长剑的伤害就是工具的基础值。
   Check(sword.tools.Select(t=>t.power).OrderBy(x=>x).SequenceEqual(longsword.tools.Select(t=>t.power).OrderBy(x=>x)) && sword.tools.Select(t=>t.cooldownTime).OrderBy(x=>x).SequenceEqual(longsword.tools.Select(t=>t.cooldownTime).OrderBy(x=>x)) && Near(ThingDefOf.Steel.GetStatValueAbstract(StatDefOf.SharpDamageMultiplier),1f) && Near(ThingDefOf.Steel.GetStatValueAbstract(StatDefOf.BluntDamageMultiplier),1f),"silver sword tools match a steel longsword");
   Check(!sword.tradeability.TraderCanSell() && sword.tradeability.PlayerCanSell(),"traders never sell silver swords but the player can sell one");
   var recipe=DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.ProducedThingDef==sword);
   Check(recipe!=null && recipe.AllRecipeUsers.Any(u=>u.defName=="LP_RuneAnvil") && recipe.ingredients.Any(i=>i.IsFixedIngredient && i.FixedIngredient==ThingDefOf.Plasteel) && recipe.ingredients.Any(i=>i.IsFixedIngredient && i.FixedIngredient==ThingDefOf.Silver) && recipe.researchPrerequisite?.defName=="LP_RacialBlades" && recipe.researchPrerequisite.prerequisites.Any(r=>r.defName=="LongBlades"),"silver sword is forged at the rune anvil from plasteel and silver after racial blades (long blades)");
   var gith=RacePawn("LP_Githyanki");var human=SensitivePawn();
   foreach(var crafter in new[]{gith,human}){crafter.skills.GetSkill(SkillDefOf.Crafting).Level=12;}
   var bill=new Bill_Production(recipe);
   Check(bill.PawnAllowedToStartAnew(gith) && !bill.PawnAllowedToStartAnew(human),"only a pawn with the silver sword gene may forge the silver sword");
   var flayer=MindFlayerPawn();var victim=SensitivePawn();
   ThingWithComps Arm(Pawn p){var s=(ThingWithComps)ThingMaker.MakeThing(sword);p.equipment.DestroyAllEquipment();p.equipment.AddEquipment(s);return s;}
   Arm(gith);Arm(human);
   float sv=victim.GetStatValue(StatDefOf.PsychicSensitivity),sf=flayer.GetStatValue(StatDefOf.PsychicSensitivity);
   Check(Near(Githyanki.SwordPsychicDamage(gith,victim),8f*Mathf.Min(2.5f,sv)) && Near(Githyanki.SwordPsychicDamage(gith,flayer),16f*Mathf.Min(2.5f,sf)),"attuned silver sword adds 8 x psychic sensitivity, doubled against mind flayers: "+Githyanki.SwordPsychicDamage(gith,victim)+" / "+Githyanki.SwordPsychicDamage(gith,flayer));
   var deaf=SharedPawn();deaf.genes.AddGene(DefDatabase<GeneDef>.GetNamed("PsychicAbility_Deaf"),true);
   // 1.20.0：完整模组列表里失聪基因不一定能把心灵敏感度压到0（有模组加固定值），只在夹具真的失聪时检查。
   Check(MindFlayer.PsychicallyDeaf(deaf)?Githyanki.SwordPsychicDamage(gith,deaf)==0f:FullModList,"psychically deaf targets take no silver sword psychic damage");
   var method=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply");
   List<DamageInfo> Hits(Pawn p,Pawn target){var verb=p.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().First(v=>v.EquipmentSource?.def==sword && v.tool.capacities.Any(c=>c.defName=="Cut"));return ((IEnumerable<DamageInfo>)method.Invoke(verb,new object[]{new LocalTargetInfo(target)})).ToList();}
   var gHits=Hits(gith,flayer);var hHits=Hits(human,flayer);
   Check(gHits.Any(d=>d.Def==Githyanki.Psychic && Near(d.Amount,16f*Mathf.Min(2.5f,sf))),"githyanki silver sword hit includes the psychic damage instance against a mind flayer");
   // 同一个人换成普通品质的钢长剑：剑刃伤害应与银剑相同。
   // 单次近战伤害带随机浮动：用同一随机种子取平均。
   Verb_MeleeAttackDamage CutVerb(Pawn p,Thing weapon)=>p.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().First(v=>v.EquipmentSource==weapon && v.tool.capacities.Any(c=>c.defName=="Cut"));
   var silverHeld=human.equipment.Primary;float silverCut=MeleeSample(CutVerb(human,silverHeld),flayer,200,55);
   var steel=(ThingWithComps)ThingMaker.MakeThing(longsword,ThingDefOf.Steel);human.equipment.Remove(silverHeld);human.equipment.AddEquipment(steel);
   float steelCut=MeleeSample(CutVerb(human,steel),flayer,200,55);
   human.equipment.DestroyEquipment(steel);human.equipment.AddEquipment(silverHeld);
   Check(!hHits.Any(d=>d.Def==Githyanki.Psychic) && Math.Abs(silverCut/steelCut-1f)<.01f,"in non-githyanki hands the silver sword is a plain steel longsword: "+silverCut+" vs "+steelCut);
   int injuries=flayer.health.hediffSet.hediffs.Count(h=>h.def.defName=="LP_PsychicWound");
   flayer.TakeDamage(gHits.First(d=>d.Def==Githyanki.Psychic));
   Check(flayer.Dead || flayer.health.hediffSet.hediffs.Count(h=>h.def.defName=="LP_PsychicWound")>injuries || flayer.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().Any(),"psychic damage leaves a psychic wound");
   var reject=DefDatabase<ThoughtDef>.GetNamed("LP_SilverSwordRejects");
   Check(reject.Worker.CurrentState(human).Active && !reject.Worker.CurrentState(gith).Active && reject.stages[0].baseMoodEffect==-2,"a non-githyanki holding the silver sword feels rejected (-2), a githyanki does not");
   var raider=RacePawn("LP_Githyanki");raider.SetFaction(EnemyFaction());
   Check(!Githyanki.MaybeArm(raider,.9f) && Githyanki.MaybeArm(raider,.1f) && raider.equipment.Primary?.def==sword,"enemy githyanki may be generated holding a silver sword to capture");
   Check(!Githyanki.MaybeArm(gith,.0f),"player githyanki are not re-armed by generation");
   foreach(var p in new[]{gith,human,flayer,victim,deaf,raider})if(!p.Destroyed){p.equipment?.DestroyAllEquipment();p.Destroy();}
  }

  void CheckAstralStep(){
   var start=ClearLine(9);var g=RacePawn("LP_Githyanki");Place(g,start);
   Check(Githyanki.CannotStep(g,start+IntVec3.East*10)!=null && Githyanki.CannotStep(g,start)!=null,"astral step rejects targets beyond 8 cells or its own cell");
   var blocker=SharedPawn();Place(blocker,start+IntVec3.East*5);
   Check(Githyanki.CannotStep(g,start+IntVec3.East*5)!=null,"astral step cannot land on another pawn");
   blocker.Destroy();
   if(g.HasPsylink)g.psychicEntropy.OffsetPsyfocusDirectly(1f);
   Check(Githyanki.Step(g,start+IntVec3.East*7,.05f) && g.Position==start+IntVec3.East*7,"astral step teleports the githyanki to the chosen cell");
   g.ChangePsylinkLevel(1,false);g.psychicEntropy.OffsetPsyfocusDirectly(.5f-g.psychicEntropy.CurrentPsyfocus);
   var comp=g.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_AstralStep")).EffectComps.OfType<CompMinotaurGithAbility>().Single();
   comp.Apply(new LocalTargetInfo(start+IntVec3.East),LocalTargetInfo.Invalid);
   Check(g.Position==start+IntVec3.East && Math.Abs(g.psychicEntropy.CurrentPsyfocus-.45f)<.005f,"astral step ability lands on target and spends 5% psyfocus when psylinked: "+g.psychicEntropy.CurrentPsyfocus+" at "+(g.Position-start)+" psylink="+g.HasPsylink+"/"+g.GetPsylinkLevel()+" cost="+comp.Props.psyfocusCost);
   g.psychicEntropy.OffsetPsyfocusDirectly(-g.psychicEntropy.CurrentPsyfocus);
   Check(!Githyanki.Step(g,start+IntVec3.East*3,.05f) && g.Position==start+IntVec3.East,"astral step fails without enough psyfocus");
   g.Destroy();
  }

  void CheckGithNemesis(){
   var g=RacePawn("LP_Githyanki");var flayer=MindFlayerPawn();var human=SharedPawn();
   Check(Near(DNDMind.Resistance(g),.3f),"githyanki nemesis gives 30% mind resistance");
   Check(Near(Tadpoles.GeneFactor(g),.5f) && Near(Tadpoles.SpeedFactor(g),.7f*.5f),"tadpole ceremorph runs at half speed in a githyanki on top of mind resistance: "+Tadpoles.SpeedFactor(g));
   var t=Tadpoles.Implant(g,null,null,out string fail);Check(t!=null,"githyanki can be infested: "+fail);
   float p0=t.Severity;t.Advance(GenDate.TicksPerDay);
   Check(Math.Abs((t.Severity-p0)-.35f/Mathf.Clamp(LichMod.Settings.ceremorphDays,1,15))<.002f,"one day of tadpole progress in a githyanki is 35% of a human's: "+(t.Severity-p0));
   g.health.RemoveHediff(t);
   var d=new DamageInfo(DamageDefOf.Blunt,10f,0f,-1f,g);flayer.PreApplyDamage(ref d,out bool _);
   var d2=new DamageInfo(DamageDefOf.Blunt,10f,0f,-1f,g);human.PreApplyDamage(ref d2,out bool _);
   Check(Near(d.Amount,12.5f) && Near(d2.Amount,10f),"githyanki deal 25% more damage to mind flayers only: "+d.Amount+" / "+d2.Amount);
   var hit=AccessTools.Method(typeof(Verb_MeleeAttack),"GetNonMissChance");var verb=DamageVerb(g,flayer);
   float vsFlayer=(float)hit.Invoke(verb,new object[]{new LocalTargetInfo(flayer)}),vsHuman=(float)hit.Invoke(verb,new object[]{new LocalTargetInfo(human)});
   g.genes.RemoveGene(g.genes.GetGene(Githyanki.NemesisGene));float plain=(float)hit.Invoke(verb,new object[]{new LocalTargetInfo(flayer)});
   Check(Near(vsFlayer,Mathf.Min(1f,plain+.1f)) && vsFlayer>=vsHuman,"githyanki melee hit chance against mind flayers is +10%: "+plain+" -> "+vsFlayer);
   g.Destroy();flayer.Destroy();human.Destroy();
  }

  void CheckMinotaurGithRelations(){
   var tracker=RacialRelations.Tracker;
   var defs=new[]{"LP_Relation_GithMindFlayer","LP_Relation_MindFlayerGith","LP_Relation_MinotaurHalfOrc","LP_Relation_HalfOrcMinotaur"}.Select(n=>DefDatabase<RacialRelationDef>.GetNamed(n)).ToList();
   Check(defs.All(r=>r.enabled && !r.ConfigErrors().Any()),"minotaur and githyanki racial relations are enabled and valid");
   var g=RacePawn("LP_Githyanki");var flayer=RacePawn("LP_MindFlayer");var m=RacePawn("LP_Minotaur");var orc=RacePawn("LP_HalfOrc");tracker.Census(map).Rebuild(map);
   Check(defs[0].thought.Worker.CurrentState(g).Active && defs[0].thought.stages[0].baseMoodEffect==-8 && defs[1].thought.Worker.CurrentState(flayer).Active && defs[1].thought.stages[0].baseMoodEffect==-6,"githyanki and mind flayer colonists resent each other (-8 / -6)");
   Check(defs[2].thought.Worker.CurrentState(m).Active && defs[3].thought.Worker.CurrentState(orc).Active && defs[2].thought.stages[0].baseMoodEffect==2 && defs[3].thought.stages[0].baseMoodEffect==2,"minotaur and half-orc colonists like each other (+2)");
   flayer.Destroy();orc.Destroy();tracker.Census(map).Rebuild(map);
   Check(!defs[0].thought.Worker.CurrentState(g).Active && !defs[2].thought.Worker.CurrentState(m).Active,"relations end when the other race leaves");
   // 巫妖女王的信徒
   var devotion=DefDatabase<ThoughtDef>.GetNamed("LP_GithSeesLich");
   Check(!devotion.Worker.CurrentState(g).Active,"no lich, no devotion mood");
   var lich=SharedPawn();lich.health.AddHediff(HediffMaker.MakeHediff(Necro.Def<HediffDef>("Lich"),lich));
   Check(Githyanki.IsLich(lich) && devotion.Worker.CurrentState(g).Active && devotion.stages[0].baseMoodEffect==3,"a githyanki living with a lich gets +3");
   Check(!devotion.Worker.CurrentState(m).Active,"other races do not get the devotion mood");
   // 巫妖劝降：同一俘虏、同一狱卒，有基因时这次谈话降低的抵抗×130%。
   var prisoner=RacePawn("LP_Githyanki");var other=OtherFaction()??EnemyFaction();prisoner.SetFaction(other);prisoner.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);
   var worker=InteractionDefOf.RecruitAttempt.Worker;
   float Attempt(Pawn warden){prisoner.guest.resistance=30f;worker.Interacted(warden,prisoner,new List<RulePackDef>(),out _,out _,out _,out _);return 30f-prisoner.guest.resistance;}
   float withGene=Attempt(lich);var devoteeGene=prisoner.genes.GetGene(Githyanki.DevoteeGene);prisoner.genes.RemoveGene(devoteeGene);float withoutGene=Attempt(lich);
   Check(withoutGene>0 && Math.Abs(withGene/withoutGene-1.3f)<.05f,"a lich lowers a githyanki prisoner's resistance 30% faster: "+withGene+" vs "+withoutGene);
   var plain=SharedPawn();float plainNoGene=Attempt(plain);prisoner.genes.AddGene(Githyanki.DevoteeGene,true);float plainWithGene=Attempt(plain);
   Check(plainNoGene>0 && Math.Abs(plainWithGene/plainNoGene-1f)<.05f,"an ordinary warden gets no devotion bonus: "+plainWithGene+" vs "+plainNoGene);
   foreach(var p in new[]{g,m,lich,prisoner,plain})if(!p.Destroyed)p.Destroy();
  }

  void PrepareMinotaurGithSave(){
   var m=RacePawn("LP_Minotaur");var ability=m.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_BullRush"));ability.StartCooldown(12345);minotaurSavedCooldown=ability.CooldownTicksRemaining;minotaurSaveId=m.ThingID;minotaurGeneCount=m.genes.GenesListForReading.Count;
   var g=RacePawn("LP_Githyanki");var s=(ThingWithComps)ThingMaker.MakeThing(Githyanki.Sword);s.TryGetComp<CompQuality>().SetQuality(QualityCategory.Excellent,ArtGenerationContext.Colony);g.equipment.AddEquipment(s);githSavedQuality=QualityCategory.Excellent;githSaveId=g.ThingID;githGeneCount=g.genes.GenesListForReading.Count;
  }
  void CheckMinotaurGithAfterLoad(){
   map=Find.CurrentMap;Pawn Find1(string id)=>map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==id);
   var m=Find1(minotaurSaveId);var g=Find1(githSaveId);var human=Person();GenSpawn.Spawn(human,m.Position,map);
   Check(m.genes.Xenotype==Minotaur.Xenotype && g.genes.Xenotype==Githyanki.Xenotype && m.genes.GenesListForReading.Count==minotaurGeneCount && g.genes.GenesListForReading.Count==githGeneCount,"minotaur and githyanki xenotypes and genes survive save/load");
   Check(Near(m.BodySize,human.BodySize*1.3f) && m.health.hediffSet.GetFirstHediffOfDef(Minotaur.HornWeapon)!=null && m.health.hediffSet.GetFirstHediffOfDef(Minotaur.Appetite)!=null,"loaded minotaur keeps size, horns and appetite");
   Check(Math.Abs(m.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_BullRush")).CooldownTicksRemaining-minotaurSavedCooldown)<=5,"bull rush cooldown survives save/load");
   Check(g.equipment.Primary?.def==Githyanki.Sword && g.equipment.Primary.TryGetComp<CompQuality>().Quality==githSavedQuality && Githyanki.Attuned(g) && Near(DNDMind.Resistance(g),.3f),"loaded githyanki keeps the attuned excellent silver sword and mind resistance");
   g.equipment.DestroyAllEquipment();m.Destroy();g.Destroy();human.Destroy();
  }
 }
}
