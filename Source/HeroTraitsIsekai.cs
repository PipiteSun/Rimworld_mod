using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.58.0 与 Isekai RPG Leveling 的互斥（规格第一节第 7 条）。整个特性对整个特性的互斥写在 Bridges/Isekai/Patches/IsekaiHeroTraits.xml（只在 Isekai 启用时加载，双方 conflictingTraits 都写）；
 // “天赋·某项 × 对应的白色属性特性”是按 degree 的，原版 conflictingTraits 做不到，所以在这里：生成时不抽冲突的那一支，GainTrait 时拦掉后来的那一个。
 public static class HeroIsekai {
  // 力 / 敏 / 体 / 智 / 感 / 魅 → 力大、敏捷、坚韧、聪慧、开悟、巧舌。
  public static readonly string[] StatTraits={"Isekai_Mighty","Isekai_Agile","Isekai_Resilient","Isekai_Brilliant","Isekai_Enlightened","Isekai_SilverTongue"};
  public static int StatIndex(TraitDef d)=>d==null?-1:System.Array.IndexOf(StatTraits,d.defName);
  public static bool StatTraitBlocks(Pawn p,int a){
   if(a<0 || a>5 || p?.story?.traits==null)return false;var l=p.story.traits.allTraits;
   for(int k=0;k<l.Count;k++)if(l[k].def.defName==StatTraits[a])return true;
   return false;
  }
  public static int Blocked; // 自检计数
  // 拦截：要加的是天赋·X 而身上有对应的 Isekai 白色特性，或者反过来。
  public static bool Blocks(Pawn p,Trait t){
   if(p?.story?.traits==null || t?.def==null)return false;
   if(t.def==HeroDefOf.LP_GiftedScore)return StatTraitBlocks(p,t.Degree-1);
   int a=StatIndex(t.def);if(a<0)return false;
   var g=p.story.traits.GetTrait(HeroDefOf.LP_GiftedScore);return g!=null && g.Degree-1==a;
  }
 }
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.GainTrait))]
 public static class HeroIsekaiGain {
  static bool Prefix(Pawn ___pawn,Trait trait){
   if(trait?.def==null || trait.def!=HeroDefOf.LP_GiftedScore && !trait.def.defName.StartsWith("Isekai_"))return true;
   if(!HeroIsekai.Blocks(___pawn,trait))return true;
   HeroIsekai.Blocked++;return false;
  }
 }
}
