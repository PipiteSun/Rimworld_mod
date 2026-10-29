using RimWorld;
using Verse;
namespace LichPhylactery {
 public class ThoughtWorker_ConstructSerenity : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>Necro.Construct(p)!=null;
 }
}
