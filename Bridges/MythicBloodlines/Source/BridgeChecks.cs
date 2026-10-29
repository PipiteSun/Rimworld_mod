using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using MythicBloodlines;

namespace LichPhylactery.MythicBridge {
 // 只在两模组都启用时由 LichChecks 通过反射调用（-lichtest）。
 public static class BridgeChecks {
  static int checks;static string savedSwordId,savedMeatId;
  static Map map;static IntVec3 center;static readonly List<Pawn> made=new List<Pawn>();
  static void Check(bool ok,string message){if(!ok)throw new Exception("[mythic bridge] "+message);checks++;Log.Message("[LICH-TEST] OK [mythic bridge] "+message);}
  static bool Near(float a,float b)=>Math.Abs(a-b)<=Math.Max(.02f,Math.Abs(b)*.001f);
  static BridgeState S=>MythicBridge.State;

  public static int Run(){
   checks=0;map=Find.CurrentMap;made.Clear();
   center=map.AllCells.Where(c=>c.x>15 && c.z>15 && c.x<map.Size.x-15 && c.z<map.Size.z-15 && CellRect.CenteredOn(c,6).Cells.All(x=>x.Standable(map) && !x.Fogged(map) && !x.Roofed(map) && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth))).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
   foreach(var c in CellRect.CenteredOn(center,6).Cells)foreach(var plant in c.GetThingList(map).OfType<Plant>().ToList())plant.Destroy();
   try{
    Definitions();Relations();Rain();Eating();Hoard();Reveal();Alchemy();
   }finally{foreach(var p in made)if(!p.Destroyed)p.Destroy();made.Clear();}
   return checks;
  }
  public static int AfterLoad(){
   checks=0;map=Find.CurrentMap;
   var sword=map.listerThings.AllThings.FirstOrDefault(t=>t.ThingID==savedSwordId);
   Check(sword!=null && S.IsMasterwork(sword),"masterwork mark survives save/load");
   var meat=map.listerThings.AllThings.FirstOrDefault(t=>t.ThingID==savedMeatId);
   Check(meat!=null && S.MeatSource(meat)=="FireGiant","fire giant meat source survives save/load");
   sword.Destroy();meat.Destroy();
   return checks;
  }
  static Pawn Make(string xenotype,Faction faction=null,IntVec3? at=null){
   Pawn p=null;
   for(int attempt=0;attempt<20;attempt++){
    p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,faction??Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:DefDatabase<XenotypeDef>.GetNamed(xenotype),mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25,fixedChronologicalAge:25));
    if(p.GetStatValue(StatDefOf.PsychicSensitivity)>0)break;
   }
   foreach(var injury in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(injury);
   var cell=at??CellFinder.RandomClosewalkCellNear(center,map,4,c=>c.GetFirstPawn(map)==null);
   GenSpawn.Spawn(p,cell,map);made.Add(p);
   if(p.needs?.food!=null)p.needs.food.CurLevelPercentage=.9f;
   return p;
  }
  static Faction Hostile(){var f=Find.FactionManager.RandomEnemyFaction(false,false,false);if(f==null)throw new Exception("[mythic bridge] no humanlike enemy faction for fixtures");return f;}
  static void Remove(params Pawn[] pawns){foreach(var p in pawns)if(!p.Destroyed)p.Destroy();}
  static Thing Item(string def,IntVec3 cell,int count=1){var t=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(def),DefDatabase<ThingDef>.GetNamed(def).MadeFromStuff?ThingDefOf.Steel:null);t.stackCount=count;GenSpawn.Spawn(t,cell,map);t.SetForbidden(false,false);return t;}

  static void Definitions(){
   var thoughts=DefDatabase<ThoughtDef>.AllDefsListForReading.Where(d=>d.defName.StartsWith("LPMB_Relation_")).ToList();
   Check(thoughts.Count==16 && thoughts.All(d=>!d.ConfigErrors().Any() && d.GetModExtension<MythicRelation>()!=null),"sixteen gene-based relation thoughts load cleanly");
   foreach(var name in new[]{"LPMB_TrollBloodRegen","LPMB_FireGiantFlesh","LPMB_DragonFlesh"}){var h=DefDatabase<HediffDef>.GetNamedSilentFail(name);Check(h!=null && !h.ConfigErrors().Any() && h.CompProps<HediffCompProperties_Disappears>()!=null,"visible short-term hediff loads: "+name);}
   var blood=MythicBridge.TrollBlood;Check(blood.IsIngestible && !blood.ConfigErrors().Any() && blood.ingestible.preferability==FoodPreferability.NeverForNutrition,"troll blood is drinkable only as a non-nutrition item");
   Check(S!=null,"bridge game component exists");
  }
  static ThoughtDef Thought(string a,string b)=>DefDatabase<ThoughtDef>.GetNamed("LPMB_Relation_"+a+b);
  static string Xeno(string race){
   switch(race){case "FireGiant":return "LP_FireGiant";case "ForceDragon":return "LP_ForceDragon";case "Elf":return "LP_Elf";case "MindFlayer":return "LP_MindFlayer";case "Troll":return "LP_Troll";case "Dwarf":return "LP_Dwarf";case "Lich":return "Baseliner";default:return "MB_"+race;}
  }
  static Pawn MakeRace(string race,Faction faction=null){var p=Make(Xeno(race),faction);if(race=="Lich")p.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);Check(MythicBridge.Is(p,race),"fixture is recognised by genes: "+race);return p;}
  static bool Active(Pawn p,ThoughtDef t){S.InvalidateCensus();return t.Worker.CurrentState(p).Active;}
  static int Count(string race){S.InvalidateCensus();return S.CensusOf(map).Count(race);}
  static void Relations(){
   var pairs=new[]{new object[]{"Suanni","FireGiant",2f,2f},new object[]{"Suanni","ForceDragon",2f,3f},new object[]{"Jumang","Elf",2f,2f},new object[]{"Jumang","FireGiant",-2f,-2f},new object[]{"Baize","MindFlayer",-3f,-3f},new object[]{"Baize","Lich",-2f,-2f},new object[]{"Taotie","Troll",2f,2f},new object[]{"Taotie","Dwarf",-1f,-1f}};
   foreach(var pair in pairs){
    string a=(string)pair[0],b=(string)pair[1];var pa=MakeRace(a);var pb=MakeRace(b);
    var ab=Thought(a,b);var ba=Thought(b,a);
    Check(Active(pa,ab) && Near(ab.stages[0].baseMoodEffect,(float)pair[2]),a+" sees "+b+": "+ab.stages[0].baseMoodEffect);
    Check(Active(pb,ba) && Near(ba.stages[0].baseMoodEffect,(float)pair[3]),b+" sees "+a+": "+ba.stages[0].baseMoodEffect);
    // 前面的测试可能在殖民地里留下同种族的人：按普查的变化判断。
    int with=Count(b);Remove(pb);int without=Count(b);
    Check(without==with-1 && (without>0 || !Active(pa,ab)),a+" relation ends when no "+b+" is left on the map ("+without+" others)");
    Remove(pa);
   }
   var suanni=MakeRace("Suanni");int giants=Count("FireGiant");var foe=MakeRace("FireGiant",Hostile());
   Check(Count("FireGiant")==giants && Active(suanni,Thought("Suanni","FireGiant"))==(giants>0),"hostile visitors do not count for relations ("+foe.Faction+")");
   var elf=MakeRace("Elf");Check(!Active(suanni,Thought("Jumang","Elf")) && !Active(elf,Thought("Elf","Jumang")),"unrelated races get no relation thought");
   Remove(suanni,foe,elf);
   // 自定义异种型：只要有对应基因就算。
   var custom=Make("Baseliner");custom.genes.AddGene(DefDatabase<GeneDef>.GetNamed("MB_TaotieGene"),true);var troll=Make("Baseliner");troll.genes.AddGene(Troll.RegenerationGene,true);
   Check(Active(custom,Thought("Taotie","Troll")) && Active(troll,Thought("Troll","Taotie")),"relations follow genes, not xenotype names");
   Remove(custom,troll);
  }
  static void Rain(){
   var troll=MakeRace("Troll",null);troll.Position=center;troll.Notify_Teleported();var gene=Troll.Gene(troll);
   var jumang=MakeRace("Jumang");jumang.Position=center+new IntVec3(3,0,0);jumang.Notify_Teleported();
   var rain=(InnatePsycast)jumang.abilities.abilities.First(a=>a.def.defName=="MB_Rain");
   Action cast=()=>{rain.ResetCooldown();jumang.psychicEntropy.RemoveAllEntropy();jumang.psychicEntropy.OffsetPsyfocusDirectly(1);Check(rain.Activate(troll.Position,troll.Position) && !troll.IsBurning(),"rain extinguishes the burning troll");};
   int now=Find.TickManager.TicksGame;
   FireUtility.TryAttachFire(troll,.5f,null);gene.Suppress();Check(troll.IsBurning() && !gene.CanRegenerate,"fire suppresses troll regeneration");
   cast();Check(gene.suppressedUntilTick<=now && gene.CanRegenerate,"rain clears fire suppression immediately");
   S.RecordAcid(troll,now+Gene_TrollRegeneration.FireSuppressionTicks);FireUtility.TryAttachFire(troll,.5f,null);gene.Suppress();
   cast();Check(!gene.CanRegenerate && gene.suppressedUntilTick==S.AcidUntil(troll),"rain keeps acid suppression until it ends");
   var acid=DefDatabase<DamageDef>.AllDefsListForReading.FirstOrDefault(d=>d.harmsHealth && MythicBridge.IsAcid(d));
   if(acid!=null){var other=MakeRace("Troll");other.TakeDamage(new DamageInfo(acid,2));Check(S.AcidUntil(other)>now,"real acid damage is recorded for trolls: "+acid.defName);Remove(other);}
   var human=Make("Baseliner");human.Position=center+new IntVec3(-1,0,0);human.Notify_Teleported();FireUtility.TryAttachFire(human,.5f,null);gene.suppressedUntilTick=0;
   cast();Check(!human.IsBurning(),"rain on non-trolls still only puts out fire");
   Remove(troll,jumang,human);
  }
  static Hediff Has(Pawn p,string name)=>p.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed(name));
  static int Ticks(Hediff h)=>((HediffWithComps)h).TryGetComp<HediffComp_Disappears>().ticksToDisappear;
  static void Eating(){
   var taotie=MakeRace("Taotie");taotie.Position=center;taotie.Notify_Teleported();var tg=taotie.genes.GenesListForReading.OfType<Gene_Taotie>().Single();
   var plain=Make("Baseliner");
   // 巨魔血：只有饕餮会喝；用真实的“饕餮进食”工作。
   var blood=Item("LP_TrollBlood",center+IntVec3.East,3);
   Check(tg.FoodAllowed(blood) && FoodUtility.WillEat(taotie,blood) && !FoodUtility.WillEat(plain,blood),"only Taotie treats troll blood as edible");
   taotie.jobs.StartJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("MB_Eat"),blood),JobCondition.InterruptForced);
   for(int i=0;i<600 && Has(taotie,"LPMB_TrollBloodRegen")==null;i++)Find.TickManager.DoSingleTick();
   var regen=Has(taotie,"LPMB_TrollBloodRegen");Check(regen!=null && Ticks(regen)>29000 && blood.stackCount==2,"Taotie eating job on troll blood gives twelve hours of lesser regeneration");
   ((HediffWithComps)regen).TryGetComp<HediffComp_Disappears>().ticksToDisappear=100;
   blood.Ingested(taotie,.05f);Check(Ticks(regen)==30000 && taotie.health.hediffSet.hediffs.Count(h=>h.def==regen.def)==1,"drinking again refreshes without stacking");
   var cut=(Hediff_Injury)taotie.health.AddHediff(HediffDefOf.Cut,taotie.RaceProps.body.corePart);cut.Severity=10;
   ((Hediff_LesserTrollRegen)regen).Heal(250);Check(Near(cut.Severity,10-.5f*.25f),"lesser regeneration heals at one quarter of troll speed");
   var finger=taotie.RaceProps.body.AllParts.First(b=>b.def.defName=="Finger");taotie.health.AddHediff(HediffDefOf.MissingBodyPart,finger);
   for(int i=0;i<40;i++)((Hediff_LesserTrollRegen)regen).Heal(250);
   Check(taotie.health.hediffSet.PartIsMissing(finger) && Troll.Gene(taotie)==null,"lesser regeneration never regrows parts and grants no troll revival");
   // 肉：屠宰火巨人/力能龙得到的肉带来源标记。
   foreach(var pair in new[]{new[]{"FireGiant","LPMB_FireGiantFlesh"},new[]{"ForceDragon","LPMB_DragonFlesh"}}){
    var donor=MakeRace(pair[0],null);donor.Kill(null);var corpse=donor.Corpse;
    var meat=corpse.ButcherProducts(taotie,1f).First(t=>t.def==donor.RaceProps.meatDef);
    Check(S.MeatSource(meat)==pair[0] && meat.def==ThingDefOf.Human.race.meatDef,"butchering a "+pair[0]+" marks its human meat");
    var normal=ThingMaker.MakeThing(meat.def);meat.stackCount=10;var piece=meat.SplitOff(1);
    Check(!meat.CanStackWith(normal) && S.MeatSource(piece)==pair[0] && meat.CanStackWith(piece),"marked meat keeps its source when split and never merges with ordinary meat");
    GenSpawn.Spawn(meat,center+IntVec3.North,map);meat.SetForbidden(false,false);Check(tg.FoodAllowed(meat),"Taotie may eat "+pair[0]+" meat");
    meat.Ingested(taotie,.1f);var h=Has(taotie,pair[1]);Check(h!=null && Ticks(h)>59000,pair[0]+" meat gives a visible 24-hour hediff");
    if(pair[0]=="FireGiant" && savedMeatId==null){var keep=meat.SplitOff(1);GenSpawn.Spawn(keep,center+IntVec3.West*5,map);savedMeatId=keep.ThingID;}
    if(!meat.Destroyed)meat.Destroy();if(!corpse.Destroyed)corpse.Destroy();
   }
   var flame=new DamageInfo(DamageDefOf.Flame,10);taotie.PreApplyDamage(ref flame,out _);
   var force=new DamageInfo(DefDatabase<DamageDef>.GetNamed("LP_Force"),10);taotie.PreApplyDamage(ref force,out _);
   var cutDamage=new DamageInfo(DamageDefOf.Cut,10);taotie.PreApplyDamage(ref cutDamage,out _);
   Check(Near(flame.Amount,5) && Near(force.Amount,5) && Near(cutDamage.Amount,10),"fire and force damage halve, other damage unchanged ("+flame.Amount+"/"+force.Amount+"/"+cutDamage.Amount+")");
   var other=MakeRace("Taotie");var human=Item("Meat_Human",center+IntVec3.South,5);human.Ingested(other,.1f);
   Check(Has(other,"LPMB_FireGiantFlesh")==null && Has(other,"LPMB_DragonFlesh")==null && Has(other,"LPMB_TrollBloodRegen")==null,"ordinary human meat gives no bridge effect");
   foreach(var name in new[]{"LP_BrainJar","LP_Tadpole"}){var t=Item(name,center+IntVec3.South*2);Check(!tg.FoodAllowed(t),"Taotie cannot eat "+name);t.Destroy();}
   if(!human.Destroyed)human.Destroy();if(!blood.Destroyed)blood.Destroy();
   Remove(taotie,plain,other);
  }
  static void Hoard(){
   var dragon=MakeRace("ForceDragon");var far=map.AllCells.Where(c=>c.InBounds(map) && c.DistanceTo(center)>25 && c.DistanceTo(center)<45).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
   DragonHoard.BuildHoard(map,far,dragon,out var interior);var room=DragonHoard.RoomOf(dragon);
   Check(room!=null && DragonHoard.Valid(room),"fixture hoard room is valid");
   var suanni=MakeRace("Suanni");suanni.Position=center;suanni.Notify_Teleported();
   var sword=Item("MeleeWeapon_LongSword",center+IntVec3.East);sword.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal,ArtGenerationContext.Colony);
   var improve=(InnatePsycast)suanni.abilities.abilities.First(a=>a.def.defName=="MB_ImproveQuality");suanni.psychicEntropy.RemoveAllEntropy();suanni.psychicEntropy.OffsetPsyfocusDirectly(1);
   Check(improve.Activate(sword,sword) && sword.TryGetComp<CompQuality>().Quality==QualityCategory.Good && S.IsMasterwork(sword),"Masterwork success marks the item");
   var copy=Item("MeleeWeapon_LongSword",center+IntVec3.West);copy.TryGetComp<CompQuality>().SetQuality(QualityCategory.Good,ArtGenerationContext.Colony);copy.HitPoints=sword.HitPoints;
   Check(!S.IsMasterwork(copy),"items not touched by Masterwork stay unmarked");
   var free=DragonHoard.FreeCells(interior,map).ToList();
   float v0=DragonHoard.Value(room);sword.DeSpawn();GenSpawn.Spawn(sword,free[0],map);float v1=DragonHoard.Value(room);copy.DeSpawn();GenSpawn.Spawn(copy,free[1],map);float v2=DragonHoard.Value(room);
   Check(Near(v1-v0,1.1f*DragonHoard.ItemValue(sword)),"marked item counts 110% in the hoard ("+(v1-v0)+" vs "+DragonHoard.ItemValue(sword)+")");
   Check(Near(v2-v1,DragonHoard.ItemValue(copy)),"unmarked item counts 100% in the hoard");
   sword.DeSpawn();GenSpawn.Spawn(sword,center+IntVec3.East*2,map);Check(Near(DragonHoard.Value(room),v2-1.1f*DragonHoard.ItemValue(sword)),"marked item outside the hoard adds nothing");
   savedSwordId=sword.ThingID;copy.Destroy();
   Remove(dragon,suanni);
  }
  static void Reveal(){
   var baize=MakeRace("Baize");baize.Position=center;baize.Notify_Teleported();
   var calm=(BaizePsycast)baize.abilities.abilities.Single(a=>a.def.defName=="MB_BaizeCalm");
   Action ready=()=>{calm.ResetCooldown();baize.psychicEntropy.RemoveAllEntropy();};
   var calmTarget=Make("Baseliner");calmTarget.Position=center+new IntVec3(2,0,0);calmTarget.Notify_Teleported();
   ready();Check(!calm.Activate(calmTarget,calmTarget) && !calm.OnCooldown,"Reveal still rejects a pawn under no control");
   var hive=Hostile();
   var thrall=Make("Baseliner",hive,center+new IntVec3(0,0,3));FactionGroups.Thrall(thrall);Check(FactionGroups.IsThralled(thrall) && !thrall.InMentalState,"thrall fixture is controlled without a mental state");
   ready();Check(calm.Activate(thrall,thrall) && !FactionGroups.IsThralled(thrall) && thrall.Faction==null && calm.OnCooldown,"Reveal frees an illithid thrall and is charged normally");
   var victim=Make("Baseliner",hive,center+new IntVec3(-2,0,0));var friend=Make("Baseliner",hive,center+new IntVec3(-3,0,0));
   victim.health.AddHediff(MindFlayerPowers.DominatedHediff);bool started=victim.mindState.mentalStateHandler.TryStartMentalState(MindFlayerPowers.Dominated,"测试支配",true,true,false,null,false,false,false);
   Check(started && victim.MentalStateDef==MindFlayerPowers.Dominated,"Dominate Person is a real mental state");
   ready();Check(calm.Activate(victim,victim) && !victim.InMentalState,"Reveal ends Dominate Person through the native mental-state path");
   Check(victim.health.hediffSet.HasHediff(MindFlayerPowers.DominatedHediff),"leftover domination hediff remains after the mental state ends");
   ready();Check(calm.Activate(victim,victim) && !victim.health.hediffSet.HasHediff(MindFlayerPowers.DominatedHediff),"Reveal removes the leftover domination through its native ending");
   Remove(baize,calmTarget,thrall,victim,friend);
  }
  // 1.22.0：饕餮喝治疗药水效果翻倍；句芒在炼金桌做药水草药减半。
  static void Alchemy(){
   var taotie=MakeRace("Taotie");var plain=Make("Baseliner");var healing=DefDatabase<ThingDef>.GetNamed("LP_PotionHealing");var giant=DefDatabase<ThingDef>.GetNamed("LP_PotionGiantStrength");
   Check(Near(Items.PotionPotency(taotie,healing),2f) && Near(Items.PotionPotency(taotie,DefDatabase<ThingDef>.GetNamed("LP_PotionGreaterHealing")),2f) && Near(Items.PotionPotency(taotie,giant),1f) && Near(Items.PotionPotency(plain,healing),1f),"Taotie gets double effect from healing potions only");
   foreach(var p in new[]{taotie,plain}){var t=ThingMaker.MakeThing(healing);GenSpawn.Spawn(t,p.Position,map);t.Ingested(p,0f);if(!t.Destroyed)t.Destroy();}
   var hd=DefDatabase<HediffDef>.GetNamed("LP_PotionHealing");
   Check(Near(taotie.health.hediffSet.GetFirstHediffOfDef(hd).Severity,2f) && Near(plain.health.hediffSet.GetFirstHediffOfDef(hd).Severity,1f) && taotie.health.hediffSet.GetFirstHediffOfDef(hd).CurStage.statFactors.GetStatFactorFromList(StatDefOf.InjuryHealingFactor)==6f,"a Taotie drinking a healing potion heals x6 instead of x3");
   var jumang=MakeRace("Jumang");var recipe=DefDatabase<RecipeDef>.AllDefsListForReading.First(r=>r.ProducedThingDef==healing && r.AllRecipeUsers.Any(u=>u.defName=="LP_AlchemyTable"));
   var herbs=ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);herbs.stackCount=2;var blood=ThingMaker.MakeThing(MythicBridge.TrollBlood);var used=new List<Thing>{blood,herbs};
   Check(MythicBridge.HerbRefund(recipe,jumang,used,.9f)==1 && MythicBridge.HerbRefund(recipe,plain,used,.1f)==0,"Jumang gets half the herbal medicine back on alchemy potions");
   var odd=ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);odd.stackCount=3;Check(MythicBridge.HerbRefund(recipe,jumang,new List<Thing>{odd},.4f)==2 && MythicBridge.HerbRefund(recipe,jumang,new List<Thing>{odd},.6f)==1,"an odd herb is refunded half the time");
   var products=GenRecipe.MakeRecipeProducts(recipe,jumang,used,blood,null,null,null,null).ToList();
   Check(products.Any(t=>t.def==healing) && products.Where(t=>t.def==ThingDefOf.MedicineHerbal).Sum(t=>t.stackCount)==1,"real Jumang alchemy returns one herbal medicine with the potion");
   var plainProducts=GenRecipe.MakeRecipeProducts(recipe,plain,used,blood,null,null,null,null).ToList();Check(!plainProducts.Any(t=>t.def==ThingDefOf.MedicineHerbal),"others get no herbs back");
   foreach(var t in products.Concat(plainProducts).Concat(new[]{herbs,blood,odd}))if(!t.Destroyed)t.Destroy();
   var baize=MakeRace("Baize");Check(ItemMaterials.ColdIronBane(baize) && !ItemMaterials.ColdIronBane(plain) && !ItemMaterials.ColdIronBane(taotie),"cold iron also bites Baize (but not other bloodlines)");
   Remove(taotie,plain,jumang,baize);
  }
 }
}
