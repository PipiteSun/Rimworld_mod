using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.72.0 职业能力：原版 RimWorld.Ability（Defs/Classes.xml），效果从 VPE 桥的 ClassAbility 子类移来，数值不变。
 // 强度 Strength = 职业强度属性（bestSkill 取战技 / 射术较高者）× 心灵系数（psychic 能力、设置开着时）；
 // Power = power × Strength；Duration = duration ×（durationScales 时 × 属性）；Radius 同理。施放先扣资源（Ability_Class.Activate）。
 public class CompProperties_ClassEffect : CompProperties_AbilityEffect {
  public string effect,hediff;public int cost,duration;public float power,radius,range,maxRange;public StatDef stat;
  public bool bestSkill,psychic,passive,ranged,durationScales,radiusScales;
  public bool sorcery,single,fixedSeverity; // 1.73.0：术士法术（强效、孪生、魔涌）；单体法术（孪生可以多打一个）；状态严重度固定 1
  public CompProperties_ClassEffect(){compClass=typeof(CompAbilityEffect_Class);}
 }
 public class Ability_Class : RimWorld.Ability {
  public Ability_Class():base(){}
  public Ability_Class(Pawn pawn):base(pawn){}
  public Ability_Class(Pawn pawn,AbilityDef def):base(pawn,def){}
  public Ability_Class(Pawn pawn,Precept sourcePrecept):base(pawn,sourcePrecept){}
  public Ability_Class(Pawn pawn,Precept sourcePrecept,AbilityDef def):base(pawn,sourcePrecept,def){}
  CompProperties_ClassEffect props;
  public CompProperties_ClassEffect Props=>props??(props=def.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault());
  public override IEnumerable<Command> GetGizmos()=>Props?.passive==true?Enumerable.Empty<Command>():base.GetGizmos(); // 被动：没有按钮
  public override bool GizmoDisabled(out string reason){
   if(base.GizmoDisabled(out reason))return true;
   reason=ClassSystem.CannotCast(pawn,def,Props);return reason!=null;
  }
  public override AcceptanceReport CanCast{get{var b=base.CanCast;if(!b.Accepted)return b;var r=ClassSystem.CannotCast(pawn,def,Props);return r==null?b:(AcceptanceReport)r;}}
  public override bool Activate(LocalTargetInfo target,LocalTargetInfo dest){
   if(ClassSystem.CannotCast(pawn,def,Props)!=null)return false;
   ClassSystem.Pay(pawn,def,Props);
   bool ok=base.Activate(target,dest);
   if(ok){ClassCasters.AfterCast(this);ClassLegendary.AfterCast(this);ClassSubclass.AfterCast(this);}
   return ok;
  }
  public override string Tooltip{get{
   var p=Props;var c=ClassSystem.ClassOf(def);string s=base.Tooltip;
   if(p==null || c==null)return s;
   if(ClassSubclass.UsesMana(def) && !p.passive)return s+"\n\n消耗：法力 "+ClassLegendary.Cost(pawn,def,p)+"（现有 "+ClassSubclass.Mana(pawn).ToString("0.#")+"）\n强度："+ClassEffects.Strength(pawn,p).ToStringPercent(); // 诡术师
   return s+"\n\n"+(p.passive?"被动":c.resource==ClassResource.None || p.cost<=0?"不消耗资源（只看冷却）":"消耗："+ClassSystem.ResourceLabel(c.resource)+" "+ClassLegendary.Cost(pawn,def,p)+"（现有 "+ClassSystem.Resource(pawn,c).ToString("0.#")+"）"+(ClassSystem.FreeCast(pawn,p)?"，魔涌中免费":""))+"\n强度："+ClassEffects.Strength(pawn,p).ToStringPercent();
  }}
 }
 public class CompAbilityEffect_Class : CompAbilityEffect {
  public new CompProperties_ClassEffect Props=>(CompProperties_ClassEffect)props;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){base.Apply(target,dest);ClassEffects.Apply(parent.pawn,parent.def,Props,target);}
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   if(!base.Valid(target,throwMessages))return false;
   var why=ClassEffects.Invalid(parent.pawn,Props,target);
   if(why!=null){if(throwMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
 }

 public static class ClassEffects {
  public static readonly Color MartialColor=new Color(.95f,.6f,.4f);
  public static float Strength(Pawn p,CompProperties_ClassEffect e)=>ClassSystem.Strength(p,e);
  public static float Power(Pawn p,CompProperties_ClassEffect e)=>e.power*Strength(p,e)*(e.sorcery && Items.Has(p,"LP_Empowered")?1.5f:1f)*ClassLegendary.PowerFactor(p,e)*Warlock.PowerFactor(p,e); // 1.73.0 超魔·强效；1.74.0 法术强效 ×1.3；邪术师（宗主化身、邪魔 + 契约）
  public static int Duration(Pawn p,CompProperties_ClassEffect e)=>Mathf.RoundToInt(e.duration*(e.durationScales?ClassSystem.StatValue(p,e):1f));
  public static float Radius(Pawn p,CompProperties_ClassEffect e)=>e.radius*(e.radiusScales?ClassSystem.StatValue(p,e):1f);
  static bool Hostile(Pawn caster,Pawn t)=>t!=null && t!=caster && !t.Dead && t.HostileTo(caster);
  static bool Friendly(Pawn caster,Pawn t)=>t!=null && !t.Dead && !t.HostileTo(caster);
  static IEnumerable<Pawn> Near(Pawn p,IntVec3 c,float r)=>p.Map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(p.Map,c,r);
  static Hediff Give(Pawn t,string name,int ticks,float severity)=>Items.Give(t,name,ticks,severity);

  // 选目标时的检查；返回 null 表示可以。
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;
   switch(e.effect){
    case "Charge":case "Trip":case "DisarmingStrike":case "Flurry":case "StunningStrike":case "OpenHand":case "QuiveringPalm":case "Vow":case "PrecisionShot":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";
     if(!t.HostileTo(caster))return "只能对敌人使用。";
     break;
    case "LayOnHands":case "Intercept":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";
     if(t.HostileTo(caster))return "只能对友军使用。";
     break;
   }
   var cw=ClassCasters.Invalid(caster,e,target)??ClassLegendary.Invalid(caster,e,target)??ClassMartial2.Invalid(caster,e,target)??ClassSubclass.Invalid(caster,e,target)??Warlock.Invalid(caster,e,target);if(cw!=null)return cw; // 1.73.0 施法职业；1.74.0 传奇；新职业；子职业；邪术师
   if(e.ranged){var v=Martial.RangedVerb(caster);if(v?.Projectile==null)return "需要远程武器。";if(target.Cell.IsValid && caster.Position.DistanceTo(target.Cell)>v.EffectiveRange)return "超出当前武器的射程（"+v.EffectiveRange.ToString("0")+" 格）。";}
   switch(e.effect){
    case "Charge":if(!t.Position.AdjacentTo8WayOrInside(caster.Position) && !Charge.LandingCell(caster,t).IsValid)return "目标身边没有能落脚的格子。";break;
    case "Intercept":if(t==caster)return "需要选择另一名友军。";float r=e.range*ClassSystem.StatValue(caster,e);if(caster.Position.DistanceTo(t.Position)>r)return "超出换位距离（"+r.ToString("0.#")+" 格）。";break;
   }
   return null;
  }

  public static void Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(p==null || e==null)return;
   var t=target.Thing as Pawn;float s=Strength(p,e);
   switch(e.effect){
    case "Passive":return;
    case "SelfBuff":Give(p,e.hediff??def.defName,Duration(p,e),s);Items.Mote(p,def.LabelCap,MartialColor);return;
    case "PowerBuff":Give(p,e.hediff??def.defName,Duration(p,e),Power(p,e));Items.Mote(p,def.LabelCap,Color.white);return;
    // 战士
    case "SecondWind":Give(p,"LP_SecondWind",600,Power(p,e));Items.Mote(p,"回气",MartialColor);return;
    case "BattleCry":BattleCry(p,Radius(p,e),Duration(p,e),s);return;
    case "Charge":if(Hostile(p,t))Charge.Start(p,t,1f+Charge.PerPower*s);return;
    case "Trip":if(Hostile(p,t))Trip(p,t,Duration(p,e),s);return;
    case "Whirlwind":Whirlwind.Sweep(p,s,Near(p,p.Position,Radius(p,e)).Where(x=>Hostile(p,x) && x.Position.AdjacentTo8WayOrInside(p.Position)).ToList());Items.Mote(p,"旋风斩",MartialColor);return;
    case "DisarmingStrike":if(!Hostile(p,t))return;ClassMelee.Strike(p,t);if(!t.Dead && Martial.CanDisarm(t))Martial.Disarm(p,t,Martial.DisarmChance(p,t,s,Adventurer.Str));return;
    case "SuppressingFire":if(target.Cell.IsValid)SuppressingFire.Start(p,target.Cell);return;
    case "PrecisionShot":if(Hostile(p,t))PrecisionShot.Shoot(p,t,s,Martial.State?.Mode(p)??PrecisionMode.Vital);return;
    // 武僧
    case "Flurry":if(!Hostile(p,t))return;for(int i=0;i<MonkCore.FlurryHits(s) && !t.Dead;i++)ClassMelee.Strike(p,t);Items.Mote(t,"疾风连击",MonkCore.Ki);return;
    case "Deflect":Give(p,"LP_Deflecting",Duration(p,e),Power(p,e));Items.Mote(p,"拨挡箭矢",MonkCore.Ki);return;
    case "StunningStrike":if(!Hostile(p,t))return;ClassMelee.Strike(p,t);if(!t.Dead)t.stances?.stunner?.StunFor(MonkCore.StunTicks(t,s),p);Items.Mote(t,"震慑拳",MonkCore.Ki);return;
    case "OpenHand":if(Hostile(p,t))MonkCore.Push(p,t,s);return;
    case "DiamondSoul":ClassMelee.Heal(p,Power(p,e));PaladinCore.Cleanse(p,.2f);Give(p,"LP_DiamondSoul",Duration(p,e),1f);Items.Mote(p,"金刚之躯",MonkCore.Ki);return;
    case "QuiveringPalm":if(!Hostile(p,t))return;ClassMelee.Strike(p,t);if(!t.Dead)MonkCore.Plant(p,t,Power(p,e));return;
    // 圣武士
    case "Smite":Give(p,e.hediff,Duration(p,e),Power(p,e));Items.Mote(p,def.LabelCap,PaladinCore.Holy);return;
    case "LayOnHands":if(Friendly(p,t))Items.Mote(t,PaladinCore.Touch(t,Power(p,e)),PaladinCore.Holy);return;
    case "Vow":if(Hostile(p,t))PaladinCore.Mark(p,t,Duration(p,e),s);return;
    case "Intercept":if(t!=null && t!=p && Friendly(p,t))PaladinCore.Swap(p,t);return;
    case "DispelEvil":PaladinCore.Dispel(p,Radius(p,e),Power(p,e));Items.Mote(p,"放逐邪恶",PaladinCore.Holy);return;
   }
   if(ClassCasters.Apply(p,def,e,target))return; // 1.73.0 施法职业
   if(ClassLegendary.Apply(p,def,e,target))return; // 1.74.0 传奇职业能力
   if(ClassMartial2.Apply(p,def,e,target))return; // 野蛮人、游侠、游荡者
   if(ClassSubclass.Apply(p,def,e,target))return; // 子职业与通用能力
   if(Warlock.Apply(p,def,e,target))return; // 邪术师
   Log.ErrorOnce("[DND边缘大陆] 未知的职业能力效果："+e.effect,e.effect?.GetHashCode()??0);
  }
  public static int BattleCry(Pawn p,float radius,int ticks,float s){
   int n=0;foreach(var t in Near(p,p.Position,radius).ToList()){if(!Hostile(p,t))continue;Give(t,"LP_Shaken",ticks,s);n++;}
   Items.Mote(p,"战吼",MartialColor);return n;
  }
  public static void Trip(Pawn p,Pawn t,int ticks,float s){
   if(t.BodySize>=2f){Give(t,"LP_Shaken",ticks,s);Items.Mote(t,"震慑",MartialColor);return;}
   t.stances?.stunner?.StunFor(ticks,p);
   if(t.Spawned){var dest=DNDPush.Destination(t.Map,t.Position,(t.Position-p.Position).ToVector3(),1);DNDPush.Move(t,dest);}
   Items.Mote(t,"绊摔",MartialColor);
  }
 }

 // 一次真正的近战结算（不掷命中）：原版近战伤害系数、武器品质、Isekai 的力量与精炼都照常生效；没有近战动作时退回钝器。
 public static class ClassMelee {
  static readonly System.Reflection.MethodInfo applyMelee=HarmonyLib.AccessTools.Method(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget");
  public static bool Strike(Pawn caster,Pawn target,float fallback=8f){
   if(caster==null || target==null || target.Dead || !target.Spawned)return false;
   var verb=caster.meleeVerbs?.TryGetMeleeVerb(target);
   if(verb is Verb_MeleeAttackDamage && applyMelee!=null)applyMelee.Invoke(verb,new object[]{new LocalTargetInfo(target)});
   else target.TakeDamage(new DamageInfo(DamageDefOf.Blunt,fallback,.1f,-1f,caster));
   caster.Drawer?.Notify_MeleeAttackOn(target);
   return true;
  }
  public static float Amount(Pawn caster,Pawn target){var verb=caster.meleeVerbs.TryGetMeleeVerb(target);return verb.verbProps.AdjustedMeleeDamageAmount(verb,caster);}
  // 治疗：先流血最多的伤口，再按严重度；返回实际治疗量。
  public static float Heal(Pawn p,float amount){
   if(p==null || p.Dead || amount<=0f)return 0f;
   var injuries=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h=>!h.IsPermanent()).OrderByDescending(h=>h.BleedRate).ThenByDescending(h=>h.Severity).ToList();
   float healed=0f;
   foreach(var inj in injuries){if(amount<=0f)break;float h=Mathf.Min(amount,inj.Severity);inj.Heal(h);amount-=h;healed+=h;}
   return healed;
  }
  public static float InjurySum(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h=>!h.IsPermanent()).Sum(h=>h.Severity);
 }

 // 旋风斩：对相邻敌人各结算一次当前近战武器（期间挂 LP_WhirlwindFury，近战伤害 × 战技强度）。
 public static class Whirlwind {
  public static int Sweep(Pawn pawn,float strength,List<Pawn> foes){
   int n=0;Items.Give(pawn,"LP_WhirlwindFury",60,strength);
   try{foreach(var t in foes){if(t.Dead || !t.Spawned)continue;ClassMelee.Strike(pawn,t,8f*strength);n++;}}
   finally{Items.Remove(pawn,"LP_WhirlwindFury");}
   return n;
  }
  public static float HitAmount(Pawn pawn,Pawn target,float strength){
   Items.Give(pawn,"LP_WhirlwindFury",60,strength);
   try{return ClassMelee.Amount(pawn,target);}finally{Items.Remove(pawn,"LP_WhirlwindFury");}
  }
 }
 // 冲锋：跳到 8 格内敌人身边（PawnFlyer），落地立刻一次近战结算，伤害 ×(1 + 0.3 × 战技强度)；体型 ≤ 1 的目标击晕 1 秒。
 public static class Charge {
  public const float PerPower=.3f;public const int KnockTicks=60;
  public static IntVec3 LandingCell(Pawn caster,Pawn t){
   var map=t.Map;if(map==null)return IntVec3.Invalid;
   IntVec3 best=IntVec3.Invalid;float bd=float.MaxValue;
   foreach(var c in GenAdj.CellsAdjacent8Way(t)){if(!c.InBounds(map) || !c.Standable(map) || c.GetFirstPawn(map)!=null)continue;float d=c.DistanceToSquared(caster.Position);if(d<bd){bd=d;best=c;}}
   return best;
  }
  public static bool Start(Pawn caster,Pawn t,float factor){
   if(caster?.Map==null || t==null || t.Dead)return false;
   if(t.Position.AdjacentTo8WayOrInside(caster.Position)){Land(caster,t,factor);return true;}
   var cell=LandingCell(caster,t);if(!cell.IsValid)return false;
   // MakeFlyer 会把施法者从地图上拿走，之后 caster.Map 是 null：地图和起点要先记下来，否则飞行物生成失败、人就没了。
   var map=caster.Map;var origin=caster.Position;
   Items.Mote(caster,"冲锋",ClassEffects.MartialColor);
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("LP_ChargeFlyer");
   var flyer=def==null?null:PawnFlyer.MakeFlyer(def,caster,cell,null,null) as LP_ChargeFlyer;
   if(flyer==null){if(!caster.Spawned)GenSpawn.Spawn(caster,origin,map);Races124.Blink(caster,cell);Land(caster,t,factor);return true;}
   flyer.target=t;flyer.factor=factor;
   GenSpawn.Spawn(flyer,origin,map);
   return true;
  }
  public static bool Land(Pawn caster,Pawn t,float factor){
   if(caster==null || caster.Dead || !caster.Spawned || t==null || t.Dead || !t.Spawned || t.Map!=caster.Map || !t.Position.AdjacentTo8WayOrInside(caster.Position))return false;
   Items.Give(caster,"LP_ChargeStrike",60,factor);
   try{ClassMelee.Strike(caster,t);}finally{Items.Remove(caster,"LP_ChargeStrike");}
   if(!t.Dead && t.BodySize<=1f){t.stances?.stunner?.StunFor(KnockTicks,caster);Items.Mote(t,"撞倒",ClassEffects.MartialColor);}
   return true;
  }
 }
 public class LP_ChargeFlyer : PawnFlyer {
  public Pawn target;public float factor=1f;
  protected override void RespawnPawn(){var p=FlyingPawn;base.RespawnPawn();if(p!=null)Charge.Land(p,target,factor);}
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref target,"lpChargeTarget");Scribe_Values.Look(ref factor,"lpChargeFactor",1f);}
 }
 // 压制射击：目标格半径 3，连射 6 发当前武器的子弹（每 6 刻一发，伤害 ×0.5）；区域内敌人被压制 6 秒。
 public static class SuppressingFire {
  public const int Shots=6,Interval=6,SuppressTicks=360;public const float Area=3f,DamageFactor=.5f;
  public static int Start(Pawn caster,IntVec3 cell){
   var map=caster.Map;if(map==null)return 0;int n=0;
   foreach(var t in Races124.PawnsNear(map,cell,Area).ToList())if(t!=caster && !t.Dead && t.HostileTo(caster)){Items.Give(t,"LP_Suppressed",SuppressTicks);n++;}
   var v=(Hediff_SuppressingVolley)Items.Give(caster,"LP_SuppressingVolley");
   if(v!=null){v.cell=cell;v.left=Shots;v.acc=Interval;}
   Items.Mote(caster,"压制射击",ClassEffects.MartialColor);
   return n;
  }
  public static Projectile FireOne(Pawn caster,IntVec3 center){
   var map=caster.Map;if(map==null)return null;
   var cells=GenRadial.RadialCellsAround(center,Area,true).Where(c=>c.InBounds(map)).ToList();
   var cell=cells.Count==0?center:cells.RandomElement();var target=(LocalTargetInfo)cell;
   var victim=cell.GetFirstPawn(map);if(victim!=null && victim.HostileTo(caster))target=victim;
   return Martial.Fire(caster,target,new ShotMod{damage=DamageFactor,caster=caster},false);
  }
 }
 public class Hediff_SuppressingVolley : HediffWithComps {
  public IntVec3 cell=IntVec3.Invalid;public int left,acc;
  public override bool ShouldRemove=>base.ShouldRemove || left<=0 || !cell.IsValid;
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(left<=0 || pawn==null || !pawn.Spawned || pawn.Downed || pawn.Dead){left=0;return;}
   acc+=delta;
   while(acc>=SuppressingFire.Interval && left>0){acc-=SuppressingFire.Interval;left--;SuppressingFire.FireOne(pawn,cell);}
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref cell,"lpVolleyCell",IntVec3.Invalid);Scribe_Values.Look(ref left,"lpVolleyLeft");Scribe_Values.Look(ref acc,"lpVolleyAcc");}
 }
 // 精准射击：必中，伤害 ×(1.5 + 0.5 × 射术强度)，穿甲 +0.3；模式：要害 / 打腿 ×0.7 + 跛行 / 打手 ×0.5 + 缴械判定。
 public static class PrecisionShot {
  public const float Base=1.5f,PerPower=.5f,Penetration=.3f,LegFactor=.7f,HandFactor=.5f;
  public static string ModeLabel(PrecisionMode m)=>m==PrecisionMode.Leg?"打腿":m==PrecisionMode.Hand?"打手":"要害";
  public static float DamageFor(float power,PrecisionMode m)=>(Base+PerPower*power)*(m==PrecisionMode.Leg?LegFactor:m==PrecisionMode.Hand?HandFactor:1f);
  public static Projectile Shoot(Pawn caster,Pawn t,float power,PrecisionMode mode){
   var proj=Martial.Fire(caster,t,new ShotMod{damage=DamageFor(power,mode),ap=Penetration,power=power,mode=mode,precision=true,caster=caster},true);
   if(proj!=null)Items.Mote(caster,"精准射击·"+ModeLabel(mode),ClassEffects.MartialColor);
   return proj;
  }
 }
}
