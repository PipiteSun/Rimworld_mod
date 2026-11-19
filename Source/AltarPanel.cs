using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.178.0 死灵仪式台专属面板（Peter：仪式台要有独特界面，不要全塞在原版工作单清单里）。
 // 仪式台不再有原版“工作单”页签：选中后点「仪式台面板」。左边是服务命匣（等级、献祭经验、统御、造物），
 // 右边按 仪式 / 献祭 / 造物 / 植入物 / 锻造与抄写 分类列出能做的事，点「排入」「持续」直接加工作单；
 // 下面是这张台子的工作队列（上下移动、加减次数、暂停、原版详细设置、删除）。
 // 底层仍是原版工作单（工人照常来做），所以存档、执行者限制、材料筛选都不变。
 public static class AltarPanel {
  public static readonly string[] Tabs={"仪式","献祭","造物","植入物","锻造与抄写"};
  public static bool Hidden(RecipeDef r)=>r.defName.StartsWith("LP_Harvest") || r.defName=="LP_Rebuild";
  public static string Category(RecipeDef r){
   string n=r.defName;
   if(n=="LP_Ascend")return "仪式";
   if(n.StartsWith("LP_Offer"))return "献祭";
   if(n=="LP_Refill" || (n.StartsWith("LP_Make") && !n.Contains("Talisman")))return "造物";
   if(r.Worker is FleshImplantRecipe || n.StartsWith("LP_Craft_"))return "植入物";
   return "锻造与抄写";
  }
  public static List<RecipeDef> Recipes(Building_Altar a,string tab)=>a.def.AllRecipes.Where(r=>!Hidden(r) && Category(r)==tab).ToList();
  // 现在为什么不能排入（null = 可以）。巫妖仪式的执行者在「指定执行者」里另外检查。
  public static string Reason(RecipeDef r,Building_Altar a){
   if(!r.AvailableNow){
    var need=new List<ResearchProjectDef>();if(r.researchPrerequisite!=null)need.Add(r.researchPrerequisite);if(r.researchPrerequisites!=null)need.AddRange(r.researchPrerequisites);
    var left=need.Where(x=>!x.IsFinished).Select(x=>x.LabelCap.ToString()).ToList();
    return left.Count>0?"需要研究：".Loc()+string.Join("、".Loc(),left):"当前条件不满足。".Loc();
   }
   if(r.Worker is RitualRecipe)return r.defName=="LP_Ascend"?null:Rituals.Blocked(r,a,null);
   return r.Worker.AvailableOnNow(a)?null:"当前条件不满足。".Loc();
  }
  public static string Cost(RecipeDef r){
   var parts=r.ingredients.Select(i=>i.Summary).ToList();
   var fx=r.GetModExtension<AltarForgeExtension>();if(fx!=null)parts.Add("命匣献祭经验 ".Loc()+fx.experience.ToString("0"));
   return parts.Count==0?"不需要材料".Loc():string.Join("，".Loc(),parts);
  }
  public static IEnumerable<Bill_Production> BillsOf(Building_Altar a,RecipeDef r)=>a.BillStack.Bills.OfType<Bill_Production>().Where(b=>b.recipe==r);
  // 排入：没有执行者限制的“按次数”工作单就加一次，否则新建；持续：已有持续单就取消暂停，否则新建。
  public static Bill_Production Queue(Building_Altar a,RecipeDef r,bool forever,Pawn performer=null){
   if(performer==null){
    var same=BillsOf(a,r).FirstOrDefault(b=>b.PawnRestriction==null && !b.suspended && b.repeatMode==(forever?BillRepeatModeDefOf.Forever:BillRepeatModeDefOf.RepeatCount));
    if(same!=null){if(!forever)same.repeatCount++;return same;}
   }
   if(!(r.MakeNewBill() is Bill_Production bill))return null;
   bill.repeatMode=forever?BillRepeatModeDefOf.Forever:BillRepeatModeDefOf.RepeatCount;bill.repeatCount=1;
   if(performer!=null)bill.SetPawnRestriction(performer);
   a.BillStack.AddBill(bill);return bill;
  }
  public static List<Pawn> Performers(Building_Altar a,RecipeDef r)=>a.Map==null?new List<Pawn>():a.Map.mapPawns.FreeColonistsSpawned.Where(p=>Rituals.Blocked(r,a,p)==null).OrderBy(p=>p.LabelShort).ToList();
  public static List<Phylactery> NearBoxes(Building_Altar a)=>(Necro.Registry?.boxes??new List<Phylactery>()).Where(b=>b!=null && !b.Destroyed && b.MapHeld==a.Map && b.PositionHeld.DistanceTo(a.Position)<=12).ToList();
  public static void PickBox(Building_Altar a){
   var opts=NearBoxes(a).Select(b=>new FloatMenuOption((b.soul?.LabelShort??"无主".Loc())+" · 等级".Loc()+b.level,()=>a.selected=b)).ToList();
   if(opts.Count==0)opts.Add(new FloatMenuOption("12格内没有命匣".Loc(),null));
   Find.WindowStack.Add(new FloatMenu(opts));
  }
  public static Texture2D Tex(string path)=>ContentFinder<Texture2D>.Get(path,false);
  public static Texture2D RecipeIcon(RecipeDef r){
   string n=r.defName;
   if(n=="LP_Ascend")return Tex("Necromancy/Icons/LichCore");
   if(n.StartsWith("LP_Offer"))return Tex("Necromancy/Phylactery");
   if(n=="LP_Refill" || n=="LP_MakeGolem")return Tex("Necromancy/Golem");
   if(n=="LP_MakeSkeleton")return Tex("Necromancy/Skeleton");
   if(n.StartsWith("LP_MakeScholar"))return Tex("Necromancy/Scholar");
   return null;
  }
 }

 public class Dialog_AltarPanel : Window {
  readonly Building_Altar altar;
  public string tab="献祭";
  Vector2 listScroll,queueScroll,constructScroll,outerScroll;
  public override Vector2 InitialSize=>LPUI.Fit(1100f,760f);
  public Dialog_AltarPanel(Building_Altar altar){
   this.altar=altar;doCloseX=true;draggable=true;closeOnClickedOutside=false;absorbInputAroundWindow=false;preventCameraMotion=false;
   var box=altar.Box;tab=box==null && NecroAny()==false?"仪式":"献祭";
  }
  static bool NecroAny()=>Necro.Registry?.boxes?.Any(b=>b!=null && !b.Destroyed)==true;
  public override void DoWindowContents(Rect inRect){
   if(altar==null || altar.Destroyed || !altar.Spawned){Close();return;}
   var rect=inRect;bool sc=LPUI.BeginMinHeight(ref rect,560f,ref outerScroll);
   try{Draw(rect);}finally{if(sc)Widgets.EndScrollView();LPUI.Reset();GUI.enabled=true;}
  }
  void Draw(Rect rect){
   float y=LPUI.Title(rect,"死灵仪式台".Loc());
   LPUI.Tiny(new Rect(0,y-4,rect.width,20),"在这里排入仪式、献祭、造物和植入物培育；工人按队列从上到下来台子做。造物唤醒与重塑的追加材料从全图吸收。".Loc(),true);
   y+=20;
   float leftW=Mathf.Min(330f,rect.width*.32f);
   DrawBox(new Rect(0,y,leftW,rect.height-y));
   var right=new Rect(leftW+LPUI.SectionGap,y,rect.width-leftW-LPUI.SectionGap,rect.height-y);
   float queueH=Mathf.Clamp(right.height*.30f,140f,220f);
   DrawRecipes(new Rect(right.x,right.y,right.width,right.height-queueH-LPUI.SectionGap));
   DrawQueue(new Rect(right.x,right.yMax-queueH,right.width,queueH));
  }

  // ---------- 左栏：服务命匣 ----------
  void DrawBox(Rect r){
   Widgets.DrawMenuSection(r);var inner=r.ContractedBy(10f);float y=inner.y;
   y=LPUI.Bar(new Rect(inner.x,y,inner.width,LPUI.RowH),"服务命匣".Loc(),LPUI.Gold,"献祭、造物胚体与补给都服务于这里选中的命匣。命匣必须留在仪式台12格内。".Loc());
   var box=altar.Box;
   if(LPUI.Button(new Rect(inner.x,y,inner.width,LPUI.ButtonH),box==null?"选择命匣…".Loc():"更换命匣…".Loc()))AltarPanel.PickBox(altar);
   y+=LPUI.ButtonH+LPUI.Gap;
   if(box==null){
    GUI.color=LPUI.Locked;y+=LPUI.Para(inner.x,y,inner.width,AltarPanel.NearBoxes(altar).Count>0?"台子12格内有命匣，点上面的按钮选一个。".Loc():"台子12格内没有命匣。还没有巫妖时，先在「仪式」页排入巫妖仪式；已有命匣就把它搬到台子旁边。".Loc());GUI.color=Color.white;
    return;
   }
   Pawn soul=box.soul;var lich=Necro.Lich(soul);
   var head=new Rect(inner.x,y,inner.width,56);
   LPUI.Icon(new Rect(head.x,head.y,52,52),AltarPanel.Tex("Necromancy/Phylactery"));
   if(soul!=null && soul.Spawned)Widgets.ThingIcon(new Rect(head.x+56,head.y,52,52),soul);
   float tx=head.x+(soul!=null && soul.Spawned?114:58);
   Text.Font=GameFont.Medium;LPUI.LabelFit(new Rect(tx,head.y,head.xMax-tx,30),soul?.LabelShort??"无主".Loc());Text.Font=GameFont.Small;
   string state=soul==null?"未绑定".Loc():soul.Dead?(box.revivalTick>0?"复生中：还有 ".Loc()+((box.revivalTick-Find.TickManager.TicksGame)/60000f).ToString("0.0")+" 天".Loc():"已死亡".Loc()):lich!=null && lich.Active?"巫妖 · 存活".Loc():"命匣失效".Loc();
   LPUI.Tiny(new Rect(tx,head.y+30,head.xMax-tx,20),state,soul==null || soul.Dead);
   y+=58;
   int cap=LichMod.Settings.levelCap;float need=Necro.Requirement(box.level);
   LPUI.LabelFit(new Rect(inner.x,y,inner.width,LPUI.RowH),"命匣等级 ".Loc()+box.level+" / "+cap+(box.Effective<box.level-.01f?"（有效 ".Loc()+box.Effective.ToString("0.#")+"）".Loc():""));y+=LPUI.RowH;
   var bar=new Rect(inner.x,y,inner.width,20);Widgets.FillableBar(bar,box.level>=cap?1f:Mathf.Clamp01(box.experience/need));
   Text.Anchor=TextAnchor.MiddleCenter;Text.Font=GameFont.Tiny;Widgets.Label(bar,box.level>=cap?"已满级".Loc():"献祭 ".Loc()+box.experience.ToString("0")+" / "+need.ToString("0"));LPUI.Reset();y+=24;
   if(soul!=null){
    int used=Necro.Used(soul),capacity=box.Capacity;
    LPUI.LabelFit(new Rect(inner.x,y,inner.width,LPUI.RowH),"统御 ".Loc()+used+" / "+capacity);y+=LPUI.RowH;
    var cbar=new Rect(inner.x,y,inner.width,14);Widgets.FillableBar(cbar,capacity<=0?0f:Mathf.Clamp01(used/(float)capacity));y+=18;
    if(lich!=null && lich.Active){LPUI.LabelFit(new Rect(inner.x,y,inner.width,LPUI.RowH),"精神力 ".Loc()+lich.Points.ToString("0")+" / "+(lich.Power*lich.FocusCeiling).ToString("0"));y+=LPUI.RowH;}
   }
   y+=LPUI.Gap;
   var cons=soul==null?new List<Pawn>():Necro.LivingConstructs(soul).OrderBy(p=>Necro.Construct(p).kind).ThenBy(p=>p.LabelShort).ToList();
   y=LPUI.Bar(new Rect(inner.x,y,inner.width,LPUI.RowH),"死灵造物（".Loc()+cons.Count+"）".Loc());
   var outR=new Rect(inner.x,y,inner.width,inner.yMax-y);if(outR.height<30)return;
   var view=new Rect(0,0,outR.width-LPUI.ScrollBarW,Math.Max(outR.height-1,cons.Count*30f));
   Widgets.BeginScrollView(outR,ref constructScroll,view);
   for(int i=0;i<cons.Count;i++){
    var p=cons[i];var h=Necro.Construct(p);var row=new Rect(0,i*30f,view.width,28);
    if(i%2==0)Widgets.DrawLightHighlight(row);Widgets.DrawHighlightIfMouseover(row);
    Widgets.ThingIcon(new Rect(row.x+2,row.y+2,24,24),p);
    string tag=(h.dormant?"休眠 · ".Loc():"工作中 · ".Loc())+"统御 ".Loc()+h.Cost+(h.kind=="Golem"?" · 血肉 ".Loc()+h.flesh.ToString("0"):"");
    LPUI.LabelFit(new Rect(row.x+30,row.y+4,row.width*.45f,22),p.LabelShort);
    LPUI.Tiny(new Rect(row.x+30+row.width*.45f,row.y+6,row.width*.55f-34,20),tag,h.dormant);
    if(Widgets.ButtonInvisible(row) && p.Spawned){CameraJumper.TryJumpAndSelect(p);}
   }
   Widgets.EndScrollView();
  }

  // ---------- 右上：分类与可做的事 ----------
  void DrawRecipes(Rect r){
   var tabs=AltarPanel.Tabs.Where(t=>AltarPanel.Recipes(altar,t).Count>0).ToList();
   if(!tabs.Contains(tab) && tabs.Count>0)tab=tabs[0];
   var cells=LPUI.Row(new Rect(r.x,r.y,r.width,LPUI.ButtonH),Math.Max(1,tabs.Count),4f);
   for(int i=0;i<tabs.Count;i++){
    string t=tabs[i];int ready=AltarPanel.Recipes(altar,t).Count(x=>AltarPanel.Reason(x,altar)==null);
    if(t==tab)Widgets.DrawBoxSolid(cells[i].ExpandedBy(2f),new Color(1f,.82f,.35f,.35f));
    if(LPUI.Button(cells[i],t.Loc()+" · "+ready))tab=t;
   }
   var list=AltarPanel.Recipes(altar,tab);
   var outR=new Rect(r.x,r.y+LPUI.ButtonH+LPUI.Gap,r.width,r.height-LPUI.ButtonH-LPUI.Gap);
   Widgets.DrawMenuSection(outR);outR=outR.ContractedBy(4f);
   const float rowH=66f;
   var view=new Rect(0,0,outR.width-LPUI.ScrollBarW,Math.Max(outR.height-1,list.Count*(rowH+4)));
   Widgets.BeginScrollView(outR,ref listScroll,view);
   for(int i=0;i<list.Count;i++)DrawRecipeRow(new Rect(0,i*(rowH+4),view.width,rowH),list[i]);
   Widgets.EndScrollView();
  }
  void DrawRecipeRow(Rect row,RecipeDef rd){
   string reason=AltarPanel.Reason(rd,altar);bool ok=reason==null;
   Widgets.DrawLightHighlight(row);Widgets.DrawHighlightIfMouseover(row);
   var icon=new Rect(row.x+6,row.y+9,48,48);
   var tex=AltarPanel.RecipeIcon(rd);var product=rd.ProducedThingDef??rd.UIIconThing;
   if(tex!=null)LPUI.Icon(icon,tex,!ok);else if(product!=null)Widgets.ThingIcon(icon,product,null,null,1f,ok?(Color?)null:new Color(.5f,.5f,.5f));else LPUI.Icon(icon,AltarPanel.Tex("Necromancy/Icons/LichCore"),!ok);
   bool ascend=rd.defName=="LP_Ascend";float btnW=ascend?150f:78f,btns=ascend?btnW:btnW*2+6;
   float tx=row.x+62,tw=row.width-62-btns-16;
   var queued=AltarPanel.BillsOf(altar,rd).ToList();
   string qtext=queued.Count==0?"":"  · 队列中 ".Loc()+(queued.Any(b=>b.repeatMode==BillRepeatModeDefOf.Forever)?"持续".Loc():queued.Where(b=>b.repeatMode==BillRepeatModeDefOf.RepeatCount).Sum(b=>b.repeatCount)+" 次".Loc());
   GUI.color=ok?Color.white:LPUI.Locked;LPUI.LabelFit(new Rect(tx,row.y+4,tw,24),rd.LabelCap+qtext,rd.description);GUI.color=Color.white;
   LPUI.Tiny(new Rect(tx,row.y+26,tw,18),"材料：".Loc()+AltarPanel.Cost(rd),true,rd.description);
   var c=GUI.color;GUI.color=ok?LPUI.Have:new Color(1f,.55f,.45f);LPUI.Tiny(new Rect(tx,row.y+44,tw,18),ok?(ascend?"选一位殖民者执行；被选中的人会成为巫妖。".Loc():"可以排入。".Loc()):reason);GUI.color=c;
   TooltipHandler.TipRegion(new Rect(row.x,row.y,row.width-btns-12,row.height),rd.description);
   float bx=row.xMax-btns-8,by=row.y+(row.height-LPUI.ButtonH)/2f;
   if(ascend){
    if(LPUI.Button(new Rect(bx,by,btnW,LPUI.ButtonH),"指定执行者…".Loc(),ok)){
     var who=AltarPanel.Performers(altar,rd);var opts=who.Select(p=>new FloatMenuOption(p.LabelShort+" · 心灵敏感度 ".Loc()+p.GetStatValue(StatDefOf.PsychicSensitivity).ToStringPercent(),()=>{AltarPanel.Queue(altar,rd,false,p);Messages.Message("已排入巫妖仪式：".Loc()+p.LabelShort,MessageTypeDefOf.TaskCompletion,false);})).ToList();
     if(opts.Count==0)opts.Add(new FloatMenuOption("没有符合条件的殖民者（尚未转化、心灵敏感度大于0）".Loc(),null));
     Find.WindowStack.Add(new FloatMenu(opts));
    }
    return;
   }
   if(!ok){LPUI.Unavailable(new Rect(bx,by,btns,LPUI.ButtonH),"暂不可排入".Loc());return;}
   if(LPUI.Button(new Rect(bx,by,btnW,LPUI.ButtonH),"排入 +1".Loc(),ok,"加一次。已有同类按次数的工作单时，在它上面加一次。".Loc()))AltarPanel.Queue(altar,rd,false);
   if(LPUI.Button(new Rect(bx+btnW+6,by,btnW,LPUI.ButtonH),"持续".Loc(),ok,"一直做下去（材料够、条件满足就做）。".Loc()))AltarPanel.Queue(altar,rd,true);
  }

  // ---------- 右下：工作队列 ----------
  void DrawQueue(Rect r){
   var bills=altar.BillStack.Bills.OfType<Bill_Production>().ToList();
   float y=LPUI.Bar(new Rect(r.x,r.y,r.width,LPUI.RowH),"工作队列（".Loc()+bills.Count+"）".Loc(),null,"工人按从上到下的顺序挑能做的工作单。「设置」打开原版的详细设置（执行者、材料筛选、搜索半径等）。".Loc());
   var outR=new Rect(r.x,y,r.width,r.yMax-y);Widgets.DrawMenuSection(outR);outR=outR.ContractedBy(4f);
   if(bills.Count==0){LPUI.Unavailable(new Rect(outR.x,outR.y,outR.width,40),"队列是空的。在上面点「排入」或「持续」。".Loc());return;}
   const float rowH=32f;
   var view=new Rect(0,0,outR.width-LPUI.ScrollBarW,Math.Max(outR.height-1,bills.Count*(rowH+2)));
   Widgets.BeginScrollView(outR,ref queueScroll,view);
   Bill_Production remove=null;Bill_Production up=null,down=null;
   for(int i=0;i<bills.Count;i++){
    var b=bills[i];var row=new Rect(0,i*(rowH+2),view.width,rowH);
    if(i%2==0)Widgets.DrawLightHighlight(row);
    float x=row.xMax;
    Rect Btn(float w){x-=w+4;return new Rect(x,row.y+2,w,rowH-4);}
    if(LPUI.Button(Btn(52),"删除".Loc()))remove=b;
    if(LPUI.Button(Btn(52),"设置".Loc()))Find.WindowStack.Add(new Dialog_BillConfig(b,altar.Position));
    if(LPUI.Button(Btn(58),b.suspended?"继续".Loc():"暂停".Loc()))b.suspended=!b.suspended;
    bool counted=b.repeatMode==BillRepeatModeDefOf.RepeatCount;
    if(LPUI.Button(Btn(28),"+",counted))b.repeatCount++;
    if(LPUI.Button(Btn(28),"−",counted && b.repeatCount>1))b.repeatCount--;
    if(LPUI.Button(Btn(28),"↓",i<bills.Count-1))down=b;
    if(LPUI.Button(Btn(28),"↑",i>0))up=b;
    string who=b.PawnRestriction!=null?"  · 执行者 ".Loc()+b.PawnRestriction.LabelShort:"";
    string reason=b.recipe.Worker is RitualRecipe && b.recipe.defName!="LP_Ascend"?Rituals.Blocked(b.recipe,altar,null):null;
    GUI.color=b.suspended?LPUI.Locked:Color.white;
    LPUI.LabelFit(new Rect(row.x+6,row.y+5,x-row.x-10,24),(i+1)+". "+b.recipe.LabelCap+"  ·  "+b.RepeatInfoText+who+(b.suspended?"  · 已暂停".Loc():reason!=null?"  · 等待：".Loc()+reason:""),b.recipe.description);
    GUI.color=Color.white;
   }
   Widgets.EndScrollView();
   if(remove!=null)altar.BillStack.Delete(remove);
   if(up!=null)altar.BillStack.Reorder(up,-1);
   if(down!=null)altar.BillStack.Reorder(down,1);
  }
 }
}
