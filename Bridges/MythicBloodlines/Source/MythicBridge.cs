using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using MythicBloodlines;

namespace LichPhylactery.MythicBridge {
 // 1.21.0 神兽血裔联动：只在启用神兽血裔时由 LoadFolders 加载；主程序集不引用本程序集或 MythicBloodlines.dll。
 [StaticConstructorOnStartup]
 public static class MythicBridge {
  static MythicBridge(){
   new Harmony("local.lich.phylactery.mythicbridge").PatchAll(typeof(MythicBridge).Assembly);
   BloodlineAPI.RainExtinguishedPawn+=OnRain;
   BloodlineAPI.TaotieAte+=OnTaotieAte;
   BloodlineAPI.QualityImproved+=OnQualityImproved;
   BloodlineAPI.RevealCustomControl+=OnReveal;
   Items.PotionPotencyHooks.Add(PotionPotency);
   // 寒铁对白泽血裔也 +25%（神兽血裔没有九尾血脉）。
   Items.ColdIronBaneHooks.Add(p=>Is(p,"Baize"));
  }
  // 1.22.0 饕餮喝治疗药水（含强效）效果翻倍。
  public static float PotionPotency(Pawn p,ThingDef potion)=>potion!=null && (potion.defName=="LP_PotionHealing" || potion.defName=="LP_PotionGreaterHealing") && Is(p,"Taotie")?2f:1f;
  // 1.22.0 句芒在炼金桌做药水，草药消耗减半：完成时退还一半草药（单数时余下那份按一半概率退还）。
  public static int HerbRefund(RecipeDef recipe,Pawn worker,List<Thing> ingredients,float roll){
   if(recipe==null || ingredients==null || !Is(worker,"Jumang") || !ItemPotions.IsPotion(recipe.ProducedThingDef) || recipe.AllRecipeUsers.All(u=>u.defName!="LP_AlchemyTable"))return 0;
   int herbs=ingredients.Where(t=>t?.def==ThingDefOf.MedicineHerbal).Sum(t=>t.stackCount);
   return herbs/2+(herbs%2==1 && roll<.5f?1:0);
  }
  public static BridgeState State=>GameComp<BridgeState>.Get();
  static GeneDef elfGene,lichGene;static ThingDef trollBlood;
  static readonly DefRef<ThingDef> trollBloodRef=new DefRef<ThingDef>("LP_TrollBlood");
  public static ThingDef TrollBlood=>trollBlood??(trollBlood=trollBloodRef.Def); // 1.58.6：查不到时也不再每次查 DefDatabase
  public static bool IsElf(Pawn p)=>p?.genes?.Xenotype?.defName=="LP_Elf" || CommonRaces.Has(p,elfGene??(elfGene=DefDatabase<GeneDef>.GetNamedSilentFail("LP_ElfBloodline")));
  public static bool IsLich(Pawn p)=>Necro.Lich(p)!=null || CommonRaces.Has(p,lichGene??(lichGene=DefDatabase<GeneDef>.GetNamedSilentFail("LP_LichGene")));
  // 种族按基因判断：血裔用 BloodlineAPI，DND 种族沿用 1.20.0 的基因判断。
  public static bool Is(Pawn p,string race){
   if(p==null)return false;
   switch(race){
    case "Taotie":return BloodlineAPI.GetBloodline(p)==MythicBloodline.Taotie;
    case "Suanni":return BloodlineAPI.GetBloodline(p)==MythicBloodline.Suanni;
    case "Jumang":return BloodlineAPI.GetBloodline(p)==MythicBloodline.Jumang;
    case "Baize":return BloodlineAPI.GetBloodline(p)==MythicBloodline.Baize;
    case "FireGiant":return FireGiant.Active(p);
    case "ForceDragon":return ForceDragon.Active(p);
    case "Elf":return IsElf(p);
    case "MindFlayer":return MindFlayer.IsMindFlayer(p);
    case "Lich":return IsLich(p);
    case "Troll":return Troll.Is(p);
    case "Dwarf":return CommonRaces.IsDwarf(p);
   }
   return false;
  }
  public static bool IsAcid(DamageDef def)=>def!=null && (Mentions(def.defName,"Acid") || (def.hediff!=null && Mentions(def.hediff.defName,"Acid")));
  public static bool Mentions(string name,string word)=>name!=null && name.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0;

