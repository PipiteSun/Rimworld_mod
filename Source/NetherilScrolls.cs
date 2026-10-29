using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.37.0 耐瑟瑞尔卷轴：研习卷、秘术残卷、卡苏斯之卷都是原版书（Book + CompProperties_Book），效果写在自定义 BookOutcomeDoer 里。
 // 1.52.0 补丁：不再按整小时结算，改成像冥想一样连续累积——阅读 job 每 60 刻结算一次（每小时量 × 经过刻数 ÷ 2500），每小时总量不变；空闲殖民者会自动研读（NetherilIdle）。
 // 读的条件（心灵链接、智识 6）挂在 BookUtility.CanReadBook 上，右键菜单和自动读书都走这里。任务线在 NetherilQuest.cs，升神仪式在 NetherilAscension.cs。
 public static class Netheril {
  public const string StudyDef="LP_NetherilStudyScroll",ArcaneDef="LP_NetherilArcaneScroll",KarsusDef="LP_KarsusScroll";
  public const int HourTicks=GenDate.TicksPerHour,MinIntellect=6,KarsusPassHours=40,MaxEcho=10,AscensionPasses=7;
  // 下面的每小时量 ÷ HourTicks 就是每刻速率；结算间隔 SettleTicks。读错：每累计读满 1 小时掷一次，读错时额外加这一小时 MisreadExtra 倍的心灵熵。
  public const int SettleTicks=60,KarsusPassTicks=KarsusPassHours*HourTicks;
  public const float StudyEntropy=8f,StudyXP=6f,ArcaneEntropy=4f,ArcaneProgress=1f,ArcaneResidueXP=2f,KarsusEntropy=6f,ArcaneGoal=24f,StopAt=.9f,MisreadExtra=2f,LichEntropy=.5f,NecroLich=1.5f,CrossSchool=.1f;
  public static float Hours(int ticks)=>ticks/(float)HourTicks;
  public const int MaxSpellsPerScroll=2;
  public static readonly int[] StudyThresholds={100,200,400,800,1600};
  public const int StudyMaxPsylink=6;
  public static readonly string[] Schools={"Evocation","Abjuration","Enchantment","Illusion","Divination","Conjuration","Transmutation","Necromancy"};
  public static string SchoolLabel(string s){switch(s){case "Evocation":return "塑能";case "Abjuration":return "防护";case "Enchantment":return "惑控";case "Illusion":return "幻术";case "Divination":return "预言";case "Conjuration":return "咒法";case "Transmutation":return "变化";case "Necromancy":return "死灵";default:return "未知";}}
  public static Color SchoolColor(string s){switch(s){case "Evocation":return new Color(.80f,.34f,.22f);case "Abjuration":return new Color(.36f,.56f,.80f);case "Enchantment":return new Color(.78f,.46f,.70f);case "Illusion":return new Color(.56f,.42f,.80f);case "Divination":return new Color(.86f,.76f,.40f);case "Conjuration":return new Color(.36f,.66f,.62f);case "Transmutation":return new Color(.50f,.68f,.34f);case "Necromancy":return new Color(.30f,.30f,.34f);default:return Color.white;}}
  // 测试用：设了就代替随机数（读错、跨学派、学派、妄念）。
  public static float? TestRoll;
  public static float Roll=>TestRoll??Rand.Value;
  public static NetherilQuestState State=>GameComp<NetherilQuestState>.Get(); // 1.58.6：缓存
  // 品质速度倍率：糟糕 ×0.6 → 传说 ×1.6（研习卷经验、残卷进度和余效都乘它；心灵熵和卡苏斯之卷不受品质影响）。
  public static float QualityFactor(QualityCategory q){switch(q){case QualityCategory.Awful:return .6f;case QualityCategory.Poor:return .8f;case QualityCategory.Normal:return 1f;case QualityCategory.Good:return 1.15f;case QualityCategory.Excellent:return 1.3f;case QualityCategory.Masterwork:return 1.45f;default:return 1.6f;}}
  public static bool IsNetheril(ThingDef d)=>d!=null && (d.defName==StudyDef || d.defName==ArcaneDef || d.defName==KarsusDef);
  public static int Intellect(Pawn p){var s=p?.skills?.GetSkill(SkillDefOf.Intellectual);return s==null || s.TotallyDisabled?0:s.Level;}
  public static bool HasPsylink(Pawn p)=>p!=null && p.GetPsylinkLevel()>0;
  public static string CannotRead(Pawn p){
   if(p==null)return "没有读者";
   if(!HasPsylink(p))return "需要心灵链接";
   int i=Intellect(p);if(i<MinIntellect)return "智识需要 "+MinIntellect+" 级（现在 "+i+" 级）";
   return null;
  }
  // 这个人能不能读这件东西（不是耐瑟卷轴一律 true）。所有挑书入口都用它：原版 CanReadBook / IsValidBook 补丁、我们的冥想 JoyGiver、自动研读。
  public static bool CanRead(Pawn p,Thing t)=>t==null || !IsNetheril(t.def) || CannotRead(p)==null;
  public static float MisreadChance(Pawn p)=>Mathf.Max(0f,(8-Intellect(p))*.05f);
  // 照原版过载规则：心灵熵可以超过上限（scale=false：数值按需求精确）。
  public static void AddEntropy(Pawn p,float v){if(v>0f && p?.psychicEntropy!=null)p.psychicEntropy.TryAddEntropy(v,null,false,true);}
  // 心灵熵超过上限 90% 时结束阅读；玩家右键指定的阅读（playerForced）不停。
  public static bool ShouldStopHot(Pawn p){
   var e=p?.psychicEntropy;if(e==null || e.MaxEntropy<=0f || e.EntropyValue<=e.MaxEntropy*StopAt)return false;
   var job=p.CurJob;return job!=null && job.def==JobDefOf.Reading && !job.playerForced;
  }
  public static void HotMessage(Pawn p){if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"的心灵熵太高，放下了耐瑟瑞尔卷轴。（右键强制研读不会自动停下）",p,MessageTypeDefOf.NeutralEvent,false);}
  // 立刻停（检查和没有娱乐需求的读者用）；阅读 job 里的结算走 NetherilIdle.RequestStop，在原版娱乐判定那一步停。
  public static bool StopIfHot(Pawn p){
   if(!ShouldStopHot(p))return false;
   p.jobs.EndCurrentJob(JobCondition.InterruptForced);HotMessage(p);
   return true;
  }
  // 经验：有 VPE 直接进 VPE 灵能经验；没有 VPE 记“耐瑟研习度”。1.52.0 补丁起没有每日上限。
  public static float GiveXP(Pawn p,float want){
   if(p==null || want<=0f)return 0f;
   want*=IdeoBridge.ScrollXpFactor(p); // 1.56.0 魔网至上 ×1.15
   if(NetherilHooks.VPEActive)NetherilHooks.VPEGainXP(p,want);else State?.AddStudyPoints(p,want);
   return want;
  }
  public static Thing Make(string def,QualityCategory? q=null,string school=null){
   var d=Items.Thing(def);if(d==null)return null;
   var t=ThingMaker.MakeThing(d);
   if(q.HasValue)t.TryGetComp<CompQuality>()?.SetQuality(q.Value,ArtGenerationContext.Outsider);
   if(school!=null && t is Book b)b.BookComp?.Doers.OfType<BookOutcome_NetherilArcane>().FirstOrDefault()?.SetSchool(school);
   return t;
  }
  public static Thing MakeStudy(QualityCategory? q=null)=>Make(StudyDef,q);
  public static Thing MakeArcane(string school=null,QualityCategory? q=null)=>Make(ArcaneDef,q,school);
  public static T Doer<T>(Thing t) where T:class=>(t as Book)?.BookComp?.Doers.OfType<T>().FirstOrDefault();
 }

 // VPE 桥接往这里登记（Bridges/VPE/Source/NetherilVPE.cs）；主程序集不引用 VPE。
 public static class NetherilHooks {
  public static Func<Pawn,int> VPELevel;
  public static Action<Pawn,float> VPEGainXP;
  public static Func<Pawn,string,List<string>> VPECandidates;
  public static Func<Pawn,string,string> VPELearn;
  public static bool VPEActive=>VPEGainXP!=null;
  public static int Level(Pawn p)=>VPELevel?.Invoke(p)??0;
 }

 // 法术池：有 VPE 时按 VPE 路径归学派（桥接里），没有 VPE 时用原版心灵法术（按 defName 归类，表外归变化）。
 public static class NetherilSpells {
  public static readonly Dictionary<string,string> Vanilla=new Dictionary<string,string>{
   {"Flashstorm","Evocation"},{"SolarPinhole","Evocation"},
   {"BulletShield","Abjuration"},{"EntropyDump","Abjuration"},{"EntropyLink","Abjuration"},
   {"Stun","Enchantment"},{"Berserk","Enchantment"},{"BerserkPulse","Enchantment"},{"ManhunterPulse","Enchantment"},{"VertigoPulse","Enchantment"},{"Burden","Enchantment"},{"Beckon","Enchantment"},
   {"Invisibility","Illusion"},{"BlindingPulse","Illusion"},{"Smokepop","Illusion"},
   {"Focus","Divination"},{"Farskip","Divination"},
   {"Skip","Conjuration"},{"Chunkskip","Conjuration"},{"Waterskip","Conjuration"},{"ChaosSkip","Conjuration"},{"MassChaosSkip","Conjuration"},{"Wallraise","Conjuration"},
   {"Painblock","Necromancy"},{"Neuroquake","Necromancy"}};
  public static string VanillaSchool(AbilityDef d){if(d==null)return null;if(Vanilla.TryGetValue(d.defName,out var s))return s;if(d.defName.StartsWith("WordOf") || d.defName=="Speech")return "Enchantment";return "Transmutation";}
  public static int PlayerLevel(Pawn p)=>NetherilHooks.VPEActive?NetherilHooks.Level(p):p?.GetPsylinkLevel()??0;
  public static List<string> Candidates(Pawn p,string school){
   if(p==null || school==null)return new List<string>();
   if(NetherilHooks.VPEActive)return NetherilHooks.VPECandidates?.Invoke(p,school)??new List<string>();
   int lvl=p.GetPsylinkLevel();
   return DefDatabase<AbilityDef>.AllDefsListForReading.Where(d=>d.IsPsycast && d.level>=1 && d.level<=lvl && VanillaSchool(d)==school && p.abilities?.GetAbility(d)==null).Select(d=>d.defName).ToList();
  }
  public static string Learn(Pawn p,string name){
   if(NetherilHooks.VPEActive)return NetherilHooks.VPELearn?.Invoke(p,name);
   var d=DefDatabase<AbilityDef>.GetNamedSilentFail(name);if(d==null || p?.abilities==null)return null;
   p.abilities.GainAbility(d);return d.LabelCap.Resolve();
  }
 }

 public class BookOutcomeProperties_Netheril : BookOutcomeProperties {public override Type DoerClass=>doerClass;}

 // 公共部分：用游戏刻差值计时（不依赖原版每次传进来的系数），按读者累计，满 60 刻结算一次：每小时量 × 经过刻数 ÷ 2500。
 public abstract class BookOutcome_NetherilBase : BookOutcomeDoer {
  readonly Dictionary<int,int> lastTick=new Dictionary<int,int>(),pend=new Dictionary<int,int>();
  // 1.42.0：这次连续阅读的刻数（不存档），阅读 job 结束时由 NetherilInsightOnReadEnd 取走，满 1 小时给耐瑟启迪。
  public readonly Dictionary<int,int> session=new Dictionary<int,int>();
  // 这次连续阅读拿到的经验（检视面板用，不存档）。
  public readonly Dictionary<int,float> sessionXP=new Dictionary<int,float>();
  public float Q=>Netheril.QualityFactor(Quality);
  // 2026-10-28 阅读速度：真正的阅读 tick 记下读者的研究速度（ReadSpeed，缓存 250 刻），只乘在奖励进度上（研习经验、残卷进度和余效、卡苏斯遍数）；
  // 心灵熵、读错、耐瑟启迪的连续时长仍按实际时间。ReadTicks（检查 / 桥接）按 ×1。
  readonly Dictionary<int,float> speed=new Dictionary<int,float>(),speedRem=new Dictionary<int,float>();
  public float Speed(Pawn r){if(r!=null && speed.TryGetValue(r.thingIDNumber,out float f))return f;return 1f;}
  protected int SpeedTicks(Pawn r,int t){
   int id=r.thingIDNumber;speedRem.TryGetValue(id,out float rem);float x=t*Speed(r)+rem;int n=(int)x;speedRem[id]=x-n;return n;
  }
  public override bool DoesProvidesOutcome(Pawn reader)=>Netheril.CannotRead(reader)==null;
  public override void OnReadingTick(Pawn reader,float factor){
   if(reader==null)return;int id=reader.thingIDNumber,now=Find.TickManager.TicksGame,delta=0;speed[id]=ReadSpeed.Factor(reader);
   if(lastTick.TryGetValue(id,out int last)){int gap=now-last;if(gap>0 && gap<=300)delta=gap;else if(gap>300)Restart(reader);}else Restart(reader);
   lastTick[id]=now;if(delta>0)Feed(reader,delta);
  }
  void Restart(Pawn r){int id=r.thingIDNumber;session.Remove(id);pend.Remove(id);sessionXP.Remove(id);NewSession(r);}
  public void Feed(Pawn r,int delta){
   int id=r.thingIDNumber;session.TryGetValue(id,out int s);session[id]=s+delta;
   pend.TryGetValue(id,out int p);p+=delta;
   if(p>=Netheril.SettleTicks){pend[id]=0;Settlement(r,p,true);}else pend[id]=p;
  }
  // 检查和 VPE 桥接用：连续读 n 刻（按 60 刻一段喂进去，结尾不足 60 刻的也结算）。ReadHour = 读 2500 刻。
  public void ReadTicks(Pawn r,int n){speed.Remove(r.thingIDNumber);while(n>0){int d=Math.Min(n,Netheril.SettleTicks);Feed(r,d);n-=d;}Flush(r,true);}
  public void ReadHour(Pawn r)=>ReadTicks(r,Netheril.HourTicks);
  void Flush(Pawn r,bool canStop){int id=r.thingIDNumber;if(pend.TryGetValue(id,out int p) && p>0){pend[id]=0;Settlement(r,p,canStop);}}
  void Settlement(Pawn r,int t,bool canStop){
   if(canStop && Netheril.ShouldStopHot(r)){NetherilIdle.RequestStop(r,true);return;}
   Accrue(r,t);
   var st=Netheril.State;
   if(st!=null){st.AddReadTicks(r,t);int hours=st.TickReadClock(r,t);for(int i=0;i<hours;i++)MaybeMisread(r);}
   if(canStop)NetherilIdle.CheckLimit(r,this);
  }
  // 读错：每累计读满 1 小时（跨 job、跨卷轴，按读者）掷一次。
  void MaybeMisread(Pawn r){
   float extra=HourEntropy(r)*Netheril.MisreadExtra;if(extra<=0f || Netheril.Roll>=Netheril.MisreadChance(r))return;
   Netheril.AddEntropy(r,extra);OnMisread(r);
   if(r.Faction==Faction.OfPlayer)Messages.Message(r.LabelShort+"读错了一段耐瑟咒文，心灵熵猛涨（+"+extra.ToString("0.#")+"）。",r,MessageTypeDefOf.NegativeEvent,false);
  }
  protected void AddXP(Pawn r,float want){float got=Netheril.GiveXP(r,want);if(got>0f){sessionXP.TryGetValue(r.thingIDNumber,out float x);sessionXP[r.thingIDNumber]=x+got;}}
  public float SessionXP(Pawn r){sessionXP.TryGetValue(r.thingIDNumber,out float x);return x;}
  public int SessionTicks(Pawn r){session.TryGetValue(r.thingIDNumber,out int t);return t;}
  protected virtual void NewSession(Pawn reader){}
  protected virtual void OnMisread(Pawn reader){}
  // 这个读者读这卷 1 小时的心灵熵（读错时额外加它的 2 倍；0 = 不会读错）。
  public abstract float HourEntropy(Pawn reader);
  // 结算 t 刻的连续收益。
  protected abstract void Accrue(Pawn reader,int t);
  public abstract string InspectLine(Pawn reader);
  // 结束这次连续阅读（先结算不足 60 刻的零头），返回读了多少刻；下次再读从头算。
  public int EndSession(Pawn reader){
   if(reader==null)return 0;int id=reader.thingIDNumber;Flush(reader,false);
   session.TryGetValue(id,out int t);session.Remove(id);lastTick.Remove(id);pend.Remove(id);sessionXP.Remove(id);speed.Remove(id);return t;
  }
 }

 // 研习卷：每小时心灵熵 +8（巫妖 ×0.5），经验 +6 × 品质倍率，冒险者经验 +50。
 public class BookOutcome_NetherilStudy : BookOutcome_NetherilBase {
  public float LastEntropy;
  public override float HourEntropy(Pawn r)=>Netheril.StudyEntropy*(Necro.Lich(r)!=null?Netheril.LichEntropy:1f);
  protected override void Accrue(Pawn r,int t){
   float h=Netheril.Hours(t);LastEntropy=HourEntropy(r);
   Netheril.AddEntropy(r,LastEntropy*h);
   AddXP(r,Netheril.StudyXP*Q*h*Speed(r));
   Adventurer.GainXP(r,Adventurer.StudyXP*h); // 1.48.0 冒险者经验：研习卷每小时 50（1.52.0 起连续发放）
  }
  public override string InspectLine(Pawn r)=>"研读中：本次 +"+SessionXP(r).ToString("0.##")+" "+(NetherilHooks.VPEActive?"灵能经验":"研习度");
  public override string GetBenefitsString(Pawn reader){
   var s="像冥想一样边读边累积，每小时：心灵熵 +"+Netheril.StudyEntropy+"（巫妖减半），"+(NetherilHooks.VPEActive?"灵能经验":"耐瑟研习度")+" +"+(Netheril.StudyXP*Q).ToString("0.#")+"（品质 ×"+Q.ToString("0.##")+"，再乘读者的阅读速度），没有每日上限。\n"+ReadSpeed.Note;
   if(reader!=null){var why=Netheril.CannotRead(reader);if(why!=null)s+="\n不能读："+why;else s+="\n读错概率 "+Netheril.MisreadChance(reader).ToStringPercent()+"（每累计读满 1 小时判定一次；读错时额外 +"+(HourEntropy(reader)*Netheril.MisreadExtra).ToString("0.#")+" 心灵熵）。";}
   if(!NetherilHooks.VPEActive && reader!=null)s+="\n研习度："+(Netheril.State?.StudyPoints(reader)??0f).ToString("0.#")+"（100/200/400/800/1600 时心灵链接 +1，最高 6 级）";
   return s;
  }
 }

 // 秘术残卷：生成时随机一个学派。每小时进度 +1 × 品质（死灵学派 + 巫妖读者 ×1.5），心灵熵 +4；满 24 点学会本学派一个法术。
 public class BookOutcome_NetherilArcane : BookOutcome_NetherilBase {
  public string school;
  public Dictionary<int,float> progress=new Dictionary<int,float>();
  public Dictionary<int,int> learned=new Dictionary<int,int>();
  readonly HashSet<int> misread=new HashSet<int>(),stuckNotified=new HashSet<int>();
  public string LastLearned;
  public void SetSchool(string s){if(Netheril.Schools.Contains(s))school=s;}
  public override void PostMake(){base.PostMake();if(school==null)school=Netheril.Schools.RandomElement();}
  public override void OnBookGenerated(Pawn author){base.OnBookGenerated(author);if(school==null)school=Netheril.Schools.RandomElement();}
  protected override void NewSession(Pawn r){misread.Remove(r.thingIDNumber);stuckNotified.Remove(r.thingIDNumber);}
  protected override void OnMisread(Pawn r){misread.Add(r.thingIDNumber);}
  public float Progress(Pawn r){progress.TryGetValue(r.thingIDNumber,out float v);return v;}
  public int Learned(Pawn r){learned.TryGetValue(r.thingIDNumber,out int v);return v;}
  public bool LearnedOut(Pawn r)=>Learned(r)>=Netheril.MaxSpellsPerScroll;
  const float Eps=.0005f;
  // 进度到 24 但眼下没有能学的法术（灵能等级不够）：卡住，自动研读不挑这卷。
  public bool Stuck(Pawn r)=>!LearnedOut(r) && Progress(r)>=Netheril.ArcaneGoal-Eps && NetherilSpells.Candidates(r,school).Count==0;
  public float ProgressRate(Pawn r)=>Netheril.ArcaneProgress*Q*(school=="Necromancy" && Necro.Lich(r)!=null?Netheril.NecroLich:1f)*Exalted.ReadFactor(r); // 1.59.0 洞彻心智 +30%
  public override float HourEntropy(Pawn r)=>LearnedOut(r)?0f:Netheril.ArcaneEntropy;
  protected override void Accrue(Pawn r,int t){
   float h=Netheril.Hours(t);
   if(LearnedOut(r)){AddXP(r,Netheril.ArcaneResidueXP*Q*h*Speed(r));return;}
   Netheril.AddEntropy(r,Netheril.ArcaneEntropy*h);
   float p=Mathf.Min(Netheril.ArcaneGoal,Progress(r)+ProgressRate(r)*h*Speed(r));progress[r.thingIDNumber]=p;
   if(p>=Netheril.ArcaneGoal-Eps)Settle(r);
  }
  public override string InspectLine(Pawn r)=>LearnedOut(r)?"研读中：本次 +"+SessionXP(r).ToString("0.##")+" 经验（残卷余效）":"研读中：残卷进度 "+Progress(r).ToString("0.0")+" / "+Netheril.ArcaneGoal;
  public bool Settle(Pawn r){
   int id=r.thingIDNumber;progress[id]=Netheril.ArcaneGoal;
   string use=school;
   if(misread.Contains(id) && Netheril.Roll<Netheril.CrossSchool)use=Netheril.Schools.Where(s=>s!=school).RandomElement();
   var pool=NetherilSpells.Candidates(r,use);
   if(pool.Count==0 && use!=school){use=school;pool=NetherilSpells.Candidates(r,use);}
   if(pool.Count==0){
    if(!stuckNotified.Contains(id) && r.Faction==Faction.OfPlayer){stuckNotified.Add(id);Messages.Message("这卷残卷暂时没有你能理解的东西。（"+r.LabelShort+"：等灵能等级够了再拿起来读就会领悟）",r,MessageTypeDefOf.NeutralEvent,false);}
    return false;
   }
   var label=NetherilSpells.Learn(r,pool.RandomElement());if(label==null)return false;
   LastLearned=label;progress[id]=0f;learned[id]=Learned(r)+1;misread.Remove(id);
   if(r.Faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("残卷领悟",r.LabelShort+"从残卷里领悟了【"+label+"】（"+Netheril.SchoolLabel(use)+"学派）。"+(use!=school?"\n\n这次读错了几段，反倒摸到了另一个学派的门道。":"")+"\n\n同一个人在同一卷残卷里最多领悟 "+Netheril.MaxSpellsPerScroll+" 个法术；学满以后再读只剩研习余效（每读 1 小时经验 +"+(Netheril.ArcaneResidueXP*Q).ToString("0.#")+"，不涨心灵熵）。",LetterDefOf.PositiveEvent,r);
   return true;
  }
  public override string GetBenefitsString(Pawn reader){
   var s="学派："+Netheril.SchoolLabel(school)+"。\n边读边累积，每小时：进度 +"+Q.ToString("0.##")+(school=="Necromancy"?"（巫妖读者 ×1.5）":"")+"，心灵熵 +"+Netheril.ArcaneEntropy+"。满 "+Netheril.ArcaneGoal+" 点学会一个本学派、等级不超过读者灵能等级的心灵法术（不占路径点，不需要解锁路径）。\n每人每卷最多 "+Netheril.MaxSpellsPerScroll+" 个；之后只剩研习余效。\n"+ReadSpeed.Note;
   if(reader!=null){var why=Netheril.CannotRead(reader);if(why!=null)s+="\n不能读："+why;else s+="\n"+reader.LabelShort+"：进度 "+Progress(reader).ToString("0.#")+" / "+Netheril.ArcaneGoal+"，已领悟 "+Learned(reader)+" 个。";}
   return s;
  }
  public override void PostExposeData(){
   base.PostExposeData();
   Scribe_Values.Look(ref school,"lpSchool");
   Scribe_Collections.Look(ref progress,"lpArcaneProgress",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref learned,"lpArcaneLearned",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(progress==null)progress=new Dictionary<int,float>();if(learned==null)learned=new Dictionary<int,int>();if(school==null)school=Netheril.Schools.RandomElement();}
  }
 }

 // 卡苏斯之卷：一遍 40 小时 = 100000 刻（不受品质影响，按读者实际阅读刻数累计在任务状态里），每小时心灵熵 +6，不给经验。
 public class BookOutcome_Karsus : BookOutcome_NetherilBase {
  public override float HourEntropy(Pawn r)=>Netheril.KarsusEntropy;
  protected override void Accrue(Pawn r,int t){Netheril.AddEntropy(r,Netheril.KarsusEntropy*Netheril.Hours(t));int k=SpeedTicks(r,t);if(k>0)Netheril.State?.AddKarsusTicks(r,k);}
  public override string InspectLine(Pawn r){var st=Netheril.State;return st==null?"研读中：卡苏斯之卷":"研读中：卡苏斯第 "+(st.Passes(r)+1)+" 遍 "+(st.KarsusTicks(r)/(float)Netheril.KarsusPassTicks).ToStringPercent("0");}
  public override string GetBenefitsString(Pawn reader){
   var st=Netheril.State;var s="读完一遍要 "+Netheril.KarsusPassHours+" 小时（按阅读时间连续累计，不受品质影响；"+ReadSpeed.Note+"）。每小时心灵熵 +"+Netheril.KarsusEntropy+"，不给经验。\n每读完一遍：卡苏斯的回响 +1 层（最多 "+Netheril.MaxEcho+" 层；每层心灵熵上限 +5%、心灵敏感度 +5%、精神崩溃阈值 +2%）。第 3、5 遍读完时可能陷入卡苏斯的妄念。";
   if(reader!=null && st!=null)s+="\n"+reader.LabelShort+"：已读 "+st.Passes(reader)+" 遍，这一遍 "+st.KarsusHours(reader).ToString("0.#")+" / "+Netheril.KarsusPassHours+" 小时。";
   if(reader!=null){var why=Netheril.CannotRead(reader);if(why!=null)s+="\n不能读："+why;}
   return s;
  }
 }

 // 三种卷轴的物品类：名字带学派、贴图按学派换色签；卡苏斯之卷检视面板列出读者遍数和升神条件。
 public class Book_Netheril : Book {
  public bool legit;
  public BookOutcome_NetherilArcane Arcane=>BookComp?.Doers.OfType<BookOutcome_NetherilArcane>().FirstOrDefault();
  public bool IsKarsus=>def.defName==Netheril.KarsusDef;
  public override string LabelNoCount{get{
   var a=Arcane;string q=this.TryGetQuality(out var qc)?"（"+qc.GetLabel()+"）":"";
   return a?.school!=null?"秘术残卷（"+Netheril.SchoolLabel(a.school)+"）"+q:def.label+q;
  }}
  Graphic schoolGraphic;string schoolGraphicFor;
  public override Graphic Graphic{get{
   var a=Arcane;if(a?.school==null || IsOpen)return base.Graphic;
   if(schoolGraphic==null || schoolGraphicFor!=a.school){schoolGraphicFor=a.school;schoolGraphic=GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath+"_"+a.school,ShaderDatabase.Cutout,def.graphicData.drawSize,Color.white);}
   return schoolGraphic;
  }}
  public override void SpawnSetup(Map map,bool respawningAfterLoad){base.SpawnSetup(map,respawningAfterLoad);if(IsKarsus)NetherilQuest.CheckKarsus(this);}
  public override void TickRare(){base.TickRare();if(IsKarsus && !Destroyed)NetherilQuest.CheckKarsus(this);}
  public override string GetInspectString(){
   var s=base.GetInspectString();var lines=new List<string>();if(!s.NullOrEmpty())lines.Add(s);
   var a=Arcane;if(a!=null)lines.Add("学派："+Netheril.SchoolLabel(a.school));
   lines.Add("娱乐类型："+NetherilJoy.KindsLabel);
   if(IsKarsus){
    var st=Netheril.State;
    if(st!=null){
     var readers=st.karsusPasses.Where(kv=>kv.Value>0).Select(kv=>(p:NetherilQuest.PawnById(kv.Key),n:kv.Value)).Where(x=>x.p!=null).Select(x=>x.p.LabelShort+" "+x.n+" 遍");
     var line=string.Join("、",readers);lines.Add("读者："+(line.NullOrEmpty()?"还没有人读完过":line));
     if(st.deciphered)lines.Add("已解读：卷中的秘密已被参透，仍可研读获得回响，但不会再有升神仪式。");
     else lines.Add(NetherilAscension.ConditionsLine(MapHeld));
    }
   }
   return string.Join("\n",lines);
  }
  public override IEnumerable<StatDrawEntry> SpecialDisplayStats(){
   foreach(var e in base.SpecialDisplayStats())yield return e;
   yield return new StatDrawEntry(StatCategoryDefOf.Basics,"娱乐类型",NetherilJoy.KindsLabel,"读耐瑟卷轴时的娱乐同时算“阅读”和“冥想”：娱乐获得对半分给两种娱乐类型的耐受度，总娱乐量不变。",4990);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref legit,"lpLegit");}
 }

 // 读的条件：没有心灵链接、智识不到 6 都不能读，按钮灰掉并写原因。
 [HarmonyPatch(typeof(BookUtility),nameof(BookUtility.CanReadBook))]
 public static class NetherilCanRead {
  static void Postfix(Book book,Pawn reader,ref string reason,ref bool __result){
   if(!__result || book==null || !Netheril.IsNetheril(book.def))return;
   var why=Netheril.CannotRead(reader);if(why!=null){__result=false;reason=why;}
  }
 }
 // 1.52.0 补丁：原版娱乐读书（JoyGiver_Read → BookUtility.TryGetRandomBookToRead）只用 IsValidBook 筛候选、不查 CanReadBook，
 // 没心灵链接的人会挑中耐瑟卷轴、job 开不了又重挑，原地打转。在候选筛选这一步就把读不了的卷轴去掉，原版会去挑别的书或别的娱乐。
 [HarmonyPatch(typeof(BookUtility),"IsValidBook")]
 public static class NetherilValidBook {
  static readonly Func<Thing,Pawn,bool> valid=AccessTools.MethodDelegate<Func<Thing,Pawn,bool>>(AccessTools.Method(typeof(BookUtility),"IsValidBook"));
  public static bool IsValid(Thing t,Pawn p)=>valid(t,p);
  static void Postfix(Thing __0,Pawn __1,ref bool __result){if(__result && !Netheril.CanRead(__1,__0))__result=false;}
 }

 // 卡苏斯的回响：层数 = 严重度（1～10）。
 public class Hediff_KarsusEcho : HediffWithComps {
  HediffStage cached;int cachedLayers=-1;
  public int Layers=>Mathf.Clamp(Mathf.RoundToInt(Severity),1,Netheril.MaxEcho);
  public override HediffStage CurStage{get{
   int n=Layers;if(cached!=null && cachedLayers==n)return cached;cachedLayers=n;
   return cached=new HediffStage{statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.PsychicEntropyMax,value=1f+.05f*n}},statOffsets=new List<StatModifier>{new StatModifier{stat=StatDefOf.PsychicSensitivity,value=.05f*n},new StatModifier{stat=StatDefOf.MentalBreakThreshold,value=.02f*n}}};
  }}
  public override string LabelInBrackets=>Layers+" 层";
  public override bool ShouldRemove=>false;
 }

 // 1.42.0 耐瑟启迪：一次连续读耐瑟卷轴满 1 小时、阅读 job 结束时（含熵 >90% 自动停读）获得或刷新为 10 小时。
 // 严重度 1 = 普通（研习卷、秘术残卷），2 = 卡苏斯启迪（卡苏斯之卷，普通档不会把它降回去）。数值在 Defs/Netheril_Scrolls.xml；智识学习 ×1.25 在 NetherilInsightLearn。
 public static class NetherilInsight {
  public const string DefName="LP_NetherilInsight";
  public const int DurationTicks=10*GenDate.TicksPerHour,CheckInterval=60;
  public const float LearnFactor=1.25f,Normal=1f,Karsus=2f;
  static HediffDef def;
  public static HediffDef Def=>def??(def=DefDatabase<HediffDef>.GetNamed(DefName));
  public static Hediff Of(Pawn p)=>p?.health?.hediffSet?.GetFirstHediffOfDef(Def);
  public static bool EntropyFull(Pawn p){var e=p?.psychicEntropy;return e!=null && e.MaxEntropy>0f && e.EntropyValue>=e.MaxEntropy;}
  // 心灵熵已经满了就不给（给了也会马上消散）。
  public static Hediff Grant(Pawn p,bool karsus){
   if(p?.health==null || p.Dead || EntropyFull(p))return null;
   var h=Of(p);
   if(h==null){h=HediffMaker.MakeHediff(Def,p);h.Severity=karsus?Karsus:Normal;p.health.AddHediff(h);h=Of(p);}
   else if(karsus)h.Severity=Karsus;
   var c=h?.TryGetComp<HediffComp_Disappears>();if(c!=null)c.ticksToDisappear=DurationTicks;
   return h;
  }
  public static void Dissipate(Hediff h){
   var p=h?.pawn;if(p==null || !p.health.hediffSet.hediffs.Contains(h))return;
   p.health.RemoveHediff(h);
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"的"+h.LabelBase+"随心灵熵满溢而消散。",p,MessageTypeDefOf.NeutralEvent,false);
  }
 }
 public class HediffCompProperties_NetherilInsight : HediffCompProperties {public HediffCompProperties_NetherilInsight(){compClass=typeof(HediffComp_NetherilInsight);}}
 // 每 60 刻看一次心灵熵，满了立刻消散。
 public class HediffComp_NetherilInsight : HediffComp {
  int acc;
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){
   acc+=delta;if(acc<NetherilInsight.CheckInterval)return;acc=0;
   if(NetherilInsight.EntropyFull(Pawn))NetherilInsight.Dissipate(parent);
  }
  public override string CompTipStringExtra=>"智识技能学习 ×"+NetherilInsight.LearnFactor.ToString("0.##")+"\n心灵熵满溢时消散";
 }
 [HarmonyPatch(typeof(JobDriver),nameof(JobDriver.Cleanup))]
 public static class NetherilInsightOnReadEnd {
  static void Postfix(JobDriver __instance){
   if(!(__instance is JobDriver_Reading))return;
   var p=__instance.pawn;var book=__instance.job?.targetA.Thing as Book;
   if(p==null || book==null || !Netheril.IsNetheril(book.def))return;
   var d=Netheril.Doer<BookOutcome_NetherilBase>(book);
   if(d!=null && d.EndSession(p)>=Netheril.HourTicks)NetherilInsight.Grant(p,d is BookOutcome_Karsus);
   NetherilIdle.ClearStop(p);
  }
 }

 // 1.52.0 补丁：空闲时自动研读。思考树插在 Humanlike_PostMain（原版工作/需求/娱乐/冥想的主体之后、闲逛之前），insertPriority -10。
 public static class NetherilIdle {
  public const int MaxTicks=2*GenDate.TicksPerHour;
  public const float EntropyBelow=.5f;
  public static bool IsAuto(Job j)=>j!=null && j.def==JobDefOf.Reading && j.jobGiver is JobGiver_NetherilIdleStudy;
  // 不能在原版阅读 tick 中途结束 job（后面还有娱乐判定会用到 CurJob），所以记下来，到原版 JoyUtility.JoyTickCheckEnd 那一步再结束；没有娱乐需求的读者立刻结束。
  static readonly Dictionary<int,bool> stops=new Dictionary<int,bool>();
  public static void RequestStop(Pawn p,bool hot){
   if(p.needs?.joy==null){p.jobs?.EndCurrentJob(hot?JobCondition.InterruptForced:JobCondition.Succeeded);if(hot)Netheril.HotMessage(p);return;}
   stops[p.thingIDNumber]=hot;
  }
  public static bool TakeStop(Pawn p,out bool hot){if(stops.TryGetValue(p.thingIDNumber,out hot)){stops.Remove(p.thingIDNumber);return true;}return false;}
  public static void ClearStop(Pawn p){if(p!=null)stops.Remove(p.thingIDNumber);}
  public static bool StopPending(Pawn p)=>p!=null && stops.ContainsKey(p.thingIDNumber);
  // 自动研读读满 2 小时就放下，交回思考树。
  public static void CheckLimit(Pawn p,BookOutcome_NetherilBase d){if(IsAuto(p.CurJob) && d.SessionTicks(p)>=MaxTicks && !StopPending(p))RequestStop(p,false);}
  // 不能自动研读的原因（null = 可以）；检查和说明用。
  public static string Blocked(Pawn p){
   if(p==null || !p.Spawned || p.Map==null)return "不在地图上";
   if(!LichMod.Settings.netherilIdleStudy)return "设置已关闭";
   if(p.Faction!=Faction.OfPlayer || !p.IsColonist || p.IsPrisoner || p.IsSlave)return "不是玩家殖民者";
   if(p.Drafted || p.InMentalState || p.Downed)return "征召、精神状态或倒地";
   if(HealthAIUtility.ShouldSeekMedicalRest(p) || p.InBed())return "在养伤或卧床";
   var why=Netheril.CannotRead(p);if(why!=null)return why;
   var e=p.psychicEntropy;if(e==null || e.MaxEntropy<=0f || e.EntropyValue>=e.MaxEntropy*EntropyBelow)return "心灵熵不低于上限的 50%";
   return null;
  }
  // 选卷：① 卡苏斯之卷（遍数 < 7，或未解读且回响 < 10 层）② 秘术残卷（这卷学会 < 2 个且没卡住）③ 研习卷（始终有用）；同类挑最近。
  public static int Rank(Pawn p,Thing t){
   var st=Netheril.State;
   switch(t.def.defName){
    case Netheril.KarsusDef:{if(st==null)return -1;int echo=(p.health.hediffSet.hediffs.OfType<Hediff_KarsusEcho>().FirstOrDefault()?.Layers)??0;return st.Passes(p)<Netheril.AscensionPasses || (!st.deciphered && echo<Netheril.MaxEcho)?0:-1;}
    case Netheril.ArcaneDef:{var a=Netheril.Doer<BookOutcome_NetherilArcane>(t);return a!=null && !a.LearnedOut(p) && !a.Stuck(p)?1:-1;}
    case Netheril.StudyDef:return 2;
    default:return -1;
   }
  }
  public static Book PickBook(Pawn p){
   var map=p.Map;Book best=null;int bestRank=int.MaxValue;float bestDist=float.MaxValue;
   foreach(var name in new[]{Netheril.KarsusDef,Netheril.ArcaneDef,Netheril.StudyDef}){
    var def=Items.Thing(name);if(def==null)continue;
    foreach(var t in map.listerThings.ThingsOfDef(def)){
     if(!(t is Book b) || !b.Spawned || b.IsForbidden(p))continue;
     int r=Rank(p,b);if(r<0 || r>bestRank)continue;
     float dist=(b.Position-p.Position).LengthHorizontalSquared;if(r==bestRank && dist>=bestDist)continue;
     if(!BookUtility.CanReadBook(b,p,out _) || !p.CanReserveAndReach(b,PathEndMode.Touch,Danger.None))continue;
     best=b;bestRank=r;bestDist=dist;
    }
   }
   return best;
  }
  public static Job MakeJob(Pawn p){
   if(Blocked(p)!=null)return null;var b=PickBook(p);if(b==null)return null;
   var j=JobMaker.MakeJob(JobDefOf.Reading,b);j.count=1;j.ignoreJoyTimeAssignment=true;return j;
  }
 }
 public class JobGiver_NetherilIdleStudy : ThinkNode_JobGiver {
  protected override Job TryGiveJob(Pawn pawn){var j=NetherilIdle.MakeJob(pawn);if(j!=null)j.jobGiver=this;return j;}
 }
 // 结算时记下的“该停了”在这里执行；自动研读不因娱乐满了而停（读满 2 小时才停）。
 [HarmonyPatch(typeof(JoyUtility),nameof(JoyUtility.JoyTickCheckEnd))]
 public static class NetherilIdleJoyTick {
  static bool Prefix(Pawn pawn,ref JoyTickFullJoyAction fullJoyAction,ref bool __result){
   var job=pawn?.CurJob;if(job==null || job.def!=JobDefOf.Reading)return true;
   if(NetherilIdle.TakeStop(pawn,out bool hot)){
    pawn.jobs.curDriver.EndJobWith(hot?JobCondition.InterruptForced:JobCondition.Succeeded);if(hot)Netheril.HotMessage(pawn);
    __result=true;return false;
   }
   if(NetherilIdle.IsAuto(job))fullJoyAction=JoyTickFullJoyAction.None;
   return true;
  }
 }
 // 1.52.0 补丁：读耐瑟卷轴的娱乐同时算“阅读”和“冥想”。原版阅读 job 的娱乐类型只能是 Reading，这里把每次娱乐获得对半分给 Reading 和 Meditative 两种耐受度（各按自己的耐受度折算，总量不翻倍）。
 public static class NetherilJoy {
  public const string MeditativeName="Meditative";
  static JoyKindDef meditative;
  public static JoyKindDef Meditative=>meditative??(meditative=DefDatabase<JoyKindDef>.GetNamedSilentFail(MeditativeName));
  public const string KindsLabel="阅读、冥想";
  public static bool ReadingNetheril(Pawn p){var j=p?.CurJob;return j!=null && j.def==JobDefOf.Reading && j.targetA.Thing is Book b && Netheril.IsNetheril(b.def);}
 }
 [HarmonyPatch(typeof(Need_Joy),nameof(Need_Joy.GainJoy))]
 public static class NetherilJoySplit {
  static bool inside;
  static bool Prefix(Need_Joy __instance,float amount,JoyKindDef joyKind,Pawn ___pawn){
   if(inside || amount<=0f || joyKind?.defName!="Reading" || NetherilJoy.Meditative==null || !NetherilJoy.ReadingNetheril(___pawn))return true;
   inside=true;
   try{__instance.GainJoy(amount*.5f,joyKind);__instance.GainJoy(amount*.5f,NetherilJoy.Meditative);}finally{inside=false;}
   return false;
  }
 }
 // 娱乐低、想要冥想类娱乐时也会去读耐瑟卷轴（条件同手动阅读：能读、能到、没禁止；优先挑对他有用的卷）。
 public class JoyGiver_NetherilRead : JoyGiver {
  public override float GetChance(Pawn pawn)=>Netheril.CannotRead(pawn)!=null?0f:base.GetChance(pawn);
  public override Job TryGiveJob(Pawn pawn){
   if(pawn?.Map==null || Netheril.CannotRead(pawn)!=null)return null;
   var e=pawn.psychicEntropy;if(e!=null && e.MaxEntropy>0f && e.EntropyValue>e.MaxEntropy*Netheril.StopAt)return null;
   Thing book=NetherilIdle.PickBook(pawn);
   if(book==null){
    float best=float.MaxValue;
    foreach(var name in new[]{Netheril.KarsusDef,Netheril.ArcaneDef,Netheril.StudyDef}){var def=Items.Thing(name);if(def==null)continue;
     foreach(var t in pawn.Map.listerThings.ThingsOfDef(def)){if(!(t is Book b) || !b.Spawned || b.IsForbidden(pawn))continue;float d=(b.Position-pawn.Position).LengthHorizontalSquared;if(d>=best || !BookUtility.CanReadBook(b,pawn,out _) || !pawn.CanReserveAndReach(b,PathEndMode.Touch,Danger.None))continue;best=d;book=b;}}
   }
   if(book==null)return null;
   var j=JobMaker.MakeJob(def.jobDef??JobDefOf.Reading,book);j.count=1;return j;
  }
 }
 // 1.52.0 补丁：能读耐瑟卷轴的人（心灵链接、智识 ≥6、心灵熵 < 上限 90%）找娱乐时，地图上有对他有用、能到、没禁止没预订的卷就先读它（选卷同空闲研读）；
 // 拿不到再走原版随机。只管原版的 JobGiver_GetJoy 本身（聚会、卧床等子类照旧），也照原版先查养伤和娱乐满了。
 [HarmonyPatch(typeof(JobGiver_GetJoy),"TryGiveJob")]
 public static class NetherilJoyFirst {
  public const string GiverDef="LP_NetherilMeditativeReading";
  static JoyGiverDef giver;
  public static JoyGiverDef Giver=>giver??(giver=DefDatabase<JoyGiverDef>.GetNamedSilentFail(GiverDef));
  static readonly Func<JobGiver_GetJoy,JoyGiverDef,bool> allowed=AccessTools.MethodDelegate<Func<JobGiver_GetJoy,JoyGiverDef,bool>>(AccessTools.Method(typeof(JobGiver_GetJoy),"JoyGiverAllowed"));
  public static Job Try(Pawn p){
   if(p?.Map==null || p.needs?.joy==null || p.needs.joy.CurLevel>=1f || Giver==null || Netheril.CannotRead(p)!=null)return null;
   if(p.InBed() && HealthAIUtility.ShouldSeekMedicalRest(p))return null;
   var e=p.psychicEntropy;if(e!=null && e.MaxEntropy>0f && e.EntropyValue>=e.MaxEntropy*Netheril.StopAt)return null;
   if(NetherilIdle.PickBook(p)==null)return null;
   return Giver.Worker.TryGiveJob(p);
  }
  static bool Prefix(JobGiver_GetJoy __instance,Pawn __0,ref Job __result){
   if(__instance.GetType()!=typeof(JobGiver_GetJoy) || Giver==null || !allowed(__instance,Giver))return true;
   var j=Try(__0);if(j==null)return true;
   __result=j;return false;
  }
 }
 // 检视面板：正在读耐瑟卷轴的人多一行“研读中：…”。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetInspectString))]
 public static class NetherilReadingInspect {
  static void Postfix(Pawn __instance,ref string __result){
   var p=__instance;if(!(p.jobs?.curDriver is JobDriver_Reading jd) || !jd.IsReading)return;
   var book=jd.job?.targetA.Thing as Book;if(book==null || !Netheril.IsNetheril(book.def))return;
   var d=Netheril.Doer<BookOutcome_NetherilBase>(book);if(d==null || !d.DoesProvidesOutcome(p))return;
   var line=d.InspectLine(p);__result=__result.NullOrEmpty()?line:__result+"\n"+line;
  }
 }
 [HarmonyPatch(typeof(SkillRecord),nameof(SkillRecord.Learn))]
 public static class NetherilInsightLearn {
  static void Prefix(SkillRecord __instance,Pawn ___pawn,ref float xp,bool direct){
   if(xp>0f && !direct && __instance.def==SkillDefOf.Intellectual && NetherilInsight.Of(___pawn)!=null)xp*=NetherilInsight.LearnFactor;
  }
 }

 // 卡苏斯的妄念：1 天不干活，只读卡苏斯之卷，读不了就在耐瑟祭坛旁游荡。
 public class JobGiver_KarsusObsession : ThinkNode_JobGiver {
  protected override Job TryGiveJob(Pawn pawn){
   var map=pawn.Map;if(map==null)return null;
   var k=NetherilQuest.Karsus;
   if(k is Book b && b.Spawned && b.Map==map && BookUtility.CanReadBook(b,pawn,out _) && pawn.CanReserveAndReach(b,PathEndMode.Touch,Danger.Some)){var j=JobMaker.MakeJob(JobDefOf.Reading,b);j.count=1;return j;}
   var altar=NetherilAscension.Altars(map).FirstOrDefault();
   if(altar!=null && RCellFinder.TryFindRandomCellNearWith(altar.Position,c=>c.Standable(map) && pawn.CanReach(c,PathEndMode.OnCell,Danger.Some),map,out var cell,3,8)){var j=JobMaker.MakeJob(JobDefOf.GotoWander,cell);j.locomotionUrgency=LocomotionUrgency.Walk;return j;}
   return null;
  }
 }

 // 黑网商队：每次 30% 带 1 卷研习卷、10% 带 1 卷秘术残卷。
 public class StockGenerator_NetherilChance : StockGenerator {
  public ThingDef thingDef;public float chance=.3f;
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   if(thingDef==null || !Rand.Chance(chance))yield break;
   var t=ThingMaker.MakeThing(thingDef);t.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityTraderItem(),ArtGenerationContext.Outsider);yield return t;
  }
  public override bool HandlesThingDef(ThingDef d)=>d==thingDef;
 }
}
