using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.180.0 虚空宣泄也给「精神失常」加强（组名 VoidCatharsis）。需要异象 DLC 和 Alpha Memes：Test.ps1 -Full -Anomaly -Only VoidCatharsis。
 // 给测试角色的文化临时加上 AM_Madness 模因，让他拿到虚空宣泄，看心情类、档位和肾上腺素激发。
 public partial class LichChecks {
  void CheckVoidCatharsis(){
   var def=DefDatabase<ThoughtDef>.GetNamedSilentFail("VoidCatharsis");
   bool alphaMemes=ModsConfig.IsActive("sarg.alphamemes");
   if(def==null || !alphaMemes){Check(true,"1.180.0 void catharsis madness: skipped (VoidCatharsis "+(def!=null)+", Alpha Memes "+alphaMemes+")");return;}
   Check(def.thoughtClass==typeof(Thought_VoidCatharsisMadness) && def.stages.Count==2,
    "1.180.0 VoidCatharsis uses "+def.thoughtClass?.Name+" with "+def.stages.Count+" stages (second stage mood "+(def.stages.Count>1?def.stages[1].baseMoodEffect.ToString():"-")+")");
   var meme=Thought_VoidCatharsisMadness.Madness;var buff=Thought_VoidCatharsisMadness.Buff;
   var p=SharedPawn();p.jobs?.StopAll();var ideo=p.Ideo;
   if(meme==null || buff==null || ideo==null || p.needs?.mood==null){Check(false,"setup: meme "+(meme!=null)+", hediff "+(buff!=null)+", ideo "+(ideo!=null)+", mood "+(p.needs?.mood!=null));return;}
   bool addedMeme=false;if(!ideo.HasMeme(meme)){ideo.memes.Add(meme);addedMeme=true;}
   var old=p.health.hediffSet.GetFirstHediffOfDef(buff);if(old!=null)p.health.RemoveHediff(old);
   int before=Thought_VoidCatharsisMadness.Applied;
   p.needs.mood.thoughts.memories.TryGainMemory(def);
   var t=p.needs.mood.thoughts.memories.GetFirstMemoryOfDef(def) as Thought_VoidCatharsisMadness;
   float mood=t?.MoodOffset()??0f;
   var h=p.health.hediffSet.GetFirstHediffOfDef(buff);
   Check(t!=null && t.CurStageIndex==1 && h!=null && h.Severity>=.99f && Thought_VoidCatharsisMadness.Applied>before,
    "with the madness meme, void catharsis is stage "+t?.CurStageIndex+" (mood "+mood.ToString("0.#")+") and gives "+buff.defName+" severity "+(h?.Severity.ToString("0.##")??"none"));
   if(t!=null)p.needs.mood.thoughts.memories.RemoveMemory(t);
   if(h!=null)p.health.RemoveHediff(h);
   if(addedMeme){ideo.memes.Remove(meme);
    p.needs.mood.thoughts.memories.TryGainMemory(def);
    var t2=p.needs.mood.thoughts.memories.GetFirstMemoryOfDef(def);t2?.MoodOffset();
    var h2=p.health.hediffSet.GetFirstHediffOfDef(buff);
    Check(t2!=null && t2.CurStageIndex==0 && h2==null,"without the madness meme, void catharsis stays stage "+t2?.CurStageIndex+" and gives no hediff ("+(h2==null)+")");
    if(t2!=null)p.needs.mood.thoughts.memories.RemoveMemory(t2);if(h2!=null)p.health.RemoveHediff(h2);
   }
  }
 }
}
