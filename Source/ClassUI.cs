using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.72.0 冒险者分页的“职业”页：职业点、每个职业的等级与资源条、能力树（每行 4 个，背景用原来 VPE 路径的塔罗牌图，按职业等级分行）。
 public static class ClassPage {
  const float Icon=40f,Gap=10f;
  static string armed;static Pawn armedFor;
  static Texture2D bar;static Texture2D Bar(Color c)=>bar??(bar=SolidColorMaterials.NewSolidColorTexture(new Color(.85f,.6f,.3f)));
  public static float Draw(float w,Pawn p,Hediff_Adventurer h,bool own){
   if(p!=armedFor){armed=null;armedFor=p;}
   if(h.classes==null)h.classes=new ClassData();
   float y=0f;int pts=ClassSystem.Points(h);
   {var r=new Rect(0f,y,w-130f,26f);if(pts<=0)GUI.color=Color.gray;
    Widgets.Label(new Rect(r.x,r.y+3f,r.width,24f),"职业点：可用 "+pts+"　已用 "+ClassSystem.Total(h)+" / "+ClassSystem.Budget(h)+(ClassLegendary.Points(h)>0?"　传奇职业点 "+ClassLegendary.Points(h):"")+(h.classes.freeUnlocks>0?"　（冒险者手册：下一个职业跳过门槛）":""));GUI.color=Color.white;
    TooltipHandler.TipRegion(r,"每个冒险者等级 1 点职业点（传奇等级 21～40 不给），职业等级总和不超过冒险者等级（最多 20）。\n第一个职业：主或副技能 "+ClassSystem.FirstSkill+" 级（或主属性 "+ClassSystem.ScoreGate+"）。\n兼修（最多 "+ClassSystem.MaxClasses+" 个）：冒险者 "+ClassSystem.MultiAdventurerLevel+" 级，且那门技能 "+ClassSystem.MultiSkill+" 级（或主属性 "+ClassSystem.ScoreGate+"）。\n能力在职业等级 "+string.Join(" / ",ClassSystem.AbilityLevels)+" 自动获得（邪术师的秘法奥秘在 11 / 13）；"+ClassSystem.AbilityLevels[ClassSystem.AbilityLevels.Length-1]+" 级的终极另要那门技能 "+ClassSystem.CapstoneSkill+" 级。");
    if(own && pts>0 && Widgets.ButtonText(new Rect(w-124f,y,124f,26f),"选择职业"))Find.WindowStack.Add(new Dialog_ChooseClass(p));}
   y+=30f;
   foreach(var c in ClassSystem.All)y=DrawClass(y,w,p,h,c,own,pts);
   return y;
  }
  static float DrawClass(float y,float w,Pawn p,Hediff_Adventurer h,LP_ClassDef c,bool own,int pts){
   int lv=ClassSystem.Level(h,c);string why=ClassSystem.CanAdd(h,c);
   var head=new Rect(0f,y,w,28f);Widgets.DrawLightHighlight(head);
   GUI.color=lv>0?c.color:Color.gray;Text.Font=GameFont.Medium;Widgets.Label(new Rect(4f,y,200f,30f),c.LabelCap+(lv>0?" Lv "+lv:""));Text.Font=GameFont.Small;GUI.color=Color.white;
   {var nr=new Rect(0f,y,200f,28f);if(Mouse.IsOver(nr))TooltipHandler.TipRegion(nr,ClassLevelInfo.ClassTip(p,h,c));} // 1.89.0 每级表与下一级获得（原 1.81.0 热修2 / 热修3 的成长说明并入）
   if(lv>0){
    float cur=ClassSystem.Resource(h,c);int max=ClassSystem.MaxResource(h,c);
    var br=new Rect(200f,y+6f,180f,18f);Widgets.FillableBar(br,max<=0?0f:cur/max,Bar(c.color));
    Text.Anchor=TextAnchor.MiddleCenter;Text.Font=GameFont.Tiny;Widgets.Label(br,ClassSystem.ResourceLabel(c.resource)+" "+cur.ToString("0.#")+" / "+max);Text.Anchor=TextAnchor.UpperLeft;Text.Font=GameFont.Small;
    Widgets.DrawHighlightIfMouseover(br);if(Mouse.IsOver(br))TooltipHandler.TipRegion(br,ClassLevelInfo.ResourceTip(h,c)); // 1.89.0 每种资源的真实上限公式
   }else{GUI.color=Color.gray;Text.Font=GameFont.Tiny;Widgets.Label(new Rect(200f,y+7f,w-320f,20f),(why??"可以学习").Truncate(w-320f));Text.Font=GameFont.Small;GUI.color=Color.white;if(why!=null)TooltipHandler.TipRegion(new Rect(200f,y,w-320f,28f),why);}
   if(own && pts>0){
    var b=new Rect(w-110f,y+1f,106f,26f);bool ok=why==null;string key=c.defName;
    if(Widgets.ButtonText(b,armed==key?"确定？":lv>0?"+1 级":"学习",active:ok) && ok){if(armed==key){armed=null;ClassLevelInfo.AddLevel(h,c);}else armed=key;}
    TooltipHandler.TipRegion(b,ok?(armed==key?"再点一次确认":"点两次确认："+c.label+" "+(lv+1)+" 级"):why);
   }
   y+=30f;
   // 1.89.0 直显：部位生命与职业强度，现在 → 下一级。
   {string line=ClassLevelInfo.RowLine(p,h,c);if(line!=null){Text.Font=GameFont.Tiny;var lr=new Rect(4f,y,w-8f,Mathf.Max(18f,Text.CalcHeight(line,w-8f)));GUI.color=lv>0?new Color(.85f,.85f,.85f):Color.gray;Widgets.Label(lr,line);GUI.color=Color.white;Text.Font=GameFont.Small;if(Mouse.IsOver(lr))TooltipHandler.TipRegion(lr,"“下一级”是在这个职业再加 1 级之后的值（悬停职业名看每级表）。");y+=lr.height+2f;}else y+=2f;}
   // 能力树：按获得等级分行，每行最多 4 个。
   var rows=new List<List<ClassAbilityEntry>>();
   foreach(var g in c.abilities.GroupBy(e=>e.level).OrderBy(g=>g.Key)){var l=g.ToList();for(int i=0;i<l.Count;i+=4)rows.Add(l.Skip(i).Take(4).ToList());}
   float rowH=Icon+18f,treeH=rows.Count*rowH+8f;
   var tree=new Rect(0f,y,w,treeH);
   var bg=c.Background;
   if(bg!=null){GUI.color=new Color(1f,1f,1f,lv>0?.35f:.15f);GUI.DrawTexture(tree,bg,ScaleMode.ScaleAndCrop);GUI.color=Color.white;}
   Widgets.DrawBox(tree);
   float ry=y+4f;
   foreach(var row in rows){
    int need=row[0].level;
    GUI.color=lv>=need?Color.white:Color.gray;Text.Font=GameFont.Tiny;Widgets.Label(new Rect(6f,ry+Icon/2f-8f,60f,18f),"Lv "+need);Text.Font=GameFont.Small;GUI.color=Color.white;
    float total=row.Count*Icon+(row.Count-1)*Gap*4f,x=(w-total)/2f;
    foreach(var e in row){
     var ir=new Rect(x,ry,Icon,Icon);x+=Icon+Gap*4f;
     bool have=p.abilities?.GetAbility(e.ability)!=null;string miss=ClassSystem.Missing(h,c,e);
     Widgets.DrawBoxSolid(ir,new Color(0f,0f,0f,.45f));
     GUI.color=have?Color.white:new Color(.45f,.45f,.45f);GUI.DrawTexture(ir.ContractedBy(2f),e.ability.uiIcon??BaseContent.BadTex);GUI.color=Color.white;
     if(have)Widgets.DrawBox(ir);
     Text.Font=GameFont.Tiny;Text.Anchor=TextAnchor.UpperCenter;GUI.color=have?Color.white:Color.gray;Widgets.Label(new Rect(ir.x-30f,ir.yMax,ir.width+60f,16f),e.ability.label);GUI.color=Color.white;Text.Anchor=TextAnchor.UpperLeft;Text.Font=GameFont.Small;
     Widgets.DrawHighlightIfMouseover(ir);
     if(Mouse.IsOver(ir))TooltipHandler.TipRegion(ir,Tip(p,c,e,have,miss));
    }
    ry+=rowH;
   }
   y+=treeH+4f;
   if(lv>0)y=ClassSubclass.DrawRow(y,w,p,h,c,own); // 子职业行
   if(lv>0 && c.legendary.Count>0)y=DrawLegendRow(y,w,p,h,c,own);
   return y+4f;
  }
  // 1.74.0「传奇」行：两个传奇能力的图标、状态，可以学的时候有按钮。
  static float DrawLegendRow(float y,float w,Pawn p,Hediff_Adventurer h,LP_ClassDef c,bool own){
   var row=new Rect(0f,y,w,44f);Widgets.DrawBoxSolid(row,new Color(.35f,.28f,.1f,.25f));
   GUI.color=new Color(1f,.82f,.35f);Widgets.Label(new Rect(6f,y+12f,50f,24f),"传奇");GUI.color=Color.white;
   float x=60f,cw=(w-60f)/Math.Max(1,c.legendary.Count);
   foreach(var a in c.legendary){
    var cell=new Rect(x,y+2f,cw-6f,40f);x+=cw;
    bool have=ClassLegendary.Knows(h,a);string why=have?null:ClassLegendary.CanLearn(h,c,a);
    if(!have)GUI.color=new Color(.5f,.5f,.5f);GUI.DrawTexture(new Rect(cell.x,cell.y+2f,36f,36f),a.uiIcon??BaseContent.BadTex);GUI.color=Color.white;
    float bw=own && !have && why==null?72f:0f;
    if(!have)GUI.color=Color.gray;Widgets.Label(new Rect(cell.x+40f,cell.y+10f,cell.width-40f-bw,24f),(a.LabelCap+(have?"":"（"+(why??"可以学")+"）")).Truncate(cell.width-40f-bw));GUI.color=Color.white;
    TooltipHandler.TipRegion(new Rect(cell.x,cell.y,cell.width-bw,cell.height),a.LabelCap+"\n\n"+a.description+"\n\n"+(have?"已掌握":why??"可以学")+"\n学会后"+c.label+"的资源上限 +"+ClassLegendary.ResourceBonus+"。");
    if(bw>0f){var b=new Rect(cell.xMax-bw,cell.y+6f,bw,28f);string key="leg:"+a.defName;if(Widgets.ButtonText(b,armed==key?"确定？":"学习")){if(armed==key){armed=null;ClassLegendary.Learn(h,c,a,true);}else armed=key;}}
   }
   return y+46f;
  }
  static string Tip(Pawn p,LP_ClassDef c,ClassAbilityEntry e,bool have,string miss){
   var d=e.ability;var props=d.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
   string cost=props==null?"":props.passive?"被动":"消耗 "+ClassSystem.ResourceLabel(c.resource)+" "+props.cost;
   int cd=d.cooldownTicksRange.max;
   return d.LabelCap+"（"+c.label+" "+e.level+" 级"+(e.capstoneSkill!=null?"，"+e.capstoneSkill.LabelCap+" "+ClassSystem.CapstoneSkill+" 级":"")+"）\n\n"+d.description+"\n\n"+cost+(cd>0?"　冷却 "+cd.ToStringTicksToPeriod():"")+(props!=null && props.stat!=null?"\n当前强度 "+ClassSystem.Strength(p,props).ToStringPercent():"")+"\n\n"+(have?"已掌握":miss??"未掌握");
  }
 }
}
