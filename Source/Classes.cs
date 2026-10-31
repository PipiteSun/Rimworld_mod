using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.72.0 职业体系脱离 VPE（第一版，见 ArtSource/Classes-1.72.0-spec.md / report.md）：战士、武僧、圣武士放在主程序集。
 // 职业等级存在冒险者状态（Hediff_Adventurer.classes）里；每个冒险者等级 1 点职业点（传奇等级 21～40 不给），职业等级总和 ≤ min(冒险者等级, 20)。
 // 第一个职业：主或副技能 ≥ 6（或主属性 ≥ 13）；兼修第 2、3 个：冒险者 ≥ 3 级且那门技能 ≥ 8（或主属性 ≥ 13）；最多 3 个。冒险者手册给一次跳过门槛。
 // 能力在职业等级 1/3/5/7/10/13/15 自动获得（15 级终极另要那门技能 15 级；1.87.0 前是 1/3/5/9/13/17/20 与技能 18）；施放消耗职业资源（斗志 / 气 / 神恩），资源上限 = 2 + 职业等级/2（四舍五入）。
 // 1.73.0：法师、牧师、德鲁伊、术士、器械师也移过来（Classes2.xml / ClassCasters.cs）。法力（Mana）每个职业一条；神恩牧师和圣武士共用一条（PoolKey）；器械师没有资源。
 public enum ClassResource{None,Fury,Ki,Grace,Mana,Rage,Pact} // Rage：野蛮人的怒气（Classes4.xml / ClassMartial2.cs）；Pact：邪术师的契约法力（ClassWarlock.xml / ClassWarlock.cs）
 public class ClassAbilityEntry {public AbilityDef ability;public int level=1;public SkillDef capstoneSkill;}
 public class LP_ClassDef : Def {
  public SkillDef mainSkill,alternateSkill;public int order;public ClassResource resource;public bool martial,caster,bloodline;
  public string primary; // 主属性："Str/Dex"（取较高）、"Int" 等
  public string legacyPath,background;public Color color=Color.white;
  public List<ClassAbilityEntry> abilities=new List<ClassAbilityEntry>();
  public List<AbilityDef> legendary=new List<AbilityDef>(); // 1.74.0 传奇职业能力（主动 + 被动）
  public float partPerLevel=-1f; // 1.81.0 热修2：每个职业等级的部位生命成长；不填按 ClassSystem.DefaultPartPer
  public float PartPerLevel=>partPerLevel>=0f?partPerLevel:ClassSystem.DefaultPartPer(defName);
  public float damagePerLevel=-1f,penPerLevel=-1f; // 1.81.0 热修3：每个职业等级的武器伤害 / 近战穿甲成长；不填按 ClassSystem.DefaultDamagePer / DefaultPenPer
  public float DamagePerLevel=>damagePerLevel>=0f?damagePerLevel:ClassSystem.DefaultDamagePer(defName);
  public float PenPerLevel=>penPerLevel>=0f?penPerLevel:ClassSystem.DefaultPenPer(defName);
  public int subclassLevel=3; // 选子职业的职业等级（邪术师的宗主 1 级就选）
  [Unsaved] Texture2D bg;[Unsaved] bool bgLoaded;
  public Texture2D Background{get{if(!bgLoaded){bgLoaded=true;bg=background.NullOrEmpty()?null:ContentFinder<Texture2D>.Get(background,false);}return bg;}}
  public int SkillLevel(Pawn p)=>Mathf.Max(ClassSystem.Skill(p,mainSkill),alternateSkill==null?0:ClassSystem.Skill(p,alternateSkill));
  public string SkillText=>mainSkill.LabelCap.Resolve()+(alternateSkill==null?"":"或"+alternateSkill.LabelCap.Resolve());
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(mainSkill==null)yield return defName+": mainSkill is null";
   foreach(var a in abilities)if(a.ability==null)yield return defName+": ability entry without ability";
  }
 }
 // 每个人的职业数据（冒险者状态里）。
 public class ClassData : IExposable {
  public Dictionary<LP_ClassDef,int> levels=new Dictionary<LP_ClassDef,int>();
  public Dictionary<LP_ClassDef,float> res=new Dictionary<LP_ClassDef,float>();
  public List<AbilityDef> kept=new List<AbilityDef>(); // 旧存档迁移：已学的能力超过换算后的职业等级也保留
  public int freeUnlocks; // 冒险者手册：下一个职业跳过技能 / 等级门槛
  public bool migrated; // VPE 旧存档迁移做过
  public int syncAcc;
  // 1.74.0 传奇职业能力：未用的传奇职业点、已经按等级发过的点数（补发用）、学会的传奇能力、各能力上次触发的刻（不屈战魂）。
  public int legendaryPoints,legendaryGranted;public List<AbilityDef> legendary=new List<AbilityDef>();public Dictionary<string,int> legendaryTicks=new Dictionary<string,int>();
  public int rageTick;public List<int> favored=new List<int>(); // 野蛮人最后一次攻防的刻（怒气衰减）、游侠的宿敌类型（ClassMartial2.FoeTypes）
  public SubclassData sub=new SubclassData(); // 子职业（ClassSubclass.cs）
  public bool npc; // NPC 职业已经分过（NpcClasses.cs）
  public List<AbilityDef> autocast=new List<AbilityDef>(); // 开了自动施放的职业能力（ClassAutoCast.cs）
  public int table=ClassSystem.TableVersion; // 1.87.0 等级表版本：旧存档读进来是 0，Tick250 里补一次新表下够格的能力
  public bool Any=>levels.Count>0;
  public void ExposeData(){
   Scribe_Collections.Look(ref levels,"levels",LookMode.Def,LookMode.Value);
   Scribe_Collections.Look(ref res,"res",LookMode.Def,LookMode.Value);
   Scribe_Collections.Look(ref kept,"kept",LookMode.Def);
   Scribe_Values.Look(ref freeUnlocks,"freeUnlocks");Scribe_Values.Look(ref migrated,"migrated");
   Scribe_Values.Look(ref legendaryPoints,"legendaryPoints");Scribe_Values.Look(ref legendaryGranted,"legendaryGranted");
   Scribe_Collections.Look(ref legendary,"legendary",LookMode.Def);Scribe_Collections.Look(ref legendaryTicks,"legendaryTicks",LookMode.Value,LookMode.Value);
   Scribe_Values.Look(ref rageTick,"rageTick");Scribe_Collections.Look(ref favored,"favored",LookMode.Value);
   Scribe_Deep.Look(ref sub,"sub");Scribe_Values.Look(ref npc,"npc");Scribe_Collections.Look(ref autocast,"autocast",LookMode.Def);Scribe_Values.Look(ref table,"table");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    if(autocast==null)autocast=new List<AbilityDef>();autocast.RemoveAll(x=>x==null);
    if(favored==null)favored=new List<int>();if(sub==null)sub=new SubclassData();
    if(levels==null)levels=new Dictionary<LP_ClassDef,int>();if(res==null)res=new Dictionary<LP_ClassDef,float>();if(kept==null)kept=new List<AbilityDef>();
    if(legendary==null)legendary=new List<AbilityDef>();legendary.RemoveAll(x=>x==null);if(legendaryTicks==null)legendaryTicks=new Dictionary<string,int>();
    foreach(var k in levels.Keys.Where(k=>k==null).ToList())levels.Remove(k);foreach(var k in res.Keys.Where(k=>k==null).ToList())res.Remove(k);kept.RemoveAll(x=>x==null);
   }
  }
 }

 public static class ClassSystem {
  public const int MaxClasses=3,CapLevel=20,FirstSkill=6,MultiSkill=8,MultiAdventurerLevel=3,CapstoneSkill=15,ScoreGate=13;
  public const float SleepRegen=.5f,MeditateRegen=.2f,DevoteeRegen=1.25f,EntropyPerCost=5f,PsychicMin=.2f;
  public static readonly int[] AbilityLevels={1,3,5,7,10,13,15}; // 1.87.0（Peter：最终特性在 15 级左右）；邪术师的秘法奥秘例外在 11 / 13
  static readonly DefRef<LP_ClassDef> fighter=new DefRef<LP_ClassDef>("LP_Class_Fighter"),monk=new DefRef<LP_ClassDef>("LP_Class_Monk"),paladin=new DefRef<LP_ClassDef>("LP_Class_Paladin");
  public static LP_ClassDef Fighter=>fighter.Def;public static LP_ClassDef Monk=>monk.Def;public static LP_ClassDef Paladin=>paladin.Def;
  static List<LP_ClassDef> all;static int allCount=-1;
  public static List<LP_ClassDef> All{get{if(all==null || allCount!=DefDatabase<LP_ClassDef>.DefCount){all=DefDatabase<LP_ClassDef>.AllDefsListForReading.OrderBy(d=>d.order).ToList();allCount=DefDatabase<LP_ClassDef>.DefCount;byAbility=null;}return all;}}
  static Dictionary<AbilityDef,(LP_ClassDef cls,ClassAbilityEntry entry)> byAbility;
  static Dictionary<AbilityDef,(LP_ClassDef cls,ClassAbilityEntry entry)> ByAbility{get{var a=All;if(byAbility==null){byAbility=new Dictionary<AbilityDef,(LP_ClassDef,ClassAbilityEntry)>();foreach(var c in a)foreach(var e in c.abilities)if(e.ability!=null)byAbility[e.ability]=(c,e);}return byAbility;}}
  public static LP_ClassDef ClassOf(AbilityDef d)=>d!=null && ByAbility.TryGetValue(d,out var v)?v.cls:ClassLegendary.ClassOf(d)??ClassSubclass.ClassOf(d); // 子职业能力算所属职业
  public static ClassAbilityEntry EntryOf(AbilityDef d)=>d!=null && ByAbility.TryGetValue(d,out var v)?v.entry:null;
  public static LP_ClassDef ByLegacyPath(string path)=>path==null?null:All.FirstOrDefault(c=>c.legacyPath==path);
  // 1.81.0 热修2（Peter）：部位生命成长按职业生命骰（d12 野蛮人 7%，d10 6%，d8 5%，d6 4%）；没分配职业点的等级按 Hediff_Adventurer.PartPerLevel。
  public static float DefaultPartPer(string d){
   switch(d){
    case "LP_Class_Barbarian":return .07f;
    case "LP_Class_Fighter":case "LP_Class_Paladin":case "LP_Class_Ranger":return .06f;
    case "LP_Class_Wizard":case "LP_Class_Sorcerer":return .04f;
    default:return Hediff_Adventurer.PartPerLevel; // 牧师、德鲁伊、武僧、游荡者、器械师、未知职业
   }
  }
  // 1.81.0 热修3（Peter，照 D&D 物理职业）：武器伤害每职业等级 战士 / 野蛮人 +3%，圣武士 / 游侠 / 武僧 / 游荡者 +2%；近战穿甲 +0.015 / +0.01；施法职业 0。
  public static float DefaultDamagePer(string d){
   switch(d){
    case "LP_Class_Fighter":case "LP_Class_Barbarian":return .03f;
    case "LP_Class_Paladin":case "LP_Class_Ranger":case "LP_Class_Monk":case "LP_Class_Rogue":return .02f;
    default:return 0f;
   }
  }
  public static float DefaultPenPer(string d){
   switch(d){
    case "LP_Class_Fighter":case "LP_Class_Barbarian":return .015f;
    case "LP_Class_Paladin":case "LP_Class_Ranger":case "LP_Class_Monk":case "LP_Class_Rogue":return .01f;
    default:return 0f;
   }
  }
  // Σ 职业等级 × 每级成长（20 级战士：伤害 +0.6 → ×1.6；不乘属性效果强度）。
  public static float MartialDamage(Hediff_Adventurer h){float f=0f;if(h?.classes?.levels!=null)foreach(var kv in h.classes.levels)if(kv.Key!=null)f+=kv.Value*kv.Key.DamagePerLevel;return f;}
  public static float MartialPen(Hediff_Adventurer h){float f=0f;if(h?.classes?.levels!=null)foreach(var kv in h.classes.levels)if(kv.Key!=null)f+=kv.Value*kv.Key.PenPerLevel;return f;}

  // ---------- 数据 ----------
  public static ClassData Data(Pawn p)=>Adventurer.Get(p)?.classes;
  public static int Level(Pawn p,LP_ClassDef c)=>Level(Adventurer.Get(p),c);
  public static int Level(Hediff_Adventurer h,LP_ClassDef c){if(h?.classes==null || c==null)return 0;h.classes.levels.TryGetValue(c,out int n);return n;}
  public static int Total(Hediff_Adventurer h){if(h?.classes==null)return 0;int n=0;foreach(var v in h.classes.levels.Values)n+=v;return n;}
  public static int Budget(Hediff_Adventurer h)=>h==null?0:Mathf.Min(h.level,CapLevel);
  public static int Points(Hediff_Adventurer h)=>h==null?0:Mathf.Max(0,Budget(h)-Total(h));
  public static int Points(Pawn p)=>Points(Adventurer.Get(p));
  public static bool Any(Pawn p)=>Data(p)?.Any==true;
  public static IEnumerable<LP_ClassDef> Taken(Hediff_Adventurer h)=>h?.classes==null?Enumerable.Empty<LP_ClassDef>():All.Where(c=>Level(h,c)>0);
  public static int Skill(Pawn p,SkillDef s){if(p?.skills==null || s==null)return 0;var r=p.skills.GetSkill(s);return r==null || r.TotallyDisabled?0:r.Level;}
  // 主属性（LP_ClassDef.primary）：几项里取当前较高的一项。
  public static int Ability(LP_ClassDef c,Pawn p)=>c==null?-1:BestAbility(c.primary,p);
  public static int BestAbility(string spec,Pawn p){
   if(spec.NullOrEmpty())return -1;int best=-1;
   foreach(var s in spec.Split('/')){int a=Adventurer.AbilityIndex(s);if(a>=0 && (best<0 || Adventurer.Score(p,a)>Adventurer.Score(p,best)))best=a;}
   return best;
  }
  static int DefaultAbility(LP_ClassDef c)=>c==null || c.primary.NullOrEmpty()?Adventurer.Str:Math.Max(0,Adventurer.AbilityIndex(c.primary.Split('/')[0]));
  // 术士血脉（1.36.0 起在 VPE 桥的 ClassRules）：这些异种、六臂蛇人、坎比恩学术士时技能门槛 −2，天赋强度 +0.15。
  public static readonly HashSet<string> Bloodlines=new HashSet<string>{"LP_ForceDragon","LP_GenasiFire","LP_GenasiWater","LP_GenasiEarth","LP_GenasiAir","LP_Genie","LP_Titan","LP_StormGiant","LP_CloudGiant","LP_Cambion"};
  public const int BloodlineSkillBonus=2;public const float BloodlineBonus=.15f;
  public static bool Bloodline(Pawn p)=>p!=null && (p.genes?.Xenotype!=null && Bloodlines.Contains(p.genes.Xenotype.defName) || Marilith.Is(p) || AbyssRaces.IsCambion(p));
  static int GateBonus(LP_ClassDef c,Pawn p)=>c!=null && c.bloodline && Bloodline(p)?BloodlineSkillBonus:0;
  public static bool ScoreOk(LP_ClassDef c,Pawn p){if(!Adventurer.Enabled)return false;int a=Ability(c,p);return a>=0 && Adventurer.Score(p,a)>=ScoreGate;}
  // 主职业（等级最高，同级按 order）：自动加点、传奇属性点的角色。
  public static LP_ClassDef MainClass(Pawn p){var h=Adventurer.Get(p);LP_ClassDef best=null;int bl=0;foreach(var c in All){int l=Level(h,c);if(l>bl){bl=l;best=c;}}return best;}
  public static int Primary(Pawn p)=>Ability(MainClass(p),p);
  public static string Role(Pawn p){var c=MainClass(p);if(c==null)return null;if(c==Fighter && Skill(p,SkillDefOf.Shooting)>Skill(p,SkillDefOf.Melee))return "ranged";return "melee";}
  public static string Forbidden(LP_ClassDef c,Pawn p){
   if(c==null || !Items.IsLich(p))return null;
   if(c==Paladin)return "亡灵无法立下圣誓";
   if(c.defName=="LP_Class_Druid")return "死亡之躯与自然相斥";
   return null;
  }

  // ---------- 加点 ----------
  public static string GateText(LP_ClassDef c,int taken,Pawn p=null){int b=GateBonus(c,p);return taken==0?"需要"+c.SkillText+" "+(FirstSkill-b)+" 级（或"+Adventurer.Names[DefaultAbility(c)]+" "+ScoreGate+"）":"兼修需要冒险者 "+MultiAdventurerLevel+" 级，且"+c.SkillText+" "+(MultiSkill-b)+" 级（或"+Adventurer.Names[DefaultAbility(c)]+" "+ScoreGate+"）";}
  public static bool GateOk(Hediff_Adventurer h,LP_ClassDef c,int taken){
   var p=h.pawn;bool skill=c.SkillLevel(p)>=(taken==0?FirstSkill:MultiSkill)-GateBonus(c,p) || ScoreOk(c,p);
   return taken==0?skill:h.level>=MultiAdventurerLevel && skill;
  }
  // 能不能给 c 加 1 级；返回 null 表示可以。
  public static string CanAdd(Hediff_Adventurer h,LP_ClassDef c){
   if(h==null || c==null)return "没有这个职业";
   if(h.classes==null)h.classes=new ClassData();
   int cur=Level(h,c);if(cur>=CapLevel)return "已经 "+CapLevel+" 级";
   if(Points(h)<=0)return h.level>CapLevel?"传奇等级不给职业点":"没有职业点（每个冒险者等级 1 点）";
   if(cur>0)return null;
   var f=Forbidden(c,h.pawn);if(f!=null)return f;
   int taken=h.classes.levels.Count(kv=>kv.Value>0);
   if(taken>=MaxClasses)return "最多兼修 "+MaxClasses+" 个职业";
   if(!GateOk(h,c,taken) && h.classes.freeUnlocks<=0)return GateText(c,taken,h.pawn);
   return null;
  }
  public static bool AddLevel(Hediff_Adventurer h,LP_ClassDef c,bool message=false){
   if(CanAdd(h,c)!=null)return false;
   int cur=Level(h,c);
   if(cur==0){int taken=h.classes.levels.Count(kv=>kv.Value>0);if(!GateOk(h,c,taken))h.classes.freeUnlocks--;}
   SetLevel(h,c,cur+1);
   if(message && h.pawn!=null && h.pawn.Faction!=null && h.pawn.Faction.IsPlayer){
    var got=c.abilities.Where(e=>e.level==cur+1).Select(e=>e.ability.label).ToList();
    Messages.Message(h.pawn.LabelShort+"："+c.label+" "+(cur+1)+" 级"+(got.Count>0?"，获得 "+string.Join("、",got):""),h.pawn,MessageTypeDefOf.PositiveEvent,false);
   }
   return true;
  }
  // 直接设等级（迁移、导师、检查用）：新职业资源满；同步能力、物理职业缓存。
  public static void SetLevel(Hediff_Adventurer h,LP_ClassDef c,int level){
   if(h==null || c==null)return;if(h.classes==null)h.classes=new ClassData();
   level=Mathf.Clamp(level,0,CapLevel);
   bool fresh=Level(h,c)==0;
   if(level<=0){h.classes.levels.Remove(c);if(PoolLevel(h,c)<=0)h.classes.res.Remove(PoolKey(c));}
   else{h.classes.levels[c]=level;if(fresh && c.resource!=ClassResource.None)h.classes.res[PoolKey(c)]=MaxResource(h,c);}
   Sync(h);
   if(h.pawn!=null)Martial.Refresh(h.pawn);
   SyncFighterFeats(h);
   h.Dirty(); // 1.81.0 热修2：部位生命成长跟职业走，通知健康页重算
  }
  // 2026-10-31 热修（Peter：7 级战士没有选专长的机会；照 3.5 战士奖励专长）：战士职业等级 1、2 和之后每个偶数级各 1 个免费专长，不占属性提升。
  // 按已发水位线补发（老存档在 Tick250 补，只发不收）；玩家的人在“冒险者”分页里选，NPC 立即随机选一个合适的。
  public static readonly int[] FighterFeatLevels={1,2,4,6,8,10,12,14,16,18,20};
  public static int FighterFeatsDue(int fighterLevel){int n=0;foreach(int l in FighterFeatLevels)if(l<=fighterLevel)n++;return n;}
  public static int SyncFighterFeats(Hediff_Adventurer h,bool letter=false){
   if(h==null || Fighter==null)return 0;
   int due=FighterFeatsDue(Level(h,Fighter));if(due<=h.fighterFeatGranted)return 0;
   int gave=due-h.fighterFeatGranted;h.fighterFeatGranted=due;h.fighterFeats+=gave;
   var p=h.pawn;
   if(p==null || p.Faction==null || !p.Faction.IsPlayer){while(h.fighterFeats>0 && h.TryEnemyFeat(true)){}}
   else if(!p.Dead && !Adventurer.TestSuppressed){
    string text=p.LabelShort+"（战士 "+Level(h,Fighter)+" 级）获得 "+gave+" 个战士奖励专长：免费选一个专长，不占属性提升。战士 1、2 级和之后每个偶数级各 1 个。\n\n在“冒险者”分页的“可选专长”里选择。";
    if(letter)Find.LetterStack.ReceiveLetter("战士奖励专长",text,LetterDefOf.PositiveEvent,p);
    else Messages.Message(p.LabelShort+"获得 "+gave+" 个战士奖励专长（在冒险者分页选择）",p,MessageTypeDefOf.PositiveEvent,false);
   }
   h.Dirty();return gave;
  }
  // 应得的能力：等级到了（终极另要技能 15），或迁移时保留的。
  public static bool Entitled(Hediff_Adventurer h,LP_ClassDef c,ClassAbilityEntry e){
   if(e?.ability==null)return false;
   if(h.classes.kept.Contains(e.ability))return true;
   int l=Level(h,c);if(l<e.level)return false;
   return e.capstoneSkill==null || Skill(h.pawn,e.capstoneSkill)>=CapstoneSkill;
  }
  public static string Missing(Hediff_Adventurer h,LP_ClassDef c,ClassAbilityEntry e){
   if(Entitled(h,c,e))return null;
   int l=Level(h,c);if(l<e.level)return c.label+" "+e.level+" 级获得";
   return "还需要"+e.capstoneSkill.LabelCap+" "+CapstoneSkill+" 级";
  }
  // 给上应得的能力（只加不减：迁移时保留的旧能力也在这里补）。
  public static int Sync(Hediff_Adventurer h){
   var p=h?.pawn;if(p?.abilities==null || h.classes==null)return 0;int n=0;
   foreach(var c in All){if(Level(h,c)<=0 && !c.abilities.Any(e=>h.classes.kept.Contains(e.ability)))continue;
    foreach(var e in c.abilities)if(Entitled(h,c,e) && p.abilities.GetAbility(e.ability)==null){p.abilities.GainAbility(e.ability);n++;}}
   foreach(var a in h.classes.legendary)if(p.abilities.GetAbility(a)==null){p.abilities.GainAbility(a);n++;} // 1.74.0
   n+=ClassSubclass.Sync(h); // 子职业与通用能力
   return n;
  }
  // 1.87.0（Peter：最终特性下调到 15 级左右）：旧存档的人按新等级表补一次够格的能力（只加不减），玩家的人发一条消息列出补上的能力。
  // 额外攻击档位、第二个宿敌、魔能爆光束、传奇门槛都是实时按等级算的，不用补。
  public const int TableVersion=1;
  public static List<AbilityDef> Regrant(Hediff_Adventurer h){
   var got=new List<AbilityDef>();var p=h?.pawn;if(h?.classes==null || p?.abilities==null)return got;
   h.classes.table=TableVersion;
   var before=new HashSet<AbilityDef>(p.abilities.abilities.Select(a=>a.def));
   if(Sync(h)>0)got.AddRange(p.abilities.abilities.Select(a=>a.def).Where(d=>!before.Contains(d)));
   Martial.Refresh(p);
   if(got.Count>0 && p.Faction!=null && p.Faction.IsPlayer && !p.Dead && !Adventurer.TestSuppressed)
    Messages.Message(p.LabelShort+"：职业能力等级下调（终极 15 级），补上 "+string.Join("、",got.Select(d=>d.label))+"。",p,MessageTypeDefOf.PositiveEvent);
   return got;
  }
  public static bool Knows(Pawn p,string ability){var d=DefCache<AbilityDef>.Get(ability);return d!=null && p?.abilities?.GetAbility(d)!=null;}

  // ---------- 资源 ----------
  public static int MaxResource(int level)=>level<=0?0:2+(level+1)/2;
  public static string ResourceLabel(ClassResource r){switch(r){case ClassResource.Fury:return "斗志";case ClassResource.Ki:return "气";case ClassResource.Grace:return "神恩";case ClassResource.Mana:return "法力";case ClassResource.Rage:return "怒气";case ClassResource.Pact:return "契约法力";default:return "";}}
  // 1.73.0：神恩是牧师和圣武士共用的一条（存在圣武士名下），上限按两个职业等级之和（最多按 20 级算）；其他资源每个职业一条。
  public static LP_ClassDef PoolKey(LP_ClassDef c)=>c!=null && c.resource==ClassResource.Grace && Paladin!=null?Paladin:c;
  public static int PoolLevel(Hediff_Adventurer h,LP_ClassDef c){
   if(h?.classes==null || c==null)return 0;var key=PoolKey(c);int n=0;
   foreach(var kv in h.classes.levels)if(kv.Value>0 && PoolKey(kv.Key)==key)n+=kv.Value;
   return Mathf.Min(n,CapLevel);
  }
  public static float Resource(Hediff_Adventurer h,LP_ClassDef c){if(h?.classes==null || c==null)return 0f;h.classes.res.TryGetValue(PoolKey(c),out float v);return v;}
  public static float Resource(Pawn p,LP_ClassDef c)=>Resource(Adventurer.Get(p),c);
  // 1.81.0 热修4：法力（法师、术士、德鲁伊，各自一条）上限 2 + 职业等级（20 级 22，照 5e 的法术位数）；斗志 / 气 / 神恩 / 怒气仍是 2 + (等级+1)/2。
  public static int MaxMana(int level)=>level<=0?0:2+level;
  public static int MaxResource(Hediff_Adventurer h,LP_ClassDef c)=>c==null || c.resource==ClassResource.None?0:(c.resource==ClassResource.Mana?MaxMana(PoolLevel(h,c)):c.resource==ClassResource.Pact?Warlock.MaxPact(PoolLevel(h,c)):MaxResource(PoolLevel(h,c)))+ClassLegendary.ResourceFor(h,c)+ClassFeats.ResourceBonus(h,c); // 1.74.0 每个传奇能力 +2；契约法力 2 + 等级/4；1.88.0 职业专属专长
  public static float AddResource(Hediff_Adventurer h,LP_ClassDef c,float amount){
   int max=MaxResource(h,c);if(max<=0 || amount==0f)return 0f;
   if(amount>0f)amount*=ClassFeats.GainFactor(h,c); // 1.88.0 职业专属专长：资源获取
   float cur=Resource(h,c),next=Mathf.Clamp(cur+amount,0f,max);h.classes.res[PoolKey(c)]=next;return next-cur;
  }
  public static string ResourceTip(LP_ClassDef c){
   switch(c?.resource){
    case ClassResource.Fury:return "斗志：命中敌对、未倒地的目标 +"+(Martial.FuryHit*Martial.FuryResourcePer).ToString("0.##")+"，击杀敌人 +"+(Martial.FuryKill*Martial.FuryResourcePer).ToString("0.##")+"（每人每游戏小时最多 +"+(Martial.FuryCap*Martial.FuryResourcePer).ToString("0.#")+"）；睡觉时每 250 刻 +"+SleepRegen+"。有心灵链接时照旧另加精神力。";
    case ClassResource.Ki:return "气：冥想时每 250 刻 +"+MeditateRegen+"，睡觉时每 250 刻 +"+SleepRegen+"。";
    case ClassResource.Grace:return "神恩（牧师和圣武士共用一条，上限按两个职业等级之和）：祈祷或冥想时每 250 刻 +"+MeditateRegen+"（信奉神明的人 ×"+DevoteeRegen+"），睡觉时每 250 刻 +"+SleepRegen+"。";
    case ClassResource.Mana:return "法力（每个职业一条，上限 2 + 职业等级）：醒着时每小时约 +0.5，睡觉时每 250 刻 +"+SleepRegen+(c.defName=="LP_Class_Wizard"?"；法师在研究、读书或在卷轴书桌干活时每 250 刻另 +"+StudyRegen:"")+"。";
    case ClassResource.Rage:return "怒气：受到敌人伤害 +"+ClassMartial2.RageTaken+"，命中敌人 +"+ClassMartial2.RageDealt+"；不在狂暴中、10 秒没有攻防时每小时 −1（原初斗士不衰减）。睡觉不恢复。";
    case ClassResource.Pact:return "契约法力（上限 2 + 邪术师等级/4）：连续 1 小时冥想、娱乐或睡觉（短休）就回满，中断要重来；其他时候不恢复。";
    case ClassResource.None:return c.defName=="LP_Class_Artificer"?"器械师没有资源，能力只看冷却。":c.label+"没有资源，能力只看冷却。";
   }
   return "";
  }
  public static bool Meditating(Pawn p){var j=p?.CurJobDef;return j!=null && (j==JobDefOf.Meditate || j.defName.Contains("Pray") || j.defName.Contains("Worship"));}
  public static bool Devotee(Pawn p)=>FaithUtility.DevoteeGod(p)!=null || FaithUtility.IsChosen(p,out _);
  public const float StudyRegen=.1f,AwakeManaRegen=.0104f;
  // 法师读书 / 研究：研究、阅读，或者在卷轴书桌、研究台干活。
  public static bool Studying(Pawn p){var job=p?.CurJob;var j=job?.def;if(j==null)return false;if(j==JobDefOf.Research || j.defName=="Reading")return true;var b=job.targetA.Thing?.def;return b!=null && (b.defName=="LP_ScribeDesk" || b.thingClass!=null && typeof(Building_ResearchBench).IsAssignableFrom(b.thingClass));}
  // 每 250 刻：资源恢复（睡觉、冥想、祈祷），每 1000 刻补能力（终极看技能）。
  public static void Regen(Hediff_Adventurer h){
   var p=h.pawn;if(p==null || p.Dead)return;bool asleep=!p.Awake(),med=!asleep && Meditating(p);
   var done=new HashSet<LP_ClassDef>();
   foreach(var kv in h.classes.levels.ToList()){
    var c=kv.Key;if(kv.Value<=0 || c.resource==ClassResource.None || c.resource==ClassResource.Rage || c.resource==ClassResource.Pact || !done.Add(PoolKey(c)))continue; // 共用的神恩只恢复一次；怒气不靠睡觉；契约法力靠短休（Warlock.Rest）
    float add=0f;
    if(asleep)add=SleepRegen;
    else if(med && c.resource==ClassResource.Ki)add=MeditateRegen;
    else if(med && c.resource==ClassResource.Grace)add=MeditateRegen*(Devotee(p)?DevoteeRegen:1f);
    else if(c.resource==ClassResource.Mana && c.defName=="LP_Class_Wizard" && Studying(p))add=StudyRegen;
    if(!asleep && c.resource==ClassResource.Mana)add+=AwakeManaRegen; // 1.81.0 热修4：醒着也慢慢回（每小时约 +0.5），和研究的回复叠加
    if(add>0f)AddResource(h,c,add);
   }
  }
  public static void Tick250(Hediff_Adventurer h){
   NpcClasses.Tick(h); // NPC 职业（懒分配、升级加点、战斗中施放；玩家的人直接返回）
   var d=h.classes;if(d==null)return;
   if(h.level>=ClassLegendary.Thresholds[0])ClassLegendary.SyncPoints(h); // 1.74.0 传奇职业点（老存档在这里补发）
   if(d.levels.Count==0)return;
   if(h.fighterFeatGranted<FighterFeatLevels.Length)SyncFighterFeats(h,true); // 2026-10-31 战士奖励专长（老存档在这里补发）
   var p=h.pawn;if(p==null)return;
   if(d.table<TableVersion)Regrant(h); // 1.87.0 新等级表（老存档补一次）
   Regen(h);
   Warlock.Tick250(h); // 邪术师：短休回满契约法力、被动
   if((d.syncAcc+=1)>=4){d.syncAcc=0;Sync(h);}
   Martial.Refresh(p);
   if(p.Dead)return;
   ClassMartial2.Tick250(h); // 怒气衰减、狂战之魂、兽伴
   if(Knows(p,"LP_MartialArts"))Martial.RefreshStance(p);
   if(!p.Downed && p.Spawned)PaladinCore.Auras(p);
   ClassLegendary.Passives(h); // 1.74.0
   ClassAutoCast.Tick(h); // 玩家的人：开了自动施放的职业能力
  }

  // ---------- 施放 ----------
  public static float PsychicFactor(Pawn p,CompProperties_ClassEffect props)=>p==null || props==null || !props.psychic || !(LichMod.Settings?.classPsychic??true)?1f:Mathf.Max(PsychicMin,p.GetStatValue(StatDefOf.PsychicSensitivity));
  public static float StatValue(Pawn p,CompProperties_ClassEffect props){
   if(p==null || props?.stat==null)return 1f;
   float v=p.GetStatValue(props.stat);
   if(props.bestSkill){var m=DefCache<StatDef>.Get("LP_MarksmanPower");if(m!=null)v=Mathf.Max(v,p.GetStatValue(m));}
   return v;
  }
  public static float Strength(Pawn p,CompProperties_ClassEffect props)=>StatValue(p,props)*PsychicFactor(p,props);
  public static string CannotCast(Pawn p,AbilityDef def,CompProperties_ClassEffect props){
   if(props==null || props.passive)return null;
   var h=Adventurer.Get(p);var c=ClassOf(def);
   if(c==null || h==null)return null;
   if(c.caster && ClassMartial2.Raging(p))return "狂暴中无法施法";
   int cost=ClassLegendary.Cost(p,def,props);
   var mana=ClassSubclass.ManaCannot(h,def,cost);if(mana!=null)return mana; // 诡术师的法力
   if(cost>0 && c.resource!=ClassResource.None && !FreeCast(p,props) && Resource(h,c)+.001f<cost)return ResourceLabel(c.resource)+"不足（"+Resource(h,c).ToString("0.#")+" / "+cost+"）";
   if(props.ranged && Martial.RangedVerb(p)?.Projectile==null)return "需要远程武器";
   return null;
  }
  public static void Pay(Pawn p,AbilityDef def,CompProperties_ClassEffect props){
   if(props==null || props.cost<=0 || FreeCast(p,props))return;var h=Adventurer.Get(p);var c=ClassOf(def);
   if(ClassSubclass.PayMana(h,def,ClassLegendary.Cost(p,def,props)))return; // 诡术师的法力
   if(c==null || c.resource==ClassResource.None)return;
   int cost=ClassLegendary.Cost(p,def,props);
   if(h!=null && cost>0)AddResource(h,c,-cost);
   if((LichMod.Settings?.classEntropy??false) && p.psychicEntropy!=null && p.HasPsylink)p.psychicEntropy.TryAddEntropy(props.cost*EntropyPerCost,p,true,true);
  }

  // ---------- 物理职业的强度加成（取代 1.53.0 的 VPE 灵能等级加成） ----------
  public const float MartialPer=.02f,MartialMax=.4f;
  public static float MartialBonusFor(int total)=>Mathf.Clamp(MartialPer*(total-1),0f,MartialMax);
  public static float MartialBonus(Pawn p,SkillDef s)=>s==SkillDefOf.Melee || s==SkillDefOf.Shooting?Martial.Bonus(p):0f;
  // 1.73.0 施法职业：用这门技能当主 / 副技能的职业等级合计每比 1 高 1 级 +2%，最多 +40%（格斗、射击仍按物理职业合计）。
  public static float ClassBonus(Pawn p,SkillDef s){
   if(s==SkillDefOf.Melee || s==SkillDefOf.Shooting)return Martial.Bonus(p);
   var h=Adventurer.Get(p);if(h?.classes==null || h.classes.levels.Count==0 || s==null)return 0f;int n=0;
   foreach(var kv in h.classes.levels)if(kv.Key.mainSkill==s || kv.Key.alternateSkill==s)n+=kv.Value;
   return MartialBonusFor(n);
  }
  public static int ClassBonusLevels(Pawn p,SkillDef s){var h=Adventurer.Get(p);int n=0;if(h?.classes!=null)foreach(var kv in h.classes.levels)if(kv.Key.mainSkill==s || kv.Key.alternateSkill==s)n+=kv.Value;return n;}
  // 魔涌期间术士法术不耗法力。
  public static bool FreeCast(Pawn p,CompProperties_ClassEffect props)=>props!=null && (props.sorcery && Items.Has(p,"LP_SorcerousSurge") || props.effect!="ArcaneTorrent" && ClassLegendary.Torrent(p) || Warlock.Free(p,props)); // 1.74.0 魔力奔流；宗主化身
  // 1.73.0：背景 / 特性的职业经验加成（原来加在 VPE 路径经验上）改成冒险者经验 ×，取所学职业里最高的一项。
  public static float XpFactor(Pawn p){
   var h=Adventurer.Get(p);if(h?.classes==null || h.classes.levels.Count==0)return 1f;float f=1f;
   foreach(var kv in h.classes.levels){
    if(kv.Value<=0)continue;var c=kv.Key;float x=c.legacyPath==null?1f:BackgroundTraits.ClassXpFactor(p,c.legacyPath);
    if(c==Paladin && HasTrait(p,"LP_Background_Acolyte") || c==Monk && HasTrait(p,"LP_Background_Hermit"))x=Mathf.Max(x,1.25f);
    if(c==ClassMartial2.Barbarian && HasTrait(p,"LP_Background_Outlander") || c==ClassMartial2.Rogue && HasTrait(p,"LP_Background_Criminal")
     || c==ClassMartial2.Ranger && (HasTrait(p,"LP_Background_Outlander") || p.story?.Childhood?.defName=="LP_Bs_RangerGuide" || p.story?.Adulthood?.defName=="LP_Bs_RangerGuide"))x=Mathf.Max(x,1.25f);
    f=Mathf.Max(f,x);
   }
   return f;
  }
  static bool HasTrait(Pawn p,string n){var d=DefCache<TraitDef>.Get(n);return d!=null && p?.story?.traits?.HasTrait(d)==true;}
  // 职业强度（主程序集版，VPE 桥的 Classes.Power 同公式）：0.5 + 技能/20 + 冒险者主属性 + 物理职业加成。
  public static int SkillAbility(SkillDef s,Pawn p){
   if(s==SkillDefOf.Melee)return Adventurer.Score(p,Adventurer.Dex)>Adventurer.Score(p,Adventurer.Str)?Adventurer.Dex:Adventurer.Str;
   if(s==SkillDefOf.Shooting)return Adventurer.Dex;
   if(s==SkillDefOf.Intellectual || s==SkillDefOf.Crafting)return Adventurer.Int;
   if(s==SkillDefOf.Social)return Adventurer.Cha;
   if(s==SkillDefOf.Plants || s==SkillDefOf.Animals)return Adventurer.Wis;
   return -1;
  }
  public static float Power(int level)=>.5f+level/20f;
  public static float Power(Pawn p,SkillDef s)=>Power(Skill(p,s))+(p==null?0f:Adventurer.ClassPowerBonus(p,SkillAbility(s,p)))+ClassBonus(p,s);
  // 新职业的强度：技能取主 / 副较高者，属性按 ability（"Str"、"Dex/Wis"）指定。
  public static int PowerSkill(Pawn p,SkillDef s,SkillDef alt)=>Mathf.Max(Skill(p,s),alt==null?0:Skill(p,alt));
  public static int PowerAbility(Pawn p,SkillDef s,string ability)=>ability.NullOrEmpty()?SkillAbility(s,p):BestAbility(ability,p);
  public static float Power(Pawn p,SkillDef s,SkillDef alt,string ability)=>alt==null && ability.NullOrEmpty()?Power(p,s):Power(PowerSkill(p,s,alt))+(p==null?0f:Adventurer.ClassPowerBonus(p,PowerAbility(p,s,ability)))+ClassBonus(p,s);

  // ---------- 旧存档 / 新游戏的提示信 ----------
  public static void PromptLetter(){
   if(!Adventurer.Enabled)return;
   var list=PawnsFinder.AllMaps_FreeColonists.Where(p=>Points(p)>0).ToList();if(list.Count==0)return;
   Find.LetterStack.ReceiveLetter("冒险者职业","DND边缘大陆：十二个 DND 职业（战士、圣武士、武僧、法师、牧师、德鲁伊、术士、器械师、野蛮人、游侠、游荡者、邪术师）不再需要 VPE，改成冒险者的职业等级。每个冒险者等级给 1 点职业点（最多 20），可以分给最多 3 个职业。\n\n"+string.Join("\n",list.Select(p=>"· "+p.LabelShort+"：职业点 "+Points(p)))+"\n\n选中殖民者点“选择职业”按钮，或在“冒险者”分页的“职业”页里加点。",LetterDefOf.NeutralEvent,new LookTargets(list.Where(p=>p.Spawned)));
  }
 }

 // 职业强度属性（战技 / 射术 / 神恩强度）：0.5 + 技能/20 + 冒险者主属性 + 物理职业加成。
 public class StatPart_ClassPower : StatPart {
  public SkillDef skill,alternateSkill;public string ability; // 新职业：副技能（取较高）、指定属性
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p)val=ClassSystem.Power(p,skill,alternateSkill,ability)+ClassFeats.PowerBonus(p,parentStat);} // 1.88.0 职业专属专长
  public override string ExplanationPart(StatRequest req){
   if(!(req.Thing is Pawn p))return null;int l=ClassSystem.PowerSkill(p,skill,alternateSkill);float a=Adventurer.ClassPowerBonus(p,ClassSystem.PowerAbility(p,skill,ability)),v=ClassSystem.ClassBonus(p,skill);
   var sk=alternateSkill!=null && ClassSystem.Skill(p,alternateSkill)>ClassSystem.Skill(p,skill)?alternateSkill:skill;
   return sk.LabelCap+" "+l+" 级：0.5 + "+l+"/20 = "+ClassSystem.Power(l).ToStringPercent()+(a!=0f?"\n冒险者（主属性调整值 ×5% 与熟练加值）："+(a>0f?"+":"")+a.ToStringPercent():"")
    +(v!=0f?"\n"+(skill==SkillDefOf.Melee || skill==SkillDefOf.Shooting?"物理职业等级 "+(Martial.Info(p)?.total??0):"相关职业等级 "+ClassSystem.ClassBonusLevels(p,skill))+"：+"+v.ToStringPercent():"")
    +ClassFeats.PowerText(p,parentStat);
  }
 }
 // 术士血脉：天赋强度 +0.15（1.73.0 从 VPE 桥移来）。
 public class StatPart_SorcererBloodline : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && ClassSystem.Bloodline(p))val+=ClassSystem.BloodlineBonus;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && ClassSystem.Bloodline(p)?"术士血脉：+"+ClassSystem.BloodlineBonus.ToStringPercent():null;
 }

 // 选职业的窗口（不用浮动菜单：Peter 的游戏里浮动菜单的选择会失效）。
 public class Dialog_ChooseClass : Window {
  readonly Pawn pawn;string armed;Vector2 scroll;
  public override Vector2 InitialSize=>new Vector2(620f,620f);
  public Dialog_ChooseClass(Pawn p){pawn=p;doCloseX=true;doCloseButton=true;closeOnClickedOutside=false;absorbInputAroundWindow=true;forcePause=true;}
  public override void DoWindowContents(Rect r){
   var h=Adventurer.Get(pawn);if(h==null){Close();return;}
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(r.x,r.y,r.width,32f),pawn.LabelShort+" 的职业");Text.Font=GameFont.Small;
   float y=r.y+36f;int pts=ClassSystem.Points(h);
   Widgets.Label(new Rect(r.x,y,r.width,24f),"职业点：可用 "+pts+"（冒险者 Lv "+h.level+"，已用 "+ClassSystem.Total(h)+" / "+ClassSystem.Budget(h)+"）"+(ClassLegendary.Points(h)>0?"　传奇职业点 "+ClassLegendary.Points(h):"")+(h.classes.freeUnlocks>0?"　冒险者手册：下一个职业跳过门槛":""));y+=28f;
   // 1.73.0：八个职业，放进滚动区。
   // 1.74.0：有传奇职业点时，最上面列出能学的传奇职业能力。
   var legend=ClassLegendary.Points(h)>0?ClassSystem.Taken(h).SelectMany(c=>c.legendary.Where(a=>!ClassLegendary.Knows(h,a)).Select(a=>(c,a))).ToList():new List<(LP_ClassDef,AbilityDef)>();
   float legendH=legend.Count>0?28f+legend.Count*46f+8f:0f;
   var outer=new Rect(r.x,y,r.width,r.yMax-y-48f);var view=new Rect(0f,0f,outer.width-16f,legendH+ClassSystem.All.Count*104f);
   Widgets.BeginScrollView(outer,ref scroll,view);r=new Rect(0f,0f,view.width,view.height);y=0f;
   try{if(legend.Count>0)y=DrawLegend(r,y,h,legend);DrawRows(r,y,h,pts);}finally{Widgets.EndScrollView();}
  }
  float DrawLegend(Rect r,float y,Hediff_Adventurer h,List<(LP_ClassDef c,AbilityDef a)> list){
   GUI.color=new Color(1f,.82f,.35f);Widgets.Label(new Rect(r.x,y,r.width,24f),"传奇职业能力（传奇职业点 "+ClassLegendary.Points(h)+"）");GUI.color=Color.white;y+=28f;
   foreach(var (c,a) in list){
    var row=new Rect(r.x,y,r.width,44f);Widgets.DrawLightHighlight(row);
    GUI.DrawTexture(new Rect(row.x+4f,row.y+4f,36f,36f),a.uiIcon??BaseContent.BadTex);
    string why=ClassLegendary.CanLearn(h,c,a);bool ok=why==null;if(!ok)GUI.color=Color.gray;
    Widgets.Label(new Rect(row.x+46f,row.y+2f,row.width-160f,22f),c.label+"·"+a.LabelCap+(ok?"":"　— "+why));
    Text.Font=GameFont.Tiny;Widgets.Label(new Rect(row.x+46f,row.y+24f,row.width-160f,18f),(a.description??"").Truncate(row.width-160f));Text.Font=GameFont.Small;GUI.color=Color.white;
    TooltipHandler.TipRegion(new Rect(row.x,row.y,row.width-110f,row.height),a.LabelCap+"\n\n"+a.description+"\n\n学会后"+c.label+"的资源上限 +"+ClassLegendary.ResourceBonus+"。");
    string key="leg:"+a.defName;var b=new Rect(row.xMax-104f,row.y+7f,96f,30f);
    if(Widgets.ButtonText(b,armed==key?"确定？":"学习",active:ok) && ok){if(armed==key){armed=null;ClassLegendary.Learn(h,c,a,true);}else armed=key;}
    y+=46f;
   }
   return y+8f;
  }
  void DrawRows(Rect r,float y,Hediff_Adventurer h,int pts){
   foreach(var c in ClassSystem.All){
    var row=new Rect(r.x,y,r.width,100f);Widgets.DrawLightHighlight(row);
    var icon=c.Background;if(icon!=null){GUI.color=new Color(1f,1f,1f,.85f);GUI.DrawTexture(new Rect(row.x+4f,row.y+4f,48f,92f),icon,ScaleMode.ScaleAndCrop);GUI.color=Color.white;}
    int lv=ClassSystem.Level(h,c);string why=ClassSystem.CanAdd(h,c);bool ok=why==null;
    GUI.color=c.color;Widgets.Label(new Rect(row.x+60f,row.y+4f,260f,24f),c.LabelCap+(lv>0?" Lv "+lv:"（未学）"));GUI.color=Color.white;
    if(lv>0)Widgets.Label(new Rect(row.x+300f,row.y+4f,160f,24f),ClassSystem.ResourceLabel(c.resource)+" "+ClassSystem.Resource(h,c).ToString("0.#")+" / "+ClassSystem.MaxResource(h,c));
    Text.Font=GameFont.Tiny;
    var next=c.abilities.Where(e=>e.level==lv+1).Select(e=>e.ability.label).ToList();
    string line=ok?ClassLevelInfo.RowLine(pawn,h,c):null; // 1.89.0 部位生命 / 职业强度 现在 → 下一级
    Widgets.Label(new Rect(row.x+60f,row.y+28f,row.width-180f,68f),(ok?"":"不能加："+why+"\n")+(next.Count>0?"下一级获得："+string.Join("、",next):"下一级没有新能力")+"\n"+c.SkillText+" "+c.SkillLevel(pawn)+" 级"+(line!=null?"\n"+line:""));
    Text.Font=GameFont.Small;
    {var tr=new Rect(row.x,row.y,row.width-110f,row.height);if(Mouse.IsOver(tr))TooltipHandler.TipRegion(tr,ClassLevelInfo.ClassTip(pawn,h,c)+"\n\n"+ClassLevelInfo.ResourceTip(h,c));}
    string key=c.defName;var b=new Rect(row.xMax-104f,row.y+34f,96f,32f);
    if(Widgets.ButtonText(b,armed==key?"确定？":lv>0?"+1 级":"学习",active:ok) && ok){if(armed==key){armed=null;ClassLevelInfo.AddLevel(h,c);}else armed=key;}
    if(ok)TooltipHandler.TipRegion(b,armed==key?"再点一次确认":"点两次确认："+c.label+" "+(lv+1)+" 级");
    y+=104f;
   }
  }
 }

 // 新游戏不发提示信；读旧存档时，第一刻给还有职业点的殖民者发一封（在所有 FinalizeInit 之后，VPE 迁移已经做完）。
 // 斗志的每小时计数、精准射击模式也在这里（1.53.0 在 VPE 桥的 MartialState 里）。
 public class GameComponent_Classes : GameComponent {
  public bool notified;bool pendingPrompt;
  public Dictionary<int,float> furyGiven=new Dictionary<int,float>();public int furyHour=-1;
  public Dictionary<int,int> precisionMode=new Dictionary<int,int>();
  public GameComponent_Classes(Game game){}
  public override void StartedNewGame(){base.StartedNewGame();notified=true;}
  public override void LoadedGame(){base.LoadedGame();if(!notified)pendingPrompt=true;}
  public override void FinalizeInit(){base.FinalizeInit();Martial.Reset();}
  public override void GameComponentTick(){
   if(pendingPrompt){pendingPrompt=false;notified=true;if(!Adventurer.TestSuppressed)ClassSystem.PromptLetter();}
   if(Find.TickManager.TicksGame%Martial.HourTicks==0)Martial.Cleanup();
  }
  int Hour=>Find.TickManager.TicksGame/Martial.HourTicks;
  public float AllowFury(Pawn p,float want){
   int h=Hour;if(h!=furyHour){furyHour=h;furyGiven.Clear();}
   furyGiven.TryGetValue(p.thingIDNumber,out float given);
   float allowed=Mathf.Max(0f,Mathf.Min(want,Martial.FuryCap-given));
   if(allowed>0f)furyGiven[p.thingIDNumber]=given+allowed;
   return allowed;
  }
  public float FuryThisHour(Pawn p){if(p==null || Hour!=furyHour)return 0f;furyGiven.TryGetValue(p.thingIDNumber,out float g);return g;}
  public PrecisionMode Mode(Pawn p){precisionMode.TryGetValue(p.thingIDNumber,out int m);return (PrecisionMode)Mathf.Clamp(m,0,2);}
  public void SetMode(Pawn p,PrecisionMode m){if(m==PrecisionMode.Vital)precisionMode.Remove(p.thingIDNumber);else precisionMode[p.thingIDNumber]=(int)m;}
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref notified,"lpClassesNotified");
   Scribe_Values.Look(ref furyHour,"lpFuryHour",-1);
   Scribe_Collections.Look(ref furyGiven,"lpFuryGiven",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref precisionMode,"lpPrecisionMode",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(furyGiven==null)furyGiven=new Dictionary<int,float>();if(precisionMode==null)precisionMode=new Dictionary<int,int>();}
  }
 }

 // ---------- 设置（放在“冒险者”分组里） ----------
 public partial class Settings {
  public bool classPsychic=true,classEntropy=false;
  void ExposeClasses(){Scribe_Values.Look(ref classPsychic,"lpClassPsychic",true);Scribe_Values.Look(ref classEntropy,"lpClassEntropy",false);ExposeNpcClasses();}
 }
 public static class ClassSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.CheckboxLabeled("施法类能力受心灵敏感度影响",ref s.classPsychic,"1.72.0 职业：武僧的气功（拨挡箭矢、震慑拳、金刚之躯、颤心掌、空灵之身）和圣武士的神术（至圣斩、圣疗、神圣武器、放逐邪恶、圣光化身）的伤害、治疗再乘心灵敏感度（最低 ×0.2，不封顶）。战士的战技永远不乘。");
   NpcClasses.Draw(l); // NPC 职业
   l.CheckboxLabeled("职业能力消耗心灵熵",ref s.classEntropy,"1.72.0 职业：打开时，有心灵链接的人施放职业能力另外增加心灵熵（每 1 点资源消耗 +5）。默认关闭：职业能力只消耗斗志 / 气 / 神恩。");
  }
 }
}
