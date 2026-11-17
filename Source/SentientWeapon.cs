using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.168.0 人格武器（DND 智能物品 / 原版人格武器的思路）：在附魔台窗口里给 +5 以上的武器「唤醒人格」。
 // 唤醒后武器得到随机人格（Defs/SentientWeapon.xml 里 hediffClass=Hediff_SentientBond 的 HediffDef，人格加成写在它的阶段里）和一个名字；
 // 第一个装备它的殖民者和它结下羁绊（只有羁绊者能装备），羁绊者身上挂对应人格的状态：拿着时按人格阶段给加成和心情，没拿着是“分离”阶段（无加成）。
 // 共鸣：羁绊者拿着它击杀 +1（人形 +2），每拿在手上一天 +1；30 苏醒、100 升华（加成更强）。羁绊者死亡或在附魔台解除羁绊后武器重新空出来。
 public static class SentientWeapon {
  public const int MinLevel=5,AwakenTier=30,ExaltTier=100,AdamantineCost=6,GoldCost=20;
  public const float BaseChance=.6f;
  public static float? TestRoll;
  public static readonly string[] Names={"霜语","赤心","夜鸣","灰烬之誓","远星","沉默者","渴血","白鸦","古誓","无眠","炽魂","寒鸦","回响","银喉","铁吟","余烬","星痕","黑荆","孤灯","狼语","月蚀","晨钟","断罪","低吟者","残阳","雾行","雷鸣","荒原之心"};
  public static readonly string[] TierLabels={"分离","初醒","苏醒","升华"};

  public static CompSentientWeapon Comp(Thing t)=>(t as ThingWithComps)?.GetComp<CompSentientWeapon>();
  public static bool Awake(Thing t)=>Comp(t)?.persona!=null;
  public static List<HediffDef> Personas=>DefDatabase<HediffDef>.AllDefsListForReading.Where(d=>d.hediffClass!=null && typeof(Hediff_SentientBond).IsAssignableFrom(d.hediffClass)).ToList();
  public static ThingDef Adamantine=>DefDatabase<ThingDef>.GetNamedSilentFail("LP_Adamantine");
  public static List<KeyValuePair<ThingDef,int>> Costs(){var l=new List<KeyValuePair<ThingDef,int>>();if(Adamantine!=null)l.Add(new KeyValuePair<ThingDef,int>(Adamantine,AdamantineCost));l.Add(new KeyValuePair<ThingDef,int>(ThingDefOf.Gold,GoldCost));return l;}
  public static int Tier(float res)=>res>=ExaltTier?3:res>=AwakenTier?2:1;
  public static float Chance(Pawn worker,Thing bench)=>Mathf.Clamp(BaseChance+GearEnhance.SkillBonus(worker)+(GearEnhance.HasFocusStone(bench)?GearEnhance.FocusBonus:0f),.05f,1f);
  public static RecipeDef SkillRecipe=>GearEnhanceBench.RecipeFor(GearEnhance.LowMax);

  // 为什么现在不能唤醒 / 重塑；null = 可以。
  public static string Blocked(Thing w,Map map){
   var c=Comp(w);if(c==null)return "只有武器能唤醒人格".Loc();
   if(GearEnhance.Level(w)<MinLevel)return "要先附魔强化到 +".Loc()+MinLevel;
   var r=SkillRecipe;if(GearEnhanceBench.Operator(map,r)==null)return "没有智识".Loc()+GearEnhanceBench.SkillNeed(r)+"以上、能做智识工作的殖民者".Loc();
   if(!Costs().All(k=>GearEnhanceBench.CountOnMap(map,k.Key)>=k.Value))return "材料不够".Loc();
   return null;
  }
  // 在附魔台上唤醒（或重塑）一次：扣材料，按成功率给一个人格（重塑时换成另一种，共鸣保留）。
  public static GearEnhance.Result TryAwaken(Thing w,Thing bench){
   var map=bench?.Map;if(Blocked(w,map)!=null)return GearEnhance.Result.None;
   var op=GearEnhanceBench.Operator(map,SkillRecipe);foreach(var k in Costs())GearEnhanceBench.Consume(map,k.Key,k.Value);
   return Awaken(w,op,bench);
  }
  public static GearEnhance.Result Awaken(Thing w,Pawn op,Thing bench,HediffDef forced=null){
   var c=Comp(w);if(c==null)return GearEnhance.Result.None;
   float chance=Chance(op,bench),roll=TestRoll??Rand.Value;
   if(roll>=chance){
    if(!GearEnhance.TestSuppressed)Messages.Message("唤醒".Loc()+w.LabelNoCount+"的人格失败了，材料没了（成功率".Loc()+chance.ToStringPercent()+"）。".Loc(),w,MessageTypeDefOf.NeutralEvent,false);
    return GearEnhance.Result.Fail;
   }
   var old=c.persona;var pool=Personas.Where(d=>d!=old).ToList();
   c.persona=forced??pool.RandomElementWithFallback(old);
   if(c.weaponName.NullOrEmpty())c.weaponName=Names.RandomElement();
   if(old==null)c.resonance=0f;
   c.soulName=null; // 重塑：换成随机人格，器灵消散
   op?.skills?.Learn(SkillDefOf.Intellectual,300f);
   // 已经拿在殖民者手上：直接结下羁绊；已经有羁绊者：换成新人格的状态。
   var holder=GearEnhanceBench.Owner(w);
   if(c.bonded!=null)Sync(c.bonded,w);else if(holder!=null)c.TryBond(holder);
   if(!GearEnhance.TestSuppressed)Messages.Message(w.LabelNoCount+(old==null?"苏醒了，它的人格是".Loc():"的人格被重塑为".Loc())+"「"+c.persona.label+"」。".Loc(),w,MessageTypeDefOf.PositiveEvent,false);
   return GearEnhance.Result.Success;
  }

  // ---------- 1.170.0 封魂入器 ----------
  // 把一名囚犯的灵魂封进 +5 以上的武器当器灵：囚犯死去，武器按他最擅长的技能（和几个特性）得到人格，名字就是他的名字，
  // 初始共鸣 = 最高技能 ×2；羁绊者拿着时多一条「器灵在哀嚎」心情；生前讨厌他的殖民者得到「仇人在受折磨」。不会失败，材料同唤醒。
  public static List<Pawn> Prisoners(Map map)=>map==null?new List<Pawn>():map.mapPawns.PrisonersOfColonySpawned.Where(p=>!p.Dead && p.RaceProps?.Humanlike==true).ToList();
  public static string BlockedSoul(Thing w,Map map)=>Blocked(w,map)??(Prisoners(map).Count==0?"地图上没有囚犯".Loc():null);
  static HediffDef P(string n)=>DefDatabase<HediffDef>.GetNamedSilentFail(n);
  public static SkillDef BestSkill(Pawn p)=>p?.skills?.skills?.Where(s=>!s.TotallyDisabled).OrderByDescending(s=>s.Level).FirstOrDefault()?.def;
  public static HediffDef PersonaFor(Pawn p){
   var t=p?.story?.traits;
   if(t!=null){
    if(t.HasTrait(TraitDefOf.Brawler))return P("LP_Persona_Fury");
    if(t.HasTrait(TraitDef.Named("Tough")))return P("LP_Persona_Guardian");
    if(t.HasTrait(TraitDefOf.Psychopath) || t.HasTrait(TraitDefOf.Bloodlust))return P("LP_Persona_Bloodthirsty");
   }
   var s=BestSkill(p);string n=s?.defName;
   switch(n){
    case "Melee":return P("LP_Persona_Bloodthirsty");
    case "Shooting":return P("LP_Persona_Hunter");
    case "Intellectual":return P("LP_Persona_Sage");
    case "Social":return P("LP_Persona_Regal");
    case "Artistic":return P("LP_Persona_Serene");
    case "Construction":case "Mining":case "Medicine":return P("LP_Persona_Guardian");
    case "Animals":case "Plants":return P("LP_Persona_Swift");
    default:return P("LP_Persona_Fury");
   }
  }
  public static float SoulResonance(Pawn p){var s=BestSkill(p);return s==null?0f:p.skills.GetSkill(s).Level*2f;}
  public static GearEnhance.Result TryBindSoul(Thing w,Pawn prisoner,Thing bench){
   var map=bench?.Map;if(BlockedSoul(w,map)!=null || prisoner==null || prisoner.Dead || !Prisoners(map).Contains(prisoner))return GearEnhance.Result.None;
   foreach(var k in Costs())GearEnhanceBench.Consume(map,k.Key,k.Value);
   return BindSoul(w,prisoner);
  }
  public static GearEnhance.Result BindSoul(Thing w,Pawn prisoner){
   var c=Comp(w);if(c==null || prisoner==null || prisoner.Dead)return GearEnhance.Result.None;
   var persona=PersonaFor(prisoner)??Personas.FirstOrDefault();
   string name=prisoner.Name?.ToStringShort??prisoner.LabelShort;
   // 生前讨厌他的殖民者（好感 ≤ -10）
   var map=prisoner.MapHeld;var haters=new List<Pawn>();
   if(map!=null)foreach(var col in map.mapPawns.FreeColonistsSpawned)if(col.relations!=null && col.relations.OpinionOf(prisoner)<=-10)haters.Add(col);
   c.persona=persona;c.weaponName=name;c.soulName=name;c.resonance=Mathf.Max(c.resonance,SoulResonance(prisoner));
   prisoner.Kill(null);
   var hate=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_EnemySoulTormented");
   if(hate!=null)foreach(var h in haters)h.needs?.mood?.thoughts?.memories?.TryGainMemory(hate);
   var holder=GearEnhanceBench.Owner(w);
   if(c.bonded!=null)Sync(c.bonded,w);else if(holder!=null)c.TryBond(holder);
   if(!GearEnhance.TestSuppressed)Messages.Message(name+"的灵魂被封进了".Loc()+w.LabelNoCount+"，成了「".Loc()+persona.label+"」的器灵，在里面永远受折磨。".Loc(),w,MessageTypeDefOf.PositiveEvent,false);
   return GearEnhance.Result.Success;
  }
  public static Hediff_SentientBond BondOf(Pawn p)=>p?.health?.hediffSet?.hediffs?.OfType<Hediff_SentientBond>().FirstOrDefault();
  // 让羁绊者身上的状态与武器对上：多余的、人格不对的去掉，缺了就加上，阶段按是否手持和共鸣刷新。
  public static void Sync(Pawn p,Thing weapon=null){
   if(p?.health==null || p.Dead)return;
   foreach(var h in p.health.hediffSet.hediffs.OfType<Hediff_SentientBond>().ToList()){var c=Comp(h.weapon);if(c==null || h.weapon.Destroyed || c.bonded!=p || c.persona!=h.def)p.health.RemoveHediff(h);}
   var bond=BondOf(p);
   if(bond==null){
    var w=weapon??FindWeapon(p);var wc=Comp(w);if(wc!=null && wc.bonded!=p)return;if(wc==null || wc.persona==null)return;
    bond=(Hediff_SentientBond)HediffMaker.MakeHediff(wc.persona,p);bond.weapon=w;p.health.AddHediff(bond);
   }
   bond.Refresh();
  }
  // 羁绊者手上的武器就是羁绊武器时最快；否则靠状态里记着的引用（Sync 前已去掉失效的）。
  static Thing FindWeapon(Pawn p){var w=p.equipment?.Primary;var c=Comp(w);return c!=null && c.bonded==p?w:null;}
  public static string Bonuses(HediffDef d,int tier){
   if(d?.stages==null || tier<0 || tier>=d.stages.Count)return "";
   var s=d.stages[tier];var parts=new List<string>();
   if(s.statFactors!=null)foreach(var m in s.statFactors)parts.Add(m.stat.LabelCap+" ×"+m.value.ToString("0.##"));
   if(s.statOffsets!=null)foreach(var m in s.statOffsets)parts.Add(m.stat.LabelCap+" "+m.stat.ValueToString(m.value,ToStringNumberSense.Offset));
   return string.Join("，".Loc(),parts);
  }
  public static void Register(){
   foreach(var d in DefDatabase<ThingDef>.AllDefsListForReading){
    if(!GearEnhance.Eligible(d) || !d.IsWeapon || d.IsApparel)continue;
    if(d.comps==null)d.comps=new List<CompProperties>();
    if(!d.comps.Any(c=>c is CompProperties_SentientWeapon))d.comps.Add(new CompProperties_SentientWeapon());
   }
  }
 }

 public class CompProperties_SentientWeapon : CompProperties {public CompProperties_SentientWeapon(){compClass=typeof(CompSentientWeapon);}}
 public class CompSentientWeapon : ThingComp {
  public HediffDef persona;public string weaponName;public Pawn bonded;public float resonance;
  // 1.170.0 封魂入器：被封进来的囚犯名字（null = 普通唤醒的人格）。
  public string soulName;
  public int Tier=>SentientWeapon.Tier(resonance);
  public override void PostExposeData(){
   Scribe_Defs.Look(ref persona,"lpPersona");Scribe_Values.Look(ref soulName,"lpPersonaSoul");Scribe_Values.Look(ref weaponName,"lpPersonaName");Scribe_References.Look(ref bonded,"lpPersonaBond");Scribe_Values.Look(ref resonance,"lpPersonaRes",0f);
  }
  public bool TryBond(Pawn p){
   if(persona==null || p==null || bonded!=null || p.Dead || p.Faction!=Faction.OfPlayer || p.RaceProps?.Humanlike!=true)return false;
   var other=SentientWeapon.BondOf(p);if(other!=null && other.weapon!=null && other.weapon!=parent && !other.weapon.Destroyed)return false; // 一个人只有一个羁绊
   bonded=p;SentientWeapon.Sync(p,parent);
   if(!GearEnhance.TestSuppressed)Messages.Message(p.LabelShort+"与".Loc()+parent.LabelNoCount+"结下了人格羁绊。".Loc(),p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  public void Unbond(bool sad){
   var p=bonded;bonded=null;if(p==null)return;SentientWeapon.Sync(p);
   if(sad && !p.Dead)p.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_PersonaBondBroken"));
  }
  public override void Notify_Equipped(Pawn pawn){base.Notify_Equipped(pawn);if(persona==null)return;if(bonded==null)TryBond(pawn);else if(bonded==pawn)SentientWeapon.Sync(pawn,parent);}
  public override void Notify_Unequipped(Pawn pawn){base.Notify_Unequipped(pawn);if(persona!=null && bonded==pawn)BondOf()?.Refresh();}
  Hediff_SentientBond BondOf()=>SentientWeapon.BondOf(bonded);
  public override string TransformLabel(string label)=>persona!=null && !weaponName.NullOrEmpty()?"「"+weaponName+"」"+label:label;
  public override string CompInspectStringExtra(){
   if(persona==null)return null;
   string s="人格：".Loc()+persona.label+"（"+SentientWeapon.TierLabels[Tier]+"，共鸣 ".Loc()+Mathf.FloorToInt(resonance)+(Tier<3?"/"+(Tier==1?SentientWeapon.AwakenTier:SentientWeapon.ExaltTier):"")+"）";
   if(soulName!=null)s+="\n"+"器灵：".Loc()+soulName+"的灵魂，正在受折磨".Loc();
   s+="\n"+(bonded!=null?"羁绊：".Loc()+bonded.LabelShort:"尚未结下羁绊（下一个装备它的殖民者）".Loc());
   return s;
  }
  public override IEnumerable<StatDrawEntry> SpecialDisplayStats(){
   if(persona!=null)yield return new StatDrawEntry(StatCategoryDefOf.BasicsImportant,"人格".Loc(),persona.LabelCap,persona.description+"\n\n"+"羁绊者拿着时（".Loc()+SentientWeapon.TierLabels[Tier]+"）：".Loc()+SentientWeapon.Bonuses(persona,Tier),4989);
  }
 }

 // 羁绊者身上的人格状态。阶段 0 分离（没拿着，无加成），1~3 按共鸣。
 public class Hediff_SentientBond : HediffWithComps {
  public Thing weapon;int acc;
  public CompSentientWeapon WComp=>SentientWeapon.Comp(weapon);
  public bool Holding=>weapon!=null && pawn?.equipment?.Primary==weapon;
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref weapon,"lpPersonaWeapon");}
  public override bool ShouldRemove=>weapon==null || weapon.Destroyed || WComp?.bonded!=pawn || WComp?.persona!=def;
  public void Refresh(){var c=WComp;Severity=c==null || !Holding?.5f:c.Tier+.5f;}
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<250)return;acc=0;
   var c=WComp;if(c==null)return;
   if(Holding)c.resonance+=250f/GenDate.TicksPerDay;
   Refresh();
  }
  public override string LabelInBrackets=>(WComp!=null?WComp.weaponName+"，":"")+(CurStageIndex>=0 && CurStageIndex<SentientWeapon.TierLabels.Length?SentientWeapon.TierLabels[CurStageIndex]:"");
 }

 public class ThoughtWorker_PersonaTorment : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){var h=SentientWeapon.BondOf(p);return h!=null && h.Holding && h.WComp?.soulName!=null?ThoughtState.ActiveDefault:ThoughtState.Inactive;}
 }
 public class ThoughtWorker_PersonaBond : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){var h=SentientWeapon.BondOf(p);if(h==null || !h.Holding || h.WComp==null)return ThoughtState.Inactive;return ThoughtState.ActiveAtStage(Mathf.Clamp(h.WComp.Tier-1,0,2));}
 }

 // 只有羁绊者能装备。
 [HarmonyPatch(typeof(EquipmentUtility),nameof(EquipmentUtility.CanEquip),new[]{typeof(Thing),typeof(Pawn),typeof(string),typeof(bool)},new[]{ArgumentType.Normal,ArgumentType.Normal,ArgumentType.Out,ArgumentType.Normal})]
 public static class SentientEquip {
  static void Postfix(Thing __0,Pawn __1,ref string __2,ref bool __result){
   if(!__result || __0==null || __1==null)return;var c=SentientWeapon.Comp(__0);
   if(c?.persona!=null && c.bonded!=null && c.bonded!=__1 && !c.bonded.Dead){__result=false;__2="它只认".Loc()+c.bonded.LabelShort;}
  }
 }
 // 击杀：羁绊者拿着它杀敌涨共鸣；羁绊者死了，武器重新空出来。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class SentientKill {
  static void Prefix(Pawn __instance,out bool __state){__state=__instance.Dead;}
  static void Postfix(Pawn __instance,DamageInfo? __0,bool __state){
   if(__state || !__instance.Dead)return;
   var own=SentientWeapon.BondOf(__instance);own?.WComp?.Unbond(false);
   var k=__0?.Instigator as Pawn;if(k==null || k==__instance)return;
   var w=k.equipment?.Primary;var c=SentientWeapon.Comp(w);
   if(c?.persona!=null && c.bonded==k){c.resonance+=__instance.RaceProps?.Humanlike==true?2f:1f;SentientWeapon.BondOf(k)?.Refresh();}
  }
 }
}
