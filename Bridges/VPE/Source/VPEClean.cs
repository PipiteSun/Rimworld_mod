using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;

namespace LichPhylactery.VPEBridge {
 // 1.99.1 VPE 里不再出现 DND 内容（职业已经是冒险者分页的独立体系）：
 // · VPE 给每个灵能自动生成的神经成型器里，79 个旧职业能力（“（旧）”）的那份：不再出现在商人、奖励和物品筛选里（定义保留，老存档里的照样读得进来，用不了）；
 // · 新生成的人（VPE 给 NPC 随机配灵能时）如果带上了旧路径 / 旧能力，当场去掉。“DND职业”页签的隐藏在 Martial.cs（HideMovedPaths）。
 [StaticConstructorOnStartup]
 public static class VPEClean {
  public static readonly List<ThingDef> Psytrainers=new List<ThingDef>();
  static VPEClean(){
   // 灵能页签在 ThingDef.ResolveReferences 时就由 InspectTabManager 建好共享实例，比本程序集打 Harmony 补丁早，
   // 构造函数补丁对它不起作用（游戏里还看得到“DND职业”页签）：这里对已建好的共享实例补做一次隐藏。
   var tabType=HideMovedPaths.TabType;
   if(tabType!=null)HideMovedPaths.Postfix(InspectTabManager.GetSharedInstance(tabType));
   foreach(var d in DefDatabase<ThingDef>.AllDefsListForReading){
    if(d.comps==null || !d.comps.OfType<CompProperties_UseEffectGiveAbility>().Any(c=>ClassMigration.Hidden(c.ability)))continue;
    Psytrainers.Add(d);
    d.tradeability=Tradeability.None;d.tradeTags=null;d.thingSetMakerTags=null;d.generateAllowChance=0f;
    if(d.thingCategories!=null){foreach(var c in d.thingCategories)c?.childThingDefs?.Remove(d);d.thingCategories.Clear();}
   }
   if(Psytrainers.Count==0)return;
   var set=new HashSet<ThingDef>(Psytrainers);
   ThingSetMakerUtility.allGeneratableItems?.RemoveAll(set.Contains);
   foreach(var c in DefDatabase<ThingCategoryDef>.AllDefsListForReading){var cache=Traverse.Create(c).Field("allChildThingDefsCached").GetValue<HashSet<ThingDef>>();cache?.RemoveWhere(set.Contains);}
  }
  // 去掉一个人身上的旧路径 / 旧能力，不换算职业等级（读档迁移见 ClassMigration）；返回去掉的个数。
  public static int Strip(Pawn p){
   var comp=p?.GetComp<CompAbilities>();var psy=Classes.Psycasts(p);int n=0;
   if(comp!=null)n+=comp.LearnedAbilities.RemoveAll(a=>ClassMigration.Hidden(a.def));
   if(psy?.unlockedPaths!=null){int k=psy.unlockedPaths.RemoveAll(ClassMigration.Hidden);psy.points+=k;n+=k;}
   return n;
  }
 }
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class VPEClean_Generate {
  static void Postfix(Pawn __result){if(__result?.RaceProps?.Humanlike==true)VPEClean.Strip(__result);}
 }
}
