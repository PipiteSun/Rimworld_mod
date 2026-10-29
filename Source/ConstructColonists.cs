using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public partial class Hediff_Construct {
  public int colonistVersion;
  public void UpgradeColonist(){
   UpgradeAppearance();
   // Native meditation explanations require non-null backstories. Repair missing
   // stories before the version gate so existing colonist-compatible constructs migrate too.
   if(pawn.story!=null){
    if(pawn.story.Childhood==null)pawn.story.Childhood=Necro.Def<BackstoryDef>("ConstructOrigin");
    if(pawn.story.Adulthood==null && pawn.DevelopmentalStage.Adult())pawn.story.Adulthood=Necro.Def<BackstoryDef>("ConstructService");
   }
   EnforcePassionLimit();
   if(colonistVersion>=1 || pawn.genes==null)return;
   var body=Necro.Def<GeneDef>("NecroticBody");
   if(!pawn.genes.GenesListForReading.Any(g=>g.def==body))pawn.genes.AddGene(body,true);
   pawn.Notify_DisabledWorkTypesChanged();
   if(pawn.Faction==Faction.OfPlayer && pawn.workSettings!=null){pawn.workSettings.EnableAndInitialize();foreach(var work in DefDatabase<WorkTypeDef>.AllDefsListForReading)if(!pawn.WorkTypeIsDisabled(work) && pawn.workSettings.GetPriority(work)==0)pawn.workSettings.SetPriority(work,3);}
   colonistVersion=1;Necro.Refresh(pawn);
  }
 }
 // Custom bodies use the human body definition. Inherit the human surgery menu,
 // including recipes added by other mods; their own availability/part checks still run.
 [HarmonyPatch(typeof(ThingDef),"get_AllRecipes")]
 public static class ConstructHumanRecipes {
  static void Postfix(ThingDef __instance,ref List<RecipeDef> __result){
   if(__instance.defName!="LP_Skeleton" && __instance.defName!="LP_Golem" && __instance.defName!="LP_Scholar")return;
   // 1.58.6：健康页打开时每帧都会取；两边的列表（原版已缓存）没换就返回上次合并的结果，不再每次 Concat/Distinct/ToList。
   var human=ThingDefOf.Human.AllRecipes;
   if(cache.TryGetValue(__instance,out var c) && ReferenceEquals(c.own,__result) && c.ownCount==__result.Count && ReferenceEquals(c.human,human) && c.humanCount==human.Count){__result=c.merged;return;}
   var merged=__result.Concat(human).Distinct().ToList();cache[__instance]=(__result,__result.Count,human,human.Count,merged);__result=merged;
  }
  static readonly System.Collections.Concurrent.ConcurrentDictionary<ThingDef,(List<RecipeDef> own,int ownCount,List<RecipeDef> human,int humanCount,List<RecipeDef> merged)> cache=new System.Collections.Concurrent.ConcurrentDictionary<ThingDef,(List<RecipeDef>,int,List<RecipeDef>,int,List<RecipeDef>)>();
 }
 public partial class ConstructDesign {
  public Dictionary<SkillDef,Passion> passions=new Dictionary<SkillDef,Passion>();
  public Passion PassionFor(SkillDef skill,string kind){
   Passion value;if(passions!=null && passions.TryGetValue(skill,out value))return value;
   var main=specialty=="大脑"?SkillDefOf.Intellectual:specialty=="眼睛"?SkillDefOf.Crafting:specialty=="手臂"?SkillDefOf.Construction:SkillDefOf.Medicine;
   return kind=="Scholar" && skill==main?Passion.Major:Passion.None;
  }
  public void NormalizePassions(){if(passions==null)passions=new Dictionary<SkillDef,Passion>();foreach(var key in passions.Keys.ToList())passions[key]=(Passion)Mathf.Clamp((int)passions[key],0,2);}
 }
 public partial class Dialog_ConstructDesign {
  public bool skillsTab;
  void DrawPassions(Rect view){
   var skills=DefDatabase<SkillDef>.AllDefsListForReading.OrderBy(s=>s.listOrder).ToList();
   Widgets.Label(new Rect(12,8,view.width-24,44),(Editable?"点击切换：无兴趣 → 单火 → 双火。双火最多3项，单火不限。已选双火："+plan.MajorCount(kind)+"/3。":"当前实际技能兴趣；初始配置在唤醒时应用，后续变化由游戏正常记录。")+(kind=="Scholar"?"知识死灵技能固定，兴趣只决定传给主人的经验。":""));
   float column=view.width/2;
   for(int i=0;i<skills.Count;i++){
    var skill=skills[i];float x=(i%2)*column,y=64+(i/2)*52;
    var row=new Rect(x+8,y,column-16,44);Widgets.DrawMenuSection(row);
    var passion=Editable?plan.PassionFor(skill,kind):finished.pawn.skills.GetSkill(skill).passion;
    Widgets.Label(new Rect(x+20,y+9,column-165,30),skill.LabelCap);
    var button=new Rect(x+column-135,y+6,112,32);string label=passion==Passion.Major?"双火":passion==Passion.Minor?"单火":"无兴趣";
    if(Editable){if(Widgets.ButtonText(button,label)){if(!plan.TrySetPassion(skill,(Passion)(((int)passion+1)%3),kind))Messages.Message(ConstructDesign.PassionLimitReason,MessageTypeDefOf.RejectInput,false);}}else Widgets.Label(button,label);
   }
  }
 }
}
