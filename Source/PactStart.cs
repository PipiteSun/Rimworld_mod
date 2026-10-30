using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.68.0 契约开局（报告 ArtSource/PactStart-1.68.0-report.md）：
 // 契约种族（九狱魔鬼、妖精宫廷、天界使者、深渊恶魔，外加萨特 / 调换儿 / 坎比恩，以及塞伊的寒冰魔 / 炎魔）不再出现在普通开局的建角异种下拉里；
 // 判定：任一基因挂着“倒下遣返”的外来者扩展（六臂蛇人的深渊之躯 banishOnDown=false，是早于契约的可玩种族，不算），或者是那三个凡人血统种族。
 // 只有带“立约者开局”组件（ScenPart_PactStart）的剧本才列出它们。开局里的外来者殖民者戴上位面锚链，倒下不会被遣返。
 public static class PactStart {
  static readonly HashSet<string> Mortal=new HashSet<string>{"LP_Satyr","LP_Changeling","LP_Cambion"};
  static Dictionary<XenotypeDef,bool> cache;
  public static bool IsPactRace(XenotypeDef x){
   if(x==null)return false;if(cache==null)cache=new Dictionary<XenotypeDef,bool>();
   if(!cache.TryGetValue(x,out bool v))cache[x]=v=Mortal.Contains(x.defName) || x.genes!=null && x.genes.Any(g=>g?.GetModExtension<LP_OutsiderExtension>()?.banishOnDown==true);
   return v;
  }
  public static IEnumerable<XenotypeDef> PactRaces=>DefDatabase<XenotypeDef>.AllDefs.Where(IsPactRace);
  public static bool TestForce; // 测试用：当作契约剧本
  public static bool Allowed(Scenario s)=>TestForce || s?.AllParts.Any(p=>p is ScenPart_PactStart)==true;
  public static bool AllowedNow=>Allowed(Current.Game?.Scenario);
  // 替换建角页里的 DefDatabase<XenotypeDef>.AllDefs
  public static IEnumerable<XenotypeDef> Choosable(){var all=DefDatabase<XenotypeDef>.AllDefs;return AllowedNow?all:all.Where(x=>!IsPactRace(x));}
 }

 [HarmonyPatch]
 public static class PactStartXenotypeMenu {
  static readonly MethodInfo AllDefs=AccessTools.PropertyGetter(typeof(DefDatabase<XenotypeDef>),nameof(DefDatabase<XenotypeDef>.AllDefs));
  public static int Patched;
  static bool Calls(MethodBase m){try{return PatchProcessor.ReadMethodBody(m).Any(x=>x.Value is MethodInfo mi && mi==AllDefs);}catch{return false;}}
  // 建角卡的异种下拉（含“任意非超凡”随机项的 lambda）和自定义异种窗口的模板列表；按名字找，游戏更新改了结构就少补几处，不影响加载。
  static IEnumerable<MethodBase> Targets(){
   foreach(var t in new[]{typeof(CharacterCardUtility),typeof(Dialog_CreateXenotype)})
    foreach(var type in new[]{t}.Concat(t.GetNestedTypes(BindingFlags.NonPublic|BindingFlags.Public)))
     foreach(var m in AccessTools.GetDeclaredMethods(type))
      if((m.Name.Contains("LifestageAndXenotypeOptions") || m.Name.Contains("DrawSearchRect")) && !m.IsAbstract && Calls(m))yield return m;
  }
  static bool Prepare()=>Targets().Any();
  static IEnumerable<MethodBase> TargetMethods()=>Targets();
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code){
   foreach(var c in code){if(c.Calls(AllDefs)){c.opcode=OpCodes.Call;c.operand=AccessTools.Method(typeof(PactStart),nameof(PactStart.Choosable));Patched++;}yield return c;}
  }
 }

 // ==================== 剧本组件：立约者开局 ====================
 // 开局物品：一座未安装的契约讲台 + scrolls 卷宗主各不相同的契约书；开局后给外来者殖民者戴位面锚链。
 public class ScenPart_PactStart : ScenPart {
  public int scrolls=3;public bool shackle=true;
  string sBuf;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref scrolls,"scrolls",3);Scribe_Values.Look(ref shackle,"shackle",true);}
  public override string Summary(Scenario scen)=>"契约种族（魔鬼、妖精、天界使者、恶魔等）可以在建角时选择；开局带一座契约讲台和"+scrolls+"卷宗主各不相同的契约书"+(shackle?"；外来者殖民者戴着位面锚链，倒下不会被遣返":"")+"。";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(scrolls<0 || scrolls>10)yield return "scrolls must be 0-10";}
  public override void DoEditInterface(Listing_ScenEdit listing){
   var r=listing.GetScenPartRect(this,RowHeight*2f);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,0,2),"契约书 ",ref scrolls,ref sBuf,0,10);
   Widgets.CheckboxLabeled(ScenEdit.Row(r,1,2),"外来者戴位面锚链",ref shackle);
  }
  public override IEnumerable<Thing> PlayerStartingThings(){
   var lec=Pacts.LecternDef;
   if(lec!=null){var b=ThingMaker.MakeThing(lec,ThingDefOf.WoodLog);b.SetFactionDirect(Faction.OfPlayer);yield return b.MakeMinified();}
   foreach(var s in Scrolls(scrolls))yield return s;
  }
  public static List<Thing> Scrolls(int n){
   var list=new List<Thing>();var def=DefDatabase<ThingDef>.GetNamedSilentFail("LP_PactScroll");if(def==null)return list;
   var patrons=Pacts.Patrons.InRandomOrder().ToList();
   for(int i=0;i<n;i++){var t=ThingMaker.MakeThing(def);var c=t.TryGetComp<CompPactScroll>();if(c!=null && patrons.Count>0)c.SetPatron(patrons[i%patrons.Count]);list.Add(t);}
   return list;
  }
  public override void PostGameStart(){if(!shackle)return;var map=Find.AnyPlayerHomeMap;if(map!=null)Shackle(ScenEdit.Starters(map));}
  public static int Shackle(IEnumerable<Pawn> pawns){
   int n=0;var def=Outsiders.ShackledDef;if(def==null)return 0;
   foreach(var p in pawns.ToList())if(Fiends.Is(p) && !Outsiders.Shackled(p)){p.health.AddHediff(def);n++;}
   return n;
  }
 }
}
