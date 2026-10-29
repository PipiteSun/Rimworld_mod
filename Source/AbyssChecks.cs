using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.54.0 深渊仪式（组名 Abyss：Test.ps1 -Only Abyss；-NoHAR -Only Abyss 检查没有 HAR 时的菜单与典籍）。
 public partial class LichChecks {
  readonly List<Thing> abMade=new List<Thing>();
  static int abBoundId,abTaintId,abNabassuId,abNamedId,abRiftId;static float abObedience,abTaint,abThin;static string abName;static int abGorge,abHoly;
  IntVec3 abArena;
  Pawn AbHuman(IntVec3 at,int level=0){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,mustBeCapableOfViolence:true,fixedBiologicalAge:30,fixedChronologicalAge:30));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h.def.isBad || h is Hediff_AddedPart).ToList())p.health.RemoveHediff(h);
   if(level>0)Adventurer.TryAdd(p,level,true);
   abMade.Add(p);GenSpawn.Spawn(p,at,map);return p;
  }
  Pawn AbXeno(string xeno,IntVec3 at){var p=RacePawn(xeno);abMade.Add(p);if(p.Spawned)p.DeSpawn();GenSpawn.Spawn(p,at,map);return p;}
  Pawn AbMake(string kind,Faction f,IntVec3 at,Gender? g=null){var p=AbyssRaces.Make(kind,f,map.Tile,g);abMade.Add(p);if(p!=null)GenSpawn.Spawn(p,at,map);return p;}
  IntVec3 AbCell(int dx,int dz){var c=abArena+new IntVec3(dx,0,dz);return c;}
  IntVec3 AbFindArena(){
   foreach(var c in map.AllCells.Where(c=>c.x>25 && c.z>25 && c.x<map.Size.x-40 && c.z<map.Size.z-30).OrderBy(c=>c.DistanceTo(altar.Position)))
    if(new CellRect(c.x-6,c.z-6,34,16).Cells.All(x=>x.InBounds(map) && x.Standable(map) && x.GetFirstPawn(map)==null && x.GetEdifice(map)==null && !x.Fogged(map) && !x.GetTerrain(map).IsWater)){
     foreach(var x in new CellRect(c.x-6,c.z-6,34,16).Cells)foreach(var t in x.GetThingList(map).Where(t=>t is Plant || t.def.category==ThingCategory.Item).ToList())t.Destroy();
     return c;
    }
   return altar.Position+IntVec3.East*8;
  }
  static void AbOff(Pawn p){if(p==null)return;if(p.Spawned)p.DeSpawn();}
  void AbClean(){
   foreach(var t in abMade){
    if(t==null)continue;
    if(t is Pawn p){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(p.Destroyed)continue;p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}
    else if(!t.Destroyed)t.Destroy();
   }
   abMade.Clear();
   foreach(var l in map.lordManager.lords.Where(l=>l.faction?.def==AbyssRaces.HordeDef).ToList())map.lordManager.RemoveLord(l);
   foreach(var r in PortalEvent.Rifts(map).ToList())if(r.Comp!=null && r.Comp.plane==AbyssItems.AbyssPlane){foreach(var s in r.Comp.spawned.ToList())if(!s.Destroyed){s.GetLord()?.RemovePawn(s);if(s.Spawned)s.DeSpawn();s.Destroy();}r.Destroy();}
   foreach(var t in map.listerThings.AllThings.Where(t=>t.def==AbyssItems.SoulCoin || t.def==AbyssItems.DemonBlood || t.def==AbyssItems.TrueNameScroll || t.def==AbyssItems.GorgedFang || t.def==AbyssItems.Shackles || t.def==AbyssItems.Demonomicon).ToList())if(t.Spawned)t.Destroy();
   Items.State?.Flush();
  }
  void ClearAbyssThings(){foreach(var t in map.listerThings.AllThings.Where(t=>t.def==AbyssItems.SoulCoin || t.def==AbyssItems.DemonBlood).ToList())t.Destroy();}

  void CheckAbyss(){
   bool har=ModsConfig.IsActive(HARId);
   abArena=AbFindArena();
   var horde=AbyssRaces.Horde();
   Check(horde!=null && horde.HostileTo(Faction.OfPlayer) && horde.Hidden && AbyssItems.AbyssPlane!=null && !AbyssItems.AbyssPlane.randomPool && DefDatabase<PortalPlaneDef>.AllDefsListForReading.Count(d=>d.randomPool)==4 && AbyssRitual.Research!=null && Math.Abs(AbyssRitual.Research.ResearchViewX-4f)<.01f && Math.Abs(AbyssRitual.Research.ResearchViewY-2.8f)<.01f,
    "abyss basics: hidden permanently hostile horde faction, abyss plane stays out of the portal breach pool (4 planes left), abyssology research at (4, 2.8)");
   try{
    CheckAbyssOutsiders(har);
    CheckAbyssTaint(har);
    CheckAbyssBinding();
    CheckAbyssRitual(har);
    CheckAbyssUnits(har);
    CheckAbyssItems();
    CheckAbyssEvents(har);
    CheckAbyssThinness();
    CheckAbyssPerformance();
   }finally{Legendary.TestD20=null;Outsiders.TestRoll=null;AbyssRitual.TestRoll=null;Thinness.TestRoll=null;TrueNameInterrogation.TestRoll=null;AbyssEvents.TestRoll=null;LichMod.Settings.abyssBindColonists=true;AbClean();}
  }

  // ---------- 外来者层与遣返 ----------
  void CheckAbyssOutsiders(bool har){
   var ice=AbXeno("LP_IceDevil",AbCell(0,0));var balor=AbXeno("LP_Balor",AbCell(2,0));
   var ei=Outsiders.Of(ice);var eb=Outsiders.Of(balor);
   Check(Fiends.Is(ice) && Fiends.Is(balor) && Outsiders.IsDevil(ice) && Outsiders.IsDemon(balor) && Near(ei.coldIron,1.5f) && Near(ei.silver,1.5f) && Near(ei.holy,2f) && Near(eb.coldIron,1.5f) && Near(eb.holy,2f) && Near(ei.trueNameChance,.4f),
    "migrated fiends: ice devil plane=Hells (cold iron 1.5, silver 1.5, holy 2), balor plane=Abyss; Fiends.Is still true for both");
   var fighter=AbHuman(AbCell(4,0));var swordDef=DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword");
   var cold=(ThingWithComps)ThingMaker.MakeThing(swordDef,ItemMaterials.ColdIron);fighter.equipment.AddEquipment(cold);
   float fc=ItemMaterials.WeaponFactor(ice,new DamageInfo(DamageDefOf.Cut,10,0,-1,fighter,null,swordDef));
   fighter.equipment.DestroyAllEquipment();var silver=(ThingWithComps)ThingMaker.MakeThing(swordDef,ThingDefOf.Silver);fighter.equipment.AddEquipment(silver);
   float fs=ItemMaterials.WeaponFactor(ice,new DamageInfo(DamageDefOf.Cut,10,0,-1,fighter,null,swordDef)),fsb=ItemMaterials.WeaponFactor(balor,new DamageInfo(DamageDefOf.Cut,10,0,-1,fighter,null,swordDef));
   Check(Near(fc,1.5f) && Near(fs,1.5f) && Near(fsb,1f) && Near(Outsiders.HolyFactor(ice),2f) && ItemThrowables.HolyTarget(balor) && !ItemThrowables.HolyTarget(fighter),"cold iron x1.5 and silver x1.5 against the ice devil (silver does nothing to the balor); holy water x2 reads the extension ("+fc+"/"+fs+"/"+fsb+")");
   fighter.equipment.DestroyAllEquipment();
   // 遣返规则表：敌人 / 仆从 / 束缚者（击倒、死亡）/ 锚链
   Outsiders.TestRoll=0f;
   var foe=AbMake("LP_AH_Barlgura",AbyssRaces.Horde(),AbCell(6,0));var at=foe.Position;
   foe.health.AddHediff(HediffDefOf.Anesthetic);FlushDeferred();
   var loot=GenRadial.RadialDistinctThingsAround(at,map,3f,true).ToList();
   Check(foe.Destroyed && !foe.Dead && loot.Any(t=>t.def==AbyssItems.SoulCoin) && loot.Any(t=>t.def==AbyssItems.DemonBlood) && loot.Any(t=>t.def==AbyssItems.TrueNameScroll),"a downed hostile barlgura is banished and drops soul coins, demon blood and its true name scroll (forced rolls)");
   ClearAbyssThings();foreach(var t in loot.Where(t=>!t.Destroyed && t.def==AbyssItems.TrueNameScroll))t.Destroy();
   var thrall=AbMake("LP_AH_Dretch",Faction.OfPlayer,AbCell(8,0));AbyssBinding.MakeThrall(thrall,GenDate.TicksPerDay);var tat=thrall.Position;
   Check(Outsiders.Thrall(thrall) && thrall.needs.mood==null,"a thrall has the thrall status and no mood need");
   thrall.health.AddHediff(HediffDefOf.Anesthetic);FlushDeferred();
   Check(thrall.Destroyed && !GenRadial.RadialDistinctThingsAround(tat,map,3f,true).Any(t=>t.def==AbyssItems.SoulCoin || t.def==AbyssItems.TrueNameScroll),"a downed thrall is banished without loot or true name scroll");
   var master=AbHuman(AbCell(10,2));
   var bound=AbMake("LP_AH_Barlgura",AbyssRaces.Horde(),AbCell(10,0));AbyssBinding.Bind(bound,master,80f);
   bound.health.AddHediff(HediffDefOf.Anesthetic);FlushDeferred();
   Check(!bound.Destroyed && bound.Downed && bound.Faction==Faction.OfPlayer && Outsiders.Bound(bound),"a bound demon who is downed stays down like a colonist (no banishing)");
   var stuff=ThingMaker.MakeThing(ThingDefOf.Silver);stuff.stackCount=7;bound.inventory.innerContainer.TryAdd(stuff);var bat=bound.Position;
   bound.Kill(null);FlushDeferred();
   Check(bound.Corpse==null || bound.Corpse.Destroyed,"a dead bound demon's corpse vanishes back to the abyss");
   Check(GenRadial.RadialDistinctThingsAround(bat,map,3f,true).Any(t=>t.def==ThingDefOf.Silver && t.stackCount==7) && !GenRadial.RadialDistinctThingsAround(bat,map,3f,true).Any(t=>t.def==AbyssItems.SoulCoin),"its belongings drop where it died, and it drops no demon loot");
   foreach(var t in GenRadial.RadialDistinctThingsAround(bat,map,3f,true).Where(t=>t.def==ThingDefOf.Silver).ToList())t.Destroy();
   var chained=AbMake("LP_AH_Dretch",AbyssRaces.Horde(),AbCell(12,0));Outsiders.Shackle(chained,null);
   chained.health.AddHediff(HediffDefOf.Anesthetic);FlushDeferred();
   bool alive=!chained.Destroyed && chained.Downed;chained.guest.CapturedBy(Faction.OfPlayer,master);
   Check(alive && chained.IsPrisonerOfColony,"a shackled demon who is downed is not banished and can be captured");
   chained.Kill(null);FlushDeferred();
   Check(chained.Corpse!=null && !chained.Corpse.Destroyed,"a shackled demon leaves a normal corpse");
   if(har){var mar=AbMake("LP_MarilithColonist",Faction.OfPlayer,AbCell(14,0));Check(mar!=null && Outsiders.IsDemon(mar) && !Fiends.Is(mar) && Outsiders.Of(mar).taintImmune,"the marilith (HAR) is an abyss outsider that is never banished and immune to taint");}
   Outsiders.TestRoll=null;
  }

  // ---------- 腐化 ----------
  void CheckAbyssTaint(bool har){
   var a=AbHuman(AbCell(0,3));float d=AbyssTaint.Add(a,.1f);
   var aas=AbXeno("LP_Aasimar",AbCell(1,3));float da=AbyssTaint.Add(aas,.1f);
   var dretch=AbMake("LP_AH_Dretch",AbyssRaces.Horde(),AbCell(2,3));float dd=AbyssTaint.Add(dretch,.1f);
   var elf=AbXeno("LP_Elf",AbCell(3,3));float de=AbyssTaint.Add(elf,.1f);
   Check(Near(d,.1f) && Near(da,.2f) && Near(dd,0f) && Near(de,.15f) && Near(AbyssTaint.RaceFactor(dretch),0f),"taint gain uses the race table: human x1, aasimar x2, elf x1.5, demons (taint immune) x0 ("+d+"/"+da+"/"+de+"/"+dd+")");
   var h=AbyssTaint.Get(a);
   h.Severity=.05f;bool hidden=!h.Visible;h.Severity=.12f;int s1=h.CurStageIndex;h.Severity=.35f;int s2=h.CurStageIndex;var st2=h.CurStage;h.Severity=.65f;int s3=h.CurStageIndex;var st3=h.CurStage;h.Severity=.92f;int s4=h.CurStageIndex;
   Check(hidden && s1==1 && s2==2 && s3==3 && s4==4 && Near(st2.statFactors.GetStatFactorFromList(StatDefOf.MeleeDamageFactor),1.06f) && Near(st2.statOffsets.GetStatOffsetFromList(StatDefOf.PainShockThreshold),.1f) && st3.damageFactors!=null && st3.damageFactors.Any(f=>f.damageDef==DamageDefOf.Flame && Near(f.factor,.8f)),
    "taint stages: hidden under 10%, whispers / shadow (melee x1.06, pain threshold +10%) / demon mark (fire x0.8) / fallen");
   h.Severity=.65f;for(int i=0;i<24;i++)AbyssTaint.Hourly(a,h);float mark=h.Severity;
   h.Severity=.2f;for(int i=0;i<24;i++)AbyssTaint.Hourly(a,h);float low=h.Severity;
   Check(Near(mark,.65f) && Math.Abs(low-.195f)<.001f,"after the demon mark taint no longer fades; below it, -0.5% per day ("+mark+" / "+low+")");
   var water=ThingMaker.MakeThing(Faith.WaterDef);abMade.Add(water);float before=h.Severity;water.TryGetComp<CompUseEffect_BlessedWater>()?.DoEffect(a);
   Check(Near(before-h.Severity,.1f),"drinking blessed water removes 10% taint");
   // 化魔
   h.Severity=.95f;AbyssTaint.Add(a,.1f);
   Check(AbyssRaces.IsCambion(a) && Near(AbyssTaint.Severity(a),.6f),"a human who reaches 100% taint turns into a cambion and drops back to 60%");
   var tief=AbXeno("LP_Tiefling",AbCell(4,3));AbyssTaint.Add(tief,.5f);AbyssTaint.Get(tief).Severity=.99f;AbyssTaint.Add(tief,.2f);
   var lich=AbHuman(AbCell(5,3));lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),false);AbyssTaint.Add(lich,.2f);AbyssTaint.Get(lich).Severity=.99f;AbyssTaint.Add(lich,.5f);
   Check(Items.Has(tief,AbyssTaint.FavoredDef) && Near(AbyssTaint.Severity(tief),1f) && !AbyssRaces.IsCambion(tief) && Items.Has(lich,AbyssTaint.TaintedPhylacteryDef) && Near(AbyssTaint.Severity(lich),1f),"a tiefling who falls becomes an abyss favored (stays at 100%), a lich gets a tainted phylactery");
   abTaintId=elf.thingIDNumber;abTaint=AbyssTaint.Severity(elf);
  }

  // ---------- 束缚 ----------
  void CheckAbyssBinding(){
   var master=AbHuman(AbCell(0,6));var b=AbMake("LP_AH_Hezrou",AbyssRaces.Horde(),AbCell(1,6));var h=AbyssBinding.Bind(b,master,60f);
   for(int i=0;i<24;i++)h.TickInterval(GenDate.TicksPerHour);float day1=h.Severity;
   var scroll=TrueNames.MakeScroll(b);GenPlace.TryPlaceThing(scroll,AbCell(2,7),map,ThingPlaceMode.Near);abMade.Add(scroll);
   for(int i=0;i<24;i++)h.TickInterval(GenDate.TicksPerHour);float day2=h.Severity;
   Check(Near(day1,58f) && Near(day2,57f),"bound obedience decays 2 per day, halved while its true name scroll is on the map ("+day1+" / "+day2+")");
   var w=new InteractionWorker_ReassertName();float wt=w.RandomSelectionWeight(master,b);float before=h.Severity;w.Interacted(master,b,null,out _,out _,out _,out _);float wt2=w.RandomSelectionWeight(master,b);
   Check(wt>0 && Near(h.Severity-before,8f) && wt2==0f,"reasserting the true name gives +8 once per day (master only)");
   abBoundId=b.thingIDNumber;abObedience=h.Severity;
   var b2=AbMake("LP_AH_Dretch",AbyssRaces.Horde(),AbCell(3,6));var m2=AbHuman(AbCell(4,6));var h2=AbyssBinding.Bind(b2,m2,70f);
   m2.Kill(null);
   Check(Near(h2.Severity,30f),"the master's death costs 40 obedience ("+h2.Severity+")");
   h2.Change(-40f,"test");FlushDeferred();
   Check(!Outsiders.Bound(b2) && b2.Faction==AbyssRaces.Horde(),"at 0 obedience the demon breaks free and joins the abyss horde");
   Check(AbyssBinding.BoundCap()==1+AdventurerEnemies.ColonyMaxLevel()/5+LichMod.Settings.abyssBoundExtra,"bound cap = 1 + highest colony level / 5 + extra setting ("+AbyssBinding.BoundCap()+")");
  }

  // ---------- 仪式 ----------
  void CheckAbyssRitual(bool har){
   var tc=Thinness.Of(map);tc.value=0f;tc.active=false;
   var circle=(Building_AbyssCircle)ThingMaker.MakeThing(AbyssItems.AbyssCircle);circle.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(circle,AbCell(20,6),map);abMade.Add(circle);
   var lead=AbHuman(AbCell(16,4),1);var ha=Adventurer.Get(lead);if(ha!=null){ha.scores=new[]{10,10,10,8,10,10};ha.Dirty();}lead.skills.GetSkill(SkillDefOf.Social).Level=0;
   var t1=AbyssRitual.Target("dretch");var t2=AbyssRitual.Target("barlgura");var t3=AbyssRitual.Target("nabassu");
   float c1=AbyssRitual.Chance(circle,t1,lead,0),c2=AbyssRitual.Chance(circle,t2,lead,0);circle.silverEdge=true;float c3=AbyssRitual.Chance(circle,t3,lead,2);circle.silverEdge=false;
   Check(Near(c1,.85f) && Near(c2,.7f) && Near(c3,.68f),"ritual chance formula for a level 1 leader with Int 8, Cha 10: tier1 85%, tier2 70%, tier3 with 2 wards and a silver edge 68% ("+c1+" / "+c2+" / "+c3+")");
   // 成功：一阶劣魔群，祭品 2 灵魂币
   var coins=ThingMaker.MakeThing(AbyssItems.SoulCoin);coins.stackCount=5;GenPlace.TryPlaceThing(coins,AbCell(16,6),map,ThingPlaceMode.Near);
   float taint0=AbyssTaint.Severity(lead);
   circle.targetKey="dretch";circle.leader=lead;circle.offering=AbyssRitual.Offerings(1)[1];circle.active=true;
   AbyssRitual.TestRoll=.1f;var r=AbyssRitual.Resolve(circle,lead,new List<Pawn>());
   var thralls=map.mapPawns.AllPawnsSpawned.Where(p=>AbyssRaces.Is(p,AbyssRaces.Dretch) && Outsiders.Thrall(p) && !abMade.Contains(p)).ToList();abMade.AddRange(thralls);
   Check(r==RitualResult.Success && thralls.Count>=3 && thralls.Count<=6 && AbyssRitual.Count(map,AbyssItems.SoulCoin)==3 && Near(tc.value,5f) && tc.active && Near(AbyssTaint.Severity(lead)-taint0,.05f) && !circle.active,"a successful tier-1 ritual summons 3-6 dretch thralls, eats 2 soul coins, raises thinness by 5 and the leader's taint by 5% ("+thralls.Count+" thralls, thinness "+tc.value+")");
   // 召来但未束缚：差 < 20
   circle.targetKey="dretch";circle.leader=lead;circle.offering=AbyssRitual.Offerings(1)[1];circle.active=true;
   var already=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
   AbyssRitual.TestRoll=.95f;r=AbyssRitual.Resolve(circle,lead,new List<Pawn>());
   var hostile=map.mapPawns.AllPawnsSpawned.Where(p=>!already.Contains(p) && AbyssRaces.Is(p,AbyssRaces.Dretch) && p.Faction==AbyssRaces.Horde()).ToList();abMade.AddRange(hostile);
   Check(r==RitualResult.Unbound && hostile.Count>=2 && hostile.Count<=4 && Near(tc.value,15f),"a near miss summons 2-4 hostile dretches for the horde and doubles the thinness gain");
   foreach(var p in hostile){p.GetLord()?.RemovePawn(p);p.DeSpawn();}
   // 裂口：差 ≥ 20（二阶，祭品 6 灵魂币 + 1 恶魔血）
   var more=ThingMaker.MakeThing(AbyssItems.SoulCoin);more.stackCount=6;GenPlace.TryPlaceThing(more,AbCell(16,6),map,ThingPlaceMode.Near);var blood=ThingMaker.MakeThing(AbyssItems.DemonBlood);GenPlace.TryPlaceThing(blood,AbCell(16,7),map,ThingPlaceMode.Near);
   circle.targetKey="barlgura";circle.leader=lead;circle.offering=AbyssRitual.Offerings(2)[1];circle.active=true;
   AbyssRitual.TestRoll=1f;r=AbyssRitual.Resolve(circle,lead,new List<Pawn>());
   Check(r==RitualResult.Rift && circle.Locked && circle.lockRift.Comp.plane==AbyssItems.AbyssPlane && AbyssRitual.Count(map,AbyssItems.DemonBlood)==0 && Near(tc.value,35f),"a bad failure opens an abyss rift next to the circle, locks the circle and still eats the offering");
   var start=AbyssRitual.Unavailable(circle,t1);
   var rift=circle.lockRift;foreach(var s in rift.Comp.spawned)abMade.Add(s);
   for(int i=0;i<4;i++)PortalEvent.HolyNear(map,rift.Position);int hits=rift.Comp.holyHits;bool open=!rift.Destroyed;PortalEvent.HolyNear(map,rift.Position);
   Check(open && hits==4 && rift.Destroyed && !circle.Locked,"holy water landing within 3 cells counts; the fifth bottle closes the abyss rift and frees the circle");
   ClearAbyssThings();
   // 没装 HAR：菜单里没有六臂蛇魔，典籍不出六臂蛇魔
   var mt=AbyssRitual.Target("marilith");var demons=Enumerable.Range(0,300).Select(i=>AbyssItems.RandomDemon()).ToList();
   Check(har?AbyssRitual.Unavailable(circle,mt)!="需要 Humanoid Alien Races":AbyssRitual.Unavailable(circle,mt)=="需要 Humanoid Alien Races" && !demons.Contains("marilith"),"marilith option exists only with HAR (har="+har+"); without HAR the demonomicon never records it");
   LichMod.Settings.abyssBindColonists=false;string off3=AbyssRitual.Unavailable(circle,t3);string offBind=AbyssRitual.Unavailable(circle,AbyssRitual.Target("barlguraBind"));LichMod.Settings.abyssBindColonists=true;
   Check(off3!=null && off3.Contains("允许束缚") && offBind!=null,"with binding disabled in the settings tier 3 and every binding option are greyed out");
   tc.value=0f;
  }

  // ---------- 单位 ----------
  void CheckAbyssUnits(bool har){
   var f=AbyssRaces.Horde();var made=new Dictionary<string,Pawn>();int x=0;
   foreach(var k in new[]{"LP_AH_Dretch","LP_AH_Barlgura","LP_AH_Hezrou","LP_AH_Succubus","LP_AH_Nabassu","LP_AH_ShadowDemon"}){made[k]=AbMake(k,f,AbCell(x,9));x+=2;}
   var cam=AbXeno("LP_Cambion",AbCell(x,9));var qz=AbMake("LP_QuasitFamiliar",Faction.OfPlayer,AbCell(x+2,9));
   Check(made.Values.All(p=>p!=null && Outsiders.IsDemon(p)) && Near(made["LP_AH_Dretch"].BodySize,.7f) && Near(made["LP_AH_Barlgura"].BodySize,1.6f) && Near(made["LP_AH_Hezrou"].BodySize,1.8f) && Near(made["LP_AH_Nabassu"].BodySize,1.6f) && AbyssRaces.IsSuccubus(made["LP_AH_Succubus"]) && made["LP_AH_Succubus"].needs.food!=null && made["LP_AH_Dretch"].needs.food==null && AbyssRaces.IsCambion(cam) && !Outsiders.Is(cam) && qz!=null && Outsiders.IsDemon(qz) && !Fiends.Is(qz) && qz.RaceProps.Animal,
    "all six demon xenotypes and the quasit generate with their genes and sizes (dretch 0.7, barlgura 1.6, hezrou 1.8, nabassu 1.6); the succubus eats, the dretch does not; the cambion is a mortal, not an outsider");
   // 跃击
   var bar=made["LP_AH_Barlgura"];var enemy=AbHuman(bar.Position+IntVec3.East);Legendary.TestD20=1;int stunned=AbyssRaces.LeapImpact(bar);Legendary.TestD20=null;
   Check(stunned>=1 && enemy.stances.stunner.Stunned,"barlgura leap: enemies next to the landing spot fail Dex DC 13 and are stunned");
   AbOff(enemy);
   // 哈兹魔恶臭
   var hez=made["LP_AH_Hezrou"];var sniff=AbHuman(hez.Position+IntVec3.North*2);var immune=new Dictionary<int,int>();
   Legendary.TestD20=1;int n1=AbyssRaces.HezrouStench(hez,immune);Legendary.TestD20=null;
   var sniff2=AbHuman(hez.Position+IntVec3.South*2);Legendary.TestD20=20;int n2=AbyssRaces.HezrouStench(hez,immune);Legendary.TestD20=1;int n3=AbyssRaces.HezrouStench(hez,immune);Legendary.TestD20=null;
   Check(n1>=1 && Items.Has(sniff,"LP_Nauseated") && !Items.Has(sniff2,"LP_Nauseated") && immune.ContainsKey(sniff2.thingIDNumber) && n3==0,"hezrou stench: a failed Con save makes the target nauseated; a pawn who saves is immune for a minute");
   AbOff(sniff);AbOff(sniff2);
   // 魅惑之吻
   var suc=made["LP_AH_Succubus"];var foe=AbHuman(suc.Position+IntVec3.West);
   Legendary.TestD20=1;var k1=AbyssRaces.Kiss(suc,foe);Legendary.TestD20=null;var foe2=AbHuman(suc.Position+IntVec3.East);Legendary.TestD20=20;var k2=AbyssRaces.Kiss(suc,foe2);Legendary.TestD20=1;var k3=AbyssRaces.Kiss(suc,foe2);Legendary.TestD20=null;
   Check(k1=="charmed" && foe.InMentalState && k2=="saved" && k3=="immune","succubus kiss: a failed Wis save charms an enemy; a successful save grants 24h immunity ("+k1+"/"+k2+"/"+k3+")");
   if(foe.InMentalState)foe.MentalState.RecoverFromState();AbOff(foe);AbOff(foe2);
   // 生育：魅魔的孩子是坎比恩
   var mom=AbXeno("LP_Succubus",AbCell(0,12));var baby=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDowned:true,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,fixedBiologicalAge:0,fixedChronologicalAge:0,developmentalStages:DevelopmentalStage.Newborn));abMade.Add(baby);
   baby.relations.AddDirectRelation(PawnRelationDefOf.Parent,mom);
   AccessTools.Method(typeof(SuccubusCambionChild),"Postfix").Invoke(null,new object[]{baby});
   Check(baby.genes.Xenotype==AbyssRaces.CambionXeno && AbyssRaces.IsCambion(baby),"a succubus's child is born a cambion");
   // 纳巴苏噬魂
   var nab=made["LP_AH_Nabassu"];var prey=AbHuman(nab.Position+IntVec3.East);
   prey.Kill(new DamageInfo(DamageDefOf.Cut,500f,0f,-1f,nab));
   bool gone=Items.Has(prey,"LP_SoulDevoured");bool res=ResurrectionUtility.TryResurrect(prey,new ResurrectionParams{removeDiedThoughts=true});
   Check(gone && !res && prey.Dead && AbyssRaces.Gorge(nab)==1,"nabassu devour: its victim's soul is gone, resurrection serum fails, the nabassu gains 1 gorge layer");
   AbyssRaces.AddGorge(nab,2);abNabassuId=nab.thingIDNumber;abGorge=AbyssRaces.Gorge(nab);
   // 影魔：非魔法武器 ×0.5，亮处 ×1.5
   var sd=made["LP_AH_ShadowDemon"];var e=Outsiders.Of(sd);var claw=new DamageInfo(DamageDefOf.Scratch,10f,0f,-1f,prey,null,ThingDefOf.Human);
   float df=Outsiders.DamageFactor(sd,e,claw);float want=.5f*(Outsiders.Lit(sd)?1.5f:1f);
   Check(e.lightBane && Near(e.nonMagicFactor,.5f) && Near(df,want) && Near(Outsiders.DamageFactor(sd,e,new DamageInfo(DamageDefOf.Flame,10f)),Outsiders.Lit(sd)?1.5f:1f),"shadow demon: natural / mundane weapons x0.5, x1.5 more in the light, abilities without a weapon unaffected by the mundane rule ("+df+", lit "+Outsiders.Lit(sd)+")");
  }

  // ---------- 真名卷 / 锚链 / 审讯 / 典籍 ----------
  void CheckAbyssItems(){
   var f=AbyssRaces.Horde();var d=AbMake("LP_AH_Dretch",f,AbCell(0,14));var n1=TrueNames.Of(d);var n2=TrueNames.Of(d);
   var scroll=TrueNames.MakeScroll(d);abMade.Add(scroll);
   Check(!n1.NullOrEmpty() && n1==n2 && n1.Length>=3 && n1.Length<=4 && scroll.LabelNoCount.Contains(n1) && scroll.TryGetComp<CompTrueName>().pawnId==d.thingIDNumber,"each outsider has a stable 3-4 syllable true name; the scroll label shows it ("+scroll.LabelNoCount+")");
   abNamedId=d.thingIDNumber;abName=n1;
   var user=AbHuman(AbCell(2,14));Legendary.TestD20=1;bool ban=TrueNames.UseOnPawn(user,d,scroll);Legendary.TestD20=null;
   Check(ban && d.Destroyed && scroll.Destroyed,"speaking a hostile demon's true name: it fails the Cha save and is banished; the scroll is used up");
   // 锚链：带着锚链的殖民者 5 格内，倒下的敌对外来者被自动锁住
   var foe=AbMake("LP_AH_Barlgura",f,AbCell(4,14));var keeper=AbHuman(AbCell(5,15));var sh=ThingMaker.MakeThing(AbyssItems.Shackles);sh.stackCount=2;keeper.inventory.innerContainer.TryAdd(sh);
   foe.health.AddHediff(HediffDefOf.Anesthetic);FlushDeferred();
   Check(!foe.Destroyed && Outsiders.Shackled(foe) && keeper.inventory.innerContainer.Where(t=>t.def==AbyssItems.Shackles).Sum(t=>t.stackCount)==1,"a colonist carrying dimensional shackles within 5 cells auto-shackles a downed hostile demon (one pair used)");
   foe.guest.CapturedBy(Faction.OfPlayer,keeper);
   TrueNameInterrogation.TestRoll=.5f;var none=TrueNameInterrogation.Interrogate(keeper,foe);var st=AbyssState.Get;st.interrogated.Clear();
   TrueNameInterrogation.TestRoll=.1f;foe.guest.SetExclusiveInteraction(TrueNameInterrogation.Mode);var got=TrueNameInterrogation.Interrogate(keeper,foe);TrueNameInterrogation.TestRoll=null;if(got!=null)abMade.Add(got);
   Check(TrueNameInterrogation.Eligible(foe) && none==null && got!=null && got.TryGetComp<CompTrueName>().pawnId==foe.thingIDNumber && foe.guest.ExclusiveInteractionMode==PrisonerInteractionModeDefOf.MaintainOnly,"interrogating a shackled prisoner: 15% (fixed roll) to get its true name scroll, then the mode switches back to maintain");
   // 典籍：读满 4 小时学会；每小时腐化 +0.5%
   var book=(Book_Demonomicon)ThingMaker.MakeThing(AbyssItems.Demonomicon);abMade.Add(book);book.demon="hezrou";st.known.Remove("hezrou");
   var reader=AbHuman(AbCell(7,14));for(int i=0;i<4;i++)BookOutcome_Demonomicon.Feed(reader,book,GenDate.TicksPerHour);
   Check(st.Known("hezrou") && Near(AbyssTaint.Severity(reader),.02f) && book.LabelNoCount.Contains("哈兹魔"),"reading a demonomicon for 4 hours teaches the colony its demon; +0.5% taint per hour");
   st.known.Add("barlgura");
  }

  // ---------- 事件 ----------
  void CheckAbyssEvents(bool har){
   var st=AbyssState.Get;
   // 深渊低语：三个选项
   var p=AbHuman(AbCell(0,17));var book=ThingMaker.MakeThing(AbyssItems.Demonomicon);GenPlace.TryPlaceThing(book,p.Position,map,ThingPlaceMode.Near);abMade.Add(book);
   float t0=AbyssTaint.Severity(p);ChoiceLetter_AbyssWhisper.Keep(p,book);float kept=AbyssTaint.Severity(p)-t0;
   ChoiceLetter_AbyssWhisper.Burn(p,book);bool burned=book.Destroyed && p.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_RejectedAbyss"))!=null;
   var colony=Factions.Colony;bool give=true;
   if(colony!=null && ChoiceLetter_AbyssWhisper.CannotGive()==null){int g0=colony.BaseGoodwillWith(Faction.OfPlayer);var b2=ThingMaker.MakeThing(AbyssItems.Demonomicon);GenPlace.TryPlaceThing(b2,p.Position,map,ThingPlaceMode.Near);ChoiceLetter_AbyssWhisper.Give(b2);give=b2.Destroyed && colony.BaseGoodwillWith(Faction.OfPlayer)==Math.Min(100,g0+10);}
   var worker=DefDatabase<IncidentDef>.GetNamed("LP_AbyssWhisper").Worker;var parms=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc,map);parms.forced=true;
   Check(Near(kept,.05f) && burned && give && worker.CanFireNow(parms),"abyss whisper: keep the book (+5% taint), burn it (+3 mood), or give it to the Faerun colony (+10 goodwill)");
   // 魅魔来访：揭晓后换成魅魔
   var cell=AbCell(3,17);var v=IncidentWorker_SuccubusVisit.Visitor(map,cell);abMade.Add(v);
   bool hiddenOk=Outsiders.Disguised(v) && !AbyssRaces.IsSuccubus(v) && v.genes.Xenotype==XenotypeDefOf.Baseliner && !v.health.hediffSet.GetFirstHediffOfDef(Outsiders.DisguiseDef).Visible;
   var gender=v.gender;AbyssEvents.RevealSuccubus(v,"测试");
   Check(hiddenOk && AbyssRaces.IsSuccubus(v) && !Outsiders.Disguised(v) && v.gender==gender && v.Faction==Faction.OfPlayer,"succubus visit: arrives as a baseliner wanderer with a hidden disguise, the reveal converts it to a succubus of the same gender");
   ChoiceLetter_SuccubusReveal.Fight(v);
   Check(v.Faction==AbyssRaces.Horde(),"choosing to fight turns the revealed succubus over to the horde");
   LichMod.Settings.abyssBindColonists=false;var vparms=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc,map);bool canVisit=DefDatabase<IncidentDef>.GetNamed("LP_SuccubusVisit").Worker.CanFireNow(vparms);LichMod.Settings.abyssBindColonists=true;
   Check(!canVisit,"with binding disabled the succubus visit never fires");
   // 深渊入侵：白天跳过影魔，炎魔要 3500 点
   PortalEvent.TryFindSpot(map,out var spot);var low=PortalEvent.Open(map,spot,AbyssItems.AbyssPlane,1000f,false);abMade.AddRange(low.Comp.spawned);
   low.Comp.wave=2;var w3=low.Comp.SpawnWave();abMade.AddRange(w3);bool night=PortalEvent.Night(map);int shadows=w3.Count(x=>AbyssRaces.Is(x,AbyssRaces.ShadowDemon));
   low.Comp.wave=4;var w5=low.Comp.SpawnWave();abMade.AddRange(w5);
   Check((night?shadows>=1:shadows==0) && w3.Any(x=>AbyssRaces.Is(x,AbyssRaces.Hezrou)) && w3.All(x=>x.Faction==AbyssRaces.Horde()) && !w5.Any(Fiends.IsBalor) && w5.Any(AbyssRaces.IsSuccubus),"abyss incursion waves: horde demons, hezrou from wave 3, shadow demons only at night (night="+night+", shadows "+shadows+"), no balor under 3500 points");
   low.Destroy();
   PortalEvent.TryFindSpot(map,out spot);var big=PortalEvent.Open(map,spot,AbyssItems.AbyssPlane,4000f,false);abMade.AddRange(big.Comp.spawned);big.Comp.wave=4;var b5=big.Comp.SpawnWave();abMade.AddRange(b5);
   Check(b5.Count(Fiends.IsBalor)==1,"at 4000 points wave 5 brings exactly one balor");
   abRiftId=big.thingIDNumber;big.Comp.holyHits=3;abHoly=3;
   PortalEvent.TryFindSpot(map,out spot);var forced=PortalEvent.Open(map,spot,AbyssItems.AbyssPlane,500f,true);abMade.AddRange(forced.Comp.spawned);
   Check(forced.Comp.spawned.Count(Fiends.IsBalor)==1,"a rift from a failed tier-4 ritual brings the balor in its first wave regardless of points");
   foreach(var s in forced.Comp.spawned.ToList())if(!s.Destroyed){s.GetLord()?.RemovePawn(s);if(s.Spawned)s.DeSpawn();}forced.Destroy();
   if(har){var mar=AbMake("LP_MarilithColonist",Faction.OfPlayer,AbCell(6,17));Check(PortalEvent.CanCommand(mar) && !PortalEvent.CanCommand(p),"a marilith can command a rift closed like a genie");}
   foreach(var s in big.Comp.spawned.ToList())if(!s.Destroyed){s.GetLord()?.RemovePawn(s);if(s.Spawned)s.DeSpawn();}
   // 纳巴苏：夜里出现，白天推迟到 22 点
   var np=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatSmall,map);np.forced=true;int hour=GenLocalDate.HourOfDay(map);st.hunters.Clear();st.huntTick=-1;
   bool fired=new IncidentWorker_NabassuHunt{def=DefDatabase<IncidentDef>.GetNamed("LP_NabassuHunt")}.TryExecute(np);bool nightNow=hour>=AbyssEvents.HuntHour || hour<4;
   bool ok=fired && (nightNow?st.hunters.Count==1:st.hunters.Count==0 && st.huntTick>Find.TickManager.TicksGame);
   if(!nightNow){st.huntTick=-1;st.huntMap=null;AbyssEvents.SpawnHunter(map,st);}
   var hunter=st.hunters.FirstOrDefault();
   Check(ok && hunter!=null && AbyssRaces.Is(hunter,AbyssRaces.Nabassu) && hunter.Faction==AbyssRaces.Horde(),"nabassu hunt: spawns at night, during the day it waits until 22:00 (hour "+hour+")");
   if(hunter!=null){abMade.Add(hunter);AbyssRaces.AddGorge(hunter,2);
    if(hour>=AbyssEvents.DawnHour && hour<AbyssEvents.DawnHour+2){AbyssEvents.TickHunters(st,Find.TickManager.TicksGame);Check(hunter.Destroyed && st.hunterLayers==2 && st.hunters.Count==0,"at dawn a surviving nabassu flies away and keeps its gorge layers for next time");}}
   st.hunters.Clear();st.hunterLayers=0;
  }

  // ---------- 薄弱度 ----------
  void CheckAbyssThinness(){
   var tc=Thinness.Of(map);tc.value=30f;tc.active=false;tc.lastDaily=-1;
   for(int i=0;i<3;i++)tc.MapComponentTick();float idle=tc.value;
   var glyphs=new List<Thing>();for(int i=0;i<6;i++){var g=ThingMaker.MakeThing(AbyssItems.WardingGlyph);g.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(g,AbCell(i*2,20),map);glyphs.Add(g);abMade.Add(g);}
   tc.active=true;Thinness.TestRoll=1f;foreach(var r in PortalEvent.Rifts(map).ToList())if(r.thingIDNumber!=abRiftId)r.Destroy();
   int glyphCount=tc.Glyphs;int rifts=tc.AbyssRifts;float before=tc.value;tc.Daily();float after=tc.value;Thinness.TestRoll=null;
   float expected=before+Thinness.RiftDaily*rifts+Thinness.ItemDaily*tc.Relics-2f-5f;
   Check(Near(idle,30f) && glyphCount==5 && Near(after,Mathf.Clamp(expected,0f,100f)),"thinness: an inactive map does nothing; a day removes 2 plus 1 per warding glyph (capped at 5 of 6), +3 per open abyss rift ("+before+" -> "+after+")");
   Check(Thinness.WhisperChance(10f)==0f && Near(Thinness.WhisperChance(30f),.1f) && Thinness.IncursionChance(50f)==0f && Near(Thinness.IncursionChance(90f),.1f),"thinness event odds: whisper (v-10)/200 from 20, incursion (v-50)/400 from 60");
   PortalEvent.TryFindSpot(map,out var c);bool blocked=!PortalEvent.SpotOk(glyphs[0].Position,map,new List<IntVec3>());
   Check(blocked,"a portal rift cannot open inside a warding glyph's radius");
   abThin=tc.value;
  }

  // ---------- 性能（规格第九节）：100 人地图。“关”= 100 个普通殖民者；“开”= 其中 10 个换成深渊仆从（恶臭、静立隐形、梦境吸取、饱魂），另外 90 人都带腐化，外加一个驱魔结界。交替各跑两轮 1500 刻取平均。 ----------
  void CheckAbyssPerformance(){
   var crowd=new List<Pawn>();
   Pawn Human(IntVec3 c){var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner));GenSpawn.Spawn(p,c,map);abMade.Add(p);return p;}
   foreach(var c in GenRadial.RadialCellsAround(map.Center,14f,true).Where(c=>c.Standable(map) && c.GetFirstPawn(map)==null)){if(crowd.Count>=90)break;crowd.Add(Human(c));}
   var swap=new List<Pawn>();for(int i=0;i<10;i++)swap.Add(Human(CellFinder.RandomClosewalkCellNear(map.Center,map,12)));
   double Run(){Step(120);var sw=Stopwatch.StartNew();Step(1500);sw.Stop();return sw.Elapsed.TotalMilliseconds/1500.0;}
   var demons=new List<Pawn>();Thing glyph=null;
   void On(){
    foreach(var p in swap)if(p.Spawned)p.DeSpawn();
    foreach(var k in new[]{"LP_AH_Dretch","LP_AH_Dretch","LP_AH_Hezrou","LP_AH_Barlgura","LP_AH_Succubus","LP_AH_Nabassu","LP_AH_Dretch","LP_AH_Hezrou","LP_AH_ShadowDemon","LP_AH_Dretch"}){var p=AbyssRaces.Make(k,Faction.OfPlayer,map.Tile);if(p==null)continue;GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,12),map);AbyssBinding.MakeThrall(p,GenDate.TicksPerDay*3);demons.Add(p);abMade.Add(p);}
    foreach(var p in crowd)AbyssTaint.Add(p,.15f);
    glyph=ThingMaker.MakeThing(AbyssItems.WardingGlyph);glyph.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(glyph,CellFinder.RandomClosewalkCellNear(map.Center,map,6),map);
   }
   void Off(){
    foreach(var p in demons){if(p.Destroyed)continue;p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();p.Destroy();}demons.Clear();
    foreach(var p in crowd){var h=AbyssTaint.Get(p);if(h!=null)p.health.RemoveHediff(h);}
    if(glyph!=null && !glyph.Destroyed)glyph.Destroy();
    foreach(var p in swap)if(!p.Spawned && !p.Dead)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,12),map);
   }
   Run(); // 预热（第一轮总是慢：JIT、小人刚落地找工作），不计
   var offs=new List<double>();var ons=new List<double>();
   for(int k=0;k<3;k++){offs.Add(Run());On();ons.Add(Run());Off();}
   double baseMs=offs.Average(),abyssMs=ons.Average(),delta=(abyssMs-baseMs)/Math.Max(.001,baseMs)*100.0;
   Log.Message("[LICH-PERF] abyss 1.54.0: 100 pawns; off "+string.Join(" / ",offs.Select(x=>x.ToString("0.000")))+" ms/tick, on "+string.Join(" / ",ons.Select(x=>x.ToString("0.000")))+" ms/tick; average "+baseMs.ToString("0.000")+" vs "+abyssMs.ToString("0.000")+" ("+delta.ToString("+0.0;-0.0")+"%)");
   Check(baseMs>0 && abyssMs>0,"performance pass recorded: 100 pawns, "+baseMs.ToString("0.000")+" vs "+abyssMs.ToString("0.000")+" ms per tick");
   foreach(var p in crowd.Concat(swap).ToList()){if(p.Destroyed)continue;p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();p.Destroy();}
  }
  // ---------- 存档往返 ----------
  void PrepareAbyssSave(){
   // CheckAbyss 结束时已经清理；这里重新放一组要存档的对象。
   abArena=AbFindArena();
   var elf=AbXeno("LP_Elf",AbCell(0,0));AbyssTaint.Add(elf,.2f);abTaintId=elf.thingIDNumber;abTaint=AbyssTaint.Severity(elf);
   var master=AbHuman(AbCell(1,0));var b=AbMake("LP_AH_Hezrou",AbyssRaces.Horde(),AbCell(2,0));var h=AbyssBinding.Bind(b,master,63f);abBoundId=b.thingIDNumber;abObedience=h.Severity;
   var d=AbMake("LP_AH_Dretch",AbyssRaces.Horde(),AbCell(4,0));abNamedId=d.thingIDNumber;abName=TrueNames.Of(d);
   var nab=AbMake("LP_AH_Nabassu",AbyssRaces.Horde(),AbCell(6,0));AbyssRaces.AddGorge(nab,4);abNabassuId=nab.thingIDNumber;abGorge=AbyssRaces.Gorge(nab);
   AbyssState.Get.Learn("succubus");
   var tc=Thinness.Of(map);tc.active=true;tc.value=42f;abThin=42f;
   PortalEvent.TryFindSpot(map,out var c);var rift=PortalEvent.Open(map,c,AbyssItems.AbyssPlane,1000f,false);rift.Comp.holyHits=3;abRiftId=rift.thingIDNumber;abHoly=3;
   foreach(var s in rift.Comp.spawned.ToList()){s.GetLord()?.RemovePawn(s);if(s.Spawned)s.DeSpawn();s.Destroy();}
   foreach(var l in map.lordManager.lords.Where(l=>l.faction?.def==AbyssRaces.HordeDef).ToList())map.lordManager.RemoveLord(l);
   abMade.Clear();
  }
  void CheckAbyssAfterLoad(){
   map=Find.CurrentMap;
   Pawn P(int id)=>map.mapPawns.AllPawns.FirstOrDefault(p=>p.thingIDNumber==id);
   var elf=P(abTaintId);var b=P(abBoundId);var d=P(abNamedId);var nab=P(abNabassuId);
   var rift=PortalEvent.Rifts(map).FirstOrDefault(r=>r.thingIDNumber==abRiftId);var st=AbyssState.Get;
   Check(elf!=null && Near(AbyssTaint.Severity(elf),abTaint) && b!=null && Near(AbyssBinding.Obedience(b),abObedience) && AbyssBinding.Binding(b).binder!=null && d!=null && TrueNames.Of(d)==abName && nab!=null && AbyssRaces.Gorge(nab)==abGorge && st.Known("succubus") && Near(Thinness.Get(map),abThin) && Thinness.Of(map).active && rift!=null && rift.Comp.holyHits==abHoly && rift.Comp.plane==AbyssItems.AbyssPlane,
    "abyss save/load keeps taint, obedience and binder, true names, known demons, gorge layers, thinness and the rift's holy water count");
   foreach(var p in new[]{elf,b,d,nab}.Where(p=>p!=null)){p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();p.Destroy();}
   rift?.Destroy();
  }
 }
}
