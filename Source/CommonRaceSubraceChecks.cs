using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 // 1.57.0：Test.ps1 -Only CommonRaces（25 项，含存读档 1 项）。1.16.0 的半兽人/精灵/矮人检查改名为 -Only CommonRaces116。
 public partial class LichChecks {
  static readonly string[] subraces={"LP_Elf","LP_WoodElf","LP_Drow","LP_Dwarf","LP_HillDwarf","LP_HalflingLightfoot","LP_HalflingStout","LP_RockGnome","LP_ForestGnome","LP_DeepGnome","LP_HalfElf","LP_Kobold","LP_Goblin","LP_Hobgoblin","LP_Bugbear"};
  static readonly string[] newSubraces={"LP_WoodElf","LP_Drow","LP_HillDwarf","LP_HalflingLightfoot","LP_HalflingStout","LP_RockGnome","LP_ForestGnome","LP_DeepGnome","LP_HalfElf","LP_Kobold","LP_Goblin","LP_Hobgoblin","LP_Bugbear"};
  static string crGrudgeFaction,crHalflingId,crHalfElfId,crDwarfId;static int crGrudgeEnd,crCooldownLeft,crTrapId;static string crTalentA,crTalentB;
  readonly List<Thing> crTemp=new List<Thing>();
  Pawn CrPawn(string xeno,IntVec3? at=null){var p=RacePawn(xeno);if(at.HasValue && at.Value.InBounds(map) && at.Value.Standable(map))p.Position=at.Value;p.Notify_Teleported();crTemp.Add(p);p.drafter.Drafted=true;return p;}
  Pawn CrHuman(IntVec3 at,Faction f=null){var p=Person();GenSpawn.Spawn(p,at,map);if(f!=null)p.SetFaction(f);else if(p.drafter!=null)p.drafter.Drafted=true;crTemp.Add(p);return p;}
  void CrClean(){foreach(var t in crTemp.ToList()){if(t is Pawn p && p.Dead){p.Corpse?.Destroy();continue;}if(!t.Destroyed)t.Destroy();}crTemp.Clear();Subraces.ClearCaches();}
  // 找一个周围 8 格都能站、3.5 格内没有别的小人的格子：邻近计数类检查不受地图上原有殖民者干扰。
  IntVec3 CrCell(int dx,int dz){
   var c=map.Center+new IntVec3(dx,0,dz);
   foreach(var x in GenRadial.RadialCellsAround(c.InBounds(map)?c:map.Center,14f,true)){
    if(!x.InBounds(map) || !x.Standable(map))continue;bool ok=true;
    for(int i=0;i<8 && ok;i++){var a=x+GenAdj.AdjacentCells[i];if(!a.InBounds(map) || !a.Standable(map))ok=false;}
    if(ok)foreach(var a in GenRadial.RadialCellsAround(x,3.5f,true))if(a.InBounds(map) && a.GetFirstPawn(map)!=null){ok=false;break;}
    if(ok)return x;
   }
   return c.InBounds(map)?c:map.Center;
  }
  void CrRoof(IntVec3 c,RoofDef r){map.roofGrid.SetRoof(c,r);}

  void CheckCommonRaces(){
   Talents.StripAll=true;
   try{
    CrDefs();CrSizes();CrHalfling();CrDrow();CrDwarf();CrHalfElf();CrKobold();CrGoblinoids();CrGnomes();CrIntegration();
   }finally{CrClean();}
  }

  // 1-3：异种型、图标、生成
  void CrDefs(){
   var bad=new List<string>();var mets=new List<string>();
   foreach(var n in subraces){
    var x=DefDatabase<XenotypeDef>.GetNamedSilentFail(n);if(x==null){bad.Add(n+" missing");continue;}
    int met=x.genes.Sum(g=>g.biostatMet);mets.Add(n+"="+met);
    if(!x.inheritable || x.genes.Any(g=>g==null) || x.ConfigErrors().Any() || met<-2 || met>2 || x.genes.Any(g=>g.biostatArc>0))bad.Add(n);
    foreach(var g in x.genes.Where(g=>g.defName.StartsWith("LP_")))if(g.ConfigErrors().Any() || g.modExtensions!=null && g.modExtensions.Any(e=>e.ConfigErrors().Any()))bad.Add(g.defName);
   }
   Check(bad.Count==0 && DefDatabase<XenotypeDef>.GetNamed("LP_Elf").label=="高等精灵" && DefDatabase<XenotypeDef>.GetNamed("LP_Dwarf").label=="山地矮人","15 common-race xenotypes are inheritable, valid and within metabolism -2..+2 ("+string.Join(" ",mets)+"); old LP_Elf/LP_Dwarf keep their defNames as 高等精灵/山地矮人: "+string.Join(",",bad));
   var tex=new List<string>();
   foreach(var n in subraces){var x=DefDatabase<XenotypeDef>.GetNamed(n);tex.Add(x.iconPath);tex.AddRange(x.genes.Where(g=>g.defName.StartsWith("LP_")).Select(g=>g.iconPath));}
   tex.Add(DefDatabase<AbilityDef>.GetNamed("LP_DrowDarkness").iconPath);tex.Add("CommonRaces/Things/ClockworkToy");tex.Add("CommonRaces/Things/KoboldSpikePit");
   foreach(var s in Subraces.TalentSkills)tex.Add(Subraces.G("LP_HalfElfTalentA_"+s).iconPath);
   var missing=tex.Distinct().Where(t=>ContentFinder<Texture2D>.Get(t,false)==null).ToList();
   var toy=DefDatabase<RecipeDef>.GetNamed("LP_MakeClockworkToy");
   Check(missing.Count==0 && toy.recipeUsers.Any(u=>u.defName=="LP_RuneAnvil") && !toy.AllRecipeUsers.Any(u=>u.defName=="FueledSmithy" || u.defName=="ElectricSmithy") && toy.researchPrerequisite==null && DefDatabase<ThingDef>.GetNamed("LP_ClockworkToy").GetStatValueAbstract(StatDefOf.Beauty)>=3f && DefDatabase<JoyGiverDef>.GetNamed("LP_PlayClockworkToy").thingDefs.Contains(DefDatabase<ThingDef>.GetNamed("LP_ClockworkToy")),"xenotype, gene, ability, toy and spike pit textures load ("+tex.Distinct().Count()+"); clockwork toy is a beauty-3 joy building made at the rune anvil without research: "+string.Join(",",missing)+" users="+string.Join("/",toy.recipeUsers.Select(u=>u.defName))+" research="+toy.researchPrerequisite+" beauty="+DefDatabase<ThingDef>.GetNamed("LP_ClockworkToy").GetStatValueAbstract(StatDefOf.Beauty));
   var genBad=new List<string>();
   foreach(var n in subraces){
    var x=DefDatabase<XenotypeDef>.GetNamed(n);var p=CrPawn(n);var look=x.GetModExtension<RaceLookExtension>();
    if(p.genes.Xenotype!=x || !x.genes.All(g=>p.genes.GetGene(g)!=null) || x.genes.Where(g=>g.defName.StartsWith("LP_")).Any(g=>!p.genes.GetGene(g).Active))genBad.Add(n+" genes");
    if(look!=null && (!look.hairGenes.NullOrEmpty() && !p.genes.GenesListForReading.Any(g=>look.hairGenes.Contains(g.def) && g.Active) || !look.skinGenes.NullOrEmpty() && !p.genes.GenesListForReading.Any(g=>look.skinGenes.Contains(g.def) && g.Active)))genBad.Add(n+" look");
   }
   CrClean();
   Check(genBad.Count==0,"every common race generates with all xenotype genes, active custom genes and a hair/skin colour from its look list: "+string.Join(",",genBad));
  }

  // 4：体型与心智抗性
  void CrSizes(){
   var human=CrHuman(CrCell(-8,-8));float hs=human.BodySize;var rows=new List<string>();bool ok=true;
   foreach(var (x,size,mind) in new[]{("LP_HalflingLightfoot",.7f,0f),("LP_HalflingStout",.7f,0f),("LP_RockGnome",.6f,.6f),("LP_ForestGnome",.6f,.6f),("LP_DeepGnome",.6f,.6f),("LP_Kobold",.6f,0f),("LP_Goblin",.6f,0f),("LP_HalfElf",1f,.25f),("LP_WoodElf",1f,.5f),("LP_Drow",1f,.5f),("LP_HillDwarf",.8f,0f)}){
    var p=CrPawn(x);float s=p.BodySize/hs,m=DNDMind.Resistance(p);rows.Add(x+" "+s.ToString("0.00")+"/"+m.ToString("0.00"));
    if(!Near(s,size) || !Near(m,mind))ok=false;
   }
   CrClean();
   Check(ok,"small bodies (halfling 0.7, gnome/kobold/goblin 0.6, hill dwarf 0.8) and mind resistance (gnomes +60%, half-elf +25%, elves 50%): "+string.Join("; ",rows));
  }

  // 5-8：半身人幸运、冷却、勇敢、恐惧心情
  void CrHalfling(){
   var luck=Subraces.G("LP_HalflingLuck");var ext=luck.GetModExtension<OncePerCooldownExtension>();
   var h=CrPawn("LP_HalflingLightfoot",CrCell(2,2));var heart=h.RaceProps.body.AllParts.First(b=>b.def.defName=="Heart");
   Hediff_Injury Lethal(){var inj=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,h,heart);inj.Severity=PartHealth.Maximum(heart,h)+2f;return inj;}
   var i1=Lethal();float before=i1.Severity;bool lethal=h.health.WouldDieAfterAddingHediff(i1);bool handled=OncePerCooldown.Process(h,i1,new DamageInfo(DamageDefOf.Cut,before,0,-1,null,heart));
   var cd=OncePerCooldown.Cooldown(h,ext);
   Check(lethal && handled && Near(i1.Severity,before*.1f) && cd!=null && cd.def.label=="运气用光了" && cd.TryGetComp<HediffComp_Disappears>().ticksToDisappear==180000 && ext.moteText=="真走运！" && ext.Worker is OncePerCooldownWorker_ReduceDamage,"halfling luck turns a lethal heart wound into 10% ("+before.ToString("0")+" -> "+i1.Severity.ToString("0.0")+") and starts a visible 3-day cooldown 运气用光了");
   h.health.AddHediff(i1);
   var i2=Lethal();float b2=i2.Severity;Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+5);bool again=OncePerCooldown.Process(h,i2,new DamageInfo(DamageDefOf.Cut,b2,0,-1,null,heart));
   Check(!h.Dead && !again && Near(i2.Severity,b2),"the halfling survives the lucky hit and luck does not trigger again during the cooldown");
   var human=CrHuman(CrCell(-2,2));
   bool hPanic=h.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"测试",true);bool humanPanic=human.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"测试",true);
   Check(!hPanic && !h.InMentalState && humanPanic,"brave halflings never panic-flee (a human does)");
   human.mindState.mentalStateHandler.Reset();
   var fear=DefDatabase<ThoughtDef>.GetNamedSilentFail("LPTest_FearMemory");
   if(fear==null){fear=new ThoughtDef{defName="LPTest_FearMemory",thoughtClass=typeof(Thought_Memory),durationDays=1f,stages=new List<ThoughtStage>{new ThoughtStage{label="测试恐惧",baseMoodEffect=-10f}}};DefDatabase<ThoughtDef>.Add(fear);}
   var mh=(Thought_Memory)ThoughtMaker.MakeThought(fear);h.needs.mood.thoughts.memories.TryGainMemory(mh);var mu=(Thought_Memory)ThoughtMaker.MakeThought(fear);human.needs.mood.thoughts.memories.TryGainMemory(mu);
   float fh=mh.MoodOffset(),fu=mu.MoodOffset();
   Check(fu<0f && Near(fh/fu,.5f) && Subraces.FearThought(fear),"fear moods are halved for halflings: "+fh+" vs "+fu);
   CrClean();
  }

  // 9-11：卓尔
  void CrDrow(){
   var drow=CrPawn("LP_Drow",CrCell(4,-4));var cell=drow.Position;var oldRoof=cell.GetRoof(map);
   var target=CrHuman(CrCell(4,-6));
   map.skyManager.ForceSetCurSkyGlow(1f);CrRoof(cell,null);
   float day=Subraces.HitFactor(drow,target,false);
   var verb=DamageVerb(drow,target);var miss=AccessTools.Method(typeof(Verb_MeleeAttack),"GetNonMissChance");float meleeDay=verb==null?0f:(float)miss.Invoke(verb,new object[]{new LocalTargetInfo(target)});
   CrRoof(cell,RoofDefOf.RoofConstructed);float roofed=Subraces.HitFactor(drow,target,false);float meleeRoof=verb==null?0f:(float)miss.Invoke(verb,new object[]{new LocalTargetInfo(target)});
   CrRoof(cell,oldRoof);
   Check(Near(day,.85f) && Near(roofed,1f) && verb!=null && meleeRoof>0f && Near(meleeDay/meleeRoof,.85f),"drow sunlight: daylight in the open multiplies hit chance by 85% (factor "+day+", melee "+meleeDay.ToString("0.000")+"/"+meleeRoof.ToString("0.000")+"), no penalty under a roof");
   var enemyF=EnemyFaction();var at=CrCell(10,10);
   var foe=CrHuman(at,enemyF);var foeDrow=CrPawn("LP_Drow");foeDrow.drafter.Drafted=false;foeDrow.SetFaction(enemyF);foeDrow.Position=at;foeDrow.Notify_Teleported();var ally=CrHuman(at);
   int hit=Subraces.CastDarkness(drow,at,3f,720);var dark=Subraces.H("LP_MagicalDarkness");
   var ability=DefDatabase<AbilityDef>.GetNamed("LP_DrowDarkness");
   Check(foe.health.hediffSet.HasHediff(dark) && !ally.health.hediffSet.HasHediff(dark) && !foeDrow.health.hediffSet.HasHediff(dark) && Near(Subraces.HitFactor(foe,ally,true),.7f) && Subraces.State.zones.Count>0 && drow.abilities.GetAbility(ability)!=null && ability.cooldownTicksRange.min==60000 && Near(ability.verbProperties.range,15f) && drow.GetPsylinkLevel()==0,"darkness: hostile non-drow in radius 3 get magical darkness (hit ×70%), allies and drow do not; ability needs no psylink, range 15, 1 day cooldown ("+hit+" affected)");
   var victim=CrHuman(CrCell(-6,-6));float c0=victim.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);bool poisoned=DrowPoisonStrike.Poison(victim);float c1=victim.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
   var venom=victim.health.hediffSet.GetFirstHediffOfDef(Subraces.H("LP_DrowVenom"));
   var mechKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Militor");Pawn mech=mechKind==null?null:PawnGenerator.GeneratePawn(new PawnGenerationRequest(mechKind,Faction.OfMechanoids,forceGenerateNewPawn:true));
   bool mechPoisoned=mech!=null && DrowPoisonStrike.Poison(mech);mech?.Destroy();
   Check(poisoned && venom!=null && venom.TryGetComp<HediffComp_Disappears>().ticksToDisappear==15000 && c1<=c0-.15f && mech!=null && !mechPoisoned && Near(Subraces.PoisonChance,.15f),"spider venom: -20% consciousness for 6 hours ("+c0.ToString("0.00")+" -> "+c1.ToString("0.00")+"), mechanoids immune, 15% chance on melee hits");
   CrClean();
  }

  // 12-13：矮人
  void CrDwarf(){
   var enemyF=EnemyFaction();var other=Find.FactionManager.AllFactions.FirstOrDefault(f=>f!=Faction.OfPlayer && f!=enemyF && !f.Hidden && f.def.humanlikeFaction);
   var dwarf=CrPawn("LP_Dwarf",CrCell(6,0));var hill=CrPawn("LP_HillDwarf",CrCell(6,2));
   var killer=CrHuman(CrCell(8,0),enemyF);var victim=CrHuman(CrCell(8,2));victim.drafter.Drafted=false;
   victim.Kill(new DamageInfo(DamageDefOf.Cut,20,0,-1,killer));
   int G(Pawn p)=>p.needs.mood.thoughts.memories.Memories.OfType<Thought_DwarfGrudge>().Count(m=>m.faction==enemyF);
   var t=dwarf.needs.mood.thoughts.memories.Memories.OfType<Thought_DwarfGrudge>().FirstOrDefault(m=>m.faction==enemyF);
   if(t!=null)t.age=5000;
   var victim2=CrHuman(CrCell(8,4));victim2.drafter.Drafted=false;victim2.Kill(new DamageInfo(DamageDefOf.Cut,20,0,-1,killer));
   var otherPawn=other==null?null:CrHuman(CrCell(10,0),other);
   Check(victim.Dead && t!=null && G(dwarf)==1 && G(hill)==1 && t.age==0 && t.LabelCap.Contains(enemyF.Name) && Near(t.MoodOffset(),-3f) && Grudges.Against(enemyF) && Grudges.End(enemyF)-Subraces.Now>=Subraces.GrudgeTicks-10 && Near(Subraces.HitFactor(dwarf,killer,true),1.05f) && (otherPawn==null || Near(Subraces.HitFactor(dwarf,otherPawn,false),1f)),"dwarf grudge: a colonist killed by "+enemyF?.Name+" gives every dwarf one -3 血仇 thought for a year (refreshed, not stacked) and ×105% hit chance against that faction only");
   var plate=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_PlateArmor"),ThingDefOf.Steel);dwarf.apparel.Wear(plate);
   float pen=StatPart_DwarfArmorTraining.Penalty(dwarf),v=1f;new StatPart_DwarfArmorTraining{parentStat=StatDefOf.MoveSpeed}.TransformValue(StatRequest.For(dwarf),ref v);
   var torso=hill.RaceProps.body.corePart;float withT=PartHealth.Maximum(torso,hill);hill.genes.RemoveGene(hill.genes.GetGene(Subraces.G("LP_DwarfToughness")));float without=PartHealth.Maximum(torso,hill);
   Check(pen<0f && Near(v,1f-.5f*pen) && StatDefOf.MoveSpeed.parts.Any(p=>p is StatPart_DwarfArmorTraining) && Math.Abs(withT/without-1.15f)<.04f,"mountain dwarf armor training halves the plate armor move speed penalty ("+pen.ToString("0.00")+"); hill dwarf toughness raises part health ×1.15 ("+without+" -> "+withT+")");
   CrClean();
  }

  // 14-15：半精灵
  void CrHalfElf(){
   var bad=new List<string>();
   for(int i=0;i<8;i++){
    var p=CrPawn("LP_HalfElf");string a=Subraces.TalentSkill(p,"A"),b=Subraces.TalentSkill(p,"B");
    int met=p.genes.GenesListForReading.Where(g=>!g.Overridden).Sum(g=>g.def.biostatMet);
    var skill=a==null?null:DefDatabase<SkillDef>.GetNamed(a);
    if(a==null || b==null || a==b || met<-2 || met>2 || p.skills.GetSkill(skill).Aptitude<4)bad.Add(a+"/"+b+"/met"+met);
   }
   var x=DefDatabase<XenotypeDef>.GetNamed("LP_HalfElf");
   CrClean();
   Check(bad.Count==0 && !x.genes.Any(g=>g.defName.StartsWith("LP_HalfElfTalent")) && DefDatabase<GeneDef>.AllDefs.Count(g=>g.defName.StartsWith("LP_HalfElfTalent"))==24 && Subraces.G("LP_HalfElfTalentA_Shooting").exclusionTags.Contains("LP_HalfElfTalentA"),"half-elves generate with one A and one B talent of different skills (+4 aptitude, metabolism stays -2..+2); 24 talent genes in two exclusive groups: "+string.Join(",",bad));
   var elf=CrPawn("LP_Elf");var dwarf=CrPawn("LP_Dwarf");var tracker=RacialRelations.Tracker;tracker.Census(map).Rebuild(map);
   var ed=DefDatabase<RacialRelationDef>.GetNamed("LP_Relation_ElfDwarf");float alone=ed.thought.Worker.MoodMultiplier(elf);
   var half=CrPawn("LP_HalfElf");tracker.Census(map).Rebuild(map);float bridged=ed.thought.Worker.MoodMultiplier(elf);
   var drow=CrPawn("LP_Drow");tracker.Census(map).Rebuild(map);var elfDrow=DefDatabase<RacialRelationDef>.GetNamed("LP_Relation_ElfDrow");var st=elfDrow.thought.Worker.CurrentState(elf);
   var rels=Subraces.Relations157.Select(n=>DefDatabase<RacialRelationDef>.GetNamedSilentFail(n)).ToList();
   Check(Near(alone,1f) && Near(bridged,.5f) && ed.halvedBy.Contains(x) && st.Active && elfDrow.thought.stages[0].baseMoodEffect==-5 && rels.All(r=>r!=null && r.enabled && !r.ConfigErrors().Any()) && DefDatabase<RacialRelationDef>.GetNamed("LP_Relation_KoboldLich").targetLich,"two-peoples bridge halves elf-dwarf negative mood ("+alone+" -> "+bridged+"); 16 new racial relations are valid, elves get -5 toward drow");
   CrClean();
  }

  // 16-17、22：狗头人
  void CrKobold(){
   foreach(var k in PawnsFinder.AllMaps_FreeColonists.Where(Subraces.IsKobold).ToList())k.Destroy();
   Subraces.ClearCaches();var pit=DefDatabase<ThingDef>.GetNamed("LP_KoboldSpikePit");var pw=new PlaceWorker_KoboldOnly();var spot=CrCell(-10,4);
   var no=pw.AllowsPlacing(pit,spot,Rot4.North,map);
   var kob=CrPawn("LP_Kobold",CrCell(-10,6));Subraces.ClearCaches();var yes=pw.AllowsPlacing(pit,spot,Rot4.North,map);
   Thing Built(Pawn by,IntVec3 c){var f=(Frame)ThingMaker.MakeThing(pit.frameDef);f.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(f,c,map);f.CompleteConstruction(by);var t=c.GetThingList(map).FirstOrDefault(x=>x.def==pit);if(t!=null)crTemp.Add(t);return t;}
   var human=CrHuman(CrCell(-12,6));var kPit=Built(kob,CrCell(-10,8));var hPit=Built(human,CrCell(-14,8));
   var frame=(Frame)ThingMaker.MakeThing(pit.frameDef);frame.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(frame,CrCell(-10,10),map);crTemp.Add(frame);
   var cs=DefDatabase<StatDef>.GetNamed("ConstructionSpeed");float s0=kob.GetStatValue(cs);kob.jobs.curJob=JobMaker.MakeJob(JobDefOf.FinishFrame,frame);float s1=kob.GetStatValue(cs);kob.jobs.curJob=null;
   var tmd=DefDatabase<StatDef>.GetNamed("TrapMeleeDamage");
   Check(!no.Accepted && no.Reason.Contains("狗头人") && yes.Accepted && pit.costList.Single().thingDef==ThingDefOf.WoodLog && pit.costList.Single().count==20 && pit.building.isTrap && !pit.building.trapDestroyOnSpring && pit.thingClass==typeof(Building_KoboldSpikePit) && pit.researchPrerequisites.NullOrEmpty() && kPit!=null && hPit!=null && Near(kPit.GetStatValue(tmd)/hPit.GetStatValue(tmd),1.3f) && Near(s1/s0,2f),"kobold spike pit: 20 wood, 3-use trap (1.63.0), no research, needs a kobold colonist to place; kobold-built traps deal ×130% ("+kPit?.GetStatValue(tmd)+" vs "+hPit?.GetStatValue(tmd)+"), kobolds build traps ×200%");
   if(kPit!=null)crTrapId=kPit.thingIDNumber;
   var target=CrHuman(CrCell(-16,-8));var a1=CrHuman(target.Position+IntVec3.East);var a2=CrHuman(target.Position+IntVec3.West);
   Subraces.ClearCaches();int m0=Subraces.cacheMisses;float f1=Subraces.PackFactor(kob,target);
   var a3=CrHuman(target.Position+IntVec3.North);float f2=Subraces.PackFactor(kob,target);int m1=Subraces.cacheMisses;
   Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+Subraces.CacheTicks+1);float f3=Subraces.PackFactor(kob,target);
   var a4=CrHuman(target.Position+IntVec3.South);Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+Subraces.CacheTicks+1);float f4=Subraces.PackFactor(kob,target);
   Check(Near(f1,1.1f) && Near(f2,1.1f) && m1-m0==1 && Near(f3,1.15f) && Near(f4,1.15f),"pack tactics: +5% melee hit per ally next to the target (1.10), cached for 250 ticks (one count, a new ally is ignored until then), capped at +15%: "+f1+" "+f2+" "+f3+" "+f4);
   var servant=DefDatabase<ThoughtDef>.GetNamed("LP_KoboldDragonServant");
   foreach(var d in map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).Where(Dragons.Is).ToList())if(!d.Dead)d.SetFaction(null);
   Subraces.ClearCaches();var s0t=servant.Worker.CurrentState(kob);float hoard1=Subraces.HoardFactor(map);
   var dragon=CrPawn("LP_ForceDragon");Subraces.ClearCaches();var s1t=servant.Worker.CurrentState(kob);
   bool oldSet=LichMod.Settings.koboldDragonServant;LichMod.Settings.koboldDragonServant=false;var s2t=servant.Worker.CurrentState(kob);float hoardOff=Subraces.HoardFactor(map);LichMod.Settings.koboldDragonServant=oldSet;
   Check(s0t.Active && s0t.StageIndex==0 && servant.stages[0].baseMoodEffect==-2 && s1t.Active && s1t.StageIndex==1 && servant.stages[1].baseMoodEffect==6 && !s2t.Active && Near(hoard1,1.02f) && Near(hoardOff,1f),"dragon servant: -2 without a dragon, +6 with a colony dragon, one kobold adds +2% hoard value; setting off disables both");
   CrClean();
  }

  // 18-20：地精三族
  void CrGoblinoids(){
   var gob=CrPawn("LP_Goblin",CrCell(12,-10));var big=CrHuman(CrCell(12,-12));var small=CrPawn("LP_Goblin",CrCell(14,-10));
   float vsBig=Subraces.DamageFactor(gob,big,true),vsSmall=Subraces.DamageFactor(gob,small,true);
   float ms0=gob.GetStatValue(StatDefOf.MoveSpeed);gob.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1,0,-1,big));var esc=gob.health.hediffSet.GetFirstHediffOfDef(Subraces.H("LP_GoblinEscape"));float ms1=gob.GetStatValue(StatDefOf.MoveSpeed);
   Check(Near(vsBig,1.15f) && Near(vsSmall,1f) && esc!=null && esc.TryGetComp<HediffComp_Disappears>().ticksToDisappear==120 && ms1/ms0>1.2f,"goblin: +15% damage against bigger targets only; a melee hit gives nimble escape (move speed ×130% for 2 seconds; "+ms0.ToString("0.00")+" -> "+ms1.ToString("0.00")+")");
   var hob=CrPawn("LP_Hobgoblin",CrCell(-14,18));CrHuman(hob.Position+IntVec3.East);CrHuman(hob.Position+IntVec3.West);
   Subraces.ClearCaches();float hf=Subraces.HobFactor(hob);float w=1f;new StatPart_HobgoblinNumbers().TransformValue(StatRequest.For(hob),ref w);
   hob.drafter.Drafted=true;hob.needs.mood.CurLevel=0f;bool broke=hob.mindState.mentalBreaker.TryDoRandomMoodCausedMentalBreak();
   var patched=Harmony.GetPatchInfo(AccessTools.Method(typeof(MentalBreaker),nameof(MentalBreaker.TryDoRandomMoodCausedMentalBreak)))?.Prefixes.Any(p=>p.owner=="local.lich.phylactery")==true;
   Check(Near(hf,1.06f) && Near(w,1.06f) && Near(Subraces.HitFactor(hob,big,false),1.06f) && !broke && !hob.InMentalState && patched,"hobgoblin: two allies within 3 cells give +6% hit chance and global work speed; iron discipline stops mood breaks while drafted");
   var bug=CrPawn("LP_Bugbear",CrCell(18,-10));var mark=CrHuman(CrCell(18,-12));mark.mindState.lastAttackTargetTick=-99999;mark.mindState.lastHarmTick=-99999;
   float surprise=Subraces.DamageFactor(bug,mark,true);mark.mindState.lastHarmTick=Find.TickManager.TicksGame;float aware=Subraces.DamageFactor(bug,mark,true);
   var limbs=Subraces.G("LP_BugbearLongLimbs");
   Check(Near(surprise,1.25f) && Near(aware,1f) && limbs.statOffsets.Any(m=>m.stat==StatDefOf.MeleeHitChance && Near(m.value,.1f)) && bug.story.bodyType==BodyTypeDefOf.Hulk,"bugbear: surprise attack ×125% on a target that has not fought for 10 seconds, normal afterwards; long limbs +10% melee hit; hulk body");
   CrClean();
  }

  // 21：侏儒
  void CrGnomes(){
   var fg=CrPawn("LP_ForestGnome",CrCell(-18,12));var human=CrHuman(CrCell(-18,14));
   var smallKind=DefDatabase<PawnKindDef>.AllDefs.Where(k=>k.RaceProps?.Animal==true && k.race.race.baseBodySize<=.35f && !k.RaceProps.Dryad && !k.RaceProps.Insect).OrderBy(k=>k.defName).FirstOrDefault();
   var small=PawnGenerator.GeneratePawn(new PawnGenerationRequest(smallKind,null,forceGenerateNewPawn:true));GenSpawn.Spawn(small,CrCell(-16,12),map);crTemp.Add(small);
   var big=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Muffalo,null,forceGenerateNewPawn:true));
   float ts=Beasts.TameChance(.3f,fg,small),tb=Beasts.TameChance(.3f,fg,big),th=Beasts.TameChance(.3f,human,small);big.Destroy();
   Subraces.calmTaming=small;bool raged=small.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter,"测试",true);Subraces.calmTaming=null;
   var dg=CrPawn("LP_DeepGnome",CrCell(-20,16));var gene=dg.genes.GetFirstGeneOfType<Gene_DeepGnomeCamouflage>();var camo=Subraces.H("LP_StoneCamouflage");
   gene.Process();bool early=dg.health.hediffSet.HasHediff(camo);Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+Subraces.StillTicks+1);gene.Process();bool hidden=dg.health.hediffSet.HasHediff(camo);float tf=Subraces.TargetFactor(dg);
   dg.stances.SetStance(new Stance_Cooldown(30,LocalTargetInfo.Invalid,null));bool after=dg.health.hediffSet.HasHediff(camo);dg.stances.SetStance(new Stance_Mobile());
   Check(smallKind!=null && Near(ts,.6f) && Near(tb,.3f) && Near(th,.3f) && !raged && !small.InMentalState && !early && hidden && Near(tf,.7f) && !after,"forest gnomes tame animals of size <=0.5 ("+smallKind?.defName+") at ×2 and they never turn on them; deep gnome stone camouflage after 120 still ticks (ranged hit ×70%), lost on attacking");
   CrClean();
  }

  // 23-24：剧本、派系、冒险者表、背景故事
  void CrIntegration(){
   var s=DefDatabase<ScenarioDef>.GetNamed("LP_AdventurerPartyScenario");
   var cfg=s.scenario.AllParts.OfType<ScenPart_ConfigPage_ConfigureStartingPawns_Xenotypes>().Single();
   var counts=Traverse.Create(cfg).Field("xenotypeCounts").GetValue<System.Collections.IList>();var xs=new List<string>();foreach(var c in counts)xs.Add(Traverse.Create(c).Field("xenotype").GetValue<XenotypeDef>().defName);
   var things=s.scenario.AllParts.OfType<ScenPart_StartingThing_Defined>().ToList();
   bool stuffOk=things.All(t=>{var st=Traverse.Create(t).Field("stuff").GetValue<ThingDef>();var td=Traverse.Create(t).Field("thingDef").GetValue<ThingDef>();return st==null?!td.MadeFromStuff:st.IsStuff && td.MadeFromStuff;});
   string info=null;try{info=s.scenario.GetFullInformationText();}catch(Exception e){info=null;Log.Warning("[LICH-TEST] scenario text: "+e);}
   var hostile=DefDatabase<FactionDef>.GetNamed("LP_FaerunHostile");var combat=hostile.pawnGroupMakers.Where(g=>g.kindDef==PawnGroupKindDefOf.Combat).SelectMany(g=>g.options).Select(o=>o.kind.defName).ToList();
   var hf=Find.FactionManager.FirstFactionOfDef(hostile);var kinds=new List<string>();bool kindsOk=true;
   foreach(var (k,allowed) in new[]{("LP_FH_GoblinRaider",new[]{"LP_Goblin","LP_Hobgoblin","LP_Bugbear"}),("LP_FH_KoboldSkulker",new[]{"LP_Kobold"}),("LP_FH_DrowBlade",new[]{"LP_Drow"})})
    for(int i=0;i<4;i++){var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(k),hf,forceGenerateNewPawn:true));kinds.Add(Subraces.Xeno(p));if(!allowed.Contains(Subraces.Xeno(p)))kindsOk=false;p.Destroy();}
   var colony=DefDatabase<FactionDef>.GetNamed("LP_FaerunColony").xenotypeSet;
   Check(xs.SequenceEqual(new[]{"Baseliner","LP_Elf","LP_Dwarf","LP_HalflingLightfoot","LP_HalfOrc"}) && stuffOk && !info.NullOrEmpty() && combat.Contains("LP_FH_GoblinRaider") && combat.Contains("LP_FH_KoboldSkulker") && combat.Contains("LP_FH_DrowBlade") && kindsOk && hf!=null && new[]{"LP_WoodElf","LP_HillDwarf","LP_HalfElf","LP_HalflingLightfoot","LP_RockGnome"}.All(n=>colony.Contains(DefDatabase<XenotypeDef>.GetNamed(n))),"冒险者小队 starts with human, high elf, mountain dwarf, lightfoot halfling and half-orc using stuff-correct vanilla gear; Thayans field goblinoids, kobolds and drow ("+string.Join(",",kinds.Distinct())+"); the Faerun colony recruits the new common races");
   var pools=DefDatabase<BackstoryPoolDef>.AllDefsListForReading;var missing=new List<string>();
   foreach(var n in newSubraces){
    if(!Affinity.Table.ContainsKey(n) || !Adventurer.Racial.ContainsKey(n))missing.Add(n+" table");
    var pool=pools.FirstOrDefault(p=>p.tier==BackstoryRuleTier.Race && p.xenotypes.Contains(n));
    if(pool==null){missing.Add(n+" pool");continue;}
    bool child=pool.childhood.Any(c=>DefDatabase<BackstoryDef>.AllDefs.Any(b=>b.slot==BackstorySlot.Childhood && b.spawnCategories.Contains(c.category) && b.spawnCategories.Any(x=>x.StartsWith("LP_Race_") && x!="LP_Race_Elf" && x!="LP_Race_Dwarf")));
    bool adult=pool.adulthood.Any(c=>DefDatabase<BackstoryDef>.AllDefs.Any(b=>b.slot==BackstorySlot.Adulthood && b.spawnCategories.Contains(c.category) && b.spawnCategories.Any(x=>x.StartsWith("LP_Race_") && x!="LP_Race_Elf" && x!="LP_Race_Dwarf")));
    if(!child || !adult)missing.Add(n+" backstory");
   }
   Check(missing.Count==0 && Affinity.Table["LP_Elf"].label=="高等精灵" && Affinity.Table["LP_Dwarf"].strong[Adventurer.Str] && Affinity.Table["LP_Drow"].strong[Adventurer.Cha],"adventurer racial bonus and affinity tables and race-only backstories (childhood + adulthood) cover all 13 new xenotypes: "+string.Join(",",missing));
  }

  void PrepareCommonRacesSave(){
   Talents.StripAll=true;
   var h=RacePawn("LP_HalflingLightfoot");var cd=OncePerCooldown.StartCooldown(h,Subraces.G("LP_HalflingLuck"));cd.TryGetComp<HediffComp_Disappears>().ticksToDisappear=123456;crHalflingId=h.ThingID;crCooldownLeft=123456;
   var he=RacePawn("LP_HalfElf");crHalfElfId=he.ThingID;crTalentA=Subraces.TalentSkill(he,"A");crTalentB=Subraces.TalentSkill(he,"B");
   var f=EnemyFaction();var d=RacePawn("LP_HillDwarf");crDwarfId=d.ThingID;Grudges.Register(f);crGrudgeFaction=f.GetUniqueLoadID();crGrudgeEnd=Grudges.End(f);
   var pit=DefDatabase<ThingDef>.GetNamed("LP_KoboldSpikePit");var t=ThingMaker.MakeThing(pit);t.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(t,CellFinder.RandomClosewalkCellNear(map.Center,map,12),map);Subraces.State.koboldTraps.Add(t.thingIDNumber);crTrapId=t.thingIDNumber;
  }
  void CheckCommonRacesAfterLoad(){
   map=Find.CurrentMap;Pawn Find1(string id)=>map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==id);
   var h=Find1(crHalflingId);var he=Find1(crHalfElfId);var d=Find1(crDwarfId);
   var f=Find.FactionManager.AllFactions.First(x=>x.GetUniqueLoadID()==crGrudgeFaction);
   var cd=OncePerCooldown.Cooldown(h,Subraces.G("LP_HalflingLuck").GetModExtension<OncePerCooldownExtension>());
   var g=d.needs.mood.thoughts.memories.Memories.OfType<Thought_DwarfGrudge>().FirstOrDefault(m=>m.faction==f);
   var trap=map.listerThings.AllThings.FirstOrDefault(x=>x.thingIDNumber==crTrapId);
   Check(cd!=null && Math.Abs(cd.TryGetComp<HediffComp_Disappears>().ticksToDisappear-crCooldownLeft)<=5 && Subraces.TalentSkill(he,"A")==crTalentA && Subraces.TalentSkill(he,"B")==crTalentB && g!=null && g.LabelCap.Contains(f.Name) && Grudges.End(f)==crGrudgeEnd && trap!=null && StatPart_KoboldTrap.Marked(trap) && d.genes.Xenotype.defName=="LP_HillDwarf","halfling luck cooldown, half-elf talents, dwarf grudge (faction thought and expiry) and kobold-built trap marks survive save/load");
   h.Destroy();he.Destroy();d.Destroy();trap.Destroy();
  }
 }
}
