using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.103.0 第二批子职业（Test.ps1 -Only Subclass2，22 条）。
 public partial class LichChecks {
  Pawn S2Pawn(string cls,int classLevel,string sub,int level=-1){
   var c=DefDatabase<LP_ClassDef>.GetNamed(cls);var p=ClsPawn(level>0?level:Mathf.Max(classLevel,1),10,10,10);
   foreach(var s in new[]{SkillDefOf.Intellectual,SkillDefOf.Crafting,SkillDefOf.Social,SkillDefOf.Melee,SkillDefOf.Shooting,SkillDefOf.Plants,SkillDefOf.Animals})p.skills.GetSkill(s).Level=12;
   var h=Adventurer.Get(p);for(int i=0;i<classLevel;i++)ClassSystem.AddLevel(h,c);
   if(sub!=null)ClassSubclass.Pick(h,DefDatabase<LP_SubclassDef>.GetNamed(sub));
   foreach(var k in ClassSystem.Taken(h).ToList())ClassSystem.AddResource(h,k,99f);
   Subclass2.Refresh(h);return p;
  }
  bool S2Cast(Pawn p,string ab,LocalTargetInfo t){var a=p.abilities.GetAbility(Ab(ab));a.ResetCooldown();return a.Activate(t,t);}
  void CheckSubclass2(){
   var made=new List<Thing>();
   Pawn Add(Pawn x){made.Add(x);return x;}
   try{
    // 1. 定义：7 个子职业（徽记、能力在 3/7/11/15），43 个能力（Ability_Class、图标、算所属职业），钢铁卫士有贴图。
    var names=new[]{Subclass2.EldritchKnight,Subclass2.PsiWarrior,Subclass2.Hemomancy,Subclass2.BattleSmith,Subclass2.Aberrant,Subclass2.Conquest,Subclass2.Redemption};
    var subs=names.Select(n=>DefDatabase<LP_SubclassDef>.GetNamed(n)).ToList();var abs=subs.SelectMany(s=>s.abilities).ToList();
    var kind=DefDatabase<PawnKindDef>.GetNamed("LP_SteelDefenderKind");
    Check(subs.All(s=>s.Emblem!=null && s.Emblem!=s.parent.Background && s.abilities.Select(e=>e.level).Distinct().OrderBy(x=>x).SequenceEqual(new[]{3,7,11,15})) && abs.Count==43 && abs.Select(e=>e.ability).Distinct().Count()==43
     && abs.All(e=>e.ability.abilityClass==typeof(Ability_Class) && e.ability.uiIcon!=null && e.ability.uiIcon!=BaseContent.BadTex && ClassSystem.ClassOf(e.ability)==ClassSubclass.SubOf(e.ability).parent)
     && kind.lifeStages[0].bodyGraphicData.Graphic.MatSouth.mainTexture!=BaseContent.BadTex,"subclass2: 7 subclasses (emblems, 3/7/11/15), 43 abilities with icons counted as their class, steel defender textured");
    // 2. 奥法骑士：选定即有法力（战士 3 级 → 2）；护盾术耗 1 法力；武器羁绊后不能缴械，丢下后召回到手。
    var ek=Add(S2Pawn("LP_Class_Fighter",3,Subclass2.EldritchKnight));ScSpawn(ek,ScCell(-6,0));var hek=Adventurer.Get(ek);
    var sword=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword"),ThingDefOf.Steel);ek.equipment.AddEquipment(sword);
    float m0=ClassSubclass.Mana(hek);bool shield=S2Cast(ek,"LP_EK_Shield",ek);float m1=ClassSubclass.Mana(hek);
    bool bound=S2Cast(ek,"LP_EK_WeaponBond",ek) && Subclass2.Bound(ek) && !Martial.CanDisarm(ek);
    ek.equipment.TryDropEquipment(sword,out var dropped,ek.Position+IntVec3.East*2);bool gone=ek.equipment.Primary==null;bool recall=S2Cast(ek,"LP_EK_WeaponBond",ek) && ek.equipment.Primary==sword;
    Check(ClassSubclass.ManaMax(hek)==2 && Near(m0,2f) && shield && Near(m1,1f) && Items.Has(ek,"LP_ShieldSpell") && bound && gone && recall,"eldritch knight: 2 mana at fighter 3, shield costs 1 ("+m0+"→"+m1+" "+shield+Items.Has(ek,"LP_ShieldSpell")+bound+gone+recall+Subclass2.Bound(ek)+"), bonded sword can't be disarmed and is recalled to hand");
    // 3. 奥法骑士 11：法术打击让目标受法术伤害 ×1.25；吸收元素让火焰 ×0.5；雷鸣斩命中后消耗。
    var ek11=Add(S2Pawn("LP_Class_Fighter",11,Subclass2.EldritchKnight,11));ScSpawn(ek11,ScCell(-6,6));var foe=Add(ScFoe(ek11));
    float vuln0=Subclass2.DamageFactor(foe,Dmg(DamageDefOf.Flame,null));Subclass2.DamageFactor(foe,Dmg(DamageDefOf.Cut,ek11));float vuln1=Subclass2.DamageFactor(foe,Dmg(DamageDefOf.Flame,null));
    S2Cast(ek11,"LP_EK_AbsorbElements",ek11);float abs1=Subclass2.DamageFactor(ek11,Dmg(DamageDefOf.Flame,null));
    S2Cast(ek11,"LP_EK_ThunderBlade",ek11);bool ready=Items.Has(ek11,"LP_EK_ThunderReady");Subclass2.DamageFactor(foe,Dmg(DamageDefOf.Cut,ek11));
    Check(Near(vuln0,1f) && Near(vuln1,1.25f) && Near(abs1,.5f) && ready && !Items.Has(ek11,"LP_EK_ThunderReady") && ClassSubclass.ManaMax(Adventurer.Get(ek11))==4,"eldritch knight 11: eldritch strike ×1.25 to spells, absorb elements ×0.5 fire, thunder blade spent on hit, 4 mana");
    // 4. 灵能战士：没有灵能器时护体念力 ×0.85、念动推送耗斗志；有灵能器后不耗斗志、改加神经热，护体念力 ×0.7。
    var pw=Add(S2Pawn("LP_Class_Fighter",7,Subclass2.PsiWarrior));ScSpawn(pw,ScCell(6,0));var hpw=Adventurer.Get(pw);var foe2=Add(ScFoe(pw));
    float noLink=Subclass2.DamageFactor(pw,Dmg(DamageDefOf.Cut,foe2));float r0=ClassSystem.Resource(hpw,ClassSystem.Fighter);ClassMartial2.RollOverride=0f;bool shoved;try{shoved=S2Cast(pw,"LP_PW_Shove",foe2);}finally{ClassMartial2.RollOverride=null;}float r1=ClassSystem.Resource(hpw,ClassSystem.Fighter);
    if(!pw.HasPsylink)pw.health.AddHediff(HediffDefOf.PsychicAmplifier,pw.health.hediffSet.GetBrain());HarmonyLib.Traverse.Create(pw.psychicEntropy).Field("psylinkCachedForTick").SetValue(-1);/* 原版按刻缓存 */if(pw.GetPsylinkLevel()<1)pw.ChangePsylinkLevel(1,false);HarmonyLib.Traverse.Create(pw.psychicEntropy).Field("psylinkCachedForTick").SetValue(-1);float e0=pw.psychicEntropy.EntropyValue;S2Cast(pw,"LP_PW_PsionicStrike",pw);float r2=ClassSystem.Resource(hpw,ClassSystem.Fighter),e1=pw.psychicEntropy.EntropyValue;float link=Subclass2.DamageFactor(pw,Dmg(DamageDefOf.Cut,foe2));
    Subclass2.Tick250(hpw);
    Check(Near(noLink,.85f) && shoved && Near(r1,r0-1f) && Near(r2,r1) && e1>e0+5f && Near(link,.7f) && Items.Has(pw,"LP_PW_AdeptBuff") && Items.Has(pw,"LP_PW_StrikeBuff"),"psi warrior: field ×0.85 without psylink, shove costs 1 斗志; with psylink strike is free and adds heat ("+e0+"→"+e1+" link="+pw.HasPsylink+"/"+pw.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier)+"/"+(pw.health.hediffSet.GetBrain()!=null)+"/"+ModsConfig.RoyaltyActive+"/"+pw.def.defName+" max="+pw.psychicEntropy.MaxEntropy+" lv="+pw.GetPsylinkLevel()+" r="+r0+"/"+r1+"/"+r2+" f="+noLink+"/"+link+" shove="+shoved+"), field ×0.7");
    // 5. 血咒学派：巫妖不能选；血矛失血 8% 并造成伤害；失血到上限就不能施放。
    var lichTest=Items.IsLich(ek);var hm=Add(S2Pawn("LP_Class_Wizard",3,Subclass2.Hemomancy));ScSpawn(hm,ScCell(6,6));var foe3=Add(ScFoe(hm));foe3.apparel?.DestroyAll();
    float hp0=ClassMelee.InjurySum(foe3);bool spear=S2Cast(hm,"LP_HM_BloodSpear",foe3);float bl=Subclass2.BloodLoss(hm);float hp1=ClassMelee.InjurySum(foe3);
    Subclass2.AddBlood(hm,.4f);string capWhy=ClassSystem.CannotCast(hm,Ab("LP_HM_BloodSpear"),Fx("LP_HM_BloodSpear"));
    Check(!lichTest && spear && Near(bl,.08f) && (hp1>hp0 || foe3.Dead) && capWhy!=null && capWhy.Contains("失血"),"hemomancy: blood spear costs 8% blood ("+bl+") and wounds; at the 45% cap it refuses ("+capWhy+")");
    // 6. 战铸师：用地图上的 100 钢 + 2 零部件造卫士；卫士受伤倍率随等级降低；卫士死后留 20 钢、重造只要 20 钢。
    var bs=Add(S2Pawn("LP_Class_Artificer",5,Subclass2.BattleSmith));bs.SetFaction(Faction.OfPlayer);ScSpawn(bs,ScCell(0,8));
    var st=ThingMaker.MakeThing(ThingDefOf.Steel);st.stackCount=150;GenPlace.TryPlaceThing(st,bs.Position+IntVec3.West*2,map,ThingPlaceMode.Near);made.Add(st);
    var cp=ThingMaker.MakeThing(ThingDefOf.ComponentIndustrial);cp.stackCount=3;GenPlace.TryPlaceThing(cp,bs.Position+IntVec3.West*3,map,ThingPlaceMode.Near);made.Add(cp);
    int steel0=map.listerThings.ThingsOfDef(ThingDefOf.Steel).Sum(x=>x.stackCount);string why=Subclass2.DefenderCannot(bs);var def1=Subclass2.Defender(bs);if(def1!=null)made.Add(def1);
    int steel1=map.listerThings.ThingsOfDef(ThingDefOf.Steel).Sum(x=>x.stackCount);float df=def1==null?1f:Subclass2.DamageFactor(def1,Dmg(DamageDefOf.Cut,null));
    if(def1!=null)def1.Kill(null);var d=ClassSubclass.Data(bs);int rs=Subclass2.Live(d).Count;
    Check(why==null && def1!=null && def1.playerSettings?.Master==bs && Items.Has(def1,"LP_DefenderCore") && steel0-steel1==100 && Near(df,1f/1.5f) && rs==0 && d.ticks.ContainsKey("defDied"),"battle smith: defender built from 100 steel + 2 components (steel "+steel0+"→"+steel1+"), takes ×"+df.ToString("0.00")+" at artificer 5, death starts the 20-steel repair window");
    // 7. 异变心灵：灵能尖刺伤敌；心灵支配让敌人加入殖民地，到期（没留下）变回原阵营。
    var am=Add(S2Pawn("LP_Class_Sorcerer",15,Subclass2.Aberrant,15));am.SetFaction(Faction.OfPlayer);ScSpawn(am,ScCell(-8,-6));var foe4=Add(ScFoe(am));foe4.apparel?.DestroyAll();var orig=foe4.Faction;
    float psySens=foe4.GetStatValue(StatDefOf.PsychicSensitivity);float a0=foe4.health.hediffSet.hediffs.Count+ClassMelee.InjurySum(foe4);bool spike=S2Cast(am,"LP_AM_PsychicSpike",foe4);float a1=foe4.health.hediffSet.hediffs.Count+ClassMelee.InjurySum(foe4);
    foe4=Add(ScFoe(am));orig=foe4.Faction;ClassMartial2.RollOverride=0f;bool dom;try{dom=Subclass2.Dominion(am,foe4);}finally{ClassMartial2.RollOverride=null;}bool joined=foe4.Faction==Faction.OfPlayer;
    ClassMartial2.RollOverride=.99f;try{Current.Game.GetComponent<GameComponent_Subclass2>().ExpireNow();}finally{ClassMartial2.RollOverride=null;}
    Check(spike && (a1>a0 || foe4.Dead || psySens<.3f) && dom && joined && foe4.Faction==orig,"aberrant mind: psychic spike wounds; dominion turns the foe to the colony, and expiry (not staying) returns it to "+orig?.Name+" ("+spike+" "+a0+"/"+a1+" "+dom+" "+joined+" now="+foe4.Faction?.Name+" mana="+ClassSystem.Resource(Adventurer.Get(am),DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Sorcerer"))+")");
    // 8. 征服之誓：誓言之力 1 次，用完就不能放；征服者之威让敌人震慑；降服让重伤的人形敌人丢武器投降。
    var cq=Add(S2Pawn("LP_Class_Paladin",15,Subclass2.Conquest,15));ScSpawn(cq,ScCell(8,-6));var hcq=Adventurer.Get(cq);var foe5=Add(ScFoe(cq));
    int left0=Subclass2.OathLeft(cq);ClassMartial2.RollOverride=0f;bool pres;try{pres=S2Cast(cq,"LP_CQ_ConqueringPresence",cq);}finally{ClassMartial2.RollOverride=null;}int left1=Subclass2.OathLeft(cq);
    bool shaken=Items.Has(foe5,"LP_Shaken");
    var foe6=Add(ScFoe(cq));foe6.equipment?.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife"),ThingDefOf.Steel));
    for(int i=0;i<60 && foe6.health.summaryHealth.SummaryHealthPercent>=.5f && !foe6.Downed && !foe6.Dead;i++)foe6.TakeDamage(new DamageInfo(DamageDefOf.Cut,4f,0f,-1f,null,foe6.health.hediffSet.GetNotMissingParts().Where(x=>x.depth==BodyPartDepth.Outside && x.def.hitPoints>=20).RandomElement()));
    int yielded=foe6.Downed || foe6.Dead?0:Subclass2.Yield(cq,true);
    Check(left0==3 && pres && left1==2 && shaken && (foe6.Downed || foe6.Dead || yielded>=1 && Items.Has(foe6,"LP_CQ_Yielded") && foe6.equipment.Primary==null),"conquest: 3 oath uses at paladin 15 ("+left0+"→"+left1+"), presence shakes, a badly hurt foe yields and drops its weapon (shaken="+shaken+" yielded="+yielded+" hp="+foe6.health.summaryHealth.SummaryHealthPercent+")");
    // 9. 救赎之誓：杀死倒地的人就背誓（神恩恢复减半）；守护者灵光（征召时）替 5 格内友军挨打；斥责暴力反伤近战攻击者。
    var rd=Add(S2Pawn("LP_Class_Paladin",7,Subclass2.Redemption,7));rd.SetFaction(Faction.OfPlayer);ScSpawn(rd,ScCell(0,-8));var foe7=Add(ScFoe(rd));
    var ally=Add(S2Pawn("LP_Class_Fighter",1,null));ally.SetFaction(Faction.OfPlayer);ScSpawn(ally,NextTo(rd));
    rd.drafter.Drafted=true;Subclass2.Refresh(Adventurer.Get(rd));float shared=Subclass2.DamageFactor(ally,Dmg(DamageDefOf.Cut,foe7));rd.drafter.Drafted=false;
    S2Cast(rd,"LP_RD_RebukeViolent",rd);bool rebuke=Subclass2.Rebukers.Contains(rd);Subclass2.DamageFactor(ally,Dmg(DamageDefOf.Cut,foe7));bool spent=!Items.Has(rd,"LP_RD_Rebuke");
    var victim=Add(ScFoe(rd));HealthUtility.DamageUntilDowned(victim,false);bool down=victim.Downed;if(!victim.Dead)victim.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,rd));
    Check(Near(shared,0f) && rebuke && spent && down && Items.Has(rd,"LP_Oathbroken") && ClassSystem.CannotCast(rd,Ab("LP_RD_EmissaryOfPeace"),Fx("LP_RD_EmissaryOfPeace"))!=null,"redemption: drafted guardian takes an ally's hit, rebuke spends on a melee hit, killing a downed foe breaks the oath");
    // 10. 龙魂（1.104.0）：龙血体质 +2；龙翼走路无视地形；龙威对附近敌人做骇人威仪判定（惊惧或记下免疫）。
    var ds=Add(S2Pawn("LP_Class_Sorcerer",14,Subclass2.DraconicSoul,14));ScSpawn(ds,ScCell(-10,4));var hds=Adventurer.Get(ds);
    int con=hds.Eff(Adventurer.Con),bonus=Subclass2.ScoreBonus(hds,Adventurer.Con);var foe8=Add(ScFoe(ds));
    int feared=Subclass2.DragonPresence(ds);var lmc=MapComp<MapComponent_Legendary>.Get(map);
    Check(bonus==2 && Subclass2.Fliers.Contains(ds) && (feared>0 && Items.Has(foe8,"LP_Frightened") || lmc!=null && lmc.ImmuneTo(ds,foe8)),"draconic soul: dragon blood Con +2 (now "+con+"), dragon flight ignores terrain, dragon presence checks a nearby foe (frightened "+feared+")");
    // 11. 恶鬼之血（1.104.0）：每个强化法力上限 −2；8 级起同时 2 个并且翻倍；第三个被拒；12 级固化的那个不占名额、不扣上限；恶鬼之皮让皮肤变红。
    var fb=Add(S2Pawn("LP_Class_Sorcerer",12,Subclass2.FiendBlood,12));ScSpawn(fb,ScCell(10,4));var hfb=Adventurer.Get(fb);var sor=DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Sorcerer");
    int max0=ClassSystem.MaxResource(hfb,sor);int cha0=hfb.Eff(Adventurer.Cha);
    string t1=Subclass2.FiendToggle(fb,0);int max1=ClassSystem.MaxResource(hfb,sor);int cha1=hfb.Eff(Adventurer.Cha);
    string t2=Subclass2.FiendToggle(fb,1);bool red=fb.story.skinColorOverride==Hediff_FiendSkin.Red;string t3=Subclass2.FiendToggle(fb,2);int max2=ClassSystem.MaxResource(hfb,sor);
    bool chose=ClassSubclass.Choose(hfb,"fbFixed",2);Subclass2.Tick250(hfb);int max3=ClassSystem.MaxResource(hfb,sor);bool tongue=Items.Has(fb,"LP_FB_TongueBuff");
    Subclass2.FiendToggle(fb,1);bool unred=fb.story.skinColorOverride!=Hediff_FiendSkin.Red;
    Check(t1=="on" && max1==max0-2 && cha1==cha0+4 && t2=="on" && red && t3!="on" && max2==max0-4 && chose && tongue && max3==max0-4 && unred,"fiend blood: each enhancement −2 mana max ("+max0+"→"+max1+"→"+max2+"), awakened eyes Cha +4, a third is refused ("+t3+"), fixed tongue is free ("+max3+"), red skin comes and goes");    // 12. 暗影宗（1.106.0）：暗影技艺给范围里的敌人魔法黑暗；幽影斗篷亮处拒绝 / 暗处隐形；灵机一斩：身边的敌人被别人打中就记一次补击。
    var sh=Add(S2Pawn("LP_Class_Monk",17,Subclass3.Shadow,17));ScSpawn(sh,ScCell(-12,-8));var hsh=Adventurer.Get(sh);Subclass3.Refresh(hsh);var foe9=Add(ScFoe(sh));
    bool arts=S2Cast(sh,"LP_SH_ShadowArts",new LocalTargetInfo(foe9.Position));bool darkened=Items.Has(foe9,"LP_WL_Darkened");
    string cloakWhy=Subclass3.Invalid(sh,Fx("LP_SH_Cloak"),sh);bool cloakOk=Subclass3.InDark(sh)?cloakWhy==null && S2Cast(sh,"LP_SH_Cloak",sh) && Items.Has(sh,"LP_SH_Cloaked"):cloakWhy!=null;
    var helper=Add(S2Pawn("LP_Class_Fighter",1,null));ScSpawn(helper,NextTo(foe9));Subclass3.DamageFactor(foe9,Dmg(DamageDefOf.Cut,helper));bool opp=ClassSubclass.Data(sh).ticks.ContainsKey("opp");
    Check(arts && darkened && cloakOk && opp && Subclass3.Opportunists.Contains(sh),"shadow way: shadow arts darkens the foe, cloak "+(Subclass3.InDark(sh)?"works in the dark":"is refused in light ("+cloakWhy+")")+", opportunist strikes a foe hit by someone else");
    // 13. 散打宗（1.106.0）：切到“推开”后疾风连击的一拳把目标推开；混元体治疗 3 × 等级；宁神境让一下落空、自己出手就散；渗透劲种下后引爆让目标倒下。
    var oh=Add(S2Pawn("LP_Class_Monk",17,Subclass3.OpenHand,17));ScSpawn(oh,ScCell(12,-8));var hoh=Adventurer.Get(oh);Subclass3.Refresh(hoh);var foe10=Add(ScFoe(oh));foe10.apparel?.DestroyAll();
    S2Cast(oh,"LP_OH_Technique",oh);int mode=Subclass3.Mode(oh);var before=foe10.Position;ClassMartial2.RollOverride=0f;try{Subclass3.FlurryHit(oh,foe10);}finally{ClassMartial2.RollOverride=null;}bool pushed=foe10.Position!=before || DNDPush.Destination(map,before,(before-oh.Position).ToVector3(),3)==before;
    oh.TakeDamage(new DamageInfo(DamageDefOf.Cut,30f,0f,-1f,null));float hurt0=ClassMelee.InjurySum(oh);S2Cast(oh,"LP_OH_Wholeness",oh);float hurt1=ClassMelee.InjurySum(oh);
    Items.Give(oh,"LP_OH_Tranquil");float tq;ClassMartial2.RollOverride=0f;try{tq=Subclass3.DamageFactor(oh,Dmg(DamageDefOf.Cut,foe10));}finally{ClassMartial2.RollOverride=null;}Subclass3.DamageFactor(foe10,Dmg(DamageDefOf.Cut,oh));bool tqGone=!Items.Has(oh,"LP_OH_Tranquil");
    var foe11=Add(ScFoe(oh));foe11.apparel?.DestroyAll();bool planted=S2Cast(oh,"LP_OH_Palm",foe11) && Subclass3.Palmed(oh)==foe11;ClassMartial2.RollOverride=0f;bool det;try{det=S2Cast(oh,"LP_OH_Detonate",oh);}finally{ClassMartial2.RollOverride=null;}
    Check(mode==1 && pushed && hurt0-hurt1>=Mathf.Min(hurt0,50f)-.5f && Near(tq,0f) && tqGone && planted && det && (foe11.Downed || foe11.Dead) && Subclass3.Palmed(oh)==null,"open hand way: technique cycles to push and shoves on a flurry hit, wholeness heals ("+hurt0+"→"+hurt1+"), tranquility dodges then breaks on attack, quivering palm planted and detonated (mode="+mode+" pushed="+pushed+" tq="+tq+" gone="+tqGone+" planted="+planted+" det="+det+" down="+foe11.Downed+"/"+foe11.Dead+")");    // 14. 塑能学派（1.107.0）：法师 2 级就能选；强效塑能按智力加成；法术塑形让火焰不伤友军；超限导能 ×2，同一觉内第二次反噬。
    var wiz=DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Wizard");var ev2=S2Pawn("LP_Class_Wizard",2,null);made.Add(ev2);bool canAt2=ClassSubclass.CanPick(Adventurer.Get(ev2),DefDatabase<LP_SubclassDef>.GetNamed(Subclass3.Evocation))==null;
    var ev=Add(S2Pawn("LP_Class_Wizard",14,Subclass3.Evocation,14));ScSpawn(ev,ScCell(-12,10));var hev=Adventurer.Get(ev);hev.scores[Adventurer.Int]=16;hev.Dirty();Subclass3.Refresh(hev);
    float emp=Subclass3.PowerFactor(ev,Fx("LP_Fireball"));var pal=Add(S2Pawn("LP_Class_Fighter",1,null));ScSpawn(pal,NextTo(ev));float sculpt=Subclass3.DamageFactor(pal,Dmg(DamageDefOf.Flame,ev));
    S2Cast(ev,"LP_EV_Overchannel",ev);float over=Subclass3.PowerFactor(ev,Fx("LP_Fireball"));var far=ScCell(-12,22);ClassSystem.AddResource(hev,wiz,99f);S2Cast(ev,"LP_Fireball",new LocalTargetInfo(far));
    float hurt=ClassMelee.InjurySum(ev);S2Cast(ev,"LP_EV_Overchannel",ev);S2Cast(ev,"LP_Fireball",new LocalTargetInfo(far));float hurt2=ClassMelee.InjurySum(ev);ClassSubclass.Data(ev).ticks.TryGetValue("ovN",out int ovN);
    Check(canAt2 && Near(emp,1.3f) && Near(sculpt,0f) && Near(over,2.6f) && !Items.Has(ev,"LP_EV_Overchannel") && ovN==2 && (hurt2>hurt+10f || ev.Dead || ev.Downed),"evocation: pickable at wizard 2, Int 16 → ×"+emp+", sculpt spares allies, overchannel ×2 ("+over+"), second overchannel backlashes ("+hurt+"→"+hurt2+", uses "+ovN+", dead="+ev.Dead+")");
    // 15. 书士会（1.107.0）：法师笔读卷轴不失误；觉醒魔典把火球换成寒冰；思维具现 3 次免费施法；大书士给卷轴；一字同心挡一下、扣法力。
    var sc=Add(S2Pawn("LP_Class_Wizard",14,Subclass3.Scribes,14));ScSpawn(sc,ScCell(12,10));var hsc=Adventurer.Get(sc);Subclass3.Refresh(hsc);sc.skills.GetSkill(SkillDefOf.Intellectual).Level=6;
    float fail=ItemScrolls.FailChance(sc);S2Cast(sc,"LP_SC_Tome",sc);S2Cast(sc,"LP_SC_Tome",sc);var re=Subclass3.Retype(sc,DamageDefOf.Flame);
    S2Cast(sc,"LP_SC_Manifest",sc);float sm0=ClassSystem.Resource(hsc,wiz);S2Cast(sc,"LP_Fireball",new LocalTargetInfo(ScCell(12,22)));float sm1=ClassSystem.Resource(hsc,wiz);float left=sc.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_SC_Manifest"))?.Severity??0f;
    var scroll=Subclass3.MasterScroll(sc);if(scroll!=null)made.Add(scroll);
    for(int i=0;i<40 && sc.health.summaryHealth.SummaryHealthPercent>=.5f && !sc.Downed;i++)sc.TakeDamage(new DamageInfo(DamageDefOf.Cut,6f,0f,-1f,null,sc.health.hediffSet.GetNotMissingParts().Where(x=>x.depth==BodyPartDepth.Outside && x.def.hitPoints>=20).RandomElement()));
    float mana0=ClassSystem.Resource(hsc,wiz);float ow=sc.health.summaryHealth.SummaryHealthPercent<.5f?Subclass3.DamageFactor(sc,Dmg(DamageDefOf.Cut,null,20f)):0f;float mana1=ClassSystem.Resource(hsc,wiz);
    Check(Near(fail,0f) && re==(DefCache<DamageDef>.Get("LP_Frost")??DamageDefOf.Frostbite) && Near(sm0,sm1) && Near(left,2f) && scroll!=null && sc.inventory.innerContainer.Contains(scroll) && Near(ow,0f) && mana1<mana0,"scribes: quill never fizzles, tome retypes fireball to frost, manifest makes a fireball free ("+sm0+"→"+sm1+", "+left+" left), master scrivener adds a scroll, one with the word blocks a hit for mana ("+mana0+"→"+mana1+" ow="+ow+" hp="+sc.health.summaryHealth.SummaryHealthPercent+" dead="+sc.Dead+" down="+sc.Downed+" flag="+Subclass3.S(sc,Subclass3.FOneWord)+" used="+ClassSubclass.Data(sc).ticks.ContainsKey("oneWord")+")");    // 16. 神龙宗（1.108.0）：切到寒冰；龙之息免费次数内不耗气并伤到锥形里的敌人；古龙姿当天第一次免费，友军寒冰 ×0.5；乘风步时龙翼无视地形。
    var dm=Add(S2Pawn("LP_Class_Monk",17,Subclass3.Dragon,17));ScSpawn(dm,ScCell(0,14));var hdm=Adventurer.Get(dm);Subclass3.Refresh(hdm);var foe12=Add(ScFoe(dm));foe12.apparel?.DestroyAll();
    S2Cast(dm,"LP_DM_Element",dm);var frost=Subclass3.DragonDamage(dm);float ki0=ClassSystem.Resource(hdm,ClassSystem.Monk);int left0b=Subclass3.BreathLeft(dm);float hp12=ClassMelee.InjurySum(foe12);
    bool br=S2Cast(dm,"LP_DM_Breath",new LocalTargetInfo(foe12.Position));float ki1=ClassSystem.Resource(hdm,ClassSystem.Monk);int left1b=Subclass3.BreathLeft(dm);bool burnt=ClassMelee.InjurySum(foe12)>hp12 || foe12.Dead || foe12.health.hediffSet.hediffs.Count>0 && Subclass3.Breath(dm,foe12.Position,1f)>0;
    var ally2=Add(S2Pawn("LP_Class_Fighter",1,null));ally2.SetFaction(dm.Faction);ScSpawn(ally2,NextTo(dm));bool asp=S2Cast(dm,"LP_DM_Aspect",dm);float ki2=ClassSystem.Resource(hdm,ClassSystem.Monk);float res=Subclass3.DamageFactor(ally2,Dmg(frost,null));
    Items.Give(dm,"LP_StepOfTheWind",600);Subclass3.Tick250(hdm);bool wing=Subclass3.Wingers.Contains(dm);
    Check(frost==(DefCache<DamageDef>.Get("LP_Frost")??DamageDefOf.Flame) && br && Near(ki0,ki1) && left1b==left0b-1 && left0b==6 && burnt && asp && Near(ki1,ki2) && Near(res,.5f) && wing && Items.Has(dm,"LP_DM_PresenceBuff"),"dragon way: element → frost, breath free ("+left0b+"→"+left1b+" left, ki "+ki0+"→"+ki1+") and hits the cone, aspect free with ally frost ×0.5, wings during step of the wind (burnt="+burnt+" asp="+asp+" res="+res+" wing="+wing+" ki2="+ki2+")");    // 17. 大地结社（1.109.0）：德鲁伊 2 级就能选；大地法术按气候生效；大地塑形永久改地形（这里改成沼泽，检查后还原）；动物伤不了。
    var dru=DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Druid");var ld=Add(S2Pawn("LP_Class_Druid",14,Subclass3.Land,14));ScSpawn(ld,ScCell(-14,-14));var hld=Adventurer.Get(ld);Subclass3.Refresh(hld);
    bool landAt2=ClassSubclass.CanPick(Adventurer.Get(Add(S2Pawn("LP_Class_Druid",2,null))),DefDatabase<LP_SubclassDef>.GetNamed(Subclass3.Land))==null;
    var foe13=Add(ScFoe(ld));int climate=Subclass3.Climate(map);int lsHit=Subclass3.LandSpell(ld,foe13.Position,1f);
    S2Cast(ld,"LP_LD_TerraMode",ld);S2Cast(ld,"LP_LD_TerraMode",ld);int tmode=Subclass3.TerraMode(ld);
    var tc=CellFinder.RandomClosewalkCellNear(ld.Position+new IntVec3(6,0,6),map,6,c=>GenRadial.RadialCellsAround(c,3f,true).All(x=>Subclass3.Terraformable(map,x)));
    var saved=GenRadial.RadialCellsAround(tc,3f,true).Where(x=>x.InBounds(map)).ToDictionary(x=>x,x=>x.GetTerrain(map));
    for(int k=0;k<7 && Subclass3.TerraMode(ld)<=1 || k<7 && tc.GetTerrain(map).defName==new[]{"WaterDeep","WaterShallow","Marsh","Mud","Soil","SoilRich","Sand"}[Subclass3.TerraMode(ld)];k++)S2Cast(ld,"LP_LD_TerraMode",ld);string want=new[]{"WaterDeep","WaterShallow","Marsh","Mud","Soil","SoilRich","Sand"}[Subclass3.TerraMode(ld)];int changed=Subclass3.Terraform(ld,tc,3f);bool marsh=tc.GetTerrain(map).defName==want;foreach(var kv in saved)map.terrainGrid.SetTerrain(kv.Key,kv.Value);
    var wolf=PawnGenerator.GeneratePawn(PawnKindDefOf.Thrumbo==null?DefDatabase<PawnKindDef>.GetNamed("Wolf_Timber"):DefDatabase<PawnKindDef>.GetNamed("Wolf_Timber"),null);made.Add(wolf);ScSpawn(wolf,NextTo(ld));wolf.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true);
    float bite=Subclass3.DamageFactor(ld,Dmg(DamageDefOf.Bite,wolf));Subclass3.Tick250(hld);
    Check(landAt2 && climate>=0 && climate<=3 && lsHit>=1 && tmode==2 && changed>0 && marsh && Near(bite,0f) && Subclass3.Wingers.Contains(ld) && Items.Has(ld,"LP_LD_StrideBuff"),"circle of the land: pickable at druid 2, land spell for climate "+Subclass3.Climates[climate]+" hits ("+lsHit+"), terraform made "+changed+" cells of "+want+" (restored), animals can't hurt, land stride");
    // 18. 孢子结社（1.109.0）：死亡光环伤到身边的敌人；腐烂之触让伤口不愈合；共生实体给护盾；尸体长成真菌傀儡；真菌之躯利器 ×0.75、不会恐慌。
    var sp=Add(S2Pawn("LP_Class_Druid",14,Subclass3.Spores,14));ScSpawn(sp,ScCell(14,-14));var hsp=Adventurer.Get(sp);Subclass3.Refresh(hsp);var foe14=Add(ScFoe(sp));
    S2Cast(sp,"LP_SP_Halo",sp);int halo=Subclass3.Halo(sp);bool rot=S2Cast(sp,"LP_SP_RotTouch",foe14) && (Items.Has(foe14,"LP_SP_Rotting") || foe14.Dead);
    S2Cast(sp,"LP_SP_Symbiotic",sp);bool spShield=Items.Has(sp,"LP_ShieldSpell") && Items.Has(sp,"LP_SP_SymbioticBuff");float sharp=Subclass3.DamageFactor(sp,Dmg(DamageDefOf.Cut,Add(ScFoe(sp))));
    var victim2=Add(ScFoe(sp));victim2.Kill(null);var puppet=Subclass3.Raise(sp,victim2);
    bool fear=sp.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,null,true);
    Check(halo>=1 && rot && spShield && Near(sharp,.75f) && puppet!=null && !puppet.Dead && puppet.Faction==sp.Faction && Items.Has(puppet,"LP_SP_Puppet") && !fear && Subclass3.HaloOn(sp),"circle of spores: halo hits "+halo+", rot touch, symbiotic shield, fungal body sharp ×"+sharp+", a corpse rises as a fungal puppet, no panic");
    // 19. 群星结社（1.109.0）：指引箭免费次数内不耗法力、照亮目标（下一下 ×1.25）；星座切到圣杯；全星辉形态中受伤 ×0.5 并无视地形；宇宙预兆醒着就抽。
    var sr=Add(S2Pawn("LP_Class_Druid",14,Subclass3.Stars,14));ScSpawn(sr,ScCell(0,-16));var hst=Adventurer.Get(sr);Subclass3.Refresh(hst);var foe15=Add(ScFoe(sr));
    float sm0b=ClassSystem.Resource(hst,dru);int sl0=Subclass3.StarBoltLeft(sr);bool bolt=S2Cast(sr,"LP_ST_StarBolt",foe15);float sm1b=ClassSystem.Resource(hst,dru);int sl1=Subclass3.StarBoltLeft(sr);
    bool lit=Items.Has(foe15,"LP_ST_Guided") || foe15.Dead;float guided=foe15.Dead?1.25f:Subclass3.DamageFactor(foe15,Dmg(DamageDefOf.Cut,sr));
    S2Cast(sr,"LP_ST_FormMode",sr);S2Cast(sr,"LP_ST_StarryForm",sr);var form=sr.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ST_Form"));float stars=Subclass3.DamageFactor(sr,Dmg(DamageDefOf.Cut,Add(ScFoe(sr))));
    Subclass3.Tick250(hst);bool omen=ClassSubclass.Data(sr).ticks.ContainsKey("omen");
    Check(bolt && Near(sm0b,sm1b) && sl1==sl0-1 && lit && Near(guided,1.25f) && form!=null && Near(form.Severity,2f) && Near(stars,.5f) && Subclass3.Wingers.Contains(sr) && omen,"circle of stars: guiding bolt free ("+sl0+"→"+sl1+") and marks the foe (×"+guided+"), chalice form, full of stars ×"+stars+" and glides, omen drawn");    // 20. 生命领域（1.110.0）：牧师 1 级就能选；治疗 ×1.95（门徒 + 至高）；引导神力 17 级每天 2 次，用完拒绝；神圣打击每 2 秒加一次。
    var cle=DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Cleric");bool lifeAt1=ClassSubclass.CanPick(Adventurer.Get(Add(S2Pawn("LP_Class_Cleric",1,null))),DefDatabase<LP_SubclassDef>.GetNamed(Subclass3.Life))==null;
    var lf=Add(S2Pawn("LP_Class_Cleric",17,Subclass3.Life,17));ScSpawn(lf,ScCell(-16,16));var hlf=Adventurer.Get(lf);Subclass3.Refresh(hlf);
    float heal=Subclass3.PowerFactor(lf,Fx("LP_CureWounds"));int ch0=Subclass3.ChannelLeft(lf);S2Cast(lf,"LP_LF_Preserve",lf);S2Cast(lf,"LP_LF_Preserve",lf);string chWhy=ClassSystem.CannotCast(lf,Ab("LP_LF_Preserve"),Fx("LP_LF_Preserve"));
    var foe16=Add(ScFoe(lf));Subclass3.DamageFactor(foe16,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,lf,null,DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife")));bool dStrike=ClassSubclass.Data(lf).ticks.ContainsKey("dsT");Subclass3.Tick250(hlf);
    Check(lifeAt1 && Near(heal,1.95f) && ch0==2 && chWhy!=null && dStrike && Items.Has(lf,"LP_LF_DiscipleBuff"),"life domain: pickable at cleric 1, heals ×"+heal+", channel 2/day then refused ("+chWhy+"), divine strike on a weapon hit ("+lifeAt1+" "+dStrike+")");
    // 21. 光明领域（1.110.0）：守护之光让一下落空并用掉一次；光亮术点亮一格；破晓之光让隐形的敌人现形；晨光之冕有引导神力时不耗神恩。
    var lt=Add(S2Pawn("LP_Class_Cleric",17,Subclass3.Light,17));ScSpawn(lt,ScCell(16,16));var hlt=Adventurer.Get(lt);Subclass3.Refresh(hlt);var foe17=Add(ScFoe(lt));
    int fl0=Subclass3.FlareLeft(lt);float flare;ClassMartial2.RollOverride=0f;try{flare=Subclass3.DamageFactor(lt,Dmg(DamageDefOf.Cut,foe17));}finally{ClassMartial2.RollOverride=null;}int fl1=Subclass3.FlareLeft(lt);
    var orb=Subclass3.LightOrb(lt,ScCell(16,22));if(orb!=null)made.Add(orb);
    Items.Give(foe17,"LP_ATInvisible",2500);S2Cast(lt,"LP_LT_Dawn",lt);bool seen=!Items.Has(foe17,"LP_ATInvisible") && (Items.Has(foe17,"LP_Spotted") || foe17.Dead);
    float g0=ClassSystem.Resource(hlt,cle);S2Cast(lt,"LP_LT_Corona",lt);float g1=ClassSystem.Resource(hlt,cle);
    Check(Near(flare,0f) && fl1==fl0-1 && orb!=null && orb.TryGetComp<CompGlower>()!=null && seen && Near(g0,g1) && Items.Has(lt,"LP_LT_CoronaOn"),"light domain: warding flare ("+fl0+"→"+fl1+"), light orb glows, dawn reveals the invisible, corona free with channel left ("+g0+"→"+g1+")");
    // 22. 熔炉领域（1.110.0）：熔炉祝福让长剑 +1、同一件不能再祝福；工匠祝福用 50 钢变出优秀长剑；熔炉之魂火焰 ×0.5，熔炉圣徒免疫火焰。
    var fg=Add(S2Pawn("LP_Class_Cleric",17,Subclass3.Forge,17));fg.SetFaction(Faction.OfPlayer);ScSpawn(fg,ScCell(0,20));var hfg=Adventurer.Get(fg);Subclass3.Refresh(hfg);
    var blade=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_LongSword"),ThingDefOf.Steel);fg.equipment.AddEquipment(blade);int lv0=GearEnhance.Level(blade);
    string b1=Subclass3.ForgeBless(fg);int lv1=GearEnhance.Level(blade);string b2=Subclass3.ForgeBless(fg);
    var st2=ThingMaker.MakeThing(ThingDefOf.Steel);st2.stackCount=60;GenPlace.TryPlaceThing(st2,fg.Position+IntVec3.West*2,map,ThingPlaceMode.Near);made.Add(st2);Subclass3.Tick250(hfg);var made2=Subclass3.Artisan(fg,0);if(made2!=null)made.Add(made2);
    float fire=Subclass3.DamageFactor(fg,Dmg(DamageDefOf.Flame,Add(ScFoe(fg))));
    Check(lv1==lv0+1 && b2!=null && b2.Contains("已经") && made2!=null && made2.def.defName=="MeleeWeapon_LongSword" && made2.TryGetComp<CompQuality>()?.Quality==QualityCategory.Excellent && Near(fire,0f) && Items.Has(fg,"LP_FG_CraftBuff"),"forge domain: blessing +1 ("+lv0+"→"+lv1+"), not twice ("+b2+"), artisan makes an excellent longsword, forge saint immune to fire");   }finally{
    ClassMartial2.RollOverride=null;
    foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn pp && pp.Dead){pp.Corpse?.Destroy();continue;}t.Destroy();}
    foreach(var c in map.listerThings.ThingsOfDef(ThingDefOf.Steel).Where(x=>x.stackCount==Subclass2.RepairSteel).ToList())c.Destroy();
   }
  }
 }
}
