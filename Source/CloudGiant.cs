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
 // 1.34.1 云巨人（只做“优雅者”）：本源（巨人血统 rank 2、2.2 体型、踏云、不触发陷阱）、贵胄之心、赠礼之风、优雅者，迷踪步、云雾术、唤风驱云。
 public static class CloudGiant {
  public const string Core="LP_CloudGiantCore",NobleGene="LP_CloudNoble",GiftGene="LP_CloudGift",GraceGene="LP_CloudGrace";
  public const string Morningstar="LP_CloudMorningstar",Robe="LP_CloudRobe",FogFlask="LP_FogFlask",Amber="LP_SkyAmber";
  public const string NobleKind="LP_FC_CloudNoble",NobleTraderKind="LP_FC_CloudNobleTrader",LuxuryCaravan="LP_Caravan_CloudLuxury";
  public const int StepRange=6,FogRadius=4,FlaskRadius=3,FogTicks=1800,GiftCap=25,KnockCells=2;
  public const float KnockChance=.15f,GiftPer100=1.5f,GraceRecruit=1.15f;
  public static bool Active(Pawn p)=>Races124.Has(p,Core);
  public static bool Has(Pawn p,string gene)=>Races124.Has(p,gene);
  public static bool Adult(Pawn p)=>StormGiant.Adult(p);
  public static bool IsNobleKind(PawnKindDef k)=>k!=null && (k.defName==NobleKind || k.defName==NobleTraderKind);

  // ---------- 踏云：沼泽、泥地、浅水的地形消耗按普通地面算（积雪走霜巨人的雪原行者补丁） ----------
  public static bool Walks(Pawn p)=>p!=null && p.RaceProps.Humanlike && Active(p);
  public static bool CloudTerrain(TerrainDef t){
   if(t==null || t.passability==Traversability.Impassable)return false;
   if(t.IsWater)return true;
   var n=t.defName;return n.IndexOf("Marsh",StringComparison.OrdinalIgnoreCase)>=0 || n.IndexOf("Mud",StringComparison.OrdinalIgnoreCase)>=0;
  }

  // ---------- 贵胄之心：卧室印象度 <“非常好” -4，≥“极好” +4 ----------
  static float Stage(int i,float fallback){var s=RoomStatDefOf.Impressiveness?.scoreStages;return s!=null && s.Count>i?s[i].minScore:fallback;}
  public static float VeryImpressive=>Stage(6,85f);
  public static float ExtremelyImpressive=>Stage(7,120f);
  // 自己床所在的房间的印象度；没有自己的床、床在室外或在营房里算 null（=配不上身份）。
  public static float? Bedroom(Pawn p){
   var bed=p?.ownership?.OwnedBed;if(bed==null || !bed.Spawned)return null;
   var room=bed.GetRoom();if(room==null || room.PsychologicallyOutdoors || room.Role==RoomRoleDefOf.Barracks)return null;
   return room.GetStat(RoomStatDefOf.Impressiveness);
  }
  // 0 = -4，1 = +4，-1 = 不生效。
  public static int NobleStage(float? impressiveness){
   if(impressiveness==null || impressiveness.Value<VeryImpressive)return 0;
   return impressiveness.Value>=ExtremelyImpressive?1:-1;
  }
  public static float ColonyBedrooms(Map map){
   var rooms=map?.mapPawns.FreeColonistsSpawned.Select(Bedroom).Where(v=>v!=null).Select(v=>v.Value).ToList();
   return rooms==null || rooms.Count==0?0f:rooms.Average();
  }

  // ---------- 赠礼之风 ----------
  public static int GiftGoodwill(float value)=>Mathf.Clamp(Mathf.RoundToInt(value/100f*GiftPer100),0,GiftCap);
  // 迁徙巨人挂在隐藏派系下，礼物记到原来的派系。
  public static Faction GiftFaction(Pawn p)=>GiantMigration.GroupOf(p)?.Origin??p?.Faction;
  public static bool CanReceive(Pawn p)=>p!=null && !p.Dead && p.RaceProps.Humanlike && p.Faction!=null && !p.Faction.IsPlayer && !p.HostileTo(Faction.OfPlayer) && !p.IsPrisoner && !p.IsSlave && GiftFaction(p)?.HasGoodwill==true;
  public static int Give(Pawn giver,Pawn receiver,Thing gift){
   if(gift==null || receiver==null)return 0;
   float value=gift.MarketValue*gift.stackCount;int gw=GiftGoodwill(value);var f=GiftFaction(receiver);
   if(gift.Spawned)gift.DeSpawn();else gift.holdingOwner?.Remove(gift);
   if(receiver.inventory==null || !receiver.inventory.innerContainer.TryAdd(gift))gift.Destroy();
   if(gw>0)Factions.ChangeGoodwillExact(f,gw,"云巨人的赠礼".Loc());
   var thought=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_CloudGiftReceived");
   if(thought!=null)receiver.needs?.mood?.thoughts?.memories?.TryGainMemory(thought,giver);
   if(giver!=null)Messages.Message(giver.LabelShort+"把".Loc()+gift.LabelCap+"（约".Loc()+value.ToString("0")+"银）送给了".Loc()+receiver.LabelShort+"：".Loc()+(f?.Name??"对方".Loc())+"好感 +".Loc()+gw+"。".Loc(),new LookTargets(receiver),MessageTypeDefOf.PositiveEvent,false);
   return gw;
  }
  // 可以送的东西：自己背包里的，和身边4格内地上没被禁止的。
  public static IEnumerable<Thing> GiftCandidates(Pawn giver){
   if(giver?.inventory!=null)foreach(var t in giver.inventory.innerContainer)if(t.MarketValue>0)yield return t;
   if(giver?.Map==null)yield break;
   foreach(var c in GenRadial.RadialCellsAround(giver.Position,4f,true)){
    if(!c.InBounds(giver.Map))continue;
    foreach(var t in c.GetThingList(giver.Map))if(t.def.EverHaulable && !(t is Pawn) && !(t is Corpse) && t.MarketValue>0 && !t.IsForbidden(giver) && giver.CanReserve(t))yield return t;
   }
  }

  // ---------- 云雾术与雾瓶：在一块圆形区域里维持致盲烟雾 ----------
  public static void Fog(Map map,IntVec3 center,float radius,int ticks){
   if(map==null || !center.IsValid)return;
   var s=CloudGiantState.Get;if(s!=null)s.fogs.Add(new FogPatch{map=map,center=center,radius=radius,until=Find.TickManager.TicksGame+ticks});
   FogPatch.Puff(map,center,radius);
  }

  // ---------- 唤风驱云 ----------
  public static readonly string[] WindChoices={"Clear","Fog","Rain"};
  public static string WindLabel(string w)=>w=="Clear"?"晴天".Loc():w=="Fog"?"雾".Loc():"下雨".Loc();
  public static GameCondition_LPWeather CallWind(Pawn caster,string weather)=>StormGiant.CallWeather(caster?.Map,StormGiant.W(weather),1f,false,caster?.Faction);

  // ---------- 云巨人贵族的装备：晨星锤 + 云纹长袍 ----------
  public static void EquipNoble(Pawn p){
   if(p?.equipment==null || p.apparel==null || !Active(p))return;
   var star=Items.Thing(Morningstar);
   if(star!=null && p.equipment.Primary?.def!=star && !p.WorkTagIsDisabled(WorkTags.Violent)){p.equipment.DestroyAllEquipment();var w=(ThingWithComps)ThingMaker.MakeThing(star);w.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good,ArtGenerationContext.Outsider);p.equipment.AddEquipment(w);}
   var robe=Items.Thing(Robe);
   if(robe!=null && Items.Worn(p,robe)==null){var a=(Apparel)ThingMaker.MakeThing(robe,GenStuff.DefaultStuffFor(robe));a.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good,ArtGenerationContext.Outsider);if(ApparelUtility.HasPartsToWear(p,robe))p.apparel.Wear(a,false);else a.Destroy();}
  }


  // ---------- 上位者的使节 ----------
  public const float EnvoyChance=.2f;public const int EnvoyGoodwill=20,EnvoyRefuse=-30,BowDays=15;
  public static bool LowGiant(Pawn p)=>GiantKin.Rank(p)>=3;
  public static bool HighGiant(Pawn p)=>GiantKin.Is(p) && GiantKin.Rank(p)<=2;
  public static bool EnvoyWanted(Map map){
   var c=Factions.Colony;if(map==null || !map.IsPlayerHome || c==null || c.defeated || !Factions.Enabled(c) || c.HostileTo(Faction.OfPlayer))return false;
   var giants=GiantKin.ColonyGiants(map);return giants.Any(LowGiant) && !giants.Any(HighGiant);
  }
  public static Pawn SendEnvoy(Map map){
   var c=Factions.Colony;var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail(NobleKind);if(c==null || kind==null || map==null)return null;
   if(!RCellFinder.TryFindRandomPawnEntryCell(out var entry,map,CellFinder.EdgeRoadChance_Friendly))return null;
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,c,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true));
   GenSpawn.Spawn(p,entry,map);
   if(!RCellFinder.TryFindRandomSpotJustOutsideColony(p,out var chill))chill=CellFinder.RandomClosewalkCellNear(map.Center,map,10);
   LordMaker.MakeNewLord(c,new LordJob_VisitColony(c,chill,GenDate.TicksPerDay),map,new List<Pawn>{p});
   var l=CloudLetter("上位者的使节".Loc(),"envoy",map,c);l.lookTargets=new LookTargets(p);l.pawn=p;
   l.Text=c.Name+"派来了一位云巨人贵族".Loc()+p.LabelShort+"。他看着殖民地里的火巨人、霜巨人，慢条斯理地说：\n\n“巨人的阶序不会因为离开故土就消失。承认云巨人在你们之上，同盟就当你们是体面的邻居。”\n\n承认阶序：同盟好感 +".Loc()+EnvoyGoodwill+"；殖民地里的火巨人、霜巨人".Loc()+BowDays+"天“俯首”-3，之后永久“名列阶序”+2。\n拒绝：".Loc()+c.Name+"好感 ".Loc()+EnvoyRefuse+"，30天内同盟会来一次袭击（袭击结束后好感回到0，不会永久敌对）。".Loc();
   Find.LetterStack.ReceiveLetter(l);
   return p;
  }
  public static void AcceptOrdning(Map map){
   var s=CloudGiantState.Get;Factions.ChangeGoodwillExact(Factions.Colony,EnvoyGoodwill,"承认巨人阶序".Loc());
   if(s!=null){s.ordningAccepted=Find.TickManager.TicksGame;s.ordningAcknowledged=true;}
   var bow=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_OrdningBowed");
   foreach(var g in GiantKin.ColonyGiants(map).Where(LowGiant))g.needs?.mood?.thoughts?.memories?.TryGainMemory(bow);
  }
  public static ScheduledRaid RefuseOrdning(){
   var c=Factions.Colony;Factions.ChangeGoodwillExact(c,EnvoyRefuse,"拒绝承认巨人阶序".Loc());
   var ev=Tribute.Events;if(ev==null || c==null)return null;
   var r=new ScheduledRaid{faction=c.def,fireTick=Find.TickManager.TicksGame+Rand.Range(5,30)*GenDate.TicksPerDay,pointsFactor=.8f,letterLabel="阶序之怒".Loc(),temporaryHostile=true};
   ev.scheduledRaids.Add(r);return r;
  }
  public static bool Listed(Pawn p){var s=CloudGiantState.Get;return s!=null && s.ordningAcknowledged && Find.TickManager.TicksGame-s.ordningAccepted>=BowDays*GenDate.TicksPerDay && LowGiant(p);}

  static Letter_CloudChoice CloudLetter(string label,string kind,Map map,Faction f){
   var l=(Letter_CloudChoice)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_CloudChoiceLetter"));
   l.Label=label;l.kind=kind;l.map=map;l.relatedFaction=f;return l;
  }
 }

 public class Gene_CloudGiant : Gene {
  public override void PostAdd(){
   base.PostAdd();GiantKin.Invalidate(pawn);
   if(pawn?.apparel!=null)foreach(var a in pawn.apparel.WornApparel.ToArray())if(!GiantGear.CanWear(pawn,a.def))pawn.apparel.TryDrop(a);
  }
  public override void PostRemove(){GiantGear.Release(pawn);base.PostRemove();}
 }
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class CloudNobleGenerated {
  static void Postfix(Pawn __result){if(__result!=null && CloudGiant.IsNobleKind(__result.kindDef))CloudGiant.EquipNoble(__result);}
 }

 // 踏云：原版 1.7 的“按角色的地形基础消耗”只对水格生效；这里对沼泽、泥地、浅水返回土壤的地形消耗（普通地面）。
 [HarmonyPatch(typeof(Pawn_PathFollower),"GetPawnCellBaseCostOverride")]
 public static class CloudWalkTerrain {
  static void Postfix(Pawn pawn,IntVec3 c,ref int? __result){if(CloudGiant.Walks(pawn) && pawn.Map!=null && CloudGiant.CloudTerrain(c.GetTerrain(pawn.Map)))__result=Math.Min(__result??int.MaxValue,TerrainDefOf.Soil.pathCost);}
 }
 // 踏云：不触发陷阱。
 [HarmonyPatch(typeof(Building_Trap),"CheckSpring")]
 public static class CloudWalkTraps {
  static bool Prefix(Pawn __0)=>!CloudGiant.Walks(__0);
 }

 public class ThoughtWorker_CloudNoble : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!CloudGiant.Has(p,CloudGiant.NobleGene) || !GiantKin.ColonyMember(p))return ThoughtState.Inactive;
   int s=CloudGiant.NobleStage(CloudGiant.Bedroom(p));return s<0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(s);
  }
 }
 // 名列阶序：承认阶序15天后，殖民地的火巨人、霜巨人永久 +2。
 public class ThoughtWorker_OrdningListed : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>GiantKin.ColonyMember(p) && CloudGiant.Listed(p)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }

 // 优雅者：招募谈话让抵抗多降 15%（照 1.32.0 罪犯背景的写法）。
 [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt),nameof(InteractionWorker_RecruitAttempt.Interacted))]
 public static class CloudGraceRecruit {
  static void Prefix(Pawn recipient,out float __state){__state=recipient?.guest?.resistance??-1f;}
  static void Postfix(Pawn initiator,Pawn recipient,float __state){
   if(__state<=0f || recipient?.guest==null || !CloudGiant.Has(initiator,CloudGiant.GraceGene))return;
   float drop=__state-recipient.guest.resistance;
   if(drop>0f)recipient.guest.resistance=Math.Max(0f,recipient.guest.resistance-drop*(CloudGiant.GraceRecruit-1f));
  }
 }
 // 云巨人晨星锤：命中 15% 把目标击退 2 格（复用 DNDPush）。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class CloudMorningstarKnock {
  public static float? TestRoll;
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){
   var caster=__instance.CasterPawn;var victim=target.Pawn;
   if(__instance.EquipmentSource?.def.defName!=CloudGiant.Morningstar || caster==null || victim==null || victim.Dead || !victim.Spawned || (TestRoll??Rand.Value)>=CloudGiant.KnockChance)return;
   DNDPush.Move(victim,DNDPush.Destination(victim.Map,victim.Position,(victim.Position-caster.Position).ToVector3(),CloudGiant.KnockCells));
  }
 }

 // ---------- 赠礼：右键非玩家派系的访客/商队成员 ----------
 public class FloatMenuOptionProvider_CloudGift : FloatMenuOptionProvider {
  protected override bool Drafted=>true;
  protected override bool Undrafted=>true;
  protected override bool Multiselect=>false;
  protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn,FloatMenuContext context){
   var giver=context.FirstSelectedPawn;
   if(giver==null || !CloudGiant.Has(giver,CloudGiant.GiftGene) || giver.Faction!=Faction.OfPlayer || clickedPawn==giver || !CloudGiant.CanReceive(clickedPawn))return null;
   if(!giver.CanReach(clickedPawn,PathEndMode.Touch,Danger.Deadly))return new FloatMenuOption("赠礼给".Loc()+clickedPawn.LabelShort+"（无法到达）".Loc(),null);
   var items=CloudGiant.GiftCandidates(giver).Distinct().OrderByDescending(t=>t.MarketValue*t.stackCount).Take(12).ToList();
   if(items.Count==0)return new FloatMenuOption("赠礼给".Loc()+clickedPawn.LabelShort+"（背包和身边4格内没有可以送的东西）".Loc(),null);
   return new FloatMenuOption("赠礼给".Loc()+clickedPawn.LabelShort+"……",()=>{
    var opts=items.Select(t=>{float v=t.MarketValue*t.stackCount;return new FloatMenuOption(t.LabelCap+"（约".Loc()+v.ToString("0")+"银，好感+".Loc()+CloudGiant.GiftGoodwill(v)+"）".Loc(),()=>{
     var job=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("LP_CloudGiveGift"),t,clickedPawn);job.count=t.stackCount;giver.jobs.TryTakeOrderedJob(job,JobTag.Misc);
    });}).ToList();
    Find.WindowStack.Add(new FloatMenu(opts));
   });
  }
 }
 public class JobDriver_CloudGiveGift : JobDriver {
  Thing Gift=>job.targetA.Thing;Pawn Receiver=>job.targetB.Pawn;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>Gift.ParentHolder==pawn.inventory || pawn.Reserve(Gift,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOn(()=>Receiver==null || !CloudGiant.CanReceive(Receiver) || !Receiver.Spawned);
   var gotoReceiver=Toils_Goto.GotoThing(TargetIndex.B,PathEndMode.Touch);
   // 背包里的东西直接拿到手上；地上的先走过去捡起来。
   yield return Toils_General.Do(()=>{if(Gift.ParentHolder==pawn.inventory)pawn.inventory.innerContainer.TryTransferToContainer(Gift,pawn.carryTracker.innerContainer,Gift.stackCount);});
   yield return Toils_Jump.JumpIf(gotoReceiver,()=>pawn.carryTracker.CarriedThing==Gift);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
   yield return Toils_Haul.StartCarryThing(TargetIndex.A,false,false,false,true,false);
   yield return gotoReceiver;
   yield return Toils_General.Do(()=>{var t=pawn.carryTracker.CarriedThing;if(t!=null)CloudGiant.Give(pawn,Receiver,t);});
  }
 }

 // ---------- 能力 ----------
 public class CompProperties_CloudAbility : CompProperties_AbilityEffect {
  public string effect;
  public CompProperties_CloudAbility(){compClass=typeof(CompCloudAbility);}
 }
 public class CompCloudAbility : CompAbilityEffect {
  public CompProperties_CloudAbility Props=>(CompProperties_CloudAbility)props;
  Pawn Caster=>parent.pawn;
  public override bool GizmoDisabled(out string reason){
   if(Props.effect=="wind" && !CloudGiant.Adult(Caster)){reason="只有成年的云巨人能唤风驱云。".Loc();return true;}
   return base.GizmoDisabled(out reason);
  }
  public string Invalid(LocalTargetInfo target){
   var map=Caster?.Map;if(map==null)return "施法者不在地图上。".Loc();
   switch(Props.effect){
    case "step":{var r=Races124.BasicLanding(Caster,target.Cell,CloudGiant.StepRange);if(r!=null)return r;return GenSight.LineOfSight(Caster.Position,target.Cell,map,true)?null:"看不见那里。".Loc();}
    case "fog":return !target.Cell.IsValid || !target.Cell.InBounds(map)?"需要地图上的目标位置。".Loc():null;
   }
   return null;
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   string reason=Invalid(target);
   if(reason!=null && throwMessages)Messages.Message(reason,MessageTypeDefOf.RejectInput,false);
   return reason==null && base.Valid(target,throwMessages);
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   if(Invalid(target)!=null)return;var map=Caster.Map;
   switch(Props.effect){
    case "step":
     for(int i=0;i<3;i++)FleckMaker.ThrowDustPuffThick(Caster.DrawPos,map,1.2f,new Color(.9f,.93f,1f));
     Races124.Blink(Caster,target.Cell);FleckMaker.ThrowAirPuffUp(target.Cell.ToVector3Shifted(),map);break;
    case "fog":CloudGiant.Fog(map,target.Cell,CloudGiant.FogRadius,CloudGiant.FogTicks);break;
    case "wind":
     // 选天气；关掉菜单没选就退还冷却。
     bool chosen=false;var ability=parent;var caster=Caster;
     var opts=CloudGiant.WindChoices.Select(w=>new FloatMenuOption("唤来".Loc()+CloudGiant.WindLabel(w)+"（持续1天）".Loc(),()=>{chosen=true;if(CloudGiant.CallWind(caster,w)!=null)Messages.Message(caster.LabelShort+"唤风驱云：天气转为".Loc()+CloudGiant.WindLabel(w)+"，持续一天。".Loc(),caster,MessageTypeDefOf.NeutralEvent,false);})).ToList();
     var menu=new FloatMenu(opts,"唤风驱云".Loc(),false);menu.onCloseCallback=()=>{if(!chosen)ability.ResetCooldown();};
     Find.WindowStack.Add(menu);break;
   }
  }
  public override void DrawEffectPreview(LocalTargetInfo target){
   if(Props.effect=="fog" && target.Cell.IsValid)GenDraw.DrawRadiusRing(target.Cell,CloudGiant.FogRadius);
  }
 }

 // ---------- 存档状态：云雾、阶序使节、限时袭击后的好感复原 ----------
 public class FogPatch : IExposable {
  public Map map;public IntVec3 center;public float radius;public int until;
  public static void Puff(Map map,IntVec3 center,float radius){
   foreach(var c in GenRadial.RadialCellsAround(center,radius,true))if(c.InBounds(map) && !c.Impassable(map))map.gasGrid.AddGas(c,GasType.BlindSmoke,255,true);
  }
  public void ExposeData(){Scribe_References.Look(ref map,"map");Scribe_Values.Look(ref center,"center");Scribe_Values.Look(ref radius,"radius");Scribe_Values.Look(ref until,"until");}
 }
 public class CloudGiantState : GameComponent {
  public List<FogPatch> fogs=new List<FogPatch>();
  public int ordningAccepted=-1,lastEnvoyYear=-1,temporaryRaidTick=-1;public bool ordningAcknowledged;public FactionDef temporaryRaid;
  public CloudGiantState(Game game){}
  public static CloudGiantState Get=>GameComp<CloudGiantState>.Get();
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;
   if(fogs.Count>0 && now%30==0){
    for(int i=fogs.Count-1;i>=0;i--){var f=fogs[i];if(f.map==null || !Find.Maps.Contains(f.map) || now>=f.until){fogs.RemoveAt(i);continue;}FogPatch.Puff(f.map,f.center,f.radius);}
   }
   if(now%250==131 && temporaryRaid!=null)CheckTemporaryRaid(now);
   if(now%GenDate.TicksPerDay==2131)DailyEnvoy();
  }
  public void DailyEnvoy(){
   var map=Find.AnyPlayerHomeMap;if(map==null || !CloudGiant.EnvoyWanted(map))return;
   int year=GenLocalDate.Year(map);if(year==lastEnvoyYear)return;lastEnvoyYear=year;
   if(Rand.Chance(CloudGiant.EnvoyChance))CloudGiant.SendEnvoy(map);
  }
  // 限时派系袭击（拒绝使节）落地后，袭击方的集团都撤离了就把好感复原为0。
  public void TrackTemporaryRaid(Faction f){temporaryRaid=f?.def;temporaryRaidTick=Find.TickManager.TicksGame;}
  public void CheckTemporaryRaid(int now){
   var f=Factions.Of(temporaryRaid);if(f==null){temporaryRaid=null;return;}
   if(now-temporaryRaidTick<2500)return;
   if(Find.Maps.Any(m=>m.lordManager.lords.Any(l=>l.faction==f && l.ownedPawns.Any(p=>p.Spawned && !p.Dead && !p.Downed))))return;
   temporaryRaid=null;
   if(f.HostileTo(Faction.OfPlayer)){Factions.SetRelation(f,Faction.OfPlayer,0);Messages.Message(f.Name+"的怒气消了：好感回到0，不再敌对。".Loc(),MessageTypeDefOf.NeutralEvent,false);}
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref fogs,"lpCloudFogs",LookMode.Deep);
   Scribe_Values.Look(ref ordningAccepted,"lpOrdningAccepted",-1);Scribe_Values.Look(ref ordningAcknowledged,"lpOrdningAcknowledged");Scribe_Values.Look(ref lastEnvoyYear,"lpLastEnvoyYear",-1);
   Scribe_Defs.Look(ref temporaryRaid,"lpTemporaryRaid");Scribe_Values.Look(ref temporaryRaidTick,"lpTemporaryRaidTick",-1);
   if(Scribe.mode==LoadSaveMode.PostLoadInit)fogs=fogs?.Where(f=>f?.map!=null).ToList()??new List<FogPatch>();
  }
 }

 public class Letter_CloudChoice : LP_EventLetter {
  public string kind;public Map map;public Pawn pawn;
  protected override bool Live=>kind=="envoy" && Here(pawn);
  public override IEnumerable<Thing> EventThings{get{if(pawn!=null)yield return pawn;}}
  public override IEnumerable<DiaOption> Choices{get{
   if(Closed){yield return Option_Close;yield break;}
   if(kind=="envoy"){
    yield return Choice("承认阶序".Loc(),()=>CloudGiant.AcceptOrdning(map));
    yield return Choice("拒绝".Loc(),()=>CloudGiant.RefuseOrdning());
   }
   yield return Option_Postpone;
  }}
  DiaOption Choice(string text,Action act)=>new DiaOption(text){action=()=>{act?.Invoke();Resolve();},resolveTree=true};
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref kind,"kind");Scribe_References.Look(ref map,"map");Scribe_References.Look(ref pawn,"lpPawn");}
 }
 // 事件：上位者的使节（平时每年一次 20% 由 CloudGiantState 判定；开发者模式可手动触发）。
 public class IncidentWorker_OrdningEnvoy : IncidentWorker {
  protected override bool CanFireNowSub(IncidentParms parms)=>CloudGiant.EnvoyWanted(parms.target as Map);
  protected override bool TryExecuteWorker(IncidentParms parms)=>CloudGiant.SendEnvoy(parms.target as Map)!=null;
 }
}
