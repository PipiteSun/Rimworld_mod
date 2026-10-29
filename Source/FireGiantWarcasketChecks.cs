using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 public partial class LichChecks {
  void CheckFireGiantWarcasket(Pawn giant){
   if(FireGiantWarcasket.Foundry==null){Check(!GenCommandLine.CommandLineArgPassed("lichwarcasket"),"warcasket compatibility is optional without Pirates");return;}
   var type=FireGiantWarcasket.Foundry;
   var foundry=(Building)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("VFEP_WarcasketFoundry"));
   foundry.SetFactionDirect(Faction.OfPlayer);
   var normal=Person();
   var reason=AccessTools.Method(type,"CannotUseNowReason");
   var register=AccessTools.Method(type,"RegisterOccupant");
   var occupant=AccessTools.Field(type,"occupant");
   Check((string)reason.Invoke(foundry,new object[]{giant})==FireGiantWarcasket.Reason,"fire giant cannot enter foundry and gets explicit reason before power checks");
   register.Invoke(foundry,new object[]{giant});Check(occupant.GetValue(foundry)==null,"direct foundry registration rejects fire giant");
   register.Invoke(foundry,new object[]{normal});Check(occupant.GetValue(foundry)==normal,"ordinary pawn can still register as foundry occupant");occupant.SetValue(foundry,null);
   var gear=giant.apparel.WornApparel.ToList();var health=giant.health.hediffSet.hediffs.ToList();
   var project=Activator.CreateInstance(FireGiantWarcasket.Project);
   AccessTools.Method(FireGiantWarcasket.Project,"ApplyOn").Invoke(project,new object[]{giant});
   Check(gear.SequenceEqual(giant.apparel.WornApparel) && health.SequenceEqual(giant.health.hediffSet.hediffs),"blocked final warcasket installation preserves armor and anatomy");
   var driver=(JobDriver)Activator.CreateInstance(FireGiantWarcasket.Driver);driver.pawn=giant;driver.job=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("VFEP_EntombIn"),foundry);
   Check(!driver.TryMakePreToilReservations(false),"queued or forced entomb job cannot reserve for fire giant");
   int caskets=0;
   foreach(var def in DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.GetType().FullName=="VFEPirates.WarcasketDef" && d.IsApparel)){
    Check(!ApparelUtility.HasPartsToWear(giant,def),"fire giant rejects warcasket component including extensions: "+def.defName);caskets++;
   }
   Check(caskets>0,"warcasket component compatibility exercised");
   normal.Destroy();foundry.Destroy();
  }
 }
}
