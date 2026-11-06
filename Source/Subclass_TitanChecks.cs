using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.115.0 战士子职业：泰坦战士（Test.ps1 -Only TitanWarrior，8 条）。
 public partial class LichChecks {
  void CheckTitanWarrior(){
   var made=new List<Thing>();
   Pawn Add(Pawn x){made.Add(x);return x;}
   try{
    // 1. 定义：战士第三个子职业，6 个能力（3/7/11/15）有图标并算战士；徽记不是职业底图。
    var sub=DefDatabase<LP_SubclassDef>.GetNamed(TitanWarrior.Sub);var abs=sub.abilities;
    Check(sub.parent.defName=="LP_Class_Fighter" && sub.Emblem!=null && sub.Emblem!=sub.parent.Background && abs.Count==6 && abs.Select(e=>e.level).Distinct().OrderBy(x=>x).SequenceEqual(new[]{3,7,11,15})
     && abs.All(e=>e.ability.abilityClass==typeof(Ability_Class) && e.ability.uiIcon!=null && e.ability.uiIcon!=BaseContent.BadTex && ClassSystem.ClassOf(e.ability)==sub.parent)
     && ClassSubclass.Options(sub.parent).Count()>=3 && sub.order==3/* 1.120.0 起战士还有第 4 个子职业（奥术射手） */,"titan warrior: 3rd fighter subclass, 6 abilities (3/7/11/15) with icons, fighter-owned, own emblem");
    // 2. 战士 20：体型 ×1.5、绘制 ×√1.5、部位生命 ×1.6、HealthScale 不变；搬运 ×1.4；只在缓存里重算。
    var t=Add(S2Pawn("LP_Class_Fighter",20,TitanWarrior.Sub,20));t.SetFaction(Faction.OfPlayer);ScSpawn(t,ScCell(-12,4));var ht=Adventurer.Get(t);TitanWarrior.Refresh(ht);
    var torso=t.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso);int id=t.thingIDNumber;var ent=TitanWarrior.Of(t);
    float s1=t.BodySize,m1=PartHealth.Maximum(torso,t),r1=RootScale(t),hs1=t.HealthScale;
    TitanWarrior.Users.Remove(id);float s0=t.BodySize,m0=PartHealth.Maximum(torso,t),r0=RootScale(t),hs0=t.HealthScale;TitanWarrior.Users[id]=ent;
    var body=t.health.hediffSet.hediffs.OfType<Hediff_TitanBody>().FirstOrDefault();float carry=body?.CurStage?.statFactors?.FirstOrDefault(m=>m.stat==StatDefOf.CarryingCapacity)?.value??0f;
    bool same=TitanWarrior.Refresh(ht)!=0 && TitanWarrior.Of(t)==ent;
    Check(ent!=null && Near(s1/s0,1.5f) && Near(r1/r0,Mathf.Sqrt(1.5f)) && Math.Abs(m1/m0-1.6f)<.05f && Near(hs1,hs0) && Near(carry,1.4f) && same,
     "titan 20: size ×"+(s1/s0)+", render ×"+(r1/r0)+", torso "+m0+"→"+m1+", HealthScale "+hs0+"→"+hs1+", carry ×"+carry+", cache reused");
    // 3. 体型公式：战士 20 后传奇等级接着长（30 级 ×1.75、40 级 ×2.0 封顶）；战士没满 20 不吃冒险者等级。
    Check(Near(TitanWarrior.SizeFactor(20,20),1.5f) && Near(TitanWarrior.SizeFactor(20,30),1.75f) && Near(TitanWarrior.SizeFactor(20,40),2f) && Near(TitanWarrior.SizeFactor(20,60),2f) && Near(TitanWarrior.SizeFactor(10,30),1.25f) && Near(TitanWarrior.SizeFactor(3,3),1.075f),
     "titan size: 1.5 at 20, legendary 30 → 1.75, cap 2.0, below fighter 20 adventurer level ignored");
    // 4. 巨力之握：打比自己小的敌人 ×1.15 并击退 1 格（成功率按 0 掷）；打友军不加成。
    var foe=Add(ScFoe(t));var ally=Add(S2Pawn("LP_Class_Fighter",1,null));ally.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(ally,NextTo(t),map);
    ClassMartial2.RollOverride=0.5f;float hitF=TitanWarrior.HitFactor(foe,Dmg(DamageDefOf.Cut,t)),hitA=TitanWarrior.HitFactor(ally,Dmg(DamageDefOf.Cut,t));
    var fp0=foe.Position;ClassMartial2.RollOverride=0f;TitanWarrior.HitFactor(foe,Dmg(DamageDefOf.Cut,t));ClassMartial2.RollOverride=null;Items.State?.Flush();var fp1=foe.Position;
    Check(Near(hitF,1.15f) && Near(hitA,1f) && fp1!=fp0,"mighty grip: smaller foe ×"+hitF+", ally ×"+hitA+", knockback "+fp0+"→"+fp1);
    // 5. 撼地践踏：耗斗志 2（打中敌人回的斗志另算），敌人受伤并被震倒（成功率按 0 掷），友军不伤。
    foreach(var x in new[]{foe,ally})if(!x.Position.AdjacentTo8WayOrInside(t.Position))x.Position=NextTo(t);
    var fighter=sub.parent;float res0=ClassSystem.Resource(ht,fighter),fh0=ClassMelee.InjurySum(foe),ah0=ClassMelee.InjurySum(ally);
    ClassMartial2.RollOverride=0f;bool cast;try{cast=S2Cast(t,"LP_TT_Stomp",t);}finally{ClassMartial2.RollOverride=null;}
    float res1=ClassSystem.Resource(ht,fighter);
    Check(cast && res0-res1>1.5f && res0-res1<=2.001f && ClassMelee.InjurySum(foe)>fh0 && foe.stances.stunner.Stunned && Near(ClassMelee.InjurySum(ally),ah0) && Near(TitanWarrior.StunChance(1.5f,1f),.75f) && Near(TitanWarrior.StunChance(3f,1f),.95f),
     "stomp: cost "+(res0-res1)+", foe hurt "+fh0+"→"+ClassMelee.InjurySum(foe)+" and stunned, ally untouched, chance 60%+30%×diff capped 95%");
    // 6. 不动如山：推不动、眩晕减半、疼痛休克阈值 +10%。
    var tp=t.Position;DNDPush.Move(t,NextTo(t));t.stances.stunner.StunFor(120,null,false);int stun=t.stances.stunner.StunTicksLeft;
    float pain=body?.CurStage?.statOffsets?.FirstOrDefault(m=>m.stat==StatDefOf.PainShockThreshold)?.value??0f;
    Check(t.Position==tp && stun==60 && Near(pain,.1f),"immovable: no knockback, stun 120→"+stun+", pain shock +"+pain);
    // 7. 泰坦化（战士 11，没有远古泰坦）：体型、部位生命 ×1.25、近战 ×1.25；结束后休息 −20%、体型复原。
    var t11=Add(S2Pawn("LP_Class_Fighter",11,TitanWarrior.Sub,11));t11.SetFaction(Faction.OfPlayer);ScSpawn(t11,ScCell(12,4));var h11=Adventurer.Get(t11);TitanWarrior.Refresh(h11);
    var torso11=t11.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso);float a0=t11.BodySize,am0=PartHealth.Maximum(torso11,t11);
    bool tz=S2Cast(t11,"LP_TT_Titanize",t11);float a1=t11.BodySize,am1=PartHealth.Maximum(torso11,t11);int left=Items.TicksLeft(t11,"LP_TT_Titanized");
    t11.needs.rest.CurLevel=.8f;Items.Remove(t11,"LP_TT_Titanized");float rest=t11.needs.rest.CurLevel,a2=t11.BodySize;
    Check(tz && Near(a1/a0,1.25f) && Math.Abs(am1/am0-1.25f)<.05f && left>3000 && left<=3600 && Near(rest,.6f) && Near(a2,a0),"titanize: size ×"+(a1/a0)+", torso "+am0+"→"+am1+", 60 s ("+left+"), fatigue rest→"+rest+", size back "+a2);
    // 8. 远古泰坦（战士 20）：泰坦化 120 秒、不疲劳；近战命中 20% 小型践踏（按 0 掷，不耗斗志）；武器矩阵随绘制倍率放大。
    ClassSystem.AddResource(ht,fighter,99f);bool tz2=S2Cast(t,"LP_TT_Titanize",t);int left2=Items.TicksLeft(t,"LP_TT_Titanized");float res2=ClassSystem.Resource(ht,fighter);
    if(!foe.Position.AdjacentTo8WayOrInside(t.Position))foe.Position=NextTo(t);float fh1=ClassMelee.InjurySum(foe);
    ClassMartial2.RollOverride=0f;try{TitanWarrior.HitFactor(foe,Dmg(DamageDefOf.Cut,t));Items.State?.Flush();}finally{ClassMartial2.RollOverride=null;}
    float fh2=ClassMelee.InjurySum(foe),res3=ClassSystem.Resource(ht,fighter);t.needs.rest.CurLevel=.8f;Items.Remove(t,"LP_TT_Titanized");float rest2=t.needs.rest.CurLevel;
    TitanWarrior.WeaponScale=1.5f;float wsc=TitanWarrior.WeaponTRS(Vector3.zero,Quaternion.identity,new Vector3(1f,0f,1f)).lossyScale.x;TitanWarrior.WeaponScale=1f;
    var info=Harmony.GetPatchInfo(AccessTools.Method(typeof(PawnRenderUtility),nameof(PawnRenderUtility.DrawEquipmentAiming)));bool wpatched=info!=null && info.Transpilers.Any(x=>x.PatchMethod.DeclaringType==typeof(TitanWarriorWeaponMatrix));
    Check(tz2 && left2>6000 && fh2>fh1 && res3>=res2-.001f && Near(rest2,.8f) && Near(wsc,1.5f) && wpatched,"ancient titan: titanize "+left2+" ticks, mini stomp on hit "+fh1+"→"+fh2+" free, no fatigue ("+rest2+"), weapon ×"+wsc+(wpatched?"":"（武器补丁没挂上）"));
   }finally{
    ClassMartial2.RollOverride=null;TitanWarrior.WeaponScale=1f;
    foreach(var x in made)if(x!=null && !x.Destroyed){if(x is Pawn pp){TitanWarrior.Users.Remove(pp.thingIDNumber);if(pp.Dead){pp.Corpse?.Destroy();continue;}}x.Destroy();}
   }
  }
 }
}
