using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.24.0 截图：11 个种族的图鉴四向（不戴帽子，方便看象鼻、恶魔角、头箍、光环；鸟人穿风羽披风），以及图标对照。
 // 截图写在测试档目录：Races124-1.png ～ Races124-4.png、Races124Icons-1.png ～ Races124Icons-3.png、Races124Map-*.png（地图上朝南/东/北）。
 public partial class LichChecks {
  static readonly string[][] races124Pages={
   new[]{"LP_GenasiFire","LP_GenasiWater","LP_GenasiEarth"},
   new[]{"LP_GenasiAir","LP_Loxodon","LP_Ogre"},
   new[]{"LP_Aarakocra","LP_IceDevil","LP_Balor"},
   new[]{"LP_Titan","LP_Genie:Fire","LP_Genie:Water"}};
  static void ShowRaces124Gallery(int page){
   var map=Find.CurrentMap;var samples=new List<Pawn>();
   foreach(var entry in races124Pages[page]){
    var parts=entry.Split(':');
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,fixedBiologicalAge:30,fixedChronologicalAge:30,forcedXenotype:DefDatabase<XenotypeDef>.GetNamed(parts[0]),canGeneratePawnRelations:false));
    if(parts.Length>1)Genie.SetElement(p,parts[1]);
    if(p.apparel!=null)foreach(var a in p.apparel.WornApparel.Where(a=>a.def.apparel.layers.Any(l=>l==ApparelLayerDefOf.Overhead || l==ApparelLayerDefOf.EyeCover)).ToList())p.apparel.Remove(a);
    if(parts[0]=="LP_Aarakocra"){var cloak=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FeatherCloak"),ThingDefOf.Leather_Plain);foreach(var a in p.apparel.WornApparel.Where(a=>a.def.apparel.layers.Contains(ApparelLayerDefOf.Shell)).ToList())p.apparel.Remove(a);p.apparel.Wear(cloak,false);}
    GenPlace.TryPlaceThing(p,map.Center,map,ThingPlaceMode.Near);p.Drawer.renderer.SetAllGraphicsDirty();samples.Add(p);
   }
   Find.WindowStack.TryRemove(typeof(GeneIconGallery));Find.WindowStack.TryRemove(typeof(CommonRaceGallery));Find.WindowStack.TryRemove(typeof(TextureGallery));
   Find.WindowStack.Add(new CommonRaceGallery(samples,"1.24.0 种族图鉴（"+(page+1)+"/4）：不戴帽子；鸟人穿风羽披风；巨灵为火/水两种血脉"));
  }
  static void ShowRaces124Icons(int page){
   Find.WindowStack.TryRemove(typeof(CommonRaceGallery));Find.WindowStack.TryRemove(typeof(TextureGallery));
   var vanilla=new[]{"UI/Icons/Genes/Gene_BodyHulk","UI/Icons/Genes/Gene_Furskin","UI/Icons/Genes/Gene_TailFurry","UI/Icons/Genes/Gene_Sterile","UI/Icons/Genes/Gene_Ageless","Things/Item/Equipment/WeaponMelee/LongSword","Things/Pawn/Humanlike/HeadAttachments/MiniHorns/MiniHorns_south","Things/Pawn/Humanlike/BodyAttachments/FurryTail/FurryTail_south"};
   string[] custom;string title;
   if(page==0){custom=new[]{"GenasiFire","GenasiWater","GenasiEarth","GenasiAir","Loxodon","Ogre","Aarakocra","IceDevil","Balor","Titan","Genie"}.Select(n=>"Races124/Xenotypes/"+n).Concat(new[]{"Spark","Tide","WindLeap","Soar","Buffet","Wish","Smite","DivineBreath","Horn"}.Select(n=>"Races124/Abilities/"+n)).ToArray();title="1.24.0 异种型与能力图标（上：原版同规格；下：本模组新图）";}
   else if(page==1){custom=DefDatabase<GeneDef>.AllDefsListForReading.Where(g=>g.iconPath!=null && g.iconPath.StartsWith("Races124/Genes/")).Select(g=>g.iconPath).Distinct().Take(24).ToArray();title="1.24.0 基因图标（一）";}
   else{custom=DefDatabase<GeneDef>.AllDefsListForReading.Where(g=>g.iconPath!=null && g.iconPath.StartsWith("Races124/Genes/")).Select(g=>g.iconPath).Distinct().Skip(24).Concat(new[]{"FocusFire","FocusWater","FocusEarth","FocusAir","IvoryHorn","OgreClub","FeatherCloak","IceDevilStinger/IceDevilStinger_c","FrostSpear","BalorHeart","BalorWhip","EmpyreanBlade","WonderLamp_Asleep","WonderLamp_Bound","WonderLamp_Dim"}.Select(n=>"Races124/Items/"+n)).Concat(new[]{"Races124/Trunk/Trunk_south","Races124/Horns/IceHorns_south","Races124/Wings/BalorWings_south"}).Take(24).ToArray();title="1.24.0 基因图标（二）与物品";}
   Find.WindowStack.Add(new TextureGallery(title,vanilla,custom));
  }
  // 地图上的实际渲染（不受肖像框裁切）：11 个种族排成两行，依次朝南、朝东、朝北各截一张。
  static readonly List<Pawn> races124MapPawns=new List<Pawn>();
  static void ShowRaces124Map(Rot4 rot){
   Find.WindowStack.TryRemove(typeof(CommonRaceGallery));Find.WindowStack.TryRemove(typeof(TextureGallery));var map=Find.CurrentMap;
   var names=races124Pages.SelectMany(p=>p).ToList();
   // 其他截图把人放在地图中央，这里往下错开，免得挤在一起。
   var spot=map.Center+new IntVec3(0,0,-26);var center=map.AllCells.OrderBy(c=>c.DistanceTo(spot)).First(c=>Enumerable.Range(0,names.Count).All(i=>{var x=c+new IntVec3((i%6)*4-10,0,(i/6)*-5+2);return x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map)==null;}));
   if(races124MapPawns.Count==0){
    for(int i=0;i<names.Count;i++){
     var parts=names[i].Split(':');
     var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,fixedBiologicalAge:30,fixedChronologicalAge:30,forcedXenotype:DefDatabase<XenotypeDef>.GetNamed(parts[0]),canGeneratePawnRelations:false));
     if(parts.Length>1)Genie.SetElement(p,parts[1]);
     if(p.apparel!=null)foreach(var a in p.apparel.WornApparel.Where(a=>a.def.apparel.layers.Any(l=>l==ApparelLayerDefOf.Overhead || l==ApparelLayerDefOf.EyeCover)).ToList())p.apparel.Remove(a);
     if(parts[0]=="LP_Aarakocra"){foreach(var a in p.apparel.WornApparel.Where(a=>a.def.apparel.layers.Contains(ApparelLayerDefOf.Shell)).ToList())p.apparel.Remove(a);p.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FeatherCloak"),ThingDefOf.Leather_Plain),false);}
     GenSpawn.Spawn(p,center+new IntVec3((i%6)*4-10,0,(i/6)*-5+2),map);races124MapPawns.Add(p);
    }
   }
   foreach(var p in races124MapPawns){p.jobs.StopAll();if(p.drafter!=null)p.drafter.Drafted=true;p.Rotation=rot;p.Drawer.renderer.SetAllGraphicsDirty();}
   Find.Selector.ClearSelection();Find.CameraDriver.SetRootPosAndSize(center.ToVector3Shifted()+new Vector3(0,0,-1.5f),11);Messages.Clear();
  }  // 接在 CaptureDesigner 的截图流程后面：返回 true 表示还在截图。
  static bool CaptureRaces124(int frame){
   const int start=1140;if(frame<start)return false;
   int step=(frame-start)/90,at=(frame-start)%90;
   string dir=GenFilePaths.SaveDataFolderPath;
   if(step<4){if(at==0)ShowRaces124Gallery(step);if(at==60)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"Races124-"+(step+1)+".png"));return true;}
   if(step<7){if(at==0)ShowRaces124Icons(step-4);if(at==60)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"Races124Icons-"+(step-3)+".png"));return true;}
   if(step<10){var rot=step==7?Rot4.South:step==8?Rot4.East:Rot4.North;if(at==0)ShowRaces124Map(rot);if(at==60)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"Races124Map-"+(step==7?"south":step==8?"east":"north")+".png"));return true;}
   return false;
  }
 }
}
