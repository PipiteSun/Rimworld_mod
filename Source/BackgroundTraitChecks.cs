using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.32.0 背景特性（组名 Traits：Test.ps1 -Only Traits），14 条检查。
 // 其他检查组的测试小人不应随机带上背景（贵族不搬运、士兵改命中、局外人改移速会让老检查不稳定）：自测一开始把背景出现率压成 0，并清掉地图上现有小人的背景；本组检查时临时按默认设置恢复。
 public partial class LichChecks {
  static string bgSaveId;static int bgSaveRoofed;
  static void SuppressBackgroundsForTests(){
   BackgroundTraits.TestSuppressed=true;BackgroundTraits.ApplySettings();
   // 1.46.0：DND 背景故事同理，只在 Backstories 组里打开。
   Backstories.TestSuppressed=true;
   if(reload)return;
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawns.ToList())if(p.story?.traits!=null)foreach(var t in p.story.traits.allTraits.Where(t=>BackgroundTraits.IsBackground(t.def)).ToList())p.story.traits.RemoveTrait(t);
  }
  List<Pawn> BgBatch(int n){
   var list=new List<Pawn>();
   for(int i=0;i<n;i++)list.Add(PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false)));
   return list;
  }
  static void BgDiscard(IEnumerable<Pawn> pawns){foreach(var p in pawns.ToList()){if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);if(!p.Destroyed)p.Destroy();}}
  static float BgRate(List<Pawn> pawns)=>pawns.Count==0?0f:(float)pawns.Count(p=>BackgroundTraits.Count(p)>0)/pawns.Count;
  // 找一个空地格子：先在 center 周围 [min,max] 范围内找；作为落脚点（min 为 0）找不到时把范围放宽到 45 格。
  IntVec3 BgCell(IntVec3 center,float min,float max,Func<IntVec3,bool> extra=null){
   center=center.ClampInsideMap(map);
   Func<IntVec3,bool> ok=c=>c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map)==null && c.GetEdifice(map)==null && c.DistanceTo(center)>=min && (extra==null || extra(c));
   foreach(var c in GenRadial.RadialCellsAround(center,max,true))if(ok(c))return c;
   if(min<=0)foreach(var c in GenRadial.RadialCellsAround(center,Math.Max(max,45f),true))if(ok(c))return c;
   throw new Exception("no free cell near "+center+" ("+min+"-"+max+")");
  }
  Pawn BgPerson(){var p=Person();foreach(var t in p.story.traits.allTraits.Where(t=>BackgroundTraits.IsBackground(t.def)).ToList())p.story.traits.RemoveTrait(t);return p;}
  Pawn BgSpawn(IntVec3 c){var p=BgPerson();GenSpawn.Spawn(p,c,map);return p;}
  static void BgGive(Pawn p,TraitDef d){foreach(var t in p.story.traits.allTraits.Where(t=>BackgroundTraits.IsBackground(t.def)).ToList())p.story.traits.RemoveTrait(t);p.story.traits.GainTrait(new Trait(d));}

  void CheckTraits(){
   var s=LichMod.Settings;bool oldOn=s.backgroundTraits;int oldPct=s.backgroundTraitPercent;
   var made=new List<Thing>();var roofs=new List<IntVec3>();var batches=new List<Pawn>();
   try{
    BackgroundTraits.TestSuppressed=false;s.backgroundTraits=true;s.backgroundTraitPercent=100;BackgroundTraits.ApplySettings();BackgroundTraits.ForgetCaches();
    // 1 加载与权重
    var all=BackgroundTraits.All.ToList();
    Check(all.Count==8 && all.All(d=>Near(BackgroundTraits.Weight(d),.3f) && d.exclusionTags.Contains(BackgroundTraits.Tag) && d.degreeDatas.Count==1 && !d.ConfigErrors().Any()),"eight background traits load with commonality 0.3 and the LP_Background exclusion tag ("+string.Join(",",all.Select(d=>d.defName+"="+BackgroundTraits.Weight(d)))+")");
    // 2、3 随机池
    var batchA=BgBatch(300);batches.AddRange(batchA);
    Check(batchA.All(p=>BackgroundTraits.Count(p)<=1),"300 random pawns: nobody has two backgrounds (max "+batchA.Max(p=>BackgroundTraits.Count(p))+")");
    float rateA=BgRate(batchA);
    Check(rateA>=.03f && rateA<=.25f,"300 random pawns: "+rateA.ToStringPercent()+" have a background (3%-25%)");
    // 4 开关与出现率
    s.backgroundTraits=false;BackgroundTraits.ApplySettings();
    bool zeroed=all.All(d=>BackgroundTraits.Weight(d)==0f);var batchB=BgBatch(100);batches.AddRange(batchB);int off=batchB.Count(p=>BackgroundTraits.Count(p)>0);
    s.backgroundTraits=true;s.backgroundTraitPercent=200;BackgroundTraits.ApplySettings();
    bool doubled=all.All(d=>Near(BackgroundTraits.Weight(d),.6f));var batchC=BgBatch(300);batches.AddRange(batchC);float rateC=BgRate(batchC);
    Check(zeroed && off==0 && doubled && rateC>rateA,"toggle off: weight 0 and 0 of 100 pawns get a background ("+off+"); rate 200%: weight 0.6 and share rises "+rateA.ToStringPercent()+" -> "+rateC.ToStringPercent());
    s.backgroundTraitPercent=100;BackgroundTraits.ApplySettings();
    // 9（第二部分）贵族与苦行
    bool asceticConflict=BackgroundTraits.Noble.ConflictsWith(TraitDefOf.Ascetic) && TraitDefOf.Ascetic.ConflictsWith(BackgroundTraits.Noble);
    bool noBoth=batches.All(p=>!(BackgroundTraits.Has(p,BackgroundTraits.Noble) && p.story.traits.HasTrait(TraitDefOf.Ascetic)));
    BgDiscard(batches);batches.Clear();
    // 5 死灵造物
    foreach(var d in all)BackgroundTraits.SetWeight(d,50f);
    var master=map.mapPawns.FreeColonistsSpawned.First();
    var skeleton=Constructs.Create("Skeleton",master,BgCell(altar.Position,4,15),map);made.Add(skeleton);
    bool cleanBorn=BackgroundTraits.Count(skeleton)==0;skeleton.story.traits.GainTrait(new Trait(BackgroundTraits.Soldier));
    Check(cleanBorn && BackgroundTraits.Count(skeleton)==0,"death constructs never carry a background (generated with weight 50, and GainTrait is refused)");
    Necro.Registry.constructs.Remove(skeleton);
    BackgroundTraits.ApplySettings();
    // 6 侍僧
    var focusStat=DefDatabase<StatDef>.GetNamedSilentFail("MeditationFocusGain");
    if(focusStat!=null){var a=BgPerson();float f0=a.GetStatValue(focusStat);BgGive(a,BackgroundTraits.Acolyte);float f1=a.GetStatValue(focusStat);BgDiscard(new[]{a});Check(f0>0 && Near(f1/f0,1.2f),"acolyte meditation psyfocus gain x1.2 ("+f0+" -> "+f1+")");}
    // 7 罪犯越狱
    var cellBuilt=DragonHoard.BuildHoard(map,altar.Position+IntVec3.West*14,null,out var cellRect);made.AddRange(cellBuilt);
    var prisonBed=cellBuilt.OfType<Building_Bed>().First();prisonBed.ForPrisoners=true;prisonBed.GetRoom()?.Notify_RoomShapeChanged();
    var prisoner=BgPerson();var enemy=Find.FactionManager.AllFactions.First(f=>!f.IsPlayer && !f.Hidden && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer));
    prisoner.SetFaction(enemy);GenSpawn.Spawn(prisoner,cellRect.Cells.First(c=>c.Standable(map) && c.GetFirstPawn(map)==null && c.GetEdifice(map)==null),map);made.Add(prisoner);prisoner.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);
    bool colonyBefore=BackgroundTraits.ColonyHasCriminal(map);
    float mtb0=PrisonBreakUtility.InitiatePrisonBreakMtbDays(prisoner,null,true);BgGive(prisoner,BackgroundTraits.Criminal);float mtb1=PrisonBreakUtility.InitiatePrisonBreakMtbDays(prisoner,null,true);
    var warden=map.mapPawns.FreeColonistsSpawned.First(p=>p.story?.traits!=null && BackgroundTraits.Count(p)==0);BgGive(warden,BackgroundTraits.Criminal);
    float mtb2=PrisonBreakUtility.InitiatePrisonBreakMtbDays(prisoner,null,true);warden.story.traits.RemoveTrait(warden.story.traits.GetTrait(BackgroundTraits.Criminal));
    Check(mtb0>0 && Near(mtb1/mtb0,.5f) && Near(mtb2,colonyBefore?mtb1:mtb1/.75f),"criminal prisoner breaks out twice as often (MTB "+mtb0.ToString("0.##")+" -> "+mtb1.ToString("0.##")+" days); a criminal colonist makes it x0.75 ("+mtb2.ToString("0.##")+"); prisoner="+prisoner.IsPrisoner+" cell="+prisoner.GetRoom()?.IsPrisonCell+" cells="+prisoner.GetRoom()?.CellCount+" bed="+prisonBed.ForPrisoners+" awake="+RestUtility.Awake(prisoner)+" downed="+prisoner.Downed);
    // 8、14 民间英雄
    var heroCell=BgCell(altar.Position+IntVec3.East*16,0,8);var hero=BgSpawn(heroCell);made.Add(hero);BgGive(hero,BackgroundTraits.FolkHero);
    var near=BgSpawn(BgCell(heroCell,2,4));var far=BgSpawn(BgCell(heroCell,9,14));made.Add(near);made.Add(far);
    var present=DefDatabase<ThoughtDef>.GetNamed("LP_BG_HeroPresent");
    hero.drafter.Drafted=true;BackgroundTraits.ForgetCaches();
    bool draftedNear=present.Worker.CurrentState(near).Active,draftedFar=present.Worker.CurrentState(far).Active,self=present.Worker.CurrentState(hero).Active;
    hero.drafter.Drafted=false;BackgroundTraits.ForgetCaches();bool undrafted=present.Worker.CurrentState(near).Active;
    Check(draftedNear && !draftedFar && !self && !undrafted && present.stages[0].baseMoodEffect==3f,"folk hero drafted: colonist within 6 cells gets +3 'hero is here', one 9+ cells away and the hero do not; gone when undrafted");
    var fallen=DefDatabase<ThoughtDef>.GetNamed("LP_BG_HeroFallen");
    // 直接走原版（已打补丁的）MakeDowned / MakeUndowned，不靠伤害，避免随机死亡。
    HarmonyLib.AccessTools.Method(typeof(Pawn_HealthTracker),"MakeDowned").Invoke(hero.health,new object[]{null,null});bool downed=hero.Downed;
    bool mourned=near.needs.mood.thoughts.memories.GetFirstMemoryOfDef(fallen)!=null && far.needs.mood.thoughts.memories.GetFirstMemoryOfDef(fallen)!=null && hero.needs.mood.thoughts.memories.GetFirstMemoryOfDef(fallen)==null;
    HarmonyLib.AccessTools.Method(typeof(Pawn_HealthTracker),"MakeUndowned").Invoke(hero.health,new object[]{null});
    foreach(var p in map.mapPawns.FreeColonistsSpawned)p.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(fallen);
    Check(downed && mourned && Near(fallen.durationDays,1f) && fallen.stages[0].baseMoodEffect==-3f,"folk hero downed: the whole colony gets -3 'hero has fallen' for 1 day (the hero does not); downed="+downed+" mourned="+mourned);
    // 9 贵族
    var noble=BgPerson();BgGive(noble,BackgroundTraits.Noble);
    bool noWork=noble.WorkTagIsDisabled(WorkTags.Cleaning) && noble.WorkTagIsDisabled(WorkTags.Hauling) && noble.WorkTypeIsDisabled(WorkTypeDefOf.Hauling) && noble.WorkTypeIsDisabled(DefDatabase<WorkTypeDef>.GetNamed("Cleaning"));
    BgDiscard(new[]{noble});
    Check(noWork && asceticConflict && noBoth,"noble cannot be assigned cleaning or hauling; noble and ascetic conflict both ways and never appear together in 700 random pawns");
    // 10 士兵
    var soldierCell=BgCell(altar.Position+IntVec3.North*14,0,8);var soldier=BgSpawn(soldierCell);made.Add(soldier);BgGive(soldier,BackgroundTraits.Soldier);BackgroundTraits.ForgetCaches();
    float aim0=soldier.GetStatValue(StatDefOf.AimingDelayFactor);float acc0=soldier.GetStatValue(StatDefOf.ShootingAccuracyPawn),hit0=soldier.GetStatValue(StatDefOf.MeleeHitChance);
    soldier.drafter.Drafted=true;float aim1=soldier.GetStatValue(StatDefOf.AimingDelayFactor);soldier.drafter.Drafted=false;
    var mate=BgSpawn(BgCell(soldierCell,1,2.9f));made.Add(mate);BgGive(mate,BackgroundTraits.Soldier);BackgroundTraits.ForgetCaches();
    float acc1=soldier.GetStatValue(StatDefOf.ShootingAccuracyPawn),hit1=soldier.GetStatValue(StatDefOf.MeleeHitChance);
    Check(Near(aim1/aim0,.9f) && Near(acc1,Mathf.Min(acc0*1.05f,StatDefOf.ShootingAccuracyPawn.maxValue)) && Near(hit1,Mathf.Min(hit0*1.05f,StatDefOf.MeleeHitChance.maxValue)),"soldier drafted aiming time x0.9 ("+aim0.ToString("0.###")+" -> "+aim1.ToString("0.###")+"); another soldier within 3 cells: shooting "+acc0.ToString("0.###")+" -> "+acc1.ToString("0.###")+", melee hit "+hit0.ToString("0.###")+" -> "+hit1.ToString("0.###")+" (x1.05 after the skill curve)");
    // 11 隐士
    var hermit=BgSpawn(BgCell(altar.Position+IntVec3.South*16,0,8));made.Add(hermit);BgGive(hermit,BackgroundTraits.Hermit);
    var roomBuilt=DragonHoard.BuildHoard(map,hermit.Position,hermit,out var roomRect);made.AddRange(roomBuilt);
    var hermitThought=DefDatabase<ThoughtDef>.GetNamed("LP_BG_HermitRoom");var alone=hermitThought.Worker.CurrentState(hermit);
    var lodger=BgPerson();var lodgerBed=(Building_Bed)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Bed"),ThingDefOf.WoodLog);lodgerBed.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(lodgerBed,roomRect.Cells.First(c=>c.x==roomRect.maxX && c.z==roomRect.minZ),map,Rot4.North,WipeMode.Vanish);made.Add(lodgerBed);
    GenSpawn.Spawn(lodger,roomRect.Cells.First(c=>c.Standable(map) && c.GetFirstPawn(map)==null && c.GetEdifice(map)==null),map);made.Add(lodger);lodger.ownership.ClaimBedIfNonMedical(lodgerBed);
    var shared=hermitThought.Worker.CurrentState(hermit);
    Check(roomBuilt.Count>0 && alone.Active && alone.StageIndex==0 && shared.Active && shared.StageIndex==1 && hermitThought.stages[1].baseMoodEffect==-4f && hermitThought.stages[0].baseMoodEffect==3f && hermit.story.traits.IsThoughtDisallowed(ThoughtDef.Named("SleptInBarracks")),"hermit: own bedroom +3, sharing the bedroom -4, vanilla barracks mood blocked");
    lodger.ownership.UnclaimBed();hermit.ownership.UnclaimBed();
    // 12 局外人
    var outCell=BgCell(altar.Position+IntVec3.East*20,0,25,c=>!c.Roofed(map) && !map.areaManager.Home[c]);
    var outlander=BgSpawn(outCell);made.Add(outlander);BgGive(outlander,BackgroundTraits.Outlander);var comp=BackgroundTraits.Comp(outlander);
    comp.Refresh(60);bool wild=comp.outdoors;float speedOut=outlander.GetStatValue(StatDefOf.MoveSpeed);comp.outdoors=false;float speedBase=outlander.GetStatValue(StatDefOf.MoveSpeed);
    map.roofGrid.SetRoof(outCell,RoofDefOf.RoofConstructed);roofs.Add(outCell);comp.Refresh(60);int roofed=comp.roofedTicks;
    var cooped=DefDatabase<ThoughtDef>.GetNamed("LP_BG_OutlanderCooped");bool fresh=!cooped.Worker.CurrentState(outlander).Active;comp.roofedTicks=BackgroundTraits.OutlanderCoopedTicks;bool stale=cooped.Worker.CurrentState(outlander).Active;
    Check(comp!=null && wild && !comp.outdoors && Near(speedOut/speedBase,1.1f) && roofed==60 && fresh && stale && outlander.story.traits.IsThoughtDisallowed(ThoughtDef.Named("SoakingWet")),"outlander: unroofed outside the home area move speed x1.1 ("+speedBase.ToString("0.##")+" -> "+speedOut.ToString("0.##")+"), none under a roof; 3 days under roofs gives -3; rain mood blocked");
   }finally{
    BgDiscard(batches);
    foreach(var c in roofs)map.roofGrid.SetRoof(c,null);
    foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn p)Necro.Registry?.constructs.Remove(p);t.Destroy();}
    foreach(var c in roofs)map.roofGrid.SetRoof(c,null);
    s.backgroundTraits=oldOn;s.backgroundTraitPercent=oldPct;BackgroundTraits.TestSuppressed=true;BackgroundTraits.ApplySettings();BackgroundTraits.ForgetCaches();
   }
  }
  // 13 存读档：一名带局外人背景、屋顶下计时 12345 的殖民者（站在屋顶下，暂停时计时不会变）。
  void PrepareTraitsSave(){
   var c=BgCell(altar.Position+IntVec3.West*20,0,25,x=>!x.Roofed(map));map.roofGrid.SetRoof(c,RoofDefOf.RoofConstructed);
   var p=BgSpawn(c);BgGive(p,BackgroundTraits.Outlander);BackgroundTraits.Comp(p).roofedTicks=12345;bgSaveId=p.ThingID;bgSaveRoofed=12345;
  }
  void CheckTraitsAfterLoad(){
   map=Find.CurrentMap;var p=map.mapPawns.AllPawns.FirstOrDefault(x=>x.ThingID==bgSaveId);
   int ticks=BackgroundTraits.Comp(p)?.roofedTicks??-1;
   Check(p!=null && BackgroundTraits.Has(p,BackgroundTraits.Outlander) && BackgroundTraits.Count(p)==1 && ticks>=bgSaveRoofed && ticks<bgSaveRoofed+600,"background trait and outlander roof timer survive save/load ("+ticks+")");
   if(p!=null){var c=p.Position;if(!p.Destroyed)p.Destroy();map.roofGrid.SetRoof(c,null);}
  }
 }
}
