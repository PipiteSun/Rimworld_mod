using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.22.0 新材料：熔岩结晶（火巨人锻造副产物）、龙鳞蜕、巨魔皮、秘银、精金、寒铁。
 public static class ItemMaterials {
  public const float LavaChance=.15f,TrollHideRepair=.01f,AdamantineFactor=2f,AdamantineArmorPen=.5f,ColdIronFactor=1.25f;
  public const int ShedPerAge=3,ShedDays=60;
  public static ThingDef Lava=>Items.Thing("LP_LavaCrystal");
  public static ThingDef Shed=>Items.Thing("LP_DragonShed");
  public static ThingDef TrollHide=>Items.Thing("LP_TrollHide");
  public static ThingDef Mithral=>Items.Thing("LP_Mithral");
  public static ThingDef Adamantine=>Items.Thing("LP_Adamantine");
  public static ThingDef ColdIron=>Items.Thing("LP_ColdIron");

  // 火巨人在熔火锻炉完成作品：roll<15% 多出1块熔岩结晶。
  public static bool LavaFrom(Pawn worker,Thing bench,float roll)=>bench?.def?.defName=="LP_FireGiantForge" && FireGiant.Active(worker) && roll<LavaChance;
  // 力能龙蜕鳞：放在龙身边（在宝库里就在宝库里）。
  public static Thing DropShed(Pawn dragon,int count){
   if(dragon?.MapHeld==null || Shed==null || count<=0)return null;
   var t=ThingMaker.MakeThing(Shed);t.stackCount=count;GenPlace.TryPlaceThing(t,dragon.PositionHeld,dragon.MapHeld,ThingPlaceMode.Near);
   if(dragon.Faction==Faction.OfPlayer)Messages.Message(dragon.LabelShort+"蜕下了"+count+"片龙鳞。",t,MessageTypeDefOf.PositiveEvent,false);
   return t;
  }
  // 卖掉龙鳞蜕：本殖民地所有力能龙记仇（“龙鳞被卖”心情；1.23.3 起宝库被拿走东西不再扣心情）。
  public static List<Pawn> ShedSold(){
   var dragons=PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Where(ForceDragon.Active).ToList();
   if(dragons.Count>0)DragonHoard.Notify_ShedSold(dragons);
   return dragons;
  }
  // 巨魔皮服装：每天恢复1%耐久（地图上的物品和所有人身上穿的）。
  public static int DailyRepair(){
   int n=0;var hide=TrollHide;if(hide==null || Find.Maps==null)return 0;
   foreach(var map in Find.Maps){
    foreach(var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel).ToList())if(Repair(t))n++;
    foreach(var p in map.mapPawns.AllPawns)if(p.apparel!=null)foreach(var a in p.apparel.WornApparel)if(Repair(a))n++;
   }
   return n;
  }
  public static bool Repair(Thing t){
   if(t==null || t.Stuff!=TrollHide || !t.def.useHitPoints || t.HitPoints>=t.MaxHitPoints)return false;
   t.HitPoints=Math.Min(t.MaxHitPoints,t.HitPoints+Math.Max(1,Mathf.RoundToInt(t.MaxHitPoints*TrollHideRepair)));return true;
  }
  // 精金护甲：穿着它时受到的穿甲减半。返回值是伤害倍率（这里不改伤害量）。
  // 1.45.0：只算真正的护甲层（躯干/头部的贴身、中层、外层、头盔），奇物层、腰带和珠宝饰品层用精金做也不算。
  static AccessTools.StructFieldRef<DamageInfo,float> armorPen;
  public static bool ArmorLayer(Apparel a){var ap=a?.def.apparel;if(ap?.layers==null)return false;foreach(var l in ap.layers)if(l==ApparelLayerDefOf.OnSkin || l==ApparelLayerDefOf.Middle || l==ApparelLayerDefOf.Shell || l==ApparelLayerDefOf.Overhead)return true;return false;}
  public static float ArmorFactor(Pawn p,ref DamageInfo dinfo){
   var worn=p?.apparel?.WornApparel;if(worn==null || Adamantine==null)return 1f;
   for(int i=0;i<worn.Count;i++)if(worn[i].Stuff==Adamantine && ArmorLayer(worn[i])){
    if(armorPen==null)armorPen=AccessTools.StructFieldRefAccess<DamageInfo,float>("armorPenetrationInt");
    armorPen(ref dinfo)*=AdamantineArmorPen;break;
   }
   return 1f;
  }
  // 武器材料：精金近战对建筑和机械体×2；寒铁对灵能链接者、精灵（及联动登记的种族）×1.25。
  public static float WeaponFactor(Thing target,DamageInfo dinfo){
   if(!(dinfo.Instigator is Pawn p) || dinfo.Def==null || dinfo.Def.isRanged)return 1f;
   var weapon=p.equipment?.Primary;if(weapon==null || dinfo.Weapon!=weapon.def || !weapon.def.IsMeleeWeapon)return 1f;
   // 1.45.0 龙杀手：对龙 ×1.5。
   float dragon=MagicEffects.DragonFactor(weapon.def,target);if(dragon!=1f)return dragon;
   if(weapon.Stuff==Adamantine && Adamantine!=null && (target is Building || (target is Pawn mech && mech.RaceProps.IsMechanoid)))return AdamantineFactor;
   // 1.24.0：寒铁对魔鬼（九狱之躯）×1.5。
   // 1.54.0：寒铁 / 银对外来者读 LP_OutsiderExtension 的倍率（九狱之躯 寒铁 ×1.5、银 ×1.5；深渊之躯 寒铁 ×1.5）。
   if(weapon.Stuff!=null && target is Pawn op){var oe=Outsiders.Of(op);if(oe!=null){if(weapon.Stuff==ColdIron && ColdIron!=null && oe.coldIron!=1f)return oe.coldIron;if(weapon.Stuff==ThingDefOf.Silver && oe.silver!=1f)return oe.silver;}}
   if(weapon.Stuff==ColdIron && ColdIron!=null && target is Pawn victim && ColdIronBane(victim))return ColdIronFactor;
   return 1f;
  }
  public static bool ColdIronBane(Pawn p){
   if(p==null)return false;
   if(p.HasPsylink || Items.IsElf(p))return true;
   foreach(var hook in Items.ColdIronBaneHooks){try{if(hook(p))return true;}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 寒铁联动出错："+e,9127342);}}
   return false;
  }
  // 秘银或精灵锁子甲：抵消服装的移速（和工作速度）惩罚。
  public static bool NoPenalty(Thing gear)=>gear!=null && (gear.Stuff!=null && gear.Stuff==Mithral || gear.def.defName=="LP_ElvenChain");
 }
 // 秘银服装与精灵锁子甲：服装自带的移速、工作速度惩罚归零（直接改原版“装备属性加成”的计算结果）。
 [HarmonyPatch(typeof(StatWorker),nameof(StatWorker.StatOffsetFromGear))]
 public static class NoArmorPenalty {
  static void Postfix(Thing gear,StatDef stat,ref float __result){if(__result<0 && (stat==StatDefOf.MoveSpeed || stat==StatDefOf.WorkSpeedGlobal) && ItemMaterials.NoPenalty(gear))__result=0;}
 } // 熔岩结晶：火巨人在熔火锻炉完成作品时 15% 额外产出1块。
 [HarmonyPatch(typeof(GenRecipe),nameof(GenRecipe.MakeRecipeProducts))]
 public static class LavaCrystalByproduct {
  static IEnumerable<Thing> Postfix(IEnumerable<Thing> values,Pawn worker,IBillGiver billGiver){
   foreach(var t in values)yield return t;
   if(ItemMaterials.Lava!=null && ItemMaterials.LavaFrom(worker,billGiver as Thing,Rand.Value)){var lava=ThingMaker.MakeThing(ItemMaterials.Lava);lava.stackCount=1;yield return lava;}
  }
 }
 // 巨魔皮：屠宰巨魔（按基因）得到巨魔皮而不是人皮。
 [HarmonyPatch(typeof(Corpse),nameof(Corpse.ButcherProducts))]
 public static class TrollHideButcher {
  static IEnumerable<Thing> Postfix(IEnumerable<Thing> values,Corpse __instance){
   var p=__instance?.InnerPawn;bool troll=Troll.Is(p) && ItemMaterials.TrollHide!=null;var leather=p?.RaceProps?.leatherDef;
   foreach(var t in values){
    if(troll && t!=null && leather!=null && t.def==leather){var hide=ThingMaker.MakeThing(ItemMaterials.TrollHide);hide.stackCount=t.stackCount;t.Destroy();yield return hide;}
    else yield return t;
   }
  }
 }
 // 力能龙：自然晋升一级龙龄蜕下3片龙鳞；在宝库里每住满60天蜕1片。
 [HarmonyPatch(typeof(Gene_ForceDragonHeart),nameof(Gene_ForceDragonHeart.TryAdvance))]
 public static class DragonShedOnAge {static void Postfix(Gene_ForceDragonHeart __instance,bool __result){if(__result)ItemMaterials.DropShed(__instance.pawn,ItemMaterials.ShedPerAge);}}
 [HarmonyPatch(typeof(Gene_ForceDragonHeart),nameof(Gene_ForceDragonHeart.Hourly))]
 public static class DragonShedOnHoard {
  static void Prefix(Gene_ForceDragonHeart __instance,out float __state){__state=__instance.daysOnHoard;}
  static void Postfix(Gene_ForceDragonHeart __instance,float __state){int before=(int)(__state/ItemMaterials.ShedDays),after=(int)(__instance.daysOnHoard/ItemMaterials.ShedDays);if(after>before)ItemMaterials.DropShed(__instance.pawn,after-before);}
 }
 [HarmonyPatch]
 public static class DragonShedSale {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){
   yield return AccessTools.Method(typeof(Pawn_TraderTracker),nameof(Pawn_TraderTracker.GiveSoldThingToTrader));
   var ship=AccessTools.Method(typeof(TradeShip),"GiveSoldThingToTrader");if(ship!=null)yield return ship;
  }
  static void Prefix(Thing __0){if(__0!=null && __0.def==ItemMaterials.Shed && ItemMaterials.Shed!=null)ItemMaterials.ShedSold();}
 }
 // 矮人开采秘银：额外再得一份同样的产量。
 [HarmonyPatch(typeof(Mineable),"TrySpawnYield",new[]{typeof(Map),typeof(bool),typeof(Pawn)})]
 public static class DwarfMithralYield {
  static readonly AccessTools.FieldRef<Mineable,float> yieldPct=AccessTools.FieldRefAccess<Mineable,float>("yieldPct");
  static void Postfix(Mineable __instance,Map __0,Pawn __2){
   var b=__instance.def.building;if(b?.mineableThing==null || b.mineableThing!=ItemMaterials.Mithral || !CommonRaces.IsDwarf(__2) || __0==null)return;
   int n=Math.Max(1,b.EffectiveMineableYield);if(b.mineableYieldWasteable)n=Math.Max(1,GenMath.RoundRandom(n*yieldPct(__instance)));
   var t=ThingMaker.MakeThing(b.mineableThing);t.stackCount=n;GenPlace.TryPlaceThing(t,__instance.Position,__0,ThingPlaceMode.Near);
  }
 }
}
