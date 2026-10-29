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
 // 1.33.0 六臂蛇人（组名 Marilith：Test.ps1 -Only Marilith）。按 Peter 的要求少而关键：主程序集 ≤12 条 + HAR 联动程序集 6 条。
 // 没有 HAR（Test.ps1 -NoHAR）时只做静态检查：蛇人的定义和联动程序集都不存在。
 public partial class LichChecks {
  static string marilithSaveId;static int marilithSaveCooldown=-1;static float marilithSaveBonus;
  static Pawn marilithColdSubject;
  static bool MarilithColdTemperature(Thing __instance,ref float __result){if(__instance!=marilithColdSubject)return true;__result=-20f;return false;}
  static Thing marilithShielded;
  static bool MarilithNoDamage(Thing __instance,ref DamageWorker.DamageResult __result){if(__instance!=marilithShielded)return true;__result=new DamageWorker.DamageResult();return false;}
  static PawnKindDef MarilithKind=>DefDatabase<PawnKindDef>.GetNamed("LP_MarilithColonist");
  Pawn MarilithPawn(Faction faction){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(MarilithKind,faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:25,fixedChronologicalAge:25));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_AddedPart || h.def.isBad).ToList())p.health.RemoveHediff(h);
   foreach(var m in p.health.hediffSet.GetMissingPartsCommonAncestors().ToList())p.health.RestorePart(m.Part);
   return p;
  }
  Pawn MarilithSpawn(Pawn p,IntVec3? at=null){GenSpawn.Spawn(p,at??CellFinder.RandomClosewalkCellNear(altar.Position,map,6),map);return p;}
  static ThingWithComps MarilithWeapon(string def,QualityCategory q){var w=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(def),ThingDefOf.Steel);w.TryGetComp<CompQuality>()?.SetQuality(q,null);return w;}
  static BodyPartRecord MarilithPart(Pawn p,string label)=>p.RaceProps.body.AllParts.First(x=>x.untranslatedCustomLabel==label);
  static Faction MarilithEnemy=>Find.FactionManager.AllFactionsListForReading.First(f=>f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && !f.def.hidden && !f.temporary);

  void CheckMarilith(){
   bool har=ModsConfig.IsActive(HARId);
   Check(har==Marilith.Loaded && har==(DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_MarilithKin")!=null) && har==(HARBridgeChecks!=null) && har==(MarilithRaid.Kind!=null),"marilith content and the HAR bridge load exactly when Humanoid Alien Races is active ("+har+")");
   if(!har)return;
   RunHARBridge();
   worldBefore=new HashSet<Pawn>(Find.WorldPawns.AllPawnsAliveOrDead);
   var made=new List<Thing>();
   var m=MarilithSpawn(MarilithPawn(Faction.OfPlayer));var human=MarilithSpawn(Person());made.Add(m);made.Add(human);
   try{
    // ---------- 种族与身体（2） ----------
    var body=m.RaceProps.body.AllParts;
    Check(m.def.GetType().FullName=="AlienRace.ThingDef_AlienRace" && Marilith.Is(m) && !Marilith.Is(human) && Near(m.BodySize,1.6f) && m.genes.Xenotype?.defName=="LP_MarilithKin" && Marilith.SkinGenes.Count(n=>m.genes.GetGene(Races124.Gene(n))!=null)==1 && m.story.bodyType==BodyTypeDefOf.Hulk && Items.Has(m,Marilith.SixArms),"marilith colonist: HAR race, body size 1.6, marilith kin xenotype with one scale colour, hulk body, six-arms state");
    Check(body.Count(x=>x.def==BodyPartDefOf.Hand)==6 && body.Count(x=>x.def==BodyPartDefOf.Shoulder)==6 && body.Count(x=>x.def.defName=="LP_SerpentTail")==1 && !body.Any(x=>x.def==BodyPartDefOf.Leg || x.def.defName=="Foot") && m.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
     && !ApparelUtility.HasPartsToWear(m,DefDatabase<ThingDef>.GetNamed("Apparel_Pants")) && !ApparelUtility.HasPartsToWear(m,DefDatabase<ThingDef>.GetNamed("Apparel_FlakPants")) && ApparelUtility.HasPartsToWear(m,DefDatabase<ThingDef>.GetNamed("Apparel_Duster")) && ApparelUtility.HasPartsToWear(m,DefDatabase<ThingDef>.GetNamed("Apparel_FlakVest")),
     "six hands, one serpent tail, no legs; can move; pants cannot be worn, duster and flak vest can");
    // ---------- 六臂操作加成随断臂变化（1） ----------
    m.health.capacities.Notify_CapacityLevelsDirty();float full=m.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);float b4=Marilith.ManipulationBonus(m);
    m.health.AddHediff(HediffDefOf.MissingBodyPart,MarilithPart(m,"左中臂"));float b3=Marilith.ManipulationBonus(m);int pairs3=Marilith.UsablePairs(m);
    m.health.AddHediff(HediffDefOf.MissingBodyPart,MarilithPart(m,"右中臂"));float b2=Marilith.ManipulationBonus(m);int pairs2=Marilith.UsablePairs(m);
    m.health.capacities.Notify_CapacityLevelsDirty();float less=m.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
    Check(Near(b4,.4f) && Near(b3,.3f) && Near(b2,.2f) && pairs3==2 && pairs2==1 && less<full-.15f && full>1.3f,"six arms: manipulation +40% with all extra hands ("+full.ToStringPercent()+"), +30% after losing one middle arm, +20% after losing both ("+less.ToStringPercent()+"); usable extra pairs 2 -> 1");
    foreach(var x in m.health.hediffSet.GetMissingPartsCommonAncestors().ToList())m.health.RestorePart(x.Part);
    // ---------- 副手：数量、伤害比例、不连锁（3） ----------
    var sword=MarilithWeapon("MeleeWeapon_LongSword",QualityCategory.Normal);m.equipment.DestroyAllEquipment();m.equipment.AddEquipment(sword);
    var knife=MarilithWeapon("MeleeWeapon_Knife",QualityCategory.Normal);var gladius=MarilithWeapon("MeleeWeapon_Gladius",QualityCategory.Masterwork);var axe=MarilithWeapon("MeleeWeapon_Axe",QualityCategory.Poor);
    foreach(var w in new[]{knife,gladius,axe})m.inventory.innerContainer.TryAdd(w);
    var offs=Marilith.Offhands(m);
    m.health.AddHediff(HediffDefOf.MissingBodyPart,MarilithPart(m,"左下臂"));int oneLower=Marilith.OffhandSlots(m);
    m.health.AddHediff(HediffDefOf.MissingBodyPart,MarilithPart(m,"右下臂"));int noLower=Marilith.OffhandSlots(m);
    foreach(var x in m.health.hediffSet.GetMissingPartsCommonAncestors().ToList())m.health.RestorePart(x.Part);
    human.genes.AddGene(Races124.Gene(Marilith.Blades),true);
    Check(offs.Count==2 && offs.SequenceEqual(new[]{knife,gladius,axe}.OrderByDescending(Marilith.Score).Take(2)) && offs.Contains(gladius) && oneLower==2 && noLower==1 && Marilith.OffhandSlots(human)==0 && Races124.Gene(Marilith.Blades).description.Contains("需要多于两条手臂"),"off-hands: best two inventory melee weapons ("+string.Join(",",offs.Select(o=>o.def.defName))+"); one lower arm keeps the pair, losing both drops to one; a human with the gene gets none (described as needing more than two arms)");
    float half=MarilithOffhand.HitAmount(m,gladius,.5f),whole=MarilithOffhand.HitAmount(m,gladius,1f);float old=LichMod.Settings.offhandDamage;
    LichMod.Settings.offhandDamage=.25f;float quarter=MarilithOffhand.HitAmount(m,gladius,Marilith.OffhandDamage);LichMod.Settings.offhandDamage=old;
    Check(whole>0 && Near(half/whole,.5f) && Near(quarter/whole,.25f),"off-hand strike damage follows the setting: x0.5 default ("+half.ToString("0.#")+" of "+whole.ToString("0.#")+"), x0.25 at the minimum");
    var foe=MarilithSpawn(PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,MarilithEnemy,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,fixedBiologicalAge:25,fixedChronologicalAge:25)),m.Position+IntVec3.East);made.Add(foe);
    foe.health.AddHediff(HediffDefOf.Anesthetic);foe.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic).Severity=1f;foe.health.CheckForStateChange(null,null);
    // 计数检查时目标不受伤（拦掉 TakeDamage），免得被前几下打死而少算。
    var shield=new Harmony("local.lich.marilithshield");var takeDamage=AccessTools.Method(typeof(Thing),nameof(Thing.TakeDamage));int normal,skill,storm,stormOff;
    try{shield.Patch(takeDamage,prefix:new HarmonyMethod(typeof(LichChecks),nameof(MarilithNoDamage)));marilithShielded=foe;
    Verb_MeleeAttack verb=CompBladeStorm.MainVerb(m,foe);
    AccessTools.Field(typeof(Verb),"currentTarget").SetValue(verb,new LocalTargetInfo(foe));
    int s0=MarilithOffhand.Strikes;AccessTools.Method(typeof(Verb_MeleeAttack),"TryCastShot").Invoke(verb,null);normal=MarilithOffhand.Strikes-s0;
    m.stances.SetStance(new Stance_Mobile());
    s0=MarilithOffhand.Strikes;AccessTools.Method(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget").Invoke(CompBladeStorm.MainVerb(m,foe),new object[]{new LocalTargetInfo(foe)});skill=MarilithOffhand.Strikes-s0;
    s0=MarilithOffhand.Strikes;storm=CompBladeStorm.Storm(m);stormOff=MarilithOffhand.Strikes-s0;
    }finally{marilithShielded=null;shield.Unpatch(takeDamage,HarmonyPatchType.Prefix,shield.Id);}
    var bow=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Bow_Short"));m.equipment.TryTransferEquipmentToContainer(sword,m.inventory.innerContainer);m.equipment.AddEquipment(bow);bool ranged=MarilithOffhand.Eligible(CompBladeStorm.MainVerb(m,foe));
    m.equipment.DestroyAllEquipment();
    Check(normal==2 && skill==0 && storm==3 && stormOff==2 && !ranged,"an ordinary melee hit adds one strike per off-hand ("+normal+"); a skill hit (whirlwind path) adds none ("+skill+"); blade storm hits main + 2 off-hands without chaining ("+storm+"); no follow-up with a ranged main weapon");
    // ---------- 1.52.0 补丁：拿作副手、卸货保留副手（3） ----------
    m.inventory.innerContainer.ClearAndDestroyContents();
    var ground=MarilithWeapon("MeleeWeapon_Gladius",QualityCategory.Normal);GenSpawn.Spawn(ground,CellFinder.RandomClosewalkCellNear(m.Position,map,3,c=>c.GetFirstItem(map)==null),map);made.Add(ground);
    Func<FloatMenuOption> takeOpt=()=>FloatMenuMakerMap.GetOptions(new List<Pawn>{m},ground.DrawPos,out _).FirstOrDefault(o=>o.Label.StartsWith("拿作副手"));
    var opt=takeOpt();var humanOpt=Marilith.TakeOffhandOption(human,ground);var plain=MarilithSpawn(Person());made.Add(plain);
    Check(map.IsPlayerHome && opt!=null && !opt.Disabled && opt.Label.Contains(ground.LabelShort) && humanOpt!=null && humanOpt.Disabled && humanOpt.Label.Contains("缺少能用的额外手臂") && Marilith.TakeOffhandOption(plain,ground)==null,"home map: right-clicking a melee weapon offers an enabled take-as-off-hand option to the marilith ("+opt?.Label+"); a two-armed human with the blades gene sees it greyed out, a plain human not at all");
    var k2=MarilithWeapon("MeleeWeapon_Knife",QualityCategory.Normal);var g2=MarilithWeapon("MeleeWeapon_Gladius",QualityCategory.Masterwork);var a2=MarilithWeapon("MeleeWeapon_Axe",QualityCategory.Poor);
    foreach(var w in new[]{k2,g2,a2})m.inventory.innerContainer.TryAdd(w);
    var kept=Marilith.Offhands(m);var surplus=new[]{k2,g2,a2}.First(w=>!kept.Contains(w));var order=m.inventory.innerContainer.ToList();
    m.inventory.UnloadEverything=true;var first=m.inventory.FirstUnloadableThing.Thing;bool sameOrder=order.SequenceEqual(m.inventory.innerContainer);
    Thing dropped=null;if(first!=null){m.inventory.innerContainer.TryDrop(first,m.Position,map,ThingPlaceMode.Near,out dropped);if(dropped!=null)made.Add(dropped);}
    bool more=m.inventory.FirstUnloadableThing.Thing!=null;m.inventory.InventoryTrackerTick();
    Check(kept.Count==2 && first==surplus && sameOrder && !more && !m.inventory.UnloadEverything && kept.All(m.inventory.innerContainer.Contains),"unload-everything drops only the surplus weapon ("+first?.def.defName+"), keeps the 2 chosen off-hands, then the unload flag clears (no loop)");
    var atCap=takeOpt();
    foreach(var arm in new[]{"左中臂","右中臂","左下臂","右下臂"})m.health.AddHediff(HediffDefOf.MissingBodyPart,MarilithPart(m,arm));
    var noArms=Marilith.TakeOffhandOption(m,ground);
    foreach(var x in m.health.hediffSet.GetMissingPartsCommonAncestors().ToList())m.health.RestorePart(x.Part);
    Check(atCap!=null && atCap.Disabled && atCap.Label.Contains("没有空闲的手") && noArms!=null && noArms.Disabled && noArms.Label.Contains("缺少能用的额外手臂"),"take-as-off-hand is disabled at the cap ("+atCap?.Label+") and without usable extra arms ("+noArms?.Label+")");
    // ---------- 缠绕、剑刃风暴的冷却（1） ----------
    if(foe.Dead){foe=MarilithSpawn(PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,MarilithEnemy,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true)),m.Position+IntVec3.West);made.Add(foe);}
    var constrict=m.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SerpentConstrictAbility"));var bladeStorm=m.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_BladeStormAbility"));
    bool cast=constrict!=null && constrict.Activate(new LocalTargetInfo(foe),LocalTargetInfo.Invalid);
    var held=foe.health.hediffSet.GetFirstHediffOfDef(Items.Hediff(Marilith.Constricted)) as Hediff_Constricted;
    float sq=held?.Squeeze()??0f;
    bool stormCast=bladeStorm!=null && bladeStorm.Activate(new LocalTargetInfo(m),LocalTargetInfo.Invalid);
    Check(cast && held!=null && held.constrictor==m && sq>0f && m.stances.FullBodyBusy && Items.Has(m,Marilith.Constricting) && constrict.CooldownTicksRemaining>29900 && stormCast && bladeStorm.CooldownTicksRemaining>119900,"constrict holds the foe (squeeze "+sq.ToString("0.#")+") and keeps the marilith in place, 12-hour cooldown; blade storm has a 2-day cooldown");
    // ---------- 蛇行、冷血（1） ----------
    var mud=DefDatabase<TerrainDef>.GetNamed("Mud");var shallow=DefDatabase<TerrainDef>.GetNamed("WaterShallow");var marsh=DefDatabase<TerrainDef>.GetNamed("Marsh");
    var over=AccessTools.Method(typeof(Pawn_PathFollower),"GetPawnCellBaseCostOverride");
    IntVec3 cell=GenRadial.RadialCellsAround(m.Position,8,false).First(c=>c.InBounds(map) && c.Standable(map) && c.GetEdifice(map)==null);var oldTerrain=cell.GetTerrain(map);
    map.terrainGrid.SetTerrain(cell,mud);int? mCost=(int?)over.Invoke(null,new object[]{m,cell}),hCost=(int?)over.Invoke(null,new object[]{human,cell});map.terrainGrid.SetTerrain(cell,oldTerrain);
    var probe=new Harmony("local.lich.marilithcold");var ambient=AccessTools.PropertyGetter(typeof(Thing),"AmbientTemperature");float warm=StatDefOf.MoveSpeed.Worker.GetValue(StatRequest.For(m)),cold;
    try{probe.Patch(ambient,prefix:new HarmonyMethod(typeof(LichChecks),nameof(MarilithColdTemperature)));marilithColdSubject=m;cold=StatDefOf.MoveSpeed.Worker.GetValue(StatRequest.For(m));}
    finally{marilithColdSubject=null;probe.Unpatch(ambient,HarmonyPatchType.Prefix,probe.Id);}
    Check(mCost==mud.pathCost/2 && hCost==null && Marilith.GlideCost(m,shallow,null)==shallow.pathCost/2 && Marilith.GlideCost(m,marsh,null)==marsh.pathCost/2 && Near(cold/warm,.85f),"serpent glide halves mud ("+mud.pathCost+" -> "+mCost+"), marsh and shallow water costs; cold blood x0.85 move speed below 0 C");
    // ---------- 巨躯规则、部位生命（1） ----------
    Check(Near(BodySize.HealthFactor(m),1.3f) && Near(BodySize.MeleeFactor(m),1.18f) && Near(BodySize.HealthFactor(human),1f) && (FullModList || Near(m.HealthScale,human.HealthScale*1.3f)) && PartHealth.Maximum(body.First(x=>x.def.defName=="LP_SerpentTail"),m)>=40f*1.3f-1f,"giant-body rule from race size 1.6: health x1.3, melee x1.18; serpent tail max health uses it ("+m.HealthScale.ToString("0.##")+" vs human "+human.HealthScale.ToString("0.##")+", tail "+PartHealth.Maximum(body.First(x=>x.def.defName=="LP_SerpentTail"),m)+")");
    // ---------- 基因提取、手术、尸体（1） ----------
    var genes=GeneWorkbench.ExtractableGenes(m).Select(g=>g.defName).ToList();
    var bionic=DefDatabase<RecipeDef>.GetNamed("InstallBionicArm");var bionicLeg=DefDatabase<RecipeDef>.GetNamed("InstallBionicLeg");var tadpole=DefDatabase<RecipeDef>.GetNamedSilentFail("LP_ImplantTadpole");
    var corpsePawn=MarilithSpawn(MarilithPawn(null));made.Add(corpsePawn);corpsePawn.Kill(null);var corpse=corpsePawn.Corpse;if(corpse!=null)made.Insert(0,corpse);
    Check(new[]{Marilith.Blades,Marilith.Glide,"LP_SerpentConstrict","LP_BladeStorm","LP_AbyssalHide",Marilith.ColdBlood}.All(genes.Contains) && m.def.AllRecipes.Contains(bionic) && bionic.Worker.GetPartsToApplyOn(m,bionic).Count()==6 && !bionicLeg.Worker.GetPartsToApplyOn(m,bionicLeg).Any() && (tadpole==null || m.def.AllRecipes.Contains(tadpole))
     && corpse!=null && corpse.InnerPawn.RaceProps.Humanlike && corpse.InnerPawn.health.hediffSet.GetBrain()!=null && MindFlayer.CannotDevour(human,corpse)==null && Necro.Integrity(corpse)>0,"all six marilith genes are extractable; bionic arms fit any of the 6 shoulders, no leg surgery; the corpse is humanlike with a brain (mind flayer can devour it, lich integrity works)");
    // ---------- Isekai（只在启用时，1） ----------
    var isekai=GenTypes.GetTypeInAnyAssembly("IsekaiLeveling.IsekaiComponent");
    if(isekai!=null){var get=AccessTools.Method(isekai,"GetCached");Check(get!=null && get.Invoke(null,new object[]{m})!=null,"Isekai attaches its component to a marilith colonist (Isekai traits and milestones apply)");}
    // ---------- 塞伊远征军（1） ----------
    var thay=Factions.Hostile;MarilithRaid.TestRoll=0f;
    try{
     var big=FactionGroup(thay,PawnGroupKindDefOf.Combat,1200f);var small=FactionGroup(thay,PawnGroupKindDefOf.Combat,900f);
     var blade=big.Where(Marilith.Is).ToList();
     Check(blade.Count==1 && blade[0].kindDef==MarilithRaid.Kind && blade[0].genes.Xenotype?.defName=="LP_MarilithKin" && Marilith.Offhands(blade[0]).Count>=1 && !small.Any(Marilith.Is),"Thay 1200-point raid brings one marilith blademaster with off-hand blades; 900 points brings none");
    }finally{MarilithRaid.TestRoll=null;CleanFactionPawns();}
   }finally{
    foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
   }
  }

  // ---------- 存读档（1） ----------
  void PrepareMarilithSave(){
   if(!Marilith.Loaded)return;
   var p=MarilithPawn(Faction.OfPlayer);GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);
   p.health.AddHediff(HediffDefOf.MissingBodyPart,MarilithPart(p,"左下臂"));
   var a=p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SerpentConstrictAbility"));a.StartCooldown(30000);
   marilithSaveId=p.ThingID;marilithSaveCooldown=a.CooldownTicksRemaining;marilithSaveBonus=Marilith.ManipulationBonus(p);
  }
  void CheckMarilithAfterLoad(){
   if(!Marilith.Loaded)return;
   var p=Find.CurrentMap.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==marilithSaveId);
   var a=p?.abilities?.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SerpentConstrictAbility"));
   Check(p!=null && Marilith.Is(p) && Items.Has(p,Marilith.SixArms) && Near(Marilith.ManipulationBonus(p),marilithSaveBonus) && Near(marilithSaveBonus,.3f) && Near(p.BodySize,1.6f) && a!=null && a.CooldownTicksRemaining==marilithSaveCooldown,"marilith race, six-arms bonus with a lost arm (+"+marilithSaveBonus+") and constrict cooldown survive save/load");
   if(p!=null)p.Destroy();
  }
 }
}
