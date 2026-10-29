using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.33.0 截图（Test.ps1 -Only Marilith，自测通过后）：地图上 5 个蛇人（裸身、防弹衣+防弹头盔、风衣+牛仔帽、板甲+简易头盔、断了左下臂）和一个人类对照，
 // 依次朝南、朝东、朝北各截一张：Marilith-south.png / -east.png / -north.png。另外导出原版 Naked_Hulk / Naked_Child 三向贴图作画图参考（MarilithRef-*.png）。
 public partial class LichChecks {
  static int marilithFrames;
  static readonly List<Pawn> marilithShots=new List<Pawn>();
  static IntVec3 marilithShotCenter;
  static void SaveTexture(Texture2D tex,string path){
   var rt=RenderTexture.GetTemporary(tex.width,tex.height,0,RenderTextureFormat.ARGB32);Graphics.Blit(tex,rt);var prev=RenderTexture.active;RenderTexture.active=rt;
   var copy=new Texture2D(tex.width,tex.height,TextureFormat.ARGB32,false);copy.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0);copy.Apply();RenderTexture.active=prev;RenderTexture.ReleaseTemporary(rt);
   File.WriteAllBytes(path,copy.EncodeToPNG());
  }
  static void ShowMarilithMap(Rot4 rot){
   var map=Find.CurrentMap;
   if(marilithShots.Count==0){
    var spot=map.Center+new IntVec3(0,0,-20);
    marilithShotCenter=map.AllCells.OrderBy(c=>c.DistanceTo(spot)).First(c=>Enumerable.Range(0,7).All(i=>{var x=c+new IntVec3(i*3-9,0,0);return x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map)==null;}));
    var outfits=new[]{new string[0],new[]{"Apparel_FlakVest","Apparel_AdvancedHelmet"},new[]{"Apparel_Duster","Apparel_CowboyHat"},new[]{"Apparel_PlateArmor","Apparel_SimpleHelmet"},new string[0]};
    for(int i=0;i<7;i++){
     Pawn p;
     if(i==6)p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_MarilithColonist"),Faction.OfPlayer,fixedBiologicalAge:8,fixedChronologicalAge:8,canGeneratePawnRelations:false,developmentalStages:DevelopmentalStage.Child));
     else if(i==5)p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,fixedBiologicalAge:30,fixedChronologicalAge:30,forcedXenotype:XenotypeDefOf.Baseliner,canGeneratePawnRelations:false));
     else{
      p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_MarilithColonist"),Faction.OfPlayer,fixedBiologicalAge:30,fixedChronologicalAge:30,canGeneratePawnRelations:false));
      p.apparel.DestroyAll();
      foreach(var a in outfits[i]){var d=DefDatabase<ThingDef>.GetNamedSilentFail(a);if(d!=null)p.apparel.Wear((Apparel)ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null),false);}
      if(i==4)p.health.AddHediff(HediffDefOf.MissingBodyPart,p.RaceProps.body.AllParts.First(x=>x.untranslatedCustomLabel=="左下臂"));
     }
     GenSpawn.Spawn(p,marilithShotCenter+new IntVec3(i*3-9,0,0),map);marilithShots.Add(p);
    }
   }
   foreach(var p in marilithShots){p.jobs.StopAll();if(p.drafter!=null)p.drafter.Drafted=true;p.Rotation=rot;p.Drawer.renderer.SetAllGraphicsDirty();}
   Find.Selector.ClearSelection();Find.CameraDriver.SetRootPosAndSize(marilithShotCenter.ToVector3Shifted()+new Vector3(-0.5f,0,0),5);Messages.Clear();
  }
  static bool CaptureMarilith(){
   if(marilithFrames==0)return false;marilithFrames++;
   string dir=GenFilePaths.SaveDataFolderPath;
   if(marilithFrames==2){
    foreach(var n in new[]{"Naked_Hulk","Naked_Child"})foreach(var r in new[]{"south","east","north"}){var t=ContentFinder<Texture2D>.Get("Things/Pawn/Humanlike/Bodies/"+n+"_"+r,false);if(t!=null)SaveTexture(t,Path.Combine(dir,"MarilithRef-"+n+"_"+r+".png"));}
   }
   string[] rots={"south","east","north"};
   for(int i=0;i<3;i++){
    int start=10+i*60;
    if(marilithFrames==start)ShowMarilithMap(i==0?Rot4.South:i==1?Rot4.East:Rot4.North);
    if(marilithFrames==start+45)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"Marilith-"+rots[i]+".png"));
   }
   if(marilithFrames>=200)Application.Quit();
   return true;
  }
 }
}
