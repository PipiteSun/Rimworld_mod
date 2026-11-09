using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.140.0：Test.ps1 -Only CombatFeats（6 个战斗专长、4 个史诗恩赐、施法奖励专长）。
 public partial class LichChecks {
  static string cfId;
  Pawn CfFighter(int dx,int dz){var p=HtPawn();var h=F2Adv(p);h.pendingASI=0;h.fighterFeats=0;h.casterFeats=0;if(!p.Spawned)GenSpawn.Spawn(p,MlCell(dx,dz),map);mlMade.Add(p);p.equipment?.DestroyAllEquipment();p.apparel?.DestroyAll();if(p.drafter!=null)p.drafter.Drafted=true;return p;}
  static DamageInfo CfMelee(Pawn a)=>new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,a,null,a.equipment?.Primary?.def??DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife"));
  void CfEquip(Pawn p,string def,ThingDef stuff=null){p.equipment.DestroyAllEquipment();var d=DefDatabase<ThingDef>.GetNamed(def);p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(d,d.MadeFromStuff?(stuff??ThingDefOf.Steel):null));}
  void CheckCombatFeats(){
   var s=LichMod.Settings;bool on=s.adventurer;s.adventurer=true;bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;
   Rand.PushState(114003);
   try{CheckCombatFeatsInner();}
   finally{Rand.PopState();s.adventurer=on;Adventurer.TestSuppressed=sup;Feats3.TestRoll=null;MlClean();}
  }
  void CheckCombatFeatsInner(){
   // 1. 定义：6 个战斗专长（图标、附带属性、敌人标签、被擒抱 hediff）；4 个新恩赐。
   {var feats=new[]{"LP_Feat_DualWielder","LP_Feat_ShieldMaster","LP_Feat_PolearmMaster","LP_Feat_CrossbowExpert","LP_Feat_Charger","LP_Feat_Grappler"}.Select(n=>DefDatabase<LP_FeatDef>.GetNamedSilentFail(n)).ToList();
    bool ok=feats.All(f=>f!=null && f.Icon!=BaseContent.BadTex && f.BonusOptions.Count>0 && f.enemyTags!=null && Feats3.Flag(f.defName)!=0 && Feats2.Is(f) && !CasterFeats.IsCasterFeat(f));
    bool boons=Boons2.Resistance!=null && Boons2.Recovery!=null && Boons2.Offense!=null && Boons2.SpellRecall!=null && Epic.Boons.Count()>=10;
    Check(ok && boons && Items.Hediff("LP_FeatGrappled")!=null && feats.Select(f=>Feats3.Flag(f.defName)).Distinct().Count()==6,"[1.140.0] 6 combat feats with icons, +1 ability, NPC tags and distinct flags; 4 new epic boons ("+Epic.Boons.Count()+" total)");}
   // 2. 冲锋者：跑动中近战 ×1.3；擒抱者：命中擒抱，对被擒抱 / 眩晕的目标 ×1.5。
   {var p=CfFighter(-10,-10);var h=Adventurer.Get(p);h.AddFeat(DefDatabase<LP_FeatDef>.GetNamed("LP_Feat_Charger"));h.AddFeat(DefDatabase<LP_FeatDef>.GetNamed("LP_Feat_Grappler"));var i=HeroTraits.Info(p);
    var foe=AdvPawn(null,AdvEnemy);GenSpawn.Spawn(foe,MlCell(-9,-10),map);mlMade.Add(foe);
    float still=Feats3.OutFactor(p,i,foe,CfMelee(p));Traverse.Create(p.pather).Field("lastMovedTick").SetValue(Find.TickManager.TicksGame);float moving=Feats3.OutFactor(p,i,foe,CfMelee(p));
    Feats3.TestRoll=.99f;Feats3.AfterHit(foe,p,i,CfMelee(p));bool free=!Items.Has(foe,"LP_FeatGrappled");Feats3.TestRoll=.1f;Feats3.AfterHit(foe,p,i,CfMelee(p));bool held=Items.Has(foe,"LP_FeatGrappled");
    Traverse.Create(p.pather).Field("lastMovedTick").SetValue(-99999);float helpless=Feats3.OutFactor(p,i,foe,CfMelee(p));
    Check(i!=null && (i.f3 & Feats3.FCharger)!=0 && Near(still,1f) && Near(moving,1.3f) && free && held && Near(helpless,1.5f),"[1.140.0] charger ×1.3 only while moving; grappler holds on a 10% roll (not 99%) and hits the held ×1.5");}
   // 3. 盾牌大师：戴护盾腰带时闪避 +10%、盾击推开；长柄武器大师：长剑（≥2 kg）算长柄，枪尾补一下。
   {var p=CfFighter(-6,-10);var h=Adventurer.Get(p);h.AddFeat(DefDatabase<LP_FeatDef>.GetNamed("LP_Feat_ShieldMaster"));h.AddFeat(DefDatabase<LP_FeatDef>.GetNamed("LP_Feat_PolearmMaster"));var i=HeroTraits.Info(p);
    float noShield=Feats3.DodgeBonus(p);p.apparel.Wear((Apparel)ThingMaker.MakeThing(ThingDefOf.Apparel_ShieldBelt),false);float withShield=Feats3.DodgeBonus(p);
    var foe=AdvPawn(null,AdvEnemy);GenSpawn.Spawn(foe,MlCell(-5,-10),map);mlMade.Add(foe);int sh0=Feats3.Shoved;Feats3.TestRoll=.1f;Feats3.AfterHit(foe,p,i,CfMelee(p));bool shoved=Feats3.Shoved==sh0+1;
    CfEquip(p,"MeleeWeapon_LongSword");bool pole=Feats3.Polearm(p);CfEquip(p,"MeleeWeapon_Knife");bool knife=!Feats3.Polearm(p);CfEquip(p,"MeleeWeapon_LongSword");
    if(!foe.Dead && !foe.Position.AdjacentTo8WayOrInside(p.Position))foe.Position=GenAdj.CellsAdjacent8Way(p).First(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null);
    float f0=foe.Dead?0f:ClassMelee.InjurySum(foe);bool butt=!foe.Dead && Feats3.Butt(p,foe);float f1=foe.Dead?999f:ClassMelee.InjurySum(foe);
    Check(Near(noShield,0f) && Near(withShield,.1f) && shoved && pole && knife && (foe.Dead || butt && f1>f0) && !Items.Has(p,"LP_WhirlwindFury"),"[1.140.0] shield master: +10% dodge and a shove only with a shield belt; polearm master: long sword counts, knife doesn't, butt-end strike lands ("+f0.ToString("0")+" → "+f1.ToString("0")+")");}
   // 4. 弩专家：弓弩算、枪不算；预热 ×0.7、冷却 ×0.8（只对弓弩）。
   {var p=CfFighter(-2,-10);var h=Adventurer.Get(p);h.AddFeat(DefDatabase<LP_FeatDef>.GetNamed("LP_Feat_CrossbowExpert"));
    CfEquip(p,"Bow_Great",ThingDefOf.WoodLog);var bow=p.equipment.Primary;var v=p.equipment.PrimaryEq.PrimaryVerb;int warm=Feats3.Warmup(v,100);float cd=Feats3.Cooldown(v,p,10f);
    CfEquip(p,"Gun_Revolver");var gun=p.equipment.Primary;var gv=p.equipment.PrimaryEq.PrimaryVerb;int gwarm=Feats3.Warmup(gv,100);
    Check(Feats3.Bow(bow) && !Feats3.Bow(gun) && warm==70 && Near(cd,8f) && gwarm==100,"[1.140.0] crossbow expert: great bow warmup 100 → "+warm+", cooldown 10 → "+cd+"; revolver untouched");}
   // 5. 恩赐：能量抗性 ×0.5；不可抗之攻；恢复之恩每天一次治一半；法术追忆消耗 −1。
   {var p=CfFighter(2,-10);var h=Adventurer.Get(p);h.boonPoints=4;h.TakeBoon(Boons2.Resistance);h.TakeBoon(Boons2.Recovery);h.TakeBoon(Boons2.Offense);h.TakeBoon(Boons2.SpellRecall);
    float fire=Boons2.InFactor(p,new DamageInfo(DamageDefOf.Flame,10f)),cut=Boons2.InFactor(p,new DamageInfo(DamageDefOf.Cut,10f));bool off=Boons2.Offensive(p,CfMelee(p));
    S6Hurt(p,40f);float i0=ClassMelee.InjurySum(p);Boons2.Tick250(h);float i1=ClassMelee.InjurySum(p);S6Hurt(p,40f);float i2=ClassMelee.InjurySum(p);Boons2.Tick250(h);float i3=ClassMelee.InjurySum(p);
    Check(h.boons.Count==4 && Near(fire,.5f) && Near(cut,1f) && off && i1<i0*.6f && Math.Abs(i2-i3-1f)<.3f && Boons2.CostReduce(p,3)==1 && Boons2.CostReduce(p,1)==0,"[1.140.0] boons: energy resistance halves fire not cut, irresistible offense on, recovery heals half once a day then 1 per tick ("+i0.ToString("0")+" → "+i1.ToString("0")+", "+i2.ToString("0")+" → "+i3.ToString("0")+"), spell recall −1 (min 1)");}
   // 6. 施法奖励专长：法师 1 级 1 个、5 级再 1 个；只能换法术类专长；法术类专长先用它；战士奖励不受影响。
   {var p=CfFighter(6,-10);var h=Adventurer.Get(p);var wiz=DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Wizard");
    ClassSystem.SetLevel(h,wiz,1);int c1=h.casterFeats;ClassSystem.SetLevel(h,wiz,5);int c5=h.casterFeats;
    string why=h.CanTakeFeat(DefDatabase<LP_FeatDef>.GetNamed("LP_Feat_GreatWeaponMaster"));var wc=DefDatabase<LP_FeatDef>.GetNamed("LP_Feat_Alert");bool took=h.CanTakeFeat(wc)==null && h.TakeFeat(wc);int after=h.casterFeats;
    h.pendingASI=1;string gwmWhy=h.CanTakeFeat(DefDatabase<LP_FeatDef>.GetNamed("LP_Feat_Mobile"));bool gwm=gwmWhy==null;
    var levels=ClassSystem.BonusFeatLevels(wiz);bool info=ClassLevelInfo_Has(wiz,5,"施法奖励专长");
    Check(c1==1 && c5==2 && why!=null && why.Contains("法术类") && took && after==1 && gwm && levels!=null && levels.SequenceEqual(CasterFeats.Levels) && info && Letter_AdventurerLevel.Pending(h).Contains("施法奖励专长"),"[1.140.0] caster bonus feats: wizard 1 → "+c1+", 5 → "+c5+"; non-spell feat refused ("+why+"); 警觉 uses the caster pick ("+took+", left "+after+"); an ASI still buys any feat ("+gwmWhy+"); level info "+info+", letter "+Letter_AdventurerLevel.Pending(h));}
   // 7. NPC 施法者立刻随机选法术类专长；非施法职业没有。
   {var e=AdvPawn(null,AdvEnemy);var he=F2Adv(e);he.pendingASI=0;he.fighterFeats=0;int n0=he.feats.Count;var cle=DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Cleric");
    ClassSystem.SetLevel(he,cle,10);ClassSystem.SyncFighterFeats(he);bool npc=he.casterFeats==0 && he.feats.Count==n0+3 && he.feats.Skip(n0).All(CasterFeats.IsCasterFeat);
    var m=CfFighter(10,-10);var hm=Adventurer.Get(m);ClassSystem.SetLevel(hm,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Barbarian"),5);
    Check(npc && hm.casterFeats==0 && CasterFeats.CasterLevel(hm)==0,"[1.140.0] NPC cleric 10 auto-picks 3 spell feats ("+string.Join(",",he.feats.Skip(n0).Select(f=>f.defName))+"); a barbarian gets none");
    e.Destroy();}
  }
  static bool ClassLevelInfo_Has(LP_ClassDef c,int level,string text){try{return ClassLevelInfo.GainsAt(null,c,level).Any(x=>x.Contains(text));}catch{return false;}}
  void PrepareCombatFeatsSave(){var p=CfFighter(14,14);var h=Adventurer.Get(p);var wiz=DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Wizard");ClassSystem.SetLevel(h,wiz,1);h.AddFeat(DefDatabase<LP_FeatDef>.GetNamed("LP_Feat_Grappler"));cfId=p.ThingID;mlMade.Remove(p);}
  void CheckCombatFeatsAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==cfId);var h=p==null?null:Adventurer.Get(p);
   Check(h!=null && h.casterFeats==1 && h.casterFeatGranted==1 && Feats3.Has(p,Feats3.FGrappler),"[1.140.0] after load: caster bonus feat and grappler flag survive");
   if(p!=null)p.Destroy();
  }
 }
}
