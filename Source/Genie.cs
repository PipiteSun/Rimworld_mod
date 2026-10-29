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
 // 1.24.0 巨灵：神灯 → 擦灯召出巨灵（殖民者）→ 每60天一次许愿 → 巨灵“死去”回到灯里，暗淡15天后能再擦出同一个巨灵。
 public static class Genie {
  public const float CooldownFactor=.5f;
  public const int DimDays=15,LeaveTicks=GenDate.TicksPerDay,FatigueDays=15,WishCooldownDays=60,Wealth=2000,PeaceDays=15,ClearDays=5,InsightLevels=2,RubTicks=180;
  public static string Prefix(string element){switch(element){case "Fire":return "伊弗利特";case "Water":return "玛瑞德";case "Earth":return "道";case "Air":return "金尼";}return "巨灵";}
  public static XenotypeDef Xenotype=>Races124.Xeno("LP_Genie");
  public static ThingDef LampDef=>Items.Thing("LP_WonderLamp");
  public static Gene_GenieCore Core(Pawn p)=>p?.genes?.GetFirstGeneOfType<Gene_GenieCore>();
  public static bool Fatigued(Pawn p)=>Races124.IsGenie(p) && Items.Has(p,"LP_GenieFatigue");
  public static readonly string[] WishLabels={"丰收","和平","痊愈","财富","晴空","顿悟"};

  // ---------- 召唤 ----------
  public static Pawn Summon(WonderLamp lamp,Pawn rubber,string element=null){
   if(lamp==null || Xenotype==null)return null;
   element=element??Genasi.Elements.RandomElement();
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:Xenotype,allowDowned:false,allowDead:false,mustBeCapableOfViolence:false,fixedBiologicalAge:30,fixedChronologicalAge:Rand.Range(300,3000),forceNoBackstory:false));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_MissingPart || h is Hediff_AddedPart).ToList()){if(h is Hediff_Injury)p.health.RemoveHediff(h);else if(h.Part!=null)p.health.RestorePart(h.Part);}
   SetElement(p,element);
   if(p.Name is NameTriple n)p.Name=new NameTriple(n.First,Prefix(element),n.Last);
   var map=lamp.MapHeld;var near=lamp.PositionHeld;if(rubber?.Spawned==true){map=rubber.Map;near=rubber.Position;}
   if(map!=null)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(near,map,3),map);
   Bind(lamp,p);
   if(map!=null){for(int i=0;i<8;i++)FleckMaker.ThrowSmoke(p.DrawPos,map,2f);Find.LetterStack.ReceiveLetter("巨灵现身","神灯里升起一缕青烟，凝成了"+Genasi.Label(element)+"之巨灵"+p.LabelShort+"（"+Prefix(element)+"）。\n\n巨灵会留下来做你的殖民者，每60天能实现一次愿望。神灯必须留在你手里：神灯被卖掉、偷走或毁掉，巨灵一天后就会化烟离去。巨灵被击倒或杀死时不会死去，而是回到灯里沉睡15天（灯里与外界隔绝，不会受伤挨饿）；暗淡时也可以花"+ReviveSilver+"白银立刻唤醒。",LetterDefOf.PositiveEvent,p);}
   return p;
  }
  // 血脉作为异种基因加入，肤色随血脉（复用元素裔的肤色基因）。
  public static void SetElement(Pawn p,string element){
   foreach(var e in Genasi.Elements){foreach(var d in new[]{Genasi.Bloodline(e),Genasi.Skin(e)}){var g=d==null?null:p.genes.GetGene(d);if(g!=null)p.genes.RemoveGene(g);}}
   p.genes.AddGene(Genasi.Bloodline(element),true);p.genes.AddGene(Genasi.Skin(element),true);
   p.Drawer?.renderer?.SetAllGraphicsDirty();
  }
  // 1.59.0 热修：野性回归会把巨灵变成野人（没有派系），这里改回玩家的殖民者。
  public static void Reclaim(Pawn p){
   if(p==null || p.Dead || p.Faction==Faction.OfPlayer)return;
   if(p.IsPrisoner)p.guest?.SetGuestStatus(null);
   if(p.kindDef==PawnKindDefOf.WildMan)p.ChangeKind(PawnKindDefOf.Colonist);
   p.SetFaction(Faction.OfPlayer);
   p.mindState?.mentalStateHandler?.Reset();
  }
  public static void Bind(WonderLamp lamp,Pawn p){var core=Core(p);if(core!=null){core.lampId=lamp.ThingID;core.leaveTick=-1;}lamp.genie=p;lamp.genieName=p.LabelShort;}

  // ---------- 神灯在不在玩家手里 ----------
  public static IEnumerable<WonderLamp> AllLamps(){
   if(LampDef==null || Find.Maps==null)yield break;
   foreach(var map in Find.Maps){
    foreach(var t in map.listerThings.ThingsOfDef(LampDef))if(t is WonderLamp l)yield return l;
    foreach(var p in map.mapPawns.AllPawnsSpawned)foreach(var l in Carried(p))yield return l;
   }
   foreach(var c in Find.WorldObjects.Caravans)foreach(var p in c.PawnsListForReading)foreach(var l in Carried(p))yield return l;
  }
  static IEnumerable<WonderLamp> Carried(Pawn p){
   if(p.carryTracker?.CarriedThing is WonderLamp c)yield return c;
   var inv=p.inventory?.innerContainer;if(inv!=null)foreach(var t in inv)if(t is WonderLamp l)yield return l;
  }
  public static WonderLamp FindLamp(string id)=>id==null?null:AllLamps().FirstOrDefault(l=>l.ThingID==id);
  public static bool PlayerHolds(WonderLamp l){
   if(l==null || l.Destroyed)return false;
   if(l.Spawned)return l.Map.IsPlayerHome || l.Map.mapPawns.FreeColonistsSpawnedCount>0;
   var holder=(l.ParentHolder as Pawn_InventoryTracker)?.pawn ?? (l.ParentHolder as Pawn_CarryTracker)?.pawn;
   return holder!=null && holder.Faction==Faction.OfPlayer;
  }

  // ---------- 回到灯里 / 化烟离去 ----------
  public static void Heal(Pawn p){
   var hs=p.health.hediffSet;
   foreach(var h in hs.hediffs.ToList()){
    if(h.def.defName.StartsWith("LP_"))continue;
    if(h is Hediff_Injury || h.def==HediffDefOf.BloodLoss || (h.def.isBad && (h.def.makesSickThought || h.TryGetComp<HediffComp_Immunizable>()!=null || h.def.lethalSeverity>0) && !(h is Hediff_Addiction)))p.health.RemoveHediff(h);
   }
   foreach(var m in hs.GetMissingPartsCommonAncestors().ToList())p.health.RestorePart(m.Part);
  }
  // 1.52.0 热修：灯里的巨灵玩家碰不到，回灯和擦出时都恢复到最佳——伤病全清（保留 LP_ 状态与成瘾）、需求拉满、负面记忆和精神状态清掉。
  public static void Refresh(Pawn p){
   if(p==null || p.Dead || p.health==null)return;
   Heal(p);
   foreach(var h in p.health.hediffSet.hediffs.ToList())if(h.def.isBad && !h.def.defName.StartsWith("LP_") && !(h is Hediff_Addiction) && !(h is Hediff_MissingPart))p.health.RemoveHediff(h);
   var mem=p.needs?.mood?.thoughts?.memories;
   if(mem!=null)foreach(var t in mem.Memories.ToList()){float o;try{o=t.MoodOffset();}catch{continue;}if(o<0)mem.RemoveMemory(t);}
   if(p.needs!=null)foreach(var n in p.needs.AllNeeds)n.CurLevel=n.MaxLevel;
   p.mindState?.mentalStateHandler?.Reset();
  }
  // 巨灵倒下（被杀）时：伤势清空，延到下一刻回灯里；神灯暗淡15天。灯不在手里时返回 false（照常死去、不留尸体）。
  public static bool CatchDeath(Pawn p){
   // 1.58.3 热修：已经在灯里的巨灵不会死，也不会重新开始暗淡计时。
   if(p.ParentHolder is WonderLamp){Refresh(p);return true;}
   var lamp=FindLamp(Core(p)?.lampId);if(lamp==null || !PlayerHolds(lamp))return false;
   Heal(p);Fiends.Defer(()=>ReturnToLamp(p,lamp));
   return true;
  }
  // 1.58.3 热修：巨灵被击倒也回灯里（灯不在手里时照常倒地）。
  public static void CatchDowned(Pawn p){
   if(p==null || p.Dead || p.ParentHolder is WonderLamp)return;
   var lamp=FindLamp(Core(p)?.lampId);if(lamp==null || !PlayerHolds(lamp))return;
   Fiends.Defer(()=>{if(!p.Dead && !p.Destroyed && p.Downed && !(p.ParentHolder is WonderLamp))ReturnToLamp(p,lamp);});
  }
  // 1.58.3 热修：暗淡时花白银立刻唤醒。白银取这张地图上没被禁止的白银。
  public const int ReviveSilver=1000;
  public static int SilverOn(Map map)=>map==null?0:map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t=>!t.IsForbidden(Faction.OfPlayer)).Sum(t=>t.stackCount);
  public static bool TakeSilver(Map map,int count){
   if(SilverOn(map)<count)return false;
   foreach(var t in map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t=>!t.IsForbidden(Faction.OfPlayer)).ToList()){
    if(count<=0)break;int n=Math.Min(count,t.stackCount);count-=n;
    if(n>=t.stackCount)t.Destroy();else t.SplitOff(n).Destroy();
   }
   return true;
  }
  public static bool ReturnToLamp(Pawn p,WonderLamp lamp){
   if(p==null || lamp==null || lamp.Destroyed || p.Destroyed)return false;
   if(p.ParentHolder==lamp)return true;
   var map=p.MapHeld;var cell=p.PositionHeld;
   p.jobs?.StopAll();
   if(p.GetCaravan() is Caravan c)c.RemovePawn(p);
   if(p.Spawned)p.DeSpawn();
   if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);
   if(!lamp.inner.TryAddOrTransfer(p,false))return false;
   Refresh(p);
   lamp.genie=null;lamp.dimUntil=Find.TickManager.TicksGame+DimDays*GenDate.TicksPerDay;
   if(map!=null){for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(cell.ToVector3Shifted(),map,2f);Messages.Message(p.LabelShort+"化作一缕青烟回到了神灯里。神灯会暗淡"+DimDays+"天。",lamp,MessageTypeDefOf.NeutralEvent,false);}
   return true;
  }
  public static void Depart(Pawn p,string reason){
   if(p==null)return;
   // 死者先处理尸体（死去的角色可能已经被标记为 Destroyed，不能先按 Destroyed 退出）。
   if(p.Dead){var corpse=p.Corpse;if(corpse==null || corpse.Destroyed)return;var cell=corpse.PositionHeld;var m=corpse.MapHeld;if(m!=null)for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(cell.ToVector3Shifted(),m,2f);Messages.Message(p.LabelShort+reason,new TargetInfo(cell,m),MessageTypeDefOf.NegativeEvent,false);corpse.Destroy();return;}
   if(p.Destroyed)return;
   if(p.Spawned){for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(p.DrawPos,p.Map,2f);}
   Messages.Message(p.LabelShort+reason,new TargetInfo(p.PositionHeld,p.MapHeld),MessageTypeDefOf.NegativeEvent,false);
   if(p.GetCaravan() is Caravan c)c.RemovePawn(p);
   p.Destroy(DestroyMode.Vanish);
  }
  // ---------- 许愿 ----------
  public static void OpenWishDialog(Pawn genie,RimWorld.Ability ability){
   var node=new DiaNode(genie.LabelShort+"双臂交叉，悬在半空：\n\n“说出你的愿望吧，凡人。一个就够了。”\n\n许愿后巨灵会倦怠15天（工作速度减半、血脉能力不可用、心情-5）。");
   for(int i=0;i<WishLabels.Length;i++){int k=i;node.options.Add(new DiaOption(WishLabels[i]+"："+WishText(i)){action=()=>Grant(genie,k),resolveTree=true});}
   node.options.Add(new DiaOption("让我更强：一名殖民者的一项属性 +1（每人一生一次，可以超过上限 1 点）"){action=()=>Growth.OpenStrongerDialog(genie,ability),resolveTree=true}); // 1.58.3
   if(CombatGifts.On)node.options.Add(new DiaOption("让我更强 · 武艺：一名殖民者获得一个随机武艺天赋（已有则直接升一阶）"){action=()=>CombatGifts.OpenWishDialog(genie,ability),resolveTree=true}); // 1.66.0
   node.options.Add(new DiaOption("再想想"){action=()=>ability?.ResetCooldown(),resolveTree=true});
   Find.WindowStack.Add(new Dialog_NodeTree(node,true,false,"巨灵的愿望"));
  }
  public static string WishText(int i){switch(i){case 0:return "地图上所有作物立刻成熟";case 1:return "15天内不会有敌对袭击";case 2:return "治好所有殖民者的伤口和疾病";case 3:return "巨灵脚下出现2000白银";case 4:return "天气放晴并维持5天";case 5:return "每个殖民者随机一项技能+2级";}return "";}
  // 执行愿望 index（0～5）并让巨灵倦怠；返回影响到的数量（作物、殖民者、白银等）。
  public static int Grant(Pawn genie,int index){
   var map=genie?.MapHeld;if(map==null)return 0;int n=0;
   switch(index){
    case 0:
     foreach(var plant in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>().ToList()){
      if(!plant.def.plant.Sowable || plant.Growth>=1f)continue;
      if(!(plant.Position.GetZone(map) is Zone_Growing) && !(plant.Position.GetEdifice(map) is Building_PlantGrower))continue;
      plant.Growth=1f;plant.DirtyMapMesh(map);n++;
     }
     break;
    case 1:
     var s=Races124.State;if(s!=null){s.peaceUntil[map.uniqueID]=Find.TickManager.TicksGame+PeaceDays*GenDate.TicksPerDay;n=1;}
     Find.LetterStack.ReceiveLetter("巨灵的屏障","一道看不见的屏障笼罩了这片土地。接下来"+PeaceDays+"天里，敌人的袭击都到不了这里。",LetterDefOf.PositiveEvent,genie);
     break;
    case 2:foreach(var p in map.mapPawns.FreeColonistsSpawned.ToList()){int before=p.health.hediffSet.hediffs.Count;Heal(p);if(p.health.hediffSet.hediffs.Count<before)n++;}break;
    case 3:{var silver=ThingMaker.MakeThing(ThingDefOf.Silver);silver.stackCount=Wealth;GenPlace.TryPlaceThing(silver,genie.PositionHeld,map,ThingPlaceMode.Near);n=Wealth;break;}
    case 4:{var def=DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_GenieClearSkies");if(def!=null){map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(def,ClearDays*GenDate.TicksPerDay));n=1;}var clear=DefDatabase<WeatherDef>.GetNamedSilentFail("Clear");if(clear!=null)map.weatherManager.TransitionTo(clear);break;}
    case 5:
     foreach(var p in map.mapPawns.FreeColonistsSpawned.ToList()){
      var skills=p.skills?.skills?.Where(sk=>!sk.TotallyDisabled && sk.Level<SkillRecord.MaxLevel).ToList();if(skills.NullOrEmpty())continue;
      var sk2=skills.RandomElement();sk2.Level=Math.Min(SkillRecord.MaxLevel,sk2.Level+InsightLevels);n++;
     }
     break;
   }
   Items.Give(genie,"LP_GenieFatigue",FatigueDays*GenDate.TicksPerDay);
   Messages.Message(genie.LabelShort+"实现了愿望："+WishLabels[Mathf.Clamp(index,0,WishLabels.Length-1)]+"。",genie,MessageTypeDefOf.PositiveEvent,false);
   return n;
  }
 }

 public class Gene_GenieCore : Gene {
  public string lampId;public int leaveTick=-1;int ticks;
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<250)return;ticks=0;Check();}
  public void Check(){
   if(pawn==null || pawn.Dead || pawn.Destroyed)return;
   // 1.59.0 热修：野性回归（没有派系、不是囚犯）不算被带走——留在殖民地，恢复成殖民者。
   if(pawn.Faction==null && !pawn.IsPrisoner && pawn.Spawned){Genie.Reclaim(pawn);return;}
   // 被俘、被别的派系带走：回到灯里（灯也不在手里就直接离去）。
   if(pawn.Faction!=Faction.OfPlayer || pawn.IsPrisoner){var l=Genie.FindLamp(lampId);if(l==null || !Genie.ReturnToLamp(pawn,l))Genie.Depart(pawn,"化烟离去了。");return;}
   var lamp=Genie.FindLamp(lampId);
   if(Genie.PlayerHolds(lamp)){leaveTick=-1;return;}
   int now=Find.TickManager.TicksGame;
   if(leaveTick<0){leaveTick=now+Genie.LeaveTicks;Messages.Message(pawn.LabelShort+"感到神灯离开了你的掌控。一天之内找不回来，巨灵就会离去。",pawn,MessageTypeDefOf.ThreatSmall,false);return;}
   if(now>=leaveTick){if(lamp!=null && lamp.genie==pawn)lamp.genie=null;Genie.Depart(pawn,"失去了神灯的束缚，化烟离去。");}
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref lampId,"genieLampId");Scribe_Values.Look(ref leaveTick,"genieLeaveTick",-1);}
 }
 public class Hediff_GenieFatigue : HediffWithComps {}

 // ---------- 神灯 ----------
 public enum LampState {Asleep,Bound,Dim}
 public class WonderLamp : ThingWithComps,IThingHolder {
  // genie：在外面的巨灵（引用）；巨灵离去、被毁或死去后神灯自动回到“沉睡”。inner：回到灯里沉睡的巨灵。
  public ThingOwner<Pawn> inner;public int dimUntil=-1;public Pawn genie;public string genieName;
  static readonly Dictionary<LampState,Graphic> graphics=new Dictionary<LampState,Graphic>();
  public WonderLamp(){inner=new ThingOwner<Pawn>(this,true,LookMode.Deep,false);}
  public Pawn Stored=>inner.Count>0?inner[0]:null;
  public bool GenieOut=>genie!=null && !genie.Destroyed && !genie.Dead;
  public LampState State=>Stored!=null?(Find.TickManager.TicksGame<dimUntil?LampState.Dim:LampState.Asleep):GenieOut?LampState.Bound:LampState.Asleep;
  public int DimTicksLeft=>Math.Max(0,dimUntil-Find.TickManager.TicksGame);
  public override Graphic Graphic{get{
   var s=State;if(!graphics.TryGetValue(s,out var g)){g=GraphicDatabase.Get<Graphic_Single>("Races124/Items/WonderLamp_"+s,ShaderDatabase.Cutout,def.graphicData.drawSize,Color.white);graphics[s]=g;}
   return g;
  }}
  public static string StateLabel(LampState s){switch(s){case LampState.Asleep:return "沉睡";case LampState.Bound:return "有主";case LampState.Dim:return "暗淡";}return "";}
  public override string GetInspectString(){
   var s=State;string text="状态："+StateLabel(s);
   if(s==LampState.Bound)text+="（"+(genieName??"巨灵")+"在外）";
   if(s==LampState.Dim)text+="，还要"+(DimTicksLeft/(float)GenDate.TicksPerDay).ToString("0.0")+"天才能再擦出"+Stored.LabelShort;
   if(s==LampState.Asleep && Stored!=null)text+="（"+Stored.LabelShort+"在灯里，可以擦出来）";
   var b=base.GetInspectString();return b.NullOrEmpty()?text:b+"\n"+text;
  }
  // 擦灯的结果（右键→擦拭神灯，3秒）。
  public string Rub(Pawn by){
   switch(State){
    case LampState.Bound:return "灯身微温，巨灵"+(genieName??"")+"已经在外面了，什么也没有发生。";
    case LampState.Dim:return "神灯还很暗淡，"+(DimTicksLeft/(float)GenDate.TicksPerDay).ToString("0.0")+"天后才能把"+Stored.LabelShort+"擦出来。";
   }
   if(Stored!=null){
    var p=Stored;var map=by?.MapHeld??MapHeld;var near=by?.PositionHeld??PositionHeld;if(map==null)return "这里放不出巨灵。";
    inner.Remove(p);Genie.Refresh(p);GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(near,map,3),map);p.health.CheckForStateChange(null,null);Genie.Reclaim(p);
    Genie.Bind(this,p);dimUntil=-1;for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(p.DrawPos,map,2f);
    return p.LabelShort+"从神灯里重新现身了。";
   }
   var g=Genie.Summon(this,by);return g==null?"神灯毫无反应。":g.LabelShort+"从神灯里现身了！";
  }
  public string Revive(){
   if(State!=LampState.Dim)return "神灯没有暗淡，不需要唤醒。";
   var map=MapHeld;if(map==null)return "这里放不出巨灵。";
   if(!Genie.TakeSilver(map,Genie.ReviveSilver))return "白银不够：需要"+Genie.ReviveSilver+"，这张地图上能用的只有"+Genie.SilverOn(map)+"。";
   dimUntil=Find.TickManager.TicksGame-1;
   var holder=(ParentHolder as Pawn_InventoryTracker)?.pawn ?? (ParentHolder as Pawn_CarryTracker)?.pawn;
   return "花了"+Genie.ReviveSilver+"白银唤醒神灯："+Rub(holder);
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(State!=LampState.Dim)yield break;
   var cmd=new Command_Action{defaultLabel="花"+Genie.ReviveSilver+"银唤醒",defaultDesc="立刻结束暗淡，把"+Stored.LabelShort+"从神灯里召回（从这张地图上没被禁止的白银里扣"+Genie.ReviveSilver+"）。",icon=ThingDefOf.Silver.uiIcon,action=()=>Messages.Message(Revive(),this,MessageTypeDefOf.NeutralEvent,false)};
   if(MapHeld==null)cmd.Disable("神灯不在地图上。");else{int have=Genie.SilverOn(MapHeld);if(have<Genie.ReviveSilver)cmd.Disable("白银不够：需要"+Genie.ReviveSilver+"，这张地图上能用的只有"+have+"。");}
   yield return cmd;
  }
  public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn){
   foreach(var o in base.GetFloatMenuOptions(selPawn))yield return o;
   var job=DefDatabase<JobDef>.GetNamedSilentFail("LP_RubLamp");if(job==null)yield break;
   if(!selPawn.CanReach(this,PathEndMode.Touch,Danger.Deadly)){yield return new FloatMenuOption("擦拭神灯（无法到达）",null);yield break;}
   yield return new FloatMenuOption("擦拭神灯",()=>selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job,this),JobTag.Misc));
  }
  public ThingOwner GetDirectlyHeldThings()=>inner;
  public void GetChildHolders(List<IThingHolder> outChildren){ThingOwnerUtility.AppendThingHoldersFromThings(outChildren,inner);}
  public override void ExposeData(){
   base.ExposeData();if(Scribe.mode==LoadSaveMode.Saving && !GenieOut)genie=null;
   Scribe_Deep.Look(ref inner,"genie",this);Scribe_Values.Look(ref dimUntil,"dimUntil",-1);Scribe_References.Look(ref genie,"boundGenie");Scribe_Values.Look(ref genieName,"genieName");
   if(Scribe.mode==LoadSaveMode.PostLoadInit && inner==null)inner=new ThingOwner<Pawn>(this,true,LookMode.Deep,false);
  }
 }
 public class JobDriver_RubLamp : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
   yield return Toils_General.Wait(Genie.RubTicks).WithProgressBarToilDelay(TargetIndex.A).FailOnCannotTouch(TargetIndex.A,PathEndMode.Touch);
   yield return Toils_General.Do(()=>{if(job.targetA.Thing is WonderLamp l)Messages.Message(l.Rub(pawn),l,MessageTypeDefOf.NeutralEvent,false);});
  }
 }

 // 巨灵不会真正死去：灯在手里就回灯里；否则照常死去但不留尸体。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class GenieDeath {
  static bool Prefix(Pawn __instance){return __instance.Dead || !Races124.IsGenie(__instance) || !Genie.CatchDeath(__instance);}
  // 死者的基因不算“激活”，这里按基因是否存在判断。
  static void Postfix(Pawn __instance){if(__instance.Dead && Genie.Core(__instance)!=null){var p=__instance;Fiends.Defer(()=>{if(p.Corpse!=null && !p.Corpse.Destroyed)Genie.Depart(p,"的身体化作青烟散去了。");});}}
 }
 // 1.58.3 热修：灯里的巨灵与外界隔绝——不跑 tick（不冻伤、不挨饿、不生病），擦出来前一直是回灯时的满状态。
 public static class GenieLampFreeze {public static bool Runs(Pawn p)=>p.Spawned || !(p.ParentHolder is WonderLamp);}
 [HarmonyPatch(typeof(Pawn),"Tick")] public static class GenieLampFreezeTick {static bool Prefix(Pawn __instance)=>GenieLampFreeze.Runs(__instance);}
 [HarmonyPatch(typeof(Pawn),"TickInterval")] public static class GenieLampFreezeInterval {static bool Prefix(Pawn __instance)=>GenieLampFreeze.Runs(__instance);}
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.TickRare))] public static class GenieLampFreezeRare {static bool Prefix(Pawn __instance)=>GenieLampFreeze.Runs(__instance);}
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class GenieDowned {static void Postfix(Pawn ___pawn){if(___pawn!=null && !___pawn.Dead && Races124.IsGenie(___pawn))Genie.CatchDowned(___pawn);}}
 // 1.59.0 热修：巨灵不会精神崩溃成野性回归（会换成别的崩溃）。
 [HarmonyPatch(typeof(MentalBreakWorker_RunWild),nameof(MentalBreakWorker_RunWild.BreakCanOccur))]
 public static class GenieNoRunWild {static void Postfix(Pawn pawn,ref bool __result){if(__result && Races124.IsGenie(pawn))__result=false;}}
 // 巨灵的屏障：和平愿望期间，敌对袭击不会落到这张地图上。
 public static class GeniePeace {
  public static bool Blocks(IncidentWorker w,IncidentParms parms)=>w is IncidentWorker_RaidEnemy && parms?.target is Map m && Races124.State?.Peaceful(m)==true && (parms.faction==null || parms.faction.HostileTo(Faction.OfPlayer));
 }
 [HarmonyPatch(typeof(IncidentWorker),nameof(IncidentWorker.CanFireNow))]
 public static class GeniePeaceCanFire {static void Postfix(IncidentWorker __instance,IncidentParms parms,ref bool __result){if(__result && GeniePeace.Blocks(__instance,parms))__result=false;}}
 [HarmonyPatch(typeof(IncidentWorker),nameof(IncidentWorker.TryExecute))]
 public static class GeniePeaceExecute {
  static bool Prefix(IncidentWorker __instance,IncidentParms parms,ref bool __result){
   if(!GeniePeace.Blocks(__instance,parms))return true;
   Messages.Message("巨灵的屏障挡住了一场袭击。",MessageTypeDefOf.PositiveEvent,false);__result=false;return false;
  }
 }
}
