using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.100.0：Test.ps1 -Only ManaLake（魔力湖泊与职业水晶）：派系与开关、48 枚结晶与能力、秘传槽、学习门槛与吸收、货单的好感门槛、
 // 公共冷却、伤害挂钩（斩铁式 / 万军之主 / 护盾 / 元素化身）、一批主动效果、击杀与陷阱被动、读档后保留。
 public partial class LichChecks {
  static readonly List<Thing> mlMade=new List<Thing>();
  static string mlSavedId;
  IntVec3 mlBase=IntVec3.Invalid;
  IntVec3 MlCell(int dx,int dz){
   if(!mlBase.IsValid){
    // 找一块尽量空的地方，再把 27×27 清干净（去雾、换成土、拆掉非玩家的建筑 / 植物 / 杂物），地图随机也不会找不到。
    mlBase=map.AllCells.Where(c=>c.x>25 && c.z>25 && c.x<map.Size.x-25 && c.z<map.Size.z-25 && c.Standable(map)).OrderBy(c=>CellRect.CenteredOn(c,4).Cells.Count(x=>!x.Standable(map) || x.Fogged(map))*1000+c.DistanceToSquared(map.Center)/100).First();
    foreach(var c in CellRect.CenteredOn(mlBase,13).Cells){
     if(!c.InBounds(map))continue;
     foreach(var th in c.GetThingList(map).ToList())if(!(th is Pawn) && th.def.destroyable && !th.Destroyed && (th.Faction==null || !th.Faction.IsPlayer) && (th.def.category==ThingCategory.Building || th.def.category==ThingCategory.Plant || th.def.category==ThingCategory.Item || th.def.category==ThingCategory.Filth))th.Destroy();
     if(!c.Standable(map) || c.GetTerrain(map).IsWater)map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil);
     if(c.Fogged(map))map.fogGrid.Unfog(c);
    }
   }
   return mlBase+new IntVec3(dx,0,dz);
  }
  Faction MlEnemy=>Find.FactionManager.AllFactionsListForReading.First(f=>!f.IsPlayer && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && !f.temporary);
  Pawn MlPawn(Faction f,int dx,int dz){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,f,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:25f,fixedChronologicalAge:25f));
   p.apparel?.DestroyAll();foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(inj);
   GenSpawn.Spawn(p,MlCell(dx,dz),map);mlMade.Add(p);return p;
  }
  Pawn MlClassPawn(LP_ClassDef c,int level,int classLevel,int dx,int dz){
   var p=ClsPawn(level,10,10,10);mlMade.Add(p);ClassSystem.SetLevel(Adventurer.Get(p),c,classLevel);
   foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(inj);
   GenSpawn.Spawn(p,MlCell(dx,dz),map);return p;
  }
  static LP_ClassDef MlClass(string n)=>DefDatabase<LP_ClassDef>.GetNamed("LP_Class_"+n);
  static AbilityDef MlAb(string key)=>DefDatabase<AbilityDef>.GetNamed("LP_AC_"+key);
  static ThingDef MlCrystal(string key)=>DefDatabase<ThingDef>.GetNamed("LP_Crystal_"+key);
  static void MlClean(){foreach(var t in mlMade)if(!t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}mlMade.Clear();}
  bool MlForce(Pawn p,string key){var h=Adventurer.Get(p);var d=MlAb(key);if(!h.classes.arcana.Contains(d))h.classes.arcana.Add(d);LakeArcana.Touched=true;if(p.abilities.GetAbility(d)==null)p.abilities.GainAbility(d);return true;}

  void CheckManaLake(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool on=s.adventurer,toggle=s.factionManaLake;s.adventurer=true;
   Rand.PushState(110001);
   try{CheckManaLakeInner();}
   finally{Rand.PopState();Adventurer.TestSuppressed=sup;s.adventurer=on;s.factionManaLake=toggle;Factions.ApplyToggles();StockGenerator_Arcana.TestGoodwill=null;ArcanaKill.TestRoll=null;MlClean();}
  }
  void CheckManaLakeInner(){
   // 1. 派系：中世纪、中立（0）、开关 manalake、和塞伊远征军 / 猎巫狂热分子敌对；商队与据点；图标；设置分组数不变。
   var def=ManaLake.Def;var fac=ManaLake.Faction;var ext=Factions.Ext(def);
   bool defs=def!=null && fac!=null && def.techLevel==TechLevel.Medieval && !def.permanentEnemy && ext?.toggle=="manalake" && ext.initialGoodwill==0 && Factions.Toggles.Contains("manalake")
    && ext.enemyFactions.Select(d=>d.defName).OrderBy(x=>x).SequenceEqual(new[]{"LP_FaerunHostile","LP_Zealots"}) && def.caravanTraderKinds.Single().defName==ManaLake.Caravan && def.baseTraderKinds.Single().defName==ManaLake.Base
    && ContentFinder<Texture2D>.Get(def.factionIconPath,false)!=null && def.leaderTitle=="湖主" && LichMod.SettingsLeft.Length+LichMod.SettingsRight.Length==20; // 后来的版本多了一组设置（与魔力湖泊无关）
   var hostile=Factions.Hostile;bool enemies=hostile==null || fac.RelationKindWith(hostile)==FactionRelationKind.Hostile;
   Check(defs && enemies,"mana lake faction loads: medieval, neutral (0), toggle manalake, enemies of the Thayan expedition and the zealots, caravan + lakeside market, icon; still 20 settings groups ("+defs+" "+enemies+" "+(fac==null?"nofac":fac.RelationKindWith(hostile).ToString())+" "+(LichMod.SettingsLeft.Length+LichMod.SettingsRight.Length)+")");

   // 2. 48 枚职业水晶：12 个职业各 4 枚、6 / 7 / 8 / 9 环各 1；能力有图标、Ability_Class；结晶价格按环。
   var all=LakeArcana.All;var crystals=LakeArcana.Crystals;
   bool perClass=ClassSystem.All.All(c=>LakeArcana.OfClass(c).Select(a=>LakeArcana.Ext(a).ring).SequenceEqual(new[]{6,7,8,9}));
   bool icons=all.All(a=>a.uiIcon!=null && a.uiIcon!=BaseContent.BadTex && a.abilityClass==typeof(Ability_Class) && a.comps.OfType<CompProperties_ClassEffect>().Any());
   bool prices=crystals.All(c=>Mathf.Abs(c.BaseMarketValue-LakeArcana.PriceFor(LakeArcana.Ext(LakeArcana.AbilityOf(c)).ring))<1f && c.label.StartsWith("职业水晶") && c.graphicData.color!=Color.white);
   Check(all.Count==52 && crystals.Count==52 && ClassSystem.All.Count==13 && perClass && icons && prices && all.All(a=>LakeArcana.CrystalFor(a)!=null),"52 class crystals: 13 classes × rings 6/7/8/9, abilities with icons, crystals priced 8000/12000/16000/25000 and tinted by class colour");

   // 3. 秘传槽：冒险者 10 级 0 个，11 / 15 / 20 级 1 / 2 / 3 个，传奇 30 级 4 个。
   var ha=new Hediff_Adventurer();int[] slots=new[]{10,11,15,20,30}.Select(l=>{ha.level=l;return LakeArcana.Slots(ha);}).ToArray();
   Check(slots.SequenceEqual(new[]{0,1,2,3,4}),"arcana slots at adventurer 10/11/15/20/30 = "+string.Join("/",slots));

   // 4. 学习门槛：战士 10 级不行；战士 11 级能学 6 环、不能学 7 环（13 级）；法师不能学战士水晶；学会后能力在身上、槽位占满；用结晶会消耗一枚。
   var fighter=MlClassPawn(ClassSystem.Fighter,11,11,0,0);var low=MlClassPawn(ClassSystem.Fighter,11,10,1,0);var wiz=MlClassPawn(MlClass("Wizard"),20,20,2,0);
   string wLow=LakeArcana.CannotLearn(low,MlAb("SteelCut")),wSeven=LakeArcana.CannotLearn(fighter,MlAb("Immovable")),wWiz=LakeArcana.CannotLearn(wiz,MlAb("SteelCut"));
   var cr=ThingMaker.MakeThing(MlCrystal("SteelCut"));cr.stackCount=2;GenSpawn.Spawn(cr,MlCell(0,1),map);mlMade.Add(cr);
   var use=cr.TryGetComp<CompUseEffect_Arcanum>();bool can=use.CanBeUsedBy(fighter).Accepted;use.DoEffect(fighter);
   bool learnt=LakeArcana.Knows(fighter,MlAb("SteelCut")) && fighter.abilities.GetAbility(MlAb("SteelCut"))!=null && cr.stackCount==1 && LakeArcana.Used(Adventurer.Get(fighter))==1;
   ClassSystem.SetLevel(Adventurer.Get(fighter),ClassSystem.Fighter,13);string wFull=LakeArcana.CannotLearn(fighter,MlAb("Immovable"));
   Check(wLow!=null && wLow.Contains("11") && wSeven!=null && wSeven.Contains("13") && wWiz!=null && wWiz.Contains("战士") && can && learnt && wFull!=null && wFull.Contains("已满"),"crystal gates: class level 11/13 by ring, only that class, absorbing uses one crystal; a full slot blocks the next ("+wFull+")");

   // 5. 货单：好感 < 75 不出结晶；盟友时商队出 6～7 环、湖畔集市另有 8～9 环；都会收购。
   var caravan=DefDatabase<TraderKindDef>.GetNamed(ManaLake.Caravan);var bas=DefDatabase<TraderKindDef>.GetNamed(ManaLake.Base);
   StockGenerator_Arcana.TestGoodwill=50;var none=bas.stockGenerators.OfType<StockGenerator_Arcana>().SelectMany(g=>g.GenerateThings(map.Tile,fac)).ToList();
   StockGenerator_Arcana.TestGoodwill=80;var rings=new List<int>();
   for(int i=0;i<20;i++)foreach(var g in bas.stockGenerators.OfType<StockGenerator_Arcana>())foreach(var t in g.GenerateThings(map.Tile,fac)){rings.Add(LakeArcana.Ext(LakeArcana.AbilityOf(t.def)).ring);t.Destroy();}
   var cRings=new List<int>();for(int i=0;i<30;i++)foreach(var g in caravan.stockGenerators.OfType<StockGenerator_Arcana>())foreach(var t in g.GenerateThings(map.Tile,fac)){cRings.Add(LakeArcana.Ext(LakeArcana.AbilityOf(t.def)).ring);t.Destroy();}
   StockGenerator_Arcana.TestGoodwill=null;
   Check(none.Count==0 && (rings.Contains(6) || rings.Contains(7)) && rings.Any(r=>r>=8) && cRings.Count>0 && cRings.All(r=>r<=7) && bas.WillTrade(MlCrystal("SteelCut")) && caravan.WillTrade(MlCrystal("SoulPact")),"stock: no crystals under goodwill 75; allied market sells rings 6–9 ("+string.Join(",",rings.Distinct().OrderBy(x=>x))+"), caravans only 6–7");

   // 6. 公共冷却：放过一个秘传后 1 小时内别的秘传不能放；冷却期间职业能力照常。
   MlForce(fighter,"Immovable");var steel=fighter.abilities.GetAbility(MlAb("SteelCut"));bool cast=steel.Activate(new LocalTargetInfo(fighter),new LocalTargetInfo(fighter));
   string cd=LakeArcana.CannotCast(fighter,MlAb("Immovable"));bool buff=Items.Has(fighter,"LP_ACB_SteelCut");
   Adventurer.Get(fighter).classes.arcanaLast-=LakeArcana.PublicCooldown+1;string after=LakeArcana.CannotCast(fighter,MlAb("Immovable"));
   Check(cast && buff && cd!=null && cd.Contains("公共冷却") && after==null,"casting an arcanum starts a 1-hour public cooldown for the others");

   // 7. 伤害挂钩：斩铁式无视护甲、万军之主 ×1.15、护盾吸收、元素化身免疫火焰。
   var enemy=MlPawn(MlEnemy,4,0);
   var d1=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,fighter);enemy.PreApplyDamage(ref d1,out bool ab1);
   Items.Give(fighter,"LP_ACB_Warlord",600);Items.Remove(fighter,"LP_ACB_SteelCut");var d2=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,fighter);enemy.PreApplyDamage(ref d2,out bool ab2);Items.Remove(fighter,"LP_ACB_Warlord");
   Items.Give(enemy,"LP_ACB_Shield",600,15f);var d3=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,fighter);enemy.PreApplyDamage(ref d3,out bool ab3);var d4=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,fighter);enemy.PreApplyDamage(ref d4,out bool ab4);
   Items.Give(enemy,"LP_ACB_ElementalForm",600);var d5=new DamageInfo(DamageDefOf.Flame,10f,0f,-1f,fighter);enemy.PreApplyDamage(ref d5,out bool ab5);Items.Remove(enemy,"LP_ACB_ElementalForm");
   Check(d1.IgnoreArmor && !ab1 && Near(d2.Amount,11.5f) && !d2.IgnoreArmor && ab3 && !ab4 && Near(d4.Amount,5f) && ab5,"damage hooks: steel cut ignores armor, warlord ×1.15, the shield absorbs 15 then lets the rest through, elemental form absorbs fire");

   // 8. 主动：攀云步落地击晕、神之圣言（低血量当场死、其余失明）、真实变形术、源能爆发耗尽法力。
   var monk=MlClassPawn(ClassSystem.Monk,20,20,-4,0);var e2=MlPawn(MlEnemy,-4,6);
   int hit=ArcanaEffects.CloudStep(monk,MlCell(-4,5),15f,2f);bool moved=monk.Position==MlCell(-4,5) && e2.stances.stunner.Stunned;
   var cleric=MlClassPawn(MlClass("Cleric"),20,20,0,-6);var strong=MlPawn(MlEnemy,-2,-6);
   bool weakLow=ArcanaEffects.WordOutcome(.1f,false)==2 && ArcanaEffects.WordOutcome(.4f,false)==1 && ArcanaEffects.WordOutcome(.9f,false)==0 && ArcanaEffects.WordOutcome(.1f,true)==0;
   ArcanaEffects.DivineWord(cleric,6f,600);
   bool word=strong.Dead==false && Items.Has(strong,"LP_ACB_Blinded");
   var wizard=wiz;
   var poly=MlPawn(MlEnemy,6,8);var animal=ArcanaEffects.Polymorph(wizard,poly);if(animal!=null)mlMade.Add(animal);
   var sorc=MlClassPawn(MlClass("Sorcerer"),20,20,-6,6);var hs=Adventurer.Get(sorc);ClassSystem.AddResource(hs,MlClass("Sorcerer"),99f);float mana0=ClassSystem.Resource(hs,MlClass("Sorcerer"));
   var e3=MlPawn(MlEnemy,-6,9);int burst=ArcanaEffects.SourceBurst(sorc,e3.Position,15f,5f);
   Check(hit>=1 && moved && weakLow && word && poly.Destroyed && animal!=null && animal.Faction==Faction.OfPlayer && animal.RaceProps.Animal && mana0>=1f && ClassSystem.Resource(hs,MlClass("Sorcerer"))<1f && burst>=1,
    "cloud step lands and stuns, divine word blinds the healthy and drops the weak, true polymorph makes a player animal, source burst spends all sorcerer mana ("+hit+" "+moved+" "+weakLow+" "+word+" "+poly.Destroyed+" "+(animal?.Faction?.Name)+" "+mana0+"→"+ClassSystem.Resource(hs,MlClass("Sorcerer"))+" "+burst+")");

   // 9. 真实复活（死亡 10 天、缺腿）、拟像术（技能相同）、力场发生器、完美复刻（扣一半市价的白银）、天使降临。
   var dead=MlPawn(Faction.OfPlayer,8,-4);var leg=dead.health.hediffSet.GetNotMissingParts().First(x=>x.def.defName=="Leg");dead.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,dead,leg));
   dead.Kill(null);var corpse=dead.Corpse;corpse.Age=600000;string resWhy=ArcanaEffects.TrueResCannot(corpse);bool res=ArcanaEffects.TrueResurrect(cleric,corpse);
   bool whole=!dead.Dead && !dead.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().Any();
   fighter.skills.GetSkill(SkillDefOf.Cooking).Level=13;var sim=ArcanaEffects.Simulacrum(wizard,fighter,180000);if(sim!=null)mlMade.Add(sim);
   bool simOk=sim!=null && sim.skills.GetSkill(SkillDefOf.Cooking).Level==13 && Items.Has(sim,"LP_ACB_Simulacrum") && Items.Has(sim,"LP_Conjured") && sim.Faction==Faction.OfPlayer;
   var ff=ArcanaEffects.ForceField(wizard,MlCell(9,-8));if(ff!=null)mlMade.Add(ff);
   var item=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_RingOfRegeneration",false)??DefDatabase<ThingDef>.AllDefsListForReading.First(x=>x.defName.StartsWith("LP_") && x.IsApparel && x.stackLimit==1 && x.tradeability!=Tradeability.None && !x.MadeFromStuff));
   GenSpawn.Spawn(item,MlCell(-8,-8),map);mlMade.Add(item);for(int left=Mathf.CeilToInt(item.MarketValue),i=0;left>0;i++){var silver=ThingMaker.MakeThing(ThingDefOf.Silver);silver.stackCount=Math.Min(left,ThingDefOf.Silver.stackLimit);left-=silver.stackCount;GenSpawn.Spawn(silver,MlCell(-8+i,-7),map);mlMade.Add(silver);}
   int silver0=ArcanaEffects.SilverOn(map);var copy=ArcanaEffects.Replicate(wizard,item);if(copy!=null)mlMade.Add(copy);int silver1=ArcanaEffects.SilverOn(map);
   var angel=ArcanaEffects.Angel(cleric,60000);if(angel!=null)mlMade.Add(angel);
   Check(resWhy==null && res && whole && simOk && ff!=null && ff.Spawned && ff.TryGetComp<CompProjectileInterceptor>()!=null && copy!=null && copy.def==item.def && silver0-silver1==ArcanaEffects.ReplicateCost(item)
    && angel!=null && angel.genes?.Xenotype?.defName==PactRaces.Deva && Items.Has(angel,"LP_ACB_Angelic") && Items.Has(angel,"LP_Conjured"),
    "true resurrection revives a 10-day corpse with its leg back; simulacrum copies skills; force field spawns; replicate costs half the market value; angel descent summons a deva");

   // 10. 被动：咒法精通让法师 1 级法术免费；超魔·追踪 ×1.25；陷阱大师不触发陷阱；灵魂契约把击杀的敌人拉成仆从；无影击杀后隐形。
   var cheap=ClassSystem.Fighter.abilities.Count==0?null:MlClass("Wizard").abilities.FirstOrDefault(e=>e.level<=1 && (e.ability.comps.OfType<CompProperties_ClassEffect>().FirstOrDefault()?.cost??0)>0)?.ability;
   int before=cheap==null?-1:ClassLegendary.Cost(wizard,cheap,cheap.comps.OfType<CompProperties_ClassEffect>().First());MlForce(wizard,"CantripMastery");int afterCost=cheap==null?-1:ClassLegendary.Cost(wizard,cheap,cheap.comps.OfType<CompProperties_ClassEffect>().First());
   var bolt=DefDatabase<AbilityDef>.GetNamed("LP_ChaosBolt").comps.OfType<CompProperties_ClassEffect>().First();float p0=ClassEffects.Power(sorc,bolt);MlForce(sorc,"SeekingSpell");float p1=ClassEffects.Power(sorc,bolt);
   var rogue=MlClassPawn(ClassMartial2.Rogue,20,20,10,4);MlForce(rogue,"TrapMaster");MlForce(rogue,"Shadowless");
   var spring=HarmonyLib.AccessTools.Method(typeof(Building_Trap),"CheckSpring");var trapDef=DefDatabase<ThingDef>.GetNamed("TrapSpike");var trap=(Building_Trap)ThingMaker.MakeThing(trapDef,ThingDefOf.WoodLog);trap.SetFaction(MlEnemy);GenSpawn.Spawn(trap,MlCell(10,6),map);mlMade.Add(trap);
   float hp0=ArcanaEffects.Health(rogue);for(int i=0;i<30;i++)spring.Invoke(trap,new object[]{rogue});bool noTrap=!trap.Destroyed && Mathf.Abs(ArcanaEffects.Health(rogue)-hp0)<.001f;
   var warlock=MlClassPawn(MlClass("Warlock"),20,20,-10,4);MlForce(warlock,"SoulPact");var victim=MlPawn(MlEnemy,-10,6);ArcanaKill.TestRoll=0f;victim.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,warlock));ArcanaKill.TestRoll=null;
   bool thrall=!victim.Dead && victim.Faction==Faction.OfPlayer && Items.Has(victim,"LP_Conjured");
   var v2=MlPawn(MlEnemy,10,8);v2.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,rogue));bool invis=rogue.health.hediffSet.hediffs.Any(x=>x.def.defName=="PsychicInvisibility");
   Check(cheap!=null && before>0 && afterCost==0 && Near(p1,p0*1.25f) && noTrap && thrall && invis,"passives: cantrip mastery frees level-1 wizard spells ("+cheap?.label+" "+before+"→"+afterCost+"), seeking spell ×1.25, trap master never springs traps, soul pact raises a thrall, shadowless hides after a kill ("+p0+"→"+p1+" "+noTrap+" "+thrall+"/"+victim.Dead+"/"+victim.Faction?.Name+" "+invis+")");

   // 11. 分页：职业行里的秘传行能画（不抛异常），存档里保留学会的秘传。
   string line=LakeArcana.SlotLine(Adventurer.Get(fighter));
   Check(line=="秘传槽 2 / 1" && LakeArcana.OfClass(ClassSystem.Fighter).Count()==4,"adventurer tab slot line ("+line+"): two arcana known with one slot after a forced learn");
  }
  void PrepareManaLakeSave(){
   var p=ClsPawn(15,10,10,10);ClassSystem.SetLevel(Adventurer.Get(p),ClassSystem.Fighter,15);GenSpawn.Spawn(p,MlCell(0,10),map);
   LakeArcana.Learn(p,MlAb("SteelCut"));LakeArcana.Learn(p,MlAb("Immovable"));mlSavedId=p.ThingID;
  }
  void CheckManaLakeAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==mlSavedId);var h=Adventurer.Get(p);
   Check(p!=null && h?.classes?.arcana!=null && h.classes.arcana.Select(a=>a.defName).OrderBy(x=>x).SequenceEqual(new[]{"LP_AC_Immovable","LP_AC_SteelCut"}) && p.abilities.GetAbility(MlAb("Immovable"))!=null,"learned arcana survive save / load");
  }
 }
}
