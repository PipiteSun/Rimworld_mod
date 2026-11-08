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
 // 1.130.0 施法职业法术扩充（Peter 2026-11-08，经协调线程；报告 ArtSource/CasterSpells-1.130.0-report.md）。
 // 把法师的法术框架（环位、法力 / 等级表、备法、仪式、升环、专注、八环每天 2 次 / 九环 1 次）泛化成“职业法术表”：
 // 每个施法职业一份 CasterListDef（Defs/CasterSpells_Lists.xml），列出这个职业能用的法术——新法术（LP_CS_*，带 CasterSpellExt）、
 // 法师已有的法术（LP_WS_* 与 12 个基础法术）和各职业已有的职业能力（写 defName=环位）。同一个法术只有一份定义，可以出现在多张表里。
 // 学法：备法职业（牧师、德鲁伊、圣武士、器械师）自动知道表里能施放环位的全部法术，睡醒后可备 = 职业等级（半施法者 等级/2 向上取整）+ 施法属性调整值（至少 1），
 // 只有备好的给按钮；已知职业（术士、邪术师、吟游诗人、游侠）每级选 1 个（1 级 2 个），每升一级可以替换 1 个，已知的都给按钮（窗口里可以隐藏）。
 // 戏法：有戏法的职业 1 级 3 个、4 级 / 10 级各 +1。邪术师 6～9 环是秘法玄奥：11 / 13 / 15 / 17 级各选 1 个，每个每天 1 次、不花契约法力。
 // 施放：扣施放职业自己的资源（神恩 / 法力 / 契约法力 / 乐韵），费用按施放职业的环位查法力表；没有资源的器械师 / 游侠用“法术能量”（2 + 职业等级，睡醒回满）。
 // 强度：施放职业自己的强度属性（ClassSystem.StatValue 里优先问 CasterSpells.StatFor）。NPC 不建法术表，也不用这些法术。
 public class CasterSpellExt : DefModExtension {
  public int circle=-1;public string school;public bool ritual,concentration;public string upcast;public int perDay;
  public int grant; // >0：超魔等职业特性，这个职业等级自动获得，不占已知名额
  // 通用效果参数（CasterEffects.cs）
  public string damage,damage2,hediff2,mental,kind,vs,thing,zone;public float chance,ap=.2f,splash,vsMult=2f;public int count,stun,days,cells;public bool mind,pull,push,line,follow;
 }
 public class CasterListDef : Def {
  public LP_ClassDef classDef;public string ability="Wis",caster="full",iconPath;public bool prepared,cantrips=true;public int order;
  public List<StatDef> stats=new List<StatDef>();public List<string> spells=new List<string>();
  public List<int> secretLevels=new List<int>();public int secretsPer=2; // 吟游诗人秘法奥秘
  [Unsaved] Dictionary<AbilityDef,int> table;[Unsaved] List<AbilityDef> ordered;[Unsaved] Texture2D icon;
  public Texture2D Icon=>icon??(icon=iconPath.NullOrEmpty()?ContentFinder<Texture2D>.Get("LP/Classes/Manual",false)??BaseContent.BadTex:ContentFinder<Texture2D>.Get(iconPath,false)??BaseContent.BadTex);
  public Dictionary<AbilityDef,int> Table{get{
   if(table!=null)return table;var t=new Dictionary<AbilityDef,int>();
   foreach(var s in spells){
    if(s.NullOrEmpty())continue;var parts=s.Split('=');var d=DefDatabase<AbilityDef>.GetNamedSilentFail(parts[0].Trim());
    if(d==null){Log.Warning("[DND边缘大陆] 法术表 "+defName+" 找不到法术 "+parts[0]);continue;}
    int c=parts.Length>1 && int.TryParse(parts[1],out int o)?o:Spellbook.Circle(d);
    if(c<0){Log.Warning("[DND边缘大陆] 法术表 "+defName+" 的 "+d.defName+" 没有环位");continue;}
    t[d]=c;
   }
   table=t;ordered=t.Keys.OrderBy(k=>t[k]).ThenBy(k=>k.label).ToList();return table;}}
  public List<AbilityDef> Ordered{get{var _=Table;return ordered;}}
  public bool Pact=>caster=="pact";public bool Half=>caster=="half" || caster=="artificer";
  public bool UsesEnergy=>classDef!=null && classDef.resource==ClassResource.None;
  public string AbilityLabel=>Adventurer.Names[Mathf.Clamp(Adventurer.AbilityIndex(ability),0,5)];
 }

 public static class CasterSpells {
  public static readonly Color Color=new Color(.55f,.8f,1f);
  public static CasterSpellExt CExt(AbilityDef d)=>d?.modExtensions==null?null:d.GetModExtension<CasterSpellExt>();
  static List<CasterListDef> lists;static int listCount=-1;
  static Dictionary<string,CasterListDef> byName;static System.Collections.Concurrent.ConcurrentDictionary<AbilityDef,int> anyCircle;
  public static List<CasterListDef> Lists{get{if(lists==null || listCount!=DefDatabase<CasterListDef>.DefCount){var l=DefDatabase<CasterListDef>.AllDefsListForReading.OrderBy(x=>x.order).ToList();var m=new Dictionary<string,CasterListDef>();foreach(var x in l)m[x.defName]=x;byName=m;anyCircle=new System.Collections.Concurrent.ConcurrentDictionary<AbilityDef,int>();lists=l;listCount=DefDatabase<CasterListDef>.DefCount;propDefs=null;Touch();}return lists;}}
  // 1.131.0 性能热修：按名字查法术表原来是 LINQ 线性找（每次 CastList 对每本法术表都查一次）。
  public static CasterListDef ListNamed(string n){if(n==null)return null;var _=Lists;return byName.TryGetValue(n,out var l)?l:null;}
  public static CasterListDef ListOf(LP_ClassDef c){if(c==null)return null;var ls=Lists;for(int i=0;i<ls.Count;i++)if(ls[i].classDef==c)return ls[i];return null;}
  // 1.131.0 性能热修：法术书 / 法术表的内容变了就 +1（备法、学法、遗忘、仪式、同步、学会 / 失去能力、职业等级）。窗口里按（版本，游戏刻）记住的法术列表靠它失效。
  public static int Version;public static void Touch(){Version++;}
  public static CasterListDef ListOf(SpellbookData b)=>ListNamed(b?.cls);
  // 新法术（没有法师扩展）的职业：第一张列出它的法术表。
  public static LP_ClassDef ClassOf(AbilityDef d){if(CExt(d)==null)return null;foreach(var l in Lists)if(l.Table.ContainsKey(d))return l.classDef;return null;}
  public static IEnumerable<AbilityDef> AllNew=>DefDatabase<AbilityDef>.AllDefsListForReading.Where(d=>CExt(d)!=null);

  // ---------- 等级与环位 ----------
  public static int Level(Hediff_Adventurer h,CasterListDef l)=>l?.classDef==null?0:ClassSystem.Level(h,l.classDef);
  public static int Level(Pawn p,CasterListDef l)=>Level(Adventurer.Get(p),l);
  static readonly int[] HalfLv={0,2,5,9,13,17},ArtLv={0,1,5,9,13,17};
  public static int MaxCircle(CasterListDef l,int lvl){
   if(l==null || lvl<=0)return 0;
   if(l.Half){var t=l.caster=="artificer"?ArtLv:HalfLv;int c=0;for(int i=1;i<t.Length;i++)if(lvl>=t[i])c=i;return c;}
   if(l.Pact)return Mathf.Min(5,Spellbook.MaxCircle(lvl));
   return Spellbook.MaxCircle(lvl);
  }
  public static int LevelFor(CasterListDef l,int circle){
   if(circle<=0)return 1;
   if(l!=null && l.Half){var t=l.caster=="artificer"?ArtLv:HalfLv;return circle<t.Length?t[circle]:99;}
   if(l!=null && l.Pact && circle>5)return ArcanumLevel(circle);
   return Spellbook.LevelFor(circle);
  }
  public static int ArcanumLevel(int circle)=>circle==6?11:circle==7?13:circle==8?15:circle==9?17:99;
  public static int AbilityMod(Pawn p,CasterListDef l)=>p==null || l==null?0:Adventurer.Mod(Adventurer.Score(p,Adventurer.AbilityIndex(l.ability)));
  public static int CircleIn(CasterListDef l,AbilityDef d)=>l!=null && d!=null && l.Table.TryGetValue(d,out int c)?c:-1;
  // 秘法奥秘学来的别家法术：环位按它在任意一张表（或法师法术书）里的最低环位。
  // 1.131.0 性能热修：只取决于定义，按法术记住（原来每次扫全部法术表；Active / CircleFor / 窗口排序每帧都调）。
  public static int AnyCircle(AbilityDef d){
   if(d==null)return -1;var ls=Lists;if(anyCircle.TryGetValue(d,out int known))return known;
   int best=Spellbook.Ext(d)!=null || Spellbook.IsBorrowed(d)?Spellbook.Circle(d):-1;for(int i=0;i<ls.Count;i++){int c=CircleIn(ls[i],d);if(c>=0 && (best<0 || c<best))best=c;}
   anyCircle[d]=best;return best;
  }

  // ---------- 数据 ----------
  public static IEnumerable<SpellbookData> Books(Pawn p){var c=Adventurer.Get(p)?.classes;return c?.casterBooks??Enumerable.Empty<SpellbookData>();}
  public static SpellbookData Book(Hediff_Adventurer h,CasterListDef l,bool create){
   var c=h?.classes;if(c==null || l==null)return null;if(c.casterBooks==null)c.casterBooks=new List<SpellbookData>();
   foreach(var x in c.casterBooks)if(x.cls==l.defName)return x;
   if(!create || Level(h,l)<=0 || !Spellbook.Player(h.pawn))return null;
   var b=new SpellbookData{cls=l.defName,init=true,mayPrepare=true};c.casterBooks.Add(b);Touch();return b;
  }
  public static SpellbookData Book(Pawn p,CasterListDef l)=>Book(Adventurer.Get(p),l,false);
  public static SpellbookData FirstBook(Pawn p){var h=Adventurer.Get(p);foreach(var b in Books(p)){var l=ListOf(b);if(l!=null && Level(h,l)>0)return b;}return null;}
  public static IEnumerable<CasterListDef> ListsOf(Pawn p){var h=Adventurer.Get(p);return Lists.Where(l=>Level(h,l)>0);}
  static bool AnyList(Hediff_Adventurer h){var ls=Lists;for(int i=0;i<ls.Count;i++)if(Level(h,ls[i])>0)return true;return false;}

  // ---------- 这个人在这个职业里能用的法术 ----------
  public static bool Granted(CasterListDef l,AbilityDef d,int level){var e=CExt(d);return e!=null && e.grant>0 && level>=e.grant && CircleIn(l,d)>=0;}
  public static bool Active(Hediff_Adventurer h,CasterListDef l,SpellbookData b,AbilityDef d,bool ritualToo=true){
   if(b==null || l==null || d==null)return false;int lv=Level(h,l);if(lv<=0)return false;
   if(b.cantrips.Contains(d) || b.arcanum.Contains(d))return true;
   if(b.secrets.Contains(d))return AnyCircle(d)<=MaxCircle(l,lv);
   if(Granted(l,d,lv))return true;
   int c=CircleIn(l,d);if(c<=0 || c>MaxCircle(l,lv))return false;
   if(ritualToo && b.ritual==d)return true;
   return l.prepared?b.prepared.Contains(d):b.book.Contains(d);
  }
  // 施放时算哪个职业：法师法术书里有就算法师；本职业的职业能力算本职业；否则第一张让它“可用”的法术表。
  // 1.131.0 性能热修：按钮栏每帧对每个能力都要问好几次（Hidden、GizmoDisabled → CannotCast → 费用 / 升环 / 每天次数……各走一遍）。
  // 试过按人按帧记住结果：这里本身只要 ~90 ns，记忆的开销比重算还大，所以只去掉了 LINQ 分配（AnyGranted、ListOf 改循环 / 字典，AnyCircle 按定义缓存）。
  public static CasterListDef CastList(Pawn p,AbilityDef d){
   if(d==null || p==null)return null;var h=Adventurer.Get(p);var c=h?.classes;if(c?.casterBooks==null || c.casterBooks.Count==0)return null;
   if(c.book!=null && Spellbook.Level(h)>0 && (c.book.book.Contains(d) || c.book.cantrips.Contains(d) || Spellbook.ClassOf(d)==Spellbook.Wizard && Spellbook.Knows(p,d) && !AnyGranted(c,d)))return null;
   var e=ClassSystem.EntryOf(d);if(e!=null){var own=ClassSystem.ClassOf(d);if(own!=null && ClassSystem.Entitled(h,own,e))return null;}
   for(int i=0;i<c.casterBooks.Count;i++){var b=c.casterBooks[i];var l=ListOf(b);if(l!=null && Active(h,l,b,d))return l;}
   return null;
  }
  static bool AnyGranted(ClassData c,AbilityDef d){for(int i=0;i<c.casterBooks.Count;i++)if(c.casterBooks[i].granted.Contains(d))return true;return false;}
  public static LP_ClassDef CastClass(Pawn p,AbilityDef d)=>CastList(p,d)?.classDef;
  public static SpellbookData BookFor(Pawn p,AbilityDef d){var l=CastList(p,d);return l==null?null:Book(p,l);}
  public static int CircleFor(Pawn p,AbilityDef d){var l=CastList(p,d);if(l==null)return Spellbook.Circle(d);int c=CircleIn(l,d);return c>=0?c:AnyCircle(d);}
  public static int? MaxCircleFor(Pawn p,AbilityDef d){var l=CastList(p,d);if(l==null)return null;return MaxCircle(l,Level(p,l));}
  public static bool? HiddenOverride(Pawn p,AbilityDef d){
   var c=Adventurer.Get(p)?.classes;if(c?.casterBooks==null || c.casterBooks.Count==0)return null;
   var l=CastList(p,d);
   if(l==null){foreach(var b in c.casterBooks)if(b.granted.Contains(d))return true;return null;} // 法术表给的、现在不能用了（降级 / 取消准备还没同步）
   var bk=Book(p,l);return bk!=null && bk.hiddenSpells.Contains(d);
  }

  // ---------- 强度 / 费用 / 次数 ----------
  static Dictionary<CompProperties_ClassEffect,AbilityDef> propDefs;
  public static StatDef StatFor(Pawn p,CompProperties_ClassEffect props){
   if(props==null)return null;var c=Adventurer.Get(p)?.classes;if(c?.casterBooks==null || c.casterBooks.Count==0)return null;
   if(propDefs==null){var m=new Dictionary<CompProperties_ClassEffect,AbilityDef>();foreach(var l in Lists)foreach(var d in l.Table.Keys){var e=d.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();if(e!=null)m[e]=d;}propDefs=m;}
   if(!propDefs.TryGetValue(props,out var def))return null;var cl=CastList(p,def);if(cl==null || cl.stats.Count==0)return null;
   if(cl.stats.Count==1)return cl.stats[0];StatDef best=null;float bv=float.MinValue;foreach(var s in cl.stats){float v=p.GetStatValue(s);if(v>bv){bv=v;best=s;}}return best;
  }
  public static int Cost(Pawn p,AbilityDef def,CompProperties_ClassEffect props,int c){
   var h=Adventurer.Get(p);if(h?.classes?.casterBooks==null || h.classes.casterBooks.Count==0)return c;
   var l=CastList(p,def);if(l==null)return c;var b=Book(h,l,false);if(b!=null && b.arcanum.Contains(def))return 0;
   int circle=CircleIn(l,def);if(circle<0)circle=AnyCircle(def);if(circle<=0)return c;
   return Mathf.Max(0,c-(props?.cost??0)+Spellbook.ManaFor(circle));
  }
  public static int? DailyOverride(Pawn p,AbilityDef d){var b=BookFor(p,d);return b!=null && b.arcanum.Contains(d)?1:(int?)null;}
  public static string DailyKey(Pawn p,AbilityDef d){var b=BookFor(p,d);return b!=null && b.arcanum.Contains(d)?"arc"+d.defName:null;}
  public static float PowerFactor(Pawn p,CompProperties_ClassEffect e)=>CasterEffects.Live>0 && p!=null && Items.Has(p,"LP_CS_BloodlineAwakened")?1.5f:1f;
  public static float ChanceBonus(Pawn caster)=>CasterEffects.Live>0 && caster!=null && Items.Has(caster,"LP_CS_MetaPierce")?.25f:0f;
  public static float DurationFactor(Pawn p,CompProperties_ClassEffect e)=>CasterEffects.Live>0 && p!=null && Items.Has(p,"LP_CS_MetaExtend")?2f:1f;
  // 法术能量（器械师 / 游侠）：上限 2 + 职业等级，睡醒回满。
  public static int EnergyMax(Hediff_Adventurer h,CasterListDef l)=>Level(h,l)<=0?0:2+Level(h,l);
  public static float EnergyLeft(Hediff_Adventurer h,CasterListDef l){var b=Book(h,l,false);return b==null?0f:Mathf.Max(0f,EnergyMax(h,l)-b.energy);}
  static int EnergyCost(Pawn p,AbilityDef def,CompProperties_ClassEffect props,CasterListDef l)=>CircleIn(l,def)<=0 && AnyCircle(def)<=0?0:ClassLegendary.Cost(p,def,props);
  public static string CannotCast(Pawn p,AbilityDef def,CompProperties_ClassEffect props){
   if(CasterEffects.Live>0 && Items.Has(p,"LP_CS_Silenced") && (Spellbook.IsSpell(def) || CastList(p,def)!=null))return "被静默术笼罩，念不出咒语";
   var c=Adventurer.Get(p)?.classes;if(c?.casterBooks==null || c.casterBooks.Count==0)return null;
   var l=CastList(p,def);if(l==null || !l.UsesEnergy)return null;var b=Book(p,l);if(b==null || b.arcanum.Contains(def))return null;
   int cost=EnergyCost(p,def,props,l);var h=Adventurer.Get(p);if(cost>0 && EnergyLeft(h,l)+.001f<cost)return "法术能量不足（"+EnergyLeft(h,l).ToString("0.#")+" / "+cost+"，睡醒回满）";
   return null;
  }
  public static bool Pay(Pawn p,AbilityDef def,CompProperties_ClassEffect props){
   var c=Adventurer.Get(p)?.classes;if(c?.casterBooks==null || c.casterBooks.Count==0)return false;
   var l=CastList(p,def);if(l==null || !l.UsesEnergy)return false;var b=Book(p,l);if(b==null)return false;
   if(!b.arcanum.Contains(def) && !ClassSystem.FreeCast(p,props))b.energy+=EnergyCost(p,def,props,l);
   return true;
  }
  public static string CostTip(Pawn p,AbilityDef def,CompProperties_ClassEffect props){
   if(props==null || props.passive)return null;var c=Adventurer.Get(p)?.classes;if(c?.casterBooks==null || c.casterBooks.Count==0)return null;
   var l=CastList(p,def);if(l==null)return null;var b=Book(p,l);var h=Adventurer.Get(p);
   if(b!=null && b.arcanum.Contains(def))return "秘法玄奥：每天 1 次，不消耗资源（今天已用 "+Spellbook.UsedToday(p,def)+" / 1）";
   if(!l.UsesEnergy)return null;int cost=EnergyCost(p,def,props,l);
   return cost<=0?"不消耗资源（只看冷却）":"消耗：法术能量 "+cost+"（剩 "+EnergyLeft(h,l).ToString("0.#")+" / "+EnergyMax(h,l)+"，睡醒回满）";
  }
  // 1.131.0 无踪步：商队里有人身上有“无踪”时，针对商队的威胁事件一半被避开。
  public static bool TraceBlocks(RimWorld.Planet.Caravan c)=>CasterEffects.Live>0 && c!=null && c.PawnsListForReading.Any(x=>Items.Has(x,"LP_CS_PassTrace")) && Rand.Chance(.5f);
  // 1.131.0 商队按钮：无踪步（1 天遇袭几率减半）、风行术（1 天全队移动 ×2）。
  static readonly (string def,string label,string hediff)[] CaravanSpells={("LP_CS_PassWithoutTrace","无踪步：1 天内遇袭几率减半","LP_CS_PassTrace"),("LP_CS_WindWalk","风行术：1 天内全队移动 ×2","LP_CS_WindWalk")};
  public static IEnumerable<Gizmo> CaravanGizmos(RimWorld.Planet.Caravan c){
   foreach(var p in c.PawnsListForReading.ToList()){
    var hh=Adventurer.Get(p);if(hh?.classes?.casterBooks==null || hh.classes.casterBooks.Count==0)continue;
    foreach(var (n,label,hed) in CaravanSpells){
     var def=DefCache<AbilityDef>.Get(n);var ab=def==null?null:p.abilities?.GetAbility(def);if(ab==null || CastList(p,def)==null)continue;var props=def.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
     string why=ab.OnCooldown?"冷却中":ClassSystem.CannotCast(p,def,props);var pawn=p;var caravan=c;
     yield return new Command_Action{defaultLabel=label,defaultDesc=pawn.LabelShort+"："+def.description,icon=def.uiIcon,Disabled=why!=null,disabledReason=why,
      action=()=>{ClassSystem.Pay(pawn,def,props);Spellbook.UseDaily(pawn,def);ab.StartCooldown(def.cooldownTicksRange.max);foreach(var x in caravan.PawnsListForReading.ToList())Items.Give(x,hed,60000,1f);Messages.Message(def.label+"："+caravan.Label,MessageTypeDefOf.PositiveEvent,false);}};
    }
   }
  }
  public static void AfterCast(Ability_Class a){
   var p=a?.pawn;if(p==null || CasterEffects.Live<=0)return;var e=CExt(a.def);if(e?.kind=="meta")return;
   if(!Spellbook.IsSpell(a.def) && CastList(p,a.def)==null)return;
   if(Items.Has(p,"LP_CS_MetaQuicken")){Items.Remove(p,"LP_CS_MetaQuicken");a.ResetCooldown();Items.Mote(p,"超魔·即时：不进冷却",Color);}
   Items.Remove(p,"LP_CS_MetaExtend");Items.Remove(p,"LP_CS_MetaPierce");
  }

  // ---------- 备法 ----------
  public static int PrepCap(Hediff_Adventurer h,CasterListDef l){int lv=Level(h,l);if(lv<=0 || !l.prepared)return 0;int b=l.Half?(lv+1)/2:lv;return Mathf.Max(1,b+AbilityMod(h.pawn,l));}
  public static bool Counted(CasterListDef l,AbilityDef d)=>CircleIn(l,d)>0 && !Spellbook.Ritual(d);
  public static int PreparedCount(Hediff_Adventurer h,CasterListDef l){var b=Book(h,l,false);if(b==null)return 0;int max=MaxCircle(l,Level(h,l));int n=0;for(int i=0;i<b.prepared.Count;i++){var d=b.prepared[i];if(Counted(l,d) && CircleIn(l,d)<=max)n++;}return n;}
  // 表里这个人能备的：环位够、不是本来就有的职业能力。
  public static bool OwnAbility(Hediff_Adventurer h,AbilityDef d){var e=ClassSystem.EntryOf(d);var own=e==null?null:ClassSystem.ClassOf(d);return own!=null && ClassSystem.Entitled(h,own,e);}
  public static IEnumerable<AbilityDef> Preparable(Hediff_Adventurer h,CasterListDef l){int max=MaxCircle(l,Level(h,l));return l.Ordered.Where(d=>{int c=CircleIn(l,d);return c>0 && c<=max && !OwnAbility(h,d);});}
  public static string CannotToggle(Hediff_Adventurer h,CasterListDef l,AbilityDef d,bool on){
   var b=Book(h,l,false);if(b==null)return "没有这个职业";if(!l.prepared)return "这个职业不用备法";
   int c=CircleIn(l,d);if(c==0)return "戏法常备，不用准备";if(c<0)return "不在"+l.classDef.label+"法术表里";
   if(c>MaxCircle(l,Level(h,l)))return Spellbook.CircleName(c)+"要"+l.classDef.label+" "+LevelFor(l,c)+" 级";
   if(OwnAbility(h,d))return "职业能力，常备";
   if(!b.mayPrepare)return "睡醒后、或在研究台 / 卷轴书桌研读 1 小时后才能更换";
   if(on && Counted(l,d) && PreparedCount(h,l)>=PrepCap(h,l))return "已经准备满了（"+PreparedCount(h,l)+" / "+PrepCap(h,l)+"）";
   return null;
  }
  public static bool SetPrepared(Hediff_Adventurer h,CasterListDef l,AbilityDef d,bool on){
   if(CannotToggle(h,l,d,on)!=null)return false;var b=Book(h,l,false);
   if(on){if(!b.prepared.Contains(d))b.prepared.Add(d);}else b.prepared.Remove(d);
   Touch();Sync(h);return true;
  }

  // ---------- 已知法术 ----------
  public static int KnownCap(Hediff_Adventurer h,CasterListDef l)=>l.prepared || Level(h,l)<=0?0:Level(h,l)+1;
  public static int KnownCount(Hediff_Adventurer h,CasterListDef l){var b=Book(h,l,false);if(b==null)return 0;int n=0;for(int i=0;i<b.book.Count;i++)if(CircleIn(l,b.book[i])>0)n++;return n;}
  public static int PicksDue(Hediff_Adventurer h,CasterListDef l)=>Mathf.Max(0,KnownCap(h,l)-KnownCount(h,l));
  public static int SwapsLeft(Hediff_Adventurer h,CasterListDef l){var b=Book(h,l,false);return b==null || l.prepared?0:Mathf.Max(0,Level(h,l)-1-b.swaps);}
  public static IEnumerable<AbilityDef> PickCandidates(Hediff_Adventurer h,CasterListDef l){var b=Book(h,l,false);int max=MaxCircle(l,Level(h,l));var p=h?.pawn;return l.Ordered.Where(d=>{int c=CircleIn(l,d);return c>0 && c<=max && b!=null && !b.book.Contains(d) && !Spellbook.Knows(p,d) && !Granted(l,d,Level(h,l));});}
  public static string CannotPick(Hediff_Adventurer h,CasterListDef l,AbilityDef d){
   var b=Book(h,l,false);if(b==null)return "没有这个职业";if(l.prepared)return "这个职业不用选法术";
   int c=CircleIn(l,d);if(c<=0)return c==0?"戏法在戏法页学":"不在"+l.classDef.label+"法术表里";
   if(c>MaxCircle(l,Level(h,l)))return Spellbook.CircleName(c)+"要"+l.classDef.label+" "+LevelFor(l,c)+" 级";
   if(b.book.Contains(d) || Spellbook.Knows(h.pawn,d))return "已经会了";
   if(PicksDue(h,l)<=0)return "没有待选名额";
   return null;
  }
  public static bool Pick(Hediff_Adventurer h,CasterListDef l,AbilityDef d){if(CannotPick(h,l,d)!=null)return false;Book(h,l,false).book.Add(d);Touch();Sync(h);return true;}
  public static string CannotForget(Hediff_Adventurer h,CasterListDef l,AbilityDef d){var b=Book(h,l,false);if(b==null || !b.book.Contains(d))return "不是已知法术";if(SwapsLeft(h,l)<=0)return "每升一级只能替换 1 个法术（已用完）";return null;}
  public static bool Forget(Hediff_Adventurer h,CasterListDef l,AbilityDef d){if(CannotForget(h,l,d)!=null)return false;var b=Book(h,l,false);b.book.Remove(d);b.swaps++;b.hiddenSpells.Remove(d);Touch();Sync(h);return true;}

  // ---------- 戏法 ----------
  public static int CantripCap(Hediff_Adventurer h,CasterListDef l)=>l.cantrips?Spellbook.CantripCap(Level(h,l)):0;
  public static int CantripsDue(Hediff_Adventurer h,CasterListDef l){var b=Book(h,l,false);return b==null?0:Mathf.Max(0,CantripCap(h,l)-b.cantrips.Count);}
  public static IEnumerable<AbilityDef> CantripCandidates(Hediff_Adventurer h,CasterListDef l){var p=h?.pawn;return l.Ordered.Where(d=>CircleIn(l,d)==0 && !Spellbook.Knows(p,d));}
  public static string CannotCantrip(Hediff_Adventurer h,CasterListDef l,AbilityDef d){var b=Book(h,l,false);if(b==null)return "没有这个职业";if(CircleIn(l,d)!=0)return "不是这个职业的戏法";if(Spellbook.Knows(h.pawn,d))return "已经会了";if(CantripsDue(h,l)<=0)return "戏法名额已满";return null;}
  public static bool LearnCantrip(Hediff_Adventurer h,CasterListDef l,AbilityDef d){if(CannotCantrip(h,l,d)!=null)return false;Book(h,l,false).cantrips.Add(d);Touch();Sync(h);return true;}

  // ---------- 秘法玄奥（邪术师 6～9 环） ----------
  public static IEnumerable<AbilityDef> ArcanumOptions(CasterListDef l,int circle)=>l.Ordered.Where(d=>CircleIn(l,d)==circle);
  public static AbilityDef ArcanumAt(SpellbookData b,CasterListDef l,int circle){if(b==null)return null;for(int i=0;i<b.arcanum.Count;i++)if(CircleIn(l,b.arcanum[i])==circle)return b.arcanum[i];return null;}
  public static int ArcanumDue(Hediff_Adventurer h,CasterListDef l){if(!l.Pact)return 0;var b=Book(h,l,false);if(b==null)return 0;int n=0;for(int c=6;c<=9;c++)if(Level(h,l)>=ArcanumLevel(c) && ArcanumAt(b,l,c)==null)n++;return n;}
  public static string CannotArcanum(Hediff_Adventurer h,CasterListDef l,AbilityDef d){
   var b=Book(h,l,false);if(b==null || !l.Pact)return "没有秘法玄奥";int c=CircleIn(l,d);if(c<6)return "不是秘法玄奥";
   if(Level(h,l)<ArcanumLevel(c))return Spellbook.CircleName(c)+"秘法玄奥要"+l.classDef.label+" "+ArcanumLevel(c)+" 级";
   if(ArcanumAt(b,l,c)!=null)return Spellbook.CircleName(c)+"已经选了「"+ArcanumAt(b,l,c).label+"」";
   if(Spellbook.Knows(h.pawn,d))return "已经会了";return null;
  }
  public static bool PickArcanum(Hediff_Adventurer h,CasterListDef l,AbilityDef d){if(CannotArcanum(h,l,d)!=null)return false;Book(h,l,false).arcanum.Add(d);Touch();Sync(h);return true;}

  // ---------- 秘法奥秘（吟游诗人：10 / 14 / 18 级各从任意职业表选 2 个） ----------
  public static int SecretCap(Hediff_Adventurer h,CasterListDef l){if(l.secretLevels.Count==0)return 0;int lv=Level(h,l),n=0;for(int i=0;i<l.secretLevels.Count;i++)if(lv>=l.secretLevels[i])n++;return n*l.secretsPer;}
  public static int SecretsDue(Hediff_Adventurer h,CasterListDef l){var b=Book(h,l,false);return b==null?0:Mathf.Max(0,SecretCap(h,l)-b.secrets.Count);}
  public static IEnumerable<AbilityDef> SecretCandidates(Hediff_Adventurer h,CasterListDef l){
   int max=MaxCircle(l,Level(h,l));var p=h?.pawn;var all=new HashSet<AbilityDef>();
   foreach(var o in Lists)if(o!=l)foreach(var d in o.Table.Keys)if(CExt(d)?.grant<=0 || CExt(d)==null)all.Add(d);
   foreach(var d in Spellbook.New.Concat(Spellbook.BorrowedDefs))all.Add(d);
   foreach(var n in new[]{"LP_MagicMissile","LP_MageArmor","LP_ShieldSpell","LP_Sleep","LP_MistyStep","LP_MirrorImage","LP_Fireball","LP_LightningBolt","LP_Haste","LP_WallOfStone","LP_Disintegrate","LP_Teleport"}){var d=DefCache<AbilityDef>.Get(n);if(d!=null)all.Add(d);}
   return all.Where(d=>{int c=AnyCircle(d);return c>=0 && c<=max && CircleIn(l,d)<0 && !Spellbook.Knows(p,d);}).OrderBy(AnyCircle).ThenBy(d=>d.label);
  }
  public static string CannotSecret(Hediff_Adventurer h,CasterListDef l,AbilityDef d){
   var b=Book(h,l,false);if(b==null || l.secretLevels.Count==0)return "没有秘法奥秘";if(SecretsDue(h,l)<=0)return "没有待选的秘法奥秘";
   int c=AnyCircle(d);if(c<0)return "不是法术";if(c>MaxCircle(l,Level(h,l)))return Spellbook.CircleName(c)+"还放不了";if(Spellbook.Knows(h.pawn,d))return "已经会了";return null;
  }
  public static bool PickSecret(Hediff_Adventurer h,CasterListDef l,AbilityDef d){if(CannotSecret(h,l,d)!=null)return false;Book(h,l,false).secrets.Add(d);Touch();Sync(h);return true;}

  public static int Due(Hediff_Adventurer h,CasterListDef l)=>PicksDue(h,l)+CantripsDue(h,l)+ArcanumDue(h,l)+SecretsDue(h,l);

  // ---------- 仪式 ----------
  public static string CannotRitual(Pawn p,CasterListDef l,AbilityDef d){
   var h=Adventurer.Get(p);var b=Book(h,l,false);if(b==null)return "没有这个职业";if(!Spellbook.Ritual(d))return "不是仪式法术";
   int c=CircleIn(l,d);bool known=l.prepared?c>0 && c<=MaxCircle(l,Level(h,l)):Active(h,l,b,d,false);if(!known)return "还不会这个法术";
   if(p.Drafted)return "仪式施法要在非征召状态下进行";if(p.Downed || !p.Spawned)return "现在不能施法";
   var ab=p.abilities?.GetAbility(d);if(ab!=null && ab.OnCooldown && d.cooldownTicksRange.max>=60000)return "冷却中（还要 "+(ab.CooldownTicksRemaining/60000f).ToString("0.#")+" 天）";
   if(CasterEffects.Live>0 && Items.Has(p,"LP_CS_Silenced"))return "被静默术笼罩";
   return null;
  }
  public static void BeginRitual(Pawn p,CasterListDef l,AbilityDef d){
   var why=CannotRitual(p,l,d);if(why!=null){Messages.Message(why,MessageTypeDefOf.RejectInput,false);return;}
   if(!d.targetRequired){StartRitual(p,l,d,new LocalTargetInfo(p));return;}
   var tp=d.verbProperties?.targetParams??new TargetingParameters();Find.Targeter.BeginTargeting(tp,t=>StartRitual(p,l,d,t),p);
  }
  public static bool StartRitual(Pawn p,CasterListDef l,AbilityDef d,LocalTargetInfo target){
   if(CannotRitual(p,l,d)!=null || Spellbook.ritualJob.Def==null)return false;var props=d.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();if(props==null)return false;
   var why=ClassEffects.Invalid(p,props,target);if(why!=null){Messages.Message(why,MessageTypeDefOf.RejectInput,false);return false;}
   var b=Book(p,l);var w=Spellbook.Data(p);if(w!=null)w.ritual=null;b.ritual=d;Touch();
   return p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Spellbook.ritualJob.Def,target.IsValid?target:new LocalTargetInfo(p.Position)),JobTag.Misc);
  }

  // ---------- 同步 / 每 250 刻 ----------
  public static bool OtherSource(Hediff_Adventurer h,AbilityDef d){
   if(OwnAbility(h,d))return true;var c=h.classes;if(c.kept.Contains(d) || c.legendary.Contains(d) || c.arcana.Contains(d))return true;
   if(c.book!=null && (c.book.book.Contains(d) || c.book.cantrips.Contains(d)))return true;
   if(Spellbook.ClassOf(d)==Spellbook.Wizard && Spellbook.Level(h)>0 && !Spellbook.IsBorrowed(d) && Spellbook.Ext(d)==null)return true; // 法师的 12 个基础法术
   if(Bard.SecretClass(h.pawn,d)!=null)return true;
   return false;
  }
  public static int Sync(Hediff_Adventurer h){
   var p=h?.pawn;var c=h?.classes;if(p?.abilities==null || c==null || p.Dead)return 0;if(c.casterBooks==null)c.casterBooks=new List<SpellbookData>();
   if(!Spellbook.Player(p) && c.casterBooks.Count==0)return 0;int n=0;
   foreach(var l in Lists)Book(h,l,true);
   var want=new Dictionary<AbilityDef,SpellbookData>();
   foreach(var b in c.casterBooks){var l=ListOf(b);if(l==null)continue;
    foreach(var d in b.cantrips.Concat(b.arcanum).Concat(b.secrets).Concat(l.prepared?b.prepared:b.book).Concat(l.Table.Keys.Where(k=>Granted(l,k,Level(h,l)))))
     if(!want.ContainsKey(d) && Active(h,l,b,d,false))want[d]=b;}
   foreach(var kv in want)if(p.abilities.GetAbility(kv.Key)==null){p.abilities.GainAbility(kv.Key);if(!kv.Value.granted.Contains(kv.Key))kv.Value.granted.Add(kv.Key);n++;}
   foreach(var b in c.casterBooks)foreach(var d in b.granted.ToList()){
    if(want.ContainsKey(d))continue;b.granted.Remove(d);
    if(OtherSource(h,d) || c.casterBooks.Any(o=>o!=b && o.granted.Contains(d)))continue;
    if(p.abilities.GetAbility(d)!=null){p.abilities.RemoveAbility(d);n++;}
   }
   Touch();return n;
  }
  public static void Tick250(Hediff_Adventurer h){
   var c=h?.classes;var p=h?.pawn;if(c==null || p==null || p.Dead)return;if(c.casterBooks==null)c.casterBooks=new List<SpellbookData>();
   if(c.casterBooks.Count==0){if(!Spellbook.Player(p) || !AnyList(h))return;Sync(h);if(c.casterBooks.Count==0)return;}
   bool asleep=!p.Awake();
   foreach(var b in c.casterBooks){if(b.wasAsleep && !asleep){b.mayPrepare=true;b.energy=0f;}b.wasAsleep=asleep;}
   if(c.book==null || Spellbook.Level(h)<=0){var d=FirstBook(p);if(d?.conc!=null){if(!Spellbook.ConcActive(d))Spellbook.EndConc(p,d,false);else Spellbook.Concentrating.Add(p);}}
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed)return;
   foreach(var b in c.casterBooks){var l=ListOf(b);if(l==null)continue;int lv=Level(h,l);if(b.notifiedLevel>=lv)continue;b.notifiedLevel=lv;int due=Due(h,l);
    if(due>0)Messages.Message(p.LabelShort+"的"+l.classDef.label+"法术：有 "+due+" 个待选（点“"+l.classDef.label+"法术”按钮选择）。",p,MessageTypeDefOf.PositiveEvent,false);}
  }
  public static string Summary(Hediff_Adventurer h,CasterListDef l){
   int lv=Level(h,l);string s=l.classDef.label+" "+lv+" 级　最高 "+Spellbook.CircleName(MaxCircle(l,lv));
   if(l.UsesEnergy)s+="　法术能量 "+EnergyLeft(h,l).ToString("0.#")+" / "+EnergyMax(h,l);
   else s+="　"+ClassSystem.ResourceLabel(l.classDef.resource)+" "+ClassSystem.Resource(h,l.classDef).ToString("0.#")+" / "+ClassSystem.MaxResource(h,l.classDef);
   if(l.prepared)s+="　已备 "+PreparedCount(h,l)+" / "+PrepCap(h,l)+"（"+(l.Half?"等级/2":"等级")+" + "+l.AbilityLabel+"调整值）";else s+="　已知 "+KnownCount(h,l)+" / "+KnownCap(h,l);
   return s;
  }
 }

 // ==================== 窗口 ====================
 public class Dialog_CasterSpells : Window {
  readonly Pawn pawn;CasterListDef list;int tab;Vector2 scroll;string armed;bool changed;
  // 1.131.0 性能热修：法术行、职业页签、页签标题原来每帧重算（全表过滤 + 排序 + 拼字串）；现在按（法术数据版本，游戏刻）记住，窗口里的每个操作都会 Touch。
  List<AbilityDef> rowsCache;string rowsKey;CasterListDef rowsList;int rowsVer=-1,rowsTick=-1;
  List<CasterListDef> mineCache;int mineVer=-1,mineTick=-1;
  List<(string key,string label,int due)> tabsCache;CasterListDef tabsList;int tabsVer=-1,tabsTick=-1;
  static bool Stale(ref int ver,ref int tick){int v=CasterSpells.Version,t=Find.TickManager.TicksGame;if(ver==v && tick==t)return false;ver=v;tick=t;return true;}
  public override Vector2 InitialSize=>LPUI.Fit(800f,720f);
  public Dialog_CasterSpells(Pawn p,CasterListDef l){pawn=p;list=l;doCloseX=true;doCloseButton=true;closeOnClickedOutside=false;absorbInputAroundWindow=true;forcePause=true;PickTab();}
  void PickTab(){var h=Adventurer.Get(pawn);var names=Tabs(h);tab=0;for(int i=0;i<names.Count;i++)if(names[i].due>0){tab=i;break;}}
  public override void PostClose(){base.PostClose();if(changed){var b=CasterSpells.Book(pawn,list);if(b!=null && list.prepared)b.mayPrepare=false;}}
  public override void DoWindowContents(Rect r){try{Draw(r);}finally{LPUI.Reset();}}
  List<(string key,string label,int due)> Tabs(Hediff_Adventurer h){
   if(tabsCache!=null && tabsList==list && !Stale(ref tabsVer,ref tabsTick))return tabsCache;
   tabsList=list;return tabsCache=TabsCompute(h);
  }
  List<(string key,string label,int due)> TabsCompute(Hediff_Adventurer h){
   var l=new List<(string,string,int)>();
   if(list.prepared)l.Add(("prep","备法",0));else{l.Add(("known","已知法术",0));l.Add(("learn","学习新法术（"+CasterSpells.PicksDue(h,list)+"）",CasterSpells.PicksDue(h,list)));}
   if(list.cantrips)l.Add(("cantrip","戏法（"+CasterSpells.CantripsDue(h,list)+"）",CasterSpells.CantripsDue(h,list)));
   if(list.Pact)l.Add(("arcanum","秘法玄奥（"+CasterSpells.ArcanumDue(h,list)+"）",CasterSpells.ArcanumDue(h,list)));
   if(list.secretLevels.Count>0)l.Add(("secret","秘法奥秘（"+CasterSpells.SecretsDue(h,list)+"）",CasterSpells.SecretsDue(h,list)));
   return l;
  }
  void Draw(Rect r){
   var h=Adventurer.Get(pawn);var b=CasterSpells.Book(h,list,false);if(b==null){Close();return;}
   float y=LPUI.Title(r,pawn.LabelShort+" 的法术",false);
   if(mineCache==null || Stale(ref mineVer,ref mineTick))mineCache=CasterSpells.ListsOf(pawn).ToList();var mine=mineCache;bool wiz=Spellbook.Level(h)>0;
   if(mine.Count>1 || wiz){
    var cls=LPUI.Row(new Rect(r.x,y,r.width,28f),mine.Count+(wiz?1:0));
    for(int i=0;i<mine.Count;i++){if(mine[i]==list)Widgets.DrawHighlightSelected(cls[i]);if(LPUI.Button(cls[i],mine[i].classDef.LabelCap) && mine[i]!=list){list=mine[i];PickTab();armed=null;scroll=Vector2.zero;}}
    if(wiz && LPUI.Button(cls[mine.Count],"法师法术书")){Close();Find.WindowStack.Add(new Dialog_Spellbook(pawn));}
    y+=32f;b=CasterSpells.Book(h,list,false);if(b==null)return;
   }
   LPUI.LabelFit(new Rect(r.x,y,r.width,24f),CasterSpells.Summary(h,list));y+=26f;
   var cb=Spellbook.ConcBook(pawn);if(cb?.conc!=null){var cr=new Rect(r.x,y,r.width-130f,24f);GUI.color=CasterSpells.Color;Widgets.Label(cr,"专注中："+cb.conc.label+"（受到 10 点以上伤害时维持成功率 "+Spellbook.ConcChance(pawn).ToStringPercent()+"）");GUI.color=Color.white;
    if(LPUI.Button(new Rect(r.xMax-124f,y,120f,24f),"结束专注"))Spellbook.EndConc(pawn,cb,false);y+=26f;}
   var names=Tabs(h);if(tab>=names.Count)tab=0;var tr=LPUI.Row(new Rect(r.x,y,r.width,30f),names.Count);
   for(int i=0;i<names.Count;i++){if(tab==i)Widgets.DrawHighlightSelected(tr[i]);if(LPUI.Button(tr[i],names[i].label)){tab=i;armed=null;}}
   y+=36f;string key=names[tab].key;
   List<AbilityDef> rows;string intro;
   bool fresh=Stale(ref rowsVer,ref rowsTick);bool reuse=!fresh && rowsCache!=null && rowsKey==key && rowsList==list;
   switch(key){
    case "prep":rows=reuse?rowsCache:CasterSpells.Preparable(h,list).ToList();intro=(b.mayPrepare?"现在可以更换备法（关闭窗口后要再睡一觉或研读 1 小时才能再改）。":"备法已定：睡醒后、或在研究台 / 卷轴书桌研读 1 小时后才能更换。")+"自动知道法术表里能施放环位的全部法术，只有准备了的出现在按钮栏；仪式法术不占名额，没准备也能在这里仪式施法（原地引导 1 小时，不耗资源）。";break;
    case "known":rows=reuse?rowsCache:b.book.Concat(b.cantrips).Concat(b.arcanum).Concat(b.secrets).Concat(list.Ordered.Where(d=>CasterSpells.Granted(list,d,CasterSpells.Level(h,list)))).Distinct().OrderBy(d=>CasterSpells.CircleFor(pawn,d)).ThenBy(d=>d.label).ToList();intro="已知的法术都在按钮栏（可以在这里隐藏）。每升一级可以替换 1 个已知法术（还能替换 "+CasterSpells.SwapsLeft(h,list)+" 个）。";break;
    case "learn":rows=reuse?rowsCache:CasterSpells.PickCandidates(h,list).ToList();intro="每个"+list.classDef.label+"等级学 1 个法术（1 级 2 个），只能选已经能施放的环位："+CircleTable()+"。待选 "+CasterSpells.PicksDue(h,list)+" 个。";break;
    case "cantrip":rows=reuse?rowsCache:CasterSpells.CantripCandidates(h,list).ToList();intro="戏法不耗资源、常备不占名额：1 级 3 个，4 级、10 级各 +1。还能学 "+CasterSpells.CantripsDue(h,list)+" 个。";break;
    case "arcanum":rows=reuse?rowsCache:list.Ordered.Where(d=>CasterSpells.CircleIn(list,d)>=6).ToList();intro="秘法玄奥：邪术师 11 / 13 / 15 / 17 级各从六 / 七 / 八 / 九环里选 1 个，每个每天施放 1 次，不花契约法力。";break;
    default:rows=reuse?rowsCache:CasterSpells.SecretCandidates(h,list).ToList();intro="秘法奥秘：吟游诗人 "+string.Join(" / ",list.secretLevels)+" 级各从任意职业的法术表选 "+list.secretsPer+" 个（按自己能施放的环位），算吟游诗人法术、扣乐韵、用激昂强度。还能选 "+CasterSpells.SecretsDue(h,list)+" 个。";break;
   }
   rowsCache=rows;rowsKey=key;rowsList=list;
   var outer=new Rect(r.x,y,r.width,r.yMax-y-44f);var view=new Rect(0f,0f,outer.width-16f,48f+rows.Count*52f+8f);
   Widgets.BeginScrollView(outer,ref scroll,view);
   try{
    Text.Font=GameFont.Tiny;Widgets.Label(new Rect(0f,0f,view.width-(key=="prep" && !b.mayPrepare?150f:0f),44f),intro);
    if(key=="prep" && !b.mayPrepare && LPUI.Button(new Rect(view.width-146f,4f,142f,28f),"去研读（1 小时）",Spellbook.StudyBench(pawn)!=null,"走到最近的研究台或卷轴书桌研读 1 小时，之后可以更换备法。")){Spellbook.StartStudy(pawn);Close();}
    Text.Font=GameFont.Small;float yy=48f;
    foreach(var a in rows){Row(new Rect(0f,yy,view.width,48f),h,b,a,key);yy+=52f;}
    if(rows.Count==0){GUI.color=Color.gray;Widgets.Label(new Rect(0f,yy,view.width,24f),"这里没有法术。");GUI.color=Color.white;}
   }finally{Widgets.EndScrollView();}
  }
  string CircleTable(){var parts=new List<string>();for(int c=1;c<=(list.Half?5:list.Pact?5:9);c++)parts.Add(Spellbook.CircleName(c)+" "+CasterSpells.LevelFor(list,c)+" 级");return string.Join("、",parts);}
  void Row(Rect row,Hediff_Adventurer h,SpellbookData b,AbilityDef a,string key){
   Widgets.DrawLightHighlight(row);GUI.DrawTexture(new Rect(row.x+4f,row.y+6f,36f,36f),a.uiIcon??BaseContent.BadTex);
   int c=key=="secret"?CasterSpells.AnyCircle(a):CasterSpells.CircleIn(list,a);if(c<0)c=CasterSpells.AnyCircle(a);
   bool arc=b.arcanum.Contains(a) || key=="arcanum";
   string res=list.UsesEnergy?"法术能量":ClassSystem.ResourceLabel(list.classDef.resource);string cost=c<=0?"不耗资源":arc?"每天 1 次、不耗资源":res+" "+Spellbook.ManaFor(c);
   float right=key=="prep" || key=="known"?300f:110f;
   string tags=Spellbook.Tags(a);if(CasterSpells.CExt(a)?.grant>0)tags=(tags.NullOrEmpty()?"":tags+"·")+"职业特性";
   LPUI.LabelFit(new Rect(row.x+46f,row.y+2f,row.width-46f-right,24f),"【"+Spellbook.CircleName(c)+"】"+a.LabelCap+(tags.NullOrEmpty()?"":"　"+tags)+"　"+cost);
   Text.Font=GameFont.Tiny;GUI.color=new Color(.8f,.8f,.8f);Widgets.Label(new Rect(row.x+46f,row.y+26f,row.width-46f-right,20f),(a.description??"").Truncate(row.width-46f-right));GUI.color=Color.white;Text.Font=GameFont.Small;
   TooltipHandler.TipRegion(new Rect(row.x,row.y,row.width-right,row.height),a.LabelCap+"\n\n"+a.description);
   float x=row.xMax-right;
   if(key=="prep" || key=="known"){
    if(key=="prep"){
     bool on=b.prepared.Contains(a);string why=CasterSpells.CannotToggle(h,list,a,!on);
     if(CasterSpells.OwnAbility(h,a)){GUI.color=Color.gray;Widgets.Label(new Rect(x,row.y+12f,96f,24f),"职业能力");GUI.color=Color.white;}
     else if(LPUI.Button(new Rect(x,row.y+12f,96f,24f),on?"✔ 已准备":"未准备",why==null,why??(on?"取消准备":"准备这个法术")+(CasterSpells.Counted(list,a)?"":"（仪式法术不占名额）"))){CasterSpells.SetPrepared(h,list,a,!on);changed=true;}
    }else{
     bool hid=b.hiddenSpells.Contains(a);
     if(LPUI.Button(new Rect(x,row.y+4f,96f,20f),hid?"已隐藏":"显示中",true,hid?"点一下让按钮回到按钮栏":"点一下把这个法术的按钮从按钮栏藏起来（不能施放、也不会自动施放）")){if(hid)b.hiddenSpells.Remove(a);else b.hiddenSpells.Add(a);CasterSpells.Touch();}
     string fw=CasterSpells.CannotForget(h,list,a);
     if(b.book.Contains(a) && LPUI.Button(new Rect(x,row.y+26f,96f,20f),armed=="f"+a.defName?"确定？":"替换",fw==null,fw??"遗忘这个法术，空出 1 个名额重新选（每级 1 次）。点两次确认") && fw==null){if(armed=="f"+a.defName){armed=null;CasterSpells.Forget(h,list,a);}else armed="f"+a.defName;}
    }
    x+=100f;
    if(Spellbook.Upcast(a)!=null){int up=Spellbook.UpLevel(pawn,a);if(LPUI.Button(new Rect(x,row.y+12f,90f,24f),up>0?"升环 +"+up:"升环",Spellbook.MaxUp(pawn,a)>0,"选择施放的环位：每升 1 环多花 1 点资源，"+Spellbook.UpText(pawn,a,1)+"。也可以右键法术按钮。"))Find.WindowStack.Add(new Dialog_Upcast(pawn,a));}
    x+=94f;
    if(Spellbook.Ritual(a)){string why=CasterSpells.CannotRitual(pawn,list,a);if(LPUI.Button(new Rect(x,row.y+12f,100f,24f),"仪式施法",why==null,why??"原地引导 1 小时施放，不耗资源（要在非征召状态下）。")){Close();CasterSpells.BeginRitual(pawn,list,a);}}
    return;
   }
   string k2=key+a.defName;var btn=new Rect(row.xMax-104f,row.y+10f,96f,28f);
   string w=key=="learn"?CasterSpells.CannotPick(h,list,a):key=="cantrip"?CasterSpells.CannotCantrip(h,list,a):key=="arcanum"?(b.arcanum.Contains(a)?"已选":CasterSpells.CannotArcanum(h,list,a)):CasterSpells.CannotSecret(h,list,a);
   if(LPUI.Button(btn,armed==k2?"确定？":b.arcanum.Contains(a)?"已选":"学会",w==null,w??"点两次确认") && w==null){
    if(armed==k2){armed=null;bool ok=key=="learn"?CasterSpells.Pick(h,list,a):key=="cantrip"?CasterSpells.LearnCantrip(h,list,a):key=="arcanum"?CasterSpells.PickArcanum(h,list,a):CasterSpells.PickSecret(h,list,a);if(ok)SoundDefOf.Tick_High.PlayOneShotOnCamera();}else armed=k2;
   }
  }
 }

 [HarmonyPatch(typeof(RimWorld.Planet.Caravan),nameof(RimWorld.Planet.Caravan.GetGizmos))]
 public static class CasterSpellsCaravanGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,RimWorld.Planet.Caravan __instance){foreach(var g in __result)yield return g;if(__instance.Faction==Faction.OfPlayer)foreach(var g in CasterSpells.CaravanGizmos(__instance))yield return g;}
 }
 // 施法职业的“法术”按钮（每个职业一个）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class CasterSpellsGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   foreach(var g in __result)yield return g;
   var p=__instance;if(!Spellbook.Player(p) || p.Dead || !p.IsColonistPlayerControlled)yield break;
   var h=Adventurer.Get(p);if(h?.classes?.casterBooks==null || h.classes.casterBooks.Count==0)yield break;
   foreach(var l in CasterSpells.Lists){
    if(CasterSpells.Level(h,l)<=0 || CasterSpells.Book(h,l,false)==null)continue;int due=CasterSpells.Due(h,l);var b=CasterSpells.Book(h,l,false);var ll=l;
    string count=l.prepared?CasterSpells.PreparedCount(h,l)+"/"+CasterSpells.PrepCap(h,l):CasterSpells.KnownCount(h,l)+"/"+CasterSpells.KnownCap(h,l);
    yield return new Command_Action{defaultLabel=l.classDef.label+"法术（"+count+"）"+(due>0?" 待选 "+due:""),icon=l.Icon,
     defaultDesc="打开"+l.classDef.label+"的法术表："+(l.prepared?"准备法术（只有准备了的出现在按钮栏）":"学习、替换、隐藏已知法术")+(l.cantrips?"、学习戏法":"")+(l.Pact?"、选择秘法玄奥":"")+"、仪式施法、设置升环。"+(l.prepared && !b.mayPrepare?"\n\n睡醒后、或在研究台 / 卷轴书桌研读 1 小时后才能更换备法。":""),
     action=()=>Find.WindowStack.Add(new Dialog_CasterSpells(p,ll))};
   }
  }
 }
}
