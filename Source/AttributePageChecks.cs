using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 2026-10-30：Test.ps1 -Only AttributePage：冒险者分页的“属性”页——公式从效果表生成、数值和“冒险者”页的展开表一致、超凡 / 传奇点叠加列。
 public partial class LichChecks {
  void CheckAttributePage(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oSup=Adventurer.TestSuppressed;
   Adventurer.TestSuppressed=false;s.adventurer=true;
   try{
    var p=AdvPawn(null);foreach(var tr in p.story.traits.allTraits.Where(tr=>HeroTraits.IsSlot(tr.def) || HeroTraits.IsDeed(tr.def) || Talents.Is(tr.def) || CombatGifts.Is(tr.def)).ToList())p.story.traits.RemoveTrait(tr);
    HeroTraits.Invalidate(p);CombatGifts.Invalidate(p);
    var h=Adventurer.Get(p);if(h.classes==null)h.classes=new ClassData();h.classes.levels.Clear();h.feats.Clear();h.featScore?.Clear();h.scores=new[]{20,14,16,10,12,8};h.legendSpent=new int[Legend.Count];h.Dirty();var st=h.CurStage;
    // 1. 每一项属性：表里每个效果都有一行，数值和冒险者页 EffectRows 一样
    bool all=true;string bad="";
    for(int a=0;a<6;a++){
     var lines=AttributePage.Lines(h,a,AttributePage.ExtrasOf(h));var rows=h.EffectRows(a);
     foreach(var e in Hediff_Adventurer.Table.Where(e=>e.a==a && DefDatabase<StatDef>.GetNamedSilentFail(e.stat)!=null))if(!lines.Any(l=>l.label==e.label)){all=false;bad+=" missing "+e.label;}
     foreach(var l in lines){var r=rows.FirstOrDefault(kv=>kv.Key==l.label);if(r.Key!=null && r.Value!=l.value){all=false;bad+=" "+l.label+" "+l.value+"≠"+r.Value;}}
     if(lines.Any(l=>string.IsNullOrEmpty(l.formula))){all=false;bad+=" empty formula "+Adventurer.Names[a];}
    }
    Check(all,"attribute page: every table effect listed with the same value as the adventurer page"+bad);
    // 2. 公式文字跟着表的参数走
    var melee=Hediff_Adventurer.Table.First(e=>e.stat=="MeleeDamageFactor");var research=Hediff_Adventurer.Table.First(e=>e.stat=="ResearchSpeed");var dodge=Hediff_Adventurer.Table.First(e=>e.stat=="MeleeDodgeChance");
    string fm=AttributePage.Formula(melee),fr=AttributePage.Formula(research),fd=AttributePage.Formula(dodge),sub=AttributePage.Substitute(h,melee,1f);
    Check(fm.Contains((melee.per*100f).ToString("0.#")+"%") && fr.Contains((research.per*100f).ToString("0.#")+"%") && fd.Contains("(M − 1)") && sub.Contains(h.LinearValue(0,melee.per).ToString("0.00")),"attribute page formulas from table: melee '"+fm+"', research '"+fr+"', dodge '"+fd+"', melee substitution '"+sub.Replace("\n"," | ")+"'");
    // 3. 超凡力量 20：负重一行的叠加列写“超凡 ×1.25”；体质有部位生命合计
    bool exOn=(h.exaltedMask&1)!=0;var carry=AttributePage.Lines(h,0,AttributePage.ExtrasOf(h)).FirstOrDefault(l=>l.label=="负重");var con=AttributePage.Lines(h,2);
    Check(!Exalted.On || exOn && carry?.extra!=null && carry.extra.Contains("超凡 ×1.25"),"attribute page extras: exalted Str carry '"+carry?.extra+"' (mask "+h.exaltedMask+")");
    Check(con.Any(l=>l.label=="部位生命合计" && l.value=="×"+Adventurer.PartHealthFactor(p).ToString("0.00")) && AttributePage.Composition(h,0).Contains("有效 20") && AttributePage.MultText(h,0).Contains("×4.00"),"attribute page composition / Con total: '"+AttributePage.Composition(h,0)+"' '"+AttributePage.MultText(h,0)+"'");
   }finally{
    s.adventurer=oOn;Adventurer.TestSuppressed=oSup;
    foreach(var x in htMade){if(!x.Destroyed)x.Destroy();}htMade.Clear();
   }
  }
 }
}
