using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public partial class Registry {
  public void RegisterConstruct(Pawn pawn){if(pawn!=null && !pawn.Destroyed && !pawn.Dead && Necro.Construct(pawn)!=null && !constructs.Contains(pawn))constructs.Add(pawn);}
  public void ReconcileConstructs(){
   // The list is an index, not the source of ownership. Recover live map/caravan/world
   // pawns from their saved construct hediff before evaluating any capacity allocation.
   constructs=constructs.Concat(PawnsFinder.AllMapsWorldAndTemporary_Alive)
    .Where(p=>p!=null && !p.Destroyed && !p.Dead && Necro.Construct(p)!=null).Distinct().ToList();
  }
 }
 public partial class Hediff_Construct {
  public void RegisterForControl()=>Necro.Registry?.RegisterConstruct(pawn);
  public override void Notify_Resurrected(){base.Notify_Resurrected();UpgradeAppearance();RegisterForControl();Maintain(0);}
 }
}
