using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.48.0 冒险者：检视分页、升级信件（1.52.0 热修2 起加点、专长、恩赐都在分页里，没有单独的窗口）。分页挂到所有人形种族（含 HAR 种族）上，没有冒险者状态时不显示。
 [StaticConstructorOnStartup]
 public static class AdventurerStartup {
  static AdventurerStartup(){
   if(GenCommandLine.CommandLineArgPassed("lichtest"))Adventurer.TestSuppressed=Talents.StripAll=HeroTraits.TestSuppressed=true; // 自测进程：冒险者 / 强力特性检查组自己临时打开
   foreach(var d in DefDatabase<ThingDef>.AllDefsListForReading){
    if(d.race==null || !d.race.Humanlike)continue;
    if(d.inspectorTabs==null)d.inspectorTabs=new List<Type>();
    if(d.inspectorTabs.Contains(typeof(ITab_Adventurer)))continue;
    d.inspectorTabs.Add(typeof(ITab_Adventurer));
    d.inspectorTabsResolved?.Add(InspectTabManager.GetSharedInstance(typeof(ITab_Adventurer)));
   }
  }
 }

 // 1.52.0 热修：分页 630 宽、高度按屏幕（屏幕高 − 300，限制在 220～480），全部内容放进从顶端开始的滚动区；
 // 顺序：等级行 → 经验条 → 六维（就地加点）→ 传奇属性点（就地投点）→ 专长（热修2：有属性提升时下面列出可选专长）→ 史诗恩赐（有恩赐点时）→ 种族 / 天赋 / VPE。没有的项画灰色占位行，Isekai 共存时不隐藏任何按钮。
 public class ITab_Adventurer : ITab {
  public const float Width=630f,MinHeight=220f,MaxHeight=480f,ScreenMargin=300f;
  public static float HeightFor(float screenHeight)=>Mathf.Clamp(screenHeight-ScreenMargin,MinHeight,MaxHeight);
  public static Vector2 SizeFor(float screenHeight)=>new Vector2(Width,HeightFor(screenHeight));
  public ITab_Adventurer(){size=SizeFor(UI.screenHeight);labelKey="LP_AdventurerTab";}
  protected override void UpdateSize(){base.UpdateSize();size=SizeFor(UI.screenHeight);}
  Pawn Shown=>SelPawn??(SelThing as Corpse)?.InnerPawn;
  public override bool IsVisible=>Adventurer.Enabled && Adventurer.Get(Shown)!=null;
  Vector2 scroll;float contentH=600f;
  public static int page; // 1.72.0：0 = 冒险者，1 = 职业；2026-10-30：2 = 属性
  static Texture2D greyBar;static Texture2D GreyBar=>greyBar??(greyBar=SolidColorMaterials.NewSolidColorTexture(new Color(.35f,.35f,.35f)));
  static readonly Color Gold=new Color(1f,.82f,.35f);
  const float Row=26f;
  // 热修2：分页里选专长 / 恩赐。“选择”第一次点只是待确认（按钮变成“确定？”），同一个按钮再点一次才生效；换人就清掉。
  string armed;Pawn armedFor;float featPickY,boonPickY;
  // 1.53.0 职业特性（VPE 桥登记）：分页里的额外攻击 / 斗志 / VPE 等级行（"文字|悬停说明"），升级信件里按等级多写的行。
  public static Func<Pawn,List<string>> ClassLinesHook;
  public static Func<Pawn,int,IEnumerable<string>> LevelUpLinesHook;
  int stylePickSlot=-1;
  public static string StyleKey(int slot,LP_FightingStyleDef s)=>"style:"+slot+":"+s.defName;
  public static bool PickStyle(Hediff_Adventurer h,int slot,LP_FightingStyleDef s,ref string armed){
   if(Styles.CanPick(h,slot,s)!=null){armed=null;return false;}
   return Confirm(ref armed,StyleKey(slot,s)) && Styles.Pick(h,slot,s);
  }
  public static string FeatKey(LP_FeatDef f)=>"feat:"+f.defName;
  public static string BoonKey(LP_EpicBoonDef b,int ability)=>"boon:"+b.defName+":"+ability;
  public static bool Confirm(ref string armed,string key){if(armed==key){armed=null;return true;}armed=key;return false;}
  public static bool PickFeat(Hediff_Adventurer h,LP_FeatDef f,ref string armed){
   if(h==null || h.CanTakeFeat(f)!=null){armed=null;return false;}
   return Confirm(ref armed,FeatKey(f)) && h.TakeFeat(f);
  }
  public static bool PickBoon(Hediff_Adventurer h,LP_EpicBoonDef b,int ability,ref string armed){
   if(h==null || h.CanTakeBoon(b)!=null || b==Epic.Might && (ability<0 || ability>5)){armed=null;return false;}
   return Confirm(ref armed,BoonKey(b,b==Epic.Might?ability:-1)) && h.TakeBoon(b,ability);
  }
  // 一行效果：去掉描述开头那句点题的话（“时刻提防着危险。”），剩下的是实际效果。
  public static string FeatEffect(LP_FeatDef f){string d=f.description??"";int i=d.IndexOf('。');return i>=0 && i<d.Length-1?d.Substring(i+1):d;}
  // 选中这个人并打开冒险者分页（升级信件用）。
  public static void OpenFor(Pawn p){
   if(p==null)return;
   CameraJumper.TryJumpAndSelect(p,CameraJumper.MovementMode.Pan);
   if(Find.Selector.SingleSelectedThing==p)InspectPaneUtility.OpenTab(typeof(ITab_Adventurer));
  }
  public static void AffinityColor(float eff){if(eff>1.0001f)GUI.color=new Color(.6f,1f,.6f);else if(eff<.9999f)GUI.color=new Color(1f,.65f,.55f);}
  protected override void FillTab(){
   var p=Shown;var h=Adventurer.Get(p);if(h==null)return;
   // 1.72.0：顶部两页——“冒险者”（原来的全部内容）和“职业”（ClassPage）。
   var all=new Rect(0f,0f,size.x,size.y).ContractedBy(10f);
   int pts=ClassSystem.Points(h);
   for(int i=0;i<3;i++){ // 2026-10-30：第三页“属性”（AttributePage：六维逐项公式与当前值）
    var tb=new Rect(all.x+i*124f,all.y,120f,24f);bool on=page==i;
    if(on)Widgets.DrawBoxSolid(tb,new Color(1f,1f,1f,.12f));
    if(Widgets.ButtonText(tb,i==0?"冒险者":i==1?"职业"+(pts>0?"（"+pts+"）":""):"属性",drawBackground:!on) && page!=i){page=i;scroll=Vector2.zero;}
   }
   var outer=new Rect(all.x,all.y+28f,all.width,all.height-28f);
   var view=new Rect(0f,0f,outer.width-16f,Mathf.Max(contentH,outer.height-4f));
   Widgets.BeginScrollView(outer,ref scroll,view);
   try{contentH=(page==1?ClassPage.Draw(view.width,p,h,p.Faction!=null && p.Faction.IsPlayer && !p.Dead):page==2?AttributePage.Draw(view.width,p,h):DrawContents(view.width,p,h))+8f;}
   finally{Text.Font=GameFont.Small;Text.Anchor=TextAnchor.UpperLeft;GUI.color=Color.white;Widgets.EndScrollView();}
  }

  // ---------- 文字（检查也用） ----------
  // 1000 以上写成 k（一位小数）：9000 → 9.0k。
  public static string K(float v)=>v>=1000f?(v/1000f).ToString("0.0")+"k":v.ToString("0");
  public static string XpText(Hediff_Adventurer h){
   if(h.level>=Legend.MaxLevel)return "经验 "+K(h.xp)+"（满级）";
   if(h.CappedAt20)return h.legendary?"已满（“传奇等级”已在设置里关闭）":"已满，需成为传奇冒险者";
   return "经验 "+K(h.xp)+" / "+K(h.NextXp);
  }
  public static float XpFrac(Hediff_Adventurer h)=>h.level>=Legend.MaxLevel || h.CappedAt20?1f:Mathf.Clamp01(Mathf.InverseLerp(Legend.XpFor(h.level),h.NextXp,h.xp));
  static readonly string[][] MilestoneList={
   new[]{"portal","传送门",""},new[]{"vessel","神明附体首领",""},new[]{"dragonDuel","龙决斗",""},new[]{"tower","法师塔",""},new[]{"larloch","拉洛克差事","每件"},new[]{"council","神灵议会","每个神性等级"}};
  static readonly float[] MilestoneXp={Adventurer.PortalXP,Adventurer.VesselXP,Adventurer.DragonDuelXP,Adventurer.TowerXP,Adventurer.LarlochXP,Adventurer.CouncilXP};
  // 悬停经验条：这个人的经验来源和具体数字。
  public static string XpSourcesTip(Hediff_Adventurer h){
   var sb=new System.Text.StringBuilder();float mult=Adventurer.XpScale*Talents.XpFactor(h);
   sb.AppendLine(XpText(h)+(h.level<Legend.MaxLevel && !h.CappedAt20?"（还差 "+K(Mathf.Max(0f,h.NextXp-h.xp))+"）":""));
   if(h.CappedAt20 && !h.legendary)sb.AppendLine("20 级已满：完成一个 DND 事件里程碑（击杀里程碑不算）成为传奇冒险者后解锁传奇等级 21～40，攒下的经验立即结算。经验照常累积（现在 "+K(h.xp)+"）。");
   sb.AppendLine();sb.AppendLine("经验来源：");
   sb.AppendLine("· 击杀 / 协助：敌人战斗力 × "+mult.ToString("0.##")+"，按造成的伤害比例分（独杀战斗力 100 的敌人 = "+(100f*mult).ToString("0")+"）");
   sb.AppendLine("· 守住袭击：袭击点数 × "+Adventurer.RaidShare+" × "+mult.ToString("0.##")+"，参战的人平分（1000 点、2 人 = 每人 "+(1000f*Adventurer.RaidShare*mult/2f).ToString("0")+"）");
   sb.AppendLine("· 完成任务：任务点数 × "+Adventurer.QuestShare+"（接任务的人），家里其他殖民者 × "+(Adventurer.QuestShare*Adventurer.QuestHomeShare).ToString("0.###"));
   sb.AppendLine("· DND 里程碑（每人每项一次）：");
   for(int i=0;i<MilestoneList.Length;i++){
    var m=MilestoneList[i];int done=m[2]==""?(h.milestonesDone.Contains(m[0])?1:0):h.milestonesDone.Count(k=>k.StartsWith(m[0]));
    sb.AppendLine("   "+(done>0?"✓ ":"　 ")+m[1]+" "+K(MilestoneXp[i]*mult)+(m[2]==""?"":"（"+m[2]+"，已完成 "+done+"）"));
   }
   int slay=h.milestonesDone.Count(k=>k.StartsWith("lp_slay_"));if(slay>0)sb.AppendLine("   ✓ 击败传奇怪物 ×"+slay);
   sb.AppendLine("· 研习卷：每读 1 小时 "+(Adventurer.StudyXP*mult).ToString("0")+(Adventurer.VPELoaded?"；云游导师：每课 "+(Adventurer.LessonXP*mult).ToString("0"):""));
   sb.AppendLine("· 冒险者手册："+(h.level>=Adventurer.TrainingCapLevel?"已超过（只练到 "+Adventurer.TrainingCapLevel+" 级）":"每本 "+(Adventurer.ManualXP*mult).ToString("0")+"，最多练到 "+Adventurer.TrainingCapLevel+" 级"));
   float wm=AdventurerWork.Mult; // 1.61.0 热修：日常工作
   sb.AppendLine(wm<=0f?"· 日常工作：已关闭":"· 日常工作：每 "+AdventurerWork.SkillPerXp.ToString("0")+" 技能经验 +"+(wm*mult).ToString("0.##")+"（今日 "+AdventurerWork.Today(h).ToString("0.#")+" / "+AdventurerWork.DailyFull.ToString("0")+" 全额，之后 ×"+AdventurerWork.OverFactor+"；累计 "+K(h.workXpTotal*mult)+"）");
   AdventurerExtraXp.Tip(sb,h,mult); // 1.75.0 非战斗经验
   if(Epic.KillMilestonesOn){
    sb.Append("· 击杀里程碑（下一级所需的 "+(Epic.KillMilestoneShare*100f).ToString("0")+"%，现在 "+K(Epic.NextLevelXp(h)*Epic.KillMilestoneShare)+"）：");
    sb.AppendLine(string.Join(" ",Epic.KillMilestones.Select(t=>(h.milestonesDone.Contains("kills"+t)?"✓":"")+t)));
    if(h.legendary)sb.AppendLine("   传奇：500 / 1000 杀各 +"+Legend.KillBonus+" 传奇属性点");
   }
   if(Epic.NemesisOn)sb.AppendLine("· 击杀宿敌冠军：下一级所需的 "+(Epic.NemesisShare*100f).ToString("0")+"%（现在 "+K(Epic.NextLevelXp(h)*Epic.NemesisShare)+"）"+(h.legendary?"，传奇 +"+Legend.NemesisBonus+" 传奇属性点":""));
   sb.AppendLine();
   sb.Append("当前经验倍率 "+(mult*100f).ToString("0")+"%（设置 "+(Adventurer.XpScale*100f).ToString("0")+"%"+(Talents.XpFactor(h)!=1f?" × 凡人之志 "+Talents.XpFactor(h).ToString("0.##"):"")+"；击杀里程碑和宿敌不乘倍率）");
   return sb.ToString();
  }
  // 预览：临时加点，取这项的效果说明（含种族亲和）。
  public static string Preview(Hediff_Adventurer h,int i,int by){h.scores[i]+=by;try{return h.EffectText(i);}finally{h.scores[i]-=by;}}
  static string EffectLine(Hediff_Adventurer h,int i){var lines=h.EffectText(i).Split('\n');foreach(var l in lines)if(l.StartsWith("属性倍率") || l.StartsWith("属性效果") || l.StartsWith("传奇单位"))return l.Trim();return lines.Length>2?lines[2].Trim():"";} // 有“专长 +1”一行时不能按行号取

  // ---------- 1.76.0 初始属性购点 ----------
  public const string BuyConfirmKey="buy:confirm";
  public static string BuyTip(Hediff_Adventurer h,int a,int d,string why){
   int b=h.buyBase[a],v=b+d;
   string head=Adventurer.Names[a]+" 购点基础值 "+b+"（种族、背景等 "+Adventurer.Signed(h.scores[a]-b)+"）";
   if(why!=null)return head+"\n不能"+(d<0?"减":"加")+"："+why;
   int c=Adventurer.BuyCost(v)-Adventurer.BuyCost(b);
   return head+"\n"+(d<0?"减到 ":"加到 ")+v+"："+(c>=0?"花 "+c:"退回 "+(-c))+" 点，剩 "+(h.BuyLeft-c)+"\n\n之后：\n"+Preview(h,a,d);
  }
  public static bool PressConfirmBuy(Hediff_Adventurer h,ref string armed){
   if(h==null || !h.buyOpen){armed=null;return false;}
   if(!Confirm(ref armed,BuyConfirmKey))return false;h.ConfirmBuy();return true;
  }
  float DrawBuyHeader(float y,float w,Pawn p,Hediff_Adventurer h){
   if(p!=armedFor){armed=null;armedFor=p;stylePickSlot=-1;}
   var row=new Rect(0f,y,w,30f);Widgets.DrawBoxSolid(row,new Color(1f,.82f,.35f,.10f));
   int left=h.BuyLeft;
   var lab=new Rect(4f,y+5f,w-270f,24f);GUI.color=left<0?new Color(1f,.6f,.5f):Gold;
   Widgets.Label(lab,"初始属性分配：剩余购点 "+left+" / "+Adventurer.BuyBudget);GUI.color=Color.white;
   if(Mouse.IsOver(lab))TooltipHandler.TipRegion(lab,"5e 购点制：每项基础值 8～15，费用 8:0　9:1　10:2　11:3　12:4　13:5　14:7　15:9（共 "+Adventurer.BuyBudget+" 点）。\n种族、背景特性等加值另算，显示的数值已经包含。\n点六维后面的 − / + 改基础值；预填的是按技能推荐的标准数组。\n点“确认”两次后锁定，每人只有一次；不确认也按当前分配生效。");
   float bx=w;
   {var b=new Rect(bx-90f,y+1f,90f,28f);bx-=94f;bool armedNow=armed==BuyConfirmKey;
    TooltipHandler.TipRegion(b,armedNow?"再点一次确认：锁定当前分配，之后不能再改"+(left>0?"（还剩 "+left+" 点没用，会作废）":"")+"。":"锁定当前分配（点两次）。");
    if(Widgets.ButtonText(b,armedNow?"确定？":"确认"))PressConfirmBuy(h,ref armed);}
   {var b=new Rect(bx-70f,y+1f,70f,28f);bx-=74f;TooltipHandler.TipRegion(b,"按技能恢复推荐分配（标准数组 15/14/13/12/10/8）。");if(Widgets.ButtonText(b,"推荐")){h.BuyRecommend();armed=null;}}
   {var b=new Rect(bx-80f,y+1f,80f,28f);TooltipHandler.TipRegion(b,"六项基础值全部改回 8，购点全部退回，再自己分配。");if(Widgets.ButtonText(b,"全部 8")){h.BuyReset();armed=null;}}
   return y+34f;
  }

  // ---------- 绘制 ----------
  float DrawContents(float w,Pawn p,Hediff_Adventurer h){
   float y=0f;bool own=p.Faction!=null && p.Faction.IsPlayer && !p.Dead;
   // 等级行：等级、熟练加值（悬停看效果）、经验数字
   Text.Font=GameFont.Medium;
   var title=new Rect(0f,y,250f,32f);
   if(h.legendary)GUI.color=Gold;
   Widgets.Label(title,h.level>Adventurer.MaxLevel?"传奇 Lv "+h.level:(h.legendary?"传奇冒险者 ":"冒险者 ")+"Lv "+h.level);GUI.color=Color.white;
   if(h.legendary)TooltipHandler.TipRegion(title,"传奇冒险者（称号，不占特性格）：20 级并完成过 DND 里程碑。\n殖民地全员心情 +3“与传奇同行”；本人社交影响 +10%；囚犯、访客和商人对其好感 +10。\n传奇死亡时全殖民地心情 −6（10 天）。");
   Text.Font=GameFont.Small;
   var prof=new Rect(252f,y+6f,120f,24f);Widgets.Label(prof,"熟练加值 +"+h.Proficiency);Widgets.DrawHighlightIfMouseover(prof);if(Mouse.IsOver(prof))TooltipHandler.TipRegion(prof,Adventurer.ProficiencyText(h));
   Text.Anchor=TextAnchor.MiddleRight;var xpr=new Rect(372f,y+4f,w-372f,26f);Widgets.Label(xpr,XpText(h));Text.Anchor=TextAnchor.UpperLeft;
   string tipCache=null;string Tip()=>tipCache??(tipCache=XpSourcesTip(h)); // 1.58.6：悬停说明只在鼠标停在上面时才拼（原来每帧都拼，带 LINQ）
   if(Mouse.IsOver(xpr))TooltipHandler.TipRegion(xpr,Tip());
   y+=34f;
   var bar=new Rect(0f,y,w,16f);
   if(h.CappedAt20 && h.level<Legend.MaxLevel)Widgets.FillableBar(bar,1f,GreyBar);else Widgets.FillableBar(bar,XpFrac(h));
   Widgets.DrawHighlightIfMouseover(bar);if(Mouse.IsOver(bar))TooltipHandler.TipRegion(bar,Tip());
   y+=22f;
   // 加点栏：可用属性点 + 自动加点 + 跳到下面的“可选专长 / 史诗恩赐”（热修2：都在分页里选）
   int pts=h.AvailablePoints;
   var head=new Rect(0f,y,180f,28f);
   if(pts<=0)GUI.color=Color.gray;Widgets.Label(new Rect(head.x,head.y+4f,head.width,24f),"可用属性点："+pts);GUI.color=Color.white;
   // 1.90.0：等级按这个人的实际规则列（勤勉 6 / 10 / 14 / 18、百炼成钢 20）；3 点的来源可能是百炼成钢、英雄之资或反骨，不再一律写百炼成钢。
   TooltipHandler.TipRegion(head,"每次属性提升 = "+h.ASIPoints+" 点"+(h.ASIPoints>2?"（天赋或特性加成，不叠加）":"")+"；"+string.Join("、",Enumerable.Range(2,Adventurer.MaxLevel-1).Where(l=>h.AsiAt(l)>0).Select(l=>l+(h.AsiAt(l)>1?"（×"+h.AsiAt(l)+"）":"")))+" 级各一次。\n点六维后面的 +1 / +2 直接加点，单项上限 "+Adventurer.ScoreCap+"（威能之恩那一项 +4，传奇冒险者每 5 个传奇等级 +2）。\n剩下 "+h.pendingASI+" 次完整的属性提升"+(h.asiLeft>0?"，另有 "+h.asiLeft+" 点是拆开的":"")+"；改选专长要消耗一次完整的属性提升。");
   float bx=w;
   {var r=new Rect(bx-110f,y,110f,28f);bx-=116f;bool auto=LichMod.Settings?.adventurerAutoASI??false,was=auto;
    Widgets.CheckboxLabeled(r,"自动加点",ref auto);
    TooltipHandler.TipRegion(r,"同设置里的“殖民者自动加点”：获得属性提升时自动加在职业主属性或最高的一项。打开时这个人剩下的点也立即自动分配。");
    if(auto!=was && LichMod.Settings!=null){LichMod.Settings.adventurerAutoASI=auto;LoadedModManager.GetMod<LichMod>()?.WriteSettings();if(auto && own && pts>0)h.AutoAssign();}}
   if(own && h.boonPoints>0){var r=new Rect(bx-120f,y,120f,28f);bx-=126f;
    if(Widgets.ButtonText(r,"选恩赐（"+h.boonPoints+"）"))scroll.y=boonPickY;
    TooltipHandler.TipRegion(r,"跳到下面的“史诗恩赐”列表，在分页里直接选。");}
   if(own && h.FeatPicks>0){var r=new Rect(bx-120f,y,120f,28f);
    if(Widgets.ButtonText(r,"选专长（"+h.FeatPicks+"）"))scroll.y=featPickY;
    TooltipHandler.TipRegion(r,"跳到下面的“可选专长”列表："+(h.fighterFeats>0?"先用战士奖励专长（免费，剩 "+h.fighterFeats+" 个），":"")+"用一次完整的属性提升换一个专长。");}
   y+=32f;
   bool buy=own && h.buyOpen;
   if(buy)y=DrawBuyHeader(y,w,p,h); // 1.76.0 初始属性购点
   // 六维：一行一项，后面就地 +1 / +2，再后面是当前效果
   int exRaw=Exalted.Raw(h); // 热修1：达标但效果没生效（Isekai 共存等）的项也标出来，灰色
   for(int i=0;i<6;i++){
    var row=new Rect(0f,y,w,26f);if(i%2==0)Widgets.DrawLightHighlight(row);
    var lab=new Rect(24f,y+2f,190f,24f);bool open=expanded.Contains(i); // 1.81.0 热修5：点属性名或箭头展开效果表
    var arrow=new Rect(4f,y+5f,16f,16f);GUI.DrawTexture(arrow,open?TexButton.Collapse:TexButton.Reveal);
    if(Widgets.ButtonInvisible(arrow) || Widgets.ButtonInvisible(lab)){if(open)expanded.Remove(i);else expanded.Add(i);}
    AffinityColor(Affinity.ScoreMult(h,i));
    if((h.mythicMask&1<<i)!=0)GUI.color=Mythic;else if((h.exaltedMask&1<<i)!=0)GUI.color=Gold;else if((exRaw&1<<i)!=0)GUI.color=ExaltedOff; // 1.59.0 超凡金色、神话紫色
    Widgets.Label(lab,Adventurer.Names[i]+" "+h.scores[i]+Feats.ScoreTag(h,i)+"/"+h.Cap(i)+" ("+Adventurer.Signed(h.Mod(i))+")"+Affinity.Tag(h,i));GUI.color=Color.white;
    if(Mouse.IsOver(lab))TooltipHandler.TipRegion(lab,h.EffectText(i)+Exalted.Tip(h,i)+"\n\n（点属性名展开 / 收起效果表）");
    float x=216f;
    if(buy){ // 1.76.0 购点：− / + 改基础值
     for(int d=-1;d<=1;d+=2){
      var b=new Rect(x,y+1f,28f,24f);x+=30f;string why=h.CanBuy(i,d);
      if(Mouse.IsOver(b))TooltipHandler.TipRegion(b,BuyTip(h,i,d,why));
      if(Widgets.ButtonText(b,d<0?"−":"+",active:why==null) && why==null)h.Buy(i,d);
     }
     x+=4f;
    }
    if(own && pts>0){
     for(int by=1;by<=2;by++){
      if(by>pts)break;var b=new Rect(x,y+1f,40f,24f);x+=44f;bool ok=h.CanRaise(i,by);
      if(Mouse.IsOver(b))TooltipHandler.TipRegion(b,ok?"加点后：\n"+Preview(h,i,by):"超过上限 "+h.Cap(i));
      if(Widgets.ButtonText(b,"+"+by,active:ok) && ok)h.SpendPoints(i,by);
     }
    }
    x=Mathf.Max(x,306f);
    Text.Font=GameFont.Tiny;GUI.color=new Color(.8f,.8f,.8f);Widgets.Label(new Rect(x,y+5f,w-x-4f,22f),EffectLine(h,i));GUI.color=Color.white;Text.Font=GameFont.Small;
    y+=27f;
    if(open)y=DrawEffectTable(y,w,h,i);
   }
   y=Growth.Draw(y+2f,w,h); // 1.58.3 典籍 / 许愿 / 实战磨砺
   y=DrawExalted(y,w,h); // 1.59.0 超凡
   if(Adventurer.EffectsHandedToIsekai){Grey(ref y,w,"六维的数值效果已交给 Isekai（设置“Isekai 共存模式”）；等级、加点、专长、恩赐、熟练加值照常");}
   else if(Adventurer.EffectScale<=0f){Grey(ref y,w,"属性效果强度 0%：六维没有数值效果");}
   else{ // 1.99.0（Peter）：和属性页“部位生命合计”同一个数（含坚韧、传奇血量），以前只算等级与体质
    float tough=Feats.ToughFactor(h),leg=Legend.HealthFactor(h);
    Widgets.Label(new Rect(0f,y,w,24f),"部位生命合计 +"+((Adventurer.PartHealthFactor(h.pawn)-1f)*100f).ToString("0.#")+"%（等级、体质"+(Math.Abs(tough-1f)>.0001f?"、坚韧":"")+(Math.Abs(leg-1f)>.0001f?"、传奇血量":"")+"）");y+=Row;}
   // 1.49.0 史诗恩赐
   if(h.boons.Count>0){
    var r=new Rect(0f,y,w,24f);
    Widgets.Label(r,"史诗恩赐："+string.Join("、",h.boons.Select(b=>b==Epic.Might && h.mightAbility>=0?b.label+"（"+Adventurer.Names[h.mightAbility]+"）":b.label)));
    var sb=new System.Text.StringBuilder();foreach(var b in h.boons)sb.AppendLine(b.LabelCap+"："+b.description);
    if(h.HasBoon(Epic.Fate)){int left=h.fateTick+Epic.FateCooldownDays*GenDate.TicksPerDay-Find.TickManager.TicksGame;sb.AppendLine(left<=0?"命运之恩：可用":"命运之恩：还要 "+(left/(float)GenDate.TicksPerDay).ToString("0.#")+" 天");}
    Widgets.DrawHighlightIfMouseover(r);TooltipHandler.TipRegion(r,sb.ToString().TrimEnd());y+=Row;
   }
   // 1.50.0 传奇属性点：就地 +1 / +5
   y+=4f;
   if(h.legendary || h.legendPoints>0 || h.legendSpent.Sum()>0){
    var r=new Rect(0f,y,w,24f);
    if(h.legendary)GUI.color=Gold;Widgets.Label(r,"传奇属性点：余 "+h.legendPoints+(Legend.On?"":"（“传奇等级”已关闭，暂时没有效果）"));GUI.color=Color.white;
    Widgets.DrawHighlightIfMouseover(r);if(Mouse.IsOver(r))TooltipHandler.TipRegion(r,Legend.Summary(h));y+=Row;
    float cw=w/2f;
    for(int i=0;i<Legend.Count;i++){
     var cell=new Rect(cw*(i%2),y+(i/2)*27f,cw-4f,26f);if((i/2)%2==0)Widgets.DrawLightHighlight(cell);
     int n=h.legendSpent[i];float k=Affinity.LegendMult(h,i);string lt=Affinity.LegendTip(h,i);
     var lab=new Rect(cell.x+4f,cell.y+3f,cell.width-100f,24f);
     if(n<=0)GUI.color=Color.gray;Widgets.Label(lab,Legend.Names[i]+" "+n+Affinity.LegendTag(h,i));GUI.color=Color.white;
     if(Mouse.IsOver(lab))TooltipHandler.TipRegion(lab,Legend.Names[i]+" "+n+"："+Legend.EffectText(i,n,k)+"\n下一点："+Legend.EffectText(i,n+1,k)+(lt==""?"":"\n"+lt));
     if(own && h.legendPoints>0){
      var b1=new Rect(cell.xMax-92f,cell.y+1f,44f,24f);if(Mouse.IsOver(b1))TooltipHandler.TipRegion(b1,"投 1 点后："+Legend.EffectText(i,n+1,k));
      if(Widgets.ButtonText(b1,"+1"))h.SpendLegend(i,1);
      if(h.legendPoints>=5){var b5=new Rect(cell.xMax-46f,cell.y+1f,44f,24f);if(Mouse.IsOver(b5))TooltipHandler.TipRegion(b5,"投 5 点后："+Legend.EffectText(i,n+5,k));if(Widgets.ButtonText(b5,"+5"))h.SpendLegend(i,5);}
     }
    }
    y+=((Legend.Count+1)/2)*27f+2f;
   }
   else Grey(ref y,w,Legend.On?"传奇属性点在成为传奇冒险者后解锁":"传奇属性点：“传奇等级”已在设置里关闭");
   // 1.48.1 专长：图标 + 名字 + 每日剩余次数，悬停看说明。
   Widgets.Label(new Rect(0f,y,w,24f),"专长");y+=24f;
   if(h.feats.Count==0)Grey(ref y,w,"暂无专长（可以用一次属性提升改选专长）");
   else{
    for(int i=0;i<h.feats.Count;i++){
     var f=h.feats[i];var cell=new Rect((i%2)*(w/2f),y+(i/2)*30f,w/2f-4f,28f);
     GUI.DrawTexture(new Rect(cell.x,cell.y+2f,24f,24f),f.Icon);
     int left=h.UsesLeft(f);
     Widgets.Label(new Rect(cell.x+28f,cell.y+3f,cell.width-28f,24f),f.LabelCap+(h.originFeat==f?"（起源）":"")+(left>=0?"（今天 "+left+"/"+h.UsesPerDay(f)+"）":""));
     Widgets.DrawHighlightIfMouseover(cell);if(Mouse.IsOver(cell))TooltipHandler.TipRegion(cell,f.LabelCap+"\n\n"+f.description+(h.featScore.TryGetValue(f.defName,out int fa)?"\n\n附带："+Adventurer.Names[fa]+" +1":"")+"\n\n前置："+f.PrereqText()+(left>=0?"\n每天 "+h.UsesPerDay(f)+" 次，午夜恢复。":""));
    }
    y+=((h.feats.Count+1)/2)*30f;
   }
   y+=4f;
   if(p!=armedFor){armed=null;armedFor=p;stylePickSlot=-1;}
   // 热修2：可选专长（有完整的属性提升时）。一行一个：图标、名字、效果、前置；不满足的灰色并写原因；“选择”点两次确认。
   featPickY=y;
   if(own && h.FeatPicks>0){
    GUI.color=Gold;Widgets.Label(new Rect(0f,y,w,24f),h.fighterFeats>0?"可选专长（战士奖励专长，免费，剩 "+h.fighterFeats+" 个"+(h.pendingASI>0?"；之后消耗属性提升，剩 "+h.pendingASI+" 次":"")+"）":"可选专长（消耗一次属性提升，剩 "+h.pendingASI+" 次）");GUI.color=Color.white;y+=26f;
    foreach(var f in Feats.All){
     if(!Feats2.Visible(f,p))continue; // 1.58.1 种族专长只对对应种族显示
     var row=new Rect(0f,y,w,54f);string why=h.CanTakeFeat(f);bool ok=why==null;
     Widgets.DrawLightHighlight(row);
     GUI.DrawTexture(new Rect(row.x+4f,row.y+9f,36f,36f),f.Icon);
     if(!ok)GUI.color=Color.gray;
     float tw=row.width-140f;
     Widgets.Label(new Rect(row.x+46f,row.y+1f,tw,22f),f.LabelCap+(h.UsesPerDay(f)>0?"（每天 "+h.UsesPerDay(f)+" 次）":"")+(f.BonusOptions.Count>0?"　"+(f.BonusOptions.Count==6?"任意属性":string.Join("/",f.BonusOptions.Select(x=>Adventurer.Names[x])))+" +1":""));
     Text.Font=GameFont.Tiny;
     Widgets.Label(new Rect(row.x+46f,row.y+21f,tw,16f),FeatEffect(f).Truncate(tw));
     if(ok)GUI.color=new Color(.75f,.75f,.75f);else GUI.color=new Color(1f,.6f,.5f);
     Widgets.Label(new Rect(row.x+46f,row.y+36f,tw,16f),("前置："+f.PrereqText()+(ok?"":"　— "+why)).Truncate(tw));
     Text.Font=GameFont.Small;GUI.color=Color.white;
     {var tr=new Rect(row.x,row.y,row.width-90f,row.height);if(Mouse.IsOver(tr))TooltipHandler.TipRegion(tr,f.LabelCap+"\n\n"+f.description+"\n\n前置："+f.PrereqText()+(ok?"":"\n不能选："+why));}
     string key=FeatKey(f);var b=new Rect(row.xMax-84f,row.y+13f,80f,28f);
     if(ok && armed==key)TooltipHandler.TipRegion(b,"再点一次确认："+(h.fighterFeats>0?"用一个战士奖励专长":"消耗一次属性提升")+"换“"+f.label+"”。");
     if(Widgets.ButtonText(b,h.HasFeat(f)?"已有":armed==key?"确定？":"选择",active:ok) && ok)PickFeat(h,f,ref armed);
     y+=56f;
    }
    y+=4f;
   }
   else if(own && h.asiLeft>0)Grey(ref y,w,"拆开的属性点不能换专长：需要一次完整的属性提升");
   y=DrawClassFeatures(y,w,p,h,own); // 1.53.0 职业特性
   // 热修2：史诗恩赐（有恩赐点时）。威能之恩直接点要加的那一项。
   boonPickY=y;
   if(own && h.boonPoints>0){
    GUI.color=Gold;Widgets.Label(new Rect(0f,y,w,24f),"史诗恩赐（剩 "+h.boonPoints+" 个恩赐点，每个恩赐只能选一次）");GUI.color=Color.white;y+=26f;
    foreach(var bd in Epic.Boons){
     bool might=bd==Epic.Might;var row=new Rect(0f,y,w,might?70f:46f);string why=h.CanTakeBoon(bd);bool ok=why==null;
     Widgets.DrawLightHighlight(row);
     if(!ok)GUI.color=Color.gray;
     float tw=might?row.width-12f:row.width-100f;
     Widgets.Label(new Rect(row.x+6f,row.y+1f,tw,22f),bd.LabelCap+(ok?"":"　— "+why));
     Text.Font=GameFont.Tiny;Widgets.Label(new Rect(row.x+6f,row.y+22f,tw,18f),bd.description.Truncate(tw));Text.Font=GameFont.Small;
     GUI.color=Color.white;
     TooltipHandler.TipRegion(new Rect(row.x,row.y,tw,44f),bd.LabelCap+"\n\n"+bd.description);
     if(might){
      float bw=(row.width-12f)/6f;
      for(int a=0;a<6;a++){
       string key=BoonKey(bd,a);var b=new Rect(row.x+6f+a*bw,row.y+42f,bw-4f,24f);
       if(ok)TooltipHandler.TipRegion(b,armed==key?"再点一次确认":Adventurer.Names[a]+" "+h.scores[a]+" → "+Math.Min(h.scores[a]+Epic.MightBonus,h.Cap(a)+Epic.MightBonus)+"，上限 +"+Epic.MightBonus);
       if(Widgets.ButtonText(b,armed==key?"确定？":Adventurer.Names[a]+" +"+Epic.MightBonus,active:ok) && ok)PickBoon(h,bd,a,ref armed);
      }
     }else{
      string key=BoonKey(bd,-1);var b=new Rect(row.xMax-90f,row.y+9f,84f,28f);
      if(ok && armed==key)TooltipHandler.TipRegion(b,"再点一次确认：消耗一个恩赐点换“"+bd.label+"”。");
      if(Widgets.ButtonText(b,h.HasBoon(bd)?"已有":armed==key?"确定？":"选择",active:ok) && ok)PickBoon(h,bd,-1,ref armed);
     }
     y+=row.height+2f;
    }
    y+=4f;
   }
   // 1.51.0 种族亲和与天赋
   {var r=new Rect(0f,y,w,24f);var t=h.Talent;
    Widgets.Label(r,"种族："+h.Affinity.label+"（"+h.Affinity.Tier+"）"+(Affinity.On?"":"，亲和已关闭")+"　天赋："+(t!=null?t.degreeDatas[0].label:"无"));
    Widgets.DrawHighlightIfMouseover(r);if(Mouse.IsOver(r))TooltipHandler.TipRegion(r,"种族亲和 — "+h.Affinity.Summary+(Affinity.On?"":"\n（“种族亲和”已在设置里关闭）")+(t!=null?"\n\n"+(h.pawn.story?.traits?.GetTrait(t)?.TipString(h.pawn)??t.degreeDatas[0].label):""));y+=Row;}
   if(h.LegendCapBonus>0){Widgets.Label(new Rect(0f,y,w,24f),"传奇 Lv"+h.level+"：所有属性上限 +"+h.LegendCapBonus+"（每 5 个传奇等级 +2）");y+=Row;}
   if(h.nemesis){GUI.color=new Color(1f,.5f,.4f);Widgets.Label(new Rect(0f,y,w,24f),"宿敌冠军");GUI.color=Color.white;y+=Row;}
   if(Adventurer.VPELoaded){
    var r=new Rect(0f,y,w,24f);
    Widgets.Label(r,"VPE 额外路径点 +"+h.vpeGranted+(h.vpeBanked>0?"（另有 "+h.vpeBanked+" 点等觉醒灵能后发放）":""));
    TooltipHandler.TipRegion(r,"每个冒险者等级给 1 点 VPE 路径点（不加 VPE 灵能等级；传奇等级每 2 级 1 点）。还没有 VPE 灵能的人先攒着，第一次获得心灵链接时一次发放。\nDND 职业路径：对应技能 6 级，或主属性 13 即可解锁；职业强度另加 0.05 × 主属性调整值 + 0.02 ×（熟练加值 − 2）。");
    y+=Row;
   }
   if(h.milestonesDone.Count>0){var r=new Rect(0f,y,w,24f);Widgets.Label(r,"里程碑："+h.milestonesDone.Count+" 个");if(Mouse.IsOver(r))TooltipHandler.TipRegion(r,Tip());y+=Row;}
   if(AdventurerEnemies.On && h.storyLevel)Grey(ref y,w,"剧情等级");
   return y;
  }
  // 1.59.0 超凡：列出已获得的项（当前生效的金色 / 神话紫色，暂停的灰色），徽记在 Textures/LP/Exalted/，缺图时画文字徽记。
  static readonly Color Mythic=new Color(.78f,.55f,1f),ExaltedOff=new Color(.6f,.6f,.6f);
  static readonly string[] ExaltedKeys={"Str","Dex","Con","Int","Wis","Cha"};
  static readonly Texture2D[] exaltedIcons=new Texture2D[12];static bool exaltedIconsLoaded;
  static Texture2D ExaltedIcon(int a,bool mythic){
   if(!exaltedIconsLoaded){exaltedIconsLoaded=true;for(int i=0;i<12;i++)exaltedIcons[i]=ContentFinder<Texture2D>.Get("LP/Exalted/"+ExaltedKeys[i%6]+(i>=6?"_Mythic":""),false);}
   return exaltedIcons[a+(mythic?6:0)];
  }
  float DrawExalted(float y,float w,Hediff_Adventurer h){
   // 热修1：有效值达标就显示（不看开关）；效果没生效时整项灰色，标题和格子悬停写原因。
   int raw=Exalted.Raw(h),ever=h.exaltedSent|h.exaltedMask|raw&63;if(ever==0)return y;
   string off=Exalted.OffReason;
   {var hr=new Rect(0f,y,w,24f);if(off!=null)GUI.color=Color.gray;Widgets.Label(hr,"超凡"+(off==null?"":"（效果未生效）"));GUI.color=Color.white;
    if(off!=null){Widgets.DrawHighlightIfMouseover(hr);TooltipHandler.TipRegion(hr,off);}}
   y+=24f;
   int n=0;
   for(int a=0;a<6;a++){
    if((ever&1<<a)==0)continue;
    bool on=(h.exaltedMask&1<<a)!=0,reached=(raw&1<<a)!=0,my=on?(h.mythicMask&1<<a)!=0:reached && (raw&1<<(a+6))!=0;
    var cell=new Rect((n%3)*(w/3f),y+(n/3)*30f,w/3f-4f,28f);n++;
    var icon=ExaltedIcon(a,my);var ir=new Rect(cell.x,cell.y+2f,24f,24f);
    if(!on)GUI.color=new Color(.5f,.5f,.5f);
    if(icon!=null)GUI.DrawTexture(ir,icon);
    else{Widgets.DrawBoxSolid(ir,my?new Color(.35f,.2f,.5f):new Color(.45f,.35f,.1f));Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(ir,Adventurer.Names[a].Substring(0,1));Text.Anchor=TextAnchor.UpperLeft;}
    GUI.color=on?(my?Mythic:Gold):Color.gray;
    Widgets.Label(new Rect(cell.x+28f,cell.y+3f,cell.width-28f,24f),(my?"神话·"+Exalted.MythicNames[a]:"超凡·"+Exalted.ExaltedNames[a])+(on?"":reached?"（未生效）":"（暂停）"));GUI.color=Color.white;
    Widgets.DrawHighlightIfMouseover(cell);
    if(Mouse.IsOver(cell))TooltipHandler.TipRegion(cell,(on?"":reached?"未生效："+(off??"属性刚变化，稍后生效")+"\n\n":"暂停：当前"+Adventurer.Names[a]+" "+h.Eff(a)+" 低于 20，回到 20 恢复。\n\n")+Exalted.Describe(a,false)+(my?"\n\n"+Exalted.Describe(a,true):"\n\n神话条件：属性 24，或 20 + 传奇冒险者 + "+Legend.Names[Exalted.LegendPairs[a][0]]+"与"+Legend.Names[Exalted.LegendPairs[a][1]]+"传奇点合计 20（现在 "+(h.legendSpent[Exalted.LegendPairs[a][0]]+h.legendSpent[Exalted.LegendPairs[a][1]])+"）。"));
   }
   return y+((n+2)/3)*30f+2f;
  }
  // 1.81.0 热修5：展开的效果表。来源一行（种族擅长 / 天赋），下面两列网格：效果名左对齐、数值右对齐，Tiny 字体，块下面一条分隔线。
  readonly HashSet<int> expanded=new HashSet<int>();
  const float FxRowH=18f,FxValW=64f;
  static float DrawEffectTable(float y,float w,Hediff_Adventurer h,int a){
   Text.Font=GameFont.Tiny;float x0=24f,cw=(w-x0-8f)/2f;
   var rows=h.EffectRows(a);
   if(rows==null){
    GUI.color=Color.gray;string why=Adventurer.EffectsHandedToIsekai?"属性效果已交给 Isekai（设置里可以打开）。":Adventurer.EffectScale<=0f?"属性效果已在设置里关闭。":"传奇单位：六维不提供数值效果（强度已经算在兵种里）。";
    Widgets.Label(new Rect(x0,y,w-x0,FxRowH),why);GUI.color=Color.white;y+=FxRowH;
   }else{
    string src=Affinity.SourceTag(h,a);
    if(src!=""){GUI.color=Gold;Widgets.Label(new Rect(x0,y,w-x0,FxRowH),"（"+src+"）");GUI.color=Color.white;y+=FxRowH;}
    for(int k=0;k<rows.Count;k++){
     var cell=new Rect(x0+cw*(k%2),y+FxRowH*(k/2),cw-8f,FxRowH);
     GUI.color=new Color(.85f,.85f,.85f);Widgets.Label(new Rect(cell.x+4f,cell.y,cell.width-FxValW-4f,FxRowH),rows[k].Key);
     GUI.color=Color.white;Text.Anchor=TextAnchor.UpperRight;Widgets.Label(new Rect(cell.xMax-FxValW,cell.y,FxValW,FxRowH),rows[k].Value);Text.Anchor=TextAnchor.UpperLeft;
    }
    y+=FxRowH*((rows.Count+1)/2);
   }
   Text.Font=GameFont.Small;GUI.color=new Color(1f,1f,1f,.25f);Widgets.DrawLineHorizontal(x0,y+2f,w-x0-4f);GUI.color=Color.white;
   return y+6f;
  }
  static void Grey(ref float y,float w,string text){GUI.color=Color.gray;Widgets.Label(new Rect(4f,y,w-4f,24f),text);GUI.color=Color.white;y+=Row;}

  // 1.53.0 职业特性：额外攻击 / 斗志 / VPE 等级（桥给的行）+ 战斗风格槽位；空槽或点了“改选”的槽下面列出可选风格（两次点击确认）。
  static readonly string[] SlotMarks={"①","②"};
  float DrawClassFeatures(float y,float w,Pawn p,Hediff_Adventurer h,bool own){
   var lines=ClassLinesHook?.Invoke(p);int slots=Styles.Slots(p);
   bool npcStyles=!own && h.styles.Count>0;
   if(ClassLinesHook==null && !npcStyles)return y;
   Widgets.Label(new Rect(0f,y,w,24f),"职业特性");y+=24f;
   if((lines==null || lines.Count==0) && slots<=0 && !npcStyles){Grey(ref y,w,"职业特性：学习战士、圣武士或武僧（“职业”页）后出现");return y+4f;}
   if(lines!=null)foreach(var line in lines){
    int bar=line.IndexOf('|');string text=bar<0?line:line.Substring(0,bar),tip=bar<0?null:line.Substring(bar+1);
    var r=new Rect(4f,y,w-4f,24f);Widgets.Label(r,text);if(tip!=null){Widgets.DrawHighlightIfMouseover(r);TooltipHandler.TipRegion(r,tip);}y+=Row;
   }
   // 风格槽位
   int shown=npcStyles?h.styles.Count:Styles.MaxSlots;
   if(own && slots<=0 && h.styles.Count==0){Grey(ref y,w,"战斗风格：学习物理职业后可选 1 个");return y+4f;}
   {
    var row=new Rect(0f,y,w,28f);float x=4f;
    Widgets.Label(new Rect(x,y+3f,70f,24f),"战斗风格");x+=72f;
    float cw=(w-x)/Math.Max(1,shown);
    for(int i=0;i<shown;i++){
     var cell=new Rect(x+i*cw,y,cw-6f,28f);
     string mark=i<SlotMarks.Length?SlotMarks[i]:(i+1).ToString();
     if(i<h.styles.Count){
      var s=h.styles[i];bool adapting=Styles.Adapting(h,i);bool live=(h.styleMask & s.Bit)!=0;
      GUI.DrawTexture(new Rect(cell.x+18f,cell.y+2f,24f,24f),s.Icon);
      if(!live)GUI.color=Color.gray;
      Widgets.Label(new Rect(cell.x,cell.y+3f,18f,24f),mark);
      float bw=own && i<slots?54f:0f;
      Widgets.Label(new Rect(cell.x+46f,cell.y+3f,cell.width-46f-bw,24f),(s.LabelCap+(adapting?"（适应中，剩 "+Styles.AdaptDaysLeft(h).ToString("0.#")+" 天）":"")).Truncate(cell.width-46f-bw));
      GUI.color=Color.white;
      TooltipHandler.TipRegion(new Rect(cell.x,cell.y,cell.width-bw,cell.height),s.LabelCap+"\n\n"+s.description+(adapting?"\n\n改选后 3 天生效，适应期间这一槽没有效果。":"")+(i>=Styles.Limit(h)?"\n\n这个槽位现在没有解锁，风格不生效。":""));
      if(bw>0f){var b=new Rect(cell.xMax-bw,cell.y+2f,bw,24f);if(Widgets.ButtonText(b,stylePickSlot==i?"收起":"改选")){stylePickSlot=stylePickSlot==i?-1:i;armed=null;}TooltipHandler.TipRegion(b,"换成另一个风格：新风格 3 天（180000 刻）后生效，期间这一槽没有效果。第一次选不用等。");}
     }else if(i<slots){
      Widgets.Label(new Rect(cell.x,cell.y+3f,cell.width,24f),mark+" （空，在下面选）");
     }else{
      GUI.color=Color.gray;Widgets.Label(new Rect(cell.x,cell.y+3f,cell.width,24f),mark+" （"+(i==0?"学习物理职业":"战士 10 级")+"解锁）");GUI.color=Color.white;
     }
    }
    y+=30f;
   }
   // 可选风格：空槽自动列出；点了“改选”的槽也列出。
   int pick=-1;
   if(own){if(stylePickSlot>=0 && stylePickSlot<h.styles.Count && stylePickSlot<slots)pick=stylePickSlot;else if(h.styles.Count<slots)pick=h.styles.Count;}
   if(pick>=0){
    GUI.color=Gold;Widgets.Label(new Rect(0f,y,w,24f),"可选战斗风格（槽位 "+(pick<SlotMarks.Length?SlotMarks[pick]:(pick+1).ToString())+(pick<h.styles.Count?"，改选后 3 天生效":"，第一次选立即生效")+"）");GUI.color=Color.white;y+=26f;
    foreach(var s in Styles.All){
     var row=new Rect(0f,y,w,46f);string why=Styles.CanPick(h,pick,s);bool ok=why==null;
     Widgets.DrawLightHighlight(row);
     GUI.DrawTexture(new Rect(row.x+4f,row.y+7f,32f,32f),s.Icon);
     if(!ok)GUI.color=Color.gray;
     float tw=row.width-140f;
     Widgets.Label(new Rect(row.x+42f,row.y+1f,tw,22f),s.LabelCap+(ok?"":"　— "+why));
     Text.Font=GameFont.Tiny;Widgets.Label(new Rect(row.x+42f,row.y+22f,tw,20f),(s.description??"").Truncate(tw));Text.Font=GameFont.Small;
     GUI.color=Color.white;
     TooltipHandler.TipRegion(new Rect(row.x,row.y,row.width-90f,row.height),s.LabelCap+"\n\n"+s.description);
     string key=StyleKey(pick,s);var b=new Rect(row.xMax-84f,row.y+9f,80f,28f);
     if(ok && armed==key)TooltipHandler.TipRegion(b,"再点一次确认："+(pick<h.styles.Count?"改选为“"+s.label+"”（3 天后生效）。":"选“"+s.label+"”。"));
     if(Widgets.ButtonText(b,armed==key?"确定？":"选择",active:ok) && ok && PickStyle(h,pick,s,ref armed))stylePickSlot=-1;
     y+=48f;
    }
   }
   return y+4f;
  }
 }

 // 升级信件：同一刻升级的人合在一封里；有待加点 / 恩赐 / 传奇属性点的殖民者有一个按钮，选中他并打开冒险者分页（热修2：不再弹单独的窗口）。
 public class Letter_AdventurerLevel : ChoiceLetter {
  public List<Pawn> pawns=new List<Pawn>();
  public static string Pending(Hediff_Adventurer h){
   var parts=new List<string>();
   if(h.buyOpen)parts.Add("初始属性分配"); // 1.76.0
   if(h.AvailablePoints>0)parts.Add("属性点 "+h.AvailablePoints);
   if(h.fighterFeats>0)parts.Add("战士奖励专长 "+h.fighterFeats); // 2026-10-31
   if(h.boonPoints>0)parts.Add("史诗恩赐 "+h.boonPoints);
   if(h.legendPoints>0)parts.Add("传奇属性点 "+h.legendPoints);
   if(ClassSystem.Points(h)>0)parts.Add("职业点 "+ClassSystem.Points(h)); // 1.72.0
   if(ClassLegendary.Points(h)>0)parts.Add("传奇职业点 "+ClassLegendary.Points(h)); // 1.74.0
   return string.Join("、",parts);
  }
  public override IEnumerable<DiaOption> Choices{get{
   if(!ArchivedOnly)foreach(var p in pawns){
    var h=Adventurer.Get(p);if(h==null || p.Dead || p.Faction==null || !p.Faction.IsPlayer)continue;
    var pp=p;
    if(ClassSystem.Points(h)>0 || ClassLegendary.Points(h)>0)yield return new DiaOption("给"+p.LabelShort+"选择职业（"+(ClassSystem.Points(h)>0?"职业点 "+ClassSystem.Points(h):"传奇职业点 "+ClassLegendary.Points(h))+"）"){action=()=>Find.WindowStack.Add(new Dialog_ChooseClass(pp)),resolveTree=true}; // 1.72.0：窗口，不用浮动菜单
    string pend=Pending(h);if(pend=="")continue;
    yield return new DiaOption("打开"+p.LabelShort+"的冒险者分页（"+pend+"）"){action=()=>ITab_Adventurer.OpenFor(pp),resolveTree=true};
   }
   if(lookTargets!=null && lookTargets.IsValid())yield return Option_JumpToLocation;
   yield return Option_Close;
  }}
  public override void ExposeData(){base.ExposeData();ScribePawns.Look(ref pawns,"lpPawns");}
 }
}
