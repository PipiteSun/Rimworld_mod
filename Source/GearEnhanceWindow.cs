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
  static void Consume(Map map,RecipeDef r){foreach(var c in Costs(r))Consume(map,c.Key,c.Value);}
  public static void Consume(Map map,ThingDef d,int need){foreach(var t in Stacks(map,d).ToList()){if(need<=0)break;int k=Mathf.Min(need,t.stackCount);t.SplitOff(k).Destroy();need-=k;}}

  // 1.118.0 修复耐久（照 Isekai 精炼炉的“修复”）：每缺 20% 耐久要 10 白银（不足 20% 也算一份），当场修满。
  // 不用研究、不用工匠、不会失败，Isekai 没开也一样能用。
  public const int RepairSilverPer=10;
  public static bool NeedsRepair(Thing t)=>t!=null && !t.Destroyed && t.def.useHitPoints && t.HitPoints<t.MaxHitPoints;
  public static int RepairCost(Thing t){
   if(!NeedsRepair(t))return 0;int max=t.MaxHitPoints,missing=max-t.HitPoints;
   return RepairSilverPer*Mathf.Max(1,(missing*5+max-1)/max);
  }
  public static bool CanRepair(Thing t,Map map)=>NeedsRepair(t) && CountOnMap(map,ThingDefOf.Silver)>=RepairCost(t);
  public static bool Repair(Thing t,Map map){
   if(!CanRepair(t,map))return false;
   Consume(map,ThingDefOf.Silver,RepairCost(t));t.HitPoints=t.MaxHitPoints;return true;
  }
  // 按列表顺序修，白银用完为止；返回修好的件数。
  public static int RepairAll(Map map){int n=0;foreach(var t in Candidates(map))if(Repair(t,map))n++;return n;}
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
   var c=GearEnhance.Comp(gear);if(c==null)return "这件不能强化".Loc();
   if(c.level>=GearEnhance.Max)return "已经是+".Loc()+GearEnhance.Max;
   if(c.level>=GearEnhance.Allowed)return GearEnhance.Allowed==0?"需要先研究“装备强化”".Loc():"需要“高阶强化”研究才能超过+".Loc()+GearEnhance.LowMax;
   var r=RecipeFor(c.level);if(Operator(map,r)==null)return "没有智识".Loc()+SkillNeed(r)+"以上、能做智识工作的殖民者".Loc();
   if(!HasCosts(map,r))return "材料不够".Loc();
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
   // 1.118.0：窗口里还能修耐久，所以没研究“装备强化”也能打开（强化那边会写着要先研究）。
   yield return new Command_Action{defaultLabel="附魔强化".Loc(),defaultDesc="打开附魔强化窗口：挑一件武器或衣服（地上、储存区里的，或殖民者手里、身上的），用地图上的材料当场强化一级（要先研究“装备强化”）；也可以用白银当场修满耐久（每缺 20% 耐久 ".Loc()+GearEnhanceBench.RepairSilverPer+" 白银）。".Loc(),icon=parent.def.uiIcon??BaseContent.BadTex,action=()=>Find.WindowStack.Add(new Window_GearEnhance(parent))};
  }
 }

 public class Window_GearEnhance : Window {
  readonly Thing bench;Thing selected;Vector2 scroll;string search="";string result;Color resultColor=Color.white;List<Thing> cache;int cacheFrame;
  const float ListW=250f,Row=40f;
  const float RepairH=112f;
  public override Vector2 InitialSize=>LPUI.Fit(800f,580f);
  public Window_GearEnhance(Thing bench){this.bench=bench;doCloseX=true;forcePause=false;absorbInputAroundWindow=false;closeOnClickedOutside=false;draggable=true;}
  Map Map=>bench?.Map;
  // 1.124.0 UI 整理：中栏说明可滚动（效果说明很长时不再画出框外）；列表第二行、材料行放不下时截断（悬停看全文）；
  // 窗口被压矮时整块滚动；画完复位文字状态。
  Vector2 outerScroll,detailScroll;float detailH;
  public override void DoWindowContents(Rect r){bool sc=LPUI.BeginMinHeight(ref r,480f,ref outerScroll);try{Draw(r);}finally{if(sc)Widgets.EndScrollView();LPUI.Reset();}}
  void Draw(Rect r){
   if(bench==null || bench.Destroyed || !bench.Spawned){Close();return;}
   float top=LPUI.Title(r,"附魔强化 · 修复".Loc());
   var body=new Rect(r.x,top,r.width,r.yMax-top);
   var left=new Rect(body.x,body.y,ListW,body.height);var mid=new Rect(left.xMax+8f,body.y,(body.width-ListW-16f)*.5f,body.height);var right=new Rect(mid.xMax+8f,body.y,body.xMax-mid.xMax-8f,body.height);
   DrawList(left);DrawDetail(mid);DrawAction(right);
  }
  void DrawList(Rect r){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(4f);
   search=Widgets.TextField(new Rect(inner.x,inner.y,inner.width,24f),search);
   if(search.NullOrEmpty()){GUI.color=Color.gray;Widgets.Label(new Rect(inner.x+5f,inner.y+2f,inner.width,22f),"搜索…".Loc());GUI.color=Color.white;}
   if(cache==null || Time.frameCount-cacheFrame>30){cache=GearEnhanceBench.Candidates(Map);cacheFrame=Time.frameCount;}
   var items=cache;
   if(!search.NullOrEmpty())items=items.Where(t=>t.LabelNoCount.Contains(search) || (GearEnhanceBench.Owner(t)?.LabelShort?.Contains(search)??false)).ToList();
   if(selected!=null && (selected.Destroyed || !items.Contains(selected) && search.NullOrEmpty()))selected=null;
   // 1.118.0：底部“全部修复”。
   int damaged=0,allCost=0;foreach(var t in cache)if(GearEnhanceBench.NeedsRepair(t)){damaged++;allCost+=GearEnhanceBench.RepairCost(t);}
   var allBtn=new Rect(inner.x,inner.yMax-28f,inner.width,28f);
   if(damaged==0){GUI.color=Color.gray;Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(allBtn,"没有要修的装备".Loc());Text.Anchor=TextAnchor.UpperLeft;GUI.color=Color.white;}
   else if(Widgets.ButtonText(allBtn,"全部修复：".Loc()+damaged+" 件，".Loc()+allCost+" 白银".Loc())){
    int n=GearEnhanceBench.RepairAll(Map);cache=null;
    if(n>0){result="修好了 ".Loc()+n+" 件装备".Loc()+(n<damaged?"（白银不够，还剩 ".Loc()+(damaged-n)+" 件）".Loc():"");resultColor=new Color(.4f,.9f,.4f);SoundDefOf.Quest_Concluded.PlayOneShotOnCamera();}
    else{result="白银不够，一件都没修".Loc();resultColor=Color.gray;SoundDefOf.ClickReject.PlayOneShotOnCamera();}
    return;
   }
   var outRect=new Rect(inner.x,inner.y+28f,inner.width,inner.height-60f);var view=new Rect(0f,0f,outRect.width-16f,items.Count*Row);
   Widgets.BeginScrollView(outRect,ref scroll,view);
   for(int i=0;i<items.Count;i++){
    var t=items[i];var row=new Rect(0f,i*Row,view.width,Row);
    if(t==selected)Widgets.DrawHighlightSelected(row);else Widgets.DrawHighlightIfMouseover(row);
    Widgets.ThingIcon(new Rect(row.x+2f,row.y+4f,32f,32f),t);
    var owner=GearEnhanceBench.Owner(t);
    Text.Font=GameFont.Small;LPUI.LabelFit(new Rect(row.x+38f,row.y+1f,row.width-40f,22f),t.LabelCapNoCount);
    string where=owner!=null?owner.LabelShort+(t.ParentHolder is Pawn_EquipmentTracker?" 手持".Loc():" 穿着".Loc()):"在地上 / 储存区".Loc();bool hurt=GearEnhanceBench.NeedsRepair(t);
    if(hurt)where+="，耐久 ".Loc()+((float)t.HitPoints/t.MaxHitPoints).ToStringPercent();
    Text.Font=GameFont.Tiny;GUI.color=hurt?new Color(.95f,.6f,.3f):Color.gray;LPUI.LabelFit(new Rect(row.x+38f,row.y+21f,row.width-38f,18f),where);GUI.color=Color.white;Text.Font=GameFont.Small;
    if(Widgets.ButtonInvisible(row)){selected=t;result=null;detailScroll=Vector2.zero;}
   }
   Widgets.EndScrollView();
  }
  void DrawDetail(Rect r){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(8f);
   if(selected==null){
    Text.Anchor=TextAnchor.MiddleCenter;GUI.color=Color.gray;Widgets.Label(inner,"从左边选一件装备".Loc());GUI.color=Color.white;Text.Anchor=TextAnchor.UpperLeft;
    if(result!=null){GUI.color=resultColor;Widgets.Label(new Rect(inner.x,inner.y,inner.width,Text.CalcHeight(result,inner.width)),result);GUI.color=Color.white;}
    return;
   }
   bool bar=detailH>inner.height;var view=new Rect(0f,0f,inner.width-(bar?LPUI.ScrollBarW:0f),Mathf.Max(detailH,inner.height));
   Widgets.BeginScrollView(inner,ref detailScroll,view);
   try{detailH=DetailBody(view);}finally{Widgets.EndScrollView();}
  }
  float DetailBody(Rect inner){
   int l=GearEnhance.Level(selected);float y=inner.y;
   Text.Font=GameFont.Medium;float h=Text.CalcHeight(selected.LabelCapNoCount,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),selected.LabelCapNoCount);Text.Font=GameFont.Small;y+=h+4f;
   if(selected.TryGetQuality(out var q)){GUI.color=Color.gray;Widgets.Label(new Rect(inner.x,y,inner.width,22f),"品质：".Loc()+q.GetLabel());GUI.color=Color.white;y+=22f;}
   if(selected.def.useHitPoints){GUI.color=GearEnhanceBench.NeedsRepair(selected)?new Color(.95f,.6f,.3f):Color.gray;Widgets.Label(new Rect(inner.x,y,inner.width,22f),"耐久：".Loc()+selected.HitPoints+" / "+selected.MaxHitPoints+"（强化成功会修满）".Loc());GUI.color=Color.white;y+=22f;}
   y+=6f;
   string now="现在 +".Loc()+l+"：".Loc()+GearEnhance.Effects(selected,l);h=Text.CalcHeight(now,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),now);y+=h+6f;
   if(l<GearEnhance.Max){GUI.color=new Color(.4f,.9f,.4f);string next="强化到 +".Loc()+(l+1)+"：".Loc()+GearEnhance.Effects(selected,l+1);h=Text.CalcHeight(next,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),next);GUI.color=Color.white;y+=h+6f;}
   if(result!=null){y+=8f;GUI.color=resultColor;h=Text.CalcHeight(result,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),result);GUI.color=Color.white;y+=h;}
   if(SentientWeapon.Comp(selected)!=null)y=PersonaBody(inner,y+10f);
   return y-inner.y+4f;
  }
  // 1.168.0 人格武器：中栏下半部分，唤醒 / 重塑人格、解除羁绊。
  float PersonaBody(Rect inner,float y){
   var c=SentientWeapon.Comp(selected);float h;
   Widgets.DrawLineHorizontal(inner.x,y,inner.width);y+=6f;
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(inner.x,y,inner.width,30f),"人格".Loc());Text.Font=GameFont.Small;y+=32f;
   string info;
   if(c.persona==null)info="唤醒武器里沉睡的意识：随机得到一种人格（嗜血、守护、智者、猎手、迅影、狂怒、静谧、王者之一）。第一个装备它的殖民者会和它结下羁绊，之后只有羁绊者能用它。羁绊者拿着它时获得人格加成和好心情；拿着它杀敌、每拿一天都会涨共鸣，共鸣 ".Loc()+SentientWeapon.AwakenTier+" 苏醒、".Loc()+SentientWeapon.ExaltTier+" 升华，加成更强。要先强化到 +".Loc()+SentientWeapon.MinLevel+"。".Loc();
   else{
    info=c.persona.LabelCap+"（"+SentientWeapon.TierLabels[c.Tier]+"，共鸣 ".Loc()+Mathf.FloorToInt(c.resonance)+(c.Tier<3?" / "+(c.Tier==1?SentientWeapon.AwakenTier:SentientWeapon.ExaltTier):"")+"）\n"+c.persona.description+"\n"+"拿着时：".Loc()+SentientWeapon.Bonuses(c.persona,c.Tier);
    if(c.Tier<3)info+="\n"+(c.Tier==1?"苏醒后：".Loc():"升华后：".Loc())+SentientWeapon.Bonuses(c.persona,c.Tier+1);
    info+="\n"+(c.bonded!=null?"羁绊：".Loc()+c.bonded.LabelShort:"尚未结下羁绊，下一个装备它的殖民者会成为羁绊者".Loc());
   }
   h=Text.CalcHeight(info,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),info);y+=h+6f;
   var op=GearEnhanceBench.Operator(Map,SentientWeapon.SkillRecipe);float chance=SentientWeapon.Chance(op,bench);
   string cost=string.Join("，".Loc(),SentientWeapon.Costs().Select(k=>k.Key.LabelCap+" ×"+k.Value+"（有 ".Loc()+GearEnhanceBench.CountOnMap(Map,k.Key)+"）".Loc()));
   GUI.color=Color.gray;string line=(c.persona==null?"唤醒".Loc():"重塑（换一种人格，共鸣保留）".Loc())+"材料：".Loc()+cost+"\n"+"成功率 ".Loc()+chance.ToStringPercent()+"，失败只损失材料".Loc();
   h=Text.CalcHeight(line,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),line);GUI.color=Color.white;y+=h+4f;
   var btn=new Rect(inner.x,y,inner.width,32f);string block=SentientWeapon.Blocked(selected,Map);
   if(block!=null)LPUI.Unavailable(btn,block);
   else if(Widgets.ButtonText(btn,c.persona==null?"唤醒人格".Loc():"重塑人格".Loc())){
    var w=selected;
    if(c.persona!=null)Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("重塑会把「".Loc()+c.persona.label+"」换成另一种人格（成功率 ".Loc()+chance.ToStringPercent()+"），确定？".Loc(),()=>DoAwaken(w)));
    else DoAwaken(w);
   }
   y+=36f;
   // 1.170.0 封魂入器：选一名囚犯，把他的灵魂封进来当器灵（不会失败，材料同唤醒）。
   string soulInfo="封魂入器：把地图上一名囚犯的灵魂封进这把武器当器灵。囚犯会死去；武器按他最擅长的技能得到人格，名字就是他的名字，初始共鸣 = 最高技能 ×2；羁绊者拿着时心情 +5（器灵在哀嚎），生前讨厌他的殖民者心情 +8（15 天）。材料同上，不会失败。".Loc();
   GUI.color=Color.gray;h=Text.CalcHeight(soulInfo,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),soulInfo);GUI.color=Color.white;y+=h+4f;
   var sbtn=new Rect(inner.x,y,inner.width,32f);string sblock=SentientWeapon.BlockedSoul(selected,Map);
   if(sblock!=null)LPUI.Unavailable(sbtn,sblock);
   else if(Widgets.ButtonText(sbtn,"封魂入器…".Loc())){
    var w=selected;var opts=new List<FloatMenuOption>();
    foreach(var pr in SentientWeapon.Prisoners(Map)){var pp=pr;var per=SentientWeapon.PersonaFor(pp);
     opts.Add(new FloatMenuOption(pp.LabelShort+" → "+(per?.label??"?")+"（共鸣 ".Loc()+Mathf.FloorToInt(SentientWeapon.SoulResonance(pp))+"）",()=>Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("把 ".Loc()+pp.LabelShort+" 的灵魂封进 ".Loc()+w.LabelNoCount+"？".Loc()+pp.LabelShort+" 会死去，灵魂永远困在武器里。".Loc(),()=>DoBindSoul(w,pp),true))));}
    Find.WindowStack.Add(new FloatMenu(opts));
   }
   y+=36f;
   if(c.bonded!=null && Widgets.ButtonText(new Rect(inner.x,y,inner.width,28f),"解除与 ".Loc()+c.bonded.LabelShort+" 的羁绊".Loc())){
    var w=selected;Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("解除后 ".Loc()+c.bonded.LabelShort+" 会难过几天，武器可以和别人重新结下羁绊（共鸣保留）。确定？".Loc(),()=>{SentientWeapon.Comp(w)?.Unbond(true);result="羁绊已解除".Loc();resultColor=Color.gray;}));
   }
   if(c.bonded!=null)y+=32f;
   return y;
  }
  void DoBindSoul(Thing w,Pawn prisoner){
   var res=SentientWeapon.TryBindSoul(w,prisoner,bench);var c=SentientWeapon.Comp(w);
   if(res==GearEnhance.Result.Success){result=c.soulName+"的灵魂被封进去了，器灵人格：「".Loc()+c.persona.label+"」";resultColor=new Color(.75f,.6f,1f);SoundDefOf.Quest_Concluded.PlayOneShotOnCamera();}
   else{result=SentientWeapon.BlockedSoul(w,Map)??"没能封魂".Loc();resultColor=Color.gray;SoundDefOf.ClickReject.PlayOneShotOnCamera();}
  }
  void DoAwaken(Thing w){
   bool had=SentientWeapon.Awake(w);var res=SentientWeapon.TryAwaken(w,bench);var c=SentientWeapon.Comp(w);
   if(res==GearEnhance.Result.Success){result=(had?"人格重塑为「".Loc():"唤醒成功！人格：「".Loc())+c.persona.label+"」";resultColor=new Color(.75f,.6f,1f);SoundDefOf.Quest_Concluded.PlayOneShotOnCamera();}
   else if(res==GearEnhance.Result.Fail){result="唤醒失败，材料没了".Loc();resultColor=Color.gray;SoundDefOf.ClickReject.PlayOneShotOnCamera();}
   else{result=SentientWeapon.Blocked(w,Map)??"没能唤醒".Loc();resultColor=Color.gray;}
  }
  void DrawAction(Rect r){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(8f);if(selected==null)return;
   DrawRepair(new Rect(inner.x,inner.yMax-RepairH,inner.width,RepairH));inner.height-=RepairH+6f;
   int l=GearEnhance.Level(selected);float y=inner.y;
   if(l>=GearEnhance.Max){Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(new Rect(inner.x,y,inner.width,40f),"已经是+".Loc()+GearEnhance.Max);Text.Anchor=TextAnchor.UpperLeft;return;}
   var rec=GearEnhanceBench.RecipeFor(l);var op=GearEnhanceBench.Operator(Map,rec);
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(inner.x,y,inner.width,30f),"强化到 +".Loc()+(l+1));Text.Font=GameFont.Small;y+=34f;
   Widgets.Label(new Rect(inner.x,y,inner.width,22f),"每次材料：".Loc());y+=22f;
   foreach(var c in GearEnhanceBench.Costs(rec)){
    int have=GearEnhanceBench.CountOnMap(Map,c.Key);Widgets.ThingIcon(new Rect(inner.x+4f,y+1f,20f,20f),c.Key);
    GUI.color=have>=c.Value?Color.white:new Color(.9f,.3f,.3f);LPUI.LabelFit(new Rect(inner.x+28f,y,inner.width-28f,22f),c.Key.LabelCap+" ×"+c.Value+"（有 ".Loc()+have+"）".Loc());GUI.color=Color.white;y+=22f;
   }
   y+=6f;
   float chance=GearEnhance.Chance(l,op,bench);
   var bar=new Rect(inner.x,y,inner.width,16f);Widgets.FillableBar(bar,chance);y+=20f;
   GUI.color=new Color(.4f,.9f,.4f);Widgets.Label(new Rect(inner.x,y,inner.width,22f),"成功率 ".Loc()+chance.ToStringPercent());GUI.color=Color.white;y+=22f;
   string fail=l>GearEnhance.LowMax?"失败：掉回 +".Loc()+(l-1):"失败：只损失材料".Loc();GUI.color=l>GearEnhance.LowMax?new Color(.95f,.5f,.1f):Color.gray;Widgets.Label(new Rect(inner.x,y,inner.width,22f),fail);GUI.color=Color.white;y+=22f;
   string who=op!=null?"工匠：".Loc()+op.LabelShort+"（智识 ".Loc()+op.skills.GetSkill(SkillDefOf.Intellectual).Level+"，手工 ".Loc()+op.skills.GetSkill(SkillDefOf.Crafting).Level+"）".Loc():"工匠：无（要智识 ".Loc()+GearEnhanceBench.SkillNeed(rec)+"）".Loc();
   string extra=GearEnhance.HasFocusStone(bench)?"\n连着奥术聚能石 +".Loc()+GearEnhance.FocusBonus.ToStringPercent():"";
   GUI.color=Color.gray;float h=Text.CalcHeight(who+extra,inner.width);Widgets.Label(new Rect(inner.x,y,inner.width,h),who+extra);GUI.color=Color.white;y+=h+10f;
   string block=GearEnhanceBench.Blocked(selected,Map);
   var btn=new Rect(inner.x,y,inner.width,36f);
   if(block!=null){LPUI.Unavailable(btn,block);return;}
   if(Widgets.ButtonText(btn,"强化到 +".Loc()+(l+1))){
    var gear=selected;
    if(l>GearEnhance.LowMax)Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("强化到 +".Loc()+(l+1)+"，成功率 ".Loc()+chance.ToStringPercent()+"。失败会掉回 +".Loc()+(l-1)+"，确定？".Loc(),()=>Do(gear)));
    else Do(gear);
   }
  }
  // 1.118.0 修复耐久：右栏底部，和强化分开。
  void DrawRepair(Rect r){
   Widgets.DrawLineHorizontal(r.x,r.y,r.width);float y=r.y+6f;
   Text.Font=GameFont.Small;Widgets.Label(new Rect(r.x,y,r.width,22f),"修复耐久".Loc());y+=24f;
   if(!selected.def.useHitPoints || !GearEnhanceBench.NeedsRepair(selected)){GUI.color=Color.gray;Widgets.Label(new Rect(r.x,y,r.width,22f),selected.def.useHitPoints?"耐久是满的".Loc():"这件没有耐久".Loc());GUI.color=Color.white;return;}
   int cost=GearEnhanceBench.RepairCost(selected),have=GearEnhanceBench.CountOnMap(Map,ThingDefOf.Silver);
   Widgets.ThingIcon(new Rect(r.x+4f,y+1f,20f,20f),ThingDefOf.Silver);
   GUI.color=have>=cost?Color.white:new Color(.9f,.3f,.3f);Widgets.Label(new Rect(r.x+28f,y,r.width-28f,22f),"白银 ×".Loc()+cost+"（有 ".Loc()+have+"）".Loc());GUI.color=Color.white;y+=22f;
   GUI.color=Color.gray;Text.Font=GameFont.Tiny;Widgets.Label(new Rect(r.x,y,r.width,18f),"每缺 20% 耐久 ".Loc()+GearEnhanceBench.RepairSilverPer+" 白银，当场修满".Loc());Text.Font=GameFont.Small;GUI.color=Color.white;y+=20f;
   var btn=new Rect(r.x,y,r.width,30f);
   if(have<cost){GUI.color=Color.gray;Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(btn,"白银不够".Loc());Text.Anchor=TextAnchor.UpperLeft;GUI.color=Color.white;return;}
   if(Widgets.ButtonText(btn,"修复（".Loc()+cost+" 白银）".Loc())){
    if(GearEnhanceBench.Repair(selected,Map)){result="修复完成！耐久 ".Loc()+selected.HitPoints+" / "+selected.MaxHitPoints;resultColor=new Color(.4f,.9f,.4f);SoundDefOf.Quest_Concluded.PlayOneShotOnCamera();}
    else{result="没能修复".Loc();resultColor=Color.gray;SoundDefOf.ClickReject.PlayOneShotOnCamera();}
   }
  }
  void Do(Thing gear){
   int from=GearEnhance.Level(gear);var res=GearEnhanceBench.TryEnhance(gear,bench);int to=GearEnhance.Level(gear);
   switch(res){
    case GearEnhance.Result.Success:result="成功！现在 +".Loc()+to;resultColor=new Color(.4f,.9f,.4f);SoundDefOf.Quest_Concluded.PlayOneShotOnCamera();break;
    case GearEnhance.Result.Fail:result="失败，材料没了，还是 +".Loc()+to;resultColor=Color.gray;SoundDefOf.ClickReject.PlayOneShotOnCamera();break;
    case GearEnhance.Result.Drop:result="失败，掉回了 +".Loc()+to;resultColor=new Color(.95f,.5f,.1f);SoundDefOf.ClickReject.PlayOneShotOnCamera();break;
    default:result=GearEnhanceBench.Blocked(gear,Map)??"没能强化".Loc();resultColor=Color.gray;break;
   }
  }
 }
}
