using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.81.0 热修：Test.ps1 -Only ResearchSpeed：研究速度照 Isekai 算（每点智力 +6%，×5 后收敛，另乘全局工作速度）；黑客速度不再叠第二次智力。
 // 复现存档里的“法师”：智力 17、极致·智力、百炼成钢、天才之资，智识 20。修之前研究速度 17460%（冒险者 ×10.06 再乘全局工作速度 ×7.29）。
 public partial class LichChecks {
  void CheckResearchSpeed(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oSup=Adventurer.TestSuppressed;
   Adventurer.TestSuppressed=false;s.adventurer=true;
   try{
    Hediff_Adventurer Make(bool wizard,int intScore){
     var p=AdvPawn(null);foreach(var tr in p.story.traits.allTraits.Where(tr=>HeroTraits.IsSlot(tr.def) || HeroTraits.IsDeed(tr.def) || Talents.Is(tr.def) || CombatGifts.Is(tr.def)).ToList())p.story.traits.RemoveTrait(tr);
     if(wizard){HeroTraits.Give(p,HeroDefOf.LP_Paragon,4);p.story.traits.GainTrait(new Trait(Talents.Tempered));p.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed("LP_CG_Brilliant")));}
     HeroTraits.Invalidate(p);CombatGifts.Invalidate(p);
     p.skills.GetSkill(SkillDefOf.Intellectual).Level=20;
     var h=Adventurer.Get(p);if(h.classes==null)h.classes=new ClassData();h.classes.levels.Clear();h.level=3;h.scores=new[]{10,10,13,10,15,8};h.scores[Adventurer.Int]=intScore;h.legendSpent=new int[Legend.Count];h.legendPoints=0;h.Dirty();return h;
    }
    float F(Hediff_Adventurer h,StatDef d)=>d==null?1f:h.BuildStage(1f).statFactors?.GetStatFactorFromList(d)??1f;
    // 1. 普通人类：智力 20 = ×1.6、14 = ×1.24、6 = ×0.76（Isekai 每点 6%，基准 10）
    float r20=F(Make(false,20),StatDefOf.ResearchSpeed),r14=F(Make(false,14),StatDefOf.ResearchSpeed),r6=F(Make(false,6),StatDefOf.ResearchSpeed);
    Check(Mathf.Abs(r20-1.6f)<.001f && Mathf.Abs(r14-1.24f)<.001f && Mathf.Abs(r6-.76f)<.001f,"Isekai research: Int 20 ×"+r20.ToString("0.###")+" (1.6), 14 ×"+r14.ToString("0.###")+" (1.24), 6 ×"+r6.ToString("0.###")+" (0.76)");
    // 2. 法师：有效智力 19 → 1.54，极致 ×2.5 = 3.85，再乘天才之资；全局工作速度仍按表另乘
    var w=Make(true,17);var p0=w.pawn;float gift=1f+.15f*CombatGifts.Mult(p0),rs=F(w,StatDefOf.ResearchSpeed),ws=F(w,StatDefOf.WorkSpeedGlobal);
    float expect=Hediff_Adventurer.SoftCap((1f+.06f*(w.Eff(Adventurer.Int)-10))*2.5f,5f)*gift;
    Check(w.Eff(Adventurer.Int)==19 && Mathf.Abs(rs-expect)<.01f && ws>5f,"wizard research ×"+rs.ToString("0.###")+" (expected ×"+expect.ToString("0.###")+": Int 19 → 1.54 × paragon 2.5 × gift "+gift.ToString("0.##")+"), work ×"+ws.ToString("0.###"));
    // 3. 超过 ×5 按 Isekai 的 SoftCap 收敛
    var hi=Make(true,26);float raw=(1f+.06f*(hi.Eff(Adventurer.Int)-10))*2.5f,v=hi.PerPointValue(Hediff_Adventurer.Table.First(e=>e.stat=="ResearchSpeed"));
    Check(raw>5f && Mathf.Abs(v-Hediff_Adventurer.SoftCap(raw,5f))<.001f && v<raw,"research above ×5 soft-caps like Isekai: raw ×"+raw.ToString("0.###")+" → ×"+v.ToString("0.###"));
    // 4. 黑客速度：表里的值和全局工作速度一样（同为 ×5 软上限），不再叠第二次
    float hs=F(w,DefDatabase<StatDef>.GetNamedSilentFail("HackingSpeed"));
    Check(Mathf.Abs(hs-1f)<.001f,"hacking speed gets no second Int factor on top of work speed: ×"+hs.ToString("0.###"));
    // 5. 实际属性值 ≈ 智识 20 ×2.38 × 研究 × 工作速度，远低于修之前的 17460%
    StatDefOf.ResearchSpeed.Worker.ClearCacheForThing(p0);float val=p0.GetStatValue(StatDefOf.ResearchSpeed);
    Check(val<2.38f*rs*ws*1.05f && val>2.38f*rs*ws*.25f && val<100f,"research speed stat "+(val*100f).ToString("0")+"% (≈ 238% × "+rs.ToString("0.##")+" × "+ws.ToString("0.##")+", was 17460% before the fix)");
    // 6. 制造品质改按器械师等级（每级 +0.1 档）：智力不再给；10 级必定 +1，5 级 50% 几率，20 级 +2，传说封顶
    var a10=Make(false,10);var a5=Make(false,10);var a20=Make(false,10);var cls=Artificer.Class;
    a10.classes.levels[cls]=10;a5.classes.levels[cls]=5;a20.classes.levels[cls]=20;
    float t10=ArtificerCraftQuality.Tiers(a10.pawn),t5=ArtificerCraftQuality.Tiers(a5.pawn),tInt=ArtificerCraftQuality.Tiers(Make(false,20).pawn),tw=ArtificerCraftQuality.Tiers(p0);
    ArtificerCraftQuality.TestRoll=.99f;var sure=ArtificerCraftQuality.Boost(a10.pawn,QualityCategory.Normal);var two=ArtificerCraftQuality.Boost(a20.pawn,QualityCategory.Normal);
    ArtificerCraftQuality.TestRoll=.05f;var up=ArtificerCraftQuality.Boost(a5.pawn,QualityCategory.Normal);
    ArtificerCraftQuality.TestRoll=.5f;var stay=ArtificerCraftQuality.Boost(a5.pawn,QualityCategory.Normal);
    var top=ArtificerCraftQuality.Boost(a20.pawn,QualityCategory.Masterwork);ArtificerCraftQuality.TestRoll=null;
    Check(cls!=null && Mathf.Abs(t10-1f)<.001f && Mathf.Abs(t5-.5f)<.001f && tInt==0f && tw==0f && sure==QualityCategory.Good && two==QualityCategory.Excellent && up==QualityCategory.Good && stay==QualityCategory.Normal && top==QualityCategory.Legendary,"artificer craft quality: lv10 +"+t10.ToString("0.###")+" (normal → "+sure+"), lv20 → "+two+", lv5 +"+t5.ToString("0.###")+" (roll .05 → "+up+", .5 → "+stay+"), Int 20 non-artificer +"+tInt+", wizard +"+tw+", masterwork caps at "+top);
    // 7. 智力那一栏不再有“制造品质”；器械师说明写了这条
    Check(!AttributePage.Lines(w,Adventurer.Int).Exists(l=>l.label=="制造品质") && cls.description.Contains("期望品质 +0.1 档"),"Int no longer lists 制造品质; artificer description mentions the per-level quality bonus");
   }finally{
    ArtificerCraftQuality.TestRoll=null;
    foreach(var x in htMade){if(!x.Destroyed)x.Destroy();}htMade.Clear();
   }
  }
 }
}
