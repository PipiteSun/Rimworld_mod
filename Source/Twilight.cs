using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.28.0 黄昏之年：神力灌注（特性 LP_DivineInfusion，8 个等级对应 8 位神）+ 神明附体（状态 LP_DivineVessel）+ 派系对应神祇 + 世界条件。
 // 平时袭击者 3% 灌注；黄昏之年里 15%，2000 点以上的袭击 50% 让领队被神明附体。概率都 × 黄昏强度。
 // 1.39.0：类名 DeityDef → LP_DeityDef（存档只存 defName，不受影响）；信仰用到的字段在 Faith.cs 的另一半里。degree 仍是神力灌注特性的等级（0 = 不进灌注）。
 public partial class LP_DeityDef : Def {
  public int degree;
  public Color color=Color.white;
  public string key,infusionTitle,vesselTitle;
 }
 // 挂在 FactionDef 或 PawnKindDef 上：兵种的优先于派系的。
 public class DeityExtension : DefModExtension {
  public LP_DeityDef deity;
  public bool onlyWhenHostile; // 洛山达：同盟对玩家敌对时才生效
  public bool vanilla;         // 原版派系（海盗、部落）：设置关闭时忽略
 }

 public static class Twilight {
  public const float NormalChance=.03f,TwilightChance=.15f,VesselChance=.5f,VesselPoints=2000f,InfusedPower=.3f,VesselPower=1f,TwilightBoost=1.5f;
  public const int OmenDays=15,MoodDays=3;
  public static float? TestRoll;
  public static float Roll=>TestRoll??Rand.Value;
  static TraitDef trait;static HediffDef vessel;static GameConditionDef condition;static List<LP_DeityDef> deities;
  public static TraitDef Trait=>trait??(trait=DefDatabase<TraitDef>.GetNamedSilentFail("LP_DivineInfusion"));
  public static HediffDef VesselDef=>vessel??(vessel=DefDatabase<HediffDef>.GetNamedSilentFail("LP_DivineVessel"));
  public static GameConditionDef Condition=>condition??(condition=DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_TwilightYear"));
  public static List<LP_DeityDef> Deities=>deities??(deities=DefDatabase<LP_DeityDef>.AllDefsListForReading.Where(d=>d.degree>0).OrderBy(d=>d.degree).ToList());
  public static LP_DeityDef ByKey(string key){var l=Deities;for(int i=0;i<l.Count;i++)if(l[i].key==key)return l[i];return null;} // 1.58.6：不用 LINQ 闭包
  public static LP_DeityDef ByDegree(int degree){var l=Deities;for(int i=0;i<l.Count;i++)if(l[i].degree==degree)return l[i];return null;}
  public static float Strength=>(LichMod.Settings?.twilightStrength??100)/100f;
  public static bool Active=>Condition!=null && Find.World?.gameConditionManager?.ConditionIsActive(Condition)==true;
  // 黄昏之年里神力效果 ×1.5 × 黄昏强度（构思里写的“D”）。
  public static float D=>Active?TwilightBoost*Strength:1f;
  public static WorldComponent_Twilight World=>WorldComp<WorldComponent_Twilight>.Get(); // 1.58.6：缓存

  // ---------- 角色身上的神 ----------
  public static LP_DeityDef Infused(Pawn p){var t=p?.story?.traits?.GetTrait(Trait);return t==null?null:ByDegree(t.Degree);}
  public static Hediff_DivineVessel Vessel(Pawn p){var list=p?.health?.hediffSet?.hediffs;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i] is Hediff_DivineVessel h)return h;return null;}
  public static LP_DeityDef VesselDeity(Pawn p)=>Vessel(p)?.Deity;
  public static bool InfusedBy(Pawn p,string key){var d=Infused(p);return d!=null && d.key==key;}
  public static bool VesselOf(Pawn p,string key){var d=VesselDeity(p);return d!=null && d.key==key;}
  // 看得见隐身（伊尔斯维“低语者”、提尔“律法之眼”）：接到寒冰魔“魔鬼之眼”的同一套判断上。
  public static bool SeesInvisible(Pawn p){var d=Infused(p);return d!=null && (d.key=="Ilsvi" || d.key=="Tyr");}

  // ---------- 生成 ----------
  public static LP_DeityDef DeityFor(Pawn p,Faction f){
   var e=p?.kindDef?.GetModExtension<DeityExtension>()??f?.def?.GetModExtension<DeityExtension>();
   if(e?.deity==null)return null;
   if(e.vanilla && !LichMod.Settings.twilightVanilla)return null;
   if(e.onlyWhenHostile && (f==null || !f.HostileTo(Faction.OfPlayer)))return null;
   return e.deity;
  }
  public static float InfusionChance=>(Active?TwilightChance:(LichMod.Settings.infusionNormalZero?0f:NormalChance))*Strength;
  public static bool Eligible(Pawn p)=>p?.story?.traits!=null && p.RaceProps.Humanlike && !p.Dead && Necro.Construct(p)==null && !Fiends.Is(p);
  public static void Infuse(Pawn p,LP_DeityDef d){
   if(p==null || d==null || Trait==null || !Eligible(p))return;
   var had=p.story.traits.GetTrait(Trait);if(had!=null){if(had.Degree==d.degree)return;p.story.traits.RemoveTrait(had);}
   p.story.traits.GainTrait(new Trait(Trait,d.degree,true));
   // 洛山达“晨光之子”的每小时治疗和“第一次倒地”记录放在一个隐藏状态里。
   if(d.key=="Lathander" && !Items.Has(p,"LP_DawnChild"))Items.Give(p,"LP_DawnChild");
  }
  public static Hediff_DivineVessel MakeVessel(Pawn p,LP_DeityDef d){
   if(p==null || d==null || VesselDef==null || !Eligible(p))return null;
   Infuse(p,d);
   var h=Vessel(p);if(h!=null)return h;
   h=(Hediff_DivineVessel)HediffMaker.MakeHediff(VesselDef,p);h.deity=d;p.health.AddHediff(h);
   if(p.Faction==null || !p.Faction.IsPlayer)AdventurerEnemies.Story(p,15); // 1.48.1 神明附体首领 15 级
   return h;
  }
  public static Pawn LeaderOf(List<Pawn> list,PawnGroupMakerParms parms){
   var ext=Factions.Ext(parms?.faction);
   return list.FirstOrDefault(p=>ext!=null && ext.leaderKinds.Contains(p.kindDef) && Eligible(p)) ?? list.FirstOrDefault(p=>p.kindDef.factionLeader && Eligible(p)) ?? list.FirstOrDefault(Eligible);
  }
  // 袭击组生成后：按神灌注、附体领队，再按多出来的战力裁掉末尾的成员（灌注者算 ×1.3、附体者 ×2）：先裁普通成员，不够再裁灌注者，附体者和商人、造物不裁。
  public static IEnumerable<Pawn> Process(IEnumerable<Pawn> source,PawnGroupMakerParms parms){
   var list=source?.ToList()??new List<Pawn>();
   if(list.Count==0 || parms?.faction==null || parms.groupKind!=PawnGroupKindDefOf.Combat || Trait==null)return list;
   float extra=0;var infused=new HashSet<Pawn>();Pawn vesselPawn=null;
   foreach(var p in list){
    var d=DeityFor(p,parms.faction);if(d==null || !Eligible(p))continue;
    if(Roll<InfusionChance){Infuse(p,d);extra+=p.kindDef.combatPower*InfusedPower;infused.Add(p);}
   }
   if(Active && parms.points>=VesselPoints){
    var leader=LeaderOf(list,parms);var d=leader==null?null:DeityFor(leader,parms.faction);
    if(d!=null && Roll<VesselChance*Strength){if(!infused.Contains(leader))extra+=leader.kindDef.combatPower*InfusedPower;MakeVessel(leader,d);extra+=leader.kindDef.combatPower*VesselPower;infused.Add(leader);vesselPawn=leader;}
   }
   Trim(list,infused,vesselPawn,extra);
   return list;
  }
  public static int Trim(List<Pawn> list,HashSet<Pawn> infused,Pawn keep,float extra){
   int removed=0;
   for(int pass=0;pass<2;pass++)for(int i=list.Count-1;i>=0 && extra>0 && list.Count>1;i--){
    var p=list[i];if(p==keep || p.kindDef.trader || Necro.Construct(p)!=null)continue;
    bool inf=infused.Contains(p);if(pass==0?inf:!inf)continue;
    extra-=p.kindDef.combatPower*(inf?1f+InfusedPower:1f);list.RemoveAt(i);removed++;
    if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);if(!p.Destroyed)p.Destroy();
   }
   return removed;
  }
  // ---------- 黄昏之年 ----------
  public static GameCondition_TwilightYear Start(int days=-1){
   if(Condition==null || Find.World==null)return null;
   var mgr=Find.World.gameConditionManager;var have=mgr.GetActiveCondition(Condition) as GameCondition_TwilightYear;if(have!=null)return have;
   int d=days>0?days:LichMod.Settings.twilightDays;
   var c=(GameCondition_TwilightYear)GameConditionMaker.MakeCondition(Condition,d*GenDate.TicksPerDay);
   mgr.RegisterCondition(c);
   Find.LetterStack.ReceiveLetter("黄昏之年","天空染成了暗红色，诸神的目光投向了这片土地。\n\n接下来的"+d+"天里，敌人中的神力灌注者大幅增多（15%），大规模袭击的领队有一半的可能被神明附体；神力的效果也更强。\n\n击退神明附体者会得到神迹残片。",LetterDefOf.ThreatBig);
   return c;
  }
  public static bool End(){var c=Condition==null?null:Find.World?.gameConditionManager?.GetActiveCondition(Condition);if(c==null)return false;c.End();return true;}
  public static void Omen(){
   var w=World;if(w==null)return;
   w.startTick=Find.TickManager.TicksGame+OmenDays*GenDate.TicksPerDay;
   Find.LetterStack.ReceiveLetter("黄昏之兆","祭司们做了同样的梦：众神在暗红的天幕下争吵。\n\n"+OmenDays+"天后，黄昏之年就要开始了。到时候敌人里会出现更多神力灌注者，大规模袭击的领队可能被神明附体。",LetterDefOf.ThreatSmall);
  }
  // ---------- 奖励 ----------
  public static void Reward(Pawn p){
   var map=p?.MapHeld;var cell=p?.PositionHeld??IntVec3.Invalid;
   Adventurer.MilestoneMap(map,"vessel",Adventurer.VesselXP); // 1.48.0 冒险者里程碑：击退神明附体者
   var def=Items.Thing("LP_MiracleShard");
   if(def!=null && map!=null && cell.IsValid){var t=ThingMaker.MakeThing(def);t.stackCount=Rand.RangeInclusive(1,2);GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);}
   var thought=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_RepelledVessel");
   if(thought!=null)foreach(var c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)c.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
  }
 }

 // ---------- 生成挂钩：袭击组生成之后（寒冰魔裁剪、斩首分组都已做完） ----------
 [HarmonyPatch(typeof(PawnGroupMakerUtility),nameof(PawnGroupMakerUtility.GeneratePawns))]
 public static class TwilightGroupHook {
  [HarmonyPriority(Priority.Low)]
  static void Postfix(PawnGroupMakerParms parms,ref IEnumerable<Pawn> __result){if(parms?.faction!=null && Twilight.Trait!=null && __result!=null)__result=Twilight.Process(__result,parms);}
 }

 // ---------- 世界条件：天空偏暗红；前 3 天殖民者心情 -3 ----------
 public class GameCondition_TwilightYear : GameCondition {
  static readonly SkyColorSet Sky=new SkyColorSet(new Color(.62f,.30f,.26f),new Color(.32f,.18f,.18f),new Color(.75f,.42f,.36f),.85f);
  public override int TransitionTicks=>2500;
  public override float SkyTargetLerpFactor(Map map)=>GameConditionUtility.LerpInOutValue(this,TransitionTicks,.55f);
  public override SkyTarget? SkyTarget(Map map)=>new SkyTarget(.7f,Sky,1f,1f);
  public bool Ominous=>TicksPassed<Twilight.MoodDays*GenDate.TicksPerDay;
 }
 public class ThoughtWorker_Twilight : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(p.Faction!=Faction.OfPlayer || Twilight.Condition==null)return false;
   var c=Find.World?.gameConditionManager?.GetActiveCondition(Twilight.Condition) as GameCondition_TwilightYear;
   return c!=null && c.Ominous;
  }
 }

 // ---------- 黄昏之兆：每年掷一次（25%），15 天后开始。第 2 年起在每年第 1 天掷；第 1 年在第 5～45 天里随机一天掷（1.97.0 补丁） ----------
 public class WorldComponent_Twilight : WorldComponent {
  public const float YearChance=.25f;
  public const int FirstYearMinDay=5,FirstYearMaxDay=45;
  public int startTick=-1,rolledYear=-1,firstRollTick=-1;
  public WorldComponent_Twilight(World world):base(world){}
  public override void WorldComponentTick(){
   int now=Find.TickManager.TicksGame;if(now%GenDate.TicksPerHour!=311)return;
   if(startTick>=0 && now>=startTick){startTick=-1;Twilight.Start();return;}
   int year=GenDate.YearsPassed;
   if(year==0 && rolledYear<0){
    if(firstRollTick<0)firstRollTick=Rand.RangeInclusive(FirstYearMinDay,FirstYearMaxDay)*GenDate.TicksPerDay;
    if(now>=firstRollTick){rolledYear=0;YearRoll();}
   }
   else if(year>=1 && year!=rolledYear){rolledYear=year;YearRoll();}
  }
  void YearRoll(){if(LichMod.Settings.twilightYear && startTick<0 && !Twilight.Active && Twilight.Roll<YearChance)Twilight.Omen();}
  public float DaysUntilStart=>startTick<0?-1f:(startTick-Find.TickManager.TicksGame)/(float)GenDate.TicksPerDay;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref startTick,"lpTwilightStart",-1);Scribe_Values.Look(ref rolledYear,"lpTwilightYear",-1);Scribe_Values.Look(ref firstRollTick,"lpTwilightFirstRoll",-1);}
 }
 public class IncidentWorker_TwilightOmen : IncidentWorker {
  protected override bool CanFireNowSub(IncidentParms parms)=>LichMod.Settings.twilightYear && Twilight.World!=null && Twilight.World.startTick<0 && !Twilight.Active;
  protected override bool TryExecuteWorker(IncidentParms parms){if(!CanFireNowSub(parms))return false;Twilight.Omen();return true;}
 }
 public static class TwilightDebug {
  [DebugAction("DND边缘大陆","立即开始黄昏之年",allowedGameStates=AllowedGameStates.Playing)]
  static void StartNow(){Twilight.Start();}
  [DebugAction("DND边缘大陆","立即结束黄昏之年",allowedGameStates=AllowedGameStates.Playing)]
  static void EndNow(){Twilight.End();}
 }

 // ---------- 神明附体（状态） ----------
 public class Hediff_DivineVessel : HediffWithComps {
  public LP_DeityDef deity;
  public bool revived;          // 洛山达“复苏”用过了
  public int furnace;           // 苏尔特“炉心”层数
  public bool sureHit;          // 维拉基斯“星界突袭”后下一次近战必中
  public bool calledHunt;       // 马拉尔“狩猎号令”只在进场时一次
  public Dictionary<string,int> ready=new Dictionary<string,int>();
  public List<Pawn> timedPawns=new List<Pawn>();public List<int> timedUntil=new List<int>(); // 伊尔斯维狂乱、马拉尔驯兽反噬：到时恢复
  public List<Pawn> hunted=new List<Pawn>();
  public IntVec3 lastCell=IntVec3.Invalid;
  int tickAcc;
  public LP_DeityDef Deity=>deity;
  public string Key=>deity?.key;
  public override string LabelInBrackets=>deity==null?null:deity.label+"的化身容器";
  public override Color LabelColor=>deity?.color??base.LabelColor;
  public override string TipStringExtra=>base.TipStringExtra+(deity==null?"":"\n"+deity.description);
  HediffStage stage;string stageKey;
  public override HediffStage CurStage{get{
   if(stage!=null && stageKey==Key)return stage;stageKey=Key;
   stage=new HediffStage{blocksMentalBreaks=true,statFactors=new List<StatModifier>(),statOffsets=new List<StatModifier>()};
   switch(Key){
    case "Surtr":stage.statFactors.Add(new StatModifier{stat=StatDefOf.Flammability,value=0f});break;
    case "Ilsvi":stage.statFactors.Add(new StatModifier{stat=StatDefOf.PsychicSensitivity,value=0f});break;
    case "Tyr":stage.painFactor=0f;break;
    case "Malar":stage.statFactors.Add(new StatModifier{stat=StatDefOf.MeleeDamageFactor,value=TwilightSkills.MalarMelee});break;
   }
   return stage;
  }}
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);pawn.Drawer?.renderer?.SetAllGraphicsDirty();Track(this,true);}
  public override void PostRemoved(){base.PostRemoved();pawn?.Drawer?.renderer?.SetAllGraphicsDirty();TwilightSkills.ReleaseTimed(this,true);Track(this,false);}
  // 1.58.6：现存的化身容器登记（PostAdd / 读档加入，PostRemoved 移除，小人销毁后清掉）。提尔审判之庭挂在每个能力的 CanCast / GizmoDisabled 上，
  // 地图上没有任何已生成的化身容器时直接返回，不再扫小人。
  static readonly HashSet<Hediff_DivineVessel> live=new HashSet<Hediff_DivineVessel>();
  static void Track(Hediff_DivineVessel h,bool on){lock(live){if(on)live.Add(h);else live.Remove(h);}}
  public static bool AnySpawned(){
   lock(live){
    if(live.Count==0)return false;
    live.RemoveWhere(h=>h.pawn==null || h.pawn.Destroyed || h.pawn.Discarded);
    foreach(var h in live)if(h.pawn.Spawned)return true;
    return false;
   }
  }
  public bool Ready(string skill,int cooldown){int now=Find.TickManager.TicksGame;if(ready.TryGetValue(skill,out int t) && now<t)return false;ready[skill]=now+cooldown;return true;}
  public void ResetCooldowns()=>ready.Clear();
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if((tickAcc+=delta)<60)return;int e=tickAcc;tickAcc=0;
   if(pawn==null || pawn.Dead || pawn.Downed || !pawn.Spawned || deity==null)return;
   TwilightSkills.Tick(this,e);
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Defs.Look(ref deity,"lpDeity");Scribe_Values.Look(ref revived,"lpRevived");Scribe_Values.Look(ref furnace,"lpFurnace");Scribe_Values.Look(ref sureHit,"lpSureHit");Scribe_Values.Look(ref calledHunt,"lpCalledHunt");
   Scribe_Values.Look(ref lastCell,"lpLastCell",IntVec3.Invalid);
   Scribe_Collections.Look(ref ready,"lpReady",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.Saving){for(int i=timedPawns.Count-1;i>=0;i--)if(timedPawns[i]==null || timedPawns[i].Destroyed){timedPawns.RemoveAt(i);timedUntil.RemoveAt(i);}hunted.RemoveAll(p=>p==null || p.Destroyed);}
   Scribe_Collections.Look(ref timedPawns,"lpTimedPawns",LookMode.Reference);Scribe_Collections.Look(ref timedUntil,"lpTimedUntil",LookMode.Value);
   Scribe_Collections.Look(ref hunted,"lpHunted",LookMode.Reference);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    Track(this,true);
    if(ready==null)ready=new Dictionary<string,int>();if(timedPawns==null)timedPawns=new List<Pawn>();if(timedUntil==null)timedUntil=new List<int>();
    if(timedPawns.Count!=timedUntil.Count){timedPawns.Clear();timedUntil.Clear();}
    for(int i=timedPawns.Count-1;i>=0;i--)if(timedPawns[i]==null){timedPawns.RemoveAt(i);timedUntil.RemoveAt(i);}
    hunted=hunted?.Where(p=>p!=null).ToList()??new List<Pawn>();
   }
  }
 }
 // 附体光晕：同一张白色光晕贴图，按神的颜色染色。
 public class PawnRenderNode_DivineHalo : PawnRenderNode {
  public PawnRenderNode_DivineHalo(Pawn pawn,PawnRenderNodeProperties props,PawnRenderTree tree):base(pawn,props,tree){}
  public override Color ColorFor(Pawn pawn){var c=Twilight.VesselDeity(pawn)?.color??Color.white;c.a=.85f;return c;}
 }
 // 曾经的化身容器（离体后留下的标记）：伊尔斯维的脑髓烧尽、提尔的投降者。
 public class Hediff_FormerVessel : HediffWithComps {
  public LP_DeityDef deity;
  public override string LabelInBrackets=>deity?.label;
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref deity,"lpDeity");}
 }
}
