using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  void CheckFireGiantEquipment(Pawn giant,Building_WorkTable forge,WorkGiver_DoBill giver){
   foreach(var other in map.mapPawns.FreeColonistsSpawned.Where(x=>x!=giant).ToList()){other.jobs.StopAll();if(other.drafter!=null)other.drafter.Drafted=true;}
   var normal=Person();normal.equipment.DestroyAllEquipment();
   var moltenResearch=DefDatabase<ResearchProjectDef>.GetNamed("LP_MoltenForging");var moltenRecipes=new[]{MoltenGuard.Maul,MoltenGuard.Helmet,MoltenGuard.Armor}.Select(n=>DefDatabase<RecipeDef>.GetNamed("Make_"+n)).ToList();
   Check(moltenRecipes.All(r=>!r.AvailableNow),"molten recipes locked before research");Find.ResearchManager.FinishProject(moltenResearch,false,null,true);Check(moltenRecipes.All(r=>r.AvailableNow),"molten forging unlocks all three recipes");
   foreach(var bill in forge.BillStack.Bills)bill.suspended=true;
   foreach(var name in new[]{"LP_FireGiantMaul","LP_FireGiantHelmet","LP_FireGiantArmor",MoltenGuard.Maul,MoltenGuard.Helmet,MoltenGuard.Armor}){
    var def=DefDatabase<ThingDef>.GetNamed(name);
    var recipe=DefDatabase<RecipeDef>.GetNamed("Make_"+name);
    Check(!def.ConfigErrors().Any() && !recipe.ConfigErrors().Any(),"giant equipment and generated recipe valid: "+name);
    Check(recipe.AllRecipeUsers.Count()==1 && recipe.AllRecipeUsers.Contains(forge.def),"equipment can only be crafted at giant forge: "+name);
    Check(def.graphicData.Graphic.MatSingle!=BaseContent.BadMat,"giant equipment inventory graphic: "+name);
    foreach(var ingredient in def.costList){var t=ThingMaker.MakeThing(ingredient.thingDef);t.stackCount=ingredient.count;GenPlace.TryPlaceThing(t,forge.InteractionCell,map,ThingPlaceMode.Near,out Thing placed);if(placed!=null)placed.SetForbidden(false,false);}
    recipe.workAmount=50;var production=new Bill_ProductionWithUft(recipe){repeatMode=BillRepeatModeDefOf.RepeatCount,repeatCount=1};production.SetPawnRestriction(giant);production.SetStoreMode(BillStoreModeDefOf.DropOnFloor,null);forge.BillStack.AddBill(production);
    giant.jobs.StopAll();FloatMenuMakerMap.makingFor=giant;Job job;
    try{job=giver.JobOnThing(giant,forge,true);}finally{FloatMenuMakerMap.makingFor=null;}
    if(job==null)foreach(var material in map.listerThings.AllThings.Where(t=>def.costList.Any(c=>c.thingDef==t.def)))Log.Message("[MOLTEN-MATERIAL] "+material+" count="+material.stackCount+" required="+string.Join(",",recipe.ingredients.Select(c=>c.CountRequiredOfFor(material.def,recipe)))+" getter="+recipe.IngredientValueGetter.GetType()+" pos="+material.Position+" allowed="+production.IsFixedOrAllowedIngredient(material)+" forbidden="+material.IsForbidden(giant)+" reachable="+giant.CanReserveAndReach(material,PathEndMode.ClosestTouch,Danger.Deadly));
    Check(job!=null,"giant receives crafting job for "+name+": "+JobFailReason.Reason);
    giant.jobs.StartJob(job,JobCondition.InterruptForced);for(int tick=0;tick<15000 && production.repeatCount>0;tick++)Step(1);
    Check(production.repeatCount==0 && map.listerThings.ThingsOfDef(def).Count>0,"native ingredient hauling and crafting produces "+name+" remaining="+production.repeatCount+" job="+giant.CurJob+" reason="+JobFailReason.Reason+" products="+map.listerThings.ThingsOfDef(def).Count);
    production.suspended=true;giant.jobs.StopAll();
    var item=(ThingWithComps)map.listerThings.ThingsOfDef(def).First();
    if(def.IsWeapon){
     Check(!EquipmentUtility.CanEquip(item,normal,out string reason) && reason.Contains("火巨人"),"ordinary pawn gets clear weapon restriction");
     item.DeSpawn();normal.equipment.AddEquipment(item);Check(normal.equipment.Primary==null && item.ParentHolder==null,"direct equip cannot bypass giant weapon restriction");
     giant.equipment.DestroyAllEquipment();Check(EquipmentUtility.CanEquip(item,giant,out _),"giant can equip forge maul");giant.equipment.AddEquipment(item);Check(giant.equipment.Primary==item,"giant equips forged maul");
    }else{
     Check(!ApparelUtility.HasPartsToWear(normal,def) && ApparelUtility.HasPartsToWear(giant,def),"exclusive armor eligibility: "+name);
     normal.apparel.Wear((Apparel)item);Check(!normal.apparel.WornApparel.Contains((Apparel)item),"direct wear cannot bypass armor restriction: "+name);
     giant.apparel.Wear((Apparel)item);Check(giant.apparel.WornApparel.Contains((Apparel)item),"giant wears crafted armor: "+name);
    }
   }
   var nodes=RenderNodes(giant);
   foreach(var gear in giant.apparel.WornApparel){
    Check(nodes.Any(n=>n.apparel==gear && n.PrimaryGraphic!=null),"actual worn node generated: "+gear.def.defName);
    foreach(var node in nodes.Where(n=>n.apparel==gear && n.PrimaryGraphic!=null))foreach(var facing in new[]{Rot4.North,Rot4.East,Rot4.South,Rot4.West})Check(node.PrimaryGraphic.MatAt(facing)!=BaseContent.BadMat,"directional equipment material: "+gear.def.defName+" "+facing);
   }
   CheckMoltenGuard(giant);normal.Destroy();
  }
  static void ShowFireGiantGearGallery(){
   var pawns=new List<Pawn>();var map=Find.CurrentMap;
   for(int i=0;i<2;i++){
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,fixedBiologicalAge:25,fixedChronologicalAge:25));GenPlace.TryPlaceThing(p,map.Center,map,ThingPlaceMode.Near);p.apparel.DestroyAll();p.equipment.DestroyAllEquipment();p.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_FireGiantCore"),true);
    if(i==0){WearVisual(p,"LP_FireGiantArmor");WearVisual(p,"LP_FireGiantHelmet");}else {WearVisual(p,MoltenGuard.Armor);WearVisual(p,MoltenGuard.Helmet);p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(MoltenGuard.Maul)));}
    p.drafter.Drafted=true;p.Drawer.renderer.SetAllGraphicsDirty();pawns.Add(p);
   }
   Messages.Clear();Find.WindowStack.Add(new FireGiantGearGallery(pawns));
  }
 }
 public class FireGiantGearGallery : Window {
  readonly List<Pawn> pawns;public FireGiantGearGallery(List<Pawn> pawns){this.pawns=pawns;forcePause=true;absorbInputAroundWindow=true;}
  public override Vector2 InitialSize=>new Vector2(1100,720);
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(10,0,1060,40),"火巨人 · 上排熔铁套装 / 下排熔核禁卫");Text.Font=GameFont.Small;
   for(int row=0;row<2;row++)for(int col=0;col<4;col++){
    var box=new Rect(10+col*260,60+row*245,250,230);Widgets.DrawBoxSolid(box,new Color(.18f,.2f,.21f));
    GUI.DrawTexture(box,PortraitsCache.Get(pawns[row],new Vector2(250,230),new[]{Rot4.South,Rot4.East,Rot4.North,Rot4.West}[col],Vector3.zero,.75f));
   }
   foreach(var entry in new[]{MoltenGuard.Maul,MoltenGuard.Helmet,MoltenGuard.Armor}.Select((name,i)=>new{name,i})){
    var def=DefDatabase<ThingDef>.GetNamed(entry.name);Widgets.ThingIcon(new Rect(20+entry.i*350,555,90,90),def);Widgets.Label(new Rect(120+entry.i*350,580,220,50),def.label);
   }
  }
 }
}

