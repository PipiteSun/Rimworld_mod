using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.102.0 六臂蛇人从 HAR 种族改成原版人类 + 异种型（照 Alpha Genes：特殊身体用基因的渲染节点 + 状态/能力，不换身体）。1.171.0 双头巨人、独眼巨人整族移除。
 // 这里是六臂蛇人的外观节点（额外手臂、蛇尾），以及旧存档里 HAR 种族角色的转换。
 // 偏移照原来的 HAR 附件换算：HAR 的 (x, y) 偏移 + layerOffset，画在身体前面再加 0.3 的高度（后面减 0.3），朝东时 x 取反；这里 south/north/east/west 写的是换算好的 (x, 高度, y)。
 public class PawnRenderNodeProperties_LPPart : PawnRenderNodeProperties {
  public string part,stage="any";public bool byHeadType;
  public Vector3 offset,south,north,east,west;
  public Color colorTwo=Color.white;
  public PawnRenderNodeProperties_LPPart(){nodeClass=typeof(PawnRenderNode_LPPart);workerClass=typeof(PawnRenderNodeWorker_LPPart);}
 }
 public class PawnRenderNode_LPPart : PawnRenderNode {
  public PawnRenderNode_LPPart(Pawn pawn,PawnRenderNodeProperties props,PawnRenderTree tree):base(pawn,props,tree){}
  PawnRenderNodeProperties_LPPart P=>(PawnRenderNodeProperties_LPPart)props;
  public override GraphicMeshSet MeshSetFor(Pawn pawn)=>props.parentTagDef==PawnRenderNodeTagDefOf.Head?HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn,1f,1f):base.MeshSetFor(pawn);
  public override Graphic GraphicFor(Pawn pawn){
   string path=XenoParts.TexPath(pawn,P);if(path.NullOrEmpty())return null;
   var shader=ShaderFor(pawn);if(shader==null)return null;
   return GraphicDatabase.Get<Graphic_Multi>(path,shader,Vector2.one,ColorFor(pawn),P.colorTwo);
  }
 }
 public class PawnRenderNodeWorker_LPPart : PawnRenderNodeWorker {
  public override bool CanDrawNow(PawnRenderNode node,PawnDrawParms parms)=>base.CanDrawNow(node,parms) && node.Props is PawnRenderNodeProperties_LPPart p && XenoParts.Visible(parms.pawn,p,parms);
  public override Vector3 OffsetFor(PawnRenderNode node,PawnDrawParms parms,out Vector3 pivot){
   var v=base.OffsetFor(node,parms,out pivot);if(!(node.Props is PawnRenderNodeProperties_LPPart p))return v;
   var r=parms.facing;return v+p.offset+(r==Rot4.South?p.south:r==Rot4.North?p.north:r==Rot4.East?p.east:p.west);
  }
 }
 public static class XenoParts {
  public static bool Child(Pawn p){var b=p?.story?.bodyType;return b==BodyTypeDefOf.Child || b==BodyTypeDefOf.Baby;}
  // byHeadType 的贴图款式跟着主头：男 A（普通脸）/ B（宽脸），女 F。
  public static string HeadVariant(Pawn p){var h=p?.story?.headType;if(p?.gender==Gender.Female || h?.gender==Gender.Female)return "F";return h!=null && h.defName.Contains("Wide")?"B":"A";}
  public static string TexPath(Pawn p,PawnRenderNodeProperties_LPPart props)=>props.byHeadType?props.texPath+HeadVariant(p):props.texPath;
  public static bool Visible(Pawn p,PawnRenderNodeProperties_LPPart props,PawnDrawParms parms){
   if(p==null)return false;
   bool child=Child(p);if(props.stage=="adult" && child || props.stage=="child" && !child)return false;
   if(WarcasketFit.Encased(p))return false; // 1.115.0 热修：装着战棺时额外手臂、蛇尾都收在战棺里
   switch(props.part){
    case "marilithArmLeft":return Marilith.SideUsable(p,0);
    case "marilithArmRight":return Marilith.SideUsable(p,1);
    case "marilithTail":return true;
   }
   return true;
  }
 }

 // ---------- 旧存档：1.33.0～1.101.0 的 HAR 种族角色 ----------
 // 读档时种族 LP_Marilith（和它的尸体）改读成原版人类；角色身上的伤口、缺失部位、义肢、手术单原来挂在旧身体（Defs/Legacy_RaceBodies.xml）上，
 // 按“从躯干往下每一层的部位定义 + 同定义里的第几个”搬到人类身体上对应的部位；额外手臂、蛇尾没有对应，丢掉。
 // 异种型已经是六臂蛇人等，缺了 1.102.0 新加的“蛇身本源”就补上（内源基因）。
 public static class LegacyRaceSave {
  public static readonly Dictionary<string,string> Things=new Dictionary<string,string>{{"LP_Marilith","Human"},{"Corpse_LP_Marilith","Corpse_Human"}};
  public static int Moved,Dropped; // 自检用
  public static string ThingName(Type t,string name)=>t!=null && typeof(ThingDef).IsAssignableFrom(t) && name!=null && Things.TryGetValue(name,out var to) && DefDatabase<ThingDef>.GetNamedSilentFail(name)==null?to:null;
  // 部位在旧身体里的路径：每一层（定义名, 同一个父部位下同定义的第几个）。
  static List<(string def,int n)> Path(BodyPartRecord r){
   var list=new List<(string,int)>();
   for(var x=r;x!=null && x.parent!=null;x=x.parent){string d=x.def.defName;int n=0;foreach(var s in x.parent.parts){if(s==x)break;if(s.def==x.def)n++;}list.Add((d,n));}
   list.Reverse();return list;
  }
  public static BodyPartRecord Map(BodyPartRecord old,BodyDef body){
   if(old==null || body==null)return null;if(old.body==body)return old;
   if(old.parent==null)return body.corePart;
   var cur=body.corePart;
   foreach(var (def,n) in Path(old)){int k=0;BodyPartRecord next=null;foreach(var c in cur.parts){if(c.def.defName!=def)continue;if(k++==n){next=c;break;}}if(next==null)return null;cur=next;}
   return cur;
  }
  static bool Within(BodyPartRecord part,BodyPartRecord root){for(var r=part;r!=null;r=r.parent)if(r==root)return true;return false;}
  public static void Fix(Pawn p){
   var set=p?.health?.hediffSet;var body=p?.RaceProps?.body;if(set==null || body==null)return;
   bool any=false;
   foreach(var h in set.hediffs.ToList()){
    var old=h.Part;if(old==null || old.body==body)continue;any=true;
    var to=Map(old,body);
    if(to==null){set.hediffs.Remove(h);Dropped++;continue;}
    h.Part=to;Moved++;
   }
   // 搬过去以后，缺失部位下面（子部位上）的东西、同一个部位上重复的缺失都去掉（原版的缺失部位下面不挂别的）。
   if(any){
    var missing=new List<Hediff_MissingPart>();
    foreach(var h in set.hediffs.ToList()){
     if(h.Part==null)continue;
     bool under=missing.Any(m=>m.Part==h.Part?h is Hediff_MissingPart:Within(h.Part,m.Part));
     if(under){set.hediffs.Remove(h);Dropped++;continue;}
     if(h is Hediff_MissingPart mp){foreach(var o in set.hediffs.Where(o=>o!=h && o.Part!=null && o.Part!=h.Part && Within(o.Part,h.Part)).ToList()){set.hediffs.Remove(o);Dropped++;}missing.Add(mp);}
    }
   }
   var bills=p.health.surgeryBills?.Bills;
   if(bills!=null)foreach(var b in bills.OfType<Bill_Medical>().ToList()){var old=b.Part;if(old==null || old.body==body)continue;any=true;var to=Map(old,body);if(to==null)p.health.surgeryBills.Delete(b);else b.Part=to;}
   if(any){set.DirtyCache();Log.Message("[DND边缘大陆] 旧存档：把 ".Loc()+p.LabelShortCap+" 从 HAR 种族的身体搬到人类身体上（异种型 ".Loc()+(p.genes?.Xenotype?.label??"?")+"）。".Loc());}
  }
  public static void EnsureCore(Pawn p){
   var x=p?.genes?.Xenotype;if(x==null || p.genes==null)return;
   if(x.defName==Marilith.Kin){var core=Races124.Gene(Marilith.Core);if(core!=null && p.genes.GetGene(core)==null){p.genes.AddGene(core,!x.inheritable);RaceSize.Invalidate(p);}}
  }
 }
 [HarmonyPatch(typeof(BackCompatibility),nameof(BackCompatibility.BackCompatibleDefName))]
 public static class LegacyRaceDefName {
  static void Postfix(Type defType,string defName,ref string __result){var to=LegacyRaceSave.ThingName(defType,defName);if(to!=null)__result=to;}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.ExposeData))]
 public static class LegacyRacePawnLoad {
  static void Postfix(Pawn __instance){
   if(Scribe.mode!=LoadSaveMode.PostLoadInit || __instance?.RaceProps?.Humanlike!=true)return;
   try{LegacyRaceSave.Fix(__instance);LegacyRaceSave.EnsureCore(__instance);}
   catch(Exception e){Log.Warning("[DND边缘大陆] 旧存档种族转换失败（".Loc()+__instance.LabelShortCap+"）：".Loc()+e.Message);}
  }
 }
}
