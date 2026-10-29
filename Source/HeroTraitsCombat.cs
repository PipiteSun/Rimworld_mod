using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.58.0 强力特性的战斗规则（类型伤害、元素之触、图腾之魂、法术抗体、决斗者、宿敌猎人、命运主宰、事迹的减伤）。
 // 每个挂钩第一步：攻击者和受害者都查 HeroTraits.Info（按 pawn 缓存，没有特性 = null），两边都是 null 立刻返回。
 public static class HeroCombat {
  public const float FavoredDamage=.25f,BaneDamage=.4f,FavoredHit=.15f,ProphecyDamage=.25f,ChampionDamage=.2f,DragonslayerDamage=.3f,GiantbaneDamage=.3f,GiantbaneSize=1.5f,LichbaneDamage=.3f,GodslayerDamage=.35f,ReturnedDamage=.2f;
  public const float ElementSpell=1.3f,ElementResist=.5f,GenasiResist=.25f,BearFactor=.65f,BearHealth=.5f,WardFactor=.65f,WardChance=.5f,FiendFire=.5f,BreathFactor=.7f,GodslayerSpell=.65f,FortressPsychic=.5f;
  public const float WolfHit=.15f,WolfDown=.1f,WolfMaxSize=3f,EagleCover=.5f,EagleRange=1.15f,DuelHit=.25f,DuelDodge=.25f,DuelCounter=.5f,SwarmHit=-.1f,ProphetHit=.1f,FeyReflect=.2f,AttunerBonus=.25f,AttunerLoot=.2f,ChainChance=.1f,ChainRange=2f,Sure=999f;
  public const int ElementExtra=3,WolfMarkTicks=480,WolfDownTicks=60,FrostSlowTicks=120,FeyStunTicks=180,BaneKills=50,StaffRangeBonus=3,NoticeCooldown=GenDate.TicksPerDay,SwarmCount=3;
  public static float? TestRoll;public static float Roll=>TestRoll??Rand.Value;
  [ThreadStatic] public static bool extra; // 我们自己追加的元素伤害：不再触发附伤和法术加成
  public static void Reset(){duel.Clear();marks.Clear();counters.Clear();lastPhylacteryHit.Clear();Hediff_HeroBuff.Holders.Clear();}

  // ---------- 伤害分类 ----------
  static readonly Dictionary<DamageDef,int> elementOf=new Dictionary<DamageDef,int>();
  static bool Any(string n,params string[] keys){foreach(var k in keys)if(n.IndexOf(k,StringComparison.Ordinal)>=0)return true;return false;}
  // 1 火 / 2 冰 / 3 雷 / 4 酸（按 defName；强酸先判，免得 AcidBurn 被当成火）。
  public static int Element(DamageDef d){
   if(d==null)return 0;if(elementOf.TryGetValue(d,out int e))return e;var n=d.defName;
   e=Any(n,"Acid","Corros")?4:Any(n,"Frost","Cold","Freez","Chill","Ice")?2:Any(n,"Shock","Lightning","Electric","Thunder")?3:Any(n,"Flame","Fire","Burn","Heat","Inferno","Hellfire")?1:0;
   return elementOf[d]=e;
  }
  static DamageDef[] elementDefs;
  public static DamageDef ElementDef(int e){
   if(elementDefs==null){DamageDef D(string a,string b)=>DefDatabase<DamageDef>.GetNamedSilentFail(a)??DefDatabase<DamageDef>.GetNamedSilentFail(b)??DamageDefOf.Burn;elementDefs=new[]{null,D("Burn","Flame"),D("LP_Frost","Frostbite"),D("LP_Shock","Burn"),D("LP_AlchemyAcid","AcidBurn")};}
   return e>=1 && e<=4?elementDefs[e]:null;
  }
  public static readonly string[] GenasiKeys={"","LP_GenasiFire","LP_GenasiWater","LP_GenasiAir","LP_GenasiEarth"};
  public static bool SameGenasi(Pawn p,int e)=>e>=1 && e<=4 && Affinity.For(p).key==GenasiKeys[e];
  public static bool Psychic(DamageDef d)=>d!=null && Any(d.defName,"Psychic","Psionic","Mind");
  // 法术 / 神术：人造成的、不带武器的伤害（能力、魔能爆、吐息），以及法杖。
  public static bool Spell(DamageInfo d)=>d.Instigator is Pawn && (d.Weapon==null || Arcana.Group(d.Weapon)=="G");
  public static bool Melee(DamageInfo d)=>d.Weapon!=null && d.Def!=null && !d.Def.isRanged && (d.Weapon.IsMeleeWeapon || d.Weapon.race!=null);
  public static bool WeaponHit(DamageInfo d)=>d.Weapon!=null && (d.Weapon.IsWeapon || d.Weapon.race!=null);

  // ---------- 类型伤害（合计封顶 +60%） ----------
  public static float TypeBonus(Pawn a,HeroInfo i,Pawn v,bool melee){
   if(i==null || v==null)return 0f;float b=0f;var r=i.rec;var t=DndCreatureType.Of(v);
   if(i.favored>0 && HeroTraits.FavoredTarget(i,v))b+=r!=null && r.favoredBane?BaneDamage:FavoredDamage;
   if(i.prophesied && r!=null && HeroDestiny.ProphecyTarget(r.prophecy,v))b+=ProphecyDamage;
   if(i.champion>0 && HeroDestiny.ChampionTarget(i.champion,v))b+=ChampionDamage;
   if(i.dragonslayer && (t&DndType.Dragon)!=0)b+=DragonslayerDamage;
   if(i.giantbane && melee && v.BodySize>=GiantbaneSize)b+=GiantbaneDamage;
   if(i.lichbane && (t&DndType.Undead)!=0)b+=LichbaneDamage;
   if(i.godslayer && (t&DndType.Divine)!=0)b+=GodslayerDamage;
   if(i.returned && (t&DndType.Undead)!=0)b+=ReturnedDamage;
   return Mathf.Min(b,HeroTraits.TypeCap);
  }
  public static float OutFactor(Pawn a,HeroInfo i,Pawn v,DamageInfo d){
   float ff=Feats2.OutFactor(a,i,v,d); // 1.58.1 酒馆斗殴者（含社交斗殴）、元素使
   if(!a.HostileTo(v))return ff;
   float f=ff*(1f+TypeBonus(a,i,v,Melee(d)));
   if(i.element>0 && !extra && Element(d.Def)==i.element && Spell(d))f*=ElementSpell;
   var r=i.rec;if(i.prophesied && r!=null && r.masterArmed){r.masterArmed=false;f*=2f;Items.Mote(a,"命运主宰！",new Color(1f,.8f,.3f));}
   return f;
  }
  public static float InFactor(Pawn v,HeroInfo i,Pawn a,DamageInfo d){
   float f=1f;int e=Element(d.Def);
   bool adept=a!=null && Feats2.AdeptIgnores(a,d.Def); // 1.58.1 元素使无视该元素的抗性
   if(i.element>0 && e==i.element && !adept)f*=SameGenasi(v,e)?GenasiResist:ElementResist;
   if(i.totem==1 && !Psychic(d.Def) && v.health.summaryHealth.SummaryHealthPercent<BearHealth)f*=BearFactor;
   if(a!=null && a!=v && a.HostileTo(v) && Spell(d)){
    if(i.warded)f*=WardFactor;
    if(i.dragonslayer && DndCreatureType.Is(a,DndType.Dragon))f*=BreathFactor;
    if(i.godslayer && DndCreatureType.Is(a,DndType.Divine))f*=GodslayerSpell;
   }
   if(i.pact==1 && i.Stage>=2 && e==1 && !adept)f*=FiendFire;
   if(i.fortress && Psychic(d.Def))f*=FortressPsychic;
   return f;
  }

  // ---------- 命中后：元素附伤、狼的标记与扑倒、妖精的迷惑 ----------
  public struct Mark{public Pawn wolf;public int tick;}
  public static readonly Dictionary<Pawn,Mark> marks=new Dictionary<Pawn,Mark>();
  public static void ExpireMarks(int now){if(marks.Count==0)return;foreach(var k in marks.Where(kv=>kv.Key==null || kv.Key.Dead || now-kv.Value.tick>WolfMarkTicks || now<kv.Value.tick).Select(kv=>kv.Key).ToList())marks.Remove(k);}
  public static void AfterHit(Pawn v,Pawn a,HeroInfo ai,DamageInfo d){
   if(!v.HostileTo(a))return;
   if(ai.element>0 && WeaponHit(d) && !SameGenasi(a,ai.element) && !v.Dead){
    extra=true;
    try{
     var def=ElementDef(ai.element);
     v.TakeDamage(new DamageInfo(def,ElementExtra,0f,-1f,a,null,null,DamageInfo.SourceCategory.ThingOrUnknown,v));
     if(ai.element==2 && !v.Dead)Items.Give(v,"LP_HeroFrostSlow",FrostSlowTicks);
     if(ai.element==3 && v.MapHeld!=null && Roll<ChainChance){
      var o=GenRadial.RadialDistinctThingsAround(v.PositionHeld,v.MapHeld,ChainRange,true).OfType<Pawn>().FirstOrDefault(x=>x!=v && x!=a && !x.Dead && x.HostileTo(a));
      if(o!=null){if(o.Spawned)FleckMaker.ThrowLightningGlow(o.DrawPos,o.Map,.8f);o.TakeDamage(new DamageInfo(def,ElementExtra,0f,-1f,a,null,null,DamageInfo.SourceCategory.ThingOrUnknown,o));}
     }
    }finally{extra=false;}
   }
   if(ai.totem==2 && Melee(d)){
    marks[v]=new Mark{wolf=a,tick=GenTicks.TicksGame};
    if(!v.Dead && v.BodySize<WolfMaxSize && Roll<WolfDown){v.stances?.stunner?.StunFor(WolfDownTicks,a,false,true,false);Items.Mote(v,"扑倒！",new Color(.8f,.8f,.7f));}
   }
  }
  public static void Reflect(Pawn v,HeroInfo vi,Pawn a){
   if(vi.pact!=2 || vi.Stage<1 || a.Dead || !a.HostileTo(v) || Roll>=FeyReflect)return;
   a.stances?.stunner?.StunFor(FeyStunTicks,v,false,true,false);Items.Mote(a,"迷惑",new Color(.7f,1f,.7f));
  }

  // ---------- 近战命中 ----------
  public static float MeleeHit(Pawn a,Pawn t){
   float b=0f;
   if(t!=null && marks.Count>0 && marks.TryGetValue(t,out var m) && m.wolf!=null && m.wolf!=a && a.Faction!=null && a.Faction==m.wolf.Faction)b+=WolfHit;
   if(Hediff_HeroBuff.Holders.Count>0 && Hediff_HeroBuff.Holders.Contains(a))b+=ProphetHit;
   var i=HeroTraits.Info(a);
   if(i!=null){
    if(i.prophesied && i.rec!=null && i.rec.masterArmed)return Sure;
    if(t!=null && HeroTraits.FavoredTarget(i,t))b+=FavoredHit;
    if(i.duelist){int n=DuelCount(a);if(n==1)b+=DuelHit;else if(n>=SwarmCount)b+=SwarmHit;}
   }
   return b;
  }

  // ---------- 决斗者：贴身敌人数（250 刻刷新）、闪避反击 ----------
  public static readonly Dictionary<Pawn,int> duel=new Dictionary<Pawn,int>();
  public static void RefreshDuelists(List<Pawn> list){duel.Clear();foreach(var p in list)if(p.Spawned && !p.Dead)duel[p]=AdjacentEnemies(p);}
  public static int AdjacentEnemies(Pawn p){
   int n=0;var map=p.Map;if(map==null)return 0;
   foreach(var c in GenAdj.CellsAdjacent8Way(p)){
    if(!c.InBounds(map))continue;var l=c.GetThingList(map);
    for(int k=0;k<l.Count;k++)if(l[k] is Pawn o && o!=p && !o.Dead && !o.Downed && o.HostileTo(p))n++;
   }
   return n;
  }
  public static int DuelCount(Pawn p)=>duel.TryGetValue(p,out int n)?n:-1;
  public struct Counter{public Pawn from,to;public int tick;}
  public static readonly List<Counter> counters=new List<Counter>();
  public static int Countered; // 自检计数
  public static void QueueCounter(Pawn from,Pawn to){counters.Add(new Counter{from=from,to=to,tick=GenTicks.TicksGame+1});}
  static readonly Func<Verb_MeleeAttack,LocalTargetInfo,DamageWorker.DamageResult> ApplyMelee=(Func<Verb_MeleeAttack,LocalTargetInfo,DamageWorker.DamageResult>)Delegate.CreateDelegate(typeof(Func<Verb_MeleeAttack,LocalTargetInfo,DamageWorker.DamageResult>),AccessTools.Method(typeof(Verb_MeleeAttack),"ApplyMeleeDamageToTarget"));
  public static bool CounterNow(Pawn from,Pawn to){
   if(from==null || to==null || from.Dead || from.Downed || to.Dead || !from.Spawned || !to.Spawned || from.Map!=to.Map || !from.Position.AdjacentTo8WayOrInside(to.Position))return false;
   if(!(from.meleeVerbs?.TryGetMeleeVerb(to) is Verb_MeleeAttack verb))return false;
   try{ApplyMelee(verb,new LocalTargetInfo(to));}catch(Exception e){Log.Warning("[DND边缘大陆] 决斗者反击失败："+e.Message);return false;}
   Countered++;Items.Mote(from,"反击！",new Color(.95f,.85f,.45f));return true;
  }
  public static void RunCounters(){
   int now=GenTicks.TicksGame;
   for(int k=counters.Count-1;k>=0;k--){var c=counters[k];if(c.tick>now)continue;counters.RemoveAt(k);CounterNow(c.from,c.to);}
  }

  // ---------- 宿敌出现在本地图：每人每天提示一次 ----------
  public static int FavoredNotices(List<Pawn> hunters,int now){
   int n=0;
   foreach(var h in hunters){
    if(!h.Spawned || h.Dead)continue;var i=HeroTraits.Info(h);var r=i?.rec;if(r==null)continue;
    if(r.favoredNoticeTick!=int.MinValue && now>=r.favoredNoticeTick && now-r.favoredNoticeTick<NoticeCooldown)continue;
    foreach(var o in h.Map.mapPawns.AllPawnsSpawned){
     if(o.Dead || o.Downed || !o.HostileTo(h) || !HeroTraits.FavoredTarget(i,o))continue;
     r.favoredNoticeTick=now;n++;
     if(!Adventurer.TestSuppressed || HeroTraits.TestNotices)Messages.Message(h.LabelShort+"认出了宿敌："+o.LabelShort+"（"+HeroTraits.FavoredLabels[i.favored]+"）。",o,MessageTypeDefOf.ThreatSmall,false);
     break;
    }
   }
   return n;
  }

  // ---------- 同调者 ----------
  public static bool MagicThing(ThingDef d)=>d!=null && (d.tradeTags!=null && (d.tradeTags.Contains("LP_MagicItem") || d.tradeTags.Contains("LP_MagicWeapon")) || d.HasModExtension<AccessorySlot>() || d.GetCompProperties<CompProperties_WornHediff>()!=null);
  public static IEnumerable<Thing> MagicGear(Pawn p){
   var worn=p?.apparel?.WornApparel;if(worn!=null)for(int k=0;k<worn.Count;k++)if(MagicThing(worn[k].def))yield return worn[k];
   var w=p?.equipment?.Primary;if(w!=null && MagicThing(w.def))yield return w;
  }
  public static int ExtraRings(Pawn p)=>HeroTraits.Info(p)?.attuner==true?1:0;
  // 骇人威仪 / 龙威：屠龙者免疫龙，宿敌克星免疫宿敌。
  public static bool FearImmuneTo(Pawn p,Pawn source){
   var i=HeroTraits.Info(p);if(i==null || source==null)return false;
   if(i.dragonslayer && DndCreatureType.Is(source,DndType.Dragon))return true;
   return i.rec!=null && i.rec.favoredBane && HeroTraits.FavoredTarget(i,source);
  }

  // ---------- 心智：支配、心灵震爆、心灵束缚（DNDMind.TryResist） ----------
  public static bool MindResist(Pawn p){
   var i=HeroTraits.Info(p);if(i==null)return false;
   if(i.fortress){Items.Mote(p,"心智堡垒",new Color(.7f,.85f,1f));return true;}
   if(i.warded && Roll<WardChance){Items.Mote(p,"法术抗体",new Color(.75f,.6f,.95f));return true;}
   return false;
  }
  static readonly HashSet<string> controlStates=new HashSet<string>{"PanicFlee","Berserk","BerserkShort","BerserkTrance","Manhunter","ManhunterPermanent","LP_Dominated"};
  public static bool ControlState(MentalStateDef d){
   if(d==null)return false;var n=d.defName;if(controlStates.Contains(n))return true;
   return n.StartsWith("LP_") && Any(n,"Charm","Fear","Panic","Dominat","Enthrall");
  }

  // ---------- 摧毁命匣的人（巫妖克星） ----------
  public static readonly Dictionary<Phylactery,Pawn> lastPhylacteryHit=new Dictionary<Phylactery,Pawn>();
 }

 // ==================== Harmony ====================
 // 伤害：攻击者的类型加成 / 元素法术 / 命运主宰，受害者的元素抗性 / 熊 / 法术抗体 / 事迹减伤；邪魔活力吸收。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class HeroDamage {
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   var def=dinfo.Def;if(def==null || !def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return true;
   var a=dinfo.Instigator as Pawn;
   float lf=Growth.DamageFactor(a,dinfo)*(Growth.legacyDefs.Count>0?Growth.GuardFactor(__instance):1f); // 1.58.3 传承武器
   if(lf!=1f){dinfo.SetAmount(dinfo.Amount*lf);if(dinfo.Amount<=.001f){absorbed=true;return false;}}
   var vi=HeroTraits.Info(__instance);var ai=a==null || a==__instance?null:HeroTraits.Info(a);
   if(vi==null && ai==null)return true;
   float amt=dinfo.Amount;
   if(ai!=null)amt*=HeroCombat.OutFactor(a,ai,__instance,dinfo);
   if(vi!=null)amt*=HeroCombat.InFactor(__instance,vi,a,dinfo);
   if(vi!=null && vi.pact==1 && amt>0f){
    var h=__instance.health.hediffSet.GetFirstHediffOfDef(HeroDestiny.VigorDef);
    if(h!=null){float take=Mathf.Min(h.Severity,amt);h.Severity-=take;amt-=take;}
   }
   if(Math.Abs(amt-dinfo.Amount)>.0001f)dinfo.SetAmount(amt);
   if(dinfo.Amount<=.001f){absorbed=true;return false;}
   return true;
  }
 }
 // 命中后：元素附伤、狼、妖精迷惑；命匣的最后一击。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class HeroAfterHit {
  static void Postfix(Thing __instance,DamageInfo dinfo,DamageWorker.DamageResult __result){
   if(HeroCombat.extra)return;
   if(__instance is Phylactery ph){if(dinfo.Instigator is Pawn pa && pa.Faction!=null && pa.Faction.IsPlayer)HeroCombat.lastPhylacteryHit[ph]=pa;return;}
   if(!(__instance is Pawn v) || !(dinfo.Instigator is Pawn a) || a==v)return;
   var ai=HeroTraits.Info(a);var vi=HeroTraits.Info(v);
   if(ai==null && vi==null || __result==null || __result.totalDamageDealt<=0f)return;
   if(ai!=null){HeroCombat.AfterHit(v,a,ai,dinfo);Feats2.AfterHit(v,a,ai,dinfo);}
   if(vi!=null && !v.Dead){HeroCombat.Reflect(v,vi,a);Feats2.Struck(v,vi,a);}
  }
 }
 // 近战命中：在幸运重掷之前（优先级高），加在最终命中率上。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class HeroMeleeHit {
  [HarmonyPriority(Priority.High)]
  static void Postfix(Verb_MeleeAttack __instance,LocalTargetInfo target,ref float __result){
   var a=__instance.CasterPawn;if(a==null)return;
   float b=HeroCombat.MeleeHit(a,target.Thing as Pawn)+Growth.SwordHit(a); // 1.58.3 黎明之誓光环
   if(b>=HeroCombat.Sure)__result=1f;else if(b!=0f)__result=Mathf.Clamp01(__result+b);
  }
 }
 // 近战闪避：决斗者单挑时 +25%，由这里先掷（原版再按 0 / 1 判），闪开时 50% 下一刻反击。命运主宰的那一下不能闪。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetDodgeChance")]
 public static class HeroDodge {
  static void Postfix(Verb_MeleeAttack __instance,LocalTargetInfo __0,ref float __result){
   var t=__0.Thing as Pawn;if(t==null)return;var a=__instance.CasterPawn;
   var ai=a==null?null:HeroTraits.Info(a);if(ai!=null && ai.prophesied && ai.rec!=null && ai.rec.masterArmed){__result=0f;return;}
   var vi=HeroTraits.Info(t);if(vi==null || !vi.duelist || HeroCombat.DuelCount(t)!=1)return;
   float c=Mathf.Clamp01(__result+HeroCombat.DuelDodge);
   if(HeroCombat.Roll<c){__result=1f;if(a!=null && Rand.Chance(HeroCombat.DuelCounter))HeroCombat.QueueCounter(t,a);}else __result=0f;
  }
 }
 // 射击：鹰（掩体减半，不和神射手叠加）、宿敌 +15%、为了预言之子 +10%、命运主宰必中。在神射手之后跑。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class HeroShot {
  static readonly AccessTools.StructFieldRef<ShotReport,float> Cover=AccessTools.StructFieldRefAccess<ShotReport,float>("coversOverallBlockChance");
  static readonly AccessTools.StructFieldRef<ShotReport,float> Shooter=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromShooterAndDist");
  [HarmonyPriority(Priority.Low)]
  static void Postfix(Thing caster,Verb verb,LocalTargetInfo target,ref ShotReport __result){
   if(!(caster is Pawn p) || verb==null || verb.IsMeleeAttack)return;
   var i=HeroTraits.Info(p);bool buff=Hediff_HeroBuff.Holders.Count>0 && Hediff_HeroBuff.Holders.Contains(p);
   if(i==null && !buff)return;
   if(i!=null && i.prophesied && i.rec!=null && i.rec.masterArmed){FeatSharpshooterCover.Apply(ref __result);return;}
   float aim=buff?1f+HeroCombat.ProphetHit:1f;
   if(i!=null){
    if(i.totem==3 && !Feats.Has(p,Feats.Sharpshooter))Cover(ref __result)*=HeroCombat.EagleCover;
    if(target.Thing is Pawn t && HeroTraits.FavoredTarget(i,t))aim*=1f+HeroCombat.FavoredHit;
   }
   if(i!=null)aim*=Feats2.ShotFactor(p,i); // 1.58.1 精灵精准
   if(aim!=1f)Shooter(ref __result)*=aim;
  }
 }
 // 射程：鹰 ×1.15；魔网之子显现（没装 VPE）法杖 +3。
 [HarmonyPatch(typeof(VerbProperties),nameof(VerbProperties.AdjustedRange))]
 public static class HeroRange {
  static void Postfix(Verb ownerVerb,Thing attacker,ref float __result){
   if(__result<=0f || !(attacker is Pawn p) || ownerVerb==null)return;var eq=ownerVerb.EquipmentSource;
   if(eq!=null && Growth.legacyDefs.Count>0)__result+=Growth.RangeBonus(p,eq); // 1.58.3 风语升华
   var i=HeroTraits.Info(p);if(i==null)return;
   if(i.totem==3 && eq!=null && !ownerVerb.verbProps.IsMeleeAttack)__result*=HeroCombat.EagleRange;
   if(i.weave && i.Stage>=1 && !HeroDestiny.VPEActive && eq!=null && Arcana.Group(eq.def)=="G")__result+=HeroCombat.StaffRangeBonus;
  }
 }
 // 眩晕：法术抗体 50% 无效；宿敌克星免疫宿敌施加的眩晕。
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.StunFor))]
 public static class HeroStun {
  static bool Prefix(StunHandler __instance,int __0,Thing __1){
   if(__0<=0 || !(__instance.parent is Pawn p))return true;
   var i=HeroTraits.Info(p);if(i==null)return true;
   var a=__1 as Pawn;if(a==null || a==p || !a.HostileTo(p))return true;
   if(i.rec!=null && i.rec.favoredBane && HeroTraits.FavoredTarget(i,a)){Items.Mote(p,"宿敌克星",new Color(.75f,.6f,.95f));return false;}
   if(i.warded && HeroCombat.Roll<HeroCombat.WardChance){Items.Mote(p,"法术抗体",new Color(.75f,.6f,.95f));return false;}
   return true;
  }
 }
 // 外力造成的控制类精神状态（恐慌、狂暴、支配、魅惑）：心智堡垒免疫，法术抗体 50% 无效。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class HeroMental {
  static bool Prefix(MentalStateDef stateDef,bool causedByMood,Pawn ___pawn,ref bool __result){
   if(causedByMood || stateDef==null || ___pawn==null)return true;
   var i=HeroTraits.Info(___pawn);if(i==null || !i.fortress && !i.warded || !HeroCombat.ControlState(stateDef))return true;
   if(!i.fortress && HeroCombat.Roll>=HeroCombat.WardChance)return true;
   Items.Mote(___pawn,i.fortress?"心智堡垒":"法术抗体",new Color(.75f,.6f,.95f));__result=false;return false;
  }
 }
 // 1.63.0：法术抗体只抵抗敌对施法者（伤害、眩晕、控制都已经要求施法者敌对），不再挡友方的增益法术（原来友方“给状态”类能力 50% 落空）。
 // 敌对施法者给的“状态”（原版给状态类能力，负面的）照样 50% 无效。
 [HarmonyPatch(typeof(CompAbilityEffect_GiveHediff),nameof(CompAbilityEffect_GiveHediff.Apply))]
 public static class HeroWardedBuff {
  public static bool Blocks(Pawn caster,Pawn target,float roll){
   if(target==null || caster==null || target==caster)return false;
   var i=HeroTraits.Info(target);if(i==null || !i.warded || !caster.HostileTo(target))return false;
   return roll<HeroCombat.WardChance;
  }
  static bool Prefix(CompAbilityEffect_GiveHediff __instance,LocalTargetInfo __0){
   var t=__0.Pawn;if(!Blocks(__instance.parent?.pawn,t,HeroCombat.Roll))return true;
   Items.Mote(t,"法术抗体",new Color(.75f,.6f,.95f));return false;
  }
 }
 // 冷却：神之勇士显现后本神领域神术 -25%；魔网涌动期间不进冷却。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.StartCooldown))]
 public static class HeroCooldown {
  static void Prefix(RimWorld.Ability __instance,ref int ticks){
   if(ticks<=0)return;var p=__instance.pawn;var i=HeroTraits.Info(p);if(i==null || !i.weave && i.champion<=0 && !i.ritual)return;
   ticks=HeroDestiny.Cooldown(p,i,__instance.def,ticks);
  }
 }
 // 心灵熵：魔网涌动期间不加。
 [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker),nameof(Pawn_PsychicEntropyTracker.TryAddEntropy))]
 public static class HeroEntropy {
  static void Prefix(Pawn_PsychicEntropyTracker __instance,ref float __0){
   if(__0<=0f)return;var p=__instance.Pawn;var i=HeroTraits.Info(p);if(i==null || !i.weave && !i.ritual)return;
   if(i.weave && HeroDestiny.Surging(p) || i.ritual && Feats2.Primed(p))__0=0f; // 1.58.1 仪式余韵
  }
 }
 // 倒地：为了预言之子、冥界的宽限、神明干预。
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class HeroDowned {
  static void Postfix(Pawn ___pawn){
   var p=___pawn;if(p==null || p.Dead || !p.Downed)return;
   var i=HeroTraits.Info(p);if(i!=null)HeroDestiny.Downed(p,i);
   if(HeroDestiny.ChampionsWaiting && p.Faction!=null && p.Faction.IsPlayer)HeroDestiny.CheckIntervention(p.MapHeld);
  }
 }
 // 冥界归来：死亡类负面记忆不进来。
 [HarmonyPatch(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)})]
 public static class HeroDeathMemory {
  static bool Prefix(MemoryThoughtHandler __instance,Thought_Memory __0){
   if(__0?.def==null)return true;var i=HeroTraits.Info(__instance.pawn);if(i==null || !i.returned)return true;
   return !HeroDeeds.DeathThought(__0.def);
  }
 }
 // 击杀：传说事迹、预言、宿敌计数、邪魔活力。伤害分成在 AdventurerKill 后缀里会被清掉，这里先拷一份。
 public class HeroKillState {public Dictionary<Pawn,float> dmg;public Pawn killer;public Map map;public bool killerMelee;}
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class HeroKill {
  [HarmonyPriority(Priority.First)]
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out HeroKillState __state){
   __state=null;if(__instance.Dead)return;
   var st=Adventurer.State;Dictionary<Pawn,float> copy=null;
   if(st!=null && st.damage.TryGetValue(__instance,out var rec))copy=new Dictionary<Pawn,float>(rec);
   var k=dinfo?.Instigator as Pawn;
   __state=new HeroKillState{dmg=copy,killer=k,map=__instance.MapHeld,killerMelee=dinfo.HasValue && HeroCombat.Melee(dinfo.Value)};
  }
  static void Postfix(Pawn __instance,HeroKillState __state){
   if(__state==null || !__instance.Dead)return;
   try{HeroDeeds.OnKilled(__instance,__state);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 强力特性击杀结算出错："+e,75158002);}
  }
 }
 [HarmonyPatch(typeof(ResurrectionUtility),nameof(ResurrectionUtility.TryResurrect))]
 public static class HeroResurrected {static void Postfix(Pawn __0,bool __result){if(__result)HeroDeeds.Resurrected(__0);}}
 [HarmonyPatch(typeof(Phylactery),nameof(Phylactery.Destroy))]
 public static class HeroPhylacteryDestroyed {static void Prefix(Phylactery __instance,DestroyMode __0){if(!__instance.Destroyed && __0==DestroyMode.KillFinalize)HeroDeeds.PhylacteryDestroyed(__instance);}}
 // 偿契：处决、派对、婚礼只记时间戳。
 [HarmonyPatch(typeof(ExecutionUtility),nameof(ExecutionUtility.DoExecutionByCut))]
 public static class HeroExecutionHook {static void Postfix(){var c=GameComponent_HeroTraits.Get;if(c!=null)c.lastExecutionTick=GenTicks.TicksGame;}}
 [HarmonyPatch(typeof(GatheringWorker),nameof(GatheringWorker.TryExecute))]
 public static class HeroGatheringHook {static void Postfix(bool __result){if(!__result)return;var c=GameComponent_HeroTraits.Get;if(c!=null)c.lastGatheringTick=GenTicks.TicksGame;}}
 [HarmonyPatch(typeof(MarriageCeremonyUtility),nameof(MarriageCeremonyUtility.Married))]
 public static class HeroWeddingHook {static void Postfix(){var c=GameComponent_HeroTraits.Get;if(c!=null)c.lastGatheringTick=GenTicks.TicksGame;}}
}
