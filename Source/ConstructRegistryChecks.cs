using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public partial class LichChecks {
  void CheckConstructRegistry(Pawn p){
   var registry=Necro.Registry;var h=Necro.Construct(p);worker.psychicEntropy.OffsetPsyfocusDirectly(1);
   registry.constructs.Remove(p);h.dormant=true;p.health.Notify_HediffChanged(h);p.health.CheckForStateChange(null,h);
   Check(p.Downed,"lost registry fixture is downed by dormant consciousness cap");
   registry.ReconcileConstructs();h.Maintain(0);
   Check(registry.constructs.Contains(p) && h.HasControl && !h.dormant && !p.Downed,"registry rebuild recovers omitted living construct and wakes it");
   registry.constructs.Remove(p);h.Maintain(0);
   Check(registry.constructs.Count(x=>x==p)==1 && h.HasControl,"maintenance self-registers an omitted construct exactly once");
   p.Kill(null);registry.constructs.Remove(p);
   Check(ResurrectionUtility.TryResurrect(p,new ResurrectionParams{restoreMissingParts=true,removeDiedThoughts=true}),"construct resurrection succeeds");
   h=Necro.Construct(p);h.Maintain(0);
   Check(registry.constructs.Contains(p) && h.HasControl && !h.dormant && !p.Downed,"resurrected construct re-enters control registry and can move");
  }
 }
}