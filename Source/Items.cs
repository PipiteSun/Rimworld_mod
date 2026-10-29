using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.22.0 物品与炼金：公共部分。种族判断沿用 1.20.0 的基因规则；伤害倍率、魔法驱散、隐形揭示由这里统一挂钩。
 public static class Items {
  // 1.24.0：改成并发字典——属性、心情等钩子可能在后台线程里查定义，普通 Dictionary 并发写入会损坏。
  // 1.58.6：查不到的名字也缓存（按 DefCount，见 PerfCache.cs），原来每次都重查 DefDatabase 并写字典。
  public static ThingDef Thing(string name)=>DefCache<ThingDef>.Get(name);
  public static HediffDef Hediff(string name)=>DefCache<HediffDef>.Get(name);
  public static bool Has(Pawn p,HediffDef def)=>def!=null && p?.health?.hediffSet?.HasHediff(def)==true;
  public static bool Has(Pawn p,string hediff)=>Has(p,Hediff(hediff));
  // 加上（或刷新）限时状态：已有就把计时重置为 ticks（<0 时用定义里的时长）。
  public static Hediff Give(Pawn p,string name,int ticks=-1,float severity=-1f){
   var def=Hediff(name);if(def==null || p?.health==null || p.Dead)return null;
   if(FrostGiant.ChillImmune(p,name))return null; // 1.30.0：霜巨人不吃冬狼咬的寒霜迟缓
   var h=p.health.hediffSet.GetFirstHediffOfDef(def);
   if(h==null){h=HediffMaker.MakeHediff(def,p);if(severity>0)h.Severity=severity;p.health.AddHediff(h);}
   else if(severity>0)h.Severity=Math.Max(h.Severity,severity);
   var timer=(h as HediffWithComps)?.TryGetComp<HediffComp_Disappears>();
   if(timer!=null)timer.ticksToDisappear=ticks>0?ticks:timer.Props.disappearsAfterTicks.max;
   return h;
  }
  public static void Remove(Pawn p,string name){var def=Hediff(name);var h=def==null?null:p?.health?.hediffSet?.GetFirstHediffOfDef(def);if(h!=null)p.health.RemoveHediff(h);}
  public static int TicksLeft(Pawn p,string name){var def=Hediff(name);var h=def==null?null:p?.health?.hediffSet?.GetFirstHediffOfDef(def) as HediffWithComps;return h?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear??-1;}

  // ---------- 种族（按基因） ----------
  static GeneDef elfGene,lichGene,githStep,minotaurStature;
  public static bool IsElf(Pawn p)=>p?.genes?.Xenotype?.defName=="LP_Elf" || CommonRaces.Has(p,elfGene??(elfGene=DefDatabase<GeneDef>.GetNamedSilentFail("LP_ElfBloodline")));
  public static bool IsLich(Pawn p)=>Necro.Lich(p)!=null || CommonRaces.Has(p,lichGene??(lichGene=DefDatabase<GeneDef>.GetNamedSilentFail("LP_LichGene")));
  public static bool IsGith(Pawn p)=>p?.genes?.Xenotype?.defName=="LP_Githyanki" || CommonRaces.Has(p,githStep??(githStep=DefDatabase<GeneDef>.GetNamedSilentFail("LP_GithAstralStep")));
  public static bool IsMinotaur(Pawn p)=>p?.genes?.Xenotype?.defName=="LP_Minotaur" || CommonRaces.Has(p,minotaurStature??(minotaurStature=DefDatabase<GeneDef>.GetNamedSilentFail("LP_MinotaurStature")));
  // 巫妖与死灵造物：不能喝药水，圣水对它们有效。
  public static bool Undead(Pawn p)=>p!=null && (Necro.Undead(p) || IsLich(p));
  public static bool Is(Pawn p,string race){
   if(p==null)return false;
   switch(race){
    case "Elf":return IsElf(p);
    case "Dwarf":return CommonRaces.IsDwarf(p);
    case "HalfOrc":return CommonRaces.IsHalfOrc(p);
    case "Minotaur":return IsMinotaur(p);
    case "Githyanki":return IsGith(p);
    case "MindFlayer":return MindFlayer.IsMindFlayer(p);
    case "Lich":return IsLich(p);
    case "Troll":return Troll.Is(p);
    case "FireGiant":return FireGiant.Active(p);
    case "ForceDragon":return ForceDragon.Active(p);
    // 1.24.0：象人、食人魔、鸟人、泰坦、巨灵；元素符按“血脉”判断（元素裔或带同一血脉的巨灵）。
    case "Loxodon":return Races124.IsLoxodon(p);
    case "Ogre":return Races124.IsOgre(p);
    case "Aarakocra":return Races124.IsAarakocra(p);
    case "Titan":return Races124.IsTitan(p);
    case "Genie":return Races124.IsGenie(p);
    case "TitanOrFireGiant":return Races124.IsTitan(p) || FireGiant.Active(p);
    // 1.30.0：巨人血统（火巨人、霜巨人……）与霜巨人。
    case "Giant":return GiantKin.Is(p);
    case "FrostGiant":return FrostGiant.Active(p);
    case "ElementFire":case "ElementWater":case "ElementEarth":case "ElementAir":return Genasi.Element(p)==race.Substring(7);
   }
   return false;
  }
  public static string RaceLabel(string race){switch(race){case "Elf":return "精灵";case "Dwarf":return "矮人";case "HalfOrc":return "半兽人";case "Minotaur":return "牛头人";case "Githyanki":return "吉斯洋基";case "MindFlayer":return "夺心魔";case "Lich":return "巫妖";case "Troll":return "巨魔";case "FireGiant":return "火巨人";case "ForceDragon":return "力能龙裔";case "Loxodon":return "象人";case "Ogre":return "食人魔";case "Aarakocra":return "鸟人";case "Titan":return "泰坦";case "Genie":return "巨灵";case "TitanOrFireGiant":return "泰坦或火巨人";case "Giant":return "巨人";case "FrostGiant":return "霜巨人";case "ElementFire":return "炽焰血脉的元素裔";case "ElementWater":return "潮汐血脉的元素裔";case "ElementEarth":return "岩石血脉的元素裔";case "ElementAir":return "疾风血脉的元素裔";}return race;}

  // ---------- 装备 ----------
  public static Apparel Worn(Pawn p,ThingDef def){var list=p?.apparel?.WornApparel;if(list==null || def==null)return null;for(int i=0;i<list.Count;i++)if(list[i].def==def)return list[i];return null;}
  public static Apparel Worn(Pawn p,string def){var list=p?.apparel?.WornApparel;return list==null || list.Count==0?null:Worn(p,Thing(def));} // 1.58.6：没穿衣服的人不查名字
  public static int WornCount(Pawn p,ThingDef def){var list=p?.apparel?.WornApparel;int n=0;if(list!=null && def!=null)for(int i=0;i<list.Count;i++)if(list[i].def==def)n++;return n;}
  public static bool Holds(Pawn p,string def)=>p?.equipment?.Primary!=null && p.equipment.Primary.def==Thing(def);
  public static ThingDef WeaponStuff(Pawn p)=>p?.equipment?.Primary?.Stuff;

  // ---------- 魔法驱散 / 隐形揭示 ----------
  static readonly DefRef<HediffDef> dispelled=new DefRef<HediffDef>("LP_MagicDispelled");
  public static bool Dispelled(Pawn p)=>Has(p,dispelled.Def);
  public static ItemsMap MapState(Map map)=>MapComp<ItemsMap>.Get(map); // 1.58.6：按地图缓存（寻路每一步都会查）
  public static bool Revealed(Map map){var m=MapState(map);return m!=null && m.Revealed;}
  public static ItemsState State=>GameComp<ItemsState>.Get();
  // 联动挂钩（神兽血裔等联动程序集往这里登记）。
  public static readonly List<Func<Pawn,ThingDef,float>> PotionPotencyHooks=new List<Func<Pawn,ThingDef,float>>();
  public static readonly List<Func<Pawn,bool>> ColdIronBaneHooks=new List<Func<Pawn,bool>>();
  public static float PotionPotency(Pawn p,ThingDef potion){float f=1f;foreach(var hook in PotionPotencyHooks){try{f*=hook(p,potion);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 药水联动出错："+e,9127341);}}return f;}
  public static void Mote(Thing t,string text,Color color){if(t!=null && t.Spawned)MoteMaker.ThrowText(t.DrawPos,t.Map,text,color);}
 }

 // 1.22.0 设置：抽巨魔血冷却天数。
 public partial class Settings {
  public int trollBleedDays=3;
  void ExposeItems(){ExposeCursed();Scribe_Values.Look(ref trollBleedDays,"trollBleedDays",3);trollBleedDays=Math.Max(1,Math.Min(15,trollBleedDays));}
 }
 public static class ItemSettings {
  public static void Draw(Listing_Standard l){l.GapLine();l.Label("抽巨魔血冷却："+LichMod.Settings.trollBleedDays+"天（同一个巨魔两次抽血的最短间隔）");LichMod.Settings.trollBleedDays=(int)l.Slider(LichMod.Settings.trollBleedDays,1,15);l.CheckboxLabeled("诅咒物品",ref LichMod.Settings.cursedItems,"（1.45.0）复仇之剑、四种混沌印记等诅咒物品戴上后脱不下，只有解除诅咒卷轴能解开。关掉后可以正常脱下，效果和代价照旧。");}
 }

 // 延后到下一刻执行的动作（传送等不能在工作单的执行过程中直接做）；每天修复巨魔皮衣物。
 public class ItemsState : GameComponent {
  public readonly List<Action> queued=new List<Action>();
  public ItemsState(Game game){}
  public void Defer(Action a){if(a!=null)queued.Add(a);}
  public void Flush(){var list=queued.ToList();queued.Clear();foreach(var a in list){try{a();}catch(Exception e){Log.Error("[DND边缘大陆] 延后动作出错："+e);}}}
  public override void GameComponentTick(){if(queued.Count>0)Flush();int now=Find.TickManager.TicksGame;if(now%GenDate.TicksPerDay==1234)ItemMaterials.DailyRepair();}
 }

 // 每张地图：识破卷轴的揭示时间、探测魔杖、防护邪恶结界。
 public class ItemsMap : MapComponent {
  public int revealUntil=-1;
  public bool wandPresent;
  int wandCheckTick=-9999;
  public List<EvilWard> wards=new List<EvilWard>();
  public ItemsMap(Map map):base(map){}
  public bool Revealed{get{int now=Find.TickManager.TicksGame;if(now-wandCheckTick>=250 || now<wandCheckTick){wandCheckTick=now;wandPresent=ItemWondrous.WandStored(map);}return wandPresent || now<revealUntil;}}
  public void RefreshWand(){wandCheckTick=-9999;}
  public override void MapComponentTick(){if(wards.Count>0 && Find.TickManager.TicksGame%30==0)ItemScrolls.MaintainWards(this);}
  public override void MapComponentUpdate(){if(wards.Count>0 && Find.CurrentMap==map)foreach(var w in wards)GenDraw.DrawFieldEdges(w.EdgeCells,new Color(1f,.92f,.55f));}
  public override void ExposeData(){
   Scribe_Values.Look(ref revealUntil,"lpRevealUntil",-1);
   Scribe_Collections.Look(ref wards,"lpEvilWards",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit && wards==null)wards=new List<EvilWard>();
  }
 }
 public class EvilWard : IExposable {
  public CellRect rect;public int until;
  // 1.58.6：画边框用的格子列表按矩形缓存（原来每帧每个结界都 ToList 一次）。
  List<IntVec3> edgeCells;CellRect edgeRect;
  public List<IntVec3> EdgeCells{get{if(edgeCells==null || !edgeRect.Equals(rect)){edgeCells=rect.Cells.ToList();edgeRect=rect;}return edgeCells;}}
  public void ExposeData(){Scribe_Values.Look(ref rect,"rect");Scribe_Values.Look(ref until,"until");}
 }

 // 物品伤害倍率：挂在 HediffDef 或服装 ThingDef 上。damageDefs 精确匹配；keywords 匹配伤害或其伤势的 defName；all=true 时对一切伤害生效。
 public class ItemDamageFactors : DefModExtension {
  public float factor=1f;
  public bool all;
  public List<string> damageDefs=new List<string>(),keywords=new List<string>(),excluded=new List<string>();
  public bool Matches(DamageDef d){
   if(d==null || !d.harmsHealth)return false;
   if(excluded.Any(k=>Mentions(d,k)))return false;
   return all || damageDefs.Contains(d.defName) || keywords.Any(k=>Mentions(d,k));
  }
  static bool Mentions(DamageDef d,string word)=>d.defName.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0 || (d.hediff!=null && d.hediff.defName.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0);
 }
 public static class ItemDamage {
  public static float Factor(Pawn p,DamageDef def){
   float f=1f;var hs=p?.health?.hediffSet?.hediffs;
   if(hs!=null)for(int i=0;i<hs.Count;i++){var e=hs[i].def.GetModExtension<ItemDamageFactors>();if(e!=null && e.Matches(def))f*=e.factor;}
   var worn=p?.apparel?.WornApparel;
   if(worn!=null)for(int i=0;i<worn.Count;i++){var e=worn[i].def.GetModExtension<ItemDamageFactors>();if(e!=null && e.Matches(def))f*=e.factor;}
   if(worn!=null)f*=MagicApparel.ElementFactor(p,def); // 1.45.1 元素织袍
   return f;
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class ItemDamageFactorPatch {
  static void Prefix(Pawn __instance,ref DamageInfo __0){float f=ItemDamage.Factor(__instance,__0.Def)*ItemMaterials.ArmorFactor(__instance,ref __0);if(f!=1f)__0.SetAmount(__0.Amount*f);}
 }
 // 驱散：护盾腰带在驱散期间停摆（能量保留），30秒后自动恢复。
 [HarmonyPatch(typeof(CompShield),"get_ShieldState")]
 public static class DispelShieldBelt {
  static void Postfix(CompShield __instance,ref ShieldState __result){if(__result==ShieldState.Active && Items.Dispelled((__instance.parent as Apparel)?.Wearer??__instance.parent as Pawn))__result=ShieldState.Disabled;}
 }
 // 驱散、识破卷轴、探测魔杖：一切原版隐形（药水、伟龙、灵能隐匿、心灵隐形、VPE 隐身）都被迫现形。
 [HarmonyPatch(typeof(HediffComp_Invisibility),"get_ForcedVisible")]
 public static class RevealInvisibility {
  static void Postfix(HediffComp_Invisibility __instance,ref bool __result){
   if(__result)return;var p=__instance.Pawn;if(p==null)return;
   if(Items.Dispelled(p) || (p.Spawned && Items.Revealed(p.Map) && !MagicItems.HidesFromDetection(p)))__result=true;
  }
 }
}
