using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.66.0：Test.ps1 -Only CombatGifts（武艺天赋，15 项）。随机都走显式的 roll 参数，CombatGifts.TestSuppressed 保持打开（不发信、不随机生成）。
 public partial class LichChecks {
  static float CgOff(Hediff_Adventurer h,string stat){var l=h.CurStage?.statOffsets;var m=l?.FirstOrDefault(x=>x.stat?.defName==stat);return m?.value??0f;}
  static float CgFac(Hediff_Adventurer h,string stat){var l=h.CurStage?.statFactors;var m=l?.FirstOrDefault(x=>x.stat?.defName==stat);return m?.value??1f;}
  static void CgLevel(Hediff_Adventurer h,int level,bool legendary){h.level=level;h.legendary=legendary;h.Dirty();}

  void CheckCombatGifts(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oCo=s.adventurerIsekaiCoexist,oCg=s.combatGifts,oCe=s.combatGiftsEnemies;int oRate=s.combatGiftRate;
   bool oSup=Adventurer.TestSuppressed,oHsup=HeroTraits.TestSuppressed,oCsup=CombatGifts.TestSuppressed;
   Adventurer.TestSuppressed=false;CombatGifts.TestSuppressed=true;
   s.adventurer=true;s.adventurerIsekaiCoexist=false;s.combatGifts=true;s.combatGiftsEnemies=true;s.combatGiftRate=8;
   var comp=GameComponent_CombatGifts.Get;bool oBack=comp?.backfilled??true;
   try{
    // 1. 定义：13 个，commonality 0，独立名额 LP_CombatGift（不占 LP_PowerTrait），蓝色标签；设置默认值；设置分组仍是 19 个
    var defs=CombatGifts.All;var ns=new Settings();
    Check(defs.Length==13 && defs.All(d=>d!=null && Near(d.GetGenderSpecificCommonality(Gender.Male),0f) && d.exclusionTags.NullOrEmpty() && d.degreeDatas.All(x=>x.label.Contains("#5B9BD5"))) && ns.combatGifts && ns.combatGiftsEnemies && ns.combatGiftRate==8 && ns.combatGiftMax==3 && ns.combatGiftExtra==25 && LichMod.SettingsLeft.Length+LichMod.SettingsRight.Length==19 && comp!=null,"13 combat gifts load: commonality 0, no exclusion tag (up to 3 per pawn, 25% extra), #5B9BD5 labels; settings default on / on / 8%; still 19 settings groups");
    // 2. 名额：每人最多 1 个，新的顶掉旧的
    var one=HtPawn();CombatGifts.Give(one,CombatGiftDefOf.LP_CG_Mighty);CombatGifts.Give(one,CombatGiftDefOf.LP_CG_Flurry);
    int two=CombatGifts.CountOf(one);bool bothKept=CombatGifts.HasGift(one,CombatGifts.Mighty) && CombatGifts.HasGift(one,CombatGifts.Flurry);
    int oMax=s.combatGiftMax;s.combatGiftMax=2;CombatGifts.Give(one,CombatGiftDefOf.LP_CG_Lethal);int capped=CombatGifts.CountOf(one);bool swapped=CombatGifts.HasGift(one,CombatGifts.Lethal) && !CombatGifts.HasGift(one,CombatGifts.Mighty);s.combatGiftMax=oMax;
    Check(two==2 && bothKept && capped==2 && swapped && one.story.traits.allTraits.Count(t=>CombatGifts.Is(t.def))==2,"several combat gifts: 巨力 + 连斩 both kept (cache mask follows); at the cap (2) a third (致命一击) replaces the oldest (巨力)");
    // 3. 和强力特性并存：先有英雄之资再得武艺天赋，再换强力特性也不碰武艺天赋
    var both=HtPawn();HeroTraits.Give(both,HeroDefOf.LP_HeroicSpark);CombatGifts.Give(both,CombatGiftDefOf.LP_CG_Lethal);HeroTraits.Give(both,HeroDefOf.LP_Edge);
    Check(HeroTraits.Has(both,HeroDefOf.LP_Edge) && CombatGifts.Gift(both)==CombatGifts.Lethal && HeroTraits.Has(both,HeroDefOf.LP_HeroicSpark) && HeroTraits.Count(both)==2,"a combat gift coexists with several power traits (英雄之资 + 锋芒 + 致命一击)");
    // 4. 属性 +2 / +3 / +4，加在上限之后（上限同样提高），走 1.65.0 倍率曲线
    var mi=HtPawn();CombatGifts.Give(mi,CombatGiftDefOf.LP_CG_Mighty);var hm=HtAdv(mi);hm.scores=new[]{20,10,10,10,10,10};CgLevel(hm,1,false);
    int e1=hm.Eff(0);CgLevel(hm,10,false);int e2=hm.Eff(0);CgLevel(hm,20,true);int e3=hm.Eff(0);
    Check(e1==22 && e2==23 && e3==24 && hm.Cap(0)==Adventurer.ScoreCap && !hm.CanRaise(0,1) && hm.Eff(1)==10 && Near(Adventurer.Curve(hm.Eff(0)),7f) && CombatGifts.ScoreTag(hm,0)=="+4","巨力 at 20 strength: effective "+e1+" / "+e2+" / "+e3+" (+2/+3/+4 over the cap), base cap stays 20, curve ×"+Adventurer.Curve(e3).ToString("0.##"));
    // 5. 阶段：1 级入门、10 级精通、传奇化境；巨灵升阶 +1；迅捷移速 +0.4 / +0.6 / +0.8
    var sw=HtPawn();CombatGifts.Give(sw,CombatGiftDefOf.LP_CG_Swift);var hs=HtAdv(sw);CgLevel(hs,1,false);
    int s0=CombatGifts.Stage(hs);float m0=CgOff(hs,"MoveSpeed");CgLevel(hs,10,false);int s1=CombatGifts.Stage(hs);float m1=CgOff(hs,"MoveSpeed");CgLevel(hs,20,true);int s2=CombatGifts.Stage(hs);float m2=CgOff(hs,"MoveSpeed");
    CgLevel(hs,1,false);hs.cgBonus=1;int sb=CombatGifts.Stage(hs);hs.cgBonus=0;hs.Dirty();
    string tip=CombatGifts.ExtraTip(sw,CombatGifts.GiftTrait(sw));
    Check(s0==0 && s1==1 && s2==2 && sb==1 && Near(m0,.4f) && Near(m1,.6f) && Near(m2,.8f) && tip.Contains("入门") && tip.Contains("精通") && tip.Contains("化境"),"stages: level 1 = 入门, 10 = 精通, legendary = 化境, genie bonus +1 stage; 迅捷 move speed +"+m0+" / +"+m1+" / +"+m2+"; tooltip lists all three stages");
    // 6. 连斩：近战冷却 ×0.8；命中后成功率内排队追加一击，追加那一击打得出来；成功率外不追加
    var fl=HtPawn(null,null,true,0,0);CombatGifts.Give(fl,CombatGiftDefOf.LP_CG_Flurry);var hf=HtAdv(fl);CgLevel(hf,1,false);var foe=HtPawn(null,AdvEnemy,true,1,0);
    var hit=new DamageInfo(DamageDefOf.Blunt,3f,0f,-1f,fl,null,fl.def);var res=new DamageWorker.DamageResult{totalDamageDealt=3f};
    CombatGifts.pending.Clear();CombatGifts.TestRoll=.1f;bool q1=CombatGifts.AfterHit(foe,hit,res);int pend=CombatGifts.pending.Count;CombatGifts.TestRoll=.2f;bool q2=CombatGifts.AfterHit(foe,hit,res);CombatGifts.TestRoll=null;CombatGifts.pending.Clear();
    int f0=CombatGifts.Flurries;int inj0=foe.health.hediffSet.hediffs.Count;bool now=CombatGifts.FlurryNow(fl,foe);
    Check(Near(CgFac(hf,"MeleeCooldownFactor"),.8f) && q1 && pend==1 && !q2 && now && CombatGifts.Flurries==f0+1,"连斩: melee cooldown ×"+CgFac(hf,"MeleeCooldownFactor").ToString("0.##")+", a hit queues an extra strike at roll 0.10 (< 15%), not at 0.20; the extra strike lands ("+inj0+" → "+foe.health.hediffSet.hediffs.Count+" hediffs)");
    // 7. 致命一击：成功率内伤害 ×2（化境 ×2.5），护甲穿透 +10%；成功率外只加穿透
    var le=HtPawn();CombatGifts.Give(le,CombatGiftDefOf.LP_CG_Lethal);var hl=HtAdv(le);CgLevel(hl,1,false);var tgt=HtPawn(null,AdvEnemy);
    var d1=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,le,null,null);CombatGifts.TestRoll=.05f;CombatGifts.OnDamage(tgt,ref d1);
    var d2=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,le,null,null);CombatGifts.TestRoll=.5f;CombatGifts.OnDamage(tgt,ref d2);
    CgLevel(hl,20,true);var d3=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,le,null,null);CombatGifts.TestRoll=.15f;CombatGifts.OnDamage(tgt,ref d3);CombatGifts.TestRoll=null;
    Check(Near(d1.Amount,20f) && Near(d1.ArmorPenetrationInt,.1f) && Near(d2.Amount,10f) && Near(d2.ArmorPenetrationInt,.1f) && Near(d3.Amount,25f) && Near(d3.ArmorPenetrationInt,.2f),"致命一击: crit at roll 0.05 → "+d1.Amount+" (AP "+d1.ArmorPenetrationInt+"), no crit at 0.5 → "+d2.Amount+", 化境 crit at 0.15 (< 20%) → "+d3.Amount+" (AP "+d3.ArmorPenetrationInt+")");
    // 8. 狂暴之血：健康低于 60% 时触发（近战伤害 ×1.3、冷却 ×0.85、疼痛 ×0.5），回满后解除
    var ra=HtPawn();CombatGifts.Give(ra,CombatGiftDefOf.LP_CG_Rage);var hr=HtAdv(ra);CgLevel(hr,1,false);CombatGifts.Tick250(hr);bool calm=!hr.cgRage && Near(CgFac(hr,"MeleeDamageFactor"),1f);
    var limbs=ra.health.hediffSet.GetNotMissingParts().Where(x=>!CombatGifts.Vital(x) && x.depth==BodyPartDepth.Outside && x.def.GetMaxHealth(ra)>=8f).ToList();
    for(int pass=0;pass<3 && ra.health.summaryHealth.SummaryHealthPercent>=CombatGifts.RageHealth;pass++)foreach(var part in limbs){
     if(ra.health.summaryHealth.SummaryHealthPercent<CombatGifts.RageHealth)break;
     float room=ra.health.hediffSet.GetPartHealth(part)-2f;if(room<1f)continue;
     var b=HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Bruise"),ra,part);b.Severity=Mathf.Min(room,6f);ra.health.AddHediff(b,part);
    }
    float hp=ra.health.summaryHealth.SummaryHealthPercent;CombatGifts.Tick250(hr);bool mad=hr.cgRage;float rd=CgFac(hr,"MeleeDamageFactor"),rc=CgFac(hr,"MeleeCooldownFactor"),rp=hr.CurStage.painFactor;
    foreach(var inj in ra.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())ra.health.RemoveHediff(inj);CombatGifts.Tick250(hr);
    Check(calm && hp<CombatGifts.RageHealth && mad && Near(rd,1.3f) && Near(rc,.85f) && Near(rp,.5f) && !hr.cgRage && Near(CgFac(hr,"MeleeDamageFactor"),1f),"狂暴之血: calm at full health, rages at "+(hp*100f).ToString("0")+"% health (melee damage ×"+rd.ToString("0.##")+", cooldown ×"+rc.ToString("0.##")+", pain ×"+rp.ToString("0.##")+"), calms again when healed");
    // 9. 铁壁：利器 / 钝器护甲 +15%（化境 +30%，封顶），踉跄时间 ×0
    var bw=HtPawn();CombatGifts.Give(bw,CombatGiftDefOf.LP_CG_Bulwark);var hb=HtAdv(bw);CgLevel(hb,1,false);float a1=CgOff(hb,"ArmorRating_Sharp"),a1b=CgOff(hb,"ArmorRating_Blunt");CgLevel(hb,20,true);float a2=CgOff(hb,"ArmorRating_Sharp");
    float stag=bw.GetStatValue(DefDatabase<StatDef>.GetNamed("StaggerDurationFactor"));
    Check(Near(a1,.15f) && Near(a1b,.15f) && Near(a2,.3f) && Near(CombatGifts.ArmorBonus(5f),CombatGifts.ArmorCap) && stag<=.001f,"铁壁: sharp/blunt armor +"+a1+" (化境 +"+a2+", capped at +30%), stagger duration ×"+stag);
    // 10. 再生之体：伤口愈合 ×2.5；缺手 15 天后长回（差一刻不长）
    var rg=HtPawn();CombatGifts.Give(rg,CombatGiftDefOf.LP_CG_Regen);var hg=HtAdv(rg);CgLevel(hg,1,false);
    var hand=rg.RaceProps.body.AllParts.First(x=>x.def.defName=="Hand");var mp=(Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,rg,hand);mp.IsFresh=false;rg.health.AddHediff(mp,hand);
    int t0=GenTicks.TicksGame;var r0=CombatGifts.RegenTick(hg,t0);bool missing=CombatGifts.MissingNonVital(rg)==hand;var r1=CombatGifts.RegenTick(hg,t0+CombatGifts.RegenInterval(0)-1);var r2=CombatGifts.RegenTick(hg,t0+CombatGifts.RegenInterval(0));
    Check(Near(CgFac(hg,"InjuryHealingFactor"),2.5f) && missing && r0==null && r1==null && r2==hand && !rg.health.hediffSet.PartIsMissing(hand) && hg.cgRegenTick<0==false && CombatGifts.MissingNonVital(rg)==null && CombatGifts.RegenInterval(2)==6*GenDate.TicksPerDay,"再生之体: healing ×"+CgFac(hg,"InjuryHealingFactor").ToString("0.##")+", a missing hand grows back after exactly 15 days (化境 6 days)");
    // 11. 法力奔涌 / 神射：法术伤害 +25%、远程伤害 +15%，近战不加；心灵敏感度或心灵熵上限
    var ar=HtPawn();CombatGifts.Give(ar,CombatGiftDefOf.LP_CG_Arcane);var ha=HtAdv(ar);CgLevel(ha,1,false);var de=HtPawn();CombatGifts.Give(de,CombatGiftDefOf.LP_CG_Deadeye);var hd=HtAdv(de);CgLevel(hd,1,false);
    var knife=ThingDefOf.MeleeWeapon_Knife;var gun=DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle");
    var sp=new DamageInfo(DamageDefOf.Burn,10f,0f,-1f,ar,null,null);CombatGifts.OnDamage(tgt,ref sp);var sk=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,ar,null,knife);CombatGifts.OnDamage(tgt,ref sk);
    var rb=new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,de,null,gun);CombatGifts.OnDamage(tgt,ref rb);var rk=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,de,null,knife);CombatGifts.OnDamage(tgt,ref rk);
    bool psy=HeroDestiny.VPEActive?Near(CgOff(ha,"PsychicEntropyMax"),20f):Near(CgOff(ha,"PsychicSensitivity"),.15f);
    Check(Near(sp.Amount,12.5f) && Near(sk.Amount,10f) && Near(rb.Amount,11.5f) && Near(rk.Amount,10f) && psy && Near(CgFac(hd,"ShootingAccuracyPawn")/CgFac(ha,"ShootingAccuracyPawn"),1.12f),"法力奔涌 spell "+sp.Amount+" / knife "+sk.Amount+", "+(HeroDestiny.VPEActive?"entropy max +20":"psychic sensitivity +15%")+"; 神射 bullet "+rb.Amount+" / knife "+rk.Amount+", shooting accuracy ×1.12");
    // 12. 获得率：开局 35%、普通 8%、传奇种族 4%、传奇怪 50%；固定种子 10000 次在 ±25% 内；生成后缀已挂上；首领 50%
    var human=HtPawn();var giant=HtPawn("LP_FireGiant");float ch=CombatGifts.ChanceFor(human,false),cgt=CombatGifts.ChanceFor(giant,false),cst=CombatGifts.ChanceFor(human,true);
    int n=0;Rand.PushState(16601);try{for(int i=0;i<10000;i++)if(Rand.Value<ch)n++;}finally{Rand.PopState();}
    var gen=HtPawn();var g1=CombatGifts.OnGenerated(gen,false,.07f);var gen2=HtPawn();var g2=CombatGifts.OnGenerated(gen2,false,.09f);var bossA=HtPawn(null,AdvEnemy);var bt=CombatGifts.Boss(bossA,.4f);var bossB=HtPawn(null,AdvEnemy);var bn=CombatGifts.Boss(bossB,.6f);
    var pinfo=Harmony.GetPatchInfo(AccessTools.Method(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)}));
    Check(Near(ch,.08f) && Near(cgt,.04f) && Near(cst,.35f) && n>=600 && n<=1000 && g1!=null && g2==null && bt!=null && bn==null && pinfo!=null && pinfo.Postfixes.Any(x=>x.PatchMethod.DeclaringType==typeof(CombatGiftGenerate)),"rates: human 8% ("+n+"/10000 seeded), fire giant 4%, starting colonist 35%; roll 0.07 gives one, 0.09 does not; bosses at 0.4 yes / 0.6 no; generation postfix patched");
    // 13. 战斗觉醒：100 / 300 杀各一次；巨灵：没有就给，已有升一阶，化境后不能再许
    var kl=HtPawn();var hk=HtAdv(kl);CgLevel(hk,1,false);kl.records.AddTo(RecordDefOf.KillsHumanlikes,100);int k1=CombatGifts.CheckKills(kl,0f);bool got=CombatGifts.Has(kl);
    kl.records.AddTo(RecordDefOf.KillsHumanlikes,200);int k2=CombatGifts.CheckKills(kl,0f);int k3=CombatGifts.CheckKills(kl,0f);
    var aw=HtPawn();var aw1=CombatGifts.Awaken(aw,CombatGifts.MilestoneChance,"测试",.3f);var aw2=CombatGifts.Awaken(aw,CombatGifts.MilestoneChance,"测试",.2f);
    var wi=HtPawn();var hw=HtAdv(wi);CgLevel(hw,1,false);bool w1=CombatGifts.Wish(wi);int wg=CombatGifts.Gift(wi);bool w2=CombatGifts.Wish(wi);bool w3=CombatGifts.Wish(wi);int oMax2=s.combatGiftMax;s.combatGiftMax=1;string full=CombatGifts.CannotWish(wi);s.combatGiftMax=oMax2;bool more=CombatGifts.CannotWish(wi)==null && CombatGifts.Wish(wi) && CombatGifts.CountOf(wi)==2;
    Check(k1==100 && got && k2==300 && k3==0 && hk.milestonesDone.Contains("kills_cg100") && hk.milestonesDone.Contains("kills_cg300") && !Epic.DndMilestone("kills_cg100") && aw1==null && aw2!=null && w1 && wg>=0 && w2 && w3 && CombatGifts.Stage(hw)==2 && CombatGifts.HasGift(wi,wg) && full!=null && more,"awakening: 100 kills at roll 0 gives a gift, 300 is marked once (not a DND milestone key); milestone awakening 25% (0.3 no, 0.2 yes); genie gives one then raises it to 化境; at the cap it refuses ("+full+"), below the cap a 化境 pawn gets a second gift");
    // 14. 老存档：补掷只给没有天赋的殖民者，标记之后不再补；设置关闭后哪里都不生成；敌人开关
    var o1=HtPawn();var o2=HtPawn();var o3=HtPawn();CombatGifts.Give(o3,CombatGiftDefOf.LP_CG_Eagle);
    var bf1=CombatGifts.Backfill(new[]{o1,o2,o3},()=>0f);var bf2=CombatGifts.Backfill(new[]{o1,o2,o3},()=>0f);
    comp.backfilled=false;comp.LoadedGame();bool flag1=comp.backfilled;var o4=HtPawn();comp.LoadedGame();bool untouched=!CombatGifts.Has(o4);
    s.combatGifts=false;var off1=CombatGifts.OnGenerated(HtPawn(),false,0f);var off2=CombatGifts.Boss(HtPawn(null,AdvEnemy),0f);var off3=CombatGifts.Awaken(HtPawn(),1f,"测试",0f);var off4=CombatGifts.Backfill(new[]{HtPawn()},()=>0f);s.combatGifts=true;
    s.combatGiftsEnemies=false;var eo=CombatGifts.OnGenerated(HtPawn(null,AdvEnemy),false,0f);var co=CombatGifts.OnGenerated(HtPawn(),false,0f);s.combatGiftsEnemies=true;
    Check(bf1.Count==2 && bf2.Count==0 && CombatGifts.Gift(o3)==CombatGifts.Eagle && flag1 && untouched && off1==null && off2==null && off3==null && off4.Count==0 && eo==null && co!=null,"old saves: backfill gives the 2 colonists without a gift ("+bf1.Count+"), a second pass gives none, LoadedGame only once (flag); setting off → nothing anywhere; enemies off → hostile pawn skipped, colonist still rolls");
    // 15. 敌人战力：+6% / 9% / 12%；袭击组多出来的战力够半个人就裁掉一个没天赋的
    var p0=HtPawn(null,AdvEnemy);var p1=HtPawn(null,AdvEnemy);CombatGifts.Give(p1,CombatGiftDefOf.LP_CG_Rage);var hp1=HtAdv(p1);CgLevel(hp1,1,false);float pf0=CombatGifts.PowerFactor(p0),pf1=CombatGifts.PowerFactor(p1);
    CgLevel(hp1,10,false);float pf2=CombatGifts.PowerFactor(p1);CgLevel(hp1,20,true);float pf3=CombatGifts.PowerFactor(p1);
    var raid=new List<Pawn>{p1};for(int i=0;i<4;i++){var x=HtPawn(null,AdvEnemy);CombatGifts.Give(x,CombatGiftDefOf.LP_CG_Swift);CgLevel(HtAdv(x),20,true);raid.Add(x);}
    var plainA=HtPawn(null,AdvEnemy);var plainB=HtPawn(null,AdvEnemy);raid.Add(plainA);raid.Add(plainB);float cp=p0.kindDef.combatPower;int cut=CombatGifts.Trim(raid);
    Check(Near(pf0,1f) && Near(pf1,1.06f) && Near(pf2,1.09f) && Near(pf3,1.12f) && cp>0f && cut==1 && raid.Count==6 && raid.All(x=>CombatGifts.Has(x) || x==plainA),"enemy combat power ×"+pf1+" / ×"+pf2+" / ×"+pf3+"; 5 化境 gifted raiders (+60% of one "+cp+"-point pawn) trim exactly the last plain raider");
   }finally{
    s.adventurer=oOn;s.adventurerIsekaiCoexist=oCo;s.combatGifts=oCg;s.combatGiftsEnemies=oCe;s.combatGiftRate=oRate;
    CombatGifts.TestRoll=null;CombatGifts.pending.Clear();if(comp!=null)comp.backfilled=oBack;
    foreach(var p in htMade){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();else if(!p.Destroyed)p.Destroy();}htMade.Clear();
    Adventurer.TestSuppressed=oSup;HeroTraits.TestSuppressed=oHsup;CombatGifts.TestSuppressed=oCsup;
   }
  }
 }
}
