using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.53.0 武人之道与战士射击支自检（spec 第七节的 12 条）：Test.ps1 -VPE -Only Martial。
 public static partial class BridgeChecks {
  static readonly string[] MartialNew={"LP_Charge","LP_DisarmingStrike","LP_SteadyAim","LP_SuppressingFire","LP_PrecisionShot","LP_RapidFire","LP_DeadEye"};
  static readonly HashSet<string> MartialShooting=new HashSet<string>{"LP_SteadyAim","LP_SuppressingFire","LP_PrecisionShot","LP_RapidFire","LP_DeadEye"};
  static string savedMartialId;static float savedFury;static int savedReady;static List<string> savedStyles;
  static void Shooting(Pawn p,int level){p.skills.GetSkill(SkillDefOf.Shooting).Level=level;}
  static Hediff_Adventurer Adv(Pawn p,int level){var h=Adventurer.Get(p)??Adventurer.TryAdd(p,level,true);h.level=level;return h;}
  static Pawn Fighter10(int melee,int shooting){var p=Make(melee);Shooting(p,shooting);Classes.Psycasts(p).UnlockPath(Fighter);return p;}
  static ThingWithComps Weapon(string def){var d=DefDatabase<ThingDef>.GetNamed(def);return (ThingWithComps)ThingMaker.MakeThing(d,d.MadeFromStuff?ThingDefOf.Steel:null);}
  static void Equip(Pawn p,ThingWithComps w){p.equipment.DestroyAllEquipment();if(w!=null)p.equipment.AddEquipment(w);}

  static void MartialChecks(){
   // 1. 定义
   var need=new Dictionary<int,int>{{1,6},{2,9},{3,12},{4,15},{5,18}};
   foreach(var n in MartialNew){
    var d=DefDatabase<AbilityDef>.GetNamedSilentFail(n);var e=d?.Psycast() as LP_ClassAbilityExt;
    Check(e!=null && e.path==Fighter && e.requiredSkillLevel==need[e.level] && (MartialShooting.Contains(n)?e.Skill==SkillDefOf.Shooting:e.Skill==SkillDefOf.Melee) && ContentFinder<Texture2D>.Get(d.iconPath,false)!=null,n+" exists with skill "+(MartialShooting.Contains(n)?"Shooting":"Melee")+" "+(e==null?-1:need[e.level])+" and an icon");
   }
   var styles=Styles.All.ToList();
   var hediffs=new[]{"LP_ExtraAttack","LP_SteadyAim","LP_Suppressed","LP_RapidFire","LP_DeadEye","LP_Hobbled","LP_ChargeStrike","LP_SuppressingVolley","LP_StyleDefense","LP_StyleDueling","LP_StyleGreatWeapon","LP_StyleUnarmed","LP_Grappled"};
   Check(Classes.Stat("LP_MarksmanPower")?.parts?.OfType<StatPart_ClassSkill>().Any(x=>x.skill==SkillDefOf.Shooting)==true && styles.Count==10 && styles.All(s=>ContentFinder<Texture2D>.Get(s.iconPath,false)!=null) && hediffs.All(h=>Items.Hediff(h)!=null) && DefDatabase<ThingDef>.GetNamedSilentFail("LP_ChargeFlyer")?.thingClass==typeof(LP_ChargeFlyer),"marksman power, 10 fighting styles with icons, "+hediffs.Length+" martial hediffs and the charge flyer exist");
   Check(Fighter.martial && Classes.Path(Classes.PaladinPath).martial && Classes.Path(Classes.MonkPath).martial && !Wizard.martial && !Cleric.martial && Fighter.alternateSkill==SkillDefOf.Shooting,"fighter / paladin / monk are martial paths; fighter's alternate skill is shooting");

   // 2. 双技能路径
   var gun=Make(0);Shooting(gun,6);var weak=Make(5);Shooting(weak,5);var blade=Make(6);
   Check(Fighter.CanPawnUnlock(gun) && !Fighter.CanPawnUnlock(weak),"fighter path unlocks at melee 0 / shooting 6, not at 5 / 5");
   Classes.Psycasts(blade).UnlockPath(Fighter);var bc=blade.GetComp<CompAbilities>();
   Check(Def("LP_Charge").Psycast().PrereqsCompleted(bc) && !Def("LP_SteadyAim").Psycast().PrereqsCompleted(bc),"melee 6 / shooting 0 can learn charge but not steady aim");

   // 3. 回气看较高的一项
   var sw=Fighter10(4,12);var swe=Def("LP_SecondWind").Psycast() as LP_ClassAbilityExt;
   float mp=sw.GetStatValue(Classes.Stat("LP_MarksmanPower"));
   Check(swe.bestSkill && swe.SkillFor(sw)==SkillDefOf.Shooting && swe.PrereqsCompleted(sw.GetComp<CompAbilities>()) && Near(Classes.Power(sw,swe),mp) && Near(mp,1.1f),"second wind at melee 4 / shooting 12 is learnable and uses marksman power ("+Classes.Power(sw,swe).ToString("0.###")+" vs "+mp.ToString("0.###")+")");

   // 4. 射术强度、VPE 等级加成
   var m10=Make(0);Shooting(m10,10);
   var vf=Fighter10(10,0);float f1=Classes.Power(vf,SkillDefOf.Melee);
   Classes.Psycasts(vf).level=11;Martial.Refresh(vf);float f11=Classes.Power(vf,SkillDefOf.Melee),s11=Classes.Power(vf,SkillDefOf.Shooting)-Classes.Power(0);
   var wiz=Make(0,10);Classes.Psycasts(wiz).UnlockPath(Wizard);float w1=Classes.Power(wiz,SkillDefOf.Intellectual);
   Classes.Psycasts(wiz).level=11;Martial.Refresh(wiz);float w11=Classes.Power(wiz,SkillDefOf.Intellectual);
   Check(Near(m10.GetStatValue(Classes.Stat("LP_MarksmanPower")),1f) && Near(f11-f1,.2f,.005f) && Near(s11,.2f,.005f) && Near(w11,w1,.001f) && !Martial.Is(wiz),"marksman power 1.0 at shooting 10; VPE level 11 adds +0.2 martial / marksman power ("+f1.ToString("0.###")+" -> "+f11.ToString("0.###")+"), wizard arcane power unchanged");

   // 5. 斗志
   var st=Martial.State;st.furyHour=-1;st.furyGiven.Clear();
   var fury=Fighter10(10,0);var plain=Make(10);var foe=MakeEnemy(center+IntVec3.East*3);
   foreach(var x in new[]{fury,plain})x.psychicEntropy.OffsetPsyfocusDirectly(-1f);
   float Hit(Pawn a,Pawn v){var d=new DamageInfo(DamageDefOf.Blunt,1f,0f,-1f,a);v.PreApplyDamage(ref d,out _);return a.psychicEntropy.CurrentPsyfocus;}
   float one=Hit(fury,foe),plainGot=Hit(plain,foe);
   for(int i=0;i<49;i++)Hit(fury,foe);
   float capped=fury.psychicEntropy.CurrentPsyfocus;
   var down=MakeEnemy(center+IntVec3.West*3);HealthUtility.DamageUntilDowned(down,false);
   var fury2=Fighter10(10,0);fury2.psychicEntropy.OffsetPsyfocusDirectly(-1f);float downGot=down.Downed?Hit(fury2,down):-1f;
   Check(Near(one,.004f,.0005f) && Near(capped,.15f,.002f) && Near(st.FuryThisHour(fury),.15f,.002f) && plainGot<.0001f && downGot>=0f && downGot<.0001f,"fury: +0.4% per hit ("+one.ToString("0.####")+"), capped at 15% after 50 hits ("+capped.ToString("0.###")+"), none for non-martial ("+plainGot+") or vs a downed foe ("+downGot+")");
   if(!foe.Destroyed)foe.Destroy();if(!down.Destroyed)down.Destroy();

   // 6. 额外攻击档位
   var pal=Make(10);Classes.Psycasts(pal).UnlockPath(Classes.Path(Classes.PaladinPath));var ph=Adv(pal,5);Martial.Refresh(pal);int palTier5=Martial.Tier(pal);bool palHediff=Items.Has(pal,"LP_ExtraAttack");
   ph.level=11;Martial.Refresh(pal);int palTier11=Martial.Tier(pal);
   var ft=Fighter10(10,0);Adv(ft,11);Martial.Refresh(ft);int ftTier=Martial.Tier(ft);
   var monk=Make(10);Classes.Psycasts(monk).UnlockPath(Classes.Path(Classes.MonkPath));Adv(monk,5);
   Equip(monk,Weapon("MeleeWeapon_Gladius"));Martial.Refresh(monk);int armed=Martial.Tier(monk);
   Equip(monk,null);Martial.Refresh(monk);int bare=Martial.Tier(monk);
   var ea=ft.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ExtraAttack"));
   Check(palTier5==1 && palHediff && palTier11==1 && ftTier==2 && ea!=null && Near(ea.CurStage.statFactors.First(m=>m.stat==StatDefOf.MeleeCooldownFactor).value,.7f) && Near(ea.CurStage.statFactors.First(m=>m.stat==StatDefOf.RangedCooldownFactor).value,.75f) && armed==0 && bare==1,"extra attack: paladin 5 -> tier "+palTier5+", paladin 11 -> "+palTier11+", fighter 11 -> "+ftTier+" (x0.70 / x0.75), monk-only armed "+armed+" / unarmed "+bare);

   // 7. 追击
   var att=Fighter10(15,0);Adv(att,5);Martial.Refresh(att);var tgt=MakeEnemy(att.Position+IntVec3.North);
   int hits0=ClassMeleeHit.Hits,fu0=Martial.FollowUps;Martial.FollowUpOverride=1f;
   try{ClassMelee.Strike(att,tgt);}finally{Martial.FollowUpOverride=null;}
   Check(ClassMeleeHit.Hits-hits0==2 && Martial.FollowUps-fu0==1,"follow-up at chance 1: one melee strike lands twice ("+(ClassMeleeHit.Hits-hits0)+" hits), no recursion");
   if(!tgt.Destroyed)tgt.Destroy();

   // 8. 战斗风格槽位与改选
   var sp=Fighter10(10,0);var sh=Adv(sp,5);Martial.Refresh(sp);int s1=Styles.Slots(sp);sh.level=10;int s2=Styles.Slots(sp);
   bool first=Styles.Pick(sh,0,Styles.GreatWeapon);bool gwLive=(sh.styleMask & Styles.GreatWeapon.Bit)!=0;
   int now=Find.TickManager.TicksGame;bool changed=Styles.Pick(sh,0,Styles.Dueling);
   bool waiting=sh.styleReadyTick==now+Styles.ReadyDelay && !Styles.Has(sp,Styles.Dueling) && !Styles.Has(sp,Styles.GreatWeapon) && Styles.CanPick(sh,0,Styles.Archery)!=null;
   sh.styleReadyTick=now;Styles.Refresh(sh);bool after=Styles.Has(sp,Styles.Dueling);
   Check(s1==1 && s2==2 && first && gwLive && changed && waiting && after,"styles: 1 slot, 2 for a level-10 fighter; first pick is live; re-pick waits 180000 ticks with neither style active, then the new one works");

   // 9. 决斗 → 巨武器（250 刻刷新）
   var du=Fighter10(10,0);var dh=Adv(du,10);Martial.Refresh(du);Styles.Pick(dh,0,Styles.Dueling);Styles.Pick(dh,1,Styles.GreatWeapon);
   Equip(du,Weapon("MeleeWeapon_Gladius"));Styles.Refresh(dh);bool duel=Items.Has(du,"LP_StyleDueling") && !Items.Has(du,"LP_StyleGreatWeapon");
   var heavy=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.IsMeleeWeapon && !d.destroyOnDrop && d.GetCompProperties<CompProperties_BladelinkWeapon>()==null && (!d.MadeFromStuff || GenStuff.AllowedStuffsFor(d).Contains(ThingDefOf.Steel)) && d.GetStatValueAbstract(StatDefOf.Mass,d.MadeFromStuff?ThingDefOf.Steel:null)>=Feats.TwoHandedMass).OrderByDescending(d=>d.modContentPack!=null && d.modContentPack.IsOfficialMod).ThenBy(d=>d.defName).FirstOrDefault();
   Equip(du,heavy==null?null:Weapon(heavy.defName));Styles.Refresh(dh);bool great=Feats.TwoHanded(du) && Items.Has(du,"LP_StyleGreatWeapon") && !Items.Has(du,"LP_StyleDueling");
   Check(duel && great,"dueling with a one-handed gladius, great weapon after switching to "+(heavy?.defName??"(none)")+" on the next refresh");

   // 10. 压制射击、精准射击的子弹登记
   var sn=Fighter10(0,10);Equip(sn,Weapon("Gun_AssaultRifle"));var mark=MakeEnemy(sn.Position+IntVec3.East*8);
   var proj=Ability_PrecisionShot.Shoot(sn,mark,1f,PrecisionMode.Vital);
   Martial.Shots.TryGetValue(proj,out var mod);int boosted=proj.DamageAmount;float apB=proj.ArmorPenetration;Martial.Shots.Remove(proj);int plainDmg=proj.DamageAmount;float apP=proj.ArmorPenetration;Martial.Shots[proj]=mod;
   var vol=Ability_SuppressingFire.FireOne(sn,mark.Position);bool volOk=Martial.Shots.TryGetValue(vol,out var vm) && Near(vm.damage,.5f);
   float before=ClassAbility.InjurySum(mark);
   var impact=AccessTools.Method(typeof(Projectile),"Impact");impact.Invoke(proj,new object[]{mark,false});if(!vol.Destroyed)impact.Invoke(vol,new object[]{null,false});
   Check(mod!=null && Near(mod.damage,2f) && boosted==Mathf.Max(1,Mathf.RoundToInt(plainDmg*2f)) && Near(apB-apP,.3f) && volOk && !Martial.Shots.ContainsKey(proj) && !Martial.Shots.ContainsKey(vol) && Hurt(mark,before),"precision shot at R=1 registers x2.0 ("+plainDmg+" -> "+boosted+", AP +"+(apB-apP).ToString("0.##")+"), suppressing fire x0.5; both leave Martial.Shots on impact");
   if(!mark.Destroyed)mark.Destroy();

   // 11. 死神之眼
   var de=Fighter10(0,18);Equip(de,Weapon("Gun_AssaultRifle"));Learn<Ability_SelfBuff>(de,"LP_DeadEye").Apply(null);
   var aim=Learn(de,"LP_SteadyAim");aim.cooldown=Find.TickManager.TicksGame+5000;
   var victim=MakeEnemy(de.Position+IntVec3.East*6);
   var rep=ShotReport.HitReportFor(de,de.equipment.PrimaryEq.PrimaryVerb,victim);
   bool sure=Feats.AimHook(de)>=Feats.MustHit && rep.AimOnTargetChance_IgnoringPosture>=.99f && rep.PassCoverChance>=.999f;
   victim.Kill(new DamageInfo(DamageDefOf.Bullet,200f,0f,-1f,de,null,de.equipment.Primary.def));
   Check(sure && aim.cooldown==0,"dead eye: AimHook = must-hit (aim "+rep.AimOnTargetChance_IgnoringPosture.ToString("0.##")+"), a ranged kill resets fighter cooldowns");
   if(!victim.Destroyed)victim.Destroy();
  }

  // 12. 存读档：战斗风格、适应中的生效刻、精准射击模式、斗志本小时计数；Martial.Cache 读档后由 250 刻刷新重建。
  static void MartialPrepareSave(){
   var p=Fighter10(10,10);var h=Adv(p,10);Martial.Refresh(p);
   Styles.Pick(h,0,Styles.Archery);Styles.Pick(h,1,Styles.Protection);Styles.Pick(h,0,Styles.Thrown);
   Learn(p,"LP_PrecisionShot");Martial.State.SetMode(p,PrecisionMode.Hand);
   p.psychicEntropy.OffsetPsyfocusDirectly(-1f);Martial.Fury(p,.05f);
   savedMartialId=p.ThingID;keep.Add(p.ThingID);savedFury=Martial.State.FuryThisHour(p);savedReady=h.styleReadyTick;savedStyles=h.styles.Select(s=>s.defName).ToList();
   Check(savedFury>.04f && savedReady>Find.TickManager.TicksGame && savedStyles.Count==2,"martial save fixture prepared");
  }
  static void MartialAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==savedMartialId);
   Check(p!=null,"martial pawn survives save/load");
   var h=Adventurer.Get(p);var st=Martial.State;
   MapComponent_ClassAuras.Refresh(map);if(h!=null)Styles.Refresh(h); // 250 刻刷新（Martial 与冒险者状态各一次）
   Check(h!=null && h.styles.Select(s=>s.defName).SequenceEqual(savedStyles) && h.styleReadyTick==savedReady && h.styleAdaptSlot==0 && st.Mode(p)==PrecisionMode.Hand && Near(st.FuryThisHour(p),savedFury,.0005f) && Martial.Is(p) && Styles.Has(p,Styles.Protection) && !Styles.Has(p,Styles.Thrown),"fighting styles, adapt tick, precision mode and fury survive save/load; Martial.Cache is rebuilt on the next 250-tick refresh");
   p.Destroy();keep.Remove(savedMartialId);
  }
 }
}
