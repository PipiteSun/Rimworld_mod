using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.90.0：Test.ps1 -Only ClassAudit（DND 系统逻辑审计修的几处：施法者判定、职业专长降级暂停数值、神恩强度看感知或魅力、门槛文字、法术狙击、能力悬停的实际消耗）。
 public partial class LichChecks {
  void CheckClassAudit(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=false;var s=LichMod.Settings;bool oldOn=s.adventurer;s.adventurer=true;
   try{
    // 1. 施法者：纯战士不算（战争施法者选不了，敌人不按法系），法师 / 圣武士算
    var wc=FD("LP_Feat_WarCaster");
    var f=ClsPawn(5,10,0,0);var hf=F2Adv(f);ClassSystem.AddLevel(hf,ClassSystem.Fighter);hf.pendingASI=1;hf.fighterFeats=0;
    bool fighterNo=!Feats.IsCaster(f) && hf.CanTakeFeat(wc)?.Contains("施法职业")==true && !Feats.EnemyRoles(f).Contains("caster") && !Feats2.IsMage(f);
    var w=ClsPawn(5);foreach(var sk in new[]{SkillDefOf.Intellectual})w.skills.GetSkill(sk).Level=10;var hw=F2Adv(w);ClassSystem.AddLevel(hw,Cls("Wizard"));
    var pl=ClsPawn(5,10,10,0);var hp=F2Adv(pl);ClassSystem.AddLevel(hp,ClassSystem.Paladin);
    Check(fighterNo && Feats.IsCaster(w) && Feats.IsCaster(pl) && wc.PrereqText().Contains("施法职业"),"caster check: fighter-only is not a caster (war caster: \""+hf.CanTakeFeat(wc)+"\"), wizard and paladin are");
    // 2. 职业专属专长：职业等级掉到要求以下，附带的数值（战场老兵 疼痛休克阈值 +10%）也暂停
    var vet=FD("LP_Feat_Class_FighterVeteran");for(int i=0;i<2;i++)ClassSystem.AddLevel(hf,ClassSystem.Fighter);hf.AddFeat(vet);hf.Dirty();
    float on=hf.CurStage.statOffsets?.GetStatOffsetFromList(StatDefOf.PainShockThreshold)??0f;
    ClassSystem.SetLevel(hf,ClassSystem.Fighter,2);float off=hf.CurStage.statOffsets?.GetStatOffsetFromList(StatDefOf.PainShockThreshold)??0f;
    ClassSystem.SetLevel(hf,ClassSystem.Fighter,3);float back=hf.CurStage.statOffsets?.GetStatOffsetFromList(StatDefOf.PainShockThreshold)??0f;
    Check(Near(on-off,.1f) && Near(back,on),"战场老兵 pain shock offset: fighter 3 "+on.ToString("0.00")+", dropped to 2 "+off.ToString("0.00")+", back to 3 "+back.ToString("0.00"));
    // 3. 神恩强度看感知或魅力（取较高）：牧师感知 18 比感知 10 高
    var dp=DefDatabase<StatDef>.GetNamed("LP_DivinePower");var cl=ClsPawn(5,0,10,0);var hc=F2Adv(cl);ClassSystem.AddLevel(hc,Cls("Cleric"));
    float d10=cl.GetStatValue(dp,true,-1);hc.scores[Adventurer.Wis]=18;hc.Dirty();float d18=cl.GetStatValue(dp,true,-1);
    Check(d18>d10+.1f && ClassLevelInfo.Part(dp).ability=="Wis/Cha","神恩强度 uses Wis or Cha: cleric Wis 10 ×"+d10.ToString("0.00")+" → Wis 18 ×"+d18.ToString("0.00"));
    // 4. 门槛文字写全主属性；5. 法术狙击对职业远程法术有效，借武器射程的不算
    string gate=ClassSystem.GateText(ClassSystem.Fighter,0);
    hw.level=7;for(int i=0;i<6;i++)ClassSystem.AddLevel(hw,Cls("Wizard"));
    var fb=w.abilities.GetAbility(Ab("LP_Fireball"));float rf0=fb==null?0f:Feats.RangeFactor(fb.verb,w);
    hw.AddFeat(FD("LP_Feat_SpellSniper"));float rf=fb==null?0f:Feats.RangeFactor(fb.verb,w);
    Check(gate.Contains("力量或敏捷 13") && Near(rf0,1f) && Near(rf,Feats.SpellSniperRange),"gate text \""+gate+"\"; spell sniper on fireball ×"+rf0+" → ×"+rf);
    // 6. 能力悬停：法术塑形后火球写实际消耗（基础 2 → 1）
    int fb0=ClassLegendary.Cost(w,Ab("LP_Fireball"),Fx("LP_Fireball"));hw.AddFeat(FD("LP_Feat_Class_WizardSculptor"));int fb1=ClassLegendary.Cost(w,Ab("LP_Fireball"),Fx("LP_Fireball"));
    Check(ClassSystem.Level(hw,Cls("Wizard"))==7 && fb0==2 && fb1==1,"fireball cost after 法术塑形: "+fb0+" → "+fb1+" (hover shows the real cost)");
   }finally{
    s.adventurer=oldOn;foreach(var p in advMade)if(!p.Destroyed)p.Destroy();advMade.Clear();Adventurer.TestSuppressed=sup;
   }
  }
 }
}
