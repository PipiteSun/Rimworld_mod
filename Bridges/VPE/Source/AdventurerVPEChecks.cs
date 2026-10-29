using System;
using System.Linq;
using RimWorld;
using Verse;

namespace LichPhylactery.VPEBridge {
 // 1.48.0 冒险者等级 × VPE（Test.ps1 -VPE）：升级给路径点、没有灵能时攒点、主属性 13 解锁路径、职业强度公式。
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
    var w=Make();var aw=Adventurer.TryAdd(w,1,true);aw.scores=new[]{8,8,8,12,8,8};
    Check(!Wizard.SkillOk(w) && !Wizard.CanPawnUnlock(w),"[adventurer] wizard path locked at intellectual 0 and Int 12");
    aw.scores[Adventurer.Int]=13;
    Check(Wizard.SkillOk(w) && Wizard.CanPawnUnlock(w),"[adventurer] Int 13 unlocks the wizard path without the skill");
    aw.scores[Adventurer.Str]=8;aw.scores[Adventurer.Dex]=13;
    Check(Fighter.SkillOk(w) && !Cleric.SkillOk(w),"[adventurer] fighter takes the higher of Str/Dex (Dex 13); cleric still needs Wis 13");
    s.adventurer=false;
    Check(!Wizard.SkillOk(w),"[adventurer] master switch off → scores no longer unlock paths");
    s.adventurer=true;
    var z=Make(intellectual:10);var az=Adventurer.TryAdd(z,1,true);az.level=5;az.scores[Adventurer.Int]=16;az.Dirty();
    var arcane=Classes.Stat("LP_ArcanePower");float pw=z.GetStatValue(arcane);
    float cpb=Adventurer.ClassPowerPer*(2.3f-1f)+.02f*(3-2); // Int 16 = ×2.3
    Check(Near(pw,.5f+.5f+cpb) && Near(AdventurerVPE.PathBonus(z,Wizard),cpb),"[adventurer] class power = 0.5 + skill/20 + 0.1 × (Int 16 ×2.3 − 1) + 0.02 × (prof − 2) = "+pw);
    s.adventurerEffectPercent=0;float pw0=z.GetStatValue(arcane);s.adventurerEffectPercent=100;
    Check(Near(pw0,1.02f),"[adventurer] effect strength 0% keeps only the proficiency part ("+pw0+")");
    h.UnlockPath(Fighter);a.scores=new[]{12,16,13,10,10,8};int primary=Adventurer.PrimaryAbilityHook(p);
    Check(primary==Adventurer.Dex,"[adventurer] auto-assign primary ability follows the first class path (fighter → Dex 16 > Str 12)");
    // 1.48.1 专长：战争施法者的前置（DND 职业路径）、VEF 施法工作中受伤不打断；法术狙击 VPE 法术射程 ×1.5。
    var c=Make(social:10);var ac=Adventurer.TryAdd(c,1,true);ac.level=8;ac.pendingASI=2;
    Check(Feats.WarCaster.Worker.Unmet(c,ac)!=null && Feats.HasClassHook!=null,"[adventurer] war caster needs a DND class path or holy symbol");
    Classes.Psycasts(c).UnlockPath(Cleric);
    Check(Feats.WarCaster.Worker.Unmet(c,ac)==null && ac.TakeFeat(Feats.WarCaster) && ac.TakeFeat(Feats.SpellSniper),"[adventurer] with the cleric path war caster and spell sniper can be taken");
    var cure=Learn(c,"LP_CureWounds");ac.RemoveFeat(Feats.SpellSniper);float r0=cure.GetRangeForPawn();ac.AddFeat(Feats.SpellSniper);float r1=cure.GetRangeForPawn();
    Check(r0>0f && Near(r1,r0*1.5f),"[adventurer] spell sniper: VPE class ability range ×1.5 ("+r0+" → "+r1+")");
    cure.StartAbilityJob(new LocalTargetInfo(c));
    bool casting=Feats.Casting(c),shielded=Feats.CastShielded(c);ac.RemoveFeat(Feats.WarCaster);bool bare=Feats.CastShielded(c);
    Check(casting && shielded && !bare,"[adventurer] war caster: a VEF cast job ("+c.CurJobDef?.defName+") is protected from damage interruption only with the feat");
    var dinfo=new DamageInfo(DamageDefOf.Cut,1f);ac.AddFeat(Feats.WarCaster);var job=c.CurJob;c.jobs.Notify_DamageTaken(dinfo);
    Check(c.CurJob==job,"[adventurer] war caster: taking damage while casting keeps the cast job");
    c.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
    // 1.50.0 传奇等级：VPE 路径点每 2 级 1 点；职业决定传奇属性点的自动投点偏好。
    var L=Make();var aL=Adventurer.TryAdd(L,1,true);var hL=Classes.Psycasts(L);aL.level=20;aL.xp=Legend.XpFor(20);aL.legendary=true;int lp0=hL.points;
    Adventurer.GainXP(L,Legend.XpFor(24)-aL.xp);
    Check(aL.level==24 && hL.points==lp0+2 && aL.legendPoints+aL.legendSpent.Sum()==8,"[adventurer] legend levels 21–24: +2 VPE path points (one per 2 levels), 8 legend points ("+lp0+" → "+hL.points+")");
    hL.UnlockPath(Fighter);var mage=Make();Classes.Psycasts(mage).UnlockPath(Wizard);
    Check(Legend.RoleHook!=null && Legend.RoleHook(L)=="melee" && Legend.RoleHook(mage)=="caster" && Legend.Prefs(L).Contains(Legend.Melee) && Legend.Prefs(mage).Contains(Legend.Psychic),"[adventurer] legend auto-spend: fighter → melee/health, wizard → psychic/health");
   }finally{s.adventurer=oldOn;s.adventurerEffectPercent=oldFx;s.adventurerXpPercent=oldXp;Adventurer.TestSuppressed=true;}
  }
 }
}
