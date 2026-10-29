using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;
namespace LichPhylactery {
 // 1.37.0 耐瑟瑞尔卷轴任务线检查（Test.ps1 -Only Netheril，共 28 条，26-28 是 1.42.0 耐瑟启迪；第 8、17 条的存读档部分在 AfterLoad 里合成一条）。
 // VPE 的经验与法术池在桥接里另查（Test.ps1 -VPE）。
 public partial class LichChecks {
  readonly List<Thing> nMade=new List<Thing>();
  static string nArcaneId;static int nErrand,nDeadline,nReaderId;static float nProgress;static string nStage;static bool nCampSite;
  Pawn NReader(int intellect,int psylink){
   var p=Person();foreach(var t in p.story.traits.allTraits.Where(t=>t.def.defName=="PsychicSensitivity").ToList())p.story.traits.RemoveTrait(t);
   GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);
   var s=p.skills.GetSkill(SkillDefOf.Intellectual);s.Level=intellect;s.passion=Passion.None;
   for(int i=0;i<psylink;i++)p.ChangePsylinkLevel(1,false);
   foreach(var a in p.abilities.abilities.ToList())p.abilities.RemoveAbility(a.def);
   nMade.Add(p);return p;
  }
  Thing NSpawn(Thing t,IntVec3? at=null){GenPlace.TryPlaceThing(t,at??CellFinder.RandomClosewalkCellNear(map.Center,map,6),map,ThingPlaceMode.Near);nMade.Add(t);return t;}
  static float Entropy(Pawn p)=>p.psychicEntropy.EntropyValue;
  void NResetQuest(NetherilQuestState st){
   if(st.karsus!=null && !st.karsus.Destroyed)st.karsus.Destroy();st.karsus=null;
   foreach(var s in new[]{st.campSite,st.cryptSite,st.wreckSite})if(s!=null && !s.Destroyed)s.Destroy();
   if(st.offeringBox!=null && !st.offeringBox.Destroyed)st.offeringBox.Destroy();
   if(st.casket!=null && !st.casket.Destroyed)st.casket.Destroy();
   st.campSite=st.cryptSite=st.wreckSite=null;st.offeringBox=null;st.casket=null;st.casketGuards.Clear();st.campLeader=null;
   st.stage="idle";st.deal=null;st.envoyTick=-1;st.refusals=0;st.errand=0;st.errandDeadline=-1;st.envoyPending=false;st.reappearMode=false;
   if(st.envoyLord!=null){foreach(var p in st.envoyLord.ownedPawns.ToList())if(!p.Destroyed){Necro.Registry?.constructs.Remove(p);p.Destroy();}if(map.lordManager.lords.Contains(st.envoyLord))map.lordManager.RemoveLord(st.envoyLord);}
   st.envoy=null;st.envoyLord=null;st.karsusStatus="none";st.deciphered=false;st.reappearTick=-1;st.larlochDefeated=false;st.nextUndeadRaid=-1;st.demigod=null;
   NetherilQuest.InvalidateScrollCache();
  }

