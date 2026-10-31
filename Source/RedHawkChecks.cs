using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.64.0：Test.ps1 -Only RedHawk（红鹰商业联盟，规格第九节 12 项）：定义与开关、奴隶商队三次、奴隶状态、种族池解析、HAR 条目、
 // 材料商队、契约商队（宗主不同、存读档不变）、契约书照常阅读、契约开关、据点、飞空商船、派系开关。-NoHAR 时第 5 项改查池里没有 HAR 条目。
 public partial class LichChecks {
  static readonly Dictionary<int,string> rhSavedPatrons=new Dictionary<int,string>();
  static readonly List<Thing> rhMade=new List<Thing>();
  static bool RhHar=>ModsConfig.IsActive("erdelf.humanoidalienraces");
  static bool RhChinese(string s)=>!s.NullOrEmpty() && s.Any(c=>c>=0x4E00 && c<=0x9FFF);
  static TraderKindDef RhKind(string name)=>DefDatabase<TraderKindDef>.GetNamed(name);
  List<Thing> RhStock(TraderKindDef k,Func<StockGenerator,bool> which=null){
   var list=new List<Thing>();var f=RedHawk.Faction;
   foreach(var g in k.stockGenerators)if(which==null || which(g))list.AddRange(g.GenerateThings(map.Tile,f));
   foreach(var t in list)rhMade.Add(t);
   return list;
  }
  static void RhClean(){foreach(var t in rhMade)if(!t.Destroyed)t.Destroy();rhMade.Clear();}
  static StockGenerator_RHRaceSlaves RhSlaveGen(string kind)=>RhKind(kind).stockGenerators.OfType<StockGenerator_RHRaceSlaves>().Single();
  static string RhPatron(Thing t)=>t.TryGetComp<CompPactScroll>()?.patron?.defName;

  void CheckRedHawk(){
   var settings=LichMod.Settings;bool pact=settings.pactEnabled,abyss=settings.abyssEnabled,toggle=settings.factionRedHawk;
   Rand.PushState(16401);
   try{CheckRedHawkInner();}
   finally{Rand.PopState();settings.pactEnabled=pact;settings.abyssEnabled=abyss;settings.factionRedHawk=toggle;Factions.ApplyToggles();RhClean();}
  }
  void CheckRedHawkInner(){
   var def=RedHawk.Def;var fac=RedHawk.Faction;
   var kinds=new[]{RedHawk.SlaveCaravan,RedHawk.MaterialCaravan,RedHawk.PactCaravan,RedHawk.Base,RedHawk.Orbital}.Select(n=>DefDatabase<TraderKindDef>.GetNamedSilentFail(n)).ToList();
   var pawnKinds=new[]{"Merchant","Slaver","Appraiser","PactBroker","Mercenary","Crossbowman","Gladiator","Leader","SlaveStock"}.Select(n=>DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_RH_"+n)).ToList();
   var ext=Factions.Ext(def);
   // 1. 定义、标签、图标、开关、设置分组
   bool defs=def!=null && fac!=null && !def.hidden && def.techLevel==TechLevel.Medieval && !def.permanentEnemy && !def.naturalEnemy && ext?.toggle=="redhawk" && ext.initialGoodwill==10
    && Factions.Toggles.Contains("redhawk") && kinds.All(k=>k!=null && RhChinese(k.label)) && pawnKinds.All(k=>k!=null && RhChinese(k.label)) && RhChinese(def.label) && def.leaderTitle=="总商"
    && def.caravanTraderKinds.Select(k=>k.defName).SequenceEqual(new[]{RedHawk.SlaveCaravan,RedHawk.MaterialCaravan,RedHawk.PactCaravan}) && def.baseTraderKinds.Single().defName==RedHawk.Base
    && kinds[4].orbital && kinds[4].faction==def && kinds.Take(4).All(k=>!k.orbital) && Near(def.RaidCommonalityFromPoints(1000),.5f)
    && kinds.Take(3).All(k=>k.GetModExtension<TraderGroupExtension>()?.traderKind!=null) && ContentFinder<Texture2D>.Get(def.factionIconPath,false)!=null
    && LichMod.SettingsLeft.Length+LichMod.SettingsRight.Length==19;
   Check(defs,"red hawk league loads: medieval, neutral (+10, not a natural / permanent enemy), toggle redhawk, raids ×0.5, icon; slaver / materials / pact caravans, base 「红鹰商站」 and orbital 「红鹰飞空商船」 with Chinese labels; 9 pawn kinds; still 19 settings groups");

   // 2. 奴隶商队三次：10~14 名，同次内种族不同，≥3 个稀有档
   var gen=RhSlaveGen(RedHawk.SlaveCaravan);var rareKeys=new HashSet<string>(gen.Valid.Where(e=>e.Rare).Select(e=>e.Key));
   var all=new List<Pawn>();var runs=new List<string>();bool runsOk=true;
   for(int i=0;i<3;i++){
    var slaves=gen.GenerateThings(map.Tile,fac).OfType<Pawn>().ToList();rhMade.AddRange(slaves);all.AddRange(slaves);
    var keys=slaves.Select(RedHawk.RaceKey).ToList();int rare=keys.Count(rareKeys.Contains);
    runs.Add(slaves.Count+"/"+keys.Distinct().Count()+"/"+rare);
    if(slaves.Count<10 || slaves.Count>14 || keys.Distinct().Count()!=keys.Count || rare<3)runsOk=false;
   }
   Check(runsOk,"slaver caravan stock ×3: 10~14 slaves, every race different within one stock, at least 3 rare races (count/distinct/rare: "+string.Join(" ",runs)+")");

   // 3. 奴隶状态
   var bad=all.Where(p=>!p.RaceProps.Humanlike || p.genes?.Xenotype==null || p.guest?.joinStatus!=JoinStatus.JoinAsSlave || p.relations.DirectRelations.Count>0 || p.relations.RelatedPawns.Any() || p.DevelopmentalStage!=DevelopmentalStage.Adult || p.Dead).Select(p=>p.LabelShort+"("+RedHawk.RaceKey(p)+")").ToList();
   Check(bad.Count==0 && all.Count>=30,"all "+all.Count+" stock slaves are adult humanlikes with a xenotype, join as slaves when bought, and have no relatives "+string.Join(",",bad));
   // 3b. 商队里的库存奴隶是商品（交易窗口能买），红鹰护卫不是
   var notChattel=all.Where(p=>p.GetTraderCaravanRole()!=TraderCaravanRole.Chattel).Select(p=>p.LabelShort).ToList();
   var guard=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_RH_Mercenary"),fac,forceGenerateNewPawn:true));rhMade.Add(guard);
   if(guard.guest!=null)guard.guest.joinStatus=JoinStatus.JoinAsSlave;
   Check(notChattel.Count==0 && guard.GetTraderCaravanRole()==TraderCaravanRole.Guard,"slaver caravan stock slaves count as goods (Chattel) so the trade window lists them; a red hawk mercenary stays a guard ("+guard.GetTraderCaravanRole()+"/"+guard.guest?.joinStatus+") "+string.Join(",",notChattel));

   // 4. 种族池 33 个基础条目都能解析并生成（含力能龙裔）
   var basic=gen.entries.Where(e=>e.pawnKind.NullOrEmpty()).ToList();
   var unresolved=basic.Where(e=>!e.Valid).Select(e=>e.xenotype).ToList();
   var wrong=new List<string>();
   foreach(var e in basic.Where(e=>e.Valid)){
    var p=RedHawk.MakeSlave(e,fac,map.Tile,false);rhMade.Add(p);
    if(p?.genes?.Xenotype!=e.Xeno || p.DevelopmentalStage!=DevelopmentalStage.Adult)wrong.Add(e.xenotype+"→"+p?.genes?.Xenotype?.defName);
   }
   bool samePools=new[]{RedHawk.Base,RedHawk.Orbital}.All(n=>RhSlaveGen(n).entries.Select(e=>e.Key).SequenceEqual(gen.entries.Select(e=>e.Key)));
   Check(basic.Count==33 && unresolved.Count==0 && wrong.Count==0 && basic.Count(e=>e.Rare)==19 && samePools,
    "slave race pool: 33 base xenotypes (14 common + 19 rare) all resolve and generate adults of that xenotype, incl. LP_ForceDragon; base and orbital use the same pool "+string.Join(",",unresolved.Concat(wrong)));

   // 5. HAR 条目
   var har=gen.entries.Where(e=>!e.pawnKind.NullOrEmpty()).ToList();
   if(RhHar){
    var races=new List<string>();
    foreach(var e in har){var p=RedHawk.MakeSlave(e,fac,map.Tile,false);rhMade.Add(p);races.Add(p?.def.defName);}
    Check(har.Count==3 && har.All(e=>e.Valid && e.Rare && Near(e.weight,.5f)) && races.SequenceEqual(new[]{"LP_Cyclops","LP_Ettin","LP_Marilith"}),"HAR on: cyclops, ettin and marilith join the slave pool (weight 0.5, rare) and generate as their own races ("+string.Join(",",races)+")");
   }else Check(har.Count==0 && gen.Valid.Count()==33,"no HAR: the slave pool has no HAR entries and the 33 base entries still generate");

   // 6. 材料商队
   var mats=RhStock(RhKind(RedHawk.MaterialCaravan)).Where(t=>t.def!=ThingDefOf.Silver).ToList();
   var matDefs=mats.Select(t=>t.def.defName).Distinct().ToList();
   int scales=matDefs.Count(n=>n.StartsWith("LP_DragonScale_"));
   var listed=DefDatabase<TraderKindDef>.AllDefsListForReading.Where(k=>k.defName.Contains("RedHawk") || k.defName.StartsWith("LP_Caravan_RH")).SelectMany(k=>k.stockGenerators.OfType<StockGenerator_RHDefList>()).SelectMany(g=>g.entries).ToList();
   var unsellable=listed.Where(e=>!e.Sellable).Select(e=>e.def).Distinct().ToList();
   var forbidden=listed.Where(e=>new[]{"Heart","Spark","Relic","Lamp","Legacy"}.Any(w=>e.def.Contains(w)) || e.Def?.thingCategories?.Any(c=>c.defName.Contains("Relic"))==true).Select(e=>e.def).ToList();
   bool buys=new[]{"LP_Mithral","LP_DragonScale_White","LP_TrollBlood"}.All(n=>RhKind(RedHawk.MaterialCaravan).WillTrade(DefDatabase<ThingDef>.GetNamed(n)));
   Check(matDefs.Count>=8 && scales==2 && unsellable.Count==0 && forbidden.Count==0 && buys,
    "materials caravan: "+matDefs.Count+" different materials, exactly 2 dragon scale colours, every listed def exists and can be sold by a trader, no dragon heart / divine spark / relic; it also buys them "+string.Join(",",unsellable.Concat(forbidden)));

   // 7. 契约商队：2~4 张，宗主互不相同，标签带宗主名
   var pk=RhKind(RedHawk.PactCaravan);var pstock=RhStock(pk);
   var scrolls=pstock.Where(t=>t.def==Pacts.ScrollDef).ToList();var patrons=scrolls.Select(RhPatron).ToList();
   bool labels=scrolls.All(t=>{var c=t.TryGetComp<CompPactScroll>();return c.patron!=null && t.LabelNoCount.Contains(c.patron.label);});
   bool pactItems=pstock.Any(t=>t.def.defName=="LP_SoulCoin") && pk.WillTrade(DefDatabase<ThingDef>.GetNamed("LP_InfernalPitchfork")) && pk.WillTrade(DefDatabase<ThingDef>.GetNamed("LP_DemonBlood"));
   Check(scrolls.Count>=2 && scrolls.Count<=4 && patrons.All(p=>p!=null) && patrons.Distinct().Count()==patrons.Count && labels && pactItems && Pacts.Patrons.Count()==7,
    "pact caravan: "+scrolls.Count+" pact scrolls from different patrons ("+string.Join(",",patrons)+"), the label names the patron, soul coins in stock, buys pact items");

   // 8. 契约书照常在契约讲台阅读（只走到出价信）
   var reader=HtPawn(null,null,true,0,0);var sc=scrolls[0];
   GenPlace.TryPlaceThing(sc,reader.Position,map,ThingPlaceMode.Near);
   Pacts.OnRead(reader,sc);
   var letter=Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Pact>().LastOrDefault(l=>l.kind=="offer" && l.thing==sc);
   bool offered=letter!=null && letter.Text.ToString().Contains(sc.TryGetComp<CompPactScroll>().patron.label) && sc.TryGetComp<CompPactScroll>().Offered().Any();
   if(letter!=null)Find.LetterStack.RemoveLetter(letter);
   Check(offered,"a red hawk pact scroll reads like any other: the offer letter names its preset patron and lists boons");
   foreach(var p in htMade){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();else if(!p.Destroyed)p.Destroy();}htMade.Clear();

   // 9. 关掉「异界契约」：没有契约书和契约专属物品，灵魂币多一份；深渊也关掉时契约商队不来
   var s=LichMod.Settings;float before=pk.CalculatedCommonality;
   s.pactEnabled=false;
   var off=RhStock(pk);var offDefs=off.Select(t=>t.def.defName).ToList();
   var pactOnly=pk.stockGenerators.OfType<StockGenerator_RHDefList>().SelectMany(g=>g.entries).Where(e=>e.requires=="pact").Select(e=>e.def).ToList();
   int coins=off.Where(t=>t.def.defName=="LP_SoulCoin").Sum(t=>t.stackCount);
   float pactOnlyOff=pk.CalculatedCommonality;
   s.abyssEnabled=false;float bothOff=pk.CalculatedCommonality;float slaverStill=RhKind(RedHawk.SlaveCaravan).CalculatedCommonality;
   s.pactEnabled=true;s.abyssEnabled=true;
   Check(!offDefs.Contains("LP_PactScroll") && !offDefs.Any(pactOnly.Contains) && coins>=30 && before>0f && Near(pactOnlyOff,before) && bothOff==0f && slaverStill>0f,
    "pact setting off: no pact scrolls or pact-only items, "+coins+" soul coins (doubled); with abyss also off the pact caravan's commonality is 0, other caravans unaffected");

   // 10. 据点
   var settlement=Find.WorldObjects.Settlements.FirstOrDefault(x=>x.Faction==fac);
   var bstock=RhStock(RhKind(RedHawk.Base));
   int bslaves=bstock.OfType<Pawn>().Count(),bmats=bstock.Count(t=>t.def.defName.StartsWith("LP_") && t.def.defName!="LP_SoulCoin" && t.def!=Pacts.ScrollDef && !(t is Pawn)),bscroll=bstock.Count(t=>t.def==Pacts.ScrollDef);
   Check(settlement!=null && settlement.trader?.TraderKind?.defName==RedHawk.Base && bslaves>=6 && bslaves<=8 && bmats>=8 && bscroll>=1 && bstock.Any(t=>t.def.defName=="LP_SoulCoin"),
    "red hawk settlements trade as 「红鹰商站」 with all three kinds of goods ("+bslaves+" slaves, "+bmats+" material / pact stacks, "+bscroll+" scrolls)");

   // 11. 飞空商船：属于红鹰；敌对时不能被选中
   var orb=RhKind(RedHawk.Orbital);var canSpawn=AccessTools.Method(typeof(IncidentWorker_OrbitalTraderArrival),"CanSpawn");var worker=DefDatabase<IncidentDef>.GetNamed("OrbitalTraderArrival").Worker;
   bool neutral=(bool)canSpawn.Invoke(worker,new object[]{map,orb});
   int goodwill=fac.BaseGoodwillWith(Faction.OfPlayer);Factions.SetRelation(fac,Faction.OfPlayer,-90);
   bool hostile=(bool)canSpawn.Invoke(worker,new object[]{map,orb});
   Factions.SetRelation(fac,Faction.OfPlayer,goodwill);
   var ostock=RhStock(orb);int oslaves=ostock.OfType<Pawn>().Count();
   Check(orb.faction==def && neutral && !hostile && fac.RelationKindWith(Faction.OfPlayer)!=FactionRelationKind.Hostile && oslaves>=3 && oslaves<=5 && ostock.Count(t=>t.def==Pacts.ScrollDef)==1,
    "orbital trader belongs to the league, can be called while neutral but not while hostile; stock has "+oslaves+" slaves and one pact scroll");

   // 12. 派系开关
   s.factionRedHawk=false;Factions.ApplyToggles();
   var worldDefs=Factions.Managed.ToList();Factions.FilterWorldFactions(worldDefs);
   bool offOk=def.startingCountAtWorldCreation==0 && def.requiredCountAtGameStart==0 && !worldDefs.Contains(def) && def.RaidCommonalityFromPoints(1000)==0f && orb.CalculatedCommonality==0f && Factions.Of(def)!=null;
   s.factionRedHawk=true;Factions.ApplyToggles();
   int count=Find.FactionManager.AllFactionsListForReading.Count;
   Check(offOk && def.startingCountAtWorldCreation==1 && Factions.Managed.Contains(def) && !def.hidden && Factions.AddMissingToCurrentGame()==0 && Find.FactionManager.AllFactionsListForReading.Count==count,
    "faction toggle off: left out of new worlds, no raids or orbital ships, the save keeps it; on again: generated in new worlds and the 「添加派系」 button (visible, managed) has nothing to add here");
  }

  // 存读档：契约书的宗主不变
  void PrepareRedHawkSave(){
   rhSavedPatrons.Clear();
   Rand.PushState(16402);
   try{
    var cell=CellFinder.RandomClosewalkCellNear(map.Center,map,6);
    foreach(var t in RhKind(RedHawk.PactCaravan).stockGenerators.OfType<StockGenerator_RHPactScrolls>().Single().GenerateThings(map.Tile,RedHawk.Faction).ToList()){
     GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);rhSavedPatrons[t.thingIDNumber]=RhPatron(t);
    }
   }finally{Rand.PopState();}
   Check(rhSavedPatrons.Count>=2 && rhSavedPatrons.Values.Distinct().Count()==rhSavedPatrons.Count,"red hawk pact scrolls placed before saving: "+string.Join(",",rhSavedPatrons.Values));
  }
  void CheckRedHawkAfterLoad(){
   var found=map.listerThings.ThingsOfDef(Pacts.ScrollDef).Where(t=>rhSavedPatrons.ContainsKey(t.thingIDNumber)).ToList();
   Check(found.Count==rhSavedPatrons.Count && found.All(t=>RhPatron(t)==rhSavedPatrons[t.thingIDNumber] && t.LabelNoCount.Contains(t.TryGetComp<CompPactScroll>().patron.label)),"red hawk pact scrolls keep their patrons after loading ("+found.Count+")");
   foreach(var t in found)t.Destroy();
  }
 }
}
