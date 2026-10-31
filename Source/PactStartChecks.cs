using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.68.0：Test.ps1 -Only PactStart（契约种族只在立约者开局可选，9 项）。
 public partial class LichChecks {
  static XenotypeDef PsX(string n)=>DefDatabase<XenotypeDef>.GetNamedSilentFail(n);

  void CheckPactStart(){
   // 1. 契约种族名单：九狱 / 妖精 / 天界 / 深渊 / 塞伊魔鬼，外加凡人血统的萨特、调换儿、坎比恩；普通种族不算
   var pact=PactStart.PactRaces.Select(x=>x.defName).OrderBy(x=>x).ToList();
   var must=new[]{"LP_ChainDevil","LP_BoneDevil","LP_HornedDevil","LP_Erinyes","LP_Amnizu","LP_Satyr","LP_Dryad","LP_Redcap","LP_Changeling","LP_WildHunter","LP_WildHuntLord","LP_Deva","LP_Planetar","LP_Dretch","LP_Barlgura","LP_Hezrou","LP_Succubus","LP_Nabassu","LP_ShadowDemon","LP_Cambion","LP_IceDevil","LP_Balor"};
   var missing=must.Where(n=>PsX(n)!=null && !pact.Contains(n)).ToList();
   var never=new[]{"Baseliner","LP_FireGiant","LP_Troll","LP_Elf","LP_Tiefling","LP_Aasimar","LP_Genie","LP_Titan","LP_MarilithKin"}.Where(n=>PsX(n)!=null && PactStart.IsPactRace(PsX(n))).ToList();
   Check(pact.Count==must.Count(n=>PsX(n)!=null) && missing.Count==0 && never.Count==0,"pact races: "+pact.Count+" ("+string.Join(",",pact)+")"+(missing.Count>0?" missing "+string.Join(",",missing):"")+(never.Count>0?" wrongly "+string.Join(",",never):""));

   // 2. 建角卡的异种下拉和自定义异种模板都换成了 PactStart.Choosable
   var choosable=AccessTools.Method(typeof(PactStart),nameof(PactStart.Choosable));var allDefs=AccessTools.PropertyGetter(typeof(DefDatabase<XenotypeDef>),"AllDefs");
   var main=AccessTools.Method(typeof(CharacterCardUtility),"LifestageAndXenotypeOptions");
   bool mainOk=false;try{var ins=PatchProcessor.GetCurrentInstructions(main);mainOk=ins.Any(c=>c.Calls(choosable)) && !ins.Any(c=>c.Calls(allDefs));}catch(Exception e){Log.Warning("[LICH-TEST] pact start IL: "+e.Message);}
   Check(mainOk && PactStartXenotypeMenu.Patched>=3,"xenotype menu, random-xenotype option and custom-xenotype templates read the filtered list ("+PactStartXenotypeMenu.Patched+" sites)");

   // 3. 普通剧本：列表里没有契约种族，其他种族都在
   var scen=Current.Game.Scenario;bool forced=PactStart.TestForce;
   try{
    PactStart.TestForce=false;
    var normal=PactStart.Allowed(scen)?null:PactStart.Choosable().ToList();
    int enemy=EnemyRaces.All.Count(); // 1.95.0：敌人专用异种两边都不列（狂猎既是契约种族也是敌人专用）
    int both=EnemyRaces.All.Count(PactStart.IsPactRace);
    Check(normal!=null && !normal.Any(PactStart.IsPactRace) && normal.Contains(XenotypeDefOf.Baseliner) && normal.Count==DefDatabase<XenotypeDef>.AllDefsListForReading.Count-pact.Count-enemy+both,"normal start lists "+(normal?.Count??-1)+" xenotypes, none of them pact races");
    PactStart.TestForce=true;
    Check(PactStart.Choosable().Count()==DefDatabase<XenotypeDef>.AllDefsListForReading.Count-enemy && PactStart.Choosable().Contains(PsX("LP_Planetar")),"pact start lists every non-enemy xenotype, including 行星使");
   }finally{PactStart.TestForce=forced;}

   // 4. 立约者剧本：加载无错、打开契约种族；其他剧本都不打开，也不在开局异种里点名契约种族
   var pdef=DefDatabase<ScenarioDef>.GetNamedSilentFail("LP_PactScenario");
   string text=null;try{text=pdef?.scenario.GetFullInformationText();}catch(Exception e){Log.Warning("[LICH-TEST] pact scenario text: "+e.Message);}
   var cfg=pdef?.scenario.AllParts.OfType<ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes>().FirstOrDefault();
   var counts=cfg==null?null:Traverse.Create(cfg).Field("xenotypeCounts").GetValue<List<XenotypeCount>>();
   Check(pdef!=null && !pdef.ConfigErrors().Any() && PactStart.Allowed(pdef.scenario) && text!=null && !text.Contains("Cannot read data") && counts!=null && counts.Sum(c=>Traverse.Create(c).Field("count").GetValue<int>())==4 && counts.Count(c=>PactStart.IsPactRace(c.xenotype))==3 && pdef.label=="立约者","scenario 立约者 loads, allows pact races and starts 立约者 + 萨特 + 坎比恩 + 劣魔");
   var leaks=DefDatabase<ScenarioDef>.AllDefs.Where(d=>d!=pdef && (PactStart.Allowed(d.scenario) || d.scenario.AllParts.OfType<ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes>().Any(p=>(Traverse.Create(p).Field("xenotypeCounts").GetValue<List<XenotypeCount>>()??new List<XenotypeCount>()).Any(c=>PactStart.IsPactRace(c.xenotype))))).Select(d=>d.defName).ToList();
   Check(leaks.Count==0,"no other scenario opens or names pact races"+(leaks.Count>0?": "+string.Join(",",leaks):""));

   // 5. 开局研究：位面血统、深渊学、位面契约学
   var res=pdef?.scenario.AllParts.OfType<ScenPart_StartingResearch>().Select(p=>Traverse.Create(p).Field("project").GetValue<ResearchProjectDef>()?.defName).ToList()??new List<string>();
   Check(res.Contains("LP_Research_Planetouched") && res.Contains("LP_Research_Abyssology") && res.Contains("LP_Research_PlanarPacts"),"pact scenario finishes 位面血统 / 深渊学 / 位面契约学");

   // 6. 开局物品：未安装的契约讲台 + 3 卷宗主各不相同的契约书
   var part=pdef?.scenario.AllParts.OfType<ScenPart_PactStart>().FirstOrDefault();var things=part?.PlayerStartingThings().ToList()??new List<Thing>();
   try{
    var lec=things.OfType<MinifiedThing>().FirstOrDefault(m=>m.InnerThing?.def==Pacts.LecternDef);
    var scrolls=things.Where(t=>t.def.defName=="LP_PactScroll").Select(t=>t.TryGetComp<CompPactScroll>()?.patron).ToList();
    Check(lec!=null && scrolls.Count==3 && scrolls.All(p=>p!=null) && scrolls.Distinct().Count()==Math.Min(3,Pacts.Patrons.Count()) && part.Summary(pdef.scenario).Contains("契约讲台"),"pact start gives a packed lectern and 3 scrolls from "+scrolls.Distinct().Count()+" patrons ("+string.Join(",",scrolls.Select(p=>p?.label))+")");
   }finally{foreach(var t in things)if(!t.Destroyed)t.Destroy();}

   // 7. 外来者殖民者戴位面锚链（倒下不遣返），凡人血统的萨特不戴
   Pawn dretch=null,satyr=null;
   try{
    dretch=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:PsX("LP_Dretch"),fixedBiologicalAge:25f,fixedChronologicalAge:25f));
    satyr=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:PsX("LP_Satyr"),fixedBiologicalAge:25f,fixedChronologicalAge:25f));
    bool before=Outsiders.BanishOnDown(dretch);int n=ScenPart_PactStart.Shackle(new[]{dretch,satyr});
    Check(before && n==1 && Outsiders.Shackled(dretch) && !Outsiders.BanishOnDown(dretch) && !Outsiders.VanishOnDeath(dretch) && !Outsiders.Shackled(satyr) && ScenPart_PactStart.Shackle(new[]{dretch})==0,"starting outsiders get a planar shackle once; the satyr does not");
   }finally{if(dretch!=null && !dretch.Destroyed)dretch.Destroy();if(satyr!=null && !satyr.Destroyed)satyr.Destroy();}
  }
 }
}
