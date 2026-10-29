using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.51.0：Test.ps1 -Only Adventurer 的种族加点亲和与稀有天赋部分。由 CheckAdventurer 在同一个 try 里调用。
 public partial class LichChecks {
  static string advSavedAffinity;
  static string AffinitySummary(Hediff_Adventurer h)=>h.Affinity.key+"|"+h.Talent?.defName+"|"+Affinity.LegendMult(h,Legend.Health).ToString("0.###")+"|"+h.legendPoints;
  static float Research(Hediff_Adventurer h)=>h.BuildStage(1f).statFactors?.GetStatFactorFromList(StatDefOf.ResearchSpeed)??1f;
  Hediff_Adventurer AdvAt(Pawn p,int intScore){var h=Adventurer.Get(p);h.scores=new[]{10,10,10,10,10,10};h.scores[Adventurer.Int]=intScore;h.legendSpent=new int[Legend.Count];h.legendPoints=0;h.Dirty();return h;}
  Pawn AdvTalentPawn(string xeno,TraitDef talent){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:DefDatabase<XenotypeDef>.GetNamed(xeno),mustBeCapableOfViolence:true,forceNoBackstory:true,forcedTraits:new[]{talent},fixedBiologicalAge:25f,fixedChronologicalAge:25f));
   advMade.Add(p);return p;
  }
  void CheckAdventurerStage5(){
   var s=LichMod.Settings;bool oAff=s.adventurerAffinity,oL=s.adventurerLegend,oAuto=s.adventurerLegendAuto;bool oStrip=Talents.StripAll;
   s.adventurerAffinity=true;s.adventurerLegend=true;s.adventurerLegendAuto=false;
   try{
    // 1. 表：每个有种族加值的种族都有亲和条目；传奇种族分档
    var missing=Adventurer.Racial.Keys.Where(k=>!Affinity.Table.ContainsKey(k)).ToList();
    var t=Affinity.Table;
    Check(missing.Count==0 && t["LP_FireGiant"].legendary && t["Lich"].legendary && t["LP_Ettin"].legendary && !t["LP_Elf"].legendary && !t["LP_HalfOrc"].legendary && !t[Affinity.HumanKey].legendary && Near(t["LP_FireGiant"].StrongMult,1.75f) && Near(t["LP_Elf"].StrongMult,1.5f),"affinity table covers every racial-bonus race ("+(missing.Count==0?"ok":"missing "+string.Join(",",missing))+"); giants/lich are legendary-tier ×1.75, elf/half-orc/human common ×1.5");
    // 2. 精灵智力 +2：研究速度收益 = 人类 1.5 倍；人类传奇 ×1.1
    var human=AdvPawn();var elf=AdvPawn(DefDatabase<XenotypeDef>.GetNamed("LP_Elf"));
    var hh=AdvAt(human,14);var he=AdvAt(elf,14);
    float rh=Research(hh)-1f,re=Research(he)-1f;
    hh.legendSpent[Legend.Health]=10;
    Check(he.Affinity.key=="LP_Elf" && hh.Affinity.key==Affinity.HumanKey && Near(rh,.75f) && Near(re,1.625f) && Near(Adventurer.LearnFactor(elf,SkillDefOf.Intellectual),2.625f) && Near(Legend.HealthFactor(hh),1.22f) && Affinity.Tag(he,Adventurer.Int)==" 擅长","elf Int +2 → research +"+(re*100f).ToString("0.#")+"% = 1.5 × human +"+(rh*100f).ToString("0.#")+"%, intellectual learning ×2.625 (1.75 × 1.5); human legend health 10 = +22% (×1.1)");
    // 3. 半兽人智力笨拙：+2 收益 ×0.5，−2 不乘
    var orc=AdvPawn(DefDatabase<XenotypeDef>.GetNamed("LP_HalfOrc"));var ho=AdvAt(orc,14);float up=Research(ho);
    ho.scores[Adventurer.Int]=6;ho.Dirty();float down=Research(ho);
    ho.legendSpent[Legend.Melee]=10;float om=ho.BuildStage(0f).statFactors.GetStatFactorFromList(StatDefOf.MeleeDamageFactor);
    Check(Near(up,1f) && Near(down,.6f) && Affinity.Tag(ho,Adventurer.Int)==" 笨拙" && Near(om,1.45f),"half-orc Int +2 → research ×"+up.ToString("0.###")+" (×0.5), Int −2 → ×"+down.ToString("0.###")+" (not multiplied); legend melee 10 = +45% (common-race strong ×1.5)");
    // 4. 火巨人（传奇种族）传奇血量 10 点 = +35%（×1.75）；属性数值和上限不变
    var giant=AdvPawn(DefDatabase<XenotypeDef>.GetNamed("LP_FireGiant"));var hg=AdvAt(giant,10);hg.legendSpent[Legend.Health]=10;
    hg.scores[Adventurer.Con]=14;hg.Dirty();
    Check(hg.Affinity.key=="LP_FireGiant" && Near(Legend.HealthFactor(hg),1.35f) && Near(hg.AMod(Adventurer.Con),3.5f) && hg.Mod(Adventurer.Con)==2 && hg.Cap(Adventurer.Con)==20,"fire giant (legendary tier): legend health 10 = ×"+Legend.HealthFactor(hg).ToString("0.###")+", Con +2 counts as +3.5 for effects while the score/modifier/cap stay 14/+2/20");
    // 5. HAR 双头巨人按种族 def 查表
    var ettinKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_EttinColonist");
    if(ettinKind!=null){
     var ettin=PawnGenerator.GeneratePawn(new PawnGenerationRequest(ettinKind,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:25f,fixedChronologicalAge:25f));advMade.Add(ettin);
     var hx=Adventurer.Get(ettin);
     Check(hx!=null && ettin.def.defName=="LP_Ettin" && hx.Affinity.key=="LP_Ettin" && hx.Affinity.legendary && Near(Affinity.ScoreMult(hx,Adventurer.Con),1.75f) && Near(Affinity.ScoreMult(hx,Adventurer.Int),.5f) && Near(Affinity.LegendMult(hx,Legend.Melee),1.75f),"HAR ettin uses the LP_Ettin race entry: Con ×1.75, Int ×0.5, legend melee ×1.75");
    }else Check(!ModsConfig.IsActive("erdelf.humanoidalienraces"),"HAR ettin affinity check skipped (HAR not loaded)");
    // 6. 设置关掉：全部 ×1；部位生命变少时伤口缩小
    s.adventurerAffinity=false;he.Dirty();hh.Dirty();
    float reOff=Research(he)-1f;bool allOne=Near(Affinity.ScoreMult(he,Adventurer.Int),1f) && Near(Affinity.LegendMult(hh,Legend.Health),1f) && Near(Legend.HealthFactor(hg),1.2f) && Affinity.Tag(he,Adventurer.Int)=="";
    s.adventurerAffinity=true;he.Dirty();
    var torso=giant.RaceProps.body.corePart;var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,giant,torso);cut.Severity=10f;giant.health.AddHediff(cut);
    int scaled=Adventurer.RescaleAffinity(new[]{giant});
    Check(allOne && Near(reOff,.75f) && scaled==1 && cut.Severity<10f,"affinity off → every multiplier ×1 (elf research +"+(reOff*100f).ToString("0.#")+"%, giant legend health ×1.2); wounds shrink with the lost part health (10 → "+cut.Severity.ToString("0.##")+")");
    // ---------- 稀有天赋 ----------
    // 7. 只在普通种族身上：传奇种族生成时去掉；同一人最多一个
    Talents.StripAll=false;var tg=AdvTalentPawn("LP_FireGiant",Talents.Gifted);var te=AdvTalentPawn("LP_Elf",Talents.Gifted);Talents.StripAll=oStrip;
    Check(Talents.All.All(d=>d!=null && d.exclusionTags.Contains(Talents.ExclusionTag) && !d.exclusionTags.Contains(HeroTraits.PowerTag) && HeroTraits.IsSlot(d) && Near(d.GetGenderSpecificCommonality(Gender.Male),0f)) && Talents.Of(tg)==null && Talents.Of(te)==Talents.Gifted && Adventurer.Get(te).Talent==Talents.Gifted && Talents.Gifted.ConflictsWith(Talents.Tempered),"talent traits: commonality 0 (1.58.0 rolled with the power traits, shared LP_PowerTrait slot), mutually exclusive, a generated fire giant loses 天赋异禀 while an elf keeps it");
    // 8. 天赋异禀：×1.5，与种族倍率取较大（不相乘）
    var hte=AdvAt(te,14);var gh=AdvPawn();var hgh=AdvAt(gh,14);gh.story.traits.GainTrait(new Trait(Talents.Gifted));
    var go=AdvPawn(DefDatabase<XenotypeDef>.GetNamed("LP_HalfOrc"));var hgo=AdvAt(go,14);go.story.traits.GainTrait(new Trait(Talents.Gifted));
    Check(hgh.Talent==Talents.Gifted && Near(Research(hgh),2.625f) && Near(Research(hte),2.625f) && Near(Research(hgo),2.625f) && Near(Affinity.ScoreMult(hgh,Adventurer.Str),1.5f),"天赋异禀: human Int +2 research ×"+Research(hgh).ToString("0.###")+"; elf strong Int stays ×1.5 (max, not ×2.25); half-orc weak Int lifted to ×1.5");
    // 9. 大器晚成：1～20 级每 4 级 1 点（5 点），传奇等级每级 3 点
    var lb=AdvPawn();lb.story.traits.GainTrait(new Trait(Talents.LateBloomer));var hlb=Adventurer.Get(lb);
    Adventurer.GainXP(lb,Legend.XpFor(20)-hlb.xp);int at20=hlb.legendPoints;hlb.legendary=true;Adventurer.GainXP(lb,Legend.Step(21));
    Check(hlb.level==21 && at20==5 && hlb.legendPoints==8,"大器晚成: 5 legend points by level 20 (one per 4 levels), level 21 gives 3 (got "+at20+" → "+hlb.legendPoints+")");
    // 10. 百炼成钢：每次属性提升 3 点，20 级也有
    var tp=AdvPawn();tp.story.traits.GainTrait(new Trait(Talents.Tempered));var htp=Adventurer.Get(tp);
    htp.Setup(new[]{10,10,10,10,10,10},20);int asi=htp.pendingASI;
    bool three=htp.ApplyASI(Adventurer.Str) && htp.scores[Adventurer.Str]==13;bool split=htp.ApplyASI(Adventurer.Dex,Adventurer.Con) && htp.scores[Adventurer.Dex]==12 && htp.scores[Adventurer.Con]==11;
    var np=AdvPawn();var hnp=Adventurer.Get(np);hnp.Setup(new[]{10,10,10,10,10,10},20);
    Check(asi==5 && hnp.pendingASI==4 && three && split && htp.ASIPoints==3,"百炼成钢: 5 ASIs by level 20 (normal 4), +3 to one or +2/+1");
    // 11. 凡人之志：经验 +25%，所有传奇属性 ×1.5（人类原本 ×1.1）
    var mw=AdvPawn();mw.story.traits.GainTrait(new Trait(Talents.MortalWill));var hmw=Adventurer.Get(mw);float xp=Adventurer.GainXP(mw,100f);
    Check(Near(xp,125f) && Near(Affinity.LegendMult(hmw,Legend.Health),1.5f) && Near(Affinity.LegendMult(hmw,Legend.Work),1.5f) && Affinity.LegendTag(hmw,Legend.Work)==" 擅长","凡人之志: 100 XP → "+xp+", every legend stat ×1.5 for a human (instead of ×1.1)");
   }finally{
    s.adventurerAffinity=oAff;s.adventurerLegend=oL;s.adventurerLegendAuto=oAuto;Talents.StripAll=oStrip;
   }
  }
  void PrepareAffinitySave(Hediff_Adventurer h){
   bool o=Talents.StripAll;Talents.StripAll=false;
   try{h.pawn.story.traits.GainTrait(new Trait(Talents.LateBloomer));}finally{Talents.StripAll=o;}
   advSavedAffinity=AffinitySummary(h);
  }
  void CheckAffinityAfterLoad(Hediff_Adventurer h){Check(h!=null && AffinitySummary(h)==advSavedAffinity,"race affinity and talent survive save/load ("+advSavedAffinity+")");}
 }
}
