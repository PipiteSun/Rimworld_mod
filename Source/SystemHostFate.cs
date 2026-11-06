using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.122.0 改命符（LP_FateRewriteCharm）：把一个特性换成「同一档位」的另一个特性（光谱特性按程度各算档位，可以选到具体程度）。
 // 档位 = TraitRarity.Tier（没有 TraitRarityExt 的算普通档）；诅咒只换诅咒、普通只换非诅咒；事迹不能换出也不能换入。
 // 入口：背包里有改命符的殖民者的小人按钮；地上的改命符自身的按钮。都不用浮动菜单。
 public static class FateRewrite {
  // 剧情授予、有存档数据或会和别的系统状态对不上的特性：不能换出，也不出现在右列。
  public static readonly HashSet<string> Story=new HashSet<string>{
   "LP_SystemHost",                                              // 系统宿主本身
   "LP_Prophesied","LP_PactDestiny","LP_WeaveChild","LP_DivineChampion", // 天命（HeroDestiny 记着天命进度 / 神选等级）
   "LP_DivineInfusion",                                          // 黄昏之年的神力灌注（Twilight 记着灌注的神）
   "LP_Devotee",                                                 // 信徒：程度 = 信仰的神（Faith 记着 devoteeDegree、祈祷与神恩）
  };
  public static int TierOf(TraitDef d,int degree){int t=TraitRarity.Tier(d,degree);return t<0?0:t;}
  public static string MarkOf(TraitDef d,int degree)=>TraitRarity.Mark(d,degree);
  public static string TierName(TraitDef d,int degree){var m=MarkOf(d,degree);string n=TraitRarity.Names[TierOf(d,degree)];return m=="Curse"?"诅咒 · "+n:m=="Deed"?"事迹 · "+n:n;}
  public static bool DefLocked(TraitDef d)=>d==null || Story.Contains(d.defName) || d.defName.StartsWith("Isekai_") || d.defName.StartsWith("IKM_");
  // 这个特性能不能被改掉：null = 可以。
  public static string Locked(Pawn p,Trait t){
   if(t?.def==null)return "没有这个特性";
   if(t.sourceGene!=null)return "由基因「"+t.sourceGene.LabelCap+"」赋予";
   if(t.ScenForced)return "由开局剧本指定";
   if(MarkOf(t.def,t.Degree)=="Deed")return "传说事迹不能改写";
   if(t.def==SystemHost.Def)return "系统宿主不能改写自己";
   if(t.def.defName.StartsWith("Isekai_") || t.def.defName.StartsWith("IKM_"))return "Isekai / 百战觉醒的特性由那边的系统管理";
   if(DefLocked(t.def))return "由剧情授予，记着剧情进度";
   return null;
  }
  static bool Disallowed(Pawn p,TraitDef d,int degree){
   var k=p.kindDef;
   if(k?.disallowedTraits!=null && k.disallowedTraits.Contains(d))return true;
   if(k?.disallowedTraitsWithDegree!=null && k.disallowedTraitsWithDegree.Any(r=>r.def==d && (r.degree==null || r.degree==degree)))return true;
   if(p.genes!=null)foreach(var g in p.genes.GenesListForReading){
    if(!g.Active || g.def.suppressedTraits==null)continue;
    if(g.def.suppressedTraits.Any(s=>s.def==d && s.degree==degree))return true;
   }
   if(d.requiredWorkTags!=WorkTags.None && (p.CombinedDisabledWorkTags & d.requiredWorkTags)!=WorkTags.None)return true;
   if(d.requiredWorkTypes!=null && d.requiredWorkTypes.Any(w=>p.WorkTypeIsDisabled(w)))return true;
   return false;
  }
  // 右列：同档位、标记一致、不冲突、种族允许、没拥有（光谱特性可以换成同一特性的另一程度）。
  public static List<KeyValuePair<TraitDef,int>> Candidates(Pawn p,Trait old){
   var list=new List<KeyValuePair<TraitDef,int>>();
   if(p?.story?.traits==null || Locked(p,old)!=null)return list;
   int tier=TierOf(old.def,old.Degree);bool curse=MarkOf(old.def,old.Degree)=="Curse";
   var others=p.story.traits.allTraits.Where(x=>x!=old).ToList();
   foreach(var d in DefDatabase<TraitDef>.AllDefsListForReading){
    if(DefLocked(d) || d.degreeDatas==null)continue;
    if(d!=old.def && others.Any(x=>x.def==d))continue;
    if(others.Any(x=>d.ConflictsWith(x) || x.def.ConflictsWith(d)))continue;
    foreach(var dd in d.degreeDatas){
     if(d==old.def && dd.degree==old.Degree)continue;
     if(dd.label.NullOrEmpty())continue;
     var m=MarkOf(d,dd.degree);if(m=="Deed" || (m=="Curse")!=curse)continue;
     if(TierOf(d,dd.degree)!=tier || Disallowed(p,d,dd.degree))continue;
     list.Add(new KeyValuePair<TraitDef,int>(d,dd.degree));
    }
   }
   return list.OrderBy(kv=>kv.Key.DataAtDegree(kv.Value).label.StripTags()).ToList();
  }
  public static bool Allowed(Pawn p,Trait old,TraitDef d,int degree)=>Candidates(p,old).Any(kv=>kv.Key==d && kv.Value==degree);
  public static string Label(TraitDef d,int degree)=>d.DataAtDegree(degree)?.GetLabelCapFor(null)??d.defName;

  // 改写：检查改命符还在、目标和特性都合法，然后换特性、消耗 1 个改命符、发信。
  public static bool Apply(Pawn p,Trait old,TraitDef d,int degree,Thing charm,out string error){
   error="改命符或特性已经变化，请重新打开窗口。";
   if(charm==null || charm.Destroyed || charm.def!=SystemHost.CharmDef || charm.stackCount<1)return false;
   if(p?.story?.traits==null || old==null || !p.story.traits.allTraits.Contains(old))return false;
   var why=Locked(p,old);if(why!=null){error="这个特性不能改写："+why;return false;}
   if(!Allowed(p,old,d,degree)){error="不能换成这个特性（档位不同、冲突或已拥有）。";return false;}
   string from=old.LabelCap.StripTags(),to=Label(d,degree).StripTags();
   p.story.traits.RemoveTrait(old);
   p.story.traits.GainTrait(new Trait(d,degree,false));
   charm.SplitOff(1).Destroy();
   Find.LetterStack.ReceiveLetter("改命："+p.LabelShort,p.LabelShortCap+"的命数被改写了。\n\n特性「"+from+"」→「"+to+"」（"+TierName(d,degree)+"）。\n\n改命符燃尽，化作一缕青烟。",LetterDefOf.PositiveEvent,p);
   error=null;return true;
  }

  public static Thing CharmIn(Pawn p)=>SystemHost.CharmDef==null?null:p?.inventory?.innerContainer?.FirstOrDefault(t=>t.def==SystemHost.CharmDef);
  public static int CharmCount(Pawn p)=>SystemHost.CharmDef==null || p?.inventory?.innerContainer==null?0:p.inventory.innerContainer.Where(t=>t.def==SystemHost.CharmDef).Sum(t=>t.stackCount);
  // 能被改命的人：持有者在前，同一地图（或同一远行队）上的其他自由殖民者。
  public static List<Pawn> Targets(Thing charm,Pawn holder){
   var list=new List<Pawn>();if(holder!=null && holder.story!=null)list.Add(holder);
   IEnumerable<Pawn> pool=null;
   var map=holder?.MapHeld??charm?.MapHeld;
   if(map!=null)pool=map.mapPawns.FreeColonists;
   else{var car=holder?.GetCaravan();if(car!=null)pool=car.PawnsListForReading.Where(x=>x.IsFreeColonist);}
   if(pool!=null)foreach(var q in pool)if(q!=holder && q.story?.traits!=null && !q.Dead)list.Add(q);
   return list;
  }
 }

 public class CompProperties_FateCharm : CompProperties {public CompProperties_FateCharm(){compClass=typeof(CompFateCharm);}}
 public class CompFateCharm : ThingComp {
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   foreach(var g in base.CompGetGizmosExtra())yield return g;
   if(!parent.Spawned || parent.MapHeld==null)yield break;
   yield return new Command_Action{
    defaultLabel="使用改命符",defaultDesc="选一名这张地图上的殖民者，把他的一个特性改写成同一档位的另一个特性。消耗 1 个改命符。",
    icon=parent.def.uiIcon,action=()=>Find.WindowStack.Add(new Dialog_FateRewrite(parent,null))
   };
  }
 }

 // 改命窗口：左 人物，中 该人的特性（不可改的灰显并说明原因），右 同档位的可选特性；确认后替换。
 public sealed class Dialog_FateRewrite : Window {
  readonly Thing charm;readonly Pawn holder;Pawn target;Trait picked;TraitDef newDef;int newDegree;
  Vector2 s0,s1,s2;
  public override Vector2 InitialSize=>new Vector2(940f,660f);
  public Dialog_FateRewrite(Thing charm,Pawn holder){
   this.charm=charm;this.holder=holder;forcePause=true;absorbInputAroundWindow=true;doCloseX=true;doCloseButton=true;closeOnClickedOutside=false;
   target=FateRewrite.Targets(charm,holder).FirstOrDefault();
  }
  public override void DoWindowContents(Rect rect){
   if(charm==null || charm.Destroyed){Close();return;}
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(0,0,rect.width-30,36),"改命");Text.Font=GameFont.Small;
   Widgets.Label(new Rect(0,40,rect.width,44),"把一个特性改写成同一档位的另一个特性（光谱特性按程度各算档位）。诅咒只能换成诅咒；传说事迹、基因赋予的特性和剧情特性不能改。消耗 1 个改命符（剩 "+charm.stackCount+" 个）。");
   float top=90,h=rect.height-top-110,w0=180,w1=(rect.width-w0-24)/2;
   var targets=FateRewrite.Targets(charm,holder);
   // 人物
   var r0=new Rect(0,top,w0,h);Widgets.DrawMenuSection(r0);
   var c0=new Rect(0,0,w0-20,Mathf.Max(h,targets.Count*30+6));
   Widgets.BeginScrollView(r0.ContractedBy(4),ref s0,c0);
   float y=0;foreach(var q in targets){var b=new Rect(0,y,c0.width,28);if(q==target)Widgets.DrawHighlightSelected(b);if(Widgets.ButtonText(b,q.LabelShortCap,false)){target=q;picked=null;newDef=null;}y+=30;}
   Widgets.EndScrollView();
   // 特性
   var r1=new Rect(w0+12,top,w1,h);Widgets.DrawMenuSection(r1);
   var traits=target?.story?.traits?.allTraits??new List<Trait>();
   var c1=new Rect(0,0,w1-20,Mathf.Max(h,traits.Count*52+6));
   Widgets.BeginScrollView(r1.ContractedBy(4),ref s1,c1);
   y=0;
   foreach(var t in traits){
    var b=new Rect(0,y,c1.width,50);var why=FateRewrite.Locked(target,t);
    if(t==picked)Widgets.DrawHighlightSelected(b);else if(Mouse.IsOver(b))Widgets.DrawHighlight(b);
    var old=GUI.color;if(why!=null)GUI.color=new Color(1,1,1,.45f);
    Widgets.Label(new Rect(b.x+6,b.y+2,b.width-12,24),t.LabelCap);
    Widgets.Label(new Rect(b.x+6,b.y+24,b.width-12,24),"<color=#B0B0B0>"+(why??FateRewrite.TierName(t.def,t.Degree))+"</color>");
    GUI.color=old;
    TooltipHandler.TipRegion(b,()=>t.TipString(target),t.GetHashCode());
    if(why==null && Widgets.ButtonInvisible(b)){picked=t;newDef=null;}
    y+=52;
   }
   Widgets.EndScrollView();
   // 可选
   var r2=new Rect(w0+w1+24,top,w1,h);Widgets.DrawMenuSection(r2);
   var cands=picked!=null?FateRewrite.Candidates(target,picked):new List<KeyValuePair<TraitDef,int>>();
   var c2=new Rect(0,0,w1-20,Mathf.Max(h,cands.Count*30+30));
   Widgets.BeginScrollView(r2.ContractedBy(4),ref s2,c2);
   y=0;
   if(picked==null)Widgets.Label(new Rect(4,0,c2.width-8,60),"先在中间选一个要改写的特性。");
   else if(cands.Count==0)Widgets.Label(new Rect(4,0,c2.width-8,60),"没有同一档位、可以换成的特性。");
   foreach(var kv in cands){
    var b=new Rect(0,y,c2.width,28);bool on=kv.Key==newDef && kv.Value==newDegree;
    if(on)Widgets.DrawHighlightSelected(b);else if(Mouse.IsOver(b))Widgets.DrawHighlight(b);
    Widgets.Label(new Rect(b.x+6,b.y+3,b.width-12,24),FateRewrite.Label(kv.Key,kv.Value));
    var d=kv.Key;int g=kv.Value;
    TooltipHandler.TipRegion(b,()=>(TraitRarity.Header(d,g)??"")+"\n"+d.DataAtDegree(g).description.Formatted(target.Named("PAWN")).AdjustedFor(target).Resolve(),d.index*64+g+32);
    if(Widgets.ButtonInvisible(b)){newDef=kv.Key;newDegree=kv.Value;}
    y+=30;
   }
   Widgets.EndScrollView();
   // 确认
   string sum=picked==null?"未选择特性":newDef==null?"「"+picked.LabelCap.StripTags()+"」→ ？":"「"+picked.LabelCap.StripTags()+"」→「"+FateRewrite.Label(newDef,newDegree).StripTags()+"」（"+FateRewrite.TierName(newDef,newDegree)+"）";
   Widgets.Label(new Rect(0,rect.height-100,rect.width-200,28),(target!=null?target.LabelShortCap+"：":"")+sum);
   bool was=GUI.enabled;GUI.enabled=was && target!=null && picked!=null && newDef!=null;
   if(Widgets.ButtonText(new Rect(rect.width-190,rect.height-104,190,32),"确认改命")){
    var p=target;var t=picked;var d=newDef;int g=newDegree;
    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(p.LabelShortCap+"："+sum+"？\n\n旧特性会被移除，消耗 1 个改命符。",()=>{
     if(FateRewrite.Apply(p,t,d,g,charm,out var err)){picked=null;newDef=null;if(charm.Destroyed)Close();}
     else Messages.Message(err,MessageTypeDefOf.RejectInput,false);
    }));
   }
   GUI.enabled=was;
  }
 }
}
