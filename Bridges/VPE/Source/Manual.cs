using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using VanillaPsycastsExpanded;

namespace LichPhylactery.VPEBridge {
 // 1.27.0 冒险者手册：没有心灵链接而任一职业技能 ≥ 8 的人获得 1 级心灵链接并免费解锁对应路径（技能最高的那条）；
 // 已有心灵链接的人只解锁一条新路径（技能 ≥ 6）。物品本身由 CompUseEffect_DestroySelf 消耗。
 public class CompProperties_UseEffect_Manual : CompProperties_UseEffect {
  public CompProperties_UseEffect_Manual(){compClass=typeof(CompUseEffect_Manual);}
 }
 public class CompUseEffect_Manual : CompUseEffect {
  public const int SkillForPsylink=8;
  public static bool HasPsylink(Pawn p)=>p?.GetMainPsylinkSource()!=null;
  // 选路径：技能最高、未解锁、且达到门槛的那条。返回 null 时 reason 说明原因。
  public static LP_ClassPathDef Choose(Pawn p,out string reason){
   reason=null;
   if(p?.skills==null){reason="需要有技能的人形。";return null;}
   bool linked=HasPsylink(p);
   var h=Classes.Psycasts(p);
   LP_ClassPathDef best=null;int bestLevel=-1;bool anyUnlockable=false;
   foreach(var path in Classes.Paths){
    int level=path.Level(p);
    if(linked && Classes.HasPath(p,path))continue;
    if(ClassRules.Forbidden(path,p)!=null)continue; // 1.36.0 巫妖不能学圣武士、德鲁伊
    // 1.48.0：已有心灵链接时按路径门槛（技能 6 级或冒险者主属性 13）。
    if(linked?!path.SkillOk(p):level<SkillForPsylink)continue;
    anyUnlockable=true;
    if(level>bestLevel){bestLevel=level;best=path;}
   }
   if(best!=null)return best;
   if(linked && Classes.Paths.All(path=>Classes.HasPath(p,path)))reason="已经掌握了全部职业路径。";
   else if(linked)reason="没有可解锁的新职业路径（需要对应技能 6 级或冒险者主属性 13）。";
   else reason="需要格斗、智识、社交、手工、种植或驯兽任一达到 "+SkillForPsylink+" 级。";
   return null;
  }
  public override AcceptanceReport CanBeUsedBy(Pawn p){
   var baseReport=base.CanBeUsedBy(p);if(!baseReport.Accepted)return baseReport;
   if(p.Dead || p.Downed)return "需要清醒。";
   var path=Choose(p,out var reason);
   return path!=null?AcceptanceReport.WasAccepted:(AcceptanceReport)reason;
  }
  public override void DoEffect(Pawn usedBy){
   base.DoEffect(usedBy);
   Apply(usedBy);
  }
  public static LP_ClassPathDef Apply(Pawn p){
   var path=Choose(p,out var reason);
   if(path==null){Messages.Message(reason,p,MessageTypeDefOf.RejectInput,false);return null;}
   bool linked=HasPsylink(p);
   if(!linked)p.ChangePsylinkLevel(1,false);
   var h=Classes.Psycasts(p);
   if(h==null){Log.Error("[DND边缘大陆] 冒险者手册：没有找到 VPE 灵能数据，"+p.LabelShort);return null;}
   if(!h.unlockedPaths.Contains(path))h.UnlockPath(path);
   Adventurer.GainTrainingXP(p,Adventurer.ManualXP); // 1.48.0 冒险者训练经验（最多练到 3 级）
   Messages.Message(p.LabelShort+(linked?"从冒险者手册里领悟了":"读完冒险者手册，觉醒了心灵链接，并领悟了")+path.LabelCap+"之道。",p,MessageTypeDefOf.PositiveEvent,false);
   return path;
  }
 }
}
