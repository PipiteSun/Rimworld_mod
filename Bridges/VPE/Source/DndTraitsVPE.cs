using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.62.0 狂野魔法的 VPE 部分：每次 VPE 施法 5% 浪涌（主程序集的 DndTraits.OnCast 判断特性与几率）。
 [HarmonyPatch(typeof(Ability),nameof(Ability.Cast),new[]{typeof(RimWorld.Planet.GlobalTargetInfo[])})]
 public static class DndWildSurgeVPE {static void Postfix(Ability __instance){var p=__instance?.pawn;if(p!=null && __instance.def?.Psycast()!=null)DndTraits.OnCast(p);}}

 // -VPE -Only DndTraits：职业天赋路径经验 ×1.25 且与背景取较高不叠加；奥术天赋灵能熵 ×0.9；狂野魔法法术 ×1.1、施法浪涌。
 public static partial class BridgeChecks {
  static void DndTraitsChecks(){
   bool oSup=DndTraits.TestSuppressed;DndTraits.TestSuppressed=false;
   try{
    var tac=Make(10,0,0);tac.story.traits.GainTrait(new Trait(DndTraitDefOf.LP_Tactician,0,true));
    var sage=Make(0,10,0,true,null,center+IntVec3.East);foreach(var t in sage.story.traits.allTraits.Where(t=>BackgroundTraits.IsBackground(t.def)).ToList())sage.story.traits.RemoveTrait(t);
    sage.story.traits.GainTrait(new Trait(BackgroundTraits.Sage));float sageOnly=BackgroundTraits.ClassXpFactor(sage,"LP_Path_Wizard");
    sage.story.traits.GainTrait(new Trait(DndTraitDefOf.LP_ArcaneGift,0,true));float both=BackgroundTraits.ClassXpFactor(sage,"LP_Path_Wizard");
    var plain=Make(0,10,0,true,null,center+IntVec3.West);
    Check(Near(BackgroundTraits.ClassXpFactor(tac,"LP_Path_Fighter"),1.25f) && Near(BackgroundTraits.ClassXpFactor(tac,"LP_Path_Wizard"),1f) && Near(sageOnly,1.25f) && Near(both,1.25f) && Near(BackgroundTraits.ClassXpFactor(plain,"LP_Path_Fighter"),1f),"dnd traits: 战术家 fighter path xp ×1.25; 贤者 + 奥术天赋 wizard ×"+both+" (higher one, not stacked)");
    var d=DefDatabase<AbilityDef>.AllDefsListForReading.First(x=>x.Psycast()!=null && x.Psycast().entropyGain>0f);
    float e0=d.Psycast().GetEntropyUsedByPawn(plain),e1=d.Psycast().GetEntropyUsedByPawn(sage);
    var wild=Make(0,10,0,true,null,center+IntVec3.North);wild.story.traits.GainTrait(new Trait(DndTraitDefOf.LP_WildMagic,0,true));
    var foe=Make(0,0,0,false,null,center+IntVec3.South);
    float spell=DndTraits.OutFactor(wild,DndTraits.Info(wild),foe,new DamageInfo(DamageDefOf.Burn,10f,0f,-1f,wild));
    int s0=DndTraits.SurgeCount;DndTraits.TestRoll=0f;try{DndTraits.OnCast(wild);}finally{DndTraits.TestRoll=null;}
    Check(e0>0f && Near(e1,e0*DndTraits.ArcaneEntropy) && HeroDestiny.VPEActive && Near(spell,DndTraits.WildSpell) && DndTraits.SurgeCount==s0+1,"dnd traits: 奥术天赋 VPE entropy ×0.9 ("+e0+" → "+e1+"); 狂野魔法 surges on a VPE cast; VPE spell damage ×"+spell);
   }finally{DndTraits.TestSuppressed=oSup;}
  }
 }
}
