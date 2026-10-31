using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.28.0 传送门失控：地图上开一个 2×2 位面裂口，按来源位面每 8 小时放一波生物；打掉、驱散粉尘、巨灵命令都能关上，3 天不关就崩溃。
 // 位面数据写在 PortalPlaneDef 里（Defs/Events_Portal.xml），以后加位面只改 XML。
 public class PlanarSpawn {
  public PawnKindDef kind;
  public IntRange count=new IntRange(1,1);
  public int fromWave=1;
  public bool scale=true;   // 是否按波次放大数量
  public int maxTotal=-1;   // 每个裂口最多放几个（寒冰魔 1 个）
  public string side="wild"; // wild：无派系狂暴；rival：同样狂暴，但和 wild 一方互相厮杀（妖精荒野）；thay：塞伊远征军的袭击者；1.54.0 abyss：深渊魔群（LP_AbyssHorde）
  public string special;     // fireGenasi：火裔元素裔 + 常驻着火免疫；invisible：幽影隐身；1.54.0 night：只在夜里（白天这一行跳过）
  public int minPoints=-1;   // 1.54.0：事件生成时的袭击点数低于它就不放（深渊入侵的炎魔 3500；仪式四阶失败的裂口不受限）
 }
 public class PortalPlaneDef : Def {
  public Color color=new Color(.6f,.3f,.8f);
  public List<PlanarSpawn> spawns=new List<PlanarSpawn>();
  // 1.54.0：randomPool=false 的位面（深渊）不进传送门失控的随机池；holyClosable：3 格内落下 5 瓶圣水就关闭；关闭时额外掉落。
  public bool randomPool=true,holyClosable;
  public ThingDef bonusReward;public IntRange bonusCount=IntRange.One;
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(spawns.NullOrEmpty())yield return defName+": no spawns";foreach(var s in spawns)if(s.kind==null)yield return defName+": spawn without kind";}
 }

 public static class PortalEvent {
  public const int WaveTicks=GenDate.TicksPerHour*8,CollapseAfterWave=9,MinColonyDistance=12,ResidueMin=3,ResidueMax=5,TeleportMemoryDays=15,FeudInterval=120;
  public const int LastWaveCalmTicks=GenDate.TicksPerHour;
  public const float WaveStep=.25f,WaveCap=2.5f,WizardFactor=3f,GenieSkillFactor=.06f,GenieCap=.8f,DispelRadius=3f,FeudRadius=12f,HolyRadius=3f;
  public const int HolyToClose=5;
  // 测试用：设了就代替随机数（巨灵命令的检定）。
  public static float? TestRoll;
  public static float Roll=>TestRoll??Rand.Value;
  // VPE 桥接登记：法师路径的判断（主程序集不引用 VPE）。
  public static Func<Pawn,bool> WizardHook;
  public static bool Wizard(Pawn p){try{return p!=null && WizardHook!=null && WizardHook(p);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 法师路径判断出错："+e,74120533);return false;}}
  static ThingDef riftDef,residueDef;
  public static ThingDef RiftDef=>riftDef??(riftDef=DefDatabase<ThingDef>.GetNamedSilentFail("LP_PlanarRift"));
  public static ThingDef ResidueDef=>residueDef??(residueDef=DefDatabase<ThingDef>.GetNamedSilentFail("LP_PlanarResidue"));
  public static EventState State=>GameComp<EventState>.Get();
  public static IEnumerable<Building_PlanarRift> Rifts(Map map)=>map==null || RiftDef==null?Enumerable.Empty<Building_PlanarRift>():map.listerThings.ThingsOfDef(RiftDef).OfType<Building_PlanarRift>();

  // 第 n 波的数量倍率：1 + 0.25×(n-1)，最多 ×2.5。
  public static float WaveFactor(int wave)=>Mathf.Min(WaveCap,1f+WaveStep*(Math.Max(1,wave)-1));
  public static int Count(PlanarSpawn s,int wave,float extra=1f){if(wave<s.fromWave)return 0;int n=s.count.RandomInRange;return Mathf.Max(0,Mathf.RoundToInt(n*(s.scale?WaveFactor(wave):1f)*extra));}

  // 殖民地里有法师路径、元素裔，或 15 天内用过传送卷轴时 ×2。
  public static bool Attracts(Map map){
   if(map==null)return false;
   foreach(var p in map.mapPawns.FreeColonistsSpawned)if(Wizard(p) || Genasi.IsGenasi(p))return true;
   var s=State;return s!=null && s.lastTeleportTick>=0 && Find.TickManager.TicksGame-s.lastTeleportTick<=TeleportMemoryDays*GenDate.TicksPerDay;
  }
  public static void NoteTeleport(){var s=State;if(s!=null)s.lastTeleportTick=Find.TickManager.TicksGame;}
  // 1.30.2：崩溃最后一波到点平静。
  public static int TickCalms(EventState s){
   int now=Find.TickManager.TicksGame,n=0;
   for(int i=s.pendingCalms.Count-1;i>=0;i--){var c=s.pendingCalms[i];if(now<c.tick)continue;n+=CompPlanarRift.CalmBeasts(c.pawns);s.pendingCalms.RemoveAt(i);}
   return n;
  }

  // 2×2 全可站立、无屋顶、没有建筑和物品，离殖民地建筑至少 12 格，离地图边至少 8 格。
  public static bool SpotOk(IntVec3 c,Map map,List<IntVec3> colony){
   var rect=new CellRect(c.x,c.z,2,2);
   if(c.x<8 || c.z<8 || c.x>map.Size.x-10 || c.z>map.Size.z-10)return false;
   foreach(var x in rect){if(!x.InBounds(map) || !x.Standable(map) || x.Roofed(map) || x.Fogged(map) || x.GetEdifice(map)!=null || x.GetFirstItem(map)!=null || x.GetFirstPawn(map)!=null || x.GetTerrain(map).IsWater)return false;}
   foreach(var b in colony)if(b.DistanceToSquared(c)<MinColonyDistance*MinColonyDistance)return false;
   var reg=MapComponent_Outsiders.Of(map);if(reg!=null && reg.glyphs.Count>0)foreach(var x in rect)if(reg.Warded(x))return false; // 1.54.0 驱魔结界范围里不开裂口
   return true;
  }
  // 1.54.0：夜里（21 点～5 点）。
  public static bool Night(Map map){int h=GenLocalDate.HourOfDay(map);return h>=21 || h<5;}
  // 1.54.0 投掷圣水落在可被圣水关闭的裂口 3 格内：计数，5 瓶关闭。
  public static int HolyNear(Map map,IntVec3 center){
   int n=0;
   foreach(var r in Rifts(map).ToList()){var c=r.Comp;if(c?.plane==null || !c.plane.holyClosable || c.closed)continue;if(r.OccupiedRect().ClosestCellTo(center).DistanceTo(center)>HolyRadius+.01f)continue;n++;
    c.holyHits++;if(c.holyHits>=HolyToClose)c.Close("五瓶圣水浇灭了裂口边缘的深渊之火",true);else Messages.Message("圣水泼在深渊裂口上，裂口剧烈收缩（"+c.holyHits+"/"+HolyToClose+"）。",new TargetInfo(r.Position,map),MessageTypeDefOf.PositiveEvent,false);}
   return n;
  }
  public static bool CanCommand(Pawn p)=>Races124.IsGenie(p) || Marilith.Is(p);
  public static bool TryFindSpot(Map map,out IntVec3 cell){
   var colony=map.listerBuildings.allBuildingsColonist.Select(b=>b.Position).ToList();
   return CellFinderLoose.TryGetRandomCellWith(c=>SpotOk(c,map,colony),map,2000,out cell);
  }
  public static Building_PlanarRift Open(Map map,IntVec3 cell,PortalPlaneDef plane){
   var rift=(Building_PlanarRift)ThingMaker.MakeThing(RiftDef);
   GenSpawn.Spawn(rift,cell,map);
   rift.Comp.plane=plane;rift.Comp.SpawnWave();
   if(plane.holyClosable)Thinness.Activate(map); // 1.54.0 开过深渊裂口的地图开始计算位面薄弱度
   return rift;
  }
  // 1.54.0：带点数、强制炎魔的版本（深渊入侵 / 仪式四阶失败）。
  public static Building_PlanarRift Open(Map map,IntVec3 cell,PortalPlaneDef plane,float points,bool forceBalor){
   var rift=(Building_PlanarRift)ThingMaker.MakeThing(RiftDef);
   GenSpawn.Spawn(rift,cell,map);
   rift.Comp.plane=plane;rift.Comp.points=points;rift.Comp.forceBig=forceBalor;rift.Comp.SpawnWave();
   if(plane.holyClosable)Thinness.Activate(map);
   return rift;
  }
  public static int DispelNear(Map map,IntVec3 center){
   int n=0;
   foreach(var r in Rifts(map).ToList())if(r.OccupiedRect().ClosestCellTo(center).DistanceTo(center)<=DispelRadius+.01f){r.Comp.Close("驱散粉尘冲散了裂口的边缘",true);n++;}
   return n;
  }
 }

 // ---------- 裂口建筑 ----------
 public class Building_PlanarRift : Building {
  public CompPlanarRift Comp=>GetComp<CompPlanarRift>();
  // 法师路径殖民者攻击裂口：伤害 ×3。
  public override void PreApplyDamage(ref DamageInfo dinfo,out bool absorbed){
   base.PreApplyDamage(ref dinfo,out absorbed);
   if(!absorbed && dinfo.Instigator is Pawn p && p.Faction==Faction.OfPlayer && PortalEvent.Wizard(p))dinfo.SetAmount(dinfo.Amount*PortalEvent.WizardFactor);
  }
  public override void Destroy(DestroyMode mode=DestroyMode.Vanish){
   var comp=Comp;var map=Map;
   if(mode==DestroyMode.KillFinalize && comp!=null && !comp.closed && map!=null)comp.Rewards(map,"裂口被打碎了",true);
   base.Destroy(mode);
  }
  public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn){
   foreach(var o in base.GetFloatMenuOptions(selPawn))yield return o;
   if(selPawn?.Faction!=Faction.OfPlayer || !PortalEvent.CanCommand(selPawn))yield break; // 1.54.0 六臂蛇人和巨灵一样能命令
   if(Comp.genieTried){yield return new FloatMenuOption("命令裂口关闭（已经试过了）",null);yield break;}
   if(!selPawn.CanReach(this,PathEndMode.Touch,Danger.Deadly)){yield return new FloatMenuOption("命令裂口关闭（走不到）",null);yield break;}
   yield return new FloatMenuOption("命令裂口关闭（成功率"+CompPlanarRift.GenieChance(selPawn).ToStringPercent()+"）",()=>selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("LP_CommandRift"),this),JobTag.Misc));
  }
  public override string GetInspectString(){var s=base.GetInspectString();var c=Comp;return c==null?s:(s.NullOrEmpty()?"":s+"\n")+c.Status();}
 }

 public class CompProperties_PlanarRift : CompProperties {public CompProperties_PlanarRift(){compClass=typeof(CompPlanarRift);}}
 public class CompPlanarRift : ThingComp {
  public PortalPlaneDef plane;
  public int wave,nextWaveTick=-1;
  public bool genieTried,closed;
  public float points=-1f;public bool forceBig;public int holyHits; // 1.54.0
  public List<Pawn> spawned=new List<Pawn>();
  public Dictionary<string,int> totals=new Dictionary<string,int>();
  int feudTick;
  static int Now=>Find.TickManager.TicksGame;
  public string Status(){
   if(plane==null)return null;
   string s="来源位面："+plane.label+"\n已涌出 "+wave+" 波";
   if(nextWaveTick>=0)s+="；"+(wave>=PortalEvent.CollapseAfterWave?"崩溃":"下一波")+"还有 "+((nextWaveTick-Now)/(float)GenDate.TicksPerHour).ToString("0.#")+" 小时";
   return s;
  }
  public static float GenieChance(Pawn p)=>Mathf.Min(PortalEvent.GenieCap,(p?.skills?.GetSkill(SkillDefOf.Social)?.Level??0)*PortalEvent.GenieSkillFactor);

  public override void CompTick(){
   base.CompTick();
   if(closed || plane==null || !parent.Spawned)return;
   int now=Now;
   if(nextWaveTick>=0 && now>=nextWaveTick){if(wave>=PortalEvent.CollapseAfterWave)Collapse();else SpawnWave();return;}
   if(now-feudTick>=PortalEvent.FeudInterval){feudTick=now;Feud();}
  }
  IntVec3 SpawnCell(){
   var map=parent.Map;
   var cells=GenAdj.CellsAdjacent8Way(parent).Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null).ToList();
   if(cells.Count>0)return cells.RandomElement();
   return CellFinder.RandomClosewalkCellNear(parent.Position,map,4);
  }
  // 生成一波：按位面表放生物。extra 用于崩溃时的 ×2。
  public List<Pawn> SpawnWave(float extra=1f,bool advance=true){
   if(advance)wave++;
   var made=new List<Pawn>();var thay=new List<Pawn>();var abyss=new List<Pawn>();var map=parent.Map;
   foreach(var s in plane.spawns){
    if(s.special=="night" && !PortalEvent.Night(map))continue; // 1.54.0 影魔只在夜里出来
    if(s.minPoints>=0 && !forceBig && points<s.minPoints)continue; // 1.54.0 炎魔的点数门槛
    int n=forceBig && s.minPoints>=0 && wave<s.fromWave?s.count.RandomInRange:PortalEvent.Count(s,wave,extra); // 仪式四阶失败：第一波就有炎魔
    string key=s.kind.defName;totals.TryGetValue(key,out int had);
    if(s.maxTotal>=0)n=Math.Min(n,s.maxTotal-had);
    for(int i=0;i<n;i++){var p=Make(s);if(p==null)continue;made.Add(p);if(s.side=="thay")thay.Add(p);if(s.side=="abyss")abyss.Add(p);totals[key]=++had;}
   }
   AdventurerEnemies.LevelLoose(thay); // 1.48.1 塞伊的人形敌人按各自战斗力定等级
   AdventurerEnemies.LevelLoose(abyss.Where(p=>!p.kindDef.defName.StartsWith("LP_AH_")).ToList()); // 1.54.0 魔群里的炎魔（深渊魔群兵种在生成时已按扩展定级）
   if(thay.Count>0){var f=thay[0].Faction;LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false,false,false),map,thay);}
   if(abyss.Count>0){var f=abyss[0].Faction;LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false,false,false),map,abyss);}
   nextWaveTick=Now+PortalEvent.WaveTicks;
   FleckMaker.Static(parent.TrueCenter().ToIntVec3(),map,FleckDefOf.PsycastAreaEffect,3f);
   return made;
  }
  Pawn Make(PlanarSpawn s){
   var map=parent.Map;Faction faction=null;
   if(s.side=="thay"){faction=Factions.Hostile;if(faction==null || !faction.HostileTo(Faction.OfPlayer))return null;}
   if(s.side=="abyss"){faction=AbyssRaces.Horde();if(faction==null)return null;}
   XenotypeDef xeno=s.special=="fireGenasi"?Races124.Xeno("LP_GenasiFire"):null;
   var req=new PawnGenerationRequest(s.kind,faction,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:s.kind.RaceProps.Humanlike,forcedXenotype:xeno);
   Pawn p;try{p=PawnGenerator.GeneratePawn(req);}catch(Exception e){Log.Warning("[DND边缘大陆] 裂口生成 "+s.kind.defName+" 失败："+e.Message);return null;}
   var cell=SpawnCell();GenSpawn.Spawn(p,cell,map);
   FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,1.2f);
   if(s.special=="fireGenasi")Items.Give(p,"LP_PlanarFlameBody");
   if(s.special=="invisible")Items.Give(p,"LP_ShadowVeil");
   if(s.side!="thay" && s.side!="abyss")p.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true,false,false,null,true);
   var mark=Items.Give(p,"LP_PlanarSpawn") as Hediff_PlanarSpawn;if(mark!=null){mark.rift=parent.ThingID;mark.side=s.side;}
   spawned.Add(p);
   return p;
  }
  // 妖精荒野：闪光犬和魔化兽互相厮杀——对手比最近的殖民者还近时，先去咬对手。
  public int Feud(){
   var map=parent.Map;if(map==null)return 0;int n=0;
   spawned.RemoveAll(p=>p==null || p.Destroyed);
   if(plane!=null && !plane.spawns.Any(s=>s.side=="rival"))return 0; // 1.58.6：只有妖精荒野有 rival 一方；其他位面原来要做完整个两两比对（每 120 刻）才在最后丢掉结果
   var live=spawned.Where(p=>p.Spawned && !p.Dead && !p.Downed && p.Map==map && p.InMentalState).ToList();
   foreach(var p in live){
    var side=Side(p);if(side!="wild" && side!="rival")continue;
    var rival=live.Where(o=>o!=p && (Side(o)=="wild" || Side(o)=="rival") && Side(o)!=side && o.Position.InHorDistOf(p.Position,PortalEvent.FeudRadius)).OrderBy(o=>o.Position.DistanceToSquared(p.Position)).FirstOrDefault();
    if(rival==null || !plane.spawns.Any(s=>s.side=="rival"))continue;
    var colonist=map.mapPawns.FreeColonistsSpawned.Where(c=>!c.Dead).OrderBy(c=>c.Position.DistanceToSquared(p.Position)).FirstOrDefault();
    if(colonist!=null && colonist.Position.DistanceToSquared(p.Position)<rival.Position.DistanceToSquared(p.Position))continue;
    if(p.CurJob?.def==JobDefOf.AttackMelee && p.CurJob.targetA.Thing==rival)continue;
    var job=JobMaker.MakeJob(JobDefOf.AttackMelee,rival);job.expiryInterval=PortalEvent.FeudInterval*2;job.killIncappedTarget=true;
    p.jobs.StartJob(job,JobCondition.InterruptForced);n++;
   }
   return n;
  }
  public static string Side(Pawn p)=>p?.health?.hediffSet?.hediffs.OfType<Hediff_PlanarSpawn>().FirstOrDefault()?.side;

  // 关闭：掉位面残渣，裂口里出来的野兽退出狂暴、变成普通野生动物。
  public void Close(string reason,bool rewards){
   if(closed)return;var map=parent.Map;
   if(rewards && map!=null)Rewards(map,reason,false);
   closed=true;
   if(!parent.Destroyed)parent.Destroy(DestroyMode.Vanish);
  }
  public void Rewards(Map map,string reason,bool killed){
   if(closed)return;closed=true;
   Adventurer.MilestoneMap(map,"portal",Adventurer.PortalXP); // 1.48.0 冒险者里程碑
   var cell=parent.Position;
   int n=Rand.RangeInclusive(PortalEvent.ResidueMin,PortalEvent.ResidueMax);
   if(PortalEvent.ResidueDef!=null){var t=ThingMaker.MakeThing(PortalEvent.ResidueDef);t.stackCount=n;GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);}
   if(plane?.bonusReward!=null){var b=ThingMaker.MakeThing(plane.bonusReward);b.stackCount=Math.Max(1,plane.bonusCount.RandomInRange);GenPlace.TryPlaceThing(b,cell,map,ThingPlaceMode.Near);} // 1.54.0 深渊裂口另掉恶魔血
   int calmed=Calm();
   for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(parent.TrueCenter()+new Vector3(Rand.Range(-1f,1f),0,Rand.Range(-1f,1f)),map,2f);
   Find.LetterStack.ReceiveLetter("裂口关闭了",reason+"：通往"+plane?.label+"的位面裂口合上了，原地留下了"+n+"份位面残渣。"+(calmed>0?"\n\n从裂口里出来的"+calmed+"只野兽失去了位面的驱使，安静下来，变成了普通的野生动物，可以驯服。":""),LetterDefOf.PositiveEvent,new LookTargets(cell,map));
  }
  public int Calm()=>CalmBeasts(spawned);
  // 平静：无派系的野兽退出狂暴、停下手上的事（有派系的寒冰魔、元素裔照常是袭击者）。
  public static int CalmBeasts(IEnumerable<Pawn> pawns){
   int n=0;
   foreach(var p in pawns.ToList()){
    if(p==null || p.Dead || p.Destroyed || p.Faction!=null || p.RaceProps.Humanlike)continue;
    if(p.InMentalState)p.MentalState.RecoverFromState();
    p.jobs?.StopAll();n++;
   }
   return n;
  }
  // 3 天不关：一次性放出当前波次 ×2 的生物，然后自己合上（不掉残渣）。
  // 1.30.2：之前各波仍存活的野兽立即平静；最后一波照常狂暴，裂口消失 1 小时后平静。
  public void Collapse(){
   var old=spawned.ToList();
   var made=SpawnWave(2f,false);closed=true;var map=parent.Map;var cell=parent.Position;
   int calmed=CalmBeasts(old);
   PortalEvent.State?.pendingCalms.Add(new PendingCalm{pawns=made.Where(p=>p.Faction==null && !p.RaceProps.Humanlike).ToList(),tick=Now+PortalEvent.LastWaveCalmTicks});
   Find.LetterStack.ReceiveLetter("裂口崩溃","通往"+plane.label+"的位面裂口开了三天，终于撑不住了：最后一次喷涌放出了"+made.Count+"个生物，然后自己塌缩消失。这次没有留下任何位面残渣。"+(calmed>0?"\n\n之前从裂口里出来的"+calmed+"只野兽失去了位面的驱使，安静下来，变成了普通的野生动物。":"")+"\n\n最后这一波野兽还会狂暴一阵，裂口消失 1 小时后才会安静下来。",LetterDefOf.ThreatBig,new LookTargets(made.Where(p=>p.Spawned).Cast<Thing>().DefaultIfEmpty(parent)));
   parent.Destroy(DestroyMode.Vanish);
  }
  // 巨灵：社交检定。成功关闭（照常掉残渣）；失败本波提前刷出。每个裂口只能试一次。
  public bool GenieCommand(Pawn genie){
   if(genieTried || closed)return false;genieTried=true;
   float chance=GenieChance(genie);
   if(PortalEvent.Roll<chance){Close(genie.LabelShort+"以巨灵的权柄命令裂口闭合",true);return true;}
   Messages.Message("裂口没有服从"+genie.LabelShort+"的命令，反而提前喷出了下一波生物！",new LookTargets(parent),MessageTypeDefOf.NegativeEvent,false);
   if(wave>=PortalEvent.CollapseAfterWave)Collapse();else SpawnWave();
   return false;
  }
  public override void PostExposeData(){
   base.PostExposeData();
   Scribe_Defs.Look(ref plane,"lpPlane");
   Scribe_Values.Look(ref wave,"lpWave");Scribe_Values.Look(ref nextWaveTick,"lpNextWave",-1);
   Scribe_Values.Look(ref genieTried,"lpGenieTried");Scribe_Values.Look(ref closed,"lpClosed");
   Scribe_Values.Look(ref points,"lpPoints",-1f);Scribe_Values.Look(ref forceBig,"lpForceBig");Scribe_Values.Look(ref holyHits,"lpHolyHits");
   if(Scribe.mode==LoadSaveMode.Saving)spawned.RemoveAll(p=>p==null || p.Destroyed || p.Discarded);
   Scribe_Collections.Look(ref spawned,"lpSpawned",LookMode.Reference);
   Scribe_Collections.Look(ref totals,"lpTotals",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){spawned=spawned?.Where(p=>p!=null).ToList()??new List<Pawn>();if(totals==null)totals=new Dictionary<string,int>();}
  }
 }
 // 裂口里出来的生物的标记：属于哪个裂口、哪一方。
 public class Hediff_PlanarSpawn : HediffWithComps {
  public string rift,side;
  public override bool Visible=>false;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref rift,"lpRift");Scribe_Values.Look(ref side,"lpSide");}
 }

 // 巨灵命令裂口：走到旁边，念 2 秒咒，然后检定。
 public class JobDriver_CommandRift : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
   yield return Toils_General.Wait(120).WithProgressBarToilDelay(TargetIndex.A);
   yield return Toils_General.Do(()=>(TargetA.Thing as Building_PlanarRift)?.Comp?.GenieCommand(pawn));
  }
 }

 // ---------- 事件 ----------
 public class IncidentWorker_PortalBreach : IncidentWorker {
  protected override bool CanFireNowSub(IncidentParms parms){
   var map=parms.target as Map;
   return LichMod.Settings.portalBreach && map!=null && map.IsPlayerHome && PortalEvent.RiftDef!=null && !PortalEvent.Rifts(map).Any() && DefDatabase<PortalPlaneDef>.AllDefsListForReading.Any(d=>d.randomPool) && PortalEvent.TryFindSpot(map,out _);
  }
  // 1.31.0：同样受“DND 事件频率”影响。
  public override float ChanceFactorNow(IIncidentTarget target)=>base.ChanceFactorNow(target)*(PortalEvent.Attracts(target as Map)?2f:1f)*Events2.Frequency;
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;if(map==null || !PortalEvent.TryFindSpot(map,out var cell))return false;
   var plane=DefDatabase<PortalPlaneDef>.AllDefsListForReading.Where(d=>d.randomPool).RandomElement(); // 1.54.0 深渊不进随机池
   var rift=PortalEvent.Open(map,cell,plane);
   SendStandardLetter("传送门失控","一道位面裂口在殖民地外撕开了！裂口另一边是"+plane.label+"，"+plane.description+"\n\n每过 8 小时就会有一波生物从裂口里涌出来，而且一波比一波多；开了 3 天还没关上的话，它会崩溃，一口气喷出双倍的生物。\n\n关上它：打掉裂口（600 生命），或者把驱散粉尘扔到它 3 格以内。法师路径的殖民者攻击裂口时伤害 ×3；巨灵可以试着直接命令它关闭（只能试一次）。关上后会留下位面残渣，野兽也会安静下来。",LetterDefOf.ThreatBig,parms,new LookTargets(rift));
   return true;
  }
 }
 // 驱散粉尘落在裂口 3 格内：立刻关闭。
 [HarmonyPatch(typeof(ItemThrowables),nameof(ItemThrowables.Dispel))]
 public static class DispelClosesRift {static void Postfix(Map map,IntVec3 center){if(map!=null)PortalEvent.DispelNear(map,center);}}
 // 传送卷轴：记下使用时间（15 天内传送门事件权重 ×2）。
 [HarmonyPatch(typeof(ItemScrolls),nameof(ItemScrolls.Teleport))]
 public static class TeleportAttractsRift {static void Postfix(bool __result){if(__result)PortalEvent.NoteTeleport();}}

 // 1.28.0 事件的存档状态（传送卷轴的使用时间等）。
 public partial class EventState : GameComponent {
  public int lastTeleportTick=-1;
  public List<PendingCalm> pendingCalms=new List<PendingCalm>();
  public EventState(Game game){}
  public override void ExposeData(){
   Scribe_Values.Look(ref lastTeleportTick,"lpLastTeleport",-1);
   Scribe_Collections.Look(ref pendingCalms,"lpPendingCalms",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit)pendingCalms=pendingCalms?.Where(c=>c!=null).ToList()??new List<PendingCalm>();
   ExposeMigration();
  }
 }
 // 1.30.2 崩溃的最后一波：到 tick 时平静。
 public class PendingCalm : IExposable {
  public List<Pawn> pawns=new List<Pawn>();public int tick;
  public void ExposeData(){
   if(Scribe.mode==LoadSaveMode.Saving)pawns.RemoveAll(p=>p==null || p.Destroyed || p.Discarded);
   Scribe_Collections.Look(ref pawns,"pawns",LookMode.Reference);Scribe_Values.Look(ref tick,"tick");
   if(Scribe.mode==LoadSaveMode.PostLoadInit)pawns=pawns?.Where(p=>p!=null).ToList()??new List<Pawn>();
  }
 }

 // 1.28.0 设置：事件。
 public partial class Settings {
  public bool portalBreach=true,giantMigration=true,twilightYear=true,twilightVanilla=false,infusionNormalZero=false;
  public int twilightStrength=100,twilightDays=45;
  void ExposeEvents(){
   Scribe_Values.Look(ref portalBreach,"portalBreach",true);Scribe_Values.Look(ref giantMigration,"giantMigration",true);Scribe_Values.Look(ref twilightYear,"twilightYear",true);
   Scribe_Values.Look(ref twilightVanilla,"twilightVanilla",false);Scribe_Values.Look(ref infusionNormalZero,"infusionNormalZero",false);
   Scribe_Values.Look(ref twilightStrength,"twilightStrength",100);Scribe_Values.Look(ref twilightDays,"twilightDays",45);
   twilightStrength=Math.Max(50,Math.Min(200,twilightStrength));twilightDays=Math.Max(15,Math.Min(120,twilightDays));
   ExposeEvents2(); // 1.31.0
   ExposeNetheril(); // 1.37.0
  }
 }
 public static class EventSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.GapLine();l.Label("事件");
   l.CheckboxLabeled("传送门失控",ref s.portalBreach,"殖民地外偶尔撕开一道位面裂口（九狱、元素火位面、妖精荒野、幽影界），每8小时涌出一波生物，3天不关会崩溃。打掉裂口、扔驱散粉尘或让巨灵命令都能关上。");
   l.CheckboxLabeled("巨人迁徙",ref s.giantMigration,"一队饿着的巨人（余烬氏族的火巨人或食人魔一家、费伦拓荒同盟的泰坦或霜巨人战帮）路过扎营，会找你讨吃的，有可能有人想留下来。");
   l.CheckboxLabeled("黄昏之年",ref s.twilightYear,"每年有25%概率出现“黄昏之兆”（第1年在随机一天，第2年起在每年第一天），15天后开始黄昏之年：神力灌注者和神明附体的敌人大幅增多。关闭后不再触发（已经开始的照常结束）；平时的神力灌注不受影响。");
   l.Label("黄昏之年持续："+s.twilightDays+"天");s.twilightDays=(int)l.Slider(s.twilightDays,15,120);
   l.Label("黄昏强度："+s.twilightStrength+"%（缩放灌注/附体概率和黄昏之年里的神力效果）");s.twilightStrength=(int)l.Slider(s.twilightStrength,50,200);
   l.CheckboxLabeled("原版派系也有神力（海盗→塔洛斯、部落→马拉尔）",ref s.twilightVanilla,"默认关闭。开启后原版海盗和野蛮部落的袭击者也会被神力灌注、附体。");
   l.CheckboxLabeled("平时没有神力灌注",ref s.infusionNormalZero,"开启后只有黄昏之年里才会出现神力灌注者（平时的3%改为0）。");
   Events2Settings.Draw(l);
   NetherilSettings.Draw(l); // 1.37.0
   EpicSettings.Draw(l); // 1.49.0 冒险者击杀里程碑、宿敌冠军
  }
 }
}
