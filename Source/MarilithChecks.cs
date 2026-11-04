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
 // 1.102.0 起六臂蛇人是原版人类 + 异种型：有没有 HAR（Test.ps1 -NoHAR）都跑全部检查；有 HAR 时另跑联动程序集的一条（HAR 不拦这三个异种型）。另含旧存档转换（CheckLegacyRaceMapping）。
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
  // 1.102.0：[左肩, 右肩]（额外手臂长在肩膀上）。
  static BodyPartRecord MarilithShoulder(Pawn p,int side)=>Marilith.Shoulders(p)[side];
  static Faction MarilithEnemy=>Find.FactionManager.AllFactionsListForReading.First(f=>f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && !f.def.hidden && !f.temporary);

  void CheckMarilith(){
   bool har=ModsConfig.IsActive(HARId);
   // 1.102.0：六臂蛇人是原版人类 + 异种型，有没有 HAR 都加载；有 HAR 时联动程序集查 HAR 不拦这三个异种型。
   Check(Marilith.Loaded && DefDatabase<XenotypeDef>.GetNamedSilentFail(Marilith.Kin)!=null && MarilithRaid.Kind!=null && Marilith.RaceDef==null && har==(HARBridgeChecks!=null),"marilith xenotype and pawn kinds load with or without HAR (har="+har+"); the old HAR race is gone");
   if(har)RunHARBridge();
   worldBefore=new HashSet<Pawn>(Find.WorldPawns.AllPawnsAliveOrDead);
   var made=new List<Thing>();
   var m=MarilithSpawn(MarilithPawn(Faction.OfPlayer));var human=MarilithSpawn(Person());made.Add(m);made.Add(human);
   try{
    // ---------- 种族与身体（3） ----------
    var body=m.RaceProps.body.AllParts;
    Check(m.def==ThingDefOf.Human && Marilith.Is(m) && !Marilith.Is(human) && Near(m.BodySize,1.6f) && Near(human.BodySize,1f) && m.genes.Xenotype?.defName==Marilith.Kin && m.genes.HasActiveGene(Races124.Gene(Marilith.Core)) && Marilith.SkinGenes.Count(n=>m.genes.GetGene(Races124.Gene(n))!=null)==1 && m.story.bodyType==BodyTypeDefOf.Hulk && Items.Has(m,Marilith.SixArms)
     && Near(Races124.Gene(Marilith.Core).statOffsets.GetStatOffsetFromList(StatDefOf.MoveSpeed),-.2f) && Near(Races124.Gene(Marilith.Core).statOffsets.GetStatOffsetFromList(StatDefOf.ComfyTemperatureMin),-11f) && Near(Races124.Gene(Marilith.Core).statOffsets.GetStatOffsetFromList(StatDefOf.ComfyTemperatureMax),14f),
     "marilith colonist: human + marilith kin xenotype with the serpent-body core gene, body size 1.6, one scale colour, hulk body, six-arms state, race stats on the gene (move 4.4, comfort 5..40 C before apparel) ["+m.def.defName+" is "+Marilith.Is(m)+" size "+m.BodySize+" xeno "+m.genes.Xenotype?.defName+" core "+m.genes.HasActiveGene(Races124.Gene(Marilith.Core))+" skins "+Marilith.SkinGenes.Count(n=>m.genes.GetGene(Races124.Gene(n))!=null)+" body "+m.story.bodyType?.defName+" arms "+Items.Has(m,Marilith.SixArms)+" comfy "+m.GetStatValue(StatDefOf.ComfyTemperatureMin)+".."+m.GetStatValue(StatDefOf.ComfyTemperatureMax)+"]");
    Check(body.Count(x=>x.def==BodyPartDefOf.Hand)==2 && m.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
     && !ApparelUtility.HasPartsToWear(m,DefDatabase<ThingDef>.GetNamed("Apparel_Pants")) && !ApparelUtility.HasPartsToWear(m,DefDatabase<ThingDef>.GetNamed("Apparel_FlakPants")) && ApparelUtility.HasPartsToWear(m,DefDatabase<ThingDef>.GetNamed("Apparel_Duster")) && ApparelUtility.HasPartsToWear(m,DefDatabase<ThingDef>.GetNamed("Apparel_FlakVest")) && ApparelUtility.HasPartsToWear(human,DefDatabase<ThingDef>.GetNamed("Apparel_Pants")),
     "human body (two real hands, extra arms are gene render nodes); can move; pants cannot be worn (serpent tail), duster and flak vest can; a human still wears pants");
    var core=Races124.Gene(Marilith.Core);var nodes=core.renderNodeProperties.OfType<PawnRenderNodeProperties_LPPart>().ToList();
    int females=0;for(int i=0;i<20;i++){var g=PawnGenerator.GeneratePawn(new PawnGenerationRequest(MarilithKind,null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false));if(g.gender==Gender.Female)females++;g.Destroy();}
    Check(nodes.Count(n=>n.part.StartsWith("marilithArm"))==8 && nodes.Count(n=>n.part=="marilithTail")==2 && nodes.All(n=>ContentFinder<Texture2D>.Get(n.texPath+"_south",false)!=null) && ContentFinder<Texture2D>.Get("Marilith/Bodies/Naked_Hulk_south",false)!=null && females>=12,
     "serpent-body gene draws four extra arms and a tail (adult + child sets, textures found) over the legless upper body; "+females+"/20 generated marilith are female (85%)");
    // ---------- 六臂操作加成：额外手臂长在肩膀上（1） ----------
    m.health.capacities.Notify_CapacityLevelsDirty();float full=m.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);float b4=Marilith.ManipulationBonus(m);
    m.health.AddHediff(HediffDefOf.MissingBodyPart,MarilithShoulder(m,0));float b2=Marilith.ManipulationBonus(m);int pairs2=Marilith.UsablePairs(m);bool leftGone=!Marilith.SideUsable(m,0) && Marilith.SideUsable(m,1);
    foreach(var x in m.health.hediffSet.GetMissingPartsCommonAncestors().ToList())m.health.RestorePart(x.Part);
    var bionicArm=DefDatabase<HediffDef>.GetNamed("BionicArm");m.health.AddHediff(bionicArm,MarilithShoulder(m,1));float bBionic=Marilith.ManipulationBonus(m);
    foreach(var h in m.health.hediffSet.hediffs.Where(h=>h.def==bionicArm).ToList())m.health.RemoveHediff(h);
    Check(Near(b4,.4f) && Near(b2,.2f) && pairs2==1 && leftGone && Near(bBionic,.2f) && Near(Marilith.ManipulationBonus(m),.4f) && full>1.3f,"six arms: manipulation +40% with both sides ("+full.ToStringPercent()+"); losing the left shoulder takes its two extra arms (+20%, 1 pair); a bionic arm on the right shoulder replaces that side's extra arms too");
    // ---------- 副手：数量、伤害比例、不连锁（3） ----------
    var sword=MarilithWeapon("MeleeWeapon_LongSword",QualityCategory.Normal);m.equipment.DestroyAllEquipment();m.equipment.AddEquipment(sword);
    var knife=MarilithWeapon("MeleeWeapon_Knife",QualityCategory.Normal);var gladius=MarilithWeapon("MeleeWeapon_Gladius",QualityCategory.Masterwork);var axe=MarilithWeapon("MeleeWeapon_Axe",QualityCategory.Poor);
    foreach(var w in new[]{knife,gladius,axe})m.inventory.innerContainer.TryAdd(w);
    var offs=Marilith.Offhands(m);
    // 两只手都没了原版会扔下主手武器：没了的一侧用缺失，另一侧换成仿生臂（同样带走那一侧的额外手臂）。
    m.health.AddHediff(HediffDefOf.MissingBodyPart,MarilithShoulder(m,0));int oneSide=Marilith.OffhandSlots(m);
    m.health.AddHediff(bionicArm,MarilithShoulder(m,1));int noSide=Marilith.OffhandSlots(m);
    foreach(var x in m.health.hediffSet.GetMissingPartsCommonAncestors().ToList())m.health.RestorePart(x.Part);foreach(var h in m.health.hediffSet.hediffs.Where(h=>h.def==bionicArm).ToList())m.health.RemoveHediff(h);
    if(m.equipment.Primary!=sword){if(sword.Spawned)sword.DeSpawn();m.equipment.AddEquipment(sword);} // 原版断了肩膀会扔下主手武器
    human.genes.AddGene(Races124.Gene(Marilith.Blades),true);
    Check(offs.Count==2 && offs.SequenceEqual(new[]{knife,gladius,axe}.OrderByDescending(Marilith.Score).Take(2)) && offs.Contains(gladius) && oneSide==1 && noSide==0 && Marilith.OffhandSlots(human)==0 && Races124.Gene(Marilith.Blades).description.Contains("需要多于两条手臂"),"off-hands: best two inventory melee weapons ("+string.Join(",",offs.Select(o=>o.def.defName))+"); losing one shoulder drops to one, both to none; a human with the gene gets none (described as needing more than two arms)");
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
    Check(normal==2 && skill==0 && storm==3 && stormOff==2 && !ranged,"an ordinary melee hit adds one strike per off-hand ("+normal+") [slots "+Marilith.OffhandSlots(m)+" pairs "+Marilith.UsablePairs(m)+" inv "+Marilith.CarriedOffhands(m)+" primary "+m.equipment.Primary+" hediffs "+m.health.hediffSet.hediffs.Select(h=>h.def.defName+(h.Part!=null?"@"+h.Part.Label:"")).ToCommaList()+" downed "+m.Downed+"]; a skill hit (whirlwind path) adds none ("+skill+"); blade storm hits main + 2 off-hands without chaining ("+storm+"); no follow-up with a ranged main weapon");
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
    for(int side=0;side<2;side++)m.health.AddHediff(bionicArm,MarilithShoulder(m,side));
    var noArms=Marilith.TakeOffhandOption(m,ground);
    foreach(var h in m.health.hediffSet.hediffs.Where(h=>h.def==bionicArm).ToList())m.health.RemoveHediff(h);
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
    var torso=body.First(x=>x.def==BodyPartDefOf.Torso);
    Check(Near(BodySize.HealthFactor(m),1.3f) && Near(BodySize.MeleeFactor(m),1.18f) && Near(BodySize.HealthFactor(human),1f) && (FullModList || Near(m.HealthScale,human.HealthScale*1.3f)) && PartHealth.Maximum(torso,m)>PartHealth.Maximum(torso,human)*1.25f,"giant-body rule from the gene's size 1.6: health x1.3, melee x1.18; part health uses it ("+m.HealthScale.ToString("0.##")+" vs human "+human.HealthScale.ToString("0.##")+", torso "+PartHealth.Maximum(torso,m)+")");
    // ---------- 基因提取、手术、尸体（1） ----------
    var genes=GeneWorkbench.ExtractableGenes(m).Select(g=>g.defName).ToList();
    var bionic=DefDatabase<RecipeDef>.GetNamed("InstallBionicArm");var tadpole=DefDatabase<RecipeDef>.GetNamedSilentFail("LP_ImplantTadpole");
    var corpsePawn=MarilithSpawn(MarilithPawn(null));made.Add(corpsePawn);corpsePawn.Kill(null);var corpse=corpsePawn.Corpse;if(corpse!=null)made.Insert(0,corpse);
    Check(new[]{Marilith.Core,Marilith.Blades,Marilith.Glide,"LP_SerpentConstrict","LP_BladeStorm","LP_AbyssalHide",Marilith.ColdBlood}.All(genes.Contains) && m.def.AllRecipes.Contains(bionic) && bionic.Worker.GetPartsToApplyOn(m,bionic).Count()==2 && (tadpole==null || m.def.AllRecipes.Contains(tadpole))
     && corpse!=null && corpse.def==ThingDefOf.Human.race.corpseDef && corpse.InnerPawn.health.hediffSet.GetBrain()!=null && Marilith.Is(corpse.InnerPawn) && MindFlayer.CannotDevour(human,corpse)==null && Necro.Integrity(corpse)>0,"all seven marilith genes (incl. the core) are extractable; bionic arms fit the two shoulders; the corpse is a human corpse with a brain that still reads as a marilith (mind flayer can devour it, lich integrity works)");
    // ---------- 旧存档：HAR 种族转人类（1） ----------
    CheckLegacyRaceMapping();
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
   p.health.AddHediff(HediffDefOf.MissingBodyPart,MarilithShoulder(p,0));
   var a=p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SerpentConstrictAbility"));a.StartCooldown(30000);
   marilithSaveId=p.ThingID;marilithSaveCooldown=a.CooldownTicksRemaining;marilithSaveBonus=Marilith.ManipulationBonus(p);
  }
  void CheckMarilithAfterLoad(){
   if(!Marilith.Loaded)return;
   var p=Find.CurrentMap.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==marilithSaveId);
   var a=p?.abilities?.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SerpentConstrictAbility"));
   Check(p!=null && Marilith.Is(p) && Items.Has(p,Marilith.SixArms) && Near(Marilith.ManipulationBonus(p),marilithSaveBonus) && Near(marilithSaveBonus,.2f) && Near(p.BodySize,1.6f) && a!=null && a.CooldownTicksRemaining==marilithSaveCooldown,"marilith xenotype, six-arms bonus with a lost shoulder (+"+marilithSaveBonus+") and constrict cooldown survive save/load");
   if(p!=null)p.Destroy();
  }

  // 1.102.0 旧存档：HAR 种族名读成人类；挂在旧身体上的伤口按部位搬到人类身体（额外手臂、蛇尾、第二个头丢掉，独眼 → 左眼，第二个头缺失 → 孤首）；旧的六臂蛇人补上蛇身本源。
  void CheckLegacyRaceMapping(){
   var mb=DefDatabase<BodyDef>.GetNamed("LP_MarilithBody");var eb=DefDatabase<BodyDef>.GetNamed("LP_EttinBody");var cb=DefDatabase<BodyDef>.GetNamed("LP_CyclopsBody");var hb=BodyDefOf.Human;
   BodyPartRecord Old(BodyDef b,string label)=>b.AllParts.First(x=>x.untranslatedCustomLabel==label);
   var upper=LegacyRaceSave.Map(Old(mb,"左上手"),hb);var mid=LegacyRaceSave.Map(Old(mb,"左中臂"),hb);var tail=LegacyRaceSave.Map(mb.AllParts.First(x=>x.def.defName=="LP_SerpentTail"),hb);
   var leftHead=LegacyRaceSave.Map(Old(eb,"左头的大脑"),hb);var rightHead=LegacyRaceSave.Map(Old(eb,"右头"),hb);var eye=LegacyRaceSave.Map(cb.AllParts.First(x=>x.def.defName==Cyclops.Eye),hb);
   string names=BackCompatibility.BackCompatibleDefName(typeof(ThingDef),"LP_Ettin")+"/"+BackCompatibility.BackCompatibleDefName(typeof(ThingDef),"Corpse_LP_Marilith");
   // 一个旧的双头巨人：右头缺失、左手有伤，挂在旧身体上；转换后右头的缺失丢掉、得到孤首，左手的伤搬到人类左手。
   var e=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_EttinColonist"),Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:30,fixedChronologicalAge:30));
   foreach(var h in e.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_MissingPart || h is Hediff_AddedPart).ToList())e.health.RemoveHediff(h);
   var gone=HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,e);gone.Part=Old(eb,"右颈");var cut=HediffMaker.MakeHediff(HediffDefOf.Cut,e);cut.Part=Old(eb,"左手");cut.Severity=3f;
   e.health.hediffSet.hediffs.Add(gone);e.health.hediffSet.hediffs.Add(cut);
   LegacyRaceSave.Fix(e);
   bool ettinOk=Ettin.Lone(e) && !e.health.hediffSet.hediffs.Contains(gone) && cut.Part?.body==hb && cut.Part.def==BodyPartDefOf.Hand && !e.Dead;
   // 一个旧的六臂蛇人（读档前异种型已经是六臂蛇人，没有 1.102.0 的本源基因）。
   var m=MarilithPawn(Faction.OfPlayer);m.genes.RemoveGene(m.genes.GetGene(Races124.Gene(Marilith.Core)));bool before=Marilith.Is(m);LegacyRaceSave.EnsureCore(m);
   Check(upper!=null && upper.def==BodyPartDefOf.Hand && upper.body==hb && mid==null && tail==null && leftHead?.def.defName=="Brain" && rightHead==null && eye?.def==BodyPartDefOf.Eye && names=="Human/Corpse_Human" && ettinOk && !before && Marilith.Is(m) && Near(m.BodySize,1.6f),
    "old HAR saves: race names load as Human ("+names+"); wounds on the old bodies move to the matching human part (upper hand, left head's brain, cyclops eye -> left eye), extra arms / tail / right head are dropped; an old ettin missing its right head becomes a lone head; an old marilith gets the serpent-body core");
   e.Destroy();m.Destroy();
  }
 }
}
