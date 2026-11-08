using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LichPhylactery {
 // 1.119.0 扭曲肉资源化：A 节描述、C 节炼金产物（再生膏瘤块、狂乱之瓶）、F3 畸变皮甲、C5 附魔台「血肉再生」词条。
 [StaticConstructorOnStartup]
 public static class TwistedMeatStartup {
  public const string FleshNote="\n\n扭曲肉 1 份算 2 份生肉，畸变膏 1 份算 20 份生肉（畸变膏要在工作单里勾选）。";
  public static readonly string[] FleshRecipes={"LP_Offer","LP_MakeGolem","LP_Refill","LP_CraftBlindSight","LP_CraftWitheringHand","LP_CraftDeadPersonality","LP_CraftShadowLeg"};
  static TwistedMeatStartup(){
   if(!TwistedMeat.Active)return;
   try{
    foreach(var n in FleshRecipes){var r=DefDatabase<RecipeDef>.GetNamedSilentFail(n);if(r!=null && r.description!=null && !r.description.Contains("扭曲肉"))r.description+=FleshNote;}
    var beauty=StatDefOf.PawnBeauty;if(beauty!=null){if(beauty.parts==null)beauty.parts=new List<StatPart>();if(!beauty.parts.Any(p=>p is StatPart_FleshRegen))beauty.parts.Add(new StatPart_FleshRegen{parentStat=beauty});}
   }catch(Exception e){Log.Error("[DND边缘大陆] 扭曲肉启动失败："+e);}
  }
 }

 // ---------- C2 畸变再生膏：15% 在治疗部位留下畸变瘤块 ----------
 [HarmonyPatch(typeof(TendUtility),nameof(TendUtility.DoTend))]
 public static class AberrantSalveTumor {
  public const float Chance=.15f;
  public static bool ForceTumor; // 自测
  static void Prefix(Medicine medicine,out ThingDef __state){__state=medicine?.def;}
  static void Postfix(Pawn patient,ThingDef __state){
   if(__state==null || __state.defName!="LP_AberrantSalve" || patient?.health==null)return;
   if(!ForceTumor && !Rand.Chance(Chance))return;
   var def=DefCache<HediffDef>.Get("LP_AberrantTumor");if(def==null)return;
   var parts=patient.health.hediffSet.hediffs.Where(h=>h.Part!=null && h.IsTended()).Select(h=>h.Part).Distinct().ToList();
   var part=parts.Where(p=>!patient.health.hediffSet.hediffs.Any(h=>h.def==def && h.Part==p)).RandomElementWithFallback();if(part==null)return;
   patient.health.AddHediff(def,part);
   if(patient.Faction==Faction.OfPlayer)Messages.Message(patient.LabelShort+"的"+part.Label+"上长出了一块畸变瘤块。",patient,MessageTypeDefOf.NegativeHealthEvent,false);
  }
 }

 // ---------- C4 狂乱之瓶 ----------
 public class Projectile_FrenzyFlask : Projectile {
  public const float Radius=3f,Chance=.6f;public const int FleeTicks=480;
  protected override void Impact(Thing hitThing,bool blockedByShield=false){
   var map=Map;var c=Position;
   if(map!=null){
    FleckMaker.Static(c.ToVector3Shifted(),map,FleckDefOf.PsycastAreaEffect,Radius*2f);
    var filth=DefCache<ThingDef>.Get("Filth_TwistedFlesh");if(filth!=null)FilthMaker.TryMakeFilth(c,map,filth,2);
    Frenzy(map,c,launcher as Pawn);
   }
   base.Impact(hitThing,blockedByShield);
  }
  public static bool Immune(Pawn p)=>p==null || p.Dead || p.RaceProps.IsMechanoid || Items.Has(p,"LP_PotionMindShield") || p.InMentalState;
  public static int Frenzy(Map map,IntVec3 c,Pawn thrower,float chance=Chance){
   int n=0;
   foreach(var p in GenRadial.RadialDistinctThingsAround(c,map,Radius,true).OfType<Pawn>().ToList()){
    if(Immune(p) || p.Downed || !Rand.Chance(chance))continue;
    if(p.mindState?.mentalStateHandler==null)continue;
    if(p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"狂乱之瓶",forced:true)){if(p.MentalState!=null)p.MentalState.forceRecoverAfterTicks=FleeTicks;n++;}
   }
   return n;
  }
 }

 // ---------- F3 畸变皮甲：他人好感 -5；每天愈合一处小伤（和血肉再生词条共用） ----------
 public class ThoughtWorker_AberrantHide : ThoughtWorker {
  protected override ThoughtState CurrentSocialStateInternal(Pawn p,Pawn other){
   if(p==other || other?.apparel==null || !p.RaceProps.Humanlike)return false;
   foreach(var a in other.apparel.WornApparel)if(a.def.defName=="LP_AberrantHideArmor")return true;
   return false;
  }
 }
 public static class FleshRegen {
  public const int PasteCost=5;public const float SmallInjury=6f;
  public static bool Marked(Thing t){var c=TwistedMeat.Comp;return t!=null && c!=null && c.regenGear.Contains(t.thingIDNumber);}
  public static bool HealsDaily(Apparel a)=>a!=null && (a.def.defName=="LP_AberrantHideArmor" || Marked(a));
  public static int MarkedCount(Pawn p){int n=0;if(p?.apparel!=null)foreach(var a in p.apparel.WornApparel)if(Marked(a))n++;return n;}
  // 治好一处最轻的、未永久的伤（大于 6 点的伤只治 6 点）。
  public static bool HealOne(Pawn p){
   var inj=p?.health?.hediffSet?.hediffs.OfType<Hediff_Injury>().Where(h=>!h.IsPermanent()).OrderBy(h=>h.Severity).FirstOrDefault();
   if(inj==null)return false;inj.Heal(Mathf.Min(inj.Severity,SmallInjury));return true;
  }
  public static void Daily(){
   foreach(var map in Find.Maps)foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()){
    if(p.apparel==null || p.Dead)continue;int n=0;foreach(var a in p.apparel.WornApparel)if(HealsDaily(a))n++;
    for(int i=0;i<n;i++)HealOne(p);
   }
  }
  public static bool CanApply(Thing t)=>t is Apparel a && !Marked(a) && a.def.apparel!=null && (a.def.apparel.layers.Contains(ApparelLayerDefOf.Middle) || a.def.apparel.layers.Contains(ApparelLayerDefOf.Shell)) && (a.GetStatValue(StatDefOf.ArmorRating_Sharp)>=.2f || a.def.thingCategories?.Contains(ThingCategoryDefOf.ApparelArmor)==true);
  public static string Blocked(Thing t,Map map){
   if(TwistedMeat.Paste==null)return "没有畸变膏（需要异象）";
   if(Marked(t))return "已经有“血肉再生”";
   if(!CanApply(t))return "只能刻在护甲上（中层或外层）";
   if(GearEnhanceBench.CountOnMap(map,TwistedMeat.Paste)<PasteCost)return "畸变膏不够（要 "+PasteCost+" 份）";
   return null;
  }
  public static bool Apply(Thing t,Map map){
   if(Blocked(t,map)!=null)return false;int need=PasteCost;
   foreach(var s in map.listerThings.ThingsOfDef(TwistedMeat.Paste).Where(x=>x.Spawned && !x.IsForbidden(Faction.OfPlayer)).ToList()){if(need<=0)break;int k=Mathf.Min(need,s.stackCount);s.SplitOff(k).Destroy();need-=k;}
   TwistedMeat.Comp.regenGear.Add(t.thingIDNumber);return true;
  }
 }
 public class StatPart_FleshRegen : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p){int n=FleshRegen.MarkedCount(p);if(n>0)val-=n;}}
  public override string ExplanationPart(StatRequest req){if(req.Thing is Pawn p){int n=FleshRegen.MarkedCount(p);if(n>0)return "血肉再生护甲：-"+n;}return null;}
 }
 // 附魔台多一个按钮「血肉再生」（照附魔强化窗口的样子）。
 [HarmonyPatch(typeof(CompEnhanceBench),nameof(CompEnhanceBench.CompGetGizmosExtra))]
 public static class FleshRegenGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos,CompEnhanceBench __instance){
   foreach(var g in gizmos)yield return g;
   var b=__instance.parent;if(!TwistedMeat.Active || TwistedMeat.Paste==null || b.Faction!=Faction.OfPlayer || !b.Spawned)yield break;
   yield return new Command_Action{defaultLabel="血肉再生",defaultDesc="给一件护甲刻上“血肉再生”词条：用 "+FleshRegen.PasteCost+" 份畸变膏做辅材。穿着者每天恢复一处小伤，但美观 -1。",icon=TwistedMeat.Icon("TwistedMeat/FleshRegenGizmo"),action=()=>Find.WindowStack.Add(new Window_FleshRegen(b))};
  }
 }
 public class Window_FleshRegen : Window {
  readonly Thing bench;Thing selected;Vector2 scroll;string result;List<Thing> cache;int cacheFrame;
  public override Vector2 InitialSize=>LPUI.Fit(620f,520f);
  public Window_FleshRegen(Thing bench){this.bench=bench;doCloseX=true;forcePause=false;absorbInputAroundWindow=false;draggable=true;}
  // 1.124.0 UI 整理：统一标题；列表第二行放不下时截断（悬停看全文）；画完复位文字状态。
  public override void DoWindowContents(Rect r){try{Draw(r);}finally{LPUI.Reset();}}
  void Draw(Rect r){
   if(bench==null || !bench.Spawned){Close();return;}var map=bench.Map;
   LPUI.Title(r,"附魔词条 · 血肉再生");
   var left=new Rect(r.x,r.y+38f,260f,r.height-38f);var right=new Rect(left.xMax+8f,left.y,r.xMax-left.xMax-8f,left.height);
   Widgets.DrawMenuSection(left);Widgets.DrawMenuSection(right);
   if(cache==null || Time.frameCount-cacheFrame>30){cache=GearEnhanceBench.Candidates(map).Where(t=>t is Apparel).ToList();cacheFrame=Time.frameCount;}
   var outRect=left.ContractedBy(4f);var view=new Rect(0f,0f,outRect.width-16f,cache.Count*40f);
   Widgets.BeginScrollView(outRect,ref scroll,view);
   for(int i=0;i<cache.Count;i++){
    var t=cache[i];var row=new Rect(0f,i*40f,view.width,40f);
    if(t==selected)Widgets.DrawHighlightSelected(row);else Widgets.DrawHighlightIfMouseover(row);
    Widgets.ThingIcon(new Rect(row.x+2f,row.y+4f,32f,32f),t);
    Widgets.Label(new Rect(row.x+38f,row.y+1f,row.width-38f,22f),t.LabelCapNoCount.Truncate(row.width-40f));
    Text.Font=GameFont.Tiny;GUI.color=FleshRegen.Marked(t)?new Color(.85f,.45f,.45f):Color.gray;var o=GearEnhanceBench.Owner(t);
    LPUI.LabelFit(new Rect(row.x+38f,row.y+21f,row.width-38f,18f),(FleshRegen.Marked(t)?"血肉再生 · ":"")+(o!=null?o.LabelShort+" 穿着":"在地上 / 储存区"));GUI.color=Color.white;Text.Font=GameFont.Small;
    if(Widgets.ButtonInvisible(row)){selected=t;result=null;}
   }
   Widgets.EndScrollView();
   var inner=right.ContractedBy(8f);float y=inner.y;
   string intro="穿着者每天恢复一处小伤（最轻的那处，最多 "+FleshRegen.SmallInjury.ToString("0")+" 点），美观 -1。只能刻在护甲上。\n材料：畸变膏 ×"+FleshRegen.PasteCost+"（地图上有 "+GearEnhanceBench.CountOnMap(map,TwistedMeat.Paste)+"）。不会失败。";
   float h=Text.CalcHeight(intro,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),intro);y+=h+10f;
   if(selected!=null){
    Widgets.Label(new Rect(inner.x,y,inner.width,24f),selected.LabelCapNoCount);y+=28f;
    string block=FleshRegen.Blocked(selected,map);var btn=new Rect(inner.x,y,inner.width,34f);
    if(block!=null)LPUI.Unavailable(btn,block);
    else if(Widgets.ButtonText(btn,"刻上血肉再生")){if(FleshRegen.Apply(selected,map)){result="完成：“"+selected.LabelCapNoCount+"”有了血肉再生。";SoundDefOf.Quest_Concluded.PlayOneShotOnCamera();cache=null;}}
    y+=40f;
   }
   if(result!=null){GUI.color=new Color(.4f,.9f,.4f);Widgets.Label(new Rect(inner.x,y,inner.width,Text.CalcHeight(result,inner.width)),result);GUI.color=Color.white;}
  }
 }
}
