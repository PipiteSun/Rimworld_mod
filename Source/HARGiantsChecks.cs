using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.44.0 双头巨人、独眼巨人（组名 GiantKinHAR：Test.ps1 -Only GiantKinHAR）。按 Peter 的要求从简：主程序集 14 条 + HAR 联动程序集 1 条。
 // 1.102.0 起两族是原版人类 + 异种型：有没有 HAR（Test.ps1 -NoHAR）都跑全部检查；有 HAR 时另跑联动程序集的一条。
 public partial class LichChecks {
  static PawnKindDef HARGiantKind(string n)=>DefDatabase<PawnKindDef>.GetNamed(n);
  Pawn HARGiantPawn(string kind,Faction faction,float age=25f){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(HARGiantKind(kind),faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:age,fixedChronologicalAge:age));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_AddedPart || h.def.isBad).ToList())p.health.RemoveHediff(h);
   foreach(var m in p.health.hediffSet.GetMissingPartsCommonAncestors().ToList())p.health.RestorePart(m.Part);
   return p;
  }
  Pawn HARGiantSpawn(Pawn p){GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(altar.Position,map,6),map);return p;}
  static BodyPartRecord HARPart(Pawn p,string label)=>p.RaceProps.body.AllParts.First(x=>x.untranslatedCustomLabel==label);
  static float HARLevel(Pawn p,PawnCapacityDef c){p.health.capacities.Notify_CapacityLevelsDirty();return p.health.capacities.GetLevel(c);}
  static Vector3 EttinHeadOffsetNow(Pawn p,Rot4 r)=>p.Drawer.renderer.BaseHeadOffsetAt(r);
  static Type HARGiantsBridge=>GenTypes.GetTypeInAnyAssembly("LichPhylactery.HARBridge.BridgeChecks");
  void RunHARGiantsBridge(){
   var m=HARGiantsBridge?.GetMethod("RunGiants");if(m==null)throw new Exception("HAR bridge RunGiants missing");
   try{checks+=(int)m.Invoke(null,null);}
   catch(TargetInvocationException e){if(e.InnerException==null)throw;System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
  }

  void CheckGiantKinHAR(){
   bool har=ModsConfig.IsActive(HARId);
   // 1 加载（1.102.0：两族是原版人类 + 异种型，有没有 HAR 都加载；旧的 HAR 种族没有了）
   Check(Ettin.RaceDef==null && Cyclops.RaceDef==null && DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_EttinKin")!=null && DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_CyclopsKin")!=null && EttinRaid.Kind!=null && CyclopsGuard.Kind!=null && Items.Thing(Ettin.Helm)!=null && EttinRaid.Kind.race==ThingDefOf.Human,"ettin and cyclops xenotypes, human pawn kinds and gear load with or without HAR (har="+har+"); the old HAR races are gone");
   if(har)RunHARGiantsBridge();
   worldBefore=new HashSet<Pawn>(Find.WorldPawns.AllPawnsAliveOrDead);
   var made=new List<Thing>();
   Pawn Add(Pawn p){made.Add(p);return p;}
   var e=Add(HARGiantSpawn(HARGiantPawn("LP_EttinColonist",Faction.OfPlayer)));var c=Add(HARGiantSpawn(HARGiantPawn("LP_CyclopsColonist",Faction.OfPlayer)));var human=Add(HARGiantSpawn(Person()));
   try{
    GiantKin.ForgetCensus();
    // 2 双头巨人：人类身体（一个头），第二个头是本源基因的外观节点；主头是四种普通头型之一；外观基因随机一个
    var core=Races124.Gene(Ettin.Core);var nodes=core.renderNodeProperties.OfType<PawnRenderNodeProperties_LPPart>().ToList();
    bool tex=nodes.All(n=>n.byHeadType?new[]{"A","B","F"}.All(v=>ContentFinder<Texture2D>.Get(n.texPath+v+"_south",false)!=null):ContentFinder<Texture2D>.Get(n.texPath+"_south",false)!=null);
    Check(e.def==ThingDefOf.Human && Ettin.Is(e) && Ettin.BothHeads(e) && Ettin.HeadsLeft(e)==2 && Ettin.Heads.Contains(e.story.headType.defName) && e.story.bodyType==BodyTypeDefOf.Hulk && nodes.Count==4 && tex && Near(EttinHeadOffsetNow(e,Rot4.South).x-EttinHeadOffsetNow(human,Rot4.South).x,.26f)
     && e.genes.Xenotype?.defName=="LP_EttinKin" && Ettin.SkinGenes.Count(n=>e.genes.GetGene(Races124.Gene(n))!=null)==1 && Near(HARLevel(e,PawnCapacityDefOf.Consciousness),1f) && !Items.Has(e,Ettin.LoneHead),"ettin: human + ettin kin, average head type, hulk body; second head / hair / helm half are gene render nodes (textures found), main head shifted aside; one skin gene, full consciousness");
    // 3 打掉一个头不死：真伤害打烂头 → 第二个头顶上：还活着、孤首、头上没有缺失、意识照常、视力 75%、轮流值守失效；夺心魔吞一颗大脑也一样
    var head=e.RaceProps.body.AllParts.First(x=>x.def==BodyPartDefOf.Head);
    // 一下重击把头打掉（多次小伤会先打坏眼睛、累积到全身致死伤害，那是原版的死法，不算“丢了一个头”）。
    e.TakeDamage(new DamageInfo(DamageDefOf.Cut,500,999,-1,null,head));if(!Ettin.Lone(e) && !e.Dead)e.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,e,head),head);
    // 伤口的疼痛会照原版再压低意识：比较前先去掉疼痛来源。
    bool lostAlive=!e.Dead,headBack=!e.health.hediffSet.PartIsMissing(head);foreach(var h in e.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())e.health.RemoveHediff(h);foreach(var h in e.health.hediffSet.hediffs.OfType<Hediff_MissingPart>())h.IsFresh=false;
    foreach(var h in e.health.hediffSet.hediffs.Where(h=>h.def==HediffDefOf.BloodLoss).ToList())e.health.RemoveHediff(h);e.health.hediffSet.DirtyCache();
    float lcon=HARLevel(e,PawnCapacityDefOf.Consciousness),lsight=HARLevel(e,PawnCapacityDefOf.Sight),leat=HARLevel(e,DefDatabase<PawnCapacityDef>.GetNamed("Eating"));
    var flayer=Add(HARGiantSpawn(Person()));var victim=Add(HARGiantSpawn(HARGiantPawn("LP_EttinColonist",null)));
    victim.health.AddHediff(HediffDefOf.Anesthetic);victim.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic).Severity=1f;victim.health.CheckForStateChange(null,null);
    float gained=MindFlayer.Devour(flayer,victim);
    Check(lostAlive && headBack && !e.Dead && Ettin.Lone(e) && Items.Has(e,Ettin.LoneHead) && Near(lcon,1f) && Near(lsight,.75f) && Near(leat,1f) && !Ettin.WatchActive(e)
     && gained>0 && !victim.Dead && Ettin.HeadsLeft(victim)==1 && Items.Has(victim,Ettin.LoneHead) && victim.health.hediffSet.GetBrain()!=null,"losing one head is not fatal: the second head takes over (lone head, no missing head) [alive "+lostAlive+" back "+headBack+" dead "+e.Dead+" lone "+Ettin.Lone(e)+" cause "+(e.Dead?e.health.hediffSet.hediffs.Where(h=>h.Part!=null).Select(h=>h.def.defName+"@"+h.Part.def.defName).ToCommaList():"")+" victim dead "+victim.Dead+" heads "+Ettin.HeadsLeft(victim)+"], consciousness "+lcon.ToStringPercent()+", sight "+lsight.ToStringPercent()+", eating "+leat.ToStringPercent()+", watch off; a mind flayer devouring one brain only costs that head");
    // 4 两个头都没了才死；两个头都在时意识下限 60%
    var e3=Add(HARGiantSpawn(HARGiantPawn("LP_EttinColonist",Faction.OfPlayer)));var brain=e3.health.hediffSet.GetBrain();
    e3.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.Cut,e3,brain),brain,null);var hb=e3.health.hediffSet.hediffs.Last(h=>h.Part==brain);hb.Severity=brain.def.GetMaxHealth(e3)*.9f;e3.health.hediffSet.DirtyCache();
    float floor=PawnCapacityUtility.CalculateTagEfficiency(e3.health.hediffSet,BodyPartTagDefOf.ConsciousnessSource);
    e.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,e,head),head);
    Check(e.Dead && Near(floor,.6f),"an ettin dies only when both heads are gone; with both heads a badly hurt brain still gives 60% (brain "+floor.ToStringPercent()+")");
    // 5 轮流值守：睡眠下降 ×0.5；人类装上无效
    var e2=Add(HARGiantSpawn(HARGiantPawn("LP_EttinColonist",Faction.OfPlayer)));
    float eRest=e2.GetStatValue(StatDefOf.RestFallRateFactor),hRest0=human.GetStatValue(StatDefOf.RestFallRateFactor);
    human.genes.AddGene(Races124.Gene(Ettin.WatchGene),true);human.genes.AddGene(Races124.Gene(Ettin.GripGene),true);
    float hRest=human.GetStatValue(StatDefOf.RestFallRateFactor);
    var foe=Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,MarilithEnemy,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true)));GenSpawn.Spawn(foe,CellFinder.RandomClosewalkCellNear(e2.Position,map,3),map); // 放在 10 格以内（原来按祭坛附近随机，偶尔超过 10 格）
    Check(Ettin.WatchActive(e2) && Near(eRest,.5f) && Near(hRest,hRest0) && !Ettin.WatchActive(human) && Ettin.FoeNear(e2) && Races124.Gene(Ettin.WatchGene).description.Contains("需要两个头"),"tag-team watch: rest fall x"+eRest.ToString("0.##")+" for an ettin, unchanged for a human with the gene; a foe within 10 cells is noticed");
    foe.Destroy();
    // 6 左右开弓：背包 1 把副手；人类装上 0 把；一只手没了 0 把
    var sword=MarilithWeapon("MeleeWeapon_LongSword",QualityCategory.Normal);e2.equipment.DestroyAllEquipment();e2.equipment.AddEquipment(sword);
    foreach(var w in new[]{MarilithWeapon("MeleeWeapon_Knife",QualityCategory.Normal),MarilithWeapon("MeleeWeapon_Gladius",QualityCategory.Good)})e2.inventory.innerContainer.TryAdd(w);
    var offs=Marilith.Offhands(e2);int humanSlots=Marilith.OffhandSlots(human);
    var hand=e2.RaceProps.body.AllParts.First(x=>x.def==BodyPartDefOf.Hand);e2.health.AddHediff(HediffDefOf.MissingBodyPart,hand);int oneHand=Marilith.OffhandSlots(e2);e2.health.RestorePart(hand);
    Check(offs.Count==1 && offs[0].def.defName=="MeleeWeapon_Gladius" && humanSlots==0 && oneHand==0 && Marilith.OffhandSlots(e2)==1,"twin grip: one off-hand from the backpack (the better gladius); none for a human with the gene or with a hand missing");
    // 7 两头不和：内讧（站着吵），只有双头巨人会；轻度崩溃候选
    var brk=DefDatabase<MentalBreakDef>.GetNamed("LP_EttinQuarrelBreak");human.genes.AddGene(Races124.Gene(Ettin.QuarrelGene),true);
    bool canE=brk.Worker.BreakCanOccur(e2),canH=brk.Worker.BreakCanOccur(human);
    bool started=Ettin.StartQuarrel(e2,"test");var ms=e2.MentalStateDef;
    var qg=e2.genes.GetGene(Races124.Gene(Ettin.QuarrelGene)) as Gene_EttinQuarrel;
    e2.MentalState?.RecoverFromState();
    Check(started && ms?.defName==Ettin.Arguing && canE && !canH && brk.intensity==MentalBreakIntensity.Minor && qg!=null && !Ettin.StartQuarrel(human,"test"),"quarrel: the ettin argues in place (minor break), a human with the gene never does");
    // 8 独眼巨人（1.102.0）：人类的两个眼部位合起来就是那只独眼——视力取最差的，任何一个没了就失明，额头的大眼睛也不画；仿生眼照常能装
    var cb=c.RaceProps.body.AllParts;var eye=cb.First(x=>x.def==BodyPartDefOf.Eye);
    float sight=HARLevel(c,PawnCapacityDefOf.Sight);var bionic=DefDatabase<RecipeDef>.GetNamed("InstallBionicEye");bool fits=c.def.AllRecipes.Contains(bionic) && bionic.Worker.GetPartsToApplyOn(c,bionic).Contains(eye);
    var c2=Add(HARGiantSpawn(HARGiantPawn("LP_CyclopsColonist",Faction.OfPlayer)));var eye2=c2.RaceProps.body.AllParts.First(x=>x.def==BodyPartDefOf.Eye);c2.health.AddHediff(HediffDefOf.MissingBodyPart,eye2);
    var hEye=human.RaceProps.body.AllParts.First(x=>x.def==BodyPartDefOf.Eye);human.health.AddHediff(HediffDefOf.MissingBodyPart,hEye);float hOneEye=HARLevel(human,PawnCapacityDefOf.Sight);human.health.RestorePart(hEye);
    var eyeNode=Races124.Gene(Cyclops.Core).renderNodeProperties.OfType<PawnRenderNodeProperties_LPPart>().Single();
    Check(Cyclops.Is(c) && Near(sight,1f) && HARLevel(c2,PawnCapacityDefOf.Sight)<=.001f && Cyclops.EyeLost(c2) && !Cyclops.EyeLost(c) && hOneEye>.4f && fits && c.def==ThingDefOf.Human && c.story.headType?.defName.StartsWith("LP_Cyclops")==true && c2.story.headType?.defName.StartsWith("LP_Cyclops")==true && c.genes.Xenotype?.defName=="LP_CyclopsKin" && Cyclops.SkinGenes.Count(n=>c.genes.GetGene(Races124.Gene(n))!=null)==1
     && eyeNode.part=="cyclopsEye" && ContentFinder<Texture2D>.Get(eyeNode.texPath+"_south",false)!=null,"cyclops: the two eye parts act as one eye (blind when either is gone, a human with one eye still sees "+hOneEye.ToStringPercent()+"); bionic eye surgery fits; cyclops head types, gene eye node (head "+c.story.headType?.defName+", "+c2.story.headType?.defName+")");
    // 9 单眼视界：射击精度 -3.5 点（透镜 -1.75）；人类戴不了透镜
    // 原版在 FinalizeValue 里先套 StatPart 再查精度曲线：不查曲线（applyPostProcess=false）比较点数。
    float Acc(Pawn p){StatDefOf.ShootingAccuracyPawn.Worker.ClearCacheForThing(p);return StatDefOf.ShootingAccuracyPawn.Worker.GetValue(StatRequest.For(p),false);}
    float cAcc=Acc(c),aim=c.GetStatValue(StatDefOf.AimingDelayFactor);c.genes.RemoveGene(c.genes.GetGene(Races124.Gene(Cyclops.FlatSightGene)));float cBase=Acc(c);aim-=c.GetStatValue(StatDefOf.AimingDelayFactor)-1f;c.genes.AddGene(Races124.Gene(Cyclops.FlatSightGene),false);
    var lens=(Apparel)ThingMaker.MakeThing(Items.Thing(Cyclops.Lens));c.apparel.Wear(lens,false);float cLens=Acc(c);
    bool humanLens=EquipmentUtility.CanEquip(lens,human,out string lensWhy,false);
    Check(Near(cAcc-cBase,Cyclops.FlatSightPoints) && Near(cLens-cBase,Cyclops.LensPoints) && !humanLens && (lensWhy??"").Contains("独眼巨人") && Near(aim,1.1f),"flat sight: shooting accuracy "+(cAcc-cBase).ToString("0.##")+" points, "+(cLens-cBase).ToString("0.##")+" with the lens; aiming time x"+aim.ToString("0.##")+"; a human cannot wear the lens ("+lensWhy+")");
    // 10 凝视免疫
    var scared=Add(HARGiantSpawn(Person()));
    bool cScared=LichSpells.Frighten(human,c),hScared=scared.GetStatValue(StatDefOf.PsychicSensitivity)<=0 || LichSpells.Frighten(human,scared);
    Check(!cScared && !c.InMentalState && hScared && Cyclops.GazeImmune(c),"cyclops ignores fear gaze; an ordinary pawn panics");
    // 11 巨人：GiantKin、阶序、双盔与锻工外衣；普通帽子盖不到第二个头
    var helm=(Apparel)ThingMaker.MakeThing(Items.Thing(Ettin.Helm));var tunic=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FireGiantTunic"),ThingDefOf.Cloth);made.Add(helm);made.Add(tunic);
    bool eHelm=EquipmentUtility.CanEquip(helm,e2,out _,false),cHelm=EquipmentUtility.CanEquip(helm,c,out _,false),hHelm=EquipmentUtility.CanEquip(helm,human,out _,false),eTunic=EquipmentUtility.CanEquip(tunic,e2,out _,false),cTunic=EquipmentUtility.CanEquip(tunic,c,out _,false);
    var secondGroup=DefDatabase<BodyPartGroupDef>.GetNamed("LP_EttinSecondHead");var hat=DefDatabase<ThingDef>.GetNamed("Apparel_CowboyHat");
    GiantKin.ForgetCensus();bool eBelow=DefDatabase<ThoughtDef>.GetNamed("LP_OrdningBelow").Worker.CurrentState(e2).Active,cAbove=DefDatabase<ThoughtDef>.GetNamed("LP_OrdningAbove").Worker.CurrentState(c).Active;
    Check(GiantKin.Rank(e2)==5 && GiantKin.Rank(c)==6 && eHelm && cHelm && !hHelm && eTunic && cTunic && helm.def.apparel.bodyPartGroups.Contains(secondGroup) && !hat.apparel.bodyPartGroups.Contains(secondGroup) && eBelow && cAbove,"both races are giants (rank 5 / 6, ordning: ettin commands, cyclops keeps its place); twin helm and forge tunic fit them, not a human; a cowboy hat cannot cover the second head, the twin helm does");
    // 12 体型只走本源基因上的 GiantRaceExtension 一个来源：2.0 / 1.8，巨躯、渲染、胃容量不叠乘（巨人血统 adultSize 1）
    Check(Near(e2.BodySize,human.BodySize*2f) && Near(c.BodySize,human.BodySize*1.8f) && Near(GiantKin.Size(e2),1f) && Near(BodySize.HealthFactor(e2),1.5f) && Near(BodySize.MeleeFactor(e2),1.3f) && Near(BodySize.HealthFactor(c),1.4f) && Near(RootScale(e2),RootScale(human)*1.4f) && Near(RootScale(c),RootScale(human)*1.35f) && Near(Satiety.Compute(e2),1.8f) && Near(Satiety.Compute(c),1.7f),
     "body size from the core gene only: ettin "+e2.BodySize+" / cyclops "+c.BodySize+", health x"+BodySize.HealthFactor(e2).ToString("0.##")+" / x"+BodySize.HealthFactor(c).ToString("0.##")+", render x1.4 / x1.35, stomach 180% / 170% (no stacking with the gene)");
    // 13 剧本描述能显示
    var scen=DefDatabase<ScenarioDef>.GetNamed("LP_HillOutcasts").scenario;string text=null;try{text=scen.GetFullInformationText();}catch(Exception ex){Log.Message("[DND边缘大陆] LP_HillOutcasts: "+ex);}
    Check(text!=null && !text.Contains("Cannot read data") && scen.AllParts.OfType<ScenPart_StartingAnimal>().Any(),"scenario 'hill outcasts' shows its full description");
    // 14 Isekai（只在启用时）：两族都挂上 Isekai 组件（特性、里程碑照常）
    var isekai=GenTypes.GetTypeInAnyAssembly("IsekaiLeveling.IsekaiComponent");
    if(isekai!=null){var get=AccessTools.Method(isekai,"GetCached");Check(get!=null && get.Invoke(null,new object[]{e2})!=null && get.Invoke(null,new object[]{c})!=null,"Isekai attaches its component to an ettin and a cyclops colonist");}
   }finally{
    foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    GiantKin.ForgetCensus();
   }
  }
 }
}
