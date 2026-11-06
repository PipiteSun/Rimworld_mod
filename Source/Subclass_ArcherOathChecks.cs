using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.120.0 战士子职业「奥术射手」（Test.ps1 -Only ArcaneArcher）、圣武士子职业「破誓者」（Test.ps1 -Only Oathbreaker）。
 public partial class LichChecks {
  static bool SubDefOk(LP_SubclassDef sub,string cls,int n,int[] levels)=>sub.parent.defName==cls && sub.Emblem!=null && sub.Emblem!=sub.parent.Background && sub.abilities.Count==n
   && sub.abilities.Select(e=>e.level).Distinct().OrderBy(x=>x).SequenceEqual(levels)
   && sub.abilities.All(e=>e.ability.abilityClass==typeof(Ability_Class) && e.ability.uiIcon!=null && e.ability.uiIcon!=BaseContent.BadTex && ClassSystem.ClassOf(e.ability)==sub.parent)
   && ClassSubclass.Options(sub.parent).Contains(sub);
  void AAArm(Pawn p,string gun){p.equipment.DestroyAllEquipment();p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(gun)));}
  // 打出奥术射击后找到这一发登记的子弹，当场结算命中（不等子弹飞），再把子弹删掉。
  bool CastAndLand(Pawn p,string ab,Pawn t){
   var before=new HashSet<Projectile>(Martial.Shots.Keys);bool ok=S2Cast(p,ab,t);
   var proj=Martial.Shots.Keys.FirstOrDefault(x=>!before.Contains(x) && Martial.Shots[x].caster==p);if(proj==null)return false;
   Martial.Impacted(proj,t);if(!proj.Destroyed)proj.Destroy();return ok;
  }
  void CheckArcaneArcher(){
   var made=new List<Thing>();
   Pawn Add(Pawn x){made.Add(x);return x;}
   try{
    // 1. 定义：战士第 4 个子职业，6 个能力（3/7/11/15）有图标并算战士；徽记独立。
    var sub=DefDatabase<LP_SubclassDef>.GetNamed(ArcaneArcher.Sub);
    Check(SubDefOk(sub,"LP_Class_Fighter",6,new[]{3,7,11,15}) && sub.order==4 && ClassSubclass.Options(sub.parent).Count()>=4,"arcane archer: 4th fighter subclass, 6 abilities (3/7/11/15) with icons, fighter-owned, own emblem");
    // 2. 被动：战士 20 拿步枪，近处射击 ×1.3，12 格外再 ×1.15；近战、友军不加成。
    var a=Add(S2Pawn("LP_Class_Fighter",20,ArcaneArcher.Sub,20));a.SetFaction(Faction.OfPlayer);ScSpawn(a,ScCell(-14,-8));AAArm(a,"Gun_AssaultRifle");var ha=Adventurer.Get(a);ArcaneArcher.Refresh(ha);
    var rifle=a.equipment.Primary.def;var foe=Add(ScFoe(a));
    DamageInfo Shot()=>new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,a,null,rifle);
    float near=ArcaneArcher.HitFactor(foe,Shot()),melee=ArcaneArcher.HitFactor(foe,Dmg(DamageDefOf.Cut,a));
    var far=Add(AdvPawn(null,AdvEnemy));var farCell=CellFinder.RandomClosewalkCellNear(a.Position,map,20,c=>c.DistanceTo(a.Position)>=13f && ScFree(c));GenSpawn.Spawn(far,farCell,map);
    float farF=ArcaneArcher.HitFactor(far,Shot());
    Check(ArcaneArcher.Of(a)!=null && Near(near,1.3f) && Near(farF,1.3f*1.15f) && Near(melee,1f),"arcane lore / keen eye: ranged ×"+near+", 12+ cells ×"+farF+", melee ×"+melee);
    // 3. 束缚箭：扣斗志 1，必中的一发登记了命中效果；命中后划伤 + 束缚荆棘（移动 ×0.5）。
    var fighter=sub.parent;ClassSystem.AddResource(ha,fighter,99f);float res0=ClassSystem.Resource(ha,fighter),h0=ClassMelee.InjurySum(foe),mv0=foe.GetStatValue(StatDefOf.MoveSpeed);
    bool g=CastAndLand(a,"LP_AA_GraspingArrow",foe);float res1=ClassSystem.Resource(ha,fighter),mv1=foe.GetStatValue(StatDefOf.MoveSpeed);
    Check(g && res0-res1>.5f && res0-res1<=1.001f /* 打中敌人回的斗志另算 */ && Items.Has(foe,"LP_AA_Grasped") && ClassMelee.InjurySum(foe)>h0 && mv1<mv0*.6f,"grasping arrow: cost "+(res0-res1)+", hurt "+h0+"→"+ClassMelee.InjurySum(foe)+", move "+mv0.ToString("0.00")+"→"+mv1.ToString("0.00"));
    // 4. 爆裂箭：命中点半径 2 内的敌人都受伤，友军不伤。
    var bt=Add(ScFoe(a));var foe2=Add(AdvPawn(null,AdvEnemy));GenSpawn.Spawn(foe2,NextTo(bt),map);var ally=Add(S2Pawn("LP_Class_Fighter",1,null));ally.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(ally,NextTo(bt),map);
    foreach(var x in new[]{bt,foe2,ally})x.apparel?.DestroyAll(); // 护甲可能把力场伤害整个挡掉
    float f1=ClassMelee.InjurySum(bt),f2=ClassMelee.InjurySum(foe2),al=ClassMelee.InjurySum(ally);ArcaneArcher.LastBurst=-1;bool b=CastAndLand(a,"LP_AA_BurstingArrow",bt);
    Check(b && (bt.Dead || ClassMelee.InjurySum(bt)>f1) && (foe2.Dead || ClassMelee.InjurySum(foe2)>f2) && Near(ClassMelee.InjurySum(ally),al),"bursting arrow: target "+f1+"→"+ClassMelee.InjurySum(bt)+", neighbour "+f2+"→"+ClassMelee.InjurySum(foe2)+" (burst hit "+ArcaneArcher.LastBurst+", dist "+foe2.Position.DistanceTo(bt.Position).ToString("0.0")+", downed "+foe2.Downed+", hostile "+foe2.HostileTo(a)+", dead "+foe2.Dead+"), ally untouched");
    // 5. 暗影箭：心灵伤害 + 视觉下降。
    var foe3=Add(ScFoe(a));float s0=foe3.health.capacities.GetLevel(PawnCapacityDefOf.Sight);bool sh=CastAndLand(a,"LP_AA_ShadowArrow",foe3);foe3.health.capacities.Notify_CapacityLevelsDirty();float s1=foe3.health.capacities.GetLevel(PawnCapacityDefOf.Sight);
    Check(sh && Items.Has(foe3,"LP_AA_Shadowed") && s1<s0-.3f,"shadow arrow: sight "+s0.ToStringPercent()+"→"+s1.ToStringPercent());
    // 6. 箭雨：半径 4 内每个没倒地的敌人各一发；没有远程武器时不能施放。
    var center=foe.Position;int expect=Races124.PawnsNear(map,center,ArcaneArcher.StormRadius).Count(x=>x.HostileTo(a) && !x.Downed);
    var before=new HashSet<Projectile>(Martial.Shots.Keys);int n=ArcaneArcher.Storm(a,center);
    foreach(var pr in Martial.Shots.Keys.Where(x=>!before.Contains(x)).ToList()){Martial.Shots.Remove(pr);if(!pr.Destroyed)pr.Destroy();}
    a.equipment.DestroyAllEquipment();string why=ClassEffects.Invalid(a,Fx("LP_AA_GraspingArrow"),foe);
    Check(expect>=2 && n==expect && why!=null && why.Contains("远程武器"),"arrow storm: "+n+"/"+expect+" shots; without a gun: "+why);
   }finally{
    foreach(var x in made)if(x!=null && !x.Destroyed){if(x is Pawn pp){ArcaneArcher.Users.Remove(pp.thingIDNumber);if(pp.Dead){pp.Corpse?.Destroy();continue;}}x.Destroy();}
   }
  }

  void CheckOathbreaker(){
   var made=new List<Thing>();
   Pawn Add(Pawn x){made.Add(x);return x;}
   try{
    // 1. 定义：圣武士第 3 个子职业，5 个能力（3/7/11/15）；两个 3 级能力走誓言之力。
    var sub=DefDatabase<LP_SubclassDef>.GetNamed(Oathbreaker.Sub);
    Check(SubDefOk(sub,"LP_Class_Paladin",5,new[]{3,7,11,15}) && sub.order==3 && Subclass2.Oath(Ab("LP_OB_DreadfulAspect")) && Subclass2.Oath(Ab("LP_OB_ControlUndead")) && !Subclass2.Oath(Ab("LP_OB_DreadLord")),
     "oathbreaker: 3rd paladin subclass, 5 abilities (3/7/11/15) with icons, two oath-power abilities");
    // 2. 恐惧面容：扣 1 次誓言之力（15 级 3 次），身边敌人被震慑逃跑（成功率按 0 掷）；用完后不能再施。
    var o=Add(S2Pawn("LP_Class_Paladin",15,Oathbreaker.Sub,15));o.SetFaction(Faction.OfPlayer);ScSpawn(o,ScCell(14,-8));var ho=Adventurer.Get(o);Oathbreaker.Refresh(ho);
    var foe=Add(ScFoe(o));int left0=Subclass2.OathLeft(o);
    ClassMartial2.RollOverride=0f;bool asp;try{asp=S2Cast(o,"LP_OB_DreadfulAspect",o);}finally{ClassMartial2.RollOverride=null;}
    int left1=Subclass2.OathLeft(o);var d=ClassSubclass.Data(o);if(d!=null)d.oath=0;string empty=ClassSystem.CannotCast(o,Ab("LP_OB_DreadfulAspect"),Fx("LP_OB_DreadfulAspect"));if(d!=null)d.oath=3;
    Check(asp && left0==3 && left1==2 && Items.Has(foe,"LP_Shaken") && empty!=null,"dreadful aspect: oath "+left0+"→"+left1+", foe shaken, blocked when spent ("+empty+")");
    // 3. 操控亡者：只能选亡者或邪魔；成功后加入你的阵营（到期表登记）。
    var human=Add(ScFoe(o));string inv=ClassEffects.Invalid(o,Fx("LP_OB_ControlUndead"),human);
    var gc=Current.Game.GetComponent<GameComponent_Subclass2>();int c0=gc.Count;ClassMartial2.RollOverride=0f;bool ctl;try{ctl=Oathbreaker.Control(o,human);}finally{ClassMartial2.RollOverride=null;}
    Check(inv!=null && inv.Contains("亡者") && ctl && human.Faction==o.Faction && gc.Count==c0+1,"control undead: humans rejected ("+inv+"), control turns the target and registers expiry");
    gc.ExpireNow();
    // 4. 憎恨灵光：自己近战 ×(1.1 + 魅力调整值 × 0.02)，远程不加成；身边同阵营的普通人不加成。
    var foe2=Add(ScFoe(o));float hate=Oathbreaker.DamageFactor(foe2,Dmg(DamageDefOf.Cut,o)),expect=Oathbreaker.HateFactor(o);
    var mate=Add(S2Pawn("LP_Class_Fighter",1,null));mate.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(mate,NextTo(o),map);float mateF=Oathbreaker.DamageFactor(foe2,Dmg(DamageDefOf.Cut,mate));
    Check(expect>=1.1f-.001f && Near(hate,expect) && Near(mateF,1f) && Oathbreaker.Haters.Contains(o),"aura of hate: own melee ×"+hate+" (expected "+expect+"), living ally ×"+mateF);
    // 5. 超凡抗性：利器、钝器 ×0.75，火焰不减。
    float cut=Oathbreaker.DamageFactor(o,Dmg(DamageDefOf.Cut,foe2)),blunt=Oathbreaker.DamageFactor(o,Dmg(DamageDefOf.Blunt,foe2)),burn=Oathbreaker.DamageFactor(o,Dmg(DamageDefOf.Burn,foe2));
    Check(Near(cut,.75f) && Near(blunt,.75f) && Near(burn,1f),"supernatural resistance: cut ×"+cut+", blunt ×"+blunt+", burn ×"+burn);
    // 6. 恐惧之王：60 秒，每次脉冲伤害身边敌人，受到的远程伤害 ×0.5；结束后从名单移除。
    foreach(var x in new[]{foe,foe2})if(x.Spawned && !x.Position.InHorDistOf(o.Position,5f))x.Position=NextTo(o);
    bool lord=S2Cast(o,"LP_OB_DreadLord",o);int lt=Items.TicksLeft(o,"LP_OB_DreadLord");float fh=ClassMelee.InjurySum(foe2);int hit=Oathbreaker.LordPulse(o);
    float ranged=Oathbreaker.DamageFactor(o,new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,foe2,null,DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle")));
    Items.Remove(o,"LP_OB_DreadLord");Oathbreaker.LordPulse(o);
    Check(lord && lt>3000 && lt<=3600 && hit>=1 && ClassMelee.InjurySum(foe2)>fh && Near(ranged,.5f*.75f) && !Oathbreaker.Lords.Contains(o),"dread lord: "+lt+" ticks, pulse hit "+hit+", ranged ×"+ranged+" (with resistance), cleared after");
   }finally{
    ClassMartial2.RollOverride=null;
    foreach(var x in made)if(x!=null && !x.Destroyed){if(x is Pawn pp){Oathbreaker.Users.Remove(pp.thingIDNumber);Oathbreaker.Haters.Remove(pp);Oathbreaker.Lords.Remove(pp);if(pp.Dead){pp.Corpse?.Destroy();continue;}}x.Destroy();}
   }
  }
 }
}
