using System;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 public partial class LichChecks {
  void CheckTroll(){
   var xenotype=DefDatabase<XenotypeDef>.GetNamed("LP_Troll");
   var geneDef=DefDatabase<GeneDef>.GetNamed("LP_TrollRegeneration");
   var appearanceDef=DefDatabase<GeneDef>.GetNamed("LP_TrollAppearance");
   var metabolism=DefDatabase<HediffDef>.GetNamed("LP_TrollMetabolism");
   Check(!xenotype.ConfigErrors().Any() && xenotype.inheritable && xenotype.genes.Contains(geneDef) && xenotype.genes.Contains(appearanceDef),"troll is a valid inheritable Biotech xenotype with appearance and regeneration genes");
   var troll=Unscathed(PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:xenotype,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25,fixedChronologicalAge:25)));
   var gene=troll.genes.GetGene(geneDef) as Gene_TrollRegeneration;
   Check(troll.genes.Xenotype==xenotype && gene?.Active==true && TrollAppearance.Active(troll) && Near(troll.BodySize,1.5f),"adult troll generates with both active genes and 1.5x physical size");
   var phenotype=new[]{"Skin_Green","Body_Hulk","Ears_Pointed","Jaw_Heavy","Brow_Heavy","Hair_BaldOnly","Beard_NoBeardOnly"};
   foreach(var name in phenotype){var def=DefDatabase<GeneDef>.GetNamed(name);Check(xenotype.genes.Contains(def) && troll.genes.HasActiveGene(def),"troll uses vanilla appearance gene: "+name);}
   var diet=TrollDiet.RawFoodGene;
   Check(diet!=null && xenotype.genes.Contains(diet) && troll.genes.HasActiveGene(diet),"troll carries vanilla robust digestion gene: "+(diet?.defName??"missing"));
   Check(troll.story.bodyType==BodyTypeDefOf.Hulk && troll.story.hairDef==HairDefOf.Bald && troll.style.beardDef==BeardDefOf.NoBeard,"vanilla genes select Hulk body, no hair and no beard");
   foreach(var path in new[]{"Troll/TrollAppearance","Troll/TrollRegeneration","Troll/Tusks/TrollTusks_south","Troll/Tusks/TrollTusks_east"})Check(ContentFinder<Texture2D>.Get(path,false)!=null,"troll art loads: "+path);
   var dusterDef=DefDatabase<ThingDef>.GetNamed("Apparel_Duster");
   var helmetDef=DefDatabase<ThingDef>.GetNamed("Apparel_SimpleHelmet");
   Check(ApparelUtility.HasPartsToWear(troll,dusterDef) && ApparelUtility.HasPartsToWear(troll,helmetDef),"large troll keeps native duster and helmet fit");
   var duster=(Apparel)ThingMaker.MakeThing(dusterDef,ThingDefOf.Cloth);
   var helmet=(Apparel)ThingMaker.MakeThing(helmetDef,ThingDefOf.Steel);
   troll.apparel.Wear(duster);troll.apparel.Wear(helmet);
   var sword=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword"),ThingDefOf.Steel);
   Check(EquipmentUtility.CanEquip(sword,troll),"large troll can equip a normal melee weapon");
   troll.equipment.AddEquipment(sword);
   Check(troll.apparel.WornApparel.Contains(duster) && troll.apparel.WornApparel.Contains(helmet) && troll.equipment.Primary==sword,"normal armor and weapon remain worn on large troll");
   var metabolismHediff=troll.health.hediffSet.GetFirstHediffOfDef(metabolism);
   Check(Near(metabolismHediff.CurStage.hungerRateFactor,1.2f),"resting troll eats 120%");
   Check(metabolismHediff.CurStage.statFactors.Any(m=>m.stat==StatDefOf.ImmunityGainSpeed && Near(m.value,1.5f)),"troll metabolism raises immunity gain speed");
   troll.needs.food.CurLevel=troll.needs.food.MaxLevel;
   var injury=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,troll,troll.RaceProps.body.corePart);
   injury.Severity=10;
   troll.health.AddHediff(injury);
   gene.Regenerate(250);
   Check(Near(injury.Severity,9.5f),"fed troll heals half a wound point per 250 ticks");
   Check(gene.Regenerating && Near(metabolismHediff.CurStage.hungerRateFactor,2f),"regenerating troll eats 200%");
   Check(troll.needs.food.CurLevel<troll.needs.food.MaxLevel,"healing costs extra food");
   float severity=injury.Severity;
   troll.needs.food.CurLevel=0;
   gene.Regenerate(250);
   Check(Near(injury.Severity,severity),"starvation stops regeneration");
   troll.needs.food.CurLevel=troll.needs.food.MaxLevel;
   troll.apparel.DestroyAll();
   troll.TakeDamage(new DamageInfo(DamageDefOf.Flame,5));
   Check(gene.suppressedUntilTick>Find.TickManager.TicksGame,"actual fire damage suppresses troll regeneration; until="+gene.suppressedUntilTick+" now="+Find.TickManager.TicksGame);
   severity=injury.Severity;
   gene.Regenerate(250);
   Check(Near(injury.Severity,severity),"fire suppression stops healing");
   gene.suppressedUntilTick=0;
   CheckTrollRegeneration(troll,gene);
   var arm=troll.RaceProps.body.AllParts.First(p=>p.def.defName=="Arm");
   troll.health.AddHediff(HediffDefOf.MissingBodyPart,arm);
   gene.regrowing=arm;
   gene.regrowthTicks=Gene_TrollRegeneration.LimbRegrowthTicks-250;
   gene.Regenerate(250);
   Check(!troll.health.hediffSet.PartIsMissing(arm),"fed troll regrows a natural arm after three days");
   var appearance=troll.genes.GetGene(appearanceDef);
   troll.genes.RemoveGene(appearance);
   Check(Near(troll.BodySize,1f) && troll.equipment.Primary==sword,"removing appearance gene restores size without removing normal equipment");
   troll.genes.RemoveGene(troll.genes.GetGene(DefDatabase<GeneDef>.GetNamed("Skin_Green")));
   troll.genes.RemoveGene(troll.genes.GetGene(DefDatabase<GeneDef>.GetNamed("Body_Hulk")));
   if(diet!=null)troll.genes.RemoveGene(troll.genes.GetGene(diet));
   TrollAppearance.Migrate(troll);
   Check(diet!=null && troll.genes.HasActiveGene(diet),"legacy troll receives robust digestion on load");
   Check(TrollAppearance.Active(troll) && Near(troll.BodySize,1.5f) && troll.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Skin_Green")) && troll.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Body_Hulk")),"legacy troll migrates to modular vanilla phenotype and size gene");
   troll.genes.RemoveGene(gene);
   Check(troll.health.hediffSet.GetFirstHediffOfDef(metabolism)==null,"removing regeneration gene removes metabolism effect");
   troll.Destroy();
   CheckTrollRevival(xenotype);
  }
  void CheckTrollRegeneration(Pawn troll,Gene_TrollRegeneration gene){
   var set=troll.health.hediffSet;
   foreach(var h in set.hediffs.OfType<Hediff_Injury>().ToList())troll.health.RemoveHediff(h);
   troll.needs.food.CurLevel=troll.needs.food.MaxLevel;
   var torso=troll.RaceProps.body.corePart;
   var bruise=(Hediff_Injury)HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Bruise"),troll,torso);bruise.Severity=8;troll.health.AddHediff(bruise);
   var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,troll,torso);cut.Severity=2;troll.health.AddHediff(cut);
   Check(cut.BleedRate>0 && Near(bruise.BleedRate,0),"test wounds: cut bleeds, bruise does not");
   gene.Regenerate(250);
   Check(Near(cut.Severity,1.5f) && Near(bruise.Severity,8f),"troll heals bleeding wounds first");
   troll.health.RemoveHediff(cut);troll.health.RemoveHediff(bruise);
   var blood=troll.health.AddHediff(HediffDefOf.BloodLoss);blood.Severity=.3f;
   gene.Regenerate(250);
   Check(blood.Severity<.295f && blood.Severity>.285f,"troll regeneration restores lost blood");
   troll.health.RemoveHediff(blood);
   troll.needs.food.CurLevel=troll.needs.food.MaxLevel;
   var scar=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,troll,torso);scar.Severity=3;scar.TryGetComp<HediffComp_GetsPermanent>().IsPermanent=true;troll.health.AddHediff(scar);
   Check(scar.IsPermanent(),"test scar is permanent");
   gene.Regenerate(250);
   Check(set.hediffs.Contains(scar),"one regeneration tick does not remove a scar");
   gene.scarTicks=Gene_TrollRegeneration.ScarTicks-250;
   gene.Regenerate(250);
   Check(!set.hediffs.Contains(scar) && gene.scarTicks==0,"troll sheds an old scar after five fed days");
   var body=troll.RaceProps.body.AllParts;
   var eye=body.First(p=>p.def.defName=="Eye");
   Check(Gene_TrollRegeneration.CanRegrow(eye) && Gene_TrollRegeneration.CanRegrow(body.First(p=>p.def.defName=="Finger")) && !Gene_TrollRegeneration.CanRegrow(body.First(p=>p.def.defName=="Kidney")) && !Gene_TrollRegeneration.CanRegrow(body.First(p=>p.def==Troll.Brain)),"troll regrows outer parts but not organs or brain");
   var finger=body.First(p=>p.def.defName=="Finger");
   Check(Gene_TrollRegeneration.RegrowthTicks(finger)==60000 && Gene_TrollRegeneration.RegrowthTicks(body.First(p=>p.def.defName=="Arm"))==Gene_TrollRegeneration.LimbRegrowthTicks,"small parts regrow in one day, limbs in three");
   troll.needs.food.CurLevel=troll.needs.food.MaxLevel;
   gene.regrowing=null;gene.regrowthTicks=0;
   troll.health.AddHediff(HediffDefOf.MissingBodyPart,finger);
   gene.Regenerate(250);
   var shown=set.GetFirstHediffOfDef(Troll.Regrowth) as Hediff_TrollRegrowth;
   Check(gene.regrowing==finger && shown!=null && shown.Part==finger.parent,"regrowth progress shows on the parent part");
   gene.regrowthTicks=60000-250;
   gene.Regenerate(250);
   Check(!set.PartIsMissing(finger) && set.GetFirstHediffOfDef(Troll.Regrowth)==null,"troll regrows a finger and clears the progress hediff");
   Check(appearanceGeneCategoryOk(),"troll size gene is not filed under cosmetic");
   Check(Troll.Burns(DamageDefOf.Flame) && !Troll.Burns(DamageDefOf.Cut) && !Troll.Burns(DamageDefOf.Blunt),"fire detection covers flame but not cut or blunt");
   troll.needs.food.CurLevel=troll.needs.food.MaxLevel;
  }
  static bool appearanceGeneCategoryOk()=>DefDatabase<GeneDef>.GetNamed("LP_TrollAppearance").displayCategory?.defName=="Miscellaneous";
  // 随机生成的角色可能自带伤疤、缺失部位或植入体，会抢走再生和重长检查的目标，像 Person() 一样先清干净。
  static Pawn Unscathed(Pawn generated){foreach(var implant in generated.health.hediffSet.hediffs.OfType<Hediff_AddedPart>().ToList())generated.health.RestorePart(implant.Part);foreach(var injury in generated.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())generated.health.RemoveHediff(injury);foreach(var missing in generated.health.hediffSet.GetMissingPartsCommonAncestors().ToList())generated.health.RestorePart(missing.Part);return generated;}
  Pawn SpawnTroll(XenotypeDef xenotype){
   var troll=Unscathed(PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:xenotype,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25,fixedChronologicalAge:25)));
   GenSpawn.Spawn(troll,CellFinder.RandomClosewalkCellNear(altar.Position,map,8),map);
   troll.needs.food.CurLevel=troll.needs.food.MaxLevel;
   return troll;
  }
  void CheckTrollRevival(XenotypeDef xenotype){
   var revivals=Troll.Revivals;
   Check(revivals!=null && LichMod.Settings.trollRevival && LichMod.Settings.trollRevivalLimit==3,"troll revival component and default settings exist");
   var troll=SpawnTroll(xenotype);var gene=Troll.Gene(troll);
   troll.Kill(null);
   Check(troll.Dead && gene.reviveTick==Find.TickManager.TicksGame+Gene_TrollRegeneration.RevivalDelay && revivals.pending.Contains(troll),"dead troll schedules revival in one day");
   Check(troll.Corpse.GetInspectString().Contains("巨魔再生"),"troll corpse shows revival countdown");
   revivals.Process();
   Check(troll.Dead && gene.reviveTick>=0,"troll stays dead before the timer ends");
   // 1.82.0 热修：死者算“已销毁”，存档引用必须带 saveDestroyedThings；旧档丢失的条目读档时从尸体找回。
   revivals.pending.Remove(troll);
   Check(revivals.Rescan()>=1 && revivals.pending.Contains(troll),"troll corpse lost from the pending list is found again on load");
   gene.reviveTick=Find.TickManager.TicksGame;
   revivals.Process();
   Check(!troll.Dead && troll.Spawned && gene.revivals==1 && troll.health.hediffSet.HasHediff(Troll.Weakness) && !revivals.pending.Contains(troll),"troll revives in place with weakness");
   troll.Kill(null);
   var corpse=troll.Corpse;
   corpse.TakeDamage(new DamageInfo(DamageDefOf.Flame,1));
   Check(gene.reviveTick<0,"burning a troll corpse cancels revival");
   revivals.Process();
   Check(!revivals.pending.Contains(troll),"cancelled revival leaves the pending list");
   if(!corpse.Destroyed)corpse.Destroy();
   troll=SpawnTroll(xenotype);gene=Troll.Gene(troll);
   gene.Suppress();
   troll.Kill(null);
   Check(gene.reviveTick<0,"troll burned within six hours does not revive");
   troll.Corpse?.Destroy();
   troll=SpawnTroll(xenotype);gene=Troll.Gene(troll);
   troll.Kill(new DamageInfo(DamageDefOf.Flame,1));
   Check(gene.reviveTick<0,"troll killed by fire does not revive");
   troll.Corpse?.Destroy();
   troll=SpawnTroll(xenotype);gene=Troll.Gene(troll);
   var starving=troll.health.AddHediff(HediffDefOf.Malnutrition);
   troll.Kill(null,starving);
   Check(gene.reviveTick<0,"troll that starved to death does not revive");
   troll.Corpse?.Destroy();
   troll=SpawnTroll(xenotype);gene=Troll.Gene(troll);
   gene.revivals=LichMod.Settings.trollRevivalLimit;
   troll.Kill(null);
   Check(gene.reviveTick<0,"troll at revival limit stays dead");
   troll.Corpse?.Destroy();
   troll=SpawnTroll(xenotype);gene=Troll.Gene(troll);
   troll.Kill(null);
   corpse=troll.Corpse;
   corpse.Destroy();
   revivals.Process();
   Check(gene.reviveTick<0 || !revivals.pending.Contains(troll),"destroyed (cremated) troll corpse leaves the pending list");
  }
 }
}
