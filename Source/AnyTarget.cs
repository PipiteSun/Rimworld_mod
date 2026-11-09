using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.136.0 热修（Peter：「DND 技能的可释放对象改为所有人」，例如法师的寒冰射线）：设置「技能可对任何人释放」（默认开）。
 // 1) 选目标：ClassEffects.Invalid 里的敌我限制（「只能对敌人 / 友军使用」之类）对玩家手动选的小人不再生效；
 // 2) 生效：施放时把选中的小人记成 Picked，各效果里的 Hostile / Friendly 判断对它放行——敌对能力（AbilityDef.hostile）可以打任何人，非敌对能力（治疗、增益）可以给任何人；
 //    范围效果里其他人照旧按敌我分（圣域治疗友军、伤害亡灵之类不变）；
 // 3) 原版选目标过滤：职业能力的 targetParams 统一打开人形 / 动物 / 机械体（纯自身、地面、尸体、建筑类不变）。
 // NPC / 自动施放用自己的目标选择（NpcClasses），只会挑原来合适的目标，不会因此打自己人。
 public static class AnyTarget {
  [ThreadStatic] public static Pawn Picked;[ThreadStatic] public static bool PickedHarm;
  public static bool On=>LichMod.Settings?.anyTarget??true;
  public static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && (t.HostileTo(a) || On && PickedHarm && t==Picked);
  public static bool Friendly(Pawn a,Pawn t)=>t!=null && !t.Dead && (!t.HostileTo(a) || On && !PickedHarm && t==Picked);
  // 只是敌我 / 阵营限制的拒绝理由（其他理由——活着、人形、射程、体型、首领——照旧）。
  static readonly string[] Side={"只能对敌人","只能对友军","只能对友方","只能对己方","只能复制己方","只能对殖民","需要选择另一名友军"};
  public static bool SideOnly(string why)=>why!=null && Side.Any(why.Contains);
  public static string Relax(string why,LocalTargetInfo target){
   if(why==null || !On || !SideOnly(why))return why;
   var t=target.Thing as Pawn;return t!=null && !t.Dead?null:why;
  }

  // ---------- 原版目标过滤 ----------
  static readonly Dictionary<TargetingParameters,(bool h,bool a,bool m)> orig=new Dictionary<TargetingParameters,(bool,bool,bool)>();
  public static int Apply(){
   int n=0;
   foreach(var d in DefDatabase<AbilityDef>.AllDefsListForReading){
    if(d.abilityClass==null || !typeof(Ability_Class).IsAssignableFrom(d.abilityClass))continue;
    var tp=d.verbProperties?.targetParams;if(tp==null || !tp.canTargetPawns)continue;
    if(!orig.ContainsKey(tp))orig[tp]=(tp.canTargetHumans,tp.canTargetAnimals,tp.canTargetMechs);
    if(On){tp.canTargetHumans=tp.canTargetAnimals=tp.canTargetMechs=true;}
    else{var o=orig[tp];tp.canTargetHumans=o.h;tp.canTargetAnimals=o.a;tp.canTargetMechs=o.m;}
    n++;
   }
   return n;
  }
 }
 [StaticConstructorOnStartup]
 public static class AnyTargetStartup {static AnyTargetStartup(){try{AnyTarget.Apply();}catch(Exception e){Log.Error("[DND边缘大陆] 技能可对任何人释放："+e);}}}
}
