using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.123.0 吟游诗人（第 13 个职业，见 ArtSource/Bard-1.123.0-report.md、Defs/ClassBard.xml）：资源乐韵（上限 2 + 等级 + 魅力调整值，最少 2）。
 // 学院（子职业）3 级选；魔法奥秘是学院定义里的分支（secret1 / secret2 / 学识 secret3），选中的别家法术在 Sync 里补发，施放时扣乐韵、用激昂强度（ClassSystem.CastClassOf / StatFor）。
 // 被动按能力位（Users），每 250 刻在 Tick250 更新；伤害挂钩（BardDamage）、聊天挂钩（BardChat）第一步都是空集合判断。
 public static class Bard {
  static readonly DefRef<LP_ClassDef> cls=new DefRef<LP_ClassDef>("LP_Class_Bard");
  public static LP_ClassDef Class=>cls.Def;
  static readonly DefRef<StatDef> power=new DefRef<StatDef>("LP_BardicPower");
  public static StatDef Power=>power.Def;
  public const string Lore="LP_Sub_Lore",Valor="LP_Sub_Valor",Swords="LP_Sub_Swords",Eloquence="LP_Sub_Eloquence";
  public const float AwakeRegen=.0104f,ActivityRegen=.1f,ChatGain=.2f,StdBase=.55f,DanceBase=.65f,TruceBase=.3f,TrucePerDown=.05f,TruceMax=.9f,PerStrength=.4f,PerWis=.05f,
   FlourishChance=.3f,MasterChance=.6f,CuttingFactor=.75f,BattleMagicFactor=1.25f,KillPer=.35f,KillMax=.7f,InfectRange=10f,LegendRadius=12f,FinaleRange=30f,FinaleFear=.25f,QualityBonus=.3f;
  public const int InspireTicks=30000,UniversalTicks=180000,MockTicks=360,ConfuseInterval=120,FinaleInterval=300,MasterTicks=600,BattleMagicTicks=300,TruceShaken=1800,
   UniversalDailyCap=5,RenownGoodwill=2,RenownCap=50,RenownTicks=900000,LegendExtra=3,FlourishShieldTicks=300,DanceBossTicks=300;
  public static readonly Color Rose=new Color(.85f,.4f,.6f),Lyric=new Color(.6f,.75f,.95f),Valiant=new Color(.9f,.55f,.35f),Silver=new Color(.95f,.8f,.45f),Steel=new Color(.75f,.75f,.9f);
  public static float? TestRoll;
  static float Roll=>TestRoll??Rand.Value;
  static int Now=>Find.TickManager.TicksGame;

  // ---------- 等级与资源 ----------
  public static int Level(Pawn p)=>ClassSystem.Level(p,Class);
  public static int Level(Hediff_Adventurer h)=>ClassSystem.Level(h,Class);
  public static int MaxMelody(int level,int chaMod)=>level<=0?0:Mathf.Max(2,2+level+chaMod);
  public static int MaxMelody(Hediff_Adventurer h){int lv=ClassSystem.PoolLevel(h,Class);return MaxMelody(lv,lv<=0 || h==null?0:h.Mod(Adventurer.Cha));}
  public static int ResourceBonus(Hediff_Adventurer h)=>h?.classes!=null && h.classes.legendary.Count>0 && ClassLegendary.Knows(h,DefCache<AbilityDef>.Get("LP_LegUndyingLegend"))?LegendExtra:0;
  public static float Melody(Pawn p)=>ClassSystem.Resource(p,Class);
  // 恢复（醒着慢、睡觉快；演奏 / 听音乐 / 做艺术品另加；聊天在 BardChat 里加）。
  public static bool Performing(Pawn p){
   var job=p?.CurJob;var j=job?.def;if(j==null)return false;var n=j.defName;
   if(n.Contains("MusicalInstrument") || n.Contains("Play_") || n.Contains("Listen") || n.Contains("Music") || n.Contains("Sing") || n.Contains("Dance"))return true;
   if(j.joyKind!=null && (j.joyKind.defName.Contains("Music") || j.joyKind.defName.Contains("Social")))return true;
   return job.bill?.recipe?.workSkill==SkillDefOf.Artistic;
  }
  public static float RegenAmount(Pawn p){
   if(p==null || p.Dead)return 0f;if(!p.Awake())return ClassSystem.SleepRegen;
   return AwakeRegen+(Performing(p)?ActivityRegen:0f);
  }

  // ---------- 成功率（对抗心智） ----------
  // 标准成功率：基础 + (强度 − 1) × 40%，目标每 1 点感知调整值 −5%，心智抗性直接扣，咒术 +15%；5%～95%。
  public static int WisMod(Pawn t){var h=Adventurer.Get(t);return h==null?0:h.Mod(Adventurer.Wis);}
  public static float Chance(float baseChance,float strength,Pawn t)=>t==null?0f:Mathf.Clamp(baseChance+(strength-1f)*PerStrength-PerWis*WisMod(t)-DNDMind.Resistance(t)+Warlock.SavePenalty(t),.05f,.95f);
  public static bool Succeed(float chance,Pawn t)=>t!=null && !HeroCombat.MindResist(t) && Roll<chance;
  public static bool Mindless(Pawn t)=>t==null || t.RaceProps.IsMechanoid || t.RaceProps.FleshType==FleshTypeDefOf.Insectoid || t.RaceProps.IsAnomalyEntity;
  static bool Boss(Pawn t)=>ClassLegendary.Boss(t) || Legendary.Is(t);
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.HostileTo(a);
  static bool Friendly(Pawn a,Pawn t)=>t!=null && !t.Dead && !t.HostileTo(a);
  static IEnumerable<Pawn> Near(Map map,IntVec3 c,float r)=>map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(map,c,r);

  // ---------- 魔法奥秘 ----------
  // 分支选项的顺序（学院定义里的 options 与这里一一对应）：前 12 个选项是那个职业 7 级及以下的法术，后 4 个是职业 10 级的（只在 secret2）。
  public static readonly string[] Secrets={"LP_Fireball","LP_MistyStep","LP_MirrorImage","LP_Haste","LP_LightningBolt","LP_CureWounds","LP_Bless","LP_LesserRestoration","LP_Barkskin","LP_Entangle","LP_DragonWings","LP_WL_Hex",
   "LP_MassHeal","LP_WallOfStone","LP_BeastCommand","LP_MassSuggestion"};
  public static readonly string[] SecretKeys={"secret1","secret2","secret3"};
  static readonly HashSet<string> SecretSet=new HashSet<string>(Secrets);
  static Dictionary<CompProperties_ClassEffect,AbilityDef> byProps;
  public static AbilityDef SecretOption(int i)=>i>=0 && i<Secrets.Length?DefCache<AbilityDef>.Get(Secrets[i]):null;
  public static IEnumerable<AbilityDef> ChosenSecrets(Hediff_Adventurer h){foreach(var k in SecretKeys){var d=SecretOption(ClassSubclass.Choice(h,k));if(d!=null)yield return d;}}
  public static int Sync(Hediff_Adventurer h){
   var p=h?.pawn;if(p?.abilities==null || Class==null || Level(h)<=0)return 0;int n=0;
   foreach(var d in ChosenSecrets(h))if(p.abilities.GetAbility(d)==null){p.abilities.GainAbility(d);n++;}
   return n;
  }
  // 魔法奥秘学来、自己又没有那个职业的法术 → 算吟游诗人的（扣乐韵、用激昂强度）。
  public static LP_ClassDef SecretClass(Pawn p,AbilityDef d){
   if(d==null || !SecretSet.Contains(d.defName))return null;
   var h=Adventurer.Get(p);if(h==null || Level(h)<=0)return null;
   var orig=ClassSystem.ClassOf(d);if(orig!=null && ClassSystem.Level(h,orig)>0)return null;
   foreach(var c in ChosenSecrets(h))if(c==d)return Class;
   return null;
  }
  public static StatDef StatFor(Pawn p,CompProperties_ClassEffect props){
   if(props==null)return null;if(Users.Count==0)return props.stat;
   if(byProps==null){byProps=new Dictionary<CompProperties_ClassEffect,AbilityDef>();foreach(var n in Secrets){var d=DefCache<AbilityDef>.Get(n);var e=d?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();if(e!=null)byProps[e]=d;}}
   return byProps.TryGetValue(props,out var def) && SecretClass(p,def)!=null && Power!=null?Power:props.stat;
  }
  // 选分支时：已经会的法术（自己的职业或别的奥秘）不能再选。
  public static string ChoiceBlock(Hediff_Adventurer h,string key,int option){
   if(key==null || !key.StartsWith("secret"))return null;var d=SecretOption(option);if(d==null)return null;
   foreach(var k in SecretKeys)if(k!=key && ClassSubclass.Choice(h,k)==option)return "已经用魔法奥秘学过「"+d.label+"」";
   if(h?.pawn?.abilities?.GetAbility(d)!=null)return "已经会「"+d.label+"」了";
   return null;
  }

  // ---------- 被动能力位 ----------
  public const int FBard=1,FJack=2,FLore=4,FPeerless=8,FValor=16,FExtra=32,FBattleMagic=64,FBlade=128,FMaster=256,FSilver=512,FInfectious=1024,FUniversal=2048,FUndying=4096,FRenown=8192;
  static readonly Dictionary<string,int> FlagOf=new Dictionary<string,int>{{"LP_BD_JackOfAllTrades",FJack},{"LP_BD_LoreMastery",FLore},{"LP_BD_PeerlessSkill",FPeerless},{"LP_BD_CombatInspiration",FValor},
   {"LP_BD_ExtraAttack",FExtra},{"LP_BD_ExtraAttackBlade",FExtra},{"LP_BD_BattleMagic",FBattleMagic},{"LP_BD_BladeFlourish",FBlade},{"LP_BD_MasterFlourish",FMaster},{"LP_BD_SilverTongue",FSilver},
   {"LP_BD_InfectiousInspiration",FInfectious},{"LP_BD_UniversalSpeech",FUniversal},{"LP_LegUndyingLegend",FUndying}};
  public static readonly Dictionary<int,int> Users=new Dictionary<int,int>();
  public static int Flags(Pawn p){if(p==null || Users.Count==0)return 0;Users.TryGetValue(p.thingIDNumber,out int f);return f;}
  public static bool S(Pawn p,int f)=>(Flags(p) & f)!=0;
  public static int Refresh(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return 0;int f=0;
   if(!p.Dead && Class!=null && Level(h)>0){
    f|=FBard;var list=p.abilities?.abilities;if(list!=null)for(int k=0;k<list.Count;k++)if(FlagOf.TryGetValue(list[k].def.defName,out int b))f|=b;
    var renown=DefCache<LP_FeatDef>.Get("LP_Feat_Class_BardRenown");if(renown!=null && h.HasFeat(renown) && ClassFeats.Active(h,renown))f|=FRenown;
   }
   if(f==0)Users.Remove(p.thingIDNumber);else Users[p.thingIDNumber]=f;
   return f;
  }
  static void Passive(Pawn p,string hediff,bool on,float severity=-1f){
   if(on){var have=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff(hediff));if(have==null)Items.Give(p,hediff,-1,severity);else if(severity>0f && Mathf.Abs(have.Severity-severity)>.01f)have.Severity=severity;}
   else if(Items.Has(p,hediff))Items.Remove(p,hediff);
  }
  static readonly string[] PassiveHediffs={"LP_BD_JackPassive","LP_BD_LorePassive","LP_BD_PeerlessPassive","LP_BD_ValorPassive","LP_BD_ExtraAttackPassive","LP_BD_BladePassive","LP_BD_SilverPassive"};
  // ClassSystem.Tick250 → 这里（不是吟游诗人的人一次字典查找就返回）。
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;if(p==null || Class==null)return;
   if(Level(h)<=0){if(Users.Count>0 && Users.Remove(p.thingIDNumber))foreach(var n in PassiveHediffs)Items.Remove(p,n);return;}
   if(p.Dead)return;
   int f=Refresh(h);
   float add=RegenAmount(p);if(add>0f)ClassSystem.AddResource(h,Class,add);
   Passive(p,"LP_BD_JackPassive",(f & FJack)!=0);
   Passive(p,"LP_BD_LorePassive",(f & FLore)!=0,(f & FLore)!=0?Mathf.Clamp(p.GetStatValue(Power),.5f,5f):-1f);
   Passive(p,"LP_BD_PeerlessPassive",(f & FPeerless)!=0);Passive(p,"LP_BD_ValorPassive",(f & FValor)!=0);
   Passive(p,"LP_BD_ExtraAttackPassive",(f & FExtra)!=0);Passive(p,"LP_BD_BladePassive",(f & FBlade)!=0);Passive(p,"LP_BD_SilverPassive",(f & FSilver)!=0);
   var d=ClassSubclass.Data(h);
   if((f & FLore)!=0 && d!=null && !d.ticks.ContainsKey("bdLore")){d.ticks["bdLore"]=Math.Max(1,Now);LoreBoost(p);}
   if((f & FMaster)!=0 && d!=null)MasterTick(h,d,p);
   if((f & FRenown)!=0 && d!=null && p.Faction!=null && p.Faction.IsPlayer)RenownTick(d,p);
  }
  // 博闻强识：兴趣最低的 3 个技能（能用的）兴趣升一档、等级 +2（只发一次）。返回升了的技能。
  public static List<SkillRecord> LoreBoost(Pawn p){
   var got=new List<SkillRecord>();if(p?.skills==null)return got;
   foreach(var r in p.skills.skills.Where(r=>!r.TotallyDisabled && r.passion!=Passion.Major).OrderBy(r=>(int)r.passion).ThenBy(r=>r.Level).Take(3).ToList()){
    r.passion=r.passion==Passion.None?Passion.Minor:Passion.Major;r.Level=Math.Min(20,r.Level+2);got.Add(r);
   }
   if(got.Count>0 && p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"的博闻强识："+string.Join("、",got.Select(r=>r.def.label))+" 兴趣提升、+2 级。",p,MessageTypeDefOf.PositiveEvent,false);
   return got;
  }
  // 大师花招：战斗中乐韵低于 2 时每 10 秒 +1。
  public static void MasterTick(Hediff_Adventurer h,SubclassData d,Pawn p,int ticks=250){
   if(ClassSubclass.NotInCombat(p) || ClassSystem.Resource(h,Class)>=2f){d.ticks.Remove("bdMaster");return;}
   d.ticks.TryGetValue("bdMaster",out int t);t+=ticks;
   if(t>=MasterTicks){t-=MasterTicks;ClassSystem.AddResource(h,Class,1f);Items.Mote(p,"大师花招：乐韵 +1",Steel);}
   d.ticks["bdMaster"]=t;
  }
  // 名动四方：每季度（15 天）非敌对派系好感 +2（到 +50 不再加）。
  public static int RenownTick(SubclassData d,Pawn p){
   int now=Now;if(!d.ticks.TryGetValue("bdRenown",out int last) || last<=0){d.ticks["bdRenown"]=Math.Max(1,now);return 0;}
   if(now-last<RenownTicks)return 0;d.ticks["bdRenown"]=now;int n=0;
   foreach(var fa in Find.FactionManager.AllFactionsVisible.Where(x=>!x.IsPlayer && x.HasGoodwill && !x.def.hidden && !x.HostileTo(Faction.OfPlayer) && x.PlayerGoodwill<RenownCap).ToList()){Factions.ChangeGoodwillExact(fa,RenownGoodwill,"名动四方");n++;}
   if(n>0 && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"名动四方：歌谣传遍大陆，"+n+" 个派系好感 +"+RenownGoodwill+"。",p,MessageTypeDefOf.PositiveEvent,false);
   return n;
  }
  // 聊天（BardChat）：吟游诗人（说的、听的都算）+0.2 乐韵；普世言谈：和非敌对派系的人聊天，对方好感 +1（每天最多 +5）。返回好感变化。
  public static int Chatted(Pawn a,Pawn b){
   foreach(var x in new[]{a,b})if(x!=null && S(x,FBard)){var h=Adventurer.Get(x);if(h!=null)ClassSystem.AddResource(h,Class,ChatGain);}
   if(a==null || b?.Faction==null || !S(a,FUniversal) || a.Faction==null || !a.Faction.IsPlayer || b.Faction.IsPlayer || b.Faction.HostileTo(a.Faction) || !b.Faction.HasGoodwill)return 0;
   var d=ClassSubclass.Data(a);if(d==null)return 0;int day=Now/GenDate.TicksPerDay+1;
   d.ticks.TryGetValue("bdUniDay",out int last);d.ticks.TryGetValue("bdUniN",out int n);if(last!=day){last=day;n=0;}
   if(n>=UniversalDailyCap)return 0;
   d.ticks["bdUniDay"]=last;d.ticks["bdUniN"]=n+1;Factions.ChangeGoodwillExact(b.Faction,1,"普世言谈");return 1;
  }
  public static float QualityTiers(Pawn p)=>Users.Count>0 && S(p,FPeerless)?QualityBonus:0f;
  public static bool Foreseeing(Pawn v)=>v!=null && Items.Has(v,"LP_ACB_Foresight");

  // ---------- 主动能力 ----------
  static readonly HashSet<string> Known=new HashSet<string>{"BdMockery","BdInspire","BdLaughter","BdRest","BdCountercharm","BdConfusion","BdBallad","BdMislead","BdCreation","BdFinale",
   "BdCutting","BdAnthem","BdFlourishMode","BdBladeWhirl","BdTruce","BdDance","BdMassInvis","BdStun"};
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e==null || !Known.Contains(e.effect))return null;var t=target.Thing as Pawn;
   switch(e.effect){
    case "BdMockery":case "BdCutting":case "BdBladeWhirl":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
    case "BdLaughter":case "BdDance":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";
     if(e.effect=="BdDance"?t.RaceProps.IsMechanoid:Mindless(t))return "对机械体"+(e.effect=="BdDance"?"":"、虫族和没有心智的目标")+"无效。";return null;
    case "BdInspire":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(t==caster)return "不能激励自己。";if(t.HostileTo(caster))return "只能对友军使用。";if(!t.RaceProps.Humanlike)return "只能激励人形。";return null;
    case "BdCreation":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";return null;
    case "BdTruce":return TruceCannot(caster,t);
    case "BdConfusion":case "BdStun":{var map=caster.Map;var c=target.Cell;if(map==null || !c.IsValid || !c.InBounds(map))return "需要选择一个位置。";return null;}
   }
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e==null || !Known.Contains(e.effect))return false;
   var t=target.Thing as Pawn;float s=ClassEffects.Strength(p,e),pw=ClassEffects.Power(p,e);int dur=ClassEffects.Duration(p,e);float rad=ClassEffects.Radius(p,e);
   switch(e.effect){
    case "BdMockery":if(Hostile(p,t))Mock(p,t,pw,dur);break;
    case "BdInspire":if(t!=null && t!=p && Friendly(p,t))Inspire(p,t,s);break;
    case "BdLaughter":if(Hostile(p,t))Laughter(p,t,s,dur);break;
    case "BdRest":Rest(p,rad,e.duration,s);break;
    case "BdCountercharm":Countercharm(p,rad,e.duration,s);break;
    case "BdConfusion":Confusion(p,target.Cell,rad,dur,s);break;
    case "BdBallad":OpenBallad(p,def);break;
    case "BdMislead":Mislead(p,rad,e.duration);break;
    case "BdCreation":if(t!=null && !t.Dead){if(t.HostileTo(p))WordKill(p,t,s,pw);else WordHeal(p,t);}break;
    case "BdFinale":Finale(p,e.duration);break;
    case "BdCutting":if(Hostile(p,t)){Items.Give(t,"LP_BD_CuttingWords",dur,1f);Items.Mote(t,"刻薄之语",Lyric);}break;
    case "BdAnthem":Anthem(p,rad,e.duration,pw);break;
    case "BdFlourishMode":CycleMode(p);break;
    case "BdBladeWhirl":if(Hostile(p,t))BladeWhirl(p,t,s);break;
    case "BdTruce":if(Hostile(p,t))Truce(p,t,s);break;
    case "BdDance":if(Hostile(p,t))Dance(p,t,s,dur);break;
    case "BdMassInvis":MassInvisibility(p,rad,e.duration);break;
    case "BdStun":PowerWordStun(p,target.Cell,rad,dur);break;
   }
   return true;
  }
  // 战斗魔法：施放吟游诗人能力后 5 秒内下一次攻击更快、伤害 ×1.25；隐形的人施法（群体隐形、幻景本身除外）现形。
  public static void AfterCast(Ability_Class a){
   var p=a?.pawn;if(p==null)return;var e=a.Props;
   if(Invisible.Count>0 && Invisible.Contains(p) && e?.effect!="BdMassInvis" && e?.effect!="BdMislead")Reveal(p);
   if(Users.Count==0 || !S(p,FBattleMagic) || e==null || e.passive || ClassSystem.CastClassOf(p,a.def)!=Class || e.effect=="BdFlourishMode")return;
   Items.Give(p,"LP_BD_BattleMagicBuff",BattleMagicTicks,1f);
  }

  public static void Mock(Pawn p,Pawn t,float amount,int ticks){
   t.TakeDamage(new DamageInfo(Warlock.PsychicDamage,amount,999f,-1f,p));
   if(!t.Dead && !t.RaceProps.IsMechanoid)Items.Give(t,"LP_BD_Mocked",ticks,1f);
   Items.Mote(t,"恶毒嘲讽",Rose);
  }
  // 吟游激励：给目标（不朽传奇：12 格内所有友方人形）。返回激励的人数。
  public static int Inspire(Pawn p,Pawn t,float s){
   var list=new List<Pawn>{t};
   if(S(p,FUndying) && p.Map!=null)list.AddRange(Near(p.Map,p.Position,LegendRadius).Where(x=>x!=p && x!=t && Friendly(p,x) && x.RaceProps.Humanlike && x.Spawned));
   int ticks=S(p,FUniversal)?UniversalTicks:InspireTicks;
   foreach(var x in list)Give(p,x,s,ticks,false);
   return list.Count;
  }
  public static Hediff_BardInspired Give(Pawn p,Pawn x,float s,int ticks,bool moved){
   var hd=Items.Give(x,"LP_BD_Inspired",ticks,Mathf.Clamp(s,.5f,5f)) as Hediff_BardInspired;
   if(hd!=null){hd.caster=p;hd.moved=moved;hd.strength=s;}
   if(S(p,FValor))Items.Give(x,"LP_BD_ValorInspired",ticks,1f);
   int lv=Level(p);
   if(lv>=5 && !moved && Roll<lv*.02f){var ih=x.mindState?.inspirationHandler;if(ih!=null && !ih.Inspired){var defs=DefDatabase<InspirationDef>.AllDefsListForReading.Where(d=>d.Worker!=null && d.Worker.InspirationCanOccur(x)).ToList();if(defs.Count>0 && ih.TryStartInspiration(defs.RandomElement(),"吟游激励",true))Items.Mote(x,"灵感！",Rose);}}
   Items.Mote(x,moved?"激励传开了":"吟游激励",Rose);
   return hd;
  }
  // 感染性激励：激励到期或对方倒地时转给 10 格内另一个没被激励的友方人形。
  public static Pawn Pass(Hediff_BardInspired from){
   var x=from?.pawn;var c=from?.caster;if(x==null || c==null || from.moved || !x.Spawned || !S(c,FInfectious))return null;
   var to=Near(x.Map,x.Position,InfectRange).Where(o=>o!=x && o!=c && !o.Dead && !o.Downed && o.Spawned && o.RaceProps.Humanlike && Friendly(c,o) && !Items.Has(o,"LP_BD_Inspired")).OrderBy(o=>o.Position.DistanceToSquared(x.Position)).FirstOrDefault();
   if(to==null)return null;from.moved=true;
   Give(c,to,from.strength>0f?from.strength:from.Severity,S(c,FUniversal)?UniversalTicks:InspireTicks,true);
   return to;
  }
  public static bool Laughter(Pawn p,Pawn t,float s,int ticks){
   if(Mindless(t))return false;
   if(!Succeed(Chance(StdBase,s,t),t)){Items.Give(t,"LP_BD_Mocked",MockTicks,1f);Items.Mote(t,"忍住了笑",Rose);return false;}
   Items.Give(t,"LP_BD_Laughing",ticks,1f);t.stances?.stunner?.StunFor(ticks,p,false);Items.Mote(t,"哈哈哈哈！",new Color(.95f,.75f,.4f));
   return true;
  }
  public static int Rest(Pawn p,float radius,int ticks,float s){
   int n=0;var th=DefCache<ThoughtDef>.Get("LP_BD_RestSongMood");
   foreach(var x in Near(p.Map,p.Position,radius).ToList()){if(!Friendly(p,x) || !x.RaceProps.Humanlike)continue;Items.Give(x,"LP_BD_Resting",ticks,Mathf.Clamp(s,.5f,5f));if(th!=null)x.needs?.mood?.thoughts?.memories?.TryGainMemory(th);n++;}
   Items.Mote(p,"安歇之歌",Lyric);return n;
  }
  public static bool Fearful(Pawn x)=>x.InMentalState && ClassSubclass.FearState(x.MentalStateDef);
  public static int Countercharm(Pawn p,float radius,int ticks,float s){
   int n=0;
   foreach(var x in Near(p.Map,p.Position,radius).ToList()){
    if(!Friendly(p,x) || !x.RaceProps.Humanlike)continue;
    if(x.InMentalState){var ms=x.MentalState;var md=x.MentalStateDef;
     if(Fearful(x) || md!=null && !md.IsExtreme && Roll<Mathf.Clamp(StdBase+(s-1f)*PerStrength,.05f,.95f)){ms.RecoverFromState();n++;Items.Mote(x,"回过神来",Lyric);}}
    Items.Give(x,"LP_BD_Countercharmed",ticks,1f);
   }
   Items.Mote(p,"反迷惑之歌",Lyric);return n;
  }
  public static int Confusion(Pawn p,IntVec3 c,float radius,int ticks,float s){
   int n=0;
   foreach(var x in Near(p.Map,c,radius).ToList()){
    if(!Hostile(p,x) || x.Downed || Mindless(x))continue;
    if(Succeed(Chance(StdBase,s,x),x)){Items.Give(x,"LP_BD_Confused",ticks,1f);Items.Mote(x,"困惑",Rose);n++;}else Items.Mote(x,"抵抗！",Rose);
   }
   if(p.Map!=null)foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(p.Map))FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),p.Map,1f,new Color(.85f,.5f,.75f));
   return n;
  }
  // 传颂之歌：能选的派系（恶名在外时只有敌对派系）。
  public static string Notorious(Pawn p)=>p?.Map==null?null:Bards.Notorious(p.Map);
  public static List<Faction> BalladTargets(Pawn p){bool bad=Notorious(p)!=null;return Find.FactionManager.AllFactionsVisible.Where(f=>!f.IsPlayer && f.HasGoodwill && !f.def.hidden && (!bad || f.HostileTo(Faction.OfPlayer))).ToList();}
  public static int BalladAmount(float s,bool satire){int a=Mathf.Min(20,Mathf.RoundToInt(5f+5f*s));return satire?Mathf.Max(1,a/2):a;}
  static void OpenBallad(Pawn p,AbilityDef def){
   if(p.Faction==null || !p.Faction.IsPlayer || Adventurer.TestSuppressed){var f=BalladTargets(p).Where(x=>!x.HostileTo(Faction.OfPlayer)).RandomElementWithFallback();if(f!=null)Ballad(p,f);return;}
   Find.WindowStack.Add(new Dialog_BardBallad(p,def));
  }
  public static int Ballad(Pawn p,Faction f){
   if(p==null || f==null)return 0;var bad=Notorious(p);bool satire=bad!=null;float s=p.GetStatValue(Power);int amount=BalladAmount(s,satire);
   Factions.ChangeGoodwillExact(f,amount,satire?"吟游诗人的威名之歌":"吟游诗人的传颂之歌");
   if(satire)GameComponent_Bard.Instance?.satire.AddDistinct(f.loadID);
   if(p.Map!=null)Events3.ThoughtAll(p.Map,"LP_BD_LegendMood");
   if(!Adventurer.TestSuppressed && p.Faction!=null && p.Faction.IsPlayer){
    var deed=p.Map==null?null:Bards.Deed(p.Map);
    Find.LetterStack.ReceiveLetter(satire?"威名之歌":"传颂之歌",p.LabelShort+(satire?"把殖民地的恶名（"+bad+"）唱成了一首让人胆寒的歌。\n\n"+f.Name+"好感 +"+amount+"，他们下一次袭击的规模 −10%。":"把殖民地的故事"+(deed!=null?"（"+deed+"）":"")+"写成歌谣传了出去。\n\n"+f.Name+"好感 +"+amount+"。")+"殖民者心情“我们的传说”+5（3 天）。",LetterDefOf.PositiveEvent,p);
   }
   return amount;
  }
  public static Thing Mislead(Pawn p,float radius,int ticks){
   var map=p.Map;if(map==null)return null;var origin=p.Position;
   var inv=DefCache<HediffDef>.Get("PsychicInvisibility");
   if(inv!=null){var h=HediffMaker.MakeHediff(inv,p);var dis=h.TryGetComp<HediffComp_Disappears>();if(dis!=null)dis.ticksToDisappear=ticks;p.health.AddHediff(h);Invisible.Add(p);}
   Thing decoy=null;var def=DefCache<ThingDef>.Get("LP_BD_Phantasm");
   if(def!=null){
    var cell=GenAdj.CellsAdjacent8Way(p).Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null && c.GetEdifice(map)==null).OrderBy(c=>c.DistanceToSquared(origin)).FirstOrDefault();
    if(cell.IsValid && cell!=default(IntVec3)){decoy=ThingMaker.MakeThing(def);decoy.SetFactionDirect(p.Faction);GenSpawn.Spawn(decoy,cell,map,WipeMode.FullRefund);}
   }
   if(decoy!=null)foreach(var x in Near(map,origin,radius).ToList())if(Hostile(p,x) && !x.Downed && x.mindState!=null){x.mindState.enemyTarget=decoy;Items.Give(x,"LP_Taunted",ticks);}
   Items.Mote(p,"惑心幻景",Rose);return decoy;
  }
  public static bool WordKill(Pawn p,Pawn t,float s,float amount){
   float line=Mathf.Min(KillMax,KillPer*s);
   if(!Boss(t) && t.health.summaryHealth.SummaryHealthPercent<line){t.Kill(new DamageInfo(Warlock.PsychicDamage,9999f,999f,-1f,p));Items.Mote(p,"力量真言·杀",Rose);return true;}
   t.TakeDamage(new DamageInfo(Warlock.PsychicDamage,amount,999f,-1f,p));if(!t.Dead)t.stances?.stunner?.StunFor(360,p);
   Items.Mote(t,"力量真言",Rose);return false;
  }
  public static float WordHeal(Pawn p,Pawn t){
   float healed=ClassMelee.Heal(t,ClassMelee.InjurySum(t)+1f);
   foreach(var n in new[]{"LP_BD_Confused","LP_BD_Laughing","LP_BD_Mocked","LP_Shaken","LP_MagicSleep","LP_ACB_Dancing"})Items.Remove(t,n);
   var bl=t.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);if(bl!=null)t.health.RemoveHediff(bl);
   t.stances?.stunner?.StopStun();if(Fearful(t))t.MentalState.RecoverFromState();
   Items.Mote(t,"力量真言·疗愈",new Color(1f,.9f,.6f));return healed;
  }
  public static int Finale(Pawn p,int ticks){
   var map=p.Map;if(map==null)return 0;int n=0;
   Items.Give(p,"LP_BD_FinaleAura",ticks,1f);
   foreach(var x in map.mapPawns.AllPawnsSpawned.ToList())if(Friendly(p,x) && x.RaceProps.Humanlike){Items.Give(x,"LP_BD_FinalSong",ticks,1f);n++;}
   Items.Mote(p,"终焉之歌",new Color(1f,.8f,.5f));return n;
  }
  public static int FinaleFearTick(Pawn p){
   int n=0;if(p?.Map==null)return 0;
   foreach(var x in Near(p.Map,p.Position,FinaleRange).ToList())if(Hostile(p,x) && !x.Downed && !x.RaceProps.IsMechanoid && Roll<FinaleFear && ClassSubclass.Fear(x,FinaleInterval,"终焉之歌"))n++;
   return n;
  }
  public static int Anthem(Pawn p,float radius,int ticks,float shield){
   int n=0;
   foreach(var x in Near(p.Map,p.Position,radius).ToList()){if(!Friendly(p,x) || !x.RaceProps.Humanlike)continue;Items.Give(x,"LP_BD_Anthem",ticks,1f);if(Fearful(x))x.MentalState.RecoverFromState();n++;}
   var cur=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_FeatInspired"));if(cur==null || cur.Severity<shield)Items.Give(p,"LP_FeatInspired",-1,shield);
   Items.Mote(p,"勇气战吼",Valiant);return n;
  }
  // 刀光剑舞：0 防御、1 斩击、2 机动；+3 = 花乐韵必定触发。
  public static readonly string[] ModeNames={"防御","斩击","机动"};
  public static int Mode(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;d.ticks.TryGetValue("bdMode",out int m);return Mathf.Clamp(m,0,5);}
  public static string ModeText(int m)=>ModeNames[m%3]+(m>=3?"（花乐韵，必定触发）":"（30% 触发，不花乐韵）");
  public static int CycleMode(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;int m=(Mode(p)+1)%6;d.ticks["bdMode"]=m;Items.Mote(p,"剑花："+ModeText(m),Steel);return m;}
  public static string Tip(Pawn p,AbilityDef def){
   if(def==null || p==null)return "";
   if(def.defName=="LP_BD_BladeFlourish")return "\n\n当前招式："+ModeText(Mode(p));
   if(SecretClass(p,def)!=null)return "\n\n魔法奥秘：用激昂强度，消耗乐韵。";
   return "";
  }
  static bool busy;
  // 近战命中时（BardDamage 里调用）：按招式打出剑花。返回是否触发。
  public static bool Flourish(Pawn a,Pawn v,float amount,DamageDef def){
   if(busy || a==null || v==null)return false;int m=Mode(a);var h=Adventurer.Get(a);bool proc;
   if(m>=3 && h!=null && ClassSystem.Resource(h,Class)>=1f){ClassSystem.AddResource(h,Class,-1f);proc=true;}
   else proc=Roll<(S(a,FMaster)?MasterChance:FlourishChance);
   if(!proc)return false;int kind=m%3;float s=a.GetStatValue(Power);
   Fiends.Defer(()=>{
    if(a.Dead || !a.Spawned)return;
    if(kind==0){float amt=10f*s;var cur=a.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_FeatInspired"));if(cur==null || cur.Severity<amt)Items.Give(a,"LP_FeatInspired",FlourishShieldTicks,amt);Items.Mote(a,"剑花·防御",Steel);}
    else if(kind==1){var other=v.Spawned?Near(v.Map,v.Position,1.5f).FirstOrDefault(x=>x!=v && Hostile(a,x) && !x.Downed && x.Position.AdjacentTo8WayOrInside(a.Position)):null;if(other!=null){busy=true;try{other.TakeDamage(new DamageInfo(def??DamageDefOf.Cut,amount,.2f,-1f,a));}finally{busy=false;}Items.Mote(other,"剑花·斩击",Steel);}}
    else if(v.Spawned && !v.Dead){var dest=DNDPush.Destination(v.Map,v.Position,(v.Position-a.Position).ToVector3(),2);DNDPush.Move(v,dest);Items.Mote(v,"剑花·机动",Steel);}
   });
   return true;
  }
  public static int BladeWhirl(Pawn p,Pawn t,float s){
   var map=p.Map;if(map==null)return 0;
   var list=new List<Pawn>{t};list.AddRange(Near(map,t.Position,3f).Where(x=>x!=t && Hostile(p,x) && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(t.Position)).Take(4));
   int n=0;Items.Give(p,"LP_WhirlwindFury",60,s);
   try{
    foreach(var x in list){
     if(x.Dead || !x.Spawned || p.Dead || !p.Spawned || p.Downed)break;
     if(!x.Position.AdjacentTo8WayOrInside(p.Position)){var c=Charge.LandingCell(p,x);if(!c.IsValid)continue;FleckMaker.ThrowDustPuffThick(p.DrawPos,map,1.2f,Steel);Races124.Blink(p,c);}
     ClassMelee.Strike(p,x);n++;
    }
   }finally{Items.Remove(p,"LP_WhirlwindFury");}
   Items.Mote(p,"刃舞旋风 ×"+n,Steel);return n;
  }
  // 停战之言。
  static bool RaidLeaderBoss(Lord l)=>l.ownedPawns.Any(x=>Boss(x));
  public static string TruceCannot(Pawn caster,Pawn t){
   if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";if(!t.RaceProps.Humanlike)return "只能对人形喊话。";
   if(t.Faction==null)return "它不属于任何派系。";
   var l=t.GetLord();if(l==null)return "它不在袭击队伍里。";
   if(Items.Undead(t) || Outsiders.Is(t) || RaidLeaderBoss(l) || l.ownedPawns.Any(x=>x.RaceProps.IsMechanoid || Mindless(x) && !x.RaceProps.Animal))return "这支队伍不吃这一套（亡灵、异界来客、机械族、虫族或剧情首领带队）。";
   return null;
  }
  public static float TruceChance(float s,Lord l)=>Mathf.Clamp(TruceBase+(s-1f)*PerStrength+TrucePerDown*((l?.numPawnsLostViolently??0)+(l?.ownedPawns.Count(x=>x.Downed)??0)),.05f,TruceMax);
  public static bool Truce(Pawn p,Pawn t,float s){
   var l=t.GetLord();if(TruceCannot(p,t)!=null || l==null)return false;
   if(Roll>=TruceChance(s,l)){Items.Give(t,"LP_BD_Mocked",TruceShaken,1f);Items.Mote(t,"不为所动",Silver);return false;}
   var f=t.Faction;var map=t.Map;Retreat(l);
   Factions.ChangeGoodwillExact(f,10,"停战之言");
   if(!Adventurer.TestSuppressed && p.Faction!=null && p.Faction.IsPlayer)Find.LetterStack.ReceiveLetter("停战",p.LabelShort+"说服了"+f.Name+"的袭击队伍：他们放弃了袭击，正在离开。\n\n"+f.Name+"好感 +10。",LetterDefOf.PositiveEvent,new LookTargets(t));
   Items.Mote(t,"停战",Silver);return true;
  }
  public static List<Pawn> Retreat(Lord l){
   var map=l.Map;var f=l.faction;var all=l.ownedPawns.ToList();var go=all.Where(x=>!x.Dead && x.Spawned && !x.Downed).ToList();
   foreach(var x in all)l.RemovePawn(x);
   if(go.Count>0 && map!=null)LordMaker.MakeNewLord(f,new LordJob_ExitMapBest(LocomotionUrgency.Jog,true,true),map,go);
   foreach(var x in go)x.mindState.enemyTarget=null;
   return go;
  }
  public static bool Dance(Pawn p,Pawn t,float s,int ticks){
   if(t.RaceProps.IsMechanoid)return false;
   if(!Succeed(Chance(DanceBase,s,t),t)){Items.Mote(t,"抵抗！",Rose);return false;}
   if(Boss(t))ticks=Math.Min(ticks,DanceBossTicks);
   Items.Give(t,"LP_ACB_Dancing",ticks,1f);t.stances?.stunner?.StunFor(ticks,p,false);Items.Mote(t,"跳起舞来",Rose);return true;
  }
  public static readonly HashSet<Pawn> Invisible=new HashSet<Pawn>();
  public static int MassInvisibility(Pawn p,float radius,int ticks){
   var inv=DefCache<HediffDef>.Get("PsychicInvisibility");if(inv==null || p.Map==null)return 0;int n=0;
   foreach(var x in Near(p.Map,p.Position,radius).ToList()){
    if(!Friendly(p,x) || !x.RaceProps.Humanlike)continue;
    var h=HediffMaker.MakeHediff(inv,x);var dis=h.TryGetComp<HediffComp_Disappears>();if(dis!=null)dis.ticksToDisappear=ticks;x.health.AddHediff(h);Invisible.Add(x);n++;
   }
   Items.Mote(p,"群体隐形",Rose);return n;
  }
  public static void Reveal(Pawn p){
   Invisible.Remove(p);var inv=DefCache<HediffDef>.Get("PsychicInvisibility");var h=inv==null?null:p?.health?.hediffSet?.GetFirstHediffOfDef(inv);
   if(h!=null){p.health.RemoveHediff(h);Items.Mote(p,"现形",Rose);}
  }
  public static int PowerWordStun(Pawn p,IntVec3 c,float radius,int ticks){
   int n=0;foreach(var x in Near(p.Map,c,radius).ToList())if(Hostile(p,x)){x.stances?.stunner?.StunFor(Boss(x)?ticks/2:ticks,p,false);n++;}
   if(p.Map!=null)foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(p.Map))FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(),p.Map,.5f);
   Items.Mote(p,"力量真言·震慑",Rose);return n;
  }

  // ---------- NPC ----------
  // 巡演的吟游诗人：直接给 level 级吟游诗人（冒险者等级不够就补到这一级）。
  public static bool MakeNpcBard(Pawn p,int level){
   if(p==null || Class==null || !Adventurer.Enabled)return false;
   var h=Adventurer.Get(p)??Adventurer.TryAdd(p,level,true);if(h==null)return false;
   if(h.level<level)h.level=level;if(h.classes==null)h.classes=new ClassData();
   h.classes.npc=true;ClassSystem.SetLevel(h,Class,level);NpcClasses.Finish(h);
   return true;
  }
  public static void Cleanup(){if(Invisible.Count>0)Invisible.RemoveWhere(x=>x==null || x.Destroyed || x.Dead || !Items.Has(x,"PsychicInvisibility"));}
 }

 // ==================== 状态 ====================
 public class Hediff_BardInspired : HediffWithComps {
  public Pawn caster;public bool moved;public float strength;
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(!moved && pawn.Downed && caster!=null && Bard.S(caster,Bard.FInfectious)){var self=this;Fiends.Defer(()=>{if(Bard.Pass(self)!=null && self.pawn?.health?.hediffSet?.hediffs.Contains(self)==true){self.pawn.health.RemoveHediff(self);Items.Remove(self.pawn,"LP_BD_ValorInspired");}});moved=true;}
  }
  public override void PostRemoved(){
   base.PostRemoved();
   if(!moved && caster!=null && pawn!=null && pawn.Spawned && Bard.S(caster,Bard.FInfectious)){var self=this;Fiends.Defer(()=>Bard.Pass(self));}
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref caster,"lpBdCaster",true);Scribe_Values.Look(ref moved,"lpBdMoved");Scribe_Values.Look(ref strength,"lpBdStrength");}
 }
 // 狂笑：受到伤害立刻清醒（取消眩晕）。
 public class Hediff_BardLaughter : HediffWithComps {
  public override void Notify_PawnPostApplyDamage(DamageInfo dinfo,float totalDamageDealt){
   base.Notify_PawnPostApplyDamage(dinfo,totalDamageDealt);
   if(totalDamageDealt<=0f || dinfo.Def==null || !dinfo.Def.harmsHealth)return;
   var self=this;Fiends.Defer(()=>{var p=self.pawn;if(p?.health?.hediffSet?.hediffs.Contains(self)==true){p.health.RemoveHediff(self);Items.Mote(p,"笑醒了",Bard.Rose);}});
  }
  public override void PostRemoved(){base.PostRemoved();pawn?.stances?.stunner?.StopStun();}
 }
 // 困惑：每 2 秒随机发呆 / 乱走 / 攻击身边的人。
 public class Hediff_BardConfused : HediffWithComps {
  int acc;
  public override void TickInterval(int delta){
   base.TickInterval(delta);acc+=delta;if(acc<Bard.ConfuseInterval)return;acc=0;
   var p=pawn;if(p==null || !p.Spawned || p.Dead || p.Downed || p.jobs==null)return;
   Act(p,Rand.Range(0,3));
  }
  public static void Act(Pawn p,int roll){
   if(roll==0){p.stances?.stunner?.StunFor(Bard.ConfuseInterval,p,false);Items.Mote(p,"发呆",Bard.Rose);return;}
   if(roll==2){
    var t=Races124.PawnsNear(p.Map,p.Position,6f).Where(x=>x!=p && !x.Dead && !x.Downed && x.Spawned).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
    if(t!=null){var job=JobMaker.MakeJob(JobDefOf.AttackMelee,t);job.expiryInterval=Bard.ConfuseInterval;job.killIncappedTarget=false;p.jobs.StartJob(job,JobCondition.InterruptForced);Items.Mote(p,"乱打",Bard.Rose);return;}
   }
   var c=CellFinder.RandomClosewalkCellNear(p.Position,p.Map,5);var j=JobMaker.MakeJob(JobDefOf.GotoWander,c);j.expiryInterval=Bard.ConfuseInterval;p.jobs.StartJob(j,JobCondition.InterruptForced);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref acc,"lpBdAcc");}
 }
 // 刻薄之语：Live = 地图上还在的个数（伤害挂钩的快速返回，同咒术）。
 public class Hediff_BardCutting : HediffWithComps {
  public static int Live;
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);Live++;}
  public override void PostRemoved(){base.PostRemoved();if(Live>0)Live--;}
  public override void ExposeData(){base.ExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit)Live++;}
 }
 // 终焉之歌（友方）：期间倒地时治好一半伤势、减轻失血，尽力站起来一次。
 public class Hediff_BardFinale : HediffWithComps {
  public bool used;
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(used || pawn==null || !pawn.Downed || pawn.Dead)return;used=true;var p=pawn;
   Fiends.Defer(()=>{if(p.Dead)return;ClassMelee.Heal(p,ClassMelee.InjurySum(p)*.5f);var bl=p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);if(bl!=null)bl.Severity=Mathf.Max(0f,bl.Severity-.3f);Items.Mote(p,"歌声里站起来",new Color(1f,.8f,.5f));});
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref used,"lpBdUsed");}
 }
 // 终焉之歌（唱的人）：每 5 秒吓跑附近的敌人。
 public class Hediff_BardFinaleAura : HediffWithComps {
  int acc;
  public override void TickInterval(int delta){base.TickInterval(delta);acc+=delta;if(acc<Bard.FinaleInterval)return;acc=0;if(pawn!=null && pawn.Spawned && !pawn.Downed)Bard.FinaleFearTick(pawn);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref acc,"lpBdAcc");}
 }
 // 不可抗拒之舞：跳舞期间一直被定住，转来转去。
 public class Hediff_BardDance : HediffWithComps {
  int acc;
  public override void TickInterval(int delta){
   base.TickInterval(delta);acc+=delta;if(acc<30)return;acc=0;var p=pawn;if(p==null || !p.Spawned || p.Dead)return;
   p.Rotation=new Rot4((p.Rotation.AsInt+1)%4);
   var left=GetComp<HediffComp_Disappears>()?.ticksToDisappear??0;if(left>0 && p.stances?.stunner!=null && !p.stances.stunner.Stunned)p.stances.stunner.StunFor(left,p,false);
  }
  public override void PostRemoved(){base.PostRemoved();pawn?.stances?.stunner?.StopStun();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref acc,"lpBdAcc");}
 }
 // 惑心幻景的幻象分身：10 秒后消散。
 public class Building_BardPhantasm : Building {
  public int born;public const int Life=600;
  protected override void Tick(){
   base.Tick();int now=Find.TickManager.TicksGame;if(born<=0)born=now;
   if(now-born>=Life && !Destroyed)Destroy(DestroyMode.Vanish);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref born,"lpBorn");}
 }

 // 传颂之歌：选一个派系（不用浮动菜单）。没选就关掉：退还乐韵、清掉冷却。
 public class Dialog_BardBallad : Window {
  readonly Pawn pawn;readonly AbilityDef def;bool done;Vector2 scroll;
  public override Vector2 InitialSize=>new Vector2(520f,560f);
  public Dialog_BardBallad(Pawn p,AbilityDef d){pawn=p;def=d;doCloseX=true;doCloseButton=true;absorbInputAroundWindow=true;forcePause=true;}
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(r.x,r.y,r.width,32f),"传颂之歌：唱给谁听？");Text.Font=GameFont.Small;
   var bad=Bard.Notorious(pawn);float s=pawn.GetStatValue(Bard.Power);int amount=Bard.BalladAmount(s,bad!=null);
   Widgets.Label(new Rect(r.x,r.y+34f,r.width,44f),bad!=null?"殖民地恶名在外（"+bad+"），只能唱成威名之歌给敌对派系：好感 +"+amount+"，他们下一次袭击的规模 −10%。":"选中的派系好感 +"+amount+"，殖民者心情“我们的传说”+5。");
   var list=Bard.BalladTargets(pawn);var outer=new Rect(r.x,r.y+82f,r.width,r.height-82f-48f);var view=new Rect(0f,0f,outer.width-16f,list.Count*36f);
   Widgets.BeginScrollView(outer,ref scroll,view);
   try{float y=0f;foreach(var f in list){var row=new Rect(0f,y,view.width,32f);Widgets.DrawLightHighlight(row);Widgets.Label(new Rect(8f,y+5f,row.width-120f,24f),f.Name+"（好感 "+f.PlayerGoodwill+"）");if(Widgets.ButtonText(new Rect(row.xMax-104f,y+2f,96f,28f),"唱给他们")){done=true;Bard.Ballad(pawn,f);Close();}y+=36f;}}
   finally{Widgets.EndScrollView();}
  }
  public override void PostClose(){
   base.PostClose();if(done)return;
   var h=Adventurer.Get(pawn);var e=def?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
   if(h!=null && e!=null)ClassSystem.AddResource(h,Bard.Class,ClassLegendary.Cost(pawn,def,e));
   pawn.abilities?.GetAbility(def)?.ResetCooldown();
  }
 }

 // 存档：威名之歌记着的派系（下一次袭击 −10%）；读档时清掉静态缓存。
 public class GameComponent_Bard : GameComponent {
  public static GameComponent_Bard Instance;
  public List<int> satire=new List<int>();
  public GameComponent_Bard(Game game){Instance=this;Bard.Users.Clear();Bard.Invisible.Clear();Hediff_BardCutting.Live=0;}
  public override void FinalizeInit(){base.FinalizeInit();Instance=this;Bard.Users.Clear();}
  public override void GameComponentTick(){if(Find.TickManager.TicksGame%Martial.HourTicks==7)Bard.Cleanup();}
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref satire,"lpBdSatire",LookMode.Value);if(Scribe.mode==LoadSaveMode.PostLoadInit && satire==null)satire=new List<int>();}
 }

 // ==================== Harmony ====================
 // 刻薄之语（攻击方 ×0.75）、战斗魔法（×1.25，用掉）、刀光剑舞（近战命中打剑花）、隐形的人攻击就现形。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class BardDamage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(Bard.Users.Count==0 && Hediff_BardCutting.Live<=0 && Bard.Invisible.Count==0)return;
   if(dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   var a=dinfo.Instigator as Pawn;if(a==null || a==__instance)return;
   float f=1f;
   if(Hediff_BardCutting.Live>0 && Items.Has(a,"LP_BD_CuttingWords"))f*=Bard.CuttingFactor;
   int g=Bard.Flags(a);
   if((g & Bard.FBattleMagic)!=0 && Items.Has(a,"LP_BD_BattleMagicBuff")){f*=Bard.BattleMagicFactor;var att=a;Fiends.Defer(()=>Items.Remove(att,"LP_BD_BattleMagicBuff"));}
   if((g & Bard.FBlade)!=0 && a.HostileTo(__instance) && HeroCombat.Melee(dinfo))Bard.Flourish(a,__instance,dinfo.Amount*f,dinfo.Def);
   if(Bard.Invisible.Count>0 && Bard.Invisible.Contains(a)){var att=a;Fiends.Defer(()=>Bard.Reveal(att));}
   if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
 // 聊天：吟游诗人每次闲聊 / 深谈 +0.2 乐韵；普世言谈：和非敌对派系的人聊天，对方派系好感 +1（每天最多 +5）。
 [HarmonyPatch(typeof(Pawn_InteractionsTracker),nameof(Pawn_InteractionsTracker.TryInteractWith))]
 public static class BardChat {
  static void Postfix(Pawn ___pawn,Pawn recipient,bool __result){
   if(!__result || Bard.Users.Count==0)return;
   Bard.Chatted(___pawn,recipient);
  }
 }
 // 威名之歌：被唱的敌对派系下一次袭击规模 −10%。
 [HarmonyPatch(typeof(IncidentWorker_Raid),"TryExecuteWorker")]
 public static class BardSatireRaid {
  static void Prefix(IncidentParms parms){
   var c=GameComponent_Bard.Instance;if(c==null || c.satire.Count==0 || parms?.faction==null)return;
   if(c.satire.Remove(parms.faction.loadID))parms.points*=.9f;
  }
 }
}
