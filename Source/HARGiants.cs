using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.44.0 双头巨人、独眼巨人（Humanoid Alien Races 可选联动，照 1.33.0 六臂蛇人）。种族、身体、基因、物品、贴图都在 Bridges/HAR（只在启用 HAR 时加载）；
 // 这里是不需要 HAR 类型的规则：种族识别（GiantRaceExtension）、双头的意识与死亡、孤首、轮流值守、两头不和、独眼、凝视免疫、单眼视界、匠心、牧者、透镜、独眼巨锤，
 // 以及迁徙、塞伊双头蛮兵、同盟商队的独眼巨人护卫。没装 HAR 时这些定义都不存在，所有查询用 GetNamedSilentFail，钩子直接返回。
 // 两族都是巨人：本源基因挂 GiantKinExtension（rank 5 / 6，adultSize 1）——体型只来自种族 baseBodySize，基因不再放大体型。

 // 种族自带的体型（HAR 种族 baseBodySize，不走 get_BodySize 补丁）：巨躯规则、整体渲染缩放、胃容量按它算；儿童随年龄长大（同巨人血统的成长曲线）。
 public class RaceSizeExtension : DefModExtension {public float adultSize=1f,renderScale=1f,stomach=1f;}
 public class GiantRaceExtension : RaceSizeExtension {public string kind;}
 public static class RaceSize {
  static readonly ConcurrentDictionary<ThingDef,RaceSizeExtension> exts=new ConcurrentDictionary<ThingDef,RaceSizeExtension>();
  public static RaceSizeExtension Ext(ThingDef d){if(d==null)return null;return exts.GetOrAdd(d,x=>x.GetModExtension<RaceSizeExtension>());}
  static float Grow(Pawn p,float adult)=>1f+(adult-1f)*GiantKin.Growth(p);
  public static float SizeFactor(Pawn p){var e=Ext(p?.def);return e==null?1f:Grow(p,e.adultSize);}
  public static float RenderFactor(Pawn p){var e=Ext(p?.def);return e==null?1f:Grow(p,e.renderScale);}
  public static float Stomach(Pawn p){var e=Ext(p?.def);return e==null?1f:Grow(p,e.stomach);}
 }
 public static class GiantRace {
  public static string Kind(Pawn p)=>(RaceSize.Ext(p?.def) as GiantRaceExtension)?.kind;
  // 外观基因（两种肤色）不放进异种型：生成时随机一种当内源基因，遗传时随父母（同六臂蛇人鳞色）。
  public static GeneDef EnsureSkin(Pawn p,string[] skins){
   if(p?.genes==null)return null;
   var defs=skins.Select(n=>Races124.Gene(n)).ToArray();if(defs.Any(d=>d==null))return null;
   var have=defs.FirstOrDefault(d=>p.genes.GetGene(d)!=null);if(have!=null)return have;
   var pick=defs.RandomElement();p.genes.AddGene(pick,false);return pick;
  }
  public static void Ensure(Pawn p){
   switch(Kind(p)){case "ettin":EnsureSkin(p,Ettin.SkinGenes);Ettin.Sync(p);break;case "cyclops":EnsureSkin(p,Cyclops.SkinGenes);Cyclops.EnsureHead(p);break;}
  }
 }
 // 种族组件：生成、读档、受伤、每 250 刻确认孤首状态。
 public class CompProperties_HARGiant : CompProperties {public CompProperties_HARGiant(){compClass=typeof(CompHARGiant);}}
 public class CompHARGiant : ThingComp {
  int ticks;
  Pawn Pawn=>parent as Pawn;
  public override void PostSpawnSetup(bool respawningAfterLoad){base.PostSpawnSetup(respawningAfterLoad);Ettin.Sync(Pawn);Cyclops.EnsureHead(Pawn);}
  public override void PostPostApplyDamage(DamageInfo dinfo,float totalDamageDealt){base.PostPostApplyDamage(dinfo,totalDamageDealt);Ettin.Sync(Pawn);}
  public override void CompTickInterval(int delta){base.CompTickInterval(delta);if((ticks+=delta)<250)return;ticks=0;Ettin.Sync(Pawn);}
  public override void PostExposeData(){base.PostExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit && Pawn?.health!=null)Ettin.Sync(Pawn);}
 }
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class HARGiantGenerated {
  static void Postfix(Pawn __result){if(__result!=null && GiantRace.Kind(__result)!=null)GiantRace.Ensure(__result);}
 }
 // 两族巨人本源：拿掉时放下巨人专属衣甲和武器（同霜巨人）。
 public class Gene_HARGiant : Gene {
  public override void PostAdd(){
   base.PostAdd();GiantKin.Invalidate(pawn);
   if(pawn?.apparel!=null)foreach(var a in pawn.apparel.WornApparel.ToArray())if(!GiantGear.CanWear(pawn,a.def))pawn.apparel.TryDrop(a);
  }
  public override void PostRemove(){GiantGear.Release(pawn);base.PostRemove();}
 }

 // ==================== 双头巨人 ====================
 public static class Ettin {
  public const string Race="LP_Ettin",Core="LP_EttinCore",WatchGene="LP_EttinWatch",GripGene="LP_EttinTwinGrip",QuarrelGene="LP_EttinQuarrel",LoneHead="LP_EttinLoneHead",Arguing="LP_EttinArguing",SecondHead="LP_EttinHead",Helm="LP_EttinTwinHelm",Brute="LP_FH_EttinBrute";
  public static readonly string[] SkinGenes={"LP_SkinEttinGrey","LP_SkinEttinOchre"};
  public const float ConsciousnessFloor=.6f,WatchRest=.5f,WatchRadius=10f,QuarrelChance=.1f,QuarrelMoodCap=.7f,Appetite=4f;
  public const int WatchInterval=250,QuarrelDays=15;
  public static ThingDef RaceDef=>DefDatabase<ThingDef>.GetNamedSilentFail(Race);
  public static bool Is(Pawn p)=>GiantRace.Kind(p)=="ettin";
  // 两个颈（各连一个头）：主头是原版 Head（左头），第二个头是 LP_EttinHead（右头）。按 BodyDef 缓存。
  static readonly ConcurrentDictionary<BodyDef,BodyPartRecord[]> necks=new ConcurrentDictionary<BodyDef,BodyPartRecord[]>();
  static bool IsHead(BodyPartRecord r)=>r.def==BodyPartDefOf.Head || r.def.defName==SecondHead;
  public static BodyPartRecord[] Necks(Pawn p){var b=p?.RaceProps?.body;if(b==null)return new BodyPartRecord[0];return necks.GetOrAdd(b,x=>x.corePart.parts.Where(r=>r.parts.Any(IsHead)).ToArray());}
  public static BodyPartRecord HeadOf(BodyPartRecord neck)=>neck?.parts.FirstOrDefault(IsHead);
  public static BodyPartRecord BrainOf(BodyPartRecord neck){var h=HeadOf(neck);return h==null?null:Under(h).FirstOrDefault(x=>x.def.tags.Contains(BodyPartTagDefOf.ConsciousnessSource));}
  static IEnumerable<BodyPartRecord> Under(BodyPartRecord r){yield return r;foreach(var c in r.parts)foreach(var x in Under(c))yield return x;}
  static bool Within(BodyPartRecord part,BodyPartRecord root){for(var r=part;r!=null;r=r.parent)if(r==root)return true;return false;}
  // 一个头“没了”：颈、头或这个头里的大脑缺失。
  public static bool HeadLost(HediffSet set,BodyPartRecord neck){if(set.PartIsMissing(neck))return true;var h=HeadOf(neck);if(h==null || set.PartIsMissing(h))return true;var b=BrainOf(neck);return b==null || set.PartIsMissing(b);}
  public static int HeadsLeft(Pawn p){var set=p?.health?.hediffSet;if(set==null)return 0;int n=0;foreach(var k in Necks(p))if(!HeadLost(set,k))n++;return n;}
  public static bool TwoHeaded(Pawn p)=>Is(p) && Necks(p).Length>=2;
  public static bool BothHeads(Pawn p)=>TwoHeaded(p) && HeadsLeft(p)>=2;
  public static bool Lone(Pawn p)=>TwoHeaded(p) && HeadsLeft(p)==1;

  // 原版 PawnCapacityUtility.CalculateTagEfficiency：同一标签的部位按效率平均（bestPartEfficiencySpecialWeight 给最好的那个加权）。
  // 意识（两颗大脑）：照原版平均，但只要还有一颗大脑，下限 = 60% × 这颗大脑的效率；两颗都没了才是 0（原版判死）。
  // 其他标签（视力、听力、进食、说话、呼吸通道）：失去一个头以后只算剩下那个头（含颈），剩下的头照常人一样工作；孤首另有视力 ×75%。
  public static float? TagEfficiency(HediffSet set,BodyPartTagDef tag,float maximum,FloatRange lerp,float weight,float vanilla){
   var p=set?.pawn;if(tag==null || !TwoHeaded(p))return null;
   var all=p.RaceProps.body.GetPartsWithTag(tag);if(all.Count<2)return null;
   if(tag==BodyPartTagDefOf.ConsciousnessSource){
    float best=0f;foreach(var b in all)if(!set.PartIsMissing(b))best=Mathf.Max(best,PawnCapacityUtility.CalculatePartEfficiency(set,b));
    float floor=Mathf.Min(maximum,ConsciousnessFloor*Mathf.Min(1f,best));
    return vanilla<floor?floor:(float?)null;
   }
   var lost=Necks(p).Where(k=>HeadLost(set,k)).ToList();if(lost.Count!=1)return null;
   var kept=all.Where(x=>!Within(x,lost[0])).ToList();if(kept.Count==0 || kept.Count==all.Count)return null;
   float sum=0f,top=0f;foreach(var x in kept){float e=PawnCapacityUtility.CalculatePartEfficiency(set,x);sum+=e;top=Mathf.Max(top,e);}
   float v=weight>=0f && kept.Count>=2?top*weight+(sum-top)/(kept.Count-1)*(1f-weight):sum/kept.Count;
   if(lerp!=default(FloatRange))v=lerp.LerpThroughRange(v);
   return Mathf.Min(v,maximum);
  }
  // 孤首：只剩一个头时加上（永久，不可治疗），两个头都在时去掉。
  public static void Sync(Pawn p){
   if(p?.health?.hediffSet==null || p.Dead || !TwoHeaded(p))return;
   var def=Items.Hediff(LoneHead);if(def==null)return;
   var h=p.health.hediffSet.GetFirstHediffOfDef(def);bool lone=HeadsLeft(p)==1;
   if(lone && h==null)p.health.AddHediff(def);else if(!lone && h!=null)p.health.RemoveHediff(h);
  }

  // ---------- 轮流值守：睡眠下降 ×0.5；睡着时 10 格内有敌人立刻醒（需要两个头都在，人类装上无效） ----------
  static readonly DefRef<GeneDef> watchGene=new DefRef<GeneDef>(WatchGene); // 1.58.6：静态 Def（没装 HAR 时为 null，直接返回）
  public static bool WatchActive(Pawn p)=>CommonRaces.Has(p,watchGene.Def) && BothHeads(p);
  public static bool FoeNear(Pawn p)=>p?.Map!=null && Races124.PawnsNear(p.Map,p.Position,WatchRadius).Any(o=>o!=p && !o.Dead && !o.Downed && o.HostileTo(p));
  public static bool TryWake(Pawn p){
   if(!WatchActive(p) || !p.Spawned || RestUtility.Awake(p) || !FoeNear(p))return false;
   RestUtility.WakeUp(p,true);Items.Mote(p,"轮流值守：有敌人！",new Color(.95f,.75f,.4f));return true;
  }

  // ---------- 两头不和：轻度崩溃“内讧”，另每 15 天 10% 自己吵起来（心情高于 70% 不吵；需要两个头都在） ----------
  public static MentalStateDef ArguingDef=>DefDatabase<MentalStateDef>.GetNamedSilentFail(Arguing);
  public static bool CanQuarrel(Pawn p)=>Races124.Has(p,QuarrelGene) && BothHeads(p) && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState && p.mindState?.mentalStateHandler!=null;
  public static bool StartQuarrel(Pawn p,string reason){var def=ArguingDef;if(def==null || !CanQuarrel(p))return false;return p.mindState.mentalStateHandler.TryStartMentalState(def,reason,forced:true);}
  public static readonly string[] Lines={"左头：往东！","右头：往西！","“是我先看见的！”","“闭嘴，该我说了！”","两个头互相瞪眼","“都怪你！”"};
 }
 [HarmonyPatch(typeof(PawnCapacityUtility),nameof(PawnCapacityUtility.CalculateTagEfficiency))]
 public static class EttinTagEfficiency {
  static void Postfix(HediffSet __0,BodyPartTagDef __1,float __2,FloatRange __3,float __5,ref float __result){var v=Ettin.TagEfficiency(__0,__1,__2,__3,__5,__result);if(v.HasValue)__result=v.Value;}
 }
 public class StatPart_EttinWatch : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Ettin.WatchActive(p))val*=Ettin.WatchRest;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Ettin.WatchActive(p)?"轮流值守：×"+Ettin.WatchRest.ToStringPercent():null;
 }
 public class Gene_EttinWatch : Gene {
  int ticks;
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<Ettin.WatchInterval)return;ticks=0;if(Active)Ettin.TryWake(pawn);}
 }
 public class Gene_EttinQuarrel : Gene {
  public int nextRoll=-1;int ticks;
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<2500)return;ticks=0;if(Active)Roll(null);}
  // 每 15 天判定一次（第一次在 0～15 天内随机，免得一群双头巨人同一天吵）。
  public bool Roll(float? roll){
   int now=Find.TickManager.TicksGame;
   if(nextRoll<0){nextRoll=now+Rand.Range(1,Ettin.QuarrelDays*GenDate.TicksPerDay);return false;}
   if(now<nextRoll)return false;
   nextRoll=now+Ettin.QuarrelDays*GenDate.TicksPerDay;
   if((roll??Rand.Value)>=Ettin.QuarrelChance)return false;
   if((pawn.needs?.mood?.CurLevelPercentage??0f)>Ettin.QuarrelMoodCap || !RestUtility.Awake(pawn))return false;
   return Ettin.StartQuarrel(pawn,"两个头吵起来了");
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref nextRoll,"lpQuarrelNext",-1);}
 }
 public class MentalBreakWorker_EttinQuarrel : MentalBreakWorker {
  public override bool BreakCanOccur(Pawn pawn)=>Races124.Has(pawn,Ettin.QuarrelGene) && Ettin.BothHeads(pawn) && base.BreakCanOccur(pawn);
 }
 // 内讧：站在原地吵 1 小时，不干活（思考树见 Bridges/HAR/Patches：原地站着 JobGiver_StandAndBeSociallyActive）。
 public class MentalState_EttinArguing : MentalState {
  int ticks;
  public override void MentalStateTick(int delta){base.MentalStateTick(delta);if((ticks+=delta)<300)return;ticks=0;if(pawn.Spawned)Items.Mote(pawn,Ettin.Lines.RandomElement(),new Color(.95f,.6f,.5f));}
 }
 public class ThoughtWorker_EttinLoneHead : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>Ettin.Lone(p) && Items.Has(p,Ettin.LoneHead)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }

 // ==================== 独眼巨人 ====================
 public static class Cyclops {
  public const string Race="LP_Cyclops",Core="LP_CyclopsCore",CraftGene="LP_CyclopsCraft",ShepherdGene="LP_CyclopsShepherd",FlatSightGene="LP_CyclopsFlatSight",Lens="LP_CyclopsLens",Maul="LP_CyclopsMaul",Eye="LP_CyclopsEye",Guard="LP_FC_CyclopsGuard";
  public static readonly string[] SkinGenes={"LP_SkinCyclopsRuddy","LP_SkinCyclopsOlive"};
  public static readonly HashSet<string> Flock=new HashSet<string>{"Sheep","Goat"};
  // 单眼视界：原版射击精度按“点数”加减再查表（触发欢 -5、谨慎 +5），-3.5 点 ≈ 射击 10 级、20 格时命中率 ×0.70；戴独眼透镜 -1.75 点 ≈ ×0.85。
  public const float FlatSightPoints=-3.5f,LensPoints=-1.75f,SmithSpeed=1.15f,MaulStunChance=.15f;
  public const int MaulStunTicks=60;
  public static float? TestRoll;
  public static ThingDef RaceDef=>DefDatabase<ThingDef>.GetNamedSilentFail(Race);
  public static bool Is(Pawn p)=>GiantRace.Kind(p)=="cyclops";
  // 免疫“凝视”类效果（巫妖恐惧凝视、恐惧凝视卷轴、半巫妖与拉洛克化身的凝视，都走 LichSpells.Frighten）。
  public static bool GazeImmune(Pawn p)=>Races124.Has(p,Core);
  static readonly DefRef<GeneDef> flatGene=new DefRef<GeneDef>(FlatSightGene),craftGene=new DefRef<GeneDef>(CraftGene); // 1.58.6
  static readonly System.Collections.Concurrent.ConcurrentDictionary<RecipeDef,bool> smithRecipes=new System.Collections.Concurrent.ConcurrentDictionary<RecipeDef,bool>();
  public static float SightPoints(Pawn p)=>!CommonRaces.Has(p,flatGene.Def)?0f:Items.Worn(p,Lens)!=null?LensPoints:FlatSightPoints;
  // 锻造类配方：锻造台、熔火锻炉、符文铁砧、巨人铁砧上的活，或者原版“锻造”效果。
  public static bool Smithing(RecipeDef r)=>r!=null && smithRecipes.GetOrAdd(r,x=>x.effectWorking?.defName=="Smith" || x.AllRecipeUsers.Any(u=>u.defName.Contains("Smithy") || u.defName.Contains("Forge") || u.defName.Contains("Anvil"))); // 1.58.6：按配方记住
  public static bool SmithBonus(Pawn p)=>CommonRaces.Has(p,craftGene.Def) && Smithing(p.CurJob?.bill?.recipe);
  public static bool HasFlock(Map map)=>map!=null && map.mapPawns.SpawnedColonyAnimals.Any(a=>Flock.Contains(a.def.defName));
  // 独眼巨人头型带 requiredGenes（原版人类随机不到）；原版挑头时种族基因还没加上，所以生成后在这里补一个同性别的独眼头。
  public static bool EnsureHead(Pawn p){
   var s=p?.story;if(s==null || !Is(p) || s.headType?.defName.StartsWith("LP_Cyclops")==true)return false;
   var opts=DefDatabase<HeadTypeDef>.AllDefsListForReading.Where(h=>h.defName.StartsWith("LP_Cyclops") && (h.gender==Gender.None || h.gender==p.gender || p.gender==Gender.None)).ToList();
   if(opts.Count==0)return false;
   s.headType=opts.RandomElement();p.Drawer?.renderer?.SetAllGraphicsDirty();return true;
  }
  public static bool Stun(Pawn caster,Pawn target){
   if(target==null || target.Dead || (TestRoll??Rand.Value)>=MaulStunChance)return false;
   target.stances?.stunner?.StunFor(MaulStunTicks,caster);return true;
  }
 }
 public class StatPart_CyclopsSight : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p)val+=Cyclops.SightPoints(p);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;float s=Cyclops.SightPoints(p);return s==0f?null:"单眼视界"+(Items.Worn(p,Cyclops.Lens)!=null?"（独眼透镜）":"")+"："+s.ToString("0.##");}
 }
 public class StatPart_CyclopsSmith : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Cyclops.SmithBonus(p))val*=Cyclops.SmithSpeed;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Cyclops.SmithBonus(p)?"独眼匠心（锻造）：×"+Cyclops.SmithSpeed.ToStringPercent():null;
 }
 public class ThoughtWorker_CyclopsFlock : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.Spawned && Races124.Has(p,Cyclops.ShepherdGene) && p.Map.IsPlayerHome && Cyclops.HasFlock(p.Map)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class CyclopsMaulStun {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){if(__instance.EquipmentSource?.def.defName==Cyclops.Maul)Cyclops.Stun(__instance.CasterPawn,target.Pawn);}
 }
 // 只有带某个基因的人能穿戴（独眼透镜：独眼巨人本源）。由 GiantGear.CanWear 一并检查。
 public class GeneWearExtension : DefModExtension {public string gene,label;}
 [StaticConstructorOnStartup]
 public static class HARGiantsStartup {
  static HARGiantsStartup(){
   void Add<T>(StatDef stat) where T:StatPart,new(){if(stat==null)return;if(stat.parts==null)stat.parts=new List<StatPart>();if(!stat.parts.Any(x=>x is T))stat.parts.Add(new T{parentStat=stat});}
   Add<StatPart_EttinWatch>(StatDefOf.RestFallRateFactor);
   Add<StatPart_CyclopsSight>(StatDefOf.ShootingAccuracyPawn);
   Add<StatPart_CyclopsSmith>(StatDefOf.GeneralLaborSpeed);
  }
 }

 // ==================== 出场 ====================
 // 塞伊远征军：≥800 点的战斗袭击 10% 带一名双头蛮兵（独眼巨锤或棍棒、双盔），顶掉 2 名普通随从。独眼巨人不进敌对派系。
 public static class EttinRaid {
  public const float MinPoints=800f,Chance=.1f;
  public static float? TestRoll;
  public static PawnKindDef Kind=>DefDatabase<PawnKindDef>.GetNamedSilentFail(Ettin.Brute);
  public static int Apply(List<Pawn> list,PawnGroupMakerParms parms){
   var k=Kind;if(k==null || list==null || parms==null)return 0;
   if(parms.points<MinPoints){foreach(var p in list.Where(x=>x.kindDef==k).ToList())Discard(list,p);return 0;}
   if(parms.groupKind!=PawnGroupKindDefOf.Combat || list.Any(x=>x.kindDef==k) || (TestRoll??Rand.Value)>=Chance)return 0;
   var m=PawnGenerator.GeneratePawn(new PawnGenerationRequest(k,parms.faction,PawnGenerationContext.NonPlayer,parms.tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true,inhabitant:parms.inhabitants));
   list.Insert(0,m);Arm(m,true);FrostGiantRaid.Spare(list,2);
   return 1;
  }
  // 独眼巨锤（钢）或棍棒（木）各一半；helm：戴双盔。
  public static void Arm(Pawn m,bool helm){
   if(m?.equipment==null)return;
   var maul=Items.Thing(Cyclops.Maul);var club=DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_Club");
   var d=maul!=null && (club==null || Rand.Bool)?maul:club;
   if(d!=null && !m.WorkTagIsDisabled(WorkTags.Violent)){
    m.equipment.DestroyAllEquipment();
    var w=(ThingWithComps)ThingMaker.MakeThing(d,d==maul?ThingDefOf.Steel:ThingDefOf.WoodLog);w.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityTraderItem(),ArtGenerationContext.Outsider);m.equipment.AddEquipment(w);
   }
   var h=Items.Thing(Ettin.Helm);
   if(helm && h!=null && m.apparel!=null && Items.Worn(m,Ettin.Helm)==null){var a=(Apparel)ThingMaker.MakeThing(h);if(ApparelUtility.HasPartsToWear(m,h))m.apparel.Wear(a,false);else a.Destroy();}
  }
  static void Discard(List<Pawn> list,Pawn p){list.Remove(p);if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}
 }
 // 费伦拓荒同盟商队：10% 多一名独眼巨人护卫（拿独眼巨锤）。
 public static class CyclopsGuard {
  public const float Chance=.1f;
  public static float? TestRoll;
  public static PawnKindDef Kind=>DefDatabase<PawnKindDef>.GetNamedSilentFail(Cyclops.Guard);
  public static int Apply(List<Pawn> list,PawnGroupMakerParms parms){
   var k=Kind;if(k==null || list==null || parms==null || parms.groupKind!=PawnGroupKindDefOf.Trader || list.Any(x=>x.kindDef==k) || (TestRoll??Rand.Value)>=Chance)return 0;
   var g=PawnGenerator.GeneratePawn(new PawnGenerationRequest(k,parms.faction,PawnGenerationContext.NonPlayer,parms.tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true,inhabitant:parms.inhabitants));
   list.Add(g);EttinRaid.Arm(g,false);
   return 1;
  }
 }
}
