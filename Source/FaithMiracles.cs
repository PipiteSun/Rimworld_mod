using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace LichPhylactery {
 // 1.39.0 神术（Defs/Faith_Miracles.xml）：原版 RimWorld.Ability，不依赖 VPE。佩戴圣徽获得 3 个通用神术 + 守护神的领域神术，摘下就收回。
 // 施放消耗殖民地守护神的神眷（FaithUtility.MiracleCost）；数值 × P = 神恩强度 × 心灵敏感度（下限 0.2，不封顶），同 1.30.1 牧师：
 // 有 VPE 时神恩强度读 LP_DivinePower 属性，没有时 0.5 + 社交/20（就是 VPE 桥接 Classes.Power 的同一公式）；圣徽 +0.1、信徒 +0.15、神选者 +0.25 加在神恩强度上。
 // 伤害用 DamageInfo、instigator = 施法者、不填武器（同 1.30.1 牧师）。牧师治疗目前没有乘 Isekai 系数，这里也不乘。
 public class CompProperties_Miracle : CompProperties_AbilityEffect {
  public string effect;public float cost,amount,radius;public bool domain;
  public CompProperties_Miracle(){compClass=typeof(CompAbilityEffect_Miracle);}
 }
 public class Ability_Miracle : RimWorld.Ability {
  public Ability_Miracle():base(){}
  public Ability_Miracle(Pawn pawn):base(pawn){}
  public Ability_Miracle(Pawn pawn,AbilityDef def):base(pawn,def){}
  public Ability_Miracle(Pawn pawn,Precept sourcePrecept):base(pawn,sourcePrecept){}
  public Ability_Miracle(Pawn pawn,Precept sourcePrecept,AbilityDef def):base(pawn,sourcePrecept,def){}
  public CompProperties_Miracle Props=>def.comps.OfType<CompProperties_Miracle>().FirstOrDefault();
  public float Cost=>FaithUtility.MiracleCost(pawn,def);
  public override bool GizmoDisabled(out string reason){
   if(base.GizmoDisabled(out reason))return true;
   reason=Miracles.Blocked(pawn,def);return reason!=null;
  }
  public override AcceptanceReport CanCast{get{var b=base.CanCast;if(!b.Accepted)return b;var r=Miracles.Blocked(pawn,def);return r==null?b:(AcceptanceReport)r;}}
  public override bool Activate(LocalTargetInfo target,LocalTargetInfo dest){
   var god=FaithUtility.Patron;if(god==null || Miracles.Blocked(pawn,def)!=null)return false;
   FaithUtility.AddFavor(god,-Cost,pawn.LabelShort+"施放"+def.label);
   return base.Activate(target,dest);
  }
 }
 public class CompAbilityEffect_Miracle : CompAbilityEffect {
  public new CompProperties_Miracle Props=>(CompProperties_Miracle)props;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){base.Apply(target,dest);Miracles.Apply(parent.pawn,Props,target);}
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   if(!base.Valid(target,throwMessages))return false;
   var p=target.Pawn;
   switch(Props.effect){
    case "Sanctuary":case "Weave":if(p==null || p.HostileTo(parent.pawn)){if(throwMessages)Messages.Message("只能对友军施放。",MessageTypeDefOf.RejectInput,false);return false;}break;
    case "Banish":if(p==null || !p.HostileTo(parent.pawn)){if(throwMessages)Messages.Message("只能对敌人施放。",MessageTypeDefOf.RejectInput,false);return false;}break;
    case "Judgment":if(p==null || !(p.HostileTo(parent.pawn) || p.IsPrisonerOfColony)){if(throwMessages)Messages.Message("只能对敌人或殖民地的囚犯施放。",MessageTypeDefOf.RejectInput,false);return false;}break;
   }
   return true;
  }
 }

 public static class Miracles {
  public static readonly string[] Generic={"LP_Miracle_Sanctuary","LP_Miracle_Banish","LP_Miracle_Consecrate"};
  public const float PsychicMin=.2f,SymbolBonus=.1f,DevoteeBonus=.15f,ChosenBonus=.25f,AtheistTaken=.5f,VesselFactor=1.5f,RemoveInfusion=.3f,LichSelfDamage=20f,GiantWrath=1.25f,WeaveFocus=.3f;
  public const int ShieldTicks=30000,HuntTicks=30000,DreadTicks=600,ZoneTicks=60000;
  static List<AbilityDef> all;static StatDef divine;
  public static List<AbilityDef> All=>all??(all=DefDatabase<AbilityDef>.AllDefs.Where(d=>d.abilityClass==typeof(Ability_Miracle)).ToList());
  // 测试用：最后一道召雷的 DamageInfo 的 instigator。
  public static Thing lastStrikeInstigator;

  // ---------- 倍率 ----------
  public static float DivineBase(Pawn p){
   if(divine==null)divine=DefDatabase<StatDef>.GetNamedSilentFail("LP_DivinePower");
   if(divine!=null)return p.GetStatValue(divine);
   var s=p.skills?.GetSkill(SkillDefOf.Social);return .5f+(s==null || s.TotallyDisabled?0:s.Level)/20f;
  }
  public static float DivinePower(Pawn p){
   if(p==null)return 1f;float b=DivineBase(p);
   if(Items.Worn(p,Faith.SymbolDef)!=null)b+=SymbolBonus;
   if(FaithUtility.DevoteeGod(p)!=null)b+=DevoteeBonus;
   if(FaithUtility.IsChosen(p,out _))b+=ChosenBonus;
   return b;
  }
  public static float Psychic(Pawn p)=>p==null?1f:Mathf.Max(PsychicMin,p.GetStatValue(StatDefOf.PsychicSensitivity));
  public static float Power(Pawn p)=>DivinePower(p)*Psychic(p);
  public static float Taken(Pawn target)=>Mathf.Min(FaithUtility.IsAtheist(target)?AtheistTaken:1f,IdeoBridge.DefiantTaken(target)); // 1.56.0 不跪之人 ×0.75，和无神者取较小值

  // ---------- 能不能放 ----------
  public static string Blocked(Pawn p,AbilityDef def){
   if(!Faith.Enabled)return "信仰系统已关闭。";
   var god=FaithUtility.Patron;if(god==null)return "殖民地没有守护神：圣徽是沉寂的。";
   if(FaithUtility.IsAtheist(p))return "无神者不能施放神术。";
   var props=def.comps?.OfType<CompProperties_Miracle>().FirstOrDefault();if(props==null)return null;
   float favor=FaithUtility.GetFavor(god);
   if(props.domain && favor<Faith.DomainThreshold)return god.label+"的神眷不到"+Faith.DomainThreshold+"，领域神术不回应。";
   float cost=FaithUtility.MiracleCost(p,def);if(favor<cost)return "神眷不足：需要"+cost.ToString("0.#")+"，现在"+favor.ToString("0.#")+"。";
   return null;
  }

  // ---------- 圣徽给的能力 ----------
  public static bool WearsSymbol(Pawn p)=>Faith.SymbolDef!=null && Items.Worn(p,Faith.SymbolDef)!=null;
  public static List<AbilityDef> Wanted(Pawn p){
   var list=new List<AbilityDef>();var god=FaithUtility.Patron;
   if(p==null || !WearsSymbol(p) || god==null || FaithUtility.IsAtheist(p) || !Faith.Enabled)return list;
   foreach(var n in Generic){var d=DefDatabase<AbilityDef>.GetNamedSilentFail(n);if(d!=null)list.Add(d);}
   if(god.domainAbility!=null)list.Add(god.domainAbility);
   return list;
  }
  public static void Sync(Pawn p){
   if(p?.abilities==null)return;var want=Wanted(p);
   foreach(var d in All)if(!want.Contains(d) && p.abilities.GetAbility(d,true)!=null)p.abilities.RemoveAbility(d);
   foreach(var d in want)if(p.abilities.GetAbility(d,true)==null)p.abilities.GainAbility(d);
  }
  public static void SyncAll(){foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive.Where(x=>x.abilities!=null && x.RaceProps.Humanlike).ToList())Sync(p);}

  // ---------- 效果 ----------
  static IEnumerable<Pawn> Near(Pawn c,float r)=>c?.Map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(c.Map,c.Position,r).Where(o=>!o.Dead);
  public static void Mote(Thing t,string text)=>Items.Mote(t,text,FaithUtility.Patron?.color??Color.white);
  public static void Apply(Pawn caster,CompProperties_Miracle props,LocalTargetInfo target){
   if(caster==null || props==null)return;float P=Power(caster);var map=caster.Map;var t=target.Pawn;
   switch(props.effect){
    case "Sanctuary":Sanctuary(t,props.amount*P*Taken(t));break;
    case "Banish":Banish(caster,t,props.amount*P);break;
    case "Consecrate":Consecrate(map,target.Cell,props.radius,props.amount*P,caster.Faction);break;
    case "Dawn":Dawn(caster,props.radius,props.amount*P);break;
    case "Judgment":Judgment(caster,t,P);break;
    case "Weave":Weave(t,props.amount);break;
    case "Rest":Rest(caster,props.radius,props.amount*P);break;
    case "Hunt":Hunt(caster,props.radius,props.amount*P);break;
    case "Thunder":Thunder(caster,target.Cell,props.amount*P);break;
    case "Dread":Dread(caster,props.radius);break;
    case "Veil":Veil(caster,props.radius,props.amount*P);break;
    case "ForgeWrath":ForgeWrath(caster,target.Cell,props.radius,props.amount*P);break;
   }
  }
  public static Hediff Sanctuary(Pawn t,float shield){
   if(t==null)return null;var h=Items.Give(t,"LP_SanctuaryShield",ShieldTicks);if(h!=null){h.Severity=Mathf.Max(h.Severity,shield);Mote(t,"护盾 "+shield.ToString("0"));}
   return h;
  }
  // 驱逐邪祟的伤害：不死者/邪魔 ×2，神明附体者再 ×1.5，无神者 ×0.5。
  public static float BanishAmount(Pawn t,float amount){if(Faith.HolyVulnerable(t))amount*=2f;if(Twilight.Vessel(t)!=null)amount*=VesselFactor;return amount*Taken(t);}
  public static float Banish(Pawn c,Pawn t,float amount){
   if(t==null || t.Dead)return 0f;
   amount=BanishAmount(t,amount);
   float dealt=t.TakeDamage(new DamageInfo(Faith.Radiant,amount,.4f,-1f,c)).totalDamageDealt;
   if(t.Spawned)FleckMaker.Static(t.Position,t.Map,FleckDefOf.PsycastAreaEffect,1.5f);
   if(!t.Dead && Twilight.Infused(t)!=null && Rand.Chance(RemoveInfusion*Power(c))){var tr=t.story?.traits?.GetTrait(Twilight.Trait);if(tr!=null){t.story.traits.RemoveTrait(tr);Mote(t,"灌注被驱逐");}}
   return dealt;
  }
  public static void Consecrate(Map map,IntVec3 cell,float radius,float dps,Faction f){
   var m=MapComp<MapComponent_Faith>.Get(map);if(m==null)return;
   m.zones.Add(new FaithZone{kind="consecrate",center=cell,radius=radius,power=dps,until=Faith.Now+ZoneTicks,faction=f});
   foreach(var c in GenRadial.RadialCellsAround(cell,radius,true))if(c.InBounds(map) && Rand.Chance(.3f))FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,.6f);
  }
  public static int Dawn(Pawn c,float radius,float heal){
   int n=0;
   foreach(var o in Near(c,radius).Where(o=>!o.HostileTo(c) && (o.Faction==c.Faction || o.IsPrisonerOfColony)).ToList()){
    float h=heal*Taken(o);
    foreach(var b in o.health.hediffSet.hediffs.Where(x=>x.Bleeding).ToList())b.Tended(1f,1f);
    foreach(var i in o.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>!x.IsPermanent()).ToList()){i.Heal(h);n++;}
   }
   var m=MapComp<MapComponent_Faith>.Get(c.Map);if(m!=null)m.zones.Add(new FaithZone{kind="dawn",center=c.Position,radius=radius,until=Faith.Now+ZoneTicks,faction=c.Faction});
   if(c.Spawned)FleckMaker.Static(c.Position,c.Map,FleckDefOf.PsycastAreaEffect,radius);
   return n;
  }
  public const float JudgeBase=10f,JudgePer=5f,JudgeCap=60f,JudgeResist=20f,JudgeWill=10f;
  public static float JudgmentDamage(Pawn t,float P){int kills=t?.records?.GetAsInt(RecordDefOf.KillsHumanlikes)??0;return Mathf.Min(JudgeCap,JudgeBase+kills/10*JudgePer)*P;}
  public static float Judgment(Pawn c,Pawn t,float P){
   if(t==null || t.Dead)return 0f;
   if(t.IsPrisonerOfColony && t.guest!=null){
    t.guest.resistance=Mathf.Max(0f,t.guest.resistance-JudgeResist*P*Taken(t));
    try{t.guest.will=Mathf.Max(0f,t.guest.will-JudgeWill*P*Taken(t));}catch{}
    Mote(t,"真言审判");return 0f;
   }
   float dmg=JudgmentDamage(t,P)*Taken(t);
   t.TakeDamage(new DamageInfo(TwilightSkills.Divine,dmg,1f,-1f,c));
   if(t.Spawned)FleckMaker.Static(t.Position,t.Map,FleckDefOf.PsycastAreaEffect,1.5f);
   Mote(t,"罪罚 "+dmg.ToString("0"));
   return dmg;
  }
  public static int Weave(Pawn t,float focus){
   if(t?.abilities==null)return 0;int n=0;
   foreach(var a in t.abilities.AllAbilitiesForReading)if(a.CooldownTicksRemaining>0 && !(a is Ability_Miracle)){a.ResetCooldown();n++;}
   if(t.psychicEntropy?.Psylink!=null)t.psychicEntropy.OffsetPsyfocusDirectly(focus*Taken(t));
   Mote(t,"魔网涌流");
   return n;
  }
  public static int Rest(Pawn c,float radius,float amount){
   int n=0;var comp=Faith.Comp;
   foreach(var o in Near(c,radius).Where(o=>o!=c && o.HostileTo(c) && Items.Undead(o)).ToList()){o.TakeDamage(new DamageInfo(Faith.Radiant,amount*Taken(o),.4f,-1f,c));n++;}
   if(c.Map!=null && comp!=null)foreach(var corpse in GenRadial.RadialDistinctThingsAround(c.Position,c.Map,radius,true).OfType<Corpse>().ToList())if(corpse.InnerPawn!=null && corpse.InnerPawn.RaceProps.Humanlike && !comp.rested.Contains(corpse.InnerPawn.ThingID)){comp.rested.Add(corpse.InnerPawn.ThingID);Mote(corpse,"已安息");}
   if(Items.IsLich(c))c.TakeDamage(new DamageInfo(Faith.Radiant,LichSelfDamage,0f,-1f,c));
   if(c.Spawned)FleckMaker.Static(c.Position,c.Map,FleckDefOf.PsycastAreaEffect,radius);
   return n;
  }
  public static bool Rested(Corpse c)=>c?.InnerPawn!=null && Faith.Comp?.rested.Contains(c.InnerPawn.ThingID)==true;
  public static int Hunt(Pawn c,float radius,float bonus){
   int n=0;
   foreach(var a in Near(c,radius).Where(a=>a.RaceProps.Animal && a.Faction==c.Faction).ToList()){var h=Items.Give(a,"LP_HuntFury",HuntTicks);if(h!=null){h.Severity=Mathf.Max(.01f,bonus);n++;}}
   return n;
  }
  public static void Strike(Map map,IntVec3 cell,float amount,Pawn caster){
   if(map==null || !cell.InBounds(map))return;
   map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningFlash(map));
   lastStrikeInstigator=caster;
   GenExplosion.DoExplosion(cell,map,1.9f,DamageDefOf.Flame,caster,Mathf.RoundToInt(amount),.3f,SoundDefOf.Thunder_OnMap);
   FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(),map,1.5f);
  }
  public static List<IntVec3> Thunder(Pawn c,IntVec3 center,float amount){
   var map=c.Map;var cells=new List<IntVec3>();if(map==null)return cells;
   for(int i=0;i<3;i++){var cell=center;if(CellFinder.TryFindRandomCellNear(center,map,2,x=>x.InBounds(map),out var r))cell=r;cells.Add(cell);Strike(map,cell,amount,c);}
   return cells;
  }
  public static int Dread(Pawn c,float radius){
   int n=0;
   foreach(var o in Near(c,radius).Where(o=>o.HostileTo(c) && !o.Downed && o.RaceProps.Humanlike).ToList()){
    if(BodySize.Size(o)>1.01f || Twilight.Vessel(o)!=null){Mote(o,"不为所动");continue;}
    if(DNDMind.TryResist(o)){Mote(o,"抵抗");continue;}
    if(Flee(o,DreadTicks,"暴君之令"))n++;
   }
   return n;
  }
  public static bool Flee(Pawn o,int ticks,string reason){
   if(o?.mindState?.mentalStateHandler==null || o.Dead)return false;
   if(!o.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,reason,true,false,false,null,true))return false;
   if(o.MentalState!=null)o.MentalState.forceRecoverAfterTicks=ticks;
   Mote(o,"惊逃");return true;
  }
  public static int Veil(Pawn c,float radius,float seconds){
   int n=0;
   foreach(var o in Near(c,radius).Where(o=>!o.HostileTo(c) && o.Faction==c.Faction && o.RaceProps.Humanlike).ToList()){Items.Give(o,"LP_SharVeil",Mathf.Max(60,Mathf.RoundToInt(seconds*60f*Taken(o))));n++;}
   return n;
  }
  public static int ForgeWrath(Pawn c,IntVec3 target,float radius,float amount){
   var map=c.Map;if(map==null)return 0;if(FireGiant.Active(c))amount*=GiantWrath;
   var dir=(target-c.Position).ToVector3();if(dir.sqrMagnitude<.01f)dir=c.Rotation.FacingCell.ToVector3();
   int n=0;
   foreach(var cell in GenRadial.RadialCellsAround(c.Position,radius,false)){
    if(!cell.InBounds(map))continue;var off=(cell-c.Position).ToVector3();if(Vector3.Angle(dir,off)>30f)continue;
    if(Rand.Chance(.5f))FleckMaker.ThrowFireGlow(cell.ToVector3Shifted(),map,1f);
    foreach(var o in cell.GetThingList(map).OfType<Pawn>().Where(o=>o!=c && !o.Dead && o.HostileTo(c)).ToList()){o.TakeDamage(new DamageInfo(DamageDefOf.Burn,amount*Taken(o),.2f,-1f,c));if(!o.Dead && o.Spawned)o.TryAttachFire(.4f,c);n++;}
   }
   return n;
  }

  // ---------- 区域 ----------
  public static bool InConsecrated(Map map,IntVec3 cell,out FaithZone zone){zone=null;var m=MapComp<MapComponent_Faith>.Get(map);if(m==null)return false;foreach(var z in m.zones)if(z.kind=="consecrate" && z.Contains(cell)){zone=z;return true;}return false;}
  public static bool InDawn(Map map,IntVec3 cell){var m=MapComp<MapComponent_Faith>.Get(map);if(m==null || m.zones.Count==0)return false;foreach(var z in m.zones)if(z.kind=="dawn" && z.Contains(cell))return true;return false;}
 }

 public class FaithZone : IExposable {
  public string kind;public IntVec3 center;public float radius,power;public int until;public Faction faction;
  public bool Active=>Faith.Now<until;
  public bool Contains(IntVec3 c)=>Active && c.InHorDistOf(center,radius);
  public void ExposeData(){Scribe_Values.Look(ref kind,"kind");Scribe_Values.Look(ref center,"center");Scribe_Values.Look(ref radius,"radius");Scribe_Values.Look(ref power,"power");Scribe_Values.Look(ref until,"until");Scribe_References.Look(ref faction,"faction");}
 }
 // 圣化之地：每秒（60 刻）对区内敌方不死者/邪魔 2×P 神圣伤害，暂停区内巫妖命匣和巨魔尸体的复活计时；黎明之光的作物加成。
 public class MapComponent_Faith : MapComponent {
  public List<FaithZone> zones=new List<FaithZone>();
  public MapComponent_Faith(Map map):base(map){}
  public override void MapComponentTick(){
   if(zones.Count==0 || Find.TickManager.TicksGame%60!=0)return;
   zones.RemoveAll(z=>!z.Active);
   foreach(var z in zones.Where(z=>z.kind=="consecrate").ToList())Pulse(z,60);
  }
  public int Pulse(FaithZone z,int ticks){
   int n=0;
   foreach(var t in GenRadial.RadialDistinctThingsAround(z.center,map,z.radius,true).ToList()){
    if(t is Pawn p && !p.Dead && Faith.HolyVulnerable(p) && (z.faction==null || p.HostileTo(z.faction))){p.TakeDamage(new DamageInfo(Faith.Radiant,z.power*Miracles.Taken(p),0f,-1f));n++;}
    else if(t is Corpse c){var g=Troll.Gene(c.InnerPawn);if(g!=null && g.reviveTick>0)g.reviveTick+=ticks;}
    else if(t is Phylactery box && box.revivalTick>0)box.revivalTick+=ticks;
   }
   return n;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref zones,"lpFaithZones",LookMode.Deep);if(Scribe.mode==LoadSaveMode.PostLoadInit && zones==null)zones=new List<FaithZone>();}
 }

 // ---------- 神术留下的状态 ----------
 public class Hediff_SanctuaryShield : HediffWithComps {
  public override string LabelInBrackets=>"剩余 "+Severity.ToString("0");
  public override bool ShouldRemove=>base.ShouldRemove || Severity<=.5f;
 }
 public class Hediff_HuntFury : HediffWithComps {
  HediffStage stage;float cached=-1f;
  public override string LabelInBrackets=>"近战 +"+Severity.ToStringPercent("0");
  public override HediffStage CurStage{get{if(stage==null || cached!=Severity){cached=Severity;stage=new HediffStage{statOffsets=new List<StatModifier>{new StatModifier{stat=StatDefOf.MeleeDamageFactor,value=Severity}},statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.MoveSpeed,value=1.2f}}};}return stage;}}
 }

 // ==================== Harmony ====================
 // 神圣庇护的护盾先吸收伤害；遗物、阿斯莫、魔鬼契约的伤害倍率。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class FaithDamage {
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   var def=dinfo.Def;if(def==null || !def.harmsHealth || __instance.Dead)return true;
   float f=DamageFactor(__instance,dinfo);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
   var h=__instance.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_SanctuaryShield"));
   if(h!=null && dinfo.Amount>0f){
    float take=Mathf.Min(h.Severity,dinfo.Amount);h.Severity-=take;dinfo.SetAmount(dinfo.Amount-take);
    if(__instance.Spawned)FleckMaker.ThrowLightningGlow(__instance.DrawPos,__instance.Map,.6f);
    if(dinfo.Amount<=.001f){absorbed=true;return false;}
   }
   return true;
  }
  public static float DamageFactor(Pawn v,DamageInfo dinfo){
   float f=1f;var def=dinfo.Def;
   if(Beasts.IsFire(def) && Items.Worn(v,"LP_Relic_ForgeHeart")!=null)f*=.5f;
   var a=dinfo.Instigator as Pawn;if(a==null || a==v)return f;
   bool melee=!def.isRanged && !def.isExplosive;
   if(melee && Items.Holds(a,"LP_Relic_MorningStar") && Items.Undead(v))f*=2f;
   if(melee && Items.Worn(a,"LP_Relic_HandOfTyr")!=null && (v.records?.GetAsInt(RecordDefOf.KillsHumanlikes)??0)>=10)f*=1.15f;
   if(FaithRaces.IsAasimar(a) && Items.Undead(v))f*=FaithRaces.UndeadBane;
   if(def.isRanged && Items.Has(a,"LP_InfernalPactActive"))f*=FaithRaces.PactDamage;
   if(def==Faith.Radiant && FaithRaces.IsAasimar(a) && Troll.Is(v))BlessedWater.SuppressTroll(v,BlessedWater.TrollTicks);
   return f;
  }
 }
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Notify_ApparelAdded))]
 public static class FaithSymbolWorn {static void Postfix(Pawn_ApparelTracker __instance,Apparel apparel){if(apparel?.def==Faith.SymbolDef)Miracles.Sync(__instance.pawn);}}
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Notify_ApparelRemoved))]
 public static class FaithSymbolRemoved {static void Postfix(Pawn_ApparelTracker __instance,Apparel apparel){if(apparel?.def==Faith.SymbolDef)Miracles.Sync(__instance.pawn);}}
 // 银焰之泪：神术与法术（原版灵能）冷却 ×0.8。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.StartCooldown))]
 public static class FaithSilverTear {static void Prefix(RimWorld.Ability __instance,ref int ticks){if(ticks>0 && (__instance is Ability_Miracle || __instance.def?.IsPsycast==true) && Items.Worn(__instance.pawn,"LP_Relic_SilverTear")!=null)ticks=Mathf.RoundToInt(ticks*.8f);}}
 // 暗影帷幕：一出手就显形。
 [HarmonyPatch(typeof(Verb),nameof(Verb.TryStartCastOn),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo),typeof(bool),typeof(bool),typeof(bool),typeof(bool)})]
 public static class FaithVeilBreak {static void Postfix(Verb __instance,bool __result){var p=__instance.CasterPawn;if(__result && p!=null && !(__instance is Verb_CastAbility) && Items.Has(p,"LP_SharVeil"))Items.Remove(p,"LP_SharVeil");}}
 // 遗物的近战附加效果。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class FaithRelicMelee {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){
   var c=__instance.CasterPawn;var t=target.Pawn;if(c==null || t==null || t.Dead || !Faith.Enabled)return;
   try{
    if(Items.Holds(c,"LP_Relic_MorningStar") && Rand.Chance(.1f) && t.Spawned)t.TryAttachFire(.4f,c);
    if(Items.Holds(c,"LP_Relic_StormSpear") && Rand.Chance(.2f) && t.Spawned){var map=t.Map;var cell=t.Position;map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningFlash(map));GenExplosion.DoExplosion(cell,map,1.5f,DamageDefOf.Flame,c,25,.3f,SoundDefOf.Thunder_OnMap,ignoredThings:new List<Thing>{c});}
    if(t.Dead)return;
    if(Items.Worn(c,"LP_Relic_BlackHand")!=null && Rand.Chance(.25f) && t.RaceProps.Humanlike && !DNDMind.TryResist(t))Miracles.Flee(t,300,"黑手护手");
    if(Items.Worn(c,"LP_Relic_ForgeHeart")!=null)t.TakeDamage(new DamageInfo(DamageDefOf.Burn,5f,0f,-1f,c));
    if(!t.Dead && Items.Worn(c,"LP_Relic_BeastClaw")!=null)t.TakeDamage(new DamageInfo(DamageDefOf.Cut,3f,0f,-1f,c));
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 遗物近战效果："+e,8814215);}
  }
 }
}
