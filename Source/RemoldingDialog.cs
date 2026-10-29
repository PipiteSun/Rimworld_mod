using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 public partial class Dialog_ConstructDesign {
  readonly Building_RemoldingPool pool;
  bool IsRemolding=>pool!=null;
  bool Editable=>finished==null || IsRemolding;
  public Dialog_ConstructDesign(Hediff_Construct h,Building_RemoldingPool pool){finished=h;kind=h.kind;this.pool=pool;plan=h.remoldDraft?.Copy()??Remolding.Current(h);Setup();}
  string RemoldDetails(){var cost=Remolding.Cost(finished,plan);return "本次消耗（含50生肉重塑费）：\n生肉 "+cost.meat+" / 钢铁 "+cost.steel+" / 玻璃钢 "+cost.plasteel+"\n重塑时间：1个游戏日\n统御占用："+finished.Cost+"（不变）\n保留技能经验，不重赠初始等级。\n降低投入不返还材料，伤势比例保留。";}
  void DrawRemoldFooter(Rect rect,float bottom){
   var supplies=Remolding.Supplies(pool);string reason=pool.Blocked(finished,plan,supplies);
   Widgets.Label(new Rect(0,bottom-1,rect.width,30),reason??("池边可用：生肉 "+supplies.Where(ConstructSeed.RawMeat).Sum(t=>t.stackCount)+" / 钢铁 "+supplies.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount)+" / 玻璃钢 "+supplies.Where(t=>t.def==ThingDefOf.Plasteel).Sum(t=>t.stackCount)));
   if(Widgets.ButtonText(new Rect(0,bottom+34,120,34),"恢复当前"))plan=Remolding.Current(finished);
   if(Widgets.ButtonText(new Rect(134,bottom+34,130,34),"保存方案")){finished.remoldDraft=plan.Copy();Close();}
   Widgets.Label(new Rect(278,bottom+32,rect.width-470,44),"入池时扣料；入池后取消不退款。");
   GUI.enabled=reason==null;if(Widgets.ButtonText(new Rect(rect.width-175,bottom+34,175,34),"安排入池重塑")){if(pool.Order(finished,plan,out var error))Close();else Messages.Message(error,MessageTypeDefOf.RejectInput,false);}GUI.enabled=true;
  }
 }
}
