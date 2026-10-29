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
 // 1.45.0 魔法物品的主动效果：物品附带的能力（CompMagicAbility）、使用类物品（CompMagicUse）、储法戒指、誓约戒指、魂石、
 // 不可移动之杖、无尽水瓶、水晶球、召唤物；以及每小时的再生/怪事/复苏核心/徽记赠礼和敌人死亡掉落。
 public class CompProperties_MagicAbility : CompProperties_AbilityEffect {
  public string effect;public float radius;public string shareCooldown;
  public CompProperties_MagicAbility(){compClass=typeof(CompMagicAbility);}
 }
 public class CompMagicAbility : CompAbilityEffect {
  public CompProperties_MagicAbility Props=>(CompProperties_MagicAbility)props;
  Pawn Caster=>parent.pawn;
  public string Invalid(LocalTargetInfo target){
   var map=Caster?.Map;if(map==null)return "施法者不在地图上。";var p=target.Pawn;
   switch(Props.effect){
    case "flametoggle":return Caster.equipment?.Primary?.GetComp<CompMagicWeapon>()==null?"需要手持火焰舌。":null;
    case "defend":return Items.Holds(Caster,"LP_DefenderSword")?null:"需要手持守护者之剑。";
    case "readmind":return p==null || p==Caster || p.Dead || !p.Spawned || !p.RaceProps.Humanlike?"需要地图上活着的人形目标。":null;
    case "storedspell":var ring=SpellStoring.Ring(Caster);if(ring==null || ring.stored==null)return "储法戒指是空的。";return MagicEffects.StoredInvalid(ring.stored,Caster,target);
    case "soothe":return p==null || !p.RaceProps.Animal || !p.InMentalState?"需要一只发狂的动物。":null;
    case "meteor":case "fireball":return !target.Cell.IsValid || !target.Cell.InBounds(map)?"需要地图上的目标位置。":Props.effect=="fireball" && (Items.Worn(Caster,"LP_NecklaceFireballs")?.GetComp<CompMagicCharges>()?.Charges??0)<=0?"火球项链上已经没有珠子了。":null;
    case "prayheal":if(MindFlayer.IsMindFlayer(Caster) || Items.IsLich(Caster))return "巫妖和夺心魔用不了祈祷念珠。";return p==null || p.Dead || !p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any(i=>!i.IsPermanent())?"需要有伤口的人。":null;
    case "praybless":return MindFlayer.IsMindFlayer(Caster) || Items.IsLich(Caster)?"巫妖和夺心魔用不了祈祷念珠。":null;
    case "astral":return Githyanki.CannotStep(Caster,target.Cell);
    case "horn":return null;
   }
   return "未知的能力。";
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){string reason=Invalid(target);if(reason!=null && throwMessages)Messages.Message(reason,MessageTypeDefOf.RejectInput,false);return reason==null && base.Valid(target,throwMessages);}
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){if(Invalid(target)!=null)return;MagicEffects.ApplyAbility(Props.effect,Caster,target,Props.radius);}
  public override void DrawEffectPreview(LocalTargetInfo target){if(Props.radius>0 && target.Cell.IsValid)GenDraw.DrawRadiusRing(Props.effect=="praybless"?Caster.Position:target.Cell,Props.radius);}
 }
 public class CompProperties_MagicUse : CompProperties_UseEffect {
  public string effect;public int cooldownTicks;
  public CompProperties_MagicUse(){compClass=typeof(CompMagicUse);}
 }
 public class CompMagicUse : CompUseEffect {
  public int lastUse=-9999999;
  public new CompProperties_MagicUse Props=>(CompProperties_MagicUse)props;
  public string Cannot(Pawn p){
   if(Props.cooldownTicks>0 && Find.TickManager.TicksGame-lastUse<Props.cooldownTicks)return "还要等"+((Props.cooldownTicks-(Find.TickManager.TicksGame-lastUse))/(float)GenDate.TicksPerHour).ToString("0.#")+"小时才能再用。";
   switch(Props.effect){
    case "golemmanual":return Items.IsLich(p)?(Items.Has(p,"LP_GolemLore")?"已经读过魔像手册了。":null):"只有巫妖读得懂魔像手册。";
    case "purityseal":if(!p.apparel?.WornApparel.Any(a=>a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))??true)return "要穿着躯干衣物才能贴净化封印。";return (p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_PuritySeals"))?.Severity??0)>=3f?"已经贴满3张了。":null;
   }
   return null;
  }
  public override AcceptanceReport CanBeUsedBy(Pawn p){var r=Cannot(p);return r!=null?(AcceptanceReport)r:base.CanBeUsedBy(p);}
  public override void DoEffect(Pawn usedBy){
   base.DoEffect(usedBy);if(Cannot(usedBy)!=null)return;
   if(MagicEffects.Use(Props.effect,usedBy,parent))lastUse=Find.TickManager.TicksGame;
  }
  public override string CompInspectStringExtra()=>Props.cooldownTicks>0 && Find.TickManager.TicksGame-lastUse<Props.cooldownTicks?"冷却中："+((Props.cooldownTicks-(Find.TickManager.TicksGame-lastUse))/(float)GenDate.TicksPerHour).ToString("0.#")+"小时":null;
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref lastUse,"lpLastUse",-9999999);}
 }

 // ---------- 储法戒指 ----------
 public class CompProperties_SpellStoring : CompProperties {public CompProperties_SpellStoring(){compClass=typeof(CompSpellStoring);}}
 public class CompSpellStoring : ThingComp {
  public string stored,storedLabel;
  public override string TransformLabel(string label)=>stored==null?label+"（空）":label+"（存着："+storedLabel+"）";
  public void Store(string effect,string label){stored=effect;storedLabel=label;}
  public override IEnumerable<Gizmo> CompGetWornGizmosExtra(){
   foreach(var g in base.CompGetWornGizmosExtra())yield return g;
   var p=(parent as Apparel)?.Wearer;if(p==null || p.Faction!=Faction.OfPlayer)yield break;
   var cmd=new Command_Action{defaultLabel="存入法术",defaultDesc="把身上或3格内的一张法术卷轴读进储法戒指（卷轴消耗）。巫妖也可以花30点精神力直接存入恐惧凝视或死灵冲击。",icon=parent.def.uiIcon,action=()=>Find.WindowStack.Add(new FloatMenu(SpellStoring.Options(p,this)))};
   if(stored!=null)cmd.Disable("戒指里已经存着"+storedLabel+"。");
   yield return cmd;
  }
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref stored,"lpStored");Scribe_Values.Look(ref storedLabel,"lpStoredLabel");}
 }
 public static class SpellStoring {
  public const float LichCost=30f;
  public static CompSpellStoring Ring(Pawn p)=>Items.Worn(p,"LP_RingSpellStoring")?.GetComp<CompSpellStoring>();
  public static IEnumerable<Thing> Scrolls(Pawn p){
   if(p.inventory!=null)foreach(var t in p.inventory.innerContainer)if(t.TryGetComp<CompScroll>()!=null)yield return t;
   if(p.Spawned)foreach(var c in GenRadial.RadialCellsAround(p.Position,3f,true))if(c.InBounds(p.Map))foreach(var t in c.GetThingList(p.Map))if(t.TryGetComp<CompScroll>()!=null && !t.IsForbidden(p))yield return t;
  }
  public static bool StoreScroll(Pawn p,Thing scroll){var ring=Ring(p);var c=scroll?.TryGetComp<CompScroll>();if(ring==null || c==null || ring.stored!=null)return false;ring.Store(c.Props.effect,scroll.def.label.Replace("卷轴",""));ItemScrolls.Consume(scroll);Items.Mote(p,"法术存进戒指",new Color(.6f,.8f,1f));return true;}
  public static bool StoreLich(Pawn p,string effect,string label){var ring=Ring(p);if(ring==null || ring.stored!=null || !Necro.Pay(p,LichCost))return false;ring.Store(effect,label);Items.Mote(p,"法术存进戒指",new Color(.6f,.8f,1f));return true;}
  public static List<FloatMenuOption> Options(Pawn p,CompSpellStoring ring){
   var opts=Scrolls(p).Distinct().Select(t=>new FloatMenuOption("存入"+t.LabelNoCount,()=>StoreScroll(p,t))).ToList();
   if(Items.IsLich(p)){opts.Add(new FloatMenuOption("存入恐惧凝视（30精神力）",()=>StoreLich(p,"feargaze","恐惧凝视")));opts.Add(new FloatMenuOption("存入死灵冲击（30精神力）",()=>StoreLich(p,"necroblast","死灵冲击")));}
   if(opts.Count==0)opts.Add(new FloatMenuOption("身边没有法术卷轴",null));
   return opts;
  }
 }

 // ---------- 护佑誓约戒指 ----------
 public class CompProperties_WardingBond : CompProperties {public CompProperties_WardingBond(){compClass=typeof(CompWardingBond);}}
 public class CompWardingBond : ThingComp {
  public Thing partner;public bool primary;
  public override string TransformLabel(string label)=>label+(partner==null?"（未绑定）":primary?"（甲戒）":"（乙戒）");
  public override string CompInspectStringExtra()=>partner==null?"没有配对的另一枚戒指。":primary?"甲戒：佩戴者受到的伤害一半转给乙戒的佩戴者。":"乙戒：替甲戒的佩戴者分担一半伤害。";
  public override void PostExposeData(){base.PostExposeData();Scribe_References.Look(ref partner,"lpPartner");Scribe_Values.Look(ref primary,"lpPrimary");}
 }
 public static class WardingBond {
  public static void Bind(Thing a,Thing b){var ca=a.TryGetComp<CompWardingBond>();var cb=b.TryGetComp<CompWardingBond>();ca.partner=b;ca.primary=true;cb.partner=a;cb.primary=false;}
  public static Pawn PartnerWearer(Pawn p){
   var list=p?.apparel?.WornApparel;if(list==null)return null;
   foreach(var a in list){var c=a.GetComp<CompWardingBond>();if(c==null || !c.primary || !(c.partner is Apparel other))continue;var q=other.Wearer;if(q!=null && q!=p && !q.Dead && q.Spawned && q.Map==p.Map)return q;}
   return null;
  }
 }

 // ---------- 魂石 ----------
 public class CompProperties_SpiritStone : CompProperties {public CompProperties_SpiritStone(){compClass=typeof(CompSpiritStone);}}
 public class CompSpiritStone : ThingComp {
  public Pawn soul;
  public override string TransformLabel(string label)=>soul==null?label:label+"（"+soul.LabelShort+"的灵魂）";
  public override string CompInspectStringExtra()=>soul==null?"空的魂石：佩戴者死亡时会收下他的灵魂。":"里面封着"+soul.LabelShort+"的灵魂。可以在死灵仪式台献给命匣，或者对"+soul.LabelShort+"的尸体使用复生卷轴（不论死了多久）。";
  public override void PostExposeData(){base.PostExposeData();Scribe_References.Look(ref soul,"lpSoul",true);}
 }
 public static class SpiritStones {
  public static Pawn Soul(Thing t)=>t?.TryGetComp<CompSpiritStone>()?.soul;
  public static IEnumerable<Thing> All(){
   var def=Items.Thing("LP_SpiritStone");if(def==null || Find.Maps==null)yield break;
   foreach(var map in Find.Maps){
    foreach(var t in map.listerThings.ThingsOfDef(def))yield return t;
    foreach(var c in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>())if(c.InnerPawn?.apparel!=null)foreach(var a in c.InnerPawn.apparel.WornApparel)if(a.def==def)yield return a;
    foreach(var p in map.mapPawns.AllPawnsSpawned){if(p.apparel!=null)foreach(var a in p.apparel.WornApparel)if(a.def==def)yield return a;if(p.inventory!=null)foreach(var t in p.inventory.innerContainer)if(t.def==def)yield return t;}
   }
  }
  public static Thing StoneOf(Pawn soul){if(soul==null)return null;var worn=soul.apparel?.WornApparel.FirstOrDefault(a=>Soul(a)==soul);if(worn!=null)return worn;return All().FirstOrDefault(t=>Soul(t)==soul);}
  public static bool Holds(Pawn soul)=>StoneOf(soul)!=null;
  public static void Capture(Pawn dead){var stone=dead?.apparel?.WornApparel.FirstOrDefault(a=>a.def.defName=="LP_SpiritStone" && Soul(a)==null);if(stone==null)return;stone.GetComp<CompSpiritStone>().soul=dead;if(dead.Faction==Faction.OfPlayer)Messages.Message(dead.LabelShort+"的灵魂被吸进了魂石。",dead.Corpse!=null?new LookTargets(dead.Corpse):LookTargets.Invalid,MessageTypeDefOf.NeutralEvent,false);}
  public static void Consume(Pawn soul){var stone=StoneOf(soul);if(stone==null)return;if(stone is Apparel a && a.Wearer!=null){CursedItems.releasing=true;try{a.Wearer.apparel.Remove(a);}finally{CursedItems.releasing=false;}}if(!stone.Destroyed)stone.Destroy();}
 }

 // ---------- 不可移动之杖 ----------
 public class CompProperties_ImmovableRod : CompProperties {public bool active;public CompProperties_ImmovableRod(){compClass=typeof(CompImmovableRod);}}
 public class CompImmovableRod : ThingComp {
  public CompProperties_ImmovableRod Props=>(CompProperties_ImmovableRod)props;
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   foreach(var g in base.CompGetGizmosExtra())yield return g;
   if(!parent.Spawned)yield break;
   if(Props.active)yield return new Command_Action{defaultLabel="收回",defaultDesc="再按一次按钮，杖子变回能搬走的物品。",icon=parent.def.uiIcon,action=()=>ImmovableRod.Release(parent)};
   else{var cmd=new Command_Action{defaultLabel="按下",defaultDesc="按下按钮：杖子定在原地，变成一格不可摧毁、不可通行的障碍。",icon=parent.def.uiIcon,action=()=>ImmovableRod.Press(parent)};var why=ImmovableRod.CannotPress(parent);if(why!=null)cmd.Disable(why);yield return cmd;}
  }
 }
 public static class ImmovableRod {
  public static string CannotPress(Thing rod){if(rod?.Map==null)return "要放在地上。";var c=rod.Position;if(c.GetFirstPawn(rod.Map)!=null)return "格子里有人。";if(c.GetEdifice(rod.Map)!=null)return "格子里已经有建筑。";return null;}
  public static Thing Press(Thing rod){
   if(CannotPress(rod)!=null)return null;var map=rod.Map;var c=rod.Position;
   rod.Destroy();var b=GenSpawn.Spawn(ThingMaker.MakeThing(Items.Thing("LP_ImmovableRodActive")),c,map);
   FleckMaker.ThrowDustPuff(c.ToVector3Shifted(),map,1f);return b;
  }
  public static Thing Release(Thing active){
   if(active?.Map==null)return null;var map=active.Map;var c=active.Position;
   active.Destroy();var rod=ThingMaker.MakeThing(Items.Thing("LP_ImmovableRod"));GenPlace.TryPlaceThing(rod,c,map,ThingPlaceMode.Near);return rod;
  }
 }

 // ---------- 无尽水瓶 ----------
 public class CompProperties_Decanter : CompProperties {public CompProperties_Decanter(){compClass=typeof(CompDecanter);}}
 public class CompDecanter : ThingComp {
  public int lastSpray=-9999999;
  public bool Ready=>Find.TickManager.TicksGame-lastSpray>=GenDate.TicksPerDay;
  public int Spray(){
   if(!parent.Spawned || !Ready)return -1;lastSpray=Find.TickManager.TicksGame;var map=parent.Map;int n=0;
   foreach(var c in GenRadial.RadialCellsAround(parent.Position,Decanters.Radius,true)){
    if(!c.InBounds(map))continue;
    foreach(var t in c.GetThingList(map).ToList()){
     if(t is Fire){t.Destroy();n++;}
     else if(t.HasAttachment(ThingDefOf.Fire)){t.GetAttachment(ThingDefOf.Fire)?.Destroy();n++;}
    }
    if(Rand.Chance(.4f))FleckMaker.ThrowDustPuff(c.ToVector3Shifted(),map,.6f);
   }
   return n;
  }
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   foreach(var g in base.CompGetGizmosExtra())yield return g;
   var cmd=new Command_Action{defaultLabel="喷射",defaultDesc="浇灭3格内的所有火（包括身上着火的人）。每天一次。",icon=parent.def.uiIcon,action=()=>{int n=Spray();Messages.Message("无尽水瓶喷出一大股水，浇灭了"+Math.Max(0,n)+"处火。",parent,MessageTypeDefOf.NeutralEvent,false);}};
   if(!Ready)cmd.Disable("今天已经喷过了。");
   yield return cmd;
  }
  public override string CompInspectStringExtra()=>"周围3格植物生长+20%。"+(Ready?"可以喷射。":"喷射冷却中。");
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref lastSpray,"lpLastSpray",-9999999);}
 }

 // ---------- 水晶球 ----------
 public class CompProperties_CrystalBall : CompProperties {public CompProperties_CrystalBall(){compClass=typeof(CompCrystalBall);}}
 public class CompCrystalBall : ThingComp {
  public int lastGaze=-9999999;
  public bool Ready=>Find.TickManager.TicksGame-lastGaze>=GenDate.TicksPerDay;
  public static IEnumerable<Settlement> Targets(Map map)=>Find.WorldObjects.Settlements.Where(s=>s.Faction!=null && !s.Faction.IsPlayer).OrderBy(s=>map==null?0f:Find.WorldGrid.ApproxDistanceInTiles(map.Tile,s.Tile)).Take(12);
  public static string Report(Settlement s){
   var f=s.Faction;var kinds=f.def.pawnGroupMakers?.SelectMany(g=>g.options??new List<PawnGenOption>()).Select(o=>o.kind).Where(k=>k!=null).Distinct().Take(5).Select(k=>k.label).ToList()??new List<string>();
   var races=f.def.pawnGroupMakers?.SelectMany(g=>g.options??new List<PawnGenOption>()).Select(o=>o.kind?.race?.label).Where(r=>r!=null).Distinct().ToList()??new List<string>();
   float pts=StorytellerUtility.DefaultThreatPointsNow(Find.World)*(f.def.techLevel>=TechLevel.Industrial?1.2f:1f);
   return s.LabelCap+"\n派系："+f.Name+"\n守军种族："+(races.Count==0?"看不清":string.Join("、",races))+"\n常见兵种："+(kinds.Count==0?"看不清":string.Join("、",kinds))+"\n预估守军威胁点数：约"+Mathf.RoundToInt(pts);
  }
  public string Gaze(Settlement s){if(s==null || !Ready)return null;lastGaze=Find.TickManager.TicksGame;var text=Report(s);Find.LetterStack.ReceiveLetter("水晶球：窥视"+s.LabelCap,"雾气在水晶球里散开，映出远方的据点：\n\n"+text,LetterDefOf.NeutralEvent,new LookTargets(s));return text;}
  public override IEnumerable<Gizmo> CompGetGizmosExtra(){
   foreach(var g in base.CompGetGizmosExtra())yield return g;
   var cmd=new Command_Action{defaultLabel="窥视",defaultDesc="选一个世界地图上的据点，看看它的派系、守军种族和大致兵力。每天一次。",icon=parent.def.uiIcon,action=()=>{var opts=Targets(parent.Map).Select(s=>new FloatMenuOption(s.LabelCap+"（"+s.Faction.Name+"）",()=>Gaze(s))).ToList();if(opts.Count==0)opts.Add(new FloatMenuOption("附近没有据点",null));Find.WindowStack.Add(new FloatMenu(opts));}};
   if(!Ready)cmd.Disable("今天已经窥视过了。");
   yield return cmd;
  }
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref lastGaze,"lpLastGaze",-9999999);}
 }

 // ---------- 召唤物：时间到了就消散 ----------
 public class Hediff_Summoned : HediffWithComps {
  public override void PostRemoved(){
   base.PostRemoved();var p=pawn;if(p==null || p.Dead || p.Destroyed)return;
   if((this.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear??1)>0)return;
   Items.State?.Defer(()=>MagicEffects.Vanish(p));
  }
 }

 public static partial class MagicEffects {
  public const float SmokeRadius=3.5f,DispelGolemDamage=20f,LightningEmp=10f,LightningShock=12f;
  public const int SmokeTicks=7200,LightningCells=3;
  // 闪电标枪：命中格起沿飞行方向共3格，每格里的人受到 EMP 10 + 电击 12。
  public static List<IntVec3> LightningLine(IntVec3 cell,Vector3 origin){
   var dir=cell.ToVector3Shifted()-origin;dir.y=0;if(dir.sqrMagnitude<.01f)dir=Vector3.forward;dir.Normalize();
   var l=new List<IntVec3>();for(int i=0;i<LightningCells;i++){var c=(cell.ToVector3Shifted()+dir*i).ToIntVec3();if(!l.Contains(c))l.Add(c);}return l;
  }
  public static int Lightning(Map map,IntVec3 cell,Vector3 origin,Thing launcher){
   int n=0;var shock=DefDatabase<DamageDef>.GetNamedSilentFail("LP_Shock")??DamageDefOf.Burn;
   foreach(var c in LightningLine(cell,origin)){
    if(!c.InBounds(map))continue;FleckMaker.Static(c,map,FleckDefOf.ExplosionFlash,1.5f);
    foreach(var p in c.GetThingList(map).OfType<Pawn>().ToList()){if(p.Dead)continue;p.TakeDamage(new DamageInfo(DamageDefOf.EMP,LightningEmp,0f,-1f,launcher));if(!p.Dead)p.TakeDamage(new DamageInfo(shock,LightningShock,.2f,-1f,launcher));n++;}
   }
   return n;
  }
  public static readonly string[] TrickAnimals={"Rat","Squirrel","Hare","Snowhare","Cat","YorkshireTerrier","Chicken","Duck","Raccoon","Fox_Fennec","Tortoise","Monkey","Guineapig","Hen"};
  public static void Vanish(Pawn p){
   if(p==null || p.Destroyed)return;var map=p.MapHeld;var pos=p.PositionHeld;
   p.equipment?.DestroyAllEquipment();p.apparel?.DestroyAll();
   if(p.Spawned)FleckMaker.ThrowDustPuffThick(p.DrawPos,map,1.5f,new Color(.85f,.9f,1f,.6f));
   p.Destroy(DestroyMode.Vanish);
  }
  public static bool ApplyAbility(string effect,Pawn caster,LocalTargetInfo target,float radius){
   var map=caster.Map;
   switch(effect){
    case "flametoggle":{var c=caster.equipment.Primary.GetComp<CompMagicWeapon>();c.active=!c.active;Items.Mote(caster,c.active?"火焰舌燃起":"火焰舌熄灭",new Color(1f,.6f,.3f));return true;}
    case "defend":{var h=caster.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_DefenderStance"));if(h!=null)caster.health.RemoveHediff(h);else caster.health.AddHediff(Items.Hediff("LP_DefenderStance"));return true;}
    case "readmind":MindFlayerPowers.ReadMind(caster,target.Pawn);return true;
    case "storedspell":{var ring=SpellStoring.Ring(caster);if(ring?.stored==null)return false;var s=ring.stored;ring.Store(null,null);bool ok=ItemScrolls.Effect(s,caster,target);Items.Mote(caster,"释放储存的法术",new Color(.6f,.8f,1f));return ok;}
    case "soothe":target.Pawn.MentalState?.RecoverFromState();Items.Mote(target.Pawn,"平静下来",new Color(.7f,1f,.7f));return true;
    case "meteor":Meteor(map,target.Cell,caster,radius>0?radius:2f);return true;
    case "fireball":return ThrowBead(caster,target.Cell,radius>0?radius:2.5f);
    case "prayheal":{var injury=target.Pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>!i.IsPermanent()).OrderByDescending(i=>i.Severity).FirstOrDefault();if(injury==null)return false;injury.Heal(injury.Severity);Items.Mote(target.Pawn,"伤口愈合",new Color(1f,.95f,.7f));return true;}
    case "praybless":{int n=0;foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>!p.Dead && p.RaceProps.Humanlike && !p.HostileTo(caster) && p.Position.InHorDistOf(caster.Position,radius)).ToList()){Items.Give(p,"LP_MI_Blessed");n++;}FleckMaker.Static(caster.Position,map,FleckDefOf.PsycastAreaEffect,radius);return n>0;}
    case "astral":return AstralStep(caster,target.Cell,Rand.Value);
    case "horn":return Horn(caster,Rand.RangeInclusive(2,4)).Count>0;
   }
   return false;
  }
  public static void Meteor(Map map,IntVec3 cell,Pawn caster,float radius){
   FleckMaker.Static(cell,map,FleckDefOf.ExplosionFlash,6f);
   GenExplosion.DoExplosion(cell,map,radius,DamageDefOf.Bomb,caster,20);
   FireUtility.TryStartFireIn(cell,map,.6f,caster);
  }
  public static bool ThrowBead(Pawn caster,IntVec3 cell,float radius){
   var necklace=Items.Worn(caster,"LP_NecklaceFireballs");var c=necklace?.GetComp<CompMagicCharges>();if(c==null || !c.TryUse())return false;
   GenExplosion.DoExplosion(cell,caster.Map,radius,DamageDefOf.Flame,caster,15);
   if(c.charges<=0){caster.apparel.Remove(necklace);necklace.Destroy();Messages.Message(caster.LabelShort+"扔出了最后一颗火球珠，项链碎了。",caster,MessageTypeDefOf.NeutralEvent,false);}
   return true;
  }
  // 佩戴者着火：所有珠子一起炸开。
  public static bool BurstNecklace(Pawn p){
   var necklace=Items.Worn(p,"LP_NecklaceFireballs");var c=necklace?.GetComp<CompMagicCharges>();if(c==null || !p.Spawned)return false;
   int beads=Math.Max(1,c.Charges);var map=p.Map;var cell=p.Position;
   p.apparel.Remove(necklace);necklace.Destroy();
   GenExplosion.DoExplosion(cell,map,1.9f+.3f*beads,DamageDefOf.Flame,p,10+3*beads);
   Messages.Message(p.LabelShort+"身上着了火，火球项链的"+beads+"颗珠子一起炸开了！",new LookTargets(cell,map),MessageTypeDefOf.NegativeEvent,false);
   return true;
  }
  public static bool AstralStep(Pawn caster,IntVec3 cell,float roll){
   if(!Githyanki.Step(caster,cell,0f))return false;
   if(roll<MagicItems.AstralMishap && caster.Spawned && CellFinder.TryFindRandomEdgeCellWith(c=>c.Standable(caster.Map) && c.GetFirstPawn(caster.Map)==null,caster.Map,0f,out var edge)){
    caster.pather?.StopDead();caster.Position=edge;caster.Notify_Teleported(true,false);
    Messages.Message(caster.LabelShort+"被星界之风卷到了地图边缘。",caster,MessageTypeDefOf.NegativeEvent,false);
   }
   return true;
  }
  // 英灵号角：2～4名英灵战士（巫妖吹响时是骷髅战士），1小时后消散。
  public static List<Pawn> Horn(Pawn caster,int count){
   var list=new List<Pawn>();var map=caster?.Map;if(map==null)return list;bool lich=Items.IsLich(caster);
   for(int i=0;i<count;i++){
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(Necro.Def<PawnKindDef>("Skeleton"),caster.Faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:30,fixedChronologicalAge:30,forcedXenotype:XenotypeDefOf.Baseliner));
    p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();p.inventory?.innerContainer.ClearAndDestroyContents();
    foreach(var t in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(t);
    foreach(var n in lich?Hediff_Construct.AppearanceGenesFor("Skeleton"):new[]{"Skin_SheerWhite","Eyes_Red"}){var g=DefDatabase<GeneDef>.GetNamedSilentFail(n);if(g!=null && !p.genes.HasActiveGene(g))p.genes.AddGene(g,true);}
    if(lich){p.story.headType=HeadTypeDefOf.Skull;p.story.bodyType=BodyTypeDefOf.Thin;}
    p.Name=new NameSingle(lich?"骷髅战士":"英灵战士");
    foreach(var sk in p.skills.skills){sk.Level=sk.def==SkillDefOf.Melee?10:2;sk.passion=Passion.None;}
    var sword=DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_LongSword");if(sword!=null)p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(sword,ThingDefOf.Steel));
    p.health.AddHediff(Items.Hediff("LP_SpectralSummon"));
    GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(caster.Position,map,3),map);
    if(p.drafter!=null && p.Faction==Faction.OfPlayer)p.drafter.Drafted=true;
    FleckMaker.ThrowDustPuffThick(p.DrawPos,map,1.5f,new Color(.8f,.9f,1f,.6f));
    list.Add(p);
   }
   if(caster.Faction==Faction.OfPlayer)Messages.Message(caster.LabelShort+"吹响英灵号角，"+list.Count+"名"+(lich?"骷髅战士":"英灵战士")+"应声而来。",caster,MessageTypeDefOf.PositiveEvent,false);
   return list;
  }
  public static Pawn TrickAnimal(Pawn user,float roll){
   var kinds=TrickAnimals.Select(n=>DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(k=>k!=null).ToList();if(kinds.Count==0 || user?.MapHeld==null)return null;
   var kind=kinds[Mathf.Clamp((int)(roll*kinds.Count),0,kinds.Count-1)];
   var a=PawnGenerator.GeneratePawn(kind,Faction.OfPlayer);a.health.AddHediff(Items.Hediff("LP_TrickAnimal"));
   GenSpawn.Spawn(a,CellFinder.RandomClosewalkCellNear(user.PositionHeld,user.MapHeld,2),user.MapHeld);
   Messages.Message(user.LabelShort+"从捉弄袋里掏出了一只"+a.LabelShort+"。",a,MessageTypeDefOf.NeutralEvent,false);
   return a;
  }
  // 使用类物品。返回 true 表示用成功了（计冷却）。
  public static bool Use(string effect,Pawn user,Thing item){
   switch(effect){
    case "golemmanual":user.health.AddHediff(Items.Hediff("LP_GolemLore"));ItemScrolls.Consume(item);Messages.Message(user.LabelShort+"读完了魔像手册：死灵仪式台可以为他制作石魔像胚体了。",user,MessageTypeDefOf.PositiveEvent,false);return true;
    case "purityseal":{var h=user.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_PuritySeals"));if(h==null)user.health.AddHediff(Items.Hediff("LP_PuritySeals"));else h.Severity=Math.Min(3f,h.Severity+1f);ItemScrolls.Consume(item);return true;}
    case "wardingcase":{var map=user.MapHeld;var pos=user.PositionHeld;var a=ThingMaker.MakeThing(Items.Thing("LP_RingWardingBond"));var b=ThingMaker.MakeThing(Items.Thing("LP_RingWardingBond"));WardingBond.Bind(a,b);ItemScrolls.Consume(item);if(map!=null){GenPlace.TryPlaceThing(a,pos,map,ThingPlaceMode.Near);GenPlace.TryPlaceThing(b,pos,map,ThingPlaceMode.Near);}return true;}
    case "bagoftricks":return TrickAnimal(user,Rand.Value)!=null;
   }
   return false;
  }
  public static string StoredInvalid(string effect,Pawn user,LocalTargetInfo t){
   switch(effect){
    case "teleport":var home=ItemScrolls.HomeMap();return home==null?"没有主基地可以回去。":user.Map==home?"已经在主基地了。":null;
    case "revivify":return ItemScrolls.CannotRevivify(t.Thing as Corpse);
    case "fleshtostone":return ItemScrolls.CannotPetrify(user,t.Pawn);
    case "removecurse":return CursedItems.RemoveCurseTarget(t)==null?"目标身上没有被诅咒的物品。":null;
   }
   return !t.Cell.IsValid || !t.Cell.InBounds(user.Map)?"需要地图上的目标位置。":null;
  }
  // ---------- 每小时 ----------
  public static bool Regenerate(Pawn p,Apparel ring){
   if(Troll.Is(p))return false;
   var injury=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>!i.IsPermanent()).OrderByDescending(i=>i.Severity).FirstOrDefault();if(injury==null)return false;
   if(ring.GetComp<CompMagicCharges>()?.TryUse()!=true)return false;
   injury.Heal(injury.Severity);Items.Mote(p,"再生戒指：伤口愈合",new Color(.6f,1f,.6f));return true;
  }
  public static string Oddity(Pawn p,float roll){
   if(roll<.5f){p.stances?.stunner?.StunFor(180,null,false,false,false);Items.Mote(p,"万变印记：一阵眩晕",new Color(.5f,.7f,1f));return "stun";}
   var skills=p.skills?.skills.Where(s=>!s.TotallyDisabled).ToList();if(skills==null || skills.Count<2)return null;
   var a=skills.RandomElement();var b=skills.Where(s=>s!=a && s.passion!=a.passion).RandomElementWithFallback();if(b==null)return null;
   var tmp=a.passion;a.passion=b.passion;b.passion=tmp;
   if(p.Faction==Faction.OfPlayer)Messages.Message("万变印记作怪："+p.LabelShort+"对"+a.def.label+"和"+b.def.label+"的热情对调了。",p,MessageTypeDefOf.NeutralEvent,false);
   return "passion";
  }
  public static bool TrySendInquisitorSeal(MagicItemsState s){
   if(s==null || s.sealGiven)return false;var a=MagicItems.Alliance;if(a==null || a.defeated || a.HostileTo(Faction.OfPlayer) || a.PlayerGoodwill<75)return false;
   var map=Find.Maps.Where(m=>m.IsPlayerHome).OrderByDescending(m=>m.mapPawns.FreeColonistsSpawnedCount).FirstOrDefault();if(map==null)return false;
   s.sealGiven=true;var seal=ThingMaker.MakeThing(Items.Thing("LP_InquisitorSeal"));var cell=DropCellFinder.TradeDropSpot(map);
   DropPodUtility.DropThingsNear(cell,map,new List<Thing>{seal},110,false,false,true);
   Find.LetterStack.ReceiveLetter("竖琴手的礼物","费伦拓荒同盟的竖琴手送来一只小包裹，里面是一枚审判徽记和一张字条：\n\n“你们一直是同盟的朋友。戴上它，狂热分子就知道这里不归他们管。”",LetterDefOf.PositiveEvent,new LookTargets(cell,map),a);
   return true;
  }
  // ---------- 复苏核心 ----------
  public static bool RevivalCore(Pawn p,float roll){
   var corpse=p.Corpse;var core=p.apparel?.WornApparel.FirstOrDefault(a=>a.def.defName=="LP_RevivalCore");
   if(core!=null){CursedItems.releasing=true;try{p.apparel.Remove(core);}finally{CursedItems.releasing=false;}core.Destroy();}
   if(corpse==null || roll>=MagicItems.RevivalChance){if(corpse?.MapHeld!=null)Messages.Message(p.LabelShort+"身上的复苏核心闪了几下，碎了，没能把他唤回来。",corpse,MessageTypeDefOf.NeutralEvent,false);return false;}
   if(!ResurrectionUtility.TryResurrect(p,new ResurrectionParams{removeDiedThoughts=true}))return false;
   p.health.AddHediff(Items.Hediff("LP_RevivalWeakness"));
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"的复苏核心启动了：他从死亡里爬了回来，核心碎成了粉末。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  public static bool RevivalBlocked(Pawn p)=>Troll.Is(p) || Items.IsLich(p) || DeathHooks.Any(h=>h(p));
  public static readonly List<Func<Pawn,bool>> DeathHooks=new List<Func<Pawn,bool>>();
  // ---------- 敌人死亡掉落 ----------
  public static readonly Dictionary<string,(string def,float chance)[]> Drops=new Dictionary<string,(string,float)[]>{
   {"LP_FH_RedWizard",new[]{("LP_NecklaceFireballs",.08f),("LP_MarkBlood",.025f),("LP_MarkDecay",.025f),("LP_MarkChange",.025f),("LP_MarkRapture",.025f)}},
   {"LP_FH_GithMercenary",new[]{("LP_VorpalSword",.05f),("LP_AmuletPlanes",.06f)}},
   {"LP_FC_Paladin",new[]{("LP_HaloOfLight",.05f)}},
   {"LP_FH_OrcWarrior",new[]{("LP_WaaaghTotem",.1f)}},
   {"LP_EC_OrcSlave",new[]{("LP_WaaaghTotem",.1f)}}};
  public static List<string> RollDrops(string kind,Func<float> rand){var l=new List<string>();if(kind!=null && Drops.TryGetValue(kind,out var table))foreach(var (def,chance) in table)if(rand()<chance)l.Add(def);return l;}
  public static void OnDeath(Pawn p){
   SpiritStones.Capture(p);
   if(Items.Worn(p,"LP_RevivalCore")!=null && !RevivalBlocked(p))MagicItemsState.Get?.Schedule(p,Rand.Value);
   if(p.Faction==null || p.Faction.IsPlayer || p.Corpse?.MapHeld==null)return;
   foreach(var name in RollDrops(p.kindDef?.defName,()=>Rand.Value)){var def=Items.Thing(name);if(def==null)continue;var t=ThingMaker.MakeThing(def,def.MadeFromStuff?GenStuff.DefaultStuffFor(def):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityUtility.GenerateQualityReward(),ArtGenerationContext.Outsider);GenPlace.TryPlaceThing(t,p.Corpse.PositionHeld,p.Corpse.MapHeld,ThingPlaceMode.Near);}
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class MagicItemsOnDeath {static void Postfix(Pawn __instance){if(__instance!=null && __instance.Dead)try{MagicEffects.OnDeath(__instance);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 魔法物品死亡钩子出错："+e,9127451);}}}
}
