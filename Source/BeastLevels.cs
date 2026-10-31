using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.70.0 野兽强化（见 ArtSource/Beasts-1.70.0-report.md）：
 // 野性等级：野生 / 非玩家动物第一次出现时定等级（狂暴兽群、敌对组按每只点数 ÷45 ±2，环境野兽 1～殖民地最高等级 −3，上限殖民地最高等级 +3），
 // 存在可见状态 LP_BeastLevel 上；等级折成虚拟力量 / 体质（1 级 10、20 级 20、30 级 24），套殖民者同一条六维曲线：近战伤害 × 力量倍率，部位生命 ×(1 + 5%/级，到 20 级) × (1 + 体质调整值 × 15%)（1.81.0 热修3），移速 +1%/级（至多 +30%）。
 // 精英（恐怖 / 狂暴 / 远古 / 深渊腐化）存在同一个状态里，另挂一个只显示的 LP_BeastElite；兽群首领必为精英、等级 +3，死后同群其余野兽退出狂暴并逃离地图。
 // 玩家的动物靠近战伤害和搬运 / 救援积累经验成长（上限：主人的冒险者等级，否则殖民地最高等级，硬上限 20）。
 // 所有钩子都在生成 / 出生 / 伤害 / 死亡 / 工作结束时触发，没有逐刻扫描。
 public static class BeastLevels {
  public const int MaxLevel=40,TameCap=20,CapOffset=3,AmbientOffset=3,LeaderBonus=3,AncientBonus=5,XpPerLevel=100;
  public const float SpeedPer=.01f,SpeedMax=.3f,PartPer=.05f,CompanionXp=1.25f,BurnShare=.3f,DreadPart=3f,DreadMelee=1.5f,FrenzyCooldown=.67f,AncientArmor=.4f,DefaultElite=.05f,EliteMax=.2f,HaulXp=5f,RescueXp=20f;
  public const int None=0,Dread=1,Frenzy=2,Ancient=3,Abyssal=4;
  public static readonly string[] EliteNames={"","恐怖","狂暴","远古","深渊腐化"};
  public static readonly Color[] EliteColors={Color.white,new Color(.85f,.35f,.3f),new Color(1f,.55f,.2f),new Color(.55f,.75f,1f),new Color(.7f,.3f,.85f)};
  public static readonly string[] EliteEffects={"",
   "部位生命 ×3，近战伤害 ×1.5。",
   "近战冷却 −33%（攻速约 +50%），重伤也不会逃跑。",
   "野性等级 +5，利器 / 钝器护甲 +40%。",
   "近战命中时追加约 30% 的烧伤。"};
  // 测试用：设了就代替随机数（精英判定 / 精英种类 / 等级浮动）。
  public static float? TestRoll;public static int? TestVariant,TestNoise;
  public static bool TestSuppressed;
  static float Roll=>TestRoll??Rand.Value;
  public static bool On=>LichMod.Settings?.beastLevels!=false;
  public static float EliteChance=>Mathf.Clamp(LichMod.Settings?.beastEliteChance??DefaultElite,0f,EliteMax);

  static readonly DefRef<HediffDef> def=new DefRef<HediffDef>("LP_BeastLevel"),eliteDef=new DefRef<HediffDef>("LP_BeastElite");
  static readonly DefRef<ThingDef> heart=new DefRef<ThingDef>("LP_BeastKingHeart"),amulet=new DefRef<ThingDef>("LP_BeastKingAmulet"),rangerArmor=new DefRef<ThingDef>("LP_RangerLeathers"),rangerCloak=new DefRef<ThingDef>("LP_WoodwalkerCloak");
  public static HediffDef Def=>def.Def;public static HediffDef EliteDef=>eliteDef.Def;public static ThingDef Heart=>heart.Def;public static ThingDef Amulet=>amulet.Def;
  public static Hediff_BeastLevel Get(Pawn p)=>HediffIndex<Hediff_BeastLevel>.Get(p);

  // 对象：动物（不含虫族、异象实体、传奇单位、龙、死灵造物 / 召唤物）。
  public static bool Eligible(Pawn p){
   var r=p?.RaceProps;
   return r!=null && r.Animal && !r.Humanlike && !r.IsMechanoid && !r.Insect && !r.IsAnomalyEntity && !Legendary.Is(p) && !Dragons.Is(p) && !Adventurer.Excluded(p);
  }

  // ---------- 等级 → 数值 ----------
  // 虚拟力量 / 体质：1 级 10，20 级 20（之间线性），之后每级 +0.4（30 级 24，40 级 28）。
  public static float Score(int level){level=Mathf.Clamp(level,1,MaxLevel);return level<=Adventurer.MaxLevel?10f+(level-1)*10f/(Adventurer.MaxLevel-1):20f+(level-Adventurer.MaxLevel)*.4f;}
  // 殖民者的六维曲线（Adventurer.Curve），小数部分在相邻两档之间插值。
  public static float CurveF(float s){int i=Mathf.FloorToInt(s);float f=s-i,a=Adventurer.Curve(i);return f<.0001f?a:Mathf.Lerp(a,Adventurer.Curve(i+1),f);}
  public static float Mult(int level)=>CurveF(Score(level));
  public static float MeleeFactor(int level,int elite)=>Adventurer.MeleeFactor((Score(level)-10f)/2f)*(elite==Dread?DreadMelee:1f); // 1.81.0：近战伤害同冒险者，1 + 虚拟力量调整值 × 10%（20 级 ×1.5，40 级 ×1.9）
  // 1.81.0 热修3：部位生命同冒险者 —— 等级成长每级 +5%（到 20 级，×1.95）× 虚拟体质（1 + 调整值 × 15%，20 级 ×1.75，40 级 ×2.35）。
  public static float ConPart(int level)=>1f+Hediff_Adventurer.ConPartPerMod*(Score(level)-10f)/2f;
  public static float LegacyPartFactor(int level,int elite)=>(1f+.01f*(Mathf.Clamp(level,1,MaxLevel)-1))*Mult(level)*(elite==Dread?DreadPart:1f); // 热修3 之前（读旧档缩小伤口用）
  public static float PartFactor(int level,int elite)=>(1f+PartPer*(Mathf.Clamp(level,1,Adventurer.MaxLevel)-1))*ConPart(level)*(elite==Dread?DreadPart:1f);
  public static float SpeedFactor(int level)=>1f+Mathf.Min(SpeedMax,SpeedPer*(Mathf.Clamp(level,1,MaxLevel)-1));
  // 部位生命（LimbHealth.cs 的 PartHealth.WithoutReinforcement 乘进去）。热路径：先看是不是动物，再查登记表。
  public static float PartHealthFactor(Pawn p){
   if(p?.def?.race==null || !p.def.race.Animal)return 1f;
   var h=Get(p);return h==null || !h.Applies?1f:PartFactor(h.level,h.elite);
  }

  // ---------- 定等级 ----------
  static int colonyMax=1,colonyTick=-99999;
  public static int ColonyMax(){int now=GenTicks.TicksAbs;if(now-colonyTick>=GenDate.TicksPerHour || now<colonyTick){colonyMax=AdventurerEnemies.ColonyMaxLevel();colonyTick=now;}return colonyMax;}
  public static void ForgetColony()=>colonyTick=-99999;
  public static int Cap()=>Mathf.Clamp(ColonyMax()+CapOffset,1,MaxLevel);
  static int Noise=>TestNoise??Rand.RangeInclusive(-AdventurerEnemies.Noise,AdventurerEnemies.Noise);
  // 兽群 / 敌对组：同冒险者敌人公式（1 + 每只点数 ÷45 + 浮动），限 1～cap。
  public static int PackLevel(float perPawn,int cap,int noise)=>Mathf.Clamp(AdventurerEnemies.BaseLevel(perPawn)+noise,1,Mathf.Clamp(cap,1,MaxLevel));
  public static int AmbientMax(int colony)=>Mathf.Clamp(colony-AmbientOffset,1,MaxLevel);
  public static int AmbientLevel(int colony)=>TestNoise.HasValue?Mathf.Clamp(1+TestNoise.Value,1,AmbientMax(colony)):Rand.RangeInclusive(1,AmbientMax(colony));
  public static int RollElite(bool force){
   if(!force && Roll>=EliteChance)return None;
   return TestVariant??Rand.RangeInclusive(Dread,Abyssal);
  }

  public static Hediff_BeastLevel Give(Pawn p,int level,int elite=None,bool leader=false,int pack=0){
   if(TestSuppressed || !On || Def==null || p?.health==null || !Eligible(p))return null;
   var h=Get(p);if(h!=null)return h;
   if(elite==Ancient)level+=AncientBonus;
   if(leader)level+=LeaderBonus;
   h=(Hediff_BeastLevel)HediffMaker.MakeHediff(Def,p);
   h.level=Mathf.Clamp(level,1,MaxLevel);h.elite=elite;h.leader=leader;h.pack=pack;h.Severity=h.level;
   p.health.AddHediff(h);
   if(elite>None){
    if(EliteDef!=null)p.health.AddHediff(HediffMaker.MakeHediff(EliteDef,p));
    if(elite==Frenzy && p.mindState?.mentalStateHandler!=null)p.mindState.mentalStateHandler.neverFleeIndividual=true;
   }
   // 首领：没有名字的动物叫「首领XX」。
   if(leader && p.Name==null)p.Name=new NameSingle("首领"+p.KindLabel);
   return h;
  }
  // 第一次出现在地图上（或旧存档读档后补一次）：玩家的动物 1 级，其余按环境规则。
  public static Hediff_BeastLevel InitAmbient(Pawn p){
   if(TestSuppressed || !On || Get(p)!=null || !Eligible(p))return null;
   if(p.Faction!=null && p.Faction.IsPlayer)return Give(p,1);
   return Give(p,AmbientLevel(ColonyMax()),RollElite(false));
  }
  static float Cp(Pawn p)=>p?.kindDef?.combatPower??0f;
  // 狂暴兽群：一群一个首领（战斗力最高的那只），必为精英、等级 +3。points<0 时每只按自己的战斗力。
  public static int Pack(List<Pawn> list,float points){
   if(TestSuppressed || !On || list==null)return 0;
   var animals=list.Where(a=>a!=null && !a.Dead && Get(a)==null && Eligible(a)).ToList();if(animals.Count==0)return 0;
   int cap=Cap();float per=points>0f?points/animals.Count:-1f;
   var leader=animals.OrderByDescending(Cp).First();int id=leader.thingIDNumber;
   foreach(var a in animals){bool lead=a==leader;Give(a,PackLevel(per>=0f?per:Cp(a),cap,Noise),RollElite(lead),lead,id);}
   return id;
  }
  // 敌对派系战斗组里的动物：按组的每人点数。不敌对的组交给出生时的环境规则。
  public static void Group(List<Pawn> list,PawnGroupMakerParms parms){
   if(TestSuppressed || !On || list==null || list.Count==0 || parms?.faction==null)return;
   var g=parms.groupKind;bool fighting=g==PawnGroupKindDefOf.Combat || g==PawnGroupKindDefOf.Settlement || g==PawnGroupKindDefOf.Settlement_RangedOnly;
   if(!fighting || !parms.faction.HostileTo(Faction.OfPlayer))return;
   int cap=Cap();float per=parms.points/list.Count;
   foreach(var a in list)if(a!=null && Get(a)==null && Eligible(a))Give(a,PackLevel(per,cap,Noise),RollElite(false));
  }

  // ---------- 驯兽成长 ----------
  public static float XpToNext(int level)=>XpPerLevel*Mathf.Max(1,level);
  public static Pawn Master(Pawn p)=>p?.playerSettings?.Master;
  // 成长上限：主人的冒险者等级，没有主人时殖民地最高等级；冒险者系统关了就是 20。硬上限 20（传奇等级不给动物）。
  public static int GrowthCap(Pawn p){
   if(ClassSubclass.Companions.Count>0 && ClassSubclass.Companions.Contains(p)){var mh=Adventurer.Get(Master(p));if(mh!=null)return Mathf.Clamp(mh.level,1,MaxLevel);} // 子职业·兽王的游侠伙伴：跟到游侠的冒险者等级（最多 40）
   int c;if(!Adventurer.Enabled)c=TameCap;else{var m=Master(p);c=m!=null?(Adventurer.Get(m)?.level??1):ColonyMax();}
   return Mathf.Clamp(c,1,TameCap);
  }
  // 动物伙伴：主人是游侠（游侠向导背景，或穿着游侠皮甲 / 林行者斗篷）或德鲁伊（VPE 的德鲁伊路径）。
  public static bool Companion(Pawn master){
   if(master==null)return false;
   var st=master.story;if(st!=null && (st.Childhood?.defName=="LP_Bs_RangerGuide" || st.Adulthood?.defName=="LP_Bs_RangerGuide"))return true;
   var worn=master.apparel?.WornApparel;if(worn!=null)for(int i=0;i<worn.Count;i++){var d=worn[i].def;if(d==rangerArmor.Def && d!=null || d==rangerCloak.Def && d!=null)return true;}
   return HasDruidPath(master);
  }
  static FieldInfo pathsField;static bool pathsSearched;
  // VPE 不在主程序集里引用：按类型名找心灵能力状态，读 unlockedPaths。
  public static bool HasDruidPath(Pawn p){
   var list=p?.health?.hediffSet?.hediffs;if(list==null)return false;
   for(int i=0;i<list.Count;i++){
    var h=list[i];if(h.GetType().Name!="Hediff_PsycastAbilities")continue;
    if(!pathsSearched){pathsSearched=true;pathsField=AccessTools.Field(h.GetType(),"unlockedPaths");}
    if(pathsField?.GetValue(h) is System.Collections.IEnumerable paths)foreach(var o in paths)if(o is Def d && d.defName=="LP_Path_Druid")return true;
    return false;
   }
   return false;
  }
  public static float XpFactor(Pawn p)=>Companion(Master(p))?CompanionXp:1f;
  static readonly HashSet<string> WorkJobs=new HashSet<string>{"HaulToCell","HaulToContainer","HaulToTransporter","UnloadYourInventory"};
  public static float JobXp(JobDef d){if(d==null)return 0f;if(d==JobDefOf.Rescue)return RescueXp;return WorkJobs.Contains(d.defName)?HaulXp:0f;}

  // ---------- Isekai ----------
  // 装了 Isekai 时它给生物挂 MobRankComponent（按等阶乘属性）；有它就只显示野性等级，不再叠加数值。
  public static bool IsekaiRanked(Pawn p){
   if(p==null || !Adventurer.IsekaiActive)return false;
   var comps=p.AllComps;if(comps==null)return false;
   for(int i=0;i<comps.Count;i++)if(comps[i].GetType().Name=="MobRankComponent")return true;
   return false;
  }

  // ---------- 死亡 ----------
  public static int HeartsDropped,PacksBroken;
  public static void OnDeath(Pawn p,Hediff_BeastLevel h,Map map,IntVec3 at){
   if(h==null || map==null)return;
   if(h.elite>None && Heart!=null && at.IsValid){var t=ThingMaker.MakeThing(Heart);if(GenPlace.TryPlaceThing(t,at,map,ThingPlaceMode.Near))HeartsDropped++;}
   if(h.leader && h.pack!=0){int n=MapComp<MapComponent_BeastPacks>.Get(map)?.LeaderDied(h.pack,p)??0;if(n>0){PacksBroken++;Messages.Message("兽群首领倒下了，其余 "+n+" 只野兽四散逃走。",new TargetInfo(at,map),MessageTypeDefOf.PositiveEvent,false);}}
  }
  // 退出狂暴并离开地图：先恢复，再逃跑；逃跑状态起不来时照原版狂暴兽群的做法让它马上离开地图。
  public static bool Scatter(Pawn m){
   if(m==null || m.Dead || !m.Spawned || m.mindState==null)return false;
   var ms=m.mindState.mentalStateHandler;
   if(m.InMentalState && m.MentalStateDef!=null && m.MentalStateDef.IsAggro)m.MentalState.RecoverFromState();
   if(ms!=null){ms.neverFleeIndividual=false;if(!m.Downed)ms.TryStartMentalState(MentalStateDefOf.PanicFlee,"首领倒下",true,true);}
   m.mindState.exitMapAfterTick=Find.TickManager.TicksGame;
   return true;
  }
 }

 public class Hediff_BeastLevel : HediffWithComps,IIndexedHediff {
  public int level=1,elite,pack;public bool leader;public float xp;
  public bool IndexLive{get;set;}
  int isekai=-1;
  public bool Isekai{get{if(isekai<0)isekai=BeastLevels.IsekaiRanked(pawn)?1:0;return isekai==1;}}
  public bool Applies=>BeastLevels.On && !Isekai;
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);HediffIndex<Hediff_BeastLevel>.Add(this);}
  public override void PostRemoved(){base.PostRemoved();HediffIndex<Hediff_BeastLevel>.Removed(this);}
  public override bool ShouldRemove=>false;
  public override string LabelBase=>"野性等级 "+level;
  public override Color LabelColor=>elite>BeastLevels.None?BeastLevels.EliteColors[elite]:base.LabelColor;

  HediffStage stage;int stageKey=-1;
  public void Dirty(){stage=null;Severity=level;if(pawn?.health?.hediffSet!=null && pawn.health.hediffSet.hediffs.Contains(this))pawn.health.Notify_HediffChanged(this);}
  public override HediffStage CurStage{get{int key=(Applies?1:0)+level*2+elite*1000;if(stage==null || stageKey!=key){stage=Build();stageKey=key;}return stage;}}
  HediffStage Build(){
   var st=new HediffStage{becomeVisible=true};
   if(!Applies)return st;
   var f=new List<StatModifier>();var o=new List<StatModifier>();
   void Add(List<StatModifier> list,StatDef s,float v,float neutral){if(s!=null && Math.Abs(v-neutral)>.0001f)list.Add(new StatModifier{stat=s,value=v});}
   Add(f,StatDefOf.MeleeDamageFactor,BeastLevels.MeleeFactor(level,elite),1f);
   Add(f,StatDefOf.MoveSpeed,BeastLevels.SpeedFactor(level),1f);
   if(elite==BeastLevels.Frenzy)Add(f,DefDatabase<StatDef>.GetNamedSilentFail("MeleeCooldownFactor"),BeastLevels.FrenzyCooldown,1f);
   if(elite==BeastLevels.Ancient){Add(o,StatDefOf.ArmorRating_Sharp,BeastLevels.AncientArmor,0f);Add(o,StatDefOf.ArmorRating_Blunt,BeastLevels.AncientArmor,0f);}
   if(f.Count>0)st.statFactors=f;if(o.Count>0)st.statOffsets=o;
   return st;
  }

  public bool GainXp(float amount){
   if(!BeastLevels.On || amount<=0f || pawn==null)return false;
   int cap=BeastLevels.GrowthCap(pawn);if(level>=cap)return false;
   xp+=amount*BeastLevels.XpFactor(pawn);bool up=false;
   while(level<cap && xp>=BeastLevels.XpToNext(level)){xp-=BeastLevels.XpToNext(level);level++;up=true;}
   if(level>=cap)xp=0f;
   if(up){Dirty();if(pawn.Spawned && PawnUtility.ShouldSendNotificationAbout(pawn))Messages.Message(pawn.LabelShortCap+" 的野性等级提升到 "+level,pawn,MessageTypeDefOf.PositiveEvent,false);}
   return up;
  }

  public string InspectLine=>"野性等级 "+level+(leader?" · 兽群首领":"")+(elite>BeastLevels.None?" · "+BeastLevels.EliteNames[elite]:"");
  public override string TipStringExtra{get{
   var sb=new StringBuilder();
   if(leader)sb.AppendLine("兽群首领：它倒下后，同群其余野兽会退出狂暴、逃离地图。");
   if(elite>BeastLevels.None)sb.AppendLine("精英「"+BeastLevels.EliteNames[elite]+"」："+BeastLevels.EliteEffects[elite]);
   if(!BeastLevels.On)sb.AppendLine("野兽野性等级已在设置里关闭，不提供效果。");
   else if(Isekai)sb.AppendLine("已由异世界等级接管：数值加成交给 Isekai 的生物等阶，这里只显示等级。");
   else{
    float s=BeastLevels.Score(level);
    sb.AppendLine("相当于力量 / 体质 "+s.ToString("0.#")+"（与殖民者同一条六维曲线）");
    sb.AppendLine("近战伤害 ×"+BeastLevels.MeleeFactor(level,elite).ToString("0.##")+"，部位生命 ×"+BeastLevels.PartFactor(level,elite).ToString("0.##")+"，移速 +"+((BeastLevels.SpeedFactor(level)-1f)*100f).ToString("0")+"%");
   }
   if(pawn?.Faction!=null && pawn.Faction.IsPlayer){
    int cap=BeastLevels.GrowthCap(pawn);
    if(level>=cap)sb.AppendLine("成长上限 "+cap+" 级"+(level>cap?"（等主人或殖民地等级追上来后再成长）":"（已到上限）"));
    else sb.AppendLine("经验 "+xp.ToString("0")+" / "+BeastLevels.XpToNext(level).ToString("0")+"（成长上限 "+cap+" 级）");
    if(BeastLevels.Companion(BeastLevels.Master(pawn)))sb.AppendLine("动物伙伴：主人是游侠或德鲁伊，经验 +25%。");
   }
   return sb.ToString().TrimEnd();
  }}

  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref level,"lpLevel",1);Scribe_Values.Look(ref elite,"lpElite",0);Scribe_Values.Look(ref pack,"lpPack",0);
   Scribe_Values.Look(ref leader,"lpLeader",false);Scribe_Values.Look(ref xp,"lpXp",0f);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    level=Mathf.Clamp(level,1,BeastLevels.MaxLevel);HediffIndex<Hediff_BeastLevel>.Add(this);
    if(elite==BeastLevels.Frenzy && pawn?.mindState?.mentalStateHandler!=null)pawn.mindState.mentalStateHandler.neverFleeIndividual=true; // 狂暴：读档后照样不逃
   }
  }
 }
 // 只显示精英种类（数值都在野性等级状态里）。
 public class Hediff_BeastElite : HediffWithComps {
  int Variant=>BeastLevels.Get(pawn)?.elite??0;
  public override bool ShouldRemove=>false;
  public override string LabelBase{get{var h=BeastLevels.Get(pawn);return (h!=null && h.leader?"首领 · ":"")+"精英 · "+(Variant>0?BeastLevels.EliteNames[Variant]:"未知");}}
  public override Color LabelColor=>Variant>0?BeastLevels.EliteColors[Variant]:base.LabelColor;
  public override string TipStringExtra=>Variant>0?BeastLevels.EliteEffects[Variant]+"死后掉落兽王之心。":null;
 }

 public class BeastPack : IExposable {
  public int id;public Pawn leader;public List<Pawn> members=new List<Pawn>();
  public void ExposeData(){
   Scribe_Values.Look(ref id,"id",0);Scribe_References.Look(ref leader,"leader");Scribe_Collections.Look(ref members,"members",LookMode.Reference);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(members==null)members=new List<Pawn>();members.RemoveAll(m=>m==null);}
  }
 }
 // 每张地图的兽群登记（存档）。读档时也给旧存档里还没有野性等级的动物补一次。
 public class MapComponent_BeastPacks : MapComponent {
  public const int InitVersion=1;
  public List<BeastPack> packs=new List<BeastPack>();int initVersion;
  public MapComponent_BeastPacks(Map map):base(map){}
  public BeastPack Find(int id){for(int i=0;i<packs.Count;i++)if(packs[i].id==id)return packs[i];return null;}
  public void Join(int id,Pawn p,bool isLeader){
   if(id==0 || p==null)return;
   var pk=Find(id);if(pk==null){pk=new BeastPack{id=id};packs.Add(pk);}
   if(isLeader)pk.leader=p;else if(!pk.members.Contains(p))pk.members.Add(p);
  }
  public int LeaderDied(int id,Pawn leader){
   var pk=Find(id);if(pk==null)return 0;packs.Remove(pk);
   int n=0;foreach(var m in pk.members)if(m!=leader && m.Map==map && BeastLevels.Scatter(m))n++;
   return n;
  }
  void Prune(){
   packs.RemoveAll(pk=>pk==null);
   foreach(var pk in packs){pk.members.RemoveAll(m=>m==null || m.Dead || m.Destroyed || !m.Spawned || m.Map!=map);if(pk.leader!=null && (pk.leader.Destroyed || pk.leader.Dead))pk.leader=null;}
   packs.RemoveAll(pk=>pk.leader==null || pk.members.Count==0);
  }
  public override void FinalizeInit(){
   base.FinalizeInit();
   if(initVersion>=InitVersion)return;
   if(BeastLevels.TestSuppressed || !BeastLevels.On)return;
   initVersion=InitVersion;
   foreach(var p in map.mapPawns.AllPawnsSpawned.ToList())if(p.RaceProps.Animal && BeastLevels.Get(p)==null)BeastLevels.InitAmbient(p);
  }
  public override void ExposeData(){
   base.ExposeData();
   if(Scribe.mode==LoadSaveMode.Saving)Prune();
   Scribe_Collections.Look(ref packs,"lpBeastPacks",LookMode.Deep);
   Scribe_Values.Look(ref initVersion,"lpBeastInit",0);
   if(packs==null)packs=new List<BeastPack>();
  }
 }

 // ---------- 钩子 ----------
 // 第一次出现在地图上：没有野性等级就按环境规则定；兽群成员登记到这张地图。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.SpawnSetup))]
 public static class BeastLevelSpawn {
  static void Postfix(Pawn __instance,Map map,bool respawningAfterLoad){
   if(respawningAfterLoad || BeastLevels.TestSuppressed || __instance?.def?.race==null || !__instance.def.race.Animal)return;
   var h=BeastLevels.Get(__instance)??BeastLevels.InitAmbient(__instance);
   if(h!=null && h.pack!=0)MapComp<MapComponent_BeastPacks>.Get(map)?.Join(h.pack,__instance,h.leader);
  }
 }
 // 狂暴兽群（原版 IncidentWorker_AggressiveAnimals、狂暴兽群据点等都走这里）：生成后、出生前定等级和首领。
 [HarmonyPatch]
 public static class BeastPackGenerate {
  static IEnumerable<MethodBase> TargetMethods()=>typeof(AggressiveAnimalIncidentUtility).GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name=="GenerateAnimals" && m.ReturnType==typeof(List<Pawn>));
  static void Postfix(List<Pawn> __result,object[] __args){
   float points=-1f;if(__args!=null)foreach(var a in __args)if(a is float f){points=f;break;}
   BeastLevels.Pack(__result,points);
  }
 }
 [HarmonyPatch(typeof(PawnGroupMakerUtility),nameof(PawnGroupMakerUtility.GeneratePawns))]
 public static class BeastGroupLevels {
  [HarmonyPriority(Priority.Last)]
  static void Postfix(PawnGroupMakerParms parms,ref IEnumerable<Pawn> __result){
   if(__result==null || parms?.faction==null || BeastLevels.TestSuppressed || !BeastLevels.On)return;
   var list=__result as List<Pawn>??__result.ToList();__result=list;
   BeastLevels.Group(list,parms);
  }
 }
 // 近战命中后：深渊腐化追加烧伤；玩家的动物按造成的伤害得经验。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class BeastLevelMelee {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target,DamageWorker.DamageResult __result){
   var p=__instance.CasterPawn;if(p?.def?.race==null || !p.def.race.Animal)return;
   var h=BeastLevels.Get(p);if(h==null)return;
   float dealt=__result?.totalDamageDealt??0f;if(dealt<=0f)return;
   var t=target.Thing;
   if(h.elite==BeastLevels.Abyssal && t!=null && !t.Destroyed && !(t is Pawn tp && tp.Dead))t.TakeDamage(new DamageInfo(DamageDefOf.Burn,Mathf.Max(1f,dealt*BeastLevels.BurnShare),0f,-1f,p));
   if(p.Faction!=null && p.Faction.IsPlayer)h.GainXp(dealt);
  }
 }
 // 玩家的动物干完搬运 / 救援：少量经验。
 [HarmonyPatch(typeof(Pawn_JobTracker),nameof(Pawn_JobTracker.EndCurrentJob))]
 public static class BeastLevelJobXp {
  static void Prefix(Pawn_JobTracker __instance,JobCondition condition,Pawn ___pawn){
   if(condition!=JobCondition.Succeeded || ___pawn?.def?.race==null || !___pawn.def.race.Animal || ___pawn.Faction==null || !___pawn.Faction.IsPlayer)return;
   float x=BeastLevels.JobXp(__instance.curJob?.def);if(x>0f)BeastLevels.Get(___pawn)?.GainXp(x);
  }
 }
 // 死亡：精英掉兽王之心，首领死后同群散开。地图和位置要在死之前记下来。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class BeastLevelDeath {
  public class State {public Hediff_BeastLevel h;public Map map;public IntVec3 at;}
  static void Prefix(Pawn __instance,out State __state){
   __state=null;if(__instance?.def?.race==null || !__instance.def.race.Animal || __instance.Dead)return;
   var h=BeastLevels.Get(__instance);if(h==null || h.elite==BeastLevels.None && !h.leader)return;
   var map=__instance.MapHeld;if(map==null)return;
   __state=new State{h=h,map=map,at=__instance.PositionHeld};
  }
  static void Postfix(Pawn __instance,State __state){if(__state!=null && __instance.Dead)BeastLevels.OnDeath(__instance,__state.h,__state.map,__state.at);}
 }
 // 狂暴精英：不因危险或重伤逃跑（neverFleeIndividual 管精神状态，这里管动物的逃跑工作）。
 [HarmonyPatch(typeof(FleeUtility),nameof(FleeUtility.ShouldAnimalFleeDanger))]
 public static class BeastFrenzyNoFlee {
  static void Postfix(Pawn pawn,ref bool __result){if(__result && BeastLevels.Get(pawn) is Hediff_BeastLevel h && h.elite==BeastLevels.Frenzy && BeastLevels.On)__result=false;}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetInspectString))]
 public static class BeastLevelInspect {
  static void Postfix(Pawn __instance,ref string __result){
   if(__instance?.def?.race==null || !__instance.def.race.Animal)return;
   var h=BeastLevels.Get(__instance);if(h==null)return;
   __result=__result.NullOrEmpty()?h.InspectLine:__result+"\n"+h.InspectLine;
  }
 }
 // 兽王护符：驯兽 +2（技能等级的天资部分）。
 [HarmonyPatch(typeof(SkillRecord),nameof(SkillRecord.Aptitude),MethodType.Getter)]
 public static class BeastAmuletAptitude {
  static void Postfix(SkillRecord __instance,ref int __result){
   if(__instance.def!=SkillDefOf.Animals)return;
   var worn=__instance.Pawn?.apparel?.WornApparel;var a=BeastLevels.Amulet;if(worn==null || a==null)return;
   for(int i=0;i<worn.Count;i++)if(worn[i].def==a){__result+=2;return;}
  }
 }
 [StaticConstructorOnStartup]
 public static class BeastLevelStartup {static BeastLevelStartup(){if(GenCommandLine.CommandLineArgPassed("lichtest"))BeastLevels.TestSuppressed=true;}} // 自测进程：野性等级检查组自己临时打开

 // 1.70.0 设置（放在“野兽”分组里）。
 public partial class Settings {
  public bool beastLevels=true;public float beastEliteChance=BeastLevels.DefaultElite;
  void ExposeBeastLevels(){Scribe_Values.Look(ref beastLevels,"beastLevels",true);Scribe_Values.Look(ref beastEliteChance,"beastEliteChance",BeastLevels.DefaultElite);}
 }
}
