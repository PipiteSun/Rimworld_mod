using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 // 1.22.0 物品与炼金的检查：定义与贴图、炼金桌与研究、药水、投掷物、材料、种族装备、卷轴、奇物、默认值、存读档。
 public partial class LichChecks {
  static string itemPotionSaveId;static int itemPotionSavedTicks,itemTonicSavedTicks;
  public static readonly string[] ItemPotionDefs={"LP_PotionHealing","LP_PotionGreaterHealing","LP_PotionGiantStrength","LP_PotionFireResistance","LP_PotionInvisibility","LP_PotionHeroism","LP_PotionMindShield","LP_PotionTrollRegen"};
  public static readonly string[] ItemThrowableDefs={"LP_AlchemistFire","LP_AcidVial","LP_HolyWater","LP_Thunderstone","LP_TanglefootBag","LP_DispelDust"};
  // 一块 (2r+1)×(2r+1) 的空地：可站立、已探索、无人无物无建筑。
  IntVec3 ItemSpot(int r=2)=>map.AllCells.Where(c=>c.x>r+6 && c.z>r+6 && c.x<map.Size.x-r-6 && c.z<map.Size.z-r-6 && CellRect.CenteredOn(c,r).Cells.All(x=>x.Standable(map) && !x.Fogged(map) && x.GetFirstPawn(map)==null && x.GetFirstItem(map)==null && x.GetEdifice(map)==null && !x.Roofed(map))).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
  Pawn ItemPawn(IntVec3 at){var p=SharedPawn();Place(p,at);p.jobs.StopAll();return p;}
  // 注意：存档前不能推进游戏刻，否则重塑池里的造物会提前愈合（重塑检查记录了伤势比例）。
  // 让测试角色原地不动：己方征召，其他人眩晕。
  static void Hold(Pawn p){p.jobs?.StopAll();if(p.drafter!=null && p.Faction==Faction.OfPlayer)p.drafter.Drafted=true;else p.stances?.stunner?.StunFor(900,null,false,false,false);}
  static float StatOf(Pawn p,string stat)=>p.GetStatValue(DefDatabase<StatDef>.GetNamed(stat),true,-1);
  static Thing MakeItem(string def,int count=1){var d=DefDatabase<ThingDef>.GetNamed(def);var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.stackCount=count;return t;}
  // 真正喝下一瓶（走原版 Thing.Ingested，包括药物成分与 outcomeDoers）。
  void Drink(Pawn p,string def){var t=MakeItem(def);GenSpawn.Spawn(t,p.Position,map);t.Ingested(p,0f);if(!t.Destroyed)t.Destroy();}
  static int TicksLeft(Pawn p,string hediff)=>Items.TicksLeft(p,hediff);
  void CleanFires(){foreach(var f in map.listerThings.ThingsOfDef(ThingDefOf.Fire).ToList())f.Destroy();}

  // -lichitemsonly（Test.ps1 -Isekai）：只跑物品与联动检查和存读档。Isekai 会随机改人物数值，老检查里假设原版数值的夹具在这个档里不稳定。
  static bool ItemsOnly=>GenCommandLine.CommandLineArgPassed("lichitemsonly");
  void RunItemsOnly(){
   map=Find.CurrentMap;
   foreach(var def in DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.defName.StartsWith("LP_")))Check(!def.ConfigErrors().Any(),def.defName+" definition: "+string.Join(";",def.ConfigErrors()));
   CheckItems();CheckGunsIsekai();CheckAdventurerIsekai();PrepareItemsSave();
  }
  void CheckItems(){
   var before=new HashSet<Thing>(map.listerThings.AllThings);
   CheckItemDefs();CheckAlchemyTable();CheckPotions();CheckThrowables();CheckMaterials();CheckRaceGear();CheckScrolls();CheckWondrous();CheckItemDefaults();CheckItemTrade();CheckItemIntegrations();CheckBabyFoodMeat();
   CleanFires();
   var leftovers=map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Filth) && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList();
   foreach(var t in leftovers)if(!t.Destroyed)t.Destroy();
   Check(map.listerThings.AllThings.All(t=>before.Contains(t) || t is Filth || t is Mote || t.def.category==ThingCategory.Ethereal),"item checks leave no extra things behind (cleaned "+leftovers.Count+": "+string.Join(",",leftovers.Select(t=>t.def.defName).Distinct())+")");
  }

  // ---------- 定义、贴图、研究 ----------
  void CheckItemDefs(){
   foreach(var name in ItemPotionDefs.Concat(ItemThrowableDefs).Concat(new[]{"LP_AlchemyTable","LP_LavaCrystal"})){
    var d=DefDatabase<ThingDef>.GetNamedSilentFail(name);
    Check(d!=null && !d.ConfigErrors().Any(),"item definition loads: "+name);
    Check(d.graphic!=null && d.graphic!=BaseContent.BadGraphic && d.graphic.MatSingle!=BaseContent.BadMat && d.uiIcon!=null && d.uiIcon!=BaseContent.BadTex,"item has its own texture: "+name+" ("+d.graphicData?.texPath+")");
    Check(d.graphicData.texPath.StartsWith("Items/"),"item texture comes from this mod: "+name);
   }
   foreach(var name in ItemThrowableDefs){
    var d=DefDatabase<ThingDef>.GetNamed(name);var proj=d.Verbs[0].defaultProjectile;
    Check(proj!=null && proj.graphic!=BaseContent.BadGraphic && proj.graphic.MatSingle!=BaseContent.BadMat && proj.graphicData.texPath=="Items/Projectiles/"+name.Substring(3),"throwable has its own flight texture: "+name);
    Check(d.IsRangedWeapon && d.thingCategories.Contains(DefDatabase<ThingCategoryDef>.GetNamed("Grenades")),"throwable is a vanilla-style grenade weapon: "+name);
   }
   var table=DefDatabase<ThingDef>.GetNamed("LP_AlchemyTable");
   foreach(var rot in new[]{Rot4.North,Rot4.East,Rot4.South,Rot4.West})Check(table.graphic.MatAt(rot)!=BaseContent.BadMat,"alchemy table graphic resolves facing "+rot);
   var lava=DefDatabase<ThingDef>.GetNamed("LP_LavaCrystal");Check(lava.graphic is Graphic_StackCount sc && Traverse.Create(sc).Field("subGraphics").GetValue<Graphic[]>().Length>=3,"lava crystal has three stack-count textures");
   foreach(var name in ItemPotionDefs){var d=DefDatabase<ThingDef>.GetNamed(name);Check(d.IsDrug && d.ingestible.drugCategory==DrugCategory.Medical && d.GetCompProperties<CompProperties_Drug>()!=null && ItemPotions.IsPotion(d),"potion is a medical drug that drug policies list: "+name);}
   Check(DrugPolicyHas("LP_PotionHealing"),"default drug policy contains the healing potion");
   var alchemy=DefDatabase<ResearchProjectDef>.GetNamed("LP_DNDAlchemy");var metal=DefDatabase<ResearchProjectDef>.GetNamed("LP_MythicMetallurgy");
   Check(alchemy.baseCost==1200 && alchemy.prerequisites.Count==1 && alchemy.prerequisites[0].defName=="DrugProduction" && alchemy.tab.defName=="LP_FireGiantTech","DND alchemy costs 1200 after drug production in the mod tab");
   Check(metal.baseCost==2500 && metal.prerequisites.Any(r=>r==alchemy) && metal.prerequisites.Any(r=>r.defName=="Smithing") && metal.tab==alchemy.tab,"mythic metallurgy costs 2500 after alchemy and smithing");
   Check(alchemy.tab.label=="DND边缘大陆","mod research tab is renamed to DND边缘大陆");
   Check(table.researchPrerequisites.Contains(alchemy),"alchemy table is unlocked by DND alchemy");
  }
  static bool DrugPolicyHas(string def){var policy=Current.Game.drugPolicyDatabase.DefaultDrugPolicy();var d=DefDatabase<ThingDef>.GetNamed(def);for(int i=0;i<policy.Count;i++)if(policy[i].drug==d)return true;return false;}

  // ---------- 炼金桌 ----------
  void CheckAlchemyTable(){
   var def=DefDatabase<ThingDef>.GetNamed("LP_AlchemyTable");
   Check(def.size==new IntVec2(2,1) && def.costStuffCount==75 && def.costList.Any(c=>c.thingDef==ThingDefOf.Steel && c.count==50) && def.costList.Any(c=>c.thingDef==ThingDefOf.ComponentIndustrial && c.count==2) && def.stuffCategories.Count>=2,"alchemy table is 2x1 and costs 75 stuff, 50 steel and 2 components");
   var expect=new Dictionary<string,(string product,int count,string[] ing)>{
    {"LP_BrewPotionHealing",("LP_PotionHealing",1,new[]{"LP_TrollBlood:1","MedicineHerbal:2"})},
    {"LP_BrewPotionGreaterHealing",("LP_PotionGreaterHealing",1,new[]{"LP_TrollBlood:3","MedicineIndustrial:1"})},
    {"LP_BrewPotionGiantStrength",("LP_PotionGiantStrength",1,new[]{"LP_LavaCrystal:2","MedicineHerbal:1"})},
    {"LP_BrewPotionFireResistance",("LP_PotionFireResistance",1,new[]{"LP_LavaCrystal:1","MedicineHerbal:1"})},
    {"LP_BrewPotionHeroism",("LP_PotionHeroism",1,new[]{"Beer:3","Gold:2"})},
    {"LP_BrewPotionMindShield",("LP_PotionMindShield",3,new[]{"LP_BrainJar:1"})},
    {"LP_BrewPotionTrollRegen",("LP_PotionTrollRegen",1,new[]{"LP_TrollBlood:5"})},
    {"LP_MakeAlchemistFire",("LP_AlchemistFire",1,new[]{"Chemfuel:20","Cloth:10"})},
    {"LP_MakeAcidVial",("LP_AcidVial",1,new[]{"Chemfuel:20","Steel:10"})},
    {"LP_MakeHolyWater",("LP_HolyWater",1,new[]{"Silver:15","MedicineHerbal:2"})},
    {"LP_MakeTanglefootBag",("LP_TanglefootBag",1,new[]{"Cloth:15","Chemfuel:10"})},
   };
   foreach(var kv in expect){
    var r=DefDatabase<RecipeDef>.GetNamed(kv.Key);
    Check(r.products.Count==1 && r.products[0].thingDef.defName==kv.Value.product && r.products[0].count==kv.Value.count,"recipe "+kv.Key+" makes "+kv.Value.count+" "+kv.Value.product);
    Check(kv.Value.ing.All(s=>{var a=s.Split(':');return r.ingredients.Any(i=>i.filter.AllowedThingDefs.Count()==1 && i.filter.AllowedThingDefs.First().defName==a[0] && Math.Abs(i.GetBaseCount()-int.Parse(a[1]))<.01f);}) && r.ingredients.Count==kv.Value.ing.Length,"recipe "+kv.Key+" uses "+string.Join("+",kv.Value.ing));
    Check(r.researchPrerequisite?.defName=="LP_DNDAlchemy" && r.AllRecipeUsers.Contains(def) && r.workSkill==SkillDefOf.Intellectual && r.workSpeedStat.defName=="LP_AlchemySpeed" && Alchemy.IsAlchemy(r),"recipe "+kv.Key+" is an alchemy-table recipe gated by DND alchemy");
   }
   foreach(var n in new[]{"LP_PotionInvisibility","LP_Thunderstone","LP_DispelDust"})Check(!DefDatabase<RecipeDef>.AllDefs.Any(r=>r.products?.Any(p=>p.thingDef.defName==n)==true),"cannot be crafted: "+n);
   // 速度按智识与医疗平均等级：10/10 与 20/0 相同，0/0 更慢。
   var a1=SharedPawn();var a2=SharedPawn();
   a1.skills.GetSkill(SkillDefOf.Intellectual).Level=10;a1.skills.GetSkill(SkillDefOf.Medicine).Level=10;
   a2.skills.GetSkill(SkillDefOf.Intellectual).Level=20;a2.skills.GetSkill(SkillDefOf.Medicine).Level=0;
   Check(Near(Alchemy.SkillFactor(a1),Alchemy.SkillFactor(a2)) && Near(Alchemy.SkillFactor(a1),.3f+.0875f*10),"alchemy skill factor uses the intellectual/medicine average");
   float s1=StatOf(a1,"LP_AlchemySpeed");a1.skills.GetSkill(SkillDefOf.Medicine).Level=0;float s0=StatOf(a1,"LP_AlchemySpeed");
   Check(s0<s1 && Near(s1/s0,(0.3f+.0875f*10)/(0.3f+.0875f*5)),"alchemy speed stat follows the average skill: "+s1+" vs "+s0);
   // 经验在智识和医疗之间平分（只在做炼金配方时）。
   var bill=new Bill_Production(DefDatabase<RecipeDef>.GetNamed("LP_BrewPotionHealing"));var job=JobMaker.MakeJob(JobDefOf.DoBill);job.bill=bill;
   var intel=a2.skills.GetSkill(SkillDefOf.Intellectual);var med=a2.skills.GetSkill(SkillDefOf.Medicine);intel.Level=3;med.Level=3;float i0=MindFlayer.OwnXp(intel),m0=MindFlayer.OwnXp(med);
   a2.jobs.curJob=job;try{a2.skills.Learn(SkillDefOf.Intellectual,100f,false,true);}finally{a2.jobs.curJob=null;}
   Check(Near(MindFlayer.OwnXp(intel)-i0,50f) && Near(MindFlayer.OwnXp(med)-m0,50f),"alchemy work splits learned XP evenly into medicine: "+(MindFlayer.OwnXp(intel)-i0)+"/"+(MindFlayer.OwnXp(med)-m0));
   i0=MindFlayer.OwnXp(intel);a2.skills.Learn(SkillDefOf.Intellectual,100f,false,true);Check(Near(MindFlayer.OwnXp(intel)-i0,100f),"ordinary intellectual XP is not split");
   a1.Destroy();a2.Destroy();
   // 原版工作单：真的在炼金桌上做出一瓶治疗药水。
   Find.ResearchManager.FinishProject(DefDatabase<ResearchProjectDef>.GetNamed("LP_DNDAlchemy"),false,null,false);
   var spot=ItemSpot(3);var bench=(Building_WorkTable)ThingMaker.MakeThing(def,ThingDefOf.Steel);bench.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(bench,spot,map);
   var crafter=ItemPawn(bench.InteractionCell);crafter.workSettings.EnableAndInitialize();crafter.workSettings.SetPriority(WorkTypeDefOf.Crafting,1);crafter.skills.GetSkill(SkillDefOf.Intellectual).Level=8;crafter.skills.GetSkill(SkillDefOf.Medicine).Level=8;
   var blood=MakeItem("LP_TrollBlood",1);GenPlace.TryPlaceThing(blood,bench.InteractionCell+IntVec3.West,map,ThingPlaceMode.Near);var herb=MakeItem("MedicineHerbal",2);GenPlace.TryPlaceThing(herb,bench.InteractionCell+IntVec3.East,map,ThingPlaceMode.Near);
   var recipe=DefDatabase<RecipeDef>.GetNamed("LP_BrewPotionHealing");int work=(int)recipe.workAmount;recipe.workAmount=60;var existing=new HashSet<Thing>(map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("LP_PotionHealing")));
   try{
    var b=new Bill_Production(recipe){repeatMode=BillRepeatModeDefOf.RepeatCount,repeatCount=1};b.SetPawnRestriction(crafter);b.SetStoreMode(BillStoreModeDefOf.DropOnFloor,null);bench.BillStack.AddBill(b);
    foreach(var other in map.mapPawns.FreeColonistsSpawned.Where(x=>x!=crafter).ToList()){other.jobs.StopAll();if(other.drafter!=null)other.drafter.Drafted=true;}
    var giver=new WorkGiver_DoBill{def=DefDatabase<WorkGiverDef>.GetNamed("LP_DoBillsAlchemy")};Job j;FloatMenuMakerMap.makingFor=crafter;try{j=giver.JobOnThing(crafter,bench,true);}finally{FloatMenuMakerMap.makingFor=null;}
    Check(j!=null,"native work giver creates the healing potion job at the alchemy table; "+JobFailReason.Reason);
    crafter.jobs.StartJob(j,JobCondition.InterruptForced);for(int i=0;i<6000 && b.repeatCount>0;i++)Step(1);
    Check(b.repeatCount==0 && blood.Destroyed && herb.Destroyed,"native alchemy bill consumes 1 troll blood and 2 herbal medicine");
    var made=map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("LP_PotionHealing")).Where(t=>!existing.Contains(t)).ToList();
    Check(made.Sum(t=>t.stackCount)==1,"native alchemy bill produces exactly one healing potion");
    foreach(var t in made)t.Destroy();
   }finally{recipe.workAmount=work;foreach(var other in map.mapPawns.FreeColonistsSpawned.ToList())if(other.drafter!=null)other.drafter.Drafted=false;}
   crafter.Destroy();bench.Destroy();
  }

  // ---------- 药水 ----------
  void CheckPotions(){
   var spot=ItemSpot(2);
   // 治疗药水：止血、愈合×3、6小时。
   var p=ItemPawn(spot);var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,p,p.RaceProps.body.corePart);cut.Severity=6;p.health.AddHediff(cut);
   Check(p.health.hediffSet.BleedRateTotal>0,"healing fixture is bleeding");float heal0=p.GetStatValue(StatDefOf.InjuryHealingFactor);
   Drink(p,"LP_PotionHealing");
   Check(p.health.hediffSet.BleedRateTotal<=0.0001f,"healing potion stops all bleeding immediately");
   Check(Near(p.GetStatValue(StatDefOf.InjuryHealingFactor),heal0*3f) && TicksLeft(p,"LP_PotionHealing")==15000,"healing potion triples natural healing for 6 hours");
   Drink(p,"LP_PotionHealing");Check(p.health.hediffSet.hediffs.Count(h=>h.def.defName=="LP_PotionHealing")==1 && TicksLeft(p,"LP_PotionHealing")==15000,"a second healing potion refreshes instead of stacking");
   p.Destroy();
   // 强效：长回一根手指，不长手臂。
   p=ItemPawn(spot);var finger=p.RaceProps.body.AllParts.First(b=>b.def.defName=="Finger");var arm=p.RaceProps.body.AllParts.First(b=>b.def.defName=="Arm" && !IsAncestor(b,finger));
   p.health.AddHediff(HediffDefOf.MissingBodyPart,finger);p.health.AddHediff(HediffDefOf.MissingBodyPart,arm);
   Drink(p,"LP_PotionGreaterHealing");
   Check(!p.health.hediffSet.PartIsMissing(finger) && p.health.hediffSet.PartIsMissing(arm) && Items.Has(p,"LP_PotionHealing"),"greater healing regrows a missing finger but not an arm");
   p.Destroy();
   // 巫妖与造物：不能喝，强行喝下也无效。
   var lich=ItemPawn(spot);lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);
   var bleed=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,lich,lich.RaceProps.body.corePart);bleed.Severity=4;lich.health.AddHediff(bleed);
   var potion=MakeItem("LP_PotionHealing");GenSpawn.Spawn(potion,lich.Position,map);
   Check(!FoodUtility.WillEat(lich,potion,null,true,false) && !FoodUtility.WillEat(lich,potion.def,null,true,false) && !lich.drugs.ShouldTryToTakeScheduledNow(potion.def),"a lich will not drink potions");
   Check(!ItemPotions.Apply(lich,potion.def,"heal",null) && !Items.Has(lich,"LP_PotionHealing") && lich.health.hediffSet.BleedRateTotal>0,"a potion forced on a lich has no effect");
   var skeleton=FactionConstructs.MakeBound("Skeleton",lich,Faction.OfPlayer);GenSpawn.Spawn(skeleton,spot+IntVec3.North,map);
   Check(Items.Undead(skeleton) && !FoodUtility.WillEat(skeleton,potion,null,true,false) && !ItemPotions.Apply(skeleton,potion.def,"heal",null),"a necromantic construct cannot drink or benefit from potions");
   var living=ItemPawn(spot+IntVec3.East);Check(!Items.Undead(living) && ItemPotions.Apply(living,potion.def,"heal",null) && Items.Has(living,"LP_PotionHealing"),"a living colonist benefits from the same potion");
   potion.Destroy();skeleton.Destroy();lich.Destroy();living.Destroy();
   // 巨人之力：近战伤害×1.3、搬运×1.5、渲染×1.1，12小时。
   p=ItemPawn(spot);float dmg=p.GetStatValue(StatDefOf.MeleeDamageFactor),carry=p.GetStatValue(StatDefOf.CarryingCapacity),scale=RootScale(p);
   Drink(p,"LP_PotionGiantStrength");p.Drawer.renderer.SetAllGraphicsDirty();
   Check(Near(p.GetStatValue(StatDefOf.MeleeDamageFactor),dmg*1.3f) && Near(p.GetStatValue(StatDefOf.CarryingCapacity)/carry,1.5f) && TicksLeft(p,"LP_PotionGiantStrength")==30000,"giant strength: melee x1.3 and carrying x1.5 for 12 hours");
   Check(Math.Abs(RootScale(p)/scale-1.1f)<.01f,"giant strength draws the pawn 10% larger: "+RootScale(p)/scale);
   p.Destroy();
   // 抗火：不会着火，火焰伤害-75%，24小时。
   p=ItemPawn(spot);var control=ItemPawn(spot+IntVec3.East*2);
   Drink(p,"LP_PotionFireResistance");
   Check(p.GetStatValue(StatDefOf.Flammability)<=0.001f && control.GetStatValue(StatDefOf.Flammability)>0 && TicksLeft(p,"LP_PotionFireResistance")==60000,"fire resistance makes the drinker non-flammable for 24 hours");
   Check(Near(ItemDamage.Factor(p,DamageDefOf.Flame),.25f) && Near(ItemDamage.Factor(p,DamageDefOf.Burn),.25f) && Near(ItemDamage.Factor(p,DamageDefOf.Cut),1f) && Near(ItemDamage.Factor(p,DefDatabase<DamageDef>.GetNamed("AcidBurn")),1f),"fire resistance reduces only fire damage by 75%");
   float hurt=TotalInjury(p);p.TakeDamage(new DamageInfo(DamageDefOf.Flame,8f,999f,-1f,null,p.RaceProps.body.corePart));float took=TotalInjury(p)-hurt;
   Items.Remove(p,"LP_PotionFireResistance");float cHurt=TotalInjury(p);p.TakeDamage(new DamageInfo(DamageDefOf.Flame,8f,999f,-1f,null,p.RaceProps.body.corePart));float cTook=TotalInjury(p)-cHurt;
   Check(cTook>0 && Near(took/cTook,.25f),"real flame damage is quartered by fire resistance: "+took+" vs "+cTook);
   CleanFires();p.Destroy();control.Destroy();
   // 隐形：2小时隐身，攻击即现形，驱散时现形。
   p=ItemPawn(spot);Drink(p,"LP_PotionInvisibility");
   Check(p.IsPsychologicallyInvisible() && TicksLeft(p,"LP_PotionInvisibility")==5000,"invisibility potion hides the drinker for 2 hours");
   Items.Give(p,"LP_MagicDispelled");Check(!p.IsPsychologicallyInvisible(),"dispel dust forces the invisible drinker visible");Items.Remove(p,"LP_MagicDispelled");Check(p.IsPsychologicallyInvisible(),"invisibility returns when the dispel ends");
   var victim=ItemPawn(spot+IntVec3.East);var verb=p.meleeVerbs.TryGetMeleeVerb(victim);
   AccessTools.Method(typeof(PotionInvisibilityBreak),"Postfix").Invoke(null,new object[]{verb});
   Check(!Items.Has(p,"LP_PotionInvisibility") && !p.IsPsychologicallyInvisible(),"attacking ends the invisibility potion");
   Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Verb),"TryCastNextBurstShot"))?.Postfixes.Any(x=>x.PatchMethod.DeclaringType==typeof(PotionInvisibilityBreak))==true,"invisibility break is hooked into every verb cast");
   victim.Destroy();p.Destroy();
   // 英雄：不精神崩溃、免疫恐惧、意识+10%，8小时。
   p=SensitivePawn();Place(p,spot);p.jobs.StopAll();float cons=p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);Drink(p,"LP_PotionHeroism");
   Check(p.mindState.mentalBreaker.Blocked && TicksLeft(p,"LP_PotionHeroism")==20000,"heroism blocks mental breaks for 8 hours");
   Check(p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness)>cons+.05f,"heroism raises consciousness");
   Check(!p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"测试",true) && !p.InMentalState,"heroism is immune to panic flee");
   var caster=ItemPawn(spot+IntVec3.East*2);Check(!LichSpells.Frighten(caster,p) && !p.InMentalState,"heroism resists the lich fear gaze");
   var coward=SensitivePawn();Place(coward,spot+IntVec3.West*2);coward.SetFaction(EnemyFaction());Check(LichSpells.Frighten(caster,coward) && coward.InMentalState,"fear gaze still frightens someone without heroism");
   coward.Destroy();caster.Destroy();p.Destroy();
   // 心智护盾：+40%，与种族相加，最多95%。
   p=ItemPawn(spot);Drink(p,"LP_PotionMindShield");Check(Near(DNDMind.Resistance(p),.4f) && TicksLeft(p,"LP_PotionMindShield")==30000,"mind shield gives an ordinary pawn 40% mind resistance for 12 hours");
   p.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_ElfBloodline"),true);Check(Near(DNDMind.Resistance(p),.9f),"mind shield adds to the elf bloodline: "+DNDMind.Resistance(p));
   p.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_GithNemesis"),true);Check(Near(DNDMind.Resistance(p),.95f),"mind shield stacking is capped at 95%: "+DNDMind.Resistance(p));
   p.Destroy();
   // 巨魔再生剂：3天，饥饿×1.5，饱食时慢慢愈合，不长断肢；结束时10%血瘾。
   p=ItemPawn(spot);float hunger=p.health.hediffSet.HungerRateFactor;Drink(p,"LP_PotionTrollRegen");
   var tonic=(Hediff_TrollTonic)p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_TrollRegenTonic"));
   Check(tonic!=null && TicksLeft(p,"LP_TrollRegenTonic")==180000 && Near(p.health.hediffSet.HungerRateFactor,hunger*1.5f),"troll regen tonic lasts 3 days and raises hunger by 50%");
   p.needs.food.CurLevelPercentage=1f;var wound=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,p,p.RaceProps.body.corePart);wound.Severity=5;p.health.AddHediff(wound);
   var toe=p.RaceProps.body.AllParts.First(b=>b.def.defName=="Toe");p.health.AddHediff(HediffDefOf.MissingBodyPart,toe);
   Check(Near(tonic.Heal(250),ItemPotions.TonicHealPerInterval) && Near(wound.Severity,5-ItemPotions.TonicHealPerInterval),"well-fed tonic drinker heals at a quarter of troll speed");
   for(int i=0;i<50;i++)tonic.Heal(2500);Check(p.health.hediffSet.PartIsMissing(toe),"troll tonic never regrows missing parts");
   p.needs.food.CurLevelPercentage=.3f;Check(tonic.Heal(250)==0,"troll tonic pauses when hunger is below 50%");p.needs.food.CurLevelPercentage=1f;
   Items.Give(p,"LP_TrollBurned");Check(tonic.Heal(250)==0,"troll tonic pauses while burned");Items.Remove(p,"LP_TrollBurned");
   Check(Troll.Gene(p)==null,"troll tonic grants no troll gene and therefore no revival");
   var addict=ItemPawn(spot+IntVec3.East);Check(ItemPotions.TryAddict(addict,.05f) && addict.health.hediffSet.HasHediff(Items.Hediff("LP_TrollBloodAddiction")) && addict.needs.AllNeeds.Any(n=>n.def.defName=="LP_Chemical_TrollBlood"),"a roll under 10% gives troll blood addiction with its chemical need; dead="+addict.Dead+" hediffs="+string.Join(",",addict.health.hediffSet.hediffs.Select(h=>h.def.defName))+" needs="+string.Join(",",addict.needs.AllNeeds.Select(n=>n.def.defName))+" ideo="+addict.Ideo?.name+" traits="+string.Join(",",addict.story.traits.allTraits.Select(x=>x.def.defName))+" faction="+addict.Faction);
   var clean=ItemPawn(spot+IntVec3.West);Check(!ItemPotions.TryAddict(clean,.5f) && !clean.health.hediffSet.HasHediff(Items.Hediff("LP_TrollBloodAddiction")),"a roll above 10% gives no addiction");
   var need=(Need_Chemical)addict.needs.AllNeeds.First(n=>n.def.defName=="LP_Chemical_TrollBlood");need.CurLevel=.1f;Drink(addict,"LP_PotionTrollRegen");Check(need.CurLevel>.9f,"another tonic satisfies the troll blood need");
   var rolls=Enumerable.Range(0,4000).Count(i=>Rand.Value<ItemPotions.TonicAddictionChance);Check(rolls>300 && rolls<500,"addiction roll threshold is 10% ("+rolls+"/4000)");
   addict.Destroy();clean.Destroy();p.Destroy();
  }
  static bool IsAncestor(BodyPartRecord a,BodyPartRecord b){for(var x=b.parent;x!=null;x=x.parent)if(x==a)return true;return false;}
  static float TotalInjury(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity);

  // ---------- 投掷物 ----------
  void Launch(string weapon,Pawn launcher,LocalTargetInfo target){
   var proj=DefDatabase<ThingDef>.GetNamed(weapon).Verbs[0].defaultProjectile;
   var t=(Projectile)GenSpawn.Spawn(proj,launcher.Position,map);t.Launch(launcher,launcher.DrawPos,target,target,ProjectileHitFlags.All,false,null,null);
   for(int i=0;i<300 && !t.Destroyed;i++)Step(1);Step(30);
  }
  void CheckThrowables(){
   var spot=ItemSpot(4);var thrower=ItemPawn(spot+IntVec3.West*4);Hold(thrower);
   // 炼金之火：巨魔被火烧→再生中断6小时、死后不复活。
   var troll=RacePawn("LP_Troll");Place(troll,spot);Hold(troll);
   Launch("LP_AlchemistFire",thrower,troll);
   var gene=Troll.Gene(troll);int now=Find.TickManager.TicksGame;
   Check(gene.suppressedUntilTick>now+14000 && Items.Has(troll,"LP_TrollBurned"),"alchemist fire marks the troll as burned and stops regeneration for 6 hours; until="+(gene.suppressedUntilTick-now)+" pos="+troll.Position+"/"+spot+" burning="+troll.IsBurning()+" hediffs="+string.Join(",",troll.health.hediffSet.hediffs.Select(h=>h.def.defName)));
   CleanFires();troll.Kill(null);Check(troll.Dead && gene.reviveTick<0,"a troll killed while burned by alchemist fire does not revive");
   troll.Corpse?.Destroy();
   // 强酸瓶：酸蚀标记，阻断再生；服装损伤×3。
   // 脱掉随机生成的衣服：强酸按锐器护甲结算，偶尔会被衣服整个挡掉（伤害为0就不会留下酸蚀）。
   troll=RacePawn("LP_Troll");Place(troll,spot);Hold(troll);troll.apparel?.DestroyAll();
   Launch("LP_AcidVial",thrower,troll);gene=Troll.Gene(troll);now=Find.TickManager.TicksGame;
   Check(Items.Has(troll,"LP_TrollAcid") && gene.suppressedUntilTick>now+14000,"acid vial marks the troll with acid and stops regeneration");
   troll.Kill(null);Check(troll.Dead && gene.reviveTick<0,"a troll killed while acid-burned does not revive");troll.Corpse?.Destroy();
   var a=ItemPawn(spot+IntVec3.North*3);var b=ItemPawn(spot+IntVec3.South*3);
   var va=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_FlakVest"));var vb=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_FlakVest"));a.apparel.Wear(va);b.apparel.Wear(vb);
   int ha=va.HitPoints,hb=vb.HitPoints;va.TakeDamage(new DamageInfo(ItemThrowables.Acid,5f));vb.TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamed("AcidBurn"),5f));
   Check(ha-va.HitPoints==15 && hb-vb.HitPoints==5,"acid vial deals triple damage to worn apparel: "+(ha-va.HitPoints)+" vs "+(hb-vb.HitPoints));
   a.Destroy();b.Destroy();
   // 圣水：造物和巫妖受伤，巫妖扣15精神力；活人无伤。
   var master=ItemPawn(spot+IntVec3.East*3);master.SetFaction(EnemyFaction());Hold(master);var skel=FactionConstructs.MakeBound("Skeleton",master,master.Faction);GenSpawn.Spawn(skel,spot,map);Hold(skel);
   var human=ItemPawn(spot+IntVec3.South);Hold(human);int humanHediffs=human.health.hediffSet.hediffs.Count;float skelHp=skel.health.summaryHealth.SummaryHealthPercent;
   Launch("LP_HolyWater",thrower,spot);
   Check(skel.Dead || skel.health.summaryHealth.SummaryHealthPercent<skelHp-.05f,"holy water burns a necromantic construct: dead="+skel.Dead+" destroyed="+skel.Destroyed+" pos="+skel.PositionHeld+"/"+spot);
   Check(!human.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury || h is Hediff_MissingPart),"holy water does not hurt the living: "+string.Join(",",human.health.hediffSet.hediffs.Select(h=>h.def.defName)));
   // 巫妖：直接命中躯干（爆炸随机部位可能把测试巫妖打死，精神力随之清零）。
   var lich=ItemPawn(spot+IntVec3.North*3);lich.ChangePsylinkLevel(1,false);var box=Rituals.Ascend(lich);Necro.Registry.boxes.Remove(box);var lh=Necro.Lich(lich);lh.autoRepair=false;lich.psychicEntropy.OffsetPsyfocusDirectly(1f);float focus=lich.psychicEntropy.CurrentPsyfocus;Hold(lich);
   lich.TakeDamage(new DamageInfo(ItemThrowables.Holy,30f,0f,-1f,thrower,lich.RaceProps.body.corePart));
   Check(!lich.Dead && lich.health.hediffSet.hediffs.Any(h=>h.def.defName=="LP_HolyBurn") && Math.Abs((focus-lich.psychicEntropy.CurrentPsyfocus)*lh.Power-15f)<.5f,"holy water burns a lich and drains 15 psyfocus points: "+(focus-lich.psychicEntropy.CurrentPsyfocus)*lh.Power);
   var living=ItemPawn(spot+IntVec3.North*4);int lh0=living.health.hediffSet.hediffs.Count;living.TakeDamage(new DamageInfo(ItemThrowables.Holy,30f,0f,-1f,thrower,living.RaceProps.body.corePart));Check(!living.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury || h is Hediff_MissingPart),"a direct holy water hit does nothing to the living");living.Destroy();
   Check(DefDatabase<RecipeDef>.GetNamed("LP_MakeHolyWater").researchPrerequisite.defName=="LP_DNDAlchemy","holy water requires DND alchemy");   if(!skel.Destroyed)skel.Destroy();lich.Destroy();human.Destroy();master.Destroy();
   // 雷鸣石：3×3 眩晕2秒并打断预热，范围外不受影响。
   var inside=ItemPawn(spot+IntVec3.East);var outside=ItemPawn(spot+IntVec3.East*3);var target=ItemPawn(spot+IntVec3.North*3);
   var mv=inside.meleeVerbs.TryGetMeleeVerb(target);inside.stances.SetStance(new Stance_Warmup(300,target,mv));Check(inside.stances.curStance is Stance_Busy,"thunderstone fixture is warming up an attack");
   int hit=ItemThrowables.Thunder(map,spot,thrower);
   Check(hit>=1 && inside.stances.stunner.Stunned && inside.stances.stunner.StunTicksLeft<=120 && inside.stances.stunner.StunTicksLeft>=110 && !(inside.stances.curStance is Stance_Warmup),"thunderstone stuns for 2 seconds and interrupts the warmup");
   Check(!outside.stances.stunner.Stunned,"thunderstone only affects the 3x3 area");
   inside.Destroy();outside.Destroy();target.Destroy();
   // 黏胶袋：移速-70%，10秒；体型>2减半。
   var slow=ItemPawn(spot);float ms=slow.GetStatValue(StatDefOf.MoveSpeed);
   var big=PawnGenerator.GeneratePawn(PawnKindDef.Named("Thrumbo"),null);GenSpawn.Spawn(big,spot+IntVec3.East,map);float bms=big.GetStatValue(StatDefOf.MoveSpeed);
   ItemThrowables.Tangle(map,spot);
   Check(Near(slow.GetStatValue(StatDefOf.MoveSpeed)/ms,.3f) && TicksLeft(slow,"LP_Tanglefoot")==600,"tanglefoot cuts movement by 70% for 10 seconds");
   Check(big.BodySize>2 && Near(big.GetStatValue(StatDefOf.MoveSpeed)/bms,.65f),"tanglefoot only half-slows creatures larger than body size 2");
   slow.Destroy();big.Destroy();
   // 驱散粉尘：护盾腰带30秒停摆后恢复，力能龙偏折失效，5×5。
   var belted=ItemPawn(spot);var belt=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_ShieldBelt"));belted.apparel.Wear(belt);var shield=belt.GetComp<CompShield>();
   AccessTools.Field(typeof(CompShield),"energy").SetValue(shield,(float)AccessTools.Property(typeof(CompShield),"EnergyMax").GetValue(shield));float energy=shield.Energy;
   Check(shield.ShieldState==ShieldState.Active,"shield belt fixture is active");
   var dragon=RacePawn("LP_ForceDragon");Place(dragon,spot+IntVec3.North*2);dragon.jobs.StopAll();ForceDragon.Heart(dragon).SetAge(3);ForceDragon.Heart(dragon).Value=ForceDragon.Heart(dragon).Max;
   var far=ItemPawn(spot+IntVec3.East*4);
   ItemThrowables.Dispel(map,spot);
   Check(shield.ShieldState==ShieldState.Disabled && Items.Dispelled(belted) && TicksLeft(belted,"LP_MagicDispelled")==1800,"dispel dust shuts the shield belt down for 30 seconds");
   Check(!Items.Dispelled(far),"dispel dust reaches 5x5 only");
   var shot=new DamageInfo(DamageDefOf.Bullet,5f,0f,-1f,far,null,DefDatabase<ThingDef>.GetNamed("Gun_Revolver"));int avoided=0;for(int i=0;i<200;i++)if(ForceDragon.Avoid(dragon,shot)!=null)avoided++;
   Check(avoided==0,"dispelled force dragon cannot deflect, blur or displace");
   Items.Remove(dragon,"LP_MagicDispelled");avoided=0;for(int i=0;i<200;i++)if(ForceDragon.Avoid(dragon,shot)!=null)avoided++;Check(avoided>20,"force dragon defenses return once the dispel ends ("+avoided+"/200)");
   var timer=((HediffWithComps)belted.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_MagicDispelled"))).TryGetComp<HediffComp_Disappears>();timer.ticksToDisappear=2;Step(3);
   Check(!Items.Dispelled(belted) && shield.ShieldState==ShieldState.Active && Near(shield.Energy,energy),"shield belt recovers with its energy after the dispel ends");
   belted.Destroy();dragon.Destroy();far.Destroy();
   // 投弹手与掉落。
   var guard=SharedPawn();guard.kindDef=PawnKindDef.Named("LP_FC_Guard");var orc=SharedPawn();orc.kindDef=PawnKindDef.Named("LP_FH_OrcWarrior");
   Check(ItemGear.GrenadeFor(guard,.01f)=="LP_HolyWater" && ItemGear.GrenadeFor(guard,.06f)=="LP_AlchemistFire" && ItemGear.GrenadeFor(guard,.5f)==null && ItemGear.GrenadeFor(orc,.01f)=="LP_AlchemistFire","alliance soldiers sometimes carry holy water or alchemist fire, Thay warriors alchemist fire");
   Check(ItemGear.Arm(guard,"LP_HolyWater") && guard.equipment.Primary.def.defName=="LP_HolyWater","grenadier is armed with the throwable");
   orc.SetFaction(EnemyFaction());var carried=ItemGear.Carry(orc,.01f);Check(carried?.def.defName=="LP_Thunderstone" && orc.inventory.innerContainer.Contains(carried),"enemies sometimes carry a thunderstone that drops on death");
   Check(ItemGear.Carry(orc,.03f)?.def.defName=="LP_DispelDust" && ItemGear.Carry(orc,.5f)==null,"enemies sometimes carry dispel dust");
   guard.Destroy();orc.Destroy();thrower.Destroy();
  }

  // ---------- 新材料 ----------
  public static readonly string[] ItemMaterialDefs={"LP_LavaCrystal","LP_DragonShed","LP_TrollHide","LP_Mithral","LP_Adamantine","LP_ColdIron"};
  void CheckMaterials(){
   foreach(var name in ItemMaterialDefs){
    var d=DefDatabase<ThingDef>.GetNamed(name);
    Check(!d.ConfigErrors().Any() && d.graphic is Graphic_StackCount sc && Traverse.Create(sc).Field("subGraphics").GetValue<Graphic[]>().Length==3 && d.graphicData.texPath=="Items/Materials/"+name.Substring(3),"material has its own three stack textures: "+name);
   }
   foreach(var name in new[]{"LP_Mithral","LP_Adamantine","LP_ColdIron"})Check(DefDatabase<ThingDef>.GetNamed(name).stuffProps.categories.Contains(StuffCategoryDefOf.Metallic),"metal material is metallic stuff: "+name);
   Check(ItemMaterials.TrollHide.stuffProps.categories.Contains(StuffCategoryDefOf.Leathery) && ItemMaterials.TrollHide.stuffProps.statFactors.GetStatFactorFromList(StatDefOf.MaxHitPoints)>=2f,"troll hide is a durable leather");
   // 矿脉：原版散布矿脉的选择里会出现秘银与精金（多次统计），精金更稀有。
   foreach(var (ore,thing) in new[]{("LP_MineableMithral","LP_Mithral"),("LP_MineableAdamantine","LP_Adamantine")}){var d=DefDatabase<ThingDef>.GetNamed(ore);Check(d.building.isResourceRock && d.building.mineableThing.defName==thing && d.building.mineableScatterCommonality>0 && d.building.veinMineable,"ore vein is a vanilla scatter mineable: "+ore);}
   var gen=new GenStep_ScatterLumpsMineable();var choose=AccessTools.Method(typeof(GenStep_ScatterLumpsMineable),"ChooseThingDef");var picks=new Dictionary<string,int>();
   Rand.PushState(20261024);try{for(int i=0;i<6000;i++){var d=(ThingDef)choose.Invoke(gen,null);if(d!=null)picks[d.defName]=(picks.TryGetValue(d.defName,out int n)?n:0)+1;}}finally{Rand.PopState();}
   int mith=picks.TryGetValue("LP_MineableMithral",out int m1)?m1:0,adam=picks.TryGetValue("LP_MineableAdamantine",out int a1)?a1:0,ura=picks.TryGetValue("MineableUranium",out int u1)?u1:0;
   Check(mith>0 && adam>0 && adam<mith && mith<ura,"map generation scatters mithral and rarer adamantine veins (6000 lumps: mithral "+mith+", adamantine "+adam+", uranium "+ura+")");
   // 矮人开采秘银产量翻倍。
   var spot=ItemSpot(3);var dwarf=RacePawn("LP_Dwarf");Place(dwarf,spot+IntVec3.West*2);Hold(dwarf);var human=ItemPawn(spot+IntVec3.East*2);Hold(human);
   int Mined(Pawn miner,IntVec3 at){var rock=(Mineable)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("LP_MineableMithral"),at,map);AccessTools.Field(typeof(Mineable),"yieldPct").SetValue(rock,1f);rock.DestroyMined(miner);var got=map.listerThings.ThingsOfDef(ItemMaterials.Mithral).Where(t=>t.Position.InHorDistOf(at,3)).ToList();int n=got.Sum(t=>t.stackCount);foreach(var t in got)t.Destroy();return n;}
   int byDwarf=Mined(dwarf,spot+IntVec3.North),byHuman=Mined(human,spot+IntVec3.South);
   Check(byHuman>0 && byDwarf==byHuman*2,"dwarves mine twice as much mithral: "+byDwarf+" vs "+byHuman);
   dwarf.Destroy();
   // 熔岩结晶：火巨人在熔火锻炉完成作品 15%。
   var giant=RacePawn("LP_FireGiant");Place(giant,spot+IntVec3.West);Hold(giant);var forge=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FireGiantForge"));var table=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_AlchemyTable"),ThingDefOf.Steel);
   Check(ItemMaterials.LavaFrom(giant,forge,.1f) && !ItemMaterials.LavaFrom(giant,forge,.2f) && !ItemMaterials.LavaFrom(human,forge,.01f) && !ItemMaterials.LavaFrom(giant,table,.01f),"lava crystal: 15% only for fire giants at the molten forge");
   var recipe=DefDatabase<RecipeDef>.GetNamed("LP_MakeFireGiantTunic");var cloth=ThingMaker.MakeThing(ThingDefOf.Cloth);int lava=0;
   Rand.PushState(7);try{for(int i=0;i<300;i++)foreach(var t in GenRecipe.MakeRecipeProducts(recipe,giant,new List<Thing>{cloth},cloth,(IBillGiver)forge,null,null,null).ToList()){if(t.def==ItemMaterials.Lava)lava+=t.stackCount;if(!t.Destroyed)t.Destroy();}}finally{Rand.PopState();}
   Check(lava>20 && lava<75,"molten forge work yields lava crystals about 15% of the time ("+lava+"/300)");
   int none=0;foreach(var t in GenRecipe.MakeRecipeProducts(recipe,human,new List<Thing>{cloth},cloth,(IBillGiver)forge,null,null,null).ToList()){if(t.def==ItemMaterials.Lava)none++;t.Destroy();}Check(none==0,"non-giants never get lava crystals");
   giant.Destroy();forge.Destroy();table.Destroy();cloth.Destroy();
   // 龙鳞蜕：晋升蜕3片，宝库住满60天蜕1片，卖掉记仇。
   var dragon=RacePawn("LP_ForceDragon");Place(dragon,spot);Hold(dragon);var heart=ForceDragon.Heart(dragon);heart.SetAge(1);
   int Sheds()=>map.listerThings.ThingsOfDef(ItemMaterials.Shed).Sum(t=>t.stackCount);int s0=Sheds();
   heart.daysOnHoard=ForceDragon.DaysNeeded(1)+1;heart.hoardValue=ForceDragon.HoardNeeded(1)+1;Check(heart.TryAdvance() && heart.Age==2 && Sheds()==s0+3,"advancing a dragon age sheds 3 dragon scales");
   foreach(var t in map.listerThings.ThingsOfDef(ItemMaterials.Shed).ToList())t.Destroy();dragon.Destroy();
   dragon=RacePawn("LP_ForceDragon");heart=ForceDragon.Heart(dragon);heart.SetAge(1);var built=DragonHoard.BuildHoard(map,map.Center,dragon,out var interior);
   Place(dragon,interior.CenterCell);Hold(dragon);heart.RefreshHoard();Check(heart.hasHoard,"shed fixture dragon has a hoard");
   heart.daysOnHoard=59.99f;heart.Hourly(2500);Check(Sheds()==1 && heart.daysOnHoard>60f,"a dragon living in its hoard sheds one scale every 60 days");
   heart.Hourly(2500);Check(Sheds()==1,"no extra scale until the next 60 days");
   var mem=dragon.needs.mood.thoughts.memories;int stolen=mem.Memories.Count(x=>x.def==DragonHoard.Stolen);
   Check(ItemMaterials.ShedSold().Contains(dragon) && mem.Memories.Count(x=>x.def==DragonHoard.Stolen)==stolen+1,"selling dragon scales offends the colony's dragons");
   Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Pawn_TraderTracker),nameof(Pawn_TraderTracker.GiveSoldThingToTrader)))?.Prefixes.Any(x=>x.PatchMethod.DeclaringType==typeof(DragonShedSale))==true,"dragon scale sale is hooked into caravan trading");
   foreach(var t in map.listerThings.ThingsOfDef(ItemMaterials.Shed).ToList())t.Destroy();foreach(var t in built)if(!t.Destroyed)t.Destroy();foreach(var c in interior.Cells)map.roofGrid.SetRoof(c,null);dragon.Destroy();
   // 巨魔皮：屠宰巨魔得到巨魔皮；巨魔皮服装每天恢复1%耐久。
   var troll=RacePawn("LP_Troll");Place(troll,spot);troll.Kill(null);var products=troll.Corpse.ButcherProducts(human,1f).ToList();
   Check(products.Any(t=>t.def==ItemMaterials.TrollHide) && !products.Any(t=>t.def.defName=="Leather_Human"),"butchering a troll yields troll hide instead of human leather");
   foreach(var t in products)if(!t.Destroyed)t.Destroy();Troll.Gene(troll)?.CancelRevival(false);troll.Corpse.Destroy();
   var man=SharedPawn();Place(man,spot);man.Kill(null);products=man.Corpse.ButcherProducts(human,1f).ToList();Check(products.Any(t=>t.def.defName=="Leather_Human") && !products.Any(t=>t.def==ItemMaterials.TrollHide),"butchering an ordinary human still yields human leather");foreach(var t in products)if(!t.Destroyed)t.Destroy();man.Corpse.Destroy();
   var duster=DefDatabase<ThingDef>.GetNamed("Apparel_Duster");var floor=ThingMaker.MakeThing(duster,ItemMaterials.TrollHide);floor.HitPoints=floor.MaxHitPoints-20;GenSpawn.Spawn(floor,spot+IntVec3.North,map);
   var worn=(Apparel)ThingMaker.MakeThing(duster,ItemMaterials.TrollHide);worn.HitPoints=worn.MaxHitPoints-20;human.apparel.Wear(worn);
   var plain=ThingMaker.MakeThing(duster,DefDatabase<ThingDef>.GetNamed("Leather_Plain"));plain.HitPoints=plain.MaxHitPoints-20;GenSpawn.Spawn(plain,spot+IntVec3.South,map);
   int h0=floor.HitPoints,w0=worn.HitPoints,p0=plain.HitPoints;ItemMaterials.DailyRepair();int step=Math.Max(1,Mathf.RoundToInt(floor.MaxHitPoints*.01f));
   Check(floor.HitPoints==h0+step && worn.HitPoints==w0+step && plain.HitPoints==p0,"troll hide apparel regains 1% durability per day, on the floor and worn ("+step+"/day)");
   floor.Destroy();plain.Destroy();human.apparel.Remove(worn);worn.Destroy();
   // 秘银：重量减半，服装无移速惩罚。
   var plate=DefDatabase<ThingDef>.GetNamed("Apparel_PlateArmor");var sword=DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword");
   var mPlate=(Apparel)ThingMaker.MakeThing(plate,ItemMaterials.Mithral);var sPlate=(Apparel)ThingMaker.MakeThing(plate,ThingDefOf.Steel);var mSword=ThingMaker.MakeThing(sword,ItemMaterials.Mithral);var sSword=ThingMaker.MakeThing(sword,ThingDefOf.Steel);
   Check(Near(mPlate.GetStatValue(StatDefOf.Mass)/sPlate.GetStatValue(StatDefOf.Mass),.5f) && Near(mSword.GetStatValue(StatDefOf.Mass)/sSword.GetStatValue(StatDefOf.Mass),.5f),"mithral items weigh half as much");
   Check(mPlate.GetStatValue(StatDefOf.ArmorRating_Sharp)>sPlate.GetStatValue(StatDefOf.ArmorRating_Sharp)*.85f,"mithral armor protects nearly as well as steel");
   float naked=human.GetStatValue(StatDefOf.MoveSpeed);human.apparel.Wear(sPlate);float heavy=human.GetStatValue(StatDefOf.MoveSpeed);human.apparel.Remove(sPlate);human.apparel.Wear(mPlate);float light=human.GetStatValue(StatDefOf.MoveSpeed);human.apparel.Remove(mPlate);
   Check(heavy<naked-.3f && Near(light,naked),"mithral plate armor has no movement penalty: "+naked+"/"+heavy+"/"+light);
   // 精金：近战对建筑和机械体×2；护甲让穿甲减半。
   var aSword=(ThingWithComps)ThingMaker.MakeThing(sword,ItemMaterials.Adamantine);human.equipment.AddEquipment(aSword);
   var wallA=GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog),spot+IntVec3.North*2,map);var wallB=GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog),spot+IntVec3.North*3,map);
   int wa=wallA.HitPoints;wallA.TakeDamage(new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,human,null,sword));int lostA=wa-wallA.HitPoints;
   human.equipment.DestroyAllEquipment();var sSword2=(ThingWithComps)ThingMaker.MakeThing(sword,ThingDefOf.Steel);human.equipment.AddEquipment(sSword2);
   int wb=wallB.HitPoints;wallB.TakeDamage(new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,human,null,sword));int lostB=wb-wallB.HitPoints;
   Check(lostB>0 && lostA==lostB*2,"adamantine melee deals double damage to buildings: "+lostA+" vs "+lostB);
   human.equipment.DestroyAllEquipment();human.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(sword,ItemMaterials.Adamantine));
   var mechKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");
   if(mechKind!=null){var mech=PawnGenerator.GeneratePawn(mechKind,Faction.OfMechanoids);Check(Near(ItemMaterials.WeaponFactor(mech,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,human,null,sword)),2f),"adamantine melee deals double damage to mechanoids");mech.Destroy();}
   var victim=ItemPawn(spot+IntVec3.East*3);Check(Near(ItemMaterials.WeaponFactor(victim,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,human,null,sword)),1f),"adamantine has no bonus against flesh");
   Check(Near(ItemMaterials.WeaponFactor(wallA,new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,human,null,DefDatabase<ThingDef>.GetNamed("Gun_Revolver"))),1f),"adamantine bonus is melee only");
   wallA.Destroy();wallB.Destroy();
   var aPlate=(Apparel)ThingMaker.MakeThing(plate,ItemMaterials.Adamantine);victim.apparel.Wear(aPlate);var pen=new DamageInfo(DamageDefOf.Bullet,10f,.8f);ItemMaterials.ArmorFactor(victim,ref pen);
   Check(Near(pen.ArmorPenetrationInt,.4f),"adamantine armor halves incoming armor penetration");
   var bare=new DamageInfo(DamageDefOf.Bullet,10f,.8f);ItemMaterials.ArmorFactor(human,ref bare);Check(Near(bare.ArmorPenetrationInt,.8f),"no adamantine armor, no change");
   // 寒铁：对灵能链接者、精灵 +25%。
   human.equipment.DestroyAllEquipment();human.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(sword,ItemMaterials.ColdIron));
   var psy=ItemPawn(spot+IntVec3.West*3);psy.ChangePsylinkLevel(1,false);var elf=RacePawn("LP_Elf");Place(elf,spot+IntVec3.South*3);Hold(elf);var plainP=ItemPawn(spot+IntVec3.South*2);
   var hit=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,human,null,sword);
   Check(Near(ItemMaterials.WeaponFactor(psy,hit),1.25f) && Near(ItemMaterials.WeaponFactor(elf,hit),1.25f) && Near(ItemMaterials.WeaponFactor(plainP,hit),1f),"cold iron deals +25% to psylinked pawns and elves only");
   Check(DefDatabase<RecipeDef>.GetNamed("LP_MakeColdIron").products[0].count==10 && DefDatabase<RecipeDef>.GetNamed("LP_MakeColdIron").AllRecipeUsers.Contains(DefDatabase<ThingDef>.GetNamed("LP_AlchemyTable")),"cold iron is quenched at the alchemy table (30 steel + 10 silver -> 10)");
   psy.Destroy();elf.Destroy();plainP.Destroy();victim.Destroy();human.Destroy();
  }
  // ---------- 种族装备 ----------
  public static readonly (string def,string race,string bench)[] ItemRaceGear={("LP_DwarvenThrowingHammer","Dwarf","LP_RuneAnvil"),("LP_DwarvenRuneShield","Dwarf","LP_RuneAnvil"),("LP_ElvenLongbow","Elf","CraftingSpot"),("LP_ElvenChain","Elf","LP_RuneAnvil"),("LP_OrcishGreataxe","HalfOrc","LP_RuneAnvil"),("LP_MinotaurHornHelm","Minotaur","LP_RuneAnvil"),("LP_GithAstralArmor","Githyanki","LP_RuneAnvil"),("LP_DragonShedArmor",null,"LP_RuneAnvil"),("LP_IllithidTendrilCloak","MindFlayer","HandTailoringBench"),("LP_BoneTalisman","Lich","LP_Altar")};
  static RecipeDef RecipeFor(string def)=>DefDatabase<RecipeDef>.AllDefsListForReading.First(r=>r.ProducedThingDef?.defName==def);
  Pawn RacedPawn(string race){switch(race){case "Dwarf":return RacePawn("LP_Dwarf");case "Elf":return RacePawn("LP_Elf");case "HalfOrc":return RacePawn("LP_HalfOrc");case "Minotaur":return RacePawn("LP_Minotaur");case "Githyanki":return RacePawn("LP_Githyanki");case "MindFlayer":return RacePawn("LP_MindFlayer");case "Lich":{var p=SharedPawn();p.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);return p;}}return SharedPawn();}
  Apparel WearItem(Pawn p,string def){var d=DefDatabase<ThingDef>.GetNamed(def);var a=(Apparel)ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);p.apparel.Wear(a,false);return a;}
  ThingWithComps HoldItem(Pawn p,string def){var d=DefDatabase<ThingDef>.GetNamed(def);p.equipment.DestroyAllEquipment();var w=(ThingWithComps)ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);p.equipment.AddEquipment(w);return w;}
  static bool HasAbility(Pawn p,string def)=>p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed(def))!=null;
  void CheckRaceGear(){
   var spot=ItemSpot(4);var human=ItemPawn(spot+IntVec3.West*4);Hold(human);
   foreach(var (def,race,bench) in ItemRaceGear){
    var d=DefDatabase<ThingDef>.GetNamed(def);
    Check(!d.ConfigErrors().Any() && d.graphic.MatSingle!=BaseContent.BadMat && d.uiIcon!=BaseContent.BadTex && d.graphicData.texPath=="Items/RaceGear/"+def.Substring(3),"racial gear has its own texture: "+def);
    var r=RecipeFor(def);Check(r.AllRecipeUsers.Any(u=>u.defName==bench),"racial gear "+def+" is made at "+bench);
    var maker=RacedPawn(race);Hold(maker);
    if(race==null)Check(RaceGear.CannotCraft(r,human)==null && RaceGear.CannotCraft(r,maker)==null,"anyone can craft "+def);
    else{Check(RaceGear.CannotCraft(r,maker)==null && RaceGear.CannotCraft(r,human)!=null && Items.Is(maker,race),"only "+race+" can craft "+def);
     Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(RimWorld.Bill),nameof(RimWorld.Bill.PawnAllowedToStartAnew)))?.Postfixes.Any(x=>x.PatchMethod.DeclaringType==typeof(RaceGearCrafting))==true,"crafting restriction is hooked into native bills");}
    maker.Destroy();
   }
   Check(RecipeFor("LP_ElvenChain").researchPrerequisite?.defName=="LP_MythicMetallurgy" && DefDatabase<ThingDef>.GetNamed("LP_ElvenChain").CostList.Count==1 && DefDatabase<ThingDef>.GetNamed("LP_ElvenChain").CostList[0].thingDef==ItemMaterials.Mithral,"elven chain is made only of mithral after mythic metallurgy");
   Check(DefDatabase<ThingDef>.GetNamed("LP_DragonShedArmor").CostList.Any(c=>c.thingDef==ItemMaterials.Shed),"dragon shed armor is made from dragon scales");
   // 矮人投掷锤：拿起获得投掷能力，卸下失去；8格；矮人无冷却。
   var dwarf=RacePawn("LP_Dwarf");Place(dwarf,spot);Hold(dwarf);var enemy=ItemPawn(spot+IntVec3.East*3);enemy.SetFaction(EnemyFaction());Hold(enemy);
   HoldItem(dwarf,"LP_DwarvenThrowingHammer");Check(HasAbility(dwarf,"LP_HammerThrow"),"holding the throwing hammer grants the throw ability");
   float hp=enemy.health.summaryHealth.SummaryHealthPercent;var throwAbility=dwarf.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_HammerThrow"));
   Check(throwAbility.Activate(new LocalTargetInfo(enemy),new LocalTargetInfo(enemy)) && enemy.health.summaryHealth.SummaryHealthPercent<hp,"thrown hammer hurts the target");
   Check(!throwAbility.OnCooldown,"dwarves throw the hammer without cooldown");
   HoldItem(human,"LP_DwarvenThrowingHammer");var humanThrow=human.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_HammerThrow"));Place(human,spot+IntVec3.West*2);
   Check(humanThrow!=null && humanThrow.Activate(new LocalTargetInfo(enemy),new LocalTargetInfo(enemy)) && humanThrow.OnCooldown && humanThrow.CooldownTicksRemaining>1700,"others must wait 30 seconds between throws");
   var far=ItemPawn(spot+IntVec3.West*4+IntVec3.North*0);far.SetFaction(EnemyFaction());Place(far,dwarf.Position+IntVec3.North*4+IntVec3.East*4+IntVec3.North*4);Hold(far);
   Check(new CompItemAbility{props=new CompProperties_ItemAbility{effect="hammer"},parent=throwAbility}.Invalid(far)!=null,"throwing hammer reaches only 8 cells");
   dwarf.equipment.DestroyAllEquipment();Check(!HasAbility(dwarf,"LP_HammerThrow"),"putting the hammer away removes the ability");human.equipment.DestroyAllEquipment();far.Destroy();
   // 矮人符文盾：远程格挡25%，山体厚岩顶下45%。
   WearItem(dwarf,"LP_DwarvenRuneShield");Check(Near(RaceGear.BlockChance(dwarf),.25f) && RaceGear.BlockChance(human)==0,"rune shield blocks 25% of ranged attacks in the open");
   map.roofGrid.SetRoof(dwarf.Position,RoofDefOf.RoofRockThick);Check(Near(RaceGear.BlockChance(dwarf),.45f),"rune shield blocks 45% under a thick mountain roof");
   var block=AccessTools.Method(typeof(RuneShieldBlock),"Prefix");var shot=new DamageInfo(DamageDefOf.Bullet,5f,0f,-1f,enemy,null,DefDatabase<ThingDef>.GetNamed("Gun_Revolver"));int blocked=0;
   Rand.PushState(11);try{for(int i=0;i<400;i++){var args=new object[]{dwarf,shot,false};if(!(bool)block.Invoke(null,args))blocked++;}}finally{Rand.PopState();}
   map.roofGrid.SetRoof(dwarf.Position,null);
   Check(blocked>140 && blocked<220,"real block rolls under the mountain match 45% ("+blocked+"/400)");
   var punch=new DamageInfo(DamageDefOf.Blunt,5f,0f,-1f,enemy);var pargs=new object[]{dwarf,punch,false};Check((bool)block.Invoke(null,pargs),"rune shield never blocks melee");
   dwarf.Destroy();
   // 精灵长弓：射程+4、瞄准更快；精灵用时没有黑暗惩罚。
   var bow=DefDatabase<ThingDef>.GetNamed("LP_ElvenLongbow");var great=DefDatabase<ThingDef>.GetNamed("Bow_Great");
   Check(bow.Verbs.Count==1 && Near(bow.Verbs[0].range,great.Verbs[0].range+4f) && bow.Verbs[0].warmupTime<great.Verbs[0].warmupTime,"elven longbow shoots 4 cells farther and aims faster");
   var elf=RacePawn("LP_Elf");Place(elf,spot);Hold(elf);HoldItem(elf,"LP_ElvenLongbow");HoldItem(human,"LP_ElvenLongbow");
   var dark=AccessTools.Method(typeof(ElvenLongbowDarkness),"Postfix");var darkField=AccessTools.Field(typeof(ShotReport),"offsetFromDarkness");
   object rep=new ShotReport();darkField.SetValue(rep,-.3f);var eargs=new object[]{elf,rep};dark.Invoke(null,eargs);object rep2=new ShotReport();darkField.SetValue(rep2,-.3f);var hargs=new object[]{human,rep2};dark.Invoke(null,hargs);
   Check(Near((float)darkField.GetValue(eargs[1]),0f) && Near((float)darkField.GetValue(hargs[1]),-.3f),"only elves ignore darkness with the elven longbow");
   human.equipment.DestroyAllEquipment();
   // 精灵锁子甲：精灵+5%近战闪避；无移速惩罚。
   // 先去掉随机的闪避特性（如“敏捷”+15）：它们把人推到原版闪避曲线的平缓段，同样 +2.5 只多约 1.25%，检查就随机失败。
   foreach(var tp in new[]{elf,human})foreach(var tr in tp.story.traits.allTraits.Where(x=>x.CurrentData.statOffsets?.Any(s=>s.stat==StatDefOf.MeleeDodgeChance)==true).ToList())tp.story.traits.RemoveTrait(tr);
   elf.skills.GetSkill(SkillDefOf.Melee).Level=12;human.skills.GetSkill(SkillDefOf.Melee).Level=12;elf.drafter.Drafted=false;human.drafter.Drafted=false;
   float dodge=elf.GetStatValue(StatDefOf.MeleeDodgeChance),hd=human.GetStatValue(StatDefOf.MeleeDodgeChance),ms=human.GetStatValue(StatDefOf.MoveSpeed);
   var chainE=WearItem(elf,"LP_ElvenChain");var chainH=WearItem(human,"LP_ElvenChain");
   Check(Math.Abs(elf.GetStatValue(StatDefOf.MeleeDodgeChance)-dodge-.05f)<.006f && Near(StatWorker.StatOffsetFromGear(chainE,StatDefOf.MeleeDodgeChance),2.5f) && Near(human.GetStatValue(StatDefOf.MeleeDodgeChance),hd) && Near(StatWorker.StatOffsetFromGear(chainH,StatDefOf.MeleeDodgeChance),0f),"elven chain gives elves (only) +5% melee dodge: elf "+dodge+"->"+elf.GetStatValue(StatDefOf.MeleeDodgeChance)+" human "+hd+"->"+human.GetStatValue(StatDefOf.MeleeDodgeChance)+" gear="+StatWorker.StatOffsetFromGear(chainE,StatDefOf.MeleeDodgeChance)+" owner="+RaceGear.Owner(chainE)+" isElf="+Items.IsElf(elf));
   Check(Near(human.GetStatValue(StatDefOf.MoveSpeed),ms) && chainH.GetStatValue(StatDefOf.ArmorRating_Sharp)>=.9f && chainH.GetStatValue(StatDefOf.Mass)<=5f,"elven chain protects near plate without movement penalty");
   human.apparel.Remove(chainH);chainH.Destroy();elf.Destroy();
   // 半兽人巨斧：野蛮重击概率翻倍；击倒敌人后5秒近战冷却-30%。
   var orc=RacePawn("LP_HalfOrc");Place(orc,spot);Hold(orc);bool savage;
   CommonRaces.MeleeFactor(orc,enemy,.15f,out savage);Check(!savage,"half-orc savage attack is 10% without the greataxe");
   HoldItem(orc,"LP_OrcishGreataxe");CommonRaces.MeleeFactor(orc,enemy,.15f,out savage);bool savage2;CommonRaces.MeleeFactor(orc,enemy,.21f,out savage2);
   Check(savage && !savage2,"half-orc savage attack doubles to 20% with the greataxe");
   var foe=ItemPawn(spot+IntVec3.North*2);foe.SetFaction(EnemyFaction());Hold(foe);float cd=orc.GetStatValue(StatDefOf.MeleeCooldownFactor);
   foe.TakeDamage(new DamageInfo(DamageDefOf.Cut,200f,999f,-1f,orc,foe.health.hediffSet.GetBrain(),DefDatabase<ThingDef>.GetNamed("LP_OrcishGreataxe")));
   Check((foe.Dead || foe.Downed) && TicksLeft(orc,"LP_AxeFrenzy")==300 && Near(orc.GetStatValue(StatDefOf.MeleeCooldownFactor),cd*.7f),"felling an enemy with the greataxe cuts melee cooldown by 30% for 5 seconds: dead="+foe.Dead+" downed="+foe.Downed+" ticks="+TicksLeft(orc,"LP_AxeFrenzy")+" cd="+cd+"->"+orc.GetStatValue(StatDefOf.MeleeCooldownFactor)+" holds="+Items.Holds(orc,"LP_OrcishGreataxe")+" hostile="+orc.HostileTo(foe));
   if(foe.Corpse!=null)foe.Corpse.Destroy();else foe.Destroy();orc.Destroy();
   // 牛头人角盔：其他人获得弱化冲撞（4格、伤害减半），牛头人自身冲撞+3格。
   var mino=RacePawn("LP_Minotaur");Place(mino,spot);Hold(mino);WearItem(mino,"LP_MinotaurHornHelm");
   Check(Minotaur.ChargeRange(mino)==Minotaur.ChargeCells+3 && !HasAbility(mino,"LP_HelmCharge"),"a minotaur wearing the horn helm charges 3 cells farther (and gets no weaker charge)");
   var helm=WearItem(human,"LP_MinotaurHornHelm");Check(HasAbility(human,"LP_HelmCharge") && Minotaur.ChargeRange(human)==Minotaur.ChargeCells,"the horn helm gives others the weaker charge");
   var line=ClearLine(6);Place(human,line);Hold(human);var target=ItemPawn(line+IntVec3.East*3);target.SetFaction(EnemyFaction());Hold(target);
   var helmCharge=human.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_HelmCharge"));var cr=Minotaur.Charge(human,line+IntVec3.East*6,4,.5f);
   Check(cr.hit==target && Near(cr.damage,Minotaur.ChargeDamage*human.BodySize*.5f),"helm charge hits for half the minotaur damage: "+cr.damage);
   Check(DefDatabase<AbilityDef>.GetNamed("LP_HelmCharge").cooldownTicksRange.min==60000 && DefDatabase<AbilityDef>.GetNamed("LP_HelmCharge").comps.OfType<CompProperties_ItemAbility>().First().range==4,"helm charge reaches 4 cells and cools down for a day");
   human.apparel.Remove(helm);helm.Destroy();Check(!HasAbility(human,"LP_HelmCharge"),"removing the helm removes the charge");
   target.Destroy();mino.Destroy();
   // 星界战甲：吉斯洋基星界跳跃后3秒受伤减半，只有吉斯洋基能触发。
   var gith=RacePawn("LP_Githyanki");Place(gith,spot);Hold(gith);WearItem(gith,"LP_GithAstralArmor");
   Check(Githyanki.Step(gith,spot+IntVec3.North*2,0f) && TicksLeft(gith,"LP_AstralWard")==180 && Near(ItemDamage.Factor(gith,DamageDefOf.Cut),.5f),"astral step in astral armor halves damage for 3 seconds");
   var astral=WearItem(human,"LP_GithAstralArmor");Place(human,spot+IntVec3.West*2);Githyanki.Step(human,spot+IntVec3.West*3,0f);Check(!Items.Has(human,"LP_AstralWard"),"only githyanki trigger the astral ward");
   human.apparel.Remove(astral);astral.Destroy();gith.Destroy();
   // 蜕鳞甲：免疫EMP眩晕，力场伤害-50%，力能龙穿不了。
   var shedA=WearItem(human,"LP_DragonShedArmor");var bare=ItemPawn(spot+IntVec3.East*2);Hold(bare);
   human.stances.stunner.StopStun();human.TakeDamage(new DamageInfo(DamageDefOf.EMP,20f));bare.TakeDamage(new DamageInfo(DamageDefOf.EMP,20f));
   Check(!human.stances.stunner.Stunned && Near(ItemDamage.Factor(human,ForceDragon.Force),.5f) && Near(ItemDamage.Factor(human,DamageDefOf.Cut),1f),"dragon shed armor ignores EMP stun and halves force damage");
   var dragon=RacePawn("LP_ForceDragon");Place(dragon,spot+IntVec3.South*2);Hold(dragon);
   Check(!ApparelUtility.HasPartsToWear(dragon,DefDatabase<ThingDef>.GetNamed("LP_DragonShedArmor")) && ApparelUtility.HasPartsToWear(human,DefDatabase<ThingDef>.GetNamed("LP_DragonShedArmor")),"force dragons cannot wear dragon shed armor");
   human.apparel.Remove(shedA);shedA.Destroy();dragon.Destroy();bare.Destroy();
   // 夺心魔触须披风：恐惧更强；夺心魔心灵震爆+2格；同图殖民者-2心情。
   var flayer=RacePawn("LP_MindFlayer");Place(flayer,spot);Hold(flayer);float range0=MindFlayer.BlastRangeFor(flayer);
   var cloak=WearItem(flayer,"LP_IllithidTendrilCloak");RaceGear.ResetCloakCensus();
   Check(Near(MindFlayer.BlastRangeFor(flayer),range0+2f) && Near(range0,MindFlayer.BlastRange),"mind flayers in the tendril cloak blast 2 cells farther");
   var thought=DefDatabase<ThoughtDef>.GetNamed("LP_SawTendrilCloak");
   Check(thought.Worker.CurrentState(human).Active && !thought.Worker.CurrentState(flayer).Active,"colonists who see the tendril cloak get -2 mood; the mind flayer does not");
   Check(thought.stages[0].baseMoodEffect==-2,"tendril cloak mood is -2");
   var victim=SensitivePawn();Place(victim,spot+IntVec3.East*3);victim.SetFaction(EnemyFaction());
   Check(LichSpells.Frighten(flayer,victim) && victim.stances.stunner.Stunned,"fear from a tendril cloak wearer also stuns the victim");
   victim.Destroy();flayer.apparel.Remove(cloak);RaceGear.ResetCloakCensus();Check(!thought.Worker.CurrentState(human).Active,"no cloak, no mood penalty");cloak.Destroy();flayer.Destroy();
   // 骨质护符：敌方死灵造物不主动攻击佩戴者，被打照常还手。
   var lichMaster=ItemPawn(spot+IntVec3.North*3);lichMaster.SetFaction(EnemyFaction());Hold(lichMaster);var skel=FactionConstructs.MakeBound("Skeleton",lichMaster,lichMaster.Faction);GenSpawn.Spawn(skel,spot+IntVec3.North*2,map);Hold(skel);
   var talisman=WearItem(human,"LP_BoneTalisman");
   Check(human.ThreatDisabled(skel) && !human.ThreatDisabled(enemy),"enemy constructs ignore the bone talisman wearer");
   human.mindState.lastAttackTargetTick=Find.TickManager.TicksGame;human.mindState.lastAttackedTarget=skel;Check(!human.ThreatDisabled(skel),"constructs fight back once the wearer attacks them");
   human.mindState.lastAttackTargetTick=-99999;human.apparel.Remove(talisman);Check(!human.ThreatDisabled(skel),"without the talisman constructs attack normally");
   Gone(talisman);Gone(skel);Gone(lichMaster);Gone(enemy);Gone(human);
  }
  // ---------- 卷轴 ----------
  public static readonly string[] ItemScrollDefs={"LP_ScrollTeleport","LP_ScrollRevivify","LP_ScrollProtectionEvil","LP_ScrollFireball","LP_ScrollTrueSeeing","LP_ScrollFleshToStone","LP_ScrollFearGaze","LP_ScrollNecroticBlast"};
  // 清理测试角色：已死的连尸体一起删，已删的跳过。
  static void Gone(Thing t){if(t==null)return;if(t is Pawn p && p.Dead && p.Corpse!=null && !p.Corpse.Destroyed){p.Corpse.Destroy();return;}if(!t.Destroyed)t.Destroy();}
  Thing ScrollAt(string def,IntVec3 c){var t=MakeItem(def);GenSpawn.Spawn(t,c,map);return t;}
  Pawn Reader(IntVec3 at,int intellect){var p=ItemPawn(at);Hold(p);p.skills.GetSkill(SkillDefOf.Intellectual).Level=intellect;return p;}
  void CheckScrolls(){
   foreach(var name in ItemScrollDefs.Concat(new[]{"LP_PetrifiedStatue"})){var d=DefDatabase<ThingDef>.GetNamed(name);Check(!d.ConfigErrors().Any() && d.graphic.MatSingle!=BaseContent.BadMat && d.graphicData.texPath.StartsWith("Items/"),"scroll/statue definition and own texture: "+name);}
   foreach(var name in ItemScrollDefs)Check(DefDatabase<ThingDef>.GetNamed(name).GetCompProperties<CompProperties_Usable>()!=null && DefDatabase<ThingDef>.GetNamed(name).GetCompProperties<CompProperties_Scroll>()!=null,"scroll is a one-use usable item: "+name);
   var spot=ItemSpot(4);
   // 读卷轴：智识≥6；失败率 (8-智识)×5%；巫妖不失败。
   var dull=Reader(spot+IntVec3.West*4,5);var six=Reader(spot+IntVec3.West*3,6);var seven=Reader(spot+IntVec3.West*2,7);var sharp=Reader(spot+IntVec3.West,12);var lichReader=Reader(spot+IntVec3.West*4+IntVec3.North,0);lichReader.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);
   Check(ItemScrolls.CannotRead(dull)!=null && ItemScrolls.CannotRead(six)==null && ItemScrolls.CannotRead(lichReader)==null,"intellect 6 is needed to read scrolls (liches always can)");
   Check(Near(ItemScrolls.FailChance(six),.1f) && Near(ItemScrolls.FailChance(seven),.05f) && ItemScrolls.FailChance(sharp)==0 && ItemScrolls.FailChance(lichReader)==0,"scroll failure chance is max(0,(8-intellect)x5%), zero for liches");
   var probe=ScrollAt("LP_ScrollFireball",spot+IntVec3.North*4);Check(!probe.TryGetComp<CompUsable>().CanBeUsedBy(dull,false,true).Accepted && probe.TryGetComp<CompUsable>().CanBeUsedBy(six,false,true).Accepted,"the native use option is blocked below intellect 6");
   int burns=six.health.hediffSet.hediffs.Count(h=>h.def==Troll.Burn);
   Check(ItemScrolls.Read(six,probe,spot+IntVec3.North*4+IntVec3.East*3,.05f) && probe.Destroyed && six.health.hediffSet.hediffs.Count(h=>h.def==Troll.Burn)==burns+1 && six.health.hediffSet.hediffs.Last(h=>h.def==Troll.Burn).Part.def.defName=="Hand","a failed reading burns the scroll and the reader's hand");
   int fails=0;var roll=AccessTools.Method(typeof(Rand),"get_Value");Rand.PushState(3);try{for(int i=0;i<2000;i++)if(Rand.Value<ItemScrolls.FailChance(six))fails++;}finally{Rand.PopState();}Check(fails>150 && fails<250,"intellect 6 fails about 10% of readings ("+fails+"/2000)");
   dull.Destroy();seven.Destroy();
   // 传送卷轴：在主基地无效且不消耗；在外地回到主基地床边。
   var home=ItemScrolls.HomeMap();Check(home==map,"test map is the main base");
   var tp=ScrollAt("LP_ScrollTeleport",sharp.Position);Check(!tp.TryGetComp<CompUsable>().CanBeUsedBy(sharp,false,true).Accepted && !ItemScrolls.Read(sharp,tp,LocalTargetInfo.Invalid,.99f) && !tp.Destroyed,"teleport scroll does nothing at the main base and is not used up");
   var bed=(Building_Bed)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Bed"),ThingDefOf.WoodLog);bed.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(bed,spot+IntVec3.South*3,map,Rot4.North);sharp.ownership.ClaimBedIfNonMedical(bed);
   Map away=null;
   foreach(var gen in new[]{MapGeneratorDefOf.SpacePocket,MapGeneratorDefOf.Encounter}){if(away!=null || gen==null)break;try{away=PocketMapUtility.GeneratePocketMap(new IntVec3(40,1,40),gen,new List<GenStepWithParams>(),map);}catch(Exception e){Log.Message("[LICH-TEST] pocket map "+gen.defName+" unavailable: "+e.Message);}}
   if(away!=null){
    var cell=CellFinder.RandomClosewalkCellNear(away.Center,away,5);sharp.DeSpawn();GenSpawn.Spawn(sharp,cell,away);tp.DeSpawn();GenSpawn.Spawn(tp,cell,away);
    Check(ItemScrolls.Read(sharp,tp,LocalTargetInfo.Invalid,.99f) && tp.Destroyed,"teleport scroll is used up away from the base");Items.State.Flush();
    Check(sharp.Map==map && sharp.Position.AdjacentTo8Way(bed.Position) || sharp.Map==map && GenAdj.CellsAdjacent8Way(bed).Contains(sharp.Position),"teleport lands next to the reader's own bed at the main base: "+sharp.Map+" "+sharp.Position+" bed "+bed.Position);
    PocketMapUtility.DestroyPocketMap(away);
   }else{Check(false,"a temporary away map could be generated for the teleport test");}
   bed.Destroy();if(!tp.Destroyed)tp.Destroy();Place(sharp,spot+IntVec3.West);Hold(sharp);
   // 复生卷轴：1天内成功并虚弱3天；超过1天、巫妖尸体、大脑被吞噬失败（不消耗）。
   var dead=ItemPawn(spot);dead.Kill(null);var corpse=dead.Corpse;var rev=ScrollAt("LP_ScrollRevivify",sharp.Position);
   Check(ItemScrolls.Read(sharp,rev,corpse,.99f) && rev.Destroyed && !dead.Dead && Items.Has(dead,"LP_RevivifyWeakness") && TicksLeft(dead,"LP_RevivifyWeakness")==180000,"revivify brings back a fresh corpse with 3 days of weakness");
   float cons=dead.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);Check(cons<.75f,"revivify weakness lowers consciousness by 30%: "+cons);dead.Destroy();
   var old=ItemPawn(spot);old.Kill(null);old.Corpse.Age=60001;rev=ScrollAt("LP_ScrollRevivify",sharp.Position);
   Check(!ItemScrolls.Read(sharp,rev,old.Corpse,.99f) && !rev.Destroyed && old.Dead,"a corpse dead for more than a day cannot be revived (scroll kept)");old.Corpse.Destroy();
   var deadLich=ItemPawn(spot);deadLich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);deadLich.Kill(null);
   Check(!ItemScrolls.Read(sharp,rev,deadLich.Corpse,.99f) && !rev.Destroyed && deadLich.Dead,"a lich corpse cannot be revived");deadLich.Corpse.Destroy();
   var brainless=ItemPawn(spot);brainless.health.AddHediff(HediffDefOf.MissingBodyPart,brainless.health.hediffSet.GetBrain());if(!brainless.Dead)brainless.Kill(null);
   Check(ItemScrolls.CannotRevivify(brainless.Corpse)!=null && !ItemScrolls.Read(sharp,rev,brainless.Corpse,.99f),"a corpse whose brain was devoured cannot be revived");brainless.Corpse.Destroy();rev.Destroy();
   // 防护邪恶：5×5、1天；敌对死灵造物不能进入，普通人可以。
   var ward=ItemScrolls.Ward(map,spot);var wm=Items.MapState(map);
   Check(ward.rect.Area==25 && ward.until==Find.TickManager.TicksGame+60000 && wm.wards.Contains(ward),"protection from evil makes a 5x5 ward for one day");
   var master=ItemPawn(spot+IntVec3.East*4);master.SetFaction(EnemyFaction());Hold(master);var skel=FactionConstructs.MakeBound("Skeleton",master,master.Faction);GenSpawn.Spawn(skel,spot,map);
   var occupy=AccessTools.Method(typeof(Pawn_PathFollower),"PawnCanOccupy");
   Check(!(bool)occupy.Invoke(skel.pather,new object[]{spot+IntVec3.North}) && (bool)occupy.Invoke(sharp.pather,new object[]{spot+IntVec3.North}),"warded cells cannot be entered by hostile constructs but can by others");
   ItemScrolls.MaintainWards(wm);Check(!ward.rect.Contains(skel.Position),"a construct inside the ward is pushed out");
   var dominated=ItemPawn(spot+IntVec3.West*2);dominated.health.AddHediff(MindFlayerPowers.DominatedHediff);Check(ItemScrolls.Evil(dominated) && !ItemScrolls.Evil(sharp),"dominated people are kept out too");dominated.health.RemoveHediff(dominated.health.hediffSet.GetFirstHediffOfDef(MindFlayerPowers.DominatedHediff));dominated.Destroy();
   ward.until=Find.TickManager.TicksGame;ItemScrolls.MaintainWards(wm);Check(!wm.wards.Contains(ward),"the ward fades after a day");
   skel.Destroy();master.Destroy();
   // 火球：半径3火焰爆炸。
   var burnt=ItemPawn(spot+IntVec3.East*2);Hold(burnt);var fb=ScrollAt("LP_ScrollFireball",sharp.Position);
   Check(ItemScrolls.Read(sharp,fb,spot,.99f) && fb.Destroyed,"fireball scroll is used up");Step(40);
   Check(burnt.health.hediffSet.hediffs.Any(h=>h.def==Troll.Burn) || burnt.Dead,"fireball burns someone 2 cells from the blast center");CleanFires();if(!burnt.Destroyed)burnt.Destroy();
   // 识破：揭示隐形1小时，解除被支配和殖巢奴仆。
   var ghost=ItemPawn(spot+IntVec3.North*2);Drink(ghost,"LP_PotionInvisibility");var slave=ItemPawn(spot+IntVec3.South*2);slave.health.AddHediff(MindFlayerPowers.DominatedHediff);
   var thrall=ItemPawn(spot+IntVec3.East*3);thrall.SetFaction(EnemyFaction());FactionGroups.Thrall(thrall);
   Check(ghost.IsPsychologicallyInvisible() && FactionGroups.IsThralled(thrall),"true seeing fixtures are invisible and thralled");
   var ts=ScrollAt("LP_ScrollTrueSeeing",sharp.Position);Check(ItemScrolls.Read(sharp,ts,LocalTargetInfo.Invalid,.99f) && ts.Destroyed,"true seeing scroll is used up");
   Check(!ghost.IsPsychologicallyInvisible() && wm.revealUntil==Find.TickManager.TicksGame+2500,"true seeing reveals invisible people for an hour");
   Check(!Items.Has(slave,MindFlayerPowers.DominatedHediff) && slave.MentalStateDef!=MindFlayerPowers.Dominated && !FactionGroups.IsThralled(thrall),"true seeing breaks domination and frees thralls at once");
   wm.revealUntil=Find.TickManager.TicksGame;Check(ghost.IsPsychologicallyInvisible(),"invisibility returns after the hour");
   ghost.Destroy();slave.Destroy();thrall.Destroy();
   // 石化：体型≤2的敌人30秒后变石像（保存人物），驱散粉尘复原；大块头不行。
   var foe=ItemPawn(spot+IntVec3.East*2);foe.SetFaction(EnemyFaction());Hold(foe);string foeId=foe.ThingID;var st=ScrollAt("LP_ScrollFleshToStone",sharp.Position);
   var big=PawnGenerator.GeneratePawn(PawnKindDef.Named("Thrumbo"),null);GenSpawn.Spawn(big,spot+IntVec3.North*3,map);
   Check(ItemScrolls.CannotPetrify(sharp,big)!=null && ItemScrolls.CannotPetrify(sharp,ItemPawn(spot+IntVec3.South))!=null,"flesh to stone only works on enemies of body size 2 or less");
   Check(ItemScrolls.Read(sharp,st,foe,.99f) && st.Destroyed && TicksLeft(foe,"LP_Petrifying")==1800,"flesh to stone starts a 30 second petrification");
   ((HediffWithComps)foe.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_Petrifying"))).TryGetComp<HediffComp_Disappears>().ticksToDisappear=1;Step(3);
   var statue=map.listerThings.ThingsOfDef(Items.Thing("LP_PetrifiedStatue")).OfType<PetrifiedStatue>().FirstOrDefault(x=>x.Pawn==foe);
   Check(statue!=null && !foe.Spawned && !foe.Dead && statue.LabelNoCount.Contains(foe.LabelShort),"after 30 seconds the enemy becomes a statue item holding the same pawn");
   ItemThrowables.Dispel(map,statue.Position);Check(foe.Spawned && !foe.Dead && foe.ThingID==foeId && statue.Destroyed,"dispel dust restores the statue to the same pawn");
   foe.Destroy();big.Destroy();foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>p.Position==spot+IntVec3.South && p.Faction==Faction.OfPlayer).ToList())p.Destroy();
   // 恐惧凝视、死灵冲击卷轴：非巫妖读出等同巫妖法术。
   var scared=SensitivePawn();Place(scared,spot+IntVec3.East*2);scared.SetFaction(EnemyFaction());var fg=ScrollAt("LP_ScrollFearGaze",sharp.Position);
   Check(ItemScrolls.Read(sharp,fg,spot,.99f) && scared.InMentalState,"a fear gaze scroll lets a non-lich frighten enemies like a level 3 lich");scared.Destroy();
   var blasted=ItemPawn(spot+IntVec3.East);blasted.SetFaction(EnemyFaction());Hold(blasted);float bhp=blasted.health.summaryHealth.SummaryHealthPercent;int bwounds=blasted.health.hediffSet.hediffs.Count(h=>h is Hediff_Injury || h is Hediff_MissingPart);var nb=ScrollAt("LP_ScrollNecroticBlast",sharp.Position);
   Check(ItemScrolls.Read(sharp,nb,spot,.99f) && (blasted.Dead || blasted.health.summaryHealth.SummaryHealthPercent<bhp || blasted.health.hediffSet.hediffs.Count(h=>h is Hediff_Injury || h is Hediff_MissingPart)>bwounds),"a necrotic blast scroll damages enemies like a level 6 lich: hp "+bhp+"->"+blasted.health.summaryHealth.SummaryHealthPercent+" hostile="+blasted.HostileTo(sharp)+" pos="+blasted.Position+"/"+spot+" scroll="+nb.Destroyed+" hediffs="+string.Join(",",blasted.health.hediffSet.hediffs.Select(h=>h.def.defName)));if(!blasted.Destroyed){if(blasted.Corpse!=null)blasted.Corpse.Destroy();else blasted.Destroy();}
   // 巫妖抄写：只有巫妖、30精神力+20布。
   foreach(var name in new[]{"LP_ScribeFearGaze","LP_ScribeNecroticBlast"}){var r=DefDatabase<RecipeDef>.GetNamed(name);Check(r.AllRecipeUsers.Any(u=>u.defName=="LP_Altar") && r.ingredients.Count==1 && Near(r.ingredients[0].GetBaseCount(),20f) && r.GetModExtension<ScribeExtension>().focusCost==30f && RaceGear.CannotCraft(r,sharp)!=null && RaceGear.CannotCraft(r,lichReader)==null,"lich scroll scribing at the ritual table costs 30 focus and 20 cloth: "+name);}
   var scribe=ItemPawn(spot+IntVec3.North*4);scribe.ChangePsylinkLevel(1,false);var sbox=Rituals.Ascend(scribe);Necro.Registry.boxes.Remove(sbox);var slh=Necro.Lich(scribe);slh.autoRepair=false;scribe.psychicEntropy.OffsetPsyfocusDirectly(1f);
   var recipe=DefDatabase<RecipeDef>.GetNamed("LP_ScribeFearGaze");float before=slh.Points;var cloth=ThingMaker.MakeThing(ThingDefOf.Cloth);
   var made=GenRecipe.MakeRecipeProducts(recipe,scribe,new List<Thing>{cloth},cloth,null,null,null,null).ToList();
   Check(made.Count==1 && made[0].def.defName=="LP_ScrollFearGaze" && Math.Abs(before-slh.Points-30f)<.5f,"scribing pays 30 psyfocus points: "+(before-slh.Points));foreach(var t in made)t.Destroy();
   scribe.psychicEntropy.OffsetPsyfocusDirectly(-1f);made=GenRecipe.MakeRecipeProducts(recipe,scribe,new List<Thing>{cloth},cloth,null,null,null,null).ToList();Check(made.Count==0,"without 30 psyfocus the scribing fails");
   cloth.Destroy();scribe.Destroy();six.Destroy();sharp.Destroy();lichReader.Destroy();
  }
  // ---------- 奇物 ----------
  public static readonly string[] ItemWondrousDefs={"LP_BagOfHolding","LP_CloakElvenkind","LP_BootsElvenkind","LP_BootsSpeed","LP_IntellectFortress","LP_RingResistanceFire","LP_RingResistanceCold","LP_RingResistanceAcid","LP_RingResistanceMind","LP_AmuletHealth","LP_IounStone","LP_WandDetection"};
  void CheckWondrous(){
   foreach(var name in ItemWondrousDefs){var d=DefDatabase<ThingDef>.GetNamed(name);Check(!d.ConfigErrors().Any() && d.graphic.MatSingle!=BaseContent.BadMat && d.uiIcon!=BaseContent.BadTex && d.graphicData.texPath.StartsWith("Items/Wondrous/"),"wondrous item has its own texture: "+name);}
   Check(ContentFinder<Texture2D>.Get("Items/Wondrous/IounGlow",false)!=null,"ioun stone glow texture loads");
   var acc=DefDatabase<ApparelLayerDef>.GetNamed("LP_Accessory");Check(acc!=null && new[]{"LP_RingResistanceFire","LP_AmuletHealth","LP_IounStone","LP_BoneTalisman"}.All(n=>DefDatabase<ThingDef>.GetNamed(n).apparel.layers.SequenceEqual(new[]{acc})),"rings, amulet, ioun stones and the bone talisman use the mod's own accessory layer");
   var spot=ItemSpot(3);var p=ItemPawn(spot);Hold(p);
   // 饰品不占其他装备位置：全套护甲 + 护盾腰带 + 戒指 + 护符 + 两颗伊奥恩石 + 骨质护符可以同时穿戴。
   foreach(var n in new[]{"Apparel_PlateArmor","Apparel_SimpleHelmet","Apparel_ShieldBelt","LP_RingResistanceFire","LP_AmuletHealth","LP_IounStone","LP_IounStone","LP_BoneTalisman"})WearItem(p,n);
   Check(p.apparel.WornApparel.Count==8,"accessories are worn on top of a full outfit: "+string.Join(",",p.apparel.WornApparel.Select(a=>a.def.defName)));
   // 1.45.0：最多戴两枚不同的戒指。
   Check(ApparelUtility.CanWearTogether(DefDatabase<ThingDef>.GetNamed("LP_RingResistanceFire"),DefDatabase<ThingDef>.GetNamed("LP_RingResistanceMind"),p.RaceProps.body) && !ApparelUtility.CanWearTogether(DefDatabase<ThingDef>.GetNamed("LP_RingResistanceFire"),DefDatabase<ThingDef>.GetNamed("LP_RingResistanceFire"),p.RaceProps.body),"two different rings can be worn together, two of the same cannot");
   var third=WearItem(p,"LP_IounStone");Check(p.apparel.WornApparel.Count(a=>ItemWondrous.IsIoun(a.def))==2 && p.apparel.WornApparel.Contains(third),"a third ioun stone replaces the oldest one (two at most)");
   foreach(var t in map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("LP_IounStone")).ToList())t.Destroy();
   p.apparel.DestroyAll();
   // 次元袋：搬运+60；受火焰或锐器伤害2%爆开。
   // 1.63.0：搬运量还乘操作能力；随机生成的人偶尔带慢性病（比如关节炎）操作略低，+60 就变成 +58.8。先去掉负面健康状态再量。
   foreach(var bad in p.health.hediffSet.hediffs.Where(h=>h.def.isBad && !(h is Hediff_MissingPart)).ToList())p.health.RemoveHediff(bad);
   float carry=p.GetStatValue(StatDefOf.CarryingCapacity);float move0=p.GetStatValue(StatDefOf.MoveSpeed);var bag=WearItem(p,"LP_BagOfHolding");float carryGain=p.GetStatValue(StatDefOf.CarryingCapacity)-carry;
   // Isekai 等模组会把搬运量整体乘上力量倍率，+60 也跟着放大。
   Check((FullModList?carryGain>=59f:Near(carryGain,60f)) && Near(p.GetStatValue(StatDefOf.MoveSpeed),move0),"bag of holding adds 60 carrying capacity without slowing the wearer: +"+carryGain);
   var gold=ThingMaker.MakeThing(ThingDefOf.Gold);gold.stackCount=20;p.inventory.innerContainer.TryAdd(gold);
   Check(!ItemWondrous.TryBurst(p,new DamageInfo(DamageDefOf.Cut,5f),.5f) && !ItemWondrous.TryBurst(p,new DamageInfo(DamageDefOf.Blunt,5f),.01f) && Items.Worn(p,"LP_BagOfHolding")!=null,"the bag only bursts on a 2% roll from fire or sharp damage");
   Check(ItemWondrous.TryBurst(p,new DamageInfo(DamageDefOf.Flame,5f),.01f) && Items.Worn(p,"LP_BagOfHolding")==null && bag.Destroyed && gold.Spawned && !p.inventory.innerContainer.Contains(gold),"a burst bag is destroyed and spills the inventory");
   gold.Destroy();
   // 精灵斗篷：站立时被远程命中-15%，精灵-30%。
   var elf=RacePawn("LP_Elf");Place(elf,spot+IntVec3.East*2);Hold(elf);WearItem(p,"LP_CloakElvenkind");WearItem(elf,"LP_CloakElvenkind");var bare=ItemPawn(spot+IntVec3.West*2);Hold(bare);
   Check(Near(ItemWondrous.CloakFactorFor(p),.85f) && Near(ItemWondrous.CloakFactorFor(elf),.7f) && Near(ItemWondrous.CloakFactorFor(bare),1f),"elven cloak cuts ranged hit chance by 15% (elves 30%) while standing");
   var sizeField=AccessTools.Field(typeof(ShotReport),"factorFromTargetSize");object rep=new ShotReport();sizeField.SetValue(rep,1f);var args=new object[]{new LocalTargetInfo(elf),rep};AccessTools.Method(typeof(ElvenCloakEvasion),"Postfix").Invoke(null,args);
   Check(Near((float)sizeField.GetValue(args[1]),.7f),"the cloak factor is applied to the native shot report");
   elf.Destroy();p.apparel.DestroyAll();
   // 精灵靴：走动不吵醒睡着的人。
   var boots=WearItem(p,"LP_BootsElvenkind");var hear=AccessTools.Method(typeof(ElvenBootsSilence),"Prefix");
   Check(!(bool)hear.Invoke(null,new object[]{p,ClamorDefOf.Movement}) && (bool)hear.Invoke(null,new object[]{bare,ClamorDefOf.Movement}) && (bool)hear.Invoke(null,new object[]{p,ClamorDefOf.Harm}),"elven boots silence only their wearer's footsteps");
   p.apparel.Remove(boots);boots.Destroy();
   // 疾行靴：疾行1分钟移速×2，冷却1天。
   var speed=WearItem(p,"LP_BootsSpeed");var burst=p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SpeedBurst"));float move=p.GetStatValue(StatDefOf.MoveSpeed);
   bool burstOn=burst!=null && burst.Activate(new LocalTargetInfo(p),new LocalTargetInfo(p));float fastMove=p.GetStatValue(StatDefOf.MoveSpeed);
   // 完整模组列表 / Isekai 里移速有上限或固定加值、技能冷却也可能被缩短：只要求明显加速、冷却至少半天。
   Check(burstOn && (FullModList?fastMove>move*1.5f && burst.CooldownTicksRemaining>30000:Near(fastMove,move*2f) && burst.CooldownTicksRemaining>59000) && TicksLeft(p,"LP_SpeedBurst")==3600,"boots of speed double movement for a minute with a one-day cooldown: move "+move+"->"+fastMove+" cooldown "+burst?.CooldownTicksRemaining);
   p.apparel.Remove(speed);speed.Destroy();Check(p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SpeedBurst"))==null,"taking the boots off removes the speed ability");Items.Remove(p,"LP_SpeedBurst");
   // 心灵屏障头环：心智抗性+40%，读心反噬。
   float mr=DNDMind.Resistance(p);var band=WearItem(p,"LP_IntellectFortress");Check(Near(DNDMind.Resistance(p),mr+.4f),"intellect fortress gives 40% mind resistance");
   var flayer=RacePawn("LP_MindFlayer");Place(flayer,spot+IntVec3.North*2);Hold(flayer);flayer.stances.stunner.StopStun();
   MindFlayerPowers.ReadMind(flayer,p);Check(flayer.stances.stunner.Stunned && Items.Has(flayer,MindFlayer.Blasted),"reading the mind of an intellect fortress wearer backlashes on the mind flayer");
   flayer.stances.stunner.StopStun();Items.Remove(flayer,MindFlayer.Blasted.defName);MindFlayerPowers.ReadMind(flayer,bare);Check(!flayer.stances.stunner.Stunned,"no backlash without the headband");
   flayer.Destroy();p.apparel.Remove(band);band.Destroy();
   // 抗性戒指。
   var fire=WearItem(p,"LP_RingResistanceFire");Check(Near(ItemDamage.Factor(p,DamageDefOf.Flame),.5f) && Near(ItemDamage.Factor(p,DamageDefOf.Cut),1f),"ring of fire resistance halves fire damage");p.apparel.Remove(fire);fire.Destroy();
   var cold=WearItem(p,"LP_RingResistanceCold");Check(Near(ItemDamage.Factor(p,DamageDefOf.Frostbite),.5f) && (DefDatabase<DamageDef>.GetNamedSilentFail("LP_IceRay")==null || Near(ItemDamage.Factor(p,DefDatabase<DamageDef>.GetNamed("LP_IceRay")),.5f)),"ring of cold resistance halves cold damage");p.apparel.Remove(cold);cold.Destroy();
   var acid=WearItem(p,"LP_RingResistanceAcid");Check(Near(ItemDamage.Factor(p,DefDatabase<DamageDef>.GetNamed("AcidBurn")),.5f) && Near(ItemDamage.Factor(p,ItemThrowables.Acid),.5f),"ring of acid resistance halves acid damage");p.apparel.Remove(acid);acid.Destroy();
   mr=DNDMind.Resistance(p);var mind=WearItem(p,"LP_RingResistanceMind");Check(Near(DNDMind.Resistance(p),mr+.3f),"ring of mind resistance gives 30% mind resistance");p.apparel.Remove(mind);mind.Destroy();
   // 健康护符：不染病。
   var amulet=WearItem(p,"LP_AmuletHealth");var flu=DefDatabase<HediffDef>.GetNamed("Flu");p.health.AddHediff(flu);var infection=DefDatabase<HediffDef>.GetNamed("WoundInfection");
   Check(!p.health.hediffSet.HasHediff(flu) && ItemWondrous.Disease(flu) && ItemWondrous.Disease(infection) && !ItemWondrous.Disease(HediffDefOf.Cut),"amulet of health blocks diseases");
   bare.health.AddHediff(flu);Check(bare.health.hediffSet.HasHediff(flu),"people without the amulet still catch the flu");bare.health.RemoveHediff(bare.health.hediffSet.GetFirstHediffOfDef(flu));
   p.apparel.Remove(amulet);amulet.Destroy();
   // 伊奥恩石：对应技能学习+50%，两颗同技能+100%。
   var stone=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_IounStone"));var comp=stone.GetComp<CompIounStone>();
   Check(comp.skill!=null && ItemWondrous.IounSkills.Contains(comp.skill) && stone.LabelNoCount.Contains(comp.skill.label),"an ioun stone rolls one of seven skills when made: "+comp.skill?.defName);
   comp.skill=SkillDefOf.Crafting;p.apparel.Wear(stone);var sk=p.skills.GetSkill(SkillDefOf.Crafting);sk.Level=3;float x0=MindFlayer.OwnXp(sk);p.skills.Learn(SkillDefOf.Crafting,100f,false,true);
   Check(Near(MindFlayer.OwnXp(sk)-x0,150f) && Near(ItemWondrous.LearnFactor(p,SkillDefOf.Shooting),1f),"ioun stone boosts its own skill's learning by 50%");
   var stone2=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_IounStone"));stone2.GetComp<CompIounStone>().skill=SkillDefOf.Crafting;p.apparel.Wear(stone2);Check(Near(ItemWondrous.LearnFactor(p,SkillDefOf.Crafting),2f),"two crafting ioun stones double crafting learning");
   p.apparel.DestroyAll();
   // 探测魔杖：放进储存区就揭示整张地图的隐形者。
   var ghost=ItemPawn(spot+IntVec3.North*3);Drink(ghost,"LP_PotionInvisibility");var wcell=map.AllCells.First(c=>c.Standable(map) && !c.Fogged(map) && c.GetZone(map)==null && c.GetFirstItem(map)==null && c.GetFirstPawn(map)==null && c.GetEdifice(map)==null && c.DistanceTo(spot)>8);
   var wand=GenSpawn.Spawn(MakeItem("LP_WandDetection"),wcell,map);var wm=Items.MapState(map);wm.RefreshWand();
   Check(!ItemWondrous.WandStored(map) && ghost.IsPsychologicallyInvisible(),"a wand lying outside storage does nothing");
   var zone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);map.zoneManager.RegisterZone(zone);zone.AddCell(wcell);wm.RefreshWand();
   Check(ItemWondrous.WandStored(map) && Items.Revealed(map) && !ghost.IsPsychologicallyInvisible(),"a wand of detection kept in storage reveals invisible pawns on the map");
   zone.Delete();wm.RefreshWand();Check(ghost.IsPsychologicallyInvisible(),"invisibility returns when the wand leaves storage");
   wand.Destroy();ghost.Destroy();bare.Destroy();p.Destroy();
  }
  // ---------- C. 默认值 ----------
  void CheckItemDefaults(){
   Check(new Settings().trollBleedDays==3,"troll blood draw cooldown defaults to 3 days");
   var spot=ItemSpot(2);var troll=RacePawn("LP_Troll");Place(troll,spot);Hold(troll);
   var worker=(Recipe_ExtractTrollBlood)DefDatabase<RecipeDef>.GetNamed("LP_ExtractTrollBlood").Worker;int old=LichMod.Settings.trollBleedDays;
   try{
    LichMod.Settings.trollBleedDays=3;
    Check(worker.AvailableOnNow(troll),"a troll that was not bled recently can be bled");
    worker.ApplyOnPawn(troll,null,null,new List<Thing>(),null);
    Check(Recipe_ExtractTrollBlood.RecentlyBled(troll) && TicksLeft(troll,"LP_RecentlyBled")==3*GenDate.TicksPerDay && !worker.AvailableOnNow(troll),"after a blood draw the same troll cannot be bled again for 3 days");
    Items.Remove(troll,"LP_RecentlyBled");Check(worker.AvailableOnNow(troll),"the troll can be bled again once the cooldown ends");
    LichMod.Settings.trollBleedDays=5;worker.ApplyOnPawn(troll,null,null,new List<Thing>(),null);
    Check(TicksLeft(troll,"LP_RecentlyBled")==5*GenDate.TicksPerDay,"the cooldown follows the settings page (5 days)");
   }finally{LichMod.Settings.trollBleedDays=old;}
   foreach(var t in map.listerThings.ThingsOfDef(Items.Thing("LP_TrollBlood")).Where(t=>t.Position.InHorDistOf(spot,4)).ToList())t.Destroy();
   Troll.Gene(troll)?.CancelRevival(false);troll.Destroy();
   Check(ItemScrolls.RevivifyMaxAge==GenDate.TicksPerDay,"revivify works only on corpses dead for at most 1 day");
   Check(DefDatabase<ThoughtDef>.GetNamed("LP_SawTendrilCloak").stages[0].baseMoodEffect==-2 && DefDatabase<ThoughtDef>.GetNamed("LP_SawTendrilCloak").stackLimit<=1,"the tendril cloak stays with a single -2 mood thought");
  }
  // ---------- 商队货单 ----------
  static IEnumerable<TraderKindDef> ModTraders=>DefDatabase<TraderKindDef>.AllDefsListForReading.Where(k=>k.defName.StartsWith("LP_Caravan_"));
  static bool Sells(TraderKindDef k,string def){var d=DefDatabase<ThingDef>.GetNamed(def);return k.stockGenerators.Any(g=>(g is StockGenerator_SingleDef || g is StockGenerator_Tag) && g.HandlesThingDef(d));}
  static bool Craftable(string def)=>DefDatabase<RecipeDef>.AllDefsListForReading.Any(r=>r.products.Any(p=>p.thingDef.defName==def));
  void CheckItemTrade(){
   foreach(var (def,_,_) in ItemRaceGear)Check(ModTraders.Any(k=>Sells(k,def)),"racial gear is sold by at least one faction caravan: "+def);
   foreach(var def in new[]{"LP_Mithral","LP_Adamantine"})Check(ModTraders.Count(k=>Sells(k,def))>=1,"rare metal comes from caravans as well as veins: "+def);
   Check(ModTraders.Where(k=>Sells(k,"LP_PotionInvisibility")).Select(k=>k.defName).SequenceEqual(new[]{"LP_Caravan_BlackNetwork"}) && !Craftable("LP_PotionInvisibility"),"invisibility potions are only bought from the black network");
   foreach(var def in new[]{"LP_Thunderstone","LP_DispelDust"})Check(ModTraders.Any(k=>Sells(k,def)) && !Craftable(def),"cannot be crafted, only bought or looted: "+def);
   Check(ModTraders.Any(k=>Sells(k,"LP_ScrollRevivify")) && Near(DefDatabase<ThingDef>.GetNamed("LP_ScrollRevivify").BaseMarketValue,3000f),"revivify scrolls are rare caravan stock at about 3000 silver");
   foreach(var def in ItemWondrousDefs.Concat(new[]{"LP_PotionHealing","LP_ScrollTeleport","LP_ScrollFireball"}))Check(ModTraders.Any(k=>Sells(k,def)),"caravans stock "+def);
   // 实际生成一次红袍商人的货单：卷轴和奇物按标签进货。
   var stock=new List<Thing>();Rand.PushState(2210);try{foreach(var g in DefDatabase<TraderKindDef>.GetNamed("LP_Caravan_RedRobe").stockGenerators)stock.AddRange(g.GenerateThings(map.Tile,Faction.OfPlayer));}finally{Rand.PopState();}
   Check(stock.Any(t=>t.def.thingCategories?.Count>0 && t.def.tradeTags?.Contains("LP_Scroll")==true) && stock.Any(t=>t.def.tradeTags?.Contains("LP_Wondrous")==true),"a generated Thay red robe stock contains scrolls and wondrous items: "+string.Join(",",stock.Where(t=>t.def.defName.StartsWith("LP_")).Select(t=>t.def.defName)));
   foreach(var t in stock)if(!t.Destroyed)t.Destroy();
  }
  // ---------- D. 联动：Isekai、VPE ----------
  void CheckItemIntegrations(){
   bool isekai=ModsConfig.IsActive("jellycreative.isekaileveling");
   foreach(var name in new[]{"Caravan_Outlander_Exotic","Orbital_Exotic"}){var k=DefDatabase<TraderKindDef>.GetNamedSilentFail(name);if(k!=null)Check(Sells(k,"LP_PotionHealing")==isekai,"the vanilla exotic trader "+name+" sells healing potions exactly when Isekai is active ("+isekai+")");}
   bool dungeons=GenTypes.GetTypeInAnyAssembly(IsekaiLoot.ChestType)!=null;
   Check(IsekaiLoot.Patched==dungeons,"Isekai treasure chests get extra DND loot exactly when the dungeon add-on is loaded ("+dungeons+")");
   Check(IsekaiLoot.CommonLoot.Concat(IsekaiLoot.RareLoot).All(n=>Items.Thing(n)!=null),"chest loot names resolve to item definitions");
   var rng=new System.Random(1022);Func<float> r=()=>(float)rng.NextDouble();int[] got=new int[5];int rareLow=0,rareHigh=0;
   for(int tier=1;tier<=4;tier++)for(int i=0;i<2000;i++){var loot=IsekaiLoot.Roll(tier,r);got[tier]+=loot.Count;int rare=loot.Count(n=>IsekaiLoot.RareLoot.Contains(n));if(tier<3)rareLow+=rare;else rareHigh+=rare;}
   Check(got[1]>600 && got[1]<1000 && got[4]>got[3] && got[3]>got[2] && rareLow==0 && rareHigh>0,"better chests give more DND loot and only tier 3+ can give rare scrolls ("+string.Join("/",got.Skip(1))+", rare "+rareHigh+")");
   if(dungeons){
    var chests=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.thingClass?.FullName==IsekaiLoot.ChestType).ToList();var chestDef=chests.FirstOrDefault(d=>d.defName=="IsekaiDungeons_TreasureChestGrand")??chests.First();
    var spot=ItemSpot(2);var chest=GenSpawn.Spawn(ThingMaker.MakeThing(chestDef),spot,map);var prize=ThingMaker.MakeThing(ThingDefOf.Steel);
    Rand.PushState(77);int expected;try{expected=IsekaiLoot.Roll(IsekaiLoot.Tier(chest),()=>Rand.Value).Count;}finally{Rand.PopState();}
    expected+=GunLoot.ForChest(chest).Count;// 1.29.0 宝箱里的 DND 枪械与法杖（按宝箱编号播种，不动全局随机数）
    var before=new HashSet<Thing>(map.listerThings.AllThings);Rand.PushState(77);try{AccessTools.Method(chest.GetType(),"Award").Invoke(chest,new object[]{prize});}finally{Rand.PopState();}
    var extra=map.listerThings.AllThings.Where(t=>!before.Contains(t) && t.def.defName.StartsWith("LP_")).ToList();
    Check(extra.Count==expected && prize.Spawned && chest.Destroyed,"opening a real Isekai chest awards its prize plus "+expected+" DND items ("+string.Join(",",extra.Select(t=>t.def.defName))+")");
    foreach(var t in extra)t.Destroy();if(!prize.Destroyed)prize.Destroy();
   }
   // VPE：灵能隐形用的是原版“心灵隐形”，识破/探测/驱散一视同仁；VPE 护盾灵能被驱散粉尘直接消散。
   var spot2=ItemSpot(2);var ghost=ItemPawn(spot2);ghost.health.AddHediff(DefDatabase<HediffDef>.GetNamed("PsychicInvisibility"));var wm=Items.MapState(map);
   Check(ghost.IsPsychologicallyInvisible(),"psychic invisibility fixture (used by VPE's invisibility psycast) is invisible");
   wm.revealUntil=Find.TickManager.TicksGame+100;Check(!ghost.IsPsychologicallyInvisible(),"true seeing reveals psychic invisibility too");wm.revealUntil=-1;
   ItemThrowables.Dispel(map,spot2);Check(!ghost.IsPsychologicallyInvisible(),"dispel dust reveals psychic invisibility");
   var shieldDef=DefDatabase<HediffDef>.GetNamedSilentFail("VPE_Overshield");
   if(shieldDef!=null){Items.Remove(ghost,"LP_MagicDispelled");ghost.health.AddHediff(shieldDef);Check(ghost.health.hediffSet.HasHediff(shieldDef),"VPE overshield fixture");ItemThrowables.Dispel(map,spot2);Check(!ghost.health.hediffSet.HasHediff(shieldDef),"dispel dust strips a VPE overshield");}
   ghost.Destroy();
  }
  // ---------- E. 1.22.1 肉制婴儿食品：电炉灶/燃料炉灶多两张配方，产物是原版婴儿食品；原版素食配方不变。 ----------
  void CheckBabyFoodMeat(){
   ThingDef T(string n)=>DefDatabase<ThingDef>.GetNamed(n);
   var babyFood=T("BabyFood");var cow=T("Meat_Cow");
   foreach(var (name,count) in new[]{("LP_Make_BabyFoodMeat",10),("LP_Make_BabyFoodMeatBulk",40)}){
    var r=DefDatabase<RecipeDef>.GetNamedSilentFail(name);
    Check(r!=null,"meat baby food recipe exists: "+name);if(r==null)continue;
    foreach(var stove in new[]{"ElectricStove","FueledStove"})Check(T(stove).AllRecipes.Contains(r),stove+" can cook "+name);
    Check(r.products.Count==1 && r.products[0].thingDef==babyFood && r.products[0].count==count,name+" makes "+count+" vanilla baby food");
    Check(r.fixedIngredientFilter.Allows(cow) && r.fixedIngredientFilter.Allows(T("RawPotatoes")) && !r.fixedIngredientFilter.Allows(T("EggChickenFertilized")),name+" accepts raw meat and vegetables but never fertilized eggs");
    Check(r.defaultIngredientFilter.Allows(cow) && !r.defaultIngredientFilter.Allows(T("Meat_Human")) && !r.defaultIngredientFilter.Allows(T("RawPotatoes")),name+" defaults to raw meat without human meat or vegetables");
   }
   Check(!DefDatabase<RecipeDef>.GetNamed("Make_BabyFood").fixedIngredientFilter.Allows(cow),"vanilla Make_BabyFood still refuses meat");
   // 真做一份：婴儿食品记住了牛肉，不再是素食。
   var cook=SharedPawn();var meat=ThingMaker.MakeThing(cow);meat.stackCount=5;var stoveThing=ThingMaker.MakeThing(T("ElectricStove"));
   var made=GenRecipe.MakeRecipeProducts(DefDatabase<RecipeDef>.GetNamed("LP_Make_BabyFoodMeat"),cook,new List<Thing>{meat},meat,(IBillGiver)stoveThing,null,null,null).ToList();
   var ing=made.FirstOrDefault(t=>t.def==babyFood)?.TryGetComp<CompIngredients>();
   Check(ing!=null && ing.ingredients.Contains(cow),"baby food cooked from cow meat lists Meat_Cow as an ingredient ("+string.Join(",",made.Select(t=>t.def.defName))+")");
   foreach(var t in made)if(!t.Destroyed)t.Destroy();if(!meat.Destroyed)meat.Destroy();stoveThing.Destroy();
  }
  static string itemBledId,itemStatueId,itemStatuePawnId,itemRunnerId;static int itemBledTicks,itemRunnerCooldown;
  void PrepareItemsSave(){
   var p=SharedPawn();Drink(p,"LP_PotionGiantStrength");Drink(p,"LP_PotionTrollRegen");
   itemPotionSaveId=p.ThingID;itemPotionSavedTicks=TicksLeft(p,"LP_PotionGiantStrength");itemTonicSavedTicks=TicksLeft(p,"LP_TrollRegenTonic");
   Check(itemPotionSavedTicks==30000 && itemTonicSavedTicks==180000,"potion save fixture starts with full durations");
   // 抽血冷却、石像、疾行靴冷却：存档前不推进游戏刻。
   var bled=RacePawn("LP_Troll");Place(bled,ItemSpot(1));Hold(bled);Items.Give(bled,"LP_RecentlyBled",2*GenDate.TicksPerDay+777);itemBledId=bled.ThingID;itemBledTicks=TicksLeft(bled,"LP_RecentlyBled");
   var stoned=ItemPawn(ItemSpot(1));stoned.SetFaction(EnemyFaction());itemStatuePawnId=stoned.ThingID;var statue=ItemScrolls.Petrify(stoned);
   Check(statue!=null && statue.Pawn==stoned,"statue save fixture holds its pawn");itemStatueId=statue.ThingID;
   var runner=ItemPawn(ItemSpot(1));Hold(runner);WearItem(runner,"LP_BootsSpeed");var burst=runner.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SpeedBurst"));
   Check(burst.Activate(new LocalTargetInfo(runner),new LocalTargetInfo(runner)),"speed boots save fixture is on cooldown");itemRunnerId=runner.ThingID;itemRunnerCooldown=burst.CooldownTicksRemaining;
  }
  void CheckItemsAfterLoad(){
   map=Find.CurrentMap;var p=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==itemPotionSaveId);
   Check(TicksLeft(p,"LP_PotionGiantStrength")==itemPotionSavedTicks && TicksLeft(p,"LP_TrollRegenTonic")==itemTonicSavedTicks,"timed potion effects keep their remaining time across save/load");
   Check(p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_TrollRegenTonic")) is Hediff_TrollTonic,"loaded troll tonic keeps its healing class");
   p.Destroy();
   var bled=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==itemBledId);
   Check(TicksLeft(bled,"LP_RecentlyBled")==itemBledTicks && !DefDatabase<RecipeDef>.GetNamed("LP_ExtractTrollBlood").Worker.AvailableOnNow(bled),"the blood draw cooldown survives save/load");
   Troll.Gene(bled)?.CancelRevival(false);bled.Destroy();
   var statue=map.listerThings.AllThings.OfType<PetrifiedStatue>().SingleOrDefault(x=>x.ThingID==itemStatueId);
   Check(statue!=null && statue.Pawn?.ThingID==itemStatuePawnId && !statue.Pawn.Dead,"a statue keeps the same petrified pawn across save/load");
   var back=statue.Restore();Check(back!=null && back.Spawned && back.ThingID==itemStatuePawnId,"a loaded statue can still be restored");back.Destroy();
   var runner=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==itemRunnerId);var burst=runner.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SpeedBurst"));
   Check(burst!=null && burst.CooldownTicksRemaining==itemRunnerCooldown && Items.Has(runner,"LP_SpeedBurst"),"speed boots keep their one-day cooldown across save/load: "+burst?.CooldownTicksRemaining+"/"+itemRunnerCooldown);
   runner.apparel.DestroyAll();runner.Destroy();
  }
 }
}
