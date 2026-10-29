using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.23.0 巨躯规则：本模组种族带来的体型倍率 S 影响生命总量（HealthScale）与近战伤害（MeleeDamageFactor）。
 // S = max(火巨人, 巨魔) × 小体型/牛头人体型扩展 × 力能龙龙龄，与 get_BodySize 补丁里的这几项一致；
 // 原版种族基础体型、构造体旧种族体型、其他模组改的体型都不算。
 // 原版 HealthScale 同时决定部位生命上限、失血速度（÷）、疼痛（÷）、愈合速度（×）和致死伤害阈值（×）。
 public partial class Settings {
  public bool bodySizeRule=true;public int bodySizeStrength=100;
  void ExposeBodySize(){Scribe_Values.Look(ref bodySizeRule,"bodySizeRule",true);Scribe_Values.Look(ref bodySizeStrength,"bodySizeStrength",100);bodySizeStrength=Math.Max(0,Math.Min(200,bodySizeStrength));}
 }
 public static class BodySize {
  public const float HealthPerSize=.5f,MeleePerSize=.3f,LegacyFactor=1.5f;
  // legacy=1.22.1 公式（火巨人、龙体各在部位生命上×1.5并相乘，HealthScale 不变）；
  // on=false 时生命取旧的×1.5但取较大值、没有近战加成；强度0%等同关闭。
  public struct Rule {
   public bool legacy,on;public float k;
   public static Rule Legacy=>new Rule{legacy=true};
   public static Rule Of(bool enabled,int strength)=>new Rule{on=enabled && strength>0,k=Math.Max(0,strength)/100f};
   public bool Same(Rule o)=>legacy==o.legacy && on==o.on && (!on || Math.Abs(k-o.k)<.0001f);
   public override string ToString()=>legacy?"1.22.1":on?"巨躯 "+(k*100f).ToString("0")+"%":"关闭";
  }
  [ThreadStatic] static bool forcing;[ThreadStatic] static Rule forced;
  public static Rule ActiveRule=>forcing?forced:Rule.Of(LichMod.Settings.bodySizeRule,LichMod.Settings.bodySizeStrength);

  // 按角色缓存来源，倍率每次按当前规则现算（只是几次乘法），所以改设置或临时测量旧规则不需要清缓存。
  // 失效：基因变化、龙龄变化、构造体/龙龄等健康状态增减、过生日（整数岁）、250刻兜底（儿童体型随年龄连续长）。
  sealed class Entry {public float size;public bool fire,fireAdult,body,construct;public int tick=int.MinValue,hediffs=-1,genes=-1,years=-1;}
  static readonly ConditionalWeakTable<Pawn,Entry> cache=new ConditionalWeakTable<Pawn,Entry>();
  const int StaleTicks=250;
  public static void Invalidate(Pawn p){if(p!=null && cache.TryGetValue(p,out var e))e.tick=int.MinValue;}
  // 体型变化后：清掉本缓存与原版最大饱食的15刻缓存，按胃容量倍率的变化缩放饱食（1.27.1），并把超出新胃容量的饱食截到上限。
  public static void Notify_SizeChanged(Pawn p){
   if(p==null)return;Invalidate(p);
   StatDefOf.MaxNutrition.Worker.ClearCacheForThing(p);Satiety.Sync(p,true);
   var food=p.needs?.food;if(food!=null && Current.ProgramState==ProgramState.Playing)food.CurLevel=food.CurLevel;
   p.health?.capacities?.Notify_CapacityLevelsDirty();
  }
  // 1.30.0：火巨人一项改为所有巨人血统（GiantKin，火巨人、霜巨人……）；火巨人数值不变。
  // 1.33.0：六臂蛇人的体型是种族自带的（HAR 种族 baseBodySize 1.6，不走 get_BodySize 补丁），这里按种族扩展再乘一项（成年 1.6，儿童随年龄长大）。
  // 1.44.0：种族一项改为通用的 RaceSize（六臂蛇人 1.6、双头巨人 2.0、独眼巨人 1.8）；这两族巨人的本源基因 adultSize 是 1，所以 GiantKin.Size 那一项是 1，不会叠乘。
  public static float Size(Pawn p)=>Mathf.Max(GiantKin.Size(p),TrollAppearance.Size(p))*SmallBody.Size(p)*ForceDragon.SizeFactor(ForceDragon.Age(p))*RaceSize.SizeFactor(p);
  static Entry Get(Pawn p){
   var e=cache.GetOrCreateValue(p);
   var list=p.health?.hediffSet?.hediffs;int hediffs=list?.Count??0,genes=p.genes.Endogenes.Count+p.genes.Xenogenes.Count,years=p.ageTracker?.AgeBiologicalYears??0;
   var tm=Current.Game?.tickManager;int now=tm?.TicksGame??0;
   if(tm!=null && e.hediffs==hediffs && e.genes==genes && e.years==years && now>=e.tick && now-e.tick<StaleTicks)return e;
   e.construct=Necro.Construct(p)!=null;
   // 1.30.0：fire/fireAdult 扩大为“巨人血统/成年巨人”：霜巨人和火巨人一样，生命不低于×150%（关闭巨躯规则时也是旧的×150%）。
   e.size=e.construct?1f:Size(p);var kin=GiantKin.Of(p);e.fire=kin!=null;e.fireAdult=e.fire && GiantKin.Size(p)>=kin.adultSize-.0001f && kin.adultSize>=2f-.0001f;e.body=ForceDragon.Has(p,ForceDragon.BodyGene);
   e.hediffs=hediffs;e.genes=genes;e.years=years;e.tick=tm!=null?now:int.MinValue;
   return e;
  }
  static float Health(Entry e,Rule r){
   if(e.construct || r.legacy)return 1f;
   float legacy=Mathf.Max(e.fire?LegacyFactor:1f,e.body?LegacyFactor:1f);
   if(!r.on)return legacy;
   float h=1f+HealthPerSize*r.k*Mathf.Max(0f,e.size-1f);
   return e.body || e.fireAdult?Mathf.Max(h,LegacyFactor):h;
  }
  static float Melee(Entry e,Rule r)=>e.construct || r.legacy || !r.on?1f:1f+MeleePerSize*r.k*Mathf.Max(0f,e.size-1f);
  public static float HealthFactor(Pawn p)=>p?.genes==null?1f:Health(Get(p),ActiveRule);
  public static float HealthFactor(Pawn p,Rule r)=>p?.genes==null?1f:Health(Get(p),r);
  public static float MeleeFactor(Pawn p)=>p?.genes==null?1f:Melee(Get(p),ActiveRule);
  public static float MeleeFactor(Pawn p,Rule r)=>p?.genes==null?1f:Melee(Get(p),r);
  // 只有测量 1.22.1 旧值时才在部位生命上乘旧系数（火巨人×1.5、龙体×1.5，相乘）。
  public static float LegacyPartFactor(Pawn p){
   if(!forcing || !forced.legacy || p?.genes==null)return 1f;
   var e=Get(p);return (e.fire?LegacyFactor:1f)*(e.body?LegacyFactor:1f);
  }
  public static T Measure<T>(Rule rule,Func<T> body){
   bool wasForcing=forcing;var prior=forced;forcing=true;forced=rule;
   try{return body();}finally{forcing=wasForcing;forced=prior;}
  }

  // 最大生命变小时按“新最大/旧最大”缩放该部位伤口：读档、改设置都不会毁掉部位、杀死角色或白白治好。变大时不动。
  public static int Rescale(IEnumerable<Pawn> pawns,Rule from,Rule to){
   int scaled=0;
   foreach(var p in pawns){
    var set=p?.health?.hediffSet;if(set==null || p.genes==null)continue;
    if(Math.Abs(HealthFactor(p,from)*Measure(from,()=>LegacyPartFactor(p))-HealthFactor(p,to))<.0001f)continue;
    var parts=set.hediffs.OfType<Hediff_Injury>().Select(h=>h.Part).Where(x=>x!=null && !set.PartIsMissing(x)).Distinct().ToList();
    if(parts.Count==0)continue;
    var before=Measure(from,()=>parts.Select(x=>PartHealth.Maximum(x,p)).ToArray());
    var after=Measure(to,()=>parts.Select(x=>PartHealth.Maximum(x,p)).ToArray());
    bool changed=false;
    for(int i=0;i<parts.Count;i++){
     if(after[i]>=before[i] || before[i]<=0)continue;
     float ratio=after[i]/before[i];
     foreach(var injury in set.hediffs.OfType<Hediff_Injury>().Where(h=>h.Part==parts[i]).ToList()){injury.Severity*=ratio;changed=true;}
    }
    if(changed){scaled++;set.DirtyCache();p.health.capacities?.Notify_CapacityLevelsDirty();}
   }
   return scaled;
  }
  public static IEnumerable<Pawn> AllPawns(){
   var seen=new HashSet<Pawn>();
   foreach(var p in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)if(p!=null && seen.Add(p))yield return p;
   var registry=Necro.Registry;
   if(registry!=null)foreach(var box in registry.boxes)if(box?.souls!=null)foreach(var p in box.souls)if(p!=null && seen.Add(p))yield return p;
  }
  // 设置窗口关闭时调用：已载入的游戏按“上次记录的规则 → 当前规则”缩放伤口。
  public static void ApplySettings(){if(Current.Game!=null && Current.ProgramState==ProgramState.Playing)Necro.Registry?.MigrateBodySize();}
 }
 public partial class Registry {
  // -1：新开局（构造时）；0：1.22.1 及更早的存档（读档时没有这个字段）；1：已按巨躯规则记录。
  public int bodySizeVersion=-1;public bool bodySizeOn=true;public int bodySizeStrength=100;
  void ExposeBodySize(){
   Scribe_Values.Look(ref bodySizeVersion,"bodySizeVersion",0);
   Scribe_Values.Look(ref bodySizeOn,"bodySizeRuleOn",true,true);Scribe_Values.Look(ref bodySizeStrength,"bodySizeRuleStrength",100,true);
  }
  public BodySize.Rule RecordedBodySizeRule=>bodySizeVersion==0?BodySize.Rule.Legacy:BodySize.Rule.Of(bodySizeOn,bodySizeStrength);
  // pawns 为空时处理所有角色（读档、设置变化）；检查只传测试角色，避免改动其他检查的伤口。
  public int MigrateBodySize(IEnumerable<Pawn> pawns=null){
   var to=BodySize.ActiveRule;int scaled=0;
   if(bodySizeVersion>=0){
    var from=RecordedBodySizeRule;
    if(!from.Same(to)){scaled=BodySize.Rescale((pawns??BodySize.AllPawns()).ToList(),from,to);Log.Message("[DND边缘大陆] 巨躯规则迁移："+from+" → "+to+"，缩放了"+scaled+"个角色的伤口。");}
   }
   bodySizeVersion=1;bodySizeOn=LichMod.Settings.bodySizeRule;bodySizeStrength=LichMod.Settings.bodySizeStrength;
   return scaled;
  }
 }
 public class StatPart_BodySizeMelee : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p)val*=BodySize.MeleeFactor(p);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;float f=BodySize.MeleeFactor(p);return f==1f?null:"巨躯：×"+f.ToStringPercent();}
 }
 [StaticConstructorOnStartup]
 public static class BodySizeStartup {
  static BodySizeStartup(){
   var stat=StatDefOf.MeleeDamageFactor;if(stat.parts==null)stat.parts=new List<StatPart>();
   if(!stat.parts.Any(p=>p is StatPart_BodySizeMelee))stat.parts.Add(new StatPart_BodySizeMelee{parentStat=stat});
  }
 }
 // 与构造体材料、其他模组的 HealthScale 补丁相乘。
 [HarmonyPatch(typeof(Pawn),"get_HealthScale")]
 public static class BodySizeHealth {
  static void Postfix(Pawn __instance,ref float __result){if(__instance.genes==null)return;float f=BodySize.HealthFactor(__instance);if(f!=1f)__result*=f;}
 }
 [HarmonyPatch(typeof(Pawn_GeneTracker),"Notify_GenesChanged")]
 public static class BodySizeGeneChanged {
  static void Postfix(Pawn_GeneTracker __instance){BodySize.Notify_SizeChanged(__instance.pawn);}
 }
}
