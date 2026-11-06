using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.102.0 第二批子职业（设计见「六职业新子职业设计」，Defs/Subclasses2.xml）：奥法骑士、灵能战士（战士），血咒学派（法师），战铸师（器械师），异变心灵（术士），征服之誓、救赎之誓（圣武士）。
 // 资源：奥法骑士用子职业法力（和诡术师同一个 sub.mana，ClassSubclass.ManaMax 加上这里的 EKManaMax）；灵能战士有灵能器时不耗斗志、改加神经热（Free + AfterCast）；
 // 血咒学派付失血（血族付血原质，CannotCast / Pay）；两个誓言的「誓言之力」每天按次数刷新（sub.oath）。
 // 被动按能力位（Users），每 250 刻在 Tick250 更新；伤害挂钩（Subclass2Damage）、击杀挂钩（Subclass2Kill）第一步都是空集合判断。
 public static class Subclass2 {
  public const string EldritchKnight="LP_Sub_EldritchKnight",PsiWarrior="LP_Sub_PsiWarrior",Hemomancy="LP_Sub_Hemomancy",BattleSmith="LP_Sub_BattleSmith",
   Aberrant="LP_Sub_AberrantMind",Conquest="LP_Sub_Conquest",Redemption="LP_Sub_Redemption",DraconicSoul="LP_Sub_DraconicSoul",FiendBlood="LP_Sub_FiendBlood";
  static readonly string[] Mine={EldritchKnight,PsiWarrior,Hemomancy,BattleSmith,Aberrant,Conquest,Redemption,DraconicSoul,FiendBlood};
  static readonly DefRef<LP_ClassDef> wizard=new DefRef<LP_ClassDef>("LP_Class_Wizard"),artificer=new DefRef<LP_ClassDef>("LP_Class_Artificer");
  public static readonly Color EKColor=new Color(.55f,.5f,.95f),PsiColor=new Color(.45f,.85f,.9f),BloodColor=new Color(.8f,.2f,.25f),SmithColor=new Color(.85f,.7f,.4f),
   AberrantColor=new Color(.35f,.6f,.45f),ConquestColor=new Color(.55f,.5f,.55f),RedeemColor=new Color(.9f,.92f,.95f);
  public const float BloodCap=.45f,RiteBloodCap=.6f,HemogenPerPct=.0125f,WoundBonusMax=.3f,SpellVuln=1.25f,AbsorbFactor=.5f,PsiFieldFactor=.7f,PsiFieldNoLink=.85f,PsiHeatPerHit=4f,
   GuardFieldFactor=.5f,DefenderPer=.1f,ImprovedFactor=.8f,SmithAura=.8f,GuardChance=.4f,WeaponFactor=1.1f,ScornChance=.25f,ScornFactor=1.25f,InvincibleFactor=.5f,
   AngelFactor=.75f,GuardianShare=.75f,YieldChance=.085f,ReconcileChance=.4f,DominateChance=.6f,DominateStay=.3f,CalmChance=.4f,TentacleChance=.3f;
  public const int DefenderSteel=100,DefenderComponents=2,RepairSteel=20,RepairWindow=60000,DominateTicks=60000,YieldTicks=60000,OathbrokenTicks=60000;

  // ---------- 被动能力位 ----------
  public const int FWarMagic=1,FEldritchStrike=2,FBladestorm=4,FPsiField=8,FPsiAdept=16,FPsiGuard=32,FPsiStorm=64,FHemo=128,FSiphon=256,FRite=512,
   FBattleReady=1<<10,FJolt=1<<11,FImproved=1<<12,FMasterSmith=1<<13,FAMDefense=1<<14,FConquest=1<<15,FCQAura=1<<16,FCQScorn=1<<17,FCQYield=1<<18,
   FRedemption=1<<19,FRDGuardian=1<<20,FRDSpirit=1<<21,FRDAngel=1<<22,FDefender=1<<23,
   FDragonBlood=1<<24,FDragonFlight=1<<25,FDragonPresence=1<<26,FFiend=1<<27,FFiendAwaken=1<<28,FFiendBound=1<<29;
  static readonly Dictionary<string,int> FlagOf=new Dictionary<string,int>{
   {"LP_EK_WarMagic",FWarMagic},{"LP_EK_EldritchStrike",FEldritchStrike},{"LP_EK_Bladestorm",FBladestorm},
   {"LP_PW_ProtectiveField",FPsiField},{"LP_PW_PsiAdept",FPsiAdept},{"LP_PW_GuardianField",FPsiGuard},{"LP_PW_TeleStorm",FPsiStorm},
   {"LP_HM_BloodSpear",FHemo},{"LP_HM_Siphon",FSiphon},{"LP_HM_BloodRite",FRite},
   {"LP_BS_SteelDefender",FDefender},{"LP_BS_BattleReady",FBattleReady},{"LP_BS_ArcaneJolt",FJolt},{"LP_BS_ImprovedDefender",FImproved},{"LP_BS_MasterSmith",FMasterSmith},
   {"LP_AM_PsychicDefense",FAMDefense},
   {"LP_CQ_ConqueringPresence",FConquest},{"LP_CQ_AuraOfConquest",FCQAura},{"LP_CQ_ScornfulRebuke",FCQScorn},{"LP_CQ_InvincibleConqueror",FCQYield},
   {"LP_RD_EmissaryOfPeace",FRedemption},{"LP_RD_AuraOfGuardian",FRDGuardian},{"LP_RD_ProtectiveSpirit",FRDSpirit},{"LP_RD_EmissaryOfRedemption",FRDAngel},
   {"LP_DS_DragonBlood",FDragonBlood},{"LP_DS_DragonFlight",FDragonFlight},{"LP_DS_DragonPresence",FDragonPresence},
   {"LP_FB_Eyes",FFiend},{"LP_FB_Awaken",FFiendAwaken},{"LP_FB_Bound",FFiendBound}};
  public static readonly Dictionary<int,int> Users=new Dictionary<int,int>();
  public static readonly HashSet<Pawn> Defenders=new HashSet<Pawn>(),Rebukers=new HashSet<Pawn>(),Guardians=new HashSet<Pawn>(),Fliers=new HashSet<Pawn>(); // Fliers：龙翼，走路无视地形（ClassWoodstride）
  public static int Flags(Pawn p){if(p==null || Users.Count==0)return 0;Users.TryGetValue(p.thingIDNumber,out int f);return f;}
  public static bool S(Pawn p,int f)=>(Flags(p) & f)!=0;
  static int Now=>Find.TickManager.TicksGame;
  static float Roll=>ClassMartial2.RollOverride??Rand.Value;
  static bool Ready(SubclassData d,string key,int ticks){if(d==null)return false;d.ticks.TryGetValue(key,out int t);int now=Now;return t<=0 || now-t>=ticks || now<t;}
  static void Use(SubclassData d,string key){if(d!=null)d.ticks[key]=Math.Max(1,Now);}
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.HostileTo(a);
  static bool Friendly(Pawn a,Pawn t)=>t!=null && !t.Dead && !t.HostileTo(a);
  static bool Ally(Pawn a,Pawn t)=>t!=null && !t.Dead && t.Faction!=null && t.Faction==a.Faction && !t.HostileTo(a);
  static IEnumerable<Pawn> Near(Pawn p,IntVec3 c,float r)=>p.Map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(p.Map,c,r);
  static bool Boss(Pawn t)=>ClassSubclass.Bossy(t);
  // 成功率：基础值，目标每比施法者高 1 级 −5%，最低 10%。
  public static float Chance(float baseChance,Pawn caster,Pawn t)=>Mathf.Clamp(baseChance-.05f*Mathf.Max(0,ClassSubclass.LevelOf(t)-ClassSubclass.LevelOf(caster)),.1f,1f);
  public static bool Bloodless(Pawn t)=>t==null || t.RaceProps.IsMechanoid || t.RaceProps.BloodDef==null || Items.IsLich(t) || Items.Undead(t);

  static bool Mine0(Hediff_Adventurer h){var d=ClassSubclass.Data(h);if(d==null)return false;foreach(var s in d.picks)if(Array.IndexOf(Mine,s.defName)>=0)return true;return false;}
  public static int Refresh(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return 0;int f=0;
   if(!p.Dead && Mine0(h)){var list=p.abilities?.abilities;if(list!=null)for(int k=0;k<list.Count;k++)if(FlagOf.TryGetValue(list[k].def.defName,out int b))f|=b;}
   if(f==0)Users.Remove(p.thingIDNumber);else Users[p.thingIDNumber]=f;
   if((f & FRDGuardian)!=0)Guardians.Add(p);else Guardians.Remove(p);
   if((f & FDragonFlight)!=0)Fliers.Add(p);else Fliers.Remove(p);
   return f;
  }
  static readonly string[] PassiveHediffs={"LP_PW_AdeptBuff","LP_PW_MindBuff","LP_PW_StormBuff","LP_AM_DefenseBuff","LP_RD_SpiritBuff"};
  static void Passive(Pawn p,string hediff,bool on){if(on){if(!Items.Has(p,hediff))Items.Give(p,hediff);}else if(Items.Has(p,hediff))Items.Remove(p,hediff);}

  // ---------- 每 250 刻（ClassSystem.Tick250） ----------
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;if(p==null)return;
   if(Users.Count==0 && !Mine0(h))return;
   if(p.Dead){Users.Remove(p.thingIDNumber);Guardians.Remove(p);return;}
   int f=Refresh(h);var d=ClassSubclass.Data(h);
   if(f==0){foreach(var n in PassiveHediffs)if(Items.Has(p,n))Items.Remove(p,n);return;}
   Passive(p,"LP_PW_AdeptBuff",(f & FPsiAdept)!=0);Passive(p,"LP_PW_MindBuff",(f & FPsiGuard)!=0);Passive(p,"LP_PW_StormBuff",(f & FPsiStorm)!=0);
   Passive(p,"LP_AM_DefenseBuff",(f & FAMDefense)!=0);Passive(p,"LP_RD_SpiritBuff",(f & FRDSpirit)!=0);
   if((f & (FConquest|FRedemption))!=0)OathRefill(h,d);
   if((f & FFiend)!=0)FiendTick(p,f);
   if(!p.Spawned || p.Downed)return;
   if((f & FDragonPresence)!=0)DragonPresence(p);
   if((f & FDefender)!=0)DefenderTick(p,h,d,f);
   if((f & FCQAura)!=0)ConquestAura(p);
   if((f & FCQYield)!=0)Yield(p);
   if((f & FRDSpirit)!=0 && p.health.summaryHealth.SummaryHealthPercent<.5f){float s=Divine(p);float healed=ClassMelee.Heal(p,s);if(healed>0f)Items.Mote(p,"坚韧之魂",RedeemColor);}
  }
  static float Divine(Pawn p){var s=DefCache<StatDef>.Get("LP_DivinePower");return s==null?1f:p.GetStatValue(s);}
  static float Martial(Pawn p){var s=DefCache<StatDef>.Get("LP_MartialPower");return s==null?1f:p.GetStatValue(s);}
  static float Artifice(Pawn p){var s=DefCache<StatDef>.Get("LP_ArtificePower");return s==null?1f:p.GetStatValue(s);}

  // ---------- 奥法骑士 ----------
  static readonly HashSet<string> EKSpells=new HashSet<string>{"LP_EK_Shield","LP_EK_ThunderBlade","LP_EK_AbsorbElements","LP_EK_ArcaneCharge","LP_EK_Bladestorm"};
  public static bool EKSpell(AbilityDef d)=>d!=null && EKSpells.Contains(d.defName);
  public static int EKManaMax(Hediff_Adventurer h){if(!ClassSubclass.Has(h,EldritchKnight))return 0;int lv=ClassSystem.Level(h,ClassSystem.Fighter);return lv<=0?0:1+lv/3+(ClassSystem.Knows(h.pawn,"LP_EK_Bladestorm")?2:0);}
  public static bool IsSpell(DamageDef d)=>d!=null && (ClassSubclass.Element(d)>=0 || ClassSubclass.Psychic(d) || d==Warlock.ForceDamage || d.defName.Contains("Arcane"));
  public static string BondCannot(Pawn p){var d=ClassSubclass.Data(p);var held=p.equipment?.Primary;if(held==null && (d?.bond==null || d.bond.Destroyed))return "需要手里拿着一把武器才能绑定。";return null;}
  // 武器羁绊：手里不是绑定的武器、而绑定的武器还在这张地图上 → 召回到手里；否则把手里的武器设为绑定武器（替换旧的）。
  public static string Bond(Pawn p){
   var d=ClassSubclass.Data(p);if(d==null || p.equipment==null)return null;var held=p.equipment.Primary;var b=d.bond as ThingWithComps;
   if(b!=null && !b.Destroyed && b!=held && b.MapHeld==p.MapHeld){
    if(b.ParentHolder is Pawn_EquipmentTracker et)et.Remove(b);else if(b.holdingOwner!=null)b.holdingOwner.Remove(b);else if(b.Spawned)b.DeSpawn();
    if(held!=null)p.equipment.TryDropEquipment(held,out _,p.Position);
    p.equipment.AddEquipment(b);Items.Mote(p,"召回绑定武器",EKColor);return "recall";
   }
   if(held==null)return null;
   d.bond=held;Items.Mote(p,"武器羁绊："+held.LabelNoCount,EKColor);return "bind";
  }
  public static bool Bound(Pawn p){var d=ClassSubclass.Data(p);return d?.bond!=null && p.equipment?.Primary==d.bond;}
  static Pawn AdjacentFoe(Pawn p){
   var t=p.mindState?.enemyTarget as Pawn;if(Hostile(p,t) && !t.Downed && t.Spawned && t.Position.AdjacentTo8WayOrInside(p.Position))return t;
   return Near(p,p.Position,1.5f).FirstOrDefault(x=>Hostile(p,x) && !x.Downed && x.Position.AdjacentTo8WayOrInside(p.Position));
  }

  // ---------- 灵能战士 ----------
  // 神经热写在能力定义的 <heat> 里：有灵能器、神经热放得下时这次不耗斗志（ClassSystem.FreeCast），施放后加神经热（AfterCast）。
  public static float HeatOf(AbilityDef d)=>d?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault()?.heat??0f;
  static bool HeatRoom(Pawn p,float heat){var e=p?.psychicEntropy;return p.HasPsylink && e!=null && e.EntropyValue+heat<=e.MaxEntropy+.001f;}

  // ---------- 血咒学派 ----------
  static readonly Dictionary<string,int> Blood=new Dictionary<string,int>{{"LP_HM_BloodSpear",8},{"LP_HM_BloodBind",8},{"LP_HM_FleshShaping",16},{"LP_HM_BloodBoil",24},{"LP_HM_FleshPrison",24},{"LP_HM_BloodRite",30}};
  public static int BloodOf(AbilityDef d)=>d!=null && Blood.TryGetValue(d.defName,out int v)?v:0;
  public static float BloodLoss(Pawn p){var h=p?.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.BloodLoss);return h?.Severity??0f;}
  public static float Cap(Pawn p)=>ClassSystem.Knows(p,"LP_HM_BloodRite")?RiteBloodCap:BloodCap;
  static Gene_Hemogen Hemogen(Pawn p)=>p?.genes?.GetFirstGeneOfType<Gene_Hemogen>();
  public static void AddBlood(Pawn p,float amount){
   if(p?.health==null || amount==0f)return;var h=p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
   if(amount<0f){if(h!=null){h.Severity+=amount;if(h.Severity<=.001f)p.health.RemoveHediff(h);}return;}
   if(h==null){h=HediffMaker.MakeHediff(HediffDefOf.BloodLoss,p);h.Severity=amount;p.health.AddHediff(h);}else h.Severity+=amount;
  }
  static string BloodCannot(Pawn p,int pct){
   if(Bloodless(p))return "身体里没有血，不能施放血咒";
   var g=Hemogen(p);if(g!=null){float need=pct*HemogenPerPct;return g.Value+.001f<need?"血原质不足（"+(g.Value*100f).ToString("0")+" / "+(need*100f).ToString("0")+"）":null;}
   float cur=BloodLoss(p),cap=Cap(p);return cur+pct/100f>cap+.001f?"失血过多（"+cur.ToStringPercent()+"，上限 "+cap.ToStringPercent()+"）":null;
  }
  static void PayBlood(Pawn p,int pct){var g=Hemogen(p);if(g!=null){g.Value-=pct*HemogenPerPct;return;}AddBlood(p,pct/100f);}
  // 越伤越强：伤势每 10% 血咒伤害 +5%，最多 +30%。
  public static float WoundFactor(Pawn p)=>1f+Mathf.Min(WoundBonusMax,.5f*(1f-p.health.summaryHealth.SummaryHealthPercent));
  static readonly HashSet<string> HemoEffects=new HashSet<string>{"S2BloodSpear","S2BloodBoil"};
  public static float PowerFactor(Pawn p,CompProperties_ClassEffect e)=>e!=null && p?.health!=null && HemoEffects.Contains(e.effect)?WoundFactor(p):1f;

  // ---------- 誓言 ----------
  static readonly HashSet<string> OathAbilities=new HashSet<string>{"LP_CQ_ConqueringPresence","LP_CQ_GuidedStrike","LP_RD_EmissaryOfPeace","LP_RD_RebukeViolent","LP_OB_DreadfulAspect","LP_OB_ControlUndead"}; // 1.120.0 破誓者（Subclass_Oathbreaker.cs）也用誓言之力
  public static bool Oath(AbilityDef d)=>d!=null && OathAbilities.Contains(d.defName);
  public static int OathMax(Hediff_Adventurer h){int lv=ClassSystem.Level(h,ClassSystem.Paladin);return lv<=0?0:1+(lv>=11?1:0)+(lv>=15?1:0);}
  public static void OathRefill(Hediff_Adventurer h,SubclassData d){
   if(d==null)return;int day=GenDate.DaysPassed;d.ticks.TryGetValue("oathDay",out int last);
   if(last!=day+1){d.ticks["oathDay"]=day+1;d.oath=OathMax(h);}
  }
  public static int OathLeft(Pawn p){var h=Adventurer.Get(p);var d=ClassSubclass.Data(h);if(d==null)return 0;OathRefill(h,d);return d.oath;}
  // 背誓：1 天内神恩恢复 ×0.5、誓言之力不能用；1 天内第三次背誓再加心情「背弃誓言」。
  public static void Oathbreak(Pawn p,string why){
   if(p==null || p.Dead)return;var d=ClassSubclass.Data(p);
   Items.Give(p,"LP_Oathbroken",OathbrokenTicks);Items.Mote(p,"背誓",new Color(.6f,.4f,.4f));
   if(d!=null){
    d.ticks.TryGetValue("breakT",out int t);d.ticks.TryGetValue("breakN",out int n);
    if(t<=0 || Now-t>OathbrokenTicks || Now<t){n=0;d.ticks["breakT"]=Math.Max(1,Now);}
    d.ticks["breakN"]=++n;
    if(n>=3){var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_OathForsaken");if(th!=null)p.needs?.mood?.thoughts?.memories?.TryGainMemory(th);d.ticks["breakN"]=0;}
   }
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"违背了誓言（"+why+"）：1 天内神恩恢复减半，誓言之力不能用。",p,MessageTypeDefOf.NegativeEvent,false);
  }

  // ---------- 资源挂钩（ClassSystem.CannotCast / Pay / FreeCast，Ability_Class.Tooltip） ----------
  public static string CannotCast(Pawn p,AbilityDef def,CompProperties_ClassEffect props){
   if(Users.Count==0 && !ClassSubclass.Has(p,Hemomancy) && !ClassSubclass.Has(p,Conquest) && !ClassSubclass.Has(p,Redemption) && !ClassSubclass.Has(p,Oathbreaker.Sub))return null;
   int b=BloodOf(def);if(b>0){var w=BloodCannot(p,b);if(w!=null)return w;}
   if(Oath(def)){if(Items.Has(p,"LP_Oathbroken"))return "背誓中，誓言之力不能用";if(OathLeft(p)<=0)return "今天的誓言之力用完了";}
   return null;
  }
  public static bool Pay(Pawn p,AbilityDef def,CompProperties_ClassEffect props){
   int b=BloodOf(def);if(b>0){PayBlood(p,b);return true;}
   if(Oath(def)){var d=ClassSubclass.Data(p);if(d!=null && d.oath>0)d.oath--;return true;}
   return false;
  }
  public static bool Free(Pawn p,CompProperties_ClassEffect props)=>props!=null && props.heat>0f && HeatRoom(p,props.heat);
  public static string CostText(Pawn p,AbilityDef def){
   int b=BloodOf(def);
   if(b>0){var g=Hemogen(p);return g!=null?"消耗：血原质 "+(b*HemogenPerPct*100f).ToString("0")+"（现有 "+(g.Value*100f).ToString("0")+"）":"消耗：失血 "+b+"%（现在失血 "+BloodLoss(p).ToStringPercent()+"，上限 "+Cap(p).ToStringPercent()+"）";}
   if(Oath(def))return "消耗：誓言之力 1（今天还剩 "+OathLeft(p)+" / "+OathMax(Adventurer.Get(p))+"）";
   float h=HeatOf(def);if(h>0f)return p.HasPsylink?"有灵能器：不耗斗志，改加 "+h.ToString("0")+" 点神经热（神经热放不下时照常耗斗志）":null;
   return null;
  }
  public static string PickBlock(Hediff_Adventurer h,LP_SubclassDef s){
   if(s?.defName==Hemomancy && h?.pawn!=null && (Items.IsLich(h.pawn) || Items.Undead(h.pawn)))return "身体里没有血，不能选血咒学派";
   return null;
  }

  // ---------- 施放之后（Ability_Class.Activate） ----------
  public static void AfterCast(Ability_Class a){
   var p=a.pawn;if(p==null)return;
   float heat=a.Props?.heat??0f;if(heat>0f && HeatRoom(p,heat))p.psychicEntropy.TryAddEntropy(heat,p,true,true); // 施放前后神经热没变：放得下 = 这次没付斗志
   if(Users.Count==0)return;int f=Flags(p);if(f==0)return;
   if((f & FWarMagic)!=0 && EKSpell(a.def) && p.Spawned){
    int n=(f & FBladestorm)!=0?2:1;
    Fiends.Defer(()=>{for(int i=0;i<n;i++){var t=AdjacentFoe(p);if(t==null || p.Dead || p.Downed)break;ClassMelee.Strike(p,t);if(i==0)Items.Mote(p,"战争魔法",EKColor);}});
   }
   if((f & FRite)!=0 && BloodOf(a.def)>0)Items.Give(p,"LP_HM_Ward",300);
  }

  // ---------- 主动能力 ----------
  static readonly HashSet<string> Known=new HashSet<string>{"S2Bond","S2Blink","S2Shove","S2PsiField","S2TeleStorm","S2BloodSpear","S2BloodBind","S2FleshShape","S2BloodBoil","S2FleshPrison","S2BloodRite",
   "S2Defender","S2CalmMind","S2Spike","S2Dominion","S2CQPresence","S2Reconcile","S2Fiend"};
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(!Known.Contains(e.effect))return null;var t=target.Thing as Pawn;
   switch(e.effect){
    case "S2Bond":return BondCannot(caster);
    case "S2Blink":{var map=caster.Map;var c=target.Cell;if(map==null || !c.IsValid || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.GetFirstPawn(map)!=null)return "需要一个空着的、能站立的格子。";
     float r=e.range>0f?e.range:10f;if(caster.Position.DistanceTo(c)>r)return "超出距离（"+r.ToString("0")+" 格）。";return null;}
    case "S2Shove":case "S2BloodSpear":case "S2BloodBind":case "S2FleshPrison":case "S2Spike":case "S2Dominion":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";
     if(e.effect=="S2Spike" && t.RaceProps.IsMechanoid)return "机械体没有心灵。";
     if(e.effect=="S2Dominion"){if(!t.RaceProps.Humanlike)return "只能支配人形。";if(Boss(t))return "剧情首领和传奇单位不会被支配。";if(t.IsPrisoner)return "已经是囚犯了。";}
     return null;
    case "S2FleshShape":case "S2BloodRite":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(t.HostileTo(caster))return "只能对友军使用。";if(Bloodless(t))return "目标身体里没有血肉可以塑形。";return null;
    case "S2CalmMind":if(t==null || t.Dead)return "需要选择一个殖民者。";if(t.HostileTo(caster))return "只能对友军使用。";if(!t.InMentalState)return "目标没有精神崩溃。";return null;
    case "S2BloodBoil":{var map=caster.Map;var c=target.Cell;if(map==null || !c.IsValid || !c.InBounds(map))return "需要选择一个位置。";return null;}
    case "S2Defender":return DefenderCannot(caster);
   }
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(!Known.Contains(e.effect))return false;
   var t=target.Thing as Pawn;float s=ClassEffects.Strength(p,e),pw=ClassEffects.Power(p,e);int dur=ClassEffects.Duration(p,e);float rad=ClassEffects.Radius(p,e);
   switch(e.effect){
    case "S2Bond":Bond(p);break;
    case "S2Blink":if(Invalid(p,e,target)==null){FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,1.5f,EKColor);Races124.Blink(p,target.Cell);FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,1.5f,EKColor);}break;
    case "S2Shove":if(Hostile(p,t))Shove(p,t,dur);break;
    case "S2PsiField":{int n=0;foreach(var x in Near(p,p.Position,rad).ToList())if(Ally(p,x)){Items.Give(x,"LP_PW_FieldBuff",dur);n++;}Items.Mote(p,"护卫念场",PsiColor);break;}
    case "S2TeleStorm":TeleStorm(p,rad,pw);break;
    case "S2BloodSpear":if(Hostile(p,t)){t.TakeDamage(new DamageInfo(DamageDefOf.Stab,pw,.3f,-1f,p));Items.Mote(t,"血矛",BloodColor);}break;
    case "S2BloodBind":if(Hostile(p,t))BloodBind(p,t,dur);break;
    case "S2FleshShape":if(t!=null && Friendly(p,t))FleshShape(p,t,pw);break;
    case "S2BloodBoil":BloodBoil(p,target.Cell,rad,pw,dur);break;
    case "S2FleshPrison":if(Hostile(p,t))FleshPrison(p,t,dur);break;
    case "S2BloodRite":if(t!=null && Friendly(p,t))Items.Mote(t,BloodRite(t),BloodColor);break;
    case "S2Defender":Defender(p);break;
    case "S2CalmMind":if(t!=null && t.InMentalState)CalmMind(p,t);break;
    case "S2Spike":if(Hostile(p,t)){t.TakeDamage(new DamageInfo(Warlock.PsychicDamage,pw,1f,-1f,p));Items.Mote(t,"灵能尖刺",AberrantColor);}break;
    case "S2Dominion":if(Hostile(p,t))Dominion(p,t);break;
    case "S2CQPresence":Presence(p,rad,dur);break;
    case "S2Reconcile":Reconcile(p);break;
    case "S2Fiend":{int i=Array.IndexOf(FiendParts,e.hediff);if(i<0)break;var r=FiendToggle(p,i);if(r!="on" && r!="off" && p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(r,p,MessageTypeDefOf.RejectInput,false);break;}
   }
   return true;
  }
  public static bool Shove(Pawn p,Pawn t,int stun){
   if(Roll>=Chance(t.RaceProps.IsMechanoid?.4f:.7f,p,t)){Items.Mote(t,"抵抗了念动推送",PsiColor);return false;}
   if(t.Spawned){var dest=DNDPush.Destination(t.Map,t.Position,(t.Position-p.Position).ToVector3(),3);DNDPush.Move(t,dest);}
   t.stances?.stunner?.StunFor(stun,p);Items.Mote(t,"念动推送",PsiColor);return true;
  }
  public static int TeleStorm(Pawn p,float radius,float amount){
   int n=0;foreach(var t in Near(p,p.Position,radius).ToList())if(Hostile(p,t)){t.stances?.stunner?.StunFor(180,p);t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,amount,.2f,-1f,p));n++;}
   if(p.Map!=null)FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,4f,PsiColor);Items.Mote(p,"念动风暴",PsiColor);return n;
  }
  public static bool BloodBind(Pawn p,Pawn t,int ticks){
   if(Bloodless(t)){Items.Mote(t,"没有血，无效",BloodColor);return false;}
   if(Roll>=Chance(.65f,p,t)){Items.Mote(t,"挣脱了血缚",BloodColor);return false;}
   t.stances?.stunner?.StunFor(ticks,p);Items.Mote(t,"血缚",BloodColor);return true;
  }
  public static float FleshShape(Pawn p,Pawn t,float amount){
   if(t==p){AddBlood(p,-(.2f+BloodOf(DefCache<AbilityDef>.Get("LP_HM_FleshShaping"))/100f));Items.Mote(p,"血肉塑形：止血",BloodColor);return 0f;}
   foreach(var inj in t.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>x.Bleeding && !x.IsTended()).ToList())inj.Tended(1f,1f);
   float healed=ClassMelee.Heal(t,amount);Items.Mote(t,"血肉塑形 "+healed.ToString("0"),BloodColor);return healed;
  }
  public static int BloodBoil(Pawn p,IntVec3 c,float radius,float perTick,int ticks){
   int n=0;foreach(var t in Near(p,c,radius).ToList()){if(!Hostile(p,t) || Bloodless(t))continue;var h=Items.Give(t,"LP_HM_BloodBoil",ticks,1f) as Hediff_BloodBoil;if(h!=null){h.caster=p;h.amount=perTick;}n++;}
   if(p.Map!=null)FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),p.Map,3f,BloodColor);Items.Mote(p,"血沸",BloodColor);return n;
  }
  public static bool FleshPrison(Pawn p,Pawn t,int ticks){
   if(Bloodless(t) || t.BodySize>2f){Items.Mote(t,"无效",BloodColor);return false;}
   if(Roll>=Chance(.6f,p,t)){Items.Mote(t,"挣脱了血肉禁锢",BloodColor);return false;}
   t.stances?.stunner?.StunFor(ticks,p,false);Items.Mote(t,"血肉禁锢",BloodColor);return true;
  }
  // 血之秘仪：自动选最需要的一项（先长回缺失的部位，再治好疾病、成瘾和疤痕，最后年龄 −10 岁，不低于 18 岁）。
  public static string BloodRite(Pawn t){
   var miss=Necro.Missing(t).FirstOrDefault();if(miss!=null){t.health.RestorePart(miss);return "血肉重生："+miss.LabelCap;}
   var cure=t.health.hediffSet.hediffs.Where(h=>h is Hediff_Addiction || h.def.chronic || h.def.makesSickThought || h is Hediff_Injury i && i.IsPermanent()).ToList();
   if(cure.Count>0){foreach(var h in cure)t.health.RemoveHediff(h);return "万病消除（"+cure.Count+" 项）";}
   long year=GenDate.TicksPerYear,min=18*year;var age=t.ageTracker;
   if(age!=null && age.AgeBiologicalTicks>min+year){age.AgeBiologicalTicks=Math.Max(min,age.AgeBiologicalTicks-10*year);return "重塑青春";}
   return "血之秘仪";
  }
  public static bool CalmMind(Pawn p,Pawn t){
   if(Roll>=CalmChance){Items.Mote(t,"安抚失败",AberrantColor);return false;}
   t.MentalState?.RecoverFromState();Items.Mote(t,"安抚心灵",AberrantColor);return true;
  }
  public static int Presence(Pawn p,float radius,int ticks){
   int n=0;
   foreach(var t in Near(p,p.Position,radius).ToList()){
    if(!Hostile(p,t) || t.Downed || t.RaceProps.IsMechanoid)continue;
    if(Roll>=Chance(.6f,p,t)){Items.Mote(t,"抵抗！",ConquestColor);continue;}
    Items.Give(t,"LP_Shaken",ticks,1f);ClassSubclass.Fear(t,ticks,"征服者之威");n++;
   }
   Items.Mote(p,"征服者之威",ConquestColor);return n;
  }
  public static int Reconcile(Pawn p){
   var map=p.Map;if(map==null)return 0;int n=0;var factions=new HashSet<Faction>();
   foreach(var t in map.mapPawns.AllPawnsSpawned.ToList()){
    if(!Hostile(p,t) || t.Downed || !t.RaceProps.Humanlike || Boss(t) || t.IsPrisoner || t.InMentalState)continue;
    if(Roll>=ReconcileChance)continue;
    t.mindState.enemyTarget=null;if(t.equipment?.Primary!=null)t.equipment.TryDropEquipment(t.equipment.Primary,out _,t.Position);
    if(t.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"和解",true,true)){n++;if(t.Faction!=null && !t.Faction.IsPlayer)factions.Add(t.Faction);Items.Mote(t,"放下武器",RedeemColor);}
   }
   if(p.Faction!=null)foreach(var f in factions)if(f.CanChangeGoodwillFor(p.Faction,15))f.TryAffectGoodwillWith(p.Faction,15,true,false);
   Items.Mote(p,"和解",RedeemColor);
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"的和解："+n+" 个敌人放下武器离开"+(factions.Count>0?"，"+string.Join("、",factions.Select(f=>f.Name))+"好感 +15":"")+"。",p,MessageTypeDefOf.PositiveEvent,false);
   return n;
  }

  // ---------- 异变心灵·心灵支配 ----------
  public static bool Dominion(Pawn p,Pawn t){
   if(t==null || t.Dead || t.mindState==null)return false;
   if(Roll>=Chance(DominateChance,p,t)){Items.Mote(t,"抵抗了支配",AberrantColor);return false;}
   var orig=t.Faction;
   if(t.InMentalState)t.MentalState.RecoverFromState();
   t.SetFaction(p.Faction);t.mindState.enemyTarget=null;
   var gc=Current.Game?.GetComponent<GameComponent_Subclass2>();gc?.Add(t,orig,Now+DominateTicks);
   Items.Mote(t,"被支配",AberrantColor);
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(t.LabelShort+"被"+p.LabelShort+"的心灵支配，暂时加入 1 天。",t,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  public static void DominionEnd(Pawn t,Faction orig){
   if(t==null || t.Dead || t.Destroyed)return;
   if(Roll<DominateStay){Items.Mote(t,"自愿留下",AberrantColor);if(t.Faction!=null && t.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(t.LabelShort+"在支配结束后选择留下。",t,MessageTypeDefOf.PositiveEvent,false);return;}
   if(t.Faction==orig)return;
   if(t.Drafted)t.drafter.Drafted=false;
   t.SetFaction(orig==null || orig.defeated?null:orig);
   if(t.Spawned){t.stances?.stunner?.StunFor(600,null,false);t.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.PanicFlee,"支配结束",true,true);}
   Items.Mote(t,"支配结束",AberrantColor);
   if(!Adventurer.TestSuppressed)Messages.Message(t.LabelShort+"挣脱了心灵支配，变回敌人。",t,MessageTypeDefOf.NegativeEvent,false);
  }

  // ---------- 征服 ----------
  public static int ConquestAura(Pawn p){
   int n=0;float dmg=8f*Divine(p);
   foreach(var t in Near(p,p.Position,5f).ToList()){
    if(!Hostile(p,t) || t.Downed || !(Items.Has(t,"LP_Shaken") || ClassMartial2.Fleeing(t)))continue;
    Items.Give(t,"LP_CQ_Pinned",300);t.TakeDamage(new DamageInfo(Warlock.PsychicDamage,dmg,1f,-1f,p));n++;
   }
   return n;
  }
  public static int Yield(Pawn p,bool force=false){
   int n=0;
   foreach(var t in Near(p,p.Position,10f).ToList()){
    if(!Hostile(p,t) || t.Downed || !t.RaceProps.Humanlike || Boss(t) || Items.Has(t,"LP_CQ_Yielded") || t.health.summaryHealth.SummaryHealthPercent>=.5f)continue;
    if(!force && Roll>=YieldChance)continue;
    if(t.equipment?.Primary!=null)t.equipment.TryDropEquipment(t.equipment.Primary,out _,t.Position);
    Items.Give(t,"LP_CQ_Yielded",YieldTicks);Items.Mote(t,"降服",ConquestColor);n++;
   }
   return n;
  }

  // ---------- 战铸师·钢铁卫士 ----------
  public static int DefenderMax(Pawn p)=>ClassSystem.Knows(p,"LP_BS_MasterSmith")?2:1;
  public static List<Pawn> Live(SubclassData d){if(d==null)return new List<Pawn>();d.defenders.RemoveAll(x=>x==null || x.Destroyed || x.Dead);return d.defenders;}
  static int Stock(Map map,ThingDef def)=>map.listerThings.ThingsOfDef(def).Where(x=>x.Spawned && !x.IsForbidden(Faction.OfPlayer)).Sum(x=>x.stackCount);
  static void Consume(Map map,ThingDef def,int n){foreach(var x in map.listerThings.ThingsOfDef(def).Where(x=>x.Spawned && !x.IsForbidden(Faction.OfPlayer)).ToList()){if(n<=0)break;int k=Math.Min(n,x.stackCount);x.SplitOff(k).Destroy();n-=k;}}
  static (int steel,int comp) DefenderCost(Pawn p){var d=ClassSubclass.Data(p);d.ticks.TryGetValue("defDied",out int t);bool repair=t>0 && Now-t<RepairWindow && Now>=t;return repair?(RepairSteel,0):(DefenderSteel,DefenderComponents);}
  public static string DefenderCannot(Pawn p){
   var d=ClassSubclass.Data(p);if(d==null || p.Map==null)return "需要在地图上。";
   if(Live(d).Count>=DefenderMax(p))return null; // 已满：改为召回
   if(p.Faction==null || !p.Faction.IsPlayer)return null;
   var (steel,comp)=DefenderCost(p);
   if(Stock(p.Map,ThingDefOf.Steel)<steel || comp>0 && Stock(p.Map,ThingDefOf.ComponentIndustrial)<comp)return "材料不够：要 "+steel+" 钢"+(comp>0?" + "+comp+" 零部件":"")+"（地图上没被禁止的）。";
   return null;
  }
  // 造一台钢铁卫士（已满时把卫士召回到身边）。
  public static Pawn Defender(Pawn p){
   var d=ClassSubclass.Data(p);var map=p.Map;if(d==null || map==null)return null;var live=Live(d);
   if(live.Count>=DefenderMax(p)){foreach(var c in live)if(c.Spawned && c.Map==map && !c.Downed){var cell=CellFinder.RandomClosewalkCellNear(p.Position,map,2);if(cell.IsValid)Races124.Blink(c,cell);}Items.Mote(p,"召回钢铁卫士",SmithColor);return live.FirstOrDefault();}
   if(p.Faction!=null && p.Faction.IsPlayer){var (steel,comp)=DefenderCost(p);Consume(map,ThingDefOf.Steel,steel);if(comp>0)Consume(map,ThingDefOf.ComponentIndustrial,comp);}
   d.ticks.Remove("defDied");
   var kind=DefCache<PawnKindDef>.Get("LP_SteelDefenderKind");if(kind==null)return null;
   var w=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,p.Faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:1,fixedChronologicalAge:1));
   GenSpawn.Spawn(w,CellFinder.RandomClosewalkCellNear(p.Position,map,2),map);
   if(w.training!=null && w.Faction!=null && w.Faction==p.Faction){
    foreach(var td in new[]{TrainableDefOf.Tameness,TrainableDefOf.Obedience,TrainableDefOf.Release})if(td!=null && w.training.CanAssignToTrain(td).Accepted)w.training.Train(td,null,true);
    if(w.playerSettings!=null)w.playerSettings.Master=p;
   }
   w.Name=new NameSingle(p.LabelShort+"的钢铁卫士");
   d.defenders.Add(w);Defenders.Add(w);Items.Give(w,"LP_DefenderCore");
   FleckMaker.ThrowDustPuffThick(w.DrawPos,map,2f,SmithColor);Items.Mote(w,"钢铁卫士",SmithColor);
   return w;
  }
  public static Pawn MasterOf(Pawn defender)=>defender?.playerSettings?.Master;
  static void DefenderTick(Pawn p,Hediff_Adventurer h,SubclassData d,int f){
   var live=Live(d);if(live.Count==0)return;int lv=ClassSystem.Level(h,artificer.Def);
   foreach(var c in live){
    Defenders.Add(c);if(!c.Spawned)continue;
    if(c.needs?.food!=null)c.needs.food.CurLevelPercentage=1f;if(c.needs?.rest!=null)c.needs.rest.CurLevel=c.needs.rest.MaxLevel;
    if(c.training!=null && (f & FImproved)!=0){var haul=DefCache<TrainableDef>.Get("Haul");if(haul!=null && !c.training.HasLearned(haul) && c.training.CanAssignToTrain(haul).Accepted)c.training.Train(haul,null,true);}
    Passive(c,"LP_DefenderImproved",(f & FImproved)!=0);
    if(c.Downed && (f & FMasterSmith)!=0 && Ready(d,"rebuild",GenDate.TicksPerDay)){Use(d,"rebuild");ClassMelee.Heal(c,ClassMelee.InjurySum(c)*.5f+1f);Items.Mote(c,"自动重构",SmithColor);}
    if(!c.Downed && Now-(c.mindState?.lastHarmTick??0)>600)ClassMelee.Heal(c,2f);
    if(p.Drafted && !c.Downed && c.Map==p.Map)HediffComp_Conjured.Engage(c);
   }
  }
  public static float DefenderFactor(Pawn defender){var m=MasterOf(defender);if(m==null)return 1f;int lv=ClassSystem.Level(m,artificer.Def);float f=1f/(1f+DefenderPer*lv);if(S(m,FImproved))f*=ImprovedFactor;return f;}

  // ---------- 伤害（Subclass2Damage） ----------
  public static bool Busy;
  public static float DamageFactor(Pawn v,DamageInfo d){
   float g=1f;var a=d.Instigator as Pawn;bool hostile=a!=null && a!=v && a.HostileTo(v);bool melee=hostile && ClassMartial2.Melee(d);int fa=Flags(a),fv=Flags(v);
   // —— 攻击方 ——
   if(a!=null && hostile && !Busy){
    if((fa & FEldritchStrike)!=0 && melee)Items.Give(v,"LP_EK_SpellVuln",600);
    if(melee){
     var th=Items.Hediff("LP_EK_ThunderReady");var hh=th==null?null:a.health.hediffSet.GetFirstHediffOfDef(th);
     if(hh!=null){float amt=12f*hh.Severity;a.health.RemoveHediff(hh);Fiends.Defer(()=>Extra(a,v,DamageDefOf.Blunt,amt,"雷鸣斩",()=>{if(v.Spawned && !v.Dead && v.BodySize<=2f){var dest=DNDPush.Destination(v.Map,v.Position,(v.Position-a.Position).ToVector3(),1);DNDPush.Move(v,dest);}}));}
     var bs=Items.Hediff("LP_EK_BladestormBuff");var bh=bs==null?null:a.health.hediffSet.GetFirstHediffOfDef(bs);
     if(bh!=null && a.Spawned){float amt=6f*bh.Severity;var other=Near(a,v.Position,3f).FirstOrDefault(x=>x!=v && Hostile(a,x) && !x.Downed);if(other!=null)Fiends.Defer(()=>Extra(a,other,Warlock.ForceDamage,amt,"法刃风暴",null));}
     var ps=Items.Hediff("LP_PW_StrikeBuff");var ph=ps==null?null:a.health.hediffSet.GetFirstHediffOfDef(ps);
     if(ph!=null){float amt=6f*ph.Severity;Fiends.Defer(()=>Extra(a,v,Warlock.PsychicDamage,amt,"灵能打击",null));}
     if(Items.Has(a,"LP_AM_Tentacles") && Roll<TentacleChance){v.stances?.stunner?.StunFor(120,a);Items.Mote(v,"被触手缠住",AberrantColor);}
     if((fa & FCQScorn)!=0){if(Items.Has(v,"LP_Shaken"))g*=ScornFactor;else if(Roll<ScornChance){Items.Give(v,"LP_Shaken",300,1f);Items.Mote(v,"威慑斩",ConquestColor);}}
    }
    if((fa & FBattleReady)!=0 && d.Weapon!=null && d.Weapon.IsWeapon)g*=WeaponFactor;
    var jm=(fa & FJolt)!=0?a:Defenders.Contains(a)?MasterOf(a):null;
    if(jm!=null && S(jm,FJolt)){var sd=ClassSubclass.Data(jm);if(Ready(sd,"jolt",600)){Use(sd,"jolt");var master=jm;Fiends.Defer(()=>Jolt(master,a,v));}}
   }
   // —— 受击方 ——
   if(Items.Has(v,"LP_EK_SpellVuln") && IsSpell(d.Def))g*=SpellVuln;
   if(Items.Has(v,"LP_EK_Absorb") && ClassSubclass.Element(d.Def)>=0)g*=AbsorbFactor;
   if(Items.Has(v,"LP_PW_FieldBuff") && ForceDragon.IsRanged(d))g*=GuardFieldFactor;
   if(Items.Has(v,"LP_HM_Ward"))g*=.5f;
   bool holy=Monsters.Holy(d);
   if(Items.Has(v,"LP_CQ_Invincible") && !holy)g*=InvincibleFactor;
   if(fv!=0){
    if((fv & FPsiField)!=0){if(HeatRoom(v,PsiHeatPerHit)){g*=PsiFieldFactor;v.psychicEntropy.TryAddEntropy(PsiHeatPerHit,v,true,false);}else if(!v.HasPsylink)g*=PsiFieldNoLink;}
    if((fv & FAMDefense)!=0 && ClassSubclass.Psychic(d.Def))g*=.5f;
    if((fv & FRDAngel)!=0){if(!holy && !ClassSubclass.Psychic(d.Def))g*=AngelFactor;if(melee && !Busy){var att=a;float amt=4f*Divine(v);Fiends.Defer(()=>Extra(v,att,PaladinCore.Radiant,amt,"救赎天使",null));}}
    if((fv & FBattleReady)!=0 && melee && v.Spawned){var dd=ClassSubclass.Data(v);if(dd!=null)foreach(var c in dd.defenders)if(c!=null && c.Spawned && !c.Downed && c.Map==v.Map && c.Position.AdjacentTo8WayOrInside(v.Position)){if(Roll<GuardChance){Items.Mote(v,"卫士护主",SmithColor);return 0f;}break;}}
   }
   if(Defenders.Count>0){
    if(Defenders.Contains(v))g*=DefenderFactor(v);
    else if(v.Spawned && v.Faction!=null)foreach(var c in Defenders)if(c!=null && c.Spawned && !c.Downed && c.Map==v.Map && c.Faction==v.Faction && c.Position.InHorDistOf(v.Position,2f) && S(MasterOf(c),FMasterSmith)){g*=SmithAura;break;}
   }
   if(hostile && !Busy && v.Spawned){
    if(Guardians.Count>0 && !S(v,FRDGuardian)){
     foreach(var gp in Guardians)if(gp!=null && gp!=v && gp.Spawned && gp.Drafted && !gp.Downed && gp.Map==v.Map && gp.Faction==v.Faction && gp.Position.InHorDistOf(v.Position,5f)){
      var share=new DamageInfo(d.Def,d.Amount*g*GuardianShare,d.ArmorPenetrationInt,d.Angle,a,null,d.Weapon);var guard=gp;Fiends.Defer(()=>{if(!guard.Dead){Busy=true;try{guard.TakeDamage(share);}finally{Busy=false;}}});
      Items.Mote(v,"守护者灵光",RedeemColor);return 0f;
     }
    }
    if(melee && Rebukers.Count>0){
     foreach(var r in Rebukers.ToList()){
      if(r==null || r.Dead || !r.Spawned || r.Map!=v.Map || r.Faction!=v.Faction || !r.Position.InHorDistOf(v.Position,15f))continue;
      var rh=r.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_RD_Rebuke"));if(rh==null){Rebukers.Remove(r);continue;}
      float amt=Mathf.Min(d.Amount*g,rh.Severity);r.health.RemoveHediff(rh);Rebukers.Remove(r);var att=a;Fiends.Defer(()=>Extra(r,att,PaladinCore.Radiant,amt,"斥责暴力",null));break;
     }
    }
   }
   return g;
  }
  static void Extra(Pawn a,Pawn t,DamageDef def,float amount,string label,Action after){
   if(t==null || t.Dead || !t.Spawned || amount<=0f)return;Busy=true;
   try{t.TakeDamage(new DamageInfo(def,amount,.2f,-1f,a));}finally{Busy=false;}
   Items.Mote(t,label,Color.white);after?.Invoke();
  }
  // 奥术震击：15 格内有伤势超过 30% 的友军就治疗最需要的一个，否则对目标加一次力场伤害。
  static void Jolt(Pawn master,Pawn src,Pawn t){
   float amount=8f+ClassSystem.Level(master,artificer.Def);
   var ally=src.Spawned?Near(src,src.Position,15f).Where(x=>Ally(src,x) && !x.Downed && x.health.summaryHealth.SummaryHealthPercent<.7f).OrderBy(x=>x.health.summaryHealth.SummaryHealthPercent).FirstOrDefault():null;
   if(ally!=null){ClassMelee.Heal(ally,amount);Items.Mote(ally,"奥术震击·治疗",SmithColor);return;}
   Extra(src,t,Warlock.ForceDamage,amount,"奥术震击",null);
  }
  // 击杀：汲血；钢铁卫士留下残骸（20 钢）。
  public static void Killed(Pawn victim,Pawn killer){
   if(Defenders.Remove(victim)){
    var m=MasterOf(victim);var d=ClassSubclass.Data(m);if(d!=null){d.defenders.Remove(victim);d.ticks["defDied"]=Math.Max(1,Now);}
    var map=victim.MapHeld;var pos=victim.PositionHeld;
    Fiends.Defer(()=>{var corpse=victim.Corpse;if(corpse!=null && !corpse.Destroyed)corpse.Destroy();if(map!=null){var steel=ThingMaker.MakeThing(ThingDefOf.Steel);steel.stackCount=RepairSteel;GenPlace.TryPlaceThing(steel,pos,map,ThingPlaceMode.Near);}});
    if(m!=null && m.Faction!=null && m.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(m.LabelShort+"的钢铁卫士被打烂了：1 天内用 20 钢就能重造。",m,MessageTypeDefOf.NegativeEvent,false);
   }
   if(killer==null || killer.Dead || !S(killer,FSiphon) || Bloodless(victim) || !victim.HostileTo(killer))return;
   var g=Hemogen(killer);if(g!=null)g.Value+=.1f;else AddBlood(killer,-.1f);Items.Mote(killer,"汲血",BloodColor);
  }
  public static void Reset(){Users.Clear();Defenders.Clear();Rebukers.Clear();Guardians.Clear();Fliers.Clear();Busy=false;}

  // ---------- 龙魂（术士） ----------
  public const int DragonCon=2,PresenceRadius=10;
  // 属性加值（Hediff_Adventurer.Eff）：龙血 体质 +2；恶鬼之眼 魅力 +2（强化后 +4）。
  public static int ScoreBonus(Hediff_Adventurer h,int a){
   var p=h?.pawn;if(p==null || Users.Count==0)return 0;int f=Flags(p);if(f==0)return 0;int b=0;
   if(a==Adventurer.Con && (f & FDragonBlood)!=0)b+=DragonCon;
   if(a==Adventurer.Cha && (f & FFiend)!=0){var e=p.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_FB_EyesBuff"));if(e!=null)b+=e.Severity>=1.5f?4:2;}
   return b;
  }
  // 龙威：半径 10 内看得见你的敌人做一次和真龙一样的骇人威仪判定（感知豁免对你的魅力难度；失败惊惧 1 分钟，人形还可能恐慌逃跑），之后 1 天免疫。
  public static int DragonPresence(Pawn p){
   var map=p.Map;if(map==null)return 0;var mc=MapComp<MapComponent_Legendary>.Get(map);int n=0;
   foreach(var t in Near(p,p.Position,PresenceRadius).ToList()){
    if(!Hostile(p,t) || t.Downed || mc!=null && mc.ImmuneTo(p,t) || !GenSight.LineOfSight(p.Position,t.Position,map,true))continue;
    if(Legendary.Presence(p,t,mc))n++;else mc?.Immune(p,t,Legendary.FearImmuneTicks);
   }
   return n;
  }

  // ---------- 恶鬼之血（术士） ----------
  // 身体强化：眼睛（魅力）、皮肤（变红、伤口愈合 ×1.5 并持续回复）、舌头（心灵敏感度）。每个开着的强化让术士法力上限 −2；
  // 8 级起能同时开两个并且强化升级；12 级选一个永久固化（不占名额、不扣法力）。
  public static readonly string[] FiendParts={"LP_FB_EyesBuff","LP_FB_SkinBuff","LP_FB_TongueBuff"};
  static readonly string[] FiendLabels={"恶鬼之眼","恶鬼之皮","恶鬼之舌"};
  public const int FiendPenalty=2;
  static readonly DefRef<LP_ClassDef> sorcerer=new DefRef<LP_ClassDef>("LP_Class_Sorcerer");
  public static int Fixed(Pawn p)=>ClassSubclass.Has(p,FiendBlood) && ClassSystem.Knows(p,"LP_FB_Bound")?ClassSubclass.Choice(p,"fbFixed"):-1;
  public static int FiendSlots(Pawn p)=>ClassSystem.Knows(p,"LP_FB_Awaken")?2:1;
  public static int PaidActive(Pawn p){int fx=Fixed(p),n=0;for(int i=0;i<3;i++)if(i!=fx && Items.Has(p,FiendParts[i]))n++;return n;}
  public static int ResourcePenalty(Hediff_Adventurer h,LP_ClassDef c){
   if(c==null || c!=sorcerer.Def || h?.pawn==null || Users.Count==0 || !S(h.pawn,FFiend))return 0;return PaidActive(h.pawn)*FiendPenalty;
  }
  public static string FiendToggle(Pawn p,int part){
   var name=FiendParts[part];int fx=Fixed(p);
   if(part==fx)return "已经永久固化";
   if(Items.Has(p,name)){Items.Remove(p,name);Items.Mote(p,"收起"+FiendLabels[part],BloodColor);ClampMana(p);return "off";}
   if(PaidActive(p)>=FiendSlots(p))return "最多同时维持 "+FiendSlots(p)+" 个强化（先收起一个）";
   Items.Give(p,name,-1,ClassSystem.Knows(p,"LP_FB_Awaken")?2f:1f);Items.Mote(p,FiendLabels[part],BloodColor);ClampMana(p);return "on";
  }
  static void ClampMana(Pawn p){var h=Adventurer.Get(p);var c=sorcerer.Def;if(h==null || c==null)return;int max=ClassSystem.MaxResource(h,c);float cur=ClassSystem.Resource(h,c);if(cur>max)h.classes.res[ClassSystem.PoolKey(c)]=Mathf.Max(0,max);}
  static void FiendTick(Pawn p,int f){
   float sev=(f & FFiendAwaken)!=0?2f:1f;int fx=Fixed(p);
   for(int i=0;i<3;i++){var h=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff(FiendParts[i]));if(i==fx && h==null)h=Items.Give(p,FiendParts[i],-1,sev);if(h!=null && h.Severity!=sev)h.Severity=sev;}
   ClampMana(p);
   if(Items.Has(p,"LP_FB_SkinBuff") && !p.Downed)ClassMelee.Heal(p,sev*.5f);
  }
 }

 // 恶鬼之皮：皮肤变红（记下原来的覆盖色，收起时还原）。
 public class Hediff_FiendSkin : HediffWithComps {
  Color old;bool had,hasOld;
  public static readonly Color Red=new Color(.72f,.22f,.2f);
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);var st=pawn?.story;if(st==null)return;had=true;hasOld=st.skinColorOverride.HasValue;old=st.skinColorOverride??Color.white;st.skinColorOverride=Red;pawn.Drawer?.renderer?.SetAllGraphicsDirty();}
  public override void PostRemoved(){base.PostRemoved();var st=pawn?.story;if(st==null || !had)return;st.skinColorOverride=hasOld?(Color?)old:null;pawn.Drawer?.renderer?.SetAllGraphicsDirty();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref had,"lpSkinHad");Scribe_Values.Look(ref hasOld,"lpSkinHasOld");Scribe_Values.Look(ref old,"lpSkinOld",Color.white);}
 }
 // 血沸：每秒一次伤害。
 public class Hediff_BloodBoil : HediffWithComps {
  public Pawn caster;public float amount=4f;int acc;
  public override void TickInterval(int delta){
   base.TickInterval(delta);acc+=delta;if(acc<60)return;acc-=60;
   if(pawn==null || pawn.Dead || !pawn.Spawned)return;
   Subclass2.Busy=true;try{pawn.TakeDamage(new DamageInfo(DamageDefOf.Burn,amount,1f,-1f,caster));}finally{Subclass2.Busy=false;}
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref caster,"lpBoilCaster");Scribe_Values.Look(ref amount,"lpBoilAmount",4f);Scribe_Values.Look(ref acc,"lpBoilAcc");}
 }
 // 斥责暴力：挂着的人登记到 Rebukers（伤害挂钩只看这个集合）。
 public class Hediff_Rebuke : HediffWithComps {
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);if(pawn!=null)Subclass2.Rebukers.Add(pawn);}
  public override void PostRemoved(){base.PostRemoved();Subclass2.Rebukers.Remove(pawn);}
  public override void ExposeData(){base.ExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit && pawn!=null)Subclass2.Rebukers.Add(pawn);}
 }
 // 心灵支配的到期表；钢铁卫士集合在读档时重建。
 public class GameComponent_Subclass2 : GameComponent {
  List<Pawn> pawns=new List<Pawn>();List<Faction> factions=new List<Faction>();List<int> ends=new List<int>();
  public GameComponent_Subclass2(Game game){}
  public void Add(Pawn p,Faction f,int end){int i=pawns.IndexOf(p);if(i>=0){ends[i]=end;return;}pawns.Add(p);factions.Add(f);ends.Add(end);}
  public int Count=>pawns.Count;
  public void ExpireNow(){for(int i=0;i<ends.Count;i++)ends[i]=0;Check();}
  void Check(){
   int now=Find.TickManager.TicksGame;
   for(int i=pawns.Count-1;i>=0;i--){
    if(pawns[i]==null || pawns[i].Destroyed || pawns[i].Dead){pawns.RemoveAt(i);factions.RemoveAt(i);ends.RemoveAt(i);continue;}
    if(now<ends[i])continue;var p=pawns[i];var f=factions[i];pawns.RemoveAt(i);factions.RemoveAt(i);ends.RemoveAt(i);Subclass2.DominionEnd(p,f);
   }
  }
  public override void FinalizeInit(){
   base.FinalizeInit();Subclass2.Reset();
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawns){if(Items.Has(p,"LP_DefenderCore"))Subclass2.Defenders.Add(p);if(Items.Has(p,"LP_RD_Rebuke"))Subclass2.Rebukers.Add(p);}
  }
  public override void GameComponentTick(){if(pawns.Count>0 && Find.TickManager.TicksGame%250==7)Check();}
  public override void ExposeData(){
   base.ExposeData();Scribe_Collections.Look(ref pawns,"lpDomPawns",LookMode.Reference);Scribe_Collections.Look(ref factions,"lpDomFactions",LookMode.Reference);Scribe_Collections.Look(ref ends,"lpDomEnds",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(pawns==null)pawns=new List<Pawn>();if(factions==null)factions=new List<Faction>();if(ends==null)ends=new List<int>();
    int n=Math.Min(pawns.Count,Math.Min(factions.Count,ends.Count));if(pawns.Count>n)pawns.RemoveRange(n,pawns.Count-n);if(factions.Count>n)factions.RemoveRange(n,factions.Count-n);if(ends.Count>n)ends.RemoveRange(n,ends.Count-n);}
  }
 }

 // ==================== Harmony ====================
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class Subclass2Damage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(Subclass2.Users.Count==0 && Subclass2.Defenders.Count==0)return;
   if(dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   float f=Subclass2.DamageFactor(__instance,dinfo);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
 // 汲血、卫士残骸；救赎之誓：亲手杀死倒地的人或囚犯就背誓。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class Subclass2Kill {
  static void Prefix(Pawn __instance,DamageInfo? dinfo){
   if(Subclass2.Users.Count==0 || __instance.Dead)return;
   if(dinfo?.Instigator is Pawn k && k!=__instance && Subclass2.S(k,Subclass2.FRedemption) && __instance.RaceProps.Humanlike && (__instance.Downed || __instance.IsPrisoner || __instance.IsSlave))Subclass2.Oathbreak(k,"杀死了无力反抗的人");
  }
  static void Postfix(Pawn __instance,DamageInfo? dinfo){
   if(Subclass2.Users.Count==0 && Subclass2.Defenders.Count==0 || !__instance.Dead)return;
   Subclass2.Killed(__instance,dinfo?.Instigator as Pawn);
  }
 }
 // 征服之誓：殖民地释放囚犯时，同一张地图上的征服圣武士背誓。
 [HarmonyPatch]
 public static class Subclass2Release {
  static System.Reflection.MethodBase TargetMethod()=>AccessTools.GetDeclaredMethods(typeof(GenGuest)).FirstOrDefault(m=>m.Name=="PrisonerRelease");
  static bool Prepare()=>TargetMethod()!=null;
  static void Postfix(object[] __args){
   if(Subclass2.Users.Count==0 || __args==null || __args.Length==0 || !(__args[0] is Pawn prisoner))return;var map=prisoner.MapHeld;if(map==null)return;
   foreach(var p in map.mapPawns.FreeColonistsSpawned.ToList())if(Subclass2.S(p,Subclass2.FConquest))Subclass2.Oathbreak(p,"释放了囚犯");
  }
 }
}
