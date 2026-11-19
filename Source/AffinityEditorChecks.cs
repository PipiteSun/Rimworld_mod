using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.166.0 种族亲和编辑：Test.ps1 -Only AffinityEditor（加 -lichuitest 时截编辑窗口 AffEditor.png）。
 public partial class LichChecks {
  void CheckAffinityEditor(){
   var s=LichMod.Settings;var keep=new System.Collections.Generic.Dictionary<string,string>(s.affinityOverrides);bool oAff=s.adventurerAffinity,oSup=Adventurer.TestSuppressed,oOn=s.adventurer;
   Adventurer.TestSuppressed=false;s.adventurer=true;s.adventurerAffinity=true;
   try{
    s.affinityOverrides.Clear();Affinity.InvalidateOverrides();
    var nx=DefDatabase<XenotypeDef>.GetNamed("Neanderthal");var p=AdvPawn(nx);
    var def0=Affinity.For(p);
    // 1. 覆盖：尼安德特人改成 智擅长、力笨拙、传奇学习、传奇种族
    var a=new AdventurerAffinity{key="Neanderthal"};a.strong[Adventurer.Int]=true;a.weak[Adventurer.Str]=true;a.legend[Legend.Learn]=true;a.legendary=true;
    s.affinityOverrides["Neanderthal"]=AffinityOverrides.Encode(a);Affinity.InvalidateOverrides();
    var o=Affinity.For(p);var o2=Affinity.For(p);
    Check(def0.key=="Neanderthal" && def0.strong[Adventurer.Str] && o.strong[Adventurer.Int] && o.weak[Adventurer.Str] && !o.strong[Adventurer.Str] && o.legend[Legend.Learn] && o.legendary && o==o2 && o.label==nx.LabelCap.ToString() && AffinityOverrides.Encode(o)==s.affinityOverrides["Neanderthal"],
     "override replaces the neanderthal entry (Int strong / Str weak / legend learn / legendary, label '"+o.label+"', same object on repeat lookups); code "+s.affinityOverrides["Neanderthal"]);
    // 2. 覆盖也压过 def 上的扩展（Pact 种族 LP_Satyr）和人类兜底；人类兜底保留 ×1.1
    var sat=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_Satyr");bool satOk=true;
    if(sat!=null){var ps=AdvPawn(sat);var b=new AdventurerAffinity{key="LP_Satyr"};b.strong[Adventurer.Wis]=true;s.affinityOverrides["LP_Satyr"]=AffinityOverrides.Encode(b);Affinity.InvalidateOverrides();var r=Affinity.For(ps);satOk=r.strong[Adventurer.Wis] && !r.strong[Adventurer.Dex];}
    var bl=AdvPawn(XenotypeDefOf.Baseliner);var hu=new AdventurerAffinity{key=Affinity.HumanKey};hu.strong[Adventurer.Cha]=true;
    s.affinityOverrides[Affinity.HumanKey]=AffinityOverrides.Encode(hu);Affinity.InvalidateOverrides();var rh=Affinity.For(bl);
    Check(satOk && rh.strong[Adventurer.Cha] && Math.Abs(rh.legendAll-1.1f)<.001f && rh.key==Affinity.HumanKey,"override beats the def extension (satyr → Wis only) and the human fallback (Cha strong, legend ×1.1 kept)");
    // 3. 冒险者刷新：hediff 上缓存的亲和换成覆盖后的；恢复默认后回到内置
    var h=Adventurer.Get(p);AccessTools.Method(typeof(Hediff_Adventurer),"RefreshAffinity").Invoke(h,null);
    bool refreshed=h.Affinity==o || h.Affinity.strong[Adventurer.Int];
    s.affinityOverrides.Remove("Neanderthal");Affinity.InvalidateOverrides();var back=Affinity.For(p);
    Check(refreshed && back.strong[Adventurer.Str] && back.weak[Adventurer.Int] && !back.legendary,"adventurer picks up the override; removing it (默认) restores the built-in neanderthal entry");
    // 4. 编辑器列表：包含人类、巫妖、原版异种、LP 异种和人形种族，不含基准人；窗口能建
    var keys=Dialog_AffinityEditor.Keys();
    Check(keys.Contains(Affinity.HumanKey) && keys.Contains(Affinity.LichKey) && keys.Contains("Neanderthal") && keys.Contains("LP_Elf") && !keys.Contains("Baseliner") && keys.Count>=60,"editor lists "+keys.Count+" races (human / lich / vanilla / LP), baseliner folded into human");
    // 5. 1.175.0 自设异种（没有 XenotypeDef）：按名字查覆盖；没配过按人类；列进编辑器；去掉覆盖后回到人类
    var cp=AdvPawn();cp.genes.xenotypeName="测试巨种";
    string ck=Affinity.CustomKey("测试巨种");var before=Affinity.For(cp);
    var ca=new AdventurerAffinity{key=ck};ca.strong[Adventurer.Con]=true;ca.weak[Adventurer.Dex]=true;ca.legend[Legend.Health]=true;ca.legendary=true;
    s.affinityOverrides[ck]=AffinityOverrides.Encode(ca);Affinity.InvalidateOverrides();
    var got=Affinity.For(cp);var other=AdvPawn();other.genes.xenotypeName="别的名字";var notMine=Affinity.For(other);bool listed=Dialog_AffinityEditor.Keys().Contains(ck);
    s.affinityOverrides.Remove(ck);Affinity.InvalidateOverrides();var after=Affinity.For(cp);
    Check(Affinity.CustomNameOf(cp)=="测试巨种" && before.key==Affinity.HumanKey && got.key==ck && got.label=="测试巨种" && got.strong[Adventurer.Con] && got.weak[Adventurer.Dex] && got.legend[Legend.Health] && got.legendary && notMine.key==Affinity.HumanKey && listed && after.key==Affinity.HumanKey,
     "1.175.0 custom xenotype '测试巨种' (no XenotypeDef): human before, uses its own override by name (Con strong / Dex weak / legendary), another custom name stays human, row listed in the editor ("+listed+"), back to human after reset");
   }finally{s.affinityOverrides.Clear();foreach(var kv in keep)s.affinityOverrides[kv.Key]=kv.Value;Affinity.InvalidateOverrides();s.adventurerAffinity=oAff;Adventurer.TestSuppressed=oSup;s.adventurer=oOn;foreach(var x in advMade)if(!x.Destroyed)x.Destroy();advMade.Clear();}
  }
  static int affFrames;
  static bool CaptureAffinityEditor(){
   if(affFrames==0)return false;affFrames++;string dir=GenFilePaths.SaveDataFolderPath;
   try{
    if(affFrames==10){var a=new AdventurerAffinity{key="Neanderthal"};a.strong[Adventurer.Int]=true;a.legendary=true;LichMod.Settings.affinityOverrides["Neanderthal"]=AffinityOverrides.Encode(a);Affinity.InvalidateOverrides();Messages.Clear();Find.WindowStack.Add(new Dialog_AffinityEditor());}
    if(affFrames==40)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir,"AffEditor.png"));
    if(affFrames>=50){LichMod.Settings.affinityOverrides.Remove("Neanderthal");Affinity.InvalidateOverrides();Log.Message("[LICH-UI] affinity editor captured");Application.Quit();}
   }catch(Exception e){Log.Error("[LICH-UI] affinity editor capture: "+e);Application.Quit();}
   return true;
  }
 }
}
