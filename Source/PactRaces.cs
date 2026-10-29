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
 // 1.55.0 新单位（规格第三节，Defs/Pact_Races.xml）：九狱（链魔、骨魔、角魔、艾林魔、阿姆尼祖、九狱小魔鬼）、妖精（萨特、林仙、红帽、小精灵、调换儿、狂猎骑手）、天界（狄瓦、行星使）。
 // 外来者判定都走 1.54.0 的 LP_OutsiderExtension：九狱 LP_HellsCore、妖精 LP_FeyCore、天界 LP_CelestialCore。
 public static class PactRaces {
  public const string Satyr="LP_Satyr",Dryad="LP_Dryad",Changeling="LP_Changeling",Deva="LP_Deva",Planetar="LP_Planetar",Erinyes="LP_Erinyes",Redcap="LP_Redcap",WildHunter="LP_WildHunter";
  public const float TreeLeash=40f,TreeStrideRange=30f,MatureGrowth=.8f,DreadRadius=8f,SubvertRadius=6f,WrathRadius=8f,VeilRadius=6f,PreyFactor=1.3f,MarkFactor=1.15f,BowFactor=1.2f,CelestialFactor=1.5f,MantleRadiant=1.1f,MantleTaint=.5f,StompFactor=1.5f;
  public const int DreadDC=13,SubvertDC=13,VenomDC=14,AmnizuDC=16,BanishDC=20,VeilTicks=360,EntangleTicks=240,ChargeStun=60,BloodlustMax=5,BloodlustPerStack=3,SubvertTicks=600,SeveredDays=1,DriftDays=30;
  public static int dryads; // 存在过的“树之绑定”基因数（砍树补丁的快速返回）
  public static float? TestRoll;public static float Roll=>TestRoll??Rand.Value;
  static XenotypeDef deva,erinyes;static ThingDef pitchfork,bow,mantle,rope;
  public static XenotypeDef DevaXeno=>deva??(deva=DefDatabase<XenotypeDef>.GetNamedSilentFail(Deva));
  public static XenotypeDef ErinyesXeno=>erinyes??(erinyes=DefDatabase<XenotypeDef>.GetNamedSilentFail(Erinyes));
  public static ThingDef Pitchfork=>pitchfork??(pitchfork=DefDatabase<ThingDef>.GetNamedSilentFail("LP_InfernalPitchfork"));
  public static ThingDef Bow=>bow??(bow=DefDatabase<ThingDef>.GetNamedSilentFail("LP_FeySilverBow"));
  public static ThingDef Mantle=>mantle??(mantle=DefDatabase<ThingDef>.GetNamedSilentFail("LP_CelestialMantle"));
  public static ThingDef Rope=>rope??(rope=DefDatabase<ThingDef>.GetNamedSilentFail("LP_EntanglingRope"));
  public static bool Is(Pawn p,string xeno)=>p?.genes?.Xenotype?.defName==xeno;
  public static bool IsDryad(Pawn p)=>Races124.Has(p,"LP_DryadTree");
  public static bool IsSatyr(Pawn p)=>Races124.Has(p,"LP_SatyrHooves");
  public static bool IsChangeling(Pawn p)=>Races124.Has(p,"LP_ChangelingFaces");
  public static bool IsDeva(Pawn p)=>Races124.Has(p,"LP_DevaMace");
  public static bool IsErinyes(Pawn p)=>Races124.Has(p,"LP_ErinyesHunt");
  public static bool Celestial(Pawn p)=>Outsiders.Of(p)?.plane==OutsiderPlane.Celestia;
  public static bool Fiendish(Pawn p)=>Outsiders.IsDemon(p) || Outsiders.IsDevil(p) || Items.Undead(p);
  public static Gene_DryadTree TreeGene(Pawn p)=>p?.genes?.GenesListForReading.OfType<Gene_DryadTree>().FirstOrDefault(g=>g.Active);
  public static Gene_ChangelingFaces Faces(Pawn p)=>p?.genes?.GenesListForReading.OfType<Gene_ChangelingFaces>().FirstOrDefault(g=>g.Active);
  public static void Teleport(Pawn p,IntVec3 c){var map=p.Map;FleckMaker.Static(p.Position,map,FleckDefOf.PsycastAreaEffect,1.2f);p.pather?.StopDead();p.Position=c;p.Notify_Teleported(false,true);FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,1.2f);}
  public static IntVec3 Beside(Pawn caster,Thing t)=>GenAdj.CellsAdjacent8Way(t).Where(c=>c.InBounds(t.Map) && c.Standable(t.Map) && c.GetFirstPawn(t.Map)==null).OrderBy(c=>c.DistanceToSquared(caster.Position)).FirstOrDefault(IntVec3.Invalid);

  // ---------- 九狱单位守契约 ----------
  // 服从度对九狱单位、林仙、守护天使、契约借来的守卫不衰减（AbyssBinding.DailyDecay 第一行）。
  public static bool NoDecay(Pawn p)=>p!=null && (Outsiders.IsDevil(p) || IsDryad(p) || Items.Has(p,"LP_PactBound") || Items.Has(p,"LP_CelestialGuardian"));
  // 九狱单位服从度到 0 不开打：信件“它要求重新谈判”。
  public static bool Renegotiate(Pawn p){
   if(p==null || !Outsiders.IsDevil(p) || p.Faction?.IsPlayer!=true)return false;
   var h=AbyssBinding.Binding(p);if(h!=null)h.Severity=Mathf.Max(h.Severity,1f);
   PactLetters.Send("renegotiate",null,p,null,p.LabelShort+"要求重新谈判",p.LabelShort+"的服从度见了底。九狱的造物守契约：它没有挣脱，而是摊开一张新的契约，要求重新谈判——给它 300 银或者 2 枚灵魂币，它就照旧留下（服从度回到 50）；不然它就走（不会动手）。");
   return true;
  }
  public static void Renegotiated(Pawn p,string paid){
   var map=p.MapHeld;
   if(paid.Contains("银")){if(!Pacts.TakeSilver(map,300))return;}else if(!AbyssRitual.TakeItems(map,AbyssItems.SoulCoin,2))return;
   var h=AbyssBinding.Binding(p);if(h!=null)h.Severity=50f;
   Messages.Message(p.LabelShort+"收下了"+paid+"，在新契约上按了手印（服从度 50）。",p,MessageTypeDefOf.PositiveEvent,false);
  }
  public static void LetLeave(Pawn p){
   if(p.Spawned){Outsiders.DropBelongings(p,p.Position,p.Map);for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(p.DrawPos,p.Map,2f);p.DeSpawn();}
   var b=AbyssBinding.Binding(p);if(b!=null){b.breaking=true;p.health.RemoveHediff(b);}
   if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);
   Messages.Message(p.LabelShort+"收起契约，回九狱去了。",MessageTypeDefOf.NeutralEvent,false);
   p.Destroy();
  }
  // 守护天使失望离开（殖民地里腐化到恶魔之印的人太多）。
  public static void LetLeaveCelestial(Pawn p){
   string name=p.LabelShort;
   if(p.Spawned){Outsiders.DropBelongings(p,p.Position,p.Map);FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,2f);p.DeSpawn();}
   var b=AbyssBinding.Binding(p);if(b!=null){b.breaking=true;p.health.RemoveHediff(b);}
   if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);
   Find.LetterStack.ReceiveLetter("守护天使离开了",name+"看着殖民地里越来越浓的深渊气息，失望地展开翅膀回了天界。",LetterDefOf.NegativeEvent);
   p.Destroy();
  }
  // 救赎：狄瓦对艾林魔的“劝返”累计 3 次 → 变成狄瓦（换异种型）；来自阿斯莫蒂尔斯契约的，该契约债务 +2。
  public static bool Redeem(Pawn erinyes){
   if(erinyes==null || DevaXeno==null)return false;
   AbyssRaces.ConvertTo(erinyes,DevaXeno);Items.Remove(erinyes,"LP_RedeemProgress");
   var c=Pacts.Comp;var pact=c?.Active.FirstOrDefault(x=>x.bound==erinyes);
   if(pact!=null){pact.bound=null;Items.Remove(erinyes,"LP_PactBound");Pacts.AddDebt(pact,2,"借来的艾林魔被狄瓦劝返了");}
   Find.LetterStack.ReceiveLetter("救赎："+erinyes.LabelShort,"狄瓦第三次和"+erinyes.LabelShort+"长谈之后，"+erinyes.LabelShort+"的黑羽一片片褪成了白色：她回到了天界的一边，成了一名狄瓦。",LetterDefOf.PositiveEvent,erinyes);
   return true;
  }

  // ---------- 林仙 ----------
  public static bool TreeOk(Thing t,Pawn d)=>t is Plant pl && pl.Spawned && pl.def.plant!=null && pl.def.plant.IsTree && pl.Growth>=MatureGrowth && !(pl.Map.zoneManager.ZoneAt(pl.Position) is Zone_Growing) && (d==null || TreeOwner(pl)==null || TreeOwner(pl)==d);
  public static Pawn TreeOwner(Plant t){if(t?.Map==null || dryads==0)return null;foreach(var p in t.Map.mapPawns.AllPawnsSpawned){var g=TreeGene(p);if(g!=null && g.tree==t)return p;}return null;}
  public static Plant NearestTree(Pawn p,float max=999f){
   if(p?.Map==null)return null;Plant best=null;float bd=max*max;
   foreach(var t in p.Map.listerThings.ThingsInGroup(ThingRequestGroup.Plant)){if(!TreeOk(t,p))continue;float d=t.Position.DistanceToSquared(p.Position);if(d<bd){bd=d;best=(Plant)t;}}
   return best;
  }
  public static bool BindNearestTree(Pawn p){var g=TreeGene(p);if(g==null)return false;var t=NearestTree(p);if(t==null){g.StartDrift();return false;}g.Bind(t);return true;}
  public static void TreeCut(Plant t,Pawn by){
   if(t?.Map==null)return;var owner=TreeOwner(t);if(owner==null)return;
   Find.LetterStack.ReceiveLetter("林仙的树被砍了",by.LabelShort+"砍倒了"+owner.LabelShort+"的树。远处传来了号角声——狂猎今晚会来找"+by.LabelShort+"。",LetterDefOf.ThreatBig,by);
   PactEvents.StartWildHunt(t.Map,by,-1,StorytellerUtility.DefaultThreatPointsNow(t.Map),true);
  }
  // ---------- 萨特：派对 / 婚礼时萨特心情 +10 ----------
  public static void Revelry(Map map){
   if(map==null)return;var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_SatyrRevelry");if(th==null)return;
   foreach(var p in map.mapPawns.FreeColonistsSpawned)if(Races124.Has(p,"LP_SatyrRevelry"))p.needs?.mood?.thoughts?.memories?.TryGainMemory(th);
  }

  // ---------- 能力 ----------
  public static string Invalid(CompAbilityEffect_Pact c,LocalTargetInfo t){
   var caster=c.parent.pawn;var map=caster?.Map;if(map==null)return "施法者不在地图上。";
   switch(c.P.effect){
    case "summon":return null;
    case "summerTouch":case "healTouch":return t.Pawn==null || t.Pawn.Dead?"需要一个活着的人":!t.Pawn.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury && !(h is Hediff_MissingPart) || h.def==Items.Hediff("LP_InfernalWound"))?t.Pawn.LabelShort+"没有可以治的伤":null;
    case "veil":case "wrath":return null;
    case "chainLash":case "hellfire":case "huntMark":case "entangle":case "rope":return t.Pawn==null || t.Pawn.Dead?"需要一个目标":null;
    case "charge":{var p=t.Pawn;if(p==null || p.Dead)return "需要一个目标";if(!Beside(caster,p).IsValid)return "目标旁边没有落脚的地方";return null;}
    case "treeStride":return !TreeOk(t.Thing,caster)?"要选 "+TreeStrideRange+" 格内一棵成年的树（不在种植区里）":t.Thing==TreeGene(caster)?.tree && caster.Position.InHorDistOf(t.Thing.Position,1.5f)?"已经在这棵树旁边了":!Beside(caster,t.Thing).IsValid?"树旁边没有落脚的地方":null;
    case "bindTree":return !TreeOk(t.Thing,caster)?"要选一棵成年的树（成长 ≥80%，不在种植区里，没被别的林仙占着）":null;
    case "faces":{var p=t.Pawn;if(p==null || p==caster || !p.RaceProps.Humanlike || p.Dead)return "要选一个看得见的人形";if(Faces(caster)?.on==true)return "已经在伪装了";return null;}
    case "banish":{var p=t.Pawn;return p==null || !Outsiders.Is(p)?"只能驱散外来者（恶魔、魔鬼、妖精）":Celestial(p)?"不能驱散天界的同伴":null;}
   }
   return null;
  }
  public static bool AICan(CompAbilityEffect_Pact c,LocalTargetInfo target){
   var caster=c.parent.pawn;var t=target.Pawn;
   switch(c.P.effect){
    case "chainLash":case "hellfire":case "entangle":case "huntMark":return t!=null && !t.Dead && !t.Downed && t.HostileTo(caster) && (c.P.effect!="huntMark" || !Items.Has(t,"LP_HuntMarked"));
    case "charge":return t!=null && !t.Dead && !t.Downed && t.HostileTo(caster) && t.Position.DistanceTo(caster.Position)>=2.5f;
    case "wrath":return caster.Map!=null && GenRadial.RadialDistinctThingsAround(caster.Position,caster.Map,WrathRadius,true).OfType<Pawn>().Count(p=>p.HostileTo(caster) && !p.Dead && !p.Downed)>=2;
    case "banish":return t!=null && Outsiders.Is(t) && t.HostileTo(caster) && !Celestial(t);
    case "healTouch":return t!=null && !t.HostileTo(caster) && t.health.summaryHealth.SummaryHealthPercent<.6f;
   }
   return false;
  }
  public static void ApplyAbility(CompAbilityEffect_Pact c,LocalTargetInfo target){
   var caster=c.parent.pawn;var map=caster.Map;var P=c.P;var t=target.Pawn;
   switch(P.effect){
    case "summon":Summon(caster,P.kind,P.count,P.days);break;
    case "summerTouch":case "healTouch":if(PactBoons.HealWorst(t))Items.Mote(t,"伤口愈合了",new Color(.6f,1f,.6f));break;
    case "veil":Veil(caster);break;
    case "chainLash":{
     t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,14f,.2f,-1f,caster));
     if(!t.Dead && t.Spawned){var dest=GenAdj.CellsAdjacent8Way(caster).Where(x=>x.InBounds(map) && x.Standable(map) && x.GetFirstPawn(map)==null).OrderBy(x=>x.DistanceToSquared(t.Position)).FirstOrDefault(IntVec3.Invalid);if(dest.IsValid)DNDPush.Move(t,dest);}
     break;
    }
    case "hellfire":t.TakeDamage(new DamageInfo(DamageDefOf.Flame,15f,0f,-1f,caster));if(!t.Dead)FireUtility.TryAttachFire(t,.3f,caster);FleckMaker.ThrowFireGlow(t.DrawPos,map,1f);break;
    case "huntMark":{var h=(Hediff_HuntMarked)Items.Give(t,"LP_HuntMarked");if(h!=null)h.hunter=caster;Items.Mote(t,"被追猎",new Color(.85f,.3f,.3f));break;}
    case "entangle":case "rope":
     if(P.effect=="rope" && !UseRope(caster))break;
     if(Legendary.Save(t,Adventurer.Dex,Legendary.DC(caster,Adventurer.Dex),caster))break;
     t.stances?.stunner?.StunFor(EntangleTicks,caster,false,true,false);Items.Mote(t,"被缚住了",new Color(.9f,.8f,.6f));break;
    case "charge":{var cell=Beside(caster,t);if(!cell.IsValid)break;Teleport(caster,cell);for(int i=0;i<4;i++)FleckMaker.ThrowDustPuffThick(caster.Position.ToVector3Shifted(),map,1.4f,new Color(.5f,.42f,.3f));if(!Legendary.Save(t,Adventurer.Str,Legendary.DC(caster,Adventurer.Str),caster))t.stances?.stunner?.StunFor(ChargeStun,caster,false,true,false);break;}
    case "treeStride":{var cell=Beside(caster,target.Thing);if(cell.IsValid)Teleport(caster,cell);break;}
    case "bindTree":TreeGene(caster)?.Bind((Plant)target.Thing);break;
    case "faces":Faces(caster)?.Copy(t);break;
    case "wrath":Wrath(caster);break;
    case "banish":Banish(caster,t);break;
   }
  }
  public static List<Pawn> Summon(Pawn caster,PawnKindDef kind,int count,float days){
   var made=new List<Pawn>();var map=caster.Map;if(kind==null || map==null)return made;
   var pact=Pacts.Of(caster);float f=pact?.boonFactor??1f;
   for(int i=0;i<count;i++){var p=AbyssRaces.Make(kind.defName,Faction.OfPlayer,map.Tile);if(p==null)continue;GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(caster.Position,map,3),map);AbyssBinding.MakeThrall(p,Mathf.RoundToInt(days*f*GenDate.TicksPerDay));made.Add(p);if(pact!=null)pact.guards.Add(p);}
   for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(caster.DrawPos,map,1.6f);
   if(made.Count>0)Messages.Message(caster.LabelShort+"召来了"+(made.Count>1?made.Count+"个":"")+made[0].KindLabel+"（仆从 "+(days*f).ToString("0.#")+" 天）。",made[0],MessageTypeDefOf.PositiveEvent,false);
   return made;
  }
  // 冬之帷幕：6 格内的敌人做感知豁免（DC = 施法者的魅力 DC），失败被魅惑 6 秒。
  public static int Veil(Pawn caster){
   int n=0;var def=DefDatabase<MentalStateDef>.GetNamedSilentFail("LP_DragonCharmed");if(def==null)return 0;int dc=Legendary.DC(caster,Adventurer.Cha);
   foreach(var p in GenRadial.RadialDistinctThingsAround(caster.Position,caster.Map,VeilRadius,true).OfType<Pawn>().ToList()){
    if(p.Dead || p.Downed || !p.HostileTo(caster) || !p.RaceProps.Humanlike)continue;if(Legendary.Save(p,Adventurer.Wis,dc,caster))continue;
    if(p.mindState.mentalStateHandler.TryStartMentalState(def,"冬之帷幕",forced:true,forceWake:true) && p.MentalState!=null){p.MentalState.forceRecoverAfterTicks=VeilTicks;n++;Items.Mote(p,"被魅惑了",new Color(.7f,.85f,1f));}
   }
   for(int i=0;i<8;i++)FleckMaker.ThrowAirPuffUp(caster.DrawPos+new Vector3(Rand.Range(-2f,2f),0,Rand.Range(-2f,2f)),caster.Map);
   return n;
  }
  public static int Wrath(Pawn caster){
   int n=0;var map=caster.Map;
   foreach(var p in GenRadial.RadialDistinctThingsAround(caster.Position,map,WrathRadius,true).OfType<Pawn>().ToList()){if(p==caster || p.Dead || !p.HostileTo(caster))continue;p.TakeDamage(new DamageInfo(Faith.Radiant,30f,.5f,-1f,caster));n++;}
   FleckMaker.Static(caster.Position,map,FleckDefOf.PsycastAreaEffect,WrathRadius);
   return n;
  }
  // 驱散：一个外来者做魅力豁免 DC 20，失败遣返；首领（传奇单位）先消耗传奇抗性。
  public static bool Banish(Pawn caster,Pawn t){
   if(t==null || t.Dead)return false;
   if(!Legendary.Save(t,Adventurer.Cha,BanishDC,caster)){
    if(Legendary.Resist(t,"驱散"))return false;
    if(Fiends.Is(t) && t.Spawned)Outsiders.Banish(t);else if(t.Spawned){FleckMaker.Static(t.Position,t.Map,FleckDefOf.PsycastAreaEffect,1.6f);t.DeSpawn();if(!Find.WorldPawns.Contains(t))Find.WorldPawns.PassToWorld(t,PawnDiscardDecideMode.Discard);}
    return true;
   }
   return false;
  }
  public static bool UseRope(Pawn p){
   var a=Items.Worn(p,Rope);var c=a?.GetComp<CompMagicCharges>();if(a==null || c==null)return true;
   if(!c.TryUse())return false;if(c.Charges<=0){p.apparel.Remove(a);a.Destroy();Messages.Message("缚绳用断了。",p,MessageTypeDefOf.NeutralEvent,false);}
   return true;
  }

  // ---------- 生成 ----------
  public static void OnGenerated(Pawn p){
   if(p==null)return;
   if(p.RaceProps.Animal){if(p.def.defName=="LP_Pixie" && !Items.Has(p,"LP_PixieNature"))Items.Give(p,"LP_PixieNature");if(p.def.defName=="LP_Imp" && !Items.Has(p,"LP_ImpNature"))Items.Give(p,"LP_ImpNature");return;}
   if(p.genes==null)return;
   if(Races124.Has(p,"LP_HornedDevilStature") && Pitchfork!=null && p.equipment!=null && p.equipment.Primary==null){var w=(ThingWithComps)ThingMaker.MakeThing(Pitchfork);p.equipment.AddEquipment(w);}
  }
  // 敌对单位的掉落（遣返时）：角魔 30% 地狱钢叉（否则毁掉）、狂猎骑手寒铁 10 + 30% 号角、敌对艾林魔 25% 缚绳、讨债者 / 狂猎首领 100% 宗主代理人的真名卷。
  public static void BanishLoot(Pawn p,IntVec3 cell,Map map){
   if(p==null || map==null)return;
   var w=p.equipment?.Primary;if(w!=null && w.def==Pitchfork && Roll>=.3f){p.equipment.DestroyEquipment(w);}
   if(Races124.Has(p,"LP_WildHunterPrey")){var ci=Items.Thing("LP_ColdIron");if(ci!=null){var t=ThingMaker.MakeThing(ci);t.stackCount=10;GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);}if(Roll<.3f && Items.Thing("LP_WildHuntHorn")!=null)GenPlace.TryPlaceThing(ThingMaker.MakeThing(Items.Thing("LP_WildHuntHorn")),cell,map,ThingPlaceMode.Near);}
   if(IsErinyes(p) && Rope!=null && Roll<.25f)GenPlace.TryPlaceThing(ThingMaker.MakeThing(Rope),cell,map,ThingPlaceMode.Near);
   var lead=p.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_PatronAgent")) as Hediff_PatronAgent;
   if(lead?.patron!=null && AbyssItems.TrueNameScroll!=null){
    var s=ThingMaker.MakeThing(AbyssItems.TrueNameScroll);var c=s.TryGetComp<CompTrueName>();
    if(c!=null){c.trueName=TrueNames.Generate(p.thingIDNumber^0x51A7);c.kind=lead.patron.defName;c.kindLabel=lead.patron.agentLabel??(lead.patron.label+"的代理人");c.pawnId=-1;}
    GenPlace.TryPlaceThing(s,cell,map,ThingPlaceMode.Near);
    Find.LetterStack.ReceiveLetter("宗主代理人的真名",p.LabelShort+"倒下时掉出了一张真名卷，上面写着"+lead.patron.label+"代理人的真名。拿到契约讲台上，可以无代价地撕毁一份和"+lead.patron.label+"签的契约。",LetterDefOf.PositiveEvent,s);
   }
  }
  // 深渊腐化获得：披着天界羽披 ×0.5。
  public static float TaintFactor(Pawn p)=>Mantle!=null && Items.Worn(p,Mantle)!=null?MantleTaint:1f;
 }

 // ---------- 基因 ----------
 // 链魔：策反骷髅——6 格内巫妖的死灵造物做意志（感知）豁免，失败 10 秒狂暴。
 public class Gene_ChainDevilSubvert : Gene {
  int acc;readonly Dictionary<int,int> tried=new Dictionary<int,int>();
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<300)return;acc=0;if(!Active || !pawn.Spawned || pawn.Downed || pawn.Faction?.IsPlayer==true)return;
   var reg=Necro.Registry;if(reg==null || reg.constructs.Count==0)return;int now=Find.TickManager.TicksGame;
   foreach(var s in reg.constructs){
    if(s==null || !s.Spawned || s.Map!=pawn.Map || s.Dead || !s.HostileTo(pawn) || !s.Position.InHorDistOf(pawn.Position,PactRaces.SubvertRadius))continue;
    if(tried.TryGetValue(s.thingIDNumber,out int t) && now-t<GenDate.TicksPerHour)continue;tried[s.thingIDNumber]=now;
    if(Legendary.Save(s,Adventurer.Wis,PactRaces.SubvertDC,pawn))continue;
    if(s.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk,"链魔的策反",forced:true) && s.MentalState!=null)s.MentalState.forceRecoverAfterTicks=PactRaces.SubvertTicks;
   }
  }
 }
 // 骨魔：恐惧气场——第一次看到它的敌人做感知豁免，失败惊惧 10 秒。
 public class Gene_BoneDevilDread : Gene {
  int acc;readonly HashSet<int> seen=new HashSet<int>();
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<300)return;acc=0;if(!Active || !pawn.Spawned || pawn.Downed)return;
   foreach(var t in pawn.Map.attackTargetsCache.TargetsHostileToFaction(pawn.Faction)){
    if(!(t.Thing is Pawn e) || e.Dead || !e.RaceProps.Humanlike || seen.Contains(e.thingIDNumber) || !e.Position.InHorDistOf(pawn.Position,PactRaces.DreadRadius))continue;
    seen.Add(e.thingIDNumber);if(Legendary.FearImmune(e) || Legendary.Save(e,Adventurer.Wis,PactRaces.DreadDC,pawn))continue;
    Items.Give(e,"LP_Frightened",600);Items.Mote(e,"惊惧！",new Color(.9f,.5f,.5f));
   }
  }
 }
 // 林仙：树之绑定。离树 40 格以上（或不在同一张地图）移速、工作速度 ×0.5、心情 -8；树死了：重创 + 心情 -30，24 小时内没绑定新树就死。
 public class Gene_DryadTree : Gene {
  public const int None=int.MinValue;public Plant tree;public int lostTick=None,driftUntil=-1;int acc,flk;
  public override void PostAdd(){base.PostAdd();PactRaces.dryads++;}
  public void Bind(Plant t){
   tree=t;lostTick=None;driftUntil=-1;Items.Remove(pawn,"LP_DryadSevered");
   if(t?.Spawned==true)FleckMaker.Static(t.Position,t.Map,FleckDefOf.PsycastAreaEffect,1.4f);
   if(pawn.Faction?.IsPlayer==true)Messages.Message(pawn.LabelShort+"和一棵"+t?.LabelNoCount+"结下了绑定。",t,MessageTypeDefOf.PositiveEvent,false);
  }
  public void StartDrift(){if(driftUntil<0)driftUntil=Find.TickManager.TicksGame+PactRaces.DriftDays*GenDate.TicksPerDay;}
  public bool Drifting=>tree==null && lostTick==None && driftUntil>Find.TickManager.TicksGame;
  public bool Far=>tree!=null && pawn.Spawned && (tree.MapHeld!=pawn.Map || !tree.Position.InHorDistOf(pawn.Position,PactRaces.TreeLeash));
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if((flk+=delta)>=600){flk=0;if(tree!=null && tree.Spawned && pawn.Faction?.IsPlayer==true)FleckMaker.ThrowMetaIcon(tree.Position,tree.Map,FleckDefOf.Heart,.42f);}
   if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;if(!Active || pawn.Dead)return;Check(Find.TickManager.TicksGame);
  }
  public void Check(int now){
   if(tree!=null && (tree.Destroyed || !tree.Spawned)){
    tree=null;lostTick=now;Items.Give(pawn,"LP_DryadSevered");
    var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_DryadTreeDied");if(th!=null)pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(th);
    if(pawn.Faction?.IsPlayer==true)Find.LetterStack.ReceiveLetter("林仙的树死了",pawn.LabelShort+"的树没了。"+pawn.LabelShort+"像被抽掉了半条命：疼痛 +50%、意识 -30%。24 小时内不和一棵新的成年树绑定（能力“绑定此树”），"+pawn.gender.GetPronoun()+"会死。",LetterDefOf.ThreatBig,pawn);
   }
   if(tree==null && lostTick!=None && now-lostTick>=PactRaces.SeveredDays*GenDate.TicksPerDay){lostTick=None;Fiends.Defer(()=>{if(!pawn.Dead)pawn.Kill(null);});return;}
   if(tree==null && lostTick==None && driftUntil<0)StartDrift();
   Races124.Ensure(pawn,Items.Hediff("LP_DryadDistant"),Far);
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref tree,"lpTree");Scribe_Values.Look(ref lostTick,"lpLost",None);Scribe_Values.Look(ref driftUntil,"lpDrift",-1);if(Scribe.mode==LoadSaveMode.PostLoadInit)PactRaces.dryads++;}
 }
 public class ThoughtWorker_DryadDrifting : ThoughtWorker {protected override ThoughtState CurrentStateInternal(Pawn p){if(PactRaces.dryads==0)return false;var g=PactRaces.TreeGene(p);return g!=null && g.Drifting;}}
 // 调换儿：千面（复制一名看得见的人形外观 24 小时；只在开关时改显示，不在渲染时每帧算）。每 30 天要做回自己 1 天，否则心情 -6。
 public class Gene_ChangelingFaces : Gene {
  public bool on;public int until=-1,lastSelf=-1,selfHours;public string copied;
  Name origName;HairDef origHair;HeadTypeDef origHead;BodyTypeDef origBody;Color? origSkin;Color origHairColor;
  int acc;
  public override void PostAdd(){base.PostAdd();lastSelf=Find.TickManager?.TicksGame??0;}
  public void Copy(Pawn t){
   if(on || t?.story==null || pawn.story==null)return;
   origName=pawn.Name;origHair=pawn.story.hairDef;origHead=pawn.story.headType;origBody=pawn.story.bodyType;origSkin=pawn.story.skinColorOverride;origHairColor=pawn.story.HairColor;
   pawn.Name=CloneName(t.Name);pawn.story.hairDef=t.story.hairDef;pawn.story.headType=t.story.headType;
   if(t.story.bodyType!=null && (t.gender==pawn.gender || t.story.bodyType!=BodyTypeDefOf.Male && t.story.bodyType!=BodyTypeDefOf.Female))pawn.story.bodyType=t.story.bodyType;
   pawn.story.skinColorOverride=t.story.SkinColor;pawn.story.HairColor=t.story.HairColor;
   copied=t.LabelShort;on=true;until=Find.TickManager.TicksGame+GenDate.TicksPerDay;selfHours=0;
   pawn.Drawer?.renderer?.SetAllGraphicsDirty();
   Items.Give(pawn,"LP_ChangelingGuise");
   if(pawn.Spawned)for(int i=0;i<4;i++)FleckMaker.ThrowSmoke(pawn.DrawPos,pawn.Map,1f);
  }
  static Name CloneName(Name n){if(n is NameTriple t)return new NameTriple(t.First,t.Nick,t.Last);if(n is NameSingle s)return new NameSingle(s.Name,s.Numerical);return n;}
  public void Revert(string why){
   if(!on)return;on=false;until=-1;
   if(origName!=null)pawn.Name=origName;if(pawn.story!=null){if(origHair!=null)pawn.story.hairDef=origHair;if(origHead!=null)pawn.story.headType=origHead;if(origBody!=null)pawn.story.bodyType=origBody;pawn.story.skinColorOverride=origSkin;pawn.story.HairColor=origHairColor;}
   pawn.Drawer?.renderer?.SetAllGraphicsDirty();Items.Remove(pawn,"LP_ChangelingGuise");
   if(why!=null && pawn.Faction?.IsPlayer==true)Messages.Message(pawn.LabelShort+"的伪装（"+copied+"）解除了："+why+"。",pawn,MessageTypeDefOf.NeutralEvent,false);
   copied=null;
  }
  public bool LongDisguised=>Find.TickManager.TicksGame-lastSelf>PactRaces.DriftDays*GenDate.TicksPerDay;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;if(pawn.Dead)return;int now=Find.TickManager.TicksGame;
   if(on){selfHours=0;if(now>=until)Revert("过了 24 小时");else if(pawn.Spawned && SeenThrough())Revert("被看穿了");}
   else if(++selfHours>=24){selfHours=0;lastSelf=now;}
  }
  bool SeenThrough(){foreach(var c in GenRadial.RadialCellsAround(pawn.Position,AbyssRaces.SeerRadius,true)){if(!c.InBounds(pawn.Map))continue;var l=c.GetThingList(pawn.Map);for(int i=0;i<l.Count;i++)if(l[i] is Pawn p && p!=pawn && !p.Dead && !p.Downed && Fiends.Sees(p))return true;}return false;}
  public override void ExposeData(){
   base.ExposeData();Scribe_Values.Look(ref on,"lpOn");Scribe_Values.Look(ref until,"lpUntil",-1);Scribe_Values.Look(ref lastSelf,"lpLastSelf",-1);Scribe_Values.Look(ref selfHours,"lpSelfHours");Scribe_Values.Look(ref copied,"lpCopied");
   Scribe_Deep.Look(ref origName,"lpOrigName");Scribe_Defs.Look(ref origHair,"lpOrigHair");Scribe_Defs.Look(ref origHead,"lpOrigHead");Scribe_Defs.Look(ref origBody,"lpOrigBody");Scribe_Values.Look(ref origSkin,"lpOrigSkin");Scribe_Values.Look(ref origHairColor,"lpOrigHairColor");
  }
 }
 // 调换儿的“没画完的脸”：伪装时不画。
 public class PawnRenderNodeWorker_ChangelingFace : PawnRenderNodeWorker_FlipWhenCrawling {
  public override bool CanDrawNow(PawnRenderNode node,PawnDrawParms parms)=>base.CanDrawNow(node,parms) && PactRaces.Faces(parms.pawn)?.on!=true;
 }
 public class ThoughtWorker_ChangelingSelf : ThoughtWorker {protected override ThoughtState CurrentStateInternal(Pawn p){var g=PactRaces.Faces(p);return g!=null && g.LongDisguised;}}

 // ---------- 状态 ----------
 public class Hediff_HuntMarked : HediffWithComps {
  public Pawn hunter;
  public override string LabelInBrackets=>hunter==null?null:"猎手 "+hunter.LabelShort;
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref hunter,"lpHunter");}
 }
 // 阿姆尼祖：被攻击时攻击者智力豁免 DC 16，失败最高技能 -2 级 1 天（移除时还原）。
 public class Hediff_AmnizuDrain : HediffWithComps {
  public SkillDef skill;public int lost;
  public override string LabelInBrackets=>skill==null?null:skill.label+" -"+lost;
  public override void PostAdd(DamageInfo? dinfo){
   base.PostAdd(dinfo);var s=pawn.skills?.skills?.Where(x=>!x.TotallyDisabled).OrderByDescending(x=>x.Level).FirstOrDefault();if(s==null)return;
   skill=s.def;lost=Mathf.Min(2,s.Level);s.Level-=lost;
  }
  public override void PostRemoved(){base.PostRemoved();var s=skill==null?null:pawn.skills?.GetSkill(skill);if(s!=null)s.Level=Mathf.Min(20,s.Level+lost);}
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref skill,"lpSkill");Scribe_Values.Look(ref lost,"lpLost");}
 }
 // 红帽嗜血：近战命中叠层（每层 +3 伤害，最多 5 层），1 小时没命中清零（消失计时）。
 public class Hediff_RedcapBloodlust : HediffWithComps {public override string LabelInBrackets=>Mathf.RoundToInt(Severity)+"层";}
 // 狂猎 / 讨债者的首领：倒下时掉宗主代理人的真名卷。
 public class Hediff_PatronAgent : HediffWithComps {
  public LP_PatronDef patron;public override bool Visible=>false;
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref patron,"lpPatron");}
 }
 // 长子条款：18 岁时 50% 离开殖民地，回九狱。
 public class Hediff_HellsClaim : HediffWithComps {
  int acc;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<GenDate.TicksPerDay)return;acc=0;
   if(pawn.Dead || pawn.ageTracker==null || pawn.ageTracker.AgeBiologicalYears<18)return;
   var p=pawn;Fiends.Defer(()=>PactEvents.HellsClaimDue(p));
  }
 }
 // 九狱小魔鬼（魔宠）：照夸塞魔——不战斗时隐身、主人心灵敏感度 +10%；不加腐化，每 10 天一封“小魔鬼的情报”（一半是假的）。
 public class Hediff_ImpNature : HediffWithComps {
  public Pawn master;int acc,hour,intel=-1;
  public override bool Visible=>true;
  public override string LabelInBrackets=>master==null?null:"主人 "+master.LabelShort;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if(pawn.Dead || !pawn.Spawned)return;
   if((acc+=delta)>=60){acc=0;int now=Find.TickManager.TicksGame;bool fight=now-pawn.LastAttackTargetTick<600 || now-pawn.mindState.lastHarmTick<600;bool has=Items.Has(pawn,"LP_QuasitVeil");if(!fight && !has)Items.Give(pawn,"LP_QuasitVeil");else if(fight && has)Items.Remove(pawn,"LP_QuasitVeil");}
   if((hour+=delta)>=GenDate.TicksPerHour){hour=0;
    if(master==null && pawn.Faction?.IsPlayer==true)master=PactRaces_Familiar.Adopt(pawn);
    if(master!=null && !master.Dead)Races124.Ensure(master,Items.Hediff("LP_QuasitBond"),master.MapHeld==pawn.Map);
    int now=Find.TickManager.TicksGame;if(intel<0)intel=now+10*GenDate.TicksPerDay;
    if(master!=null && now>=intel){intel=now+10*GenDate.TicksPerDay;PactRaces_Familiar.Intel(pawn,master);}
   }
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref master,"lpMaster");Scribe_Values.Look(ref intel,"lpIntel",-1);}
 }
 // 小精灵：每天给主人或身边一名殖民者一种好效果 6 小时；每 5 天 30% 恶作剧（主人背包里一件非装备的东西掉到地上）。
 public class Hediff_PixieNature : HediffWithComps {
  public Pawn master;int acc,dust=-1,prank=-1;
  public override bool Visible=>true;
  public override string LabelInBrackets=>master==null?null:"主人 "+master.LabelShort;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if(pawn.Dead || !pawn.Spawned)return;if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;
   if(master==null && pawn.Faction?.IsPlayer==true)master=PactRaces_Familiar.Adopt(pawn);if(master==null || master.Dead)return;
   int now=Find.TickManager.TicksGame;if(dust<0)dust=now+GenDate.TicksPerDay;if(prank<0)prank=now+5*GenDate.TicksPerDay;
   if(now>=dust){dust=now+GenDate.TicksPerDay;PactRaces_Familiar.Dust(pawn,master);}
   if(now>=prank){prank=now+5*GenDate.TicksPerDay;if(PactRaces.Roll<.3f)PactRaces_Familiar.Prank(pawn,master);}
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref master,"lpMaster");Scribe_Values.Look(ref dust,"lpDust",-1);Scribe_Values.Look(ref prank,"lpPrank",-1);}
 }
 public static class PactRaces_Familiar {
  public static readonly string[] DustHediffs={"LP_PixieDust_Swift","LP_PixieDust_Diligent","LP_PixieDust_Joy","LP_PixieDust_Veil"};
  public static Pawn Adopt(Pawn fam){
   var m=TradeSession.playerNegotiator;if(m==null || m.Dead || m.MapHeld!=fam.MapHeld)m=fam.Map?.mapPawns.FreeColonistsSpawned.OrderBy(c=>c.Position.DistanceToSquared(fam.Position)).FirstOrDefault();
   if(m==null)return null;
   if(m.relations!=null && !m.relations.DirectRelationExists(PawnRelationDefOf.Bond,fam))m.relations.AddDirectRelation(PawnRelationDefOf.Bond,fam);
   if(fam.playerSettings!=null){fam.playerSettings.Master=m;fam.playerSettings.followDrafted=true;fam.playerSettings.followFieldwork=true;}
   return m;
  }
  public static string Dust(Pawn pixie,Pawn master){
   var who=new[]{master}.Concat(pixie.Map?.mapPawns.FreeColonistsSpawned.Where(c=>c!=master && c.Position.InHorDistOf(pixie.Position,8f))??Enumerable.Empty<Pawn>()).Where(c=>c!=null && !c.Dead && c.MapHeld==pixie.MapHeld).RandomElementWithFallback();
   if(who==null)return null;var h=DustHediffs.RandomElement();Items.Give(who,h);Items.Mote(who,"妖精粉尘",new Color(1f,.85f,1f));
   if(h=="LP_PixieDust_Joy"){var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_PixieDustJoy");if(th!=null)who.needs?.mood?.thoughts?.memories?.TryGainMemory(th);}
   return h;
  }
  public static Thing Prank(Pawn pixie,Pawn master){
   var inv=master?.inventory?.innerContainer;if(inv==null || inv.Count==0 || !master.Spawned)return null;var t=inv.RandomElement();
   if(inv.TryDrop(t,master.Position,master.Map,ThingPlaceMode.Near,out var dropped)){Messages.Message(pixie.LabelShort+"恶作剧：把"+master.LabelShort+"背包里的"+dropped.LabelShort+"扔到了地上。",dropped,MessageTypeDefOf.NeutralEvent,false);return dropped;}
   return null;
  }
  public static string Intel(Pawn imp,Pawn master){
   var map=master.MapHeld??Find.AnyPlayerHomeMap;if(map==null)return null;float pts=StorytellerUtility.DefaultThreatPointsNow(map);bool truth=PactRaces.Roll<.5f;
   if(!truth)pts*=Rand.Range(.4f,1.8f);
   var fac=Find.FactionManager.AllFactionsVisible.Where(f=>!f.IsPlayer && f.HostileTo(Faction.OfPlayer)).RandomElementWithFallback();
   string text=imp.LabelShort+"凑到"+master.LabelShort+"耳边嘀咕：下一次来打殖民地的，规模大概有 "+pts.ToString("0")+" 点"+(fac!=null?"，多半是"+fac.Name:"")+"。\n\n（小魔鬼的话一半是真的。这一次……谁知道呢。）";
   Find.LetterStack.ReceiveLetter("小魔鬼的情报",text,LetterDefOf.NeutralEvent,imp);
   return truth?"true":"false";
  }
 }
 // 狄瓦的“劝返”：对艾林魔束缚者每天最多一次，20% 记一次，累计 3 次救赎。
 public class InteractionWorker_DevaRedeem : InteractionWorker {
  public override float RandomSelectionWeight(Pawn initiator,Pawn recipient){
   if(!PactRaces.IsDeva(initiator) || !PactRaces.IsErinyes(recipient) || recipient.Faction?.IsPlayer!=true)return 0f;
   var h=recipient.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_RedeemProgress")) as Hediff_RedeemProgress;if(h!=null && h.day==GenDate.DaysPassed)return 0f;
   return 4f;
  }
  public override void Interacted(Pawn initiator,Pawn recipient,List<RulePackDef> extraSentencePacks,out string letterText,out string letterLabel,out LetterDef letterDef,out LookTargets lookTargets){
   letterText=null;letterLabel=null;letterDef=null;lookTargets=null;Redeem(recipient);
  }
  public static bool Redeem(Pawn recipient){
   var h=recipient.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_RedeemProgress")) as Hediff_RedeemProgress;
   if(h==null){h=(Hediff_RedeemProgress)Items.Give(recipient,"LP_RedeemProgress");if(h==null)return false;h.Severity=.01f;}
   h.day=GenDate.DaysPassed;if(PactRaces.Roll>=.2f)return false;
   h.Severity=Mathf.Round(h.Severity)+1f;if(h.Severity>=3f)PactRaces.Redeem(recipient);
   return true;
  }
 }
 public class Hediff_RedeemProgress : HediffWithComps {
  public int day=-1;public override string LabelInBrackets=>Mathf.RoundToInt(Severity)+" / 3";
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref day,"lpDay",-1);}
 }

 // ---------- 狂猎号角 ----------
 public class CompUseEffect_WildHuntHorn : CompUseEffect {
  public override void DoEffect(Pawn p){base.DoEffect(p);PactRaces_Horn.Blow(p);}
 }
 public static class PactRaces_Horn {
  static readonly string[] Canine={"Dog","Wolf","Husky","Retriever","Labrador","Worg","Warg","Hound","Fox","Coyote","Warg","BlinkDog"};
  public static bool IsCanine(Pawn p)=>p?.RaceProps?.Animal==true && Canine.Any(k=>p.def.defName.IndexOf(k,StringComparison.OrdinalIgnoreCase)>=0);
  public static int Blow(Pawn p){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var a in map.mapPawns.SpawnedColonyAnimals.ToList())if(IsCanine(a)){Items.Give(a,"LP_HornRush");n++;}
   foreach(var e in map.mapPawns.AllPawnsSpawned.ToList())if(e.RaceProps.Humanlike && !e.Dead && !e.Downed && e.HostileTo(Faction.OfPlayer) && PactRaces.Roll<.1f)e.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"狂猎号角",forced:true);
   for(int i=0;i<6;i++)FleckMaker.ThrowAirPuffUp(p.DrawPos,map);
   Messages.Message(p.LabelShort+"吹响了狂猎号角："+n+"只犬科动物冲劲十足。",p,MessageTypeDefOf.PositiveEvent,false);
   return n;
  }
 }

 [StaticConstructorOnStartup]
 public static class PactRacesStartup {
  static PactRacesStartup(){
   var fear=Legendary.FearImmuneHook;Legendary.FearImmuneHook=p=>fear!=null && fear(p) || Races124.Has(p,"LP_ErinyesResolve");
   var save=Legendary.SaveBonusHook;Legendary.SaveBonusHook=(p,s,a)=>(save?.Invoke(p,s,a)??0)+(a!=Adventurer.Str && a!=Adventurer.Dex && a!=Adventurer.Con && Races124.Has(p,"LP_SatyrMagicResist")?2:0);
   Items.ColdIronBaneHooks.Add(PactRaces.IsChangeling); // 调换儿的妖精血：寒铁 ×1.25（沿用精灵那一档）
   // 地狱铸炉：符文铁砧的所有配方
   var anvil=DefDatabase<ThingDef>.GetNamedSilentFail("LP_RuneAnvil");var forge=Pacts.ForgeDef;
   if(anvil!=null && forge!=null){foreach(var r in DefDatabase<RecipeDef>.AllDefsListForReading)if(r.recipeUsers!=null && r.recipeUsers.Contains(anvil) && !r.recipeUsers.Contains(forge))r.recipeUsers.Add(forge);forge.recipes=null;var cache=AccessTools.Field(typeof(ThingDef),"allRecipesCached");if(cache!=null)cache.SetValue(forge,null);}
  }
 }

 // ---------- Harmony ----------
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class PactRaceOnGenerated {static void Postfix(Pawn __result){if(__result!=null)PactRaces.OnGenerated(__result);}}
 // 出手方的倍率：天界对恶魔 / 魔鬼 / 亡灵 ×1.5、狂猎对猎物 +30%、艾林魔对追猎目标 +15%、妖精银弓对外来者 +20%、红帽嗜血 / 重踏、天界羽披光辉 +10%。没有出手的人第一行返回。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class PactOutgoingDamage {
  static void Prefix(Pawn __instance,ref DamageInfo __0){
   var inst=__0.Instigator as Pawn;if(inst==null)return;float f=1f;
   var w=__0.Weapon;
   if(w!=null && w==PactRaces.Bow && Outsiders.Is(__instance) && !PactRaces.Celestial(__instance))f*=PactRaces.BowFactor;
   var e=Outsiders.Of(inst);
   if(e!=null){
    if(e.plane==OutsiderPlane.Celestia && PactRaces.Fiendish(__instance))f*=PactRaces.CelestialFactor;
    else if(e.plane==OutsiderPlane.Feywild){
     if(Races124.Has(inst,"LP_WildHunterPrey") && Items.Has(__instance,"LP_WildHuntPrey"))f*=PactRaces.PreyFactor;
     var bl=inst.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_RedcapBloodlust"));if(bl!=null && __0.Def.isRanged==false)__0.SetAmount(__0.Amount+PactRaces.BloodlustPerStack*Mathf.RoundToInt(bl.Severity));
     if(Races124.Has(inst,"LP_RedcapStomp") && __instance.Downed)f*=PactRaces.StompFactor;
    }
    else if(e.plane==OutsiderPlane.Hells && Items.Has(__instance,"LP_HuntMarked")){var h=__instance.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_HuntMarked")) as Hediff_HuntMarked;if(h?.hunter==inst)f*=PactRaces.MarkFactor;}
   }
   if(__0.Def==Faith.Radiant && PactRaces.Mantle!=null && Items.Worn(inst,PactRaces.Mantle)!=null)f*=PactRaces.MantleRadiant;
   if(f!=1f)__0.SetAmount(__0.Amount*f);
  }
 }
 // 命中后的效果：骨魔尾刺（体质 DC 14 → 骨毒）、地狱钢叉（25% 地狱之伤）、阿姆尼祖（被打时攻击者智力 DC 16）、红帽嗜血叠层、冰狱奥术（冰霜伤害 +1 层寒气）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PostApplyDamage))]
 public static class PactAfterDamage {
  static void Postfix(Pawn __instance,DamageInfo __0,float __1){
   if(__1<=0f)return;var inst=__0.Instigator as Pawn;if(inst==null || inst==__instance)return;var v=__instance;
   if(__0.Weapon!=null && __0.Weapon==PactRaces.Pitchfork && !v.Dead && PactRaces.Roll<.25f)Items.Give(v,"LP_InfernalWound");
   var ei=Outsiders.Of(inst);
   if(ei!=null){
    if(ei.plane==OutsiderPlane.Hells && __0.Def==DamageDefOf.Stab && (__0.Weapon==null || __0.Weapon.race!=null) && Races124.Has(inst,"LP_BoneDevilTail") && !v.Dead && !Legendary.Save(v,Adventurer.Con,PactRaces.VenomDC,inst))Items.Give(v,"LP_BoneVenom");
    if(ei.plane==OutsiderPlane.Feywild && !__0.Def.isRanged && Races124.Has(inst,"LP_RedcapBloodlustGene")){var h=Items.Give(inst,"LP_RedcapBloodlust");if(h!=null)h.Severity=Mathf.Min(PactRaces.BloodlustMax,Mathf.Round(h.Severity)+1f);}
   }
   var ev=Outsiders.Of(v);
   if(ev!=null && ev.plane==OutsiderPlane.Hells && !v.Dead && inst.RaceProps.Humanlike && Races124.Has(v,"LP_AmnizuMind") && !Items.Has(inst,"LP_AmnizuDrain") && !Legendary.Save(inst,Adventurer.Int,PactRaces.AmnizuDC,v))Items.Give(inst,"LP_AmnizuDrain");
   if(PactBoons.iceArcana>0 && !v.Dead && (__0.Def==DamageDefOf.Frostbite || __0.Def.defName.IndexOf("Frost",StringComparison.Ordinal)>=0 || __0.Def.defName.IndexOf("Cold",StringComparison.Ordinal)>=0 || __0.Def.defName.IndexOf("Ice",StringComparison.Ordinal)>=0) && Items.Has(inst,"LP_Boon_IceArcana"))Fiends.Chill(v,1);
  }
 }
 // 萨特的羊蹄：不能穿鞋、靴（盖住脚的衣物）。
 [HarmonyPatch(typeof(ApparelUtility),nameof(ApparelUtility.HasPartsToWear),new Type[]{typeof(Pawn),typeof(ThingDef)})]
 public static class SatyrNoShoes {
  static void Postfix(Pawn p,ThingDef apparel,ref bool __result){
   if(!__result || apparel?.apparel?.bodyPartGroups==null || p?.genes==null)return;
   if(feet==null)feet=DefDatabase<BodyPartGroupDef>.GetNamedSilentFail("Feet");
   if(feet==null || !apparel.apparel.bodyPartGroups.Contains(feet) || !PactRaces.IsSatyr(p))return;
   __result=false;
  }
  static BodyPartGroupDef feet;
 }
 // 地狱之伤：圣水、信仰治疗能止住。
 [HarmonyPatch(typeof(CompUseEffect_BlessedWater),nameof(CompUseEffect_BlessedWater.DoEffect))]
 public static class InfernalWoundBlessedWater {static void Postfix(Pawn usedBy){Items.Remove(usedBy,"LP_InfernalWound");}}
 [HarmonyPatch(typeof(FaithRaces),nameof(FaithRaces.HealingHands))]
 public static class InfernalWoundHealingHands {static void Postfix(Pawn t){if(t!=null)Items.Remove(t,"LP_InfernalWound");}}
}
