using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.59.0 超凡属性特性（见 ArtSource/ExaltedScores-1.59.0-spec.md）：不是 TraitDef。冒险者状态重建属性缓存（BuildStage）时算两个 6 位掩码：
 // exaltedMask（有效属性 ≥ 20）、mythicMask（≥ 24，或 ≥ 20 + 传奇冒险者 + 对应两项传奇属性点合计 ≥ 20）。掩码不存档（读档重建），只存“已发过信”的两个掩码。
 // 热路径：先看 Exalted.Count（有掩码的人数）是 0 就返回，再按 thingIDNumber 查合并掩码（低 6 位超凡、高 6 位神话）。
 // 周期：携带者自己的冒险者状态每 250 刻（Hediff_Adventurer.TickInterval 原有的 250 刻钩）做光环、显形、封顶、心灵效果减半；硬撑到期判定在同一个 TickInterval 里比较一个时间戳。
 public static class Exalted {
  public const int Threshold=20,MythicScore=24,MythicLegend=20,AuraInterval=250;
  public const float StrCarry=1.25f,StrMining=1.2f,StrPen=.15f,MythicPen=.3f,StaggerChance=.15f,StaggerSize=1.5f,MythicStaggerSize=3f,MythicBuilding=2f;
  public const float DexAim=.8f,DexDodge=10f,DexMove=.2f,DexShot=.85f,MythicShot=.75f,EvadeChance=.25f;
  public const float ConBleed=.7f,ConPain=.15f,BloodCap=.58f;
  public const float IntRead=1.5f,IntEcho=.25f,MythicEcho=.5f,MythicLearn=1.2f; // 1.92.0：原 1.3 / 10% / 20%，没有学习速度
  public const float WisFocus=1.3f,WisTend=1.1f,WisRadius=8f,MythicWisRadius=15f,PsychicHalf=.5f;
  public const float ChaTrade=.05f,ChaPersuade=1.15f,ChaRadius=10f,MythicChaRadius=15f,ChaBreak=-.03f,ChaEnemyRadius=6f,ChaEnemyHit=.08f,RoutRadius=8f,RoutChance=.15f,RepFactor=1.5f;
  public const float CostPer=.04f,MythicCostPer=.08f;
  public const int StaggerTicks=90,HoldTicks=480,MythicHoldTicks=720,HoldPeriod=GenDate.TicksPerDay,EvadeWindow=600,ScarTicks=5*GenDate.TicksPerDay,MythicScarTicks=2*GenDate.TicksPerDay;
  public static readonly string[] ExaltedNames={"巨人之力","疾影","不屈之躯","洞彻心智","明察","威仪"};
  public static readonly string[] MythicNames={"撼山","无影","金刚不坏","天才","先知","君临"};
  // 神话的传奇属性点：力→近战+血量；敏→远程+近战；体→血量+承受；智→学习+工作；感→心灵+回复；魅→心灵+工作。
  public static readonly int[][] LegendPairs={new[]{Legend.Melee,Legend.Health},new[]{Legend.Ranged,Legend.Melee},new[]{Legend.Health,Legend.Tough},new[]{Legend.Learn,Legend.Work},new[]{Legend.Psychic,Legend.Recovery},new[]{Legend.Psychic,Legend.Work}};
  // 封顶在致死前的原版疾病（只对有 lethalSeverity 的生效；食物中毒、肠道蠕虫、肌肉寄生虫在 1.7 不会致死，名单里保留但不起作用）。
  public static readonly HashSet<string> Diseases=new HashSet<string>{"FoodPoisoning","Flu","Plague","Malaria","SleepingSickness","GutWorms","MuscleParasites"};
  // 持续时间减半的心灵类状态（心灵震爆、夺心魔支配、群脑恍惚、魅惑）。
  public static readonly HashSet<string> PsychicHediffs=new HashSet<string>{"LP_MindBlasted","LP_Dominated","LP_PsychicDaze","LP_HeroFeyCharm"};
  public static float? TestRoll;public static float Roll=>TestRoll??Rand.Value;
  public static int Letters; // 自检计数

  // ---------- 开关 ----------
  // 总开关关、冒险者系统关，或 Isekai 共存模式下六维交给 Isekai 时掩码视为 0；勾选“共存模式下仍生效”例外。
  public static bool On{get{var s=LichMod.Settings;if(s==null)return true;if(!s.adventurer || !s.exaltedScores)return false;return !Adventurer.EffectsHandedToIsekai || s.exaltedCoexist;}}
  // 热修1：效果没生效的原因（生效时 null），分页和健康页标记的悬停用。
  public static string OffReason{get{
   var s=LichMod.Settings;if(s==null)return null;
   if(!s.adventurer)return "冒险者等级已在设置里关闭";
   if(!s.exaltedScores)return "超凡属性特性已在设置里关闭";
   if(Adventurer.EffectsHandedToIsekai && !s.exaltedCoexist)return "Isekai 共存模式：效果已关，可在设置「冒险者」中开启（勾选“共存模式下超凡特性仍生效”）";
   return null;
  }}

  // ---------- 掩码 ----------
  public static bool MythicFor(Hediff_Adventurer h,int a){
   int v=h.Eff(a);if(v>=MythicScore)return true;
   if(v<Threshold || !h.legendary || h.legendSpent==null)return false;
   var pr=LegendPairs[a];return h.legendSpent[pr[0]]+h.legendSpent[pr[1]]>=MythicLegend; // 不乘种族亲和
  }
  // 返回合并掩码（低 6 位超凡、高 6 位神话）。神话的那一项一定也是超凡。
  public static int Compute(Hediff_Adventurer h)=>On?Raw(h):0;
  // 热修1：不看开关、只看有效值的掩码。分页的“超凡”小节、金色数字和健康页标记用它：效果关着时照样显示（灰色，悬停写原因）。
  public static int Raw(Hediff_Adventurer h){
   if(h==null || h.scores==null)return 0;int ex=0,my=0;
   for(int a=0;a<6;a++){if(h.scores[a]<Threshold || h.Eff(a)<Threshold)continue;ex|=1<<a;if(MythicFor(h,a))my|=1<<a;} // 原始值不到 20 就不算上限
   return ex|my<<6;
  }
  static readonly System.Collections.Concurrent.ConcurrentDictionary<int,int> holders=new System.Collections.Concurrent.ConcurrentDictionary<int,int>();
  static int count;public static int Count=>count;
  internal static void Store(Pawn p,int mask){
   if(p==null)return;int id=p.thingIDNumber;
   if(mask==0){if(holders.TryRemove(id,out _))System.Threading.Interlocked.Decrement(ref count);return;}
   bool added=false;holders.AddOrUpdate(id,k=>{added=true;return mask;},(k,o)=>mask);if(added)System.Threading.Interlocked.Increment(ref count);
  }
  public static int MaskOf(Pawn p){if(count==0 || p==null)return 0;return holders.TryGetValue(p.thingIDNumber,out int m)?m:0;}
  public static bool Has(Pawn p,int a)=>(MaskOf(p)&1<<a)!=0;
  public static bool Mythic(Pawn p,int a)=>(MaskOf(p)&1<<(a+6))!=0;
  // 读档（FinalizeInit）：清掉上一个存档的登记，再按已经算好的掩码重登（读档时 CurStage 可能已经建好，不会再走 BuildStage）。
  public static void Reset(){holders.Clear();count=0;chaCarriers.Clear();evadeTick.Clear();revealed.Clear();foreach(var h in Adventurer.Registered)if(h.live && h.pawn!=null && !h.pawn.Destroyed)Store(h.pawn,h.exaltedMask|h.mythicMask<<6);}
  static int Bits(int m){int n=0;for(;m!=0;m&=m-1)n++;return n;}
  // 1.48.1 战斗力折算：每个超凡项 +4%，神话项 +8%（神话不再另算超凡的 4%）。
  public static float CostFactor(Pawn p){int m=MaskOf(p);if(m==0)return 1f;int my=m>>6&63,ex=m&63&~my;return 1f+CostPer*Bits(ex)+MythicCostPer*Bits(my);}

  // ---------- 属性（合进冒险者状态的 CurStage；不乘属性效果强度，不乘种族亲和） ----------
  static void F(List<StatModifier> l,string stat,float v){var d=DefDatabase<StatDef>.GetNamedSilentFail(stat);if(d!=null)l.Add(new StatModifier{stat=d,value=v});}
  public static void AddStats(Hediff_Adventurer h,HediffStage st,List<StatModifier> f,List<StatModifier> o){
   int m=h.exaltedMask;if(m==0)return;
   if((m&1)!=0){F(f,"CarryingCapacity",StrCarry);F(f,"MiningSpeed",StrMining);}
   if((m&2)!=0){F(f,"AimingDelayFactor",DexAim);F(o,"MeleeDodgeChance",DexDodge);if(h.HasFeat(Feats.Mobile))F(o,"MoveSpeed",DexMove);} // 已有灵活：免借机攻击重叠，改为移速
   if((m&4)!=0){st.totalBleedFactor*=ConBleed;F(o,"PainShockThreshold",ConPain);}
   if((m&8)!=0){F(f,"ReadingSpeed",IntRead);if((h.mythicMask&8)!=0)F(f,"GlobalLearningFactor",MythicLearn);}
   if((m&16)!=0){F(f,"MeditationFocusGain",WisFocus);F(f,"MedicalTendQuality",WisTend);}
   if((m&32)!=0){F(o,"TradePriceImprovement",ChaTrade);F(f,"NegotiationAbility",ChaPersuade);F(f,"ConversionPower",ChaPersuade);}
  }

  // ---------- 信件：每人每项一次；第一次计算（旧存档读档、刚生成）只记基准不发信 ----------
  public static int Sync(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return 0;
   int ex=h.exaltedMask,my=h.mythicMask;
   if(!h.exaltedInit){h.exaltedInit=true;h.exaltedSent|=ex;h.mythicSent|=my;return 0;}
   if(p.Faction==null || !p.Faction.IsPlayer || p.Dead)return 0;
   int newEx=ex&~h.exaltedSent,newMy=my&~h.mythicSent,n=0;
   for(int a=0;a<6;a++){
    if((newEx&1<<a)!=0){Letter(p,"超凡·"+ExaltedNames[a],p.LabelShort+"的"+Adventurer.Names[a]+"达到了凡人的极限——超凡·"+ExaltedNames[a]+"。\n\n"+Describe(a,false));n++;}
    if((newMy&1<<a)!=0){Letter(p,"神话·"+MythicNames[a],p.LabelShort+"的"+Adventurer.Names[a]+"越过了凡人的极限，踏入神话——神话·"+MythicNames[a]+"。\n\n"+Describe(a,true));n++;}
   }
   h.exaltedSent|=ex;h.mythicSent|=my;return n;
  }
  static void Letter(Pawn p,string label,string text){Letters++;if(Adventurer.TestSuppressed)return;Find.LetterStack.ReceiveLetter(label,text,LetterDefOf.PositiveEvent,p.Spawned?new LookTargets(p):LookTargets.Invalid);}
  public static string Describe(int a,bool mythic){
   switch(a){
    case 0:return mythic?"撼山：负重 +25%、采矿 +20%；近战护甲穿透 +30%；近战命中 15% 让体型 ≤3 的目标踉跄 1.5 秒；近战打建筑 ×2；免疫踉跄和击倒类状态。":"巨人之力：负重 +25%、采矿速度 +20%；近战护甲穿透 +15%；近战命中 15% 让体型 ≤1.5 的目标踉跄 1.5 秒（移速 ×0.1、近战冷却 ×1.5）。";
    case 1:return mythic?"无影：在疾影之上，敌人射击的命中 ×0.75；每 10 秒受到的第一次近战或投射物伤害有 25% 完全落空。":"疾影：瞄准时间 −20%，近战闪避 +10；敌人射击他的命中 ×0.85；不会被哨兵借机攻击（已有“灵活”则改为移速 +0.2）。";
    case 2:return mythic?"金刚不坏：在不屈之躯之上，硬撑每天 2 次、每次 12 秒；失血封顶在极重之前；伤疤消除加快到每 2 天一处。":"不屈之躯：失血速度 −30%，疼痛休克阈值 +15%；食物中毒、流感、瘟疫、疟疾、睡眠病等可致死的疾病停在致死之前；硬撑：每天一次，本该倒地时撑 8 秒再倒；身上有伤疤或旧伤时，每 5 天消除最重的一处。";
    case 3:return mythic?"天才：在洞彻心智之上，全局学习速度 ×1.2；没有热情的技能按小热情学习；举一反三 50%。":"洞彻心智：读书、研读卷轴和典籍速度 +50%；技能不衰退；举一反三：技能升级时随机另一项技能得到这次升级所需经验的 25%。";
    case 4:return mythic?"先知：在明察之上，显形半径 15 格；免疫心灵震爆；敌对袭击到来时收到一封先知的预感。":"明察：冥想心灵专注 +30%，医疗品质 +10%；8 格内隐形或伪装的敌人显形；心灵震爆、夺心魔支配、魅惑等心灵效果持续时间减半。";
    default:return mythic?"君临：光环 15 格、心情 +6；他击倒或击杀敌人时，8 格内的敌方人形 15% 逃跑（首领、宿敌冠军、神力灌注 3 级以上、传奇怪物除外）；派系声望获得 ×1.5。":"威仪：交易价格 +5%，招募 / 说服 +15%；10 格内己方人形心情 +3、精神崩溃阈值 −3%；6 格内敌方人形命中 −8%。多个威仪不叠加。";
   }
  }
  // 分页悬停用：这一项的超凡 / 神话说明（没有就空串）。
  public static string Tip(Hediff_Adventurer h,int a){
   bool ex=(h.exaltedMask&1<<a)!=0,my=(h.mythicMask&1<<a)!=0;
   if(ex)return "\n\n"+(my?"<color=#C080FF>神话·"+MythicNames[a]+"</color>\n"+Describe(a,true):"<color=#E8C050>超凡·"+ExaltedNames[a]+"</color>\n"+Describe(a,false))+(my?"":"\n神话条件：属性 24，或 20 + 传奇冒险者 + "+Legend.Names[LegendPairs[a][0]]+"与"+Legend.Names[LegendPairs[a][1]]+"传奇点合计 20。");
   int raw=Raw(h);
   if((raw&1<<a)!=0){bool rm=(raw&1<<(a+6))!=0;return "\n\n<color=#909090>"+(rm?"神话·"+MythicNames[a]:"超凡·"+ExaltedNames[a])+"：已达成，效果未生效——"+(OffReason??"属性刚变化，稍后生效")+"</color>\n"+Describe(a,rm);}
   if(((h.exaltedSent|h.mythicSent)&1<<a)!=0)return "\n\n<color=#909090>超凡·"+ExaltedNames[a]+"：暂停（当前值低于 20）</color>";
   return "";
  }

  // ---------- 周期：携带者每 250 刻 ----------
  public static readonly HashSet<Pawn> chaCarriers=new HashSet<Pawn>();
  public static void Tick(Hediff_Adventurer h){
   var p=h.pawn;int raw=Raw(h),want=On?raw:0;
   if(want!=(h.exaltedMask|h.mythicMask<<6))h.Dirty(); // 漏掉 Dirty 的路径（上限变化等）在这里补
   var _=h.CurStage;int have=h.exaltedMask|h.mythicMask<<6;if(MaskOf(p)!=have)Store(p,have);Sync(h);SyncMark(h,raw);
   bool cha=(h.exaltedMask&32)!=0 && p.Spawned && !p.Dead;
   if(cha)chaCarriers.Add(p);else if(chaCarriers.Count>0)chaCarriers.Remove(p);
   if(h.exaltedMask==0 || p.Dead)return;
   if((h.exaltedMask&4)!=0)CapDiseases(h);
   if((h.exaltedMask&16)!=0){HalvePsychic(h);if(p.Spawned)Reveal(p,(h.mythicMask&16)!=0?MythicWisRadius:WisRadius);}
   if(cha && !p.Downed)Aura(p,(h.mythicMask&32)!=0);
  }
  // 热修1：健康页标记“超凡·xxx”（显示用，没有数值）。冒险者等级或超凡总开关关着、人死了时去掉；效果没生效（Isekai 共存）时标“未生效”。
  // 标记自己按 mask==0 移除（ShouldRemove），这里不在冒险者状态的 TickInterval 里直接删别的状态。
  static readonly DefRef<HediffDef> MarkDef=new DefRef<HediffDef>("LP_ExaltedMark");
  public static HediffDef MarkHediff=>MarkDef.Def;
  public static Hediff_ExaltedMark SyncMark(Hediff_Adventurer h,int raw){
   var p=h?.pawn;var d=MarkHediff;if(p?.health?.hediffSet==null || d==null)return null;
   var s=LichMod.Settings;if(s!=null && (!s.adventurer || !s.exaltedScores) || p.Dead)raw=0;
   var m=p.health.hediffSet.GetFirstHediffOfDef(d) as Hediff_ExaltedMark;bool on=On;
   if(raw==0){if(m!=null)m.mask=0;return null;}
   if(m==null){m=(Hediff_ExaltedMark)HediffMaker.MakeHediff(d,p);m.mask=raw;m.active=on;p.health.AddHediff(m);}
   else if(m.mask!=raw || m.active!=on){m.mask=raw;m.active=on;p.health.Notify_HediffChanged(m);}
   return m;
  }
  // 体：疾病停在致死前；神话失血停在极重前；去疤（HealScar）。
  public static int CapDiseases(Hediff_Adventurer h){
   var p=h.pawn;var list=p.health.hediffSet.hediffs;int n=0;bool mythic=(h.mythicMask&4)!=0;
   for(int i=0;i<list.Count;i++){
    var x=list[i];
    if(Diseases.Contains(x.def.defName) && x.def.lethalSeverity>0f){float cap=x.def.lethalSeverity-.02f;if(x.Severity>cap){x.Severity=cap;n++;}}
    else if(mythic && x.def==HediffDefOf.BloodLoss && x.Severity>BloodCap){x.Severity=BloodCap;n++;}
   }
   HealScar(h,mythic);
   return n;
  }
  // 1.91.0 去疤：身上有伤疤 / 旧伤时计时，不屈之躯满 5 天、金刚不坏满 2 天消除最重的一处（意识形态的仪式疤痕不动）；没有伤疤时计时归零，新疤从头算。
  public static Hediff_Injury HealScar(Hediff_Adventurer h,bool mythic){
   var p=h.pawn;int now=GenTicks.TicksGame;Hediff_Injury scar=null;
   foreach(var x in p.health.hediffSet.hediffs)if(x is Hediff_Injury i && i.IsPermanent() && i.def.defName!="Scarification" && (scar==null || i.Severity>scar.Severity))scar=i;
   if(scar==null || h.scarTick==int.MinValue || now<h.scarTick){h.scarTick=now;return null;}
   if(now-h.scarTick<(mythic?MythicScarTicks:ScarTicks))return null;
   p.health.RemoveHediff(scar);h.scarTick=now;Items.Mote(p,mythic?"金刚不坏":"不屈之躯",new Color(.95f,.85f,.45f));
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"的"+(mythic?"金刚不坏":"不屈之躯")+"消除了"+(scar.Part!=null?scar.Part.LabelCap+"上的":"")+scar.LabelCap+"。",p,MessageTypeDefOf.PositiveEvent,false);
   return scar;
  }
  // 感：外力造成的控制类精神状态剩余时间减半（每个状态一次）。
  static readonly AccessTools.FieldRef<MentalState,int> Age=AccessTools.FieldRefAccess<MentalState,int>("age");
  public static bool HalvePsychic(Hediff_Adventurer h){
   var ms=h.pawn.MentalState;if(ms==null || ms.causedByMood || ms==h.halvedState || !HeroCombat.ControlState(ms.def) && !ms.causedByPsycast)return false; // 2026-10-28：原版 / VPE 灵能失控（如仓皇逃窜）也减半
   h.halvedState=ms;int age=Age(ms);
   if(ms.forceRecoverAfterTicks>0)ms.forceRecoverAfterTicks=age+Mathf.Max(1,(ms.forceRecoverAfterTicks-age)/2);
   else if(ms.def.maxTicksBeforeRecovery>0 && ms.def.maxTicksBeforeRecovery<99999)ms.forceRecoverAfterTicks=age+Mathf.Max(1,(ms.def.maxTicksBeforeRecovery-age)/2);
   return true;
  }
  public static float PsychicDuration(Pawn p)=>Has(p,Adventurer.Wis)?PsychicHalf:1f;
  public static bool BlastImmune(Pawn p){if(!Mythic(p,Adventurer.Wis))return false;Items.Mote(p,"先知",new Color(.75f,.6f,1f));return true;}
  // 感：半径内隐形（原版隐身组件）或伪装（1.54 魅魔）的敌人显形。
  // 原版的“被打断后显形”窗口可能短于 250 刻：另记“显形到哪一刻”，由 IsPsychologicallyInvisible 后缀读取（同 1.28 正义之神的做法）。
  internal static readonly Dictionary<Pawn,int> revealed=new Dictionary<Pawn,int>();
  public static bool Revealed(Pawn p)=>revealed.TryGetValue(p,out int until) && GenTicks.TicksGame<until;
  public static int Reveal(Pawn seer,float radius){
   var map=seer.Map;if(map==null)return 0;int n=0,now=GenTicks.TicksGame;float r2=radius*radius;
   if(revealed.Count>0)foreach(var k in revealed.Where(kv=>kv.Value<=now || kv.Key.Destroyed).Select(kv=>kv.Key).ToList())revealed.Remove(k);
   foreach(var o in map.mapPawns.AllPawnsSpawned){
    if(o==seer || o.Dead || (o.Position-seer.Position).LengthHorizontalSquared>r2)continue;
    if(o.HostileTo(seer)){
     var hs=o.health.hediffSet.hediffs;
     for(int i=0;i<hs.Count;i++){var inv=(hs[i] as HediffWithComps)?.GetComp<HediffComp_Invisibility>();if(inv==null)continue;inv.DisruptInvisibility();if(!revealed.ContainsKey(o))n++;revealed[o]=now+AuraInterval+60;break;}
    }
    if(AbyssRaces.IsSuccubus(o)){
     if(Outsiders.Disguised(o)){AbyssEvents.RevealSuccubus(o,seer.LabelShort+"的明察看穿了它的伪装");n++;}
     else if(AbyssRaces.Guised(o)){AbyssRaces.Guise(o).Set(false);Messages.Message(seer.LabelShort+"看穿了"+o.LabelShort+"的变形伪装！",o,MessageTypeDefOf.NeutralEvent,false);n++;}
    }
   }
   return n;
  }
  // 魅：10 / 15 格内己方人形得到“威仪”（不叠加，取最高）。正在“鼓舞”（鼓舞领袖的护盾）的人只拿心情、不再降低崩溃阈值（两个领袖效果取一次）。
  static readonly DefRef<HediffDef> PresenceDef=new DefRef<HediffDef>("LP_ExaltedPresence"),InspiredDef=new DefRef<HediffDef>("LP_FeatInspired"),StaggerDef=new DefRef<HediffDef>("LP_ExaltedStagger");
  public static HediffDef Presence=>PresenceDef.Def;public static HediffDef Stagger=>StaggerDef.Def;
  public static int Aura(Pawn leader,bool mythic){
   var d=Presence;var map=leader.Map;if(d==null || map==null || leader.Faction==null)return 0;float r=mythic?MythicChaRadius:ChaRadius,r2=r*r;int n=0;
   foreach(var o in map.mapPawns.SpawnedPawnsInFaction(leader.Faction)){
    if(o==leader || o.Dead || !o.RaceProps.Humanlike || o.needs?.mood==null || (o.Position-leader.Position).LengthHorizontalSquared>r2)continue;
    bool inspired=InspiredDef.Def!=null && o.health.hediffSet.HasHediff(InspiredDef.Def);
    float sev=(mythic?2f:1f)-(inspired?.5f:0f);
    var h=o.health.hediffSet.GetFirstHediffOfDef(d);
    if(h==null){h=HediffMaker.MakeHediff(d,o);h.Severity=sev;o.health.AddHediff(h);}
    else{var dis=h.TryGetComp<HediffComp_Disappears>();bool fresh=dis!=null && dis.ticksToDisappear>AuraInterval+100;h.Severity=fresh?Mathf.Max(h.Severity,sev):sev;} // 本轮已被别的携带者刷新过：取较高的一档，不叠加
    h.TryGetComp<HediffComp_Disappears>()?.SetDuration(AuraInterval*2+60);n++;
   }
   return n;
  }
  // 魅：6 格内有威仪携带者（和攻击者敌对）时攻击者命中 −8%（多个不叠加）。
  public static bool Cowed(Pawn attacker){
   if(chaCarriers.Count==0 || attacker==null || !attacker.Spawned || attacker.RaceProps?.Humanlike!=true)return false;
   float r2=ChaEnemyRadius*ChaEnemyRadius;
   foreach(var c in chaCarriers)if(c.Map==attacker.Map && c.Spawned && !c.Dead && (c.Position-attacker.Position).LengthHorizontalSquared<=r2 && c.HostileTo(attacker))return true;
   return false;
  }
  // 魅·君临：击倒 / 击杀敌人时 8 格内敌方人形 15% 逃跑。
  public static bool RoutImmune(Pawn o){
   if(Legendary.Is(o) || AdventurerEnemies.IsLeader(o) || Twilight.Vessel(o)!=null)return true;
   var h=Adventurer.Get(o);if(h!=null && (h.nemesis || h.storyLevel))return true;
   var inf=DefDatabase<TraitDef>.GetNamedSilentFail("LP_DivineInfusion");var t=inf==null?null:o.story?.traits?.GetTrait(inf);
   return t!=null && t.Degree>=3;
  }
  public static int Rout(Pawn king,IntVec3 at,Map map){
   if(map==null || king==null)return 0;int n=0;float r2=RoutRadius*RoutRadius;
   foreach(var o in map.mapPawns.AllPawnsSpawned.ToList()){
    if(o.Dead || o.Downed || !o.RaceProps.Humanlike || !o.HostileTo(king) || (o.Position-at).LengthHorizontalSquared>r2 || o.InMentalState || RoutImmune(o) || Roll>=RoutChance)continue;
    if(o.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.PanicFlee,"君临",forced:true,forceWake:true)==true){Items.Mote(o,"畏惧君临",new Color(.85f,.7f,1f));n++;}
   }
   return n;
  }
  public static void Defeated(Pawn victim,Pawn by){if(by==null || victim==null || by==victim || !Mythic(by,Adventurer.Cha) || !victim.HostileTo(by))return;Rout(by,victim.PositionHeld,victim.MapHeld);}
  // 派系声望（1.58.3）：殖民地有君临时获得 ×1.5。
  public static int Reputation(int n){
   if(n<=0 || count==0)return n;
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)if(Mythic(p,Adventurer.Cha))return Mathf.RoundToInt(n*RepFactor);
   return n;
  }

  // ---------- 力 ----------
  static AccessTools.StructFieldRef<DamageInfo,float> pen;
  public static void Penetrate(ref DamageInfo d,Pawn a){
   if(!Has(a,Adventurer.Str))return;
   if(pen==null)pen=AccessTools.StructFieldRefAccess<DamageInfo,float>("armorPenetrationInt");
   pen(ref d)+=Mythic(a,Adventurer.Str)?MythicPen:StrPen;
  }
  public static bool StaggerImmune(Pawn p)=>Mythic(p,Adventurer.Str);
  public static bool TryStagger(Pawn a,Pawn v){
   if(v==null || v.Dead || !Has(a,Adventurer.Str) || Stagger==null)return false;
   float size=Mythic(a,Adventurer.Str)?MythicStaggerSize:StaggerSize;
   if(v.BodySize>size || StaggerImmune(v) || Roll>=StaggerChance)return false;
   var old=v.health.hediffSet.GetFirstHediffOfDef(Stagger);if(old!=null)v.health.RemoveHediff(old);
   v.health.AddHediff(Stagger);Items.Mote(v,"踉跄",new Color(.9f,.8f,.6f));return true;
  }
  // ---------- 敏 ----------
  public static float ShotAgainst(Pawn target,Pawn shooter){
   int m=MaskOf(target);if((m&2)==0 || shooter==null || !shooter.HostileTo(target))return 1f;
   return (m&(2<<6))!=0?MythicShot:DexShot;
  }
  // 神话·无影：每 600 刻里第一次受到的近战或投射物伤害掷一次，25% 落空。时间戳不存档。
  internal static readonly Dictionary<int,int> evadeTick=new Dictionary<int,int>();
  public static bool Evade(Pawn v,DamageInfo d){
   if(!Mythic(v,Adventurer.Dex) || !(HeroCombat.Melee(d) || d.Def!=null && d.Def.isRanged && d.Weapon!=null))return false;
   int now=GenTicks.TicksGame,id=v.thingIDNumber;
   if(evadeTick.TryGetValue(id,out int last) && now>=last && now-last<EvadeWindow)return false;
   evadeTick[id]=now;
   if(Roll>=EvadeChance)return false;
   Items.Mote(v,"落空",new Color(.8f,.9f,1f));return true;
  }
  public static bool ImmuneToSentinel(Pawn p)=>Has(p,Adventurer.Dex);
  // ---------- 体：硬撑 ----------
  [ThreadStatic] public static bool rechecking;
  public static bool Hold(Pawn p){
   var h=Adventurer.Get(p);if(h==null || (h.exaltedMask&4)==0)return false;
   int now=GenTicks.TicksGame;
   if(h.holdUntil>0 && now<h.holdUntil)return true;
   if(rechecking)return false;
   bool mythic=(h.mythicMask&4)!=0;
   if(h.holdStart==int.MinValue || now-h.holdStart>=HoldPeriod || now<h.holdStart){h.holdStart=now;h.holdUses=0;}
   if(h.holdUses>=(mythic?2:1))return false;
   h.holdUses++;h.holdUntil=now+(mythic?MythicHoldTicks:HoldTicks);Items.Mote(p,"硬撑！",new Color(1f,.6f,.4f));return true;
  }
  public static void HoldExpired(Hediff_Adventurer h){
   h.holdUntil=0;var p=h.pawn;if(p==null || p.Dead || p.Downed)return;
   rechecking=true;try{p.health.CheckForStateChange(null,null);}finally{rechecking=false;}
  }
  // ---------- 智 ----------
  public static float ReadFactor(Pawn p)=>Has(p,Adventurer.Int)?IntRead:1f;
  public static bool NoDecay(Pawn p)=>Has(p,Adventurer.Int);
  [ThreadStatic] static bool echoing;
  public static SkillRecord Echo(Pawn p,SkillRecord from,int oldLevel){
   if(echoing || p?.skills==null || from==null)return null;
   float share=Mythic(p,Adventurer.Int)?MythicEcho:IntEcho;float xp=SkillRecord.XpRequiredToLevelUpFrom(oldLevel)*share;
   var pick=p.skills.skills.Where(s=>s!=from && !s.TotallyDisabled).ToList();if(pick.Count==0 || xp<=0f)return null;
   var to=pick.RandomElement();Echoes++;echoing=true;try{to.Learn(xp,true,true);}finally{echoing=false;}
   return to;
  }
  public static bool Echoing=>echoing;public static int Echoes; // 自检计数
 }

 // ==================== Harmony ====================
 // 伤害（受害者）：无影落空；（攻击者）巨人之力 / 撼山的近战穿透。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class ExaltedDamage {
  [HarmonyPriority(Priority.High)]
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(Exalted.Count==0 || dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return true;
   if(Exalted.Evade(__instance,dinfo)){absorbed=true;return false;}
   if(dinfo.Instigator is Pawn a && a!=__instance && HeroCombat.Melee(dinfo))Exalted.Penetrate(ref dinfo,a);
   return true;
  }
 }
 // 明察 / 先知：显形中的敌人不再算隐身。
 [HarmonyPatch(typeof(InvisibilityUtility),nameof(InvisibilityUtility.IsPsychologicallyInvisible))]
 public static class ExaltedReveal {static void Postfix(Pawn pawn,ref bool __result){if(__result && Exalted.revealed.Count>0 && Exalted.Revealed(pawn))__result=false;}}
 // 撼山：近战打建筑 ×2。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class ExaltedBuilding {
  static void Prefix(Thing __instance,ref DamageInfo dinfo){
   if(Exalted.Count==0 || !(__instance is Building) || !(dinfo.Instigator is Pawn a) || !Exalted.Mythic(a,Adventurer.Str) || !HeroCombat.Melee(dinfo))return;
   dinfo.SetAmount(dinfo.Amount*Exalted.MythicBuilding);
  }
  // 命中后：巨人之力的踉跄。
  static void Postfix(Thing __instance,DamageInfo dinfo,DamageWorker.DamageResult __result){
   if(Exalted.Count==0 || !(__instance is Pawn v) || !(dinfo.Instigator is Pawn a) || a==v || __result==null || __result.totalDamageDealt<=0f || !HeroCombat.Melee(dinfo))return;
   if(a.HostileTo(v))Exalted.TryStagger(a,v);
  }
 }
 // 撼山免疫踉跄和扑倒类状态。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.AddHediff),new[]{typeof(Hediff),typeof(BodyPartRecord),typeof(DamageInfo?),typeof(DamageWorker.DamageResult)})]
 public static class ExaltedHediffAdded {
  static bool Prefix(Hediff hediff,Pawn ___pawn){
   if(Exalted.Count==0 || hediff==null)return true;
   if(Exalted.StaggerImmune(___pawn) && (hediff.def==Exalted.Stagger || hediff.def.defName.IndexOf("Knock",StringComparison.Ordinal)>=0)){Items.Mote(___pawn,"撼山",new Color(.95f,.85f,.45f));return false;}
   return true;
  }
  // 明察：心灵类状态持续时间减半。
  static void Postfix(Hediff hediff,Pawn ___pawn){
   if(Exalted.Count==0 || hediff==null || !Exalted.PsychicHediffs.Contains(hediff.def.defName) || !Exalted.Has(___pawn,Adventurer.Wis))return;
   var d=hediff.TryGetComp<HediffComp_Disappears>();if(d!=null && d.ticksToDisappear>1)d.ticksToDisappear=Mathf.Max(1,Mathf.RoundToInt(d.ticksToDisappear*Exalted.PsychicHalf));
  }
 }
 // 疾影 / 无影：敌人射他的命中；威仪：6 格内的敌方人形射击 ×0.92。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class ExaltedShot {
  static readonly AccessTools.StructFieldRef<ShotReport,float> Shooter=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromShooterAndDist");
  static void Postfix(Thing caster,Verb verb,LocalTargetInfo target,ref ShotReport __result){
   if(Exalted.Count==0 || !(caster is Pawn p) || verb==null || verb.IsMeleeAttack)return;
   float f=target.Thing is Pawn t?Exalted.ShotAgainst(t,p):1f;
   if(Exalted.Cowed(p))f*=1f-Exalted.ChaEnemyHit;
   if(f!=1f)Shooter(ref __result)*=f;
  }
 }
 // 威仪：近战命中 −8%（加在最终命中率上，幸运重掷之前）。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class ExaltedMeleeHit {
  [HarmonyPriority(Priority.HigherThanNormal)]
  static void Postfix(Verb_MeleeAttack __instance,ref float __result){
   if(Exalted.chaCarriers.Count==0)return;
   if(Exalted.Cowed(__instance.CasterPawn))__result=Mathf.Clamp01(__result-Exalted.ChaEnemyHit);
  }
 }
 // 硬撑：本该倒地时延后。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.ShouldBeDowned))]
 public static class ExaltedHold {
  static void Postfix(Pawn ___pawn,ref bool __result){
   if(!__result || Exalted.Count==0 || ___pawn==null || ___pawn.Dead || ___pawn.Downed)return;
   if(Exalted.Hold(___pawn))__result=false;
  }
 }
 // 洞彻心智：技能不衰退。
 [HarmonyPatch(typeof(SkillRecord),nameof(SkillRecord.Interval))]
 public static class ExaltedNoDecay {
  static bool Prefix(Pawn ___pawn)=>Exalted.Count==0 || !Exalted.NoDecay(___pawn);
 }
 // 天才：没有热情的技能按小热情学习；举一反三：升级那一刻随机另一项技能得到这次升级所需经验的 10% / 20%。
 [HarmonyPatch(typeof(SkillRecord),nameof(SkillRecord.Learn))]
 public static class ExaltedLearn {
  // 1.63.0：__state 存“等级 + 1”，0 = 不举一反三。知识亡灵的学习前缀（Priority.First）返回 false 时 Harmony 会跳过这个前缀，
  // __state 保持默认 0；原来 0 被当成“学习前 0 级”，满级的知识亡灵每次学习都会对 0～19 级各举一反三一次，经验连锁传给主人。
  static void Prefix(SkillRecord __instance,Pawn ___pawn,ref float xp,bool direct,bool ignoreLearnRate,out int __state){
   __state=0;if(Exalted.Count==0 || xp<=0f || Exalted.Echoing)return;
   int m=Exalted.MaskOf(___pawn);if((m&8)==0)return;
   __state=__instance.levelInt+1;
   if(!direct && !ignoreLearnRate && (m&8<<6)!=0 && __instance.passion==Passion.None){
    float none=__instance.LearnRateFactor(false);__instance.passion=Passion.Minor;float minor=__instance.LearnRateFactor(false);__instance.passion=Passion.None;
    if(none>0f)xp*=minor/none;
   }
  }
  static void Postfix(SkillRecord __instance,Pawn ___pawn,int __state){
   int from=__state-1;if(from<0 || __instance.levelInt<=from)return;
   for(int l=from;l<__instance.levelInt;l++)Exalted.Echo(___pawn,__instance,l);
  }
 }
 // 君临：击倒、击杀。
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class ExaltedDowned {
  static void Postfix(Pawn ___pawn,DamageInfo? dinfo){if(Exalted.chaCarriers.Count>0 && dinfo?.Instigator is Pawn a)Exalted.Defeated(___pawn,a);}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class ExaltedKill {
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out Pawn __state){__state=Exalted.chaCarriers.Count>0 && !__instance.Dead && !__instance.Downed?dinfo?.Instigator as Pawn:null;}
  static void Postfix(Pawn __instance,Pawn __state){if(__state!=null && __instance.Dead)Exalted.Defeated(__instance,__state);}
 }
 // 先知：敌对袭击到来时一封预感信（列出来者）。原版袭击不能安全地延后 1 小时（任务和事件会立刻读袭击者），只发信。
 [HarmonyPatch(typeof(IncidentWorker_Raid),"TryExecuteWorker")]
 public static class ExaltedProphet {
  static void Prefix(IncidentParms parms,out HashSet<Lord> __state){__state=Exalted.Count>0 && parms?.target is Map map && map.IsPlayerHome && Seer(map)!=null?new HashSet<Lord>(map.lordManager.lords):null;}
  static void Postfix(IncidentParms parms,bool __result,HashSet<Lord> __state){
   if(!__result || __state==null || !(parms.target is Map map))return;var seer=Seer(map);if(seer==null)return;
   var raiders=map.lordManager.lords.Where(l=>!__state.Contains(l) && l.faction!=null && l.faction.HostileTo(Faction.OfPlayer)).SelectMany(l=>l.ownedPawns).ToList();
   if(raiders.Count==0)return;Exalted.Letters++;if(Adventurer.TestSuppressed)return;
   var sb=new StringBuilder();sb.AppendLine(seer.LabelShort+"在它们出现之前就看见了："+(parms.faction?.Name??"敌人")+"，共 "+raiders.Count+" 人。");
   var lead=raiders.Where(r=>AdventurerEnemies.IsLeader(r) || Adventurer.Get(r)?.storyLevel==true).ToList();if(lead.Count>0)sb.AppendLine("领头的："+string.Join("、",lead.Select(r=>r.LabelShort)));
   int top=raiders.Select(r=>Adventurer.Get(r)?.level??0).DefaultIfEmpty(0).Max();if(top>0)sb.AppendLine("最高冒险者等级："+top);
   int ex=raiders.Count(r=>Exalted.MaskOf(r)!=0);if(ex>0)sb.AppendLine("其中 "+ex+" 人带着超凡属性。");
   Find.LetterStack.ReceiveLetter("先知的预感",sb.ToString().TrimEnd(),LetterDefOf.ThreatSmall,new LookTargets(raiders));
  }
  static Pawn Seer(Map map){foreach(var p in map.mapPawns.FreeColonistsSpawned)if(Exalted.Mythic(p,Adventurer.Wis))return p;return null;}
 }

 // ---------- 热修1：健康页标记 ----------
 // 一个状态列出全部达标项：“超凡·巨人之力、神话·无影”。没有任何数值（没有阶段、不改属性、不算疾病），由冒险者状态每 250 刻同步（Exalted.SyncMark）。
 public class Hediff_ExaltedMark : Hediff {
  public int mask;public bool active=true;
  static readonly Color Gold=new Color(.91f,.75f,.31f),Purple=new Color(.78f,.55f,1f),Grey=new Color(.6f,.6f,.6f);
  public override bool ShouldRemove=>mask==0;
  public override string LabelBase{get{
   var parts=new List<string>();
   for(int a=0;a<6;a++){if((mask&1<<a)==0)continue;parts.Add((mask&1<<(a+6))!=0?"神话·"+Exalted.MythicNames[a]:"超凡·"+Exalted.ExaltedNames[a]);}
   return parts.Count==0?def.label:string.Join("、",parts);
  }}
  public override string LabelInBrackets=>active?null:"未生效";
  public override Color LabelColor=>!active?Grey:(mask>>6&63)!=0?Purple:Gold;
  public override string TipStringExtra{get{
   var sb=new StringBuilder();
   if(!active)sb.AppendLine("效果未生效："+(Exalted.OffReason??"属性刚变化，稍后生效")).AppendLine();
   for(int a=0;a<6;a++){if((mask&1<<a)==0)continue;bool my=(mask&1<<(a+6))!=0;sb.AppendLine(Adventurer.Names[a]+"："+Exalted.Describe(a,my));}
   return sb.ToString().TrimEnd();
  }}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref mask,"lpMask");Scribe_Values.Look(ref active,"lpActive",true);}
 }

 // ---------- 设置（画在“冒险者”分组里） ----------
 // 热修1：“共存模式下仍生效”默认改为开（超凡大多是规则效果，和 Isekai 的数值不叠加）。exaltedVersion 是设置版本：旧设置文件（没有这个键）读入时迁移一次改成开，
 // 之后玩家自己关掉会连同版本号一起存下，不再被改。
 public partial class Settings {
  public const int ExaltedVersion=1;
  public bool exaltedScores=true,exaltedCoexist=true;public int exaltedVersion=ExaltedVersion;
  void ExposeExalted(){
   Scribe_Values.Look(ref exaltedScores,"exaltedScores",true);Scribe_Values.Look(ref exaltedCoexist,"exaltedCoexist",true);Scribe_Values.Look(ref exaltedVersion,"exaltedVersion",0);
   if(Scribe.mode==LoadSaveMode.LoadingVars)MigrateExalted();
  }
  public bool MigrateExalted(){if(exaltedVersion>=ExaltedVersion)return false;exaltedCoexist=true;exaltedVersion=ExaltedVersion;return true;}
 }
 public static class ExaltedSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.CheckboxLabeled("超凡属性特性",ref s.exaltedScores,"六维的有效值达到 20 时获得该项的超凡特性（巨人之力、疾影、不屈之躯、洞彻心智、明察、威仪）；24，或 20 + 传奇冒险者 + 对应两项传奇属性点合计 20 时升为神话。降到 20 以下暂停，回升恢复。获得时发一封信。");
   var old=GUI.color;if(!Adventurer.IsekaiActive)GUI.color=Color.gray;
   l.CheckboxLabeled("共存模式下超凡特性仍生效"+(Adventurer.IsekaiActive?"":"（未检测到 Isekai）"),ref s.exaltedCoexist,"默认开：Isekai 共存模式下六维的数值效果交给 Isekai，超凡特性大多是规则效果（踉跄、硬撑、显形、光环等），和 Isekai 的数值不叠加，照常生效。取消勾选则共存模式下超凡也一起关闭（分页和健康页仍显示，灰色）。");
   GUI.color=old;
  }
 }
}
