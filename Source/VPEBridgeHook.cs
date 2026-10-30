using System;
using System.Reflection;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.27.0：VPE 职业路径联动检查在独立的联动程序集里（Bridges/VPE），主程序集只按名字反射调用，不引用它、VEF.dll 或 VanillaPsycastsExpanded.dll。
 public partial class LichChecks {
  const string VPEId="vanillaexpanded.vpsycastse";
  static bool ClassesOnly=>GenCommandLine.CommandLineArgPassed("lichclassesonly");
  static Type VPEBridgeChecks=>GenTypes.GetTypeInAnyAssembly("LichPhylactery.VPEBridge.BridgeChecks");
  void CheckVPEBridge(){
   bool vpe=ModsConfig.IsActive(VPEId);
   Check((VPEBridgeChecks!=null)==vpe && (DefDatabase<IncidentDef>.GetNamedSilentFail("LP_WanderingMentor")!=null)==vpe,"VPE class bridge loads exactly when Vanilla Psycasts Expanded is active ("+vpe+")");
   RunVPEBridge("Run");
  }
  // 1.53.0：-VPE -Only <组名> 只跑 VPE 桥里的这些检查组（BridgeChecks 的 <组名>Checks / <组名>PrepareSave / <组名>AfterLoad）。
  void CheckVPEBridgeAfterLoad(){if(OnlyMode)RunVPEBridge("AfterLoadOnly",OnlyArg);else RunVPEBridge("AfterLoad");}
  // -lichclassesonly：只跑职业检查和存读档（Test.ps1 -VPE）。
  void RunVPEBridgeOnly(){map=Find.CurrentMap;if(OnlyMode){Check(VPEBridgeChecks!=null,"VPE class bridge is loaded for -VPE -Only");RunVPEBridge("RunOnly",OnlyArg);}else CheckVPEBridge();}
  static void RunVPEBridge(string method,string arg=null){
   var type=VPEBridgeChecks;if(type==null)return;
   try{checks+=(int)type.GetMethod(method).Invoke(null,arg==null?null:new object[]{arg});}
   catch(TargetInvocationException e){if(e.InnerException==null)throw;System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
  }
 }
}
