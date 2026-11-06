using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.122.0 检查：Test.ps1 -Only SecondLife（二周目）、-Only SystemHost（系统宿主 + 改命符）；两组都有存读档部分。
 public partial class LichChecks {
  readonly List<Thing> slMade=new List<Thing>();
  static int slSavedId=-1,slSavedPast=-1,shSavedId=-1,shSavedTarget=-1,shSavedRewards=-1;static string shSavedItem;static HostTaskKind shSavedKind;
  static readonly string[] slBanned={"d20","D20","掷骰","检定","DC"};
  Pawn SlColonist(int level){
   var p=Person();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,6),map);slMade.Add(p);
   var h=Adventurer.TryAdd(p,1,true);if(h!=null){h.level=level;h.xp=Adventurer.XpTable[level-1];}
   return p;
  }
  static void SlStrip(Pawn p){foreach(var t in p.story.traits.allTraits.ToList())if(t.sourceGene==null)p.story.traits.RemoveTrait(t);}
  static Trait SlGive(Pawn p,string def,int degree=0){var t=new Trait(DefDatabase<TraitDef>.GetNamed(def),degree,false);p.story.traits.GainTrait(t);return t;}
  void SlWith(Action body){
   var s=LichMod.Settings;bool oAdv=s.adventurer,oSup=Adventurer.TestSuppressed;s.adventurer=true;Adventurer.TestSuppressed=false;
   try{body();}
   finally{s.adventurer=oAdv;Adventurer.TestSuppressed=oSup;SecondLife.TestMemory=null;SecondLife.TestKind=null;SystemHost.TestIsekai=null;SystemHost.State?.ClearNotifications();
    foreach(var t in slMade)if(t!=null && !t.Destroyed)t.Destroy();slMade.Clear();}
  }

  // ==================== 二周目 ====================
  void CheckSecondLife(){Rand.PushState(12201);try{SlWith(SlBody);}finally{Rand.PopState();}}
  void SlBody(){
   // 1. 定义与档位
   var d=SecondLife.Def;var hd=SystemHost.Def;
   Check(d!=null && TraitRarity.Tier(d,0)==2 && TraitRarity.Hex(d,0)=="#5B9BD5" && d.degreeDatas[0].label.Contains("二周目") && Mathf.Approximately(SecondLifeStartup.XmlSecond,.1f)
    && hd!=null && TraitRarity.Tier(hd,0)==3 && hd.degreeDatas[0].label.StartsWith("<color=#B070E0>") && Mathf.Approximately(SecondLifeStartup.XmlHost,.05f),
    "[second life] 二周目 is a blue Rare trait (commonality 0.1), 系统宿主 a purple Epic trait (0.05)");
   Check(!slBanned.Any(b=>d.degreeDatas[0].description.Contains(b) || hd.degreeDatas[0].description.Contains(b)),"[second life] descriptions contain no dice wording");
   // 2. 前世等级 8～14，重复查询不变；没有特性 = 0
   var p=SlColonist(3);var plain=SlColonist(3);
   SlGive(p,"LP_SecondLife");int past=SecondLife.Past(p);
   Check(past>=8 && past<=14 && SecondLife.Past(p)==past && SecondLife.Past(plain)==0,"[second life] past level "+past+" is in 8–14 and stable; pawns without the trait have none");
   var many=new List<int>();for(int i=0;i<12;i++){var q=SlColonist(1);SlGive(q,"LP_SecondLife");many.Add(SecondLife.Past(q));}
   Check(many.All(v=>v>=8 && v<=14) && many.Distinct().Count()>1,"[second life] past levels vary within 8–14: "+string.Join(",",many));
   // 3. 低于前世等级经验 ×2（在其他倍率之后），达到后 ×1，传奇等级不受影响
   var h=Adventurer.Get(p);
   float with=Adventurer.GainXP(p,10f);
   var t=p.story.traits.GetTrait(d);p.story.traits.RemoveTrait(t);float without=Adventurer.GainXP(p,10f);SlGive(p,"LP_SecondLife");
   Check(without>0f && Mathf.Abs(with/without-2f)<.001f && SecondLife.Past(p)==past,"[second life] below past level: adventurer XP ×2 ("+with.ToString("0.###")+" vs "+without.ToString("0.###")+"), past level kept after losing and regaining the trait");
   h.level=past;h.xp=Adventurer.XpTable[past-1];float at=Adventurer.GainXP(p,10f);
   h.level=25;h.legendary=true;float leg=SecondLife.XpFactor(p,h);h.legendary=false;
   Check(Mathf.Abs(at-without)<.001f && leg==1f && SecondLife.XpFactor(p,h)==1f,"[second life] at the past level XP is ×1 again; legendary levels are not affected");
   h.level=3;h.xp=Adventurer.XpTable[2];
   Check(SecondLife.TipLine(p).Contains("前世等级 "+past) && SecondLife.TipLine(p).Contains("×2") && p.story.traits.GetTrait(d).TipString(p).Contains("前世等级 "+past),"[second life] tooltip shows past level, current level and the ×2 state: "+SecondLife.TipLine(p));
   // 4. 前世记忆：研究进度 +30% 花费；没有研究改为热情；热情全满跳过
   var rm=Find.ResearchManager;var prev=rm.GetProject(null);
   var proj=DefDatabase<ResearchProjectDef>.AllDefsListForReading.FirstOrDefault(r=>r.knowledgeCategory==null && !r.IsFinished && r.CanStartNow && rm.GetProgress(r)<=0f);
   Check(proj!=null,"[second life] found an unstarted research project for the memory check");
   try{
    rm.SetCurrentProject(proj);float before=rm.GetProgress(proj);
    var text=SecondLife.Memory(p,0);float gained=rm.GetProgress(proj)-before;
    Check(text!=null && text.Contains(proj.LabelCap) && Mathf.Abs(gained-proj.Cost*.3f)<1f,"[second life] memory adds 30% of the current project's cost ("+gained.ToString("0")+" / "+proj.Cost+")");
    rm.StopProject(proj);
    int sum0=p.skills.skills.Sum(s=>(int)s.passion);var text2=SecondLife.Memory(p,0);int sum1=p.skills.skills.Sum(s=>(int)s.passion);
    Check(text2!=null && text2.Contains("热情") && sum1==sum0+1,"[second life] without research the memory raises one passion instead");
    foreach(var s in p.skills.skills)s.passion=Passion.Major;
    Check(SecondLife.Memory(p,1)==null,"[second life] all passions burning: the memory is skipped");
    foreach(var s in p.skills.skills)s.passion=Passion.None;
   }finally{rm.StopProject(proj);if(prev!=null)rm.SetCurrentProject(prev);}
   // 5. 升级时触发：强制记忆 + 走到前世等级发信、之后不再触发
   h.level=past-1;h.xp=Legend.XpFor(past)-1f;SecondLife.TestMemory=true;SecondLife.TestKind=1;
   int letters=Find.LetterStack.LettersListForReading.Count;int ps=p.skills.skills.Sum(s=>(int)s.passion);
   Adventurer.GainXP(p,1f,false); // ×2 → 刚好到前世等级
   Check(h.level==past && p.skills.skills.Sum(s=>(int)s.passion)==ps+1 && SecondLife.State.done.Contains(p.thingIDNumber) && Find.LetterStack.LettersListForReading.Skip(letters).Any(l=>l.Label.ToString().Contains("前世的路走完了")),
    "[second life] levelling up to the past level fires the forced memory and the 前世的路走完了 letter");
   h.xp=Legend.XpFor(past+1)-1f;SecondLife.TestMemory=true;SecondLife.TestKind=1;ps=p.skills.skills.Sum(s=>(int)s.passion);
   Adventurer.GainXP(p,1f,false);
   Check(h.level==past+1 && p.skills.skills.Sum(s=>(int)s.passion)==ps && SecondLife.XpFactor(p,h)==1f,"[second life] above the past level: no more memories, XP ×1");
   SecondLife.TestMemory=null;SecondLife.TestKind=null;
   // 6. 冒险者系统关闭：只显示不生效
   LichMod.Settings.adventurer=false;
   Check(SecondLife.XpFactor(plain,Adventurer.Get(plain))==1f && Adventurer.GainXP(p,10f)==0f && SecondLife.TipLine(p).Contains("不生效"),"[second life] adventurer system off: the trait does nothing and says so");
   LichMod.Settings.adventurer=true;
  }
  void PrepareSecondLifeSave(){
   var s=LichMod.Settings;bool oSup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=false;
   try{
    var p=Person();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,6),map);Adventurer.TryAdd(p,1,true);
    SlGive(p,"LP_SecondLife");slSavedId=p.thingIDNumber;slSavedPast=SecondLife.Past(p);
    Check(slSavedPast>=8,"[second life] before saving: past level "+slSavedPast);
   }finally{Adventurer.TestSuppressed=oSup;}
  }
  void CheckSecondLifeAfterLoad(){
   var p=map.mapPawns.AllPawns.FirstOrDefault(x=>x.thingIDNumber==slSavedId);
   Check(p!=null && SecondLife.Has(p) && SecondLife.Past(p)==slSavedPast,"[second life] after loading: past level still "+slSavedPast);
   p?.Destroy();
  }

  // ==================== 系统宿主 + 改命符 ====================
  void CheckSystemHost(){Rand.PushState(12202);try{SlWith(()=>{ShTasks();ShRewards();ShFail();ShLost();ShFate();});}finally{Rand.PopState();}}
  Pawn ShHost(int level){var p=SlColonist(level);SlStrip(p);SlGive(p,"LP_SystemHost");return p;}
  static RecordDef Rec(string n)=>DefDatabase<RecordDef>.GetNamed(n);

  void ShTasks(){
   Check(SystemHost.CharmDef!=null && SystemHost.CharmDef.BaseMarketValue==2000f && SystemHost.CharmDef.tradeability==Tradeability.None && SystemHost.CharmDef.generateCommonality==0f && SystemHost.PenaltyDef!=null
    && ContentFinder<Texture2D>.Get("LP/SystemHost/SystemPanel",false)!=null && ContentFinder<Texture2D>.Get("LP/SystemHost/FateCharm",false)!=null,
    "[system host] 改命符 (2000 silver, not tradeable, not generated), 系统惩罚 and both textures load");
   var p=ShHost(6);var st=SystemHost.State;int now=Find.TickManager.TicksGame;
   st.Step();var e=st.Lookup(p);
   Check(e!=null && e.offers.Count==3 && e.offers.Select(o=>o.kind).Distinct().Count()==3 && e.active==null && e.nextOfferTick==now+SystemHost.Period,"[system host] first release: 3 different tasks, next release in 7 days");
   var want=new Dictionary<HostTaskKind,int>{{HostTaskKind.Kill,6},{HostTaskKind.Craft,16},{HostTaskKind.Tend,6},{HostTaskKind.Harvest,21},{HostTaskKind.Research,2400},{HostTaskKind.Capture,1}};
   Check(e.offers.All(o=>o.target==want[o.kind]) && Enum.GetValues(typeof(HostTaskKind)).Cast<HostTaskKind>().All(k=>SystemHost.TargetFor(k,6)==want[k]),"[system host] targets at level 6: kill 6, craft 16, tend 6, harvest 21, research 2400, capture 1");
   var gizmos=p.GetGizmos().OfType<Command_Action>().ToList();
   Check(gizmos.Any(g=>g.defaultLabel.StartsWith("系统面板")) && !SlColonist(3).GetGizmos().OfType<Command_Action>().Any(g=>g.defaultLabel.StartsWith("系统面板")),"[system host] hosts get the 系统面板 button, others don't");
   var w=new SystemHostWindow(p);Check(w.InitialSize==new Vector2(840f,710f) && w.forcePause && !w.closeOnClickedOutside,"[system host] window: 840×710, pauses, not closed by clicking outside");
   // 接受（只能接受当前列表里的一个）
   var kill=new HostTask{kind=HostTaskKind.Kill,target=SystemHost.TargetFor(HostTaskKind.Kill,6)};e.offers[0]=kill;
   Check(!SystemHost.Accept(p,new HostTask{kind=HostTaskKind.Kill,target=1},out _) && SystemHost.Accept(p,kill,out _) && e.active==kill && e.offers.Count==0 && kill.deadline==now+SystemHost.Deadline
    && !SystemHost.Accept(p,kill,out _),"[system host] accepting: only a listed task, one at a time, 7-day deadline, other offers dropped");
   // 记录差值推进
   p.records.AddTo(Rec("Kills"),kill.target-1);st.Step();
   Check(e.active==kill && Mathf.Approximately(SystemHost.Done(p,kill),kill.target-1) && e.rewards.Count==0,"[system host] progress counts the Kills record delta ("+SystemHost.Done(p,kill)+" / "+kill.target+")");
   p.records.Increment(Rec("Kills"));st.Step();
   Check(e.active==null && e.completed==1 && e.rewards.Count==1 && e.offers.Count==0,"[system host] reaching the target completes the task and queues one reward; no new offers before the 7-day mark");
   // 其他类型也按记录推进
   foreach(var k in new[]{HostTaskKind.Craft,HostTaskKind.Tend,HostTaskKind.Harvest,HostTaskKind.Research,HostTaskKind.Capture}){
    var t=new HostTask{kind=k,target=SystemHost.TargetFor(k,6)};e.offers.Add(t);SystemHost.Accept(p,t,out _);
    p.records.AddTo(Rec(SystemHost.RecordOf(k)),t.target);st.Step();
    Check(e.active==null && e.completed==1+(int)k,"[system host] "+t.Label+" completes from the "+SystemHost.RecordOf(k)+" record");
   }
   // 到 7 天发布新一批
   e.nextOfferTick=now-1;st.Step();
   Check(e.offers.Count==3,"[system host] after 7 days a new batch of 3 tasks is released");
   e.rewards.Clear();e.offers.Clear();
  }

  void ShRewards(){
   var p=ShHost(5);var st=SystemHost.State;var e=st.For(p);var h=Adventurer.Get(p);
   Check(!SystemHost.UsesIsekaiPool==!SystemHost.IsekaiItems.Any(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)!=null),"[system host] random item pool follows whether Isekai is loaded (Isekai: "+SystemHost.UsesIsekaiPool+")");
   SystemHost.TestIsekai=false;
   for(int i=0;i<3;i++)e.rewards.Add(SystemHost.RollReward(i+1,"测试"));
   var items=Enumerable.Range(0,30).Select(i=>SystemHost.RollReward(9,"x")).ToList();
   Check(items.All(r=>SystemHost.DndItems.Any(kv=>kv.Key==r.item && kv.Value==r.count)) && items.Select(r=>r.item).Distinct().Count()>=3,"[system host] without Isekai the random item comes from the DND pool ("+string.Join(",",items.Select(r=>r.item).Distinct())+")");
   // 0：+1 属性点
   int pts=h.AvailablePoints;var r0=e.rewards[0];
   Check(!SystemHost.Claim(p,e.rewards[1],0,out _) && SystemHost.Claim(p,r0,0,out _) && h.AvailablePoints==pts+1 && h.asiLeft>=1 && e.rewards.Count==2 && e.claimed==1,"[system host] stat reward: +1 adventurer point (only the first queued reward can be claimed)");
   Check(!SystemHost.Claim(p,r0,0,out _) && !SystemHost.Claim(p,r0,2,out _) && h.AvailablePoints==pts+1,"[system host] the same reward cannot be claimed twice");
   Check(h.SpendPoints(h.scores.Select((v,i)=>new{v,i}).First(x=>h.CanRaise(x.i,1)).i,1) && h.AvailablePoints==pts,"[system host] the point is spent in the adventurer tab like a level-up point");
   // 1：改命符进背包
   int c0=FateRewrite.CharmCount(p);
   Check(SystemHost.Claim(p,e.rewards[0],1,out _) && FateRewrite.CharmCount(p)==c0+1 && e.claimed==2,"[system host] charm reward: one 改命符 in the inventory");
   // 2：随机物品
   var r2=e.rewards[0];var def=DefDatabase<ThingDef>.GetNamed(r2.item);int n0=p.inventory.innerContainer.TotalStackCountOfDef(def);
   Check(SystemHost.Claim(p,r2,2,out _) && p.inventory.innerContainer.TotalStackCountOfDef(def)==n0+r2.count && e.rewards.Count==0 && e.claimed==3 && e.history.Count>=3,"[system host] item reward: "+def.label+" ×"+r2.count+" in the inventory; history recorded");
   // 没有冒险者等级：属性项不可领
   e.rewards.Add(SystemHost.RollReward(4,"测试"));var hd=Adventurer.Get(p);p.health.RemoveHediff(hd);
   Check(!SystemHost.Claim(p,e.rewards[0],0,out var err) && err.Contains("冒险者") && e.rewards.Count==1,"[system host] without an adventurer level the stat reward is refused and the reward kept");
   e.rewards.Clear();
  }

  void ShFail(){
   var p=ShHost(4);var st=SystemHost.State;var e=st.For(p);
   float ws=p.GetStatValue(StatDefOf.WorkSpeedGlobal,true,-1);
   var t=new HostTask{kind=HostTaskKind.Harvest,target=19};e.offers.Add(t);SystemHost.Accept(p,t,out _);t.deadline=Find.TickManager.TicksGame-1;st.Step();
   var pen=p.health.hediffSet.GetFirstHediffOfDef(SystemHost.PenaltyDef);
   float ws2=p.GetStatValue(StatDefOf.WorkSpeedGlobal,true,-1);
   Check(e.active==null && e.failed==1 && pen!=null && pen.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear==SystemHost.PenaltyTicks && Mathf.Abs(ws2/ws-.85f)<.01f && e.rewards.Count==0,
    "[system host] timeout: 3-day 系统惩罚, global work speed ×0.85 ("+ws.ToString("0.00")+" → "+ws2.ToString("0.00")+"), no reward");
   p.health.RemoveHediff(pen);
   var t2=new HostTask{kind=HostTaskKind.Tend,target=6};e.offers.Add(t2);SystemHost.Accept(p,t2,out _);
   Check(SystemHost.Abandon(p) && e.active==null && e.abandoned==1 && p.health.hediffSet.GetFirstHediffOfDef(SystemHost.PenaltyDef)==null,"[system host] abandoning a task has no penalty");
  }

  void ShLost(){
   var p=ShHost(4);var st=SystemHost.State;var e=st.For(p);
   var t=new HostTask{kind=HostTaskKind.Craft,target=14};e.offers.Add(t);SystemHost.Accept(p,t,out _);e.rewards.Add(SystemHost.RollReward(1,"测试"));
   p.story.traits.RemoveTrait(p.story.traits.GetTrait(SystemHost.Def));e.nextOfferTick=0;st.Step();
   Check(e.active==null && e.offers.Count==0 && e.rewards.Count==1 && p.health.hediffSet.GetFirstHediffOfDef(SystemHost.PenaltyDef)==null && p.GetGizmos().OfType<Command_Action>().Any(g=>g.defaultLabel.StartsWith("系统面板")),
    "[system host] losing the trait: no more tasks (the active one is void, no penalty), pending rewards stay and the panel button remains");
   Check(SystemHost.Claim(p,e.rewards[0],1,out _) && e.rewards.Count==0 && !p.GetGizmos().OfType<Command_Action>().Any(g=>g.defaultLabel.StartsWith("系统面板")),"[system host] the pending reward is still claimable; afterwards the button is gone");
  }

  void ShFate(){
   var p=SlColonist(4);SlStrip(p);
   var fleet=SlGive(p,"LP_Fleetfoot");var spell=SlGive(p,"LP_Spellblood");
   var c=FateRewrite.Candidates(p,fleet);
   bool sameTier=c.All(kv=>FateRewrite.TierOf(kv.Key,kv.Value)==2 && FateRewrite.MarkOf(kv.Key,kv.Value)==null);
   Check(c.Count>5 && sameTier && c.Any(kv=>kv.Key.defName=="LP_Artisan") && !c.Any(kv=>kv.Key.defName=="LP_Prodigy"),"[fate charm] candidates for a Rare trait are all Rare and not cursed ("+c.Count+"), e.g. 巧匠之手 yes, Epic 天才 no");
   Check(!c.Any(kv=>kv.Key.defName=="LP_SpellWarded") && !c.Any(kv=>kv.Key.defName=="LP_Giantbane" || FateRewrite.MarkOf(kv.Key,kv.Value)=="Deed"),"[fate charm] conflicting (法术护体 vs 法术之血) and deed traits are excluded");
   Check(!c.Any(kv=>FateRewrite.Story.Contains(kv.Key.defName) || kv.Key.defName.StartsWith("Isekai_") || kv.Key.defName.StartsWith("IKM_")) && !DefDatabase<TraitDef>.AllDefs.Any(d=>d.defName=="LP_SystemHost" && c.Any(kv=>kv.Key==d)),"[fate charm] story traits, 系统宿主 and Isekai / IKM traits never appear on the right");
   // 改写 + 消耗
   var charm=ThingMaker.MakeThing(SystemHost.CharmDef);charm.stackCount=2;p.inventory.innerContainer.TryAdd(charm,true);charm=FateRewrite.CharmIn(p);
   var artisan=DefDatabase<TraitDef>.GetNamed("LP_Artisan");var prodigy=DefDatabase<TraitDef>.GetNamed("LP_Prodigy");
   Check(!FateRewrite.Apply(p,fleet,prodigy,0,charm,out _) && p.story.traits.HasTrait(fleet.def) && FateRewrite.CharmCount(p)==2,"[fate charm] a different tier cannot be chosen (nothing consumed)");
   int letters=Find.LetterStack.LettersListForReading.Count;
   Check(FateRewrite.Apply(p,fleet,artisan,0,charm,out _) && p.story.traits.HasTrait(artisan) && !p.story.traits.HasTrait(fleet.def) && FateRewrite.CharmCount(p)==1 && Find.LetterStack.LettersListForReading.Count>letters,"[fate charm] same-tier rewrite 疾风之足 → 巧匠之手 works, uses 1 charm and sends a letter");
   var art=p.story.traits.GetTrait(artisan);
   Check(!FateRewrite.Candidates(p,spell).Any(kv=>kv.Key==artisan) && !FateRewrite.Allowed(p,spell,artisan,0),"[fate charm] traits the pawn already has are excluded");
   // 诅咒只换诅咒
   var doomed=SlGive(p,"LP_Doomed");var cc=FateRewrite.Candidates(p,doomed);
   Check(cc.Count>0 && cc.All(kv=>FateRewrite.MarkOf(kv.Key,kv.Value)=="Curse" && FateRewrite.TierOf(kv.Key,kv.Value)==2) && cc.Any(kv=>kv.Key.defName=="LP_Hollow") && !FateRewrite.Candidates(p,art).Any(kv=>FateRewrite.MarkOf(kv.Key,kv.Value)=="Curse"),"[fate charm] curses only swap with curses of the same tier, normal traits never with curses");
   // 事迹、基因、剧情、宿主本身不可改
   Trait Loose(string n)=>new Trait(DefDatabase<TraitDef>.GetNamed(n),0,false);var deed=Loose("LP_DeedDragonslayer");var host=Loose("LP_SystemHost");var dest=Loose("LP_Prophesied");
   Check(FateRewrite.Locked(p,deed)!=null && FateRewrite.Candidates(p,deed).Count==0 && FateRewrite.Locked(p,host)!=null && FateRewrite.Locked(p,dest)!=null && !FateRewrite.Apply(p,deed,DefDatabase<TraitDef>.GetNamed("LP_Giantbane"),0,charm,out _),"[fate charm] deeds, 系统宿主 and 天命 are locked");
   var gene=p.genes?.GenesListForReading.FirstOrDefault();
   if(gene!=null){var gt=new Trait(DefDatabase<TraitDef>.GetNamed("LP_Hardy"),0,false){sourceGene=gene};p.story.traits.GainTrait(gt);
    Check(FateRewrite.Locked(p,gt)!=null && FateRewrite.Locked(p,gt).Contains("基因") && FateRewrite.Candidates(p,gt).Count==0,"[fate charm] gene-given traits are locked");p.story.traits.RemoveTrait(gt);}
   // 光谱：按程度各算档位
   SlStrip(p);var stance=SlGive(p,"LP_DragonStance",1);var sc=FateRewrite.Candidates(p,stance);
   Check(TraitRarity.Tier(stance.def,1)==1 && !sc.Any(kv=>kv.Key==stance.def) && sc.All(kv=>FateRewrite.TierOf(kv.Key,kv.Value)==1),"[fate charm] spectrum degrees count separately: 屠龙志士 (Uncommon) can't become 惧龙 (Common)");
   SlStrip(p);var ind=DefDatabase<TraitDef>.GetNamedSilentFail("Industriousness");
   if(ind!=null){var it=SlGive(p,"Industriousness",1);var ic=FateRewrite.Candidates(p,it);
    Check(ic.Any(kv=>kv.Key==ind && kv.Value==2) && !ic.Any(kv=>kv.Key==ind && kv.Value==1) && ic.All(kv=>FateRewrite.TierOf(kv.Key,kv.Value)==0),"[fate charm] vanilla traits count as Common; a spectrum trait can move to another degree of itself");}
   // 用完最后一个改命符：物品消失
   SlStrip(p);var a=SlGive(p,"LP_Artisan");
   Check(FateRewrite.Apply(p,a,DefDatabase<TraitDef>.GetNamed("LP_Hardy"),0,charm,out _) && charm.Destroyed && FateRewrite.CharmCount(p)==0 && FateRewrite.CharmIn(p)==null && !p.GetGizmos().OfType<Command_Action>().Any(g=>g.defaultLabel.StartsWith("改命符")),"[fate charm] the last charm is consumed and the inventory button disappears");
   // 地上的改命符有自己的按钮；窗口能打开
   var ground=ThingMaker.MakeThing(SystemHost.CharmDef);GenSpawn.Spawn(ground,p.Position,map);slMade.Add(ground);
   Check(ground.GetGizmos().OfType<Command_Action>().Any(g=>g.defaultLabel=="使用改命符") && FateRewrite.Targets(ground,null).Contains(p),"[fate charm] a charm on the ground has a 使用改命符 button targeting colonists on the map");
  }

  void PrepareSystemHostSave(){
   bool oSup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=false;
   try{
    var p=Person();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,6),map);Adventurer.TryAdd(p,1,true);SlStrip(p);SlGive(p,"LP_SystemHost");
    var st=SystemHost.State;var e=st.For(p);st.Step();
    var t=e.offers[0];SystemHost.Accept(p,t,out _);SystemHost.TestIsekai=false;e.rewards.Add(SystemHost.RollReward(1,"存档测试"));SystemHost.TestIsekai=null;
    shSavedId=p.thingIDNumber;shSavedKind=t.kind;shSavedTarget=t.target;shSavedRewards=e.rewards.Count;shSavedItem=e.rewards[0].item;
    Check(e.active!=null && shSavedRewards==1,"[system host] before saving: one active task ("+t.Label+") and one pending reward");
   }finally{Adventurer.TestSuppressed=oSup;SystemHost.State?.ClearNotifications();}
  }
  void CheckSystemHostAfterLoad(){
   var p=map.mapPawns.AllPawns.FirstOrDefault(x=>x.thingIDNumber==shSavedId);var e=SystemHost.State?.Lookup(p);
   Check(p!=null && e?.active!=null && e.active.kind==shSavedKind && e.active.target==shSavedTarget && e.rewards.Count==shSavedRewards && e.rewards[0].item==shSavedItem && e.history.Count>0,
    "[system host] after loading: active task, pending reward ("+shSavedItem+") and history are kept");
   Check(SystemHost.Claim(p,e.rewards[0],1,out _) && FateRewrite.CharmCount(p)==1,"[system host] after loading the reward is still claimable");
   p.Destroy();
  }
 }
}
