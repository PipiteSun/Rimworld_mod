using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 public partial class LichChecks {
  static void ShowTrollGallery(){
   var samples=new List<Pawn>();var map=Find.CurrentMap;
   var xenotype=DefDatabase<XenotypeDef>.GetNamed("LP_Troll");
   for(int i=0;i<2;i++){
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,fixedBiologicalAge:25,fixedChronologicalAge:25,forcedXenotype:xenotype,canGeneratePawnRelations:false));
    GenPlace.TryPlaceThing(p,map.Center,map,ThingPlaceMode.Near);
    p.apparel.DestroyAll();p.equipment.DestroyAllEquipment();
    if(i==1){
     p.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_Duster"),ThingDefOf.Cloth));
     p.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_SimpleHelmet"),ThingDefOf.Steel));
     p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword"),ThingDefOf.Steel));
    }
    p.drafter.Drafted=true;p.Drawer.renderer.SetAllGraphicsDirty();samples.Add(p);
   }
   Find.WindowStack.TryRemove(typeof(FireGiantGearGallery));
   Find.WindowStack.Add(new TrollGallery(samples));
  }
 }
 public class TrollGallery : Window {
  readonly List<Pawn> samples;
  public TrollGallery(List<Pawn> samples){this.samples=samples;forcePause=true;absorbInputAroundWindow=true;}
  public override Vector2 InitialSize=>new Vector2(1100,720);
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(10,0,1060,40),"巨魔 · 上排原生外貌 / 下排常规风衣、头盔与长剑");Text.Font=GameFont.Small;
   var facings=new[]{Rot4.South,Rot4.East,Rot4.North,Rot4.West};
   for(int row=0;row<2;row++)for(int col=0;col<4;col++){
    var box=new Rect(10+col*260,55+row*285,250,265);Widgets.DrawBoxSolid(box,new Color(.18f,.2f,.21f));
    GUI.DrawTexture(box,PortraitsCache.Get(samples[row],new Vector2(250,265),facings[col],Vector3.zero,.75f));
   }
  }
 }
}
