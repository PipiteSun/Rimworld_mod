using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 2026-10-29：Test.ps1 -RPG -Only RPGInventory。RPG Style Inventory Revamped 联动（Patches/RPGInventory.xml）：
 // DND 槽位都在、不和别的格子重叠，每件奇物/披风/盾牌都有自己的格子（不再掉进杂项区）。没装该模组时只查补丁没留下任何槽位定义。
 public partial class LichChecks {
  const string RpgPackage="sandy.rpgstyleinventory.avilmask.revamped";
  static readonly string[] RpgSlots={"LP_ItemSlotHeadWondrous","LP_ItemSlotEyesWondrous","LP_ItemSlotNeckWondrous","LP_ItemSlotCape","LP_ItemSlotChestWondrous","LP_ItemSlotHandsWondrous","LP_ItemSlotRing","LP_ItemSlotWaistWondrous","LP_ItemSlotFocusWondrous","LP_ItemSlotWondrous"};

  void CheckRPGInventory(){
   var slotType=AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.ItemSlotDef");
   if(!ModsConfig.IsActive(RpgPackage)){
    Check(slotType==null,"RPG inventory absent: the compatibility patch adds no slot defs");
    return;
   }
   var slotsType=AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.Slots");
   Check(slotType!=null && slotsType!=null,"RPG inventory types found");
   if(slotType==null || slotsType==null)return;
   var all=((IEnumerable)typeof(DefDatabase<>).MakeGenericType(slotType).GetProperty("AllDefs").GetValue(null)).Cast<Def>().ToList();
   Func<Def,string,object> F=(d,n)=>AccessTools.Field(slotType,n).GetValue(d);
   Func<Def,List<ApparelLayerDef>> Layers=d=>(List<ApparelLayerDef>)F(d,"apparelLayers")??new List<ApparelLayerDef>();
   // 1. 槽位都在，盾牌格收 LP_Shield
   var mine=RpgSlots.Select(n=>all.FirstOrDefault(d=>d.defName==n)).ToList();
   var shield=all.FirstOrDefault(d=>d.defName=="ItemSlotShield");
   Check(mine.All(d=>d!=null) && shield!=null && Layers(shield).Any(l=>l.defName=="LP_Shield"),"RPG inventory: 10 DND slots exist and the shield slot takes LP_Shield");
   // 2. 实际格子（不算占位格）不重叠
   var real=all.Where(d=>!(bool)F(d,"placeholder")).ToList();
   var clash=real.GroupBy(d=>((int)F(d,"xPos"),(int)F(d,"yPos"))).Where(g=>g.Count()>1).Select(g=>string.Join("/",g.Select(d=>d.defName))).ToList();
   Check(clash.Count==0,"RPG inventory: no two real slots share a cell ("+string.Join(", ",clash)+")");
   // 3. 每件带本模组层的衣物都有主格子
   var display=AccessTools.Field(AccessTools.TypeByName("Sandy_Detailed_RPG_Inventory.Sandy_RPG_Settings"),"displayAllSlots");
   AccessTools.Method(slotsType,"MakePreps").Invoke(null,new object[]{display!=null && (bool)display.GetValue(null),true});
   var dict=(IDictionary)AccessTools.Field(slotsType,"dict").GetValue(null);
   Func<ThingDef,Def> Primary=t=>{var l=dict!=null && dict.Contains(t)?dict[t] as IList:null;return l!=null && l.Count>0?(Def)l[0]:null;};
   var ours=DefDatabase<ThingDef>.AllDefs.Where(t=>t.apparel!=null && t.apparel.layers.Any(l=>l.defName.StartsWith("LP_"))).ToList();
   var homeless=ours.Where(t=>Primary(t)==null).Select(t=>t.defName).ToList();
   Check(ours.Count>60 && homeless.Count==0,"RPG inventory: every apparel on a DND layer gets a slot ("+ours.Count+" defs; without: "+string.Join(",",homeless)+")");
   // 4. 各部位落在对应的格子
   var expect=new Dictionary<string,string>{{"LP_RingResistanceFire","LP_ItemSlotRing"},{"LP_AmuletHealth","LP_ItemSlotNeckWondrous"},{"LP_IounStone","LP_ItemSlotHeadWondrous"},{"LP_EyesEagle","LP_ItemSlotEyesWondrous"},{"LP_KnightMantle","LP_ItemSlotCape"},{"LP_BoneTalisman","LP_ItemSlotChestWondrous"},{"LP_SaintSeal","LP_ItemSlotChestWondrous"},{"LP_BracersArchery","LP_ItemSlotHandsWondrous"},{"LP_StabilizerBand","LP_ItemSlotWaistWondrous"},{"LP_ElementalFocus_Fire","LP_ItemSlotFocusWondrous"},{"LP_DwarvenRuneShield","ItemSlotShield"},{"LP_DeathknightPlate","ItemSlotTorsoShell"}};
   var wrong=expect.Where(e=>{var t=DefDatabase<ThingDef>.GetNamedSilentFail(e.Key);return t!=null && Primary(t)?.defName!=e.Value;}).Select(e=>e.Key+"→"+Primary(DefDatabase<ThingDef>.GetNamed(e.Key))?.defName).ToList();
   Check(wrong.Count==0,"RPG inventory: rings, amulets, ioun stones, eyes, capes, chest/hand/waist/off-hand wondrous items and the rune shield land in their own slots ("+string.Join(", ",wrong)+")");
   // 5. 兜底格没有物品时不显示
   var active=((IEnumerable)AccessTools.Field(slotsType,"activeSlots").GetValue(null)).Cast<Def>().ToList();
   Check(mine.Take(9).All(d=>active.Contains(d)) && !active.Contains(mine[9]),"RPG inventory: the nine DND slots are shown and the empty catch-all slot stays hidden");
  }
 }
}
