using System;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // 1.27.1 胃容量检查（规格第8节；1.167.0 移除力能龙后去掉了龙之长眠部分）。
 public partial class LichChecks {
  static string satietyKeeperId;
  // 原版最大营养（MaxNutrition = 体型 × 生命阶段 foodMaxFactor），不含本规则。
  static float VanillaStomach(Pawn p)=>p.BodySize*p.ageTracker.CurLifeStage.foodMaxFactor;
  string StomachInfo(Pawn p)=>p.LabelShort+" max="+p.needs.food.MaxLevel.ToString("0.###")+" vanilla="+VanillaStomach(p).ToString("0.###")+" F="+Satiety.Factor(p)+" pct="+p.needs.food.CurLevelPercentage.ToStringPercent();

  void CheckSatiety(){
   var registry=Necro.Registry;var human=SharedPawn();
   Check(StatDefOf.MaxNutrition.parts.Count(x=>x is StatPart_Stomach)==1 && registry.satietyVersion==1,"stomach stat part is registered once and the running game records satiety version 1");
   Check(Near(Satiety.Factor(human),1f) && Near(human.needs.food.MaxLevel,VanillaStomach(human)),"baseliner stomach is unchanged");

   // 6. 大体型种族：上限 = 体型倍率×100%，阈值原版。
   foreach(var row in new[]{("LP_FireGiant",2f),("LP_Troll",1.5f),("LP_Minotaur",1.3f),("LP_Loxodon",1.3f),("LP_Ogre",1.6f),("LP_Titan",2.5f),("LP_Dwarf",1f)}){
    var p=RacePawn(row.Item1);
    Check(p.needs.food!=null && Near(Satiety.Factor(p),row.Item2) && Near(p.needs.food.MaxLevel,VanillaStomach(p)*row.Item2) && Near(p.needs.food.PercentageThreshHungry,p.RaceProps.FoodLevelPercentageWantEat*.8f),row.Item1+" stomach is x"+row.Item2.ToStringPercent()+" of vanilla with vanilla thresholds: "+StomachInfo(p));
    if(row.Item1=="LP_FireGiant")Check(StatDefOf.MaxNutrition.Worker.GetExplanationFull(StatRequest.For(p),ToStringNumberSense.Absolute,p.needs.food.MaxLevel).Contains("巨胃：×200%"),"max nutrition explanation shows the giant stomach");
    p.Destroy();
   }

   // 8. 旧存档迁移：饱食按原版上限记录，载入后按 F 放大一次。
   var troll=RacePawn("LP_Troll");var tf=troll.needs.food;var legacy=RacePawn("LP_FireGiant");var lf=legacy.needs.food;
   registry.satietyVersion=0;registry.stomachFactors.Remove(troll.thingIDNumber);registry.stomachFactors.Remove(legacy.thingIDNumber);
   tf.CurLevel=VanillaStomach(troll)*.8f;lf.CurLevel=VanillaStomach(legacy)*.9f;
   int scaled=registry.MigrateSatiety(new[]{troll,legacy,human});
   Check(scaled==2 && registry.satietyVersion==1 && Near(tf.CurLevelPercentage,.8f) && Near(lf.CurLevelPercentage,.9f) && Near(registry.RecordedStomach(troll),1.5f) && Near(registry.RecordedStomach(legacy),2f),"1.27.0 save migration scales troll and fire giant food once, keeping the percentage: "+StomachInfo(troll)+" / "+StomachInfo(legacy));
   Check(registry.MigrateSatiety(new[]{troll,legacy,human})==0 && Near(tf.CurLevelPercentage,.8f) && Near(lf.CurLevelPercentage,.9f),"satiety migration runs only once");
   foreach(var p in new[]{troll,legacy,human})p.Destroy();

   // 9. 存读档用的世界角色。
   var keeper=RacePawn("LP_Troll");if(keeper.Spawned)keeper.DeSpawn();keeper.needs.food.CurLevelPercentage=.7f;
   Find.WorldPawns.PassToWorld(keeper,RimWorld.Planet.PawnDiscardDecideMode.KeepForever);satietyKeeperId=keeper.ThingID;
  }
  void CheckSatietyAfterLoad(){
   var registry=Necro.Registry;var keeper=Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p=>p.ThingID==satietyKeeperId);
   Check(keeper!=null && registry.satietyVersion==1 && Near(registry.RecordedStomach(keeper),1.5f) && Near(keeper.needs.food.MaxLevel,VanillaStomach(keeper)*1.5f) && Near(keeper.needs.food.CurLevelPercentage,.7f),"troll stomach record and food level survive save/load"+(keeper==null?"":": "+StomachInfo(keeper)));
  }
 }
}
