using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.58.6：-VPE -Only Perf。主程序集的 LichPerf.Run 跑同样的整图计时和微基准，这里只把开人口里 32 人加上职业路径（8 条路径各 4 人，照 Mentor.MakeMentor），
 // 再加几项桥接热点：技能经验、VEF IsEnabledForPawn、守护姿态、职业光环刷新。
 public static partial class BridgeChecks {
  static void PerfChecks(){
   string[] paths={Classes.FighterPath,Classes.WizardPath,Classes.ClericPath,Classes.DruidPath,Classes.PaladinPath,Classes.SorcererPath,Classes.MonkPath,Artificer.PathName};
   LichPerf.Decorate=list=>{
    int i=0;
    foreach(var p in list.Where(p=>p.RaceProps.Humanlike && p.GetComp<CompAbilities>()!=null && Adventurer.Get(p)!=null).Take(32)){
     var path=Classes.Path(paths[i++%paths.Length]);if(path==null)continue;
     foreach(var sd in new[]{path.requiredSkill,path.alternateSkill}){if(sd==null)continue;var s=p.skills?.GetSkill(sd);if(s!=null && !s.TotallyDisabled && s.Level<16)s.Level=16;}
     if(p.GetMainPsylinkSource()==null)p.ChangePsylinkLevel(1,false);
     var h=Classes.Psycasts(p);if(h==null)continue;
     if(h.level<10)h.ChangeLevel(10-h.level,false);
     if(!h.unlockedPaths.Contains(path))h.UnlockPath(path);
     var comp=p.GetComp<CompAbilities>();foreach(var a in path.abilities)if(!comp.HasAbility(a))comp.GiveAbility(a);
     Martial.Refresh(p);
    }
   };
   LichPerf.ExtraBench=(plain,adv,enemy)=>{
    object sink=null;
    var cls=adv.Map.mapPawns.AllPawnsSpawned.FirstOrDefault(p=>Classes.Psycasts(p)?.unlockedPaths.Any(x=>x is LP_ClassPathDef)==true);
    if(cls==null){LichPerf.Line("vpe: no class pawn spawned");return;}
    LichPerf.Bench("vpe plain Learn(Construction)",20000,()=>plain.skills.Learn(SkillDefOf.Construction,.0001f));
    LichPerf.Bench("vpe class Learn(Melee)",20000,()=>cls.skills.Learn(SkillDefOf.Melee,.0001f));
    var ab=cls.GetComp<CompAbilities>()?.LearnedAbilities.FirstOrDefault();
    if(ab!=null)LichPerf.Bench("vpe class IsEnabledForPawn",20000,()=>sink=ab.IsEnabledForPawn(out _));
    LichPerf.Bench("vpe GuardStanceShare.Guardian",20000,()=>sink=GuardStanceShare.Guardian(plain));
    var m=adv.Map;LichPerf.Bench("vpe ClassAuras.Refresh",100,()=>MapComponent_ClassAuras.Refresh(m));
   };
   try{var s=LichPerf.Run(map);Check(s.Length>0,"performance pass recorded (VPE): "+s);}
   finally{LichPerf.Decorate=null;LichPerf.ExtraBench=null;}
  }
 }
}
