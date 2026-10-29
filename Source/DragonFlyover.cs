using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;
using UnityEngine;

namespace LichPhylactery {
 // 1.31.0 龙过境（两阶段）。
 // 阶段一：地图条件“龙过境”1 天，天色略暗；每 2～4 小时龙影掠过一次（大轮廓 + 远处雷声），龙影 20 格内的野生动物惊逃离图。
 //   殖民者 -3“巨龙的阴影”，力能龙裔改为 +3“同类的气息”。20% 在条件期间吐息一次：离殖民者 ≥6 格的点，半径 3 内建筑与物品受 60 力场伤害、人只被推开，留下力场弹坑。
 // 阶段二：过境结束时殖民地有成年（龙龄 ≥3）力能龙裔：60% 一条外来古龙带 2～4 名仆从降落在离宝库 ≥15 格处，三选一：决斗、联姻、拒绝。
 public static class DragonFlyover {
  public const int DurationTicks=GenDate.TicksPerDay,AdultAge=3,AncientAge=4,SpotMinHoardDistance=15,RaidDelayDays=3,WinnerHoardDays=10,SilverReward=1500,ArtRewardCount=2,LossMaxItems=5,PushCells=2,MaxChampions=3;
  public const float FleeRadius=20f,BreathRadius=3f,BreathMinColonistDistance=6f,BreathChance=.2f,LandChance=.6f,BreathDamage=60f,LossShare=.2f,ShadowSpeed=40f,ShadowScale=24f;
  static GameConditionDef condition;static FleckDef fleck;static ThingDef crater;static FactionDef wanderers;
  public static GameConditionDef ConditionDef=>condition??(condition=DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_DragonShadow"));
  public static FleckDef ShadowFleck=>fleck??(fleck=DefDatabase<FleckDef>.GetNamedSilentFail("LP_DragonShadowFleck"));
  public static ThingDef CraterDef=>crater??(crater=DefDatabase<ThingDef>.GetNamedSilentFail("LP_ForceCrater"));
  public static FactionDef WanderersDef=>wanderers??(wanderers=DefDatabase<FactionDef>.GetNamedSilentFail("LP_DragonWanderers"));
  public static bool Active(Map map)=>map!=null && ConditionDef!=null && map.gameConditionManager.ConditionIsActive(ConditionDef);
  public static GameCondition_DragonShadow Condition(Map map)=>map?.gameConditionManager?.GetActiveCondition(ConditionDef) as GameCondition_DragonShadow;
  public static bool CanFire(Map map)=>LichMod.Settings.dragonFlyover && map!=null && map.IsPlayerHome && ConditionDef!=null && !Active(map);

  // ---------- 阶段一 ----------
  public static GameCondition_DragonShadow Start(Map map){
   var c=(GameCondition_DragonShadow)GameConditionMaker.MakeCondition(ConditionDef,DurationTicks);
   map.gameConditionManager.RegisterCondition(c);
   return c;
  }
  // 龙影：从地图一边横穿到另一边，行进的那一行上下 20 格内的野生动物惊逃。返回惊逃的数量。
  public static int Pass(Map map,int row=-1,bool? east=null){
   if(map==null)return 0;
   bool e=east??Rand.Bool;if(row<0)row=Rand.Range(10,Math.Max(11,map.Size.z-10));
   float dist=map.Size.x+30f;
   if(ShadowFleck!=null){
    var data=FleckMaker.GetDataStatic(new Vector3(e?-15f:map.Size.x+15f,0f,row),map,ShadowFleck,ShadowScale);
    data.velocityAngle=e?90f:270f;data.velocitySpeed=ShadowSpeed;data.rotation=e?0f:180f;data.solidTimeOverride=dist/ShadowSpeed;
    map.flecks.CreateFleck(data);
   }
   SoundDefOf.Thunder_OffMap?.PlayOneShotOnCamera(map);
   return ScareAnimals(map,row);
  }
  public static int ScareAnimals(Map map,int row){
   int n=0;
   foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()){
    if(p.Faction!=null || !p.RaceProps.Animal || p.Dead || p.Downed || p.InMentalState || Math.Abs(p.Position.z-row)>FleeRadius)continue;
    if(p.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.PanicFlee,"龙影",true)==true)n++;
   }
   return n;
  }
  public static IntVec3 BreathCell(Map map){
   var colonists=map.mapPawns.FreeColonistsSpawned.ToList();
   bool Ok(IntVec3 c)=>c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && colonists.All(p=>p.Position.DistanceTo(c)>=BreathMinColonistDistance);
   var anchor=map.listerBuildings.allBuildingsColonist.Count>0?map.listerBuildings.allBuildingsColonist.RandomElement().Position:map.Center;
   if(CellFinder.TryFindRandomCellNear(anchor,map,30,Ok,out var cell) || CellFinder.TryFindRandomCell(map,Ok,out cell))return cell;
   return IntVec3.Invalid;
  }
  public static bool Breath(Map map){
   var cell=BreathCell(map);if(!cell.IsValid)return false;
   var r=Breathe(map,cell);
   Find.LetterStack.ReceiveLetter("巨龙的吐息","盘旋的巨龙朝地面喷出了一道力场吐息！落点附近的建筑和物品被撕裂了"+(r.hit>0?"（"+r.hit+"件）":"")+"，人只是被冲开"+(r.pushed>0?"（"+r.pushed+"人）":"")+"，地上留下了一个力场弹坑。",LetterDefOf.NegativeEvent,new LookTargets(cell,map));
   return true;
  }
  // 吐息：半径 3 内建筑与物品受 60 点力场伤害（建筑抵消力场伤害对建筑的倍率，实际也是 60），人被推开 2 格不受伤。
  public static (int hit,int pushed) Breathe(Map map,IntVec3 center){
   int hit=0,pushed=0;var force=ForceDragon.Force;
   foreach(var t in GenRadial.RadialDistinctThingsAround(center,map,BreathRadius,true).ToList()){
    if(t.Destroyed)continue;
    if(t is Pawn p){
     var dir=(p.Position-center).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(Rand.Range(-1f,1f),0f,Rand.Range(-1f,1f));
     var dest=DNDPush.Destination(map,p.Position,dir,PushCells);if(dest!=p.Position){DNDPush.Move(p,dest);pushed++;}
     continue;
    }
    if(t.def.category!=ThingCategory.Building && t.def.category!=ThingCategory.Item)continue;
    if(!t.def.useHitPoints || t is Filth)continue;
    float amount=BreathDamage;
    if(t.def.category==ThingCategory.Building && force.buildingDamageFactor>0f)amount/=force.buildingDamageFactor;
    t.TakeDamage(new DamageInfo(force,amount,0f,-1f));hit++;
   }
   // 弹坑：落点那一格不收污物（水、沼泽等）就放到 6 格内最近能放的格子；全是沼泽就不留。
   if(CraterDef!=null){foreach(var c in GenRadial.RadialCellsAround(center,6f,true).Where(x=>x.InBounds(map)))if(FilthMaker.TryMakeFilth(c,map,CraterDef,3))break;foreach(var c in GenAdj.CellsAdjacent8Way(new TargetInfo(center,map)).Where(c=>c.InBounds(map) && Rand.Chance(.5f)))FilthMaker.TryMakeFilth(c,map,CraterDef,1);}
   FleckMaker.Static(center,map,FleckDefOf.PsycastAreaEffect,6f);
   for(int i=0;i<6;i++)FleckMaker.ThrowDustPuffThick(center.ToVector3Shifted(),map,2.5f,new Color(.7f,.8f,1f));
   return (hit,pushed);
  }

