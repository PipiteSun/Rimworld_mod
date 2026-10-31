using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.88.0：Test.ps1 -Only ClassFeats（职业专属专长：定义、显示与前置、职业强度、资源上限 / 获取、冷却、消耗、降级暂停）。
 public partial class LichChecks {
  Pawn CfPawn(int level){var p=ClsPawn(level,10,10,10);foreach(var s in new[]{SkillDefOf.Intellectual,SkillDefOf.Crafting,SkillDefOf.Plants,SkillDefOf.Animals})p.skills.GetSkill(s).Level=10;F2Adv(p);return p;}
  void CheckClassFeats(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=false;var s=LichMod.Settings;bool oldOn=s.adventurer;s.adventurer=true;
   try{
    // 1. 定义：24 个，十二个职业各两个（3 级、7 级），都有图标、附带属性、能被 NPC 选到
    var all=ClassFeats.All.ToList();
    Check(all.Count==24 && ClassSystem.All.Count==12 && ClassSystem.All.All(c=>ClassFeats.For(c).Select(f=>f.classLevel).OrderBy(x=>x).SequenceEqual(new[]{3,7}))
     && all.All(f=>f.Icon!=BaseContent.BadTex && f.BonusOptions.Count>0 && f.enemyTags!=null && f.enemyTags.Contains("any") && f.category=="Class"),"24 class feats: every class has one at level 3 and one at level 7, all with icons, +1 ability and NPC tags (classes "+ClassSystem.All.Count+", feats "+all.Count+")");
    // 2. 显示与前置：只对有这个职业的人显示；战士 2 级选不了，3 级可以；前置文字写职业等级
    var vet=FD("LP_Feat_Class_FighterVeteran");var wizF=FD("LP_Feat_Class_WizardScholar");
    var a=CfPawn(7);var ha=Adventurer.Get(a);bool hidden=!Feats2.Visible(vet,a);
    ClassSystem.AddLevel(ha,ClassSystem.Fighter);ClassSystem.AddLevel(ha,ClassSystem.Fighter);ha.pendingASI=1;ha.fighterFeats=0;
    string why2=ha.CanTakeFeat(vet);bool vis=Feats2.Visible(vet,a) && !Feats2.Visible(wizF,a);
    ClassSystem.AddLevel(ha,ClassSystem.Fighter);ha.fighterFeats=0;string why3=ha.CanTakeFeat(vet);
    Check(hidden && vis && why2=="需要战士 3 级" && why3==null && vet.PrereqText().StartsWith("战士 3 级"),"visible only with fighter levels, wizard feats hidden; fighter 2 → \""+why2+"\", fighter 3 → takeable; prereq \""+vet.PrereqText()+"\"");
    // 3. 职业强度 +15%（战技、射术），资源上限 +2；附带属性先清掉只量专长本身
    var mp=DefDatabase<StatDef>.GetNamed("LP_MartialPower");var sp=DefDatabase<StatDef>.GetNamed("LP_MarksmanPower");
    float m0=a.GetStatValue(mp,true,-1),s0=a.GetStatValue(sp,true,-1);int r0=ClassSystem.MaxResource(ha,ClassSystem.Fighter);
    bool took=ha.TakeFeat(vet);ha.featScore.Clear();ha.Dirty();
    float m1=a.GetStatValue(mp,true,-1),s1=a.GetStatValue(sp,true,-1);int r1=ClassSystem.MaxResource(ha,ClassSystem.Fighter);
    Check(took && Near(m1-m0,.15f) && Near(s1-s0,.15f) && r1==r0+2 && ClassFeats.PowerText(a,mp).Contains("战场老兵"),"战场老兵: martial power +"+(m1-m0).ToString("0.00")+", marksman +"+(s1-s0).ToString("0.00")+", 斗志 max "+r0+" → "+r1);
    // 4. 百战之躯（战士 7）：斗志获取 ×1.5，战士能力冷却 ×0.8
    var hb=FD("LP_Feat_Class_FighterHundredBattles");for(int i=0;i<4;i++)ClassSystem.AddLevel(ha,ClassSystem.Fighter);ha.AddFeat(hb);
    ha.classes.res[ClassSystem.Fighter]=0f;float got=ClassSystem.AddResource(ha,ClassSystem.Fighter,1f);
    var sw=a.abilities.GetAbility(Ab("LP_SecondWind"));sw.StartCooldown(1000);int cd=sw.CooldownTicksRemaining;sw.ResetCooldown();
    Check(ClassSystem.Level(ha,ClassSystem.Fighter)==7 && Near(got,1.5f) && cd==800,"百战之躯 at fighter 7: +1 斗志 becomes +"+got+", 回气 cooldown 1000 → "+cd);
    // 5. 降级（角色编辑器）：专长保留但效果暂停
    ClassSystem.SetLevel(ha,ClassSystem.Fighter,2);float m2=a.GetStatValue(mp,true,-1);sw=a.abilities.GetAbility(Ab("LP_SecondWind"));int cd2=-1;if(sw!=null){sw.StartCooldown(1000);cd2=sw.CooldownTicksRemaining;sw.ResetCooldown();}
    Check(ha.HasFeat(vet) && Near(ClassFeats.PowerBonus(a,mp),0f) && cd2==1000 && Near(ClassFeats.GainFactor(ha,ClassSystem.Fighter),1f),"fighter dropped to 2: feats kept, power bonus 0, cooldown back to "+cd2+", gain ×1");
    // 6. 法术塑形（法师 7）：火球 2 → 1，魔法飞弹 0 不变；奥术学者法力上限 +3
    var w=CfPawn(7);var hw=Adventurer.Get(w);for(int i=0;i<7;i++)ClassSystem.AddLevel(hw,Cls("Wizard"));
    int fb0=ClassLegendary.Cost(w,Ab("LP_Fireball"),Fx("LP_Fireball"));int mana0=ClassSystem.MaxResource(hw,Cls("Wizard"));
    hw.AddFeat(FD("LP_Feat_Class_WizardSculptor"));hw.AddFeat(wizF);
    int fb1=ClassLegendary.Cost(w,Ab("LP_Fireball"),Fx("LP_Fireball")),mm=ClassLegendary.Cost(w,Ab("LP_MagicMissile"),Fx("LP_MagicMissile"));int mana1=ClassSystem.MaxResource(hw,Cls("Wizard"));
    Check(fb0==2 && fb1==1 && mm==0 && mana1==mana0+3,"法术塑形: fireball cost "+fb0+" → "+fb1+", magic missile "+mm+"; 奥术学者: mana max "+mana0+" → "+mana1);
    // 7. 神恩共用：牧师的神眷之手 +2 加在牧师 / 圣武士共用的那一条上，虔诚祷者的获取 ×1.5 也一样
    var c=CfPawn(10);var hc=Adventurer.Get(c);for(int i=0;i<7;i++)ClassSystem.AddLevel(hc,Cls("Cleric"));for(int i=0;i<3;i++)ClassSystem.AddLevel(hc,ClassSystem.Paladin);
    int g0=ClassSystem.MaxResource(hc,ClassSystem.Paladin);hc.AddFeat(FD("LP_Feat_Class_ClericBlessedHands"));hc.AddFeat(FD("LP_Feat_Class_ClericDevout"));
    int g1=ClassSystem.MaxResource(hc,ClassSystem.Paladin),g2=ClassSystem.MaxResource(hc,Cls("Cleric"));hc.classes.res[ClassSystem.PoolKey(Cls("Cleric"))]=0f;float gg=ClassSystem.AddResource(hc,ClassSystem.Paladin,1f);
    Check(g1==g0+2 && g2==g1 && Near(gg,1.5f),"shared 神恩: max "+g0+" → "+g1+" seen from both classes, paladin-side gain +"+gg);
    // 8. 没有资源的职业：器械师工坊大师冷却 ×0.75；冒险者总开关关掉时全部失效
    var r=CfPawn(7);var hr=Adventurer.Get(r);for(int i=0;i<7;i++)ClassSystem.AddLevel(hr,Cls("Artificer"));hr.AddFeat(FD("LP_Feat_Class_ArtificerMasterwork"));
    var ab=r.abilities.abilities.FirstOrDefault(x=>x is Ability_Class && ClassSystem.ClassOf(x.def)==Cls("Artificer"));int acd=-1,off=-1;
    if(ab!=null){ab.StartCooldown(1000);acd=ab.CooldownTicksRemaining;s.adventurer=false;ab.StartCooldown(1000);off=ab.CooldownTicksRemaining;s.adventurer=true;ab.ResetCooldown();}
    Check(acd==750 && off==1000,"工坊大师: "+ab?.def.label+" cooldown 1000 → "+acd+"; adventurer switch off → "+off);
   }finally{
    s.adventurer=oldOn;foreach(var p in advMade)if(!p.Destroyed)p.Destroy();advMade.Clear();Adventurer.TestSuppressed=sup;
   }
  }
 }
}
