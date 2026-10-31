using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.85.0 附魔强化：取代 Isekai 的装备强化。所有武器和衣物启动时加上 CompGearEnhance（等级 0~10）。
 // 玩家在地上的装备上点“附魔强化”选目标等级；附魔台的两张工作单（Defs/GearEnhance.xml）只接选了目标、等级在本档里的装备，
 // 做完不消耗装备（RecipeWorker_GearEnhance.ConsumeIngredient 跳过它），在 MakeRecipeProducts 里按成功率升一级或失败。
 public static class GearEnhance {
  public const int Max=10,LowMax=5;
  public const string LowRecipe="LP_EnhanceGearLow",HighRecipe="LP_EnhanceGearHigh",LowResearch="LP_Research_GearEnhancing",HighResearch="LP_Research_GearEnhancingHigh";
  public const float DamagePer=.06f,AccuracyPer=.015f,ArmorPer=.05f,ArmorFlatPer=.01f,ValuePer=.15f,SkillPer=.015f,SkillCap=.18f,SkillFloor=-.1f,FocusBonus=.05f;
  // 从 L 强化到 L+1 的基础成功率。
  public static readonly float[] BaseChance={1f,.95f,.9f,.8f,.7f,.6f,.5f,.4f,.3f,.2f};
  public static float? TestRoll;
  public enum Result{None,Success,Fail,Drop}

  static ResearchProjectDef R(string n)=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail(n);
  // 当前研究允许的最高目标。
  public static int Allowed=>R(HighResearch)?.IsFinished==true?Max:R(LowResearch)?.IsFinished==true?LowMax:0;
  public static CompGearEnhance Comp(Thing t)=>(t as ThingWithComps)?.GetComp<CompGearEnhance>();
  public static int Level(Thing t)=>Comp(t)?.level??0;
  public static bool Eligible(ThingDef d)=>d!=null && d.category==ThingCategory.Item && (d.IsApparel || d.IsWeapon && d.equipmentType==EquipmentType.Primary) && d.stackLimit==1 && !d.IsStuff && d.thingClass!=null && typeof(ThingWithComps).IsAssignableFrom(d.thingClass);
  public static bool IsWeapon(Thing t)=>t?.def!=null && t.def.IsWeapon && !t.def.IsApparel;

  // 这张工作单接不接这件装备：选了更高的目标，现等级在本档里（低档 0~4，高档 5~9）。
  public static bool InBand(RecipeDef r,int level)=>r?.defName==LowRecipe?level>=0 && level<LowMax:r?.defName==HighRecipe && level>=LowMax && level<Max;
  public static bool Wants(Thing t,RecipeDef r){var c=Comp(t);return c!=null && c.target>c.level && InBand(r,c.level);}

  public static float SkillBonus(Pawn p){
   if(p?.skills==null)return 0f;
   float avg=(p.skills.GetSkill(SkillDefOf.Intellectual).Level+p.skills.GetSkill(SkillDefOf.Crafting).Level)/2f;
   return Mathf.Clamp((avg-8f)*SkillPer,SkillFloor,SkillCap);
  }
  public static bool HasFocusStone(Thing bench)=>bench?.TryGetComp<CompAffectedByFacilities>()?.LinkedFacilitiesListForReading.Any(f=>f?.def?.defName=="LP_ArcaneFocusStone")==true;
  public static float Chance(int level,Pawn worker,Thing bench){
   if(level<0 || level>=Max)return 0f;
   return Mathf.Clamp(BaseChance[level]+SkillBonus(worker)+(HasFocusStone(bench)?FocusBonus:0f),.05f,1f);
  }

  // 做完一次工作单：升一级（并修满耐久），或失败（+6 起掉一级，不低于 +5）。
  public static Result Apply(Thing gear,Pawn worker,Thing bench){
   var c=Comp(gear);if(c==null || c.level>=Max)return Result.None;
   int from=c.level;float chance=Chance(from,worker,bench);float roll=TestRoll??Rand.Value;
   string who=worker?.LabelShort??"工匠";Result result;
   if(roll<chance){
    c.level++;if(gear.def.useHitPoints)gear.HitPoints=gear.MaxHitPoints;
    if(c.target<=c.level)c.target=0;
    result=Result.Success;
    if(!TestSuppressed)Messages.Message(who+"把"+gear.LabelNoCount+"强化到了+"+c.level+"。",gear,MessageTypeDefOf.PositiveEvent,false);
   }else if(from>LowMax){
    c.level--;result=Result.Drop;
    if(!TestSuppressed)Messages.Message(who+"强化失败，"+gear.LabelNoCount+"掉回了+"+c.level+"（成功率"+chance.ToStringPercent()+"）。",gear,MessageTypeDefOf.NegativeEvent,false);
   }else{
    result=Result.Fail;
    if(!TestSuppressed)Messages.Message(who+"强化"+gear.LabelNoCount+"失败了，材料没了，装备不受影响（成功率"+chance.ToStringPercent()+"）。",gear,MessageTypeDefOf.NeutralEvent,false);
   }
   ClearStatCache(gear);
   return result;
  }
  public static bool TestSuppressed;
  public static readonly List<StatDef> Stats=new List<StatDef>();
  public static void ClearStatCache(Thing t){foreach(var s in Stats)s.Worker.ClearCacheForThing(t);}

  // 属性变化。
  public static float Transform(string kind,float val,int l){
   switch(kind){
    case "dmg":return val*(1f+DamagePer*l);
    case "acc":return Mathf.Max(val,Mathf.Min(val+AccuracyPer*l,.99f));
    case "armor":return val*(1f+ArmorPer*l)+ArmorFlatPer*l;
    case "value":return val*(1f+ValuePer*l);
   }
   return val;
  }
  public static bool Applies(string kind,Thing t)=>kind=="value" || (kind=="armor"?t.def.IsApparel:IsWeapon(t));
  public static string Effects(Thing t,int l){
   if(l<=0)return "无";
   var parts=new List<string>();
   if(IsWeapon(t)){parts.Add("伤害+"+(DamagePer*l).ToStringPercent());if(t.def.IsRangedWeapon)parts.Add("命中+"+(AccuracyPer*l).ToStringPercent());}
   if(t.def.IsApparel)parts.Add("护甲×"+(1f+ArmorPer*l).ToString("0.##")+"再+"+(ArmorFlatPer*l).ToStringPercent());
   parts.Add("价值+"+(ValuePer*l).ToStringPercent());
   return string.Join("，",parts);
  }

  public static void Register(){
   foreach(var d in DefDatabase<ThingDef>.AllDefsListForReading){
    if(!Eligible(d))continue;
    if(d.comps==null)d.comps=new List<CompProperties>();
    if(!d.comps.Any(c=>c is CompProperties_GearEnhance))d.comps.Add(new CompProperties_GearEnhance());
   }
   void Add(string stat,string kind){
    var d=DefDatabase<StatDef>.GetNamedSilentFail(stat);if(d==null)return;
    if(d.parts==null)d.parts=new List<StatPart>();
    if(!d.parts.Any(x=>x is StatPart_GearEnhance))d.parts.Add(new StatPart_GearEnhance{kind=kind,parentStat=d});
    if(!Stats.Contains(d))Stats.Add(d);
   }
   Add("MeleeWeapon_DamageMultiplier","dmg");Add("RangedWeapon_DamageMultiplier","dmg");
   Add("AccuracyTouch","acc");Add("AccuracyShort","acc");Add("AccuracyMedium","acc");Add("AccuracyLong","acc");
   Add("ArmorRating_Sharp","armor");Add("ArmorRating_Blunt","armor");Add("ArmorRating_Heat","armor");
   Add("MarketValue","value");
  }
 }

 public class CompProperties_GearEnhance : CompProperties {public CompProperties_GearEnhance(){compClass=typeof(CompGearEnhance);}}
 public class CompGearEnhance : ThingComp {
  public int level,target;
  public override void PostExposeData(){Scribe_Values.Look(ref level,"lpEnhance",0);Scribe_Values.Look(ref target,"lpEnhanceTarget",0);}
  public override string TransformLabel(string label)=>level>0?"+"+level+" "+label:label;
  public override string CompInspectStringExtra(){
   if(level<=0 && target<=level)return null;
   string s="附魔强化 +"+level+"："+GearEnhance.Effects(parent,level);
   if(target>level)s+="\n强化目标：+"+target;
   return s;
  }
  public override IEnumerable<StatDrawEntry> SpecialDisplayStats(){
   if(level>0)yield return new StatDrawEntry(StatCategoryDefOf.BasicsImportant,"附魔强化","+"+level,"在附魔台上强化过的等级。"+GearEnhance.Effects(parent,level)+"。",4990);
  }
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   foreach(var g in base.CompGetGizmosExtra())yield return g;
   int allowed=GearEnhance.Allowed;
   if(!parent.Spawned || allowed==0 || parent.Faction!=null && parent.Faction!=Faction.OfPlayer)yield break;
   var icon=DefDatabase<ThingDef>.GetNamedSilentFail(Workbenches.Enchanting)?.uiIcon;
   yield return new Command_Action{
    defaultLabel=target>level?"强化目标 +"+target:"附魔强化",
    defaultDesc="选这件装备在附魔台上强化到几级（现在+"+level+"，研究允许到+"+allowed+"）。附魔台上要有“附魔强化”工作单，并备好材料；到达目标后自动取消。",
    icon=icon??BaseContent.BadTex,
    action=()=>{
     var opts=new List<FloatMenuOption>{new FloatMenuOption("不强化",()=>target=0)};
     for(int t=level+1;t<=allowed;t++){int v=t;opts.Add(new FloatMenuOption("强化到 +"+v,()=>target=v));}
     if(level>=allowed)opts.Add(new FloatMenuOption(level>=GearEnhance.Max?"已经是+10":"需要“高阶强化”研究才能超过+5",null));
     Find.WindowStack.Add(new FloatMenu(opts));
    }
   };
  }
 }

 public class StatPart_GearEnhance : StatPart {
  public string kind;
  public override void TransformValue(StatRequest req,ref float val){var t=req.Thing;if(t==null)return;int l=GearEnhance.Level(t);if(l<=0 || !GearEnhance.Applies(kind,t))return;val=GearEnhance.Transform(kind,val,l);}
  public override string ExplanationPart(StatRequest req){
   var t=req.Thing;if(t==null)return null;int l=GearEnhance.Level(t);if(l<=0 || !GearEnhance.Applies(kind,t))return null;
   switch(kind){
    case "dmg":return "附魔强化 +"+l+"：×"+(1f+GearEnhance.DamagePer*l).ToString("0.##");
    case "acc":return "附魔强化 +"+l+"：+"+(GearEnhance.AccuracyPer*l).ToStringPercent()+"（最高99%）";
    case "armor":return "附魔强化 +"+l+"：×"+(1f+GearEnhance.ArmorPer*l).ToString("0.##")+"，+"+(GearEnhance.ArmorFlatPer*l).ToStringPercent();
    case "value":return "附魔强化 +"+l+"：×"+(1f+GearEnhance.ValuePer*l).ToString("0.##");
   }
   return null;
  }
 }

 public class RecipeWorker_GearEnhance : RecipeWorker {
  // 装备本身不消耗：留在附魔台旁边，搬运工会把它送回储存区。
  public override void ConsumeIngredient(Thing ingredient,RecipeDef recipe,Map map){if(GearEnhance.Comp(ingredient)!=null)return;base.ConsumeIngredient(ingredient,recipe,map);}
 }

 [HarmonyPatch(typeof(GenRecipe),nameof(GenRecipe.MakeRecipeProducts))]
 public static class GearEnhanceProducts {
  static bool Prefix(RecipeDef recipeDef,Pawn worker,List<Thing> ingredients,IBillGiver billGiver,ref IEnumerable<Thing> __result){
   if(!(recipeDef?.Worker is RecipeWorker_GearEnhance))return true;
   var gear=ingredients?.FirstOrDefault(t=>GearEnhance.Comp(t)!=null);
   if(gear!=null)GearEnhance.Apply(gear,worker,billGiver as Thing);
   __result=Enumerable.Empty<Thing>();return false;
  }
 }
 [HarmonyPatch(typeof(Bill),nameof(Bill.IsFixedOrAllowedIngredient),new[]{typeof(Thing)})]
 public static class GearEnhanceIngredient {
  static void Postfix(Bill __instance,Thing __0,ref bool __result){if(!__result || !(__instance.recipe?.Worker is RecipeWorker_GearEnhance))return;if(GearEnhance.Comp(__0)!=null)__result=GearEnhance.Wants(__0,__instance.recipe);}
 }

 [StaticConstructorOnStartup]
 public static class GearEnhanceStartup {
  static GearEnhanceStartup(){if(GenCommandLine.CommandLineArgPassed("lichtest"))GearEnhance.TestSuppressed=true;GearEnhance.Register();}
 }
}
