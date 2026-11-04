using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LichPhylactery {
 // 1.114.0 热修（Peter）：附魔强化改成 Isekai 精炼炉那样在附魔台上操作——点附魔台的“附魔强化”打开窗口，
 // 左边挑装备（地上 / 储存区里的，加上殖民者手里和身上的），右边看材料和成功率，点按钮当场强化一次（从地图上扣材料）。
 // 执行的工匠是地图上智识够、智识和手工平均最高的殖民者。装备上的选目标按钮和两张工作单不再给新加（旧工作单照常做完）。
 public static class GearEnhanceBench {
  public static RecipeDef RecipeFor(int level)=>DefDatabase<RecipeDef>.GetNamedSilentFail(level<GearEnhance.LowMax?GearEnhance.LowRecipe:GearEnhance.HighRecipe);
  public static int SkillNeed(RecipeDef r)=>r?.skillRequirements?.FirstOrDefault(s=>s.skill==SkillDefOf.Intellectual)?.minLevel??0;
  public static List<KeyValuePair<ThingDef,int>> Costs(RecipeDef r)=>r==null?new List<KeyValuePair<ThingDef,int>>():r.ingredients.Where(i=>i.IsFixedIngredient).Select(i=>new KeyValuePair<ThingDef,int>(i.FixedIngredient,Mathf.RoundToInt(i.GetBaseCount()))).ToList();
  static IEnumerable<Thing> Stacks(Map map,ThingDef d)=>map.listerThings.ThingsOfDef(d).Where(t=>t.Spawned && !t.IsForbidden(Faction.OfPlayer));
  public static int CountOnMap(Map map,ThingDef d)=>map==null?0:Stacks(map,d).Sum(t=>t.stackCount);
  public static bool HasCosts(Map map,RecipeDef r)=>Costs(r).All(c=>CountOnMap(map,c.Key)>=c.Value);
  static void Consume(Map map,RecipeDef r){
   foreach(var c in Costs(r)){int need=c.Value;foreach(var t in Stacks(map,c.Key).ToList()){if(need<=0)break;int k=Mathf.Min(need,t.stackCount);t.SplitOff(k).Destroy();need-=k;}}
  }
  // 执行的工匠：能做智识工作、智识达到本档要求的殖民者里，智识和手工平均最高的那个。
  public static Pawn Operator(Map map,RecipeDef r){
   if(map==null)return null;int need=SkillNeed(r);
   return map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Downed && p.skills!=null && !p.WorkTagIsDisabled(WorkTags.Intellectual) && p.skills.GetSkill(SkillDefOf.Intellectual).Level>=need).OrderByDescending(GearEnhance.SkillBonus).FirstOrDefault();
  }
  // 附魔台能强化的装备：地图上没被禁用的，加上殖民者手里的武器和身上的衣服。
  public static List<Thing> Candidates(Map map){
   var list=new List<Thing>();if(map==null)return list;
   foreach(var t in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver))if(t.Spawned && GearEnhance.Comp(t)!=null && !t.IsForbidden(Faction.OfPlayer))list.Add(t);
   foreach(var p in map.mapPawns.FreeColonistsSpawned){
    var w=p.equipment?.Primary;if(w!=null && GearEnhance.Comp(w)!=null)list.Add(w);
    if(p.apparel!=null)foreach(var a in p.apparel.WornApparel)if(GearEnhance.Comp(a)!=null)list.Add(a);
   }
   return list.OrderBy(t=>Owner(t)==null?1:0).ThenBy(t=>Owner(t)?.LabelShort??"").ThenByDescending(GearEnhance.Level).ThenBy(t=>t.def.label).ToList();
  }
  public static Pawn Owner(Thing t)=>(t.ParentHolder as Pawn_EquipmentTracker)?.pawn??(t.ParentHolder as Pawn_ApparelTracker)?.pawn;
  // 为什么现在不能强化；null = 可以。
  public static string Blocked(Thing gear,Map map){
   var c=GearEnhance.Comp(gear);if(c==null)return "这件不能强化";
   if(c.level>=GearEnhance.Max)return "已经是+"+GearEnhance.Max;
   if(c.level>=GearEnhance.Allowed)return GearEnhance.Allowed==0?"需要先研究“装备强化”":"需要“高阶强化”研究才能超过+"+GearEnhance.LowMax;
   var r=RecipeFor(c.level);if(Operator(map,r)==null)return "没有智识"+SkillNeed(r)+"以上、能做智识工作的殖民者";
   if(!HasCosts(map,r))return "材料不够";
   return null;
  }
  // 当场强化一次：扣材料，按成功率升一级或失败（规则同 GearEnhance.Apply）。
  public static GearEnhance.Result TryEnhance(Thing gear,Thing bench){
   var map=bench?.Map;if(Blocked(gear,map)!=null)return GearEnhance.Result.None;
   var r=RecipeFor(GearEnhance.Level(gear));var op=Operator(map,r);Consume(map,r);
   var res=GearEnhance.Apply(gear,op,bench);GearEnhance.LastResult=GearEnhance.Result.None;
   if(res==GearEnhance.Result.Success)op?.skills?.Learn(SkillDefOf.Intellectual,200f);
   return res;
  }
 }

 public class CompProperties_EnhanceBench : CompProperties {public CompProperties_EnhanceBench(){compClass=typeof(CompEnhanceBench);}}
 public class CompEnhanceBench : ThingComp {
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   foreach(var g in base.CompGetGizmosExtra())yield return g;
   if(parent.Faction!=Faction.OfPlayer || !parent.Spawned)yield break;
   int allowed=GearEnhance.Allowed;
   var cmd=new Command_Action{defaultLabel="附魔强化",defaultDesc="打开附魔强化窗口：挑一件武器或衣服（地上、储存区里的，或殖民者手里、身上的），用地图上的材料当场强化一级。",icon=parent.def.uiIcon??BaseContent.BadTex,action=()=>Find.WindowStack.Add(new Window_GearEnhance(parent))};
   if(allowed==0)cmd.Disable("需要先研究“装备强化”。");
   yield return cmd;
  }
 }

 public class Window_GearEnhance : Window {
  readonly Thing bench;Thing selected;Vector2 scroll;string search="";string result;Color resultColor=Color.white;List<Thing> cache;int cacheFrame;
  const float ListW=250f,Row=40f;
  public override Vector2 InitialSize=>new Vector2(800f,520f);
  public Window_GearEnhance(Thing bench){this.bench=bench;doCloseX=true;forcePause=false;absorbInputAroundWindow=false;closeOnClickedOutside=false;draggable=true;}
  Map Map=>bench?.Map;
  public override void DoWindowContents(Rect r){
   if(bench==null || bench.Destroyed || !bench.Spawned){Close();return;}
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(r.x,r.y,r.width,32f),"附魔强化");Text.Font=GameFont.Small;
   var body=new Rect(r.x,r.y+38f,r.width,r.height-38f);
   var left=new Rect(body.x,body.y,ListW,body.height);var mid=new Rect(left.xMax+8f,body.y,(body.width-ListW-16f)*.5f,body.height);var right=new Rect(mid.xMax+8f,body.y,body.xMax-mid.xMax-8f,body.height);
   DrawList(left);DrawDetail(mid);DrawAction(right);
  }
  void DrawList(Rect r){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(4f);
   search=Widgets.TextField(new Rect(inner.x,inner.y,inner.width,24f),search);
   if(search.NullOrEmpty()){GUI.color=Color.gray;Widgets.Label(new Rect(inner.x+5f,inner.y+2f,inner.width,22f),"搜索…");GUI.color=Color.white;}
   if(cache==null || Time.frameCount-cacheFrame>30){cache=GearEnhanceBench.Candidates(Map);cacheFrame=Time.frameCount;}
   var items=cache;
   if(!search.NullOrEmpty())items=items.Where(t=>t.LabelNoCount.Contains(search) || (GearEnhanceBench.Owner(t)?.LabelShort?.Contains(search)??false)).ToList();
   if(selected!=null && (selected.Destroyed || !items.Contains(selected) && search.NullOrEmpty()))selected=null;
   var outRect=new Rect(inner.x,inner.y+28f,inner.width,inner.height-28f);var view=new Rect(0f,0f,outRect.width-16f,items.Count*Row);
   Widgets.BeginScrollView(outRect,ref scroll,view);
   for(int i=0;i<items.Count;i++){
    var t=items[i];var row=new Rect(0f,i*Row,view.width,Row);
    if(t==selected)Widgets.DrawHighlightSelected(row);else Widgets.DrawHighlightIfMouseover(row);
    Widgets.ThingIcon(new Rect(row.x+2f,row.y+4f,32f,32f),t);
    var owner=GearEnhanceBench.Owner(t);
    Text.Font=GameFont.Small;Widgets.Label(new Rect(row.x+38f,row.y+1f,row.width-38f,22f),t.LabelCapNoCount.Truncate(row.width-40f));
    Text.Font=GameFont.Tiny;GUI.color=Color.gray;Widgets.Label(new Rect(row.x+38f,row.y+21f,row.width-38f,18f),owner!=null?owner.LabelShort+(t.ParentHolder is Pawn_EquipmentTracker?" 手持":" 穿着"):"在地上 / 储存区");GUI.color=Color.white;Text.Font=GameFont.Small;
    if(Widgets.ButtonInvisible(row)){selected=t;result=null;}
   }
   Widgets.EndScrollView();
  }
  void DrawDetail(Rect r){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(8f);
   if(selected==null){Text.Anchor=TextAnchor.MiddleCenter;GUI.color=Color.gray;Widgets.Label(inner,"从左边选一件装备");GUI.color=Color.white;Text.Anchor=TextAnchor.UpperLeft;return;}
   int l=GearEnhance.Level(selected);float y=inner.y;
   Text.Font=GameFont.Medium;float h=Text.CalcHeight(selected.LabelCapNoCount,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),selected.LabelCapNoCount);Text.Font=GameFont.Small;y+=h+4f;
   if(selected.TryGetQuality(out var q)){GUI.color=Color.gray;Widgets.Label(new Rect(inner.x,y,inner.width,22f),"品质："+q.GetLabel());GUI.color=Color.white;y+=22f;}
   if(selected.def.useHitPoints){GUI.color=Color.gray;Widgets.Label(new Rect(inner.x,y,inner.width,22f),"耐久："+selected.HitPoints+" / "+selected.MaxHitPoints+"（强化成功会修满）");GUI.color=Color.white;y+=22f;}
   y+=6f;
   string now="现在 +"+l+"："+GearEnhance.Effects(selected,l);h=Text.CalcHeight(now,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),now);y+=h+6f;
   if(l<GearEnhance.Max){GUI.color=new Color(.4f,.9f,.4f);string next="强化到 +"+(l+1)+"："+GearEnhance.Effects(selected,l+1);h=Text.CalcHeight(next,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),next);GUI.color=Color.white;y+=h+6f;}
   if(result!=null){y+=8f;GUI.color=resultColor;h=Text.CalcHeight(result,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),result);GUI.color=Color.white;}
  }
  void DrawAction(Rect r){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(8f);if(selected==null)return;
   int l=GearEnhance.Level(selected);float y=inner.y;
   if(l>=GearEnhance.Max){Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(new Rect(inner.x,y,inner.width,40f),"已经是+"+GearEnhance.Max);Text.Anchor=TextAnchor.UpperLeft;return;}
   var rec=GearEnhanceBench.RecipeFor(l);var op=GearEnhanceBench.Operator(Map,rec);
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(inner.x,y,inner.width,30f),"强化到 +"+(l+1));Text.Font=GameFont.Small;y+=34f;
   Widgets.Label(new Rect(inner.x,y,inner.width,22f),"每次材料：");y+=22f;
   foreach(var c in GearEnhanceBench.Costs(rec)){
    int have=GearEnhanceBench.CountOnMap(Map,c.Key);Widgets.ThingIcon(new Rect(inner.x+4f,y+1f,20f,20f),c.Key);
    GUI.color=have>=c.Value?Color.white:new Color(.9f,.3f,.3f);Widgets.Label(new Rect(inner.x+28f,y,inner.width-28f,22f),c.Key.LabelCap+" ×"+c.Value+"（有 "+have+"）");GUI.color=Color.white;y+=22f;
   }
   y+=6f;
   float chance=GearEnhance.Chance(l,op,bench);
   var bar=new Rect(inner.x,y,inner.width,16f);Widgets.FillableBar(bar,chance);y+=20f;
   GUI.color=new Color(.4f,.9f,.4f);Widgets.Label(new Rect(inner.x,y,inner.width,22f),"成功率 "+chance.ToStringPercent());GUI.color=Color.white;y+=22f;
   string fail=l>GearEnhance.LowMax?"失败：掉回 +"+(l-1):"失败：只损失材料";GUI.color=l>GearEnhance.LowMax?new Color(.95f,.5f,.1f):Color.gray;Widgets.Label(new Rect(inner.x,y,inner.width,22f),fail);GUI.color=Color.white;y+=22f;
   string who=op!=null?"工匠："+op.LabelShort+"（智识 "+op.skills.GetSkill(SkillDefOf.Intellectual).Level+"，手工 "+op.skills.GetSkill(SkillDefOf.Crafting).Level+"）":"工匠：无（要智识 "+GearEnhanceBench.SkillNeed(rec)+"）";
   string extra=GearEnhance.HasFocusStone(bench)?"\n连着奥术聚能石 +"+GearEnhance.FocusBonus.ToStringPercent():"";
   GUI.color=Color.gray;float h=Text.CalcHeight(who+extra,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),who+extra);GUI.color=Color.white;y+=h+10f;
   string block=GearEnhanceBench.Blocked(selected,Map);
   var btn=new Rect(inner.x,y,inner.width,36f);
   if(block!=null){GUI.color=Color.gray;Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(btn,block);Text.Anchor=TextAnchor.UpperLeft;GUI.color=Color.white;return;}
   if(Widgets.ButtonText(btn,"强化到 +"+(l+1))){
    var gear=selected;
    if(l>GearEnhance.LowMax)Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("强化到 +"+(l+1)+"，成功率 "+chance.ToStringPercent()+"。失败会掉回 +"+(l-1)+"，确定？",()=>Do(gear)));
    else Do(gear);
   }
  }
  void Do(Thing gear){
   int from=GearEnhance.Level(gear);var res=GearEnhanceBench.TryEnhance(gear,bench);int to=GearEnhance.Level(gear);
   switch(res){
    case GearEnhance.Result.Success:result="成功！现在 +"+to;resultColor=new Color(.4f,.9f,.4f);SoundDefOf.Quest_Concluded.PlayOneShotOnCamera();break;
    case GearEnhance.Result.Fail:result="失败，材料没了，还是 +"+to;resultColor=Color.gray;SoundDefOf.ClickReject.PlayOneShotOnCamera();break;
    case GearEnhance.Result.Drop:result="失败，掉回了 +"+to;resultColor=new Color(.95f,.5f,.1f);SoundDefOf.ClickReject.PlayOneShotOnCamera();break;
    default:result=GearEnhanceBench.Blocked(gear,Map)??"没能强化";resultColor=Color.gray;break;
   }
  }
 }
}
