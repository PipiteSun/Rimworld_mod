using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.120.0 圣武士第三个子职业「破誓者」（Defs/Subclass_Oathbreaker.xml，圣武士 3 / 7 / 11 / 15 级）。
 // 背弃了誓言的圣武士：没有戒律、不会背誓；两个 3 级能力用「誓言之力」（和征服 / 救赎共用 Subclass2 的次数、刷新与扣除，见 ClassSubclass2.cs 的 OathAbilities）。
 // 分派同泰坦战士：Tick250 / 能力检查 / 能力效果挂在 ClassSystem.Tick250 / ClassEffects.Invalid / ClassEffects.Apply 上（效果名前缀 OB）；伤害前缀第一步是空集合判断。
 public static class Oathbreaker {
  public const string Sub="LP_Sub_Oathbreaker";
  public static readonly Color DreadColor=new Color(.6f,.15f,.2f);
  public const float HateRadius=5f,HateBase=.1f,HatePerCha=.02f,ResistFactor=.75f,DreadRadius=6f,LordRanged=.5f,ControlChance=.8f,AspectChance=.6f;
  public const int ControlTicks=60000;
  public const int FHate=1,FResist=2;
  static readonly Dictionary<string,int> FlagOf=new Dictionary<string,int>{{"LP_OB_AuraOfHate",FHate},{"LP_OB_SupernaturalResistance",FResist}};
  public static readonly Dictionary<int,int> Users=new Dictionary<int,int>();
  public static readonly HashSet<Pawn> Haters=new HashSet<Pawn>(),Lords=new HashSet<Pawn>();
  public static int Flags(Pawn p){if(p==null || Users.Count==0)return 0;Users.TryGetValue(p.thingIDNumber,out int f);return f;}
  public static bool S(Pawn p,int f)=>(Flags(p) & f)!=0;
  static float Roll=>ClassMartial2.RollOverride??Rand.Value;
  static int Now=>Find.TickManager.TicksGame;
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.HostileTo(a);
  static float Divine(Pawn p){var s=DefCache<StatDef>.Get("LP_DivinePower");return s==null?1f:p.GetStatValue(s);}
  static DamageDef Necrotic=>DefCache<DamageDef>.Get("LP_Necrotic")??DamageDefOf.Burn;
  // 亡者或邪魔：巫妖、构装 / 死灵造物、异象的蹒跚尸等变异体、死亡骑士、半巫妖、深渊与九狱的异界生物。
  public static bool UndeadOrFiend(Pawn t)=>t!=null && (Items.Undead(t) || t.IsMutant || Monsters.Is(t,Monsters.DeathKnight) || Monsters.Is(t,Monsters.Demilich) || Outsiders.Is(t));

  public static int Refresh(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return 0;int f=0;
   if(!p.Dead && ClassSubclass.Has(h,Sub)){var list=p.abilities?.abilities;if(list!=null)for(int k=0;k<list.Count;k++)if(FlagOf.TryGetValue(list[k].def.defName,out int b))f|=b;}
   if(f==0)Users.Remove(p.thingIDNumber);else Users[p.thingIDNumber]=f;
   if((f & FHate)!=0)Haters.Add(p);else Haters.Remove(p);
   return f;
  }
  public static void Refresh(Pawn p){var h=Adventurer.Get(p);if(h!=null)Refresh(h);}
  public static void Tick250(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return;
   bool lord=Lords.Contains(p);
   if(Users.Count==0 && !lord && !ClassSubclass.Has(h,Sub))return;
   if(p.Dead){Users.Remove(p.thingIDNumber);Haters.Remove(p);Lords.Remove(p);return;}
   Refresh(h);
   if(lord)LordPulse(p);
  }

  // ---------- 誓言之力 ----------
  // 恐惧面容：半径 6 内的敌人（机械体除外）成功率 60%（目标每比你高 1 级 −5%，最低 10%）被震慑并逃跑。
  public static int Aspect(Pawn p,float radius,int ticks){
   if(p.Map==null)return 0;int n=0;
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,radius).ToList()){
    if(!Hostile(p,t) || t.Downed || t.RaceProps.IsMechanoid)continue;
    if(Roll>=Subclass2.Chance(AspectChance,p,t)){Items.Mote(t,"抵抗！",DreadColor);continue;}
    Items.Give(t,"LP_Shaken",ticks,1f);ClassSubclass.Fear(t,ticks,"恐惧面容");n++;
   }
   FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,radius);Items.Mote(p,"恐惧面容",DreadColor);return n;
  }
  // 操控亡者：敌对的亡者或邪魔成功率 80%（同上按等级差扣）倒戈，加入你的阵营 1 天；到期多半挣脱（同心灵支配的到期表）。
  public static bool Control(Pawn p,Pawn t){
   if(t==null || t.Dead || t.mindState==null)return false;
   if(Roll>=Subclass2.Chance(ControlChance,p,t)){Items.Mote(t,"抵抗了操控",DreadColor);return false;}
   var orig=t.Faction;
   if(t.InMentalState)t.MentalState.RecoverFromState();
   t.SetFaction(p.Faction);t.mindState.enemyTarget=null;
   Current.Game?.GetComponent<GameComponent_Subclass2>()?.Add(t,orig,Now+ControlTicks);
   Items.Mote(t,"被操控",DreadColor);
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(t.LabelShort+"被"+p.LabelShort+"操控，暂时为你作战 1 天。",t,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }

  // ---------- 憎恨灵光 ----------
  public static float HateFactor(Pawn holder){var h=Adventurer.Get(holder);return 1f+HateBase+HatePerCha*Mathf.Max(0f,h?.AModRace(Adventurer.Cha)??0f);}
  // 近战攻击方：破誓者本人，或半径 5 内同阵营的亡者 / 邪魔。取最强的一个灵光，不叠加。
  public static float HateFor(Pawn a){
   if(a==null || Haters.Count==0)return 1f;float best=1f;
   foreach(var o in Haters){
    if(o==null || o.Dead || !o.Spawned || o.Downed)continue;
    if(o==a || a.Spawned && o.Map==a.Map && o.Faction!=null && o.Faction==a.Faction && UndeadOrFiend(a) && o.Position.InHorDistOf(a.Position,HateRadius))best=Mathf.Max(best,HateFactor(o));
   }
   return best;
  }

  // ---------- 恐惧之王 ----------
  // 60 秒：每 4 秒（Tick250）半径 6 内的敌人受「严重度」点死灵伤害；受到的远程伤害 ×0.5。施放时先放一次恐惧面容。
  public static void LordStart(Pawn p,int ticks,float power){
   Items.Give(p,"LP_OB_DreadLord",ticks,power);Lords.Add(p);
   Aspect(p,DreadRadius,600);Items.Mote(p,"恐惧之王",DreadColor);
  }
  public static bool Lord(Pawn p)=>Lords.Count>0 && Lords.Contains(p) && Items.Has(p,"LP_OB_DreadLord");
  public static int LordPulse(Pawn p){
   var h=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_OB_DreadLord"));if(h==null){Lords.Remove(p);return 0;}
   if(!p.Spawned || p.Downed)return 0;float dmg=h.Severity;int n=0;
   Busy=true;
   try{foreach(var t in Races124.PawnsNear(p.Map,p.Position,DreadRadius).ToList())if(Hostile(p,t) && !t.Downed){t.TakeDamage(new DamageInfo(Necrotic,dmg,.3f,-1f,p));n++;}}
   finally{Busy=false;}
   if(n>0)FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,DreadRadius*.6f,new Color(.2f,.05f,.08f));
   return n;
  }

  // ---------- 伤害 ----------
  public static bool Busy;
  public static float DamageFactor(Pawn v,DamageInfo d){
   float g=1f;var a=d.Instigator as Pawn;
   if(a!=null && a!=v && a.HostileTo(v) && !Busy && Haters.Count>0 && ClassMartial2.Melee(d))g*=HateFor(a);
   int fv=Flags(v);
   if((fv & FResist)!=0 && !Monsters.Holy(d)){var cat=d.Def.armorCategory;if(cat==DamageArmorCategoryDefOf.Sharp || cat?.defName=="Blunt")g*=ResistFactor;}
   if(Lords.Count>0 && ForceDragon.IsRanged(d) && Lord(v))g*=LordRanged;
   return g;
  }

  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e.effect!="OBControl")return null;var t=target.Thing as Pawn;
   if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";
   if(!UndeadOrFiend(t))return "只能操控亡者或邪魔。";if(ClassSubclass.Bossy(t))return "剧情首领和传奇单位不会被操控。";if(t.IsPrisoner)return "已经是囚犯了。";
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;
   switch(e.effect){
    case "OBAspect":Aspect(p,ClassEffects.Radius(p,e),ClassEffects.Duration(p,e));return true;
    case "OBControl":if(Hostile(p,t))Control(p,t);return true;
    case "OBDreadLord":LordStart(p,ClassEffects.Duration(p,e),ClassEffects.Power(p,e));return true;
   }
   return false;
  }
  public static void Reset(){Users.Clear();Haters.Clear();Lords.Clear();Busy=false;}
 }

 public class GameComponent_Oathbreaker : GameComponent {
  public GameComponent_Oathbreaker(Game game){}
  public override void FinalizeInit(){
   base.FinalizeInit();Oathbreaker.Reset();
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawns){
    if(Items.Has(p,"LP_OB_DreadLord"))Oathbreaker.Lords.Add(p);
    var h=Adventurer.Get(p);if(h!=null && ClassSubclass.Has(h,Oathbreaker.Sub))Oathbreaker.Refresh(h);
   }
  }
 }

 // ==================== Harmony ====================
 [HarmonyPatch(typeof(ClassSystem),nameof(ClassSystem.Tick250))]
 public static class OathbreakerTick {static void Postfix(Hediff_Adventurer h)=>Oathbreaker.Tick250(h);}
 [HarmonyPatch(typeof(ClassEffects),nameof(ClassEffects.Invalid))]
 public static class OathbreakerInvalid {static void Postfix(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target,ref string __result){if(__result==null && e?.effect!=null && e.effect.StartsWith("OB"))__result=Oathbreaker.Invalid(caster,e,target);}}
 [HarmonyPatch(typeof(ClassEffects),nameof(ClassEffects.Apply))]
 public static class OathbreakerApply {static bool Prefix(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target)=>p==null || e?.effect==null || !e.effect.StartsWith("OB") || !Oathbreaker.Apply(p,def,e,target);}
 // 憎恨灵光（攻击方）、超凡抗性与恐惧之王（受击方）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class OathbreakerDamage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(Oathbreaker.Users.Count==0 && Oathbreaker.Lords.Count==0 || Oathbreaker.Busy || dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   float f=Oathbreaker.DamageFactor(__instance,dinfo);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
}
