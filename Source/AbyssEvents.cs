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
 // 1.54.0 深渊事件（规格第七节）：深渊低语、魅魔来访、深渊入侵（只由薄弱度触发）、纳巴苏猎魂；以及死亡时的各种深渊后果。
 public static class AbyssEvents {
  public const float RiseChance=.5f,WizardBookChance=.05f,WhisperTaint=.05f,BurnMood=3f;
  public const int RiseTicks=GenDate.TicksPerHour*6,FactionGoodwill=10,DawnHour=5,HuntHour=22,HuntBaseLevel=9;
  public static float? TestRoll;
  static float Roll=>TestRoll??Rand.Value;

  // ---------- 死亡 ----------
  public static void OnDeath(Pawn victim,DamageInfo? dinfo,Map map){
   var killer=dinfo?.Instigator as Pawn;
   if(victim.RaceProps.Humanlike && AbyssBinding.PlayerPawns.Any(Outsiders.Bound))AbyssBinding.OnMasterDied(victim);
   if(killer!=null && AbyssRaces.Is(killer,AbyssRaces.Nabassu))AbyssRaces.Devour(killer,victim);
   if(killer!=null && victim.RaceProps.Humanlike && Items.Worn(killer,"LP_GorgedFang")!=null){var d=Items.Hediff("LP_FangGorged");var h=d==null?null:killer.health.hediffSet.GetFirstHediffOfDef(d);if(h==null)Items.Give(killer,"LP_FangGorged");else h.Severity=Mathf.Min(10f,h.Severity+1f);}
   if(Items.Has(victim,"LP_FangGorged"))Items.Remove(victim,"LP_FangGorged");
   var corpse=victim.Corpse;var st=AbyssState.Get;
   if(Items.Has(victim,"LP_SoulMortgaged") && corpse!=null){Fiends.Defer(()=>{if(corpse.Spawned){var c=corpse.Position;var m=corpse.Map;Outsiders.DropBelongings(victim,c,m);for(int i=0;i<5;i++)FleckMaker.ThrowSmoke(c.ToVector3Shifted(),m,2f);corpse.Destroy();Messages.Message(victim.LabelShort+"抵押给深渊的灵魂被收走了，尸体化作了黑烟。",new TargetInfo(c,m),MessageTypeDefOf.NegativeEvent,false);}});return;}
   if(st!=null && corpse!=null && victim.RaceProps.Humanlike && !Outsiders.Is(victim) && AbyssTaint.Severity(victim)>=AbyssTaint.Fallen && Roll<RiseChance)st.pending.Add(new AbyssPending{corpse=corpse,tick=Find.TickManager.TicksGame+RiseTicks,reason="taint"});
   if(victim.kindDef?.defName=="LP_FH_RedWizard" && AbyssItems.Demonomicon!=null && map!=null && Roll<WizardBookChance)GenPlace.TryPlaceThing(ThingMaker.MakeThing(AbyssItems.Demonomicon),victim.PositionHeld,map,ThingPlaceMode.Near);
   if(dinfo?.Def==DamageDefOf.ExecutionCut && map!=null){var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_BarlguraBloodthirst");if(th!=null)foreach(var b in map.mapPawns.FreeColonistsSpawned.Where(x=>AbyssRaces.Is(x,AbyssRaces.Barlgura)))b.needs?.mood?.thoughts?.memories?.TryGainMemory(th);}
  }
  public static int ProcessPending(AbyssState st,int now){
   int n=0;
   for(int i=st.pending.Count-1;i>=0;i--){
    var p=st.pending[i];if(now<p.tick)continue;st.pending.RemoveAt(i);
    if(p.corpse==null || p.corpse.Destroyed || !p.corpse.Spawned)continue;
    if(Rise(p.corpse,p.reason)!=null)n++;
   }
   return n;
  }
  // 尸体起身成敌对劣魔。
  public static Pawn Rise(Corpse c,string reason){
   var map=c.Map;var cell=c.Position;var who=c.InnerPawn?.LabelShort;var f=AbyssRaces.Horde();
   var d=AbyssRaces.Make("LP_AH_Dretch",f,map.Tile);if(d==null)return null;
   c.Destroy();GenSpawn.Spawn(d,cell,map);for(int i=0;i<5;i++)FleckMaker.ThrowSmoke(cell.ToVector3Shifted(),map,2f);
   if(f!=null)LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false,false,false),map,new List<Pawn>{d});
   Find.LetterStack.ReceiveLetter("怨魂起身",who+(reason=="devour"?"被吞噬后剩下的残躯":"的尸体")+"抽搐着站了起来：它的灵魂化作了怨魂，成了一只劣魔。",LetterDefOf.ThreatSmall,d);
   return d;
  }

  // ---------- 纳巴苏猎魂 ----------
  public static int HoursUntil(Map map,int hour){int h=GenLocalDate.HourOfDay(map);return (hour-h+24)%24;}
  public static Pawn SpawnHunter(Map map,AbyssState st){
   if(map==null || !Find.Maps.Contains(map))return null;var f=AbyssRaces.Horde();if(f==null)return null;
   if(!RCellFinder.TryFindRandomPawnEntryCell(out var cell,map,CellFinder.EdgeRoadChance_Hostile))return null;
   var p=AbyssRaces.Make("LP_AH_Nabassu",f,map.Tile);if(p==null)return null;
   AbyssBinding.SetLevel(p,HuntBaseLevel+AdventurerEnemies.ColonyMaxLevel()/4,false);
   if(st.hunterLayers>0)AbyssRaces.AddGorge(p,st.hunterLayers);
   GenSpawn.Spawn(p,cell,map);
   LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false,false,false),map,new List<Pawn>{p});
   st.hunters.Add(p);
   Find.LetterStack.ReceiveLetter("猎魂者","一只纳巴苏趁夜色闯进了地图：灰黑的石皮、蝠翼、长獠牙。它专挑落单的人下手，被它杀死的人灵魂会被吞掉，再也无法复活。"+(st.hunterLayers>0?"\n\n它就是上次逃走的那一只，身上还带着 "+st.hunterLayers+" 层饱魂。":"")+"\n\n天亮时它还活着就会飞走。",LetterDefOf.ThreatBig,p);
   return p;
  }
  public static void TickHunters(AbyssState st,int now){
   if(st.huntTick>=0 && now>=st.huntTick){var m=st.huntMap;st.huntTick=-1;st.huntMap=null;SpawnHunter(m,st);}
   for(int i=st.hunters.Count-1;i>=0;i--){
    var h=st.hunters[i];
    if(h==null || h.Destroyed){st.hunters.RemoveAt(i);continue;}
    if(h.Dead){st.hunterLayers=0;st.hunters.RemoveAt(i);continue;}
    if(!h.Spawned || h.Downed)continue;
    int hour=GenLocalDate.HourOfDay(h.Map);if(hour<DawnHour || hour>=DawnHour+2)continue;
    Flee(h,st);st.hunters.RemoveAt(i);
   }
  }
  public static void Flee(Pawn h,AbyssState st){
   st.hunterLayers=Mathf.Min((int)AbyssRaces.DevourLayerMax,AbyssRaces.Gorge(h));
   var map=h.Map;var cell=h.Position;for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(cell.ToVector3Shifted(),map,2f);
   Messages.Message("天亮了，纳巴苏振翅飞走了"+(st.hunterLayers>0?"，带走了 "+st.hunterLayers+" 层饱魂":"")+"。",new TargetInfo(cell,map),MessageTypeDefOf.NeutralEvent,false);
   h.GetLord()?.RemovePawn(h);h.Destroy(DestroyMode.Vanish);
  }

  // ---------- 魅魔来访：揭晓 ----------
  public static void RevealSuccubus(Pawn p,string reason){
   if(p==null || p.Dead || !Outsiders.Disguised(p))return;
   Items.Remove(p,"LP_SuccubusDisguise");
   AbyssRaces.ConvertTo(p,AbyssRaces.SuccubusXeno);
   if(p.Spawned)for(int i=0;i<5;i++)FleckMaker.ThrowSmoke(p.DrawPos,p.Map,1.5f);
   var l=(ChoiceLetter_SuccubusReveal)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_SuccubusRevealLetter"));
   l.Label=(p.gender==Gender.Male?"梦魔":"魅魔")+"现形："+p.LabelShort;l.succubus=p;l.lookTargets=new LookTargets(p);
   l.Text=reason+"：前些日子加入的"+p.LabelShort+"根本不是凡人——角、蝠翼和细尾从幻象里显了出来，是一只"+(p.gender==Gender.Male?"梦魔":"魅魔")+"。最近那些“噩梦缠身”的夜晚，原来是它在吸取同床人的精力。\n\n要怎么处置它？";
   Find.LetterStack.ReceiveLetter(l);
  }
  public static Pawn BestSocial(Map map)=>map?.mapPawns.FreeColonistsSpawned.Where(c=>!Outsiders.Is(c) && !c.Downed).OrderByDescending(c=>c.skills?.GetSkill(SkillDefOf.Social)?.Level??0).FirstOrDefault();
  public static void Expel(Pawn p){
   var close=p.MapHeld?.mapPawns.FreeColonistsSpawned.Where(c=>c!=p && c.relations!=null).OrderByDescending(c=>c.relations.OpinionOf(p)).FirstOrDefault();
   var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_AbandonedBySuccubus");if(close!=null && th!=null)close.needs?.mood?.thoughts?.memories?.TryGainMemory(th,p);
   var f=AbyssRaces.Horde();if(f!=null)p.SetFaction(f);
   if(p.Spawned){var map=p.Map;var c=p.Position;for(int i=0;i<5;i++)FleckMaker.ThrowSmoke(c.ToVector3Shifted(),map,2f);p.DeSpawn();}
   if(!Find.WorldPawns.Contains(p))Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.Discard);
  }
 }

 // ---------- 事件 ----------
 // 深渊低语：一名殖民者（优先腐化最高的）梦见深渊，身边出现一本恶魔学典籍。
 public class IncidentWorker_AbyssWhisper : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms){
   if(!AbyssSettings.Enabled || !(parms.target is Map map) || !map.IsPlayerHome || AbyssItems.Demonomicon==null || !map.mapPawns.FreeColonistsSpawned.Any())return false;
   if(parms.forced)return true;
   return AbyssRitual.Research?.IsFinished==true || map.mapPawns.FreeColonistsSpawned.Any(c=>AbyssTaint.Severity(c)>=AbyssTaint.Shadow);
  }
  public override float ChanceFactorNow(IIncidentTarget target)=>base.ChanceFactorNow(target)*AbyssSettings.ThinFrequency;
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;var p=map.mapPawns.FreeColonistsSpawned.Where(c=>!Outsiders.Is(c)).OrderByDescending(c=>AbyssTaint.Severity(c)).ThenBy(c=>Rand.Value).FirstOrDefault();if(p==null)return false;
   var book=ThingMaker.MakeThing(AbyssItems.Demonomicon);GenPlace.TryPlaceThing(book,p.PositionHeld,map,ThingPlaceMode.Near);
   var l=(ChoiceLetter_AbyssWhisper)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_AbyssWhisperLetter"));
   l.Label="深渊低语";l.dreamer=p;l.book=book;l.lookTargets=new LookTargets(book);
   l.Text=p.LabelShort+"梦见自己站在深渊的边缘，有个声音一遍遍念着一个名字。醒来时，枕边多了一本用不知名的皮装订的书——"+book.LabelNoCount+"。\n\n留下它，"+p.LabelShort+"会被深渊多染一分；烧掉它，心里会轻松一些；或者交给费伦拓荒同盟处理。";
   Find.LetterStack.ReceiveLetter(l);
   return true;
  }
 }
 public class ChoiceLetter_AbyssWhisper : LP_EventLetter {
  public Pawn dreamer;public Thing book;
  // 1.69.0 事件小人入口：做梦的殖民者还在、书还在（选中他或右键那本书）。
  protected override bool Live=>book!=null && !book.Destroyed && dreamer!=null && !dreamer.Dead;
  public override IEnumerable<Thing> EventThings{get{yield return dreamer;yield return book;}}
  public static void Keep(Pawn p,Thing b){AbyssTaint.Add(p,AbyssEvents.WhisperTaint,"深渊低语");}
  public static void Burn(Pawn p,Thing b){if(b!=null && !b.Destroyed)b.Destroy();var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_RejectedAbyss");if(th!=null)p?.needs?.mood?.thoughts?.memories?.TryGainMemory(th);}
  public static string CannotGive(){var f=Factions.Colony;return f==null?"没有费伦拓荒同盟":f.HostileTo(Faction.OfPlayer)?"费伦拓荒同盟和你敌对":null;}
  public static void Give(Thing b){if(b!=null && !b.Destroyed)b.Destroy();Factions.ChangeGoodwillExact(Factions.Colony,AbyssEvents.FactionGoodwill,"交出了一本恶魔学典籍");}
  public override IEnumerable<DiaOption> Choices{get{
   if(Closed || book==null || book.Destroyed){yield return Option_Close;yield break;}
   yield return Choice("留下（"+dreamer?.LabelShort+"腐化 +5%）",()=>Keep(dreamer,book));
   yield return Choice("烧掉（心情 +3，3 天）",()=>Burn(dreamer,book));
   var give=Choice("交给费伦拓荒同盟（好感 +10）",()=>Give(book));var why=CannotGive();if(why!=null)give.Disable(why);yield return give;
   yield return Option_Postpone;
  }}
  DiaOption Choice(string text,Action act)=>new DiaOption(text){action=()=>{act?.Invoke();Resolve();},resolveTree=true};
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref dreamer,"lpDreamer");Scribe_References.Look(ref book,"lpBook");}
 }
 // 魅魔来访：看起来就是普通的流浪者加入。10～20 天后（或被魔鬼之眼 / 阿斯莫看到时）现形。
 public class IncidentWorker_SuccubusVisit : IncidentWorker_DND {
  public const int MinColonists=5;
  protected override bool CanFireNowSub(IncidentParms parms)=>AbyssSettings.Enabled && AbyssSettings.BindColonists && parms.target is Map map && map.IsPlayerHome && map.mapPawns.FreeColonistsSpawnedCount>=MinColonists && AbyssRaces.SuccubusXeno!=null;
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;if(!CellFinder.TryFindRandomEdgeCellWith(c=>map.reachability.CanReachColony(c) && !c.Fogged(map),map,CellFinder.EdgeRoadChance_Neutral,out var cell))return false;
   var p=Visitor(map,cell);if(p==null)return false;
   SendStandardLetter("流浪者加入",p.LabelShort+"是一名独自流浪的旅人，想要加入你们。看起来没什么可疑的。",LetterDefOf.PositiveEvent,parms,p);
   return true;
  }
  public static Pawn Visitor(Map map,IntVec3 cell){
   var req=new PawnGenerationRequest(PawnKindDefOf.Villager,Faction.OfPlayer,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,fixedGender:Rand.Chance(.7f)?Gender.Female:Gender.Male,fixedBiologicalAge:Rand.Range(19f,30f));
   Pawn p;try{p=PawnGenerator.GeneratePawn(req);}catch(Exception e){Log.Warning("[DND边缘大陆] 魅魔来访生成失败："+e.Message);return null;}
   GenSpawn.Spawn(p,cell,map);
   var h=(Hediff_SuccubusDisguise)HediffMaker.MakeHediff(Outsiders.DisguiseDef,p);h.revealTick=Find.TickManager.TicksGame+Rand.RangeInclusive(10,20)*GenDate.TicksPerDay;p.health.AddHediff(h);
   MapComponent_Outsiders.Of(map)?.Register(p);
   return p;
  }
 }
 public class Hediff_SuccubusDisguise : HediffWithComps {
  public int revealTick;public DreamState dream=new DreamState();int acc;
  public override bool Visible=>false;
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);AbyssRaces.succubi++;}
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;if(pawn.Dead)return;
   AbyssRaces.DreamTick(pawn,dream);
   if(Find.TickManager.TicksGame>=revealTick){var p=pawn;Fiends.Defer(()=>AbyssEvents.RevealSuccubus(p,"时间到了"));}
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref revealTick,"lpReveal");Scribe_Deep.Look(ref dream,"lpDream");if(dream==null)dream=new DreamState();}
 }
 public class ChoiceLetter_SuccubusReveal : LP_EventLetter {
  public Pawn succubus;
  // 1.69.0 事件小人入口：现形的魅魔还没处置（选中它）。
  protected override bool Live=>succubus!=null && !succubus.Dead && !succubus.Destroyed && !Outsiders.Bound(succubus) && succubus.Faction?.IsPlayer==true;
  protected override bool StateTracked=>true;
  public override IEnumerable<Thing> EventThings{get{yield return succubus;}}
  public static string CannotBind(Pawn p){var map=p?.MapHeld;if(map==null || AbyssItems.AbyssCircle==null || !map.listerThings.ThingsOfDef(AbyssItems.AbyssCircle).Any())return "地图上没有深渊法阵";if(AbyssBinding.BoundCount()>=AbyssBinding.BoundCap())return "束缚者已满";return null;}
  public static void BindIt(Pawn p){var m=AbyssEvents.BestSocial(p.MapHeld);AbyssBinding.Bind(p,m,50f);AbyssBinding.SetLevel(p,8,true);}
  public static void Fight(Pawn p){AbyssBinding.BreakFree(p,"撕下了伪装");}
  public override IEnumerable<DiaOption> Choices{get{
   var p=succubus;if(Closed || p==null || p.Dead || p.Destroyed || Outsiders.Bound(p) || p.Faction?.IsPlayer!=true){yield return Option_Close;yield break;}
   var bind=Choice("束缚它（服从度 50，主人是社交最高的殖民者）",()=>BindIt(p));var why=CannotBind(p);if(why!=null)bind.Disable(why);yield return bind;
   yield return Choice("驱逐（它离开；和它最亲近的人心情 -8）",()=>AbyssEvents.Expel(p));
   yield return Choice("动手",()=>Fight(p));
  }}
  DiaOption Choice(string text,Action act)=>new DiaOption(text){action=()=>{act?.Invoke();Resolve();},resolveTree=true};
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref succubus,"lpSuccubus");}
 }
 // 深渊入侵：只由薄弱度触发（baseChance 0）。打开一道深渊裂口，按事件点数决定第 5 波有没有炎魔。
 public class IncidentWorker_AbyssIncursion : IncidentWorker {
  protected override bool CanFireNowSub(IncidentParms parms)=>AbyssSettings.Enabled && parms.target is Map map && map.IsPlayerHome && AbyssItems.AbyssPlane!=null && PortalEvent.RiftDef!=null && PortalEvent.TryFindSpot(map,out _);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;if(!PortalEvent.TryFindSpot(map,out var cell))return false;
   var rift=PortalEvent.Open(map,cell,AbyssItems.AbyssPlane,parms.points,false);
   SendStandardLetter("深渊入侵","位面薄得像一层纸：一道深渊裂口在殖民地外撕开了！劣魔先冲出来，然后是巴路古拉、哈兹魔，夜里还有影魔；再往后魅魔和（袭击点数够高时）炎魔也会亲自走出来。每 8 小时一波，开了 3 天不关就会崩溃。\n\n关上它：打掉裂口（600 生命）、扔驱散粉尘、让巨灵或六臂蛇人命令它闭合、在它 3 格内落下 5 瓶圣水，或者用一张真名卷直接封住。关上后会留下位面残渣和恶魔血。",LetterDefOf.ThreatBig,parms,new LookTargets(rift));
   return true;
  }
 }
 // 纳巴苏猎魂：只在夜里（22 点～4 点）出现，白天推迟到当晚 22 点。
 public class IncidentWorker_NabassuHunt : IncidentWorker_DND {
  public const int MinLevel=8;
  protected override bool CanFireNowSub(IncidentParms parms)=>AbyssSettings.Enabled && parms.target is Map map && map.IsPlayerHome && AdventurerEnemies.ColonyMaxLevel()>=MinLevel && AbyssState.Get?.huntTick<0;
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;var st=AbyssState.Get;if(st==null)return false;
   int h=GenLocalDate.HourOfDay(map);
   if(h>=AbyssEvents.HuntHour || h<4)return AbyssEvents.SpawnHunter(map,st)!=null;
   st.huntMap=map;st.huntTick=Find.TickManager.TicksGame+AbyssEvents.HoursUntil(map,AbyssEvents.HuntHour)*GenDate.TicksPerHour;
   return true;
  }
 }

 // ---------- 圣坛：赎回灵魂 ----------
 public static class SoulRedemption {
  public const float Cost=30f;
  public static string Cannot(Pawn p){
   if(!Items.Has(p,"LP_SoulMortgaged"))return "灵魂没有抵押";var god=FaithUtility.Patron;
   if(god==null || !god.Good)return "需要善神做守护神";if(FaithUtility.GetFavor(god)<Cost)return god.label+"的神眷不足 "+Cost.ToString("0");return null;
  }
  public static bool Redeem(Pawn p){if(Cannot(p)!=null)return false;FaithUtility.AddFavor(FaithUtility.Patron,-Cost,p.LabelShort+"赎回灵魂");Items.Remove(p,"LP_SoulMortgaged");Messages.Message(p.LabelShort+"在圣坛前赎回了抵押给深渊的灵魂。",p,MessageTypeDefOf.PositiveEvent,false);return true;}
 }
 [HarmonyPatch(typeof(Building_FaithAltar),nameof(Building_FaithAltar.GetFloatMenuOptions))]
 public static class AltarRedeemSoul {
  static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> values,Building_FaithAltar __instance,Pawn selPawn){
   foreach(var o in values)yield return o;
   if(selPawn==null || !Items.Has(selPawn,"LP_SoulMortgaged"))yield break;
   var why=SoulRedemption.Cannot(selPawn);var job=DefDatabase<JobDef>.GetNamedSilentFail("LP_RedeemSoul");if(job==null)yield break;
   yield return why!=null?new FloatMenuOption("赎回灵魂（"+why+"）",null):new FloatMenuOption("在圣坛赎回灵魂（"+SoulRedemption.Cost.ToString("0")+" 神眷）",()=>selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job,__instance),JobTag.Misc));
  }
 }
 public class JobDriver_RedeemSoul : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.InteractionCell);
   yield return Toils_General.Wait(600).WithProgressBarToilDelay(TargetIndex.A);
   yield return Toils_General.Do(()=>SoulRedemption.Redeem(pawn));
  }
 }
}
