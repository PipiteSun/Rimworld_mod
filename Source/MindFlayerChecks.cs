using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  static string flayerSaveId,tadpoleHostId,tadpoleDoctorId,elderBrainId,halfIllithidId;static float flayerSavedPoints,tadpoleSavedProgress;static int flayerGeneCount;
  Pawn MindFlayerPawn(){var p=RacePawn("LP_MindFlayer");MindFlayer.Cerebrum(p).Value=.5f;MindFlayer.Cerebrum(p).SyncHunger();return p;}
  // 倒地夹具：意识上限0.1，不伤任何部位。
  static HediffDef Unconscious=>TestHediff("LPTest_Unconscious",new HediffStage{capMods=new List<PawnCapacityModifier>{new PawnCapacityModifier{capacity=PawnCapacityDefOf.Consciousness,setMax=.1f}}});
  Pawn DownedVictim(int intellect=10){
   Pawn p=null;
   for(int attempt=0;attempt<20;attempt++){p?.Destroy();p=SharedPawn();if(p.GetStatValue(StatDefOf.PsychicSensitivity)>.5f && !p.skills.GetSkill(SkillDefOf.Intellectual).TotallyDisabled)break;}
   p.skills.GetSkill(SkillDefOf.Intellectual).Level=intellect;p.health.AddHediff(Unconscious);return p;
  }
  Faction OtherFaction()=>Find.FactionManager.AllFactions.Where(f=>f!=Faction.OfPlayer && !f.Hidden && f.def.humanlikeFaction && !f.def.permanentEnemy && f.PlayerGoodwill>-80).OrderByDescending(f=>f.PlayerGoodwill).FirstOrDefault();
  void CheckMindFlayer(){
   var before=new HashSet<Thing>(map.listerThings.AllThings);var goodwill=Find.FactionManager.AllFactions.Where(f=>!f.IsPlayer && f.def.humanlikeFaction && !f.def.permanentEnemy).ToDictionary(f=>f,f=>f.PlayerGoodwill);
   CheckMindFlayerDefs();CheckCerebrum();CheckDevour();CheckMindBlast();CheckHiveLink();CheckTadpoles();CheckElderBrain();CheckMindFlayerPowers();CheckHalfIllithid();
   // 清理：后面的巫妖/造物测试依赖地图上的物品与人。上古脑被毁会吐出养料，拆除会返还材料；全部清掉，派系好感也恢复。
   var leftovers=map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Filth) && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList();
   foreach(var t in leftovers)if(!t.Destroyed)t.Destroy();
   foreach(var kv in goodwill)if(kv.Key.PlayerGoodwill!=kv.Value)kv.Key.TryAffectGoodwillWith(Faction.OfPlayer,kv.Value-kv.Key.PlayerGoodwill,false,false);
   Check(map.listerThings.AllThings.All(t=>before.Contains(t) || t is Filth || t is Mote || t.def.category==ThingCategory.Ethereal),"mind flayer checks leave no extra things behind (cleaned "+leftovers.Count+": "+string.Join(",",leftovers.Select(t=>t.def.defName).Distinct())+")");
  }

  void CheckMindFlayerDefs(){
   var x=MindFlayer.Xenotype;
   Check(x!=null && !x.ConfigErrors().Any() && !x.inheritable && x.factionlessGenerationWeight==0 && x.genes.All(g=>g!=null),"mind flayer is a non-inheritable xenotype with resolved genes and no random generation");
   Check(MindFlayer.CerebrumGene.biostatArc>0 && MindFlayer.CerebrumGene.resourceGizmoType==typeof(GeneGizmo_CerebrumResource) && MindFlayer.CerebrumGene.geneClass==typeof(Gene_Cerebrum),"cerebrum is an archite core resource gene with its own bar");
   foreach(var n in new[]{"PsychicAbility_Extreme","Learning_Fast","AptitudeStrong_Intellectual","AptitudePoor_Social","UVSensitivity_Intense","DarkVision","Beauty_VeryUgly","Hair_BaldOnly","Beard_NoBeardOnly","Skin_Purple","Delicate","LP_MindFlayerSterile","LP_PsionicSense","LP_HiveLink"})Check(x.genes.Any(g=>g.defName==n),"mind flayer xenotype includes "+n);
   int met=x.genes.Sum(g=>g.biostatMet);Check(met>=-4 && met<=-2,"mind flayer metabolism total is about -3: "+met);
   Check(ContentFinder<Texture2D>.Get(x.iconPath,false)!=null && x.genes.Where(g=>g.defName.StartsWith("LP_")).All(g=>ContentFinder<Texture2D>.Get(g.iconPath,false)!=null),"mind flayer xenotype and custom gene icons load");
   foreach(var g in x.genes.Where(g=>g.defName.StartsWith("LP_")))Check(!g.ConfigErrors().Any(),"mind flayer gene validates: "+g.defName);
   foreach(var a in new[]{"LP_DevourBrain","LP_MindBlast"}){var def=DefDatabase<AbilityDef>.GetNamed(a);Check(!def.ConfigErrors().Any() && ContentFinder<Texture2D>.Get(def.iconPath,false)!=null && def.comps.OfType<CompProperties_MindFlayerAbility>().Any(),"mind flayer ability validates: "+a);}
   Check(DefDatabase<AbilityDef>.GetNamed("LP_MindBlast").cooldownTicksRange.min==60000,"mind blast cooldown is one day");
   Check(LichMod.Settings.brainHungerRage && LichMod.Settings.ceremorphDays==5 && !LichMod.Settings.elderBrainSummon,"mind flayer settings default: hunger rage on, 5 day ceremorph, summoning off");
   if(ModsConfig.IdeologyActive)Check(DefDatabase<PreceptDef>.GetNamedSilentFail("LP_BrainDevouring_Abhorrent")?.issue?.defName=="LP_BrainDevouring" && DefDatabase<PreceptDef>.GetNamedSilentFail("LP_BrainDevouring_Sacred")!=null,"brain devouring precepts are registered with Ideology");
   var p=MindFlayerPawn();
   Check(p.genes.Xenotype==x && x.genes.All(g=>p.genes.GetGene(g)?.Active==true),"mind flayer generates with every xenotype gene active");
   Check(p.GetStatValue(StatDefOf.Fertility)==0 && Near(DNDMind.Resistance(p),.3f),"mind flayer is sterile and has 30% mind resistance");
   Check(p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_DevourBrain"))!=null && p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_MindBlast"))!=null,"mind flayer has devour brain and mind blast");
   p.Destroy();
  }

  void CheckCerebrum(){
   var p=RacePawn("LP_MindFlayer");var g=MindFlayer.Cerebrum(p);
   Check(Near(g.Points,60),"new mind flayer starts with 60 cerebrum: "+g.Points);
   g.Drain(60000);Check(Near(g.Points,52),"cerebrum drains 8 per day: "+g.Points);
   g.Value=.25f;g.SyncHunger();var h=p.health.hediffSet.GetFirstHediffOfDef(MindFlayer.Hunger);
   Check(h!=null && h.CurStageIndex==0 && DefDatabase<ThoughtDef>.GetNamed("LP_CerebrumHunger").Worker.CurrentState(p).StageIndex==0,"below 30 cerebrum the mind flayer weakens (stage 1 and mood)");
   g.Value=.05f;g.SyncHunger();Check(h.CurStageIndex==1,"below 10 cerebrum the hunger worsens");
   g.Value=0;g.SyncHunger();Check(h.CurStageIndex==2 && h.Label.Contains("脑饥"),"at 0 cerebrum the mind flayer starves");
   Check(MindFlayer.StartRage(p) && p.MentalStateDef==MentalStateDefOf.Berserk,"starving mind flayer can fall into brain hunger rage");
   p.MentalState.RecoverFromState();
   LichMod.Settings.brainHungerRage=false;try{Check(!MindFlayer.StartRage(p) && !p.InMentalState,"brain hunger rage can be turned off in settings");}finally{LichMod.Settings.brainHungerRage=true;}
   MindFlayer.Gain(p,50);Check(Near(g.Points,50) && p.health.hediffSet.GetFirstHediffOfDef(MindFlayer.Hunger)==null,"feeding back above 30 removes the hunger");
   MindFlayer.Gain(p,500);Check(Near(g.Points,100),"cerebrum caps at 100");
   Check(MindFlayer.Pay(p,30) && Near(g.Points,70) && !MindFlayer.Pay(p,80) && Near(g.Points,70),"paying cerebrum spends only what is available");
   p.Destroy();
  }

  void CheckDevour(){
   var caster=MindFlayerPawn();var g=MindFlayer.Cerebrum(caster);
   var victim=DownedVictim(10);
   Check(victim.Downed && MindFlayer.CannotDevour(caster,victim)==null,"downed human can be devoured");
   float sens=victim.GetStatValue(StatDefOf.PsychicSensitivity),expected=Mathf.Min(80,(20+30)*sens);
   Check(Near(MindFlayer.Value(victim,false),expected),"devour value is (20+3x intellect) x psychic sensitivity: "+MindFlayer.Value(victim,false)+" expected "+expected);
   // 经验吸收用夺心魔没有天赋加成的技能（天赋会改变显示等级）：让建造成为受害者最高的技能，比较吞噬前后的增量。
   var construction=victim.skills.GetSkill(SkillDefOf.Construction);if(!construction.TotallyDisabled)construction.Level=13;
   var best=MindFlayer.BestSkill(victim);float bestXp=MindFlayer.OwnXp(best);var bestDef=best.def;var mine=caster.skills.GetSkill(bestDef);mine.levelInt=0;mine.xpSinceLastLevel=0;float mineBefore=MindFlayer.OwnXp(mine);
   // 固定站位：施法者在空地中心，目击者在两侧（保证视线）。
   var spot=PowerSpot();caster.Position=spot;caster.Notify_Teleported();victim.Position=spot+IntVec3.North*3;victim.Notify_Teleported();
   var witness=SharedPawn();foreach(var tr in witness.story.traits.allTraits.Where(tr=>MindFlayer.Witnessed.nullifyingTraits.Contains(tr.def)).ToList())witness.story.traits.RemoveTrait(tr);witness.Position=spot+IntVec3.East*2;witness.Notify_Teleported();
   var flayerWitness=MindFlayerPawn();flayerWitness.Position=spot+IntVec3.West*3;flayerWitness.Notify_Teleported();
   var other=OtherFaction();int goodwill=0;if(other!=null){victim.SetFaction(other);goodwill=other.PlayerGoodwill;}
   g.Value=.1f;float gained=MindFlayer.Devour(caster,victim);
   Check(victim.Dead && victim.Corpse!=null && victim.Corpse.InnerPawn.health.hediffSet.GetBrain()==null,"devoured victim dies with a missing brain");
   Check(Near(gained,expected) && Near(g.Points,10+expected),"devouring adds the cerebrum value: "+g.Points);
   Check(mine.TotallyDisabled || Math.Abs(MindFlayer.OwnXp(mine)-mineBefore-bestXp*.2f)<1f,"devourer absorbs 20% of the victim's best skill experience: +"+(MindFlayer.OwnXp(mine)-mineBefore)+" of "+bestXp+" ("+bestDef.defName+")");
   Check(witness.needs.mood.thoughts.memories.GetFirstMemoryOfDef(MindFlayer.Witnessed)!=null && flayerWitness.needs.mood.thoughts.memories.GetFirstMemoryOfDef(MindFlayer.Witnessed)==null,"human witnesses are horrified, mind flayers are not: awake="+witness.Awake()+" los="+GenSight.LineOfSight(witness.Position,caster.Position,map,true));
   Check(MindFlayer.Witnessed.stages[0].baseMoodEffect<DefDatabase<ThoughtDef>.GetNamed("AteHumanlikeMeatDirect").stages[0].baseMoodEffect,"witnessing a brain devour is one step worse than eating human meat");
   if(other!=null)Check(other.PlayerGoodwill<=Math.Max(-100,goodwill-15),"devouring a faction member costs 15 goodwill on top of the native death penalty: "+goodwill+" -> "+other.PlayerGoodwill);
   Check(MindFlayer.CannotDevour(caster,victim.Corpse)!=null,"a corpse without a brain cannot be devoured again");
   victim.Corpse.Destroy();
   // 新鲜尸体给三分之一；超过6小时不能吞噬。
   // 1.20.0：死后再取完整值比较（完整模组列表里有模组会改死者的心灵敏感度等属性）。
   var dead=DownedVictim(10);dead.Kill(null);var corpse=dead.Corpse;float full=MindFlayer.Value(dead,false);
   Check(MindFlayer.CannotDevour(caster,corpse)==null && Near(MindFlayer.Value(dead,true),full/3f),"fresh human corpse can be devoured for a third: "+MindFlayer.Value(dead,true)+" of "+full);
   // 1.31.1：不再限6小时，只看是否开始腐烂。
   corpse.Age=MindFlayer.CorpseFreshTicks*4;Check(corpse.GetRotStage()==RotStage.Fresh && MindFlayer.CannotDevour(caster,corpse)==null,"a corpse older than six hours but not yet rotting can be devoured: "+MindFlayer.CannotDevour(caster,corpse));
   var rot=corpse.TryGetComp<CompRottable>();float rotWas=rot.RotProgress;rot.RotProgress=rot.PropsRot.TicksToRotStart+1;
   Check(corpse.GetRotStage()!=RotStage.Fresh && MindFlayer.CannotDevour(caster,corpse)!=null,"a rotting corpse cannot be devoured");
   rot.RotProgress=rotWas;corpse.Age=100;float before=g.Points;Check(Near(MindFlayer.Devour(caster,corpse),full/3f) && Near(g.Points,Mathf.Min(100,before+full/3f)) && corpse.InnerPawn.health.hediffSet.GetBrain()==null,"devouring a fresh corpse removes its brain for a third of the value");
   corpse.Destroy();
   // 巨魔：大脑被吃掉就不会复活（复用 1.12.0 的大脑检查）。
   // 随机特质可能是心灵失聪（无法吞噬）：重新生成到心灵敏感为止。
   var troll=RacePawn("LP_Troll");for(int attempt=0;attempt<20 && troll.GetStatValue(StatDefOf.PsychicSensitivity)<.5f;attempt++){troll.Destroy();troll=RacePawn("LP_Troll");}troll.health.AddHediff(Unconscious);
   Check(MindFlayer.Devour(caster,troll)>0 && troll.Dead && Troll.Gene(troll).reviveTick<0,"a troll whose brain was devoured does not revive");
   troll.Corpse?.Destroy();
   // 巫妖与死灵造物：吞噬失败，6小时亡灵之味。
   var lich=DownedVictim();lich.health.AddHediff(Necro.Def<HediffDef>("Lich"));
   Check(MindFlayer.CannotDevour(caster,lich)==null && MindFlayer.Devour(caster,lich)==0 && !lich.Dead,"devouring a lich fails and leaves it alive");
   var taste=caster.health.hediffSet.GetFirstHediffOfDef(MindFlayer.Taste);
   Check(taste!=null && taste.TryGetComp<HediffComp_Disappears>().ticksToDisappear==15000,"failed lich devour gives six hours of undead taste");
   caster.health.RemoveHediff(taste);lich.Destroy();
   var construct=DownedVictim();construct.health.AddHediff(Necro.Def<HediffDef>("Construct"));
   Check(MindFlayer.Devour(caster,construct)==0 && !construct.Dead && caster.health.hediffSet.HasHediff(MindFlayer.Taste),"devouring a necromantic construct also fails with undead taste");
   caster.health.RemoveHediff(caster.health.hediffSet.GetFirstHediffOfDef(MindFlayer.Taste));construct.Destroy();
   // 心灵失聪、机械体、未倒地、自己。
   var deaf=DownedVictim();deaf.genes.AddGene(DefDatabase<GeneDef>.GetNamed("PsychicAbility_Deaf"),true);
   // 1.20.0：完整模组列表里有模组给心灵敏感度加固定值，失聪基因不一定能把属性压到0；规则按属性判断，只有夹具真的失聪时才能检查。
   float deafSense=deaf.GetStatValue(StatDefOf.PsychicSensitivity);
   Check(MindFlayer.PsychicallyDeaf(deaf)?MindFlayer.CannotDevour(caster,deaf)!=null && MindFlayer.Devour(caster,deaf)<0 && !deaf.Dead:FullModList,"psychically deaf pawns cannot be devoured (sensitivity "+deafSense.ToString("0.##")+")");
   // 1.31.1：心灵失聪者的新鲜尸体可以吞噬。
   deaf.Kill(null);Check(deaf.Corpse!=null && MindFlayer.CannotDevour(caster,deaf.Corpse)==null && MindFlayer.Devour(caster,deaf.Corpse)>=0 && deaf.health.hediffSet.GetBrain()==null,"a psychically deaf pawn's fresh corpse can be devoured: "+(deaf.Corpse==null?"no corpse":MindFlayer.CannotDevour(caster,deaf.Corpse)));
   if(deaf.Corpse!=null)deaf.Corpse.Destroy();else deaf.Destroy();
   var standing=SharedPawn();Check(MindFlayer.CannotDevour(caster,standing)!=null && MindFlayer.CannotDevour(caster,caster)!=null,"standing targets and self cannot be devoured");standing.Destroy();
   var mechKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Militor");
   if(mechKind!=null){var mech=PawnGenerator.GeneratePawn(new PawnGenerationRequest(mechKind,null));GenSpawn.Spawn(mech,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);Check(MindFlayer.CannotDevour(caster,mech)!=null,"mechanoids have no brain to devour");mech.Destroy();}
   var animal=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("Muffalo"),null));GenSpawn.Spawn(animal,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);animal.health.AddHediff(Unconscious);
   float animalValue=MindFlayer.Value(animal,false);before=g.Points;
   Check(animal.Downed && animalValue>=3 && animalValue<=10 && Near(MindFlayer.Devour(caster,animal),animalValue) && animal.Dead,"devouring an animal gives 3 to 10 cerebrum: "+animalValue);
   animal.Corpse?.Destroy();
   var smart=DownedVictim(20);smart.genes.AddGene(DefDatabase<GeneDef>.GetNamed("PsychicAbility_Extreme"),true);
   Check(Near(MindFlayer.Value(smart,false),80),"devour value caps at 80");smart.Destroy();
   caster.Destroy();witness.Destroy();flayerWitness.Destroy();
  }

  void CheckMindBlast(){
   var caster=MindFlayerPawn();var enemyFaction=Find.FactionManager.AllFactions.FirstOrDefault(f=>f!=Faction.OfPlayer && !f.Hidden && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer));
   var spot=PowerSpot();caster.Position=spot;caster.Notify_Teleported();caster.Rotation=Rot4.North;
   Pawn Place(IntVec3 c,Faction f){var p=SharedPawn();for(int attempt=0;attempt<20 && p.GetStatValue(StatDefOf.PsychicSensitivity)<.5f;attempt++){p.Destroy();p=SharedPawn();}if(f!=null)p.SetFaction(f);p.Position=c;p.Notify_Teleported();return p;}
   var foe=Place(spot+IntVec3.North*3,enemyFaction);var ally=Place(spot+IntVec3.North*2+IntVec3.East,null);var behind=Place(spot+IntVec3.South*3,enemyFaction);
   var ward=TestGene("LPTest_MindWardFull",null,DNDMind.Stat,1f);var shielded=Place(spot+IntVec3.North*4,enemyFaction);shielded.genes.AddGene(ward,true);
   Check(foe.HostileTo(caster) && MindFlayer.ConeCells(caster,foe.Position,MindFlayer.BlastRange,MindFlayer.BlastHalfAngle).Contains(foe.Position) && !MindFlayer.ConeCells(caster,foe.Position,MindFlayer.BlastRange,MindFlayer.BlastHalfAngle).Contains(behind.Position),"mind blast cone covers the front and not the back");
   int hit=MindFlayer.MindBlast(caster,foe.Position);
   Check(foe.stances.stunner.Stunned && foe.health.hediffSet.HasHediff(MindFlayer.Blasted),"mind blast stuns and dazes a hostile in the cone");
   Check(!ally.stances.stunner.Stunned && !behind.stances.stunner.Stunned,"mind blast spares allies and pawns outside the cone");
   Check(!shielded.stances.stunner.Stunned && !shielded.health.hediffSet.HasHediff(MindFlayer.Blasted) && hit==1,"full mind resistance resists the blast through DNDMind.TryResist");
   var ability=caster.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_MindBlast"));var comp=ability.CompOfType<CompMindFlayerAbility>();
   MindFlayer.Cerebrum(caster).Value=.05f;Check(comp.GizmoDisabled(out string why) && why.Contains("脑髓"),"mind blast is disabled below 10 cerebrum");
   MindFlayer.Cerebrum(caster).Value=.5f;comp.Apply(new LocalTargetInfo(foe.Position),LocalTargetInfo.Invalid);Check(Near(MindFlayer.Points(caster),40),"casting mind blast costs 10 cerebrum");
   foreach(var p in new[]{caster,foe,ally,behind,shielded})p.Destroy();
  }

  void CheckHiveLink(){
   var a=MindFlayerPawn();var b=MindFlayerPawn();var hive=a.genes.GetFirstGeneOfType<Gene_HiveLink>();
   float alone;{var c=b;b.DeSpawn();hive.Sync();alone=a.GetStatValue(StatDefOf.PsychicSensitivity);GenSpawn.Spawn(c,a.Position+IntVec3.East,map);}
   hive.Sync();var h=a.health.hediffSet.GetFirstHediffOfDef(MindFlayer.HiveLinkHediff);
   Check(h!=null && Near(h.Severity,1) && Near(a.GetStatValue(StatDefOf.PsychicSensitivity),alone+.1f),"a second mind flayer on the map gives +10% psychic sensitivity");
   var extra=new List<Pawn>{MindFlayerPawn(),MindFlayerPawn(),MindFlayerPawn()};hive.Sync();
   Check(Near(h.Severity,3),"hive link counts at most three companions");
   foreach(var e in extra)e.Destroy();
   b.Kill(null);
   Check(a.needs.mood.thoughts.memories.GetFirstMemoryOfDef(MindFlayer.HiveBroken)!=null,"a companion's death breaks the hive and hurts the others");
   hive.Sync();Check(a.health.hediffSet.GetFirstHediffOfDef(MindFlayer.HiveLinkHediff)==null,"alone again the hive bonus disappears");
   b.Corpse?.Destroy();a.Destroy();
  }

  void CheckTadpoles(){
   var implant=DefDatabase<RecipeDef>.GetNamed("LP_ImplantTadpole");var remove=DefDatabase<RecipeDef>.GetNamed("LP_RemoveTadpole");
   Check(implant.Worker is Recipe_ImplantTadpole && implant.isViolation && ThingDefOf.Human.AllRecipes.Contains(implant) && ThingDefOf.Human.AllRecipes.Contains(remove),"tadpole implant and removal surgeries are available on humans");
   Check(ContentFinder<Texture2D>.Get(Tadpoles.Item.graphicData.texPath,false)!=null && Tadpoles.Item.GetCompProperties<CompProperties_Rottable>()?.daysToRotStart==15,"tadpole item has a texture and dies after 15 days outside a host");
   var doctor=SharedPawn();var host=SharedPawn();
   Check(implant.Worker.AvailableOnNow(host),"implant surgery is offered on an ordinary human");
   var h=Tadpoles.Implant(host,doctor,Faction.OfPlayer,out string why);
   Check(h!=null && why==null && h.Part==MindFlayer.Brain(host) && h.implanter==doctor && h.CurStageIndex==0,"tadpole implants into the brain and records the implanter");
   Check(!implant.Worker.AvailableOnNow(host) && Tadpoles.Implant(host,doctor,Faction.OfPlayer,out why)==null,"a host cannot be implanted twice");
   int days=LichMod.Settings.ceremorphDays;
   h.Advance(60000);Check(Near(h.Severity,.2f+.001f) && h.CurStageIndex==1,"default five day ceremorph advances 20% per day into the second stage: "+h.Severity);
   LichMod.Settings.ceremorphDays=10;try{float s=h.Severity;h.Advance(60000);Check(Near(h.Severity-s,.1f),"ceremorph days setting slows progress");}finally{LichMod.Settings.ceremorphDays=days;}
   Check(DefDatabase<ThoughtDef>.GetNamed("LP_TadpoleParasite").Worker.CurrentState(host).Active,"a parasitized host feels the tadpole");
   h.Severity=.65f;Check(h.CurStageIndex==3 && host.health.hediffSet.GetFirstHediffOfDef(Tadpoles.Parasite)!=null,"five tadpole stages follow progress");
   Check(remove.Worker.GetPartsToApplyOn(host,remove).Contains(MindFlayer.Brain(host)),"removal surgery targets the infested brain");
   remove.Worker.ApplyOnPawn(host,MindFlayer.Brain(host),null,new List<Thing>(),null);
   Check(Tadpoles.Of(host)==null && !MindFlayer.IsMindFlayer(host),"removal surgery takes the tadpole out before ceremorph");
   // 抗性：火巨人免疫、巫妖/死灵造物/机械体免疫；精灵按心智抗性减速；巨魔减半并会排出；心灵失聪会饿死蝌蚪。
   var giant=RacePawn("LP_FireGiant");Check(Tadpoles.Implant(giant,doctor,Faction.OfPlayer,out why)==null && why.Contains("火巨人"),"fire giants are immune to tadpoles");
   var lich=SharedPawn();lich.health.AddHediff(Necro.Def<HediffDef>("Lich"));Check(Tadpoles.Implant(lich,doctor,Faction.OfPlayer,out why)==null,"liches are immune to tadpoles");
   var construct=SharedPawn();construct.health.AddHediff(Necro.Def<HediffDef>("Construct"));Check(Tadpoles.Immunity(construct)!=null,"necromantic constructs are immune to tadpoles");
   var flayer=MindFlayerPawn();Check(Tadpoles.Immunity(flayer)!=null && !implant.Worker.AvailableOnNow(flayer),"mind flayers cannot be implanted");
   var child=RacePawn("Baseliner",8);Check(Tadpoles.Immunity(child)!=null && !implant.Worker.AvailableOnNow(child),"children cannot be implanted");child.Destroy();
   var elf=RacePawn("LP_Elf");var eh=Tadpoles.Implant(elf,doctor,Faction.OfPlayer,out why);float s0=eh.Severity;eh.Advance(60000);
   Check(Near(eh.Severity-s0,.2f*.5f),"elf mind resistance halves ceremorph speed through LP_MindResist: "+(eh.Severity-s0));
   var troll=RacePawn("LP_Troll");var th=Tadpoles.Implant(troll,doctor,Faction.OfPlayer,out why);s0=th.Severity;th.Advance(60000);
   Check(Near(th.Severity-s0,.1f),"trolls ceremorph at half speed");
   Check(!th.DailyRoll(.5f) && Tadpoles.Of(troll)!=null && th.DailyRoll(.1f) && Tadpoles.Of(troll)==null,"trolls expel the tadpole on a 20% daily roll");
   var deaf=SharedPawn();deaf.genes.AddGene(DefDatabase<GeneDef>.GetNamed("PsychicAbility_Deaf"),true);var dh=Tadpoles.Implant(deaf,doctor,Faction.OfPlayer,out why);
   Check(MindFlayer.PsychicallyDeaf(deaf)?dh!=null && !dh.DailyRoll(.4f) && dh.DailyRoll(.25f) && Tadpoles.Of(deaf)==null:FullModList,"psychically deaf hosts starve the tadpole on a 30% daily roll");
   var plain=SharedPawn();for(int attempt=0;attempt<20 && MindFlayer.PsychicallyDeaf(plain);attempt++){plain.Destroy();plain=SharedPawn();}var ph=Tadpoles.Implant(plain,doctor,Faction.OfPlayer,out why);Check(!ph.DailyRoll(0f) && Tadpoles.Of(plain)!=null,"ordinary hosts never lose the tadpole on the daily roll");
   // 化形：囚犯被植入后成为植入者阵营的夺心魔。
   var prisoner=SharedPawn();var other=OtherFaction();if(other!=null)prisoner.SetFaction(other);prisoner.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);
   var trait=DefDatabase<TraitDef>.GetNamed("Kind");if(!prisoner.story.traits.HasTrait(trait))prisoner.story.traits.GainTrait(new Trait(trait));
   // 坚韧会被夺心魔的体弱基因压制：回归测试"先重置特质再换异种型"。
   var tough=DefDatabase<TraitDef>.GetNamed("Tough");if(!prisoner.story.traits.HasTrait(tough))prisoner.story.traits.GainTrait(new Trait(tough));
   var shooting=prisoner.skills.GetSkill(SkillDefOf.Shooting);if(!shooting.TotallyDisabled)shooting.Level=10;float shootXp=MindFlayer.OwnXp(shooting);
   var endogenes=prisoner.genes.Endogenes.Select(g=>g.def).ToList();var childhood=prisoner.story.Childhood;
   var ch=Tadpoles.Implant(prisoner,doctor,Faction.OfPlayer,out why);Check(ch!=null && prisoner.IsPrisonerOfColony,"prisoner fixture is parasitized");
   ch.Severity=.95f;ch.Advance(60000);
   Check(Tadpoles.Of(prisoner)==null && prisoner.genes.Xenotype==MindFlayer.Xenotype && MindFlayer.IsMindFlayer(prisoner),"reaching 100% ceremorphs the host into a mind flayer");
   Check(endogenes.All(d=>prisoner.genes.Endogenes.Any(g=>g.def==d)) && prisoner.genes.IsXenogene(prisoner.genes.GetGene(MindFlayer.CerebrumGene)),"ceremorph keeps every endogene and adds the mind flayer genes as xenogenes");
   Check(!prisoner.story.traits.HasTrait(trait) && !prisoner.story.traits.HasTrait(tough) && prisoner.story.traits.allTraits.All(t=>t.sourceGene!=null),"ceremorph resets the host's traits: "+string.Join(",",prisoner.story.traits.allTraits.Select(tr=>tr.def.defName+(tr.sourceGene!=null?"(gene "+tr.sourceGene.def.defName+")":"")+(tr.Suppressed?"[suppressed]":""))));
   Check(prisoner.story.Adulthood==Tadpoles.Ceremorphed && prisoner.story.Childhood==childhood,"ceremorph replaces the adulthood with the ceremorphed backstory");
   Check(shooting.TotallyDisabled || Math.Abs(MindFlayer.OwnXp(shooting)-shootXp*.5f)<2f,"ceremorph keeps half of each skill's experience: "+MindFlayer.OwnXp(shooting)+" of "+shootXp);
   Check(prisoner.Faction==Faction.OfPlayer && !prisoner.IsPrisoner,"the new mind flayer joins the implanter's faction");
   Check(prisoner.relations.DirectRelationExists(Tadpoles.Relation,doctor),"the new mind flayer is bound to its implanter by the ceremorph relation");
   Check(Near(MindFlayer.Points(prisoner),50),"a fresh ceremorph starts with 50 cerebrum");
   // 屠宰夺心魔尸体：50%得到蝌蚪。
   var corpseFlayer=MindFlayerPawn();int yields=0;Rand.PushState(777);try{for(int i=0;i<400;i++)if(corpseFlayer.ButcherProducts(doctor,1f).Any(t=>t.def==Tadpoles.Item))yields++;}finally{Rand.PopState();}
   Check(yields>160 && yields<240 && !doctor.ButcherProducts(doctor,1f).Any(t=>t.def==Tadpoles.Item),"butchering a mind flayer corpse yields a tadpole half the time: "+yields+"/400");
   foreach(var p in new[]{doctor,host,giant,lich,construct,flayer,elf,troll,deaf,plain,prisoner,corpseFlayer})if(!p.Destroyed)p.Destroy();
  }

  IntVec3 ClearSpot(int radius=3){for(int r=radius;r>=2;r--){var c=map.AllCells.FirstOrDefault(x=>x.x>15 && x.z>15 && x.x<map.Size.x-15 && x.z<map.Size.z-15 && !x.InHorDistOf(altar.Position,8) && CellRect.CenteredOn(x,r).Cells.All(y=>y.InBounds(map) && y.Standable(map) && !y.Fogged(map) && y.GetFirstPawn(map)==null && y.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth)));if(c.IsValid && c!=IntVec3.Zero)return c;}throw new Exception("no clear spot for the fixture");}
  // 灵能测试需要的格子：施法者周围这些偏移都能站人、没有别人，且与施法者之间有视线。
  static readonly IntVec3[] powerOffsets={IntVec3.North*2+IntVec3.East,IntVec3.North*3,IntVec3.North*3+IntVec3.East*2,IntVec3.North*4,IntVec3.West*3,IntVec3.West*4,IntVec3.East*2,IntVec3.East*3,IntVec3.East*4,IntVec3.East*5,IntVec3.East*6,IntVec3.South*3,IntVec3.South*4,IntVec3.South*12,IntVec3.North*5,IntVec3.North*6};
  IntVec3 PowerSpot()=>map.AllCells.First(c=>c.x>15 && c.z>15 && c.x<map.Size.x-15 && c.z<map.Size.z-15 && !c.InHorDistOf(altar.Position,8) && c.Standable(map) && c.GetFirstPawn(map)==null && powerOffsets.All(o=>(c+o).InBounds(map) && (c+o).Standable(map) && !(c+o).Fogged(map) && (c+o).GetFirstPawn(map)==null && GenSight.LineOfSight(c,c+o,map,true)));
  Building_ElderBrain SpawnElderBrain(){var b=(Building_ElderBrain)ThingMaker.MakeThing(ElderBrain.Def);b.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(b,ClearSpot(),map);return b;}
  Pawn FlayerAt(IntVec3 c){var p=MindFlayerPawn();p.Position=c;p.Notify_Teleported();return p;}
  void CheckElderBrain(){
   var def=ElderBrain.Def;var fuel=def.GetCompProperties<CompProperties_Refuelable>();
   Check(def.size==new IntVec2(3,3) && def.thingClass==typeof(Building_ElderBrain) && ContentFinder<Texture2D>.Get(def.graphicData.texPath,false)!=null,"elder brain is a 3x3 building with a texture");
   Check(fuel.fuelFilter.Allows(ThingDefOf.MedicineHerbal) && fuel.fuelFilter.Allows(ThingDefOf.Meat_Human) && fuel.fuelFilter.Allows(ThingDefOf.MealSimple) && !fuel.fuelFilter.Allows(ThingDefOf.Steel),"elder brain is maintained with herbal medicine or food");
   Check(new[]{0,1,3,4,9,10,19,20,34,35,99}.Select(ElderBrain.LevelFor).SequenceEqual(new[]{0,1,1,2,2,3,3,4,4,5,5}),"elder brain levels need 1/4/10/20/35 brains");
   var b=SpawnElderBrain();
   Check(!b.initialized && b.Level==0,"a new elder brain sleeps until fed a mind flayer corpse");
   var human=SharedPawn();human.Position=b.Position+IntVec3.East*2;human.Notify_Teleported();human.Kill(null);b.Process(250);
   Check(!b.initialized && !human.Corpse.Destroyed,"an ordinary corpse does not wake the elder brain");human.Corpse.Destroy();
   var dead=FlayerAt(b.Position+IntVec3.West*2);dead.Kill(null);var corpse=dead.Corpse;b.Process(250);
   Check(b.initialized && b.brains==1 && b.Level==1 && corpse.Destroyed,"a mind flayer corpse next to it wakes the elder brain at level 1");
   // 吞噬人形大脑：计数并保存记忆，最多5份，丢弃最早的。
   var caster=FlayerAt(b.Position+IntVec3.South*3);var names=new List<string>();
   for(int i=0;i<6;i++){var v=DownedVictim();v.Position=caster.Position+IntVec3.East;v.Notify_Teleported();names.Add(v.LabelShort);MindFlayer.Devour(caster,v);v.Corpse?.Destroy();}
   Check(b.brains==7 && b.Level==2,"each human brain devoured on the map feeds the elder brain: "+b.brains);
   Check(b.memories.Count==5 && b.memories.Select(m=>m.name).SequenceEqual(names.Skip(1)),"the elder brain keeps the five latest inherited memories");
   var animal=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("Muffalo"),null));GenSpawn.Spawn(animal,caster.Position+IntVec3.West,map);animal.health.AddHediff(Unconscious);MindFlayer.Devour(caster,animal);animal.Corpse?.Destroy();
   Check(b.brains==7,"animal brains do not count toward the elder brain");
   // 养料与蝌蚪产出。
   b.Food.Refuel(20f);Check(b.Fed,"elder brain accepts food");
   int Tadpoles_()=>map.listerThings.ThingsOfDef(Tadpoles.Item).Sum(t=>t.stackCount);int t0=Tadpoles_();
   Check(b.TadpoleInterval==Mathf.RoundToInt(4f*60000),"level 2 elder brain makes a tadpole every 4 days");
   b.tadpoleTicks=b.TadpoleInterval-100;b.Process(250);
   Check(Tadpoles_()==t0+1 && b.tadpoleTicks==0,"a fed elder brain spawns a tadpole when the timer completes");
   b.brains=35;Check(b.Level==5 && b.TadpoleInterval==60000,"a level 5 elder brain makes a tadpole every day");
   // 饥饿：停止产出，每天丢一份记忆，夺心魔心情受影响。
   b.Food.ConsumeFuel(b.Food.Fuel);int memoriesBefore=b.memories.Count;b.tadpoleTicks=b.TadpoleInterval-100;t0=Tadpoles_();
   b.Process(60000);
   Check(!b.Fed && b.memories.Count==memoriesBefore-1 && Tadpoles_()==t0 && caster.needs.mood.thoughts.memories.GetFirstMemoryOfDef(ElderBrain.Starving)!=null,"a starving elder brain stops producing and loses a memory per day");
   b.Food.Refuel(20f);
   // 脑浆罐与传承记忆。
   var best=b.memories[0].Best;float stored=b.memories[0].xp[best];int before=b.memories.Count;
   var jar=(Thing_BrainJar)b.FillJar(0);
   Check(jar!=null && jar.Spawned && jar.memory!=null && b.memories.Count==before-1 && jar.LabelNoCount.Contains(jar.memory.name),"filling a brain jar moves one memory into an item");
   var use=jar.GetComp<CompUseEffect_InheritMemory>();var outsider=SharedPawn();
   Check(!use.CanBeUsedBy(outsider).Accepted && use.CanBeUsedBy(caster).Accepted,"only mind flayers can absorb a brain jar");
   var skill=caster.skills.GetSkill(best);skill.levelInt=0;skill.xpSinceLastLevel=0;float own=MindFlayer.OwnXp(skill);
   use.DoEffect(caster);
   Check(jar.Destroyed && (skill.TotallyDisabled || Math.Abs(MindFlayer.OwnXp(skill)-own-stored*.8f)<1f),"absorbing a brain jar inherits 80% of the stored experience: +"+(MindFlayer.OwnXp(skill)-own)+" of "+stored);
   // 召唤：默认关闭；打开后3级以上召唤流浪者，走到旁边倒下。
   b.summonTicks=ElderBrain.SummonIntervalDays*60000;int pawns=map.mapPawns.AllPawnsSpawned.Count;b.Process(250);
   Check(map.mapPawns.AllPawnsSpawned.Count==pawns,"summoning stays off by default");
   LichMod.Settings.elderBrainSummon=true;
   try{
    var called=b.Summon();var hc=called?.health.hediffSet.GetFirstHediffOfDef(ElderBrain.Call) as Hediff_ElderBrainCall;
    Check(called!=null && called.Faction==null && hc!=null && hc.brain==b && called.CurJobDef==JobDefOf.Goto,"a summoned drifter walks toward the elder brain");
    called.Position=b.Position+IntVec3.South*3;called.Notify_Teleported();hc.Pull();
    Check(called.Downed && called.health.hediffSet.HasHediff(ElderBrain.Daze) && !called.health.hediffSet.HasHediff(ElderBrain.Call),"arriving at the elder brain the summoned pawn collapses");
    called.Destroy();
   }finally{LichMod.Settings.elderBrainSummon=false;}
   // 拆除不惩罚；被摧毁惩罚所有夺心魔。
   var other=SpawnElderBrain();other.initialized=true;other.brains=4;other.Destroy(DestroyMode.Deconstruct);
   Check(caster.needs.mood.thoughts.memories.GetFirstMemoryOfDef(ElderBrain.Destroyed)==null,"deconstructing an elder brain does not punish the mind flayers");
   MindFlayer.Cerebrum(caster).Value=.8f;b.Destroy(DestroyMode.KillFinalize);
   Check(caster.needs.mood.thoughts.memories.GetFirstMemoryOfDef(ElderBrain.Destroyed)!=null && Near(MindFlayer.Points(caster),50) && caster.stances.stunner.Stunned,"destroying the elder brain shocks every mind flayer: -15 mood, -30 cerebrum");
   foreach(var t in map.listerThings.ThingsOfDef(Tadpoles.Item).ToList())t.Destroy();
   caster.Destroy();outsider.Destroy();
  }

  // 1.20.0 A9：10点钝伤打中鼻子这类小部位会直接打掉部位（伤势变成缺失部位），总体健康百分比不变；按“造成了伤害且留下伤势或缺失部位”判断。
  static bool ThrowHurt(Pawn p,DamageWorker.DamageResult r)=>r!=null && r.totalDamageDealt>0 && (p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any() || p.health.hediffSet.GetMissingPartsCommonAncestors().Any());
  static string ThrowDiag(Pawn p,DamageWorker.DamageResult r)=>r==null?"no result":"dealt="+r.totalDamageDealt.ToString("0.#")+" deflected="+r.deflected+" diminished="+r.diminished+" parts="+string.Join("/",r.parts?.Select(b=>b.def.defName)??new string[0])+" injuries="+p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count()+" missing="+p.health.hediffSet.GetMissingPartsCommonAncestors().Count()+" dead="+p.Dead;
  Pawn EnemyAt(IntVec3 c,Faction f){var p=SharedPawn();for(int attempt=0;attempt<20 && (p.GetStatValue(StatDefOf.PsychicSensitivity)<.5f || p.WorkTagIsDisabled(WorkTags.Violent));attempt++){p.Destroy();p=SharedPawn();}if(f!=null)p.SetFaction(f);p.Position=c;p.Notify_Teleported();return p;}
  void CheckMindFlayerPowers(){
   var cooldowns=new Dictionary<string,int>{{"LP_ReadMind",2500},{"LP_Dominate",120000},{"LP_PsychicBind",60000},{"LP_TelekineticThrow",15000},{"LP_Unseen",60000},{"LP_PsychicStorm",300000}};
   foreach(var kv in cooldowns){var def=DefDatabase<AbilityDef>.GetNamed(kv.Key);Check(!def.ConfigErrors().Any() && def.cooldownTicksRange.min==kv.Value && ContentFinder<Texture2D>.Get(def.iconPath,false)!=null,"psionic "+kv.Key+" validates with cooldown "+kv.Value);}
   var caster=MindFlayerPawn();var spot=PowerSpot();caster.Position=spot;caster.Notify_Teleported();caster.Rotation=Rot4.North;
   Check(new[]{"LP_ReadMind","LP_Dominate","LP_TelekineticThrow","LP_Unseen","LP_PsychicStorm"}.All(n=>caster.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed(n))!=null),"mind flayers know read mind, domination, telekinetic throw, unseen and psychic storm");
   Check((caster.abilities.GetAbility(MindFlayerPowers.Bind)!=null)==ModsConfig.IdeologyActive,"psychic bind is granted only when Ideology is loaded");
   var enemy=Find.FactionManager.AllFactions.First(f=>f!=Faction.OfPlayer && !f.Hidden && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer));
   // 读心
   var prisoner=EnemyAt(spot+IntVec3.West*3,enemy);prisoner.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);prisoner.guest.resistance=10;
   string read=MindFlayerPowers.ReadMind(caster,prisoner);
   Check(read.Contains("特质") && Near(prisoner.guest.resistance,7),"read mind reveals traits and lowers a prisoner's resistance by 3");
   // 支配人类
   var a=EnemyAt(spot+IntVec3.North*3,enemy);var b=EnemyAt(spot+IntVec3.North*3+IntVec3.East*2,enemy);
   var colonist=SharedPawn();colonist.Position=spot+IntVec3.North*5;colonist.Notify_Teleported();var otherFlayer=MindFlayerPawn();otherFlayer.SetFaction(enemy);otherFlayer.Position=spot+IntVec3.North*6;otherFlayer.Notify_Teleported();
   Check(MindFlayerPowers.CannotDominate(caster,prisoner)==null && MindFlayerPowers.CannotDominate(caster,colonist)!=null && MindFlayerPowers.CannotDominate(caster,otherFlayer)!=null,"domination targets other factions' humans, never colonists or mind flayers");
   Check(MindFlayerPowers.Dominate(caster,a) && a.MentalStateDef==MindFlayerPowers.Dominated && a.health.hediffSet.HasHediff(MindFlayerPowers.DominatedHediff),"domination puts the target into the dominated state");
   var rage=a.MentalState as MentalState_MurderousRage;
   Check(rage!=null && rage.target!=null && rage.target.Faction==a.Faction && rage.target!=a,"a dominated pawn only attacks its original faction: "+rage?.target);
   Check(a.health.hediffSet.GetFirstHediffOfDef(MindFlayerPowers.DominatedHediff).TryGetComp<HediffComp_Disappears>().ticksToDisappear==1800,"domination lasts 30 seconds");
   a.health.RemoveHediff(a.health.hediffSet.GetFirstHediffOfDef(MindFlayerPowers.DominatedHediff));
   Check(!a.InMentalState && a.Downed && a.health.hediffSet.HasHediff(ElderBrain.Daze),"when domination ends the pawn collapses");
   var ward=TestGene("LPTest_MindWardFull",null,DNDMind.Stat,1f);var strong=EnemyAt(spot+IntVec3.North*4,enemy);strong.genes.AddGene(ward,true);
   Check(!MindFlayerPowers.Dominate(caster,strong) && !strong.InMentalState,"domination is resisted through DNDMind.TryResist");
   // 心灵束缚（仅意识形态）
   if(ModsConfig.IdeologyActive && caster.Ideo!=null){
    var other=Find.IdeoManager.IdeosListForReading.FirstOrDefault(i=>i!=caster.Ideo);
    if(other!=null){var convert=EnemyAt(spot+IntVec3.West*4,enemy);convert.ideo.SetIdeo(other);Traverse.Create(convert.ideo).Field("certaintyInt").SetValue(.9f);
     Check(MindFlayerPowers.CannotBind(caster,convert)==null && MindFlayerPowers.PsychicBind(caster,convert) && convert.ideo.Certainty<.9f-.1f,"psychic bind shakes the target's certainty: "+convert.ideo.Certainty);
     Traverse.Create(convert.ideo).Field("certaintyInt").SetValue(.01f);MindFlayerPowers.PsychicBind(caster,convert);
     Check(convert.Ideo==caster.Ideo,"psychic bind converts a target whose certainty breaks");convert.Destroy();}
   }
   // 念力投掷
   var thrown=EnemyAt(spot+IntVec3.East*2,enemy);int before=thrown.Position.DistanceToSquared(caster.Position);
   var dest=MindFlayerPowers.Throw(caster,thrown,out var thrownHit);
   Check(thrown.Position==dest && thrown.Position.DistanceToSquared(caster.Position)>before && ThrowHurt(thrown,thrownHit),"telekinetic throw flings the target away and hurts it: moved="+(thrown.Position==dest)+" farther="+(thrown.Position.DistanceToSquared(caster.Position)>before)+" "+ThrowDiag(thrown,thrownHit));
   // 1.20.0 A9：连续投掷 100 次，统计没掉血的原因（查偶发失败）。
   // 完整模组列表里有模组给人加天然护甲，10点钝伤偶尔被整个弹开（deflected）；只在那里放行这种情况，其余任何没掉血都算失败。
   int misses=0,deflects=0;var missWhy=new Dictionary<string,int>();
   for(int i=0;i<100;i++){
    var t2=EnemyAt(spot+IntVec3.East*2,enemy);MindFlayerPowers.Throw(caster,t2,out var h2);
    if(!ThrowHurt(t2,h2) && FullModList && h2!=null && h2.deflected)deflects++;
    else if(!ThrowHurt(t2,h2)){misses++;string k=ThrowDiag(t2,h2);missWhy[k]=(missWhy.TryGetValue(k,out int mn)?mn:0)+1;}
    t2.Destroy();
   }
   Check(misses==0 && deflects<=10,"100 telekinetic throws all hurt their targets: "+misses+" misses, "+deflects+" deflected by third-party armor "+string.Join(" | ",missWhy.Select(kv=>kv.Value+"x "+kv.Key)));
   // 灵能隐匿
   var hide=MindFlayerPowers.Hide(caster);
   Check(hide.def==MindFlayerPowers.Unseen && hide.TryGetComp<HediffComp_Invisibility>()!=null && hide.TryGetComp<HediffComp_Disappears>().ticksToDisappear==2500,"unseen makes the mind flayer psychically invisible for an hour: "+hide.def.defName);
   caster.health.RemoveHediff(hide);
   // 心灵风暴
   var near=EnemyAt(spot+IntVec3.South*4,enemy);var far=EnemyAt(spot+IntVec3.South*12,enemy);var friend=SharedPawn();friend.Position=spot+IntVec3.South*3;friend.Notify_Teleported();
   int hit=MindFlayerPowers.Storm(caster);
   Check(near.stances.stunner.Stunned && near.health.hediffSet.HasHediff(MindFlayer.Blasted) && !far.stances.stunner.Stunned && !friend.stances.stunner.Stunned,"psychic storm stuns hostiles within 8 cells only");
   int resisted=0;Rand.PushState(99);try{for(int i=0;i<400;i++)if(DNDMind.TryResist(strong,MindFlayerPowers.StormResistFactor))resisted++;}finally{Rand.PopState();}
   Check(resisted>150 && resisted<250,"psychic storm halves mind resistance: "+resisted+"/400");
   var storm=caster.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_PsychicStorm")).CompOfType<CompMindFlayerAbility>();
   MindFlayer.Cerebrum(caster).Value=.3f;Check(storm.GizmoDisabled(out string why),"psychic storm needs 40 cerebrum");
   foreach(var p in new[]{caster,prisoner,a,b,strong,thrown,near,far,friend,colonist,otherFlayer})if(!p.Destroyed)p.Destroy();
  }

  void CheckHalfIllithid(){
   var drug=DefDatabase<ThingDef>.GetNamed("LP_PsionicSuppressant");
   Check(drug.ingestible.outcomeDoers.Any(d=>d is IngestionOutcomeDoer_PsionicSuppress) && DefDatabase<RecipeDef>.AllDefs.Any(r=>r.ProducedThingDef==drug && r.AllRecipeUsers.Any(u=>u.defName=="DrugLab")),"psionic suppressant is a drug made at the drug lab");
   var doctor=SharedPawn();var host=SharedPawn();var t=Tadpoles.Implant(host,doctor,Faction.OfPlayer,out _);t.Severity=.3f;
   var pill=ThingMaker.MakeThing(drug);GenSpawn.Spawn(pill,host.Position,map);pill.Ingested(host,0);
   var supp=host.health.hediffSet.GetFirstHediffOfDef(HalfIllithid.Suppression);var g=HalfIllithid.Of(host);
   Check(supp!=null && supp.TryGetComp<HediffComp_Disappears>().ticksToDisappear==180000,"suppressant lasts three days");
   Check(g!=null && g.Active && host.genes.IsXenogene(g) && host.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_ReadMind"))!=null && host.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_DevourBrain"))!=null,"a host dosed before 50% becomes a half-illithid with read mind and devour");
   t.Advance(60000);Check(Near(t.Severity,.3f) && t.Frozen && t.LabelInBrackets.Contains("抑制"),"suppressant freezes the ceremorph");
   host.health.RemoveHediff(supp);t.Advance(60000);Check(t.Severity>.45f && !t.Frozen,"three days without the drug the ceremorph resumes");
   var late=SharedPawn();var lt=Tadpoles.Implant(late,doctor,Faction.OfPlayer,out _);lt.Severity=.6f;HalfIllithid.Dose(late);float ls=lt.Severity;lt.Advance(60000);
   Check(HalfIllithid.Of(late)==null && lt.Severity>ls && !lt.Frozen,"after 50% the suppressant can no longer stop the ceremorph");
   // 吞噬大脑解锁灵能，最多3项；灵能免费。
   HalfIllithid.Dose(host);var unlocked=new List<string>();
   for(int i=0;i<4;i++){var v=DownedVictim();v.Position=host.Position+IntVec3.East;v.Notify_Teleported();HalfIllithid.Dose(host);Check(MindFlayer.Devour(host,v)>=0 && v.Dead,"half-illithid can devour brains");v.Corpse?.Destroy();unlocked.Add(g.unlocked.ToString());}
   Check(g.unlocked==3 && HalfIllithid.Unlocks.All(n=>host.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed(n))!=null) && string.Join(",",unlocked)=="1,2,3,3","each devoured brain unlocks one psionic power, at most three: "+string.Join(",",unlocked));
   Check(MindFlayer.Points(host)==0 && host.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_MindBlast")).CompOfType<CompMindFlayerAbility>().Free,"half-illithid psionics cost no cerebrum");
   int fails=0;Rand.PushState(4321);try{for(int i=0;i<200;i++){HalfIllithid.Dose(host);MindFlayerHooks.Devoured(host,doctor,true);if(!HalfIllithid.Suppressed(host))fails++;}}finally{Rand.PopState();}
   Check(fails>30 && fails<70,"each devour has a 25% chance to break the suppression: "+fails+"/200");
   var victim=DownedVictim();victim.Position=host.Position+IntVec3.North;victim.Notify_Teleported();var witness=HalfIllithid.Is(host)?host:null;var flayer=MindFlayerPawn();flayer.Position=host.Position+IntVec3.West;flayer.Notify_Teleported();
   MindFlayer.Devour(flayer,victim);Check(host.needs.mood.thoughts.memories.GetFirstMemoryOfDef(MindFlayer.Witnessed)==null,"half-illithids are not horrified by brain devouring");
   victim.Corpse?.Destroy();
   Check(!g.CheckHost() && HalfIllithid.Of(host)!=null,"a half-illithid keeps its powers while the tadpole lives");
   host.health.RemoveHediff(Tadpoles.Of(host));
   Check(g.CheckHost() && HalfIllithid.Of(host)==null && host.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_MindBlast"))==null && host.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_ReadMind"))==null,"removing the tadpole ends the half-illithid powers");
   foreach(var p in new[]{doctor,host,late,flayer})if(!p.Destroyed)p.Destroy();
  }

  void PrepareMindFlayerSave(){
   var p=MindFlayerPawn();MindFlayer.Cerebrum(p).Value=.2345f;MindFlayer.Cerebrum(p).SyncHunger();flayerSaveId=p.ThingID;flayerSavedPoints=23.45f;flayerGeneCount=p.genes.GenesListForReading.Count;
   var doctor=SharedPawn();var host=SharedPawn();var h=Tadpoles.Implant(host,doctor,Faction.OfPlayer,out _);h.Severity=.3456f;tadpoleHostId=host.ThingID;tadpoleDoctorId=doctor.ThingID;tadpoleSavedProgress=.3456f;
   var b=SpawnElderBrain();b.initialized=true;b.brains=12;b.tadpoleTicks=4321;b.Food.Refuel(15f);b.memories.Add(new BrainMemory{name="测试记忆",xp=new Dictionary<SkillDef,float>{{SkillDefOf.Shooting,1234f}}});elderBrainId=b.ThingID;
   var half=SharedPawn();Tadpoles.Implant(half,doctor,Faction.OfPlayer,out _).Severity=.25f;HalfIllithid.Dose(half);HalfIllithid.Of(half).unlocked=2;HalfIllithid.Of(half).SyncPowers();halfIllithidId=half.ThingID;
  }
  void CheckMindFlayerAfterLoad(){
   map=Find.CurrentMap;var p=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==flayerSaveId);
   Check(p.genes.Xenotype==MindFlayer.Xenotype && p.genes.GenesListForReading.Count==flayerGeneCount && Math.Abs(MindFlayer.Points(p)-flayerSavedPoints)<.2f && p.health.hediffSet.HasHediff(MindFlayer.Hunger),"mind flayer genes, cerebrum and hunger survive save/load: "+MindFlayer.Points(p));
   var host=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==tadpoleHostId);var doctor=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==tadpoleDoctorId);var h=Tadpoles.Of(host);
   Check(h!=null && Math.Abs(h.Severity-tadpoleSavedProgress)<.01f && h.implanter==doctor && h.implanterFaction==Faction.OfPlayer && h.Part==MindFlayer.Brain(host),"tadpole progress, implanter and faction survive save/load");
   host.Destroy();doctor.Destroy();
   var b=map.listerThings.ThingsOfDef(ElderBrain.Def).OfType<Building_ElderBrain>().Single(x=>x.ThingID==elderBrainId);
   Check(b.initialized && b.brains==12 && b.Level==3 && Math.Abs(b.tadpoleTicks-4321)<=250 && Near(b.Food.Fuel,15f) && b.memories.Count==1 && b.memories[0].name=="测试记忆" && Near(b.memories[0].xp[SkillDefOf.Shooting],1234f),"elder brain level, timers, food and memories survive save/load");
   b.Destroy();
   var half=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==halfIllithidId);var hg=HalfIllithid.Of(half);
   Check(hg!=null && hg.unlocked==2 && half.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_TelekineticThrow"))!=null && half.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_Unseen"))==null && HalfIllithid.Suppressed(half) && Tadpoles.Of(half).Frozen,"half-illithid powers, suppression and frozen tadpole survive save/load");
   half.Destroy();
   p.Destroy();
  }
 }
}
