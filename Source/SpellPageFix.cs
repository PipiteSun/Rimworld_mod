using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.136.0 热修（Peter：黑网商队来了，和商人交互打不开交易界面）。
 // 原因：法术页的 ThingDef 叫 LP_WS_SpellPage1 / 2 / 3，以数字结尾。原版存档里物品 ID = defName + 编号，读档时把尾部所有数字当编号：
 // SpellPage1 的 110 号存成「LP_WS_SpellPage1110」，读回来成了 1110 号（和别的物品撞号，或者超过 int 范围），对象登记出错，地图的物品清单里留下没生成在地图上的物品，
 // 交易窗口统计殖民地可卖物品时碰到它就空引用（Pawn_TraderTracker.ColonyThingsWillingToBuy → Fogged）。黑网 / 异域商队带着法术页来，最容易触发。
 // 修法：defName 改成 LP_WS_SpellPageA / B / C；旧存档的 def 名自动换新，旧 ID 去掉多出来的那一位环位数字后再读编号；读档后清掉地图物品清单里没生成的残留。
 public static class SpellPageFix {
  public const string Prefix="LP_WS_SpellPage";
  public static readonly HashSet<int> Seen=new HashSet<int>();public static int Renumbered;
  public static string NewName(string old){if(old==null || old.Length!=Prefix.Length+1 || !old.StartsWith(Prefix))return null;char c=old[Prefix.Length];return c=='1'?Prefix+"A":c=='2'?Prefix+"B":c=='3'?Prefix+"C":null;}
  // 旧 ID「LP_WS_SpellPage」+ 环位数字 + 编号 → 编号。新 ID（A / B / C 结尾）不归这里管。
  public static bool OldId(string id,out int number){
   number=0;if(id==null || id.Length<=Prefix.Length+1 || !id.StartsWith(Prefix))return false;
   char c=id[Prefix.Length];if(c<'1' || c>'3')return false;
   return int.TryParse(id.Substring(Prefix.Length+1),out number);
  }
  // 地图物品清单里没生成在这张地图上的残留（读档撞号留下的）。
  public static int CleanListers(Map map){
   if(map?.listerThings==null)return 0;int n=0;
   foreach(var t in map.listerThings.AllThings.ToList())if(t==null || t.Destroyed || !t.Spawned || t.Map!=map){map.listerThings.Remove(t);n++;}
   return n;
  }
 }
 [HarmonyPatch(typeof(BackCompatibility),nameof(BackCompatibility.BackCompatibleDefName))]
 public static class SpellPageDefName {
  static void Postfix(Type defType,string defName,ref string __result){if(defType==typeof(ThingDef)){var to=SpellPageFix.NewName(defName);if(to!=null)__result=to;}}
 }
 [HarmonyPatch]
 public static class SpellPageOldId {
  static MethodBase TargetMethod(){
   var m=AccessTools.Method(typeof(Thing),"IDNumberFromThingID");if(m!=null)return m;
   foreach(var t in typeof(Thing).Assembly.GetTypes()){var x=t.GetMethod("IDNumberFromThingID",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);if(x!=null)return x;}
   return null;
  }
  static bool Prepare()=>TargetMethod()!=null;
  // 旧 ID 按正确编号读；同一次读档里第二次出现的同号法术页（旧版存档时已经撞号存了两份）换一个新编号，不再登记冲突。
  static bool Prefix(string __0,ref int __result){
   bool old=SpellPageFix.OldId(__0,out int n);
   if(!old && !(__0!=null && __0.StartsWith(SpellPageFix.Prefix) && __0.Length>SpellPageFix.Prefix.Length+1 && int.TryParse(__0.Substring(SpellPageFix.Prefix.Length+1),out n)))return true;
   if(Scribe.mode==LoadSaveMode.LoadingVars && Current.Game?.uniqueIDsManager!=null && !SpellPageFix.Seen.Add(n)){__result=Current.Game.uniqueIDsManager.GetNextThingID();SpellPageFix.Renumbered++;return false;}
   __result=n;return false;
  }
 }
 // 旧版撞号时存下来的「已销毁、数量 0」的法术页：读档生成前从地图的待生成列表里拿掉（否则每次读档都报「Spawning destroyed thing」）。
 [HarmonyPatch(typeof(Map),nameof(Map.ExposeData))]
 public static class SpellPageDeadLoad {
  static readonly FieldInfo F=AccessTools.Field(typeof(Map),"loadedFullThings");
  static void Postfix(Map __instance){
   if(Scribe.mode!=LoadSaveMode.PostLoadInit || F==null)return;
   if(!(F.GetValue(__instance) is List<Thing> list))return;
   int n=list.RemoveAll(t=>t!=null && t.def!=null && t.def.defName.StartsWith(SpellPageFix.Prefix) && t.stackCount<=0);
   if(n>0)Log.Message("[DND边缘大陆] 读档修复：丢掉 "+n+" 张数量为 0 的旧法术页残留。");
  }
 }
 public class GameComponent_SpellPageFix : GameComponent {
  public GameComponent_SpellPageFix(Game g){}
  public override void FinalizeInit(){
   if(SpellPageFix.Renumbered>0)Log.Message("[DND边缘大陆] 读档修复：给 "+SpellPageFix.Renumbered+" 张撞号的旧法术页换了新编号。");SpellPageFix.Seen.Clear();SpellPageFix.Renumbered=0;
   int n=0;foreach(var m in Find.Maps)n+=SpellPageFix.CleanListers(m);
   if(n>0)Log.Message("[DND边缘大陆] 读档修复：清掉地图物品清单里 "+n+" 个没生成在地图上的残留（旧版法术页撞号留下的）。");
  }
 }
}
