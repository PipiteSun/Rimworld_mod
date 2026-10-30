using System;
using System.Linq;
using RimWorld;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;

namespace LichPhylactery.VPEBridge {
 // 1.48.0 冒险者等级 × VPE（Test.ps1 -VPE -Only Adventurer）：升级给 VPE 路径点、没有灵能时攒点、传奇等级每 2 级 1 点、法术狙击、战争施法者前置。
 // 1.73.0：DND 职业都在主程序集，路径解锁 / 主属性 / 职业强度的检查搬到主程序集 -Only Classes；法术狙击的期望改成 ×1.75（1.67.0 起的数值）。
 public static partial class BridgeChecks {
  static void AdventurerChecks(){
   var s=LichMod.Settings;bool oldOn=s.adventurer;int oldFx=s.adventurerEffectPercent,oldXp=s.adventurerXpPercent;
   Adventurer.TestSuppressed=false;s.adventurer=true;s.adventurerEffectPercent=100;s.adventurerXpPercent=100;
   try{
    Check(Adventurer.VPELoaded && Adventurer.PrimaryAbilityHook!=null,"[adventurer] VPE bridge registers the level-up and primary-ability hooks");
    var p=Make();var a=Adventurer.TryAdd(p,1,true);var h=Classes.Psycasts(p);int pts=h.points;
    Adventurer.GainXP(p,300f);
    Check(a.level==3 && h.points==pts+2 && a.vpeGranted==2 && a.vpeBanked==0,"[adventurer] each level gives +1 VPE path point ("+pts+" → "+h.points+")");
    var q=Make(psylink:false);var aq=Adventurer.TryAdd(q,1,true);Adventurer.GainXP(q,100f);
    Check(Classes.Psycasts(q)==null && aq.level==2 && aq.vpeBanked==1,"[adventurer] without VPE psycasts the point is banked");
    q.ChangePsylinkLevel(1,false);var hq=Classes.Psycasts(q);
    Check(hq!=null && aq.vpeBanked==0 && aq.vpeGranted==1 && hq.points==pts+1,"[adventurer] banked points are paid when the first psylink arrives ("+hq?.points+")");
    // 战争施法者 / 法术狙击：有冒险者职业就能选；法术狙击让 VPE 灵能射程 ×1.75。
    var c=Make(social:10);var ac=Adventurer.TryAdd(c,1,true);ac.level=8;ac.pendingASI=2;ac.scores=new[]{10,10,10,10,10,10};ac.Dirty();
    Check(Feats.WarCaster.Worker.Unmet(c,ac)!=null,"[adventurer] war caster needs a DND class or holy symbol");
    ClassSystem.SetLevel(ac,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Cleric"),1);
    Check(Feats.WarCaster.Worker.Unmet(c,ac)==null && ac.TakeFeat(Feats.WarCaster) && ac.TakeFeat(Feats.SpellSniper),"[adventurer] with the cleric class war caster and spell sniper can be taken");
    var psy=DefDatabase<AbilityDef>.AllDefsListForReading.FirstOrDefault(d=>d.Psycast()!=null && !ClassMigration.Hidden(d) && d.range>3f);
    var cast=Learn(c,psy.defName);ac.RemoveFeat(Feats.SpellSniper);float r0=cast.GetRangeForPawn();ac.AddFeat(Feats.SpellSniper);float r1=cast.GetRangeForPawn();
    Check(r0>0f && Near(r1,r0*Feats.SpellSniperRange),"[adventurer] spell sniper: VPE psycast range ×"+Feats.SpellSniperRange+" ("+psy.defName+": "+r0+" → "+r1+")");
    // 传奇等级：VPE 路径点每 2 级 1 点。
    var L=Make();var aL=Adventurer.TryAdd(L,1,true);var hL=Classes.Psycasts(L);aL.level=20;aL.xp=Legend.XpFor(20);aL.legendary=true;int lp0=hL.points;
    Adventurer.GainXP(L,Legend.XpFor(24)-aL.xp);
    Check(aL.level==24 && hL.points==lp0+2 && aL.legendPoints+aL.legendSpent.Sum()==8,"[adventurer] legend levels 21–24: +2 VPE path points (one per 2 levels), 8 legend points ("+lp0+" → "+hL.points+")");
   }finally{s.adventurer=oldOn;s.adventurerEffectPercent=oldFx;s.adventurerXpPercent=oldXp;Adventurer.TestSuppressed=true;}
  }
 }
}
