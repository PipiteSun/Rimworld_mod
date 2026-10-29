using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.58.0 天命（规格第一节第 5 条）：预言之子、邪契者、魔网之子、神之勇士。三阶：觉醒（获得时）→ 显现（冒险者 10 级）→ 升华（传奇冒险者）；
 // 冒险者系统关闭（或没有冒险者状态）时：显现 = 殖民 60 天，升华 = 殖民 180 天且完成过 DND 里程碑。预言之子只靠预言兑现升华。
 // 升阶、偿契都在 GameComponent_HeroTraits 的 2500 刻检查里（只遍历有天命的殖民者）。
 public static class HeroDestiny {
  public const int FateAwakenDays=20,FateManifestDays=12,DemandDays=3,DemandMin=15,DemandMax=20,WrathDays=10,MeditateNeed=GenDate.TicksPerHour*8,ReadNeed=GenDate.TicksPerHour,SilverDemand=500;
  public const int VigorBase=15,VigorMax=60,ManifestColonyDays=60,AscendColonyDays=180,ProphecyRaids=10,GraceDays=30,ProphetRadius=5,CharmRadius=6,CharmTicks=600,TentacleRadius=3,BlastStunTicks=60,BlastStunGap=600,DivineDowned=3;
  public const float WeaveSensitivity=.15f,WeaveStaff=1.2f,WeaveEntropy=.75f,WeaveSkip=.15f,ChampionXp=1.3f,ChampionPrayer=2f,ChampionAscension=1.5f,ChampionCooldown=.75f;
  public const string VPEId="vanillaexpanded.vpsycastse";
  public static bool? forceVPE; // 自检
  static bool? vpe;public static bool VPEActive=>forceVPE??vpe??(vpe=ModsConfig.IsActive(VPEId)).Value; // 1.58.6：缓存启用状态
  static int Now=>GenTicks.TicksGame;
  static bool Ready(int tick,int cd){int now=Now;return tick==int.MinValue || now<tick || now-tick>=cd;}
  public static HediffDef VigorDef=>Items.Hediff("LP_FiendishVigor");
  public static string StageLabel(int s)=>s<=0?"觉醒":s==1?"显现":"升华";

  // ---------- 预言 ----------
  public static readonly string[] Prophecies={"杀死一条五色真龙或一名力能龙裔首领","杀死一名巨人首领","击败影巫王拉洛克","击败一名宿敌冠军","殖民地出现一位半神","亲自守住 10 次袭击"};
  public static string ProphecyText(int i)=>i>=0 && i<Prophecies.Length?"当{0}之时，命运才会放过{1}。".Replace("{0}",Prophecies[i]).Replace("{1}","这个孩子"):"（还没有说出口）";
  // 对预言目标伤害 +25%：龙 / 巨人 / 不死 / 宿敌冠军 / 神力 / 袭击领队。
  public static bool ProphecyTarget(int idx,Pawn v){
   switch(idx){
    case 0:return DndCreatureType.Is(v,DndType.Dragon);
    case 1:return DndCreatureType.Is(v,DndType.Giant);
    case 2:return DndCreatureType.Is(v,DndType.Undead);
    case 3:return Adventurer.Get(v)?.nemesis==true;
    case 4:return DndCreatureType.Is(v,DndType.Divine);
    case 5:return AdventurerEnemies.IsLeader(v);
   }
   return false;
  }
  public static bool Boss(Pawn v){var h=Adventurer.Get(v);return v!=null && (AdventurerEnemies.IsLeader(v) || h!=null && (h.storyLevel || h.nemesis || h.legendary) || Legendary.Is(v));}
  public static bool FateReady(Pawn p,Hediff_HeroRecord r)=>r!=null && Ready(r.fateTick,(r.stage>=1?FateManifestDays:FateAwakenDays)*GenDate.TicksPerDay);
  // 命运未尽：预言未兑现时致命伤改为倒地并止血。由 EpicFate 前缀最先调用（冷却比命运之恩短），成功时命运之恩不再触发。
  public static int Saved;
  public static bool TryFate(Pawn p,DamageInfo? dinfo,Hediff culprit){
   var i=HeroTraits.Info(p);if(i==null || !i.prophesied)return false;var r=i.rec;
   if(r==null || r.prophecyDone || !FateReady(p,r) || p.health==null || dinfo==null && culprit==null)return false;
   if(dinfo.HasValue && (dinfo.Value.Def==DamageDefOf.ExecutionCut || dinfo.Value.Def==DamageDefOf.SurgicalCut))return false;
   if(!Epic.Rescue(p))return false;
   r.fateTick=Now;Saved++;
   Items.Mote(p,"命运未尽",new Color(1f,.8f,.3f));
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"本该死去，但预言还没有兑现：命运未尽让其倒地幸存（伤口已止血）。"+(r.stage>=1?FateManifestDays:FateAwakenDays)+" 天后才能再次生效。",p,MessageTypeDefOf.PositiveEvent);
   return true;
  }
  public static bool Fulfill(Pawn p,string why){
   var i=HeroTraits.Info(p);if(i==null || !i.prophesied)return false;var r=i.rec??HeroTraits.EnsureRecord(p);
   if(r==null || r.prophecyDone)return false;
   int from=r.stage;r.prophecyDone=true;r.stage=2;r.masterArmed=false;
   var h=Adventurer.Get(p);float xp=Epic.NextLevelXp(h);if(xp>0f)Adventurer.GainXP(p,xp,false);
   var t=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_ProphecyFulfilled");
   if(t!=null && p.Faction!=null && p.Faction.IsPlayer)foreach(var c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList())c.needs?.mood?.thoughts?.memories?.TryGainMemory(t);
   SyncAbilities(p);
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("预言兑现："+p.LabelShort,"“"+Prophecies[Mathf.Clamp(r.prophecy,0,Prophecies.Length-1)]+"”——"+why+"。\n\n"+p.LabelShort+"的天命升华了（"+StageLabel(from)+" → 升华）：一次性获得冒险者经验 "+xp.ToString("0")+"（下一级所需的全部），全殖民地心情 +8“预言兑现”（10 天）。\n\n命运未尽换成了命运主宰：每天一次，下一次攻击必中且伤害 ×2。",LetterDefOf.PositiveEvent,p);
   return true;
  }
  // 殖民地出现半神（预言 4）。
  public static int CheckColonyDemigod(){
   int n=0;var c=GameComponent_HeroTraits.Get;if(c==null)return 0;
   List<Pawn> want=null;
   foreach(var p in c.destinies){var i=HeroTraits.Info(p);if(i!=null && i.prophesied && i.rec!=null && i.rec.prophecy==4 && !i.rec.prophecyDone)(want??(want=new List<Pawn>())).Add(p);}
   if(want==null)return 0;
   var god=PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.FirstOrDefault(Demigods.Is);if(god==null)return 0;
   foreach(var p in want)if(Fulfill(p,god.LabelShort+"成了半神"))n++;
   return n;
  }

  // ---------- 阶段 ----------
  static float ColonyDays(Pawn p)=>p?.records==null?0f:p.records.GetValue(RecordDefOf.TimeAsColonistOrColonyAnimal)/GenDate.TicksPerDay;
  public static int TargetStage(Pawn p,HeroInfo i,Hediff_HeroRecord r){
   var h=Adventurer.Get(p);bool manifest,ascend;
   if(Adventurer.Enabled && h!=null){manifest=h.level>=HeroTraits.ManifestLevel;ascend=h.legendary;}
   else{float d=ColonyDays(p);manifest=d>=ManifestColonyDays;ascend=d>=AscendColonyDays && h!=null && h.milestonesDone.Any(Epic.DndMilestone);}
   if(i.prophesied)return r.prophecyDone?2:manifest?1:0;
   return ascend?2:manifest?1:0;
  }
  public static bool CheckStage(Pawn p,HeroInfo i,Hediff_HeroRecord r){
   if(p==null || i==null || r==null || !i.Destiny)return false;
   int want=TargetStage(p,i,r);if(r.demoted)want=Math.Min(want,r.stage);
   if(want<=r.stage)return false;
   int from=r.stage;r.stage=want;SyncAbilities(p);
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("天命"+StageLabel(want)+"："+p.LabelShort,p.LabelShort+"的天命“"+HeroTraits.Plain(i.destiny??i.power,i.destiny!=null?i.destinyDegree:i.degree)+"”从"+StageLabel(from)+"进入"+StageLabel(want)+"。\n\n"+StageGains(i,want),LetterDefOf.PositiveEvent,p);
   return true;
  }
  public static string StageGains(HeroInfo i,int s){
   if(i.prophesied)return s>=1?"命运未尽的冷却缩短到 "+FateManifestDays+" 天；倒地时 5 格内友军“为了预言之子”命中 +10%（1 小时）。":"";
   if(i.pact>0){
    if(s==1)return i.pact==1?"邪契魔能爆命中会点燃目标。":i.pact==2?"被命中时 20% 让攻击者迷惑 3 秒。":"邪契魔能爆命中让目标眩晕 1 秒（同一目标 10 秒一次）。";
    return i.pact==1?"火焰伤害 -50%；新能力“直坠地狱”（每天一次）。":i.pact==2?"新能力“妖精魅惑”（每天一次）。":"新能力“异界触须”（每天一次）。";
   }
   if(i.weave)return s==1?(VPEActive?"每次施法 15% 不进冷却。":"法杖射程 +3。"):"新能力“魔网涌动”（每天一次，60 秒内施法不耗心灵熵、不进冷却）；魔网震颤和魔网崩溃不再影响"+"施法。";
   if(i.champion>0){var g=ChampionGod(i.champion);return s==1?"成神之路进度 ×1.5，"+(g?.label??"神")+"的领域神术冷却 -25%。":"神明干预：同一张地图上 3 名以上殖民者倒地时，"+(g?.label??"神")+"降下神迹（每季一次）："+InterventionLabel(i.champion)+"。";}
   return "";
  }
  public static void Periodic(Pawn p,int now){
   var i=HeroTraits.Info(p);if(i==null || !i.Destiny)return;var r=i.rec??HeroTraits.EnsureRecord(p);if(r==null)return;
   CheckStage(p,i,r);
   if(i.pact>0)PactTick(p,i,r,now);
  }

  // ---------- 能力 ----------
  public static readonly string[] OurAbilities={"LP_HeroPactBlast","LP_HeroFeyStep","LP_HeroHurlHell","LP_HeroFeyCharm","LP_HeroTentacles","LP_HeroFateMaster","LP_HeroWeaveSurge"};
  public static IEnumerable<string> Wanted(HeroInfo i){
   if(i==null)yield break;int s=i.Stage;
   if(i.pact>0){yield return "LP_HeroPactBlast";if(i.pact==2)yield return "LP_HeroFeyStep";if(s>=2)yield return i.pact==1?"LP_HeroHurlHell":i.pact==2?"LP_HeroFeyCharm":"LP_HeroTentacles";}
   if(i.prophesied && i.rec!=null && i.rec.prophecyDone)yield return "LP_HeroFateMaster";
   if(i.weave && s>=2)yield return "LP_HeroWeaveSurge";
  }
  public static void SyncAbilities(Pawn p){
   if(p?.abilities==null)return;var want=new HashSet<string>(Wanted(HeroTraits.Info(p)));
   foreach(var n in OurAbilities){
    var d=DefDatabase<AbilityDef>.GetNamedSilentFail(n);if(d==null)continue;bool has=p.abilities.GetAbility(d,true)!=null;
    if(want.Contains(n) && !has)p.abilities.GainAbility(d);else if(!want.Contains(n) && has)p.abilities.RemoveAbility(d);
   }
  }
  public static bool IsDaily(AbilityDef d)=>d!=null && (d.defName=="LP_HeroHurlHell" || d.defName=="LP_HeroFeyCharm" || d.defName=="LP_HeroTentacles" || d.defName=="LP_HeroFateMaster" || d.defName=="LP_HeroWeaveSurge");

  // 邪契魔能爆：道数按冒险者等级（同 1.55.0），显现后邪魔点燃、旧日眩晕（同一目标 10 秒一次）。
  static readonly Dictionary<int,int> lastStun=new Dictionary<int,int>();
  public static int Blast(Pawn caster,Pawn target){
   if(caster?.Map==null || target==null)return 0;var map=caster.Map;var i=HeroTraits.Info(caster);int n=Pacts.Beams(caster),hits=0;var t=target;int now=Now;
   for(int k=0;k<n;k++){
    if(t==null || t.Dead || t.Downed || !t.Spawned){t=GenRadial.RadialDistinctThingsAround(target.PositionHeld,map,6f,true).OfType<Pawn>().Where(x=>x!=caster && !x.Dead && !x.Downed && x.HostileTo(caster)).OrderBy(x=>x.Position.DistanceToSquared(target.PositionHeld)).FirstOrDefault();if(t==null)break;}
    var a=caster.DrawPos;var b=t.DrawPos;for(int s=1;s<=4;s++)FleckMaker.ThrowLightningGlow(a+(b-a)*(s/5f),map,.6f);
    t.TakeDamage(new DamageInfo(Pacts.Eldritch,Pacts.BlastDamage,Pacts.BlastPen,-1f,caster,null,null,DamageInfo.SourceCategory.ThingOrUnknown,t));hits++;
    if(i!=null && i.Stage>=1 && !t.Dead){
     if(i.pact==1)FireUtility.TryAttachFire(t,.3f,caster);
     if(i.pact==3 && (!lastStun.TryGetValue(t.thingIDNumber,out int at) || now<at || now-at>=BlastStunGap)){lastStun[t.thingIDNumber]=now;t.stances?.stunner?.StunFor(BlastStunTicks,caster,false,true,false);}
    }
   }
   return hits;
  }
  public static bool Wrathful(Pawn p)=>Items.Has(p,"LP_PatronWrath");
  public static bool FeyStep(Pawn p,IntVec3 c){
   if(p?.Map==null || !c.InBounds(p.Map) || !c.Standable(p.Map) || c.GetFirstPawn(p.Map)!=null)return false;
   FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,1.2f);p.Position=c;p.Notify_Teleported();FleckMaker.Static(c,p.Map,FleckDefOf.PsycastAreaEffect,1.2f);
   return true;
  }
  public static Hediff Torment(Pawn target,string def,Pawn source){
   var d=Items.Hediff(def);if(d==null || target?.health==null || target.Dead)return null;
   var old=target.health.hediffSet.GetFirstHediffOfDef(d);if(old!=null)target.health.RemoveHediff(old);
   var h=(Hediff_HeroTorment)HediffMaker.MakeHediff(d,target);h.source=source;target.health.AddHediff(h);return h;
  }
  public static bool Immovable(Pawn v)=>Boss(v) || Demigods.Is(v) || Twilight.Vessel(v)!=null;
  public static int Charm(Pawn p){
   if(p?.Map==null)return 0;int n=0;
   foreach(var o in GenRadial.RadialDistinctThingsAround(p.Position,p.Map,CharmRadius,true).OfType<Pawn>().ToList()){
    if(o==p || o.Dead || o.Downed || !o.HostileTo(p) || Immovable(o))continue;
    o.stances?.stunner?.StunFor(CharmTicks,p,false,true,false);Items.Mote(o,"魅惑",new Color(.7f,1f,.7f));n++;
   }
   FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,CharmRadius);return n;
  }
  public static int Tentacles(Pawn p,IntVec3 c){
   if(p?.Map==null || !c.InBounds(p.Map))return 0;int n=0;
   foreach(var o in GenRadial.RadialDistinctThingsAround(c,p.Map,TentacleRadius,true).OfType<Pawn>().ToList())if(o!=p && !o.Dead && o.HostileTo(p) && Torment(o,"LP_EldritchTentacles",p)!=null)n++;
   FleckMaker.Static(c,p.Map,FleckDefOf.PsycastAreaEffect,TentacleRadius*2f);return n;
  }
  public static void Arm(Pawn p){var r=HeroTraits.Record(p);if(r==null)return;r.masterArmed=true;Items.Mote(p,"命运主宰",new Color(1f,.8f,.3f));}
  public static bool Surging(Pawn p)=>Items.Has(p,"LP_WeaveSurge");
  public static void Surge(Pawn p){Items.Give(p,"LP_WeaveSurge");Items.Mote(p,"魔网涌动",new Color(.5f,.7f,1f));}

  // ---------- 魔网之子 ----------
  public static float StaffFactor(Pawn p)=>HeroTraits.Info(p)?.weave==true?WeaveStaff:1f;
  public static bool WeaveImmune(Pawn p){var i=HeroTraits.Info(p);return i!=null && i.weave && i.Stage>=2;}
  // VPE 桥：灵能熵 ×0.75（涌动期间 0）；显现后每次施法 15% 不进冷却（涌动期间不进冷却）。
  public static float VpeEntropyFactor(Pawn p){var i=HeroTraits.Info(p);if(i==null)return 1f;if(i.ritual && Feats2.Primed(p))return 0f;if(!i.weave)return 1f;return Surging(p)?0f:WeaveEntropy;} // 1.58.1 仪式余韵也走这里
  public static bool VpeSkipCooldown(Pawn p){var i=HeroTraits.Info(p);if(i==null || !i.weave)return false;return Surging(p) || i.Stage>=1 && Rand.Chance(WeaveSkip);}
  public static int Cooldown(Pawn p,HeroInfo i,AbilityDef d,int ticks){
   if(i.ritual && d?.defName!="LP_FeatRitualCast" && Feats2.Primed(p)){var h=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_RitualPrimed")) as HediffWithComps;var c=h?.TryGetComp<HediffComp_Disappears>();if(c!=null)c.ticksToDisappear=1;return 0;} // 1.58.1 仪式余韵：这一次不进冷却（同一刻的心灵熵也免）
   if(i.weave && !IsDaily(d) && Surging(p))return 0;
   if(i.champion>0 && i.Stage>=1){var g=ChampionGod(i.champion);if(g?.domainAbility!=null && g.domainAbility==d)ticks=Mathf.RoundToInt(ticks*ChampionCooldown);}
   return ticks;
  }

  // ---------- 神之勇士 ----------
  public static LP_DeityDef ChampionGod(int degree)=>FaithUtility.DegreeToDeity(degree);
  static DndType ChampionType(string key){
   switch(key){case "Lathander":case "Kelemvor":return DndType.Undead;case "Tyr":return DndType.Fiend;case "Mystra":case "Shar":return DndType.Aberration;case "Malar":return DndType.Beast;case "Talos":return DndType.Giant;case "Bane":return DndType.Divine;case "Surtr":return DndType.Dragon;}
   return DndType.None;
  }
  // 敌对类型 + 敌对神的派系（派系 FaithExtension 里供奉的是本神的敌对神）。
  public static bool ChampionTarget(int degree,Pawn v){
   var g=ChampionGod(degree);if(g==null || v==null)return false;
   if(DndCreatureType.Is(v,ChampionType(g.key)))return true;
   var e=v.Faction?.def?.GetModExtension<FaithExtension>();if(e?.deities!=null)foreach(var d in e.deities)if(g.EnemyOf(d))return true;
   return false;
  }
  public static string ChampionTargetLabel(int degree){var g=ChampionGod(degree);if(g==null)return "";var en=g.Enemies.Select(x=>x.label).ToList();return DndCreatureType.Label(ChampionType(g.key))+(en.Count>0?"，以及供奉"+string.Join("、",en)+"的派系":"");}
  public static float ClassXpFactor(Pawn p,string path)=>(path=="LP_Path_Cleric" || path=="LP_Path_Paladin") && HeroTraits.Info(p)?.champion>0?ChampionXp:1f;
  public static float PrayerFactor(Pawn p)=>HeroTraits.Info(p)?.champion>0?ChampionPrayer:1f;
  public static int AscensionScale(Pawn p,int n){var i=HeroTraits.Info(p);if(n<=0 || i==null || i.champion<=0 || i.Stage<1)return n;return GenMath.RoundRandom(n*ChampionAscension);}
  public static int DemigodStartBonus(Pawn p)=>HeroTraits.Info(p)?.champion>0?1:0;
  public static bool DivineReady(Hediff_HeroRecord r)=>r!=null && Ready(r.divineTick,GenDate.TicksPerQuadrum);
  public static bool ChampionsWaiting=>GameComponent_HeroTraits.Get?.champions.Count>0;
  public static string InterventionLabel(int degree){
   switch(ChampionGod(degree)?.key){
    case "Lathander":return "晨光普照（全员治愈伤口、止血）";case "Tyr":return "公正之剑（30 秒全员近战伤害 +50%）";case "Mystra":return "魔网断流（打断敌方施法，敌人全部眩晕 5 秒）";
    case "Kelemvor":return "死亡止步（倒地者止血，1 天内不再流血）";case "Malar":return "兽王咆哮（敌人恐慌逃跑 10 秒）";case "Talos":return "雷霆之怒（最多 6 道闪电，每道 30 伤害）";
    case "Bane":return "暴君之威（敌人惊惧 1 小时）";case "Shar":return "暗夜帷幕（殖民者隐身 30 秒）";case "Surtr":return "熔炉之怒（敌人着火并受到 20 点火焰伤害）";
   }
   return "神迹";
  }
  public static int Interventions;
  public static bool CheckIntervention(Map map){
   var c=GameComponent_HeroTraits.Get;if(map==null || c==null || c.champions.Count==0)return false;
   if(map.mapPawns.FreeColonistsSpawned.Count(x=>x.Downed)<DivineDowned)return false;
   foreach(var p in c.champions.ToList()){
    if(p.Dead || p.MapHeld!=map)continue;var i=HeroTraits.Info(p);var r=i?.rec;if(r==null || i.Stage<2 || !DivineReady(r))continue;
    r.divineTick=Now;Intervene(p,i,map);return true;
   }
   return false;
  }
  public static int Intervene(Pawn champ,HeroInfo i,Map map){
   var g=ChampionGod(i.champion);if(g==null || map==null)return 0;Interventions++;int n=0;
   var colonists=map.mapPawns.FreeColonistsSpawned.ToList();
   var enemies=map.mapPawns.AllPawnsSpawned.Where(o=>!o.Dead && o.HostileTo(Faction.OfPlayer)).ToList();
   switch(g.key){
    case "Lathander":foreach(var c in colonists){foreach(var inj in c.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>!x.IsPermanent()).ToList())inj.Heal(inj.Severity*.6f);foreach(var inj in c.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>x.Bleeding).ToList())inj.Tended(.6f,1f);n++;}break;
    case "Tyr":foreach(var c in colonists)if(Items.Give(c,"LP_DivineBlessing")!=null)n++;break;
    case "Mystra":foreach(var o in enemies){if(Feats.Casting(o))o.jobs?.EndCurrentJob(JobCondition.InterruptForced);o.stances?.stunner?.StunFor(300,champ,false,true,false);n++;}break;
    case "Kelemvor":foreach(var c in colonists.Where(x=>x.Downed)){foreach(var inj in c.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>x.Bleeding).ToList())inj.Tended(.5f,1f);Items.Give(c,"LP_DivineRespite");n++;}break;
    case "Malar":foreach(var o in enemies){if(o.Downed)continue;if(o.mindState?.mentalStateHandler!=null && o.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"兽王咆哮",true,true) && o.MentalState!=null)o.MentalState.forceRecoverAfterTicks=600;else o.stances?.stunner?.StunFor(600,champ,false,true,false);n++;}break;
    case "Talos":{var shock=HeroCombat.ElementDef(3);foreach(var o in enemies.Where(x=>!x.Downed).InRandomOrder().Take(6).ToList()){FleckMaker.ThrowLightningGlow(o.DrawPos,map,2f);o.TakeDamage(new DamageInfo(shock,30f,.3f,-1f,champ));n++;}break;}
    case "Bane":foreach(var o in enemies)if(Items.Give(o,"LP_Frightened",GenDate.TicksPerHour)!=null)n++;break;
    case "Shar":foreach(var c in colonists){var h=c.health.AddHediff(MindFlayerPowers.Unseen);h.TryGetComp<HediffComp_Disappears>()?.SetDuration(1800);n++;}break;
    case "Surtr":foreach(var o in enemies){FireUtility.TryAttachFire(o,.5f,champ);o.TakeDamage(new DamageInfo(DamageDefOf.Flame,20f,0f,-1f,champ));n++;}break;
   }
   FleckMaker.Static(champ.PositionHeld,map,FleckDefOf.PsycastAreaEffect,8f);
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("神明干预："+g.label,"殖民地快撑不住了——"+g.FullLabel+"没有抛弃祂的勇士"+champ.LabelShort+"。\n\n"+InterventionLabel(i.champion)+"。\n\n下一次神明干预要等一季（15 天）。",LetterDefOf.PositiveEvent,champ);
   return n;
  }

  // ---------- 倒地 ----------
  public static void Downed(Pawn p,HeroInfo i){
   var r=i.rec;
   if(i.prophesied && i.Stage>=1 && p.Map!=null){
    foreach(var o in GenRadial.RadialDistinctThingsAround(p.Position,p.Map,ProphetRadius,true).OfType<Pawn>().ToList())if(o!=p && !o.Dead && !o.Downed && o.Faction!=null && o.Faction==p.Faction && o.RaceProps.Humanlike)Items.Give(o,"LP_ForTheProphesied");
   }
   if(i.returned && r!=null && Ready(r.graceTick,GraceDays*GenDate.TicksPerDay)){r.graceTick=Now;Items.Give(p,"LP_DeathGrace");Items.Mote(p,"冥界的宽限",new Color(.6f,.8f,.8f));}
  }

  // ---------- 偿契 ----------
  public static int DemandIntervalTicks()=>Rand.RangeInclusive(DemandMin,DemandMax)*GenDate.TicksPerDay;
  public static string PatronName(int branch){
   if(branch==1)return Pacts.Patron("LP_Patron_Asmodeus")?.label??"九狱的大魔鬼";
   if(branch==2)return Pacts.Patron("LP_Patron_Titania")?.label??"妖精女王";
   return "星界之外的无名存在";
  }
  public static string DemandKey(int branch)=>branch==1?"fiend":branch==2?"fey":"old";
  public static string DemandLabel(string key){
   switch(key){
    case "fiend":return "3 天内处决一名囚犯，或交出 "+SilverDemand+" 银";
    case "fey":return "3 天内举办一次派对或婚礼，或交出一件品质优秀以上的艺术品";
    case "old":return "3 天内本人冥想 8 小时，或研读耐瑟瑞尔卷轴 1 小时";
   }
   return "";
  }
  public static string DemandStatus(Hediff_HeroRecord r){
   if(r==null)return "";
   if(r.demand==null)return r.nextDemandTick<0?"宗主还没有提要求。":"下一次偿契：约 "+Mathf.Max(0f,(r.nextDemandTick-Now)/(float)GenDate.TicksPerDay).ToString("0.#")+" 天后";
   var s="当前要求："+DemandLabel(r.demand)+"（还剩 "+Mathf.Max(0,r.demandDeadline-Now).ToStringTicksToPeriod()+"）";
   if(r.demand=="old")s+="；已冥想 "+(r.meditateTicks/(float)GenDate.TicksPerHour).ToString("0.#")+" 小时，已研读 "+(r.readTicks/(float)GenDate.TicksPerHour).ToString("0.#")+" 小时";
   return s;
  }
  public static void PactTick(Pawn p,HeroInfo i,Hediff_HeroRecord r,int now){
   if(p.Faction==null || !p.Faction.IsPlayer || p.Dead)return;
   if(r.nextDemandTick<0){r.nextDemandTick=now+DemandIntervalTicks();return;}
   if(r.demand==null){if(now>=r.nextDemandTick)StartDemand(p,i,r,now);return;}
   if(DemandMet(r)){Paid(p,r,now);return;}
   if(now>=r.demandDeadline)Failed(p,i,r,now);
  }
  public static void StartDemand(Pawn p,HeroInfo i,Hediff_HeroRecord r,int now){
   r.demand=DemandKey(i.pact);r.demandStart=now;r.demandDeadline=now+DemandDays*GenDate.TicksPerDay;r.demandPaid=false;r.meditateTicks=0;r.readTicks=0;
   if(Adventurer.TestSuppressed)return;
   var def=DefDatabase<LetterDef>.GetNamedSilentFail("LP_HeroPactLetter");
   string label=PatronName(i.pact)+"要求偿契";
   string text=p.LabelShort+"的宗主"+PatronName(i.pact)+"捎来了话：偿契的时候到了。\n\n要求："+DemandLabel(r.demand)+"。"+(r.demand=="old"?"":"\n\n交东西用下面的按钮（从地图上直接扣）。")+"\n\n完不成：宗主之怒 10 天（邪契魔能爆不能用、心情 -10）；连续两次完不成，天命降一阶（觉醒阶再连败，契约作废，“背弃之契”心情 -15）。";
   if(def!=null && LetterMaker.MakeLetter(def) is ChoiceLetter_HeroPact l){l.pawn=p;l.Label=label;l.Text=text;l.lookTargets=p.Spawned?new LookTargets(p):LookTargets.Invalid;Find.LetterStack.ReceiveLetter(l);}
   else Find.LetterStack.ReceiveLetter(label,text,LetterDefOf.NeutralEvent,p);
  }
  public static bool DemandMet(Hediff_HeroRecord r){
   if(r?.demand==null)return false;if(r.demandPaid)return true;var c=GameComponent_HeroTraits.Get;
   switch(r.demand){
    case "fiend":return c!=null && c.lastExecutionTick!=int.MinValue && c.lastExecutionTick>=r.demandStart;
    case "fey":return c!=null && c.lastGatheringTick!=int.MinValue && c.lastGatheringTick>=r.demandStart;
    case "old":return r.meditateTicks>=MeditateNeed || r.readTicks>=ReadNeed;
   }
   return false;
  }
  public static Map HomeOf(Pawn p)=>p?.MapHeld??Find.Maps.FirstOrDefault(m=>m.IsPlayerHome);
  public static Thing ArtFor(Map map)=>Pacts.ItemsFor(map,new PactClauseState{category="Art"}).FirstOrDefault();
  public static string CannotDeliver(Pawn p,Hediff_HeroRecord r){
   var map=HomeOf(p);if(map==null || r?.demand==null)return "没有可以交付的要求";
   if(r.demand=="fiend")return Pacts.SilverOn(map)>=SilverDemand?null:"地图上的白银不到 "+SilverDemand;
   if(r.demand=="fey")return ArtFor(map)!=null?null:"地图上没有品质优秀以上的艺术品";
   return "这一条要本人去做";
  }
  public static bool Deliver(Pawn p,Hediff_HeroRecord r){
   if(CannotDeliver(p,r)!=null)return false;var map=HomeOf(p);
   if(r.demand=="fiend")Pacts.TakeSilver(map,SilverDemand);else ArtFor(map)?.Destroy();
   r.demandPaid=true;return true;
  }
  public static void Paid(Pawn p,Hediff_HeroRecord r,int now){
   r.demand=null;r.demandPaid=false;r.pactFails=0;bool was=r.demoted;r.demoted=false;r.nextDemandTick=now+DemandIntervalTicks();
   if(!Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"偿还了契约。"+(was?"宗主的怒气消了：天命可以重新升阶。":""),p,MessageTypeDefOf.PositiveEvent,false);
  }
  public static int Demotions;
  public static void Failed(Pawn p,HeroInfo i,Hediff_HeroRecord r,int now){
   r.demand=null;r.demandPaid=false;r.pactFails++;r.nextDemandTick=now+DemandIntervalTicks();
   Items.Give(p,"LP_PatronWrath",WrathDays*GenDate.TicksPerDay);
   var wrath=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_PatronWrathMood");if(wrath!=null)p.needs?.mood?.thoughts?.memories?.TryGainMemory(wrath);
   string who=PatronName(i.pact);
   if(r.pactFails<2){if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("宗主之怒："+p.LabelShort,p.LabelShort+"没能偿还"+who+"的契约。宗主之怒 10 天：邪契魔能爆不能用，心情 -10。\n\n再失败一次，天命会降一阶。",LetterDefOf.NegativeEvent,p);return;}
   r.pactFails=0;Demotions++;
   if(r.stage>0){
    int from=r.stage;r.stage--;r.demoted=true;SyncAbilities(p);
    if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("契约降阶："+p.LabelShort,p.LabelShort+"连续两次没能偿还"+who+"的契约：天命从"+StageLabel(from)+"降到"+StageLabel(r.stage)+"。下一次偿契成功后才会重新升阶。",LetterDefOf.NegativeEvent,p);
    return;
   }
   var t=p.story?.traits?.GetTrait(HeroDefOf.LP_PactDestiny);if(t!=null)p.story.traits.RemoveTrait(t);
   var forsaken=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_PactForsaken");if(forsaken!=null)p.needs?.mood?.thoughts?.memories?.TryGainMemory(forsaken);
   Items.Remove(p,"LP_PatronWrath");
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("背弃之契："+p.LabelShort,who+"抛弃了"+p.LabelShort+"：邪契者的特性和魔能爆都消失了。心情 -15“背弃之契”（15 天）。",LetterDefOf.NegativeEvent,p);
  }
  // 旧日的偿契：本人冥想 / 研读耐瑟瑞尔卷轴的时间（250 刻采样，只看有当期要求的人）。
  public static void SampleOldOnes(List<Pawn> list,int delta){
   foreach(var p in list){
    var r=HeroTraits.Record(p);if(r?.demand!="old" || p.Dead)continue;
    if(p.CurJobDef==JobDefOf.Meditate)r.meditateTicks+=delta;
    if(NetherilJoy.ReadingNetheril(p))r.readTicks+=delta;
   }
  }
 }

 // ---------- 能力效果（XML 里 effect 选择） ----------
 public class CompProperties_HeroPower : CompProperties_AbilityEffect {public string effect;public CompProperties_HeroPower(){compClass=typeof(CompAbilityEffect_HeroPower);}}
 public class CompAbilityEffect_HeroPower : CompAbilityEffect {
  string Effect=>((CompProperties_HeroPower)props).effect;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   base.Apply(target,dest);var p=parent.pawn;
   switch(Effect){
    case "blast":if(target.Pawn!=null)HeroDestiny.Blast(p,target.Pawn);break;
    case "feystep":HeroDestiny.FeyStep(p,target.Cell);break;
    case "hell":if(target.Pawn!=null)HeroDestiny.Torment(target.Pawn,"LP_HurledThroughHell",p);break;
    case "charm":HeroDestiny.Charm(p);break;
    case "tentacles":HeroDestiny.Tentacles(p,target.Cell);break;
    case "fate":HeroDestiny.Arm(p);break;
    case "weave":HeroDestiny.Surge(p);break;
   }
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   if(Effect=="feystep"){var map=parent.pawn?.Map;if(map==null || !target.Cell.InBounds(map) || !target.Cell.Standable(map) || target.Cell.GetFirstPawn(map)!=null){if(throwMessages)Messages.Message("那里站不了人。",MessageTypeDefOf.RejectInput,false);return false;}}
   if((Effect=="blast" || Effect=="hell") && (target.Pawn==null || target.Pawn.Dead))return false;
   return base.Valid(target,throwMessages);
  }
  public override bool AICanTargetNow(LocalTargetInfo target){var t=target.Pawn;return t!=null && !t.Dead && !t.Downed && t.HostileTo(parent.pawn);}
  public override bool GizmoDisabled(out string reason){
   if(Effect=="blast" && HeroDestiny.Wrathful(parent.pawn)){reason="宗主之怒：魔能爆暂时用不了。";return true;}
   if(Effect=="fate" && HeroTraits.Record(parent.pawn)?.masterArmed==true){reason="命运主宰已经蓄势，等下一次攻击。";return true;}
   return base.GizmoDisabled(out reason);
  }
  public override string ExtraLabelMouseAttachment(LocalTargetInfo target)=>Effect=="blast"?"邪契魔能爆 ×"+Pacts.Beams(parent.pawn):null;
 }
 // 直坠地狱 / 异界触须：动弹不得，每秒受伤。
 public class Hediff_HeroTorment : HediffWithComps {
  public Pawn source;int acc;
  public override void PostAdd(DamageInfo? dinfo){
   base.PostAdd(dinfo);int dur=this.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear??600;
   pawn.stances?.stunner?.StunFor(dur,source,false,true,false);
  }
  bool Hell=>def.defName=="LP_HurledThroughHell";
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<60 || pawn==null || pawn.Dead)return;acc=0;
   pawn.TakeDamage(new DamageInfo(Hell?HeroCombat.ElementDef(1):Pacts.Eldritch,Hell?6f:3f,0f,-1f,source,null,null,DamageInfo.SourceCategory.ThingOrUnknown,pawn));
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref source,"lpSource");Scribe_Values.Look(ref acc,"lpAcc");}
 }
 // 偿契信件：交银子 / 交艺术品。
 public class ChoiceLetter_HeroPact : ChoiceLetter {
  public Pawn pawn;
  public override IEnumerable<DiaOption> Choices{get{
   if(ArchivedOnly){yield return Option_Close;yield break;}
   var r=HeroTraits.Record(pawn);
   if(pawn!=null && !pawn.Dead && r?.demand!=null && r.demand!="old" && !r.demandPaid){
    var why=HeroDestiny.CannotDeliver(pawn,r);
    var o=new DiaOption(r.demand=="fiend"?"交出 "+HeroDestiny.SilverDemand+" 银":"交出一件优秀以上的艺术品"){action=()=>{Find.LetterStack.RemoveLetter(this);if(HeroDestiny.Deliver(pawn,r))Messages.Message("交付了偿契的东西。",pawn,MessageTypeDefOf.NeutralEvent,false);},resolveTree=true};
    if(why!=null)o.Disable(why);yield return o;
    yield return Option_Postpone;
   }else yield return Option_Close;
  }}
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref pawn,"lpPawn");}
 }
}
