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
  static string savedDragonId;
  static readonly string[] dragonGenes={"LP_ForceDragonHeart","LP_ForceImmunity","LP_DragonClaws","LP_DragonScales","LP_DragonBody","LP_DragonCarnivore","LP_DragonArrogance","LP_DragonHoarder"};
  static readonly string[] dragonVanillaGenes={"Ageless","DarkVision","Headbone_MiniHorns","Tail_Smooth","Skin_SheerWhite","Beauty_Beautiful","AptitudeStrong_Intellectual","AptitudePoor_Social","Robust","RobustDigestion","StrongStomach","Sterile"};
  // 留在世界里的存档测试龙要有正常背景，否则原版生成配偶时会读到空的背景数据。
  Pawn GenerateDragon(XenotypeDef xenotype,bool noBackstory=true)=>Unscathed(PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:xenotype,mustBeCapableOfViolence:true,forceNoBackstory:noBackstory,fixedBiologicalAge:30,fixedChronologicalAge:30)));
  // 找一块平地（允许屋顶、树木），清掉其中的植物和散落物品，作为力能龙的测试场地。
  IntVec3 OpenArea(int radius,IntVec3 avoid){
   var c=map.AllCells.Where(x=>x.x>radius+10 && x.z>radius+10 && x.x<map.Size.x-radius-10 && x.z<map.Size.z-radius-10 && x.DistanceTo(avoid)>radius+8).OrderBy(x=>x.DistanceToSquared(map.Center)).First(x=>CellRect.CenteredOn(x,radius).Cells.All(y=>!y.Fogged(map) && y.GetEdifice(map)==null && y.GetFirstPawn(map)==null && y.GetTerrain(map).passability==Traversability.Standable && !y.GetTerrain(map).IsWater));
   foreach(var y in CellRect.CenteredOn(c,radius).Cells)foreach(var t in y.GetThingList(map).Where(t=>t.def.category==ThingCategory.Item || t is Plant || t.def.category==ThingCategory.Filth).ToList())t.Destroy();
   return c;
  }
  static float Stat(StatDef stat,Pawn p)=>stat.Worker.GetValue(p);
  // 1.20.0：只看龙鳞自己带来的增量（龙龄 hediff 阶段里的护甲加值），不受其他模组改护甲的影响。
  static float ScaleOffset(StatDef stat,Pawn p)=>ForceDragon.AgeOf(p)?.CurStage?.statOffsets?.Where(m=>m.stat==stat).Sum(m=>m.value)??0f;
  // 有龙鳞和暂时去掉龙鳞时的实际护甲差必须为正（其他模组可能按倍率改护甲，所以不比精确值）。
  bool ScalesRaiseArmor(Pawn p){
   var gene=p.genes.GetGene(ForceDragon.ScalesGene);if(gene==null)return false;bool endo=p.genes.Endogenes.Contains(gene);
   float sharp=Stat(StatDefOf.ArmorRating_Sharp,p),blunt=Stat(StatDefOf.ArmorRating_Blunt,p);
   p.genes.RemoveGene(gene);float sharp0=Stat(StatDefOf.ArmorRating_Sharp,p),blunt0=Stat(StatDefOf.ArmorRating_Blunt,p);
   p.genes.AddGene(ForceDragon.ScalesGene,!endo);
   return sharp>sharp0+.01f && blunt>blunt0+.01f && Near(Stat(StatDefOf.ArmorRating_Sharp,p),sharp);
  }
  static float InjurySum(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity);
  static float ClawDamage(Verb_MeleeAttackDamage verb,Pawn target){
   var method=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply");
   Rand.PushState(777);
   try{return ((IEnumerable<DamageInfo>)method.Invoke(verb,new object[]{new LocalTargetInfo(target)})).Sum(d=>d.Amount);}finally{Rand.PopState();}
  }
  // 1.23.2：原版食物中毒概率倍率（含难度与基因）；找不到原版方法时返回-1，让检查失败。
  static float FoodPoisonFactor(Pawn p){var m=AccessTools.Method(typeof(FoodUtility),"GetFoodPoisonChanceFactor",new[]{typeof(Pawn)});return m==null?-1f:(float)m.Invoke(null,new object[]{p});}
  // 1.23.1：原版食物消耗里与基因有关的两项（龙龄等 hediff 倍率 × 基因代谢率曲线）相乘。
  static float HungerTotal(Pawn p)=>p.health.hediffSet.HungerRateFactor*ForceDragon.MetabolismFactor(ForceDragon.Metabolism(p));
  static string HungerInfo(Pawn p)=>"stage="+p.DevelopmentalStage+" met="+ForceDragon.Metabolism(p)+" gene×"+ForceDragon.MetabolismFactor(ForceDragon.Metabolism(p))+" hediff×"+p.health.hediffSet.HungerRateFactor+" total="+HungerTotal(p);
  Pawn GenerateDragonChild(XenotypeDef xenotype)=>Unscathed(PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:true,canGeneratePawnRelations:false,forcedXenotype:xenotype,developmentalStages:DevelopmentalStage.Child,forceNoBackstory:true,fixedBiologicalAge:8,fixedChronologicalAge:8)));
  static void ChargeShield(CompShield shield){var t=Traverse.Create(shield);t.Field("energy").SetValue(t.Property("EnergyMax").GetValue<float>());t.Field("ticksToReset").SetValue(-1);}

  void CheckForceDragon(){
   var xenotype=DefDatabase<XenotypeDef>.GetNamed("LP_ForceDragon");
   Check(!xenotype.ConfigErrors().Any() && xenotype.inheritable && dragonGenes.All(n=>xenotype.genes.Contains(DefDatabase<GeneDef>.GetNamed(n))),"force dragon is an inheritable xenotype with all eight dragon genes");
   foreach(var n in dragonVanillaGenes){var def=DefDatabase<GeneDef>.GetNamedSilentFail(n);Check(def!=null && xenotype.genes.Contains(def),"force dragon uses vanilla gene: "+n);}
   foreach(var n in dragonGenes)Check(!DefDatabase<GeneDef>.GetNamed(n).ConfigErrors().Any(),"dragon gene definition loads cleanly: "+n);
   foreach(var a in ForceDragon.Abilities){var def=DefDatabase<AbilityDef>.GetNamedSilentFail(a.name);Check(def!=null && !def.ConfigErrors().Any() && def.comps.OfType<CompProperties_ForceDragonAbility>().Any(),"dragon ability loads cleanly: "+a.name);}
   Check(ForceDragon.HeartGene.biostatArc>0 && ForceDragon.HeartGene.resourceGizmoType==typeof(GeneGizmo_ForceResource),"dragon heart is an archite resource gene with a force bar");
   Check(DefDatabase<GeneDef>.GetNamed("LP_ForceImmunity").damageFactors.Any(d=>d.damageDef==ForceDragon.Force && d.factor==0f) && Near(ForceDragon.Force.buildingDamageFactor,3f),"force damage deals triple building damage and dragons are immune");
   Check(!DefDatabase<GeneDef>.AllDefs.Any(g=>g.defName=="LP_ForceConstitution"),"no shield-forbidding force constitution gene exists");
   var paths=dragonGenes.Select(n=>DefDatabase<GeneDef>.GetNamed(n).iconPath).Concat(ForceDragon.Abilities.Select(a=>DefDatabase<AbilityDef>.GetNamed(a.name).iconPath)).Concat(new[]{xenotype.iconPath,"Other/ForceField"});
   foreach(var path in paths.Distinct())Check(ContentFinder<Texture2D>.Get(path,false)!=null,"force dragon placeholder art loads: "+path);
   Check(DragonHoard.EggDef.graphic!=null && DragonHoard.EggDef.graphic.MatSingle!=BaseContent.BadMat && DefDatabase<ThingDef>.GetNamed("LP_ForceWall").graphic.MatSingle!=BaseContent.BadMat,"dragon egg and force wall placeholder graphics resolve");
   Check(LichMod.Settings.dragonAgePercent==100 && LichMod.Settings.hoardTheftRage && LichMod.Settings.dragonEggHatchDays==30,"force dragon settings default to 100% age gates, theft rage on, 30 hatch days");

   var area=OpenArea(9,altar.Position);
   var dragon=GenerateDragon(xenotype);GenSpawn.Spawn(dragon,area,map);
   var heart=ForceDragon.Heart(dragon);
   Check(heart!=null && dragon.genes.Xenotype==xenotype && dragon.genes.Endogenes.Any(g=>g.def==ForceDragon.HeartGene),"generated force dragon carries the heart as an inheritable endogene");
   Check(ForceDragon.Age(dragon)==1 && Near(heart.Max,.6f) && heart.Value>0,"new force dragon is a wyrmling with 60 force capacity");
   Check(dragon.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_ForceBreath"))!=null && dragon.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_DevourShield"))!=null && dragon.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_ResilientSphere"))==null,"wyrmling knows breath and devour shield but not resilient sphere");
   // 1.27.1 龙之长眠：成年龙抵消基因代谢率，总饥饿速率 = H(龙龄)，幼龙约57.3%。
   Check(!ForceDragon.Juvenile(dragon) && Near(ForceDragon.MetabolismFactor(ForceDragon.Metabolism(dragon)),2.25f) && Near(HungerTotal(dragon),Satiety.DragonHunger(1)) && Near(Satiety.DragonHunger(1),.573f),"adult wyrmling eats at the long-sleep rate 57.3%: "+HungerInfo(dragon));
   Check(ForceDragon.StomachGene!=null && Near(ForceDragon.StomachGene.foodPoisoningChanceFactor,0f) && dragon.genes.Endogenes.Any(g=>g.def==ForceDragon.StomachGene) && dragon.genes.HasActiveGene(ForceDragon.StomachGene) && Near(FoodPoisonFactor(dragon),0f),"force dragon has strong stomach and never gets food poisoning: factor "+FoodPoisonFactor(dragon));
   Check(ForceDragon.Heart(dragon).stomachMigrated,"newly generated dragon is already marked as strong stomach migrated");
   {
    var legacy=GenerateDragon(xenotype);var legacyHeart=ForceDragon.Heart(legacy);
    legacy.genes.RemoveGene(legacy.genes.GetGene(ForceDragon.StomachGene));legacyHeart.stomachMigrated=false;
    Check(!legacy.genes.HasActiveGene(ForceDragon.StomachGene) && Near(FoodPoisonFactor(legacy),0f),"legacy dragon without strong stomach still cannot get food poisoning (force dragonborn are a legendary race, 1.52.0 hotfix)");
    ForceDragon.Migrate(legacy);
    Check(legacy.genes.Endogenes.Any(g=>g.def==ForceDragon.StomachGene) && legacyHeart.stomachMigrated && Near(FoodPoisonFactor(legacy),0f),"legacy dragon receives strong stomach as an endogene on load");
    legacy.genes.RemoveGene(legacy.genes.GetGene(ForceDragon.StomachGene));ForceDragon.Migrate(legacy);
    Check(legacy.genes.GetGene(ForceDragon.StomachGene)==null,"strong stomach removed by the player is not added back on later loads");
    legacy.Destroy();
   }
   // 龙体：部位血量沿用 PartHealth 机制
   var arm=dragon.RaceProps.body.AllParts.First(p=>p.def.defName=="Arm");
   // 1.23.0：龙体×1.5 改为 HealthScale 因子（与巨躯规则取较大值），原版部位上限已含在内。
   float armBase=Mathf.Ceil(arm.def.hitPoints*dragon.HealthScale/BodySize.HealthFactor(dragon));
   Check(Near(BodySize.HealthFactor(dragon),1.5f) && Near(PartHealth.WithoutReinforcement(arm,dragon),Mathf.Ceil(arm.def.hitPoints*dragon.HealthScale/1.5f*1.5f)) && Near(dragon.health.hediffSet.GetPartHealth(arm),PartHealth.WithoutReinforcement(arm,dragon)),"dragon body gene raises native part health by 50%: "+armBase+" to "+dragon.health.hediffSet.GetPartHealth(arm));
   // 龙鳞
   Check(Near(ScaleOffset(StatDefOf.ArmorRating_Sharp,dragon),.3f) && Near(ScaleOffset(StatDefOf.ArmorRating_Blunt,dragon),.15f) && ScalesRaiseArmor(dragon),"wyrmling scales give 30% sharp and 15% blunt armor (scale offsets "+ScaleOffset(StatDefOf.ArmorRating_Sharp,dragon)+"/"+ScaleOffset(StatDefOf.ArmorRating_Blunt,dragon)+", total "+Stat(StatDefOf.ArmorRating_Sharp,dragon)+"/"+Stat(StatDefOf.ArmorRating_Blunt,dragon)+")");
   // 护盾腰带可以穿
   var beltDef=DefDatabase<ThingDef>.GetNamed("Apparel_ShieldBelt");
   var belt=(Apparel)ThingMaker.MakeThing(beltDef);
   Check(ApparelUtility.HasPartsToWear(dragon,beltDef),"force dragon may wear a shield belt");
   dragon.apparel.Wear(belt);
   Check(dragon.apparel.WornApparel.Contains(belt),"force dragon wears a shield belt");
   dragon.apparel.Remove(belt);belt.Destroy();
   // 龙爪
   var claws=dragon.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().FirstOrDefault(v=>v.HediffCompSource?.parent?.def==ForceDragon.ClawWeapon);
   Check(dragon.health.hediffSet.HasHediff(ForceDragon.ClawWeapon) && claws!=null && claws.tool.capacities.Any(c=>c.defName=="Scratch") && Near(claws.tool.power,16f),"dragon claws give a 16 power sharp melee attack");
   var victim=Person();victim.apparel.DestroyAll();GenSpawn.Spawn(victim,area+IntVec3.West*3,map);
   float clawAge1=ClawDamage(claws,victim);
   Check(clawAge1>0,"claw damage resolves at wyrmling age: "+clawAge1);
   // 力场伤害与免疫
   float injuries=InjurySum(victim);
   victim.TakeDamage(new DamageInfo(ForceDragon.Force,10f,2f,-1f,null));
   Check(InjurySum(victim)>injuries,"force damage injures ordinary pawns");
   foreach(var h in victim.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())victim.health.RemoveHediff(h);
   dragon.TakeDamage(new DamageInfo(ForceDragon.Force,10f,0f,-1f,null));
   Check(InjurySum(dragon)==0,"force dragon is immune to force damage");
   var testWall=ThingMaker.MakeThing(ThingDefOf.Wall,DefDatabase<ThingDef>.GetNamed("BlocksGranite"));GenSpawn.Spawn(testWall,area+IntVec3.South*6,map);
   int hp=testWall.HitPoints;testWall.TakeDamage(new DamageInfo(ForceDragon.Force,10f,0f,-1f,null));
   Check(hp-testWall.HitPoints==30,"force damage deals 3x to buildings: "+(hp-testWall.HitPoints));
   testWall.Destroy();
   // 偏折、朦胧、EMP
   Check(Near(ForceDragon.DeflectChance(1),.15f) && Near(ForceDragon.DeflectChance(3),.25f) && Near(ForceDragon.DeflectChance(5),.35f) && Near(ForceDragon.DeflectChance(9),.35f),"deflection grows 15% to 35% and is capped at 35%");
   Check(Near(ForceDragon.EvadeChance(1),0) && Near(ForceDragon.EvadeChance(2),.2f) && Near(ForceDragon.EvadeChance(4),.5f),"blur at young age is 20%, displacement at ancient age is 50%");
   var bullet=new DamageInfo(DamageDefOf.Bullet,5f,0f,-1f,victim);var cut=new DamageInfo(DamageDefOf.Cut,5f,0f,-1f,victim);
   heart.Value=heart.Max;
   int deflected=0,meleeAvoided=0;Rand.PushState(42);
   try{for(int i=0;i<2000;i++){if(ForceDragon.Avoid(dragon,bullet)!=null)deflected++;if(ForceDragon.Avoid(dragon,cut)!=null)meleeAvoided++;}}finally{Rand.PopState();}
   Check(deflected>220 && deflected<380 && meleeAvoided==0,"wyrmling deflects about 15% of ranged hits and no melee: "+deflected+"/2000");
   heart.Value=.1f;
   Check(Enumerable.Range(0,300).All(_=>ForceDragon.Avoid(dragon,bullet)==null),"deflection switches off below 20 force");
   heart.Value=heart.Max;
   float forceBefore=heart.Value;
   dragon.TakeDamage(new DamageInfo(DamageDefOf.EMP,30f,0f,-1f,victim));
   Check(!dragon.stances.stunner.Stunned && Near(heart.Value,forceBefore) && Near(ForceDragon.DeflectChance(ForceDragon.Age(dragon)),.15f),"EMP neither stuns the dragon nor drains its force or deflection");
   // 力场吐息：打碎护盾、3倍拆建筑、不点火
   var enemy=Person();enemy.apparel.DestroyAll();GenSpawn.Spawn(enemy,area+IntVec3.East*2,map);
   var enemyBelt=(Apparel)ThingMaker.MakeThing(beltDef);enemy.apparel.Wear(enemyBelt);var shield=enemyBelt.GetComp<CompShield>();ChargeShield(shield);
   Check(shield.ShieldState==ShieldState.Active && shield.Energy>0,"test shield belt is charged");
   var cover=ThingMaker.MakeThing(ThingDefOf.Wall,DefDatabase<ThingDef>.GetNamed("BlocksGranite"));GenSpawn.Spawn(cover,area+new IntVec3(3,0,1),map);int coverHp=cover.HitPoints;
   var cone=ForceDragonPowers.BreathCells(dragon,area+IntVec3.East*3);
   Check(cone.Count>0 && cone.All(c=>c.DistanceTo(area)<=4.01f) && cone.Contains(enemy.Position) && cone.Contains(cover.Position) && !cone.Contains(victim.Position),"wyrmling breath is a 4-cell forward cone");
   heart.Value=.2f;injuries=InjurySum(enemy);int missingBefore=enemy.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().Count();
   int broken=ForceDragonPowers.Breath(dragon,area+IntVec3.East*3);
   Check(broken==1 && shield.ShieldState!=ShieldState.Active,"breath shatters the target's shield belt");
   Check(Near(heart.Value,.45f),"shattering a shield restores 25 force: "+heart.Value*100);
   // 13点伤害可能直接打掉手指、脚趾这类小部位：那是缺失部位而不是伤口，也算命中。
   Check(InjurySum(enemy)>injuries || enemy.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().Count()>missingBefore,"breath injures pawns in the cone");
   Check(coverHp-cover.HitPoints==Mathf.RoundToInt(3*ForceDragon.BreathDamage(1)),"breath deals triple damage to cover: "+(coverHp-cover.HitPoints));
   Check(!enemy.IsBurning() && cone.All(c=>c.GetFirstThing<Fire>(map)==null),"force breath does not start fires");
   cover.Destroy();
   var breath=dragon.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_ForceBreath")).comps.OfType<CompForceDragonAbility>().Single();
   heart.Value=.1f;Check(breath.GizmoDisabled(out _),"breath gizmo is disabled without 25 force");
   heart.Value=heart.Max;Check(!breath.GizmoDisabled(out _),"breath gizmo is enabled with enough force");
   // 吞噬护盾
   var loose=ThingMaker.MakeThing(beltDef);GenSpawn.Spawn(loose,area+IntVec3.North*2,map);
   Check(Near(ForceDragonPowers.DevourValue(loose),40f) && Near(ForceDragonPowers.DevourValue(enemyBelt),0f),"only unequipped shield belts can be devoured");
   heart.Value=.1f;ForceDragonPowers.Devour(dragon,loose);
   Check(loose.Destroyed && Near(heart.Value,.5f),"devouring a shield belt restores 40 force");
   // 弹力法球（1.20.0：吐息偶尔会打中头部直接打死测试敌人，那时换一个新的，否则后面对尸体施法会空引用）
   if(enemy.Dead || !enemy.Spawned){enemy.Corpse?.Destroy();if(!enemy.Destroyed)enemy.Destroy();enemy=Person();enemy.apparel.DestroyAll();GenSpawn.Spawn(enemy,area+IntVec3.East*2,map);}
   ForceDragonPowers.Sphere(enemy,dragon);
   injuries=InjurySum(enemy);enemy.TakeDamage(new DamageInfo(DamageDefOf.Cut,20f,0f,-1f,dragon));
   Check(enemy.health.hediffSet.HasHediff(ForceDragon.Sphere) && enemy.stances.stunner.Stunned && Near(InjurySum(enemy),injuries),"resilient sphere holds its target and blocks all damage");
   enemy.health.RemoveHediff(enemy.health.hediffSet.GetFirstHediffOfDef(ForceDragon.Sphere));enemy.stances.stunner.StopStun();
   // 宝库
   var built=DragonHoard.BuildHoard(map,area+IntVec3.North*22,dragon,out var interior);
   var room=DragonHoard.RoomOf(dragon);
   Check(interior.Area==25 && room!=null && DragonHoard.Owners(room).Contains(dragon),"dragon's own bedroom becomes its hoard");
   Check(ThoughtWorker_DragonSharedRoom.Shares(dragon)==false,"dragon alone in its hoard is not sharing a room");
   var gold=DragonHoard.Stock(map,interior,ThingDefOf.Gold,100);built.AddRange(gold);
   var tunic=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FireGiantTunic"),ThingDefOf.Cloth);GenSpawn.Spawn(tunic,DragonHoard.FreeCells(interior,map).First(),map);built.Add(tunic);
   heart.RefreshHoard();
   float expected=gold.Sum(t=>t.MarketValue*t.stackCount)+2f*tunic.MarketValue;
   Check(heart.hasHoard && Math.Abs(heart.hoardValue-expected)<1f,"hoard value counts items and doubles fire giant work: "+heart.hoardValue+" vs "+expected);
   var hoardThought=DefDatabase<ThoughtDef>.GetNamed("LP_DragonHoard");
   // 1.23.3：宝库心情只有正面，财富和美观度各一条。
   int wealthStage=DragonHoard.MoodStage(true,heart.hoardValue);var wealthState=hoardThought.Worker.CurrentState(dragon);
   Check((wealthStage<0?!wealthState.Active:wealthState.Active && wealthState.StageIndex==wealthStage) && DragonHoard.MoodStage(false,0)==-1 && DragonHoard.MoodStage(true,500)==-1 && DragonHoard.MoodStage(true,1000)==0 && DragonHoard.MoodStage(true,60000)==2 && DragonHoard.MoodStage(true,200000)==3,"hoard wealth mood follows hoard value and is absent without a hoard: "+heart.hoardValue);
   var beautyThought=DefDatabase<ThoughtDef>.GetNamed("LP_DragonHoardBeauty");
   Check(hoardThought.stages.Count==4 && hoardThought.stages.All(s=>s.baseMoodEffect>0) && beautyThought.stages.Count==4 && beautyThought.stages.All(s=>s.baseMoodEffect>0),"hoard wealth and beauty moods have only positive stages");
   Check(Near(heart.hoardBeauty,room.GetStat(RoomStatDefOf.Beauty)) && DragonHoard.BeautyStage(true,0)==-1 && DragonHoard.BeautyStage(true,2.4f)==0 && DragonHoard.BeautyStage(true,15f)==2 && DragonHoard.BeautyStage(true,60f)==3 && DragonHoard.BeautyStage(false,60f)==-1,"hoard beauty mood follows the room's beauty: "+heart.hoardBeauty);
   int beautyStage=DragonHoard.BeautyStage(true,heart.hoardBeauty);var beautyState=beautyThought.Worker.CurrentState(dragon);
   Check(beautyStage<0?!beautyState.Active:beautyState.Active && beautyState.StageIndex==beautyStage,"hoard beauty thought matches its stage");
   var lodgerBed=(Building_Bed)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Bed"),ThingDefOf.WoodLog);lodgerBed.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(lodgerBed,interior.Cells.First(c=>c.x==interior.maxX && c.z==interior.minZ),map,Rot4.North,WipeMode.Vanish);built.Add(lodgerBed);
   victim.ownership.ClaimBedIfNonMedical(lodgerBed);
   Check(ThoughtWorker_DragonSharedRoom.Shares(dragon) && DefDatabase<ThoughtDef>.GetNamed("LP_DragonSharedRoom").Worker.CurrentState(dragon).Active,"lodging a non-dragon in the hoard upsets the dragon");
   victim.ownership.UnclaimBed();
   // 龙龄晋升
   float size=dragon.BodySize;
   heart.daysOnHoard=29f;heart.RefreshHoard();
   Check(!heart.TryAdvance(),"dragon does not advance before thirty days and 5000 hoard");
   heart.daysOnHoard=30f;Check(!heart.TryAdvance(),"days alone do not advance a poor dragon");
   built.AddRange(DragonHoard.Stock(map,interior,ThingDefOf.Gold,450));heart.RefreshHoard();
   Check(heart.hoardValue>=5000 && heart.TryAdvance() && ForceDragon.Age(dragon)==2,"thirty days plus 5000 hoard promote to young dragon");
   Check(Near(heart.Max,.7f) && dragon.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_ResilientSphere"))!=null && Near(dragon.BodySize,size*1.05f),"young dragon gains force capacity, resilient sphere and 5% size");
   LichMod.Settings.dragonAgePercent=50;Check(Near(ForceDragon.DaysNeeded(2),30f) && Near(ForceDragon.HoardNeeded(2),10000f),"age gate setting scales days and hoard");LichMod.Settings.dragonAgePercent=100;
   heart.SetAge(3);
   Check(dragon.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_WallOfForce"))!=null && Near(ScaleOffset(StatDefOf.ArmorRating_Sharp,dragon),.5f) && Near(ScaleOffset(StatDefOf.ArmorRating_Blunt,dragon),.3f) && Near(HungerTotal(dragon),.630f),"adult dragon has wall of force, 50% sharp and 30% blunt scales and eats 63.0%: "+HungerInfo(dragon));
   float clawAge3=ClawDamage(claws,victim);
   Check(Math.Abs(clawAge3/clawAge1-1.5f)<.01f,"claw damage grows 25% per age: "+clawAge1+" to "+clawAge3);
   // 力墙
   var wallAbility=dragon.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_WallOfForce")).comps.OfType<CompForceDragonAbility>().Single();
   heart.Value=heart.Max;float points=ForceDragon.Points(dragon);
   var wallCenter=area+IntVec3.South*4;
   wallAbility.Apply(new LocalTargetInfo(wallCenter),LocalTargetInfo.Invalid);
   var walls=ForceDragonPowers.WallCells(dragon,wallCenter).Select(c=>c.GetFirstThing<ForceWall>(map)).Where(w=>w!=null).ToList();
   Check(walls.Count==5 && walls.All(w=>w.Position.Impassable(map)) && Near(ForceDragon.Points(dragon),points-30f),"adult wall of force raises five impassable cells for 30 force");
   Check(walls.Select(w=>w.Position.x).Distinct().Count()==5,"wall of force runs perpendicular to the cast direction");
   int hpTicks=walls[0].ticksLeft;walls[0].TakeDamage(new DamageInfo(DamageDefOf.Bomb,500f));
   Check(!walls[0].Destroyed && hpTicks==ForceDragon.WallTicks,"wall of force cannot be destroyed and lasts 20 seconds");
   foreach(var w in walls){w.ticksLeft=1;w.DoTick();}
   Check(walls.All(w=>w.Destroyed),"wall of force dissolves when its time runs out");
   // 盗窃
   var hauler=Person();GenSpawn.Spawn(hauler,interior.CenterCell,map);
   var loot=gold.First(t=>!t.Destroyed);
   Check(hauler.carryTracker.TryStartCarry(loot,5,false)==5,"colonist picks gold up from the hoard");
   Check(dragon.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DragonHoard.Stolen)==null && !dragon.InMentalState,"colonist hauling from the hoard no longer lowers the dragon's mood");
   hauler.carryTracker.DestroyCarriedThing();
   var hostile=Find.FactionManager.AllFactions.FirstOrDefault(f=>!f.IsPlayer && !f.Hidden && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer));
   Check(hostile!=null,"a hostile humanlike faction exists for theft checks");
   var thief=Person();thief.SetFaction(hostile);GenSpawn.Spawn(thief,interior.CenterCell+IntVec3.East,map);
   dragon.skills.GetSkill(SkillDefOf.Melee).Level=12;thief.skills.GetSkill(SkillDefOf.Melee).Level=2;
   Check(ForceDragonPride.Applies(thief,dragon) && !ForceDragonPride.Applies(dragon,thief),"arrogant pressure lowers weaker enemies' melee accuracy against the dragon");
   loot=gold.First(t=>!t.Destroyed);
   Check(thief.carryTracker.TryStartCarry(loot,5,false)==5,"outsider grabs gold from the hoard");
   Check(dragon.MentalState is MentalState_MurderousRage rage && rage.target==thief && dragon.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DragonHoard.Stolen)==null,"outsider theft sends the dragon into a rage at the thief without a mood penalty");
   dragon.MentalState.RecoverFromState();
   thief.carryTracker.DestroyCarriedThing();
   LichMod.Settings.hoardTheftRage=false;loot=gold.First(t=>!t.Destroyed);thief.carryTracker.TryStartCarry(loot,1,false);
   Check(!dragon.InMentalState,"theft rage can be switched off in settings");
   LichMod.Settings.hoardTheftRage=true;thief.carryTracker.DestroyCarriedThing();thief.Destroy();hauler.Destroy();
   // 食性与傲慢
   var potatoes=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RawPotatoes"));potatoes.stackCount=20;GenSpawn.Spawn(potatoes,area+IntVec3.North*3,map);
   var meat=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Meat_Muffalo"));meat.stackCount=20;GenSpawn.Spawn(meat,area+IntVec3.North*4,map);
   // 用新生成的普通人对照：前面的吐息测试可能已经把 victim 打死（死者没有饮食需求）。
   var eater=Person();
   Check(!FoodUtility.WillEat(dragon,potatoes,dragon,true,false) && FoodUtility.WillEat(dragon,meat,dragon,true,false) && FoodUtility.WillEat(eater,potatoes,eater,true,false),"carnivore dragon refuses vegetables but eats meat");
   eater.Destroy();
   heart.Value=.1f;meat.Ingested(dragon,.05f);
   Check(Near(heart.Value,.11f) && dragon.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_DragonAteNonMeat"))==null,"eating meat restores one force without complaint");
   potatoes.Ingested(dragon,.05f);
   Check(dragon.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_DragonAteNonMeat"))!=null,"eating vegetables gives the forced-vegetarian mood");
   if(!potatoes.Destroyed)potatoes.Destroy();if(!meat.Destroyed)meat.Destroy();
   Check(dragon.WorkTagIsDisabled(WorkTags.Hauling) && dragon.WorkTagIsDisabled(WorkTags.Cleaning) && dragon.WorkTagIsDisabled(WorkTags.ManualDumb) && !dragon.WorkTypeIsDisabled(WorkTypeDefOf.Research),"arrogant dragon refuses hauling and cleaning but still researches");
   Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(InteractionWorker_Insult),nameof(InteractionWorker_Insult.RandomSelectionWeight)))?.Postfixes.Any(p=>p.owner=="local.lich.phylactery")==true,"arrogant dragons insult non-dragons more often");
   // 古龙、伟龙
   heart.SetAge(4);
   Check(dragon.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_ForceCage"))!=null && !dragon.health.hediffSet.HasHediff(ForceDragon.Invisibility),"ancient dragon learns force cage but is not yet invisible");
   var caged=Person();GenSpawn.Spawn(caged,area+IntVec3.South*7,map);
   var cage=ForceDragonPowers.Cage(caged);
   Check(cage.Count==8 && GenAdj.CellsAdjacent8Way(caged).All(c=>c.Impassable(map)) && cage.All(w=>w.ticksLeft==ForceDragon.CageTicks),"force cage surrounds its target for a day");
   foreach(var w in cage)w.Destroy();caged.Destroy();
   heart.SetAge(5);heart.Value=heart.Max;heart.SyncInvisibility();
   Check(dragon.health.hediffSet.HasHediff(ForceDragon.Invisibility) && dragon.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_TelekineticSphere"))!=null && Near(heart.Max,1f),"great wyrm is invisible, knows telekinetic sphere and holds 100 force");
   heart.Value=0;heart.SyncInvisibility();
   Check(!dragon.health.hediffSet.HasHediff(ForceDragon.Invisibility),"invisibility fails when force runs out");
   heart.Value=heart.Max;
   // 1.20.0：前面的测试偶尔会打死受害者，那时换一个新的，否则传送尸体会空引用。
   if(victim.Dead || !victim.Spawned){victim.Corpse?.Destroy();if(!victim.Destroyed)victim.Destroy();victim=Person();victim.apparel.DestroyAll();GenSpawn.Spawn(victim,area+IntVec3.West*8,map);}
   victim.Position=area+IntVec3.West*8;victim.Notify_Teleported(true,false);
   Check(ForceDragonPowers.Telekinetic(dragon,victim) && victim.Position.AdjacentTo8Way(dragon.Position) && victim.health.hediffSet.HasHediff(ForceDragon.Sphere),"telekinetic sphere drags its target beside the dragon");
   victim.health.RemoveHediff(victim.health.hediffSet.GetFirstHediffOfDef(ForceDragon.Sphere));victim.stances.stunner.StopStun();
   // 龙蛋
   heart.RefreshHoard();
   var egg=ThingMaker.MakeThing(DragonHoard.EggDef);GenSpawn.Spawn(egg,DragonHoard.FreeCells(interior,map).First(),map);
   var eggComp=egg.TryGetComp<CompDragonEgg>();
   Check(eggComp.Rate>1f,"egg warms in the hoard faster when the hoard is rich: "+eggComp.Rate);
   var coldEgg=ThingMaker.MakeThing(DragonHoard.EggDef);GenSpawn.Spawn(coldEgg,CellRect.CenteredOn(area,8).Cells.First(c=>!interior.ExpandedBy(2).Contains(c) && c.Standable(map) && c.GetFirstItem(map)==null && c.GetFirstPawn(map)==null),map);
   Check(coldEgg.TryGetComp<CompDragonEgg>().Rate==0 && coldEgg.TryGetComp<CompDragonEgg>().Advance(60000)==null,"egg outside a hoard does not develop");
   coldEgg.Destroy();
   eggComp.progressDays=eggComp.HatchDays-.001f;
   var baby=eggComp.Advance(250);
   Check(egg.Destroyed && baby!=null && baby.Spawned && baby.genes.Xenotype==xenotype && (baby.DevelopmentalStage&(DevelopmentalStage.Newborn|DevelopmentalStage.Baby))!=0 && baby.ageTracker.AgeBiologicalYears==0 && ForceDragon.Age(baby)==1 && baby.Faction==Faction.OfPlayer,"egg hatches into a wyrmling baby of the hoard's faction: destroyed="+egg.Destroyed+" baby="+baby+" spawned="+baby?.Spawned+" xeno="+baby?.genes?.Xenotype+" stage="+baby?.DevelopmentalStage+" age="+(baby==null?-1:ForceDragon.Age(baby))+" faction="+baby?.Faction);
   Check(!ForceDragonDiet.Refuses(baby,DefDatabase<ThingDef>.GetNamed("BabyFood"),null),"dragon newborns may still drink milk and eat baby food");
   Check(ForceDragon.Juvenile(baby) && Near(HungerTotal(baby),1f) && ForceDragon.MetabolismFactor(ForceDragon.Metabolism(baby))>1.5f,"wyrmling baby food consumption stays at 100%: "+HungerInfo(baby));
   baby.Destroy();
   var child=GenerateDragonChild(xenotype);GenSpawn.Spawn(child,area+IntVec3.South*2,map);
   Check(child.DevelopmentalStage==DevelopmentalStage.Child && ForceDragon.Age(child)==1 && Near(HungerTotal(child),1f),"wyrmling child food consumption stays at 100%: "+HungerInfo(child));
   ForceDragon.Heart(child).SetAge(3);
   Check(Near(HungerTotal(child),1f),"older dragon age does not raise a child's food consumption: "+HungerInfo(child));
   child.Destroy();
   var mate=GenerateDragon(xenotype);GenSpawn.Spawn(mate,interior.CenterCell,map);ForceDragon.Heart(mate).SetAge(3);
   var mateBed=(Building_Bed)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Bed"),ThingDefOf.WoodLog);mateBed.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(mateBed,interior.Cells.First(c=>c.x==interior.maxX && c.z==interior.minZ),map,Rot4.North,WipeMode.Vanish);built.Add(mateBed);
   mate.ownership.ClaimBedIfNonMedical(mateBed);
   Check(DragonHoard.Owners(room).Count==2 && !ThoughtWorker_DragonSharedRoom.Shares(dragon),"two dragons share a hoard without the shared-room mood");
   var layer=DragonHoard.Layer(dragon,mate)?dragon:mate;var layerHeart=ForceDragon.Heart(layer);
   int eggsBefore=map.listerThings.ThingsOfDef(DragonHoard.EggDef).Count;
   DragonHoard.TickNest(layerHeart,DragonHoard.RoomOf(layer),(int)(DragonHoard.NestDays*60000));
   int laid=map.listerThings.ThingsOfDef(DragonHoard.EggDef).Count-eggsBefore;
   Check(laid>=2 && laid<=5 && layerHeart.nextClutchTick>Find.TickManager.TicksGame && map.listerThings.ThingsOfDef(DragonHoard.EggDef).All(e=>e.GetRoom()==room),"nesting adult pair lays two to five eggs inside the hoard");
   foreach(var e in map.listerThings.ThingsOfDef(DragonHoard.EggDef).ToList())e.Destroy();
   // 清理测试用宝库
   mate.Destroy();
   foreach(var t in built)if(!t.Destroyed)t.Destroy();
   foreach(var c in interior.Cells)map.roofGrid.SetRoof(c,null);
   foreach(var t in new Thing[]{dragon,victim,enemy})if(!t.Destroyed)t.Destroy();
   // 存档往返用的世界角色
   var keeper=GenerateDragon(xenotype,false);var keeperHeart=ForceDragon.Heart(keeper);
   keeperHeart.SetAge(3);keeperHeart.Value=.55f;keeperHeart.daysOnHoard=12.5f;
   Find.WorldPawns.PassToWorld(keeper,RimWorld.Planet.PawnDiscardDecideMode.KeepForever);savedDragonId=keeper.ThingID;
  }
  void CheckForceDragonAfterLoad(){
   var keeper=Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p=>p.ThingID==savedDragonId);
   var heart=ForceDragon.Heart(keeper);
   Check(heart!=null && ForceDragon.Age(keeper)==3 && Near(heart.Value,.55f) && Near(heart.daysOnHoard,12.5f) && Near(heart.Max,.8f),"force dragon age, force and hoard days survive save/load");
   Check(keeper.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_WallOfForce"))!=null && keeper.health.hediffSet.HasHediff(ForceDragon.ClawWeapon),"force dragon abilities and claws survive save/load");
   Check(heart!=null && heart.stomachMigrated && keeper.genes.HasActiveGene(ForceDragon.StomachGene),"force dragon strong stomach and migration flag survive save/load");
  }
 }
}
