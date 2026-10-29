using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.44.0 截图（Test.ps1 -Only GiantKinHAR，自测通过后）：上排双头巨人（裸身、牛仔帽、双盔、没了右头、没了左头、儿童），下排独眼巨人（裸身、牛仔帽、简易头盔+透镜、没了独眼、儿童）和一个人类对照；
 // 依次朝南、朝东、朝北、朝西各截一张：HARGiants-south.png / -east.png / -north.png / -west.png。另外导出原版头型与简易头盔的三向贴图作画图参考（HARGiantsRef-*.png）。
 public partial class LichChecks {
  static int harGiantFrames;
  static readonly List<Pawn> harGiantShots=new List<Pawn>();
  static IntVec3 harGiantCenter;
  static Pawn HARGallery(string kind,int age){
   var k=DefDatabase<PawnKindDef>.GetNamed(kind);bool child=age<13;
   return PawnGenerator.GeneratePawn(new PawnGenerationRequest(k,Faction.OfPlayer,fixedBiologicalAge:age,fixedChronologicalAge:age,canGeneratePawnRelations:false,developmentalStages:child?DevelopmentalStage.Child:DevelopmentalStage.Adult));
  }
  static void Dress(Pawn p,params string[] defs){
   p.apparel.DestroyAll();
   foreach(var a in defs){var d=DefDatabase<ThingDef>.GetNamedSilentFail(a);if(d!=null)p.apparel.Wear((Apparel)ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null),false);}
  }
  static void ShowHARGiantMap(Rot4 rot){
   var map=Find.CurrentMap;
   if(harGiantShots.Count==0){
    var spot=map.Center+new IntVec3(0,0,-20);
    harGiantCenter=map.AllCells.OrderBy(c=>c.DistanceTo(spot)).First(c=>Enumerable.Range(0,6).All(i=>Enumerable.Range(0,2).All(j=>{var x=c+new IntVec3(i*3-8,0,j*4-2);return x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map)==null;})));
    var row=new List<Pawn>();
    for(int i=0;i<6;i++){
     var p=HARGallery("LP_EttinColonist",i==5?8:30);
     if(i<5)Dress(p,i==1?new[]{"Apparel_Pants","Apparel_CollarShirt","Apparel_CowboyHat"}:i==2?new[]{"Apparel_Pants","Apparel_CollarShirt",Ettin.Helm}:new[]{"Apparel_Pants"});
     if(i==3)p.health.AddHediff(HediffDefOf.MissingBodyPart,p.RaceProps.body.AllParts.First(x=>x.def.defName==Ettin.SecondHead));
     if(i==4)p.health.AddHediff(HediffDefOf.MissingBodyPart,p.RaceProps.body.AllParts.First(x=>x.def==BodyPartDefOf.Head));
     GenSpawn.Spawn(p,harGiantCenter+new IntVec3(i*3-8,0,2),map);harGiantShots.Add(p);
    }
    for(int i=0;i<6;i++){
     Pawn p;
     if(i==5)p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,fixedBiologicalAge:30,fixedChronologicalAge:30,forcedXenotype:XenotypeDefOf.Baseliner,canGeneratePawnRelations:false));
     else{
      p=HARGallery("LP_CyclopsColonist",i==4?8:30);
      if(i<4)Dress(p,i==1?new[]{"Apparel_Pants","Apparel_CollarShirt","Apparel_CowboyHat"}:i==2?new[]{"Apparel_Pants","Apparel_CollarShirt","Apparel_SimpleHelmet",Cyclops.Lens}:new[]{"Apparel_Pants"});
      if(i==3)p.health.AddHediff(HediffDefOf.MissingBodyPart,p.RaceProps.body.AllParts.First(x=>x.def.defName==Cyclops.Eye));
     }
     GenSpawn.Spawn(p,harGiantCenter+new IntVec3(i*3-8,0,-2),map);harGiantShots.Add(p);
    }
   }
   foreach(var p in harGiantShots){if(p.Dead)continue;p.jobs.StopAll();if(p.drafter!=null)p.drafter.Drafted=true;p.Rotation=rot;p.Drawer.renderer.SetAllGraphicsDirty();}
   Find.Selector.ClearSelection();Find.CameraDriver.SetRootPosAndSize(harGiantCenter.ToVector3Shifted()+new Vector3(-1f,0,0),6);Messages.Clear();
  }
  static bool CaptureHARGiants(){
   if(harGiantFrames==0)return false;harGiantFrames++;
   string dir=GenFilePaths.SaveDataFolderPath;
   if(harGiantFrames==2){
    foreach(var n in new[]{"Heads/Male/Male_Average_Normal","Heads/Male/Male_Average_Wide","Heads/Female/Female_Average_Normal","Heads/Female/Female_Average_Wide","Apparel/SimpleHelmet/SimpleHelmet","Apparel/CowboyHat/CowboyHat"})
     foreach(var r in new[]{"south","east","north"}){var t=ContentFinder<Texture2D>.Get("Things/Pawn/Humanlike/"+n+"_"+r,false);if(t!=null)SaveTexture(t,Path.Combine(dir,"HARGiantsRef-"+n.Substring(n.LastIndexOf('/')+1)+"_"+r+".png"));}
   }
   string[] rots={"south","east","north","west"};
   for(int i=0;i<4;i++){
    int start=10+i*60;
    if(harGiantFrames==start)ShowHARGiantMap(i==0?Rot4.South:i==1?Rot4.East:i==2?Rot4.North:Rot4.West);
    if(harGiantFrames==start+45)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"HARGiants-"+rots[i]+".png"));
   }
   if(harGiantFrames>=260)Application.Quit();
   return true;
  }
 }
}
