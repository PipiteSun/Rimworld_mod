using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 public partial class LichChecks {
  static void ShowConstructRoleGallery(){
   var map=Find.CurrentMap;
   var owner=map.mapPawns.FreeColonistsSpawned.Find(p=>Necro.Lich(p)!=null && !p.Dead);
   var samples=new List<Pawn>();
   foreach(var kind in new[]{"Skeleton","Golem","Scholar"})for(int dressed=0;dressed<2;dressed++){
    var p=Constructs.Create(kind,owner,CellFinder.RandomClosewalkCellNear(owner.Position,map,9),map);
    p.apparel.DestroyAll();p.equipment.DestroyAllEquipment();
    if(dressed==1){
     p.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_ArmorRecon")));
     p.apparel.Wear((Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_ArmorHelmetRecon")));
    }
    p.drafter.Drafted=true;p.Drawer.renderer.SetAllGraphicsDirty();samples.Add(p);
   }
   Find.WindowStack.TryRemove(typeof(TrollGallery));
   Find.WindowStack.Add(new ConstructRoleGallery(samples));
  }
 }
 public class ConstructRoleGallery : Window {
  readonly List<Pawn> samples;
  public ConstructRoleGallery(List<Pawn> samples){this.samples=samples;forcePause=true;absorbInputAroundWindow=true;}
  public override Vector2 InitialSize=>new Vector2(1140,725);
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(10,0,1090,38),"死灵造物 · 原版外表基因 + 独立特征 / 装备适配");Text.Font=GameFont.Small;
   var names=new[]{"骷髅仆役","血肉巨像","知识死灵"};
   var facings=new[]{Rot4.South,Rot4.South,Rot4.East,Rot4.North,Rot4.West};
   for(int row=0;row<3;row++){
    Widgets.Label(new Rect(8,52+row*204,115,40),names[row]);
    for(int col=0;col<5;col++){
     var box=new Rect(120+col*197,45+row*204,190,193);Widgets.DrawBoxSolid(box,new Color(.18f,.2f,.21f));
     GUI.DrawTexture(box,PortraitsCache.Get(samples[row*2+(col==0?0:1)],new Vector2(190,193),facings[col],Vector3.zero,1f));
    }
   }
   Widgets.Label(new Rect(120,660,1000,28),"左：未着甲　其余：侦察护甲与头盔的正面、侧面、背面");
  }
 }
}
