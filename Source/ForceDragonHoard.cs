using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 宝库：力能龙自己床铺所在的房间。房间里所有物品的市值（火巨人锻造品双倍）就是宝库价值。
 public static class DragonHoard {
  public const float NestDays=10f,EggHoardScale=50000f,EggMaxBonus=2f;
  public const int ClutchCooldownDays=60;
  static ThingDef egg;static ThoughtDef stolen;
  public static ThingDef EggDef=>egg??(egg=DefDatabase<ThingDef>.GetNamed("LP_ForceDragonEgg"));
  public static ThoughtDef Stolen=>stolen??(stolen=DefDatabase<ThoughtDef>.GetNamed("LP_HoardStolen"));
  public static XenotypeDef Xenotype=>DefDatabase<XenotypeDef>.GetNamed("LP_ForceDragon");
  public static bool Valid(Room room)=>room!=null && room.ProperRoom && !room.IsHuge;
  public static Room RoomOf(Pawn p){var bed=p?.ownership?.OwnedBed;if(bed==null || !bed.Spawned)return null;var room=bed.GetRoom();return Valid(room)?room:null;}
  public static IEnumerable<Pawn> BedOwners(Room room){if(room==null)yield break;foreach(var bed in room.ContainedBeds)foreach(var o in bed.OwnersForReading)yield return o;}
  public static List<Pawn> Owners(Room room)=>Valid(room)?BedOwners(room).Where(ForceDragon.Active).Distinct().ToList():new List<Pawn>();
  public static bool FireGiantWork(ThingDef def)=>FireGiant.ForgeApparel(def) || FireGiant.GiantWeapon(def);
  public static bool Counts(Thing t)=>t.def.category==ThingCategory.Item && t.def!=EggDef;
  public static float ItemValue(Thing t){float v=t.MarketValue*t.stackCount;return FireGiantWork(t.def)?v*2f:v;}
  public static float Value(Room room){
   if(!Valid(room))return 0;
   float sum=0;var grid=room.Map.thingGrid;
   foreach(var c in room.Cells){var list=grid.ThingsListAtFast(c);for(int i=0;i<list.Count;i++)if(Counts(list[i]))sum+=ItemValue(list[i]);}
   return sum*Subraces.HoardFactor(room.Map); // 1.57.0 狗头人龙之仆从
  }
  // 1.23.3：宝库心情只有正面，没有宝库或不足1000银时不产生心情（-1 = 不显示）。
  public static readonly float[] WealthStages={1000f,10000f,50000f,150000f};
  public static readonly float[] BeautyStages={2.4f,5f,15f,50f};
  public static int Stage(float score,float[] stages){int s=-1;for(int i=0;i<stages.Length;i++)if(score>=stages[i])s=i;return s;}
  public static int MoodStage(bool has,float value)=>has?Stage(value,WealthStages):-1;
  public static int BeautyStage(bool has,float beauty)=>has?Stage(beauty,BeautyStages):-1;
  public static float Beauty(Room room)=>Valid(room)?room.GetStat(RoomStatDefOf.Beauty):0;

  // 有人从宝库拿走东西：1.23.3 起不再扣心情；外人拿的仍会触发锁定小偷的杀意狂暴（可在设置里关闭）。
  public static void Notify_Taken(List<Pawn> owners,Pawn taker){
   if(owners==null || taker==null || !LichMod.Settings.hoardTheftRage)return;
   foreach(var dragon in owners){
    if(dragon==taker || dragon.Dead)continue;
    if(taker.Faction!=dragon.Faction)StartRage(dragon,taker);
   }
  }
  // 卖掉龙鳞蜕：本殖民地所有力能龙记仇（与宝库无关）。
  public static void Notify_ShedSold(List<Pawn> dragons){
   if(dragons==null)return;
   foreach(var dragon in dragons)if(!dragon.Dead)dragon.needs?.mood?.thoughts?.memories?.TryGainMemory(Stolen);
  }
  public static bool StartRage(Pawn dragon,Pawn thief){
   if(!dragon.Spawned || !thief.Spawned || dragon.Map!=thief.Map || dragon.Downed || dragon.InMentalState || thief.Dead)return false;
   var handler=dragon.mindState?.mentalStateHandler;if(handler==null)return false;
   if(!handler.TryStartMentalState(DefDatabase<MentalStateDef>.GetNamed("MurderousRage"),"有人动了宝库",true,true,false,thief))return false;
   if(handler.CurState is MentalState_MurderousRage rage){rage.target=thief;return true;}
   return false;
  }

  public static bool Layer(Pawn a,Pawn b)=>a.gender!=b.gender?a.gender==Gender.Female:a.thingIDNumber<b.thingIDNumber;
  // 两条成年以上、同住一间宝库的力能龙筑巢，10天后由其中一条产下2～5枚龙蛋；每窝之间间隔60天。
  public static void TickNest(Gene_ForceDragonHeart heart,Room room,int elapsed){
   var pawn=heart.pawn;
   if(room==null || heart.Age<3 || !pawn.Spawned || pawn.Map!=room.Map){heart.nestDays=0;return;}
   if(heart.nextClutchTick>Find.TickManager.TicksGame)return;
   var mate=Owners(room).FirstOrDefault(o=>o!=pawn && !o.Dead && ForceDragon.Age(o)>=3);
   if(mate==null || !Layer(pawn,mate)){heart.nestDays=0;return;}
   heart.nestDays+=elapsed/60000f;
   if(heart.nestDays<NestDays)return;
   heart.nestDays=0;heart.nextClutchTick=Find.TickManager.TicksGame+ClutchCooldownDays*60000;
   var eggs=LayClutch(pawn,room,Rand.RangeInclusive(2,5));
   if(eggs.Count>0 && pawn.Faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("龙蛋",pawn.LabelShort+"和"+mate.LabelShort+"在宝库中筑巢，产下了"+eggs.Count+"枚力能龙蛋。龙蛋必须留在宝库里才会孵化。",LetterDefOf.PositiveEvent,eggs[0]);
  }
  public static List<Thing> LayClutch(Pawn mother,Room room,int count){
   var map=room.Map;var eggs=new List<Thing>();
   foreach(var c in room.Cells.Where(c=>c.Standable(map) && c.GetFirstItem(map)==null).OrderBy(c=>c.DistanceToSquared(mother.Position)).Take(count)){
    var e=ThingMaker.MakeThing(EggDef);GenSpawn.Spawn(e,c,map);eggs.Add(e);
   }
   return eggs;
  }

  // 剧本与测试用：在指定位置附近建一间7×7（内部5×5）带屋顶的宝库，并把床分配给龙。
  public static List<Thing> BuildHoard(Map map,IntVec3 near,Pawn owner,out CellRect interior){
   interior=CellRect.Empty;var built=new List<Thing>();
   var wallDef=ThingDefOf.Wall;var stone=DefDatabase<ThingDef>.GetNamed("BlocksGranite");var wood=ThingDefOf.WoodLog;
   foreach(var c in GenRadial.RadialCellsAround(near,40f,true)){
    var outer=CellRect.CenteredOn(c,3);
    if(!outer.InBounds(map) || outer.ExpandedBy(1).Cells.Any(x=>!x.InBounds(map)))continue;
    if(!outer.Cells.All(x=>x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map)==null && x.GetFirstPawn(map)==null && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth) && GenConstruct.CanBuildOnTerrain(wallDef,x,map,Rot4.North)))continue;
    var door=new IntVec3(c.x,0,c.z-3);
    foreach(var e in outer.EdgeCells){
     var t=e==door?ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Door"),wood):ThingMaker.MakeThing(wallDef,stone);
     t.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(t,e,map,WipeMode.Vanish);built.Add(t);
    }
    interior=CellRect.CenteredOn(c,2);
    foreach(var x in interior.Cells)map.roofGrid.SetRoof(x,RoofDefOf.RoofConstructed);
    var bed=(Building_Bed)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Bed"),wood);bed.SetFactionDirect(Faction.OfPlayer);
    GenSpawn.Spawn(bed,new IntVec3(c.x-2,0,c.z),map,Rot4.North,WipeMode.Vanish);built.Add(bed);
    owner?.ownership?.ClaimBedIfNonMedical(bed);
    return built;
   }
   Messages.Message("开局附近没有足够的空地建造宝库，请自行为力能龙裔建一间独占卧室。",MessageTypeDefOf.CautionInput,false);
   return built;
  }
  public static IEnumerable<IntVec3> FreeCells(CellRect rect,Map map)=>rect.Cells.Where(c=>c.Standable(map) && c.GetFirstItem(map)==null && c.GetEdifice(map)==null);
  public static List<Thing> Stock(Map map,CellRect rect,ThingDef def,int count){
   var placed=new List<Thing>();
   foreach(var c in FreeCells(rect,map).ToList()){
    if(count<=0)break;
    var t=ThingMaker.MakeThing(def);t.stackCount=Math.Min(count,def.stackLimit);count-=t.stackCount;
    GenSpawn.Spawn(t,c,map);placed.Add(t);
   }
   return placed;
  }
 }

 [HarmonyPatch(typeof(Pawn_CarryTracker),nameof(Pawn_CarryTracker.TryStartCarry),new[]{typeof(Thing),typeof(int),typeof(bool)})]
 public static class DragonHoardCarryTheft {
  static void Prefix(Thing __0,out List<Pawn> __state){
   __state=null;
   if(!LichMod.Settings.hoardTheftRage)return;
   if(__0==null || !__0.Spawned || !DragonHoard.Counts(__0))return;
   var room=__0.GetRoom();if(!DragonHoard.Valid(room))return;
   var owners=DragonHoard.Owners(room);if(owners.Count>0)__state=owners;
  }
  static void Postfix(Pawn_CarryTracker __instance,int __result,List<Pawn> __state){
   if(__state==null || __result<=0 || __state.Contains(__instance.pawn))return;
   DragonHoard.Notify_Taken(__state,__instance.pawn);
  }
 }

 public class ThoughtWorker_DragonHoard : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!ForceDragon.Has(p,ForceDragon.HoarderGene))return false;
   var heart=ForceDragon.Heart(p);if(heart==null)return false;
   int stage=DragonHoard.MoodStage(heart.hasHoard,heart.hoardValue);
   return stage<0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(stage);
  }
 }
 public class ThoughtWorker_DragonHoardBeauty : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!ForceDragon.Has(p,ForceDragon.HoarderGene))return false;
   var heart=ForceDragon.Heart(p);if(heart==null)return false;
   int stage=DragonHoard.BeautyStage(heart.hasHoard,heart.hoardBeauty);
   return stage<0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(stage);
  }
 }
 public class ThoughtWorker_DragonSharedRoom : ThoughtWorker {
  public static bool Shares(Pawn p){
   var room=p?.ownership?.OwnedBed?.GetRoom();
   if(room==null || room.PsychologicallyOutdoors)return false;
   return DragonHoard.BedOwners(room).Any(o=>o!=p && !ForceDragon.Active(o));
  }
  protected override ThoughtState CurrentStateInternal(Pawn p)=>ForceDragon.Has(p,ForceDragon.ArroganceGene) && Shares(p);
 }

 public class CompProperties_DragonEgg : CompProperties {public CompProperties_DragonEgg(){compClass=typeof(CompDragonEgg);}}
 public class CompDragonEgg : ThingComp {
  public float progressDays;
  public float HatchDays=>LichMod.Settings.dragonEggHatchDays;
  public Room Hoard{get{var room=parent.Spawned?parent.GetRoom():null;return DragonHoard.Valid(room)?room:null;}}
  // 只有放在宝库里才会孵化；宝库越富有孵得越快（每5万银+100%，最多×3）。
  public float Rate{get{
   var room=Hoard;if(room==null)return 0;
   var owners=DragonHoard.Owners(room);if(owners.Count==0)return 0;
   float value=owners.Max(o=>ForceDragon.Heart(o)?.hoardValue??0);
   return 1f+Mathf.Min(DragonHoard.EggMaxBonus,value/DragonHoard.EggHoardScale);
  }}
  public override void CompTickRare(){base.CompTickRare();Advance(250);}
  public Pawn Advance(int ticks){
   float rate=Rate;if(rate<=0)return null;
   progressDays+=ticks/60000f*rate;
   return progressDays>=HatchDays?Hatch():null;
  }
  public Pawn Hatch(){
   var map=parent.MapHeld;if(map==null)return null;
   var pos=parent.PositionHeld;var room=Hoard;
   var faction=room!=null?DragonHoard.Owners(room).FirstOrDefault()?.Faction:null;
   if(faction==null && map.IsPlayerHome)faction=Faction.OfPlayer;
   var request=new PawnGenerationRequest(PawnKindDefOf.Colonist,faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:true,canGeneratePawnRelations:false,forcedXenotype:DragonHoard.Xenotype,developmentalStages:DevelopmentalStage.Newborn,fixedBiologicalAge:0,fixedChronologicalAge:0,forceNoBackstory:true);
   var baby=PawnGenerator.GeneratePawn(request);
   parent.Destroy();
   GenSpawn.Spawn(baby,pos,map);
   if(baby.Faction==Faction.OfPlayer)Messages.Message("一枚力能龙蛋孵化了："+baby.LabelShort+"破壳而出。",baby,MessageTypeDefOf.PositiveEvent,false);
   return baby;
  }
  public override string CompInspectStringExtra(){
   float rate=Rate;
   return "孵化进度："+progressDays.ToString("0.#")+" / "+HatchDays.ToString("0")+" 天"+(rate<=0?"\n需要放在力能龙裔的宝库里才会孵化。":"\n宝库保温中，孵化速度×"+rate.ToString("0.##"));
  }
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref progressDays,"dragonEggProgress",0);}
 }

 // 龙蛋开局：力能龙是古龙，守着一座小宝库和一窝快孵化的蛋。
 public class ScenPart_ForceDragonClutch : ScenPart {
  public int age=4,eggs=3,gold=200,silver=1500;public float eggProgress=.9f;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref age,"age",4);Scribe_Values.Look(ref eggs,"eggs",3);Scribe_Values.Look(ref gold,"gold",200);Scribe_Values.Look(ref silver,"silver",1500);Scribe_Values.Look(ref eggProgress,"eggProgress",.9f);}
  public override string Summary(Scenario scen)=>"开局人物页中的力能龙裔已是"+ForceDragon.AgeLabel(age)+"，守着一座带屋顶的小宝库（"+gold+"金、"+silver+"银）和"+eggs+"枚快要孵化的龙蛋。";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(age<1 || age>ForceDragon.MaxAge)yield return "age must be 1-5";if(eggs<0 || gold<0 || silver<0)yield return "counts must not be negative";if(eggProgress<0 || eggProgress>1)yield return "eggProgress must be 0-1";}
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(map,age,eggs,eggProgress,gold,silver,out _);}
  public static List<Thing> Apply(Map map,int age,int eggs,float eggProgress,int gold,int silver,out CellRect interior){
   interior=CellRect.Empty;
   var dragon=map.mapPawns.FreeColonistsSpawned.Where(ForceDragon.Active).OrderByDescending(p=>p.ageTracker.AgeBiologicalYearsFloat).FirstOrDefault();
   if(dragon==null){Log.Warning("[DND边缘大陆] 龙蛋开局：找不到力能龙裔开局角色。");return new List<Thing>();}
   var heart=ForceDragon.Heart(dragon);
   heart.SetAge(age);heart.daysOnHoard=age>1?ForceDragon.DaysNeeded(age-1):0;heart.Value=heart.Max;
   var built=DragonHoard.BuildHoard(map,dragon.Position,dragon,out interior);
   if(interior.Area>0){
    built.AddRange(DragonHoard.Stock(map,interior,ThingDefOf.Gold,gold));
    built.AddRange(DragonHoard.Stock(map,interior,ThingDefOf.Silver,silver));
    foreach(var c in DragonHoard.FreeCells(interior,map).Take(eggs).ToList()){
     var e=ThingMaker.MakeThing(DragonHoard.EggDef);e.TryGetComp<CompDragonEgg>().progressDays=eggProgress*LichMod.Settings.dragonEggHatchDays;
     GenSpawn.Spawn(e,c,map);built.Add(e);
    }
   }
   heart.RefreshHoard();
   return built;
  }
 }
}
