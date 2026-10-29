using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.46.0 背景故事：139 条 DND 背景（Defs/Backstories_DND.xml，由 ArtSource/Backstories/gen_backstories.py 生成），类别全部 LP_ 开头，原版筛选器选不中。
 // 分配不用原版派系 backstoryFilters：原版选完以后按 Defs/BackstoryPools.xml 的规则逐个槽位替换。
 // 规则优先级 兵种 > 种族 > 派系 > 原版人类；某一级掷骰失败就试下一级，掷骰成功但挑不到合法背景就保留原版。
 public enum BackstoryRuleTier{Kind,Race,Faction,VanillaHuman}
 public class BackstoryCategoryWeight {public string category;public float weight=1f;}
 public class BackstoryPoolDef : Def {
  public BackstoryRuleTier tier;
  // 用字符串：HAR 种族和兵种只在装了 HAR 时存在，写成 Def 引用会在没装时报错。
  public List<string> pawnKinds=new List<string>(),xenotypes=new List<string>(),races=new List<string>(),factions=new List<string>();
  public List<BackstoryCategoryWeight> childhood=new List<BackstoryCategoryWeight>(),adulthood=new List<BackstoryCategoryWeight>();
  public float chance=1f;
  // race → 设置“种族背景出现率”；vanillaHuman → 设置“原版人类费伦背景”；空 → 用 chance。
  public string chanceSetting;
  // 跳过规则：匹配到（兵种、异种型、种族或派系任一）就整个小人不换背景。
  public bool skip;
  public float Chance{get{var s=LichMod.Settings;switch(chanceSetting){case "race":return (s?.raceBackstoryPercent??75)/100f;case "vanillaHuman":return (s?.vanillaFaerunPercent??10)/100f;}return chance;}}
  public List<BackstoryCategoryWeight> For(BackstorySlot slot)=>slot==BackstorySlot.Childhood?childhood:adulthood;
  public bool Matches(Pawn p,FactionDef faction,XenotypeDef xeno){
   if(skip)return pawnKinds.Contains(p.kindDef?.defName) || xeno!=null && xenotypes.Contains(xeno.defName) || races.Contains(p.def.defName) || faction!=null && factions.Contains(faction.defName);
   switch(tier){
    case BackstoryRuleTier.Kind:return p.kindDef!=null && pawnKinds.Contains(p.kindDef.defName);
    case BackstoryRuleTier.Race:return xeno!=null && xenotypes.Contains(xeno.defName) || races.Contains(p.def.defName);
    case BackstoryRuleTier.Faction:return faction!=null && factions.Contains(faction.defName);
    case BackstoryRuleTier.VanillaHuman:return Backstories.VanillaHuman(p,xeno) && !Backstories.HasFactionRule(faction);
   }
   return false;
  }
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(skip)yield break;
   if(childhood.Count==0 && adulthood.Count==0)yield return "needs childhood or adulthood categories";
   foreach(var c in childhood.Concat(adulthood))if(c.category.NullOrEmpty() || c.weight<=0f)yield return "bad category entry "+c.category;
   if(tier==BackstoryRuleTier.Kind && pawnKinds.Count==0 || tier==BackstoryRuleTier.Race && xenotypes.Count==0 && races.Count==0 || tier==BackstoryRuleTier.Faction && factions.Count==0)yield return "rule has nothing to match";
  }
 }
 // 背景特性联动：带这个扩展的背景按 chance × “背景特性出现率”给对应的 1.32.0 背景特性。
 public class BackstoryTraitExtension : DefModExtension {public TraitDef trait;public float chance;}

 public static class Backstories {
  public const string Prefix="LP_Bs_";
  // 背景特性是在原版 2~3 个特性之外额外加的：不算基因特性满 4 个才算满。
  public const int MinAge=13,MaxTraits=4;
  // 自测期间（LichChecks）除本组外不换背景：随机背景会改动其他检查组小人的技能和可做工作。
  public static bool TestSuppressed;
  // 测试用：不为空时代替联动概率。
  public static float? TestTraitChance;
  public static bool Enabled=>!TestSuppressed && LichMod.Settings?.dndBackstories!=false;

  static List<BackstoryPoolDef> rules,skips;static HashSet<string> factionRuleDefs;
  static Dictionary<string,List<BackstoryDef>> childByCat,adultByCat;
  static void Build(){
   if(rules!=null)return;
   var all=DefDatabase<BackstoryPoolDef>.AllDefsListForReading;
   skips=all.Where(r=>r.skip).ToList();
   // 同一级里按 XML 顺序。
   rules=all.Where(r=>!r.skip).Select((r,i)=>(r,i)).OrderBy(x=>(int)x.r.tier).ThenBy(x=>x.i).Select(x=>x.r).ToList();
   factionRuleDefs=new HashSet<string>(rules.Where(r=>r.tier==BackstoryRuleTier.Faction).SelectMany(r=>r.factions));
   childByCat=new Dictionary<string,List<BackstoryDef>>();adultByCat=new Dictionary<string,List<BackstoryDef>>();
   foreach(var b in DefDatabase<BackstoryDef>.AllDefsListForReading){
    if(!b.shuffleable || b.spawnCategories==null)continue;
    var dict=b.slot==BackstorySlot.Childhood?childByCat:adultByCat;
    foreach(var c in b.spawnCategories)if(c!=null && c.StartsWith("LP_")){if(!dict.TryGetValue(c,out var l))dict[c]=l=new List<BackstoryDef>();l.Add(b);}
   }
  }
  public static IEnumerable<BackstoryPoolDef> Rules{get{Build();return rules;}}
  public static List<BackstoryDef> InCategory(string category,BackstorySlot slot){Build();var dict=slot==BackstorySlot.Childhood?childByCat:adultByCat;return dict.TryGetValue(category,out var l)?l:new List<BackstoryDef>();}
  public static bool HasFactionRule(FactionDef f){Build();return f!=null && factionRuleDefs.Contains(f.defName);}
  public static bool IsDND(BackstoryDef b)=>b?.defName!=null && b.defName.StartsWith(Prefix);
  public static bool InCategory(BackstoryDef b,string category)=>b?.spawnCategories!=null && b.spawnCategories.Contains(category);
  // 原版人类：原版人类种族、原版人类异种型（或没有异种型），不是自定义异种型。
  public static bool VanillaHuman(Pawn p,XenotypeDef xeno)=>p.def==ThingDefOf.Human && (xeno==null || xeno==XenotypeDefOf.Baseliner);

  static bool Skipped(Pawn p,FactionDef faction,XenotypeDef xeno,PawnGenerationRequest req){
   if(p?.story==null || !p.RaceProps.Humanlike || p.kindDef==null)return true;
   if(req.ForceNoBackstory || req.OnlyUseForcedBackstories)return true;
   if(p.ageTracker.AgeBiologicalYears<MinAge)return true;
   // 有名有姓的原版角色背景、兵种自带固定背景或背景筛选器（帝国、Anomaly 怪人等）。
   if(p.story.Childhood!=null && !p.story.Childhood.shuffleable || p.story.Adulthood!=null && !p.story.Adulthood.shuffleable)return true;
   var k=p.kindDef;
   if(!k.fixedChildBackstories.NullOrEmpty() || !k.fixedAdultBackstories.NullOrEmpty() || k.backstoryFiltersOverride!=null || k.GetType().Name.Contains("CreepJoiner"))return true;
   if(Necro.Construct(p)!=null)return true;
   Build();
   foreach(var r in skips)if(r.Matches(p,faction,xeno))return true;
   return false;
  }

  // 照抄原版 FillBackstorySlotShuffled 的判断，加上兵种需要的工作和请求里的强制特性。
  public static bool Legal(Pawn p,BackstoryDef b,PawnGenerationRequest req){
   if(b==null || !b.shuffleable)return false;
   var other=b.slot==BackstorySlot.Childhood?p.story.Adulthood:p.story.Childhood;
   var adult=b.slot==BackstorySlot.Adulthood?b:other;var child=b.slot==BackstorySlot.Childhood?b:other;
   if(adult!=null && child!=null && adult.requiredWorkTags!=WorkTags.None && WorkTypeDefsUtility.OverlapsWithOnAnyWorkType(adult.requiredWorkTags,child.workDisables))return false;
   var disabled=b.workDisables|(other?.workDisables??WorkTags.None);
   var required=p.kindDef.requiredWorkTags;
   if(req.MustBeCapableOfViolence || p.kindDef.isFighter)required|=WorkTags.Violent;
   if((disabled&required)!=WorkTags.None)return false;
   if(req.ForcedTraits!=null)foreach(var t in req.ForcedTraits){
    if(t==null)continue;
    if(b.disallowedTraits!=null && b.disallowedTraits.Any(d=>d.def==t))return false;
    if(b.forcedTraits!=null && b.forcedTraits.Any(f=>f.def==t || f.def.ConflictsWith(t)))return false;
    if((t.requiredWorkTags&disabled)!=WorkTags.None)return false;
   }
   if(req.ProhibitedTraits!=null && b.forcedTraits!=null && b.forcedTraits.Any(f=>req.ProhibitedTraits.Contains(f.def)))return false;
   return true;
  }

  // 带 LP_Race_* 类别的背景（如“独眼之神的祭司”同时在 LP_Faith 里）只给这个种族：别的种族从共用类别里抽不到它。
  // allowed = 小人自己种族规则里的类别 + 当前规则列出的类别（巫妖 NPC 规则直接用 LP_Race_Lich）。
  public static bool RaceAllowed(BackstoryDef b,HashSet<string> allowed){
   bool raced=false;
   foreach(var c in b.spawnCategories){if(!c.StartsWith("LP_Race_"))continue;if(allowed.Contains(c))return true;raced=true;}
   return !raced;
  }
  static readonly List<(List<BackstoryDef> list,float weight)> tmpCats=new List<(List<BackstoryDef>,float)>();
  // 先按权重挑一个有合法背景的类别，再在类别里平均挑一条。
  public static BackstoryDef Pick(Pawn p,BackstorySlot slot,List<BackstoryCategoryWeight> cats,PawnGenerationRequest req,HashSet<string> allowed){
   tmpCats.Clear();
   foreach(var c in cats){var legal=InCategory(c.category,slot).Where(b=>RaceAllowed(b,allowed) && Legal(p,b,req)).ToList();if(legal.Count>0)tmpCats.Add((legal,c.weight));}
   if(tmpCats.Count==0)return null;
   var chosen=tmpCats.RandomElementByWeight(x=>x.weight);tmpCats.Clear();
   return chosen.list.RandomElement();
  }

  public static void Assign(Pawn p,FactionDef faction,PawnGenerationRequest req,XenotypeDef xeno){
   if(!Enabled || Skipped(p,faction,xeno,req))return;
   var matching=Rules.Where(r=>r.Matches(p,faction,xeno)).ToList();
   if(matching.Count==0)return;
   // 自己种族的类别（不管种族规则这次掷骰成没成功）。
   var own=new HashSet<string>(matching.Where(r=>r.tier==BackstoryRuleTier.Race).SelectMany(r=>r.childhood.Concat(r.adulthood)).Select(c=>c.category));
   // 先给两个槽位各自掷骰定下用哪条规则，再挑：童年不和马上要被换掉的原版成年背景比（原版成年背景可能禁用暴力），成年和新的童年比。
   var childRule=p.story.Childhood==null?null:Roll(matching,BackstorySlot.Childhood);
   var adultRule=p.story.Adulthood==null?null:Roll(matching,BackstorySlot.Adulthood);
   if(childRule!=null){
    var vanillaAdult=p.story.Adulthood;if(adultRule!=null)p.story.Adulthood=null;
    var pick=Pick(p,BackstorySlot.Childhood,childRule.childhood,req,Allowed(own,childRule.childhood));
    p.story.Adulthood=vanillaAdult;
    if(pick!=null)p.story.Childhood=pick;
   }
   if(adultRule!=null){var pick=Pick(p,BackstorySlot.Adulthood,adultRule.adulthood,req,Allowed(own,adultRule.adulthood));if(pick!=null)p.story.Adulthood=pick;}
  }
  // 按优先级找第一条这个槽位有类别、并且掷骰成功的规则。
  static BackstoryPoolDef Roll(List<BackstoryPoolDef> matching,BackstorySlot slot){
   foreach(var r in matching)if(r.For(slot).Count>0 && Rand.Chance(r.Chance))return r;
   return null;
  }
  static HashSet<string> Allowed(HashSet<string> own,List<BackstoryCategoryWeight> cats){var allowed=new HashSet<string>(own);foreach(var c in cats)allowed.Add(c.category);return allowed;}

  // ---------- 背景特性联动 ----------
  public static bool CanGiveTrait(Pawn p,TraitDef def,PawnGenerationRequest req){
   var traits=p.story?.traits;if(def==null || traits==null || traits.HasTrait(def))return false;
   if(BackgroundTraits.Count(p)>0)return false;
   if(traits.allTraits.Count(t=>t.sourceGene==null)>=MaxTraits)return false;
   if(traits.allTraits.Any(t=>t.def.ConflictsWith(def) || def.ConflictsWith(t.def)))return false;
   if(p.kindDef?.disallowedTraits!=null && p.kindDef.disallowedTraits.Contains(def))return false;
   if(req.ProhibitedTraits!=null && req.ProhibitedTraits.Contains(def))return false;
   int degree=def.degreeDatas[0].degree;
   if(p.story.Childhood!=null && p.story.Childhood.DisallowsTrait(def,degree) || p.story.Adulthood!=null && p.story.Adulthood.DisallowsTrait(def,degree))return false;
   var required=p.kindDef?.requiredWorkTags??WorkTags.None;if(req.MustBeCapableOfViolence)required|=WorkTags.Violent;
   if((def.disabledWorkTags&required)!=WorkTags.None)return false;
   return true;
  }
  public static void LinkTraits(Pawn p,PawnGenerationRequest req){
   var s=LichMod.Settings;if(p?.story?.traits==null || s!=null && !s.backgroundTraits)return;
   if(BackgroundTraits.Count(p)>0)return;
   float rate=s==null?1f:s.backgroundTraitPercent/100f;
   foreach(var b in new[]{p.story.Adulthood,p.story.Childhood}){
    var ext=b?.GetModExtension<BackstoryTraitExtension>();if(ext?.trait==null)continue;
    if(!Rand.Chance(TestTraitChance??ext.chance*rate))continue;
    if(!CanGiveTrait(p,ext.trait,req))continue;
    p.story.traits.GainTrait(new Trait(ext.trait,ext.trait.degreeDatas[0].degree));
    return;
   }
  }
 }

 // 原版选完背景之后替换（1.7 里它在异种型选定之后、基因/特性/体型/技能生成之前执行）；排在 HAR 等其他后缀之后。
 [HarmonyPatch(typeof(PawnBioAndNameGenerator),nameof(PawnBioAndNameGenerator.GiveAppropriateBioAndNameTo))]
 public static class Backstories_GiveBio {
  [HarmonyPriority(Priority.Last)]
  static void Postfix(Pawn __0,FactionDef __1,PawnGenerationRequest __2,XenotypeDef __3){
   try{Backstories.Assign(__0,__1,__2,__3);}
   catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 分配背景故事出错："+e,73519021);}
  }
 }
 [HarmonyPatch(typeof(PawnGenerator),"GenerateTraits")]
 public static class Backstories_LinkTraits {
  [HarmonyPriority(Priority.Low)]
  static void Postfix(Pawn pawn,PawnGenerationRequest request){
   try{Backstories.LinkTraits(pawn,request);}
   catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 背景特性联动出错："+e,73519022);}
  }
 }
 // DND 成年背景不指定体型：原版会直接拿成年背景的体型（为空），这里按原版没有成年背景时的办法补上。
 [HarmonyPatch(typeof(PawnGenerator),"GetBodyTypeFor")]
 public static class Backstories_BodyType {
  static void Postfix(Pawn pawn,ref BodyTypeDef __result){
   if(__result!=null || pawn?.story?.Adulthood==null || !Backstories.IsDND(pawn.story.Adulthood))return;
   __result=Rand.Value<.5f?BodyTypeDefOf.Thin:pawn.gender==Gender.Female?BodyTypeDefOf.Female:BodyTypeDefOf.Male;
  }
 }

 public partial class Settings {
  public bool dndBackstories=true;public int raceBackstoryPercent=75,vanillaFaerunPercent=10;
  void ExposeBackstories(){
   Scribe_Values.Look(ref dndBackstories,"dndBackstories",true);Scribe_Values.Look(ref raceBackstoryPercent,"raceBackstoryPercent",75);Scribe_Values.Look(ref vanillaFaerunPercent,"vanillaFaerunPercent",10);
   raceBackstoryPercent=Math.Max(0,Math.Min(100,raceBackstoryPercent));vanillaFaerunPercent=Math.Max(0,Math.Min(100,vanillaFaerunPercent));
  }
 }
 public static class BackstorySettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.CheckboxLabeled("DND 背景故事",ref s.dndBackstories,"（1.46.0）新生成的小人按兵种、种族、派系换上费伦的童年和成年背景（139 条）。只影响之后新生成的小人，已有的小人不变。关闭后完全按原版分配。");
   l.Label("种族背景出现率："+s.raceBackstoryPercent+"%（DND 种族的小人每个背景槽位换成本族背景的概率）");
   s.raceBackstoryPercent=Mathf.RoundToInt(l.Slider(s.raceBackstoryPercent,0,100)/5f)*5;
   l.Label("原版人类费伦背景："+s.vanillaFaerunPercent+"%（不属于 DND 派系的原版人类，包括开局殖民者，每个背景槽位换成费伦背景的概率）");
   s.vanillaFaerunPercent=Mathf.RoundToInt(l.Slider(s.vanillaFaerunPercent,0,100)/5f)*5;
  }
 }
}
