using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.102.0 六臂蛇人、双头巨人、独眼巨人从 HAR 种族改成原版人类 + 异种型（照 Alpha Genes：特殊身体用基因的渲染节点 + 状态/能力，不换身体）。
 // 这里是三族共用的外观节点（额外手臂、蛇尾、第二个头和它的头发、双盔的另一半、独眼），主头的左右偏移，以及旧存档里 HAR 种族角色的转换。
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
  // 双头巨人第二个头的款式跟着主头：男 A（普通脸）/ B（宽脸），女 F。
  public static string HeadVariant(Pawn p){var h=p?.story?.headType;if(p?.gender==Gender.Female || h?.gender==Gender.Female)return "F";return h!=null && h.defName.Contains("Wide")?"B":"A";}
  public static string TexPath(Pawn p,PawnRenderNodeProperties_LPPart props)=>props.byHeadType?props.texPath+HeadVariant(p):props.texPath;
  static bool Helm(Pawn p)=>Items.Worn(p,Ettin.Helm)!=null;
  // 遮住整个头或眼睛的头盔（照原来 HAR 独眼附件的 hiddenUnderApparelFor FullHead / Eyes）。
  static bool EyeCovered(Pawn p,PawnDrawParms parms){
   if(p.apparel==null || !parms.flags.FlagSet(PawnRenderFlags.Headgear))return false;
   foreach(var a in p.apparel.WornApparel){var g=a.def.apparel?.bodyPartGroups;if(g!=null && (g.Contains(BodyPartGroupDefOf.FullHead) || g.Contains(BodyPartGroupDefOf.Eyes)))return true;}
   return false;
  }
  public static bool Visible(Pawn p,PawnRenderNodeProperties_LPPart props,PawnDrawParms parms){
   if(p==null)return false;
   bool child=Child(p);if(props.stage=="adult" && child || props.stage=="child" && !child)return false;
   if(props.part!="cyclopsEye" && WarcasketFit.Encased(p))return false; // 1.115.0 热修：装着战棺时额外手臂、蛇尾、第二个头都收在战棺里
   switch(props.part){
    case "marilithArmLeft":return Marilith.SideUsable(p,0);
    case "marilithArmRight":return Marilith.SideUsable(p,1);
    case "marilithTail":return true;
    case "ettinHead":return Ettin.BothHeads(p);
    case "ettinHair":return Ettin.BothHeads(p) && !(Helm(p) && parms.flags.FlagSet(PawnRenderFlags.Headgear));
    case "ettinHelm":return Ettin.BothHeads(p) && Helm(p) && parms.flags.FlagSet(PawnRenderFlags.Headgear);
    case "cyclopsEye":return !Cyclops.EyeLost(p) && !EyeCovered(p,parms);
   }
   return true;
  }
  // 主头往一侧挪，给第二个头让位置（原来 HAR 种族的 headOffsetDirectional，原样加到头的基础偏移上）；孤首时回到正中。
  public static Vector3 EttinHeadOffset(Rot4 r)=>r==Rot4.South?new Vector3(.26f,0f,0f):r==Rot4.North?new Vector3(-.26f,0f,0f):r==Rot4.East?new Vector3(.06f,0f,.12f):new Vector3(-.04f,0f,-.07f);
 }
 [HarmonyPatch(typeof(PawnRenderer),nameof(PawnRenderer.BaseHeadOffsetAt))]
 public static class EttinHeadOffset {
  static void Postfix(Rot4 rotation,Pawn ___pawn,ref Vector3 __result){if(Ettin.BothHeads(___pawn))__result+=XenoParts.EttinHeadOffset(rotation);}
 }

 // ---------- 旧存档：1.33.0～1.101.0 的 HAR 种族角色 ----------
 // 读档时种族 LP_Marilith / LP_Ettin / LP_Cyclops（和它们的尸体）改读成原版人类；角色身上的伤口、缺失部位、义肢、手术单原来挂在旧身体（Defs/Legacy_RaceBodies.xml）上，
 // 按“从躯干往下每一层的部位定义 + 同定义里的第几个”搬到人类身体上对应的部位（独眼 → 左眼）；额外手臂、蛇尾、第二个头没有对应，丢掉。
 // 双头巨人：已经是孤首的，主头那一侧的缺失部位也丢掉（剩下的那个头就是现在的头）；第二个头缺失却没有孤首的，补上孤首。
 // 异种型已经是六臂蛇人等，缺了 1.102.0 新加的“蛇身本源”就补上（内源基因）。
 public static class LegacyRaceSave {
  public static readonly Dictionary<string,string> Things=new Dictionary<string,string>{{"LP_Marilith","Human"},{"LP_Ettin","Human"},{"LP_Cyclops","Human"},{"Corpse_LP_Marilith","Corpse_Human"},{"Corpse_LP_Ettin","Corpse_Human"},{"Corpse_LP_Cyclops","Corpse_Human"}};
  static readonly Dictionary<string,string> alias=new Dictionary<string,string>{{"LP_CyclopsEye","Eye"}};
  public static int Moved,Dropped; // 自检用
  public static string ThingName(Type t,string name)=>t!=null && typeof(ThingDef).IsAssignableFrom(t) && name!=null && Things.TryGetValue(name,out var to) && DefDatabase<ThingDef>.GetNamedSilentFail(name)==null?to:null;
  // 部位在旧身体里的路径：每一层（定义名, 同一个父部位下同定义的第几个）。
  static List<(string def,int n)> Path(BodyPartRecord r){
   var list=new List<(string,int)>();
   for(var x=r;x!=null && x.parent!=null;x=x.parent){string d=x.def.defName;if(alias.TryGetValue(d,out var a))d=a;int n=0;foreach(var s in x.parent.parts){if(s==x)break;if(s.def==x.def)n++;}list.Add((d,n));}
   list.Reverse();return list;
  }
  public static BodyPartRecord Map(BodyPartRecord old,BodyDef body){
   if(old==null || body==null)return null;if(old.body==body)return old;
   if(old.parent==null)return body.corePart;
   var cur=body.corePart;
   foreach(var (def,n) in Path(old)){int k=0;BodyPartRecord next=null;foreach(var c in cur.parts){if(c.def.defName!=def)continue;if(k++==n){next=c;break;}}if(next==null)return null;cur=next;}
   return cur;
  }
  static bool InSecondNeck(BodyPartRecord r){for(var x=r;x!=null;x=x.parent)if(x.def==BodyPartDefOf.Neck && x.parts.Any(c=>c.def.defName==Ettin.SecondHead))return true;return false;}
  static bool InMainNeck(BodyPartRecord r){for(var x=r;x!=null;x=x.parent)if(x.def==BodyPartDefOf.Neck && x.parts.Any(c=>c.def==BodyPartDefOf.Head))return true;return false;}
  public static void Fix(Pawn p){
   var set=p?.health?.hediffSet;var body=p?.RaceProps?.body;if(set==null || body==null)return;
   bool any=false,secondLost=false,lone=set.hediffs.Any(h=>h.def.defName==Ettin.LoneHead);
   foreach(var h in set.hediffs.ToList()){
    var old=h.Part;if(old==null || old.body==body)continue;any=true;
    if(old.body?.defName=="LP_EttinBody" && h is Hediff_MissingPart && InSecondNeck(old))secondLost=true;
    var to=Map(old,body);
    if(to!=null && lone && h is Hediff_MissingPart && old.body?.defName=="LP_EttinBody" && InMainNeck(old))to=null;
    if(to==null){set.hediffs.Remove(h);Dropped++;continue;}
    h.Part=to;Moved++;
   }
   // 搬过去以后，缺失部位下面（子部位上）的东西、同一个部位上重复的缺失都去掉（原版的缺失部位下面不挂别的）。
   if(any){
    var missing=new List<Hediff_MissingPart>();
    foreach(var h in set.hediffs.ToList()){
     if(h.Part==null)continue;
     bool under=missing.Any(m=>m.Part==h.Part?h is Hediff_MissingPart:Ettin.Within(h.Part,m.Part));
     if(under){set.hediffs.Remove(h);Dropped++;continue;}
     if(h is Hediff_MissingPart mp){foreach(var o in set.hediffs.Where(o=>o!=h && o.Part!=null && o.Part!=h.Part && Ettin.Within(o.Part,h.Part)).ToList()){set.hediffs.Remove(o);Dropped++;}missing.Add(mp);}
    }
   }
   if(secondLost && !lone){var d=Items.Hediff(Ettin.LoneHead);if(d!=null){set.hediffs.Add(HediffMaker.MakeHediff(d,p));any=true;}}
   var bills=p.health.surgeryBills?.Bills;
   if(bills!=null)foreach(var b in bills.OfType<Bill_Medical>().ToList()){var old=b.Part;if(old==null || old.body==body)continue;any=true;var to=Map(old,body);if(to==null)p.health.surgeryBills.Delete(b);else b.Part=to;}
   if(any){set.DirtyCache();Log.Message("[DND边缘大陆] 旧存档：把 "+p.LabelShortCap+" 从 HAR 种族的身体搬到人类身体上（异种型 "+(p.genes?.Xenotype?.label??"?")+"）。");}
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
   catch(Exception e){Log.Warning("[DND边缘大陆] 旧存档种族转换失败（"+__instance.LabelShortCap+"）："+e.Message);}
  }
 }
}
