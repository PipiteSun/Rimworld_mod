using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.135.0 术士子职业补完（Defs/Subclasses5.xml）：神圣之魂、暗影魔法、时钟之魂。挂在 LP_SubclassDef 框架上：
 // 主动效果走 ClassEffects.Apply / Invalid 的链（Subclass5.Apply / Invalid），被动在 ClassSystem.Tick250 里（Subclass5.Tick250），威力系数在 ClassEffects.Power（Subclass5.PowerFactor）。
 public static class Subclass5 {
  public const string DivineSoul="LP_Sub_DivineSoul",ShadowMagic="LP_Sub_ShadowMagic",Clockwork="LP_Sub_ClockworkSoul";
  public const float DarkGlow=.3f,LightHeal=1.25f,TwilightDamage=1.2f,GuardShield=8f,DarkPower=1.2f,StepPower=1.2f,GraveChance=.5f,FormFactor=.5f,FormMiss=.3f,MechFactor=.9f,HoundFear=.3f;
  public const int BalancePerDay=2,HoundFearTicks=300,MechStun=180,LegionCount=3;
  static readonly DefRef<LP_ClassDef> sorcerer=new DefRef<LP_ClassDef>("LP_Class_Sorcerer");
  public static LP_ClassDef Sorcerer=>sorcerer.Def;
  // 有这三个子职业的人（每 250 刻刷新；读档后 Tick250 里重新登记）。没人学的时候挂钩直接返回。
  public static readonly HashSet<Pawn> Users=new HashSet<Pawn>();
  public static void Reset(){Users.Clear();day.Clear();}
  static readonly Dictionary<string,int> day=new Dictionary<string,int>(); // "键:thingID" → 上次用掉的日子 / 次数
  static int Today=>GenDate.DaysPassed;
  public static bool Has(Pawn p,string sub)=>p!=null && Users.Contains(p) && ClassSubclass.Has(p,sub);
  public static int Affinity(Pawn p)=>ClassSubclass.Choice(p,"dsAffinity");
  public static int SorcLevel(Pawn p)=>Sorcerer==null?0:ClassSystem.Level(p,Sorcerer);
  public static bool? TestDark;
  public static bool Dark(Pawn p)=>p!=null && p.Spawned && (TestDark??p.Map.glowGrid.GroundGlowAt(p.Position)<DarkGlow);
  public static bool DarkCell(Map m,IntVec3 c)=>m!=null && c.InBounds(m) && (TestDark??m.glowGrid.GroundGlowAt(c)<DarkGlow);
  static bool UseDaily(Pawn p,string key,int max){string k=key+":"+p.thingIDNumber;day.TryGetValue(k,out int v);int d=v/100,n=v%100;if(d!=Today){d=Today;n=0;}if(n>=max)return false;day[k]=d*100+n+1;return true;}
  public static int DailyLeft(Pawn p,string key,int max){string k=key+":"+p.thingIDNumber;day.TryGetValue(k,out int v);return v/100!=Today?max:Mathf.Max(0,max-v%100);}
  public static bool Holy(DamageDef d)=>d!=null && (d==DamageDefOf.Flame || d==DamageDefOf.Burn || d.defName=="LP_Radiant" || d.defName=="LP_DivineSmite" || d.defName=="LP_HolyWater");
  public static bool FormBypass(DamageDef d)=>Holy(d) || d!=null && (d.defName=="LP_Force" || d.defName=="LP_ArcaneForce");

  // ---------- 被动（每 250 刻） ----------
  public static void Tick250(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null || p.Dead)return;
   bool any=ClassSubclass.Has(h,DivineSoul) || ClassSubclass.Has(h,ShadowMagic) || ClassSubclass.Has(h,Clockwork);
   if(!any){Users.Remove(p);return;}
   Users.Add(p);var s=Sorcerer;
   if(ClassSystem.Knows(p,"LP_DS_Resonance") && s!=null){var j=p.CurJobDef;if(j==JobDefOf.Meditate || j!=null && j.defName.Contains("Pray"))ClassSystem.AddResource(h,s,ClassSystem.MeditateRegen*(p.Ideo!=null && p.Ideo.HasMeme(DefDatabase<MemeDef>.GetNamedSilentFail("Structure_TheistEmbodied")??DefDatabase<MemeDef>.GetNamedSilentFail("Structure_TheistAbstract"))?1.25f:1f));}
   if(ClassSystem.Knows(p,"LP_SM_ShadowForm") && s!=null && !p.Awake()){int hr=GenLocalDate.HourOfDay(p);if(hr>=22 || hr<6)ClassSystem.AddResource(h,s,ClassSystem.SleepRegen);}
   bool cw=ClassSystem.Knows(p,"LP_CW_Balance");if(cw && !Items.Has(p,"LP_CW_Affinity"))Items.Give(p,"LP_CW_Affinity");else if(!cw)Items.Remove(p,"LP_CW_Affinity");
  }
  // ---------- 威力 ----------
  public static float PowerFactor(Pawn p,CompProperties_ClassEffect e){
   if(Users.Count==0 || e==null || p==null || !Users.Contains(p))return 1f;float f=1f;
   if(e.effect=="Heal" && ClassSubclass.Has(p,DivineSoul) && Affinity(p)==0)f*=LightHeal;
   if(e.sorcery && ClassSystem.Knows(p,"LP_SM_EyesDark") && Dark(p))f*=DarkPower;
   if(e.sorcery && Items.Has(p,"LP_SM_StepBuff"))f*=StepPower;
   return f;
  }
  public static void AfterCast(Ability_Class a,LocalTargetInfo target){
   var p=a?.pawn;var e=a?.Props;if(p==null || e==null || Users.Count==0 || !Users.Contains(p))return;
   if(e.sorcery && Items.Has(p,"LP_SM_StepBuff") && e.effect!="S5_ShadowStep"){var pp=p;Fiends.Defer(()=>Items.Remove(pp,"LP_SM_StepBuff"));}
   if(ClassSubclass.Has(p,DivineSoul) && Affinity(p)==2 && e.sorcery){LakeArcana.Touched=true;var sh=Items.Give(p,"LP_ACB_Shield",2500,GuardShield);Items.Mote(p,"守护",new Color(1f,.9f,.6f));}
   if(e.effect=="Heal" && ClassSystem.Knows(p,"LP_DS_Resonance") && target.Thing is Pawn t && t!=p){ClassMelee.Heal(p,ClassEffects.Power(p,e)*.25f);}
  }
  // ---------- 选目标 ----------
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;
   switch(e.effect){
    case "S5_Hound":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
    case "S5_ShadowStep":
     if(!Dark(caster))return "要站在黑暗里（光照低于 30%）。";
     if(!DarkCell(caster.Map,target.Cell))return "目的地也要在黑暗里。";
     if(!target.Cell.Standable(caster.Map) || target.Cell.GetFirstPawn(caster.Map)!=null)return "需要一处能站立的空地。";
     if(caster.Position.DistanceTo(target.Cell)>20.5f)return "最远 20 格。";return null;
    case "S5_Ward":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(t.HostileTo(caster))return "只能对友军使用。";return null;
   }
   return null;
  }
  // ---------- 施放 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;
   switch(e.effect){
    case "S5_Angel":Items.Give(p,"LP_DS_Angelic",ClassEffects.Duration(p,e),ClassEffects.Power(p,e));Items.Mote(p,"天使形态",new Color(1f,.95f,.65f));return true;
    case "S5_Miracle":Miracle(p);return true;
    case "S5_Hound":if(t!=null)Hound(p,t,ClassEffects.Duration(p,e));return true;
    case "S5_ShadowStep":ShadowStep(p,target.Cell,e.duration);return true;
    case "S5_Ward":if(t!=null)Ward(p,t,ClassEffects.Duration(p,e));return true;
    case "S5_Domain":Domain(p,ClassEffects.Duration(p,e),ClassEffects.Radius(p,e),-1f);return true;
    case "S5_Legion":Legion(p,e.duration);return true;
   }
   return false;
  }
  public static int Miracle(Pawn p){
   int n=0;var map=p.Map;if(map==null)return 0;var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_DS_MiracleThought");
   foreach(var c in map.mapPawns.FreeColonistsSpawned.ToList()){
    ClassMelee.Heal(c,ClassMelee.InjurySum(c)*.5f);
    foreach(var d in c.health.hediffSet.hediffs.Where(ClassCasters.IsDisease).ToList())d.Severity=Mathf.Max(.01f,d.Severity-.3f);
    if(th!=null)c.needs?.mood?.thoughts?.memories?.TryGainMemory(th);
    Items.Mote(c,"神迹",new Color(1f,.95f,.65f));n++;
   }
   foreach(var cell in GenRadial.RadialCellsAround(p.Position,6f,true))if(cell.InBounds(map) && Rand.Chance(.3f))FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(),map,1.2f);
   if(p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"降下神迹：殖民者的伤口愈合了一半。",p,MessageTypeDefOf.PositiveEvent,false);
   return n;
  }
  public static Pawn Hound(Pawn p,Pawn t,int ticks){
   var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Warg")??DefDatabase<PawnKindDef>.GetNamedSilentFail("Wolf_Timber");
   var w=ArcanaEffects.Summon(p,kind,null,ticks,true);if(w==null)return null;
   Items.Give(w,"LP_SM_HoundMark");Items.Give(t,"LP_SM_Prey",ticks);WizardSpellsReveal(t);
   if(w.jobs!=null && t.Spawned){var job=JobMaker.MakeJob(JobDefOf.AttackMelee,t);job.expiryInterval=ticks;w.jobs.StartJob(job,JobCondition.InterruptForced);}
   Items.Mote(w,"影犬",new Color(.6f,.45f,.85f));return w;
  }
  static void WizardSpellsReveal(Pawn t){var inv=DefCache<HediffDef>.Get("PsychicInvisibility");var h=inv==null?null:t.health.hediffSet.GetFirstHediffOfDef(inv);if(h!=null)t.health.RemoveHediff(h);}
  public static bool ShadowStep(Pawn p,IntVec3 c,int buff){
   var map=p.Map;if(map==null)return false;FleckMaker.ThrowDustPuffThick(p.DrawPos,map,1.5f,new Color(.3f,.2f,.4f));
   Races124.Blink(p,c);FleckMaker.ThrowDustPuffThick(p.DrawPos,map,1.5f,new Color(.3f,.2f,.4f));
   Items.Give(p,"LP_SM_StepBuff",buff);Items.Mote(p,"暗影步",new Color(.6f,.45f,.85f));return true;
  }
  public static float Ward(Pawn p,Pawn t,int ticks){
   float amount=10f+SorcLevel(p)*2f;LakeArcana.Touched=true;Items.Give(t,"LP_ACB_Shield",ticks,amount);
   if(ModsConfig.BiotechActive && MechanitorUtility.IsMechanitor(t))Items.Give(t,"LP_CW_Bandwidth",ticks);
   Items.Mote(t,"秩序护盾 "+amount.ToString("0"),new Color(.85f,.62f,.35f));return amount;
  }
  public static int Domain(Pawn p,int ticks,float radius,float roll){
   Items.Give(p,"LP_CW_DomainBuff",ticks);int n=0;
   if(p.Map!=null)foreach(var m in Races124.PawnsNear(p.Map,p.Position,radius).ToList()){if(!m.RaceProps.IsMechanoid || !m.HostileTo(p))continue;if((roll>=0f?roll:Rand.Value)<.3f){m.stances?.stunner?.StunFor(MechStun,p);Items.Mote(m,"停机",new Color(.85f,.62f,.35f));n++;}}
   Items.Mote(p,"秩序领域",new Color(.85f,.62f,.35f));return n;
  }
  public static List<Pawn> Legion(Pawn p,int ticks){
   var list=new List<Pawn>();
   var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Militor")??DefDatabase<PawnKindDef>.GetNamedSilentFail("Mech_Scyther");if(kind==null)return list;
   for(int i=0;i<LegionCount;i++){var w=ArcanaEffects.Summon(p,kind,null,ticks,true);if(w==null)continue;w.Name=new NameSingle("秩序构装 "+(i+1));list.Add(w);}
   Items.Mote(p,"秩序军团",new Color(.85f,.62f,.35f));return list;
  }

  // ---------- 不倒（神迹被动、坟墓之力） ----------
  static readonly Dictionary<int,DamageDef> lastHit=new Dictionary<int,DamageDef>();
  public static void NoteHit(Pawn v,DamageDef d){if(Users.Contains(v))lastHit[v.thingIDNumber]=d;}
  // 返回 true = 救下了（治好一半伤口 / 硬撑）。
  public static bool TryStand(Pawn p,float? roll=null){
   if(p==null || p.Dead || Users.Count==0 || !Users.Contains(p))return false;
   if(ClassSystem.Knows(p,"LP_DS_Miracle") && UseDaily(p,"miracleStand",1)){ClassMelee.Heal(p,ClassMelee.InjurySum(p)*.5f);Items.Give(p,"LP_StaunchBleeding",2500);Items.Mote(p,"神迹：站起",new Color(1f,.95f,.65f));return true;}
   if(ClassSystem.Knows(p,"LP_SM_EyesDark")){
    lastHit.TryGetValue(p.thingIDNumber,out var d);if(Holy(d))return false;
    if(!UseDaily(p,"grave",1))return false;
    if((roll??Rand.Value)>=GraveChance){Items.Mote(p,"坟墓之力没撑住",Color.gray);return false;}
    ClassMelee.Heal(p,ClassMelee.InjurySum(p)*.25f);Items.Give(p,"LP_StaunchBleeding",2500);Items.Mote(p,"坟墓之力",new Color(.6f,.45f,.85f));return true;
   }
   return false;
  }
  // ---------- 秩序之手：同地图己方豁免失败时重新判定（每天 2 次） ----------
  public static bool Rebalance(Pawn v,int a,int dc,Pawn source,float? roll=null){
   if(Users.Count==0 || v?.Map==null || v.Faction==null)return false;
   foreach(var c in Users)if(c!=null && !c.Dead && c.Map==v.Map && c.Faction==v.Faction && ClassSystem.Knows(c,"LP_CW_Balance") && DailyLeft(c,"balance",BalancePerDay)>0){
    UseDaily(c,"balance",BalancePerDay);bool ok=(roll??TestRoll??Rand.Value)<Legendary.SaveChance(v,a,dc,source);
    Items.Mote(v,ok?"秩序之手：重判成功":"秩序之手：重判失败",new Color(.85f,.62f,.35f));return ok;
   }
   return false;
  }
  public static float? TestRoll;
  public static bool LegionWild(Pawn p)=>Users.Count>0 && ClassSystem.Knows(p,"LP_CW_Legion");
  public static readonly int[] BadWild={4,6,7};
  public static readonly HashSet<string> NoAi=new HashSet<string>{"S5_Miracle","S5_Legion","S5_ShadowStep"};
 }

 // ==================== 挂钩 ====================
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class Subclass5Damage {
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(Subclass5.Users.Count==0 || dinfo.Def==null || dinfo.Amount<=0f)return true;
   var v=__instance;var a=dinfo.Instigator as Pawn;
   if(Subclass5.Users.Contains(v)){
    Subclass5.NoteHit(v,dinfo.Def);
    if(Items.Has(v,"LP_SM_Form") && dinfo.Def.harmsHealth){
     if(a!=null && a.HostileTo(v) && dinfo.Def.isRanged && Subclass5.Dark(v) && Rand.Chance(Subclass5.FormMiss)){absorbed=true;Items.Mote(v,"落空",new Color(.6f,.45f,.85f));return false;}
     if(!Subclass5.FormBypass(dinfo.Def))dinfo.SetAmount(dinfo.Amount*Subclass5.FormFactor);
    }
    if(a!=null && a.RaceProps.IsMechanoid && Items.Has(v,"LP_CW_Affinity"))dinfo.SetAmount(dinfo.Amount*Subclass5.MechFactor);
   }
   if(a!=null && a!=v && Subclass5.Users.Contains(a) && dinfo.Def.harmsHealth && ClassSubclass.Has(a,Subclass5.DivineSoul) && Subclass5.Affinity(a)==1 && v.RaceProps.IsFlesh && a.CurJob?.ability!=null)dinfo.SetAmount(dinfo.Amount*Subclass5.TwilightDamage);
   if(a!=null && Items.Has(a,"LP_SM_HoundMark") && v.HostileTo(a) && Rand.Chance(Subclass5.HoundFear)){var vv=v;Fiends.Defer(()=>ClassSubclass.Fear(vv,Subclass5.HoundFearTicks,"影犬"));}
   return true;
  }
 }
 // 神迹被动、坟墓之力：本应倒地时（和死亡守卫同一处，在它之前）。
 [HarmonyPatch(typeof(Pawn_HealthTracker),"CheckForStateChange")]
 public static class Subclass5Stand {
  [HarmonyPriority(Priority.High)]
  static void Prefix(Pawn ___pawn){if(Subclass5.Users.Count>0 && Subclass5.Users.Contains(___pawn) && !___pawn.Dead && !___pawn.Downed && ClassCasters.WouldDown(___pawn))Subclass5.TryStand(___pawn);}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class Subclass5NoDeath {
  static bool Prefix(Pawn __instance){
   if(Subclass5.Users.Count==0 || !Subclass5.Users.Contains(__instance) || __instance.Dead)return true;
   if(!Subclass5.TryStand(__instance))return true;
   return __instance.health.ShouldBeDead();
  }
 }
 [HarmonyPatch(typeof(Legendary),nameof(Legendary.Save))]
 public static class Subclass5Rebalance {static void Postfix(Pawn p,int a,int dc,Pawn source,ref bool __result){if(!__result && Subclass5.Users.Count>0 && Subclass5.Rebalance(p,a,dc,source))__result=true;}}
 [HarmonyPatch(typeof(ClassCasters),nameof(ClassCasters.WildSurge))]
 public static class Subclass5Wild {
  static void Prefix(Pawn p,ref int pick){if(Subclass5.LegionWild(p) && Array.IndexOf(Subclass5.BadWild,pick)>=0){var good=new[]{0,1,2,3,5};pick=good[Rand.Range(0,good.Length)];}}
 }
 [HarmonyPatch(typeof(StatPart_Glow),"ActiveFor")]
 public static class Subclass5DarkVision {
  static bool Prepare()=>AccessTools.Method(typeof(StatPart_Glow),"ActiveFor")!=null;
  static void Postfix(Thing t,ref bool __result){if(__result && Subclass5.Users.Count>0 && t is Pawn p && Subclass5.Users.Contains(p) && ClassSystem.Knows(p,"LP_SM_EyesDark"))__result=false;}
 }
 [HarmonyPatch(typeof(NpcClasses),nameof(NpcClasses.AiUsable))]
 public static class Subclass5NoAi {static void Postfix(Ability_Class a,ref bool __result){if(__result && a?.Props!=null && Subclass5.NoAi.Contains(a.Props.effect))__result=false;}}

 // 读旧存档：给术士 3 级以上、还没选子职业的殖民者发一封信（三个新子职业可选）。新游戏不发。
 public class GameComponent_Subclass5 : GameComponent {
  public bool notified;bool pending;
  public GameComponent_Subclass5(Game g){Subclass5.Reset();}
  public override void StartedNewGame(){base.StartedNewGame();notified=true;}
  public override void LoadedGame(){base.LoadedGame();Subclass5.Reset();if(!notified)pending=true;}
  public override void GameComponentTick(){
   if(!pending)return;pending=false;notified=true;
   if(Adventurer.TestSuppressed)return;
   var who=PawnsFinder.AllMaps_FreeColonists.Where(p=>{var h=Adventurer.Get(p);return h!=null && Subclass5.Sorcerer!=null && ClassSystem.Level(h,Subclass5.Sorcerer)>=ClassSubclass.PickLevelFor(Subclass5.Sorcerer) && ClassSubclass.Picked(h,Subclass5.Sorcerer)==null;}).ToList();
   if(who.Count==0)return;
   Find.LetterStack.ReceiveLetter("新的术士子职业","术士多了三个子职业可选：神圣之魂、暗影魔法、时钟之魂。\n\n还没选子职业的术士："+string.Join("、",who.Select(p=>p.LabelShort))+"。选中他们，用「选择子职业」按钮打开窗口。",LetterDefOf.NeutralEvent,new LookTargets(who.Cast<Thing>()));
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref notified,"lpSub5Notified");}
 }
}
