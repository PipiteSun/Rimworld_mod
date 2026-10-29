using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 public partial class LichChecks {
  static bool healthReload;
  static readonly Dictionary<string,float[]> healthSnapshots=new Dictionary<string,float[]>();
  static string healthOwnerId;
  static Hediff Growth(Pawn p,BodyPartRecord part,HediffDef def,float steps){var h=p.health.AddHediff(def,part);h.Severity=steps;return h;}
  static float[] HealthSnapshot(Pawn p){return new[]{p.HealthScale}.Concat(p.RaceProps.body.AllParts.Select(x=>PartHealth.Maximum(x,p))).ToArray();}
  static bool SameHealth(float[] a,float[] b)=>a.Length==b.Length && a.Zip(b,(x,y)=>Math.Abs(x-y)<.001f).All(x=>x);
  void HealthCompatibilityUpdate(){
   if(ran || Current.ProgramState!=ProgramState.Playing || Find.CurrentMap==null || LongEventHandler.AnyEventNowOrWaiting)return;
   ran=true;Find.TickManager.Pause();map=Find.CurrentMap;string output=Path.Combine(GenFilePaths.SaveDataFolderPath,"LichResult.txt");
   try{
    if(!healthReload){RunHealthCompatibility();healthReload=true;GameDataSaveLoader.SaveGame("Lich-Health-Roundtrip");LongEventHandler.QueueLongEvent(()=>GameDataSaveLoader.LoadGame("Lich-Health-Roundtrip"),"LoadingLongEvent",false,null);return;}
    foreach(var entry in healthSnapshots){
     var p=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==entry.Key);
     Check(SameHealth(HealthSnapshot(p),entry.Value),"health compatibility: all part maxima and external health scale survive save/load for "+p.LabelShort);
     foreach(var part in p.RaceProps.body.AllParts.Where(x=>!p.health.hediffSet.PartIsMissing(x))){float wounds=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h=>h.Part==part).Sum(h=>h.Severity);Check(Near(p.health.hediffSet.GetPartHealth(part),Mathf.RoundToInt(Mathf.Max(0,PartHealth.Maximum(part,p)-wounds))),"loaded native current health agrees for "+p.LabelShort+" / "+part.Label);}
     p.Kill(null);Check(ResurrectionUtility.TryResurrect(p,new ResurrectionParams{restoreMissingParts=true,removeDiedThoughts=true}),"health fixture resurrects: "+p.LabelShort);
     Check(SameHealth(HealthSnapshot(p),entry.Value),"health compatibility: resurrection preserves all maxima exactly once for "+p.LabelShort);
    }
    File.WriteAllText(output,"PASS: "+checks+" health compatibility checks; native HP, combined tempering cap, reinforcement, materials, Titan, injury ratios, full save/load and resurrection.");
   }catch(Exception e){File.WriteAllText(output,"FAIL after "+checks+" health checks\n"+e);Log.Error("[LICH-TEST] "+e);}
   Application.Quit();
  }
  void RunHealthCompatibility(){
   foreach(var patch in Harmony.GetPatchInfo(AccessTools.PropertyGetter(typeof(Pawn),"HealthScale")).Postfixes)Log.Message("[LICH-HEALTH] HealthScale provider: "+patch.owner+" / "+patch.PatchMethod);
   var center=map.mapPawns.FreeColonistsSpawned.First().Position;
   worker=Person();GenSpawn.Spawn(worker,CellFinder.RandomClosewalkCellNear(center,map,5),map);var box=Rituals.Ascend(worker);box.level=20;GenPlace.TryPlaceThing(box,center,map,ThingPlaceMode.Near);worker.psychicEntropy.OffsetPsyfocusDirectly(1);healthOwnerId=worker.ThingID;
   var defs=new[]{"HA_Tempering","MB_Tempering"}.Select(n=>DefDatabase<HediffDef>.GetNamedSilentFail(n)).Where(x=>x!=null).ToArray();
   var lich=Necro.Lich(worker);var arm=worker.RaceProps.body.AllParts.First(x=>x.def.defName=="Arm");var opposite=worker.RaceProps.body.AllParts.First(x=>x.def==arm.def && x!=arm);var brain=worker.health.hediffSet.GetBrain();
   float untrained=arm.def.GetMaxHealth(worker);
   foreach(var def in defs){var h=Growth(worker,arm,def,25);Check(Near(worker.health.hediffSet.GetPartHealth(arm),Mathf.RoundToInt(untrained*1.25f)),def.defName+" standalone retains native part HP");worker.health.RemoveHediff(h);}
   var growth=defs.Select(d=>Growth(worker,arm,d,80)).ToList();float bonus=defs.Length==0?0:Math.Min(100,defs.Length*80)*.01f;float trained=bonus>0?Mathf.RoundToInt(untrained*(1+bonus)):untrained;
   Check(Near(worker.health.hediffSet.GetPartHealth(arm),trained),"two tempering sources share their 100 percent cap");
   Check(lich.Strengthen(arm) && lich.Strengthen(arm),"combined health fixture strengthens arm twice");
   float maximum=Mathf.Ceil(trained*1.4f);Check(Near(worker.health.hediffSet.GetPartHealth(arm),maximum),"native arm HP composes tempering then reinforcement once");
   Check(Near(worker.health.hediffSet.GetPartHealth(opposite),opposite.def.GetMaxHealth(worker)),"combined growth does not leak to opposite arm");
   foreach(var provider in new[]{TemperingCompatibility.HumanMaximum,TemperingCompatibility.MythicMaximum}.Where(x=>x!=null))Check(Near((float)provider.Invoke(null,new object[]{arm,worker}),maximum),"provider UI maximum agrees with native reinforced HP: "+provider.DeclaringType);
   var injury=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,worker,arm);injury.Severity=trained+1;worker.health.AddHediff(injury);
   var limb=worker.health.hediffSet.hediffs.OfType<Hediff_Limb>().Single(x=>x.Part==arm);
   Check(!worker.health.hediffSet.PartIsMissing(arm) && !lich.Release(limb),"combined maximum survives deep wounds and blocks unsafe release");
   injury.Severity=trained*.7f;Check(lich.Release(limb) && !worker.health.hediffSet.PartIsMissing(arm) && Near(injury.Severity,trained*.7f),"release retains tempering HP and does not heal existing wounds");worker.health.RemoveHediff(injury);
   worker.psychicEntropy.OffsetPsyfocusDirectly(1);Check(lich.Strengthen(arm) && lich.Strengthen(arm),"restore arm reinforcement for load roundtrip");
   if(defs.Length>0)Growth(worker,brain,defs[0],1);worker.psychicEntropy.OffsetPsyfocusDirectly(1);Check(lich.Strengthen(brain) && lich.Strengthen(brain),"reinforce rounded brain maximum");
   float brainBase=brain.def.GetMaxHealth(worker);float expectedBrain=Mathf.Ceil((defs.Length>0?Mathf.RoundToInt(brainBase*1.01f):brainBase)*1.4f);
   Check(Near(worker.health.hediffSet.GetPartHealth(brain),expectedBrain),"brain rounding matches native healthy HP without phantom wounds");
   var titan=DefDatabase<GeneDef>.GetNamedSilentFail("HA_TitanGene");if(titan!=null){
    var gene=worker.genes.AddGene(titan,true);var memory=AccessTools.Field(gene.GetType(),"memory").GetValue(gene);var field=AccessTools.Field(memory.GetType(),"titanNutritionGrowth");field.SetValue(memory,0f);float baseline=worker.HealthScale;field.SetValue(memory,1f);Necro.Refresh(worker);
    Check(gene.Active && Near(worker.HealthScale,baseline*1.5f),"Titan growth composes with external health scale once");
    Check(Near(worker.health.hediffSet.GetPartHealth(arm),Mathf.Ceil((bonus>0?Mathf.RoundToInt(arm.def.GetMaxHealth(worker)*(1+bonus)):arm.def.GetMaxHealth(worker))*1.4f)),"Titan, shared tempering and lich layers compose in native arm health");
   }
   healthSnapshots[worker.ThingID]=HealthSnapshot(worker);
   // 1.23.0 巨躯规则与外部生命来源相乘：火巨人 HealthScale×1.5 叠锻体，巨魔×1.25 叠泰坦成长。
   var giant=RacePawn("LP_FireGiant");var giantArm=giant.RaceProps.body.AllParts.First(x=>x.def.defName=="Arm");
   Check(Near(BodySize.HealthFactor(giant),1.5f) && Near(giant.HealthScale,BodySize.Measure(BodySize.Rule.Legacy,()=>giant.HealthScale)*1.5f),"giant-body health composes with external health providers exactly once: "+giant.HealthScale);
   if(defs.Length>0){Growth(giant,giantArm,defs[0],50);Check(Near(giant.health.hediffSet.GetPartHealth(giantArm),Mathf.RoundToInt(giantArm.def.GetMaxHealth(giant)*1.5f)) && giantArm.def.GetMaxHealth(giant)>=Mathf.CeilToInt(giantArm.def.hitPoints*1.5f),"tempering composes on top of giant-body part health: "+giant.health.hediffSet.GetPartHealth(giantArm));}
   healthSnapshots[giant.ThingID]=HealthSnapshot(giant);
   if(titan!=null){
    var troll=RacePawn("LP_Troll");
    var gene=troll.genes.AddGene(titan,true);var memory=AccessTools.Field(gene.GetType(),"memory").GetValue(gene);var field=AccessTools.Field(memory.GetType(),"titanNutritionGrowth");field.SetValue(memory,0f);float baseline=troll.HealthScale;field.SetValue(memory,1f);Necro.Refresh(troll);
    Check(gene.Active && Near(BodySize.HealthFactor(troll),1.25f) && Near(troll.HealthScale,baseline*1.5f) && Near(baseline,BodySize.Measure(BodySize.Rule.Legacy,()=>{field.SetValue(memory,0f);float v=troll.HealthScale;field.SetValue(memory,1f);return v;})*1.25f),"Titan growth and giant-body troll health multiply: "+troll.HealthScale);
    healthSnapshots[troll.ThingID]=HealthSnapshot(troll);
   }
   foreach(string kind in new[]{"Skeleton","Golem","Scholar"}){
    var p=Constructs.Create(kind,worker,CellFinder.RandomClosewalkCellNear(center,map,5),map,"大脑",new ConstructDesign{meat=200});var h=Necro.Construct(p);
    foreach(var wound in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(wound);
    var plan=h.design;h.design=null;float baseline=p.HealthScale;h.design=plan;Check(Near(p.HealthScale,baseline*1.5f),kind+" material health preserves external multipliers exactly once");
    var part=p.RaceProps.body.AllParts.First(x=>x.def.defName=="Arm");if(defs.Length>0)Growth(p,part,defs[0],37);
    float oldMax=PartHealth.Maximum(part,p);var wound2=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,p,part);wound2.Severity=oldMax*.4f;p.health.AddHediff(wound2);
    var upgrade=Remolding.Current(h);upgrade.meat=600;Remolding.Apply(h,upgrade);Check(Math.Abs(wound2.Severity/PartHealth.Maximum(part,p)-.4f)<.0001f,kind+" remolding upgrade preserves wound fraction including rounded tempering");
    upgrade.meat=0;Remolding.Apply(h,upgrade);Check(!p.Dead && !p.health.hediffSet.PartIsMissing(part) && Math.Abs(wound2.Severity/PartHealth.Maximum(part,p)-.4f)<.0001f,kind+" remolding downgrade preserves wound fraction without amputation");
    p.health.RemoveHediff(wound2);upgrade.meat=200;Remolding.Apply(h,upgrade);
    if(defs.Length>0){var growthH=p.health.hediffSet.hediffs.First(x=>x.Part==part && x.def==defs[0]);p.health.AddHediff(DefDatabase<HediffDef>.GetNamed("BionicArm"),part);Check(Near(PartHealth.Maximum(part,p),part.def.GetMaxHealth(p)),kind+" bionic replacement cannot retain natural-part tempering bonus");}
    healthSnapshots[p.ThingID]=HealthSnapshot(p);
   }
  }
 }
}
