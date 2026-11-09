using System;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.136.0 热修：Test.ps1 -Only SpellPageFix（法术页 defName 以数字结尾导致读档撞号、交易窗口空引用）。
 public partial class LichChecks {
  static int spfSavedId=-1;
  void CheckSpellPageFix(){
   // 1. 新名字 A / B / C；旧名字读档自动换新；本模组没有以数字结尾的 ThingDef。
   var digit=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.modContentPack!=null && d.modContentPack.PackageId.ToLowerInvariant().Contains("lich.phylactery") && char.IsDigit(d.defName[d.defName.Length-1])).Select(d=>d.defName).ToList();
   Check(Spellbook.PageDef(1)?.defName=="LP_WS_SpellPageA" && Spellbook.PageDef(3)?.defName=="LP_WS_SpellPageC" && BackCompatibility.BackCompatibleDefName(typeof(ThingDef),"LP_WS_SpellPage2")=="LP_WS_SpellPageB" && digit.Count==0,
    "[1.136.0] spell page defs renamed to A/B/C, old names redirected, no ThingDef ends with a digit ("+string.Join(",",digit)+")");
   // 2. 旧 ID 去掉环位数字再读编号（原来会读成 1110、或者超出 int）。
   bool a=SpellPageFix.OldId("LP_WS_SpellPage1110",out int n1),b=SpellPageFix.OldId("LP_WS_SpellPage2222171039",out int n2),c=SpellPageFix.OldId("LP_WS_SpellPageA110",out _);
   Check(a && n1==110 && b && n2==222171039 && !c,"[1.136.0] old spell page IDs parse to their real numbers ("+n1+", "+n2+")");
   // 3. 地图物品清单里的残留（没生成在地图上的物品）读档时被清掉，交易统计不会再空引用。
   var stray=ThingMaker.MakeThing(Spellbook.PageDef(1));map.listerThings.Add(stray);int removed=SpellPageFix.CleanListers(map);
   Check(removed>=1 && !map.listerThings.AllThings.Contains(stray),"[1.136.0] stale unspawned entries are removed from the map's thing list ("+removed+")");
  }
  void PrepareSpellPageFixSave(){var page=Spellbook.MakePage(2,null);GenPlace.TryPlaceThing(page,map.Center,map,ThingPlaceMode.Near);spfSavedId=page.thingIDNumber;}
  void CheckSpellPageFixAfterLoad(){
   var page=map.listerThings.ThingsOfDef(Spellbook.PageDef(2)).FirstOrDefault(t=>t.thingIDNumber==spfSavedId);
   Check(page!=null && page.ThingID=="LP_WS_SpellPageB"+spfSavedId,"[1.136.0] a spell page keeps its ID through save/reload ("+page?.ThingID+")");
  }
 }
}
