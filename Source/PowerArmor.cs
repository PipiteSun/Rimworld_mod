using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.124.0 奥能动力甲（Defs/PowerArmor.xml）：奥术核心驱动的秘铸骑士装甲。
 // 核心能量存在盔甲上（CompArcaneCore）；受击、征召待命、装甲能力消耗；奥能充能座（要电）或奥能电池充能；耗尽时“铁壳”状态（移动 / 操作大减）。
 // 每 250 刻由 GameComponent_PowerArmor 统一结算（只走登记过的盔甲，没人穿时第一步就返回）；没有渲染缓存，渲染线程不碰这里。
 public class CompProperties_ArcaneCore : CompProperties {
  public float capacity=100f;
  public bool leap,field,overload;
  public float fieldCost=PowerArmor.FieldCostPerDamage; // 力场每挡 1 点伤害耗多少能量
  public float overloadScale; // >0：过载冲击伤害 × 奥术强度
  public int attuneBonus; // 奇械型：植入物调谐位 +1
  public float arcanePower,divinePower; // 秘法炮手型 / 圣誓型：职业强度加成（强度属性由 StatPart_ClassPower 整个重算，装备偏移会被盖掉，所以走 StatPart_PowerArmor）
  public CompProperties_ArcaneCore(){compClass=typeof(CompArcaneCore);}
 }
 public class CompProperties_ArcaneHelm : CompProperties {public CompProperties_ArcaneHelm(){compClass=typeof(CompArcaneHelm);}}
 public class CompArcaneHelm : ThingComp {}

 public class CompArcaneCore : ThingComp {
  public float energy=-1f;
  public int lastLeap=-99999,lastField=-99999,lastOverload=-99999;
  public CompProperties_ArcaneCore Props=>(CompProperties_ArcaneCore)props;
  public Pawn Wearer=>(parent as Apparel)?.Wearer;
  public float Max=>PowerArmor.MaxEnergy(this,Wearer);
  public float Energy{get{if(energy<0f)energy=Props.capacity;return Mathf.Min(energy,Max);}set{energy=Mathf.Clamp(value,0f,Max);}}
  public bool Empty=>Energy<=.001f;
  public float Fraction=>Max<=0f?0f:Mathf.Clamp01(Energy/Max);
  public override void PostPostMake(){base.PostPostMake();energy=Props.capacity;}
  public override void Notify_Equipped(Pawn pawn){base.Notify_Equipped(pawn);PowerArmor.Live.Add(this);PowerArmor.Sync(pawn);}
  public override void Notify_Unequipped(Pawn pawn){base.Notify_Unequipped(pawn);PowerArmor.Sync(pawn,this);}
  public override void PostExposeData(){
   base.PostExposeData();
   Scribe_Values.Look(ref energy,"lpPaEnergy",-1f);Scribe_Values.Look(ref lastLeap,"lpPaLeap",-99999);Scribe_Values.Look(ref lastField,"lpPaField",-99999);Scribe_Values.Look(ref lastOverload,"lpPaOverload",-99999);
   if(Scribe.mode==LoadSaveMode.PostLoadInit)PowerArmor.Live.Add(this);
  }
  // 受击：力场开着就整个挡下（按伤害扣能量）；否则核心分担冲击，按伤害扣一点能量。
  public override void PostPreApplyDamage(ref DamageInfo dinfo,out bool absorbed){
   absorbed=false;var p=Wearer;if(p==null || dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f)return;
   if(PowerArmor.FieldOn(p) && !Empty){
    float cost=dinfo.Amount*Props.fieldCost;
    if(Energy>=cost){Energy-=cost;absorbed=true;PowerArmor.FieldFlash(p);return;}
    dinfo.SetAmount(dinfo.Amount-Energy/Math.Max(.01f,Props.fieldCost));Energy=0f;Items.Remove(p,PowerArmor.FieldHediff);Items.Mote(p,"力场崩溃",PowerArmor.CoreColor);PowerArmor.Sync(p);return;
   }
   if(!Empty){Energy-=dinfo.Amount*PowerArmor.HitDrain;if(Empty)PowerArmor.Sync(p);}
  }
  public override string CompInspectStringExtra()=>"奥能核心："+Energy.ToString("0")+" / "+Max.ToString("0")+(Empty?"（耗尽）":"");
  public override IEnumerable<Gizmo> CompGetWornGizmosExtra(){
   foreach(var g in base.CompGetWornGizmosExtra())yield return g;
   var p=Wearer;if(p==null || p.Faction!=Faction.OfPlayer)yield break;
   yield return new Gizmo_ArcaneCore(this);
   foreach(var g in PowerArmor.Commands(p,this))yield return g;
  }
 }

 public static class PowerArmor {
  public const float HitDrain=.25f,DraftDrain=1f,CradleRate=12f,CellCharge=60f,FieldCostPerDamage=.5f,HelmBonus=.2f,ArmorerBonus=.5f;
  public const float LeapCost=20f,FieldCost=30f,OverloadCost=40f,LeapMin=2f,LeapRange=12f,LeapRadius=1.9f,LeapDamage=10f,LeapChance=.6f,OverloadRadius=3.9f,OverloadDamage=14f,OverloadChance=.7f;
  public const int LeapCooldown=600,FieldCooldown=1200,OverloadCooldown=1800,FieldTicks=600,LeapStun=120,OverloadStun=150,ChargeMaxTicks=7500;
  public const string FieldHediff="LP_PA_Field",HuskHediff="LP_PA_Husk",CellDef="LP_ArcaneCell",CradleDef="LP_ArcaneCradle",FoundryDef="LP_ArcaneFoundry";
  public static readonly Color CoreColor=new Color(.45f,.85f,1f);
  public static readonly HashSet<CompArcaneCore> Live=new HashSet<CompArcaneCore>();
  public static float? TestRoll;public static int lastLandStunned; // 自检用
  static float Roll=>TestRoll??Rand.Value;
  static int Now=>Find.TickManager.TicksGame;
  static readonly DefRef<JobDef> chargeJob=new DefRef<JobDef>("LP_PA_Charge");
  static readonly DefRef<ThingDef> leapFlyer=new DefRef<ThingDef>("LP_PA_LeapFlyer");

  public static CompArcaneCore Core(Pawn p){
   var list=p?.apparel?.WornApparel;if(list==null || Live.Count==0)return null;
   for(int i=0;i<list.Count;i++){var c=list[i].GetComp<CompArcaneCore>();if(c!=null)return c;}
   return null;
  }
  public static bool HasHelm(Pawn p){var list=p?.apparel?.WornApparel;if(list==null)return false;for(int i=0;i<list.Count;i++)if(list[i].GetComp<CompArcaneHelm>()!=null)return true;return false;}
  // 上限 = 基础容量 ×（戴配套头盔 1.2）×（装甲师把它改成了奥术装甲 1.5）。
  public static float MaxEnergy(CompArcaneCore c,Pawn p){
   float m=c.Props.capacity;if(p==null)return m;
   if(HasHelm(p))m*=1f+HelmBonus;
   if(Subclass4.ArcaneArmor(p)==c.parent)m*=1f+ArmorerBonus;
   return m;
  }
  public static int AttuneBonus(Pawn p){var c=Core(p);return c==null?0:c.Props.attuneBonus;}
  public static bool FieldOn(Pawn p)=>Items.Has(p,FieldHediff);

  // 铁壳状态跟着能量走；脱下（gone 是刚脱下的那件）时去掉。
  public static void Sync(Pawn p,CompArcaneCore gone=null){
   if(p?.health==null || p.Dead)return;var c=Core(p);if(c==gone)c=null;
   bool husk=c!=null && c.Empty;
   if(husk && !Items.Has(p,HuskHediff)){Items.Give(p,HuskHediff);Items.Mote(p,"奥能耗尽：铁壳",new Color(.7f,.7f,.72f));}
   else if(!husk)Items.Remove(p,HuskHediff);
   if(c==null || c.Empty)Items.Remove(p,FieldHediff);
  }
  // 每 250 刻：征召中的穿戴者扣能量，顺带清理已经没人穿的登记。
  public static void Tick250(){
   if(Live.Count==0)return;
   List<CompArcaneCore> drop=null;
   foreach(var c in Live){
    var p=c.Wearer;if(p==null || c.parent.Destroyed || p.Destroyed){(drop??(drop=new List<CompArcaneCore>())).Add(c);continue;}
    if(p.Dead)continue;
    if(p.Drafted && !c.Empty){c.Energy-=DraftDrain;}
    Sync(p);
   }
   if(drop!=null)foreach(var c in drop)Live.Remove(c);
  }
  public static void Charge(CompArcaneCore c,float amount){if(c==null)return;bool was=c.Empty;c.Energy+=amount;if(was && !c.Empty)Sync(c.Wearer);}

  // ---------- 能力 ----------
  static string Cannot(Pawn p,CompArcaneCore c,float cost,int last,int cd){
   if(p.Downed)return "倒地时不能使用。";
   if(c.Empty)return "奥能耗尽了，先充能。";
   if(c.Energy+.001f<cost)return "奥能不足（需要 "+cost.ToString("0")+"）。";
   if(Now-last<cd && Now>=last)return "冷却中（还要 "+((cd-(Now-last))/60f).ToString("0")+" 秒）。";
   return null;
  }
  public static string CannotLeap(Pawn p,CompArcaneCore c)=>!c.Props.leap?"这件甲没有推进器。":!p.Spawned?"不在地图上。":Cannot(p,c,LeapCost,c.lastLeap,LeapCooldown);
  public static string CannotField(Pawn p,CompArcaneCore c)=>!c.Props.field?"这件甲没有力场发生器。":FieldOn(p)?"力场已经开着。":Cannot(p,c,FieldCost,c.lastField,FieldCooldown);
  public static string CannotOverload(Pawn p,CompArcaneCore c)=>!c.Props.overload?"这件甲不能过载。":!p.Spawned?"不在地图上。":Cannot(p,c,OverloadCost,c.lastOverload,OverloadCooldown);
  public static string BadLeapCell(Pawn p,IntVec3 cell){
   var map=p.Map;if(map==null || !cell.IsValid || !cell.InBounds(map))return "落点无效。";
   float d=cell.DistanceTo(p.Position);if(d<LeapMin)return "太近了。";if(d>LeapRange)return "超出跃击距离（"+LeapRange.ToString("0")+" 格）。";
   if(cell.Fogged(map) || !cell.Standable(map) || cell.GetFirstPawn(map)!=null)return "落不了脚。";
   if(cell.Roofed(map) && p.Position.Roofed(map) && !GenSight.LineOfSight(p.Position,cell,map))return "中间挡着。";
   return null;
  }
  public static bool Leap(Pawn p,CompArcaneCore c,IntVec3 cell){
   if(CannotLeap(p,c)!=null || BadLeapCell(p,cell)!=null)return false;
   c.Energy-=LeapCost;c.lastLeap=Math.Max(1,Now);
   var map=p.Map;var origin=p.Position;Items.Mote(p,"推进跃击",CoreColor);FleckMaker.ThrowDustPuffThick(p.DrawPos,map,1.6f,new Color(.7f,.8f,.9f));
   var def=leapFlyer.Def;var flyer=def==null?null:PawnFlyer.MakeFlyer(def,p,cell,null,null);
   if(flyer==null){if(!p.Spawned)GenSpawn.Spawn(p,origin,map);Races124.Blink(p,cell);Land(p);}
   else GenSpawn.Spawn(flyer,origin,map);
   Sync(p);return true;
  }
  // 落地：1.9 格内的敌人受钝击，成功率 60% 被震倒（眩晕 2 秒）。
  public static int Land(Pawn p){
   if(p==null || !p.Spawned || p.Dead)return 0;var map=p.Map;int n=0;
   FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2.2f,new Color(.6f,.6f,.62f));
   foreach(var t in Races124.PawnsNear(map,p.Position,LeapRadius).ToList()){
    if(t==p || t.Dead || !t.HostileTo(p))continue;
    t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,LeapDamage,.2f,-1f,p));
    if(!t.Dead && Roll<LeapChance){t.stances?.stunner?.StunFor(LeapStun,p,false,true,false);n++;}
   }
   lastLandStunned=n;return n;
  }
  public static bool Field(Pawn p,CompArcaneCore c){
   if(CannotField(p,c)!=null)return false;
   c.Energy-=FieldCost;c.lastField=Math.Max(1,Now);Items.Give(p,FieldHediff,FieldTicks);
   Items.Mote(p,"奥能力场",CoreColor);FieldFlash(p);return true;
  }
  public static void FieldFlash(Pawn p){if(p.Spawned)FleckMaker.ThrowLightningGlow(p.DrawPos,p.Map,1.4f);}
  public static float OverloadAmount(Pawn p,CompArcaneCore c){
   float a=OverloadDamage;if(c.Props.overloadScale>0f){var st=DefDatabase<StatDef>.GetNamedSilentFail("LP_ArcanePower");if(st!=null)a*=Mathf.Max(.5f,p.GetStatValue(st))*c.Props.overloadScale;}
   return a;
  }
  // 过载冲击：3.9 格内的敌人受钝击，成功率 70% 被震晕 2.5 秒。
  public static int Overload(Pawn p,CompArcaneCore c){
   if(CannotOverload(p,c)!=null)return -1;
   c.Energy-=OverloadCost;c.lastOverload=Math.Max(1,Now);var map=p.Map;float amount=OverloadAmount(p,c);int n=0;
   Items.Mote(p,"过载冲击",CoreColor);
   foreach(var cell in GenRadial.RadialCellsAround(p.Position,OverloadRadius,true))if(cell.InBounds(map) && Rand.Chance(.35f))FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(),map);
   FleckMaker.ThrowLightningGlow(p.DrawPos,map,OverloadRadius);
   foreach(var t in Races124.PawnsNear(map,p.Position,OverloadRadius).ToList()){
    if(t==p || t.Dead || !t.HostileTo(p))continue;
    t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,amount,.3f,-1f,p));
    if(!t.Dead && Roll<OverloadChance){t.stances?.stunner?.StunFor(OverloadStun,p,false,true,false);n++;}
   }
   Sync(p);return n;
  }

  // ---------- 充能 ----------
  public static Thing CarriedCell(Pawn p){var inv=p?.inventory?.innerContainer;if(inv==null)return null;foreach(var t in inv)if(t.def.defName==CellDef)return t;return null;}
  public static bool UseCarriedCell(Pawn p,CompArcaneCore c){
   var cell=CarriedCell(p);if(cell==null || c.Energy>=c.Max-.5f)return false;
   if(cell.stackCount>1)cell.SplitOff(1).Destroy();else cell.Destroy();
   Charge(c,CellCharge);Items.Mote(p,"换上奥能电池",CoreColor);return true;
  }
  public static Thing NearestCell(Pawn p)=>p.Map==null?null:GenClosest.ClosestThingReachable(p.Position,p.Map,ThingRequest.ForDef(DefDatabase<ThingDef>.GetNamedSilentFail(CellDef)),PathEndMode.ClosestTouch,TraverseParms.For(p),40f,t=>!t.IsForbidden(p) && p.CanReserve(t));
  public static bool Powered(Thing cradle)=>cradle!=null && cradle.Spawned && (cradle.TryGetComp<CompPowerTrader>()?.PowerOn??false);
  public static Thing NearestCradle(Pawn p)=>p.Map==null?null:GenClosest.ClosestThingReachable(p.Position,p.Map,ThingRequest.ForDef(DefDatabase<ThingDef>.GetNamedSilentFail(CradleDef)),PathEndMode.InteractionCell,TraverseParms.For(p),9999f,t=>Powered(t) && !t.IsForbidden(p) && p.CanReserve(t));
  public static string CannotCharge(Pawn p,Thing cradle){
   var c=Core(p);if(c==null)return "没有穿奥能动力甲。";
   if(c.Energy>=c.Max-.5f)return "奥能已经满了。";
   if(!Powered(cradle))return "充能座没有通电。";
   if(!p.CanReserveAndReach(cradle,PathEndMode.InteractionCell,Danger.Deadly))return "过不去或者有人在用。";
   return null;
  }
  public static bool OrderCharge(Pawn p,Thing cradle){
   if(cradle==null || CannotCharge(p,cradle)!=null || chargeJob.Def==null)return false;
   return p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(chargeJob.Def,cradle),JobTag.Misc);
  }

  // ---------- 按钮 ----------
  static Texture2D icon(string n)=>ContentFinder<Texture2D>.Get("LP/PowerArmor/UI/"+n,false)??BaseContent.BadTex;
  public static IEnumerable<Gizmo> Commands(Pawn p,CompArcaneCore c){
   if(c.Props.leap){
    var cmd=new Command_Action{defaultLabel="推进跃击",defaultDesc="背后的奥能推进器喷出一道蓝焰，跃到 "+LeapMin.ToString("0")+"～"+LeapRange.ToString("0")+" 格外的落点（消耗 "+LeapCost.ToString("0")+" 奥能，冷却 "+(LeapCooldown/60)+" 秒）。落地时 "+LeapRadius.ToString("0.#")+" 格内的敌人受到 "+LeapDamage.ToString("0")+" 点钝击，成功率 "+LeapChance.ToStringPercent()+" 被震倒（眩晕 "+(LeapStun/60f).ToString("0.#")+" 秒）。",icon=icon("Leap")};
    cmd.action=()=>Find.Targeter.BeginTargeting(new TargetingParameters{canTargetLocations=true,canTargetPawns=false,canTargetBuildings=false,canTargetItems=false},t=>{string bad=BadLeapCell(p,t.Cell);if(bad!=null){Messages.Message(bad,MessageTypeDefOf.RejectInput,false);return;}Leap(p,c,t.Cell);},p);
    string why=CannotLeap(p,c);if(why!=null)cmd.Disable(why);yield return cmd;
   }
   if(c.Props.field){
    var cmd=new Command_Action{defaultLabel="奥能力场",defaultDesc="展开贴身的奥能力场 "+(FieldTicks/60)+" 秒（消耗 "+FieldCost.ToString("0")+" 奥能，冷却 "+(FieldCooldown/60)+" 秒）：力场开着时所有伤害都被挡下，每挡 1 点伤害消耗 "+c.Props.fieldCost.ToString("0.##")+" 奥能；奥能耗尽时力场崩溃。",icon=icon("Field"),action=()=>Field(p,c)};
    string why=CannotField(p,c);if(why!=null)cmd.Disable(why);yield return cmd;
   }
   if(c.Props.overload){
    var cmd=new Command_Action{defaultLabel="过载冲击",defaultDesc="让核心瞬间过载，向四周放出一圈奥能冲击（消耗 "+OverloadCost.ToString("0")+" 奥能，冷却 "+(OverloadCooldown/60)+" 秒）："+OverloadRadius.ToString("0.#")+" 格内的敌人受到 "+OverloadAmount(p,c).ToString("0")+" 点钝击"+(c.Props.overloadScale>0f?"（随奥术强度）":"")+"，成功率 "+OverloadChance.ToStringPercent()+" 被震晕 "+(OverloadStun/60f).ToString("0.#")+" 秒。不伤友军。",icon=icon("Overload"),action=()=>Overload(p,c)};
    string why=CannotOverload(p,c);if(why!=null)cmd.Disable(why);yield return cmd;
   }
   var charge=new Command_Action{defaultLabel="充能",defaultDesc="身上带着奥能电池就立刻换上一块（+"+CellCharge.ToString("0")+"）；否则去最近一座通电的奥能充能座充能，每小时 +"+(CradleRate*10f).ToString("0")+"，充满或被打断为止；没有充能座时去拿附近的奥能电池。",icon=icon("Charge")};
   charge.action=()=>{
    if(UseCarriedCell(p,c))return;
    var cr=NearestCradle(p);if(cr!=null && OrderCharge(p,cr))return;
    var cell=NearestCell(p);var use=cell?.TryGetComp<CompUsable>();
    if(use!=null){use.TryStartUseJob(p,cell,false);return;}
    Messages.Message("附近没有通电的奥能充能座，也没有奥能电池。",MessageTypeDefOf.RejectInput,false);
   };
   if(c.Energy>=c.Max-.5f)charge.Disable("奥能已经满了。");else if(p.Downed)charge.Disable("倒地时不能充能。");
   yield return charge;
  }
  // 测试用：不弹窗地拿到按钮。
  public static List<Gizmo> GizmosFor(Pawn p){var c=Core(p);return c==null?new List<Gizmo>():c.CompGetWornGizmosExtra().ToList();}

  // ---------- NPC：少量精英穿戴 ----------
  public static readonly Dictionary<string,(string armor,string helm,float chance)[]> Elites=new Dictionary<string,(string,string,float)[]>{
   {"LP_FC_Knight",new[]{("LP_PA_SquireArmor","LP_PA_SquireHelm",.10f),("LP_PA_KnightArmor","LP_PA_KnightHelm",.04f)}},
   {"LP_FC_Paladin",new[]{("LP_PA_OathArmor","LP_PA_KnightHelm",.06f),("LP_PA_BastionArmor","LP_PA_BastionHelm",.02f)}},
   {"LP_FH_GithMercenary",new[]{("LP_PA_KnightArmor","LP_PA_KnightHelm",.06f)}},
   {"LP_BN_Enforcer",new[]{("LP_PA_SquireArmor","LP_PA_SquireHelm",.08f)}}};
  public static string Outfit(Pawn p,float? roll=null){
   var kind=p?.kindDef?.defName;if(kind==null || p.apparel==null || !Elites.TryGetValue(kind,out var list))return null;
   float r=roll??Rand.ValueSeeded(p.thingIDNumber^0x50A4),acc=0f;
   foreach(var e in list){
    acc+=e.chance;if(r>=acc)continue;
    var armor=Items.Thing(e.armor);if(armor==null || !ApparelUtility.HasPartsToWear(p,armor))return null;
    var a=(Apparel)ThingMaker.MakeThing(armor);a.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Outsider);p.apparel.Wear(a,false);
    var hd=Items.Thing(e.helm);if(hd!=null && ApparelUtility.HasPartsToWear(p,hd)){var h=(Apparel)ThingMaker.MakeThing(hd);h.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Normal,ArtGenerationContext.Outsider);p.apparel.Wear(h,false);}
    return e.armor;
   }
   return null;
  }
 }

 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class PowerArmorOnGeneration {
  [HarmonyPriority(Priority.Last)]
  static void Postfix(Pawn __result){if(__result?.kindDef!=null && PowerArmor.Elites.ContainsKey(__result.kindDef.defName))PowerArmor.Outfit(__result);}
 }

 // 奥术 / 神恩强度加成：加在强度属性最后（接在 StatPart_ClassPower 的重算之后）。
 public class StatPart_PowerArmor : StatPart {
  public bool divine;
  float Bonus(StatRequest req){if(PowerArmor.Live.Count==0 || !(req.Thing is Pawn p))return 0f;var c=PowerArmor.Core(p);return c==null?0f:divine?c.Props.divinePower:c.Props.arcanePower;}
  public override void TransformValue(StatRequest req,ref float val){val+=Bonus(req);}
  public override string ExplanationPart(StatRequest req){float b=Bonus(req);return b==0f?null:"奥能动力甲：+"+b.ToStringPercent();}
 }
 [StaticConstructorOnStartup]
 public static class PowerArmorStats {
  static PowerArmorStats(){Add("LP_ArcanePower",false);Add("LP_DivinePower",true);}
  static void Add(string stat,bool divine){var s=DefDatabase<StatDef>.GetNamedSilentFail(stat);if(s==null)return;if(s.parts==null)s.parts=new List<StatPart>();s.parts.Add(new StatPart_PowerArmor{divine=divine,parentStat=s});}
 }

 public class GameComponent_PowerArmor : GameComponent {
  public GameComponent_PowerArmor(Game game){PowerArmor.Live.Clear();}
  public override void GameComponentTick(){if(PowerArmor.Live.Count>0 && Find.TickManager.TicksGame%250==0)PowerArmor.Tick250();}
 }

 public class LP_PA_LeapFlyer : PawnFlyer {
  protected override void RespawnPawn(){var p=FlyingPawn;base.RespawnPawn();if(p!=null)PowerArmor.Land(p);}
 }

 // 奥能电池：用掉一块，给身上的奥能动力甲 +60。
 public class CompProperties_ArcaneCell : CompProperties_UseEffect {public CompProperties_ArcaneCell(){compClass=typeof(CompUseEffect_ArcaneCell);}}
 public class CompUseEffect_ArcaneCell : CompUseEffect {
  public override AcceptanceReport CanBeUsedBy(Pawn p){
   var c=PowerArmor.Core(p);if(c==null)return "没有穿奥能动力甲。";
   if(c.Energy>=c.Max-.5f)return "奥能已经满了。";
   return base.CanBeUsedBy(p);
  }
  public override void DoEffect(Pawn usedBy){base.DoEffect(usedBy);var c=PowerArmor.Core(usedBy);if(c==null)return;PowerArmor.Charge(c,PowerArmor.CellCharge);Items.Mote(usedBy,"换上奥能电池",PowerArmor.CoreColor);}
 }

 // 奥能充能座：站到交互格上充能，通电时每 250 刻 +12，满了或 3 小时后结束。
 public class JobDriver_ArcaneCharge : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
   this.FailOn(()=>!PowerArmor.Powered(TargetThingA) || PowerArmor.Core(pawn)==null);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.InteractionCell);
   var wait=ToilMaker.MakeToil("LP_PA_Charge");
   wait.defaultCompleteMode=ToilCompleteMode.Delay;wait.defaultDuration=PowerArmor.ChargeMaxTicks;
   wait.tickIntervalAction=d=>{
    pawn.rotationTracker.FaceTarget(TargetA);
    if(!pawn.IsHashIntervalTick(250,d))return;
    var c=PowerArmor.Core(pawn);if(c==null){EndJobWith(JobCondition.Incompletable);return;}
    PowerArmor.Charge(c,PowerArmor.CradleRate);FleckMaker.ThrowLightningGlow(pawn.DrawPos,pawn.Map,.8f);
    if(c.Energy>=c.Max-.5f)EndJobWith(JobCondition.Succeeded);
   };
   wait.WithProgressBar(TargetIndex.A,()=>PowerArmor.Core(pawn)?.Fraction??1f);
   yield return wait;
  }
 }
 // 右键充能座：去充能。
 public class CompProperties_ArcaneCradle : CompProperties {public CompProperties_ArcaneCradle(){compClass=typeof(CompArcaneCradle);}}
 public class CompArcaneCradle : ThingComp {
  public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn p){
   foreach(var o in base.CompFloatMenuOptions(p))yield return o;
   if(PowerArmor.Core(p)==null)yield break;
   string why=PowerArmor.CannotCharge(p,parent);
   yield return why==null?new FloatMenuOption("给奥能动力甲充能",()=>PowerArmor.OrderCharge(p,parent)):new FloatMenuOption("不能充能："+why,null);
  }
 }

 [StaticConstructorOnStartup]
 public class Gizmo_ArcaneCore : Gizmo {
  static readonly Texture2D Fill=SolidColorMaterials.NewSolidColorTexture(new Color(.3f,.72f,.95f));
  static readonly Texture2D Low=SolidColorMaterials.NewSolidColorTexture(new Color(.85f,.4f,.25f));
  static readonly Texture2D Empty=SolidColorMaterials.NewSolidColorTexture(new Color(.06f,.09f,.12f));
  readonly CompArcaneCore c;
  public Gizmo_ArcaneCore(CompArcaneCore c){this.c=c;Order=-95f;}
  public override float GetWidth(float maxWidth)=>140f;
  public override GizmoResult GizmoOnGUI(Vector2 topLeft,float maxWidth,GizmoRenderParms parms){
   var r=new Rect(topLeft.x,topLeft.y,GetWidth(maxWidth),75f);Widgets.DrawWindowBackground(r);
   var inner=r.ContractedBy(6f);
   Text.Font=GameFont.Small;Widgets.Label(new Rect(inner.x,inner.y,inner.width,22f),c.Empty?"奥能核心（耗尽）":"奥能核心");
   var bar=new Rect(inner.x,inner.y+24f,inner.width,inner.height-24f);
   Widgets.FillableBar(bar,c.Fraction,c.Fraction<.25f?Low:Fill,Empty,true);
   Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(bar,c.Energy.ToString("0")+" / "+c.Max.ToString("0"));Text.Anchor=TextAnchor.UpperLeft;
   if(Mouse.IsOver(r))TooltipHandler.TipRegion(r,"奥能："+c.Energy.ToString("0.#")+" / "+c.Max.ToString("0")+"\n受到伤害时核心分担冲击，每点伤害 -"+PowerArmor.HitDrain.ToString("0.##")+"；征召时每小时 -"+(PowerArmor.DraftDrain*10f).ToString("0")+"；装甲能力另外消耗。\n耗尽时变成沉重的铁壳：移动 -35%、操作 -25%，能力不能用。\n在通电的奥能充能座充能（每小时 +"+(PowerArmor.CradleRate*10f).ToString("0")+"），或换上奥能电池（+"+PowerArmor.CellCharge.ToString("0")+"）。\n戴配套头盔时上限 +20%；装甲师把它改成奥术装甲时上限 +50%。");
   return new GizmoResult(GizmoState.Clear);
  }
 }
}
