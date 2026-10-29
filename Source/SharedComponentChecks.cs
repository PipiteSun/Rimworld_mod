using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  static string sharedCooldownId,sharedRelationId;static int sharedCooldownLeft,sharedCooldownTick,sharedRelationTicks;
  // 运行时测试基因：defName 不用 LP_ 前缀，避免被"全部 LP_ 定义"类检查扫到；注册进 DefDatabase 以便同进程存读档能解析。
  static GeneDef TestGene(string name,DefModExtension ext=null,StatDef stat=null,float offset=0){
   var def=DefDatabase<GeneDef>.GetNamedSilentFail(name);if(def!=null)return def;
   def=new GeneDef{defName=name,label=name,geneClass=typeof(Gene),selectionWeight=0,canGenerateInGeneSet=false};
   if(ext!=null)def.modExtensions=new List<DefModExtension>{ext};
   if(stat!=null)def.statOffsets=new List<StatModifier>{new StatModifier{stat=stat,value=offset}};
   DefDatabase<GeneDef>.Add(def);return def;
  }
  static DamageInfo Lethal(BodyPartRecord part){var d=new DamageInfo(DamageDefOf.Cut,100,999,-1,null,part);d.SetIgnoreInstantKillProtection(true);return d;}
  static T FromXml<T>(string xml){var doc=new XmlDocument();doc.LoadXml(xml);var result=DirectXmlToObject.ObjectFromXml<T>(doc.DocumentElement,true);DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences(FailMode.LogErrors);return result;}
  Pawn SharedPawn(){var p=Person();p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);return p;}
  static float RootScale(Pawn p){p.Drawer.renderer.EnsureGraphicsInitialized();var root=p.Drawer.renderer.renderTree.rootNode;return root.Worker.ScaleFor(root,DrawParms(p,Rot4.South)).x;}
  void CheckSharedComponents(){CheckSmallBody();CheckMindResist();CheckOncePerCooldown();CheckRacialRelation();}

  void CheckSmallBody(){
   var ext=FromXml<SmallBodyExtension>("<li><bodySize>0.8</bodySize><carryFactor>0.9</carryFactor></li>");
   Check(Near(ext.bodySize,.8f) && Near(ext.RenderScale,.8f) && Near(ext.carryFactor,.9f) && !ext.ConfigErrors().Any(),"small body extension parses from XML; render scale follows body size by default");
   Check(new SmallBodyExtension{bodySize=0}.ConfigErrors().Any(),"small body rejects zero body size at load validation");
   Check(StatDefOf.CarryingCapacity.parts.Count(x=>x is StatPart_SmallBodyCarry)==1,"small body carry stat part is registered exactly once");
   var dwarf=TestGene("LPTest_SmallBodyDwarf",ext);var tiny=TestGene("LPTest_SmallBodyHalf",new SmallBodyExtension{bodySize=.5f,renderScale=.6f});
   Check(SmallBody.Genes.Contains(dwarf) && SmallBody.Genes.Contains(tiny),"small body gene cache picks up newly registered genes");
   var p=SharedPawn();float size=p.BodySize,carry=p.GetStatValue(StatDefOf.CarryingCapacity),scale=RootScale(p),cargo=MassUtility.Capacity(p);
   p.genes.AddGene(dwarf,true);
   Check(Near(p.BodySize,size*.8f),"small body gene scales real body size to 80%: "+p.BodySize);
   Check(Math.Abs(p.GetStatValue(StatDefOf.CarryingCapacity)-carry*.8f*.9f)<.6f,"carrying capacity follows body size and the extra carry factor: "+p.GetStatValue(StatDefOf.CarryingCapacity)+" from "+carry);
   Check(Near(RootScale(p),scale*.8f),"whole pawn render scale follows small body");
   Check(Math.Abs(MassUtility.Capacity(p)-cargo*.8f)<.6f,"caravan mass capacity follows native body size: "+MassUtility.Capacity(p)+" from "+cargo);
   p.genes.AddGene(tiny,true);
   Check(Near(p.BodySize,size*.4f) && Near(RootScale(p),scale*.48f),"two small body genes multiply in size and render scale");
   p.genes.RemoveGene(p.genes.GetGene(tiny));p.genes.RemoveGene(p.genes.GetGene(dwarf));
   Check(Near(p.BodySize,size) && Near(p.GetStatValue(StatDefOf.CarryingCapacity),carry) && Near(RootScale(p),scale),"removing small body genes restores size, carry and render scale");
   var giant=SharedPawn();giant.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_FireGiantCore"),true);float giantSize=giant.BodySize,giantScale=RootScale(giant);
   Check(Near(FireGiant.Size(giant),2f) && Near(giantSize,size*2f),"fire giant size logic unchanged by small body patch");
   giant.genes.AddGene(dwarf,true);
   Check(Near(giant.BodySize,giantSize*.8f) && Near(RootScale(giant),giantScale*.8f) && Near(FireGiant.Size(giant),2f),"small body multiplies with fire giant size instead of replacing it");
   var troll=SharedPawn();troll.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_TrollAppearance"),true);
   Check(Near(troll.BodySize,size*1.5f),"troll size logic unchanged by small body patch");
   troll.genes.AddGene(dwarf,true);Check(Near(troll.BodySize,size*1.2f),"small body multiplies with troll size");
   p.Destroy();giant.Destroy();troll.Destroy();
  }

  void CheckMindResist(){
   var stat=DNDMind.Stat;
   Check(stat!=null && stat.defaultBaseValue==0 && stat.maxValue==1 && stat.minValue==0,"mind resistance stat is defined in 0..1 with default 0");
   var p=SharedPawn();
   Check(DNDMind.Resistance(p)==0 && Enumerable.Range(0,200).All(i=>!DNDMind.TryResist(p)) && Near(DNDMind.Attenuate(p,10),10),"ordinary pawn has no mind resistance and never resists");
   var ward=TestGene("LPTest_MindWard",null,stat,.6f);var ward2=TestGene("LPTest_MindWardStrong",null,stat,.6f);
   // 心灵类负面心情：取原版心灵嗡鸣的负面阶段，比较有无抗性的实际心情值。
   var droneDef=DefDatabase<ThoughtDef>.GetNamed("PsychicDrone");int stage=droneDef.stages.FindIndex(s=>s!=null && s.baseMoodEffect<0);
   var drone=(Thought_Situational)Activator.CreateInstance(droneDef.ThoughtClass);drone.def=droneDef;drone.pawn=p;Traverse.Create(drone).Field("curStageIndex").SetValue(stage);
   for(int attempt=0;attempt<20 && drone.MoodOffset()>=0;attempt++){p.Destroy();p=SharedPawn();drone.pawn=p;}
   float mood=drone.MoodOffset();Check(mood<0,"psychic drone fixture has a negative mood: "+mood);
   p.genes.AddGene(ward,true);
   Check(Near(DNDMind.Resistance(p),.6f) && Near(DNDMind.Attenuate(p,10),4),"gene statOffsets grant 60% mind resistance: "+DNDMind.Resistance(p));
   int resisted=0;Rand.PushState(4242);try{for(int i=0;i<2000;i++)if(DNDMind.TryResist(p))resisted++;}finally{Rand.PopState();}
   Check(resisted>1080 && resisted<1320,"TryResist succeeds at the resistance rate: "+resisted+"/2000");
   resisted=0;Rand.PushState(4243);try{for(int i=0;i<2000;i++)if(DNDMind.TryResist(p,.5f))resisted++;}finally{Rand.PopState();}
   Check(resisted>480 && resisted<720,"TryResist chance factor scales the roll: "+resisted+"/2000");
   Check(Math.Abs(drone.MoodOffset()-mood*.4f)<.05f,"psychic drone mood is reduced by mind resistance: "+drone.MoodOffset()+" from "+mood);
   p.genes.AddGene(ward2,true);
   Check(Near(DNDMind.Resistance(p),1f) && Enumerable.Range(0,200).All(i=>DNDMind.TryResist(p)) && Math.Abs(drone.MoodOffset())<.01f,"stacked resistance clamps to 100%");
   var soothe=DefDatabase<ThoughtDef>.GetNamed("PsychicSoothe",false);
   if(soothe!=null){int good=soothe.stages.FindIndex(s=>s!=null && s.baseMoodEffect>0);if(good>=0){var t=(Thought_Situational)Activator.CreateInstance(soothe.ThoughtClass);t.def=soothe;t.pawn=p;Traverse.Create(t).Field("curStageIndex").SetValue(good);Check(t.MoodOffset()>0,"positive psychic mood is not reduced by mind resistance");}}
   p.Destroy();
  }

  void CheckOncePerCooldown(){
   var ext=FromXml<OncePerCooldownExtension>("<li><workerClass>LichPhylactery.OncePerCooldownWorker_ReduceDamage</workerClass><cooldownHediff>LP_CheatDeathCooldown</cooldownHediff><cooldownDays>2</cooldownDays><damageFactor>0.1</damageFactor><moteText>真走运！</moteText></li>");
   Check(ext.cooldownHediff?.defName=="LP_CheatDeathCooldown" && ext.Worker is OncePerCooldownWorker_ReduceDamage && ext.CooldownTicks==120000 && !ext.ConfigErrors().Any(),"once-per-cooldown extension parses worker, cooldown hediff and duration from XML");
   Check(new OncePerCooldownExtension{onDeath=false,onDowned=false}.ConfigErrors().Count()>=3,"once-per-cooldown rejects missing worker, hediff and triggers at load validation");
   var gene=TestGene("LPTest_Lucky",ext);
   var p=SharedPawn();p.genes.AddGene(gene,true);var brain=p.health.hediffSet.GetBrain();
   p.TakeDamage(new DamageInfo(DamageDefOf.Cut,1,999,-1,null,p.RaceProps.body.AllParts.First(b=>b.def.defName=="Torso")));
   Check(p.health.hediffSet.GetFirstHediffOfDef(ext.cooldownHediff)==null,"harmless injury does not spend the cooldown");
   float before=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity);p.TakeDamage(Lethal(brain));
   var cd=OncePerCooldown.Cooldown(p,ext);float brainDamage=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h=>h.Part==brain).Sum(h=>h.Severity),total=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity);
   Check(!p.Dead && cd!=null && cd.sourceGene==gene && cd.triggeredTick==Find.TickManager.TicksGame,"lethal brain hit triggers the lucky gene and starts a visible cooldown");
   Check(brainDamage>0 && total-before<=100f*.1f*1.5f && brainDamage<5f,"reduce-damage example keeps only 10% of the lethal hit: brain "+brainDamage+", new wounds "+(total-before));
   Check(cd.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear==120000 && cd.Visible && cd.LabelInBrackets.Contains(gene.label),"cooldown lasts the configured two days and names its source");
   Check(!OncePerCooldown.Ready(p,gene),"gene is not ready while its cooldown hediff exists");
   Step(1);p.TakeDamage(Lethal(brain));
   Check(p.Dead,"second lethal hit during cooldown is not prevented");
   // 1.22.0：Isekai 体质会放大部位生命值，100点不一定打穿大脑；对照组用足以毁掉任何大脑的伤害。
   var q=SharedPawn();var overkill=Lethal(q.health.hediffSet.GetBrain());overkill.SetAmount(99999);q.TakeDamage(overkill);
   Check(q.Dead,"pawn without the gene is unaffected by the cooldown patch");
   var r=SharedPawn();r.genes.AddGene(gene,true);r.TakeDamage(Lethal(r.health.hediffSet.GetBrain()));
   var rc=OncePerCooldown.Cooldown(r,ext);rc.TryGetComp<HediffComp_Disappears>().ticksToDisappear=1;Step(2);
   Check(!r.Dead && OncePerCooldown.Ready(r,gene),"cooldown expires through the native disappear timer and the gene is ready again");
   // 1.20.0：先清掉第一次留下的伤（完整模组列表里伤害倍率更高，两次各留10%会叠到大脑上限），只看冷却后能否再次触发。
   foreach(var h in r.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())r.health.RemoveHediff(h);
   r.TakeDamage(Lethal(r.health.hediffSet.GetBrain()));
   Check(!r.Dead && OncePerCooldown.Cooldown(r,ext)!=null,"gene triggers again after the cooldown");
   p.Corpse?.Destroy();q.Corpse?.Destroy();r.Destroy();
  }

  void CheckRacialRelation(){
   var relation=DefDatabase<RacialRelationDef>.GetNamed("LP_Relation_TrollFireGiant");var thought=relation.thought;var tracker=RacialRelations.Tracker;
   Check(!relation.ConfigErrors().Any() && !relation.enabled && relation.observer.defName=="LP_Troll" && relation.target.defName=="LP_FireGiant","example racial relation validates and ships disabled");
   Check(tracker!=null,"racial relation tracker is a game component");
   var troll=SharedPawn();troll.genes.SetXenotypeDirect(relation.observer);var giant=SharedPawn();giant.genes.SetXenotypeDirect(relation.target);
   tracker.Census(map).Rebuild(map);
   Check(!thought.Worker.CurrentState(troll).Active,"disabled relation never produces a mood");
   relation.enabled=true;
   try{
    var s=thought.Worker.CurrentState(troll);
    Check(s.Active && s.StageIndex==0,"troll seeing a fire giant on the map gets the initial relation mood");
    Check(!thought.Worker.CurrentState(giant).Active,"relation is one-way: the target does not get the observer's mood");
    Check(Near(thought.Worker.MoodMultiplier(troll),1f),"no bridge xenotype means full mood");
    var census=tracker.Census(map);int stamp=census.tick;for(int i=0;i<5000;i++)thought.Worker.CurrentState(troll);
    Check(tracker.Census(map).tick==stamp,"mood checks reuse the cached map census instead of scanning pawns");
    tracker.Accumulate(relation.AcceptTicks-RacialRelationTracker.Interval);
    Check(thought.Worker.CurrentState(troll).StageIndex==0,"mood stays initial before the configured days");
    tracker.Accumulate(RacialRelationTracker.Interval);
    Check(thought.Worker.CurrentState(troll).StageIndex==1 && tracker.Ticks(troll,relation)>=relation.AcceptTicks,"after enough days together the mood turns into the accepted stage");
    var bridge=SharedPawn();bridge.genes.SetXenotypeDirect(DefDatabase<XenotypeDef>.GetNamed("LP_ForceDragon"));relation.halvedBy.Add(bridge.genes.Xenotype);
    // 1.63.0：1.57.0 起两族之桥只减半负面心情；这条检查原来在已转为正面阶段之后才测，检查过时。
    // 现在：正面阶段（老战友）倍率不变，把同处时间清零回到负面初始阶段后倍率 ×0.5。
    int together=tracker.Ticks(troll,relation);
    try{
     tracker.Census(map).Rebuild(map);bool posKept=Near(thought.Worker.MoodMultiplier(troll),1f);
     tracker.SetTicks(troll,relation,0);bool negHalved=thought.Worker.CurrentState(troll).StageIndex==0 && Near(thought.Worker.MoodMultiplier(troll),.5f);
     Check(posKept && negHalved,"a bridge xenotype colonist on the map halves the negative relation mood (negative "+negHalved+") but keeps the accepted positive one ("+posKept+")");
    }
    finally{relation.halvedBy.Clear();bridge.Destroy();tracker.SetTicks(troll,relation,together);}
    giant.DeSpawn();tracker.Census(map).Rebuild(map);
    Check(!thought.Worker.CurrentState(troll).Active,"mood ends when the target leaves the map");
   }finally{relation.enabled=false;}
   troll.Destroy();giant.Destroy();
  }

  // 存读档夹具：冷却 hediff 剩余时间与触发刻、种族关系同处天数。
  // 1.63.0：-Only SharedComponents 按组名找 PrepareSharedComponentsSave；原来只有 PrepareSharedSave，读档后的检查找不到夹具。
  void PrepareSharedComponentsSave()=>PrepareSharedSave();
  void PrepareSharedSave(){
   var gene=DefDatabase<GeneDef>.GetNamed("LPTest_Lucky");var p=SharedPawn();p.genes.AddGene(gene,true);var cd=OncePerCooldown.StartCooldown(p,gene);
   // 运行时测试基因不能写进存档：改用原版基因作为来源，并从所有角色身上移除测试基因。
   cd.sourceGene=DefDatabase<GeneDef>.GetNamed("Robust");foreach(var x in PawnsFinder.All_AliveOrDead.Where(x=>x.genes!=null).ToList())foreach(var g in x.genes.GenesListForReading.Where(g=>g.def.defName.StartsWith("LPTest_")).ToList())x.genes.RemoveGene(g);
   cd.TryGetComp<HediffComp_Disappears>().ticksToDisappear=54321;sharedCooldownId=p.ThingID;sharedCooldownLeft=54321;sharedCooldownTick=cd.triggeredTick;
   var relation=DefDatabase<RacialRelationDef>.GetNamed("LP_Relation_TrollFireGiant");var troll=SharedPawn();troll.genes.SetXenotypeDirect(relation.observer);
   RacialRelations.Tracker.SetTicks(troll,relation,123456);sharedRelationId=troll.ThingID;sharedRelationTicks=123456;
  }
  void CheckSharedComponentsAfterLoad(){
   map=Find.CurrentMap;var gene=DefDatabase<GeneDef>.GetNamed("LPTest_Lucky");var ext=gene.GetModExtension<OncePerCooldownExtension>();
   var p=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==sharedCooldownId);var cd=OncePerCooldown.Cooldown(p,ext);
   Check(cd!=null && cd.sourceGene?.defName=="Robust" && cd.triggeredTick==sharedCooldownTick && Math.Abs(cd.TryGetComp<HediffComp_Disappears>().ticksToDisappear-sharedCooldownLeft)<=5,"cooldown hediff, its source and remaining time survive save/load");
   var relation=DefDatabase<RacialRelationDef>.GetNamed("LP_Relation_TrollFireGiant");var troll=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==sharedRelationId);
   Check(RacialRelations.Tracker.Ticks(troll,relation)==sharedRelationTicks,"racial relation days together survive save/load");
   p.Destroy();troll.Destroy();
  }
 }
}
