using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 2026-11-10：Test.ps1 -Only PartHpTotal：总生命随部位生命缩放（AdventurerLethalThreshold / AdventurerSummaryHealth / AdventurerBleedScale）。
 // 复现 Peter 的问题：20 级战士体质 20 部位生命 ×3.78，躯干 152 比原版全身致死阈值 150 还高。
 public partial class LichChecks {
  void CheckPartHpTotal(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oSup=Adventurer.TestSuppressed,oTotal=s.adventurerTotalScale;
   Adventurer.TestSuppressed=false;s.adventurer=true;
   try{
    var p=AdvPawn(null);var h=Adventurer.Get(p);if(h.classes==null)h.classes=new ClassData();h.classes.levels.Clear();
    var fighter=DefDatabase<LP_ClassDef>.GetNamedSilentFail("LP_Class_Fighter");if(fighter!=null)h.classes.levels[fighter]=20;
    h.level=20;h.scores=new[]{10,10,20,10,10,10};h.Dirty();
    float f=Adventurer.PartHealthFactor(p);var torso=p.RaceProps.body.corePart;float torsoMax=PartHealth.Maximum(torso,p);
    // 1. 致死阈值：开 = 150 × HealthScale × 倍率，关 = 原版
    s.adventurerTotalScale=true;float on=p.health.LethalDamageThreshold;s.adventurerTotalScale=false;float off=p.health.LethalDamageThreshold;s.adventurerTotalScale=true;
    Check(f>3f && Mathf.Abs(off-150f*p.HealthScale)<.01f && Mathf.Abs(on-off*f)<.05f && on>torsoMax,"[part hp total] lethal threshold ×part factor: factor ×"+f.ToString("0.##")+", threshold "+off.ToString("0")+" → "+on.ToString("0")+", torso max "+torsoMax.ToString("0"));
    // 2. 健康%：同一道伤口扣的 ÷ 倍率；3. 失血速度 ÷ 倍率
    var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,p,torso);cut.Severity=20f;p.health.AddHediff(cut,torso);
    var bleed=AccessTools.Method(typeof(HediffSet),"CalculateBleedRate");
    float iOn=cut.SummaryHealthPercentImpact,bOn=(float)bleed.Invoke(p.health.hediffSet,null);
    s.adventurerTotalScale=false;float iOff=cut.SummaryHealthPercentImpact,bOff=(float)bleed.Invoke(p.health.hediffSet,null);s.adventurerTotalScale=true;
    Check(iOff>0f && Mathf.Abs(iOn*f-iOff)<.0005f && bOff>0f && Mathf.Abs(bOn*f-bOff)<.0005f,"[part hp total] health% impact and bleed ÷part factor: impact "+iOff.ToString("0.###")+" → "+iOn.ToString("0.###")+", bleed "+bOff.ToString("0.###")+" → "+bOn.ToString("0.###"));
    p.health.RemoveHediff(cut);
    // 4. 160 点伤口分在几个部位上：开着不会死于伤势过重（原版 150 会死）
    var parts=p.health.hediffSet.GetNotMissingParts().Where(x=>x.depth==BodyPartDepth.Outside && x.def.GetMaxHealth(p)>=20f && !x.def.tags.Any(t=>t.vital)).Take(4).ToList();
    if(!parts.Contains(torso))parts.Insert(0,torso);float per=160f/parts.Count;
    foreach(var part in parts){var c=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,p,part);c.Severity=Mathf.Min(per,PartHealth.Maximum(part,p)-1f);p.health.AddHediff(c,part);}
    float sum=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity);
    bool aliveOn=!p.Dead && !p.health.ShouldBeDead();s.adventurerTotalScale=false;bool deadOff=p.health.ShouldBeDead();s.adventurerTotalScale=true;
    Check(sum>150f && aliveOn && deadOff,"[part hp total] "+sum.ToString("0")+" damage over "+parts.Count+" parts: alive with the setting on, lethal with it off (dead "+p.Dead+")");
    // 5. 没有冒险者状态的普通人和传奇单位不变
    var plain=PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,null);var hp=Adventurer.Get(plain);if(hp!=null)plain.health.RemoveHediff(hp);
    Check(Mathf.Abs(Adventurer.TotalDivisor(plain)-1f)<.0001f && Mathf.Abs(Adventurer.TotalDivisor(null)-1f)<.0001f,"[part hp total] pawns without adventurer levels keep vanilla totals");
    if(!p.Dead)p.Destroy();if(!plain.Destroyed)plain.Destroy();
   }
   finally{s.adventurer=oOn;Adventurer.TestSuppressed=oSup;s.adventurerTotalScale=oTotal;}
  }
 }
}
