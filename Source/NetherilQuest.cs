using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.37.0 拉洛克的邀约：NetherilQuestState 驱动（阶段、倒计时、选择全部存档），每一步都有信件说明下一步。
 // 阶段 stage：idle（没触发）→ scheduled（使者在路上）→ envoy（使者在殖民地边上等回复）→ errand（三件差事）/ refused（拒绝后等 60 天）/ crypt（古墓已显露）→ scroll（卷轴到手）。
 // 卡苏斯之卷的下落 karsusStatus：none 未出现 / larloch 在拉洛克手里（古墓里）/ site 在浮城残骸里 / player 在玩家手里 / lost 已毁待重现；已解读另记 deciphered。
 public static class NetherilQuest {
  public const int EnvoyDaysMin=3,EnvoyDaysMax=10,EnvoyStayTicks=GenDate.TicksPerDay,RefuseDays=60,MaxRefusals=3,ErrandDays=30,ResidueNeed=20,ShardNeed=3,CasketRaidTicks=6*GenDate.TicksPerHour,CasketStayTicks=GenDate.TicksPerDay,ReappearMin=30,ReappearMax=60,WreckDays=30,RaidMin=60,RaidMax=90;
  public const float ReadHoursNeed=20f,CampPoints=1f,CryptPoints=1.5f,WreckPoints=1.2f,CasketRaidPoints=1.2f,UndeadRaidPoints=.6f,ThayScrollWeight=1.25f;
  public const int ASendPsylink=4,ASendVPE=10,ErrandGoodwill=30,SilverReward=1000;
  public const string ResearchName="LP_Research_NetherilArcana";
  static FactionDef larlochDef;
  public static FactionDef LarlochDef=>larlochDef??(larlochDef=DefDatabase<FactionDef>.GetNamedSilentFail("LP_Larloch"));
  public static NetherilQuestState State=>Netheril.State;
  public static Thing Karsus=>State?.karsus is Thing t && !t.Destroyed?t:null;
  public static int Now=>Find.TickManager.TicksGame;
  public static bool ResearchDone=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail(ResearchName)?.IsFinished==true;
  public static Map Home=>Find.AnyPlayerHomeMap;
  public static Faction Larloch{get{
   var def=LarlochDef;if(def==null || Find.FactionManager==null)return null;
   var f=Factions.Of(def);if(f!=null)return f;
   f=Factions.EnsureHidden(def);if(f==null)return null;
   Factions.SetRelation(f,Faction.OfPlayer,0);
   var thay=Factions.Hostile;if(thay!=null)Factions.SetRelation(f,thay,-100);
   return f;
  }}
  public static Faction Thay{get{var f=Factions.Hostile;return f!=null && !f.defeated && Factions.Enabled(f)?f:null;}}
  public static bool PhylacteryLost(Pawn p)=>Items.Has(p,"LP_PhylacteryLost");
  public static Pawn PawnById(int id){
   foreach(var p in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)if(p.thingIDNumber==id)return p;
   return null;
  }
  static void Letter(string label,string text,LetterDef def,LookTargets look=null){Find.LetterStack.ReceiveLetter(label,text,def,look??LookTargets.Invalid,Larloch);}

  // ---------- 有卷轴时塞伊袭击权重 ×1.25（每 250 刻重新数一次） ----------
  static int scrollCacheTick=-99999;static bool scrollCache;
  public static void InvalidateScrollCache(){scrollCacheTick=-99999;}
  public static bool ColonyHasScroll{get{
   if(Current.Game==null)return false;int now=Now;if(now-scrollCacheTick<250)return scrollCache;scrollCacheTick=now;
   scrollCache=Find.Maps.Any(m=>m.IsPlayerHome && (Count(m,Netheril.StudyDef)+Count(m,Netheril.ArcaneDef)+Count(m,Netheril.KarsusDef)>0 || m.listerBuildings.allBuildingsColonist.OfType<Building_Bookcase>().Any(b=>b.GetDirectlyHeldThings().Any(t=>Netheril.IsNetheril(t.def)))));
   return scrollCache;
  }}
  static int Count(Map m,string def){var d=Items.Thing(def);return d==null?0:m.listerThings.ThingsOfDef(d).Count;}

  // ---------- 卡苏斯之卷 ----------
  public static Thing MakeKarsus(){
   var st=State;if(st==null)return null;
   if(Karsus!=null)return null;
   var t=(Book_Netheril)Netheril.Make(Netheril.KarsusDef,QualityCategory.Legendary);if(t==null)return null;
   t.legit=true;st.karsus=t;return t;
  }
  // 世界唯一：登记过的那卷还在，另一卷（开发者模式生成之类）就地消失；没登记过就收下它。
  public static void CheckKarsus(Book_Netheril b){
   var st=State;if(st==null || b==null || b.Destroyed)return;
   if(st.karsus==b)return;
   if(Karsus!=null){if(b.Spawned)b.Destroy(DestroyMode.Vanish);return;}
   st.karsus=b;b.legit=true;if(PlayerHolds(b))st.karsusStatus="player";
  }
  public static bool PlayerHolds(Thing t){
   if(t==null || t.Destroyed)return false;
   if(t.ParentHolder is Pawn_InventoryTracker inv && inv.pawn?.Faction==Faction.OfPlayer)return true;
   if(t.ParentHolder is Pawn_CarryTracker carry && carry.pawn?.Faction==Faction.OfPlayer)return true;
   if(t.GetCaravan()?.IsPlayerControlled==true)return true;
   return t.MapHeld?.IsPlayerHome==true;
  }
  // 还在玩家能碰到的地方：任何地图上、玩家商队里、路上的运输舱里或玩家的人身上。被小偷带出地图（进了世界角色池）就算遗失。
  public static bool Reachable(Thing t){if(t.MapHeld!=null || t.GetCaravan()!=null)return true;IThingHolder h=t.ParentHolder;while(h!=null){if(h is WorldObject)return true;if(h is Pawn p && p.Faction==Faction.OfPlayer)return true;h=h.ParentHolder;}return false;}
  public static void GiveKarsus(Map map,IntVec3 near,string how){
   var t=MakeKarsus();if(t==null)return;
   GenPlace.TryPlaceThing(t,near,map,ThingPlaceMode.Near);t.SetForbidden(false,false);
   State.karsusStatus="player";OnObtained(t,how);
  }
  public static void OnObtained(Thing t,string how){
   var st=State;st.karsusStatus="player";st.stage="scroll";st.deal=st.deal??how;
   Letter("卡苏斯之卷到手","卡苏斯之卷——耐瑟瑞尔奥术师卡苏斯妄图夺取魔网神格时写下的卷轴——现在在你手里了。"+(how!=null?"（"+how+"）":"")+"\n\n下一步：\n· 让一名有心灵链接、智识≥6 的殖民者研读它。读完一遍要 "+Netheril.KarsusPassHours+" 小时，每遍得一层“卡苏斯的回响”。\n· 有人读满 "+Netheril.AscensionPasses+" 遍，并且心灵链接 6 级（装了 VPE 时灵能等级≥20），就能在耐瑟祭坛发起升神仪式。\n· 耐瑟祭坛需要研究“耐瑟瑞尔奥术”，用金 100、秘银 50、玻璃钢 50 建造。\n\n卷轴不能交易。如果它被毁或被偷走，30～60 天后会传来它的下落。",LetterDefOf.PositiveEvent,new LookTargets(t));
  }
  public static void OnKarsusLost(string why){
   var st=State;if(st==null)return;st.karsus=null;
   if(st.karsusStatus=="larloch"){return;} // 古墓的地图被收起时卷轴跟着没了：下次进古墓重新放一卷
   st.karsusStatus="lost";st.reappearTick=Now+Rand.RangeInclusive(ReappearMin,ReappearMax)*GenDate.TicksPerDay;
   if(st.stage=="scroll")st.stage="lostScroll";
   Letter("卡苏斯之卷遗失","卡苏斯之卷"+why+"。\n\n它不会就此消失。"+ReappearMin+"～"+ReappearMax+"天后会传来它的下落。各人读过的遍数都还记着。",LetterDefOf.NegativeEvent);
  }
  // 每 250 刻：卷轴还在不在、在谁手里。
  public static void TrackKarsus(NetherilQuestState st){
   var k=st.karsus;
   if(k==null)return;
   if(k.Destroyed){OnKarsusLost(st.karsusStatus=="site"?"随浮城残骸一起沉没了":"被毁了");return;}
   if(st.karsusStatus=="player"){if(!Reachable(k))OnKarsusLost("离开了你所有的地图，被带走了");return;}
   if((st.karsusStatus=="larloch" || st.karsusStatus=="site") && PlayerHolds(k)){var how=st.karsusStatus=="larloch"?"从拉洛克的古墓里夺来":"从浮城残骸里找回";if(st.karsusStatus=="site")DestroySite(ref st.wreckSite);OnObtained(k,how);}
  }

  // ---------- 使者 ----------
  public static string ScheduleBlocked(NetherilQuestState st){
   if(!LichMod.Settings.netherilQuest)return "耐瑟瑞尔任务线已在设置里关闭";
   if(st.stage!="idle")return "使者已经来过";
   if(!ResearchDone)return "还没有完成研究“耐瑟瑞尔奥术”";
   if(st.readHours<ReadHoursNeed)return "殖民者累计读耐瑟卷轴 "+st.readHours.ToString("0.#")+" / "+ReadHoursNeed+" 小时";
   if(LarlochDef==null)return "没有拉洛克派系";
   return null;
  }
  public static bool DailyCheck(NetherilQuestState st){
   if(ScheduleBlocked(st)!=null)return false;
   st.stage="scheduled";st.envoyTick=Now+Rand.RangeInclusive(EnvoyDaysMin,EnvoyDaysMax)*GenDate.TicksPerDay;
   return true;
  }
  public static Pawn Master{get{
   var st=State;if(st==null)return null;
   if(st.larlochMaster!=null && !st.larlochMaster.Dead && !st.larlochMaster.Destroyed)return st.larlochMaster;
   var f=Larloch;if(f==null)return null;
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_LarlochDemilich"),f,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
   MakeDemilich(p,false);AdventurerEnemies.Story(p,14);Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);
   return st.larlochMaster=p;
  }}
  // 半巫妖造型：灰白皮肤、光头、暗紫长袍；带死灵躯体状态（不痛、不饿、不困，会用恐惧凝视和死灵冲击）。
  public static readonly Color DemilichSkin=new Color(.66f,.70f,.66f),DemilichRobe=new Color(.20f,.16f,.28f);
  public static void MakeDemilich(Pawn p,bool avatar){
   if(p?.story!=null){p.story.skinColorOverride=DemilichSkin;var bald=DefDatabase<HairDef>.GetNamedSilentFail("Bald");if(bald!=null)p.story.hairDef=bald;}
   FactionGear.Wear(p,"Apparel_Robe",ThingDefOf.Cloth);
   var robe=p.apparel?.WornApparel.FirstOrDefault(a=>a.def.defName=="Apparel_Robe");robe?.SetColor(DemilichRobe,false);
   Items.Give(p,avatar?"LP_LarlochAvatar":"LP_NetherilUndead");
   if(avatar)p.Name=new NameSingle("拉洛克");
   p.needs?.AddOrRemoveNeedsAsAppropriate();p.Drawer?.renderer?.SetAllGraphicsDirty();
  }
  public static Pawn Skeleton(Pawn master,Faction f){var g=FactionConstructs.MakeBound("Skeleton",master,f);g.Name=new NameSingle("拉洛克的骷髅");return g;}
  public static Lord SendEnvoy(Map map,bool reappear){
   var st=State;var f=Larloch;if(st==null || f==null || map==null)return null;
   if(f.HostileTo(Faction.OfPlayer) && !st.larlochDefeated)Factions.SetRelation(f,Faction.OfPlayer,0);
   if(f.HostileTo(Faction.OfPlayer))return null;
   if(!Events2.EdgeCell(map,out var entry))return null;
   var envoy=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_LarlochEnvoy"),f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
   MakeDemilich(envoy,false);AdventurerEnemies.Story(envoy,12); // 1.48.1 剧情敌人等级
   var list=new List<Pawn>{envoy,Skeleton(envoy,f),Skeleton(envoy,f)};
   foreach(var p in list)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,4),map);
   var stand=CellFinder.RandomClosewalkCellNear(entry,map,8);
   var lord=LordMaker.MakeNewLord(f,new LordJob_LarlochEnvoy(stand,Now+EnvoyStayTicks),map,list);
   st.envoy=envoy;st.envoyLord=lord;st.stage=reappear?st.stage:"envoy";st.reappearMode=reappear;st.envoyPending=true;st.envoyStand=stand;
   var l=(Letter_LarlochEnvoy)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_LarlochChoiceLetter"));
   l.Label="拉洛克的邀约";l.lookTargets=new LookTargets(envoy);l.relatedFaction=f;l.envoy=envoy;
   l.Text=reappear
    ?"影巫王拉洛克的使者又来了。这一次他开门见山：卡苏斯之卷又落回了拉洛克手里，他愿意再给你一次机会。\n\n以人换卷：交出一名心灵链接≥"+ASendPsylink+"（装了 VPE 时灵能等级≥"+ASendVPE+"）的殖民者，他会随使者永远离开（不算死亡），你立刻得到卷轴。\n以命匣换卷：交出一名巫妖的命匣。巫妖保留能力和等级，但从此死了不再复活。\n去古墓硬抢：拉洛克的古墓会显露在世界地图上，卷轴就放在中央大厅里。\n\n使者在殖民地边上等一天。不回复就当作你要去古墓硬抢。"
    :"一个半巫妖带着两名骷髅护卫停在殖民地边上。他自称影巫王拉洛克的使者：主人察觉到你们在读耐瑟瑞尔的卷轴，愿意把真正的宝贝——卡苏斯之卷——交给配得上的人。\n\n以人换卷：交出一名心灵链接≥"+ASendPsylink+"（装了 VPE 时灵能等级≥"+ASendVPE+"）的殖民者，他会随使者永远离开（不算死亡，全体心情 -5 持续 10 天），你立刻得到卷轴。\n以命匣换卷：交出一名巫妖的命匣。命匣会被带走；巫妖保留能力和等级，但从此死了不再复活，也不能再向命匣献祭。\n为他办三件事：献上位面遗物、斩杀一名红袍法师、守护一次命匣转移，办完就把卷轴送来。\n拒绝：使者 60 天后再来，最多再来 2 次。\n\n也可以去古墓硬抢——第三次拒绝以后，拉洛克的古墓就会显露在世界地图上。\n\n使者在殖民地边上等一天。不回复就当作拒绝。";
   Find.LetterStack.ReceiveLetter(l);
   return lord;
  }
  public static IEnumerable<Pawn> OfferablePawns(Map map)=>map==null?Enumerable.Empty<Pawn>():map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Dead && (p.GetPsylinkLevel()>=ASendPsylink || (NetherilHooks.VPEActive && NetherilHooks.Level(p)>=ASendVPE)));
  public static IEnumerable<Pawn> OfferableLiches(Map map)=>map==null?Enumerable.Empty<Pawn>():map.mapPawns.FreeColonistsSpawned.Where(p=>{var h=Necro.Lich(p);return h!=null && h.box!=null && !h.box.Destroyed && h.box.soul==p && !PhylacteryLost(p);});
  public static string ABlocked(Map map)=>OfferablePawns(map).Any()?null:"没有心灵链接≥"+ASendPsylink+(NetherilHooks.VPEActive?"或灵能等级≥"+ASendVPE:"")+"的殖民者";
  public static string BBlocked(Map map)=>OfferableLiches(map).Any()?null:"殖民地里没有带着命匣的巫妖";
  static Map EnvoyMap(NetherilQuestState st)=>st.envoy?.MapHeld??Home;
  static void EnvoyLeaves(NetherilQuestState st){st.envoyPending=false;if(st.envoyLord?.LordJob is LordJob_LarlochEnvoy j)j.leaveNow=true;}
  static IntVec3 EnvoyCell(NetherilQuestState st,Map map)=>st.envoy!=null && st.envoy.Spawned && st.envoy.Map==map?st.envoy.Position:(st.envoyStand.IsValid && st.envoyStand.InBounds(map)?st.envoyStand:map.Center);
  // A：以人换卷。
  public static bool ChooseA(Pawn p){
   var st=State;if(st==null || p==null || !OfferablePawns(p.MapHeld).Contains(p))return false;
   var map=p.Map;var f=Larloch;var cell=EnvoyCell(st,map);
   p.jobs?.StopAll();if(p.drafter!=null)p.drafter.Drafted=false;
   p.SetFaction(f);
   if(st.envoyLord!=null && st.envoyLord.ownedPawns.Count>0)st.envoyLord.AddPawn(p);else LordMaker.MakeNewLord(f,new LordJob_ExitMapBest(LocomotionUrgency.Walk,false),map,new List<Pawn>{p});
   foreach(var c in PawnsFinder.AllMaps_FreeColonists)c.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("LP_CompanionSentAway"));
   EnvoyLeaves(st);st.deal="以人换卷";
   GiveKarsus(map,cell,"以"+p.LabelShort+"换来");
   return true;
  }
  // B：以命匣换卷。命匣交到拉洛克手里（存在任务状态里，不销毁：巫妖的等级和能力照旧），死亡后不再复活。
  public static bool ChooseB(Pawn lich){
   var st=State;if(st==null || lich==null || !OfferableLiches(lich.MapHeld).Contains(lich))return false;
   var map=lich.Map;var box=Necro.Lich(lich).box;var cell=EnvoyCell(st,map);
   if(box.Spawned)box.DeSpawn(DestroyMode.Vanish);else box.holdingOwner?.Remove(box);
   st.hold.TryAdd(box,false);
   Items.Give(lich,"LP_PhylacteryLost");
   foreach(var altar in map.listerThings.ThingsOfDef(Necro.Def<ThingDef>("Altar")).OfType<Building_Altar>())if(altar.selected==box)altar.selected=null;
   EnvoyLeaves(st);st.deal="以命匣换卷";
   GiveKarsus(map,cell,"以"+lich.LabelShort+"的命匣换来");
   return true;
  }
  // C：三件差事。
  public static bool ChooseC(){
   var st=State;if(st==null)return false;var map=EnvoyMap(st);
   EnvoyLeaves(st);st.stage="errand";StartErrand(st,1,map);return true;
  }
  // D：拒绝（不回复、使者离开也算）。第三次拒绝后古墓显露。
  public static void Refuse(string why="你拒绝了拉洛克的邀约"){
   var st=State;if(st==null)return;
   EnvoyLeaves(st);st.refusals++;st.errand=0;
   if(st.refusals>=MaxRefusals){st.stage="crypt";CreateCrypt(st,"第"+st.refusals+"次拒绝以后");return;}
   st.stage="refused";st.envoyTick=Now+RefuseDays*GenDate.TicksPerDay;
   Letter("拉洛克的耐心",why+"（已拒绝 "+st.refusals+" / "+MaxRefusals+" 次）。\n\n使者 "+RefuseDays+" 天后会再来。第"+MaxRefusals+"次拒绝以后，拉洛克的古墓会显露在世界地图上，可以去那里硬抢卡苏斯之卷。",LetterDefOf.NeutralEvent);
  }
  // 重现时的使者：只能 A/B/去古墓。
  public static void ChooseCrypt(){var st=State;if(st==null)return;EnvoyLeaves(st);st.reappearMode=false;st.stage="crypt";CreateCrypt(st,"你决定去古墓硬抢");}
  // 1.61.0 热修补救：选项 A/B 以前点了没反应、使者等到走了（记成一次拒绝），这里把那次拒绝退回并让使者立刻再来。成功返回 null。
  public static string RecallEnvoy(){
   var st=State;if(st==null)return "没有任务状态";
   if(st.envoyPending)return "使者还在殖民地边上，直接打开“拉洛克的邀约”信件";
   if(st.stage!="refused" && st.stage!="scheduled")return "当前阶段不需要使者（"+st.stage+"）";
   var map=Home;if(map==null)return "没有殖民地地图";
   int refusals=st.refusals;if(st.stage=="refused" && st.refusals>0)st.refusals--;
   if(SendEnvoy(map,false)==null){st.refusals=refusals;return "使者来不了（拉洛克派系敌对，或找不到地图边缘）";}
   return null;
  }
  public static void OnEnvoyGone(NetherilQuestState st){
   if(!st.envoyPending)return;st.envoyPending=false;
   if(st.reappearMode){ChooseCrypt();return;}
   Refuse(st.envoy==null || st.envoy.Dead?"使者倒在了殖民地边上，没人给拉洛克答复":"使者等了一天没等到答复，走了");
  }

  // ---------- 三件差事 ----------
  public static void StartErrand(NetherilQuestState st,int n,Map map){
   st.errand=n;st.errandDeadline=Now+ErrandDays*GenDate.TicksPerDay;map=map??Home;
   switch(n){
    case 1:{
     var box=ThingMaker.MakeThing(Items.Thing("LP_LarlochOfferingBox"));box.SetFaction(Faction.OfPlayer);
     var cell=map==null?IntVec3.Invalid:EnvoyCell(st,map);
     if(map!=null){if(!CellFinder.TryFindRandomCellNear(cell,map,10,c=>c.Standable(map) && c.GetEdifice(map)==null && c.GetFirstItem(map)==null && !c.Fogged(map),out var spot))spot=CellFinder.RandomClosewalkCellNear(map.Center,map,10);GenSpawn.Spawn(box,spot,map);st.offeringBox=box;}
     Letter("差事一：献上位面遗物","拉洛克要的第一件东西："+ResidueNeed+" 份位面残渣和 "+ShardNeed+" 块神迹残片（传送门失控、黄昏之年的产物）。\n\n使者在地图边上留下了“拉洛克的祭箱”，只收这两种东西。把它们搬进祭箱，够数后祭箱和东西一起消失。\n\n期限 "+ErrandDays+" 天。超时就算拒绝。完成后会得到一卷死灵学派的秘术残卷。",LetterDefOf.NeutralEvent,new LookTargets(box));
     break;}
    case 2:{
     var thay=Thay;
     if(thay==null){Letter("差事二：斩杀红袍","拉洛克本想让你去杀一名塞伊精英法师，可世上已经没有塞伊远征军了。这件差事就算办完了。",LetterDefOf.NeutralEvent);CompleteErrand(2);return;}
     var site=MakeSite("LP_RedWizardCamp",thay,4,10,CampPoints);st.campSite=site;
     Letter("差事二：斩杀红袍","拉洛克要你去杀一名塞伊的精英红袍法师。他就在世界地图上的“红袍法师营地”（离殖民地 4～10 格），身边有塞伊守军。\n\n组队过去，杀掉那名精英法师就算完成（营地里其他人不用管）。期限 "+ErrandDays+" 天。完成后会得到一卷优秀的研习卷、"+SilverReward+" 银和一块传奇重铸之石。",LetterDefOf.NeutralEvent,site!=null?new LookTargets(site):null);
     break;}
    case 3:{
     if(map==null)return;
     var f=Larloch;var casket=ThingMaker.MakeThing(Items.Thing("LP_PhylacteryCasket"));casket.SetFaction(f);
     IntVec3 spot;if(!CellFinder.TryFindRandomCellNear(map.Center,map,14,c=>CellRect.CenteredOn(c,1).Cells.All(x=>x.InBounds(map) && x.Standable(map) && x.GetEdifice(map)==null && x.GetFirstPawn(map)==null) && !c.Fogged(map) && map.reachability.CanReachColony(c),out spot))spot=CellFinder.RandomClosewalkCellNear(map.Center,map,10);
     GenSpawn.Spawn(casket,spot,map);
     var guards=new List<Pawn>();var master=Master;for(int i=0;i<3;i++){var g=Skeleton(master,f);GenSpawn.Spawn(g,CellFinder.RandomClosewalkCellNear(spot,map,3),map);guards.Add(g);}
     LordMaker.MakeNewLord(f,new LordJob_DefendPoint(spot,3f,8f,false,false),map,guards);
     st.casket=casket;st.casketGuards=guards;st.casketRaidTick=Now+CasketRaidTicks;st.casketDoneTick=Now+CasketStayTicks;st.casketRaidSent=false;
     Letter("差事三：守护转移","拉洛克的骷髅护送着一具“命匣匣柩”来到殖民地，要在这里停一天。\n\n塞伊得到了风声：大约 6 小时后会有一支塞伊部队直奔匣柩而来。守住它——一天结束时匣柩还在就算完成。匣柩被毁就算失败（等同拒绝）。\n\n完成后拉洛克会把卡苏斯之卷交给你，拉洛克的好感 +"+ErrandGoodwill+"。",LetterDefOf.ThreatSmall,new LookTargets(casket));
     break;}
   }
  }
  public static void CompleteErrand(int n){
   var st=State;if(st==null || st.stage!="errand" || st.errand!=n)return;var map=Home;
   Adventurer.MilestoneColony("larloch"+n,Adventurer.LarlochXP); // 1.48.0 冒险者里程碑：每件差事全体殖民者各一次
   switch(n){
    case 1:
     if(map!=null)Messenger(map,new List<Thing>{Netheril.MakeArcane("Necromancy",QualityCategory.Good),Growth.MakeLegacy()}.Where(t=>t!=null).ToList(),"拉洛克收下了位面遗物，骷髅信使送来了一卷死灵学派的秘术残卷和一件沉睡的传承武器。"); // 1.58.3 传承武器
     StartErrand(st,2,map);break;
    case 2:
     DestroySite(ref st.campSite);st.campLeader=null;
     if(map!=null){var silver=ThingMaker.MakeThing(ThingDefOf.Silver);silver.stackCount=SilverReward;var gifts=new List<Thing>{Netheril.MakeStudy(QualityCategory.Excellent),silver};var stone=Legend.MakeStone();if(stone!=null)gifts.Add(stone); // 1.50.0 传奇重铸之石
      var tome=Growth.MakeTome();if(tome!=null)gifts.Add(tome); // 1.58.3 属性典籍
      Messenger(map,gifts,"红袍法师死了。骷髅信使送来了一卷优秀的研习卷、"+SilverReward+" 银"+(stone!=null?"、一块传奇重铸之石":"")+(tome!=null?"和一本"+tome.LabelNoCount:"")+"。");}
     StartErrand(st,3,map);break;
    case 3:
     var cell=st.casket!=null && st.casket.Spawned?st.casket.Position:(map?.Center??IntVec3.Invalid);
     CasketLeaves(st);
     Factions.ChangeGoodwillExact(Larloch,ErrandGoodwill,"守护命匣转移");
     st.errand=0;
     if(map!=null)GiveKarsus(map,cell,"三件差事办完，拉洛克兑现了承诺");
     break;
   }
  }
  static void CasketLeaves(NetherilQuestState st){
   if(st.casket!=null && !st.casket.Destroyed){if(st.casket.Spawned)FleckMaker.ThrowDustPuffThick(st.casket.DrawPos,st.casket.Map,2f,Color.gray);st.casket.Destroy(DestroyMode.Vanish);}
   foreach(var g in st.casketGuards)if(g!=null && !g.Dead && !g.Destroyed && g.Spawned){var lord=g.GetLord();lord?.RemovePawn(g);}
   var alive=st.casketGuards.Where(g=>g!=null && !g.Dead && !g.Destroyed && g.Spawned).ToList();
   if(alive.Count>0)LordMaker.MakeNewLord(Larloch,new LordJob_ExitMapBest(LocomotionUrgency.Walk,false),alive[0].Map,alive);
   st.casket=null;st.casketGuards.Clear();
  }
  public static void ErrandFailed(string why){
   var st=State;if(st==null || st.stage!="errand")return;
   if(st.offeringBox!=null && !st.offeringBox.Destroyed)st.offeringBox.Destroy(DestroyMode.Vanish);st.offeringBox=null;
   DestroySite(ref st.campSite);st.campLeader=null;CasketLeaves(st);
   Refuse(why);
  }
  public static void ErrandTick(NetherilQuestState st){
   if(Now>st.errandDeadline){ErrandFailed("差事"+st.errand+"超过 "+ErrandDays+" 天没有办完，拉洛克当作你拒绝了");return;}
   if(st.errand==2 && st.campSite!=null && st.campSite.Destroyed && (st.campLeader==null || !st.campLeader.Dead)){st.campSite=null;var thay=Thay;if(thay!=null)st.campSite=MakeSite("LP_RedWizardCamp",thay,4,10,CampPoints);}
   if(st.errand==3 && st.casket!=null){
    if(st.casket.Destroyed){st.casket=null;ErrandFailed("命匣匣柩被毁了，差事三失败");return;}
    if(!st.casketRaidSent && Now>=st.casketRaidTick){st.casketRaidSent=true;CasketRaid(st);}
    if(Now>=st.casketDoneTick)CompleteErrand(3);
   }
  }
  public static Lord CasketRaid(NetherilQuestState st){
   var thay=Thay;var casket=st.casket;var map=casket?.Map;if(thay==null || map==null)return null;
   float points=Mathf.Max(StorytellerUtility.DefaultThreatPointsNow(map)*CasketRaidPoints,thay.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)*1.05f);
   var pawns=PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,tile=map.Tile,faction=thay,points=points},false).ToList();
   if(pawns.Count==0 || !Events2.EdgeCell(map,out var entry,casket.Position)){foreach(var p in pawns)p.Destroy();return null;}
   foreach(var p in pawns)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,6),map);
   var lord=LordMaker.MakeNewLord(thay,new LordJob_AssaultThings(thay,new List<Thing>{casket},1f,false),map,pawns);
   Find.LetterStack.ReceiveLetter("塞伊来抢匣柩",thay.Name+"的部队冲着命匣匣柩来了！守住它，直到拉洛克的骷髅带它离开。",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),thay);
   return lord;
  }
  // 骷髅信使：从地图边上走过来，放下奖励，再走掉。
  public static void Messenger(Map map,List<Thing> reward,string text){
   reward.RemoveAll(t=>t==null);var f=Larloch;
   IntVec3 cell=map.Center;if(Events2.EdgeCell(map,out var entry))cell=CellFinder.RandomClosewalkCellNear(entry,map,6);
   if(!CellFinder.TryFindRandomCellNear(map.Center,map,12,c=>c.Standable(map) && !c.Fogged(map) && c.GetFirstItem(map)==null && map.reachability.CanReachColony(c),out var spot))spot=cell;
   foreach(var t in reward){GenPlace.TryPlaceThing(t,spot,map,ThingPlaceMode.Near);t.SetForbidden(false,false);}
   if(f!=null && Master!=null){var s=Skeleton(Master,f);GenSpawn.Spawn(s,CellFinder.RandomClosewalkCellNear(spot,map,2),map);LordMaker.MakeNewLord(f,new LordJob_ExitMapBest(LocomotionUrgency.Walk,false),map,new List<Pawn>{s});}
   Find.LetterStack.ReceiveLetter("骷髅信使",text+"\n\n奖励："+string.Join("、",reward.Select(t=>t.LabelCap)),LetterDefOf.PositiveEvent,new LookTargets(reward),Larloch);
  }

  // ---------- 世界地图站点 ----------
  public static Site MakeSite(string part,Faction f,int min,int max,float pointsFactor){
   var home=Home;var def=DefDatabase<SitePartDef>.GetNamedSilentFail(part);if(home==null || def==null || f==null)return null;
   if(!TileFinder.TryFindNewSiteTile(out PlanetTile tile,home.Tile,min,max))return null;
   float points=Mathf.Max(300f,StorytellerUtility.DefaultThreatPointsNow(home)*pointsFactor);
   var site=SiteMaker.MakeSite(def,tile,f,true,points);
   if(site==null)return null;
   site.GetComponent<TimeoutComp>()?.StopTimeout();
   Find.WorldObjects.Add(site);
   return site;
  }
  // 地图还开着（玩家在里面）就留给原版收起；否则直接从世界地图上拿掉。
  public static void DestroySite(ref Site site){if(site!=null && !site.Destroyed && !site.HasMap)site.Destroy();site=null;}
  public static void CreateCrypt(NetherilQuestState st,string why){
   if(st.cryptSite!=null && !st.cryptSite.Destroyed)return;
   st.cryptSite=MakeSite("LP_LarlochCrypt",Larloch,6,15,CryptPoints);
   if(st.karsusStatus=="none" || st.karsusStatus=="lost")st.karsusStatus="larloch";
   Letter("拉洛克的古墓",why+"，拉洛克的古墓显露在世界地图上（离殖民地 6～15 格）。\n\n古墓是石砌的墓室：外墙、几间墓室和中央大厅，守着骷髅、血肉巨像和半巫妖，最深处是拉洛克本人的化身——一名巫妖，会恐惧凝视、死灵冲击和生命汲取光环。卡苏斯之卷放在中央大厅的架子上。\n\n踏进古墓，拉洛克的扈从就会与你为敌。古墓会一直在那里，直到你攻下它。",LetterDefOf.ThreatBig,st.cryptSite!=null?new LookTargets(st.cryptSite):null);
  }
  public static void OnLarlochDefeated(Pawn p){
   var st=State;if(st==null || st.larlochDefeated)return;
   st.larlochDefeated=true;var f=Larloch;if(f!=null)Factions.SetRelation(f,Faction.OfPlayer,-100);
   st.nextUndeadRaid=Now+Rand.RangeInclusive(RaidMin,RaidMax)*GenDate.TicksPerDay;
   Letter("影巫王的化身","拉洛克倒下了——尸体在眨眼间化成了灰。“这只是影巫王的化身。”空气里留下这么一句话。\n\n他不会复活，但拉洛克的扈从从此与你为敌："+(LichMod.Settings.larlochRaids?"每 "+RaidMin+"～"+RaidMax+" 天会有一小队亡灵来袭（设置里可以关掉）。":"（亡灵袭击已在设置里关闭）")+"\n\n卡苏斯之卷还在中央大厅的架子上。",LetterDefOf.PositiveEvent,new LookTargets(p.Corpse??(Thing)p));
  }
  public static void UndeadRaid(Map map){
   var f=Larloch;if(f==null || map==null)return;if(!f.HostileTo(Faction.OfPlayer))Factions.SetRelation(f,Faction.OfPlayer,-100);
   float points=Mathf.Max(200f,StorytellerUtility.DefaultThreatPointsNow(map)*UndeadRaidPoints);
   var pawns=PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,tile=map.Tile,faction=f,points=points},false).ToList();
   if(pawns.Count==0 || !Events2.EdgeCell(map,out var entry)){foreach(var p in pawns)p.Destroy();return;}
   foreach(var p in pawns)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,6),map);
   LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,true,false,false,false,false,false),map,pawns);
   Find.LetterStack.ReceiveLetter("拉洛克的亡灵","影巫王拉洛克没有忘记你：一小队亡灵从地图边上冒了出来。",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f);
  }
  // 重现：拉洛克被打过 → 塞伊占着的浮城残骸；没被打过 → 使者再来（只有 A/B/去古墓）。
  public static void Reappear(NetherilQuestState st){
   st.reappearTick=-1;
   if(!st.larlochDefeated){
    var map=Home;st.karsusStatus="larloch";
    if(map==null || SendEnvoy(map,true)==null){st.reappearTick=Now+GenDate.TicksPerDay;st.karsusStatus="lost";}
    return;
   }
   var thay=Thay??Find.FactionManager.RandomEnemyFaction(false,false,true);
   var site=MakeSite("LP_NetherilWreck",thay,4,12,WreckPoints);
   if(site==null){st.reappearTick=Now+GenDate.TicksPerDay;return;}
   st.wreckSite=site;st.wreckDeadline=Now+WreckDays*GenDate.TicksPerDay;st.karsusStatus="site";
   Letter("卡苏斯之卷的下落","有消息说，卡苏斯之卷落在一座“沉没的耐瑟浮城残骸”里，"+(thay?.Name??"敌人")+"正占着那里。\n\n残骸在世界地图上（离殖民地 4～12 格）。"+WreckDays+" 天内不去，残骸就会再次沉没；30～60 天后再传来消息。",LetterDefOf.NeutralEvent,new LookTargets(site));
  }

  // ---------- 状态推进 ----------
  public static void Tick(NetherilQuestState st){
   TrackKarsus(st);
   int now=Now;var s=LichMod.Settings;
   if(now>=st.nextDailyCheck){st.nextDailyCheck=now+GenDate.TicksPerDay;if(st.stage=="idle")DailyCheck(st);}
   if(st.envoyPending && (st.envoy==null || st.envoy.Dead || !st.envoy.Spawned || st.envoy.Faction!=Larloch || st.envoyLord==null || !st.envoyLord.ownedPawns.Contains(st.envoy)))OnEnvoyGone(st);
   if(s.netherilQuest && (st.stage=="scheduled" || st.stage=="refused") && st.envoyTick>=0 && now>=st.envoyTick && !st.envoyPending){var map=Home;if(map==null || SendEnvoy(map,false)==null)st.envoyTick=now+GenDate.TicksPerDay;}
   if(st.stage=="errand")ErrandTick(st);
   if(st.cryptSite!=null && st.cryptSite.Destroyed){st.cryptSite=null;if(!st.larlochDefeated || st.karsusStatus=="larloch")CreateCryptQuiet(st);}
   if(st.karsusStatus=="lost" && st.reappearTick>=0 && now>=st.reappearTick && !st.envoyPending)Reappear(st);
   if(st.karsusStatus=="site" && st.wreckSite!=null && now>=st.wreckDeadline && !st.wreckSite.HasMap){DestroySite(ref st.wreckSite);st.karsus?.Destroy();st.karsus=null;st.karsusStatus="lost";st.reappearTick=now+Rand.RangeInclusive(ReappearMin,ReappearMax)*GenDate.TicksPerDay;Letter("浮城残骸沉没了","沉没的耐瑟浮城残骸又沉回了地底。卡苏斯之卷的下落要等 "+ReappearMin+"～"+ReappearMax+" 天后才会再有消息。",LetterDefOf.NeutralEvent);}
   if(st.karsusStatus=="site" && st.wreckSite!=null && st.wreckSite.Destroyed && st.karsus==null){st.wreckSite=null;st.karsusStatus="lost";st.reappearTick=now+Rand.RangeInclusive(ReappearMin,ReappearMax)*GenDate.TicksPerDay;}
   if(st.larlochDefeated && s.larlochRaids && st.nextUndeadRaid>=0 && now>=st.nextUndeadRaid){st.nextUndeadRaid=now+Rand.RangeInclusive(RaidMin,RaidMax)*GenDate.TicksPerDay;UndeadRaid(Home);}
  }
  static void CreateCryptQuiet(NetherilQuestState st){if(st.stage!="crypt" && st.karsusStatus!="larloch")return;if(st.larlochDefeated && st.karsusStatus!="larloch")return;st.cryptSite=MakeSite("LP_LarlochCrypt",Larloch,6,15,CryptPoints);}

  // ---------- 死亡钩子：红袍掉卷轴、营地精英、拉洛克 ----------
  public const float RedWizardStudy=.05f,RedWizardArcane=.02f;
  public static void OnPawnKilled(Pawn p){
   var st=State;if(st==null || p==null || !p.Dead)return;
   if(st.campLeader==p)CompleteErrand(2);
   if(Items.Has(p,"LP_LarlochAvatar"))OnLarlochDefeated(p);
   if(p.kindDef?.defName=="LP_FH_RedWizard" && p.Faction!=null && p.Faction!=Faction.OfPlayer && p.MapHeld!=null){
    var map=p.MapHeld;var cell=p.PositionHeld;
    if(Rand.Chance(RedWizardStudy)){var t=Netheril.MakeStudy();if(t!=null)GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);}
    if(Rand.Chance(RedWizardArcane)){var t=Netheril.MakeArcane();if(t!=null)GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);}
   }
  }
 }

 public class NetherilQuestState : GameComponent,IThingHolder {
  // 阅读
  // 1.52.0 补丁取消了每天 40 的经验上限；旧档里的 lpXpToday / lpXpDay / lpCappedToday 不再读写（读档时忽略）。
  public Dictionary<int,float> studyPoints=new Dictionary<int,float>();
  public float readHours;
  // 1.52.0 补丁：卡苏斯之卷这一遍读了多少刻（旧档的 lpKarsusHours 读进来 ×2500）；readClock = 每人累计阅读刻数里不满 1 小时的零头（读错判定用）。
  public Dictionary<int,int> karsusTicks=new Dictionary<int,int>(),karsusPasses=new Dictionary<int,int>(),readClock=new Dictionary<int,int>();
  // 卡苏斯之卷
  public Thing karsus;public string karsusStatus="none";public bool deciphered;public int reappearTick=-1;
  // 任务线
  public string stage="idle",deal;public int envoyTick=-1,nextDailyCheck,refusals,errand,errandDeadline=-1;
  public bool envoyPending,reappearMode;public int intelEnvoyId=-1;public Pawn envoy;public Lord envoyLord;public IntVec3 envoyStand=IntVec3.Invalid;
  public Thing offeringBox;public Site campSite,cryptSite,wreckSite;public Pawn campLeader;public int wreckDeadline=-1;
  public Thing casket;public List<Pawn> casketGuards=new List<Pawn>();public int casketRaidTick=-1,casketDoneTick=-1;public bool casketRaidSent;
  public bool larlochDefeated;public int nextUndeadRaid=-1;public Pawn larlochMaster;
  public ThingOwner<Thing> hold;
  // 升神
  public Pawn demigod;
  public NetherilQuestState(Game game){hold=new ThingOwner<Thing>(this,false,LookMode.Deep);}
  public IThingHolder ParentHolder=>null;
  public ThingOwner GetDirectlyHeldThings()=>hold;
  public void GetChildHolders(List<IThingHolder> outChildren){ThingOwnerUtility.AppendThingHoldersFromThings(outChildren,hold);}
  public override void GameComponentTick(){if(Find.TickManager.TicksGame%250!=77)return;try{NetherilQuest.Tick(this);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 耐瑟瑞尔任务线出错："+e,73710001);}}
  public float StudyPoints(Pawn p){studyPoints.TryGetValue(p.thingIDNumber,out float v);return v;}
  public int AddStudyPoints(Pawn p,float amount){
   float before=StudyPoints(p),after=before+amount;studyPoints[p.thingIDNumber]=after;int gained=0;
   foreach(int t in Netheril.StudyThresholds)if(before<t && after>=t && p.GetPsylinkLevel()<Netheril.StudyMaxPsylink){p.ChangePsylinkLevel(1,false);gained++;}
   if(gained>0 && p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"的耐瑟研习度达到 "+after.ToString("0")+"：心灵链接升到 "+p.GetPsylinkLevel()+" 级。",p,MessageTypeDefOf.PositiveEvent,false);
   return gained;
  }
  public void AddReadTicks(Pawn p,int ticks){if(p?.Faction==Faction.OfPlayer)readHours+=Netheril.Hours(ticks);}
  // 累计阅读刻数，返回这次跨过了几个整小时。
  public int TickReadClock(Pawn p,int ticks){
   int id=p.thingIDNumber;readClock.TryGetValue(id,out int c);c+=ticks;int n=c/Netheril.HourTicks;readClock[id]=c-n*Netheril.HourTicks;return n;
  }
  public int Passes(Pawn p){if(p==null)return 0;karsusPasses.TryGetValue(p.thingIDNumber,out int v);return v;}
  public int KarsusTicks(Pawn p){if(p==null)return 0;karsusTicks.TryGetValue(p.thingIDNumber,out int v);return v;}
  public float KarsusHours(Pawn p)=>Netheril.Hours(KarsusTicks(p));
  public void AddKarsusTicks(Pawn p,int ticks){
   int id=p.thingIDNumber;int t=KarsusTicks(p)+ticks;
   while(t>=Netheril.KarsusPassTicks){t-=Netheril.KarsusPassTicks;karsusTicks[id]=t;CompletePass(p);}
   karsusTicks[id]=t;
  }
  void CompletePass(Pawn p){
   int id=p.thingIDNumber;int n=Passes(p)+1;karsusPasses[id]=n;
   var echo=Items.Give(p,"LP_KarsusEcho");if(echo!=null){echo.Severity=Mathf.Min(Netheril.MaxEcho,n);p.health.Notify_HediffChanged(echo);}
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"读完了第 "+n+" 遍卡苏斯之卷，卡苏斯的回响 "+Mathf.Min(Netheril.MaxEcho,n)+" 层。",p,MessageTypeDefOf.PositiveEvent,false);
   if((n==3 || n==5) && Netheril.Roll<.5f){var def=DefDatabase<MentalStateDef>.GetNamedSilentFail("LP_KarsusObsession");if(def!=null)p.mindState?.mentalStateHandler?.TryStartMentalState(def,"卡苏斯之卷",true,true);}
   if(n==Netheril.AscensionPasses && p.Faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("读满七遍",p.LabelShort+"已经把卡苏斯之卷读了 "+n+" 遍。\n\n"+NetherilAscension.ConditionsText(p),LetterDefOf.PositiveEvent,p);
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref studyPoints,"lpStudyPoints",LookMode.Value,LookMode.Value);
   Scribe_Values.Look(ref readHours,"lpReadHours");
   Scribe_Collections.Look(ref karsusTicks,"lpKarsusTicks",LookMode.Value,LookMode.Value);Scribe_Collections.Look(ref karsusPasses,"lpKarsusPasses",LookMode.Value,LookMode.Value);Scribe_Collections.Look(ref readClock,"lpReadClock",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.LoadingVars){Dictionary<int,int> oldHours=null;Scribe_Collections.Look(ref oldHours,"lpKarsusHours",LookMode.Value,LookMode.Value);if(oldHours!=null && oldHours.Count>0){if(karsusTicks==null)karsusTicks=new Dictionary<int,int>();foreach(var kv in oldHours)if(!karsusTicks.ContainsKey(kv.Key))karsusTicks[kv.Key]=kv.Value*Netheril.HourTicks;}}
   Scribe_References.Look(ref karsus,"lpKarsus");Scribe_Values.Look(ref karsusStatus,"lpKarsusStatus","none");Scribe_Values.Look(ref deciphered,"lpDeciphered");Scribe_Values.Look(ref reappearTick,"lpReappearTick",-1);
   Scribe_Values.Look(ref stage,"lpStage","idle");Scribe_Values.Look(ref deal,"lpDeal");Scribe_Values.Look(ref envoyTick,"lpEnvoyTick",-1);Scribe_Values.Look(ref nextDailyCheck,"lpNextDailyCheck");Scribe_Values.Look(ref refusals,"lpRefusals");Scribe_Values.Look(ref errand,"lpErrand");Scribe_Values.Look(ref errandDeadline,"lpErrandDeadline",-1);
   Scribe_Values.Look(ref envoyPending,"lpEnvoyPending");Scribe_Values.Look(ref intelEnvoyId,"lpIntelEnvoyId",-1);Scribe_Values.Look(ref reappearMode,"lpReappearMode");Scribe_References.Look(ref envoy,"lpEnvoy");Scribe_References.Look(ref envoyLord,"lpEnvoyLord");Scribe_Values.Look(ref envoyStand,"lpEnvoyStand",IntVec3.Invalid);
   Scribe_References.Look(ref offeringBox,"lpOfferingBox");Scribe_References.Look(ref campSite,"lpCampSite");Scribe_References.Look(ref cryptSite,"lpCryptSite");Scribe_References.Look(ref wreckSite,"lpWreckSite");Scribe_References.Look(ref campLeader,"lpCampLeader");Scribe_Values.Look(ref wreckDeadline,"lpWreckDeadline",-1);
   Scribe_References.Look(ref casket,"lpCasket");ScribePawns.Look(ref casketGuards,"lpCasketGuards");Scribe_Values.Look(ref casketRaidTick,"lpCasketRaidTick",-1);Scribe_Values.Look(ref casketDoneTick,"lpCasketDoneTick",-1);Scribe_Values.Look(ref casketRaidSent,"lpCasketRaidSent");
   Scribe_Values.Look(ref larlochDefeated,"lpLarlochDefeated");Scribe_Values.Look(ref nextUndeadRaid,"lpNextUndeadRaid",-1);Scribe_References.Look(ref larlochMaster,"lpLarlochMaster");
   Scribe_Deep.Look(ref hold,"lpLarlochHold",this);
   Scribe_References.Look(ref demigod,"lpDemigod");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    if(studyPoints==null)studyPoints=new Dictionary<int,float>();
    if(karsusTicks==null)karsusTicks=new Dictionary<int,int>();if(karsusPasses==null)karsusPasses=new Dictionary<int,int>();if(readClock==null)readClock=new Dictionary<int,int>();
    if(hold==null)hold=new ThingOwner<Thing>(this,false,LookMode.Deep);if(stage==null)stage="idle";if(karsusStatus==null)karsusStatus="none";
   }
  }
 }

 // 使者：走到殖民地边上，待一天（或者谈妥）就离开；被打就还手。
 public class LordJob_LarlochEnvoy : LordJob {
  public IntVec3 cell;public int leaveTick;public bool leaveNow;
  public LordJob_LarlochEnvoy(){}
  public LordJob_LarlochEnvoy(IntVec3 cell,int leaveTick){this.cell=cell;this.leaveTick=leaveTick;}
  bool Leave(TriggerSignal s)=>leaveNow || Find.TickManager.TicksGame>=leaveTick;
  public override StateGraph CreateGraph(){
   var g=new StateGraph();
   var travel=new LordToil_Travel(cell);g.StartingToil=travel;
   var wait=new LordToil_DefendPoint(cell,3f,null);g.AddToil(wait);
   var leave=new LordToil_ExitMap(LocomotionUrgency.Walk,false,true);g.AddToil(leave);
   var fight=new LordToil_AssaultColony(false,false);g.AddToil(fight);
   var arrive=new Transition(travel,wait);arrive.AddTrigger(new Trigger_Memo("TravelArrived"));arrive.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerHour*6));g.AddTransition(arrive);
   var go=new Transition(wait,leave);go.AddSource(travel);go.AddTrigger(new Trigger_Custom(Leave));g.AddTransition(go);
   var hostile=new Transition(travel,fight);hostile.AddSources(wait,leave);hostile.AddTrigger(new Trigger_BecamePlayerEnemy());g.AddTransition(hostile);
   return g;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref cell,"cell");Scribe_Values.Look(ref leaveTick,"leaveTick");Scribe_Values.Look(ref leaveNow,"leaveNow");}
 }

 public class Letter_LarlochEnvoy : LP_EventLetter {
  // 1.69.0 事件小人入口：这一次来访的使者（和骷髅护卫）还在等答复。
  public Pawn envoy;
  protected override bool Live{get{var st=Netheril.State;return st!=null && st.envoyPending && (envoy==null || envoy==st.envoy);}}
  protected override bool StateTracked=>true;
  public override IEnumerable<Thing> EventThings{get{var st=Netheril.State;if(st?.envoy==null)return Enumerable.Empty<Thing>();return (st.envoyLord?.ownedPawns??new List<Pawn>()).Prepend(st.envoy).Distinct().Cast<Thing>();}}
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref envoy,"lpEnvoy");}
  public override IEnumerable<DiaOption> Choices{get{
   var st=Netheril.State;
   if(Closed || st==null || !st.envoyPending){yield return Option_Close;yield break;}
   var map=st.envoy?.MapHeld??NetherilQuest.Home;
   // 1.61.0 热修：选人/选巫妖改成信件里的下一页（原来是关掉对话框再弹浮动菜单，玩家那边点了没反应）。
   var a=new DiaOption("以人换卷"){link=Pick("选一名殖民者随使者离开：",NetherilQuest.OfferablePawns(map),p=>p.LabelShort+"（心灵链接 "+p.GetPsylinkLevel()+(NetherilHooks.VPEActive?"，灵能等级 "+NetherilHooks.Level(p):"")+"）",NetherilQuest.ChooseA)};
   var why=NetherilQuest.ABlocked(map);if(why!=null)a.Disable(why);yield return a;
   var b=new DiaOption("以命匣换卷"){link=Pick("选一名巫妖，把他的命匣交给使者：",NetherilQuest.OfferableLiches(map),p=>p.LabelShort+"的命匣（等级 "+Necro.Lich(p).box.level+"）",NetherilQuest.ChooseB)};
   why=NetherilQuest.BBlocked(map);if(why!=null)b.Disable(why);yield return b;
   yield return LarlochIntel.Option(st,map,()=>Find.LetterStack.RemoveLetter(this)); // 1.63.0 购买情报（买完使者照常等答复，不算结束）
   if(st.reappearMode){yield return Choice("去古墓硬抢",NetherilQuest.ChooseCrypt);}
   else{yield return Choice("为他办三件事",()=>NetherilQuest.ChooseC());yield return Choice("拒绝",()=>NetherilQuest.Refuse());}
   yield return Option_Postpone;
  }}
  DiaOption Choice(string text,Action act)=>new DiaOption(text){action=()=>{act?.Invoke();Resolve();},resolveTree=true};
  public DiaNode Pick(string prompt,IEnumerable<Pawn> pawns,Func<Pawn,string> label,Func<Pawn,bool> choose){
   var node=new DiaNode(prompt);
   foreach(var p in pawns.ToList())node.options.Add(new DiaOption(label(p)){action=()=>{if(choose(p))Resolve();else Messages.Message("没能交换："+p.LabelShort+"已经不符合条件（不在使者所在的地图，或命匣已毁、已交出）。",MessageTypeDefOf.RejectInput,false);},resolveTree=true});
   node.options.Add(new DiaOption("返回"){action=OpenLetter,resolveTree=true});
   return node;
  }
 }
 public static class NetherilDebug {
  [LudeonTK.DebugAction("DND边缘大陆","拉洛克使者立即再来",allowedGameStates=LudeonTK.AllowedGameStates.Playing)]
  static void RecallEnvoy(){Messages.Message(NetherilQuest.RecallEnvoy()??"拉洛克的使者正在赶来",MessageTypeDefOf.NeutralEvent,false);}
 }

 // 拉洛克的祭箱：只收位面残渣和神迹残片，够数（20 + 3）后连箱带物消失。
 public class Building_LarlochOffering : Building_Storage {
  public static int Held(Building_Storage b,string def){var d=Items.Thing(def);return d==null?0:b.slotGroup?.HeldThings.Where(t=>t.def==d).Sum(t=>t.stackCount)??0;}
  public override void TickRare(){base.TickRare();TryComplete();}
  public bool TryComplete(){
   if(!Spawned || Held(this,"LP_PlanarResidue")<NetherilQuest.ResidueNeed || Held(this,"LP_MiracleShard")<NetherilQuest.ShardNeed)return false;
   foreach(var t in slotGroup.HeldThings.ToList())t.Destroy();
   FleckMaker.ThrowDustPuffThick(DrawPos,Map,2f,Color.gray);
   var st=Netheril.State;if(st!=null)st.offeringBox=null;
   Destroy(DestroyMode.Vanish);
   NetherilQuest.CompleteErrand(1);
   return true;
  }
  public override string GetInspectString(){var s=base.GetInspectString();var line="位面残渣 "+Held(this,"LP_PlanarResidue")+" / "+NetherilQuest.ResidueNeed+"，神迹残片 "+Held(this,"LP_MiracleShard")+" / "+NetherilQuest.ShardNeed;return s.NullOrEmpty()?line:s+"\n"+line;}
 }
 public class Building_PhylacteryCasket : Building {
  public override string GetInspectString(){var s=base.GetInspectString();var st=Netheril.State;string line=st==null || st.casketDoneTick<0?"":(st.casketRaidSent?"":"塞伊大约 "+Mathf.Max(0f,(st.casketRaidTick-Find.TickManager.TicksGame)/2500f).ToString("0.#")+" 小时后到。")+"还要守 "+Mathf.Max(0f,(st.casketDoneTick-Find.TickManager.TicksGame)/2500f).ToString("0.#")+" 小时。";return s.NullOrEmpty()?line:s+"\n"+line;}
 }

 // 半巫妖（使者、古墓守卫）和拉洛克的化身：不痛不饿不困；附近有敌人时自己用恐惧凝视、死灵冲击；化身另有生命汲取光环、部件生命 ×2。
 public class Hediff_NetherilUndead : HediffWithComps {
  public int fearReady,blastReady,drainTicks;
  public bool Avatar=>def.defName=="LP_LarlochAvatar";
  public const int FearCooldown=1800,BlastCooldown=900,Range=22;
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(pawn==null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Faction==Faction.OfPlayer || pawn.InMentalState)return;
   if(Avatar){drainTicks+=delta;if(drainTicks>=LichSpells.DrainInterval){drainTicks=0;Drain();}}
   if(!pawn.IsHashIntervalTick(250,delta))return;
   int now=Find.TickManager.TicksGame;var map=pawn.Map;
   var foes=map.mapPawns.AllPawnsSpawned.Where(p=>LichSpells.Enemy(pawn,p) && !p.Downed && p.Position.InHorDistOf(pawn.Position,Range) && GenSight.LineOfSight(pawn.Position,p.Position,map)).ToList();
   if(foes.Count==0)return;
   var target=foes.OrderByDescending(f=>foes.Count(o=>o.Position.InHorDistOf(f.Position,4f))).First();
   if(now>=fearReady && foes.Count>=2){fearReady=now+FearCooldown;foreach(var p in foes.Where(p=>p.Position.InHorDistOf(target.Position,4.9f)))LichSpells.Frighten(pawn,p);FleckMaker.Static(target.Position,map,FleckDefOf.PsycastAreaEffect,4.9f);Items.Mote(pawn,"恐惧凝视",new Color(.6f,.8f,.6f));return;}
   if(now>=blastReady){blastReady=now+BlastCooldown;float dmg=15f*(Avatar?2f:1.5f);foreach(var p in foes.Where(p=>p.Position.InHorDistOf(target.Position,3.9f)))p.TakeDamage(new DamageInfo(LichSpells.Necrotic,dmg,0f,-1f,pawn));FleckMaker.Static(target.Position,map,FleckDefOf.PsycastAreaEffect,3.9f);Items.Mote(pawn,"死灵冲击",new Color(.6f,.8f,.6f));}
  }
  void Drain(){
   var map=pawn.Map;float healed=0f;
   foreach(var t in map.mapPawns.AllPawnsSpawned.Where(p=>LichSpells.Enemy(pawn,p) && !p.Downed && NecromancyGenes.IsLivingTarget(p) && p.Position.InHorDistOf(pawn.Position,LichSpells.DrainRadius)).ToList()){healed+=t.TakeDamage(new DamageInfo(LichSpells.Necrotic,LichSpells.DrainDamage,0f,-1f,pawn)).totalDamageDealt;FleckMaker.Static(t.Position,map,FleckDefOf.PsycastAreaEffect,.6f);}
   foreach(var injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(i=>i.Severity).ToList()){if(healed<=0)break;float h=Math.Min(healed,injury.Severity);injury.Heal(h);healed-=h;}
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref fearReady,"lpFearReady");Scribe_Values.Look(ref blastReady,"lpBlastReady");Scribe_Values.Look(ref drainTicks,"lpDrainTicks");}
 }
 [HarmonyPatch(typeof(Pawn),"get_HealthScale")]
 public static class LarlochAvatarHealth {static void Postfix(Pawn __instance,ref float __result){if(Items.Has(__instance,Avatar.Def))__result*=2f;}static readonly DefRef<HediffDef> Avatar=new DefRef<HediffDef>("LP_LarlochAvatar");} // 1.58.6：静态 Def，不按名字查
 [HarmonyPatch(typeof(Pawn_NeedsTracker),"ShouldHaveNeed")]
 public static class NetherilNeeds {
  static void Postfix(Pawn ___pawn,NeedDef nd,ref bool __result){
   if(!__result || ___pawn?.health==null)return;
   if(nd==NeedDefOf.Food && (Items.Has(___pawn,"LP_NetherilUndead") || Items.Has(___pawn,"LP_LarlochAvatar") || Items.Has(___pawn,"LP_ArcaneDemigod")))__result=false;
   else if(nd==NeedDefOf.Rest && (Items.Has(___pawn,"LP_NetherilUndead") || Items.Has(___pawn,"LP_LarlochAvatar")))__result=false;
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class NetherilKillHook {static void Postfix(Pawn __instance){try{NetherilQuest.OnPawnKilled(__instance);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 耐瑟瑞尔死亡钩子出错："+e,73710002);}}}
 [HarmonyPatch(typeof(FactionDef),nameof(FactionDef.RaidCommonalityFromPoints))]
 public static class NetherilRaidWeight {static void Postfix(FactionDef __instance,ref float __result){if(__result>0f && __instance==Factions.HostileDef && NetherilQuest.ColonyHasScroll)__result*=NetherilQuest.ThayScrollWeight;}}
 // 失匣巫妖：命匣在拉洛克手里，不再复活。
 [HarmonyPatch(typeof(Phylactery),nameof(Phylactery.ProcessRevival))]
 public static class LostPhylacteryNoRevival {static bool Prefix(Phylactery __instance)=>!NetherilQuest.PhylacteryLost(__instance.soul);}
 [HarmonyPatch(typeof(Phylactery),nameof(Phylactery.ScheduleRevival))]
 public static class LostPhylacteryNoSchedule {static bool Prefix(Phylactery __instance)=>!NetherilQuest.PhylacteryLost(__instance.soul);}

 // ---------- 站点 ----------
 public class SitePartWorker_Netheril : SitePartWorker {
  public override void PostMapGenerate(Map map){
   base.PostMapGenerate(map);
   if(def.defName=="LP_LarlochCrypt"){var f=NetherilQuest.Larloch;if(f!=null && !f.HostileTo(Faction.OfPlayer)){Factions.SetRelation(f,Faction.OfPlayer,-100);Find.LetterStack.ReceiveLetter("闯入古墓","你闯进了拉洛克的古墓。拉洛克的扈从从此与你为敌。",LetterDefOf.ThreatBig,LookTargets.Invalid,f);}}
  }
 }
 // 三个站点的地图：红袍法师营地（塞伊守军 + 精英法师）、拉洛克的古墓（石砌墓室）、沉没的耐瑟浮城残骸（塞伊占着，里面有卷轴）。
 public class GenStep_NetherilSite : GenStep {
  public string kind="camp";
  public override int SeedPart=>737100+kind.Length;
  public override void Generate(Map map,GenStepParams parms){
   float points=parms.sitePart?.parms?.threatPoints??StorytellerUtility.DefaultThreatPointsNow(map);
   var f=parms.sitePart?.site?.Faction;
   try{switch(kind){case "crypt":Crypt(map,f??NetherilQuest.Larloch,points);break;case "wreck":Wreck(map,f??NetherilQuest.Thay,points);break;default:Camp(map,f??NetherilQuest.Thay,points);break;}}
   catch(Exception e){Log.Error("[DND边缘大陆] 生成耐瑟瑞尔站点地图出错（"+kind+"）："+e);}
  }
  static ThingDef Stuff(string n)=>DefDatabase<ThingDef>.GetNamedSilentFail(n)??ThingDefOf.BlocksGranite;
  static TerrainDef Floor(string n)=>DefDatabase<TerrainDef>.GetNamedSilentFail(n)??TerrainDefOf.Soil;
  static void Clear(Map map,CellRect r){foreach(var c in r.Cells){if(!c.InBounds(map))continue;foreach(var t in c.GetThingList(map).ToList())if(!(t is Pawn))t.Destroy(DestroyMode.Vanish);}}
  static void Put(Map map,ThingDef def,ThingDef stuff,IntVec3 c,Faction f=null){if(!c.InBounds(map))return;foreach(var t in c.GetThingList(map).ToList())if(!(t is Pawn))t.Destroy(DestroyMode.Vanish);var w=ThingMaker.MakeThing(def,stuff);if(f!=null && w.def.CanHaveFaction)w.SetFaction(f);GenSpawn.Spawn(w,c,map);}
  static List<Pawn> Group(Map map,Faction f,float points){
   if(f==null)return new List<Pawn>();points=Mathf.Max(points,f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)*1.05f);
   return PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,tile=map.Tile,faction=f,points=points,inhabitants=true},false).ToList();
  }
  static void Defend(Map map,Faction f,List<Pawn> pawns,IntVec3 at,float radius){
   pawns=pawns.Where(p=>p!=null && !p.Destroyed).ToList();if(pawns.Count==0)return;
   foreach(var p in pawns)if(!p.Spawned)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(at,map,(int)Mathf.Max(2,radius/2)),map);
   LordMaker.MakeNewLord(f,new LordJob_DefendPoint(at,radius,radius+6f,false,false),map,pawns);
  }
  public static void Camp(Map map,Faction f,float points){
   var c=map.Center;var st=Netheril.State;
   Clear(map,CellRect.CenteredOn(c,6));
   foreach(var x in GenRadial.RadialCellsAround(c,6.5f,false).Where(x=>x.DistanceTo(c)>=5.8f && Rand.Chance(.45f)))Put(map,ThingDefOf.Wall,Stuff("WoodLog"),x);
   var fire=DefDatabase<ThingDef>.GetNamedSilentFail("Campfire");if(fire!=null)Put(map,fire,null,c+IntVec3.East*2,f);
   var pawns=Group(map,f,points);
   var leader=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_FH_RedWizard"),f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true,canGeneratePawnRelations:false));
   Items.Give(leader,"LP_ThayEliteMage");FactionGroups.RedRobe(leader);AdventurerEnemies.Story(leader,13);
   if(leader.Name is NameTriple n)leader.Name=new NameTriple(n.First,"精英法师",n.Last);
   GenSpawn.Spawn(leader,c,map);pawns.Insert(0,leader);
   if(st!=null)st.campLeader=leader;
   Defend(map,f,pawns,c,5f);
  }
  public static void Crypt(Map map,Faction f,float points){
   var c=map.Center;var stone=Stuff("BlocksGranite");var floor=Floor("FlagstoneGranite");var st=Netheril.State;
   var outer=new CellRect(c.x-16,c.z-12,33,25).ClipInsideMap(map);var hall=new CellRect(c.x-6,c.z-4,13,9);
   Clear(map,outer.ExpandedBy(1).ClipInsideMap(map));
   foreach(var x in outer.Cells){map.terrainGrid.SetTerrain(x,floor);map.roofGrid.SetRoof(x,RoofDefOf.RoofConstructed);}
   var walls=new HashSet<IntVec3>(outer.EdgeCells);foreach(var x in hall.EdgeCells)walls.Add(x);
   for(int x=outer.minX;x<hall.minX;x++)walls.Add(new IntVec3(x,0,c.z));for(int x=hall.maxX+1;x<=outer.maxX;x++)walls.Add(new IntVec3(x,0,c.z));
   for(int z=outer.minZ;z<hall.minZ;z++)walls.Add(new IntVec3(c.x,0,z));for(int z=hall.maxZ+1;z<=outer.maxZ;z++)walls.Add(new IntVec3(c.x,0,z));
   var doors=new HashSet<IntVec3>{new IntVec3(c.x+5,0,outer.minZ),new IntVec3(c.x+2,0,hall.minZ),new IntVec3(c.x-2,0,hall.maxZ),new IntVec3(hall.maxX,0,c.z+2),new IntVec3(hall.minX,0,c.z-2),
    new IntVec3((outer.minX+hall.minX)/2,0,c.z),new IntVec3((outer.maxX+hall.maxX)/2,0,c.z),new IntVec3(c.x,0,(outer.minZ+hall.minZ)/2),new IntVec3(c.x,0,(outer.maxZ+hall.maxZ)/2)};
   foreach(var x in walls)if(!doors.Contains(x))Put(map,ThingDefOf.Wall,stone,x);
   foreach(var x in doors)Put(map,ThingDefOf.Door,stone,x,f);
   // 中央大厅：架子上放卡苏斯之卷。
   var shelf=DefDatabase<ThingDef>.GetNamedSilentFail("ShelfSmall");var shelfCell=new IntVec3(c.x,0,hall.maxZ-1);
   if(shelf!=null)Put(map,shelf,GenStuff.DefaultStuffFor(shelf),shelfCell);
   if(st!=null && (st.karsusStatus=="larloch" || st.karsusStatus=="none") && NetherilQuest.Karsus==null){var k=NetherilQuest.MakeKarsus();if(k!=null){GenSpawn.Spawn(k,shelfCell,map);k.SetForbidden(false,false);st.karsusStatus="larloch";}}
   if(f==null)return;
   int demi=points>=1500f?3:2;
   var hallGuards=new List<Pawn>();Pawn larloch=null;
   if(st==null || !st.larlochDefeated){
    larloch=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_Larloch"),f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:60,fixedChronologicalAge:1200));
    NetherilQuest.MakeDemilich(larloch,true);AdventurerEnemies.Story(larloch,18);hallGuards.Add(larloch);
   }
   var rooms=new[]{new IntVec3((outer.minX+hall.minX)/2,0,(outer.minZ+c.z)/2),new IntVec3((outer.maxX+hall.maxX)/2,0,(outer.minZ+c.z)/2),new IntVec3((outer.minX+hall.minX)/2,0,(outer.maxZ+c.z)/2),new IntVec3((outer.maxX+hall.maxX)/2,0,(outer.maxZ+c.z)/2)};
   int skeletons=Mathf.Clamp((int)((points-demi*150f)/35f),4,20),golems=points>=2000f?2:points>=1000f?1:0;
   var masters=new List<Pawn>();
   for(int i=0;i<demi;i++){var d=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_LarlochDemilich"),f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false));NetherilQuest.MakeDemilich(d,false);AdventurerEnemies.Story(d,12);masters.Add(d);}
   var groups=rooms.Select(r=>new List<Pawn>()).ToList();
   for(int i=0;i<masters.Count;i++)groups[i%4].Add(masters[i]);
   for(int i=0;i<skeletons+golems;i++){var m=masters[i%masters.Count];var g=FactionConstructs.MakeBound(i<golems?"Golem":"Skeleton",m,f);g.Name=new NameSingle(i<golems?"拉洛克的血肉巨像":"拉洛克的骷髅");if(i%5==4)hallGuards.Add(g);else groups[i%4].Add(g);}
   for(int i=0;i<4;i++)Defend(map,f,groups[i],rooms[i],3f);
   Defend(map,f,hallGuards,hall.CenterCell,4f);
  }
  public static void Wreck(Map map,Faction f,float points){
   var c=map.Center;var stone=Stuff("BlocksMarble");var floor=Floor("FlagstoneMarble");var st=Netheril.State;
   Clear(map,CellRect.CenteredOn(c,11));
   foreach(var x in GenRadial.RadialCellsAround(c,10.5f,true)){if(Rand.Chance(.8f))map.terrainGrid.SetTerrain(x,floor);}
   foreach(var x in GenRadial.RadialCellsAround(c,10.5f,true).Where(x=>x.DistanceTo(c)>=9.4f && Rand.Chance(.55f)))Put(map,ThingDefOf.Wall,stone,x);
   foreach(var x in GenRadial.RadialCellsAround(c,6f,false).Where(x=>Rand.Chance(.06f)))Put(map,ThingDefOf.Wall,stone,x);
   if(st!=null && NetherilQuest.Karsus==null){var k=NetherilQuest.MakeKarsus();if(k!=null){GenSpawn.Spawn(k,c,map);k.SetForbidden(false,false);st.karsusStatus="site";}}
   var residue=Items.Thing("LP_PlanarResidue");if(residue!=null){var r=ThingMaker.MakeThing(residue);r.stackCount=Rand.RangeInclusive(5,12);GenPlace.TryPlaceThing(r,c+IntVec3.North*3,map,ThingPlaceMode.Near);}
   Defend(map,f,Group(map,f,points),c,6f);
  }
 }

 // 拉洛克扈从的袭击组：生成的“拉洛克的骷髅”换成绑在半巫妖名下的死灵造物，半巫妖换上造型。
 [HarmonyPatch(typeof(PawnGroupMakerUtility),nameof(PawnGroupMakerUtility.GeneratePawns))]
 public static class LarlochGroupPostProcess {
  static void Postfix(PawnGroupMakerParms parms,ref IEnumerable<Pawn> __result){
   if(parms?.faction?.def==null || parms.faction.def!=NetherilQuest.LarlochDef)return;
   var list=__result?.ToList()??new List<Pawn>();var outList=new List<Pawn>();
   var masters=list.Where(p=>p.kindDef.defName!="LP_LarlochSkeleton").ToList();
   foreach(var m in masters){if(!Items.Has(m,"LP_NetherilUndead") && !Items.Has(m,"LP_LarlochAvatar"))NetherilQuest.MakeDemilich(m,m.kindDef.defName=="LP_Larloch");outList.Add(m);}
   var master=masters.FirstOrDefault()??NetherilQuest.Master;
   foreach(var s in list.Where(p=>p.kindDef.defName=="LP_LarlochSkeleton")){if(Find.WorldPawns.Contains(s))Find.WorldPawns.RemovePawn(s);s.Destroy();if(master!=null)outList.Add(NetherilQuest.Skeleton(master,parms.faction));}
   __result=outList;
  }
 }

 public class IncidentWorker_LarlochEnvoy : IncidentWorker {
  protected override bool CanFireNowSub(IncidentParms parms)=>parms.target is Map map && map.IsPlayerHome && NetherilQuest.Larloch!=null && Netheril.State!=null && !Netheril.State.envoyPending;
  protected override bool TryExecuteWorker(IncidentParms parms)=>NetherilQuest.SendEnvoy(parms.target as Map,false)!=null;
 }

 // 1.37.0 设置：任务线开关、拉洛克亡灵袭击开关（在“事件”分组里）。
 public partial class Settings {
  public bool netherilQuest=true,larlochRaids=true,netherilIdleStudy=true;
  void ExposeNetheril(){Scribe_Values.Look(ref netherilQuest,"lpNetherilQuest",true);Scribe_Values.Look(ref larlochRaids,"lpLarlochRaids",true);Scribe_Values.Look(ref netherilIdleStudy,"lpNetherilIdleStudy",true);}
 }
 public static class NetherilSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.CheckboxLabeled("耐瑟瑞尔任务线",ref s.netherilQuest,"完成研究“耐瑟瑞尔奥术”、殖民者累计读耐瑟卷轴 20 小时后，影巫王拉洛克的使者会来谈卡苏斯之卷。关掉后不再触发使者（已经开始的任务照常进行）。");
   l.CheckboxLabeled("拉洛克亡灵袭击",ref s.larlochRaids,"打下拉洛克的古墓以后，每 60～90 天有一小队亡灵来袭（点数 ×0.6）。");
   l.CheckboxLabeled("空闲时自动研读耐瑟卷轴",ref s.netherilIdleStudy,"有心灵链接、智识 6 级以上、心灵熵低于上限一半的殖民者没事可做时，会自己拿起对他有用的耐瑟卷轴读（卡苏斯之卷 > 秘术残卷 > 研习卷，同类挑最近），最多读 2 小时。不想让某人读：禁止那卷卷轴或用区域限制。");
   if(Current.ProgramState==ProgramState.Playing && Netheril.State is NetherilQuestState st && (st.stage=="refused" || st.stage=="scheduled") && !st.envoyPending && l.ButtonText("让拉洛克的使者马上再来（补救：以前点选项没反应、使者走了）")){var r=NetherilQuest.RecallEnvoy();Messages.Message(r??"拉洛克的使者正在赶来（退回了一次拒绝）",r==null?MessageTypeDefOf.NeutralEvent:MessageTypeDefOf.RejectInput,false);}
  }
 }
}
