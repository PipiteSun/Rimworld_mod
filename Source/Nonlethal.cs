using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.61.0 制伏武器：LP_Subdue（制伏打击，按钝击护甲减免）和 LP_SleepMagic（安眠魔法，无视护甲、按心智抗性减免）不造成任何伤口，
 // 只给 LP_Subdued 加严重度：增量 = 伤害 × 设置倍率 ÷ (40 × 体型 × 生命值倍率)，上限 1.3，到 1.0 时意识最多 10%，昏迷倒地（意识 0 会致死，所以不压到 0）。
 // 机械体、非血肉、死灵造物、巫妖免疫；已经倒地的目标不再加（所以怎么打都打不死）。恢复见 HediffComp_SubdueRecover。
 public static class Nonlethal {
  public const float Divisor=40f,Cap=1.3f,DownedAt=1f,ArmorWeight=.75f;
  public const string ImmuneText="免疫制伏",DownedText="已经昏迷了";
  public static readonly string[] Weapons={"LP_SubduingCudgel","LP_NetCrossbow","LP_SleepBow","LP_SleepStaff","LP_SleepDustPouch"};
  static readonly HashSet<string> weaponSet=new HashSet<string>(Weapons);
  static readonly DefRef<HediffDef> subdued=new DefRef<HediffDef>("LP_Subdued");
  static readonly DefRef<DamageDef> subdue=new DefRef<DamageDef>("LP_Subdue"),sleep=new DefRef<DamageDef>("LP_SleepMagic");
  public static HediffDef SubduedDef=>subdued.Def;
  public static DamageDef SubdueDamage=>subdue.Def;
  public static DamageDef SleepDamage=>sleep.Def;
  public static readonly Color MoteColor=new Color(.8f,.65f,1f);
  // 设置“制伏伤害倍率”50%–200%。
  public static float Multiplier=>Mathf.Clamp((LichMod.Settings?.subdueDamagePercent??100)/100f,.5f,2f);
  public static bool IsWeapon(ThingDef d)=>d!=null && weaponSet.Contains(d.defName);
  public static float Severity(Pawn p){var d=SubduedDef;return d==null?0f:p?.health?.hediffSet?.GetFirstHediffOfDef(d)?.Severity??0f;}

  // 不能制伏的原因（浮字）；null = 可以。
  public static string Immune(Pawn p){
   if(p==null || p.Dead || p.health==null)return ImmuneText;
   if(p.Downed)return DownedText;
   if(p.RaceProps.IsMechanoid || !p.RaceProps.IsFlesh || Items.Undead(p))return ImmuneText;
   return null;
  }
  // 安眠魔法按心智抗性削弱（精灵 50% → 一半，≥100% 免疫）；制伏打击不看心智抗性。
  public static float MindFactor(Pawn p,DamageDef def)=>def!=null && def==SleepDamage?Mathf.Clamp01(1f-DNDMind.Resistance(p)):1f;
  public static float Increment(float amount,float bodySize,float healthScale,float mind=1f,float mult=1f){
   if(amount<=0f || mind<=0f || mult<=0f)return 0f;
   return amount*mult*mind/(Divisor*Mathf.Max(.05f,bodySize)*Mathf.Max(.05f,healthScale));
  }
  // 护甲：躯干上的天生护甲 + 覆盖躯干的衣物护甲 − 穿甲，按原版“弹开 / 减半”两档的期望值折算（1 − 0.75 × 有效护甲），不掷骰，也不磨损衣物。
  public static float ArmorFactor(Pawn p,DamageInfo dinfo){
   var stat=dinfo.Def?.armorCategory?.armorRatingStat;if(stat==null || p==null)return 1f;
   float armor=p.GetStatValue(stat);
   var worn=p.apparel?.WornApparel;var torso=p.RaceProps.body.corePart;
   if(worn!=null)for(int i=0;i<worn.Count;i++)if(worn[i].def.apparel!=null && worn[i].def.apparel.CoversBodyPart(torso))armor+=worn[i].GetStatValue(stat);
   return 1f-ArmorWeight*Mathf.Clamp01(armor-dinfo.ArmorPenetrationInt);
  }
  public static float IncrementFor(Pawn p,DamageInfo dinfo)=>Increment(dinfo.Amount*ArmorFactor(p,dinfo),p.BodySize,p.HealthScale,MindFactor(p,dinfo.Def),Multiplier);
  // 命中：返回实际增加的严重度（免疫时 0，并飘字）。
  public static float Subdue(Pawn p,DamageInfo dinfo,bool mote=true){
   var why=Immune(p);
   if(why==null && MindFactor(p,dinfo.Def)<=0f)why=ImmuneText;
   if(why!=null){if(mote)Items.Mote(p,why,MoteColor);return 0f;}
   var def=SubduedDef;if(def==null)return 0f;
   float inc=IncrementFor(p,dinfo);if(inc<=0f)return 0f;
   var h=p.health.hediffSet.GetFirstHediffOfDef(def);
   if(h==null){h=HediffMaker.MakeHediff(def,p);h.Severity=Mathf.Min(Cap,inc);p.health.AddHediff(h,null,dinfo);}
   else h.Severity=Mathf.Min(Cap,h.Severity+inc);
   (h as HediffWithComps)?.TryGetComp<HediffComp_SubdueRecover>()?.Notify_Hit();
   if(h.Severity>=DownedAt && !p.Downed && !p.Dead)p.health.CheckForStateChange(dinfo,h);
   return inc;
  }
  // 束缚网：活着、没倒地的目标眩晕并减速（网是实物，机械体也会被缠住）。
  public static bool Entangle(Pawn p,NonlethalShotExt ext,Thing instigator){
   if(ext==null || p==null || p.Dead || p.Downed)return false;
   if(ext.stunTicks>0)p.stances?.stunner?.StunFor(ext.stunTicks,instigator,false,true,false);
   if(!ext.hediff.NullOrEmpty())Items.Give(p,ext.hediff,ext.hediffTicks);
   return true;
  }
 }

 public class DamageWorker_LPSubdue : DamageWorker {
  // 不造成伤口、不流血、不断肢、不碰衣物；建筑和物品不受影响。
  public override DamageResult Apply(DamageInfo dinfo,Thing victim){
   var result=new DamageResult();
   if(victim is Pawn p && !p.Dead){Nonlethal.Subdue(p,dinfo);result.hitThing=p;}
   return result;
  }
  // 安眠粉囊炸开时多扬几团紫色粉尘。
  public override void ExplosionStart(Explosion explosion,List<IntVec3> cellsToAffect){
   base.ExplosionStart(explosion,cellsToAffect);
   var map=explosion.Map;if(map==null)return;
   for(int i=0;i<6;i++){var off=new Vector3(Rand.Range(-1f,1f),0f,Rand.Range(-1f,1f))*explosion.radius*.6f;FleckMaker.ThrowDustPuffThick(explosion.Position.ToVector3Shifted()+off,map,Rand.Range(1.5f,2.6f),new Color(.72f,.5f,1f,.85f));}
  }
 }

 public class HediffCompProperties_SubdueRecover : HediffCompProperties {
  public int graceTicks=2500;public float perHour=.1f;
  public HediffCompProperties_SubdueRecover(){compClass=typeof(HediffComp_SubdueRecover);}
 }
 // 最后一次制伏伤害后 graceTicks（1 小时）内不消退，之后每小时 −perHour（满 1.3 约昏迷 4 小时）。
 public class HediffComp_SubdueRecover : HediffComp {
  public int lastHitTick=-1;
  public HediffCompProperties_SubdueRecover Props=>(HediffCompProperties_SubdueRecover)props;
  public void Notify_Hit()=>lastHitTick=Find.TickManager.TicksGame;
  public bool Recovering(int now)=>lastHitTick==-1 || now-lastHitTick>=Props.graceTicks;
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){
   if(Recovering(Find.TickManager.TicksGame))severityAdjustment-=Props.perHour*delta/(float)GenDate.TicksPerHour;
  }
  public override string CompTipStringExtra{get{
   int now=Find.TickManager.TicksGame;
   if(!Recovering(now))return "再过 "+(lastHitTick+Props.graceTicks-now).ToStringTicksToPeriod()+" 开始消退";
   return "每小时消退 "+Props.perHour.ToStringPercent();
  }}
  public override void CompExposeData(){Scribe_Values.Look(ref lastHitTick,"lpLastSubdueTick",-1);}
 }

 public class NonlethalShotExt : DefModExtension {
  public int stunTicks;public string hediff;public int hediffTicks;
 }
 // 束缚网：照常结算 LP_Subdue 伤害，再眩晕并减速。
 public class Projectile_LPNet : Bullet {
  protected override void Impact(Thing hitThing,bool blockedByShield=false){
   var ext=def.GetModExtension<NonlethalShotExt>();var inst=launcher;
   base.Impact(hitThing,blockedByShield);
   if(!blockedByShield && hitThing is Pawn p)Nonlethal.Entangle(p,ext,inst);
  }
 }

 // 猎人工作不认制伏武器（否则拿着它去打猎会一直打不死猎物）。
 [HarmonyPatch(typeof(WorkGiver_HunterHunt),"HasHuntingWeapon")]
 public static class NonlethalNoHunting {
  static void Postfix(Pawn __0,ref bool __result){if(__result && Nonlethal.IsWeapon(__0?.equipment?.Primary?.def))__result=false;}
 }
}
