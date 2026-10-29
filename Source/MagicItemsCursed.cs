using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.45.0 诅咒物品：穿上/拿起后脱不下（设置“诅咒物品”关掉时可以正常脱，效果照旧）；未鉴定时标签与描述是伪装的。
 // 鉴定：识破卷轴（整张地图）或研究台“鉴定”工作2小时（需要附魔学）。解除诅咒卷轴解除一件的诅咒（顺便鉴定）。死亡、物品被毁、截肢失去部位时照常脱落。
 public partial class Settings {
  public bool cursedItems=true;
  void ExposeCursed(){Scribe_Values.Look(ref cursedItems,"cursedItems",true);}
 }
 public class CompProperties_Cursed : CompProperties {
  public string disguiseLabel,disguiseDescription;public bool mark;
  public CompProperties_Cursed(){compClass=typeof(CompCursed);}
 }
 public class CompCursed : ThingComp {
  public bool identified,lifted,allianceNotified;public int wornSince=-1;
  public CompProperties_Cursed Props=>(CompProperties_Cursed)props;
  public bool Active=>!lifted && LichMod.Settings!=null && LichMod.Settings.cursedItems;
  public Pawn Holder=>(parent as Apparel)?.Wearer??(parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;
  public override string TransformLabel(string label){
   if(!identified)return (Props.disguiseLabel??label)+"（未鉴定）";
   return label+(lifted?"（已解咒）":"（诅咒）");
  }
  public override string CompInspectStringExtra(){
   if(!identified)return "未鉴定：看不出真正的效果。识破卷轴或研究台“鉴定”（需要附魔学）可以看穿。";
   return lifted?"诅咒已解除：可以正常脱下，效果和代价都还在。":"诅咒物品："+(LichMod.Settings?.cursedItems==false?"设置里关掉了“诅咒物品”，可以正常脱下。":"穿上后脱不下，只有解除诅咒卷轴能解开。");
  }
  public override void Notify_Equipped(Pawn pawn){
   base.Notify_Equipped(pawn);wornSince=Find.TickManager.TicksGame;
   // 巫妖戴混沌印记：没有代价，但同盟好感 -30（每件只扣一次）。
   if(Props.mark && !allianceNotified && Items.IsLich(pawn) && pawn.Faction==Faction.OfPlayer && MagicItems.Alliance!=null && !MagicItems.Alliance.HostileTo(Faction.OfPlayer)){
    allianceNotified=true;MagicItems.Alliance.TryAffectGoodwillWith(Faction.OfPlayer,MagicItems.LichMarkGoodwill,true,true,null,pawn);
   }
  }
  public void Identify(bool message=true){if(identified)return;identified=true;if(message && parent.MapHeld!=null)Messages.Message("鉴定结果："+parent.LabelCap+"。",new LookTargets(parent.SpawnedOrAnyParentSpawned?parent:(Thing)Holder),MessageTypeDefOf.NeutralEvent,false);}
  public void Lift(){identified=true;lifted=true;}
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){foreach(var g in base.CompGetGizmosExtra())yield return g;if(!identified && parent.Spawned)yield return CursedItems.IdentifyCommand(parent);}
  public override IEnumerable<Gizmo> CompGetWornGizmosExtra(){foreach(var g in base.CompGetWornGizmosExtra())yield return g;var p=Holder;if(!identified && p!=null && p.Faction==Faction.OfPlayer)yield return CursedItems.IdentifyCommand(parent);}
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref identified,"lpIdentified");Scribe_Values.Look(ref lifted,"lpLifted");Scribe_Values.Look(ref allianceNotified,"lpAllianceNotified");Scribe_Values.Look(ref wornSince,"lpWornSince",-1);}
 }
 public static class CursedItems {
  public const int IdentifyTicks=5000;
  public static bool releasing;
  public static CompCursed Comp(Thing t)=>t?.TryGetComp<CompCursed>();
  public static bool Locked(Thing t,Pawn holder)=>!releasing && holder!=null && !holder.Dead && Comp(t)?.Active==true;
  public static string LockedReason(Thing t)=>t.LabelCap+"被诅咒了，脱不下来。";
  // 想换装备：会被换下的诅咒物品挡住。
  public static string BlocksEquip(Pawn p,Thing thing){
   if(p==null || thing==null)return null;
   if(thing.def.IsWeapon && p.equipment?.Primary!=null && p.equipment.Primary!=thing && Locked(p.equipment.Primary,p))return LockedReason(p.equipment.Primary);
   if(thing.def.IsApparel && p.apparel!=null)foreach(var a in p.apparel.WornApparel)if(a!=thing && Locked(a,p) && !ApparelUtility.CanWearTogether(thing.def,a.def,p.RaceProps.body))return LockedReason(a);
   return null;
  }
  public static IEnumerable<CompCursed> Unidentified(Map map){
   foreach(var t in map.listerThings.AllThings.ToList()){var c=Comp(t);if(c!=null && !c.identified)yield return c;}
   foreach(var p in map.mapPawns.AllPawnsSpawned){
    var w=p.equipment?.Primary;var cw=Comp(w);if(cw!=null && !cw.identified)yield return cw;
    if(p.apparel!=null)foreach(var a in p.apparel.WornApparel){var c=Comp(a);if(c!=null && !c.identified)yield return c;}
    if(p.inventory!=null)foreach(var t in p.inventory.innerContainer){var c=Comp(t);if(c!=null && !c.identified)yield return c;}
   }
  }
  // 识破卷轴：鉴定地图上所有未鉴定的诅咒物品。
  public static int IdentifyMap(Map map){int n=0;if(map==null)return 0;foreach(var c in Unidentified(map).Distinct().ToList()){c.Identify(false);n++;}if(n>0)Messages.Message("识破卷轴的光芒照出了"+n+"件物品的真面目。",MessageTypeDefOf.NeutralEvent,false);return n;}
  public static List<Thing> CursedOn(Pawn p){var l=new List<Thing>();if(p==null)return l;if(Comp(p.equipment?.Primary)?.lifted==false)l.Add(p.equipment.Primary);if(p.apparel!=null)l.AddRange(p.apparel.WornApparel.Where(a=>Comp(a)?.lifted==false));return l;}
  public static Thing RemoveCurseTarget(LocalTargetInfo t){if(t.Thing is Pawn p)return CursedOn(p).FirstOrDefault();return Comp(t.Thing)?.lifted==false?t.Thing:null;}
  public static bool RemoveCurse(LocalTargetInfo t){var thing=RemoveCurseTarget(t);var c=Comp(thing);if(c==null)return false;c.Lift();Messages.Message(thing.LabelCap+"的诅咒解除了。",thing.SpawnedOrAnyParentSpawned?new LookTargets(thing):LookTargets.Invalid,MessageTypeDefOf.PositiveEvent,false);return true;}
  // 研究台鉴定：需要完成附魔学；选一个能做研究的殖民者。
  public static ResearchProjectDef Enchanting=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LP_Research_Enchanting");
  // 1.47.0：卷轴书桌也能鉴定，而且快一半（Workbenches.IdentifyFactor）。
  public static Building Bench(Pawn p)=>p?.Map==null?null:p.Map.listerBuildings.allBuildingsColonist.Where(b=>Workbenches.CanIdentifyAt(b) && p.CanReserveAndReach(b,PathEndMode.InteractionCell,Danger.Some)).OrderBy(b=>b.Position.DistanceToSquared(p.Position)).FirstOrDefault();
  public static string CannotIdentify(Pawn p,Thing item){
   if(Enchanting!=null && !Enchanting.IsFinished)return "需要先完成研究“附魔学”。";
   if(p==null || p.Dead || p.Downed || p.WorkTypeIsDisabled(WorkTypeDefOf.Research))return "需要能做研究的人。";
   if(Bench(p)==null)return "附近没有可用的研究台或卷轴书桌。";
   var holder=Comp(item)?.Holder;if(holder!=null && holder!=p)return "只能由佩戴者本人去鉴定身上的物品。";
   return null;
  }
  public static Command IdentifyCommand(Thing item){
   var cmd=new Command_Action{defaultLabel="鉴定",defaultDesc="让一名研究者把它带到研究台研究2小时（卷轴书桌只要80分钟），看穿它的真面目（需要附魔学）。",icon=item.def.uiIcon,action=()=>{
    var holder=Comp(item)?.Holder;
    var who=holder!=null?new List<Pawn>{holder}:item.MapHeld.mapPawns.FreeColonistsSpawned.ToList();
    var opts=who.Select(p=>{string why=CannotIdentify(p,item);return why==null?new FloatMenuOption(p.LabelShort,()=>StartIdentify(p,item)):new FloatMenuOption(p.LabelShort+"（"+why+"）",null);}).ToList();
    if(opts.Count==0)opts.Add(new FloatMenuOption("没有可用的研究者",null));
    Find.WindowStack.Add(new FloatMenu(opts));
   }};
   return cmd;
  }
  public static bool StartIdentify(Pawn p,Thing item){
   if(CannotIdentify(p,item)!=null)return false;
   var job=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("LP_IdentifyItem"),item,Bench(p));job.count=1;
   return p.jobs.TryTakeOrderedJob(job,JobTag.Misc);
  }
 }
 public class JobDriver_IdentifyItem : JobDriver {
  Thing Item=>job.targetA.Thing;
  bool Worn=>CursedItems.Comp(Item)?.Holder==pawn;
  public override bool TryMakePreToilReservations(bool errorOnFailed){if(!pawn.Reserve(job.targetB,job,1,-1,null,errorOnFailed))return false;return Worn || pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);}
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDestroyedOrNull(TargetIndex.B);
   if(!Worn){
    yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.ClosestTouch).FailOnSomeonePhysicallyInteracting(TargetIndex.A);
    yield return Toils_Haul.StartCarryThing(TargetIndex.A);
   }
   yield return Toils_Goto.GotoThing(TargetIndex.B,PathEndMode.InteractionCell);
   var study=Toils_General.Wait(Workbenches.IdentifyTicks(job.targetB.Thing),TargetIndex.B);
   study.WithProgressBarToilDelay(TargetIndex.B);study.activeSkill=()=>SkillDefOf.Intellectual;
   study.tickIntervalAction=delta=>pawn.skills?.Learn(SkillDefOf.Intellectual,.05f*delta);
   yield return study;
   yield return Toils_General.Do(()=>{CursedItems.Comp(Item)?.Identify();if(pawn.carryTracker?.CarriedThing!=null)pawn.carryTracker.TryDropCarriedThing(pawn.Position,ThingPlaceMode.Near,out _);});
  }
 }
 // ---------- 脱不下 ----------
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.TryDrop),new Type[]{typeof(Apparel),typeof(Apparel),typeof(IntVec3),typeof(bool)},new ArgumentType[]{ArgumentType.Normal,ArgumentType.Out,ArgumentType.Normal,ArgumentType.Normal})]
 public static class CursedApparelDrop {
  static bool Prefix(Pawn ___pawn,Apparel __0,ref Apparel __1,ref bool __result){if(!CursedItems.Locked(__0,___pawn))return true;__1=null;__result=false;return false;}
 }
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Remove))]
 public static class CursedApparelRemove {
  static bool Prefix(Pawn ___pawn,Apparel __0)=>!CursedItems.Locked(__0,___pawn);
 }
 // 换上会冲突的衣服：被诅咒物品挡住就不穿。
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Wear),new Type[]{typeof(Apparel),typeof(bool),typeof(bool)})]
 public static class CursedApparelWear {
  static bool Prefix(Pawn ___pawn,Apparel __0){
   if(__0==null || ___pawn?.apparel==null)return true;
   foreach(var a in ___pawn.apparel.WornApparel)if(a!=__0 && CursedItems.Locked(a,___pawn) && !ApparelUtility.CanWearTogether(__0.def,a.def,___pawn.RaceProps.body)){if(___pawn.Faction==Faction.OfPlayer)Messages.Message(CursedItems.LockedReason(a),___pawn,MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
 }
 // 截肢：失去部位时照常掉落。
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Notify_LostBodyPart))]
 public static class CursedApparelLostPart {
  static void Prefix(){CursedItems.releasing=true;}
  static void Finalizer(){CursedItems.releasing=false;}
 }
 [HarmonyPatch(typeof(Pawn_EquipmentTracker),nameof(Pawn_EquipmentTracker.TryDropEquipment))]
 public static class CursedWeaponDrop {
  static bool Prefix(Pawn_EquipmentTracker __instance,ThingWithComps __0,ref ThingWithComps __1,ref bool __result){if(!CursedItems.Locked(__0,__instance.pawn))return true;__1=null;__result=false;return false;}
 }
 [HarmonyPatch(typeof(Pawn_EquipmentTracker),nameof(Pawn_EquipmentTracker.TryTransferEquipmentToContainer))]
 public static class CursedWeaponTransfer {
  static bool Prefix(Pawn_EquipmentTracker __instance,ThingWithComps __0,ref bool __result){if(!CursedItems.Locked(__0,__instance.pawn))return true;__result=false;return false;}
 }
 [HarmonyPatch(typeof(Pawn_EquipmentTracker),nameof(Pawn_EquipmentTracker.Remove))]
 public static class CursedWeaponRemove {
  static bool Prefix(Pawn_EquipmentTracker __instance,ThingWithComps __0)=>!CursedItems.Locked(__0,__instance.pawn);
 }
 // 未鉴定时只显示伪装的描述。
 [HarmonyPatch(typeof(Thing),"get_DescriptionFlavor")]
 public static class CursedDescription {
  static void Postfix(Thing __instance,ref string __result){var c=CursedItems.Comp(__instance);if(c!=null && !c.identified && c.Props.disguiseDescription!=null)__result=c.Props.disguiseDescription;}
 }
 [HarmonyPatch(typeof(Thing),"get_DescriptionDetailed")]
 public static class CursedDescriptionDetailed {
  static void Postfix(Thing __instance,ref string __result){var c=CursedItems.Comp(__instance);if(c!=null && !c.identified && c.Props.disguiseDescription!=null)__result=c.Props.disguiseDescription;}
 }
 // ---------- 黑网：“看起来很好的货”里有30%是诅咒品（未鉴定） ----------
 public class StockGenerator_LPMagicGoods : StockGenerator {
  public List<ThingDef> goods=new List<ThingDef>(),cursed=new List<ThingDef>();
  public float cursedChance=.3f;
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   int n=countRange.RandomInRange;
   for(int i=0;i<n;i++){
    var def=cursed.Count>0 && Rand.Chance(cursedChance)?cursed.RandomElement():goods.RandomElementWithFallback();if(def==null)continue;
    var t=ThingMaker.MakeThing(def,def.MadeFromStuff?GenStuff.DefaultStuffFor(def):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityTraderItem(),ArtGenerationContext.Outsider);yield return t;
   }
  }
  public override bool HandlesThingDef(ThingDef thingDef)=>goods.Contains(thingDef) || cursed.Contains(thingDef);
 }
}
