using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using GR = LichPhylactery.Growth;
namespace LichPhylactery {
 // 1.58.3：Test.ps1 -Only Growth（持续成长）：典籍、许愿、传承武器、实战磨砺、派系声望、存读档。
 public partial class LichChecks {
  static string grSaved,grIds,grWeaponId;static int grRep;
  Hediff_Adventurer GrAdv(Pawn p){var h=Adventurer.Get(p)??Adventurer.TryAdd(p,1,true);h.scores=new[]{10,10,10,10,10,10};h.tomes=new bool[6];h.wishAbility=-1;h.grind=new float[6];h.grindGained=new int[6];h.grindLast=null;h.Dirty();return h;}
  static BookOutcome_Tome TomeDoer(Thing t)=>(t as Book)?.BookComp?.Doers.OfType<BookOutcome_Tome>().FirstOrDefault();
  Faction GrFriend=>Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f=>f.def.defName=="LP_FaerunColony") ?? Find.FactionManager.AllFactionsListForReading.FirstOrDefault(GR.Tracked);

  void CheckGrowth(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oGrind=s.growthGrind,oRep=s.factionReputation,oCo=s.adventurerIsekaiCoexist;bool oSup=Adventurer.TestSuppressed;
   Adventurer.TestSuppressed=false;s.adventurer=true;s.growthGrind=true;s.factionReputation=true;s.adventurerIsekaiCoexist=false;
   var made=new List<Thing>();var gs=GR.State;Dictionary<int,int> oldRep=gs==null?null:new Dictionary<int,int>(gs.rep);
   try{
    // 1. 定义：6 本典籍、4 件传承武器，贴图都在，都不能制作；黑网商队带典籍；设置默认开
    var tomes=Enumerable.Range(0,6).Select(GR.TomeDef).ToList();var legacy=GR.LegacyNames.Select(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)).ToList();
    bool tex=tomes.Concat(legacy).All(d=>d!=null && ContentFinder<Texture2D>.Get(d.graphicData.texPath,false)!=null);
    bool craft=DefDatabase<RecipeDef>.AllDefsListForReading.Any(r=>r.products!=null && r.products.Any(c=>tomes.Contains(c.thingDef) || legacy.Contains(c.thingDef)));
    var bn=DefDatabase<TraderKindDef>.GetNamedSilentFail("LP_Caravan_BlackNetwork");
    Check(tex && !craft && bn!=null && bn.stockGenerators.OfType<StockGenerator_RandomTome>().Any(g=>Near(g.chance,.05f)) && legacy.All(d=>d.GetCompProperties<CompProperties_LegacyWeapon>()!=null) && new Settings().growthGrind && new Settings().factionReputation,"6 tomes and 4 legacy weapons load with textures, none craftable; black net caravans carry a tome 5% of the time; growth settings default on");
    // 2. 典籍：30 小时后该项 +2、上限 +2，书化为灰
    var r=HtPawn();var hr=GrAdv(r);var str=GR.MakeTome(0);made.Add(str);var d=TomeDoer(str);
    bool early=d.Feed(r,GR.TomeTicks-1,true);bool done=d.Feed(r,1,true);
    Check(d!=null && !early && done && hr.scores[0]==12 && hr.Cap(0)==22 && hr.tomes[0] && str.Destroyed,"力量手册: 30 hours of reading → Str 10 → "+hr.scores[0]+", cap "+hr.Cap(0)+", the book turns to ash");
    // 3. 同一项只能吃一本；不同属性的照样能读
    var str2=GR.MakeTome(0);var dex=GR.MakeTome(1);made.Add(str2);made.Add(dex);
    Check(!TomeDoer(str2).DoesProvidesOutcome(r) && GR.CannotReadTome(r,0)!=null && TomeDoer(dex).DoesProvidesOutcome(r) && !TomeDoer(str2).Feed(r,GR.TomeTicks,true) && hr.scores[0]==12,"a second 力量手册 is refused, 敏捷手册 is still readable");
    // 4. 巨灵“让我更强”：+1、上限 +1，每人一生一次
    bool w1=GR.WishStronger(r,1),w2=GR.WishStronger(r,2);
    Check(w1 && !w2 && hr.scores[1]==11 && hr.Cap(1)==21 && hr.wishAbility==1 && hr.scores[2]==10,"genie wish 让我更强: Dex +1 with cap 21, a second wish for the same person is refused");
    // 5. 传承武器：第一个击杀 10 个敌人的人成为主人
    var owner=HtPawn(null,null,true,0,0);var ho=GrAdv(owner);var sword=(ThingWithComps)GR.MakeLegacy("LP_Legacy_Sword");owner.equipment.AddEquipment(sword);var c=sword.GetComp<CompLegacyWeapon>();
    var enemy=AdvEnemy;for(int i=0;i<9;i++)GR.OnKill(owner,HtPawn(null,enemy));bool none=c.owner==null && c.kills.Values.Max()==9;GR.OnKill(owner,HtPawn(null,enemy));
    Check(none && c.owner==owner && sword.TryGetQuality(out var q) && q==QualityCategory.Masterwork && Near(GR.DamageFactor(owner,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,owner,null,sword.def)),1f),"legacy sword: 9 kills is not enough, the 10th binds it (masterwork, still dormant ×1)");
    // 6. 觉醒（主人 10 级）：伤害 +15%，得到能力；别人拿着只是沉睡版
    ho.level=10;GR.Sync(owner);var dawn=DefDatabase<AbilityDef>.GetNamed("LP_LegacyDawnStrike");
    float f1=GR.DamageFactor(owner,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,owner,null,sword.def));
    var other=HtPawn(null,null,true,1,0);GrAdv(other).level=20;var sword2=(ThingWithComps)GR.MakeLegacy("LP_Legacy_Sword");other.equipment.AddEquipment(sword2);
    Check(Near(f1,1.15f) && owner.abilities.GetAbility(dawn)!=null && Near(GR.DamageFactor(other,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,other,null,sword2.def)),1f) && other.abilities.GetAbility(dawn)==null,"awakened at owner level 10: damage ×"+f1+" and 黎明斩; someone else's unbound sword stays dormant");
    // 7. 升华（传奇冒险者）：再 +15%，3 格内友军近战命中 +5%；战锤升华主人受伤 -10%
    ho.legendary=true;GR.Sync(owner);float f2=GR.DamageFactor(owner,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,owner,null,sword.def));float aura=GR.SwordHit(other);
    var hm=HtPawn();var hhm=GrAdv(hm);hhm.legendary=true;var hammer=(ThingWithComps)GR.MakeLegacy("LP_Legacy_Hammer");hm.equipment.AddEquipment(hammer);hammer.GetComp<CompLegacyWeapon>().owner=hm;
    Check(Near(f2,1.3f) && Near(aura,.05f) && Near(GR.GuardFactor(hm),.9f) && Near(GR.GuardFactor(other),1f),"ascended (legendary owner): sword ×"+f2+", allies within 3 cells +5% melee hit; ascended hammer: owner takes ×0.9 damage");
    // 8. 主人死去：回到沉睡，击杀数清零
    c.kills[other.thingIDNumber]=3;GR.OwnerDied(owner);
    Check(c.owner==null && c.kills.Count==0 && Near(GR.DamageFactor(owner,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,owner,null,sword.def)),1f),"when the owner dies the weapon sleeps again and the kill counts reset");
    owner.equipment.Remove(sword);GR.Sync(owner);
    // 9. 实战磨砺：按差值累计（第一次只记基准），满 120000 +1
    var g=HtPawn();var hg=GrAdv(g);int raised=GR.Raised;GR.Sample(g);g.records.AddTo(RecordDefOf.DamageTaken,1300f);int n1=GR.Sample(g);
    Check(hg.grindLast!=null && n1==1 && hg.scores[2]==11 && hg.grindGained[2]==1 && Near(hg.grind[2],10000f),"实战磨砺: the first sample is only a baseline; 1300 damage taken = 130000 grind → Con +1 (10000 carried over)");
    // 10. 每项一生最多 +2，到上限就不加
    g.records.AddTo(RecordDefOf.DamageTaken,1300f);GR.Sample(g);g.records.AddTo(RecordDefOf.DamageTaken,2600f);GR.Sample(g);
    var g2=HtPawn();var hg2=GrAdv(g2);hg2.scores[2]=20;GR.Sample(g2);g2.records.AddTo(RecordDefOf.DamageTaken,2000f);int capped=GR.Sample(g2);
    Check(hg.grindGained[2]==2 && hg.scores[2]==12 && hg.grind[2]<=GR.GrindNeed && capped==0 && hg2.scores[2]==20,"grinding stops at +2 per score (Con 12) and never goes past the cap (Con 20 stays 20)");
    // 11. 派系声望：任务 +3 → 头衔一级、商队卖价 -5%；送礼每 20 好感 +1
    var f=GrFriend;
    if(f!=null && gs!=null){
     gs.rep.Remove(f.loadID);gs.giftAccum.Remove(f.loadID);bool hostile=f.HostileTo(Faction.OfPlayer);
     if(hostile)Factions.SetRelation(f,Faction.OfPlayer,50);
     GR.Gain(f,GR.QuestRep,"测试任务");int afterQuest=GR.Rep(f);float price=GR.PriceFactor(f,TradeAction.PlayerBuys);
     GR.Gift(f,45);int afterGift=GR.Rep(f);
     Check(afterQuest==3 && GR.Rank(f)>=1 && GR.Title(f)!="无" && Near(price,.95f) && Near(GR.PriceFactor(f,TradeAction.PlayerSells),1f) && afterGift==5 && gs.giftAccum[f.loadID]==5,"faction reputation ("+f.Name+"): quest +3 → title "+GR.Title(f)+", buy prices ×0.95; a 45-goodwill gift → +2 (5 carried)");
     // 12. 不会超过 50；敌对时冻结；四级头衔有季度礼物
     GR.Gain(f,100,"测试");int max=GR.Rep(f);Factions.SetRelation(f,Faction.OfPlayer,-80);bool frozen=!GR.Gain(f,3,"测试") && GR.Rep(f)==max;Factions.SetRelation(f,Faction.OfPlayer,50);
     int gifts=gs.gifts;GR.TestRoll=.5f;try{GR.Quarterly(gs);}finally{GR.TestRoll=null;}
     Check(max==GR.RepMax && GR.Rank(f)==4 && frozen && gs.gifts==gifts+1,"reputation caps at 50 (rank 4), is frozen while hostile, and rank 4 sends a quarterly gift");
     grRep=GR.Rep(f);
    }else Check(true,"no friendly DND faction in this world: reputation checks skipped");
   }finally{
    s.adventurer=oOn;s.growthGrind=oGrind;s.factionReputation=oRep;s.adventurerIsekaiCoexist=oCo;GR.TestRoll=null;
    foreach(var t in made)if(!t.Destroyed)t.Destroy();
    foreach(var p in htMade){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();else if(!p.Destroyed)p.Destroy();}htMade.Clear();
    Adventurer.TestSuppressed=oSup;
   }
  }
  // 13. 存读档：典籍、许愿、磨砺、传承武器主人与击杀数、派系声望
  void PrepareGrowthSave(){
   Adventurer.TestSuppressed=false;
   try{
    var p=HtPawn(null,null,true,0,-2);var h=GrAdv(p);h.tomes[3]=true;h.scores[3]=12;h.wishAbility=4;h.grind=new[]{1f,2f,3f,4f,5f,6f};h.grindGained=new[]{0,1,2,0,1,0};h.grindLast=new[]{10f,20f,30f,40f,50f,60f};
    var w=(ThingWithComps)GR.MakeLegacy("LP_Legacy_Bow");p.equipment.AddEquipment(w);var c=w.GetComp<CompLegacyWeapon>();c.owner=p;
    var b=GR.MakeLegacy("LP_Legacy_Staff");GenSpawn.Spawn(b,HtCell(1,-2),map);b.TryGetComp<CompLegacyWeapon>().kills[12345]=7;
    grIds=p.ThingID;grWeaponId=b.ThingID;grSaved=GrSummary(p,b);htMade.Clear();
   }finally{Adventurer.TestSuppressed=true;}
  }
  static string GrSummary(Pawn p,Thing b){
   var h=Adventurer.Get(p);var c=GR.Legacy(p);var cb=b?.TryGetComp<CompLegacyWeapon>();
   return string.Join(",",h.tomes)+"|"+h.wishAbility+"|"+string.Join(",",h.grind)+"|"+string.Join(",",h.grindGained)+"|"+string.Join(",",h.grindLast??new float[0])+"|"+(c?.owner==p)+"|"+(cb==null?-1:cb.kills.TryGetValue(12345,out int k)?k:0);
  }
  void CheckGrowthAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==grIds);var b=map.listerThings.AllThings.FirstOrDefault(t=>t.ThingID==grWeaponId);
   string now=p==null?"missing":GrSummary(p,b);var f=GrFriend;
   Check(now==grSaved && (f==null || GR.Rep(f)==grRep),"tomes, wish, grinding, legacy owner and kill counts, faction reputation survive save/load ("+grSaved+", rep "+grRep+")");
   p?.Destroy();b?.Destroy();
  }
 }
}
