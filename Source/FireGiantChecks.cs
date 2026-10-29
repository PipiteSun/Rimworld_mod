using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  void CheckFireGiant() {
   var gene=DefDatabase<GeneDef>.GetNamed("LP_FireGiantCore");
   var bodyGene=DefDatabase<GeneDef>.GetNamed("Body_Hulk");
   var skinGene=DefDatabase<GeneDef>.GetNamed("Skin_DeepRed");
   var xenotype=DefDatabase<XenotypeDef>.GetNamed("LP_FireGiant");
   var forgeDef=DefDatabase<ThingDef>.GetNamed("LP_FireGiantForge");
   var tunicDef=DefDatabase<ThingDef>.GetNamed("LP_FireGiantTunic");
   var dusterDef=DefDatabase<ThingDef>.GetNamed("Apparel_Duster");
   var recipe=DefDatabase<RecipeDef>.GetNamed("LP_MakeFireGiantTunic");
   var vanillaRaceGenes=new[]{"AptitudeRemarkable_Crafting","MinTemp_SmallIncrease","MaxTemp_LargeIncrease"}.Select(n=>DefDatabase<GeneDef>.GetNamedSilentFail(n)).ToList();
   Check(!xenotype.ConfigErrors().Any() && xenotype.inheritable && xenotype.genes.Contains(gene) && xenotype.genes.Contains(bodyGene) && xenotype.genes.Contains(skinGene),"fire giant xenotype is an inheritable race with core, vanilla Hulk and deep red genes");
   Check(vanillaRaceGenes.All(g=>g!=null && xenotype.genes.Contains(g)),"fire giant xenotype carries vanilla great crafting, cold weakness and heat super-tolerance genes");
   var bornGiant=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:xenotype,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25,fixedChronologicalAge:25));
   Check(bornGiant.genes.Xenotype==xenotype && bornGiant.genes.HasActiveGene(gene) && FireGiant.Active(bornGiant),"fire giant xenotype generates a functioning giant");
   Check(bornGiant.genes.HasActiveGene(bodyGene) && bornGiant.genes.HasActiveGene(skinGene) && bornGiant.story.bodyType==BodyTypeDefOf.Hulk,"generated fire giant uses active vanilla body and skin genes");
   Check(bornGiant.genes.GenesListForReading.Count(g=>g.def==bodyGene)==1 && bornGiant.genes.GenesListForReading.Count(g=>g.def==skinGene)==1,"fire giant generation does not duplicate native appearance genes");
   Check(bornGiant.genes.GetGene(gene)!=null && bornGiant.genes.Endogenes.Any(g=>g.def==gene) && vanillaRaceGenes.All(g=>bornGiant.genes.Endogenes.Any(x=>x.def==g)),"generated fire giant carries race genes as inheritable endogenes");
   Check(bornGiant.skills.GetSkill(SkillDefOf.Crafting).passion!=Passion.None,"generated fire giant has a crafting passion");
   bornGiant.Destroy();
   Check(gene.biostatArc==0 && gene.displayCategory?.defName!="Archite" && gene.bodyType==null && !gene.skinColorOverride.HasValue && skinGene.skinColorOverride.HasValue,"fire giant core is an ordinary gene without bundled cosmetic fields");
   Check(gene.exclusionTags!=null && gene.exclusionTags.Contains("FireDamage") && gene.damageFactors.Any(d=>d.damageDef==DamageDefOf.Flame && d.factor==0f),"fire giant core is fully flame immune and excludes vanilla fire resistance");
   Check(recipe.AllRecipeUsers.Contains(forgeDef),"giant tunic recipe belongs to the forge");
   foreach(var path in new[]{"FireGiant/FireGiantGene","FireGiant/Forge","FireGiant/Tunic"})
    Check(ContentFinder<Texture2D>.Get(path,false)!=null,"fire giant texture loads: "+path);
   var giant=Person();
   GenPlace.TryPlaceThing(giant,altar.InteractionCell+IntVec3.East*4,map,ThingPlaceMode.Near);
   float oldSize=giant.BodySize;
   var arm=giant.RaceProps.body.AllParts.First(p=>p.def.defName=="Arm");
   float oldHealth=PartHealth.Maximum(arm,giant);
   var added=giant.genes.AddGene(gene,true);
   Check(FireGiant.Active(giant) && Near(giant.BodySize,oldSize*2f) && Near(FireGiant.Size(giant),2f),"gene enlarges adult body to exactly 2x");
   Check(giant.genes.HasActiveGene(bodyGene) && giant.genes.HasActiveGene(skinGene) && giant.story.bodyType==BodyTypeDefOf.Hulk,"adding core gene separately installs native fire giant appearance genes");
   giant.genes.RemoveGene(giant.genes.GetGene(bodyGene));
   giant.genes.RemoveGene(giant.genes.GetGene(skinGene));
   ((Gene_FireGiant)added).appearanceGenesInitialized=false;
   FireGiant.Migrate(giant);
   Check(giant.genes.HasActiveGene(bodyGene) && giant.genes.HasActiveGene(skinGene) && ((Gene_FireGiant)added).appearanceGenesInitialized,"legacy giant gets one-time cosmetic gene migration");
   // 1.23.0：生命×1.5 改走 HealthScale，只取整一次；其他模组让原版 HealthScale 不是整数时可能比 1.22.1 少 1 点。
   Check(PartHealth.Maximum(arm,giant)>=oldHealth*1.5f-1f && Math.Abs(PartHealth.Maximum(arm,giant)-LegacyMax(arm,giant))<=1f,"gene increases limb health: "+oldHealth+" to "+PartHealth.Maximum(arm,giant));
   Check(giant.GetStatValue(StatDefOf.Flammability)<=0.001f,"fire giant cannot be ignited: flammability "+giant.GetStatValue(StatDefOf.Flammability));
   FireUtility.TryAttachFire(giant,5f,null);
   Check(!giant.IsBurning(),"fire giant does not catch fire");
   giant.apparel.DestroyAll();
   var nakedDef=DefDatabase<ThoughtDef>.GetNamed("Naked");   Check(FireGiantNakedMood.IsNakedThought(nakedDef) && !nakedDef.Worker.CurrentState(giant).Active,"naked fire giant has no naked mood");
   Rand.PushState(1234);
   int improved=0;
   try{for(int i=0;i<1000;i++)if(FireGiant.ImproveQuality(giant,SkillDefOf.Crafting,QualityCategory.Normal)==QualityCategory.Good)improved++;}finally{Rand.PopState();}
   Check(improved>400 && improved<600,"fire giant crafting raises quality by one tier about half the time: "+improved+"/1000");
   Check(Enumerable.Range(0,200).All(_=>FireGiant.ImproveQuality(giant,SkillDefOf.Crafting,QualityCategory.Masterwork)==QualityCategory.Masterwork),"fire giant crafting bonus cannot create legendary items");
   Check(Enumerable.Range(0,200).All(_=>FireGiant.ImproveQuality(giant,SkillDefOf.Artistic,QualityCategory.Normal)==QualityCategory.Normal),"fire giant quality bonus only applies to crafting");
   // 1.30.0：巨人默认能穿普通衣服（设置“巨人穿普通衣服”），专属衣甲仍只有巨人能穿。
   Check(ApparelUtility.HasPartsToWear(giant,dusterDef) && ApparelUtility.HasPartsToWear(giant,tunicDef),"fire giant can wear ordinary and giant apparel");
   var tunic=(Apparel)ThingMaker.MakeThing(tunicDef,ThingDefOf.Cloth);
   giant.apparel.Wear(tunic);
   Check(giant.apparel.WornApparel.Contains(tunic),"fire giant wears forge tunic");
   var forge=(Building_WorkTable)ThingMaker.MakeThing(forgeDef);
   var forgeCell=map.AllCells.Where(c=>c.x>12 && c.z>12 && c.x<map.Size.x-12 && c.z<map.Size.z-12 && CellRect.CenteredOn(c,4).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth))).First();
   forge.SetFactionDirect(Faction.OfPlayer);
   GenSpawn.Spawn(forge,forgeCell,map);
   var bill=new Bill_Production(recipe){repeatMode=BillRepeatModeDefOf.RepeatCount,repeatCount=1};
   bill.nextTickToSearchForIngredients=-1;
   bill.SetPawnRestriction(giant);
   forge.BillStack.AddBill(bill);
   foreach(var ingredient in new[]{ThingDefOf.Cloth,ThingDefOf.Steel}) {
    var material=ThingMaker.MakeThing(ingredient);
    material.stackCount=ingredient==ThingDefOf.Cloth?100:60;
    GenPlace.TryPlaceThing(material,forge.InteractionCell,map,ThingPlaceMode.Near);
    material.SetForbidden(false,false);
   }
   giant.workSettings.EnableAndInitialize();
   giant.workSettings.SetPriority(WorkTypeDefOf.Smithing,1);
   giant.skills.GetSkill(SkillDefOf.Crafting).Level=12;
   var workgiver=new WorkGiver_DoBill{def=DefDatabase<WorkGiverDef>.GetNamed("LP_DoFireGiantForge")};
   var fuelComp=forge.GetComp<CompRefuelable>();
   Check(fuelComp!=null && fuelComp.Props.fuelFilter.Allows(ThingDefOf.WoodLog) && fuelComp.Props.fuelFilter.Allows(ThingDefOf.Chemfuel) && fuelComp.Props.consumeFuelOnlyWhenUsed,"giant forge burns wood or chemfuel only while used");
   FloatMenuMakerMap.makingFor=giant;
   try {Check(!fuelComp.HasFuel && workgiver.JobOnThing(giant,forge,true)==null,"unfueled giant forge offers no bill job");} finally {FloatMenuMakerMap.makingFor=null;}
   fuelComp.Refuel(fuelComp.Props.fuelCapacity);
   float fullFuel=fuelComp.Fuel;
   for(int i=0;i<600;i++)fuelComp.Notify_UsedThisTick();
   float ordinaryUse=fullFuel-fuelComp.Fuel;
   fuelComp.Refuel(fuelComp.Props.fuelCapacity);
   var oldGiantCell=giant.Position;
   giant.Position=forge.InteractionCell;giant.Notify_Teleported(true,false);
   var oldJob=giant.jobs.curJob;
   giant.jobs.curJob=JobMaker.MakeJob(JobDefOf.DoBill,forge);
   try {
    Check(FireGiant.ForgeUserIsGiant(forge),"forge detects a fire giant working at it");
    fullFuel=fuelComp.Fuel;
    for(int i=0;i<600;i++)fuelComp.Notify_UsedThisTick();
   } finally {giant.jobs.curJob=oldJob;}
   float giantUse=fullFuel-fuelComp.Fuel;
   Check(ordinaryUse>0f && Near(giantUse,ordinaryUse*FireGiant.ForgeFuelFactor),"fire giant halves forge fuel use: "+giantUse+" vs "+ordinaryUse);
   giant.Position=oldGiantCell;giant.Notify_Teleported(true,false);
   fuelComp.Refuel(fuelComp.Props.fuelCapacity);
   FloatMenuMakerMap.makingFor=giant;
   Verse.AI.Job forgeJob;
   try {forgeJob=workgiver.JobOnThing(giant,forge,true);} finally {FloatMenuMakerMap.makingFor=null;}
   Check(forgeJob!=null,"fire giant receives native forge bill job");
   CheckFireGiantEquipment(giant,forge,workgiver);
   CheckFireGiantWarcasket(giant);
   giant.genes.RemoveGene(added);
   Check(giant.equipment.Primary==null && !giant.apparel.WornApparel.Any(a=>GiantGear.Exclusive(a.def)),"removing fire giant gene releases all exclusive armor and weapon");
   Check(!FireGiant.Active(giant) && !giant.apparel.WornApparel.Contains(tunic),"removing gene removes oversized tunic");
   Check(ApparelUtility.HasPartsToWear(giant,dusterDef) && !ApparelUtility.HasPartsToWear(giant,tunicDef),"ordinary pawn has original apparel eligibility");
   Check(workgiver.JobOnThing(giant,forge,true)==null,"ordinary pawn cannot work giant forge");
   forge.Destroy();
   giant.Destroy();
  }
 }
}
