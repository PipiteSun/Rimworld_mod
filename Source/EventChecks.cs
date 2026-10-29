using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld.Planet;
using UnityEngine;
namespace LichPhylactery {
 // 1.28.0 事件的检查（需求第 4 节）：传送门失控、巨人迁徙、黄昏之年。
 public partial class LichChecks {
  readonly List<Thing> eventMade=new List<Thing>();
  static string eventRiftId,eventRiftPlane,eventVesselId;static int eventRiftWave,eventRiftNext,eventOmenStart,eventFurnace;
  static int EvNow=>Find.TickManager.TicksGame;

  Pawn EvPawn(string xeno=null,Faction f=null){
   var p=xeno==null?Person():RacePawn(xeno);if(p.Spawned)p.DeSpawn();
   p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();
   if(f!=Faction.OfPlayer)p.SetFaction(f);
   GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);eventMade.Add(p);return p;
  }
  Pawn EvKind(string kind,Faction f,IntVec3? at=null){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(kind),f,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:30,fixedChronologicalAge:30));
   foreach(var i in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(i);p.apparel?.DestroyAll();
   GenSpawn.Spawn(p,at??CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);eventMade.Add(p);return p;
  }
  static Faction Enemy=>Factions.Hostile;
  static int Count(Map m,ThingDef d)=>m.listerThings.ThingsOfDef(d).Sum(t=>t.stackCount);
  static void Flush()=>Items.State?.Flush();

  void CheckEvents(){
   bool portal=LichMod.Settings.portalBreach,mig=LichMod.Settings.giantMigration,tw=LichMod.Settings.twilightYear,van=LichMod.Settings.twilightVanilla,zero=LichMod.Settings.infusionNormalZero;int str=LichMod.Settings.twilightStrength;
   var corpsesBefore=new HashSet<Thing>(map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse));
   var emberGw=Factions.Ember?.PlayerGoodwill;worldBefore=new HashSet<Pawn>(Find.WorldPawns.AllPawnsAliveOrDead);
   try{CheckEventDefs();CheckPortal();CheckMigration();CheckTwilight();}
   finally{
    LichMod.Settings.portalBreach=portal;LichMod.Settings.giantMigration=mig;LichMod.Settings.twilightYear=tw;LichMod.Settings.twilightVanilla=van;LichMod.Settings.infusionNormalZero=zero;LichMod.Settings.twilightStrength=str;
    PortalEvent.TestRoll=null;GiantMigration.TestRoll=null;Twilight.TestRoll=1f;PortalEvent.WizardHook=wizardHookBefore;
    Twilight.End();
    foreach(var r in PortalEvent.Rifts(map).ToList())if(!r.Destroyed)r.Destroy();
    var s=PortalEvent.State;if(s!=null){foreach(var g in s.migrations.ToList()){foreach(var t in g.campThings)if(t!=null && !t.Destroyed)t.Destroy();if(g.lord!=null && map.lordManager.lords.Contains(g.lord))map.lordManager.RemoveLord(g.lord);foreach(var p in g.pawns)if(p!=null && !p.Destroyed && p.Faction!=Faction.OfPlayer){if(p.Spawned)p.DeSpawn();p.Destroy();}}s.migrations.Clear();s.lastTeleportTick=-1;}
    foreach(var t in eventMade)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    eventMade.Clear();
    foreach(var l in Find.LetterStack.LettersListForReading.OfType<Letter_GiantMigration>().ToList())Find.LetterStack.RemoveLetter(l);
    if(emberGw.HasValue && Factions.Ember!=null)Factions.SetRelation(Factions.Ember,Faction.OfPlayer,emberGw.Value);
    foreach(var m in map.mapPawns.AllPawnsSpawned.Where(p=>p.Faction==null && p.InMentalState && p.health.hediffSet.hediffs.Any(h=>h is Hediff_PlanarSpawn)).ToList())m.Destroy();
    // 事件检查里死掉的人和动物：尸体一律清掉（否则存档里留下内部角色已销毁的尸体，读档报错）。
    foreach(var corpse in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Where(t=>!corpsesBefore.Contains(t)).ToList())if(!corpse.Destroyed)corpse.Destroy();
    CleanFactionPawns();ExtinguishAll();
   }
  }
  void ExtinguishAll(){foreach(var fire in map.listerThings.ThingsOfDef(ThingDefOf.Fire).ToList())if(!fire.Destroyed)fire.Destroy();}
  static Func<Pawn,bool> wizardHookBefore=PortalEvent.WizardHook;

  void CheckEventDefs(){
   foreach(var name in new[]{"LP_PortalBreach","LP_GiantMigration","LP_TwilightOmen"}){var d=DefDatabase<IncidentDef>.GetNamedSilentFail(name);Check(d!=null && !d.ConfigErrors().Any() && Chinese(d.label),"event incident loads: "+name);}
   var portal=DefDatabase<IncidentDef>.GetNamed("LP_PortalBreach");Check(portal.category==IncidentCategoryDefOf.ThreatBig && portal.minRefireDays==20 && Near(portal.baseChance,1f) && portal.earliestDay==15,"portal breach: big threat, half a manhunter pack, 20 day refire, from day 15");
   var mig=DefDatabase<IncidentDef>.GetNamed("LP_GiantMigration");Check(mig.category==IncidentCategoryDefOf.Misc && mig.minRefireDays==25 && Near(mig.baseChance,2f),"giant migration: misc, half a traveler group, 25 day refire");
   Check(DefDatabase<IncidentDef>.GetNamed("LP_TwilightOmen").baseChance==0f,"twilight omen is only fired by the yearly roll");
   var rift=PortalEvent.RiftDef;Check(rift!=null && rift.size==new IntVec2(2,2) && rift.thingClass==typeof(Building_PlanarRift) && rift.GetStatValueAbstract(StatDefOf.MaxHitPoints)==600 && rift.GetCompProperties<CompProperties_Glower>()!=null && rift.useHitPoints && rift.building!=null && !rift.building.claimable && rift.designationCategory==null,"planar rift: 2x2, 600 hp, glows, not claimable or buildable");
   Check(ContentFinder<Texture2D>.Get(rift.graphicData.texPath,false)!=null && ContentFinder<Texture2D>.Get("Items/Materials/MiracleShard",false)!=null && ContentFinder<Texture2D>.Get("Things/Pawn/LP_DivineHalo",false)!=null && ContentFinder<Texture2D>.Get("Items/Materials/PlanarResidue/PlanarResidue_a",false)!=null,"event textures load (rift, miracle shard, halo, residue)");
   Check(DefDatabase<PortalPlaneDef>.AllDefsListForReading.Where(d=>d.randomPool).Select(d=>d.defName).OrderBy(n=>n).SequenceEqual(new[]{"LP_Plane_ElementalFire","LP_Plane_Feywild","LP_Plane_NineHells","LP_Plane_Shadowfell"}) && DefDatabase<PortalPlaneDef>.AllDefsListForReading.All(d=>!d.ConfigErrors().Any() && Chinese(d.label)),"four source planes in the portal breach pool load with Chinese names (1.54.0: the abyss plane stays out of the pool)");
   var residue=PortalEvent.ResidueDef;Check(residue!=null && Near(residue.BaseMarketValue,60) && residue.stackLimit==50 && residue.thingCategories.Any(c=>c.defName=="LP_AlchemyMaterials" && c.label=="炼金材料"),"planar residue: value 60, stack 50, alchemy materials");
   var shard=Items.Thing("LP_MiracleShard");Check(shard!=null && Near(shard.BaseMarketValue,300) && shard.stackLimit==25,"miracle shard: value 300, stack 25");
   foreach(var t in new[]{"LP_Caravan_DwarfSmith","LP_Caravan_ElfHerbalist","LP_Caravan_ChurchPriest","LP_Caravan_FaerunGoods","LP_Caravan_BlackNetwork"})Check(DefDatabase<TraderKindDef>.GetNamed(t).WillTrade(shard),"trader buys miracle shards: "+t);
   foreach(var (recipe,input,output) in new[]{("LP_BrewPotionGreaterHealingEnhanced","LP_PotionGreaterHealing","LP_PotionGreaterHealingEnhanced"),("LP_BrewPotionFireResistanceEnhanced","LP_PotionFireResistance","LP_PotionFireResistanceEnhanced")}){
    var r=DefDatabase<RecipeDef>.GetNamedSilentFail(recipe);
    Check(r!=null && r.AllRecipeUsers.Any(u=>u.defName=="LP_AlchemyTable") && r.ingredients.Any(i=>i.filter.Allows(residue) && i.GetBaseCount()==2) && r.ingredients.Any(i=>i.filter.Allows(Items.Thing(input)) && i.GetBaseCount()==1) && r.products.Single().thingDef.defName==output,"enhancement recipe: 2 residue + 1 potion at the alchemy table: "+recipe);
   }
   foreach(var k in new[]{"LP_FC_OgreWanderer","LP_FC_TitanWanderer"}){var d=DefDatabase<PawnKindDef>.GetNamedSilentFail(k);Check(d!=null && !d.ConfigErrors().Any() && new[]{Factions.ColonyDef,Factions.EmberDef}.Where(fd=>fd?.pawnGroupMakers!=null).All(fd=>fd.pawnGroupMakers.All(m=>m.options.All(o=>o.kind!=d) && (m.traders==null || m.traders.All(o=>o.kind!=d)))),"migration-only pawn kind exists outside the alliance and ember raid groups: "+k);}
   // 1.30.2：食人魔流民归余烬氏族，泰坦流浪者仍是同盟的。
   Check(DefDatabase<PawnKindDef>.GetNamed("LP_FC_OgreWanderer").defaultFactionDef==Factions.EmberDef && DefDatabase<PawnKindDef>.GetNamed("LP_FC_TitanWanderer").defaultFactionDef==Factions.ColonyDef && GiantMigration.FactionFor("ogre")==Factions.Ember,"ogre wanderers belong to the ember clan, titan wanderers to the alliance");
  }

  // ==================== 1. 传送门失控 ====================
  void CheckPortal(){
   var hellhound=DefDatabase<PawnKindDef>.GetNamed("LP_HellHound");
   // 事件可触发：选址、生成、信件。
   var parms=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig,map);var def=DefDatabase<IncidentDef>.GetNamed("LP_PortalBreach");
   Check(Traverse.Create(def.Worker).Method("CanFireNowSub",parms).GetValue<bool>(),"portal breach can fire on the home map");
   LichMod.Settings.portalBreach=false;Check(!Traverse.Create(def.Worker).Method("CanFireNowSub",parms).GetValue<bool>(),"portal breach setting turns the event off");LichMod.Settings.portalBreach=true;
   int letters=Find.LetterStack.LettersListForReading.Count;
   Check(def.Worker.TryExecute(parms),"portal breach executes");
   var rift=PortalEvent.Rifts(map).Single();var comp=rift.Comp;
   Check(Find.LetterStack.LettersListForReading.Count>letters && Find.LetterStack.LettersListForReading.Last().Label.ToString()=="传送门失控","portal breach sends a big threat letter");
   var colony=map.listerBuildings.allBuildingsColonist.Where(b=>b!=rift).Select(b=>b.Position).ToList();
   Check(rift.OccupiedRect().Cells.All(c=>!c.Roofed(map) && c.GetTerrain(map).passability!=Traversability.Impassable) && colony.All(b=>b.DistanceTo(rift.Position)>=PortalEvent.MinColonyDistance-.01f),"rift opens on open, unroofed ground at least 12 cells from colony buildings");
   Check(comp.wave==1 && comp.spawned.Count>0 && comp.nextWaveTick==EvNow+PortalEvent.WaveTicks,"first wave comes out at once and the next is 8 hours away");
   Check(Traverse.Create(def.Worker).Method("CanFireNowSub",parms).GetValue<bool>()==false,"only one rift per map");
   RiftCleanup(rift);

   // 波次倍率。
   Check(Near(PortalEvent.WaveFactor(1),1f) && Near(PortalEvent.WaveFactor(3),1.5f) && Near(PortalEvent.WaveFactor(7),2.5f) && Near(PortalEvent.WaveFactor(20),2.5f),"wave size: 1 + 0.25 per wave, capped at x2.5");
   // 吸引：传送卷轴 15 天内 ×2。
   var s=PortalEvent.State;s.lastTeleportTick=-1;bool baseAttract=PortalEvent.Attracts(map);
   PortalEvent.NoteTeleport();Check(PortalEvent.Attracts(map),"recent teleport scroll attracts rifts");
   float f1=def.Worker.ChanceFactorNow(map);s.lastTeleportTick=EvNow-16*GenDate.TicksPerDay;float f0=def.Worker.ChanceFactorNow(map);
   Check(baseAttract || Near(f1,2f*f0),"rift weight doubles after a teleport (x"+(f1/Math.Max(.0001f,f0)).ToString("0.##")+")");s.lastTeleportTick=-1;
   PortalEvent.WizardHook=p=>p.Faction==Faction.OfPlayer;Check(PortalEvent.Attracts(map) && PortalEvent.Wizard(Colonist()),"wizard path colonists attract rifts (VPE hook)");PortalEvent.WizardHook=wizardHookBefore;

   // 四个位面：第 1 波、第 3 波。
   foreach(var plane in DefDatabase<PortalPlaneDef>.AllDefsListForReading.Where(d=>d.randomPool)){ // 1.54.0 深渊位面另由 -Only Abyss 检查
    Check(PortalEvent.TryFindSpot(map,out var cell),"rift spot found for "+plane.defName);
    var r=PortalEvent.Open(map,cell,plane);var c=r.Comp;
    var w1=c.spawned.ToList();
    foreach(var sp in plane.spawns.Where(x=>x.fromWave<=1))Check(w1.Count(p=>p.kindDef==sp.kind && CompPlanarRift.Side(p)==sp.side)>=sp.count.min*(plane.spawns.Count(x=>x.kind==sp.kind && x.side==sp.side && x.fromWave<=1)) && w1.Count(p=>p.kindDef==sp.kind)<=plane.spawns.Where(x=>x.kind==sp.kind && x.fromWave<=1).Sum(x=>x.count.max),plane.defName+" wave 1 releases "+sp.kind.defName+" x"+w1.Count(p=>p.kindDef==sp.kind));
    Check(w1.All(p=>p.Spawned && p.Position.InHorDistOf(r.TrueCenter().ToIntVec3(),4f) && p.MentalStateDef==MentalStateDefOf.ManhunterPermanent),plane.defName+" wave 1 creatures are manhunting next to the rift");
    Check(plane.spawns.Where(x=>x.fromWave>=3).All(x=>w1.All(p=>p.kindDef!=x.kind || x.kind.RaceProps.Animal && plane.spawns.Any(y=>y.kind==x.kind && y.fromWave<=1))),plane.defName+" wave 1 has no upgrade units yet");
    c.SpawnWave();var w2=c.spawned.Except(w1).ToList();c.SpawnWave();var w3=c.spawned.Except(w1).Except(w2).ToList();
    Check(c.wave==3,plane.defName+" reaches wave 3");
    switch(plane.defName){
     case "LP_Plane_NineHells":
      Check(w3.Count(p=>p.kindDef==hellhound)>=3 && w3.Count(p=>p.kindDef==hellhound)<=5,"nine hells wave 3: hellhounds scaled x1.5 ("+w3.Count(p=>p.kindDef==hellhound)+")");
      if(Enemy!=null && Enemy.HostileTo(Faction.OfPlayer)){var devils=w3.Where(Fiends.IsIceDevil).ToList();Check(devils.Count==1 && devils[0].Faction==Enemy && devils[0].GetLord()?.LordJob is LordJob_AssaultColony,"nine hells wave 3 adds one ice devil raiding for Thay");c.SpawnWave();Check(c.spawned.Count(Fiends.IsIceDevil)==1,"at most one ice devil per rift");}
      break;
     case "LP_Plane_ElementalFire":
      if(Enemy!=null && Enemy.HostileTo(Faction.OfPlayer)){var g=w3.Where(p=>p.RaceProps.Humanlike).ToList();Check(g.Count==1 && g[0].Faction==Enemy && Genasi.Element(g[0])=="Fire" && Items.Has(g[0],"LP_PlanarFlameBody") && ItemDamage.Factor(g[0],DamageDefOf.Flame)==0f && g[0].GetStatValue(StatDefOf.Flammability)==0f,"elemental fire wave 3 adds a hostile fire genasi immune to burning");}
      break;
     case "LP_Plane_Feywild":
      var blink=c.spawned.Where(p=>p.kindDef.defName=="LP_BlinkDog").ToList();var disp=c.spawned.Where(p=>p.kindDef.defName=="LP_DisplacerBeast").ToList();
      Check(w1.Count(p=>p.kindDef.defName=="LP_BlinkDog")==2 && w1.Count(p=>p.kindDef.defName=="LP_DisplacerBeast")==1,"feywild wave 1: 2 blink dogs + 1 displacer beast");
      Check(w3.Count(p=>p.kindDef.defName=="LP_BlinkDog")==Mathf.RoundToInt(2*1.5f)+1 && w3.Count(p=>p.kindDef.defName=="LP_DisplacerBeast")==Mathf.RoundToInt(1.5f)+1,"feywild wave 3 adds one more of each");
      Check(blink.All(p=>CompPlanarRift.Side(p)=="wild") && disp.All(p=>CompPlanarRift.Side(p)=="rival"),"blink dogs and displacer beasts belong to rival sides");
      var a=blink[0];var b=disp[0];b.Position=a.Position+IntVec3.East;b.Notify_Teleported();
      int feuds=c.Feud();Check(feuds>0 && c.spawned.Any(p=>p.CurJob?.def==JobDefOf.AttackMelee && p.CurJob.targetA.Pawn!=null && CompPlanarRift.Side(p.CurJob.targetA.Pawn)!=CompPlanarRift.Side(p)),"feywild beasts attack each other when the rival is closer than any colonist ("+feuds+")");
      break;
     case "LP_Plane_Shadowfell":
      var hidden=w3.Where(p=>p.kindDef.defName=="LP_DisplacerBeast").ToList();
      Check(hidden.Count==1 && Items.Has(hidden[0],"LP_ShadowVeil") && hidden[0].IsPsychologicallyInvisible(),"shadowfell wave 3 adds one invisible displacer beast");
      Check(w3.Count(p=>p.kindDef.defName=="LP_WinterWolf")>=3,"shadowfell wave 3 scales winter wolves");
      break;
    }
    RiftCleanup(r);
   }

   // 计时：8 小时一波（走游戏刻）。
   {
    PortalEvent.TryFindSpot(map,out var cell);var r=PortalEvent.Open(map,cell,DefDatabase<PortalPlaneDef>.GetNamed("LP_Plane_Shadowfell"));var c=r.Comp;
    c.nextWaveTick=EvNow+1;Step(2);Check(c.wave==2 && c.nextWaveTick>EvNow+PortalEvent.WaveTicks-10,"rift timer releases the next wave after 8 hours");
    // 法师路径 ×3。
    var wiz=Colonist();PortalEvent.WizardHook=p=>p==wiz;
    var dinfo=new DamageInfo(DamageDefOf.Blunt,10f,0f,-1f,wiz);r.PreApplyDamage(ref dinfo,out bool absorbed);Check(!absorbed && Near(dinfo.Amount,30f),"wizard path colonists deal triple damage to the rift");
    var plain=new DamageInfo(DamageDefOf.Blunt,10f,0f,-1f,map.mapPawns.FreeColonistsSpawned.First(p=>p!=wiz));r.PreApplyDamage(ref plain,out _);Check(Near(plain.Amount,10f),"other attackers deal normal damage");
    PortalEvent.WizardHook=wizardHookBefore;
    // 打掉：600 生命，关闭并掉残渣，野兽退出狂暴。
    int before=Count(map,PortalEvent.ResidueDef);var beasts=c.spawned.ToList();
    r.TakeDamage(new DamageInfo(DamageDefOf.Bomb,900f,0f,-1f,wiz));
    Check(r.Destroyed && PortalEvent.Rifts(map).Count()==0,"breaking the rift (600 hp) closes it");
    int got=Count(map,PortalEvent.ResidueDef)-before;Check(got>=PortalEvent.ResidueMin && got<=PortalEvent.ResidueMax,"closed rift leaves 3-5 planar residue ("+got+")");
    Check(beasts.Where(p=>!p.Dead && p.Faction==null).All(p=>!p.InMentalState),"beasts calm down and stay as wild animals after the rift closes");
    foreach(var p in beasts)eventMade.Add(p);
   }
   // 驱散粉尘：落在 3 格内立刻关闭；4 格外不关。
   {
    PortalEvent.TryFindSpot(map,out var cell);var r=PortalEvent.Open(map,cell,DefDatabase<PortalPlaneDef>.GetNamed("LP_Plane_Shadowfell"));foreach(var p in r.Comp.spawned)eventMade.Add(p);
    int before=Count(map,PortalEvent.ResidueDef);
    ItemThrowables.Dispel(map,r.Position+new IntVec3(6,0,0));Check(!r.Destroyed,"dispel dust 5 cells away leaves the rift open");
    ItemThrowables.Dispel(map,r.Position+new IntVec3(4,0,0));Check(r.Destroyed && Count(map,PortalEvent.ResidueDef)-before>=PortalEvent.ResidueMin,"dispel dust within 3 cells closes the rift with residue");
   }
   // 巨灵命令：成功一次、失败一次（固定随机），每个裂口只能试一次。
   {
    // 1.63.0：最后一条“只有巨灵能命令”用的是这个人，所以要挑一个本身不能命令裂口的殖民者（随机开局里偶尔就是巨灵 / 六臂蛇人）。
    var genie=map.mapPawns.FreeColonistsSpawned.First(p=>!Necro.Undead(p) && !p.skills.GetSkill(SkillDefOf.Social).TotallyDisabled && !PortalEvent.CanCommand(p));var social=genie.skills.GetSkill(SkillDefOf.Social);int oldSocial=social.Level;
    social.Level=10;Check(Near(CompPlanarRift.GenieChance(genie),.6f),"genie command chance is social x6%");social.Level=20;Check(Near(CompPlanarRift.GenieChance(genie),.8f),"genie command chance is capped at 80%");social.Level=10;
    PortalEvent.TryFindSpot(map,out var cell);var r=PortalEvent.Open(map,cell,DefDatabase<PortalPlaneDef>.GetNamed("LP_Plane_Shadowfell"));foreach(var p in r.Comp.spawned)eventMade.Add(p);
    PortalEvent.TestRoll=.59f;int before=Count(map,PortalEvent.ResidueDef);Check(r.Comp.GenieCommand(genie) && r.Destroyed && Count(map,PortalEvent.ResidueDef)>before,"genie command succeeds under its chance and closes the rift");
    PortalEvent.TryFindSpot(map,out cell);r=PortalEvent.Open(map,cell,DefDatabase<PortalPlaneDef>.GetNamed("LP_Plane_Shadowfell"));
    PortalEvent.TestRoll=.61f;int wave=r.Comp.wave;Check(!r.Comp.GenieCommand(genie) && !r.Destroyed && r.Comp.wave==wave+1 && r.Comp.genieTried,"failed genie command releases the next wave early");
    Check(!r.Comp.GenieCommand(genie) && r.Comp.wave==wave+1,"each rift can only be commanded once");
    Check(!r.GetFloatMenuOptions(genie).Any(o=>o.Label.Contains("命令裂口")),"only genies get the command option");
    PortalEvent.TestRoll=null;social.Level=oldSocial;
    // 3 天崩溃：第 9 波之后放出 ×2 再自己关上（不掉残渣）。
    foreach(var p in r.Comp.spawned)eventMade.Add(p);
    r.Comp.wave=PortalEvent.CollapseAfterWave;r.Comp.nextWaveTick=EvNow+1;int had=r.Comp.spawned.Count;before=Count(map,PortalEvent.ResidueDef);var lettersBefore=Find.LetterStack.LettersListForReading.Count;
    bool Beast(Pawn p)=>!p.Dead && p.Faction==null && !p.RaceProps.Humanlike;
    var oldBeasts=r.Comp.spawned.Where(Beast).ToList();var es=PortalEvent.State;es.pendingCalms.Clear();
    Step(2);int collapseTick=EvNow;
    int burst=r.Comp.spawned.Count-had;
    Check(r.Destroyed && burst>=2*Mathf.RoundToInt(2*PortalEvent.WaveFactor(9)) && Count(map,PortalEvent.ResidueDef)==before && Find.LetterStack.LettersListForReading.Last().Label.ToString()=="裂口崩溃","after wave 9 the rift collapses: double burst ("+burst+"), no residue, collapse letter");
    foreach(var p in r.Comp.spawned)eventMade.Add(p);
    // 1.30.2：崩溃时之前各波的野兽立即平静；最后一波照常狂暴，1 小时后平静。
    Check(oldBeasts.Where(Beast).All(p=>!p.InMentalState),"older waves' beasts calm down when the rift collapses ("+oldBeasts.Count+")");
    var lastWave=r.Comp.spawned.Skip(had).Where(Beast).ToList();
    Check(es.pendingCalms.Count==1 && es.pendingCalms[0].pawns.Count==lastWave.Count && lastWave.All(p=>es.pendingCalms[0].pawns.Contains(p)) && Math.Abs(es.pendingCalms[0].tick-(collapseTick+GenDate.TicksPerHour))<=2 && lastWave.Where(p=>p.Spawned).All(p=>p.InMentalState),"the last wave stays enraged and is queued to calm 1 hour after the collapse ("+lastWave.Count+")");
    Check(PortalEvent.TickCalms(es)==0 && es.pendingCalms.Count==1,"the last wave does not calm before the hour is up");
    es.pendingCalms[0].tick=EvNow;Step(250);
    Check(es.pendingCalms.Count==0 && lastWave.Where(Beast).All(p=>!p.InMentalState),"the last wave calms down 1 hour after the rift is gone");
   }
   // 强化药水：持续时间 ×2。
   {
    var p=EvPawn();
    ItemPotions.Apply(p,Items.Thing("LP_PotionGreaterHealingEnhanced"),"greaterheal",null,2f);int heal=Items.TicksLeft(p,"LP_PotionHealing");
    ItemPotions.Apply(p,Items.Thing("LP_PotionFireResistanceEnhanced"),"give",Items.Hediff("LP_PotionFireResistance"),2f);int fire=Items.TicksLeft(p,"LP_PotionFireResistance");
    Check(heal==30000 && fire==120000,"enhanced potions last twice as long ("+heal+", "+fire+")");
    var doer=Items.Thing("LP_PotionFireResistanceEnhanced").ingestible.outcomeDoers.OfType<IngestionOutcomeDoer_LPPotion>().Single();Check(Near(doer.durationFactor,2f) && doer.hediff.defName=="LP_PotionFireResistance","enhanced fire resistance potion uses the same effect with x2 duration");
   }
  }
  void RiftCleanup(Building_PlanarRift r){foreach(var p in r.Comp.spawned)eventMade.Add(p);if(!r.Destroyed)r.Destroy();}

  // ==================== 2. 巨人迁徙 ====================
  void CheckMigration(){
   var ember=Factions.Ember;var colony=Factions.Colony;
   if(ember!=null)Factions.SetRelation(ember,Faction.OfPlayer,0);
   Check(GiantMigration.CanFire(map),"giant migration can fire with friendly ember clan and alliance");
   // 讨食营养值：火巨人×2、泰坦×2.5、食人魔×1.6，两天的量。
   var fg=EvPawn("LP_FireGiant");var ti=EvPawn("LP_Titan");var og=EvPawn("LP_Ogre");var hu=EvPawn();
   Check(Near(GiantMigration.Need(new[]{fg}),6.4f) && Near(GiantMigration.Need(new[]{ti}),8f) && Near(GiantMigration.Need(new[]{og}),5.12f) && Near(GiantMigration.Need(new[]{hu}),3.2f),"food asked: fire giant 6.4, titan 8, ogre 5.12, human 3.2 nutrition");
   foreach(var x in new[]{fg,ti,og,hu}){x.Destroy();}
   // 三种队伍各一次。
   var groups=new Dictionary<string,GiantGroup>();
   foreach(var kind in new[]{"ember","ogre","titan"}){
    if(!GiantMigration.Available(kind)){Log.Message("[LICH-TEST] skip migration "+kind+" (faction unavailable)");continue;}
    var g=GiantMigration.Start(map,kind);Check(g!=null && g.lord?.LordJob is LordJob_GiantMigration && g.Origin==GiantMigration.FactionFor(kind) && g.pawns.All(p=>p.Spawned && p.Faction==(GiantMigration.Wanderers()??GiantMigration.FactionFor(kind))),"migration group arrives: "+kind);
    groups[kind]=g;
    switch(kind){
     case "ember":{int giants=g.pawns.Count(FireGiant.Active),slaves=g.pawns.Count-giants;Check(giants>=1 && giants<=2 && slaves>=2 && slaves<=3 && g.pawns.Where(p=>!FireGiant.Active(p)).All(p=>p.kindDef.defName=="LP_EC_HumanSlave" || p.kindDef.defName=="LP_EC_OrcSlave"),"ember group: 1-2 fire giants + 2-3 slaves ("+giants+"+"+slaves+")");break;}
     case "ogre":Check(g.pawns.Count>=2 && g.pawns.Count<=3 && g.pawns.All(Races124.IsOgre) && g.Origin==ember,"ogre family: 2-3 ogres of the ember clan");break;
     case "titan":Check(g.pawns.Count(Races124.IsTitan)==1 && g.pawns.Count==3 && g.Origin==colony,"titan group: 1 titan + 2 followers of the alliance");break;
    }
   }
   if(!groups.TryGetValue("ember",out var eg))eg=groups.Values.First();
   // 扎营：篝火、睡袋、讨食信。
   int letters=Find.LetterStack.LettersListForReading.Count;
   GiantMigration.OnCamp(eg);
   Check(eg.camped && eg.campThings.Any(t=>t.def==ThingDefOf.Campfire && t.Spawned) && eg.campThings.Count(t=>t.def==ThingDefOf.Bedroll)>=1 && eg.campThings.All(t=>t.Position.InHorDistOf(eg.camp,6.5f)),"camp: campfire and bedrolls at the camp spot ("+string.Join(",",eg.campThings.Select(t=>t.def.defName+"@"+t.Position))+" camp "+eg.camp+")");
   var food=Find.LetterStack.LettersListForReading.OfType<Letter_GiantMigration>().LastOrDefault();
   Check(food!=null && food.kind=="food" && food.group==eg && Near(eg.need,GiantMigration.Need(eg.Alive)) && food.Text.ToString().Contains(eg.need.ToString("0.#")),"hungry giants letter names the nutrition needed ("+eg.need.ToString("0.#")+")");
   Check(eg.camp.InBounds(map) && map.listerBuildings.allBuildingsColonist.All(b=>b.Position.DistanceTo(eg.camp)>=GiantMigration.CampMinColonyDistance-7),"camp is away from the colony");
   // 种族心情：殖民地火巨人 +3；余烬火巨人在有巨魔时厌恶巨魔。
   if(eg.kind=="ember"){
    var mine=EvPawn("LP_FireGiant",Faction.OfPlayer);var troll=EvPawn("LP_Troll",Faction.OfPlayer);ColonyCensus.Dirty();
    Check(DefDatabase<ThoughtDef>.GetNamed("LP_ClanPassing").Worker.CurrentState(mine).Active,"colony fire giant is glad to see the clan passing");
    var visitor=eg.pawns.First(FireGiant.Active);Check(DefDatabase<ThoughtDef>.GetNamed("LP_GiantHatesTrolls").Worker.CurrentState(visitor).Active,"ember fire giant hates the colony's trolls");
    troll.Destroy();ColonyCensus.Dirty();Check(!DefDatabase<ThoughtDef>.GetNamed("LP_GiantHatesTrolls").Worker.CurrentState(visitor).Active,"no troll, no hatred");
   }
   // 给食物：扣除正确、好感 +12、离开时留礼物。
   var zone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);map.zoneManager.RegisterZone(zone);
   foreach(var zc in GenRadial.RadialCellsAround(map.Center,30f,true).Where(x=>x.InBounds(map) && x.Standable(map) && !x.Roofed(map) && map.zoneManager.ZoneAt(x)==null && x.GetEdifice(map)==null && x.GetFirstItem(map)==null && x.GetFirstPawn(map)==null).Take(9).ToList())zone.AddCell(zc);
   Check(zone.Cells.Count>0,"test stockpile created");
   Thing pem=ThingMaker.MakeThing(ThingDefOf.Pemmican);pem.stackCount=pem.def.stackLimit;GenPlace.TryPlaceThing(pem,zone.Cells.First(),map,ThingPlaceMode.Direct,out pem);eventMade.Add(pem);
   Check(pem.IsInAnyStorage() && GiantMigration.StoredFood(map).First()==pem,"pemmican in a stockpile is the first food given");
   eg.need=2f;float stored=GiantMigration.StoredNutrition(map);int gw=eg.Origin.PlayerGoodwill;
   var tooMuch=new GiantGroup{map=map,need=stored+50f};Check(GiantMigration.TakeFood(map,tooMuch.need)==0f && Near(GiantMigration.StoredNutrition(map),stored),"not enough food: nothing is taken");
   Check(GiantMigration.Feed(eg) && eg.fed,"giving food works");
   float taken=stored-GiantMigration.StoredNutrition(map);float per=ThingDefOf.Pemmican.GetStatValueAbstract(StatDefOf.Nutrition);
   Check(taken>=2f-.001f && taken<2f+per+.001f && !pem.Destroyed && pem.stackCount==pem.def.stackLimit-Mathf.CeilToInt(2f/per-.0001f),"food deducted matches the request ("+taken.ToString("0.##")+")");
   Check(eg.Origin.PlayerGoodwill==Math.Min(100,gw+GiantMigration.FedGoodwill),"fed giants: goodwill +12 to their home faction ("+eg.Origin?.Name+")");
   Check(!GiantMigration.Feed(eg),"giants can only be fed once");
   var nearBefore=GenRadial.RadialCellsAround(eg.camp,6f,true).Where(c=>c.InBounds(map)).SelectMany(c=>c.GetThingList(map)).Where(t=>t.def.category==ThingCategory.Item).ToList();
   GiantMigration.TestRoll=.99f;GiantMigration.OnLeave(eg);
   var gifts=GenRadial.RadialCellsAround(eg.camp,6f,true).Where(c=>c.InBounds(map)).SelectMany(c=>c.GetThingList(map)).Where(t=>t.def.category==ThingCategory.Item && !nearBefore.Contains(t)).ToList();eventMade.AddRange(gifts);
   var giftNames=gifts.Select(t=>t.def.defName).ToList();
   Check(eg.left && eg.campThings.Count==0 && (eg.kind=="ember"?giftNames.Any(n=>n=="LP_LavaCrystal" || n.StartsWith("LP_FireGiant")):eg.kind=="ogre"?giftNames.Contains("LP_OgreClub"):giftNames.Contains("LP_MiracleShard")),"fed giants pack up and leave a gift: "+string.Join(",",giftNames));
   Check(GiantMigration.Gifts("ogre").Single().def.defName=="LP_OgreClub" && GiantMigration.Gifts("titan").Any(t=>t.def.defName=="LP_MiracleShard") && GiantMigration.Gifts("titan").Any(t=>t.def.defName.StartsWith("LP_Scroll")),"ogre gift is a club; titan gift is a scroll and a miracle shard");
   // 不给：15% 夜里偷粮。
   var g2=groups.Values.FirstOrDefault(x=>x!=eg)??GiantMigration.Start(map,eg.kind);GiantMigration.OnCamp(g2);
   GiantMigration.TestRoll=.2f;GiantMigration.Refuse(g2);Check(g2.answered && g2.theftTick<0,"refused giants above 15% do not steal");
   g2.answered=false;GiantMigration.TestRoll=.1f;GiantMigration.Refuse(g2);Check(g2.theftTick>EvNow && GenLocalDate.HourOfDay(map)+(g2.theftTick-EvNow)/GenDate.TicksPerHour>=22,"refused giants plan a night theft");
   g2.theftTick=EvNow;GiantMigration.Tick(PortalEvent.State);
   Check(g2.theftTick<0 && g2.pawns.Any(p=>p.CurJob?.def==JobDefOf.HaulToCell && g2.stolen.Contains(p.CurJob.targetA.Thing)),"theft: a giant hauls a stack of food toward the camp"+" (stolen "+g2.stolen.Count+", jobs "+string.Join(",",g2.pawns.Select(p=>p.CurJob?.def?.defName??"none"))+")");
   // 留下居住：接受（加入、余烬好感 -5）、拒绝。
   GiantMigration.TestRoll=0f;
   var g3=groups.TryGetValue("ember",out var e3) && e3!=eg?e3:GiantMigration.Available("ember")?GiantMigration.Start(map,"ember"):null;
   if(g3!=null){
    GiantMigration.OnCamp(g3);int egw=ember.PlayerGoodwill;var stay=GiantMigration.TryAskStay(g3);
    Check(stay!=null && stay.kind=="stay" && GiantMigration.IsGiant(g3.stayer),"a giant (not a slave) asks to stay");
    Check(GiantMigration.Join(g3) && g3.stayer.Faction==Faction.OfPlayer && g3.stayer.GetLord()==null && ember.PlayerGoodwill==egw+GiantMigration.TakenKinGoodwill,"accepting: the giant joins and the ember clan loses 5 goodwill");
    eventMade.Add(g3.stayer);
   }
   var g4=GiantMigration.Start(map,GiantMigration.Available("ogre")?"ogre":eg.kind);GiantMigration.OnCamp(g4);var ask=GiantMigration.TryAskStay(g4);
   Check(ask!=null,"stay letter sent for the refusal test");
   if(g4.kind=="ogre")Check(ask.relatedFaction==ember && ask.Text.ToString().Contains("余烬氏族不高兴族人被带走：好感 "+GiantMigration.TakenKinGoodwill),"an ogre who stays costs ember clan goodwill (letter says -5)");
   var reject=ask.Choices.First(o=>Traverse.Create(o).Field("text").GetValue<string>()=="拒绝");reject.action();
   Check(g4.stayer.Faction!=Faction.OfPlayer && g4.stayer.GetLord()==g4.lord,"refusing: the giant stays with the group, which leaves");
   GiantMigration.TestRoll=null;
   var fake=new GiantGroup{fed=false};Check(Near(GiantMigration.StayChanceFor(fake,g4.stayer),GiantMigration.KinInColony(g4.stayer)?.6f:.35f),"stay chance 35% (60% with kin)");fake.fed=true;Check(Near(GiantMigration.StayChanceFor(fake,g4.stayer),(GiantMigration.KinInColony(g4.stayer)?.6f:.35f)+.2f),"feeding them adds 20%");
   // 敌对派系不触发。
   if(ember!=null && colony!=null){
    int cg=colony.PlayerGoodwill;Factions.SetRelation(ember,Faction.OfPlayer,-80);Factions.SetRelation(colony,Faction.OfPlayer,-80);
    Check(!GiantMigration.CanFire(map) && !GiantMigration.Available("ember") && !GiantMigration.Available("ogre"),"hostile factions send no migrants");
    Factions.SetRelation(colony,Faction.OfPlayer,cg);Factions.SetRelation(ember,Faction.OfPlayer,0);
   }
   LichMod.Settings.giantMigration=false;Check(!GiantMigration.CanFire(map),"migration setting turns the event off");LichMod.Settings.giantMigration=true;
   zone.Delete();
  }

  // ==================== 3. 黄昏之年 ====================
  void CheckTwilight(){
   Twilight.TestRoll=1f;
   var deities=Twilight.Deities;
   Check(deities.Count==8 && deities.Select(d=>d.degree).SequenceEqual(Enumerable.Range(1,8)) && deities.All(d=>Chinese(d.label) && !d.key.NullOrEmpty() && Chinese(d.description)),"eight deities load with degrees 1-8");
   var trait=Twilight.Trait;
   Check(trait!=null && trait.GetGenderSpecificCommonality(Gender.Male)==0f && trait.degreeDatas.Count==8 && deities.All(d=>trait.DataAtDegree(d.degree).label=="神力灌注（"+d.label+"）"),"divine infusion trait: 8 degrees named after the gods, commonality 0");
   Check(Twilight.VesselDef?.hediffClass==typeof(Hediff_DivineVessel) && Twilight.VesselDef.RenderNodeProperties?.Any(r=>r.nodeClass==typeof(PawnRenderNode_DivineHalo))==true && Twilight.Condition?.conditionClass==typeof(GameCondition_TwilightYear),"vessel hediff with tinted halo, twilight world condition");
   // 随机殖民者不带特性。
   int carried=0;for(int i=0;i<40;i++){var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false));if(p.story.traits.HasTrait(trait))carried++;p.Destroy();}
   Check(carried==0,"40 random colonists never roll divine infusion");
   // 条件开关、天色、前 3 天心情、D。
   Check(!Twilight.Active && Near(Twilight.D,1f),"no twilight: D = 1");
   var cond=Twilight.Start(45);Check(Twilight.Active && cond.Duration==45*GenDate.TicksPerDay && cond.SkyTarget(map).HasValue && cond.SkyTarget(map).Value.colors.sky.r>cond.SkyTarget(map).Value.colors.sky.b,"twilight year starts for the set days with a dark red sky");
   Check(Near(Twilight.D,1.5f) && Near(Twilight.InfusionChance,.15f),"twilight: D = 1.5, infusion 15%");
   LichMod.Settings.twilightStrength=200;Check(Near(Twilight.D,3f) && Near(Twilight.InfusionChance,.3f),"twilight strength 200% doubles D and chances");LichMod.Settings.twilightStrength=100;
   var col=Colonist();var dread=DefDatabase<ThoughtDef>.GetNamed("LP_TwilightDread");
   Check(dread.Worker.CurrentState(col).Active && Near(dread.stages[0].baseMoodEffect,-3f),"ominous twilight: colonists -3 mood during the first 3 days");
   cond.startTick-=4*GenDate.TicksPerDay;Check(!dread.Worker.CurrentState(col).Active,"ominous mood ends after 3 days");cond.startTick+=4*GenDate.TicksPerDay;
   Check(Twilight.End() && !Twilight.Active,"twilight year can end");
   Check(Near(Twilight.InfusionChance,.03f),"normal infusion chance 3%");LichMod.Settings.infusionNormalZero=true;Check(Twilight.InfusionChance==0f,"setting can make normal infusion 0");LichMod.Settings.infusionNormalZero=false;
   var omen=DefDatabase<IncidentDef>.GetNamed("LP_TwilightOmen");var wp=new IncidentParms{target=Find.World};
   LichMod.Settings.twilightYear=false;Check(!Traverse.Create(omen.Worker).Method("CanFireNowSub",wp).GetValue<bool>(),"twilight setting stops the omen");LichMod.Settings.twilightYear=true;
   Check(Traverse.Create(omen.Worker).Method("CanFireNowSub",wp).GetValue<bool>(),"omen can fire");
   var world=Twilight.World;world.startTick=-1;Twilight.Omen();Check(world.startTick==EvNow+15*GenDate.TicksPerDay,"omen: twilight starts in 15 days");
   world.startTick=EvNow;WorldTick311(world);
   Check(Twilight.Active && world.startTick<0,"countdown reaching zero starts the twilight year");Twilight.End();
   int year=GenDate.YearsPassed;world.rolledYear=year-1;Twilight.TestRoll=0f;WorldTick311(world);
   Check(year>=1?world.startTick>0 && world.rolledYear==year:world.startTick<0,"yearly roll happens once a year from year 2 (year "+year+")");world.startTick=-1;Twilight.TestRoll=1f;

   CheckInfusions();CheckGenerationHook();CheckVessels();
  }

  void CheckInfusions(){
   Twilight.TestRoll=0f;
   var foes=new List<Pawn>();
   foreach(var d in Twilight.Deities){
    var p=EvPawn(null,Enemy??Faction.OfPlayer);float mind=DNDMind.Resistance(p),speed=p.GetStatValue(StatDefOf.MoveSpeed);
    Twilight.Infuse(p,d);Check(Twilight.Infused(p)==d && p.story.traits.GetTrait(Twilight.Trait).Label=="神力灌注（"+d.label+"）","infusion trait for "+d.label);
    switch(d.key){
     case "Bane":Check(Near(DNDMind.Resistance(p)-mind,.2f),"Bane infusion: mind resistance +20%");break;
     case "Surtr":Check(p.GetStatValue(StatDefOf.Flammability)==0f && Near(TwilightSkills.DamageFactor(p,new DamageInfo(DamageDefOf.Flame,10f)),.5f),"Surtr infusion: fire damage halved, cannot catch fire");break;
     case "Ilsvi":Check(Near(DNDMind.Resistance(p)-mind,.3f) && Fiends.Sees(p),"Ilsvi infusion: mind resistance +30%, sees the psychically hidden");break;
     case "Tyr":Check(Fiends.Sees(p),"Tyr infusion: sees through invisibility");break;
     case "Lathander":Check(Items.Has(p,"LP_DawnChild"),"Lathander infusion carries the dawn blessing");break;
     case "Vlaakith":Check(Near(Races124Cooldowns.Factor(p,DefDatabase<AbilityDef>.GetNamed("LP_AstralStep")),.5f),"Vlaakith infusion: astral step cooldown halved");break;
     case "Talos":Check(TwilightTalosEmpProbe(p),"Talos infusion: EMP does not stun");break;
     case "Malar":Check(Twilight.Trait.DataAtDegree(d.degree).statFactors.Any(s=>s.stat==StatDefOf.MoveSpeed && Near(s.value,1.1f)) && p.GetStatValue(StatDefOf.MoveSpeed)>speed && !Traverse.Create(new StatPart_Glow()).Method("ActiveFor",new Type[]{typeof(Thing)},new object[]{p}).GetValue<bool>(),"Malar infusion: +10% speed, ignores darkness");break;
    }
    foes.Add(p);
   }
   // 触发（固定随机 0）。
   Pawn Foe(string key)=>foes.First(p=>Twilight.InfusedBy(p,key));
   var target=EvPawn();
   TwilightSkills.OnMeleeHit(Foe("Bane"),target);Check(Items.Has(target,"LP_BaneDread"),"Bane melee hit: target dreads");
   var rifle=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"));target.equipment.AddEquipment((ThingWithComps)rifle);
   Check(!target.equipment.PrimaryEq.PrimaryVerb.TryStartCastOn(new LocalTargetInfo(Foe("Bane"))),"dreading pawns cannot shoot");
   target.equipment.DestroyAllEquipment();Items.Remove(target,"LP_BaneDread");
   float inj=target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity);
   TwilightSkills.OnMeleeHit(Foe("Surtr"),target);Check(target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(h=>h.def.defName=="Burn") && (target.IsBurning() || target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h=>h.Severity)>inj),"Surtr melee hit: extra burn and fire");
   ExtinguishAll();
   float hp=target.health.summaryHealth.SummaryHealthPercent;int bites=target.health.hediffSet.hediffs.Count(h=>h.def==HediffDefOf.Bite || h is Hediff_MissingPart);
   TwilightSkills.OnMeleeHit(Foe("Malar"),target);Check(target.health.summaryHealth.SummaryHealthPercent<hp && target.health.hediffSet.hediffs.Count(h=>h.def==HediffDefOf.Bite || h is Hediff_MissingPart)>bites,"Malar melee hit: bleeding bite");
   var ilsvi=Foe("Ilsvi");TwilightSkills.OnDowned(ilsvi,new DamageInfo(DamageDefOf.Cut,1f,0f,-1f,target));Check(target.stances.stunner.Stunned,"Ilsvi infused knocked down: psychic shock stuns the attacker");
   var tyr=Foe("Tyr");var sinner=EvPawn();sinner.records.AddTo(RecordDefOf.KillsHumanlikes,5);
   Check(Near(TwilightSkills.DamageFactor(sinner,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,tyr)),1.1f) && Near(TwilightSkills.DamageFactor(target,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,tyr)),1f) && TwilightSkills.Crimes(sinner)>=5,"Tyr infusion: +10% against sinners only");
   var lath=Foe("Lathander");var undead=map.mapPawns.AllPawnsSpawned.FirstOrDefault(p=>Necro.Construct(p)!=null);
   if(undead!=null)Check(Near(TwilightSkills.DamageFactor(undead,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,lath)),1.25f) && Crimesish(undead),"Lathander infusion: +25% against the undead");
   DownTorso(lath);Flush();Check(!lath.Dead && !lath.Downed && ((Hediff_DawnChild)lath.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_DawnChild"))).risen,"Lathander infusion: first downing, rises again");
   DownTorso(lath);Flush();Check(lath.Downed,"Lathander infusion rises only once");
   var vl=Foe("Vlaakith");var from=vl.Position;var neck=vl.RaceProps.body.AllParts.First(b=>b.def.defName=="Neck");
   vl.TakeDamage(new DamageInfo(DamageDefOf.Cut,99999f,999f,-1f,target,neck));Flush();
   Check(!vl.Dead && vl.Position!=from && vl.Position.DistanceTo(from)>=TwilightSkills.DodgeRange-2.01f,"Vlaakith infusion: astral dodge escapes a lethal blow");
   Twilight.TestRoll=1f;
   // 灌注者都在地图上会影响后面的附体检查（例如班恩灌注者让附近同伴不逃跑）：清掉。
   foreach(var p in foes.Concat(new[]{target,sinner}))if(!p.Destroyed){if(p.Spawned)p.DeSpawn();p.Destroy();}
  }
  // 世界组件每小时第 311 刻检查一次：把时间拨到下一个这样的刻再调用。
  static void WorldTick311(WorldComponent_Twilight w){int add=(311-EvNow%GenDate.TicksPerHour+GenDate.TicksPerHour)%GenDate.TicksPerHour;if(add>0)Find.TickManager.DebugSetTicksGame(EvNow+add);w.WorldComponentTick();}
  static IEnumerable<Pawn> Enemies(Pawn v)=>TwilightSkills.Enemies(v,TwilightSkills.SkillRange).Where(x=>x.RaceProps.Humanlike);
  // 只用钝击打躯干直到倒地：不会打断四肢（随机部位可能打掉一条腿，爬不起来）。
  static void DownTorso(Pawn p)=>HealthUtility.DamageUntilDowned(p,false,DamageDefOf.Blunt,null,DefDatabase<BodyPartGroupDef>.GetNamed("Torso"));
  static string Diag(Pawn p)=>" [dead="+p.Dead+" downed="+p.Downed+" vessel="+(Twilight.Vessel(p)!=null)+" revived="+Twilight.Vessel(p)?.revived+" pain="+p.health.hediffSet.PainTotal.ToString("0.##")+" conscious="+p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness).ToString("0.##")+" moving="+p.health.capacities.GetLevel(PawnCapacityDefOf.Moving).ToString("0.##")+" hediffs="+string.Join(",",p.health.hediffSet.hediffs.Select(h=>h.def.defName+(h.Part!=null?"@"+h.Part.def.defName:"")))+"]";
  static bool Crimesish(Pawn p)=>TwilightSkills.Crimes(p)==10;
  static bool TwilightTalosEmpProbe(Pawn p){var dinfo=new DamageInfo(DamageDefOf.EMP,30f);p.stances.stunner.Notify_DamageApplied(dinfo);return !p.stances.stunner.Stunned;}

  void CheckGenerationHook(){
   Twilight.TestRoll=0f;
   var paths=new List<(Faction f,string key)>{(Factions.Hostile,"Bane"),(Factions.Ember,"Surtr"),(Factions.Illithid,"Ilsvi"),(Factions.EnsureHidden(Factions.ZealotsDef),"Tyr")};
   foreach(var (f,key) in paths){
    if(f==null){Log.Message("[LICH-TEST] skip twilight hook for missing faction "+key);continue;}
    var list=FactionGroup(f,PawnGroupKindDefOf.Combat,600);
    var eligible=list.Where(Twilight.Eligible).ToList();
    Check(eligible.Count>0 && eligible.All(p=>Twilight.Infused(p)!=null && (Twilight.Infused(p).key==key || p.kindDef.defName=="LP_FH_GithMercenary" && Twilight.Infused(p).key=="Vlaakith")),"raid generation infuses "+f.def.defName+" raiders with "+key+" ("+eligible.Count+")");
   }
   var gith=FactionPawn("LP_FH_GithMercenary",Factions.Hostile);Check(Twilight.DeityFor(gith,Factions.Hostile)?.key=="Vlaakith","githyanki mercenaries serve Vlaakith even in the Thayan raid (pawn kind first)");
   if(Factions.Colony!=null)Check(Twilight.DeityFor(null,Factions.Colony)==null || Factions.Colony.HostileTo(Faction.OfPlayer),"Lathander only answers the alliance when it is hostile");
   var pirate=Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x=>x.def.defName=="Pirate");var piratesExt=DefDatabase<FactionDef>.GetNamed("Pirate").GetModExtension<DeityExtension>();
   Check(piratesExt?.deity?.key=="Talos" && piratesExt.vanilla && DefDatabase<FactionDef>.GetNamed("TribeSavage").GetModExtension<DeityExtension>()?.deity?.key=="Malar","vanilla pirates and savage tribes map to Talos and Malar");
   if(pirate!=null){
    LichMod.Settings.twilightVanilla=false;var pl=FactionGroup(pirate,PawnGroupKindDefOf.Combat,600);Check(pl.All(p=>Twilight.Infused(p)==null),"vanilla toggle off: pirates carry no divine power");
    LichMod.Settings.twilightVanilla=true;var pl2=FactionGroup(pirate,PawnGroupKindDefOf.Combat,600);Check(pl2.Where(Twilight.Eligible).All(p=>Twilight.InfusedBy(p,"Talos")),"vanilla toggle on: pirates are infused by Talos");LichMod.Settings.twilightVanilla=false;
   }
   // 2000 点以上、黄昏之年：领队附体；点数裁剪。
   Twilight.Start(10);
   Twilight.TestRoll=1f;var plain=FactionGroup(Factions.Hostile,PawnGroupKindDefOf.Combat,2400);Twilight.TestRoll=0f;
   float sum=plain.Sum(p=>p.kindDef.combatPower),maxOne=plain.Max(p=>p.kindDef.combatPower);int n0=plain.Count;
   var big=Twilight.Process(plain.ToList(),new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,faction=Factions.Hostile,points=2400,tile=map.Tile}).ToList();var vessels=big.Where(p=>Twilight.Vessel(p)!=null).ToList();
   Check(vessels.Count==1 && Twilight.Infused(vessels[0])!=null,"2000+ point twilight raid: exactly one leader becomes a vessel ("+vessels.FirstOrDefault()?.kindDef.defName+")");
   float Weighted(List<Pawn> l)=>l.Sum(p=>p.kindDef.combatPower*(1f+(Twilight.Infused(p)!=null?Twilight.InfusedPower:0f)+(Twilight.Vessel(p)!=null?Twilight.VesselPower:0f)));
   Check(big.Count<n0 && Weighted(big)<=sum+maxOne*(1f+Twilight.InfusedPower+Twilight.VesselPower)+.01f,"infused raids are trimmed to their points: "+big.Count+" of "+n0+" raiders, weighted "+Weighted(big).ToString("0")+" vs "+sum.ToString("0"));
   // 裁剪函数本身：灌注者 ×1.3 的战力从末尾的普通成员里扣。
   var dummy=Enumerable.Range(0,5).Select(i=>FactionPawn("LP_FH_OrcWarrior",Factions.Hostile)).ToList();var inf=new HashSet<Pawn>{dummy[0],dummy[4]};float power=dummy[0].kindDef.combatPower;var keep0=dummy[0];var inf4=dummy[4];
   int removed=Twilight.Trim(dummy,inf,null,power*1.5f);Check(removed==2 && dummy.Count==3 && dummy.Contains(keep0) && dummy.Contains(inf4),"trim removes trailing ordinary raiders first ("+removed+")");
   var two=new List<Pawn>{keep0,inf4};removed=Twilight.Trim(two,inf,keep0,power*1.2f);Check(removed==1 && two.Count==1 && two[0]==keep0,"then infused raiders (worth x1.3), never the vessel");
   Twilight.End();Twilight.TestRoll=1f;
  }

  // 1.63.0：10 格内没有建筑、没有人、没起雾的空地；里面的树砍掉、水面改成土，保证能站、视线通畅、能放灰（测试地图是随机的，有的地图找不到天然的大块干地）。
  IntVec3 DryOpenCell(float radius){
   bool Clear(IntVec3 c)=>GenRadial.RadialCellsAround(c,radius,true).All(x=>x.InBounds(map) && !x.Fogged(map) && x.GetFirstPawn(map)==null && x.GetEdifice(map)==null && x.GetThingList(map).All(th=>th is Plant || th.def.passability==Traversability.Standable));
   if(!CellFinder.TryFindRandomCellNear(map.Center,map,50,Clear,out var cell))cell=map.AllCells.Where(c=>c.x>radius && c.z>radius && c.x<map.Size.x-radius && c.z<map.Size.z-radius).OrderBy(c=>c.DistanceToSquared(map.Center)).First(Clear);
   foreach(var x in GenRadial.RadialCellsAround(cell,radius,true)){foreach(var pl in x.GetThingList(map).OfType<Plant>().Where(pl=>pl.def.passability!=Traversability.Standable || pl.def.blockWind).ToList())pl.Destroy();if(x.GetTerrain(map).IsWater || !x.Standable(map))map.terrainGrid.SetTerrain(x,TerrainDefOf.Soil);}
   return cell;
  }
  void CheckVessels(){
   var enemy=Enemy;if(enemy==null || !enemy.HostileTo(Faction.OfPlayer)){Log.Message("[LICH-TEST] skip vessels (Thay not hostile)");return;}
   var thought=DefDatabase<ThoughtDef>.GetNamed("LP_RepelledVessel");
   foreach(var d in Twilight.Deities){
    // 1.63.0：化身检查原来用随机空地（可能是浅水）和随机 2 格内的殖民者，偶尔看不见 / 灰放不下；现在用 10 格内全是干地的空地，殖民者固定在正东 2 格。
    var cell=DryOpenCell(10f);
    var v=EvKind("LP_FH_RedWizard",enemy,cell);var h=Twilight.MakeVessel(v,d);
    Check(h!=null && Twilight.VesselDeity(v)==d && Twilight.InfusedBy(v,d.key) && h.LabelInBrackets==d.label+"的化身容器","vessel of "+d.label);
    var near=EvPawn(null,Faction.OfPlayer);near.Position=cell+IntVec3.East*2;near.Notify_Teleported();
    switch(d.key){
     case "Bane":{
      var rifle=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"));near.equipment.AddEquipment(rifle);
      h.ResetCooldowns();TwilightSkills.Tick(h,60);
      Check(Items.Has(near,"LP_BaneFear") && near.equipment.Primary==null,"Bane vessel: fear aura and tyrant's command disarms");
      var follower=EvKind("LP_FH_RedWizard",enemy,CellFinder.RandomClosewalkCellNear(cell,map,3));
      Check(!follower.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,null,true),"Bane vessel: iron law, nearby raiders do not flee");
      v.stances.stunner.StunFor(120,null);Check(!v.stances.stunner.Stunned,"vessels are immune to stun");
      Check(!v.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk,null,true),"vessels are immune to mental states");
      HealthUtility.DamageUntilDowned(v,false);Flush();
      Check(Twilight.Vessel(v)==null && follower.MentalStateDef==MentalStateDefOf.PanicFlee,"Bane departs on downing: power drained, followers flee");
      break;}
     case "Surtr":{
      h.ResetCooldowns();float before=near.health.summaryHealth.SummaryHealthPercent;TwilightSkills.Tick(h,60);
      Check(near.Dead || near.health.summaryHealth.SummaryHealthPercent<before,"Surtr vessel: flame sweep hits enemies within 3");
      Check(TwilightSkills.DamageFactor(v,new DamageInfo(DamageDefOf.Flame,10f))==0f,"Surtr vessel: complete fire immunity");
      for(int i=0;i<3;i++)v.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1f,0f,-1f,near));Check(h.furnace==3 && Near(TwilightSkills.DamageFactor(near,new DamageInfo(DamageDefOf.Blunt,10f,0f,-1f,v)),1.15f),"Surtr vessel: furnace heart +5% melee per hit taken");
      ExtinguishAll();
      TwilightSkills.Depart(v,h);Check(Twilight.Vessel(v)==null && !v.Dead && v.health.hediffSet.hediffs.Count(x=>x.def.defName=="Burn")>=1,"Surtr departs: burst, badly burned but alive");
      break;}
     case "Ilsvi":{
      h.ResetCooldowns();Twilight.TestRoll=1f;var w=TwilightSkills.Whisper(h);
      Check(w!=null && w.MentalStateDef==MentalStateDefOf.Berserk,"Ilsvi vessel: whisper drives an enemy berserk");
      h.timedUntil[h.timedPawns.IndexOf(w)]=EvNow;TwilightSkills.ReleaseTimed(h,false);Check(!w.InMentalState,"whisper berserk ends after 10 seconds");
      Check(v.GetStatValue(StatDefOf.PsychicSensitivity)==0f,"Ilsvi vessel: psychic barrier");
      var thrall=EvKind("LP_IC_Thrall",enemy,CellFinder.RandomClosewalkCellNear(cell,map,3));FactionGroups.Thrall(thrall);
      Check(Near(TwilightSkills.DamageFactor(near,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,thrall)),1.15f),"Ilsvi vessel: dominated thralls deal +15%");
      TwilightSkills.Depart(v,h);float withMark=MindFlayer.Value(v,false);var mark=v.health.hediffSet.hediffs.OfType<Hediff_FormerVessel>().FirstOrDefault();if(mark!=null)v.health.RemoveHediff(mark);float without=MindFlayer.Value(v,false);
      Check(Twilight.Vessel(v)==null && v.health.hediffSet.HasHediff(HediffDefOf.Dementia) && mark?.deity?.key=="Ilsvi" && Near(withMark-without,TwilightSkills.IlsviDevourBonus),"Ilsvi departs: brain burnt out (dementia), +60 cerebrum when devoured");
      break;}
     case "Tyr":{
      var ab=near.abilities;var abilityDef=DefDatabase<AbilityDef>.GetNamed("LP_AstralStep");ab.GainAbility(abilityDef);var a=ab.GetAbility(abilityDef);
      var home=near.Position;near.Position=GenRadial.RadialCellsAround(v.Position,25f,true).First(x=>x.InBounds(map) && x.Standable(map) && x.GetFirstPawn(map)==null && x.DistanceTo(v.Position)>TwilightSkills.TyrCourtRadius+2 && !map.mapPawns.AllPawnsSpawned.Any(o=>Twilight.VesselOf(o,"Tyr") && o.Position.InHorDistOf(x,TwilightSkills.TyrCourtRadius+1)));near.Notify_Teleported();bool baseOk=a.CanCast.Accepted;Check(!TwilightSkills.Silenced(near),"outside the court of judgment abilities are free");
      near.Position=home;near.Notify_Teleported();string why=null;Check(TwilightSkills.Silenced(near) && !a.CanCast.Accepted && (!baseOk || a.CanCast.Reason.Contains("审判之庭")) && a.GizmoDisabled(out why),"Tyr vessel: court of judgment silences abilities within 10 ("+why+")");
      near.records.AddTo(RecordDefOf.KillsHumanlikes,4);h.ResetCooldowns();var hurt=Enemies(v).ToDictionary(x=>x,x=>x.health.summaryHealth.SummaryHealthPercent);var hit=TwilightSkills.Punish(v,d);
      Check(hit!=null && TwilightSkills.Crimes(hit)>=TwilightSkills.Crimes(near) && hit.health.summaryHealth.SummaryHealthPercent<hurt[hit],"Tyr vessel: punishment strikes the worst sinner ("+(hit==null?0:TwilightSkills.Crimes(hit))+" crimes)");
      Check(Twilight.Vessel(v).CurStage.painFactor==0f,"Tyr vessel: no pain");
      TwilightSkills.Depart(v,h);
      Check(Twilight.Vessel(v)==null && Items.Has(v,"LP_Surrendered") && !v.health.hediffSet.hediffs.Any(x=>x.Bleeding) && v.health.hediffSet.hediffs.OfType<Hediff_FormerVessel>().Any(x=>x.deity==d),"Tyr departs: surrenders, no bleeding, can be captured");
      ab.RemoveAbility(abilityDef);
      break;}
     case "Lathander":{
      var ally=EvKind("LP_FH_RedWizard",enemy,CellFinder.RandomClosewalkCellNear(cell,map,3));ally.TakeDamage(new DamageInfo(DamageDefOf.Cut,8f,5f,-1f,null,ally.RaceProps.body.corePart));
      float hurt=ally.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity);h.ResetCooldowns();TwilightSkills.Tick(h,60);
      Check(ally.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity)<hurt,"Lathander vessel: dawn aura heals allies");
      float before=near.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity);var beam=TwilightSkills.Beam(v,d);
      Check(beam!=null && beam.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity)>0,"Lathander vessel: light beam strikes an enemy");
      DownTorso(v);Flush();Check(Twilight.Vessel(v)!=null && h.revived && !v.Downed,"Lathander vessel: first downing revives at full health"+Diag(v));
      DownTorso(v);Flush();Check(Twilight.Vessel(v)==null && v.Downed,"Lathander departs on the second downing"+Diag(v));
      break;}
     case "Vlaakith":{
      near.Position=cell+IntVec3.East*5;near.Notify_Teleported();
      h.ResetCooldowns();var aim=TwilightSkills.AstralRaid(h);
      Check(aim!=null && v.Position.AdjacentTo8WayOrInside(aim.Position) && h.sureHit,"Vlaakith vessel: astral raid lands next to a colonist");
      var verb=v.meleeVerbs.TryGetMeleeVerb(aim);Check(verb is Verb_MeleeAttack m && Near((float)AccessTools.Method(typeof(Verb_MeleeAttack),"GetNonMissChance").Invoke(m,new object[]{new LocalTargetInfo(aim)}),1f),"astral raid: the next melee hit is certain");
      float before=aim.health.summaryHealth.SummaryHealthPercent;TwilightSkills.OnMeleeHit(v,aim);
      Check(!h.sureHit && aim.health.summaryHealth.SummaryHealthPercent<before,"Vlaakith vessel: psychic blade adds damage");
      var vcell=v.Position;int giths=map.mapPawns.AllPawnsSpawned.Count(p=>p.kindDef.defName=="LP_FH_GithMercenary");
      HealthUtility.DamageUntilDowned(v,false);Flush();Flush();
      var newGith=map.mapPawns.AllPawnsSpawned.Where(p=>p.kindDef.defName=="LP_FH_GithMercenary").ToList();eventMade.AddRange(newGith);
      Check(v.Dead && (v.Corpse==null || v.Corpse.Destroyed) && GenRadial.RadialCellsAround(vcell,4f,true).Where(c=>c.InBounds(map)).SelectMany(c=>c.GetThingList(map)).Any(t=>t.def==ThingDefOf.Filth_Ash),"Vlaakith departs: the body turns to ash, nothing to capture"+Diag(v)+" corpse="+(v.Corpse==null?"none":v.Corpse.Destroyed?"destroyed":"exists@"+v.Corpse.PositionHeld)+" ashNear="+GenRadial.RadialCellsAround(vcell,3f,true).Where(c=>c.InBounds(map)).SelectMany(c=>c.GetThingList(map)).Count(t=>t.def==ThingDefOf.Filth_Ash)+" cell="+vcell+" terrain="+vcell.GetTerrain(map).defName);
      Check(newGith.Count-giths==TwilightSkills.VlaakithRevenge,"queen's revenge: 2 githyanki leap out of the astral sea");
      break;}
     case "Talos":{
      h.ResetCooldowns();h.ready["lightning"]=EvNow+99999;TwilightSkills.Tick(h,60);
      Check(Items.Has(near,"LP_TalosGale") && map.weatherManager.curWeather.defName=="RainyThunderstorm","Talos vessel: gale aura and forced thunderstorm");
      var strike=TwilightSkills.Lightning(v,d);Check(strike.IsValid,"Talos vessel: lightning near a colonist");
      TwilightSkills.Depart(v,h);Check(Twilight.Vessel(v)==null && !v.Dead,"Talos departs: struck by his own lightning, alive");
      break;}
     case "Malar":{
      var hare=DefDatabase<PawnKindDef>.GetNamed("Hare");var wild=PawnGenerator.GeneratePawn(hare);GenSpawn.Spawn(wild,CellFinder.RandomClosewalkCellNear(cell,map,6),map);eventMade.Add(wild);
      foreach(var other in map.mapPawns.AllPawnsSpawned.Where(a=>a.RaceProps.Animal && a.Faction==null && a!=wild && !a.InMentalState).ToList())other.Destroy();
      TwilightSkills.Tick(h,60);
      Check(h.calledHunt && wild.MentalStateDef==MentalStateDefOf.ManhunterPermanent && h.hunted.Contains(wild),"Malar vessel: hunting call turns wild animals on the colony");
      var pet=PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Muffalo"),Faction.OfPlayer);GenSpawn.Spawn(pet,CellFinder.RandomClosewalkCellNear(cell,map,4),map);eventMade.Add(pet);
      foreach(var c in map.mapPawns.FreeColonistsSpawned)if(c.Position.InHorDistOf(pet.Position,2f))c.skills.GetSkill(SkillDefOf.Animals).Level=Math.Min(c.skills.GetSkill(SkillDefOf.Animals).Level,5);
      h.ResetCooldowns();h.calledHunt=true;var lost=TwilightSkills.Backlash(h);
      Check(lost==pet && pet.MentalStateDef==MentalStateDefOf.Manhunter,"Malar vessel: a colony animal goes wild for 30 seconds");
      h.timedUntil[h.timedPawns.IndexOf(pet)]=EvNow;TwilightSkills.ReleaseTimed(h,false);Check(!pet.InMentalState,"backlash ends after 30 seconds");
      Check(Twilight.Vessel(v).CurStage.statFactors.Any(x=>x.stat==StatDefOf.MeleeDamageFactor && Near(x.value,1.25f)),"Malar vessel: melee +25%");
      TwilightSkills.Depart(v,h);Check(Twilight.Vessel(v)==null && !wild.InMentalState,"Malar departs: hunted beasts calm down");
      break;}
    }
    Check(col0(thought),"repelling the "+d.label+" vessel cheers the colony");
    Check(map.listerThings.ThingsOfDef(Items.Thing("LP_MiracleShard")).Any(),"vessel of "+d.label+" leaves miracle shards");
    foreach(var s in map.listerThings.ThingsOfDef(Items.Thing("LP_MiracleShard")).ToList())s.Destroy();
    foreach(var c in map.mapPawns.FreeColonistsSpawned)c.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(thought);
    foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>p.InMentalState && p.Faction==Faction.OfPlayer).ToList())p.MentalState.RecoverFromState();
    ExtinguishAll();
   }
   map.weatherManager.TransitionTo(WeatherDefOf.Clear);
  }
  bool col0(ThoughtDef t)=>map.mapPawns.FreeColonistsSpawned.Any(c=>c.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(t)!=null);

  // ==================== 存读档 ====================
  void PrepareEventsSave(){
   Twilight.TestRoll=1f;
   if(PortalEvent.TryFindSpot(map,out var cell)){
    var r=PortalEvent.Open(map,cell,DefDatabase<PortalPlaneDef>.GetNamed("LP_Plane_Feywild"));r.Comp.SpawnWave();
    foreach(var p in r.Comp.spawned.ToList()){if(p.Spawned)p.DeSpawn();p.Destroy();}r.Comp.spawned.Clear();
    r.Comp.nextWaveTick=EvNow+10*GenDate.TicksPerDay;eventRiftId=r.ThingID;eventRiftWave=r.Comp.wave;eventRiftNext=r.Comp.nextWaveTick;eventRiftPlane=r.Comp.plane.defName;
   }
   Twilight.World.startTick=EvNow+12345*10;eventOmenStart=Twilight.World.startTick;
   var v=Person();var h=Twilight.MakeVessel(v,Twilight.ByKey("Surtr"));h.furnace=3;eventFurnace=3;Find.WorldPawns.PassToWorld(v,PawnDiscardDecideMode.KeepForever);eventVesselId=v.ThingID;
  }
  void CheckEventsAfterLoad(){
   map=Find.CurrentMap;
   if(eventRiftId!=null){
    var r=PortalEvent.Rifts(map).FirstOrDefault(x=>x.ThingID==eventRiftId);
    Check(r!=null && r.Comp.plane?.defName==eventRiftPlane && r.Comp.wave==eventRiftWave && r.Comp.nextWaveTick==eventRiftNext,"rift plane, wave and timer survive save/load");
    r?.Destroy();
   }
   Check(Twilight.World!=null && Twilight.World.startTick==eventOmenStart,"twilight countdown survives save/load");Twilight.World.startTick=-1;
   var v=Find.WorldPawns.AllPawnsAliveOrDead.FirstOrDefault(p=>p.ThingID==eventVesselId);
   Check(v!=null && Twilight.VesselOf(v,"Surtr") && Twilight.Vessel(v).furnace==eventFurnace && Twilight.InfusedBy(v,"Surtr"),"vessel deity, furnace stacks and infusion trait survive save/load");
   if(v!=null){Find.WorldPawns.RemovePawn(v);v.Destroy();}
  }
 }
}
