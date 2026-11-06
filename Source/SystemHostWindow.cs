using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.122.0 系统面板：照搬百战觉醒 MilestoneWindow 的布局（840×710，强制暂停，关闭可暂存、候选不重抽，确认对话框，领取记录滚动区）。
 // 上方：当前任务进度条，或「接受任务」三选一；下方三个奖励框：属性成长 / 改命符 / 随机物品。
 public sealed class SystemHostWindow : Window {
  readonly Pawn pawn;Vector2 scroll;
  public override Vector2 InitialSize=>new Vector2(840f,710f);
  public SystemHostWindow(Pawn pawn){
   this.pawn=pawn;forcePause=true;absorbInputAroundWindow=true;doCloseX=true;doCloseButton=true;closeOnClickedOutside=false;
  }
  public override void DoWindowContents(Rect rect){
   var st=SystemHost.State;
   if(pawn==null || st==null){Close();return;}
   var e=st.Lookup(pawn)??(SystemHost.Has(pawn)?st.For(pawn):null);
   if(e==null){Close();return;}
   bool host=SystemHost.Has(pawn);
   Text.Font=GameFont.Medium;
   Widgets.Label(new Rect(0,0,rect.width-30,36),pawn.LabelShortCap+" · 系统宿主");
   Text.Font=GameFont.Small;
   int now=Find.TickManager.TicksGame;
   string next=!host?"没有“系统宿主”特性，不再发布任务":e.active!=null?"任务进行中，完成或结束后再发布":e.nextOfferTick<0?"即将发布":"下次发布：约 "+((e.nextOfferTick-now)/(float)GenDate.TicksPerDay).ToString("0.0")+" 天后";
   Widgets.Label(new Rect(0,44,rect.width,28),$"已完成任务 {e.completed} 次    超时 {e.failed} 次    放弃 {e.abandoned} 次    已领奖 {e.claimed} 次    {next}");
   // 任务区（78–150）
   if(e.active!=null){
    var t=e.active;float done=SystemHost.Done(pawn,t);
    var bar=new Rect(0,78,rect.width-132,25);
    Widgets.FillableBar(bar,Mathf.Clamp01(done/Mathf.Max(1,t.target)));
    Text.Anchor=TextAnchor.MiddleCenter;
    Widgets.Label(bar,$"当前任务：{t.Label}    {Mathf.FloorToInt(Mathf.Min(done,t.target))} / {t.target}    剩余 {Mathf.Max(0f,(t.deadline-now)/(float)GenDate.TicksPerDay):0.0} 天");
    Text.Anchor=TextAnchor.UpperLeft;
    if(Widgets.ButtonText(new Rect(rect.width-120,76,120,29),"放弃任务"))
     Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation($"确认放弃任务「{t.Label}」？主动放弃没有惩罚，但这次任务不会再有奖励。",()=>SystemHost.Abandon(pawn),true));
    Widgets.Label(new Rect(0,110,rect.width,40),"进度按接受任务后此人的记录统计（击杀、制作、治疗、收获、研究、俘获）。期限内完成即可领取一次奖励；超时会受到 3 天系统惩罚（全局工作速度 ×0.85）。");
   } else if(host && e.offers.Count>0){
    float w=(rect.width-24)/3;
    for(int i=0;i<e.offers.Count && i<3;i++){
     var t=e.offers[i];var box=new Rect(i*(w+12),78,w,62);
     Widgets.DrawMenuSection(box);
     Widgets.Label(new Rect(box.x+8,box.y+4,box.width-16,24),t.Label);
     if(Widgets.ButtonText(new Rect(box.x+8,box.y+30,box.width-16,26),"接受任务")){
      var cap=t;
      Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation($"确认接受任务「{cap.Label}」？期限 7 天，超时会受到 3 天系统惩罚；另外两个任务将放弃。",()=>{
       if(!SystemHost.Accept(pawn,cap,out var err))Messages.Message(err,MessageTypeDefOf.RejectInput,false);}));
     }
    }
    Widgets.Label(new Rect(0,146,rect.width,24),"三个任务选一个接受，也可以不接（下次发布时换一批）。关闭面板不会重抽。");
   } else {
    Widgets.Label(new Rect(0,82,rect.width,60),host?"现在没有可接受的任务。系统每 7 天发布一次任务。":"此人已经没有“系统宿主”特性：不再发布任务，但已经完成的任务奖励仍可领取。");
   }
   // 奖励区
   var offer=e.rewards.Count>0?e.rewards[0]:null;
   Widgets.Label(new Rect(0,174,rect.width,24),$"待领取 {e.rewards.Count} 次。每次三选一：属性点在“冒险者”分页分配，物品放入背包。");
   float bw=(rect.width-24)/3;
   var adv=Adventurer.Enabled?Adventurer.Get(pawn):null;
   DrawChoice(new Rect(0,200,bw,170),"属性成长","+1 属性点",
    adv!=null?"加入冒险者的可分配属性点（和升级得到的属性点在同一处分配），可用于力量、敏捷、体质、智力、感知或魅力。":"此人没有冒险者等级（冒险者系统关闭或未满 13 岁），此项不可选。",
    offer,0,offer!=null && adv!=null);
   DrawChoice(new Rect(bw+12,200,bw,170),"改命符","改命符 ×1",
    "放入背包。使用时把自己或同一处另一名殖民者的一个特性改写成同一档位的另一个特性（诅咒只换诅咒，事迹与基因特性不能改）。",
    offer,1,offer!=null && SystemHost.CharmDef!=null);
   var item=DefDatabase<ThingDef>.GetNamedSilentFail(offer?.item??"");
   DrawChoice(new Rect((bw+12)*2,200,bw,170),"随机物品",
    item==null?(offer==null?"完成任务后揭晓":"暂无可用物品"):item.LabelCap+" ×"+offer.count,
    item?.description??(SystemHost.UsesIsekaiPool?"魔核、强化核心、魔力精华或星辰碎片，随机一种。":"治疗药水、强效治疗药水、英雄药水、位面残渣或秘银，随机一种。"),
    offer,2,offer!=null && item!=null);
   bool wasEnabled=GUI.enabled;GUI.enabled=wasEnabled && adv!=null;
   if(Widgets.ButtonText(new Rect(0,380,180,30),"打开冒险者分页")){Close();ITab_Adventurer.OpenFor(pawn);}
   GUI.enabled=wasEnabled;
   Widgets.Label(new Rect(192,382,rect.width-192,28),offer!=null?$"当前领取：{offer.Label}":host?"完成系统任务可获得下一次奖励。":"没有“系统宿主”特性，不再有新的奖励。");
   // 记录
   var view=new Rect(0,420,rect.width,Math.Max(30,rect.height-470));
   var content=new Rect(0,0,view.width-20,40+e.history.Count*27);
   Widgets.BeginScrollView(view,ref scroll,content);
   float y=0;
   Widgets.Label(new Rect(0,y,content.width,27),"系统记录（任务与领奖，保留 60 条）");y+=30;
   foreach(var line in e.history){Widgets.Label(new Rect(0,y,content.width,27),line);y+=27;}
   Widgets.EndScrollView();
  }
  void DrawChoice(Rect box,string title,string reward,string description,HostReward offer,int choice,bool enabled){
   Widgets.DrawMenuSection(box);
   var inner=box.ContractedBy(12);
   Widgets.Label(new Rect(inner.x,inner.y,inner.width,28),title);
   Widgets.Label(new Rect(inner.x,inner.y+34,inner.width,50),reward);
   TooltipHandler.TipRegion(box,description);
   Widgets.Label(new Rect(inner.x,inner.y+88,inner.width,24),"悬停查看详情");
   bool wasEnabled=GUI.enabled;
   GUI.enabled=wasEnabled && enabled && SystemHost.Eligible(pawn);
   if(Widgets.ButtonText(new Rect(inner.x,box.yMax-45,inner.width,30),"选择此奖励")){
    // 记下这一份奖励：旧窗口不能领到下一份。
    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
     $"确认领取{offer.Label}，选择「{title}：{reward}」？本次另外两项奖励将放弃。",
     ()=>{if(!SystemHost.Claim(pawn,offer,choice,out var error))Messages.Message(error,MessageTypeDefOf.RejectInput,false);}));
   }
   GUI.enabled=wasEnabled;
  }
 }
}
