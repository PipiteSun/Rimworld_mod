using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.95.0：Test.ps1 -Only EnemyRaces（敌人专用异种不进建角下拉，敌人照常生成）。-Full 时按玩家的整套模组列出名单。
 public partial class LichChecks {
  void CheckEnemyRaces(){
   EnemyRaces.Reset();
   var enemy=EnemyRaces.All.OrderBy(x=>x.defName).ToList();
   foreach(var x in enemy)Log.Message("[LICH-TEST] enemy-only xenotype: "+x.defName+" "+x.label+" ("+(x.modContentPack?.Name??"?")+") <- "+EnemyRaces.Factions(x));
   // 1. 判定：每个都只出现在永久敌对派系，没有自由人权重，也没有剧本点名；可玩的、契约种族、原版基准人都不算
   // 狂猎骑手 / 首领是点名的例外（契约种族里只有它们连立约者开局也不列）
   var bad=enemy.Where(x=>!EnemyRaces.Always.Contains(x.defName) && (x==XenotypeDefOf.Baseliner || x.factionlessGenerationWeight>0f || PactStart.IsPactRace(x) || x.defName.StartsWith("LP_"))).Select(x=>x.defName).ToList();
   bad.AddRange(EnemyRaces.Always.Where(n=>DefDatabase<XenotypeDef>.GetNamedSilentFail(n) is XenotypeDef x && !EnemyRaces.IsEnemyOnly(x)).Select(n=>"missing "+n));
   var keep=new[]{"Baseliner","LP_MindFlayer","LP_Titan","LP_Genie","LP_Troll","LP_Goblin","LP_Drow","LP_FireGiant","LP_Githyanki"}.Where(n=>DefDatabase<XenotypeDef>.GetNamedSilentFail(n) is XenotypeDef x && EnemyRaces.IsEnemyOnly(x)).ToList();
   Check(bad.Count==0 && keep.Count==0,"enemy-only xenotypes: "+enemy.Count+" ("+string.Join(",",enemy.Select(x=>x.defName))+")"+(bad.Count>0?" wrong "+string.Join(",",bad):"")+(keep.Count>0?" playable flagged "+string.Join(",",keep):""));

   // 2. 建角下拉不列敌人专用异种（普通剧本和立约者剧本都一样），关掉过滤后又都回来
   bool forced=PactStart.TestForce;
   try{
    PactStart.TestForce=false;var normal=PactStart.Choosable().ToList();
    PactStart.TestForce=true;var pact=PactStart.Choosable().ToList();
    EnemyRaces.TestDisable=true;var off=PactStart.Choosable().ToList();EnemyRaces.TestDisable=false;
    Check(!normal.Any(EnemyRaces.IsEnemyOnly) && !pact.Any(EnemyRaces.IsEnemyOnly) && enemy.All(off.Contains) && normal.Contains(XenotypeDefOf.Baseliner),"start screen hides "+enemy.Count+" enemy-only xenotypes ("+normal.Count+" normal / "+pact.Count+" pact start)");
   }finally{PactStart.TestForce=forced;EnemyRaces.TestDisable=false;}

   // 3. 敌人生成不受影响：每个敌人专用异种所在的派系兵种照样能生出它
   var miss=new List<string>();
   foreach(var x in enemy.Take(6)){
    var kind=DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(k=>k.RaceProps.Humanlike && k.xenotypeSet!=null && k.xenotypeSet.Contains(x));
    var fac=Find.FactionManager.AllFactions.FirstOrDefault(f=>f.def.permanentEnemy && f.def.xenotypeSet!=null && f.def.xenotypeSet.Contains(x)) ?? Find.FactionManager.AllFactions.FirstOrDefault(f=>f.def.permanentEnemy && f.def.humanlikeFaction && !f.IsPlayer);
    Pawn p=null;
    try{
     p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind??fac?.def.basicMemberKind??PawnKindDefOf.Villager,fac,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:x));
     if(p?.genes?.Xenotype!=x)miss.Add(x.defName);
    }catch(Exception e){miss.Add(x.defName+":"+e.GetType().Name);}
    finally{if(p!=null && !p.Destroyed)p.Destroy();}
   }
   Check(miss.Count==0,"enemy-only xenotypes still generate for their factions ("+Math.Min(6,enemy.Count)+" tried)"+(miss.Count>0?": "+string.Join(",",miss):""));
  }
 }
}
