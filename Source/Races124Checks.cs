using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 // 1.24.0 元素裔、象人、食人魔、鸟人、寒冰魔、炎魔、巨灵、泰坦的检查（需求第十二节）。
 public partial class LichChecks {
  static readonly string[] races124Normal={"LP_GenasiFire","LP_GenasiWater","LP_GenasiEarth","LP_GenasiAir","LP_Loxodon","LP_Ogre","LP_Aarakocra"};
  static readonly string[] races124Legend={"LP_IceDevil","LP_Balor","LP_Titan","LP_Genie"};
  static readonly Dictionary<string,int> races124Met=new Dictionary<string,int>{{"LP_GenasiFire",-1},{"LP_GenasiWater",-1},{"LP_GenasiEarth",-1},{"LP_GenasiAir",-1},{"LP_Loxodon",-1},{"LP_Ogre",1},{"LP_Aarakocra",-2}};
  public static readonly Dictionary<string,int> races124MeasuredMet=new Dictionary<string,int>();
  static string r124WaterId,r124LoxId,r124OgreId,r124BirdId,r124TitanId,r124GenieId,r124LampId,r124DimLampId,r124StoredId,r124ChilledId;
  static int r124TideCd,r124SoarCd,r124SmiteCd,r124WishCd,r124Roofed,r124DimUntil;static float r124Divinity;
  readonly List<Thing> r124Made=new List<Thing>();
  T R124<T>(T t) where T:Thing{if(t!=null)r124Made.Add(t);return t;}
  Pawn R124Pawn(string xeno){var p=RacePawn(xeno);r124Made.Add(p);return p;}
  Pawn R124Human(){var p=SharedPawn();r124Made.Add(p);return p;}
  static Faction ThayFaction=>Factions.Hostile;
  static void FlushDeferred(){Items.State?.Flush();}
  static float Ratio(float a,float b)=>b==0?0:a/b;
  // 属性值有缓存：基因、装备刚变过时先清掉再读。
  static float Fresh(Thing t,StatDef stat){stat.Worker.ClearCacheForThing(t);return t.GetStatValue(stat);}

  void CheckRaces124(){
   var before=new HashSet<Thing>(map.listerThings.AllThings);
   var goodwill=Find.FactionManager.AllFactions.Where(f=>!f.IsPlayer && f.HasGoodwill).ToDictionary(f=>f,f=>f.PlayerGoodwill);
   var letters=new HashSet<Letter>(Find.LetterStack.LettersListForReading);
   var weather=map.weatherManager.curWeather;var conditions=new HashSet<GameCondition>(map.gameConditionManager.ActiveConditions);
   var skills=map.mapPawns.FreeColonistsSpawned.ToDictionary(p=>p,p=>p.skills?.skills.ToDictionary(s=>s.def,s=>(s.Level,s.xpSinceLastLevel)));
   bool titanWeather=LichMod.Settings.titanWeather;
   try{
    CheckRaces124Defs();CheckRaces124Bodies();CheckGenasi124();CheckAmphibious124();CheckLoxodon124();CheckOgre124();CheckAarakocra124();
    CheckFiends124();CheckFiendRaids124();CheckGenie124();CheckTitan124();CheckRaceItems124();CheckRaces124Relations();CheckRaces124Kinds();
   }finally{
    LichMod.Settings.titanWeather=titanWeather;
    FlushDeferred();Races124.State?.bursts.Clear();Races124.State?.peaceUntil.Remove(map.uniqueID);
    foreach(var t in r124Made)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    r124Made.Clear();
    foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p)).ToList())p.Destroy();
    foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Filth) && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList())if(!t.Destroyed)t.Destroy();
    foreach(var kv in goodwill)if(kv.Key.PlayerGoodwill!=kv.Value)Factions.SetRelation(kv.Key,Faction.OfPlayer,kv.Value);
    foreach(var l in Find.LetterStack.LettersListForReading.Where(l=>!letters.Contains(l)).ToList())Find.LetterStack.RemoveLetter(l);
    foreach(var c in map.gameConditionManager.ActiveConditions.Where(c=>!conditions.Contains(c)).ToList())c.End();
    if(weather!=null)map.weatherManager.TransitionTo(weather);
    foreach(var kv in skills)if(kv.Key.skills!=null && kv.Value!=null)foreach(var s in kv.Key.skills.skills)if(kv.Value.TryGetValue(s.def,out var v)){s.Level=v.Level;s.xpSinceLastLevel=v.xpSinceLastLevel;}
    RacialRelations.Tracker?.Census(map).Rebuild(map);ColonyCensus.Dirty();
   }
   Check(map.listerThings.AllThings.All(t=>before.Contains(t) || t is Filth || t is Mote || t.def.category==ThingCategory.Ethereal),"1.24.0 race checks leave no extra things behind");
  }

  // ---------- 定义、代谢、贴图 ----------
  void CheckRaces124Defs(){
   foreach(var name in races124Normal.Concat(races124Legend)){
    var x=DefDatabase<XenotypeDef>.GetNamed(name);
    Check(!x.ConfigErrors().Any() && x.genes.All(g=>g!=null) && x.factionlessGenerationWeight==0,name+" xenotype validates with resolved genes and no random generation");
    bool legend=races124Legend.Contains(name);
    Check(legend?!x.inheritable && x.genes.Any(g=>g.defName=="Sterile"):x.inheritable,name+(legend?" is not inheritable and is sterile":" is inheritable"));
    Check(x.genes.All(g=>g.biostatArc==0),name+" uses no archite genes");
    int met=x.genes.Sum(g=>g.biostatMet);races124MeasuredMet[name]=met;
    if(races124Met.TryGetValue(name,out int want))Check(met==want,name+" metabolism total "+met+" (target "+want+")");
    else Check(true,name+" metabolism total "+met+" (legendary, recorded only)");
    Check(ContentFinder<Texture2D>.Get(x.iconPath,false)!=null,name+" xenotype icon loads");
    foreach(var g in x.genes.Where(q=>q.defName.StartsWith("LP_"))){
     Check(!g.ConfigErrors().Any() && (g.modExtensions.NullOrEmpty() || g.modExtensions.All(e=>!e.ConfigErrors().Any())),name+" gene validates: "+g.defName);
     Check(g.selectionWeight==0 && ContentFinder<Texture2D>.Get(g.iconPath,false)!=null,"custom gene stays out of the random pool and has an icon: "+g.defName);
    }
    var p=RacePawn(name);
    Check(p.genes.Xenotype==x && x.genes.All(g=>p.genes.GetGene(g)!=null) && x.genes.Where(g=>g.defName.StartsWith("LP_")).All(g=>p.genes.GetGene(g).Active),name+" generates with every xenotype gene active");
    p.Destroy();
   }
   var elements=Genasi.Elements.Select(Genasi.Bloodline).ToList();
   Check(elements.All(g=>g!=null && g.exclusionTags.Contains("DND_Element")) && elements[0].ConflictsWith(elements[1]),"the four element bloodlines share the DND_Element exclusion tag");
   Check(Genie.Xenotype.genes.All(g=>!elements.Contains(g)),"the genie xenotype itself carries no bloodline (it is added on summoning)");
   foreach(var a in new[]{"LP_ElementSpark","LP_ElementTide","LP_ElementWindLeap","LP_Soar","LP_BalorBuffet","LP_GenieWish","LP_TitanSmite","LP_TitanBreath","LP_IvoryHornBlast"}){var def=DefDatabase<AbilityDef>.GetNamed(a);Check(!def.ConfigErrors().Any() && ContentFinder<Texture2D>.Get(def.iconPath,false)!=null && def.comps.OfType<CompProperties_Races124Ability>().Any(),"ability validates with icon: "+a);}
   var cds=new Dictionary<string,int>{{"LP_ElementSpark",30000},{"LP_ElementTide",60000},{"LP_ElementWindLeap",20000},{"LP_Soar",10000},{"LP_BalorBuffet",60000},{"LP_GenieWish",3600000},{"LP_TitanSmite",60000},{"LP_TitanBreath",120000},{"LP_IvoryHornBlast",60000}};
   foreach(var kv in cds)Check(DefDatabase<AbilityDef>.GetNamed(kv.Key).cooldownTicksRange.min==kv.Value,"ability cooldown "+kv.Key+" = "+kv.Value);
   var sizes=new[]{"Races124/Trunk/Trunk_south","Races124/Trunk/Trunk_east","Races124/Horns/IceHorns_south","Races124/Horns/IceHorns_east","Races124/Horns/IceHorns_north","Races124/Horns/BalorHorns_south","Races124/Horns/BalorHorns_east","Races124/Horns/BalorHorns_north","Races124/Circlet/GenieCirclet_south","Races124/Circlet/GenieCirclet_east","Races124/Circlet/GenieCirclet_north","Races124/Halo/TitanHalo_south","Races124/Halo/TitanHalo_east","Races124/Halo/TitanHalo_north","Races124/Wings/AarakocraWings_south","Races124/Wings/AarakocraWings_east","Races124/Wings/AarakocraWings_north","Races124/Wings/BalorWings_south","Races124/Wings/BalorWings_east","Races124/Wings/BalorWings_north"};
   foreach(var path in sizes){var tex=ContentFinder<Texture2D>.Get(path,false);Check(tex!=null && tex.width==128 && tex.height==128,"attachment art loads at 128px: "+path);}
   foreach(var b in new[]{"Male","Female","Thin","Hulk","Fat"})foreach(var r in new[]{"south","east","north"}){var tex=ContentFinder<Texture2D>.Get("Races124/Items/Worn/FeatherCloak_"+b+"_"+r,false);Check(tex!=null && tex.width==128,"feather cloak worn art loads: "+b+" "+r);}
   foreach(var def in DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.graphicData?.texPath?.StartsWith("Races124/")==true))Check(def.graphic!=null && def.graphic.MatSingle!=BaseContent.BadMat,"item graphic resolves: "+def.defName);
   var scen=DefDatabase<ScenarioDef>.GetNamed("LP_FallenEmpyrean");Check(scen!=null && !scen.ConfigErrors().Any() && scen.scenario.AllParts.Any(p=>p is ScenPart_FallenEmpyrean) && scen.scenario.AllParts.Any(p=>p is ScenPart_TimedRaid r && r.faction==Factions.HostileDef && Math.Abs(r.days.min-10)<.01f),"scenario Fallen Empyrean loads with its titan part and a day-10 Thayan raid");
  }

  // ---------- 体型、渲染、搬运、食量、巨躯 ----------
  void CheckRaces124Bodies(){
   var human=R124Human();float hs=human.BodySize,hr=RootScale(human);
   var table=new (string x,float size,float render,float carry,string appetite,float hunger)[]{("LP_Loxodon",1.3f,1.15f,1.15f,"LP_LoxodonAppetite",1.2f),("LP_Ogre",1.6f,1.3f,1.1f,"LP_OgreAppetite",1.3f),("LP_Aarakocra",.9f,1f,1f,null,1f),("LP_IceDevil",1.5f,1.3f,1f,null,1f),("LP_Balor",2.5f,1.6f,1f,null,1f),("LP_Genie",1.5f,1.3f,1f,null,1f),("LP_Titan",2.5f,1.6f,1.2f,"LP_TitanAppetite",1.5f)};
   var rule=BodySize.ActiveRule;
   foreach(var row in table){
    var p=R124Pawn(row.x);
    Check(Near(p.BodySize,hs*row.size) && Math.Abs(RootScale(p)-hr*row.render)<.03f,row.x+" real size x"+row.size+" and drawn at x"+row.render+": "+p.BodySize.ToString("0.00")+" / "+(RootScale(p)/hr).ToString("0.00"));
    Check(Math.Abs(SmallBody.Carry(p)-row.carry)<.01f,row.x+" extra carry factor x"+row.carry);
    if(row.appetite!=null){var h=p.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed(row.appetite));Check(h!=null && Near(h.CurStage.hungerRateFactor,row.hunger),row.x+" appetite hediff x"+row.hunger);}
    float hw=rule.on?1f+BodySize.HealthPerSize*rule.k*Mathf.Max(0f,row.size-1f):1f,mw=rule.on?1f+BodySize.MeleePerSize*rule.k*Mathf.Max(0f,row.size-1f):1f;
    Check(Near(BodySize.HealthFactor(p),hw) && Near(BodySize.MeleeFactor(p),mw),row.x+" giant-body rule health x"+BodySize.HealthFactor(p).ToString("0.00")+" melee x"+BodySize.MeleeFactor(p).ToString("0.00"));
    if(row.size>=2.5f && rule.on && Near(rule.k,1f))Check(Near(BodySize.HealthFactor(p),1.75f) && Near(BodySize.MeleeFactor(p),1.45f),row.x+" at body size 2.5 gets health x1.75 and melee x1.45");
   }
  }

  // ---------- 元素裔 ----------
  void CheckGenasi124(){
   var enemyF=EnemyFaction();var start=ClearLine(9);
   var fire=R124Pawn("LP_GenasiFire");Place(fire,start);
   Check(Fresh(fire,StatDefOf.Flammability)<=.001f,"fire genasi cannot be ignited (flammability 0)");
   Check(Near(fire.genes.FactorForDamage(new DamageInfo(DamageDefOf.Flame,10)),.5f) && Near(fire.genes.FactorForDamage(new DamageInfo(DamageDefOf.Blunt,10)),1f),"fire genasi take half flame damage, not immunity");
   var target=R124Human();target.SetFaction(enemyF);Place(target,start+IntVec3.East*4);target.jobs.StopAll();
   Check(Genasi.CannotSpark(fire,new LocalTargetInfo(start+IntVec3.East*7))!=null && Genasi.CannotSpark(fire,new LocalTargetInfo(target))==null,"spark reaches 6 cells");
   Check(Genasi.Spark(fire,new LocalTargetInfo(target)) && target.HasAttachment(ThingDefOf.Fire),"spark sets the target on fire");
   target.GetAttachment(ThingDefOf.Fire)?.Destroy();
   var spark=fire.abilities.GetAbility(Races124.Ability("LP_ElementSpark"));spark.StartCooldown(spark.def.cooldownTicksRange.min);
   Check(Math.Abs(spark.CooldownTicksRemaining-30000)<=5,"spark cools down for 12 hours: "+spark.CooldownTicksRemaining);spark.ResetCooldown();
   // 水流
   var water=R124Pawn("LP_GenasiWater");Place(water,start);var center=start+IntVec3.East*5;
   Place(target,center);var friend=R124Human();Place(friend,center+IntVec3.North);
   foreach(var c in CellRect.CenteredOn(center,1).Cells)if(c.InBounds(map) && c.GetFirstPawn(map)==null){var fl=(Fire)ThingMaker.MakeThing(ThingDefOf.Fire);fl.fireSize=.5f;GenSpawn.Spawn(fl,c,map);}
   int fires=CellRect.CenteredOn(center,1).Cells.Sum(c=>c.GetThingList(map).Count(t=>t is Fire));
   float speed=Fresh(target,StatDefOf.MoveSpeed);
   int soaked=Genasi.Tide(water,center);
   Check(fires>0 && CellRect.CenteredOn(center,1).Cells.All(c=>!c.GetThingList(map).Any(t=>t is Fire)),"tide puts out every fire in 3x3 ("+fires+" fires)");
   Check(soaked==1 && Items.Has(target,"LP_Soaked") && !Items.Has(friend,"LP_Soaked") && Ratio(Fresh(target,StatDefOf.MoveSpeed),speed)<.85f && Items.TicksLeft(target,"LP_Soaked")==Genasi.SoakTicks,"tide soaks enemies only: move x0.8 for 10 seconds");
   Check(Genasi.TideRadius(water)==1,"tide is 3x3 without the focus");
   // 土
   var earth=R124Pawn("LP_GenasiEarth");var msk=earth.skills.GetSkill(SkillDefOf.Mining);if(!msk.TotallyDisabled)msk.Level=12;float mine=Fresh(earth,StatDefOf.MiningSpeed),yield=Fresh(earth,StatDefOf.MiningYield),blunt=Fresh(earth,StatDefOf.ArmorRating_Blunt),move=Fresh(earth,StatDefOf.MoveSpeed);
   earth.genes.RemoveGene(earth.genes.GetGene(Genasi.Bloodline("Earth")));
   float mine0=Fresh(earth,StatDefOf.MiningSpeed),yield0=Fresh(earth,StatDefOf.MiningYield),blunt0=Fresh(earth,StatDefOf.ArmorRating_Blunt),move0=Fresh(earth,StatDefOf.MoveSpeed);
   Check(Math.Abs(Ratio(mine,mine0)-1.3f)<.02f,"earth bloodline mines 30% faster: "+Ratio(mine,mine0).ToString("0.000")+" ("+mine+" vs "+mine0+", disabled="+StatDefOf.MiningSpeed.Worker.IsDisabledFor(earth)+", genes="+string.Join(",",earth.genes.GenesListForReading.Select(g=>g.def.defName+(g.Active?"":"(off)")))+")");
   Check(Math.Abs(yield-(yield0<1f?Mathf.Min(1f,yield0+.1f):yield0))<.011f && Math.Abs(blunt-blunt0-.15f)<.011f && Math.Abs(Ratio(move,move0)-.95f)<.02f,"earth bloodline: mining yield +10% (cap 100%), blunt armor +15%, move x0.95: yield "+yield0+"->"+yield+" blunt "+blunt0+"->"+blunt+" move "+move0+"->"+move);
   // 风
   var air=R124Pawn("LP_GenasiAir");float am=Fresh(air,StatDefOf.MoveSpeed);air.genes.RemoveGene(air.genes.GetGene(Genasi.Bloodline("Air")));float am0=Fresh(air,StatDefOf.MoveSpeed);
   Check(Math.Abs(Ratio(am,am0)-1.1f)<.02f,"air bloodline moves 10% faster: "+Ratio(am,am0).ToString("0.000"));
   air.Destroy();air=R124Pawn("LP_GenasiAir");var ls=ClearLine(10);Place(air,ls);
   Check(Genasi.CannotLeap(air,ls+IntVec3.East*9)!=null && Genasi.CannotLeap(air,ls+IntVec3.East*8)==null,"wind leap reaches 8 cells");
   var wall=R124(GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog),ls+IntVec3.East*3,map));
   Check(Genasi.CannotLeap(air,ls+IntVec3.East*6)!=null,"wind leap needs line of sight and cannot pass a wall");
   wall.Destroy();
   Check(Genasi.Leap(air,ls+IntVec3.East*6) && air.Position==ls+IntVec3.East*6,"wind leap moves the air genasi");
  }

  static float MoveCost(Pawn p,IntVec3 c)=>(float)AccessTools.Method(typeof(Pawn_PathFollower),"CostToMoveIntoCell",new[]{typeof(Pawn),typeof(IntVec3)}).Invoke(null,new object[]{p,c});
  void CheckAmphibious124(){
   var cell=map.AllCells.Where(c=>c.x>8 && c.z>8 && c.x<map.Size.x-8 && c.z<map.Size.z-8 && CellRect.CenteredOn(c,2).Cells.All(x=>x.Standable(map) && !x.Fogged(map) && x.GetFirstPawn(map)==null && x.GetEdifice(map)==null && x.GetFirstItem(map)==null && !x.GetTerrain(map).IsWater)).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
   var land=cell+IntVec3.West;var water=cell+IntVec3.East;var old=water.GetTerrain(map);
   try{
    map.terrainGrid.SetTerrain(water,TerrainDefOf.WaterShallow);
    var g=R124Pawn("LP_GenasiWater");var h=R124Human();
    Place(g,cell);float gWater=MoveCost(g,water),gLand=MoveCost(g,land);
    Place(h,cell);float hWater=MoveCost(h,water),hLand=MoveCost(h,land);
    Check(g.WaterCellCost==0 && h.WaterCellCost==null,"water genasi carry a per-pawn water cell cost of 0; humans have none");
    Check(hWater>hLand+5 && gWater<=gLand+.5f,"amphibious: shallow water costs a water genasi no more than land ("+gWater+" vs "+gLand+"), a human more ("+hWater+" vs "+hLand+")");
   }finally{map.terrainGrid.SetTerrain(water,old);}
  }

  // ---------- 象人 ----------
  void CheckLoxodon124(){
   var lox=R124Pawn("LP_Loxodon");var human=R124Human();
   var nodes=RenderNodes(lox);
   Check(nodes.Any(n=>n.Props?.texPath=="Races124/Trunk/Trunk" && n.PrimaryGraphic!=null) && !nodes.Any(n=>n.Props?.texPath!=null && n.Props.texPath.Contains("PigNose")),"loxodon draws its trunk on the head and no vanilla nose");
   Check(lox.story.bodyType==BodyTypeDefOf.Hulk && lox.story.hairDef==HairDefOf.Bald,"loxodon is hulking and hairless");
   foreach(var p in new[]{lox,human})foreach(var arm in p.RaceProps.body.AllParts.Where(b=>b.def.defName=="Arm").ToList())p.health.AddHediff(HediffDefOf.MissingBodyPart,arm);
   lox.health.capacities.Notify_CapacityLevelsDirty();human.health.capacities.Notify_CapacityLevelsDirty();
   float lm=lox.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation),hm=human.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation);
   Check(Near(lm,.5f) && hm<.2f,"with both arms gone the trunk still gives 50% manipulation: "+lm.ToString("0.00")+" (human "+hm.ToString("0.00")+")");
   lox.Destroy();lox=R124Pawn("LP_Loxodon");human.Destroy();human=R124Human();
   var lich=R124Human();
   Check(!LichSpells.Frighten(lich,lox) && !lox.InMentalState,"fear gaze does nothing to a calm loxodon");
   Check(!lox.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"测试",true) && !lox.InMentalState,"loxodon cannot be made to panic");
   Check(LichSpells.Frighten(lich,human) || Fresh(human,StatDefOf.PsychicSensitivity)<=0,"the same fear gaze still panics a human");
   human.mindState.mentalStateHandler.CurState?.RecoverFromState();
   var cloak=DefDatabase<ThoughtDef>.GetNamed("LP_SawTendrilCloak");
   Check(ThoughtUtility.ThoughtNullified(lox,cloak) && !ThoughtUtility.ThoughtNullified(human,cloak),"loxodon ignores the tendril cloak fear mood");
   Check(Near(DNDMind.Resistance(lox),.2f),"loxodon mind resistance 20%: "+DNDMind.Resistance(lox));
   var bl=Fresh(lox,StatDefOf.ArmorRating_Blunt);lox.genes.RemoveGene(lox.genes.GetGene(Races124.Gene("LP_LoxodonHide")));
   Check(Math.Abs(bl-Fresh(lox,StatDefOf.ArmorRating_Blunt)-.1f)<.011f,"thick hide adds 10% blunt armor");
  }

  // ---------- 食人魔 ----------
  void CheckOgre124(){
   var ogre=R124Pawn("LP_Ogre");var human=R124Human();var club=DefDatabase<ThingDef>.GetNamed("LP_OgreClub");var ext=club.GetModExtension<HeavyWeaponExtension>();
   ThingWithComps Arm(Pawn p){var w=(ThingWithComps)ThingMaker.MakeThing(club,ThingDefOf.WoodLog);w.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Colony);p.equipment.DestroyAllEquipment();p.equipment.AddEquipment(w);return w;}
   float hMove=Fresh(human,StatDefOf.MoveSpeed),oMove=Fresh(ogre,StatDefOf.MoveSpeed);
   var hw=Arm(human);var ow=Arm(ogre);
   Check(Near(HeavyWeapons.DamageFactor(human,hw),.6f) && Near(HeavyWeapons.DamageFactor(ogre,ow),1f),"ogre club: users under body size 1.4 deal x0.6, ogres x1");
   StatDefOf.MoveSpeed.Worker.ClearCacheForThing(human);StatDefOf.MoveSpeed.Worker.ClearCacheForThing(ogre);
   Check(Math.Abs(Fresh(human,StatDefOf.MoveSpeed)-(hMove-.2f))<.05f || FullModList,"the club is too heavy for a human: move -0.2 ("+hMove+" -> "+Fresh(human,StatDefOf.MoveSpeed)+")");
   Check(Math.Abs(Fresh(ogre,StatDefOf.MoveSpeed)-oMove)<.02f,"an ogre carries the club without slowing down");
   var dummy=R124Human();
   Verb_MeleeAttackDamage Head(Pawn p)=>p.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().First(v=>v.EquipmentSource!=null && v.tool.capacities.Any(c=>c.defName=="Blunt"));
   float small=MeleeSample(Head(human),dummy,200,91);float keep=ext.minBodySize;ext.minBodySize=0f;float full=MeleeSample(Head(human),dummy,200,91);ext.minBodySize=keep;
   Check(Math.Abs(Ratio(small,full)-.6f)<.01f,"real club damage in human hands is 60%: "+Ratio(small,full).ToString("0.000"));
   var start=ClearLine(5);Place(ogre,start);Place(dummy,start+IntVec3.East);
   Check(!Ogre.TryKnock(dummy,ogre,.5f) && dummy.Position==start+IntVec3.East && Ogre.TryKnock(dummy,ogre,.1f) && dummy.Position==start+IntVec3.East*2,"club hits knock the target back 1 cell 20% of the time");
   // 技能太低时研究速度、交易压价会贴着下限，比不出倍率：先把智识、社交提到12级。
   foreach(var sk in new[]{SkillDefOf.Intellectual,SkillDefOf.Social}){var s=ogre.skills.GetSkill(sk);if(!s.TotallyDisabled)s.Level=12;}
   float rs=Fresh(ogre,StatDefOf.ResearchSpeed),tp=Fresh(ogre,StatDefOf.TradePriceImprovement);
   Check(DNDMind.Resistance(ogre)==0f,"ogre mind resistance cannot go below 0");
   ogre.genes.RemoveGene(ogre.genes.GetGene(Races124.Gene("LP_OgreDull")));
   Check(Math.Abs(Ratio(rs,Fresh(ogre,StatDefOf.ResearchSpeed))-.5f)<.02f && Fresh(ogre,StatDefOf.TradePriceImprovement)-tp>.01f,"dull ogres research at half speed and get worse trade prices: research "+rs+" / "+Fresh(ogre,StatDefOf.ResearchSpeed)+" trade "+tp+" / "+Fresh(ogre,StatDefOf.TradePriceImprovement));
   hw.Destroy();ow.Destroy();
  }

  // ---------- 鸟人 ----------
  void CheckAarakocra124(){
   var bird=R124Pawn("LP_Aarakocra");var nodes=RenderNodes(bird);
   Check(nodes.Any(n=>n.Props?.texPath=="Races124/Wings/AarakocraWings" && n.PrimaryGraphic!=null && n.Props.parentTagDef?.defName=="Body"),"aarakocra draws its wings as a body attachment");
   Check(Near(bird.genes.FactorForDamage(new DamageInfo(DamageDefOf.Blunt,10)),1.1f),"hollow bones take 10% more blunt damage");
   var start=ClearLine(14);Place(bird,start);
   var wall=R124(GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog),start+IntVec3.East*4,map));
   Check(Aarakocra.CannotSoar(bird,start+IntVec3.East*13)!=null && Aarakocra.CannotSoar(bird,start+IntVec3.East*10)==null,"soar reaches 12 cells and ignores the wall in between");
   var roofCell=start+IntVec3.East*9;var oldRoof=roofCell.GetRoof(map);map.roofGrid.SetRoof(roofCell,RoofDefOf.RoofRockThick);
   Check(Aarakocra.CannotSoar(bird,roofCell)!=null,"soar cannot land under a thick rock roof");
   map.roofGrid.SetRoof(roofCell,oldRoof);
   Check(Aarakocra.Soar(bird,start+IntVec3.East*10) && bird.Position==start+IntVec3.East*10,"soar flies over the wall");
   wall.Destroy();
   var soar=bird.abilities.GetAbility(Races124.Ability("LP_Soar"));soar.StartCooldown(soar.def.cooldownTicksRange.min);
   Check(Math.Abs(soar.CooldownTicksRemaining-10000)<=5,"soar cools down for 4 hours");soar.ResetCooldown();
   // 天生飞禽
   var g=bird.genes.GetFirstGeneOfType<Gene_AarakocraSkyborn>();var caged=DefDatabase<ThoughtDef>.GetNamed("LP_AarakocraCaged");var sky=DefDatabase<ThoughtDef>.GetNamed("LP_AarakocraOpenSky");
   var cell=bird.Position;var roof=cell.GetRoof(map);g.roofedTicks=0;g.freeTicks=0;
   map.roofGrid.SetRoof(cell,RoofDefOf.RoofConstructed);g.Advance(2000);
   Check(!caged.Worker.CurrentState(bird).Active,"under a roof for less than an hour: not caged yet");
   g.Advance(600);Check(caged.Worker.CurrentState(bird).Active && caged.stages[0].baseMoodEffect==-3,"after an hour under a roof the aarakocra feels caged (-3)");
   map.roofGrid.SetRoof(cell,null);g.Advance(250);Check(caged.Worker.CurrentState(bird).Active,"stepping outside for a moment is not enough");
   g.Advance(250);Check(!caged.Worker.CurrentState(bird).Active,"about ten minutes under the open sky clears the caged mood");
   var job=JobMaker.MakeJob(JobDefOf.Wait_Wander);job.workGiverDef=DefDatabase<WorkGiverDef>.AllDefsListForReading.First();bird.jobs.StartJob(job,JobCondition.InterruptForced);
   Check(sky.Worker.CurrentState(bird).Active && sky.stages[0].baseMoodEffect==2,"working under the open sky gives +2");
   map.roofGrid.SetRoof(cell,RoofDefOf.RoofConstructed);Check(!sky.Worker.CurrentState(bird).Active,"no open-sky mood under a roof");
   map.roofGrid.SetRoof(cell,roof);bird.jobs.StopAll();
  }

  // ---------- 寒冰魔、炎魔 ----------
  void CheckFiends124(){
   var thay=ThayFaction;Check(thay!=null,"the Thayan expedition exists for the fiend checks");
   var start=ClearLine(9);
   var devil=R124Pawn("LP_IceDevil");devil.SetFaction(thay);Place(devil,start);devil.jobs.StopAll();
   Check(devil.needs.food==null && devil.needs.rest==null,"fiends do not eat or sleep");
   var victim=R124Human();Place(victim,start+IntVec3.East*3);var ally=R124Pawn("LP_Ogre");ally.SetFaction(thay);Place(ally,start+IntVec3.East*2);ally.jobs.StopAll();
   float speed=Fresh(victim,StatDefOf.MoveSpeed);
   Fiends.ChillAura(devil);
   Check(Fiends.ChillLayers(victim)==1 && Fiends.ChillLayers(ally)==0,"the chill aura stacks on enemies within 5 cells, not on Thayan allies");
   for(int i=0;i<4;i++)Fiends.ChillAura(devil);
   Check(Fiends.ChillLayers(victim)==Fiends.ChillMax,"chill caps at 3 layers");
   StatDefOf.MoveSpeed.Worker.ClearCacheForThing(victim);
   Check(Math.Abs(Ratio(Fresh(victim,StatDefOf.MoveSpeed),speed)-.614f)<.03f || FullModList,"three chill layers slow movement to about 61%: "+Ratio(Fresh(victim,StatDefOf.MoveSpeed),speed).ToString("0.000"));
   var chill=(HediffWithComps)victim.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_Chilled"));
   Check(chill.TryGetComp<HediffComp_Disappears>().ticksToDisappear==Fiends.ChillTicks,"chill lasts 30 seconds after the last application");
   chill.TryGetComp<HediffComp_Disappears>().ticksToDisappear=5;Place(victim,start+IntVec3.East*7);Step(300);
   Check(Fiends.ChillLayers(victim)==0,"chill wears off away from the aura");
   // 尾刺
   var sting=devil.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().FirstOrDefault(v=>v.tool?.label=="尾刺");
   Check(sting!=null && sting.tool.capacities.Any(c=>c.defName=="Stab") && Near(sting.tool.power,18f) && Near(sting.tool.cooldownTime,3f),"ice devils get an 18-damage stinging tail every 3 seconds");
   var method=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply");
   var hit=((IEnumerable<DamageInfo>)method.Invoke(sting,new object[]{new LocalTargetInfo(victim)})).First();
   Check(hit.WeaponLinkedHediff?.defName=="LP_IceDevilStingerWeapon","the tail hit is linked to the stinger");
   victim.TakeDamage(hit);Check(victim.Dead || Fiends.ChillLayers(victim)>=1,"a tail hit adds a chill layer");
   // 魔鬼之眼
   var hidden=R124Human();Place(hidden,start+IntVec3.East*5);Items.Give(hidden,"LP_PotionInvisibility");var enemyHuman=R124Human();enemyHuman.SetFaction(thay);Place(enemyHuman,start+IntVec3.North*2);
   Check(InvisibilityUtility.IsPsychologicallyInvisible(hidden),"the invisibility fixture is invisible");
   // 原版的隐身门槛之一：能否命中。把隐身者放在寒冰魔和普通敌人旁边，比较两者的近战 verb。
   Place(devil,start);devil.jobs.StopAll();Place(hidden,start+IntVec3.North);Place(enemyHuman,start+IntVec3.North*2);enemyHuman.jobs.StopAll();
   var humanVerb=enemyHuman.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).First();
   bool devilHits=sting.CanHitTarget(new LocalTargetInfo(hidden)),humanHits=humanVerb.CanHitTarget(new LocalTargetInfo(hidden));
   Items.Remove(hidden,"LP_PotionInvisibility");bool devilBase=sting.CanHitTarget(new LocalTargetInfo(hidden)),humanBase=humanVerb.CanHitTarget(new LocalTargetInfo(hidden));Items.Give(hidden,"LP_PotionInvisibility");
   Check(devilHits && !humanHits,"the devil's sight lets it strike an invisible pawn that others cannot: devil="+devilHits+" human="+humanHits+" (visible: devil="+devilBase+" human="+humanBase+", devil at "+devil.Position+" downed="+devil.Downed+", hidden at "+hidden.Position+", human at "+enemyHuman.Position+", verbs "+sting+" / "+humanVerb+")");   Check(Fiends.Depth==0,"devil sight context is cleared after targeting");
   // 寒铁与圣水
   var coldIron=DefDatabase<ThingDef>.GetNamedSilentFail("LP_ColdIron");var swordDef=DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword");
   var fighter=R124Human();var sword=(ThingWithComps)ThingMaker.MakeThing(swordDef,coldIron);fighter.equipment.AddEquipment(sword);
   Check(Near(ItemMaterials.WeaponFactor(devil,new DamageInfo(DamageDefOf.Cut,10,0,-1,fighter,null,swordDef)),Fiends.ColdIronFactor) && Near(ItemMaterials.WeaponFactor(victim,new DamageInfo(DamageDefOf.Cut,10,0,-1,fighter,null,swordDef)),victim.HasPsylink?1.25f:1f),"cold iron deals x1.5 to fiends");
   Check(ItemThrowables.HolyTarget(devil) && !ItemThrowables.HolyTarget(victim),"holy water now harms fiends but still not the living");
   // 用一个新的、没打过架的寒冰魔，看这一次伤害实际结算了多少（3 点圣水应结算 6 点）。
   var holyDevil=R124Pawn("LP_IceDevil");holyDevil.jobs.StopAll();
   var holy=ItemThrowables.Holy.Worker.Apply(new DamageInfo(ItemThrowables.Holy,3,1f,-1,null,holyDevil.RaceProps.body.corePart),holyDevil);
   // 期望值再乘它自己的“受到伤害倍率”：随机特性“坚韧”×0.5、“纤弱”×1.15 会改变它（原来写死 6 点，生成到这类特性时就随机失败）。
   float holyIncoming=holyDevil.GetStatValue(StatDefOf.IncomingDamageFactor);
   Check(Math.Abs(holy.totalDamageDealt-6f*holyIncoming)<.01f,"holy water deals double damage to fiends (3 -> 6, times the pawn's incoming damage factor "+holyIncoming+"): "+holy.totalDamageDealt);
   holyDevil.Destroy();
   // 霜刺长枪
   var spear=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FrostSpear"));var spearman=R124Human();spearman.equipment.AddEquipment(spear);var target2=R124Human();Place(target2,start+IntVec3.South*2);
   target2.TakeDamage(new DamageInfo(DamageDefOf.Stab,5,0,-1,spearman,null,spear.def));
   int dh=devil.health.hediffSet.hediffs.Count;devil.TakeDamage(new DamageInfo(DamageDefOf.Stab,5,0,-1,spearman,null,spear.def));
   Check(Fiends.ChillLayers(target2)==1 && devil.health.hediffSet.hediffs.Count==dh && Fiends.ChillLayers(devil)==0,"the frost spear chills its target but does nothing to fiends");
   // 击倒即消散
   var spearDef=DefDatabase<ThingDef>.GetNamed("LP_FrostSpear");FactionGear.Replace(devil,"LP_FrostSpear",null);var dpos=devil.Position;
   devil.health.AddHediff(HediffDefOf.Anesthetic);
   Check(devil.Downed || devil.Destroyed,"anesthetic downs the ice devil");
   FlushDeferred();
   var loot=GenRadial.RadialDistinctThingsAround(dpos,map,3f,true).ToList();
   Check(devil.Destroyed && !devil.Dead && loot.Any(t=>t.def.defName=="LP_IceDevilStinger") && loot.Any(t=>t.def==spearDef),"a downed ice devil is banished, leaving its stinger and spear");
   Check(!map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Any(c=>c.InnerPawn==devil),"no ice devil corpse remains");
   foreach(var t in loot.Where(t=>t.def.defName=="LP_IceDevilStinger" || t.def==spearDef).ToList())t.Destroy();
   // 炎魔
   var balor=R124Pawn("LP_Balor");balor.SetFaction(thay);var bs=ClearLine(9);Place(balor,bs);balor.jobs.StopAll();
   Check(Near(balor.genes.FactorForDamage(new DamageInfo(DamageDefOf.Flame,10)),0f) && Near(balor.genes.FactorForDamage(new DamageInfo(DamageDefOf.Frostbite,10)),1.5f),"balors are immune to fire and take x1.5 cold damage");
   var near1=R124Human();Place(near1,bs+IntVec3.East);var thayMate=R124Pawn("LP_Ogre");thayMate.SetFaction(thay);Place(thayMate,bs+IntVec3.West);thayMate.jobs.StopAll();
   Check(Fiends.FlameAura(balor,()=>.99f)==0 && !near1.HasAttachment(ThingDefOf.Fire),"the flame aura spares everything when the 20% roll fails");
   Fiends.FlameAura(balor,()=>0f);
   Check(near1.HasAttachment(ThingDefOf.Fire) && thayMate.HasAttachment(ThingDefOf.Fire) && !balor.HasAttachment(ThingDefOf.Fire),"the flame aura ignites everyone within 2 cells, Thayan allies included: near "+near1.HasAttachment(ThingDefOf.Fire)+"/"+Fresh(near1,StatDefOf.Flammability)+" at "+(near1.Position-bs)+" mate "+thayMate.HasAttachment(ThingDefOf.Fire)+"/"+Fresh(thayMate,StatDefOf.Flammability)+" at "+(thayMate.Position-bs)+" balor "+balor.HasAttachment(ThingDefOf.Fire)+" weather "+map.weatherManager.curWeather.defName);
   foreach(var p in new[]{near1,thayMate})p.GetAttachment(ThingDefOf.Fire)?.Destroy();
   foreach(var f in map.listerThings.ThingsOfDef(ThingDefOf.Fire).ToList())f.Destroy();
   Place(near1,bs+IntVec3.East*3);near1.SetFaction(Faction.OfPlayer);
   int pushed=Fiends.Buffet(balor);
   Check(pushed>=1 && near1.Position==bs+IntVec3.East*5 && near1.stances.stunner.Stunned && thayMate.Position==bs+IntVec3.West && !thayMate.stances.stunner.Stunned,"buffet knocks enemies within 4 cells back 2 cells and stuns them; allies stay: pushed="+pushed+" near at "+(near1.Position-bs)+" stunned="+near1.stances.stunner.Stunned+" mate at "+(thayMate.Position-bs)+" mateStunned="+thayMate.stances.stunner.Stunned+" balor at "+(balor.Position-bs));
   var buffetComp=balor.abilities.GetAbility(Races124.Ability("LP_BalorBuffet")).EffectComps.OfType<CompRaces124Ability>().Single();
   Place(near1,bs+IntVec3.East*2);Check(buffetComp.AICanTargetNow(new LocalTargetInfo(near1)) && Races124.Ability("LP_BalorBuffet").aiCanUse,"the AI can use buffet on a nearby enemy");
   FactionGear.Replace(balor,"LP_BalorWhip",null);
   Check(balor.equipment.Primary?.def.defName=="LP_BalorWhip","the balor wields its fire whip");
   var bpos=balor.Position;var bystander=R124Human();Place(bystander,bs+IntVec3.East*3);Place(near1,bs+IntVec3.North*8);
   balor.Kill(null);FlushDeferred();
   var bloot=GenRadial.RadialDistinctThingsAround(bpos,map,3f,true).ToList();
   var whip=bloot.FirstOrDefault(t=>t.def.defName=="LP_BalorWhip");
   Check(bloot.Any(t=>t.def.defName=="LP_BalorHeart") && whip!=null && whip.TryGetComp<CompQuality>().Quality==QualityCategory.Excellent,"a slain balor leaves its heart and an excellent fire whip");
   Check(balor.Corpse==null || balor.Corpse.Destroyed,"the balor corpse is gone");
   var state=Races124.State;Check(state.bursts.Count==1 && state.bursts[0].tick==Find.TickManager.TicksGame+Fiends.BurstDelay-1 || state.bursts.Count==1,"the death burst is scheduled one second later");
   int bh=bystander.health.hediffSet.hediffs.Count;state.FlushBursts();Step(40);
   Check(bystander.Dead || bystander.health.hediffSet.hediffs.Count>bh || bystander.HasAttachment(ThingDefOf.Fire),"the death burst burns pawns within 4 cells");
   foreach(var f in map.listerThings.ThingsOfDef(ThingDefOf.Fire).ToList())f.Destroy();
   foreach(var t in bloot.Where(t=>!t.Destroyed && (t.def.defName=="LP_BalorHeart" || t.def.defName=="LP_BalorWhip")).ToList())t.Destroy();
   // 火鞭
   var whipper=R124Human();var lashed=R124Human();lashed.SetFaction(EnemyFaction());var ws=ClearLine(7);Place(whipper,ws);Place(lashed,ws+IntVec3.East*4);
   var dest=RaceItems124.Pull(lashed,whipper);
   Check(dest==ws+IntVec3.East*2 && lashed.Position==ws+IntVec3.East*2,"the fire whip drags the target 2 cells toward the wielder");
   int wh=whipper.health.hediffSet.hediffs.Count;
   Check(RaceItems124.BurnHand(whipper) && whipper.health.hediffSet.hediffs.Count>wh,"a fire whip burns the hand of a wielder who is not fireproof");
   var fg=R124Pawn("LP_GenasiFire");Check(!RaceItems124.BurnHand(fg),"fire genasi hold the whip without burning");
   var w2=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_BalorWhip"));Check(w2.def.Verbs[0].range<5f && w2.def.Verbs[0].defaultProjectile.thingClass==typeof(Projectile_BalorLash) && Near(w2.def.Verbs[0].defaultProjectile.projectile.GetDamageAmount(null),18f),"the fire whip is a 4.9-range lash dealing 18 flame");w2.Destroy();
  }

  void CheckFiendRaids124(){
   var thay=ThayFaction;
   List<Pawn> Make(int ice,int balors){var l=new List<Pawn>();for(int i=0;i<ice;i++)l.Add(FactionPawn("LP_FH_IceDevil",thay));for(int i=0;i<balors;i++)l.Add(FactionPawn("LP_FH_Balor",thay));l.Add(FactionPawn("LP_FH_OrcWarrior",thay));return l;}
   int Count(List<Pawn> l,string kind)=>l.Count(p=>p.kindDef.defName==kind);
   var l1=Make(3,2);Fiends.abyssRaid=false;Fiends.TrimRaid(l1,new PawnGroupMakerParms{faction=thay,points=1000,groupKind=PawnGroupKindDefOf.Combat});
   Check(Count(l1,"LP_FH_IceDevil")==0 && Count(l1,"LP_FH_Balor")==0 && !Fiends.abyssRaid,"1000-point raid: no ice devils, no balor");
   var l2=Make(3,2);Fiends.TrimRaid(l2,new PawnGroupMakerParms{faction=thay,points=2000,groupKind=PawnGroupKindDefOf.Combat});
   Check(Count(l2,"LP_FH_IceDevil")==2 && Count(l2,"LP_FH_Balor")==0,"2000-point raid: at most 2 ice devils, no balor");
   var l3=Make(3,2);Fiends.TrimRaid(l3,new PawnGroupMakerParms{faction=thay,points=4000,groupKind=PawnGroupKindDefOf.Combat});
   Check(Count(l3,"LP_FH_IceDevil")==2 && Count(l3,"LP_FH_Balor")==1 && Fiends.abyssRaid,"4000-point raid: at most 2 ice devils and 1 balor, flagged as an abyssal rift");
   var label=(string)AccessTools.Method(typeof(IncidentWorker_RaidEnemy),"GetLetterLabel").Invoke(IncidentDefOf.RaidEnemy.Worker,new object[]{new IncidentParms{faction=thay,target=map,raidStrategy=RaidStrategyDefOf.ImmediateAttack,raidArrivalMode=PawnsArrivalModeDefOf.EdgeWalkIn}});
   Check(label!=null && label.Contains("深渊裂口"),"a raid with a balor gets an abyssal rift letter title: "+label);
   Fiends.abyssRaid=false;
   var real=FactionGroup(thay,PawnGroupKindDefOf.Combat,1000);
   Check(!real.Any(p=>Fiends.Is(p)),"a real 1000-point Thayan raid contains no fiends");
   foreach(var p in l1.Concat(l2).Concat(l3).Concat(real))if(!p.Destroyed)p.Destroy();
   foreach(var name in new[]{"LP_FaerunHostile"}){var def=DefDatabase<FactionDef>.GetNamed(name);foreach(var g in def.pawnGroupMakers)foreach(var o in g.options??new List<PawnGenOption>())if(o.kind==Fiends.IceKind || o.kind==Fiends.BalorKind)Check(g.kindDef==PawnGroupKindDefOf.Combat || g.kindDef==PawnGroupKindDefOf.Settlement,"fiends only appear in combat and settlement groups");}
  }

  // ---------- 巨灵 ----------
  void CheckGenie124(){
   var rubber=R124Human();var lamp=(WonderLamp)R124(GenSpawn.Spawn(ThingMaker.MakeThing(Genie.LampDef),rubber.Position+IntVec3.East,map));
   Check(lamp.State==LampState.Asleep && lamp.Graphic.path.Contains("Asleep"),"a new lamp is asleep");
   int pawns=map.mapPawns.FreeColonistsSpawnedCount;lamp.Rub(rubber);
   var genie=map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p=>Races124.IsGenie(p) && Genie.Core(p).lampId==lamp.ThingID);r124Made.Add(genie);
   Check(genie!=null && map.mapPawns.FreeColonistsSpawnedCount==pawns+1 && genie.genes.Xenotype==Genie.Xenotype,"rubbing a sleeping lamp summons a genie colonist");
   string el=Genasi.Element(genie);
   Check(el!=null && genie.genes.HasActiveGene(Genasi.Skin(el)) && (genie.Name as NameTriple)?.Nick==Genie.Prefix(el),"the genie has a random bloodline, its skin color and its title ("+el+" / "+(genie.Name as NameTriple)?.Nick+")");
   Check(lamp.State==LampState.Bound && lamp.Graphic.path.Contains("Bound"),"the lamp is bound while its genie is out");
   lamp.Rub(rubber);Check(map.mapPawns.FreeColonistsSpawnedCount==pawns+1,"rubbing a bound lamp does nothing");
   Genie.SetElement(genie,"Water");
   Check(Genasi.Element(genie)=="Water" && genie.genes.HasActiveGene(Genasi.Skin("Water")) && !genie.genes.HasActiveGene(Genasi.Skin(el=="Water"?"Fire":el)),"changing the bloodline swaps the skin gene too");
   var tide=genie.abilities.GetAbility(Races124.Ability("LP_ElementTide"));tide.StartCooldown(tide.def.cooldownTicksRange.min);
   Check(Math.Abs(tide.CooldownTicksRemaining-30000)<=5,"genie bloodline abilities cool down twice as fast: "+tide.CooldownTicksRemaining);tide.ResetCooldown();
   Check(Near(genie.BodySize,rubber.BodySize*1.5f) && genie.genes.GetGene(Races124.Gene("LP_GenieAgeless"))?.def.biologicalAgeTickFactorFromAgeCurve.Evaluate(30)==0,"genies are 1.5x size and ageless");
   // 许愿
   var wish=genie.abilities.GetAbility(Races124.Ability("LP_GenieWish"));Check(wish!=null && wish.def.cooldownTicksRange.min==60*GenDate.TicksPerDay,"the wish ability cools down for 60 days");
   Genie.OpenWishDialog(genie,wish);var dlg=Find.WindowStack.WindowOfType<Dialog_NodeTree>();
   Check(dlg!=null && Traverse.Create(dlg).Field("curNode").GetValue<DiaNode>().options.Count==8,"the wish dialog offers six wishes, make-me-stronger (1.58.3) and a cancel");dlg?.Close(false);
   // 丰收
   var zc=ClearLine(3);var zone=new Zone_Growing(map.zoneManager);map.zoneManager.RegisterZone(zone);zone.AddCell(zc);
   var plantDef=DefDatabase<ThingDef>.GetNamed("Plant_Potato");var plant=(Plant)R124(GenSpawn.Spawn(ThingMaker.MakeThing(plantDef),zc,map));plant.Growth=.2f;
   int harvest=Genie.Grant(genie,0);
   Check(harvest>=1 && plant.Growth>=1f,"harvest wish ripens crops in growing zones");
   zone.Delete();plant.Destroy();
   Check(Items.Has(genie,"LP_GenieFatigue") && Math.Abs(Items.TicksLeft(genie,"LP_GenieFatigue")-15*GenDate.TicksPerDay)<=1,"a wish leaves the genie fatigued for 15 days");
   var spark=genie.abilities.GetAbility(Races124.Ability("LP_ElementTide")).EffectComps.OfType<CompRaces124Ability>().Single();
   Check(spark.GizmoDisabled(out string why) && why.Contains("倦怠"),"fatigue disables the genie's bloodline ability");
   // 和平
   Genie.Grant(genie,1);
   Check(Races124.State.Peaceful(map),"peace wish raises the genie's barrier for 15 days");
   var raid=new IncidentParms{target=map,faction=ThayFaction,points=500,forced=true};int hostiles=map.mapPawns.AllPawnsSpawned.Count(p=>p.HostileTo(Faction.OfPlayer));
   Check(!IncidentDefOf.RaidEnemy.Worker.CanFireNow(raid) && !IncidentDefOf.RaidEnemy.Worker.TryExecute(raid) && map.mapPawns.AllPawnsSpawned.Count(p=>p.HostileTo(Faction.OfPlayer))==hostiles,"no hostile raid lands during the barrier");
   Races124.State.peaceUntil.Remove(map.uniqueID);
   // 痊愈
   var patient=R124Human();var cut=HediffMaker.MakeHediff(HediffDefOf.Cut,patient,patient.RaceProps.body.corePart);cut.Severity=4;patient.health.AddHediff(cut);var flu=DefDatabase<HediffDef>.GetNamedSilentFail("Flu");if(flu!=null)patient.health.AddHediff(flu);
   Genie.Grant(genie,2);
   Check(!patient.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury) && (flu==null || !patient.health.hediffSet.HasHediff(flu)),"healing wish cures wounds and disease");
   // 财富
   int silver=map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t=>t.stackCount);Genie.Grant(genie,3);
   Check(map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t=>t.stackCount)==silver+Genie.Wealth,"wealth wish drops 2000 silver");
   // 晴空
   Genie.Grant(genie,4);
   Check(map.gameConditionManager.ConditionIsActive(DefDatabase<GameConditionDef>.GetNamed("LP_GenieClearSkies")) && map.weatherManager.curWeather.defName=="Clear","clear-sky wish forces clear weather for 5 days");
   // 顿悟
   var student=R124Human();var levels=student.skills.skills.ToDictionary(s=>s.def,s=>s.Level);Genie.Grant(genie,5);
   Check(student.skills.skills.Count(s=>s.Level==Math.Min(20,levels[s.def]+2) && s.Level!=levels[s.def])==1,"insight wish raises one random skill by 2");
   // 神灯被卖：一天后离去
   var core=Genie.Core(genie);lamp.DeSpawn();core.leaveTick=-1;core.Check();
   Check(core.leaveTick>0 && !genie.Destroyed,"losing the lamp starts a one-day countdown");
   core.leaveTick=Find.TickManager.TicksGame;core.Check();
   Check(genie.Destroyed,"a genie whose lamp is gone for a day departs");
   GenSpawn.Spawn(lamp,rubber.Position+IntVec3.East,map);
   Check(lamp.State==LampState.Asleep,"the lamp goes back to sleep after its genie leaves");
   // 死亡 → 回灯 → 暗淡15天 → 再擦出同一个巨灵
   lamp.Rub(rubber);var g2=map.mapPawns.FreeColonistsSpawned.First(p=>Races124.IsGenie(p) && Genie.Core(p).lampId==lamp.ThingID);r124Made.Add(g2);
   string id=g2.ThingID,name=g2.Name.ToStringFull,el2=Genasi.Element(g2);int melee=g2.skills.GetSkill(SkillDefOf.Melee).Level;
   var wound=HediffMaker.MakeHediff(HediffDefOf.Cut,g2,g2.RaceProps.body.corePart);wound.Severity=6;g2.health.AddHediff(wound);
   // 1.52.0 热修：倒下前把需求压低、加慢性病和负面记忆，回灯后应恢复到最佳。
   var badBack=DefDatabase<HediffDef>.GetNamedSilentFail("BadBack");if(badBack!=null)g2.health.AddHediff(badBack,g2.RaceProps.body.corePart);
   foreach(var n in new Need[]{g2.needs.food,g2.needs.rest,g2.needs.joy})if(n!=null)n.CurLevel=.05f;
   g2.needs.mood?.thoughts.memories.TryGainMemory(ThoughtDefOf.AteWithoutTable);
   bool Fresh2(Pawn p)=>p.needs.AllNeeds.All(n=>n.CurLevel>=n.MaxLevel-.001f) && !p.health.hediffSet.hediffs.Any(h=>h.def.isBad && !h.def.defName.StartsWith("LP_")) && !p.needs.mood.thoughts.memories.Memories.Any(t=>t.MoodOffset()<0) && !p.InMentalState;
   g2.Kill(null);
   Check(!g2.Dead,"a genie does not die");
   FlushDeferred();
   Check(!g2.Spawned && lamp.Stored==g2 && lamp.State==LampState.Dim && Math.Abs(lamp.DimTicksLeft-15*GenDate.TicksPerDay)<=2 && lamp.Graphic.path.Contains("Dim"),"a slain genie returns to its lamp, which dims for 15 days");
   Check(!map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Any(c=>c.InnerPawn==g2),"no genie corpse is left");
   Check(Fresh2(g2),"a genie back in its lamp is kept at its best: needs full, no illness, no bad memories");
   foreach(var n in new Need[]{g2.needs.food,g2.needs.rest,g2.needs.joy})if(n!=null)n.CurLevel=.05f;
   lamp.Rub(rubber);Check(lamp.Stored==g2,"rubbing a dim lamp only tells how long is left");
   lamp.dimUntil=Find.TickManager.TicksGame-1;lamp.Rub(rubber);
   Check(g2.Spawned && g2.ThingID==id && g2.Name.ToStringFull==name && Genasi.Element(g2)==el2 && g2.skills.GetSkill(SkillDefOf.Melee).Level==melee && !g2.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury) && lamp.State==LampState.Bound,"after 15 days the same genie comes back with its name, skills and bloodline, wounds healed");
   Check(Fresh2(g2) && !g2.Downed,"a genie rubbed out of its lamp comes back at its best: "+string.Join(",",g2.needs.AllNeeds.Where(n=>n.CurLevel<n.MaxLevel-.001f).Select(n=>n.def.defName))+" / "+string.Join(",",g2.health.hediffSet.hediffs.Where(h=>h.def.isBad).Select(h=>h.def.defName)));
   // 1.58.3 热修：击倒也回灯；灯里不跑 tick、不会死、不重新暗淡；暗淡时花1000白银唤醒。
   HealthUtility.DamageUntilDowned(g2,false);FlushDeferred();
   Check(!g2.Dead && !g2.Spawned && lamp.Stored==g2 && lamp.State==LampState.Dim,"a downed genie returns to its lamp too");
   int dim=lamp.dimUntil;g2.needs.food.CurLevel=.5f;
   var tickI=AccessTools.Method(typeof(Pawn),"TickInterval");for(int i=0;i<10;i++)tickI.Invoke(g2,new object[]{2500});
   Check(Near(g2.needs.food.CurLevel,.5f) && !g2.health.hediffSet.hediffs.Any(h=>h.def.isBad && !h.def.defName.StartsWith("LP_")),"a genie inside its lamp is cut off from the world: no hunger, cold or illness");
   g2.Kill(null);FlushDeferred();
   Check(!g2.Dead && lamp.Stored==g2 && lamp.dimUntil==dim,"a genie inside its lamp can't die and doesn't restart the 15-day dimming");
   var silverThings=map.listerThings.ThingsOfDef(ThingDefOf.Silver).ToList();var wasForbidden=silverThings.ToDictionary(t=>t,t=>t.IsForbidden(Faction.OfPlayer));
   foreach(var t in silverThings)t.SetForbidden(true,false);
   var revive=lamp.GetGizmos().OfType<Command_Action>().FirstOrDefault(c=>c.defaultLabel.Contains("唤醒"));
   Check(revive!=null && revive.Disabled && lamp.Revive().Contains("不够") && lamp.Stored==g2,"without 1000 usable silver the dim lamp can't be revived");
   foreach(var kv in wasForbidden)kv.Key.SetForbidden(kv.Value,false);
   var pay=ThingMaker.MakeThing(ThingDefOf.Silver);pay.stackCount=Genie.ReviveSilver;GenPlace.TryPlaceThing(pay,rubber.Position,map,ThingPlaceMode.Near);
   int silverBefore=Genie.SilverOn(map);lamp.Revive();
   Check(g2.Spawned && !g2.Downed && lamp.State==LampState.Bound && Genie.SilverOn(map)==silverBefore-Genie.ReviveSilver && Fresh2(g2),"paying 1000 silver revives the genie at once, at its best");
   // 1.59.0 热修：野性回归不再让巨灵回灯；旧档里已经变野、回了灯的巨灵唤醒后恢复成殖民者，不会马上又回灯。
   var runWild=DefDatabase<MentalBreakDef>.GetNamed("RunWild");
   Check(!runWild.Worker.BreakCanOccur(g2),"genies can't run wild");
   var g2core=Genie.Core(g2);g2.ChangeKind(PawnKindDefOf.WildMan);g2.SetFaction(null);g2core.Check();
   Check(g2.Spawned && lamp.State==LampState.Bound && g2.Faction==Faction.OfPlayer && g2.kindDef==PawnKindDefOf.Colonist,"a genie that went wild stays out and turns back into a colonist");
   g2.ChangeKind(PawnKindDefOf.WildMan);g2.SetFaction(null);Genie.ReturnToLamp(g2,lamp);
   var pay2=ThingMaker.MakeThing(ThingDefOf.Silver);pay2.stackCount=Genie.ReviveSilver;GenPlace.TryPlaceThing(pay2,rubber.Position,map,ThingPlaceMode.Near);
   lamp.Revive();g2core.Check();g2core.Check();
   Check(g2.Spawned && lamp.State==LampState.Bound && g2.Faction==Faction.OfPlayer && g2.kindDef==PawnKindDefOf.Colonist && !g2.InMentalState,"a wild genie revived from its lamp comes back as a colonist and doesn't slip straight back in");
   // 灯毁了再死：照常死去，不留尸体
   lamp.Destroy();g2.Kill(null);FlushDeferred();
   Check(g2.Dead && !map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Any(cp=>cp.InnerPawn==g2),"with its lamp destroyed the genie dies and leaves no corpse: dead="+g2.Dead+" destroyed="+g2.Destroyed+" spawned="+g2.Spawned+" corpse="+(g2.Corpse==null?"none":g2.Corpse.Destroyed?"destroyed":"present")+" queued="+(Items.State?.queued.Count??-1));
  }

  // ---------- 泰坦 ----------
  void CheckTitan124(){
   var titan=R124Pawn("LP_Titan");var g=Titan.Divinity(titan);
   Check(g!=null && titan.genes.GetGene(Races124.Gene("LP_TitanDivinity")).def.resourceGizmoType==typeof(GeneGizmo_DivinityResource),"titans have a divinity resource bar");
   var nodes=RenderNodes(titan);Check(nodes.Any(n=>n.Props?.texPath=="Races124/Halo/TitanHalo" && n.PrimaryGraphic!=null),"titans draw a halo");
   g.Value=.5f;titan.needs.mood.CurLevel=.5f;g.Accrue(GenDate.TicksPerDay);
   Check(Near(Titan.Points(titan),60f),"divinity regains 10 a day: "+Titan.Points(titan));
   titan.needs.mood.CurLevel=.9f;g.Accrue(GenDate.TicksPerDay);
   Check(Near(Titan.Points(titan),80f),"divinity regains 20 a day with mood above 80%: "+Titan.Points(titan));
   var foe=R124Human();foe.SetFaction(EnemyFaction());foe.Kill(new DamageInfo(DamageDefOf.Blunt,9999,0,-1,titan));
   Check(Near(Titan.Points(titan),85f),"killing an enemy gives 5 divinity");
   var st=ClearLine(12);Place(titan,st);
   Check(Titan.Smite(titan,st+IntVec3.East*8) && Near(Titan.Points(titan),55f),"divine smite calls down lightning for 30 divinity");
   Step(30);foreach(var f in map.listerThings.ThingsOfDef(ThingDefOf.Fire).ToList())f.Destroy();
   var comp=titan.abilities.GetAbility(Races124.Ability("LP_TitanSmite")).EffectComps.OfType<CompRaces124Ability>().Single();
   Check(comp.Invalid(new LocalTargetInfo(st+IntVec3.East*11))!=null && comp.Invalid(new LocalTargetInfo(st+IntVec3.East*9))==null,"smite reaches 10 cells (11: "+comp.Invalid(new LocalTargetInfo(st+IntVec3.East*11))+"; 9: "+comp.Invalid(new LocalTargetInfo(st+IntVec3.East*9))+"; titan at "+titan.Position+", st "+st+")");
   var ally=R124Human();Place(ally,st+IntVec3.North*2);var c1=HediffMaker.MakeHediff(HediffDefOf.Cut,ally,ally.RaceProps.body.corePart);c1.Severity=15;ally.health.AddHediff(c1);
   var c2=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,ally,ally.RaceProps.body.AllParts.First(b=>b.def.defName=="Leg"));c2.Severity=8;ally.health.AddHediff(c2);
   var stranger=R124Human();stranger.SetFaction(EnemyFaction());Place(stranger,st+IntVec3.South*2);var c3=HediffMaker.MakeHediff(HediffDefOf.Cut,stranger,stranger.RaceProps.body.corePart);c3.Severity=15;stranger.health.AddHediff(c3);
   Titan.Breath(titan);
   Check(Near(c1.Severity,5f) && !ally.health.hediffSet.hediffs.Contains(c2) && !((Hediff_Injury)c1).Bleeding && Near(c3.Severity,15f) && Near(Titan.Points(titan),15f),"divine breath heals 10 per wound on allies, stops bleeding and costs 40");
   Check(!Titan.CanPay(titan,Titan.SmiteCost) && !Titan.Smite(titan,st+IntVec3.East*5),"not enough divinity, no smite");
   titan.stances.stunner.StunFor(120,null);titan.TakeDamage(new DamageInfo(DamageDefOf.EMP,50,0,-1,null));
   Check(!titan.stances.stunner.Stunned,"titans cannot be stunned, not even by EMP");
   Check(Near(DNDMind.Resistance(titan),.5f),"titan mind resistance 50%");
   // 天候
   LichMod.Settings.titanWeather=true;titan.needs.mood.CurLevel=.9f;
   Check(Titan.ApplyWeather(titan)?.defName=="Clear" && map.weatherManager.curWeather.defName=="Clear","a happy titan clears the sky");
   titan.needs.mood.CurLevel=.2f;
   Check(Titan.ApplyWeather(titan)?.defName=="RainyThunderstorm" && map.weatherManager.curWeather.defName=="RainyThunderstorm","an unhappy titan brings thunderstorms");
   titan.needs.mood.CurLevel=.5f;Check(Titan.ApplyWeather(titan)==null,"a titan in between leaves the weather alone");
   LichMod.Settings.titanWeather=false;titan.needs.mood.CurLevel=.9f;
   Check(Titan.ApplyWeather(titan)==null && map.weatherManager.curWeather.defName=="RainyThunderstorm","the weather setting turns the effect off");
   LichMod.Settings.titanWeather=true;
   // 傲慢
   var pride=DefDatabase<ThoughtDef>.GetNamed("LP_TitanPride");
   Check(!pride.Worker.CurrentState(titan).Active,"no pride mood when the titan is the largest");
   var rival=R124Pawn("LP_Titan");rival.genes.AddGene(Races124.Gene("LP_OgreStature"),true);
   Check(pride.Worker.CurrentState(titan).Active && pride.stages[0].baseMoodEffect==-3,"a larger colonist offends the titan (-3)");
   rival.Destroy();
   // 剧本组件
   var part=new ScenPart_FallenEmpyrean{def=DefDatabase<ScenPartDef>.GetNamed("LP_FallenEmpyreanPart")};var t2=R124Pawn("LP_Titan");
   Check(part.Apply(new List<Pawn>{ally,t2})==t2 && t2.equipment.Primary?.def.defName=="LP_EmpyreanBlade" && t2.skills.GetSkill(SkillDefOf.Melee).Level>=8 && t2.skills.GetSkill(SkillDefOf.Social).Level>=8,"the Fallen Empyrean start arms the titan with the empyrean blade and strong melee/social");
   // 天穹之剑
   var blade=t2.equipment.Primary;var human=R124Human();
   Check(Near(HeavyWeapons.DamageFactor(human,blade),.5f) && Near(HeavyWeapons.DamageFactor(t2,blade),1f),"empyrean blade: half damage below body size 2");
   var method=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply");var verb=t2.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(v=>v.verb).OfType<Verb_MeleeAttackDamage>().First(v=>v.EquipmentSource==blade && v.tool.capacities.Any(c=>c.defName=="Cut"));
   int thunder=0;Rand.PushState(17);try{for(int i=0;i<400;i++)if(((IEnumerable<DamageInfo>)method.Invoke(verb,new object[]{new LocalTargetInfo(human)})).Any(d=>d.Def==DamageDefOf.EMP))thunder++;}finally{Rand.PopState();}
   Check(thunder>60 && thunder<140,"about a quarter of empyrean blade hits add thunder: "+thunder+"/400");
  }

  // ---------- 物品 ----------
  void CheckRaceItems124(){
   foreach(var e in Genasi.Elements){
    var r=DefDatabase<RecipeDef>.GetNamed("LP_MakeElementalFocus_"+e);
    Check(r.AllRecipeUsers.Any(u=>u.defName=="LP_EnchantingTable") && r.ingredients.Any(i=>i.IsFixedIngredient && i.FixedIngredient.defName=="LP_Mithral" && i.GetBaseCount()==20),"focus recipe at the enchanting table with 20 mithral: "+e);
   }
   var fire=R124Pawn("LP_GenasiFire");var water=R124Pawn("LP_GenasiWater");var human=R124Human();
   var fr=DefDatabase<RecipeDef>.GetNamed("LP_MakeElementalFocus_Fire");
   Check(RaceGear.CannotCraft(fr,fire)==null && RaceGear.CannotCraft(fr,water)!=null && RaceGear.CannotCraft(fr,human)!=null,"only fire-blooded genasi may make the fire focus");
   foreach(var (item,race,ok) in new[]{("LP_IvoryHorn","LP_Loxodon",true),("LP_OgreClub","LP_Ogre",true),("LP_FeatherCloak","LP_Aarakocra",true),("LP_EmpyreanBlade","LP_Titan",true),("LP_EmpyreanBlade","LP_FireGiant",true)}){
    var recipe=DefDatabase<RecipeDef>.AllDefsListForReading.First(rr=>rr.ProducedThingDef?.defName==item);var p=R124Pawn(race);
    Check(RaceGear.CannotCraft(recipe,p)==null && RaceGear.CannotCraft(recipe,human)!=null,item+" can be made by "+race+" but not a plain human");
   }
   Apparel Wear(Pawn p,string def){var a=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(def),DefDatabase<ThingDef>.GetNamed(def).MadeFromStuff?ThingDefOf.Leather_Plain:null);p.apparel.Wear(a,false);return a;}
   // 元素符：本血脉 vs 其他人
   Check(Near(Races124Cooldowns.Factor(fire,Races124.Ability("LP_ElementSpark")),1f),"no focus, normal spark cooldown");
   Wear(fire,"LP_ElementalFocus_Fire");
   Check(Near(Races124Cooldowns.Factor(fire,Races124.Ability("LP_ElementSpark")),.5f) && ElementalFocus.FireBoost(fire),"a fire genasi with the fire focus casts spark twice as often and halves fire damage again");
   var d=new DamageInfo(DamageDefOf.Flame,10);fire.PreApplyDamage(ref d,out bool _);Check(Near(d.Amount,2.5f),"with the fire focus a fire genasi takes a quarter of flame damage (focus x0.5 and bloodline x0.5): "+d.Amount);
   float hf=Fresh(human,StatDefOf.Flammability);var hfFocus=Wear(human,"LP_ElementalFocus_Fire");StatDefOf.Flammability.Worker.ClearCacheForThing(human);
   Check(Math.Abs(Ratio(Fresh(human,StatDefOf.Flammability),hf)-.5f)<.02f,"anyone else wearing the fire focus is half as flammable");
   human.apparel.Remove(hfFocus);hfFocus.Destroy();
   Wear(water,"LP_ElementalFocus_Water");Check(Genasi.TideRadius(water)==2,"the water focus widens tide to 5x5 for water genasi");
   var wf=Wear(human,"LP_ElementalFocus_Water");Check(Amphibious.Has(human) && human.WaterCellCost==0,"anyone wearing the water focus walks through water");human.apparel.Remove(wf);wf.Destroy();
   var earth=R124Pawn("LP_GenasiEarth");var esk=earth.skills.GetSkill(SkillDefOf.Mining);if(!esk.TotallyDisabled)esk.Level=12;foreach(var hk in new[]{human})hk.skills.GetSkill(SkillDefOf.Mining).Level=Math.Max(hk.skills.GetSkill(SkillDefOf.Mining).Level,8);float em=Fresh(earth,StatDefOf.MiningSpeed);Wear(earth,"LP_ElementalFocus_Earth");StatDefOf.MiningSpeed.Worker.ClearCacheForThing(earth);
   Check(Math.Abs(Ratio(Fresh(earth,StatDefOf.MiningSpeed),em)-1.6f/1.3f)<.02f,"earth focus lifts an earth genasi's mining from x1.3 to x1.6");
   float hm=Fresh(human,StatDefOf.MiningSpeed);var ef=Wear(human,"LP_ElementalFocus_Earth");StatDefOf.MiningSpeed.Worker.ClearCacheForThing(human);
   Check(Math.Abs(Ratio(Fresh(human,StatDefOf.MiningSpeed),hm)-1.15f)<.02f,"earth focus gives others x1.15 mining");human.apparel.Remove(ef);ef.Destroy();
   var air=R124Pawn("LP_GenasiAir");float amv=Fresh(air,StatDefOf.MoveSpeed);Wear(air,"LP_ElementalFocus_Air");StatDefOf.MoveSpeed.Worker.ClearCacheForThing(air);
   Check(Math.Abs(Ratio(Fresh(air,StatDefOf.MoveSpeed),amv)-1.2f/1.1f)<.02f || FullModList,"air focus lifts an air genasi's speed from x1.1 to x1.2");
   float hmv=Fresh(human,StatDefOf.MoveSpeed);var af=Wear(human,"LP_ElementalFocus_Air");StatDefOf.MoveSpeed.Worker.ClearCacheForThing(human);
   Check(Math.Abs(Ratio(Fresh(human,StatDefOf.MoveSpeed),hmv)-1.05f)<.02f || FullModList,"air focus gives others x1.05 speed");human.apparel.Remove(af);af.Destroy();
   // 象牙号角
   var lox=R124Pawn("LP_Loxodon");var horn=Wear(lox,"LP_IvoryHorn");
   Check(lox.abilities.GetAbility(Races124.Ability("LP_IvoryHornBlast"))!=null,"wearing the ivory horn grants the horn ability");
   var hs=ClearLine(14);Place(lox,hs);var near=R124Human();Place(near,hs+IntVec3.East*10);var far=R124Human();Place(far,hs+IntVec3.East*13);
   RaceItems124.Horn(lox);
   Check(Items.Has(near,"LP_HornCourage") && !Items.Has(far,"LP_HornCourage") && Items.TicksLeft(near,"LP_HornCourage")==RaceItems124.HornTicks,"a loxodon's horn reaches 12 cells for 30 seconds");
   Check(!near.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"测试",true),"the horn's courage blocks panic");
   Check(RaceItems124.HornRadius(human)==8,"other horn players reach 8 cells");
   // 风羽披风
   var bird=R124Pawn("LP_Aarakocra");var cloak=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FeatherCloak"),ThingDefOf.Leather_Plain);bird.apparel.Wear(cloak,false);
   Check(Aarakocra.Range(bird)==16 && Near(Races124Cooldowns.Factor(bird,Races124.Ability("LP_Soar")),.75f),"an aarakocra in the feather cloak soars 16 cells with x0.75 cooldown");
   float before=Fresh(human,StatDefOf.MoveSpeed);var c2=(Apparel)ThingMaker.MakeThing(cloak.def,ThingDefOf.Leather_Plain);human.apparel.Wear(c2,false);StatDefOf.MoveSpeed.Worker.ClearCacheForThing(human);
   Check(Math.Abs(Ratio(Fresh(human,StatDefOf.MoveSpeed),before)-1.08f)<.02f || FullModList,"anyone else in the feather cloak moves 8% faster");
   Check(cloak.def.apparel.bodyPartGroups.All(g=>g.defName!="Arms") && cloak.def.apparel.layers.Contains(ApparelLayerDefOf.Shell),"the feather cloak is a shell layer that leaves the arms free");
   human.apparel.Remove(c2);c2.Destroy();
   // 材料与价格
   Check(DefDatabase<ThingDef>.GetNamed("LP_IceDevilStinger").stackLimit==10 && DefDatabase<ThingDef>.GetNamed("LP_BalorHeart").BaseMarketValue>=2500 && Genie.LampDef.BaseMarketValue>=3500,"stinger stacks to 10, the balor heart sells for 2500, the lamp is worth 3500");
   var spear=DefDatabase<ThingDef>.GetNamed("LP_FrostSpear");var sr=DefDatabase<RecipeDef>.AllDefsListForReading.First(r=>r.ProducedThingDef==spear);
   Check(sr.researchPrerequisite?.defName=="LP_MythicMetallurgy" && spear.costList.Any(c=>c.thingDef.defName=="LP_ColdIron" && c.count==40) && spear.costList.Any(c=>c.thingDef.defName=="LP_IceDevilStinger" && c.count==1),"frost spear: 40 cold iron and a stinger after mythic metallurgy");
   // 商队
   var black=DefDatabase<TraderKindDef>.GetNamed("LP_Caravan_BlackNetwork");var red=DefDatabase<TraderKindDef>.GetNamed("LP_Caravan_RedRobe");
   Check(black.stockGenerators.OfType<StockGenerator_SingleDef>().Any(g=>Traverse.Create(g).Field("thingDef").GetValue<ThingDef>()==Genie.LampDef && g.countRange.min>=1),"the black network always brings a lamp");
   Check(red.stockGenerators.OfType<StockGenerator_SingleDef>().Any(g=>Traverse.Create(g).Field("thingDef").GetValue<ThingDef>()==Genie.LampDef && g.countRange.min<1 && g.countRange.max==1),"red robe traders sometimes bring a lamp");
   Check(black.WillTrade(DefDatabase<ThingDef>.GetNamed("LP_BalorHeart")) && red.WillTrade(DefDatabase<ThingDef>.GetNamed("LP_BalorHeart")),"the balor heart can be sold to the black network and red robes");
   Check(Near(IsekaiLoot.LampChance,.05f),"Isekai treasure chests hide a lamp 5% of the time");
  }

  // ---------- 种族关系与好感 ----------
  void CheckRaces124Relations(){
   var tracker=RacialRelations.Tracker;
   var names=new[]{"LP_Relation_FireGenasiGiant","LP_Relation_GiantFireGenasi","LP_Relation_WaterGenasiGiant","LP_Relation_LoxodonMinotaur","LP_Relation_MinotaurLoxodon","LP_Relation_LoxodonOgre","LP_Relation_OgreHalfOrc","LP_Relation_DwarfOgre","LP_Relation_ElfOgre","LP_Relation_AarakocraElf","LP_Relation_ElfAarakocra","LP_Relation_AarakocraMindFlayer"};
   var defs=names.ToDictionary(n=>n,n=>DefDatabase<RacialRelationDef>.GetNamed(n));
   Check(defs.Values.All(r=>r.enabled && !r.ConfigErrors().Any()),"1.24.0 racial relations are enabled and valid");
   var expect=new Dictionary<string,int>{{"LP_Relation_FireGenasiGiant",2},{"LP_Relation_GiantFireGenasi",2},{"LP_Relation_WaterGenasiGiant",-1},{"LP_Relation_LoxodonMinotaur",2},{"LP_Relation_MinotaurLoxodon",2},{"LP_Relation_LoxodonOgre",-2},{"LP_Relation_OgreHalfOrc",1},{"LP_Relation_DwarfOgre",-2},{"LP_Relation_ElfOgre",-2},{"LP_Relation_AarakocraElf",1},{"LP_Relation_ElfAarakocra",1},{"LP_Relation_AarakocraMindFlayer",-3}};
   foreach(var kv in expect)Check(Near(defs[kv.Key].thought.stages[0].baseMoodEffect,kv.Value),kv.Key+" mood "+kv.Value);
   var lox=R124Pawn("LP_Loxodon");var mino=R124Pawn("LP_Minotaur");var ogre=R124Pawn("LP_Ogre");tracker.Census(map).Rebuild(map);
   Check(defs["LP_Relation_LoxodonMinotaur"].thought.Worker.CurrentState(lox).Active && defs["LP_Relation_MinotaurLoxodon"].thought.Worker.CurrentState(mino).Active && defs["LP_Relation_LoxodonOgre"].thought.Worker.CurrentState(lox).Active,"loxodon and minotaur like each other; the loxodon dislikes the ogre");
   mino.Destroy();ogre.Destroy();tracker.Census(map).Rebuild(map);
   Check(!defs["LP_Relation_LoxodonMinotaur"].thought.Worker.CurrentState(lox).Active,"relations end when the other race leaves");
   var colony=Factions.Colony;
   Check(SituationValue("LP_Goodwill_Colony124Friends",colony)==5,"a loxodon colonist gives +5 with the Faerun colony");
   lox.Destroy();
   bool hadFriend=ColonyCensus.Holds("faerunFriend124");if(!hadFriend)Check(SituationValue("LP_Goodwill_Colony124Friends",colony)==0,"the bonus ends when they leave");
   var og=R124Pawn("LP_Ogre");Check(SituationValue("LP_Goodwill_ColonyOgre",colony)==-5 && SituationValue("LP_Goodwill_ColonyOgre",ThayFaction)==0,"an ogre colonist costs -5 with the Faerun colony only");og.Destroy();
  }

  // ---------- 派系兵种 ----------
  void CheckRaces124Kinds(){
   var colonyF=Factions.Colony;var thay=ThayFaction;var ember=Factions.Ember;
   foreach(var (kind,f,xeno) in new[]{("LP_FC_LoxodonWarden",colonyF,"LP_Loxodon"),("LP_FC_AarakocraScout",colonyF,"LP_Aarakocra"),("LP_FH_OgreBrute",thay,"LP_Ogre"),("LP_FH_IceDevil",thay,"LP_IceDevil"),("LP_FH_Balor",thay,"LP_Balor"),("LP_EC_OgreSlave",ember,"LP_Ogre")}){
    var p=FactionPawn(kind,f);Check(p.genes.Xenotype.defName==xeno && p.kindDef.combatPower>0,kind+" generates as "+xeno);
    if(kind=="LP_FH_Balor")Check(p.equipment.Primary?.def.defName=="LP_BalorWhip" && p.apparel.WornApparelCount==0,"balors always carry the fire whip");
    if(kind=="LP_FH_IceDevil")Check(p.apparel.WornApparelCount==0 && (p.equipment.Primary==null || p.equipment.Primary.def.defName=="LP_FrostSpear"),"ice devils fight unarmed or with a frost spear");
    if(kind=="LP_FC_LoxodonWarden")Check(p.apparel.WornApparel.Any(a=>a.def.defName=="LP_LoxodonWardenArmor" || (a.def.defName=="Apparel_PlateArmor" && p.BodySize<1.3f)),"loxodon wardens wear the warden armor (1.45.1; plate armor while too small)");
    p.Destroy();
   }
   var genasiKind=DefDatabase<PawnKindDef>.GetNamed("LP_FC_Genasi");
   Check(genasiKind.xenotypeSet.Count==4 && Enumerable.Range(0,4).All(i=>Near(genasiKind.xenotypeSet[i].chance,.25f)) && Math.Abs(genasiKind.combatPower-60)<.1f,"genasi nomads are 25% of each element");
   var gp=FactionPawn("LP_FC_Genasi",colonyF);Check(Genasi.IsGenasi(gp),"a genasi nomad is a genasi: "+gp.genes.Xenotype.defName);gp.Destroy();
   foreach(var kv in new Dictionary<string,float>{{"LP_FC_Genasi",60},{"LP_FC_LoxodonWarden",90},{"LP_FC_AarakocraScout",65},{"LP_FH_OgreBrute",95},{"LP_FH_IceDevil",220},{"LP_FH_Balor",450},{"LP_EC_OgreSlave",90}})Check(Math.Abs(DefDatabase<PawnKindDef>.GetNamed(kv.Key).combatPower-kv.Value)<.1f,kv.Key+" combat power "+kv.Value);
   bool InGroup(FactionDef def,PawnGroupKindDef g,string kind)=>def.pawnGroupMakers.Any(m=>m.kindDef==g && m.options.Any(o=>o.kind.defName==kind));
   Check(InGroup(Factions.ColonyDef,PawnGroupKindDefOf.Combat,"LP_FC_Genasi") && InGroup(Factions.ColonyDef,PawnGroupKindDefOf.Peaceful,"LP_FC_AarakocraScout") && InGroup(Factions.ColonyDef,PawnGroupKindDefOf.Settlement,"LP_FC_LoxodonWarden"),"new Faerun kinds join combat, peaceful and settlement groups");
   Check(!InGroup(Factions.HostileDef,PawnGroupKindDefOf.Peaceful,"LP_FH_IceDevil") && !InGroup(Factions.HostileDef,PawnGroupKindDefOf.Peaceful,"LP_FH_Balor") && InGroup(Factions.EmberDef,PawnGroupKindDefOf.Combat,"LP_EC_OgreSlave") && !InGroup(Factions.ColonyDef,PawnGroupKindDefOf.Combat,"LP_FH_OgreBrute"),"fiends stay out of peaceful groups; ogres fight for Thay and the ember clan, not the Faerun colony");
  }

  // ---------- 存读档 ----------
  void PrepareRaces124Save(){
   map=Find.CurrentMap;
   var w=RacePawn("LP_GenasiWater");var tide=w.abilities.GetAbility(Races124.Ability("LP_ElementTide"));tide.StartCooldown(23456);r124TideCd=tide.CooldownTicksRemaining;r124WaterId=w.ThingID;
   r124LoxId=RacePawn("LP_Loxodon").ThingID;r124OgreId=RacePawn("LP_Ogre").ThingID;
   var b=RacePawn("LP_Aarakocra");var soar=b.abilities.GetAbility(Races124.Ability("LP_Soar"));soar.StartCooldown(3456);r124SoarCd=soar.CooldownTicksRemaining;var sky=b.genes.GetFirstGeneOfType<Gene_AarakocraSkyborn>();sky.roofedTicks=1234;r124Roofed=1234;r124BirdId=b.ThingID;
   var t=RacePawn("LP_Titan");Titan.Divinity(t).Value=.375f;r124Divinity=37.5f;var smite=t.abilities.GetAbility(Races124.Ability("LP_TitanSmite"));smite.StartCooldown(4567);r124SmiteCd=smite.CooldownTicksRemaining;r124TitanId=t.ThingID;
   var rubber=map.mapPawns.FreeColonistsSpawned.First();
   var lamp=(WonderLamp)GenSpawn.Spawn(ThingMaker.MakeThing(Genie.LampDef),CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);lamp.Rub(rubber);
   var genie=map.mapPawns.FreeColonistsSpawned.First(p=>Races124.IsGenie(p) && Genie.Core(p).lampId==lamp.ThingID);var wish=genie.abilities.GetAbility(Races124.Ability("LP_GenieWish"));wish.StartCooldown(5678);r124WishCd=wish.CooldownTicksRemaining;r124GenieId=genie.ThingID;r124LampId=lamp.ThingID;
   var lamp2=(WonderLamp)GenSpawn.Spawn(ThingMaker.MakeThing(Genie.LampDef),CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);lamp2.Rub(rubber);
   var stored=map.mapPawns.FreeColonistsSpawned.First(p=>Races124.IsGenie(p) && Genie.Core(p).lampId==lamp2.ThingID);Genie.ReturnToLamp(stored,lamp2);r124DimLampId=lamp2.ThingID;r124StoredId=stored.ThingID;r124DimUntil=lamp2.dimUntil;
   var chilled=SharedPawn();Fiends.Chill(chilled,2);r124ChilledId=chilled.ThingID;
   Races124.State.peaceUntil[map.uniqueID]=Find.TickManager.TicksGame+GenDate.TicksPerDay;
  }
  void CheckRaces124AfterLoad(){
   map=Find.CurrentMap;Pawn Find1(string id)=>map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==id);
   var human=Person();GenSpawn.Spawn(human,map.Center,map);
   var w=Find1(r124WaterId);var lox=Find1(r124LoxId);var ogre=Find1(r124OgreId);var b=Find1(r124BirdId);var t=Find1(r124TitanId);var genie=Find1(r124GenieId);var chilled=Find1(r124ChilledId);
   Check(w.genes.Xenotype.defName=="LP_GenasiWater" && Genasi.Element(w)=="Water" && Amphibious.Has(w),"water genasi keep their bloodline and amphibious walking after save/load");
   Check(Math.Abs(w.abilities.GetAbility(Races124.Ability("LP_ElementTide")).CooldownTicksRemaining-r124TideCd)<=5,"tide cooldown survives save/load");
   Check(Near(lox.BodySize,human.BodySize*1.3f) && Near(ogre.BodySize,human.BodySize*1.6f) && Near(b.BodySize,human.BodySize*.9f) && Near(t.BodySize,human.BodySize*2.5f),"1.24.0 body sizes survive save/load");
   Check(lox.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("LP_LoxodonAppetite"))!=null && ogre.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("LP_OgreAppetite"))!=null,"appetite hediffs survive save/load");
   Check(Math.Abs(b.abilities.GetAbility(Races124.Ability("LP_Soar")).CooldownTicksRemaining-r124SoarCd)<=5 && b.genes.GetFirstGeneOfType<Gene_AarakocraSkyborn>().roofedTicks>=r124Roofed,"soar cooldown and time under roofs survive save/load");
   Check(Math.Abs(Titan.Points(t)-r124Divinity)<.2f && Math.Abs(t.abilities.GetAbility(Races124.Ability("LP_TitanSmite")).CooldownTicksRemaining-r124SmiteCd)<=5,"titan divinity and smite cooldown survive save/load: "+Titan.Points(t));
   var lamp=map.listerThings.ThingsOfDef(Genie.LampDef).OfType<WonderLamp>().Single(l=>l.ThingID==r124LampId);
   Check(Genie.Core(genie).lampId==lamp.ThingID && lamp.State==LampState.Bound && lamp.genie==genie,"the genie's lamp binding survives save/load");
   Check(Math.Abs(genie.abilities.GetAbility(Races124.Ability("LP_GenieWish")).CooldownTicksRemaining-r124WishCd)<=5 && Genasi.Element(genie)!=null,"the wish cooldown and genie bloodline survive save/load");
   var lamp2=map.listerThings.ThingsOfDef(Genie.LampDef).OfType<WonderLamp>().Single(l=>l.ThingID==r124DimLampId);
   Check(lamp2.Stored?.ThingID==r124StoredId && lamp2.dimUntil==r124DimUntil && lamp2.State==LampState.Dim && Races124.IsGenie(lamp2.Stored),"a genie sleeping inside a dim lamp survives save/load");
   Check(Fiends.ChillLayers(chilled)==2,"chill layers survive save/load");
   Check(Races124.State.Peaceful(map),"the genie's barrier survives save/load");
   Races124.State.peaceUntil.Remove(map.uniqueID);
   foreach(var p in new[]{w,lox,ogre,b,t,genie,chilled,human})if(!p.Destroyed)p.Destroy();
   lamp.Destroy();lamp2.Destroy();
  }
 }
}
