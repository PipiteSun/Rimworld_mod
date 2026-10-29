using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.35.0：Test.ps1 -Only ResearchTab。所有研究在“DND边缘大陆”页签、坐标不重叠、前置在左边、旧页签已删除、DND 内容不再直接挂原版研究、旧存档迁移。
 public partial class LichChecks {
  static bool OwnDef(Def d)=>d?.modContentPack!=null && d.modContentPack.PackageId.ToLowerInvariant()=="local.lich.phylactery";
  void CheckResearchTab(){
   var tab=DefDatabase<ResearchTabDef>.GetNamedSilentFail(ResearchTab.Tab);
   var own=DefDatabase<ResearchProjectDef>.AllDefs.Where(OwnDef).ToList();
   Check(tab!=null && tab.label=="DND边缘大陆" && own.Count>=11 && own.All(p=>p.tab==tab),"all "+own.Count+" mod research projects are in the DND边缘大陆 tab: "+string.Join(",",own.Where(p=>p.tab!=tab).Select(p=>p.defName+"@"+p.tab?.defName)));
   var inTab=DefDatabase<ResearchProjectDef>.AllDefs.Where(p=>p.tab==tab).ToList();
   var clash=new List<string>();for(int i=0;i<inTab.Count;i++)for(int j=i+1;j<inTab.Count;j++)if(Math.Abs(inTab[i].ResearchViewX-inTab[j].ResearchViewX)<.5f && Math.Abs(inTab[i].ResearchViewY-inTab[j].ResearchViewY)<.5f)clash.Add(inTab[i].defName+"/"+inTab[j].defName);
   Check(clash.Count==0,"no two projects in the tab overlap: "+string.Join(",",clash));
   var back=inTab.SelectMany(p=>(p.prerequisites??new List<ResearchProjectDef>()).Where(r=>r.tab==tab && r.ResearchViewX>=p.ResearchViewX).Select(r=>r.defName+"->"+p.defName)).ToList();
   Check(back.Count==0,"every in-tab prerequisite sits to the left: "+string.Join(",",back));
   Check(DefDatabase<ResearchTabDef>.GetNamedSilentFail("LP_ForgeTab")==null && !DefDatabase<ResearchTabDef>.AllDefs.Any(t=>t!=tab && OwnDef(t)),"old 熔火锻造 tab LP_ForgeTab is gone and the mod has only one tab");
   // DND 的装备、建筑和配方只认本页签的研究，不再直接挂在原版研究下面。
   var vanilla=new List<string>();
   foreach(var d in DefDatabase<ThingDef>.AllDefs.Where(OwnDef)){foreach(var r in (d.researchPrerequisites??new List<ResearchProjectDef>()))if(r.tab!=tab)vanilla.Add(d.defName+":"+r.defName);var rm=d.recipeMaker;if(rm!=null){if(rm.researchPrerequisite!=null && rm.researchPrerequisite.tab!=tab)vanilla.Add(d.defName+":"+rm.researchPrerequisite.defName);foreach(var r in (rm.researchPrerequisites??new List<ResearchProjectDef>()))if(r.tab!=tab)vanilla.Add(d.defName+":"+r.defName);}}
   foreach(var d in DefDatabase<RecipeDef>.AllDefs.Where(OwnDef)){if(d.researchPrerequisite!=null && d.researchPrerequisite.tab!=tab)vanilla.Add(d.defName+":"+d.researchPrerequisite.defName);foreach(var r in (d.researchPrerequisites??new List<ResearchProjectDef>()))if(r.tab!=tab)vanilla.Add(d.defName+":"+r.defName);}
   Check(vanilla.Count==0,"mod items, buildings and recipes are unlocked only by research in the DND tab: "+string.Join(",",vanilla));
   // 旧存档迁移：只完成原版前置都已完成的新研究。
   Find.ResearchManager.ResetAllProgress();Find.ResearchManager.FinishProject(DefDatabase<ResearchProjectDef>.GetNamed("Smithing"),false,null,false);
   int n=ResearchTab.MigrateLegacy();Func<string,bool> fin=s=>DefDatabase<ResearchProjectDef>.GetNamed(s).IsFinished;
   Check(n==2 && fin("LP_GiantForging") && fin("LP_RacialArmory") && !fin("LP_GiantPlate") && !fin("LP_RacialBlades") && !fin("LP_GeneWorkbenchTech") && DefDatabase<ThingDef>.GetNamed("LP_FireGiantForge").IsResearchFinished,"legacy save with smithing keeps giant forge and racial armory, nothing more: "+n);
   Find.ResearchManager.ResetAllProgress();
  }
 }
}
