using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.135.0 术士子职业补完：Test.ps1 -Only Subclass5（每个子职业 3 级选择、核心效果、终极）。
 public partial class LichChecks {
  Pawn S5Pawn(int lvl,string sub,int dx,int dz){
   var p=S2Pawn("LP_Class_Sorcerer",lvl,sub);if(!p.Spawned)GenSpawn.Spawn(p,MlCell(dx,dz),map);mlMade.Add(p);
   foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(inj);
   Subclass5.Tick250(Adventurer.Get(p));return p;
  }
  void S5Hurt(Pawn p,float amount){p.TakeDamage(new DamageInfo(DamageDefOf.Cut,amount,0f,-1f,null,p.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso)));}

  void CheckSubclass5(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool on=s.adventurer;s.adventurer=true;
   Rand.PushState(113501);
   try{CheckSubclass5Inner();}
   finally{Rand.PopState();Adventurer.TestSuppressed=sup;s.adventurer=on;Subclass5.TestDark=null;Subclass5.TestRoll=null;Legendary.TestD20=null;MlClean();Subclass5.Reset();}
  }
  void CheckSubclass5Inner(){
   var sor=Subclass5.Sorcerer;
   // 0. 定义：三个子职业挂在术士下、3 / 7 / 11 / 15 级能力、徽记和图标都在；神圣之魂有「神恩亲和」三选一。
   {var subs=new[]{Subclass5.DivineSoul,Subclass5.ShadowMagic,Subclass5.Clockwork}.Select(n=>DefDatabase<LP_SubclassDef>.GetNamedSilentFail(n)).ToList();
    bool ok=subs.All(x=>x!=null && x.parent==sor && x.Emblem!=null && x.Emblem!=sor.Background && x.abilities.All(e=>e.ability!=null && e.ability.uiIcon!=BaseContent.BadTex && new[]{3,7,11,15}.Contains(e.level)) && x.abilities.Any(e=>e.level==15));
    var ch=subs[0]?.choices.FirstOrDefault();bool words=!subs.SelectMany(x=>x.abilities.Select(e=>e.ability.description).Append(x.description)).Any(t=>t.Contains("d20") || t.Contains("掷骰") || t.Contains("检定"));
    Check(ok && ch!=null && ch.key=="dsAffinity" && ch.options.Count==3 && ch.level==3 && words && ClassSubclass.Options(sor).Count()>=6,"[1.135.0] three new sorcerer subclasses (divine soul, shadow magic, clockwork soul) with 3/7/11/15 abilities, emblems, icons and the affinity choice");}

   // ===== 神圣之魂 =====
   // 1. 3 级选择：得到治疗伤口和神恩庇佑，「神恩亲和」待选；选光明后治疗 ×1.25。
   {var p=S2Pawn("LP_Class_Sorcerer",3,null);GenSpawn.Spawn(p,MlCell(-8,-8),map);mlMade.Add(p);var h=Adventurer.Get(p);
    bool need=ClassSubclass.NeedsPick(h,sor);bool picked=ClassSubclass.Pick(h,DefDatabase<LP_SubclassDef>.GetNamed(Subclass5.DivineSoul));ClassSystem.Sync(h);Subclass5.Tick250(h);
    bool abil=p.abilities.GetAbility(Ab("LP_DS_CureWounds"))!=null && p.abilities.GetAbility(Ab("LP_DS_Favored"))!=null;bool pend=ClassSubclass.PendingChoices(h).Any(x=>x.ch.key=="dsAffinity");
    var e=Ab("LP_DS_CureWounds").comps.OfType<CompProperties_ClassEffect>().First();float base0=ClassEffects.Power(p,e);ClassSubclass.Choose(h,"dsAffinity",0);float light=ClassEffects.Power(p,e);
    Check(need && picked && abil && pend && Near(light/base0,1.25f),"[1.135.0] divine soul at sorcerer 3: cure wounds + favored, affinity pending; light affinity heals ×1.25 ("+base0.ToString("0.0")+" → "+light.ToString("0.0")+")");}
   // 2. 核心：守护亲和施放术士法术后得到 8 点护盾；神恩共鸣治疗别人时自己恢复 25%。
   {var p=S5Pawn(7,Subclass5.DivineSoul,-6,-8);var h=Adventurer.Get(p);ClassSubclass.Choose(h,"dsAffinity",2);var ally=MlPawn(Faction.OfPlayer,-5,-7);S5Hurt(ally,12f);S5Hurt(p,12f);
    float selfHp0=ClassMelee.InjurySum(p);bool cast=S2Cast(p,"LP_DS_CureWounds",ally);var sh=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ACB_Shield"));float selfHp1=ClassMelee.InjurySum(p);
    Check(cast && sh!=null && Near(sh.Severity,8f) && selfHp1<selfHp0,"[1.135.0] divine soul: guardian affinity gives an 8-point shield after a sorcerer spell; resonance heals the caster too ("+selfHp0.ToString("0.0")+" → "+selfHp1.ToString("0.0")+")");}
   // 3. 终极：神迹治好殖民者一半伤口并给心情；被动每天第一次倒地自愈站起（第二次不再）。
   {var p=S5Pawn(15,Subclass5.DivineSoul,-4,-8);var c=MlPawn(Faction.OfPlayer,-3,-8);S5Hurt(c,14f);float i0=ClassMelee.InjurySum(c);int n=Subclass5.Miracle(p);float i1=ClassMelee.InjurySum(c);
    bool thought=c.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_DS_MiracleThought"))!=null;
    S5Hurt(p,10f);bool first=Subclass5.TryStand(p);bool second=Subclass5.TryStand(p);
    Check(n>=2 && i1<=i0*.55f && thought && first && !second,"[1.135.0] miracle: colonists heal half ("+i0.ToString("0.0")+" → "+i1.ToString("0.0")+"), +10 mood; the first fall each day is healed, the second isn't");}

   // ===== 暗影魔法 =====
   // 4. 3 级选择：暗视之眼被动；黑暗里术士法术威力 ×1.2，亮处不加；黑暗不扣工作和瞄准。
   {var p=S5Pawn(3,Subclass5.ShadowMagic,0,-8);var e=Ab("LP_AM_PsychicSpike").comps.OfType<CompProperties_ClassEffect>().First();
    Subclass5.TestDark=false;float lit=Subclass5.PowerFactor(p,e);Subclass5.TestDark=true;float dark=Subclass5.PowerFactor(p,e);
    Check(p.abilities.GetAbility(Ab("LP_SM_EyesDark"))!=null && Near(lit,1f) && Near(dark,1.2f),"[1.135.0] shadow magic at 3: eyes of the dark; sorcerer spells ×1.2 in darkness ("+lit+" / "+dark+")");
    Subclass5.TestDark=null;}
   // 5. 核心：影犬（召唤、猎物无法隐形）、暗影步在黑暗之间瞬移并让下一个法术 +20%。
   {var p=S5Pawn(11,Subclass5.ShadowMagic,2,-8);var foe=MlPawn(MlEnemy,6,-8);var w=Subclass5.Hound(p,foe,2500);
    Subclass5.TestDark=true;var dest=MlCell(2,-2);string why=Subclass5.Invalid(p,Ab("LP_SM_ShadowStep").comps.OfType<CompProperties_ClassEffect>().First(),dest);bool stepped=why==null && Subclass5.ShadowStep(p,dest,1800) && p.Position==dest && Items.Has(p,"LP_SM_StepBuff");
    Subclass5.TestDark=false;string lit=Subclass5.Invalid(p,Ab("LP_SM_ShadowStep").comps.OfType<CompProperties_ClassEffect>().First(),MlCell(2,-6));Subclass5.TestDark=null;
    if(w!=null)mlMade.Add(w);
    Check(w!=null && Items.Has(w,"LP_Conjured") && Items.Has(w,"LP_SM_HoundMark") && Items.Has(foe,"LP_SM_Prey") && stepped && lit!=null,"[1.135.0] shadow hound summoned (conjured, prey marked); shadow step between dark cells, refused in light ("+why+" / "+lit+")");}
   // 6. 终极：影之形态让普通伤害减半、火焰照常；坟墓之力每天判定一次（成功率 50%），火焰伤害不触发。
   {var p=S5Pawn(15,Subclass5.ShadowMagic,4,-8);Items.Give(p,"LP_SM_Form",2500);
    var pre=HarmonyLib.AccessTools.Method(typeof(Subclass5Damage),"Prefix");
    var a1=new object[]{p,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,null),false};pre.Invoke(null,a1);float cut=((DamageInfo)a1[1]).Amount;
    var a2=new object[]{p,new DamageInfo(DamageDefOf.Burn,10f,0f,-1f,null),false};pre.Invoke(null,a2);float burn=((DamageInfo)a2[1]).Amount;
    var q=S5Pawn(3,Subclass5.ShadowMagic,6,-6);Subclass5.NoteHit(q,DamageDefOf.Flame);bool fire=Subclass5.TryStand(q,0f);Subclass5.NoteHit(q,DamageDefOf.Cut);bool ok=Subclass5.TryStand(q,0f);bool again=Subclass5.TryStand(q,0f);
    Check(Near(cut,5f) && Near(burn,10f) && !fire && ok && !again,"[1.135.0] shadow form halves cut ("+cut.ToString("0.0")+") but not fire ("+burn.ToString("0.0")+"); strength of the grave once a day, never against fire");}

   // ===== 时钟之魂 =====
   // 7. 3 级选择：秩序之手；机械亲和（建造 / 修理速度 ×1.3）。
   {var p=S5Pawn(3,Subclass5.Clockwork,8,-8);
    Check(p.abilities.GetAbility(Ab("LP_CW_Balance"))!=null && Items.Has(p,"LP_CW_Affinity") && p.GetStatValue(StatDefOf.ConstructionSpeed)>0f,"[1.135.0] clockwork soul at 3: restore balance + mechanical affinity hediff");}
   // 8. 核心：秩序护盾 = 10 + 术士等级 × 2；秩序之手每天 2 次把失败的豁免改判；秩序领域让敌方机械体停机。
   {var p=S5Pawn(11,Subclass5.Clockwork,10,-8);Subclass5.Users.RemoveWhere(x=>x!=p);var ally=MlPawn(Faction.OfPlayer,11,-8);float amt=Subclass5.Ward(p,ally,2500);
    Legendary.TestD20=1;Subclass5.TestRoll=0f;int saved=0;for(int i=0;i<3;i++)if(Legendary.Save(ally,Adventurer.Wis,15))saved++;Legendary.TestD20=null;Subclass5.TestRoll=null;
    int stunned=0;var mk=DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");Pawn mech=null;
    if(mk!=null){mech=PawnGenerator.GeneratePawn(new PawnGenerationRequest(mk,Faction.OfMechanoids));GenSpawn.Spawn(mech,MlCell(12,-7),map);mlMade.Add(mech);stunned=Subclass5.Domain(p,2500,10f,0f);}
    Check(Near(amt,32f) && Items.Has(ally,"LP_ACB_Shield") && saved==2 && Items.Has(p,"LP_CW_DomainBuff") && (mech==null || stunned==1 && mech.stances.stunner.Stunned),"[1.135.0] order ward = 10 + 2×level ("+amt+"), restore balance flips 2 failed saves a day ("+saved+"/3), order domain stuns hostile mechs ("+stunned+")");}
   // 9. 终极：秩序军团召来 3 台友方临时机械体；狂野魔法不出坏结果。
   {var p=S5Pawn(15,Subclass5.Clockwork,12,-10);var list=Subclass5.Legion(p,60000);foreach(var m in list)mlMade.Add(m);
    bool good=true;for(int i=0;i<20;i++){int pick=new[]{4,6,7}[i%3];var args=new object[]{p,pick};HarmonyLib.AccessTools.Method(typeof(Subclass5Wild),"Prefix").Invoke(null,args);if(Array.IndexOf(Subclass5.BadWild,(int)args[1])>=0)good=false;}
    Check(list.Count==3 && list.All(m=>m.Faction==p.Faction && Items.Has(m,"LP_Conjured") && m.RaceProps.IsMechanoid) && good,"[1.135.0] clockwork legion: 3 friendly conjured mechs ("+string.Join(",",list.Select(m=>m.kindDef.label))+"); wild magic never rolls a bad result");}
  }
 }
}
