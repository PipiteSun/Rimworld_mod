using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.178.0 死灵仪式台面板 + 造物材料全图吸收：Test.ps1 -Only AltarPanel（-lichuitest 时截 AltarPanel-*.png）。
 public partial class LichChecks {
  void CheckAltarPanel(){
   var box=altar.selected;
   // 1. 原版工作单页签去掉了，面板按钮在
   Check(altar.def.inspectorTabs==null || !altar.def.inspectorTabs.Contains(typeof(ITab_Bills)),"altar has no vanilla bills tab");
   Check(altar.GetGizmos().OfType<Command_Action>().Any(g=>g.defaultLabel=="仪式台面板"),"altar shows the 仪式台面板 gizmo");
   // 2. 分类
   RecipeDef R(string n)=>Necro.Def<RecipeDef>(n);
   var all=altar.def.AllRecipes.Where(r=>!AltarPanel.Hidden(r)).ToList();
   Check(AltarPanel.Category(R("Ascend"))=="仪式" && AltarPanel.Category(R("Offer"))=="献祭" && AltarPanel.Category(R("OfferSoul"))=="献祭" && AltarPanel.Category(R("MakeSkeleton"))=="造物" && AltarPanel.Category(R("Refill"))=="造物" && AltarPanel.Category(R("MakeScholarEye"))=="造物" && AltarPanel.Category(R("Craft_HellHeart"))=="植入物",
    "recipes sort into 仪式 / 献祭 / 造物 / 植入物");
   Check(!all.Any(r=>r.defName.StartsWith("LP_Harvest") || r.defName=="LP_Rebuild") && AltarPanel.Tabs.Sum(t=>AltarPanel.Recipes(altar,t).Count)==all.Count,
    "retired organ / rebuild bills are hidden; every other altar recipe ("+all.Count+") lands in exactly one tab: "+string.Join(" | ",AltarPanel.Tabs.Select(t=>t+": "+string.Join(",",AltarPanel.Recipes(altar,t).Select(r=>r.label)))));
   // 3. 排入：按次数的叠在同一张单上，持续另开一张；执行者限制的另开
   foreach(var b in altar.BillStack.Bills.ToList())altar.BillStack.Delete(b);
   var offer=R("Offer");
   Check(AltarPanel.Reason(offer,altar)==null && AltarPanel.Reason(R("MakeSkeleton"),altar)==null,"offer / skeleton are available with the fixture lich's phylactery selected");
   var b1=AltarPanel.Queue(altar,offer,false);var b2=AltarPanel.Queue(altar,offer,false);var bf=AltarPanel.Queue(altar,offer,true);var bf2=AltarPanel.Queue(altar,offer,true);
   Check(b1==b2 && b1.repeatCount==2 && b1.repeatMode==BillRepeatModeDefOf.RepeatCount && bf==bf2 && bf!=b1 && bf.repeatMode==BillRepeatModeDefOf.Forever && altar.BillStack.Count==2,"「排入 +1」 adds to one counted bill (×2), 「持续」 makes one forever bill");
   var other=Person();GenSpawn.Spawn(other,altar.InteractionCell+IntVec3.West*2,map);
   var asc=R("Ascend");var performers=AltarPanel.Performers(altar,asc);var ba=AltarPanel.Queue(altar,asc,false,other);
   Check(performers.Contains(other) && !performers.Contains(worker) && ba.PawnRestriction==other && altar.BillStack.Count==3,"lich ritual lists unconverted colonists only and queues a bill restricted to the chosen one");
   foreach(var b in altar.BillStack.Bills.ToList())altar.BillStack.Delete(b);other.Destroy();
   // 4. 没选命匣时造物给出原因
   altar.selected=null;string why=AltarPanel.Reason(R("MakeSkeleton"),altar);altar.selected=box;
   Check(why!=null && why.Contains("命匣") && AltarPanel.NearBoxes(altar).Contains(box),"without a selected phylactery the construct rows explain why ("+why+")");
   // 5. 唤醒材料全图吸收：钢铁放在离胚体很远的地方
   var seed=ConstructSeed.Make("Skeleton",worker);GenPlace.TryPlaceThing(seed,altar.InteractionCell,map,ThingPlaceMode.Near);
   foreach(var t in Remolding.Supplies(seed))t.SetForbidden(true,false);
   var far=map.AllCells.Where(c=>c.DistanceTo(seed.Position)>30 && c.Standable(map) && !c.Fogged(map) && c.GetFirstItem(map)==null && map.reachability.CanReach(seed.Position,c,PathEndMode.ClosestTouch,TraverseMode.PassDoors,Danger.Deadly)).OrderBy(c=>c.DistanceTo(seed.Position)).First();
   var steel=ThingMaker.MakeThing(ThingDefOf.Steel);steel.stackCount=40;GenSpawn.Spawn(steel,far,map);steel.SetForbidden(false,false);
   var supplies=Remolding.Supplies(seed);
   var plan=new ConstructDesign{steel=40};
   Check(supplies.Contains(steel) && seed.Blocked(plan,supplies)==null,"steel "+far.DistanceTo(seed.Position).ToString("0")+" cells away counts for awakening");
   Check(seed.Awaken(plan,out var made,out var err) && steel.Destroyed && Necro.Construct(made)?.design?.steel==40,"awakening consumes the far steel: "+err);
   var none=ConstructSeed.Make("Skeleton",worker);GenPlace.TryPlaceThing(none,altar.InteractionCell,map,ThingPlaceMode.Near);
   string miss=none.Blocked(new ConstructDesign{plasteel=150},Remolding.Supplies(none).Where(t=>t.def!=ThingDefOf.Plasteel).ToList());
   Check(miss=="地图上可用的玻璃钢不足。","missing material message speaks of the whole map ("+miss+")");
   none.Destroy();made.Destroy();
   // 6. 1.179.0 仪式台可以拆下重新安装：队列和选中的命匣保留
   AltarPanel.Queue(altar,offer,false);var pos=altar.Position;var rot=altar.Rotation;
   var mini=altar.MakeMinified();
   Check(altar.def.Minifiable && mini!=null && !altar.Spawned && mini.InnerThing==altar,"altar can be uninstalled into a minified thing");
   var inner=mini.InnerThing;mini.InnerThing=null;GenSpawn.Spawn(inner,pos,map,rot);
   Check(altar.Spawned && altar.selected==box && altar.Box==box && altar.BillStack.Bills.Any(b=>b.recipe==offer),"reinstalled altar keeps its queue and chosen phylactery");
   foreach(var b in altar.BillStack.Bills.ToList())altar.BillStack.Delete(b);
   // 7. 1.179.0 命匣初成只有巫妖一人
   var scen=DefDatabase<ScenarioDef>.GetNamed("LP_LichScenario").scenario;
   Check(scen.AllParts.OfType<ScenPart_ConfigPage_ConfigureStartingPawns>().Single().pawnCount==1,"lich scenario starts with the lich alone");
  }
  static int altarFrames;
  static bool CaptureAltarPanel(){
   if(altarFrames==0)return false;altarFrames++;string dir=GenFilePaths.SaveDataFolderPath;
   try{
    if(altarFrames==10){
     var a=Find.CurrentMap.listerThings.AllThings.OfType<Building_Altar>().FirstOrDefault(x=>x.Box!=null)??Find.CurrentMap.listerThings.AllThings.OfType<Building_Altar>().First();
     if(a.Box==null)a.selected=AltarPanel.NearBoxes(a).FirstOrDefault();
     foreach(var b in a.BillStack.Bills.ToList())a.BillStack.Delete(b);
     AltarPanel.Queue(a,Necro.Def<RecipeDef>("Offer"),false);AltarPanel.Queue(a,Necro.Def<RecipeDef>("Offer"),false);AltarPanel.Queue(a,Necro.Def<RecipeDef>("OfferSoul"),true);AltarPanel.Queue(a,Necro.Def<RecipeDef>("MakeGolem"),false).suspended=true;
     Messages.Clear();Find.Selector.ClearSelection();Find.Selector.Select(a);Find.WindowStack.Add(new Dialog_AltarPanel(a));
    }
    if(altarFrames==40)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"AltarPanel-1.png"));
    if(altarFrames==50){var w=Find.WindowStack.WindowOfType<Dialog_AltarPanel>();if(w!=null)w.tab="造物";}
    if(altarFrames==80)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"AltarPanel-2.png"));
    if(altarFrames==90){var w=Find.WindowStack.WindowOfType<Dialog_AltarPanel>();if(w!=null)w.tab="植入物";}
    if(altarFrames==120)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"AltarPanel-3.png"));
    if(altarFrames>=130){Log.Message("[LICH-UI] altar panel captured");Application.Quit();}
   }catch(Exception e){Log.Error("[LICH-UI] altar panel capture: "+e);Application.Quit();}
   return true;
  }
 }
}
