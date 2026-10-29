using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  static readonly Rot4[] visualFacings={Rot4.South,Rot4.East,Rot4.North,Rot4.West};
  static readonly string[] implantKeys={"BlindSight","WitheringHand","DeadPersonality","ShadowLeg"};
  static readonly List<string> visualFindings=new List<string>();
  static List<Pawn> compatibilitySamples;
  static List<string> compatibilityLabels;
  static CompatibilityGallery compatibilityGallery;
  static int compatibilityFrames;
  static List<string> compatibilityIds,compatibilityStates;
  static bool compatibilityReload;
  static string VisualState(Pawn p)=>p.story.bodyType.defName+"|"+p.story.headType.defName+"|"+string.Join(",",p.genes.GenesListForReading.Select(g=>g.def.defName).OrderBy(s=>s))+"|"+string.Join(",",p.apparel.WornApparel.Select(a=>a.def.defName).OrderBy(s=>s));
  static bool FromVre(Def d)=>d.modContentPack?.PackageIdPlayerFacing?.StartsWith("vanillaracesexpanded.",StringComparison.OrdinalIgnoreCase)==true;
  static bool Warcasket(ThingDef d)=>d.IsApparel && (d.GetType().FullName=="VFEPirates.WarcasketDef");
  static List<PawnGraphicDrawRequest> ActualRequests(Pawn p,Rot4 facing){
   RenderNodes(p);var parms=DrawParms(p,facing);parms.flags=PawnRenderFlags.Clothes|PawnRenderFlags.Headgear;var result=new List<PawnGraphicDrawRequest>();
   p.Drawer.renderer.renderTree.rootNode.AppendRequests(parms,result);return result;
  }
  Pawn VisualPerson(string label){
   var p=Person();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,15),map);p.apparel.DestroyAll();p.equipment.DestroyAllEquipment();
   p.story.hairDef=DefDatabase<HairDef>.GetNamed("Shaved");p.style.beardDef=BeardDefOf.NoBeard;
   foreach(var key in implantKeys){var r=Necro.Def<RecipeDef>("Install"+key);var part=r.Worker.GetPartsToApplyOn(p,r).FirstOrDefault();if(part!=null)p.health.AddHediff(Necro.Def<HediffDef>(key),part);}
   p.drafter.Drafted=true;p.Name=new NameSingle(label);return p;
  }
  static void Sample(Pawn p,string label){compatibilitySamples.Add(p);compatibilityLabels.Add(label);}
  static void AuditGraphics(Pawn p,string label){
   var nodes=RenderNodes(p);
   foreach(var r in visualFacings){
    var req=ActualRequests(p,r);
    Check(req.All(x=>x.material==null || x.material!=BaseContent.BadMat),label+" valid requested materials "+r);
    foreach(var gear in p.apparel.WornApparel){
     Check(nodes.Any(n=>n.apparel==gear && n.PrimaryGraphic!=null),label+" worn node "+gear.def.defName+" "+r);
     Check(req.Any(x=>x.node.apparel==gear && x.material!=null && x.mesh!=null),label+" actually submits worn graphic "+gear.def.defName+" "+r);
    }
    foreach(var n in nodes.Where(n=>n is ImplantMarkNode)){
     Vector3 pivot;var pos=n.Worker.OffsetFor(n,DrawParms(p,r),out pivot);var scale=n.Worker.ScaleFor(n,DrawParms(p,r));
     Check(!float.IsNaN(pos.x+pos.z+scale.x+scale.z) && Math.Abs(pos.x)<2 && Math.Abs(pos.z)<2,label+" bounded implant transform "+n.hediff.def.defName+" "+r);
    }
   }
  }
  static void AuditOcclusion(Pawn p,string label){
   foreach(var facing in visualFacings){
    var requests=ActualRequests(p,facing);
    foreach(var mark in RenderNodes(p).Where(n=>n is ImplantMarkNode && n.hediff.def.defName!="LP_ShadowLeg")){
     var part=mark.hediff.def.defName=="LP_DeadPersonality"?p.RaceProps.body.AllParts.First(b=>b.def==BodyPartDefOf.Head):mark.hediff.Part;
     if(ImplantMarkWorker.Covered(p,part))Check(!requests.Any(r=>r.node==mark && r.material!=null),label+" covered implant absent from draw requests "+mark.hediff.def.defName+" "+facing);
    }
   }
  }
  static void AuditEyeAnchor(Pawn p,string label){
   var eye=RenderNodes(p).First(n=>n.hediff?.def==Necro.Def<HediffDef>("BlindSight"));
   foreach(var r in visualFacings.Where(r=>r!=Rot4.North)){
    var parms=DrawParms(p,r);if(!eye.Worker.CanDrawNow(eye,parms))continue;
    var anchor=PawnDrawUtility.FindAnchors(p,eye.hediff.Part).FirstOrDefault(a=>PawnDrawUtility.AnchorUsable(p,a,r) && (!a.narrowCrown.HasValue || a.narrowCrown.Value==p.story.headType.narrow));
    if(anchor==null)continue;Vector3 expected,pivot;float radius;PawnDrawUtility.CalcAnchorData(p,anchor,r,out expected,out radius);
    expected+=DefDatabase<GeneDef>.GetNamed("Eyes_Red").renderNodeProperties[0].drawData.OffsetForRot(r);
    var actual=eye.Worker.OffsetFor(eye,parms,out pivot);
    Check((actual-expected).sqrMagnitude<.0001f,label+" implanted eye matches native eye-color position "+r+": actual="+actual+" native="+expected);
   }
  }
  void RunVisualCompatibility(){
   map=Find.CurrentMap;compatibilitySamples=new List<Pawn>();compatibilityLabels=new List<string>();visualFindings.Clear();
   File.WriteAllLines(Path.Combine(GenFilePaths.SaveDataFolderPath,"VisualMods.txt"),LoadedModManager.RunningModsListForReading.Select(m=>m.PackageIdPlayerFacing+" | "+m.Name));
   var p=VisualPerson("visual probe");
   foreach(var body in new[]{BodyTypeDefOf.Thin,BodyTypeDefOf.Male,BodyTypeDefOf.Female,BodyTypeDefOf.Fat,BodyTypeDefOf.Hulk}){
    p.story.bodyType=body;p.Drawer.renderer.SetAllGraphicsDirty();AuditEyeAnchor(p,body.defName);AuditGraphics(p,body.defName);
    foreach(var def in DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>Warcasket(d) && d.defName!="VFEP_Warcasket_Bodysuit" || d.IsApparel && d.modContentPack?.PackageIdPlayerFacing=="local.standalone.occupationalarmor")){
     var gear=WearVisual(p,def.defName);AuditGraphics(p,body.defName+"/"+def.defName);p.apparel.Remove(gear);gear.Destroy();
    }
   }
   p.Destroy();
   // Exercise real gene combinations, including the vanilla xenotypes extended
   // by VRE. The same Human pawn also carries our construct state and implants.
   int kindIndex=0;
   var xenos=DefDatabase<XenotypeDef>.AllDefsListForReading.Where(x=>FromVre(x) || x.genes?.Any(FromVre)==true).ToList();
   foreach(var xeno in xenos){
    var subject=VisualPerson(xeno.defName);var construct=(Hediff_Construct)subject.health.AddHediff(Necro.Def<HediffDef>("Construct"));construct.kind=new[]{"Skeleton","Golem","Scholar"}[kindIndex++%3];construct.UpgradeColonist();
    subject.genes.SetXenotype(xeno);Necro.Refresh(subject);var edited=VisualState(subject);construct.UpgradeAppearance();Check(VisualState(subject)==edited,xeno.defName+" edited xenotype appearance not reset by construct upgrade");
    AuditGraphics(subject,xeno.defName);AuditEyeAnchor(subject,xeno.defName);
    WearVisual(subject,"Apparel_ArmorRecon");WearVisual(subject,"Apparel_ArmorHelmetRecon");AuditGraphics(subject,xeno.defName+" Recon");AuditOcclusion(subject,xeno.defName+" Recon");
    foreach(var a in subject.apparel.WornApparel.ToList()){subject.apparel.Remove(a);a.Destroy();}
    Sample(subject,xeno.defName+" / "+construct.kind);
   }
   foreach(var group in DefDatabase<HeadTypeDef>.AllDefsListForReading.Where(FromVre).GroupBy(h=>h.modContentPack.PackageIdPlayerFacing)){
    Pawn sample=null;
    foreach(var head in group){
     var subject=VisualPerson(head.defName);subject.story.bodyType=BodyTypeDefOf.Thin;subject.story.headType=head;subject.Drawer.renderer.SetAllGraphicsDirty();AuditEyeAnchor(subject,head.defName);AuditGraphics(subject,head.defName);
     var armor=WearVisual(subject,"Apparel_ArmorRecon");var helmet=WearVisual(subject,"Apparel_ArmorHelmetRecon");AuditGraphics(subject,head.defName+" Recon");subject.apparel.Remove(armor);subject.apparel.Remove(helmet);armor.Destroy();helmet.Destroy();
     if(sample==null){sample=subject;Sample(subject,group.Key.Replace("vanillaracesexpanded.","VRE "));}else subject.Destroy();
    }
   }
   var sets=new[]{new[]{"VFEP_Warcasket_Warcasket","VFEP_WarcasketHelmet_Warcasket","VFEP_WarcasketShoulders_Warcasket"},new[]{"TXWC_Warcasket_Toxin","TXWC_WarcasketHelmet_Toxin","TXWC_WarcasketShoulders_Toxin"},new[]{"Warcasket_Mechlord","WarcasketHelmet_Mechlord","WarcasketShoulders_Mechlord"}};
   foreach(var set in sets){
    var subject=VisualPerson(set[0]);subject.story.bodyType=BodyTypeDefOf.Thin;
    foreach(var name in set){Check(DefDatabase<ThingDef>.GetNamedSilentFail(name)!=null,"active warcasket set contains "+name);WearVisual(subject,name);}
    var construct=(Hediff_Construct)subject.health.AddHediff(Necro.Def<HediffDef>("Construct"));construct.kind=new[]{"Skeleton","Golem","Scholar"}[Array.IndexOf(sets,set)];construct.UpgradeColonist();
    AuditGraphics(subject,set[0]+" full set");AuditOcclusion(subject,set[0]+" full set");
    var portrait=DrawParms(subject,Rot4.South);portrait.flags=PawnRenderFlags.Portrait;
    foreach(var mark in RenderNodes(subject).Where(n=>n is ImplantMarkNode && (n.hediff.def.defName=="LP_BlindSight" || n.hediff.def.defName=="LP_DeadPersonality")))
     Check(!mark.Worker.CanDrawNow(mark,portrait),set[0]+" persistent warcasket helmet hides face implant even with portrait hats disabled");
    Sample(subject,set[0]+" / "+construct.kind);
   }
   File.WriteAllLines(Path.Combine(GenFilePaths.SaveDataFolderPath,"VisualFindings.txt"),visualFindings);
   File.WriteAllLines(Path.Combine(GenFilePaths.SaveDataFolderPath,"VisualCoverage.txt"),new[]{"VRE xenotypes: "+xenos.Count,"VRE head types: "+DefDatabase<HeadTypeDef>.AllDefsListForReading.Count(FromVre),"Warcasket apparel definitions: "+DefDatabase<ThingDef>.AllDefsListForReading.Count(Warcasket)}.Concat(compatibilityLabels));
   compatibilityIds=compatibilitySamples.Select(s=>s.ThingID).ToList();compatibilityStates=compatibilitySamples.Select(VisualState).ToList();compatibilityReload=true;
   GameDataSaveLoader.SaveGame("VisualCompatibility-Roundtrip");LongEventHandler.QueueLongEvent(()=>GameDataSaveLoader.LoadGame("VisualCompatibility-Roundtrip"),"LoadingLongEvent",false,null);
  }
  void VisualCompatibilityAfterLoad(){
   map=Find.CurrentMap;compatibilitySamples=compatibilityIds.Select(id=>map.mapPawns.AllPawns.First(p=>p.ThingID==id)).ToList();
   for(int i=0;i<compatibilitySamples.Count;i++){var p=compatibilitySamples[i];Check(VisualState(p)==compatibilityStates[i],compatibilityLabels[i]+" saved genes, body, head and apparel preserved");AuditGraphics(p,compatibilityLabels[i]+" reloaded");AuditOcclusion(p,compatibilityLabels[i]+" reloaded");}
   foreach(var p in compatibilitySamples.Where(s=>s.apparel.WornApparel.Any(a=>Warcasket(a.def)))){
    var state=VisualState(p);p.Kill(null);Check(ResurrectionUtility.TryResurrect(p,new ResurrectionParams{restoreMissingParts=true,removeDiedThoughts=true}),p.Name+" warcasket construct resurrects");
    Check(VisualState(p)==state,p.Name+" warcasket and edited genes survive resurrection");AuditGraphics(p,p.Name+" resurrected");AuditOcclusion(p,p.Name+" resurrected");
   }
   File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"LichResult.txt"),"PASS: "+checks+" visual checks; native eye anchors, actual apparel draw requests, warcasket portrait occlusion and VRE/construct save-load.");
   compatibilityGallery=new CompatibilityGallery(compatibilitySamples,compatibilityLabels);Find.WindowStack.Add(compatibilityGallery);compatibilityFrames=1;
  }
  void VisualCompatibilityUpdate(){
   if(compatibilityFrames>0){
    compatibilityFrames++;if(compatibilityFrames%70==40)ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath,"VisualCompat-"+compatibilityGallery.page+".png"));
    if(compatibilityFrames%70==0){compatibilityGallery.page++;if(compatibilityGallery.page*5>=compatibilitySamples.Count){compatibilityFrames=0;Application.Quit();}}
    return;
   }
   if(ran || Current.ProgramState!=ProgramState.Playing || Find.CurrentMap==null || LongEventHandler.AnyEventNowOrWaiting)return;
   ran=true;Find.TickManager.Pause();try{if(compatibilityReload)VisualCompatibilityAfterLoad();else RunVisualCompatibility();}catch(Exception e){File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"LichResult.txt"),"FAIL after "+checks+" visual checks\n"+e);Log.Error("[LICH-VISUAL] "+e);Application.Quit();}
  }
 }
 public class CompatibilityGallery:Window {
  readonly List<Pawn> pawns;readonly List<string> labels;public int page;
  public CompatibilityGallery(List<Pawn> pawns,List<string> labels){this.pawns=pawns;this.labels=labels;forcePause=true;absorbInputAroundWindow=true;}
  public override Vector2 InitialSize=>new Vector2(1160,760);
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(12,0,1100,36),"VRE / Warcasket / Lich visual compatibility · "+(page+1));Text.Font=GameFont.Small;
   for(int row=0;row<5;row++){int idx=page*5+row;if(idx>=pawns.Count)break;var p=pawns[idx];Widgets.Label(new Rect(4,66+row*132,205,110),labels[idx]);
    for(int col=0;col<4;col++){var rect=new Rect(212+col*222,45+row*132,215,128);Widgets.DrawBoxSolid(rect,new Color(.13f,.16f,.18f));GUI.DrawTexture(rect,PortraitsCache.Get(p,new Vector2(215,128),new[]{Rot4.South,Rot4.East,Rot4.North,Rot4.West}[col],Vector3.zero,1.0f,healthStateOverride:PawnHealthState.Mobile));}
   }
  }
 }
}
