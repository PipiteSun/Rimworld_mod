using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.59.0：Test.ps1 -Only Exalted（超凡属性特性）。-Isekai -Only Exalted 另查共存模式。
 public partial class LichChecks {
  static string exSaved,exId;
  // 测试用冒险者：六维 10，没有传奇点和恩赐，已算过基准（之后的新获得会发信）。
  Hediff_Adventurer ExAdv(Pawn p,int a=-1,int v=10){
   var h=HtAdv(p);h.scores=new[]{10,10,10,10,10,10};if(a>=0)h.scores[a]=v;
   h.legendary=false;h.legendSpent=new int[Legend.Count];h.legendPoints=0;h.boons.Clear();h.mightAbility=-1;h.tomes=new bool[6];h.wishAbility=-1;
   h.exaltedInit=true;h.exaltedSent=0;h.mythicSent=0;h.holdUntil=0;h.holdStart=int.MinValue;h.holdUses=0;
   ExRefresh(h);return h;
  }
  static void ExRefresh(Hediff_Adventurer h){h.Dirty();var _=h.CurStage;}
  // 热修1：按设置文件的格式读一份 Settings（只读临时文件，不碰玩家的设置）。
  static Settings ExLoadSettings(string body){
   string tmp=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"lp_exalted_settings_test.xml");Settings st=null;
   System.IO.File.WriteAllText(tmp,"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<SettingsBlock>\n<ModSettings Class=\"LichPhylactery.Settings\">"+body+"</ModSettings>\n</SettingsBlock>");
   try{Scribe.loader.InitLoading(tmp);try{Scribe_Deep.Look(ref st,"ModSettings");}finally{Scribe.loader.FinalizeLoading();}}
   catch(Exception e){Log.Warning("[LP] settings load test: "+e.Message);st=null;}
   finally{try{System.IO.File.Delete(tmp);}catch{}}
   return st;
  }
  // 24 需要上限 24：威能之恩那一项 +4。
  static void ExMythic(Hediff_Adventurer h,int a){h.boons.Add(Epic.Might);h.mightAbility=a;h.scores[a]=24;ExRefresh(h);}
  static float ExFactor(Hediff_Adventurer h,string stat){var m=h.CurStage.statFactors?.FirstOrDefault(x=>x.stat.defName==stat);return m==null?1f:m.value;}
  static float ExOffset(Hediff_Adventurer h,string stat){var m=h.CurStage.statOffsets?.FirstOrDefault(x=>x.stat.defName==stat);return m==null?0f:m.value;}
  Pawn ExAnimal(float min,float max){
   var kind=DefDatabase<PawnKindDef>.AllDefsListForReading.Where(k=>k.RaceProps!=null && k.RaceProps.Animal && !k.RaceProps.Dryad && k.race.race.baseBodySize>=min && k.race.race.baseBodySize<=max).OrderBy(k=>k.defName).First();
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,fixedBiologicalAge:kind.RaceProps.lifeStageAges.Last().minAge+.5f));htMade.Add(p);return p;
  }

  void CheckExalted(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oEx=s.exaltedScores,oCo=s.exaltedCoexist,oIco=s.adventurerIsekaiCoexist,oAff=s.adventurerAffinity;
   s.adventurer=true;s.exaltedScores=true;s.exaltedCoexist=false;s.adventurerIsekaiCoexist=false;s.adventurerAffinity=false;
   var enemy=AdvEnemy;
   try{
    // 1. 定义与设置默认值
    var stagger=Exalted.Stagger;var pres=Exalted.Presence;var mood=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_ExaltedPresenceMood");
    Check(stagger!=null && pres!=null && mood!=null && stagger.stages[0].statFactors.Any(m=>m.stat==StatDefOf.MoveSpeed && Near(m.value,.1f)) && stagger.stages[0].statFactors.Any(m=>m.stat.defName=="MeleeCooldownFactor" && Near(m.value,1.5f)) && new Settings().exaltedScores && new Settings().exaltedCoexist && new Settings().exaltedVersion==Settings.ExaltedVersion,"LP_ExaltedStagger (move ×0.1, melee cooldown ×1.5), LP_ExaltedPresence and its mood load; 超凡属性特性 defaults on, coexist override defaults on (hotfix 1)");
    // 2. 20 获得、19 没有；数值合进冒险者状态
    var a20=HtPawn();var h20=ExAdv(a20,Adventurer.Str,20);var a19=HtPawn();var h19=ExAdv(a19,Adventurer.Str,19);
    Check(h20.exaltedMask==1 && Exalted.Has(a20,Adventurer.Str) && h19.exaltedMask==0 && !Exalted.Has(a19,Adventurer.Str) && Near(ExFactor(h20,"MiningSpeed"),4f*1.2f) && Near(ExFactor(h20,"CarryingCapacity"),4f*1.25f) && Near(ExFactor(h19,"MiningSpeed"),3.5f),"Str 20 → 巨人之力 (mining ×4×1.2, carry ×4×1.25 with the 1.65.0 curve), Str 19 → nothing");
    // 3. 降到线下暂停、回升恢复（已发信掩码不变）
    h20.scores[0]=18;ExRefresh(h20);bool paused=!Exalted.Has(a20,Adventurer.Str) && Near(ExFactor(h20,"MiningSpeed"),3f);
    h20.scores[0]=20;ExRefresh(h20);
    Check(paused && Exalted.Has(a20,Adventurer.Str) && Near(ExFactor(h20,"MiningSpeed"),4f*1.2f),"dropping to 18 pauses 巨人之力, back at 20 it resumes");
    // 4. 旧存档补算不发信；新获得发信一次
    int l0=Exalted.Letters;var old=HtPawn();var ho=ExAdv(old,Adventurer.Con,20);ho.exaltedInit=false;ho.exaltedSent=0;int n0=Exalted.Sync(ho);
    var nw=HtPawn();var hn=ExAdv(nw);int none=Exalted.Sync(hn);hn.scores[1]=20;ExRefresh(hn);int n1=Exalted.Sync(hn),n2=Exalted.Sync(hn);
    Check(n0==0 && ho.exaltedInit && ho.exaltedSent==4 && none==0 && n1==1 && n2==0 && hn.exaltedSent==2 && Exalted.Letters==l0+1,"old save backfill: Con 20 is recorded without a letter; a new 疾影 sends exactly one letter");
    // 5. 神话：24（威能之恩上限 24）
    var m24=HtPawn();var hm=ExAdv(m24);ExMythic(hm,Adventurer.Wis);var m23=HtPawn();var hm23=ExAdv(m23);hm23.boons.Add(Epic.Might);hm23.mightAbility=4;hm23.scores[4]=23;ExRefresh(hm23);
    Check(hm.mythicMask==16 && hm.exaltedMask==16 && Exalted.Mythic(m24,Adventurer.Wis) && hm23.exaltedMask==16 && hm23.mythicMask==0,"Wis 24 → 神话·先知 (also 超凡); Wis 23 → only 明察");
    // 6. 神话：20 + 传奇冒险者 + 近战 + 血量 ≥ 20
    var lg=HtPawn();var hl=ExAdv(lg,Adventurer.Str,20);hl.legendary=true;hl.legendSpent[Legend.Melee]=12;hl.legendSpent[Legend.Health]=7;ExRefresh(hl);bool short19=hl.mythicMask==0;
    hl.legendSpent[Legend.Health]=8;ExRefresh(hl);bool ok20=hl.mythicMask==1;hl.legendary=false;ExRefresh(hl);
    Check(short19 && ok20 && hl.mythicMask==0,"Str 20 + legendary + 近战 12 + 血量 8 → 撼山; 19 points or not legendary → no");
    // 7. 近战穿透：巨人之力 +15%，撼山 +30%
    var d1=new DamageInfo(DamageDefOf.Cut,10f,.1f,-1f,a20,null,a20.def);Exalted.Penetrate(ref d1,a20);
    var my=HtPawn();var hmy=ExAdv(my);ExMythic(hmy,Adventurer.Str);var d2=new DamageInfo(DamageDefOf.Cut,10f,.1f,-1f,my,null,my.def);Exalted.Penetrate(ref d2,my);
    var d3=new DamageInfo(DamageDefOf.Cut,10f,.1f,-1f,a19,null,a19.def);Exalted.Penetrate(ref d3,a19);
    Check(Near(d1.ArmorPenetrationInt,.25f) && Near(d2.ArmorPenetrationInt,.4f) && Near(d3.ArmorPenetrationInt,.1f),"melee armor penetration 0.1 → "+d1.ArmorPenetrationInt+" (巨人之力) / "+d2.ArmorPenetrationInt+" (撼山)");
    // 8. 踉跄：体型 1 生效、体型 2 无效、撼山对体型 ≤3 生效；撼山自己免疫
    Exalted.TestRoll=0f;
    var t1=HtPawn(null,enemy);var big=ExAnimal(1.8f,2.9f);float bs=big.BodySize;
    bool s1=Exalted.TryStagger(a20,t1) && t1.health.hediffSet.HasHediff(stagger);bool s2=Exalted.TryStagger(a20,big);bool s3=Exalted.TryStagger(my,big) && big.health.hediffSet.HasHediff(stagger);
    var mine=HtPawn(null,enemy);var hmine=ExAdv(mine);ExMythic(hmine,Adventurer.Str);bool s4=Exalted.TryStagger(a20,mine);mine.health.AddHediff(stagger);
    Check(s1 && !s2 && s3 && !s4 && !mine.health.hediffSet.HasHediff(stagger) && bs>1.5f && bs<=3f,"stagger: body size 1 staggers, size "+bs.ToString("0.0")+" resists 巨人之力 but not 撼山; 撼山 is immune");
    Exalted.TestRoll=1f;bool s5=Exalted.TryStagger(a20,HtPawn(null,enemy));Exalted.TestRoll=null;
    Check(!s5,"stagger needs the 15% roll");
    // 9. 疾影 ×0.85、无影 ×0.75（只对敌人）
    var dx=HtPawn();ExAdv(dx,Adventurer.Dex,20);var dm=HtPawn();ExMythic(ExAdv(dm),Adventurer.Dex);var shooter=HtPawn(null,enemy);var friend=HtPawn();
    Check(Near(Exalted.ShotAgainst(dx,shooter),.85f) && Near(Exalted.ShotAgainst(dm,shooter),.75f) && Near(Exalted.ShotAgainst(dx,friend),1f) && Near(Exalted.ShotAgainst(a19,shooter),1f) && Exalted.ImmuneToSentinel(dx) && Near(ExFactor(Adventurer.Get(dx),"AimingDelayFactor"),.8f/4f),"enemy shots at 疾影 ×0.85, at 无影 ×0.75 (allies unaffected); 疾影 aims −20% (on top of Dex 20 aim ÷4) and ignores sentinels");
    // 10. 有灵活：免借机重叠，改为移速 +0.2
    var mob=HtPawn();var hmob=ExAdv(mob,Adventurer.Dex,20);float mv0=ExOffset(hmob,"MoveSpeed");hmob.feats.Add(Feats.Mobile);ExRefresh(hmob);float mv1=ExOffset(hmob,"MoveSpeed");
    Check(Near(mv1-mv0,Exalted.DexMove+(Feats.Mobile.statOffsets?.Where(m=>m.stat==StatDefOf.MoveSpeed).Sum(m=>m.value)??0f)),"疾影 with 灵活: move speed "+mv0+" → "+mv1+" (+0.2 on top of 灵活)");
    // 11. 无影：600 刻里第一次掷，25% 落空
    var hit=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,shooter,null,shooter.def);Exalted.evadeTick.Remove(dm.thingIDNumber);
    Exalted.TestRoll=0f;bool e1=Exalted.Evade(dm,hit),e2=Exalted.Evade(dm,hit);Exalted.evadeTick[dm.thingIDNumber]=GenTicks.TicksGame-Exalted.EvadeWindow;bool e3=Exalted.Evade(dm,hit);bool e4=Exalted.Evade(dx,hit);Exalted.TestRoll=null;
    Check(e1 && !e2 && e3 && !e4,"无影: the first hit in a 600-tick window can miss, the next one in the window cannot; 疾影 alone never evades");
    // 12. 硬撑：每天一次 8 秒；金刚不坏每天 2 次 12 秒
    var c1=HtPawn();var hc=ExAdv(c1,Adventurer.Con,20);int now=GenTicks.TicksGame;
    bool h1=Exalted.Hold(c1);int until=hc.holdUntil;bool hIn=Exalted.Hold(c1);hc.holdUntil=0;bool h2=Exalted.Hold(c1);
    var c2=HtPawn();var hc2=ExAdv(c2);ExMythic(hc2,Adventurer.Con);bool m1=Exalted.Hold(c2);int until2=hc2.holdUntil;hc2.holdUntil=0;bool m2=Exalted.Hold(c2);hc2.holdUntil=0;bool m3=Exalted.Hold(c2);
    hc.holdStart=now-Exalted.HoldPeriod;hc.holdUntil=0;bool h3=Exalted.Hold(c1);
    Check(h1 && until==now+480 && hIn && !h2 && m1 && until2==now+720 && m2 && !m3 && h3 && !Exalted.Hold(a19),"硬撑: 1 hold of 480 ticks per day (again after a day); 金刚不坏: 2 holds of 720 ticks");
    // 13. 疾病封顶在致死前；金刚不坏失血封顶在极重前
    var plague=c1.health.AddHediff(HediffDefOf.Plague);plague.Severity=.995f;var blood=c2.health.AddHediff(HediffDefOf.BloodLoss);blood.Severity=.9f;var plague2=a19.health.AddHediff(HediffDefOf.Plague);plague2.Severity=.97f;
    Exalted.CapDiseases(hc);Exalted.CapDiseases(hc2);
    Check(plague.Severity<HediffDefOf.Plague.lethalSeverity-.01f && Near(blood.Severity,Exalted.BloodCap) && Near(plague2.Severity,.97f) && Near(hc.CurStage.totalBleedFactor,.7f) && Near(ExOffset(hc,"PainShockThreshold"),.15f+.05f*3f),"不屈之躯 holds plague at "+plague.Severity.ToString("0.000")+"; 金刚不坏 holds blood loss at 0.58; bleeding ×0.7, pain shock +15%");
    c1.health.RemoveHediff(plague);c2.health.RemoveHediff(blood);a19.health.RemoveHediff(plague2);
    // 14. 技能不衰退
    var it=HtPawn();ExAdv(it,Adventurer.Int,20);var sk=it.skills.skills.First(x=>!x.TotallyDisabled);sk.Level=18;sk.xpSinceLastLevel=1000f;float before=sk.xpSinceLastLevel;sk.Interval();
    var ctl=HtPawn();var sc=ctl.skills.skills.First(x=>!x.TotallyDisabled);sc.Level=18;sc.xpSinceLastLevel=1000f;float cb=sc.xpSinceLastLevel;sc.Interval();
    Check(sk.Level==18 && Near(sk.xpSinceLastLevel,before) && sc.xpSinceLastLevel<cb && Near(ExFactor(Adventurer.Get(it),"ReadingSpeed"),1.3f) && Near(Exalted.ReadFactor(it),1.3f),"洞彻心智: a level-18 skill does not decay (control lost "+(cb-sc.xpSinceLastLevel).ToString("0")+" xp); reading ×1.3");
    // 15. 举一反三：升级那一刻另一项技能得到所需经验的 10%（天才 20%）
    foreach(var x in it.skills.skills){x.Level=3;x.xpSinceLastLevel=0f;}
    var up=it.skills.skills.First(x=>!x.TotallyDisabled);float need=SkillRecord.XpRequiredToLevelUpFrom(3);float others=it.skills.skills.Where(x=>x!=up).Sum(x=>x.XpTotalEarned+x.xpSinceLastLevel);
    up.Learn(need+1f,true,true);float gain=it.skills.skills.Where(x=>x!=up).Sum(x=>x.XpTotalEarned+x.xpSinceLastLevel)-others;
    Check(up.Level==4 && Near(gain/need,.1f) && Exalted.Echoes>0,"举一反三: level 3 → 4 gives another skill "+gain.ToString("0")+" xp ("+(gain/need).ToStringPercent()+" of "+need.ToString("0")+"; mask "+Exalted.MaskOf(it)+", echoes "+Exalted.Echoes+")");
    var gn=HtPawn();ExMythic(ExAdv(gn),Adventurer.Int);var sa=gn.skills.skills.Where(x=>!x.TotallyDisabled).Take(2).ToList();
    foreach(var x in sa){x.Level=5;x.xpSinceLastLevel=0f;}sa[0].passion=Passion.None;sa[1].passion=Passion.Minor;sa[0].Learn(100f,false);sa[1].Learn(100f,false);
    var ct=HtPawn();var sct=ct.skills.skills.Where(x=>!x.TotallyDisabled).Take(2).ToList();foreach(var x in sct){x.Level=5;x.xpSinceLastLevel=0f;}sct[0].passion=Passion.None;sct[1].passion=Passion.Minor;sct[0].Learn(100f,false);sct[1].Learn(100f,false);
    Check(Near(sa[0].xpSinceLastLevel,sa[1].xpSinceLastLevel) && sct[0].xpSinceLastLevel<sct[1].xpSinceLastLevel*.5f,"天才: a no-passion skill learns like minor passion ("+sa[0].xpSinceLastLevel.ToString("0")+" vs "+sa[1].xpSinceLastLevel.ToString("0")+"; normal pawn "+sct[0].xpSinceLastLevel.ToString("0")+")");
    // 16. 明察显形：8 格；先知 15 格
    var seer=HtPawn(null,null,true,0,0);ExAdv(seer,Adventurer.Wis,20);var prophet=HtPawn(null,null,true,1,0);ExMythic(ExAdv(prophet),Adventurer.Wis);
    var inv=DefDatabase<HediffDef>.GetNamed("PsychicInvisibility");var near=HtPawn(null,enemy,true,0,6);var far=HtPawn(null,enemy,true,0,12);near.health.AddHediff(inv);far.health.AddHediff(inv);
    Exalted.revealed.Clear();int r1=Exalted.Reveal(seer,Exalted.WisRadius);bool nearSeen=Exalted.Revealed(near) && !InvisibilityUtility.IsPsychologicallyInvisible(near),farHidden=!Exalted.Revealed(far);int r2=Exalted.Reveal(prophet,Exalted.MythicWisRadius);
    Check(r1==1 && nearSeen && farHidden && r2==1 && Exalted.Revealed(far) && !InvisibilityUtility.IsPsychologicallyInvisible(far),"明察 reveals an invisible enemy 6 cells away but not 12; 先知 (15 cells) reveals the far one too");
    // 17. 心灵效果时长 −50%；先知免疫心灵震爆
    var blast=seer.health.AddHediff(MindFlayer.Blasted);int left=blast.TryGetComp<HediffComp_Disappears>().ticksToDisappear;var blast2=a19.health.AddHediff(MindFlayer.Blasted);int left2=blast2.TryGetComp<HediffComp_Disappears>().ticksToDisappear;
    bool immune=!MindFlayer.PsychicStrike(near,prophet,60);
    Check(left*2==left2 && immune && !prophet.health.hediffSet.HasHediff(MindFlayer.Blasted) && Near(ExFactor(Adventurer.Get(seer),"MeditationFocusGain"),Hediff_Adventurer.SoftCap(4f,2.5f)*1.3f) && Near(ExFactor(Adventurer.Get(seer),"MedicalTendQuality"),4f*1.1f),"明察: 心灵震荡 lasts "+left+" ticks instead of "+left2+"; 先知 ignores a mind blast; focus ×1.3, tend quality ×1.1 on top of Wis 20 ×4");
    // 18. 威仪光环：心情 +3，多个不叠加；君临 +6
    var k1=HtPawn(null,null,true,3,0);ExAdv(k1,Adventurer.Cha,20);var k2=HtPawn(null,null,true,3,1);ExAdv(k2,Adventurer.Cha,20);var ally=HtPawn(null,null,true,3,3);
    Exalted.Aura(k1,false);Exalted.Aura(k2,false);var ph=ally.health.hediffSet.hediffs.Where(x=>x.def==pres).ToList();var st1=mood.Worker.CurrentState(ally);
    Check(ph.Count==1 && Near(ph[0].Severity,1f) && st1.Active && mood.stages[st1.StageIndex].baseMoodEffect==3 && ph[0].CurStage.statOffsets.Any(m=>m.stat==StatDefOf.MentalBreakThreshold && Near(m.value,-.03f)),"威仪: one ally next to two carriers gets one 威仪 (mood +3, break threshold −3%)");
    var k3=HtPawn(null,null,true,4,0);ExMythic(ExAdv(k3),Adventurer.Cha);Exalted.Aura(k3,true);Exalted.Aura(k1,false);var st2=mood.Worker.CurrentState(ally);
    Check(Near(ph[0].Severity,2f) && mood.stages[st2.StageIndex].baseMoodEffect==6 && ally.health.hediffSet.hediffs.Count(x=>x.def==pres)==1,"君临 raises it to +6 and a later 威仪 refresh does not lower it");
    // 19. 与鼓舞领袖取一次：鼓舞中的人只拿心情
    var ins=HtPawn(null,null,true,3,4);ins.health.AddHediff(DefDatabase<HediffDef>.GetNamed("LP_FeatInspired"));Exalted.Aura(k1,false);var pi=ins.health.hediffSet.GetFirstHediffOfDef(pres);
    Check(pi!=null && mood.stages[mood.Worker.CurrentState(ins).StageIndex].baseMoodEffect==3 && (pi.CurStage.statOffsets==null || !pi.CurStage.statOffsets.Any()),"an ally under 鼓舞 (鼓舞领袖) gets the 威仪 mood but not a second break-threshold bonus");
    // 20. 6 格内敌人命中 −8%；君临对首领无效
    Exalted.chaCarriers.Add(k1);var foe=HtPawn(null,enemy,true,3,6);var foe2=HtPawn(null,enemy,true,3,12);bool cow=Exalted.Cowed(foe),cow2=Exalted.Cowed(foe2);
    var boss=HtPawn(null,enemy,true,5,3);ExAdv(boss).storyLevel=true;var grunt=HtPawn(null,enemy,true,5,4);ExAdv(grunt);
    Exalted.TestRoll=0f;int fled=Exalted.Rout(k3,k3.Position,map);Exalted.TestRoll=null;
    Check(cow && !cow2 && grunt.MentalStateDef==MentalStateDefOf.PanicFlee && boss.MentalStateDef!=MentalStateDefOf.PanicFlee && fled>=1 && Exalted.RoutImmune(boss),"威仪: enemies within 6 cells −8% hit (not at 12); 君临 routs a raider but not a story boss");
    grunt.MentalState?.RecoverFromState();
    // 21. 战斗力折算：超凡 +4%，神话 +8%
    var cr=HtPawn(null,enemy);var hcr=ExAdv(cr,Adventurer.Str,20);ExMythic(hcr,Adventurer.Dex);var list=new List<Pawn>{cr};
    float c1v=AdventurerEnemies.Cost(list,list,500f,20);hcr.scores=new[]{10,10,10,10,10,10};hcr.boons.Clear();ExRefresh(hcr);float c0v=AdventurerEnemies.Cost(list,list,500f,20);
    Check(Near(Exalted.CostFactor(a20),1.04f) && Near(c1v/c0v,1.12f),"combat-power cost: 巨人之力 + 无影 = ×"+(c1v/c0v).ToString("0.00"));
    // 22. 派系声望 ×1.5（殖民地有君临）
    Check(Exalted.Reputation(4)==6 && Exalted.Reputation(0)==0,"君临: faction reputation gains ×1.5 (4 → 6)");
    // 23. 总开关
    s.exaltedScores=false;ExRefresh(h20);bool off=h20.exaltedMask==0 && !Exalted.Has(a20,0);s.exaltedScores=true;ExRefresh(h20);
    Check(off && Exalted.Has(a20,0),"turning 超凡属性特性 off zeroes the mask; on again restores it");
    // 热修1 a：效果关着时分页照样认出达标项（Raw），悬停写原因
    s.exaltedScores=false;ExRefresh(h20);string tipOff=Exalted.Tip(h20,0),why=Exalted.OffReason;int rawOff=Exalted.Raw(h20);s.exaltedScores=true;ExRefresh(h20);
    Check(rawOff==1 && h20.exaltedMask==1 && tipOff.Contains("未生效") && why!=null && tipOff.Contains(why) && Exalted.OffReason==null && Exalted.Raw(h19)==0 && !Exalted.Tip(h20,0).Contains("未生效"),"hotfix a: with the effect off, Str 20 still shows (Raw mask 1, tip “"+why+"”); on again the tip is the normal gold one");
    // 热修1 d：加点到 20 后缓存立刻重算
    h19.pendingASI=1;h19.asiLeft=0;bool spent=h19.SpendPoints(0,1);var _s=h19.CurStage;
    Check(spent && h19.scores[0]==20 && h19.exaltedMask==1 && Exalted.Has(a19,Adventurer.Str),"hotfix d: spending a point 19 → 20 rebuilds the mask at once (巨人之力)");
    h19.scores[0]=19;ExRefresh(h19);
    // 热修1 c：健康页标记（显示用、无数值），Tick 同步；效果关着标“未生效”，降到 19 移除
    Exalted.Tick(h20);var mark=a20.health.hediffSet.GetFirstHediffOfDef(Exalted.MarkHediff) as Hediff_ExaltedMark;
    bool mk1=mark!=null && mark.active && mark.LabelBase=="超凡·巨人之力" && mark.LabelInBrackets==null && mark.CurStage==null && mark.Visible && mark.TipStringExtra.Contains("负重");
    s.exaltedCoexist=true;s.exaltedScores=false;Exalted.SyncMark(h20,Exalted.Raw(h20));bool gone=mark!=null && mark.ShouldRemove;
    s.exaltedScores=true;ExRefresh(h20);
    var mm=HtPawn();var hmm=ExAdv(mm,Adventurer.Str,20);ExMythic(hmm,Adventurer.Dex);Exalted.Tick(hmm);var mark2=mm.health.hediffSet.GetFirstHediffOfDef(Exalted.MarkHediff) as Hediff_ExaltedMark;
    Exalted.Tick(h19);bool none19=!a19.health.hediffSet.HasHediff(Exalted.MarkHediff);
    Check(mk1 && gone && mark2!=null && mark2.LabelBase=="超凡·巨人之力、神话·无影" && none19,"hotfix c: health-tab mark “"+mark?.LabelBase+"” / “"+mark2?.LabelBase+"” (no stage, no stats); turning 超凡 off flags it for removal; Str 19 gets none");
    // 热修1 b：旧设置文件（Peter 的：只有一项非默认值）读入后共存仍生效 = 开；新版本里自己关掉的保留
    Settings oldS=ExLoadSettings("<adventurerEnemyLegendCoexist>True</adventurerEnemyLegendCoexist>"),offS=ExLoadSettings("<exaltedCoexist>False</exaltedCoexist><exaltedVersion>1</exaltedVersion>"),preS=ExLoadSettings("<exaltedCoexist>False</exaltedCoexist>");
    var mig=new Settings{exaltedCoexist=false,exaltedVersion=0};bool first=mig.MigrateExalted(),second=mig.MigrateExalted();
    Check(oldS!=null && oldS.exaltedCoexist && oldS.exaltedVersion==1 && offS!=null && !offS.exaltedCoexist && preS!=null && preS.exaltedCoexist && first && !second && mig.exaltedCoexist,"hotfix b: an old settings file loads with 共存模式下仍生效 on (version 0 → 1, migrated once); a player's own “off” saved with version 1 stays off");
    // 24. Isekai 共存：默认掩码 0，勾选后生效
    if(Adventurer.IsekaiActive){
     s.adventurerIsekaiCoexist=true;s.exaltedCoexist=false;ExRefresh(h20);bool zero=h20.exaltedMask==0 && Exalted.Count>=0 && !Exalted.Has(a20,0);
     s.exaltedCoexist=true;ExRefresh(h20);bool back=h20.exaltedMask==1 && Near(ExFactor(h20,"MiningSpeed"),1.2f);
     s.exaltedCoexist=false;s.adventurerIsekaiCoexist=false;ExRefresh(h20);
     Check(zero && back,"Isekai coexist mode: with the override off masks are 0, 共存模式下超凡特性仍生效 turns 巨人之力 back on (mining ×1.2 while six-score effects stay with Isekai)");
     // 热修1 a/c：共存模式 + 覆盖关 → 分页灰色写 Isekai 原因，健康页标“未生效”
     s.adventurerIsekaiCoexist=true;s.exaltedCoexist=false;ExRefresh(h20);string whyI=Exalted.OffReason,tipI=Exalted.Tip(h20,0);Exalted.Tick(h20);var mk=a20.health.hediffSet.GetFirstHediffOfDef(Exalted.MarkHediff) as Hediff_ExaltedMark;
     bool grey=whyI!=null && whyI.Contains("Isekai 共存模式") && whyI.Contains("「冒险者」") && tipI.Contains(whyI) && Exalted.Raw(h20)==1 && h20.exaltedMask==0 && mk!=null && !mk.active && mk.LabelInBrackets=="未生效";
     s.exaltedCoexist=true;ExRefresh(h20);Exalted.Tick(h20);bool live=mk!=null && mk.active && h20.exaltedMask==1;
     s.exaltedCoexist=false;s.adventurerIsekaiCoexist=false;ExRefresh(h20);
     Check(grey && live,"hotfix a/c under Isekai: coexist without the override shows Str 20 grey (“"+whyI+"”) and the health mark says 未生效; with the override it turns gold/active");
    }else{Check(true,"Isekai not loaded: coexist check runs under -Isekai");Check(true,"Isekai not loaded: hotfix grey-reason check runs under -Isekai");}
   }finally{
    s.adventurer=oOn;s.exaltedScores=oEx;s.exaltedCoexist=oCo;s.adventurerIsekaiCoexist=oIco;s.adventurerAffinity=oAff;Exalted.TestRoll=null;Exalted.chaCarriers.Clear();
    foreach(var p in htMade){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();else if(!p.Destroyed)p.Destroy();}htMade.Clear();
   }
  }
  // 25. 存读档：掩码重建一致、已发信掩码和硬撑次数保留
  void PrepareExaltedSave(){
   var s=LichMod.Settings;s.adventurerIsekaiCoexist=false;s.exaltedScores=true;
   var p=HtPawn(null,null,true,-2,-2);var h=ExAdv(p,Adventurer.Str,20);ExMythic(h,Adventurer.Wis);h.exaltedSent=17;h.mythicSent=16;h.holdUses=1;h.holdStart=GenTicks.TicksGame;
   exId=p.ThingID;exSaved=ExSummary(h);htMade.Clear();
  }
  static string ExSummary(Hediff_Adventurer h)=>h.exaltedMask+"|"+h.mythicMask+"|"+h.exaltedSent+"|"+h.mythicSent+"|"+h.exaltedInit+"|"+h.holdUses;
  void CheckExaltedAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==exId);var h=p==null?null:Adventurer.Get(p);
   if(h!=null){var _=h.CurStage;}
   string now=h==null?"missing":ExSummary(h);
   Check(now==exSaved && Exalted.Has(p,Adventurer.Str) && Exalted.Mythic(p,Adventurer.Wis),"超凡 masks rebuild identically after save/load and the sent-letter masks survive ("+exSaved+" → "+now+")");
   p?.Destroy();
  }
 }
}
