using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.37.0 升神仪式：耐瑟祭坛的按钮 → 选人对话框（主祭 + 至少 3 名协祭）→ 确认框写明成功率 → 自定义 LordJob 吟诵 6 小时（期间本地图“魔网震颤”）→ 成功成为奥术半神，失败化为不可复原的石像并引发“魔网崩溃”。
 // 不走 Ideology 的仪式框架：没装 Ideology 也一样能用（照 1.0 巫妖仪式“工作单之外自己处理”的思路）。
 public static class NetherilAscension {
  public const int DurationTicks=6*GenDate.TicksPerHour,CollapseDays=15,MinCo=3,PsylinkNeed=6,VPENeed=20;
  public const float FocusNeed=.8f,ChanceMin=.4f,ChanceMax=.9f;
  // 另一个线程把奥术半神并入统一半神框架时用：授予时触发。
  public static event Action<Pawn> ArcaneDemigodGranted;
  static readonly DefRef<HediffDef> arcane=new DefRef<HediffDef>("LP_ArcaneDemigod");
  public static bool IsArcaneDemigod(Pawn p)=>Items.Has(p,arcane.Def); // 1.58.6：衰老钩子对每个成年小人每刻都会问一次，不再按名字查
  static GameConditionDef tremor,collapse;
  public static GameConditionDef TremorDef=>tremor??(tremor=DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_WeaveTremor"));
  public static GameConditionDef CollapseDef=>collapse??(collapse=DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_WeaveCollapse"));
  public static bool Active(Map m,GameConditionDef d)=>m!=null && d!=null && m.gameConditionManager.ConditionIsActive(d);
  public static bool WeaveCollapsed(Map m)=>Active(m,CollapseDef);
  public const string CollapseReason="魔网崩溃：这张地图上的魔网碎了，法术施放不出来";
  // 心灵法术冷却：魔网震颤 ×2（1.58.0 魔网之子升华不受影响），奥术半神 ×0.5（原版与 VPE 都走这里）。
  public static float CooldownFactor(Pawn p){float f=1f;if(p==null)return f;if(Active(p.MapHeld,TremorDef) && !HeroDestiny.WeaveImmune(p))f*=2f;if(IsArcaneDemigod(p))f*=.5f;return f;}
  public static IEnumerable<Building_NetherilAltar> Altars(Map map)=>map==null?Enumerable.Empty<Building_NetherilAltar>():map.listerBuildings.allBuildingsColonist.OfType<Building_NetherilAltar>();
  public static int Level(Pawn p)=>NetherilHooks.VPEActive?NetherilHooks.Level(p):p?.GetPsylinkLevel()??0;
  public static bool LevelOk(Pawn p)=>p!=null && (p.GetPsylinkLevel()>=PsylinkNeed || (NetherilHooks.VPEActive && NetherilHooks.Level(p)>=VPENeed));
  public static string LevelText=>"心灵链接 "+PsylinkNeed+" 级"+(NetherilHooks.VPEActive?"或 VPE 灵能等级 ≥ "+VPENeed:"");
  public static string PrincipalBlocked(Pawn p){
   var st=Netheril.State;if(p==null || st==null)return "没有主祭";
   if(st.Passes(p)<Netheril.AscensionPasses)return p.LabelShort+"读卡苏斯之卷 "+st.Passes(p)+" / "+Netheril.AscensionPasses+" 遍";
   if(!LevelOk(p))return p.LabelShort+"需要"+LevelText;
   return null;
  }
  public static string ConditionsText(Pawn p){
   var st=Netheril.State;
   return "升神仪式的三个条件：\n① 一个人把卡苏斯之卷读满 "+Netheril.AscensionPasses+" 遍"+(p!=null && st!=null?"（"+p.LabelShort+"："+st.Passes(p)+" 遍）":"")+"\n② 这个人"+LevelText+(p!=null?"（现在 "+(NetherilHooks.VPEActive?"灵能等级 "+NetherilHooks.Level(p)+"，":"")+"心灵链接 "+p.GetPsylinkLevel()+" 级）":"")+"\n③ 一座耐瑟祭坛（研究“耐瑟瑞尔奥术”，金 100 + 秘银 50 + 玻璃钢 50）\n\n另外还要至少 "+MinCo+" 名有心灵链接的协祭，开始时所有人精神力 ≥ "+FocusNeed.ToStringPercent()+"。";
  }
  public static string ConditionsLine(Map map){
   var st=Netheril.State;if(st==null)return "";
   var best=map?.mapPawns.FreeColonists.OrderByDescending(p=>st.Passes(p)).FirstOrDefault();
   bool a=best!=null && st.Passes(best)>=Netheril.AscensionPasses,b=best!=null && LevelOk(best),c=Altars(map).Any();
   return "升神条件：读满 "+Netheril.AscensionPasses+" 遍 "+(a?"✓":"✗")+(best!=null?"（最多："+best.LabelShort+" "+st.Passes(best)+" 遍）":"")+"；"+LevelText+" "+(b?"✓":"✗")+"；耐瑟祭坛 "+(c?"✓":"✗");
  }
  public static List<Pawn> Principals(Map map)=>map==null?new List<Pawn>():map.mapPawns.FreeColonistsSpawned.Where(p=>PrincipalBlocked(p)==null && !p.Downed && !p.InMentalState).ToList();
  public static List<Pawn> CoCandidates(Map map,Pawn principal)=>map==null?new List<Pawn>():map.mapPawns.FreeColonistsSpawned.Where(p=>p!=principal && Netheril.HasPsylink(p) && !p.Downed && !p.InMentalState).ToList();
  // 发起前的总条件（不含选人）。
  public static string Blocked(Building_NetherilAltar altar){
   var st=Netheril.State;if(st==null || altar==null || !altar.Spawned)return "祭坛不在地图上";
   if(st.demigod!=null && !st.demigod.Dead && !st.demigod.Destroyed && IsArcaneDemigod(st.demigod))return "世上已经有一位奥术半神了（"+st.demigod.LabelShort+"）";
   if(st.deciphered)return "卡苏斯之卷已被解读，不会再有升神仪式";
   if(altar.lord!=null && altar.Map.lordManager.lords.Contains(altar.lord))return "仪式正在进行";
   var k=NetherilQuest.Karsus;if(k==null || k.MapHeld!=altar.Map)return "卡苏斯之卷要在这张地图上";
   if(Principals(altar.Map).Count==0)return "没有人满足主祭条件："+Netheril.AscensionPasses+" 遍 + "+LevelText;
   return null;
  }
  public static string StartBlocked(Building_NetherilAltar altar,Pawn principal,List<Pawn> co){
   var b=Blocked(altar);if(b!=null)return b;
   b=PrincipalBlocked(principal);if(b!=null)return b;
   if(co==null || co.Count(p=>p!=principal && Netheril.HasPsylink(p))<MinCo)return "至少要 "+MinCo+" 名有心灵链接的协祭";
   foreach(var p in co.Concat(new[]{principal}))if((p.psychicEntropy?.CurrentPsyfocus??0f)<FocusNeed-.0001f)return p.LabelShort+"的精神力不到 "+FocusNeed.ToStringPercent();
   return null;
  }
  // 成功率 = 40% + 5%×(遍数−7) + 5%×(协祭−3) + 2%×floor((心灵敏感度−100%)/10%)，夹在 40%～90%。
  public static float Chance(int passes,int co,float sensitivity)=>Mathf.Clamp(.4f+.05f*(passes-Netheril.AscensionPasses)+.05f*(co-MinCo)+.02f*Mathf.Floor((sensitivity-1f)/.1f+.0001f),ChanceMin,ChanceMax);
  public static float Chance(Pawn principal,int co)=>Chance(Netheril.State?.Passes(principal)??0,co,principal?.GetStatValue(StatDefOf.PsychicSensitivity)??1f);
  public static Lord Start(Building_NetherilAltar altar,Pawn principal,List<Pawn> co){
   if(StartBlocked(altar,principal,co)!=null)return null;
   var map=altar.Map;var all=new List<Pawn>{principal};all.AddRange(co.Where(p=>p!=principal));
   foreach(var p in all){p.jobs?.StopAll();if(p.drafter!=null)p.drafter.Drafted=false;}
   var job=new LordJob_NetherilAscension(altar,principal,co.Count,Find.TickManager.TicksGame);
   var lord=LordMaker.MakeNewLord(Faction.OfPlayer,job,map,all);
   altar.lord=lord;
   if(TremorDef!=null)map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(TremorDef,DurationTicks));
   Messages.Message(principal.LabelShort+"开始了升神仪式："+co.Count+" 名协祭，成功率 "+Chance(principal,co.Count).ToStringPercent()+"。仪式要 6 小时。",new LookTargets(altar),MessageTypeDefOf.NeutralEvent,false);
   return lord;
  }
  static void ClearFocus(IEnumerable<Pawn> pawns){foreach(var p in pawns)if(p?.psychicEntropy!=null)p.psychicEntropy.OffsetPsyfocusDirectly(-p.psychicEntropy.CurrentPsyfocus);}
  // 结算：roll 为空时掷骰。返回是否成功。
  public static bool Resolve(Building_NetherilAltar altar,Pawn principal,List<Pawn> co,float? roll=null){
   float chance=Chance(principal,co.Count);bool ok=(roll??Netheril.Roll)<chance;
   ClearFocus(co.Concat(new[]{principal}));
   var map=altar?.Map??principal?.MapHeld;
   foreach(var c in (map?.gameConditionManager.ActiveConditions??new List<GameCondition>()).Where(c=>c.def==TremorDef).ToList())c.End();
   if(ok)Succeed(principal);else Fail(principal,map);
   return ok;
  }
  public static void Succeed(Pawn p){
   var st=Netheril.State;
   if(!IsArcaneDemigod(p))p.health.AddHediff(Items.Hediff("LP_ArcaneDemigod"));
   var oracle=DefDatabase<AbilityDef>.GetNamedSilentFail("LP_Oracle");if(oracle!=null && p.abilities?.GetAbility(oracle)==null)p.abilities.GainAbility(oracle);
   if(st!=null){st.demigod=p;st.deciphered=true;}
   p.needs?.AddOrRemoveNeedsAsAppropriate();p.Drawer?.renderer?.SetAllGraphicsDirty();
   var t=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_WitnessedAscension");if(t!=null)foreach(var c in PawnsFinder.AllMaps_FreeColonists)c.needs?.mood?.thoughts?.memories?.TryGainMemory(t);
   try{ArcaneDemigodGranted?.Invoke(p);}catch(Exception e){Log.Error("[DND边缘大陆] ArcaneDemigodGranted 订阅者出错："+e);}
   Find.LetterStack.ReceiveLetter("奥术半神",p.LabelShort+"完成了卡苏斯没能完成的事：魔网接纳了他，"+p.LabelShort+"成为了奥术半神。\n\n心灵熵上限 ×2、精神力回复 ×2、不再衰老、不再需要进食；心灵法术冷却减半。获得能力“神谕”：每天一次，抹去一名殖民者所有的负面心情记忆。\n\n卡苏斯之卷已被解读：仍然可以研读以获得回响，但不会再有升神仪式。全体殖民者“见证升神”心情 +5，持续 15 天。",LetterDefOf.PositiveEvent,p);
  }
  public static void Fail(Pawn p,Map map){
   var st=Netheril.State;
   PetrifiedStatue statue=null;if(p!=null && p.Spawned){statue=ItemScrolls.Petrify(p);if(statue!=null)statue.permanent=true;}
   if(map!=null && CollapseDef!=null)map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(CollapseDef,CollapseDays*GenDate.TicksPerDay));
   var k=NetherilQuest.Karsus;if(k!=null){if(k.Spawned)FleckMaker.ThrowDustPuffThick(k.DrawPos,k.Map,2f,Color.gray);k.Destroy(DestroyMode.Vanish);NetherilQuest.OnKarsusLost("在失败的仪式里化成了灰");}
   var t=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_AscensionFailed");if(t!=null)foreach(var c in PawnsFinder.AllMaps_FreeColonists)c.needs?.mood?.thoughts?.memories?.TryGainMemory(t);
   Find.LetterStack.ReceiveLetter("升神失败",(p?.LabelShort??"主祭")+"没能撑住魔网的重量，化成了一尊石像——这尊石像不可复原，驱散粉尘也救不回来。\n\n这张地图陷入“魔网崩溃”，持续 "+CollapseDays+" 天：所有心灵法术（原版和 VPE）、职业法术、巫妖法术和法杖射击都用不了。卡苏斯之卷在仪式里化成了灰，30～60 天后会传来它的下落；各人读过的遍数都还记着。全体殖民者“升神失败”心情 -8，持续 10 天。",LetterDefOf.NegativeEvent,statue!=null?new LookTargets(statue):LookTargets.Invalid);
  }
  public static void Abort(Building_NetherilAltar altar,List<Pawn> all,string why){
   ClearFocus(all);
   var map=altar?.Map;foreach(var c in (map?.gameConditionManager.ActiveConditions??new List<GameCondition>()).Where(c=>c.def==TremorDef).ToList())c.End();
   Messages.Message("升神仪式中断了："+why+"。卷轴完好，可以重新发起。",new LookTargets(altar),MessageTypeDefOf.NegativeEvent,false);
  }
 }

 public class Building_NetherilAltar : Building {
  public Lord lord;
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(Faction!=Faction.OfPlayer)yield break;
   var cmd=new Command_Action{defaultLabel="发起升神仪式",defaultDesc="主祭（读满 "+Netheril.AscensionPasses+" 遍卡苏斯之卷、"+NetherilAscension.LevelText+"）和至少 "+NetherilAscension.MinCo+" 名有心灵链接的协祭在祭坛前吟诵 6 小时。开始时所有人精神力 ≥ 80%，结束时清零。\n\n成功率 = 40% + 5%×(遍数−7) + 5%×(协祭−3) + 2%×每高出 10% 的心灵敏感度，40%～90%。\n成功：主祭成为奥术半神。失败：主祭化为不可复原的石像，本地图魔网崩溃 15 天，卷轴化为灰烬。",icon=ContentFinder<Texture2D>.Get(def.uiIconPath??def.graphicData.texPath,false)??BaseContent.BadTex,action=()=>Find.WindowStack.Add(new Dialog_NetherilAscension(this))};
   var why=NetherilAscension.Blocked(this);if(why!=null)cmd.Disable(why);
   yield return cmd;
  }
  public override string GetInspectString(){
   var s=base.GetInspectString();var lines=new List<string>();if(!s.NullOrEmpty())lines.Add(s);
   lines.Add(NetherilAscension.ConditionsLine(Map));
   if(lord?.LordJob is LordJob_NetherilAscension j && Map.lordManager.lords.Contains(lord))lines.Add("升神仪式进行中：还剩 "+Mathf.Max(0f,(j.startTick+NetherilAscension.DurationTicks-Find.TickManager.TicksGame)/2500f).ToString("0.#")+" 小时");
   return string.Join("\n",lines);
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref lord,"lpAscensionLord");}
 }

 public class Dialog_NetherilAscension : Window {
  readonly Building_NetherilAltar altar;Pawn principal;readonly HashSet<Pawn> co=new HashSet<Pawn>();Vector2 scroll;
  public override Vector2 InitialSize=>new Vector2(520f,560f);
  public Dialog_NetherilAscension(Building_NetherilAltar altar){this.altar=altar;doCloseX=true;forcePause=true;absorbInputAroundWindow=true;principal=NetherilAscension.Principals(altar.Map).FirstOrDefault();}
  public override void DoWindowContents(Rect inRect){
   var l=new Listing_Standard();l.Begin(inRect);
   Text.Font=GameFont.Medium;l.Label("升神仪式");Text.Font=GameFont.Small;
   l.Label("主祭：");
   foreach(var p in NetherilAscension.Principals(altar.Map))if(l.RadioButton(p.LabelShort+"（"+Netheril.State.Passes(p)+" 遍，心灵敏感度 "+p.GetStatValue(StatDefOf.PsychicSensitivity).ToStringPercent()+"）",principal==p)){principal=p;co.Remove(p);}
   l.GapLine();l.Label("协祭（至少 "+NetherilAscension.MinCo+" 名有心灵链接的殖民者）：");
   var cands=NetherilAscension.CoCandidates(altar.Map,principal);
   var outRect=l.GetRect(220f);var view=new Rect(0f,0f,outRect.width-20f,cands.Count*26f);
   Widgets.BeginScrollView(outRect,ref scroll,view);float y=0f;
   foreach(var p in cands){bool on=co.Contains(p);Widgets.CheckboxLabeled(new Rect(0f,y,view.width,24f),p.LabelShort+"（精神力 "+(p.psychicEntropy?.CurrentPsyfocus??0f).ToStringPercent()+"）",ref on);if(on)co.Add(p);else co.Remove(p);y+=26f;}
   Widgets.EndScrollView();
   co.RemoveWhere(p=>!cands.Contains(p));
   l.GapLine();
   var list=co.ToList();
   if(principal!=null)l.Label("成功率："+NetherilAscension.Chance(principal,list.Count).ToStringPercent()+"（40%～90%）");
   var why=NetherilAscension.StartBlocked(altar,principal,list);
   if(why!=null)l.Label("<color=#d08080>"+why+"</color>");
   if(l.ButtonText("开始仪式") && why==null){
    float chance=NetherilAscension.Chance(principal,list.Count);var p0=principal;
    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("由 "+p0.LabelShort+" 主祭、"+list.Count+" 名协祭发起升神仪式。\n\n成功率 "+chance.ToStringPercent()+"。\n失败时 "+p0.LabelShort+" 会化为不可复原的石像，这张地图魔网崩溃 15 天，卡苏斯之卷化为灰烬。\n\n确定开始吗？",()=>{NetherilAscension.Start(altar,p0,list);Close();},true));
   }
   l.End();
  }
 }

 // 吟诵：所有人站到祭坛周围的位置上，6 小时后结算。主祭倒下、离开或协祭不足 3 人时中断。
 public class LordJob_NetherilAscension : LordJob {
  public Building_NetherilAltar altar;public Pawn principal;public int startTick,coCount;bool done;
  public LordJob_NetherilAscension(){}
  public LordJob_NetherilAscension(Building_NetherilAltar altar,Pawn principal,int coCount,int startTick){this.altar=altar;this.principal=principal;this.coCount=coCount;this.startTick=startTick;}
  public override StateGraph CreateGraph(){var g=new StateGraph();g.StartingToil=new LordToil_NetherilChant();return g;}
  public IntVec3 CellFor(Pawn p){
   if(altar==null || !altar.Spawned)return p.Position;var map=altar.Map;
   if(p==principal)return altar.Position+IntVec3.South*2;
   var ring=GenRadial.RadialCellsAround(altar.Position,3.9f,true).Where(c=>c.DistanceTo(altar.Position)>=2.9f && c.InBounds(map) && c.Standable(map) && c!=altar.Position+IntVec3.South*2).OrderBy(c=>Mathf.Atan2(c.z-altar.Position.z,c.x-altar.Position.x)).ToList();
   int i=lord.ownedPawns.Where(x=>x!=principal).ToList().IndexOf(p);
   return ring.Count==0?p.Position:ring[Mathf.Max(0,i)*Mathf.Max(1,ring.Count/Mathf.Max(1,coCount))%ring.Count];
  }
  List<Pawn> Co=>lord.ownedPawns.Where(p=>p!=principal).ToList();
  public override void LordJobTick(){
   base.LordJobTick();if(done)return;int now=Find.TickManager.TicksGame;
   if(now%250==0){
    string why=null;
    if(altar==null || altar.Destroyed || !altar.Spawned)why="祭坛没了";
    else if(principal==null || principal.Dead || principal.Downed || !lord.ownedPawns.Contains(principal))why="主祭离开了仪式";
    else if(Co.Count<NetherilAscension.MinCo)why="协祭不足 "+NetherilAscension.MinCo+" 人";
    if(why!=null){done=true;NetherilAscension.Abort(altar,lord.ownedPawns.ToList(),why);End();return;}
   }
   if(now>=startTick+NetherilAscension.DurationTicks){done=true;var co=Co;var p=principal;var a=altar;End();NetherilAscension.Resolve(a,p,co);}
  }
  void End(){var l=lord;if(altar!=null)altar.lord=null;l?.lordManager?.RemoveLord(l);}
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref altar,"altar");Scribe_References.Look(ref principal,"principal");Scribe_Values.Look(ref startTick,"startTick");Scribe_Values.Look(ref coCount,"coCount");Scribe_Values.Look(ref done,"done");}
 }
 public class LordToil_NetherilChant : LordToil {
  public override void UpdateAllDuties(){
   var job=lord.LordJob as LordJob_NetherilAscension;var duty=DefDatabase<DutyDef>.GetNamed("LP_NetherilChant");
   foreach(var p in lord.ownedPawns)p.mindState.duty=new PawnDuty(duty,job?.CellFor(p)??p.Position,job?.altar);
  }
 }
 public class JobGiver_NetherilChant : ThinkNode_JobGiver {
  protected override Job TryGiveJob(Pawn pawn){
   var d=pawn.mindState?.duty;if(d==null || !d.focus.IsValid)return null;
   var j=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("LP_NetherilChant"),d.focus,d.focusSecond);j.locomotionUrgency=LocomotionUrgency.Jog;return j;
  }
 }
 public class JobDriver_NetherilChant : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  protected override IEnumerable<Toil> MakeNewToils(){
   yield return Toils_Goto.GotoCell(TargetIndex.A,PathEndMode.OnCell);
   var t=ToilMaker.MakeToil("NetherilChant");
   t.initAction=()=>{if(job.targetB.IsValid)pawn.rotationTracker.FaceTarget(job.targetB);if(pawn.Spawned)FleckMaker.Static(pawn.Position,pawn.Map,FleckDefOf.PsycastAreaEffect,1f);};
   t.defaultCompleteMode=ToilCompleteMode.Delay;t.defaultDuration=GenDate.TicksPerHour;t.handlingFacing=true;
   yield return t;
  }
 }

 // 奥术半神：心灵熵上限 ×2、精神力回复（冥想精神力增长）×2、不需进食；不老与冷却减半在下面的补丁里。
 public class Hediff_ArcaneDemigod : HediffWithComps {
  HediffStage cached;
  public override bool ShouldRemove=>false;
  public override HediffStage CurStage{get{
   if(cached!=null)return cached;
   var factors=new List<StatModifier>{new StatModifier{stat=StatDefOf.PsychicEntropyMax,value=2f}};
   var gain=DefDatabase<StatDef>.GetNamedSilentFail("MeditationFocusGain");if(gain!=null)factors.Add(new StatModifier{stat=gain,value=2f});
   return cached=new HediffStage{statFactors=factors,hungerRateFactor=0f};
  }}
 }
 // 1.63.0：奥术半神同时带半神层（LP_Demigod，心灵敏感度 +20%），原版心灵熵上限又乘心灵敏感度，结果 ×2.4。
 // 奥术半神的 ×2 已经包含神性，这里把半神层的敏感度加成从心灵熵上限里除掉，只乘一次。
 public class StatPart_ArcaneEntropyOnce : StatPart {
  static float Ratio(Pawn p){
   if(p==null || !NetherilAscension.IsArcaneDemigod(p))return 1f;var h=Demigods.Of(p);if(h==null)return 1f;
   float s=p.GetStatValue(StatDefOf.PsychicSensitivity),off=Demigods.Sensitivity(h.Level);
   return s>off+.01f?(s-off)/s:1f;
  }
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p){float r=Ratio(p);if(r!=1f)val*=r;}}
  public override string ExplanationPart(StatRequest req){float r=Ratio(req.Thing as Pawn);return r==1f?null:"奥术半神（半神层敏感度不重复计入）：×"+r.ToStringPercent();}
 }
 [StaticConstructorOnStartup]
 public static class ArcaneEntropyStartup {
  static ArcaneEntropyStartup(){var s=StatDefOf.PsychicEntropyMax;if(s==null)return;if(s.parts==null)s.parts=new List<StatPart>();s.parts.Add(new StatPart_ArcaneEntropyOnce{parentStat=s});}
 }
 // 神谕：抹去一名殖民者所有的负面心情记忆。
 public class CompProperties_Oracle : CompProperties_AbilityEffect {public CompProperties_Oracle(){compClass=typeof(CompOracle);}}
 public class CompOracle : CompAbilityEffect {
  public static int Clear(Pawn p){var mem=p?.needs?.mood?.thoughts?.memories;if(mem==null)return 0;var bad=mem.Memories.Where(m=>m.MoodOffset()<0f).ToList();foreach(var m in bad)mem.RemoveMemory(m);return bad.Count;}
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   var p=target.Pawn;string why=p==null || p.Dead?"需要一名活着的殖民者":p.Faction!=Faction.OfPlayer || !p.RaceProps.Humanlike?"只能对殖民者使用":null;
   if(why!=null && throwMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);
   return why==null && base.Valid(target,throwMessages);
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){base.Apply(target,dest);var p=target.Pawn;int n=Clear(p);Messages.Message("神谕抚平了"+p.LabelShort+"心里的 "+n+" 段阴影。",p,MessageTypeDefOf.PositiveEvent,false);}
 }

 [HarmonyPatch(typeof(Pawn_AgeTracker),"get_BiologicalTicksPerTick")]
 public static class DemigodAgeless {static void Postfix(Pawn ___pawn,ref float __result){if(__result>0f && ___pawn?.ageTracker!=null && ___pawn.ageTracker.Adult && NetherilAscension.IsArcaneDemigod(___pawn))__result=0f;}}
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.StartCooldown))]
 public static class NetherilPsycastCooldown {
  static void Prefix(RimWorld.Ability __instance,ref int ticks){
   if(ticks<=0 || __instance?.pawn==null || __instance.def==null || !__instance.def.IsPsycast)return;
   float f=NetherilAscension.CooldownFactor(__instance.pawn);if(f!=1f)ticks=Math.Max(1,Mathf.RoundToInt(ticks*f));
  }
 }
 // 魔网崩溃：原版心灵法术（含巫妖法术）在这张地图上用不了；VPE 的在桥接里另挂，法杖在 Arcana.CanFire 里。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.GizmoDisabled))]
 public static class WeaveCollapseGizmo {static void Postfix(RimWorld.Ability __instance,ref bool __result,ref string reason){if(__instance?.def!=null && __instance.def.IsPsycast && NetherilAscension.WeaveCollapsed(__instance.pawn?.MapHeld) && !HeroDestiny.WeaveImmune(__instance.pawn)){__result=true;reason=NetherilAscension.CollapseReason;}}}
 [HarmonyPatch(typeof(RimWorld.Ability),"get_CanCast")]
 public static class WeaveCollapseCast {static void Postfix(RimWorld.Ability __instance,ref AcceptanceReport __result){if(__result.Accepted && __instance?.def!=null && __instance.def.IsPsycast && NetherilAscension.WeaveCollapsed(__instance.pawn?.MapHeld) && !HeroDestiny.WeaveImmune(__instance.pawn))__result=NetherilAscension.CollapseReason;}}
}
