using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.153.0：Test.ps1 -Only StorytellerDM。叙事者「地下城主」的定义、组件、命运之骰各档点数。
 public partial class LichChecks {
  void CheckStorytellerDM(){
   var st=DefDatabase<StorytellerDef>.GetNamedSilentFail("LP_DungeonMaster");
   Check(st!=null && st.label.Contains("地下城主"),"storyteller LP_DungeonMaster loads");
   if(st==null)return;
   Check(st.comps.Any(c=>c is StorytellerCompProperties_DmCampaign) && st.comps.Any(c=>c is StorytellerCompProperties_DmFateRoll d && d.incident?.defName=="LP_DmFateRoll"),"DM has campaign + fate-roll comps");
   Check(st.comps.Any(c=>c is StorytellerCompProperties_OnOffCycle o && o.incident==null && o.minSpacingDays>1f),"DM keeps the classic big-threat cycle");
   Check(st.portraitLargeTex!=null && st.portraitLargeTex!=BaseContent.BadTex && st.portraitTinyTex!=null && st.portraitTinyTex!=BaseContent.BadTex,"DM portraits load");
   var pool=DungeonMaster.CampaignPool(false).ToList();
   Check(pool.Count>=15 && pool.All(DungeonMaster.IsLP) && !pool.Any(d=>d.category==IncidentCategoryDefOf.ThreatBig),"campaign pool = LP non-big events ("+pool.Count+")");
   Check(DungeonMaster.Good().Count()>=10,"good-event list resolves ("+DungeonMaster.Good().Count()+")");
   Check(DungeonMaster.Bad(true).Any() && DungeonMaster.Bad(false).Any(),"bad-event pools are not empty");

   // 组件实例：战役组件能挑出事件
   Check(DungeonMaster.TryPick(pool,map,out var pick,out var pp) && pick!=null,"campaign can pick a firable LP event on the test map ("+pick?.defName+")");

   // 组件本身：战役组件（mtb 调到极小）出事件；命运组件只在 5 天整点出骰子
   var cp=new StorytellerCompProperties_DmCampaign{mtbDays=0.0001f};var cc=new StorytellerComp_DmCampaign{props=cp};
   var made=Enumerable.Range(0,5).SelectMany(_=>cc.MakeIntervalIncidents(map)).ToList();
   Check(made.Count>0 && made.All(f=>DungeonMaster.IsLP(f.def)),"campaign comp yields LP incidents ("+made.Count+")");
   var fp=(StorytellerCompProperties_DmFateRoll)st.comps.First(c=>c is StorytellerCompProperties_DmFateRoll);var fc=new StorytellerComp_DmFateRoll{props=fp};
   var tf=HarmonyLib.AccessTools.Field(typeof(TickManager),"ticksGameInt");int oldTicks=(int)tf.GetValue(Find.TickManager);
   try{
    tf.SetValue(Find.TickManager,300000);int on=fc.MakeIntervalIncidents(map).Count();
    tf.SetValue(Find.TickManager,301000);int off=fc.MakeIntervalIncidents(map).Count();
    Check(on==1 && off==0,"fate comp rolls exactly on the 5-day mark (on "+on+", off "+off+")");
   }finally{tf.SetValue(Find.TickManager,oldTicks);}

   var fate=DefDatabase<IncidentDef>.GetNamed("LP_DmFateRoll");
   int letters=Find.LetterStack.LettersListForReading.Count;
   foreach(int r in new[]{10,3,17,20,1}){
    DungeonMaster.ForcedRoll=r;var p=StorytellerUtility.DefaultParmsNow(fate.category,map);
    bool ok=fate.Worker.TryExecute(p);
    Check(ok && IncidentWorker_DmFateRoll.LastRoll==r,"fate roll "+r+" executes (fired "+(IncidentWorker_DmFateRoll.LastFired??"nothing")+")");
    if(r==20 || r==17 || r==1)Check(IncidentWorker_DmFateRoll.LastFired!=null,"fate roll "+r+" triggers a follow-up event");
   }
   Check(Find.LetterStack.LettersListForReading.Count>letters,"fate rolls send letters");
   // 清掉测试里来的敌人，免得影响后面的组
   foreach(var x in map.mapPawns.AllPawnsSpawned.Where(x=>x.HostileTo(Faction.OfPlayer)).ToList())x.Destroy();
  }
 }
}
