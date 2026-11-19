using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 2026-11-19 心灵术士（Peter，设计文档 https://claude.ai/artifact/4YimhXHHjfazx191WSatmP；照 UA 2025《Psion Update》）：智识职业，法术用法力（施法职业法术表 LP_CSL_Psion），
 // 职业能力和灵能修行用「灵能点」（心灵术士 1 / 5 / 9 / 13 / 17 级 4 / 6 / 8 / 10 / 12 点，睡醒回满，连续 1 小时冥想 / 娱乐 / 睡觉回 1 点）。
 // 灵能点不改存档格式：存在子职业数据的 ticks 里（psUsed = 已用点数）。灵能点用完时，有心灵链接的人可以改加神经热 12 代付。
 // 灵能值（代替 UA 的能量骰）：灵能档 一 2～8、二 3～10、三 4～12、四 5～14（1 / 5 / 11 / 17 级），每次在范围内随机再乘灵能强度；「成功率 +X%」的修行按档 15 / 20 / 25 / 30%。
 // 灵能修行是子职业里的 6 个分支（psd1～psd6，3 / 3 / 5 / 10 / 13 / 17 级，同一项不能修两次，ChoiceBlock）；扩展感知、灵能守护、锐化心智选了就给能力按钮。
 public static class Psion {
  static readonly DefRef<LP_ClassDef> cls=new DefRef<LP_ClassDef>("LP_Class_Psion");
  public static LP_ClassDef Class=>cls.Def;
  public const string Metamorph="LP_Sub_Metamorph",Psykinetic="LP_Sub_Psykinetic",Telepath="LP_Sub_Telepath",Warper="LP_Sub_PsiWarper";
  public const string UsedKey="psUsed",RestKey="psRest",SleepKey="psSleep",FreeTeleKey="psTele",ReserveKey="psReserve",ObserveKey="psObserve",BacklashKey="psBacklash",DistractKey="psDistract",DisplaceKey="psDisplace",LifeKey="psLife",BlinkKey="psBlink";
  public const string BeaconX="psBX",BeaconZ="psBZ",BeaconMap="psBM";
  public const float MeditateRegen=.05f,HeatPerPoint=12f,SurgeHeat=6f,SurgeRest=.03f,EnkindleHeat=10f,SurgeFloor=.6f;
  public const int RestTicks=2500;
  public static readonly Color Psi=new Color(.55f,.75f,.95f);
  public static readonly string[] Disciplines={"生物反馈","预知增幅","毁灭思绪","魔鬼之舌","扩展感知","心智暗示","无误瞄准","观察入微","灵能反噬","灵能守护","锐化心智","念动精修"};
  public const int DBio=0,DPrecog=1,DDestructive=2,DTongue=3,DAwareness=4,DId=5,DAim=6,DObserve=7,DBacklash=8,DGuards=9,DSharpened=10,DTK=11;
  public static readonly string[] DisciplineKeys={"psd1","psd2","psd3","psd4","psd5","psd6"};

  // ---------- 等级、灵能点、灵能值 ----------
  public static int Level(Pawn p)=>ClassSystem.Level(p,Class);
  public static int Level(Hediff_Adventurer h)=>ClassSystem.Level(h,Class);
  public static int BasePoints(int lv)=>lv<=0?0:lv>=17?12:lv>=13?10:lv>=9?8:lv>=5?6:4;
  public static int MaxPoints(Hediff_Adventurer h){int lv=Level(h);if(lv<=0)return 0;int n=BasePoints(lv);var p=h.pawn;if(p!=null){if(Feats.Has(p,DefDatabase<LP_FeatDef>.GetNamedSilentFail("LP_Feat_Class_PsionTempered")))n+=2;if(ClassLegendary.HasLeg(p,"LP_LegTranscendentMind"))n+=4;}return n;}
  public static int Tier(int lv)=>lv>=17?4:lv>=11?3:lv>=5?2:1;
  public static int Tier(Pawn p)=>Tier(Level(p));
  public static float Low(int tier)=>tier+1;
  public static float High(int tier)=>6+2*tier;
  public static float Pct(int tier)=>.1f+.05f*tier; // 15 / 20 / 25 / 30%
  public static float Pct(Pawn p)=>Pct(Tier(p));
  public static string RangeText(int tier)=>Low(tier).ToString("0")+"～"+High(tier).ToString("0");
  public static SubclassData Data(Hediff_Adventurer h)=>ClassSubclass.Data(h);
  static int Get(SubclassData d,string k){if(d==null)return 0;d.ticks.TryGetValue(k,out int v);return v;}
  public static int Used(Hediff_Adventurer h)=>Get(Data(h),UsedKey);
  public static int Points(Hediff_Adventurer h)=>Mathf.Max(0,MaxPoints(h)-Used(h));
  public static int Points(Pawn p)=>Points(Adventurer.Get(p));
  static void SetUsed(Hediff_Adventurer h,int v){var d=Data(h);if(d==null)return;v=Mathf.Clamp(v,0,MaxPoints(h));if(v==0)d.ticks.Remove(UsedKey);else d.ticks[UsedKey]=v;}
  public static void Refill(Hediff_Adventurer h)=>SetUsed(h,0);
  public static void Restore(Hediff_Adventurer h,int n)=>SetUsed(h,Used(h)-n);
  public static float StatPower(Pawn p){var s=DefCache<StatDef>.Get("LP_PsionicPower");return s==null?1f:p.GetStatValue(s);}
  // 灵能值：灵能档的范围里随机（灵能涌动 / 超凡心智：下限提到范围的 60%），乘灵能强度；燃魂期间另加 1～2 份（每份神经热 10）。
  public static float RollOverride=-1f;
  public static float Roll(Pawn p,bool power=true){
   int t=Tier(p);float lo=Low(t),hi=High(t);
   bool leg=ClassLegendary.HasLeg(p,"LP_LegTranscendentMind"),surge=Items.Has(p,"LP_PS_SurgeOn");
   if(leg || surge)lo+=(hi-lo)*SurgeFloor;
   float r=RollOverride>=0f?Mathf.Lerp(lo,hi,RollOverride):Rand.Range(lo,hi);
   if(surge && !leg)Strain(p,SurgeHeat,SurgeRest);
   if(Items.Has(p,"LP_PS_Enkindled"))for(int i=0;i<2;i++){if(!Strain(p,EnkindleHeat,SurgeRest*2f,true))break;r+=RollOverride>=0f?Mathf.Lerp(Low(t),hi,RollOverride):Rand.Range(Low(t),hi);}
   return power?r*StatPower(p):r;
  }
  // 神经热（没有心灵链接时扣休息值）。strict：神经热放不下就不做。
  public static bool Strain(Pawn p,float heat,float rest,bool strict=false){
   var e=p?.psychicEntropy;
   if(e!=null && p.HasPsylink){if(strict && e.EntropyValue+heat>e.MaxEntropy)return false;e.TryAddEntropy(heat,p,true,true);return true;}
   var need=p?.needs?.rest;if(need==null)return !strict;if(strict && need.CurLevel<rest+.05f)return false;need.CurLevel=Mathf.Max(0f,need.CurLevel-rest);return true;
  }
  static bool Catastrophe(Pawn p)=>Items.Has(p,"LP_LegPsychicCatastropheBuff");
  // 能不能花 n 点（点数不够、有心灵链接时看神经热放不放得下）。
  public static bool CanSpend(Pawn p,int n){
   if(n<=0)return true;var h=Adventurer.Get(p);if(h==null || Level(h)<=0)return false;
   if(Catastrophe(p))return true;if(Points(h)>=n)return true;
   var e=p.psychicEntropy;int lack=n-Points(h);return e!=null && p.HasPsylink && e.EntropyValue+lack*HeatPerPoint<=e.MaxEntropy;
  }
  public static bool Spend(Pawn p,int n){
   if(n<=0)return true;if(!CanSpend(p,n))return false;var h=Adventurer.Get(p);
   if(Catastrophe(p)){p.psychicEntropy?.TryAddEntropy(4f*n,p,true,true);return true;}
   int have=Points(h),use=Mathf.Min(have,n);SetUsed(h,Used(h)+use);
   if(n>use)p.psychicEntropy.TryAddEntropy((n-use)*HeatPerPoint,p,true,true);
   return true;
  }
  public static string PointsText(Pawn p){var h=Adventurer.Get(p);if(h==null || Level(h)<=0)return null;return "灵能点 ".Loc()+Points(h)+" / "+MaxPoints(h);}
  public static string PointsTip(Pawn p){
   var h=Adventurer.Get(p);if(h==null)return "";int t=Tier(Level(h));
   return "灵能点：".Loc()+Points(h)+" / "+MaxPoints(h)+"（心灵术士 1 / 5 / 9 / 13 / 17 级 4 / 6 / 8 / 10 / 12 点；睡醒回满，连续 1 小时冥想、娱乐或睡觉回 1 点）。\n灵能档 ".Loc()+t+"：灵能值 ".Loc()+RangeText(t)+" × 灵能强度 ".Loc()+StatPower(p).ToStringPercent()+"；修行的成功率加成 +".Loc()+Pct(t).ToStringPercent()+"。\n灵能点用完时，有心灵链接的人每 1 点改加神经热 ".Loc()+HeatPerPoint.ToString("0")+"（放不下就不能用）。".Loc();
  }

  // ---------- 子职业、修行 ----------
  public static bool Has(Hediff_Adventurer h,string sub)=>ClassSubclass.Has(h,sub);
  public static bool Has(Pawn p,string sub)=>ClassSubclass.Has(p,sub);
  public static bool Discipline(Hediff_Adventurer h,int option){if(h==null || Level(h)<=0)return false;foreach(var k in DisciplineKeys)if(ClassSubclass.Choice(h,k)==option)return true;return false;}
  public static bool Discipline(Pawn p,int option)=>Discipline(Adventurer.Get(p),option);
  public static string ChoiceBlock(Hediff_Adventurer h,string key,int option){
   if(key==null || !key.StartsWith("psd"))return null;
   foreach(var k in DisciplineKeys)if(k!=key && ClassSubclass.Choice(h,k)==option)return "已经修过「".Loc()+(option>=0 && option<Disciplines.Length?Disciplines[option]:"?")+"」".Loc();
   return null;
  }
  static readonly Dictionary<int,string> DisciplineAbility=new Dictionary<int,string>{{DAwareness,"LP_PS_Awareness"},{DGuards,"LP_PS_Guards"},{DSharpened,"LP_PS_Sharpened"}};
  public static bool Knows(Pawn p,string ability)=>ClassSystem.Knows(p,ability);

  // 子职业常备法术（照 UA 的 Subclass Spells）：法术表里只给这个子职业的人（其他心灵术士不能备）。
  // 子职业常备法术（照 UA 的 Subclass Spells）。only=true：只给这个子职业的人（其他心灵术士不能备）；false：本来就在心灵术士法术表里，子职业只是让它常备（不占名额）。
  static readonly (string def,string sub,int level,bool only)[] SubSpellRows={
   ("LP_CureWounds",Metamorph,3,true),("LP_LesserRestoration",Metamorph,3,true),("LP_PS_LifeSiphon",Metamorph,3,false),("LP_WS_EnhanceAbility",Metamorph,3,false),
   ("LP_Haste",Metamorph,5,true),("LP_CS_MassHealingWord",Metamorph,5,true),("LP_WS_Polymorph",Metamorph,7,false),("LP_WS_Stoneskin",Metamorph,7,true),("LP_MassHeal",Metamorph,9,true),("LP_CS_GreaterRestoration",Metamorph,9,true),
   ("LP_ShieldSpell",Psykinetic,3,false),("LP_WS_Thunderwave",Psykinetic,3,false),("LP_WS_Shatter",Psykinetic,3,false),("LP_CS_Jump",Psykinetic,3,false),("LP_WS_Slow",Psykinetic,5,true),("LP_PS_TelekineticCrush",Psykinetic,5,false),
   ("LP_CS_StoneShape",Psykinetic,7,true),("LP_CS_FreedomOfMovement",Psykinetic,7,false),("LP_WS_Telekinesis",Psykinetic,9,false),("LP_WallOfStone",Psykinetic,9,true),
   ("LP_CS_Command",Telepath,3,false),("LP_WS_MindSpike",Telepath,3,false),("LP_CharmPerson",Telepath,3,false),("LP_PS_EgoWhip",Telepath,3,false),("LP_WS_Counterspell",Telepath,5,true),("LP_WS_Slow",Telepath,5,true),
   ("LP_WS_Confusion",Telepath,7,false),("LP_WS_PhantasmalKiller",Telepath,7,false),("LP_WS_ModifyMemory",Telepath,9,false),("LP_WS_DominatePerson",Telepath,9,false),
   ("LP_MistyStep",Warper,3,true),("LP_CS_Jump",Warper,3,false),("LP_MirrorImage",Warper,3,false),("LP_WS_Blink",Warper,5,true),("LP_Haste",Warper,5,true),
   ("LP_WS_DimensionDoor",Warper,7,false),("LP_WS_Banishment",Warper,7,false),("LP_WS_TeleportationCircle",Warper,9,false),("LP_WS_Scrying",Warper,9,false)};
  static HashSet<string> onlySet;static HashSet<string> OnlySet=>onlySet??(onlySet=new HashSet<string>(SubSpellRows.Where(r=>r.only).Select(r=>r.def)));
  static bool Ours(CasterListDef l)=>l!=null && l.classDef!=null && l.classDef==Class;
  public static bool SubOnly(CasterListDef l,AbilityDef d)=>d!=null && Ours(l) && OnlySet.Contains(d.defName);
  public static bool SubSpell(Hediff_Adventurer h,CasterListDef l,AbilityDef d){
   if(d==null || !Ours(l) || h==null)return false;int lv=Level(h);if(lv<=0)return false;
   foreach(var r in SubSpellRows)if(r.def==d.defName && lv>=r.level && Has(h,r.sub))return true;
   return false;
  }

  // ---------- 被动 ----------
  public static readonly HashSet<int> Users=new HashSet<int>();
  public static readonly HashSet<Pawn> Telepaths=new HashSet<Pawn>();
  public static bool Is(Pawn p)=>p!=null && Users.Contains(p.thingIDNumber);
  static void Passive(Pawn p,string hediff,bool on,float severity=1f){
   if(on){var x=Items.Has(p,hediff)?p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff(hediff)):Items.Give(p,hediff);if(x!=null && Mathf.Abs(x.Severity-severity)>.001f)x.Severity=severity;}
   else if(Items.Has(p,hediff))Items.Remove(p,hediff);
  }
  static readonly string[] PassiveHediffs={"LP_PS_Pool","LP_PS_MindHand","LP_PS_Resonance","LP_PS_DevilTongue","LP_PS_InerrantAim","LP_PS_Kinetic","LP_PS_EvolvedStone","LP_PS_EvolvedStride","LP_PS_EvolvedFlex","LP_LegTranscendentBuff"};
  public static int Now=>Find.TickManager.TicksGame;
  static bool Ready(SubclassData d,string key,int ticks){if(d==null)return false;d.ticks.TryGetValue(key,out int t);int now=Now;return t<=0 || now-t>=ticks || now<t;}
  static void Use(SubclassData d,string key){if(d!=null)d.ticks[key]=Math.Max(1,Now);}
  public static bool Resting(Pawn p)=>p!=null && (!p.Awake() || ClassSystem.Meditating(p) || p.CurJobDef?.joyKind!=null);
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;if(p==null)return;int lv=Level(h);
   if(lv<=0){if(Users.Remove(p.thingIDNumber)){Telepaths.Remove(p);if(!p.Dead)foreach(var n in PassiveHediffs)Items.Remove(p,n);}return;}
   if(p.Dead)return;Users.Add(p.thingIDNumber);var d=Data(h);
   // 睡醒回满；短休（连续 1 小时）回 1 点。
   bool asleep=!p.Awake();bool was=Get(d,SleepKey)>0;
   if(was && !asleep)Refill(h);
   if(asleep)d.ticks[SleepKey]=1;else d.ticks.Remove(SleepKey);
   if(Resting(p)){int t=Get(d,RestKey)+250;if(t>=RestTicks){t=0;if(Used(h)>0){Restore(h,1);if(p.Faction==Faction.OfPlayer)Items.Mote(p,"灵能点 +1".Loc(),Psi);}}d.ticks[RestKey]=t;}else d.ticks.Remove(RestKey);
   Awaken(h);
   foreach(var kv in DisciplineAbility){var a=DefCache<AbilityDef>.Get(kv.Value);if(a!=null && Discipline(h,kv.Key) && p.abilities?.GetAbility(a)==null)p.abilities.GainAbility(a);}
   int pts=Points(h);
   Passive(p,"LP_PS_Pool",true);
   Passive(p,"LP_PS_MindHand",true,Knows(p,"LP_PS_StrongTK")?2f:1f);
   Passive(p,"LP_PS_Resonance",true,lv>=10?2f:1f);
   Passive(p,"LP_PS_DevilTongue",Discipline(h,DTongue) && pts>0,Tier(lv));
   Passive(p,"LP_PS_InerrantAim",Discipline(h,DAim) && pts>0,Tier(lv));
   Passive(p,"LP_PS_Kinetic",Knows(p,"LP_PS_StrongTK"));
   int evo=Knows(p,"LP_PS_EvolvedForm") && Items.Has(p,"LP_PS_Mutable")?ClassSubclass.Choice(h,"psevo"):-1;
   Passive(p,"LP_PS_EvolvedStone",evo==0);Passive(p,"LP_PS_EvolvedStride",evo==1);Passive(p,"LP_PS_EvolvedFlex",evo==2);
   Passive(p,"LP_LegTranscendentBuff",ClassLegendary.HasLeg(p,"LP_LegTranscendentMind"));
   if(Knows(p,"LP_PS_TeleDistract") || Knows(p,"LP_PS_TeleBolster"))Telepaths.Add(p);else Telepaths.Remove(p);
   if(!p.Spawned || p.Downed)return;
   // 灵能储备（18 级）：被征召时灵能点少于 4 就补到 4，每天一次。
   if(lv>=18 && p.Drafted && Points(h)<4 && Ready(d,ReserveKey,GenDate.TicksPerDay)){SetUsed(h,MaxPoints(h)-4);Use(d,ReserveKey);Items.Mote(p,"灵能储备".Loc(),Psi);}
   // 观察入微：研究时自动花 1 点，4 小时研究速度提高（每天最多 2 次）。
   if(Discipline(h,DObserve) && p.CurJobDef==JobDefOf.Research && !Items.Has(p,"LP_PS_Observant") && pts>0){
    int day=Now/GenDate.TicksPerDay,cnt=Get(d,ObserveKey);if(cnt/10!=day)cnt=day*10;
    if(cnt%10<2 && Spend(p,1)){d.ticks[ObserveKey]=cnt+1;var x=Items.Give(p,"LP_PS_Observant",10000,Tier(lv));if(x!=null)x.Severity=Tier(lv);Items.Mote(p,"观察入微".Loc(),Psi);}
   }
   // 心灵鼓舞（心灵感应者 10 级）：感应范围内的殖民者共享一张心灵网络。
   if(Knows(p,"LP_PS_TeleBolster") && p.Map!=null)foreach(var o in Races124.PawnsNear(p.Map,p.Position,TeleRange(p)).ToList())if(o!=p && o.Faction==p.Faction && o.RaceProps.Humanlike && !o.Dead)Items.Give(o,"LP_PS_Linked",600,1f);
  }
  public static float TeleRange(Pawn p)=>Knows(p,"LP_PS_PotentThoughts")?24f:12f;
  // 心灵觉醒：1 级心灵链接；7 / 13 / 19 级至少 2 / 3 / 4 级。只升不降，设置可关。
  public static int PsylinkFor(int lv)=>lv>=19?4:lv>=13?3:lv>=7?2:lv>=1?1:0;
  public static bool Awaken(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null || p.Dead || !(LichMod.Settings?.psionPsylink??true) || !ModsConfig.RoyaltyActive || p.health?.hediffSet?.GetBrain()==null)return false;
   int want=PsylinkFor(Level(h));if(want<=0 || p.GetStatValue(StatDefOf.PsychicSensitivity)<=0.01f)return false;
   int cur=p.GetPsylinkLevel();if(cur>=want)return false;
   if(!p.HasPsylink && !p.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier))p.health.AddHediff(HediffDefOf.PsychicAmplifier,p.health.hediffSet.GetBrain());
   cur=p.GetPsylinkLevel();if(cur<want)p.ChangePsylinkLevel(want-cur,false);
   if(p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"的心灵觉醒：心灵链接 ".Loc()+p.GetPsylinkLevel()+" 级".Loc(),p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }

  // ---------- 施放 ----------
  static readonly Dictionary<string,int> Costs=new Dictionary<string,int>{{"PSTelepathy",1},{"PSAwareness",1},{"PSGuards",1},{"PSSharpened",1},{"PSMutable",1},{"PSTrance",1},{"PSBulwark",1},{"PSRescue",1},{"PSEnkindle",2},{"PSHeightenedTK",4},{"PSScramble",4},{"PSMassWarp",4},{"PSCatastrophe",3}};
  public static int CostOf(Pawn p,CompProperties_ClassEffect e){
   if(e?.effect==null || !Costs.TryGetValue(e.effect,out int c))return 0;
   var d=Data(Adventurer.Get(p));
   if(e.effect=="PSTelepathy" && Ready(d,"psTeleDay",GenDate.TicksPerDay))return 0; // 每天第一次免费
   if(e.effect=="PSMutable" && Knows(p,"LP_PS_FleshWeave"))return CanSpend(p,2)?2:1;
   return c;
  }
  public static string CostTip(Pawn p,CompProperties_ClassEffect e){
   if(e?.effect==null || e.passive || !e.effect.StartsWith("PS") || !Known.Contains(e.effect) || Level(p)<=0 || e.effect=="PSLifeSiphon" || e.effect=="PSCrush" || e.effect=="PSScream")return null; // 法术走法力
   int c=CostOf(p,e);return (c<=0?"不消耗灵能点（只看冷却）".Loc():"消耗：灵能点 ".Loc()+c)+"（现有 ".Loc()+Points(p)+" / "+MaxPoints(Adventurer.Get(p))+(p.HasPsylink?"；不够时每点改加神经热 ".Loc()+HeatPerPoint.ToString("0"):"")+"）".Loc();
  }
  public static string CannotCast(Pawn p,AbilityDef def,CompProperties_ClassEffect e){
   if(e?.effect==null || e.passive || !e.effect.StartsWith("PS"))return null;
   int c=CostOf(p,e);if(c>0 && !CanSpend(p,c))return "灵能点不足（".Loc()+Points(p)+" / "+c+"）".Loc()+(p.HasPsylink?"，神经热也放不下".Loc():"");
   if(e.effect=="PSBile" && !Items.Has(p,"LP_PS_FormLauncher"))return "先用生体武器变出内脏弩".Loc();
   if(e.effect=="PSBlink"){var d=Data(Adventurer.Get(p));if(!Ready(d,BlinkKey,2500) && !CanSpend(p,1))return "灵能点不足（每小时第一次免费）".Loc();}
   return null;
  }
  static readonly HashSet<string> Known=new HashSet<string>{"PSPropel","PSTelepathy","PSRestore","PSSurge","PSEnkindle","PSAwareness","PSGuards","PSSharpened","PSMutable","PSOrganic","PSBile","PSTrance","PSHeightenedTK",
   "PSInfiltrate","PSBulwark","PSScramble","PSBlink","PSRescue","PSBeacon","PSMassWarp","PSLifeSiphon","PSCrush","PSScream","PSCatastrophe","PSArcEyes","PSArcSeal","PSArcStorm","PSArcDominion"};
  static bool Hostile(Pawn a,Pawn t)=>AnyTarget.Hostile(a,t);
  static bool Friendly(Pawn a,Pawn t)=>AnyTarget.Friendly(a,t);
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e?.effect==null || !Known.Contains(e.effect))return null;var t=target.Thing as Pawn;var map=caster.Map;
   switch(e.effect){
    case "PSPropel":if(t==null || t.Dead || t==caster)return "需要选择另一个活着的目标。".Loc();if(t.BodySize>(Discipline(caster,DTK)?2f:1.6f))return "目标太大了（体型上限 ".Loc()+(Discipline(caster,DTK)?"2":"1.6")+"）。".Loc();
     float r=PropelRange(caster);if(caster.Position.DistanceTo(t.Position)>r)return "超出念动推拉的距离（".Loc()+r.ToString("0")+" 格）。".Loc();return null;
    case "PSBile":case "PSHeightenedTK":case "PSLifeSiphon":case "PSArcSeal":if(t==null || t.Dead)return "需要选择一个活着的目标。".Loc();if(!t.HostileTo(caster))return "只能对敌人使用。".Loc();return null;
    case "PSArcDominion":if(t==null || t.Dead || !t.RaceProps.Humanlike)return "需要选择一个人形。".Loc();if(!t.HostileTo(caster))return "只能对敌人使用。".Loc();if(!t.Downed)return "目标要先倒地。".Loc();if(ClassSubclass.Bossy(t))return "剧情首领和传奇单位不行。".Loc();return null;
    case "PSInfiltrate":if(t==null || t.Dead || t==caster || !t.RaceProps.Humanlike)return "需要选择另一个活着的人形。".Loc();return null;
    case "PSRescue":if(t==null || t.Dead || t==caster)return "需要选择一名倒地的友军。".Loc();if(t.HostileTo(caster))return "只能救友军。".Loc();if(!t.Downed)return "目标没有倒地。".Loc();return null;
    case "PSBlink":case "PSCrush":case "PSScramble":case "PSMassWarp":
     if(map==null || !target.Cell.IsValid || !target.Cell.InBounds(map))return "需要选择一个位置。".Loc();
     if(e.effect=="PSBlink" || e.effect=="PSMassWarp"){if(t!=null && e.effect=="PSBlink"){if(t.HostileTo(caster))return "只能和友军换位。".Loc();return null;}if(!target.Cell.Standable(map))return "那里站不住人。".Loc();if(!GenSight.LineOfSight(caster.Position,target.Cell,map))return "看不见那个位置。".Loc();}
     return null;
   }
   return null;
  }
  public static float PropelRange(Pawn p)=>Discipline(p,DTK)?20f:12f;
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e?.effect==null || !Known.Contains(e.effect))return false;
   var t=target.Thing as Pawn;var h=Adventurer.Get(p);var d=Data(h);float pw=ClassEffects.Power(p,e);int dur=ClassEffects.Duration(p,e);
   int cost=CostOf(p,e);if(cost>0 && !Spend(p,cost))return true;
   if(e.effect=="PSTelepathy")Use(d,"psTeleDay");
   switch(e.effect){
    case "PSPropel":if(t!=null)Propel(p,t);break;
    case "PSTelepathy":Sense(p,Roll(p,false)*4f);break;
    case "PSRestore":Refill(h);if(p.psychicEntropy!=null && p.HasPsylink)p.psychicEntropy.OffsetPsyfocusDirectly(.1f);Items.Mote(p,"灵能复苏：灵能点回满".Loc(),Psi);break;
    case "PSSurge":if(Items.Has(p,"LP_PS_SurgeOn")){Items.Remove(p,"LP_PS_SurgeOn");Items.Mote(p,"灵能涌动：关".Loc(),Psi);}else{Items.Give(p,"LP_PS_SurgeOn");Items.Mote(p,"灵能涌动：开".Loc(),Psi);}break;
    case "PSEnkindle":case "PSAwareness":case "PSSharpened":case "PSTrance":case "PSCatastrophe":Items.Give(p,e.hediff,dur,1f);Items.Mote(p,def.LabelCap,Psi);break;
    case "PSGuards":Items.Give(p,e.hediff,dur,1f);EndFear(p);Items.Mote(p,def.LabelCap,Psi);break;
    case "PSBulwark":{var x=Items.Give(p,e.hediff,dur,Tier(p));if(x!=null)x.Severity=Tier(p);Items.Mote(p,def.LabelCap,Psi);break;}
    case "PSMutable":Mutable(p,dur,cost>=2);break;
    case "PSOrganic":CycleForm(p);break;
    case "PSBile":if(Hostile(p,t)){Line(p,t);WizardSpells.Hit(p,t,CasterEffects.Dmg("Acid"),pw,.2f);if(!t.Dead && d!=null && Ready(d,"psBile",60)){Use(d,"psBile");WizardSpells.Hit(p,t,CasterEffects.Dmg("Acid"),pw*.8f,.2f);}}break;
    case "PSHeightenedTK":if(Hostile(p,t))Heighten(p,t,pw,dur);break;
    case "PSInfiltrate":Infiltrate(p,t);break;
    case "PSScramble":Scramble(p,target.Cell,ClassEffects.Radius(p,e),dur);break;
    case "PSBlink":Blink(p,target);break;
    case "PSRescue":if(t!=null && t.Spawned && p.Spawned){var c=FreeCellNear(p.Map,p.Position);if(c.IsValid){Races124.Blink(t,c);CasterEffects.TempHp(t,Roll(p)*2f,2500);Items.Mote(t,"救援跃迁".Loc(),Psi);}}break;
    case "PSBeacon":Beacon(p,h);break;
    case "PSMassWarp":MassWarp(p,target.Cell,6f);break;
    case "PSLifeSiphon":if(Hostile(p,t)){float dealt=WizardSpells.Hit(p,t,WizardSpells.Necrotic,pw,.3f);ClassMelee.Heal(p,dealt*.5f);Line(p,t);}break;
    case "PSCrush":Crush(p,def,target.Cell,ClassEffects.Radius(p,e),pw);break;
    case "PSScream":Scream(p,ClassEffects.Radius(p,e),pw,dur);break;
    case "PSArcEyes":if(p.Map!=null){Sense(p,9999f);foreach(var o in p.Map.mapPawns.FreeColonistsSpawned.ToList())Items.Give(o,"LP_PS_ArcEyesBuff",dur,1f);}break;
    case "PSArcSeal":if(Hostile(p,t)){if(ClassSubclass.Bossy(t) || DNDMind.TryResist(t))Items.Mote(t,"抵抗！".Loc(),Psi);else{Items.Give(t,"LP_PS_MindSealed",GenDate.TicksPerDay,1f);Items.Mote(t,"思维幽闭".Loc(),Psi);}Line(p,t);}break;
    case "PSArcStorm":{var x=Items.Give(p,"LP_PS_StormAura",600,1f) as Hediff_PsionStorm;if(x!=null){x.power=pw;x.radius=ClassEffects.Radius(p,e);}Items.Mote(p,"心灵风暴".Loc(),Psi);break;}
    case "PSArcDominion":if(t!=null && t.Downed && !DNDMind.TryResist(t)){if(p.Faction==Faction.OfPlayer)InteractionWorker_RecruitAttempt.DoRecruit(p,t);else t.SetFaction(p.Faction);Items.Mote(t,"心智被重塑".Loc(),Psi);}else if(t!=null)Items.Mote(t,"抵抗！".Loc(),Psi);break;
   }
   return true;
  }
  static void Line(Pawn p,Thing t){if(p.Map==null || t==null)return;var line=DefCache<FleckDef>.Get("PsycastPsychicLine");if(line!=null)FleckMaker.ConnectingLine(p.DrawPos,t.DrawPos,line,p.Map);}
  public static IntVec3 FreeCellNear(Map map,IntVec3 c){if(map==null)return IntVec3.Invalid;foreach(var x in GenRadial.RadialCellsAround(c,3f,false))if(x.InBounds(map) && x.Standable(map) && x.GetFirstPawn(map)==null)return x;return IntVec3.Invalid;}
  static void EndFear(Pawn p){var ms=p.MentalState;if(ms!=null && ClassSubclass.FearState(ms.def))ms.RecoverFromState();}

  // 念动推拉：免费推 / 拉 1 格（念动精修、念动技巧 2 格）；有灵能点时改推 灵能值/2 格（目标失败才扣点）。撞墙、撞人另受灵能值钝击。
  public static int Propel(Pawn p,Pawn t){
   bool friend=!t.HostileTo(p);
   if(!friend && DNDMind.TryResist(t)){Items.Mote(t,"抵抗！".Loc(),Psi);return 0;}
   bool free=Discipline(p,DTK) || Knows(p,"LP_PS_TKTechniques");int cells=free?2:1;float roll=0f;
   if(Points(p)>0 || Catastrophe(p)){roll=Roll(p,false);cells=Mathf.Max(cells,Mathf.RoundToInt(roll/2f));Spend(p,1);}
   if(Knows(p,"LP_PS_WarpPropel"))cells+=2;
   var dir=(t.Position-p.Position).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(1f,0f,0f);
   if(friend)dir=-dir; // 友军拉近
   var start=t.Position;var dest=DNDPush.Destination(t.Map,t.Position,dir,cells);DNDPush.Move(t,dest);
   int moved=Mathf.RoundToInt(start.DistanceTo(t.Position));
   if(!friend){
    if(moved<cells && roll>0f)t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,roll*StatPower(p),.1f,-1f,p));
    if(Knows(p,"LP_PS_TKTechniques")){float bolt=(roll>0f?roll:Low(Tier(p)))*2f*StatPower(p);WizardSpells.Hit(p,t,CasterEffects.Dmg("Force"),bolt,.2f);}
   }
   Items.Mote(t,friend?"念动拉近".Loc():"念动推开".Loc(),Psi);
   return moved;
  }
  public static int Sense(Pawn p,float range){
   if(p.Map==null)return 0;int n=0;var inv=DefCache<HediffDef>.Get("PsychicInvisibility");
   foreach(var o in Races124.PawnsNear(p.Map,p.Position,range).ToList()){if(!Hostile(p,o) || o.RaceProps.IsMechanoid)continue;n++;if(inv!=null){var x=o.health.hediffSet.GetFirstHediffOfDef(inv);if(x!=null)o.health.RemoveHediff(x);}Items.Give(o,"LP_PS_Sensed",2500,1f);}
   Items.Mote(p,"心灵感应：感知到 ".Loc()+n+" 个心智".Loc(),Psi);return n;
  }
  public static void Mutable(Pawn p,int dur,bool weave){
   if(Knows(p,"LP_PS_EvolvedForm"))dur=Mathf.Max(dur,10000);
   Items.Give(p,"LP_PS_Mutable",dur,1f);
   CasterEffects.TempHp(p,(Roll(p,false)+IntMod(p))*2f*StatPower(p),dur);
   if(weave)Items.Give(p,"LP_PS_FleshWeaveBuff",dur,1f);
   Items.Mote(p,"柔变之躯".Loc(),Psi);
  }
  public static int IntMod(Pawn p)=>Mathf.Max(1,Adventurer.Mod(Adventurer.Score(p,Adventurer.Int)));
  // 生体武器：骨刃 → 血肉巨锤 → 内脏弩 → 收起。
  static readonly string[] Forms={"LP_PS_FormBlade","LP_PS_FormMaul","LP_PS_FormLauncher"};
  public static int CycleForm(Pawn p){
   int cur=-1;for(int i=0;i<Forms.Length;i++)if(Items.Has(p,Forms[i]))cur=i;
   foreach(var f in Forms)Items.Remove(p,f);
   int next=cur+1;if(next>=Forms.Length){Items.Mote(p,"生体武器：收起".Loc(),Psi);return -1;}
   Items.Give(p,Forms[next]);Items.Mote(p,"生体武器：".Loc()+(next==0?"骨刃".Loc():next==1?"血肉巨锤".Loc():"内脏弩".Loc()),Psi);return next;
  }
  public static bool OrganicWeapon(Pawn p)=>Items.Has(p,"LP_PS_FormBlade") || Items.Has(p,"LP_PS_FormMaul") || Items.Has(p,"LP_PS_FormLauncher");
  public static void Heighten(Pawn p,Pawn t,float pw,int dur){
   bool boss=ClassSubclass.Bossy(t);
   t.stances?.stunner?.StunFor(boss?dur/2:dur,p,false);
   WizardSpells.Hit(p,t,CasterEffects.Dmg("Force"),pw,.3f);
   if(t.Spawned && !t.Dead){var dir=(t.Position-p.Position).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(0f,0f,1f);DNDPush.Move(t,DNDPush.Destination(t.Map,t.Position,dir,boss?2:5));}
   Line(p,t);Items.Mote(t,"高阶心灵遥控".Loc(),Psi);
  }
  public static void Infiltrate(Pawn p,Pawn t){
   if(t==null)return;var sb=new System.Text.StringBuilder();
   sb.AppendLine(t.LabelCap+(t.Faction!=null?"（".Loc()+t.Faction.Name+"）":""));
   if(t.needs?.mood!=null)sb.AppendLine("心情：".Loc()+t.needs.mood.CurLevelPercentage.ToStringPercent());
   if(t.skills!=null)sb.AppendLine("拿手技能：".Loc()+string.Join("、".Loc(),t.skills.skills.Where(s=>!s.TotallyDisabled).OrderByDescending(s=>s.Level).Take(3).Select(s=>s.def.label+" "+s.Level)));
   if(t.story?.traits!=null)sb.AppendLine("特性：".Loc()+string.Join("、".Loc(),t.story.traits.allTraits.Select(x=>x.LabelCap)));
   if(t.HostileTo(p) || t.IsPrisoner)sb.AppendLine("心智抗性：".Loc()+DNDMind.Resistance(t).ToStringPercent());
   if(t.IsPrisonerOfColony && t.guest!=null && !Items.Has(t,"LP_PS_Probed") && (Points(p)>0 || CanSpend(p,1))){
    bool caught=DNDMind.TryResist(t);Spend(p,1);float r=Roll(p);
    if(t.guest.resistance>0f){float before=t.guest.resistance;t.guest.resistance=Mathf.Max(0f,before-r*.5f);sb.AppendLine("潜入心灵：招募抗拒 ".Loc()+before.ToString("0.#")+" → "+t.guest.resistance.ToString("0.#"));}
    else if(t.guest.will>0f){float before=t.guest.will;t.guest.will=Mathf.Max(0f,before-r*.2f);sb.AppendLine("潜入心灵：意志 ".Loc()+before.ToString("0.#")+" → "+t.guest.will.ToString("0.#"));}
    Items.Give(t,"LP_PS_Probed",GenDate.TicksPerDay,1f);
    if(caught)sb.AppendLine("对方察觉到有人在翻它的心思。".Loc());
   }
   if(p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Find.WindowStack.Add(new Dialog_MessageBox(sb.ToString().TrimEnd(),null,null,null,null,"读心".Loc()));
   Line(p,t);
  }
  public static int Scramble(Pawn p,IntVec3 c,float r,int dur){
   if(p.Map==null)return 0;int n=0;Pawn spared=null;
   foreach(var o in Races124.PawnsNear(p.Map,c,r).Where(x=>Hostile(p,x) && !x.Downed).ToList()){
    if(o.RaceProps.IsMechanoid){o.stances?.stunner?.StunFor(120,p);continue;}
    if(ClassSubclass.Bossy(o)){o.stances?.stunner?.StunFor(dur/4,p);continue;}
    if(DNDMind.TryResist(o)){Items.Mote(o,"抵抗！".Loc(),Psi);continue;}
    var msh=o.mindState?.mentalStateHandler;
    if(msh!=null && msh.TryStartMentalState(MentalStateDefOf.Berserk,"心智搅乱".Loc(),true,true)){if(o.MentalState!=null)o.MentalState.forceRecoverAfterTicks=dur;n++;}
    else o.stances?.stunner?.StunFor(dur/2,p);
   }
   if(c.InBounds(p.Map))FleckMaker.Static(c,p.Map,FleckDefOf.PsycastAreaEffect,r*2f);
   Items.Mote(p,"心智搅乱".Loc(),Psi);return n;
  }
  public static bool Blink(Pawn p,LocalTargetInfo target){
   var d=Data(Adventurer.Get(p));
   if(Ready(d,BlinkKey,2500))Use(d,BlinkKey);else if(!Spend(p,1))return false;
   if(target.Thing is Pawn t && t!=p && !t.HostileTo(p)){PaladinCore.Swap(p,t);Items.Mote(p,"灵能换位".Loc(),Psi);return true;}
   if(!target.Cell.IsValid)return false;Races124.Blink(p,target.Cell);Items.Mote(p,"灵能瞬移".Loc(),Psi);return true;
  }
  // 跃迁信标：没有信标时放下（1 点）；有信标且在同一张地图时传回去，信标消失。
  public static void Beacon(Pawn p,Hediff_Adventurer h){
   var d=Data(h);if(d==null || p.Map==null)return;
   if(d.ticks.TryGetValue(BeaconMap,out int mid) && mid==p.Map.uniqueID+1 && d.ticks.TryGetValue(BeaconX,out int x) && d.ticks.TryGetValue(BeaconZ,out int z)){
    var c=new IntVec3(x,0,z);d.ticks.Remove(BeaconMap);
    if(c.InBounds(p.Map) && c.Standable(p.Map)){Races124.Blink(p,c);Items.Mote(p,"回到跃迁信标".Loc(),Psi);return;}
    Items.Mote(p,"信标所在的地方站不住人".Loc(),Psi);return;
   }
   if(!Spend(p,1))return;
   d.ticks[BeaconX]=p.Position.x;d.ticks[BeaconZ]=p.Position.z;d.ticks[BeaconMap]=p.Map.uniqueID+1;
   FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,1.5f);Items.Mote(p,"放下跃迁信标".Loc(),Psi);
  }
  public static int MassWarp(Pawn p,IntVec3 dest,float r){
   var map=p.Map;if(map==null)return 0;int max=IntMod(p)+2,n=0;
   var cells=GenRadial.RadialCellsAround(dest,5f,true).Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null).ToList();
   foreach(var o in Races124.PawnsNear(map,p.Position,r).Where(x=>x!=p && !x.Dead).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).ToList()){
    if(n>=max || cells.Count==0)break;bool foe=o.HostileTo(p);
    if(foe && (ClassSubclass.Bossy(o) || DNDMind.TryResist(o))){Items.Mote(o,"抵抗！".Loc(),Psi);continue;}
    var c=cells[0];cells.RemoveAt(0);Races124.Blink(o,c);if(foe)o.stances?.stunner?.StunFor(120,p);n++;
   }
   var self=cells.Count>0?cells[0]:dest;if(self.Standable(map))Races124.Blink(p,self);
   FleckMaker.Static(dest,map,FleckDefOf.PsycastAreaEffect,8f);Items.Mote(p,"群体跃迁 ×".Loc()+n,Psi);return n;
  }
  public static int Crush(Pawn p,AbilityDef def,IntVec3 c,float r,float pw){
   var map=p.Map;if(map==null)return 0;int n=0;bool enh=Knows(p,"LP_PS_EnhancedCrush") && Points(p)>0;if(enh)Spend(p,1);
   float extra=enh?Roll(p)*2f:0f;
   foreach(var o in Races124.PawnsNear(map,c,r).Where(x=>Hostile(p,x)).ToList()){
    bool saved=Rand.Chance(ClassSubclass.ResistChance(o));float a=(saved?pw*.5f:pw)+extra;
    WizardSpells.Hit(p,o,CasterEffects.Dmg("Force"),a,.3f);n++;
    if(!o.Dead && !saved)o.stances?.stunner?.StunFor(120,p);
    if(!o.Dead && enh)Items.Give(o,"LP_PS_Slowed",300,1f);
   }
   foreach(var b in GenRadial.RadialDistinctThingsAround(c,map,r,true).OfType<Building>().ToList())if(!b.Destroyed && b.def.useHitPoints)b.TakeDamage(new DamageInfo(DamageDefOf.Blunt,pw*2f,0f,-1f,p));
   if(c.InBounds(map))FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,r*2f);
   Items.Mote(p,def.label,Psi);return n;
  }
  public static int Scream(Pawn p,float r,float pw,int dur){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var o in Races124.PawnsNear(map,p.Position,r).Where(x=>Hostile(p,x) && !x.RaceProps.IsMechanoid && !(x.RaceProps.Animal && x.RaceProps.intelligence==Intelligence.Animal && x.BodySize<.5f)).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).Take(10).ToList()){
    bool saved=Rand.Chance(ClassSubclass.ResistChance(o));WizardSpells.Hit(p,o,WizardSpells2.Psychic,saved?pw*.5f:pw,1f);n++;
    if(!o.Dead && !saved){o.stances?.stunner?.StunFor(ClassSubclass.Bossy(o)?dur/2:dur,p);if(o.Downed){var brain=o.health.hediffSet.GetBrain();if(brain!=null)o.TakeDamage(new DamageInfo(DamageDefOf.Blunt,3f,1f,-1f,p,brain));}}
   }
   FleckMaker.Static(p.Position,map,FleckDefOf.PsycastAreaEffect,12f);Items.Mote(p,"心灵尖啸".Loc(),Psi);return n;
  }

  // ---------- 施法前后（灵能修行） ----------
  static string School(AbilityDef d)=>CasterSpells.CExt(d)?.school??Spellbook.Ext(d)?.school;
  static int Circle(Pawn p,AbilityDef d)=>CasterSpells.CircleFor(p,d);
  static bool PsionSpell(Pawn p,AbilityDef d)=>Class!=null && CasterSpells.CastClass(p,d)==Class;
  public static void BeforeCast(Ability_Class a,LocalTargetInfo target){
   var p=a?.pawn;if(p==null || Users.Count==0 || !Is(p) || !PsionSpell(p,a.def))return;
   var s=School(a.def);if((s=="惑控" || s=="幻术") && Discipline(p,DId) && target.Thing is Pawn t && t.HostileTo(p) && Points(p)>0 && Spend(p,1)){var x=Items.Give(t,"LP_PS_IdInsinuation",120,Tier(p));if(x!=null)x.Severity=Tier(p);Items.Mote(t,"心智暗示".Loc(),Psi);}
  }
  public static void AfterCast(Ability_Class a,LocalTargetInfo target){
   var p=a?.pawn;if(p==null || Users.Count==0 || !Is(p))return;
   if(!PsionSpell(p,a.def) || Circle(p,a.def)<1)return;
   var s=School(a.def);var t=target.Thing as Pawn;int mod=IntMod(p);
   if((s=="死灵" || s=="变化") && Discipline(p,DBio)){int n=Mathf.Min(mod,Points(p));if(n>0 && Spend(p,n)){float sum=0f;for(int i=0;i<n;i++)sum+=Roll(p,false);CasterEffects.TempHp(p,(sum+mod)*StatPower(p),2500);Items.Mote(p,"生物反馈".Loc(),Psi);}}
   if((s=="防护" || s=="预言") && Discipline(p,DPrecog) && Points(p)>0 && Spend(p,1)){var who=t!=null && !t.HostileTo(p) && t.Spawned?t:p;var x=Items.Give(who,"LP_PS_Precog",600,Tier(p));if(x!=null)x.Severity=Tier(p);Items.Mote(who,"预知增幅".Loc(),Psi);}
   if((s=="咒法" || s=="塑能") && Discipline(p,DDestructive) && t!=null && t.HostileTo(p) && !t.Dead){int n=Mathf.Min(mod,Points(p));if(n>0 && Spend(p,n)){float sum=0f;for(int i=0;i<n;i++)sum+=Roll(p,false);WizardSpells.Hit(p,t,WizardSpells2.Psychic,(sum+mod)*StatPower(p),1f);Items.Mote(t,"毁灭思绪".Loc(),Psi);}}
  }

  // ---------- 伤害（PsionDamage） ----------
  public static void Damage(Pawn v,ref DamageInfo d){
   var a=d.Instigator as Pawn;bool psychic=ClassSubclass.Psychic(d.Def);
   if(a!=null && a!=v && Is(a)){
    if(psychic && Items.Has(a,"LP_PS_SharpenedBuff"))d.SetAmount(d.Amount*1.3f);
    if(psychic && Knows(a,"LP_PS_PotentThoughts"))d.SetAmount(d.Amount+2f*IntMod(a));
    if(d.Weapon==null && d.WeaponLinkedHediff==null && Items.Has(a,"LP_PS_TranceBuff") && v.HostileTo(a) && !ClassMartial2.Melee(d))d.SetAmount(d.Amount+Roll(a,false)*2f);
    if(d.WeaponLinkedHediff!=null && d.WeaponLinkedHediff.defName.StartsWith("LP_PS_Form") && Knows(a,"LP_PS_LifeBending") && v.HostileTo(a)){
     d.SetAmount(d.Amount+Roll(a,false));
     var ad=Data(Adventurer.Get(a));
     if(a.Map!=null && Points(a)>0 && Ready(ad,LifeKey,180)){var hurt=Races124.PawnsNear(a.Map,a.Position,6f).Where(o=>o.Faction==a.Faction && !o.Dead && ClassMelee.InjurySum(o)>0f).ToList();if(hurt.Count>0 && Spend(a,1)){Use(ad,LifeKey);float heal=Roll(a)+IntMod(a);foreach(var o in hurt)ClassMelee.Heal(o,heal);Items.Mote(a,"生死扭曲".Loc(),Psi);}}
    }
   }
   if(psychic && (Items.Has(v,"LP_PS_Fortress") || Items.Has(v,"LP_PS_BulwarkBuff")))d.SetAmount(d.Amount*.5f);
   if(a==null || a==v || !a.HostileTo(v) || d.Amount<=0f)return;
   // 心灵干扰：心灵感应者感应范围内的友军被打中时，有 档% × 2 的机会改成打偏（成功才扣点，每 3 秒一次）。
   if(Telepaths.Count>0 && v.Map!=null)foreach(var tp in Telepaths){
    if(tp==null || tp.Dead || tp.Downed || tp.Map!=v.Map || tp.Faction!=v.Faction || !Knows(tp,"LP_PS_TeleDistract") || tp.Position.DistanceTo(v.Position)>TeleRange(tp))continue;
    var td=Data(Adventurer.Get(tp));if(!Ready(td,DistractKey,180) || !CanSpend(tp,1))continue;Use(td,DistractKey);
    if(Rand.Chance(Pct(tp)*2f)){Spend(tp,1);d.SetAmount(0f);Items.Mote(v,"心灵干扰：打偏".Loc(),Psi);return;}
    break;
   }
   if(!Is(v) || v.Dead)return;var vd=Data(Adventurer.Get(v));
   // 换位迷踪（灵跃者 6 级）：被远程攻击时和 6 格内一个敌人换位，这一下打到它身上。
   if(Knows(v,"LP_PS_Displace") && AttackKind.IsRanged(d) && v.Map!=null && Ready(vd,DisplaceKey,600) && CanSpend(v,1)){
    var foe=Races124.PawnsNear(v.Map,v.Position,6f).Where(o=>o!=a && Hostile(v,o) && !o.Downed && !ClassSubclass.Bossy(o)).OrderBy(o=>o.Position.DistanceToSquared(v.Position)).FirstOrDefault();
    if(foe!=null && Spend(v,1)){Use(vd,DisplaceKey);PaladinCore.Swap(v,foe);var copy=d;d.SetAmount(0f);foe.TakeDamage(copy);Items.Mote(v,"换位迷踪".Loc(),Psi);return;}
   }
   // 灵能反噬：被打中时伤害 −（2 × 灵能值 + 智力调整值），攻击者没抵抗住就受等量心灵伤害（每 10 秒一次）。
   if(Discipline(Adventurer.Get(v),DBacklash) && Ready(vd,BacklashKey,600) && Points(v)>0 && Spend(v,1)){
    Use(vd,BacklashKey);float cut=Mathf.Min(d.Amount,2f*Roll(v)+IntMod(v));d.SetAmount(d.Amount-cut);
    if(!DNDMind.TryResist(a) && !a.Dead)a.TakeDamage(new DamageInfo(WizardSpells2.Psychic,cut,1f,-1f,v));
    Items.Mote(v,"灵能反噬 −".Loc()+cut.ToString("0"),Psi);
   }
   // 反弹力场（念动师 6 级）：护盾术挡着时，远程攻击打来就反弹力场伤害并得到等量临时生命。
   if(Knows(v,"LP_PS_ReboundField") && AttackKind.IsRanged(d) && Items.Has(v,"LP_ShieldSpell") && Points(v)>0 && Spend(v,1)){
    float back=(Roll(v,false)+IntMod(v))*2f*StatPower(v);bool saved=Rand.Chance(ClassSubclass.ResistChance(a));if(saved)back*=.5f;
    a.TakeDamage(new DamageInfo(CasterEffects.Dmg("Force"),back,.3f,-1f,v));CasterEffects.TempHp(v,back,1200);Items.Mote(v,"反弹力场".Loc(),Psi);
   }
  }
 }

 // 心灵风暴（8 环秘传）：每秒伤害半径内的敌人。
 public class Hediff_PsionStorm : HediffWithComps {
  public float power=15f,radius=12f;int acc;
  public override void TickInterval(int delta){
   base.TickInterval(delta);acc+=delta;if(acc<60)return;acc=0;var p=pawn;if(p==null || !p.Spawned || p.Dead)return;
   foreach(var o in Races124.PawnsNear(p.Map,p.Position,radius).ToList())if(o!=p && !o.Dead && o.HostileTo(p))WizardSpells.Hit(p,o,WizardSpells2.Psychic,power,1f);
   FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,radius);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref power,"lpStormPower",15f);Scribe_Values.Look(ref radius,"lpStormRadius",12f);}
 }
 // 灵能点显示在健康页（不加新界面）。
 public class Hediff_PsionPool : HediffWithComps {
  public override string LabelInBrackets=>Psion.PointsText(pawn)??base.LabelInBrackets;
  public override string TipStringExtra=>Psion.PointsTip(pawn)+"\n"+base.TipStringExtra;
  public override bool Visible=>true;
 }
 public class GameComponent_Psion : GameComponent {
  public GameComponent_Psion(Game game){Psion.Users.Clear();Psion.Telepaths.Clear();Gunslinger.Users.Clear();}
  public override void FinalizeInit(){base.FinalizeInit();Psion.Users.Clear();Psion.Telepaths.Clear();Gunslinger.Users.Clear();}
  public override void GameComponentTick(){if(Find.TickManager.TicksGame%Martial.HourTicks==7){Psion.Telepaths.RemoveWhere(x=>x==null || x.Destroyed || x.Dead);}}
 }

 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class PsionDamage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(Psion.Users.Count==0)return;
   if(dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   Psion.Damage(__instance,ref dinfo);
  }
 }
}
