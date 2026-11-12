using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.142.0 银行（Defs/Bank.xml，设计稿「银行（金银储存）设计稿」）：2×2 金库，只收银与金，每格 20 堆。
 // 储存全用原版（maxItemsInCell、固定过滤器），这里只管展示和保护：
 //  - 1.142.0 改版（Peter 给的参考图）：一座带宽玻璃窗的保险金柜，窗里左半 / 右半按左右两列的堆数各摆 0–6 个原版银 / 金物品图，窗框与反光另印一层盖在前面；堆数变化时重算并标脏；
 //  - 银行格里的银 / 金不画、不显示数量标签、不能点选、鼠标悬停读数只显示银行一行；
 //  - 银行格里的银 / 金不受伤害；拆除时多出来的堆就近放回地上。
 public class Building_LPBank : Building_Storage {
  public const int PerCell=20;
  // 柜窗左半 / 右半（左列 / 右列，各管上下两格）里要画的物品图：整组替换，渲染线程只读。
  public struct Icon { public Vector3 offset; public Material mat; public ThingDef def; }
  Icon[][] cols={new Icon[0],new Icon[0]};
  int goldCached;
  public Icon[] ColumnIcons(int col)=>cols[col];
  // 柜窗里放几堆物品图：0 堆不画，每 7 堆多一个，最多 6 个（整列 40 堆）。
  public static int IconCount(int stacks)=>stacks<=0?0:Mathf.Min(6,1+(stacks-1)/7);
  // 柜窗里的摆放位置（相对柜窗中心，格）：按放入顺序，后放的在上层。
  static readonly Vector2[] Slots={new Vector2(-.22f,-.17f),new Vector2(.22f,-.17f),new Vector2(0f,-.15f),new Vector2(-.11f,.01f),new Vector2(.11f,.02f),new Vector2(0f,.13f)};
  public const float IconSize=.44f;
  // 柜窗：整座一扇宽窗，中心在建筑中心往上 0.45 格；左半（中心 x -0.4）摆左边两格的钱，右半（+0.4）摆右边两格的。
  public static readonly float WindowUp=.45f,HalfX=.4f;
  public static bool IsCoin(ThingDef d)=>d==ThingDefOf.Silver || d==ThingDefOf.Gold;
  // 这件东西是不是存在某座银行里的银 / 金（用建筑网格查，任何线程都能读）。
  public static bool InBank(Thing t){
   if(t==null || !IsCoin(t.def) || !t.Spawned)return false;
   var map=t.Map;if(map==null)return false;
   return t.Position.GetEdifice(map) is Building_LPBank;
  }

  public IEnumerable<Thing> Coins(){
   if(!Spawned)yield break;
   foreach(var c in this.OccupiedRect()){var list=Map.thingGrid.ThingsListAtFast(c);for(int i=0;i<list.Count;i++)if(IsCoin(list[i].def))yield return list[i];}
  }
  public int Count(ThingDef d){int n=0;foreach(var t in Coins())if(t.def==d)n+=t.stackCount;return n;}
  public int StackCount=>Coins().Count();
  public int Capacity=>PerCell*def.size.x*def.size.z;
  public float TotalValue{get{float v=0f;foreach(var t in Coins())v+=t.stackCount*t.MarketValue;return v;}}
  // 美观：基础 +6，另按存金量每 500 金 +1，最多 +6。
  public float GoldBeauty=>Mathf.Min(6f,goldCached/500);

  public override void SpawnSetup(Map map,bool respawningAfterLoad){
   base.SpawnSetup(map,respawningAfterLoad);
   Refresh(null,null,true);
  }
  // 读档时银行可能先于里面的钱生成：整张图读完（PostMapInit）再算一次。
  public override void PostMapInit(){base.PostMapInit();Refresh(null,null,true);}
  public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish){
   var map=Map;var rect=this.OccupiedRect();
   base.DeSpawn(mode);
   if(map==null)return;
   // 钱留在原地：每格只留一堆，其余就近放到地上（数量不变）。
   foreach(var c in rect){
    var coins=c.GetThingList(map).Where(t=>IsCoin(t.def)).ToList();
    for(int i=1;i<coins.Count;i++){var t=coins[i];if(t.Destroyed || !t.Spawned)continue;t.DeSpawn();if(!GenPlace.TryPlaceThing(t,c,map,ThingPlaceMode.Near))GenSpawn.Spawn(t,c,map);}
    map.mapDrawer.MapMeshDirty(c,MapMeshFlagDefOf.Things);
   }
  }
  public override void Notify_ReceivedThing(Thing newItem){base.Notify_ReceivedThing(newItem);Refresh(newItem,null,false);}
  public override void Notify_LostThing(Thing newItem){base.Notify_LostThing(newItem);Refresh(null,newItem,false);}
  public override void TickRare(){base.TickRare();Refresh(null,null,false);}

  // 重算两列柜窗里的物品图；变了才重印网格。added / removed：通知时这件东西可能还没进 / 还没离开格子。
  public void Refresh(Thing added,Thing removed,bool force){
   if(!Spawned)return;
   var rect=this.OccupiedRect();int gold=0;
   var ns=new int[2];var ng=new int[2];var maxS=new int[2];var maxG=new int[2];
   foreach(var c in rect){
    int col=c.x-rect.minX>0?1:0;bool seenAdded=false;var list=Map.thingGrid.ThingsListAtFast(c);
    void Add(Thing x){if(x.def==ThingDefOf.Silver){ns[col]++;maxS[col]=Mathf.Max(maxS[col],x.stackCount);}else if(x.def==ThingDefOf.Gold){ng[col]++;gold+=x.stackCount;maxG[col]=Mathf.Max(maxG[col],x.stackCount);}}
    for(int k=0;k<list.Count;k++){var x=list[k];if(x==removed)continue;if(x==added)seenAdded=true;Add(x);}
    if(added!=null && !seenAdded && added.Position==c && !added.Destroyed)Add(added);
   }
   goldCached=gold;
   var next=new Icon[2][];bool same=!force;
   for(int col=0;col<2;col++){
    next[col]=Build(col,ns[col],ng[col],maxS[col],maxG[col]);
    var old=cols[col];if(old.Length!=next[col].Length)same=false;else for(int k=0;k<old.Length && same;k++)if(old[k].mat!=next[col][k].mat)same=false;
   }
   cols=next;
   if(!same)foreach(var c in rect)Map.mapDrawer.MapMeshDirty(c,MapMeshFlagDefOf.Things);
  }
  // 一列：总图数按堆数，金的份数按比例（两种都有时各至少一个），金银交替摆。
  Icon[] Build(int col,int s,int g,int maxS,int maxG){
   int n=IconCount(s+g);if(n==0)return new Icon[0];
   int gi=Mathf.RoundToInt(n*(float)g/(s+g));if(g>0)gi=Mathf.Max(1,gi);if(s>0)gi=Mathf.Min(n-1,gi);int si=n-gi;
   var r=new Icon[n];float baseX=col==0?-HalfX:HalfX;
   for(int k=0;k<n;k++){
    bool isGold=si==0 || gi>0 && (k%2==1 || k>=2*si);
    if(isGold)gi--;else si--;
    var d=isGold?ThingDefOf.Gold:ThingDefOf.Silver;
    r[k]=new Icon{def=d,mat=BankArt.ItemMat(d,isGold?maxG:maxS),offset=new Vector3(baseX+Slots[k].x,0f,WindowUp+Slots[k].y)};
   }
   return r;
  }

  public override void Print(SectionLayer layer){
   base.Print(layer);
   var ctr=DrawPos;float y=AltitudeLayer.BuildingOnTop.AltitudeFor();
   foreach(var icons in cols)
    for(int k=0;k<icons.Length;k++){var p=ctr+icons[k].offset;p.y=y+k*.0004f;PrintMat(layer,p,new Vector2(IconSize,IconSize),icons[k].mat,TextureAtlasGroup.Item,false);}
   // 玻璃与柜门立柱盖在物品图前面。
   var f=ctr;f.y=y+.01f;PrintMat(layer,f,new Vector2(def.size.x,def.size.z),BankArt.Front,TextureAtlasGroup.Building,false);
  }
  // 照 Graphic.Print 的做法取贴图集里的 UV（原版物品图都在贴图集里）。不在贴图集里的单张贴图，
  // 这个运行时按默认 UV 印出来是上下颠倒的，flipV 时换成上下翻转的 UV。
  static readonly Vector2[] FlipV={new Vector2(0f,1f),new Vector2(0f,0f),new Vector2(1f,0f),new Vector2(1f,1f)};
  static void PrintMat(SectionLayer layer,Vector3 pos,Vector2 size,Material mat,TextureAtlasGroup group,bool flipV){
   Vector2[] uvs=null;var col=new Color32(255,255,255,255);
   if(Graphic.TryGetTextureAtlasReplacementInfo(mat,group,false,true,out var atlasMat,out var atlasUvs,out var atlasCol)){mat=atlasMat;uvs=atlasUvs;col=atlasCol;}
   else if(flipV)uvs=FlipV;
   Printer_Plane.PrintPlane(layer,pos,size,mat,0f,false,uvs,new[]{col,col,col,col});
  }

  // 原版储存会给每一堆钱一个「选择」按钮（几十个）；银行在「金库清单」里管单堆，去掉这些按钮。
  static string selectDesc;
  public override IEnumerable<Gizmo> GetGizmos(){
   if(selectDesc==null)selectDesc="CommandSelectStoredThingDesc".Translate().ToString();
   foreach(var g in base.GetGizmos()){
    if(g is Command_Action c && !selectDesc.NullOrEmpty() && c.defaultDesc!=null && c.defaultDesc.StartsWith(selectDesc))continue;
    yield return g;
   }
  }

  public string Summary()=>"银 "+Count(ThingDefOf.Silver).ToString("N0")+" · 金 "+Count(ThingDefOf.Gold).ToString("N0")+" · 库存 "+StackCount+"/"+Capacity+" 堆 · 总价值 "+TotalValue.ToString("N0");
  public override string LabelMouseover=>LabelCap+"（"+Summary()+"）";
  public override string GetInspectString(){
   var s=base.GetInspectString();
   return (s.NullOrEmpty()?"":s.TrimEndNewlines()+"\n")+Summary();
  }
 }

 [StaticConstructorOnStartup]
 public static class BankArt {
  public static readonly Material Front=MaterialPool.MatFrom("LP/Bank/BankVault_Front",ShaderDatabase.Cutout);
  // 原版物品图：银 / 金按堆的数量取对应的那张（Graphic_StackCount 的 a/b/c）。
  public static Material ItemMat(ThingDef d,int count){
   var g=d.graphic;if(g is Graphic_StackCount sc)return sc.SubGraphicForStackCount(Mathf.Max(1,count),d).MatSingle;
   return g.MatSingle;
  }
  static BankArt(){
   var b=StatDefOf.Beauty;if(b.parts==null)b.parts=new List<StatPart>();
   if(!b.parts.Any(x=>x is StatPart_BankBeauty)){b.parts.Add(new StatPart_BankBeauty{parentStat=b});b.immutable=false;}
  }
 }

 public class StatPart_BankBeauty : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Building_LPBank b)val+=b.GoldBeauty;}
  public override string ExplanationPart(StatRequest req){if(req.Thing is Building_LPBank b && b.GoldBeauty>0f)return "金库里的金子：+"+b.GoldBeauty.ToString("0");return null;}
 }

 // ==================== Harmony ====================
 [HarmonyPatch(typeof(SectionLayer_ThingsGeneral),"TakePrintFrom")]
 static class BankHidePrint { static bool Prefix(Thing t)=>!Building_LPBank.InBank(t); }

 [HarmonyPatch]
 static class BankHideLabel {
  static IEnumerable<MethodBase> TargetMethods(){
   yield return AccessTools.DeclaredMethod(typeof(Thing),nameof(Thing.DrawGUIOverlay));
   var comps=AccessTools.DeclaredMethod(typeof(ThingWithComps),nameof(Thing.DrawGUIOverlay));if(comps!=null)yield return comps;
  }
  static bool Prefix(Thing __instance)=>!Building_LPBank.InBank(__instance);
 }

 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 static class BankProtect {
  static bool Prefix(Thing __instance,ref DamageWorker.DamageResult __result){
   if(!Building_LPBank.InBank(__instance))return true;
   __result=new DamageWorker.DamageResult();return false;
  }
 }

 // 点格子（或框选）只选中银行，不会一次选中几十堆钱；单堆在「金库清单」里管理。
 [HarmonyPatch(typeof(ThingSelectionUtility),nameof(ThingSelectionUtility.SelectableByMapClick))]
 static class BankNoClickSelect { static void Postfix(Thing t,ref bool __result){if(__result && Building_LPBank.InBank(t))__result=false;} }

 // 左下角鼠标悬停读数：银行格里不逐堆列出银 / 金（银行自己那行带总数）。
 [HarmonyPatch(typeof(MouseoverReadout),nameof(MouseoverReadout.MouseoverReadoutOnGUI))]
 static class BankReadout {
  static readonly List<Thing> buf=new List<Thing>();
  public static List<Thing> Things(IntVec3 c,Map map){
   var list=c.GetThingList(map);
   if(!(c.GetEdifice(map) is Building_LPBank))return list;
   buf.Clear();for(int i=0;i<list.Count;i++)if(!Building_LPBank.IsCoin(list[i].def))buf.Add(list[i]);
   return buf;
  }
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code){
   var from=AccessTools.Method(typeof(GridsUtility),nameof(GridsUtility.GetThingList),new[]{typeof(IntVec3),typeof(Map)});
   var to=AccessTools.Method(typeof(BankReadout),nameof(Things));
   foreach(var ins in code){if(ins.Calls(from)){ins.opcode=OpCodes.Call;ins.operand=to;}yield return ins;}
  }
 }

 // ==================== 「金库清单」检视页 ====================
 public class ITab_BankLedger : ITab {
  Vector2 scroll;float viewH;
  public ITab_BankLedger(){size=new Vector2(460f,480f);labelKey="LP_BankLedgerTab";}
  Building_LPBank Bank=>SelThing as Building_LPBank;
  public override bool IsVisible=>Bank!=null;

  protected override void FillTab(){
   var b=Bank;if(b==null)return;
   var rect=new Rect(0f,0f,size.x,size.y).ContractedBy(12f);
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(rect.x,rect.y,rect.width,32f),"金库清单");
   Text.Font=GameFont.Small;float y=rect.y+36f;
   var coins=b.Coins().ToList();
   foreach(var d in new[]{ThingDefOf.Silver,ThingDefOf.Gold}){
    var of=coins.Where(t=>t.def==d).ToList();int n=of.Sum(t=>t.stackCount);float v=of.Sum(t=>t.stackCount*t.MarketValue);
    Widgets.ThingIcon(new Rect(rect.x,y,28f,28f),d);
    Widgets.Label(new Rect(rect.x+34f,y+4f,rect.width-34f,24f),d.LabelCap+"："+n.ToString("N0")+"（"+of.Count+" 堆，价值 "+v.ToString("N0")+"）");
    y+=30f;
   }
   Widgets.Label(new Rect(rect.x,y+2f,rect.width,24f),"库存 "+coins.Count+"/"+b.Capacity+" 堆 · 总价值 "+b.TotalValue.ToString("N0"));
   y+=30f;Widgets.DrawLineHorizontal(rect.x,y,rect.width);y+=6f;
   var outRect=new Rect(rect.x,y,rect.width,rect.yMax-y);var view=new Rect(0f,0f,outRect.width-16f,Mathf.Max(viewH,outRect.height));
   Widgets.BeginScrollView(outRect,ref scroll,view);
   float ry=0f;
   if(coins.Count==0){Widgets.Label(new Rect(0f,0f,view.width,24f),"金库是空的。");ry=24f;}
   foreach(var t in coins.OrderBy(x=>x.def==ThingDefOf.Gold).ThenByDescending(x=>x.stackCount)){
    var row=new Rect(0f,ry,view.width,28f);if(Mouse.IsOver(row))Widgets.DrawHighlight(row);
    Widgets.ThingIcon(new Rect(row.x,row.y,28f,28f),t);
    Widgets.Label(new Rect(row.x+34f,row.y+4f,170f,24f),t.LabelCapNoCount+" ×"+t.stackCount);
    bool allowed=!t.IsForbidden(Faction.OfPlayer);bool was=allowed;
    Widgets.CheckboxLabeled(new Rect(row.x+210f,row.y+2f,90f,24f),"可用",ref allowed);
    if(allowed!=was)t.SetForbidden(!allowed,false);
    TooltipHandler.TipRegion(new Rect(row.x+210f,row.y,90f,28f),"取消勾选＝禁止：小人和交易都不会动这一堆。");
    var btn=new Rect(row.xMax-80f,row.y+2f,78f,24f);
    if(Widgets.ButtonText(btn,"取出")){if(!Bank_TakeOut(b,t))Messages.Message("银行旁边没有空地可放。",b,MessageTypeDefOf.RejectInput,false);}
    TooltipHandler.TipRegion(btn,"把这一堆放到银行旁边的空地上，并设为禁止（免得又被搬回来）。");
    ry+=30f;
   }
   viewH=ry;
   Widgets.EndScrollView();
  }

  // 放到银行外一圈第一个没有东西的空地上，设为禁止。
  public static bool Bank_TakeOut(Building_LPBank b,Thing t){
   if(b==null || t==null || !t.Spawned || !b.Spawned)return false;
   var map=b.Map;var rect=b.OccupiedRect();
   foreach(var c in GenAdj.CellsAdjacent8Way(b).Concat(rect.ExpandedBy(2).EdgeCells)){
    if(!c.InBounds(map) || rect.Contains(c) || !c.Standable(map) || c.Fogged(map))continue;
    if(c.GetThingList(map).Any(x=>x.def.category==ThingCategory.Item || x.def.category==ThingCategory.Building && !(x is Building_Door)))continue;
    t.DeSpawn();GenSpawn.Spawn(t,c,map);t.SetForbidden(true,false);return true;
   }
   return false;
  }
 }
}
