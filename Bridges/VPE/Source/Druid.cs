using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.36.0 德鲁伊（主 种植 / 副 驯兽）：左支驯兽（安抚野兽、召唤野兽、荒野变形、兽王号令），右支种植（纠缠术、树肤术、植物滋长、召雷术），终极 掌控天气。
 // 所有效果的“量”乘心灵敏感度（路径 classPsychic）。
 public static class Druid {
  public static readonly Color Nature=new Color(.55f,.85f,.45f);
 }

 // 安抚野兽：发狂的动物恢复正常；野生动物按概率直接被驯服，失败时逃开。
 public class Ability_AnimalFriendship : ClassAbility {
  public static string Cannot(Pawn t){
   if(t==null || t.Dead)return "需要活着的动物。";
   if(!t.RaceProps.Animal)return "只能对动物使用。";
   if(t.Faction!=null && !t.InMentalState)return "这只动物已经有主人了。";
   return null;
  }
  public override bool ValidateTarget(LocalTargetInfo target,bool showMessages=true){
   if(!base.ValidateTarget(target,showMessages))return false;
   var why=Cannot(target.Thing as Pawn);if(why!=null){if(showMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
  public static float Chance(float strength,Pawn t)=>Mathf.Min(.9f,.35f*strength*(1f-t.GetStatValue(StatDefOf.Wildness)/2f));
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)Befriend(pawn,PawnOf(g),Strength,Rand.Value);}
  public static string Befriend(Pawn caster,Pawn t,float strength,float roll){
   if(Cannot(t)!=null)return "invalid";
   if(t.InMentalState && t.MentalStateDef!=null && t.MentalStateDef.IsAggro){t.MentalState.RecoverFromState();Items.Mote(t,"平静下来",Druid.Nature);return "calmed";}
   if(t.Faction!=null)return "owned";
   if(roll<Chance(strength,t)){
    InteractionWorker_RecruitAttempt.DoRecruit(caster,t);
    Items.Mote(t,"被驯服了",Druid.Nature);return "tamed";
   }
   t.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.PanicFlee,"德鲁伊的安抚",true);
   Items.Mote(t,"安抚失败",Druid.Nature);return "fled";
  }
 }
 // 纠缠术：目标格半径内的敌人被藤蔓缠住（体型 2 以上的减速较轻）。
 public class Ability_Entangle : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)if(g.Cell.IsValid)Entangle(pawn,g.Cell,Radius,Duration);}
  public static int Entangle(Pawn caster,IntVec3 c,float radius,int ticks){
   var map=caster.Map;if(map==null)return 0;int n=0;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(map))FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1f,new Color(.4f,.6f,.3f));
   foreach(var t in Races124.PawnsNear(map,c,radius).ToList()){if(t==caster || !t.HostileTo(caster))continue;Items.Give(t,"LP_Entangled",ticks,t.BodySize>=2f?2f:1f);n++;}
   return n;
  }
 }
 // 召唤野兽：身边召出灵体森林狼（强度 ≥ 1.25 时 3 只），1 天后或死亡时消失。
 public class Ability_ConjureAnimals : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){Conjure(pawn,Strength>=1.25f?3:2);Mote(pawn,"召唤野兽",Druid.Nature);}
  public static List<Pawn> Conjure(Pawn caster,int count){
   var made=new List<Pawn>();var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Wolf_Timber");var map=caster.Map;
   if(kind==null || map==null)return made;
   for(int i=0;i<count;i++){
    var w=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,caster.Faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:3,fixedChronologicalAge:3));
    var cell=CellFinder.RandomClosewalkCellNear(caster.Position,map,2);
    GenSpawn.Spawn(w,cell,map);
    Items.Give(w,"LP_Conjured");
    if(w.training!=null && w.Faction==Faction.OfPlayer){
     foreach(var td in new[]{TrainableDefOf.Tameness,TrainableDefOf.Obedience,TrainableDefOf.Release})if(td!=null && w.training.CanAssignToTrain(td).Accepted)w.training.Train(td,null,true);
     if(w.playerSettings!=null)w.playerSettings.Master=caster;
    }
    FleckMaker.ThrowDustPuffThick(w.DrawPos,map,2f,Druid.Nature);
    made.Add(w);
   }
   return made;
  }
 }
 // 荒野变形：熊形。近战换成熊爪/熊咬，伤害 × 野性强度，受伤 ×0.6，不能做精细工作；远程武器先收进背包，变回时装回。
 public class Ability_WildShape : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){Shift(pawn,Duration,Strength);Mote(pawn,"荒野变形",Druid.Nature);}
  public static Hediff Shift(Pawn p,int ticks,float strength)=>Items.Give(p,"LP_WildShape",ticks,strength);
 }
 public class Hediff_WildShape : Hediff_ClassScaled {
  public Thing stowed;
  public override void PostAdd(DamageInfo? dinfo){
   base.PostAdd(dinfo);
   var w=pawn.equipment?.Primary;
   if(w!=null && w.def.IsRangedWeapon && pawn.inventory!=null && pawn.equipment.TryTransferEquipmentToContainer(w,pawn.inventory.innerContainer))stowed=w;
   if(pawn.Spawned)FleckMaker.ThrowDustPuffThick(pawn.DrawPos,pawn.Map,3f,new Color(.55f,.4f,.25f));
  }
  public override void PostRemoved(){
   base.PostRemoved();
   if(stowed!=null && !stowed.Destroyed && pawn.inventory!=null && pawn.inventory.innerContainer.Contains(stowed) && pawn.equipment!=null && pawn.equipment.Primary==null && stowed is ThingWithComps eq){
    pawn.inventory.innerContainer.Remove(eq);pawn.equipment.AddEquipment(eq);
   }
   stowed=null;
   if(pawn.Spawned)FleckMaker.ThrowDustPuffThick(pawn.DrawPos,pawn.Map,3f,new Color(.55f,.4f,.25f));
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(pawn.Faction==Faction.OfPlayer)yield return new Command_Action{defaultLabel="变回原形",defaultDesc="结束荒野变形，恢复人形并装回收起的武器。",icon=ContentFinder<Texture2D>.Get("LP/Classes/Druid/WildShape",false)??BaseContent.BadTex,action=()=>pawn.health.RemoveHediff(this)};
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref stowed,"lpStowed");}
 }
 // 植物滋长：半径内所有植物生长度 + 0.25 × 生长强度，去掉枯萎病。
 public class Ability_PlantGrowth : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)if(g.Cell.IsValid)Grow(pawn.Map,g.Cell,Radius,Power);}
  public static int Grow(Map map,IntVec3 c,float radius,float amount){
   if(map==null)return 0;int n=0;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true)){
    if(!cell.InBounds(map))continue;
    var plant=cell.GetPlant(map);if(plant==null)continue;
    plant.Growth=Mathf.Min(1f,plant.Growth+amount);
    plant.Blight?.Destroy();
    plant.DirtyMapMesh(map);
    if(n%4==0)FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1f,Druid.Nature);
    n++;
   }
   return n;
  }
 }
 // 兽王号令：半径内本派系动物 1 小时近战伤害 ×(1 + 0.3 × 强度)、移动 ×1.2、受伤 ×0.8。
 public class Ability_BeastCommand : ClassAbility {
  public override void Apply(GlobalTargetInfo[] targets){Command(pawn,Radius,Duration,Strength);Mote(pawn,"兽王号令",Druid.Nature);}
  public static int Command(Pawn caster,float radius,int ticks,float strength){
   if(caster.Map==null)return 0;int n=0;
   foreach(var t in Races124.PawnsNear(caster.Map,caster.Position,radius).ToList()){if(!t.RaceProps.Animal || t.Faction==null || t.Faction!=caster.Faction)continue;Items.Give(t,"LP_BeastFrenzy",ticks,strength);n++;}
   return n;
  }
 }
 // 召雷术：落雷，半径 1.9 内 25 × 生长强度 电击；下雨或雷暴时伤害 ×1.5、冷却减半。
 public class Ability_CallLightning : ClassAbility {
  public static bool Stormy(Map map)=>map?.weatherManager?.curWeather!=null && map.weatherManager.curWeather.rainRate>0f;
  public override int GetCooldownForPawn()=>Stormy(pawn?.Map)?base.GetCooldownForPawn()/2:base.GetCooldownForPawn();
  public override void Apply(GlobalTargetInfo[] targets){foreach(var g in targets)if(g.Cell.IsValid)Strike(pawn,g.Cell,Power*(Stormy(pawn.Map)?1.5f:1f));}
  public static int Strike(Pawn caster,IntVec3 c,float amount){
   var map=caster.Map;if(map==null)return 0;int n=0;
   FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),map,3f);
   FleckMaker.ThrowMicroSparks(c.ToVector3Shifted(),map);
   SoundDefOf.Thunder_OnMap?.PlayOneShot(new TargetInfo(c,map));
   foreach(var t in Races124.PawnsNear(map,c,1.9f).ToList()){
    if(t==caster || !t.HostileTo(caster))continue;
    bool mech=t.RaceProps.IsMechanoid;
    t.TakeDamage(new DamageInfo(mech?DamageDefOf.EMP:(Ability_LightningBolt.Lightning??DamageDefOf.Burn),amount,.5f,-1f,caster));
    n++;
   }
   return n;
  }
 }
 // 掌控天气：选择天气维持 1 天，并结束本地图一个负面天气事件（任务、永久事件除外）。
 public class Ability_ControlWeather : ClassAbility {
  public static readonly string[] BadConditions={"ToxicFallout","ColdSnap","HeatWave","VolcanicWinter","Flashstorm","NoxiousHaze","Aurora_Bad"};
  public static List<WeatherDef> Choices(Map map){
   var l=new List<WeatherDef>();
   foreach(var n in new[]{"Clear","Rain","Fog","RainyThunderstorm"}){var w=DefDatabase<WeatherDef>.GetNamedSilentFail(n);if(w!=null)l.Add(w);}
   if(map!=null && map.mapTemperature.OutdoorTemp<=0f){var s=DefDatabase<WeatherDef>.GetNamedSilentFail("SnowGentle");if(s!=null)l.Add(s);}
   return l;
  }
  public override void Apply(GlobalTargetInfo[] targets){
   var map=pawn.Map;if(map==null)return;
   var ended=EndBadCondition(map);
   if(ended!=null)Messages.Message(pawn.LabelShort+"驱散了"+ended+"。",pawn,MessageTypeDefOf.PositiveEvent,false);
   var opts=Choices(map).Select(w=>new FloatMenuOption(w.LabelCap,()=>SetWeather(map,w))).ToList();
   if(opts.Count>0)Find.WindowStack.Add(new FloatMenu(opts,"选择天气（维持 1 天）"));
  }
  public static void SetWeather(Map map,WeatherDef w){
   map.weatherManager.TransitionTo(w);
   Traverse.Create(map.weatherDecider).Field("curWeatherDuration").SetValue(60000);
  }
  public static string EndBadCondition(Map map){
   foreach(var mgr in new[]{map.gameConditionManager,Find.World?.gameConditionManager}){
    if(mgr==null)continue;
    var c=mgr.ActiveConditions.FirstOrDefault(x=>BadConditions.Contains(x.def.defName) && !x.Permanent && x.quest==null);
    if(c!=null){string label=c.LabelCap;c.End();return label;}
   }
   return null;
  }
 }
}
