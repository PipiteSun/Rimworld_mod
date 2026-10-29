using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 public partial class LichChecks {
  static void ShowCommonRaceGallery()=>ShowRaceGallery(new[]{"LP_HalfOrc","LP_Elf","LP_Dwarf"},"常见种族 · 半兽人 / 精灵 / 矮人（原生外貌，随机服装）");
  static void ShowMindFlayerGallery()=>ShowRaceGallery(new[]{"LP_MindFlayer","LP_MindFlayer"},"夺心魔（原版无发+紫色皮肤+面部触须，随机服装）");
  static void ShowMinotaurGithGallery()=>ShowRaceGallery(new[]{"LP_Minotaur","LP_Githyanki"},"牛头人（壮硕+棕色毛皮+自绘牛角/牛鼻）/ 吉斯洋基（纤瘦+尖耳+灰黄绿皮肤），随机服装、不戴帽子",true);
  static void ShowRaceGallery(string[] names,string title,bool bareHead=false){
   var samples=new List<Pawn>();var map=Find.CurrentMap;
   foreach(var name in names){
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,fixedBiologicalAge:25,fixedChronologicalAge:25,forcedXenotype:DefDatabase<XenotypeDef>.GetNamed(name),canGeneratePawnRelations:false));
    // 头饰会盖住牛角、牛鼻和尖耳：对位检查时去掉帽子。
    if(bareHead && p.apparel!=null)foreach(var a in p.apparel.WornApparel.Where(a=>a.def.apparel.layers.Any(l=>l==ApparelLayerDefOf.Overhead || l==ApparelLayerDefOf.EyeCover)).ToList())p.apparel.Remove(a);
    GenPlace.TryPlaceThing(p,map.Center,map,ThingPlaceMode.Near);p.Drawer.renderer.SetAllGraphicsDirty();samples.Add(p);
   }
   Find.WindowStack.TryRemove(typeof(GeneIconGallery));Find.WindowStack.TryRemove(typeof(CommonRaceGallery));
   Find.WindowStack.Add(new CommonRaceGallery(samples,title));
  }
 }
 public class CommonRaceGallery : Window {
  readonly List<Pawn> samples;readonly string title;
  public CommonRaceGallery(List<Pawn> samples,string title){this.samples=samples;this.title=title;forcePause=true;absorbInputAroundWindow=true;}
  public override Vector2 InitialSize=>new Vector2(1100,760);
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(10,0,1060,36),title);Text.Font=GameFont.Small;
   var facings=new[]{Rot4.South,Rot4.East,Rot4.North,Rot4.West};
   for(int row=0;row<samples.Count;row++){
    Widgets.Label(new Rect(10,44+row*230,200,22),samples[row].genes.XenotypeLabelCap+"（体型×"+samples[row].BodySize.ToString("0.##")+"）");
    for(int col=0;col<4;col++){
     var box=new Rect(10+col*262,66+row*230,252,200);Widgets.DrawBoxSolid(box,new Color(.18f,.2f,.21f));
     GUI.DrawTexture(box,PortraitsCache.Get(samples[row],new Vector2(252,200),facings[col],Vector3.zero,1f));
    }
   }
  }
 }
}
