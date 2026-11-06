using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.116.0：五色真龙和贝希尔 / 眼魔 / 半巫妖从 HAR 人形种族（Bridges/HAR）改成主模组里的动物型怪物（Defs/ChromaticDragons_Races.xml、Defs/LegendaryMonsters_Races.xml），不再需要 HAR。
 // defName 不变。这里是转换带来的两处补偿：旧存档里的人形部件，和原来由冒险者 hediff 给的传奇等级近战加成。
 public static class LegendaryBeasts {
  public static readonly HashSet<string> Races=new HashSet<string>{"LP_RedDragon","LP_BlueDragon","LP_GreenDragon","LP_BlackDragon","LP_WhiteDragon","LP_Behir","LP_Beholder","LP_Demilich"};
  public static bool IsRace(ThingDef d)=>d!=null && Races.Contains(d.defName);

  // 人形时，传奇等级（超过 20 级）的传奇属性点全投近战（Legendary.Setup），由冒险者 hediff 给近战伤害。动物没有冒险者 hediff：按兵种等级下限照同样的点数补上（属性系统关闭时同样不给）。
  public static float MeleeFactor(Pawn p){
   if(p?.RaceProps==null || p.RaceProps.Humanlike)return 1f;
   var e=Legendary.Ext(p);if(e==null || !Adventurer.Enabled || Adventurer.Get(p)!=null)return 1f;
   int over=e.level.min-Adventurer.MaxLevel;return over<=0?1f:1f+Legend.MeleePer*Legend.PointsPerLevel*over;
  }

  // 旧存档：1.115.0 以前生成的龙和传奇怪带着人形才有的部件（背景、技能、衣物、基因……原版 Pawn.ExposeData 不看种族照读），还有冒险者 hediff。
  // 读档时去掉，和新生成的动物型怪物一致；身体（BodyDef）没变，伤口和缺失部位照旧。
  public static int Stripped;
  public static bool StripHumanlike(Pawn p){
   if(p==null || !IsRace(p.def) || p.RaceProps.Humanlike)return false;
   bool any=p.story!=null || p.skills!=null || p.apparel!=null || p.equipment!=null || p.genes!=null;
   if(!p.RaceProps.ToolUser){p.apparel=null;p.equipment=null;}
   p.story=null;p.skills=null;p.workSettings=null;p.guest=null;p.guilt=null;p.royalty=null;p.ideo=null;p.style=null;p.surroundings=null;p.genes=null;p.styleObserver=null;p.creepjoiner=null;
   p.interactions=null;p.outfits=null;p.drugs=null;p.foodRestriction=null;p.timetable=null;
   if(any && !p.RaceProps.hasGenders)p.gender=Gender.None;
   var adv=p.health?.hediffSet?.hediffs.OfType<Hediff_Adventurer>().ToList();
   if(adv!=null && adv.Count>0){any=true;foreach(var h in adv){p.health.hediffSet.hediffs.Remove(h);h.live=false;}p.health.hediffSet.DirtyCache();}
   if(any)Stripped++;
   return any;
  }
 }

 [HarmonyPatch(typeof(Pawn),nameof(Pawn.ExposeData))]
 public static class LegendaryBeastLoad {
  static void Postfix(Pawn __instance){
   if(Scribe.mode!=LoadSaveMode.PostLoadInit || !LegendaryBeasts.IsRace(__instance?.def))return;
   try{if(LegendaryBeasts.StripHumanlike(__instance))Log.Message("[DND边缘大陆] 旧存档："+__instance.LabelShortCap+"（"+__instance.def.label+"）改成动物型怪物，去掉了人形部件。");}
   catch(Exception e){Log.Warning("[DND边缘大陆] 旧存档传奇怪转换失败（"+__instance.LabelShortCap+"）："+e.Message);}
  }
 }

 [StaticConstructorOnStartup]
 public static class LegendaryBeastStartup {
  static LegendaryBeastStartup(){
   var stat=StatDefOf.MeleeDamageFactor;if(stat.parts==null)stat.parts=new List<StatPart>();
   if(!stat.parts.Any(x=>x is StatPart_LegendaryBeastMelee))stat.parts.Add(new StatPart_LegendaryBeastMelee{parentStat=stat});
  }
 }
 public class StatPart_LegendaryBeastMelee : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p)val*=LegendaryBeasts.MeleeFactor(p);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;float f=LegendaryBeasts.MeleeFactor(p);return f>1.0001f?"传奇等级（近战）：×"+f.ToString("0.##"):null;}
 }
}
