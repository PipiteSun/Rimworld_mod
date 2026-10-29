using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.55.0 宗主恩赐（规格第二节）：每个恩赐在 Defs/Pact_Patrons.xml 里写 workerClass + 参数（hediff / ability / kind / thing / count / days）。
 public abstract class PactBoonWorker {
  public LP_PactBoon boon;
  public virtual void OnSign(PlanarPact p){}
  public virtual void OnEnd(PlanarPact p){}
  public virtual void Tick(PlanarPact p,int now){}
 }
 // 签约人身上的状态（地狱之主的印记、冰狱奥术、女王之舌、霜影）。命匣抵押时数值 ×1.5（写在 severity 上）。
 public class PactBoon_Hediff : PactBoonWorker {
  public override void OnSign(PlanarPact p){Ensure(p);}
  public override void Tick(PlanarPact p,int now){Ensure(p);}
  void Ensure(PlanarPact p){var s=p.signer;if(s==null || s.Dead || boon.hediff==null)return;var h=s.health.hediffSet.GetFirstHediffOfDef(boon.hediff);if(h==null){h=HediffMaker.MakeHediff(boon.hediff,s);h.Severity=p.boonFactor;s.health.AddHediff(h);PactBoons.Dirty();}}
  public override void OnEnd(PlanarPact p){var s=p.signer;if(s?.health==null || boon.hediff==null)return;var h=s.health.hediffSet.GetFirstHediffOfDef(boon.hediff);if(h!=null)s.health.RemoveHediff(h);PactBoons.Dirty();}
 }
 // 签约人得到一个能力（召唤角魔 / 寒冰魔 / 红帽、夏日之触、冬之帷幕）。
 public class PactBoon_Ability : PactBoonWorker {
  public override void OnSign(PlanarPact p){var s=p.signer;if(s?.abilities!=null && boon.ability!=null && s.abilities.GetAbility(boon.ability)==null)s.abilities.GainAbility(boon.ability);}
  public override void OnEnd(PlanarPact p){var s=p.signer;if(s?.abilities!=null && boon.ability!=null && s.abilities.GetAbility(boon.ability)!=null)s.abilities.RemoveAbility(boon.ability);}
 }
 // 艾林魔：立即得到一名束缚者（九狱规则，不挣脱），终身跟随签约人；签约人死亡或契约结束 → 回九狱。
 public class PactBoon_Bound : PactBoonWorker {
  public override void OnSign(PlanarPact p){
   var s=p.signer;var map=s?.MapHeld;if(map==null || boon.kind==null)return;
   var e=AbyssRaces.Make(boon.kind.defName,Faction.OfPlayer,map.Tile,Rand.Chance(.5f)?Gender.Female:Gender.Male);if(e==null)return;
   GenSpawn.Spawn(e,CellFinder.RandomClosewalkCellNear(s.PositionHeld,map,3),map);
   AbyssBinding.Bind(e,s,100f);AbyssBinding.SetLevel(e,Mathf.RoundToInt(boon.count*(p.boonFactor>1f?1.1f:1f)),true);
   Items.Give(e,"LP_PactBound");p.bound=e;
   Find.LetterStack.ReceiveLetter("艾林魔："+e.LabelShort,"一名艾林魔从硫磺烟里走出来，在"+s.LabelShort+"面前单膝跪下："+e.gender.GetPronoun()+"是阿斯莫蒂尔斯借给签约人的复仇者，会终身跟随"+s.LabelShort+"。九狱的造物守契约：它不会挣脱束缚。",LetterDefOf.PositiveEvent,e);
  }
  public override void OnEnd(PlanarPact p){
   var e=p.bound;p.bound=null;if(e==null || e.Dead || e.Destroyed || !Outsiders.Bound(e))return;
   if(e.Spawned){Outsiders.DropBelongings(e,e.Position,e.Map);for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(e.DrawPos,e.Map,2f);e.DeSpawn();}
   if(Find.WorldPawns.Contains(e))Find.WorldPawns.RemovePawn(e);
   Messages.Message(e.LabelShort+"回九狱去了（契约结束）。",MessageTypeDefOf.NeutralEvent,false);
   e.Destroy();
  }
 }
 // 定期空投（铁城之金：每季 1500 银）：签约时先送一次。
 public class PactBoon_Drop : PactBoonWorker {
  public override void OnSign(PlanarPact p){Drop(p);p.nextBoonTick=Pacts.Now+Mathf.RoundToInt(boon.days*GenDate.TicksPerDay);}
  public override void Tick(PlanarPact p,int now){if(p.nextBoonTick>=0 && now>=p.nextBoonTick){p.nextBoonTick=now+Mathf.RoundToInt(boon.days*GenDate.TicksPerDay);Drop(p);}}
  void Drop(PlanarPact p){
   var map=Pacts.HomeOf(p);if(map==null || boon.thing==null)return;int n=Mathf.RoundToInt(boon.count*p.boonFactor);var list=new List<Thing>();
   while(n>0){var t=ThingMaker.MakeThing(boon.thing);t.stackCount=Mathf.Min(n,boon.thing.stackLimit);n-=t.stackCount;list.Add(t);}
   DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map),map,list,110,false,false,true,false,true,null);
   Messages.Message(p.patron.label+"送来了"+boon.label+"："+list.Sum(t=>t.stackCount)+" "+boon.thing.label+"。",new TargetInfo(DropCellFinder.TradeDropSpot(map),map),MessageTypeDefOf.PositiveEvent,false);
  }
 }
 // 禁书：每 60 天送来 1 本恶魔学典籍 + 1 张随机耐瑟瑞尔秘术残卷。
 public class PactBoon_Books : PactBoonWorker {
  public override void OnSign(PlanarPact p){Drop(p);p.nextBoonTick=Pacts.Now+Mathf.RoundToInt(boon.days*GenDate.TicksPerDay);}
  public override void Tick(PlanarPact p,int now){if(p.nextBoonTick>=0 && now>=p.nextBoonTick){p.nextBoonTick=now+Mathf.RoundToInt(boon.days*GenDate.TicksPerDay);Drop(p);}}
  public static List<Thing> Make(){
   var list=new List<Thing>();if(AbyssItems.Demonomicon!=null)list.Add(ThingMaker.MakeThing(AbyssItems.Demonomicon));
   try{var s=Netheril.MakeArcane(Netheril.Schools.RandomElement());if(s!=null)list.Add(s);}catch(Exception e){Log.Warning("[DND边缘大陆] 禁书：残卷生成失败 "+e.Message);}
   return list;
  }
  void Drop(PlanarPact p){var map=Pacts.HomeOf(p);if(map==null)return;var list=Make();DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map),map,list,110,false,false,true,false,true,null);Messages.Message(p.patron.label+"送来了禁书："+string.Join("、",list.Select(t=>t.LabelNoCount))+"。",list.FirstOrDefault(),MessageTypeDefOf.PositiveEvent,false);}
 }
 // 铁城工匠：签约人活着、在殖民地地图上时，全殖民地（这张地图的殖民者）建造速度 +25%。
 public class PactBoon_ColonyHediff : PactBoonWorker {
  public override void OnSign(PlanarPact p){Tick(p,Pacts.Now);}
  public override void Tick(PlanarPact p,int now){
   var s=p.signer;var map=s?.Spawned==true?s.Map:null;bool on=map!=null && map.IsPlayerHome && !s.Dead;
   foreach(var m in Find.Maps)foreach(var c in m.mapPawns.FreeColonistsSpawned)Races124.Ensure(c,boon.hediff,on && m==map);
  }
  public override void OnEnd(PlanarPact p){foreach(var m in Find.Maps)foreach(var c in m.mapPawns.AllPawns)if(Items.Has(c,boon.hediff))c.health.RemoveHediff(c.health.hediffSet.GetFirstHediffOfDef(boon.hediff));}
 }
 // 地狱铸炉：立即空投一座未安装的地狱铸炉（只能这样得到）。
 public class PactBoon_Forge : PactBoonWorker {
  public override void OnSign(PlanarPact p){
   var map=Pacts.HomeOf(p);var def=boon.thing??Pacts.ForgeDef;if(map==null || def==null)return;
   var b=ThingMaker.MakeThing(def,def.MadeFromStuff?GenStuff.DefaultStuffFor(def):null);var m=MinifyUtility.MakeMinified(b);
   DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map),map,new List<Thing>{m},110,false,false,true,false,true,null);
   Find.LetterStack.ReceiveLetter("地狱铸炉",p.patron.label+"空投了一座未安装的地狱铸炉：烧灵魂币（1 枚 = 1 天），能做符文铁砧的所有配方，速度 ×1.5。",LetterDefOf.PositiveEvent,new TargetInfo(DropCellFinder.TradeDropSpot(map),map));
  }
 }
 // 看穿条款：殖民地所有其他契约的隐藏条款自动揭开。
 public class PactBoon_SeeClauses : PactBoonWorker {
  public override void OnSign(PlanarPact p){Tick(p,Pacts.Now);}
  public override void Tick(PlanarPact p,int now){var c=Pacts.Comp;if(c==null)return;foreach(var o in c.Active)if(o!=p)foreach(var cl in o.clauses)if(cl.hidden)cl.revealed=true;}
 }
 // 解开契约：立刻解除殖民地里另一份契约（多份时选），之后本契约代价周期 ×0.75。
 public class PactBoon_Unbind : PactBoonWorker {
  public override void OnSign(PlanarPact p){
   var c=Pacts.Comp;var others=c?.Active.Where(x=>x!=p).ToList();
   if(others==null || others.Count==0){p.periodFactor*=.75f;Messages.Message("殖民地里没有别的契约可以解开：格拉西娅只把代价周期放宽了（×0.75）。",p.signer,MessageTypeDefOf.NeutralEvent,false);return;}
   if(others.Count==1){Pacts.Unbind(others[0],"格拉西娅的恩赐“解开契约”");p.periodFactor*=.75f;return;}
   PactLetters.Send("unbind",p,p.signer,null,"解开哪一份契约？","格拉西娅轻轻一笑：殖民地里的哪一份契约，她替你撕掉？");
  }
 }
 // 夏日恩泽：有这份契约的殖民地地图作物生长 +30%，种植区作物不因低温枯萎（补丁第一行看缓存的地图集合）。
 public class PactBoon_Summer : PactBoonWorker {
  public override void OnSign(PlanarPact p){PactBoons.Dirty();}
  public override void OnEnd(PlanarPact p){PactBoons.Dirty();}
 }
 // 夏宫来客：立即得到 1 名萨特或 1 名林仙（选择信件）。
 public class PactBoon_Guest : PactBoonWorker {
  public override void OnSign(PlanarPact p){PactLetters.Send("guest",p,p.signer,null,"夏宫来客","提泰妮娅说要送一位客人来殖民地常住。来的是谁？\n\n· 萨特：羊角毛腿、爱热闹的乐师，可遗传的凡人。\n· 林仙：和一棵树绑在一起的妖精，种植和驯兽极好，离树太远会憔悴，树死了自己也活不长。");}
 }

 public static class PactBoons {
  public static readonly HashSet<Map> summerMaps=new HashSet<Map>();
  public static int iceArcana,infernalMarks;
  public static void Dirty()=>RefreshMaps(Pacts.Comp);
  public static void RefreshMaps(GameComponent_Pacts c){
   summerMaps.Clear();iceArcana=0;infernalMarks=0;if(c==null)return;
   foreach(var p in c.Active){
    if(p.boonKey=="SummerGrace" && p.signer?.MapHeld!=null)summerMaps.Add(p.signer.MapHeld);
    if(p.boonKey=="IceArcana")iceArcana++;
    if(p.boonKey=="InfernalMark")infernalMarks++;
   }
  }
  public static BodyPartRecord Eye(Pawn p)=>p?.health?.hediffSet?.GetNotMissingParts().FirstOrDefault(x=>x.def==BodyPartDefOf.Eye);
  public static Pawn SummerGuest(PlanarPact p,string kind){
   var map=Pacts.HomeOf(p);if(map==null)return null;
   var g=AbyssRaces.Make(kind,Faction.OfPlayer,map.Tile,kind=="LP_FY_Dryad" && Rand.Chance(.9f)?Gender.Female:(Gender?)null);if(g==null)return null;
   if(!CellFinder.TryFindRandomEdgeCellWith(x=>x.Standable(map) && map.reachability.CanReachColony(x),map,CellFinder.EdgeRoadChance_Neutral,out var cell))cell=CellFinder.RandomClosewalkCellNear(map.Center,map,8);
   GenSpawn.Spawn(g,cell,map);g.workSettings?.EnableAndInitialize();
   if(PactRaces.IsDryad(g)){AbyssBinding.Bind(g,p.signer,100f);PactRaces.BindNearestTree(g);}
   Find.LetterStack.ReceiveLetter("夏宫来客："+g.LabelShort,"提泰妮娅的客人"+g.LabelShort+"（"+g.genes?.Xenotype?.label+"）到了，作为殖民者加入。",LetterDefOf.PositiveEvent,g);
   return g;
  }
  // 治好一名殖民者最严重的一处伤（不含缺失部位）。
  public static bool HealWorst(Pawn t){
   var inj=t?.health?.hediffSet?.hediffs.OfType<Hediff_Injury>().Where(h=>!(h is Hediff_MissingPart)).OrderByDescending(h=>h.Severity).FirstOrDefault();
   if(inj==null){var other=t?.health?.hediffSet?.hediffs.FirstOrDefault(h=>h.def==Items.Hediff("LP_InfernalWound"));if(other==null)return false;t.health.RemoveHediff(other);return true;}
   t.health.RemoveHediff(inj);if(t.Spawned)FleckMaker.ThrowMetaIcon(t.Position,t.Map,FleckDefOf.HealingCross);return true;
  }
 }

 // ---------- 恩赐状态 ----------
 // 数值按 severity 缩放（命匣抵押 1.5）：偏移 ×s，倍率 1+(v-1)×s。
 public class Hediff_PactBoon : HediffWithComps {
  HediffStage stage;float stageFor=-1f;
  public override HediffStage CurStage{get{
   var b=def.stages?.FirstOrDefault();if(b==null)return null;float f=Mathf.Max(1f,Severity);if(stage!=null && Mathf.Approximately(stageFor,f))return stage;stageFor=f;
   stage=new HediffStage{label=b.label,painFactor=b.painFactor,capMods=b.capMods};
   if(b.statOffsets!=null)stage.statOffsets=b.statOffsets.Select(m=>new StatModifier{stat=m.stat,value=m.value*f}).ToList();
   if(b.statFactors!=null)stage.statFactors=b.statFactors.Select(m=>new StatModifier{stat=m.stat,value=1f+(m.value-1f)*f}).ToList();
   return stage;
  }}
 }
 // 地狱之主的印记：每 300 刻，5 格内敌对人形得到“恐惧”（精神崩溃阈值 +15%，10 秒）。用地图的敌对目标缓存，不扫全部 pawn。
 public class Hediff_InfernalMark : Hediff_PactBoon {
  int acc;
  public const float Radius=5f;public const int Interval=300,DreadTicks=600;
  public override void TickInterval(int delta){base.TickInterval(delta);if((acc+=delta)<Interval)return;acc=0;if(pawn.Spawned && !pawn.Dead && !pawn.Downed)Pulse(pawn);}
  public static int Pulse(Pawn p){
   int n=0;var set=p.Map.attackTargetsCache.TargetsHostileToFaction(p.Faction);
   foreach(var t in set){if(!(t.Thing is Pawn e) || e.Dead || !e.RaceProps.Humanlike || !e.Position.InHorDistOf(p.Position,Radius))continue;if(Legendary.FearImmune(e))continue;Items.Give(e,"LP_InfernalDread",DreadTicks);n++;}
   return n;
  }
 }
 // 霜影：签约人静止 3 秒后隐身（复用巴路古拉的“融入阴影”）。
 public class Hediff_FrostShadow : Hediff_PactBoon {
  int ticks,still;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((ticks+=delta)<60)return;int d=ticks;ticks=0;if(!pawn.Spawned)return;
   int now=Find.TickManager.TicksGame;bool calm=!pawn.Downed && !pawn.pather.Moving && now-pawn.LastAttackTargetTick>AbyssRaces.StillTicks && now-pawn.mindState.lastHarmTick>AbyssRaces.StillTicks;
   still=calm?still+d:0;bool want=still>=AbyssRaces.StillTicks;bool has=Items.Has(pawn,"LP_BarlguraHidden");
   if(want && !has)Items.Give(pawn,"LP_BarlguraHidden");else if(!want && has)Items.Remove(pawn,"LP_BarlguraHidden");
  }
  public override void PostRemoved(){base.PostRemoved();Items.Remove(pawn,"LP_BarlguraHidden");}
 }

 // ---------- 恩赐能力（召唤、夏日之触、冬之帷幕） ----------
 public class CompProperties_PactAbility : CompProperties_AbilityEffect {
  public string effect;public PawnKindDef kind;public int count=1;public float days,radius,range;public int dc,ticks;
  public CompProperties_PactAbility(){compClass=typeof(CompAbilityEffect_Pact);}
 }
 public class CompAbilityEffect_Pact : CompAbilityEffect {
  public CompProperties_PactAbility P=>(CompProperties_PactAbility)props;
  Pawn Caster=>parent.pawn;
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   if(!base.Valid(target,throwMessages))return false;var why=PactRaces.Invalid(this,target);
   if(why!=null && throwMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);
   return why==null;
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   base.Apply(target,dest);
   PactRaces.ApplyAbility(this,target);
   // 债务 1：恩赐给的能力冷却 ×2（原版在效果之后才开始冷却，所以下一刻再改）
   var pact=Pacts.Of(Caster);if(pact!=null && pact.debt>=1 && parent.def.cooldownTicksRange.max>0){var ab=parent;int cd=parent.def.cooldownTicksRange.max*2;Fiends.Defer(()=>ab.StartCooldown(cd));}
  }
  public override bool AICanTargetNow(LocalTargetInfo target)=>PactRaces.AICan(this,target);
 }

 // ---------- Harmony：夏日恩泽 ----------
 [HarmonyPatch(typeof(Plant),nameof(Plant.GrowthRate),MethodType.Getter)]
 public static class SummerGrowth {
  static void Postfix(Plant __instance,ref float __result){if(PactBoons.summerMaps.Count==0 || __result<=0f)return;var m=__instance.MapHeld;if(m!=null && __instance.IsCrop && PactBoons.summerMaps.Contains(m))__result*=1.3f;}
 }
 [HarmonyPatch(typeof(Plant),nameof(Plant.MakeLeafless))]
 public static class SummerNoFrost {
  static bool Prefix(Plant __instance,Plant.LeaflessCause __0){
   if(PactBoons.summerMaps.Count==0 || __0!=Plant.LeaflessCause.Cold)return true;
   var m=__instance.Map;if(m==null || !PactBoons.summerMaps.Contains(m))return true;
   return !(m.zoneManager.ZoneAt(__instance.Position) is Zone_Growing);
  }
 }
}
