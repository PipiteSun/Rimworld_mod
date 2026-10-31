using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.79.0 新增强力特性（Defs/HeroTraits_More.xml）：蓝 6 个、紫 5 个进 HeroTraits 的紫色池，红 4 个进 DndTraits 的诅咒池。
 // 名额、互斥、生成、首领、额外几率都走原来的代码（HeroTraits.Sets / Choices、DndTraits.Sets / AddCurseChoices 里各加一行）；
 // 纯属性的写在 XML 里，这里只管规则型：百折不挠（EpicFate 前缀里命运之恩之后）、处刑者、吸血鬼之咒、厄运缠身、狱火烙印。
 // 热路径先查按 pawn 缓存的 MoreInfo（没有这几个规则型特性 = null）；冷却放在限时状态里，不需要额外存档。
 [DefOf]
 public static class MoreTraitDefOf {
  public static TraitDef LP_Artisan,LP_Fleetfoot,LP_IronWill,LP_Silvertongue,LP_Hardy,LP_EagleEyed;
  public static TraitDef LP_Prodigy,LP_Unyielding,LP_Juggernaut,LP_Spellblood,LP_Executioner;
  public static TraitDef LP_Vampirism,LP_Doomed,LP_Hollow,LP_Hellbrand;
  public static HediffDef LP_MT_UnyieldingSpent,LP_MT_Momentum,LP_MT_Sunburn;
  static MoreTraitDefOf(){DefOfHelper.EnsureInitializedInCtor(typeof(MoreTraitDefOf));}
 }

 public sealed class MoreInfo {public bool unyielding,executioner,vampire,doomed,hellbrand;}

 public static class MoreTraits {
  public const float ExecuteFactor=1.5f,ExecuteHealth=.3f,VampireHeal=2f,DoomReflect=.15f,DoomReflectPart=.5f,DoomWear=.3f,HellFireTaken=.25f,HellColdTaken=1.5f,SunGlow=.5f;
  public const int HellExtra=4,SunBurn=1,SunTicks=300;
  public const float DoomMtbDays=8f,HellMtbDays=12f;
  public static float? TestRoll;public static float Roll=>TestRoll??Rand.Value;
  [ThreadStatic] static bool reflecting;

  // ---------- 分类 ----------
  static List<TraitDef> purple,curses;static HashSet<TraitDef> all;
  static void Sets(){
   if(all!=null)return;
   purple=new List<TraitDef>{MoreTraitDefOf.LP_Artisan,MoreTraitDefOf.LP_Fleetfoot,MoreTraitDefOf.LP_IronWill,MoreTraitDefOf.LP_Silvertongue,MoreTraitDefOf.LP_Hardy,MoreTraitDefOf.LP_EagleEyed,
    MoreTraitDefOf.LP_Prodigy,MoreTraitDefOf.LP_Unyielding,MoreTraitDefOf.LP_Juggernaut,MoreTraitDefOf.LP_Spellblood,MoreTraitDefOf.LP_Executioner};
   curses=new List<TraitDef>{MoreTraitDefOf.LP_Vampirism,MoreTraitDefOf.LP_Doomed,MoreTraitDefOf.LP_Hollow,MoreTraitDefOf.LP_Hellbrand};
   purple.RemoveAll(d=>d==null);curses.RemoveAll(d=>d==null);
   all=new HashSet<TraitDef>(purple.Concat(curses));
  }
  public static IEnumerable<TraitDef> Purple{get{Sets();return purple;}}
  public static IEnumerable<TraitDef> Curses{get{Sets();return curses;}}
  public static bool Is(TraitDef d){if(d==null)return false;Sets();return all.Contains(d);}
  // 诅咒池：不死者（巫妖、死灵造物）不会再染上吸血鬼之咒。
  public static bool CurseAllowed(Pawn p,TraitDef d)=>d!=MoreTraitDefOf.LP_Vampirism || !Items.Undead(p);

  // ---------- 缓存 ----------
  static readonly Dictionary<Pawn,MoreInfo> cache=new Dictionary<Pawn,MoreInfo>();
  public static MoreInfo Info(Pawn p){
   if(p==null)return null;
   if(cache.TryGetValue(p,out var i))return i;
   i=Build(p);cache[p]=i;return i;
  }
  public static void Invalidate(Pawn p){if(p!=null)cache.Remove(p);}
  public static void ClearCache(){cache.Clear();}
  public static int Cleanup(){var dead=cache.Keys.Where(p=>p==null || p.Destroyed || p.Discarded).ToList();foreach(var p in dead)cache.Remove(p);return dead.Count;}
  static MoreInfo Build(Pawn p){
   var list=p.story?.traits?.allTraits;if(list==null)return null;MoreInfo i=null;
   for(int k=0;k<list.Count;k++){
    var d=list[k].def;if(!Is(d))continue;
    if(d==MoreTraitDefOf.LP_Unyielding)(i??(i=new MoreInfo())).unyielding=true;
    else if(d==MoreTraitDefOf.LP_Executioner)(i??(i=new MoreInfo())).executioner=true;
    else if(d==MoreTraitDefOf.LP_Vampirism)(i??(i=new MoreInfo())).vampire=true;
    else if(d==MoreTraitDefOf.LP_Doomed)(i??(i=new MoreInfo())).doomed=true;
    else if(d==MoreTraitDefOf.LP_Hellbrand)(i??(i=new MoreInfo())).hellbrand=true;
   }
   return i;
  }

  // ---------- 百折不挠 ----------
  public static int Saved; // 自检计数
  public static bool TryUnyielding(Pawn p,DamageInfo? dinfo,Hediff culprit){
   var i=Info(p);if(i==null || !i.unyielding || p.health==null || dinfo==null && culprit==null)return false;
   if(dinfo.HasValue && (dinfo.Value.Def==DamageDefOf.ExecutionCut || dinfo.Value.Def==DamageDefOf.SurgicalCut))return false;
   if(Items.Has(p,MoreTraitDefOf.LP_MT_UnyieldingSpent) || !Epic.Rescue(p))return false;
   p.health.AddHediff(MoreTraitDefOf.LP_MT_UnyieldingSpent);Saved++;
   Items.Mote(p,"百折不挠",new Color(.69f,.44f,.88f));
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"本该死去，却又一次撑了下来：百折不挠让其倒地幸存（伤口已止血）。10 天后才能再次生效。",p,MessageTypeDefOf.PositiveEvent);
   return true;
  }

  // ---------- 伤害 ----------
  public static float OutFactor(Pawn a,MoreInfo i,Pawn v){
   if(!i.executioner || !a.HostileTo(v))return 1f;
   return v.Downed || v.health.summaryHealth.SummaryHealthPercent<ExecuteHealth?ExecuteFactor:1f;
  }
  public static float InFactor(MoreInfo i,DamageDef d){
   if(!i.hellbrand)return 1f;int e=HeroCombat.Element(d);
   return e==1?HellFireTaken:e==2?HellColdTaken:1f;
  }
  public static void AfterHit(Pawn v,Pawn a,MoreInfo ai,MoreInfo vi,DamageInfo d,float dealt){
   if(reflecting || HeroCombat.extra)return;
   if(ai!=null && a.HostileTo(v)){
    if(ai.vampire && HeroCombat.Melee(d) && !a.Dead){DndTraits.Heal(a,VampireHeal);Items.Mote(a,"吸血",new Color(.8f,.2f,.25f));}
    if(ai.hellbrand && HeroCombat.WeaponHit(d) && !v.Dead){
     HeroCombat.extra=true;
     try{v.TakeDamage(new DamageInfo(HeroCombat.ElementDef(1),HellExtra,0f,-1f,a,null,null,DamageInfo.SourceCategory.ThingOrUnknown,v));}finally{HeroCombat.extra=false;}
    }
   }
   if(vi!=null && vi.doomed && !v.Dead && !a.Dead && a.HostileTo(v) && dealt>0f && Roll<DoomReflect){
    reflecting=true;HeroCombat.extra=true;
    try{a.TakeDamage(new DamageInfo(d.Def,dealt*DoomReflectPart,0f,-1f,v,null,null,DamageInfo.SourceCategory.ThingOrUnknown,a));Items.Mote(a,"厄运转嫁",new Color(.82f,.31f,.31f));}
    finally{reflecting=false;HeroCombat.extra=false;}
   }
  }

  // ---------- 周期 ----------
  public static bool InSun(Pawn p)=>p.Spawned && p.Map!=null && !p.Position.Roofed(p.Map) && p.Map.skyManager.CurSkyGlow>SunGlow;
  public static void Sun(Pawn p){if(InSun(p))Items.Give(p,"LP_MT_Sunburn",SunTicks);}
  public static void Burn(Pawn p){if(!p.Dead && Items.Has(p,MoreTraitDefOf.LP_MT_Sunburn))p.TakeDamage(new DamageInfo(DamageDefOf.Burn,SunBurn,0f,-1f,null,null,null,DamageInfo.SourceCategory.ThingOrUnknown,p));}
  // 厄运降临：身上一件衣甲耐久 -30%（至少 1 点），心情 -5。返回被磨损的衣甲（可能没有）。
  public static Apparel Doom(Pawn p){
   Apparel worn=p.apparel?.WornApparel?.Where(x=>x.def.useHitPoints && x.HitPoints>1).RandomElementWithFallback();
   if(worn!=null)worn.HitPoints=Math.Max(1,worn.HitPoints-Mathf.CeilToInt(worn.MaxHitPoints*DoomWear));
   DndTraits.Memory(p,"LP_MT_Doom");
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"又倒霉了"+(worn!=null?"："+worn.LabelShort+"莫名其妙地坏了一大块":"")+"。",p,MessageTypeDefOf.NegativeEvent,false);
   return worn;
  }
  public static void HellDebt(Pawn p){
   DndTraits.Memory(p,"LP_MT_HellDebt");
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"胸口的狱火烙印又烫了起来：魔鬼来索债了。",p,MessageTypeDefOf.NegativeEvent,false);
  }
 }

 // 250 刻：吸血鬼晒太阳；2500 刻：名单、烧伤、厄运、索债。
 public class GameComponent_MoreTraits : GameComponent {
  readonly List<Pawn> holders=new List<Pawn>();bool dirty=true;
  public GameComponent_MoreTraits(Game game){}
  public static GameComponent_MoreTraits Get=>GameComp<GameComponent_MoreTraits>.Get();
  public void MarkDirty(){dirty=true;}
  public override void FinalizeInit(){base.FinalizeInit();MoreTraits.ClearCache();Refresh();}
  public List<Pawn> Holders{get{if(dirty)Refresh();return holders;}}
  public void Refresh(){
   dirty=false;holders.Clear();var seen=new HashSet<Pawn>();
   void Add(Pawn p){if(p!=null && !p.Dead && seen.Add(p) && MoreTraits.Info(p)!=null)holders.Add(p);}
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawnsSpawned)Add(p);
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)Add(p);
  }
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;
   if(now%250==173)Tick250();
   if(now%2500==1773)Tick2500(now);
  }
  public void Tick250(){
   var list=Holders;if(list.Count==0)return;
   for(int k=0;k<list.Count;k++){var p=list[k];if(p.Dead || !p.Spawned)continue;var i=MoreTraits.Info(p);if(i!=null && i.vampire)MoreTraits.Sun(p);}
  }
  public void Tick2500(int now){
   Refresh();
   foreach(var p in holders.ToList()){
    var i=MoreTraits.Info(p);if(i==null || p.Dead)continue;
    try{
     if(i.vampire)MoreTraits.Burn(p);
     if(i.doomed && Rand.MTBEventOccurs(MoreTraits.DoomMtbDays,GenDate.TicksPerDay,2500f))MoreTraits.Doom(p);
     if(i.hellbrand && Rand.MTBEventOccurs(MoreTraits.HellMtbDays,GenDate.TicksPerDay,2500f))MoreTraits.HellDebt(p);
    }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 新增强力特性 2500 刻出错："+e,77000001);}
   }
   if(now%60000<2500)MoreTraits.Cleanup();
  }
 }

 // ==================== Harmony ====================
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.GainTrait))]
 public static class MoreGain {static void Postfix(Pawn ___pawn,Trait trait){if(trait==null || !MoreTraits.Is(trait.def))return;MoreTraits.Invalidate(___pawn);GameComponent_MoreTraits.Get?.MarkDirty();}}
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.RemoveTrait))]
 public static class MoreLose {static void Postfix(Pawn ___pawn,Trait trait){if(trait==null || !MoreTraits.Is(trait.def))return;MoreTraits.Invalidate(___pawn);GameComponent_MoreTraits.Get?.MarkDirty();if(trait.def==MoreTraitDefOf.LP_Vampirism)Items.Remove(___pawn,"LP_MT_Sunburn");}}
 // 伤害：处刑者（攻击方）、狱火烙印的火 / 寒（受害方）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class MoreDamage {
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   var def=dinfo.Def;if(def==null || !def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return true;
   var a=dinfo.Instigator as Pawn;var vi=MoreTraits.Info(__instance);var ai=a==null || a==__instance?null:MoreTraits.Info(a);
   if(vi==null && ai==null)return true;
   float f=1f;
   if(ai!=null)f*=MoreTraits.OutFactor(a,ai,__instance);
   if(vi!=null)f*=MoreTraits.InFactor(vi,def);
   if(Math.Abs(f-1f)<.0001f)return true;
   dinfo.SetAmount(dinfo.Amount*f);if(dinfo.Amount<=.001f){absorbed=true;return false;}
   return true;
  }
 }
 // 命中后：吸血、狱火附伤、厄运转嫁。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class MoreAfterHit {
  static void Postfix(Thing __instance,DamageInfo dinfo,DamageWorker.DamageResult __result){
   if(!(__instance is Pawn v) || !(dinfo.Instigator is Pawn a) || a==v || __result==null || __result.totalDamageDealt<=0f)return;
   var ai=MoreTraits.Info(a);var vi=MoreTraits.Info(v);if(ai==null && vi==null)return;
   MoreTraits.AfterHit(v,a,ai,vi,dinfo,__result.totalDamageDealt);
  }
 }
 // 处刑者：亲手击杀敌人后乘胜追击。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class MoreKill {
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out Pawn __state){ // 敌对要在死前判（死后尸体不再算敌对）
   var k=__instance.Dead?null:dinfo?.Instigator as Pawn;__state=k!=null && k!=__instance && MoreTraits.Info(k)?.executioner==true && k.HostileTo(__instance)?k:null;
  }
  static void Postfix(Pawn __instance,Pawn __state){
   if(__state==null || !__instance.Dead || __state.Dead)return;
   Items.Give(__state,"LP_MT_Momentum");
  }
 }
}
