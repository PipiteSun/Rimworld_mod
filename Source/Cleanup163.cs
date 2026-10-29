using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.63.0 清理版：DND 警报（原版右侧警报栏）、巨魔尸体过滤与“标记焚烧”。
 public partial class Settings {
  public bool dndAlerts=true;
  void ExposeCleanup(){Scribe_Values.Look(ref dndAlerts,"dndAlerts",true);}
  // 1.63.0：敌人冒险者等级 / 敌人传奇等级原来各存两份（平时、Isekai 共存）。现在存一个新键（1 / 0）。
  // 读老设置（只有旧键、还没有新键）时一律重置为开启：共存那份旧默认是关，按“任一份开”合并仍会让玩家的敌人停在 1 级。
  // 旧键不再读也不再写（老设置文件里留着也照常加载）；之后按新键。legacyKey 参数只是记录旧键名。
  internal static bool ExposeMergedToggle(bool value,string key,string legacyKey,string legacyCoexistKey){
   int merged=Scribe.mode==LoadSaveMode.Saving?(value?1:0):-1;
   Scribe_Values.Look(ref merged,key,-1);
   if(Scribe.mode!=LoadSaveMode.LoadingVars)return value;
   return merged<0 || merged==1;
  }
 }
 public static class CleanupSettings {
  public static void Draw(Listing_Standard l){
   l.CheckboxLabeled("DND 警报",ref LichMod.Settings.dndAlerts,"在右侧警报栏提示：巨魔尸体将会复活、蝌蚪化形中、龙蛋即将孵化、巫妖复生中、命匣不在殖民地、脑髓不足。点警报会跳到对应目标。");
   l.GapLine();
  }
 }

 // 1.63.0：耐瑟“冥想式阅读”故意用阅读工作、算冥想娱乐；只跳过原版“工作的娱乐类型与本定义不一致”这一条定义检查，其余照常。
 public class JoyGiverDef_CrossKind : JoyGiverDef {
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())if(!(e.Contains("joyKind") && e.Contains("does not match")))yield return e;}
 }

 // ---------- 警报 ----------
 // 结果缓存 60 刻：警报栏每帧都会问，这里最多每秒扫一次。
 public abstract class Alert_LP : Alert {
  int lastTick=-99999;Game lastGame;
  protected readonly List<GlobalTargetInfo> targets=new List<GlobalTargetInfo>();
  protected readonly List<string> lines=new List<string>();
  protected abstract void Collect();
  protected abstract string Head{get;}
  protected virtual string Tail=>null;
  public void Refresh(){
   targets.Clear();lines.Clear();lastTick=Find.TickManager?.TicksGame??0;lastGame=Current.Game;
   if(LichMod.Settings?.dndAlerts!=false && Current.Game!=null)Collect();
  }
  void Ensure(){if(Current.Game!=lastGame || Find.TickManager.TicksGame-lastTick>=60 || Find.TickManager.TicksGame<lastTick)Refresh();}
  public IReadOnlyList<GlobalTargetInfo> Targets{get{Ensure();return targets;}}
  public override AlertReport GetReport(){Ensure();return targets.Count==0?AlertReport.Inactive:AlertReport.CulpritsAre(targets.ToList());}
  public override string GetLabel(){Ensure();return defaultLabel+(targets.Count>1?" ×"+targets.Count:"");}
  public override TaggedString GetExplanation(){Ensure();return Head+"\n\n"+string.Join("\n",lines)+(Tail.NullOrEmpty()?"":"\n\n"+Tail);}
  protected static string Days(int ticks)=>(Math.Max(0,ticks)/(float)GenDate.TicksPerDay).ToString("0.#")+" 天";
  protected static string Hours(int ticks)=>(Math.Max(0,ticks)/2500f).ToString("0.#")+" 小时";
  protected static IEnumerable<Map> Maps=>Find.Maps??Enumerable.Empty<Map>();
 }

 public static class CleanupRules {
  // 巨魔尸体：火葬场过滤器、警报和“标记焚烧”用同一个判断（复活规则也是按这个基因判断是不是巨魔）。
  public static bool IsTrollCorpse(Thing t)=>t is Corpse c && Troll.Gene(c.InnerPawn)?.Active==true;
  public static bool WillRevive(Corpse c)=>LichMod.Settings.trollRevival && IsTrollCorpse(c) && Troll.Gene(c.InnerPawn).reviveTick>=0 && !c.Destroyed;
  // 本方巫妖的命匣：主人是本方的人（活着或死了）。
  public static IEnumerable<Phylactery> PlayerBoxes(){
   var reg=Necro.Registry;if(reg==null)yield break;
   foreach(var b in reg.boxes)if(b!=null && !b.Destroyed && b.soul!=null && !b.soul.Discarded && b.soul.Faction==Faction.OfPlayer)yield return b;
  }
  public static Pawn Holder(Thing t){var h=t?.ParentHolder;return h is Pawn_CarryTracker c?c.pawn:h is Pawn_InventoryTracker i?i.pawn:h is Pawn_EquipmentTracker e?e.pawn:null;}
  // 命匣不在殖民地的原因；在殖民地里返回 null。拉洛克收走的（耐瑟任务）不算。
  public static string Away(Phylactery b){
   if(NetherilQuest.PhylacteryLost(b.soul))return null;
   var holder=Holder(b);
   if(holder!=null && holder.Faction!=Faction.OfPlayer)return "被"+holder.LabelShort+"（"+(holder.Faction?.Name??"无派系")+"）带着";
   var map=b.MapHeld;
   if(map==null)return holder!=null && holder.GetCaravan()!=null?"在商队“"+holder.GetCaravan().Name+"”里":"不在任何地图上";
   if(!map.IsPlayerHome)return "在别的地图上（"+map.Parent?.LabelCap+"）";
   if(holder!=null)return null; // 本方殖民者正拿着，在家园里
   if(!map.areaManager.Home[b.PositionHeld])return "在家园区外";
   return null;
  }
  public static GlobalTargetInfo Where(Thing t){
   if(t.SpawnedOrAnyParentSpawned)return new GlobalTargetInfo(t.SpawnedParentOrMe);
   var holder=Holder(t);var caravan=holder?.GetCaravan();if(caravan!=null)return caravan;
   return GlobalTargetInfo.Invalid;
  }
  public static float BrainPercent(Pawn p)=>MindFlayer.Cerebrum(p)?.ValuePercent??1f;
  public const float LowBrain=.2f,EggDays=3f;
  public static Func<CompDragonEgg,float> EggRateHook; // 自检用：不搭宝库也能给龙蛋一个孵化速度
  public static float EggDaysLeft(CompDragonEgg e){float r=EggRateHook?.Invoke(e)??e.Rate;return r<=0?float.PositiveInfinity:(e.HatchDays-e.progressDays)/r;}
 }

 public class Alert_LP_TrollRevive : Alert_LP {
  public Alert_LP_TrollRevive(){defaultLabel="巨魔尸体将会复活";defaultPriority=AlertPriority.High;}
  protected override string Head=>"这些巨魔的尸体还在再生，到时会原地站起来（敌方巨魔也一样）：";
  protected override string Tail=>"用火烧掉或火化才能彻底杀死。选中尸体可以点“标记焚烧”，让搬运工优先送到火葬场。";
  protected override void Collect(){
   foreach(var map in Maps)foreach(var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)){
    if(!(t is Corpse c) || !CleanupRules.WillRevive(c))continue;
    var g=Troll.Gene(c.InnerPawn);
    targets.Add(new GlobalTargetInfo(c.SpawnedParentOrMe));
    lines.Add("  "+c.InnerPawn.LabelShort+(c.InnerPawn.HostileTo(Faction.OfPlayer)?"（敌方）":"")+"：约 "+Hours(g.reviveTick-Find.TickManager.TicksGame)+"后复活");
   }
  }
 }
 public class Alert_LP_Ceremorph : Alert_LP {
  public Alert_LP_Ceremorph(){defaultLabel="蝌蚪化形中";defaultPriority=AlertPriority.High;}
  protected override string Head=>"这些人脑中有夺心魔蝌蚪，化形完成后会变成夺心魔：";
  protected override string Tail=>"阻止办法：做“取出蝌蚪”手术；或服用抑灵剂冻结进度（成为半夺心魔，要一直服药）。";
  protected override void Collect(){
   foreach(var map in Maps)foreach(var p in map.mapPawns.AllPawnsSpawned){
    if(p.Dead || !(p.IsColonist || p.IsPrisonerOfColony || p.IsSlaveOfColony))continue;
    var h=Tadpoles.Of(p);if(h==null)continue;
    float per=Tadpoles.DailyProgress(p);string left=h.Frozen?"已被抑灵剂冻结":per<=0?"进度停止":"约 "+((1f-h.Severity)/per).ToString("0.#")+" 天后完成";
    targets.Add(p);lines.Add("  "+p.LabelShort+(p.IsPrisonerOfColony?"（囚犯）":"")+"：进度 "+h.Severity.ToStringPercent("0")+"，"+left);
   }
  }
 }
 public class Alert_LP_DragonEgg : Alert_LP {
  public Alert_LP_DragonEgg(){defaultLabel="龙蛋即将孵化";defaultPriority=AlertPriority.Medium;}
  protected override string Head=>"这些龙蛋 "+CleanupRules.EggDays+" 天内会孵化：";
  protected override string Tail=>"孵出的幼龙归宝库主人的派系；放在家里时归殖民地。";
  protected override void Collect(){
   foreach(var map in Maps)foreach(var t in map.listerThings.AllThings){
    var e=t.TryGetComp<CompDragonEgg>();if(e==null)continue;
    float d=CleanupRules.EggDaysLeft(e);if(d>CleanupRules.EggDays)continue;
    targets.Add(new GlobalTargetInfo(t.SpawnedParentOrMe));lines.Add("  "+t.LabelShort+"：约 "+Math.Max(0f,d).ToString("0.#")+" 天");
   }
  }
 }
 public class Alert_LP_LichRevival : Alert_LP {
  public Alert_LP_LichRevival(){defaultLabel="巫妖复生中";defaultPriority=AlertPriority.Medium;}
  protected override string Head=>"这些巫妖已经死去，灵魂在命匣里等待复生：";
  protected override string Tail=>"命匣被毁就再也不能复生；保护好它。";
  protected override void Collect(){
   foreach(var b in CleanupRules.PlayerBoxes()){
    if(b.soul?.Dead!=true || b.revivalTick<0)continue;
    var at=CleanupRules.Where(b);if(!at.IsValid)continue;
    int left=b.revivalTick-Find.TickManager.TicksGame;
    targets.Add(at);lines.Add("  "+b.soul.LabelShort+"："+(left>0?"约 "+Hours(left)+"后复生":"复生已就绪，等命匣旁边有空位")+"；命匣在"+(b.MapHeld!=null?(b.MapHeld.Parent?.LabelCap??"地图上"):"地图外"));
   }
  }
 }
 public class Alert_LP_PhylacteryAway : Alert_LP {
  public Alert_LP_PhylacteryAway(){defaultLabel="命匣不在殖民地";defaultPriority=AlertPriority.High;}
  protected override string Head=>"本方巫妖的命匣不在殖民地的家园区里：";
  protected override string Tail=>"巫妖死后只能在命匣旁边复生；命匣落到别人手里或被毁，巫妖就回不来了。";
  protected override void Collect(){
   foreach(var b in CleanupRules.PlayerBoxes()){
    string why=CleanupRules.Away(b);if(why==null)continue;
    var at=CleanupRules.Where(b);if(!at.IsValid)continue;
    targets.Add(at);lines.Add("  "+b.soul.LabelShort+"的命匣："+why);
   }
  }
 }
 public class Alert_LP_LowBrain : Alert_LP {
  public Alert_LP_LowBrain(){defaultLabel="脑髓不足";defaultPriority=AlertPriority.Medium;}
  protected override string Head=>"这些夺心魔的脑髓低于 "+CleanupRules.LowBrain.ToStringPercent("0")+"：";
  protected override string Tail=>"吞噬大脑（囚犯、尸体）或脑浆罐可以补充；脑髓耗尽会饥饿发狂。";
  protected override void Collect(){
   foreach(var map in Maps)foreach(var p in map.mapPawns.FreeColonistsSpawned){
    if(!MindFlayer.IsMindFlayer(p))continue;float v=CleanupRules.BrainPercent(p);if(v>=CleanupRules.LowBrain)continue;
    targets.Add(p);lines.Add("  "+p.LabelShort+"：脑髓 "+v.ToStringPercent("0"));
   }
  }
 }

 // ---------- 巨魔尸体过滤（火葬场“只烧巨魔”：取消勾选“非巨魔尸体”） ----------
 public class SpecialThingFilterWorker_TrollCorpse : SpecialThingFilterWorker {
  public override bool Matches(Thing t)=>CleanupRules.IsTrollCorpse(t);
  public override bool CanEverMatch(ThingDef def)=>def.IsCorpse && def.ingestible?.sourceDef?.race?.Humanlike==true;
 }
 public class SpecialThingFilterWorker_NonTrollCorpse : SpecialThingFilterWorker {
  public override bool Matches(Thing t)=>t is Corpse && !CleanupRules.IsTrollCorpse(t);
  public override bool CanEverMatch(ThingDef def)=>def.IsCorpse;
 }

 // ---------- 标记焚烧 ----------
 // 改动最小的做法：不加新的工作，只在火葬场最前面放一张“只烧巨魔”的火化账单（重复，不限人），并把尸体解禁；原版搬运 / 火化工作会去拿它。
 public static class TrollCremation {
  public const string BillLabel="只烧巨魔";
  static RecipeDef cremate;static SpecialThingFilterDef nonTroll;
  public static RecipeDef Cremate=>cremate??(cremate=DefDatabase<RecipeDef>.GetNamedSilentFail("CremateCorpse"));
  public static SpecialThingFilterDef NonTroll=>nonTroll??(nonTroll=DefDatabase<SpecialThingFilterDef>.GetNamedSilentFail("LP_AllowNonTrollCorpses"));
  public static IEnumerable<Building_WorkTable> Crematoria(Map map)=>map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>().Where(b=>Cremate!=null && b.def.AllRecipes.Contains(Cremate));
  public static Bill_Production BillOn(Building_WorkTable b)=>b.BillStack.Bills.OfType<Bill_Production>().FirstOrDefault(x=>x.recipe==Cremate && NonTroll!=null && !x.ingredientFilter.Allows(NonTroll));
  // 返回用到的账单；没有火葬场时返回 null 并提示。
  public static Bill_Production Mark(Corpse c,bool message=true){
   if(c?.MapHeld==null || !CleanupRules.IsTrollCorpse(c))return null;
   c.SetForbidden(false,false);
   var map=c.MapHeld;var crem=Crematoria(map).OrderBy(b=>b.Position.DistanceToSquared(c.PositionHeld)).FirstOrDefault();
   if(crem==null){if(message)Messages.Message("没有火葬场：先建一座电火葬场，或者直接用火烧掉"+c.InnerPawn.LabelShort+"的尸体。",c,MessageTypeDefOf.RejectInput,false);return null;}
   var bill=BillOn(crem);
   if(bill==null){
    bill=(Bill_Production)Cremate.MakeNewBill();bill.repeatMode=BillRepeatModeDefOf.Forever;
    if(NonTroll!=null)bill.ingredientFilter.SetAllow(NonTroll,false);
    crem.BillStack.AddBill(bill);
   }
   var list=crem.BillStack.Bills;if(list.IndexOf(bill)>0){list.Remove(bill);list.Insert(0,bill);}
   bill.suspended=false;
   if(message)Messages.Message("已标记焚烧："+c.InnerPawn.LabelShort+"的尸体会优先送到"+crem.LabelShort+"（不收非巨魔尸体的火化账单排在最前）。",c,MessageTypeDefOf.TaskCompletion,false);
   return bill;
  }
 }
 [HarmonyPatch(typeof(Corpse),nameof(Corpse.GetGizmos))]
 public static class TrollCorpseBurnGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Corpse __instance){
   foreach(var g in __result)yield return g;
   if(__instance.Spawned && CleanupRules.IsTrollCorpse(__instance) && Find.Selector.SingleSelectedThing==__instance)
    yield return new Command_Action{defaultLabel="标记焚烧",defaultDesc="让搬运工优先把这具巨魔尸体送去火化：在最近的火葬场最前面放一张“只烧巨魔”的火化账单，并解除禁用。没有火葬场时只给提示。",icon=ContentFinder<Texture2D>.Get("UI/Designators/Strip",false)??BaseContent.BadTex,action=()=>TrollCremation.Mark(__instance)};
  }
 }
 // ---------- 4.1 五色真龙：地形、水中、黑暗 ----------
 public static class DragonTerrain {
  public static bool IceWalker(Pawn p)=>Dragons.ColorOf(p)=="White";
  public static bool BogWalker(Pawn p){var c=Dragons.ColorOf(p);return c=="Green" || c=="Black";}
  static bool Has(string n,params string[] keys){foreach(var k in keys)if(n.IndexOf(k,StringComparison.OrdinalIgnoreCase)>=0)return true;return false;}
  public static bool IceTerrain(TerrainDef t)=>t!=null && Has(t.defName,"Ice","Snow","Glacier");
  // 浅水、沼泽、泥地；深水原版不可通行，这里不改通行性。
  public static bool BogTerrain(TerrainDef t)=>t!=null && t.passability!=Traversability.Impassable && (t.IsWater || Has(t.defName,"Marsh","Mud","Bog","Swamp"));
  public static bool InWater(Pawn p)=>p?.Spawned==true && BogTerrain(p.Position.GetTerrain(p.Map));
  public static bool? TestDark; // 自检用
  public static bool InDark(Pawn p)=>p?.Spawned==true && (TestDark??p.Map.glowGrid.PsychGlowAt(p.Position)==PsychGlow.Dark);
  public const float WaterShot=.7f,DarkHit=.1f,ShadowShot=.8f;
  // 走进这一格只算基础步长（地形倍率按 1）。
  public static bool FreeCell(Pawn p,IntVec3 c){
   if(p?.Map==null || !c.InBounds(p.Map))return false;var t=c.GetTerrain(p.Map);
   if(IceWalker(p) && (IceTerrain(t) || p.Map.snowGrid.GetDepth(c)>0f))return true;
   return BogWalker(p) && BogTerrain(t);
  }
  public static float BaseCost(Pawn p,IntVec3 c)=>c.x!=p.Position.x && c.z!=p.Position.z?p.TicksPerMoveDiagonal:p.TicksPerMoveCardinal;
 }
 [HarmonyPatch(typeof(Pawn_PathFollower),"CostToMoveIntoCell",new[]{typeof(Pawn),typeof(IntVec3)})]
 public static class DragonTerrainCost {
  static void Postfix(Pawn pawn,IntVec3 c,ref float __result){if(Dragons.IsChromatic(pawn) && DragonTerrain.FreeCell(pawn,c))__result=Math.Min(__result,DragonTerrain.BaseCost(pawn,c));}
 }
 // 绿龙 / 黑龙：寻路也不绕开水格（原版 1.7 的按角色水格成本）。
 [HarmonyPatch(typeof(Pawn),"get_WaterCellCost")]
 public static class DragonWaterCellCost {
  static void Postfix(Pawn __instance,ref int? __result){if(DragonTerrain.BogWalker(__instance))__result=0;}
 }
 // 远程命中：在水里的绿龙 / 黑龙 ×0.7、暗处的影魔 ×0.8（目标体型系数，和斗篷类的写法一样）；黑龙在黑暗中射击命中 +10%。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class CleanupShotReport {
  static readonly AccessTools.StructFieldRef<ShotReport,float> size=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromTargetSize");
  static readonly AccessTools.StructFieldRef<ShotReport,float> shooter=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromShooterAndDist");
  public static float TargetFactor(Pawn t){
   if(t==null)return 1f;float f=1f;
   if(DragonTerrain.BogWalker(t) && DragonTerrain.InWater(t))f*=DragonTerrain.WaterShot;
   if(AbyssRaces.Is(t,AbyssRaces.ShadowDemon) && DragonTerrain.InDark(t))f*=DragonTerrain.ShadowShot;
   return f;
  }
  public static float ShooterFactor(Pawn a)=>a!=null && Dragons.ColorOf(a)=="Black" && DragonTerrain.InDark(a)?1f+DragonTerrain.DarkHit:1f;
  static void Postfix(Thing caster,LocalTargetInfo target,ref ShotReport __result){
   float tf=TargetFactor(target.Thing as Pawn);if(tf!=1f)size(ref __result)*=tf;
   float sf=ShooterFactor(caster as Pawn);if(sf!=1f)shooter(ref __result)=Mathf.Min(1f,shooter(ref __result)*sf);
  }
 }
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class DragonDarkMelee {
  static void Postfix(Verb_MeleeAttack __instance,ref float __result){if(__result<1f && CleanupShotReport.ShooterFactor(__instance.CasterPawn)>1f)__result=Mathf.Min(1f,__result+DragonTerrain.DarkHit);}
 }
 // 黑龙巢穴：只有那张地图永久日食。
 [HarmonyPatch(typeof(GenStep_DragonLair),nameof(GenStep_DragonLair.Build))]
 public static class BlackLairEclipse {
  public static void Apply(Map map){
   if(map==null || map.gameConditionManager.ConditionIsActive(GameConditionDefOf.Eclipse))return;
   var c=GameConditionMaker.MakeConditionPermanent(GameConditionDefOf.Eclipse);map.gameConditionManager.RegisterCondition(c);
  }
  static void Postfix(Map map,DragonLair lair){if(lair?.color=="Black")Apply(map);}
 }

 // ---------- 4.3 魅魔 / 梦魔被识破：周围的人「被欺骗」 ----------
 public static class SuccubusDeceit {
  public const float Radius=12f;
  static ThoughtDef def;public static ThoughtDef Deceived=>def??(def=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_Deceived"));
  public static int Apply(Pawn s){
   if(s?.MapHeld==null || Deceived==null)return 0;int n=0;
   foreach(var p in s.MapHeld.mapPawns.FreeColonistsSpawned)if(p!=s && p.needs?.mood?.thoughts?.memories!=null && p.Position.InHorDistOf(s.PositionHeld,Radius)){p.needs.mood.thoughts.memories.TryGainMemory(Deceived,s);n++;}
   return n;
  }
 }
 [HarmonyPatch(typeof(AbyssEvents),nameof(AbyssEvents.RevealSuccubus))]
 public static class SuccubusRevealDeceit {
  static void Prefix(Pawn p,out bool __state){__state=p!=null && !p.Dead && Outsiders.Disguised(p);}
  static void Postfix(Pawn p,bool __state){if(__state && !Outsiders.Disguised(p))SuccubusDeceit.Apply(p);}
 }
 // 倒地时幻象碎掉：伪装中的来访魅魔现形。
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class SuccubusDownedReveal {
  static void Postfix(Pawn ___pawn){var p=___pawn;if(p!=null && !p.Dead && Outsiders.Disguised(p))Fiends.Defer(()=>AbyssEvents.RevealSuccubus(p,"倒地时幻象碎了"));}
 }

 // ---------- 4.4 狗头人刺坑：利刃 20、穿甲 15%、体型 >1.5 减半，触发 3 次后要重新布置 ----------
 public class Building_KoboldSpikePit : Building_TrapDamager {
  public const float Damage=20f,ArmorPen=.15f,BigBody=1.5f,BigFactor=.5f;public const int Uses=3;
  public int springs;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref springs,"lpSpikeSprings",0);}
  public static float DamageTo(Pawn p,Thing pit)=>Damage*(StatPart_KoboldTrap.Marked(pit)?Subraces.TrapFactor:1f)*(p!=null && p.BodySize>BigBody?BigFactor:1f);
  protected override void SpringSub(Pawn p){
   if(p!=null && !p.Dead)p.TakeDamage(new DamageInfo(DamageDefOf.Stab,DamageTo(p,this),ArmorPen,-1f,this));
   springs++;
   if(springs>=Uses){var map=Map;var pos=Position;Fiends.Defer(()=>{if(!Destroyed){Messages.Message("狗头人刺坑用坏了（已触发 "+Uses+" 次），需要重新布置。",new TargetInfo(pos,map),MessageTypeDefOf.NeutralEvent,false);Destroy(DestroyMode.KillFinalize);}});}
  }
  public override string GetInspectString(){var s=base.GetInspectString();return (s.NullOrEmpty()?"":s+"\n")+"剩余次数："+Math.Max(0,Uses-springs)+" / "+Uses;}
 }

 // ---------- 4.5 遗迹 / 古代宝箱：同调者在场 15% 一件魔法饰品；5% 一本属性典籍 ----------
 public static class AncientCrateLoot {
  public const float AttunerChance=.15f,TomeChance=.05f;
  public static float? TestRoll;
  public static bool AttunerPresent(Map map)=>map!=null && map.mapPawns.FreeColonistsSpawned.Any(p=>HeroTraits.Info(p)?.attuner==true);
  static List<ThingDef> accessories;
  public static List<ThingDef> Accessories=>accessories??(accessories=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.HasModExtension<AccessorySlot>() && d.tradeability!=Tradeability.None).ToList());
  // 返回额外掉落；随机数按箱子编号播种（同一个箱子结果固定）。
  public static List<Thing> Roll(Thing crate,bool attuner){
   var list=new List<Thing>();Rand.PushState(crate.thingIDNumber^0x163A);
   try{
    if(attuner && (TestRoll??Rand.Value)<AttunerChance && Accessories.Count>0){var d=Accessories.RandomElement();var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityReward(),ArtGenerationContext.Outsider);list.Add(t);}
    if((TestRoll??Rand.Value)<TomeChance){var tm=Growth.MakeTome();if(tm!=null)list.Add(tm);}
   }finally{Rand.PopState();}
   return list;
  }
 }
 [HarmonyPatch(typeof(Building_Crate),nameof(Building_Crate.Open))]
 public static class AncientCrateOpen {
  static void Prefix(Building_Crate __instance,out (Map map,IntVec3 cell) __state){__state=(__instance.MapHeld,__instance.PositionHeld);}
  static void Postfix(Building_Crate __instance,(Map map,IntVec3 cell) __state){
   try{
    var map=__state.map;if(map==null || !__state.cell.IsValid)return;
    foreach(var t in AncientCrateLoot.Roll(__instance,AncientCrateLoot.AttunerPresent(map))){GenPlace.TryPlaceThing(t,__state.cell,map,ThingPlaceMode.Near);Messages.Message("箱子里还有："+t.LabelCap+"。",t,MessageTypeDefOf.PositiveEvent,false);}
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 古代宝箱额外掉落出错："+e,16301163);}
  }
 }

 // ---------- 4.2 眼柄折扣：书桌上的卷轴配方各多一个“用 1 根眼柄代替一半主要材料”的版本 ----------
 [StaticConstructorOnStartup]
 public static class EyestalkRecipes {
  public const string Suffix="_Eyestalk";
  public static readonly List<RecipeDef> Made=new List<RecipeDef>();
  static EyestalkRecipes(){try{Build();}catch(Exception e){Log.Error("[DND边缘大陆] 眼柄配方出错："+e);}}
  static void Build(){
   var stalk=DefDatabase<ThingDef>.GetNamedSilentFail("LP_BeholderStalk");var desk=DefDatabase<ThingDef>.GetNamedSilentFail("LP_ScribeDesk");if(stalk==null || desk==null)return;
   foreach(var r in DefDatabase<RecipeDef>.AllDefsListForReading.Where(x=>x.recipeUsers!=null && x.recipeUsers.Contains(desk) && x.defName.StartsWith("LP_WriteScroll") && x.ingredients.Count>=2).ToList()){
    if(DefDatabase<RecipeDef>.GetNamedSilentFail(r.defName+Suffix)!=null)continue;
    var main=r.ingredients.Last();var mainDef=main.filter.AllowedThingDefs.FirstOrDefault();if(mainDef==null || mainDef==ThingDefOf.Cloth)continue;
    var v=new RecipeDef{defName=r.defName+Suffix,label=r.label+"（眼柄）",description=r.description+"\n\n眼柄版本：用 1 根眼魔眼柄代替一半的"+mainDef.label+"。",jobString=r.jobString,workAmount=r.workAmount,workSpeedStat=r.workSpeedStat,workSkill=r.workSkill,effectWorking=r.effectWorking,soundWorking=r.soundWorking,
     recipeUsers=r.recipeUsers.ToList(),researchPrerequisite=r.researchPrerequisite,allowMixingIngredients=r.allowMixingIngredients,skillRequirements=r.skillRequirements?.ToList(),products=r.products.ToList(),modExtensions=r.modExtensions?.ToList(),modContentPack=r.modContentPack};
    v.ingredients=new List<IngredientCount>();
    foreach(var ic in r.ingredients){
     var n=new IngredientCount();foreach(var d in ic.filter.AllowedThingDefs)n.filter.SetAllow(d,true);
     n.SetBaseCount(ic==main?Mathf.Max(1f,Mathf.Ceil(ic.GetBaseCount()*.5f)):ic.GetBaseCount());v.ingredients.Add(n);
    }
    var s=new IngredientCount();s.filter.SetAllow(stalk,true);s.SetBaseCount(1f);v.ingredients.Add(s);
    v.fixedIngredientFilter=new ThingFilter();foreach(var ic in v.ingredients)foreach(var d in ic.filter.AllowedThingDefs)v.fixedIngredientFilter.SetAllow(d,true);
    v.defaultIngredientFilter=new ThingFilter();v.defaultIngredientFilter.CopyAllowancesFrom(v.fixedIngredientFilter);
    DefDatabase<RecipeDef>.Add(v);v.ResolveReferences();Made.Add(v);
   }
   var cache=AccessTools.Field(typeof(ThingDef),"allRecipesCached");desk.recipes=null;cache?.SetValue(desk,null);
  }
 }
}