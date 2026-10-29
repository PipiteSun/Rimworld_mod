using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  static List<Pawn> visualPawns;
  static Window visualWindow;
  static List<PawnRenderNode> RenderNodes(Pawn p){p.Drawer.renderer.EnsureGraphicsInitialized();var list=new List<PawnRenderNode>();CollectNodes(p.Drawer.renderer.renderTree.rootNode,list);return list;}
  static void CollectNodes(PawnRenderNode n,List<PawnRenderNode> list){if(n==null)return;list.Add(n);if(n.children!=null)foreach(var child in n.children)CollectNodes(child,list);}
  static PawnDrawParms DrawParms(Pawn p,Rot4 facing)=>new PawnDrawParms{pawn=p,facing=facing,rotDrawMode=RotDrawMode.Fresh,posture=PawnPosture.Standing,flags=PawnRenderFlags.None};
  static Apparel WearVisual(Pawn p,string name){var def=DefDatabase<ThingDef>.GetNamed(name);var item=(Apparel)ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.Cloth:null);p.apparel.Wear(item,false);return item;}
  void CheckAppearance(Pawn implanted){
   foreach(var p in Necro.LivingConstructs(worker).GroupBy(x=>Necro.Construct(x).kind).Select(g=>g.First())){
    var h=Necro.Construct(p);h.UpgradeAppearance();var nodes=RenderNodes(p);
    if(h.kind=="Golem"){
     float armorBefore=p.GetStatValue(StatDefOf.ArmorRating_Sharp,true,-1);var saved=h.design;var plan=saved?.Copy()??new ConstructDesign();plan.steel+=60;h.design=plan;p.health.Notify_HediffChanged(h);
     Check(Near(p.GetStatValue(StatDefOf.ArmorRating_Sharp,true,-1),armorBefore+.1f),"human golem retains native role armor plus added material armor");h.design=saved;p.health.Notify_HediffChanged(h);
     Check(p.BodySize>2f && p.GetStatValue(StatDefOf.MeleeDamageFactor,true,-1)>=5f && p.story.bodyType==BodyTypeDefOf.Hulk && Near(ConstructAppearance.Size(p),1.25f),"human golem keeps physical size and melee role with scaled Hulk appearance");
    }
    var roleMarks=nodes.OfType<ConstructRoleMarkNode>().Where(n=>n.PrimaryGraphic!=null).ToList();
    Check(roleMarks.Count==(h.kind=="Skeleton"?0:1),h.kind+" has only its intended modular appearance mark");
    if(h.kind!="Skeleton")Check(roleMarks.All(n=>n.Worker.CanDrawNow(n,DrawParms(p,Rot4.South))),h.kind+" role mark draws on uncovered pawn");
    Check(new[]{"Body","Head","ApparelBody","ApparelHead"}.All(tag=>nodes.Any(n=>n.Props.tagDef?.defName==tag)),p.def.defName+" exposes native body/head/apparel anchors");
    Check(nodes.Any(n=>n.Worker is PawnRenderNodeWorker_Carried),p.def.defName+" includes carried weapon/item render worker");
    var armor=WearVisual(p,"Apparel_ArmorRecon");var helmet=WearVisual(p,"Apparel_ArmorHelmetRecon");nodes=RenderNodes(p);
    foreach(var gear in new[]{armor,helmet})Check(nodes.Any(n=>n.apparel==gear && n.PrimaryGraphic!=null),p.def.defName+" builds actual worn graphic for "+gear.def.defName);
    if(h.kind!="Skeleton")Check(nodes.OfType<ConstructRoleMarkNode>().Where(n=>n.PrimaryGraphic!=null).All(n=>!n.Worker.CanDrawNow(n,DrawParms(p,Rot4.South))),h.kind+" role mark hides beneath covering armor");
    foreach(var r in new[]{Rot4.North,Rot4.East,Rot4.South,Rot4.West})foreach(var n in nodes.Where(n=>n.apparel!=null))Check(n.PrimaryGraphic==null || n.PrimaryGraphic.MatAt(r)!=BaseContent.BadMat,"apparel facing material exists "+p.def.defName+" "+r);
    p.apparel.Remove(armor);p.apparel.Remove(helmet);armor.Destroy();helmet.Destroy();
   }
   implanted.apparel.DestroyAll();var marks=RenderNodes(implanted).Where(n=>n is ImplantMarkNode).ToList();
   Check(marks.Count==4,"all four installed implants create actual pawn render nodes");
   foreach(var n in marks)Check(n.PrimaryGraphic!=null && n.PrimaryGraphic.MatSingle!=BaseContent.BadMat,"worn implant texture resolves "+n.hediff.def.defName);
   Check(marks.All(n=>n.Worker.CanDrawNow(n,DrawParms(implanted,Rot4.South))),"all implant marks show on uncovered front-facing pawn");
   Check(marks.Where(n=>n.hediff.def.defName=="LP_BlindSight" || n.hediff.def.defName=="LP_DeadPersonality").All(n=>!n.Worker.CanDrawNow(n,DrawParms(implanted,Rot4.North))),"face marks do not appear on back of head");
   var coat=WearVisual(implanted,"Apparel_ArmorRecon");var helm=WearVisual(implanted,"Apparel_ArmorHelmetRecon");marks=RenderNodes(implanted).Where(n=>n is ImplantMarkNode).ToList();
   Check(marks.Where(n=>n.hediff.def.defName!="LP_ShadowLeg").All(n=>!n.Worker.CanDrawNow(n,DrawParms(implanted,Rot4.South))),"helmet and sleeves hide covered implants");
   Check(marks.Single(n=>n.hediff.def.defName=="LP_ShadowLeg").Worker.CanDrawNow(marks.Single(n=>n.hediff.def.defName=="LP_ShadowLeg"),DrawParms(implanted,Rot4.South)),"shadow aura stays below equipped armor");
   var portrait=DrawParms(implanted,Rot4.South);portrait.flags=PawnRenderFlags.Portrait;
   Check(marks.All(n=>n.Worker.CanDrawNow(n,portrait)),"portrait hiding clothes/headgear also reveals covered implant marks");
   implanted.apparel.Remove(coat);implanted.apparel.Remove(helm);coat.Destroy();helm.Destroy();
   var oldLeg=implanted.health.hediffSet.hediffs.First(h=>h.def==Necro.Def<HediffDef>("ShadowLeg"));InstallFlesh(implanted,"ShadowLeg");
   var legMarks=RenderNodes(implanted).Where(n=>n.hediff?.def==oldLeg.def).ToList();
   foreach(var r in new[]{Rot4.North,Rot4.East,Rot4.South,Rot4.West})Check(legMarks.Count(n=>n.Worker.CanDrawNow(n,DrawParms(implanted,r)))==1,"dual shadow legs display exactly one aura in facing "+r);
   implanted.health.RemoveHediff(implanted.health.hediffSet.hediffs.First(h=>h.def==oldLeg.def && h!=oldLeg));
   var occupational=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.IsApparel && d.modContentPack?.PackageIdPlayerFacing=="local.standalone.occupationalarmor").ToList();
   var originalType=implanted.story.bodyType;
   foreach(var bodyType in new[]{BodyTypeDefOf.Male,BodyTypeDefOf.Female,BodyTypeDefOf.Thin,BodyTypeDefOf.Fat,BodyTypeDefOf.Hulk})foreach(var def in occupational){
    implanted.story.bodyType=bodyType;var gear=WearVisual(implanted,def.defName);implanted.Drawer.renderer.SetAllGraphicsDirty();var nodes=RenderNodes(implanted).Where(n=>n.apparel==gear).ToList();
    Check(nodes.Any(n=>n.PrimaryGraphic!=null),"occupational gear native worn node "+def.defName+" "+bodyType.defName);
    foreach(var r in new[]{Rot4.North,Rot4.East,Rot4.South,Rot4.West})Check(nodes.All(n=>n.PrimaryGraphic==null || n.PrimaryGraphic.MatAt(r)!=BaseContent.BadMat),"occupational gear facing "+def.defName+" "+bodyType.defName+" "+r);
    implanted.apparel.Remove(gear);gear.Destroy();
   }
   implanted.story.bodyType=originalType;implanted.Drawer.renderer.SetAllGraphicsDirty();
   var eye=implanted.health.hediffSet.hediffs.First(h=>h.def==Necro.Def<HediffDef>("BlindSight"));implanted.health.RemoveHediff(eye);
   Check(!RenderNodes(implanted).Any(n=>n.hediff==eye),"removing implant removes its render node immediately");InstallFlesh(implanted,"BlindSight");
   var legacy=Person();var old=(Hediff_Construct)legacy.health.AddHediff(Necro.Def<HediffDef>("Construct"));old.kind="Golem";old.appearanceVersion=2;
   foreach(var name in Hediff_Construct.LegacyAppearanceGenes)legacy.genes.AddGene(DefDatabase<GeneDef>.GetNamed(name),true);
   old.SeedAppearanceGenes();
   Check(old.appearanceVersion==3 && Hediff_Construct.AppearanceGenesFor("Golem").All(n=>legacy.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed(n))) && legacy.story.bodyType==BodyTypeDefOf.Hulk,"untouched version 2 construct upgrades to role-specific native genes: version="+old.appearanceVersion+" body="+legacy.story.bodyType?.defName+" genes="+string.Join(",",legacy.genes.GenesListForReading.Select(g=>g.def.defName)));
   legacy.Destroy();
   var edited=Person();var custom=(Hediff_Construct)edited.health.AddHediff(Necro.Def<HediffDef>("Construct"));custom.kind="Scholar";custom.appearanceVersion=2;
   edited.genes.AddGene(DefDatabase<GeneDef>.GetNamed("Body_Hulk"),true);edited.genes.AddGene(DefDatabase<GeneDef>.GetNamed("Skin_Blue"),true);
   custom.SeedAppearanceGenes();
   Check(custom.appearanceVersion==3 && edited.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Skin_Blue")) && edited.genes.GetGene(DefDatabase<GeneDef>.GetNamed("Skin_LightGray"))==null,"edited version 2 construct keeps player appearance");
   edited.Destroy();
  }
  static void ShowAppearanceGallery(){
   var map=Find.CurrentMap;var owner=map.mapPawns.FreeColonistsSpawned.First(p=>Necro.Lich(p)!=null && !p.Dead);visualPawns=new List<Pawn>();
   foreach(var kind in new[]{"Human","Skeleton","Golem","Scholar"})for(int dressed=0;dressed<2;dressed++){
    var at=CellFinder.RandomClosewalkCellNear(owner.Position,map,9);Pawn p;
    if(kind=="Human"){p=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,Faction.OfPlayer);GenSpawn.Spawn(p,at,map);}else p=Constructs.Create(kind,owner,at,map,"大脑");
    p.apparel.DestroyAll();p.equipment.DestroyAllEquipment();p.story.hairDef=DefDatabase<HairDef>.GetNamed("Shaved");p.style.beardDef=BeardDefOf.NoBeard;
    foreach(var key in new[]{"BlindSight","WitheringHand","DeadPersonality","ShadowLeg"}){var recipe=Necro.Def<RecipeDef>("Install"+key);var part=recipe.Worker.GetPartsToApplyOn(p,recipe).FirstOrDefault();if(part!=null){if(p.health.hediffSet.PartIsMissing(part))p.health.RestorePart(part);p.health.AddHediff(Necro.Def<HediffDef>(key),part);}}
    if(dressed==1){var body=DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d=>d.IsApparel && d.modContentPack?.PackageIdPlayerFacing=="local.standalone.occupationalarmor" && d.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso));WearVisual(p,body?.defName??"Apparel_ArmorRecon");var head=DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d=>d.IsApparel && d.modContentPack?.PackageIdPlayerFacing=="local.standalone.occupationalarmor" && d.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead));WearVisual(p,head?.defName??"Apparel_ArmorHelmetRecon");}
    p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle")));p.drafter.Drafted=true;p.Drawer.renderer.SetAllGraphicsDirty();visualPawns.Add(p);
   }
   Messages.Clear();visualWindow=new AppearanceGallery(visualPawns);Find.WindowStack.Add(visualWindow);
  }
  static void ShowAppearanceMap(){
   visualWindow.Close();var map=Find.CurrentMap;
   var center=map.AllCells.OrderBy(c=>c.DistanceTo(map.Center)).First(c=>Enumerable.Range(0,8).All(i=>{var x=c+new IntVec3((i/2)*3-5,0,(i%2)*4-2);return x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map)==null;}));
   for(int i=0;i<visualPawns.Count;i++){var p=visualPawns[i];p.Position=center+new IntVec3((i/2)*3-5,0,(i%2)*4-2);p.Notify_Teleported();p.Rotation=Rot4.South;p.jobs.StopAll();p.drafter.Drafted=true;}
   Find.Selector.ClearSelection();Find.CameraDriver.SetRootPosAndSize(center.ToVector3Shifted(),8);Messages.Clear();
  }
 }
 public class AppearanceGallery : Window {
  readonly List<Pawn> pawns;public AppearanceGallery(List<Pawn> pawns){this.pawns=pawns;forcePause=true;absorbInputAroundWindow=true;}
  public override Vector2 InitialSize=>new Vector2(1160,760);
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(12,0,1100,35),"人类死灵造物 · 原版外表基因与装备显示");Text.Font=GameFont.Small;
   string[] labels={"未着甲 · 正面","装备 · 正面","装备 · 侧面","装备 · 背面","装备 · 另一侧"};
   for(int col=0;col<5;col++)Widgets.Label(new Rect(115+col*195,40,190,26),labels[col]);
   string[] rows={"巫妖/人形","骷髅仆役","血肉巨像","知识死灵"};
   for(int row=0;row<4;row++){
    Widgets.Label(new Rect(6,110+row*153,108,50),rows[row]);
    for(int col=0;col<5;col++){var p=pawns[row*2+(col==0?0:1)];var face=col<2?Rot4.South:col==2?Rot4.East:col==3?Rot4.North:Rot4.West;
     var rect=new Rect(113+col*195,68+row*153,190,148);Widgets.DrawBoxSolid(rect,new Color(.13f,.16f,.18f));GUI.DrawTexture(rect,PortraitsCache.Get(p,new Vector2(190,148),face,new Vector3(0,0,.03f),1.15f));
    }
   }
  }
 }
}
