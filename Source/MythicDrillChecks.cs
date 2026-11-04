using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.101.0：Test.ps1 -Only MythicDrill。秘金深钻：研究位置、贴图、通电才能钻、工作给予、钻满一份出秘银/精金、矮人秘银翻倍、存读档保留进度。
 public partial class LichChecks {
  static string mdSaveId;
  static Building SpawnMythicDrill(Map map,IntVec3 at){var b=(Building)ThingMaker.MakeThing(WorkGiver_MythicDrill.Def);b.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(b,at,map,Rot4.North);return b;}
  void CheckMythicDrill(){
   var def=WorkGiver_MythicDrill.Def;
   Check(def!=null && !def.ConfigErrors().Any() && def.HasComp(typeof(CompMythicDrill)) && def.hasInteractionCell && def.designationCategory==DesignationCategoryDefOf.Production,"mythic drill is a buildable production building with its comp");
   Check(def.graphic!=null && def.graphic.MatSingle.mainTexture!=BaseContent.BadTex,"mythic drill has its own texture");
   var res=DefDatabase<ResearchProjectDef>.GetNamed("LP_Research_MythicDrilling");
   Check(def.researchPrerequisites.Contains(res) && res.prerequisites.Any(r=>r.defName=="LP_MythicMetallurgy") && res.prerequisites.Any(r=>r.defName=="DeepDrilling") && res.tab.defName=="LP_FireGiantTech","research 秘金深掘 needs 秘金冶炼 + deep drilling, on the DND tab");
   Check(!DefDatabase<ResearchProjectDef>.AllDefs.Any(r=>r!=res && r.tab==res.tab && Math.Abs(r.researchViewX-res.researchViewX)<.01f && Math.Abs(r.researchViewY-res.researchViewY)<.01f),"秘金深掘 has its own slot on the research tab");
   var wg=DefDatabase<WorkGiverDef>.GetNamed("LP_DrillMythic");
   Check(wg.workType==WorkTypeDefOf.Mining && wg.Worker is WorkGiver_MythicDrill,"drilling is mining work");

   var spot=ItemSpot(3);var drill=SpawnMythicDrill(map,spot);var comp=drill.GetComp<CompMythicDrill>();var power=drill.GetComp<CompPowerTrader>();
   var human=ItemPawn(spot+IntVec3.East*2);Hold(human);human.SetFaction(Faction.OfPlayer);
   var giver=(WorkGiver_MythicDrill)wg.Worker;
   power.PowerOn=false;
   Check(!comp.CanDrillNow() && !giver.HasJobOnThing(human,drill) && giver.ShouldSkip(human),"unpowered drill gives no work");
   power.PowerOn=true;
   Check(comp.CanDrillNow() && giver.HasJobOnThing(human,drill) && !giver.ShouldSkip(human),"powered drill gives drilling work");
   var job=giver.JobOnThing(human,drill);
   Check(job?.def.defName=="LP_OperateMythicDrill" && job.targetA.Thing==drill,"drilling job targets the drill");

   // 钻满一份：小人的实际钻探速度，进度归零并在旁边出货。
   int Metal(ThingDef d)=>map.listerThings.ThingsOfDef(d).Where(t=>t.Position.InHorDistOf(spot,4)).Sum(t=>t.stackCount);
   void Clear(){foreach(var d in new[]{ItemMaterials.Mithral,ItemMaterials.Adamantine})foreach(var t in map.listerThings.ThingsOfDef(d).Where(t=>t.Position.InHorDistOf(spot,4)).ToList())t.Destroy();}
   Clear();int ticks=0;
   while(Metal(ItemMaterials.Mithral)+Metal(ItemMaterials.Adamantine)==0 && ticks<200000){comp.DrillWorkDone(human,250);ticks+=250;}
   float speed=human.GetStatValue(StatDefOf.DeepDrillingSpeed);
   Check(ticks>0 && ticks<200000 && comp.portionProgress==0f && Math.Abs(ticks*speed-comp.Props.workPerPortion)<=250*speed+1,"one portion takes workPerPortion / drilling speed ("+ticks+" ticks at speed "+speed.ToString("F2")+")");
   Clear();

   // 分布：25% 精金。
   int mith=0,adam=0;Rand.PushState(20261103);
   try{for(int i=0;i<400;i++){var t=comp.Produce(1f,human,Rand.Value);if(t.def==ItemMaterials.Adamantine)adam++;else mith++;if(!t.Destroyed)t.Destroy();}}finally{Rand.PopState();}
   Clear();
   Check(adam>60 && adam<140 && mith+adam==400,"about a quarter of portions are adamantine ("+adam+"/400)");
   Check(comp.CountFor(ItemMaterials.Mithral,1f,human)==7 && comp.CountFor(ItemMaterials.Adamantine,1f,human)==3,"full portion: 7 mithral or 3 adamantine");
   var dwarf=RacePawn("LP_Dwarf");Place(dwarf,spot+IntVec3.West*2);Hold(dwarf);
   Check(comp.CountFor(ItemMaterials.Mithral,1f,dwarf)==14 && comp.CountFor(ItemMaterials.Adamantine,1f,dwarf)==3,"dwarves drill twice the mithral, same adamantine");
   dwarf.Destroy();
   Check(comp.Pick(.1f)==ItemMaterials.Adamantine && comp.Pick(.9f)==ItemMaterials.Mithral && !comp.CompInspectStringExtra().NullOrEmpty(),"pick by roll and inspect string");
   drill.Destroy();human.Destroy();
  }
  void PrepareMythicDrillSave(){
   var drill=SpawnMythicDrill(map,ItemSpot(3));drill.GetComp<CompMythicDrill>().portionProgress=1234f;mdSaveId=drill.ThingID;
  }
  void CheckMythicDrillAfterLoad(){
   if(mdSaveId==null)return;var drill=map.listerThings.ThingsOfDef(WorkGiver_MythicDrill.Def).FirstOrDefault(t=>t.ThingID==mdSaveId);
   Check(drill!=null && Math.Abs(drill.TryGetComp<CompMythicDrill>().portionProgress-1234f)<.01f,"mythic drill keeps its progress through save/load");
   drill?.Destroy();
  }
 }
}
