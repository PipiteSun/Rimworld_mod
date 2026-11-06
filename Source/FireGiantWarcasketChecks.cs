using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 public partial class LichChecks {
  // 1.115.0 热修起火巨人可以装战棺（原来是拦截）：铸造厂不再给火巨人专门的拒绝理由，能登记为使用者，所有战棺件都能穿。
  void CheckFireGiantWarcasket(Pawn giant){
   if(FireGiantWarcasket.Foundry==null){Check(!GenCommandLine.CommandLineArgPassed("lichwarcasket"),"warcasket compatibility is optional without Pirates");return;}
   var type=FireGiantWarcasket.Foundry;
   var foundry=(Building)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("VFEP_WarcasketFoundry"));
   foundry.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(foundry,map.Center+new IntVec3(20,0,20),map,WipeMode.Vanish); // 没生成的铸造厂没有电力组件，CannotUseNowReason 会空引用
   var normal=Person();
   string rg=(string)AccessTools.Method(type,"CannotUseNowReason").Invoke(foundry,new object[]{giant}),rn=(string)AccessTools.Method(type,"CannotUseNowReason").Invoke(foundry,new object[]{normal});
   var register=AccessTools.Method(type,"RegisterOccupant");var occupant=AccessTools.Field(type,"occupant");
   register.Invoke(foundry,new object[]{giant});bool reg=occupant.GetValue(foundry)==giant;occupant.SetValue(foundry,null);
   var defs=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>WarcasketFit.IsCasket(d) && ApparelUtility.HasPartsToWear(normal,d)).ToList(); // 普通人也穿不了的（战棺内衬 Bodysuit）不算
   Check(rg==rn && reg && defs.Count>0 && defs.All(d=>ApparelUtility.HasPartsToWear(giant,d) && GiantGear.CanWear(giant,d)),"fire giant can use the warcasket foundry (reason giant "+rg+" / normal "+rn+", registered "+reg+", blocked: "+string.Join(",",defs.Where(d=>!ApparelUtility.HasPartsToWear(giant,d) || !GiantGear.CanWear(giant,d)).Select(d=>d.defName))+") and wears all "+defs.Count+" warcasket parts");
   normal.Destroy();foundry.Destroy();
  }
 }
}
