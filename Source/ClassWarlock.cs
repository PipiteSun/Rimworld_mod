using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 邪术师（见 ArtSource/WarlockNpc-spec.md、Defs/ClassWarlock.xml）：资源契约法力（上限 2 + 等级/4），连续 1 小时冥想 / 娱乐 / 睡觉（短休）回满；
 // 宗主是子职业（LP_ClassDef.subclassLevel = 1，邪术师 1 级就选）；魔能祈唤是宗主定义里的两个分支（invoc1 / invoc2，0 苦痛魔能爆、1 魔鬼视觉、2 魔能长矛）。
 // 被动按能力位（Users），每 250 刻在 Tick250 更新；伤害挂钩（WarlockDamage）、击杀挂钩（WarlockKill）第一步都是空集合判断。
 public static class Warlock {
  static readonly DefRef<LP_ClassDef> cls=new DefRef<LP_ClassDef>("LP_Class_Warlock");
  public static LP_ClassDef Class=>cls.Def;
  public const string Fiend="LP_Sub_Fiend",Archfey="LP_Sub_Archfey",GreatOldOne="LP_Sub_GreatOldOne",RestKey="wlRest";
  public const int RestTicks=2500,BeguileStun=120,DelirumShaken=600;
  public const float BlastRange=24f,SpearRange=36f,HexBonus=.25f,HexSave=.15f,FiendPactFactor=1.25f,AvatarFactor=1.5f,DarkLuckFactor=.9f,ResilienceFactor=.5f,ThoughtFactor=.5f,EntropicFactor=.85f,BeguileChance=.2f,BossBreak=.5f;
  public const int Agonizing=0,DevilSight=1,Spear=2;
  public static readonly Color Eldritch=new Color(.6f,.4f,.85f);

  // ---------- 等级与资源 ----------
  public static int Level(Pawn p)=>ClassSystem.Level(p,Class);
  public static int Level(Hediff_Adventurer h)=>ClassSystem.Level(h,Class);
  public static int MaxPact(int level)=>level<=0?0:2+level/4;
  public static int Beams(int level)=>level>=15?4:level>=10?3:level>=5?2:1; // 1.87.0：5 / 11 / 17 → 5 / 10 / 15
  public static int Beams(Pawn p)=>Beams(Level(p))+(S(p,FSoul)?1:0);
  public static bool Invocation(Hediff_Adventurer h,int option)=>ClassSubclass.Choice(h,"invoc1")==option || ClassSubclass.Choice(h,"invoc2")==option;
  public static bool Invocation(Pawn p,int option)=>Invocation(Adventurer.Get(p),option);
  public static float Range(Pawn p)=>Invocation(p,Spear)?SpearRange:BlastRange;
  // 苦痛魔能爆：每道 + 魅力调整值 × 2（至少 +2）。
  public static float AgonizingBonus(Pawn p)=>Invocation(p,Agonizing)?Mathf.Max(2f,2f*Adventurer.Mod(Adventurer.Score(p,Adventurer.Cha))):0f;
  public static bool Resting(Pawn p)=>p!=null && (!p.Awake() || ClassSystem.Meditating(p) || p.CurJobDef?.joyKind!=null);
  public static int RestProgress(Hediff_Adventurer h){var d=ClassSubclass.Data(h);if(d==null)return 0;d.ticks.TryGetValue(RestKey,out int t);return t;}
  // 短休：连续休息满 1 小时，契约法力回满（每 250 刻累计一次，中断就重来）。
  public static bool Rest(Hediff_Adventurer h,bool resting,int ticks=250){
   var d=ClassSubclass.Data(h);if(d==null)return false;
   if(!resting){d.ticks.Remove(RestKey);return false;}
   d.ticks.TryGetValue(RestKey,out int t);t+=ticks;
   if(t<RestTicks){d.ticks[RestKey]=t;return false;}
   d.ticks.Remove(RestKey);
   int max=ClassSystem.MaxResource(h,Class);if(ClassSystem.Resource(h,Class)>=max-.001f)return false;
   ClassSystem.AddResource(h,Class,max);
   if(h.pawn?.Faction!=null && h.pawn.Faction.IsPlayer)Items.Mote(h.pawn,"契约法力回满",Eldritch);
   return true;
  }
  // 邪魔宗主 + 九狱 / 深渊的异界契约：邪术师能力 ×1.25；宗主化身 ×1.5。
  public static bool PactSynergy(Pawn p){if(!ClassSubclass.Has(p,Fiend))return false;var pact=Pacts.Of(p);var pl=pact?.patron?.plane;return pl==OutsiderPlane.Hells || pl==OutsiderPlane.Abyss;}
  static readonly DefRef<StatDef> pactPower=new DefRef<StatDef>("LP_PactPower");
  public static bool Ours(CompProperties_ClassEffect e)=>e?.stat!=null && e.stat==pactPower.Def;
  public static float PowerFactor(Pawn p,CompProperties_ClassEffect e){
   if(!Ours(e) || p==null)return 1f;float f=1f;
   if(e.effect!="PowerBuff" && Items.Has(p,"LP_LegPatronAvatarBuff"))f*=AvatarFactor;
   if(PactSynergy(p))f*=FiendPactFactor;
   return f;
  }
  public static bool Free(Pawn p,CompProperties_ClassEffect e)=>Ours(e) && e.effect!="PowerBuff" && Items.Has(p,"LP_LegPatronAvatarBuff");

  // ---------- 被动能力位 ----------
  public const int FBless=1,FLuck=2,FResilience=4,FBeguile=8,FAwakened=16,FEntropic=32,FThought=64,FSoul=128,FSight=256;
  static readonly Dictionary<string,int> FlagOf=new Dictionary<string,int>{{"LP_WL_DarkBlessing",FBless},{"LP_WL_DarkLuck",FLuck},{"LP_WL_FiendResilience",FResilience},{"LP_WL_BeguilingDefense",FBeguile},
   {"LP_WL_AwakenedMind",FAwakened},{"LP_WL_EntropicWard",FEntropic},{"LP_WL_ThoughtShield",FThought},{"LP_LegEldritchSoul",FSoul}};
  public static readonly Dictionary<int,int> Users=new Dictionary<int,int>();
  public static int Flags(Pawn p){if(p==null || Users.Count==0)return 0;Users.TryGetValue(p.thingIDNumber,out int f);return f;}
  public static bool S(Pawn p,int f)=>(Flags(p) & f)!=0;
  public static int Refresh(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return 0;int f=0;
   if(!p.Dead && Level(h)>0){var list=p.abilities?.abilities;if(list!=null)for(int k=0;k<list.Count;k++)if(FlagOf.TryGetValue(list[k].def.defName,out int b))f|=b;if(Invocation(h,DevilSight))f|=FSight;}
   if(f==0)Users.Remove(p.thingIDNumber);else Users[p.thingIDNumber]=f;
   return f;
  }
  static void Passive(Pawn p,string hediff,bool on){if(on){if(!Items.Has(p,hediff))Items.Give(p,hediff);}else if(Items.Has(p,hediff))Items.Remove(p,hediff);}
  // ClassSystem.Tick250 → 这里（不是邪术师的人一次字典查找就返回）。
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;if(p==null)return;
   if(Level(h)<=0){if(Users.Count>0 && Users.Remove(p.thingIDNumber))foreach(var n in PassiveHediffs)Items.Remove(p,n);return;}
   if(p.Dead)return;
   int f=Refresh(h);
   Rest(h,Resting(p));
   Passive(p,"LP_WL_DarkLuckBuff",(f & FLuck)!=0);Passive(p,"LP_WL_FeyDefenseBuff",(f & FBeguile)!=0);
   Passive(p,"LP_WL_AwakenedMindBuff",(f & FAwakened)!=0);Passive(p,"LP_WL_ThoughtShieldBuff",(f & FThought)!=0);
   bool sight=(f & FSight)!=0;
   Passive(p,"LP_WL_DevilSightBuff",sight && p.Spawned && ClassSubclass.Dark(p));
   if(sight){Items.Remove(p,"LP_WL_Darkened");Items.Remove(p,"LP_MagicalDarkness");}
  }
  static readonly string[] PassiveHediffs={"LP_WL_DarkLuckBuff","LP_WL_FeyDefenseBuff","LP_WL_AwakenedMindBuff","LP_WL_ThoughtShieldBuff","LP_WL_DevilSightBuff"};

  // ---------- 咒术 ----------
  public static Hediff_ClassMark Hex(Pawn v)=>Hediff_Hex.Live<=0 || v==null?null:ClassMartial2.Mark(v,"LP_WL_Hexed");
  // 豁免成功率 −15%：DNDMind.TryResist 直接减，属性豁免（Legendary.SaveBonus）−3（每点 5%）。
  public static float SavePenalty(Pawn v)=>Hex(v)!=null?HexSave:0f;
  public static int SaveBonusPenalty(Pawn v)=>Hex(v)!=null?Mathf.RoundToInt(HexSave*20f):0;

  // ---------- 伤害（WarlockDamage）与击杀（WarlockKill） ----------
  public static float DamageFactor(Pawn v,DamageInfo d){
   float f=1f;var a=d.Instigator as Pawn;
   if(a!=null && a!=v && Hediff_Hex.Live>0){var m=Hex(v);if(m!=null && m.caster==a)f*=1f+HexBonus;}
   int g=Flags(v);if(g==0)return f;
   if((g & FLuck)!=0)f*=DarkLuckFactor;
   if((g & FResilience)!=0 && ClassSubclass.Element(d.Def)==0)f*=ResilienceFactor;
   if((g & FThought)!=0 && ClassSubclass.Psychic(d.Def))f*=ThoughtFactor;
   if((g & FEntropic)!=0 && ForceDragon.IsRanged(d))f*=EntropicFactor;
   if((g & FBeguile)!=0 && a!=null && a!=v && a.HostileTo(v) && HeroCombat.Melee(d) && Rand.Chance(BeguileChance)){a.stances?.stunner?.StunFor(BeguileStun,v);Items.Mote(a,"被迷住了",new Color(.5f,.8f,.6f));}
   return f;
  }
  public static void Killed(Pawn victim,Pawn killer){
   int g=Flags(killer);if(g==0 || killer.Dead || !victim.HostileTo(killer))return;
   var h=Adventurer.Get(killer);if(h==null)return;
   if((g & FBless)!=0){float healed=ClassMelee.Heal(killer,5f+Level(h)/2f);if(healed>0f)Items.Mote(killer,"黑暗祝福",new Color(.85f,.4f,.35f));}
   if((g & FSoul)!=0)ClassSystem.AddResource(h,Class,1f);
  }

  // ---------- 主动能力 ----------
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.HostileTo(a);
  static IEnumerable<Pawn> Near(Pawn p,IntVec3 c,float r)=>p.Map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(p.Map,c,r);
  static bool Boss(Pawn t)=>ClassLegendary.Boss(t) || Legendary.Is(t);
  static readonly HashSet<string> Known=new HashSet<string>{"EldritchBlast","WLDarkness","WLHex","WLDominate","WLForceCage","WLMaster","WLRebuke","WLHurl","WLFeyPresence","WLDelirium","WLWhispers","WLThrall"};
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(!Known.Contains(e.effect))return null;var t=target.Thing as Pawn;
   switch(e.effect){
    case "EldritchBlast":case "WLHex":case "WLRebuke":case "WLHurl":case "WLDelirium":case "WLWhispers":case "WLDominate":case "WLThrall":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";
     if(!t.HostileTo(caster))return "只能对敌人使用。";
     if(e.effect=="EldritchBlast"){float r=Range(caster);if(caster.Position.DistanceTo(t.Position)>r)return "超出魔能爆的射程（"+r.ToString("0")+" 格）。";}
     if(e.effect=="WLDominate" && !t.RaceProps.Humanlike)return "只能支配人形。";
     if(e.effect=="WLThrall"){if(!t.RaceProps.Humanlike)return "只能对人形使用。";if(!t.Downed)return "目标要先倒地。";if(Boss(t))return "剧情首领和传奇单位不会变成奴仆。";}
     return null;
    case "WLDarkness":case "WLForceCage":{var map=caster.Map;var c=target.Cell;if(map==null || !c.IsValid || !c.InBounds(map))return "需要选择一个位置。";return null;}
   }
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(!Known.Contains(e.effect))return false;
   var t=target.Thing as Pawn;float s=ClassEffects.Strength(p,e),pw=ClassEffects.Power(p,e);int dur=ClassEffects.Duration(p,e);float rad=ClassEffects.Radius(p,e);
   switch(e.effect){
    case "EldritchBlast":if(Hostile(p,t))Blast(p,t,pw);break;
    case "WLDarkness":Darkness(p,target.Cell,rad,dur);break;
    case "WLHex":if(Hostile(p,t)){foreach(var old in Hexed.Where(x=>x!=t && Hex(x)?.caster==p).ToList())Items.Remove(old,"LP_WL_Hexed");ClassMartial2.MarkTarget(p,t,"LP_WL_Hexed",dur,s,Eldritch,"咒术");Hexed.Add(t);}break;
    case "WLDominate":if(Hostile(p,t))Dominate(p,t,dur);break;
    case "WLForceCage":ForceCage(p,target.Cell,rad,dur);break;
    case "WLMaster":{var h=Adventurer.Get(p);if(h!=null){ClassSystem.AddResource(h,Class,ClassSystem.MaxResource(h,Class));Items.Mote(p,"秘法大师：契约法力回满",Eldritch);}break;}
    case "WLRebuke":if(Hostile(p,t)){t.TakeDamage(new DamageInfo(DamageDefOf.Flame,pw,.2f,-1f,p));if(!t.Dead)t.TryAttachFire(.4f,p);Line(p,t);Items.Mote(t,"炼狱叱喝",new Color(.95f,.45f,.3f));}break;
    case "WLHurl":if(Hostile(p,t)){t.TakeDamage(new DamageInfo(DamageDefOf.Flame,pw,.4f,-1f,p));if(!t.Dead){t.stances?.stunner?.StunFor(300,p);Items.Give(t,"LP_Shaken",600,1f);}Items.Mote(t,"掷入地狱",new Color(.95f,.35f,.25f));}break;
    case "WLFeyPresence":foreach(var x in Near(p,p.Position,rad).ToList())if(Hostile(p,x) && !x.Downed){if(DNDMind.TryResist(x))Items.Mote(x,"抵抗！",Eldritch);else ClassSubclass.Fear(x,dur,"妖精风采");}Items.Mote(p,"妖精风采",new Color(.5f,.8f,.6f));break;
    case "WLDelirium":if(Hostile(p,t))Delirium(p,t,dur);break;
    case "WLWhispers":if(Hostile(p,t)){t.TakeDamage(new DamageInfo(PsychicDamage,pw,1f,-1f,p));if(!t.Dead && !DNDMind.TryResist(t))ClassSubclass.Fear(t,dur,"不谐低语");Items.Mote(t,"不谐低语",new Color(.45f,.7f,.75f));}break;
    case "WLThrall":Thrall(p,t);break;
   }
   return true;
  }
  public static readonly HashSet<Pawn> Hexed=new HashSet<Pawn>();
  public static DamageDef PsychicDamage=>DefCache<DamageDef>.Get("LP_SilverPsychic")??DefCache<DamageDef>.Get("LP_ArcaneForce")??DamageDefOf.Blunt;
  public static DamageDef ForceDamage=>DefCache<DamageDef>.Get("LP_ArcaneForce")??DamageDefOf.Blunt;
  static void Line(Pawn p,Thing t){if(p.Map==null)return;var line=DefCache<FleckDef>.Get("PsycastPsychicLine");if(line!=null)FleckMaker.ConnectingLine(p.DrawPos,t.DrawPos,line,p.Map);}
  // 魔能爆：按等级 1～4 道；目标倒下后剩下的光束打 6 格内另一个敌人。返回打出的光束数。
  public static int Blast(Pawn p,Pawn t,float perBeam){
   int beams=Beams(p),n=0;float amount=perBeam+AgonizingBonus(p);var target=t;
   for(int i=0;i<beams;i++){
    if(target==null || target.Dead || !target.Spawned){target=target?.MapHeld==null?null:Races124.PawnsNear(target.MapHeld,target.PositionHeld,6f).Where(x=>Hostile(p,x) && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(t.PositionHeld)).FirstOrDefault();if(target==null)break;}
    Line(p,target);target.TakeDamage(new DamageInfo(ForceDamage,amount,.3f,-1f,p));n++;
   }
   Items.Mote(p,"魔能爆"+(n>1?" ×"+n:""),Eldritch);
   return n;
  }
  public static int Darkness(Pawn p,IntVec3 c,float radius,int ticks){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(map))FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1.2f,new Color(.12f,.08f,.18f));
   foreach(var x in Near(p,c,radius).ToList())if(Hostile(p,x) && !Invocation(x,DevilSight)){Items.Give(x,"LP_WL_Darkened",ticks,1f);n++;}
   return n;
  }
  public static bool Dominate(Pawn p,Pawn t,int ticks,float roll=-1f){
   if(roll<0f)roll=Rand.Value;
   if(Boss(t) && roll<BossBreak || DNDMind.TryResist(t)){Items.Mote(t,"抵抗了支配",Eldritch);return false;}
   var msh=t.mindState?.mentalStateHandler;
   if(msh==null || !msh.TryStartMentalState(MentalStateDefOf.Berserk,"被邪术师支配",true,true)){Items.Give(t,"LP_Shaken",ticks,1f);return false;}
   if(t.MentalState!=null)t.MentalState.forceRecoverAfterTicks=ticks;
   Line(p,t);Items.Mote(t,"被支配",Eldritch);return true;
  }
  public static int ForceCage(Pawn p,IntVec3 c,float radius,int ticks){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var x in Near(p,c,radius).ToList()){
    if(!Hostile(p,x))continue;int k=Boss(x)?ticks/2:ticks;
    x.stances?.stunner?.StunFor(k,p,false);Items.Give(x,"LP_WL_Caged",k,1f);n++;
   }
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(map))FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(),map,.6f);
   Items.Mote(p,"力场监牢",Eldritch);return n;
  }
  public static void Delirium(Pawn p,Pawn t,int ticks){
   Items.Give(t,"LP_Shaken",ticks,1f);
   if(Boss(t)){Items.Mote(t,"震慑",Eldritch);return;}
   if(DNDMind.TryResist(t)){Items.Mote(t,"抵抗！",Eldritch);return;}
   ClassSubclass.Fear(t,ticks,"黑暗谵妄",false);Items.Mote(t,"黑暗谵妄",new Color(.5f,.8f,.6f));
  }
  public static bool Thrall(Pawn p,Pawn t){
   if(t==null || t.Dead || !t.Downed || !t.RaceProps.Humanlike || Boss(t) || !t.HostileTo(p))return false;
   if(DNDMind.TryResist(t)){Items.Mote(t,"抵抗！",Eldritch);return false;}
   if(p.Faction==Faction.OfPlayer)InteractionWorker_RecruitAttempt.DoRecruit(p,t);else t.SetFaction(p.Faction);
   Items.Mote(t,"成了奴仆",new Color(.45f,.7f,.75f));
   return t.Faction==p.Faction;
  }
 }

 // 咒术：Live = 地图上还在的个数（伤害 / 豁免挂钩的快速返回，同 Hediff_Ward 的写法）。
 public class Hediff_Hex : Hediff_ClassMark {
  public static int Live;
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);Live++;}
  public override void PostRemoved(){base.PostRemoved();if(Live>0)Live--;Warlock.Hexed.Remove(pawn);}
  public override void ExposeData(){base.ExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit){Live++;if(pawn!=null)Warlock.Hexed.Add(pawn);}}
 }
 public class GameComponent_Warlock : GameComponent {
  public GameComponent_Warlock(Game game){}
  public override void FinalizeInit(){base.FinalizeInit();Warlock.Users.Clear();}
  public override void GameComponentTick(){if(Find.TickManager.TicksGame%Martial.HourTicks==2 && Warlock.Hexed.Count>0)Warlock.Hexed.RemoveWhere(x=>x==null || x.Destroyed || Warlock.Hex(x)==null);}
 }

 // ==================== Harmony ====================
 // 咒术增伤（攻击方）；黑暗之运、邪魔韧性、思维屏障、熵能护盾、魅惑防御（受击方）。没有邪术师被动、也没有咒术时第一步返回。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class WarlockDamage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(Warlock.Users.Count==0 && Hediff_Hex.Live<=0)return;
   if(dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   float f=Warlock.DamageFactor(__instance,dinfo);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
 // 黑暗祝福（击杀治疗）、魔能之魂（击杀 +1 契约法力）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class WarlockKill {
  static void Postfix(Pawn __instance,DamageInfo? dinfo){
   if(Warlock.Users.Count==0 || !__instance.Dead)return;
   if(dinfo?.Instigator is Pawn k && k!=__instance)Warlock.Killed(__instance,k);
  }
 }
}
