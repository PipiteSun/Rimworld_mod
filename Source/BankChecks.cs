using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.142.0：Test.ps1 -Only Bank（银行，11 条 + 读档 1 条）。
 public partial class LichChecks {
  static string bankId;
  readonly List<Thing> bankThings=new List<Thing>();
  Building_LPBank BankSpawn(){
   var d=DefDatabase<ThingDef>.GetNamed("LP_BankVault");
   var cell=map.AllCells.Where(c=>c.x>16 && c.z>16 && c.x<map.Size.x-16 && c.z<map.Size.z-16 && CellRect.CenteredOn(c,4).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetFirstPawn(map)==null && x.GetFirstBuilding(map)==null && x.GetFirstItem(map)==null)).OrderBy(c=>c.DistanceToSquared(map.Center)).Skip(bankThings.Count*40).First();
   var b=(Building_LPBank)ThingMaker.MakeThing(d);b.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(b,cell,map,Rot4.South);bankThings.Add(b);
   b.settings.Priority=StoragePriority.Critical;return b;
  }
  // 往一格里放 n 堆满堆（500）的银或金；返回放进去的堆数。
  int BankPut(IntVec3 c,ThingDef d,int n,int count=500){
   int put=0;for(int i=0;i<n;i++){var t=ThingMaker.MakeThing(d);t.stackCount=count;if(GenPlace.TryPlaceThing(t,c,map,ThingPlaceMode.Direct))put++;else t.Destroy();}
   return put;
  }
  int CoinsAround(CellRect r,ThingDef d)=>r.ExpandedBy(4).ClipInsideMap(map).Cells.Sum(c=>c.GetThingList(map).Where(t=>t.def==d).Sum(t=>t.stackCount));
  void CheckBank(){
   try{CheckBankInner();}
   finally{foreach(var t in bankThings)if(!t.Destroyed){var r=t.OccupiedRect();t.Destroy();foreach(var c in r.ExpandedBy(4).ClipInsideMap(map))foreach(var x in c.GetThingList(map).Where(x=>Building_LPBank.IsCoin(x.def)).ToList())x.Destroy();}bankThings.Clear();}
  }
  void CheckBankInner(){
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("LP_BankVault");var res=DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LP_Banking");
   // 1. def：名字不以数字结尾、2×2、每格 20、造价、耐久、不可燃、研究在 DND 页、贴图都在。
   {bool tex=new[]{"BankVault_south","BankVault_Front","BankVault_Icon"}.All(n=>ContentFinder<Texture2D>.Get("LP/Bank/"+n,false)!=null) && BankArt.Front!=BaseContent.BadMat && !def.rotatable;
    bool cost=def!=null && def.CostList.Any(c=>c.thingDef==ThingDefOf.Steel && c.count==200) && def.CostList.Any(c=>c.thingDef==ThingDefOf.Plasteel && c.count==50) && def.CostList.Any(c=>c.thingDef==ThingDefOf.Gold && c.count==20) && def.CostList.Any(c=>c.thingDef==ThingDefOf.ComponentIndustrial && c.count==4);
    bool ok=def!=null && res!=null && !char.IsDigit(def.defName.Last()) && !char.IsDigit(res.defName.Last()) && def.size==new IntVec2(2,2) && def.building.maxItemsInCell==20 && cost && Near(def.GetStatValueAbstract(StatDefOf.MaxHitPoints),1500f) && Near(def.GetStatValueAbstract(StatDefOf.Flammability),0f) && def.researchPrerequisites.Contains(res) && res.tab?.defName==ResearchTab.Tab && Near(res.baseCost,1200f) && res.prerequisites.Any(p=>p.defName=="ComplexFurniture") && def.label=="银行" && res.label=="银行业";
    Check(ok && tex,"[1.142.0] 银行 LP_BankVault: 2×2, 20 per cell, steel 200 / plasteel 50 / gold 20 / components 4, 1500 HP, not flammable, research 银行业 (1200, after complex furniture) in the DND tab; body / glass front / icon textures load, not rotatable ("+tex+")");}
   var b=BankSpawn();var cells=b.OccupiedRect().Cells.ToList();
   // 2. 过滤器只收银和金。
   {var steel=ThingMaker.MakeThing(ThingDefOf.Steel);var jade=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Jade"));var silver=ThingMaker.MakeThing(ThingDefOf.Silver);var gold=ThingMaker.MakeThing(ThingDefOf.Gold);
    bool ok=b.Accepts(silver) && b.Accepts(gold) && !b.Accepts(steel) && !b.Accepts(jade) && b.settings.filter.AllowedDefCount==2;
    var fixedF=b.GetParentStoreSettings().filter;bool locked=fixedF.Allows(ThingDefOf.Silver) && fixedF.Allows(ThingDefOf.Gold) && !fixedF.Allows(ThingDefOf.Steel);
    foreach(var t in new[]{steel,jade,silver,gold})t.Destroy();
    Check(ok && locked,"[1.142.0] bank accepts silver and gold only (steel / jade refused), and the fixed filter can't be widened");}
   // 3. 一格放满 20 堆，第 21 堆放不进。
   {int put=BankPut(cells[0],ThingDefOf.Silver,21);int max=cells[0].GetMaxItemsAllowedInCell(map);
    Check(put==20 && max==20 && cells[0].GetThingList(map).Count(t=>t.def==ThingDefOf.Silver)==20,"[1.142.0] one cell holds "+put+" full silver stacks (max "+max+"), the 21st is refused");}
   // 4. 搬运：地上的银会被安排搬进银行。
   {var pawn=map.mapPawns.FreeColonistsSpawned.First();var loose=ThingMaker.MakeThing(ThingDefOf.Silver);loose.stackCount=37;
    var spot=CellFinder.StandableCellNear(b.Position+new IntVec3(0,0,-4),map,3f,c=>c.GetFirstItem(map)==null && !b.OccupiedRect().Contains(c));GenSpawn.Spawn(loose,spot,map);bankThings.Add(loose);
    bool found=StoreUtility.TryFindBestBetterStoreCellFor(loose,pawn,map,StoragePriority.Unstored,Faction.OfPlayer,out var dest,true);
    var job=HaulAIUtility.HaulToStorageJob(pawn,loose,false);
    Check(found && b.OccupiedRect().Contains(dest) && dest!=cells[0] && job!=null,"[1.142.0] loose silver gets a haul job into the bank (cell "+dest+", the full cell is skipped)");
    loose.Destroy();}
   // 5. 档位：0 / 3 / 9 / 15 / 20 堆 → 空 / 少 / 半 / 快满 / 满；满格的叠层是「满」。
   {int[] want={0,1,2,3,6};int[] got={Building_LPBank.IconCount(0),Building_LPBank.IconCount(3),Building_LPBank.IconCount(9),Building_LPBank.IconCount(20),Building_LPBank.IconCount(40)};
    b.Refresh(null,null,false);var c0=b.ColumnIcons(0);var c1=b.ColumnIcons(1);var full=BankArt.ItemMat(ThingDefOf.Silver,500);
    Check(want.SequenceEqual(got) && c0.Length==3 && c0.All(x=>x.def==ThingDefOf.Silver && x.mat==full && x.mat!=BaseContent.BadMat) && c1.Length==0,"[1.142.0] glass-case icons: 0/3/9/20/40 stacks → "+string.Join("/",got)+"; a column with 20 full silver stacks shows "+c0.Length+" vanilla silver pictures, the empty column none");}
   // 6. 混放格同时有银、金两个半格叠层；存金加美观。
   {BankPut(cells[1],ThingDefOf.Silver,3);BankPut(cells[1],ThingDefOf.Gold,9);BankPut(cells[2],ThingDefOf.Gold,6,500);b.Refresh(null,null,false);var c0=b.ColumnIcons(0);var c1=b.ColumnIcons(1);
    float beauty=b.GetStatValue(StatDefOf.Beauty);
    int g0=c0.Count(x=>x.def==ThingDefOf.Gold),s0=c0.Count(x=>x.def==ThingDefOf.Silver),g1=c1.Count(x=>x.def==ThingDefOf.Gold),s1=c1.Count(x=>x.def==ThingDefOf.Silver);
    Check(s0==3 && g0==1 && s1==1 && g1==1 && c0.Where(x=>x.def==ThingDefOf.Gold).All(x=>x.mat==BankArt.ItemMat(ThingDefOf.Gold,500)) && Near(b.GoldBeauty,6f) && beauty>=11.9f,"[1.142.0] mixed: left case "+s0+" silver + "+g0+" gold pictures (20 + 6 stacks), right case "+s1+" + "+g1+" (3 + 9); "+b.Count(ThingDefOf.Gold)+" gold → beauty "+beauty.ToString("0.#")+" (6 + 6)");}
   // 7. 交易 / 取材：银行里的银算「在储存里」，小人摸得到，按区域搜物品也找得到。
   {var pawn=map.mapPawns.FreeColonistsSpawned.First();var s=cells[0].GetThingList(map).First(x=>x.def==ThingDefOf.Silver);
    bool reach=map.reachability.CanReach(pawn.Position,s,PathEndMode.Touch,TraverseMode.PassDoors,Danger.Some);
    var near=GenClosest.ClosestThingReachable(pawn.Position,map,ThingRequest.ForDef(ThingDefOf.Silver),PathEndMode.ClosestTouch,TraverseParms.For(pawn),9999f,x=>Building_LPBank.InBank(x));
    string sum=b.Summary();
    Check(s.IsInAnyStorage() && reach && near!=null && TradeUtility.PlayerSellableNow(s,pawn) && sum.Contains("银 11,500") && sum.Contains("库存 38/80") && b.LabelMouseover.Contains("总价值"),"[1.142.0] bank silver is in storage, reachable for trade and found by item search; summary 「"+sum+"」");}
   // 8. 银行里的钱不受伤害；地上的不保护（钩子挂上）。
   {var s=cells[0].GetThingList(map).First(x=>x.def==ThingDefOf.Silver);int before=b.Count(ThingDefOf.Silver);
    var r=s.TakeDamage(new DamageInfo(DamageDefOf.Bomb,500f));s.TakeDamage(new DamageInfo(DamageDefOf.Flame,500f));
    bool hooked=Harmony.GetPatchInfo(AccessTools.Method(typeof(Thing),nameof(Thing.TakeDamage)))?.Prefixes?.Any(p=>p.PatchMethod.DeclaringType==typeof(BankProtect))==true;
    Check(!s.Destroyed && b.Count(ThingDefOf.Silver)==before && r.totalDamageDealt==0f && hooked && Building_LPBank.InBank(s),"[1.142.0] bomb / flame damage on bank silver does nothing (silver "+before+" → "+b.Count(ThingDefOf.Silver)+")");}
   // 9. 隐藏：银行格里的银 / 金不印、不显示标签、点不中，悬停读数只剩银行。
   {var s=cells[0].GetThingList(map).First(x=>x.def==ThingDefOf.Silver);
    bool sel=ThingSelectionUtility.SelectableByMapClick(s);var readout=BankReadout.Things(cells[0],map);
    var tp=AccessTools.Method(typeof(SectionLayer_ThingsGeneral),"TakePrintFrom");bool printHook=Harmony.GetPatchInfo(tp)?.Prefixes?.Any(p=>p.PatchMethod.DeclaringType==typeof(BankHidePrint))==true;
    bool readHook=Harmony.GetPatchInfo(AccessTools.Method(typeof(MouseoverReadout),nameof(MouseoverReadout.MouseoverReadoutOnGUI)))?.Transpilers?.Any(p=>p.PatchMethod.DeclaringType==typeof(BankReadout))==true;
    Find.Selector.ClearSelection();Find.Selector.Select(b,false,false);string sd="CommandSelectStoredThingDesc".Translate().ToString();int selButtons=b.GetGizmos().Count(g=>g is Command_Action c && c.defaultDesc!=null && c.defaultDesc.StartsWith(sd));Find.Selector.ClearSelection();
    Check(selButtons==0 && !sel && ThingSelectionUtility.SelectableByMapClick(b) && readout.Count==1 && readout[0]==b && printHook && readHook,"[1.142.0] coins inside are hidden: no per-stack select buttons, not click-selectable, mouseover lists only the bank ("+readout.Count+" line), print / readout hooks in place");}
   // 10. 「取出」：放到旁边空地并禁止；拆除后钱全部还在（就近落地）。
   {var g=cells[2].GetThingList(map).First(x=>x.def==ThingDefOf.Gold);int goldBefore=CoinsAround(b.OccupiedRect(),ThingDefOf.Gold),silverBefore=CoinsAround(b.OccupiedRect(),ThingDefOf.Silver);
    bool took=ITab_BankLedger.Bank_TakeOut(b,g);bool outside=g.Spawned && !b.OccupiedRect().Contains(g.Position) && g.IsForbidden(Faction.OfPlayer);
    var rect=b.OccupiedRect();b.Destroy(DestroyMode.Deconstruct);
    int goldAfter=CoinsAround(rect,ThingDefOf.Gold),silverAfter=CoinsAround(rect,ThingDefOf.Silver);
    bool spread=rect.ExpandedBy(4).ClipInsideMap(map).Cells.All(c=>c.GetThingList(map).Count(t=>Building_LPBank.IsCoin(t.def))<=1);
    Check(took && outside && goldAfter>=goldBefore && goldAfter-goldBefore<=20 && silverAfter==silverBefore && spread,"[1.142.0] 取出 puts a stack outside, forbidden; after deconstructing, silver "+silverBefore+" → "+silverAfter+", gold "+goldBefore+" → "+goldAfter+" (incl. the build-cost refund), one stack per cell");}
  }
  void PrepareBankSave(){
   var b=BankSpawn();var cells=b.OccupiedRect().Cells.ToList();BankPut(cells[0],ThingDefOf.Silver,7);BankPut(cells[3],ThingDefOf.Gold,13);BankPut(cells[3],ThingDefOf.Silver,2);
   bankId=b.ThingID;bankThings.Remove(b);
  }
  void CheckBankAfterLoad(){
   var b=map.listerThings.AllThings.OfType<Building_LPBank>().FirstOrDefault(x=>x.ThingID==bankId);
   var c0=b?.ColumnIcons(0);var c1=b?.ColumnIcons(1);
   Check(b!=null && b.StackCount==22 && b.Count(ThingDefOf.Silver)==4500 && b.Count(ThingDefOf.Gold)==6500 && c0.Length==1 && c0[0].def==ThingDefOf.Silver && c1.Length==3 && c1.Count(x=>x.def==ThingDefOf.Gold)==2,"[1.142.0] after load the bank still holds 22 stacks (silver "+b?.Count(ThingDefOf.Silver)+", gold "+b?.Count(ThingDefOf.Gold)+") and the same case pictures ("+c0?.Length+" + "+c1?.Length+")");
   if(b!=null){var r=b.OccupiedRect();b.Destroy();foreach(var c in r.ExpandedBy(4).ClipInsideMap(map))foreach(var x in c.GetThingList(map).Where(x=>Building_LPBank.IsCoin(x.def)).ToList())x.Destroy();}
  }
 }
}
namespace LichPhylactery {
 // 1.142.0 银行截图：Test.ps1 -Only BankShot（三座银行：混放 / 堆满 / 空），截两张：地图 + 「金库清单」。
 public partial class LichChecks {
  void CheckBankShot(){}
  static int bankFrames;static Building_LPBank bankShot;static Vector3 bankCam;
  static bool CaptureBank(){
   if(bankFrames==0)return false;bankFrames++;var map=Find.CurrentMap;string dir=GenFilePaths.SaveDataFolderPath;
   try{
    if(bankFrames==10){
     var d=DefDatabase<ThingDef>.GetNamed("LP_BankVault");
     var origin=map.AllCells.Where(c=>c.x>20 && c.z>20 && c.x<map.Size.x-20 && c.z<map.Size.z-20 && new CellRect(c.x-1,c.z-1,14,6).Cells.All(x=>x.Standable(map) && !x.Fogged(map) && x.GetFirstBuilding(map)==null && x.GetFirstPawn(map)==null)).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
     foreach(var x in new CellRect(origin.x-1,origin.z-1,14,6).Cells)foreach(var t in x.GetThingList(map).Where(t=>t.def.category==ThingCategory.Item || t is Plant).ToList())t.Destroy();
     Building_LPBank Make(int dx,Rot4 rot){var b=(Building_LPBank)ThingMaker.MakeThing(d);b.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(b,origin+new IntVec3(dx,0,0),map,rot);return b;}
     void Put(IntVec3 c,ThingDef td,int n){for(int i=0;i<n;i++){var t=ThingMaker.MakeThing(td);t.stackCount=500;GenPlace.TryPlaceThing(t,c,map,ThingPlaceMode.Direct);}}
     var a=Make(0,Rot4.South);var ca=a.OccupiedRect().Cells.ToList();Put(ca[0],ThingDefOf.Silver,3);Put(ca[1],ThingDefOf.Gold,9);Put(ca[2],ThingDefOf.Silver,15);Put(ca[3],ThingDefOf.Silver,4);Put(ca[3],ThingDefOf.Gold,8);
     var f=Make(3,Rot4.South);var cf=f.OccupiedRect().Cells.ToList();Put(cf[0],ThingDefOf.Silver,20);Put(cf[1],ThingDefOf.Gold,20);Put(cf[2],ThingDefOf.Gold,14);Put(cf[3],ThingDefOf.Silver,7);
     Make(6,Rot4.South);
     var loose=ThingMaker.MakeThing(ThingDefOf.Silver);loose.stackCount=300;GenSpawn.Spawn(loose,origin+new IntVec3(9,0,0),map);
     foreach(var b in map.listerThings.AllThings.OfType<Building_LPBank>())b.Refresh(null,null,true);
     bankShot=a;bankCam=origin.ToVector3Shifted()+new Vector3(4.5f,0f,-1.5f);Find.CameraDriver.SetRootPosAndSize(bankCam,9f);
     Find.Selector.ClearSelection();Find.Selector.Select(a);Messages.Clear();
    }
    if(bankFrames==25)Find.CameraDriver.SetRootPosAndSize(bankCam,9f);
    if(bankFrames==40)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"Bank-Map.png"));
    if(bankFrames==50){var w=(MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect,false);InspectPaneUtility.OpenTab(typeof(ITab_BankLedger));}
    if(bankFrames==80)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"Bank-Ledger.png"));
    if(bankFrames>=95){Log.Message("[LICH-UI] bank captured");Application.Quit();}
   }catch(Exception e){Log.Error("[LICH-UI] bank capture: "+e);Application.Quit();}
   return true;
  }
 }
}
