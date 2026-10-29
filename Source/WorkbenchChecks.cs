using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // 1.47.0：Test.ps1 -Only Workbenches。DND工作台：定义与贴图、研究、配方归属表、附件与工具柜、巨人铁砧/法袍织机速度、卷轴配方、鉴定、旧存档清理（含存读档）。
 public partial class LichChecks {
  static string wbSmithyId;static int wbLettersBefore;
  public static readonly string[] WorkbenchDefs={"LP_EnchantingTable","LP_ScribeDesk","LP_RuneAnvil","LP_GiantAnvil","LP_ArcaneLoom"};
  public static readonly string[] WorkbenchFacilityDefs={"LP_Alembic","LP_ArcaneFocusStone","LP_RuneQuenchTrough","LP_DeityStatue"};
  static readonly string[] WbRuneOnly={"Make_LP_HammerCannon","Make_LP_LavaPiercer","Make_LP_MountainWrath","Make_LP_RunicMusket","Make_LP_RunicPistol","Make_LP_RunicRepeater","Make_LP_SilverPistol","Make_LP_ThunderGun",
   "Make_LP_DwarvenRuneShield","Make_LP_DwarvenThrowingHammer","Make_LP_ElvenChain","Make_LP_MinotaurHornHelm","Make_LP_GithAstralArmor","Make_LP_OrcishGreataxe","Make_LP_SilverSword","Make_LP_FrostSpear",
   "Make_LP_JavelinLightning","Make_LP_FrostBrand","Make_LP_DragonSlayer","Make_LP_DefenderSword",
   "Make_LP_PaladinPlate","Make_LP_GreatHelmOath","Make_LP_ChainShirt","Make_LP_DwarvenRuneplate","Make_LP_BeardedHelm","Make_LP_BattleClericBreastplate","Make_LP_DragonknightArmor","Make_LP_DragonHeadHelm","Make_LP_LoxodonWardenArmor","Make_LP_CelestialPanoply"};
  static readonly string[] WbGiantOnly={"LP_MakeFrostGiantAxe","Make_LP_FrostGiantAxe","Make_LP_StormTrident","Make_LP_AbyssCoralArmor","Make_LP_CloudMorningstar","LP_MakeCyclopsMaulSteel","LP_MakeCyclopsMaulWood","Make_LP_CyclopsMaul","Make_LP_EttinTwinHelm"};
  static readonly string[] WbRelics={"LP_MakePrayerBeads","LP_MakeScarabProtection","LP_MakeMartyrsBeads","LP_ReconsecrateMartyrsBeads","LP_MakePuritySeal","LP_MakeHaloOfLight","LP_MakeWardingBondCase"};
  static readonly string[] WbDesk={"LP_MakeScrollRemoveCurse","LP_MakeManualOfGolems","LP_WriteScrollTrueSeeing","LP_WriteScrollProtectionEvil","LP_WriteScrollFireball","LP_WriteScrollTeleport","LP_WriteScrollFleshToStone","LP_WriteScrollRevivify"};
  static readonly string[] WbLoomOnly={"Make_LP_ArchmageRobe","Make_LP_WizardHat","Make_LP_MorninglordVestments","Make_LP_LichShroud","Make_LP_CourtAttire","LP_MakeElementalWeave"};
  static readonly string[] WbLoomAndTailor={"Make_LP_WinterWolfCloak","Make_LP_CloudRobe","Make_LP_IllithidTendrilCloak","Make_LP_FeatherCloak","Make_LP_StabilizerBand"};
  static readonly string[] WbAlchemy={"LP_BrewPotionHealing","LP_BrewPotionGreaterHealing","LP_BrewPotionGiantStrength","LP_BrewPotionFireResistance","LP_BrewPotionHeroism","LP_BrewPotionMindShield","LP_BrewPotionTrollRegen","LP_BrewPotionFireResistanceEnhanced","LP_BrewPotionGreaterHealingEnhanced",
   "LP_MakeAlchemistFire","LP_MakeAcidVial","LP_MakeHolyWater","LP_MakeTanglefootBag","LP_MakeAcidVialGel","LP_MakeTanglefootBagGel","LP_MakeFogFlask","LP_MakeStormOrb","LP_MakeColdIron","LP_MakeGelDressing","Make_LP_PsionicSuppressant","Make_LP_PsionicSuppressantBulk"};
  static bool WbUsers(string recipe,params string[] users){var r=DefDatabase<RecipeDef>.GetNamedSilentFail(recipe);if(r==null)return false;var u=r.AllRecipeUsers.Select(x=>x.defName).Distinct().OrderBy(x=>x).ToList();return u.SequenceEqual(users.OrderBy(x=>x));}
  static string WbWhere(string recipe){var r=DefDatabase<RecipeDef>.GetNamedSilentFail(recipe);return recipe+"@"+(r==null?"missing":string.Join("+",r.AllRecipeUsers.Select(x=>x.defName).Distinct()));}
  Thing WbSpawn(string def,IntVec3 at,Rot4 rot){var d=DefDatabase<ThingDef>.GetNamed(def);var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.SetFactionDirect(Faction.OfPlayer);return GenSpawn.Spawn(t,at,map,rot);}

  void CheckWorkbenches(){
   Func<string,ThingDef> D=n=>DefDatabase<ThingDef>.GetNamedSilentFail(n);Func<string,ResearchProjectDef> R=n=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail(n);
   // 1. 定义、贴图（非正方形的 Graphic_Multi 要南/北/东三向）
   var bad=new List<string>();
   foreach(var n in WorkbenchDefs.Concat(WorkbenchFacilityDefs)){
    var d=D(n);if(d==null){bad.Add(n+" missing");continue;}
    if(d.ConfigErrors().Any())bad.Add(n+":"+string.Join(";",d.ConfigErrors()));
    if(d.graphicData.graphicClass==typeof(Graphic_Multi)){foreach(var dir in new[]{"_south","_north","_east"})if(ContentFinder<Texture2D>.Get(d.graphicData.texPath+dir,false)==null)bad.Add(n+dir);}
    else if(ContentFinder<Texture2D>.Get(d.graphicData.texPath,false)==null)bad.Add(n+" tex");
    if(d.uiIcon==null || d.uiIcon==BaseContent.BadTex)bad.Add(n+" icon");
    if(d.designationCategory?.defName!="Production")bad.Add(n+" not in Production");
   }
   Check(bad.Count==0,"5 workbenches and 4 facilities load with textures (all three views for multi-cell benches) in the Production menu: "+string.Join(",",bad));
   var size=new Dictionary<string,IntVec2>{{"LP_EnchantingTable",new IntVec2(3,2)},{"LP_ScribeDesk",new IntVec2(2,1)},{"LP_RuneAnvil",new IntVec2(3,2)},{"LP_GiantAnvil",new IntVec2(4,3)},{"LP_ArcaneLoom",new IntVec2(3,1)},{"LP_Alembic",new IntVec2(1,1)},{"LP_ArcaneFocusStone",new IntVec2(1,1)},{"LP_RuneQuenchTrough",new IntVec2(2,1)},{"LP_DeityStatue",new IntVec2(1,1)}};
   Check(size.All(kv=>D(kv.Key).size==kv.Value),"workbench and facility sizes match the spec");
   var gate=new Dictionary<string,string>{{"LP_EnchantingTable","LP_Research_Enchanting"},{"LP_ScribeDesk","LP_Research_Scribing"},{"LP_RuneAnvil","LP_Research_RuneSmithing"},{"LP_GiantAnvil","LP_GiantForging"},{"LP_ArcaneLoom","LP_Research_ArcaneTailoring"},{"LP_Alembic","LP_DNDAlchemy"},{"LP_ArcaneFocusStone","LP_Research_WondrousCraft"},{"LP_RuneQuenchTrough","LP_Research_RuneSmithing"},{"LP_DeityStatue","LP_Research_Faith"}};
   Check(gate.All(kv=>D(kv.Key).researchPrerequisites?.Count==1 && D(kv.Key).researchPrerequisites[0].defName==kv.Value),"each workbench and facility is unlocked by its planned research: "+string.Join(",",gate.Where(kv=>D(kv.Key).researchPrerequisites?.FirstOrDefault()?.defName!=kv.Value).Select(kv=>kv.Key)));
   Func<string,string> work=n=>DefDatabase<WorkGiverDef>.AllDefs.FirstOrDefault(w=>w.fixedBillGiverDefs!=null && w.fixedBillGiverDefs.Contains(D(n)))?.workType?.defName;
   Check(work("LP_EnchantingTable")=="Crafting" && work("LP_ScribeDesk")=="Research" && work("LP_RuneAnvil")=="Smithing" && work("LP_GiantAnvil")=="Smithing" && work("LP_ArcaneLoom")=="Tailoring","work types: enchanting=crafting, scribe desk=research, anvils=smithing, loom=tailoring");
   var anvilFuel=D("LP_RuneAnvil").GetCompProperties<CompProperties_Refuelable>();
   Check(anvilFuel!=null && anvilFuel.consumeFuelOnlyWhenUsed && Near(anvilFuel.fuelConsumptionRate,40f) && anvilFuel.fuelFilter.Allows(ThingDefOf.WoodLog) && anvilFuel.fuelFilter.Allows(ThingDefOf.Chemfuel),"rune anvil burns wood or chemfuel, 40/day, only while working");
   // 2. 研究
   var tab=DefDatabase<ResearchTabDef>.GetNamed(ResearchTab.Tab);
   var res=new[]{("LP_Research_RuneSmithing",0f,1.4f,500f),("LP_Research_Scribing",2f,1.4f,1000f),("LP_Research_HighScrolls",4f,1.4f,2500f)};
   Check(res.All(r=>{var d=R(r.Item1);return d!=null && d.tab==tab && Near(d.ResearchViewX,r.Item2) && Near(d.ResearchViewY,r.Item3) && Near(d.baseCost,r.Item4);}),"three new research projects sit in the DND tab at (0,1.4), (2,1.4), (4,1.4) with costs 500/1000/2500");
   Check(R("LP_Research_RuneSmithing").prerequisites.Select(p=>p.defName).SequenceEqual(new[]{"Smithing"}) && R("LP_Research_Scribing").prerequisites.Select(p=>p.defName).SequenceEqual(new[]{"LP_DNDAlchemy"}) && R("LP_Research_HighScrolls").prerequisites.Select(p=>p.defName).OrderBy(x=>x).SequenceEqual(new[]{"LP_Research_Scribing","LP_Research_WondrousCraft"}),"rune smithing needs smithing; scribing needs DND alchemy; high scrolls need scribing and wondrous craft");
   // 3. 配方归属表
   var wrong=new List<string>();
   foreach(var n in WbRuneOnly)if(!WbUsers(n,"LP_RuneAnvil"))wrong.Add(WbWhere(n));
   foreach(var n in new[]{"Make_LP_DragonShedArmor","Make_LP_EmpyreanBlade"})if(!WbUsers(n,"LP_RuneAnvil","LP_FireGiantForge"))wrong.Add(WbWhere(n));
   if(!WbUsers("Make_LP_ElvenLongbow","LP_RuneAnvil","CraftingSpot"))wrong.Add(WbWhere("Make_LP_ElvenLongbow"));
   foreach(var n in WbGiantOnly)if(DefDatabase<RecipeDef>.GetNamedSilentFail(n)!=null || !n.Contains("Cyclops") && !n.Contains("Ettin"))if(!WbUsers(n,"LP_GiantAnvil"))wrong.Add(WbWhere(n));
   if(!WbUsers("Make_LP_OgreClub","LP_GiantAnvil","CraftingSpot"))wrong.Add(WbWhere("Make_LP_OgreClub"));
   foreach(var n in WbRelics)if(!WbUsers(n,"LP_FaithAltar"))wrong.Add(WbWhere(n));
   foreach(var n in WbDesk)if(!WbUsers(n,"LP_ScribeDesk"))wrong.Add(WbWhere(n));
   if(DefDatabase<RecipeDef>.GetNamedSilentFail("LP_MakeAdventurersManual")!=null && !WbUsers("LP_MakeAdventurersManual","LP_ScribeDesk"))wrong.Add(WbWhere("LP_MakeAdventurersManual"));
   foreach(var n in WbLoomOnly)if(!WbUsers(n,"LP_ArcaneLoom"))wrong.Add(WbWhere(n));
   foreach(var n in WbLoomAndTailor)if(!WbUsers(n,"LP_ArcaneLoom","HandTailoringBench","ElectricTailoringBench"))wrong.Add(WbWhere(n));
   foreach(var n in new[]{"Make_LP_FireStaff","Make_LP_FrostStaff","Make_LP_MissileStaff","LP_MakeElementalFocus_Fire","LP_MakeElementalFocus_Water","LP_MakeElementalFocus_Earth","LP_MakeElementalFocus_Air"})if(!WbUsers(n,"LP_EnchantingTable"))wrong.Add(WbWhere(n));
   var magic=DefDatabase<RecipeDef>.AllDefs.Where(r=>r.fileName=="MagicItems_Recipes.xml" && !WbRelics.Contains(r.defName) && !WbDesk.Contains(r.defName)).ToList();
   foreach(var r in magic)if(!WbUsers(r.defName,"LP_EnchantingTable"))wrong.Add(WbWhere(r.defName));
   foreach(var r in DefDatabase<RecipeDef>.AllDefs.Where(r=>r.defName.StartsWith("LP_EnchantJewelry_")))if(!WbUsers(r.defName,"LP_EnchantingTable"))wrong.Add(WbWhere(r.defName));
   foreach(var n in WbAlchemy.Where(n=>!n.Contains("Psionic")))if(!WbUsers(n,"LP_AlchemyTable"))wrong.Add(WbWhere(n));
   foreach(var n in new[]{"Make_LP_PsionicSuppressant","Make_LP_PsionicSuppressantBulk"})if(!WbUsers(n,"DrugLab","LP_AlchemyTable"))wrong.Add(WbWhere(n));
   Check(wrong.Count==0 && magic.Count==22,"recipe ownership table: "+magic.Count+" magic recipes at the enchanting table, relics at the altar, scrolls at the desk, robes at the loom, rune/giant gear at the anvils: "+string.Join(",",wrong));
   var onSmithy=DefDatabase<RecipeDef>.AllDefs.Where(r=>OwnDef(r) && r.AllRecipeUsers.Any(u=>u.defName=="FueledSmithy" || u.defName=="ElectricSmithy")).Select(r=>r.defName).ToList();
   Check(onSmithy.Count==0,"no mod recipe is left on the vanilla smithies: "+string.Join(",",onSmithy));
   var onAlchemy=D("LP_AlchemyTable").AllRecipes.Where(OwnDef).Select(r=>r.defName).OrderBy(x=>x).ToList();
   Check(onAlchemy.SequenceEqual(WbAlchemy.OrderBy(x=>x)),"the alchemy table keeps exactly its "+WbAlchemy.Length+" alchemy recipes: "+onAlchemy.Count+" "+string.Join(",",onAlchemy.Except(WbAlchemy)));
   var onTailor=D("HandTailoringBench").AllRecipes.Where(OwnDef).Select(r=>r.defName).ToList();
   Check(!onTailor.Intersect(WbLoomOnly).Any() && WbLoomAndTailor.All(onTailor.Contains),"robes left the vanilla tailoring bench, racial cloaks stay there too");
   Check(D("LP_FireGiantForge").AllRecipes.Any(r=>r.defName=="Make_LP_FlameTongue") && D("LP_Altar").AllRecipes.Any(r=>r.defName=="LP_ScribeRemoveCurse") && D("LP_Altar").AllRecipes.Any(r=>r.defName=="LP_ForgeDeathknightPlate"),"molten forge and necro altar keep their own recipes (flame tongue, lich scroll, death knight plate)");
   // 4. 附件、工具柜
   var cabinet=D("ToolCabinet").GetCompProperties<CompProperties_Facility>();
   Check(WorkbenchDefs.All(n=>cabinet.linkableBuildings.Contains(D(n))),"the vanilla tool cabinet links to all five new workbenches");
   var links=new Dictionary<string,string[]>{{"LP_Alembic",new[]{"LP_AlchemyTable"}},{"LP_ArcaneFocusStone",new[]{"LP_EnchantingTable","LP_ScribeDesk"}},{"LP_RuneQuenchTrough",new[]{"LP_RuneAnvil","LP_GiantAnvil"}},{"LP_DeityStatue",new[]{"LP_FaithAltar"}}};
   var off=new Dictionary<string,float>{{"LP_Alembic",.15f},{"LP_ArcaneFocusStone",.15f},{"LP_RuneQuenchTrough",.10f},{"LP_DeityStatue",.10f}};
   Check(links.All(kv=>{var f=D(kv.Key).GetCompProperties<CompProperties_Facility>();return f!=null && f.maxSimultaneous==1 && Near(f.statOffsets.GetStatOffsetFromList(StatDefOf.WorkTableWorkSpeedFactor),off[kv.Key]) && kv.Value.All(b=>f.linkableBuildings.Contains(D(b))) && f.linkableBuildings.Count==kv.Value.Length;}),"facilities link to their benches (one each) with +15%/+15%/+10%/+10% work speed");
   // 实地：炼金桌 + 蒸馏器，圣坛 + 守护神像
   var spot=ItemSpot(5);var spawned=new List<Thing>();
   var table=WbSpawn("LP_AlchemyTable",spot,Rot4.South);spawned.Add(table);
   float before=table.GetStatValue(StatDefOf.WorkTableWorkSpeedFactor);
   var alembic=WbSpawn("LP_Alembic",spot+new IntVec3(0,0,2),Rot4.South);spawned.Add(alembic);
   float after=table.GetStatValue(StatDefOf.WorkTableWorkSpeedFactor);
   Check(table.TryGetComp<CompAffectedByFacilities>().LinkedFacilitiesListForReading.Contains(alembic) && after>before*1.05f,"an alembic next to the alchemy table links and raises its work speed: "+before.ToString("0.###")+" -> "+after.ToString("0.###"));
   var altarF=WbSpawn("LP_FaithAltar",spot+new IntVec3(3,0,0),Rot4.South);spawned.Add(altarF);
   Check(Near(Workbenches.PrayJoyFactor(altarF),1f),"an altar without a statue gives normal prayer joy");
   var statue=WbSpawn("LP_DeityStatue",spot+new IntVec3(3,0,2),Rot4.South);spawned.Add(statue);
   Check(Workbenches.HasStatue(altarF) && Near(Workbenches.PrayJoyFactor(altarF),1.2f),"a deity statue next to the altar links and gives prayer joy x1.2");
   foreach(var t in spawned)if(!t.Destroyed)t.Destroy();spawned.Clear();
   // 5. 巨人铁砧 / 法袍织机速度
   var giant=RacePawn("LP_FireGiant");var human=RacePawn("Baseliner");
   var axe=DefDatabase<RecipeDef>.GetNamed("LP_MakeFrostGiantAxe");
   Check(giant.BodySize>=Workbenches.GiantBodySize && human.BodySize<Workbenches.GiantBodySize,"test pawns: fire giant body size "+giant.BodySize+", human "+human.BodySize);
   var gspot=ItemSpot(4);var anvil=(Building_WorkTable)WbSpawn("LP_GiantAnvil",gspot,Rot4.South);spawned.Add(anvil);
   Func<Pawn,float> atAnvil=p=>{Place(p,anvil.InteractionCell);var bill=axe.MakeNewBill();anvil.BillStack.AddBill(bill);var job=JobMaker.MakeJob(JobDefOf.DoBill,anvil);job.bill=bill;var old=p.jobs.curJob;p.jobs.curJob=job;try{return anvil.GetStatValue(StatDefOf.WorkTableWorkSpeedFactor);}finally{p.jobs.curJob=old;anvil.BillStack.Delete(bill);}};
   float vg=atAnvil(giant);Place(giant,gspot+new IntVec3(0,0,5));float vh=atAnvil(human);
   Check(Near(vg/vh,1.25f) && Near(Workbenches.WorkerFactor(anvil,giant,axe),1.25f) && Near(Workbenches.WorkerFactor(anvil,human,axe),1f),"a body-size "+giant.BodySize+" giant works 25% faster than a human at the giant anvil: "+vg.ToString("0.###")+" vs "+vh.ToString("0.###"));
   var loom=WbSpawn("LP_ArcaneLoom",gspot+new IntVec3(0,0,4),Rot4.South);spawned.Add(loom);
   var cape=DefDatabase<RecipeDef>.GetNamed("Make_LP_WinterWolfCloak");var robe=DefDatabase<RecipeDef>.GetNamed("Make_LP_ArchmageRobe");
   Check(Near(Workbenches.WorkerFactor(loom,human,cape),1.15f) && Near(Workbenches.WorkerFactor(loom,human,robe),1f) && Near(Workbenches.WorkerFactor(anvil,human,cape),1f),"racial cloaks are 15% faster on the loom; robes are not");
   // 熔火锻炉仍只许火巨人
   var forge=WbSpawn("LP_FireGiantForge",gspot+new IntVec3(0,0,-4),Rot4.South);spawned.Add(forge);
   var forgeWg=new WorkGiver_DoBill{def=DefDatabase<WorkGiverDef>.GetNamed("LP_DoFireGiantForge")};
   Check(forgeWg.JobOnThing(human,forge,true)==null,"the molten forge still refuses non-fire-giants");
   foreach(var t in spawned)if(!t.Destroyed)t.Destroy();spawned.Clear();
   giant.Destroy();human.Destroy();
   // 6. 新卷轴配方
   var scrolls=new[]{("LP_WriteScrollTrueSeeing","LP_ScrollTrueSeeing","LP_Research_Scribing",6,1800f),("LP_WriteScrollProtectionEvil","LP_ScrollProtectionEvil","LP_Research_Scribing",6,1800f),("LP_WriteScrollFireball","LP_ScrollFireball","LP_Research_Scribing",8,2200f),
    ("LP_WriteScrollTeleport","LP_ScrollTeleport","LP_Research_HighScrolls",10,3000f),("LP_WriteScrollFleshToStone","LP_ScrollFleshToStone","LP_Research_HighScrolls",12,3600f),("LP_WriteScrollRevivify","LP_ScrollRevivify","LP_Research_HighScrolls",14,6000f)};
   var badScroll=new List<string>();var ratios=new List<string>();
   foreach(var s in scrolls){
    var r=DefDatabase<RecipeDef>.GetNamedSilentFail(s.Item1);
    if(r==null || r.products.Count!=1 || r.products[0].thingDef.defName!=s.Item2 || r.products[0].count!=1 || r.researchPrerequisite?.defName!=s.Item3 || r.workSkill!=SkillDefOf.Intellectual || r.skillRequirements?.FirstOrDefault(q=>q.skill==SkillDefOf.Intellectual)?.minLevel!=s.Item4 || !Near(r.workAmount,s.Item5)){badScroll.Add(s.Item1);continue;}
    float cost=r.ingredients.Sum(i=>i.GetBaseCount()*(i.IsFixedIngredient?i.FixedIngredient.BaseMarketValue:i.filter.AllowedThingDefs.Min(t=>t.BaseMarketValue)));
    ratios.Add(s.Item2.Substring(9)+" "+(cost/r.products[0].thingDef.BaseMarketValue).ToStringPercent());
   }
   Check(badScroll.Count==0,"six new scroll recipes make the right scroll with the right research, intellect level and work: "+string.Join(",",badScroll)+" | material/value "+string.Join(", ",ratios));
   // 7. 鉴定：研究台和卷轴书桌都行，书桌快一半
   var desk=(Building)WbSpawn("LP_ScribeDesk",ItemSpot(3),Rot4.South);
   Check(Workbenches.CanIdentifyAt(desk) && Workbenches.IdentifyTicks(desk)==Mathf.RoundToInt(CursedItems.IdentifyTicks/1.5f) && Workbenches.IdentifyTicks(null)==CursedItems.IdentifyTicks && CursedItems.Bench(null)==null,"items can be identified at the scribe desk in "+Workbenches.IdentifyTicks(desk)+" ticks (research bench "+CursedItems.IdentifyTicks+")");
   desk.Destroy();
   // 8. 器械师门槛只看配方：符文手铳搬到符文铁砧后照旧（VPE 组里另有实测）
   Check(DefDatabase<RecipeDef>.GetNamed("Make_LP_RunicPistol").ProducedThingDef?.defName=="LP_RunicPistol","runic pistol recipe still produces the runic pistol (artificer gate keys on the product, not the bench)");
   // 9. 旧存档：读档时孤儿工作单判定
   var smithy=(Building_WorkTable)WbSpawn("FueledSmithy",ItemSpot(3),Rot4.South);
   var pistol=DefDatabase<RecipeDef>.GetNamed("Make_LP_RunicPistol");var orphan=pistol.MakeNewBill();smithy.BillStack.AddBill(orphan);
   var vanillaBill=smithy.def.AllRecipes.First(r=>!OwnDef(r)).MakeNewBill();smithy.BillStack.AddBill(vanillaBill);
   Check(Workbenches.Orphan(smithy,orphan) && !Workbenches.Orphan(smithy,vanillaBill) && Workbenches.NewHome(pistol)?.defName=="LP_RuneAnvil","a runic pistol bill on the vanilla smithy counts as orphaned and its new home is the rune anvil; vanilla bills are kept");
   wbSmithyId=smithy.ThingID;
  }
  void PrepareWorkbenchesSave(){
   // 旧存档模拟：组件版本回到 0，原版锻造台上挂着符文手铳单子，异族军械和附魔学已完成、新研究未完成。
   var comp=Current.Game.GetComponent<WorkbenchMigration>();comp.version=0;
   var rm=Find.ResearchManager;Func<string,ResearchProjectDef> R=n=>DefDatabase<ResearchProjectDef>.GetNamed(n);
   foreach(var n in new[]{"LP_Research_RuneSmithing","LP_Research_Scribing","LP_Research_HighScrolls"})if(R(n).IsFinished)rm.ResetAllProgress();
   rm.FinishProject(R("LP_RacialArmory"),false,null,false);rm.FinishProject(R("LP_Research_Enchanting"),false,null,false);
   wbLettersBefore=WorkbenchMigration.lettersSent;
  }
  void CheckWorkbenchesAfterLoad(){
   var comp=Current.Game.GetComponent<WorkbenchMigration>();
   var smithy=map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().FirstOrDefault(b=>b.ThingID==wbSmithyId);
   Check(smithy!=null && !smithy.BillStack.Bills.Any(b=>OwnDef(b.recipe)) && smithy.BillStack.Bills.Count==1,"after loading an old save the runic pistol bill is gone from the vanilla smithy, the vanilla bill stays");
   var letters=Find.LetterStack.LettersListForReading.Where(l=>l.Label.ToString()==Workbenches.LetterLabel).ToList();
   Check(letters.Count==1 && WorkbenchMigration.lettersSent==wbLettersBefore+1 && letters[0] is ChoiceLetter cl && cl.Text.ToString().Contains("符文铁砧") && cl.Text.ToString().Contains(DefDatabase<ThingDef>.GetNamed("LP_RunicPistol").label),"one letter lists the cleared runic pistol bill under the rune anvil");
   Func<string,bool> fin=n=>DefDatabase<ResearchProjectDef>.GetNamed(n).IsFinished;
   Check(comp.version==1 && fin("LP_Research_RuneSmithing") && fin("LP_Research_Scribing") && !fin("LP_Research_HighScrolls"),"racial armory completes rune smithing, enchanting completes scribing, high scrolls stay locked");
   comp.LoadedGame();
   Check(WorkbenchMigration.lettersSent==wbLettersBefore+1 && Find.LetterStack.LettersListForReading.Count(l=>l.Label.ToString()==Workbenches.LetterLabel)==1,"the migration letter is sent only once");
  }
 }
}
