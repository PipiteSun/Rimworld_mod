using System;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // 1.27.1 胃容量与龙之长眠检查（规格第8节）。
 public partial class LichChecks {
  static string satietyKeeperId;
  // 原版最大营养（MaxNutrition = 体型 × 生命阶段 foodMaxFactor），不含本规则。
  static float VanillaStomach(Pawn p)=>p.BodySize*p.ageTracker.CurLifeStage.foodMaxFactor;
  static float TraitHunger(Pawn p)=>p.story?.traits?.HungerRateFactor??1f;
  // 吃饱时实际饥饿速率（相对常人100%），除掉性格倍率。
  static float FedRate(Pawn p)=>p.needs.food.FoodFallPerTickAssumingCategory(HungerCategory.Fed)*60000f/Satiety.FoodPerDay/TraitHunger(p);
  static bool HungerMood(Pawn p)=>DefDatabase<ThoughtDef>.GetNamed("NeedFood").Worker.CurrentState(p).Active;
  static bool Malnourished(Pawn p)=>p.health.hediffSet.HasHediff(HediffDefOf.Malnutrition);
  string StomachInfo(Pawn p)=>p.LabelShort+" max="+p.needs.food.MaxLevel.ToString("0.###")+" vanilla="+VanillaStomach(p).ToString("0.###")+" F="+Satiety.Factor(p)+" pct="+p.needs.food.CurLevelPercentage.ToStringPercent();

  void CheckSatiety(){
   var registry=Necro.Registry;var xenotype=DefDatabase<XenotypeDef>.GetNamed("LP_ForceDragon");var giver=new JobGiver_GetFood();
   Check(StatDefOf.MaxNutrition.parts.Count(x=>x is StatPart_Stomach)==1 && registry.satietyVersion==1,"stomach stat part is registered once and the running game records satiety version 1");
   Check(Near(Satiety.DragonWantEat,.45f) && Near(Satiety.DragonHungry,.2f) && Near(Satiety.DragonUrgent,.08f),"dragon thresholds: want food below 45%, hungry below 20%, urgently hungry below 8% of a 500% stomach");

   // 1. 成年龙：上限=原版×5，龙之胃说明，饥饿速率 H(龙龄)。
   var dragon=GenerateDragon(xenotype);GenSpawn.Spawn(dragon,CellFinder.RandomClosewalkCellNear(map.Center,map,12),map);dragon.jobs.StopAll();
   var heart=ForceDragon.Heart(dragon);var food=dragon.needs.food;
   Check(Satiety.Dragon(dragon) && Near(food.MaxLevel,VanillaStomach(dragon)*5f) && Near(registry.RecordedStomach(dragon),5f),"adult force dragon stomach is 500% of vanilla: "+StomachInfo(dragon));
   Check(StatDefOf.MaxNutrition.Worker.GetExplanationFull(StatRequest.For(dragon),ToStringNumberSense.Absolute,food.MaxLevel).Contains("龙之胃：×500%"),"max nutrition explanation shows the dragon stomach");
   float[] rates={.573f,.602f,.630f,.659f,.688f};
   for(int a=1;a<=ForceDragon.MaxAge;a++){
    heart.SetAge(a);
    Check(Near(Satiety.DragonHunger(a),rates[a-1]) && Near(HungerTotal(dragon),rates[a-1]) && Near(FedRate(dragon),rates[a-1]) && Near(food.MaxLevel,VanillaStomach(dragon)*5f),ForceDragon.AgeLabel(a)+" hunger rate is "+rates[a-1].ToStringPercent()+": "+HungerInfo(dragon)+" fed="+FedRate(dragon));
   }
   // 2. 500%→225% 约3天（逐个原版需求间隔模拟）。
   foreach(var a in new[]{3,5}){
    heart.SetAge(a);heart.binging=false;food.CurLevelPercentage=1f;int intervals=0;
    while(food.CurLevelPercentage>=Satiety.DragonWantEat && intervals<10000){food.NeedInterval();intervals++;}
    float days=intervals*150f/60000f*TraitHunger(dragon);
    Check(Math.Abs(days-Satiety.LongSleepDays)<.05f,ForceDragon.AgeLabel(a)+" sleeps about 3 days from 500% to 225%: "+days.ToString("0.###")+" days");
    Check(food.CurCategory==HungerCategory.Fed && !HungerMood(dragon) && !Malnourished(dragon),"at 225% the dragon is still fed: no hunger mood, no malnutrition");
   }
   heart.SetAge(3);
   // 3. 阈值与暴食。
   heart.binging=false;food.CurLevelPercentage=.6f;
   Check(giver.GetPriority(dragon)==0f && food.CurCategory==HungerCategory.Fed && !heart.binging,"P=300%: the dragon does not want to eat");
   food.CurLevelPercentage=.4f;
   Check(giver.GetPriority(dragon)==SatietyGetFood.Priority && food.CurCategory==HungerCategory.Fed && heart.binging && !HungerMood(dragon),"P=200%: the dragon goes to eat but stays fed without a hunger mood");
   food.CurLevelPercentage=.6f;
   Check(giver.GetPriority(dragon)==SatietyGetFood.Priority && heart.binging,"binging keeps the dragon eating at P=300%");
   food.CurLevelPercentage=.96f;
   Check(giver.GetPriority(dragon)==0f && !heart.binging,"binging ends at 95% (P=480%)");
   food.CurLevelPercentage=.6f;
   Check(giver.GetPriority(dragon)==0f,"after the binge P=300% no longer wants food");
   food.CurLevelPercentage=.4f;dragon.drafter.Drafted=true;
   Check(giver.GetPriority(dragon)==0f,"a drafted dragon is not forced to eat");dragon.drafter.Drafted=false;
   food.CurLevelPercentage=.18f;
   Check(food.CurCategory==HungerCategory.Hungry && HungerMood(dragon) && Near(food.PercentageThreshHungry,.2f),"P=90%: the dragon is hungry");
   food.CurLevelPercentage=.06f;
   Check(food.CurCategory==HungerCategory.UrgentlyHungry && Near(food.PercentageThreshUrgentlyHungry,.08f),"P=30%: the dragon is urgently hungry");
   food.CurLevelPercentage=0f;
   Check(food.CurCategory==HungerCategory.Starving,"P=0%: the dragon starves as usual");
   // 4. 一次吃很多：饭菜一次最多3份。
   food.CurLevelPercentage=.4f;heart.binging=false;
   var human=SharedPawn();
   Check(FoodUtility.WillIngestStackCountOf(dragon,ThingDefOf.MealSimple,.9f)==Satiety.DragonMealsAtOnce && FoodUtility.WillIngestStackCountOf(human,ThingDefOf.MealSimple,.9f)==1,"a dragon takes up to three meals per trip, a human one");
   var meals=ThingMaker.MakeThing(ThingDefOf.MealSimple);meals.stackCount=5;float each=FoodUtility.NutritionForEater(dragon,meals);
   float eaten=meals.Ingested(dragon,food.NutritionWanted);
   Check(meals.stackCount==2 && Near(eaten,each*3f),"a dragon eats three meals in one sitting: "+eaten);if(!meals.Destroyed)meals.Destroy();
   food.CurLevelPercentage=.5f;
   Check(food.GetTipString().Contains("相当于常人饱食度250%") && food.GetTipString().Contains("上限500%") && !human.needs.food.GetTipString().Contains("相当于常人"),"food tooltip shows the common-person percentage for big stomachs only");
   Check(Near(Satiety.Factor(human),1f) && Near(human.needs.food.MaxLevel,VanillaStomach(human)),"baseliner stomach is unchanged");

   // 5. 幼年龙：上限、速率、阈值都不变。
   var child=GenerateDragonChild(xenotype);GenSpawn.Spawn(child,CellFinder.RandomClosewalkCellNear(map.Center,map,12),map);
   Check(!Satiety.Dragon(child) && Near(Satiety.Factor(child),1f) && Near(child.needs.food.MaxLevel,VanillaStomach(child)) && Near(HungerTotal(child),1f) && Near(child.needs.food.PercentageThreshHungry,child.RaceProps.FoodLevelPercentageWantEat*.8f),"juvenile dragon keeps the vanilla stomach, 100% hunger and vanilla thresholds: "+StomachInfo(child));
   child.Destroy();

   // 6. 大体型种族：上限 = 体型倍率×100%，阈值原版。
   foreach(var row in new[]{("LP_FireGiant",2f),("LP_Troll",1.5f),("LP_Minotaur",1.3f),("LP_Loxodon",1.3f),("LP_Ogre",1.6f),("LP_Titan",2.5f),("LP_Dwarf",1f)}){
    var p=RacePawn(row.Item1);
    Check(p.needs.food!=null && Near(Satiety.Factor(p),row.Item2) && Near(p.needs.food.MaxLevel,VanillaStomach(p)*row.Item2) && Near(p.needs.food.PercentageThreshHungry,p.RaceProps.FoodLevelPercentageWantEat*.8f),row.Item1+" stomach is x"+row.Item2.ToStringPercent()+" of vanilla with vanilla thresholds: "+StomachInfo(p));
    if(row.Item1=="LP_FireGiant")Check(StatDefOf.MaxNutrition.Worker.GetExplanationFull(StatRequest.For(p),ToStringNumberSense.Absolute,p.needs.food.MaxLevel).Contains("巨胃：×200%"),"max nutrition explanation shows the giant stomach");
    p.Destroy();
   }

   // 7. F 变化时保持百分比。
   var convert=SharedPawn();var cf=convert.needs.food;cf.CurLevelPercentage=.6f;float vanilla=VanillaStomach(convert);
   convert.genes.AddGene(ForceDragon.HeartGene,true);
   Check(Satiety.Dragon(convert) && Near(cf.MaxLevel,vanilla*5f) && Near(cf.CurLevelPercentage,.6f) && Near(registry.RecordedStomach(convert),5f),"gaining a dragon heart scales food so the percentage stays at 60%: "+StomachInfo(convert));
   convert.genes.RemoveGene(convert.genes.GetGene(ForceDragon.HeartGene));
   Check(!Satiety.Dragon(convert) && Near(cf.MaxLevel,vanilla) && Near(cf.CurLevelPercentage,.6f) && !registry.stomachFactors.ContainsKey(convert.thingIDNumber),"losing it scales back without starving: "+StomachInfo(convert));
   convert.Destroy();

   // 8. 旧存档迁移：饱食按原版上限记录，载入后按 F 放大一次。
   var troll=RacePawn("LP_Troll");var tf=troll.needs.food;var legacy=GenerateDragon(xenotype);GenSpawn.Spawn(legacy,CellFinder.RandomClosewalkCellNear(map.Center,map,12),map);var lf=legacy.needs.food;
   registry.satietyVersion=0;registry.stomachFactors.Remove(troll.thingIDNumber);registry.stomachFactors.Remove(legacy.thingIDNumber);
   tf.CurLevel=VanillaStomach(troll)*.8f;lf.CurLevel=VanillaStomach(legacy)*.9f;
   int scaled=registry.MigrateSatiety(new[]{troll,legacy,human});
   Check(scaled==2 && registry.satietyVersion==1 && Near(tf.CurLevelPercentage,.8f) && Near(lf.CurLevelPercentage,.9f) && Near(registry.RecordedStomach(troll),1.5f) && Near(registry.RecordedStomach(legacy),5f),"1.27.0 save migration scales food by the stomach factor: "+StomachInfo(troll)+"; "+StomachInfo(legacy));
   Check(registry.MigrateSatiety(new[]{troll,legacy,human})==0 && Near(tf.CurLevelPercentage,.8f) && Near(lf.CurLevelPercentage,.9f),"satiety migration runs only once");
   foreach(var p in new[]{troll,legacy,human,dragon})p.Destroy();

   // 9. 存读档用的世界角色。
   var keeper=GenerateDragon(xenotype,false);keeper.needs.food.CurLevelPercentage=.7f;ForceDragon.Heart(keeper).binging=true;
   Find.WorldPawns.PassToWorld(keeper,RimWorld.Planet.PawnDiscardDecideMode.KeepForever);satietyKeeperId=keeper.ThingID;
  }
  void CheckSatietyAfterLoad(){
   var registry=Necro.Registry;var keeper=Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p=>p.ThingID==satietyKeeperId);
   Check(keeper!=null && registry.satietyVersion==1 && Near(registry.RecordedStomach(keeper),5f) && Near(keeper.needs.food.MaxLevel,VanillaStomach(keeper)*5f) && Near(keeper.needs.food.CurLevelPercentage,.7f) && ForceDragon.Heart(keeper).binging,"stomach record, dragon food level and binging survive save/load"+(keeper==null?"":": "+StomachInfo(keeper)));
  }
 }
}
