using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.105.0 公告板「魔物来袭」委托（见 ArtSource/QuestBoardMonsters-1.105.0-report.md）：和守卫家园一样在殖民地打，来的是本模组独有的 DND 怪物。
 // 六种魔物，按等阶解锁：魔兽群（青铜起：猫头鹰熊、座狼、蝎尾狮、掘地兽……，永久狂暴）、巨人劫掠队（白银起：食人魔、双头巨人、霜巨人、风暴巨人，红袍法师会派系）、
 // 深渊魔群（白银起：劣魔、巴路古拉、影魔、魅魔、哈兹魔、纳巴苏）、九狱魔军（黄金起：链魔、骨魔、艾林魔、角魔、阿姆尼祖）、
 // 真龙来袭（秘银起：青年五色龙，传说是成年龙，带本色护卫，照 1.52.0 的真龙来袭生成）、传奇魔物（传说：贝希尔带魔兽，或死亡骑士带红袍亡灵）。
 // 阵容在贴出委托时就按威胁点数配好（存在委托里），公告板上写着会来什么；对应系统在设置里关掉、或缺少需要的模组时，这种魔物不出。
 public static class QuestBoardMonsters {
  public const string Beasts="beasts",Giants="giants",Abyss="abyss",Hell="hell",Dragon="dragon",Legend="legend";
  public static readonly string[] Themes={Beasts,Giants,Abyss,Hell,Dragon,Legend};
  public static readonly string[] BeastKinds={"LP_BlinkDog","LP_RustMonster","LP_Worg","LP_WinterWolf","LP_HellHound","LP_DisplacerBeast","LP_CarrionCrawler","LP_GelatinousCube","LP_Basilisk","LP_Owlbear","LP_Manticore","LP_Bulette"};
  public static readonly string[] GiantKinds={"LP_FH_OgreBrute","LP_FH_EttinBrute","LP_FH_FrostGiantRaider","LP_FH_EnslavedStormGiant"};
  public static readonly string[] AbyssKinds={"LP_AH_Dretch","LP_AH_Barlgura","LP_AH_ShadowDemon","LP_AH_Succubus","LP_AH_Hezrou","LP_AH_Nabassu"};
  public static readonly string[] HellKinds={"LP_HL_ChainDevil","LP_HL_BoneDevil","LP_HL_Erinyes","LP_HL_HornedDevil","LP_HL_Amnizu"};
  public static readonly string[] UndeadKinds={"LP_FH_Skeleton","LP_FH_Golem"};
  public const int MaxPawns=16;
  public const float EscortShare=.4f,MonsterSilver=1.25f,MonsterDndBonus=.15f;
  public static string TestTheme;

  public static string ThemeLabel(string t)=>t==Beasts?"魔兽群":t==Giants?"巨人劫掠队":t==Abyss?"深渊魔群":t==Hell?"九狱魔军":t==Dragon?"真龙":t==Legend?"传奇魔物":"魔物";
  public static int MinTier(string t)=>t==Beasts?1:t==Giants || t==Abyss?2:t==Hell?3:t==Dragon?4:5;
  static PawnKindDef K(string n)=>DefDatabase<PawnKindDef>.GetNamedSilentFail(n??"");
  static bool Any(string[] kinds)=>kinds.Any(n=>K(n)!=null);
  static Faction GiantFaction(){var f=Factions.Hostile;return f!=null && !f.defeated && f.HostileTo(Faction.OfPlayer)?f:null;}
  public static bool Available(string t){
   switch(t){
    case Beasts:return Any(BeastKinds);
    case Giants:return Any(GiantKinds) && GiantFaction()!=null;
    case Abyss:return AbyssSettings.Enabled && AbyssRaces.HordeDef!=null && Any(AbyssKinds);
    case Hell:return Pacts.Enabled && PactEvents.LegionDef!=null && Any(HellKinds);
    case Dragon:return Dragons.On;
    case Legend:return Monsters.On && (Monsters.Available || Monsters.DeathKnightOn);
   }
   return false;
  }
  // 魔兽群权重 2，其他 1；刚解锁的那一种（最低等阶 = 本阶）再 +1，让新内容更常露面。
  public static string PickTheme(int tier){
   if(TestTheme!=null)return Available(TestTheme) && MinTier(TestTheme)<=tier?TestTheme:null;
   var ok=Themes.Where(t=>MinTier(t)<=tier && Available(t)).ToList();if(ok.Count==0)return null;
   return ok.RandomElementByWeight(t=>(t==Beasts?2f:1f)+(MinTier(t)==tier && t!=Beasts?1f:0f));
  }

  // 按点数配阵容：每次从“战斗力不超过剩余点数”的种类里随机挑一个（都超了就挑最便宜的），至少两个、最多 MaxPawns 个。
  // 魔兽群另外要求战斗力 ≥ MinBeastPower（同远征讨伐，不要一大群闪光犬去打传说委托）。
  public static List<string> Compose(string[] kinds,float points,bool beasts){
   var pool=kinds.Select(K).Where(k=>k!=null).ToList();
   if(beasts){var big=pool.Where(k=>k.combatPower>=QuestBoard.MinBeastPower(points)).ToList();if(big.Count>0)pool=big;}
   var list=new List<string>();if(pool.Count==0)return list;
   float left=points,cheapest=pool.Min(k=>k.combatPower);
   while(list.Count<MaxPawns && (left>=cheapest*.5f || list.Count<2)){
    var fit=pool.Where(k=>k.combatPower<=left).ToList();
    var k2=fit.Count>0?fit.RandomElement():pool.MinBy(k=>k.combatPower);
    list.Add(k2.defName);left-=k2.combatPower;
   }
   return list;
  }
  public static bool Setup(BoardContract c,int tier){
   var t=PickTheme(tier);if(t==null)return false;
   c.theme=t;c.lead=null;c.kinds=new List<string>();
   switch(t){
    case Beasts:c.kinds=Compose(BeastKinds,c.points,true);break;
    case Giants:c.kinds=Compose(GiantKinds,c.points,false);break;
    case Abyss:c.kinds=Compose(AbyssKinds,c.points,false);break;
    case Hell:c.kinds=Compose(HellKinds,c.points,false);break;
    case Dragon:c.lead=Dragons.PickColor(QuestBoard.Home,false);break;
    case Legend:
     c.lead=Monsters.Available && (!Monsters.DeathKnightOn || Rand.Chance(.5f))?Monsters.Behir:Monsters.DeathKnight;
     c.kinds=c.lead==Monsters.Behir?Compose(BeastKinds,c.points*EscortShare,true):Compose(UndeadKinds,c.points*EscortShare,false);
     break;
   }
   return c.lead!=null || c.kinds.Count>0;
  }
  public static int DragonStage(BoardContract c)=>c.tier>=QuestBoard.MaxTier?1:0;

  // ---------- 文字 ----------
  public static string LeadLabel(BoardContract c){
   if(c.theme==Dragon && c.lead!=null && Dragons.Info.ContainsKey(c.lead))return Dragons.TitleFor(c.lead,DragonStage(c));
   if(c.theme==Legend && c.lead!=null)return Monsters.Label(c.lead);
   return null;
  }
  public static string Roster(BoardContract c){
   var parts=new List<string>();var lead=LeadLabel(c);if(lead!=null)parts.Add(lead);
   if(c.kinds!=null)foreach(var g in c.kinds.GroupBy(n=>n))parts.Add((K(g.Key)?.label??g.Key)+(g.Count()>1?" ×"+g.Count():""));
   if(c.theme==Dragon)parts.Add("本色护卫");
   return string.Join("、",parts);
  }
  public static string Title(BoardContract c){var lead=LeadLabel(c);return "魔物来袭："+(lead!=null?(c.theme==Legend?lead+"带队":lead):ThemeLabel(c.theme));}
  public static string Describe(BoardContract c){
   string who=c.theme==Beasts?"一群被魔力扭曲的魔兽（永久狂暴）":c.theme==Giants?"一支巨人劫掠队（"+(GiantFaction()?.Name??"敌对派系")+"）":c.theme==Abyss?"一股从深渊裂口里爬出的魔群":c.theme==Hell?"一支九狱军团的魔鬼小队":c.theme==Dragon?"一头"+LeadLabel(c)+"和它的护卫":c.theme==Legend?"传说中的"+LeadLabel(c)+"和它的随从":"一群魔物";
   return "冒险者公会的斥候带回消息："+who+"正朝殖民地而来。接下委托后 6～12 小时内来袭，打退所有来犯者（杀死、打倒、俘虏或赶走）就算完成。\n\n预计阵容："+Roster(c)+(c.theme==Dragon?"（真龙受重伤会飞走，也算打退）":"")+"。";
  }

  // ---------- 来袭 ----------
  public static List<Pawn> Spawn(BoardContract c,Map home,IntVec3 entry,out Faction letterFaction){
   letterFaction=null;var pawns=new List<Pawn>();
   switch(c.theme){
    case Beasts:pawns.AddRange(SpawnBeasts(c.kinds,home,entry));break;
    case Giants:{var f=GiantFaction()??Factions.Hostile;letterFaction=f;pawns.AddRange(SpawnGroup(c.kinds,f,home,entry));break;}
    case Abyss:{var f=AbyssRaces.Horde();letterFaction=f;pawns.AddRange(SpawnGroup(c.kinds,f,home,entry));break;}
    case Hell:{var f=PactEvents.Legion();letterFaction=f;pawns.AddRange(SpawnGroup(c.kinds,f,home,entry));break;}
    case Dragon:{
     var enc=Dragons.Raid(home,c.lead??Dragons.PickColor(home,false),DragonStage(c),c.points);
     if(enc!=null){letterFaction=enc.dragon?.Faction;pawns.Add(enc.dragon);pawns.AddRange(enc.guards);}
     break;
    }
    case Legend:{
     if(c.lead==Monsters.Behir){
      var b=Monsters.SpawnHunter(home,entry);if(b!=null){letterFaction=b.Faction;pawns.Add(b);}
      pawns.AddRange(SpawnBeasts(c.kinds,home,entry));
     }else{
      var k=Monsters.Make(Monsters.DeathKnight,GiantFaction()??Monsters.Beasts(),home.Tile);
      if(k!=null){
       letterFaction=k.Faction;GenSpawn.Spawn(k,entry,home);
       var escort=SpawnGroup(c.kinds,k.Faction,home,entry,false);
       var all=new List<Pawn>{k};all.AddRange(escort);
       LordMaker.MakeNewLord(k.Faction,new LordJob_AssaultColony(k.Faction,false,true,false,false,false,false,false),home,all);
       pawns.AddRange(all);
      }
     }
     break;
    }
   }
   pawns.RemoveAll(p=>p==null);
   return pawns;
  }
  // 同种的野兽一起走原版 GenerateAnimals（野性等级、精英、兽群首领照常由 BeastLevels 的补丁给）。
  static List<Pawn> SpawnBeasts(List<string> kinds,Map home,IntVec3 entry){
   var list=new List<Pawn>();if(kinds==null)return list;
   foreach(var g in kinds.GroupBy(n=>n)){
    var k=K(g.Key);if(k==null)continue;
    var made=AggressiveAnimalIncidentUtility.GenerateAnimals(k,home.Tile,k.combatPower*g.Count(),g.Count());
    foreach(var p in made){GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,home,8),home);p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true);list.Add(p);}
   }
   return list;
  }
  static List<Pawn> SpawnGroup(List<string> kinds,Faction f,Map home,IntVec3 entry,bool lord=true){
   var list=new List<Pawn>();if(kinds==null || f==null)return list;
   foreach(var n in kinds){var p=AbyssRaces.Make(n,f,home.Tile);if(p==null)continue;GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,home,6),home);list.Add(p);}
   if(lord && list.Count>0)LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,true,false,false,false,false,false),home,list);
   return list;
  }
 }
}
