using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 三个种族开局共用：在开局人物附近找一块空地直接放下建筑。
 public static class StartingBuildings {
  public static Thing Place(ThingDef def,ThingDef stuff,IntVec3 near,Map map){
   var rot=Rot4.North;
   foreach(var c in GenRadial.RadialCellsAround(near,40f,false)){
    if(!c.InBounds(map))continue;
    var rect=GenAdj.OccupiedRect(c,rot,def.size).ExpandedBy(1);
    if(!rect.InBounds(map))continue;
    if(def.hasInteractionCell){var ic=ThingUtility.InteractionCellWhenAt(def,c,rot,map);if(!ic.InBounds(map) || !ic.Standable(map) || ic.Fogged(map))continue;}
    if(!rect.Cells.All(x=>x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map)==null && x.GetFirstPawn(map)==null && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth)))continue;
    if(!GenConstruct.CanBuildOnTerrain(def,c,map,rot))continue;
    var thing=ThingMaker.MakeThing(def,stuff);thing.SetFactionDirect(Faction.OfPlayer);
    GenSpawn.Spawn(thing,c,map,rot,WipeMode.Vanish);return thing;
   }
   Messages.Message("开局附近没有足够的空地放置"+def.label+"，请自行建造。",MessageTypeDefOf.CautionInput,false);
   return null;
  }
  public static IntVec3 Center(Map map){var pawns=map.mapPawns.FreeColonistsSpawned;return pawns.Count>0?pawns[0].Position:map.Center;}
 }

 // 巫妖开局：第一位开局角色直接完成巫妖仪式，带命匣、仪式台与死灵造物。
 public class ScenPart_LichAscension : ScenPart {
  public int level=3,skeletons=2,golems=1;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref level,"level",3);Scribe_Values.Look(ref skeletons,"skeletons",2);Scribe_Values.Look(ref golems,"golems",1);}
  public override string Summary(Scenario scen)=>"开局人物页的第一位角色已是巫妖：命匣"+level+"级，统御"+skeletons+"具骷髅仆役"+(golems>0?"和"+golems+"具血肉巨像":"")+"，附近有一座死灵仪式台。";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(level<1)yield return "level must be at least 1";if(skeletons<0 || golems<0)yield return "construct counts must not be negative";}
  public override void PostGameStart(){
   var map=Find.AnyPlayerHomeMap;if(map==null)return;
   var lich=PickLich(map);if(lich==null){Log.Warning("[DND边缘大陆] 巫妖开局：找不到可转化的开局角色。");return;}
   Apply(lich,level,skeletons,golems);
  }
  // 优先人物页第一位；若其心灵失聪，改选心灵敏感度最高的人。
  public static Pawn PickLich(Map map){
   var candidates=map.mapPawns.FreeColonistsSpawned.Where(p=>p.genes!=null && p.RaceProps.Humanlike && !Necro.Undead(p)).ToList();
   var first=Find.GameInitData?.startingAndOptionalPawns?.FirstOrDefault(p=>candidates.Contains(p));
   if(first!=null && first.GetStatValue(StatDefOf.PsychicSensitivity)>0)return first;
   return candidates.OrderByDescending(p=>p.GetStatValue(StatDefOf.PsychicSensitivity)).FirstOrDefault();
  }
  public static Phylactery Apply(Pawn lich,int level,int skeletons,int golems){
   var map=lich.Map;
   var box=Rituals.Ascend(lich);
   box.level=Math.Max(1,Math.Min(level,LichMod.Settings.levelCap));
   GenPlace.TryPlaceThing(box,lich.Position,map,ThingPlaceMode.Near);
   var h=Necro.Lich(lich);h.SyncSpells();lich.psychicEntropy?.OffsetPsyfocusDirectly(1);
   if(StartingBuildings.Place(Necro.Def<ThingDef>("Altar"),null,lich.Position,map) is Building_Altar altar)altar.selected=box;
   for(int i=0;i<skeletons;i++)Constructs.Create("Skeleton",lich,CellFinder.RandomClosewalkCellNear(lich.Position,map,5),map);
   for(int i=0;i<golems;i++)Constructs.Create("Golem",lich,CellFinder.RandomClosewalkCellNear(lich.Position,map,5),map);
   return box;
  }
 }

 // 火巨人开局：带余火的熔火锻炉，每个火巨人穿上锻工外衣，最擅长近战的人拿熔炉重锤。
 public class ScenPart_FireGiantClan : ScenPart {
  public float fuel=30;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref fuel,"fuel",30);}
  public override string Summary(Scenario scen)=>"开局附近有一座尚存"+fuel.ToString("0")+"燃料的熔火锻炉；每个火巨人穿着锻工外衣，近战最好的火巨人手持熔炉重锤。";
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(map,fuel);}
  public static Thing Apply(Map map,float fuel){
   var forge=StartingBuildings.Place(DefDatabase<ThingDef>.GetNamed("LP_FireGiantForge"),null,StartingBuildings.Center(map),map);
   if(forge!=null && fuel>0)forge.TryGetComp<CompRefuelable>()?.Refuel(fuel);
   var giants=map.mapPawns.FreeColonistsSpawned.Where(FireGiant.Active).ToList();
   var tunic=DefDatabase<ThingDef>.GetNamed("LP_FireGiantTunic");
   foreach(var giant in giants){
    if(giant.apparel==null || giant.apparel.WornApparel.Any(a=>a.def==tunic))continue;
    var worn=(Apparel)ThingMaker.MakeThing(tunic,ThingDefOf.Cloth);worn.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good,ArtGenerationContext.Colony);
    giant.apparel.Wear(worn);
   }
   var bearer=giants.Where(p=>p.equipment!=null && !p.WorkTagIsDisabled(WorkTags.Violent)).OrderByDescending(p=>p.skills?.GetSkill(SkillDefOf.Melee)?.Level??0).FirstOrDefault();
   if(bearer!=null){
    var maul=(ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FireGiantMaul"));maul.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Good,ArtGenerationContext.Colony);
    if(bearer.equipment.Primary!=null)bearer.equipment.TryDropEquipment(bearer.equipment.Primary,out _,bearer.Position,false);
    bearer.equipment.AddEquipment(maul);
   }
   return forge;
  }
 }

 // 开局后限时的地图状况（日食、火山冬天等）。
 public class ScenPart_TimedCondition : ScenPart {
  public GameConditionDef condition;public float days=3;
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref condition,"condition");Scribe_Values.Look(ref days,"days",3);}
  public override string Summary(Scenario scen)=>condition==null?null:"开局后"+days.ToString("0.#")+"天内："+condition.LabelCap;
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(condition==null)yield return "condition is null";if(days<=0)yield return "days must be positive";}
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(map,condition,days);}
  public static GameCondition Apply(Map map,GameConditionDef def,float days){if(def==null)return null;var cond=GameConditionMaker.MakeCondition(def,(int)(days*60000));map.gameConditionManager.RegisterCondition(cond);return cond;}
 }

 // ==================== 1.20.0 六个种族开局的剧本组件 ====================
 // 剧本编辑器里的小控件。
 public static class ScenEdit {
  public static string Name(Def d)=>d==null?"（无）":d.label.NullOrEmpty()?d.defName:d.LabelCap.ToString();
  public static void DefButton<T>(Rect r,T cur,IEnumerable<T> options,Action<T> set,bool allowNone=false) where T:Def{
   if(!Widgets.ButtonText(r,Name(cur)))return;
   var opts=new List<FloatMenuOption>();if(allowNone)opts.Add(new FloatMenuOption("（无）",()=>set(null)));
   foreach(var d in options){var x=d;opts.Add(new FloatMenuOption(Name(x),()=>set(x)));}
   Find.WindowStack.Add(new FloatMenu(opts));
  }
  public static Rect Row(Rect r,int i,int rows)=>new Rect(r.x,r.y+i*r.height/rows,r.width,r.height/rows);
  public static IEnumerable<Pawn> Starters(Map map)=>map.mapPawns.FreeColonistsSpawned.Where(p=>p.RaceProps.Humanlike && !Necro.Undead(p));
 }
 // 开局物资条目（山腹营地、迷宫遗迹用）。
 public class CampThing : IExposable {
  public ThingDef thing,stuff;public int count=1;public QualityCategory quality=QualityCategory.Normal;
  public void ExposeData(){Scribe_Defs.Look(ref thing,"thing");Scribe_Defs.Look(ref stuff,"stuff");Scribe_Values.Look(ref count,"count",1);Scribe_Values.Look(ref quality,"quality",QualityCategory.Normal);}
  public string Label=>thing==null?"?":(thing.MadeFromStuff && stuff!=null?stuff.LabelAsStuff:"")+thing.label+"×"+count;
  public IEnumerable<Thing> Make(){
   if(thing==null)yield break;
   for(int left=count;left>0;){var t=ThingMaker.MakeThing(thing,thing.MadeFromStuff?(stuff??GenStuff.DefaultStuffFor(thing)):null);t.TryGetComp<CompQuality>()?.SetQuality(quality,ArtGenerationContext.Colony);t.stackCount=Math.Min(left,Math.Max(1,thing.stackLimit));left-=t.stackCount;yield return t;}
  }
 }

 // ---------- 限时派系袭击 ----------
 public class ScheduledRaid : IExposable {
  public FactionDef faction;public int fireTick;public float pointsFactor=1f;public PawnsArrivalModeDef arrival;public string letterLabel;
  // 1.34.1：temporaryHostile——落地时把（不敌对的）派系临时设为敌对，袭击撤走后好感回到0（拒绝云巨人使节用）。
  public bool temporaryHostile;
  // 1.38.0：fixedPoints>0 时用固定点数（实验池逃亡的300点追捕队），不按当前威胁点数×倍率。
  public float fixedPoints;
  public void ExposeData(){Scribe_Defs.Look(ref faction,"faction");Scribe_Values.Look(ref fireTick,"fireTick");Scribe_Values.Look(ref pointsFactor,"pointsFactor",1f);Scribe_Defs.Look(ref arrival,"arrival");Scribe_Values.Look(ref letterLabel,"letterLabel");Scribe_Values.Look(ref temporaryHostile,"temporaryHostile");Scribe_Values.Look(ref fixedPoints,"fixedPoints",0f);}
  public static int Process(FactionEvents events,bool force=false){
   int fired=0;
   for(int i=events.scheduledRaids.Count-1;i>=0;i--){var r=events.scheduledRaids[i];if(!force && Find.TickManager.TicksGame<r.fireTick)continue;events.scheduledRaids.RemoveAt(i);if(r.Fire(Find.AnyPlayerHomeMap))fired++;}
   return fired;
  }
  public static bool CanGenerate(Map map,Faction f,float points){
   Rand.PushState(0x5CE1);
   try{return PawnGroupMakerUtility.GeneratePawnKindsExample(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,faction=f,points=IncidentWorker_Raid.AdjustedRaidPoints(points,PawnsArrivalModeDefOf.EdgeWalkIn,RaidStrategyDefOf.ImmediateAttack,f,PawnGroupKindDefOf.Combat,map),tile=map.Tile,raidStrategy=RaidStrategyDefOf.ImmediateAttack}).Any();}
   catch{return true;}finally{Rand.PopState();}
  }
  public static float GeneratablePoints(Map map,Faction f,float points){for(int i=0;i<12 && !CanGenerate(map,f,points);i++)points*=1.35f;return points;}
  public float Points(Map map,Faction f)=>Math.Max(fixedPoints>0?fixedPoints:StorytellerUtility.DefaultThreatPointsNow(map)*pointsFactor,f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)*1.05f);
  // 剧本袭击按时落地：forced，不被竖琴手推迟。派系不在、被关掉或已不敌对就不来，只发一条消息。
  public bool Fire(Map map){
   var f=Factions.Of(faction);if(map==null)return false;
   bool madeHostile=false;
   if(temporaryHostile && f!=null && !f.defeated && Factions.Enabled(f) && !f.HostileTo(Faction.OfPlayer)){Factions.SetRelation(f,Faction.OfPlayer,-100);madeHostile=true;}
   if(f==null || f.defeated || !Factions.Enabled(f) || !f.HostileTo(Faction.OfPlayer)){Messages.Message((letterLabel.NullOrEmpty()?"开局约定的袭击":"“"+letterLabel+"”")+"没有到来："+(f==null?"这个派系不在本存档里":f.Name+(f.HostileTo(Faction.OfPlayer)?"已被关闭":"已经不再敌对"))+"。",MessageTypeDefOf.NeutralEvent,false);return false;}
   // 1.63.0：完整测试 #925 的根因——小殖民地的威胁点数很低（23～35），“派系最低点数×1.05”仍然低于原版单人花费上限曲线允许的最便宜兵种，
   // 一个人也生成不出来，袭击就悄悄没了。落地前先看这个点数能不能生成，不能就逐步加到能生成为止（最多 12 次 ×1.35）。
   var parms=new IncidentParms{target=map,faction=f,forced=true,points=GeneratablePoints(map,f,Points(map,f)),raidStrategy=RaidStrategyDefOf.ImmediateAttack};
   if(arrival!=null && arrival.Worker.CanUseWith(parms))parms.raidArrivalMode=arrival;
   if(!letterLabel.NullOrEmpty())parms.customLetterLabel=letterLabel;
   bool fired=IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
   // 1.63.0：没指定到达方式时原版随机挑（边缘走入 / 空投……），有的地图上挑中的方式找不到落点，约定的袭击就悄悄没了（完整测试 #925 时好时坏的根因）。
   // 失败时依次改用边缘走入、中心空投再试；都不行才算没来，并写一条日志。
   if(!fired && arrival==null)foreach(var fallback in new[]{PawnsArrivalModeDefOf.EdgeWalkIn,PawnsArrivalModeDefOf.CenterDrop}){
    var p2=new IncidentParms{target=map,faction=f,forced=true,points=parms.points,raidStrategy=RaidStrategyDefOf.ImmediateAttack,raidArrivalMode=fallback,customLetterLabel=parms.customLetterLabel};
    if(!fallback.Worker.CanUseWith(p2))continue;
    if(fired=IncidentDefOf.RaidEnemy.Worker.TryExecute(p2))break;
   }
   if(!fired)Log.Warning("[DND边缘大陆] 剧本约定的袭击（"+(letterLabel??faction?.defName)+"）没能落地（点数 "+parms.points.ToString("0")+"）。");
   if(madeHostile){if(fired)CloudGiantState.Get?.TrackTemporaryRaid(f);else Factions.SetRelation(f,Faction.OfPlayer,0);}
   return fired;
  }
 }
 public class ScenPart_TimedRaid : ScenPart {
  public FactionDef faction;public FloatRange days=new FloatRange(6,6);public float pointsFactor=.6f;public PawnsArrivalModeDef arrival;public string letterLabel;
  string factorBuf;
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref faction,"faction");Scribe_Values.Look(ref days,"days",new FloatRange(6,6));Scribe_Values.Look(ref pointsFactor,"pointsFactor",.6f);Scribe_Defs.Look(ref arrival,"arrival");Scribe_Values.Look(ref letterLabel,"letterLabel");}
  public string DaysText=>Math.Abs(days.max-days.min)<.05f?"第"+days.min.ToString("0.#")+"天":"第"+days.min.ToString("0.#")+"～"+days.max.ToString("0.#")+"天";
  public override string Summary(Scenario scen)=>faction==null?null:"开局后"+DaysText+"："+(faction.fixedName??faction.label)+"来袭（点数×"+pointsFactor.ToString("0.##")+(arrival!=null?"，"+ScenEdit.Name(arrival):"")+"）"+(letterLabel.NullOrEmpty()?"":"——“"+letterLabel+"”")+"。";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(faction==null)yield return "faction is null";if(days.min<0 || days.max<days.min)yield return "days must be a non-negative range";if(pointsFactor<=0)yield return "pointsFactor must be positive";}
  public override void DoEditInterface(Listing_ScenEdit listing){
   var r=listing.GetScenPartRect(this,RowHeight*5f);
   ScenEdit.DefButton(ScenEdit.Row(r,0,5),faction,DefDatabase<FactionDef>.AllDefs.Where(d=>!d.isPlayer && d.humanlikeFaction && d.pawnGroupMakers!=null),x=>faction=x);
   Widgets.FloatRange(ScenEdit.Row(r,1,5),GetHashCode(),ref days,0f,60f);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,2,5),"点数倍率 ",ref pointsFactor,ref factorBuf,.1f,5f);
   ScenEdit.DefButton(ScenEdit.Row(r,3,5),arrival,DefDatabase<PawnsArrivalModeDef>.AllDefs,x=>arrival=x,true);
   letterLabel=Widgets.TextField(ScenEdit.Row(r,4,5),letterLabel??"");
  }
  public override void PostGameStart(){Schedule();}
  public ScheduledRaid Schedule(){
   var ev=Tribute.Events;if(ev==null || faction==null)return null;
   var r=new ScheduledRaid{faction=faction,fireTick=Find.TickManager.TicksGame+(int)(days.RandomInRange*GenDate.TicksPerDay),pointsFactor=pointsFactor,arrival=arrival,letterLabel=letterLabel};
   ev.scheduledRaids.Add(r);return r;
  }
 }

 // ---------- 山腹营地 ----------
 public class ScenPart_MountainCamp : ScenPart {
  public List<CampThing> things=new List<CampThing>();public float radius=40;
  string[] bufs;
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref things,"things",LookMode.Deep);Scribe_Values.Look(ref radius,"radius",40);if(Scribe.mode==LoadSaveMode.PostLoadInit && things==null)things=new List<CampThing>();}
  public override string Summary(Scenario scen)=>"开局物资放在附近有厚岩顶的空地上（山腹营地）："+string.Join("、",things.Select(t=>t.Label))+"。找不到山体时放在普通位置。";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(things.Any(t=>t.thing==null || t.count<1))yield return "camp things need a def and a positive count";}
  public override void DoEditInterface(Listing_ScenEdit listing){
   var r=listing.GetScenPartRect(this,RowHeight*(1+things.Count));
   Widgets.Label(ScenEdit.Row(r,0,1+things.Count).LeftHalf(),"搜索半径："+radius.ToString("0"));radius=Widgets.HorizontalSlider(ScenEdit.Row(r,0,1+things.Count).RightHalf(),radius,10f,55f,true);
   if(bufs==null || bufs.Length!=things.Count)bufs=new string[things.Count];
   for(int i=0;i<things.Count;i++)Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,i+1,1+things.Count),things[i].thing==null?"?":ScenEdit.Name(things[i].thing)+" ",ref things[i].count,ref bufs[i],1,10000);
  }
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(map,StartingBuildings.Center(map),things,radius,out _);}
  static bool Free(IntVec3 c,Map map)=>c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetEdifice(map)==null && c.GetFirstItem(map)==null && c.GetFirstPawn(map)==null && !c.GetTerrain(map).IsWater;
  public static bool UnderRock(IntVec3 c,Map map)=>c.GetRoof(map)?.isThickRoof==true;
  static bool Reach(Map map,IntVec3 a,IntVec3 b)=>map.reachability.CanReach(a,b,PathEndMode.OnCell,TraverseParms.For(TraverseMode.PassDoors));
  static int RockRoom(Map map,IntVec3 spot){int n=0;foreach(var c in GenRadial.RadialCellsAround(spot,10f,true))if(Free(c,map) && UnderRock(c,map))n++;return n;}
  // 优先找最近的、可走到的、周围10格内的厚岩顶空地放得下全部物资的地方；都放不下就选最宽敞的一处；一处厚岩顶都没有就退回开局人物身边。
  public static IntVec3 FindCamp(Map map,IntVec3 near,float radius,int need,out bool underRock){
   underRock=false;IntVec3 best=IntVec3.Invalid;int bestRoom=-1,tried=0;
   foreach(var c in GenRadial.RadialCellsAround(near,Math.Min(radius,55f),true)){
    if(!Free(c,map) || !UnderRock(c,map) || !Reach(map,near,c))continue;
    int room=RockRoom(map,c);if(room>=need){underRock=true;return c;}
    if(room>bestRoom){bestRoom=room;best=c;}
    if(++tried>=600)break;
   }
   if(best.IsValid){underRock=true;return best;}
   return near;
  }
  public static List<Thing> Apply(Map map,IntVec3 near,List<CampThing> things,float radius,out bool underRock){
   int need=things.Where(t=>t.thing!=null).Sum(t=>(t.count+Math.Max(1,t.thing.stackLimit)-1)/Math.Max(1,t.thing.stackLimit));
   var spot=FindCamp(map,near,radius,need,out underRock);
   var cells=underRock?GenRadial.RadialCellsAround(spot,10f,true).Where(c=>Free(c,map) && UnderRock(c,map) && Reach(map,spot,c)).ToList():new List<IntVec3>();
   var placed=new List<Thing>();int i=0;
   foreach(var ct in things)foreach(var t in ct.Make()){
    if(i<cells.Count)GenSpawn.Spawn(t,cells[i++],map);else if(!GenPlace.TryPlaceThing(t,spot,map,ThingPlaceMode.Near)){t.Destroy();continue;}
    t.SetForbidden(false,false);placed.Add(t);
   }
   if(!underRock)Find.LetterStack.ReceiveLetter("没找到山体","开局人物附近找不到带厚岩顶的空地，物资放在了普通位置。\n\n建议在丘陵或山地开局：矮人的“石之耐力”只在厚岩顶下生效。",LetterDefOf.NeutralEvent,new LookTargets(spot,map));
   return placed;
  }
 }

 // ---------- 迷宫遗迹 ----------
 public class ScenPart_Labyrinth : ScenPart {
  public int size=17,bedrolls=2;public bool roof=true;public List<CampThing> things=new List<CampThing>();
  string sizeBuf,bedBuf;
  public int Size=>Labyrinth.ClampSize(size);
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref size,"size",17);Scribe_Values.Look(ref bedrolls,"bedrolls",2);Scribe_Values.Look(ref roof,"roof",true);Scribe_Collections.Look(ref things,"things",LookMode.Deep);if(Scribe.mode==LoadSaveMode.PostLoadInit && things==null)things=new List<CampThing>();}
  public override string Summary(Scenario scen)=>"开局附近有一座"+Size+"×"+Size+"的花岗岩迷宫遗迹"+(roof?"（全部盖有屋顶）":"")+"，中央5×5的房间里有"+bedrolls+"张睡袋"+(things.Count>0?"和"+string.Join("、",things.Select(t=>t.Label)):"")+"。找不到平地时不生成。";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(size<Labyrinth.MinSize || size>Labyrinth.MaxSize || size%2==0)yield return "size must be odd and between "+Labyrinth.MinSize+" and "+Labyrinth.MaxSize;if(bedrolls<0 || bedrolls>Labyrinth.Room)yield return "bedrolls must be 0-5";}
  public override void DoEditInterface(Listing_ScenEdit listing){
   var r=listing.GetScenPartRect(this,RowHeight*3f);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,0,3),"尺寸（奇数） ",ref size,ref sizeBuf,Labyrinth.MinSize,Labyrinth.MaxSize);
   Widgets.CheckboxLabeled(ScenEdit.Row(r,1,3),"全部加屋顶",ref roof);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,2,3),"睡袋 ",ref bedrolls,ref bedBuf,0,Labyrinth.Room);
  }
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Labyrinth.Build(map,StartingBuildings.Center(map),Size,roof,bedrolls,things);}
 }
 public class LabyrinthResult {public CellRect rect;public IntVec3 entrance,outside,roomCenter;public CellRect room;public List<Thing> things=new List<Thing>();}
 public static class Labyrinth {
  public const int MinSize=11,MaxSize=31,Room=5;
  public static int ClampSize(int n)=>Math.Max(MinSize,Math.Min(MaxSize,n|1));
  static readonly IntVec2[] Steps={new IntVec2(2,0),new IntVec2(-2,0),new IntVec2(0,2),new IntVec2(0,-2)};
  // 迷宫布局（true 为通道）：格子中心在奇数坐标，深度优先打通相邻格子；中央 5×5 房间外留一圈墙，朝迷宫开一扇门；入口在外墙上（这里只算格子，入口由 Build 选边）。
  public static bool[,] Layout(int n){
   var open=new bool[n,n];int c=n/2;
   bool Blocked(int x,int z)=>Math.Abs(x-c)<=3 && Math.Abs(z-c)<=3;
   for(int x=c-2;x<=c+2;x++)for(int z=c-2;z<=c+2;z++)open[x,z]=true;
   var stack=new Stack<IntVec2>();open[1,1]=true;stack.Push(new IntVec2(1,1));
   var next=new List<IntVec2>();
   while(stack.Count>0){
    var cur=stack.Peek();next.Clear();
    foreach(var d in Steps){int x=cur.x+d.x,z=cur.z+d.z;if(x>=1 && z>=1 && x<=n-2 && z<=n-2 && !Blocked(x,z) && !open[x,z])next.Add(new IntVec2(x,z));}
    if(next.Count==0){stack.Pop();continue;}
    var nx=next.RandomElement();open[(cur.x+nx.x)/2,(cur.z+nx.z)/2]=true;open[nx.x,nx.z]=true;stack.Push(nx);
   }
   // 房间门：从最近的格子打通到房间。
   int side=Rand.Range(0,4);int row=Enumerable.Range(c-2,Room).Where(v=>v%2==1).RandomElement();
   int far=c-4;if(far%2==0)far--;
   for(int k=far+1;k<=c-3;k++){int d=k,o=n-1-k;if(side==0)open[d,row]=true;else if(side==1)open[o,row]=true;else if(side==2)open[row,d]=true;else open[row,o]=true;}
   return open;
  }
  public static bool CanPlace(CellRect rect,Map map){
   if(!rect.ExpandedBy(1).InBounds(map))return false;
   foreach(var c in rect.Cells){
    if(c.Fogged(map) || c.Roofed(map) || c.GetEdifice(map)!=null || c.GetTerrain(map).IsWater || !c.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy))return false;
    foreach(var t in c.GetThingList(map))if(!(t is Plant) && t.def.category!=ThingCategory.Filth && !(t.def.thingCategories?.Contains(ThingCategoryDefOf.StoneChunks)??false))return false;
   }
   return true;
  }
  public static CellRect? FindSpot(Map map,IntVec3 near,int n){
   foreach(var c in GenRadial.RadialCellsAround(near,55f,true)){var rect=new CellRect(c.x-n/2,c.z-n/2,n,n);if(CanPlace(rect,map))return rect;}
   return null;
  }
  public static LabyrinthResult Build(Map map,IntVec3 near,int n,bool roof,int bedrolls,List<CampThing> things){
   n=ClampSize(n);
   var found=FindSpot(map,near,n);
   if(found==null){NoSpaceLetter(map,near,n);return null;}
   var rect=found.Value;var open=Layout(n);var res=new LabyrinthResult{rect=rect};
   // 入口：外墙上一个格子，外侧必须能站人。
   var exits=new List<(IntVec3 wall,IntVec3 outside)>();
   for(int k=1;k<n-1;k+=2){
    exits.Add((new IntVec3(rect.minX+k,0,rect.minZ),new IntVec3(rect.minX+k,0,rect.minZ-1)));exits.Add((new IntVec3(rect.minX+k,0,rect.maxZ),new IntVec3(rect.minX+k,0,rect.maxZ+1)));
    exits.Add((new IntVec3(rect.minX,0,rect.minZ+k),new IntVec3(rect.minX-1,0,rect.minZ+k)));exits.Add((new IntVec3(rect.maxX,0,rect.minZ+k),new IntVec3(rect.maxX+1,0,rect.minZ+k)));
   }
   var good=exits.InRandomOrder().Where(e=>e.outside.InBounds(map) && e.outside.Standable(map) && !e.outside.GetTerrain(map).IsWater).ToList();
   var exit=good.Count>0?good[0]:exits.RandomElement();
   res.entrance=exit.wall;res.outside=exit.outside;open[res.entrance.x-rect.minX,res.entrance.z-rect.minZ]=true;
   var granite=DefDatabase<ThingDef>.GetNamedSilentFail("BlocksGranite");
   foreach(var c in rect.Cells){
    foreach(var t in c.GetThingList(map).ToList())if(!t.Destroyed && (t is Plant || t.def.category==ThingCategory.Filth || t.def.category==ThingCategory.Item))t.Destroy();
    if(roof)map.roofGrid.SetRoof(c,RoofDefOf.RoofConstructed);
    if(open[c.x-rect.minX,c.z-rect.minZ])continue;
    var wall=ThingMaker.MakeThing(ThingDefOf.Wall,granite??ThingDefOf.Steel);wall.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(wall,c,map,WipeMode.Vanish);
   }
   int ctr=n/2;res.roomCenter=new IntVec3(rect.minX+ctr,0,rect.minZ+ctr);res.room=CellRect.CenteredOn(res.roomCenter,2);
   // 睡袋靠房间北墙一字排开，其余格子放物资。
   var used=new HashSet<IntVec3>();var bedroll=DefDatabase<ThingDef>.GetNamedSilentFail("Bedroll");
   for(int i=0;i<bedrolls && bedroll!=null;i++){
    var pos=new IntVec3(res.room.minX+i,0,res.room.maxZ);var occ=GenAdj.OccupiedRect(pos,Rot4.South,bedroll.size);
    if(!occ.Cells.All(x=>res.room.Contains(x) && !used.Contains(x)))continue;
    var bed=ThingMaker.MakeThing(bedroll,GenStuff.DefaultStuffFor(bedroll));bed.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(bed,pos,map,Rot4.South);foreach(var x in occ.Cells)used.Add(x);res.things.Add(bed);
   }
   var free=res.room.Cells.Where(x=>!used.Contains(x)).OrderBy(x=>x.DistanceToSquared(res.roomCenter)).ToList();int fi=0;
   foreach(var ct in things??new List<CampThing>())foreach(var t in ct.Make()){
    if(fi<free.Count)GenSpawn.Spawn(t,free[fi++],map);else if(!GenPlace.TryPlaceThing(t,res.roomCenter,map,ThingPlaceMode.Near)){t.Destroy();continue;}
    t.SetForbidden(false,false);res.things.Add(t);
   }
   return res;
  }
  static void NoSpaceLetter(Map map,IntVec3 near,int n)=>Find.LetterStack.ReceiveLetter("没有地方放迷宫","开局人物附近找不到一块"+n+"×"+n+"、没有建筑、没有水、没有山体屋顶的平地，迷宫遗迹没有生成。\n\n物资照常在开局人物身边。",LetterDefOf.NeutralEvent,new LookTargets(near,map));
 }

 // ---------- 开局装备：每人随机一件武器；某技能最好的一人拿指定武器；每人穿上指定衣物 ----------
 public class ScenPart_StarterKit : ScenPart {
  public List<ThingDef> weapons=new List<ThingDef>();public ThingDef weaponStuff;
  public ThingDef champion,championStuff;public QualityCategory championQuality=QualityCategory.Excellent;public SkillDef championSkill;
  public List<ThingDef> apparel=new List<ThingDef>();public ThingDef apparelStuff;
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref weapons,"weapons",LookMode.Def);Scribe_Defs.Look(ref weaponStuff,"weaponStuff");Scribe_Defs.Look(ref champion,"champion");Scribe_Defs.Look(ref championStuff,"championStuff");Scribe_Values.Look(ref championQuality,"championQuality",QualityCategory.Excellent);Scribe_Defs.Look(ref championSkill,"championSkill");Scribe_Collections.Look(ref apparel,"apparel",LookMode.Def);Scribe_Defs.Look(ref apparelStuff,"apparelStuff");if(Scribe.mode==LoadSaveMode.PostLoadInit){weapons=weapons?.Where(d=>d!=null).ToList()??new List<ThingDef>();apparel=apparel?.Where(d=>d!=null).ToList()??new List<ThingDef>();}}
  static string StuffLabel(ThingDef stuff)=>stuff==null?"":stuff.LabelAsStuff;
  public override string Summary(Scenario scen){
   var parts=new List<string>();
   if(champion!=null)parts.Add((championSkill??SkillDefOf.Melee).label+"最好的人手持"+championQuality.GetLabel()+"品质的"+StuffLabel(championStuff)+champion.label);
   if(weapons.Count>0)parts.Add((champion!=null?"其余每人":"每人")+"一把"+StuffLabel(weaponStuff)+string.Join("或",weapons.Select(w=>w.label))+(weapons.Count>1?"（随机）":""));
   if(apparel.Count>0)parts.Add("每人穿着"+string.Join("、",apparel.Select(a=>StuffLabel(apparelStuff)+a.label)));
   return parts.Count==0?null:"开局装备："+string.Join("；",parts)+"。";
  }
  public override void DoEditInterface(Listing_ScenEdit listing){
   var r=listing.GetScenPartRect(this,RowHeight*2f);
   ScenEdit.DefButton(ScenEdit.Row(r,0,2),champion,DefDatabase<ThingDef>.AllDefs.Where(d=>d.IsWeapon && d.equipmentType==EquipmentType.Primary && !d.destroyOnDrop),x=>champion=x,true);
   ScenEdit.DefButton(ScenEdit.Row(r,1,2),championSkill??SkillDefOf.Melee,DefDatabase<SkillDef>.AllDefs,x=>championSkill=x);
  }
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(ScenEdit.Starters(map).ToList());}
  static ThingWithComps Make(ThingDef def,ThingDef stuff,QualityCategory q){var t=(ThingWithComps)ThingMaker.MakeThing(def,def.MadeFromStuff?(stuff??GenStuff.DefaultStuffFor(def)):null);t.TryGetComp<CompQuality>()?.SetQuality(q,ArtGenerationContext.Colony);return t;}
  static void Arm(Pawn p,ThingWithComps w){
   if(p.equipment==null || p.WorkTagIsDisabled(WorkTags.Violent)){GenPlace.TryPlaceThing(w,p.Position,p.Map,ThingPlaceMode.Near);return;}
   if(p.equipment.Primary!=null)p.equipment.TryDropEquipment(p.equipment.Primary,out _,p.Position,false);
   p.equipment.AddEquipment(w);
  }
  public Pawn Apply(List<Pawn> pawns){
   Pawn chosen=null;var skill=championSkill??SkillDefOf.Melee;
   if(champion!=null)chosen=pawns.Where(p=>p.equipment!=null && !p.WorkTagIsDisabled(WorkTags.Violent)).OrderByDescending(p=>p.skills?.GetSkill(skill)?.Level??0).FirstOrDefault();
   foreach(var p in pawns){
    if(p==chosen)Arm(p,Make(champion,championStuff,championQuality));
    else if(weapons.Count>0)Arm(p,Make(weapons.RandomElement(),weaponStuff,QualityCategory.Normal));
    foreach(var def in apparel){
     if(p.apparel==null || p.apparel.WornApparel.Any(a=>a.def==def))continue;
     var a=(Apparel)Make(def,apparelStuff,QualityCategory.Normal);
     if(ApparelUtility.HasPartsToWear(p,def))p.apparel.Wear(a,true);else GenPlace.TryPlaceThing(a,p.Position,p.Map,ThingPlaceMode.Near);
    }
   }
   return chosen;
  }
 }

 // ---------- 开局天赋：全员或某技能最好的一人获得心灵链接等级 / 技能兴趣 ----------
 public class ScenPart_StarterBoon : ScenPart {
  public SkillDef bestAt;public int psylink;public SkillDef passionSkill;public Passion passion=Passion.Minor;
  string psyBuf;
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref bestAt,"bestAt");Scribe_Values.Look(ref psylink,"psylink");Scribe_Defs.Look(ref passionSkill,"passionSkill");Scribe_Values.Look(ref passion,"passion",Passion.Minor);}
  public override string Summary(Scenario scen){
   var gifts=new List<string>();if(psylink>0)gifts.Add(psylink+"级心灵链接");if(passionSkill!=null && passion!=Passion.None)gifts.Add((passion==Passion.Major?"二级":"一级")+passionSkill.label+"兴趣");
   return gifts.Count==0?null:(bestAt==null?"每个开局人物":bestAt.label+"最好的开局人物")+"带"+string.Join("和",gifts)+"。";
  }
  public override void DoEditInterface(Listing_ScenEdit listing){
   var r=listing.GetScenPartRect(this,RowHeight*3f);
   ScenEdit.DefButton(ScenEdit.Row(r,0,3),bestAt,DefDatabase<SkillDef>.AllDefs,x=>bestAt=x,true);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,1,3),"心灵链接等级 ",ref psylink,ref psyBuf,0,6);
   ScenEdit.DefButton(ScenEdit.Row(r,2,3),passionSkill,DefDatabase<SkillDef>.AllDefs,x=>passionSkill=x,true);
  }
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(ScenEdit.Starters(map).ToList());}
  public List<Pawn> Apply(List<Pawn> pawns){
   var targets=bestAt==null?pawns:pawns.Where(p=>p.skills!=null).OrderByDescending(p=>p.skills.GetSkill(bestAt).Level).Take(1).ToList();
   foreach(var p in targets){
    if(psylink>0 && ModsConfig.RoyaltyActive)for(int i=0;i<10 && p.GetPsylinkLevel()<psylink;i++)p.ChangePsylinkLevel(1,false);
    var s=passionSkill==null?null:p.skills?.GetSkill(passionSkill);if(s!=null && !s.TotallyDisabled && s.passion<passion)s.passion=passion;
   }
   return targets;
  }
 }

 // ---------- 开局背景：开局人物的成年背景换成指定背景 ----------
 public class ScenPart_StarterBackstory : ScenPart {
  public BackstoryDef adulthood;
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref adulthood,"adulthood");}
  public override string Summary(Scenario scen)=>adulthood==null?null:"开局人物的成年背景都是“"+adulthood.title+"”。";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(adulthood==null || adulthood.slot!=BackstorySlot.Adulthood)yield return "adulthood must be an adulthood backstory";}
  public override void DoEditInterface(Listing_ScenEdit listing){
   var r=listing.GetScenPartRect(this,RowHeight);
   if(Widgets.ButtonText(r,adulthood?.title??"（无）"))Find.WindowStack.Add(new FloatMenu(DefDatabase<BackstoryDef>.AllDefs.Where(b=>b.slot==BackstorySlot.Adulthood).OrderBy(b=>b.title).Select(b=>new FloatMenuOption(b.title,()=>adulthood=b)).ToList()));
  }
  public override void Notify_PawnGenerated(Pawn pawn,PawnGenerationContext context,bool redressed){if(context==PawnGenerationContext.PlayerStarter)Apply(pawn,adulthood);}
  public static bool Apply(Pawn p,BackstoryDef b){
   if(b==null || p?.story?.Adulthood==null)return false;
   p.story.Adulthood=b;p.Notify_DisabledWorkTypesChanged();p.skills?.Notify_SkillDisablesChanged();return true;
  }
 }

 // ---------- 逃离殖巢：夺心魔脑髓、半夺心魔殖民者、被捆缚的囚犯 ----------
 public class ScenPart_FlayerEscape : ScenPart {
  public float cerebrum=60,tadpole=.3f;public int prisoners=2;
  string cBuf,tBuf,pBuf;
  static HediffDef bound;
  public static HediffDef Bound=>bound??(bound=DefDatabase<HediffDef>.GetNamed("LP_Bound"));
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref cerebrum,"cerebrum",60);Scribe_Values.Look(ref tadpole,"tadpole",.3f);Scribe_Values.Look(ref prisoners,"prisoners",2);}
  public override string Summary(Scenario scen)=>"夺心魔开局脑髓"+cerebrum.ToString("0")+"；人类殖民者脑中有一条进度"+tadpole.ToStringPercent("0")+"的蝌蚪，正处在抑灵剂药效中（半夺心魔）；附近有"+prisoners+"名被捆缚的人类囚犯。";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(cerebrum<0 || cerebrum>MindFlayer.Max)yield return "cerebrum must be 0-100";if(tadpole<0 || tadpole>=HalfIllithid.FreezeLimit)yield return "tadpole progress must be below the suppression limit";if(prisoners<0)yield return "prisoners must not be negative";}
  public override void DoEditInterface(Listing_ScenEdit listing){
   var r=listing.GetScenPartRect(this,RowHeight*3f);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,0,3),"脑髓 ",ref cerebrum,ref cBuf,0,MindFlayer.Max);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,1,3),"蝌蚪进度 ",ref tadpole,ref tBuf,0,.49f);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,2,3),"囚犯 ",ref prisoners,ref pBuf,0,6);
  }
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(map,ScenEdit.Starters(map).ToList(),cerebrum,tadpole,prisoners);}
  public static List<Pawn> Apply(Map map,List<Pawn> starters,float cerebrum,float tadpole,int prisoners){
   var flayer=starters.FirstOrDefault(MindFlayer.IsMindFlayer);
   if(flayer!=null){var g=MindFlayer.Cerebrum(flayer);g.Value=Mathf.Clamp01(cerebrum/MindFlayer.Max);g.SyncHunger();}
   var host=starters.FirstOrDefault(p=>!MindFlayer.IsMindFlayer(p) && Tadpoles.Of(p)==null && Tadpoles.Immunity(p)==null);
   if(host!=null && tadpole>0){var h=Tadpoles.Implant(host,flayer,Faction.OfPlayer,out _);if(h!=null){h.Severity=Mathf.Min(tadpole,HalfIllithid.FreezeLimit-.01f);HalfIllithid.Dose(host);}}
   var made=new List<Pawn>();var near=flayer?.Position??StartingBuildings.Center(map);
   var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Drifter")??PawnKindDefOf.Villager;
   for(int i=0;i<prisoners;i++){
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,allowDowned:false,allowDead:false,fixedBiologicalAge:Rand.Range(20,45),fixedChronologicalAge:Rand.Range(45,60)));
    p.equipment?.DestroyAllEquipment();p.inventory?.innerContainer.ClearAndDestroyContents();
    GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(near,map,6),map);
    p.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);p.health.AddHediff(Bound);made.Add(p);
   }
   return made;
  }
 }
}
