using System;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.76.0 初始属性购点：Test.ps1 -Only Adventurer 的一部分，由 CheckAdventurer 在同一个 try 里调用。
 public partial class LichChecks {
  void CheckAdventurerPointBuy(){
   var s=LichMod.Settings;bool oldOn=s.adventurerPointBuy;int oldPts=s.adventurerPointBuyPoints;
   try{
    s.adventurerPointBuy=true;s.adventurerPointBuyPoints=27;
    // 1. 新殖民者：预填标准数组（27 点用完），分配开着；敌人只记基础值
    var a=AdvPawn();var h=Adventurer.Get(a);var gen=(int[])h.scores.Clone();
    Check(h.buyOpen && h.buyBase!=null && h.buyBase.OrderByDescending(x=>x).SequenceEqual(Adventurer.StandardArray) && Adventurer.BuyCost(h.buyBase)==27 && h.BuyLeft==0,"1.76.0 point buy: a new colonist starts with the recommended standard array open for reassignment ("+Join(h.buyBase)+", left "+h.BuyLeft+")");
    var e=AdvPawn(null,AdvEnemy);var he=Adventurer.Get(e);
    Check(he!=null && he.buyBase!=null && !he.buyOpen,"enemies keep the base array but get no allocation");
    // 2. 全部 8：属性按差值下降，购点全部退回；种族 / 背景加值保留
    var bonus=Enumerable.Range(0,6).Select(i=>gen[i]-h.buyBase[i]).ToArray();
    h.BuyReset();
    Check(h.buyBase.All(x=>x==8) && h.BuyLeft==27 && Enumerable.Range(0,6).All(i=>h.scores[i]==8+bonus[i]),"reset: every base 8, 27 points back, racial/background bonus kept ("+Join(h.scores)+")");
    // 3. 费用表与边界
    bool down=h.Buy(Adventurer.Str,-1);int c=0;while(h.Buy(Adventurer.Str,1))c++;
    Check(!down && h.CanBuy(Adventurer.Str,-1)==null && c==7 && h.buyBase[Adventurer.Str]==15 && h.BuyLeft==18 && h.scores[Adventurer.Str]==15+bonus[Adventurer.Str] && h.CanBuy(Adventurer.Str,1).Contains("最高"),"Str 8 → 15 costs 9 (18 left); 7 and 16 refused");
    while(h.Buy(Adventurer.Dex,1)){}while(h.Buy(Adventurer.Con,1)){}
    string why=h.CanBuy(Adventurer.Int,1);
    Check(h.buyBase[Adventurer.Dex]==15 && h.buyBase[Adventurer.Con]==15 && h.BuyLeft==0 && why!=null && why.Contains("购点不够"),"three 15s use all 27 points; another +1 is refused ("+why+")");
    // 4. 推荐：回到生成时的分配
    h.BuyRecommend();
    Check(h.scores.SequenceEqual(gen) && h.BuyLeft==0,"recommend restores the generated scores ("+Join(h.scores)+")");
    // 5. 属性提升加过的点不受影响
    h.scores[Adventurer.Wis]+=2;int wisBase=h.buyBase[Adventurer.Wis];h.BuyReset();
    Check(h.scores[Adventurer.Wis]==8+bonus[Adventurer.Wis]+2,"reallocating keeps points from ability score improvements (Wis base "+wisBase+" → 8, +2 kept)");
    h.scores[Adventurer.Wis]-=2;h.BuyRecommend();
    // 6. 确认点两次，之后锁定
    string armed=null;bool first=ITab_Adventurer.PressConfirmBuy(h,ref armed);bool stillOpen=h.buyOpen;bool second=ITab_Adventurer.PressConfirmBuy(h,ref armed);
    Check(!first && stillOpen && second && !h.buyOpen && h.buyBase==null && !h.Buy(Adventurer.Str,1) && !Adventurer.OpenPointBuy(a) && h.scores.SequenceEqual(gen),"confirm takes two clicks, then the allocation is locked for good");
    // 7. 招募：加入玩家阵营后打开；设置关闭时不打开
    var r=AdvPawn(null,AdvEnemy);var hr=Adventurer.Get(r);r.SetFaction(Faction.OfPlayer);
    s.adventurerPointBuy=false;bool offOpen=Adventurer.OpenPointBuy(r);s.adventurerPointBuy=true;bool onOpen=Adventurer.OpenPointBuy(r);
    Check(!offOpen && onOpen && hr.buyOpen && Letter_AdventurerLevel.Pending(hr).Contains("初始属性分配") && GameComponent_Adventurer.BuyNoteText(new[]{r}).Contains(r.LabelShort),"a recruited pawn gets the allocation when it joins (not with the setting off); letter text and pending list mention it");
    // 8. 购点设置少于 27：打开时按推荐值从高到低买到用完
    s.adventurerPointBuyPoints=20;var low=AdvPawn();var hl=Adventurer.Get(low);
    Check(hl.buyOpen && hl.BuyLeft>=0 && Adventurer.BuyCost(hl.buyBase)<=20 && hl.buyBase.Max()==15,"budget 20: the prefill is trimmed to fit ("+Join(hl.buyBase)+", left "+hl.BuyLeft+")");
    s.adventurerPointBuyPoints=27;
    // 9. 老存档的小人（没有基础值）没有分配
    var old=AdvPawn();var ho=Adventurer.Get(old);ho.buyBase=null;ho.buyOpen=false;
    Check(!Adventurer.OpenPointBuy(old) && ho.CanBuy(0,1)!=null,"pawns without a stored base array (pre-1.76.0) get no allocation");
   }finally{s.adventurerPointBuy=oldOn;s.adventurerPointBuyPoints=oldPts;}
  }
 }
}
