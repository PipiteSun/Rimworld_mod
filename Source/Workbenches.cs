using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.47.0 DND工作台：附魔台、卷轴书桌、符文铁砧、巨人铁砧、法袍织机（Defs/Workbenches.xml）。
 // 这里放 XML 写不了的部分：巨人铁砧体型≥1.8 +25%、法袍织机做种族披风 +15%、卷轴书桌鉴定 ×1.5、守护神像让祈祷娱乐 +20%，
 // 以及旧存档读档时清掉挂在原版锻造台/炼金桌/裁缝台上、配方已经搬走的工作单（发一封信），并补完符文锻造/卷轴抄写研究。
 public static class Workbenches {
  public const string Enchanting="LP_EnchantingTable",Scribe="LP_ScribeDesk",RuneAnvil="LP_RuneAnvil",GiantAnvil="LP_GiantAnvil",Loom="LP_ArcaneLoom",Statue="LP_DeityStatue";
  public const float GiantBodySize=1.8f,GiantFactor=1.25f,LoomCapeFactor=1.15f,IdentifyFactorDesk=1.5f,PrayJoyStatue=1.2f;
  // 原版裁缝台照留、另加织机的种族披风：在织机上快15%。
  public static readonly HashSet<string> LoomCapes=new HashSet<string>{"LP_WinterWolfCloak","LP_CloudRobe","LP_IllithidTendrilCloak","LP_FeatherCloak","LP_StabilizerBand"};
  // 读档清理只看这些旧工作台：配方从它们身上搬走了。
  public static readonly HashSet<string> OldBenches=new HashSet<string>{"FueledSmithy","ElectricSmithy","LP_AlchemyTable","HandTailoringBench","ElectricTailoringBench"};

  public static bool Is(Thing t,string def)=>t?.def?.defName==def;
  // 正在这张工作台的交互格上做工作单的人。
  public static Pawn CurrentWorker(Building b){
   if(b==null || !b.Spawned || !b.def.hasInteractionCell)return null;
   foreach(var t in b.InteractionCell.GetThingList(b.Map))if(t is Pawn p && p.CurJob?.bill!=null && p.CurJob.targetA.Thing==b)return p;
   return null;
  }
  // 工作台速度里和工匠、配方有关的部分。
  public static float WorkerFactor(Thing bench,Pawn worker,RecipeDef recipe,out string why){
   why=null;
   if(bench==null || worker==null)return 1f;
   if(Is(bench,GiantAnvil) && worker.BodySize>=GiantBodySize){why="巨人铁砧（体型"+worker.BodySize.ToString("0.##")+"≥"+GiantBodySize+"）";return GiantFactor;}
   if(Is(bench,Loom) && recipe?.ProducedThingDef!=null && LoomCapes.Contains(recipe.ProducedThingDef.defName)){why="法袍织机（种族披风）";return LoomCapeFactor;}
   return 1f;
  }
  public static float WorkerFactor(Thing bench,Pawn worker,RecipeDef recipe)=>WorkerFactor(bench,worker,recipe,out _);
  // 鉴定：研究台或卷轴书桌；书桌快一半。
  public static bool CanIdentifyAt(Building b)=>b is Building_ResearchBench || Is(b,Scribe);
  public static float IdentifyFactor(Thing bench)=>Is(bench,Scribe)?IdentifyFactorDesk:1f;
  public static int IdentifyTicks(Thing bench)=>UnityEngine.Mathf.RoundToInt(CursedItems.IdentifyTicks/IdentifyFactor(bench));
  // 守护神像：连着神像的圣坛祈祷娱乐+20%。
  public static bool HasStatue(Thing altar)=>altar?.TryGetComp<CompAffectedByFacilities>()?.LinkedFacilitiesListForReading.Any(f=>Is(f,Statue))==true;
  public static float PrayJoyFactor(Thing altar)=>HasStatue(altar)?PrayJoyStatue:1f;

  // ---------- 旧存档 ----------
  public static ThingDef NewHome(RecipeDef r){
   foreach(var name in new[]{RuneAnvil,GiantAnvil,Enchanting,Scribe,Loom,"LP_FaithAltar"}){var d=DefDatabase<ThingDef>.GetNamedSilentFail(name);if(d!=null && d.AllRecipes.Contains(r))return d;}
   return r.AllRecipeUsers.FirstOrDefault();
  }
  public static bool Orphan(Building_WorkTable bench,Bill bill)=>bench!=null && bill?.recipe!=null && OldBenches.Contains(bench.def.defName) && bill.recipe.modContentPack?.PackageId?.ToLowerInvariant()=="local.lich.phylactery" && !bench.def.AllRecipes.Contains(bill.recipe);
  // 返回清掉的工作单（配方 → 新工作台）。
  public static List<(RecipeDef recipe,ThingDef home)> RemoveOrphanBills(){
   var removed=new List<(RecipeDef,ThingDef)>();
   foreach(var map in Find.Maps)foreach(var bench in map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().ToList()){
    foreach(var bill in bench.BillStack.Bills.Where(b=>Orphan(bench,b)).ToList()){removed.Add((bill.recipe,NewHome(bill.recipe)));bench.BillStack.Delete(bill);}
   }
   return removed;
  }
  public static readonly string[] RuneSmithingFrom={"LP_RacialArmory","LP_RunicFirearms","LP_MythicMetallurgy"};
  public static int CompleteResearch(){
   int n=0;Func<string,ResearchProjectDef> P=DefDatabase<ResearchProjectDef>.GetNamedSilentFail;
   var rune=P("LP_Research_RuneSmithing");if(rune!=null && !rune.IsFinished && RuneSmithingFrom.Any(s=>P(s)?.IsFinished==true)){Find.ResearchManager.FinishProject(rune,false,null,false);n++;}
   var scribe=P("LP_Research_Scribing");if(scribe!=null && !scribe.IsFinished && P("LP_Research_Enchanting")?.IsFinished==true){Find.ResearchManager.FinishProject(scribe,false,null,false);n++;}
   return n;
  }
  public const string LetterLabel="DND配方搬家了";
  public static string LetterText(List<(RecipeDef recipe,ThingDef home)> removed){
   var lines=removed.GroupBy(x=>x.home).Select(g=>"· "+(g.Key?.LabelCap.ToString()??"其他工作台")+"："+string.Join("、",g.Select(x=>x.recipe.ProducedThingDef?.label??x.recipe.label).Distinct()));
   return "DND配方搬家了：符文火器和异族装备去符文铁砧，巨人装备去巨人铁砧，魔法物品去附魔台，圣物去圣坛，卷轴和手册去卷轴书桌，法袍去法袍织机。\n\n这个存档里原版锻造台、炼金桌、裁缝台上挂着的这些工作单已经清掉了，请在新工作台上重新下单：\n"+string.Join("\n",lines)+"\n\n正在做的半成品按原版规则处理（可以丢弃返还材料）。";
  }
 }

 public class StatPart_WorkbenchWorker : StatPart {
  public override void TransformValue(StatRequest req,ref float val){
   if(!(req.Thing is Building b) || (b.def.defName!=Workbenches.GiantAnvil && b.def.defName!=Workbenches.Loom))return;
   var p=Workbenches.CurrentWorker(b);if(p!=null)val*=Workbenches.WorkerFactor(b,p,p.CurJob.bill.recipe);
  }
  public override string ExplanationPart(StatRequest req){
   if(!(req.Thing is Building b) || (b.def.defName!=Workbenches.GiantAnvil && b.def.defName!=Workbenches.Loom))return null;
   if(b.def.defName==Workbenches.GiantAnvil){var p=Workbenches.CurrentWorker(b);float f=Workbenches.WorkerFactor(b,p,p?.CurJob?.bill?.recipe,out var why);return f!=1f?why+"：×"+f.ToStringPercent():"体型1.8以上的工匠：×"+Workbenches.GiantFactor.ToStringPercent();}
   var w=Workbenches.CurrentWorker(b);float g=Workbenches.WorkerFactor(b,w,w?.CurJob?.bill?.recipe,out var why2);return g!=1f?why2+"：×"+g.ToStringPercent():"种族披风：×"+Workbenches.LoomCapeFactor.ToStringPercent();
  }
 }
 [StaticConstructorOnStartup]
 public static class WorkbenchesStartup {
  static WorkbenchesStartup(){
   var stat=StatDefOf.WorkTableWorkSpeedFactor;if(stat.parts==null)stat.parts=new List<StatPart>();
   if(!stat.parts.Any(p=>p is StatPart_WorkbenchWorker))stat.parts.Add(new StatPart_WorkbenchWorker{parentStat=stat});
  }
 }

 public class WorkbenchMigration : GameComponent {
  public int version;
  public WorkbenchMigration(Game game){}
  public override void StartedNewGame(){version=1;}
  public override void LoadedGame(){if(version>=1)return;try{Migrate();}catch(Exception e){Log.Error("[DND边缘大陆] 工作台搬家迁移失败："+e);}version=1;}
  public static int lettersSent;
  public static void Migrate(){
   int research=Workbenches.CompleteResearch();
   var removed=Workbenches.RemoveOrphanBills();
   if(research>0)Log.Message("[DND边缘大陆] DND工作台：按已完成的研究补完了"+research+"项新研究（符文锻造/卷轴抄写）。");
   if(removed.Count>0){
    Log.Message("[DND边缘大陆] DND工作台：清掉了"+removed.Count+"张配方已搬走的旧工作单。");
    Find.LetterStack.ReceiveLetter(Workbenches.LetterLabel,Workbenches.LetterText(removed),LetterDefOf.NeutralEvent);lettersSent++;
   }
  }
  public override void ExposeData(){Scribe_Values.Look(ref version,"workbenchVersion",0);}
 }
}
