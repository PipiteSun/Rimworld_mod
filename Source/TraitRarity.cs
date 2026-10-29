using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 2026-10-29 特性稀有度（报告 ArtSource/TraitRarity-1.66.0-report.md）：所有 DND 特性挂 TraitRarityExt，
 // 六档 普通 / 优秀 / 稀有 / 史诗 / 传说 / 神话 = 无色 / 绿 / 蓝 / 紫 / 橙 / 金；诅咒（红）、事迹（青）是性质标记，档位写在悬停说明第一行。
 // 名字颜色直接写在 XML 标签里（ArtSource/trait_rarity.py 按同一张表生成，-Only TraitRarity 检查两边一致），这里只加悬停标头。
 public class TraitRarityExt:DefModExtension {
  public string tier="Common",kind="DND 特性",mark;
  public List<string> degrees; // "程度:档位" 或 "程度:档位:标记"（标记 None = 去掉诅咒 / 事迹标记），如光谱两端、安息之魂
  Dictionary<int,KeyValuePair<int,string>> parsed;
  public KeyValuePair<int,string> At(int degree){
   if(parsed==null){
    parsed=new Dictionary<int,KeyValuePair<int,string>>();
    if(degrees!=null)foreach(var s in degrees){var a=s.Split(':');if(a.Length<2 || !int.TryParse(a[0],out int g))continue;parsed[g]=new KeyValuePair<int,string>(TraitRarity.Index(a[1]),a.Length>2?(a[2]=="None"?null:a[2]):mark);}
   }
   return parsed.TryGetValue(degree,out var v)?v:new KeyValuePair<int,string>(TraitRarity.Index(tier),mark);
  }
 }
 public static class TraitRarity {
  public static readonly string[] Keys={"Common","Uncommon","Rare","Epic","Legendary","Mythic"};
  public static readonly string[] Names={"普通","优秀","稀有","史诗","传说","神话"};
  public static readonly string[] Hexes={null,"#7FBF6A","#5B9BD5","#B070E0","#E08A3A","#E0B040"};
  public const string CurseHex="#D05050",DeedHex="#50C0C0";
  public static int Index(string key){int i=Array.IndexOf(Keys,key);return i<0?0:i;}
  public static TraitRarityExt Ext(TraitDef d)=>d?.GetModExtension<TraitRarityExt>();
  public static int Tier(TraitDef d,int degree){var e=Ext(d);return e==null?-1:e.At(degree).Key;}
  public static string Mark(TraitDef d,int degree)=>Ext(d)?.At(degree).Value;
  // 名字该用的颜色：诅咒红、事迹青，其他按档位；普通档 null（原版白字）。
  public static string Hex(TraitDef d,int degree){var e=Ext(d);if(e==null)return null;var a=e.At(degree);return a.Value=="Curse"?CurseHex:a.Value=="Deed"?DeedHex:Hexes[a.Key];}
  static readonly Dictionary<int,string> headers=new Dictionary<int,string>();
  public static string Header(TraitDef d,int degree){
   var e=Ext(d);if(e==null)return null;int key=d.index*64+degree+32;
   if(headers.TryGetValue(key,out var h))return h;
   var a=e.At(degree);string tn=Names[a.Key];
   string text=a.Value=="Curse"?"〔诅咒 · "+tn+"〕":a.Value=="Deed"?"〔事迹 · "+tn+"〕":"〔"+tn+" · "+e.kind+(e.mark=="Curse"?"（已解除）":"")+"〕";
   var hex=Hex(d,degree);h=hex==null?text:"<color="+hex+">"+text+"</color>";
   headers[key]=h;return h;
  }
 }
 [HarmonyPatch(typeof(Trait),nameof(Trait.TipString))]
 public static class TraitRarityTip {
  static void Postfix(Trait __instance,ref string __result){
   if(__instance?.def==null || __result==null)return;var h=TraitRarity.Header(__instance.def,__instance.Degree);if(h!=null)__result=h+"\n"+__result;
  }
 }
}
