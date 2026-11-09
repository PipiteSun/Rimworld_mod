using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.132.0 职业窗口「法术与能力一览」：按职业等级列出每级获得的能力、子职业、法术环位（1.128–1.131 法术书 / 法术表）、
 // 秘法玄奥 / 秘法奥秘、职业水晶，再列传奇职业能力和秘传槽（按冒险者等级）。只读现有数据，不改任何数值和逻辑。
 public class OverviewRow {
  public int level;          // 需要的职业等级（group=1 时是冒险者等级）
  public int group;          // 0 = 职业等级；1 = 冒险者等级（秘传槽）；2 = 传奇职业点
  public string label,tag,tip;
  public Texture icon;
  public bool have,pending;  // 已获得；到了等级但还没选
 }
 public static class ClassOverview {
  public const float RowH=30f,GroupH=26f,GroupGap=4f;

  static string AbTip(AbilityDef a)=>a==null?"":a.LabelCap+(a.description.NullOrEmpty()?"":"\n\n"+a.description);
  static bool Knows(Pawn p,AbilityDef a)=>a!=null && p?.abilities?.GetAbility(a)!=null;

  public static List<OverviewRow> Build(Hediff_Adventurer h,LP_ClassDef c){
   var rows=new List<OverviewRow>();if(c==null)return rows;
   var p=h?.pawn;int lv=ClassSystem.Level(h,c);var used=new HashSet<string>();
   // 1. 职业能力
   foreach(var e in c.abilities){
    if(e.ability==null)continue;string lab=e.ability.label+(e.capstoneSkill!=null?"（另要"+e.capstoneSkill.label+" "+ClassSystem.CapstoneSkill+" 级）":"");used.Add(lab);
    rows.Add(new OverviewRow{level=e.level,tag="职业能力",label=e.ability.LabelCap+(e.capstoneSkill!=null?"（另要"+e.capstoneSkill.label+" "+ClassSystem.CapstoneSkill+" 级）":""),tip=AbTip(e.ability),icon=e.ability.uiIcon,have=Knows(p,e.ability) || lv>=e.level && e.capstoneSkill==null});
   }
   // 2. 子职业
   var opts=ClassSubclass.Options(c).ToList();var picked=ClassSubclass.Picked(h,c);int pl=ClassSubclass.PickLevelFor(c);
   if(opts.Count>0){
    used.Add("选择子职业");if(picked!=null)used.Add("子职业「"+picked.label+"」");
    rows.Add(new OverviewRow{level=pl,tag="子职业",label=picked==null?"选择子职业（"+string.Join("、",opts.Select(s=>s.label))+"）":"子职业「"+picked.label+"」",icon=(picked??opts[0]).Emblem,have=picked!=null,pending=picked==null && lv>=pl,
     tip=string.Join("\n\n",opts.Select(s=>s.LabelCap+(s==picked?"（已选）":"")+"\n"+s.description))});
    if(picked!=null){
     foreach(var e in picked.abilities.Where(x=>x.ability!=null)){used.Add(e.ability.label);rows.Add(new OverviewRow{level=e.level,tag=picked.label,label=e.ability.LabelCap,tip=AbTip(e.ability),icon=e.ability.uiIcon,have=Knows(p,e.ability) || lv>=e.level});}
     foreach(var ch in picked.choices){used.Add("选择"+ch.label);string cur=ClassSubclass.ChoiceLabel(h,ch);
      rows.Add(new OverviewRow{level=ch.level,tag=picked.label,label=cur!=null?ch.label+"：「"+cur+"」":"选择"+ch.label+"（"+string.Join("、",ch.options)+"）",have=cur!=null,pending=cur==null && lv>=ch.level,icon=picked.Emblem,tip=ch.label+"\n\n"+string.Join("\n",ch.options.Select((o,i)=>"· "+o+(i<ch.tips.Count && !ch.tips[i].NullOrEmpty()?"："+ch.tips[i]:"")))});}
    }else{
     foreach(int l in opts.SelectMany(s=>s.abilities.Select(e=>e.level)).Distinct().OrderBy(x=>x))
      rows.Add(new OverviewRow{level=l,tag="子职业",label="子职业能力（按所选子职业）",icon=opts[0].Emblem,have=false,
       tip=string.Join("\n",opts.Select(s=>s.label+"："+string.Join("、",s.abilities.Where(e=>e.level==l && e.ability!=null).Select(e=>e.ability.label))))});
    }
   }
   var gen=ClassSubclass.General(c);
   if(gen!=null)foreach(var e in gen.abilities.Where(x=>x.ability!=null)){used.Add(e.ability.label);rows.Add(new OverviewRow{level=e.level,tag="职业特性",label=e.ability.LabelCap,tip=AbTip(e.ability),icon=e.ability.uiIcon,have=Knows(p,e.ability) || lv>=e.level});}
   // 3. 其他等级特性（额外攻击、奖励专长、魔能爆光束、宿敌……）：沿用职业名悬停里的等级表
   for(int l=1;l<=ClassSystem.CapLevel;l++)foreach(var g in ClassLevelInfo.GainsAt(h,c,l))
    if(!used.Contains(g) && !g.StartsWith("子职业「"))rows.Add(new OverviewRow{level=l,tag="特性",label=g,have=lv>=l,icon=c.Background});
   // 4. 法术环位
   Spells(rows,h,c,lv);
   // 5. 职业水晶（魔力湖泊秘传，职业等级 11 / 13 / 15 / 17）
   foreach(var a in LakeArcana.OfClass(c)){var e=LakeArcana.Ext(a);bool have=LakeArcana.Knows(h,a);
    rows.Add(new OverviewRow{level=LakeArcana.ClassLevelFor(e.ring),tag="职业水晶",label=a.LabelCap+"（"+e.ring+" 环）",icon=a.uiIcon,have=have,
     tip=AbTip(a)+"\n\n"+(have?"已掌握":"需要「"+a.label+"」秘传结晶（魔力湖泊出售），"+c.label+" "+LakeArcana.ClassLevelFor(e.ring)+" 级，并占 1 个秘传槽。")});}
   // 6. 传奇职业能力（传奇职业点）
   foreach(var a in c.legendary.Where(x=>x!=null))
    rows.Add(new OverviewRow{group=2,level=0,tag="传奇",label=a.LabelCap,icon=a.uiIcon,have=ClassLegendary.Knows(h,a),tip=AbTip(a)+"\n\n冒险者 "+string.Join(" / ",ClassLegendary.Thresholds)+" 级各得 1 个传奇职业点；第一个要"+c.label+" "+ClassLegendary.FirstLevel+" 级，第二个 "+ClassLegendary.SecondLevel+" 级。"});
   // 7. 秘传槽（冒险者等级，所有职业共用）
   for(int i=0;i<LakeArcana.SlotLevels.Length;i++){int al=LakeArcana.SlotLevels[i];
    rows.Add(new OverviewRow{group=1,level=al,tag="秘传槽",label="秘传槽 第 "+(i+1)+" 个"+(al>=30?"（传奇）":""),have=h!=null && h.level>=al,tip=LakeArcana.SlotTip,icon=LakeArcana.All.FirstOrDefault()?.uiIcon});}
   return rows.OrderBy(r=>r.group).ThenBy(r=>r.level).ToList();
  }

  static void Spells(List<OverviewRow> rows,Hediff_Adventurer h,LP_ClassDef c,int lv){
   var p=h?.pawn;
   if(c==Spellbook.Wizard && c!=null){
    var book=Spellbook.Bookable.Concat(Spellbook.CantripList).ToList();
    for(int k=0;k<=9;k++){
     var list=book.Where(d=>Spellbook.Circle(d)==k).OrderBy(d=>d.label).ToList();if(list.Count==0)continue;int need=k==0?1:Spellbook.LevelFor(k);
     rows.Add(SpellRow(p,need,lv,k,list,"法术书",k==0?"每级可学的戏法：1 级 3 个，4 / 10 级各 +1。":"写进法术书（每级 "+Spellbook.PicksPerLevel+" 个，也可抄录卷轴），每天备法（法师等级 + 智力调整值）。"));
    }
    return;
   }
   var l=CasterSpells.ListOf(c);if(l==null)return;
   for(int k=0;k<=9;k++){
    if(k==0 && !l.cantrips)continue;
    var list=l.Ordered.Where(d=>CasterSpells.CircleIn(l,d)==k).ToList();if(list.Count==0)continue;
    int need=CasterSpells.LevelFor(l,k);if(need>ClassSystem.CapLevel)continue;
    if(l.Pact && k>5){
     var b=CasterSpells.Book(h,l,false);var pick=CasterSpells.ArcanumAt(b,l,k);
     rows.Add(new OverviewRow{level=need,tag="秘法玄奥",label="秘法玄奥·"+Spellbook.CircleName(k)+(pick!=null?"：「"+pick.label+"」":"（选 1 个，每天 1 次）"),icon=(pick??list[0]).uiIcon,have=pick!=null,pending=pick==null && lv>=need,
      tip="邪术师 "+need+" 级从这些"+Spellbook.CircleName(k)+"法术里选 1 个，每天施放 1 次、不耗契约法力：\n"+string.Join("、",list.Select(d=>d.label))});
     continue;
    }
    string how=l.prepared?"备法职业：自动知道表里能施放环位的全部法术，每天准备一部分。":"已知职业：已知上限 = 职业等级 + 1，每升一级可替换 1 个。";
    rows.Add(SpellRow(p,need,lv,k,list,"法术表",how));
   }
   foreach(int sl in l.secretLevels){
    var b=CasterSpells.Book(h,l,false);
    rows.Add(new OverviewRow{level=sl,tag="秘法奥秘",label="秘法奥秘（从任意职业的法术表选 "+l.secretsPer+" 个）",icon=l.Icon,have=lv>=sl,pending=lv>=sl && CasterSpells.SecretsDue(h,l)>0,
     tip="吟游诗人 "+string.Join(" / ",l.secretLevels)+" 级各从任意职业的法术表（含法师法术书）选 "+l.secretsPer+" 个，按自己能施放的环位；算吟游诗人法术、扣乐韵。"+(b!=null && b.secrets.Count>0?"\n已选："+string.Join("、",b.secrets.Select(d=>d.label)):"")});
   }
  }
  static OverviewRow SpellRow(Pawn p,int need,int lv,int k,List<AbilityDef> list,string tag,string how){
   int known=list.Count(d=>Knows(p,d));
   return new OverviewRow{level=need,tag=tag,label=Spellbook.CircleName(k)+(k==0?"":"法术")+"（"+list.Count+" 个"+(known>0?"，已会 "+known:"")+"）",icon=list[0].uiIcon,have=lv>=need,
    tip=Spellbook.CircleName(k)+(k==0?"":"法术")+"："+need+" 级起可用。"+how+"\n\n"+string.Join("、",list.Select(d=>(Knows(p,d)?"✓":"")+d.label))};
  }

  // 一览的总高度（不用 GUI 量字，测试里也能算）。
  public static float Height(List<OverviewRow> rows){
   if(rows==null || rows.Count==0)return RowH;
   int groups=rows.Select(r=>r.group*1000+r.level).Distinct().Count();
   return groups*(GroupH+GroupGap)+rows.Count*RowH;
  }
  static string GroupLabel(OverviewRow r,Hediff_Adventurer h,int lv){
   if(r.group==2)return "传奇职业能力（传奇职业点 "+ClassLegendary.Points(h)+"）";
   if(r.group==1)return "冒险者 "+r.level+" 级"+(h!=null && h.level>=r.level?"":"（现在 "+(h?.level??0)+" 级）");
   return "Lv "+r.level+(lv>=r.level?"":"（还差 "+(r.level-lv)+" 级）");
  }
  // 画在 (x, y) 起、宽 w 的区域（滚动区里），返回画完后的 y。
  public static float Draw(float x,float y,float w,List<OverviewRow> rows,Hediff_Adventurer h,LP_ClassDef c){
   int lv=ClassSystem.Level(h,c);int last=int.MinValue;
   if(rows==null || rows.Count==0){LPUI.Unavailable(new Rect(x,y,w,RowH),"这个职业没有可列出的能力");return y+RowH;}
   foreach(var r in rows){
    int key=r.group*1000+r.level;
    if(key!=last){
     last=key;bool reached=r.group==2?true:r.group==1?h!=null && h.level>=r.level:lv>=r.level;
     y=LPUI.Bar(new Rect(x,y,w,GroupH),GroupLabel(r,h,lv),reached?LPUI.Gold:LPUI.Locked)-LPUI.Gap+GroupGap+(GroupH-LPUI.RowH);
    }
    var row=new Rect(x,y,w,RowH-2f);if(r.have)Widgets.DrawBoxSolid(row,new Color(.35f,.6f,.35f,.12f));Widgets.DrawHighlightIfMouseover(row);
    if(r.icon!=null)LPUI.Icon(new Rect(row.x+4f,row.y+2f,24f,24f),r.icon,!r.have && !r.pending);
    float stW=Mathf.Min(110f,w*.22f),tagW=Mathf.Min(90f,w*.18f);
    var c0=GUI.color;GUI.color=r.have?Color.white:r.pending?LPUI.Gold:LPUI.Locked;
    LPUI.LabelFit(new Rect(row.x+32f,row.y+3f,row.width-32f-stW-tagW-8f,RowH-4f),r.label);GUI.color=c0;
    LPUI.Tiny(new Rect(row.xMax-stW-tagW-4f,row.y+7f,tagW,RowH-6f),r.tag,true);
    string st=r.have?"已获得":r.pending?"待选择":r.group==2?"需传奇职业点":r.group==1?"需冒险者 "+r.level+" 级":"需要 Lv "+r.level;
    var a=Text.Anchor;Text.Anchor=TextAnchor.MiddleRight;GUI.color=r.have?LPUI.Have:r.pending?LPUI.Gold:LPUI.Locked;
    LPUI.LabelFit(new Rect(row.xMax-stW-4f,row.y,stW,RowH-2f),st);GUI.color=c0;Text.Anchor=a;
    if(!r.tip.NullOrEmpty() && Mouse.IsOver(row))TooltipHandler.TipRegion(row,r.tip);
    y+=RowH;
   }
   return y;
  }
 }
}
