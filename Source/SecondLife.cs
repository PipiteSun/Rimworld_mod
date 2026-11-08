using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.122.0 二周目（LP_SecondLife，传说；1.127.0 热修前为稀有）：前世等级 8～14（第一次见到这个特性时决定，存在 GameComponent_SecondLife 里）。
 // 冒险者等级低于前世等级时冒险者经验 ×2（Adventurer.GainXP / GainTrainingXP 里乘，在其他倍率之后）；
 // 前世等级以内每次升级 25% 几率想起前世（研究进度 +30% 或一项技能热情 +1，各半）；到达前世等级发一封信，之后没有加成。
 // 冒险者系统关闭时 GainXP 直接返回、也不会升级，所以特性只显示不生效。
 public static class SecondLife {
  public const int MinPast=8,MaxPast=14;
  public const float XpMult=2f,MemoryChance=.25f,ResearchFraction=.3f;
  static readonly DefRef<TraitDef> def=new DefRef<TraitDef>("LP_SecondLife");
  public static TraitDef Def=>def.Def;
  public static GameComponent_SecondLife State=>GameComp<GameComponent_SecondLife>.Get();
  public static bool Has(Pawn p)=>Def!=null && p?.story?.traits!=null && p.story.traits.HasTrait(Def);
  // 自测：强制下一次升级是否触发前世记忆、触发哪一种（0 研究，1 热情）。null = 按几率。
  public static bool? TestMemory;public static int? TestKind;

  // 前世等级：有特性时第一次查询就决定并存档；没有特性返回 0（之前定过的仍保留，重新得到特性时沿用）。
  public static int Past(Pawn p){
   if(!Has(p))return 0;var st=State;if(st==null)return 0;
   if(!st.past.TryGetValue(p.thingIDNumber,out int v)){v=Rand.RangeInclusive(MinPast,MaxPast);st.past[p.thingIDNumber]=v;}
   return v;
  }
  public static bool Active(Pawn p,Hediff_Adventurer h)=>Adventurer.Enabled && h!=null && h.level<Adventurer.MaxLevel+1 && h.level<Past(p);
  public static float XpFactor(Pawn p,Hediff_Adventurer h)=>Active(p,h)?XpMult:1f;

  // Hediff_Adventurer.LevelUp 末尾调用（只有玩家阵营的人）。
  public static void OnLevelUp(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null || p.Dead || p.Faction==null || !p.Faction.IsPlayer || !Adventurer.Enabled)return;
   int past=Past(p);if(past<=0 || h.level>past)return;
   bool fire=TestMemory??Rand.Chance(MemoryChance);
   if(fire){var text=Memory(p,TestKind??(Rand.Bool?0:1));if(text!=null)Find.LetterStack.ReceiveLetter("前世记忆："+p.LabelShort,text,LetterDefOf.PositiveEvent,p);}
   var st=State;
   if(h.level>=past && st!=null && st.done.Add(p.thingIDNumber))
    Find.LetterStack.ReceiveLetter("前世的路走完了",p.LabelShortCap+"到了 "+past+" 级，追上了前世走到过的地方。\n\n从这里开始的路前世没有走过：冒险者经验不再翻倍，升级时也不会再想起前世的事。",LetterDefOf.NeutralEvent,p);
   TestMemory=null;TestKind=null;
  }

  // 前世记忆：kind 0 = 当前研究项目 +30% 花费（没有研究就改为热情），1 = 随机一项技能热情 +1（全满跳过）。返回信件正文，null = 什么都没发生。
  public static string Memory(Pawn p,int kind){
   if(kind==0){var r=Research(p);if(r!=null)return r;}
   return Passion(p);
  }
  static string Research(Pawn p){
   var rm=Find.ResearchManager;var proj=rm?.GetProject(null);if(proj==null || proj.IsFinished)return null;
   float add=proj.Cost*ResearchFraction;rm.AddProgress(proj,add,p);
   return p.LabelShortCap+"在梦里回到了前世的书房，醒来后把记得的东西都写了下来。\n\n研究项目「"+proj.LabelCap+"」进度 +"+Mathf.RoundToInt(add)+"（该项目花费的 30%）。";
  }
  static string Passion(Pawn p){
   var list=p.skills?.skills?.Where(s=>!s.TotallyDisabled && s.passion!=RimWorld.Passion.Major).ToList();
   if(list==null || list.Count==0)return null;
   var s0=list.RandomElement();var before=s0.passion;s0.passion=before==RimWorld.Passion.None?RimWorld.Passion.Minor:RimWorld.Passion.Major;
   return p.LabelShortCap+"拿起工具的那一刻，手好像自己想起了前世的事。\n\n技能「"+s0.def.LabelCap+"」热情："+(before==RimWorld.Passion.None?"无 → 小火":"小火 → 大火")+"。";
  }

  public static string TipLine(Pawn p){
   if(p==null)return null;int past=Past(p);if(past<=0)return null;var h=Adventurer.Get(p);
   if(!Adventurer.Enabled || h==null)return "前世等级 "+past+"（冒险者系统未启用，此特性暂不生效）";
   return "前世等级 "+past+"，当前 "+h.level+" 级，"+(Active(p,h)?"冒险者经验 ×2":"经验加成已结束");
  }
 }

 // 自测进程：两个新特性不随机出现在其他检查组的小人身上（二周目会让经验翻倍），系统面板不弹窗；本组检查自己加特性。
 [StaticConstructorOnStartup]
 public static class SecondLifeStartup {
  public static float XmlSecond=-1f,XmlHost=-1f; // XML 里的原值（检查用）
  static readonly AccessTools.FieldRef<TraitDef,float> commonality=AccessTools.FieldRefAccess<TraitDef,float>("commonality");
  static SecondLifeStartup(){
   if(SecondLife.Def!=null)XmlSecond=commonality(SecondLife.Def);if(SystemHost.Def!=null)XmlHost=commonality(SystemHost.Def);
   if(!GenCommandLine.CommandLineArgPassed("lichtest"))return;
   if(SecondLife.Def!=null)commonality(SecondLife.Def)=0f;
   if(SystemHost.Def!=null)commonality(SystemHost.Def)=0f;
   SystemHost.NoPopup=true;
  }
 }

 public class GameComponent_SecondLife : GameComponent {
  public Dictionary<int,int> past=new Dictionary<int,int>();public HashSet<int> done=new HashSet<int>();
  List<int> k1;List<int> v1;
  public GameComponent_SecondLife(Game g){}
  public override void ExposeData(){
   Scribe_Collections.Look(ref past,"lpSecondLifePast",LookMode.Value,LookMode.Value,ref k1,ref v1);
   Scribe_Collections.Look(ref done,"lpSecondLifeDone",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){past=past??new Dictionary<int,int>();done=done??new HashSet<int>();}
  }
 }

 // 悬停说明末尾加一行状态（二周目、系统宿主）。
 [HarmonyPatch(typeof(Trait),nameof(Trait.TipString))]
 public static class SecondLifeTip {
  static void Postfix(Trait __instance,Pawn __0,ref string __result){
   if(__instance?.def==null || __result==null)return;
   string line=null;
   if(__instance.def==SecondLife.Def)line=SecondLife.TipLine(__0??__instance.pawn);
   else if(__instance.def==SystemHost.Def)line=SystemHost.TipLine(__0??__instance.pawn);
   if(line!=null)__result+="\n\n<color=#9FC5E8>"+line+"</color>";
  }
 }
}
