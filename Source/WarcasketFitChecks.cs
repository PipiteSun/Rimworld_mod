using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.115.0 热修：战棺适配体型（Test.ps1 -Warcasket -Only WarcasketFit，5 条）。
 public partial class LichChecks {
  static readonly string[] CasketSet={"VFEP_Warcasket_Warcasket","VFEP_WarcasketHelmet_Warcasket","VFEP_WarcasketShoulders_Warcasket"};
  static PawnRenderNode CasketNode(Pawn p)=>RenderNodes(p).FirstOrDefault(n=>n is PawnRenderNode_Apparel a && a.apparel!=null && WarcasketFit.Kind(a.apparel.def)==1);
  static void Redraw(Pawn p){p.Drawer.renderer.SetAllGraphicsDirty();PortraitsCache.SetDirty(p);}
  // 画一张肖像存成 PNG（对照用，Peter 的游戏外看效果）。
  static void Shot(Pawn p,Rot4 r,string file){
   Redraw(p);var rt=PortraitsCache.Get(p,new Vector2(160f,200f),r,new Vector3(0f,0f,.15f),1f);
   var prev=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();RenderTexture.active=prev;
   File.WriteAllBytes(file,tex.EncodeToPNG());UnityEngine.Object.Destroy(tex);
  }
  void CheckWarcasketFit(){
   if(WarcasketFit.CasketType==null){Check(!GenCommandLine.CommandLineArgPassed("lichwarcasket"),"warcasket fit is optional without Pirates");return;}
   var made=new List<Thing>();Pawn Add(Pawn x){made.Add(x);return x;}
   var dir=Path.Combine(GenFilePaths.SaveDataFolderPath,"WarcasketShots");Directory.CreateDirectory(dir);
   try{
    // 1. 火巨人能用战棺铸造厂、能穿所有战棺件。
    var giant=Add(Person());GenSpawn.Spawn(giant,ScCell(-14,-14),map);giant.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_FireGiantCore"),true);
    CheckFireGiantWarcasket(giant);
    // 2. 身体类型：战棺外壳五种体型一样大（截图对照）；装着战棺时头放到参考（男性）体型的位置，没装时照原样。
    var p=Add(Person());GenSpawn.Spawn(p,ScCell(-10,-14),map);p.apparel.DestroyAll();
    var bodies=new[]{BodyTypeDefOf.Male,BodyTypeDefOf.Female,BodyTypeDefOf.Thin,BodyTypeDefOf.Fat,BodyTypeDefOf.Hulk};float k=Mathf.Sqrt(p.ageTracker.CurLifeStage.bodySizeFactor);
    bool headsOk=true;foreach(var b in bodies){p.story.bodyType=b;Redraw(p);var off=p.Drawer.renderer.BaseHeadOffsetAt(Rot4.East);if(!Near(off.x,b.headOffset.x*k) || !Near(off.z,b.headOffset.y*k))headsOk=false;}
    foreach(var n in CasketSet)WearVisual(p,n);
    bool fit=true;string log="";var refO=WarcasketFit.Reference.headOffset*k;
    foreach(var b in bodies){
     p.story.bodyType=b;Redraw(p);
     WarcasketFit.Enabled=false;var h0=p.Drawer.renderer.BaseHeadOffsetAt(Rot4.East);Shot(p,Rot4.East,Path.Combine(dir,b.defName+"_E_before.png"));Shot(p,Rot4.South,Path.Combine(dir,b.defName+"_S.png"));
     WarcasketFit.Enabled=true;var h1=p.Drawer.renderer.BaseHeadOffsetAt(Rot4.East);var w1=p.Drawer.renderer.BaseHeadOffsetAt(Rot4.West);Shot(p,Rot4.East,Path.Combine(dir,b.defName+"_E_after.png"));
     if(!Near(h1.x,refO.x) || !Near(h1.z,refO.y) || !Near(w1.x,-refO.x))fit=false;log+=b.defName+" "+h0.x.ToString("0.00")+"→"+h1.x.ToString("0.00")+"; ";
    }
    Check(headsOk && fit,"warcasket head aligned to reference body (east x): "+log+"unencased heads untouched "+headsOk+"; shots in "+dir);
    // 3. 双头巨人、六臂蛇人：装着战棺时第二个头、额外手臂、蛇尾隐藏，主头回正中；脱了恢复。
    var et=Add(Person());GenSpawn.Spawn(et,ScCell(-6,-14),map);et.genes.SetXenotype(DefDatabase<XenotypeDef>.GetNamed("LP_EttinKin"));
    var ma=Add(Person());GenSpawn.Spawn(ma,ScCell(-2,-14),map);ma.genes.SetXenotype(DefDatabase<XenotypeDef>.GetNamed("LP_MarilithKin"));
    int Extra(Pawn x){Redraw(x);var parms=DrawParms(x,Rot4.South);parms.flags=PawnRenderFlags.Clothes|PawnRenderFlags.Headgear;return RenderNodes(x).Count(n=>n.Props is PawnRenderNodeProperties_LPPart pp && pp.part!="cyclopsEye" && n.Worker.CanDrawNow(n,parms));}
    float eh0=et.Drawer.renderer.BaseHeadOffsetAt(Rot4.South).x;int e0=Extra(et),m0=Extra(ma);
    var worn=new List<Apparel>();foreach(var x in new[]{et,ma}){x.apparel.DestroyAll();foreach(var n in CasketSet)worn.Add(WearVisual(x,n));}
    int e1=Extra(et),m1=Extra(ma);float eh1=et.Drawer.renderer.BaseHeadOffsetAt(Rot4.South).x;Shot(et,Rot4.South,Path.Combine(dir,"Ettin_S.png"));Shot(ma,Rot4.South,Path.Combine(dir,"Marilith_S.png"));
    foreach(var a in worn){a.Wearer?.apparel.Remove(a);a.Destroy();}int e2=Extra(et),m2=Extra(ma);
    Check(e0>0 && m0>0 && e1==0 && m1==0 && e2==e0 && m2==m0 && Math.Abs(eh0)>.1f && Near(eh1,0f),"encased ettin/marilith hide extra parts ("+e0+"/"+m0+" → "+e1+"/"+m1+" → "+e2+"/"+m2+"), ettin head centred "+eh0+"→"+eh1);
    // 4. 泰坦战士 20 装战棺：根节点 ×√1.5、战棺件再乘身体类型修正；泰坦化时根节点跟着变到 ×√1.875，结束复原。
    var t=Add(S2Pawn("LP_Class_Fighter",20,TitanWarrior.Sub,20));t.SetFaction(Faction.OfPlayer);ScSpawn(t,ScCell(2,-14));TitanWarrior.Refresh(Adventurer.Get(t));t.story.bodyType=BodyTypeDefOf.Hulk;
    t.apparel.DestroyAll();foreach(var n in CasketSet)WearVisual(t,n);Redraw(t);
    var cn=CasketNode(t);float root1=RootScale(t),cs1=cn.Worker.ScaleFor(cn,DrawParms(t,Rot4.South)).x;Shot(t,Rot4.South,Path.Combine(dir,"Titan20_S.png"));
    ClassSystem.AddResource(Adventurer.Get(t),DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Fighter"),99f);S2Cast(t,"LP_TT_Titanize",t);float root2=RootScale(t);Shot(t,Rot4.South,Path.Combine(dir,"Titan20_Titanized_S.png"));
    Items.Remove(t,"LP_TT_Titanized");float root3=RootScale(t);var plain=Add(Person());GenSpawn.Spawn(plain,ScCell(6,-14),map);float rootP=RootScale(plain);
    Check(Near(root1/rootP,Mathf.Sqrt(1.5f)) && Near(root2/rootP,Mathf.Sqrt(1.875f)) && Near(root3,root1) && cs1>0f,
     "titan in warcasket: root ×"+(root1/rootP)+" → titanized ×"+(root2/rootP)+" → back ×"+(root3/rootP)+", casket node ×"+cs1);
    // 5. 泰坦的武器握持位置按画面倍率外移（相对身体中心）。
    TitanWarrior.WeaponScale=1.5f;TitanWarrior.WeaponOrigin=new Vector3(10f,0f,10f);var m=TitanWarrior.WeaponTRS(new Vector3(10.4f,1f,9.8f),Quaternion.identity,Vector3.one);TitanWarrior.WeaponScale=1f;TitanWarrior.WeaponOrigin=Vector3.zero;
    var pos=m.GetColumn(3);
    Check(Near(pos.x,10.6f) && Near(pos.z,9.7f) && Near(pos.y,1f) && Near(m.lossyScale.x,1.5f),"titan weapon grip offset scales with render: ("+pos.x+","+pos.z+")");
   }finally{
    WarcasketFit.Enabled=true;TitanWarrior.WeaponScale=1f;
    foreach(var x in made)if(x!=null && !x.Destroyed){if(x is Pawn pp){TitanWarrior.Users.Remove(pp.thingIDNumber);if(pp.Dead){pp.Corpse?.Destroy();continue;}}x.Destroy();}
   }
  }
 }
}
