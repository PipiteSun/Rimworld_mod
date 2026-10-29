using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // 1.45.0：Test.ps1 -Only MagicItems。魔法物品：定义与贴图、研究、两枚戒指、诅咒与鉴定、魔化兵器、防护奇物、器物、魔像手册与石魔像（含存读档）。
 public partial class LichChecks {
  static string magicGolemId,magicLichId,magicCursedId;
  public static readonly string[] MagicItemDefs={
   "LP_FlameTongue","LP_FrostBrand","LP_VorpalSword","LP_DragonSlayer","LP_DefenderSword","LP_JavelinLightning","LP_SwordVengeance",
   "LP_GauntletsOgrePower","LP_GlovesMissileSnaring","LP_BracersArchery","LP_BracersDefense","LP_HelmTelepathy","LP_EyesEagle","LP_EyesMinuteSeeing","LP_PeriaptWoundClosure","LP_AmuletProofDetection","LP_RingSpellTurning","LP_RingFreeAction","LP_RingRegeneration",
   "LP_RingProtection","LP_RingWardingBond","LP_WardingBondCase","LP_RingSpellStoring","LP_RingAnimalInfluence","LP_RingShootingStars","LP_NecklaceAdaptation","LP_NecklaceFireballs","LP_PrayerBeads","LP_BroochShielding","LP_ScarabProtection","LP_AmuletPlanes","LP_GogglesNight",
   "LP_HaloOfLight","LP_MartyrsBeads","LP_MartyrsBeadsBroken","LP_PuritySeal","LP_SpiritStone","LP_PsychicHood","LP_CogOfMakers","LP_InquisitorSeal","LP_WaaaghTotem","LP_RevivalCore","LP_MarkBlood","LP_MarkDecay","LP_MarkChange","LP_MarkRapture",
   "LP_ImmovableRod","LP_ImmovableRodActive","LP_DecanterEndlessWater","LP_EversmokingBottle","LP_EverburningTorch","LP_HornValhalla","LP_BagOfTricks","LP_CrystalBall","LP_ManualOfGolems","LP_StoneGolemSeed","LP_ScrollRemoveCurse"};
  Apparel MWear(Pawn p,string def){var d=DefDatabase<ThingDef>.GetNamed(def);var a=(Apparel)ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);p.apparel.Wear(a,false);return a;}
  void CheckMagicItems(){
   var before=new HashSet<Thing>(map.listerThings.AllThings);
   // 1. 定义、贴图
   var bad=new List<string>();
   foreach(var n in MagicItemDefs){var d=DefDatabase<ThingDef>.GetNamedSilentFail(n);if(d==null || d.ConfigErrors().Any() || d.uiIcon==null || d.uiIcon==BaseContent.BadTex || d.graphic==BaseContent.BadGraphic)bad.Add(n+(d==null?"":"("+string.Join(";",d.ConfigErrors())+")"));}
   Check(bad.Count==0,"all "+MagicItemDefs.Length+" magic item definitions load with their own textures: "+string.Join(",",bad));
   Check(ContentFinder<Texture2D>.Get("Items/Magic/HaloGlow",false)!=null && ContentFinder<Texture2D>.Get("Necromancy/StoneGolem",false)!=null && ContentFinder<Texture2D>.Get("Items/Magic/JavelinLightningFlight",false)!=null,"halo glow, javelin flight and stone golem icon textures load");
   // 2. 研究：6项都在 DND 页签，前置在左边。
   var res=new[]{("LP_Research_Enchanting",2f,.7f),("LP_Research_EnchantedArms",3f,0f),("LP_Research_WondrousCraft",3f,.7f),("LP_Research_SacredRelics",3f,1.4f),("LP_Research_WondrousDevices",4f,.7f),("LP_Research_GolemManual",5f,.7f)};
   Check(res.All(r=>{var d=DefDatabase<ResearchProjectDef>.GetNamedSilentFail(r.Item1);return d!=null && d.tab?.defName=="LP_FireGiantTech" && Near(d.ResearchViewX,r.Item2) && Near(d.ResearchViewY,r.Item3);}),"six magic item research projects sit in the DND tab at their planned cells");
   Check(DefDatabase<ApparelLayerDef>.GetNamed("LP_Accessory").label=="奇物" && DefDatabase<RecipeDef>.GetNamed("Make_LP_FlameTongue").AllRecipeUsers.Any(u=>u.defName=="LP_FireGiantForge") && DefDatabase<RecipeDef>.GetNamed("LP_MakeHaloOfLight").researchPrerequisite.defName=="LP_Research_SacredRelics","accessory layer is renamed 奇物; flame tongue is forged at the molten forge; halo is a sacred relic recipe");
   var spot=ItemSpot(3);var p=ItemPawn(spot);Hold(p);
   // 3. 两枚戒指
   var body=p.RaceProps.body;Func<string,ThingDef> D=n=>DefDatabase<ThingDef>.GetNamed(n);
   Check(ApparelUtility.CanWearTogether(D("LP_RingResistanceFire"),D("LP_RingProtection"),body) && !ApparelUtility.CanWearTogether(D("LP_RingProtection"),D("LP_RingProtection"),body) && ApparelUtility.CanWearTogether(D("LP_GauntletsOgrePower"),D("LP_RingProtection"),body) && !ApparelUtility.CanWearTogether(D("LP_PeriaptWoundClosure"),D("LP_AmuletProofDetection"),body),"two different rings fit together, the same ring twice does not, gloves fit with rings, two neck items conflict");
   var r1=MWear(p,"LP_RingResistanceFire");var r2=MWear(p,"LP_RingProtection");var r3=MWear(p,"LP_RingFreeAction");
   Check(p.apparel.WornApparel.Count(a=>MagicSlots.Of(a.def)?.slot=="Ring")==2 && !p.apparel.WornApparel.Contains(r1) && p.apparel.WornApparel.Contains(r3),"a third ring replaces the oldest of two");
   float armor=p.GetStatValue(StatDefOf.ArmorRating_Sharp);Check(armor>=.05f,"ring of protection adds sharp armor: "+armor);
   // 4. 自由行动戒指挡住黏胶袋
   Items.Give(p,"LP_Tanglefoot",600);Check(!Items.Has(p,"LP_Tanglefoot"),"ring of free action blocks the tanglefoot slow");
   p.apparel.DestroyAll();foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && t.def.defName.StartsWith("LP_Ring")).ToList())t.Destroy();
   // 5. 诅咒：穿上脱不下；识破卷轴鉴定；解除诅咒卷轴后能脱；设置关掉时能脱。
   var mark=MWear(p,"LP_MarkBlood");var cc=mark.GetComp<CompCursed>();
   Check(mark.LabelNoCount.Contains("未鉴定") && !mark.LabelNoCount.Contains("鲜血") && mark.DescriptionFlavor==cc.Props.disguiseDescription,"an unidentified cursed mark shows its disguise label and description: "+mark.LabelNoCount);
   Check(!p.apparel.TryDrop(mark,out _,p.Position,false) && p.apparel.WornApparel.Contains(mark),"a cursed mark cannot be taken off");
   Check(!EquipmentUtility.CanEquip(ThingMaker.MakeThing(D("LP_MarkDecay")),p,out var why,false) && why.Contains("诅咒"),"another mark cannot replace the cursed one: "+why);
   ItemScrolls.TrueSeeing(map);Check(cc.identified && mark.LabelNoCount.Contains("鲜血印记") && mark.LabelNoCount.Contains("诅咒"),"the true seeing scroll identifies the cursed mark: "+mark.LabelNoCount);
   Check(Near(p.GetStatValue(StatDefOf.MeleeDamageFactor),1.3f) || p.GetStatValue(StatDefOf.MeleeDamageFactor)>1.25f,"the blood mark adds 30% melee damage");
   bool old=LichMod.Settings.cursedItems;
   try{LichMod.Settings.cursedItems=false;Check(p.apparel.TryDrop(mark,out var dropped,p.Position,false),"with the cursed-items setting off the mark comes off");if(!dropped.Destroyed)p.apparel.Wear((Apparel)dropped,false);}finally{LichMod.Settings.cursedItems=old;}
   var scroll=MakeItem("LP_ScrollRemoveCurse");GenSpawn.Spawn(scroll,p.Position,map);p.skills.GetSkill(SkillDefOf.Intellectual).Level=12;Apparel freed=null;
   Check(ItemScrolls.Read(p,scroll,new LocalTargetInfo(p),.99f) && cc.lifted && p.apparel.TryDrop(mark,out freed,p.Position,false),"the remove curse scroll lifts the curse and the mark can be dropped");
   freed?.Destroy();
   // 6. 复仇之剑：拿起来放不下。
   var vengeance=(ThingWithComps)ThingMaker.MakeThing(D("LP_SwordVengeance"));p.equipment.AddEquipment(vengeance);
   Check(!p.equipment.TryDropEquipment(vengeance,out _,p.Position,false) && p.equipment.Primary==vengeance && !EquipmentUtility.CanEquip(ThingMaker.MakeThing(D("MeleeWeapon_LongSword"),ThingDefOf.Steel),p),"the sword of vengeance cannot be dropped or swapped");
   magicCursedId=vengeance.ThingID;
   // 7. 火焰舌：命中巨魔让再生中断；关掉燃火就不附带。
   var troll=RacePawn("LP_Troll");Place(troll,spot+IntVec3.East*2);Hold(troll);var gene=Troll.Gene(troll);gene.suppressedUntilTick=0;
   var flame=(ThingWithComps)ThingMaker.MakeThing(D("LP_FlameTongue"));var fext=flame.def.GetModExtension<MagicWeaponExtension>();var swordsman=ItemPawn(spot+IntVec3.West*2);Hold(swordsman);swordsman.equipment.AddEquipment(flame);
   Check(swordsman.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_FlameToggle"))!=null,"holding the flame tongue grants the 燃火 toggle");
   flame.GetComp<CompMagicWeapon>().active=false;MagicEffects.OnWeaponHit(swordsman,flame,fext,troll,new DamageWorker.DamageResult());bool offBurn=gene.suppressedUntilTick>Find.TickManager.TicksGame;
   flame.GetComp<CompMagicWeapon>().active=true;MagicEffects.OnWeaponHit(swordsman,flame,fext,troll,new DamageWorker.DamageResult());
   Check(!offBurn && gene.suppressedUntilTick>Find.TickManager.TicksGame,"a lit flame tongue hit counts as fire for the troll (regeneration suppressed), an unlit one does not");
   troll.Destroy();
   // 8. 锐锋剑：生命<25%、命中头部、体型<2、非巫妖。
   var victim=ItemPawn(spot+IntVec3.North*2);Hold(victim);
   Check(MagicEffects.BeheadBlocked(victim,true,.2f)==null && MagicEffects.BeheadBlocked(victim,true,.3f)!=null && MagicEffects.BeheadBlocked(victim,false,.2f)!=null,"vorpal beheading needs a head hit below 25% health");
   var giant=RacePawn("LP_FireGiant");Place(giant,spot+IntVec3.South*2);Hold(giant);bool bigBlocked=giant.BodySize>=2f && MagicEffects.BeheadBlocked(giant,true,.1f)!=null;giant.Destroy();
   Check(bigBlocked && MagicEffects.Behead(swordsman,victim) && victim.Dead,"the vorpal sword beheads a normal victim and spares big bodies");
   victim.Corpse?.Destroy();
   // 9. 龙杀手：对龙 ×1.5，对人 ×1；伤害 -30% 的守御。
   var dragon=RacePawn("LP_ForceDragon");Place(dragon,spot+IntVec3.North*3);Hold(dragon);
   Check(Near(MagicEffects.DragonFactor(D("LP_DragonSlayer"),dragon),1.5f) && Near(MagicEffects.DragonFactor(D("LP_DragonSlayer"),swordsman),1f),"dragon slayer deals x1.5 to dragons only");
   MagicEffects.OnWeaponHit(swordsman,(ThingWithComps)ThingMaker.MakeThing(D("LP_DragonSlayer")),D("LP_DragonSlayer").GetModExtension<MagicWeaponExtension>(),dragon,new DamageWorker.DamageResult());
   Check(Items.Has(dragon,"LP_DeflectSuppressed") && TicksLeft(dragon,"LP_DeflectSuppressed")==300,"a dragon slayer hit suppresses deflection for 5 seconds");
   dragon.Destroy();
   swordsman.equipment.DestroyAllEquipment();swordsman.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(D("LP_DefenderSword")));float md=swordsman.GetStatValue(StatDefOf.MeleeDamageFactor);
   swordsman.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_DefenderStance")).Activate(new LocalTargetInfo(swordsman),LocalTargetInfo.Invalid);
   Check(Items.Has(swordsman,"LP_DefenderStance") && Near(swordsman.GetStatValue(StatDefOf.MeleeDamageFactor),md*.7f),"defender stance cuts melee damage by 30%");
   swordsman.equipment.DestroyAllEquipment();swordsman.health.hediffSet.hediffs.RemoveAll(h=>h.def.defName=="LP_DefenderStance");
   // 10. 护盾胸针、圣甲虫、伤口闭合护符、食人魔手套。
   MWear(swordsman,"LP_BroochShielding");float force=ItemDamage.Factor(swordsman,DefDatabase<DamageDef>.GetNamed("LP_Force"));var scarab=MWear(swordsman,"LP_ScarabProtection");
   Check(Near(force,.25f) && Near(ItemDamage.Factor(swordsman,LichSpells.Necrotic),.5f) && Items.Worn(swordsman,"LP_BroochShielding")==null,"brooch of shielding cuts force damage by 75%, scarab cuts necrotic by 50% (both pin to the chest, one at a time)");
   int resisted=0;for(int i=0;i<12;i++)if(MagicItems.ScarabResists(swordsman))resisted++;
   Check(resisted==12 && scarab.Destroyed && !MagicItems.ScarabResists(swordsman) && map.listerThings.ThingsOfDef(ThingDefOf.Jade).Any(t=>!before.Contains(t)),"the scarab resists exactly 12 times, then turns into a jade stone");
   MWear(swordsman,"LP_PeriaptWoundClosure");var loss=HediffMaker.MakeHediff(HediffDefOf.BloodLoss,swordsman);swordsman.health.AddHediff(loss);loss.Severity=.99f;
   Check(!swordsman.Dead && Near(loss.Severity,MagicItems.BloodLossCap),"the periapt of wound closure stops blood loss at 90%");
   swordsman.health.RemoveHediff(loss);swordsman.apparel.DestroyAll();
   float carry=swordsman.GetStatValue(StatDefOf.CarryingCapacity);MWear(swordsman,"LP_GauntletsOgrePower");
   Check(Near(swordsman.GetStatValue(StatDefOf.CarryingCapacity)/carry,1.5f) || FullModList,"ogre power gauntlets multiply carrying capacity by 1.5");
   swordsman.apparel.DestroyAll();Check(!Items.Has(swordsman,"LP_MI_OgrePower"),"taking the gauntlets off removes their effect");
   // 11. 圣光冠环：每天5次；殉道者念珠吸收300。
   var halo=MWear(swordsman,"LP_HaloOfLight");int blocks=0;for(int i=0;i<8;i++)if(MagicEffects.HaloBlocks(swordsman,0f))blocks++;
   Check(blocks==5 && !MagicEffects.HaloBlocks(swordsman,0f) && !MagicEffects.HaloBlocks(swordsman,.5f),"the halo of light blocks at most 5 attacks a day");
   swordsman.apparel.DestroyAll();MWear(swordsman,"LP_MartyrsBeads");var hit=new DamageInfo(DamageDefOf.Cut,250f,0f,-1f,p);bool all=MagicEffects.MartyrAbsorb(swordsman,ref hit);var hit2=new DamageInfo(DamageDefOf.Cut,80f,0f,-1f,p);bool all2=MagicEffects.MartyrAbsorb(swordsman,ref hit2);
   Check(all && !all2 && Near(hit2.Amount,30f) && Items.Worn(swordsman,"LP_MartyrsBeads")==null && swordsman.inventory.innerContainer.Any(t=>t.def.defName=="LP_MartyrsBeadsBroken"),"martyr's beads absorb 300 damage, then break into the inventory");
   swordsman.inventory.innerContainer.ClearAndDestroyContents();
   // 12. 护佑誓约：甲戒受伤一半转给乙戒。
   var caseItem=MakeItem("LP_WardingBondCase");GenSpawn.Spawn(caseItem,p.Position,map);MagicEffects.Use("wardingcase",p,caseItem);
   var rings=map.listerThings.ThingsOfDef(D("LP_RingWardingBond")).ToList();var ra=rings.First(r=>r.TryGetComp<CompWardingBond>().primary);var rb=rings.First(r=>!r.TryGetComp<CompWardingBond>().primary);
   p.apparel.Wear((Apparel)ra,false);swordsman.apparel.Wear((Apparel)rb,false);var shared=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,victim);float moved=MagicEffects.WardingTransfer(p,ref shared);
   Check(rings.Count==2 && Near(moved,5f) && Near(shared.Amount,5f) && ra.TryGetComp<CompWardingBond>().partner==rb,"the warding bond case opens into a bound pair; half the damage moves to the partner");
   p.apparel.DestroyAll();swordsman.apparel.DestroyAll();
   // 13. 不可移动之杖、无尽水瓶。
   var rodCell=spot+IntVec3.South*3;var rod=GenSpawn.Spawn(MakeItem("LP_ImmovableRod"),rodCell,map);var pressed=ImmovableRod.Press(rod);
   Check(pressed!=null && pressed.def.passability==Traversability.Impassable && !pressed.def.useHitPoints && rodCell.GetEdifice(map)==pressed,"pressing the immovable rod turns it into an impassable, indestructible block");
   var back=ImmovableRod.Release(pressed);Check(back!=null && back.def.defName=="LP_ImmovableRod" && rodCell.GetEdifice(map)==null,"releasing the rod turns it back into an item");
   back.Destroy();
   var decCell=spot+IntVec3.East*4;var decanter=GenSpawn.Spawn(ThingMaker.MakeThing(D("LP_DecanterEndlessWater")),decCell,map);
   var fire=(Fire)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Fire),decCell+IntVec3.North*2,map);Decanters.Refresh();
   Check(decanter.TryGetComp<CompDecanter>().Spray()>=1 && fire.Destroyed && decanter.TryGetComp<CompDecanter>().Spray()==-1 && Decanters.Irrigated(map,decCell+IntVec3.East),"the decanter of endless water puts out nearby fires once a day and irrigates its surroundings");
   decanter.Destroy();
   // 14. 魔像手册：巫妖读完后仪式台解锁石魔像；石魔像能生成。
   var lich=ItemPawn(spot+IntVec3.North*4);lich.ChangePsylinkLevel(1,false);var box=Rituals.Ascend(lich);GenPlace.TryPlaceThing(box,altar.Position,map,ThingPlaceMode.Near);box.level=12;altar.selected=box;lich.psychicEntropy.OffsetPsyfocusDirectly(1f);
   var recipe=DefDatabase<RecipeDef>.GetNamed("LP_MakeStoneGolem");var manual=GenSpawn.Spawn(MakeItem("LP_ManualOfGolems"),lich.Position,map);var use=manual.TryGetComp<CompMagicUse>();
   Check(Rituals.Blocked(recipe,altar,lich)!=null && !use.CanBeUsedBy(p).Accepted && use.CanBeUsedBy(lich).Accepted,"only a lich can read the manual of golems; the stone golem recipe starts locked");
   use.DoEffect(lich);Check(Items.Has(lich,"LP_GolemLore") && manual.Destroyed && Rituals.Blocked(recipe,altar,lich)==null,"after reading the manual the ritual table unlocks the stone golem");
   var golem=Constructs.Create("StoneGolem",lich,CellFinder.RandomClosewalkCellNear(altar.Position,map,4),map);var gh=Necro.Construct(golem);
   Check(gh.kind=="StoneGolem" && gh.Cost==4 && golem.BodySize>=1.9f && golem.Name.ToStringShort=="石魔像" && golem.needs.food==null,"a stone golem awakens: size 2, control 4, no food need");
   float hp=golem.health.summaryHealth.SummaryHealthPercent;ItemThrowables.Dispel(map,golem.Position);Check(golem.health.summaryHealth.SummaryHealthPercent<hp,"dispel dust damages the stone golem");
   magicGolemId=golem.ThingID;magicLichId=lich.ThingID;
   // 15. 黑网货单30%诅咒品；Isekai 宝箱魔法物品。
   var gen=DefDatabase<TraderKindDef>.GetNamed("LP_Caravan_BlackNetwork").stockGenerators.OfType<StockGenerator_LPMagicGoods>().Single();int cursed=0,total=0;
   Rand.PushState(4507);try{for(int i=0;i<40;i++)foreach(var t in gen.GenerateThings(map.Tile,null)){total++;if(t.TryGetComp<CompCursed>()!=null)cursed++;t.Destroy();}}finally{Rand.PopState();}
   Check(total>0 && cursed>0 && cursed<total && Math.Abs(cursed/(float)total-.3f)<.15f,"the black network stock mixes in about 30% cursed goods: "+cursed+"/"+total);
   Check(IsekaiLoot.MagicDrop(2,()=>0f)==null && IsekaiLoot.MagicDrop(3,()=>0f)=="LP_VorpalSword" && IsekaiLoot.MagicDrop(4,()=>.5f)==null,"Isekai chests of tier 3+ roll a 15% magic item");
   // 16. 精金只算真正的护甲层。
   var ring=(Apparel)ThingMaker.MakeThing(D("LP_RingProtection"));var adamantPlate=(Apparel)ThingMaker.MakeThing(D("Apparel_PlateArmor"),ItemMaterials.Adamantine);
   Check(!ItemMaterials.ArmorLayer(ring) && ItemMaterials.ArmorLayer(adamantPlate),"adamantine armor counts only real armor layers, not accessories");
   ring.Destroy();adamantPlate.Destroy();
   foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !(t is Pawn) && !(t is Filth) && !(t is Mote) && t.def.category!=ThingCategory.Ethereal && t.def.defName!="LP_Phylactery").ToList())if(!t.Destroyed)t.Destroy();
   swordsman.Destroy();
  }
  void PrepareMagicItemsSave(){}
  void CheckMagicItemsAfterLoad(){
   var golem=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==magicGolemId);var gh=Necro.Construct(golem);
   Check(gh!=null && gh.kind=="StoneGolem" && gh.Cost==4 && gh.master?.ThingID==magicLichId,"stone golem survives save/load with its kind and master");
   var lich=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==magicLichId);
   Check(lich!=null && Items.Has(lich,"LP_GolemLore"),"the lich still knows golem lore after save/load");
   var cursedOwner=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.equipment?.Primary?.ThingID==magicCursedId);
   Check(cursedOwner==null || CursedItems.Comp(cursedOwner.equipment.Primary)?.Active==true,"cursed state survives save/load");
  }
 }
}
