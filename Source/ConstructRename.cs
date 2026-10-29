using Verse;
namespace LichPhylactery {
 public partial class Hediff_Construct {
  public Command_Action RenameCommand()=>new Command_Action{
   defaultLabel="重命名造物",
   defaultDesc="修改此造物的名字。名字随角色保存，复生和重塑后保留。",
   icon=Necro.Icon(kind),
   action=()=>Find.WindowStack.Add(new Dialog_NamePawn(pawn,NameFilter.First|NameFilter.Nick|NameFilter.Last,NameFilter.First|NameFilter.Nick|NameFilter.Last,null,null,null,null,null))
  };
 }
}
