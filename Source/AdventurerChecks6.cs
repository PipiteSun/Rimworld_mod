using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.52.0 热修：Test.ps1 -Only Adventurer 的冒险者分页部分（尺寸、经验文字、就地加点、熟练加值效果）。由 CheckAdventurer 在同一个 try 里调用。
 public partial class LichChecks {
  void CheckAdventurerStage6(){
   // 1. 分页尺寸：宽 630，高 = 屏幕高 − 300，限制在 220～480
   var cur=ITab_Adventurer.SizeFor(UI.screenHeight);
   Check(Near(ITab_Adventurer.HeightFor(1080f),480f) && Near(ITab_Adventurer.HeightFor(700f),400f) && Near(ITab_Adventurer.HeightFor(400f),220f) && Near(cur.x,630f) && cur.y>=220f && cur.y<=480f,"冒险者 tab is 630 wide, height screen − 300 clamped 220–480 (1080 → 480, 700 → 400, 400 → 220; now "+cur.x+"×"+cur.y+")");
   // 2. 经验文字：1000 以上一位小数 k；20 级非传奇显示已满
   var a=AdvPawn();var h=Adventurer.Get(a);h.level=7;h.xp=9000f;h.legendary=false;
   string t7=ITab_Adventurer.XpText(h);
   h.level=20;h.xp=130000f;string t20=ITab_Adventurer.XpText(h);string tip=ITab_Adventurer.XpSourcesTip(h);
   h.level=2;h.xp=150f;string t2=ITab_Adventurer.XpText(h);
   Check(t7=="经验 9.0k / 11.3k" && t20=="已满，需成为传奇冒险者" && Near(ITab_Adventurer.XpFrac(h),.25f) && t2=="经验 150 / 300" && tip.Contains("击杀") && tip.Contains("守住袭击") && tip.Contains("传送门") && tip.Contains("当前经验倍率"),"XP text: level 7 at 9000 → \""+t7+"\", level 20 non-legend → \""+t20+"\", level 2 → \""+t2+"\"; the bar tooltip lists the sources");
   // 3. 就地加点：+1 扣一点（拆开一次属性提升），不超过上限 20；剩下的点存档前后一致
   h.level=4;h.scores=new[]{19,10,10,10,10,10};h.pendingASI=1;h.asiLeft=0;h.Dirty();
   bool one=h.SpendPoints(Adventurer.Str,1);int afterOne=h.AvailablePoints;
   bool over=h.SpendPoints(Adventurer.Str,1);
   bool other=h.SpendPoints(Adventurer.Dex,1);
   bool empty=h.SpendPoints(Adventurer.Con,1);
   Check(one && afterOne==1 && h.pendingASI==0 && !over && h.scores[Adventurer.Str]==20 && other && h.scores[Adventurer.Dex]==11 && h.AvailablePoints==0 && !empty && h.scores[Adventurer.Con]==10,"in-place +1: 2 points per improvement, Str 19 → 20 then refused at the cap, the second point goes to Dex, then nothing left");
   h.pendingASI=1;h.scores=new[]{12,16,13,10,10,8};h.SpendPoints(Adventurer.Int,1);h.AutoAssign();
   Check(h.asiLeft==0 && h.pendingASI==0 && h.scores[Adventurer.Int]==11 && h.scores.Sum()==12+16+13+10+10+8+2,"auto-assign spends the leftover split point ("+Join(h.scores)+")");
   // 4. 熟练加值：9 级 +4 → 近战命中 +4%（最终命中率补丁）、射击精度 ×1.02；1 级没有
   h.level=9;h.scores=new[]{10,10,10,10,10,10};h.Dirty();float hit9=Adventurer.MeleeHitBonus(a),aim9=h.BuildStage(1f).statFactors?.GetStatFactorFromList(StatDefOf.ShootingAccuracyPawn)??1f;
   h.level=1;h.Dirty();float hit1=Adventurer.MeleeHitBonus(a);
   var patch=Harmony.GetPatchInfo(AccessTools.Method(typeof(Verb_MeleeAttack),"GetNonMissChance"));
   bool patched=patch!=null && patch.Postfixes.Any(x=>x.PatchMethod.DeclaringType==typeof(AdventurerMeleeHit));
   h.level=9;string letter=GameComponent_Adventurer.LevelGains(a,h,8,9);
   Check(Near(hit9,.04f) && Near(aim9,1.02f) && Near(hit1,0f) && patched && letter.Contains("熟练加值 +3 → +4") && letter.Contains("近战命中 +4%") && Adventurer.ProficiencyText(h).Contains("近战命中"),"proficiency +4 at level 9: melee hit +4% on the final chance (patched "+patched+"), shooting accuracy ×"+aim9.ToString("0.###")+"; level-up letter lists it");
   CheckAdventurerTabPicks();
  }
  // 热修2：分页里选专长 / 恩赐（“选择”点两次确认）。
  void CheckAdventurerTabPicks(){
   var a=AdvPawn();var h=Adventurer.Get(a);
   foreach(var f in h.feats.ToList())h.RemoveFeat(f);
   h.level=8;h.scores=new[]{10,10,10,10,10,10};h.pendingASI=1;h.asiLeft=0;h.Dirty();
   string armed=null;
   bool heavyBlocked=h.CanTakeFeat(Feats.HeavyArmor)!=null;
   bool heavy1=ITab_Adventurer.PickFeat(h,Feats.HeavyArmor,ref armed),heavy2=ITab_Adventurer.PickFeat(h,Feats.HeavyArmor,ref armed);
   bool first=ITab_Adventurer.PickFeat(h,Feats.Alert,ref armed);int afterFirst=h.pendingASI;string armedKey=armed;
   bool second=ITab_Adventurer.PickFeat(h,Feats.Alert,ref armed);
   bool again=ITab_Adventurer.PickFeat(h,Feats.Lucky,ref armed) || ITab_Adventurer.PickFeat(h,Feats.Lucky,ref armed);
   Check(heavyBlocked && !heavy1 && !heavy2 && !first && afterFirst==1 && armedKey==ITab_Adventurer.FeatKey(Feats.Alert) && second && h.HasFeat(Feats.Alert) && h.pendingASI==0 && armed==null && !again && !h.HasFeat(Feats.Lucky) && ITab_Adventurer.FeatEffect(Feats.Alert).StartsWith("被偷袭"),"tab feat pick: heavy armor master refused at Str 10, alert needs a second click and then uses the improvement, nothing left for a second feat");
   h.boons.Clear();h.mightAbility=-1;h.boonPoints=2;h.scores=new[]{16,10,10,10,10,10};h.Dirty();armed=null;
   bool m1=ITab_Adventurer.PickBoon(h,Epic.Might,Adventurer.Str,ref armed);
   bool mSwitch=ITab_Adventurer.PickBoon(h,Epic.Might,Adventurer.Dex,ref armed); // 换了一项：重新待确认
   bool m2=ITab_Adventurer.PickBoon(h,Epic.Might,Adventurer.Dex,ref armed);
   bool s1=ITab_Adventurer.PickBoon(h,Epic.Speed,-1,ref armed),s2=ITab_Adventurer.PickBoon(h,Epic.Speed,-1,ref armed);
   bool none=ITab_Adventurer.PickBoon(h,Epic.Fate,-1,ref armed) || ITab_Adventurer.PickBoon(h,Epic.Fate,-1,ref armed);
   Check(!m1 && !mSwitch && m2 && h.mightAbility==Adventurer.Dex && h.scores[Adventurer.Dex]==14 && h.scores[Adventurer.Str]==16 && !s1 && s2 && h.HasBoon(Epic.Speed) && h.boonPoints==0 && !none && !h.HasBoon(Epic.Fate),"tab boon pick: might boon on the confirmed score only (Dex 10 → 14), speed after two clicks, no points left for a third");
   h.boons.Clear();h.mightAbility=-1;foreach(var f in h.feats.ToList())h.RemoveFeat(f);h.Dirty();
  }
 }
}
