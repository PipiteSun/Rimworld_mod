using System;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.61.0 热修（日常工作经验）：Test.ps1 -Only Adventurer 的日常工作部分。由 CheckAdventurer 在同一个 try 里调用；存读档在 PrepareAdventurerSave / CheckAdventurerAfterLoad。
 public partial class LichChecks {
  void CheckAdventurerWork(){
   var s=LichMod.Settings;float oldWork=s.adventurerWorkXp;
   try{
    s.adventurerWorkXp=1f;
    var a=AdvPawn();var h=Adventurer.Get(a);float scale=Adventurer.XpScale*Talents.XpFactor(h);
    var sk=DefDatabase<SkillDef>.AllDefsListForReading.First(d=>!a.skills.GetSkill(d).TotallyDisabled);
    Func<float> total=()=>h.xp/scale+h.workXpRem; // 基础值：已入账的经验折回倍率前 + 攒着的余数
    int day=Feats.LocalDay(a);
    // a. 1000 技能经验 → +20（全额）
    h.workXpDay=-1;h.workXpToday=0f;h.workXpRem=0f;h.workXpTotal=0f;
    float x0=h.xp,t0=total();a.skills.Learn(sk,1000f,false,false);
    Check(Near(total()-t0,20f) && Near(h.xp-x0,20f*scale) && Near(h.workXpRem,0f) && Near(AdventurerWork.Today(h),20f) && h.workXpDay==day && Near(h.workXpTotal,20f),"work XP: 1000 "+sk.defName+" skill XP → +20 adventurer XP ("+(h.xp-x0)+" after ×"+scale.ToString("0.##")+"), today 20 / 80");
    // b. 今天已满 80：1025 → 20.5 × 0.5 = +10.25（入账 10，余 0.25）
    h.workXpToday=80f;h.workXpRem=0f;x0=h.xp;t0=total();a.skills.Learn(sk,1025f,false,false);
    Check(Near(total()-t0,10.25f) && Near(h.xp-x0,10f*scale) && Near(h.workXpRem,.25f) && Near(h.workXpToday,100.5f),"after 80 today, 1025 more → +10.25 (×0.5; 10 credited, 0.25 kept as remainder)");
    // 跨过 80：70 → 再得 20 = 10 全额 + 10 × 0.5
    h.workXpToday=70f;h.workXpRem=0f;t0=total();a.skills.Learn(sk,1000f,false,false);
    Check(Near(total()-t0,15f),"crossing 80 (70 + 20) → 10 full + 10 × 0.5 = 15");
    // c. 换日：昨天的 80 不算
    h.workXpDay=day-1;h.workXpToday=80f;h.workXpRem=0f;t0=total();float before=AdventurerWork.Today(h);a.skills.Learn(sk,1000f,false,false);
    Check(Near(before,0f) && Near(total()-t0,20f) && h.workXpDay==day && Near(h.workXpToday,20f),"a new local day resets the daily 80");
    // d. 倍率 0 → 关闭；倍率 2 → 翻倍
    s.adventurerWorkXp=0f;h.workXpRem=0f;h.workXpDay=day;h.workXpToday=0f;x0=h.xp;a.skills.Learn(sk,1000f,false,false);bool offNone=Near(h.xp,x0) && Near(h.workXpRem,0f) && Near(h.workXpToday,0f);
    string offTip=ITab_Adventurer.XpSourcesTip(h);
    s.adventurerWorkXp=2f;t0=total();a.skills.Learn(sk,1000f,false,false);float doubled=total()-t0;s.adventurerWorkXp=1f;
    Check(offNone && offTip.Contains("日常工作：已关闭") && Near(doubled,40f),"multiplier 0 → no work XP and the tooltip says 已关闭; multiplier 2 → +40");
    // e. direct（训练、神经增强）不算；敌人不算
    h.workXpRem=0f;h.workXpToday=0f;x0=h.xp;a.skills.Learn(sk,1000f,true,false);bool directNone=Near(h.xp,x0) && Near(h.workXpRem,0f) && Near(h.workXpToday,0f);
    var enemy=AdvEnemy;var e=enemy==null?null:AdvPawn(null,enemy);var he=Adventurer.Get(e);float ex0=he?.xp??0f;
    var esk=e==null?null:DefDatabase<SkillDef>.AllDefsListForReading.FirstOrDefault(d=>!e.skills.GetSkill(d).TotallyDisabled);
    if(esk!=null)e.skills.Learn(esk,1000f,false,false);
    Check(directNone && (he==null || Near(he.xp,ex0) && Near(he.workXpToday,0f)),"direct skill XP and non-colony pawns give no work XP");
    // 悬停说明
    h.workXpDay=day;h.workXpToday=12f;string tip=ITab_Adventurer.XpSourcesTip(h);
    Check(tip.Contains("日常工作：每 50 技能经验") && tip.Contains("今日 12 / 80 全额，之后 ×0.5"),"XP tooltip lists 日常工作 with today's 12 / 80");
   }finally{s.adventurerWorkXp=oldWork;}
  }
 }
}
