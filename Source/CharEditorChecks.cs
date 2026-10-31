using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.86.0：Test.ps1 [-CE] -Only CharEditor：角色编辑器联动——装了编辑器时补丁挂上；面板用的修改函数（等级、六维、职业 / 子职业、专长、恩赐、特性）和模组自己的状态同步。
 public partial class LichChecks {
  void CheckCharEditor(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oSup=Adventurer.TestSuppressed;
   Adventurer.TestSuppressed=false;s.adventurer=true;Pawn p=null;
   try{
    // 1. 只在装了编辑器时启用，并且挂在 EditorUI.DoWindowContents 上
    bool ce=ModsConfig.IsActive(CharEditorCompat.PackageId);
    var m=CharEditorCompat.UiType==null?null:AccessTools.Method(CharEditorCompat.UiType,"DoWindowContents");
    bool patched=m!=null && Harmony.GetPatchInfo(m)?.Postfixes.Any(x=>x.owner=="local.lich.phylactery.chareditor")==true;
    Check(CharEditorCompat.Active==ce && patched==ce,"char editor bridge active only with Character Editor: loaded "+ce+", active "+CharEditorCompat.Active+", patched "+patched);
    p=AdvPawn(null);
    foreach(var tr in p.story.traits.allTraits.Where(tr=>HeroTraits.IsSlot(tr.def) || HeroTraits.IsDeed(tr.def) || Talents.Is(tr.def) || CombatGifts.Is(tr.def)).ToList())p.story.traits.RemoveTrait(tr);
    HeroTraits.Invalidate(p);CombatGifts.Invalidate(p);
    var h=Adventurer.Get(p);if(h.classes==null)h.classes=new ClassData();h.classes.levels.Clear();h.feats.Clear();h.featScore?.Clear();h.pendingASI=0;h.asiLeft=0;h.level=1;h.xp=0f;h.boon19=false;h.boonPoints=0;h.Dirty();
    // 2. 等级：升到 8 级发两次属性提升，经验取下限；降回 3 级不收回
    CharEditorCompat.SetLevel(h,8);int asi8=h.pendingASI;
    CharEditorCompat.SetLevel(h,3);
    Check(asi8==2 && h.level==3 && h.pendingASI==2 && h.xp==Legend.XpFor(3),"char editor level: to 8 gives ASI "+asi8+", back to 3 → level "+h.level+" ASI "+h.pendingASI+" xp "+h.xp);
    // 3. 六维：基础值直接设，超过上限保留但有效值按上限
    CharEditorCompat.SetScore(h,Adventurer.Str,30);
    Check(h.scores[0]==30 && h.Eff(0)==h.Cap(0),"char editor score: Str base "+h.scores[0]+", effective "+h.Eff(0)+" (cap "+h.Cap(0)+")");
    // 4. 职业：直接设 5 级给能力，降到 1 级收回高等级能力，0 级去掉
    var f=ClassSystem.Fighter;
    CharEditorCompat.SetClassLevel(h,f,5);int at5=f.abilities.Count(e=>p.abilities.GetAbility(e.ability)!=null);
    CharEditorCompat.SetClassLevel(h,f,1);int at1=f.abilities.Count(e=>p.abilities.GetAbility(e.ability)!=null);int want1=f.abilities.Count(e=>e.level<=1);
    Check(at5>at1 && at1==want1 && ClassSystem.Level(h,f)==1,"char editor class level: fighter 5 abilities "+at5+", at 1 → "+at1+" (want "+want1+")");
    // 5. 子职业：选了给能力，换成不选收回
    var sc=ClassSystem.All.First(c=>ClassSubclass.Options(c).Any(x=>x.abilities.Any(e=>e.level<=10)));CharEditorCompat.SetClassLevel(h,sc,10);var sub=ClassSubclass.Options(sc).First(x=>x.abilities.Any(e=>e.level<=10));
    bool subOk;
    {
     CharEditorCompat.SetSubclass(h,sc,sub);bool got=ClassSubclass.Picked(h,sc)==sub && sub.abilities.Where(e=>e.level<=10).All(e=>p.abilities.GetAbility(e.ability)!=null);
     CharEditorCompat.SetSubclass(h,sc,null);bool gone=ClassSubclass.Picked(h,sc)==null && sub.abilities.All(e=>p.abilities.GetAbility(e.ability)==null);
     subOk=got && gone;
    }
    CharEditorCompat.SetSubclass(h,sc,sub);CharEditorCompat.SetClassLevel(h,sc,0);
    Check(subOk && ClassSystem.Level(h,sc)==0 && ClassSubclass.Picked(h,sc)==null && sc.abilities.Concat(sub.abilities).All(e=>p.abilities.GetAbility(e.ability)==null),"char editor subclass "+sub?.defName+" pick / drop and class removal");
    // 6. 专长、恩赐
    var feat=DefDatabase<LP_FeatDef>.AllDefsListForReading.First(x=>!x.origin && x.grantAbility==null);
    CharEditorCompat.SetFeat(h,feat,true);bool fOn=h.HasFeat(feat);CharEditorCompat.SetFeat(h,feat,false);
    var boon=Epic.Boons.FirstOrDefault(b=>b!=Epic.Might);
    CharEditorCompat.SetBoon(h,boon,true);bool bOn=h.HasBoon(boon);CharEditorCompat.SetBoon(h,boon,false);
    Check(fOn && !h.HasFeat(feat) && bOn && !h.HasBoon(boon),"char editor feat "+feat.defName+" / boon "+boon?.defName+" toggle");
    // 7. 特性：武艺天赋、金色强力特性加上 / 去掉，缓存跟着刷新
    var gift=CombatGifts.All[CombatGifts.Mighty];var gold=HeroTraits.Gold.First();
    bool g1=CharEditorCompat.SetTrait(p,gift,0,true),h1=CharEditorCompat.SetTrait(p,gold,gold.degreeDatas[0].degree,true);
    int bonus=h.Eff(0)-h.Cap(0);bool mask=CombatGifts.HasGift(p,CombatGifts.Mighty);
    bool g0=CharEditorCompat.SetTrait(p,gift,0,false),h0=CharEditorCompat.SetTrait(p,gold,gold.degreeDatas[0].degree,false);
    Check(g1 && h1 && mask && bonus>0 && g0 && h0 && !CombatGifts.HasGift(p,CombatGifts.Mighty) && !HeroTraits.Has(p,gold),"char editor traits: gift "+g1+"/"+g0+" (Str bonus "+bonus+"), gold "+gold.defName+" "+h1+"/"+h0);
   }finally{
    s.adventurer=oOn;Adventurer.TestSuppressed=oSup;
    if(p!=null && !p.Destroyed)p.Destroy();
   }
  }
 }
}
