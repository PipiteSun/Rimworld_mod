using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;

namespace LichPhylactery.VPEBridge {
 // 1.99.1（-VPE -Only VPEClean）：VPE 里不再出现 DND 内容——旧能力的神经成型器不再流通，新生成的人身上没有旧路径 / 旧能力。
 public static partial class BridgeChecks {
  static void VPECleanChecks(){
   // 游戏里真正显示的是 InspectTabManager 的共享实例（人形 ThingDef 的 inspectorTabsResolved 里那个），不是新建的。
   var shared=InspectTabManager.GetSharedInstance(HideMovedPaths.TabType);
   var byTab=Traverse.Create(shared).Field("pathsByTab").GetValue<System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<PsycasterPathDef>>>();
   var tabs=Traverse.Create(shared).Field("tabs").GetValue<System.Collections.Generic.List<TabRecord>>();
   bool onHuman=ThingDefOf.Human.inspectorTabsResolved?.Contains(shared)==true;
   Check(byTab!=null && byTab.Count>0 && !byTab.ContainsKey("DND职业") && !byTab.Values.SelectMany(l=>l).Any(ClassMigration.Hidden) && (tabs==null || !tabs.Any(t=>t.label=="DND职业")) && onHuman,"vpe clean: the live psycasts tab (shared instance on Human) has no DND职业 page or DND paths ("+(byTab==null?"null":string.Join("/",byTab.Keys))+")");
   var hiddenTrainers=VPEClean.Psytrainers;
   var allTrainers=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.comps!=null && d.comps.OfType<CompProperties_UseEffectGiveAbility>().Any()).ToList();
   var others=allTrainers.Except(hiddenTrainers).ToList();
   var cats=DefDatabase<ThingCategoryDef>.AllDefsListForReading;
   Check(hiddenTrainers.Count>0 && hiddenTrainers.All(d=>d.comps.OfType<CompProperties_UseEffectGiveAbility>().Any(c=>ClassMigration.Hidden(c.ability))),"vpe clean: "+hiddenTrainers.Count+" legacy DND psytrainers found ("+others.Count+" VPE psytrainers untouched)");
   Check(hiddenTrainers.All(d=>d.tradeability==Tradeability.None && d.thingSetMakerTags.NullOrEmpty() && d.generateAllowChance==0f && d.thingCategories.NullOrEmpty() && !ThingSetMakerUtility.allGeneratableItems.Contains(d) && !cats.Any(c=>c.childThingDefs.Contains(d) || c.ContainedInThisOrDescendant(d))),"vpe clean: legacy psytrainers are out of traders, rewards and item filters");
   Check(others.Count>0 && others.Any(d=>!d.thingCategories.NullOrEmpty() && d.tradeability!=Tradeability.None),"vpe clean: VPE's own psytrainers still trade and keep their category");
   var p=Make(0,10,0);var psy=Classes.Psycasts(p);var comp=p.GetComp<CompAbilities>();
   psy.UnlockPath(Classes.Path(Classes.WizardPath));comp.GiveAbility(Def("LP_MagicMissile"));comp.GiveAbility(Def("LP_Fireball"));int pts=psy.points;
   int n=VPEClean.Strip(p);
   Check(n==3 && psy.points==pts+1 && !comp.LearnedAbilities.Any(a=>ClassMigration.Hidden(a.def)) && !psy.unlockedPaths.Any(ClassMigration.Hidden) && VPEClean.Strip(p)==0,"vpe clean: Strip removes 1 legacy path + 2 legacy abilities and refunds the path point");
   var patched=Harmony.GetPatchInfo(AccessTools.Method(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)}))?.Postfixes.Any(x=>x.PatchMethod.DeclaringType==typeof(VPEClean_Generate))==true;
   bool clean=true;
   for(int i=0;i<20;i++){var g=PawnGenerator.GeneratePawn(PawnKindDefOf.Villager);var gc=g.GetComp<CompAbilities>();if(gc!=null && gc.LearnedAbilities.Any(a=>ClassMigration.Hidden(a.def)) || Classes.Psycasts(g)?.unlockedPaths?.Any(ClassMigration.Hidden)==true)clean=false;Find.WorldPawns.PassToWorld(g,PawnDiscardDecideMode.Discard);}
   Check(patched && clean,"vpe clean: newly generated pawns never carry legacy DND paths or abilities");
  }
 }
}
