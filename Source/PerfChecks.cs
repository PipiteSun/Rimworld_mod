using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
namespace LichPhylactery {
 // 1.58.6 性能组（Test.ps1 -Only Perf；VPE 桥另有 -VPE -Only Perf）：
 // ① 整图：100 个普通殖民者（关）与 100 人 DND 混合人口（开：冒险者带专长、DND 种族、深渊仆从、强力 / 背景特性、半数持枪）预热两轮后交替各跑 5 轮取中位，每轮 1000 刻，固定随机种子、关叙事者；
 // ② 热点微基准：同一批调用分别在普通小人和 DND 小人身上测每次调用的纳秒数和分配字节（属性先清缓存）。
 // 数字只写日志（[LICH-PERF] 行和存档目录里的 LichPerf.txt），不按阈值判失败：机器噪声约 ±20%。
 public static class LichPerf {
  public static Action<List<Pawn>> Decorate;          // VPE 桥：给开人口加职业路径
  public static Action<Pawn,Pawn,Pawn> ExtraBench;     // VPE 桥：额外微基准（普通、DND、敌人）
  static object sink;static string file;
  public static int Ticks=1000;
  static void Step(int n){for(int i=0;i<n;i++)Find.TickManager.DoSingleTick();}
  public static void Line(string s){Log.Message("[LICH-PERF] "+s);if(file!=null)File.AppendAllText(file,s+"\n");}
  // 每次调用的纳秒数（5 轮取中位）和分配字节（同一中位轮）。
  public static void Bench(string name,int count,Action a){
   try{
    for(int i=0;i<Math.Min(count,200);i++)a();
    var t=new double[5];var b=new long[5];
    for(int r=0;r<5;r++){long b0=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();for(int i=0;i<count;i++)a();sw.Stop();t[r]=sw.Elapsed.TotalMilliseconds*1e6/count;b[r]=GC.GetAllocatedBytesForCurrentThread()-b0;}
    Array.Sort(t);Array.Sort(b);
    Line(name+": "+t[2].ToString("0.0")+" ns/call, "+((double)b[2]/count).ToString("0.0")+" B/call");
   }catch(Exception e){Line(name+": ERROR "+e.GetType().Name+" "+e.Message);}
  }
  static IEnumerable<IntVec3> Cells(Map map,IntVec3 center,float r)=>GenRadial.RadialCellsAround(center,r,true).Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null && !c.Fogged(map));
  static Pawn Gen(Faction f,XenotypeDef x,int age=30){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,f,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:x??XenotypeDefOf.Baseliner,mustBeCapableOfViolence:true,fixedBiologicalAge:age,fixedChronologicalAge:age));
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_AddedPart || h.def.isBad).ToList())p.health.RemoveHediff(h);
   foreach(var missing in p.health.hediffSet.GetMissingPartsCommonAncestors().ToList())p.health.RestorePart(missing.Part);
   return p;
  }
  static void Arm(Pawn p,bool ranged){
   if(p.equipment==null)return;p.equipment.DestroyAllEquipment();
   var def=DefDatabase<ThingDef>.GetNamedSilentFail(ranged?"Gun_AssaultRifle":"MeleeWeapon_LongSword");if(def==null)return;
   var w=(ThingWithComps)ThingMaker.MakeThing(def,def.MadeFromStuff?ThingDefOf.Steel:null);p.equipment.AddEquipment(w);
  }
  static void Kill(IEnumerable<Pawn> list){foreach(var p in list.ToList()){if(p==null)continue;if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(p.Destroyed)continue;p.GetLord()?.RemovePawn(p);if(p.Spawned)p.DeSpawn();if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}}
  static void Park(List<Pawn> list){foreach(var p in list)if(p.Spawned){p.jobs?.StopAll();p.DeSpawn();}}
  static void Place(Map map,List<Pawn> list,List<IntVec3> cells){int i=0;foreach(var p in list){if(p.Destroyed || p.Dead)continue;if(!p.Spawned)GenSpawn.Spawn(p,cells[i++%cells.Count],map);}}

  // 开人口：40 冒险者（1～20 级、每人 2 个专长、一半持枪）、30 个 DND 种族（轮换 LP_ 异种型）、10 深渊仆从、20 强力 / 背景特性小人。
  static List<Pawn> MakeDnd(Map map,Faction f){
   var list=new List<Pawn>();
   var feats=Feats.All.ToList();
   for(int i=0;i<40;i++){
    var p=Gen(f,null);var h=Adventurer.TryAdd(p,1+i%20,true);
    if(h!=null && feats.Count>0){h.AddFeat(feats[(i*2)%feats.Count]);h.AddFeat(feats[(i*2+7)%feats.Count]);}
    Arm(p,i%2==0);list.Add(p);
   }
   var xenos=DefDatabase<XenotypeDef>.AllDefsListForReading.Where(x=>x.defName.StartsWith("LP_")).OrderBy(x=>x.defName).ToList();
   for(int i=0,made=0;made<30 && i<xenos.Count*2;i++){
    try{var p=Gen(f,xenos[i%xenos.Count]);Adventurer.TryAdd(p,1+i%10,true);Arm(p,i%2==1);list.Add(p);made++;}
    catch(Exception e){Line("skip xenotype "+xenos[i%xenos.Count].defName+": "+e.GetType().Name);}
   }
   string[] kinds={"LP_AH_Dretch","LP_AH_Hezrou","LP_AH_Barlgura","LP_AH_Succubus","LP_AH_Nabassu","LP_AH_Dretch","LP_AH_Hezrou","LP_AH_ShadowDemon","LP_AH_Dretch","LP_AH_Barlgura"};
   foreach(var k in kinds){try{var p=AbyssRaces.Make(k,f,map.Tile);if(p!=null)list.Add(p);}catch(Exception e){Line("skip outsider "+k+": "+e.GetType().Name);}}
   var traits=DefDatabase<TraitDef>.AllDefsListForReading.Where(d=>d.defName.StartsWith("LP_") && d.degreeDatas.Count>0).OrderBy(d=>d.defName).ToList();
   for(int i=0;i<20;i++){
    var p=Gen(f,null);Adventurer.TryAdd(p,5+i%10,true);
    if(traits.Count>0)for(int k=0;k<2;k++){var d=traits[(i*3+k*11)%traits.Count];if(!p.story.traits.HasTrait(d)){try{p.story.traits.GainTrait(new Trait(d,d.degreeDatas[0].degree));}catch{}}}
    Arm(p,i%2==0);list.Add(p);
   }
   return list;
  }

  public static string Run(Map map){
   file=Path.Combine(GenFilePaths.SaveDataFolderPath,"LichPerf.txt");File.WriteAllText(file,"LichPhylactery perf "+DateTime.Now+"\n");
   var storyteller=DebugSettings.enableStoryteller;DebugSettings.enableStoryteller=false;
   var plain=new List<Pawn>();var dnd=new List<Pawn>();var items=new List<Thing>();Pawn enemy=null;
   string summary="";
   try{
    var center=map.Center;var cells=Cells(map,center,16f).ToList();
    Rand.PushState(20261027);
    try{
     for(int i=0;i<100;i++){var p=Gen(Faction.OfPlayer,null);Arm(p,i%2==0);plain.Add(p);}
     dnd=MakeDnd(map,Faction.OfPlayer);
     Decorate?.Invoke(dnd);
    }finally{Rand.PopState();}
    foreach(var p in dnd)if(Outsiders.Is(p))AbyssBinding.MakeThrall(p,GenDate.TicksPerDay*3);
    // 搬运 / 合并 / 进食目标：几堆钢材和简单餐（堆叠、禁用、食物搜索钩子都会被走到）。
    foreach(var x in Cells(map,center+IntVec3.East*18,4f).Take(10)){var t=ThingMaker.MakeThing(ThingDefOf.Steel);t.stackCount=7;GenSpawn.Spawn(t,x,map);items.Add(t);}
    foreach(var x in Cells(map,center+IntVec3.West*18,4f).Take(6)){var t=ThingMaker.MakeThing(ThingDefOf.MealSimple);t.stackCount=3;GenSpawn.Spawn(t,x,map);items.Add(t);}
    Line("population: plain "+plain.Count+", dnd "+dnd.Count+" ("+dnd.Count(p=>Adventurer.Get(p)!=null)+" adventurers, "+dnd.Count(p=>p.genes?.Xenotype!=null && p.genes.Xenotype.defName.StartsWith("LP_"))+" LP xenotypes, "+dnd.Count(Outsiders.Is)+" outsiders); "+Ticks+" ticks per run");
    double Time(List<Pawn> pop,int seed){Park(plain);Park(dnd);Place(map,pop,cells);Rand.PushState(seed);try{Step(120);GC.Collect();var sw=Stopwatch.StartNew();Step(Ticks);sw.Stop();return sw.Elapsed.TotalMilliseconds/Ticks;}finally{Rand.PopState();}}
    Time(plain,1);Time(dnd,2);Time(plain,3);Time(dnd,4); // 两轮预热：JIT、小人落地找工作
    var offs=new List<double>();var ons=new List<double>();
    for(int k=0;k<5;k++){offs.Add(Time(plain,100+k));ons.Add(Time(dnd,200+k));}
    double Median(List<double> l){var s=l.OrderBy(x=>x).ToList();return s[s.Count/2];}
    double off=Median(offs),on=Median(ons);
    summary="off "+off.ToString("0.000")+" / on "+on.ToString("0.000")+" ms/tick";
    Line("whole map, 100 pawns: off (plain) "+string.Join(" / ",offs.Select(x=>x.ToString("0.000")))+"; on (DND mix) "+string.Join(" / ",ons.Select(x=>x.ToString("0.000")))+" ms/tick; median "+off.ToString("0.000")+" vs "+on.ToString("0.000")+" ("+((on-off)/Math.Max(.001,off)*100).ToString("+0.0;-0.0")+"%)");

    // ---------- 微基准：普通小人 a、DND 冒险者 b（持枪）、DND 种族 c，敌人 e ----------
    Park(plain);Park(dnd);Place(map,plain.Take(10).ToList(),cells);Place(map,dnd.Take(60).ToList(),cells.Skip(10).ToList());
    var a=plain.FirstOrDefault(p=>p.Spawned && p.equipment?.Primary!=null && p.equipment.Primary.def.IsRangedWeapon)??plain.First(p=>p.Spawned);
    var b=dnd.FirstOrDefault(p=>p.Spawned && Adventurer.Get(p)!=null && p.equipment?.Primary!=null && p.equipment.Primary.def.IsRangedWeapon && (p.genes?.Xenotype==null || p.genes.Xenotype==XenotypeDefOf.Baseliner))??dnd.First(p=>p.Spawned);
    var c=dnd.FirstOrDefault(p=>p.Spawned && p.genes?.Xenotype!=null && p.genes.Xenotype.defName.StartsWith("LP_"))??b;
    foreach(var p in plain.Concat(dnd))if(p.Spawned)p.jobs?.StopAll();
    var hostile=Find.FactionManager.RandomEnemyFaction(false,false,true,TechLevel.Industrial)??Find.FactionManager.RandomEnemyFaction();
    enemy=Gen(hostile,null);Arm(enemy,true);GenSpawn.Spawn(enemy,CellFinder.RandomClosewalkCellNear(center+IntVec3.North*12,map,6),map);
    var animal=map.mapPawns.AllPawnsSpawned.FirstOrDefault(p=>p.RaceProps.Animal && !p.Dead);
    var steel=items.FirstOrDefault(t=>t.def==ThingDefOf.Steel && t.Spawned);var steel2=items.LastOrDefault(t=>t.def==ThingDefOf.Steel && t.Spawned && t!=steel);var meal=items.FirstOrDefault(t=>t.def==ThingDefOf.MealSimple && t.Spawned);Line("bench subjects: steel "+(steel!=null)+", meal "+(meal!=null)+", a ranged "+(a.equipment?.Primary!=null)+", b ranged "+(b.equipment?.Primary!=null));
    var far=CellFinder.RandomClosewalkCellNear(center+IntVec3.South*20,map,8);
    Pawn[] who={a,b,c};string[] tag={"plain","adventurer","race:"+(c.genes?.Xenotype?.defName??"?")};
    for(int w=0;w<3;w++){
     var p=who[w];var n=tag[w];var verb=p.equipment?.PrimaryEq?.PrimaryVerb;
     Bench(n+" BodySize",20000,()=>sink=p.BodySize);
     Bench(n+" HealthScale",20000,()=>sink=p.HealthScale);
     Bench(n+" BiologicalTicksPerTick",20000,()=>sink=p.ageTracker.BiologicalTicksPerTick);
     var torso=p.RaceProps.body.corePart;Bench(n+" GetPartHealth(torso)",20000,()=>sink=p.health.hediffSet.GetPartHealth(torso));
     if(verb!=null){
      Bench(n+" Verb.EffectiveRange",20000,()=>sink=verb.EffectiveRange);
      Bench(n+" Verb.CanHitTargetFrom",5000,()=>sink=verb.CanHitTargetFrom(p.Position,enemy));
      Bench(n+" ShotReport.HitReportFor",5000,()=>sink=ShotReport.HitReportFor(p,verb,enemy));
     }
     Bench(n+" ThreatDisabled(enemy searcher)",20000,()=>sink=p.ThreatDisabled(enemy));
     if(steel!=null)Bench(n+" IsForbidden(steel)",20000,()=>sink=steel.IsForbidden(p));
     if(meal!=null)Bench(n+" WillEat(meal)",20000,()=>sink=FoodUtility.WillEat(p,meal));
     Bench(n+" Learn(Shooting)",20000,()=>p.skills.Learn(SkillDefOf.Shooting,.0001f));
     Bench(n+" Learn(Construction)",20000,()=>p.skills.Learn(SkillDefOf.Construction,.0001f));
     Bench(n+" CanReach(far,OnCell)",5000,()=>sink=p.CanReach(far,PathEndMode.OnCell,Danger.Deadly));
     foreach(var s in new[]{StatDefOf.MoveSpeed,StatDefOf.WorkSpeedGlobal,StatDefOf.GeneralLaborSpeed,StatDefOf.ShootingAccuracyPawn,StatDefOf.MeleeHitChance,StatDefOf.PsychicSensitivity,StatDefOf.CarryingCapacity,StatDefOf.MentalBreakThreshold}){
      var stat=s;Bench(n+" stat "+stat.defName+" (cache cleared)",3000,()=>{stat.Worker.ClearCacheForThing(p);sink=p.GetStatValue(stat);});
     }
     Bench(n+" ThingID",20000,()=>sink=p.ThingID);
    }
    if(animal!=null)Bench("animal BodySize",20000,()=>sink=animal.BodySize);
    if(animal!=null)Bench("animal BiologicalTicksPerTick",20000,()=>sink=animal.ageTracker.BiologicalTicksPerTick);
    if(steel!=null && steel2!=null)Bench("steel CanStackWith",20000,()=>sink=steel.CanStackWith(steel2));
    // 原版能力（皇权 DLC 的止痛灵能）：CanCast / GizmoDisabled 都会经过提尔审判之庭钩子。
    var ad=DefDatabase<AbilityDef>.GetNamedSilentFail("Painblock");
    if(ad!=null){b.abilities.GainAbility(ad);var ab=b.abilities.GetAbility(ad);if(ab!=null){Bench("ability CanCast",20000,()=>sink=ab.CanCast);Bench("ability GizmoDisabled",20000,()=>sink=ab.GizmoDisabled(out _));}b.abilities.RemoveAbility(ad);}
    ExtraBench?.Invoke(a,b,enemy);
   }
   finally{
    DebugSettings.enableStoryteller=storyteller;
    if(enemy!=null)Kill(new[]{enemy});Kill(plain);Kill(dnd);
    foreach(var t in items)if(!t.Destroyed)t.Destroy();
    foreach(var t in map.listerThings.AllThings.Where(t=>t is Corpse).ToList())t.Destroy();
    file=null;
   }
   return summary;
  }
 }
 public partial class LichChecks {
  void CheckPerf(){var s=LichPerf.Run(map);Check(s.Length>0,"performance pass recorded: "+s);}
 }
}
