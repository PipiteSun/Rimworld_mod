using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace LichPhylactery {
 // 1.26.0 基因工作台：5×5，一台顶原版基因提取器 + 基因装配器 + 基因库。
 // 装配沿用原版 Building_GeneAssembler（复杂度、代谢、古菌、时长全按原版）；自带 8 格基因库（原版 CompGenepackContainer）；
 // 定向提取：选一个人、选一个基因，被提取者进入工作台 30000 刻（同原版提取器），只产出含这一个基因的基因组。
 public static class GeneWorkbench {
  public const int ExtractTicks=30000,NoPowerEjectTicks=60000;
  static ThingDef def;static JobDef enter,carry;
  public static ThingDef Def=>def??(def=DefDatabase<ThingDef>.GetNamed("LP_GeneWorkbench"));
  public static JobDef EnterJob=>enter??(enter=DefDatabase<JobDef>.GetNamed("LP_EnterGeneWorkbench"));
  public static JobDef CarryJob=>carry??(carry=DefDatabase<JobDef>.GetNamed("LP_CarryToGeneWorkbench"));
  public static CompGeneWorkbench Comp(Thing t)=>(t as Building_GeneWorkbench)?.Extraction;
  // 原版提取器不会取的基因：古菌基因（不可提取）、黑色素（肤色）基因、单个基因代谢超出 -5～5。
  public static bool Extractable(GeneDef g)=>g!=null && g.biostatArc==0 && g.endogeneCategory!=EndogeneCategory.Melanin && g.biostatMet>=GeneTuning.BiostatRange.min && g.biostatMet<=GeneTuning.BiostatRange.max;
  public static List<GeneDef> ExtractableGenes(Pawn p)=>p?.genes==null?new List<GeneDef>():p.genes.GenesListForReading.Select(g=>g.def).Where(Extractable).Distinct().ToList();
  // 人员资格（不管工作台是否空闲）。
  public static AcceptanceReport CanExtractFrom(Pawn p){
   if(p==null || p.Dead || !p.RaceProps.Humanlike || p.IsQuestLodger())return false;
   if(!p.IsColonist && !p.IsSlaveOfColony && !p.IsPrisonerOfColony)return false;
   if(p.genes==null || !p.genes.GenesListForReading.Any())return "没有基因";
   if(p.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating))return "基因重构中，再提取会致死";
   if(p.health.hediffSet.HasHediff(HediffDefOf.XenogerminationComa))return "异种植入昏迷中";
   if(!ExtractableGenes(p).Any())return "没有可提取的基因（古菌基因无法提取）";
   return true;
  }
  public static string GeneLabel(Pawn p,GeneDef g){
   bool xeno=p.genes.Xenogenes.Any(x=>x.def==g),endo=p.genes.Endogenes.Any(x=>x.def==g);
   return g.LabelCap+"（"+(xeno && endo?"种系+异种":xeno?"异种":"种系")+"；复杂度 "+g.biostatCpx+"，代谢 "+g.biostatMet.ToStringWithSign()+"）";
  }
  // 只装这一个基因的基因组。
  public static Genepack MakeGenepack(GeneDef gene){var pack=(Genepack)ThingMaker.MakeThing(ThingDefOf.Genepack);pack.Initialize(new List<GeneDef>{gene});return pack;}
  // 搬运基因组入库时，原版会把东西放进建筑的 GetDirectlyHeldThings（对装配器来说是古菌胶囊容器），这里改投到工作台的基因库。
  [ThreadStatic] public static Thing depositTarget;
  // 1.27.2 存档修复：1.26～1.27.1 的原版基因库组件和装配器都把容器存成同名的 innerContainer，读档时两边都读到第一个（基因库的），
  // 基因组被读两遍（重复注册、可刷复制），装配器真正的超凡胶囊没人读而丢失。新版基因库改存 lpGenepacks；旧档在读档前改 XML：
  // 第一个 innerContainer 改名为 lpGenepacks，装配器那份里混进来的基因组条目删掉（原版装配器只放超凡胶囊）。返回删掉的条目数。
  public const string BankLabel="lpGenepacks";
  public static int MigrateLegacyNode(System.Xml.XmlNode node){
   if(node==null || node[BankLabel]!=null)return 0;
   var olds=node.ChildNodes.OfType<System.Xml.XmlElement>().Where(e=>e.Name=="innerContainer").ToList();
   if(olds.Count<2)return 0;
   var first=olds[0];var renamed=node.OwnerDocument.CreateElement(BankLabel);
   foreach(System.Xml.XmlAttribute a in first.Attributes)renamed.SetAttribute(a.Name,a.Value);
   while(first.HasChildNodes)renamed.AppendChild(first.FirstChild);
   node.ReplaceChild(renamed,first);
   int removed=0;var list=olds[1]["innerList"];
   if(list!=null)foreach(var li in list.ChildNodes.OfType<System.Xml.XmlElement>().Where(li=>li["def"]?.InnerText==ThingDefOf.Genepack.defName).ToList()){list.RemoveChild(li);removed++;}
   return removed;
  }
  // 已经带着旧 bug 读过档的游戏，复制出来的基因组可能已经被倒在地上或搬进别的库：读档后按 ThingID 只留一份（优先留在基因库里的）。
  public static int RemoveDuplicateGenepacks(){
   int removed=0;
   foreach(var map in Find.Maps){
    var packs=ThingOwnerUtility.GetAllThingsRecursively(map,false).Where(t=>t.def==ThingDefOf.Genepack && !t.Destroyed).ToList();
    foreach(var g in packs.GroupBy(t=>t.thingIDNumber).Where(g=>g.Count()>1)){
     var keep=g.OrderBy(t=>t.ParentHolder is CompGenepackContainer?0:t.Spawned?1:2).First();
     foreach(var extra in g.Where(t=>t!=keep).ToList()){extra.holdingOwner?.Remove(extra);if(!extra.Destroyed)extra.Destroy();removed++;}
    }
   }
   if(removed>0)Log.Message("[DND边缘大陆] 基因工作台存档修复：清除了 "+removed+" 个重复的基因组（旧版本读档 bug 复制出来的）。");
   return removed;
  }
 }

 // 基因工作台的基因库：就是原版基因库组件，只把存档标签从 innerContainer 改成 lpGenepacks，避免和装配器的 innerContainer 撞名。
 public class CompLP_GenepackContainer : CompGenepackContainer {
  public override void PostExposeData(){
   Scribe_Deep.Look(ref innerContainer,GeneWorkbench.BankLabel,this);Scribe_Collections.Look(ref leftToLoad,"leftToLoad",LookMode.Reference);Scribe_Values.Look(ref autoLoad,"autoLoad",true);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(innerContainer==null)innerContainer=new ThingOwner<Thing>(this);if(leftToLoad==null)leftToLoad=new List<Thing>();leftToLoad.RemoveAll(t=>t==null);}
  }
 }
 public class GeneWorkbenchSaveFix : GameComponent {
  public GeneWorkbenchSaveFix(Game game){}
  public override void LoadedGame(){try{GeneWorkbench.RemoveDuplicateGenepacks();}catch(Exception e){Log.Error("[DND边缘大陆] 基因组去重失败："+e);}}
 }

 [StaticConstructorOnStartup]
 public class Building_GeneWorkbench : Building_GeneAssembler {
  CompGeneWorkbench extraction;CompGenepackContainer bank;
  public CompGeneWorkbench Extraction=>extraction??(extraction=GetComp<CompGeneWorkbench>());
  public CompGenepackContainer Bank=>bank??(bank=GetComp<CompGenepackContainer>());
  public override void ExposeData(){
   if(Scribe.mode==LoadSaveMode.LoadingVars){int n=GeneWorkbench.MigrateLegacyNode(Scribe.loader.curXmlParent);if(n>0)Log.Message("[DND边缘大陆] 基因工作台：旧存档里装配器容器混入的 "+n+" 个重复基因组已在读档前剔除。");}
   base.ExposeData();
  }
  protected override void Tick(){
   base.Tick();
   if(this.IsHashIntervalTick(250)){
    if(Extraction.Working)((CompPowerTrader)PowerComp).PowerOutput=-PowerComp.Props.PowerConsumption;
    // 工作台是 Normal 刻建筑，原版只在 TickRare 里修复/老化库里的基因组，这里手动补上。
    Bank.CompTickRare();
   }
  }
  public override void DynamicDrawPhaseAt(DrawPhase phase,Vector3 drawLoc,bool flip=false){base.DynamicDrawPhaseAt(phase,drawLoc,flip);var p=Extraction?.Contained;if(p!=null)p.Drawer.renderer.DynamicDrawPhaseAt(phase,drawLoc+Extraction.PawnDrawOffset,null,true);}
  public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn){
   foreach(var o in base.GetFloatMenuOptions(selPawn))yield return o;
   var c=Extraction;if(c.Selected!=selPawn || c.Working)yield break;
   if(!selPawn.CanReach(this,PathEndMode.InteractionCell,Danger.Deadly)){yield return new FloatMenuOption("无法前往基因工作台：没有路径",null);yield break;}
   yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("进入基因工作台接受提取（"+c.SelectedGene.label+"）",()=>c.OrderEnter()),selPawn,this);
  }
 }

 public class CompProperties_GeneWorkbench : CompProperties {public CompProperties_GeneWorkbench(){compClass=typeof(CompGeneWorkbench);}}

 // 定向提取的状态与被提取者容器。放在组件上：原版装配器的 IThingHolder 已被古菌胶囊容器占用；组件容器会被地图的持有树收录（与原版基因库同理）。
 [StaticConstructorOnStartup]
 public class CompGeneWorkbench : ThingComp, IThingHolder, IThingHolderWithDrawnPawn {
  public ThingOwner<Pawn> holder;Pawn selected;GeneDef gene;public int ticksRemaining,powerCutTicks;bool working;
  [Unsaved] Sustainer sustainer;[Unsaved] Effecter progressBar;
  static readonly Texture2D CancelIcon=ContentFinder<Texture2D>.Get("UI/Designators/Cancel");
  public static readonly Texture2D ExtractIcon=ContentFinder<Texture2D>.Get("GeneWorkbench/ExtractGene",false)??BaseContent.BadTex;
  public CompGeneWorkbench(){holder=new ThingOwner<Pawn>(this);}
  public Building_GeneWorkbench Bench=>(Building_GeneWorkbench)parent;
  public Pawn Selected=>selected;public GeneDef SelectedGene=>gene;public bool Working=>working;
  public Pawn Contained=>holder.Count>0?holder[0]:null;
  bool PowerOn=>parent.GetComp<CompPowerTrader>()?.PowerOn??true;
  IntVec3 DropCell=>parent.def.hasInteractionCell?parent.InteractionCell:parent.Position;
  public Vector3 PawnDrawOffset=>Vector3.zero;
  public float HeldPawnDrawPos_Y=>parent.DrawPos.y+0.03658537f;
  public float HeldPawnBodyAngle=>parent.Rotation.Opposite.AsAngle;
  public PawnPosture HeldPawnPosture=>PawnPosture.LayingOnGroundFaceUp;
  public void GetChildHolders(List<IThingHolder> outChildren)=>ThingOwnerUtility.AppendThingHoldersFromThings(outChildren,GetDirectlyHeldThings());
  public ThingOwner GetDirectlyHeldThings()=>holder;

  public AcceptanceReport CanAccept(Pawn p){
   var r=GeneWorkbench.CanExtractFrom(p);if(!r.Accepted)return r;
   if(!PowerOn)return "NoPower".Translate().CapitalizeFirst();
   if(holder.Count>0 && Contained!=p)return "Occupied".Translate();
   if(selected!=null && selected!=p)return "已指定"+selected.LabelShort;
   if(gene!=null && !GeneWorkbench.ExtractableGenes(p).Contains(gene))return p.LabelShort+"已没有"+gene.label;
   return true;
  }
  // 小人自己能走的（殖民者、奴隶）直接下令走过来；囚犯、倒地或不能走的由搬运工搬过来（工作类型：搬运）。
  public static bool NeedsCarrying(Pawn p)=>p.IsPrisonerOfColony || p.Downed || !p.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
  public void Select(Pawn p,GeneDef g){
   Clear();selected=p;gene=g;
   if(!NeedsCarrying(p))OrderEnter();
   else Messages.Message("等待搬运工把"+p.LabelShort+"送到基因工作台。",parent,MessageTypeDefOf.NeutralEvent,false);
  }
  public void OrderEnter(){if(selected!=null && !NeedsCarrying(selected) && selected.Spawned)selected.jobs.TryTakeOrderedJob(JobMaker.MakeJob(GeneWorkbench.EnterJob,parent),JobTag.Misc);}
  public bool TryAccept(Pawn p){
   if(p!=selected || working || !CanAccept(p).Accepted)return false;
   bool reselect=p.DeSpawnOrDeselect();
   if(!holder.TryAddOrTransfer(p))return false;
   working=true;ticksRemaining=GeneWorkbench.ExtractTicks;powerCutTicks=0;
   if(reselect)Find.Selector.Select(p,false,false);
   return true;
  }
  void Clear(){
   var p=selected;selected=null;gene=null;working=false;powerCutTicks=0;sustainer=null;
   if(holder.Count>0)holder.TryDropAll(DropCell,parent.Map,ThingPlaceMode.Near);
   if(p!=null && p.Spawned && (p.CurJobDef==GeneWorkbench.EnterJob))p.jobs.EndCurrentJob(JobCondition.InterruptForced);
   if(progressBar!=null){progressBar.Cleanup();progressBar=null;}
  }
  public void Cancel()=>Clear();
  public override void CompTick(){
   base.CompTick();
   holder.DoTick();
   if(!working){
    if(selected!=null && (selected.Dead || selected.Destroyed || (parent.IsHashIntervalTick(250) && !GeneWorkbench.CanExtractFrom(selected).Accepted)))Clear();
    return;
   }
   if(Contained==null){Clear();return;}
   if(PowerOn){
    TickEffects();
    if(--ticksRemaining<=0)Finish();
    return;
   }
   if(++powerCutTicks>=GeneWorkbench.NoPowerEjectTicks){Messages.Message("GeneExtractorNoPowerEjectedMessage".Translate(Contained.Named("PAWN")),Contained,MessageTypeDefOf.NegativeEvent,false);Clear();}
  }
  void TickEffects(){
   if(sustainer==null || sustainer.Ended)sustainer=SoundDefOf.GeneExtractor_Working.TrySpawnSustainer(SoundInfo.InMap(parent,MaintenanceType.PerTick));else sustainer.Maintain();
   if(progressBar==null)progressBar=EffecterDefOf.ProgressBarAlwaysVisible.Spawn();
   progressBar.EffectTick(new TargetInfo(parent.Position+IntVec3.North.RotatedBy(parent.Rotation)*3,parent.Map),TargetInfo.Invalid);
   var mote=((SubEffecter_ProgressBar)progressBar.children[0]).mote;if(mote!=null)mote.progress=1f-Mathf.Clamp01(ticksRemaining/(float)GeneWorkbench.ExtractTicks);
  }
  // 结束：只装选定基因的基因组进自带基因库（满了就放在交互格旁），套用原版提取后状态（基因缺失冲击 + 12～20 天基因重构），囚犯/奴隶得原版“基因被采集”心情。基因不会从人身上移除。
  public Genepack Finish(){
   var p=Contained;var g=gene;Genepack pack=null;
   if(p!=null && g!=null){
    pack=GeneWorkbench.MakeGenepack(g);
    GeneUtility.ExtractXenogerm(p,Mathf.RoundToInt(60000f*GeneTuning.GeneExtractorRegrowingDurationDaysRange.RandomInRange));
    if(!p.Dead && (p.IsPrisonerOfColony || p.IsSlaveOfColony))p.needs?.mood?.thoughts?.memories?.TryGainMemory(ThoughtDefOf.XenogermHarvested_Prisoner);
    var bank=Bench.Bank;
    if(bank==null || bank.Full || !bank.innerContainer.TryAdd(pack)){GenPlace.TryPlaceThing(pack,DropCell,parent.Map,ThingPlaceMode.Near);Messages.Message("基因工作台的基因库已满，基因组放在了工作台旁边。",pack,MessageTypeDefOf.CautionInput,false);}
    Messages.Message("GeneExtractionComplete".Translate(p.Named("PAWN"))+"："+g.LabelCap,new LookTargets(p,parent),MessageTypeDefOf.PositiveEvent);
    SoundDefOf.GeneAssembler_Complete.PlayOneShot(SoundInfo.InMap(parent));
   }
   Clear();return pack;
  }
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   if(parent.Faction!=Faction.OfPlayer)yield break;
   if(working || selected!=null){
    yield return new Command_Action{defaultLabel=working?"取消提取":"取消指定",defaultDesc=working?"中止提取，被提取者离开工作台。不会产出基因组，也不会施加提取后状态。":"取消对"+selected.LabelShort+"的提取安排。",icon=CancelIcon,activateSound=SoundDefOf.Designate_Cancel,action=Cancel};
    if(working && DebugSettings.ShowDevGizmos)yield return new Command_Action{defaultLabel="DEV: 立即完成提取",action=()=>Finish()};
    yield break;
   }
   var cmd=new Command_Action{defaultLabel="指定提取基因",defaultDesc="选一个殖民者、奴隶或囚犯，再选他身上的一个基因，提取成只含这个基因的基因组（存入工作台自带的基因库）。\n\n需要电力，耗时与原版基因提取器相同（半天）；基因不会从人身上移除，被提取者会得到原版的基因缺失冲击和12～20天的基因重构。古菌基因与肤色基因无法提取；正在基因重构的人不能再次提取。",icon=ExtractIcon,action=OpenPawnMenu};
   if(!PowerOn)cmd.Disable("NoPower".Translate().CapitalizeFirst());
   yield return cmd;
  }
  void OpenPawnMenu(){
   var list=new List<FloatMenuOption>();
   foreach(var p in parent.Map.mapPawns.AllPawnsSpawned.Where(x=>x.RaceProps.Humanlike && (x.IsColonist || x.IsSlaveOfColony || x.IsPrisonerOfColony)).OrderBy(x=>x.IsColonist?0:1).ThenBy(x=>x.LabelShort)){
    var r=CanAccept(p);string label=p.LabelShortCap+"，"+p.genes?.XenotypeLabelCap+(p.IsPrisonerOfColony?"（囚犯）":p.IsSlaveOfColony?"（奴隶）":"");
    if(!r.Accepted){if(!r.Reason.NullOrEmpty())list.Add(new FloatMenuOption(label+"："+r.Reason,null,p,Color.white));continue;}
    var pawn=p;list.Add(new FloatMenuOption(label,()=>OpenGeneMenu(pawn),pawn,Color.white));
   }
   if(!list.Any())list.Add(new FloatMenuOption("NoExtractablePawns".Translate(),null));
   Find.WindowStack.Add(new FloatMenu(list));
  }
  void OpenGeneMenu(Pawn p){
   var list=GeneWorkbench.ExtractableGenes(p).OrderByDescending(g=>g.biostatCpx).ThenBy(g=>g.label).Select(g=>new FloatMenuOption(GeneWorkbench.GeneLabel(p,g),()=>Select(p,g),g.Icon,g.IconColor)).ToList();
   if(!list.Any())list.Add(new FloatMenuOption("没有可提取的基因",null));
   Find.WindowStack.Add(new FloatMenu(list));
  }
  public override string CompInspectStringExtra(){
   if(working && Contained!=null)return "从"+Contained.LabelShort+"提取："+gene?.label+"\n"+(PowerOn?"DurationLeft".Translate(ticksRemaining.ToStringTicksToPeriod()).Resolve():("提取暂停（断电），"+(GeneWorkbench.NoPowerEjectTicks-powerCutTicks).ToStringTicksToPeriod()+"后弹出").Colorize(ColorLibrary.RedReadable));
   if(selected!=null)return "WaitingForPawn".Translate(selected.Named("PAWN")).Resolve()+"（提取："+gene?.label+"）";
   return null;
  }
  public override void PostDrawExtraSelectionOverlays(){if(!working && selected!=null && selected.Map==parent.Map)GenDraw.DrawLineBetween(parent.TrueCenter(),selected.TrueCenter());}
  public override void PostDeSpawn(Map map,DestroyMode mode=DestroyMode.Vanish){
   if(progressBar!=null){progressBar.Cleanup();progressBar=null;}sustainer=null;
   if(holder.Count>0)holder.TryDropAll(parent.def.hasInteractionCell?parent.InteractionCell:parent.Position,map,ThingPlaceMode.Near);
   selected=null;gene=null;working=false;
  }
  public override void PostExposeData(){
   Scribe_Deep.Look(ref holder,"lpExtractHolder",this);Scribe_References.Look(ref selected,"lpExtractPawn");Scribe_Defs.Look(ref gene,"lpExtractGene");
   Scribe_Values.Look(ref working,"lpExtractWorking");Scribe_Values.Look(ref ticksRemaining,"lpExtractTicks");Scribe_Values.Look(ref powerCutTicks,"lpExtractPowerCut");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(holder==null)holder=new ThingOwner<Pawn>(this);if(gene==null && selected!=null){selected=null;working=false;}}
  }
 }

 // 被提取者自己走进工作台（同原版 JobDriver_EnterBuilding；不预约建筑，避免和装配研究员抢预约）。
 public class JobDriver_EnterGeneWorkbench : JobDriver {
  CompGeneWorkbench Comp=>GeneWorkbench.Comp(job.targetA.Thing);
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);this.FailOn(()=>Comp==null || Comp.Selected!=pawn || !Comp.CanAccept(pawn).Accepted);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.InteractionCell);
   yield return Toils_General.WaitWith(TargetIndex.A,60,true);
   yield return Toils_General.Do(()=>Comp.TryAccept(pawn));
  }
 }
 // 搬运工把囚犯/倒地者送进工作台（同原版 JobDriver_CarryToBuilding）。
 public class JobDriver_CarryToGeneWorkbench : JobDriver {
  CompGeneWorkbench Comp=>GeneWorkbench.Comp(job.targetA.Thing);Pawn Takee=>(Pawn)job.targetB.Thing;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetB,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDestroyedOrNull(TargetIndex.B);this.FailOnDespawnedNullOrForbidden(TargetIndex.A);this.FailOn(()=>Comp==null || Comp.Selected!=Takee || Comp.Working || !Comp.CanAccept(Takee).Accepted);
   yield return Toils_Goto.GotoThing(TargetIndex.B,PathEndMode.OnCell);
   yield return Toils_Haul.StartCarryThing(TargetIndex.B);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.InteractionCell);
   yield return Toils_General.WaitWith(TargetIndex.A,60,true);
   yield return Toils_General.Do(()=>Comp.TryAccept(Takee));
  }
 }
 public class WorkGiver_CarryToGeneWorkbench : WorkGiver_Scanner {
  public override ThingRequest PotentialWorkThingRequest=>ThingRequest.ForDef(GeneWorkbench.Def);
  public override PathEndMode PathEndMode=>PathEndMode.InteractionCell;
  public override bool ShouldSkip(Pawn pawn,bool forced=false)=>!ModsConfig.BiotechActive;
  public override bool HasJobOnThing(Pawn pawn,Thing t,bool forced=false){
   var c=GeneWorkbench.Comp(t);var p=c?.Selected;
   if(p==null || c.Working || p==pawn || p.Map!=pawn.Map || !p.Spawned || !CompGeneWorkbench.NeedsCarrying(p) || !c.CanAccept(p).Accepted)return false;
   if(t.IsForbidden(pawn) || pawn.Map.designationManager.DesignationOn(t,DesignationDefOf.Deconstruct)!=null)return false;
   return pawn.CanReach(t,PathEndMode.InteractionCell,Danger.Deadly) && pawn.CanReserveAndReach(p,PathEndMode.OnCell,Danger.Deadly,1,-1,null,forced);
  }
  public override Job JobOnThing(Pawn pawn,Thing t,bool forced=false){var job=JobMaker.MakeJob(GeneWorkbench.CarryJob,t,GeneWorkbench.Comp(t).Selected);job.count=1;return job;}
 }
 // 原版 WorkGiver_CreateXenogerm 只找原版装配器这一个定义；工作台用同一套逻辑。
 public class WorkGiver_CreateXenogermWorkbench : WorkGiver_CreateXenogerm {
  public override ThingRequest PotentialWorkThingRequest=>ThingRequest.ForDef(GeneWorkbench.Def);
 }

 // 装配器的基因组库：在相连基因库之外，加上工作台自带的基因库。
 [HarmonyPatch(typeof(Building_GeneAssembler),nameof(Building_GeneAssembler.GetGenepacks))]
 public static class GeneWorkbenchGetGenepacks {
  static void Postfix(Building_GeneAssembler __instance,bool includePowered,bool includeUnpowered,List<Genepack> __result){
   if(!(__instance is Building_GeneWorkbench w) || w.Bank==null || __result==null)return;
   bool on=w.PowerComp==null || ((CompPowerTrader)w.PowerComp).PowerOn;
   if((includePowered && on) || (includeUnpowered && !on))foreach(var p in w.Bank.ContainedGenepacks)if(!__result.Contains(p))__result.Add(p);
  }
 }
 // 装配界面里“从基因库弹出”要找得到工作台自带的库。
 [HarmonyPatch(typeof(Building_GeneAssembler),nameof(Building_GeneAssembler.GetGeneBankHoldingPack))]
 public static class GeneWorkbenchHoldingPack {
  static void Postfix(Building_GeneAssembler __instance,Genepack pack,ref CompGenepackContainer __result){
   if(__result==null && __instance is Building_GeneWorkbench w && w.Bank!=null && w.Bank.ContainedGenepacks.Contains(pack))__result=w.Bank;
  }
 }
 // 装配进行中原版每 180 刻检查选中的基因组还在不在相连基因库里；工作台自带的库也算。
 [HarmonyPatch(typeof(Building_GeneAssembler),"CheckAllContainersValid")]
 public static class GeneWorkbenchContainersValid {
  static readonly AccessTools.FieldRef<Building_GeneAssembler,List<Genepack>> packs=AccessTools.FieldRefAccess<Building_GeneAssembler,List<Genepack>>("genepacksToRecombine");
  static readonly System.Reflection.MethodInfo reset=AccessTools.Method(typeof(Building_GeneAssembler),"Reset");
  static bool Prefix(Building_GeneAssembler __instance){
   if(!(__instance is Building_GeneWorkbench w))return true;
   var list=packs(__instance);if(list.NullOrEmpty())return false;
   var have=new HashSet<Genepack>(w.GetGenepacks(true,true));
   if(list.All(have.Contains))return false;
   Messages.Message("MessageXenogermCancelledMissingPack".Translate(w),w,MessageTypeDefOf.NegativeEvent);reset.Invoke(w,null);return false;
  }
 }
 // 搬运基因组入库：工作台的 GetDirectlyHeldThings 是装配器的古菌胶囊容器，入库那一步改投到工作台的基因库。
 [HarmonyPatch(typeof(JobDriver_CarryGenepackToContainer),"MakeNewToils")]
 public static class GeneWorkbenchDepositGenepack {
  static IEnumerable<Toil> Postfix(IEnumerable<Toil> toils,JobDriver_CarryGenepackToContainer __instance){
   var list=toils.ToList();
   var target=__instance.job.targetB.Thing as Building_GeneWorkbench;
   if(target!=null && list.Count>0){var last=list[list.Count-1];var init=last.initAction;last.initAction=()=>{GeneWorkbench.depositTarget=target;try{init?.Invoke();}finally{GeneWorkbench.depositTarget=null;}};}
   return list;
  }
 }
 [HarmonyPatch(typeof(ThingOwnerUtility),nameof(ThingOwnerUtility.TryGetInnerInteractableThingOwner))]
 public static class GeneWorkbenchInnerOwner {
  static void Postfix(Thing thing,ref ThingOwner __result){if(GeneWorkbench.depositTarget!=null && thing==GeneWorkbench.depositTarget && thing is Building_GeneWorkbench w && w.Bank!=null)__result=w.Bank.innerContainer;}
 }
}
