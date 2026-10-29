using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.57.0 普通种族（见 ArtSource/CommonRaces-1.57.0-spec.md）：精灵/矮人亚种、半身人、侏儒、半精灵、狗头人、地精三族。
 // 全部是异种型 + 基因；这里是共享的判断、缓存、生成时的外貌/天赋、存档状态。战斗相关补丁在 CommonRaceSubraceCombat.cs。
 // 所有"身边有几个友方"的判断都按 250 刻缓存；补丁先判断基因再做别的事。
 public static class Subraces {
  public const int CacheTicks=250;
  public const float SunlightGlow=.6f,SunlightFactor=.85f,DarknessFactor=.7f,GrudgeFactor=1.05f,HideFactor=.85f,CamouflageFactor=.7f;
  public const float PoisonChance=.15f,PackStep=.05f,HobStep=.03f,FuryFactor=1.15f,SurpriseFactor=1.5f,ToughnessFactor=1.15f,TrapFactor=1.3f,TrapBuildFactor=2f,TinkerToyFactor=2f;
  public const int PackMax=3,HobMax=3,SurpriseTicks=600,EscapeTicks=120,StillTicks=120,GrudgeTicks=60*GenDate.TicksPerDay,DarknessRefresh=90;
  public const float HideRadius=2f,PackRadius=1.5f,HobRadius=3f,SmallBeastSize=.5f,SmallBeastFactor=2f,HoardStep=.02f,HoardMax=.2f;
  // 1.58.6：改用线程安全、查不到也缓存的 DefCache（原来是普通 Dictionary，又会在属性 / 命中钩子里被写入）。
  public static GeneDef G(string n)=>DefCache<GeneDef>.Get(n);
  public static bool Has(Pawn p,string gene)=>p?.genes!=null && CommonRaces.Has(p,G(gene));
  public static bool Has(Pawn p,DefRef<GeneDef> gene)=>p?.genes!=null && CommonRaces.Has(p,gene.Def);
  // 1.58.6：属性部件和部位生命里用到的基因用静态 DefRef。
  public static readonly DefRef<GeneDef> ArmorTraining=new DefRef<GeneDef>("LP_DwarfArmorTraining"),Trapsmith=new DefRef<GeneDef>("LP_KoboldTrapsmith"),Tinker=new DefRef<GeneDef>("LP_RockGnomeTinker"),DwarfToughness=new DefRef<GeneDef>("LP_DwarfToughness");
  public static HediffDef H(string n)=>DefCache<HediffDef>.Get(n);
  public static string Xeno(Pawn p)=>p?.genes?.Xenotype?.defName;
  public static int Now=>Find.TickManager?.TicksGame??0;

  // ---------- 种族判断（异种型或标志基因任一满足） ----------
  public static bool IsDrow(Pawn p)=>Xeno(p)=="LP_Drow" || Has(p,"LP_DrowSunlight");
  public static bool IsHalfling(Pawn p)=>Has(p,"LP_HalflingBlood");
  public static bool IsGnome(Pawn p)=>Has(p,"LP_GnomeBlood");
  public static bool IsKobold(Pawn p)=>Xeno(p)=="LP_Kobold" || Has(p,"LP_KoboldBlood");
  public static bool IsGoblinoid(Pawn p)=>Has(p,"LP_GoblinoidBlood");
  public static bool IsHalfElf(Pawn p)=>Xeno(p)=="LP_HalfElf" || Has(p,"LP_HalfElfBlood");

  // ---------- 缓存的邻近计数 ----------
  static readonly Dictionary<long,int> cacheTick=new Dictionary<long,int>(),cacheValue=new Dictionary<long,int>();
  public static int cacheMisses; // 自检用：计算（非缓存命中）次数
  // 1.58.6：加锁（属性和命中钩子可能不在主线程，原来是无锁写普通 Dictionary）；命中缓存时不再分配闭包——计算放在单独的方法里，只在未命中时调用。
  static readonly object cacheLock=new object();
  public static void ClearCaches(){lock(cacheLock){cacheTick.Clear();cacheValue.Clear();}}
  static bool TryCached(long key,out int v){
   int now=Now;
   lock(cacheLock){if(cacheTick.TryGetValue(key,out int t) && now>=t && now-t<CacheTicks){v=cacheValue[key];return true;}}
   v=0;return false;
  }
  static int Store(long key,int v){
   int now=Now;
   lock(cacheLock){if(cacheTick.Count>4000){cacheTick.Clear();cacheValue.Clear();}cacheMisses++;cacheTick[key]=now;cacheValue[key]=v;}
   return v;
  }
  static int Cached(long key,Func<int> compute)=>TryCached(key,out int v)?v:Store(key,compute());
  public static int CountPawnsNear(Map map,IntVec3 center,float radius,Func<Pawn,bool> pred,int max){
   if(map==null)return 0;int n=0,cells=GenRadial.NumCellsInRadius(radius);
   for(int i=0;i<cells;i++){
    var c=center+GenRadial.RadialPattern[i];if(!c.InBounds(map))continue;
    var list=map.thingGrid.ThingsListAtFast(c);
    for(int j=0;j<list.Count;j++)if(list[j] is Pawn o && pred(o) && ++n>=max)return n;
   }
   return n;
  }
  static bool Friendly(Pawn o,Faction f)=>f!=null && o.Faction==f && !o.Dead && !o.Downed;
  // 躲到大个子身后：2格内有体型≥1的同派系单位。
  public static bool BigFriendNear(Pawn p){
   if(p?.Map==null || p.Faction==null)return false;
   long key=1L<<40|(uint)p.thingIDNumber;
   return (TryCached(key,out int v)?v:Store(key,BigFriendCount(p)))>0;
  }
  static int BigFriendCount(Pawn p)=>CountPawnsNear(p.Map,p.Position,HideRadius,o=>o!=p && Friendly(o,p.Faction) && o.BodySize>=1f,1);
  // 群体战术：目标1格内（不含攻击者）的攻击者本方单位数，最多3。
  public static int PackCount(Pawn caster,Thing target){
   if(caster?.Map==null || caster.Faction==null || target==null)return 0;var f=caster.Faction;
   long key=2L<<40|((long)(uint)target.thingIDNumber<<8)^(uint)f.loadID^((long)(uint)caster.thingIDNumber<<24);
   return TryCached(key,out int v)?v:Store(key,PackCompute(caster,target));
  }
  static int PackCompute(Pawn caster,Thing target){var f=caster.Faction;return CountPawnsNear(caster.Map,target.Position,PackRadius,o=>o!=caster && o!=target && Friendly(o,f),PackMax);}
  // 群体之助：3格内的本方人形，最多3。
  public static int HobCount(Pawn p){
   if(p?.Map==null || p.Faction==null)return 0;
   long key=3L<<40|(uint)p.thingIDNumber;
   return TryCached(key,out int v)?v:Store(key,HobCompute(p));
  }
  static int HobCompute(Pawn p)=>CountPawnsNear(p.Map,p.Position,HobRadius,o=>o!=p && o.RaceProps.Humanlike && Friendly(o,p.Faction),HobMax);
  public static float PackFactor(Pawn caster,Thing target)=>1f+PackStep*PackCount(caster,target);
  public static float HobFactor(Pawn p)=>1f+HobStep*HobCount(p);
  // 本殖民地狗头人（全部地图 / 指定地图，带龙之仆从）。
  public static int KoboldColonists()=>Cached(4L<<40,()=>PawnsFinder.AllMaps_FreeColonists.Count(IsKobold));
  public static int KoboldServantsOn(Map map)=>map==null?0:Cached(5L<<40|(uint)map.uniqueID,()=>map.mapPawns.FreeColonistsSpawned.Count(p=>Has(p,"LP_KoboldDragonServant")));
  public static bool PlayerHasDragon()=>Cached(6L<<40,()=>{foreach(var m in Find.Maps)foreach(var o in m.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))if(!o.Dead && Dragons.Is(o))return 1;return 0;})>0;

  public static bool InSunlight(Pawn p)=>p.Spawned && p.Map.skyManager.CurSkyGlow>=SunlightGlow && !p.Position.Roofed(p.Map);

  // ---------- 命中修正 ----------
  // 攻击者一侧（近战与远程都算）：卓尔阳光敏感、魔法黑暗、矮人血仇、狗头人群体战术（近战）、大地精群体之助。
  public static float HitFactor(Pawn caster,Thing target,bool melee){
   if(caster==null)return 1f;float f=1f;var tp=target as Pawn;
   if(caster.genes!=null){
    if(CommonRaces.Has(caster,gDrowSun) && InSunlight(caster))f*=SunlightFactor;
    if(tp!=null && CommonRaces.Has(caster,gGrudge) && Grudges.Against(tp.Faction))f*=GrudgeFactor;
    if(melee && target!=null && CommonRaces.Has(caster,gPack))f*=PackFactor(caster,target);
    if(CommonRaces.Has(caster,gHob))f*=HobFactor(caster);
   }
   var dark=hDark.Def;if(dark!=null && caster.health?.hediffSet?.HasHediff(dark)==true)f*=DarknessFactor;
   return f;
  }
  // 1.58.6：命中报告每发子弹都要查这几个，用静态 DefRef，不再按名字查。
  static readonly DefRef<GeneDef> gDrowSun=new DefRef<GeneDef>("LP_DrowSunlight"),gGrudge=new DefRef<GeneDef>("LP_DwarfGrudge"),gPack=new DefRef<GeneDef>("LP_KoboldPackTactics"),gHob=new DefRef<GeneDef>("LP_HobgoblinNumbers"),gHide=new DefRef<GeneDef>("LP_HalflingHideBehind");
  static readonly DefRef<HediffDef> hDark=new DefRef<HediffDef>("LP_MagicalDarkness"),hCamo=new DefRef<HediffDef>("LP_StoneCamouflage");
  // 目标一侧（只算远程）：躲到大个子身后、石肤伪装。
  public static float TargetFactor(Pawn target){
   if(target?.genes==null)return 1f;float f=1f;
   if(CommonRaces.Has(target,gHide) && BigFriendNear(target))f*=HideFactor;
   var camo=hCamo.Def;if(camo!=null && target.health.hediffSet.HasHediff(camo))f*=CamouflageFactor;
   return f;
  }
  // 近战/远程伤害：熊地精突袭（只近战）、地精小个子的怒火。
  public static float DamageFactor(Pawn caster,Thing target,bool melee){
   if(caster?.genes==null || !(target is Pawn tp))return 1f;float f=1f;
   if(Has(caster,"LP_GoblinFury") && tp.BodySize>caster.BodySize+.001f)f*=FuryFactor;
   if(melee && Has(caster,"LP_BugbearSurprise") && Unaware(tp))f*=SurpriseFactor;
   return f;
  }
  public static bool Unaware(Pawn t){var m=t.mindState;if(m==null)return false;int now=Now;return now-m.lastAttackTargetTick>SurpriseTicks && now-m.lastHarmTick>SurpriseTicks;}
  // 矮人坚韧：部位最大生命 ×1.15（与巫妖强化、巨躯规则、冒险者体质相乘）。
  public static float PartHealthFactor(Pawn p)=>Has(p,DwarfToughness)?ToughnessFactor:1f;
  // 森林侏儒驯服小动物 ×2。
  public static float TameFactor(Pawn tamer,Pawn animal)=>animal!=null && animal.BodySize<=SmallBeastSize+.0001f && Has(tamer,"LP_GnomeSmallBeasts")?SmallBeastFactor:1f;
  // 龙之仆从：同一地图每个狗头人殖民者让宝库价值 +2%，最多 +20%（设置关掉时 ×1）。
  public static float HoardFactor(Map map)=>LichMod.Settings?.koboldDragonServant==false?1f:1f+Mathf.Min(HoardMax,HoardStep*KoboldServantsOn(map));

  // ---------- 恐惧 ----------
  static readonly Dictionary<ThoughtDef,bool> fearThought=new Dictionary<ThoughtDef,bool>();
  public static bool FearThought(ThoughtDef d){
   if(d==null)return false;if(fearThought.TryGetValue(d,out bool b))return b;
   string n=d.defName;b=Loxodon.FearThoughts.Contains(n) || new[]{"Fear","Terror","Dread","Fright","Panic","Scared","Horror"}.Any(k=>n.IndexOf(k,StringComparison.OrdinalIgnoreCase)>=0);
   return fearThought[d]=b;
  }
  public static Pawn calmTaming; // 森林侏儒驯服小动物期间，失败也不反扑

  // ---------- 黑暗术 ----------
  public static CommonRaceState State=>GameComp<CommonRaceState>.Get();
  public static int CastDarkness(Pawn caster,IntVec3 cell,float radius,int duration){
   var map=caster?.Map;if(map==null || State==null)return 0;
   var z=new DarknessZone{map=map,cell=cell,radius=radius,end=Now+duration,faction=caster.Faction};State.zones.Add(z);
   for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(cell.ToVector3Shifted()+new Vector3(Rand.Range(-radius,radius),0,Rand.Range(-radius,radius))*.7f,map,Rand.Range(1.5f,2.5f));
   return z.Apply();
  }
  public static bool DarknessTarget(Pawn o,Faction f)=>!o.Dead && !IsDrow(o) && (f==null?o.Faction!=null:o.HostileTo(f));
  public static void GiveDarkness(Pawn o){
   var def=H("LP_MagicalDarkness");if(def==null)return;
   var h=o.health.hediffSet.GetFirstHediffOfDef(def)??o.health.AddHediff(def);
   var c=h.TryGetComp<HediffComp_Disappears>();if(c!=null)c.ticksToDisappear=DarknessRefresh;
  }

  // ---------- 生成：外貌、半精灵天赋 ----------
  public static readonly string[] TalentSkills={"Shooting","Melee","Construction","Mining","Cooking","Plants","Animals","Crafting","Artistic","Medicine","Social","Intellectual"};
  public static void OnGenerated(Pawn p){
   var x=p?.genes?.Xenotype;if(x==null)return;
   var look=x.GetModExtension<RaceLookExtension>();if(look!=null)ApplyLook(p,look);
   if(x.defName=="LP_HalfElf")AddTalents(p);
  }
  // 原版生成时已经随机给了肤色（黑色素）和发色基因：不在候选表里的换成候选表里的一个（候选表里已有就不动）。
  public static void ApplyLook(Pawn p,RaceLookExtension look){
   bool changed=false;var x=p.genes.Xenotype;
   bool Skin(GeneDef d)=>d.skinColorOverride.HasValue || d.skinColorBase.HasValue;
   bool Hair(GeneDef d)=>d.hairColorOverride.HasValue;
   void Swap(List<GeneDef> pool,Func<GeneDef,bool> kind){
    if(pool.NullOrEmpty() || p.genes.Endogenes.Any(g=>pool.Contains(g.def)))return;
    foreach(var g in p.genes.Endogenes.Where(g=>kind(g.def) && !x.genes.Contains(g.def)).ToList())p.genes.RemoveGene(g);
    p.genes.AddGene(pool.RandomElement(),false);changed=true;
   }
   Swap(look.skinGenes,Skin);Swap(look.hairGenes,Hair);
   if(changed)p.Drawer?.renderer?.SetAllGraphicsDirty();
  }
  public static string TalentSkill(Pawn p,string group){var g=p?.genes?.GenesListForReading.FirstOrDefault(x=>x.def.defName.StartsWith("LP_HalfElfTalent"+group+"_"));return g?.def.defName.Substring(("LP_HalfElfTalent"+group+"_").Length);}
  public static bool AddTalents(Pawn p){
   if(TalentSkill(p,"A")!=null || TalentSkill(p,"B")!=null)return false;
   var pool=TalentSkills.Where(s=>G("LP_HalfElfTalentA_"+s)!=null).ToList();if(pool.Count<2)return false;
   string a=pool.RandomElement(),b=pool.Where(s=>s!=a).RandomElement();
   p.genes.AddGene(G("LP_HalfElfTalentA_"+a),false);p.genes.AddGene(G("LP_HalfElfTalentB_"+b),false);return true;
  }

  // ---------- 设置 ----------
  public static readonly string[] Relations157={"LP_Relation_ElfDrow","LP_Relation_DrowElf","LP_Relation_GnomeDwarf","LP_Relation_DwarfGnome","LP_Relation_GnomeMindFlayer","LP_Relation_KoboldGnome","LP_Relation_GnomeKobold","LP_Relation_KoboldLich","LP_Relation_GoblinoidDwarf","LP_Relation_DwarfGoblinoid","LP_Relation_HobgoblinElf","LP_Relation_ElfHobgoblin","LP_Relation_GoblinoidHalfOrc","LP_Relation_HalfOrcGoblinoid","LP_Relation_HalflingDwarf","LP_Relation_DwarfHalfling"};
  public static void ApplySettings(){bool on=LichMod.Settings?.commonRaceRelations!=false;foreach(var n in Relations157){var r=DefDatabase<RacialRelationDef>.GetNamedSilentFail(n);if(r!=null)r.enabled=on;}}
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;bool before=s.commonRaceRelations;
   l.CheckboxLabeled("普通种族关系心情（1.57.0）",ref s.commonRaceRelations,"精灵与卓尔、侏儒与矮人、狗头人与侏儒、地精族与矮人等新增的种族关系心情（只计本殖民地成员与囚犯；有半精灵时精灵与矮人之间的负面心情减半）。关闭后这些新关系不产生心情，1.16.0 已有的精灵/矮人关系不受影响。");
   l.CheckboxLabeled("狗头人：龙之仆从",ref s.koboldDragonServant,"狗头人在本殖民地有力能龙或五色真龙时心情+6、没有龙时-2，并让同一地图的力能龙宝库价值每个狗头人+2%（最多+20%）。关闭后没有这项心情和宝库加成。");
   if(before!=s.commonRaceRelations)ApplySettings();
  }
 }

 // 外貌：异种型上的发色/肤色候选，生成时随机挑一个加为内源基因（已有对应颜色基因时不加）。
 public class RaceLookExtension : DefModExtension {
  public List<GeneDef> skinGenes=new List<GeneDef>(),hairGenes=new List<GeneDef>();
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   if(skinGenes.Any(g=>g==null || !g.skinColorOverride.HasValue && !g.skinColorBase.HasValue))yield return "RaceLookExtension.skinGenes must be skin color genes";
   if(hairGenes.Any(g=>g==null || !g.hairColorOverride.HasValue))yield return "RaceLookExtension.hairGenes must be hair color genes";
  }
 }
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class SubraceGenerate {
  static void Postfix(Pawn __result){if(__result?.genes?.Xenotype!=null && __result.RaceProps?.Humanlike==true && !__result.Dead)Subraces.OnGenerated(__result);}
 }

 [StaticConstructorOnStartup]
 public static class SubraceStartup {
  static SubraceStartup(){
   void Add(StatDef stat,StatPart part){if(stat==null)return;if(stat.parts==null)stat.parts=new List<StatPart>();if(stat.parts.Any(x=>x.GetType()==part.GetType()))return;part.parentStat=stat;stat.parts.Add(part);stat.immutable=false;}
   StatDef S(string n)=>DefDatabase<StatDef>.GetNamedSilentFail(n);
   Add(StatDefOf.MoveSpeed,new StatPart_DwarfArmorTraining());
   Add(S("WorkSpeedGlobal"),new StatPart_HobgoblinNumbers());
   Add(S("ConstructionSpeed"),new StatPart_KoboldTrapBuilding());
   Add(S("TrapMeleeDamage"),new StatPart_KoboldTrap());
   Add(S("GeneralLaborSpeed"),new StatPart_TinkerToy());
   Subraces.ApplySettings();
  }
 }

 // ---------- 存档状态：血仇、狗头人陷阱、黑暗区 ----------
 public class DarknessZone {
  public Map map;public IntVec3 cell;public float radius;public int end;public Faction faction;
  public int Apply(){int n=0;foreach(var c in GenRadial.RadialCellsAround(cell,radius,true)){if(!c.InBounds(map))continue;foreach(var t in map.thingGrid.ThingsListAtFast(c).ToList())if(t is Pawn o && Subraces.DarknessTarget(o,faction)){Subraces.GiveDarkness(o);n++;}}return n;}
 }
 public class CommonRaceState : GameComponent {
  public Dictionary<Faction,int> grudges=new Dictionary<Faction,int>();
  public HashSet<int> koboldTraps=new HashSet<int>();
  public List<DarknessZone> zones=new List<DarknessZone>();
  List<Faction> gKeys;List<int> gValues;
  public CommonRaceState(Game game){}
  public override void GameComponentTick(){
   if(zones.Count==0)return;int now=Find.TickManager.TicksGame;if(now%30!=11)return;
   for(int i=zones.Count-1;i>=0;i--){var z=zones[i];if(z.map==null || !Find.Maps.Contains(z.map) || now>=z.end){zones.RemoveAt(i);continue;}z.Apply();if(now%90==11)FleckMaker.ThrowSmoke(z.cell.ToVector3Shifted()+new Vector3(Rand.Range(-z.radius,z.radius),0,Rand.Range(-z.radius,z.radius))*.6f,z.map,Rand.Range(1.5f,2.2f));}
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref grudges,"lpDwarfGrudges",LookMode.Reference,LookMode.Value,ref gKeys,ref gValues);
   Scribe_Collections.Look(ref koboldTraps,"lpKoboldTraps",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    if(grudges==null)grudges=new Dictionary<Faction,int>();grudges.RemoveAll(kv=>kv.Key==null);
    if(koboldTraps==null)koboldTraps=new HashSet<int>();if(zones==null)zones=new List<DarknessZone>();
   }
  }
 }
 public static class Grudges {
  public static bool Against(Faction f){var s=Subraces.State;return f!=null && s!=null && s.grudges.TryGetValue(f,out int end) && end>Subraces.Now;}
  public static int End(Faction f){var s=Subraces.State;return f!=null && s!=null && s.grudges.TryGetValue(f,out int end)?end:-1;}
  static ThoughtDef def;
  public static ThoughtDef Def=>def??(def=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_DwarfBloodGrudge"));
  // 本殖民地人形被 f 的成员杀死：记血仇（刷新到期时间），所有带矮人记仇基因的自由殖民者得到/刷新"血仇：派系"。
  public static int Register(Faction f){
   var s=Subraces.State;if(f==null || f==Faction.OfPlayer || s==null || Def==null)return 0;
   s.grudges[f]=Subraces.Now+Subraces.GrudgeTicks;int n=0;
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.ToList()){
    if(!Subraces.Has(p,"LP_DwarfGrudge"))continue;var mem=p.needs?.mood?.thoughts?.memories;if(mem==null)continue;
    var old=mem.Memories.OfType<Thought_DwarfGrudge>().FirstOrDefault(m=>m.faction==f);
    if(old!=null)old.Renew();else{var t=(Thought_DwarfGrudge)ThoughtMaker.MakeThought(Def);t.faction=f;mem.TryGainMemory(t);}
    n++;
   }
   return n;
  }
 }
 public class Thought_DwarfGrudge : Thought_Memory {
  public Faction faction;
  public override string LabelCap=>"血仇："+(faction?.Name??"未知派系");
  public override bool GroupsWith(Thought other)=>other is Thought_DwarfGrudge g && g.faction==faction && base.GroupsWith(other);
  public override bool ShouldDiscard=>base.ShouldDiscard || faction==null;
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref faction,"grudgeFaction");}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class DwarfGrudgeOnKill {
  static void Postfix(Pawn __instance,DamageInfo? dinfo){
   if(!__instance.Dead || __instance.RaceProps?.Humanlike!=true || __instance.Faction!=Faction.OfPlayer)return;
   if(dinfo?.Instigator is Pawn killer && killer.Faction!=null && killer.Faction!=Faction.OfPlayer)Grudges.Register(killer.Faction);
  }
 }

 // ---------- 半身人：好胃口（随基因同步的 hediff） ----------
 public class Gene_HalflingBlood : Gene {
  int accumulated;
  public override void TickInterval(int delta){base.TickInterval(delta);if((accumulated+=delta)<Subraces.CacheTicks)return;accumulated=0;Sync();}
  public void Sync(){
   var def=Subraces.H("LP_HalflingAppetite");if(def==null || pawn?.health==null)return;
   bool want=Active && !pawn.Dead;var h=pawn.health.hediffSet.GetFirstHediffOfDef(def);
   if(want && h==null)pawn.health.AddHediff(def);else if(!want && h!=null)pawn.health.RemoveHediff(h);
  }
  public override void PostRemove(){base.PostRemove();var def=Subraces.H("LP_HalflingAppetite");var h=def==null?null:pawn.health.hediffSet.GetFirstHediffOfDef(def);if(h!=null)pawn.health.RemoveHediff(h);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref accumulated,"halflingTicks");}
 }
 public class Hediff_HalflingAppetite : HediffWithComps {
  public override bool ShouldRemove=>base.ShouldRemove || pawn.genes?.GetFirstGeneOfType<Gene_HalflingBlood>()==null;
 }

 // ---------- 地侏：石肤伪装 ----------
 public class Gene_DeepGnomeCamouflage : Gene {
  public IntVec3 lastPos=IntVec3.Invalid;public int stillSince;int accumulated;
  public override void TickInterval(int delta){base.TickInterval(delta);if((accumulated+=delta)<60)return;accumulated=0;Process();}
  public static bool Busy(Pawn p)=>p.pather?.Moving==true || p.stances?.curStance is Stance_Busy;
  public void Process(){
   int now=Subraces.Now;
   if(!pawn.Spawned || pawn.Dead || !Active){Disturb();return;}
   if(pawn.Position!=lastPos || Busy(pawn)){lastPos=pawn.Position;Disturb();return;}
   var def=Subraces.H("LP_StoneCamouflage");
   if(def!=null && now-stillSince>=Subraces.StillTicks && !pawn.health.hediffSet.HasHediff(def))pawn.health.AddHediff(def);
  }
  public void Disturb(){stillSince=Subraces.Now;var def=Subraces.H("LP_StoneCamouflage");var h=def==null?null:pawn?.health?.hediffSet?.GetFirstHediffOfDef(def);if(h!=null)pawn.health.RemoveHediff(h);}
  public override void PostRemove(){base.PostRemove();Disturb();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref lastPos,"camoPos",IntVec3.Invalid);Scribe_Values.Look(ref stillSince,"camoStill");Scribe_Values.Look(ref accumulated,"camoTicks");}
 }
 public class HediffCompProperties_StoneCamouflage : HediffCompProperties {public HediffCompProperties_StoneCamouflage(){compClass=typeof(HediffComp_StoneCamouflage);}}
 public class HediffComp_StoneCamouflage : HediffComp {
  int accumulated;
  // 250 刻检查一次：在走路、出手，或基因没了就移除。
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){
   if((accumulated+=delta)<Subraces.CacheTicks)return;accumulated=0;
   var gene=Pawn.genes?.GetFirstGeneOfType<Gene_DeepGnomeCamouflage>();
   if(gene==null || !gene.Active || Gene_DeepGnomeCamouflage.Busy(Pawn) || Pawn.Position!=gene.lastPos)Pawn.health.RemoveHediff(parent);
  }
 }
 // 出手（开始瞄准或近战后硬直）就失去伪装。
 [HarmonyPatch(typeof(Pawn_StanceTracker),nameof(Pawn_StanceTracker.SetStance))]
 public static class StoneCamouflageAttack {
  static void Prefix(Pawn_StanceTracker __instance,Stance newStance){
   if(!(newStance is Stance_Warmup) && !(newStance is Stance_Cooldown))return;
   __instance.pawn?.genes?.GetFirstGeneOfType<Gene_DeepGnomeCamouflage>()?.Disturb();
  }
 }

 // ---------- 狗头人：龙之仆从、刺坑放置 ----------
 public class ThoughtWorker_KoboldDragonServant : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(LichMod.Settings?.koboldDragonServant==false || p.Faction!=Faction.OfPlayer || !Subraces.Has(p,"LP_KoboldDragonServant"))return ThoughtState.Inactive;
   return ThoughtState.ActiveAtStage(Subraces.PlayerHasDragon()?1:0);
  }
 }
 public class PlaceWorker_KoboldOnly : PlaceWorker {
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef,IntVec3 loc,Rot4 rot,Map map,Thing thingToIgnore=null,Thing thing=null){
   return Subraces.KoboldColonists()>0?AcceptanceReport.WasAccepted:new AcceptanceReport("需要殖民地里至少有一名狗头人，才能布置狗头人刺坑。");
  }
 }

 // ---------- 设置字段 ----------
 public partial class Settings {
  public bool commonRaceRelations=true,koboldDragonServant=true;
  void ExposeCommonRaces(){Scribe_Values.Look(ref commonRaceRelations,"commonRaceRelations",true);Scribe_Values.Look(ref koboldDragonServant,"koboldDragonServant",true);}
 }
}
