using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.89.0 职业页 / 升级信件的部位生命与职业强度前后对比、资源上限公式（-Only Classes，3 条）。
 public partial class LichChecks {
  void CheckClassUI(){
   // 1. 预算的“下一级”= 加点后的真实值（战士：部位生命 + 战技强度；法师：奥术强度）。
   {var p=ClsPawn(8,10);p.skills.GetSkill(SkillDefOf.Intellectual).Level=10;var h=Adventurer.Get(p);h.scores[Adventurer.Con]=14;h.Dirty();
    var F=ClassSystem.Fighter;var W=Cls("Wizard");for(int i=0;i<3;i++){ClassSystem.AddLevel(h,F);ClassSystem.AddLevel(h,W);}
    var mp=DefDatabase<StatDef>.GetNamed("LP_MartialPower");var ap=DefDatabase<StatDef>.GetNamed("LP_ArcanePower");
    float pf=ClassLevelInfo.PartWith(h,F,h.level),mf=ClassLevelInfo.PowerWith(p,h,mp,F,h.level),af0=ClassLevelInfo.PowerWith(p,h,ap,F,h.level);bool okF=ClassLevelInfo.AddLevel(h,F);
    float pf1=Adventurer.PartHealthFactor(p),mf1=p.GetStatValue(mp),af1=p.GetStatValue(ap);
    float pw=ClassLevelInfo.PartWith(h,W,h.level),aw=ClassLevelInfo.PowerWith(p,h,ap,W,h.level);bool okW=ClassLevelInfo.AddLevel(h,W);
    float pw1=Adventurer.PartHealthFactor(p),aw1=p.GetStatValue(ap);string row=ClassLevelInfo.RowLine(p,h,W);
    Check(okF && okW && Near(pf,pf1) && Near(mf,mf1) && Near(af0,af1) && Near(pw,pw1) && Near(aw,aw1) && pf1>1f && aw1>af1 && row!=null && row.Contains("部位生命") && row.Contains(ap.label) && ClassLevelInfo.ClassTip(p,h,W).Contains("下一级获得"),
     "[1.89.0] predicted next level matches the real stats: part HP "+pf.ToString("0.###")+"/"+pf1.ToString("0.###")+", martial "+mf.ToString("0.###")+"/"+mf1.ToString("0.###")+", arcane "+aw.ToString("0.###")+"/"+aw1.ToString("0.###"));}
   // 2. 升级信件：部位生命和受影响的职业强度写成“前 → 后”（冒险者 4 → 5 级，熟练加值 +2 → +3）。
   {var p=ClsPawn(4);p.skills.GetSkill(SkillDefOf.Intellectual).Level=10;var h=Adventurer.Get(p);ClassSystem.AddLevel(h,Cls("Wizard"));
    var ap=DefDatabase<StatDef>.GetNamed("LP_ArcanePower");float part4=Adventurer.PartHealthFactor(p),a4=p.GetStatValue(ap);
    h.level=5;h.Dirty();float a5=p.GetStatValue(ap);var lines=ITab_Adventurer.LevelUpLinesHook?.Invoke(p,5)?.ToList()??new List<string>();string text=GameComponent_Adventurer.LevelGains(p,h,4,5);
    Check(lines.Any(l=>l.Contains(ap.LabelCap+" ×"+a4.ToString("0.00")+" → ×"+a5.ToString("0.00"))) && text.Contains("部位生命 "+ClassLevelInfo.PartText(part4)+" → "+ClassLevelInfo.PartText(Adventurer.PartHealthFactor(p))) && text.Contains(ap.LabelCap+" ×"),
     "[1.89.0] level-up letter: part HP and arcane power before → after ("+string.Join(" / ",lines)+")");}
   // 3. 资源条悬停：每种资源用自己的上限公式，“现在”合计 = MaxResource（职业 4 级：法力 6、契约法力 3、其他 4）。
   {var bad=new List<string>();
    foreach(var c in ClassSystem.All.Where(x=>x.resource!=ClassResource.None)){
     var p=ClsPawn(4);var h=Adventurer.Get(p);ClassSystem.SetLevel(h,c,4);int max=ClassSystem.MaxResource(h,c);string tip=ClassLevelInfo.ResourceTip(h,c);
     int want=c.resource==ClassResource.Mana?6:c.resource==ClassResource.Pact?3:4;
     if(max!=want || !tip.Contains(ClassLevelInfo.ResourceFormula(c)) || !tip.Contains("= "+max+"。") || tip.Contains("四舍五入"))bad.Add(c.label+" "+max);
    }
    Check(bad.Count==0,"[1.89.0] resource tooltip uses each resource's own cap formula (mana 2 + level, pact 2 + level/4, others 2 + level/2)"+(bad.Count>0?": "+string.Join("、",bad):""));}
  }
 }
}
