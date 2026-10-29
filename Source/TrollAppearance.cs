using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 public static class TrollAppearance {
  static GeneDef appearanceGene;
  static readonly string[] phenotypeGenes={"Skin_Green","Body_Hulk","Ears_Pointed","Jaw_Heavy","Brow_Heavy","Hair_BaldOnly","Beard_NoBeardOnly"};
  public static GeneDef Gene=>appearanceGene??(appearanceGene=DefDatabase<GeneDef>.GetNamedSilentFail("LP_TrollAppearance"));
  public static bool Active(Pawn pawn)=>Gene!=null && pawn?.genes?.HasActiveGene(Gene)==true;
  public static float Size(Pawn pawn){
   if(!Active(pawn))return 1f;
   float age=pawn.ageTracker?.AgeBiologicalYearsFloat??18f;
   return 1f+.5f*Mathf.Clamp01(age/18f);
  }
  public static void Refresh(Pawn pawn){
   pawn?.health?.capacities?.Notify_CapacityLevelsDirty();
   pawn?.Drawer?.renderer?.SetAllGraphicsDirty();
  }
  public static void Migrate(Pawn pawn){
   if(pawn?.genes?.Xenotype?.defName!="LP_Troll" || pawn.Dead || Gene==null)return;
   bool changed=false;
   foreach(var name in phenotypeGenes){
    var def=DefDatabase<GeneDef>.GetNamedSilentFail(name);
    if(def!=null && pawn.genes.GetGene(def)==null){pawn.genes.AddGene(def,false);changed=true;}
   }
   if(pawn.genes.GetGene(Gene)==null){pawn.genes.AddGene(Gene,false);changed=true;}
   var diet=TrollDiet.RawFoodGene;
   if(diet!=null && pawn.genes.GetGene(diet)==null){pawn.genes.AddGene(diet,false);changed=true;}
   if(changed)Refresh(pawn);
  }
  public static void MigrateLoadedGame(){
   if(Find.Maps!=null)foreach(var pawn in Find.Maps.SelectMany(m=>m.mapPawns.AllPawns).Distinct())Migrate(pawn);
   if(Find.WorldPawns!=null)foreach(var pawn in Find.WorldPawns.AllPawnsAliveOrDead.ToList())Migrate(pawn);
  }
 }
 public class Gene_TrollAppearance : Gene {
  public override void PostAdd(){base.PostAdd();TrollAppearance.Refresh(pawn);}
  public override void PostRemove(){base.PostRemove();TrollAppearance.Refresh(pawn);}
 }
}