  void CheckNetheril(){
   var st=Netheril.State;if(st==null)throw new Exception("netheril: quest state component missing");
   var s=LichMod.Settings;var saved=(s.netherilQuest,s.larlochRaids);s.netherilQuest=true;
   var before=new HashSet<Thing>(map.listerThings.AllThings);var lords=new HashSet<Lord>(map.lordManager.lords);var letters=new HashSet<Letter>(Find.LetterStack.LettersListForReading);
   var research=DefDatabase<ResearchProjectDef>.GetNamed(NetherilQuest.ResearchName);bool researched=research.IsFinished;
   var goodwill=Find.FactionManager.AllFactions.Where(f=>!f.IsPlayer && f.HasGoodwill).ToDictionary(f=>f,f=>f.PlayerGoodwill);
   try{NetherilQuestChecks(st);NetherilScrollChecks(st);NetherilAscensionChecks(st);NetherilErrandForSave(st);}
   finally{
    Netheril.TestRoll=null;s.netherilQuest=saved.Item1;s.larlochRaids=saved.Item2;
    foreach(var c in map.gameConditionManager.ActiveConditions.Where(c=>c.def==NetherilAscension.TremorDef || c.def==NetherilAscension.CollapseDef).ToList())c.End();
    foreach(var l in map.lordManager.lords.Where(l=>!lords.Contains(l)).ToList())map.lordManager.RemoveLord(l);
    foreach(var t in nMade)if(t!=null && !t.Destroyed && t.ThingID!=nArcaneId){if(t is Pawn p){Necro.Registry?.constructs.Remove(p);if(p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();}if(!t.Destroyed)t.Destroy();}
    nMade.Clear();
    foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p)).ToList()){Necro.Registry?.constructs.Remove(p);p.Destroy();}
    foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Mote) && t.def.category!=ThingCategory.Ethereal && t.ThingID!=nArcaneId && t!=st.offeringBox).ToList())if(!t.Destroyed)t.Destroy();
    foreach(var l in Find.LetterStack.LettersListForReading.Where(l=>!letters.Contains(l)).ToList())Find.LetterStack.RemoveLetter(l);
    foreach(var c in map.mapPawns.FreeColonists)foreach(var n in new[]{"LP_CompanionSentAway","LP_WitnessedAscension","LP_AscensionFailed"})c.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(DefDatabase<ThoughtDef>.GetNamed(n));
    foreach(var kv in goodwill)if(kv.Key.PlayerGoodwill!=kv.Value)Factions.SetRelation(kv.Key,Faction.OfPlayer,kv.Value);
    if(!researched)Traverse.Create(Find.ResearchManager).Field("progress").GetValue<Dictionary<ResearchProjectDef,float>>()?.Remove(research);
    ExtinguishAll();
   }
  }

  // 1.42.0 耐瑟启迪（netheril 26-28）。
  void NetherilInsightChecks(Book study){
   var d=Netheril.Doer<BookOutcome_NetherilBase>(study);
   Hediff Read(Pawn p,int ticks){var j=JobMaker.MakeJob(JobDefOf.Reading,study);p.jobs.StartJob(j,JobCondition.InterruptForced);d.ReadTicks(p,ticks);p.jobs.EndCurrentJob(JobCondition.InterruptForced,false);return NetherilInsight.Of(p);}
   // 26 读不满 1 小时没有；满 1 小时放下卷轴得普通档（数值照表），智识学习 ×1.25；卡苏斯档不被普通档降回。
   var r=NReader(10,1);bool shortNone=Read(r,Netheril.HourTicks-1)==null;
   var sk=r.skills.GetSkill(SkillDefOf.Intellectual);float x0=(float)KnowledgeFeedback.Experience(sk);sk.Learn(100f,false,true);float plain=(float)KnowledgeFeedback.Experience(sk)-x0;
   var h=Read(r,Netheril.HourTicks);x0=(float)KnowledgeFeedback.Experience(sk);sk.Learn(100f,false,true);float boosted=(float)KnowledgeFeedback.Experience(sk)-x0;
   var stg=h?.CurStage;float Off(StatDef s)=>stg?.statOffsets?.FirstOrDefault(m=>m.stat==s)?.value??0f;float Fac(StatDef s)=>stg?.statFactors?.FirstOrDefault(m=>m.stat==s)?.value??1f;
   bool normal=h!=null && Near(h.Severity,NetherilInsight.Normal) && Near(Off(StatDefOf.PsychicSensitivity),.2f) && Near(Fac(StatDefOf.MeditationFocusGain),1.25f) && Near(Fac(StatDefOf.PsychicEntropyRecoveryRate),.6f) && plain>0f && Near(boosted/plain,1.25f);
   NetherilInsight.Grant(r,true);var hk=Read(r,Netheril.HourTicks);stg=hk?.CurStage;
   bool karsus=hk!=null && Near(hk.Severity,NetherilInsight.Karsus) && hk.LabelBase=="卡苏斯启迪" && Near(Off(StatDefOf.PsychicSensitivity),.35f) && Near(Fac(StatDefOf.PsychicEntropyMax),1.1f) && Near(Fac(StatDefOf.PsychicEntropyRecoveryRate),.5f);
   Check(shortNone && normal && karsus,"netheril 26: a 59-minute read gives nothing ("+shortNone+"), a 1-hour read gives "+NetherilInsight.DefName+" (sensitivity +20%, meditation x1.25, entropy recovery x0.6, intellectual learning "+plain+" -> "+boosted+"), Karsus tier is kept by a later normal read ("+karsus+")");
   // 27 10 小时后消失；再读满 1 小时把剩余时间重置为 10 小时。
   var r2=NReader(10,1);var h2=Read(r2,Netheril.HourTicks);var dis=h2?.TryGetComp<HediffComp_Disappears>();float sev=0f;
   bool full=dis!=null && dis.ticksToDisappear==NetherilInsight.DurationTicks;
   if(dis!=null)dis.ticksToDisappear=100;int left=dis?.ticksToDisappear??-1;Read(r2,Netheril.HourTicks);int after=dis?.ticksToDisappear??-1;bool reset=after==NetherilInsight.DurationTicks;
   if(dis!=null)for(int i=1;i<NetherilInsight.DurationTicks;i++)dis.CompPostTick(ref sev);bool before=h2!=null && !h2.ShouldRemove;dis?.CompPostTick(ref sev);bool gone=h2!=null && h2.ShouldRemove;
   Check(full && left==100 && reset && before && gone,"netheril 27: insight lasts "+NetherilInsight.DurationTicks+" ticks (10 h), a new 1-hour read resets it ("+left+" -> "+after+"), it is removed at 10 h ("+before+"/"+gone+")");
   // 28 心灵熵到上限：下一次检查（60 刻）立刻消散；熵满时读完不给。
   var r3=NReader(10,1);var h3=Read(r3,Netheril.HourTicks);var ic=h3?.TryGetComp<HediffComp_NetherilInsight>();
   r3.psychicEntropy.TryAddEntropy(r3.psychicEntropy.MaxEntropy*1.05f,null,false,true);float s3=0f;ic?.CompPostTickInterval(ref s3,NetherilInsight.CheckInterval);
   bool dissipated=h3!=null && NetherilInsight.Of(r3)==null;bool refused=Read(r3,Netheril.HourTicks)==null;
   Check(dissipated && refused,"netheril 28: entropy at its max ("+r3.psychicEntropy.EntropyValue.ToString("0")+"/"+r3.psychicEntropy.MaxEntropy.ToString("0")+") dissipates the insight within "+NetherilInsight.CheckInterval+" ticks ("+dissipated+"), and a read ending at full entropy gives none ("+refused+")");
  }

  // 1.52.0 补丁：连续累积 + 空闲自动研读（netheril 29-32）。
  void NetherilIdleChecks(NetherilQuestState st,Book study,Thing karsus){
   var s=LichMod.Settings;bool savedIdle=s.netherilIdleStudy;s.netherilIdleStudy=true;
   var forbidden=new List<Thing>();
   void OnlyAllow(params Thing[] keep){foreach(var t in map.listerThings.AllThings.Where(t=>t is Book && Netheril.IsNetheril(t.def) && !keep.Contains(t)).ToList())if(!t.IsForbidden(Faction.OfPlayer)){t.SetForbidden(true,false);forbidden.Add(t);}}
   void AllowAll(){foreach(var t in forbidden)if(!t.Destroyed)t.SetForbidden(false,false);forbidden.Clear();}
   try{
    // 29 研习卷读 600 刻：心灵熵 +1.92、经验 +1.44 × 品质（普通 ×1），检视面板有“研读中”。
    var sd=Netheril.Doer<BookOutcome_NetherilStudy>(study);var r=NReader(10,1);float e0=Entropy(r),sp0=st.StudyPoints(r);
    sd.ReadTicks(r,600);float de=Entropy(r)-e0,dx=sd.SessionXP(r);string line=sd.InspectLine(r);sd.EndSession(r);
    Check(Math.Abs(de-1.92f)<=.05f && Math.Abs(dx-1.44f*sd.Q)<=.05f && (NetherilHooks.VPEActive || Math.Abs(st.StudyPoints(r)-sp0-1.44f*sd.Q)<=.05f) && line.StartsWith("研读中：本次 +1.44"),"netheril 29: 600 ticks of the study scroll add "+de.ToString("0.###")+" entropy and "+dx.ToString("0.###")+" experience continuously (expected 1.92 / 1.44 x"+sd.Q+"), inspect line '"+line+"'");
    // 30 合格的空闲殖民者拿到研读 job；没心灵链接 / 心灵熵 ≥ 50% / 设置关闭时拿不到。
    var tree=DefDatabase<ThinkTreeDef>.GetNamedSilentFail("LP_NetherilIdleStudy");var giver=new JobGiver_NetherilIdleStudy();
    Job Give(Pawn p)=>giver.TryIssueJobPackage(p,default(JobIssueParams)).Job;
    var ok=NReader(10,1);var good=Give(ok);
    var noLink=NReader(10,0);var hot=NReader(10,1);hot.psychicEntropy.TryAddEntropy(hot.psychicEntropy.MaxEntropy*.5f,null,false,true);
    bool refuse=Give(noLink)==null && Give(hot)==null;s.netherilIdleStudy=false;bool off=Give(ok)==null;s.netherilIdleStudy=true;
    Check(tree!=null && tree.insertTag=="Humanlike_PostMain" && tree.insertPriority<0 && good!=null && good.def==JobDefOf.Reading && Netheril.IsNetheril(good.targetA.Thing?.def) && good.ignoreJoyTimeAssignment && refuse && off,"netheril 30: an idle colonist gets a reading job ("+good?.targetA.Thing?.LabelShort+"), none without psylink ('"+NetherilIdle.Blocked(noLink)+"'), at 50% entropy ('"+NetherilIdle.Blocked(hot)+"') or with the setting off ("+off+")");
    // 31 卡苏斯之卷（遍数 < 7）优先于研习卷；只剩研习卷时选研习卷（1.52.0 补丁起研习卷始终有用，经验已超过旧的 40 也照选）。
    var pk=NReader(10,1);OnlyAllow(study,karsus);var pick=Give(pk)?.targetA.Thing;
    OnlyAllow(study);var onlyStudy=Give(pk)?.targetA.Thing;
    Netheril.GiveXP(pk,50f);var past=Give(pk)?.targetA.Thing;AllowAll();
    Check(pick==karsus && onlyStudy==study && past==study,"netheril 31: Karsus (0/7 reads) is picked over the study scroll ("+pick?.LabelShort+"), study alone is picked ("+onlyStudy?.LabelShort+"), still picked after 50 experience today ("+past?.LabelShort+")");
    // 32 自动研读读满 2 小时结束（在原版娱乐判定那一步），之后有耐瑟启迪；娱乐满了也不提前停。
    var ar=NReader(10,1);OnlyAllow(study);var aj=giver.TryIssueJobPackage(ar,default(JobIssueParams));AllowAll();
    if(aj.Job!=null)ar.jobs.StartJob(aj.Job,JobCondition.InterruptForced,aj.SourceNode);
    int aid=aj.Job?.loadID??-1;var ad=Netheril.Doer<BookOutcome_NetherilBase>(aj.Job?.targetA.Thing);bool auto=NetherilIdle.IsAuto(ar.CurJob);
    ad?.ReadTicks(ar,NetherilIdle.MaxTicks-Netheril.SettleTicks);if(ar.needs?.joy!=null)ar.needs.joy.CurLevel=1f;
    JoyUtility.JoyTickCheckEnd(ar,1,JoyTickFullJoyAction.GoToNextToil,1f,null);bool keptAtFullJoy=ar.CurJob?.loadID==aid && !NetherilIdle.StopPending(ar);
    ad?.ReadTicks(ar,Netheril.SettleTicks);bool pending=NetherilIdle.StopPending(ar);
    JoyUtility.JoyTickCheckEnd(ar,1,JoyTickFullJoyAction.GoToNextToil,1f,null);bool ended=ar.CurJob?.loadID!=aid;
    Check(auto && keptAtFullJoy && pending && ended && NetherilInsight.Of(ar)!=null && !NetherilIdle.StopPending(ar),"netheril 32: the auto-study job keeps going at full joy ("+keptAtFullJoy+"), asks to stop at "+NetherilIdle.MaxTicks+" ticks ("+pending+"), ends ("+ended+") and leaves "+NetherilInsight.Of(ar)?.LabelBase);
    ar.jobs.StopAll();
    // 33 读耐瑟卷轴的娱乐同时算阅读和冥想：两种耐受度都上升；冥想类 JoyGiver 能挑到耐瑟卷轴。
    var jr=NReader(10,1);var rk=DefDatabase<JoyKindDef>.GetNamed("Reading");var mk=NetherilJoy.Meditative;
    var rj=JobMaker.MakeJob(JobDefOf.Reading,study);jr.jobs.StartJob(rj,JobCondition.InterruptForced);
    var tol=jr.needs.joy.tolerances;float tr0=tol[rk],tm0=tol[mk];jr.needs.joy.CurLevel=.3f;float lv0=jr.needs.joy.CurLevel;
    JoyUtility.JoyTickCheckEnd(jr,250,JoyTickFullJoyAction.GoToNextToil,1f,null);float tr1=tol[rk],tm1=tol[mk],lv1=jr.needs.joy.CurLevel;jr.jobs.StopAll();
    var jg=DefDatabase<JoyGiverDef>.GetNamedSilentFail("LP_NetherilMeditativeReading");var mj=jg?.Worker.TryGiveJob(jr);
    Check(mk!=null && tr1>tr0 && tm1>tm0 && Math.Abs((tr1-tr0)-(tm1-tm0))<.0001f && lv1>lv0 && jg!=null && jg.joyKind==mk && mj!=null && Netheril.IsNetheril(mj.targetA.Thing?.def),"netheril 33: reading a Netheril scroll raises both Reading ("+tr0.ToString("0.####")+" -> "+tr1.ToString("0.####")+") and Meditative ("+tm0.ToString("0.####")+" -> "+tm1.ToString("0.####")+") tolerance, joy "+lv0.ToString("0.###")+" -> "+lv1.ToString("0.###")+"; the meditative joy giver picks "+mj?.targetA.Thing?.LabelShort);
    // 34 没心灵链接的人娱乐低时：原版读书 JoyGiver 只挑普通书、不挑耐瑟卷轴；我们的冥想 JoyGiver 和自动研读也不给 job。
    var nl=NReader(10,0);nl.needs.joy.CurLevel=.1f;var novel=NSpawn(BookUtility.MakeBook(DefDatabase<ThingDef>.GetNamed("Novel"),ArtGenerationContext.Outsider,null));
    OnlyAllow(study);var vg=DefDatabase<JoyGiverDef>.AllDefs.FirstOrDefault(d=>d.giverClass==typeof(JoyGiver_Read));
    int got=0,neth=0;for(int i=0;i<30;i++){var vj=vg?.Worker.TryGiveJob(nl);if(vj==null)continue;got++;if(Netheril.IsNetheril(vj.targetA.Thing?.def))neth++;}
    bool validNl=NetherilValidBook.IsValid(study,nl),validNovel=NetherilValidBook.IsValid(novel,nl),validOk=NetherilValidBook.IsValid(study,NReader(10,1));
    var mnl=jg?.Worker.TryGiveJob(nl);var inl=Give(nl);AllowAll();
    Check(vg!=null && got>0 && neth==0 && !validNl && validNovel && validOk && mnl==null && inl==null,"netheril 34: without psylink the vanilla reading joy giver gave "+got+"/30 jobs, "+neth+" on a Netheril scroll; IsValidBook scroll/novel/psylinker = "+validNl+"/"+validNovel+"/"+validOk+"; meditative giver "+(mnl==null?"none":mnl.targetA.Thing?.LabelShort)+", idle study "+(inl==null?"none":"job"));
    // 35 有心灵链接的人娱乐低时，原版 JobGiver_GetJoy 先给耐瑟卷轴（地图上还有普通书）；心灵熵 ≥ 90% 时照原版、不先给卷轴。
    var getJoy=new JobGiver_GetJoy();getJoy.ResolveReferences();
    var pj=NReader(10,1);pj.needs.joy.CurLevel=.1f;OnlyAllow(study);
    int first=0;for(int i=0;i<10;i++){var gj=getJoy.TryIssueJobPackage(pj,default(JobIssueParams)).Job;if(gj!=null && gj.targetA.Thing==study)first++;}
    pj.psychicEntropy.TryAddEntropy(pj.psychicEntropy.MaxEntropy*.95f,null,false,true);var hotTry=NetherilJoyFirst.Try(pj);AllowAll();
    Check(first==10 && hotTry==null && novel.Spawned,"netheril 35: with psylink and low joy the vanilla joy giver picks the Netheril scroll first "+first+"/10 times (a novel is also on the map); at 95% entropy it falls back to vanilla ("+(hotTry==null)+")");
   }finally{AllowAll();s.netherilIdleStudy=savedIdle;}
  }

  void NetherilScrollChecks(NetherilQuestState st){
   // 1 三种卷轴：原版书类、不消耗、卡苏斯之卷不能交易、不进原版随机书池。
   var defs=new[]{Netheril.StudyDef,Netheril.ArcaneDef,Netheril.KarsusDef}.Select(n=>Items.Thing(n)).ToList();
   var study=(Book)NSpawn(Netheril.MakeStudy(QualityCategory.Normal));
   Check(defs.All(d=>d!=null && typeof(Book).IsAssignableFrom(d.thingClass) && d.GetCompProperties<CompProperties_Book>()?.pickWeight==0f && d.GetCompProperties<CompProperties_UseEffect>()==null && !d.IsIngestible) && defs[2].tradeability==Tradeability.None && study.BookComp.Doers.OfType<BookOutcome_NetherilStudy>().Any() && Near(Netheril.QualityFactor(QualityCategory.Awful),.6f) && Near(Netheril.QualityFactor(QualityCategory.Legendary),1.6f),"netheril 1: three scroll defs are vanilla books with their outcome doers, not consumable, Karsus untradeable, quality speed 0.6..1.6");
   // 2 读的条件。
   var noLink=NReader(10,0);var dull=NReader(4,1);var ok=NReader(10,1);
   bool a=BookUtility.CanReadBook(study,noLink,out var r1),b=BookUtility.CanReadBook(study,dull,out var r2),c=BookUtility.CanReadBook(study,ok,out _);
   Check(!a && r1=="需要心灵链接" && !b && r2.Contains("智识") && c,"netheril 2: no psylink -> '"+r1+"', intellect 4 -> '"+r2+"', psylink + intellect 10 can read");
   // 3 研习卷每小时心灵熵 +8，巫妖 +4。
   var doer=study.BookComp.Doers.OfType<BookOutcome_NetherilStudy>().First();Netheril.TestRoll=1f;
   float e0=Entropy(ok);doer.ReadTicks(ok,Netheril.HourTicks);float e1=Entropy(ok);
   var lich=NReader(10,1);var lh=lich.health.AddHediff(Necro.Def<HediffDef>("Lich"));float l0=Entropy(lich);doer.ReadTicks(lich,Netheril.HourTicks);float l1=Entropy(lich);lich.health.RemoveHediff(lh);
   Check(Near(e1-e0,8f) && Near(l1-l0,4f),"netheril 3: one hour of the study scroll adds 8 entropy ("+(e1-e0)+"), a lich 4 ("+(l1-l0)+")");
   // 4 经验去向：有 VPE 进 VPE（桥接里查精确值）；没有 VPE 记研习度。都不碰精神力。
   float f0=ok.psychicEntropy.CurrentPsyfocus,sp0=st.StudyPoints(ok);doer.ReadTicks(ok,Netheril.HourTicks);
   Check(Near(ok.psychicEntropy.CurrentPsyfocus,f0) && (NetherilHooks.VPEActive || Near(st.StudyPoints(ok)-sp0,Netheril.StudyXP)),"netheril 4: study experience goes "+(NetherilHooks.VPEActive?"to VPE":"to Netheril study points (+"+(st.StudyPoints(ok)-sp0)+")")+", psyfocus unchanged");
   // 5 无 VPE：研习度到 100 心灵链接 +1（单元式）。
   var learner=NReader(10,1);st.studyPoints[learner.thingIDNumber]=95f;int lv=learner.GetPsylinkLevel();int gained=st.AddStudyPoints(learner,6f);
   Check(gained==1 && learner.GetPsylinkLevel()==lv+1,"netheril 5: study points crossing 100 raise the psylink "+lv+" -> "+learner.GetPsylinkLevel());
   // 6 1.52.0 补丁：没有每日上限，一天里读满 8 小时（48 点，超过旧的 40）照样一直加经验。
   var capped=NReader(10,1);float cp=st.StudyPoints(capped);doer.ReadTicks(capped,8*Netheril.HourTicks);float sx=doer.SessionXP(capped);doer.EndSession(capped);float x9=Netheril.GiveXP(capped,5f);
   Check(Near(sx,48f*doer.Q) && Near(x9,5f) && (NetherilHooks.VPEActive || Near(st.StudyPoints(capped)-cp,48f*doer.Q+5f)),"netheril 6: no daily cap - 8 hours give "+sx.ToString("0.#")+" experience (old cap was 40) and a further 5 still lands ("+x9+")");
   // 7 心灵熵超过 90% 自动停读；玩家强制阅读不停。
   var hot=NReader(10,1);var j=JobMaker.MakeJob(JobDefOf.Reading,study);hot.jobs.StartJob(j,JobCondition.InterruptForced);
   hot.psychicEntropy.TryAddEntropy(hot.psychicEntropy.MaxEntropy*.95f,null,false,true);string hd="job="+hot.CurJob?.def?.defName+" same="+(hot.CurJob==j)+" e="+hot.psychicEntropy.EntropyValue+"/"+hot.psychicEntropy.MaxEntropy;bool stopped=Netheril.StopIfHot(hot);
   var j2=JobMaker.MakeJob(JobDefOf.Reading,study);j2.playerForced=true;hot.jobs.StartJob(j2,JobCondition.InterruptForced);bool kept=!Netheril.StopIfHot(hot) && hot.CurJob==j2;hot.jobs.StopAll();
   Check(stopped && kept,"netheril 7: entropy above 90% ends reading ("+stopped+"), a player-forced read keeps going ("+kept+") "+hd);
   NetherilInsightChecks(study);
   // 8 残卷学派写进名字（存读档在 AfterLoad）。
   var arcane=(Book_Netheril)NSpawn(Netheril.MakeArcane("Evocation",QualityCategory.Normal));nArcaneId=arcane.ThingID;
   if(!arcane.LabelNoCount.Contains("秘术残卷（塑能）"))throw new Exception("arcane scroll label lacks the school: "+arcane.LabelNoCount);
   nReaderId=ok.thingIDNumber;nProgress=7.5f;arcane.Arcane.progress[nReaderId]=nProgress;
   // 9 残卷 24 点学会本学派、等级合规的法术（无 VPE：原版心灵法术；心灵链接等级不变）。
   var mage=NReader(10,1);var ench=(Book)NSpawn(Netheril.MakeArcane("Enchantment",QualityCategory.Normal));var ad=Netheril.Doer<BookOutcome_NetherilArcane>(ench);
   ad.progress[mage.thingIDNumber]=23f;int ml=mage.GetPsylinkLevel();ad.ReadTicks(mage,Netheril.HourTicks);
   var got=mage.abilities.abilities.Select(x=>x.def).Where(d=>d.IsPsycast).ToList();
   Check(ad.Learned(mage)==1 && got.Count==1 && got[0].level<=ml && NetherilSpells.VanillaSchool(got[0])=="Enchantment" && mage.GetPsylinkLevel()==ml && Near(ad.Progress(mage),0f),"netheril 9: 24 points teach an enchantment psycast of level <= "+ml+" ("+got.FirstOrDefault()?.defName+") without changing the psylink");
   // 10 同一读者同一卷学满 2 个后只剩余效：经验 +2、不涨熵、不涨进度。
   ad.learned[mage.thingIDNumber]=2;float me=Entropy(mage),mp=st.StudyPoints(mage),mpr=ad.Progress(mage);ad.ReadTicks(mage,Netheril.HourTicks);
   Check(Near(Entropy(mage),me) && Near(st.StudyPoints(mage)-mp,Netheril.ArcaneResidueXP) && Near(ad.Progress(mage),mpr),"netheril 10: after two spells the same scroll only gives residue study (+"+(st.StudyPoints(mage)-mp)+", no entropy)");
   // 11 等级不够：进度停在 24 并提示；等级够了再读 1 小时就结算。
   var low=NReader(10,1);var evo=(Book)NSpawn(Netheril.MakeArcane("Divination",QualityCategory.Normal));var ed=Netheril.Doer<BookOutcome_NetherilArcane>(evo);
   ed.progress[low.thingIDNumber]=23f;ed.ReadTicks(low,Netheril.HourTicks);bool stuck=ed.Learned(low)==0 && Near(ed.Progress(low),Netheril.ArcaneGoal);
   for(int i=0;i<5;i++)low.ChangePsylinkLevel(1,false);foreach(var x in low.abilities.abilities.ToList())low.abilities.RemoveAbility(x.def);ed.ReadTicks(low,Netheril.HourTicks);
   Check(stuck && ed.Learned(low)==1,"netheril 11: divination at psylink 1 stays at 24 ("+stuck+"), learns once the level is enough ("+ed.LastLearned+")");
   // 12 卡苏斯之卷唯一。
   var k1=NetherilQuest.MakeKarsus();var k2=NetherilQuest.MakeKarsus();
   var dup=ThingMaker.MakeThing(Items.Thing(Netheril.KarsusDef));GenSpawn.Spawn(dup,CellFinder.RandomClosewalkCellNear(map.Center,map,6),map);
   Check(k1!=null && k2==null && NetherilQuest.Karsus==k1 && dup.Destroyed,"netheril 12: Karsus scroll is world-unique (second make refused, stray copy vanishes)");
   // 13 读完一遍得一层回响，数值正确。
   NSpawn(k1);var kd=Netheril.Doer<BookOutcome_Karsus>(k1);var kr=NReader(10,1);st.karsusTicks[kr.thingIDNumber]=Netheril.KarsusPassTicks-Netheril.HourTicks;float ke=Entropy(kr);kd.ReadTicks(kr,Netheril.HourTicks);
   var echo=kr.health.hediffSet.hediffs.OfType<Hediff_KarsusEcho>().FirstOrDefault();var stage=echo?.CurStage;
   Check(st.Passes(kr)==1 && echo!=null && echo.Layers==1 && Near(stage.statFactors.First(m=>m.stat==StatDefOf.PsychicEntropyMax).value,1.05f) && Near(stage.statOffsets.First(m=>m.stat==StatDefOf.PsychicSensitivity).value,.05f) && Near(stage.statOffsets.First(m=>m.stat==StatDefOf.MentalBreakThreshold).value,.02f) && Near(Entropy(kr)-ke,6f),"netheril 13: finishing 40 hours gives one echo layer (entropy max x1.05, sensitivity +5%, break threshold +2%) and 6 entropy per hour");
   NetherilIdleChecks(st,study,k1);
   Netheril.TestRoll=null;
  }

  void NetherilQuestChecks(NetherilQuestState st){
   var research=DefDatabase<ResearchProjectDef>.GetNamed(NetherilQuest.ResearchName);
   // 25 有卷轴时塞伊袭击权重 ×1.25。
   NResetQuest(st);
   NetherilQuest.InvalidateScrollCache();float w0=Factions.HostileDef.RaidCommonalityFromPoints(1000f);
   var sc=NSpawn(Netheril.MakeStudy());NetherilQuest.InvalidateScrollCache();float w1=Factions.HostileDef.RaidCommonalityFromPoints(1000f);sc.DeSpawn();NetherilQuest.InvalidateScrollCache();
   Check(w0>0f && Near(w1/w0,1.25f),"netheril 25: a Netheril scroll on a home map makes Thayan raids x"+(w1/Math.Max(.0001f,w0)).ToString("0.##")+" as likely");
   // 14 使者触发条件两分支。
   NResetQuest(st);st.readHours=25f;
   var prog=Traverse.Create(Find.ResearchManager).Field("progress").GetValue<Dictionary<ResearchProjectDef,float>>();prog?.Remove(research);
   bool noResearch=!NetherilQuest.DailyCheck(st) && NetherilQuest.ScheduleBlocked(st).Contains("研究");
   Find.ResearchManager.FinishProject(research,false,null,false);st.readHours=10f;bool fewHours=!NetherilQuest.DailyCheck(st) && st.stage=="idle";
   st.readHours=25f;int now=Find.TickManager.TicksGame;bool scheduled=NetherilQuest.DailyCheck(st) && st.stage=="scheduled" && st.envoyTick>=now+3*GenDate.TicksPerDay && st.envoyTick<=now+10*GenDate.TicksPerDay;
   Check(noResearch && fewHours && scheduled,"netheril 14: envoy needs the research ("+noResearch+") and 20 reading hours ("+fewHours+"), then comes in 3-10 days ("+scheduled+")");
   // 15 选项 A：以人换卷。
   NResetQuest(st);var lord=NetherilQuest.SendEnvoy(map,false);var gift=NReader(10,4);
   string diag="lord="+(lord!=null)+" pending="+st.envoyPending+" blocked="+NetherilQuest.ABlocked(map);
   bool aOk=lord!=null && st.envoyPending && NetherilQuest.ABlocked(map)==null && NetherilQuest.ChooseA(gift);
   diag+=" chose="+aOk+" faction="+gift.Faction?.Name+" inLord="+(lord?.ownedPawns.Contains(gift))+" karsus="+(NetherilQuest.Karsus!=null)+" spawned="+NetherilQuest.Karsus?.Spawned+" status="+st.karsusStatus+" stage="+st.stage;
   var anyColonist=map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
   Check(aOk && gift.Faction==NetherilQuest.Larloch && !gift.Dead && lord.ownedPawns.Contains(gift) && NetherilQuest.Karsus!=null && NetherilQuest.Karsus.Spawned && st.karsusStatus=="player" && st.stage=="scroll" && !st.envoyPending && anyColonist?.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_CompanionSentAway"))!=null,"netheril 15: option A sends the colonist away alive with the envoy, gives the Karsus scroll and the sent-away thought ("+diag+")");
   // 16 选项 B：以命匣换卷，命匣消失、失匣巫妖、死后不复活。
   NResetQuest(st);lord=NetherilQuest.SendEnvoy(map,false);var lichP=NReader(10,1);var box=Rituals.Ascend(lichP);GenSpawn.Spawn(box,CellFinder.RandomClosewalkCellNear(lichP.Position,map,3),map);
   bool bOk=NetherilQuest.BBlocked(map)==null && NetherilQuest.ChooseB(lichP);bool keeps=Necro.Lich(lichP)?.Active==true && !box.Spawned && !box.Destroyed && st.hold.Contains(box);
   lichP.Kill(null);box.ProcessRevival();box.ScheduleRevival();var corpse=lichP.Corpse;corpse?.Destroy();
   Check(bOk && keeps && NetherilQuest.PhylacteryLost(lichP) && box.revivalTick<0 && !box.souls.Contains(lichP) && NetherilQuest.Karsus!=null && st.karsusStatus=="player","netheril 16: option B takes the phylactery off the map (lich keeps its power), adds LP_PhylacteryLost, and the lich no longer revives");
   st.hold.Remove(box);Necro.Registry?.boxes.Remove(box);box.Destroy();
   // 36 1.61.0 热修：从信件走一遍选项 B（按钮 → 下一页选巫妖 → 点巫妖）：命匣离开地图、失匣、卷轴到手、进入卷轴阶段；“使者马上再来”补救退回一次拒绝。
   NResetQuest(st);lord=NetherilQuest.SendEnvoy(map,false);var uiLich=NReader(10,1);var uiBox=Rituals.Ascend(uiLich);GenSpawn.Spawn(uiBox,CellFinder.RandomClosewalkCellNear(uiLich.Position,map,3),map);
   var envoyLetter=Find.LetterStack.LettersListForReading.OfType<Letter_LarlochEnvoy>().LastOrDefault();
   var bOpt=envoyLetter?.Choices.FirstOrDefault(o=>OptText(o)=="以命匣换卷");
   string uiDiag="letter="+(envoyLetter!=null)+" disabled="+bOpt?.disabled+" '"+bOpt?.disabledReason+"'";
   var page=bOpt?.link;var pick=page?.options.FirstOrDefault(o=>OptText(o).StartsWith(uiLich.LabelShort));bool hasBack=page?.options.Any(o=>OptText(o)=="返回")==true;
   uiDiag+=" page="+(page!=null)+" options="+page?.options.Count+" pick='"+(pick==null?"":OptText(pick))+"' back="+hasBack;
   pick?.action?.Invoke();
   bool bUi=pick!=null && hasBack && !uiBox.Spawned && !uiBox.Destroyed && st.hold.Contains(uiBox) && NetherilQuest.PhylacteryLost(uiLich) && NetherilQuest.PlayerHolds(NetherilQuest.Karsus) && st.stage=="scroll" && st.karsusStatus=="player" && !Find.LetterStack.LettersListForReading.Contains(envoyLetter);
   uiDiag+=" spawned="+uiBox.Spawned+" held="+st.hold.Contains(uiBox)+" lost="+NetherilQuest.PhylacteryLost(uiLich)+" karsus="+(NetherilQuest.Karsus!=null)+" stage="+st.stage+" letterLeft="+Find.LetterStack.LettersListForReading.Contains(envoyLetter);
   st.hold.Remove(uiBox);Necro.Registry?.boxes.Remove(uiBox);uiBox.Destroy();
   NResetQuest(st);st.stage="refused";st.refusals=2;st.envoyTick=Find.TickManager.TicksGame+59*GenDate.TicksPerDay;string recall=NetherilQuest.RecallEnvoy();bool recalled=recall==null && st.envoyPending && st.stage=="envoy" && st.refusals==1 && NetherilQuest.RecallEnvoy()!=null;
   uiDiag+=" recall='"+recall+"' recalled="+recalled;
   Check(bUi && recalled,"netheril 36: option B through the letter page takes the phylactery off the map, marks the lich LP_PhylacteryLost, gives the Karsus scroll and moves the quest to the scroll stage; recalling the envoy undoes one refusal ("+uiDiag+")");
   NResetQuest(st);
   // 18 差事超时 → 拒绝计数 +1。
   NResetQuest(st);st.stage="errand";st.errand=1;st.errandDeadline=Find.TickManager.TicksGame-1;NetherilQuest.ErrandTick(st);
   Check(st.refusals==1 && st.stage=="refused" && st.envoyTick>=Find.TickManager.TicksGame+59*GenDate.TicksPerDay,"netheril 18: an errand past its 30-day deadline counts as a refusal (refusals "+st.refusals+", envoy back in 60 days)");
   // 19 第三次拒绝 → 古墓站点。
   NResetQuest(st);st.refusals=2;NetherilQuest.Refuse();
   Check(st.refusals==3 && st.stage=="crypt" && st.cryptSite!=null && st.cryptSite.parts.Any(p=>p.def.defName=="LP_LarlochCrypt") && st.karsusStatus=="larloch" && Find.WorldObjects.Contains(st.cryptSite),"netheril 19: the third refusal reveals Larloch's crypt on the world map");
   // 24 卷轴被毁 → 待重现 → 站点出现。
   NResetQuest(st);var kk=NSpawn(NetherilQuest.MakeKarsus());st.karsusStatus="player";kk.Destroy();NetherilQuest.Tick(st);
   bool pending=st.karsusStatus=="lost" && st.reappearTick>=Find.TickManager.TicksGame+30*GenDate.TicksPerDay;
   st.larlochDefeated=true;st.reappearTick=Find.TickManager.TicksGame;NetherilQuest.Tick(st);
   Check(pending && st.karsusStatus=="site" && st.wreckSite!=null && st.wreckSite.parts.Any(p=>p.def.defName=="LP_NetherilWreck") && st.wreckDeadline>Find.TickManager.TicksGame,"netheril 24: a destroyed Karsus scroll goes missing for 30-60 days, then the sunken Netheril wreck appears");
   NResetQuest(st);
  }

  void NetherilAscensionChecks(NetherilQuestState st){
   NResetQuest(st);
   var cell=E2Open(2.5f,0f);var altar=(Building_NetherilAltar)ThingMaker.MakeThing(Items.Thing("LP_NetherilAltar"));altar.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(altar,cell,map);nMade.Add(altar);
   // 20 祭坛三项条件。
   var pr=NReader(10,6);st.karsusPasses[pr.thingIDNumber]=6;string few=NetherilAscension.PrincipalBlocked(pr);
   st.karsusPasses[pr.thingIDNumber]=7;string fine=NetherilAscension.PrincipalBlocked(pr);string noScroll=NetherilAscension.Blocked(altar);
   var weak=NReader(10,5);st.karsusPasses[weak.thingIDNumber]=9;string lowLevel=NetherilAscension.PrincipalBlocked(weak);
   var k=NSpawn(NetherilQuest.MakeKarsus(),altar.Position+IntVec3.North*3);st.karsusStatus="player";
   Check(few!=null && few.Contains("6 / 7") && fine==null && lowLevel!=null && lowLevel.Contains("心灵链接") && noScroll!=null && noScroll.Contains("卡苏斯之卷") && NetherilAscension.Blocked(altar)==null && NetherilAscension.Altars(map).Contains(altar),"netheril 20: ascension needs 7 reads ('"+few+"'), psylink 6 ('"+lowLevel+"') and an altar with the scroll on its map ('"+noScroll+"')");
   // 21 成功率公式含上下限。
   Check(Near(NetherilAscension.Chance(7,3,1f),.4f) && Near(NetherilAscension.Chance(9,5,1.25f),.64f) && Near(NetherilAscension.Chance(20,12,3f),.9f) && Near(NetherilAscension.Chance(7,3,.5f),.4f),"netheril 21: success chance 40% + 5%/read + 5%/co-celebrant + 2%/10% sensitivity, clamped to 40%-90%");
   // 22 成功：奥术半神、卷轴已解读、世界唯一、事件触发。
   var co=new List<Pawn>{NReader(10,1),NReader(10,1),NReader(10,1)};foreach(var p in co.Concat(new[]{pr}))p.psychicEntropy.OffsetPsyfocusDirectly(1f);
   Pawn granted=null;Action<Pawn> hook=p=>granted=p;NetherilAscension.ArcaneDemigodGranted+=hook;
   float maxBefore=pr.GetStatValue(StatDefOf.PsychicEntropyMax);bool won;
   try{won=NetherilAscension.Resolve(altar,pr,co,0f);}finally{NetherilAscension.ArcaneDemigodGranted-=hook;}
   pr.health.capacities.Notify_CapacityLevelsDirty();float maxAfter=pr.GetStatValue(StatDefOf.PsychicEntropyMax);
   Check(won && NetherilAscension.IsArcaneDemigod(pr) && granted==pr && st.demigod==pr && st.deciphered && Near(maxAfter/maxBefore,2f) && pr.needs.food==null && pr.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_Oracle"))!=null && co.All(p=>p.psychicEntropy.CurrentPsyfocus<.01f) && NetherilAscension.Blocked(altar).Contains("奥术半神"),"netheril 22: success makes an arcane demigod (entropy max x"+(maxAfter/maxBefore).ToString("0.##")+", no food, oracle), fires ArcaneDemigodGranted, deciphers the scroll and blocks a second ritual");
   pr.health.RemoveHediff(pr.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ArcaneDemigod")));st.demigod=null;st.deciphered=false;
   // 23 失败：不可复原的石像、魔网崩溃下心灵法术和法杖都用不了、卷轴遗失。
   var loser=NReader(10,6);st.karsusPasses[loser.thingIDNumber]=7;foreach(var p in co)p.psychicEntropy.OffsetPsyfocusDirectly(1f);
   var caster=NReader(10,1);caster.abilities.GainAbility(DefDatabase<AbilityDef>.GetNamed("Stun"));
   bool lost=!NetherilAscension.Resolve(altar,loser,co,.99f);
   var statue=map.listerThings.AllThings.OfType<PetrifiedStatue>().FirstOrDefault(x=>x.Pawn==loser);
   bool disabled=caster.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("Stun")).GizmoDisabled(out var why);
   bool staff=Arcana.CanFire(caster,Items.Thing("LP_FireStaff"),out var staffWhy);
   Check(lost && statue!=null && statue.permanent && statue.Restore()==null && !statue.Destroyed && NetherilAscension.WeaveCollapsed(map) && disabled && why==NetherilAscension.CollapseReason && !staff && staffWhy==NetherilAscension.CollapseReason && st.karsusStatus=="lost" && st.Passes(loser)==7,"netheril 23: failure leaves an unrestorable statue and a weave collapse ("+why+"; staff: "+staffWhy+"), the scroll goes missing, reads are kept");
   if(statue!=null){nMade.Add(statue);}
   NResetQuest(st);
  }

  // 17 差事一 → 差事二（存读档往返在 AfterLoad 里）。
  void NetherilErrandForSave(NetherilQuestState st){
   NResetQuest(st);NetherilQuest.SendEnvoy(map,false);NetherilQuest.ChooseC();var box=st.offeringBox as Building_LarlochOffering;
   bool started=st.stage=="errand" && st.errand==1 && box!=null && box.Spawned;
   var residue=ThingMaker.MakeThing(Items.Thing("LP_PlanarResidue"));residue.stackCount=NetherilQuest.ResidueNeed;GenSpawn.Spawn(residue,box.Position,map);
   var shard=ThingMaker.MakeThing(Items.Thing("LP_MiracleShard"));shard.stackCount=NetherilQuest.ShardNeed;GenSpawn.Spawn(shard,box.Position,map);
   bool done=box.TryComplete();
   bool reward=map.listerThings.ThingsOfDef(Items.Thing(Netheril.ArcaneDef)).Any(t=>Netheril.Doer<BookOutcome_NetherilArcane>(t)?.school=="Necromancy");
   Check(started && done && box.Destroyed && residue.Destroyed && shard.Destroyed && reward && st.stage=="errand" && st.errand==2 && st.errandDeadline>Find.TickManager.TicksGame+29*GenDate.TicksPerDay && (st.campSite!=null || NetherilQuest.Thay==null),"netheril 17: option C starts errand 1; 20 residue + 3 shards in the offering box finish it, a necromancy scroll arrives and errand 2 (red wizard camp) begins");
  }
  void PrepareNetherilSave(){var st=Netheril.State;if(st.envoy!=null && st.envoy.Destroyed)st.envoy=null;st.envoyLord=null;st.envoyPending=false;nErrand=st.errand;nDeadline=st.errandDeadline;nStage=st.stage;nCampSite=st.campSite!=null;}
  void CheckNetherilAfterLoad(){
   var st=Netheril.State;
   var arcane=map.listerThings.AllThings.FirstOrDefault(t=>t.ThingID==nArcaneId) as Book_Netheril;
   Check(arcane!=null && arcane.Arcane.school=="Evocation" && arcane.LabelNoCount.Contains("秘术残卷（塑能）") && arcane.Arcane.progress.TryGetValue(nReaderId,out var np) && Near(np,nProgress) && st.stage==nStage && st.errand==nErrand && nErrand==2 && st.errandDeadline==nDeadline && (st.campSite!=null)==nCampSite,"netheril 8+17: the arcane scroll keeps its school, name and per-reader progress, and the quest stays on errand 2 with its deadline and camp site after save/load");
   arcane?.Destroy();NResetQuest(st);
  }
 }
}
