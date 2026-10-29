using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.24.0 四个普通种族（元素裔、象人、食人魔、鸟人）与四个传奇种族（寒冰魔、炎魔、泰坦、巨灵）的公共部分。
 // 种族判定按 1.20.0 的基因规则：本源基因激活即算本族。
 public static class Races124 {
  // 并发字典：这些查询会从属性、心情、寻路等钩子里调用，可能不在主线程。
  // 1.58.6：查不到的名字也缓存（按 DefCount，见 PerfCache.cs）；没装 HAR 时 HAR 专属基因原来每次都重查 DefDatabase。没有基因的小人（动物、机械体）先返回。
  public static GeneDef Gene(string name)=>DefCache<GeneDef>.Get(name);
  public static XenotypeDef Xeno(string name)=>DefCache<XenotypeDef>.Get(name);
  public static AbilityDef Ability(string name)=>DefCache<AbilityDef>.Get(name);
  public static bool Has(Pawn p,string gene)=>p?.genes!=null && CommonRaces.Has(p,Gene(gene));
  public static bool IsLoxodon(Pawn p)=>Has(p,"LP_LoxodonStature");
  public static bool IsOgre(Pawn p)=>Has(p,"LP_OgreStature");
  public static bool IsAarakocra(Pawn p)=>Has(p,"LP_AarakocraWings");
  public static bool IsTitan(Pawn p)=>Has(p,"LP_TitanDivinity");
  public static bool IsGenie(Pawn p)=>Has(p,"LP_GenieCore");
  // 不怕火的人：九狱之躯、火巨人、炽焰血脉（炎魔火鞭不烧手）。
  public static bool FireProof(Pawn p)=>Has(p,"LP_FiendCore") || FireGiant.Active(p) || Has(p,"LP_ElementFire");
  public static void Ensure(Pawn p,HediffDef def,bool want)=>Minotaur.Ensure(p,def,want);
  // 瞬移（风跃、翱翔共用；位移写法同吉斯洋基的星界跳跃）。
  public static void Blink(Pawn p,IntVec3 c){p.pather?.StopDead();p.Position=c;p.Notify_Teleported(false,true);}
  public static string BasicLanding(Pawn caster,IntVec3 c,int range){
   var map=caster?.Map;if(map==null)return "施法者不在地图上。";
   if(!c.IsValid || !c.InBounds(map))return "需要地图上的目标位置。";
   if(c==caster.Position)return "已经站在这里了。";
   if(c.DistanceTo(caster.Position)>range+.5f)return "超出距离（"+range+"格）。";
   if(c.Fogged(map))return "不能落到未探索的地方。";
   if(!c.Standable(map))return "那里没有可以站立的地方。";
   if(c.GetFirstPawn(map)!=null)return "那里已经有人了。";
   return null;
  }
  // 1.58.6：原来每次都 AllPawnsSpawned.ToList() 复制整张地图的小人。现在只把调用时在半径内的人放进快照（顺序不变），
  // 产出时照旧再查一次“没死、仍在半径内”。唯一的差别：遍历途中才从半径外进来的人不会出现（原来会）。
  public static IEnumerable<Pawn> PawnsNear(Map map,IntVec3 center,float radius){
   var all=map.mapPawns.AllPawnsSpawned;List<Pawn> near=null;
   for(int i=0;i<all.Count;i++){var p=all[i];if(!p.Dead && p.Position.InHorDistOf(center,radius))(near??(near=new List<Pawn>())).Add(p);}
   if(near==null)yield break;
   foreach(var p in near)if(!p.Dead && p.Position.InHorDistOf(center,radius))yield return p;
  }
  // 只读、只要一个结果的调用（提尔审判之庭、守护姿态）：不分配，直接按 AllPawnsSpawned 的顺序找第一个。
  public static Pawn FirstPawnNear(Map map,IntVec3 center,float radius,Func<Pawn,bool> pred){
   var all=map.mapPawns.AllPawnsSpawned;
   for(int i=0;i<all.Count;i++){var p=all[i];if(!p.Dead && p.Position.InHorDistOf(center,radius) && pred(p))return p;}
   return null;
  }
  public static Races124State State=>GameComp<Races124State>.Get();
 }

 // ---------- 基因附带的伤病：体型基因的食量（象人、食人魔、泰坦；照牛头人体型的写法）、寒冰魔尾刺 ----------
 public class LinkedHediffExtension : DefModExtension {public HediffDef hediff;}
 public class Gene_LinkedHediff : Gene {
  int ticks;
  HediffDef Linked=>def.GetModExtension<LinkedHediffExtension>()?.hediff;
  public override void PostAdd(){base.PostAdd();Races124.Ensure(pawn,Linked,true);}
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<2500)return;ticks=0;if(Active)Races124.Ensure(pawn,Linked,true);}
  public override void PostRemove(){Races124.Ensure(pawn,Linked,false);base.PostRemove();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticks,"races124BodyTicks",0);}
 }
 public class Hediff_GeneLinked : HediffWithComps {
  public override bool ShouldRemove{get{
   if(base.ShouldRemove)return true;var list=pawn?.genes?.GenesListForReading;if(list==null)return true;
   for(int i=0;i<list.Count;i++)if(list[i].Active && list[i].def.GetModExtension<LinkedHediffExtension>()?.hediff==def)return false;
   return true;
  }}
 }

 // ---------- 本批所有主动能力 ----------
 public class CompProperties_Races124Ability : CompProperties_AbilityEffect {
  public string effect;
  public CompProperties_Races124Ability(){compClass=typeof(CompRaces124Ability);}
 }
 public class CompRaces124Ability : CompAbilityEffect {
  public CompProperties_Races124Ability Props=>(CompProperties_Races124Ability)props;
  Pawn Caster=>parent.pawn;
  public override bool GizmoDisabled(out string reason){
   if(Genie.Fatigued(Caster) && Genasi.IsBloodlineAbility(parent.def)){reason="巨灵正处于许愿后的倦怠中，血脉能力暂时无法使用。";return true;}
   if(Props.effect=="smite" && !Titan.CanPay(Caster,Titan.SmiteCost)){reason="神威不足：需要"+Titan.SmiteCost.ToString("0")+"。";return true;}
   if(Props.effect=="breath" && !Titan.CanPay(Caster,Titan.BreathCost)){reason="神威不足：需要"+Titan.BreathCost.ToString("0")+"。";return true;}
   return base.GizmoDisabled(out reason);
  }
  public string Invalid(LocalTargetInfo target){
   var map=Caster?.Map;if(map==null)return "施法者不在地图上。";
   switch(Props.effect){
    case "spark":return Genasi.CannotSpark(Caster,target);
    case "tide":return !target.Cell.IsValid || !target.Cell.InBounds(map)?"需要地图上的目标位置。":target.Cell.DistanceTo(Caster.Position)>Genasi.TideRange+.5f?"超出距离（"+Genasi.TideRange+"格）。":null;
    case "windleap":return Genasi.CannotLeap(Caster,target.Cell);
    case "soar":return Aarakocra.CannotSoar(Caster,target.Cell);
    case "buffet":return target.Pawn!=null && target.Pawn.Position.DistanceTo(Caster.Position)>Fiends.BuffetRadius+.5f?"超出振翼范围（4格）。":null;
    case "smite":return !target.Cell.IsValid || !target.Cell.InBounds(map)?"需要地图上的目标位置。":target.Cell.DistanceTo(Caster.Position)>Titan.SmiteRange+.5f?"超出神罚距离（10格）。":target.Cell.Roofed(map) && target.Cell.GetRoof(map).isThickRoof?"闪电打不穿山体。":null;
    case "breath":case "wish":case "horn":return null;
   }
   return "未知的能力。";
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   string reason=Invalid(target);
   if(reason!=null && throwMessages)Messages.Message(reason,MessageTypeDefOf.RejectInput,false);
   return reason==null && base.Valid(target,throwMessages);
  }
  public override bool AICanTargetNow(LocalTargetInfo target){
   if(Props.effect!="buffet")return false;
   return target.Pawn!=null && target.Pawn.HostileTo(Caster) && Invalid(target)==null && Fiends.BuffetTargets(Caster).Any();
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   if(Invalid(target)!=null)return;
   switch(Props.effect){
    case "spark":Genasi.Spark(Caster,target);break;
    case "tide":Genasi.Tide(Caster,target.Cell);break;
    case "windleap":Genasi.Leap(Caster,target.Cell);break;
    case "soar":Aarakocra.Soar(Caster,target.Cell);break;
    case "buffet":Fiends.Buffet(Caster);break;
    case "smite":Titan.Smite(Caster,target.Cell);break;
    case "breath":Titan.Breath(Caster);break;
    case "wish":Genie.OpenWishDialog(Caster,parent);break;
    case "horn":RaceItems124.Horn(Caster);break;
   }
  }
  public override void DrawEffectPreview(LocalTargetInfo target){
   if(Caster?.Map==null)return;
   switch(Props.effect){
    case "tide":if(target.Cell.IsValid)GenDraw.DrawFieldEdges(CellRect.CenteredOn(target.Cell,Genasi.TideRadius(Caster)).ClipInsideMap(Caster.Map).Cells.ToList());break;
    case "smite":if(target.Cell.IsValid)GenDraw.DrawRadiusRing(target.Cell,1.9f);break;
   }
  }
 }

 // 能力冷却倍率：巨灵的血脉能力×0.5、佩戴对应元素符的本血脉×0.5、鸟人穿风羽披风翱翔×0.75（相乘）。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.StartCooldown))]
 public static class Races124Cooldown {
  static void Prefix(RimWorld.Ability __instance,ref int ticks){
   if(ticks<=0 || __instance?.pawn==null)return;
   float f=Races124Cooldowns.Factor(__instance.pawn,__instance.def);
   if(f!=1f)ticks=Math.Max(1,Mathf.RoundToInt(ticks*f));
  }
 }
 public static class Races124Cooldowns {
  public static float Factor(Pawn p,AbilityDef def){
   if(def==null)return 1f;float f=1f;
   var element=Genasi.ElementOfAbility(def);
   if(element!=null){
    if(Races124.IsGenie(p))f*=Genie.CooldownFactor;
    if(Genasi.Element(p)==element && ElementalFocus.Worn(p,element)!=null)f*=ElementalFocus.CooldownFactor;
   }
   if(def.defName=="LP_Soar" && Aarakocra.CloakBoost(p))f*=Aarakocra.CloakCooldownFactor;
   // 1.28.0：维拉基斯的神力灌注者星界跳跃冷却减半。
   if(def.defName=="LP_AstralStep" && Twilight.InfusedBy(p,"Vlaakith"))f*=TwilightSkills.VlaakithStepCooldown;
   return f;
  }
 }

 // ---------- 存档状态：炎魔死亡爆炸的倒计时、巨灵和平屏障、泰坦天候 ----------
 public class DelayedBurst : IExposable {
  public Map map;public IntVec3 cell;public int tick;public string label;
  public void ExposeData(){Scribe_References.Look(ref map,"map");Scribe_Values.Look(ref cell,"cell");Scribe_Values.Look(ref tick,"tick");Scribe_Values.Look(ref label,"label");}
 }
 public class Races124State : GameComponent {
  public List<DelayedBurst> bursts=new List<DelayedBurst>();
  public Dictionary<int,int> peaceUntil=new Dictionary<int,int>(),weatherDay=new Dictionary<int,int>();
  public Races124State(Game game){}
  public bool Peaceful(Map map)=>map!=null && peaceUntil.TryGetValue(map.uniqueID,out int t) && Find.TickManager.TicksGame<t;
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;
   if(bursts.Count>0)for(int i=bursts.Count-1;i>=0;i--){var b=bursts[i];if(now<b.tick)continue;bursts.RemoveAt(i);Fiends.Explode(b);}
   if(now%250==123)Titan.WeatherTick();
  }
  public void FlushBursts(){foreach(var b in bursts.ToList()){bursts.Remove(b);Fiends.Explode(b);}}
  public override void ExposeData(){
   Scribe_Collections.Look(ref bursts,"lpFiendBursts",LookMode.Deep);
   Scribe_Collections.Look(ref peaceUntil,"lpGeniePeace",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref weatherDay,"lpTitanWeatherDay",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){bursts=bursts?.Where(b=>b?.map!=null).ToList()??new List<DelayedBurst>();if(peaceUntil==null)peaceUntil=new Dictionary<int,int>();if(weatherDay==null)weatherDay=new Dictionary<int,int>();}
  }
 }

 // 1.24.0 设置：神裔影响天气。
 public partial class Settings {
  public bool titanWeather=true;
  void ExposeRaces124(){Scribe_Values.Look(ref titanWeather,"titanWeather",true);ExposeCommonRaces();}
 }
 public static class Races124Settings {
  public static void Draw(Listing_Standard l){l.GapLine();l.CheckboxLabeled("神裔影响天气",ref LichMod.Settings.titanWeather,"泰坦（天穹神裔）在地图上时，每天黎明看一次心情：心情≥80%天气转晴，≤30%转雷雨。关闭后泰坦不再影响天气。");Subraces.Draw(l);}
 }
}
