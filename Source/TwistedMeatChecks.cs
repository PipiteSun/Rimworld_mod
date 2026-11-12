using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.119.0 扭曲肉资源化：Test.ps1 -Anomaly -Only TwistedMeat（25 条检查，含存读档）。
 public partial class LichChecks {
  static int tmDissected=-1,tmVestId=-1,tmSeedId=-1;static string tmCycleKey;
  IntVec3 TMCell(int minDist){
   var a=altar.Position;
   return map.AllCells.Where(c=>c.x>10 && c.z>10 && c.x<map.Size.x-10 && c.z<map.Size.z-10 && c.DistanceTo(a)>=minDist && CellRect.CenteredOn(c,2).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth))).OrderBy(c=>c.DistanceTo(a)).First();
  }
  Thing TMSpawn(string def,int n,IntVec3 cell){var t=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(def));t.stackCount=n;GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);t.SetForbidden(false,false);return t;}
  Pawn TMPawn(IntVec3 cell){var p=Person();GenSpawn.Spawn(p,CellFinder.StandableCellNear(cell,map,3),map);return p;}
  Pawn TMLich(){
   var p=Person();for(int i=0;i<20 && p.GetStatValue(StatDefOf.PsychicSensitivity)<=0;i++){p.Destroy();p=Person();}
   GenSpawn.Spawn(p,altar.InteractionCell,map);p.workSettings.EnableAndInitialize();p.workSettings.SetPriority(WorkTypeDefOf.Crafting,1);p.skills.GetSkill(SkillDefOf.Crafting).Level=12;
   for(int h=0;h<24;h++)p.timetable.SetAssignment(h,TimeAssignmentDefOf.Work);
   var box=Rituals.Ascend(p);GenPlace.TryPlaceThing(box,altar.Position,map,ThingPlaceMode.Near);altar.selected=box;return p;
  }
  static void TMFinish(string name){var d=DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);if(d!=null && !d.IsFinished)Find.ResearchManager.FinishProject(d,false,null,false);}
  static HediffDef TMH(string n)=>DefDatabase<HediffDef>.GetNamed(n);

  void CheckTwistedMeat(){
   TwistedMeat.TestSuppressed=true;var c=TwistedMeat.Comp;var meat=TwistedMeat.Meat;var paste=TwistedMeat.Paste;
   // 1 桥接加载
   bool things=new[]{"LP_AberrantPaste","LP_AberrantSalve","LP_MorphDraught","LP_EyeOfHadar","LP_FleshIncubator","LP_FleshWall","LP_AberrantHideArmor"}.All(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)!=null);
   bool recs=new[]{"LP_StabilizeRot","LP_BrewTrollRegenAberrant","LP_BrewAberrantSalve","LP_BrewMorphDraught","LP_FarDissection","LP_ExtractBioferrite","LP_TemperColdIronBio","LP_Offer_Twisted","LP_MakeAberrantGolem","LP_RemoveAberrantTumor"}.All(n=>DefDatabase<RecipeDef>.GetNamedSilentFail(n)!=null);
   var rd=DefDatabase<PsychicRitualDef>.GetNamedSilentFail("LP_FarRealmFeast") as PsychicRitualDef_FarRealmFeast;
   Check(TwistedMeat.Active && c!=null && things && recs && rd!=null && rd.RequiredOffering?.GetBaseCount()==150 && rd.cooldownHours==240 && DefDatabase<TraderKindDef>.GetNamedSilentFail("LP_HagMarketTrader")!=null && DefDatabase<JobDef>.GetNamedSilentFail("LP_FleshCatalysis")!=null,"twisted meat: Anomaly bridge loads items, 11 recipes, the far-realm feast ritual (150 meat, 10 days), hag market and catalysis job");
   // 2 A 节折算
   var offer=DefDatabase<RecipeDef>.GetNamed("LP_Offer");var refill=DefDatabase<RecipeDef>.GetNamed("LP_Refill");var golem=DefDatabase<RecipeDef>.GetNamed("LP_MakeGolem");var craft=DefDatabase<RecipeDef>.GetNamed("LP_CraftBlindSight");
   var craftMeat=craft.ingredients.First(i=>i.filter.Allows(ThingDefOf.Meat_Human));
   bool getter=new[]{offer,refill,golem,craft}.All(r=>r.IngredientValueGetter is FleshCount);
   int rm=refill.ingredients[0].CountRequiredOfFor(meat,refill),rp=refill.ingredients[0].CountRequiredOfFor(paste,refill),cm=craftMeat.CountRequiredOfFor(meat,craft),ch=craftMeat.CountRequiredOfFor(ThingDefOf.Meat_Human,craft);
   bool gold=craft.ingredients.First(i=>i.filter.Allows(ThingDefOf.Gold)).CountRequiredOfFor(ThingDefOf.Gold,craft)==40;
   Check(getter && rm==50 && rp==5 && cm==200 && ch==400 && gold && offer.fixedIngredientFilter.Allows(paste) && craftMeat.filter.Allows(paste) && offer.description.Contains("扭曲肉 1 份算 2 份生肉"),"twisted meat: altar / flesh implant recipes count twisted meat ×2 and paste ×20 (refill 50 / 5, blind sight 200 / 400 human, gold unchanged), paste allowed, description says so: "+rm+"/"+rp+"/"+cm+"/"+ch);
   // 3-5 献祭生肉用畸变膏（原版工作单流程）
   worker=TMLich();var box=altar.Box;box.level=1;box.experience=0f;int oldCap=LichMod.Settings.levelCap;if(LichMod.Settings.levelCap<5)LichMod.Settings.levelCap=5;
   Spawn("LP_AberrantPaste",5);Execute("Offer"); // 2 条
   Check(box.level>1 || Near(box.experience,100f),"twisted meat: offering 5 aberrant paste at the altar feeds 100 experience (level "+box.level+", xp "+box.experience+")");
   // 6-8 畸变巨像胚体
   // 6 畸变巨像胚体（直接走原版产物流程；Execute 会自动唤醒胚体并多出一串造物检查）
   TMFinish("LP_Research_FleshShaping");box.level=10;var gr=DefDatabase<RecipeDef>.GetNamed("LP_MakeAberrantGolem");var gIng=new List<Thing>{TMSpawn("Meat_Twisted",200,altar.InteractionCell),TMSpawn("LP_AberrantPaste",10,altar.InteractionCell)};
   var seed=GenRecipe.MakeRecipeProducts(gr,worker,gIng,null,altar).OfType<ConstructSeed>().FirstOrDefault();bool marked=AberrantGolem.Marked(seed);string insp="";Pawn awake=null;string why=null;
   if(seed!=null){GenPlace.TryPlaceThing(seed,altar.InteractionCell,map,ThingPlaceMode.Near);insp=seed.GetInspectString();seed.Awaken(new ConstructDesign(),out awake,out why);}
   Check(seed!=null && marked && seed.kind=="Golem" && seed.master==worker && insp.Contains("畸变巨像") && awake!=null && awake.health.hediffSet.HasHediff(TMH("LP_AberrantGolem")),"twisted meat: the aberrant golem recipe makes a marked golem seed that awakens with 畸变血肉 ("+why+")");
   var seed2=GenRecipe.MakeRecipeProducts(gr,worker,new List<Thing>(),null,altar).OfType<ConstructSeed>().FirstOrDefault();if(seed2!=null){GenPlace.TryPlaceThing(seed2,altar.InteractionCell,map,ThingPlaceMode.Near);tmSeedId=seed2.thingIDNumber;}
   LichMod.Settings.levelCap=oldCap;
   // 9 B / C 配方
   R("LP_StabilizeRot",out var stab);R("LP_BrewTrollRegenAberrant",out var troll);
   Check(stab.products[0].thingDef==paste && stab.products[0].count==2 && stab.ingredients.Any(i=>i.filter.Allows(meat) && i.GetBaseCount()==20) && stab.AllRecipeUsers.Any(u=>u.defName=="LP_AlchemyTable") && paste.GetCompProperties<CompProperties_Rottable>()==null && paste.stackLimit==50 && Near(paste.BaseMarketValue,12f) && troll.products[0].thingDef.defName=="LP_PotionTrollRegen","twisted meat: 20 meat + 2 chemfuel → 2 paste at the alchemy table; paste never rots, stacks 50, 12 silver; troll regen and frenzy flask recipes");
   // 10 畸变再生膏
   var cellA=TMCell(16);var doc=TMPawn(cellA);var pat=TMPawn(cellA);var torso=pat.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso);
   var cut=HediffMaker.MakeHediff(HediffDefOf.Cut,pat,torso);cut.Severity=4f;pat.health.AddHediff(cut,torso);float b0=pat.GetStatValue(StatDefOf.PawnBeauty);
   var salve=(Medicine)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_AberrantSalve"));salve.stackCount=2;GenSpawn.Spawn(salve,cellA,map);
   AberrantSalveTumor.ForceTumor=true;try{TendUtility.DoTend(doc,pat,salve);}finally{AberrantSalveTumor.ForceTumor=false;}
   var rem=DefDatabase<RecipeDef>.GetNamed("LP_RemoveAberrantTumor");bool tumor=pat.health.hediffSet.hediffs.Any(h=>h.def.defName=="LP_AberrantTumor" && h.Part==torso);
   Check(tumor && cut.IsTended() && Near(pat.GetStatValue(StatDefOf.PawnBeauty)-b0,-1f) && ThingDefOf.Human.AllRecipes.Contains(rem) && rem.Worker.GetPartsToApplyOn(pat,rem).Contains(torso) && Near(salve.def.GetStatValueAbstract(StatDefOf.MedicalPotency),1f),"twisted meat: aberrant salve tends like medicine and can leave a removable tumor (beauty -1) on the tended part");
   // 11 蜕形药剂
   var mp=TMPawn(cellA);float md0=mp.GetStatValue(StatDefOf.MeleeDamageFactor),si0=mp.GetStatValue(StatDefOf.SocialImpact);mp.health.AddHediff(TMH("LP_MorphDraught"));
   var mh=mp.health.hediffSet.GetFirstHediffOfDef(TMH("LP_MorphDraught"));
   Check(Near(mp.GetStatValue(StatDefOf.MeleeDamageFactor)-md0,.2f) && mp.GetStatValue(StatDefOf.SocialImpact)-si0<-.2f && Near(mh.PainFactor,.8333f) && mh.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear==30000,"twisted meat: morph draught gives +20% melee damage, pain ×0.83 (pain threshold +20%), -30% social impact for 12 hours: "+(mp.GetStatValue(StatDefOf.MeleeDamageFactor)-md0)+"/"+(mp.GetStatValue(StatDefOf.SocialImpact)-si0)+"/"+mh.PainFactor+"/"+mh.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear);
   // 12 狂乱之瓶
   var cellB=TMCell(26);var f1=TMPawn(cellB);var f2=TMPawn(cellB);var fs=TMPawn(cellB);fs.health.AddHediff(TMH("LP_PotionMindShield"));
   int fled=Projectile_FrenzyFlask.Frenzy(map,cellB,null,1f);bool ok12=f1.MentalStateDef==MentalStateDefOf.PanicFlee && f1.MentalState.forceRecoverAfterTicks==480 && f2.InMentalState && !fs.InMentalState;
   foreach(var p in new[]{f1,f2}.Where(x=>x.InMentalState))p.MentalState.RecoverFromState();
   Check(ok12 && fled>=2,"twisted meat: frenzy flask makes creatures within 3 cells flee for 8 seconds; mind shield potion protects ("+fled+")");
   // 13 血肉再生词条 + 畸变皮甲
   var vest=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_FlakVest"));int pasteBefore=GearEnhanceBench.CountOnMap(map,paste);Spawn("LP_AberrantPaste",5);
   bool applied=FleshRegen.Apply(vest,map);tmVestId=vest.thingIDNumber;var wearer=TMPawn(cellA);float wb0=wearer.GetStatValue(StatDefOf.PawnBeauty);wearer.apparel.Wear(vest,false);
   float wb1=wearer.GetStatValue(StatDefOf.PawnBeauty);var cut2=HediffMaker.MakeHediff(HediffDefOf.Cut,wearer,wearer.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso));cut2.Severity=3f;wearer.health.AddHediff(cut2);
   FleshRegen.HealOne(wearer);bool healed=!wearer.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(h=>h.Severity>.001f);
   var hide=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_AberrantHideArmor"));var hideWearer=TMPawn(cellA);hideWearer.apparel.Wear(hide,false);
   bool social=DefDatabase<ThoughtDef>.GetNamed("LP_AberrantHideSocial").Worker.CurrentSocialState(wearer,hideWearer).Active;
   Check(applied && FleshRegen.Marked(vest) && GearEnhanceBench.CountOnMap(map,paste)==pasteBefore && Near(wb1-wb0,-1f) && healed && social && FleshRegen.HealsDaily(hide) && hide.GetStatValue(StatDefOf.ArmorRating_Sharp)<vest.GetStatValue(StatDefOf.ArmorRating_Sharp),"twisted meat: 血肉再生 costs 5 paste, gives beauty -1 and heals a small wound; aberrant hide armor heals daily, others -5 opinion, sharp armor below flak vest: "+applied+FleshRegen.Marked(vest)+"/"+GearEnhanceBench.CountOnMap(map,paste)+"vs"+pasteBefore+"/"+(wb1-wb0)+"/"+healed+social+FleshRegen.HealsDaily(hide)+"/"+hide.GetStatValue(StatDefOf.ArmorRating_Sharp)+"<"+vest.GetStatValue(StatDefOf.ArmorRating_Sharp));
   // 14 生物铁每日上限
   c.daily.Clear();var bio=DefDatabase<RecipeDef>.GetNamed("LP_ExtractBioferrite");bool a0=bio.Worker.AvailableOnNow(altar);c.AddToday("bioferrite",30);bool a1=bio.Worker.AvailableOnNow(altar);
   Check(a0 && !a1 && bio.products[0].thingDef.defName=="Bioferrite" && bio.products[0].count==10 && bio.AllRecipeUsers.Any(u=>u.defName=="LP_InfernalForge") && DefDatabase<RecipeDef>.GetNamed("LP_TemperColdIronBio").products[0].thingDef.defName=="LP_ColdIron","twisted meat: bioferrite extraction at the infernal forge stops after 30 per day; bioferrite + steel → cold iron");
   // 15 凯兰沃净化焚化（原版顺序：先 ConsumeIngredient 再 Notify）
   var kel=Faith.God("Kelemvor");var oldPatron=FaithUtility.Patron;FaithUtility.SetPatron(kel,true);float fav0=FaithUtility.GetFavor(kel);
   var ofr=DefDatabase<RecipeDef>.GetNamed("LP_Offer_Twisted");var ow=(RecipeWorker_TwistedOffering)ofr.Worker;var om=TMSpawn("Meat_Twisted",30,cellA);ow.ConsumeIngredient(om,ofr,map);ow.Notify_IterationCompleted(null,new List<Thing>{om});
   float fav1=FaithUtility.GetFavor(kel);int more=RecipeWorker_TwistedOffering.Grant(null,5);int none=RecipeWorker_TwistedOffering.Grant(null,5);
   FaithUtility.SetPatron(Faith.God("Mystra"),true);bool mystra=ow.AvailableOnNow(altar);
   Check(Near(fav1-fav0,Faith.GainFactor) && more==2 && none==0 && !mystra && om.Destroyed,"twisted meat: burning 30 twisted meat gives Kelemvor +1 favor (counted at consume time), daily cap +3, other gods refuse");
   // 16 远域解剖 + 异怪学 + 研究门槛
   c.dissected=0f;var dis=DefDatabase<RecipeDef>.GetNamed("LP_FarDissection");var dw=(RecipeWorker_FarDissection)dis.Worker;
   var d1=TMSpawn("Meat_Twisted",50,cellA);dw.ConsumeIngredient(d1,dis,map);dw.Notify_IterationCompleted(null,new List<Thing>{d1});bool first=Near(c.dissected,50f) && !RecipeWorker_FarDissection.Aberrology.IsFinished;
   c.dissected=460f;var d2=TMSpawn("Meat_Twisted",50,cellA);dw.ConsumeIngredient(d2,dis,map);dw.Notify_IterationCompleted(null,new List<Thing>{d2});tmDissected=Mathf.RoundToInt(c.dissected);
   var entity=PawnGenerator.GeneratePawn(new PawnGenerationRequest(FleshWorks.MinionKind,Faction.OfEntities,forceGenerateNewPawn:true));float dmg=AberrologyDamage.Factor(doc,entity);float dmgHuman=AberrologyDamage.Factor(doc,pat);entity.Destroy();
   var fsr=DefDatabase<ResearchProjectDef>.GetNamed("LP_Research_FleshShaping");var abr=RecipeWorker_FarDissection.Aberrology;
   Check(first && abr.IsFinished && Near(dmg,1.1f) && Near(dmgHuman,1f) && !abr.CanStartNow,"twisted meat: dissecting 500 twisted meat completes 异怪学 (+10% damage to anomaly entities only), which cannot be researched directly");
   // 17 食尸鬼
   var gh=TMPawn(cellA);MutantUtility.SetPawnAsMutantInstantly(gh,MutantDefOf.Ghoul);var gm=TMSpawn("Meat_Twisted",10,gh.Position);
   float got=gm.Ingested(gh,.1f);var sat=gh.health.hediffSet.GetFirstHediffOfDef(TMH("LP_FarRealmSated"));
   Check(gh.IsGhoul && Near(got,.3f) && sat!=null && sat.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear==15000,"twisted meat: a ghoul eating twisted meat gets ×3 nutrition ("+got+") and 远域饱食 for 6 hours");
   // 18 远域献飨结算
   var cellC=TMCell(36);
   string k1=PsychicRitualDef_FarRealmFeast.Resolve(1f,map,cellC,null,.5f,.1f),k2=PsychicRitualDef_FarRealmFeast.Resolve(.7f,map,cellC,null,.5f,.9f),k3=PsychicRitualDef_FarRealmFeast.Resolve(.3f,map,cellC,null,.9f,.9f),k4=PsychicRitualDef_FarRealmFeast.Resolve(0f,map,cellC,null,.1f,.9f);
   var drone=map.gameConditionManager.GetActiveCondition(GameConditionDefOf.PsychicDrone);bool droneOn=drone!=null;drone?.End();
   Check(k1=="eye" && k2=="adamantine" && k3=="mithral" && k4=="fail-wail" && droneOn && PsychicRitualDef_FarRealmFeast.Adamantine(.7f)==11 && PsychicRitualDef_FarRealmFeast.Mithral(0f)==15 && Near(PsychicRitualDef_FarRealmFeast.FailChance(.6f),.1f),"twisted meat: far-realm feast pays mithral / adamantine / Eye of Hadar by quality and fails at (1 − quality) × 25% (psychic wail)");
   // 19 血肉催转：价格递增、60% 上限、至少剩 1 天
   var cellD=TMCell(46);var spot=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("PsychicRitualSpot"));spot.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(spot,cellD,map);
   var mgr=CatalysisSource_Psychic.Manager;mgr.ClearCooldown(rd);mgr.RegisterCooldown(rd);var src=FleshCatalysis.Find(spot,"psy:LP_FarRealmFeast");tmCycleKey=src.key;
   int day=GenDate.TicksPerDay,rem0=src.Remaining,max0=FleshCatalysis.MaxDays(src),cost3=FleshCatalysis.Cost(src,3,out int t3);
   TMSpawn("Meat_Twisted",100,cellD);var cp=TMPawn(cellD);int red1=FleshCatalysis.Apply(spot,src.key,3,cp);int rem1=src.Remaining,left=TwistedMeat.MeatEquivalent(FleshCatalysis.Supplies(spot)),next=FleshCatalysis.Cost(src,1,out _);
   TMSpawn("LP_AberrantPaste",16,cellD);int red2=FleshCatalysis.Apply(spot,src.key,3,cp);string capped=FleshCatalysis.Blocked(spot,src,1,cp);
   Check(rem0>=9*day && max0==6 && cost3==91 && t3==3*day && red1==3*day && rem0-rem1==3*day && left==9 && next==43 && red2==3*day && FleshCatalysis.MaxDays(src)==0 && capped!=null && FleshCatalysis.Cooling(spot).Any(s=>s.key==src.key),"flesh catalysis: 25/30/36 then 43… meat per day, paste = 10 meat, at most 60% of the 10-day cooldown ("+cost3+","+next+","+left+")");
   // 20 血肉催转：窗口派工（工作 1 小时）+ 圣坛限制
   mgr.ClearCooldown(rd);mgr.RegisterCooldown(rd);TMSpawn("Meat_Twisted",30,cellD);var cp2=TMPawn(cellD);int before=src.Remaining;bool ordered=FleshCatalysis.Order(spot,src.key,1,cp2);
   for(int i=0;i<6000 && cp2.CurJobDef?.defName=="LP_FleshCatalysis";i++)Step(1);int after=src.Remaining;
   var altarDef=DefDatabase<ThingDef>.GetNamed("LP_FaithAltar");var fa=ThingMaker.MakeThing(altarDef,altarDef.MadeFromStuff?GenStuff.DefaultStuffFor(altarDef):null);fa.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(fa,TMCell(56),map);
   Faith.Comp.patronCooldownUntil=Faith.Now+15*day;var dev=TMPawn(fa.Position);dev.story.traits.GainTrait(new Trait(Faith.Devotee,kel.devoteeDegree));var godless=TMPawn(fa.Position);
   bool altarOk=FleshCatalysis.Cooling(fa).Any(s=>s.key=="faith:patron") && FleshCatalysis.PawnBlocked(fa,dev)!=null && FleshCatalysis.PawnBlocked(fa,godless)==null;
   Check(ordered && before-after>=day-10 && before-after<day+3000 && altarOk,"flesh catalysis: ordering from the dialog sends the colonist to work 1 hour and shortens 1 day; the altar's patron cooldown can be shortened only by Shar devotees or the godless ("+(before-after)+")");
   // 21 远域回响
   c.echoStacks.Clear();c.echoProgress=0f;var fb=DefDatabase<IncidentDef>.GetNamed("FleshbeastAttack");float e0=fb.Worker.BaseChanceThisGame;c.AddEcho(300f);float e1=fb.Worker.BaseChanceThisGame;float echo1=c.Echo;
   c.AddEcho(3000f);float capE=c.Echo;for(int i=0;i<c.echoStacks.Count;i++)c.echoStacks[i]=TwistedMeat.Now-15*day;float half=c.Echo;c.echoStacks.Clear();
   Check(TwistedMeatEchoChance.IsAnomalyThreat(fb) && Near(echo1,.05f) && (e0<=0f || Near(e1/e0,1.05f)) && Near(capE,.25f) && Near(half,.125f),"twisted meat: every 300 meat used adds +5% to Anomaly threat chance (max 25%), fading over 30 days ("+e0+"→"+e1+")");
   // 22 鬼婆集市
   var hag=DefDatabase<TraderKindDef>.GetNamed("LP_HagMarketTrader");var inf=DefDatabase<TraderKindDef>.GetNamed("LP_InfernalCaravanTrader");var bulk=DefDatabase<TraderKindDef>.GetNamedSilentFail("Caravan_Outlander_BulkGoods");
   HagMarket.ForceGift=true;Thing gift;try{gift=HagMarket.MaybeGift(map,cellC);}finally{HagMarket.ForceGift=false;}
   var hinc=DefDatabase<IncidentDef>.GetNamed("LP_HagMarket");c.hagQuadrum=IncidentWorker_HagMarket.Quadrum;bool q1=IncidentWorker_HagMarket.Allowed(map,false),q2=IncidentWorker_HagMarket.Allowed(map,true);
   Check(hag.WillTrade(paste) && hag.WillTrade(meat) && inf.WillTrade(paste) && (bulk==null || !bulk.WillTrade(paste)) && gift!=null && HagMarket.Gifts.Contains(gift.def.defName) && !q1 && q2,"twisted meat: hag market buys twisted meat and paste (ordinary caravans don't take paste), leaves a cursed gift, at most once a quadrum");
   // 23 孵化槽 + 血肉壁垒
   var cellE=TMCell(66);var incu=(Building_FleshIncubator)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FleshIncubator"));incu.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(incu,cellE,map);incu.Fuel.Refuel(60f);
   var minion=incu.Hatch();bool hatched=minion!=null && minion.Faction==Faction.OfPlayer && FleshWorks.IsMinion(minion) && Near(incu.Fuel.Fuel,30f);
   var mc=minion?.health.hediffSet.hediffs.Select(h=>h.TryGetComp<HediffComp_FleshMinion>()).FirstOrDefault(x=>x!=null);bool three=mc?.ticksLeft==3*day;if(mc!=null){mc.ticksLeft=1;float sv=0f;mc.CompPostTickInterval(ref sv,10);}
   bool rotted=minion!=null && minion.Dead && (minion.Corpse==null || minion.Corpse.Destroyed);
   var wall=(Building_FleshWall)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FleshWall"));wall.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(wall,cellE+new IntVec3(3,0,0),map);
   int hp=wall.MaxHitPoints;wall.HitPoints=hp-200;for(int i=0;i<GenDate.TicksPerDay/GenTicks.TickRareInterval;i++)wall.TickRare();int regen=wall.HitPoints-(hp-200);
   int pre=wall.HitPoints;wall.TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamed("LP_AlchemyAcid"),10f));int acid=pre-wall.HitPoints;
   Check(hatched && three && rotted && Math.Abs(regen-hp/10)<=1 && acid==20,"twisted meat: incubator turns 30 meat into a friendly fleshbeast that rots after 3 days; flesh wall regrows 10% a day and takes double acid damage ("+regen+","+acid+")");
   // 24 畸变巨像 / 死灵滋养 / 巫妖吞食
   var gp=TMPawn(cellE);AberrantGolem.MakeAberrant(gp);float inc=gp.GetStatValue(StatDefOf.IncomingDamageFactor);int m0=map.mapPawns.AllPawnsSpawned.Count(FleshWorks.IsMinion);gp.Kill(null);
   var spawned=map.mapPawns.AllPawnsSpawned.Where(FleshWorks.IsMinion).ToList();bool split=spawned.Count-m0==2 && spawned.All(p=>p.health.hediffSet.hediffs.Select(h=>h.TryGetComp<HediffComp_FleshMinion>()).Any(x=>x!=null && x.ticksLeft<=day));
   var np=TMPawn(cellE);TMSpawn("Meat_Twisted",5,np.Position);float ws0=np.GetStatValue(StatDefOf.WorkSpeedGlobal);bool fed=FleshWorks.Feed(np);float ws1=np.GetStatValue(StatDefOf.WorkSpeedGlobal);
   var ab=worker.abilities.GetAbility(Necro.Def<AbilityDef>("MendConstruct"));ab.StartCooldown(20000);worker.jobs.StopAll();TMSpawn("Meat_Twisted",10,worker.Position);var reset=FleshWorks.Devour(worker);
   Check(Near(inc,.833f) && split && fed && Near(ws1-ws0,.1f) && reset==ab && ab.CooldownTicksRemaining==0,"twisted meat: aberrant golem flesh takes -17% damage and splits into 2 one-day fleshbeasts; nourishing gives +10% work; a lich devouring 10 meat resets a necro ability: "+inc+"/"+split+"("+(spawned.Count-m0)+")/"+fed+"/"+(ws1-ws0)+"/"+(reset==ab)+"/"+ab.CooldownTicksRemaining+"/"+FleshWorks.DevourBlocked(worker));
   TwistedMeat.TestSuppressed=false;
  }
  void R(string n,out RecipeDef r){r=DefDatabase<RecipeDef>.GetNamed(n);}
  void PrepareTwistedMeatSave(){var c=TwistedMeat.Comp;if(c!=null && tmDissected>=0)c.dissected=tmDissected;}
  // 25 存读档
  void CheckTwistedMeatAfterLoad(){
   var c=TwistedMeat.Comp;var vest=Find.Maps.SelectMany(m=>m.mapPawns.AllPawnsSpawned).SelectMany(p=>p.apparel?.WornApparel??new List<Apparel>()).FirstOrDefault(a=>a.thingIDNumber==tmVestId);
   Check(c!=null && Mathf.RoundToInt(c.dissected)==tmDissected && vest!=null && FleshRegen.Marked(vest) && c.cycles.ContainsKey(tmCycleKey??"") && c.aberrantSeeds.Contains(tmSeedId),"twisted meat: save/load keeps dissection total, 血肉再生 marks, catalysis cycles and aberrant seeds");
  }
 }
}
