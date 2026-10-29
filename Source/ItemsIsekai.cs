using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.22.0 Isekai 地下城联动：开宝箱时按宝箱等级额外掉落治疗药水、炼金之火或卷轴。
 // 宝箱的奖励由 IsekaiDungeons 在 C# 里掷出（没有 XML 掉落表），所以按类型名反射挂到 TreasureChest.Award，不引用其程序集；没装就什么都不做。
 [StaticConstructorOnStartup]
 public static class IsekaiLoot {
  public const string ChestType="IsekaiDungeons.TreasureChest";
  // 宝箱等级 1~4：额外掉落的概率与件数。
  public static readonly float[] TierChance={.4f,.55f,.7f,.85f};
  public static readonly int[] TierRolls={1,1,2,3};
  public static readonly string[] CommonLoot={"LP_PotionHealing","LP_PotionHealing","LP_AlchemistFire","LP_ScrollFireball","LP_ScrollTeleport","LP_ScrollProtectionEvil","LP_ScrollTrueSeeing"};
  public static readonly string[] RareLoot={"LP_PotionGreaterHealing","LP_ScrollFleshToStone","LP_ScrollRevivify"};
  public const float LampChance=.05f,StudyChance=.1f,ArcaneChance=.05f,MagicChance=.15f;
  // 1.45.0：三级以上宝箱额外15%掉一件魔法物品，锐锋剑、复苏核心、流星戒指、不可移动之杖、魔像手册优先（各占两份）。
  public static readonly string[] MagicLoot={"LP_VorpalSword","LP_VorpalSword","LP_RevivalCore","LP_RevivalCore","LP_RingShootingStars","LP_RingShootingStars","LP_ImmovableRod","LP_ImmovableRod","LP_ManualOfGolems","LP_ManualOfGolems","LP_BagOfTricks","LP_RingProtection","LP_NecklaceAdaptation","LP_PeriaptWoundClosure","LP_HelmTelepathy","LP_EyesEagle","LP_GauntletsOgrePower","LP_HornValhalla","LP_ScarabProtection","LP_BroochShielding"};
  public static string MagicDrop(int tier,Func<float> rand){if(tier<3 || rand()>=MagicChance)return null;return MagicLoot[Math.Min(MagicLoot.Length-1,(int)(rand()*MagicLoot.Length))];}
  public static List<string> NetherilLoot(int tier,Func<float> rand){var l=new List<string>();if(rand()<StudyChance)l.Add("study");if(tier>=3 && rand()<ArcaneChance)l.Add("arcane");return l;}
  public static bool Patched{get;private set;}
  static IsekaiLoot(){
   var chest=GenTypes.GetTypeInAnyAssembly(ChestType);if(chest==null)return;
   var award=AccessTools.Method(chest,"Award",new[]{typeof(Thing)});
   if(award==null){Log.Warning("[DND边缘大陆] 找到了 Isekai 地下城宝箱，但没有 Award 方法，宝箱额外掉落已跳过。");return;}
   new Harmony("local.lich.phylactery.isekai").Patch(award,prefix:new HarmonyMethod(typeof(IsekaiLoot),nameof(AwardPrefix)));
   Patched=true;
  }
  public static int Tier(Thing chest){try{return Math.Max(1,Math.Min(4,Traverse.Create(chest).Property("Tier").GetValue<int>()));}catch{return 1;}}
  // 按等级掷出额外掉落：每次 roll 先判概率，再从普通池挑；三级以上的宝箱每次有 1/3 机会改从稀有池挑。
  public static List<string> Roll(int tier,Func<float> rand){
   var list=new List<string>();tier=Math.Max(1,Math.Min(4,tier));
   for(int i=0;i<TierRolls[tier-1];i++){
    if(rand()>=TierChance[tier-1])continue;
    var pool=tier>=3 && rand()<1f/3f?RareLoot:CommonLoot;
    list.Add(pool[Math.Min(pool.Length-1,(int)(rand()*pool.Length))]);
   }
   return list;
  }
  static void AwardPrefix(Thing __instance,Thing item){
   try{
    if(item==null || __instance==null || __instance.Destroyed)return;
    var map=__instance.MapHeld;var cell=__instance.PositionHeld;if(map==null || !cell.IsValid)return;
    foreach(var name in Roll(Tier(__instance),()=>Rand.Value)){
     var def=Items.Thing(name);if(def==null)continue;
     var t=ThingMaker.MakeThing(def);GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);
     Messages.Message("宝箱底层还压着一件费伦的物品："+t.LabelCap+"。",t,MessageTypeDefOf.PositiveEvent,false);
    }
    // 1.29.0：2 级以上的宝箱可能有 DND 枪械或法杖，4 级以上可能有噩耗或天火降世（按宝箱编号播种）。
    // 生成武器（品质、艺术描述）用独立的随机数状态，不影响后面神灯的概率。
    var guns=GunLoot.ForChest(__instance);
    if(guns.Count>0){
     Rand.PushState(__instance.thingIDNumber^0x4756);
     try{
      foreach(var name in guns){
       var def=Items.Thing(name);if(def==null)continue;
       var t=ThingMaker.MakeThing(def,GenStuff.DefaultStuffFor(def));t.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityReward(),ArtGenerationContext.Outsider);
       GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);
       Messages.Message("宝箱里还躺着一件费伦的魔法武器："+t.LabelCap+"。",t,MessageTypeDefOf.PositiveEvent,false);
      }
     }finally{Rand.PopState();}
    }
    // 1.37.0：耐瑟瑞尔卷轴——研习卷 10%，3 级以上宝箱秘术残卷 5%（卡苏斯之卷不进宝箱）。
    // 用按宝箱编号播种的独立随机数，不影响后面神灯的概率。
    List<string> netheril;Rand.PushState(__instance.thingIDNumber^0x4E45);try{netheril=NetherilLoot(Tier(__instance),()=>Rand.Value);}finally{Rand.PopState();}
    foreach(var scroll in netheril){var t=scroll=="arcane"?Netheril.MakeArcane():Netheril.MakeStudy();if(t==null)continue;t.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityReward(),ArtGenerationContext.Outsider);GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);Messages.Message("宝箱里藏着一卷耐瑟瑞尔卷轴："+t.LabelCap+"。",t,MessageTypeDefOf.PositiveEvent,false);}
    // 1.45.0：三级以上宝箱额外15%掉一件魔法物品（按宝箱编号播种）。
    string magic;Rand.PushState(__instance.thingIDNumber^0x4D41);try{magic=MagicDrop(Tier(__instance),()=>Rand.Value);}finally{Rand.PopState();}
    if(magic!=null && Items.Thing(magic) is ThingDef md){var t=ThingMaker.MakeThing(md,md.MadeFromStuff?GenStuff.DefaultStuffFor(md):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityReward(),ArtGenerationContext.Outsider);GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);Messages.Message("宝箱的暗格里藏着一件魔法物品："+t.LabelCap+"。",t,MessageTypeDefOf.PositiveEvent,false);}
    // 1.58.3：三级以上宝箱 3% 一本属性典籍。
    // 1.63.0：典籍和神灯也按宝箱编号播种（原来用全局随机数，-Isekai 宝箱掉落检查时好时坏）。
    float tomeRoll,lampRoll;Rand.PushState(__instance.thingIDNumber^0x544D);try{tomeRoll=Rand.Value;lampRoll=Rand.Value;}finally{Rand.PopState();}
    if(Tier(__instance)>=3 && tomeRoll<Growth.TomeIsekai){var tm=Growth.MakeTome();if(tm!=null){GenPlace.TryPlaceThing(tm,cell,map,ThingPlaceMode.Near);Messages.Message("宝箱底层压着一本属性典籍："+tm.LabelCap+"。",tm,MessageTypeDefOf.PositiveEvent,false);}}
    // 1.24.0：每个宝箱5%概率压着一盏神灯。
    if(lampRoll<LampChance && Items.Thing("LP_WonderLamp") is ThingDef lamp){var t=ThingMaker.MakeThing(lamp);GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);Messages.Message("宝箱最深处躺着一盏古旧的神灯！",t,MessageTypeDefOf.PositiveEvent,false);}
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] Isekai 宝箱联动出错："+e,9127343);}
  }
 }
}
