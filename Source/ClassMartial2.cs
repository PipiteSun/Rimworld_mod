using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 新职业（见 ArtSource/Classes3-spec.md）：野蛮人（怒气）、游侠（宿敌、兽伴）、游荡者（偷袭）。都是物理职业（在 Martial 缓存里）；
 // 游荡者不给额外攻击档位。被动按能力位（MartialInfo.flags）记，Martial.Refresh（每 250 刻、加点时）更新；所有命中 / 伤害挂钩第一步都是 Martial.Is 或空集合判断。
 public static class ClassMartial2 {
  static readonly DefRef<LP_ClassDef> barbarian=new DefRef<LP_ClassDef>("LP_Class_Barbarian"),ranger=new DefRef<LP_ClassDef>("LP_Class_Ranger"),rogue=new DefRef<LP_ClassDef>("LP_Class_Rogue");
  public static LP_ClassDef Barbarian=>barbarian.Def;public static LP_ClassDef Ranger=>ranger.Def;public static LP_ClassDef Rogue=>rogue.Def;
  public static readonly Color RageColor=new Color(.9f,.45f,.35f),HuntColor=new Color(.55f,.75f,.45f),ShadowColor=new Color(.62f,.62f,.78f);

  // ---------- 被动能力位 ----------
  public const int FDanger=1,FBrutal=2,FRelentless=4,FPrimal=8,FFavored=16,FWoodstride=32,FBond=64,FSlayer=128,FSneak=256,FUncanny=512,FLuck=1024,FBerserker=2048,FFoeBane=4096,FShadowDance=8192;
  public static int Flags(Pawn p){
   var list=p?.abilities?.abilities;if(list==null)return 0;int f=0;
   for(int i=0;i<list.Count;i++){
    switch(list[i].def.defName){
     case "LP_DangerSense":f|=FDanger;break;case "LP_BrutalCritical":f|=FBrutal;break;case "LP_RelentlessRage":f|=FRelentless;break;case "LP_PrimalChampion":f|=FPrimal;break;
     case "LP_FavoredFoe":f|=FFavored;break;case "LP_Woodstride":f|=FWoodstride;break;case "LP_BeastBond":f|=FBond;break;case "LP_FoeSlayer":f|=FSlayer;break;
     case "LP_SneakAttack":f|=FSneak;break;case "LP_UncannyDodge":f|=FUncanny;break;case "LP_StrokeOfLuck":f|=FLuck;break;
     case "LP_LegBerserkerSoul":f|=FBerserker;break;case "LP_LegFoeBane":f|=FFoeBane;break;case "LP_LegShadowDance":f|=FShadowDance;break;
    }
   }
   return f;
  }
  public static bool Has(Pawn p,int flag){var i=Martial.Is(p)?Martial.Info(p):null;return i!=null && (i.flags & flag)!=0;}
  public static readonly HashSet<Pawn> Woodstriders=new HashSet<Pawn>();
  // Martial.Refresh 之后：野外穿行名单；原初斗士变了就让冒险者状态重算属性。
  public static void Refreshed(Pawn p,MartialInfo i,int oldFlags){
   if(i!=null && (i.flags & FWoodstride)!=0)Woodstriders.Add(p);else Woodstriders.Remove(p);
   int now=i?.flags??0;
   if(((now^oldFlags) & FPrimal)!=0)Adventurer.Get(p)?.Dirty();
   ClassSubclass.Refreshed(p,i); // 子职业能力位
  }
  public static void Reset(){Woodstriders.Clear();Bonded.Clear();lastSneak.Clear();luckCrit.Clear();ClassSubclass.Reset();}

  // ---------- 野蛮人：怒气、狂暴、原初斗士 ----------
  public const float RageTaken=.5f,RageDealt=.1f,RageDecay=.1f,RageReduction=.7f,DangerDodge=.15f,DangerDodgeMax=.3f,ExplosionFactor=.5f,CritChance=.15f,CritRaging=.25f,RoarFear=.4f,RoarFearMax=.8f;
  public const int CombatTicks=600,PrimalStr=4,RelentlessTicks=600;
  static readonly DefRef<HediffDef> rageDef=new DefRef<HediffDef>("LP_Rage"),relentlessDef=new DefRef<HediffDef>("LP_RelentlessRageActive");
  public static Hediff_Rage Rage(Pawn p){var d=rageDef.Def;return d==null?null:p?.health?.hediffSet?.GetFirstHediffOfDef(d) as Hediff_Rage;}
  public static bool Raging(Pawn p)=>Rage(p)!=null;
  public static int ScoreBonus(Hediff_Adventurer h,int a)=>a==Adventurer.Str && h?.pawn!=null && Has(h.pawn,FPrimal)?PrimalStr:0;
  public static float GainRage(Pawn p,float amount){
   var h=Adventurer.Get(p);var b=Barbarian;if(h?.classes==null || b==null || ClassSystem.Level(h,b)<=0)return 0f;
   h.classes.rageTick=Find.TickManager.TicksGame;
   return ClassSystem.AddResource(h,b,amount);
  }
  public static float DangerChance(Pawn p){var s=DefCache<StatDef>.Get("LP_RagePower");return Mathf.Min(DangerDodgeMax,DangerDodge*(s==null?1f:p.GetStatValue(s)));}
  public static bool TryRelentless(Pawn p){
   if(p==null || p.Dead || p.Downed || !Has(p,FRelentless))return false;
   var r=Rage(p);if(r==null || r.relentlessUsed || Items.Has(p,relentlessDef.Def) || !ClassCasters.WouldDown(p))return false;
   r.relentlessUsed=true;Items.Give(p,"LP_RelentlessRageActive",RelentlessTicks);
   Items.Mote(p,"不灭之怒",RageColor);
   return true;
  }
  public static bool Holding(Pawn p)=>Martial.Cache.Count>0 && Martial.Is(p) && Items.Has(p,relentlessDef.Def);

  // ---------- 游侠：宿敌、兽伴 ----------
  public const int Humanoid=1<<10,Construct=1<<11;
  public const float FavoredDamage=.2f,FavoredHit=.2f,FoeBaneDamage=.15f,MarkPer=.15f,AmbushFactor=1.5f,BondDamage=.2f;
  public static readonly int[] FoeTypes={(int)DndType.Dragon,(int)DndType.Giant,(int)DndType.Undead,(int)DndType.Fiend,(int)DndType.Aberration,(int)DndType.Beast,(int)DndType.Divine,Humanoid,Construct};
  public static string FoeLabel(int t)=>t==Humanoid?"人形":t==Construct?"机械体":DndCreatureType.Label((DndType)t);
  public static bool FoeMatches(int t,Pawn v){
   if(v?.RaceProps==null)return false;
   if(t==Humanoid)return v.RaceProps.Humanlike;
   if(t==Construct)return v.RaceProps.IsMechanoid;
   return DndCreatureType.Is(v,(DndType)t);
  }
  public const int SecondFoeLevel=10; // 1.87.0：第二个宿敌 13 → 10
  public static int FoeSlots(Hediff_Adventurer h){int l=ClassSystem.Level(h,Ranger);return l<=0?0:l>=SecondFoeLevel?2:1;}
  public static int OpenFoeSlots(Hediff_Adventurer h)=>h?.classes==null?0:Mathf.Max(0,FoeSlots(h)-h.classes.favored.Count);
  public static bool PickFoe(Hediff_Adventurer h,int t){
   if(h?.classes==null || !FoeTypes.Contains(t) || h.classes.favored.Contains(t) || OpenFoeSlots(h)<=0)return false;
   h.classes.favored.Add(t);return true;
  }
  public static bool Favored(Pawn ranger,Pawn v){
   if(v==null || !Has(ranger,FFavored))return false;var l=Adventurer.Get(ranger)?.classes?.favored;if(l==null)return false;
   for(int i=0;i<l.Count;i++)if(FoeMatches(l[i],v))return true;
   return false;
  }
  public static float FavoredFactor(Pawn a)=>(Has(a,FSlayer)?2f:1f);
  public static readonly HashSet<Pawn> Bonded=new HashSet<Pawn>();
  static readonly DefRef<HediffDef> bondDef=new DefRef<HediffDef>("LP_BeastBondBuff");
  public static int Bond(Pawn ranger){
   var map=ranger.Map;if(map==null)return 0;int n=0;var s=DefCache<StatDef>.Get("LP_HuntPower");float pw=s==null?1f:ranger.GetStatValue(s);
   foreach(var a in map.mapPawns.SpawnedColonyAnimals){if(a.Dead || a.playerSettings?.Master!=ranger)continue;Items.Give(a,"LP_BeastBondBuff",300,pw);Bonded.Add(a);n++;}
   return n;
  }

  // ---------- 游荡者：偷袭、诡诈闪避、幸运一击 ----------
  public const float SneakBase=.2f,SneakPer=.03f,PoisonPer=.06f,AssassinBase=2f,AssassinAware=1.5f;
  public const int SneakTicks=60,ShadowDanceTicks=30,UncannyTicks=600;
  static readonly Dictionary<int,int> lastSneak=new Dictionary<int,int>();
  public static readonly HashSet<int> luckCrit=new HashSet<int>();
  public static float SneakBonus(int level,float strength)=>(SneakBase+SneakPer*Mathf.Max(0,level-1))*strength;
  public static bool Hidden(Pawn a)=>Items.Has(a,"LP_ShadowStep") || Items.Has(a,"LP_HideInPlain") || a.IsPsychologicallyInvisible();
  public static bool Targeting(Pawn v,Pawn a){var job=v.CurJob;return v.mindState?.enemyTarget==a || job!=null && (job.targetA.Thing==a || job.targetB.Thing==a);}
  public static bool Fleeing(Pawn v)=>v.InMentalState && v.MentalStateDef!=null && (v.MentalStateDef==MentalStateDefOf.PanicFlee || v.MentalStateDef.defName.Contains("Flee"));
  public static bool SneakOk(Pawn a,Pawn v){
   if(Has(a,FShadowDance) || ClassSubclass.SneakExtra(a,v))return true; // 子职业：稳固目标、多才多艺的诡计
   if(v.Downed || v.stances?.stunner?.Stunned==true || Fleeing(v) || !Targeting(v,a) || Hidden(a))return true;
   if(a.Map!=null && a.Faction!=null)foreach(var o in Races124.PawnsNear(a.Map,v.Position,1.5f))if(o!=a && o!=v && !o.Dead && !o.Downed && o.Faction==a.Faction && !o.HostileTo(a))return true;
   return false;
  }
  public static bool SneakReady(Pawn a){lastSneak.TryGetValue(a.thingIDNumber,out int t);int now=Find.TickManager.TicksGame;return t<=0 || now-t>=(Has(a,FShadowDance)?ShadowDanceTicks:SneakTicks) || now<t;}
  static bool Ready(Hediff_Adventurer h,string key,int ticks){if(h?.classes==null)return false;h.classes.legendaryTicks.TryGetValue(key,out int t);int now=Find.TickManager.TicksGame;return t<=0 || now-t>=ticks || now<t;}
  static void Use(Hediff_Adventurer h,string key){h.classes.legendaryTicks[key]=Math.Max(1,Find.TickManager.TicksGame);}
  public static bool LuckReady(Pawn p)=>Has(p,FLuck) && Ready(Adventurer.Get(p),"luck",GenDate.TicksPerDay);
  public static float? RollOverride;
  static float Roll=>RollOverride??Rand.Value;

  // ---------- 伤害（PreApplyDamage 前缀） ----------
  public static bool Melee(DamageInfo d)=>!d.Def.isRanged && !d.Def.isExplosive && !(d.Weapon!=null && d.Weapon.IsRangedWeapon);
  public static float Out(Pawn a,Pawn v,DamageInfo d){
   var i=Martial.Info(a);if(i==null)return 1f;float f=1f;
   bool hostile=v.HostileTo(a);
   if(i.barbarian>0){
    if(hostile)GainRage(a,RageDealt);
    if((i.flags & FBrutal)!=0 && Melee(d) && Roll<(Raging(a)?CritRaging:CritChance)){f*=2f;Items.Mote(v,"重击",RageColor);}
   }
   if(i.ranger>0){
    if(Favored(a,v))f*=1f+FavoredDamage*FavoredFactor(a)+((i.flags & FFoeBane)!=0?FoeBaneDamage:0f);
    if(Items.Has(a,"LP_HideInPlain")){if(!ClassSubclass.Unsurprisable(v))f*=AmbushFactor;Items.Remove(a,"LP_HideInPlain");Items.Mote(a,"伏击",HuntColor);}
   }
   var mark=Mark(v,"LP_RangerMark");if(mark!=null && mark.caster==a)f*=1f+MarkPer*mark.Severity;
   if(i.rogue>0){
    var dm=Mark(v,"LP_DeathMark");if(dm!=null && dm.caster==a)f*=2f;
    bool open=hostile && ClassSubclass.Opening(a,v); // 子职业·刺客 暗杀：重击 ×2 并必定偷袭
    if(open){f*=2f;Items.Mote(v,"暗杀",ShadowColor);}
    if(hostile && (i.flags & FSneak)!=0 && !ClassSubclass.Unsurprisable(v) && (open || SneakReady(a) && SneakOk(a,v))){
     var s=DefCache<StatDef>.Get("LP_CunningPower");f*=1f+SneakBonus(i.rogue,s==null?1f:a.GetStatValue(s));
     if(ClassSubclass.S(a,ClassSubclass.SDeadly) && ClassSubclass.Controlled(v))f*=ClassSubclass.DeadlyFactor; // 致命一击
     lastSneak[a.thingIDNumber]=Find.TickManager.TicksGame;Items.Mote(v,"偷袭",ShadowColor);
    }
    if(luckCrit.Remove(a.thingIDNumber))f*=2f;
    if(Items.Has(a,"LP_PoisonBlade") && v.RaceProps.IsFlesh && !Items.Undead(v)){var s=DefCache<StatDef>.Get("LP_CunningPower");HealthUtility.AdjustSeverity(v,HediffDefOf.ToxicBuildup,PoisonPer*(s==null?1f:a.GetStatValue(s)));}
    if(Items.Has(a,"LP_ShadowStep"))Items.Remove(a,"LP_ShadowStep");
   }
   if(ClassSubclass.Users.Count>0)f*=ClassSubclass.Out(a,v,d,hostile); // 子职业
   return f;
  }
  // 返回 -1 表示完全闪开。
  public static float In(Pawn v,Pawn a,DamageInfo d){
   var i=Martial.Info(v);if(i==null)return 1f;float f=1f;
   if(i.barbarian>0){
    if(a!=null && a!=v && a.HostileTo(v))GainRage(v,RageTaken);
    var cat=d.Def.armorCategory?.defName;
    if((cat=="Sharp" || cat=="Blunt") && Raging(v))f*=RageReduction;
    if((i.flags & FDanger)!=0 && !v.Downed){
     if(d.Def.isExplosive)f*=ExplosionFactor;
     else if(ForceDragon.IsRanged(d) && Roll<DangerChance(v)){Items.Mote(v,"闪开",RageColor);return -1f;}
    }
   }
   if(i.rogue>0 && (i.flags & FUncanny)!=0 && ForceDragon.IsRanged(d)){var h=Adventurer.Get(v);if(Ready(h,"uncanny",UncannyTicks)){Use(h,"uncanny");f*=.5f;Items.Mote(v,"诡诈闪避",ShadowColor);}}
   if(ClassSubclass.Users.Count>0){float g=ClassSubclass.In(v,a,d);if(g<0f)return -1f;f*=g;} // 子职业
   return f;
  }
  public static Hediff_ClassMark Mark(Pawn v,string name){var def=Items.Hediff(name);return def==null?null:v.health?.hediffSet?.GetFirstHediffOfDef(def) as Hediff_ClassMark;}

  // ---------- 每 250 刻（职业者自己的冒险者状态） ----------
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;if(p==null || p.Dead || h.classes==null || !Martial.Is(p))return;var i=Martial.Info(p);
   if(i.barbarian>0){
    bool raging=Raging(p);
    if((i.flags & FPrimal)==0 && !raging && Find.TickManager.TicksGame-h.classes.rageTick>CombatTicks)ClassSystem.AddResource(h,Barbarian,-RageDecay);
    if(raging && (i.flags & FBerserker)!=0)Items.Give(p,"LP_LegBerserkerBuff",300);
   }
   if((i.flags & FBond)!=0 && p.Spawned)Bond(p);
   ClassSubclass.Tick250(h); // 子职业
  }
  public static void Cleanup(){if(Bonded.Count>0)Bonded.RemoveWhere(a=>a==null || a.Dead || a.Destroyed || !Items.Has(a,bondDef.Def));}

  // ---------- 主动能力 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;float s=ClassEffects.Strength(p,e);
   switch(e.effect){
    case "Rage":{var r=Items.Give(p,"LP_Rage",ClassEffects.Duration(p,e),s) as Hediff_Rage;if(r!=null){r.Severity=s;r.relentlessUsed=false;}Items.Mote(p,"狂暴",RageColor);return true;}
    case "PrimalRoar":PrimalRoar(p,ClassEffects.Radius(p,e),ClassEffects.Duration(p,e),s);return true;
    case "EarthShatter":EarthShatter(p,ClassEffects.Radius(p,e),ClassEffects.Power(p,e));return true;
    case "HuntersMark":if(t!=null && !t.Dead && t.HostileTo(p))MarkTarget(p,t,"LP_RangerMark",ClassEffects.Duration(p,e),s,HuntColor,"猎人印记");return true;
    case "DeathMark":if(t!=null && !t.Dead && t.HostileTo(p))MarkTarget(p,t,"LP_DeathMark",ClassEffects.Duration(p,e),s,ShadowColor,"死亡印记");return true;
    case "Volley":Volley(p,target.Cell,e.radius,6,1,ClassEffects.Power(p,e));Items.Mote(p,"齐射",HuntColor);return true;
    case "ArrowStorm":Volley(p,target.Cell,e.radius,10,3,ClassEffects.Power(p,e));Items.Mote(p,"万箭齐发",HuntColor);return true;
    case "Assassinate":if(t!=null && !t.Dead && t.HostileTo(p))Assassinate(p,t,s);return true;
   }
   return false;
  }
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   switch(e.effect){
    case "HuntersMark":case "DeathMark":case "Assassinate":{var t=target.Thing as Pawn;if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;}
   }
   return null;
  }
  public static bool Boss(Pawn t)=>ClassLegendary.Boss(t) || t.BodySize>=3f;
  public static int PrimalRoar(Pawn p,float radius,int ticks,float s){
   if(p.Map==null)return 0;int fled=0;float chance=Mathf.Min(RoarFearMax,RoarFear*s);
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,radius).ToList()){
    if(t==p || t.Dead || !t.HostileTo(p))continue;
    Items.Give(t,"LP_Shaken",ticks,s);
    if(!t.Downed && !Boss(t) && (t.RaceProps.Humanlike || t.RaceProps.Animal) && Roll<chance && t.mindState?.mentalStateHandler!=null && t.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"野性怒吼",true))fled++;
   }
   Items.Mote(p,"野性怒吼",RageColor);
   return fled;
  }
  public static int EarthShatter(Pawn p,float radius,float amount){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var t in Races124.PawnsNear(map,p.Position,radius).ToList()){
    if(t==p || t.Dead || !t.HostileTo(p))continue;
    t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,amount,.3f,-1f,p));n++;
    if(!t.Dead)t.stances?.stunner?.StunFor(180,p);
   }
   FleckMaker.ThrowDustPuffThick(p.DrawPos,map,4f,new Color(.55f,.45f,.35f));
   Items.Mote(p,"大地崩裂",RageColor);
   return n;
  }
  public static Hediff_ClassMark MarkTarget(Pawn caster,Pawn t,string name,int ticks,float s,Color c,string label){
   var h=Items.Give(t,name,ticks,s) as Hediff_ClassMark;
   if(h!=null){h.caster=caster;h.Severity=s;}
   Items.Mote(t,label,c);
   return h;
  }
  public static int Volley(Pawn p,IntVec3 cell,float radius,int max,int shots,float damage){
   var map=p.Map;if(map==null || !cell.IsValid)return 0;int n=0;
   var foes=Races124.PawnsNear(map,cell,radius).Where(t=>t!=p && !t.Dead && t.HostileTo(p)).OrderBy(t=>t.Position.DistanceToSquared(cell)).Take(max).ToList();
   foreach(var t in foes)for(int k=0;k<shots;k++)if(Martial.Fire(p,t,new ShotMod{damage=damage,caster=p},true)!=null)n++;
   return n;
  }
  public static bool Unaware(Pawn a,Pawn t)=>!ClassSubclass.Unsurprisable(t) && (!Targeting(t,a) || Hidden(a) || t.Downed); // 野性直觉：不会被偷袭
  public static float AssassinFactor(Pawn a,Pawn t,float s)=>Unaware(a,t)?AssassinBase+s:AssassinAware;
  public static void Assassinate(Pawn p,Pawn t,float s){
   float f=AssassinFactor(p,t,s);
   Items.Give(p,"LP_AssassinStrike",60,f);
   try{ClassMelee.Strike(p,t);}finally{Items.Remove(p,"LP_AssassinStrike");}
   Items.Mote(t,f>AssassinAware?"刺杀":"突刺",ShadowColor);
  }
  public static int ResetRogue(Pawn p){
   var list=p?.abilities?.abilities;if(list==null)return 0;int n=0;var r=Rogue;
   foreach(var a in list)if(ClassSystem.ClassOf(a.def)==r && a.OnCooldown){a.ResetCooldown();n++;}
   return n;
  }

  // ---------- 分页说明 ----------
  public static void Lines(Pawn p,MartialInfo i,List<string> list){
   if(i.ranger>0){var h=Adventurer.Get(p);var l=h?.classes?.favored;list.Add("宿敌："+(l==null || l.Count==0?"未选择":string.Join("、",l.Select(FoeLabel)))+(OpenFoeSlots(h)>0?"（还能选 "+OpenFoeSlots(h)+" 种）":"")+"|宿敌（游侠 1 级，"+SecondFoeLevel+" 级再选一种）：对它伤害 +"+(FavoredDamage*FavoredFactor(p)).ToStringPercent()+"、近战命中 +"+(FavoredHit*FavoredFactor(p)).ToStringPercent()+"、射击命中 ×"+(1f+FavoredHit*FavoredFactor(p)).ToString("0.0#")+"。");}
   if(i.rogue>0){var s=DefCache<StatDef>.Get("LP_CunningPower");list.Add("偷袭：伤害 +"+SneakBonus(i.rogue,s==null?1f:p.GetStatValue(s)).ToStringPercent()+"|偷袭（游荡者）：目标倒地、被击晕、逃跑、没把你当目标，或身边有你的友军时触发，每秒最多一次。伤害 ×(1 + (0.2 + 0.03 ×（游荡者等级 − 1）) × 诡术强度)。");}
  }
 }

 // 狂暴：严重度 = 狂怒强度（近战伤害 ×(1.15 + 0.1 × 严重度)）；记着这次狂暴里不灭之怒用过没有。
 public class Hediff_Rage : Hediff_ScaledBuff {
  public bool relentlessUsed;int subAcc;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref relentlessUsed,"lpRelentlessUsed");}
  // 子职业：狂暴中每秒（狂乱、无脑狂怒、风暴光环），狂暴结束（狂乱的疲惫）。
  public override void TickInterval(int delta){base.TickInterval(delta);if(ClassSubclass.Users.Count>0 && (subAcc+=delta)>=60){subAcc=0;ClassSubclass.RageTick(pawn);}}
  public override void PostRemoved(){base.PostRemoved();if(ClassSubclass.Users.Count>0)ClassSubclass.RageEnded(pawn);}
 }
 // 猎人印记 / 死亡印记：记标记者。
 public class Hediff_ClassMark : HediffWithComps {
  public Pawn caster;
  public override string LabelInBrackets=>caster==null?base.LabelInBrackets:caster.LabelShort;
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref caster,"lpMarkCaster");}
 }

 // 选宿敌的窗口（不用浮动菜单）。
 public class Dialog_ChooseFavored : Window {
  readonly Pawn pawn;
  public override Vector2 InitialSize=>new Vector2(420f,480f);
  public Dialog_ChooseFavored(Pawn p){pawn=p;doCloseX=true;doCloseButton=true;absorbInputAroundWindow=true;forcePause=true;}
  public override void DoWindowContents(Rect r){
   var h=Adventurer.Get(pawn);if(h?.classes==null){Close();return;}
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(r.x,r.y,r.width,32f),pawn.LabelShort+" 的宿敌");Text.Font=GameFont.Small;
   float y=r.y+36f;int open=ClassMartial2.OpenFoeSlots(h);
   Widgets.Label(new Rect(r.x,y,r.width,24f),open>0?"还能选 "+open+" 种（选定后不能更改）":"宿敌已经选满");y+=28f;
   foreach(var t in ClassMartial2.FoeTypes){
    bool have=h.classes.favored.Contains(t);var b=new Rect(r.x,y,r.width,30f);
    if(Widgets.ButtonText(b,ClassMartial2.FoeLabel(t)+(have?"（已选）":""),active:!have && open>0) && !have && open>0){ClassMartial2.PickFoe(h,t);Messages.Message(pawn.LabelShort+"把"+ClassMartial2.FoeLabel(t)+"定为宿敌。",pawn,MessageTypeDefOf.PositiveEvent,false);if(ClassMartial2.OpenFoeSlots(h)<=0)Close();}
    y+=34f;
   }
  }
 }

 // ==================== Harmony ====================
 // 伤害：宿敌、猎人印记、伏击、偷袭、死亡印记、幸运一击、淬毒、残暴重击（攻击方）；狂暴减伤、危险感知、诡诈闪避（受击方）；兽伴对主人宿敌 +20%；怒气。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class ClassDamageMods2 {
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(Martial.Cache.Count==0 && ClassMartial2.Bonded.Count==0 && ClassSubclass.Users.Count==0)return true;
   var def=dinfo.Def;if(def==null || !def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return true;
   var a=dinfo.Instigator as Pawn;float f=1f;
   if(a!=null && a!=__instance){
    if(Martial.Is(a))f*=ClassMartial2.Out(a,__instance,dinfo);
    else if(ClassMartial2.Bonded.Count>0 && ClassMartial2.Bonded.Contains(a) && ClassMartial2.Favored(a.playerSettings?.Master,__instance))f*=1f+ClassMartial2.BondDamage;
    else if(ClassSubclass.Companions.Count>0 && ClassSubclass.Companions.Contains(a))f*=ClassSubclass.CompanionOut(a,__instance); // 兽王伙伴
   }
   if(ClassSubclass.Users.Count>0)f*=ClassSubclass.Ward(__instance,dinfo); // 庇护风暴、伙伴守护
   if(Martial.Is(__instance)){
    float g=ClassMartial2.In(__instance,a,dinfo);
    if(g<0f){absorbed=true;return false;}
    f*=g;
   }
   if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
   return true;
  }
 }
 // 近战命中：鲁莽攻击 +20%、宿敌 +20%（屠戮宿敌 +40%）、宿敌克星必中；幸运一击把落空改成命中。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class ClassMeleeHit2 {
  static void Postfix(Verb_MeleeAttack __instance,LocalTargetInfo target,ref float __result){
   if(Martial.Cache.Count==0)return;var a=__instance.CasterPawn;var t=target.Thing as Pawn;
   if(ClassSubclass.Users.Count>0){ // 子职业：受击方（暗处）、攻击方（稳固目标必中、追猎者的乱舞再判定一次）
    if(t!=null)__result*=ClassSubclass.HitAgainst(t,false);
    if(a!=null){if(ClassSubclass.SteadyHit(a)){__result=1f;return;}if(ClassSubclass.Flurry(a))__result=1f-(1f-__result)*(1f-__result);}
   }
   if(a==null || !Martial.Is(a))return;
   float b=0f;if(Items.Has(a,"LP_RecklessAttack"))b+=.2f;
   if(t!=null && ClassMartial2.Favored(a,t)){if(ClassMartial2.Has(a,ClassMartial2.FFoeBane)){__result=1f;return;}b+=ClassMartial2.FavoredHit*ClassMartial2.FavoredFactor(a);}
   if(b!=0f)__result=Mathf.Clamp01(__result+b);
   if(__result<1f && ClassMartial2.LuckReady(a) && Rand.Value>=__result){
    var h=Adventurer.Get(a);h.classes.legendaryTicks["luck"]=Math.Max(1,Find.TickManager.TicksGame);ClassMartial2.luckCrit.Add(a.thingIDNumber);__result=1f;
    Items.Mote(a,"幸运一击",ClassMartial2.ShadowColor);
   }
  }
 }
 // 射击：宿敌 ×1.2（屠戮宿敌 ×1.4），宿敌克星必中。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class ClassShot2 {
  static readonly AccessTools.StructFieldRef<ShotReport,float> Cover=AccessTools.StructFieldRefAccess<ShotReport,float>("coversOverallBlockChance");
  static readonly AccessTools.StructFieldRef<ShotReport,float> Shooter=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromShooterAndDist");
  static void Postfix(Thing caster,Verb verb,LocalTargetInfo target,ref ShotReport __result){
   if(Martial.Cache.Count==0 || !(caster is Pawn p) || verb==null || verb.IsMeleeAttack || !(target.Thing is Pawn t))return;
   if(ClassSubclass.Users.Count>0){ // 子职业：受击方（鹰之图腾、暗处）、攻击方（稳固目标必中、追猎者的乱舞）
    float g=ClassSubclass.HitAgainst(t,true);if(g!=1f)Shooter(ref __result)*=g;
    if(ClassSubclass.SteadyHit(p)){Cover(ref __result)=0f;Shooter(ref __result)=Mathf.Max(Shooter(ref __result),10f);return;}
    if(ClassSubclass.Flurry(p)){float s=Mathf.Clamp01(Shooter(ref __result));Shooter(ref __result)=1f-(1f-s)*(1f-s);}
   }
   if(!Martial.Is(p) || !ClassMartial2.Favored(p,t))return;
   if(ClassMartial2.Has(p,ClassMartial2.FFoeBane)){Cover(ref __result)=0f;Shooter(ref __result)=Mathf.Max(Shooter(ref __result),10f);return;}
   Shooter(ref __result)*=1f+ClassMartial2.FavoredHit*ClassMartial2.FavoredFactor(p);
  }
 }
 // 不灭之怒：狂暴中本应倒地时撑 10 秒（倒地判定前先检查；撑住期间 ShouldBeDowned 返回 false）。
 [HarmonyPatch(typeof(Pawn_HealthTracker),"CheckForStateChange")]
 public static class ClassRelentless {
  static void Prefix(Pawn ___pawn){if(Martial.Cache.Count>0 && Martial.Is(___pawn))ClassMartial2.TryRelentless(___pawn);}
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.ShouldBeDowned))]
 public static class ClassRelentlessHold {
  static void Postfix(Pawn ___pawn,ref bool __result){if(__result && ClassMartial2.Holding(___pawn))__result=false;}
 }
 // 野外穿行：地形、积雪、植物的移动代价少 75%。
 [HarmonyPatch(typeof(Pawn_PathFollower),"CostToMoveIntoCell",new[]{typeof(Pawn),typeof(IntVec3)})]
 public static class ClassWoodstride {
  public const float Cut=.75f;
  static void Postfix(Pawn __0,IntVec3 __1,ref float __result){
   if(ClassMartial2.Woodstriders.Count==0 && ClassSubclass.Climbers.Count==0 || __0==null)return;
   bool climb=ClassSubclass.Climbers.Count>0 && ClassSubclass.Climbers.Contains(__0); // 子职业·盗贼 飞檐走壁：全免
   if(!climb && !ClassMartial2.Woodstriders.Contains(__0))return;
   var map=__0.Map;if(map==null || !__1.InBounds(map))return;
   __result=Mathf.Max(1f,__result-Extra(map,__1)*(climb?1f:Cut));
  }
  public static float Extra(Map map,IntVec3 c){
   float extra=c.GetTerrain(map)?.pathCost??0;
   if(map.snowGrid!=null)extra+=WeatherBuildupUtility.MovementTicksAddOn(map.snowGrid.GetCategory(c));
   var plant=c.GetPlant(map);if(plant!=null)extra+=plant.def.pathCost;
   return Mathf.Max(0f,extra);
  }
 }
 // 死亡印记：目标死在印记里，游荡者能力冷却完。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class ClassDeathMarkKill {
  static void Postfix(Pawn __instance){
   if(!__instance.Dead || Martial.Cache.Count==0)return;
   var m=ClassMartial2.Mark(__instance,"LP_DeathMark");var c=m?.caster;if(c==null || c.Dead)return;
   if(ClassMartial2.ResetRogue(c)>0)Items.Mote(c,"死亡印记",ClassMartial2.ShadowColor);
  }
 }
 // 动物伙伴（1.70.0 野性等级）：主人是游侠也算。
 [HarmonyPatch(typeof(BeastLevels),nameof(BeastLevels.Companion))]
 public static class ClassRangerCompanion {
  static void Postfix(Pawn master,ref bool __result){if(!__result && master!=null && ClassMartial2.Ranger!=null && ClassSystem.Level(master,ClassMartial2.Ranger)>0)__result=true;}
 }
}
