using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.52.1 传奇怪（组名 LegendaryMonsters：Test.ps1 -Only LegendaryMonsters）。死亡骑士不需要 HAR；贝希尔 / 眼魔 / 半巫妖只在有 HAR 时检查。
 public partial class LichChecks {
  readonly List<Thing> lmMade=new List<Thing>();
  Pawn LmMake(string k,IntVec3? at=null,Faction f=null){var p=Monsters.Make(k,f,map.Tile);lmMade.Add(p);if(at.HasValue)GenSpawn.Spawn(p,at.Value,map);return p;}
  Pawn LmColonist(IntVec3 at,int level=0){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,mustBeCapableOfViolence:true,fixedBiologicalAge:30,fixedChronologicalAge:30));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h.def.isBad || h is Hediff_AddedPart).ToList())p.health.RemoveHediff(h);
   if(level>0)Adventurer.TryAdd(p,level,true);
   lmMade.Add(p);GenSpawn.Spawn(p,at,map);return p;
  }
  IntVec3 LmArena(){
   foreach(var c in map.AllCells.Where(c=>c.x>25 && c.z>25 && c.x<map.Size.x-35 && c.z<map.Size.z-25).OrderBy(c=>c.DistanceTo(altar.Position)))
    if(new CellRect(c.x-6,c.z-6,30,13).Cells.All(x=>x.InBounds(map) && x.Standable(map) && x.GetFirstPawn(map)==null && x.GetEdifice(map)==null && !x.Fogged(map))){
     foreach(var x in new CellRect(c.x-6,c.z-6,30,13).Cells)foreach(var pl in x.GetThingList(map).Where(t=>t is Plant || t.def.category==ThingCategory.Item).ToList())pl.Destroy();
     return c;
    }
   return altar.Position+IntVec3.East*8;
  }
  static void LmOff(Pawn p){if(p==null)return;if(p.Spawned)p.DeSpawn();else if(p.Corpse!=null && p.Corpse.Spawned)p.Corpse.DeSpawn();}
  void LmClean(){
   foreach(var t in lmMade){
    if(t is Pawn p){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(p.Destroyed)continue;p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}
    else if(!t.Destroyed)t.Destroy();
   }
   lmMade.Clear();
   foreach(var l in map.lordManager.lords.Where(l=>l.faction?.def==Monsters.BeastsDef).ToList())map.lordManager.RemoveLord(l);
   var st=Monsters.State;if(st!=null){foreach(var s in st.sites)if(s.site!=null && !s.site.Destroyed)s.site.Destroy();st.sites.Clear();}
   map.GetComponent<MapComponent_Legendary>()?.Refresh();
  }

  void CheckLegendaryMonsters(){
   bool har=ModsConfig.IsActive(HARId);
   var parms=new IncidentParms{target=map,points=20000f};
   Check(har==Monsters.HarAvailable && har==(Monsters.Kind(Monsters.Behir)!=null) && har==(Monsters.Kind(Monsters.Beholder)!=null) && har==(Monsters.Kind(Monsters.Demilich)!=null) && Monsters.Kind(Monsters.DeathKnight)!=null
    && Items.Thing("LP_SoulGem")!=null && Items.Thing("LP_HellfireOrb")!=null && Items.Thing("LP_AntiMagicAmulet")!=null && (har || !DefDatabase<IncidentDef>.GetNamed("LP_BehirHunt").Worker.CanFireNow(parms) && Monsters.RevealSite(Monsters.Beholder,"自检")==null),
    "legendary monsters: behir / beholder / demilich exist exactly when HAR is active ("+har+"); the death knight and all loot load without HAR; without HAR the hunter event and the sites stay off");
   try{CheckDeathKnight();if(har)CheckHarMonsters();}
   finally{Legendary.TestD20=null;Legendary.TestRoll=null;Monsters.TestRoll=null;LmClean();}
  }

  void CheckDeathKnight(){
   var arena=LmArena();var thay=Factions.Hostile??LegEnemy();
   // 1 死亡骑士：固定等级、CR、生命 ×4、铠甲、亡灵之躯；地狱火球一半火焰一半死灵
   var k=LmMake(Monsters.DeathKnight,arena,thay);var hk=Adventurer.Get(k);
   bool plate=k.apparel.WornApparel.Any(a=>a.def.defName=="LP_DeathknightPlate" && a.TryGetComp<CompQuality>()?.Quality==QualityCategory.Excellent) && k.apparel.WornApparel.Any(a=>a.def.defName=="LP_HelmRestlessDead");
   Check(hk!=null && hk.level>=22 && hk.level<=24 && hk.legendary && Legendary.InspectLine(k).StartsWith("挑战等级 CR 17") && Near(k.HealthScale,4f*Dragons.ScoreHealth) && plate && k.equipment.Primary!=null && Items.Has(k,"LP_DeathKnightBody") && k.needs.food==null && Legendary.Resistance(k)?.Max==3 && !Monsters.NoButcher(k),
    "[cr "+Legendary.InspectLine(k)+" hs "+k.HealthScale+" plate "+plate+" worn "+string.Join(",",k.apparel.WornApparel.Select(x=>x.def.defName))+" weapon "+k.equipment.Primary?.def.defName+" body "+Items.Has(k,"LP_DeathKnightBody")+" food "+(k.needs.food==null)+" res "+Legendary.Resistance(k)?.Max+"] death knight: human, level "+hk?.level+" (legendary), CR 17, health ×4, excellent death knight plate + helm and a sword, undead body (no food need), 3 legendary resistances");
   var a=LmColonist(arena+new IntVec3(6,0,0),5);var b=LmColonist(arena+new IntVec3(7,0,1));
   var giantX=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_FireGiant");Pawn giant=null;
   if(giantX!=null){giant=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forceGenerateNewPawn:true,forcedXenotype:giantX,fixedBiologicalAge:30,fixedChronologicalAge:30));lmMade.Add(giant);GenSpawn.Spawn(giant,arena+new IntVec3(6,0,-1),map);}
   Legendary.TestD20=1;var r=Monsters.Hellfire(map,arena+new IntVec3(6,0,0),k);Legendary.TestD20=null;
  Check(r.pawns>=2 && r.dealt.ContainsKey(a) && Near(r.dealt[a],Monsters.HellfireDamage*Dragons.DamageMult) && (b.Dead || b.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any()) && (giant==null || giant.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any() || giant.Dead),
    "hellfire orb: radius 4, failed Dex save takes 18 fire + 18 necrotic; the necrotic half still hurts a fire giant");
   LmOff(a);LmOff(b);LmOff(giant);
   // 2 亡灵统领：15 格内的己方亡灵伤害与护甲 +20%
   var skel=FactionConstructs.MakeBound("Skeleton",k,thay);lmMade.Add(skel);GenSpawn.Spawn(skel,k.Position+IntVec3.North*3,map);
   var farSkel=FactionConstructs.MakeBound("Skeleton",k,thay);lmMade.Add(farSkel);GenSpawn.Spawn(farSkel,CellFinder.RandomClosewalkCellNear(k.Position+IntVec3.West*20,map,1),map);
   int cmd=Monsters.Command(k);
   var buff=Items.Hediff("LP_UndeadCommanded");
   Check(cmd>=1 && Items.Has(skel,"LP_UndeadCommanded") && !Items.Has(farSkel,"LP_UndeadCommanded") && Near(buff.stages[0].statFactors.GetStatFactorFromList(StatDefOf.MeleeDamageFactor),1.2f),"undead command: allied skeleton within 15 cells gets +20% damage and armor, one 20 cells away does not");
   // 3 倒地 1 小时后化灰，留下优秀品质的铠甲和地狱火宝珠
   var res=Legendary.Resistance(k);if(res!=null)res.charges=0;
   // 超凡体质的“硬撑”会把第一次倒地延后（游戏里到时会再检查），自检直接用掉本期的硬撑次数。
   if(hk!=null){hk.holdUntil=0;hk.holdStart=Find.TickManager.TicksGame;hk.holdUses=99;}Legendary.painWindow.Remove(k);
   k.health.AddHediff(HediffDefOf.Anesthetic);var anes=k.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic);if(anes!=null)anes.Severity=1f;k.health.CheckForStateChange(null,null);bool downed=k.Downed && !k.Dead;
   var body=k.health.hediffSet.hediffs.OfType<Hediff_MonsterBody>().First();
   var at=k.Position;int ashed=Monsters.Ashed;
   body.TickInterval(60);int dt1=body.downTick;body.downTick=Find.TickManager.TicksGame-Monsters.AshTicks;bool downNow=k.Downed;string kk=body.Kind;body.TickInterval(60);
   var drop=at.GetThingList(map).Concat(GenRadial.RadialCellsAround(at,3f,true).Where(x=>x.InBounds(map)).SelectMany(x=>x.GetThingList(map))).Distinct().ToList();
   var armor=drop.FirstOrDefault(t=>t.def.defName=="LP_DeathknightPlate");var orb=drop.FirstOrDefault(t=>t.def.defName=="LP_HellfireOrb");
   if(armor!=null)lmMade.Add(armor);if(orb!=null)lmMade.Add(orb);
   Check(downed && Monsters.Ashed==ashed+1 && k.Dead && (k.Corpse==null || k.Corpse.Destroyed) && armor!=null && armor.TryGetComp<CompQuality>()?.Quality==QualityCategory.Excellent && orb!=null,"[downed "+downed+" ashed "+(Monsters.Ashed-ashed)+" dead "+k.Dead+" corpse "+(k.Corpse!=null && !k.Corpse.Destroyed)+" armor "+(armor!=null)+" q "+armor?.TryGetComp<CompQuality>()?.Quality+" orb "+(orb!=null)+" downTick "+body.downTick+" dt1 "+dt1+" now "+Find.TickManager.TicksGame+" downNow "+downNow+" kind "+kk+"] a downed death knight turns to ash after an hour (no corpse), leaving its excellent plate and a hellfire orb");
   // 4 塞伊 ≥5000 点 10% 死亡骑士带队：抵消点数、和红袍法师一起带队，倒下时原来的斩首规则照旧生效
   if(thay!=null && Factions.HostileDef!=null){
    var gp=new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,tile=map.Tile,faction=thay,points=6000f};
    Monsters.TestRoll=0f;var group=PawnGroupMakerUtility.GeneratePawns(gp,false).ToList();Monsters.TestRoll=null;
    foreach(var p in group)lmMade.Add(p);
    var dk=group.FirstOrDefault(x=>Monsters.Is(x,Monsters.DeathKnight));var tr=Decapitation.Tracker;
    var alive=group.Where(x=>!x.Destroyed && !x.Dead).ToList();foreach(var p in alive)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(arena+IntVec3.North*4,map,6),map);
    if(dk!=null && !alive.Any(x=>x!=dk && x.RaceProps.Humanlike && Necro.Construct(x)==null && x.kindDef.defName!="LP_FH_RedWizard")){var orc=PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("LP_FH_OrcWarrior"),thay);lmMade.Add(orc);group.Add(orc);alive.Add(orc);GenSpawn.Spawn(orc,CellFinder.RandomClosewalkCellNear(arena+IntVec3.North*4,map,6),map);} // 裁人后可能只剩法师和骷髅
    if(dk!=null){LordMaker.MakeNewLord(thay,new LordJob_AssaultColony(thay,false,false,false,false,false),map,alive);var kb=dk.health.hediffSet.hediffs.OfType<Hediff_MonsterBody>().First();kb.TickInterval(60);kb.TickInterval(60);}
    var followers=dk==null?new List<Pawn>():tr.FollowersOf(dk).Where(x=>!x.Destroyed).ToList();
    var wizards=group.Count(x=>x.kindDef.defName=="LP_FH_RedWizard");
    Monsters.TestRoll=.99f;var none=PawnGroupMakerUtility.GeneratePawns(gp,false).ToList();Monsters.TestRoll=null;foreach(var p in none)lmMade.Add(p);
    int reacted=dk==null?0:tr.Trigger(dk);
    Check(dk!=null && followers.Count>=1 && reacted>=1 && !none.Any(x=>Monsters.Is(x,Monsters.DeathKnight)),"[group "+string.Join(",",group.Select(x=>x.kindDef.defName+(x.Destroyed?"(x)":"")))+" lord "+dk?.GetLord()?.ownedPawns.Count+"] Thay raid ≥5000 points: a death knight leads ("+followers.Count+" followers bound to it, "+wizards+" red wizards keep theirs); when it falls its followers break ("+reacted+") like any Thay leader; no knight on a failed 10% roll");
   }
   // 5 拉洛克古墓 50% 多一名死亡骑士守卫
   Monsters.TestRoll=0f;var guard=Monsters.CryptGuard(map,thay);Monsters.TestRoll=.99f;var noGuard=Monsters.CryptGuard(map,thay);Monsters.TestRoll=null;
   if(guard!=null)lmMade.Add(guard);
   Check(guard!=null && guard.Spawned && guard.GetLord()?.LordJob is LordJob_DefendPoint && noGuard==null,"Larloch's crypt: 50% an extra death knight guard defending the centre");
   LmClean();
  }

  void CheckHarMonsters(){
   var m=DragonBridgeMonsters?.GetMethod("RunMonsters");if(m==null)throw new Exception("HAR bridge RunMonsters missing");
   try{checks+=(int)m.Invoke(null,null);}catch(TargetInvocationException e){if(e.InnerException==null)throw;System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
   var arena=LmArena();
   // 6 贝希尔：体型、生命、护甲、等级、不是龙、十二足、龙之死敌、闪电吐息
   var behir=LmMake(Monsters.Behir,arena);behir.Rotation=Rot4.East;var hb=Adventurer.Get(behir);
   int legs=behir.RaceProps.body.AllParts.Count(x=>x.def.defName=="Leg");
   var adultDragon=Dragons.Available?Dragons.Make("Red",1):null;if(adultDragon!=null)lmMade.Add(adultDragon);
   float foe=adultDragon==null?1.5f:Monsters.DamageFactor(adultDragon,new DamageInfo(DamageDefOf.Bite,10f,0f,-1f,behir));
   var line1=LmColonist(arena+new IntVec3(6,0,0),5);var off=LmColonist(arena+new IntVec3(5,0,3));
   Legendary.TestD20=1;var br=Monsters.BehirBreath(behir,line1.Position);Legendary.TestD20=null;
   Check(Near(behir.BodySize,3.5f) && Near(behir.HealthScale,12f*Dragons.ScoreHealth) && Near(behir.GetStatValue(StatDefOf.ArmorRating_Sharp),.45f) && hb.level>=15 && hb.level<=17 && Legendary.InspectLine(behir).StartsWith("挑战等级 CR 11") && legs==12
    && !Dragons.Is(behir) && !MagicEffects.IsDragon(behir) && Near(foe,1.5f) && Legendary.FearImmune(behir) && br.dealt.ContainsKey(line1) && !br.dealt.ContainsKey(off) && Near(br.dealt[line1],12f) && Legendary.Resistance(behir)?.Max==2,
    "behir: size 3.5, health ×12, armor 45%, L"+hb.level+", CR 11, 12 legs, not a dragon (dragon slayer ignores it), ×1.5 damage vs dragons, fear immune; lightning line hits on-axis only (4×12)");
   LmOff(off);
   // 7 吞噬：敏捷豁免失败被吞（离开地图）；每秒酸蚀；外面打 60 点吐出来
   var body=behir.health.hediffSet.hediffs.OfType<Hediff_MonsterBody>().First();
   var food=LmColonist(behir.Position+IntVec3.West,3);Legendary.TestD20=1;bool swallowed=body.TrySwallow(food);Legendary.TestD20=null;
   bool gone=!food.Spawned && body.swallowed==food;float hp=food.health.summaryHealth.SummaryHealthPercent;body.Digest();bool acid=food.health.summaryHealth.SummaryHealthPercent<hp || food.Dead;
   var res=Legendary.Resistance(behir);if(res!=null)res.charges=0;
   behir.TakeDamage(new DamageInfo(DamageDefOf.Blunt,40f,2f,-1f,null));behir.TakeDamage(new DamageInfo(DamageDefOf.Blunt,40f,2f,-1f,null));
   bool spat=body.swallowed==null && (food.Spawned || food.Dead);
   Check(swallowed && gone && acid && spat,"behir swallows an adjacent colonist on a failed Dex save (off the map), digests 4 acid per second, spits them out after 60 damage from outside");
   LmOff(food);LmOff(line1);LmOff(behir);
   // 8 眼魔：10 根眼柄 10 种射线、打掉眼柄少一种、反魔法锥、护盾腰带失效
   var bh=LmMake(Monsters.Beholder,arena);bh.Rotation=Rot4.East;var hbh=Adventurer.Get(bh);
   var front=LmColonist(arena+new IntVec3(8,0,0),5);var back=LmColonist(arena+new IntVec3(-6,0,0),5);
   var mc=map.GetComponent<MapComponent_Legendary>();mc.Refresh();
   var belt=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_ShieldBelt"));front.apparel.Wear(belt);var sh=belt.TryGetComp<CompShield>();
   bool inCone=Monsters.InAntiMagic(front),behind=Monsters.InAntiMagic(back);
   Check(Near(bh.BodySize,2.5f) && Near(bh.HealthScale,9f*Dragons.ScoreHealth) && hbh.level>=20 && hbh.level<=22 && Monsters.Stalks(bh).Count==10 && Monsters.LiveRays(bh).Count==10 && Monsters.MainEye(bh) && !bh.Downed && bh.health.capacities.CapableOf(PawnCapacityDefOf.Moving)
    && mc.beholders.Contains(bh) && inCone && !behind && sh.ShieldState==ShieldState.Disabled,
    "beholder: size 2.5, health ×9, L"+hbh.level+", 10 eye stalks = 10 rays, floats (moving "+bh.health.capacities.GetLevel(PawnCapacityDefOf.Moving).ToStringPercent()+"); anti-magic cone covers a colonist in front (shield belt off), not one behind");
   Legendary.TestD20=1;Legendary.TestRoll=.99f;
   var targets=new List<Pawn>();for(int i=0;i<10;i++){targets.Add(LmColonist(DragOpen(arena+new IntVec3(4+i%5,0,i<5?-4:4),1),3));}
   targets[7].TakeDamage(new DamageInfo(DamageDefOf.Cut,1f,0f,-1f,null));foreach(var part in targets[7].health.hediffSet.GetNotMissingParts().Where(x=>x.depth==BodyPartDepth.Outside).Take(14).ToList()){var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,targets[7],part);cut.Severity=Mathf.Min(part.def.hitPoints*.5f,10f);targets[7].health.AddHediff(cut);if(targets[7].health.summaryHealth.SummaryHealthPercent<.25f)break;}
   var results=new List<string>();for(int i=0;i<10;i++)results.Add(Monsters.Ray(bh,i,targets[i]));
   Legendary.TestD20=null;Legendary.TestRoll=null;
   bool charm=targets[0].MentalStateDef?.defName=="LP_DragonCharmed",slow=Items.Has(targets[3],"LP_FrostSlow"),fear=Items.Has(targets[2],"LP_Frightened"),stone=Items.Has(targets[5],"LP_BeholderPetrified") && !targets[5].health.capacities.CapableOf(PawnCapacityDefOf.Moving),sleep=Items.Has(targets[8],"LP_MagicSleep") || targets[8].health.hediffSet.HasHediff(HediffDefOf.Anesthetic);
   bool death=targets[7].Dead || targets[7].health.summaryHealth.SummaryHealthPercent>=.25f;
   Check(results.All(x=>x!=null) && charm && fear && slow && stone && sleep && death,"[res "+string.Join(",",results.Select(x=>x??"null"))+" charm "+charm+" fear "+fear+" slow "+slow+" stone "+stone+" sleep "+sleep+" ms0 "+targets[0].MentalStateDef?.defName+"] beholder rays on failed saves: charm, fear, slow, petrify (can't move; dispel dust removes it), sleep; death ray only below 25% health ("+targets[7].Dead+")");
   var stalk=Monsters.Stalks(bh)[5];Legendary.bypass=true;try{bh.health.AddHediff(HediffDefOf.MissingBodyPart,stalk);}finally{Legendary.bypass=false;}
   Check(Monsters.LiveRays(bh).Count==9 && !Monsters.LiveRays(bh).Contains(5),"shooting off the petrify eye stalk removes that ray");
   var disp=Items.Hediff("LP_MagicDispelled")?.GetModExtension<DispelExtension>();
   Check(disp!=null && disp.removeHediffs.Contains("LP_BeholderPetrified"),"dispel dust cures beholder petrification");
   foreach(var t in targets)LmOff(t);LmOff(front);LmOff(back);LmOff(bh);mc.Refresh();
   // 9 半巫妖：体型 0.8、生命 ×3、护甲 80%；吞魂之嚎（DC 21）；巫妖免疫；30 秒不救治就死、包扎后活；圣水银器 ×2
   var dl=LmMake(Monsters.Demilich,arena);var hd=Adventurer.Get(dl);
   var v1=LmColonist(arena+new IntVec3(5,0,0),5);var v2=LmColonist(arena+new IntVec3(5,0,2),5);
   Legendary.TestD20=1;int torn=Monsters.Howl(dl);Legendary.TestD20=null;
   bool down1=Items.Has(v1,"LP_SoulTorn") && v1.Downed,down2=Items.Has(v2,"LP_SoulTorn") && v2.Downed;
   var st1=(Hediff_SoulTorn)v1.health.hediffSet.hediffs.First(h=>h is Hediff_SoulTorn);var st2=(Hediff_SoulTorn)v2.health.hediffSet.hediffs.First(h=>h is Hediff_SoulTorn);
   st2.Tended(1f,1f);
   st1.ageTicks=Hediff_SoulTorn.DeathTicks;st2.ageTicks=Hediff_SoulTorn.DeathTicks;st1.TickInterval(60);st2.TickInterval(60);
   bool lichImmune=Monsters.SoulImmune(dl);
   Check(Near(dl.BodySize,.8f) && Near(dl.HealthScale,3f*Dragons.ScoreHealth) && Near(dl.GetStatValue(StatDefOf.ArmorRating_Sharp),.8f) && hd.level>=26 && hd.level<=28 && Legendary.InspectLine(dl).StartsWith("挑战等级 CR 18") && !dl.Downed
    && torn>=2 && down1 && down2 && v1.Dead && !v2.Dead && lichImmune && Near(Monsters.DamageFactor(dl,new DamageInfo(DefDatabase<DamageDef>.GetNamed("LP_Radiant"),10f,0f,-1f,null)),2f) && Near(Monsters.DamageFactor(dl,new DamageInfo(DamageDefOf.Cut,10f)),1f),
    "demilich: size 0.8, health ×3, armor 80%, L"+hd.level+", CR 18; soul-devouring howl downs both colonists (DC 21), the untreated one dies after 30 s, the tended one lives; radiant / holy damage ×2");
   LmOff(v1);LmOff(v2);
   // 10 掉落、灵魂宝石、半巫妖头骨
   var loot=Monsters.Loot(dl).Concat(Monsters.Loot(LmMake(Monsters.Beholder))).Concat(Monsters.Loot(LmMake(Monsters.Behir))).ToList();lmMade.AddRange(loot);
   Check(loot.Any(t=>t.def.defName=="LP_SoulGem") && loot.Any(t=>t.def.defName=="LP_DemilichSkull") && loot.Any(t=>t.def.defName=="LP_BeholderEye") && loot.Count(t=>t.def.defName=="LP_BeholderStalk")>=1 && loot.Any(t=>t.def.defName=="LP_BehirHorn") && loot.Any(t=>t.def.defName=="LP_Leather_Behir")
    && Monsters.NoButcher(dl) && !Monsters.NoButcher(LmMake(Monsters.DeathKnight)),"loot: soul gem + demilich skull, beholder eye + surviving eye stalks, behir horns + hide; their corpses butcher to nothing");
   LmOff(dl);
   // 11 地点：眼魔巢穴（夺心魔首领 20%）、无尽墓穴；宝藏
   Monsters.TestRoll=0f;var site1=Monsters.RevealSite(Monsters.Beholder,"自检");var site2=Monsters.RevealSite(Monsters.Demilich,"自检");Monsters.TestRoll=null;
   var hoard=GenStep_MonsterLair.Hoard(Monsters.Demilich);lmMade.AddRange(hoard);
   Check(site1!=null && site2!=null && Monsters.SiteOf(site1)?.kind==Monsters.Beholder && Monsters.SiteOf(site2)?.kind==Monsters.Demilich && hoard.Any(t=>t.def.defName=="LP_LegendRespecStone") && hoard.Count(t=>t.def.tradeTags?.Contains("LP_MagicItem")==true)==2,
    "beholder lair and endless tomb sites appear on the world map; the tomb hoard has 2 magic items and a respec stone");
   // 12 龙的猎手：要有人杀过真龙（或有成年力能龙裔）
   var inc=DefDatabase<IncidentDef>.GetNamed("LP_BehirHunt");var colonist=map.mapPawns.FreeColonistsSpawned.First();var rec=DefDatabase<RecordDef>.GetNamed("LP_DragonsSlain");
   float old=colonist.records.GetValue(rec);bool before=Monsters.HunterReady(map);colonist.records.AddTo(rec,1f);bool after=Monsters.HunterReady(map);
   bool fired=inc.Worker.TryExecute(new IncidentParms{target=map,points=3000f});colonist.records.AddTo(rec,-1f);
   var hunter=map.mapPawns.AllPawnsSpawned.FirstOrDefault(p=>Monsters.Is(p,Monsters.Behir));if(hunter!=null)lmMade.Add(hunter);
   Check((before || DragonFlyover.AdultDragons(map).Count>0) || !before && after && fired && hunter!=null && hunter.GetLord()?.LordJob is LordJob_AssaultColony,"dragon hunter: a behir only comes after someone has slain a true dragon (or the colony has an adult force dragon), and assaults");
  }
  static Type DragonBridgeMonsters=>GenTypes.GetTypeInAnyAssembly("LichPhylactery.HARBridge.BridgeChecks");
 }
}
