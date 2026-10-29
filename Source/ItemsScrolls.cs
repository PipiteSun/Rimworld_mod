using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.22.0 卷轴：一次性使用。智识≥6才能读；失败率 max(0,(8-智识)×5%)，失败时卷轴烧毁、手部轻微烧伤；巫妖读不会失败。
 public static class ItemScrolls {
  public const int MinIntellect=6,RevivifyMaxAge=60000,WardTicks=60000,RevealTicks=2500,PetrifyTicks=1800,WardRadius=2;
  public const float FailPerLevel=.05f,FireballRadius=3f,MaxPetrifySize=2f;
  public static int Intellect(Pawn p)=>p?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level??0;
  public static float FailChance(Pawn p)=>Items.IsLich(p)?0f:Mathf.Max(0f,(8-Intellect(p))*FailPerLevel)*MagicApparel.ScrollFailFactor(p); // 1.45.1 法袍失误率减半
  public static string CannotRead(Pawn p){
   if(p?.skills==null)return "不会读卷轴。";
   if(!Items.IsLich(p) && Intellect(p)<MinIntellect)return "智识不足"+MinIntellect+"级，读不懂卷轴。";
   return null;
  }
  // 读卷轴失误：卷轴烧毁，一只手轻微烧伤。
  public static void Fizzle(Pawn p,Thing scroll){
   var hand=p.health.hediffSet.GetNotMissingParts().Where(b=>b.def.defName=="Hand").RandomElementWithFallback();
   var burn=HediffMaker.MakeHediff(Troll.Burn,p,hand);burn.Severity=2f;p.health.AddHediff(burn,hand);
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"念错了"+scroll.LabelNoCount+"上的咒文，卷轴在手里烧了起来。",p,MessageTypeDefOf.NegativeEvent,false);
   Consume(scroll);
  }
  public static void Consume(Thing scroll){if(scroll==null || scroll.Destroyed)return;if(scroll.stackCount>1)scroll.SplitOff(1).Destroy();else scroll.Destroy();}
  // 读一张卷轴：roll 决定是否失误。返回 true 表示卷轴被用掉（成功或失误）。
  public static bool Read(Pawn user,Thing scroll,LocalTargetInfo target,float roll){
   var comp=scroll?.TryGetComp<CompScroll>();if(comp==null || CannotRead(user)!=null)return false;
   string invalid=comp.Invalid(user,target);if(invalid!=null){if(user.Faction==Faction.OfPlayer)Messages.Message(invalid,user,MessageTypeDefOf.RejectInput,false);return false;}
   if(roll<FailChance(user)){Fizzle(user,scroll);return true;}
   if(!Effect(comp.Props.effect,user,target))return false;
   if(!MagicApparel.KeepScroll(user,Rand.Value))Consume(scroll); // 1.45.1 大法师套装
   return true;
  }
  public static bool Effect(string effect,Pawn user,LocalTargetInfo target){
   switch(effect){
    case "teleport":return Teleport(user);
    case "revivify":return Revivify(user,target.Thing as Corpse);
    case "protection":return Ward(user.Map,user.Position)!=null;
    case "fireball":GenExplosion.DoExplosion(target.Cell,user.Map,FireballRadius,DamageDefOf.Flame,user,damAmount:15);return true;
    case "trueseeing":TrueSeeing(user.Map);return true;
    case "fleshtostone":return StartPetrify(user,target.Pawn);
    case "feargaze":foreach(var p in Near(user,target.Cell,4.9f))LichSpells.Frighten(user,p);FleckMaker.Static(target.Cell,user.Map,FleckDefOf.PsycastAreaEffect,4.9f);return true;
    case "necroblast":foreach(var p in Near(user,target.Cell,3.9f))p.TakeDamage(new DamageInfo(LichSpells.Necrotic,15f,0f,-1f,user));FleckMaker.Static(target.Cell,user.Map,FleckDefOf.PsycastAreaEffect,3.9f);return true;
    // 1.45.0 解除诅咒卷轴。
    case "removecurse":return CursedItems.RemoveCurse(target);
   }
   return false;
  }
  static List<Pawn> Near(Pawn user,IntVec3 c,float r)=>user.Map.mapPawns.AllPawnsSpawned.Where(p=>LichSpells.Enemy(user,p) && p.Position.InHorDistOf(c,r)).ToList();

  // ---------- 传送 ----------
  public static Map HomeMap()=>Find.Maps.Where(m=>m.IsPlayerHome).OrderByDescending(m=>m.mapPawns.FreeColonistsSpawnedCount).ThenBy(m=>m.uniqueID).FirstOrDefault();
  public static IntVec3 HomeSpot(Pawn p,Map home){
   var bed=p.ownership?.OwnedBed;
   if(bed!=null && bed.Spawned && bed.Map==home){foreach(var c in GenAdj.CellsAdjacent8Way(bed))if(c.InBounds(home) && c.Standable(home) && c.GetFirstPawn(home)==null)return c;}
   return CellFinder.RandomClosewalkCellNear(home.Center,home,8);
  }
  public static bool Teleport(Pawn p){
   var home=HomeMap();if(home==null || p.MapHeld==home)return false;
   var cell=HomeSpot(p,home);
   Items.State.Defer(()=>{if(p.Dead || !p.Spawned)return;p.jobs?.StopAll();p.GetLord()?.Notify_PawnLost(p,PawnLostCondition.ExitedMap,null);p.DeSpawn(DestroyMode.Vanish);GenSpawn.Spawn(p,cell,home);if(p.drafter!=null)p.drafter.Drafted=false;FleckMaker.Static(cell,home,FleckDefOf.PsycastAreaEffect,1.5f);if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"读完传送卷轴，回到了主基地。",p,MessageTypeDefOf.PositiveEvent,false);});
   return true;
  }
  // ---------- 复生 ----------
  public static string CannotRevivify(Corpse c){
   var p=c?.InnerPawn;if(p==null || !p.RaceProps.Humanlike)return "需要人形尸体。";
   if(Items.Undead(p) || NecromancyGenes.HasBody(p))return "复生卷轴对巫妖和死灵造物无效。";
   // 1.45.0：灵魂封在魂石里的人不论死了多久都能复生（尸体要在）。
   bool stone=SpiritStones.Holds(p);
   if(!stone && c.Age>RevivifyMaxAge)return "死亡超过1天，灵魂已经走远。";
   if(p.health.hediffSet.GetBrain()==null)return "大脑已经不在了（被吞噬或被毁），无法复生。";
   if(!stone && c.GetRotStage()!=RotStage.Fresh)return "尸体已经腐烂。";
   return null;
  }
  public static bool Revivify(Pawn user,Corpse c){
   if(CannotRevivify(c)!=null)return false;
   var p=c.InnerPawn;bool stone=SpiritStones.Holds(p);if(!ResurrectionUtility.TryResurrect(p,new ResurrectionParams{removeDiedThoughts=true}))return false;
   if(stone)SpiritStones.Consume(p);
   p.health.AddHediff(Items.Hediff("LP_RevivifyWeakness"));
   if(p.Faction==Faction.OfPlayer || user.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"在复生卷轴的光芒中重新睁开了眼睛。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  // ---------- 防护邪恶 ----------
  // 不能进入结界的人：敌对的死灵造物与夺心魔，以及被支配、被奴役的人。
  public static bool Evil(Pawn p)=>p!=null && !p.Dead && ((p.HostileTo(Faction.OfPlayer) && (Necro.Construct(p)!=null || MindFlayer.IsMindFlayer(p))) || p.MentalStateDef==MindFlayerPowers.Dominated || Items.Has(p,MindFlayerPowers.DominatedHediff) || FactionGroups.IsThralled(p));
  public static EvilWard Ward(Map map,IntVec3 center){
   var m=Items.MapState(map);if(m==null)return null;
   var w=new EvilWard{rect=CellRect.CenteredOn(center,WardRadius).ClipInsideMap(map),until=Find.TickManager.TicksGame+WardTicks};m.wards.Add(w);
   foreach(var c in w.rect.EdgeCells)FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,.6f);
   MaintainWards(m);return w;
  }
  public static bool Warded(Map map,IntVec3 c){var m=Items.MapState(map);if(m==null || m.wards.Count==0)return false;int now=Find.TickManager.TicksGame;foreach(var w in m.wards)if(w.until>now && w.rect.Contains(c))return true;return false;}
  public static void MaintainWards(ItemsMap m){
   int now=Find.TickManager.TicksGame;m.wards.RemoveAll(w=>w.until<=now);
   foreach(var w in m.wards)foreach(var p in w.rect.Cells.SelectMany(c=>c.GetThingList(m.map)).OfType<Pawn>().Where(Evil).ToList()){
    if(!CellFinder.TryFindRandomCellNear(p.Position,m.map,6,c=>c.Standable(m.map) && !Warded(m.map,c) && c.GetFirstPawn(m.map)==null,out var outside))continue;
    p.pather?.StopDead();p.Position=outside;p.Notify_Teleported(true,false);Items.Mote(p,"被结界推开",new Color(1f,.95f,.6f));
   }
  }
  // ---------- 识破 ----------
  public static int TrueSeeing(Map map){
   var m=Items.MapState(map);if(m!=null)m.revealUntil=Find.TickManager.TicksGame+RevealTicks;int freed=0;
   foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()){
    if(p.Dead)continue;
    var dom=p.health.hediffSet.GetFirstHediffOfDef(MindFlayerPowers.DominatedHediff);
    if(dom!=null){p.health.RemoveHediff(dom);freed++;}else if(p.MentalStateDef==MindFlayerPowers.Dominated){MindFlayerPowers.EndDomination(p);freed++;}
    if(FactionGroups.IsThralled(p)){Decapitation.Free(p);freed++;}
   }
   if(freed>0)Messages.Message("识破卷轴的光芒驱散了夺心魔的控制："+freed+"人清醒过来。",MessageTypeDefOf.PositiveEvent,false);
   CursedItems.IdentifyMap(map); // 1.45.0：顺便鉴定地图上的诅咒物品
   return freed;
  }
  // ---------- 石化 ----------
  public static string CannotPetrify(Pawn user,Pawn p){
   if(p==null || p.Dead || !p.Spawned)return "需要地图上活着的目标。";
   if(user!=null && !p.HostileTo(user))return "只能对敌人使用。";
   if(p.BodySize>MaxPetrifySize)return "目标体型太大（超过2）。";
   return null;
  }
  public static bool StartPetrify(Pawn user,Pawn p){if(CannotPetrify(user,p)!=null)return false;Items.Give(p,"LP_Petrifying",PetrifyTicks);Items.Mote(p,"开始石化",new Color(.75f,.75f,.7f));return true;}
  public static PetrifiedStatue Petrify(Pawn p){
   if(p==null || p.Dead || !p.Spawned)return null;var map=p.Map;var cell=p.Position;
   var statue=(PetrifiedStatue)ThingMaker.MakeThing(Items.Thing("LP_PetrifiedStatue"));
   p.jobs?.StopAll();p.GetLord()?.Notify_PawnLost(p,PawnLostCondition.Incapped,null);p.DeSpawn(DestroyMode.Vanish);
   if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);
   statue.inner.TryAdd(p);GenPlace.TryPlaceThing(statue,cell,map,ThingPlaceMode.Near);
   Messages.Message(p.LabelShort+"化成了一尊石像。",statue,MessageTypeDefOf.NeutralEvent,false);
   return statue;
  }
 }
 public class CompProperties_Scroll : CompProperties_UseEffect {
  public string effect;public string target="none";public float range=30f;
  public CompProperties_Scroll(){compClass=typeof(CompScroll);}
 }
 public class CompScroll : CompUseEffect {
  public LocalTargetInfo target=LocalTargetInfo.Invalid;
  public new CompProperties_Scroll Props=>(CompProperties_Scroll)props;
  public string Invalid(Pawn user,LocalTargetInfo t){
   if(user?.Map==null)return "读卷轴的人不在地图上。";
   switch(Props.effect){
    case "teleport":var home=ItemScrolls.HomeMap();return home==null?"没有主基地可以回去。":user.Map==home?"已经在主基地了，传送卷轴没有效果。":null;
    case "revivify":return ItemScrolls.CannotRevivify(t.Thing as Corpse);
    case "fleshtostone":return ItemScrolls.CannotPetrify(user,t.Pawn);
    case "removecurse":return CursedItems.RemoveCurseTarget(t)==null?"目标身上没有被诅咒的物品。":null;
   }
   if(Props.target=="cell" && (!t.Cell.IsValid || !t.Cell.InBounds(user.Map)))return "需要地图上的目标位置。";
   return null;
  }
  public override AcceptanceReport CanBeUsedBy(Pawn p){
   string r=ItemScrolls.CannotRead(p);if(r!=null)return r;
   if(Props.effect=="teleport"){string t=Invalid(p,LocalTargetInfo.Invalid);if(t!=null)return t;}
   return base.CanBeUsedBy(p);
  }
  TargetingParameters Params(){
   switch(Props.target){
    case "corpse":return new TargetingParameters{canTargetPawns=false,canTargetBuildings=false,canTargetItems=true,mapObjectTargetsMustBeAutoAttackable=false,validator=x=>x.Thing is Corpse c && c.InnerPawn?.RaceProps.Humanlike==true};
    case "pawn":return new TargetingParameters{canTargetPawns=true,canTargetBuildings=false,canTargetItems=false,validator=x=>x.Thing is Pawn};
    case "cursed":return new TargetingParameters{canTargetPawns=true,canTargetBuildings=false,canTargetItems=true,mapObjectTargetsMustBeAutoAttackable=false,validator=x=>x.Thing!=null && CursedItems.RemoveCurseTarget(new LocalTargetInfo(x.Thing))!=null};
    default:return new TargetingParameters{canTargetLocations=true,canTargetPawns=true,canTargetBuildings=false};
   }
  }
  public override bool SelectedUseOption(Pawn p){
   if(Props.target=="none"){target=LocalTargetInfo.Invalid;return false;}
   Find.Targeter.BeginTargeting(Params(),t=>{
    if(t.Cell.DistanceTo(p.Position)>Props.range){Messages.Message("超出卷轴射程（"+Props.range.ToString("0")+"格）。",MessageTypeDefOf.RejectInput,false);return;}
    string bad=Invalid(p,t);if(bad!=null){Messages.Message(bad,MessageTypeDefOf.RejectInput,false);return;}
    target=t;parent.GetComp<CompUsable>()?.TryStartUseJob(p,t,false);
   },p);
   return true;
  }
  public override void DoEffect(Pawn usedBy){base.DoEffect(usedBy);var t=target;target=LocalTargetInfo.Invalid;ItemScrolls.Read(usedBy,parent,t,Rand.Value);}
  public override void PostExposeData(){base.PostExposeData();Scribe_TargetInfo.Look(ref target,"scrollTarget");}
 }
 // 防护邪恶结界：被挡的人不能走进结界格。
 [HarmonyPatch(typeof(Pawn_PathFollower),"PawnCanOccupy")]
 public static class EvilWardOccupy {
  static void Postfix(Pawn ___pawn,IntVec3 c,ref bool __result){if(__result && ___pawn?.Map!=null && Items.MapState(___pawn.Map)?.wards.Count>0 && ItemScrolls.Warded(___pawn.Map,c) && ItemScrolls.Evil(___pawn))__result=false;}
 }
 // 石化中：30秒后化成石像（死亡、被驱散则作罢）。
 public class Hediff_Petrifying : HediffWithComps {
  public override void PostRemoved(){
   base.PostRemoved();
   if(pawn==null || pawn.Dead || !pawn.Spawned || Items.Dispelled(pawn) || (this.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear??1)>0)return;
   var p=pawn;Items.State?.Defer(()=>ItemScrolls.Petrify(p));
  }
 }
 // 石像：里面完整保存着被石化的人；对它扬驱散粉尘就会复原。
 public class PetrifiedStatue : ThingWithComps,IThingHolder {
  public ThingOwner<Pawn> inner;
  // 1.37.0：升神失败化成的石像不可复原（驱散粉尘无效）。
  public bool permanent;
  public PetrifiedStatue(){inner=new ThingOwner<Pawn>(this,true,LookMode.Deep,false);}
  public Pawn Pawn=>inner.Count>0?inner[0]:null;
  public override string LabelNoCount=>Pawn!=null?"石化的"+Pawn.LabelShort:base.LabelNoCount;
  public override string GetInspectString(){var s=base.GetInspectString();var line=Pawn==null?"空的石像。":permanent?Pawn.LabelShort+"在升神仪式失败时化成了石像。魔网的反噬把血肉彻底变成了石头，不可复原（驱散粉尘无效）。":Pawn.LabelShort+"被石化在里面。对它扬驱散粉尘可以复原。";return string.IsNullOrEmpty(s)?line:s+"\n"+line;}
  public Pawn Restore(){
   var p=Pawn;var map=MapHeld;if(p==null || map==null)return null;var cell=PositionHeld;
   if(permanent){Messages.Message(p.LabelShort+"的石像纹丝不动：这尊石像不可复原。",this,MessageTypeDefOf.RejectInput,false);return null;}
   inner.Remove(p);GenSpawn.Spawn(p,cell,map);Destroy(DestroyMode.Vanish);
   Messages.Message(p.LabelShort+"从石像中恢复了血肉。",p,MessageTypeDefOf.NeutralEvent,false);
   return p;
  }
  public ThingOwner GetDirectlyHeldThings()=>inner;
  public void GetChildHolders(List<IThingHolder> outChildren){ThingOwnerUtility.AppendThingHoldersFromThings(outChildren,inner);}
  public override void ExposeData(){base.ExposeData();Scribe_Deep.Look(ref inner,"petrified",this);Scribe_Values.Look(ref permanent,"lpPermanent");if(Scribe.mode==LoadSaveMode.PostLoadInit && inner==null)inner=new ThingOwner<Pawn>(this,true,LookMode.Deep,false);}
  public override void Destroy(DestroyMode mode=DestroyMode.Vanish){base.Destroy(mode);inner.ClearAndDestroyContentsOrPassToWorld();}
 }
 // 巫妖抄写卷轴：只有巫妖能做，开工前要有30点精神力，完成时扣除。
 public class ScribeExtension : DefModExtension {public float focusCost=30f;}
 [HarmonyPatch(typeof(Bill),nameof(Bill.PawnAllowedToStartAnew))]
 public static class ScribeFocusAllowed {
  static void Postfix(Bill __instance,Pawn p,ref bool __result){
   var ext=__instance.recipe?.GetModExtension<ScribeExtension>();if(!__result || ext==null)return;
   var h=Necro.Lich(p);if(h!=null && h.Active && h.Points+.001f>=ext.focusCost)return;
   __result=false;JobFailReason.Is("需要至少"+ext.focusCost.ToString("0")+"点精神力的巫妖。");
  }
 }
 [HarmonyPatch(typeof(GenRecipe),nameof(GenRecipe.MakeRecipeProducts))]
 public static class ScribeFocusCost {
  static IEnumerable<Thing> Postfix(IEnumerable<Thing> values,RecipeDef recipeDef,Pawn worker){
   var ext=recipeDef?.GetModExtension<ScribeExtension>();
   if(ext==null){foreach(var t in values)yield return t;yield break;}
   bool paid=Necro.Pay(worker,ext.focusCost);
   foreach(var t in values){if(paid)yield return t;else if(!t.Destroyed)t.Destroy();}
   if(!paid)Messages.Message((worker?.LabelShort??"抄写者")+"的精神力不够，抄写失败。",worker,MessageTypeDefOf.NegativeEvent,false);
  }
 }
}
