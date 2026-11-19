using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.176.0 吃整具尸体（组名 CorpseMeal）。用 Test.ps1 -Full -Only CorpseMeal 在 Peter 的完整 mod 列表下跑：
 // 一个人一口要 999 营养，把一具美洲狮尸体整具吃掉（同原版 FinalizeIngest：Thing.Ingested 之后把返回的营养加到饱食度）。
 // Alpha Memes 的后置补丁只在吃的人是玩家殖民者、文化同时带“必须吃虫肉”“必须吃真菌”两条戒律时才去查这具（已经销毁的）尸体是不是虫肉，
 // 以前就在这里抛空引用、营养加不上。装着 Alpha Memes 时这里给测试角色的文化临时加上这两条戒律来重现。
 public partial class LichChecks {
  void CheckCorpseMeal(){
   var eater=SizePawn(null);if(eater.Faction!=Faction.OfPlayer)eater.SetFaction(Faction.OfPlayer);
   var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Cougar")??PawnKindDefOf.Thrumbo;
   var prey=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true));
   GenSpawn.Spawn(prey,eater.Position,map);prey.Kill(null,null);var corpse=prey.Corpse;
   bool alphaMemes=ModsConfig.IsActive("sarg.alphamemes");int mods=0;foreach(var m in ModLister.AllInstalledMods)if(m.Active)mods++;
   // 重现条件：两条戒律 + 玩家控制的殖民者。
   var added=new List<Precept>();var ideo=eater.Ideo;bool setup=false;
   if(alphaMemes && ideo!=null){
    foreach(var n in new[]{"AM_InsectMeatEating_Required","AM_FungusEating_Required"}){
     var d=DefDatabase<PreceptDef>.GetNamedSilentFail(n);if(d==null)continue;
     if(!ideo.HasPrecept(d)){var p=PreceptMaker.MakePrecept(d);ideo.AddPrecept(p,true);added.Add(p);}
    }
    setup=ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("AM_InsectMeatEating_Required")) && ideo.HasPrecept(DefDatabase<PreceptDef>.GetNamedSilentFail("AM_FungusEating_Required")) && eater.IsColonistPlayerControlled;
   }
   var food=eater.needs.food;food.CurLevel=0f;
   int guardedBefore=CorpseMeatSourceGuard.Guarded;float got=0f;string err=null;
   try{got=corpse.Ingested(eater,999f);food.CurLevel+=got;}catch(Exception e){err=e.GetType().Name+": "+e.Message;}
   int guarded=CorpseMeatSourceGuard.Guarded-guardedBefore;string job=eater.CurJobDef?.defName;
   try{foreach(var p in added)ideo.RemovePrecept(p);}catch(Exception){}
   Check(corpse!=null && err==null && got>0f && corpse.Destroyed && food.CurLevel>0f,
    "1.176.0 whole-corpse meal: eating an entire "+kind.defName+" corpse in one bite gives nutrition "+got.ToString("0.##")+" (food level 0 → "+food.CurLevel.ToString("0.##")+"), corpse consumed "+(corpse?.Destroyed)+", error "+(err??"none")+"; active mods "+mods+", Alpha Memes "+(alphaMemes?"active":"not loaded")+", insect+fungus precepts on a player colonist "+setup+", guard used "+guarded+" time(s), job afterwards "+job);
   if(alphaMemes)Check(setup && guarded>0,"with Alpha Memes and the insect-meat + fungus precepts, its post-ingest lookup on the consumed corpse goes through the guard (setup "+setup+", guarded "+guarded+")");
  }
 }
}
