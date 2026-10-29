using System;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // Body/head stay on native human meshes so ordinary apparel and modded apparel
 // use the same anchors, body types, facing and posture as the visible anatomy.
 public class ConstructBodyNode : PawnRenderNode_Body {
  public ConstructBodyNode(Pawn p,PawnRenderNodeProperties props,PawnRenderTree tree):base(p,props,tree){}
  public override Graphic GraphicFor(Pawn p){
   if(p.Drawer.renderer.CurRotDrawMode==RotDrawMode.Dessicated)return base.GraphicFor(p);
   bool bone=p.def.defName!="LP_Golem";
   string path=bone?p.story.bodyType.bodyDessicatedGraphicPath:p.story.bodyType.bodyNakedGraphicPath;
   return GraphicDatabase.Get<Graphic_Multi>(path,ShaderDatabase.Cutout,Vector2.one,bone?new Color(.82f,.88f,.79f):new Color(.62f,.46f,.47f));
  }
 }
 public class ConstructHeadNode : PawnRenderNode_Head {
  public ConstructHeadNode(Pawn p,PawnRenderNodeProperties props,PawnRenderTree tree):base(p,props,tree){}
  public override Graphic GraphicFor(Pawn p){
   if(!p.health.hediffSet.HasHead)return null;
   if(p.Drawer.renderer.CurRotDrawMode==RotDrawMode.Dessicated)return base.GraphicFor(p);
   return (p.def.defName=="LP_Golem"?p.story.headType:HeadTypeDefOf.Skull).GetGraphic(p,p.def.defName=="LP_Golem"?new Color(.62f,.46f,.47f):new Color(.82f,.88f,.79f));
  }
 }
 public class ConstructRootWorker : PawnRenderNodeWorker {
  public override Vector3 ScaleFor(PawnRenderNode node,PawnDrawParms parms)=>base.ScaleFor(node,parms)*(parms.pawn.def.defName=="LP_Golem"?1.25f:1f);
 }
 public static class ConstructAppearance {
  public static float Size(Pawn p){if(p?.def!=ThingDefOf.Human)return 1f;var k=Necro.Construct(p)?.kind;return k=="Golem"?1.25f:k=="StoneGolem"?1.2f:1f;}
 }
 public class ConstructRoleMarkNode : PawnRenderNode {
  public ConstructRoleMarkNode(Pawn p,PawnRenderNodeProperties props,PawnRenderTree tree):base(p,props,tree){}
  public override Graphic GraphicFor(Pawn p){
   var kind=Necro.Construct(p)?.kind;
   bool head=Props.parentTagDef?.defName=="Head";
   if((head && kind!="Scholar") || (!head && kind!="Golem"))return null;
   return GraphicDatabase.Get<Graphic_Single>(Props.texPath,ShaderDatabase.Cutout,Vector2.one,Color.white);
  }
 }
 public class ConstructRoleMarkWorker : PawnRenderNodeWorker {
  public override bool CanDrawNow(PawnRenderNode node,PawnDrawParms parms){
   if(!base.CanDrawNow(node,parms) || parms.pawn.Dead || parms.facing==Rot4.North)return false;
   bool head=node.Props.parentTagDef?.defName=="Head";
   if(head && !parms.pawn.health.hediffSet.HasHead)return false;
   if(parms.Portrait && (head?!PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(parms):!parms.flags.FlagSet(PawnRenderFlags.Clothes)))return true;
   var part=head?parms.pawn.RaceProps.body.AllParts.FirstOrDefault(x=>x.def==BodyPartDefOf.Head):parms.pawn.RaceProps.body.corePart;
   return !ImplantMarkWorker.Covered(parms.pawn,part);
  }
  public override Vector3 OffsetFor(PawnRenderNode node,PawnDrawParms parms,out Vector3 pivot){
   var offset=base.OffsetFor(node,parms,out pivot);
   if(node.Props.parentTagDef?.defName!="Head")return offset;
   return offset+new Vector3(parms.facing==Rot4.East ? .11f : parms.facing==Rot4.West ? -.11f : 0f,0f,.11f);
  }
 }
 public partial class Hediff_Construct {
  public int appearanceVersion;
  public void UpgradeAppearance(){UpgradeHuman();SeedAppearanceGenes();}
 }
 public class ImplantMarkNode : PawnRenderNode {
  public ImplantMarkNode(Pawn p,PawnRenderNodeProperties props,PawnRenderTree tree):base(p,props,tree){}
  public override Graphic GraphicFor(Pawn p)=>GraphicDatabase.Get<Graphic_Single>(Props.texPath,ShaderDatabase.Cutout,Vector2.one,Color.white);
 }
 public class ImplantMarkWorker : PawnRenderNodeWorker {
  // Native wound anchors carry the body type, head shape and mirrored-part data.
  // Keep these offsets local to the Head/Body parent: VEF scales those parents,
  // so multiplying the cosmetic scale here would apply it a second time.
  public static bool TryAnchor(Pawn p,BodyPartRecord part,Rot4 facing,out Vector3 offset){
   offset=Vector3.zero;if(part==null || p.story?.bodyType==null)return false;
   bool found=false;
   foreach(var anchor in PawnDrawUtility.FindAnchors(p,part)){
    if(!PawnDrawUtility.AnchorUsable(p,anchor,facing))continue;
    if(!found || anchor.narrowCrown.HasValue){float range;PawnDrawUtility.CalcAnchorData(p,anchor,facing,out offset,out range);found=true;}
    if(anchor.narrowCrown.HasValue)return true;
   }
   return found;
  }
  public static bool Covered(Pawn p,BodyPartRecord part){var apparel=p.apparel?.WornApparel;if(part!=null && apparel!=null)for(int i=0;i<apparel.Count;i++)if(apparel[i].def.apparel.CoversBodyPart(part))return true;return false;}
  public override bool CanDrawNow(PawnRenderNode node,PawnDrawParms parms){
   var h=node.hediff;var p=parms.pawn;
   if(h==null || p.Dead || p.health.hediffSet.PartIsMissing(h.Part))return false;
   string key=h.def.defName;
   // The shared ground aura has no left/right occlusion, unlike the actual limb.
   if(key=="LP_ShadowLeg")return node.DebugEnabled && (parms.rotDrawMode==RotDrawMode.Fresh || parms.rotDrawMode==RotDrawMode.Rotting) && p.health.hediffSet.hediffs.FirstOrDefault(x=>x.def==h.def && !p.health.hediffSet.PartIsMissing(x.Part))==h;
   if(!base.CanDrawNow(node,parms))return false;
   if((key=="LP_BlindSight" || key=="LP_DeadPersonality") && parms.facing==Rot4.North)return false;
   bool headMark=key=="LP_BlindSight" || key=="LP_DeadPersonality";
   // Warcaskets deliberately keep their helmets visible even when portrait hats
   // are disabled. Use the game's (and VEF/Pirates' patched) visibility decision.
   if(parms.Portrait && (headMark?!PawnRenderNodeWorker_Apparel_Head.HeadgearVisible(parms):!parms.flags.FlagSet(PawnRenderFlags.Clothes)))return true;
   // A brain implant is represented by a forehead seal; helmets cover the head,
   // not the internal brain record. Eye coverings and sleeves use actual coverage.
   var coverPart=key=="LP_DeadPersonality"?p.RaceProps.body.AllParts.FirstOrDefault(x=>x.def==BodyPartDefOf.Head):h.Part;
   return !Covered(p,coverPart);
  }
  public override Vector3 OffsetFor(PawnRenderNode node,PawnDrawParms parms,out Vector3 pivot){
   var offset=base.OffsetFor(node,parms,out pivot);var part=node.hediff?.Part;var p=parms.pawn;Vector3 anchor;
   switch(node.hediff?.def.defName){
    case "LP_BlindSight":
     return TryAnchor(p,part,parms.facing,out anchor)?offset+anchor:offset;
    case "LP_DeadPersonality":
     // Place the seal above the eyes, following the head's own side-eye offset.
     Vector3 sum=Vector3.zero;int count=0;
     foreach(var eye in p.RaceProps.body.AllParts)if(eye.def==BodyPartDefOf.Eye && TryAnchor(p,eye,parms.facing,out anchor)){sum+=anchor;count++;}
     // Wound eye anchors use the native eye attachment's -0.25 head-space
     // correction; the forehead seal sits 0.17 above that eye line.
     if(count>0){sum/=count;if(parms.facing.IsHorizontal)sum.x*=.5f;sum.z-=.08f;return offset+sum;}
     return offset+new Vector3(0,0,.10f);
    case "LP_WitheringHand":
     return TryAnchor(p,part,parms.facing,out anchor)?offset+anchor+new Vector3(0,0,-.08f):offset;
    default:return offset+new Vector3(0,0,-.63f);
   }
  }
  public override Vector3 ScaleFor(PawnRenderNode node,PawnDrawParms parms){
   var scale=base.ScaleFor(node,parms);
   if(node.hediff?.def.defName=="LP_BlindSight")return scale*(parms.pawn.ageTracker.CurLifeStage.eyeSizeFactor??1f);
   return scale;
  }
 }
}