  // ---------- 阶段二 ----------
  public static List<Pawn> AdultDragons(Map map)=>map==null?new List<Pawn>():map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Dead && ForceDragon.Active(p) && ForceDragon.Age(p)>=AdultAge).ToList();
  public static DragonVisit OnFlyoverEnd(Map map){
   if(map==null || !Find.Maps.Contains(map))return null;
   var adults=AdultDragons(map);if(adults.Count==0){Dragons.OnFlyoverEnd(map);return null;} // 1.52.0：没有成年力能龙裔时 25% 五色龙降落勒索
   if(Events2.Roll>=LandChance)return null;
   return Land(map,adults.OrderByDescending(ForceDragon.Age).First());
  }
  // 隐藏派系：没有就补上，新建时把好感设为 0。
  public static Faction Wanderers(){
   if(WanderersDef==null)return null;
   bool had=Factions.Of(WanderersDef)!=null;
   var f=Factions.EnsureHidden(WanderersDef);
   if(f!=null && !had)Factions.SetRelation(f,Faction.OfPlayer,0);
   return f;
  }
  public static Pawn MakeDragon(Faction f,Gender gender,int tile){
   var kind=DefDatabase<PawnKindDef>.GetNamed("LP_WildDragon");
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,f,PawnGenerationContext.NonPlayer,tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedGender:gender,forcedXenotype:DragonHoard.Xenotype));
   var heart=ForceDragon.Heart(p);
   if(heart!=null){heart.SetAge(AncientAge);heart.daysOnHoard=ForceDragon.DaysNeeded(AncientAge-1);heart.Value=heart.Max;}
   return p;
  }
  public static Pawn MakeServant(Faction f,int tile)=>PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_DragonServant"),f,PawnGenerationContext.NonPlayer,tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,forcedXenotype:XenotypeDefOf.Baseliner));
  public static Room Hoard(Pawn host)=>DragonHoard.RoomOf(host);
  public static IntVec3 HoardCenter(Pawn host){var room=Hoard(host);if(room==null)return host?.Position??IntVec3.Invalid;var cells=room.Cells.ToList();return new IntVec3((int)cells.Average(c=>c.x),0,(int)cells.Average(c=>c.z));}
  public static IntVec3 HoardDoor(Pawn host){
   var room=Hoard(host);if(room==null)return host?.PositionHeld??IntVec3.Invalid;
   var door=room.ContainedAndAdjacentThings.OfType<Building_Door>().FirstOrDefault();
   return door?.Position??room.Cells.First();
  }
  public static bool SpotOk(IntVec3 c,Map map,IntVec3 hoard)=>c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && !c.Roofed(map) && c.DistanceTo(hoard)>=SpotMinHoardDistance && map.reachability.CanReachColony(c);
  public static DragonVisit Land(Map map,Pawn host,int servants=-1){
   var f=Wanderers();if(f==null || host==null)return null;
   var hoard=HoardCenter(host);
   if(!CellFinder.TryFindRandomCellNear(hoard,map,40,c=>SpotOk(c,map,hoard),out var spot) && !CellFinder.TryFindRandomCell(map,c=>SpotOk(c,map,hoard),out spot))return null;
   if(!Events2.EdgeCell(map,out var entry,spot))return null;
   var dragon=MakeDragon(f,host.gender==Gender.Male?Gender.Female:Gender.Male,map.Tile);
   var list=new List<Pawn>();int n=servants>0?servants:Rand.RangeInclusive(2,4);
   for(int i=0;i<n;i++)list.Add(MakeServant(f,map.Tile));
   foreach(var p in list.Prepend(dragon)){GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,5),map);Events2.MarkVisitor(p,"dragon");}
   var lord=LordMaker.MakeNewLord(f,new LordJob_DragonVisit(spot),map,list.Prepend(dragon));
   var v=new DragonVisit{map=map,lord=lord,dragon=dragon,host=host,servants=list,spot=spot,stage="arriving"};
   Events2.State?.dragonVisits.Add(v);
   Find.LetterStack.ReceiveLetter("龙过境：降落",ForceDragon.AgeLabel(AncientAge)+dragon.LabelShort+"嗅到了"+host.LabelShort+"的气息，带着"+n+"名仆从降落在殖民地外。它正朝你们走来。",LetterDefOf.NeutralEvent,dragon);
   return v;
  }
  public static DragonVisit VisitOf(Lord lord)=>Events2.State?.dragonVisits.FirstOrDefault(v=>v.lord==lord);
  public static DragonVisit VisitOf(Pawn p)=>p==null?null:Events2.State?.dragonVisits.FirstOrDefault(v=>v.dragon==p || v.servants.Contains(p));
  public static Letter_DragonVisit OnArrive(DragonVisit v){
   if(v==null || v.stage!="arriving")return null;v.stage="waiting";v.arriveTick=Events2.Now;
   var l=(Letter_DragonVisit)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_DragonChoiceLetter"));
   l.Label="另一条龙";l.visit=v;l.lookTargets=new LookTargets(v.dragon);l.relatedFaction=v.lord?.faction;
   l.Text=v.dragon.LabelShort+"（"+ForceDragon.AgeLabel(ForceDragon.Age(v.dragon))+"，"+(v.dragon.gender==Gender.Female?"雌":"雄")+"）在殖民地外停下，隔着老远就冲着"+v.host.LabelShort+"的宝库咆哮。它的仆从站开了一圈。\n\n"
    +"决斗：派一条成年以上的龙和它一对一（倒地即分胜负，不补刀；其他人不许插手，否则算拒绝）。赢了：它把随身的宝藏（"+SilverReward+"银和"+ArtRewardCount+"件珍品）放在宝库门口离开，你的龙心情+10、守着宝库的天数+10。输了：它的仆从搬走宝库里最值钱的两成东西（最多"+LossMaxItems+"件），你的龙心情-8。\n"
    +"联姻：殖民地最好的谈判者去说服它（成功率 = 最高社交×5%，最多75%）。成功：它以"+ForceDragon.AgeLabel(ForceDragon.Age(v.dragon))+"之身加入殖民地，可以和"+v.host.LabelShort+"筑巢产蛋；失败按拒绝处理。\n"
    +"拒绝：它带着仆从离开，"+RaidDelayDays+"天后回来抢你的宝库。\n\n一天之内不答复，它会失去耐心自己离开。";
   Find.LetterStack.ReceiveLetter(l);
   return l;
  }
  public static void Leave(DragonVisit v){v.lord?.ReceiveMemo(LordJob_DragonVisit.LeaveMemo);}

  public static bool Duel(DragonVisit v,Pawn champion){
   if(v==null || v.answered || champion==null || champion.Dead || champion.Downed || !champion.Spawned || champion.Map!=v.map || !v.dragon.Spawned)return false;
   v.answered=true;v.stage="duel";v.champion=champion;v.duelTick=Events2.Now;
   StartFight(v);
   Messages.Message(champion.LabelShort+"迎向"+v.dragon.LabelShort+"，两条龙的决斗开始了！",new LookTargets(champion,v.dragon),MessageTypeDefOf.NeutralEvent,false);
   return true;
  }
  // 原版的社交斗殴（非致命、倒地即止）；斗殴状态起不来时退回到互相近战的工作。
  public static void StartFight(DragonVisit v){
   var a=v.champion;var b=v.dragon;
   if(a.drafter!=null)a.drafter.Drafted=false;
   if(!(a.MentalStateDef==MentalStateDefOf.SocialFighting) || !(b.MentalStateDef==MentalStateDefOf.SocialFighting)){
    a.interactions?.StartSocialFight(b,"MessageSocialFight");
   }
   if(a.MentalStateDef!=MentalStateDefOf.SocialFighting || b.MentalStateDef!=MentalStateDefOf.SocialFighting){
    foreach(var (x,y) in new[]{(a,b),(b,a)}){if(x.InMentalState)continue;var job=JobMaker.MakeJob(JobDefOf.AttackMelee,y);job.killIncappedTarget=false;job.expiryInterval=600;x.jobs.StartJob(job,JobCondition.InterruptForced);}
   }
  }
  public static void Tick(Events2State s){
   for(int i=s.dragonVisits.Count-1;i>=0;i--){
    var v=s.dragonVisits[i];
    bool lordGone=v.lord==null || v.map==null || !Find.Maps.Contains(v.map) || !v.map.lordManager.lords.Contains(v.lord);
    if(v.stage=="duel")TickDuel(v);
    if(lordGone && v.stage!="duel"){s.dragonVisits.RemoveAt(i);continue;}
    if(v.stage=="waiting" && !v.answered && Events2.Now-v.arriveTick>=GenDate.TicksPerDay){v.answered=true;v.stage="done";Leave(v);Messages.Message(v.dragon.LabelShort+"等得不耐烦，带着仆从离开了。",new LookTargets(v.dragon),MessageTypeDefOf.NeutralEvent,false);}
   }
  }
  public static void TickDuel(DragonVisit v){
   var a=v.champion;var b=v.dragon;
   if(b==null || b.Dead || b.Destroyed || b.Downed){Win(v);return;}
   if(a==null || a.Dead || a.Destroyed || a.Downed || !a.Spawned || a.Map!=v.map){Lose(v);return;}
   if(!a.InMentalState && !b.InMentalState && a.CurJob?.def!=JobDefOf.AttackMelee)StartFight(v);
  }
  static void EndFight(DragonVisit v){foreach(var p in new[]{v.champion,v.dragon})if(p!=null && !p.Dead && p.InMentalState)p.MentalState.RecoverFromState();}
  public static List<Thing> Win(DragonVisit v){
   v.stage="done";EndFight(v);var map=v.map;var host=v.champion??v.host;
   // 倒地的古龙站起来才能离开：只清掉这次决斗留下的伤。
   if(!v.dragon.Dead)foreach(var h in v.dragon.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())v.dragon.health.RemoveHediff(h);
   var loot=Reward();var door=HoardDoor(Owner(v));if(!door.IsValid)door=host.PositionHeld;
   foreach(var t in loot)GenPlace.TryPlaceThing(t,door,map,ThingPlaceMode.Near);
   var champ=v.champion;
   if(champ!=null && !champ.Dead){champ.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("LP_DuelWinner"));var heart=ForceDragon.Heart(champ);if(heart!=null)heart.daysOnHoard+=WinnerHoardDays;Adventurer.Milestone(champ,"dragonDuel",Adventurer.DragonDuelXP);}
   Leave(v);
   Find.LetterStack.ReceiveLetter("决斗胜利",(champ?.LabelShort??"你的龙")+"打倒了"+v.dragon.LabelShort+"！落败的古龙把随身的宝藏（"+string.Join("、",loot.Select(t=>t.LabelCap))+"）放在宝库门口，带着仆从离开了。\n\n"+(champ?.LabelShort??"")+"心情+10（15天），守着宝库的天数+"+WinnerHoardDays+"。",LetterDefOf.PositiveEvent,new LookTargets(door,map));
   return loot;
  }
  // 决斗的宝库：出战的龙自己有宝库就用它的，否则用被嗅到的那条龙的。
  public static Pawn Owner(DragonVisit v)=>v.champion!=null && Hoard(v.champion)!=null?v.champion:v.host;
  public static List<Thing> Reward(){
   var list=new List<Thing>();
   for(int left=SilverReward;left>0;){var s=ThingMaker.MakeThing(ThingDefOf.Silver);s.stackCount=Math.Min(left,ThingDefOf.Silver.stackLimit);left-=s.stackCount;list.Add(s);}
   for(int i=0;i<ArtRewardCount;i++){var t=Rand.Bool?Art():TowerCrash.MakeWondrous();if(t!=null)list.Add(t);}
   return list;
  }
  static Thing Art(){
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("SculptureSmall");if(def==null)return TowerCrash.MakeWondrous();
   var t=ThingMaker.MakeThing(def,ThingDefOf.Gold);t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good,ArtGenerationContext.Outsider);
   return def.Minifiable?t.MakeMinified():t;
  }
  // 输了：宝库里按价值排前 20% 的物品（最多 5 件）被仆从装进背包带走。
  public static List<Thing> HoardLoss(Pawn host){
   var room=Hoard(host);if(room==null)return new List<Thing>();
   var items=room.Cells.SelectMany(c=>c.GetThingList(room.Map)).Where(DragonHoard.Counts).Distinct().OrderByDescending(DragonHoard.ItemValue).ToList();
   int n=Math.Min(LossMaxItems,Mathf.CeilToInt(items.Count*LossShare));
   return items.Take(n).ToList();
  }
  public static List<Thing> Lose(DragonVisit v){
   v.stage="done";EndFight(v);
   var taken=HoardLoss(Owner(v));
   var carriers=v.servants.Where(p=>p!=null && !p.Dead && p.Spawned).ToList();if(carriers.Count==0 && v.dragon.Spawned)carriers.Add(v.dragon);
   int i=0;
   foreach(var t in taken){
    if(carriers.Count==0){t.Destroy();continue;}
    var c=carriers[i++%carriers.Count];if(t.Spawned)t.DeSpawn();
    if(!c.inventory.innerContainer.TryAdd(t))t.Destroy();
   }
   var champ=v.champion;if(champ!=null && !champ.Dead)champ.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("LP_DuelLoser"));
   Leave(v);
   Find.LetterStack.ReceiveLetter("决斗失败",(champ?.LabelShort??"你的龙")+"倒在了"+v.dragon.LabelShort+"面前。古龙的仆从走进宝库，搬走了"+(taken.Count>0?string.Join("、",taken.Select(t=>t.LabelCap)):"——宝库里没什么可拿的")+"，然后跟着主人离开了。",LetterDefOf.NegativeEvent,new LookTargets(v.dragon));
   return taken;
  }
  public static bool Marry(DragonVisit v){
   if(v==null || v.answered)return false;
   float chance=Events2.SocialChance(v.map);
   if(Events2.Roll>=chance){Refuse(v,"谈判失败了：古龙觉得受了冒犯。");return false;}
   v.answered=true;v.stage="done";
   var d=v.dragon;Items.Remove(d,"LP_EventVisitor");d.SetFaction(Faction.OfPlayer);
   Leave(v);
   Find.LetterStack.ReceiveLetter("联姻",d.LabelShort+"被说服了：它以"+ForceDragon.AgeLabel(ForceDragon.Age(d))+"之身留在殖民地，和"+v.host.LabelShort+"结为伴侣。给它们一间共同的宝库，它们就会筑巢产蛋。仆从们各自散去。",LetterDefOf.PositiveEvent,d);
   return true;
  }
  public static DragonRaid Refuse(DragonVisit v,string reason=null){
   if(v==null || v.stage=="done")return null;
   v.answered=true;v.stage="done";EndFight(v);Leave(v);
   var r=new DragonRaid{map=v.map,host=v.host,dragon=v.dragon,servants=v.servants.ToList(),tick=Events2.Now+RaidDelayDays*GenDate.TicksPerDay};
   Events2.State?.dragonRaids.Add(r);
   Find.LetterStack.ReceiveLetter("古龙的威胁",(reason==null?"":reason+"\n\n")+v.dragon.LabelShort+"带着仆从离开了，临走前扬言"+RaidDelayDays+"天后回来抢走"+v.host.LabelShort+"的宝库。",LetterDefOf.ThreatSmall,new LookTargets(v.dragon));
   return r;
  }
  // 殖民者对古龙一行动手：决斗中出战的龙打古龙不算；其他一律按拒绝处理。
  public static void OnVisitorHarmed(Pawn victim,Pawn attacker){
   var v=VisitOf(victim);if(v==null || v.stage=="done")return;
   if(v.stage=="duel" && attacker==v.champion && victim==v.dragon)return;
   Refuse(v,attacker.LabelShort+"对"+victim.LabelShort+"动了手，古龙认定这是拒绝。");
  }

  // ---------- 拒绝后的袭击 ----------
  public static void TickRaids(Events2State s){
   int now=Events2.Now;
   for(int i=s.dragonRaids.Count-1;i>=0;i--){
    var r=s.dragonRaids[i];
    if(r.map==null || !Find.Maps.Contains(r.map)){s.dragonRaids.RemoveAt(i);ResetWanderers();continue;}
    if(!r.fired){if(now>=r.tick)FireRaid(r);continue;}
    if(r.lord==null || !r.map.lordManager.lords.Contains(r.lord)){s.dragonRaids.RemoveAt(i);ResetWanderers();}
   }
  }
  // 袭击结束后不留世界影响：好感回到 0。
  public static void ResetWanderers(){var f=Factions.Of(WanderersDef);if(f!=null)Factions.SetRelation(f,Faction.OfPlayer,0);}
  static Pawn Reuse(Pawn p,Faction f){
   if(p==null || p.Dead || p.Destroyed || p.Discarded || p.Spawned)return null;
   if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);
   if(p.Faction!=f)p.SetFaction(f);
   return p;
  }
  public static Lord FireRaid(DragonRaid r){
   r.fired=true;var map=r.map;var f=Wanderers();if(f==null)return null;
   Factions.SetRelation(f,Faction.OfPlayer,-100);
   var pawns=new List<Pawn>();
   var dragon=Reuse(r.dragon,f)??MakeDragon(f,r.host!=null && r.host.gender==Gender.Male?Gender.Female:Gender.Male,map.Tile);pawns.Add(dragon);
   foreach(var s in r.servants){var p=Reuse(s,f);if(p!=null)pawns.Add(p);}
   while(pawns.Count<3)pawns.Add(MakeServant(f,map.Tile));
   var target=r.host!=null && r.host.Spawned && r.host.Map==map?HoardCenter(r.host):map.Center;
   if(!Events2.EdgeCell(map,out var entry,target)){Factions.SetRelation(f,Faction.OfPlayer,0);return null;}
   foreach(var p in pawns){Items.Remove(p,"LP_EventVisitor");GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,5),map);}
   r.lord=LordMaker.MakeNewLord(f,new LordJob_Steal(),map,pawns);
   Find.LetterStack.ReceiveLetter("古龙来抢宝库",dragon.LabelShort+"说到做到，带着"+(pawns.Count-1)+"名仆从回来了！它们要抢走宝库里的东西，抢到就撤。",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f);
   return r.lord;
  }
 }

 public class DragonVisit : IExposable {
  public Map map;public Lord lord;public Pawn dragon,host,champion;public List<Pawn> servants=new List<Pawn>();public IntVec3 spot;public string stage="arriving";public bool answered;public int arriveTick=-1,duelTick=-1;
  public void ExposeData(){
   Scribe_References.Look(ref map,"map");Scribe_References.Look(ref lord,"lord");Scribe_References.Look(ref dragon,"dragon");Scribe_References.Look(ref host,"host");Scribe_References.Look(ref champion,"champion");
   ScribePawns.Look(ref servants,"servants");Scribe_Values.Look(ref spot,"spot");Scribe_Values.Look(ref stage,"stage","arriving");Scribe_Values.Look(ref answered,"answered");Scribe_Values.Look(ref arriveTick,"arriveTick",-1);Scribe_Values.Look(ref duelTick,"duelTick",-1);
  }
 }
 public class DragonRaid : IExposable {
  public Map map;public Lord lord;public Pawn dragon,host;public List<Pawn> servants=new List<Pawn>();public int tick;public bool fired;
  public void ExposeData(){
   Scribe_References.Look(ref map,"map");Scribe_References.Look(ref lord,"lord");Scribe_References.Look(ref dragon,"dragon");Scribe_References.Look(ref host,"host");
   ScribePawns.Look(ref servants,"servants");Scribe_Values.Look(ref tick,"tick");Scribe_Values.Look(ref fired,"fired");
  }
 }

 // 走到停留点 → 站开等待答复 → 离开。被打成敌人就进攻。
 public class LordJob_DragonVisit : LordJob {
  public const string LeaveMemo="LP_DragonLeave";
  public IntVec3 spot;
  public LordJob_DragonVisit(){}
  public LordJob_DragonVisit(IntVec3 spot){this.spot=spot;}
  public override StateGraph CreateGraph(){
   var g=new StateGraph();
   var travel=new LordToil_Travel(spot);g.StartingToil=travel;
   var wait=new LordToil_DefendPoint(spot,6f,null);g.AddToil(wait);
   var leave=new LordToil_ExitMap(LocomotionUrgency.Walk,false,true);g.AddToil(leave);
   var fight=new LordToil_AssaultColony(false,false);g.AddToil(fight);
   var arrive=new Transition(travel,wait);arrive.AddTrigger(new Trigger_Memo("TravelArrived"));arrive.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerDay/2));arrive.AddPreAction(new TransitionAction_Custom(()=>DragonFlyover.OnArrive(DragonFlyover.VisitOf(lord))));g.AddTransition(arrive);
   var go=new Transition(wait,leave);go.AddSource(travel);go.AddTrigger(new Trigger_Memo(LeaveMemo));g.AddTransition(go);
   var hostile=new Transition(travel,fight);hostile.AddSources(wait,leave);hostile.AddTrigger(new Trigger_BecamePlayerEnemy());g.AddTransition(hostile);
   return g;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref spot,"spot");}
 }

 public class Letter_DragonVisit : ChoiceLetter {
  public DragonVisit visit;
  public override IEnumerable<DiaOption> Choices{get{
   if(ArchivedOnly || visit==null){yield return Option_Close;yield break;}
   var champs=DragonFlyover.AdultDragons(visit.map).Where(p=>p.Spawned && !p.Downed).Take(DragonFlyover.MaxChampions).ToList();
   if(champs.Count==0){var none=new DiaOption("决斗");none.Disable("殖民地里没有能出战的成年龙");yield return none;}
   foreach(var c in champs){var o=Choice("决斗：派"+c.LabelShort+"（"+ForceDragon.AgeLabel(ForceDragon.Age(c))+"）出战",()=>DragonFlyover.Duel(visit,c));if(visit.answered)o.Disable("已经答复过了");yield return o;}
   var marry=Choice("联姻（成功率"+Events2.SocialChance(visit.map).ToStringPercent()+"）",()=>DragonFlyover.Marry(visit));if(visit.answered)marry.Disable("已经答复过了");yield return marry;
   var no=Choice("拒绝",()=>DragonFlyover.Refuse(visit));if(visit.answered)no.Disable("已经答复过了");yield return no;
   yield return Option_Postpone;
  }}
  DiaOption Choice(string text,Action act)=>new DiaOption(text){action=()=>{act?.Invoke();Find.LetterStack.RemoveLetter(this);},resolveTree=true};
  public override void ExposeData(){base.ExposeData();Scribe_Deep.Look(ref visit,"lpDragonVisitCopy");
   if(Scribe.mode==LoadSaveMode.PostLoadInit && visit?.lord!=null){var live=DragonFlyover.VisitOf(visit.lord);if(live!=null)visit=live;}
  }
 }

 // 地图条件：天色略暗，按时放龙影；20% 安排一次吐息；结束时掷降落。
 public class GameCondition_DragonShadow : GameCondition {
  public int nextShadowTick=-1,breathTick=-1,passes;public bool breathed,landingRolled;
  static readonly SkyColorSet Sky=new SkyColorSet(new Color(.55f,.58f,.66f),new Color(.30f,.32f,.38f),new Color(.60f,.62f,.70f),.9f);
  public override int TransitionTicks=>1200;
  public override float SkyTargetLerpFactor(Map map)=>GameConditionUtility.LerpInOutValue(this,TransitionTicks,.35f);
  public override SkyTarget? SkyTarget(Map map)=>new SkyTarget(.75f,Sky,1f,1f);
  public Map Map=>SingleMap;
  public override void Init(){
   base.Init();
   nextShadowTick=startTick+GenDate.TicksPerHour;
   if(Events2.Roll<DragonFlyover.BreathChance)breathTick=startTick+(int)(Rand.Range(.1f,.9f)*Duration);
  }
  public override void GameConditionTick(){
   base.GameConditionTick();
   var map=SingleMap;if(map==null)return;int now=Find.TickManager.TicksGame;
   if(nextShadowTick>=0 && now>=nextShadowTick){DragonFlyover.Pass(map);passes++;nextShadowTick=now+(int)(Rand.Range(2f,4f)*GenDate.TicksPerHour);}
   if(breathTick>=0 && !breathed && now>=breathTick){breathed=true;DragonFlyover.Breath(map);}
  }
  public override void End(){
   var map=SingleMap;base.End();
   if(!landingRolled){landingRolled=true;DragonFlyover.OnFlyoverEnd(map);}
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref nextShadowTick,"lpNextShadow",-1);Scribe_Values.Look(ref breathTick,"lpBreathTick",-1);Scribe_Values.Look(ref passes,"lpPasses");
   Scribe_Values.Look(ref breathed,"lpBreathed");Scribe_Values.Look(ref landingRolled,"lpLandingRolled");
  }
 }
 public class ThoughtWorker_DragonShadow : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.Faction==Faction.OfPlayer && p.Spawned && DragonFlyover.Active(p.Map) && !ForceDragon.Active(p);
 }
 public class ThoughtWorker_DragonKinScent : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.Faction==Faction.OfPlayer && p.Spawned && DragonFlyover.Active(p.Map) && ForceDragon.Active(p);
 }
 public class IncidentWorker_DragonFlyover : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms)=>DragonFlyover.CanFire(parms.target as Map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;if(!DragonFlyover.CanFire(map))return false;
   DragonFlyover.Start(map);
   SendStandardLetter("龙过境","一条巨龙在这片天空上盘旋，接下来一天里它的影子会不时掠过大地。龙影经过的地方，野兽会惊慌逃离。\n\n殖民者看着头顶的巨龙心里发毛（心情-3）；力能龙裔却嗅到了同类的气息（心情+3）。\n\n它偶尔可能朝地面喷一口力场吐息。"+(DragonFlyover.AdultDragons(map).Count>0?"\n\n殖民地里的成年龙已经抬头盯住了它……":""),LetterDefOf.NeutralEvent,parms,LookTargets.Invalid);
   return true;
  }
 }
}
