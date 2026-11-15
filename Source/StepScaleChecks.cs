using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.155.0：Test.ps1 -Only StepScale：设置「每级属性效果」——100% 时数值不变，50% 时线性效果减半、表类倍率开平方，部位生命变少时伤口按比例缩小。
 public partial class LichChecks {
  void CheckStepScale(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oSup=Adventurer.TestSuppressed,oCo=s.adventurerIsekaiCoexist,oAff=s.adventurerAffinity;int oFx=s.adventurerEffectPercent,oStep=s.adventurerStepPercent;
   Adventurer.TestSuppressed=false;s.adventurer=true;s.adventurerIsekaiCoexist=false;s.adventurerAffinity=false;s.adventurerEffectPercent=100;s.adventurerStepPercent=100;
   try{
    // 1. 默认 100%：表类曲线和原来一模一样
    bool same=true;for(int sc=1;sc<=40;sc++)if(Math.Abs(Adventurer.ScoreCurve(sc)-Adventurer.Curve(sc))>.0001f)same=false;
    Check(same && Math.Abs(Adventurer.StepScale-1f)<.0001f && Math.Abs(Adventurer.Step(.1f)-.1f)<.00001f,"step scale 100% keeps the 3.5 table and per-modifier values unchanged");
    var p=AdvPawn(null);foreach(var tr in p.story.traits.allTraits.Where(tr=>HeroTraits.IsSlot(tr.def) || HeroTraits.IsDeed(tr.def) || Talents.Is(tr.def) || CombatGifts.Is(tr.def)).ToList())p.story.traits.RemoveTrait(tr);
    HeroTraits.Invalidate(p);CombatGifts.Invalidate(p);
    var h=Adventurer.Get(p);if(h.classes==null)h.classes=new ClassData();h.classes.levels.Clear();h.feats.Clear();h.featScore?.Clear();h.scores=new[]{20,14,16,10,12,8};h.legendSpent=new int[Legend.Count];h.Dirty();
    var melee=Hediff_Adventurer.Table.First(e=>e.stat=="MeleeDamageFactor");
    float lv100=h.LinearValue(Adventurer.Str,melee.per),am100=h.AMult(Adventurer.Str),con100=h.ConPart(1f),part100=Adventurer.PartHealthFactor(p);
    float st100=h.CurStage.statFactors.First(m=>m.stat==StatDefOf.MeleeDamageFactor).value;
    // 2. 50%：近战 +10% → +5%（力量 20 = ×1.25），负重表 ×4 → ×2，体质 16 部位生命 1.45 → 1.225，已建好的状态会重建
    s.adventurerStepPercent=50;
    float lv50=h.LinearValue(Adventurer.Str,melee.per),am50=h.AMult(Adventurer.Str),con50=h.ConPart(1f),part50=Adventurer.PartHealthFactor(p);
    float st50=h.CurStage.statFactors.First(m=>m.stat==StatDefOf.MeleeDamageFactor).value;
    Check(Math.Abs(lv100-1.5f)<.001f && Math.Abs(lv50-1.25f)<.001f && Math.Abs(am100-4f)<.001f && Math.Abs(am50-2f)<.001f && Math.Abs(con100-1.45f)<.001f && Math.Abs(con50-1.225f)<.001f,
     "step scale 50%: Str 20 melee ×"+lv100.ToString("0.###")+" → ×"+lv50.ToString("0.###")+" (1.5 → 1.25), carry table ×"+am100.ToString("0.##")+" → ×"+am50.ToString("0.##")+" (4 → 2), Con 16 part ×"+con100.ToString("0.###")+" → ×"+con50.ToString("0.###"));
    Check(Math.Abs(st50/st100-lv50/lv100)<.002f,"step scale change rebuilds the cached stage: melee stat factor "+st100.ToString("0.###")+" → "+st50.ToString("0.###"));
    // 3. 10%：Peter 的例子，每 +2 属性近战约 +1%
    s.adventurerStepPercent=10;float lv10=h.LinearValue(Adventurer.Str,melee.per);
    Check(Math.Abs(lv10-1.05f)<.001f && Math.Abs(Adventurer.StepCurve-Mathf.Pow(4f,.02f))<.0001f,"step scale 10%: Str 20 melee ×"+lv10.ToString("0.###")+" (+1% per modifier), table ×"+Adventurer.StepCurve.ToString("0.####")+" per +2");
    // 4. 调低后部位生命变少：伤口按比例缩小
    s.adventurerStepPercent=50;
    var part=p.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso);
    var inj=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,p,part);inj.Severity=10f;p.health.AddHediff(inj,part);float before=inj.Severity;
    int n=Adventurer.RescaleStep(new[]{p},1f);
    Check(n==1 && part50<part100 && Math.Abs(inj.Severity-before*part50/part100)<.01f,"lower step scale shrinks wounds: part ×"+part100.ToString("0.###")+" → ×"+part50.ToString("0.###")+", cut "+before.ToString("0.##")+" → "+inj.Severity.ToString("0.##"));
    // 5. 属性页公式跟着设置走
    string fm=AttributePage.Formula(melee);
    Check(fm.Contains("5%"),"attribute page formula uses the scaled per-modifier value: '"+fm+"'");
   }
   finally{s.adventurer=oOn;Adventurer.TestSuppressed=oSup;s.adventurerIsekaiCoexist=oCo;s.adventurerAffinity=oAff;s.adventurerEffectPercent=oFx;s.adventurerStepPercent=oStep;Adventurer.stepForced=-1f;}
  }
 }
}
