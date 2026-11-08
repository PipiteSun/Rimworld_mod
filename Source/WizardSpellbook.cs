using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LichPhylactery {
 // 1.128.0 法师法术书（Peter 2026-11-08，见 ArtSource/WizardSpellbook-1.128.0-spec.md / report.md）。
 // 法术书存在法师的职业数据里（ClassData.book）：原有 12 个法师能力照旧按等级自动学会；每个法师等级另外从法术表自选 2 个已能施放环位的法术；
 // 戏法 1 级 3 个（魔法飞弹算一个）、4 级和 10 级各 +1；卷轴书桌上可以抄录残页 / 卷轴。
 // 备法：可准备数 = 法师等级 + 智力调整值（至少 1），戏法、仪式法术不占名额；只有准备了的法术出现在按钮栏。睡醒后或研读 1 小时后才能更换。
 // 仪式法术只要在法术书里就能原地引导 1 小时施放（不耗法力）；可升环的法术多花法力提升效果（右键按钮或窗口里选环）；专注法术同时只能一个，受到 10 点以上伤害要维持专注。
 // NPC 不用这些：新法术只能靠玩家的人自选 / 抄录，NPC 的法师照旧只有 12 个职业能力（备法也只对玩家的人生效）。
 public class WizardSpellExt : DefModExtension {
  public int circle;public string school;public bool ritual,concentration;
  public string upcast; // power：每环 +25% 效果；target：每环 +1 目标 / 道 / 具；radius：每环半径 +1；charge：1.129.0 延时爆裂火球的蓄力秒数（不加法力）
  public int perDay; // 1.129.0 每天次数（次元门 2）；8 环合计每天 2 次、9 环合计每天 1 次写在 Spellbook.DailyCap
 }
 public class SpellbookData : IExposable {
  public List<AbilityDef> book=new List<AbilityDef>(),cantrips=new List<AbilityDef>(),prepared=new List<AbilityDef>(),seen=new List<AbilityDef>(),copied=new List<AbilityDef>();
  public int picksUsed,notifiedLevel;public bool init,mayPrepare=true,wasAsleep;
  public Dictionary<AbilityDef,int> upcast=new Dictionary<AbilityDef,int>();
  public AbilityDef conc;public List<Pawn> concPawns=new List<Pawn>();public string concHediff;
  public AbilityDef ritual;
  public Pawn familiar;public int familiarDied=-1,sendingTick=-999999;
  public List<int> spoken=new List<int>(); // 亡者交谈问过的尸体（thingIDNumber）
  public Dictionary<string,int> daily=new Dictionary<string,int>(); // 1.129.0 每天次数：键 = 法术或 "c8"/"c9"，值 = 天数 × 1000 + 次数
  public Thing concThing;public string concKind; // 1.129.0 专注在区域（火墙、黑触手）或放逐上
  public AbilityDef contingency;public int contingencyUntil=-1,cloneReady=-1,scryUntil=-1;public bool wishLost;
  // 1.130.0 其他施法职业（CasterSpells.cs）：cls = 法术表 defName；granted = 由法术表给上的能力（取消准备 / 遗忘时收回）；arcanum = 邪术师秘法玄奥；
  // hiddenSpells = 已知法术里玩家隐藏的按钮；swaps = 已用的替换次数（每级 1 次）；secrets = 吟游诗人秘法奥秘；energy = 器械师 / 游侠今天用掉的法术能量。
  public string cls;public List<AbilityDef> granted=new List<AbilityDef>(),arcanum=new List<AbilityDef>(),hiddenSpells=new List<AbilityDef>(),secrets=new List<AbilityDef>();public int swaps;public float energy;
  public void ExposeData(){
   Scribe_Values.Look(ref cls,"cls");Scribe_Collections.Look(ref granted,"granted",LookMode.Def);Scribe_Collections.Look(ref arcanum,"arcanum",LookMode.Def);Scribe_Collections.Look(ref hiddenSpells,"hiddenSpells",LookMode.Def);Scribe_Collections.Look(ref secrets,"secrets",LookMode.Def);
   Scribe_Values.Look(ref swaps,"swaps");Scribe_Values.Look(ref energy,"energy");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){foreach(var l in new[]{granted,arcanum,hiddenSpells,secrets})l?.RemoveAll(x=>x==null);if(granted==null)granted=new List<AbilityDef>();if(arcanum==null)arcanum=new List<AbilityDef>();if(hiddenSpells==null)hiddenSpells=new List<AbilityDef>();if(secrets==null)secrets=new List<AbilityDef>();}
   Scribe_Collections.Look(ref book,"book",LookMode.Def);Scribe_Collections.Look(ref cantrips,"cantrips",LookMode.Def);Scribe_Collections.Look(ref prepared,"prepared",LookMode.Def);
   Scribe_Collections.Look(ref seen,"seen",LookMode.Def);Scribe_Collections.Look(ref copied,"copied",LookMode.Def);
   Scribe_Values.Look(ref picksUsed,"picksUsed");Scribe_Values.Look(ref notifiedLevel,"notifiedLevel");Scribe_Values.Look(ref init,"init");Scribe_Values.Look(ref mayPrepare,"mayPrepare",true);Scribe_Values.Look(ref wasAsleep,"wasAsleep");
   Scribe_Collections.Look(ref upcast,"upcast",LookMode.Def,LookMode.Value);
   Scribe_Defs.Look(ref conc,"conc");Scribe_Collections.Look(ref concPawns,"concPawns",LookMode.Reference);Scribe_Values.Look(ref concHediff,"concHediff");
   Scribe_Defs.Look(ref ritual,"ritual");
   Scribe_References.Look(ref familiar,"familiar");Scribe_Values.Look(ref familiarDied,"familiarDied",-1);Scribe_Values.Look(ref sendingTick,"sendingTick",-999999);
   Scribe_Collections.Look(ref spoken,"spoken",LookMode.Value);
   Scribe_Collections.Look(ref daily,"daily",LookMode.Value,LookMode.Value);Scribe_References.Look(ref concThing,"concThing");Scribe_Values.Look(ref concKind,"concKind");
   Scribe_Defs.Look(ref contingency,"contingency");Scribe_Values.Look(ref contingencyUntil,"contingencyUntil",-1);Scribe_Values.Look(ref cloneReady,"cloneReady",-1);Scribe_Values.Look(ref scryUntil,"scryUntil",-1);Scribe_Values.Look(ref wishLost,"wishLost");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    foreach(var l in new[]{book,cantrips,prepared,seen,copied})l?.RemoveAll(x=>x==null);
    if(book==null)book=new List<AbilityDef>();if(cantrips==null)cantrips=new List<AbilityDef>();if(prepared==null)prepared=new List<AbilityDef>();if(seen==null)seen=new List<AbilityDef>();if(copied==null)copied=new List<AbilityDef>();
    if(upcast==null)upcast=new Dictionary<AbilityDef,int>();foreach(var k in upcast.Keys.Where(k=>k==null).ToList())upcast.Remove(k);
    if(concPawns==null)concPawns=new List<Pawn>();concPawns.RemoveAll(x=>x==null);if(spoken==null)spoken=new List<int>();if(daily==null)daily=new Dictionary<string,int>();
   }
  }
 }

 public static class Spellbook {
  static readonly DefRef<LP_ClassDef> wizard=new DefRef<LP_ClassDef>("LP_Class_Wizard"),sorcerer=new DefRef<LP_ClassDef>("LP_Class_Sorcerer");
  public static LP_ClassDef Wizard=>wizard.Def;
  public const int PicksPerLevel=2,RitualTicks=2500,StudyTicks=2500,SendingTicks=60000,FamiliarRespawn=60000;
  public const float CopyBase=.6f,CopyPerInt=.05f,CopyMax=.95f,CopyScribe=.15f,ConcBase=.7f,ConcPerCon=.05f,ConcDamage=10f,UpPower=.25f,FamiliarRange=3f,FamiliarNear=10f;
  public static readonly Color Color=new Color(.7f,.5f,1f);
  // 1.128.0 之前就有的 12 个法师能力 + 术士的燃烧之手、魅惑人类（法师可以写进法术书，复用原能力）的环位与学派。
  static readonly Dictionary<string,int> BaseCircle=new Dictionary<string,int>{{"LP_MagicMissile",0},{"LP_MageArmor",1},{"LP_ShieldSpell",1},{"LP_Sleep",1},{"LP_MistyStep",2},{"LP_MirrorImage",2},
   {"LP_Fireball",3},{"LP_LightningBolt",3},{"LP_Haste",3},{"LP_WallOfStone",5},{"LP_Disintegrate",6},{"LP_Teleport",7},{"LP_BurningHands",1},{"LP_CharmPerson",1}};
  static readonly Dictionary<string,string> BaseSchool=new Dictionary<string,string>{{"LP_MagicMissile","塑能"},{"LP_MageArmor","防护"},{"LP_ShieldSpell","防护"},{"LP_Sleep","惑控"},{"LP_MistyStep","咒法"},{"LP_MirrorImage","幻术"},
   {"LP_Fireball","塑能"},{"LP_LightningBolt","塑能"},{"LP_Haste","变化"},{"LP_WallOfStone","塑能"},{"LP_Disintegrate","变化"},{"LP_Teleport","咒法"},{"LP_BurningHands","塑能"},{"LP_CharmPerson","惑控"}};
  public static readonly string[] Borrowed={"LP_BurningHands","LP_CharmPerson"};
  public static readonly string[] CircleNames={"戏法","一环","二环","三环","四环","五环","六环","七环","八环","九环"};
  public static string CircleName(int c)=>c>=0 && c<CircleNames.Length?CircleNames[c]:c+"环";
  // 卷轴 → 法术（抄录法术的配方也收这些卷轴）。
  public static readonly Dictionary<string,string> ScrollSpell=new Dictionary<string,string>{{"LP_ScrollTrueSeeing","LP_WS_DetectMagic"},{"LP_ScrollFleshToStone","LP_WS_HoldPerson"},{"LP_ScrollFearGaze","LP_WS_Fear"},{"LP_ScrollProtectionEvil","LP_WS_MagicCircle"},{"LP_ScrollNecroticBlast","LP_WS_VampiricTouch"}};

  // ---------- 法术表 ----------
  public static WizardSpellExt Ext(AbilityDef d)=>d?.GetModExtension<WizardSpellExt>();
  public static int Circle(AbilityDef d){if(d==null)return -1;var e=Ext(d);if(e!=null)return e.circle;var ce=CasterSpells.CExt(d);if(ce!=null && ce.circle>=0)return ce.circle;return BaseCircle.TryGetValue(d.defName,out int c)?c:-1;} // 1.130.0 施法职业法术表的新法术
  public static string School(AbilityDef d)=>d==null?"":Ext(d)?.school??CasterSpells.CExt(d)?.school??(BaseSchool.TryGetValue(d.defName,out var s)?s:"");
  public static bool IsSpell(AbilityDef d)=>Circle(d)>=0;
  public static bool Cantrip(AbilityDef d)=>Circle(d)==0;
  public static bool Ritual(AbilityDef d)=>Ext(d)?.ritual==true || CasterSpells.CExt(d)?.ritual==true;
  public static bool Concentration(AbilityDef d)=>Ext(d)?.concentration==true || CasterSpells.CExt(d)?.concentration==true;
  public static string Upcast(AbilityDef d)=>Ext(d)?.upcast??CasterSpells.CExt(d)?.upcast;
  public static bool Counted(AbilityDef d)=>IsSpell(d) && !Cantrip(d) && !Ritual(d);
  public static bool IsBorrowed(AbilityDef d)=>d!=null && Array.IndexOf(Borrowed,d.defName)>=0;
  static readonly int[] Mana={0,1,1,2,2,3,4,5,6,9}; // 1.129.0：4 环 2、5 环 3、6 环 4、7 环 5、8 环 6、9 环 9
  public static int ManaFor(int circle)=>circle<=0?0:circle<Mana.Length?Mana[circle]:9;
  public static int LevelFor(int circle)=>circle<=1?1:circle==2?3:circle==3?5:circle*2-1;
  public static int MaxCircle(int level)=>level<=0?0:Mathf.Min(9,(level+1)/2); // 1 级一环、3 级二环……17 级九环
  public static int CantripCap(int level)=>level<=0?0:3+(level>=4?1:0)+(level>=10?1:0);
  static List<AbilityDef> news;static int newsCount=-1;
  public static List<AbilityDef> New{get{if(news==null || newsCount!=DefDatabase<AbilityDef>.DefCount){news=DefDatabase<AbilityDef>.AllDefsListForReading.Where(d=>Ext(d)!=null).OrderBy(d=>Ext(d).circle).ThenBy(d=>d.defName).ToList();newsCount=DefDatabase<AbilityDef>.DefCount;}return news;}}
  public static IEnumerable<AbilityDef> BorrowedDefs=>Borrowed.Select(n=>DefCache<AbilityDef>.Get(n)).Where(d=>d!=null);
  // 1.131.0 性能热修：可写进书的法术表只取决于定义，按 DefCount 记住一份列表和集合（原来每次 Contains / 过滤都重新串 LINQ 走一遍）。
  static List<AbilityDef> bookable,cantripList;static HashSet<AbilityDef> bookableSet;static int bookableCount=-1;
  static void EnsureBookable(){var n=New;if(bookable!=null && bookableCount==DefDatabase<AbilityDef>.DefCount)return;bookable=n.Where(d=>!Cantrip(d)).Concat(BorrowedDefs).ToList();bookableSet=new HashSet<AbilityDef>(bookable);cantripList=n.Where(Cantrip).ToList();bookableCount=DefDatabase<AbilityDef>.DefCount;}
  public static IEnumerable<AbilityDef> Bookable{get{EnsureBookable();return bookable;}}
  public static bool IsBookable(AbilityDef d){EnsureBookable();return d!=null && bookableSet.Contains(d);}
  public static IEnumerable<AbilityDef> CantripList{get{EnsureBookable();return cantripList;}}
  // 1.131.0 性能热修：标签只取决于定义，按法术记住（窗口每行每帧都拼一次）。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<AbilityDef,string> tags=new System.Collections.Concurrent.ConcurrentDictionary<AbilityDef,string>();
  public static string Tags(AbilityDef d){if(d==null)return "";if(tags.TryGetValue(d,out var t))return t;var l=new List<string>();var s=School(d);if(!s.NullOrEmpty())l.Add(s);if(Ritual(d))l.Add("仪式");if(Concentration(d))l.Add("专注");if(Upcast(d)!=null)l.Add("可升环");t=string.Join("·",l);tags[d]=t;return t;}

  // ---------- 数据 ----------
  public static int Level(Hediff_Adventurer h)=>Wizard==null?0:ClassSystem.Level(h,Wizard);
  public static int Level(Pawn p)=>Level(Adventurer.Get(p));
  public static SpellbookData Data(Hediff_Adventurer h){if(h?.classes==null)return null;if(h.classes.book==null && Level(h)>0){h.classes.book=new SpellbookData();CasterSpells.Touch();}return h.classes.book;}
  public static SpellbookData Data(Pawn p)=>Data(Adventurer.Get(p));
  public static bool Knows(Pawn p,AbilityDef d)=>d!=null && p?.abilities?.GetAbility(d)!=null;
  public static bool Player(Pawn p)=>p?.Faction!=null && p.Faction.IsPlayer;
  public static int IntMod(Pawn p)=>Adventurer.Mod(Adventurer.Score(p,Adventurer.Int));
  public static int PrepCap(Hediff_Adventurer h)=>Mathf.Max(1,Level(h)+(h?.pawn==null?0:IntMod(h.pawn)));
  // 法术书里的法术：这个人会的、属于法师的法术（自动学会的 12 个 + 自选 / 抄录 / 戏法），加上写进书里的术士法术。
  public static bool InBook(Hediff_Adventurer h,AbilityDef a){
   if(!IsSpell(a) || !Knows(h?.pawn,a))return false;var d=h.classes?.book;
   return ClassSystem.ClassOf(a)==Wizard && !IsBorrowed(a) || d!=null && (d.book.Contains(a) || d.cantrips.Contains(a));
  }
  public static List<AbilityDef> BookList(Hediff_Adventurer h){
   var p=h?.pawn;if(p?.abilities==null)return new List<AbilityDef>();
   return p.abilities.abilities.Select(a=>a.def).Where(a=>InBook(h,a)).Distinct().OrderBy(Circle).ThenBy(a=>a.label).ToList();
  }
  public static int PreparedCount(Hediff_Adventurer h){var d=h?.classes?.book;if(d==null)return 0;int n=0;for(int i=0;i<d.prepared.Count;i++){var a=d.prepared[i];if(Counted(a) && InBook(h,a))n++;}return n;}
  public static int PicksDue(Hediff_Adventurer h){var d=Data(h);return d==null?0:Mathf.Max(0,PicksPerLevel*Level(h)-d.picksUsed);}
  public static IEnumerable<AbilityDef> PickCandidates(Hediff_Adventurer h){int max=MaxCircle(Level(h));var p=h?.pawn;return Bookable.Where(a=>Circle(a)<=max && !Knows(p,a));}
  public static int CantripsKnown(Hediff_Adventurer h){var d=Data(h);if(d==null)return 0;var mm=DefCache<AbilityDef>.Get("LP_MagicMissile");return d.cantrips.Count+(Knows(h.pawn,mm)?1:0);}
  public static int CantripsDue(Hediff_Adventurer h)=>Mathf.Max(0,CantripCap(Level(h))-CantripsKnown(h));
  public static IEnumerable<AbilityDef> CantripCandidates(Hediff_Adventurer h)=>CantripList.Where(a=>!Knows(h?.pawn,a));

  // ---------- 学习 ----------
  public static string CannotPick(Hediff_Adventurer h,AbilityDef a){
   if(Level(h)<=0)return "不是法师";if(!IsSpell(a) || Cantrip(a))return "不是能写进法术书的法术";
   if(Knows(h.pawn,a))return "已经在法术书里了";
   if(Circle(a)>MaxCircle(Level(h)))return CircleName(Circle(a))+"要法师 "+LevelFor(Circle(a))+" 级";
   if(!IsBookable(a))return "不在法师法术表里";
   if(a.defName=="LP_WS_Wish" && Data(h)?.wishLost==true)return "祈愿术已经永远失去了";
   return null;
  }
  public static bool Pick(Hediff_Adventurer h,AbilityDef a){
   if(PicksDue(h)<=0 || CannotPick(h,a)!=null)return false;
   var d=Data(h);d.picksUsed++;Write(h,a);return true;
  }
  // 写进法术书（自选或抄录）：学会能力；备法有空位时自动准备。
  public static void Write(Hediff_Adventurer h,AbilityDef a){
   var d=Data(h);if(d==null || a==null)return;if(!d.book.Contains(a))d.book.Add(a);CasterSpells.Touch();
   var p=h.pawn;if(p?.abilities!=null && p.abilities.GetAbility(a)==null)p.abilities.GainAbility(a);
   Sync(h);
  }
  public static string CannotCantrip(Hediff_Adventurer h,AbilityDef a){
   if(Level(h)<=0)return "不是法师";if(!Cantrip(a) || Ext(a)==null)return "不是戏法";if(Knows(h.pawn,a))return "已经会了";if(CantripsDue(h)<=0)return "戏法名额已满";return null;
  }
  public static bool LearnCantrip(Hediff_Adventurer h,AbilityDef a){
   if(CannotCantrip(h,a)!=null)return false;var d=Data(h);d.cantrips.Add(a);CasterSpells.Touch();if(h.pawn.abilities.GetAbility(a)==null)h.pawn.abilities.GainAbility(a);Sync(h);return true;
  }

  // ---------- 备法 ----------
  public static bool Prepared(Pawn p,AbilityDef a){var d=Adventurer.Get(p)?.classes?.book;return d!=null && d.prepared.Contains(a);}
  public static string CannotToggle(Hediff_Adventurer h,AbilityDef a,bool on){
   var d=Data(h);if(d==null)return "不是法师";if(Cantrip(a))return "戏法常备，不用准备";
   if(!d.mayPrepare)return "睡醒后、或在研究台 / 卷轴书桌研读 1 小时后才能更换";
   if(on && Counted(a) && PreparedCount(h)>=PrepCap(h))return "已经准备满了（"+PreparedCount(h)+" / "+PrepCap(h)+"）";
   return null;
  }
  public static bool SetPrepared(Hediff_Adventurer h,AbilityDef a,bool on){
   if(CannotToggle(h,a,on)!=null)return false;var d=Data(h);
   if(on){if(!d.prepared.Contains(a))d.prepared.Add(a);}else d.prepared.Remove(a);
   CasterSpells.Touch();return true;
  }
  // 玩家的人：法师法术没准备就不显示按钮、也不能施放（自动施放同样跳过）。
  public static bool Hidden(Pawn p,AbilityDef def){
   if(def==null || !Player(p))return false;
   var cs=CasterSpells.HiddenOverride(p,def);if(cs.HasValue)return cs.Value; // 1.130.0 别的施法职业备好 / 已知（或玩家在窗口里隐藏了）
   if(!IsSpell(def) || Cantrip(def))return false;
   var h=Adventurer.Get(p);var d=h?.classes?.book;if(d==null || !d.init || Level(h)<=0)return false;
   if(IsBorrowed(def) && CastClass(p,def)!=Wizard)return false; // 术士自己的燃烧之手、魅惑人类
   if(!IsBorrowed(def) && ClassSystem.ClassOf(def)!=Wizard)return false;
   return !d.prepared.Contains(def);
  }
  public static string CannotCast(Pawn p,AbilityDef def){
   if(Hidden(p,def))return "没有准备这个法术（点“备法”在法术书里准备；睡醒后或研读 1 小时后可以更换）";
   if(Hediff_WS.Live>0 && (Items.Has(p,"LP_WS_Polymorphed")))return "变成兔子了，放不了法术";
   if(Hediff_Feebleminded.Live>0 && Items.Has(p,"LP_WS_Feebleminded"))return "被弱智术打碎了心智，放不了法术"; // 1.131.0 性能热修：没人被弱智时不扫状态表（每帧每个能力都走到这里）
   int cap=DailyCap(p,def);if(cap>0 && UsedToday(p,def)>=cap)return DailyText(p,def)+"今天的次数已经用完（"+cap+" / "+cap+"）";
   return null;
  }
  // 1.129.0 每天次数：8 环合计 2 次、9 环合计 1 次，个别法术自己写（次元门 2 次）。1.130.0 别的施法职业按施放职业的环位算、秘法玄奥每个每天 1 次。
  public static int DailyCap(AbilityDef d){var e=Ext(d);if(e!=null && e.perDay>0)return e.perDay;var ce=CasterSpells.CExt(d);if(ce!=null && ce.perDay>0)return ce.perDay;int c=Circle(d);return Ext(d)==null && ce==null?0:c==8?2:c==9?1:0;}
  public static int DailyCap(Pawn p,AbilityDef d){var o=CasterSpells.DailyOverride(p,d);return o??DailyCap(d);}
  static string DailyKey(Pawn p,AbilityDef d){var k=CasterSpells.DailyKey(p,d);if(k!=null)return k;return Ext(d)?.perDay>0 || CasterSpells.CExt(d)?.perDay>0?d.defName:"c"+Circle(d);}
  static string DailyText(Pawn p,AbilityDef d){var k=DailyKey(p,d);return k.StartsWith("c") && k.Length<=2?CircleName(Circle(d))+"法术":d.label;}
  static int Today=>Find.TickManager.TicksGame/60000;
  static SpellbookData DailyBook(Pawn p,AbilityDef d)=>CasterSpells.BookFor(p,d)??Adventurer.Get(p)?.classes?.book;
  public static int UsedToday(Pawn p,AbilityDef d){var b=DailyBook(p,d);if(b==null || !b.daily.TryGetValue(DailyKey(p,d),out int v))return 0;return v/1000==Today?v%1000:0;}
  public static void UseDaily(Pawn p,AbilityDef d){if(DailyCap(p,d)<=0)return;var b=CasterSpells.BookFor(p,d)??Data(p);if(b==null)return;int n=UsedToday(p,d);b.daily[DailyKey(p,d)]=Today*1000+n+1;}

  // ---------- 职业归属 / 强度 ----------
  public static LP_ClassDef ClassOf(AbilityDef d)=>Ext(d)!=null?Wizard:null;
  // 写进法术书的术士法术：自己没有术士等级时算法师的（扣法师法力、用奥术强度）。
  public static LP_ClassDef CastClass(Pawn p,AbilityDef d){
   if(!IsBorrowed(d))return null;var h=Adventurer.Get(p);var b=h?.classes?.book;if(b==null || !b.book.Contains(d) || Level(h)<=0)return null;
   return sorcerer.Def!=null && ClassSystem.Level(h,sorcerer.Def)>0?null:Wizard;
  }
  static Dictionary<CompProperties_ClassEffect,AbilityDef> borrowedProps;
  public static StatDef StatFor(Pawn p,CompProperties_ClassEffect props){
   if(props==null)return null;
   if(borrowedProps==null){borrowedProps=new Dictionary<CompProperties_ClassEffect,AbilityDef>();foreach(var d in BorrowedDefs){var e=d.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();if(e!=null)borrowedProps[e]=d;}}
   return borrowedProps.TryGetValue(props,out var def) && CastClass(p,def)==Wizard?DefCache<StatDef>.Get("LP_ArcanePower"):null;
  }

  // ---------- 升环 ----------
  public const int MaxCharge=5;
  public static bool Charge(AbilityDef d)=>Upcast(d)=="charge";
  public static int MaxUp(Pawn p,AbilityDef d)=>Upcast(d)==null?0:Charge(d)?MaxCharge:Mathf.Max(0,MaxCircleFor(p,d)-CasterSpells.CircleFor(p,d));
  public static int MaxCircleFor(Pawn p,AbilityDef d)=>CasterSpells.MaxCircleFor(p,d)??MaxCircle(Level(p)); // 1.130.0 别的施法职业按那个职业的最高环
  public static int UpLevel(Pawn p,AbilityDef d){if(Upcast(d)==null)return 0;var b=CasterSpells.BookFor(p,d)??Data(p);if(b==null || !b.upcast.TryGetValue(d,out int n))return 0;return Mathf.Clamp(n,0,MaxUp(p,d));}
  public static void SetUp(Pawn p,AbilityDef d,int n){var b=CasterSpells.BookFor(p,d)??Data(p);if(b==null)return;n=Mathf.Clamp(n,0,MaxUp(p,d));if(n<=0)b.upcast.Remove(d);else b.upcast[d]=n;}
  public static int UpcastCost(Pawn p,AbilityDef d)=>Charge(d)?0:UpLevel(p,d); // 每升 1 环 +1 法力；蓄力不加法力
  [ThreadStatic] public static int CurUp;[ThreadStatic] static int curPower;
  public static void BeginCast(Ability_Class a){CurUp=a==null?0:UpLevel(a.pawn,a.def);curPower=Upcast(a?.def)=="power"?CurUp:0;}
  public static void EndCast(){CurUp=0;curPower=0;}
  public static float PowerFactor(CompProperties_ClassEffect e)=>curPower>0?1f+UpPower*curPower:1f;
  public static string UpText(Pawn p,AbilityDef d,int up){
   switch(Upcast(d)){case "power":return "效果 ×"+(1f+UpPower*up).ToString("0.##");case "target":return "+"+up+" 个目标";case "radius":return "半径 +"+up;case "charge":return "蓄力 "+up+" 秒";}return "";
  }
  public static bool RightClick(Ability_Class a,Rect r){
   if(a==null || Event.current.type!=EventType.MouseDown || Event.current.button!=1 || !Mouse.IsOver(r) || Upcast(a.def)==null || !Player(a.pawn) || Level(a.pawn)<=0 && CasterSpells.BookFor(a.pawn,a.def)==null)return false;
   Find.WindowStack.Add(new Dialog_Upcast(a.pawn,a.def));Event.current.Use();return true;
  }

  // ---------- 专注 ----------
  public static readonly HashSet<Pawn> Concentrating=new HashSet<Pawn>(),Fliers=new HashSet<Pawn>(); // Fliers：飞行术，走路无视地形（ClassWoodstride）
  public static float? RollOverride; // 检查用
  static float Roll=>RollOverride??Rand.Value;
  public static float ConcChance(Pawn p)=>Mathf.Clamp01(ConcBase+ConcPerCon*Adventurer.Mod(Adventurer.Score(p,Adventurer.Con)));
  // 专注的对象：状态（concHediff 挂在 concPawns 上）、区域（concThing：火墙、黑触手）、放逐（concKind = "banish"：被放逐的人提前回来）。
  public static void Concentrate(Pawn p,AbilityDef def,IEnumerable<Pawn> targets,string hediff,Thing zone=null,string kind=null){
   var d=ConcBook(p);if(d==null)return;
   if(d.conc!=null)EndConc(p,d,false);
   d.conc=def;d.concHediff=hediff;d.concPawns=(targets??Enumerable.Empty<Pawn>()).Where(x=>x!=null).Distinct().ToList();d.concThing=zone;d.concKind=kind;Concentrating.Add(p);
  }
  // 1.130.0 一个人同时只有一个专注：有法师法术书就记在法术书里，否则记在第一个施法职业的法术表数据里。
  public static SpellbookData ConcBook(Pawn p)=>Data(p)??CasterSpells.FirstBook(p);
  public static bool ConcActive(SpellbookData d){
   if(d.conc==null)return false;
   if(d.concThing!=null)return d.concThing.Spawned && !d.concThing.Destroyed;
   if(d.concKind=="banish")return d.concPawns.Any(x=>Banishment.IsBanished(x));
   var hd=Items.Hediff(d.concHediff);return hd!=null && d.concPawns.Any(x=>x!=null && !x.Dead && x.health.hediffSet.HasHediff(hd));
  }
  public static void EndConc(Pawn p,SpellbookData d,bool broken){
   if(d==null || d.conc==null)return;var label=d.conc.label;
   foreach(var x in d.concPawns.ToList()){if(x==null)continue;if(d.concKind=="banish")Banishment.Return(x);else if(!x.Dead && d.concHediff!=null)Items.Remove(x,d.concHediff);}
   if(d.concThing!=null && !d.concThing.Destroyed)d.concThing.Destroy();
   d.conc=null;d.concHediff=null;d.concPawns.Clear();d.concThing=null;d.concKind=null;Concentrating.Remove(p);
   if(broken){Items.Mote(p,"失去专注："+label,Color);if(Player(p) && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"受伤失去了专注，"+label+"结束。",p,MessageTypeDefOf.NegativeEvent,false);}
  }
  public static bool Damaged(Pawn p,float amount){
   if(amount<ConcDamage || !Concentrating.Contains(p))return false;var d=ConcBook(p);if(d?.conc==null){Concentrating.Remove(p);return false;}
   if(Roll<ConcChance(p)){Items.Mote(p,"维持专注",Color);return false;}
   EndConc(p,d,true);return true;
  }

  // ---------- 施放之后 ----------
  public static bool Free(Pawn p,CompProperties_ClassEffect props)=>props?.effect=="WS_VampiricTouch" && Items.Has(p,"LP_WS_VampiricTouch") || props?.effect=="WS_Sunbeam" && Items.Has(p,"LP_WS_SunbeamCharge");
  public static void AfterCast(Ability_Class a){
   var p=a?.pawn;if(p==null)return;
   if(Hediff_WS.Live>0 && Items.Has(p,"LP_WS_Invisible")){Items.Remove(p,"LP_WS_Invisible");Items.Mote(p,"现形",Color);}
   if(Hediff_WS.Live>0 && a.def.hostile && Items.Has(p,"LP_WS_Ethereal")){Items.Remove(p,"LP_WS_Ethereal");Items.Mote(p,"回到物质位面",Color);} // 1.129.0 以太化：放伤害法术就回来
   UseDaily(p,a.def);
   if(IsSpell(a.def) && Items.Has(p,"LP_WS_TimeStop") && a.def.defName!="LP_WS_TimeStop"){a.ResetCooldown();Items.Mote(p,"时间停止：不进冷却",Color);}
  }

  // ---------- 每 250 刻 / 同步 ----------
  public static int Sync(Hediff_Adventurer h){
   var p=h?.pawn;if(p?.abilities==null || Level(h)<=0)return 0;if(h.classes.book==null && !Player(p))return 0; // NPC 法师不建法术书（被招募后 Tick250 再建）
   var d=Data(h);if(d==null)return 0;int n=0;
   foreach(var a in d.book.Concat(d.cantrips).ToList())if(a!=null && p.abilities.GetAbility(a)==null){p.abilities.GainAbility(a);n++;}
   var known=BookList(h);
   if(!d.init){d.init=true;d.mayPrepare=true;foreach(var a in known){if(!Cantrip(a) && !d.prepared.Contains(a))d.prepared.Add(a);if(!d.seen.Contains(a))d.seen.Add(a);}} // 老存档 / 新法师：已学会的都先准备上（超出上限也保留）
   else foreach(var a in known)if(!d.seen.Contains(a)){d.seen.Add(a);if(!Cantrip(a) && !d.prepared.Contains(a) && (!Counted(a) || PreparedCount(h)<PrepCap(h)))d.prepared.Add(a);} // 新学会的：有空位就自动准备
   CasterSpells.Touch();return n;
  }
  public static void Tick250(Hediff_Adventurer h){
   var d=h?.classes?.book;var p=h?.pawn;if(p==null || p.Dead)return;
   if(d==null){if(Level(h)<=0 || !Player(p))return;d=Data(h);Sync(h);}
   if(Level(h)<=0)return;
   bool asleep=!p.Awake();if(d.wasAsleep && !asleep)d.mayPrepare=true;d.wasAsleep=asleep; // 睡醒：可以更换备法
   if(d.conc!=null){if(!ConcActive(d))EndConc(p,d,false);else Concentrating.Add(p);}
   Familiars.Tick(p,d);
   if(d.contingency!=null)WizardSpells2.ContingencyPawns.Add(p); // 1.129.0 应急术（读档后重新登记）
   if(Player(p) && !Adventurer.TestSuppressed && d.notifiedLevel<Level(h) && (PicksDue(h)>0 || CantripsDue(h)>0)){
    d.notifiedLevel=Level(h);
    Messages.Message(p.LabelShort+"的法术书：待选法术 "+PicksDue(h)+" 个、戏法 "+CantripsDue(h)+" 个（点“备法”按钮选择）。",p,MessageTypeDefOf.PositiveEvent,false);
   }
  }

  // ---------- 仪式施法 / 研读 ----------
  public static readonly DefRef<JobDef> ritualJob=new DefRef<JobDef>("LP_WS_RitualCast"),studyJob=new DefRef<JobDef>("LP_WS_Study");
  public static string CannotRitual(Pawn p,AbilityDef def){
   var h=Adventurer.Get(p);if(!Ritual(def))return "不是仪式法术";if(!InBook(h,def))return "不在法术书里";
   if(p.Drafted)return "仪式施法要在非征召状态下进行";if(p.Downed || !p.Spawned)return "现在不能施法";
   if(ClassMartial2.Raging(p))return "狂暴中无法施法";
   var ab=p.abilities?.GetAbility(def);if(ab!=null && ab.OnCooldown && def.cooldownTicksRange.max>=60000)return "冷却中（还要 "+(ab.CooldownTicksRemaining/60000f).ToString("0.#")+" 天）";
   int cap=DailyCap(p,def);if(cap>0 && UsedToday(p,def)>=cap)return "今天的次数已经用完";
   if(Items.Has(p,"LP_WS_Feebleminded"))return "被弱智术打碎了心智";
   return null;
  }
  public static bool StartRitual(Pawn p,AbilityDef def,LocalTargetInfo target){
   if(CannotRitual(p,def)!=null || ritualJob.Def==null)return false;
   var props=def.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();if(props==null)return false;
   var why=ClassEffects.Invalid(p,props,target);if(why!=null){Messages.Message(why,MessageTypeDefOf.RejectInput,false);return false;}
   var d=CasterSpells.BookFor(p,def)??Data(p);if(d==null)return false;d.ritual=def;CasterSpells.Touch();
   var job=JobMaker.MakeJob(ritualJob.Def,target.IsValid?target:new LocalTargetInfo(p.Position));
   return p.jobs.TryTakeOrderedJob(job,JobTag.Misc);
  }
  public static void BeginRitual(Pawn p,AbilityDef def){
   if(CannotRitual(p,def)!=null){Messages.Message(CannotRitual(p,def),MessageTypeDefOf.RejectInput,false);return;}
   if(!def.targetRequired){StartRitual(p,def,new LocalTargetInfo(p));return;}
   var tp=def.verbProperties?.targetParams??new TargetingParameters();
   Find.Targeter.BeginTargeting(tp,t=>StartRitual(p,def,t),p);
  }
  public static bool FinishRitual(Pawn p,LocalTargetInfo target){
   var d=RitualBook(p);var def=d?.ritual;if(def==null)return false;d.ritual=null;CasterSpells.Touch();
   var props=def.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();if(props==null)return false;
   var t=def.targetRequired?target:new LocalTargetInfo(p);
   var why=ClassEffects.Invalid(p,props,t);if(why!=null){if(Player(p))Messages.Message(def.LabelCap+"：仪式失败——"+why,p,MessageTypeDefOf.RejectInput,false);return false;}
   EndCast();ClassEffects.Apply(p,def,props,t);Items.Mote(p,"仪式："+def.label,Color);
   UseDaily(p,def);var ab=p.abilities?.GetAbility(def);if(ab!=null && def.cooldownTicksRange.max>=60000)ab.StartCooldown(def.cooldownTicksRange.max); // 1.129.0 每天一次 / 每几天一次的仪式也算次数
   return true;
  }
  public static SpellbookData RitualBook(Pawn p){var d=Data(p);if(d?.ritual!=null)return d;return CasterSpells.Books(p).FirstOrDefault(b=>b.ritual!=null)??d;}
  public static Thing StudyBench(Pawn p){
   if(p?.Map==null)return null;
   return p.Map.listerBuildings.allBuildingsColonist.Where(b=>(b is Building_ResearchBench || b.def.defName=="LP_ScribeDesk") && !b.IsForbidden(p) && p.CanReserveAndReach(b,PathEndMode.InteractionCell,Danger.Some))
    .OrderBy(b=>b.Position.DistanceToSquared(p.Position)).FirstOrDefault();
  }
  public static bool StartStudy(Pawn p){
   var b=StudyBench(p);if(b==null || studyJob.Def==null){Messages.Message("附近没有能用的研究台或卷轴书桌。",MessageTypeDefOf.RejectInput,false);return false;}
   return p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(studyJob.Def,b),JobTag.Misc);
  }
  public static void FinishStudy(Pawn p){var d=Data(p);foreach(var cb in CasterSpells.Books(p))cb.mayPrepare=true;if(d==null)return;d.mayPrepare=true;if(Player(p))Messages.Message(p.LabelShort+"研读完法术书，可以重新备法了。",p,MessageTypeDefOf.PositiveEvent,false);}

  // ---------- 抄录 ----------
  public static AbilityDef SpellOf(Thing t){
   if(t==null)return null;var c=t.TryGetComp<CompSpellPage>();if(c!=null)return c.Spell;
   return ScrollSpell.TryGetValue(t.def.defName,out var n)?DefCache<AbilityDef>.Get(n):null;
  }
  public static float CopyChance(Pawn p)=>Mathf.Min(CopyMax,CopyBase+CopyPerInt*IntMod(p)+(Scribe(p)?CopyScribe:0f));
  public static bool Scribe(Pawn p)=>ClassSubclass.Has(p,Subclass3.Scribes);
  public static string CannotCopy(Pawn p,AbilityDef a){
   var h=Adventurer.Get(p);if(Level(h)<=0)return "只有法师能抄录法术";if(a==null)return "这张残页上没有法术";
   if(Knows(p,a))return "已经会「"+a.label+"」了";
   if(Circle(a)>MaxCircle(Level(h)))return CircleName(Circle(a))+"要法师 "+LevelFor(Circle(a))+" 级才能抄录";
   return null;
  }
  // 返回 "ok" / "failed" / 不能抄录的原因。
  public static string Copy(Pawn p,AbilityDef a,float roll){
   var why=CannotCopy(p,a);if(why!=null)return why;
   if(roll>=CopyChance(p)){Items.Mote(p,"抄录失败",Color);return "failed";}
   var h=Adventurer.Get(p);Write(h,a);var d=Data(h);if(!d.copied.Contains(a))d.copied.Add(a);
   Items.Mote(p,"抄录："+a.label,Color);return "ok";
  }
  // 敌方施法者死亡 15% 掉一张残页（按等级决定最高环）。
  public const float PageDrop=.15f;
  public static ThingDef PageDef(int circle)=>DefCache<ThingDef>.Get("LP_WS_SpellPage"+Mathf.Clamp(circle,1,3));
  public static Thing MakePage(int circle,AbilityDef spell=null){
   var def=PageDef(circle);if(def==null)return null;var t=ThingMaker.MakeThing(def);var c=t.TryGetComp<CompSpellPage>();if(c!=null && spell!=null)c.spell=spell;return t;
  }
  public static int DropCircle(Pawn dead){int lvl=Adventurer.Get(dead)?.level??1;int max=lvl>=5?3:lvl>=3?2:1;return Rand.RangeInclusive(1,max);}
 }

 // 残页：每张随机写着一个该环位的法师法术（第一次生成时决定）。
 public class CompProperties_SpellPage : CompProperties {public int circle=1;public CompProperties_SpellPage(){compClass=typeof(CompSpellPage);}}
 public class CompSpellPage : ThingComp {
  public AbilityDef spell;
  public CompProperties_SpellPage Props=>(CompProperties_SpellPage)props;
  public AbilityDef Spell{get{if(spell==null){var l=Spellbook.Bookable.Where(a=>Spellbook.Circle(a)==Props.circle && !Spellbook.IsBorrowed(a)).ToList();if(l.Count>0)spell=l.RandomElement();}return spell;}}
  public override void PostExposeData(){base.PostExposeData();Scribe_Defs.Look(ref spell,"lpSpell");}
  public override string TransformLabel(string label)=>Spell==null?label:label+"："+Spell.label;
  public override string CompInspectStringExtra()=>Spell==null?null:"法术："+Spell.label+"（"+Spellbook.CircleName(Spellbook.Circle(Spell))+"，"+Spellbook.Tags(Spell)+"）";
  public override bool AllowStackWith(Thing other)=>false;
 }
 // 抄录法术：消耗残页或卷轴 + 白银；ConsumeIngredient 时记下法术（Destroy 之后读不到）。
 public class RecipeWorker_CopySpell : RecipeWorker {
  [ThreadStatic] static AbilityDef pending;[ThreadStatic] static ThingDef pendingDef;
  public override void ConsumeIngredient(Thing ingredient,RecipeDef r,Map map){
   var s=Spellbook.SpellOf(ingredient);if(s!=null){pending=s;pendingDef=ingredient.def;}
   base.ConsumeIngredient(ingredient,r,map);
  }
  public override void Notify_IterationCompleted(Pawn billDoer,List<Thing> ingredients){
   base.Notify_IterationCompleted(billDoer,ingredients);
   var spell=pending;var src=pendingDef;pending=null;pendingDef=null;if(billDoer==null)return;
   int silver=recipe.ingredients.Where(i=>i.filter.Allows(ThingDefOf.Silver)).Sum(i=>Mathf.RoundToInt(i.GetBaseCount()));
   var res=Spellbook.Copy(billDoer,spell,Rand.Value);
   if(res=="ok" || res=="failed"){
    if(Spellbook.Scribe(billDoer) && silver>0)Refund(billDoer,ThingDefOf.Silver,silver/2); // 书士会：花费减半
    if(Spellbook.Player(billDoer))Messages.Message(billDoer.LabelShort+(res=="ok"?"把「"+spell.label+"」抄进了法术书。":"抄录「"+(spell?.label??"?")+"」失败，材料损失了。"),billDoer,res=="ok"?MessageTypeDefOf.PositiveEvent:MessageTypeDefOf.NegativeEvent,false);
    return;
   }
   // 不能抄（已经会了 / 环位不够）：材料退回。
   if(src!=null){Thing back=src.HasComp(typeof(CompSpellPage))?Spellbook.MakePage(src.GetCompProperties<CompProperties_SpellPage>()?.circle??1,spell):ThingMaker.MakeThing(src);Refund(billDoer,back);}
   if(silver>0)Refund(billDoer,ThingDefOf.Silver,silver);
   if(Spellbook.Player(billDoer))Messages.Message(billDoer.LabelShort+"没有抄录："+res+"，材料已退回。",billDoer,MessageTypeDefOf.RejectInput,false);
  }
  static void Refund(Pawn p,ThingDef def,int count){if(count<=0)return;var t=ThingMaker.MakeThing(def);t.stackCount=count;Refund(p,t);}
  static void Refund(Pawn p,Thing t){if(t==null)return;if(p.Spawned)GenPlace.TryPlaceThing(t,p.Position,p.Map,ThingPlaceMode.Near);else t.Destroy();}
 }

 // ---------- 工作 ----------
 public class JobDriver_SpellRitual : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOn(()=>pawn.Drafted || Spellbook.RitualBook(pawn)?.ritual==null);
   var wait=Toils_General.Wait(Spellbook.RitualTicks).WithProgressBarToilDelay(TargetIndex.A);
   wait.AddPreInitAction(()=>{if(job.targetA.IsValid && job.targetA.Cell!=pawn.Position)pawn.rotationTracker.FaceTarget(job.targetA);});
   wait.tickAction=()=>{if(pawn.IsHashIntervalTick(120))FleckMaker.ThrowDustPuffThick(pawn.DrawPos,pawn.Map,1f,Spellbook.Color);};
   yield return wait;
   yield return Toils_General.Do(()=>Spellbook.FinishRitual(pawn,job.targetA));
  }
 }
 public class JobDriver_SpellStudy : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.InteractionCell);
   var wait=Toils_General.Wait(Spellbook.StudyTicks).WithProgressBarToilDelay(TargetIndex.A);
   wait.tickAction=()=>pawn.skills?.Learn(SkillDefOf.Intellectual,.05f);
   yield return wait;
   yield return Toils_General.Do(()=>Spellbook.FinishStudy(pawn));
  }
 }

 // ==================== 窗口 ====================
 public class Dialog_Spellbook : Window {
  readonly Pawn pawn;int tab;Vector2 scroll;string armed;bool changed;
  List<AbilityDef> rowsCache;int rowsTab=-1,rowsVer=-1,rowsTick=-1; // 1.131.0 性能热修：法术行按（法术数据版本，游戏刻）记住，不再每帧过滤 + 排序全表
  public override Vector2 InitialSize=>LPUI.Fit(780f,700f);
  public Dialog_Spellbook(Pawn p,int tab=-1){pawn=p;doCloseX=true;doCloseButton=true;closeOnClickedOutside=false;absorbInputAroundWindow=true;forcePause=true;
   var h=Adventurer.Get(p);this.tab=tab>=0?tab:Spellbook.CantripsDue(h)>0 && Spellbook.PicksDue(h)<=0?2:Spellbook.PicksDue(h)>0?1:0;}
  public override void PostClose(){base.PostClose();var d=Spellbook.Data(pawn);if(changed && d!=null)d.mayPrepare=false;} // 改过备法：下次要睡醒或研读后才能再改
  public override void DoWindowContents(Rect r){try{Draw(r);}finally{LPUI.Reset();}}
  void Draw(Rect r){
   var h=Adventurer.Get(pawn);var d=Spellbook.Data(h);if(d==null){Close();return;}
   float y=LPUI.Title(r,pawn.LabelShort+" 的法术书",false);
   int lvl=Spellbook.Level(h);
   Widgets.Label(new Rect(r.x,y,r.width,24f),"法师 "+lvl+" 级　最高 "+Spellbook.CircleName(Spellbook.MaxCircle(lvl))+"　法力 "+ClassSystem.Resource(h,Spellbook.Wizard).ToString("0.#")+" / "+ClassSystem.MaxResource(h,Spellbook.Wizard)+"　已备 "+Spellbook.PreparedCount(h)+" / "+Spellbook.PrepCap(h)+"（法师等级 + 智力调整值）");y+=26f;
   if(d.conc!=null){var cr=new Rect(r.x,y,r.width-130f,24f);GUI.color=Spellbook.Color;Widgets.Label(cr,"专注中："+d.conc.label+"（受到 10 点以上伤害时维持成功率 "+Spellbook.ConcChance(pawn).ToStringPercent()+"）");GUI.color=Color.white;
    if(LPUI.Button(new Rect(r.xMax-124f,y,120f,24f),"结束专注"))Spellbook.EndConc(pawn,d,false);y+=26f;}
   var tabs=LPUI.Row(new Rect(r.x,y,r.width,30f),3);
   string[] names={"法术书 / 备法","学习新法术（"+Spellbook.PicksDue(h)+"）","戏法（"+Spellbook.CantripsDue(h)+"）"};
   for(int i=0;i<3;i++){if(tab==i)Widgets.DrawHighlightSelected(tabs[i]);if(LPUI.Button(tabs[i],names[i])){tab=i;armed=null;}}
   y+=36f;
   var outer=new Rect(r.x,y,r.width,r.yMax-y-44f);
   List<AbilityDef> list;int ver=CasterSpells.Version,tick=Find.TickManager.TicksGame;
   if(rowsCache!=null && rowsTab==tab && rowsVer==ver && rowsTick==tick)list=rowsCache;
   else{
    if(tab==0)list=Spellbook.BookList(h);else if(tab==1)list=Spellbook.PickCandidates(h).OrderBy(Spellbook.Circle).ThenBy(a=>a.label).ToList();else list=Spellbook.CantripCandidates(h).ToList();
    rowsCache=list;rowsTab=tab;rowsVer=ver;rowsTick=tick;
   }
   float head=tab==0?48f:40f;
   var view=new Rect(0f,0f,outer.width-16f,head+list.Count*52f+8f);
   Widgets.BeginScrollView(outer,ref scroll,view);
   try{
    float yy=0f;Text.Font=GameFont.Tiny;
    if(tab==0){
     string t=d.mayPrepare?"现在可以更换备法（关闭窗口后要再睡一觉或研读 1 小时才能再改）。戏法常备；仪式法术不占名额，没准备也能在这里仪式施法（原地引导 1 小时，不耗法力）。":"备法已定：睡醒后、或在研究台 / 卷轴书桌研读 1 小时后才能更换。仪式法术随时可以在这里仪式施法。";
     Widgets.Label(new Rect(0f,yy,view.width-150f,44f),t);
     if(!d.mayPrepare && LPUI.Button(new Rect(view.width-146f,yy+4f,142f,28f),"去研读（1 小时）",Spellbook.StudyBench(pawn)!=null,"走到最近的研究台或卷轴书桌研读 1 小时，之后可以更换备法。")){Spellbook.StartStudy(pawn);Close();}
    }
    else if(tab==1)Widgets.Label(new Rect(0f,yy,view.width,38f),"每个法师等级可以写进法术书 2 个法术，只能选已经能施放的环位（一环 1 级、二环 3 级、三环 5 级……九环 17 级，每 2 级一环）。待选 "+Spellbook.PicksDue(h)+" 个。卷轴书桌的“抄录法术”不占这个名额。");
    else Widgets.Label(new Rect(0f,yy,view.width,38f),"戏法不耗法力、常备不占备法名额。法师 1 级 3 个（魔法飞弹算一个），4 级、10 级各 +1。还能学 "+Spellbook.CantripsDue(h)+" 个。");
    Text.Font=GameFont.Small;yy+=head;
    foreach(var a in list){DrawRow(new Rect(0f,yy,view.width,48f),h,d,a);yy+=52f;}
    if(list.Count==0){GUI.color=Color.gray;Widgets.Label(new Rect(0f,yy,view.width,24f),tab==0?"法术书是空的。":tab==1?"没有可以选的法术（已经全会了，或要更高的法师等级）。":"没有可以学的戏法了。");GUI.color=Color.white;}
   }finally{Widgets.EndScrollView();}
  }
  void DrawRow(Rect row,Hediff_Adventurer h,SpellbookData d,AbilityDef a){
   Widgets.DrawLightHighlight(row);
   GUI.DrawTexture(new Rect(row.x+4f,row.y+6f,36f,36f),a.uiIcon??BaseContent.BadTex);
   int c=Spellbook.Circle(a);string cost=Spellbook.ManaFor(c)>0?"法力 "+(a.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault()?.cost??Spellbook.ManaFor(c)):"不耗法力";
   float right=tab==0?300f:110f;
   LPUI.LabelFit(new Rect(row.x+46f,row.y+2f,row.width-46f-right,24f),"【"+Spellbook.CircleName(c)+"】"+a.LabelCap+"　"+Spellbook.Tags(a)+"　"+cost);
   Text.Font=GameFont.Tiny;GUI.color=new Color(.8f,.8f,.8f);Widgets.Label(new Rect(row.x+46f,row.y+26f,row.width-46f-right,20f),(a.description??"").Truncate(row.width-46f-right));GUI.color=Color.white;Text.Font=GameFont.Small;
   TooltipHandler.TipRegion(new Rect(row.x,row.y,row.width-right,row.height),a.LabelCap+"\n\n"+a.description);
   float x=row.xMax-right;
   if(tab==0){
    if(Spellbook.Cantrip(a)){GUI.color=Color.gray;Widgets.Label(new Rect(x,row.y+12f,90f,24f),"戏法·常备");GUI.color=Color.white;}
    else{
     bool on=d.prepared.Contains(a);string why=Spellbook.CannotToggle(h,a,!on);
     var cb=new Rect(x,row.y+12f,96f,24f);
     if(LPUI.Button(cb,on?"✔ 已准备":"未准备",why==null,why??(on?"取消准备":"准备这个法术")+(Spellbook.Counted(a)?"":"（仪式法术不占名额）"))){Spellbook.SetPrepared(h,a,!on);changed=true;}
    }
    x+=100f;
    if(Spellbook.Upcast(a)!=null){int up=Spellbook.UpLevel(pawn,a);bool ch=Spellbook.Charge(a);if(LPUI.Button(new Rect(x,row.y+12f,90f,24f),ch?"蓄力 "+up+" 秒":up>0?"升环 +"+up:"升环",Spellbook.MaxUp(pawn,a)>0,ch?"选择蓄力秒数（0～5，不加法力）。也可以右键法术按钮。":"选择施放的环位：每升 1 环多花 1 法力，"+Spellbook.UpText(pawn,a,1)+"。也可以右键法术按钮。"))Find.WindowStack.Add(new Dialog_Upcast(pawn,a));}
    x+=94f;
    if(Spellbook.Ritual(a)){string why=Spellbook.CannotRitual(pawn,a);if(LPUI.Button(new Rect(x,row.y+12f,100f,24f),"仪式施法",why==null,why??"原地引导 1 小时施放，不耗法力（要在非征召状态下）。")){Close();Spellbook.BeginRitual(pawn,a);}}
    return;
   }
   string key=a.defName;var b=new Rect(row.xMax-104f,row.y+10f,96f,28f);
   string w=tab==1?(Spellbook.PicksDue(h)<=0?"没有待选名额":Spellbook.CannotPick(h,a)):Spellbook.CannotCantrip(h,a);
   if(LPUI.Button(b,armed==key?"确定？":tab==1?"写入":"学会",w==null,w??"点两次确认") && w==null){
    if(armed==key){armed=null;if(tab==1)Spellbook.Pick(h,a);else Spellbook.LearnCantrip(h,a);SoundDefOf.Tick_High.PlayOneShotOnCamera();}else armed=key;
   }
  }
 }
 public class Dialog_Upcast : Window {
  readonly Pawn pawn;readonly AbilityDef def;
  public override Vector2 InitialSize=>LPUI.Fit(460f,140f+Mathf.Max(1,Spellbook.MaxUp(pawn,def)+1)*40f);
  public Dialog_Upcast(Pawn p,AbilityDef d){pawn=p;def=d;doCloseX=true;closeOnClickedOutside=true;absorbInputAroundWindow=true;}
  public override void DoWindowContents(Rect r){try{Draw(r);}finally{LPUI.Reset();}}
  void Draw(Rect r){
   int c=CasterSpells.CircleFor(pawn,def),max=Spellbook.MaxUp(pawn,def),cur=Spellbook.UpLevel(pawn,def);
   var props=def.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();int baseCost=props?.cost??0;
   if(Spellbook.Charge(def)){ // 1.129.0 延时爆裂火球：蓄力秒数
    float yc=LPUI.Title(r,"蓄力："+def.LabelCap,false);
    Text.Font=GameFont.Tiny;Widgets.Label(new Rect(r.x,yc,r.width,36f),"之后施放都按选中的蓄力秒数：火珠落地后过这么多秒才炸开，每秒伤害 +10 × 奥术强度。不加法力。");Text.Font=GameFont.Small;yc+=40f;
    for(int s=0;s<=max;s++){var b=new Rect(r.x,yc,r.width,34f);if(s==cur)Widgets.DrawHighlightSelected(b);if(LPUI.Button(b,"蓄力 "+s+" 秒：伤害 ("+(20+10*s)+") × 奥术强度")){Spellbook.SetUp(pawn,def,s);Close();}yc+=40f;}
    return;
   }
   float y=LPUI.Title(r,"升环："+def.LabelCap,false);
   Text.Font=GameFont.Tiny;Widgets.Label(new Rect(r.x,y,r.width,36f),"之后施放这个法术都按选中的环位（每升 1 环多花 1 法力）。最高到自己能放的 "+Spellbook.CircleName(Spellbook.MaxCircleFor(pawn,def))+"。");Text.Font=GameFont.Small;y+=40f;
   for(int up=0;up<=max;up++){
    var b=new Rect(r.x,y,r.width,34f);if(up==cur)Widgets.DrawHighlightSelected(b);
    if(LPUI.Button(b,"按"+Spellbook.CircleName(c+up)+"施放：法力 "+(baseCost+up)+(up>0?"，"+Spellbook.UpText(pawn,def,up):"（原样）"))){Spellbook.SetUp(pawn,def,up);Close();}
    y+=40f;
   }
  }
 }
 // 通用的选择窗口（增强属性、防护能量、魔法阵、亡者交谈、送讯术、寻获魔宠）。cancel 为 null 时必须选一个。
 public class Dialog_SpellChoice : Window {
  readonly string title,text;readonly List<(string label,string tip,Action act)> options;readonly Action cancel;Vector2 scroll;
  public override Vector2 InitialSize=>LPUI.Fit(520f,150f+options.Count*40f);
  public Dialog_SpellChoice(string title,string text,List<(string,string,Action)> options,Action cancel=null){
   this.title=title;this.text=text;this.options=options;this.cancel=cancel;doCloseX=cancel!=null;closeOnCancel=cancel!=null;closeOnAccept=false;closeOnClickedOutside=false;absorbInputAroundWindow=true;forcePause=true;
  }
  public override void OnCancelKeyPressed(){if(cancel==null)return;base.OnCancelKeyPressed();}
  public override void PostClose(){base.PostClose();if(!picked)cancel?.Invoke();}
  bool picked;
  public override void DoWindowContents(Rect r){try{Draw(r);}finally{LPUI.Reset();}}
  void Draw(Rect r){
   float y=LPUI.Title(r,title,false);
   if(!text.NullOrEmpty()){Text.Font=GameFont.Tiny;float th=Text.CalcHeight(text,r.width);Widgets.Label(new Rect(r.x,y,r.width,th),text);Text.Font=GameFont.Small;y+=th+6f;}
   var outer=new Rect(r.x,y,r.width,r.yMax-y);var view=new Rect(0f,0f,outer.width-16f,options.Count*40f);
   Widgets.BeginScrollView(outer,ref scroll,view);
   try{float yy=0f;foreach(var o in options){if(LPUI.Button(new Rect(0f,yy,view.width,34f),o.label,true,o.tip)){picked=true;Close();o.act?.Invoke();}yy+=40f;}}
   finally{Widgets.EndScrollView();}
  }
 }

 // ==================== Harmony ====================
 // 法师的“备法”按钮。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class SpellbookGizmo {
  static Texture2D icon;
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   foreach(var g in __result)yield return g;
   var p=__instance;if(!Spellbook.Player(p) || p.Dead || !p.IsColonistPlayerControlled)yield break;
   var h=Adventurer.Get(p);if(Spellbook.Level(h)<=0)yield break;
   int due=Spellbook.PicksDue(h)+Spellbook.CantripsDue(h);var d=Spellbook.Data(h);
   if(icon==null)icon=ContentFinder<Texture2D>.Get("LP/Classes/Manual",false)??BaseContent.BadTex;
   yield return new Command_Action{defaultLabel="备法（"+Spellbook.PreparedCount(h)+"/"+Spellbook.PrepCap(h)+"）"+(due>0?" 待选 "+due:""),icon=icon,
    defaultDesc="打开法术书：准备法术（只有准备了的法术出现在按钮栏）、学习新法术和戏法、仪式施法、设置升环。"+(d?.conc!=null?"\n\n专注中："+d.conc.label:"")+(d!=null && !d.mayPrepare?"\n\n睡醒后、或在研究台 / 卷轴书桌研读 1 小时后才能更换备法。":""),
    action=()=>Find.WindowStack.Add(new Dialog_Spellbook(p))};
  }
 }
 // 专注：受到 10 点以上伤害时维持专注。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PostApplyDamage))]
 public static class SpellbookConcentration {
  static void Postfix(Pawn __instance,float totalDamageDealt){
   if(Spellbook.Concentrating.Count>0 && totalDamageDealt>=Spellbook.ConcDamage)Spellbook.Damaged(__instance,totalDamageDealt);
   if(WizardSpells2.ContingencyPawns.Count>0 && WizardSpells2.ContingencyPawns.Contains(__instance))WizardSpells2.CheckContingency(__instance); // 1.129.0 应急术
  }
 }
 // 敌方施法者死亡：15% 掉一张法术书残页。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class SpellbookPageDrop {
  static void Prefix(Pawn __instance,out (Map map,IntVec3 cell,bool caster) __state){
   var p=__instance;bool caster=p!=null && !p.Dead && p.Spawned && p.RaceProps.Humanlike && p.Faction!=null && !p.Faction.IsPlayer && p.HostileTo(Faction.OfPlayer) && ClassSystem.Spellcaster(p);
   __state=(caster?p.Map:null,caster?p.Position:IntVec3.Invalid,caster);
  }
  static void Postfix(Pawn __instance,(Map map,IntVec3 cell,bool caster) __state){
   if(!__state.caster || __state.map==null || !__instance.Dead || !Rand.Chance(Spellbook.PageDrop))return;
   var page=Spellbook.MakePage(Spellbook.DropCircle(__instance));if(page!=null)GenPlace.TryPlaceThing(page,__state.cell,__state.map,ThingPlaceMode.Near);
  }
 }
 // 抄录法术只有法师能做。
 [HarmonyPatch(typeof(Bill),nameof(Bill.PawnAllowedToStartAnew))]
 public static class SpellbookCopyBill {
  static void Postfix(Bill __instance,Pawn p,ref bool __result){
   if(!__result || __instance?.recipe?.workerClass!=typeof(RecipeWorker_CopySpell))return;
   if(Spellbook.Level(p)<=0){__result=false;JobFailReason.Is("只有法师能抄录法术");}
  }
 }
 // 魔宠在 10 格内：法师法术射程 +3。
 [HarmonyPatch(typeof(Verb),nameof(Verb.EffectiveRange),MethodType.Getter)]
 public static class SpellbookFamiliarRange {
  static void Postfix(Verb __instance,ref float __result){
   if(Familiars.Masters.Count==0 || !(__instance is Verb_CastAbility v) || !(v.ability is Ability_Class a) || !Familiars.Masters.Contains(a.pawn))return;
   if(Spellbook.IsSpell(a.def) && Familiars.Near(a.pawn))__result+=Spellbook.FamiliarRange;
  }
 }
 // 飞行术：走路无视地形（在 ClassWoodstride 里查 Spellbook 的 Fliers）。
 // 1.131.0 性能热修：学会 / 失去任何能力都让按帧记忆的施放职业和窗口里的法术列表失效（Spellbook.Knows 看的是能力表）。
 [HarmonyPatch]
 public static class SpellbookAbilityVersion {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){foreach(var m in AccessTools.GetDeclaredMethods(typeof(Pawn_AbilityTracker)))if(m.Name=="GainAbility" || m.Name=="RemoveAbility")yield return m;}
  static void Postfix(){CasterSpells.Touch();}
 }
 // 弱智术的状态：计数用于 CanCast 钩子 / 法术 CannotCast 的快速返回（没人被弱智时不扫状态表）。
 public class Hediff_Feebleminded : HediffWithComps {
  public static int Live;
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);Live++;}
  public override void PostRemoved(){base.PostRemoved();if(Live>0)Live--;}
  public override void ExposeData(){base.ExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit)Live++;}
 }

 // 换存档 / 新游戏：清掉上一局的静态登记（构造时清，在地图读档之前；读档时 Hediff_WS、区域、门锁再自己登记回来）。
 public class GameComponent_Spellbook : GameComponent {
  public GameComponent_Spellbook(Game game){Spellbook.Concentrating.Clear();Spellbook.Fliers.Clear();Familiars.Masters.Clear();SpellZone.Circles.Clear();Hediff_WS.Live=0;Hediff_Feebleminded.Live=0;WizardLocks.Count=0;Hediff_WS.Counterspellers.Clear();Banishment.Reset();CasterSpells.Touch();}
  // 1.129.0 放逐 / 迷宫放逐：暂时离开地图的人（世界小人）和回来的时间、地点。
  public List<Pawn> banished=new List<Pawn>();public List<int> banishTick=new List<int>(),banishMap=new List<int>();public List<IntVec3> banishCell=new List<IntVec3>();
  public List<Pawn> clonePending=new List<Pawn>(); // 克隆体：死后下一刻在克隆体里醒来
  // 1.131.0 性能热修：弱智术计数按实际状态重数一遍（老存档里的弱智术还是旧的 HediffWithComps 类，不会自己登记；多数只会让计数偏高，不会漏掉效果）。
  public override void FinalizeInit(){base.FinalizeInit();var def=DefCache<HediffDef>.Get("LP_WS_Feebleminded");int n=0;if(def!=null)foreach(var p in PawnsFinder.AllMapsWorldAndTemporary_Alive){var hs=p?.health?.hediffSet?.hediffs;if(hs==null)continue;for(int i=0;i<hs.Count;i++)if(hs[i].def==def)n++;}Hediff_Feebleminded.Live=n;}
  public override void GameComponentTick(){
   if(Find.TickManager.TicksGame%60!=0)return;
   Banishment.Tick(this);
   if(clonePending.Count>0){var l=clonePending.ToList();clonePending.Clear();foreach(var p in l)WizardSpells2.Reawaken(p);}
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref banished,"lpWsBanished",LookMode.Reference);Scribe_Collections.Look(ref banishTick,"lpWsBanishTick",LookMode.Value);Scribe_Collections.Look(ref banishMap,"lpWsBanishMap",LookMode.Value);Scribe_Collections.Look(ref banishCell,"lpWsBanishCell",LookMode.Value);
   Scribe_Collections.Look(ref clonePending,"lpWsClonePending",LookMode.Reference);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(banished==null)banished=new List<Pawn>();if(banishTick==null)banishTick=new List<int>();if(banishMap==null)banishMap=new List<int>();if(banishCell==null)banishCell=new List<IntVec3>();if(clonePending==null)clonePending=new List<Pawn>();clonePending.RemoveAll(x=>x==null);Banishment.Clean(this);}
  }
 }
}
