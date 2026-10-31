using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 子职业（见 ArtSource/Subclasses-spec.md）：野蛮人、游侠、游荡者各 3 个子职业（职业 3 级选，能力在 3/7/11/15 级）＋每个职业 2 个通用能力（7、11 级，general 定义，自动获得）。
 // 存在 ClassData.sub；能力由 ClassSystem.Sync 补发，ClassSystem.ClassOf 对子职业能力返回所属职业。被动按能力位（Flags），在 Martial.Refresh 时更新；
 // 伤害 / 命中挂在 ClassMartial2 的 Out / In / 伤害前缀 / 命中补丁里，第一步都是 Users 空集合判断。
 public class SubclassChoice {public string key,label,distinctFrom;public int level=3; // distinctFrom：不能和那个分支选同一项（邪术师的两次魔能祈唤）
 public List<string> options=new List<string>(),tips=new List<string>();}
 public class LP_SubclassDef : Def {
  public LP_ClassDef parent;public int order;public bool general;public string emblem;public Color color=Color.white;
  public List<ClassAbilityEntry> abilities=new List<ClassAbilityEntry>();
  public List<SubclassChoice> choices=new List<SubclassChoice>();
  [Unsaved] Texture2D tex;[Unsaved] bool texLoaded;
  public Texture2D Emblem{get{if(!texLoaded){texLoaded=true;tex=emblem.NullOrEmpty()?null:ContentFinder<Texture2D>.Get(emblem,false);}return tex??parent?.Background;}}
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(parent==null)yield return defName+": parent is null";
   foreach(var a in abilities)if(a.ability==null)yield return defName+": ability entry without ability";
  }
 }
 public class SubclassData : IExposable {
  public List<LP_SubclassDef> picks=new List<LP_SubclassDef>();
  public Dictionary<string,int> choices=new Dictionary<string,int>();
  public float mana;public Pawn companion;
  public Dictionary<string,int> ticks=new Dictionary<string,int>();
  public int lastFight=-99999,lastStrike=-99999;
  public AbilityDef stolen;public int stolenTick;
  public List<LP_ClassDef> reminded=new List<LP_ClassDef>();
  public void ExposeData(){
   Scribe_Collections.Look(ref picks,"picks",LookMode.Def);
   Scribe_Collections.Look(ref choices,"choices",LookMode.Value,LookMode.Value);
   Scribe_Values.Look(ref mana,"mana");Scribe_References.Look(ref companion,"companion");
   Scribe_Collections.Look(ref ticks,"ticks",LookMode.Value,LookMode.Value);
   Scribe_Values.Look(ref lastFight,"lastFight",-99999);Scribe_Values.Look(ref lastStrike,"lastStrike",-99999);
   Scribe_Defs.Look(ref stolen,"stolen");Scribe_Values.Look(ref stolenTick,"stolenTick");
   Scribe_Collections.Look(ref reminded,"reminded",LookMode.Def);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    if(picks==null)picks=new List<LP_SubclassDef>();picks.RemoveAll(x=>x==null);
    if(choices==null)choices=new Dictionary<string,int>();if(ticks==null)ticks=new Dictionary<string,int>();
    if(reminded==null)reminded=new List<LP_ClassDef>();reminded.RemoveAll(x=>x==null);
   }
  }
 }

 public static class ClassSubclass {
  public const int PickLevel=3,FightGap=2500,StolenTicks=20000,TrapMax=3,ChargeStun=120,PounceStun=120,DreadFearTicks=180,ReflexTicks=600,ShareRange=30;
  public const float ChargeRange=8f,ChargeDamage=5f,BossResist=.5f,BearFactor=.5f,EagleRanged=.7f,DarkFactor=.8f,ColossusFactor=1.25f,DreadFactor=1.5f,ShadowDodgeChance=.3f,
   DeadlyFactor=1.5f,DeathStrikeFactor=3f,AmbushResist=.2f,ThiefResist=.35f,GuardFactor=.75f,ShelterFactor=.75f,StormBase=1.5f,StormPer=.1f,StormExtra=.25f,ElementFactor=.5f,EagleRange=3f;
  public const string Berserker="LP_Sub_Berserker",Totem="LP_Sub_TotemWarrior",Storm="LP_Sub_StormHerald",Assassin="LP_Sub_Assassin",Thief="LP_Sub_Thief",Trickster="LP_Sub_ArcaneTrickster",
   Hunter="LP_Sub_Hunter",BeastMaster="LP_Sub_BeastMaster",Gloom="LP_Sub_GloomStalker";
  public static readonly Color SubColor=new Color(.85f,.75f,.5f);

  // ---------- 定义 ----------
  static List<LP_SubclassDef> all;static int allCount=-1;static Dictionary<AbilityDef,LP_SubclassDef> byAbility;
  public static List<LP_SubclassDef> All{get{if(all==null || allCount!=DefDatabase<LP_SubclassDef>.DefCount){all=DefDatabase<LP_SubclassDef>.AllDefsListForReading.OrderBy(d=>d.parent?.order??0).ThenBy(d=>d.order).ToList();allCount=DefDatabase<LP_SubclassDef>.DefCount;byAbility=null;}return all;}}
  public static LP_SubclassDef SubOf(AbilityDef d){
   var a=All;if(byAbility==null){byAbility=new Dictionary<AbilityDef,LP_SubclassDef>();foreach(var s in a)foreach(var e in s.abilities)if(e.ability!=null)byAbility[e.ability]=s;}
   return d!=null && byAbility.TryGetValue(d,out var r)?r:null;
  }
  public static LP_ClassDef ClassOf(AbilityDef d)=>SubOf(d)?.parent;
  public static IEnumerable<LP_SubclassDef> Options(LP_ClassDef c)=>All.Where(s=>s.parent==c && !s.general);
  public static LP_SubclassDef General(LP_ClassDef c)=>All.FirstOrDefault(s=>s.parent==c && s.general);

  // ---------- 数据 ----------
  public static SubclassData Data(Hediff_Adventurer h){var c=h?.classes;if(c==null)return null;return c.sub??(c.sub=new SubclassData());}
  public static SubclassData Data(Pawn p)=>Data(Adventurer.Get(p));
  public static LP_SubclassDef Picked(Hediff_Adventurer h,LP_ClassDef c){var d=Data(h);if(d==null || c==null)return null;foreach(var s in d.picks)if(s.parent==c)return s;return null;}
  public static bool Has(Hediff_Adventurer h,string sub){var d=Data(h);if(d==null)return false;foreach(var s in d.picks)if(s.defName==sub)return true;return false;}
  public static bool Has(Pawn p,string sub)=>Has(Adventurer.Get(p),sub);
  public static int PickLevelFor(LP_ClassDef c)=>c!=null && c.subclassLevel>0?c.subclassLevel:PickLevel; // 邪术师 1 级
  public static bool NeedsPick(Hediff_Adventurer h,LP_ClassDef c)=>c!=null && ClassSystem.Level(h,c)>=PickLevelFor(c) && Picked(h,c)==null && Options(c).Any();
  public static IEnumerable<LP_ClassDef> NeedPick(Hediff_Adventurer h)=>ClassSystem.Taken(h).Where(c=>NeedsPick(h,c));
  public static IEnumerable<(LP_SubclassDef s,SubclassChoice ch)> PendingChoices(Hediff_Adventurer h){
   var d=Data(h);if(d==null)yield break;
   foreach(var s in d.picks)foreach(var ch in s.choices)if(ClassSystem.Level(h,s.parent)>=ch.level && !d.choices.ContainsKey(ch.key))yield return (s,ch);
  }
  public static int Pending(Hediff_Adventurer h)=>h?.classes==null || h.classes.levels.Count==0?0:NeedPick(h).Count()+PendingChoices(h).Count();
  public static int Choice(Hediff_Adventurer h,string key){var d=Data(h);return d!=null && d.choices.TryGetValue(key,out int v)?v:-1;}
  public static int Choice(Pawn p,string key)=>Choice(Adventurer.Get(p),key);
  public static SubclassChoice FindChoice(Hediff_Adventurer h,string key){var d=Data(h);if(d==null)return null;foreach(var s in d.picks)foreach(var ch in s.choices)if(ch.key==key)return ch;return null;}
  public static string ChoiceLabel(Hediff_Adventurer h,SubclassChoice ch){int v=Choice(h,ch.key);return v>=0 && v<ch.options.Count?ch.options[v]:null;}

  public static string CanPick(Hediff_Adventurer h,LP_SubclassDef s){
   if(h?.classes==null || s==null || s.general || s.parent==null)return "没有这个子职业";
   if(ClassSystem.Level(h,s.parent)<PickLevelFor(s.parent))return "需要"+s.parent.label+" "+PickLevelFor(s.parent)+" 级";
   var cur=Picked(h,s.parent);if(cur!=null)return cur==s?"已经选了":"已经选了"+cur.label+"（选定后不能更改）";
   return null;
  }
  public static bool Pick(Hediff_Adventurer h,LP_SubclassDef s,bool message=false){
   if(CanPick(h,s)!=null)return false;var d=Data(h);d.picks.Add(s);
   if(s.defName==Trickster)d.mana=ManaMax(h);
   ClassSystem.Sync(h);if(h.pawn!=null)Martial.Refresh(h.pawn);
   if(message && h.pawn?.Faction!=null && h.pawn.Faction.IsPlayer){
    var more=s.choices.Where(c=>c.level<=ClassSystem.Level(h,s.parent)).Select(c=>c.label).ToList();
    Messages.Message(h.pawn.LabelShort+"踏上了「"+s.label+"」（"+s.parent.label+"）"+(more.Count>0?"，还要选择："+string.Join("、",more):""),h.pawn,MessageTypeDefOf.PositiveEvent,false);
   }
   return true;
  }
  public static string CanChoose(Hediff_Adventurer h,string key,int option){
   var d=Data(h);var ch=FindChoice(h,key);if(d==null || ch==null)return "还没有这个分支";
   if(d.choices.ContainsKey(key))return "已经选过（不能更改）";
   if(option<0 || option>=ch.options.Count)return "没有这个选项";
   if(!ch.distinctFrom.NullOrEmpty() && Choice(h,ch.distinctFrom)==option)return "已经选过「"+ch.options[option]+"」";
   var s=d.picks.First(x=>x.choices.Contains(ch));if(ClassSystem.Level(h,s.parent)<ch.level)return "需要"+s.parent.label+" "+ch.level+" 级";
   return null;
  }
  public static bool Choose(Hediff_Adventurer h,string key,int option,bool message=false){
   if(CanChoose(h,key,option)!=null)return false;Data(h).choices[key]=option;
   if(h.pawn!=null)Martial.Refresh(h.pawn);
   var ch=FindChoice(h,key);
   if(message && h.pawn?.Faction!=null && h.pawn.Faction.IsPlayer)Messages.Message(h.pawn.LabelShort+"的"+ch.label+"："+ch.options[option],h.pawn,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  public static bool Entitled(Hediff_Adventurer h,LP_SubclassDef s,ClassAbilityEntry e){
   if(h?.classes==null || e?.ability==null || ClassSystem.Level(h,s.parent)<e.level)return false;
   return s.general || Data(h).picks.Contains(s);
  }
  // ClassSystem.Sync 的尾巴：补发能力（只加不减）；新到 3 级没选子职业时提醒一次。
  public static int Sync(Hediff_Adventurer h){
   var p=h?.pawn;if(p?.abilities==null || h.classes==null || h.classes.levels.Count==0)return 0;int n=0;var d=Data(h);
   foreach(var s in All){
    if(s.parent==null || ClassSystem.Level(h,s.parent)<=0 || !s.general && !d.picks.Contains(s))continue;
    foreach(var e in s.abilities)if(Entitled(h,s,e) && p.abilities.GetAbility(e.ability)==null){p.abilities.GainAbility(e.ability);n++;}
   }
   if(p.Faction!=null && p.Faction.IsPlayer)foreach(var c in NeedPick(h).ToList())if(!d.reminded.Contains(c)){
    d.reminded.Add(c);
    if(!Adventurer.TestSuppressed && Current.ProgramState==ProgramState.Playing)Messages.Message(p.LabelShort+"的"+c.label+"到了 "+PickLevelFor(c)+" 级，可以选择子职业了（“选择子职业”按钮，或冒险者分页的职业页）。",p,MessageTypeDefOf.PositiveEvent,false);
   }
   return n;
  }

  // ---------- 被动能力位 ----------
  public const int SInstinct=1,SFrenzy=2,SMindless=4,SRetaliation=8,STotem=16,SAspect=32,SStormAura=64,SStormSoul=128,SShelter=256,SRagingStorm=512,
   SEvasion=1<<10,SAssassin=1<<11,SImpostor=1<<12,SDeadly=1<<13,SReflex=1<<14,SClimb=1<<15,STreasure=1<<16,SAmbush=1<<17,SSpellThief=1<<18,SFieldEye=1<<19,
   SPrey=1<<20,SDefense=1<<21,SSuperior=1<<22,SCompanion=1<<23,STraining=1<<24,SBeastFury=1<<25,SShare=1<<26,SDread=1<<27,SIronMind=1<<28,SFlurry=1<<29,SShadowDodge=1<<30;
  static readonly Dictionary<string,int> FlagOf=new Dictionary<string,int>{
   {"LP_FeralInstinct",SInstinct},{"LP_Frenzy",SFrenzy},{"LP_MindlessRage",SMindless},{"LP_Retaliation",SRetaliation},{"LP_TotemSpiritRage",STotem},{"LP_AspectOfBeast",SAspect},
   {"LP_StormAura",SStormAura},{"LP_StormSoul",SStormSoul},{"LP_ShieldingStorm",SShelter},{"LP_RagingStorm",SRagingStorm},
   {"LP_RogueEvasion",SEvasion},{"LP_AssassinsOpening",SAssassin},{"LP_Impostor",SImpostor},{"LP_DeadlyStrike",SDeadly},{"LP_ThiefReflexes",SReflex},{"LP_SecondStoryWork",SClimb},
   {"LP_TreasureSense",STreasure},{"LP_MagicalAmbush",SAmbush},{"LP_SpellThief",SSpellThief},{"LP_FieldEye",SFieldEye},{"LP_HuntersPrey",SPrey},{"LP_DefensiveTactics",SDefense},
   {"LP_SuperiorDefense",SSuperior},{"LP_RangerCompanion",SCompanion},{"LP_ExceptionalTraining",STraining},{"LP_BestialFury",SBeastFury},{"LP_ShareSpells",SShare},
   {"LP_DreadAmbusher",SDread},{"LP_IronMind",SIronMind},{"LP_StalkersFlurry",SFlurry},{"LP_ShadowyDodge",SShadowDodge}};
  public static readonly Dictionary<int,int> Users=new Dictionary<int,int>();
  public static readonly HashSet<int> Eagles=new HashSet<int>();
  public static readonly HashSet<Pawn> Climbers=new HashSet<Pawn>(),Companions=new HashSet<Pawn>();
  public static int Flags(Pawn p){if(p==null || Users.Count==0)return 0;Users.TryGetValue(p.thingIDNumber,out int f);return f;}
  public static bool S(Pawn p,int f)=>(Flags(p) & f)!=0;
  // Martial.Refresh → ClassMartial2.Refreshed → 这里（i 为空表示已经不是物理职业）。
  public static void Refreshed(Pawn p,MartialInfo i){
   if(p==null)return;int id=p.thingIDNumber,f=0;
   if(i!=null && !p.Dead){var list=p.abilities?.abilities;if(list!=null)for(int k=0;k<list.Count;k++)if(FlagOf.TryGetValue(list[k].def.defName,out int b))f|=b;}
   if(f==0)Users.Remove(id);else Users[id]=f;
   if((f & SAspect)!=0 && Choice(p,"totem")==1)Eagles.Add(id);else Eagles.Remove(id);
   if((f & SClimb)!=0)Climbers.Add(p);else Climbers.Remove(p);
   if((f & SCompanion)==0){var c=Data(p)?.companion;if(c!=null)Companions.Remove(c);}
  }
  public static void Reset(){Users.Clear();Eagles.Clear();Climbers.Clear();Companions.Clear();}
  public static void Cleanup(){
   if(Climbers.Count>0)Climbers.RemoveWhere(x=>x==null || x.Destroyed || x.Dead);
   if(Companions.Count>0)Companions.RemoveWhere(x=>x==null || x.Destroyed || x.Dead);
  }
  static int Now=>Find.TickManager.TicksGame;
  static float Roll=>ClassMartial2.RollOverride??Rand.Value;
  static bool Ready(SubclassData d,string key,int ticks){if(d==null)return false;d.ticks.TryGetValue(key,out int t);int now=Now;return t<=0 || now-t>=ticks || now<t;}
  static void Use(SubclassData d,string key){if(d!=null)d.ticks[key]=Math.Max(1,Now);}

  // ---------- 诡术师的法力 ----------
  static readonly HashSet<string> ManaSpells=new HashSet<string>{"LP_ATMagicMissile","LP_ATInvisibility","LP_ATSleep"};
  public static bool UsesMana(AbilityDef d)=>d!=null && ManaSpells.Contains(d.defName);
  public static int ManaMax(Hediff_Adventurer h)=>Has(h,Trickster)?ClassSystem.MaxResource(ClassSystem.Level(h,ClassMartial2.Rogue)):0;
  public static float Mana(Hediff_Adventurer h)=>Data(h)?.mana??0f;
  public static float Mana(Pawn p)=>Mana(Adventurer.Get(p));
  public static float AddMana(Hediff_Adventurer h,float a){int max=ManaMax(h);if(max<=0 || a==0f)return 0f;var d=Data(h);float cur=d.mana,next=Mathf.Clamp(cur+a,0f,max);d.mana=next;return next-cur;}
  public static string ManaCannot(Hediff_Adventurer h,AbilityDef def,int cost){if(!UsesMana(def) || cost<=0)return null;float m=Mana(h);return m+.001f<cost?"法力不足（"+m.ToString("0.#")+" / "+cost+"）":null;}
  public static bool PayMana(Hediff_Adventurer h,AbilityDef def,int cost){if(!UsesMana(def))return false;if(h!=null && cost>0)AddMana(h,-cost);return true;}

  // ---------- 小工具 ----------
  public static int LevelOf(Pawn t){var h=Adventurer.Get(t);if(h!=null)return h.level;return BeastLevels.Get(t)?.level??1;}
  public static bool Bossy(Pawn t)=>ClassMartial2.Boss(t) || HeroDestiny.Boss(t);
  public static float ResistChance(Pawn t)=>Bossy(t)?BossResist:Mathf.Clamp(.1f+.02f*LevelOf(t),.1f,.5f);
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.HostileTo(a);
  static bool Friendly(Pawn a,Pawn t)=>t!=null && !t.Dead && !t.HostileTo(a);
  public static bool FearState(MentalStateDef d)=>d!=null && (d==MentalStateDefOf.PanicFlee || d.defName.Contains("Flee") || d.defName.Contains("Panic") || d.defName.Contains("Terror"));
  // 恐惧：能逃的人形 / 动物逃跑 ticks 刻，否则震慑。
  public static bool Fear(Pawn t,int ticks,string reason,bool allowBoss=false){
   if(t==null || t.Dead)return false;
   if(!t.Downed && (t.RaceProps.Humanlike || t.RaceProps.Animal) && (allowBoss || !Bossy(t)) && t.mindState?.mentalStateHandler!=null && t.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,reason,true,true)){
    if(t.MentalState!=null)t.MentalState.forceRecoverAfterTicks=ticks;return true;
   }
   Items.Give(t,"LP_Shaken",ticks,1f);return false;
  }
  public static bool NotInCombat(Pawn v){var m=v?.mindState;if(m==null || v.Downed)return false;int now=Now;return m.enemyTarget==null && now-m.lastHarmTick>600 && now-v.LastAttackTargetTick>600;}
  public static bool Unsurprisable(Pawn v)=>Users.Count>0 && S(v,SInstinct);
  public static bool Controlled(Pawn v)=>v.Downed || !v.Awake() || v.stances?.stunner?.Stunned==true || v.InMentalState || Items.Has(v,"LP_MagicSleep") || Items.Has(v,"LP_Entangled");
  public static bool Dark(Pawn t)=>t.Spawned && t.Map.glowGrid.GroundGlowAt(t.Position)<.3f;
  public static bool Psychic(DamageDef d)=>d!=null && (d.defName.Contains("Psychic") || d.defName.Contains("Psy"));
  // 元素：0 火（沙漠）、1 雷（海洋）、2 冰（冻原），其他 −1。
  public static int Element(DamageDef d){
   if(d==null)return -1;var n=d.defName;
   if(d==DamageDefOf.Flame || d==DamageDefOf.Burn || n.Contains("Fire") || n.Contains("Flame") || n.Contains("Burn") || n.Contains("Hellfire"))return 0;
   if(n.Contains("Lightning") || n.Contains("Shock") || n.Contains("Electric") || n.Contains("Thunder"))return 1;
   if(d==DamageDefOf.Frostbite || n.Contains("Frost") || n.Contains("Cold") || n.Contains("Ice"))return 2;
   return -1;
  }
  static DamageDef AuraDamage(int el,Pawn t){
   if(el==1)return t.RaceProps.IsMechanoid?DamageDefOf.EMP:(ClassCasters.Lightning??DamageDefOf.Burn);
   if(el==2)return DefCache<DamageDef>.Get("LP_Frost")??DamageDefOf.Frostbite;
   return DamageDefOf.Burn;
  }
  static void Passive(Pawn p,string hediff,bool on,float severity=-1f){
   if(on){var h=Items.Hediff(hediff);var cur=h==null?null:p.health.hediffSet.GetFirstHediffOfDef(h);if(cur==null)Items.Give(p,hediff,-1,severity);else if(severity>0f && cur.Severity!=severity)cur.Severity=severity;}
   else Items.Remove(p,hediff);
  }
  // 新的一场战斗（1 小时没有攻防）：盗贼反射。
  static void Fight(Pawn p,SubclassData d,int f){
   if(d==null)return;int now=Now;bool fresh=now-d.lastFight>FightGap || now<d.lastFight;d.lastFight=now;
   if(fresh && (f & SReflex)!=0){Items.Give(p,"LP_ThiefReflexBuff",ReflexTicks);Items.Mote(p,"盗贼反射",ClassMartial2.ShadowColor);}
  }

  // ---------- 伤害（ClassMartial2.Out / In 里调用） ----------
  // 暗杀：目标不在战斗中 → 重击 ×2 并必定偷袭。
  public static bool Opening(Pawn a,Pawn v)=>Users.Count>0 && S(a,SAssassin) && !Unsurprisable(v) && NotInCombat(v);
  // 额外的偷袭条件：稳固目标、多才多艺的诡计。
  public static bool SneakExtra(Pawn a,Pawn v)=>Users.Count>0 && (Items.Has(a,"LP_RogueSteady") || Items.Has(v,"LP_Distracted"));
  public static float Out(Pawn a,Pawn v,DamageInfo d,bool hostile){
   int f=Flags(a);float g=1f;
   if(f!=0 && hostile){
    var sd=Data(a);Fight(a,sd,f);
    if((f & SDread)!=0 && sd!=null){int now=Now;bool first=now-sd.lastStrike>FightGap || now<sd.lastStrike;sd.lastStrike=now;if(first){g*=DreadFactor;Fear(v,DreadFearTicks,"恐惧伏击");Items.Mote(v,"恐惧伏击",ClassMartial2.HuntColor);}}
    if((f & SPrey)!=0){
     int c=Choice(a,"prey");
     if(c==0 && v.health.summaryHealth.SummaryHealthPercent<.999f)g*=ColossusFactor;
     else if(c==1 && ClassMartial2.Melee(d) && a.Spawned && Ready(sd,"horde",60)){
      var adj=Races124.PawnsNear(a.Map,a.Position,1.5f).Where(x=>Hostile(a,x) && !x.Downed).ToList();
      if(adj.Count>=3){var other=adj.FirstOrDefault(x=>x!=v);if(other!=null){Use(sd,"horde");Fiends.Defer(()=>{if(!a.Dead && !a.Downed && !other.Dead && other.Spawned)ClassMelee.Strike(a,other);});Items.Mote(a,"群敌杀手",ClassMartial2.HuntColor);}}
     }
    }
   }
   if(Items.Has(a,"LP_WolfPounce") && ClassMartial2.Melee(d) && v.BodySize<=2f){v.stances?.stunner?.StunFor(PounceStun,a);Items.Mote(v,"扑倒",ClassMartial2.RageColor);}
   if(Items.Has(a,"LP_RogueSteady"))Items.Remove(a,"LP_RogueSteady");
   if(Items.Has(a,"LP_ATInvisible"))Items.Remove(a,"LP_ATInvisible");
   return g;
  }
  // 返回 −1 表示完全躲开。
  public static float In(Pawn v,Pawn a,DamageInfo d){
   int f=Flags(v);if(f==0)return 1f;float g=1f;var sd=Data(v);bool hostile=a!=null && a!=v && a.HostileTo(v);
   if(hostile)Fight(v,sd,f);
   if((f & SShadowDodge)!=0 && hostile && !v.Downed && Ready(sd,"shadowDodge",300) && Roll<ShadowDodgeChance){Use(sd,"shadowDodge");Items.Mote(v,"暗影闪避",ClassMartial2.HuntColor);return -1f;}
   if((f & STotem)!=0 && ClassMartial2.Raging(v) && Choice(v,"totem")==0 && !Psychic(d.Def)){var cat=d.Def.armorCategory?.defName;g*=cat=="Sharp" || cat=="Blunt"?BearFactor/ClassMartial2.RageReduction:BearFactor;}
   if((f & SEvasion)!=0 && d.Def.isExplosive)g*=.5f;
   if((f & SStormSoul)!=0){int el=Choice(v,"element");if(el>=0 && Element(d.Def)==el)g*=ElementFactor;}
   if((f & SDefense)!=0 && sd!=null){
    int c=Choice(v,"defense");
    if(c==0 && hostile){sd.ticks.TryGetValue("multiA",out int who);sd.ticks.TryGetValue("multiT",out int t);if(who==a.thingIDNumber && Now-t<180 && Now>=t)g*=.5f;sd.ticks["multiA"]=a.thingIDNumber;sd.ticks["multiT"]=Now;}
    else if(c==1 && (d.Def.isExplosive || Element(d.Def)==0))g*=.5f;
   }
   if((f & SSuperior)!=0 && sd!=null){
    int key=d.Def.shortHash;sd.ticks.TryGetValue("supD",out int last);sd.ticks.TryGetValue("supT",out int t);
    if(last==key && Now-t<600 && Now>=t){g*=.5f;sd.ticks.Remove("supD");Items.Mote(v,"高级猎手防御",ClassMartial2.HuntColor);}
    else{sd.ticks["supD"]=key;sd.ticks["supT"]=Now;}
   }
   if((f & SRetaliation)!=0 && hostile && ClassMartial2.Melee(d) && a.Spawned && v.Spawned && a.Map==v.Map && a.Position.AdjacentTo8WayOrInside(v.Position) && !v.Downed && Ready(sd,"retaliate",60)){
    Use(sd,"retaliate");var att=a;Fiends.Defer(()=>{if(!v.Dead && !v.Downed && !att.Dead && att.Spawned){ClassMelee.Strike(v,att);Items.Mote(v,"报复",ClassMartial2.RageColor);}});
   }
   return g;
  }
  // 任何受击者（不一定是职业者）：庇护风暴、伙伴守护。
  public static float Ward(Pawn v,DamageInfo d){
   float g=1f;
   var sh=Items.Hediff("LP_StormShelter");var h=sh==null?null:v.health?.hediffSet?.GetFirstHediffOfDef(sh);
   if(h!=null && Element(d.Def)==Mathf.RoundToInt(h.Severity)-1)g*=ShelterFactor;
   var gd=Items.Hediff("LP_CompanionGuard");var gh=gd==null?null:v.health?.hediffSet?.GetFirstHediffOfDef(gd) as Hediff_ClassMark;
   if(gh?.caster!=null && gh.caster.Spawned && v.Spawned && gh.caster.Map==v.Map && !gh.caster.Downed && gh.caster.Position.InHorDistOf(v.Position,3f))g*=GuardFactor;
   return g;
  }
  // 兽王伙伴打游侠的宿敌 +20%（同兽伴，不叠加）。
  public static float CompanionOut(Pawn a,Pawn v){var m=a.playerSettings?.Master;return m!=null && ClassMartial2.Favored(m,v)?1f+ClassMartial2.BondDamage:1f;}
  // 命中：受击方（鹰之图腾、暗处的幽域追踪者）。
  public static float HitAgainst(Pawn t,bool ranged){
   int f=Flags(t);if(f==0)return 1f;float g=1f;
   if(ranged && (f & STotem)!=0 && ClassMartial2.Raging(t) && Choice(t,"totem")==1)g*=EagleRanged;
   if((f & SDread)!=0 && Dark(t))g*=DarkFactor;
   return g;
  }
  public static bool SteadyHit(Pawn a)=>Items.Has(a,"LP_RogueSteady");
  public static bool Flurry(Pawn a)=>S(a,SFlurry);

  // ---------- 每 250 刻 ----------
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;if(p==null || p.Dead)return;var d=Data(h);int f=Flags(p);
   if(d.picks.Count>0 && Has(h,Trickster)){
    bool asleep=!p.Awake();if(asleep)AddMana(h,ClassSystem.SleepRegen);else if(ClassSystem.Studying(p))AddMana(h,ClassSystem.StudyRegen);
   }
   if(d.stolen!=null && (Now-d.stolenTick>StolenTicks || Now<d.stolenTick))DropStolen(p,d);
   if(f==0)return;
   bool raging=ClassMartial2.Raging(p);
   if(raging && (f & STotem)!=0){
    int t=Choice(h,"totem");
    if(t==1)Items.Give(p,"LP_TotemEagleRage",300);
    else if(t==2 && p.Spawned)foreach(var o in Races124.PawnsNear(p.Map,p.Position,5f).ToList())if(o!=p && Friendly(p,o) && o.Faction==p.Faction)Items.Give(o,"LP_WolfPackBuff",300);
   }
   Passive(p,"LP_TotemAspect",(f & SAspect)!=0 && Choice(h,"totem")>=0,Choice(h,"totem")+1);
   Passive(p,"LP_StormSoulBuff",(f & SStormSoul)!=0 && Choice(h,"element")>=0,Choice(h,"element")+1);
   Passive(p,"LP_SecondStoryBuff",(f & SClimb)!=0);
   Passive(p,"LP_IronMindBuff",(f & SIronMind)!=0);
   if((f & SIronMind)!=0 || raging && (f & SMindless)!=0)Items.Remove(p,"LP_Shaken");
   if((f & SAspect)!=0 && Choice(h,"totem")==2 && p.Spawned)foreach(var o in Races124.PawnsNear(p.Map,p.Position,40f).ToList())if(Hostile(p,o) && ClassMartial2.Fleeing(o))Items.Give(o,"LP_Tracked",300);
   if((f & SFieldEye)!=0 && p.Spawned)FieldEye(p,d);
   if((f & SCompanion)!=0)CompanionTick(p,h,d,f);
  }
  // 狂暴中每秒（Hediff_Rage.TickInterval）：狂乱、无脑狂怒、风暴光环。
  public static void RageTick(Pawn p){
   int f=Flags(p);if(f==0 || p.Dead || !p.Spawned)return;
   if((f & SMindless)!=0){Items.Remove(p,"LP_Shaken");if(p.InMentalState && FearState(p.MentalStateDef))p.MentalState.RecoverFromState();}
   if((f & SFrenzy)!=0 && !p.Downed){
    var t=p.CurJobDef==JobDefOf.AttackMelee?p.CurJob.targetA.Thing as Pawn:null;
    if(Hostile(p,t) && !t.Downed && t.Spawned && t.Position.AdjacentTo8WayOrInside(p.Position))ClassMelee.Strike(p,t);
   }
   if((f & SStormAura)!=0)StormPulse(p,f);
  }
  public static void RageEnded(Pawn p){if(p!=null && !p.Dead && S(p,SFrenzy))Items.Give(p,"LP_FrenzyFatigue",2500);}
  public static float StormRadius(int f)=>(f & SRagingStorm)!=0?5f:3f;
  public static float StormAmount(Pawn p,int f){var s=DefCache<StatDef>.Get("LP_RagePower");return (StormBase+StormPer*ClassSystem.Level(p,ClassMartial2.Barbarian))*(s==null?1f:p.GetStatValue(s))*((f & SRagingStorm)!=0?2f:1f);}
  public static int StormPulse(Pawn p,int f){
   var map=p.Map;if(map==null)return 0;int el=Mathf.Max(0,Choice(p,"element")),n=0;float amount=StormAmount(p,f);
   foreach(var t in Races124.PawnsNear(map,p.Position,StormRadius(f)).ToList()){
    if(t==p || t.Dead)continue;
    if(Hostile(p,t)){
     t.TakeDamage(new DamageInfo(AuraDamage(el,t),amount,.2f,-1f,p));n++;
     if((f & SRagingStorm)!=0 && !t.Dead && Roll<StormExtra){
      if(el==0)FireUtility.TryAttachFire(t,.3f,p);else if(el==1)t.stances?.stunner?.StunFor(60,p);else Items.Give(t,"LP_Hobbled",120);
     }
    }else if((f & SShelter)!=0 && t.Faction!=null && t.Faction==p.Faction)Items.Give(t,"LP_StormShelter",300,el+1);
   }
   if(el==1)FleckMaker.ThrowLightningGlow(p.DrawPos,map,1.5f);else FleckMaker.ThrowDustPuffThick(p.DrawPos,map,1.5f,el==0?new Color(1f,.55f,.25f):new Color(.8f,.9f,1f));
   return n;
  }
  static void FieldEye(Pawn p,SubclassData d){
   var l=Adventurer.Get(p)?.classes?.favored;if(l==null || l.Count==0)return;int n=0;
   foreach(var t in p.Map.mapPawns.AllPawnsSpawned){
    if(!Hostile(p,t) || t.Downed)continue;bool fav=false;for(int i=0;i<l.Count;i++)if(ClassMartial2.FoeMatches(l[i],t)){fav=true;break;}
    if(!fav)continue;if(!Items.Has(t,"LP_Spotted"))n++;Items.Give(t,"LP_Spotted",600);
   }
   if(n>0 && Ready(d,"fieldEye",2500) && p.Faction!=null && p.Faction.IsPlayer){Use(d,"fieldEye");Messages.Message("原野之眼："+p.LabelShort+"发现了 "+n+" 个宿敌。",p,MessageTypeDefOf.NeutralEvent,false);}
  }
  static void CompanionTick(Pawn p,Hediff_Adventurer h,SubclassData d,int f){
   var c=d.companion;
   if(c==null)return;
   if(c.Destroyed || c.Dead){Companions.Remove(c);if(c.Dead && p.Faction!=null && p.Faction.IsPlayer)Messages.Message(p.LabelShort+"的游侠伙伴"+c.LabelShort+"死了，可以重新选一只。",p,MessageTypeDefOf.NegativeEvent,false);d.companion=null;return;}
   Companions.Add(c);
   var bl=BeastLevels.Get(c);int want=Mathf.Clamp(h.level,1,BeastLevels.MaxLevel);if(bl!=null && bl.level<want){bl.level=want;bl.xp=0f;bl.Dirty();}
   if((f & SBeastFury)!=0)Items.Give(c,"LP_BeastFuryBuff",300);
   if((f & SShare)!=0 && c.Downed && Ready(d,"revive",GenDate.TicksPerDay)){Use(d,"revive");ClassMelee.Heal(c,100f);Items.Mote(c,"分享法术：救起伙伴",ClassMartial2.HuntColor);}
  }
  public static Pawn Companion(Pawn ranger)=>Data(ranger)?.companion;
  public static bool Bond(Pawn ranger,Pawn animal){
   var d=Data(ranger);if(d==null || animal==null || animal.Dead || !animal.RaceProps.Animal || animal.Faction!=ranger.Faction)return false;
   if(d.companion!=null)Companions.Remove(d.companion);
   d.companion=animal;
   if(animal.training!=null)foreach(var td in new[]{TrainableDefOf.Tameness,TrainableDefOf.Obedience})if(td!=null && !animal.training.HasLearned(td) && animal.training.CanAssignToTrain(td).Accepted)animal.training.Train(td,null,true); // 有主人要先会服从
   if(animal.playerSettings!=null)animal.playerSettings.Master=ranger;Companions.Add(animal);
   CompanionTick(ranger,Adventurer.Get(ranger),d,Flags(ranger));
   return true;
  }
  public static void DropStolen(Pawn p,SubclassData d){
   if(d.stolen!=null && p.abilities?.GetAbility(d.stolen)!=null && !p.abilities.abilities.Any(a=>a.def==d.stolen && ClassSystem.ClassOf(a.def)!=null))p.abilities.RemoveAbility(d.stolen);
   d.stolen=null;d.stolenTick=0;
  }
  // 偷取法术：敌人对诡术师施放原版能力时有 35% + 心智抗性 的成功率抵抗并偷走。
  public static bool TrySteal(Ability ab,LocalTargetInfo target){
   var t=target.Thing as Pawn;var caster=ab.pawn;
   if(t==null || caster==null || t==caster || !S(t,SSpellThief) || !caster.HostileTo(t) || ab.def==null || ClassSystem.ClassOf(ab.def)!=null)return false;
   if(Roll>=Mathf.Clamp01(ThiefResist+DNDMind.Resistance(t)))return false;
   var d=Data(t);Items.Mote(t,"偷取法术："+ab.def.label,ClassCasters.Arcane);
   if(d!=null && t.abilities!=null && t.abilities.GetAbility(ab.def)==null){if(d.stolen!=null)DropStolen(t,d);t.abilities.GainAbility(ab.def);d.stolen=ab.def;d.stolenTick=Math.Max(1,Now);}
   return true;
  }

  // ---------- 施放之后 ----------
  public static void AfterCast(Ability_Class a){
   var p=a.pawn;var e=a.Props;if(p==null || e==null || Users.Count==0)return;int f=Flags(p);if(f==0)return;
   if(a.def.defName=="LP_ShadowStep" && (f & SImpostor)!=0){var h=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ShadowStep")) as HediffWithComps;var tm=h?.TryGetComp<HediffComp_Disappears>();if(tm!=null)tm.ticksToDisappear*=2;}
   if((f & SShare)!=0 && (e.effect=="SelfBuff" || e.effect=="PowerBuff" || e.effect=="Rage")){
    var c=Companion(p);var name=e.effect=="Rage"?"LP_Rage":e.hediff??a.def.defName;var src=Items.Hediff(name)==null?null:p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff(name));
    if(c!=null && src!=null && !c.Dead && c.Spawned && p.Spawned && c.Map==p.Map && c.Position.InHorDistOf(p.Position,ShareRange)){Items.Give(c,name,(src as HediffWithComps)?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear??600,src.Severity);Items.Mote(c,"分享法术",ClassMartial2.HuntColor);}
   }
  }

  // ---------- 主动能力 ----------
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;
   switch(e.effect){
    case "RageCharge":return CellCannot(caster,target.Cell,ChargeRange,"冲撞");
    case "Intimidate":case "DeathStrike":case "Pilfer":case "Distract":case "ATSleep":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";
     if(e.effect=="ATSleep")return ClassCasters.SleepCannot(t);
     return null;
    case "TotemAttune":return Choice(caster,"totem")==1?(!ClassMartial2.Raging(caster)?"鹰之滑翔要在狂暴中使用。":CellCannot(caster,target.Cell,10f,"滑翔")):null;
    case "MageHand":{var th=target.Thing;if(th is Building_Door)return null;if(th==null || th is Pawn || th.def.category!=ThingCategory.Item)return "只能选物品或门。";return null;}
    case "ThornTrap":{var map=caster.Map;var c=target.Cell;if(map==null || !c.IsValid || !c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map)!=null || c.GetFirstPawn(map)!=null || c.GetThingList(map).Any(x=>x.def.category==ThingCategory.Building))return "需要一个空着的、能站立的格子。";return null;}
    case "Training":{
     var c=Companion(caster);if(c==null || c.Dead || !c.Spawned || c.Map!=caster.Map || c.Downed)return "游侠伙伴不在身边（先用“选择伙伴”）。";
     var th=target.Thing;if(th==null)return "选择一个敌人、友军或物品。";if(th is Pawn tp){if(tp==c)return "不能对伙伴自己用。";return null;}
     if(th.def.category!=ThingCategory.Item)return "选择一个敌人、友军或物品。";return null;}
   }
   return null;
  }
  static string CellCannot(Pawn p,IntVec3 c,float range,string what){
   var map=p.Map;if(map==null || !c.IsValid || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.GetFirstPawn(map)!=null)return "需要一个空着的、能站立的格子。";
   if(p.Position.DistanceTo(c)>range)return "超出"+what+"距离（"+range.ToString("0")+" 格）。";
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;float s=ClassEffects.Strength(p,e);
   switch(e.effect){
    case "RageCharge":if(CellCannot(p,target.Cell,ChargeRange,"冲撞")==null)RageCharge(p,target.Cell,s);return true;
    case "Intimidate":if(Hostile(p,t))Intimidate(p,t,ClassEffects.Duration(p,e));return true;
    case "SpiritWalker":SpiritWalker(p,s);return true;
    case "TotemAttune":TotemAttune(p,target.Cell,s);return true;
    case "SteadyTarget":Items.Give(p,"LP_RogueSteady",ClassEffects.Duration(p,e));Items.Mote(p,"稳固目标",ClassMartial2.ShadowColor);return true;
    case "DeathStrike":if(Hostile(p,t))DeathStrike(p,t);return true;
    case "Pilfer":if(Hostile(p,t))Pilfer(p,t);return true;
    case "MageHand":MageHand(p,target.Thing);return true;
    case "ATSleep":if(Hostile(p,t))Sleep(p,t,ClassEffects.Duration(p,e),s);return true;
    case "Distract":if(Hostile(p,t)){var m=ClassMartial2.MarkTarget(p,t,"LP_Distracted",ClassEffects.Duration(p,e),1f,ClassCasters.Arcane,"法师之手干扰");}return true;
    case "ThornTrap":PlaceTrap(p,target.Cell);return true;
    case "MultiAttack":MultiAttack(p,s);return true;
    case "Training":Training(p,target.Thing);return true;
   }
   return false;
  }
  public static int RageCharge(Pawn p,IntVec3 dest,float s){
   var map=p.Map;if(map==null)return 0;int n=0;var hit=new HashSet<Pawn>();
   foreach(var c in GenSight.BresenhamCellsBetween(p.Position,dest)){
    if(!c.InBounds(map))continue;
    foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList())if(Hostile(p,t) && hit.Add(t)){t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,ChargeDamage*s,.2f,-1f,p));if(!t.Dead)t.stances?.stunner?.StunFor(ChargeStun,p);n++;}
   }
   FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,ClassMartial2.RageColor);Races124.Blink(p,dest);FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,ClassMartial2.RageColor);
   Items.Mote(p,"狂暴冲撞",ClassMartial2.RageColor);return n;
  }
  public static bool Intimidate(Pawn p,Pawn t,int ticks){
   if(Roll<ResistChance(t)){Items.Mote(t,"抵抗了威吓",ClassMartial2.RageColor);return false;}
   bool fled=Fear(t,ticks,"威吓凝视",true);Items.Mote(t,fled?"被吓跑了":"震慑",ClassMartial2.RageColor);return fled;
  }
  public static List<Pawn> SpiritWalker(Pawn p,float s){
   int t=Mathf.Max(0,Choice(p,"totem"));string kind=t==0?"Bear_Grizzly":t==1?"Cassowary":"Wolf_Timber";int count=t==0?1:t==2 && s>=1.25f?3:2;
   var made=Summon(p,kind,count,3600);Items.Mote(p,t==0?"熊之灵":t==1?"鹰之灵":"狼之灵",ClassMartial2.RageColor);return made;
  }
  public static List<Pawn> Summon(Pawn caster,string kindName,int count,int ticks){
   var made=new List<Pawn>();var kind=DefCache<PawnKindDef>.Get(kindName)??DefCache<PawnKindDef>.Get("Wolf_Timber");var map=caster.Map;
   if(kind==null || map==null)return made;
   for(int i=0;i<count;i++){
    var w=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,caster.Faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:3,fixedChronologicalAge:3));
    GenSpawn.Spawn(w,CellFinder.RandomClosewalkCellNear(caster.Position,map,2),map);
    var h=Items.Give(w,"LP_Conjured");var comp=(h as HediffWithComps)?.TryGetComp<HediffComp_Conjured>();if(comp!=null)comp.ticksLeft=ticks;
    if(w.training!=null && w.Faction==Faction.OfPlayer){
     foreach(var td in new[]{TrainableDefOf.Tameness,TrainableDefOf.Obedience,TrainableDefOf.Release})if(td!=null && w.training.CanAssignToTrain(td).Accepted)w.training.Train(td,null,true);
     if(w.playerSettings!=null)w.playerSettings.Master=caster;
    }
    FleckMaker.ThrowDustPuffThick(w.DrawPos,map,2f,ClassCasters.Nature);HediffComp_Conjured.Engage(w);made.Add(w);
   }
   return made;
  }
  public static int TotemAttune(Pawn p,IntVec3 cell,float s){
   int t=Mathf.Max(0,Choice(p,"totem"));var map=p.Map;if(map==null)return 0;int n=0;
   if(t==0){foreach(var o in Races124.PawnsNear(map,p.Position,8f).ToList())if(Hostile(p,o) && !o.Downed && o.mindState!=null){o.mindState.enemyTarget=p;Items.Give(o,"LP_Taunted",600);n++;}Items.Mote(p,"熊之嘲讽",ClassMartial2.RageColor);}
   else if(t==1){if(CellCannot(p,cell,10f,"滑翔")!=null)return 0;FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,Color.white);Races124.Blink(p,cell);Items.Mote(p,"鹰之滑翔",ClassMartial2.RageColor);n=1;}
   else{Items.Give(p,"LP_WolfPounce",1200);Items.Mote(p,"狼之扑倒",ClassMartial2.RageColor);n=1;}
   return n;
  }
  public static bool DeathStrike(Pawn p,Pawn t){
   Items.Give(p,"LP_DeathStrikeFx",60,DeathStrikeFactor);
   try{ClassMelee.Strike(p,t);}finally{Items.Remove(p,"LP_DeathStrikeFx");}
   if(t.Dead || t.Downed){Items.Mote(t,"死亡之击",ClassMartial2.ShadowColor);return true;}
   if(Roll<ResistChance(t)){Items.Mote(t,"抵抗了死亡之击",ClassMartial2.ShadowColor);return false;}
   HealthUtility.DamageUntilDowned(t,false);Items.Mote(t,"死亡之击",ClassMartial2.ShadowColor);return true;
  }
  public static Thing Pilfer(Pawn p,Pawn t){
   var inv=t.inventory?.innerContainer;if(inv==null || inv.Count==0){Items.Mote(t,"什么也没摸到",ClassMartial2.ShadowColor);return null;}
   var pick=inv.FirstOrDefault(x=>x.def.IsMedicine)??inv.OrderByDescending(x=>x.MarketValue*x.stackCount).First();
   var dest=p.inventory?.innerContainer;if(dest==null)return null;
   int n=pick.def.IsMedicine?pick.stackCount:pick.stackCount;
   if(inv.TryTransferToContainer(pick,dest,n)<=0){Items.Mote(t,"没拿走",ClassMartial2.ShadowColor);return null;}
   Items.Mote(p,"顺手牵羊："+pick.LabelNoCount,ClassMartial2.ShadowColor);return pick;
  }
  public static bool MageHand(Pawn p,Thing th){
   var map=p.Map;if(map==null || th==null || !th.Spawned)return false;
   if(th is Building_Door door){door.StartManualOpenBy(p);Items.Mote(door,"法师之手",ClassCasters.Arcane);return true;}
   var cell=th.Position;th.DeSpawn();
   if(!GenPlace.TryPlaceThing(th,p.Position,map,ThingPlaceMode.Near)){GenPlace.TryPlaceThing(th,cell,map,ThingPlaceMode.Near);return false;}
   Items.Mote(p,"法师之手",ClassCasters.Arcane);return true;
  }
  // 睡眠术（诡术师）：魔法伏击时（隐形中施放）目标抵抗成功率 −20%。
  public static bool Ambushing(Pawn p)=>S(p,SAmbush) && ClassMartial2.Hidden(p);
  public static bool Sleep(Pawn p,Pawn t,int ticks,float s){
   if(ClassCasters.SleepCannot(t)!=null)return false;
   float chance=HeroCombat.MindResist(t)?1f:DNDMind.Resistance(t);if(Ambushing(p))chance-=AmbushResist;
   if(chance>0f && Roll<chance){Items.Mote(t,"抵抗了睡眠术",ClassCasters.Arcane);return false;}
   Items.Give(t,"LP_MagicSleep",ticks,s);Items.Mote(t,Ambushing(p)?"魔法伏击·睡眠":"睡眠",ClassCasters.Arcane);return true;
  }
  public static Building_ThornTrap PlaceTrap(Pawn p,IntVec3 c){
   var map=p.Map;var def=DefCache<ThingDef>.Get("LP_ThornTrapThing");if(map==null || def==null)return null;
   var mine=map.listerThings.ThingsOfDef(def).OfType<Building_ThornTrap>().Where(x=>x.owner==p).OrderBy(x=>x.placed).ToList();
   while(mine.Count>=TrapMax){mine[0].Destroy();mine.RemoveAt(0);}
   var trap=(Building_ThornTrap)ThingMaker.MakeThing(def);trap.owner=p;trap.placed=Now;trap.SetFaction(p.Faction);
   GenSpawn.Spawn(trap,c,map);FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,1f,new Color(.4f,.6f,.3f));Items.Mote(p,"荆棘陷阱",ClassMartial2.HuntColor);
   return trap;
  }
  public static int MultiAttack(Pawn p,float s){
   var map=p.Map;if(map==null)return 0;int n=0;var verb=Martial.RangedVerb(p);
   if(verb?.Projectile!=null){
    float r=Mathf.Min(12f,verb.EffectiveRange);
    foreach(var t in Races124.PawnsNear(map,p.Position,r).Where(x=>Hostile(p,x) && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).Take(8).ToList())if(Martial.Fire(p,t,new ShotMod{damage=1f,caster=p},true)!=null)n++;
    Items.Mote(p,"多重攻击·齐射",ClassMartial2.HuntColor);
   }else{
    n=Whirlwind.Sweep(p,s,Races124.PawnsNear(map,p.Position,1.5f).Where(x=>Hostile(p,x) && x.Position.AdjacentTo8WayOrInside(p.Position)).ToList());
    Items.Mote(p,"多重攻击·旋风",ClassMartial2.HuntColor);
   }
   return n;
  }
  public static string Training(Pawn p,Thing th){
   var c=Companion(p);if(c==null || c.Dead || !c.Spawned || c.Map!=p.Map || th==null)return null;var map=p.Map;
   if(th is Pawn t){
    if(Hostile(p,t)){
     var cell=Charge.LandingCell(c,t);if(cell.IsValid)Races124.Blink(c,cell);
     if(t.BodySize<=2f)t.stances?.stunner?.StunFor(PounceStun,c);
     if(c.Position.AdjacentTo8WayOrInside(t.Position))ClassMelee.Strike(c,t);
     Items.Mote(t,"伙伴扑倒",ClassMartial2.HuntColor);return "knockdown";
    }
    var m=ClassMartial2.MarkTarget(c,t,"LP_CompanionGuard",1800,1f,ClassMartial2.HuntColor,"伙伴守护");
    var near=CellFinder.RandomClosewalkCellNear(t.Position,map,2);if(near.IsValid && near.GetFirstPawn(map)==null)Races124.Blink(c,near);
    return "guard";
   }
   if(th.def.category==ThingCategory.Item && th.Spawned){
    th.DeSpawn();GenPlace.TryPlaceThing(th,p.Position,map,ThingPlaceMode.Near);
    var near=CellFinder.RandomClosewalkCellNear(p.Position,map,2);if(near.IsValid && near.GetFirstPawn(map)==null)Races124.Blink(c,near);
    Items.Mote(p,"伙伴叼回",ClassMartial2.HuntColor);return "fetch";
   }
   return null;
  }

  // ---------- 提示信 ----------
  public static List<Pawn> NeedingPick()=>Find.Maps.SelectMany(m=>m.mapPawns.FreeColonists).Distinct().Where(p=>{var h=Adventurer.Get(p);return h?.classes!=null && NeedPick(h).Any();}).ToList();
  public static void PromptLetter(){
   if(!Adventurer.Enabled)return;var list=NeedingPick();if(list.Count==0)return;
   foreach(var p in list){var d=Data(p);foreach(var c in NeedPick(Adventurer.Get(p)))if(!d.reminded.Contains(c))d.reminded.Add(c);}
   Find.LetterStack.ReceiveLetter("子职业","DND边缘大陆：野蛮人、游侠、游荡者现在有子职业了（职业 3 级选一个，能力在 3 / 7 / 11 / 15 级获得），每个职业另有 2 个通用能力（7、11 级自动获得）。\n\n"
    +string.Join("\n",list.Select(p=>"· "+p.LabelShort+"："+string.Join("、",NeedPick(Adventurer.Get(p)).Select(c=>c.label+" "+ClassSystem.Level(p,c)+" 级"))))
    +"\n\n选中殖民者点“选择子职业”按钮，或在“冒险者”分页的“职业”页里选。选定后不能更改。",LetterDefOf.NeutralEvent,new LookTargets(list.Where(p=>p.Spawned)));
  }

  // ---------- 职业页的「子职业」行 ----------
  public static float DrawRow(float y,float w,Pawn p,Hediff_Adventurer h,LP_ClassDef c,bool own){
   var gen=General(c);if(gen==null && !Options(c).Any())return y;
   var pick=Picked(h,c);int lv=ClassSystem.Level(h,c);
   var row=new Rect(0f,y,w,48f);Widgets.DrawBoxSolid(row,new Color(.25f,.22f,.15f,.25f));
   var em=pick?.Emblem;if(em!=null)GUI.DrawTexture(new Rect(4f,y+4f,40f,40f),em);
   string title=pick==null?(lv>=PickLevelFor(c)?"子职业：未选择":"子职业："+c.label+" "+PickLevelFor(c)+" 级可选"):"子职业："+pick.label+string.Concat(pick.choices.Select(ch=>{var l=ChoiceLabel(h,ch);return l==null?"":"·"+l;}));
   GUI.color=pick!=null?pick.color:Color.gray;Widgets.Label(new Rect(48f,y+2f,260f,22f),title.Truncate(260f));GUI.color=Color.white;
   if(pick!=null)TooltipHandler.TipRegion(new Rect(0f,y,300f,48f),pick.LabelCap+"\n\n"+pick.description);
   // 图标：通用 + 子职业能力
   var entries=new List<(LP_SubclassDef s,ClassAbilityEntry e)>();
   if(gen!=null)foreach(var e in gen.abilities)entries.Add((gen,e));
   if(pick!=null)foreach(var e in pick.abilities)entries.Add((pick,e));
   float x=48f;float iy=y+24f;
   foreach(var (s,e) in entries.OrderBy(t=>t.e.level)){
    var ir=new Rect(x,iy,22f,22f);x+=25f;if(x>w-130f)break;
    bool have=p.abilities?.GetAbility(e.ability)!=null;
    GUI.color=have?Color.white:new Color(.4f,.4f,.4f);GUI.DrawTexture(ir,e.ability.uiIcon??BaseContent.BadTex);GUI.color=Color.white;
    Widgets.DrawHighlightIfMouseover(ir);
    if(Mouse.IsOver(ir))TooltipHandler.TipRegion(ir,e.ability.LabelCap+"（"+(s.general?c.label+"通用":s.label)+"，"+c.label+" "+e.level+" 级）\n\n"+e.ability.description+"\n\n"+(have?"已掌握":"未掌握"));
   }
   int pend=(pick==null && lv>=PickLevelFor(c)?1:0)+PendingChoices(h).Count(t=>t.s.parent==c);
   if(own && pend>0 && Widgets.ButtonText(new Rect(w-124f,y+10f,120f,28f),pick==null?"选择子职业":"选择分支"))Find.WindowStack.Add(new Dialog_ChooseSubclass(p));
   if(Has(h,Trickster) && c==ClassMartial2.Rogue){Text.Font=GameFont.Tiny;Widgets.Label(new Rect(312f,y+4f,140f,18f),"法力 "+Mana(h).ToString("0.#")+" / "+ManaMax(h));Text.Font=GameFont.Small;}
   if(pick!=null && pick.defName==BeastMaster){Text.Font=GameFont.Tiny;var cp=Data(h).companion;Widgets.Label(new Rect(312f,y+4f,200f,18f),"伙伴："+(cp==null?"未选择":cp.LabelShort));Text.Font=GameFont.Small;}
   return y+52f;
  }
 }

 // 荆棘陷阱：敌人踩到被缠绕 5 秒（藤蔓缠绕 + 击晕），踩一次就消失。
 public class Building_ThornTrap : Building_Trap {
  public Pawn owner;public int placed;
  public const int Ticks=300;
  protected override void SpringSub(Pawn p){
   if(p==null || p.Dead)return;
   Items.Give(p,"LP_Entangled",Ticks,p.BodySize>=2f?2f:1f);p.stances?.stunner?.StunFor(Ticks,owner);
   Items.Mote(p,"荆棘缠绕",ClassMartial2.HuntColor);
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref owner,"lpTrapOwner");Scribe_Values.Look(ref placed,"lpTrapPlaced");}
  public override string GetInspectString(){var s=base.GetInspectString();return (s.NullOrEmpty()?"":s+"\n")+"布置者："+(owner?.LabelShort??"无");}
 }

 // 选子职业 / 分支的窗口（不用浮动菜单）。
 public class Dialog_ChooseSubclass : Window {
  readonly Pawn pawn;string armed;Vector2 scroll;
  public override Vector2 InitialSize=>new Vector2(680f,640f);
  public Dialog_ChooseSubclass(Pawn p){pawn=p;doCloseX=true;doCloseButton=true;closeOnClickedOutside=false;absorbInputAroundWindow=true;forcePause=true;}
  public override void DoWindowContents(Rect r){
   var h=Adventurer.Get(pawn);if(h?.classes==null){Close();return;}
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(r.x,r.y,r.width,32f),pawn.LabelShort+" 的子职业");Text.Font=GameFont.Small;
   float y=r.y+36f;
   var need=ClassSubclass.NeedPick(h).ToList();var pend=ClassSubclass.PendingChoices(h).ToList();
   if(need.Count==0 && pend.Count==0){Widgets.Label(new Rect(r.x,y,r.width,48f),"现在没有要选的子职业或分支（职业 "+ClassSubclass.PickLevel+" 级可以选子职业；有些分支到更高等级才能选）。");return;}
   float viewH=need.Sum(c=>30f+ClassSubclass.Options(c).Count()*112f)+pend.Sum(t=>30f+t.ch.options.Count*52f)+10f;
   var outer=new Rect(r.x,y,r.width,r.yMax-y-48f);var view=new Rect(0f,0f,outer.width-16f,viewH);
   Widgets.BeginScrollView(outer,ref scroll,view);
   try{
    float yy=0f;
    foreach(var c in need){
     GUI.color=c.color;Widgets.Label(new Rect(0f,yy,view.width,26f),c.LabelCap+" "+ClassSystem.Level(h,c)+" 级：选一个子职业（选定后不能更改）");GUI.color=Color.white;yy+=30f;
     foreach(var s in ClassSubclass.Options(c)){
      var row=new Rect(0f,yy,view.width,108f);Widgets.DrawLightHighlight(row);
      var em=s.Emblem;if(em!=null)GUI.DrawTexture(new Rect(row.x+4f,row.y+4f,64f,64f),em);
      GUI.color=s.color;Widgets.Label(new Rect(row.x+74f,row.y+2f,row.width-190f,24f),s.LabelCap);GUI.color=Color.white;
      Text.Font=GameFont.Tiny;Widgets.Label(new Rect(row.x+74f,row.y+24f,row.width-190f,82f),s.description+"\n"+string.Join("　",s.abilities.OrderBy(e=>e.level).Select(e=>e.level+" "+e.ability.label)));Text.Font=GameFont.Small;
      TooltipHandler.TipRegion(new Rect(row.x,row.y,row.width-110f,row.height),s.LabelCap+"\n\n"+string.Join("\n\n",s.abilities.OrderBy(e=>e.level).Select(e=>e.level+" 级 "+e.ability.LabelCap+"：\n"+e.ability.description)));
      string why=ClassSubclass.CanPick(h,s);bool ok=why==null;string key="sub:"+s.defName;var b=new Rect(row.xMax-104f,row.y+36f,96f,32f);
      if(Widgets.ButtonText(b,armed==key?"确定？":"选择",active:ok) && ok){if(armed==key){armed=null;ClassSubclass.Pick(h,s,true);}else armed=key;}
      TooltipHandler.TipRegion(b,ok?(armed==key?"再点一次确认":"点两次确认："+s.label):why);
      yy+=112f;
     }
    }
    foreach(var (s,ch) in pend){
     GUI.color=s.color;Widgets.Label(new Rect(0f,yy,view.width,26f),s.LabelCap+"·"+ch.label+"：选一个（选定后不能更改）");GUI.color=Color.white;yy+=30f;
     for(int i=0;i<ch.options.Count;i++){
      var row=new Rect(0f,yy,view.width,48f);Widgets.DrawLightHighlight(row);
      Widgets.Label(new Rect(row.x+8f,row.y+2f,160f,24f),ch.options[i]);
      string tip=i<ch.tips.Count?ch.tips[i]:"";Text.Font=GameFont.Tiny;Widgets.Label(new Rect(row.x+8f,row.y+22f,row.width-130f,26f),tip.Truncate(row.width-130f));Text.Font=GameFont.Small;
      if(!tip.NullOrEmpty())TooltipHandler.TipRegion(new Rect(row.x,row.y,row.width-110f,row.height),ch.options[i]+"\n\n"+tip);
      string key="ch:"+ch.key+":"+i;var b=new Rect(row.xMax-104f,row.y+8f,96f,30f);
      if(Widgets.ButtonText(b,armed==key?"确定？":"选择")){if(armed==key){armed=null;ClassSubclass.Choose(h,ch.key,i,true);}else armed=key;}
      yy+=52f;
     }
    }
   }finally{Widgets.EndScrollView();}
  }
 }
 // 兽王：选伙伴（同地图、自己阵营的动物）。
 public class Dialog_ChooseCompanion : Window {
  readonly Pawn pawn;Vector2 scroll;
  public override Vector2 InitialSize=>new Vector2(440f,520f);
  public Dialog_ChooseCompanion(Pawn p){pawn=p;doCloseX=true;doCloseButton=true;absorbInputAroundWindow=true;forcePause=true;}
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(r.x,r.y,r.width,32f),pawn.LabelShort+" 的游侠伙伴");Text.Font=GameFont.Small;
   float y=r.y+36f;var cur=ClassSubclass.Companion(pawn);
   Widgets.Label(new Rect(r.x,y,r.width,24f),"现在："+(cur==null?"没有":cur.LabelShort)+"（换一只会解除原来的）");y+=28f;
   var list=pawn.Map==null?new List<Pawn>():pawn.Map.mapPawns.SpawnedColonyAnimals.Where(a=>!a.Dead && !Martial.Conjured(a)).OrderByDescending(a=>a.playerSettings?.Master==pawn).ThenBy(a=>a.LabelShort).ToList();
   if(list.Count==0){Widgets.Label(new Rect(r.x,y,r.width,48f),"这张地图上没有殖民地的动物。");return;}
   var outer=new Rect(r.x,y,r.width,r.yMax-y-48f);var view=new Rect(0f,0f,outer.width-16f,list.Count*34f);
   Widgets.BeginScrollView(outer,ref scroll,view);
   try{float yy=0f;foreach(var a in list){var b=new Rect(0f,yy,view.width,30f);
    if(Widgets.ButtonText(b,a.LabelShort+"（"+a.KindLabel+(BeastLevels.Get(a)!=null?"，野性等级 "+BeastLevels.Get(a).level:"")+(a==cur?"，当前":"")+"）",active:a!=cur) && a!=cur){ClassSubclass.Bond(pawn,a);Messages.Message(pawn.LabelShort+"和"+a.LabelShort+"结成了游侠伙伴。",pawn,MessageTypeDefOf.PositiveEvent,false);Close();}
    yy+=34f;}}
   finally{Widgets.EndScrollView();}
  }
 }

 // 读旧存档：第一刻给“职业 ≥ 3 但没选子职业”的殖民者发一封信；新游戏不发。
 public class GameComponent_Subclasses : GameComponent {
  public bool notified;bool pending;
  public GameComponent_Subclasses(Game game){}
  public override void StartedNewGame(){base.StartedNewGame();notified=true;}
  public override void LoadedGame(){base.LoadedGame();if(!notified)pending=true;}
  public override void GameComponentTick(){
   if(pending){pending=false;notified=true;if(!Adventurer.TestSuppressed)ClassSubclass.PromptLetter();}
   if(Find.TickManager.TicksGame%Martial.HourTicks==1)ClassSubclass.Cleanup();
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref notified,"lpSubclassNotified");}
 }

 // ==================== Harmony ====================
 // 按钮：选择子职业 / 分支（有要选的）、选择伙伴（兽王）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class SubclassGizmos {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   foreach(var g in __result)yield return g;
   var p=__instance;if(p.Dead || p.Faction!=Faction.OfPlayer || !Adventurer.Enabled)yield break;
   var h=Adventurer.Get(p);if(h?.classes==null || h.classes.levels.Count==0)yield break;
   int n=ClassSubclass.Pending(h);
   if(n>0){var c=ClassSubclass.NeedPick(h).FirstOrDefault();yield return new Command_Action{
    defaultLabel="选择子职业（"+n+"）",defaultDesc="职业 "+ClassSubclass.PickLevel+" 级可以选择子职业（野蛮人、游侠、游荡者各 3 个），子职业能力在职业 3 / 7 / 11 / 15 级获得；有的子职业还要选一个分支。选定后不能更改。",
    icon=(c!=null?ClassSubclass.Options(c).FirstOrDefault()?.Emblem:null)??ClassMartial2.Barbarian?.Background??BaseContent.BadTex,
    action=()=>Find.WindowStack.Add(new Dialog_ChooseSubclass(p))};}
   if(ClassSubclass.S(p,ClassSubclass.SCompanion) && (ClassSubclass.Companion(p)==null || p.Drafted))yield return new Command_Action{
    defaultLabel=ClassSubclass.Companion(p)==null?"选择伙伴":"更换伙伴",defaultDesc="兽王：选同一地图上一只殖民地的动物做游侠伙伴（设为主人）。伙伴的野性等级跟着你的冒险者等级成长，对你的宿敌伤害 +20%。",
    icon=ContentFinder<Texture2D>.Get("LP/Classes/Subclass/RangerCompanion",false)??BaseContent.BadTex,
    action=()=>Find.WindowStack.Add(new Dialog_ChooseCompanion(p))};
  }
 }
 // 无脑狂怒（狂暴中免疫恐惧、魅惑、精神崩溃）、钢铁意志（免疫恐惧）。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class SubclassMind {
  static bool Prefix(Pawn ___pawn,MentalStateDef stateDef,ref bool __result){
   if(ClassSubclass.Users.Count==0 || stateDef==null)return true;int f=ClassSubclass.Flags(___pawn);if(f==0)return true;
   bool block=(f & ClassSubclass.SMindless)!=0 && ClassMartial2.Raging(___pawn) || (f & ClassSubclass.SIronMind)!=0 && ClassSubclass.FearState(stateDef);
   if(!block)return true;__result=false;return false;
  }
 }
 // 野性直觉：袭击到来时先动。
 [HarmonyPatch(typeof(IncidentWorker_Raid),"TryExecuteWorker")]
 public static class SubclassRaidInstinct {
  static void Postfix(IncidentParms parms,bool __result){
   if(!__result || ClassSubclass.Users.Count==0 || !(parms?.target is Map map))return;
   foreach(var p in map.mapPawns.FreeColonistsSpawned)if(ClassSubclass.S(p,ClassSubclass.SInstinct)){Items.Give(p,"LP_FeralInstinctBuff",900);Items.Mote(p,"野性直觉",ClassMartial2.RageColor);}
  }
 }
 // 兽之面相·鹰：远程武器射程 +3。
 [HarmonyPatch(typeof(VerbProperties),nameof(VerbProperties.AdjustedRange))]
 public static class SubclassEagleRange {
  static void Postfix(VerbProperties __instance,Thing attacker,ref float __result){
   if(ClassSubclass.Eagles.Count==0 || __result<=1.5f || __instance.IsMeleeAttack || !(attacker is Pawn p) || !ClassSubclass.Eagles.Contains(p.thingIDNumber))return;
   __result+=ClassSubclass.EagleRange;
  }
 }
 // 非凡训练：伙伴的攻击算魔法武器。
 [HarmonyPatch(typeof(Outsiders),nameof(Outsiders.NonMagic))]
 public static class SubclassCompanionMagic {
  static void Postfix(DamageInfo d,ref bool __result){
   if(!__result || ClassSubclass.Companions.Count==0 || !(d.Instigator is Pawn a) || !ClassSubclass.Companions.Contains(a))return;
   var m=a.playerSettings?.Master;if(m!=null && ClassSubclass.S(m,ClassSubclass.STraining))__result=false;
  }
 }
 // 偷取法术：敌人的原版能力打在诡术师身上时先判定；诡术师用掉偷来的法术后移除。
 [HarmonyPatch(typeof(Ability),nameof(Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class SubclassSpellThief {
  static bool Prefix(Ability __instance,LocalTargetInfo target){
   if(ClassSubclass.Users.Count==0 || __instance?.pawn==null)return true;
   return !ClassSubclass.TrySteal(__instance,target);
  }
  static void Postfix(Ability __instance,bool __result){
   if(!__result || ClassSubclass.Users.Count==0 || __instance?.pawn==null)return;
   var p=__instance.pawn;var d=ClassSubclass.Data(p);
   if(d?.stolen!=null && d.stolen==__instance.def)Fiends.Defer(()=>ClassSubclass.DropStolen(p,d));
  }
 }
 // 寻宝直觉：迷锁地牢的宝藏 / 守卫奖励多一件。
 [HarmonyPatch(typeof(Mythal),nameof(Mythal.RollLoot))]
 public static class SubclassTreasure {
  public static int Extra;
  static void Postfix(List<Thing> __result){
   if(ClassSubclass.Users.Count==0 || __result==null || !ClassSubclass.Users.Values.Any(f=>(f & ClassSubclass.STreasure)!=0))return;
   var def=DefCache<ThingDef>.Get("LP_PotionHealing");if(def==null)return;
   __result.Add(ThingMaker.MakeThing(def));Extra++;
  }
 }
}
