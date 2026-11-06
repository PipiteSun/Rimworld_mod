using System;
using HarmonyLib;
namespace LichPhylactery {
 // 战棺（VFE 海盗）类型。1.115.0 热修起火巨人不再被挡在战棺铸造厂外（原来的 5 个拦截补丁已删），体型适配见 WarcasketFit.cs。
 public static class FireGiantWarcasket {
  public static Type Foundry=>AccessTools.TypeByName("VFEPirates.Building_WarcasketFoundry");
  public static Type Driver=>AccessTools.TypeByName("VFEPirates.JobDriver_EntombIn");
  public static Type Project=>AccessTools.TypeByName("VFEPirates.WarcasketProject");
 }
}
