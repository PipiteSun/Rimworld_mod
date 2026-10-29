using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.28.0：Test.ps1 -Only <组名>（多个用逗号隔开）只跑指定的检查组和它自己的存读档部分，默认行为不变。
 // 组名就是检查方法名去掉 Check：Events → CheckEvents()、PrepareEventsSave()、CheckEventsAfterLoad()；Beasts、Items、Races124、GeneWorkbench 等同理。
 public partial class LichChecks {
  static string OnlyArg{get{return GenCommandLine.TryGetCommandLineArg("lichonly",out var v)?v:null;}}
  static bool OnlyMode=>!OnlyArg.NullOrEmpty();
  static List<string> OnlyGroups=>(OnlyArg??"").Split(',').Select(s=>s.Trim()).Where(s=>s.Length>0).ToList();
  void InvokeGroup(string name,bool required){
   var m=AccessTools.Method(typeof(LichChecks),name);
   if(m==null || m.GetParameters().Length>0){if(required)throw new Exception("unknown check group for -Only: "+name);return;}
   try{m.Invoke(this,null);}
   catch(TargetInvocationException e){if(e.InnerException==null)throw;System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
  }
  void RunOnly(){
   map=Find.CurrentMap;
   // 有些组要用死灵仪式台旁边的格子放东西（Spawn）：和完整检查一样先放一张。
   var cell=map.AllCells.Where(c=>c.x>12 && c.z>12 && c.x<map.Size.x-12 && c.z<map.Size.z-12 && CellRect.CenteredOn(c,3).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth))).First();
   altar=(Building_Altar)ThingMaker.MakeThing(Necro.Def<ThingDef>("Altar"));altar.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(altar,cell,map);
   if(OnlyGroups.Any(g=>LichFixtureGroups.Contains(g)))LichWorkerFixture();
   foreach(var g in OnlyGroups)InvokeGroup("Check"+g,true);
   foreach(var g in OnlyGroups)InvokeGroup("Prepare"+g+"Save",false);
  }
  // 1.63.0：知识回馈、造物设计的检查用完整检查里那位巫妖（worker）和他的骷髅、魔像、四个知识亡灵。
  // -Only 时没有这些，所以照完整检查的流程（同样的仪式账单）先做一份；完整检查不走这里。
  static readonly HashSet<string> LichFixtureGroups=new HashSet<string>{"KnowledgeFeedback","ConstructDesign"};
  void LichWorkerFixture(){Rand.PushState(16301);try{LichWorkerFixtureInner();}finally{Rand.PopState();}}
  void LichWorkerFixtureInner(){
   worker=Person();
   for(int attempt=0;attempt<20 && worker.GetStatValue(StatDefOf.PsychicSensitivity)<=0;attempt++){worker.Destroy();worker=Person();}
   GenSpawn.Spawn(worker,altar.InteractionCell,map);worker.workSettings.EnableAndInitialize();worker.workSettings.SetPriority(WorkTypeDefOf.Crafting,1);worker.skills.GetSkill(SkillDefOf.Crafting).Level=12;
   for(int h=0;h<24;h++)worker.timetable.SetAssignment(h,TimeAssignmentDefOf.Work);
   Spawn("Gold",50);Spawn("Plasteel",50);Spawn("MedicineIndustrial",5);Spawn("ComponentSpacer",2);Execute("Ascend");
   var box=altar.selected;Check(Necro.Lich(worker)!=null && box!=null,"-Only fixture: ritual makes a lich with a phylactery");
   box.level=10;box.experience=23.5f;worker.psychicEntropy.OffsetPsyfocusDirectly(1);
   Spawn("Meat_Human",50);Execute("MakeSkeleton");Spawn("Meat_Human",300);Execute("MakeGolem");
   foreach(var part in new[]{"Brain","Eye","Arm","Heart"}){Spawn("Meat_Human",150);Execute("MakeScholar"+part);}
   var kinds=Necro.LivingConstructs(worker).Select(p=>Necro.Construct(p).kind).ToList();
   Check(kinds.Count(k=>k=="Skeleton")==1 && kinds.Count(k=>k=="Golem")==1 && kinds.Count(k=>k=="Scholar")==4,"-Only fixture: skeleton, golem and four knowledge undead ("+string.Join(",",kinds)+")");
  }
  void OnlyAfterLoad(){map=Find.CurrentMap;foreach(var g in OnlyGroups)InvokeGroup("Check"+g+"AfterLoad",false);}
 }
}
