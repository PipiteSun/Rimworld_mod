using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.52.0 五色真龙（组名 ChromaticDragons：Test.ps1 -Only ChromaticDragons；需要 HAR，没有 HAR 时只做加载检查）。
 public partial class LichChecks {
  readonly List<Thing> dragMade=new List<Thing>();
  Pawn DragMake(string color,int stage,IntVec3? at=null){
   var p=Dragons.Make(color,stage,Dragons.Faction(),map.Tile);dragMade.Add(p);
   if(at.HasValue)GenSpawn.Spawn(p,at.Value,map);
   return p;
  }
  Pawn DragColonist(IntVec3 at){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,mustBeCapableOfViolence:true,fixedBiologicalAge:30,fixedChronologicalAge:30));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h.def.isBad || h is Hediff_AddedPart).ToList())p.health.RemoveHediff(h);
   dragMade.Add(p);GenSpawn.Spawn(p,at,map);return p;
  }
  static void Off(Pawn p){if(p==null)return;if(p.Spawned)p.DeSpawn();else if(p.Corpse!=null && p.Corpse.Spawned)p.Corpse.DeSpawn();}
  IntVec3 DragOpen(IntVec3 near,int r=0)=>CellFinder.RandomClosewalkCellNear(near,map,r);
  IntVec3 DragArena(){
   // 一块 30×9 的空地（自检地图上找），吐息形状检查用。
   foreach(var c in map.AllCells.Where(c=>c.x>20 && c.z>20 && c.x<map.Size.x-40 && c.z<map.Size.z-20).OrderBy(c=>c.DistanceTo(altar.Position)))
    if(new CellRect(c.x,c.z-4,30,9).Cells.All(x=>x.InBounds(map) && x.Standable(map) && x.GetFirstPawn(map)==null && x.GetEdifice(map)==null && !x.Fogged(map))){foreach(var x in new CellRect(c.x,c.z-4,30,9).Cells)foreach(var pl in x.GetThingList(map).OfType<Plant>().ToList())pl.Destroy();return c;}
   return altar.Position+IntVec3.East*6;
  }
  void DragClean(){
   foreach(var t in dragMade){
    if(t is Pawn p){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(p.Destroyed)continue;p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}
    else if(!t.Destroyed)t.Destroy();
   }
   dragMade.Clear();
   foreach(var l in map.lordManager.lords.Where(l=>l.faction?.def==Dragons.FactionDef).ToList())map.lordManager.RemoveLord(l);
   var st=Dragons.State;if(st!=null){st.encounters.Clear();st.repeats.Clear();foreach(var l in st.lairs)if(l.site!=null && !l.site.Destroyed)l.site.Destroy();st.lairs.Clear();st.lastAncientRaid=-1;st.nextRaidTick=-1;}
   foreach(var l in Find.LetterStack.LettersListForReading.OfType<Letter_DragonExtortion>().ToList())Find.LetterStack.RemoveLetter(l);
   var mc=map.GetComponent<MapComponent_DragonLair>();if(mc!=null){mc.owner=null;mc.pending.Clear();mc.ice.Clear();}
   map.GetComponent<MapComponent_Legendary>()?.Forget();
  }

  void CheckChromaticDragons(){
   bool har=ModsConfig.IsActive(HARId);
   Check(har==Dragons.Available && har==(DefDatabase<BodyDef>.GetNamedSilentFail("LP_DragonBody")!=null) && har==(Dragons.Kind("White",2)!=null) && Items.Thing("LP_DragonHeart_Green")!=null && Items.Thing("LP_DragonBlood_Black")!=null && Items.Thing("LP_DragonFang")!=null,
    "five chromatic dragon races / body / 15 kinds load exactly when HAR is active ("+har+"); loot items always load");
   if(!har)return;
   var oSet=(LichMod.Settings.dragonStrength,LichMod.Settings.chromaticDragons,LichMod.Settings.dragonExtortion,LichMod.Settings.dragonRaid,LichMod.Settings.dragonLair,LichMod.Settings.dragonFlyoverLink);
   LichMod.Settings.dragonStrength=100;LichMod.Settings.chromaticDragons=LichMod.Settings.dragonExtortion=LichMod.Settings.dragonRaid=LichMod.Settings.dragonLair=LichMod.Settings.dragonFlyoverLink=true;
   try{CheckChromaticInner();}
   finally{
    Legendary.TestD20=null;Legendary.TestRoll=null;Dragons.TestRoll=null;Events2.TestRoll=null;DragClean();
    (LichMod.Settings.dragonStrength,LichMod.Settings.chromaticDragons,LichMod.Settings.dragonExtortion,LichMod.Settings.dragonRaid,LichMod.Settings.dragonLair,LichMod.Settings.dragonFlyoverLink)=oSet;
   }
  }
  void CheckChromaticInner(){
   RunDragonBridge();
   // 1 五色成年龙：种族、身体、年龄档、体型、生命倍率、免疫
   var expect=new Dictionary<string,string>{{"Red","Flame"},{"Blue","LP_Shock"},{"Green","LP_DragonPoison"},{"Black","LP_AlchemyAcid"},{"White","LP_Frost"}};
   foreach(var c in Dragons.Colors){
    var d=DragMake(c,1);
    var dmg=DefDatabase<DamageDef>.GetNamed(expect[c]);
    var body=Items.Hediff("LP_DragonBody_"+c);
    bool immune=body.stages[0].damageFactors.Any(f=>f.damageDef==dmg && f.factor==0f);
    float hpBefore=d.health.summaryHealth.SummaryHealthPercent;d.TakeDamage(new DamageInfo(dmg,30f,0f,-1f,null));bool untouched=Near(d.health.summaryHealth.SummaryHealthPercent,hpBefore) && !d.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any();
    Check(Dragons.ColorOf(d)==c && d.RaceProps.body.defName=="LP_DragonBody" && d.RaceProps.Humanlike && Dragons.Stage(d)==1 && Near(d.BodySize,4f) && Near(d.HealthScale,24f*Dragons.ScoreHealth) && immune && untouched && Items.Has(d,"LP_DragonBody_"+c)
     && d.needs.food==null && d.needs.rest==null && d.story.headType.defName=="LP_DragonHead_"+c && d.apparel.WornApparelCount==0 && d.equipment.Primary==null && Legendary.PartCount(d,"LP_DragonWing")==2 && Legendary.HasTail(d),
     "[stage "+Dragons.Stage(d)+" size "+d.BodySize+" hs "+d.HealthScale+" immune "+immune+" untouched "+untouched+" food "+(d.needs.food==null)+" head "+d.story.headType?.defName+" app "+d.apparel.WornApparelCount+" wings "+Legendary.PartCount(d,"LP_DragonWing")+" tail "+Legendary.HasTail(d)+"] adult "+Dragons.Info[c].label+": HAR race with LP_DragonBody (two wings, tail), adult life stage, body size 4, health scale 24 × six-score health ×"+Dragons.ScoreHealth+" (balance pass: spec 16; 1.65.0 ×3), immune to "+dmg.defName+", no food/rest need, dragon head, no gear");
   }
   // 2 年龄档：青年 / 远古的体型、生命、近战；等级、CR、固定六维；传奇抗性次数
   var young=DragMake("Red",0);var adult=DragMake("Red",1);var ancient=DragMake("Red",2);
   var ha=Adventurer.Get(adult);var hn=Adventurer.Get(ancient);var hy=Adventurer.Get(young);
   Check(Near(young.BodySize,2.5f) && Near(young.HealthScale,6f*Dragons.ScoreHealth) && Near(ancient.BodySize,6f) && Near(ancient.HealthScale,40f*Dragons.ScoreHealth) && Near(ancient.ageTracker.CurLifeStage.meleeDamageFactor,2.5f)
    && hy.level>=10 && hy.level<=12 && ha.level>=18 && ha.level<=20 && hn.level>=31 && hn.level<=35 && hn.legendary && hn.legendSpent[Legend.Melee]>0 && hn.scores[Adventurer.Str]==30 && ha.scores[Adventurer.Cha]==21
    && Legendary.Resistance(young)==null && Legendary.Resistance(adult)?.Max==3 && Legendary.Resistance(ancient)?.Max==3 && Legendary.Actions(young)==null && Legendary.Ext(ancient).actions.Any(x=>x.action=="Glance")
    && Legendary.InspectLine(ancient).StartsWith("挑战等级 CR 24") && Legendary.InspectLine(young).StartsWith("挑战等级 CR 10") && Epic.ShowsCR(adult) && young.Name is NameSingle,
    "red dragon ages: young 2.5/×6 L"+hy.level+", adult L"+ha.level+", ancient 6/×40 melee ×2.5 L"+hn.level+" (legendary, legend points spent); 5e scores (ancient Str 30), CR 10/17/24 in the inspect line; resistance 0/3/3, actions only adult+ (ancient adds 骇人一瞥)");
   if(Adventurer.IsekaiActive)Check(Legendary.IsekaiLocked(adult),"Isekai: dragons are forge-locked");

   // 3 吐息形状与伤害：红龙锥 / 蓝龙线；豁免减半；建筑受伤；白龙寒气；黑龙衣甲耐久；蓝龙打机械 ×2
   var arena=DragArena();
   var red=DragMake("Red",1,arena);red.Rotation=Rot4.East;
   var front=DragColonist(arena+new IntVec3(4,0,0));var side=DragColonist(arena+new IntVec3(5,0,2));var behind=DragColonist(arena+new IntVec3(-3,0,0));
   var redCells=Dragons.BreathCells(red,front.Position);
   Legendary.TestD20=1;var r1=Dragons.Breathe(red,front.Position);float full=r1.dealt.TryGetValue(front,out var fd)?fd:-1f;
   Legendary.TestD20=20;var front2=DragColonist(arena+new IntVec3(3,0,0));Adventurer.TryAdd(front2,5,true);var r2=Dragons.Breathe(red,front2.Position);float half=r2.dealt.TryGetValue(front2,out var hd)?hd:-1f;
   Check(redCells.Contains(front.Position) && redCells.Contains(side.Position) && !redCells.Contains(behind.Position) && redCells.All(x=>x.DistanceTo(red.Position)<=8.01f) && r1.pawns>=2 && Near(full,12f) && Near(half,6f) && r2.saved>=1
    && (front.Dead || front.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(i=>i.def.defName=="Burn")),
    "[front "+redCells.Contains(front.Position)+" side "+redCells.Contains(side.Position)+" behind "+redCells.Contains(behind.Position)+" n "+redCells.Count+" pawns "+r1.pawns+" full "+full+" half "+half+" saved "+r2.saved+" frontDead "+front.Dead+" burns "+front.health.hediffSet.hediffs.Count(i=>i.def.defName=="Burn")+" dir "+(front.Position-red.Position)+"] adult red breath: 8-cell cone hits the target and a pawn 2 cells off-axis, not one behind; 4 hits × 12 fire, a successful Dex save halves it (6)");
   foreach(var p in new[]{front,side,behind,front2})Off(p);
   var blue=DragMake("Blue",1,arena);blue.Rotation=Rot4.East;
   var onLine=DragColonist(arena+new IntVec3(10,0,0));var offLine=DragColonist(arena+new IntVec3(5,0,2));
   var wallCell=arena+new IntVec3(14,0,0);var wall=ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.WoodLog);GenSpawn.Spawn(wall,wallCell,map);dragMade.Add(wall);int wallHp=wall.HitPoints;
   var lineCells=Dragons.BreathCells(blue,wallCell);
   Legendary.TestD20=1;var r3=Dragons.Breathe(blue,wallCell);
   var mechKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");Pawn mech=null;float mechDmg=0f;
   if(mechKind!=null){mech=PawnGenerator.GeneratePawn(mechKind,Faction.OfMechanoids);dragMade.Add(mech);GenSpawn.Spawn(mech,arena+new IntVec3(3,0,0),map);Off(onLine);var r4=Dragons.Breathe(blue,mech.Position);mechDmg=r4.lastDamage;Off(mech);}
   Check(lineCells.Contains(onLine.Position) && !lineCells.Contains(offLine.Position) && lineCells.Max(x=>x.DistanceTo(blue.Position))>=13f && lineCells.All(x=>x.DistanceTo(blue.Position)<=16.01f) && wall.HitPoints<wallHp && r3.buildings>=1 && (mech==null || Near(mechDmg,24f)),
    "[on "+lineCells.Contains(onLine.Position)+" off "+lineCells.Contains(offLine.Position)+" max "+lineCells.Max(x=>x.DistanceTo(blue.Position))+" b "+r3.buildings+"] adult blue breath: 16-cell line (hits along the axis, not 2 cells aside), damages buildings ("+wallHp+"→"+wall.HitPoints+"), ×2 against mechanoids ("+mechDmg+")");
   Off(offLine);Off(blue);Off(red);
   var white=DragMake("White",1,arena);white.Rotation=Rot4.East;var cold=DragColonist(arena+new IntVec3(3,0,0));
   Legendary.TestD20=1;Dragons.Breathe(white,cold.Position);int layers=Fiends.ChillLayers(cold);Off(white);Off(cold);
   var black=DragMake("Black",1,arena);black.Rotation=Rot4.East;var armored=DragColonist(arena+new IntVec3(3,0,0));
   var vest=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_FlakVest"));armored.apparel.Wear(vest);int vh=vest.HitPoints;
   Dragons.Breathe(black,armored.Position);int vh2=vest.Destroyed?0:vest.HitPoints;Off(black);Off(armored);
   Legendary.TestD20=null;
   Check(layers==1 && vh2<=vh-Mathf.CeilToInt(vest.MaxHitPoints*.3f),"white breath adds 1 chill layer ("+layers+"); black acid breath strips 30% of worn apparel durability ("+vh+"→"+vh2+")");

   // 4 翅膀与尾巴：断尾不能尾击、两翼都断不能振翼（也不能飞跃）
   var maimed=DragMake("Green",1,DragOpen(arena+new IntVec3(0,0,0)));var foeNear=DragColonist(maimed.Position+IntVec3.East);
   bool tailBefore=Legendary.Worker("Tail").CanDo(maimed,foeNear),wingBefore=Legendary.Worker("Wing").CanDo(maimed,foeNear);
   Legendary.bypass=true;
   try{
    maimed.health.AddHediff(HediffDefOf.MissingBodyPart,maimed.RaceProps.body.AllParts.First(x=>x.def.defName=="Tail"));
    var wing=maimed.RaceProps.body.AllParts.Where(x=>x.def.defName=="LP_DragonWing").ToList();maimed.health.AddHediff(HediffDefOf.MissingBodyPart,wing[0]);bool oneWing=Legendary.CanFly(maimed);
    maimed.health.AddHediff(HediffDefOf.MissingBodyPart,wing[1]);
    Check(tailBefore && wingBefore && oneWing && !Legendary.HasTail(maimed) && !Legendary.Worker("Tail").CanDo(maimed,foeNear) && !Legendary.CanFly(maimed) && !Legendary.Worker("Wing").CanDo(maimed,foeNear),"tail strike needs the tail; one wing still flies, losing both wings stops flying and the wing buffet");
   }finally{Legendary.bypass=false;}
   Off(maimed);Off(foeNear);

   // 5 骇人威仪（成年 18 格），绿龙魅惑（心灵敏感度 0 免疫），火巨人对红龙 +2
   var presence=DragMake("Blue",1,DragOpen(arena));var watcher=DragColonist(DragOpen(presence.Position+IntVec3.West*6,1));
   var mc=map.GetComponent<MapComponent_Legendary>();mc.Refresh();mc.Forget();
   Legendary.TestD20=1;Legendary.TestRoll=.99f;int scared=mc.PresenceTick();
   var green=DragMake("Green",1,DragOpen(arena+new IntVec3(0,0,3)));var charmed=DragColonist(DragOpen(green.Position+IntVec3.West*4,1));
   bool charm=Dragons.Charm(green,charmed);bool inCharm=charmed.MentalStateDef?.defName=="LP_DragonCharmed";
   charmed.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1f,0f,-1f,null));bool woke=!charmed.InMentalState;
   Legendary.TestD20=null;Legendary.TestRoll=null;
   var giantX=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_FireGiant");int giantBonus=0;
   if(giantX!=null){var g=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forceGenerateNewPawn:true,forcedXenotype:giantX,fixedBiologicalAge:30,fixedChronologicalAge:30));dragMade.Add(g);giantBonus=Dragons.SaveBonus(g,adult,Adventurer.Wis);}
   Check(scared>=1 && Items.Has(watcher,"LP_Frightened") && charm && inCharm && woke && Dragons.CharmDC(green,charmed)<=Legendary.DC(green,Adventurer.Cha) && (giantX==null || giantBonus==2),
    "[scared "+scared+" fr "+Items.Has(watcher,"LP_Frightened")+" charm "+charm+" in "+inCharm+" woke "+woke+" giant "+giantBonus+"] adult dragon's frightful presence frightens a colonist who fails; green dragon charm (mental state LP_DragonCharmed) breaks on damage; fire giants get +2 to saves against red dragons");
   Off(presence);Off(watcher);Off(green);Off(charmed);

   // 6 来袭门槛、远古每年一次
   Check(Dragons.RaidStage(1500f,8,false)==0 && Dragons.RaidStage(1400f,8,false)==-1 && Dragons.RaidStage(4000f,16,false)==1 && Dragons.RaidStage(4000f,15,false)==0 && Dragons.RaidStage(9000f,28,false)==2 && Dragons.RaidStage(9000f,28,true)==1 && Dragons.RaidStage(9000f,27,false)==1 && Dragons.RaidStage(20000f,7,false)==-1,
    "raid age: young needs top level 8 + 1500 pts, adult 16 + 4000, ancient "+Dragons.AncientLevel+" + 9000 and not yet this year; otherwise no raid");
   var st=Dragons.State;st.lastAncientRaid=GenTicks.TicksAbs;bool used=Dragons.AncientUsedThisYear();st.lastAncientRaid=GenTicks.TicksAbs-GenDate.TicksPerYear-1;bool free=!Dragons.AncientUsedThisYear();st.lastAncientRaid=-1;
   var raidEnc=Dragons.Raid(map,"White",0,3000f);
   Check(used && free && raidEnc!=null && raidEnc.dragon.Spawned && raidEnc.guards.Count>=4 && raidEnc.guards.All(g=>g.def.defName=="LP_WinterWolf" && g.InMentalState) && raidEnc.lord?.LordJob is LordJob_AssaultColony,"ancient raid at most once a year; a white dragon raid arrives with 4-6 manhunting winter wolves and assaults");
   DragClean();

   // 7 勒索：付款按价值从储存区拿走、谈判减半、拒绝开打、一天不答复算拒绝
   var zone=new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile,map.zoneManager);map.zoneManager.RegisterZone(zone);
   var zc=DragArena();foreach(var x in new CellRect(zc.x,zc.z,4,1).Cells)zone.AddCell(x);
   var gold=ThingMaker.MakeThing(ThingDefOf.Gold);gold.stackCount=60;GenSpawn.Spawn(gold,zc,map);dragMade.Add(gold);
   var silver=ThingMaker.MakeThing(ThingDefOf.Silver);silver.stackCount=500;GenSpawn.Spawn(silver,zc+IntVec3.East,map);dragMade.Add(silver);
   var ex=Dragons.Extort(map,"Red",1);
   float expected=Dragons.Demand(map,1);
   Check(ex!=null && ex.dragon.Spawned && ex.kind=="extortion" && Near(ex.demand/Mathf.Max(1f,expected),1f) && Mathf.Abs(ex.demand-Mathf.Max(200f,map.wealthWatcher.WealthTotal*.06f))<expected*.05f+1f && ex.lord!=null && !ex.dragon.Faction.IsPlayer,"extortion: an adult red dragon lands at the edge and demands 6% of colony wealth ("+ex.demand.ToString("0")+")");
   ex.demand=600f;Legendary.TestD20=20;var who=Dragons.Negotiator(map);bool talked=Dragons.Negotiate(ex,who);float halved=ex.demand;
   Dragons.TestRoll=.99f;float taken=Dragons.Pay(ex);
   Check(talked && Near(halved,300f) && taken>=300f && taken<300f+gold.MarketValue*1.01f && (gold.Destroyed || gold.stackCount<60) && silver.stackCount==500 && ex.answered && ex.kind=="left" && st.repeats.Count==0,"negotiation with a natural 20 halves the demand (600→300); paying takes ~"+taken.ToString("0")+" worth of gold first (red dragons want gold), silver untouched; the dragon leaves (no repeat on a 99% roll)");
   Legendary.TestD20=null;Dragons.TestRoll=null;
   var ex2=Dragons.Extort(map,"Black",0);Dragons.Refuse(ex2);
   var ex3=Dragons.Extort(map,"Green",0);ex3.deadline=Find.TickManager.TicksGame;st.GameComponentTick();for(int i=0;i<250 && !ex3.fighting;i++){Find.TickManager.DoSingleTick();}
   Check(ex2.fighting && ex2.lord?.LordJob is LordJob_AssaultColony && ex3.fighting && ex3.answered,"refusing → the dragon and its guards assault; no answer within a day counts as refusing");
   map.zoneManager.DeregisterZone(zone);
   DragClean();

   // 8 龙过境联动：没有成年力能龙裔 → 25% 五色龙降落勒索；有力能龙裔时原来的逻辑不变
   Dragons.TestRoll=0f;var fly=Dragons.OnFlyoverEnd(map);Dragons.TestRoll=.99f;var noFly=Dragons.OnFlyoverEnd(map);Dragons.TestRoll=null;
   bool forceDragonBranch=DragonFlyover.AdultDragons(map).Count==0;
   Events2.TestRoll=.99f;int before=st.encounters.Count;DragonFlyover.OnFlyoverEnd(map);Events2.TestRoll=null;
   Check(fly!=null && fly.kind=="extortion" && fly.color!="White" && noFly==null && forceDragonBranch,"dragon flyover without an adult force dragon: 25% a chromatic dragon lands and extorts (never white)");
   DragClean();

   // 9 龙巢：地点、宝藏价值、魔法物品、重铸之石；巢穴动作
   var lair=Dragons.RevealLair("Black",2,"自检");
   var hoard=Dragons.Hoard(2);float gv=Dragons.HoardGoldValue(hoard);var hoard1=Dragons.Hoard(1);
   Check(lair?.site!=null && Find.WorldObjects.Contains(lair.site) && Dragons.LairOf(lair.site)==lair && gv>=40000f*.95f && hoard.Count(t=>t.def.tradeTags?.Contains("LP_MagicItem")==true)==4 && hoard.Any(t=>t.def.defName=="LP_LegendRespecStone") && Dragons.HoardGoldValue(hoard1)>=15000f*.95f && hoard1.Count(t=>t.def.tradeTags?.Contains("LP_MagicItem")==true)==2,
    "lair site on the world map; ancient hoard "+gv.ToString("0")+" (≥40000) + 4 magic items + respec stone; adult hoard ≥15000 + 2 magic items");
   foreach(var t in hoard.Concat(hoard1))t.Destroy();
   var owner=DragMake("Red",1,DragOpen(arena));var lmc=map.GetComponent<MapComponent_DragonLair>();lmc.owner=owner;lmc.center=owner.Position;lmc.color="Red";lmc.asleep=false;
   var res=Legendary.Resistance(owner);res.Refill();int lairMax=res.Max;
   var victim=DragColonist(DragOpen(owner.Position+IntVec3.West*6,1));if(victim.drafter!=null)victim.drafter.Drafted=true;float hp=victim.health.summaryHealth.SummaryHealthPercent;
   int firedBefore=MapComponent_DragonLair.Fired;var pend=lmc.Queue(victim,0);bool waits=lmc.pending.Count==1;
   for(int i=0;i<130;i++)Find.TickManager.DoSingleTick();
   bool hurt=victim.health.summaryHealth.SummaryHealthPercent<hp || victim.Dead;
   lmc.center=owner.Position+IntVec3.West*30;bool lured=!lmc.OwnerHome;
   Check(lairMax==4 && waits && MapComponent_DragonLair.Fired>=firedBefore+1 && hurt && lured,"[max "+lairMax+" waits "+waits+" fired "+(MapComponent_DragonLair.Fired-firedBefore)+" hurt "+hurt+" lured "+lured+"] lair: resistance +1 at home (4); a lair action waits 1 s behind a warning ring, then 岩浆喷涌 burns the target; dragged 20+ cells from the lair it stops");
   lmc.owner=null;DragClean();

   // 10 战利品：剥皮出龙鳞 / 心 / 牙，不出肉；龙鳞板甲本色护甲 ×2；龙之血药剂
   var corpseDragon=DragMake("Red",1,DragOpen(arena));corpseDragon.Kill(null);
   var products=corpseDragon.Corpse.ButcherProducts(null,1f).ToList();
   bool lootOk=products.Where(t=>t.def.defName=="LP_DragonScale_Red").Sum(t=>t.stackCount)==50 && products.Any(t=>t.def.defName=="LP_DragonHeart_Red") && products.Where(t=>t.def.defName=="LP_DragonFang").Sum(t=>t.stackCount)>=2 && !products.Any(t=>t.def.IsMeat);
   var plate=DefDatabase<ThingDef>.GetNamed("Apparel_PlateArmor");
   var scaleArmor=ThingMaker.MakeThing(plate,Items.Thing("LP_DragonScale_Red"));var steelArmor=ThingMaker.MakeThing(plate,ThingDefOf.Steel);
   float heatRatio=scaleArmor.GetStatValue(StatDefOf.ArmorRating_Heat)/steelArmor.GetStatValue(StatDefOf.ArmorRating_Heat),sharpRatio=scaleArmor.GetStatValue(StatDefOf.ArmorRating_Sharp)/steelArmor.GetStatValue(StatDefOf.ArmorRating_Sharp);
   var drinker=DragColonist(DragOpen(arena,2));Items.Give(drinker,"LP_DragonBlood_Red");
   var acidMan=DragColonist(DragOpen(arena,3));var blackVest=(Apparel)ThingMaker.MakeThing(plate,Items.Thing("LP_DragonScale_Black"));acidMan.apparel.Wear(blackVest);
   var brew=DefDatabase<RecipeDef>.GetNamed("LP_BrewDragonBlood_Red");
   Check(lootOk && Near(heatRatio,2f) && Near(sharpRatio,1.6f) && Near(Dragons.ElementFactor(drinker,DamageDefOf.Flame),.5f) && Items.TicksLeft(drinker,"LP_DragonBlood_Red")>=GenDate.TicksPerDay*6
    && Near(Dragons.ElementFactor(acidMan,DefDatabase<DamageDef>.GetNamed("LP_AlchemyAcid")),.5f) && Near(Dragons.ElementFactor(acidMan,DamageDefOf.Flame),1f) && brew.recipeUsers.Any(u=>u.defName=="LP_AlchemyTable") && brew.products[0].count==3,
    "loot: adult red corpse → 50 red scales, a heart, fangs, no meat; red-scale plate armor heat ×"+heatRatio.ToString("0.00")+" sharp ×"+sharpRatio.ToString("0.00")+" vs steel; red dragon blood halves fire for 7 days; black-scale armor halves acid; 1 heart → 3 bloods at the alchemy table");
   foreach(var t in products)t.Destroy();scaleArmor.Destroy();steelArmor.Destroy();

   // 11 数值核对（不打仗，只算）：有效生命 = 生命 × 100 / (1 − 利器护甲)；5 名标准殖民者的每秒伤害 = 12 × 近战伤害系数
   var ad=DragMake("Red",1);var an=DragMake("Red",2);
   float Ehp(Pawn d)=>d.HealthScale*100f/(1f-d.GetStatValue(StatDefOf.ArmorRating_Sharp));
   var lv20=DragColonist(DragOpen(arena,4));var h20=Adventurer.Get(lv20)??Adventurer.TryAdd(lv20,1,true);h20.level=20;h20.scores=new[]{20,14,16,10,12,10};h20.legendary=false;h20.legendSpent=new int[Legend.Count];h20.Dirty();
   float f20=h20.BuildStage(1f).statFactors?.GetStatFactorFromList(StatDefOf.MeleeDamageFactor)??1f;
   h20.level=40;h20.legendary=true;h20.legendSpent[Legend.Melee]=20;h20.Dirty();float f40=h20.BuildStage(1f).statFactors?.GetStatFactorFromList(StatDefOf.MeleeDamageFactor)??1f;
   float t20=Ehp(ad)/(5f*12f*f20),t40=Ehp(an)/(5f*12f*f40);
   Log.Message("[LICH-TEST] dragon numbers: adult EHP "+Ehp(ad).ToString("0")+" vs 5×L20 dps "+(60f*f20).ToString("0.0")+" = "+t20.ToString("0")+" s; ancient EHP "+Ehp(an).ToString("0")+" vs 5×L40 dps "+(60f*f40).ToString("0.0")+" = "+t40.ToString("0")+" s");
   Check(t20>=60f && t20<=150f && t40>=60f && t40<=150f,"balance: adult dragon vs five level-20 colonists "+t20.ToString("0")+" s, ancient vs five level-40 legends "+t40.ToString("0")+" s (target 60-150 s)");
  }
  static Type DragonBridge=>GenTypes.GetTypeInAnyAssembly("LichPhylactery.HARBridge.BridgeChecks");
  void RunDragonBridge(){
   var m=DragonBridge?.GetMethod("RunDragons");if(m==null)throw new Exception("HAR bridge RunDragons missing");
   try{checks+=(int)m.Invoke(null,null);}
   catch(TargetInvocationException e){if(e.InnerException==null)throw;System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
  }
 }
}
