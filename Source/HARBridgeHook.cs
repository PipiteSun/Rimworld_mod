using System;
using System.Reflection;
using Verse;

namespace LichPhylactery {
 // 1.33.0：HAR 联动检查在独立的联动程序集里（Bridges/HAR），主程序集只按名字反射调用，不引用 AlienRace.dll。
 public partial class LichChecks {
  public const string HARId="erdelf.humanoidalienraces";
  static Type HARBridgeChecks=>GenTypes.GetTypeInAnyAssembly("LichPhylactery.HARBridge.BridgeChecks");
  static void RunHARBridge(){
   var type=HARBridgeChecks;if(type==null)return;
   try{checks+=(int)type.GetMethod("Run").Invoke(null,null);}
   catch(TargetInvocationException e){if(e.InnerException==null)throw;System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
  }
 }
}
