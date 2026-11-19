using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.180.0 兼容（Peter：殖民者没有获得「精神失常」文化的加强 → 虚空宣泄）。
 // Alpha Memes 的「精神失常」（AM_Madness）只改了原版心情「宣泄」（Catharsis → AlphaMemes.Thought_Catharsis）：
 // 拿到宣泄时若文化带这个模因，加一天的「肾上腺素激发」（AM_CatharsisHediff），心情换成 +60 那一档。
 // 异象 DLC 的异常崩溃（黑暗幻象等，BaseAnomalyMentalState）恢复时给的是「虚空宣泄」（VoidCatharsis），Alpha Memes 不认，所以从来不给加强。
 // 这里照同样的规则给虚空宣泄换一个心情类（Bridges/Anomaly/Patches/AlphaMemesVoidCatharsis.xml，只在 Alpha Memes 启用、且虚空宣泄还没有别的心情类时）：
 // 文化带精神失常 → 用第二档（+50，原档 +30，和原版宣泄一样多 20），并在第一次算心情时加上肾上腺素激发（严重度 1，一天退完）。
 // 不引用 AlphaMemes.dll，模因和健康状态都按名字查；没装 Alpha Memes 时这两样查不到，什么都不做。
 // 不能直接把虚空宣泄指到 AlphaMemes.Thought_Catharsis：那个类带模因时固定返回第 1 档，虚空宣泄原本只有一档。
 public class Thought_VoidCatharsisMadness : Thought_Memory {
  public bool appliedOnce;
  public static int Applied; // 自检用
  static MemeDef madness;static HediffDef buff;static bool looked;
  public static MemeDef Madness{get{Look();return madness;}}
  public static HediffDef Buff{get{Look();return buff;}}
  static void Look(){
   if(looked)return;looked=true;
   madness=DefDatabase<MemeDef>.GetNamedSilentFail("AM_Madness");
   buff=DefDatabase<HediffDef>.GetNamedSilentFail("AM_CatharsisHediff");
  }
  bool Mad=>pawn!=null && Madness!=null && pawn.Ideo!=null && pawn.Ideo.HasMeme(Madness);
  public override int CurStageIndex=>Mad && def.stages.Count>1?1:0;
  public override float MoodOffset(){
   if(!appliedOnce && Mad && !pawn.Dead && pawn.health!=null){
    if(Buff!=null){
     var h=pawn.health.hediffSet.GetFirstHediffOfDef(Buff)??pawn.health.AddHediff(Buff);
     if(h!=null)h.Severity=1f;
     Applied++;
    }
    appliedOnce=true;
   }
   return base.MoodOffset();
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref appliedOnce,"appliedOnce");
  }
 }
}
