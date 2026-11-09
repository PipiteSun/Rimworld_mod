using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.111.0 器械师子职业（子职业设计稿，器械师 3 / 7 / 11 / 15 级，Defs/Subclasses4.xml）：炼金师、装甲师、堡垒工程师。
 // 被动按能力位（Users），每 250 刻在 Tick250 更新；伤害挂钩（Subclass4Damage）第一步是空集合判断。
 public static class Subclass4 {
  public const string Alchemist="LP_Sub_Alchemist",Armorer="LP_Sub_Armorer",Fortress="LP_Sub_FortEngineer";
  public static readonly Color AlchemyColor=new Color(.95f,.6f,.25f),ArmorColor=new Color(.62f,.66f,.74f),FortColor=new Color(.72f,.6f,.42f);
  public const float RefinedFactor=1.3f,ReinforceFactor=1.5f,CapacitorRate=1.15f,CapacitorRange=3.9f,FortifyFactor=.25f,CoverFactor=.85f,UpgradeFactor=1.3f,CoreRadius=20f,CoreRepairPer=.05f,EvadeChance=.1f,MarkFactor=1.5f;
  public const int PotionTicks=60000,MineMax=3,FortifyTicks=30000,UpgradeTicks=2500,PullGap=600,FieldTicks=2500,NotFiredGap=600;

  public const int FExperimental=1,FSavant=2,FRestorative=4,FChemist=8,FArmor=16,FModified=32,FUpgrade=64,FPerfect=128,FReinforce=256,FCapacitor=512,FFortify=1024,FCitadel=2048;
  static readonly Dictionary<string,int> FlagOf=new Dictionary<string,int>{{"LP_AL_Experimental",FExperimental},{"LP_AL_Savant",FSavant},{"LP_AL_Restorative",FRestorative},{"LP_AL_Chemist",FChemist},
   {"LP_AR_ArcaneArmor",FArmor},{"LP_AR_Modifications",FModified},{"LP_AR_Upgrade",FUpgrade},{"LP_AR_Perfected",FPerfect},
   {"LP_FE_Reinforce",FReinforce},{"LP_FE_Capacitor",FCapacitor},{"LP_FE_Fortify",FFortify},{"LP_FE_Citadel",FCitadel}};
  public static readonly Dictionary<int,int> Users=new Dictionary<int,int>();
  public static readonly HashSet<Pawn> LightFeet=new HashSet<Pawn>(); // 轻盈药剂：走路无视地形（ClassWoodstride）
  public static int Flags(Pawn p){if(p==null || Users.Count==0)return 0;Users.TryGetValue(p.thingIDNumber,out int f);return f;}
  public static bool S(Pawn p,int f)=>(Flags(p) & f)!=0;
  static int Now=>Find.TickManager.TicksGame;
  static int Day=>GenDate.DaysPassed+1;
  static float Roll=>ClassMartial2.RollOverride??Rand.Value;
  static bool Hostile(Pawn a,Pawn t)=>AnyTarget.Hostile(a,t); // 1.136.0 热修：手动选的目标不分敌我
  static bool Mine(Hediff_Adventurer h)=>ClassSubclass.Has(h,Alchemist) || ClassSubclass.Has(h,Armorer) || ClassSubclass.Has(h,Fortress);
  static void Passive(Pawn p,string hediff,bool on){if(on){if(!Items.Has(p,hediff))Items.Give(p,hediff);}else if(Items.Has(p,hediff))Items.Remove(p,hediff);}
  static readonly DefRef<LP_ClassDef> artificer=new DefRef<LP_ClassDef>("LP_Class_Artificer");
  public static int Level(Pawn p)=>ClassSystem.Level(p,artificer.Def);
  public static float Artifice(Pawn p){var s=DefCache<StatDef>.Get("LP_ArtificePower");return s==null?1f:p.GetStatValue(s);}
  public static GameComponent_Subclass4 State=>GameComp<GameComponent_Subclass4>.Get();

  public static int Refresh(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return 0;int f=0;
   if(!p.Dead && Mine(h)){var list=p.abilities?.abilities;if(list!=null)for(int k=0;k<list.Count;k++)if(FlagOf.TryGetValue(list[k].def.defName,out int b))f|=b;}
   if(f==0)Users.Remove(p.thingIDNumber);else Users[p.thingIDNumber]=f;
   return f;
  }
  // ClassSystem.Tick250 → 这里。
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;if(p==null)return;
   if(Users.Count==0 && !Mine(h))return;
   if(p.Dead){Users.Remove(p.thingIDNumber);return;}
   int f=Refresh(h);var d=ClassSubclass.Data(h);
   Passive(p,"LP_AL_SavantBuff",(f & FSavant)!=0);Passive(p,"LP_AL_ResistBuff",(f & FRestorative)!=0);
   if((f & FExperimental)!=0 && d!=null)DailyPotions(p,d,f);
   if((f & FArmor)!=0 && d!=null)ArmorTick(p,d,f);else Items.Remove(p,"LP_AR_ArmorOn");
   if((f & FReinforce)!=0)RepairTick(p);
  }

  // ==================== 炼金师 ====================
  public static readonly string[] Potions={"LP_ALP_Healing","LP_ALP_Swift","LP_ALP_Tough","LP_ALP_Light","LP_ALP_Lucid","LP_ALP_Courage"};
  public static readonly string[] PotionLabels={"治愈","迅捷","坚韧","轻盈","清醒","勇气"};
  public static int PerDay(int f)=>(f & FChemist)!=0?3:(f & FSavant)!=0?2:1;
  // 化学大师自选：0 = 随机，1～6 = 指定种类。
  public static int Pick(Pawn p){var d=ClassSubclass.Data(p);if(d==null || !S(p,FChemist))return 0;d.ticks.TryGetValue("alPick",out int m);return Mathf.Clamp(m,0,Potions.Length);}
  // 实验药剂：醒着时每个新的一天往背包里放药剂（1 / 2 / 3 瓶）。
  static void DailyPotions(Pawn p,SubclassData d,int f){
   if(!p.Awake() || p.inventory==null)return;d.ticks.TryGetValue("alDay",out int day);if(day==Day)return;d.ticks["alDay"]=Day;
   int n=PerDay(f),pick=Pick(p);var got=new List<string>();
   for(int i=0;i<n;i++){var th=GivePotion(p,pick>0?Potions[pick-1]:Potions.RandomElement());if(th!=null)got.Add(th.def.label);}
   if(got.Count>0)Items.Mote(p,"实验药剂："+string.Join("、",got),AlchemyColor);
  }
  public static Thing MakePotion(Pawn maker,string defName){
   var def=DefCache<ThingDef>.Get(defName);if(def==null)return null;var th=ThingMaker.MakeThing(def);
   var c=th.TryGetComp<CompAlchemistPotion>();if(c!=null)c.refined=S(maker,FSavant);return th;
  }
  public static Thing GivePotion(Pawn p,string defName){
   var th=MakePotion(p,defName);if(th==null)return null;
   if(p.inventory!=null && p.inventory.innerContainer.TryAdd(th,true))return th;
   if(p.Spawned){GenPlace.TryPlaceThing(th,p.Position,p.Map,ThingPlaceMode.Near);return th;}
   th.Destroy();return null;
  }
  // 喝下药剂（谁都能喝）；精炼药剂（炼金学识）治疗量、持续时间 ×1.3。
  public static string Drink(Pawn p,string effect,float f){
   if(p==null || p.Dead)return null;
   switch(effect){
    case "heal":{foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>x.Bleeding).ToList())inj.Tended(.5f,1f);float h=ClassMelee.Heal(p,20f*f);Items.Mote(p,"治愈药剂 "+h.ToString("0"),AlchemyColor);return "heal";}
    case "Swift":case "Tough":case "Light":case "Lucid":case "Courage":Items.Give(p,"LP_ALB_"+effect,Mathf.RoundToInt(PotionTicks*f));Items.Mote(p,DefCache<HediffDef>.Get("LP_ALB_"+effect)?.label??effect,AlchemyColor);return effect;
    case "restore":{var what=CureOne(p);Items.Mote(p,what==null?"复原试剂：没有可治的":"复原试剂："+what,AlchemyColor);return what;}
    case "greater":{int n=Greater(p);Items.Mote(p,"大复原药剂（"+n+" 项）",AlchemyColor);return n.ToString();}
   }
   return null;
  }
  static readonly HashSet<string> Diseases=new HashSet<string>{"Flu","Plague","Malaria","SleepingSickness","GutWorms","MuscleParasites","FibrousMechanites","SensoryMechanites","FoodPoisoning","WoundInfection","LungRot","LungRotExposure"};
  public static bool Disease(Hediff h)=>h!=null && Diseases.Contains(h.def.defName);
  // 复原试剂：治好最重的一种疾病；没有病就清除毒素累积。返回治好的名字。
  public static string CureOne(Pawn p){
   var h=p.health.hediffSet.hediffs.Where(Disease).OrderByDescending(x=>x.Severity).FirstOrDefault();
   if(h!=null){string n=h.LabelCap;p.health.RemoveHediff(h);return n;}
   var tox=p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);if(tox!=null){p.health.RemoveHediff(tox);return "毒素累积";}
   return null;
  }
  // 大复原药剂：长回一个缺失的肢体或器官（最大的那个），清除所有疾病、成瘾和毒素累积。返回处理的项数。
  public static int Greater(Pawn p){
   int n=0;var miss=p.health.hediffSet.GetMissingPartsCommonAncestors().Where(x=>x.Part!=null).OrderByDescending(x=>x.Part.coverageAbsWithChildren).FirstOrDefault();
   if(miss!=null){p.health.RestorePart(miss.Part,null,true);n++;}
   foreach(var h in p.health.hediffSet.hediffs.Where(x=>Disease(x) || x is Hediff_Addiction || x.def==HediffDefOf.ToxicBuildup).ToList()){p.health.RemoveHediff(h);n++;}
   return n;
  }
  static bool Toxic(DamageDef d)=>d!=null && (d.defName=="LP_DragonPoison" || d.defName=="LP_OozeAcid" || d.defName=="LP_AlchemyAcid" || d.defName=="AcidBurn" || d.defName=="ToxGas");
  static bool Burning(DamageDef d)=>d==DamageDefOf.Flame || d==DamageDefOf.Burn;

  // ==================== 装甲师 ====================
  public static readonly string[] ArmorModes={"守护者","渗透者"};
  public static int ArmorMode(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;d.ticks.TryGetValue("arMode",out int m);return Mathf.Clamp(m,0,1);}
  public static bool Guardian(Pawn p)=>ArmorMode(p)==0 && ArcaneArmor(p)!=null;
  public static bool Infiltrator(Pawn p)=>ArmorMode(p)==1 && ArcaneArmor(p)!=null;
  // 能改的护甲：盖住躯干的外层或中层衣服，利器护甲 30% 以上（防弹衣、海军装甲、动力甲之类）；挑利器护甲最高的一件。
  public static Apparel Candidate(Pawn p)=>p.apparel?.WornApparel.Where(a=>a.def.apparel!=null && a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso) && (a.def.apparel.layers.Contains(ApparelLayerDefOf.Shell) || a.def.apparel.layers.Contains(ApparelLayerDefOf.Middle)) && a.GetStatValue(StatDefOf.ArmorRating_Sharp)>=.3f).OrderByDescending(a=>a.GetStatValue(StatDefOf.ArmorRating_Sharp)).FirstOrDefault();
  public static Apparel ArcaneArmor(Pawn p){
   if(Users.Count==0 || p?.apparel==null)return null;var d=ClassSubclass.Data(p);if(d==null || !d.ticks.TryGetValue("arArmor",out int id))return null;
   var list=p.apparel.WornApparel;for(int i=0;i<list.Count;i++)if(list[i].thingIDNumber==id)return list[i];return null;
  }
  public static Thing Workbench(Pawn p)=>p.Map==null?null:GenRadial.RadialDistinctThingsAround(p.Position,p.Map,3.9f,true).FirstOrDefault(t=>t is Building_WorkTable);
  public static string Convert(Pawn p,Apparel a){
   var d=ClassSubclass.Data(p);if(d==null || a==null)return null;d.ticks["arArmor"]=a.thingIDNumber;Modify(p,d,a);
   Items.Mote(p,"奥术装甲："+a.LabelNoCount,ArmorColor);return a.LabelNoCount;
  }
  // 甲胄改装：奥术装甲的附魔强化 +2（每件只加一次，上限照旧）。
  static void Modify(Pawn p,SubclassData d,Apparel a){
   if(!S(p,FModified) && !ClassSystem.Knows(p,"LP_AR_Modifications"))return;string key="arEnh"+a.thingIDNumber;if(d.ticks.ContainsKey(key))return;
   var c=GearEnhance.Comp(a);if(c==null)return;c.level=Mathf.Min(GearEnhance.Max,c.level+2);GearEnhance.ClearStatCache(a);d.ticks[key]=1;
  }
  static void ArmorTick(Pawn p,SubclassData d,int f){
   var a=ArcaneArmor(p);if(a==null){Items.Remove(p,"LP_AR_ArmorOn");return;}
   if((f & FModified)!=0)Modify(p,d,a);
   var h=Items.Has(p,"LP_AR_ArmorOn")?p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_AR_ArmorOn")):Items.Give(p,"LP_AR_ArmorOn");
   float slow=0f;if(a.def.equippedStatOffsets!=null)foreach(var m in a.def.equippedStatOffsets)if(m.stat==StatDefOf.MoveSpeed && m.value<0f)slow-=m.value;
   (h as Hediff_ArcaneArmor)?.Set(slow,ArmorMode(p)==1,(f & FUpgrade)!=0,(f & FPerfect)!=0);
   if((f & FPerfect)!=0 && ArmorMode(p)==0 && p.Spawned && !p.Downed){d.ticks.TryGetValue("arPull",out int last);if(last<=0 || Now-last>=PullGap || Now<last){if(Pull(p)!=null)d.ticks["arPull"]=Math.Max(1,Now);}}
  }
  // 完美装甲·守护者：把 6 格内一个敌人磁力拉到身边并嘲讽。
  public static Pawn Pull(Pawn p){
   var t=Races124.PawnsNear(p.Map,p.Position,6f).Where(x=>Hostile(p,x) && !x.Downed && x.Spawned && !x.Position.AdjacentTo8WayOrInside(p.Position)).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault();if(t==null)return null;
   var cells=GenAdj.CellsAdjacent8Way(p).Where(c=>c.InBounds(p.Map) && c.Standable(p.Map) && c.GetFirstPawn(p.Map)==null).OrderBy(c=>c.DistanceToSquared(t.Position)).ToList();
   if(cells.Count>0)DNDPush.Move(t,cells[0]);Taunt(p,t);Items.Mote(t,"磁力牵引",ArmorColor);return t;
  }
  static void Taunt(Pawn p,Pawn t){if(t.mindState!=null)t.mindState.enemyTarget=p;Items.Give(t,"LP_Taunted",600);}
  public static float FieldAmount(Pawn p)=>(10f+2f*Level(p))*(S(p,FUpgrade)?2f:1f);
  public static bool NotFired(Pawn t)=>t.mindState==null || t.mindState.lastAttackTargetTick<=0 || Now-t.mindState.lastAttackTargetTick>NotFiredGap;
  public static float LauncherDamage(Pawn p,Pawn t)=>12f*Artifice(p)*(S(p,FUpgrade)?1.5f*(NotFired(t)?2f:1f):1f);
  static void Mark(Pawn a,Pawn v){Items.Give(v,"LP_AR_Marked",600);Items.Give(v,"LP_Spotted",600);foreach(var n in new[]{"LP_SH_Cloaked","LP_ATInvisible","LP_HideInPlain","LP_ShadowStep"})Items.Remove(v,n);}
  // 甲胄改装后：奥术装甲和头盔不会被打烂（最多剩 1 点耐久）。
  public static bool Kept(Pawn w,Apparel ap)=>ap==ArcaneArmor(w) || ap.def.apparel!=null && ap.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead);

  // ==================== 堡垒工程师 ====================
  // 防御建筑：墙、门、沙袋这类掩体、炮台。
  public static bool Defensive(ThingDef d)=>d!=null && d.building!=null && !d.building.isNaturalRock && (typeof(Building_Door).IsAssignableFrom(d.thingClass) || typeof(Building_Turret).IsAssignableFrom(d.thingClass) || d.IsWall || d.passability==Traversability.PassThroughOnly && d.fillPercent>=.3f);
  // 1.7 的最大生命是“不变”属性，按物品永久缓存：加固后（和读档后）清掉缓存。
  static readonly AccessTools.FieldRef<StatWorker,System.Collections.Concurrent.ConcurrentDictionary<Thing,float>> hpCache=AccessTools.FieldRefAccess<StatWorker,System.Collections.Concurrent.ConcurrentDictionary<Thing,float>>("immutableStatCache");
  public static void ClearHpCache(Thing t){var c=hpCache(StatDefOf.MaxHitPoints.Worker);if(c==null)return;if(t==null)c.Clear();else c.TryRemove(t,out _);}
  public static bool Reinforced(Thing t){var s=State;return t!=null && s!=null && s.reinforced.Count>0 && s.reinforced.Contains(t.thingIDNumber);}
  public static void Reinforce(Thing b){
   var s=State;if(s==null || b==null || !s.reinforced.Add(b.thingIDNumber))return;ClearHpCache(b);
   b.HitPoints=Mathf.RoundToInt(b.def.GetStatValueAbstract(StatDefOf.MaxHitPoints,b.Stuff)*ReinforceFactor);
   Items.Mote(b,"加固施工",FortColor);
  }
  // 自己修理速度 ×2：修理时每 250 刻再补一份原版的修理量。
  static void RepairTick(Pawn p){
   if(!(p.jobs?.curDriver is JobDriver_Repair) || !(p.CurJob?.targetA.Thing is Building b) || !b.Spawned || b.HitPoints>=b.MaxHitPoints)return;
   if(!b.OccupiedRect().ExpandedBy(1).Contains(p.Position))return;
   b.HitPoints=Mathf.Min(b.MaxHitPoints,b.HitPoints+Mathf.Max(1,Mathf.RoundToInt(250f*p.GetStatValue(StatDefOf.ConstructionSpeed)*1.7f/80f)));
   if(b.HitPoints>=b.MaxHitPoints)b.Map.listerBuildingsRepairable.Notify_BuildingRepaired(b);
  }
  public static IEnumerable<Building_RuneMine> Mines(Pawn p)=>p.Map==null?Enumerable.Empty<Building_RuneMine>():p.Map.listerThings.ThingsOfDef(DefCache<ThingDef>.Get("LP_RuneMine")).OfType<Building_RuneMine>().Where(m=>m.ownerId==p.thingIDNumber);
  public static Building_RuneMine PlaceMine(Pawn p,IntVec3 c){
   var def=DefCache<ThingDef>.Get("LP_RuneMine");if(def==null || p.Map==null)return null;
   var m=(Building_RuneMine)ThingMaker.MakeThing(def);m.ownerId=p.thingIDNumber;GenSpawn.Spawn(m,c,p.Map);if(p.Faction!=null)m.SetFaction(p.Faction);
   FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),p.Map,1f,FortColor);Items.Mote(m,"符文地雷",FortColor);return m;
  }
  public static void MineBlast(Building_RuneMine m,Pawn t){
   if(t==null || t.Dead)return;var owner=m.Map?.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.thingIDNumber==m.ownerId);
   t.TakeDamage(new DamageInfo(Warlock.ForceDamage,20f*(owner==null?1f:Artifice(owner)),.2f,-1f,owner??(Thing)m));
   if(!t.Dead)t.stances?.stunner?.StunFor(120,owner);if(m.Spawned)FleckMaker.ThrowLightningGlow(m.DrawPos,m.Map,1.5f);Items.Mote(t,"符文地雷",FortColor);
  }
  // 奥术电容上限：殖民地里每个会「奥术电容」的工程师 器械师等级 ÷ 3 台，合计。
  public static int CapacitorLimit(){int n=0;foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)if(ClassSubclass.Has(p,Fortress) && ClassSystem.Knows(p,"LP_FE_Capacitor"))n+=Level(p)/3;return n;}
  public static bool HasCitadelEngineer(){foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)if(ClassSubclass.Has(p,Fortress) && ClassSystem.Knows(p,"LP_FE_Citadel"))return true;return false;}
  public static int CountBuilt(ThingDef def){int n=0;foreach(var map in Find.Maps)n+=map.listerThings.ThingsOfDef(def).Count+map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Count(t=>t.def.entityDefToBuild==def)+map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Count(t=>t.def.entityDefToBuild==def);return n;}
  public static bool NearCapacitor(Thing turret){
   foreach(var c in Building_ArcaneCapacitor.Live)if(c.Map==turret.Map && c.Position.InHorDistOf(turret.Position,CapacitorRange) && (c.PowerOn))return true;
   return false;
  }
  // 战地加固：5×5 内的建筑 12 小时受伤 ×0.25。
  public static int Fortify(Pawn p,IntVec3 center){
   var s=State;if(s==null || p.Map==null)return 0;int n=0;var seen=new HashSet<Thing>();
   foreach(var c in CellRect.CenteredOn(center,2).ClipInsideMap(p.Map))foreach(var t in c.GetThingList(p.Map))if(t is Building b && b.def.useHitPoints && seen.Add(b)){s.fortified[b.thingIDNumber]=Now+FortifyTicks;n++;}
   FleckMaker.ThrowDustPuffThick(center.ToVector3Shifted(),p.Map,3f,FortColor);Items.Mote(p,"战地加固 ×"+n,FortColor);return n;
  }
  public static bool Fortified(Thing b){var s=State;return s!=null && s.fortified.Count>0 && s.fortified.TryGetValue(b.thingIDNumber,out int until) && until>Now;}
  public static bool Upgraded(Thing turret){var s=State;return s!=null && s.upgraded.Count>0 && s.upgraded.TryGetValue(turret.thingIDNumber,out int until) && until>Now;}
  // 要塞核心：半径 20 内自己建的防御建筑每小时修复 5%。
  public static int CoreRepair(Building_FortressCore core){
   int n=0;var map=core.Map;if(map==null)return 0;
   foreach(var t in GenRadial.RadialDistinctThingsAround(core.Position,map,CoreRadius,true))if(t is Building b && b.def.useHitPoints && b.HitPoints<b.MaxHitPoints && Reinforced(b)){b.HitPoints=Mathf.Min(b.MaxHitPoints,b.HitPoints+Mathf.Max(1,Mathf.RoundToInt(b.MaxHitPoints*CoreRepairPer)));if(b.HitPoints>=b.MaxHitPoints)map.listerBuildingsRepairable.Notify_BuildingRepaired(b);n++;}
   return n;
  }
  // 全线升级：范围内所有炮台（含符文炮台）1 小时伤害 ×1.3。
  public static int Upgrade(Building_FortressCore core){
   var s=State;var map=core.Map;if(s==null || map==null)return 0;int n=0;
   foreach(var t in GenRadial.RadialDistinctThingsAround(core.Position,map,CoreRadius,true))if(t is Building_Turret){s.upgraded[t.thingIDNumber]=Now+UpgradeTicks;n++;}
   FleckMaker.ThrowLightningGlow(core.DrawPos,map,4f);Items.Mote(core,"全线升级 ×"+n,FortColor);return n;
  }
  static bool BesideCover(Pawn v){
   if(!v.Spawned || State==null || State.reinforced.Count==0)return false;
   foreach(var c in GenAdj.CellsAdjacent8Way(v))if(c.InBounds(v.Map)){var e=c.GetEdifice(v.Map);if(e!=null && e.def.fillPercent>=.3f && Reinforced(e))return true;}
   return false;
  }

  // ---------- 主动能力 ----------
  static readonly HashSet<string> Known=new HashSet<string>{"S4Restorative","S4Greater","S4Pick","S4Armor","S4ArmorMode","S4Field","S4Launcher","S4Mine","S4Fortify"};
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(!Known.Contains(e.effect))return null;var t=target.Thing as Pawn;var map=caster.Map;
   switch(e.effect){
    case "S4Armor":{var a=Candidate(caster);if(a==null)return "身上没有能改的躯干护甲（防弹衣、海军装甲、动力甲之类）。";if(a==ArcaneArmor(caster))return "这件已经是奥术装甲了。";if(Workbench(caster)==null)return "要站在工作台旁边（3 格内）。";return null;}
    case "S4ArmorMode":return null;
    case "S4Field":return Guardian(caster)?null:"要穿着奥术装甲、处在守护者模式。";
    case "S4Launcher":if(!Infiltrator(caster))return "要穿着奥术装甲、处在渗透者模式。";if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
    case "S4Mine":{
     if(map==null || !target.Cell.IsValid || !target.Cell.InBounds(map) || !target.Cell.Standable(map) || target.Cell.GetEdifice(map)!=null)return "需要一个能站人的空地。";
     if(target.Cell.GetThingList(map).Any(x=>x.def.category==ThingCategory.Building))return "这里已经有建筑了。";
     if(Mines(caster).Count()>=MineMax)return "已经埋了 "+MineMax+" 个符文地雷（踩掉或拆掉之后才能再埋）。";return null;}
    case "S4Fortify":if(map==null || !target.Cell.IsValid || !target.Cell.InBounds(map))return "需要选择一个位置。";return null;
    case "S4Pick":return null;
   }
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(!Known.Contains(e.effect))return false;var t=target.Thing as Pawn;
   switch(e.effect){
    case "S4Restorative":{var th=GivePotion(p,"LP_ALP_Restorative");Items.Mote(p,th!=null?"复原试剂":"背包装不下",AlchemyColor);break;}
    case "S4Greater":{var th=GivePotion(p,"LP_ALP_GreaterRestorative");Items.Mote(p,th!=null?"大复原药剂":"背包装不下",AlchemyColor);break;}
    case "S4Pick":{var d=ClassSubclass.Data(p);if(d!=null){int m=(Pick(p)+1)%(Potions.Length+1);d.ticks["alPick"]=m;Items.Mote(p,"每日药剂："+(m==0?"随机":PotionLabels[m-1]),AlchemyColor);}break;}
    case "S4Armor":Convert(p,Candidate(p));break;
    case "S4ArmorMode":{var d=ClassSubclass.Data(p);if(d!=null){int m=1-ArmorMode(p);d.ticks["arMode"]=m;Items.Mote(p,"奥术装甲："+ArmorModes[m],ArmorColor);}break;}
    case "S4Field":{float amt=FieldAmount(p);var h=Items.Give(p,"LP_ShieldSpell",FieldTicks,amt);if(h!=null)h.Severity=Mathf.Max(h.Severity,amt);Items.Mote(p,"防御力场 "+amt.ToString("0"),ArmorColor);break;}
    case "S4Launcher":if(Hostile(p,t)){float amt=LauncherDamage(p,t);t.TakeDamage(new DamageInfo(ClassCasters.Lightning??DamageDefOf.Burn,amt,.3f,-1f,p));if(t.Spawned)FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,1.2f);Items.Mote(t,"闪电发射器 "+amt.ToString("0"),ArmorColor);}break;
    case "S4Mine":PlaceMine(p,target.Cell);break;
    case "S4Fortify":Fortify(p,target.Cell);break;
   }
   return true;
  }

  // ---------- 伤害（Subclass4Damage） ----------
  public static bool Busy;
  public static float DamageFactor(Pawn v,DamageInfo d){
   float g=1f;
   if(d.Instigator is Building_Turret tb && Upgraded(tb))g*=UpgradeFactor; // 全线升级
   if(Users.Count==0)return g;
   // 炼金：毒、酸减半（复原试剂）；化学大师免疫毒、酸、燃烧。
   if(S(v,FChemist) && (Toxic(d.Def) || Burning(d.Def)))return 0f;
   if(S(v,FRestorative) && Toxic(d.Def))g*=.5f;
   var a=d.Instigator as Pawn;bool hostile=a!=null && a!=v && a.HostileTo(v);bool melee=hostile && ClassMartial2.Melee(d);
   if(Items.Has(v,"LP_AR_Marked") && hostile && !(S(a,FPerfect) && Infiltrator(a))){Items.Remove(v,"LP_AR_Marked");g*=MarkFactor;} // 渗透者标记：队友下一击 ×1.5
   if(!hostile)return g;
   // 攻击方：雷霆拳套（守护者近战附加电击并嘲讽）；完美装甲·渗透者命中后标记。
   if(!Busy && melee && S(a,FArmor) && Guardian(a)){float amt=6f*Artifice(a);var tv=v;var me=a;Fiends.Defer(()=>{if(tv.Dead || !tv.Spawned)return;Busy=true;try{tv.TakeDamage(new DamageInfo(ClassCasters.Lightning??DamageDefOf.Burn,amt,.2f,-1f,me));}finally{Busy=false;}if(!tv.Dead)Taunt(me,tv);});}
   if(S(a,FPerfect) && Infiltrator(a))Mark(a,v);
   // 受击方：渗透者远程闪避 10%；自己建的掩体旁边远程 ×0.85。
   if(!melee && Infiltrator(v) && Roll<EvadeChance){Items.Mote(v,"闪开了",ArmorColor);return 0f;}
   if(!melee && v.Faction!=null && v.Faction.IsPlayer && BesideCover(v))g*=CoverFactor;
   return g;
  }
  public static void Reset(){Users.Clear();LightFeet.Clear();Busy=false;Building_ArcaneCapacitor.Live.RemoveWhere(c=>c==null || !c.Spawned);}
 }

 // 1.111.0 状态：加固过的建筑、战地加固、全线升级（按 thingIDNumber）。
 public class GameComponent_Subclass4 : GameComponent {
  public HashSet<int> reinforced=new HashSet<int>();
  public Dictionary<int,int> fortified=new Dictionary<int,int>(),upgraded=new Dictionary<int,int>();
  public GameComponent_Subclass4(Game game){}
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref reinforced,"reinforced",LookMode.Value);
   Scribe_Collections.Look(ref fortified,"fortified",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref upgraded,"upgraded",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){reinforced=reinforced??new HashSet<int>();fortified=fortified??new Dictionary<int,int>();upgraded=upgraded??new Dictionary<int,int>();}
  }
  public override void GameComponentTick(){
   if(Find.TickManager.TicksGame%GenDate.TicksPerHour!=0)return;int now=Find.TickManager.TicksGame;
   foreach(var k in fortified.Where(kv=>kv.Value<=now).Select(kv=>kv.Key).ToList())fortified.Remove(k);
   foreach(var k in upgraded.Where(kv=>kv.Value<=now).Select(kv=>kv.Key).ToList())upgraded.Remove(k);
  }
  public override void FinalizeInit(){
   base.FinalizeInit();Subclass4.Reset();if(reinforced.Count>0)Subclass4.ClearHpCache(null);
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawns)if(Items.Has(p,"LP_ALB_Light"))Subclass4.LightFeet.Add(p);
  }
 }

 // ---------- 药剂 ----------
 public class CompProperties_AlchemistPotion : CompProperties {public CompProperties_AlchemistPotion(){compClass=typeof(CompAlchemistPotion);}}
 // 精炼（炼金学识做的）：效果 ×1.3；精炼与普通的分开堆叠。
 public class CompAlchemistPotion : ThingComp {
  public bool refined;
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref refined,"lpRefined");}
  public override bool AllowStackWith(Thing other)=>other.TryGetComp<CompAlchemistPotion>()?.refined==refined;
  public override void PostSplitOff(Thing piece){base.PostSplitOff(piece);var c=piece.TryGetComp<CompAlchemistPotion>();if(c!=null)c.refined=refined;}
  public override string TransformLabel(string label)=>refined?label+"（精炼）":label;
  public override void Notify_RecipeProduced(Pawn pawn){base.Notify_RecipeProduced(pawn);refined=Subclass4.S(pawn,Subclass4.FSavant) || ClassSystem.Knows(pawn,"LP_AL_Savant");}
 }
 public class IngestionOutcomeDoer_Alchemist : IngestionOutcomeDoer {
  public string effect;
  protected override void DoIngestionOutcomeSpecial(Pawn pawn,Thing ingested,int ingestedCount)=>Subclass4.Drink(pawn,effect,ingested?.TryGetComp<CompAlchemistPotion>()?.refined==true?Subclass4.RefinedFactor:1f);
 }
 // 轻盈：走路无视地形减速（登记到 Subclass4.LightFeet）。
 public class Hediff_LightFoot : HediffWithComps {
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);if(pawn!=null)Subclass4.LightFeet.Add(pawn);}
  public override void PostRemoved(){base.PostRemoved();if(pawn!=null)Subclass4.LightFeet.Remove(pawn);}
 }
 // 只有炼金师（器械师 3 级以上选了炼金师）能做实验药剂的配方。
 public class AlchemistRecipeExtension : DefModExtension {}

 // ---------- 奥术装甲：属性按护甲和模式现算 ----------
 public class Hediff_ArcaneArmor : HediffWithComps {
  public float slow;public bool infil,upgrade,sealedSuit;HediffStage stage;
  public void Set(float s,bool inf,bool up,bool seal){
   if(stage!=null && Mathf.Approximately(s,slow) && inf==infil && up==upgrade && seal==sealedSuit)return;
   slow=s;infil=inf;upgrade=up;sealedSuit=seal;stage=null;pawn?.health?.Notify_HediffChanged(this);
  }
  public override string LabelInBrackets=>infil?Subclass4.ArmorModes[1]:Subclass4.ArmorModes[0];
  public override HediffStage CurStage=>stage??(stage=Build());
  HediffStage Build(){
   var s=new HediffStage{statOffsets=new List<StatModifier>(),statFactors=new List<StatModifier>()};
   if(slow>0f)s.statOffsets.Add(new StatModifier{stat=StatDefOf.MoveSpeed,value=slow});
   if(infil)s.statFactors.Add(new StatModifier{stat=StatDefOf.MoveSpeed,value=1.1f});
   if(upgrade)foreach(var st in new[]{StatDefOf.ArmorRating_Sharp,StatDefOf.ArmorRating_Blunt,StatDefOf.ArmorRating_Heat})s.statOffsets.Add(new StatModifier{stat=st,value=.15f});
   if(sealedSuit)foreach(var n in new[]{"ToxicEnvironmentResistance","VacuumResistance"}){var st=DefCache<StatDef>.Get(n);if(st!=null)s.statOffsets.Add(new StatModifier{stat=st,value=1f});}
   return s;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref slow,"lpSlow");Scribe_Values.Look(ref infil,"lpInfil");Scribe_Values.Look(ref upgrade,"lpUp");Scribe_Values.Look(ref sealedSuit,"lpSeal");}
 }

 // ---------- 建筑 ----------
 public class Building_RuneMine : Building_Trap {
  public int ownerId;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ownerId,"lpOwner");}
  protected override void SpringSub(Pawn p){if(p!=null)Subclass4.MineBlast(this,p);}
 }
 public class Building_ArcaneCapacitor : Building {
  public static readonly HashSet<Building_ArcaneCapacitor> Live=new HashSet<Building_ArcaneCapacitor>();
  public bool PowerOn=>GetComp<CompPowerTrader>()?.PowerOn??true;
  public override void SpawnSetup(Map map,bool respawningAfterLoad){base.SpawnSetup(map,respawningAfterLoad);Live.Add(this);}
  public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish){Live.Remove(this);base.DeSpawn(mode);}
  public override void DrawExtraSelectionOverlays(){base.DrawExtraSelectionOverlays();GenDraw.DrawRadiusRing(Position,Subclass4.CapacitorRange);}
 }
 public class Building_FortressCore : Building {
  public bool raidUsed;int acc;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref raidUsed,"lpRaidUsed");Scribe_Values.Look(ref acc,"lpAcc");}
  public static bool Raid(Map map)=>map!=null && map.attackTargetsCache.TargetsHostileToColony.Any(t=>t.Thing is Pawn p && !p.Downed && !p.Dead && p.Spawned);
  public override void TickRare(){
   base.TickRare();if(!Spawned)return;
   if((acc+=250)>=GenDate.TicksPerHour){acc=0;Subclass4.CoreRepair(this);}
   if(raidUsed && !Raid(Map))raidUsed=false;
  }
  public override void DrawExtraSelectionOverlays(){base.DrawExtraSelectionOverlays();GenDraw.DrawRadiusRing(Position,Subclass4.CoreRadius);}
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(Faction!=Faction.OfPlayer)yield break;
   var c=new Command_Action{defaultLabel="全线升级",defaultDesc="半径 "+Subclass4.CoreRadius.ToString("0")+" 格内所有炮台（含符文炮台）1 小时内伤害 ×1.3。每场袭击一次（地图上有敌人时才能用）。",icon=ContentFinder<Texture2D>.Get("LP/Classes/Subclass4/FortCitadel",false)??BaseContent.BadTex,action=()=>{raidUsed=true;Subclass4.Upgrade(this);}};
   if(raidUsed)c.Disable("这场袭击已经用过了。");else if(!Raid(Map))c.Disable("现在没有袭击。");
   yield return c;
  }
 }
 // 奥术电容：数量受工程师限制（器械师等级 ÷ 3，合计）。
 public class PlaceWorker_ArcaneCapacitor : PlaceWorker {
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef,IntVec3 loc,Rot4 rot,Map map,Thing thingToIgnore=null,Thing thing=null){
   int max=Subclass4.CapacitorLimit();if(max<=0)return "需要殖民地里有会「奥术电容」的堡垒工程师（器械师 7 级）。";
   return Subclass4.CountBuilt(checkingDef as ThingDef)>=max?new AcceptanceReport("奥术电容最多 "+max+" 台（工程师的器械师等级 ÷ 3）。"):AcceptanceReport.WasAccepted;
  }
  public override void DrawGhost(ThingDef def,IntVec3 center,Rot4 rot,Color ghostCol,Thing thing=null){GenDraw.DrawRadiusRing(center,Subclass4.CapacitorRange);}
 }
 // 要塞核心：每个殖民地 1 个，要有会「要塞之心」的工程师。
 public class PlaceWorker_FortressCore : PlaceWorker {
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef,IntVec3 loc,Rot4 rot,Map map,Thing thingToIgnore=null,Thing thing=null){
   if(!Subclass4.HasCitadelEngineer())return "需要殖民地里有会「要塞之心」的堡垒工程师（器械师 15 级）。";
   var def=checkingDef as ThingDef;int n=map.listerThings.ThingsOfDef(def).Count+map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Count(t=>t.def.entityDefToBuild==def)+map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Count(t=>t.def.entityDefToBuild==def);
   return n>=1?new AcceptanceReport("每个殖民地只能有 1 个要塞核心。"):AcceptanceReport.WasAccepted;
  }
  public override void DrawGhost(ThingDef def,IntVec3 center,Rot4 rot,Color ghostCol,Thing thing=null){GenDraw.DrawRadiusRing(center,Subclass4.CoreRadius);}
 }
 // 加固施工：自己建的防御建筑最大生命 ×1.5。
 public class StatPart_Reinforced : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.HasThing && Subclass4.Reinforced(req.Thing))val*=Subclass4.ReinforceFactor;}
  public override string ExplanationPart(StatRequest req)=>req.HasThing && Subclass4.Reinforced(req.Thing)?"加固施工：×"+Subclass4.ReinforceFactor.ToStringPercent():null;
 }
 [StaticConstructorOnStartup]
 public static class Subclass4Startup {
  static Subclass4Startup(){var st=StatDefOf.MaxHitPoints;if(st.parts==null)st.parts=new List<StatPart>();if(!st.parts.Any(x=>x is StatPart_Reinforced))st.parts.Add(new StatPart_Reinforced{parentStat=st});}
 }

 // ==================== Harmony ====================
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class Subclass4Damage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(Subclass4.Users.Count==0 && (Subclass4.State==null || Subclass4.State.upgraded.Count==0))return;
   if(dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   float f=Subclass4.DamageFactor(__instance,dinfo);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
 // 战地加固：建筑受伤 ×0.25。
 [HarmonyPatch(typeof(Building),nameof(Building.PreApplyDamage))]
 public static class Subclass4BuildingDamage {
  static void Prefix(Building __instance,ref DamageInfo dinfo){if(dinfo.Amount>0f && Subclass4.Fortified(__instance))dinfo.SetAmount(dinfo.Amount*Subclass4.FortifyFactor);}
 }
 // 甲胄改装：奥术装甲和头盔最多剩 1 点耐久，不会被打烂掉落。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class Subclass4ArmorKeep {
  static void Prefix(Thing __instance,ref DamageInfo __0){
   if(Subclass4.Users.Count==0 || !(__instance is Apparel ap))return;var w=ap.Wearer;
   if(w==null || !Subclass4.S(w,Subclass4.FModified) || !Subclass4.Kept(w,ap))return;
   if(__0.Amount>=ap.HitPoints)__0.SetAmount(Mathf.Max(0f,ap.HitPoints-1));
  }
 }
 // 加固施工：工程师建成的防御建筑记下来。
 [HarmonyPatch(typeof(Frame),nameof(Frame.CompleteConstruction))]
 public static class Subclass4Build {
  static void Prefix(Frame __instance,Pawn worker,out (Map map,IntVec3 pos,ThingDef def) __state){
   __state=(null,IntVec3.Invalid,null);
   if(Subclass4.Users.Count==0 || worker==null || !Subclass4.S(worker,Subclass4.FReinforce))return;
   __state=(__instance.Map,__instance.Position,__instance.def.entityDefToBuild as ThingDef);
  }
  static void Postfix((Map map,IntVec3 pos,ThingDef def) __state){
   if(__state.map==null || !Subclass4.Defensive(__state.def))return;
   var b=__state.pos.GetThingList(__state.map).FirstOrDefault(x=>x.def==__state.def);if(b!=null)Subclass4.Reinforce(b);
  }
 }
 // 奥术电容：3 格内的炮台射速 ×1.15。
 [HarmonyPatch(typeof(Building_TurretGun),"BurstCooldownTime")]
 public static class Subclass4TurretRate {
  static void Postfix(Building_TurretGun __instance,ref float __result){if(Building_ArcaneCapacitor.Live.Count==0)return;if(Subclass4.NearCapacitor(__instance))__result/=Subclass4.CapacitorRate;}
 }
 // 实验药剂配方：只有炼金师能做。
 [HarmonyPatch(typeof(RecipeDef),nameof(RecipeDef.PawnSatisfiesSkillRequirements))]
 public static class AlchemistRecipeGate {
  static void Postfix(RecipeDef __instance,Pawn pawn,ref bool __result){if(__result && pawn!=null && __instance.HasModExtension<AlchemistRecipeExtension>() && !(ClassSubclass.Has(pawn,Subclass4.Alchemist) && ClassSystem.Knows(pawn,"LP_AL_Experimental")))__result=false;}
 }
}
