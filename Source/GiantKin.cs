using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.30.0 巨人通用层。血统写在本源基因的 GiantKinExtension 上：火巨人 rank 3、霜巨人 rank 4，预留风暴巨人 1、云巨人 2（数字越小地位越高）。
 // 以后“是不是巨人”都走 GiantKin.Of；食人魔、泰坦不是巨人血统（迁徙里仍算“巨人”，见 GiantMigration.IsGiant）。
 public class GiantKinExtension : DefModExtension {
  public int rank=9;public string element,label;
  public float adultSize=2f,renderScale=-1f; // 成年体型倍率（儿童随年龄长大）；渲染缩放≤0时跟随体型
  public float RenderScale=>renderScale>0?renderScale:adultSize;
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(rank<1)yield return "GiantKinExtension.rank must be at least 1";
   if(element.NullOrEmpty())yield return "GiantKinExtension.element is required";
   if(adultSize<1f || adultSize>4f || RenderScale<1f || RenderScale>4f)yield return "GiantKinExtension sizes must be in [1,4]";
  }
 }
 // 巨人专属衣甲（只有巨人能穿，每件 mood 点“合身的巨人之衣”）与巨人专用武器。
 public class GiantApparelExtension : DefModExtension {public int mood=2;}
 public class GiantWeaponExtension : DefModExtension {}

 public static class GiantKin {
  public const float AdultAge=18f;
  static List<GeneDef> genes;static int geneCount=-1;
  public static List<GeneDef> Genes{get{
   if(genes==null || geneCount!=DefDatabase<GeneDef>.DefCount){geneCount=DefDatabase<GeneDef>.DefCount;genes=DefDatabase<GeneDef>.AllDefsListForReading.Where(g=>g.GetModExtension<GiantKinExtension>()!=null).ToList();}
   return genes;
  }}
  // 按角色缓存最高位的血统；Notify_GenesChanged 时失效，基因数量变化也算失效。
  sealed class Entry {public GiantKinExtension ext;public int genes=-1;public bool valid;}
  static readonly ConditionalWeakTable<Pawn,Entry> cache=new ConditionalWeakTable<Pawn,Entry>();
  public static void Invalidate(Pawn p){if(p!=null && cache.TryGetValue(p,out var e))e.valid=false;}
  public static GiantKinExtension Of(Pawn p){
   if(p?.genes==null)return null;
   var list=Genes;if(list.Count==0)return null;
   var e=cache.GetOrCreateValue(p);int n=p.genes.GenesListForReading.Count;
   if(e.valid && e.genes==n)return e.ext;
   GiantKinExtension best=null;
   for(int i=0;i<list.Count;i++)if(p.genes.HasActiveGene(list[i])){var x=list[i].GetModExtension<GiantKinExtension>();if(best==null || x.rank<best.rank)best=x;}
   e.ext=best;e.genes=n;e.valid=true;
   return best;
  }
  public static bool Is(Pawn p)=>Of(p)!=null;
  public static bool Is(Pawn p,string element)=>Of(p)?.element==element;
  public static int Rank(Pawn p)=>Of(p)?.rank??0;
  public static string Label(Pawn p)=>Of(p)?.label??"巨人";
  // 儿童随年龄长大：成年（18岁）达到 adultSize。原来写在 FireGiant.Size 里。
  public static float Growth(Pawn p)=>Mathf.Clamp01((p?.ageTracker?.AgeBiologicalYearsFloat??AdultAge)/AdultAge);
  public static float Size(Pawn p,float adultSize)=>1f+(adultSize-1f)*Growth(p);
  public static float Size(Pawn p){var e=Of(p);return e==null?1f:Size(p,e.adultSize);}
  public static float RenderSize(Pawn p){var e=Of(p);return e==null?1f:Size(p,e.RenderScale);}
  // 殖民地（同一张地图上的自由殖民者与奴隶）里的巨人，每250刻重数一次。
  sealed class Census {public int tick=-99999;public readonly List<Pawn> giants=new List<Pawn>();}
  static readonly Dictionary<int,Census> census=new Dictionary<int,Census>();
  public static List<Pawn> ColonyGiants(Map map){
   if(map==null)return new List<Pawn>();
   int now=Find.TickManager?.TicksGame??0;
   if(!census.TryGetValue(map.uniqueID,out var c))census[map.uniqueID]=c=new Census();
   if(now-c.tick>=250 || now<c.tick){
    c.tick=now;c.giants.Clear();
    foreach(var p in map.mapPawns.FreeColonistsSpawned)if(Is(p))c.giants.Add(p);
    foreach(var p in map.mapPawns.SlavesOfColonySpawned)if(Is(p) && !c.giants.Contains(p))c.giants.Add(p);
   }
   return c.giants;
  }
  public static void ForgetCensus()=>census.Clear();
  public static bool ColonyMember(Pawn p)=>p!=null && p.Spawned && !p.Dead && (p.IsFreeColonist || p.IsSlaveOfColony);
 }

 // ---------- 衣甲与武器 ----------
 public static class GiantGear {
  public static GiantApparelExtension Ext(ThingDef d)=>d?.GetModExtension<GiantApparelExtension>();
  public static bool Exclusive(ThingDef d)=>Ext(d)!=null;
  public static bool Weapon(ThingDef d)=>d?.GetModExtension<GiantWeaponExtension>()!=null;
  // 设置“巨人穿普通衣服”（默认开）。关掉回到 1.29.0 的旧规则：巨人只能穿巨人专属衣甲。
  public static bool NormalAllowed=>LichMod.Settings?.giantsWearNormal!=false;
  public static bool CanWear(Pawn p,ThingDef d){
   if(p==null || d==null || !d.IsApparel)return true;
   // 1.44.0：带 GeneWearExtension 的装备（独眼透镜）只有带那个基因的人能戴。
   var g=d.GetModExtension<GeneWearExtension>();if(g!=null && !Races124.Has(p,g.gene))return false;
   bool giant=GiantKin.Is(p);
   if(Exclusive(d))return giant;
   return !giant || NormalAllowed;
  }
  public static string WearReason(Pawn p,ThingDef d){var g=d?.GetModExtension<GeneWearExtension>();if(g!=null && !Races124.Has(p,g.gene))return "只有"+g.label+"能戴。";return Exclusive(d)?"只有巨人能穿。":"巨人只能穿巨人专属衣甲（模组设置“巨人穿普通衣服”已关闭）。";}
  // 失去巨人血统（或改了设置）后，脱下不合规矩的衣甲、放下巨人武器。
  public static void Release(Pawn p){
   if(p==null)return;GiantKin.Invalidate(p);
   if(p.apparel!=null)foreach(var a in p.apparel.WornApparel.ToArray())if(!CanWear(p,a.def)){if(p.Spawned)p.apparel.TryDrop(a);else p.apparel.Remove(a);}
   if(p.equipment!=null && !GiantKin.Is(p))foreach(var w in p.equipment.AllEquipmentListForReading.ToArray()){
    if(!Weapon(w.def))continue;
    if(p.Spawned)p.equipment.TryDropEquipment(w,out _,p.Position);
    else if(p.inventory!=null)p.equipment.GetDirectlyHeldThings().TryTransferToContainer(w,p.inventory.innerContainer);
   }
  }
  // 合身的巨人之衣：每件专属衣甲 +mood（默认2），最多 +6；穿齐熔核禁卫三件（破城锤、炉心战盔、熔核重甲）另 +2，总上限 +8。
  public const int AttireCap=6,AttireMax=8,GuardBonus=2;
  public static int AttireMood(Pawn p){
   if(!GiantKin.Is(p) || p.apparel==null)return 0;
   int sum=0;var worn=p.apparel.WornApparel;
   for(int i=0;i<worn.Count;i++){var e=Ext(worn[i].def);if(e!=null)sum+=e.mood;}
   sum=Mathf.Min(AttireCap,sum);
   if(MoltenGuard.Complete(p))sum+=GuardBonus;
   return Mathf.Min(AttireMax,sum);
  }
 }
 public class ThoughtWorker_GiantAttire : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){int m=GiantGear.AttireMood(p);return m<=0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(Mathf.Clamp((m+1)/2-1,0,def.stages.Count-1));}
 }
 [HarmonyPatch(typeof(Pawn_GeneTracker),"Notify_GenesChanged")]
 public static class GiantKinGeneChanged {
  static void Postfix(Pawn_GeneTracker __instance){GiantKin.Invalidate(__instance.pawn);}
 }

 // ---------- 阶序（Ordning） ----------
 // 阶序井然：殖民地有地位更高的巨人 +3；统领众巨：有地位更低的巨人 +2；氏族之首：同种巨人（至少2个）里近战或手工最高 +3；同族之语：殖民地有至少2个巨人，各 +2。
 // 挑战首领：同种巨人里有人技能超过现任首领，触发一次辱骂级别的社交冲突，同一氏族30天内不重复。
 public static class Ordning {
  public const int ChallengeCooldown=30*GenDate.TicksPerDay,CheckInterval=2500;
  public static bool On=>LichMod.Settings?.ordningMood!=false;
  public static int Score(Pawn p){var s=p?.skills;if(s==null)return 0;return Math.Max(s.GetSkill(SkillDefOf.Melee)?.Level??0,s.GetSkill(SkillDefOf.Crafting)?.Level??0);}
  public static IEnumerable<Pawn> Others(Pawn p)=>GiantKin.ColonyGiants(p.MapHeld).Where(o=>o!=p && !o.Dead);
  public static bool Above(Pawn p){int r=GiantKin.Rank(p);return r>0 && Others(p).Any(o=>GiantKin.Rank(o)<r);}
  public static bool Below(Pawn p){int r=GiantKin.Rank(p);return r>0 && Others(p).Any(o=>GiantKin.Rank(o)>r);}
  // 同种巨人（element 相同）里分数最高的；同分取 thingIDNumber 小的。少于2人时没有首领。
  public static Pawn Head(Map map,string element){
   Pawn best=null;int n=0;
   foreach(var g in GiantKin.ColonyGiants(map)){
    if(g.Dead || GiantKin.Of(g)?.element!=element)continue;n++;
    if(best==null || Score(g)>Score(best) || (Score(g)==Score(best) && g.thingIDNumber<best.thingIDNumber))best=g;
   }
   return n>=2?best:null;
  }
  public static bool IsHead(Pawn p){var e=GiantKin.Of(p);return e!=null && Head(p.MapHeld,e.element)==p;}
  public static bool Tongue(Pawn p)=>GiantKin.Is(p) && Others(p).Any();
 }
 public abstract class ThoughtWorker_Ordning : ThoughtWorker {
  protected abstract bool Applies(Pawn p);
  protected override ThoughtState CurrentStateInternal(Pawn p)=>Ordning.On && GiantKin.ColonyMember(p) && GiantKin.Is(p) && Applies(p)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }
 public class ThoughtWorker_OrdningAbove : ThoughtWorker_Ordning {protected override bool Applies(Pawn p)=>Ordning.Above(p);}
 public class ThoughtWorker_OrdningBelow : ThoughtWorker_Ordning {protected override bool Applies(Pawn p)=>Ordning.Below(p);}
 public class ThoughtWorker_ClanHead : ThoughtWorker_Ordning {protected override bool Applies(Pawn p)=>Ordning.IsHead(p);}
 public class ThoughtWorker_GiantTongue : ThoughtWorker_Ordning {protected override bool Applies(Pawn p)=>Ordning.Tongue(p);}

 // 记住每张地图每个氏族的首领；换人时新首领向旧首领发起挑战（原版“辱骂”互动，失败时直接给旧首领“被辱骂”记忆）。
 public class GiantOrdningState : GameComponent {
  public Dictionary<string,int> heads=new Dictionary<string,int>(),lastChallenge=new Dictionary<string,int>();
  public GiantOrdningState(Game game){}
  public static GiantOrdningState Get=>GameComp<GiantOrdningState>.Get();
  public override void GameComponentTick(){if(Find.TickManager.TicksGame%Ordning.CheckInterval==911)Process();}
  public int Process(){
   if(!Ordning.On)return 0;int n=0;
   foreach(var map in Find.Maps){
    foreach(var element in GiantKin.ColonyGiants(map).Select(g=>GiantKin.Of(g)?.element).Where(x=>x!=null).Distinct().ToList()){
     string key=map.uniqueID+"|"+element;var head=Ordning.Head(map,element);
     if(head==null){heads.Remove(key);continue;}
     if(heads.TryGetValue(key,out int old) && old!=head.thingIDNumber){
      var prior=GiantKin.ColonyGiants(map).FirstOrDefault(g=>g.thingIDNumber==old);
      if(prior!=null && Challenge(head,prior,key))n++;
     }
     heads[key]=head.thingIDNumber;
    }
   }
   return n;
  }
  public bool Challenge(Pawn challenger,Pawn head,string key){
   int now=Find.TickManager.TicksGame;
   if(lastChallenge.TryGetValue(key,out int t) && now-t<Ordning.ChallengeCooldown && now>=t)return false;
   lastChallenge[key]=now;
   var insult=DefDatabase<InteractionDef>.GetNamedSilentFail("Insult");
   bool done=insult!=null && challenger.interactions!=null && challenger.interactions.TryInteractWith(head,insult);
   var insulted=DefDatabase<ThoughtDef>.GetNamedSilentFail("Insulted");
   if(!done && insulted!=null)head.needs?.mood?.thoughts?.memories?.TryGainMemory(insulted,challenger);
   Messages.Message(challenger.LabelShort+"的本事已经超过了"+head.LabelShort+"，当众向这位"+GiantKin.Label(head)+"首领发起了挑战。",new LookTargets(challenger,head),MessageTypeDefOf.NeutralEvent,false);
   return true;
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref heads,"lpGiantHeads",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref lastChallenge,"lpGiantChallenge",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(heads==null)heads=new Dictionary<string,int>();if(lastChallenge==null)lastChallenge=new Dictionary<string,int>();}
  }
 }

 // 1.30.0 设置：巨人穿普通衣服、阶序心情。
 public partial class Settings {
  public bool giantsWearNormal=true,ordningMood=true;
  void ExposeGiants(){Scribe_Values.Look(ref giantsWearNormal,"giantsWearNormal",true);Scribe_Values.Look(ref ordningMood,"ordningMood",true);Scribe_Values.Look(ref stormOmen,"stormOmen",true);}
 }
 public static class GiantSettings {
  public static void Draw(Listing_Standard l){
   l.GapLine();l.Label("巨人");
   l.CheckboxLabeled("巨人穿普通衣服",ref LichMod.Settings.giantsWearNormal,"火巨人、霜巨人（以后的巨人也一样）能穿任何普通衣服，普通衣服随巨躯一起放大显示。巨人专属衣甲（锻工外衣、熔铁/熔核装备、冬狼皮大氅）仍只有巨人能穿。关闭后回到1.29.0的旧规则：巨人只能穿专属衣甲；保存设置后，已穿着的普通衣服会在下次换装时脱下。");
   l.CheckboxLabeled("阶序心情",ref LichMod.Settings.ordningMood,"巨人的阶序（风暴＞云＞火＞霜＞双头＞独眼）：殖民地有更高位的巨人“阶序井然”+3、有更低位的“统领众巨”+2，同种巨人里近战或手工最高的“氏族之首”+3，有两个以上巨人时“同族之语”+2；有人本事超过首领时会挑战首领（30天一次）。关闭后这些心情和挑战都不出现（火巨人与霜巨人的“世仇”种族关系不受影响）。");
   l.CheckboxLabeled("风暴预兆",ref LichMod.Settings.stormOmen,"（1.34.0）家园地图上有成年的风暴巨人（带“风暴预兆”基因）时，点数≥1000的敌对袭击推迟12小时落地，并提前来信说明派系和方向。与竖琴手预警叠加（两者都触发时推迟时间相加）。");
  }
 }
}
