using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.35.0：所有研究合并到“DND边缘大陆”页签（LP_FireGiantTech）。原来直接挂在原版研究下的装备和建筑改由新研究解锁；
 // 旧存档第一次读取时，前置原版研究都已完成的新研究直接记为完成，已经能做的东西不会突然锁上。
 public static class ResearchTab {
  public const string Tab="LP_FireGiantTech";
  // 按前置顺序排：巨人重甲要在巨人锻造之后判断。
  public static readonly string[] Added={"LP_GiantForging","LP_GiantPlate","LP_RacialArmory","LP_RacialBlades","LP_AstralPlate","LP_GeneWorkbenchTech"};
  public static int MigrateLegacy(){
   int done=0;
   foreach(var name in Added){
    var p=DefDatabase<ResearchProjectDef>.GetNamedSilentFail(name);
    if(p==null || p.IsFinished || p.prerequisites==null || !p.prerequisites.All(r=>r.IsFinished))continue;
    Find.ResearchManager.FinishProject(p,false,null,false);done++;
   }
   if(done>0)Log.Message("[DND边缘大陆] 研究页签迁移：按已完成的原版前置，直接完成了"+done+"项新研究。");
   return done;
  }
 }

 public class ResearchTabState : GameComponent {
  public int layoutVersion;
  public ResearchTabState(Game game){}
  public override void StartedNewGame(){layoutVersion=1;}
  public override void LoadedGame(){if(layoutVersion>=1)return;try{ResearchTab.MigrateLegacy();}catch(Exception e){Log.Error("[DND边缘大陆] 研究页签迁移失败："+e);}layoutVersion=1;}
  public override void ExposeData(){Scribe_Values.Look(ref layoutVersion,"layoutVersion",0);}
 }
}
