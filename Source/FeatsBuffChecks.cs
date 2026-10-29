using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.67.0：Test.ps1 -Only FeatsBuff（专长加强：附带 +1 属性、随熟练加值成长、8 个新专长）。旧数值的检查在 Adventurer / Feats2 组里一起改了。
 public partial class LichChecks {
  static string fbId,fbSaved;
  void CheckFeatsBuff(){
   var s=LichMod.Settings;bool oOn=s.adventurer;bool oSup=Adventurer.TestSuppressed;
   Adventurer.TestSuppressed=false;s.adventurer=true;
   try{
    // 1. 定义：每个专长都写了附带属性；8 个新专长；妖精之触的长跳能力在
    var all=DefDatabase<LP_FeatDef>.AllDefsListForReading;var news=new[]{"LP_Feat_Slasher","LP_Feat_Piercer","LP_Feat_Crusher","LP_Feat_MageSlayer","LP_Feat_DefensiveDuelist","LP_Feat_WeaponMaster","LP_Feat_Durable","LP_Feat_FeyTouched"}.Select(n=>DefDatabase<LP_FeatDef>.GetNamedSilentFail(n)).ToList();
    Check(all.All(f=>f.BonusOptions.Count>0) && news.All(f=>f!=null && f.Icon!=BaseContent.BadTex) && FD("LP_Feat_FeyTouched").grantAbility!=null,"every feat has a +1 ability score; 8 new feats with icons; 妖精之触 grants an ability (missing bonus: "+string.Join(",",all.Where(f=>f.BonusOptions.Count==0).Select(f=>f.defName))+")");
    // 2. +1 加在可选项里最高的一项，并且加在上限之后（20 → 21）
    var a=HtPawn();var ha=F2Adv(a);ha.scores=new[]{14,16,12,10,10,10};ha.Dirty();int e0=ha.Eff(Adventurer.Dex),s0=ha.Eff(Adventurer.Str);
    ha.AddFeat(Feats.Sentinel);int e1=ha.Eff(Adventurer.Dex),s1=ha.Eff(Adventurer.Str);
    ha.scores[Adventurer.Dex]=20;int capped=ha.Eff(Adventurer.Dex)-Math.Min(20,ha.Cap(Adventurer.Dex))-CombatGifts.ScoreBonus(ha,Adventurer.Dex);
    Check(e1==e0+1 && s1==s0 && ha.featScore.TryGetValue("LP_Feat_Sentinel",out int fs) && fs==Adventurer.Dex && capped==1 && Feats.ScoreTag(ha,Adventurer.Dex).StartsWith("+"),"哨兵 (Str/Dex) adds +1 to the higher Dex (Dex "+e0+" → "+e1+", Str unchanged) and on top of the cap (20 → +"+capped+")");
    // 3. 失去专长收回；旧存档补选
    ha.RemoveFeat(Feats.Sentinel);bool gone=!ha.featScore.ContainsKey("LP_Feat_Sentinel") && Feats.ScoreBonus(ha,Adventurer.Dex)==0;
    ha.scores=new[]{10,10,10,10,15,10};ha.AddFeat(Feats.Alert);ha.featScore.Clear();Feats.FixScores(ha);
    Check(gone && ha.featScore.TryGetValue("LP_Feat_Alert",out int fa) && fa==Adventurer.Wis,"losing a feat removes its +1; old saves get it assigned on load (警觉 → Wis 15 over Dex 10)");
    // 4. 幸运次数 = 熟练加值；坚韧部位生命 +2%/级
    var b=HtPawn();var hb=F2Adv(b);hb.AddFeat(Feats.Lucky);hb.AddFeat(Feats.Tough);hb.level=1;int u1=hb.UsesPerDay(Feats.Lucky);hb.level=20;int u20=hb.UsesPerDay(Feats.Lucky);
    float tf=Feats.ToughFactor(hb);hb.level=40;float tf40=Feats.ToughFactor(hb);hb.level=20;
    Check(u1==2 && u20==6 && Near(tf,1.4f) && Near(tf40,1.6f),"幸运: 2 uses at level 1, 6 at level 20; 坚韧: part health ×"+tf+" at level 20, ×"+tf40+" at level 40 (capped at 30)");
    // 5. 巨武器大师双手近战 ×1.25；神射手远程武器 ×1.15
    var heavy=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.IsMeleeWeapon && !d.MadeFromStuff && d.equipmentType==EquipmentType.Primary && d.GetStatValueAbstract(StatDefOf.Mass)>=Feats.TwoHandedMass && !d.destroyOnDrop).OrderBy(d=>d.GetStatValueAbstract(StatDefOf.Mass)).FirstOrDefault();
    var g=HtPawn(null,null,true,6,6);var hg=F2Adv(g);hg.AddFeat(Feats.GreatWeapon);hg.AddFeat(Feats.Sharpshooter);g.equipment.DestroyAllEquipment();if(heavy!=null)g.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(heavy));
    var foe=HtPawn(null,AdvEnemy,true,7,6);var rifle=DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle");
    float gw=Feats2.OutFactor(g,HeroTraits.Info(g),foe,new DamageInfo(DamageDefOf.Blunt,10f,0f,-1f,g,null,heavy));
    float sh=Feats2.OutFactor(g,HeroTraits.Info(g),foe,new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,g,null,rifle));
    Check(heavy!=null && Near(gw,Feats.GreatWeaponDamage) && Near(sh,Feats.SharpshooterDamage),"巨武器大师: two-handed melee ×"+gw+"; 神射手: ranged weapon ×"+sh);
    // 6. 斩 / 刺 / 碎：对应伤害 ×1.2，穿刺者 20% 双倍；三选一
    var w=HtPawn(null,null,true,6,8);var hw=F2Adv(w);hw.AddFeat(FD("LP_Feat_Piercer"));var knife=ThingDefOf.MeleeWeapon_Knife;
    float stab,crit,cut;Feats2.TestRoll=.9f;try{stab=Feats2.OutFactor(w,HeroTraits.Info(w),foe,new DamageInfo(DamageDefOf.Stab,10f,0f,-1f,w,null,knife));cut=Feats2.OutFactor(w,HeroTraits.Info(w),foe,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,w,null,knife));}finally{Feats2.TestRoll=null;}
    Feats2.TestRoll=0f;try{crit=Feats2.OutFactor(w,HeroTraits.Info(w),foe,new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,w,null,rifle));}finally{Feats2.TestRoll=null;}
    hw.pendingASI=1;w.skills.GetSkill(SkillDefOf.Melee).Level=10;
    Check(Near(stab,1.2f) && Near(cut,1f) && Near(crit,2.4f) && hw.CanTakeFeat(FD("LP_Feat_Slasher"))?.Contains("同类")==true,"穿刺者: stab ×"+stab+", cut ×"+cut+", a lucky bullet ×"+crit+"; 斩击者 is refused afterwards (one of three)");
    // 7. 法师杀手：对施法者 ×1.3，对普通敌人不加
    var m=HtPawn(null,null,true,6,10);F2Adv(m).AddFeat(FD("LP_Feat_MageSlayer"));
    var wiz=AdvKind(DefDatabase<PawnKindDef>.GetNamed("LP_FH_RedWizard"),Factions.Hostile??AdvEnemy);
    float vsWiz=Feats2.OutFactor(m,HeroTraits.Info(m),wiz,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,m,null,knife));
    float vsFoe=Feats2.IsMage(foe)?1.3f:Feats2.OutFactor(m,HeroTraits.Info(m),foe,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,m,null,knife));
    Check(Feats2.IsMage(wiz) && Near(vsWiz,1.3f) && Near(vsFoe,Feats2.IsMage(foe)?1.3f:1f),"法师杀手: ×"+vsWiz+" against a red wizard, ×"+vsFoe+" against an ordinary raider");
    // 8. 数值专长合进冒险者状态：耐久伤口愈合 ×1.5、防御决斗者近战闪避 +15
    var dur=HtPawn();var hd=F2Adv(dur);hd.Dirty();float heal0=hd.CurStage.statFactors?.GetStatFactorFromList(StatDefOf.InjuryHealingFactor)??1f,dodge0=hd.CurStage.statOffsets?.GetStatOffsetFromList(StatDefOf.MeleeDodgeChance)??0f;
    hd.AddFeat(FD("LP_Feat_Durable"));hd.AddFeat(FD("LP_Feat_DefensiveDuelist"));hd.featScore.Clear();hd.Dirty(); // 去掉附带的体质/敏捷 +1，只量专长本身
    float heal1=hd.CurStage.statFactors?.GetStatFactorFromList(StatDefOf.InjuryHealingFactor)??1f,dodge1=hd.CurStage.statOffsets?.GetStatOffsetFromList(StatDefOf.MeleeDodgeChance)??0f;
    Check(heal0>0f && Near(heal1/heal0,1.5f) && dodge1-dodge0>=15f-.01f,"耐久: injury healing ×"+(heal1/heal0).ToString("0.##")+"; 防御决斗者: melee dodge +"+(dodge1-dodge0).ToString("0.#"));
    // 9. 妖精之触：获得长跳，失去收回
    var fey=HtPawn();var hf=F2Adv(fey);var jump=FD("LP_Feat_FeyTouched").grantAbility;hf.AddFeat(FD("LP_Feat_FeyTouched"));bool got=fey.abilities.GetAbility(jump)!=null;hf.RemoveFeat(FD("LP_Feat_FeyTouched"));
    Check(got && fey.abilities.GetAbility(jump)==null,"妖精之触: gains "+jump?.label+", losing the feat removes it");
   }finally{
    s.adventurer=oOn;Feats2.TestRoll=null;
    foreach(var p in htMade){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();else if(!p.Destroyed)p.Destroy();}htMade.Clear();
    Adventurer.TestSuppressed=oSup;
   }
  }
  // 10. 存读档：专长附带的 +1 记在哪项
  void PrepareFeatsBuffSave(){
   Adventurer.TestSuppressed=false;
   try{
    var p=HtPawn(null,null,true,2,-6);var h=F2Adv(p);h.scores=new[]{10,10,10,10,10,15};h.AddFeat(Feats.Leader);h.AddFeat(Feats.Tough);
    fbId=p.ThingID;fbSaved=string.Join(",",h.featScore.OrderBy(kv=>kv.Key).Select(kv=>kv.Key+"="+kv.Value));htMade.Clear();
   }finally{Adventurer.TestSuppressed=true;}
  }
  void CheckFeatsBuffAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==fbId);var h=Adventurer.Get(p);
   string now=h==null?"missing":string.Join(",",h.featScore.OrderBy(kv=>kv.Key).Select(kv=>kv.Key+"="+kv.Value));
   Check(now==fbSaved && fbSaved.Contains("LP_Feat_InspiringLeader=5") && fbSaved.Contains("LP_Feat_Tough=2"),"feat +1 ability choices survive save/load ("+fbSaved+")");
   p?.Destroy();
  }
 }
}
