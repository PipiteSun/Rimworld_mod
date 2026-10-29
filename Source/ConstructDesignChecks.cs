using System;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  static float designedHealthBeforeSave;static string designedPawnId,savedSeedId;static int uiFrames;static Dialog_ConstructDesign uiDesigner;
  void AwakenBillProduct(string id){
   var seed=map.listerThings.AllThings.OfType<ConstructSeed>().Single();
   Check(seed.master==altar.Box.soul && seed.Spawned,"native meat bill produces configurable bound embryo: "+id);
   Check(seed.GetGizmos().OfType<Command_Action>().Any(c=>c.defaultLabel=="配置死灵造物"),"embryo has its own configuration UI");
   Pawn result;string error;Check(seed.Awaken(seed.design,out result,out error),"baseline embryo awakens without extra resources: "+error);
   CheckConstructColonist(result);Check(seed.Destroyed && result!=null && Necro.Construct(result).design.Score==0,"awakening consumes embryo and records baseline design");
  }
  void CheckCapacityAtAwakening(){
   var seed=ConstructSeed.Make("Scholar",worker);GenPlace.TryPlaceThing(seed,altar.Position,map,ThingPlaceMode.Near);
   Pawn result;string error;Check(!seed.Awaken(seed.design,out result,out error) && error.Contains("统御") && !seed.Destroyed,"capacity shortage prevents awakening and preserves embryo");seed.Destroy();
  }
  void CheckConstructDesign(){
   var seed=ConstructSeed.Make("Scholar",worker);GenPlace.TryPlaceThing(seed,altar.Position,map,ThingPlaceMode.Near);
   var hidden=seed.Supplies();foreach(var t in hidden)t.SetForbidden(true,false);
   var plan=new ConstructDesign{meat=200,steel=100,plasteel=60,specialty="眼睛"};
   plan.passions[SkillDefOf.Crafting]=Passion.None;plan.passions[SkillDefOf.Plants]=Passion.Minor;plan.passions[SkillDefOf.Medicine]=Passion.Major;
   var copy=plan.Copy();copy.passions[SkillDefOf.Crafting]=Passion.Major;Check(plan.passions[SkillDefOf.Crafting]==Passion.None,"editing copied passion plan does not mutate saved draft");
   Pawn result;string error;
   Check(!seed.Awaken(plan,out result,out error) && !seed.Destroyed,"insufficient materials cannot consume embryo or create pawn");
   var meat=Spawn("Meat_Human",240);var steel=Spawn("Steel",130);var plasteel=Spawn("Plasteel",80);
   var newMeat=seed.Supplies().Where(ConstructSeed.RawMeat).ToList();foreach(var t in newMeat)t.SetForbidden(true,false);Check(!seed.Awaken(plan,out result,out error),"forbidden meat is excluded from awakening supplies");foreach(var t in newMeat)t.SetForbidden(false,false);
   var eligible=seed.Supplies();int meatBefore=seed.Supplies().Where(ConstructSeed.RawMeat).Sum(t=>t.stackCount),steelBefore=seed.Supplies().Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount),plastBefore=seed.Supplies().Where(t=>t.def==ThingDefOf.Plasteel).Sum(t=>t.stackCount);
   var invalid=plan.Copy();invalid.meat=-1;Check(!seed.Awaken(invalid,out result,out error),"negative resource configuration cannot grant a free upgraded pawn");
   var overCap=plan.Copy();foreach(var sk in new[]{SkillDefOf.Intellectual,SkillDefOf.Crafting,SkillDefOf.Construction,SkillDefOf.Medicine})overCap.passions[sk]=Passion.Major;Check(!seed.Awaken(overCap,out result,out error) && error==ConstructDesign.PassionLimitReason && !seed.Destroyed,"four major passions block awakening before consuming materials");
   var basePawn=Necro.LivingConstructs(worker).First(p=>Necro.Construct(p).kind=="Scholar");float baseHealth=basePawn.HealthScale,baseArmor=basePawn.GetStatValue(StatDefOf.ArmorRating_Sharp,true,-1),baseMove=basePawn.GetStatValue(StatDefOf.MoveSpeed,true,-1);var work=DefDatabase<StatDef>.GetNamed("WorkSpeedGlobal");float baseWork=basePawn.GetStatValue(work,true,-1);
   Check(seed.Awaken(plan,out result,out error),"custom design awakens with exact requested resources: "+error);
   // The seed is gone; compare map counts against the same radius and allowed status.
   var remaining=eligible.Where(t=>!t.Destroyed && t.Spawned).ToList();
   Check(remaining.Where(ConstructSeed.RawMeat).Sum(t=>t.stackCount)==meatBefore-200 && remaining.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount)==steelBefore-100 && remaining.Where(t=>t.def==ThingDefOf.Plasteel).Sum(t=>t.stackCount)==plastBefore-60,"awakening deducts selected meat, steel and plasteel exactly once");
   // Generated pawns may carry random scars; isolate material speed from injury/health-scale effects.
   foreach(var wound in result.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())result.health.RemoveHediff(wound);
   var generated=Necro.Construct(result);var recorded=generated.design;generated.design=null;result.health.Notify_HediffChanged(generated);
   baseHealth=result.HealthScale;baseArmor=result.GetStatValue(StatDefOf.ArmorRating_Sharp,true,-1);baseMove=result.GetStatValue(StatDefOf.MoveSpeed,true,-1);baseWork=result.GetStatValue(work,true,-1);
   generated.design=recorded;result.health.Notify_HediffChanged(generated);
   Check(Near(result.HealthScale,baseHealth*plan.Health),"meat changes actual pawn health scale");
   Check(Near(result.GetStatValue(StatDefOf.ArmorRating_Sharp,true,-1),baseArmor+plan.Armor),"metals change actual sharp armor");
   Check(Near(result.GetStatValue(StatDefOf.MoveSpeed,true,-1),baseMove*plan.Speed),"steel/plasteel balance changes actual movement; actual="+result.GetStatValue(StatDefOf.MoveSpeed,true,-1)+" baseline="+baseMove+" factor="+plan.Speed);
   float materialWork=result.GetStatValue(work,true,-1);
   Check(Near(generated.CurStage.statFactors.Single(m=>m.stat==work).value,Necro.Mentoring(worker)*plan.Work),"plasteel and mentoring contribute one combined work factor");
   // Other mods can add a fixed value AFTER native factors (e.g. Isekai level).
   // Compare the marginal effect on the SAME pawn; do not multiply that offset.
   int invested=generated.design.plasteel;generated.design.plasteel=invested*2;result.health.Notify_HediffChanged(generated);
   float doubleMaterialWork=result.GetStatValue(work,true,-1);generated.design.plasteel=invested;result.health.Notify_HediffChanged(generated);
   Check(materialWork>baseWork && Near(materialWork-baseWork,(doubleMaterialWork-baseWork)/2f),"plasteel improves actual work speed linearly while retaining external offsets");
   Check(result.skills.GetSkill(SkillDefOf.Crafting).Level==12 && Necro.Construct(result).specialty=="眼睛","selected specialty and initial skill investment are applied");
   Check(!seed.Awaken(plan,out var duplicate,out error) && duplicate==null,"spent embryo cannot duplicate the pawn");
   Check(Necro.Construct(result).GetGizmos().OfType<Command_Action>().Any(c=>c.defaultLabel=="造物档案"),"finished construct has its own design record UI");
   CheckColonistOperations(result);designedPawnId=result.ThingID;
   foreach(var t in hidden)if(!t.Destroyed)t.SetForbidden(false,false);
   var saved=ConstructSeed.Make("Golem",worker);saved.design=new ConstructDesign{meat=350,steel=80,plasteel=25};saved.design.passions[SkillDefOf.Cooking]=Passion.Major;GenPlace.TryPlaceThing(saved,altar.Position,map,ThingPlaceMode.Near);savedSeedId=saved.ThingID;
   var oldBill=Bill("Offer");oldBill.ingredientFilter.SetDisallowAll();oldBill.ingredientFilter.SetAllow(ThingCategoryDefOf.CorpsesHumanlike,true);oldBill.suspended=true;altar.fleshRecipeVersion=0;
  }
  void CaptureDesignHealth(){var p=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==designedPawnId);designedHealthBeforeSave=p.HealthScale;var provider=GenTypes.GetTypeInAnyAssembly("IsekaiLeveling.Patches.Patch_HealthBonus");if(provider!=null){var args=new object[]{p,1f};HarmonyLib.AccessTools.Method(provider,"Postfix").Invoke(null,args);Log.Message("[LICH-HEALTH] Before save actual="+p.HealthScale+" Isekai vitality/passive multiplier="+args[1]);}}
  void CheckDesignAfterLoad(){
   var pawn=Necro.Registry.constructs.Single(p=>p.ThingID==designedPawnId);var h=Necro.Construct(pawn);
   Check(h.design.meat==200 && h.design.steel==100 && h.design.plasteel==60 && h.specialty=="眼睛","finished construct quality and chosen specialty survive full save/load");
   Check(Near(pawn.HealthScale,designedHealthBeforeSave),"custom health including external modifiers survives save/load exactly once; before="+designedHealthBeforeSave+" after="+pawn.HealthScale);
   var recorded=h.design;float total=pawn.HealthScale;h.design=null;float baseline=pawn.HealthScale;h.design=recorded;Check(Near(total,baseline*recorded.Health),"loaded material health retains external multipliers exactly once");
   var saved=map.listerThings.AllThings.OfType<ConstructSeed>().Single(s=>s.ThingID==savedSeedId);
   Check(saved.design.meat==350 && saved.design.steel==80 && saved.design.plasteel==25 && saved.master==h.master,"unspent embryo draft and owner survive full save/load");
   CheckColonistAfterLoad(pawn,saved);Check(altar.fleshRecipeVersion==1 && altar.BillStack.Bills.Where(b=>b.recipe.defName=="LP_Offer").All(b=>b.ingredientFilter.Allows(ThingDefOf.Meat_Human)),"old corpse-only offering bills migrate to raw meat on load");
  }
  void ShowDesignerForScreenshot(){
   var pawn=Person();GenSpawn.Spawn(pawn,CellFinder.RandomClosewalkCellNear(altar.Position,map,5),map);var box=Rituals.Ascend(pawn);GenPlace.TryPlaceThing(box,altar.Position,map,ThingPlaceMode.Near);
   var seed=ConstructSeed.Make("Scholar",pawn);seed.design=new ConstructDesign{meat=200,steel=100,plasteel=60,specialty="眼睛"};GenPlace.TryPlaceThing(seed,altar.Position,map,ThingPlaceMode.Near);Spawn("Meat_Human",300);Spawn("Steel",200);Spawn("Plasteel",100);
   var pool=NewPool();PoolStock(pool,"Meat_Human",500);PoolStock(pool,"Steel",200);PoolStock(pool,"Plasteel",100);var subject=Constructs.Create("Scholar",pawn,pool.InteractionCell+IntVec3.East,map,"眼睛",seed.design.Copy());var h=Necro.Construct(subject);h.remoldDraft=Remolding.Current(h);h.remoldDraft.meat=300;h.remoldDraft.specialty="心脏";h.remoldDraft.passions[SkillDefOf.Medicine]=Passion.Major;renameScreenshotPawn=subject;uiDesigner=new Dialog_ConstructDesign(h,pool);Find.WindowStack.Add(uiDesigner);uiFrames=1;
  }
  static Pawn renameScreenshotPawn;
  static bool CaptureDesigner(){if(uiFrames==0)return false;uiFrames++;if(uiFrames==60)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"RemoldingDesigner.png"));if(uiFrames==80)uiDesigner.skillsTab=true;if(uiFrames==120)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"RemoldingPassions.png"));if(uiFrames==160){uiDesigner.Close();Necro.Construct(renameScreenshotPawn).RenameCommand().action();}if(uiFrames==210)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"ConstructRename.png"));if(uiFrames==240){Find.WindowStack.TryRemove(typeof(Dialog_NamePawn));ShowAppearanceGallery();}if(uiFrames==300)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"ImplantEquipmentGallery.png"));if(uiFrames==330)ShowAppearanceMap();if(uiFrames==390)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"ImplantEquipmentMap.png"));if(uiFrames==420)ShowFireGiantGearGallery();if(uiFrames==480)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"FireGiantEquipment.png"));if(uiFrames==510)ShowTrollGallery();if(uiFrames==570)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"TrollAppearanceAndGear.png"));if(uiFrames==600)ShowConstructRoleGallery();if(uiFrames==660)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"ConstructRoleAppearance.png"));if(uiFrames==690)ShowGeneIconGallery();if(uiFrames==750)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"GeneIconComparison.png"));if(uiFrames==780)ShowCommonRaceGallery();if(uiFrames==840)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"CommonRaces.png"));if(uiFrames==870)ShowMindFlayerGallery();if(uiFrames==930)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"MindFlayer.png"));if(uiFrames==960)ShowMinotaurGithGallery();if(uiFrames==1020)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"MinotaurGith.png"));if(uiFrames==1050)ShowMinotaurGithIcons();if(uiFrames==1110)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"MinotaurGithIcons.png"));if(CaptureRaces124(uiFrames))return true;if(uiFrames>=1140)Application.Quit();return true;}
 }
}
