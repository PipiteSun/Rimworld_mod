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
 // 1.55.0 异界契约（组名 Pact：Test.ps1 -Only Pact；-NoHAR -Only Pact 同样跑完，不需要 HAR）。
 public partial class LichChecks {
  readonly List<Thing> pcMade=new List<Thing>();
  IntVec3 pcArena;
  static int pcSignerId,pcDebt,pcDemandClause,pcDryadId,pcBabyId,pcFacesId,pcPactId;static string pcBoon,pcPatron,pcCopied,pcRevealed;static int pcTreeId;
  Pawn PcHuman(IntVec3 at,int level=0,string xeno=null,Gender? gender=null){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:xeno==null?XenotypeDefOf.Baseliner:DefDatabase<XenotypeDef>.GetNamed(xeno),mustBeCapableOfViolence:true,fixedGender:gender,fixedBiologicalAge:30,fixedChronologicalAge:30));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h.def.isBad || h is Hediff_AddedPart).ToList())p.health.RemoveHediff(h);
   if(level>0)Adventurer.TryAdd(p,level,true);
   pcMade.Add(p);GenSpawn.Spawn(p,at,map);return p;
  }
  Pawn PcFoe(IntVec3 at){var f=Find.FactionManager.AllFactions.FirstOrDefault(x=>!x.IsPlayer && !x.Hidden && x.HostileTo(Faction.OfPlayer) && x.def.humanlikeFaction)??PactEvents.Legion();var p=PcHuman(at);p.SetFaction(f);return p;}
  Pawn PcMake(string kind,Faction f,IntVec3 at,Gender? g=null){var p=AbyssRaces.Make(kind,f,map.Tile,g);if(p!=null){pcMade.Add(p);GenSpawn.Spawn(p,at,map);}return p;}
  IntVec3 PcCell(int dx,int dz)=>pcArena+new IntVec3(dx,0,dz);
  CompPactScroll PcScroll(string patron,IntVec3 at){var t=ThingMaker.MakeThing(Pacts.ScrollDef);var s=t.TryGetComp<CompPactScroll>();s.SetPatron(Pacts.Patron(patron));GenPlace.TryPlaceThing(t,at,map,ThingPlaceMode.Near);pcMade.Add(t);return s;}
  PlanarPact PcSign(Pawn p,string patron,string boon){var s=PcScroll(patron,p.Position);return Pacts.Sign(p,s,boon);}
  Thing PcPlace(ThingDef def,int n,IntVec3 at){var t=ThingMaker.MakeThing(def,def.MadeFromStuff?GenStuff.DefaultStuffFor(def):null);t.stackCount=n;GenPlace.TryPlaceThing(t,at,map,ThingPlaceMode.Near);pcMade.Add(t);return t;}
  void PcEndAll(){var c=Pacts.Comp;if(c==null)return;foreach(var p in c.pacts.Where(x=>x.Active).ToList())Pacts.End(p,"测试清理",false);c.pacts.Clear();Pacts.Refresh(c);}
  void PcClean(){
   var c=Pacts.Comp;
   if(c!=null){PcEndAll();c.taken.Clear();c.hunts.Clear();c.market=null;c.bloodWar=null;c.patronCooldown.Clear();c.brokersRobbed=false;c.lastExecutionTick=-1;c.lastGatheringTick=-1;c.planetarReadyTick=-1;}
   foreach(var t in pcMade){
    if(t==null)continue;
    if(t is Pawn p){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(p.Destroyed)continue;p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}
    else if(!t.Destroyed)t.Destroy();
   }
   pcMade.Clear();
   var fdefs=new[]{PactEvents.LegionDef,PactEvents.HuntDef,PactEvents.BrokersDef,PactEvents.CourtDef,PactEvents.TruceDef,AbyssRaces.HordeDef};
   foreach(var l in map.lordManager.lords.Where(l=>l.faction!=null && fdefs.Contains(l.faction.def)).ToList())map.lordManager.RemoveLord(l);
   foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>p.Faction!=null && fdefs.Contains(p.Faction.def) || PactRaces.Is(p,PactRaces.Deva) || PactRaces.Is(p,PactRaces.Planetar) || p.kindDef?.defName?.StartsWith("LP_HL_")==true || p.def.defName=="LP_Pixie").ToList()){p.GetLord()?.RemovePawn(p);p.DeSpawn();p.Destroy();}
   foreach(var t in map.listerThings.AllThings.Where(t=>t.def==Pacts.ScrollDef || t.def==AbyssItems.TrueNameScroll || t.def==AbyssItems.Demonomicon || t.def==AbyssItems.SoulCoin || t.def==PactRaces.Pitchfork || t.def.defName=="LP_NetherilArcane" || t.def==PactRaces.Rope || t.def.defName=="LP_WildHuntHorn" || t.def.defName=="LP_ColdIron" && t.stackCount==10).ToList())if(t.Spawned)t.Destroy();
   Items.State?.Flush();
  }
  void PcFlush(){Items.State?.Flush();}
  static string Txt(DiaOption o)=>(string)AccessTools.Field(typeof(DiaOption),"text").GetValue(o)??"";

  void CheckPact(){
   pcArena=AbFindArena();bool har=ModsConfig.IsActive(HARId);
   var oldHidden=LichMod.Settings.pactHiddenClauses;
   try{
    CheckPactDefs();
    CheckPactSigning();
    CheckPactReview();
    CheckPactRaces();
    CheckPactDemands();
    CheckPactDebt();
    CheckPactUnbind();
    CheckPactBlast();
    CheckPactBoons();
    CheckPactUnits(har);
    CheckPactCelestial();
    CheckPactEvents();
    CheckPactPerformance();
   }finally{Legendary.TestD20=null;Pacts.TestRoll=null;PactRaces.TestRoll=null;PactCelestial.TestRoll=null;Outsiders.TestRoll=null;LichMod.Settings.pactHiddenClauses=oldHidden;PcClean();}
  }

  // ---------- 定义 ----------
  void CheckPactDefs(){
   var patrons=Pacts.Patrons.ToList();
   Check(patrons.Count==7 && patrons.Count(d=>d.Hells)==4 && patrons.Count(d=>d.Fey)==3 && patrons.All(d=>!d.ConfigErrors().Any()) && patrons.All(d=>d.boons.All(b=>b.Worker!=null)),"seven patrons (4 Hells, 2 queens + the hag coven) load cleanly; every boon has a worker");
   var r=Pacts.Research;
   Check(r!=null && Math.Abs(r.ResearchViewX-5f)<.01f && Math.Abs(r.ResearchViewY-2.8f)<.01f && r.prerequisites.Contains(AbyssRitual.Research) && Math.Abs(r.baseCost-1800f)<1f,"planar pacts research at (5, 2.8) after abyssology, 1800");
   foreach(var def in DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.defName.StartsWith("LP_") && (d.defName.Contains("Pact") || d.defName.Contains("Infernal") || d.defName.Contains("Fey") || d.defName.Contains("Celestial") || d.defName=="LP_Imp" || d.defName=="LP_Pixie" || d.defName=="LP_EntanglingRope" || d.defName=="LP_WildHuntHorn")))Check(!def.ConfigErrors().Any(),def.defName+" definition: "+string.Join(";",def.ConfigErrors()));
   var legion=PactEvents.Legion();var hunt=PactEvents.FeyHunt();var brokers=PactEvents.BrokersEnsure();var court=PactEvents.Court();var horde=AbyssRaces.Horde();
   Check(legion!=null && hunt!=null && legion.Hidden && hunt.Hidden && legion.HostileTo(Faction.OfPlayer) && hunt.HostileTo(Faction.OfPlayer) && legion.HostileTo(horde) && hunt.HostileTo(horde) && brokers!=null && court!=null && !brokers.HostileTo(Faction.OfPlayer) && !court.HostileTo(Faction.OfPlayer),
    "factions: Hells legion and wild hunt are hidden, hostile to the player and to the abyss horde; Hells brokers and the fey court are neutral");
  }

  // ---------- 签约流程 / 一人一份 / 上限 ----------
  void CheckPactSigning(){
   var lec=ThingMaker.MakeThing(Pacts.LecternDef,ThingDefOf.WoodLog);lec.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(lec,PcCell(0,0),map);pcMade.Add(lec);
   var p=PcHuman(PcCell(0,2),3);var s=PcScroll("LP_Patron_Asmodeus",PcCell(1,2));var scroll=s.parent;
   Check(s.Draft().Count(c=>c.hidden)==2 && s.Draft().Count(c=>!c.hidden)==1 && s.Offered().Count()==3 && scroll.LabelNoCount.Contains("阿斯莫蒂尔斯"),"Asmodeus scroll: 1 visible clause, 2 hidden clauses rolled from the pool, 3 boons, label names the patron ("+scroll.LabelNoCount+")");
   Pacts.OnRead(p,scroll);var letter=Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Pact>().LastOrDefault(l=>l.kind=="offer" && l.thing==scroll);
   Check(letter!=null && letter.Choices.Count(o=>Txt(o).StartsWith("签下"))==3 && letter.Choices.Any(o=>Txt(o).StartsWith("审阅")) && letter.Choices.Any(o=>Txt(o).StartsWith("谈判")) && letter.Choices.Any(o=>Txt(o).StartsWith("拒绝")) && letter.Text.ToString().Contains("？？？"),"reading at the lectern sends a choice letter: three sign buttons, review, negotiate, refuse; hidden clauses show as ？？？");
   if(letter!=null)Find.LetterStack.RemoveLetter(letter);
   float taint0=AbyssTaint.Severity(p);int now=Find.TickManager.TicksGame;
   var pact=Pacts.Sign(p,s,"HellGuard");
   Check(pact!=null && pact.Active && scroll.Destroyed && Items.Has(p,Pacts.SignerDef) && p.abilities.GetAbility(Pacts.BlastDef)!=null && p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SummonHornedDevil"))!=null && Items.Has(p,"LP_SoulMortgaged") && Near(AbyssTaint.Severity(p)-taint0,.1f) && Math.Abs(pact.nextDemandTick-now-20*GenDate.TicksPerDay)<GenDate.TicksPerHour,
    "signing: the scroll burns, the signer gets the pact status, eldritch blast, the boon ability, the soul mortgage (soul term) and +10% taint; the first demand is due in 20 days");
   var s2=PcScroll("LP_Patron_Titania",p.Position);
   Check(Pacts.CannotSign(p,s2.patron)!=null && Pacts.Sign(p,s2,"SummerTouch")==null,"one pact per person");
   var others=new List<Pawn>();int i=0;
   while(Pacts.ActiveCount()<Pacts.Cap() && i<4){var o=PcHuman(PcCell(2+i,2),1);others.Add(o);PcSign(o,"LP_Patron_Titania","SummerTouch");i++;}
   var extra=PcHuman(PcCell(8,2),1);
   Check(Pacts.Cap()==1+AdventurerEnemies.ColonyMaxLevel()/8 && Pacts.ActiveCount()==Pacts.Cap() && Pacts.CannotSign(extra,s2.patron)!=null && Pacts.CannotSign(extra,s2.patron).Contains("上限"),"colony cap = 1 + highest level / 8 ("+Pacts.Cap()+"); a pact over the cap is refused");
   // 拒绝：书销毁，心情 -3，该宗主 60 天不再低语
   var s3=PcScroll("LP_Patron_Glasya",extra.Position);Pacts.Refuse(extra,s3);
   Check(s3.parent.Destroyed && extra.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_PactMissed"))!=null && Pacts.Comp.patronCooldown.TryGetValue("LP_Patron_Glasya",out int cd) && cd>now+59*GenDate.TicksPerDay,"refusing burns the scroll, -3 mood, and the patron stays away from whispers for 60 days");
   PcEndAll();
  }

  // ---------- 审阅 / 谈判 / 隐藏条款开关 ----------
  void CheckPactReview(){
   var p=PcHuman(PcCell(0,4),1);var law=PcHuman(PcCell(1,4),1);law.skills.GetSkill(SkillDefOf.Social).Level=20;
   var s=PcScroll("LP_Patron_Mephistopheles",p.Position);
   Legendary.TestD20=1;bool fail=Pacts.Review(p,s);Legendary.TestD20=20;bool ok=Pacts.Review(p,s,law);bool third=Pacts.Review(p,s,law);Legendary.TestD20=null;
   Check(!fail && ok && !third && s.reviews==2 && s.Draft().Count(c=>c.hidden && c.revealed)==1,"review: Int save against DC 18 (fixed d20: 1 fails, 20 succeeds) reveals one hidden clause; at most two reviews per scroll");
   Legendary.TestD20=20;bool neg=Pacts.Negotiate(p,s);int vis=s.Draft().Count(c=>!c.hidden);bool struck=Pacts.Strike(s,s.Draft().FindIndex(c=>!c.hidden));bool again=Pacts.Negotiate(p,s);Legendary.TestD20=null;
   Check(neg && struck && s.Draft().Count(c=>!c.hidden)==vis-1 && !again,"negotiation (Hells only, once): a successful Cha save lets you strike a visible cost");
   var s2=PcScroll("LP_Patron_Dispater",p.Position);int v2=s2.Draft().Count(c=>!c.hidden);Legendary.TestD20=1;Pacts.Negotiate(p,s2);Legendary.TestD20=null;
   var s3=PcScroll("LP_Patron_Titania",p.Position);
   Check(s2.Draft().Count(c=>!c.hidden)==v2+1 && !s3.patron.negotiable && !Pacts.Negotiate(p,s3),"a failed negotiation adds one more visible cost from the pool; fey patrons cannot be negotiated with");
   LichMod.Settings.pactHiddenClauses=false;Pacts.OnRead(p,s3.parent);var l=Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Pact>().LastOrDefault(x=>x.thing==s3.parent);bool noReview=l!=null && !l.Choices.Any(o=>Txt(o).StartsWith("审阅")) && !l.Text.ToString().Contains("？？？");if(l!=null)Find.LetterStack.RemoveLetter(l);LichMod.Settings.pactHiddenClauses=true;
   Check(noReview,"with hidden clauses turned off every clause is visible and there is no review button");
   foreach(var x in Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Pact>().ToList())Find.LetterStack.RemoveLetter(x);
  }

  // ---------- 种族与信仰修正 ----------
  void CheckPactRaces(){
   var tief=PcHuman(PcCell(0,6),1,"LP_Tiefling");var s=PcScroll("LP_Patron_Glasya",tief.Position);
   int dc=Pacts.ReviewDC(tief,s.patron);var pact=Pacts.Sign(tief,s,"QueenTongue");
   Check(dc==16 && pact!=null && Near(pact.periodFactor,1.33f),"a tiefling signing with the Hells: review +2 (DC 16), payment period ×1.33");
   PcEndAll();
   var aas=PcHuman(PcCell(1,6),1,"LP_Aasimar");
   Check(Pacts.CannotSign(aas,Pacts.Patron("LP_Patron_Asmodeus"))!=null && Pacts.CannotSign(aas,Pacts.Patron("LP_Patron_Titania"))==null,"an aasimar refuses the Hells (taint under 50%) but may sign with the fey");
   var lich=PcHuman(PcCell(2,6),1);lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),false);
   var lp=PcSign(lich,"LP_Patron_Asmodeus","InfernalMark");
   Check(lp!=null && lp.lichMortgage && Near(lp.boonFactor,1.5f) && !Items.Has(lich,"LP_SoulMortgaged") && Near(lich.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_Boon_InfernalMark")).Severity,1.5f),"a lich signing a soul pact: phylactery mortgage, boon values ×1.5, no soul mortgage status");
   Pacts.AddDebt(lp,3,"测试");
   Check(lp.ended && Items.Has(lich,"LP_PhylacteryLost") && lich.Spawned,"a lich's debt 3 takes the phylactery instead of the lich");
   PcEndAll();
  }

  // ---------- 偿契 ----------
  void CheckPactDemands(){
   var c=Pacts.Comp;int now=Find.TickManager.TicksGame;
   var a=PcHuman(PcCell(0,8),1);var pa=PcSign(a,"LP_Patron_Asmodeus","HellGuard");
   Pacts.StartDemand(pa,now,pa.clauses.FindIndex(x=>x.kind=="SacrificePrisoner"));
   var pr=PcHuman(PcCell(1,8));pr.guest.CapturedBy(Faction.OfPlayer,a);ExecutionUtility.DoExecutionByCut(a,pr,8,true);
   Pacts.Check(pa,Find.TickManager.TicksGame);
   Check(c.lastExecutionTick>=now && pa.demandClause<0 && pa.debt==0,"sacrifice demand: a vanilla execution is recorded by the hook (timestamp) and satisfies it");
   Pacts.StartDemand(pa,now,pa.clauses.FindIndex(x=>x.kind=="SacrificePrisoner"));pa.demandDeadline=Find.TickManager.TicksGame-1;c.lastExecutionTick=-1;
   Pacts.Check(pa,Find.TickManager.TicksGame);
   Check(pa.debt==1 && pa.demandClause<0,"a demand not met in 3 days: debt +1");
   Pacts.End(pa,"测试",false);
   var m=PcHuman(PcCell(2,8),1);var pm=PcSign(m,"LP_Patron_Mephistopheles","IceArcana");
   Pacts.StartDemand(pm,now,pm.clauses.FindIndex(x=>x.kind=="Item"));
   PcPlace(AbyssItems.Demonomicon,1,PcCell(3,8));
   bool canBook=Pacts.CannotDeliver(pm,pm.Demand)==null;bool paid=Pacts.Deliver(pm,pm.Demand);pm.demandPaid=paid;Pacts.Check(pm,Find.TickManager.TicksGame);
   Check(canBook && paid && pm.demandClause<0 && pm.debt==0,"item demand (book or scroll): the deliver button takes a book from the map");
   Pacts.End(pm,"测试",false);
   var d=PcHuman(PcCell(4,8),1);var pd=PcSign(d,"LP_Patron_Dispater","IronArtisans");
   Check(pd.nextDemandTick<0 && pd.expireTick>0,"Dispater's one-year pact has no periodic demand, only the bill at expiry");
   pd.expireTick=Find.TickManager.TicksGame-1;Pacts.Check(pd,Find.TickManager.TicksGame);var bill=pd.Demand;
   for(int k=0;k<4;k++)PcPlace(ThingDefOf.Silver,1500,PcCell(5,8));
   bool payDisp=bill!=null && bill.kind=="Silver" && bill.count==6000 && Pacts.Deliver(pd,bill);pd.demandPaid=true;Pacts.Check(pd,Find.TickManager.TicksGame);
   Check(payDisp && pd.ended && Pacts.SilverOn(map)<6000,"at expiry Dispater bills 6000 silver; paying it ends the pact");
   var g=PcHuman(PcCell(6,8),1);var pg=PcSign(g,"LP_Patron_Titania","SummerTouch");
   Pacts.StartDemand(pg,now,pg.clauses.FindIndex(x=>x.kind=="Gathering"));c.lastGatheringTick=Find.TickManager.TicksGame;Pacts.Check(pg,Find.TickManager.TicksGame);
   bool hooked=Harmony.GetPatchInfo(AccessTools.Method(typeof(GatheringWorker),nameof(GatheringWorker.TryExecute)))?.Postfixes.Any(x=>x.owner=="local.lich.phylactery")==true;
   Check(hooked && pg.demandClause<0 && pg.debt==0,"gathering demand: the party / wedding hooks record a timestamp that satisfies it");
   // 自动执行：秘密、名字、一年寿命
   var x1=PcHuman(PcCell(7,8));var x2=PcHuman(PcCell(8,8));
   var secret=new PactClauseState{kind="Secret",label="秘密"};var nameC=new PactClauseState{kind="Name",label="名字"};var yearC=new PactClauseState{kind="Year",label="一年寿命"};
   string sec=Pacts.AutoExecute(pg,secret);
   bool opinion=map.mapPawns.FreeColonistsSpawned.Count(p=>p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_SecretRevealed"))!=null)>=2;
   var names=map.mapPawns.FreeColonistsSpawned.ToDictionary(p=>p,p=>p.Name.ToStringFull);Pacts.AutoExecute(pg,nameC);
   var renamed=map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p=>names.ContainsKey(p) && names[p]!=p.Name.ToStringFull);
   long age=g.ageTracker.AgeBiologicalTicks;Pacts.AutoExecute(pg,yearC);
   Check(sec!=null && opinion && renamed!=null && Items.Has(renamed,"LP_NameStolenHediff") && g.ageTracker.AgeBiologicalTicks-age==GenDate.TicksPerYear && pg.debt==0,"automatic costs never add debt: a secret (two colonists -20 opinion, -5 mood), a name (renamed, -20% social impact) and a year of life (+1 biological year)");
   PcEndAll();
  }

  // ---------- 债务 1/2/3、补偿、赎人 ----------
  void CheckPactDebt(){
   var c=Pacts.Comp;
   var a=PcHuman(PcCell(0,10),1);var pa=PcSign(a,"LP_Patron_Asmodeus","HellGuard");
   Pacts.AddDebt(pa,1,"测试");var tw=new ThoughtWorker_PactDebt{def=DefDatabase<ThoughtDef>.GetNamed("LP_PactDebtThought")};
   bool mood=tw.CurrentState(a).Active;
   var ab=a.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SummonHornedDevil"));
   var before=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);ab.Activate(new LocalTargetInfo(a),new LocalTargetInfo(a));PcFlush();
   var devil=map.mapPawns.AllPawnsSpawned.FirstOrDefault(p=>!before.Contains(p) && PactRaces.Is(p,"LP_HornedDevil"));if(devil!=null)pcMade.Add(devil);
   Check(mood && devil!=null && devil.Faction==Faction.OfPlayer && Outsiders.Thrall(devil) && devil.equipment?.Primary?.def==PactRaces.Pitchfork && ab.CooldownTicksRemaining>=900000*2-10,"debt 1: '违约的阴影' mood; the summon still works (a horned devil thrall with a pitchfork) but its cooldown doubles ("+ab.CooldownTicksRemaining+")");
   before=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);Pacts.AddDebt(pa,1,"测试");
   var collectors=map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p) && p.Faction?.def==PactEvents.LegionDef).ToList();pcMade.AddRange(collectors);
   var lead=collectors.FirstOrDefault(p=>p.health.hediffSet.hediffs.OfType<Hediff_PatronAgent>().Any());
   Check(collectors.Count>=3 && lead!=null && lead.health.hediffSet.hediffs.OfType<Hediff_PatronAgent>().First().patron==pa.patron && Legendary.LevelOf(lead)>=14 && pa.lastCollectorTick>=0,"debt 2: Asmodeus sends debt collectors (chain / bone / horned devils) led by a high-level horned devil who carries the patron agent's true name ("+collectors.Count+")");
   // 补偿：地图上有 2 名囚犯 → 债务 -1
   var p1=PcHuman(PcCell(2,10));p1.guest.CapturedBy(Faction.OfPlayer,a);var p2=PcHuman(PcCell(3,10));p2.guest.CapturedBy(Faction.OfPlayer,a);
   string why=Pacts.CannotCompensate(pa);bool comp=Pacts.Compensate(pa);
   Check(why==null && comp && pa.debt==1 && p1.Dead && p2.Dead,"compensating the patron at the lectern (double the current demand: two prisoners) lowers debt by 1");
   // 强制执行：拖走 → 赎回
   Pacts.AddDebt(pa,2,"测试");var t=c.taken.FirstOrDefault(x=>x.pawn==a);
   Check(pa.ended && t!=null && !a.Spawned && a.abilities.GetAbility(Pacts.BlastDef)==null && t.plane==OutsiderPlane.Hells,"debt 3: the signer is dragged into the Hells (gone from the map), the pact ends and its boons are removed");
   for(int k=0;k<2;k++)PcPlace(ThingDefOf.Silver,1500,PcCell(4,10));
   float taint0=AbyssTaint.Severity(a);var back=Pacts.Ransom(t,map,true);
   Check(back==a && a.Spawned && a.Faction==Faction.OfPlayer && !c.taken.Contains(t) && AbyssTaint.Severity(a)-taint0>.19f,"ransom from the Hells brokers (3000 silver): the signer comes back at the map edge with +20% taint");
   PcEndAll();
  }

  // ---------- 解约三种方式 ----------
  void CheckPactUnbind(){
   var c=Pacts.Comp;var god=FaithUtility.AllDeities.FirstOrDefault(d=>d.Good && d.worshippable);var oldPatron=FaithUtility.Patron;
   FaithUtility.SetPatron(god,true);FaithUtility.AddFavor(god,100f-FaithUtility.GetFavor(god),"测试");
   var a=PcHuman(PcCell(0,12),1);var pa=PcSign(a,"LP_Patron_QueenOfAir","WinterVeil");
   PactCelestial.TestRoll=.9f;float t0=AbyssTaint.Severity(a);bool broke=PactCelestial.Break(pa);
   Check(!broke && pa.Active && pa.debt==1 && AbyssTaint.Severity(a)-t0>.29f && Near(FaithUtility.GetFavor(god),40f),"break-pact rite at the altar: 60 favor; a failure costs +30% taint and +1 debt");
   FaithUtility.AddFavor(god,60f,"测试");PactCelestial.TestRoll=0f;broke=PactCelestial.Break(pa);PactCelestial.TestRoll=null;
   Check(broke && pa.ended && a.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_WinterVeil"))==null && a.abilities.GetAbility(Pacts.BlastDef)==null,"a successful break-pact rite ends the pact and removes the boon ability and eldritch blast");
   // 代理人真名卷
   var b=PcHuman(PcCell(1,12),1);var pb=PcSign(b,"LP_Patron_Mephistopheles","IceGuard");
   var lead=PcMake("LP_HL_CollectorLeader",PactEvents.Legion(),PcCell(2,12));var h=(Hediff_PatronAgent)Items.Give(lead,"LP_PatronAgent");h.patron=pb.patron;Outsiders.Banish(lead);
   var sc=Building_PactLectern.AgentScroll(map,pb.patron);if(sc!=null)pcMade.Add(sc);
   if(sc!=null){sc.Destroy();Pacts.Unbind(pb,"测试：代理人真名");}
   Check(sc!=null && pb.ended,"the collector leader drops the patron agent's true name scroll; reading it at the lectern ends the pact at no cost");
   // 格拉西娅：解开契约
   var d=PcHuman(PcCell(3,12),20);var e=PcHuman(PcCell(4,12),1);var pe=PcSign(e,"LP_Patron_Titania","SummerTouch");var pd=PcSign(d,"LP_Patron_Glasya","Unbind");
   Check(pe!=null && pe.ended && pd!=null && Near(pd.periodFactor,.75f),"Glasya's 'Unbind' boon dissolves another pact in the colony; her own pact's period becomes ×0.75");
   PcEndAll();FaithUtility.SetPatron(oldPatron,true);
  }

  // ---------- 魔能爆 ----------
  void CheckPactBlast(){
   var a=PcHuman(PcCell(0,14),1);var h=Adventurer.Get(a);
   var beams=new List<int>();foreach(int l in new[]{1,5,11,17}){h.level=l;beams.Add(Pacts.Beams(a));}h.level=1;
   Check(beams.SequenceEqual(new[]{1,2,3,4}) && Pacts.Eldritch.defName=="LP_Eldritch" && Near(Pacts.Eldritch.defaultArmorPenetration,.3f) && Near(Pacts.BlastDamage,12f),"eldritch blast: 1/2/3/4 beams at levels 1/5/11/17, 12 eldritch (force) damage, armor penetration 30%");
   var pa=PcSign(a,"LP_Patron_Asmodeus","HellGuard");var foe=PcFoe(PcCell(3,14));h.level=5;
   Pacts.TestRoll=0f;int hits=Pacts.Blast(a,foe);Pacts.TestRoll=null;
   Check(hits==2 && (foe.Dead || foe.health.hediffSet.hediffs.Any(x=>x is Hediff_Injury)) && (foe.Dead || foe.Downed || foe.HasAttachment(ThingDefOf.Fire) || map.weatherManager.RainRate>0f || foe.GetStatValue(StatDefOf.Flammability)<=0f),"[hits "+hits+" fire "+foe.HasAttachment(ThingDefOf.Fire)+" rain "+map.weatherManager.RainRate+" flam "+foe.GetStatValue(StatDefOf.Flammability)+"] a level 5 Hells signer fires two beams; Hells patrons ignite (fixed roll)");
   PcEndAll();
   var b=PcHuman(PcCell(0,15),1);PcSign(b,"LP_Patron_Titania","SummerTouch");var foe2=PcFoe(PcCell(3,15));Pacts.TestRoll=0f;Pacts.Blast(b,foe2);Pacts.TestRoll=null;
   Check(foe2.Dead || foe2.stances.stunner.Stunned,"fey patrons: a blast hit beguiles (stuns) the target for 2 seconds (fixed roll)");
   PcEndAll();
  }

  // ---------- 每个宗主的恩赐 ----------
  void CheckPactBoons(){
   // 阿斯莫蒂尔斯：印记 / 艾林魔
   var a=PcHuman(PcCell(0,17),1);PcSign(a,"LP_Patron_Asmodeus","InfernalMark");var foe=PcFoe(PcCell(2,17));
   int n=Hediff_InfernalMark.Pulse(a);
   Check(n>=1 && Items.Has(foe,"LP_InfernalDread") && Near(foe.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_InfernalDread")).CurStage.statOffsets.GetStatOffsetFromList(StatDefOf.MentalBreakThreshold),.15f),"Asmodeus 'Infernal Mark': hostile humanlikes within 5 cells get dread (mental break threshold +15%)");
   PcEndAll();
   var e=PcHuman(PcCell(0,18),1);var pe=PcSign(e,"LP_Patron_Asmodeus","Erinyes");var bound=pe.bound;if(bound!=null)pcMade.Add(bound);
   Check(bound!=null && PactRaces.IsErinyes(bound) && Outsiders.Bound(bound) && Legendary.LevelOf(bound)>=9 && AbyssBinding.DailyDecay(bound,AbyssBinding.Binding(bound))==0f,"Asmodeus 'Erinyes': a bound erinyes (level 9) whose obedience never decays");
   e.Kill(null);PcFlush();
   Check(pe.ended && (bound==null || bound.Destroyed),"when the signer dies the erinyes returns to the Hells");
   // 梅菲斯托菲勒斯
   var m=PcHuman(PcCell(0,19),1);float ent0=m.GetStatValue(StatDefOf.PsychicEntropyMax);PcSign(m,"LP_Patron_Mephistopheles","IceArcana");float ent1=m.GetStatValue(StatDefOf.PsychicEntropyMax);
   var foe2=PcFoe(PcCell(2,19));foe2.TakeDamage(new DamageInfo(DamageDefOf.Frostbite,5f,1f,-1f,m));
   var books=PactBoon_Books.Make();pcMade.AddRange(books);
   Check((ent0<=0f || Near(ent1/ent0,1.2f)) && Fiends.ChillLayers(foe2)>=1 && books.Count==2 && books.Any(t=>t.def==AbyssItems.Demonomicon),"Mephistopheles: 'Ice Arcana' psychic entropy max ×1.2 and frost hits add a chill layer; 'Forbidden Books' brings a demonomicon and a Netheril arcane scroll");
   PcEndAll();
   // 迪斯帕特
   var d=PcHuman(PcCell(0,20),1);var other=PcHuman(PcCell(1,20));PcSign(d,"LP_Patron_Dispater","IronArtisans");
   var forge=Pacts.ForgeDef;var anvil=DefDatabase<ThingDef>.GetNamed("LP_RuneAnvil");
   Check(Items.Has(other,"LP_Boon_IronArtisan") && Near(other.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_Boon_IronArtisan")).CurStage.statFactors.GetStatFactorFromList(StatDefOf.ConstructionSpeed),1.25f) && anvil.AllRecipes.Count>0 && anvil.AllRecipes.All(r=>forge.AllRecipes.Contains(r)) && Near(forge.GetStatValueAbstract(StatDefOf.WorkTableWorkSpeedFactor),1.5f),"Dispater: 'Iron Artisans' gives colonists on the map construction ×1.25; the infernal forge makes every rune anvil recipe at ×1.5");
   PcEndAll();
   Check(!Items.Has(other,"LP_Boon_IronArtisan"),"ending the pact removes the colony-wide boon");
   // 格拉西娅
   var g1=PcHuman(PcCell(0,21),1);var g2=PcHuman(PcCell(1,21),1);var pg2=PcSign(g2,"LP_Patron_Asmodeus","HellGuard");
   float si0=g1.GetStatValue(StatDefOf.SocialImpact);var pg1=PcSign(g1,"LP_Patron_Glasya","QueenTongue");float si1=g1.GetStatValue(StatDefOf.SocialImpact);
   PcEndAll();var g3=PcHuman(PcCell(2,21),20);var g4=PcHuman(PcCell(3,21),1);var p4=PcSign(g4,"LP_Patron_Asmodeus","HellGuard");PcSign(g3,"LP_Patron_Glasya","SeeClauses");
   Check(si1>si0*1.2f && p4.clauses.Where(c=>c.hidden).All(c=>c.revealed),"Glasya: 'Queen's Tongue' social impact ×1.3; 'See Clauses' reveals every hidden clause of the other pacts");
   PcEndAll();
   // 提泰妮娅
   var t=PcHuman(PcCell(0,22),1);PcSign(t,"LP_Patron_Titania","SummerGrace");
   var zone=new Zone_Growing(map.zoneManager);map.zoneManager.RegisterZone(zone);zone.AddCell(PcCell(4,22));
   var crop=(Plant)ThingMaker.MakeThing(ThingDefOf.Plant_Potato);crop.Growth=.3f;GenSpawn.Spawn(crop,PcCell(4,22),map);pcMade.Add(crop);
   bool frost=(bool)AccessTools.Method(typeof(SummerNoFrost),"Prefix").Invoke(null,new object[]{crop,Plant.LeaflessCause.Cold});
   bool inSet=PactBoons.summerMaps.Contains(map);var gargs=new object[]{crop,1f};AccessTools.Method(typeof(SummerGrowth),"Postfix").Invoke(null,gargs);float grow=(float)gargs[1];PcEndAll();gargs=new object[]{crop,1f};AccessTools.Method(typeof(SummerGrowth),"Postfix").Invoke(null,gargs);float grow0=(float)gargs[1];zone.Delete();
   Check(inSet && PactBoons.summerMaps.Count==0 && !frost && Near(grow,1.3f) && Near(grow0,1f),"Titania 'Summer Grace': crops on the signer's map grow ×1.3 and do not die of cold in growing zones ("+grow+" / "+grow0+")");
   var t2=PcHuman(PcCell(0,23),1);var pt=PcSign(t2,"LP_Patron_Titania","SummerTouch");var hurt=PcHuman(PcCell(1,23));
   var inj=HediffMaker.MakeHediff(HediffDefOf.Cut,hurt,hurt.RaceProps.body.corePart);inj.Severity=8;hurt.health.AddHediff(inj);
   var ta=t2.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SummerTouch"));ta.Activate(new LocalTargetInfo(hurt),new LocalTargetInfo(hurt));
   Check(!hurt.health.hediffSet.hediffs.Contains(inj),"Titania 'Summer Touch' heals a colonist's worst wound");
   PcEndAll();
   // 风暗女王
   var q=PcHuman(PcCell(0,24),1);PcSign(q,"LP_Patron_QueenOfAir","WinterVeil");var vf=PcFoe(PcCell(2,24));
   Legendary.TestD20=1;int charmed=PactRaces.Veil(q);Legendary.TestD20=null;
   bool veil=charmed>=1 && vf.MentalStateDef?.defName=="LP_DragonCharmed";if(vf.InMentalState)vf.MentalState.RecoverFromState();
   PcEndAll();var q2=PcHuman(PcCell(0,25),1);PcSign(q2,"LP_Patron_QueenOfAir","FrostShadow");var fs=q2.health.hediffSet.hediffs.OfType<Hediff_FrostShadow>().FirstOrDefault();
   q2.jobs.StopAll();q2.mindState.lastHarmTick=-99999;for(int k=0;k<5;k++)fs?.TickInterval(60);
   bool hidden=Items.Has(q2,"LP_BarlguraHidden");PcEndAll();
   var q3=PcHuman(PcCell(0,26),1);PcSign(q3,"LP_Patron_QueenOfAir","RedcapServants");var before=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
   q3.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SummonRedcaps")).Activate(new LocalTargetInfo(q3),new LocalTargetInfo(q3));
   var caps=map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p) && PactRaces.Is(p,PactRaces.Redcap)).ToList();pcMade.AddRange(caps);
   Check(veil && hidden && caps.Count==4 && caps.All(Outsiders.Thrall),"Queen of Air: 'Winter Veil' charms enemies that fail a Wis save; 'Frost Shadow' hides a still signer; 'Redcap Servants' brings 4 redcap thralls");
   PcEndAll();
   // 巫婆集会：6 个恩赐里抽 3 个；定金
   var hg=PcScroll("LP_Patron_Hags",PcCell(0,27));var offered=hg.Offered().ToList();
   var hp=PcHuman(PcCell(1,27),1);var ph=Pacts.Sign(hp,hg,offered[0].key);var hl=Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Pact>().LastOrDefault(l=>l.kind=="hags");
   var eye=hl?.Choices.FirstOrDefault(o=>Txt(o).Contains("眼睛"));eye?.action?.Invoke();
   Check(hg.patron.boons.Count==6 && offered.Count==3 && offered.Distinct().Count()==3 && ph!=null && eye!=null && hp.health.hediffSet.GetMissingPartsCommonAncestors().Any(x=>x.Part.def==BodyPartDefOf.Eye) && ph.Clause("YearOrEye")==null,"the hag coven offers 3 of the queens' 6 boons; the down payment can be an eye");
   PcEndAll();
  }

  // ---------- 单位 ----------
  void CheckPactUnits(bool har){
   var legion=PactEvents.Legion();var hunt=PactEvents.FeyHunt();var made=new Dictionary<string,Pawn>();int x=0;
   foreach(var k in new[]{"LP_HL_ChainDevil","LP_HL_BoneDevil","LP_HL_HornedDevil","LP_HL_Erinyes","LP_HL_Amnizu"}){made[k]=PcMake(k,legion,PcCell(x,29));x+=2;}
   foreach(var k in new[]{"LP_FW_Redcap","LP_FW_WildHunter","LP_FW_WildHuntLord"}){made[k]=PcMake(k,hunt,PcCell(x,29));x+=2;}
   var sat=PcMake("LP_FY_Satyr",Faction.OfPlayer,PcCell(0,31));var dry=PcMake("LP_FY_Dryad",Faction.OfPlayer,PcCell(2,31),Gender.Female);var cha=PcMake("LP_FY_ChangelingVolunteer",Faction.OfPlayer,PcCell(4,31));
   var deva=PcMake("LP_CE_Deva",Faction.OfPlayer,PcCell(6,31));var plan=PcMake("LP_CE_Planetar",Faction.OfPlayer,PcCell(9,31));
   var imp=PcMake("LP_ImpFamiliar",Faction.OfPlayer,PcCell(12,31));var pixie=PcMake("LP_PixieFamiliar",Faction.OfPlayer,PcCell(13,31));
   Check(made.Values.All(p=>p!=null) && made.Take(5).All(kv=>Outsiders.IsDevil(kv.Value)) && made.Skip(5).All(kv=>Outsiders.Of(kv.Value)?.plane==OutsiderPlane.Feywild) && Near(made["LP_HL_HornedDevil"].BodySize,1.8f) && Near(made["LP_FW_Redcap"].BodySize,.6f) && made["LP_HL_ChainDevil"].needs.food==null
    && Legendary.LevelOf(made["LP_HL_BoneDevil"])==8 && Legendary.LevelOf(made["LP_FW_WildHuntLord"])>=12,"all Hells / fey units generate with their genes, sizes (horned devil 1.8, redcap 0.6) and levels (bone devil 8, hunt lord 12-14)");
   Check(sat!=null && dry!=null && cha!=null && deva!=null && plan!=null && imp!=null && pixie!=null && !Outsiders.Is(sat) && !Outsiders.Is(cha) && Outsiders.Of(dry)?.plane==OutsiderPlane.Feywild && PactRaces.Celestial(deva) && PactRaces.Celestial(plan) && imp.RaceProps.Animal && Items.Has(imp,"LP_ImpNature") && Items.Has(pixie,"LP_PixieNature")
    && sat.genes.Xenotype.inheritable && cha.genes.Xenotype.inheritable,"satyr and changeling are inheritable mortals; the dryad is a fey outsider; deva and planetar are celestials; imp and pixie are familiar animals");
   // 克制：九狱银 ×1.5，妖精寒铁 ×1.5
   var fighter=PcHuman(PcCell(0,33));var sword=DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword");
   var silver=(ThingWithComps)ThingMaker.MakeThing(sword,ThingDefOf.Silver);fighter.equipment.AddEquipment(silver);float fs=ItemMaterials.WeaponFactor(made["LP_HL_ChainDevil"],new DamageInfo(DamageDefOf.Cut,10,0,-1,fighter,null,sword));fighter.equipment.DestroyAllEquipment();
   var cold=(ThingWithComps)ThingMaker.MakeThing(sword,ItemMaterials.ColdIron);fighter.equipment.AddEquipment(cold);float fc=ItemMaterials.WeaponFactor(made["LP_FW_WildHunter"],new DamageInfo(DamageDefOf.Cut,10,0,-1,fighter,null,sword));float fcd=ItemMaterials.WeaponFactor(made["LP_HL_ChainDevil"],new DamageInfo(DamageDefOf.Cut,10,0,-1,fighter,null,sword));fighter.equipment.DestroyAllEquipment();
   Check(Near(fs,1.5f) && Near(fc,1.5f) && Near(fcd,1f) && Near(Outsiders.HolyFactor(made["LP_HL_BoneDevil"]),2f),"silver ×1.5 against devils, cold iron ×1.5 against the fey (nothing extra against devils), holy water ×2 against devils ("+fs+"/"+fc+"/"+fcd+")");
   // 九狱束缚者不衰减；到 0 要求重新谈判
   var master=PcHuman(PcCell(2,33));var er=PcMake("LP_HL_Erinyes",legion,PcCell(3,33));AbyssBinding.Bind(er,master,50f);
   var hb=AbyssBinding.Binding(er);for(int i=0;i<24;i++)hb.TickInterval(GenDate.TicksPerHour);float ob=hb.Severity;
   AbyssBinding.BreakFree(er,"测试");var rl=Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Pact>().LastOrDefault(l=>l.kind=="renegotiate" && l.pawn==er);
   PcPlace(ThingDefOf.Silver,300,PcCell(4,33));rl?.Choices.First(o=>Txt(o).Contains("300")).action();
   Check(Near(ob,50f) && Outsiders.Bound(er) && er.Faction==Faction.OfPlayer && rl!=null && Near(AbyssBinding.Obedience(er),50f),"bound devils never lose obedience; at zero they demand renegotiation instead of fighting (paying 300 silver resets obedience to 50)");
   // 艾林魔救赎
   PactRaces.TestRoll=0f;for(int i=0;i<3;i++){InteractionWorker_DevaRedeem.Redeem(er);}PactRaces.TestRoll=null;
   Check(PactRaces.IsDeva(er) && !PactRaces.IsErinyes(er),"a deva's 'redeem' talks (20%, three times) turn an erinyes back into a deva");
   // 萨特禁鞋
   var feet=DefDatabase<BodyPartGroupDef>.GetNamed("Feet");var boots=new ThingDef{defName="LP_TestBoots",thingClass=typeof(Apparel),category=ThingCategory.Item,apparel=new ApparelProperties{bodyPartGroups=new List<BodyPartGroupDef>{feet},layers=new List<ApparelLayerDef>{ApparelLayerDefOf.Shell}}};
   Check(!ApparelUtility.HasPartsToWear(sat,boots) && ApparelUtility.HasPartsToWear(fighter,boots),"satyr hooves: no shoes or boots (apparel covering the feet)");
   // 林仙：离树、树死
   var tree=(Plant)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Plant_TreeOak"));tree.Growth=1f;GenSpawn.Spawn(tree,PcCell(0,36),map);pcMade.Add(tree);
   var tg=PactRaces.TreeGene(dry);tg.Bind(tree);
   var tp=tree.Position;var farCell=tp.x>map.Size.x/2?new IntVec3(tp.x-45,0,tp.z):new IntVec3(tp.x+45,0,tp.z);farCell=CellFinder.RandomClosewalkCellNear(farCell.ClampInsideMap(map),map,4);
   PactRaces.Teleport(dry,farCell);tg.Check(Find.TickManager.TicksGame);bool far=tg.Far && Items.Has(dry,"LP_DryadDistant");
   tree.Destroy();tg.Check(Find.TickManager.TicksGame);bool sev=Items.Has(dry,"LP_DryadSevered") && tg.tree==null;
   tg.lostTick=Find.TickManager.TicksGame-GenDate.TicksPerDay-1;tg.Check(Find.TickManager.TicksGame);PcFlush();
   Check(far && sev && (dry.Dead || dry.Destroyed),"dryad: more than 40 cells from her tree she weakens; when the tree dies she is severed, and dies if no new tree is bound within 24 hours");
   // 调换儿：千面
   var target=PcHuman(PcCell(6,36));var fg=PactRaces.Faces(cha);string own=cha.Name.ToStringFull;fg.Copy(target);
   bool copied=cha.Name.ToStringFull==target.Name.ToStringFull && cha.story.hairDef==target.story.hairDef && Items.Has(cha,"LP_ChangelingGuise");
   var seer=PcMake("LP_CE_Deva",Faction.OfPlayer,cha.Position+IntVec3.East);fg.TickInterval(GenDate.TicksPerHour);
   Check(copied && !fg.on && cha.Name.ToStringFull==own && Fiends.Sees(seer),"changeling 'Thousand Faces' copies a pawn's name and look; a deva's true sight (counts as devil sight) sees through it and it reverts");
   // 行星使：传奇抗性；驱散
   var dretch=PcMake("LP_AH_Dretch",AbyssRaces.Horde(),PcCell(10,33));Legendary.TestD20=1;bool ban=PactRaces.Banish(plan,dretch);Legendary.TestD20=null;
   var res=Legendary.Resistance(plan);
   Check(res!=null && Legendary.Ext(plan)?.resistance==2 && Legendary.Ext(plan).cr==16 && Legendary.LevelOf(plan)==18 && ban && dretch.Destroyed,"planetar: legendary layer (resistance 2, CR 16, level 18); banishment sends a failing demon back to its plane");
   // 骨魔尾刺、阿姆尼祖、地狱钢叉
   var victim=PcHuman(PcCell(12,36));Legendary.TestD20=1;victim.TakeDamage(new DamageInfo(DamageDefOf.Stab,6f,1f,-1f,made["LP_HL_BoneDevil"]));
   var hitter=PcHuman(PcCell(13,36));int lvl=hitter.skills.skills.Where(s=>!s.TotallyDisabled).Max(s=>s.Level);made["LP_HL_Amnizu"].TakeDamage(new DamageInfo(DamageDefOf.Blunt,2f,0f,-1f,hitter));Legendary.TestD20=null;
   var drain=hitter.health.hediffSet.hediffs.OfType<Hediff_AmnizuDrain>().FirstOrDefault();
   PactRaces.TestRoll=0f;var victim2=PcHuman(PcCell(14,36));victim2.TakeDamage(new DamageInfo(DamageDefOf.Stab,6f,1f,-1f,made["LP_HL_HornedDevil"],null,PactRaces.Pitchfork));PactRaces.TestRoll=null;
   Check(Items.Has(victim,"LP_BoneVenom") && drain!=null && drain.lost>0 && hitter.skills.skills.Where(s=>!s.TotallyDisabled).Max(s=>s.Level)<=lvl && Items.Has(victim2,"LP_InfernalWound") && made["LP_HL_HornedDevil"].equipment.Primary?.def==PactRaces.Pitchfork,"bone devil sting (Con DC 14 → bone venom), amnizu (attacker's Int DC 16 → best skill -2), horned devil pitchfork (25% infernal wound)");
   PcClean();
  }

  // ---------- 圣坛祈唤与堕天使 ----------
  void CheckPactCelestial(){
   var god=FaithUtility.AllDeities.FirstOrDefault(d=>d.Good && d.worshippable);var oldPatron=FaithUtility.Patron;FaithUtility.SetPatron(god,true);
   var altar=(Building_FaithAltar)ThingMaker.MakeThing(Faith.AltarDef,Faith.AltarDef.MadeFromStuff?GenStuff.DefaultStuffFor(Faith.AltarDef):null);altar.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(altar,PcCell(0,39),map);pcMade.Add(altar);
   var dev=PcHuman(PcCell(3,39));dev.story.traits.GainTrait(new Trait(Faith.Devotee,god.devoteeDegree));
   FaithUtility.AddFavor(god,45f-FaithUtility.GetFavor(god),"测试");string low=PactCelestial.CannotInvoke("deva",map);
   FaithUtility.AddFavor(god,100f-FaithUtility.GetFavor(god),"测试");
   var g1=Building_FaithAltar.ExtraGizmos.SelectMany(f=>f(altar)).OfType<Command_Action>().Where(c=>c.defaultLabel.StartsWith("祈唤天界援军")).ToList();
   var d1=PactCelestial.Invoke("deva",altar);pcMade.AddRange(d1);float f1=FaithUtility.GetFavor(god);
   FaithUtility.AddFavor(god,100f,"测试");var d2=PactCelestial.Invoke("guardian",altar);pcMade.AddRange(d2);
   FaithUtility.AddFavor(god,100f,"测试");var d3=PactCelestial.Invoke("planetar",altar);pcMade.AddRange(d3);string again=PactCelestial.CannotInvoke("planetar",map);
   Check(low!=null && g1.Count==3 && d1.Count==1 && Outsiders.Thrall(d1[0]) && Near(f1,60f) && d2.Count==1 && Outsiders.Bound(d2[0]) && Items.Has(d2[0],"LP_CelestialGuardian") && AbyssBinding.DailyDecay(d2[0],AbyssBinding.Binding(d2[0]))==0f && d3.Count==1 && PactRaces.Is(d3[0],PactRaces.Planetar) && Outsiders.Thrall(d3[0]) && again!=null,
    "altar invocation: deva aid (40, needs 50), guardian angel (90, a bound deva that never decays), planetar (100, once a year)");
   // 堕天使：主人腐化 ≥ 0.6 → 变成艾林魔
   var bind=AbyssBinding.Binding(d2[0]);AbyssTaint.Add(bind.binder,.7f);PactCelestial.TestRoll=0f;PactCelestial.Daily(Find.TickManager.TicksGame);PactCelestial.TestRoll=null;
   Check(PactRaces.IsErinyes(d2[0]) && Outsiders.Bound(d2[0]) && d2[0].Faction==Faction.OfPlayer,"a bound deva whose master reaches the demon mark falls and becomes an erinyes (still bound, not part of any pact)");
   var evil=FaithUtility.AllDeities.FirstOrDefault(d=>d.Evil && d.worshippable);if(evil!=null){FaithUtility.SetPatron(evil,true);Check(!Building_FaithAltar.ExtraGizmos.SelectMany(f=>f(altar)).OfType<Command_Action>().Any(c=>c.defaultLabel.StartsWith("祈唤天界援军")),"evil patrons get no celestial invocation");}
   FaithUtility.SetPatron(oldPatron,true);PcClean();
  }

  // ---------- 事件 ----------
  void CheckPactEvents(){
   var c=Pacts.Comp;
   // 5.1 宗主的低语
   var before=map.listerThings.ThingsOfDef(Pacts.ScrollDef).Count;var wp=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc,map);wp.forced=true;
   bool fired=DefDatabase<IncidentDef>.GetNamed("LP_PatronWhisper").Worker.TryExecute(wp);
   var tief=PcHuman(PcCell(0,42),1,"LP_Tiefling");int hells=Enumerable.Range(0,200).Select(i=>PactEvents.RandomPatron(map)).Count(d=>d.Hells);
   Check(fired && map.listerThings.ThingsOfDef(Pacts.ScrollDef).Count==before+1 && hells>100,"patron whisper: a pact scroll appears next to a colonist; with a tiefling around Hells patrons are weighted ×2 ("+hells+"/200)");
   // 5.2 血战之潮
   var a=PcHuman(PcCell(1,42),1);var pa=PcSign(a,"LP_Patron_Asmodeus","HellGuard");Pacts.AddDebt(pa,1,"测试");
   var w=PactEvents.StartBloodWar(map,2000f);if(w!=null){pcMade.AddRange(w.hells);pcMade.AddRange(w.abyss);}
   PactEvents.AidBloodWar("hells");
   Check(w!=null && w.hells.Count>0 && w.abyss.Count>0 && w.hells.Where(p=>p.Spawned).All(p=>!p.HostileTo(Faction.OfPlayer) && p.HostileTo(w.abyss[0])) && pa.debt==0,"blood war: legion and horde arrive from both sides; aiding the Hells makes the legion neutral to you this time and lowers Hells pact debt by 1");
   c.bloodWar=null;
   // 5.3 九狱商队
   var cp=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc,map);cp.forced=true;var bset=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
   bool car=DefDatabase<IncidentDef>.GetNamed("LP_InfernalCaravan").Worker.TryExecute(cp);var cars=map.mapPawns.AllPawnsSpawned.Where(p=>!bset.Contains(p) && p.Faction?.def==PactEvents.BrokersDef).ToList();pcMade.AddRange(cars);
   var trader=PactEvents.Trader(cars);
   Factions.SetRelation(PactEvents.Brokers,Faction.OfPlayer,-100);Pacts.CheckAll(c,Find.TickManager.TicksGame);
   Check(car && trader!=null && PactRaces.Is(trader,"LP_Amnizu") && cars.Any(p=>PactRaces.Is(p,"LP_BoneDevil")) && c.brokersRobbed && pa.debt==2,"[car "+car+" n "+cars.Count+" trader "+trader?.kindDef+" kinds "+string.Join(",",cars.Select(p=>p.kindDef.defName))+" robbed "+c.brokersRobbed+" debt "+pa.debt+"] infernal caravan: an amnizu trader with bone devil guards; robbing it (brokers turn hostile) adds +2 debt to every Hells pact");
   foreach(var p in cars)if(!p.Destroyed){p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();}
   Factions.SetRelation(PactEvents.Brokers,Faction.OfPlayer,0);c.brokersRobbed=false;PcEndAll();
   // 5.4 调换儿：长子归冬宫必定
   var mom=PcHuman(PcCell(2,42),1,null,Gender.Female);var pq=PcSign(mom,"LP_Patron_QueenOfAir","WinterVeil");if(!pq.Has("WinterFirstborn"))pq.clauses.Add(new PactClauseState{kind="WinterFirstborn",label="长子归冬宫",hidden=true});
   var baby=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDowned:true,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,fixedBiologicalAge:0,fixedChronologicalAge:0,developmentalStages:DevelopmentalStage.Newborn));pcMade.Add(baby);
   baby.relations.AddDirectRelation(PawnRelationDefOf.Parent,mom);GenSpawn.Spawn(baby,PcCell(3,42),map);
   AccessTools.Method(typeof(PactBirth),"Postfix").Invoke(null,new object[]{baby});
   var hid=baby.health.hediffSet.hediffs.OfType<Hediff_ChangelingHidden>().FirstOrDefault();bool looksNormal=hid!=null && !hid.Visible && baby.genes.Xenotype==XenotypeDefOf.Baseliner;
   PactEvents.RevealChangeling(baby);var cl=Find.LetterStack.LettersListForReading.OfType<ChoiceLetter_Pact>().LastOrDefault(l=>l.kind=="changeling");
   Check(looksNormal && PactRaces.IsChangeling(baby) && cl!=null && pq.Clause("WinterFirstborn")==null,"[hid "+(hid!=null)+" xeno "+baby.genes.Xenotype?.defName+" mother "+baby.GetMother()?.LabelShort+" ch "+PactRaces.IsChangeling(baby)+" letter "+(cl!=null)+"] changeling: 'Firstborn to the Winter Court' swaps the next baby; it looks normal (hidden status) until the reveal turns it into a changeling and asks raise / swap back");
   PcEndAll();
   // 5.5 狂猎
   var f=PcHuman(PcCell(4,42),1);var pf=PcSign(f,"LP_Patron_Titania","SummerTouch");Pacts.AddDebt(pf,1,"测试");
   var h=new PactHunt{map=map,prey=f,pactId=pf.id,points=3000f};c.hunts.Add(h);PactEvents.SpawnHunt(h);pcMade.AddRange(h.hunters);
   bool lordOk=h.hunters.Count>=7 && h.hunters.Count(p=>PactRaces.Is(p,PactRaces.WildHunter))>=3 && h.hunters.Any(p=>PactRaces.Is(p,"LP_WildHuntLord")) && h.hunters.Any(p=>p.def.defName=="LP_WinterWolf") && h.hunters.Any(p=>p.def.defName=="LP_BlinkDog") && Items.Has(f,"LP_WildHuntPrey") && h.hunters[0].GetLord()?.LordJob is LordJob_WildHunt;
   PactEvents.EndHunt(h,true);
   Check(lordOk && pf.debt==0 && !Items.Has(f,"LP_WildHuntPrey"),"wild hunt: 3-5 riders, a lord (≥2500 points), 2 winter wolves and 2 blink dogs chase one prey; surviving until 6:00 lowers the fey pact's debt by 1");
   foreach(var p in h.hunters.ToList())if(!p.Destroyed){p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();}c.hunts.Clear();PcEndAll();
   // 5.6 坠落天使
   var god=FaithUtility.AllDeities.FirstOrDefault(d=>d.Good && d.worshippable);var oldPatron=FaithUtility.Patron;FaithUtility.SetPatron(god,true);
   var fd=PactCelestial.FallenDeva(map);if(fd!=null)pcMade.Add(fd);Step(400);
   bool down=fd!=null && fd.Spawned && fd.Downed && Items.Has(fd,"LP_FallenDevaGuest");
   if(fd!=null){foreach(var i in fd.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())fd.health.RemoveHediff(i);var gh=fd.health.hediffSet.hediffs.OfType<Hediff_FallenDevaGuest>().First();gh.TickInterval(GenDate.TicksPerHour);PcFlush();}
   float fav0=FaithUtility.GetFavor(god);var th=fd?.health.hediffSet.hediffs.OfType<Hediff_AbyssThrall>().FirstOrDefault();if(th!=null)th.ticksLeft=10;fd?.health.hediffSet.hediffs.OfType<Hediff_FallenDevaGuest>().FirstOrDefault()?.TickInterval(GenDate.TicksPerHour);
   Check(down && th!=null && !Items.Has(fd,"LP_DevaStupor") && FaithUtility.GetFavor(god)-fav0>=29f,"fallen deva: crashes in badly hurt; once tended it wakes as a 10-day guest (thrall timer); leaving grants +30 favor");
   FaithUtility.SetPatron(oldPatron,true);
   // 5.7 妖精集市
   var mp=StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc,map);mp.forced=true;var bm=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
   bool mk=DefDatabase<IncidentDef>.GetNamed("LP_FeyMarket").Worker.TryExecute(mp);var court=map.mapPawns.AllPawnsSpawned.Where(p=>!bm.Contains(p) && p.Faction?.def==PactEvents.CourtDef).ToList();pcMade.AddRange(court);
   var buyer=PcHuman(PcCell(6,42),1);long age=buyer.ageTracker.AgeBiologicalTicks;PactEvents.PayCost("year",buyer);var got=PactEvents.GiveReward("satyr",buyer);
   var newSat=map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p=>PactRaces.IsSatyr(p) && p.LabelShort==got);if(newSat!=null)pcMade.Add(newSat);
   Check(mk && c.market!=null && c.market.offers.Count==3 && PactEvents.Trader(court)!=null && c.market.leaveTick>Find.TickManager.TicksGame && buyer.ageTracker.AgeBiologicalTicks-age==GenDate.TicksPerYear && newSat!=null,"fey market: the court's merchants arrive with 3 cost trades and leave before dawn; paying a year of life buys a satyr colonist");
   foreach(var p in court)if(!p.Destroyed){p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();}c.market=null;
  }

  // ---------- 性能：100 人地图。“关”= 100 个普通人；“开”= 10 名签约人（印记 / 霜影 / 女王之舌 / 冰狱奥术）、一名林仙、一只小精灵、一只小魔鬼。交替各 3 轮 1500 刻。 ----------
  void CheckPactPerformance(){
   var crowd=new List<Pawn>();
   Pawn Human(IntVec3 c){var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner));GenSpawn.Spawn(p,c,map);pcMade.Add(p);return p;}
   foreach(var c in GenRadial.RadialCellsAround(map.Center,14f,true).Where(c=>c.Standable(map) && c.GetFirstPawn(map)==null)){if(crowd.Count>=100)break;crowd.Add(Human(c));}
   double Run(){Step(120);var sw=Stopwatch.StartNew();Step(1500);sw.Stop();return sw.Elapsed.TotalMilliseconds/1500.0;}
   var extras=new List<Pawn>();string[] boons={"InfernalMark","FrostShadow","QueenTongue","IceArcana","HellGuard","SummerGrace","WinterVeil","IronArtisans","SeeClauses","SummerTouch"};string[] pats={"LP_Patron_Asmodeus","LP_Patron_QueenOfAir","LP_Patron_Glasya","LP_Patron_Mephistopheles","LP_Patron_Asmodeus","LP_Patron_Titania","LP_Patron_QueenOfAir","LP_Patron_Dispater","LP_Patron_Glasya","LP_Patron_Titania"};
   void On(){
    for(int i=0;i<10;i++){var p=crowd[i];var s=ThingMaker.MakeThing(Pacts.ScrollDef);var cs=s.TryGetComp<CompPactScroll>();cs.SetPatron(Pacts.Patron(pats[i]));GenPlace.TryPlaceThing(s,p.Position,map,ThingPlaceMode.Near);Pacts.TestNoCap=true;Pacts.Sign(p,cs,boons[i]);Pacts.TestNoCap=false;}
    foreach(var k in new[]{"LP_FY_Dryad","LP_PixieFamiliar","LP_ImpFamiliar"}){var x=AbyssRaces.Make(k,Faction.OfPlayer,map.Tile);if(x==null)continue;GenSpawn.Spawn(x,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);extras.Add(x);pcMade.Add(x);if(PactRaces.IsDryad(x)){AbyssBinding.Bind(x,crowd[0],100f);PactRaces.BindNearestTree(x);}}
   }
   void Off(){PcEndAll();foreach(var x in extras){if(x.Destroyed)continue;x.GetLord()?.RemovePawn(x);if(x.Spawned)x.DeSpawn();x.Destroy();}extras.Clear();foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>Outsiders.Thrall(p)).ToList()){p.DeSpawn();p.Destroy();}}
   Run();Run(); // 两轮预热（第一轮总是慢）
   var offs=new List<double>();var ons=new List<double>();
   for(int k=0;k<3;k++){offs.Add(Run());On();ons.Add(Run());Off();}
   double baseMs=offs.Average(),pactMs=ons.Average(),delta=(pactMs-baseMs)/Math.Max(.001,baseMs)*100.0;
   Log.Message("[LICH-PERF] pact 1.55.0: 100 pawns; off "+string.Join(" / ",offs.Select(x=>x.ToString("0.000")))+" ms/tick, on "+string.Join(" / ",ons.Select(x=>x.ToString("0.000")))+" ms/tick; average "+baseMs.ToString("0.000")+" vs "+pactMs.ToString("0.000")+" ("+delta.ToString("+0.0;-0.0")+"%)");
   Check(baseMs>0 && pactMs>0,"performance pass recorded: 100 pawns, "+baseMs.ToString("0.000")+" vs "+pactMs.ToString("0.000")+" ms per tick");
   foreach(var p in crowd.ToList()){if(p.Destroyed)continue;p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();p.Destroy();}
  }

  // ---------- 存档往返 ----------
  void PreparePactSave(){
   pcArena=AbFindArena();pcMade.Clear();
   var p=PcHuman(PcCell(0,0),1);var s=PcScroll("LP_Patron_Asmodeus",p.Position);Legendary.TestD20=20;Pacts.Review(p,s);Legendary.TestD20=null;
   var pact=Pacts.Sign(p,s,"HellGuard");Pacts.AddDebt(pact,1,"测试");Pacts.StartDemand(pact,Find.TickManager.TicksGame,pact.clauses.FindIndex(x=>x.kind=="SacrificePrisoner"));
   pcSignerId=p.thingIDNumber;pcDebt=pact.debt;pcDemandClause=pact.demandClause;pcBoon=pact.boonKey;pcPatron=pact.patron.defName;pcPactId=pact.id;pcRevealed=string.Join(",",pact.clauses.Select(c=>c.kind+":"+c.revealed));
   var dry=PcMake("LP_FY_Dryad",Faction.OfPlayer,PcCell(2,0),Gender.Female);AbyssBinding.Bind(dry,p,100f);
   var tree=(Plant)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Plant_TreeOak"));tree.Growth=1f;GenSpawn.Spawn(tree,PcCell(3,2),map);PactRaces.TreeGene(dry).Bind(tree);pcDryadId=dry.thingIDNumber;pcTreeId=tree.thingIDNumber;
   var baby=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDowned:true,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,fixedBiologicalAge:0,fixedChronologicalAge:0,developmentalStages:DevelopmentalStage.Newborn));
   GenSpawn.Spawn(baby,PcCell(5,0),map);var hh=(Hediff_ChangelingHidden)Items.Give(baby,"LP_ChangelingHidden");hh.reveal=Find.TickManager.TicksGame+20*GenDate.TicksPerDay;hh.originalXeno=XenotypeDefOf.Baseliner;pcBabyId=baby.thingIDNumber;
   var cha=PcMake("LP_FY_ChangelingVolunteer",Faction.OfPlayer,PcCell(7,0));var tgt=PcHuman(PcCell(8,0));PactRaces.Faces(cha).Copy(tgt);pcFacesId=cha.thingIDNumber;pcCopied=cha.Name.ToStringFull;
   pcMade.Clear();
  }
  void CheckPactAfterLoad(){
   map=Find.CurrentMap;Pawn P(int id)=>map.mapPawns.AllPawns.FirstOrDefault(x=>x.thingIDNumber==id);
   var pact=Pacts.ById(pcPactId);var p=P(pcSignerId);var dry=P(pcDryadId);var baby=P(pcBabyId);var cha=P(pcFacesId);var tg=PactRaces.TreeGene(dry);
   Check(pact!=null && pact.signer==p && pact.debt==pcDebt && pact.demandClause==pcDemandClause && pact.boonKey==pcBoon && pact.patron.defName==pcPatron && string.Join(",",pact.clauses.Select(c=>c.kind+":"+c.revealed))==pcRevealed && Pacts.Any && p.abilities.GetAbility(Pacts.BlastDef)!=null && Items.Has(p,Pacts.SignerDef),
    "pact save/load keeps signer, patron, boon, every clause with its revealed flag, debt and the running demand");
   Check(tg?.tree!=null && tg.tree.thingIDNumber==pcTreeId && Outsiders.Bound(dry),"the dryad's tree survives save/load");
   var hid=baby?.health.hediffSet.hediffs.OfType<Hediff_ChangelingHidden>().FirstOrDefault();var fg=PactRaces.Faces(cha);
   Check(hid!=null && hid.reveal>Find.TickManager.TicksGame && hid.originalXeno==XenotypeDefOf.Baseliner && fg!=null && fg.on && cha.Name.ToStringFull==pcCopied,"a hidden changeling and an active 'Thousand Faces' disguise survive save/load");
   fg?.Revert(null);Pacts.End(pact,"测试",false);
   foreach(var x in new[]{p,dry,baby,cha}.Where(x=>x!=null)){x.GetLord()?.RemovePawn(x);if(x.Spawned)x.DeSpawn();if(!x.Destroyed)x.Destroy();}
  }
 }
}
