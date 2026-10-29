using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 // 1.26.0 基因工作台的检查：定义、自带基因库接入装配、定向提取（自己走进去 / 搬运工送囚犯）、单基因基因组、古菌排除、原版提取后状态、取消、断电、存读档。
 public partial class LichChecks {
  static string gwBenchId,gwPawnId,gwGene;static int gwTicksLeft,gwBankCount;
  readonly List<Thing> gwMade=new List<Thing>();
  static readonly System.Reflection.MethodInfo gwCheckContainers=AccessTools.Method(typeof(Building_GeneAssembler),"CheckAllContainersValid");

  Building_GeneWorkbench GwSpawnBench(){
   var cell=map.AllCells.Where(c=>c.x>15 && c.z>15 && c.x<map.Size.x-15 && c.z<map.Size.z-15 && CellRect.CenteredOn(c,4).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth))).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
   foreach(var t in CellRect.CenteredOn(cell,4).Cells.SelectMany(c=>c.GetThingList(map)).Where(t=>t is Plant).ToList())t.Destroy();
   var w=(Building_GeneWorkbench)ThingMaker.MakeThing(GeneWorkbench.Def);w.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(w,cell,map,Rot4.South);gwMade.Add(w);
   w.GetComp<CompPowerTrader>().PowerOn=true;return w;
  }
  Pawn GwPawn(){var p=Person();p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,6),map);gwMade.Add(p);return p;}
  // 其余殖民者全部征召，免得抢活。
  void GwQuiet(params Pawn[] keep){foreach(var other in map.mapPawns.FreeColonistsSpawned.Where(p=>!keep.Contains(p)).ToList()){other.jobs.StopAll();if(other.drafter!=null)other.drafter.Drafted=true;}}

  void CheckGeneWorkbench(){
   try{GwDefs();GwSaveFixUnits();var w=GwSpawnBench();GwBank(w);GwHaulIntoBank(w);GwExtractColonist(w);GwExtractPrisoner(w);GwCancelAndPower(w);}
   finally{foreach(var t in gwMade.ToList())if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}gwMade.Clear();foreach(var p in map.mapPawns.FreeColonistsSpawned.ToList())if(p.drafter!=null)p.drafter.Drafted=false;}
  }

  // 定义：5×5、中文、组件、研究、造价、工作。
  void GwDefs(){
   var d=GeneWorkbench.Def;
   Check(d!=null && d.thingClass==typeof(Building_GeneWorkbench) && typeof(Building_GeneAssembler).IsAssignableFrom(d.thingClass),"gene workbench loads and derives from the vanilla gene assembler");
   Check(d.size==new IntVec2(5,5) && d.graphicData.drawSize==new Vector2(5,5) && d.hasInteractionCell,"gene workbench occupies 5x5 with an interaction cell");
   Check(d.label=="基因工作台" && Chinese(d.description),"gene workbench has a Chinese label and description");
   Check(d.GetCompProperties<CompProperties_Power>()?.PowerConsumption==400f && d.GetCompProperties<CompProperties_GenepackContainer>()?.maxCapacity==8 && d.GetCompProperties<CompProperties_Flickable>()!=null && d.GetCompProperties<CompProperties_GeneWorkbench>()!=null,"gene workbench has power 400 W, an 8-slot gene bank, a switch and the extraction component");
   var link=d.GetCompProperties<CompProperties_AffectedByFacilities>();Check(link!=null && link.linkableFacilities.Contains(ThingDefOf.GeneBank) && link.linkableFacilities.Any(f=>f.defName=="GeneProcessor"),"vanilla gene banks and gene processors can still link to the workbench");
   Check(d.inspectorTabs.Contains(typeof(ITab_ContentsGenepackHolder)) && d.designationCategory?.defName=="Biotech" && d.researchPrerequisites.Any(r=>r.defName=="LP_GeneWorkbenchTech" && r.prerequisites.Any(x=>x.defName=="Xenogermination")),"workbench shows the genepack tab, is in the Biotech menu and needs gene workbench research (after Xenogermination)");
   Check(d.CostListAdjusted(null).Any(c=>c.thingDef==ThingDefOf.Steel && c.count==400) && d.CostListAdjusted(null).Any(c=>c.thingDef==ThingDefOf.ComponentIndustrial && c.count==10) && d.CostListAdjusted(null).Any(c=>c.thingDef==ThingDefOf.ComponentSpacer && c.count==4),"workbench costs 400 steel, 10 components, 4 advanced components");
   Check(ContentFinder<Texture2D>.Get("GeneWorkbench/GeneWorkbench_south",false)!=null && ContentFinder<Texture2D>.Get("GeneWorkbench/ExtractGene",false)!=null,"workbench texture and extraction gizmo icon load");
   var giver=DefDatabase<WorkGiverDef>.GetNamed("LP_CreateXenogermWorkbench");Check(giver.Worker is WorkGiver_CreateXenogerm && ((WorkGiver_Scanner)giver.Worker).PotentialWorkThingRequest.singleDef==d && giver.workType==WorkTypeDefOf.Research,"researchers create xenogerms at the workbench through the vanilla work logic");
   Check(DefDatabase<WorkGiverDef>.GetNamed("LP_CarryToGeneWorkbench").workType==WorkTypeDefOf.Hauling,"carrying a person to the workbench is hauling work, like the vanilla extractor");
   // 古菌、黑色素排除；普通基因可提取。
   Check(!GeneWorkbench.Extractable(DefDatabase<GeneDef>.GetNamed("Ageless")) && DefDatabase<GeneDef>.GetNamed("Ageless").biostatArc>0,"archite genes cannot be extracted");
   Check(DefDatabase<GeneDef>.AllDefs.Where(g=>g.endogeneCategory==EndogeneCategory.Melanin).All(g=>!GeneWorkbench.Extractable(g)) && GeneWorkbench.Extractable(DefDatabase<GeneDef>.GetNamed("DarkVision")),"melanin genes are skipped like the vanilla extractor; ordinary genes are extractable");
  }

  // 自带基因库进入装配器的基因组列表；断电时算未供电；装配进行中基因组被拿走会取消。
  void GwBank(Building_GeneWorkbench w){
   var bank=w.Bank;var power=w.GetComp<CompPowerTrader>();
   var pack=GeneWorkbench.MakeGenepack(DefDatabase<GeneDef>.GetNamed("DarkVision"));Check(bank.innerContainer.TryAdd(pack),"genepack goes into the built-in gene bank");
   Check(w.GetGenepacks(true,false).Contains(pack) && w.GetGenepacks(true,true).Contains(pack) && !w.GetGenepacks(false,true).Contains(pack),"assembler library includes the built-in bank's genepacks when powered");
   Check(w.GetGeneBankHoldingPack(pack)==bank,"assembler finds the built-in bank holding a genepack (eject from the assembly dialog works)");
   Check(w.MaxComplexity()==6,"workbench complexity limit stays vanilla without processors: "+w.MaxComplexity());
   power.PowerOn=false;Check(!w.GetGenepacks(true,false).Contains(pack) && w.GetGenepacks(false,true).Contains(pack),"unpowered workbench marks its own genepacks unpowered");power.PowerOn=true;
   var dialog=new Dialog_CreateXenogerm(w);var lib=(List<Genepack>)AccessTools.Field(typeof(Dialog_CreateXenogerm),"libraryGenepacks").GetValue(dialog);Check(lib.Contains(pack),"vanilla assembly dialog lists the built-in bank's genepacks");
   w.Start(new List<Genepack>{pack},0,"测试",XenotypeIconDefOf.Basic);gwCheckContainers.Invoke(w,null);Check(w.Working,"assembly using a built-in genepack is not cancelled by the container check");
   Check(w.CanBeWorkedOnNow.Accepted,"assembly using a built-in genepack can be worked on: "+w.CanBeWorkedOnNow.Reason);
   bank.innerContainer.TryDrop(pack,w.InteractionCell,map,ThingPlaceMode.Near,1,out var dropped);gwCheckContainers.Invoke(w,null);Check(!w.Working,"removing the genepack cancels the assembly like vanilla");
   if(dropped!=null && !dropped.Destroyed)dropped.Destroy();
   var archite=ThingMaker.MakeThing(ThingDefOf.ArchiteCapsule);Check(w.TryGetInnerInteractableThingOwner()==w.innerContainer,"archite capsules still go into the assembler container");archite.Destroy();
  }

  // 原版“存入基因组”搬运工作把地上的基因组送进工作台自带的库（而不是古菌胶囊容器）。
  void GwHaulIntoBank(Building_GeneWorkbench w){
   var hauler=GwPawn();GwQuiet(hauler);hauler.workSettings.EnableAndInitialize();
   var pack=GeneWorkbench.MakeGenepack(DefDatabase<GeneDef>.GetNamed("DarkVision"));GenPlace.TryPlaceThing(pack,w.InteractionCell+IntVec3.South*2,map,ThingPlaceMode.Near);pack.SetForbidden(false,false);
   var giver=new WorkGiver_HaulToGeneBank{def=DefDatabase<WorkGiverDef>.GetNamed("HaulToGeneBank")};var job=giver.JobOnThing(hauler,pack,true);
   Check(job!=null && job.targetB.Thing==w,"vanilla genepack hauling targets the workbench's built-in bank");
   hauler.jobs.StartJob(job,JobCondition.InterruptForced);for(int i=0;i<3000 && pack.ParentHolder!=w.Bank;i++)Step(1);
   Check(pack.ParentHolder==w.Bank && !w.innerContainer.Contains(pack),"hauled genepack lands in the built-in bank, not the archite container; job="+hauler.CurJob);
   pack.Destroy();
  }

  // 殖民者：选人选基因 → 自己走进去 → 30000 刻 → 单基因基因组进库，原版提取后状态，基因不移除。
  void GwExtractColonist(Building_GeneWorkbench w){
   var p=GwPawn();GwQuiet(p);var dark=DefDatabase<GeneDef>.GetNamed("DarkVision");var ageless=DefDatabase<GeneDef>.GetNamed("Ageless");
   p.genes.AddGene(dark,true);p.genes.AddGene(ageless,true);
   var genes=GeneWorkbench.ExtractableGenes(p);
   Check(genes.Contains(dark) && !genes.Contains(ageless) && genes.All(g=>g.biostatArc==0 && g.endogeneCategory!=EndogeneCategory.Melanin),"gene menu lists the pawn's genes except archite and melanin genes");
   Check(GeneWorkbench.GeneLabel(p,dark).Contains(dark.label) && GeneWorkbench.GeneLabel(p,dark).Contains("复杂度") && GeneWorkbench.GeneLabel(p,dark).Contains("异种"),"gene menu entry shows the Chinese gene name, complexity and metabolism");
   var c=w.Extraction;Check(c.CanAccept(p).Accepted,"colonist can be chosen for extraction: "+c.CanAccept(p).Reason);
   c.Select(p,dark);Step(2);Check(c.Selected==p && c.SelectedGene==dark && (p.CurJobDef==GeneWorkbench.EnterJob || c.Working),"choosing a colonist orders them to walk to the workbench; job="+p.CurJob+" queue="+p.jobs.jobQueue.Count+" drafted="+p.Drafted);
   for(int i=0;i<3000 && !c.Working;i++)Step(1);
   Check(c.Working && c.Contained==p && !p.Spawned && p.MapHeld==map,"colonist enters the workbench; job="+p.CurJob);
   Check(map.mapPawns.AllPawns.Contains(p) && p.ParentHolder==c,"pawn inside the workbench is still tracked by the map");
   int left=c.ticksRemaining;Check(left>GeneWorkbench.ExtractTicks-3000 && left<=GeneWorkbench.ExtractTicks,"extraction takes the vanilla 30000 ticks");
   Step(5);Check(c.ticksRemaining==left-5,"extraction progresses with power");
   float food=p.needs.food.CurLevel;Step(300);Check(p.needs.food.CurLevel<food,"pawn inside keeps ticking (needs fall)");Check(((CompPowerTrader)w.PowerComp).PowerOutput==-400f,"workbench draws full 400 W while extracting: "+((CompPowerTrader)w.PowerComp).PowerOutput);
   int before=w.Bank.innerContainer.Count;c.ticksRemaining=1;Step(1);
   Check(!c.Working && c.Selected==null && p.Spawned && p.Position.InHorDistOf(w.InteractionCell,3f),"extraction finishes and the pawn steps out at the interaction cell");
   var made=w.Bank.ContainedGenepacks.LastOrDefault();
   Check(w.Bank.innerContainer.Count==before+1 && made!=null && made.GeneSet.GenesListForReading.Count==1 && made.GeneSet.GenesListForReading[0]==dark,"result is a genepack with exactly the chosen gene, stored in the built-in bank");
   var regrow=p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.XenogermReplicating);int ticks=regrow?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear??0;
   Check(regrow!=null && ticks>=60000*12-10 && ticks<=60000*20 && p.health.hediffSet.HasHediff(HediffDefOf.XenogermLossShock),"vanilla post-extraction states applied (gene loss shock, 12-20 days regrowing): "+ticks);
   Check(p.genes.HasActiveGene(dark) || p.genes.GenesListForReading.Any(g=>g.def==dark),"extracted gene stays on the pawn");
   Check(!GeneWorkbench.CanExtractFrom(p).Accepted && !c.CanAccept(p).Accepted,"regrowing pawn cannot be extracted again");
   Check(w.GetGenepacks(true,false).Contains(made),"new genepack is immediately usable for assembly");
  }

  // 囚犯：搬运工送进去；结束时囚犯得原版“基因被采集”心情；库满时基因组放地上。
  void GwExtractPrisoner(Building_GeneWorkbench w){
   var hauler=GwPawn();var p=GwPawn();GwQuiet(hauler,p);hauler.workSettings.EnableAndInitialize();
   var other=Find.FactionManager.AllFactionsVisible.First(f=>!f.IsPlayer && f.def.humanlikeFaction && !f.temporary);p.SetFaction(other);p.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);
   Check(p.IsPrisonerOfColony,"prisoner fixture");
   var gene=GeneWorkbench.ExtractableGenes(p).First();var c=w.Extraction;
   c.Select(p,gene);Check(c.Selected==p && p.CurJobDef!=GeneWorkbench.EnterJob && CompGeneWorkbench.NeedsCarrying(p),"choosing a prisoner waits for a hauler instead of ordering the prisoner");
   var giver=(WorkGiver_Scanner)DefDatabase<WorkGiverDef>.GetNamed("LP_CarryToGeneWorkbench").Worker;
   Check(giver.HasJobOnThing(hauler,w,true),"hauler finds the carry-to-workbench job");
   hauler.jobs.StartJob(giver.JobOnThing(hauler,w,true),JobCondition.InterruptForced);
   for(int i=0;i<4000 && !c.Working;i++)Step(1);
   Check(c.Working && c.Contained==p,"hauler carries the prisoner into the workbench; job="+hauler.CurJob);
   Check(!giver.HasJobOnThing(hauler,w,true),"no second carry job while extracting");
   while(!w.Bank.Full)w.Bank.innerContainer.TryAdd(GeneWorkbench.MakeGenepack(DefDatabase<GeneDef>.GetNamed("DarkVision")));
   var pack=c.Finish();
   Check(pack!=null && pack.Spawned && pack.GeneSet.GenesListForReading.Single()==gene,"when the built-in bank is full the genepack is placed next to the workbench");
   Check(p.needs.mood.thoughts.memories.GetFirstMemoryOfDef(ThoughtDefOf.XenogermHarvested_Prisoner)!=null,"prisoner gets the vanilla 'xenogerm harvested' memory");
   gwMade.Add(pack);foreach(var t in w.Bank.innerContainer.ToList())t.Destroy();
  }

  // 取消：放人出来、不产出、不加状态；断电暂停。
  void GwCancelAndPower(Building_GeneWorkbench w){
   var p=GwPawn();GwQuiet(p);var gene=GeneWorkbench.ExtractableGenes(p).First();var c=w.Extraction;var power=w.GetComp<CompPowerTrader>();
   c.Select(p,gene);p.jobs.StopAll();Check(c.TryAccept(p) && c.Working,"pawn accepted directly");
   power.PowerOn=false;int left=c.ticksRemaining;Step(10);Check(c.ticksRemaining==left && c.powerCutTicks>=10,"extraction pauses without power");power.PowerOn=true;
   int packs=w.Bank.innerContainer.Count;c.Cancel();
   Check(!c.Working && c.Selected==null && p.Spawned && w.Bank.innerContainer.Count==packs && !p.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating),"cancelling releases the pawn without a genepack or post-extraction states");
   c.Select(p,gene);Step(2);Check(c.Selected==p && p.CurJobDef==GeneWorkbench.EnterJob,"pawn can be chosen again after cancelling; job="+p.CurJob);Step(1);c.Cancel();Check(c.Selected==null && p.CurJobDef!=GeneWorkbench.EnterJob,"cancelling a pending choice stops the walk order");
  }

  // 1.27.2 存档修复：迁移函数（XML 单元检查）与读档后去重。
  void GwSaveFixUnits(){
   var doc=new System.Xml.XmlDocument();
   doc.LoadXml("<thing><def>LP_GeneWorkbench</def><innerContainer><innerList><li><def>Genepack</def><id>Genepack1</id></li></innerList></innerContainer><leftToLoad/><innerContainer><innerList><li><def>ArchiteCapsule</def><id>ArchiteCapsule2</id><stackCount>3</stackCount></li><li><def>Genepack</def><id>Genepack1</id></li></innerList></innerContainer></thing>");
   var node=doc.DocumentElement;int removed=GeneWorkbench.MigrateLegacyNode(node);
   var ic=node.ChildNodes.OfType<System.Xml.XmlElement>().Where(e=>e.Name=="innerContainer").ToList();
   Check(removed==1 && node[GeneWorkbench.BankLabel]?.SelectNodes("innerList/li").Count==1 && node[GeneWorkbench.BankLabel].SelectSingleNode("innerList/li/id").InnerText=="Genepack1","legacy save: the first innerContainer (old gene bank) is renamed to lpGenepacks");
   Check(ic.Count==1 && ic[0].SelectNodes("innerList/li").Count==1 && ic[0].SelectSingleNode("innerList/li/def").InnerText=="ArchiteCapsule","legacy save: the assembler keeps only its archite capsules (duplicated genepack entries stripped)");
   var before=node.OuterXml;Check(GeneWorkbench.MigrateLegacyNode(node)==0 && node.OuterXml==before,"migration is idempotent");
   doc.LoadXml("<thing><lpGenepacks><innerList><li><def>Genepack</def></li></innerList></lpGenepacks><innerContainer><innerList/></innerContainer></thing>");before=doc.OuterXml;
   Check(GeneWorkbench.MigrateLegacyNode(doc.DocumentElement)==0 && doc.OuterXml==before,"new-format save is left untouched");
   Check(GeneWorkbench.Def.comps.OfType<CompProperties_GenepackContainer>().Single().compClass==typeof(CompLP_GenepackContainer),"workbench gene bank uses the renamed-save-label component");
   // 地图上已有的复制品：同一 ThingID 只留一份，优先留在基因库里的。
   var w=GwSpawnBench();var real=GeneWorkbench.MakeGenepack(DefDatabase<GeneDef>.GetNamed("DarkVision"));w.Bank.innerContainer.TryAdd(real);
   var copy=GeneWorkbench.MakeGenepack(DefDatabase<GeneDef>.GetNamed("DarkVision"));copy.thingIDNumber=real.thingIDNumber;GenPlace.TryPlaceThing(copy,w.InteractionCell,map,ThingPlaceMode.Near);
   Check(GeneWorkbench.RemoveDuplicateGenepacks()==1 && copy.Destroyed && !real.Destroyed && real.ParentHolder==w.Bank,"duplicate cleanup keeps the copy in the gene bank and destroys the one on the floor");
   Check(GeneWorkbench.RemoveDuplicateGenepacks()==0,"cleanup finds nothing more");
   real.Destroy();
  }

  // 存读档：提取进行中，人在里面，库里有两个基因组，装配器容器里有 3 个超凡胶囊；另一台工作台在存档文件里改写成 1.26～1.27.1 的旧格式。
  static List<string> gwPackIds=new List<string>(),gwLegacyPackIds=new List<string>();static string gwLegacyId;
  void PrepareGeneWorkbenchSave(){
   map=Find.CurrentMap;var w=GwSpawnBench();gwMade.Clear();var p=GwPawn();gwMade.Clear();var gene=GeneWorkbench.ExtractableGenes(p).First();
   for(int i=0;i<2;i++){var pack=GeneWorkbench.MakeGenepack(gene);w.Bank.innerContainer.TryAdd(pack);gwPackIds.Add(pack.ThingID);}
   var cap=ThingMaker.MakeThing(ThingDefOf.ArchiteCapsule);cap.stackCount=3;w.innerContainer.TryAdd(cap);
   w.Extraction.Select(p,gene);p.jobs.StopAll();w.Extraction.TryAccept(p);w.Extraction.ticksRemaining=12345;
   gwBenchId=w.ThingID;gwPawnId=p.ThingID;gwGene=gene.defName;gwTicksLeft=12345;gwBankCount=w.Bank.innerContainer.Count;
   var legacy=GwSpawnBench();gwMade.Clear();for(int i=0;i<2;i++){var pack=GeneWorkbench.MakeGenepack(gene);legacy.Bank.innerContainer.TryAdd(pack);gwLegacyPackIds.Add(pack.ThingID);}
   var cap2=ThingMaker.MakeThing(ThingDefOf.ArchiteCapsule);cap2.stackCount=2;legacy.innerContainer.TryAdd(cap2);gwLegacyId=legacy.ThingID;
  }
  // 存档写完、读档之前调用：把 gwLegacyId 那台的 lpGenepacks 改回旧的 innerContainer 并放到装配器容器前面（旧版本的写法），
  // 再往装配器容器里塞一份重复的基因组条目（模拟“带着 bug 读过又存过”的档）。
  public static void RewriteLegacyGeneWorkbench(){
   if(gwLegacyId==null)return;
   string path=GenFilePaths.FilePathForSavedGame("Lich-Roundtrip");var doc=new System.Xml.XmlDocument();doc.PreserveWhitespace=true;doc.Load(path);
   var node=doc.SelectNodes("//thing").OfType<System.Xml.XmlElement>().First(e=>e["id"]?.InnerText==gwLegacyId);
   var bank=node[GeneWorkbench.BankLabel];var asm=node["innerContainer"];
   var old=doc.CreateElement("innerContainer");foreach(System.Xml.XmlAttribute a in bank.Attributes)old.SetAttribute(a.Name,a.Value);while(bank.HasChildNodes)old.AppendChild(bank.FirstChild);node.ReplaceChild(old,bank);
   node.RemoveChild(asm);node.InsertAfter(asm,old);
   var dup=(System.Xml.XmlElement)old.SelectSingleNode("innerList/li").CloneNode(true);asm["innerList"].AppendChild(dup);
   doc.Save(path);
  }
  void CheckGeneWorkbenchAfterLoad(){
   map=Find.CurrentMap;var w=(Building_GeneWorkbench)map.listerThings.ThingsOfDef(GeneWorkbench.Def).Single(t=>t.ThingID==gwBenchId);var c=w.Extraction;
   Check(c.Working && c.Contained?.ThingID==gwPawnId && c.Selected==c.Contained && c.SelectedGene?.defName==gwGene && Math.Abs(c.ticksRemaining-gwTicksLeft)<=10,"extraction in progress survives save/load (pawn inside, gene, time left)");
   Check(w.Bank.innerContainer.Count==gwBankCount && w.Bank.innerContainer.Select(t=>t.ThingID).OrderBy(x=>x).SequenceEqual(gwPackIds.OrderBy(x=>x)) && w.GetGenepacks(true,true).Count>=gwBankCount,"built-in gene bank survives save/load with the same genepacks");
   Check(w.innerContainer.Count==1 && w.innerContainer[0].def==ThingDefOf.ArchiteCapsule && w.innerContainer[0].stackCount==3,"assembler container keeps exactly its 3 archite capsules after save/load (no duplicated genepacks): "+w.innerContainer.ContentsString);
   var legacy=(Building_GeneWorkbench)map.listerThings.ThingsOfDef(GeneWorkbench.Def).Single(t=>t.ThingID==gwLegacyId);
   Check(legacy.Bank.innerContainer.Select(t=>t.ThingID).OrderBy(x=>x).SequenceEqual(gwLegacyPackIds.OrderBy(x=>x)),"legacy-format workbench: genepacks come back in the built-in bank: "+legacy.Bank.innerContainer.ContentsString);
   Check(legacy.innerContainer.Count==1 && legacy.innerContainer[0].def==ThingDefOf.ArchiteCapsule && legacy.innerContainer[0].stackCount==2,"legacy-format workbench: archite capsules are restored and the duplicated genepack entry is dropped: "+legacy.innerContainer.ContentsString);
   var all=Find.Maps.SelectMany(m=>ThingOwnerUtility.GetAllThingsRecursively(m,false)).Where(t=>t.def==ThingDefOf.Genepack).ToList();
   Check(all.GroupBy(t=>t.thingIDNumber).All(g=>g.Count()==1),"no genepack exists twice after loading");
   var p=c.Contained;c.Cancel();Check(p.Spawned,"loaded extraction can be cancelled");p.Destroy();
   foreach(var b in new[]{w,legacy}){foreach(var t in b.Bank.innerContainer.ToList())t.Destroy();foreach(var t in b.innerContainer.ToList())t.Destroy();b.Destroy();}
  }
 }
}