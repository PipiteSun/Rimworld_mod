using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.33.0 六臂蛇人（Humanoid Alien Races 可选联动）。种族、身体、基因、贴图都在 Bridges/HAR（只在启用 HAR 时加载）；
 // 这里是不需要 HAR 类型的规则：种族识别（MarilithExtension）、六臂操作加成、副手连击、蛇行、冷血、缠绕、剑刃风暴。
 // 没装 HAR 时这些定义都不存在，所有查询用 GetNamedSilentFail，钩子直接返回。
 // 1.44.0：体型、渲染、胃容量字段移到通用的 RaceSizeExtension（Source/HARGiants.cs，双头巨人、独眼巨人共用）。
 public class MarilithExtension : RaceSizeExtension {
  public MarilithExtension(){adultSize=1.6f;renderScale=1.3f;} // 巨躯规则用的成年体型倍率、整体渲染缩放（衣服、额外手臂、蛇尾一起放大），儿童随年龄长大
 }
 // 1.44.0 副手连击改成通用扩展（挂在基因上）：六臂连击 max 2、按额外手臂对数；双头巨人的左右开弓 max 1、要双头巨人的身体且两只手都能用。
 public class OffhandExtension : DefModExtension {public int max=2;public bool perExtraArmPair=true,needsTwinHeads;}
 public static class Marilith {
  public const string Race="LP_Marilith",SixArms="LP_SixArms",Blades="LP_MarilithBlades",Glide="LP_SerpentGlide",ColdBlood="LP_ColdBlood",Constricted="LP_Constricted",Constricting="LP_Constricting";
  // 额外手臂按 customLabel 认（BodyDef LP_MarilithBody）：第 3/4 臂一对、第 5/6 臂一对。
  public static readonly string[] ExtraHands={"左中手","右中手","左下手","右下手"};
  public static readonly string[] SkinGenes={"LP_SkinSerpentTeal","LP_SkinSerpentDark","LP_SkinSerpentGold"};
  static readonly float[] skinWeights={.4f,.35f,.25f};
  public const float ManipPerHand=.1f,ColdMove=.85f,ColdBelow=0f;
  public const int MaxOffhands=2;

  static readonly System.Collections.Concurrent.ConcurrentDictionary<ThingDef,MarilithExtension> exts=new System.Collections.Concurrent.ConcurrentDictionary<ThingDef,MarilithExtension>();
  public static MarilithExtension Ext(ThingDef d){if(d==null)return null;return exts.GetOrAdd(d,x=>x.GetModExtension<MarilithExtension>());}
  public static bool Is(Pawn p)=>p!=null && Ext(p.def)!=null;
  public static ThingDef RaceDef=>DefDatabase<ThingDef>.GetNamedSilentFail(Race);
  public static bool Loaded=>RaceDef!=null;
  // 巨躯规则（BodySize.Size）里的种族倍率：成年 1.6，儿童随年龄长大（与巨人血统同一条成长曲线）。
  public static float SizeFactor(Pawn p)=>Ext(p?.def)==null?1f:RaceSize.SizeFactor(p);
  public static float RenderFactor(Pawn p)=>Ext(p?.def)==null?1f:RaceSize.RenderFactor(p);

  // ---------- 额外手臂 ----------
  static readonly System.Collections.Concurrent.ConcurrentDictionary<BodyDef,BodyPartRecord[]> hands=new System.Collections.Concurrent.ConcurrentDictionary<BodyDef,BodyPartRecord[]>();
  public static BodyPartRecord[] Hands(Pawn p){
   var body=p?.RaceProps?.body;if(body==null)return new BodyPartRecord[0];
   return hands.GetOrAdd(body,b=>ExtraHands.Select(l=>b.AllParts.FirstOrDefault(x=>x.def==BodyPartDefOf.Hand && x.untranslatedCustomLabel==l)).ToArray());
  }
  public static bool HandUsable(Pawn p,BodyPartRecord hand)=>hand!=null && p?.health?.hediffSet!=null && !p.health.hediffSet.PartIsMissing(hand) && PawnCapacityUtility.CalculatePartEfficiency(p.health.hediffSet,hand)>.001f;
  // 能用的额外手（0～4）。
  public static int UsableHands(Pawn p){int n=0;foreach(var h in Hands(p))if(HandUsable(p,h))n++;return n;}
  // 能用的额外手臂对数（0～2）：一对里至少一只手能用就算。
  public static int UsablePairs(Pawn p){var h=Hands(p);if(h.Length<4)return 0;int n=0;for(int i=0;i<4;i+=2)if(HandUsable(p,h[i]) || HandUsable(p,h[i+1]))n++;return n;}
  // 六臂：操作 +0.4 ×（完好额外手臂对数 / 2）。按手算：每只能用的额外手 +0.1，一整对 +0.2，四只都在 +0.4。
  public static float ManipulationBonus(Pawn p)=>Is(p)?ManipPerHand*UsableHands(p):0f;
  public static void EnsureSixArms(Pawn p){
   var def=Items.Hediff(SixArms);if(def==null || !Is(p) || p.health?.hediffSet==null || p.Dead)return;
   if(!p.health.hediffSet.HasHediff(def))p.health.AddHediff(def);
  }
  // 鳞色基因（青绿/墨绿/赤金）随机一种作为内源基因（遗传时随父母）；已有就不动。
  public static GeneDef EnsureSkin(Pawn p){
   if(!Is(p) || p.genes==null)return null;
   var defs=SkinGenes.Select(n=>Races124.Gene(n)).ToArray();if(defs.Any(d=>d==null))return null;
   var have=defs.FirstOrDefault(d=>p.genes.GetGene(d)!=null);if(have!=null)return have;
   float r=Rand.Value;int i=0;for(;i<defs.Length-1;i++){if(r<skinWeights[i])break;r-=skinWeights[i];}
   p.genes.AddGene(defs[i],false);return defs[i];
  }

  // ---------- 副手连击 ----------
  public static bool BladesActive(Pawn p)=>Races124.Has(p,Blades) && !DualWield;
  // 副手数 = 各个带 OffhandExtension 的生效基因里最多的那个。
  // 1.53.0：双武器战斗风格至少 1 个副手槽（AdventurerStyles.cs）。
  public static int OffhandSlots(Pawn p){
   if(DualWield || p==null)return 0;int best=GeneSlots(p);
   if(best<1 && Styles.TwoWeaponUser(p))best=1;
   return best;
  }
  public static int GeneSlots(Pawn p){
   if(DualWield || p?.genes==null)return 0;int best=0;var list=p.genes.GenesListForReading;
   for(int i=0;i<list.Count;i++){if(!list[i].Active)continue;var e=list[i].def.GetModExtension<OffhandExtension>();if(e!=null)best=Math.Max(best,Slots(p,e));}
   return best;
  }
  // 副手追加攻击的触发概率：蛇人、双头巨人（基因副手）每次都触发；只有双武器风格的人 30%。
  public static float OffhandChance(Pawn p)=>GeneSlots(p)>0?1f:Styles.TwoWeaponChance;
  static int Slots(Pawn p,OffhandExtension e){
   if(e.perExtraArmPair)return Math.Min(e.max,UsablePairs(p));
   if(e.needsTwinHeads && !Ettin.TwoHeaded(p))return 0;
   var hands=p.RaceProps.body.GetPartsWithDef(BodyPartDefOf.Hand);
   return hands.Count>=2 && hands.All(h=>HandUsable(p,h))?e.max:0;
  }
  public static float Score(Thing w)=>w.GetStatValue(StatDefOf.MeleeWeapon_AverageDPS);
  public static bool OffhandWeapon(Thing t)=>t is ThingWithComps w && w.def.IsMeleeWeapon && w.TryGetComp<CompEquippable>()!=null;
  // 背包里的近战武器，按“品质×DPS”（MeleeWeapon_AverageDPS 已含品质与材料）从高到低，最多 = 可用额外手臂对数。
  public static List<ThingWithComps> Offhands(Pawn p){
   var list=new List<ThingWithComps>();int n=OffhandSlots(p);if(n<=0 || p.inventory?.innerContainer==null)return list;
   foreach(var t in p.inventory.innerContainer)if(OffhandWeapon(t))list.Add((ThingWithComps)t);
   return list.OrderByDescending(Score).ThenBy(w=>w.thingIDNumber).Take(n).ToList();
  }
  // 1.52.0 补丁：有副手连击基因（六臂蛇人、双头巨人）的人；双持类模组启用时不算。
  // 1.53.0：有双武器战斗风格的人也算。
  public static bool OffhandUser(Pawn p){
   if(DualWield || p==null)return false;
   if(Styles.TwoWeaponUser(p))return true;
   if(p.genes==null)return false;var list=p.genes.GenesListForReading;
   for(int i=0;i<list.Count;i++)if(list[i].Active && list[i].def.HasModExtension<OffhandExtension>())return true;
   return false;
  }
  public static int CarriedOffhands(Pawn p)=>p?.inventory?.innerContainer==null?0:p.inventory.innerContainer.Count(OffhandWeapon);
  // 不能再拿副手的原因（null = 可以）。
  public static string CannotTakeOffhand(Pawn p){
   int cap=OffhandSlots(p);
   if(cap<=0)return p.genes!=null && p.genes.GenesListForReading.Any(g=>g.Active && g.def.GetModExtension<OffhandExtension>()?.perExtraArmPair==true)?"缺少能用的额外手臂":"缺少能用的手臂";
   return CarriedOffhands(p)>=cap?"没有空闲的手":null;
  }
  // 右键地上的近战武器：“拿作副手：{武器}”（任何地图，包括家园；原版家里的“拿起”只给药物方针里的药）。不显示 = null。
  public static FloatMenuOption TakeOffhandOption(Pawn pawn,Thing t){
   if(pawn==null || pawn.Faction!=Faction.OfPlayer || pawn.inventory==null || !OffhandUser(pawn) || !OffhandWeapon(t) || !t.Spawned || t.Map!=pawn.Map || t.IsForbidden(pawn) || !pawn.CanReserve(t))return null;
   string label="拿作副手："+t.LabelShort;
   var why=CannotTakeOffhand(pawn);if(why!=null)return new FloatMenuOption(label+"（"+why+"）",null);
   if(!pawn.CanReach(t,PathEndMode.ClosestTouch,Danger.Deadly))return new FloatMenuOption(label+"（无法到达）",null);
   if(MassUtility.WillBeOverEncumberedAfterPickingUp(pawn,t,1))return new FloatMenuOption(label+"（太重）",null);
   return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label,()=>{var j=JobMaker.MakeJob(JobDefOf.TakeInventory,t);j.count=1;pawn.jobs.TryTakeOrderedJob(j,JobTag.Misc);}),pawn,t);
  }
  public static bool MainHandOk(Pawn p){var w=p?.equipment?.Primary;return w==null || w.def.IsMeleeWeapon;}
  public static float OffhandDamage=>Mathf.Clamp(LichMod.Settings?.offhandDamage??.5f,.25f,1f);
  // 双持类模组（Tacticowl、Dual Wield 等，按 packageId 认）启用时自动关闭副手连击。
  static bool? dual;
  public static bool DualWield=>dual??(dual=DualWieldMods().Any()).Value;
  public static IEnumerable<string> DualWieldMods()=>ModsConfig.ActiveModsInLoadOrder.Select(m=>m.PackageIdPlayerFacing??"").Where(id=>{var s=id.ToLowerInvariant();return s.Contains("dualwield") || s.Contains("tacticowl");});

  // ---------- 冷血、蛇行 ----------
  // 1.58.6：冷血挂在 MoveSpeed 上（每个小人都算），没装 HAR 时基因根本不存在：用静态 DefRef，查不到就直接返回。
  static readonly DefRef<GeneDef> coldGene=new DefRef<GeneDef>(ColdBlood),glideGene=new DefRef<GeneDef>(Glide);
  public static bool Cold(Pawn p)=>CommonRaces.Has(p,coldGene.Def) && p.Spawned && p.AmbientTemperature<ColdBelow;
  public static bool Glides(Pawn p)=>p!=null && p.RaceProps.Humanlike && CommonRaces.Has(p,glideGene.Def);
  // 泥地、沼泽、浅水（深水原版不可通行，这里不改通行性）。
  // 1.58.6：结果只取决于地形定义，按定义记住（原来每走一格做两次不区分大小写的 IndexOf）。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<TerrainDef,bool> mire=new System.Collections.Concurrent.ConcurrentDictionary<TerrainDef,bool>();
  public static bool Mire(TerrainDef t)=>t!=null && mire.GetOrAdd(t,x=>x.passability!=Traversability.Impassable && (x.IsWater || x.defName.IndexOf("Mud",StringComparison.OrdinalIgnoreCase)>=0 || x.defName.IndexOf("Marsh",StringComparison.OrdinalIgnoreCase)>=0));
  public static int? GlideCost(Pawn p,TerrainDef t,int? vanilla){
   if(!Mire(t) || !Glides(p))return vanilla;
   int half=t.pathCost/2;return vanilla.HasValue?Math.Min(vanilla.Value,half):half;
  }
 }

 // 六臂（常驻）：操作 +0.1 × 能用的额外手。不可治疗、不可移除（蛇人的种族组件会补回来）。
 public class Hediff_SixArms : Hediff {
  HediffStage stage;float built=-1f;
  public override bool ShouldRemove=>!Marilith.Is(pawn);
  public override HediffStage CurStage{get{
   float b=Marilith.ManipulationBonus(pawn);
   if(stage==null || built!=b){built=b;stage=new HediffStage{capMods=new List<PawnCapacityModifier>{new PawnCapacityModifier{capacity=PawnCapacityDefOf.Manipulation,offset=b}}};}
   return stage;
  }}
  public override string LabelInBrackets=>"额外手 "+Marilith.UsableHands(pawn)+"/4，操作 +"+Marilith.ManipulationBonus(pawn).ToStringPercent();
 }
 // 种族组件：生成、出生、读档、每 2500 刻确认六臂状态与鳞色基因都在。
 public class CompProperties_Marilith : CompProperties {public CompProperties_Marilith(){compClass=typeof(CompMarilith);}}
 public class CompMarilith : ThingComp {
  int ticks;
  Pawn Pawn=>parent as Pawn;
  public override void PostSpawnSetup(bool respawningAfterLoad){base.PostSpawnSetup(respawningAfterLoad);Marilith.EnsureSixArms(Pawn);}
  public override void CompTickInterval(int delta){base.CompTickInterval(delta);if((ticks+=delta)<2500)return;ticks=0;Marilith.EnsureSixArms(Pawn);}
  public override void PostExposeData(){base.PostExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit && Pawn?.health!=null)Marilith.EnsureSixArms(Pawn);}
 }
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class MarilithGenerated {
  static void Postfix(Pawn __result){if(__result==null || !Marilith.Is(__result))return;Marilith.EnsureSkin(__result);Marilith.EnsureSixArms(__result);}
 }

 // 渲染：根节点 ×1.3（同巨人的做法，不用 HAR 的 customDrawSize，头、衣服、身体附件一起缩放）。1.44.0 起所有带 RaceSizeExtension 的种族通用（双头巨人 ×1.4、独眼巨人 ×1.35）。
 [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.ScaleFor))]
 public static class MarilithRenderSize {
  static void Postfix(PawnRenderNode node,PawnDrawParms parms,ref Vector3 __result){if(RenderTags.IsRoot(node?.Props?.tagDef) && RaceSize.Ext(parms.pawn?.def)!=null)__result*=RaceSize.RenderFactor(parms.pawn);}
 }
 // ---------- 冷血：环境低于 0℃ 移速 ×85% ----------
 public class StatPart_ColdBlood : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Marilith.Cold(p))val*=Marilith.ColdMove;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Marilith.Cold(p)?"冷血（低于0℃）：×"+Marilith.ColdMove.ToStringPercent():null;
 }
 [StaticConstructorOnStartup]
 public static class MarilithStartup {
  static MarilithStartup(){
   var stat=StatDefOf.MoveSpeed;if(stat.parts==null)stat.parts=new List<StatPart>();
   if(!stat.parts.Any(p=>p is StatPart_ColdBlood))stat.parts.Add(new StatPart_ColdBlood{parentStat=stat});
   if(Marilith.Loaded && Marilith.DualWield)Log.Message("[DND边缘大陆] 检测到双持类模组（"+string.Join("、",Marilith.DualWieldMods())+"），六臂蛇人的副手连击已自动关闭。");
  }
 }
 // ---------- 蛇行：泥地、沼泽、浅水的地形消耗减半（只改走路，寻路预算不改；同霜巨人雪原行者） ----------
 [HarmonyPatch(typeof(Pawn_PathFollower),"GetPawnCellBaseCostOverride")]
 public static class SerpentGlideCost {
  static void Postfix(Pawn pawn,IntVec3 c,ref int? __result){if(pawn?.Map==null)return;__result=Marilith.GlideCost(pawn,c.GetTerrain(pawn.Map),__result);}
 }

 // ---------- 副手连击 ----------
 // 只有原版“普通近战”（Verb_MeleeAttack.TryCastShot 里命中、走到 ApplyMeleeDamageToTarget）才触发；
 // 旋风斩、剑刃风暴直接调 ApplyMeleeDamageToTarget，不经过 TryCastShot，所以不连锁；追加攻击本身也不再触发。
 public static class MarilithOffhand {
  [ThreadStatic] static Verb normal;[ThreadStatic] static Thing hit;[ThreadStatic] static bool following;
  [ThreadStatic] static Verb scaled;[ThreadStatic] static float scale;
  public static int Strikes; // 自检：追加攻击次数
  static readonly MethodInfo applyMelee=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget");
  public struct State {public Verb normal;public Thing hit;}
  public static bool Eligible(Verb_MeleeAttack verb){
   var p=verb?.CasterPawn;if(p==null || following || !Marilith.MainHandOk(p))return false;
   var src=verb.EquipmentSource;if(src!=null && src!=p.equipment?.Primary)return false;
   return Marilith.OffhandSlots(p)>0;
  }
  public static State Enter(Verb_MeleeAttack verb){var s=new State{normal=normal,hit=hit};normal=Eligible(verb)?verb:null;hit=null;return s;}
  public static void Hit(Verb verb,LocalTargetInfo target){if(verb!=null && verb==normal && target.Thing!=null)hit=target.Thing;}
  public static void Exit(Verb_MeleeAttack verb,State s){
   var v=normal;var t=hit;normal=s.normal;hit=s.hit;
   if(v==verb && t!=null)FollowUp(verb.CasterPawn,t);
  }
  public static void Restore(State s){normal=s.normal;hit=s.hit;}
  static bool Alive(Thing t)=>t!=null && !t.Destroyed && t.Spawned && !(t is Pawn q && q.Dead);
  // 每把副手各追加一次：用那把武器自己的原版近战 Verb，伤害 × 设置倍率。
  public static int FollowUp(Pawn p,Thing target){
   int n=0;if(p==null)return 0;
   float chance=Marilith.OffhandChance(p);if(chance<1f && !Rand.Chance(chance))return 0; // 1.53.0 双武器风格 30%
   foreach(var w in Marilith.Offhands(p)){if(!Alive(target) || p.Dead || p.Downed)break;if(Strike(p,w,target,Marilith.OffhandDamage))n++;}
   return n;
  }
  public static Verb_MeleeAttackDamage BestVerb(Pawn p,ThingWithComps weapon){
   var comp=weapon?.TryGetComp<CompEquippable>();if(comp==null)return null;
   Verb_MeleeAttackDamage best=null;float top=-1f;
   foreach(var v in comp.AllVerbs){if(!(v is Verb_MeleeAttackDamage m) || m.verbProps==null || !m.verbProps.IsMeleeAttack)continue;float d=m.verbProps.AdjustedMeleeDamageAmount(m,p);if(d>top){top=d;best=m;}}
   return best;
  }
  public static bool Strike(Pawn p,ThingWithComps weapon,Thing target,float factor){
   var verb=BestVerb(p,weapon);if(verb==null || applyMelee==null)return false;
   var oldCaster=verb.caster;bool wasFollowing=following;var oldScaled=scaled;float oldScale=scale;
   following=true;scaled=verb;scale=factor;verb.caster=p;
   try{applyMelee.Invoke(verb,new object[]{new LocalTargetInfo(target)});p.Drawer?.Notify_MeleeAttackOn(target);Strikes++;}
   catch(TargetInvocationException e){if(e.InnerException==null)throw;System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
   finally{verb.caster=oldCaster;following=wasFollowing;scaled=oldScaled;scale=oldScale;}
   return true;
  }
  // 自检与说明用：这把副手对 target 的一击会用到的伤害量（含近战伤害系数、品质与副手倍率）。
  public static float HitAmount(Pawn p,ThingWithComps weapon,float factor){
   var verb=BestVerb(p,weapon);if(verb==null)return 0f;
   var oldCaster=verb.caster;var oldScaled=scaled;float oldScale=scale;scaled=verb;scale=factor;verb.caster=p;
   try{return verb.verbProps.AdjustedMeleeDamageAmount(verb,p);}finally{verb.caster=oldCaster;scaled=oldScaled;scale=oldScale;}
  }
  public static float Scale(Verb verb)=>scaled!=null && verb==scaled?scale:1f;
 }
 [HarmonyPatch(typeof(Verb_MeleeAttack),"TryCastShot")]
 public static class MarilithOffhandCast {
  static void Prefix(Verb_MeleeAttack __instance,out MarilithOffhand.State __state){__state=MarilithOffhand.Enter(__instance);}
  static void Postfix(Verb_MeleeAttack __instance,MarilithOffhand.State __state){MarilithOffhand.Exit(__instance,__state);}
  static Exception Finalizer(Exception __exception,MarilithOffhand.State __state){if(__exception!=null)MarilithOffhand.Restore(__state);return __exception;}
 }
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class MarilithOffhandHit {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){MarilithOffhand.Hit(__instance,target);}
 }
 [HarmonyPatch(typeof(VerbProperties),nameof(VerbProperties.AdjustedMeleeDamageAmount),new[]{typeof(Verb),typeof(Pawn)})]
 public static class MarilithOffhandDamage {
  static void Postfix(Verb ownerVerb,ref float __result){float s=MarilithOffhand.Scale(ownerVerb);if(s!=1f)__result*=s;}
 }
 // 1.52.0 补丁：右键地上的近战武器“拿作副手”（原版 TakeInventory 任务，拿进背包）。
 public class FloatMenuOptionProvider_TakeOffhand : FloatMenuOptionProvider {
  protected override bool Drafted=>true;
  protected override bool Undrafted=>true;
  protected override bool Multiselect=>false;
  protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing,FloatMenuContext context)=>Marilith.TakeOffhandOption(context.FirstSelectedPawn,clickedThing);
 }
 // 1.52.0 补丁：卸下背包（商队进图、取消组建商队后的 UnloadEverything）时，正在用的副手（Offhands() 选中的）留在背包；超出手数的照常卸下。
 // 原版按背包顺序找第一件可卸的；这里把副手临时从列表里拿掉再找一次，只剩副手时返回空 → 原版每刻把 UnloadEverything 清掉，卸货任务结束。
 [HarmonyPatch(typeof(Pawn_InventoryTracker),nameof(Pawn_InventoryTracker.FirstUnloadableThing),MethodType.Getter)]
 public static class MarilithKeepOffhands {
  [ThreadStatic] static bool busy;
  static void Postfix(Pawn_InventoryTracker __instance,ref ThingCount __result){
   if(busy || !Marilith.OffhandWeapon(__result.Thing) || !Marilith.OffhandUser(__instance.pawn))return;
   var keep=Marilith.Offhands(__instance.pawn);if(!keep.Contains(__result.Thing))return;
   var list=__instance.innerContainer.InnerListForReading;var taken=new List<KeyValuePair<int,Thing>>();
   busy=true;
   try{
    for(int i=list.Count-1;i>=0;i--)if(list[i] is ThingWithComps w && keep.Contains(w)){taken.Add(new KeyValuePair<int,Thing>(i,w));list.RemoveAt(i);}
    __result=__instance.FirstUnloadableThing;
   }finally{
    for(int i=taken.Count-1;i>=0;i--)list.Insert(taken[i].Key,taken[i].Value);
    busy=false;
   }
  }
 }

 // ---------- 缠绕：近身缠住一名敌人 3 秒，每秒 6 点钝伤（按体型比修正），期间自己不能移动 ----------
 public class CompProperties_SerpentConstrict : CompProperties_AbilityEffect {
  public int ticks=180;public float damagePerSecond=6f;
  public CompProperties_SerpentConstrict(){compClass=typeof(CompSerpentConstrict);}
 }
 public class CompSerpentConstrict : CompAbilityEffect {
  public CompProperties_SerpentConstrict Props=>(CompProperties_SerpentConstrict)props;
  public static float SizeFactor(Pawn caster,Pawn target)=>Mathf.Clamp(caster.BodySize/Mathf.Max(.1f,target.BodySize),.5f,2f);
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   var t=target.Pawn;var p=parent.pawn;
   string why=t==null || t.Dead?"需要选择一个活着的目标。":t==p?"不能缠住自己。":!t.Position.AdjacentTo8WayOrInside(p.Position)?"要贴身才能缠住。":null;
   if(why!=null){if(throwMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);return false;}
   return base.Valid(target,throwMessages);
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){base.Apply(target,dest);Constrict(parent.pawn,target.Pawn,Props.ticks,Props.damagePerSecond);}
  public static Hediff_Constricted Constrict(Pawn caster,Pawn t,int ticks,float dps){
   if(caster==null || t==null || t.Dead)return null;
   t.stances?.stunner?.StunFor(ticks,caster);
   var h=Items.Give(t,Marilith.Constricted,ticks) as Hediff_Constricted;
   if(h!=null){h.constrictor=caster;h.damage=dps*SizeFactor(caster,t);}
   Items.Give(caster,Marilith.Constricting,ticks);
   caster.pather?.StopDead();caster.stances?.SetStance(new Stance_Cooldown(ticks,t,null));
   Items.Mote(t,"缠绕",new Color(.6f,.85f,.6f));
   return h;
  }
 }
 public class Hediff_Constricted : HediffWithComps {
  public Pawn constrictor;public float damage=6f;int ticks;
  public override void TickInterval(int delta){
   base.TickInterval(delta);ticks+=delta;
   while(ticks>=60){ticks-=60;Squeeze();}
  }
  public float Squeeze(){
   if(pawn.Dead || damage<=0f)return 0f;
   var r=pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt,damage,0f,-1f,constrictor,null,null,DamageInfo.SourceCategory.ThingOrUnknown,pawn));
   return r.totalDamageDealt;
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref constrictor,"lpConstrictor");Scribe_Values.Look(ref damage,"lpConstrictDamage",6f);Scribe_Values.Look(ref ticks,"lpConstrictTicks");}
 }

 // ---------- 剑刃风暴：周围 1 格的每个敌人，主手 + 每把可用副手各攻击一次（不连锁副手追加） ----------
 public class CompProperties_BladeStorm : CompProperties_AbilityEffect {public CompProperties_BladeStorm(){compClass=typeof(CompBladeStorm);}}
 public class CompBladeStorm : CompAbilityEffect {
  static readonly MethodInfo applyMelee=AccessTools.Method(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget");
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){base.Apply(target,dest);Storm(parent.pawn);}
  // 主手的伤害型近战 Verb（原版挑选的若是附加状态类 Verb，就退回到装备/肢体的伤害 Verb）。
  public static Verb_MeleeAttackDamage MainVerb(Pawn p,Thing t){if(p?.meleeVerbs==null)return null;if(p.meleeVerbs.TryGetMeleeVerb(t) is Verb_MeleeAttackDamage v)return v;return p.meleeVerbs.GetUpdatedAvailableVerbsList(false).Select(e=>e.verb).OfType<Verb_MeleeAttackDamage>().OrderByDescending(x=>x.EquipmentSource!=null).FirstOrDefault();}
  public static List<Pawn> Foes(Pawn p)=>p?.Map==null?new List<Pawn>():Races124.PawnsNear(p.Map,p.Position,1.5f).Where(t=>t!=p && !t.Dead && t.HostileTo(p) && t.Position.AdjacentTo8WayOrInside(p.Position)).ToList();
  // 返回总攻击次数（主手 + 副手）。
  public static int Storm(Pawn p){
   int n=0;if(p?.Map==null)return 0;
   foreach(var t in Foes(p)){
    if(t.Dead || !t.Spawned)continue;
    var verb=MainVerb(p,t);
    if(verb!=null && applyMelee!=null){applyMelee.Invoke(verb,new object[]{new LocalTargetInfo(t)});n++;}
    p.Drawer?.Notify_MeleeAttackOn(t);
    foreach(var w in Marilith.Offhands(p)){if(t.Dead || !t.Spawned)break;if(MarilithOffhand.Strike(p,w,t,Marilith.OffhandDamage))n++;}
   }
   Items.Mote(p,"剑刃风暴",new Color(.95f,.6f,.4f));
   return n;
  }
 }

 // ---------- 塞伊远征军：大型袭击（≥1000 点）里低权重出现一名六臂蛇人剑舞者 ----------
 public static class MarilithRaid {
  public const float MinPoints=1000f,Chance=.15f;
  public static float? TestRoll;
  static PawnKindDef kind;
  public static PawnKindDef Kind=>kind??(kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FH_MarilithBlademaster"));
  public static int Apply(List<Pawn> list,PawnGroupMakerParms parms){
   var k=Kind;if(k==null || list==null || parms==null)return 0;
   if(parms.points<MinPoints){foreach(var p in list.Where(x=>x.kindDef==k).ToList())Discard(list,p);return 0;}
   if(parms.groupKind!=PawnGroupKindDefOf.Combat || list.Any(x=>x.kindDef==k) || (TestRoll??Rand.Value)>=Chance)return 0;
   var m=PawnGenerator.GeneratePawn(new PawnGenerationRequest(k,parms.faction,PawnGenerationContext.NonPlayer,parms.tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true,inhabitant:parms.inhabitants));
   list.Insert(0,m);ArmOffhands(m,parms.points);
   for(int i=0;i<2;i++){var spare=list.LastOrDefault(x=>x!=m && Ordinary(x));if(spare==null || list.Count(Ordinary)<=2)break;Discard(list,spare);}
   return 1;
  }
  // 背包里放 1 把（≥2500 点 2 把）副手刀剑，材料按常见度随机。
  static readonly string[] blades={"MeleeWeapon_LongSword","MeleeWeapon_Gladius","MeleeWeapon_Knife"};
  public static void ArmOffhands(Pawn m,float points){
   if(m?.inventory==null)return;
   for(int i=points>=2500f?2:1;i>0;i--){var d=DefDatabase<ThingDef>.GetNamedSilentFail(blades.RandomElement());if(d==null)continue;var w=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.RandomStuffByCommonalityFor(d,TechLevel.Medieval):null);w.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityRandomEqualChance(),ArtGenerationContext.Outsider);m.inventory.innerContainer.TryAdd(w);}
  }
  static bool Ordinary(Pawn x)=>x.RaceProps.Humanlike && x.kindDef!=Kind && !x.kindDef.trader && Necro.Construct(x)==null && !Fiends.Is(x) && !Marilith.Is(x) && !GiantKin.Is(x) && !(Factions.Ext(x.Faction)?.leaderKinds.Contains(x.kindDef)??false);
  static void Discard(List<Pawn> list,Pawn p){list.Remove(p);if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}
 }

 // ---------- 设置 ----------
 public partial class Settings {
  public float offhandDamage=.5f;
  void ExposeMarilith(){Scribe_Values.Look(ref offhandDamage,"marilithOffhandDamage",.5f);offhandDamage=Mathf.Clamp(offhandDamage,.25f,1f);}
 }
 public static class MarilithSettings {
  public static void Draw(Listing_Standard l){
   l.GapLine();l.Label("六臂蛇人（需要 Humanoid Alien Races）");
   l.Label("副手追加伤害："+LichMod.Settings.offhandDamage.ToStringPercent()+(Marilith.DualWield?"（已检测到双持类模组，副手连击关闭）":""));
   LichMod.Settings.offhandDamage=Mathf.Round(l.Slider(LichMod.Settings.offhandDamage,.25f,1f)*20f)/20f;
  }
 }
}
