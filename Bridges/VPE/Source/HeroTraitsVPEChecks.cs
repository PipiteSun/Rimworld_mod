using System.Linq;
using RimWorld;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;

namespace LichPhylactery.VPEBridge {
 // 1.58.0 补测（-VPE -Only HeroTraits）：魔网之子的 VPE 灵能熵 / 冷却、神之勇士的路径经验、魔网崩溃豁免。
 public static partial class BridgeChecks {
  static void HeroTraitsChecks(){
   var d=DefDatabase<AbilityDef>.AllDefsListForReading.First(x=>x.Psycast()!=null && x.Psycast().entropyGain>0f);
   var plain=Make(0,10,0);var weave=Make(0,10,0,true,null,center+IntVec3.East);
   HeroTraits.Give(weave,HeroDefOf.LP_WeaveChild);
   float e0=d.Psycast().GetEntropyUsedByPawn(plain),e1=d.Psycast().GetEntropyUsedByPawn(weave);
   Items.Give(weave,"LP_WeaveSurge");float e2=d.Psycast().GetEntropyUsedByPawn(weave);bool skip=HeroDestiny.VpeSkipCooldown(weave);Items.Remove(weave,"LP_WeaveSurge");
   Check(e0>0f && Near(e1,e0*HeroDestiny.WeaveEntropy) && e2==0f && skip && HeroDestiny.VPEActive,"hero traits: 魔网之子 VPE entropy ×0.75 ("+e0+" → "+e1+"), 0 and no cooldown during 魔网涌动 ("+d.defName+")");
   var champ=Make(0,0,10,true,null,center+IntVec3.West);HeroTraits.Give(champ,HeroDefOf.LP_DivineChampion,2);
   Check(Near(BackgroundTraits.ClassXpFactor(champ,"LP_Path_Cleric"),1.3f) && Near(BackgroundTraits.ClassXpFactor(champ,"LP_Path_Paladin"),1.3f) && Near(BackgroundTraits.ClassXpFactor(champ,"LP_Path_Wizard"),1f) && Near(BackgroundTraits.ClassXpFactor(plain,"LP_Path_Cleric"),1f),"hero traits: 神之勇士 cleric / paladin path experience ×1.3, wizard unchanged");
  }
 }
}
