using System.Linq;
using RimWorld;
using Verse;

namespace LichPhylactery.VPEBridge {
 // 1.61.0（-VPE -Only Nonlethal）：五件制伏武器都要器械师「奥术工坊」；没学会的人做不了安眠法杖，学会后可以；设置关掉时谁都能做。
 public static partial class BridgeChecks {
  static void NonlethalChecks(){
   RecipeDef R(string thing)=>DefDatabase<RecipeDef>.AllDefsListForReading.First(r=>r.ProducedThingDef?.defName==thing);
   var recipes=Nonlethal.Weapons.Select(R).ToList();
   Check(recipes.All(r=>Artificer.Needed(r)==Artificer.Workshop) && Nonlethal.Weapons.All(n=>DefDatabase<ThingDef>.GetNamed(n).description.Contains(Artificer.NeedPrefix)),"nonlethal: all 5 weapons need the artificer arcane workshop and say so in their description");
   var smith=Make(0,20,0,false);smith.skills.GetSkill(SkillDefOf.Crafting).Level=20;
   var staff=R("LP_SleepStaff");var cudgel=R("LP_SubduingCudgel");var setting=LichMod.Settings.dndWeaponsNeedArtificer;
   try{
    LichMod.Settings.dndWeaponsNeedArtificer=true;
    bool blocked=!staff.PawnSatisfiesSkillRequirements(smith) && !cudgel.PawnSatisfiesSkillRequirements(smith);
    LichMod.Settings.dndWeaponsNeedArtificer=false;
    bool off=staff.PawnSatisfiesSkillRequirements(smith);
    LichMod.Settings.dndWeaponsNeedArtificer=true;
    var ah=Adventurer.Get(smith)??Adventurer.TryAdd(smith,1,true);ClassSystem.SetLevel(ah,Artificer.Class,1); // 1.73.0：器械师 1 级（主程序集职业）
    bool learned=staff.PawnSatisfiesSkillRequirements(smith) && cudgel.PawnSatisfiesSkillRequirements(smith);
    Check(blocked && off && learned,"nonlethal: a crafting/intellectual-20 pawn without the artificer class cannot craft the sleep staff or cudgel; setting off allows it; artificer level 1 unlocks both");
   }finally{LichMod.Settings.dndWeaponsNeedArtificer=setting;}
  }
 }
}
