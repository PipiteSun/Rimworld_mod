using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.136.0 便利三：「DND 角色卡」检视分页——六维与调整值、冒险者 / 传奇等级、职业与子职业、专长与恩赐、调谐位、信仰、秘传槽与湖印、各强度的分项来源。
 // 全部复用现有计算（Eff / Mod、ClassSystem、ClassLevelInfo.Stats + StatWorker 的说明、LakeArcana、LakeSeals、DndImplants、Faith），样式照 1.124.0 LPUI。
 [StaticConstructorOnStartup]
 public static class DndCardStartup {
  static DndCardStartup(){
   foreach(var d in DefDatabase<ThingDef>.AllDefsListForReading){
    if(d.race==null || !d.race.Humanlike)continue;
    if(d.inspectorTabs==null)d.inspectorTabs=new List<Type>();
    if(d.inspectorTabs.Contains(typeof(ITab_DndCard)))continue;
    d.inspectorTabs.Add(typeof(ITab_DndCard));
    d.inspectorTabsResolved?.Add(InspectTabManager.GetSharedInstance(typeof(ITab_DndCard)));
   }
  }
 }
 public static class DndCard {
  public class Line{public string label,value,tip;public Color color=Color.white;}
  public class Section{public string title;public List<Line> lines=new List<Line>();public Section(string t){title=t;}public Line Add(string l,string v,string tip=null){var x=new Line{label=l,value=v,tip=tip};lines.Add(x);return x;}}
  public static List<Section> Build(Pawn p){
   var list=new List<Section>();var h=Adventurer.Get(p);if(h==null)return list;
   // 等级
   var s=new Section("冒险者");list.Add(s);
   s.Add("冒险者等级",h.level>Adventurer.MaxLevel?Adventurer.MaxLevel+"（传奇等级 "+h.level+"）":h.level.ToString(),"经验 "+h.xp.ToString("0")+(h.legendary?"\n传奇："+Legend.Summary(h):""));
   s.Add("熟练加值","+"+Legendary.Proficiency(h.level));
   // 六维
   var a=new Section("六维属性");list.Add(a);
   for(int i=0;i<6;i++){int e=h.Eff(i),m=Adventurer.Mod(e);a.Add(Adventurer.Names[i],e+"（调整值 "+(m>=0?"+":"")+m+"）","基础 "+h.scores[i]+"，各种加成后 "+e+"；效果调整值（含种族亲和）"+h.AMod(i).ToString("+0.##;-0.##"));}
   // 职业
   var c=new Section("职业");list.Add(c);
   var taken=ClassSystem.Taken(h).ToList();
   if(taken.Count==0)c.Add("职业","还没有学职业");
   foreach(var k in taken){
    var sub=ClassSubclass.Picked(h,k);var ch=sub==null?"":string.Join("、",sub.choices.Select(x=>ClassSubclass.ChoiceLabel(h,x)).Where(x=>x!=null));
    var l=c.Add(k.LabelCap,ClassSystem.Level(h,k)+" 级"+(sub!=null?" · "+sub.label+(ch.Length>0?"（"+ch+"）":""):""),k.description);l.color=k.color;
   }
   if(ClassLegendary.Points(h)>0 || h.classes?.legendary.Count>0)c.Add("传奇职业能力",string.Join("、",h.classes.legendary.Select(x=>x.label))+(ClassLegendary.Points(h)>0?"（未用点数 "+ClassLegendary.Points(h)+"）":""));
   // 专长与恩赐
   var f=new Section("专长与恩赐");list.Add(f);
   f.Add("专长",h.feats.Count==0?"无":string.Join("、",h.feats.Select(x=>x.label)),string.Join("\n",h.feats.Select(x=>x.LabelCap+"："+x.description)));
   if(h.originFeat!=null)f.Add("起源专长",h.originFeat.label,h.originFeat.description);
   if(h.boons.Count>0 || h.boonPoints>0)f.Add("史诗恩赐",string.Join("、",h.boons.Select(x=>x.label))+(h.boonPoints>0?"（未用 "+h.boonPoints+"）":""));
   // 装备与信仰
   var g=new Section("调谐与信仰");list.Add(g);
   g.Add("调谐位",DndImplants.Attuned(p)+" / "+DndImplants.AttuneMax(p),"稀有档和神器档植入物占调谐位。");
   var god=FaithUtility.DevoteeGod(p);g.Add("信仰",god!=null?"虔信 "+god.label:p.Ideo!=null?p.Ideo.name:"无");
   // 秘传与湖印
   var m2=new Section("秘传与湖印");list.Add(m2);
   m2.Add("秘传槽",LakeArcana.Used(h)+" / "+LakeArcana.Slots(h)+(h.classes?.arcana.Count>0?"："+string.Join("、",h.classes.arcana.Select(x=>x.label)):""),LakeArcana.SlotTip);
   var lake=h.classes?.lake;
   m2.Add("湖印","法术 "+LakeSeals.Used(h,false)+" / "+LakeSeals.SpellSlots(h)+"，被动 "+LakeSeals.Used(h,true)+" / "+LakeSeals.PassiveSlots(h)+(lake!=null && lake.seals.Count>0?"："+string.Join("、",lake.seals.Select(x=>x.label)):""),LakeSeals.SlotTip);
   if(lake!=null && lake.seals.Any(x=>!LakeSeals.Passive(x)))m2.Add("湖息",LakeSeals.Breath(h).ToString("0.#")+" / "+LakeSeals.BreathMax(h).ToString("0"));
   // 强度
   var w=new Section("职业强度（悬停看分项来源）");list.Add(w);
   var stats=taken.SelectMany(ClassLevelInfo.Stats).Distinct().ToList();
   if(stats.Count==0)w.Add("强度","没有职业强度");
   foreach(var st in stats){float v=p.GetStatValue(st);string expl;try{expl=st.Worker.GetExplanationFull(StatRequest.For(p),st.toStringNumberSense,v);}catch(Exception e){expl=e.Message;}w.Add(st.LabelCap,"×"+v.ToString("0.00"),expl);}
   foreach(var k in taken.Where(x=>x.resource!=ClassResource.None))w.Add(ClassSystem.ResourceLabel(k.resource)+"（"+k.label+"）",ClassSystem.Resource(h,k).ToString("0.#")+" / "+ClassSystem.MaxResource(h,k),ClassLevelInfo.ResourceTip(h,k));
   return list;
  }
  public static float Height(List<Section> list)=>list.Sum(s=>LPUI.RowH+LPUI.Gap+s.lines.Count*LPUI.RowH+LPUI.SectionGap);
 }
 public class ITab_DndCard : ITab {
  Vector2 scroll;List<DndCard.Section> cache;Pawn cacheFor;int cacheFrame=-999;
  public ITab_DndCard(){size=new Vector2(560f,ITab_Adventurer.HeightFor(UI.screenHeight));labelKey="LP_DndCardTab";}
  protected override void UpdateSize(){base.UpdateSize();size=new Vector2(560f,ITab_Adventurer.HeightFor(UI.screenHeight));}
  Pawn Shown=>SelPawn??(SelThing as Corpse)?.InnerPawn;
  public override bool IsVisible=>Adventurer.Enabled && Adventurer.Get(Shown)!=null;
  protected override void FillTab(){
   try{
    var p=Shown;if(p==null)return;
    if(cache==null || cacheFor!=p || Time.frameCount-cacheFrame>30){cache=DndCard.Build(p);cacheFor=p;cacheFrame=Time.frameCount;}
    var r=new Rect(0f,0f,size.x,size.y).ContractedBy(10f);
    float y=LPUI.Title(r,p.LabelShortCap+" · DND 角色卡",false);
    var outer=new Rect(r.x,y,r.width,r.yMax-y);var view=new Rect(0f,0f,outer.width-LPUI.ScrollBarW,Mathf.Max(DndCard.Height(cache),outer.height-1f));
    Widgets.BeginScrollView(outer,ref scroll,view);
    try{
     float yy=0f;float lw=view.width*.36f;
     foreach(var s in cache){
      yy=LPUI.Bar(new Rect(0f,yy,view.width,LPUI.RowH),s.title,LPUI.Gold);
      foreach(var l in s.lines){
       var row=new Rect(0f,yy,view.width,LPUI.RowH);Widgets.DrawHighlightIfMouseover(row);
       GUI.color=l.color;LPUI.LabelFit(new Rect(6f,yy+1f,lw-6f,LPUI.RowH-2f),l.label);GUI.color=Color.white;
       LPUI.LabelFit(new Rect(lw,yy+1f,view.width-lw,LPUI.RowH-2f),l.value);
       if(!l.tip.NullOrEmpty() && Mouse.IsOver(row))TooltipHandler.TipRegion(row,l.tip);
       yy+=LPUI.RowH;
      }
      yy+=LPUI.SectionGap;
     }
    }finally{Widgets.EndScrollView();}
   }finally{LPUI.Reset();}
  }
 }

 // ---------- 设置：殖民者头像资源条 ----------
 public partial class Settings {
  public bool resourceBar=true,anyTarget=true; // 1.136.0 热修：技能可对任何人释放
  void ExposeConvenience(){Scribe_Values.Look(ref resourceBar,"lpResourceBar",true);Scribe_Values.Look(ref anyTarget,"lpAnyTarget",true);}
 }
 public static class ConvenienceSettings {
  public static void Draw(Listing_Standard l){var s=LichMod.Settings;bool any=s.anyTarget;l.CheckboxLabeled("技能可对任何人释放",ref s.anyTarget,"1.136.0：DND 职业能力、法术、秘传、湖印可以对任何小人施放（殖民者、访客、中立、囚犯、奴隶、动物、机械体）：伤害 / 控制类可以打自己人，治疗 / 增益可以给敌人。范围效果里其他人照旧按敌我分；纯自身、地面、尸体、建筑类目标不变。NPC 和自动施放仍按原来的规则挑目标。");if(any!=s.anyTarget)AnyTarget.Apply();l.CheckboxLabeled("殖民者头像资源条",ref s.resourceBar,"1.136.0：殖民者头像下面画一条细资源条，显示主资源（等级最高、有资源的职业：法力 / 斗志 / 神恩 / 气 / 乐韵 / 契约法力 / 怒气；没有就显示法术能量或湖息）。");}
 }
}
