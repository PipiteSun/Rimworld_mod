using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.185.0 第七批子职业 10 个：Test.ps1 -Only Subclass7（定义 + 每个子职业的核心效果）。
 public partial class LichChecks {
  Pawn S7Pawn(string cls,int lvl,string sub,int dx,int dz){var p=S6Pawn(cls,lvl,sub,dx,dz);p.equipment?.DestroyAllEquipment();Subclass7.Tick250(Adventurer.Get(p));return p;}
  static bool S7In(Pawn v,Pawn a,out float f){f=Subclass7.Incoming(v,a,S6Melee(a),out bool dodge);return dodge;}
  void S7Rage(Pawn p){ClassEffects.Apply(p,Ab("LP_Rage"),Fx("LP_Rage"),new LocalTargetInfo(p));}

  void CheckSubclass7(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool on=s.adventurer;s.adventurer=true;
   Rand.PushState(118501);
   try{CheckSubclass7Inner();}
   finally{Rand.PopState();Adventurer.TestSuppressed=sup;s.adventurer=on;Subclass7.TestRoll=null;Subclass6.TestRoll=null;MlClean();Subclass7.Reset();Subclass6.Reset();}
  }
  void CheckSubclass7Inner(){
   LP_ClassDef C(string n)=>DefDatabase<LP_ClassDef>.GetNamed("LP_Class_"+n);
   // 0. 定义：10 个子职业挂对父职业、能力等级照各职业的节奏、徽记与图标都在、不写 d20；现在每个职业至少 5 个子职业。
   {var subs=Subclass7.Names.Select(n=>DefDatabase<LP_SubclassDef>.GetNamedSilentFail(n)).ToList();
    var levels=new Dictionary<string,int[]>{{"Monk",new[]{3,6,11,17}},{"Barbarian",new[]{3,7,11,15}},{"Ranger",new[]{3,7,11,15}},{"Druid",new[]{2,6,10,14}},{"Bard",new[]{3,7,11,15}},{"Gunslinger",new[]{3,7,11,15}},{"Psion",new[]{3,6,10,14}}};
    bool ok=subs.All(x=>x!=null && x.parent!=null && x.Emblem!=null && x.Emblem!=x.parent.Background && x.abilities.Count>=4 && x.abilities.All(e=>e.ability!=null && e.ability.uiIcon!=BaseContent.BadTex && e.ability.abilityClass==typeof(Ability_Class) && levels[x.parent.defName.Substring(9)].Contains(e.level)) && ClassSystem.ClassOf(x.abilities[0].ability)==x.parent);
    bool words=!subs.Where(x=>x!=null).SelectMany(x=>x.abilities.Select(e=>e.ability.description).Append(x.description)).Any(t=>t.Contains("d20") || t.Contains("掷骰") || t.Contains("检定"));
    bool names=!subs.Where(x=>x!=null).SelectMany(x=>x.abilities.Select(e=>e.ability.defName).Append(x.defName)).Any(n=>char.IsDigit(n[n.Length-1]));
    var classes=DefDatabase<LP_ClassDef>.AllDefsListForReading;int least=classes.Min(c=>ClassSubclass.Options(c).Count());
    int monk=subs.Count(x=>x.parent==C("Monk")),barb=subs.Count(x=>x.parent==C("Barbarian")),rang=subs.Count(x=>x.parent==C("Ranger"));
    Check(ok && words && names && subs.Count==10 && monk==2 && barb==2 && rang==2 && subs.Count(x=>x.parent==C("Druid"))==1 && subs.Count(x=>x.parent==C("Bard"))==1 && subs.Count(x=>x.parent==C("Gunslinger"))==1 && subs.Count(x=>x.parent==C("Psion"))==1 && classes.Count==14 && least>=5,
     "[1.185.0] 10 subclasses (2 monk, 2 barbarian, 2 ranger, druid, bard, gunslinger, psion) with emblems, icons, class-paced levels and no dice words; every one of "+classes.Count+" classes now has at least "+least+" subclasses");}
   // 1. 醉拳宗：两个被动状态；借力打力 25% 卸掉近战并有 3 秒间隔；醉拳乱舞打到相邻的敌人。
   {var p=S7Pawn("LP_Class_Monk",17,Subclass7.Drunken,-10,2);var foe=S6Foe(p,-9,2);
    bool buffs=Items.Has(p,"LP_DK_SwayBuff") && Items.Has(p,"LP_DK_NumbBuff");
    Subclass7.TestRoll=.9f;bool miss=S7In(p,foe,out float f0);Subclass7.TestRoll=.1f;bool first=S7In(p,foe,out float f1);bool again=S7In(p,foe,out float f2);Subclass7.TestRoll=null;
    float h0=foe.health.summaryHealth.SummaryHealthPercent;int n=Subclass7.Frenzy(p,1.9f);float h1=foe.Dead?0f:foe.health.summaryHealth.SummaryHealthPercent;
    Check(buffs && !miss && Near(f0,1f) && first && !again && n==1 && h1<=h0,"[1.185.0] drunken master: sway + numbness hediffs, redirect attack avoids a melee hit at a 10% roll but not twice within 3 seconds, intoxicated frenzy strikes "+n+" adjacent foe");}
   // 2. 剑圣宗：空手没有剑圣之道，拿剑才有；人剑合一 ×1.1、穿透 +0.2；无误精准 15% ×2；磨砺剑锋。
   {var p=S7Pawn("LP_Class_Monk",17,Subclass7.Kensei,-6,2);var foe=S6Foe(p,-5,2);
    bool bare=!Items.Has(p,"LP_KS_PathBuff");
    p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword"),ThingDefOf.Steel));Subclass7.Tick250(Adventurer.Get(p));bool armed=Items.Has(p,"LP_KS_PathBuff");
    var d=S6Melee(p);Subclass7.TestRoll=.9f;float plain=Subclass7.Outgoing(p,foe,ref d);float pen=d.ArmorPenetrationInt;var d2=S6Melee(p);Subclass7.TestRoll=.1f;float crit=Subclass7.Outgoing(p,foe,ref d2);Subclass7.TestRoll=null;
    bool sharp=S2Cast(p,"LP_KS_Sharpen",p) && Items.Has(p,"LP_KS_Sharpened");
    Check(bare && armed && Near(plain,1.1f) && Near(pen,.2f) && Near(crit,2.2f) && sharp,"[1.185.0] kensei: path of the kensei only with a melee weapon, one with the blade ×"+plain.ToString("0.00")+" pen +"+pen.ToString("0.0")+", unerring accuracy ×2 at a 10% roll, sharpen the blade buff");}
   // 3. 狂热者之道：狂热专注；神圣狂怒只在狂暴中 ×1.15；死战不休狂暴中 ×0.85；狂热威仪给友军状态。
   {var p=S7Pawn("LP_Class_Barbarian",15,Subclass7.Zealot,-2,2);var foe=S6Foe(p,-1,2);var ally=MlPawn(Faction.OfPlayer,-2,3);
    var d=S6Melee(p);float calm=Subclass7.Outgoing(p,foe,ref d);S7In(p,foe,out float calmIn);
    S7Rage(p);bool raging=ClassMartial2.Raging(p);var d2=S6Melee(p);float fury=Subclass7.Outgoing(p,foe,ref d2);S7In(p,foe,out float rageIn);
    bool cry=S2Cast(p,"LP_ZL_Presence",p) && Items.Has(p,"LP_ZL_Zeal") && Items.Has(ally,"LP_ZL_Zeal");
    Check(Items.Has(p,"LP_ZL_FocusBuff") && Near(calm,1f) && Near(calmIn,1f) && raging && Near(fury,1.15f) && Near(rageIn,.85f) && cry,"[1.185.0] zealot: fanatical focus, divine fury ×"+fury.ToString("0.00")+" and rage beyond death ×"+rageIn.ToString("0.00")+" only while raging, zealous presence buffs an ally");}
   // 4. 先祖守卫之道：狂暴中近战标记敌人；被标记者打别人 ×0.75，灵魂护盾再 ×0.85；打野蛮人自己不减。
   {var p=S7Pawn("LP_Class_Barbarian",15,Subclass7.Ancestral,2,2);var foe=S6Foe(p,3,2);var ally=MlPawn(Faction.OfPlayer,2,3);
    S7In(ally,foe,out float before);S7Rage(p);var d=S6Melee(p);Subclass7.Outgoing(p,foe,ref d);
    bool haunted=foe.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_AG_Haunted")) is Hediff_ClassMark m && m.caster==p;
    S7In(ally,foe,out float guarded);S7In(p,foe,out float self);
    Check(Items.Has(p,"LP_AG_ConsultBuff") && Near(before,1f) && haunted && Near(guarded,.75f*.85f) && Near(self,1f),"[1.185.0] ancestral guardian: consult the spirits, raging melee hit haunts the foe, haunted foe ×0.75 and spirit shield ×0.85 on an ally (×"+guarded.ToString("0.00")+"), nothing against the barbarian");}
   // 5. 妖精漫游者：迷人之姿；恐惧打击 ×1.1；惑心之雾震慑身边敌人；迷雾漫步瞬移。
   {var p=S7Pawn("LP_Class_Ranger",15,Subclass7.FeyWanderer,6,2);var foe=S6Foe(p,7,2);
    var d=S6Melee(p);float f=Subclass7.Outgoing(p,foe,ref d);int n=Subclass7.Mist(p,4f,600);bool shaken=Items.Has(foe,"LP_Shaken");
    var to=MlCell(6,4);string bad=Subclass7.Invalid(p,Fx("LP_WN_MistyStep"),new LocalTargetInfo(foe.Position));bool step=S2Cast(p,"LP_WN_MistyStep",new LocalTargetInfo(to)) && p.Position==to;
    Check(Items.Has(p,"LP_WN_BeguilingBuff") && Near(f,1.1f) && n>=1 && shaken && bad!=null && step,"[1.185.0] fey wanderer: beguiling presence, dreadful strikes ×1.1, beguiling mist shakes "+n+" foe, misty wanderer teleports to an empty cell only");}
   // 6. 地平线行者：位面战士 5 秒一次追加伤害；以太步；远距打击瞬移后武器 ×1.2；幽灵防御每 5 秒减半一次。
   {var p=S7Pawn("LP_Class_Ranger",15,Subclass7.Horizon,10,2);var foe=S6Foe(p,11,2);
    var d=S6Melee(p);float f0=Subclass7.Outgoing(p,foe,ref d);bool timer=ClassSubclass.Data(p).ticks.ContainsKey("hwT");
    bool eth=S2Cast(p,"LP_HW_EtherealStep",p) && Items.Has(p,"LP_HW_Ethereal");
    var to=MlCell(10,4);bool jump=S2Cast(p,"LP_HW_DistantStrike",new LocalTargetInfo(to)) && p.Position==to && Items.Has(p,"LP_HW_Momentum");var d2=S6Melee(p);float f1=Subclass7.Outgoing(p,foe,ref d2);
    S7In(p,foe,out float half);S7In(p,foe,out float full);
    Check(Near(f0,1f) && timer && eth && jump && Near(f1,1.2f) && Near(half,.5f) && Near(full,1f),"[1.185.0] horizon walker: planar warrior strike timer, ethereal step, distant strike teleports and gives ×"+f1.ToString("0.0")+", spectral defense halves one hit per 5 seconds");}
   // 7. 野火结社：野火之灵 ×1.1；炽焰之种伤到敌人；治愈余烬杀敌回血 6 + 等级；浴火重生每天一次。
   {var p=S7Pawn("LP_Class_Druid",14,Subclass7.Wildfire,-10,6);var foe=S6Foe(p,-9,6);
    float pf=Subclass7.PowerFactor(p,Fx("LP_WF_Seed"));float h0=foe.health.summaryHealth.SummaryHealthPercent;bool seed=S2Cast(p,"LP_WF_Seed",foe);float h1=foe.Dead?0f:foe.health.summaryHealth.SummaryHealthPercent;
    foreach(var fire in map.listerThings.ThingsOfDef(ThingDefOf.Fire).ToList())fire.Destroy();
    S6Hurt(p,40f);float i0=ClassMelee.InjurySum(p);Subclass7.Killed(foe);float i1=ClassMelee.InjurySum(p);
    bool first=Subclass7.TryStand(p);float i2=ClassMelee.InjurySum(p);bool second=Subclass7.TryStand(p);
    Check(Near(pf,1.1f) && seed && h1<h0 && Near(i0-i1,20f) && first && !second && i2<i1,"[1.185.0] wildfire: wildfire spirit ×"+pf.ToString("0.0")+", fiery seed burns the foe, cauterizing flames heal 6 + level ("+(i0-i1).ToString("0")+"), blazing revival once a day ("+i1.ToString("0.0")+" → "+i2.ToString("0.0")+")");}
   // 8. 迷惑学院：迷魂演出；激励披风给自己和友军护盾 (6 + 等级) × 强度；威仪披风震慑敌人；不破威严；魔法奥秘两个分支照抄。
   {var p=S7Pawn("LP_Class_Bard",15,Subclass7.Glamour,-6,6);var ally=MlPawn(Faction.OfPlayer,-6,7);var foe=S6Foe(p,-5,6);
    float want=Subclass7.MantleAmount(p,ClassEffects.Strength(p,Fx("LP_GL_Mantle")));bool cast=S2Cast(p,"LP_GL_Mantle",p);var sh=ally.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ACB_Shield"));
    bool maj=S2Cast(p,"LP_GL_Majesty",foe) && Items.Has(foe,"LP_Shaken");bool unb=S2Cast(p,"LP_GL_Unbreakable",p) && Items.Has(p,"LP_GL_UnbreakBuff");
    int choices=DefDatabase<LP_SubclassDef>.GetNamed(Subclass7.Glamour).choices.Count;
    Check(Items.Has(p,"LP_GL_EnthrallBuff") && cast && sh!=null && want>=21f*.5f && Math.Abs(sh.Severity-want)<.5f && maj && unb && choices==2,"[1.185.0] glamour: enthralling performance, mantle of inspiration shields an ally for "+want.ToString("0")+", mantle of majesty shakes the foe, unbreakable majesty, "+choices+" magical secrets");}
   // 9. 赏金猎人：追踪好手；悬赏标记后自己打它 ×1.4（15 级生死不论），别人不加；缚足；赏金到手回 1 点胆识。
   {var p=S7Pawn("LP_Class_Gunslinger",15,Subclass7.Bounty,-2,6);var foe=S6Foe(p,-1,6);var other=S7Pawn("LP_Class_Gunslinger",3,Subclass7.Bounty,-2,7);var h=Adventurer.Get(p);var gs=C("Gunslinger");
    bool mark=S2Cast(p,"LP_BH_Mark",foe) && foe.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_BH_Marked")) is Hediff_ClassMark m && m.caster==p;
    var d=S6Melee(p);float mine=Subclass7.Outgoing(p,foe,ref d);var d2=S6Melee(other);float theirs=Subclass7.Outgoing(other,foe,ref d2);
    bool bola=S2Cast(p,"LP_BH_Bola",foe) && Items.Has(foe,"LP_BH_Hobbled");
    ClassSystem.AddResource(h,gs,-2f);float g0=ClassSystem.Resource(h,gs);Subclass7.Killed(foe);float g1=ClassSystem.Resource(h,gs);
    Check(Items.Has(p,"LP_BH_TrackerBuff") && mark && Near(mine,1.4f) && Near(theirs,1f) && bola && g1>g0,"[1.185.0] bounty hunter: expert tracker, bounty mark ×"+mine.ToString("0.0")+" for the marker only, hobbling shot, payday returns grit ("+g0.ToString("0.#")+" → "+g1.ToString("0.#")+")");}
   // 10. 预知者：两个被动；危机预感花 1 点灵能点；洞悉破绽；先知先觉每 10 秒躲一次；命运改写治疗；常备法术；修行分支照抄。
   {var p=S7Pawn("LP_Class_Psion",14,Subclass7.Seer,2,6);var ally=MlPawn(Faction.OfPlayer,2,7);var foe=S6Foe(p,3,6);var h=Adventurer.Get(p);
    bool buffs=Items.Has(p,"LP_SR_ForesightBuff") && Items.Has(p,"LP_SR_EyeBuff");
    Check(HasAb(p,"LP_SR_Premonition"),"[1.185.0] seer setup: psion level "+Psion.Level(h)+", picked "+ClassSubclass.Has(h,Subclass7.Seer)+", pending "+ClassSubclass.Pending(h)+", abilities "+string.Join(",",p.abilities.abilities.Select(x=>x.def.defName).Where(x=>x.StartsWith("LP_SR")||x.StartsWith("LP_PS"))));
    int p0=Psion.Points(p);bool warn=S2Cast(p,"LP_SR_Premonition",ally) && Items.Has(ally,"LP_SR_Warned");int p1=Psion.Points(p);
    bool exp=S2Cast(p,"LP_SR_Expose",foe) && Items.Has(foe,"LP_SR_Exposed");
    bool dodge=S7In(p,foe,out float fa);bool again=S7In(p,foe,out float fb);
    S6Hurt(ally,20f);float i0=ClassMelee.InjurySum(ally);bool rew=S2Cast(p,"LP_SR_Rewind",p);float i1=ClassMelee.InjurySum(ally);int p2=Psion.Points(p);
    var list=DefDatabase<CasterListDef>.AllDefsListForReading.FirstOrDefault(l=>l.classDef==Psion.Class);bool spell=list!=null && Psion.SubSpell(h,list,Ab("LP_ShieldSpell")) && Psion.SubSpell(h,list,Ab("LP_WS_Scrying"));
    int choices=DefDatabase<LP_SubclassDef>.GetNamed(Subclass7.Seer).choices.Count;
    Check(buffs && warn && p1==p0-1 && exp && dodge && !again && rew && i1<i0 && p2==p1-5 && spell && choices==6,"[1.185.0] seer: foresight + third eye, premonition costs 1 psi point ("+p0+" → "+p1+"), expose weakness, precognition dodges once per 10 seconds, rewrite fate heals an ally ("+i0.ToString("0.0")+" → "+i1.ToString("0.0")+", points "+p2+"), always-prepared spells, "+choices+" disciplines");}
   // 11. 不会误伤：没有这些子职业的人各入口都不起作用。
   {var plain=MlPawn(Faction.OfPlayer,10,11);var foe=S6Foe(plain,11,11);var d=S6Melee(plain);Subclass7.Users.Remove(plain);
    bool dodge=S7In(plain,foe,out float f);
    Check(!Subclass7.Users.Contains(plain) && !dodge && Near(f,1f) && Subclass7.PowerFactor(plain,Fx("LP_WF_Seed"))==1f && !Subclass7.TryStand(plain) && !Subclass7.Knows(plain,"LP_DK_Sway"),"[1.185.0] no subclass → no effect");}
  }
 }
}
