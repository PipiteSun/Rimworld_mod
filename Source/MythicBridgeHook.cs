using System;
using System.Reflection;
using Verse;

namespace LichPhylactery {
 // 1.21.0：神兽血裔联动检查在独立的联动程序集里（Bridges/MythicBloodlines），主程序集只按名字反射调用，不引用它。
 public partial class LichChecks {
  const string MythicId="local.standalone.mythicbloodlines";
  static Type MythicBridgeChecks=>GenTypes.GetTypeInAnyAssembly("LichPhylactery.MythicBridge.BridgeChecks");
  void CheckMythicBridge(){
   bool mythic=ModsConfig.IsActive(MythicId);
   Check((MythicBridgeChecks!=null)==mythic && (DefDatabase<HediffDef>.GetNamedSilentFail("LPMB_TrollBloodRegen")!=null)==mythic,"Mythic Bloodlines bridge loads exactly when that mod is active ("+mythic+")");
   RunMythicBridge("Run");
  }
  void CheckMythicBridgeAfterLoad()=>RunMythicBridge("AfterLoad");
  static void RunMythicBridge(string method){
   var type=MythicBridgeChecks;if(type==null)return;
   try{checks+=(int)type.GetMethod(method).Invoke(null,null);}
   catch(TargetInvocationException e){throw e.InnerException??e;}
  }
 }
}
