using System.Linq;
using RimWorld;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;

namespace LichPhylactery.VPEBridge {
 // 1.58.1（-VPE -Only Feats2）：魔法学徒学会所选路径的第一个能力（不占路径点、不解锁路径）；仪式施法者重置 VPE 能力冷却，下一次 VPE 施法不进冷却、不加熵。
 public static partial class BridgeChecks {
  static void Feats2Checks(){
   var p=Make(0,12,2);var h=Adventurer.Get(p)??Adventurer.TryAdd(p,1,true);var comp=p.GetComp<CompAbilities>();int points=Classes.Psycasts(p)?.points??0;
   var path=Feats2VPE.PathFor(p);var first=Feats2VPE.FirstAbility(path);
   h.AddFeat(Feats2.MagicInitiate);
   Check(path==Classes.Path(Classes.WizardPath) && first!=null && comp.HasAbility(first) && !Classes.HasPath(p,path) && (Classes.Psycasts(p)?.points??0)==points && p.abilities.GetAbility(Feats2.MissileDef)==null,"feats2: 魔法学徒 (intellectual highest) learns the wizard path's first ability "+first?.defName+" without unlocking the path or spending points");
   var a=comp.LearnedAbilities.First(x=>x.def==first);int now=Find.TickManager.TicksGame;a.cooldown=now+30000;
   h.scores[Adventurer.Int]=14;h.Dirty();var rc=Feats2.RitualCaster;
   h.AddFeat(rc);bool any=Feats2.AnyOnCooldown(p);Feats2.Ritual(p);bool reset=a.cooldown<=now && Feats2.Primed(p);
   float e=first.Psycast().entropyGain>0f?first.Psycast().GetEntropyUsedByPawn(p):0f;
   Check(any && reset && e==0f,"feats2: 仪式施法者 sees the VPE cooldown, resets it, and while primed the VPE entropy cost is 0");
  }
 }
}
