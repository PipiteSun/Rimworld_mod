using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.52.0 五色真龙（见 ArtSource/ChromaticDragons-1.52.0-spec.md 第三节）。种族、身体、贴图、兵种都在 Bridges/HAR（只在启用 HAR 时加载）；
 // 这里是不需要 HAR 类型的规则：吐息、魅惑、飞跃、撤退、勒索 / 来袭 / 龙巢远征 / 龙过境联动、巢穴动作、战利品、龙鳞与龙之血的元素抗性、设置。
 // 没装 HAR 时种族不存在（Dragons.Available = false）：事件不触发、设置灰显，所有查询用 GetNamedSilentFail。
 public class LP_DragonExtension : DefModExtension {public string color;}

 public class DragonColorInfo {
  public string key,label,breathLabel,damage,biomeWords,guardKind,lairRock,lairFloor;public bool line,extorts=true;public int save;public IntRange guards;public bool guardsMad;
 }

 public static class Dragons {
  public static readonly string[] Colors={"Red","Blue","Green","Black","White"};
  public static readonly string[] StageKey={"Young","Adult","Ancient"},StageLabel={"青年","成年","远古"};
  public static readonly IntRange[] AgeRange={new IntRange(30,99),new IntRange(100,799),new IntRange(800,1500)};
  public static readonly int[] Hits={3,4,5};public static readonly float[] HitDamage={9f,12f,18f},ConeRange={6f,8f,11f},LineRange={12f,16f,22f},WingDamage={0f,15f,25f};
  public static readonly float[] RenderScale={2.6f,3.8f,5.2f}; // 1.63.0：画得更大（原 2.0 / 2.8 / 3.8），只影响外观
  public const float ConeHalfAngle=28f,ChargeChance=1f/3f,CharmRange=15f,FlyRange=12f,FleeHealth=.35f,ApparelLoss=.3f,ToxPerHit=.04f;
  public const int ChargeTicks=360,CharmTicks=1800,CharmDuration=1800,FlyTicks=900,BurrowStun=120;
  public static readonly Dictionary<string,DragonColorInfo> Info=new Dictionary<string,DragonColorInfo>{
   {"Red",new DragonColorInfo{key="Red",label="红龙",breathLabel="火焰吐息",damage="Flame",line=false,save=Adventurer.Dex,biomeWords="mountain|desert|volcan|hot|arid|scrub",guardKind="LP_EC_FireGiant",guards=new IntRange(2,4),lairRock="Granite",lairFloor="FlagstoneGranite"}},
   {"Blue",new DragonColorInfo{key="Blue",label="蓝龙",breathLabel="闪电吐息",damage="LP_Shock",line=true,save=Adventurer.Dex,biomeWords="desert|arid|scrub|dune",guardKind="LP_DragonCultist",guards=new IntRange(2,5),lairRock="Sandstone",lairFloor="FlagstoneSandstone"}},
   {"Green",new DragonColorInfo{key="Green",label="绿龙",breathLabel="毒气吐息",damage="LP_DragonPoison",line=false,save=Adventurer.Con,biomeWords="forest|jungle|grove",guardKind="LP_Worg",guards=new IntRange(3,5),guardsMad=true,lairRock="Limestone",lairFloor="FlagstoneLimestone"}},
   {"Black",new DragonColorInfo{key="Black",label="黑龙",breathLabel="强酸吐息",damage="LP_AlchemyAcid",line=true,save=Adventurer.Dex,biomeWords="swamp|marsh|bog|wetland|jungle",guardKind="LP_DragonCultist",guards=new IntRange(2,4),lairRock="Slate",lairFloor="FlagstoneSlate"}},
   {"White",new DragonColorInfo{key="White",label="白龙",breathLabel="寒冰吐息",damage="LP_Frost",line=false,save=Adventurer.Con,biomeWords="tundra|ice|glacier|boreal|cold",guardKind="LP_WinterWolf",guards=new IntRange(4,6),guardsMad=true,extorts=false,lairRock="Marble",lairFloor="FlagstoneMarble"}},
  };
  public static ThingDef Race(string c)=>DefDatabase<ThingDef>.GetNamedSilentFail("LP_"+c+"Dragon");
  public static bool Available=>Race("Red")!=null;
  public static PawnKindDef Kind(string c,int stage)=>DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_"+c+"Dragon_"+StageKey[Mathf.Clamp(stage,0,2)]);
  // 1.58.6：ColorOf 也从渲染缩放后缀里调用（可能在并行的预绘制线程上），改成线程安全的并发字典（原来是普通 Dictionary，并发写入会损坏）。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<ThingDef,string> colorCache=new System.Collections.Concurrent.ConcurrentDictionary<ThingDef,string>();
  public static string ColorOf(Pawn p){var d=p?.def;if(d==null)return null;return colorCache.GetOrAdd(d,x=>x.GetModExtension<LP_DragonExtension>()?.color);}
  public static bool IsChromatic(Pawn p)=>ColorOf(p)!=null;
  // 龙杀手等“龙”的统一判定：力能龙裔、五色龙，以及名字里带 Dragon 的非人形生物（Isekai 生物扩展的真龙）。人形兵种（龙教徒）不算。
  public static bool Is(Pawn p)=>p!=null && (IsChromatic(p) || ForceDragon.Active(p) || p.RaceProps!=null && !p.RaceProps.Humanlike && p.def.defName.IndexOf("Dragon",StringComparison.OrdinalIgnoreCase)>=0);
  public static int Stage(Pawn p){var ls=p?.ageTracker?.CurLifeStage?.defName;return ls=="LP_DragonAncient"?2:ls=="LP_DragonAdult"?1:ls=="LP_DragonYoung"?0:-1;}
  public static int StageOrYoung(Pawn p)=>Mathf.Max(0,Stage(p));
  public static string Title(Pawn p){var c=ColorOf(p);return c==null?null:StageLabel[StageOrYoung(p)]+Info[c].label;}
  public static string TitleFor(string c,int stage)=>StageLabel[Mathf.Clamp(stage,0,2)]+Info[c].label;

  // ---------- 强度 ----------
  public static float Strength=>(LichMod.Settings?.dragonStrength??100)/100f;
  public static float HealthMult=>Strength*(Adventurer.IsekaiActive?(LichMod.Settings?.dragonIsekaiHealth??200)/100f:1f)*ScoreHealth;
  // 1.65.0：六维改成 3.5 负重表倍率后（力量 20 近战 ×4），真龙与传奇怪生命 ×3（按“属性效果强度”插值；交给 Isekai 时为 ×1，由上面的 Isekai 倍率接手）。
  public const float ScoreHealthFull=3f;
  public static float ScoreHealth=>1f+(ScoreHealthFull-1f)*Adventurer.EffectScale;
  public static float DamageMult=>Strength*(Adventurer.IsekaiActive?(LichMod.Settings?.dragonIsekaiDamage??150)/100f:1f);

  // ---------- 开关 ----------
  public static bool On=>Available && (LichMod.Settings?.chromaticDragons??true);
  public static bool ExtortionOn=>On && (LichMod.Settings?.dragonExtortion??true);
  public static bool RaidOn=>On && (LichMod.Settings?.dragonRaid??true);
  public static bool LairOn=>On && (LichMod.Settings?.dragonLair??true);
  public static bool FlyoverLinkOn=>On && (LichMod.Settings?.dragonFlyoverLink??true);
  public static int AncientLevel=>LichMod.Settings?.dragonAncientLevel??28;
  public static float? TestRoll;public static float Roll=>TestRoll??Rand.Value;

  // ---------- 派系 ----------
  static FactionDef factionDef;public static FactionDef FactionDef=>factionDef??(factionDef=DefDatabase<FactionDef>.GetNamedSilentFail("LP_ChromaticDragons"));
  public static Faction Faction(){var f=Factions.EnsureHidden(FactionDef);if(f!=null && !f.HostileTo(RimWorld.Faction.OfPlayer))Factions.SetRelation(f,RimWorld.Faction.OfPlayer,-100);return f;}

  // ---------- 生成 ----------
  static readonly string[] Names={"阿达亚克斯","克拉拉吉斯","奥默拉斯","瓦拉斯卡","伊莉安德拉","萨卡拉斯","塔罗斯赫","韦尔米索","乌姆布拉","科尔加斯","梅格拉齐","斯卡尔维克","格丽姆塔","阿尔萨洛斯","黑焰","霜牙","苍雷","毒棘","伊瑞维斯","普尔普拉"};
  public static Pawn Make(string color,int stage,Faction f=null,int tile=-1){
   var kind=Kind(color,stage);if(kind==null)return null;f=f??Faction();
   var age=AgeRange[Mathf.Clamp(stage,0,2)].RandomInRange;
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,f,PawnGenerationContext.NonPlayer,tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:age,fixedChronologicalAge:age,forceNoBackstory:false,allowAddictions:false));
   Normalize(p);
   return p;
  }
  // 外观与身份：龙头、龙身、光头无须、无纹身、无特性、肤色不染（贴图本色）、龙名；衣服武器全清。
  public static void Normalize(Pawn p){
   var c=ColorOf(p);if(c==null)return;
   if(p.story!=null){
    var head=DefDatabase<HeadTypeDef>.GetNamedSilentFail("LP_DragonHead_"+c);if(head!=null)p.story.headType=head;
    p.story.bodyType=BodyTypeDefOf.Hulk;p.story.hairDef=HairDefOf.Bald;p.story.skinColorOverride=Color.white;
    if(p.style!=null){p.style.beardDef=BeardDefOf.NoBeard;p.style.FaceTattoo=TattooDefOf.NoTattoo_Face;p.style.BodyTattoo=TattooDefOf.NoTattoo_Body;}
    if(p.story.traits!=null)foreach(var t in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(t);
   }
   if(!(p.Name is NameSingle))p.Name=new NameSingle(Names.RandomElement());
   // 生成时的旧伤、缺失部位（原版随机给的）全部去掉：真龙登场时是完整的。
   if(p.health!=null){foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_AddedPart || h.def.isBad && !(h is Hediff_MissingPart)).ToList())p.health.RemoveHediff(h);foreach(var m in p.health.hediffSet.GetMissingPartsCommonAncestors().ToList())p.health.RestorePart(m.Part);}
   p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();p.inventory?.DestroyAll();
   if(p.skills!=null){int lv=new[]{12,16,20}[StageOrYoung(p)];foreach(var s in new[]{SkillDefOf.Melee,SkillDefOf.Shooting})p.skills.GetSkill(s).Level=Mathf.Max(p.skills.GetSkill(s).Level,lv);}
   if(!Items.Has(p,"LP_DragonBody_"+c)){Legendary.bypass=true;try{Items.Give(p,"LP_DragonBody_"+c);}finally{Legendary.bypass=false;}}
   p.Drawer?.renderer?.SetAllGraphicsDirty();
  }

  // ---------- 吐息 ----------
  public static List<IntVec3> BreathCells(Pawn d,IntVec3 target){
   var map=d.Map;var info=Info[ColorOf(d)];int st=StageOrYoung(d);var o=d.Position;
   return ShapeCells(map,o,target,info.line?LineRange[st]:ConeRange[st],info.line,ConeHalfAngle,d.Rotation);
  }
  // 1.52.1：通用的锥 / 线形状（传奇怪的闪电吐息、眼魔反魔法锥也用）。
  public static List<IntVec3> ShapeCells(Map map,IntVec3 o,IntVec3 target,float range,bool line,float halfAngle,Rot4 facing,bool needSight=true){
   var dir=(target-o).ToVector3();dir.y=0f;if(dir.sqrMagnitude<.01f)dir=facing.FacingCell.ToVector3();dir.Normalize();
   var list=new List<IntVec3>();
   foreach(var c in GenRadial.RadialCellsAround(o,range,false)){
    if(!c.InBounds(map))continue;var v=(c-o).ToVector3();float along=Vector3.Dot(v,dir);if(along<.5f)continue;
    if(line){if((v-dir*along).magnitude>.8f)continue;}else if(Vector3.Angle(v,dir)>halfAngle)continue;
    if(needSight && !GenSight.LineOfSight(o,c,map,true))continue;
    list.Add(c);
   }
   return list;
  }
  // 1.52.1：通用的范围伤害（豁免成功减半；同派系不伤；建筑受总伤害）。吐息、贝希尔闪电、地狱火球都走这里。
  public static BreathResult HitArea(Pawn source,IEnumerable<IntVec3> cells,DamageDef def,int hits,float per,int save,int dc,Func<Pawn,float> mult=null,Action<Pawn,bool> after=null,float buildingFactor=1f){
   var r=new BreathResult{cells=cells.ToList()};var map=source.Map;if(map==null)return r;
   var pawns=new HashSet<Pawn>();var builds=new HashSet<Thing>();
   foreach(var cell in r.cells)foreach(var t in cell.GetThingList(map).ToList()){
    if(t==source)continue;
    if(t is Pawn p){if(!p.Dead && (p.Faction==null || p.Faction!=source.Faction))pawns.Add(p);}
    else if(t.def.category==ThingCategory.Building && t.def.useHitPoints)builds.Add(t);
   }
   foreach(var p in pawns){
    bool saved=Legendary.Save(p,save,dc,source);if(saved)r.saved++;
    float amt=(saved?per*.5f:per)*(mult?.Invoke(p)??1f);r.lastDamage=amt;r.dealt[p]=amt;
    for(int i=0;i<hits && !p.Dead;i++)p.TakeDamage(new DamageInfo(def,amt,.25f,(p.Position-source.Position).AngleFlat,source));
    r.pawns++;if(!p.Dead)after?.Invoke(p,saved);
   }
   foreach(var b in builds){if(b.Destroyed)continue;b.TakeDamage(new DamageInfo(def,per*hits*buildingFactor,.25f,-1f,source));r.buildings++;}
   return r;
  }
  public class BreathResult {public int pawns,saved,buildings;public float lastDamage;public List<IntVec3> cells;public Dictionary<Pawn,float> dealt=new Dictionary<Pawn,float>();}
  public static int Breaths;
  public static BreathResult Breathe(Pawn d,IntVec3 target){
   var r=new BreathResult();var c=ColorOf(d);if(c==null || d.Map==null)return r;
   var info=Info[c];var map=d.Map;int st=StageOrYoung(d);
   var def=DefDatabase<DamageDef>.GetNamedSilentFail(info.damage)??DamageDefOf.Flame;
   int dc=Legendary.DC(d,Adventurer.Con),hits=Hits[st];float per=HitDamage[st]*DamageMult;
   var cells=r.cells=BreathCells(d,target);
   var pawns=new HashSet<Pawn>();var builds=new HashSet<Thing>();
   foreach(var cell in cells){
    foreach(var t in cell.GetThingList(map).ToList()){
     if(t==d)continue;
     if(t is Pawn p){if(!p.Dead && (p.Faction==null || p.Faction!=d.Faction))pawns.Add(p);}
     else if(t.def.category==ThingCategory.Building && t.def.useHitPoints)builds.Add(t);
    }
    Effect(c,cell,map);
   }
   foreach(var p in pawns){
    bool saved=Legendary.Save(p,info.save,dc,d);if(saved)r.saved++;
    float amt=(saved?per*.5f:per)*(c=="Blue" && p.RaceProps.IsMechanoid?2f:1f);r.lastDamage=amt;r.dealt[p]=amt;
    if(c=="Blue")BreakShields(p);
    for(int i=0;i<hits && !p.Dead;i++)p.TakeDamage(new DamageInfo(def,amt,.25f,(p.Position-d.Position).AngleFlat,d));
    r.pawns++;
    if(p.Dead)continue;
    if(c=="White")Fiends.Chill(p,1);
    if(c=="Black" && p.apparel!=null)foreach(var a in p.apparel.WornApparel.ToList()){int loss=Mathf.CeilToInt(a.MaxHitPoints*ApparelLoss*(saved?.5f:1f));a.HitPoints=Mathf.Max(0,a.HitPoints-loss);if(a.HitPoints<=0)a.Destroy();}
    if(c=="Green"){float tox=ToxPerHit*hits*(saved?.5f:1f)*Mathf.Max(0f,1f-p.GetStatValue(StatDefOf.ToxicResistance));if(tox>0f)HealthUtility.AdjustSeverity(p,HediffDefOf.ToxicBuildup,tox);}
   }
   foreach(var b in builds){if(b.Destroyed)continue;float amt=per*hits*(c=="Blue" && b is Building_Turret?2f:1f);b.TakeDamage(new DamageInfo(def,amt,.25f,-1f,d));r.buildings++;}
   if(c=="Green" && ModsConfig.BiotechActive)foreach(var cell in cells.Where((x,i)=>i%3==0))GasUtility.AddGas(cell,map,GasType.ToxGas,60);
   Breaths++;
   Items.Mote(d,info.breathLabel+"！",new Color(1f,.55f,.3f));
   return r;
  }
  static void Effect(string c,IntVec3 cell,Map map){
   var v=cell.ToVector3Shifted();
   switch(c){
    case "Red":FleckMaker.ThrowFireGlow(v,map,1f);if(Rand.Chance(.25f))FireUtility.TryStartFireIn(cell,map,.4f,null);break;
    case "Blue":FleckMaker.ThrowMicroSparks(v,map);FleckMaker.ThrowLightningGlow(v,map,.8f);break;
    case "White":FleckMaker.ThrowAirPuffUp(v,map);break;
    default:FleckMaker.ThrowDustPuff(v,map,.9f);break;
   }
  }
  static readonly System.Reflection.MethodInfo shieldBreak=AccessTools.Method(typeof(CompShield),"Break");static readonly System.Reflection.FieldInfo shieldEnergy=AccessTools.Field(typeof(CompShield),"energy");
  public static int BreakShields(Pawn p){
   int n=0;if(p?.apparel==null)return 0;
   foreach(var a in p.apparel.WornApparel){var s=a.TryGetComp<CompShield>();if(s==null || s.Energy<=0f)continue;try{shieldEnergy?.SetValue(s,0f);shieldBreak?.Invoke(s,null);}catch{}n++;}
   return n;
  }
  public static Pawn BreathTarget(Pawn d){
   if(d?.Map==null)return null;var info=Info[ColorOf(d)];float range=info.line?LineRange[StageOrYoung(d)]:ConeRange[StageOrYoung(d)];
   var list=Legendary.EnemiesNear(d,range).Where(p=>!p.Downed && GenSight.LineOfSight(d.Position,p.Position,d.Map,true)).ToList();
   if(list.Count==0)return null;
   // 金属与色彩之争：成年力能龙裔优先。
   return list.OrderByDescending(p=>ForceDragon.Active(p) && ForceDragon.Age(p)>=3?1:0).ThenBy(p=>p.Position.DistanceTo(d.Position)).First();
  }

  // ---------- 绿龙魅惑 ----------
  public static int CharmDC(Pawn dragon,Pawn p){float sens=p.GetStatValue(StatDefOf.PsychicSensitivity);return Legendary.DC(dragon,Adventurer.Cha)-Mathf.RoundToInt(4f*(1f-Mathf.Min(1f,sens)));}
  public static bool Charm(Pawn dragon,Pawn p,MapComponent_Legendary mc=null){
   if(p==null || p.Dead || p.Downed || !p.RaceProps.Humanlike || p.GetStatValue(StatDefOf.PsychicSensitivity)<=0f)return false;
   if(mc!=null && mc.ImmuneTo(dragon,p))return false;
   if(Legendary.Save(p,Adventurer.Cha,CharmDC(dragon,p),dragon)){mc?.Immune(dragon,p,Legendary.FearImmuneTicks);return false;}
   var def=DefDatabase<MentalStateDef>.GetNamedSilentFail("LP_DragonCharmed");if(def==null)return false;
   Legendary.bypass=true;
   try{if(!p.mindState.mentalStateHandler.TryStartMentalState(def,"真龙魅惑",forced:true,forceWake:true))return false;}finally{Legendary.bypass=false;}
   if(p.MentalState!=null)p.MentalState.forceRecoverAfterTicks=CharmDuration;
   Items.Mote(p,"被魅惑了",new Color(.5f,.9f,.5f));return true;
  }
  public static Pawn CharmTarget(Pawn d)=>Legendary.EnemiesNear(d,CharmRange).Where(p=>p.Faction!=null && p.Faction.IsPlayer && p.RaceProps.Humanlike && !p.Downed && !p.InMentalState && GenSight.LineOfSight(d.Position,p.Position,d.Map,true)).OrderBy(p=>p.Position.DistanceTo(d.Position)).FirstOrDefault();

  // ---------- 豁免加值（火巨人 / 吉斯洋基对红龙、霜巨人对白龙 +2） ----------
  public static int SaveBonus(Pawn p,Pawn source,int a){
   var c=ColorOf(source);if(c==null || p==null)return 0;int b=0;
   if(c=="Red" && (GiantKin.Is(p,"fire") || p.genes?.Xenotype?.defName=="LP_Githyanki"))b+=2;
   if(c=="White" && GiantKin.Is(p,"frost"))b+=2;
   return b;
  }

  // ---------- 撤退：成年以上不在巢里、生命低于 35% 时离开地图（算“击退”） ----------
  public static bool ShouldFlee(Pawn d)=>Stage(d)>=1 && !Legendary.InOwnLair(d) && d.health.summaryHealth.SummaryHealthPercent<FleeHealth;
  public static void Flee(Pawn d){
   if(d?.Map==null || !d.Spawned)return;var map=d.Map;
   d.GetLord()?.RemovePawn(d);
   LordMaker.MakeNewLord(d.Faction,new LordJob_ExitMapBest(LocomotionUrgency.Sprint,true,true),map,new List<Pawn>{d});
   var st=State;var enc=st?.Encounter(d);if(enc!=null)enc.fled=true;
   Find.LetterStack.ReceiveLetter(Title(d)+"撤退了",d.LabelShort+"受了重伤，拍着翅膀撤退了。它不会忘记这里。",LetterDefOf.PositiveEvent,d);
  }

  // ---------- 存档组件 ----------
  public static GameComponent_Dragons State=>GameComp<GameComponent_Dragons>.Get();

  // ---------- 来袭条件 ----------
  public static int RaidStage(float points,int maxLevel,bool ancientUsed){
   if(maxLevel>=AncientLevel && points>=9000f && !ancientUsed)return 2;
   if(maxLevel>=16 && points>=4000f)return 1;
   if(maxLevel>=8 && points>=1500f)return 0;
   return -1;
  }
  public static bool AncientUsedThisYear(){var st=State;return st!=null && st.lastAncientRaid>=0 && GenTicks.TicksAbs-st.lastAncientRaid<GenDate.TicksPerYear;}
  public static bool HasGreatWyrm(Map map)=>map!=null && map.mapPawns.FreeColonistsSpawned.Any(p=>ForceDragon.Active(p) && ForceDragon.Age(p)>=5);
  // 颜色按生态区加权：偏好的生态区权重 5，其他 1（每色至少 10%）。
  public static string PickColor(Map map,bool extortion){
   var biome=(map?.Biome?.defName??"").ToLowerInvariant();
   var opts=Colors.Where(c=>!extortion || Info[c].extorts).Select(c=>(c,w:Info[c].biomeWords.Split('|').Any(x=>biome.Contains(x))?5f:1f)).ToList();
   return opts.RandomElementByWeight(o=>o.w).c;
  }

  // ---------- 护卫 ----------
  public static List<Pawn> Guards(string color,Faction f,int tile,float points,int mult=1){
   var info=Info[color];var list=new List<Pawn>();
   string kindName=info.guardKind;
   // 余烬氏族与玩家好感 ≥75 时，红龙的护卫改为龙教徒。
   if(color=="Red"){var ember=Factions.Of(DefDatabase<FactionDef>.GetNamedSilentFail("LP_EmberClan"));if(ember!=null && ember.GoodwillWith(RimWorld.Faction.OfPlayer)>=75)kindName="LP_DragonCultist";}
   var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName)??DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_DragonCultist");if(kind==null)return list;
   int n=Mathf.Clamp(Mathf.RoundToInt(info.guards.min+(info.guards.max-info.guards.min)*Mathf.Clamp01(points/6000f)),info.guards.min,info.guards.max)*mult;
   bool animal=kind.RaceProps.Animal;
   for(int i=0;i<n;i++){
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,animal?null:f,PawnGenerationContext.NonPlayer,tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:!animal));
    list.Add(p);
   }
   return list;
  }
  public static void MakeMad(IEnumerable<Pawn> pawns){foreach(var p in pawns)if(p.RaceProps.Animal && p.Spawned)p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,forced:true);}

  // ---------- 登场 ----------
  public static bool EntryCell(Map map,out IntVec3 cell)=>Events2.EdgeCell(map,out cell);
  public static List<Pawn> Arrive(Map map,Pawn dragon,List<Pawn> guards,IntVec3 entry,bool burrow=false){
   var all=new List<Pawn>{dragon};all.AddRange(guards);
   IntVec3 at=entry;
   // 蓝龙潜沙：沙漠 / 干旱地图从殖民地附近的地下钻出。
   if(burrow){var home=map.mapPawns.FreeColonistsSpawned.FirstOrDefault()?.Position??map.Center;if(CellFinder.TryFindRandomCellNear(home,map,18,c=>c.Standable(map) && !c.Fogged(map) && c.DistanceTo(home)>=10f,out var b))at=b;}
   GenSpawn.Spawn(dragon,at,map);
   foreach(var g in guards)GenSpawn.Spawn(g,CellFinder.RandomClosewalkCellNear(burrow?entry:at,map,5),map);
   if(burrow){int dc=Legendary.DC(dragon,Adventurer.Str);foreach(var p in Legendary.EnemiesNear(dragon,3.9f).ToList())if(!Legendary.Save(p,Adventurer.Dex,dc,dragon))p.stances?.stunner?.StunFor(BurrowStun,dragon,false,true,false);FleckMaker.Static(at,map,FleckDefOf.PsycastAreaEffect,5f);}
   return all;
  }
  public static bool Burrows(string color,Map map)=>color=="Blue" && Info["Blue"].biomeWords.Split('|').Any(x=>(map?.Biome?.defName??"").ToLowerInvariant().Contains(x));

  // 来袭：龙 + 本色护卫，一起进攻殖民地。
  public static DragonEncounter Raid(Map map,string color,int stage,float points){
   if(map==null || !Available)return null;var f=Faction();if(f==null)return null;
   if(!EntryCell(map,out var entry))return null;
   var dragon=Make(color,stage,f,map.Tile);if(dragon==null)return null;
   var guards=Guards(color,f,map.Tile,points);
   Arrive(map,dragon,guards,entry,Burrows(color,map));
   var lordPawns=new List<Pawn>{dragon};lordPawns.AddRange(guards.Where(g=>!g.RaceProps.Animal));
   var lord=LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false),map,lordPawns);
   if(Info[color].guardsMad)MakeMad(guards);
   if(stage==2 && State!=null)State.lastAncientRaid=GenTicks.TicksAbs;
   var enc=new DragonEncounter{map=map,dragon=dragon,guards=guards,lord=lord,color=color,stage=stage,kind="raid",fighting=true};
   State?.encounters.Add(enc);
   return enc;
  }

  // 勒索：龙落在地图边上要贡品。
  public const float YoungShare=.03f,AdultShare=.06f,RepeatChance=.5f,RepeatFactor=1.5f;public const int RepeatDays=30;
  public static float Demand(Map map,int stage,float factor=1f)=>Mathf.Max(200f,(map?.wealthWatcher?.WealthTotal??0f)*(stage>=1?AdultShare:YoungShare)*factor);
  public static DragonEncounter Extort(Map map,string color,int stage,float factor=1f){
   if(map==null || !Available)return null;var f=Faction();if(f==null)return null;
   if(!EntryCell(map,out var entry))return null;
   var dragon=Make(color,stage,f,map.Tile);if(dragon==null)return null;
   var guards=Guards(color,f,map.Tile,StorytellerUtility.DefaultThreatPointsNow(map));
   Arrive(map,dragon,guards,entry);
   var all=new List<Pawn>{dragon};all.AddRange(guards);
   var lord=LordMaker.MakeNewLord(f,new LordJob_DefendPoint(entry,8f,14f,false,false),map,all.Where(g=>!g.RaceProps.Animal));
   foreach(var g in guards.Where(g=>g.RaceProps.Animal))g.mindState.duty=new PawnDuty(DutyDefOf.Defend,entry,8f);
   var enc=new DragonEncounter{map=map,dragon=dragon,guards=guards,lord=lord,color=color,stage=stage,kind="extortion",demand=Demand(map,stage,factor),factor=factor,deadline=Find.TickManager.TicksGame+GenDate.TicksPerDay};
   State?.encounters.Add(enc);
   SendExtortionLetter(enc);
   return enc;
  }
  public static bool WantsGold(string c)=>c=="Red" || c=="Blue";
  public static string DemandText(DragonEncounter e)=>WantsGold(e.color)?"金子、宝石和艺术品":"银子";
  public static void SendExtortionLetter(DragonEncounter e){
   if(Adventurer.TestSuppressed && !GenCommandLine.CommandLineArgPassed("lichdragonletters"))return;
   var l=(Letter_DragonExtortion)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_DragonExtortionLetter"));
   l.Label=TitleFor(e.color,e.stage)+"的勒索";l.encounter=e;l.lookTargets=new LookTargets(e.dragon);l.relatedFaction=e.dragon.Faction;
   l.Text=ExtortionText(e);
   Find.LetterStack.ReceiveLetter(l);
  }
  public static string ExtortionText(DragonEncounter e)=>e.dragon.LabelShort+"（"+TitleFor(e.color,e.stage)+"）落在殖民地外，带着"+e.guards.Count+"名护卫。它的声音震得人耳朵发麻：\n\n“交出价值 "+e.demand.ToString("0")+" 的"+DemandText(e)+"，我就饶过你们。”\n\n"
   +"付：从储存区里直接拿走这么多价值的东西（不够就拿别的值钱货），它带着护卫离开；三十天后有一半的可能再来，要价再高一半。\n"
   +"谈：派一名殖民者交涉（难度 "+NegotiateDC(e.stage)+"，"+NegotiateChanceText(e)+"；魅力调整值和熟练加值每点 +5%），成功要价减半。"+(e.color=="Green"?"绿龙还会接受交出一名殖民者给它做仆从。":"")+"\n"
   +"拒绝：它立刻进攻，护卫一起上。\n\n一天之内不答复就算拒绝。";
  public static int NegotiateDC(int stage)=>stage>=1?18:15;
  // 1.59.0 热修（去 d20 措辞）：交涉对玩家显示成功率（和 Negotiate 的判定同一概率）。
  public static float NegotiateChance(Pawn who,int stage)=>who==null?0f:Legendary.SaveChance(Legendary.Mod(who,Adventurer.Cha),Legendary.Proficiency(Adventurer.Get(who)?.level??0),0,NegotiateDC(stage));
  static string NegotiateChanceText(DragonEncounter e){var who=Negotiator(e.dragon?.MapHeld);return who!=null?who.LabelShort+"的成功率 "+NegotiateChance(who,e.stage).ToStringPercent():"基础成功率 "+Legendary.SaveChance(0,2,0,NegotiateDC(e.stage)).ToStringPercent();}
  public static Pawn Negotiator(Map map)=>map?.mapPawns.FreeColonistsSpawned.Where(p=>!p.Downed && p.skills!=null && !p.skills.GetSkill(SkillDefOf.Social).TotallyDisabled).OrderByDescending(p=>Legendary.Mod(p,Adventurer.Cha)+Legendary.Proficiency(Adventurer.Get(p)?.level??0)).FirstOrDefault();
  public static bool Negotiate(DragonEncounter e,Pawn who){
   if(e==null || e.negotiated)return false;e.negotiated=true;if(who==null)return false;
   int roll=Legendary.D20()+Legendary.Mod(who,Adventurer.Cha)+Legendary.Proficiency(Adventurer.Get(who)?.level??0);
   bool ok=roll>=NegotiateDC(e.stage);if(ok)e.demand*=.5f;
   if(!Adventurer.TestSuppressed)Messages.Message(who.LabelShort+"的交涉（成功率 "+NegotiateChance(who,e.stage).ToStringPercent()+"）"+(ok?"成功了：要价减半，现在是 "+e.demand.ToString("0")+"。":"失败了：要价不变。"),e.dragon,ok?MessageTypeDefOf.PositiveEvent:MessageTypeDefOf.NegativeEvent,false);
   return ok;
  }
  // 付款：按偏好从储存区拿（红、蓝：金、宝石（玉）、艺术品；绿、黑：银），不够再拿其他值钱的东西。返回拿走的总价值。
  public static float Pay(DragonEncounter e){
   var map=e.map;float need=e.demand,taken=0f;if(map==null)return 0f;
   bool Pref(Thing t)=>WantsGold(e.color)?t.def==ThingDefOf.Gold || t.def.defName=="Jade" || t.def.IsArt:t.def==ThingDefOf.Silver;
   var stored=map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Where(t=>t.Spawned && t.IsInAnyStorage() && t.MarketValue>0f && !(t is Pawn) && !(t is Corpse)).ToList();
   foreach(var t in stored.OrderByDescending(Pref).ThenByDescending(t=>t.MarketValue)){
    if(taken>=need)break;if(t.Destroyed)continue;
    float unit=t.MarketValue;int want=Mathf.Clamp(Mathf.CeilToInt((need-taken)/unit),1,t.stackCount);
    var piece=want>=t.stackCount?t:t.SplitOff(want);taken+=unit*piece.stackCount;piece.Destroy();
   }
   e.paid=taken;e.answered=true;Leave(e);
   if(Roll<RepeatChance)State?.repeats.Add(new DragonRepeat{map=map,color=e.color,stage=e.stage,factor=e.factor*RepeatFactor,tick=Find.TickManager.TicksGame+RepeatDays*GenDate.TicksPerDay});
   return taken;
  }
  public static void Leave(DragonEncounter e){
   var pawns=new List<Pawn>{e.dragon};pawns.AddRange(e.guards);pawns=pawns.Where(p=>p!=null && p.Spawned && !p.Dead).ToList();
   foreach(var p in pawns){p.GetLord()?.RemovePawn(p);if(p.InMentalState)p.MentalState.RecoverFromState();}
   if(pawns.Count>0)LordMaker.MakeNewLord(e.dragon.Faction,new LordJob_ExitMapBest(LocomotionUrgency.Walk,true,true),e.map,pawns);
   e.kind="left";
  }
  public static void Refuse(DragonEncounter e,string why=null){
   if(e==null || e.fighting)return;e.answered=true;e.fighting=true;e.kind="raid";
   var pawns=new List<Pawn>{e.dragon};pawns.AddRange(e.guards);pawns=pawns.Where(p=>p!=null && p.Spawned && !p.Dead).ToList();
   foreach(var p in pawns)p.GetLord()?.RemovePawn(p);
   e.lord=LordMaker.MakeNewLord(e.dragon.Faction,new LordJob_AssaultColony(e.dragon.Faction,false,false,false,false,false),e.map,pawns.Where(p=>!p.RaceProps.Animal));
   if(Info[e.color].guardsMad)MakeMad(e.guards);
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter(TitleFor(e.color,e.stage)+"进攻了",(why??"你拒绝了它的要求。")+e.dragon.LabelShort+"咆哮着扑向殖民地！",LetterDefOf.ThreatBig,e.dragon,e.dragon.Faction);
  }
  // 绿龙：交出一名殖民者做仆从（被带走，算被绑架）。
  public static bool HandOver(DragonEncounter e,Pawn p){
   if(e==null || p==null || e.color!="Green" || e.dragon?.Faction==null)return false;
   if(p.Spawned)p.DeSpawn();
   e.dragon.Faction.kidnapped.Kidnap(p,e.dragon);
   e.answered=true;Leave(e);return true;
  }

  // 龙过境联动：没有成年力能龙裔时，过境结束 25% 一条五色龙（青年 / 成年，按来袭的条件）降落并勒索。
  public const float FlyoverChance=.25f;
  public static DragonEncounter OnFlyoverEnd(Map map){
   if(!FlyoverLinkOn || map==null)return null;
   if(Roll>=FlyoverChance)return null;
   int st=RaidStage(StorytellerUtility.DefaultThreatPointsNow(map),AdventurerEnemies.ColonyMaxLevel(),true);
   if(st<0)st=0;if(st>1)st=1;
   if(st==0 && HasGreatWyrm(map))return null;
   return Extort(map,PickColor(map,true),st);
  }

  // ---------- 龙巢 ----------
  public const float RepelLairChance=.6f,KillLairChance=.3f,MapChance=.15f;public const int LairDays=30,MapPrice=3000;
  public static readonly float[] HoardValue={5000f,15000f,40000f};public static readonly int[] HoardItems={1,2,4};public static readonly float[] StoneChance={0f,.25f,1f};
  public static DragonLair RevealLair(string color,int stage,string why){
   if(!LairOn)return null;var st=State;var f=Faction();if(st==null || f==null)return null;
   var site=NetherilQuest.MakeSite("LP_DragonLair",f,6,15,1f);if(site==null)return null;
   site.customLabel=TitleFor(color,stage)+"的巢穴";
   var lair=new DragonLair{site=site,color=color,stage=stage,expire=Find.TickManager.TicksGame+LairDays*GenDate.TicksPerDay};
   st.lairs.Add(lair);
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("龙巢显露",why+"，"+TitleFor(color,stage)+"的巢穴显露在世界地图上（离殖民地 6～15 格），"+LairDays+"天后消失。\n\n巢主睡在宝藏堆旁，有人走近 10 格或开枪就会醒来；在巢里它的传奇抗性多 1 次，每 10 秒放一次巢穴动作。把它引出巢穴 20 格以外，巢穴动作就停了。宝藏："+HoardValue[stage].ToString("0")+" 价值的金银珠宝、"+HoardItems[stage]+" 件魔法物品"+(StoneChance[stage]>=1f?"和一块传奇重铸之石":"，可能还有一块传奇重铸之石")+"。",LetterDefOf.PositiveEvent,new LookTargets(site));
   return lair;
  }
  public static DragonLair LairOf(Site s)=>s==null?null:State?.lairs.FirstOrDefault(l=>l.site==s);
  public static List<Thing> Hoard(int stage){
   var list=new List<Thing>();float target=HoardValue[Mathf.Clamp(stage,0,2)],sum=0f;
   void Add(ThingDef d,int count,ThingDef stuff=null){if(d==null || count<=0)return;while(count>0){var t=ThingMaker.MakeThing(d,stuff);t.stackCount=Mathf.Min(count,d.stackLimit);count-=t.stackCount;list.Add(t);sum+=t.MarketValue*t.stackCount;}}
   for(int i=0;i<Mathf.Max(1,stage+1);i++){var art=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SculptureSmall"),ThingDefOf.Gold);art.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Outsider);art.TryGetComp<CompArt>()?.InitializeArt(ArtGenerationContext.Outsider);list.Add(art);sum+=art.MarketValue;}
   var jade=DefDatabase<ThingDef>.GetNamedSilentFail("Jade");
   Add(ThingDefOf.Gold,Mathf.FloorToInt(target*.3f/ThingDefOf.Gold.BaseMarketValue));
   if(jade!=null)Add(jade,Mathf.FloorToInt(target*.15f/jade.BaseMarketValue));
   if(sum<target)Add(ThingDefOf.Silver,Mathf.CeilToInt((target-sum)/ThingDefOf.Silver.BaseMarketValue));
   for(int i=0;i<HoardItems[Mathf.Clamp(stage,0,2)];i++){var d=Epic.RandomMagicItemDef();if(d!=null)list.Add(ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null));}
   if(Rand.Chance(StoneChance[Mathf.Clamp(stage,0,2)])){var s=Legend.MakeStone();if(s!=null)list.Add(s);}
   return list;
  }
  public static float HoardGoldValue(List<Thing> l)=>l.Where(t=>t.def==ThingDefOf.Gold || t.def==ThingDefOf.Silver || t.def.defName=="Jade" || t.def.IsArt).Sum(t=>t.MarketValue*t.stackCount);

  // ---------- 战利品 ----------
  public static readonly int[] Scales={20,50,120};
  public static List<Thing> Loot(Pawn d){
   var list=new List<Thing>();var c=ColorOf(d);if(c==null)return list;int st=StageOrYoung(d);
   void Add(string n,int count){var def=DefDatabase<ThingDef>.GetNamedSilentFail(n);if(def==null || count<=0)return;while(count>0){var t=ThingMaker.MakeThing(def);t.stackCount=Mathf.Min(count,def.stackLimit);count-=t.stackCount;list.Add(t);}}
   Add("LP_DragonScale_"+c,Scales[st]);
   if(st>=1){Add("LP_DragonHeart_"+c,st==2?2:1);Add("LP_DragonFang",Rand.RangeInclusive(2,4)*(st==2?2:1));}
   return list;
  }
  // 元素：伤害 → 颜色。
  public static string ElementOf(DamageDef d){
   if(d==null)return null;var n=d.defName;
   if(d==DamageDefOf.Flame || d==DamageDefOf.Burn || n.Contains("Flame") || n.Contains("Incendiary"))return "Red";
   if(n=="LP_Shock" || n.Contains("Lightning") || n.Contains("Electr"))return "Blue";
   if(n=="LP_DragonPoison" || n=="ToxGas" || n.Contains("Tox"))return "Green";
   if(n.Contains("Acid"))return "Black";
   if(n=="LP_Frost" || n=="Frostbite" || n.Contains("Frost") || n.Contains("Cold"))return "White";
   return null;
  }
  public static bool WearsScale(Pawn p,string color)=>p?.apparel!=null && p.apparel.WornApparel.Any(a=>a.Stuff?.defName=="LP_DragonScale_"+color);
  // 龙之血：本色元素 −50%；龙鳞衣甲：没有对应护甲统计的电、毒、酸 −50%（火、冷走热护甲 ×2）。
  public static float ElementFactor(Pawn p,DamageDef d){
   var c=ElementOf(d);if(c==null || p==null)return 1f;float f=1f;
   if(Items.Has(p,"LP_DragonBlood_"+c))f*=.5f;
   if((c=="Blue" || c=="Green" || c=="Black") && WearsScale(p,c))f*=.5f;
   return f;
  }
 }

 // ---------- 真龙之躯：不饿不困、不崩溃、本色免疫；吐息充能、魅惑、飞跃、撤退 ----------
 public class Hediff_DragonBody : HediffWithComps {
  public bool charged;int chargeTick,charmTick,flyTick,ticks;public bool fled;
  public override bool Visible=>true;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((ticks+=delta)<60)return;ticks=0;
   var p=pawn;if(!p.Spawned || p.Dead || p.Downed || p.Map==null)return;
   try{
    int now=Find.TickManager.TicksGame;
    if(!charged && now>=chargeTick){chargeTick=now+Dragons.ChargeTicks;if(Rand.Value<Dragons.ChargeChance)charged=true;}
    if(!p.HostileTo(Faction.OfPlayer) && p.Faction!=null)return; // 勒索谈判期间不动手
    if(p.stances?.stunner?.Stunned==true)return;
    if(charged){var t=Dragons.BreathTarget(p);if(t!=null){charged=false;Dragons.Breathe(p,t.Position);}}
    if(Dragons.ColorOf(p)=="Green" && now>=charmTick){var t=Dragons.CharmTarget(p);if(t!=null){charmTick=now+Dragons.CharmTicks;Dragons.Charm(p,t,MapComp<MapComponent_Legendary>.Get(p.Map));}}
    if(now>=flyTick && Legendary.CanFly(p)){
     var target=p.mindState?.enemyTarget as Pawn??Legendary.EnemiesNear(p,40f).OrderBy(x=>x.Position.DistanceTo(p.Position)).FirstOrDefault();
     if(target!=null && (target.Position.DistanceTo(p.Position)>Dragons.FlyRange || !p.Map.reachability.CanReach(p.Position,target,PathEndMode.Touch,TraverseParms.For(p)))){flyTick=now+Dragons.FlyTicks;ChromaticFlight.Hop(p,Dragons.FlyRange,target.Position);}
    }
    if(!fled && Dragons.ShouldFlee(p)){fled=true;Dragons.Flee(p);}
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 真龙行为出错："+e,74152101);}
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref charged,"lpCharged");Scribe_Values.Look(ref fled,"lpFled");}
 }

 // ---------- 存档：遭遇、重复勒索、龙巢 ----------
 public class DragonEncounter : IExposable {
  public Map map;public Pawn dragon;public List<Pawn> guards=new List<Pawn>();public Lord lord;public string color,kind;public int stage,deadline=-1;public float demand,factor=1f,paid;public bool answered,negotiated,fighting,fled,resolved;
  public void ExposeData(){
   Scribe_References.Look(ref map,"map");Scribe_References.Look(ref dragon,"dragon");ScribePawns.Look(ref guards,"guards");Scribe_References.Look(ref lord,"lord");
   Scribe_Values.Look(ref color,"color");Scribe_Values.Look(ref kind,"kind");Scribe_Values.Look(ref stage,"stage");Scribe_Values.Look(ref deadline,"deadline",-1);
   Scribe_Values.Look(ref demand,"demand");Scribe_Values.Look(ref factor,"factor",1f);Scribe_Values.Look(ref paid,"paid");
   Scribe_Values.Look(ref answered,"answered");Scribe_Values.Look(ref negotiated,"negotiated");Scribe_Values.Look(ref fighting,"fighting");Scribe_Values.Look(ref fled,"fled");Scribe_Values.Look(ref resolved,"resolved");
   if(guards==null)guards=new List<Pawn>();
  }
 }
 public class DragonRepeat : IExposable {public Map map;public string color;public int stage,tick;public float factor=1f;public void ExposeData(){Scribe_References.Look(ref map,"map");Scribe_Values.Look(ref color,"color");Scribe_Values.Look(ref stage,"stage");Scribe_Values.Look(ref tick,"tick");Scribe_Values.Look(ref factor,"factor",1f);}}
 public class DragonLair : IExposable {public Site site;public string color;public int stage,expire;public Pawn owner;public void ExposeData(){Scribe_References.Look(ref site,"site");Scribe_Values.Look(ref color,"color");Scribe_Values.Look(ref stage,"stage");Scribe_Values.Look(ref expire,"expire");Scribe_References.Look(ref owner,"owner");}}
 public class GameComponent_Dragons : GameComponent {
  public List<DragonEncounter> encounters=new List<DragonEncounter>();public List<DragonRepeat> repeats=new List<DragonRepeat>();public List<DragonLair> lairs=new List<DragonLair>();
  public int lastAncientRaid=-1,nextRaidTick=-1;
  public GameComponent_Dragons(Game g){}
  public DragonEncounter Encounter(Pawn p)=>encounters.FirstOrDefault(e=>e.dragon==p);
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;if(now%250!=0)return;
   try{
    foreach(var e in encounters.ToList()){
     if(e.resolved){encounters.Remove(e);continue;}
     if(e.kind=="extortion" && !e.answered && e.deadline>=0 && now>=e.deadline){Dragons.Refuse(e,"一天过去了，你没有答复。");continue;}
     if(e.dragon==null)continue;
     if(e.dragon.Dead){e.resolved=true;if(e.stage==1 && Dragons.Roll<Dragons.KillLairChance)Dragons.RevealLair(e.color,2,"在"+e.dragon.LabelShort+"的尸体旁，你们找到了通往它长辈巢穴的线索");continue;} // 击杀成年龙：30% 它的长辈（远古龙）的巢穴显露
     if(!e.dragon.Spawned && (e.fighting || e.fled)){e.resolved=true;
      if(e.stage>=1 && Dragons.Roll<Dragons.RepelLairChance)Dragons.RevealLair(e.color,e.stage,"击退"+e.dragon.LabelShort+"之后，你们找到了它飞回去的方向");}
     else if(!e.dragon.Spawned && e.kind=="left")e.resolved=true;
    }
    if(repeats.Count>0)foreach(var r in repeats.ToList())if(now>=r.tick){repeats.Remove(r);if(r.map!=null && Find.Maps.Contains(r.map) && Dragons.ExtortionOn)Dragons.Extort(r.map,r.color,r.stage,r.factor);}
    if(lairs.Count>0)foreach(var l in lairs.ToList()){if(l.site==null || l.site.Destroyed){lairs.Remove(l);continue;}if(!l.site.HasMap && now>=l.expire){l.site.Destroy();lairs.Remove(l);}}
   }catch(Exception x){Log.ErrorOnce("[DND边缘大陆] 真龙遭遇出错："+x,74152102);}
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref encounters,"lpDragonEncounters",LookMode.Deep);Scribe_Collections.Look(ref repeats,"lpDragonRepeats",LookMode.Deep);Scribe_Collections.Look(ref lairs,"lpDragonLairs",LookMode.Deep);
   Scribe_Values.Look(ref lastAncientRaid,"lpLastAncientRaid",-1);Scribe_Values.Look(ref nextRaidTick,"lpNextDragonRaid",-1);
   if(encounters==null)encounters=new List<DragonEncounter>();if(repeats==null)repeats=new List<DragonRepeat>();if(lairs==null)lairs=new List<DragonLair>();
  }
 }

 // ---------- 信件 ----------
 public class Letter_DragonExtortion : ChoiceLetter {
  public DragonEncounter encounter;
  public override IEnumerable<DiaOption> Choices{get{
   var e=encounter;if(ArchivedOnly || e==null || e.answered || e.dragon==null || e.dragon.Dead){yield return Option_Close;yield break;}
   yield return Choice("付（"+e.demand.ToString("0")+" 价值的"+Dragons.DemandText(e)+"）",()=>Dragons.Pay(e),true);
   var who=Dragons.Negotiator(e.map);
   var neg=Choice("谈"+(who!=null?"：派"+who.LabelShort+"交涉（成功率 "+Dragons.NegotiateChance(who,e.stage).ToStringPercent()+"）":""),()=>{Dragons.Negotiate(e,who);Dragons.SendExtortionLetter(e);},true);
   if(e.negotiated)neg.Disable("已经谈过了");else if(who==null)neg.Disable("没有能交涉的殖民者");
   yield return neg;
   if(e.color=="Green")foreach(var p in e.map.mapPawns.FreeColonistsSpawned.Where(x=>!x.Downed).OrderBy(x=>x.MarketValue).Take(3).ToList())yield return Choice("交出"+p.LabelShort+"做仆从",()=>Dragons.HandOver(e,p),true);
   yield return Choice("拒绝",()=>Dragons.Refuse(e),true);
   yield return Option_Postpone;
  }}
  DiaOption Choice(string text,Action act,bool close)=>new DiaOption(text){action=()=>{act?.Invoke();if(close)Find.LetterStack.RemoveLetter(this);},resolveTree=true};
  public override void ExposeData(){
   base.ExposeData();Pawn d=encounter?.dragon;Scribe_References.Look(ref d,"lpDragon");
   if(Scribe.mode==LoadSaveMode.PostLoadInit)encounter=Dragons.State?.Encounter(d);
  }
 }

 // ---------- 事件 ----------
 public class IncidentWorker_DragonExtortion : IncidentWorker_DND {
  public static int Stage(Map map){int st=Dragons.RaidStage(StorytellerUtility.DefaultThreatPointsNow(map),AdventurerEnemies.ColonyMaxLevel(),true);if(st>1)st=1;if(st==0 && Dragons.HasGreatWyrm(map))return -1;return st;}
  protected override bool CanFireNowSub(IncidentParms parms)=>Dragons.ExtortionOn && parms.target is Map map && Stage(map)>=0;
  protected override bool TryExecuteWorker(IncidentParms parms){var map=parms.target as Map;int st=Stage(map);if(st<0)return false;return Dragons.Extort(map,Dragons.PickColor(map,true),st)!=null;}
 }
 public class IncidentWorker_DragonRaid : IncidentWorker_DND {
  public static int Stage(Map map,float points)=>Dragons.RaidStage(points,AdventurerEnemies.ColonyMaxLevel(),Dragons.AncientUsedThisYear());
  protected override bool CanFireNowSub(IncidentParms parms){
   if(!Dragons.RaidOn || !(parms.target is Map map))return false;var st=Dragons.State;if(st!=null && st.nextRaidTick>=0 && GenTicks.TicksAbs<st.nextRaidTick)return false;
   return Stage(map,parms.points>0?parms.points:StorytellerUtility.DefaultThreatPointsNow(map))>=0;
  }
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;float pts=parms.points>0?parms.points:StorytellerUtility.DefaultThreatPointsNow(map);int stage=Stage(map,pts);if(stage<0)return false;
   var c=Dragons.PickColor(map,false);var enc=Dragons.Raid(map,c,stage,pts);if(enc==null)return false;
   var st=Dragons.State;if(st!=null)st.nextRaidTick=GenTicks.TicksAbs+Mathf.RoundToInt(Rand.Range(20f,30f)*GenDate.TicksPerDay/Mathf.Max(.25f,Events2.Frequency));
   SendStandardLetter(Dragons.TitleFor(c,stage)+"来袭",enc.dragon.LabelShort+"（"+Dragons.TitleFor(c,stage)+"）带着"+enc.guards.Count+"名护卫扑向殖民地！\n\n它会喷吐"+Dragons.Info[c].breathLabel+"（豁免成功伤害减半），"+(stage>=1?"有传奇抗性（拦下眩晕、精神控制、控制类状态和疼痛倒地）和传奇动作，看到它的人要做感知豁免（难度 "+Legendary.DC(enc.dragon,Adventurer.Cha)+"，属性调整值和熟练加值每点 +5% 成功率），否则会惊惧。":"")+"倒地即死，不能俘虏。",LetterDefOf.ThreatBig,parms,new LookTargets(enc.dragon));
   return true;
  }
 }

 // ---------- 巢穴：地图组件（巢主、巢穴动作、提示圈） ----------
 public class LairPending : IExposable {public IntVec3 cell;public int tick,index;public void ExposeData(){Scribe_Values.Look(ref cell,"cell");Scribe_Values.Look(ref tick,"tick");Scribe_Values.Look(ref index,"index");}}
 public class MapComponent_DragonLair : MapComponent {
  public Pawn owner;public IntVec3 center=IntVec3.Invalid;public string color;public bool asleep;
  public List<LairPending> pending=new List<LairPending>();public HashSet<IntVec3> ice=new HashSet<IntVec3>();
  public const int Interval=600,Warning=60;public const float LureRadius=20f,WakeRadius=10f;
  public static int Fired;
  public MapComponent_DragonLair(Map map):base(map){}
  public bool Active=>owner!=null && !owner.Dead && owner.Spawned && owner.Map==map && center.IsValid;
  public bool OwnerHome=>Active && owner.Position.DistanceTo(center)<=LureRadius;
  public IEnumerable<Pawn> Enemies=>owner==null?Enumerable.Empty<Pawn>():map.mapPawns.AllPawnsSpawned.Where(p=>!p.Dead && !p.Downed && p.HostileTo(owner));
  public void Wake(string why){if(!asleep)return;asleep=false;if(owner!=null && owner.Spawned){owner.jobs?.EndCurrentJob(JobCondition.InterruptForced);Items.Mote(owner,"醒了！",new Color(1f,.4f,.3f));if(!Adventurer.TestSuppressed)Messages.Message(owner.LabelShort+"醒了（"+why+"）！",owner,MessageTypeDefOf.ThreatBig,false);}}
  public LairPending Queue(Pawn target,int index=-1){
   if(target==null)return null;var pd=new LairPending{cell=target.Position,tick=Find.TickManager.TicksGame+Warning,index=index>=0?index:Rand.RangeInclusive(0,2)};
   pending.Add(pd);FleckMaker.Static(pd.cell,map,FleckDefOf.PsycastAreaEffect,3f);return pd;
  }
  public override void MapComponentTick(){
   int now=Find.TickManager.TicksGame;if(owner==null || (now+map.uniqueID)%60!=0)return;
   try{
    if(!Active){pending.Clear();return;}
    if(asleep){if(Enemies.Any(p=>p.Position.DistanceTo(owner.Position)<=WakeRadius))Wake("有人走近了");else if(now%600==0)FleckMaker.ThrowMetaIcon(owner.Position,map,FleckDefOf.SleepZ);}
    foreach(var pd in pending.ToList())if(now>=pd.tick){pending.Remove(pd);if(OwnerHome)LairActions.Do(this,pd.index,pd.cell);}
    if(ice.Count>0)foreach(var p in Enemies.ToList())if(ice.Contains(p.Position) && p.pather!=null && p.pather.Moving && Rand.Chance(.2f)){p.stances?.stunner?.StunFor(60,owner,false,true,false);Items.Mote(p,"滑倒了",Color.white);}
    if(!asleep && OwnerHome && (now+map.uniqueID)%Interval<60){var t=Enemies.Where(p=>p.Faction!=null && p.Faction.IsPlayer).RandomElementWithFallback();if(t!=null)Queue(t);}
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 巢穴动作出错："+e,74152103);}
  }
  public override void ExposeData(){
   base.ExposeData();Scribe_References.Look(ref owner,"lpLairOwner");Scribe_Values.Look(ref center,"lpLairCenter",IntVec3.Invalid);Scribe_Values.Look(ref color,"lpLairColor");Scribe_Values.Look(ref asleep,"lpLairAsleep");
   Scribe_Collections.Look(ref pending,"lpLairPending",LookMode.Deep);if(pending==null)pending=new List<LairPending>();
   var iceList=ice.ToList();Scribe_Collections.Look(ref iceList,"lpLairIce",LookMode.Value);if(Scribe.mode==LoadSaveMode.PostLoadInit)ice=new HashSet<IntVec3>(iceList??new List<IntVec3>());
  }
 }
 // 巢穴动作（每色三个）：落点先出提示圈，1 秒后生效。「警觉」专长免疫突袭类（直接打在人身上的那几个）。
 public static class LairActions {
  public static readonly string[][] Names={new[]{"岩浆喷涌","地震","火山烟"},new[]{"落雷","沙尘","流沙"},new[]{"荆棘缠绕","毒雾","魅惑低语"},new[]{"黑暗","酸泉","沼泽化"},new[]{"冰刺","冰雾","冰面"}};
  public static string Name(string color,int i)=>Names[Array.IndexOf(Dragons.Colors,color)][Mathf.Clamp(i,0,2)];
  static bool Ambush(string c,int i)=>i==0 || c=="Black" && i==1;
  static IEnumerable<Pawn> Victims(MapComponent_DragonLair mc,IntVec3 cell,float r,string c,int i)=>mc.Enemies.Where(p=>p.Position.DistanceTo(cell)<=r && !(Ambush(c,i) && Feats.Has(p,Feats.Alert))).ToList();
  static void Terrain(Map map,IntVec3 cell,float r,string def){var t=DefDatabase<TerrainDef>.GetNamedSilentFail(def);if(t==null)return;foreach(var c in GenRadial.RadialCellsAround(cell,r,true))if(c.InBounds(map) && c.GetEdifice(map)==null)map.terrainGrid.SetTerrain(c,t);}
  static void Hurt(Pawn p,string def,float amt,Pawn by){var d=DefDatabase<DamageDef>.GetNamedSilentFail(def)??DamageDefOf.Blunt;p.TakeDamage(new DamageInfo(d,amt*Dragons.DamageMult,.2f,-1f,by));}
  public static string Do(MapComponent_DragonLair mc,int i,IntVec3 cell){
   var map=mc.map;var o=mc.owner;var c=mc.color;int dc=Legendary.DC(o,Adventurer.Con);string name=Name(c,i);MapComponent_DragonLair.Fired++;
   Items.Mote(o,"巢穴动作："+name,new Color(1f,.5f,.3f));
   switch(c+i){
    case "Red0":foreach(var p in Victims(mc,cell,3f,c,i))Hurt(p,"Flame",15f,o);foreach(var x in GenRadial.RadialCellsAround(cell,3f,true))if(x.InBounds(map))FireUtility.TryStartFireIn(x,map,.5f,o);break;
    case "Red1":foreach(var p in Victims(mc,cell,6f,c,i))if(!Legendary.Save(p,Adventurer.Dex,dc,o))p.stances?.stunner?.StunFor(60,o,false,true,false);foreach(var b in GenRadial.RadialCellsAround(cell,6f,true).Where(x=>x.InBounds(map)).Select(x=>x.GetEdifice(map)).Where(b=>b!=null && b.def.useHitPoints).ToList())b.TakeDamage(new DamageInfo(DamageDefOf.Blunt,40f,0f,-1f,o));break;
    case "Red2":case "White1":GasUtility.AddGas(cell,map,GasType.BlindSmoke,3f);break;
    case "Blue0":map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map,cell));break;
    case "Blue1":foreach(var p in Victims(mc,cell,10f,c,i))Items.Give(p,"LP_LairSandstorm",1200);break;
    case "Blue2":Terrain(map,cell,2f,"Mud");break;
    case "Green0":var g=Victims(mc,cell,1.5f,c,i).FirstOrDefault();if(g!=null && !Legendary.Save(g,Adventurer.Str,dc,o))g.stances?.stunner?.StunFor(180,o,false,true,false);break;
    case "Green1":if(ModsConfig.BiotechActive)GasUtility.AddGas(cell,map,GasType.ToxGas,2.5f);else foreach(var p in Victims(mc,cell,2.5f,c,i))HealthUtility.AdjustSeverity(p,HediffDefOf.ToxicBuildup,.08f);break;
    case "Green2":var w=Victims(mc,cell,2f,c,i).FirstOrDefault();if(w!=null)Dragons.Charm(o,w);break;
    case "Black0":foreach(var p in Victims(mc,cell,8f,c,i))Items.Give(p,"LP_LairDarkness",1200);break;
    case "Black1":foreach(var p in Victims(mc,cell,2f,c,i))Hurt(p,"LP_AlchemyAcid",12f,o);break;
    case "Black2":Terrain(map,cell,3f,"Marsh");break;
    case "White0":foreach(var p in Victims(mc,cell,2f,c,i)){Hurt(p,"LP_Frost",12f,o);if(!p.Dead)Fiends.Chill(p,1);}break;
    case "White2":foreach(var x in GenRadial.RadialCellsAround(cell,3f,true))if(x.InBounds(map))mc.ice.Add(x);Terrain(map,cell,3f,"Ice");break;
   }
   return name;
  }
 }

 // ---------- 龙巢地图 ----------
 public class SitePartWorker_DragonLair : SitePartWorker {}
 public class GenStep_DragonLair : GenStep {
  public override int SeedPart=>741520;
  public override void Generate(Map map,GenStepParams parms){
   var site=parms.sitePart?.site;var lair=Dragons.LairOf(site)??new DragonLair{color=Dragons.Colors.RandomElement(),stage=1};
   try{Build(map,lair,site?.Faction??Dragons.Faction());}catch(Exception e){Log.Error("[DND边缘大陆] 生成龙巢出错："+e);}
  }
  public static Pawn Build(Map map,DragonLair lair,Faction f){
   var info=Dragons.Info[lair.color];var c=map.Center;int r=12;
   var rock=DefDatabase<ThingDef>.GetNamedSilentFail(info.lairRock)??ThingDefOf.Granite;
   var floor=DefDatabase<TerrainDef>.GetNamedSilentFail(info.lairFloor)??TerrainDefOf.Gravel;
   var room=CellRect.CenteredOn(c,r);
   foreach(var x in room.ExpandedBy(3).Cells){
    if(!x.InBounds(map))continue;foreach(var t in x.GetThingList(map).ToList())if(!(t is Pawn))t.Destroy(DestroyMode.Vanish);
    bool inside=room.Contains(x);
    if(inside){map.terrainGrid.SetTerrain(x,floor);map.roofGrid.SetRoof(x,RoofDefOf.RoofRockThick);}
    else if(!(x.z<c.z+3 && x.z>c.z-3 && x.x>c.x)){var w=ThingMaker.MakeThing(rock);GenSpawn.Spawn(w,x,map);} // 东面留一条洞口
   }
   var hoard=Dragons.Hoard(lair.stage);
   foreach(var t in hoard)GenPlace.TryPlaceThing(t,c,map,ThingPlaceMode.Near);
   var owner=Dragons.Make(lair.color,lair.stage,f,map.Tile);
   GenSpawn.Spawn(owner,c+IntVec3.West*3,map);
   var guards=Dragons.Guards(lair.color,f,map.Tile,6000f,2);
   foreach(var g in guards)GenSpawn.Spawn(g,CellFinder.RandomClosewalkCellNear(c+IntVec3.East*6,map,4),map);
   LordMaker.MakeNewLord(f,new LordJob_DefendPoint(c,10f,16f,false,false),map,new List<Pawn>{owner}.Concat(guards.Where(g=>!g.RaceProps.Animal)));
   foreach(var g in guards.Where(g=>g.RaceProps.Animal))g.mindState.duty=new PawnDuty(DutyDefOf.Defend,c,10f);
   var mc=MapComp<MapComponent_DragonLair>.Get(map);
   if(mc!=null){mc.owner=owner;mc.center=c;mc.color=lair.color;mc.asleep=true;}
   owner.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.Wait,30000),JobCondition.InterruptForced);
   lair.owner=owner;
   if(lair.stage<=1 && Monsters.BehirOn && Rand.Chance(.25f) && CellFinder.TryFindRandomEdgeCellWith(x=>x.Standable(map),map,0f,out var edge))Monsters.SpawnHunter(map,edge); // 1.52.1：贝希尔追着龙来了
   Legendary.Resistance(owner)?.Refill();
   return owner;
  }
 }

 // ---------- 龙巢情报（同盟商队 15%，殖民地最高等级 ≥28 时） ----------
 public class StockGenerator_DragonLairMap : StockGenerator {
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   if(!Dragons.LairOn || AdventurerEnemies.ColonyMaxLevel()<Dragons.AncientLevel || !Rand.Chance(Dragons.MapChance))yield break;
   var d=DefDatabase<ThingDef>.GetNamedSilentFail("LP_DragonLairMap");if(d!=null)yield return ThingMaker.MakeThing(d);
  }
  public override bool HandlesThingDef(ThingDef thingDef)=>thingDef.defName=="LP_DragonLairMap";
 }
 public class CompUseEffect_DragonLairMap : CompUseEffect {
  public override void DoEffect(Pawn usedBy){base.DoEffect(usedBy);Dragons.RevealLair(Dragons.Colors.RandomElement(),2,usedBy.LabelShort+"读懂了龙巢情报");}
  public override AcceptanceReport CanBeUsedBy(Pawn p)=>Dragons.LairOn?true:new AcceptanceReport("真龙内容没有开启（需要 Humanoid Alien Races）");
 }

 // ---------- 钩子 ----------
 [StaticConstructorOnStartup]
 public static class DragonStartup {
  static DragonStartup(){
   Legendary.SaveBonusHook=Dragons.SaveBonus;
   Legendary.TitleHook=Dragons.Title;
   Legendary.InOwnLairHook=p=>{var mc=MapComp<MapComponent_DragonLair>.Get(p?.Map);return mc!=null && mc.owner==p && mc.OwnerHome;};
   Legendary.SlayRecordHook=p=>Dragons.IsChromatic(p);
   LA_Tail.TailDamage=p=>Dragons.IsChromatic(p)?new[]{22f,35f,55f}[Dragons.StageOrYoung(p)]*Dragons.DamageMult:20f;
   LA_Wing.WingDamage=p=>Dragons.IsChromatic(p)?Mathf.Max(15f,Dragons.WingDamage[Dragons.StageOrYoung(p)])*Dragons.DamageMult:15f;
   var stat=StatDefOf.MeleeDamageFactor;if(stat.parts==null)stat.parts=new List<StatPart>();
   if(!stat.parts.Any(x=>x is StatPart_DragonStrength))stat.parts.Add(new StatPart_DragonStrength{parentStat=stat});
  }
 }
 public class StatPart_DragonStrength : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Dragons.IsChromatic(p))val*=Dragons.DamageMult;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Dragons.IsChromatic(p) && Math.Abs(Dragons.DamageMult-1f)>.001f?"真龙强度：×"+Dragons.DamageMult.ToStringPercent():null;
 }
 [HarmonyPatch(typeof(Pawn),"get_HealthScale")]
 public static class DragonHealthScale {
  static void Postfix(Pawn __instance,ref float __result){
   if(Dragons.IsChromatic(__instance))__result*=Dragons.HealthMult;
   else{float m=Monsters.HealthMult(__instance);if(m!=1f)__result*=m;} // 1.52.1 传奇怪
   if(__instance.health?.hediffSet!=null && __instance.RaceProps.Humanlike)foreach(var b in Blood)if(Items.Has(__instance,b.Def)){__result*=1.05f;break;}
  }
  // 1.58.6：原来每次都拼 5 个 "LP_DragonBlood_"+颜色 字符串（HealthScale 每个部位都会读）。
  static readonly DefRef<HediffDef>[] Blood=Dragons.Colors.Select(c=>new DefRef<HediffDef>("LP_DragonBlood_"+c)).ToArray();
 }
 // 渲染：根节点按年龄档放大（不走 RaceSizeExtension，避免进巨躯规则）。
 [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.ScaleFor))]
 public static class DragonRenderSize {
  static void Postfix(PawnRenderNode node,PawnDrawParms parms,ref Vector3 __result){if(!RenderTags.IsRoot(node?.Props?.tagDef))return;if(Dragons.IsChromatic(parms.pawn))__result*=Dragons.RenderScale[Dragons.StageOrYoung(parms.pawn)];else{float m=Monsters.RenderScale(parms.pawn);if(m!=1f)__result*=m;}}
 }
 // 龙尸：剥皮只出龙鳞、真龙之心、龙牙；肉不能吃（不出肉），也没有“屠宰人形”心情。
 [HarmonyPatch(typeof(Corpse),nameof(Corpse.ButcherProducts))]
 public static class DragonButcher {
  static bool Prefix(Corpse __instance,ref IEnumerable<Thing> __result){if(Monsters.NoButcher(__instance.InnerPawn)){__result=Enumerable.Empty<Thing>();return false;}if(!Dragons.IsChromatic(__instance.InnerPawn))return true;__result=Dragons.Loot(__instance.InnerPawn);return false;}
 }
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class DragonElementResist {
  static void Prefix(Thing __instance,ref DamageInfo dinfo){
   if(!(__instance is Pawn p) || !p.RaceProps.Humanlike)return;
   float mf=Monsters.DamageFactor(p,dinfo);if(mf!=1f)dinfo.SetAmount(dinfo.Amount*mf); // 1.52.1：贝希尔打龙 ×1.5、半巫妖怕圣水银器 ×2
   if(Dragons.IsChromatic(p))return;
   float f=Dragons.ElementFactor(p,dinfo.Def);if(f<1f)dinfo.SetAmount(dinfo.Amount*f);
  }
  // 被魅惑的人受到伤害立刻醒。
  static void Postfix(Thing __instance){if(__instance is Pawn p && !p.Dead && p.MentalStateDef?.defName=="LP_DragonCharmed")p.MentalState.RecoverFromState();}
 }

 // ---------- 设置（右栏「真龙与传奇怪」） ----------
 public partial class Settings {
  public bool chromaticDragons=true,dragonExtortion=true,dragonRaid=true,dragonLair=true,dragonFlyoverLink=true;
  public int dragonStrength=100,dragonIsekaiHealth=200,dragonIsekaiDamage=150,dragonAncientLevel=28;
  void ExposeChromatic(){
   ExposeMonsters(); // 1.52.1 传奇怪
   Scribe_Values.Look(ref chromaticDragons,"lpChromaticDragons",true);Scribe_Values.Look(ref dragonExtortion,"lpDragonExtortion",true);Scribe_Values.Look(ref dragonRaid,"lpDragonRaid",true);
   Scribe_Values.Look(ref dragonLair,"lpDragonLair",true);Scribe_Values.Look(ref dragonFlyoverLink,"lpDragonFlyoverLink",true);
   Scribe_Values.Look(ref dragonStrength,"lpDragonStrength",100);Scribe_Values.Look(ref dragonIsekaiHealth,"lpDragonIsekaiHealth",200);Scribe_Values.Look(ref dragonIsekaiDamage,"lpDragonIsekaiDamage",150);Scribe_Values.Look(ref dragonAncientLevel,"lpDragonAncientLevel",28);
   dragonStrength=Math.Max(50,Math.Min(300,dragonStrength));dragonIsekaiHealth=Math.Max(100,Math.Min(500,dragonIsekaiHealth));dragonIsekaiDamage=Math.Max(100,Math.Min(300,dragonIsekaiDamage));dragonAncientLevel=Math.Max(21,Math.Min(40,dragonAncientLevel));
  }
 }
 public static class ChromaticSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;l.GapLine();l.Label("真龙与传奇怪");
   bool har=Dragons.Available;var old=GUI.color;if(!har){GUI.color=Color.gray;l.Label("需要 Humanoid Alien Races（五色真龙是 HAR 种族；没装时真龙事件全部关闭）");}
   l.CheckboxLabeled("五色真龙",ref s.chromaticDragons,"红、蓝、绿、黑、白五色真龙（青年 / 成年 / 远古）。有传奇抗性、传奇动作、骇人威仪和吐息，是冒险者等级的终局对手。关闭后下面的事件都不出现。");
   l.CheckboxLabeled("　真龙勒索",ref s.dragonExtortion,"一条青年或成年龙（白龙不勒索）落在地图边上要贡品：付、谈或拒绝。需要殖民地最高冒险者等级 ≥8。");
   l.CheckboxLabeled("　真龙来袭",ref s.dragonRaid,"龙带着本色护卫直接进攻。青年：最高等级 ≥8、点数 ≥1500；成年：≥16、≥4000；远古：≥"+s.dragonAncientLevel+"、≥9000，每年最多一次。冷却 20～30 天。");
   l.CheckboxLabeled("　龙巢远征",ref s.dragonLair,"击退成年龙后 60% 它的巢穴显露、击杀后 30% 远古龙巢显露；殖民地最高等级够高时同盟商队会卖龙巢情报。巢里有大量宝藏、魔法物品和传奇重铸之石。");
   l.CheckboxLabeled("　龙过境联动",ref s.dragonFlyoverLink,"殖民地没有成年力能龙裔时，龙过境结束有 25% 一条五色龙降落勒索。有力能龙裔时龙过境照旧。");
   l.Label("真龙强度："+s.dragonStrength+"%（乘在生命和伤害上）");s.dragonStrength=Mathf.RoundToInt(l.Slider(s.dragonStrength,50,300)/5f)*5;
   if(Adventurer.IsekaiActive){
    l.Label("Isekai 下真龙额外生命：×"+(s.dragonIsekaiHealth/100f).ToString("0.##"));s.dragonIsekaiHealth=Mathf.RoundToInt(l.Slider(s.dragonIsekaiHealth,100,500)/10f)*10;
    l.Label("Isekai 下真龙额外伤害：×"+(s.dragonIsekaiDamage/100f).ToString("0.##"));s.dragonIsekaiDamage=Mathf.RoundToInt(l.Slider(s.dragonIsekaiDamage,100,300)/10f)*10;
   }
   l.Label("远古龙来袭需要的殖民地最高等级："+s.dragonAncientLevel);s.dragonAncientLevel=Mathf.RoundToInt(l.Slider(s.dragonAncientLevel,21,40));
   GUI.color=old;
   MonsterSettings.Draw(l); // 1.52.1 传奇怪（死亡骑士不需要 HAR，不灰显）
  }
 }
}
