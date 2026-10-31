using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.89.0 职业页 / 升级信件的部位生命与职业强度前后对比、资源上限公式（-Only Classes，4 条）；1.97.0 奥术强度比例（第 5 条）。
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
   // 4. 1.93.0（Peter：升级法师部位生命下降）：冒险者升级（到 24 级，越过传奇 21 级）和给任何职业加点都不会降低部位生命，预算 = 实际。
   {var bad=new List<string>();
    var p=ClsPawn(3);var h=Adventurer.Get(p);h.scores[Adventurer.Con]=14;var W=Cls("Wizard");ClassSystem.SetLevel(h,W,2);
    float s0=Adventurer.PartHealthFactor(p),sp=ClassLevelInfo.PartWith(h,W,h.level);ClassSystem.SetLevel(h,W,3);float s1=Adventurer.PartHealthFactor(p);
    if(s1<s0-1e-4f || !Near(sp,s1))bad.Add("截图 法师 2→3 "+ClassLevelInfo.PartText(s0)+" → "+ClassLevelInfo.PartText(s1));
    foreach(var c in ClassSystem.All){
     p=ClsPawn(1);h=Adventurer.Get(p);h.scores[Adventurer.Con]=14;h.Dirty();float prev=Adventurer.PartHealthFactor(p);
     for(int lv=1;lv<=24;lv++){
      if(lv>1){h.level=lv;h.Dirty();float f=Adventurer.PartHealthFactor(p);if(f<prev-1e-4f)bad.Add(c.label+" 冒险者 "+lv);prev=f;}
      // 隔一级加一次点：加点前总有未分配的等级换成职业等级
      if(lv%2==0 && ClassSystem.Total(h)<Math.Min(lv,Adventurer.MaxLevel) && ClassSystem.Level(h,c)<ClassSystem.CapLevel){
       float pred=ClassLevelInfo.PartWith(h,c,h.level);ClassSystem.SetLevel(h,c,ClassSystem.Level(h,c)+1);float f=Adventurer.PartHealthFactor(p);
       if(f<prev-1e-4f || !Near(pred,f))bad.Add(c.label+" 加点 @"+lv+" "+prev.ToString("0.###")+"→"+f.ToString("0.###"));prev=f;
      }
     }
    }
    Check(bad.Count==0,"[1.93.0] part HP never drops on adventurer level-up (1→24) or on spending a class point, any class (screenshot wizard 3: "+ClassLevelInfo.PartText(s0)+" → "+ClassLevelInfo.PartText(s1)+")"+(bad.Count>0?": "+string.Join("、",bad.Take(8)):""));}
   // 5. 1.97.0（Peter：法师等级 +5%、智力 10%）：法师 10 级、智识 15、智力 18（调整值 +4）、冒险者 10 级（熟练 +4）
   //    奥术强度 = 0.5 + 15/20 + 4×10% + (4−2)×2% + 9×5% = 1.25 + 0.40 + 0.04 + 0.45 = ×2.14（旧 ×1.67）；器械强度的智力项仍 5%；下一级预算 +5%。
   {var p=ClsPawn(10);p.skills.GetSkill(SkillDefOf.Intellectual).Level=15;var h=Adventurer.Get(p);h.scores[Adventurer.Int]=18;h.Dirty();var W=Cls("Wizard");ClassSystem.SetLevel(h,W,10);
    var ap=DefDatabase<StatDef>.GetNamed("LP_ArcanePower");var part=ClassLevelInfo.Part(ap);var art=ClassLevelInfo.Part(DefDatabase<StatDef>.GetNamed("LP_ArtificePower"));
    float v=p.GetStatValue(ap),mod=h.AMod(Adventurer.Int),tm=Affinity.TalentMult(h,Adventurer.Int),sc=Adventurer.EffectScale;
    float want=1.25f+(Affinity.Talented(1f+.1f*mod,tm)-1f)*sc+.04f+.45f,next=ClassLevelInfo.PowerWith(p,h,ap,W,ClassLevelInfo.NextAdventurerLevel(h));
    bool plain=Near(mod,4f) && Near(tm,1f) && Near(sc,1f);string tip=ClassLevelInfo.ClassTip(p,h,W),expl=part.ExplanationPart(StatRequest.For(p));
    Check(part!=null && Near(part.abilityPer,.1f) && Near(part.LevelPer,.05f) && Near(part.LevelMax,.95f) && art!=null && Near(art.abilityPer,.05f) && Near(art.LevelPer,.02f)
     && Near(v,want) && (!plain || Near(v,2.14f)) && Near(next-v,.05f) && tip.Contains(ap.LabelCap+" +5%") && tip.Contains("最多 +95%") && expl.Contains("×10%") && expl.Contains("+5%"),
     "[1.97.0] wizard 10, Int skill 15, INT 18 (mod "+mod.ToString("0.##")+", talent ×"+tm.ToString("0.##")+", scale "+sc.ToString("0.##")+"): arcane power ×"+v.ToString("0.00")+" (want ×"+want.ToString("0.00")+", old ×1.67), next level ×"+next.ToString("0.00")+"; artificer still 5% / 2%");}
   // 6. 1.98.0（Peter：术士和法师一样）：术士 10 级、社交 12、魅力 18（调整值 +4）、冒险者 10 级（熟练 +4）
   //    天赋强度 = 0.5 + 12/20 + 4×10% + (4−2)×2% + 9×5% = 1.10 + 0.40 + 0.04 + 0.45 = ×1.99（旧 ×1.52）；神恩强度的魅力项仍 5%、等级 2%。
   {var p=ClsPawn(10,0,12);var h=Adventurer.Get(p);h.scores[Adventurer.Cha]=18;h.Dirty();var S=Cls("Sorcerer");ClassSystem.SetLevel(h,S,10);
    var ip=DefDatabase<StatDef>.GetNamed("LP_InnatePower");var part=ClassLevelInfo.Part(ip);var div=ClassLevelInfo.Part(DefDatabase<StatDef>.GetNamed("LP_DivinePower"));
    float v=p.GetStatValue(ip),mod=h.AMod(Adventurer.Cha),tm=Affinity.TalentMult(h,Adventurer.Cha),sc=Adventurer.EffectScale;
    float want=1.10f+(Affinity.Talented(1f+.1f*mod,tm)-1f)*sc+.04f+.45f+(ClassSystem.Bloodline(p)?ClassSystem.BloodlineBonus:0f),next=ClassLevelInfo.PowerWith(p,h,ip,S,ClassLevelInfo.NextAdventurerLevel(h));
    bool plain=Near(mod,4f) && Near(tm,1f) && Near(sc,1f) && !ClassSystem.Bloodline(p);string tip=ClassLevelInfo.ClassTip(p,h,S),expl=part.ExplanationPart(StatRequest.For(p));
    Check(part!=null && Near(part.abilityPer,.1f) && Near(part.LevelPer,.05f) && Near(part.LevelMax,.95f) && div!=null && Near(div.abilityPer,.05f) && Near(div.LevelPer,.02f)
     && Near(v,want) && (!plain || Near(v,1.99f)) && Near(next-v,.05f) && tip.Contains(ip.LabelCap+" +5%") && tip.Contains("最多 +95%") && expl.Contains("×10%") && expl.Contains("+5%"),
     "[1.98.0] sorcerer 10, Social 12, CHA 18 (mod "+mod.ToString("0.##")+", talent ×"+tm.ToString("0.##")+", scale "+sc.ToString("0.##")+"): innate power ×"+v.ToString("0.00")+" (want ×"+want.ToString("0.00")+", old ×1.52), next level ×"+next.ToString("0.00")+"; divine still 5% / 2%");}
   // 7. 1.99.0（Peter：不睡觉的种族回不了职业资源）：没有休息需求的人平时每 250 刻回上限 1/240（240 次 = 一天回满），正常人不多回；冥想调息回满；按钮只给不睡觉的人。
   {var map=Find.CurrentMap;var M=ClassSystem.Monk;var W=Cls("Wizard");
    Pawn Make(bool noSleep){var p=ClsPawn(8);var h=Adventurer.Get(p);ClassSystem.SetLevel(h,M,4);ClassSystem.SetLevel(h,W,4);if(noSleep)p.needs.rest=null;
     if(map!=null)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map);h.classes.res[M]=0f;h.classes.res[W]=0f;return p;}
    var ns=Make(true);var nm=Make(false);var hns=Adventurer.Get(ns);var hnm=Adventurer.Get(nm);
    bool calm=ClassRestless.Calm(ns);for(int i=0;i<ClassRestless.TrickleDiv/4;i++){ClassRestless.Tick250(hns);ClassRestless.Tick250(hnm);}
    float qk=ClassSystem.Resource(hns,M),qm=ClassSystem.Resource(hns,W),kMax=ClassSystem.MaxResource(hns,M),mMax=ClassSystem.MaxResource(hns,W);
    for(int i=0;i<ClassRestless.TrickleDiv;i++)ClassRestless.Tick250(hns);
    bool full=Near(ClassSystem.Resource(hns,M),kMax) && Near(ClassSystem.Resource(hns,W),mMax),normal=Near(ClassSystem.Resource(hnm,M),0f) && Near(ClassSystem.Resource(hnm,W),0f);
    hns.classes.res[M]=0f;hns.classes.res[W]=0f;ClassRestless.Refill(hns);bool refill=Near(ClassSystem.Resource(hns,M),kMax) && Near(ClassSystem.Resource(hns,W),mMax);
    bool gz=ns.GetGizmos().OfType<Command_Action>().Any(g=>g.defaultLabel=="冥想调息"),gzN=nm.GetGizmos().OfType<Command_Action>().Any(g=>g.defaultLabel=="冥想调息");
    ns.drafter.Drafted=true;bool draftedStop=!ClassRestless.Calm(ns);ns.drafter.Drafted=false;
    if(ns.Spawned)ns.DeSpawn();if(nm.Spawned)nm.DeSpawn();
    Check(map!=null && ClassRestless.NoSleep(ns) && !ClassRestless.NoSleep(nm) && calm && Near(qk,kMax/4f) && Near(qm,mMax/4f) && full && normal && refill && gz && !gzN && draftedStop && ClassRestless.AttuneJob!=null,
     "[1.99.0] no-sleep pawn: 6 h → ki "+qk.ToString("0.##")+"/"+kMax+", mana "+qm.ToString("0.##")+"/"+mMax+" (want 1/4), a day → full "+full+"; sleeping-race pawn unchanged "+normal+"; attune refills "+refill+"; gizmo only for no-sleep "+gz+"/"+gzN+"; drafted stops "+draftedStop);}
  }
 }
}
