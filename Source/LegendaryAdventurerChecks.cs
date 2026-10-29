using RimWorld;
using Verse;
namespace LichPhylactery {
 // 2026-10-28：Test.ps1 -Only LegendaryAdventurer：传奇冒险者每 5 级 1 次传奇抗性。
 public partial class LichChecks {
  void CheckLegendaryAdventurer(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oSup=Adventurer.TestSuppressed;
   Adventurer.TestSuppressed=false;s.adventurer=true;
   try{
    // 1. 传奇 25 级 → 5 次；名字显示 5/5；不算传奇单位
    var p=HtPawn(spawn:true);var h=GrAdv(p);h.legendary=true;h.level=25;Legendary.SyncAdventurer(h);
    var r=Legendary.Resistance(p);
    Check(r!=null && r.Max==5 && r.charges==5 && r.LabelBase=="传奇抗性 5/5" && !Legendary.Is(p) && Legendary.CanResist(p),"legendary adventurer level 25 has legendary resistance 5/5 without becoming a legendary unit: "+r?.LabelBase);

    // 2. 眩晕被拦并扣 1 次；外力精神状态也拦
    p.stances.stunner.StunFor(120,null,false);bool stunBlocked=!p.stances.stunner.Stunned;
    bool berserk=p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk,"测试",true,false,false,null,true);
    Check(stunBlocked && !berserk && r.charges==3,"stun and a forced berserk are each blocked, charges 5 → "+r.charges);

    // 3. 升到 30 级：上限 6，多出来的 1 次马上可用
    h.level=30;r.Sync();
    Check(r.Max==6 && r.charges==4,"level 30 raises the cap to 6 and adds the new charge now: "+r.charges+"/"+r.Max);

    // 4. 非传奇的 25 级、传奇 4 级都没有；失去传奇身份会移除
    var q=HtPawn(spawn:true,dx:2);var hq=GrAdv(q);hq.level=25;Legendary.SyncAdventurer(hq);
    var low=HtPawn(spawn:true,dx:4);var hl=GrAdv(low);hl.legendary=true;hl.level=4;Legendary.SyncAdventurer(hl);
    h.legendary=false;Legendary.SyncAdventurer(h);
    Check(Legendary.Resistance(q)==null && Legendary.Resistance(low)==null && Legendary.Resistance(p)==null && !Legendary.CanResist(q),"non-legendary level 25, legendary level 4 and a pawn that lost legendary status have no legendary resistance");
   }finally{
    s.adventurer=oOn;Adventurer.TestSuppressed=oSup;
    foreach(var x in htMade){if(x.Dead && x.Corpse!=null && !x.Corpse.Destroyed)x.Corpse.Destroy();else if(!x.Destroyed)x.Destroy();}htMade.Clear();
   }
  }
 }
}
