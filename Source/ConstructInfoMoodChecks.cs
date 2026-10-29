using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public partial class LichChecks {
  void CheckConstructInfoAndMood(Pawn p){
   var stats=(IEnumerable<StatDrawEntry>)AccessTools.Method(typeof(StatsReportUtility),"StatsToDraw",new[]{typeof(Thing)}).Invoke(null,new object[]{p});
   var entries=stats.ToList();foreach(var entry in entries){var value=entry.ValueString;}
   Check(entries.Count>20,"native information card enumerates complete stats without errors: "+p.def.defName);
   var def=Necro.Def<ThoughtDef>("ConstructSerenity");
   p.needs.mood.thoughts.situational.Notify_SituationalThoughtsDirty();
   var thoughts=new List<Thought>();p.needs.mood.thoughts.GetAllMoodThoughts(thoughts);
   var serenity=thoughts.Where(t=>t.def==def).ToList();
   Check(serenity.Count==1 && Near(serenity[0].MoodOffset(),30),"construct has exactly one permanent +30 mood thought: "+p.def.defName);
   Check(!def.Worker.CurrentState(worker).Active,"construct serenity excludes the lich master");
   var ordinary=map.mapPawns.FreeColonistsSpawned.FirstOrDefault(x=>!Necro.Undead(x));
   if(ordinary!=null)Check(!def.Worker.CurrentState(ordinary).Active,"construct serenity excludes ordinary colonists");
  }
 }
}