  // 甘霖：熄灭巨魔身上的火后，火焰造成的再生阻断立即解除；酸蚀造成的阻断保留到原定时间。
  static void OnRain(Pawn caster,Pawn victim){
   var gene=Troll.Gene(victim);if(gene==null || !gene.Active || victim.Dead)return;
   int now=Find.TickManager.TicksGame;if(gene.suppressedUntilTick<=now)return;
   int acid=State?.AcidUntil(victim)??0;
   int until=acid>now?Math.Min(gene.suppressedUntilTick,acid):now;
   if(until>=gene.suppressedUntilTick)return;
   gene.suppressedUntilTick=until;
   if(victim.Faction==Faction.OfPlayer)Messages.Message(until>now?"甘霖浇灭了"+victim.LabelShort+"身上的火，但酸蚀仍在阻断巨魔再生。":"甘霖浇灭了"+victim.LabelShort+"身上的火，巨魔再生恢复了。",victim,MessageTypeDefOf.PositiveEvent,false);
  }
  static void OnTaotieAte(Pawn eater,Thing food,ThingDef def){
   if(eater==null || eater.Dead)return;
   if(def!=null && def==TrollBlood)Give(eater,"LPMB_TrollBloodRegen");
   switch(State?.MeatSource(food)){
    case "FireGiant":Give(eater,"LPMB_FireGiantFlesh");break;
    case "ForceDragon":Give(eater,"LPMB_DragonFlesh");break;
   }
  }
  // 重复进食只刷新时间，不叠加。
  public static Hediff Give(Pawn p,string name){
   var def=DefDatabase<HediffDef>.GetNamedSilentFail(name);if(def==null || p.health==null)return null;
   var h=p.health.hediffSet.GetFirstHediffOfDef(def);
   if(h==null){h=p.health.AddHediff(def);if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"获得了"+def.label+"。",p,MessageTypeDefOf.PositiveEvent,false);}
   else{var timer=(h as HediffWithComps)?.TryGetComp<HediffComp_Disappears>();if(timer!=null)timer.ticksToDisappear=timer.Props.disappearsAfterTicks.max;}
   return h;
  }
  static void OnQualityImproved(Pawn caster,Thing item,QualityCategory quality){State?.MarkMasterwork(item);}
  // 识破：夺心魔的“支配人类”本身是原版精神状态，由神兽血裔按原版净化；这里只处理没有精神状态时的夺心魔控制：
  // 幽暗殖巢奴仆的“被支配”（LP_Thralled）按斩首规则解放；精神状态结束后残留的“被支配”状态（LP_Dominated）按原有结束流程移除。
  static bool OnReveal(Pawn caster,Pawn target,bool execute){
   if(target==null || target.Dead || target.health==null)return false;
   if(FactionGroups.IsThralled(target)){
    if(execute){Decapitation.Free(target);Messages.Message(caster.LabelShort+"识破了夺心魔的控制，"+target.LabelShort+"清醒过来。",target,MessageTypeDefOf.PositiveEvent,false);}
    return true;
   }
   var dominated=target.health.hediffSet.GetFirstHediffOfDef(MindFlayerPowers.DominatedHediff);
   if(dominated!=null){if(execute)target.health.RemoveHediff(dominated);return true;}
   return false;
  }
 }

 // 存档数据：天工点化标记、火巨人/力能龙肉的来源、巨魔酸蚀阻断时间。地图普查不存档。
 public class BridgeState : GameComponent {
  HashSet<Thing> masterworks=new HashSet<Thing>();
  Dictionary<Thing,string> meat=new Dictionary<Thing,string>();
  Dictionary<string,int> acid=new Dictionary<string,int>();
  List<Thing> meatKeys;List<string> meatValues;
  readonly Dictionary<int,Census> census=new Dictionary<int,Census>();
  public BridgeState(Game game){}
  public bool AnyMasterworks=>masterworks.Count>0;
  public IEnumerable<Thing> Masterworks=>masterworks;
  public void MarkMasterwork(Thing t){if(t!=null)masterworks.Add(t);}
  public bool IsMasterwork(Thing t)=>t!=null && masterworks.Contains(t);
  public bool AnyMeat=>meat.Count>0;
  public string MeatSource(Thing t)=>t!=null && meat.TryGetValue(t,out var s)?s:null;
  public void MarkMeat(Thing t,string race){if(t!=null && race!=null)meat[t]=race;}
  public int AcidUntil(Pawn p)=>p!=null && acid.TryGetValue(p.GetUniqueLoadID(),out int t)?t:0;
  public void RecordAcid(Pawn p,int until){if(p!=null)acid[p.GetUniqueLoadID()]=Math.Max(AcidUntil(p),until);}
  public Census CensusOf(Map map){
   if(map==null)return null;int now=Find.TickManager.TicksGame;
   if(!census.TryGetValue(map.uniqueID,out var c))census[map.uniqueID]=c=new Census();
   if(now-c.tick>=250 || now<c.tick)c.Rebuild(map);
   return c;
  }
  public void InvalidateCensus()=>census.Clear();
  // 既不在地图上也不在任何容器里的物品不会被存档，存它们的引用读档时会变成空键。
  static bool Gone(Thing t)=>t==null || t.Destroyed || t.Discarded || (!t.Spawned && t.holdingOwner==null);
  public override void ExposeData(){
   base.ExposeData();
   if(Scribe.mode==LoadSaveMode.Saving){
    masterworks.RemoveWhere(Gone);
    foreach(var t in meat.Keys.Where(Gone).ToList())meat.Remove(t);
    int now=Find.TickManager.TicksGame;foreach(var k in acid.Where(e=>e.Value<=now).Select(e=>e.Key).ToList())acid.Remove(k);
   }
   Scribe_Collections.Look(ref masterworks,"mbMasterworks",LookMode.Reference);
   Scribe_Collections.Look(ref meat,"mbMeatSources",LookMode.Reference,LookMode.Value,ref meatKeys,ref meatValues);
   Scribe_Collections.Look(ref acid,"mbTrollAcid",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    masterworks=masterworks==null?new HashSet<Thing>():new HashSet<Thing>(masterworks.Where(t=>t!=null));
    meat=meat==null?new Dictionary<Thing,string>():meat.Where(e=>e.Key!=null).ToDictionary(e=>e.Key,e=>e.Value);
    if(acid==null)acid=new Dictionary<string,int>();
   }
  }
 }
 // 每张地图的种族普查（本殖民地成员和囚犯），最多每250刻重建。一人可以同时属于多个种族。
 public class Census {
  public int tick=-99999;
  readonly Dictionary<string,int> counts=new Dictionary<string,int>();
  public static readonly string[] Races={"Taotie","Suanni","Jumang","Baize","FireGiant","ForceDragon","Elf","MindFlayer","Lich","Troll","Dwarf"};
  public static bool Counted(Pawn p)=>p!=null && !p.Dead && p.RaceProps.Humanlike && (p.Faction==Faction.OfPlayer || p.IsPrisonerOfColony);
  public int Count(string race)=>counts.TryGetValue(race,out int n)?n:0;
  public void Rebuild(Map map){
   tick=Find.TickManager.TicksGame;counts.Clear();
   foreach(var p in map.mapPawns.AllHumanlikeSpawned){
    if(!Counted(p))continue;
    foreach(var r in Races)if(MythicBridge.Is(p,r))counts[r]=Count(r)+1;
   }
  }
 }

 public class MythicRelation : DefModExtension {public string observer,target;}
 // 种族关系心情（按基因）：observer 同地图有 target 种族的本殖民地成员或囚犯（不含自己）时生效。
 public class ThoughtWorker_MythicRelation : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   var r=def.GetModExtension<MythicRelation>();
   if(r==null || p==null || !p.Spawned || p.Map==null || !MythicBridge.Is(p,r.observer))return ThoughtState.Inactive;
   var c=MythicBridge.State?.CensusOf(p.Map);if(c==null)return ThoughtState.Inactive;
   int others=c.Count(r.target)-(Census.Counted(p) && MythicBridge.Is(p,r.target)?1:0);
   return others>0?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
  }
 }

 // 饕餮饮下巨魔血：巨魔再生的四分之一速度（只愈合伤口与失血），不长肢体、不复活；着火时暂停。
 public class Hediff_LesserTrollRegen : HediffWithComps {
  public const float Speed=.25f;
  int accumulated;
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   accumulated+=delta;if(accumulated<250)return;
   int elapsed=accumulated;accumulated=0;
   if(pawn==null || pawn.Dead || (pawn.Spawned && pawn.IsBurning()))return;
   Heal(elapsed);
  }
  public void Heal(int elapsed){
   var set=pawn.health.hediffSet;float budget=.5f*Speed*elapsed/250f;
   foreach(var injury in set.hediffs.OfType<Hediff_Injury>().Where(h=>!h.IsPermanent()).OrderByDescending(h=>h.BleedRate>0).ThenByDescending(h=>h.Severity).ToList()){float heal=Math.Min(injury.Severity,budget);injury.Heal(heal);budget-=heal;if(budget<=0)break;}
   var blood=set.GetFirstHediffOfDef(HediffDefOf.BloodLoss);if(blood!=null)blood.Severity-=Math.Min(blood.Severity,.01f*Speed*elapsed/250f);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref accumulated,"mbRegenTicks",0);}
 }
 // 火巨人/力能龙之肉：对应伤害乘以 factor。名单写在 XML。
 public class DamageFactorExtension : DefModExtension {
  public float factor=.5f;
  public List<string> damageDefs=new List<string>();
  public List<string> keywords=new List<string>();
  public List<string> excludedKeywords=new List<string>();
  public bool Matches(DamageDef d){
   if(d==null)return false;
   if(excludedKeywords.Any(k=>MythicBridge.Mentions(d.defName,k)))return false;
   return damageDefs.Contains(d.defName) || keywords.Any(k=>MythicBridge.Mentions(d.defName,k));
  }
 }

 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class FleshDamageFactor {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   var list=__instance?.health?.hediffSet?.hediffs;if(list==null)return;
   for(int i=0;i<list.Count;i++){var e=list[i].def.GetModExtension<DamageFactorExtension>();if(e!=null && e.Matches(dinfo.Def))dinfo.SetAmount(dinfo.Amount*e.factor);}
  }
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.PostApplyDamage))]
 public static class TrollAcidRecord {
  static void Postfix(Pawn ___pawn,DamageInfo dinfo,float totalDamageDealt){
   if(totalDamageDealt<=0 || !MythicBridge.IsAcid(dinfo.Def) || !Troll.Is(___pawn))return;
   MythicBridge.State?.RecordAcid(___pawn,Find.TickManager.TicksGame+Gene_TrollRegeneration.FireSuppressionTicks);
  }
 }
 // 屠宰火巨人、力能龙得到的肉记下来源；来源不同的肉不合并，拆分时保留来源。
 [HarmonyPatch(typeof(Corpse),nameof(Corpse.ButcherProducts))]
 public static class MarkButcheredMeat {
  static IEnumerable<Thing> Postfix(IEnumerable<Thing> values,Corpse __instance){
   var p=__instance?.InnerPawn;string race=p==null?null:FireGiant.Active(p)?"FireGiant":ForceDragon.Active(p)?"ForceDragon":null;
   foreach(var t in values){if(race!=null && t!=null && t.def==p.RaceProps.meatDef)MythicBridge.State?.MarkMeat(t,race);yield return t;}
  }
 }
 [HarmonyPatch(typeof(ThingWithComps),nameof(ThingWithComps.CanStackWith))]
 public static class MeatSourceStacking {
  static void Postfix(Thing __instance,Thing other,ref bool __result){
   if(!__result)return;var s=MythicBridge.State;if(s==null || !s.AnyMeat)return;
   if(s.MeatSource(__instance)!=s.MeatSource(other))__result=false;
  }
 }
 [HarmonyPatch(typeof(Thing),nameof(Thing.SplitOff))]
 public static class MeatSourceSplit {
  static void Postfix(Thing __instance,Thing __result){
   if(__result==null || __result==__instance)return;var s=MythicBridge.State;if(s==null || !s.AnyMeat)return;
   var source=s.MeatSource(__instance);if(source!=null)s.MarkMeat(__result,source);
  }
 }
 // 宝库：天工点化过的物品按 110% 计价（在原有市值和火巨人锻造品双倍之后）。
 [HarmonyPatch(typeof(DragonHoard),nameof(DragonHoard.Value))]
 public static class MasterworkHoardValue {
  public const float Bonus=.1f;
  static void Postfix(Room room,ref float __result){
   var s=MythicBridge.State;if(__result<=0 || s==null || !s.AnyMasterworks || !DragonHoard.Valid(room))return;
   foreach(var t in s.Masterworks){
    Thing item=t;if(item==null || item.Destroyed)continue;
    if(!item.Spawned && item.ParentHolder is MinifiedThing m)item=m;
    if(item.Spawned && item.Map==room.Map && item.GetRoom()==room && DragonHoard.Counts(item))__result+=Bonus*DragonHoard.ItemValue(item);
   }
  }
 }
 // 句芒做药水：退还的草药作为额外产物放在炼金桌旁。
 [HarmonyPatch(typeof(GenRecipe),nameof(GenRecipe.MakeRecipeProducts))]
 public static class JumangHerbSaving {
  static IEnumerable<Thing> Postfix(IEnumerable<Thing> values,RecipeDef recipeDef,Pawn worker,List<Thing> ingredients){
   foreach(var t in values)yield return t;
   int refund=MythicBridge.HerbRefund(recipeDef,worker,ingredients,Rand.Value);
   if(refund<=0)yield break;
   var herbs=ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);herbs.stackCount=refund;yield return herbs;
  }
 }
 // 巨魔血只有饕餮会喝：其他人不会把它当食物。
 // 1.58.6：原来用 object[] __args，Harmony 每次调用（食物搜索里每个候选食物一次）都要分配参数数组；拆成两个按类型绑定参数的补丁，判断不变。
 [HarmonyPatch]
 public static class TrollBloodOnlyForTaotie {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){
   return typeof(FoodUtility).GetMethods().Where(m=>m.Name=="WillEat" && m.ReturnType==typeof(bool) && m.GetParameters().Length>=2 && m.GetParameters()[0].ParameterType==typeof(Pawn) && m.GetParameters()[1].ParameterType==typeof(Thing));
  }
  static void Postfix(Pawn __0,Thing __1,ref bool __result){if(__result && MythicBridge.TrollBlood!=null)Apply(__0,__1?.def,ref __result);}
  public static void Apply(Pawn p,ThingDef def,ref bool result){if(def==MythicBridge.TrollBlood && !MythicBridge.Is(p,"Taotie"))result=false;}
 }
 [HarmonyPatch]
 public static class TrollBloodOnlyForTaotieDef {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){
   return typeof(FoodUtility).GetMethods().Where(m=>m.Name=="WillEat" && m.ReturnType==typeof(bool) && m.GetParameters().Length>=2 && m.GetParameters()[0].ParameterType==typeof(Pawn) && m.GetParameters()[1].ParameterType==typeof(ThingDef));
  }
  static void Postfix(Pawn __0,ThingDef __1,ref bool __result){if(__result && MythicBridge.TrollBlood!=null)TrollBloodOnlyForTaotie.Apply(__0,__1,ref __result);}
 }
}
