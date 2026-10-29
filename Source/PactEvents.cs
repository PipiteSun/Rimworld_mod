using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.55.0 契约事件（规格第五节，Defs/Pact_Events.xml）：宗主的低语、血战之潮、九狱商队、调换儿、狂猎、坠落天使、妖精集市；讨债小队；派系。
 public class PactHunt : IExposable {
  public Map map;public Pawn prey;public int pactId=-1,spawnAt=-1,started=-1;public float points;public List<Pawn> hunters=new List<Pawn>();public bool done;
  public void ExposeData(){Scribe_References.Look(ref map,"map");Scribe_References.Look(ref prey,"prey");Scribe_Values.Look(ref pactId,"pact",-1);Scribe_Values.Look(ref spawnAt,"spawnAt",-1);Scribe_Values.Look(ref started,"started",-1);Scribe_Values.Look(ref points,"points");Scribe_Collections.Look(ref hunters,"hunters",LookMode.Reference);Scribe_Values.Look(ref done,"done");if(Scribe.mode==LoadSaveMode.PostLoadInit)hunters=hunters?.Where(h=>h!=null).ToList()??new List<Pawn>();}
 }
 public class PactMarket : IExposable {
  public Map map;public int leaveTick=-1;public List<Pawn> pawns=new List<Pawn>();public List<string> offers=new List<string>();public List<int> used=new List<int>();
  public void ExposeData(){Scribe_References.Look(ref map,"map");Scribe_Values.Look(ref leaveTick,"leave",-1);Scribe_Collections.Look(ref pawns,"pawns",LookMode.Reference);Scribe_Collections.Look(ref offers,"offers",LookMode.Value);Scribe_Collections.Look(ref used,"used",LookMode.Value);if(Scribe.mode==LoadSaveMode.PostLoadInit){pawns=pawns?.Where(p=>p!=null).ToList()??new List<Pawn>();offers=offers??new List<string>();used=used??new List<int>();}}
 }
 public class PactBloodWar : IExposable {
  public Map map;public List<Pawn> hells=new List<Pawn>(),abyss=new List<Pawn>();public string aided;public int start=-1;public bool leaving;
  public void ExposeData(){Scribe_References.Look(ref map,"map");Scribe_Collections.Look(ref hells,"hells",LookMode.Reference);Scribe_Collections.Look(ref abyss,"abyss",LookMode.Reference);Scribe_Values.Look(ref aided,"aided");Scribe_Values.Look(ref start,"start",-1);Scribe_Values.Look(ref leaving,"leaving");if(Scribe.mode==LoadSaveMode.PostLoadInit){hells=hells?.Where(p=>p!=null).ToList()??new List<Pawn>();abyss=abyss?.Where(p=>p!=null).ToList()??new List<Pawn>();}}
 }

 public static class PactEvents {
  public const float WizardScrollChance=.03f,WhisperLecternFactor=3f,BloodWarFactor=3f,BloodWarShare=.6f,HuntLordPoints=2500f;
  public const int HuntHour=22,HuntEndHour=6,MarketDay=6,MarketHour=20,MarketStay=GenDate.TicksPerHour*9,MarketEarliestDay=25;
  static FactionDef legion,hunt,brokers,court,truce;
  public static FactionDef LegionDef=>legion??(legion=DefDatabase<FactionDef>.GetNamedSilentFail("LP_HellsLegion"));
  public static FactionDef HuntDef=>hunt??(hunt=DefDatabase<FactionDef>.GetNamedSilentFail("LP_FeyHunt"));
  public static FactionDef BrokersDef=>brokers??(brokers=DefDatabase<FactionDef>.GetNamedSilentFail("LP_HellsBrokers"));
  public static FactionDef CourtDef=>court??(court=DefDatabase<FactionDef>.GetNamedSilentFail("LP_FeyCourt"));
  public static FactionDef TruceDef=>truce??(truce=DefDatabase<FactionDef>.GetNamedSilentFail("LP_PlanarTruce"));
  public static Faction Brokers=>BrokersDef==null?null:Find.FactionManager?.FirstFactionOfDef(BrokersDef);
  // 永久敌对的两个隐藏派系：九狱军团、狂猎（和深渊魔群互相敌对）。
  public static Faction Legion(){var f=Factions.EnsureHidden(LegionDef);if(f!=null){Hostile(f,Faction.OfPlayer);Hostile(f,AbyssRaces.Horde());}return f;}
  public static Faction FeyHunt(){var f=Factions.EnsureHidden(HuntDef);if(f!=null){Hostile(f,Faction.OfPlayer);Hostile(f,AbyssRaces.Horde());}return f;}
  // 中立的：九狱掮客（抢劫后敌对）、妖精宫廷、临时停战（血战之潮里援助的一方）。
  public static Faction BrokersEnsure(){var f=Factions.EnsureHidden(BrokersDef);if(f!=null && Pacts.Comp?.brokersRobbed!=true && f.HostileTo(Faction.OfPlayer))Factions.SetRelation(f,Faction.OfPlayer,0);return f;}
  public static Faction Court(){var f=Factions.EnsureHidden(CourtDef);if(f!=null && f.HostileTo(Faction.OfPlayer))Factions.SetRelation(f,Faction.OfPlayer,0);return f;}
  public static Faction Truce(){var f=Factions.EnsureHidden(TruceDef);if(f!=null){Factions.SetRelation(f,Faction.OfPlayer,0);Hostile(f,AbyssRaces.Horde());Hostile(f,Legion());}return f;}
  static void Hostile(Faction a,Faction b){if(a!=null && b!=null && a!=b && !a.HostileTo(b))Factions.SetRelation(a,b,-100);}

  // ---------- 宗主加权（5.1） ----------
  public static LP_PatronDef RandomPatron(Map map){
   var c=Pacts.Comp;int now=Pacts.Now;var colonists=map?.mapPawns.FreeColonistsSpawned??Enumerable.Empty<Pawn>();
   bool tief=colonists.Any(p=>FaithRaces.IsTiefling(p) || AbyssRaces.IsCambion(p)),fey=colonists.Any(p=>Items.IsElf(p) || PactRaces.IsSatyr(p));
   bool taint=colonists.Sum(p=>AbyssTaint.Severity(p))>=1f;
   var list=Pacts.Patrons.Where(d=>c==null || !c.patronCooldown.TryGetValue(d.defName,out int until) || now>=until).ToList();if(list.Count==0)list=Pacts.Patrons.ToList();
   return list.RandomElementByWeightWithFallback(d=>(d.Hells?(tief?2f:1f)*(taint?1.5f:1f):1f)*(d.Fey?(fey?2f:1f):1f));
  }
  public static Thing SpawnScroll(Pawn near,LP_PatronDef d){
   if(Pacts.ScrollDef==null || near?.MapHeld==null)return null;var t=ThingMaker.MakeThing(Pacts.ScrollDef);t.TryGetComp<CompPactScroll>()?.SetPatron(d);
   GenPlace.TryPlaceThing(t,near.PositionHeld,near.MapHeld,ThingPlaceMode.Near);return t;
  }

  // ---------- 讨债小队（九狱，债务 2） ----------
  static readonly (string kind,float cp)[] Devils={("LP_HL_ChainDevil",150f),("LP_HL_BoneDevil",200f),("LP_HL_HornedDevil",260f)};
  public static List<Pawn> SpawnCollectors(Map map,PlanarPact p,float points){
   var f=Legion();if(f==null || map==null)return new List<Pawn>();
   if(!RCellFinder.TryFindRandomPawnEntryCell(out var cell,map,CellFinder.EdgeRoadChance_Hostile))return new List<Pawn>();
   var made=new List<Pawn>();var lead=AbyssRaces.Make("LP_HL_CollectorLeader",f,map.Tile);
   if(lead!=null){AbyssBinding.SetLevel(lead,14,false);var h=(Hediff_PatronAgent)Items.Give(lead,"LP_PatronAgent");if(h!=null)h.patron=p.patron;made.Add(lead);points-=400f;}
   int n=0;while((points>0f || n<2) && n<12){var d=Devils.RandomElement();var x=AbyssRaces.Make(d.kind,f,map.Tile);if(x!=null)made.Add(x);points-=d.cp;n++;}
   foreach(var x in made)GenSpawn.Spawn(x,CellFinder.RandomClosewalkCellNear(cell,map,5),map);
   LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false,false,false),map,made);
   Find.LetterStack.ReceiveLetter("讨债者",p.patron.label+"派来了讨债的魔鬼："+made.Count+" 个，领头的是一名高阶角魔。它们要的是"+p.signer.LabelShort+"欠下的债。\n\n打倒领头的角魔会掉落宗主代理人的真名卷——拿到契约讲台上可以无代价地撕毁契约。",LetterDefOf.ThreatBig,new LookTargets(made.Cast<Thing>()));
   return made;
  }

  // ---------- 狂猎（5.5） ----------
  public static bool Night(Map m){int h=GenLocalDate.HourOfDay(m);return h>=HuntHour || h<4;}
  public static PactHunt StartWildHunt(Map map,Pawn prey,int pactId,float points,bool letter){
   var c=Pacts.Comp;if(c==null || map==null || prey==null)return null;
   var h=new PactHunt{map=map,prey=prey,pactId=pactId,points=Mathf.Max(points,600f)};c.hunts.Add(h);
   if(Night(map))SpawnHunt(h);else{h.spawnAt=Pacts.Now+AbyssEvents.HoursUntil(map,HuntHour)*GenDate.TicksPerHour;if(letter)Find.LetterStack.ReceiveLetter("号角声","远处的林子里响起了狩猎号角。今晚 22 点，狂猎会来追"+prey.LabelShort+"。",LetterDefOf.ThreatSmall,prey);}
   return h;
  }
  public static List<Pawn> SpawnHunt(PactHunt h){
   var map=h.map;var f=FeyHunt();h.spawnAt=-1;if(f==null || map==null || !Find.Maps.Contains(map) || h.prey==null || h.prey.Dead){h.done=true;return h.hunters;}
   if(!RCellFinder.TryFindRandomPawnEntryCell(out var cell,map,CellFinder.EdgeRoadChance_Hostile)){h.done=true;return h.hunters;}
   int riders=Mathf.Clamp(3+Mathf.FloorToInt(h.points/1500f),3,5);
   var kinds=new List<string>();for(int i=0;i<riders;i++)kinds.Add("LP_FW_WildHunter");if(h.points>=HuntLordPoints)kinds.Add("LP_FW_WildHuntLord");
   kinds.AddRange(new[]{"LP_WinterWolf","LP_WinterWolf","LP_BlinkDog","LP_BlinkDog"});
   foreach(var k in kinds){var p=AbyssRaces.Make(k,f,map.Tile);if(p==null)continue;GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(cell,map,5),map);h.hunters.Add(p);if(k=="LP_FW_WildHuntLord"){var a=(Hediff_PatronAgent)Items.Give(p,"LP_PatronAgent");var pact=Pacts.ById(h.pactId);if(a!=null)a.patron=pact?.patron??Pacts.Patrons.FirstOrDefault(d=>d.Fey);}}
   Items.Give(h.prey,"LP_WildHuntPrey");
   LordMaker.MakeNewLord(f,new LordJob_WildHunt(h.prey),map,h.hunters);h.started=Pacts.Now;
   Find.LetterStack.ReceiveLetter("狂猎","鹿角盔、冰雾和号角：狂猎冲进了地图！它们只追"+h.prey.LabelShort+"一个人——挡路的才会挨打。\n\n撑到早上 6 点，狂猎就会散去"+(h.pactId>=0?"（"+h.prey.LabelShort+"的妖精契约债务 -1）":"")+"；猎物死了它们立刻离开。骑手怕寒铁。",LetterDefOf.ThreatBig,new LookTargets(h.hunters.Cast<Thing>()));
   return h.hunters;
  }
  public static void EndHunt(PactHunt h,bool survived){
   h.done=true;if(h.prey!=null)Items.Remove(h.prey,"LP_WildHuntPrey");
   var left=h.hunters.Where(x=>x!=null && x.Spawned && !x.Dead && !x.Downed).ToList();
   if(left.Count>0 && h.map!=null){var f=left[0].Faction;foreach(var x in left)x.GetLord()?.RemovePawn(x);LordMaker.MakeNewLord(f,new LordJob_ExitMapBest(LocomotionUrgency.Jog,false,true),h.map,left);}
   if(survived){var p=Pacts.ById(h.pactId);if(p!=null && p.Active)Pacts.AddDebt(p,-1,"熬过了狂猎");Messages.Message("天亮了，狂猎散去。"+h.prey?.LabelShort+"活了下来。",MessageTypeDefOf.PositiveEvent,false);}
   else Messages.Message("狂猎带着猎物离开了。",MessageTypeDefOf.NegativeEvent,false);
  }

  // ---------- 每 250 刻 / 每小时 ----------
  public static void Tick250(GameComponent_Pacts c,int now){
   for(int i=c.hunts.Count-1;i>=0;i--){
    var h=c.hunts[i];if(h.done){c.hunts.RemoveAt(i);continue;}
    if(h.spawnAt>=0){if(now>=h.spawnAt)SpawnHunt(h);continue;}
    if(h.started<0)continue;
    if(h.prey==null || h.prey.Dead || h.prey.Destroyed){EndHunt(h,false);continue;}
    if(h.map==null || !Find.Maps.Contains(h.map) || !h.hunters.Any(x=>x!=null && x.Spawned && !x.Dead && !x.Downed)){h.done=true;Items.Remove(h.prey,"LP_WildHuntPrey");continue;}
    int hour=GenLocalDate.HourOfDay(h.map);if(hour>=HuntEndHour && hour<HuntEndHour+3 && now-h.started>GenDate.TicksPerHour)EndHunt(h,true);
   }
   if(c.market!=null){var m=c.market;if(m.map==null || !Find.Maps.Contains(m.map) || m.pawns.All(p=>p==null || !p.Spawned || p.Dead))c.market=null;else if(m.leaveTick>=0 && now>=m.leaveTick){m.leaveTick=-1;var left=m.pawns.Where(p=>p.Spawned && !p.Dead && !p.Downed).ToList();if(left.Count>0){var f=left[0].Faction;foreach(var p in left)p.GetLord()?.RemovePawn(p);LordMaker.MakeNewLord(f,new LordJob_ExitMapBest(LocomotionUrgency.Walk,false,true),m.map,left);}Messages.Message("天快亮了，妖精集市收摊离开。",MessageTypeDefOf.NeutralEvent,false);}}
   if(c.bloodWar!=null)TickBloodWar(c,now);
  }
  public static void Hourly(GameComponent_Pacts c,int now){
   if(!Pacts.Enabled)return;var map=Find.Maps.FirstOrDefault(m=>m.IsPlayerHome);if(map==null)return;
   int quadrum=GenDate.DaysPassed/15;
   if(GenDate.DaysPassed>=MarketEarliestDay && GenLocalDate.DayOfQuadrum(map)==MarketDay && GenLocalDate.HourOfDay(map)==MarketHour && c.lastMarketQuadrum!=quadrum && c.market==null){
    c.lastMarketQuadrum=quadrum;
    if(Pacts.Roll<.3f*Events2.Frequency){var def=DefDatabase<IncidentDef>.GetNamedSilentFail("LP_FeyMarket");if(def!=null){var parms=StorytellerUtility.DefaultParmsNow(def.category,map);parms.forced=true;def.Worker.TryExecute(parms);}}
   }
  }

  // ---------- 商队（九狱商队 / 妖精集市都借原版商队事件生成） ----------
  public static List<Pawn> SpawnCaravan(Map map,Faction f,TraderKindDef kind,float points){
   if(map==null || f==null)return new List<Pawn>();var before=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
   var parms=new IncidentParms{target=map,faction=f,traderKind=kind,points=Mathf.Max(500f,points),forced=true};
   try{IncidentDefOf.TraderCaravanArrival.Worker.TryExecute(parms);}catch(Exception e){Log.Warning("[DND边缘大陆] 商队生成失败："+e.Message);}
   return map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p) && p.Faction==f).ToList();
  }
  public static Pawn Trader(IEnumerable<Pawn> pawns)=>pawns.FirstOrDefault(p=>p.kindDef?.trader==true)??pawns.FirstOrDefault(p=>p.RaceProps.Humanlike);

  // ---------- 妖精集市的代价交易（首领右键 → 选择信件，3 个随机交易各一次） ----------
  public static readonly string[] Costs={"memory","year","name","laughter"};
  public static readonly string[] Rewards={"magic","pixie","unbind","satyr","ransom"};
  public static string CostLabel(string c){switch(c){case "memory":return "一段记忆（最高技能 -2 级）";case "year":return "一年寿命";case "name":return "一个名字";case "laughter":return "一份笑声（心情 -5，一季）";}return c;}
  public static string RewardLabel(string r){switch(r){case "magic":return "一件魔法物品";case "pixie":return "一只小精灵的友谊";case "unbind":return "解开一份妖精契约";case "satyr":return "一名萨特加入";case "ransom":return "赎回被拖进妖精荒野的人";}return r;}
  public static List<string> RollOffers(){var list=new List<string>();var rs=Rewards.InRandomOrder().Take(3).ToList();foreach(var r in rs)list.Add(Costs.RandomElement()+"|"+r);return list;}
  public static string CannotReward(string r,Pawn payer){
   var c=Pacts.Comp;
   switch(r){
    case "unbind":return c==null || !c.Active.Any(p=>p.patron.Fey)?"殖民地没有妖精契约":null;
    case "ransom":return c==null || !c.taken.Any(t=>t.plane==OutsiderPlane.Feywild)?"没有被拖进妖精荒野的人":null;
    case "magic":return MagicItemDefs().Any()?null:"没有魔法物品";
   }
   return null;
  }
  public static IEnumerable<ThingDef> MagicItemDefs()=>DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.tradeTags!=null && (d.tradeTags.Contains("LP_MagicItem") || d.tradeTags.Contains("LP_MagicWeapon")) && d.tradeability!=Tradeability.None && !d.IsBlueprint && !d.IsFrame);
  public static bool PayCost(string cost,Pawn p){
   if(p==null || p.Dead)return false;
   switch(cost){
    case "memory":{var s=p.skills?.skills?.Where(x=>!x.TotallyDisabled).OrderByDescending(x=>x.Level).FirstOrDefault();if(s!=null)s.Level=Mathf.Max(0,s.Level-2);break;}
    case "year":p.ageTracker.AgeBiologicalTicks+=GenDate.TicksPerYear;break;
    case "name":try{p.Name=PawnBioAndNameGenerator.GeneratePawnName(p,NameStyle.Full,null,false,p.genes?.Xenotype);}catch(Exception e){Log.Warning("[DND边缘大陆] 改名失败："+e.Message);}break;
    case "laughter":{var t=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_LaughterSold");if(t!=null)p.needs?.mood?.thoughts?.memories?.TryGainMemory(t);break;}
   }
   return true;
  }
  public static string GiveReward(string r,Pawn p){
   var map=p.MapHeld;var c=Pacts.Comp;
   switch(r){
    case "magic":{var d=MagicItemDefs().RandomElementWithFallback();if(d==null)return null;var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good,ArtGenerationContext.Outsider);GenPlace.TryPlaceThing(t,p.PositionHeld,map,ThingPlaceMode.Near);return t.LabelCap;}
    case "pixie":{var x=AbyssRaces.Make("LP_PixieFamiliar",Faction.OfPlayer,map.Tile);if(x==null)return null;GenSpawn.Spawn(x,CellFinder.RandomClosewalkCellNear(p.PositionHeld,map,2),map);var h=x.health.hediffSet.hediffs.OfType<Hediff_PixieNature>().FirstOrDefault();if(h!=null)h.master=p;if(p.relations!=null)p.relations.AddDirectRelation(PawnRelationDefOf.Bond,x);if(x.playerSettings!=null)x.playerSettings.Master=p;return x.LabelShort;}
    case "unbind":{var pact=c?.Active.FirstOrDefault(x=>x.patron.Fey);if(pact==null)return null;Pacts.Unbind(pact,"在妖精集市上换来了解约");return "解开了"+pact.signer.LabelShort+"的契约";}
    case "satyr":{var x=AbyssRaces.Make("LP_FY_Satyr",Faction.OfPlayer,map.Tile);if(x==null)return null;GenSpawn.Spawn(x,CellFinder.RandomClosewalkCellNear(p.PositionHeld,map,3),map);x.workSettings?.EnableAndInitialize();return x.LabelShort;}
    case "ransom":{var t=c?.taken.FirstOrDefault(x=>x.plane==OutsiderPlane.Feywild);if(t==null)return null;var back=Pacts.Ransom(t,map,false);return back?.LabelShort;}
   }
   return null;
  }

  // ---------- 血战之潮（5.2） ----------
  public static PactBloodWar StartBloodWar(Map map,float points){
   var c=Pacts.Comp;var legionF=Legion();var hordeF=AbyssRaces.Horde();if(c==null || legionF==null || hordeF==null)return null;
   if(!RCellFinder.TryFindRandomPawnEntryCell(out var a,map,CellFinder.EdgeRoadChance_Hostile))return null;
   var b=new IntVec3(map.Size.x-1-a.x,0,map.Size.z-1-a.z);b=CellFinder.RandomClosewalkCellNear(b.ClampInsideMap(map),map,8);
   var w=new PactBloodWar{map=map,start=Pacts.Now};
   float hp=points*BloodWarShare,ap=points*BloodWarShare;
   while(hp>0f && w.hells.Count<10){var d=Devils.RandomElement();var x=AbyssRaces.Make(d.kind,legionF,map.Tile);if(x==null)break;GenSpawn.Spawn(x,CellFinder.RandomClosewalkCellNear(a,map,5),map);w.hells.Add(x);hp-=d.cp;}
   var demons=new[]{("LP_AH_Dretch",35f),("LP_AH_Barlgura",140f),("LP_AH_Hezrou",180f)};
   while(ap>0f && w.abyss.Count<14){var d=demons.RandomElement();var x=AbyssRaces.Make(d.Item1,hordeF,map.Tile);if(x==null)break;GenSpawn.Spawn(x,CellFinder.RandomClosewalkCellNear(b,map,5),map);w.abyss.Add(x);ap-=d.Item2;}
   if(w.hells.Count>0)LordMaker.MakeNewLord(legionF,new LordJob_AssaultColony(legionF,false,false,false,false,false,false,false),map,w.hells);
   if(w.abyss.Count>0)LordMaker.MakeNewLord(hordeF,new LordJob_AssaultColony(hordeF,false,false,false,false,false,false,false),map,w.abyss);
   c.bloodWar=w;
   PactLetters.Send("bloodwar",null,null,null,"血战之潮","九狱军团（"+w.hells.Count+"）和深渊魔群（"+w.abyss.Count+"）从地图两侧同时杀了进来——血战打到了你的家门口。它们互相敌对，也都敌视你。\n\n· 旁观：谁都不帮。\n· 援助九狱：所有九狱契约债务 -1；深渊束缚者服从度 -20；九狱军团这一次对你中立，帮你打深渊。\n· 援助深渊：反过来，深渊魔群这一次对你中立；所有九狱契约债务 +1。\n\n中立的一方在另一方全灭后离开。");
   return w;
  }
  public static void AidBloodWar(string side){
   var c=Pacts.Comp;var w=c?.bloodWar;if(w==null || w.aided!=null)return;w.aided=side;var f=Truce();if(f==null)return;
   var pawns=(side=="hells"?w.hells:w.abyss).Where(p=>p!=null && p.Spawned && !p.Dead).ToList();
   foreach(var p in pawns){p.GetLord()?.RemovePawn(p);p.SetFaction(f);}
   if(pawns.Count>0)LordMaker.MakeNewLord(f,new LordJob_AssistColony(f,w.map.Center),w.map,pawns);
   if(side=="hells"){foreach(var p in c.Active.Where(x=>x.patron.Hells).ToList())Pacts.AddDebt(p,-1,"在血战之潮里援助了九狱");foreach(var b in AbyssBinding.PlayerPawns.Where(x=>Outsiders.Bound(x) && Outsiders.IsDemon(x)).ToList())AbyssBinding.Binding(b)?.Change(-20f,"殖民地帮九狱打深渊");}
   else foreach(var p in c.Active.Where(x=>x.patron.Hells).ToList())Pacts.AddDebt(p,1,"在血战之潮里援助了深渊");
  }
  static bool Alive(Pawn p)=>p!=null && p.Spawned && !p.Dead && !p.Downed;
  static void TickBloodWar(GameComponent_Pacts c,int now){
   var w=c.bloodWar;if(w.map==null || !Find.Maps.Contains(w.map)){c.bloodWar=null;return;}
   bool timeout=now-w.start>GenDate.TicksPerDay*2;
   if(w.aided==null){if(timeout || !w.hells.Any(Alive) && !w.abyss.Any(Alive))c.bloodWar=null;return;}
   var mine=w.aided=="hells"?w.hells:w.abyss;var other=w.aided=="hells"?w.abyss:w.hells;
   if(!w.leaving && (timeout || !other.Any(Alive))){
    w.leaving=true;var left=mine.Where(Alive).ToList();
    if(left.Count>0){var f=left[0].Faction;foreach(var p in left)p.GetLord()?.RemovePawn(p);LordMaker.MakeNewLord(f,new LordJob_ExitMapBest(LocomotionUrgency.Walk,false,true),w.map,left);}
    Messages.Message("血战之潮平息了：援助你的一方收兵离开。",MessageTypeDefOf.NeutralEvent,false);
   }
   if(w.leaving && !mine.Any(p=>p!=null && p.Spawned))c.bloodWar=null;
  }

  // ---------- 死亡 / 长子条款 ----------
  public static void OnDeath(Pawn p){
   var c=Pacts.Comp;if(c==null)return;
   foreach(var h in c.hunts)if(h.prey==p && !h.done && h.started>=0)EndHunt(h,false);
  }
  public static void HellsClaimDue(Pawn p){
   if(p==null || p.Dead)return;Items.Remove(p,"LP_HellsClaim");
   if(Pacts.Roll<.5f){
    if(p.Spawned){for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(p.DrawPos,p.Map,2f);p.DeSpawn();}
    var f=Legion();if(f!=null)p.SetFaction(f);if(!Find.WorldPawns.Contains(p))Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);
    Find.LetterStack.ReceiveLetter("长子条款："+p.LabelShort,p.LabelShort+"十八岁了。那天夜里一个穿黑袍的魔鬼律师来敲门，拿出一份旧契约——"+p.LabelShort+"跟着它走了，回到了九狱。",LetterDefOf.Death);
   }else Find.LetterStack.ReceiveLetter("长子条款："+p.LabelShort,p.LabelShort+"十八岁了。魔鬼律师来了，"+p.LabelShort+"把它赶了出去：九狱的这条条款作废了。",LetterDefOf.PositiveEvent,p);
  }

  // ---------- 调换儿（5.4） ----------
  public static void OnBirth(Pawn baby){
   if(baby==null || baby.genes==null)return;
   var mom=baby.GetMother();var dad=baby.GetFather();
   PlanarPact claim=null,winter=null;
   if(Pacts.Any)foreach(var par in new[]{mom,dad}){var x=Pacts.Of(par);if(x==null)continue;if(x.Has("Firstborn"))claim=x;if(x.Has("WinterFirstborn"))winter=x;}
   if(claim!=null){
    var c=claim.Clause("Firstborn");c.consumed=true;c.revealed=true;var tx=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_Tiefling");if(tx!=null)baby.genes.SetXenotype(tx);Items.Give(baby,"LP_HellsClaim");baby.Drawer?.renderer?.SetAllGraphicsDirty();
    Find.LetterStack.ReceiveLetter("长子条款",baby.LabelShort+"出生时额头上就有两颗小小的角芽：阿斯莫蒂尔斯契约里的“长子条款”兑现了。这孩子是提夫林，十八岁那年九狱会来要人（50%）。",LetterDefOf.NegativeEvent,baby);
   }
   bool forced=winter!=null;if(forced){var c=winter.Clause("WinterFirstborn");c.consumed=true;c.revealed=true;}
   if(forced || Pacts.Roll<Pacts.ChangelingChance){var h=(Hediff_ChangelingHidden)Items.Give(baby,"LP_ChangelingHidden");if(h!=null){h.reveal=Pacts.Now+30*GenDate.TicksPerDay;h.originalXeno=baby.genes.Xenotype;}}
  }
  public static void RevealChangeling(Pawn baby){
   if(baby==null || baby.Dead)return;var h=baby.health.hediffSet.hediffs.OfType<Hediff_ChangelingHidden>().FirstOrDefault();var orig=h?.originalXeno;if(h!=null)baby.health.RemoveHediff(h);
   var x=DefDatabase<XenotypeDef>.GetNamedSilentFail(PactRaces.Changeling);if(x!=null)AbyssRaces.ConvertTo(baby,x);
   var l=PactLetters.Send("changeling",null,baby,null,"调换儿","一个月来，"+baby.LabelShort+"越长越不像父母：皮肤苍白得没有一点血色，五官像没画完的素描。真正的孩子被妖精调换走了——摇篮里躺着的是一个调换儿。\n\n养大它？还是用一件杰作品质的艺术品把孩子换回来？");
   if(orig!=null)l.extra=DefDatabase<XenotypeDef>.AllDefsListForReading.IndexOf(orig);
  }
  public static Thing MasterworkArt(Map map)=>map?.listerThings.ThingsInGroup(ThingRequestGroup.Art).FirstOrDefault(t=>t.Spawned && t.def.category==ThingCategory.Item && !t.IsForbidden(Faction.OfPlayer) && t.TryGetQuality(out var q) && q>=QualityCategory.Masterwork);
  public static Pawn SwapBack(Pawn changeling,XenotypeDef xeno){
   var map=changeling.MapHeld;var art=MasterworkArt(map);if(art==null)return null;art.Destroy();
   var mom=changeling.GetMother();var dad=changeling.GetFather();
   var req=new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,allowDowned:true,forcedXenotype:xeno??XenotypeDefOf.Baseliner,fixedBiologicalAge:0,fixedChronologicalAge:0,developmentalStages:DevelopmentalStage.Newborn);
   Pawn baby;try{baby=PawnGenerator.GeneratePawn(req);}catch(Exception e){Log.Warning("[DND边缘大陆] 换回孩子失败："+e.Message);return null;}
   if(mom!=null)baby.relations.AddDirectRelation(PawnRelationDefOf.Parent,mom);if(dad!=null)baby.relations.AddDirectRelation(PawnRelationDefOf.Parent,dad);
   GenSpawn.Spawn(baby,CellFinder.RandomClosewalkCellNear((mom!=null && mom.Spawned?mom:changeling).PositionHeld,map,2),map);
   PactLetters.Send("changelingSwap",null,changeling,null,"孩子回来了","妖精收下了艺术品，真正的孩子"+baby.LabelShort+"回到了摇篮里。那个调换儿呢——留下来当殖民地的一员，还是送它走？");
   return baby;
  }

  // ---------- 信件里的其他选项 ----------
  public static IEnumerable<DiaOption> LetterChoices(ChoiceLetter_Pact l){
   switch(l.kind){
    case "bloodwar":{var w=Pacts.Comp?.bloodWar;if(w==null || w.aided!=null){yield return l.Option_CloseOrPostpone();yield break;}
     yield return l.Choice("旁观",()=>{});yield return l.Choice("援助九狱",()=>AidBloodWar("hells"));yield return l.Choice("援助深渊",()=>AidBloodWar("abyss"));break;}
    case "changeling":{var b=l.pawn;if(b==null || b.Dead){yield return l.Option_CloseOrPostpone();yield break;}
     var xeno=l.extra>=0 && l.extra<DefDatabase<XenotypeDef>.AllDefsListForReading.Count?DefDatabase<XenotypeDef>.AllDefsListForReading[l.extra]:null;
     yield return l.Choice("养大它",()=>{});
     yield return l.Choice("换回孩子（交一件杰作品质的艺术品）",()=>SwapBack(b,xeno),MasterworkArt(b.MapHeld)==null?"地图上没有杰作以上品质的艺术品":null);
     yield return l.Option_CloseOrPostpone();break;}
    case "changelingSwap":{var b=l.pawn;if(b==null || b.Dead){yield return l.Option_CloseOrPostpone();yield break;}
     yield return l.Choice("留下它",()=>{});
     yield return l.Choice("送它走",()=>{if(b.Spawned){FleckMaker.ThrowSmoke(b.DrawPos,b.Map,1.5f);b.DeSpawn();}var f=Court();if(f!=null)b.SetFaction(f);if(!Find.WorldPawns.Contains(b))Find.WorldPawns.PassToWorld(b,PawnDiscardDecideMode.Discard);});break;}
    case "feyBargain":{var m=Pacts.Comp?.market;var p=l.pawn;if(m==null || p==null || p.Dead){yield return l.Option_CloseOrPostpone();yield break;}
     for(int i=0;i<m.offers.Count;i++){var parts=m.offers[i].Split('|');int ii=i;string why=m.used.Contains(i)?"这笔交易已经做过了":CannotReward(parts[1],p);
      yield return l.Choice(p.LabelShort+"付出"+CostLabel(parts[0])+" → 换"+RewardLabel(parts[1]),()=>{if(m.used.Contains(ii))return;m.used.Add(ii);PayCost(parts[0],p);var got=GiveReward(parts[1],p);Messages.Message("妖精收下了"+CostLabel(parts[0])+"："+(got??"……什么也没给"),p,MessageTypeDefOf.NeutralEvent,false);},why);}
     yield return l.Option_CloseOrPostpone();break;}
    default:yield return l.Option_CloseOrPostpone();break;
   }
  }
  public static DiaOption Option_CloseOrPostpone(this ChoiceLetter_Pact l)=>l.ArchivedOnly?l.CloseOption():l.PostponeOption();
 }

 // ---------- 狂猎的 Lord：只追猎物，挡路才打 ----------
 public class LordJob_WildHunt : LordJob {
  public Pawn prey;
  public LordJob_WildHunt(){}
  public LordJob_WildHunt(Pawn prey){this.prey=prey;}
  public override StateGraph CreateGraph(){var g=new StateGraph();g.StartingToil=new LordToil_WildHunt();return g;}
  public override void ExposeData(){Scribe_References.Look(ref prey,"prey");}
 }
 public class LordToil_WildHunt : LordToil {
  static DutyDef duty;
  public override void UpdateAllDuties(){
   if(duty==null)duty=DefDatabase<DutyDef>.GetNamedSilentFail("LP_WildHuntDuty");var prey=(lord.LordJob as LordJob_WildHunt)?.prey;
   foreach(var p in lord.ownedPawns)p.mindState.duty=duty==null?new PawnDuty(DutyDefOf.AssaultColony):new PawnDuty(duty,prey,-1f);
  }
 }
 public class JobGiver_WildHunt : ThinkNode_JobGiver {
  protected override Job TryGiveJob(Pawn pawn){
   var prey=pawn.mindState.duty?.focus.Pawn;if(prey==null || prey.Dead || !prey.Spawned || prey.Map!=pawn.Map)return null;
   if(!pawn.CanReach(prey,PathEndMode.Touch,Danger.Deadly))return null;
   var job=JobMaker.MakeJob(JobDefOf.AttackMelee,prey);job.maxNumMeleeAttacks=1;job.expiryInterval=240;job.checkOverrideOnExpire=true;job.collideWithPawns=true;job.killIncappedTarget=true;
   return job;
  }
 }

 // ---------- 事件 ----------
 // 5.1 宗主的低语：一名殖民者身边出现一份契约书。有契约讲台 ×3。
 public class IncidentWorker_PatronWhisper : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms)=>Pacts.Enabled && parms.target is Map map && map.IsPlayerHome && map.mapPawns.FreeColonistsSpawned.Any() && Pacts.ScrollDef!=null;
  public override float ChanceFactorNow(IIncidentTarget target){float f=base.ChanceFactorNow(target);if(target is Map m && Pacts.LecternDef!=null && m.listerThings.ThingsOfDef(Pacts.LecternDef).Any())f*=PactEvents.WhisperLecternFactor;return f;}
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;var p=map.mapPawns.FreeColonistsSpawned.Where(c=>!Outsiders.Is(c)).RandomElementWithFallback();if(p==null)return false;
   var d=PactEvents.RandomPatron(map);if(d==null)return false;var t=PactEvents.SpawnScroll(p,d);if(t==null)return false;
   SendStandardLetter("宗主的低语",p.LabelShort+"在梦里听见了"+d.label+"的声音："+(d.Hells?"低沉、客气、每个字都像签了名":"像风穿过树叶，又像有人在笑")+"。醒来时身边多了一份契约书。\n\n把它拿到契约讲台上读 4 小时，就能看清条款——签不签由你。",LetterDefOf.NeutralEvent,parms,new LookTargets(t));
   return true;
  }
 }
 // 5.2 血战之潮
 public class IncidentWorker_BloodWar : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms)=>Pacts.Enabled && parms.target is Map map && map.IsPlayerHome && (parms.points>=1500f || parms.forced) && Pacts.Comp?.bloodWar==null;
  public override float ChanceFactorNow(IIncidentTarget target){float f=base.ChanceFactorNow(target);bool bound=AbyssBinding.PlayerPawns.Any(p=>Outsiders.Bound(p) && Outsiders.IsDemon(p));bool hells=Pacts.Comp?.Active.Any(p=>p.patron.Hells)==true;if(bound && hells)f*=PactEvents.BloodWarFactor;return f;}
  protected override bool TryExecuteWorker(IncidentParms parms)=>PactEvents.StartBloodWar((Map)parms.target,Mathf.Max(parms.points,1500f))!=null;
 }
 // 5.3 九狱商队
 public class IncidentWorker_InfernalCaravan : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms){
   if(!Pacts.Enabled || !(parms.target is Map map) || !map.IsPlayerHome || Pacts.Comp?.brokersRobbed==true)return false;
   if(parms.forced)return true;
   return Pacts.Comp?.Active.Any(p=>p.patron.Hells)==true || map.mapPawns.FreeColonistsSpawned.Any(p=>FaithRaces.IsTiefling(p) || AbyssRaces.IsCambion(p));
  }
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;var f=PactEvents.BrokersEnsure();if(f==null || f.HostileTo(Faction.OfPlayer))return false;
   var kind=DefDatabase<TraderKindDef>.GetNamedSilentFail("LP_InfernalCaravanTrader");
   var made=PactEvents.SpawnCaravan(map,f,kind,parms.points);
   // 规格：阿姆尼祖 + 2 骨魔护卫（原版按点数配护卫，点数低时补齐）
   var lord=made.Select(p=>p.GetLord()).FirstOrDefault(l=>l!=null);
   for(int i=made.Count(p=>PactRaces.Is(p,"LP_BoneDevil"));i<2 && made.Count>0;i++){var g=AbyssRaces.Make("LP_HB_BoneDevil",f,map.Tile);if(g==null)break;GenSpawn.Spawn(g,CellFinder.RandomClosewalkCellNear(made[0].Position,map,3),map);lord?.AddPawn(g);made.Add(g);}
   if(made.Count>0)Find.LetterStack.ReceiveLetter("九狱商队","九狱掮客的商队来了：一名干瘪的魔鬼律师（阿姆尼祖）带着骨魔护卫。卖契约书、灵魂币、恶魔学典籍、地狱钢叉；收囚犯、灵魂币、真名卷、神迹残片。\n\n右键阿姆尼祖：赎回被拖进九狱的人、被收走的命匣（各 "+Pacts.RansomSilver.ToString("0")+" 银）。\n\n抢劫商队：所有九狱契约债务 +2，掮客从此敌对。",LetterDefOf.PositiveEvent,new LookTargets(made.Cast<Thing>()));
   return made.Count>0;
  }
 }
 // 5.5 狂猎（随机）
 public class IncidentWorker_WildHunt : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms)=>Pacts.Enabled && parms.target is Map map && map.IsPlayerHome && map.mapPawns.FreeColonistsSpawned.Any() && Pacts.Comp?.hunts.Count==0;
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;var prey=map.mapPawns.FreeColonistsSpawned.RandomElementWithFallback();if(prey==null)return false;
   return PactEvents.StartWildHunt(map,prey,-1,parms.points,true)!=null;
  }
 }
 // 5.7 妖精集市：每季第 7 天 20 点来、次日 5 点走（由契约组件按小时检查，baseChance 0）。
 public class IncidentWorker_FeyMarket : IncidentWorker {
  protected override bool CanFireNowSub(IncidentParms parms)=>Pacts.Enabled && parms.target is Map map && map.IsPlayerHome && Pacts.Comp?.market==null;
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;var c=Pacts.Comp;var f=PactEvents.Court();if(f==null || c==null)return false;
   var kind=DefDatabase<TraderKindDef>.GetNamedSilentFail("LP_FeyMarketTrader");
   var made=PactEvents.SpawnCaravan(map,f,kind,parms.points);if(made.Count==0)return false;
   c.market=new PactMarket{map=map,pawns=made,leaveTick=Pacts.Now+PactEvents.MarketStay,offers=PactEvents.RollOffers()};
   Find.LetterStack.ReceiveLetter("妖精集市","月亮升起时，林边亮起了一串小灯笼：妖精集市开张了，天亮前就走。卖妖精银弓、小精灵，还有自愿来当仆人的萨特和调换儿。\n\n右键妖精商人：代价交易（每次集市 3 笔，各一次）——拿记忆、寿命、名字或笑声，换魔法物品、小精灵、解约、萨特，或者赎回被拖进妖精荒野的人。",LetterDefOf.PositiveEvent,new LookTargets(made.Cast<Thing>()));
   return true;
  }
 }
 // 5.6 坠落天使：一名重伤倒地的狄瓦。治好伤 → 客人 10 天（仆从计时，可征召），离开时主神神眷 +30，10% 留下天界羽披。
 public class IncidentWorker_FallenDeva : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms){
   if(!Pacts.Celestial || !(parms.target is Map map) || !map.IsPlayerHome)return false;
   if(map.mapPawns.FreeColonistsSpawned.Count(p=>AbyssTaint.Severity(p)>=AbyssTaint.Mark)>1)return false;
   return parms.forced || map.mapPawns.FreeColonistsSpawned.Any(p=>FaithUtility.DevoteeGod(p)?.Good==true);
  }
  protected override bool TryExecuteWorker(IncidentParms parms)=>PactCelestial.FallenDeva((Map)parms.target)!=null;
 }

 // ---------- 赎人 / 代价交易（右键商人） ----------
 public class FloatMenuOptionProvider_PactTrader : FloatMenuOptionProvider {
  protected override bool Drafted=>true;protected override bool Undrafted=>true;protected override bool Multiselect=>false;
  public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn,FloatMenuContext context){
   var p=context.FirstSelectedPawn;var c=Pacts.Comp;if(p==null || clickedPawn==null || c==null || clickedPawn.Faction==null || clickedPawn.Dead)yield break;
   var map=p.Map;
   if(clickedPawn.Faction.def==PactEvents.BrokersDef && !clickedPawn.HostileTo(p)){
    foreach(var t in c.taken.Where(x=>x.plane==OutsiderPlane.Hells).ToList()){var tt=t;yield return Pacts.SilverOn(map)>=Pacts.RansomSilver?new FloatMenuOption("赎回"+t.pawn.LabelShort+"（"+Pacts.RansomSilver.ToString("0")+" 银）",()=>Pacts.Ransom(tt,map,true)):new FloatMenuOption("赎回"+t.pawn.LabelShort+"（白银不到 "+Pacts.RansomSilver.ToString("0")+"）",null);}
    foreach(var o in c.seizedOwners.ToList()){var oo=o;if(oo==null)continue;yield return Pacts.SilverOn(map)>=Pacts.RansomSilver?new FloatMenuOption("赎回"+o.LabelShort+"的命匣（"+Pacts.RansomSilver.ToString("0")+" 银）",()=>Pacts.RansomPhylactery(oo,map,true)):new FloatMenuOption("赎回"+o.LabelShort+"的命匣（白银不够）",null);}
   }
   if(clickedPawn.Faction.def==PactEvents.CourtDef && c.market!=null && c.market.pawns.Contains(clickedPawn))
    yield return new FloatMenuOption("和妖精做代价交易（"+p.LabelShort+"付代价）",()=>PactLetters.Send("feyBargain",null,p,null,"妖精集市：代价交易","妖精商人眯着眼睛打量"+p.LabelShort+"：“想要点什么？我们不收银子——今晚只做这三笔买卖，每笔一次。”"));
  }
 }

 // ---------- 隐藏的调换儿 ----------
 public class Hediff_ChangelingHidden : HediffWithComps {
  public int reveal=-1;public XenotypeDef originalXeno;int acc;
  public override bool Visible=>false;
  public override void TickInterval(int delta){base.TickInterval(delta);if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;if(reveal>=0 && Find.TickManager.TicksGame>=reveal && !pawn.Dead){var p=pawn;reveal=-1;Fiends.Defer(()=>PactEvents.RevealChangeling(p));}}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref reveal,"lpReveal",-1);Scribe_Defs.Look(ref originalXeno,"lpOrigXeno");}
 }
 [HarmonyPatch(typeof(PregnancyUtility),nameof(PregnancyUtility.ApplyBirthOutcome))]
 public static class PactBirth {
  [HarmonyPriority(Priority.VeryLow)]
  static void Postfix(Thing __result){if(__result is Pawn baby)PactEvents.OnBirth(baby);}
 }
 public static class ChoiceLetterExt {
  public static DiaOption CloseOption(this ChoiceLetter l)=>new DiaOption("关闭"){action=()=>Find.LetterStack.RemoveLetter(l),resolveTree=true};
  public static DiaOption PostponeOption(this ChoiceLetter l)=>new DiaOption("稍后再说"){resolveTree=true};
 }
}
