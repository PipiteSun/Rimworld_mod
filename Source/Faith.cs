using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.39.0 信仰（ArtSource/Faith-1.39.0-spec.md）：11 位神、殖民地级神眷值、圣坛与守护神、祈祷、献祭、季度显圣、神选仪式、信徒/无神者特性。
 // 神术在 FaithMiracles.cs，任务在 FaithIncidents.cs，阿斯莫/提夫林在 FaithRaces.cs。给成神线程的接口是 FaithUtility（名字按需求第八节）。
 public enum DeityAlignment{Good,Neutral,Evil}
 public partial class LP_DeityDef {
  public int devoteeDegree;                       // 信徒特性 LP_Devotee 的等级（洛山达 1 … 维拉基斯 11）
  public DeityAlignment alignment=DeityAlignment.Neutral;
  public bool worshippable;
  public string epithet;
  public List<LP_DeityDef> enemies=new List<LP_DeityDef>();
  public AbilityDef domainAbility;
  public ThingDef relic;
  public List<string> likedOfferings=new List<string>(); // ThingDef 或 ThingCategoryDef 的 defName
  public Type blessing,curse;                      // FaithManifest 子类
  public string iconPath;
  [Unsaved] Texture2D icon;[Unsaved] List<ThingDef> likedThings;[Unsaved] List<ThingCategoryDef> likedCategories;[Unsaved] FaithManifest blessingWorker,curseWorker;
  public Texture2D Icon=>icon??(icon=(iconPath.NullOrEmpty()?null:ContentFinder<Texture2D>.Get(iconPath,false))??BaseContent.BadTex);
  public bool Good=>alignment==DeityAlignment.Good;
  public bool Evil=>alignment==DeityAlignment.Evil;
  public string AlignmentLabel=>alignment==DeityAlignment.Good?"善":alignment==DeityAlignment.Evil?"恶":"中立";
  public string FullLabel=>epithet.NullOrEmpty()?label:label+"（"+epithet+"）";
  // 敌对关系是双向的：洛山达列了莎尔，马拉尔、塔洛斯列了洛山达，所以洛山达的敌对神是莎尔、马拉尔、塔洛斯。
  public bool EnemyOf(LP_DeityDef o)=>o!=null && o!=this && (enemies.Contains(o) || o.enemies.Contains(this));
  public IEnumerable<LP_DeityDef> Enemies=>FaithUtility.AllDeities.Where(EnemyOf);
  public List<ThingDef> LikedThings{get{Resolve();return likedThings;}}
  public List<ThingCategoryDef> LikedCategories{get{Resolve();return likedCategories;}}
  void Resolve(){
   if(likedThings!=null)return;
   likedThings=new List<ThingDef>();likedCategories=new List<ThingCategoryDef>();
   foreach(var n in likedOfferings){var t=DefDatabase<ThingDef>.GetNamedSilentFail(n);if(t!=null){likedThings.Add(t);continue;}var c=DefDatabase<ThingCategoryDef>.GetNamedSilentFail(n);if(c!=null)likedCategories.Add(c);}
  }
  public bool Likes(ThingDef t)=>t!=null && (LikedThings.Contains(t) || LikedCategories.Any(c=>c.ContainedInThisOrDescendant(t)));
  public string LikedLabel=>string.Join("、",LikedThings.Select(t=>t.label).Concat(LikedCategories.Select(c=>c.label)));
  public FaithManifest BlessingWorker=>blessing==null?null:blessingWorker??(blessingWorker=(FaithManifest)Activator.CreateInstance(blessing));
  public FaithManifest CurseWorker=>curse==null?null:curseWorker??(curseWorker=(FaithManifest)Activator.CreateInstance(curse));
  public override IEnumerable<string> ConfigErrors(){
   foreach(var e in base.ConfigErrors())yield return e;
   foreach(var n in likedOfferings)if(DefDatabase<ThingDef>.GetNamedSilentFail(n)==null && DefDatabase<ThingCategoryDef>.GetNamedSilentFail(n)==null)yield return defName+": liked offering "+n+" is neither a ThingDef nor a ThingCategoryDef";
   if(blessing!=null && !typeof(FaithManifest).IsAssignableFrom(blessing))yield return defName+": blessing is not a FaithManifest";
   if(curse!=null && !typeof(FaithManifest).IsAssignableFrom(curse))yield return defName+": curse is not a FaithManifest";
  }
 }
 // 派系固定信仰（Patches/Faith.xml 挂在 FactionDef 上）：成员 25% 带其中一位神的信徒特性；亵渎者、寻回圣物用它找敌对神的派系。
 public class FaithExtension : DefModExtension {public List<LP_DeityDef> deities=new List<LP_DeityDef>();}

 // ---------- 设置（左栏“信仰”分组） ----------
 public partial class Settings {
  public bool faithSystem=true,faithManifest=true;public int faithFavorPercent=100,faithQuestPercent=100;
  void ExposeFaith(){
   Scribe_Values.Look(ref faithSystem,"lpFaithSystem",true);Scribe_Values.Look(ref faithManifest,"lpFaithManifest",true);
   Scribe_Values.Look(ref faithFavorPercent,"lpFaithFavorPercent",100);Scribe_Values.Look(ref faithQuestPercent,"lpFaithQuestPercent",100);
   faithFavorPercent=Math.Max(50,Math.Min(200,faithFavorPercent));faithQuestPercent=Math.Max(0,Math.Min(200,faithQuestPercent));
  }
 }
 public static class FaithSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;l.GapLine();l.Label("信仰");
   l.CheckboxLabeled("信仰系统",ref s.faithSystem,"圣坛、守护神、神眷、神术、信仰任务和季度显圣。关闭后神眷不再变化，神术不能施放，信仰任务不出现，信徒和无神者不再随机出现（已有的保留）。");
   l.Label("神眷获取倍率："+s.faithFavorPercent+"%（祈祷、献祭、各神作为和任务奖励得到的神眷；扣神眷不受影响）");s.faithFavorPercent=(int)l.Slider(s.faithFavorPercent,50,200);
   l.Label("信仰任务频率："+s.faithQuestPercent+"%（朝圣者、神谕试炼、寻回圣物、亵渎者；和“DND 事件频率”相乘）");s.faithQuestPercent=(int)l.Slider(s.faithQuestPercent,0,200);
   l.CheckboxLabeled("季度显圣",ref s.faithManifest,"每15天按守护神的神眷结算一次：≥50赐福3天，≤-30降罚1天。");
  }
 }

 // ---------- 内部规则 ----------
 public static class Faith {
  public const float Min=-100f,Max=100f,PrayerGain=.5f,PrayerDailyCap=5f,DailyDecay=.5f,EnemyDaily=.2f,ShardFavor=10f,SilverPerFavor=100f,LikedFactor=3f,BlessedWaterCost=.5f;
  public const float DomainThreshold=20f,BlessingThreshold=50f,ChosenThreshold=80f,CurseThreshold=-30f,ForsakenThreshold=-50f,ChangePenalty=30f,RelicThreshold=80f;
  public const float ChosenFavorCost=50f,ConstructDaily=.2f,LichDaily=.5f,TalosDailyCap=3f;
  public const int ChosenShards=3,ChosenPerGod=3,ChangeCooldownDays=15,ManifestDays=15,BlessingDays=3,CurseDays=1,PrayTicks=2500,ChosenTicks=5000,HistoryKeep=40;
  public const float FactionDevoteeChance=.25f,AtheistCommonality=.1f,DevoteeCommonality=.3f;
  public static bool Enabled=>LichMod.Settings?.faithSystem!=false;
  public static float GainFactor=>(LichMod.Settings?.faithFavorPercent??100)/100f;
  public static float QuestFactor=>(LichMod.Settings?.faithQuestPercent??100)/100f;
  public static int Now=>Find.TickManager?.TicksGame??0;
  static TraitDef devotee,atheist;static HediffDef chosen;static JobDef pray,ritual;static ThingDef altar,symbol,water,shard;static DamageDef radiant;static GameConditionDef manifest;static ResearchProjectDef research,planetouched;
  public static TraitDef Devotee=>devotee??(devotee=DefDatabase<TraitDef>.GetNamedSilentFail("LP_Devotee"));
  public static TraitDef Atheist=>atheist??(atheist=DefDatabase<TraitDef>.GetNamedSilentFail("LP_Atheist"));
  public static HediffDef ChosenDef=>chosen??(chosen=DefDatabase<HediffDef>.GetNamedSilentFail("LP_Chosen"));
  public static JobDef PrayJob=>pray??(pray=DefDatabase<JobDef>.GetNamedSilentFail("LP_Pray"));
  public static JobDef RitualJob=>ritual??(ritual=DefDatabase<JobDef>.GetNamedSilentFail("LP_ChosenRitual"));
  public static ThingDef AltarDef=>altar??(altar=DefDatabase<ThingDef>.GetNamedSilentFail("LP_FaithAltar"));
  public static ThingDef SymbolDef=>symbol??(symbol=DefDatabase<ThingDef>.GetNamedSilentFail("LP_HolySymbol"));
  public static ThingDef WaterDef=>water??(water=DefDatabase<ThingDef>.GetNamedSilentFail("LP_BlessedWater"));
  public static ThingDef ShardDef=>shard??(shard=DefDatabase<ThingDef>.GetNamedSilentFail("LP_MiracleShard"));
  public static DamageDef Radiant=>radiant??(radiant=DefDatabase<DamageDef>.GetNamedSilentFail("LP_Radiant")??DamageDefOf.Burn);
  public static GameConditionDef ManifestDef=>manifest??(manifest=DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_FaithManifest"));
  public static ResearchProjectDef Research=>research??(research=DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LP_Research_Faith"));
  public static ResearchProjectDef Planetouched=>planetouched??(planetouched=DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LP_Research_Planetouched"));
  public static LP_DeityDef God(string key){var l=FaithUtility.AllDeities;for(int i=0;i<l.Count;i++)if(l[i].key==key)return l[i];return null;} // 1.58.6：不用 LINQ 闭包
  public static WorldComponent_Faith Comp=>WorldComp<WorldComponent_Faith>.Get(); // 1.58.6：缓存（信仰属性部件、植物生长每次都查）

  // 自测：除信仰检查组外不随机生成信徒/无神者（其他组的测试小人不该带心智抗性、心灵敏感度之类的变化）。
  public static bool TestSuppressed;
  static readonly AccessTools.FieldRef<TraitDef,float> commonalityRef=AccessTools.FieldRefAccess<TraitDef,float>("commonality");
  public static void ApplySettings(){
   bool on=Enabled && !TestSuppressed;
   if(Devotee!=null)commonalityRef(Devotee)=on?DevoteeCommonality:0f;
   if(Atheist!=null)commonalityRef(Atheist)=on?AtheistCommonality:0f;
  }
  public static float Commonality(TraitDef d)=>d==null?0f:commonalityRef(d);

  // 正数 × 神眷获取倍率（祈祷、献祭、作为、任务奖励）；负数原样。
  public static void Gain(LP_DeityDef god,float amount,string reason){if(god==null || !Enabled)return;FaithUtility.AddFavor(god,amount>0?amount*GainFactor:amount,reason);}
  // 各神作为只对守护神和守护神的敌对神结算。
  public static bool Relevant(LP_DeityDef god){var p=FaithUtility.Patron;return god!=null && p!=null && (god==p || p.EnemyOf(god));}
  public static void Deed(string key,float amount,string reason){var g=God(key);if(Enabled && Relevant(g)){Gain(g,amount,reason);IdeoBridge.OnDeed(g,amount,reason);}}
  public static bool PlayerColonist(Pawn p)=>p?.Faction!=null && p.Faction.IsPlayer && p.RaceProps.Humanlike; // 世界生成时还没有玩家派系，不能碰 Faction.OfPlayer
  public static bool PlayerHome(Map m)=>m!=null && m.IsPlayerHome;
  public static IEnumerable<Map> HomeMaps=>Find.Maps.Where(m=>m.IsPlayerHome);
  public static IEnumerable<Pawn> Colonists=>Find.Maps.SelectMany(m=>m.mapPawns.FreeColonistsSpawned);

  // 小人身上的信仰
  public static bool HolyVulnerable(Pawn p)=>p!=null && (Items.Undead(p) || Fiends.Is(p) || MindFlayer.IsMindFlayer(p));
  public static bool Dark(Pawn p)=>p?.Map!=null && p.Spawned && p.Map.glowGrid.GroundGlowAt(p.Position)<.3f;
  public static bool Night(Map m){int h=m==null?GenLocalDate.HourOfDay(Find.CurrentMap?.Tile??0):GenLocalDate.HourOfDay(m);return h>=22 || h<5;}
  public static bool Storm(Map m)=>m?.weatherManager?.curWeather?.defName=="RainyThunderstorm" || m?.weatherManager?.curWeather?.defName=="DryThunderstorm";
  public static bool CanPray(Pawn p,out string reason){
   reason=null;
   if(!Enabled){reason="信仰系统已关闭。";return false;}
   if(FaithUtility.IsAtheist(p)){reason="无神者不祈祷。";return false;}
   if(FaithUtility.Patron==null){reason="殖民地还没有守护神：先在圣坛选一位。";return false;}
   if(p.Faction?.IsPlayer!=true && !Pilgrims.Is(p)){reason="只有殖民者会在这里祈祷。";return false;}
   return true;
  }
  public static IntVec3 PrayCell(Pawn p,Building altar){
   var map=altar.Map;var cells=GenAdj.CellsAdjacent8Way(altar).Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null && p.CanReach(c,PathEndMode.OnCell,Danger.Some)).ToList();
   var ic=altar.InteractionCell;if(cells.Contains(ic))return ic;
   return cells.Count==0?IntVec3.Invalid:cells.OrderBy(c=>c.DistanceToSquared(ic)).First();
  }
  public static Job PrayJobFor(Pawn p,Building_FaithAltar altar){
   if(altar==null || PrayJob==null || !CanPray(p,out _) || altar.IsForbidden(p) || !p.CanReserveAndReach(altar,PathEndMode.Touch,Danger.Some,8,-1,null,false))return null;
   var cell=PrayCell(p,altar);if(!cell.IsValid)return null;
   return JobMaker.MakeJob(PrayJob,altar,cell);
  }
  // 祈祷完成：守护神 +0.5（信徒 ×2，塔洛斯雷暴中 ×2，莎尔夜里 ×2），全殖民地每天最多 +5（倍率之前）。
  public static float PrayerReward(Pawn p){
   var god=FaithUtility.Patron;var c=Comp;if(god==null || c==null || !Enabled || FaithUtility.IsAtheist(p))return 0f;
   float g=PrayerGain;if(FaithUtility.IsDevoteeOf(p,god))g*=2f;
   g*=HeroDestiny.PrayerFactor(p); // 1.58.0 神之勇士 ×2
   g*=IdeoBridge.PrayerFactor(p); // 1.56.0 祈祷乘确信度（众神殿、神殿再 ×1.25）
   if(god.key=="Talos" && Storm(p.MapHeld))g*=2f;
   if(god.key=="Shar" && Night(p.MapHeld)){g*=2f;c.TrialProgress("Shar",1f);}
   int day=GenDate.DaysPassed;if(c.prayDay!=day){c.prayDay=day;c.prayToday=0f;}
   g=Mathf.Min(g,PrayerDailyCap*IdeoBridge.PrayerCapFactor-c.prayToday);if(g<=0f)return 0f;
   c.prayToday+=g;Gain(god,g,p.LabelShort+"祈祷");
   return g;
  }
  public static void PrayerDone(Pawn p){
   if(p==null || p.Dead)return;
   var t=IdeoBridge.PrayerThought(p)??DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_Prayed");if(t!=null && !FaithUtility.IsAtheist(p))p.needs?.mood?.thoughts?.memories?.TryGainMemory(t);
   if(p.Faction?.IsPlayer==true)PrayerReward(p);else Pilgrims.Prayed(p);
  }

  // 神迹残片：地图上合计够数才扣。
  public static int ShardsOn(Map map)=>map==null || ShardDef==null?0:map.listerThings.ThingsOfDef(ShardDef).Where(t=>!t.IsForbidden(Faction.OfPlayer) || true).Sum(t=>t.stackCount);
  public static bool TakeShards(Map map,int n){
   if(ShardsOn(map)<n)return false;
   foreach(var t in map.listerThings.ThingsOfDef(ShardDef).ToList()){if(n<=0)break;int take=Math.Min(n,t.stackCount);t.SplitOff(take).Destroy();n-=take;}
   return true;
  }
  public static Thing MakeRelic(LP_DeityDef god){
   if(god?.relic==null)return null;
   var t=ThingMaker.MakeThing(god.relic);t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary,ArtGenerationContext.Outsider);
   return t;
  }
  public static void DropNearAltar(Thing t,Map map=null){
   map=map??HomeMaps.FirstOrDefault();if(t==null || map==null)return;
   var altar=FaithUtility.Altars(map).FirstOrDefault();var cell=altar?.InteractionCell??DropCellFinder.TradeDropSpot(map);
   GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);
  }
  // 神选者
  public static int ChosenCount(LP_DeityDef god)=>PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.Count(p=>FaithUtility.IsChosen(p,out var g) && g==god);
  public static string CannotChoose(Pawn p,LP_DeityDef god){
   if(!Enabled)return "信仰系统已关闭。";
   if(god==null)return "殖民地没有守护神。";
   if(FaithUtility.GetFavor(god)<ChosenThreshold)return god.label+"的神眷不到"+ChosenThreshold+"。";
   if(p!=null){
    if(!FaithUtility.IsDevoteeOf(p,god))return p.LabelShort+"不是"+god.label+"的信徒。";
    if(FaithUtility.IsChosen(p,out _))return p.LabelShort+"已经是神选者了。";
   }
   if(ChosenCount(god)>=ChosenPerGod)return god.label+"在殖民地已经有"+ChosenPerGod+"名神选者了。";
   if(FaithUtility.GetFavor(god)<ChosenFavorCost)return "神眷不足"+ChosenFavorCost+"。";
   var map=p?.MapHeld??Find.CurrentMap;if(ShardsOn(map)<ChosenShards)return "地图上的神迹残片不足"+ChosenShards+"片。";
   return null;
  }
  public static bool CompleteChosen(Pawn p,bool convert){
   var god=FaithUtility.Patron;if(p==null || p.Dead || CannotChoose(p,god)!=null)return false;
   if(!TakeShards(p.MapHeld,ChosenShards))return false;
   FaithUtility.AddFavor(god,-ChosenFavorCost,p.LabelShort+"的神选仪式");
   var h=(Hediff_Chosen)HediffMaker.MakeHediff(ChosenDef,p);h.deity=god;p.health.AddHediff(h);
   string extra="";
   if(convert){var x=FaithRaces.ConversionFor(god);if(x!=null && FaithRaces.CannotConvert(p)==null && FaithRaces.Convert(p,x))extra="\n\n"+p.LabelShort+"的血脉被神力重塑，成为了"+x.label+"。";}
   if(p.Spawned)FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,2.5f);
   IdeoBridge.OnChosen(p);
   Find.LetterStack.ReceiveLetter("神选者："+p.LabelShort,p.LabelShort+"在圣坛前被"+god.FullLabel+"选中，成为了神选者：神恩强度+0.25，神术消耗减半，心智抗性+20%。"+extra,LetterDefOf.PositiveEvent,p);
   return true;
  }
 }

 // ==================== 给成神线程的接口（需求第八节，名字固定） ====================
 public static class FaithUtility {
  public static event Action<LP_DeityDef,float,string> FavorChanged;
  static List<LP_DeityDef> all;
  public static List<LP_DeityDef> AllDeities=>all??(all=DefDatabase<LP_DeityDef>.AllDefsListForReading.OrderBy(d=>d.devoteeDegree<=0?99:d.devoteeDegree).ToList());
  public static IEnumerable<LP_DeityDef> Worshippable=>AllDeities.Where(d=>d.worshippable);
  public static float GetFavor(LP_DeityDef god){var c=Faith.Comp;return god==null || c==null?0f:c.Get(god);}
  // 夹到 -100～100；reason 进日志和圣坛上的历史记录。
  public static void AddFavor(LP_DeityDef god,float amount,string reason){
   var c=Faith.Comp;if(god==null || c==null || amount==0f)return;
   float before=c.Get(god),after=Mathf.Clamp(before+amount,Faith.Min,Faith.Max);float delta=after-before;
   c.favor[god]=after;c.Log(god,delta,reason);
   if(Prefs.DevMode)Log.Message("[DND边缘大陆] 神眷 "+god.label+" "+(delta>=0?"+":"")+delta.ToString("0.##")+"（"+reason+"）→ "+after.ToString("0.##"));
   c.Notify_Changed(god,before,after);
   try{FavorChanged?.Invoke(god,delta,reason);}catch(Exception e){Log.Error("[DND边缘大陆] FavorChanged 订阅者出错："+e);}
  }
  public static LP_DeityDef Patron=>Faith.Comp?.patron;
  public static void SetPatron(LP_DeityDef god,bool ignoreCooldown=false){var c=Faith.Comp;if(c==null)return;if(!ignoreCooldown && CannotSetPatron(god)!=null)return;c.SetPatron(god);}
  public static string CannotSetPatron(LP_DeityDef god){
   var c=Faith.Comp;if(c==null)return "没有世界";
   if(!Faith.Enabled)return "信仰系统已关闭。";
   if(god==null)return null;
   var locked=IdeoBridge.PatronLockReason(god);if(locked!=null)return locked; // 1.56.0 文化锁定守护神
   if(!god.worshippable)return god.label+"不接受凡人的信奉。";
   if(c.patron==god)return god.label+"已经是守护神了。";
   if(c.patron!=null && Faith.Now<c.patronCooldownUntil)return "刚换过守护神，还要等"+((c.patronCooldownUntil-Faith.Now)/(float)GenDate.TicksPerDay).ToString("0.#")+"天。";
   return null;
  }
  public static bool IsDevoteeOf(Pawn p,LP_DeityDef god)=>god!=null && DevoteeGod(p)==god;
  public static LP_DeityDef DevoteeGod(Pawn p){var t=p?.story?.traits?.GetTrait(Faith.Devotee);return t==null?null:DegreeToDeity(t.Degree);}
  public static bool IsAtheist(Pawn p)=>Faith.Atheist!=null && p?.story?.traits?.HasTrait(Faith.Atheist)==true;
  public static bool IsChosen(Pawn p,out LP_DeityDef god){god=null;var list=p?.health?.hediffSet?.hediffs;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i] is Hediff_Chosen h){god=h.deity;return true;}return false;}
  public static IEnumerable<Building> Altars(Map map)=>map==null || Faith.AltarDef==null?Enumerable.Empty<Building>():map.listerThings.ThingsOfDef(Faith.AltarDef).OfType<Building>().Where(b=>b.Faction?.IsPlayer==true);
  public static LP_DeityDef DegreeToDeity(int degree){var l=AllDeities;for(int i=0;i<l.Count;i++)if(l[i].devoteeDegree==degree)return l[i];return null;} // 1.58.6：不用 LINQ 闭包
  public static int DeityToDegree(LP_DeityDef god)=>god?.devoteeDegree??0;
  // 神术消耗：信徒（信守护神）×0.5，神选者 ×0.5，最多叠到 ×0.25。外部可以给它挂后缀。
  public static float MiracleCost(Pawn p,AbilityDef def){
   var ext=def?.comps?.OfType<CompProperties_Miracle>().FirstOrDefault();if(ext==null)return 0f;
   float f=1f;var god=Patron;
   if(god!=null && IsDevoteeOf(p,god))f*=.5f;
   if(IsChosen(p,out _))f*=.5f;
   f*=IdeoBridge.MiracleCostFactor(p); // 1.56.0 神官 ×0.75
   return ext.cost*Mathf.Max(.25f,f);
  }
 }

 public class FavorEntry : IExposable {
  public int tick;public LP_DeityDef god;public float amount;public string reason;
  public void ExposeData(){Scribe_Values.Look(ref tick,"tick");Scribe_Defs.Look(ref god,"god");Scribe_Values.Look(ref amount,"amount");Scribe_Values.Look(ref reason,"reason");}
 }

 // ==================== 存档组件 ====================
 public class WorldComponent_Faith : WorldComponent {
  public Dictionary<LP_DeityDef,float> favor=new Dictionary<LP_DeityDef,float>();
  public LP_DeityDef patron;public int patronCooldownUntil=-1;
  public Dictionary<LP_DeityDef,int> lastManifest=new Dictionary<LP_DeityDef,int>(); // 每位神上次季度显圣的时刻
  public int nextManifestTick=-1;
  public LP_DeityDef manifestGod;public bool manifestBlessing;public int manifestUntil=-1;public bool kelemvorSaveUsed;
  public Dictionary<string,int> questCooldown=new Dictionary<string,int>();
  public List<LP_DeityDef> relicGranted=new List<LP_DeityDef>();
  public int prayDay=-1;public float prayToday;
  public int talosDay=-1;public float talosToday;
  public int lastDay=-1;
  public List<FavorEntry> history=new List<FavorEntry>();
  // 神谕试炼
  public LP_DeityDef trialGod;public int trialUntil=-1;public float trialProgress,trialTarget;
  // 寻回圣物
  public LP_DeityDef lostRelicGod;public Thing lostRelic;public int lostRelicUntil=-1;public WorldObject lostRelicSite;
  // 亵渎者
  public Lord desecrators;public Map desecratorMap;public int desecratorStart=-1;
  // 安息的尸体、灰烬天平的申诉（按小人 ID）
  public List<string> rested=new List<string>();
  public Dictionary<string,int> appeals=new Dictionary<string,int>();
  public WorldComponent_Faith(World world):base(world){}
  public float Get(LP_DeityDef god)=>god!=null && favor.TryGetValue(god,out float v)?v:0f;
  public void Log(LP_DeityDef god,float amount,string reason){
   if(Mathf.Abs(amount)<.001f)return;
   history.Add(new FavorEntry{tick=Faith.Now,god=god,amount=amount,reason=reason??""});
   if(history.Count>Faith.HistoryKeep)history.RemoveRange(0,history.Count-Faith.HistoryKeep);
  }
  public void Notify_Changed(LP_DeityDef god,float before,float after){
   // 守护神神眷第一次到 80：赐一件遗物（一神一次）。
   if(god==patron && before<Faith.RelicThreshold && after>=Faith.RelicThreshold && !relicGranted.Contains(god) && god.relic!=null){
    relicGranted.Add(god);var r=Faith.MakeRelic(god);var map=Faith.HomeMaps.FirstOrDefault();
    if(r!=null && map!=null){Faith.DropNearAltar(r,map);Find.LetterStack.ReceiveLetter("神赐遗物："+r.LabelNoCount,god.FullLabel+"对殖民地的虔诚很满意，在圣坛旁降下了"+r.LabelNoCount+"。\n\n"+r.def.description,LetterDefOf.PositiveEvent,r);}
   }
  }
  // 1.56.0：penalty -1 = 原来的 Faith.ChangePenalty（文化同步改奉时传 15）；cooldown=false 时不设冷却；众神殿文化冷却减半（IdeoBridge.PatronCooldownFactor）。
  public void SetPatron(LP_DeityDef god,float penalty=-1f,bool cooldown=true){
   var old=patron;if(old==god)return;
   patron=god;
   if(old!=null){FaithUtility.AddFavor(old,-(penalty<0f?Faith.ChangePenalty:penalty),"殖民地改奉"+(god?.label??"无"));if(cooldown)patronCooldownUntil=Faith.Now+(int)(Faith.ChangeCooldownDays*GenDate.TicksPerDay*IdeoBridge.PatronCooldownFactor);}
   if(nextManifestTick<0)nextManifestTick=Faith.Now+Faith.ManifestDays*GenDate.TicksPerDay;
   FaithAltarUtility.RefreshAll();
   Miracles.SyncAll();
  }
  public void TrialProgress(string key,float amount){if(trialGod==null || trialGod.key!=key || trialUntil<Faith.Now)return;trialProgress+=amount;if(trialProgress>=trialTarget)FaithQuests.TrialSucceeded(this);}
  public bool Blessed(string key)=>manifestGod!=null && manifestBlessing && manifestGod.key==key && Faith.Now<manifestUntil && Faith.Enabled;
  public bool Cursed(string key)=>manifestGod!=null && !manifestBlessing && manifestGod.key==key && Faith.Now<manifestUntil && Faith.Enabled;
  public override void WorldComponentTick(){
   int now=Find.TickManager.TicksGame;if(now%GenDate.TicksPerHour!=419)return;
   if(!Faith.Enabled)return;
   FaithQuests.HourlyCheck(this);
   int day=GenDate.DaysPassed;if(day==lastDay)return;
   bool first=lastDay<0;lastDay=day;if(first)return;
   Daily();
  }
  public void Daily(){
   foreach(var god in FaithUtility.AllDeities){
    float v=Get(god);
    if(god!=patron && v!=0f)FaithUtility.AddFavor(god,v>0?-Mathf.Min(v,Faith.DailyDecay):Mathf.Min(-v,Faith.DailyDecay),"每天回落");
   }
   if(patron!=null){
    foreach(var e in patron.Enemies.ToList())FaithUtility.AddFavor(e,-Faith.EnemyDaily,"殖民地侍奉"+patron.label);
    // 洛山达：每个死灵造物每天 -0.2；凯兰沃：殖民地有巫妖每天 -0.5。
    if(Faith.Relevant(Faith.God("Lathander"))){int n=Necro.Registry?.constructs.Count(p=>p!=null && !p.Dead && p.Faction?.IsPlayer==true)??0;if(n>0)FaithUtility.AddFavor(Faith.God("Lathander"),-Faith.ConstructDaily*n,n+"个死灵造物");}
    if(Faith.Relevant(Faith.God("Kelemvor")) && PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction.Any(Items.IsLich))FaithUtility.AddFavor(Faith.God("Kelemvor"),-Faith.LichDaily,"殖民地里有巫妖");
   }
   FaithQuests.DailyCheck(this);
   IdeoBridge.Daily(); // 1.56.0 文化：确信度漂移、神像、圣物柜
   if(nextManifestTick>=0 && Faith.Now>=nextManifestTick){nextManifestTick=Faith.Now+Faith.ManifestDays*GenDate.TicksPerDay;FaithManifest.Quarterly(this);}
  }
  public override void ExposeData(){
   base.ExposeData();
   if(Scribe.mode==LoadSaveMode.Saving){favor=favor.Where(kv=>kv.Key!=null).ToDictionary(kv=>kv.Key,kv=>kv.Value);lastManifest=lastManifest.Where(kv=>kv.Key!=null).ToDictionary(kv=>kv.Key,kv=>kv.Value);}
   Scribe_Collections.Look(ref favor,"lpFavor",LookMode.Def,LookMode.Value);
   Scribe_Defs.Look(ref patron,"lpPatron");Scribe_Values.Look(ref patronCooldownUntil,"lpPatronCooldown",-1);
   Scribe_Collections.Look(ref lastManifest,"lpLastManifest",LookMode.Def,LookMode.Value);
   Scribe_Values.Look(ref nextManifestTick,"lpNextManifest",-1);
   Scribe_Defs.Look(ref manifestGod,"lpManifestGod");Scribe_Values.Look(ref manifestBlessing,"lpManifestBlessing");Scribe_Values.Look(ref manifestUntil,"lpManifestUntil",-1);Scribe_Values.Look(ref kelemvorSaveUsed,"lpKelemvorSave");
   Scribe_Collections.Look(ref questCooldown,"lpQuestCooldown",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref relicGranted,"lpRelicGranted",LookMode.Def);
   Scribe_Values.Look(ref prayDay,"lpPrayDay",-1);Scribe_Values.Look(ref prayToday,"lpPrayToday");
   Scribe_Values.Look(ref talosDay,"lpTalosDay",-1);Scribe_Values.Look(ref talosToday,"lpTalosToday");
   Scribe_Values.Look(ref lastDay,"lpLastDay",-1);
   Scribe_Collections.Look(ref history,"lpFavorHistory",LookMode.Deep);
   Scribe_Defs.Look(ref trialGod,"lpTrialGod");Scribe_Values.Look(ref trialUntil,"lpTrialUntil",-1);Scribe_Values.Look(ref trialProgress,"lpTrialProgress");Scribe_Values.Look(ref trialTarget,"lpTrialTarget");
   Scribe_Defs.Look(ref lostRelicGod,"lpLostRelicGod");Scribe_References.Look(ref lostRelic,"lpLostRelic");Scribe_Values.Look(ref lostRelicUntil,"lpLostRelicUntil",-1);Scribe_References.Look(ref lostRelicSite,"lpLostRelicSite");
   Scribe_References.Look(ref desecrators,"lpDesecrators");Scribe_References.Look(ref desecratorMap,"lpDesecratorMap");Scribe_Values.Look(ref desecratorStart,"lpDesecratorStart",-1);
   Scribe_Collections.Look(ref rested,"lpRested",LookMode.Value);
   Scribe_Collections.Look(ref appeals,"lpAppeals",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    favor=favor?.Where(kv=>kv.Key!=null).ToDictionary(kv=>kv.Key,kv=>kv.Value)??new Dictionary<LP_DeityDef,float>();
    lastManifest=lastManifest?.Where(kv=>kv.Key!=null).ToDictionary(kv=>kv.Key,kv=>kv.Value)??new Dictionary<LP_DeityDef,int>();
    if(questCooldown==null)questCooldown=new Dictionary<string,int>();if(relicGranted==null)relicGranted=new List<LP_DeityDef>();relicGranted.RemoveAll(d=>d==null);
    history=history?.Where(h=>h?.god!=null).ToList()??new List<FavorEntry>();
    if(rested==null)rested=new List<string>();if(appeals==null)appeals=new Dictionary<string,int>();
   }
  }
 }

 // ==================== 圣坛 ====================
 public class Building_FaithAltar : Building_WorkTable {
  // 外部（成神线程）在这里登记额外的圣坛按钮。
  public static readonly List<Func<Building_FaithAltar,IEnumerable<Gizmo>>> ExtraGizmos=new List<Func<Building_FaithAltar,IEnumerable<Gizmo>>>();
  public override void SpawnSetup(Map map,bool respawningAfterLoad){base.SpawnSetup(map,respawningAfterLoad);RefreshGlow();Offerings.RefreshLiked();}
  public void RefreshGlow(){
   var g=GetComp<CompGlower>();if(g==null)return;var god=FaithUtility.Patron;
   var c=god==null?new Color(.86f,.78f,.6f):god.color;
   try{g.GlowColor=new ColorInt(c);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 圣坛发光："+e.Message,8814211);}
  }
  public override string GetInspectString(){
   var s=base.GetInspectString();var god=FaithUtility.Patron;
   string line=!Faith.Enabled?"信仰系统已关闭":god==null?"守护神：未选择":"守护神："+god.FullLabel+"；神眷 "+FaithUtility.GetFavor(god).ToString("0.#");
   return s.NullOrEmpty()?line:s+"\n"+line;
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(Faction!=Faction.OfPlayer)yield break;
   var patron=FaithUtility.Patron;
   yield return new Command_Action{defaultLabel="选择守护神",defaultDesc="选一位殖民地的守护神（一个殖民地一位）。更换时原来的神神眷-30，之后"+Faith.ChangeCooldownDays+"天内不能再换。\n\n当前："+(patron?.FullLabel??"无"),icon=patron?.Icon??Faith.AltarDef?.uiIcon??BaseContent.BadTex,action=()=>Find.WindowStack.Add(new FloatMenu(FaithUtility.Worshippable.Select(d=>{string why=FaithUtility.CannotSetPatron(d);return new FloatMenuOption(d.FullLabel+" · "+d.AlignmentLabel+" · 神眷 "+FaithUtility.GetFavor(d).ToString("0.#")+(why==null?"":"（"+why+"）"),why==null?(Action)(()=>FaithUtility.SetPatron(d)):null,d.Icon,d.color);}).ToList()))};
   yield return new Command_Action{defaultLabel="神眷一览",defaultDesc=FaithAltarUtility.FavorTable(),icon=FaithAltarUtility.OverviewIcon,action=()=>Find.WindowStack.Add(new Dialog_MessageBox(FaithAltarUtility.FavorTable()+"\n\n"+FaithAltarUtility.HistoryText()))};
   string why2=Faith.CannotChoose(null,patron);
   var ritual=new Command_Action{defaultLabel="神选仪式",defaultDesc="守护神神眷≥"+Faith.ChosenThreshold+"时，为一名信奉守护神的信徒举行神选仪式（2小时，可打断，打断不扣资源）：消耗神眷"+Faith.ChosenFavorCost+"和"+Faith.ChosenShards+"片神迹残片。神选者神恩强度+0.25、神术消耗减半、心智抗性+20%；每位神在殖民地最多"+Faith.ChosenPerGod+"名。研究“天界与地狱血统”后可以顺带改换血脉（善神→阿斯莫，恶神→提夫林）。",icon=FaithAltarUtility.ChosenIcon,action=()=>FaithAltarUtility.ChooseRitualPawn(this)};
   if(why2!=null)ritual.Disable(why2);
   yield return ritual;
   foreach(var f in ExtraGizmos){IEnumerable<Gizmo> list=null;try{list=f(this)?.ToList();}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 圣坛外部按钮出错："+e,8814212);}if(list!=null)foreach(var g in list)yield return g;}
  }
  public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn){
   foreach(var o in base.GetFloatMenuOptions(selPawn))yield return o;
   if(!Faith.CanPray(selPawn,out string why)){yield return new FloatMenuOption("在圣坛祈祷（"+why+"）",null);yield break;}
   var job=Faith.PrayJobFor(selPawn,this);
   yield return job==null?new FloatMenuOption("在圣坛祈祷（到不了圣坛旁边）",null):new FloatMenuOption("在圣坛祈祷（1小时）",()=>selPawn.jobs.TryTakeOrderedJob(job,JobTag.Misc));
  }
  public override void Destroy(DestroyMode mode=DestroyMode.Vanish){
   var map=Map;base.Destroy(mode);
   if(mode==DestroyMode.KillFinalize)FaithQuests.AltarDestroyed(map);
  }
 }
 public static class FaithAltarUtility {
  public static Texture2D OverviewIcon=>ContentFinder<Texture2D>.Get("Faith/HolySymbol",false)??BaseContent.BadTex;
  public static Texture2D ChosenIcon=>ContentFinder<Texture2D>.Get("Faith/Miracles/Sanctuary",false)??BaseContent.BadTex;
  public static void RefreshAll(){foreach(var m in Find.Maps)foreach(var a in FaithUtility.Altars(m).OfType<Building_FaithAltar>())a.RefreshGlow();Offerings.RefreshLiked();}
  public static string FavorTable(){
   var sb=new System.Text.StringBuilder();var patron=FaithUtility.Patron;
   sb.AppendLine("各神神眷（-100～100）：");
   foreach(var d in FaithUtility.AllDeities)sb.AppendLine((d==patron?"★ ":"   ")+d.FullLabel+"："+FaithUtility.GetFavor(d).ToString("0.#")+(d.worshippable?"":"（不接受信奉）")+(patron!=null && patron.EnemyOf(d)?"（敌对）":""));
   sb.AppendLine();
   sb.AppendLine("守护神阈值：≥"+Faith.DomainThreshold+" 可施放领域神术；≥"+Faith.BlessingThreshold+" 季度赐福；≥"+Faith.ChosenThreshold+" 可做神选仪式（第一次到"+Faith.RelicThreshold+"赐一件遗物）；≤"+Faith.CurseThreshold+" 季度降罚。任何神≤"+Faith.ForsakenThreshold+"时它的信徒心情-6。");
   sb.AppendLine("非守护神每天向0回落"+Faith.DailyDecay+"；守护神的敌对神每天-"+Faith.EnemyDaily+"。");
   if(patron!=null)sb.Append(patron.label+"喜爱的祭品："+patron.LikedLabel);
   return sb.ToString().TrimEnd();
  }
  public static string HistoryText(){
   var c=Faith.Comp;if(c==null || c.history.Count==0)return "（还没有神眷变化记录）";
   return "最近的变化：\n"+string.Join("\n",c.history.AsEnumerable().Reverse().Take(15).Select(h=>GenDate.ToStringTicksToPeriod(Math.Max(0,Faith.Now-h.tick))+"前 · "+h.god.label+" "+(h.amount>=0?"+":"")+h.amount.ToString("0.##")+" · "+h.reason));
  }
  public static void ChooseRitualPawn(Building_FaithAltar altar){
   var god=FaithUtility.Patron;
   var opts=altar.Map.mapPawns.FreeColonistsSpawned.Where(p=>FaithUtility.IsDevoteeOf(p,god)).Select(p=>{string why=Faith.CannotChoose(p,god);return new FloatMenuOption(p.LabelShort+(why==null?"":"（"+why+"）"),why==null?(Action)(()=>Find.WindowStack.Add(new Dialog_ChosenRitual(p,altar))):null);}).ToList();
   if(opts.Count==0)opts.Add(new FloatMenuOption("地图上没有"+god?.label+"的信徒",null));
   Find.WindowStack.Add(new FloatMenu(opts));
  }
 }
 public class Dialog_ChosenRitual : Window {
  readonly Pawn pawn;readonly Building_FaithAltar altar;bool convert=true;
  public override Vector2 InitialSize=>new Vector2(520f,300f);
  public Dialog_ChosenRitual(Pawn p,Building_FaithAltar a){pawn=p;altar=a;forcePause=true;doCloseX=true;absorbInputAroundWindow=true;}
  public override void DoWindowContents(Rect r){
   var god=FaithUtility.Patron;var x=FaithRaces.ConversionFor(god);string cannot=FaithRaces.ConversionBlocked(pawn,god);
   var l=new Listing_Standard();l.Begin(r);
   l.Label("为"+pawn.LabelShort+"举行神选仪式（"+god?.FullLabel+"）：消耗神眷"+Faith.ChosenFavorCost+"和"+Faith.ChosenShards+"片神迹残片，2小时，可打断（打断不扣资源）。");
   l.Gap();
   if(cannot==null){l.CheckboxLabeled("同时改换血脉为"+x.label+"（只替换异种基因，内生基因保留）",ref convert);}
   else{l.Label("改换血脉：不可用（"+cannot+"）");convert=false;}
   l.Gap();
   if(l.ButtonText("开始仪式")){
    var cell=Faith.PrayCell(pawn,altar);
    if(cell.IsValid && Faith.RitualJob!=null){var job=JobMaker.MakeJob(Faith.RitualJob,altar,cell);job.count=convert?1:0;pawn.jobs.TryTakeOrderedJob(job,JobTag.Misc);}
    else Messages.Message(pawn.LabelShort+"到不了圣坛旁边。",MessageTypeDefOf.RejectInput,false);
    Close();
   }
   l.End();
  }
 }

 // ==================== 祈祷与神选仪式 ====================
 public class JobDriver_Pray : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetB,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   yield return Toils_Goto.GotoCell(TargetIndex.B,PathEndMode.OnCell);
   var pray=ToilMaker.MakeToil("LP_Pray");
   pray.defaultCompleteMode=ToilCompleteMode.Delay;pray.defaultDuration=Faith.PrayTicks;
   // 1.47.0：圣坛连着守护神像时祈祷娱乐+20%。
   float joyFactor=Workbenches.PrayJoyFactor(TargetA.Thing);
   pray.tickIntervalAction=delta=>{pawn.rotationTracker.FaceTarget(TargetA);if(pawn.needs?.joy!=null)JoyUtility.JoyTickCheckEnd(pawn,delta,JoyTickFullJoyAction.None,joyFactor);};
   pray.WithProgressBarToilDelay(TargetIndex.B);
   pray.handlingFacing=true;
   yield return pray;
   yield return Toils_General.Do(()=>Faith.PrayerDone(pawn));
  }
 }
 public class JoyGiver_Pray : JoyGiver {
  public override Job TryGiveJob(Pawn pawn){
   if(!Faith.CanPray(pawn,out _) || pawn.Map==null)return null;
   var altar=FaithUtility.Altars(pawn.Map).OfType<Building_FaithAltar>().Where(a=>!a.IsForbidden(pawn)).OrderBy(a=>a.Position.DistanceToSquared(pawn.Position)).FirstOrDefault(a=>pawn.CanReach(a,PathEndMode.Touch,Danger.None));
   return Faith.PrayJobFor(pawn,altar);
  }
 }
 public class JobDriver_ChosenRitual : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetB,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   this.FailOn(()=>Faith.CannotChoose(pawn,FaithUtility.Patron)!=null);
   yield return Toils_Goto.GotoCell(TargetIndex.B,PathEndMode.OnCell);
   var wait=ToilMaker.MakeToil("LP_ChosenRitual");
   wait.defaultCompleteMode=ToilCompleteMode.Delay;wait.defaultDuration=Faith.ChosenTicks;
   wait.tickIntervalAction=delta=>{pawn.rotationTracker.FaceTarget(TargetA);if(pawn.IsHashIntervalTick(300,delta))FleckMaker.ThrowLightningGlow(pawn.DrawPos,pawn.Map,.8f);};
   wait.WithProgressBarToilDelay(TargetIndex.B);wait.handlingFacing=true;
   yield return wait;
   yield return Toils_General.Do(()=>Faith.CompleteChosen(pawn,job.count==1));
  }
 }

 // ==================== 献祭 ====================
 public static class Offerings {
  static RecipeDef liked;
  public static RecipeDef Liked=>liked??(liked=DefDatabase<RecipeDef>.GetNamedSilentFail("LP_Offer_Liked"));
  // 喜爱的祭品：固定过滤只放守护神喜欢的东西（换神时重建）；默认过滤放所有神喜欢的东西，新老工作单都能跟着换。
  public static void RefreshLiked(){
   var r=Liked;if(r==null)return;var god=FaithUtility.Patron;
   var fixedF=new ThingFilter();
   if(god!=null){foreach(var t in god.LikedThings)fixedF.SetAllow(t,true);foreach(var c in god.LikedCategories)fixedF.SetAllow(c,true,null,null);}
   else if(Faith.ShardDef!=null)fixedF.SetAllow(Faith.ShardDef,true);
   r.fixedIngredientFilter=fixedF;
   var ing=r.ingredients.FirstOrDefault();if(ing!=null){var f=new ThingFilter();f.CopyAllowancesFrom(fixedF);ing.filter=f;ing.ResolveReferences();}
   if(r.defaultIngredientFilter==null || god!=null){var all=new ThingFilter();foreach(var d in FaithUtility.Worshippable){foreach(var t in d.LikedThings)all.SetAllow(t,true);foreach(var c in d.LikedCategories)all.SetAllow(c,true,null,null);}r.defaultIngredientFilter=all;}
   try{Traverse.Create(r).Field("cachedBillRequirementsDescription").SetValue(null);}catch{}
  }
  // 单件原料值多少神眷（要在销毁前算：原版 Thing.Destroy 会把 stackCount 清零）。
  public static float FavorFor(RecipeDef recipe,Thing t){
   if(recipe==null || t==null)return 0f;
   switch(recipe.defName){
    case "LP_Offer_Shard":return t.def==Faith.ShardDef?Faith.ShardFavor*t.stackCount:0f;
    case "LP_Offer_Silver":return t.MarketValue*t.stackCount/Faith.SilverPerFavor;
    case "LP_Offer_Liked":return t.MarketValue*t.stackCount/Faith.SilverPerFavor*Faith.LikedFactor;
   }
   return 0f;
  }
  public static float FavorFor(RecipeDef recipe,List<Thing> ingredients)=>ingredients==null?0f:ingredients.Sum(t=>FavorFor(recipe,t));
 }
 public class RecipeWorker_Offering : RecipeWorker {
  float pending; // 本次献祭已消耗原料的神眷
  public override bool AvailableOnNow(Thing thing,BodyPartRecord part=null)=>Faith.Enabled && FaithUtility.Patron!=null && base.AvailableOnNow(thing,part);
  // 热修：原版先 ConsumeIngredients（逐个 Destroy，stackCount 归零）再 Notify_IterationCompleted，所以在消耗时记下神眷。
  public override void ConsumeIngredient(Thing ingredient,RecipeDef recipe,Map map){
   pending+=Offerings.FavorFor(recipe,ingredient);
   base.ConsumeIngredient(ingredient,recipe,map);
  }
  public override void Notify_IterationCompleted(Pawn billDoer,List<Thing> ingredients){
   base.Notify_IterationCompleted(billDoer,ingredients);
   float raw=pending;pending=0f;
   var god=FaithUtility.Patron;float f=raw*IdeoBridge.OfferingFactor;if(f>0f)IdeoBridge.OfferingDone(); // 1.56.0 供奉「每季献祭」
   if(god!=null && f>0f){Faith.Gain(god,f,billDoer?.LabelShort+"献上"+recipe.label.Replace("献上",""));if(billDoer?.Spawned==true)Items.Mote(billDoer,"+"+(f*Faith.GainFactor).ToString("0.#")+" 神眷",god.color);}
  }
 }
 public class RecipeWorker_BlessedWater : RecipeWorker {
  public override bool AvailableOnNow(Thing thing,BodyPartRecord part=null)=>Faith.Enabled && FaithUtility.Patron!=null && FaithUtility.GetFavor(FaithUtility.Patron)>=Faith.BlessedWaterCost && base.AvailableOnNow(thing,part);
 }
 public class IngredientValueGetter_MarketValue : IngredientValueGetter {
  public override float ValuePerUnitOf(ThingDef t)=>t.BaseMarketValue;
  public override string BillRequirementsDescription(RecipeDef r,IngredientCount ing)=>"市值 "+ing.GetBaseCount().ToString("0")+" 的"+(FaithUtility.Patron?.label??"守护神")+"喜爱之物";
 }

 // ==================== 祝圣圣水 ====================
 public class Projectile_BlessedWater : Projectile_Explosive {
  protected override void Explode(){
   var map=Map;var cell=Position;var inst=launcher;Destroy();
   if(map==null)return;
   BlessedWater.Burst(map,cell,inst,def.projectile.explosionRadius,DamageAmount);
  }
 }
 public static class BlessedWater {
  public const float Damage=12f;public const int TrollTicks=2500;
  // 1.9 格内：活物受 12 神圣伤害（不死者与邪魔 ×2），灭火，巨魔再生停 1 小时（火焰的一半效果是 6 小时的…这里按需求写 1 小时）。
  public static int Burst(Map map,IntVec3 center,Thing instigator,float radius=1.9f,float amount=Damage){
   int n=0;
   foreach(var c in GenRadial.RadialCellsAround(center,radius,true)){
    if(!c.InBounds(map))continue;
    foreach(var t in c.GetThingList(map).ToList()){
     if(t is Fire fire){fire.Destroy();continue;}
     if(!(t is Pawn p) || p.Dead)continue;
     if(p.IsBurning())p.GetAttachment(ThingDefOf.Fire)?.Destroy();
     float a=amount*(Faith.HolyVulnerable(p)?2f:1f);
     p.TakeDamage(new DamageInfo(Faith.Radiant,a,.2f,-1f,instigator));n++;
     if(!p.Dead)SuppressTroll(p,TrollTicks);
    }
    if(Rand.Chance(.4f))FleckMaker.ThrowMicroSparks(c.ToVector3Shifted(),map);
   }
   FleckMaker.Static(center,map,FleckDefOf.PsycastAreaEffect,radius);
   return n;
  }
  // 巨魔：再生停 ticks（不阻止复活，所以是火焰效果的一半）。阿斯莫的神圣伤害、圣水共用。
  public static bool SuppressTroll(Pawn p,int ticks){var g=Troll.Gene(p);if(g==null || !g.Active)return false;g.suppressedUntilTick=Math.Max(g.suppressedUntilTick,Faith.Now+ticks);return true;}
  public static readonly string[] Diseases={"WoundInfection","Flu","Plague","Malaria","SleepingSickness","GutWorms","MuscleParasites","LungRot","Animal_Plague","Animal_Flu"};
  public static Hediff Drink(Pawn p){
   if(p?.health==null)return null;
   var h=p.health.hediffSet.hediffs.Where(x=>Diseases.Contains(x.def.defName) || (x.def.makesSickThought && x.def.lethalSeverity>0 && x.def.tendable)).OrderByDescending(x=>x.Severity).FirstOrDefault();
   if(h!=null)h.Severity=Mathf.Max(0.001f,h.Severity*.9f);
   var t=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_DrankBlessedWater");if(t!=null)p.needs?.mood?.thoughts?.memories?.TryGainMemory(t);
   return h;
  }
 }
 public class CompUseEffect_BlessedWater : CompUseEffect {
  public override void DoEffect(Pawn usedBy){base.DoEffect(usedBy);var h=BlessedWater.Drink(usedBy);if(usedBy.Faction?.IsPlayer==true)Messages.Message(usedBy.LabelShort+"喝下了祝圣圣水"+(h==null?"。":"："+h.LabelCap+"减轻了一些。"),usedBy,MessageTypeDefOf.PositiveEvent,false);}
 }

 // ==================== 心情 ====================
 public class ThoughtWorker_ServeMyGod : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>Faith.Enabled && p.Faction?.IsPlayer==true && !FaithUtility.IsAtheist(p) && FaithUtility.Patron!=null && FaithUtility.IsDevoteeOf(p,FaithUtility.Patron);
 }
 public class ThoughtWorker_ServeEnemyGod : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){if(!Faith.Enabled || p.Faction?.IsPlayer!=true)return false;var g=FaithUtility.DevoteeGod(p);return g!=null && FaithUtility.Patron!=null && FaithUtility.Patron.EnemyOf(g);}
 }
 public class ThoughtWorker_Forsaken : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){if(!Faith.Enabled || p.Faction?.IsPlayer!=true)return false;var g=FaithUtility.DevoteeGod(p);return g!=null && FaithUtility.GetFavor(g)<=Faith.ForsakenThreshold;}
 }
 public class ThoughtWorker_DawnFavor : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.Faction?.IsPlayer==true && !FaithUtility.IsAtheist(p) && Faith.Comp?.Blessed("Lathander")==true;
 }
 public class ThoughtWorker_StormJoy : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>Faith.Enabled && FaithUtility.DevoteeGod(p)?.key=="Talos" && p.Spawned && Faith.Storm(p.Map);
 }
 public class ThoughtWorker_Consecrated : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.Faction?.IsPlayer==true && p.Spawned && !FaithUtility.IsAtheist(p) && Miracles.InConsecrated(p.Map,p.Position,out _);
 }

 // ==================== 季度显圣 ====================
 public class FaithManifest {
  public virtual string BlessText=>"";public virtual string CurseText=>"";
  public virtual void Bless(Map map){}
  public virtual void Curse(Map map){}
  public static void Quarterly(WorldComponent_Faith c){
   if(LichMod.Settings?.faithManifest==false || c.patron==null)return;
   float v=c.Get(c.patron);
   if(v>=Faith.BlessingThreshold){Start(c,c.patron,true);IdeoBridge.OnManifest(true);}
   else if(v<=Faith.CurseThreshold){Start(c,c.patron,false);IdeoBridge.OnManifest(false);}
  }
  public static void Start(WorldComponent_Faith c,LP_DeityDef god,bool blessing){
   if(god==null)return;
   c.manifestGod=god;c.manifestBlessing=blessing;c.manifestUntil=Faith.Now+(blessing?Faith.BlessingDays:Faith.CurseDays)*GenDate.TicksPerDay;c.kelemvorSaveUsed=false;c.lastManifest[god]=Faith.Now;
   var w=blessing?god.BlessingWorker:god.CurseWorker;
   foreach(var map in Faith.HomeMaps.ToList()){
    if(Faith.ManifestDef!=null){var cond=(GameCondition_FaithManifest)GameConditionMaker.MakeCondition(Faith.ManifestDef,c.manifestUntil-Faith.Now);cond.god=god;cond.blessing=blessing;map.gameConditionManager.RegisterCondition(cond);}
    try{if(blessing)w?.Bless(map);else w?.Curse(map);}catch(Exception e){Log.Error("[DND边缘大陆] 季度显圣出错："+e);}
   }
   string text=blessing?w?.BlessText:w?.CurseText;
   Find.LetterStack.ReceiveLetter((blessing?"赐福：":"降罚：")+god.label,god.FullLabel+(blessing?"对殖民地的虔诚很满意，降下赐福（"+Faith.BlessingDays+"天）：\n\n":"对殖民地很不满，降下惩罚（"+Faith.CurseDays+"天）：\n\n")+text,blessing?LetterDefOf.PositiveEvent:LetterDefOf.NegativeEvent);
  }
 }
 public class GameCondition_FaithManifest : GameCondition {
  public LP_DeityDef god;public bool blessing;
  public override string Label=>(blessing?"赐福：":"降罚：")+(god?.label??"神");
  public override string Description=>(blessing?god?.BlessingWorker?.BlessText:god?.CurseWorker?.CurseText)??base.Description;
  public override int TransitionTicks=>1500;
  public override float SkyTargetLerpFactor(Map map)=>GameConditionUtility.LerpInOutValue(this,TransitionTicks,blessing?.25f:.4f);
  public override SkyTarget? SkyTarget(Map map){var c=god?.color??Color.white;var s=new SkyColorSet(Color.Lerp(Color.white,c,.6f),Color.Lerp(new Color(.4f,.4f,.4f),c*.5f,.5f),Color.Lerp(Color.white,c,.4f),blessing?1f:.75f);return new SkyTarget(blessing?.95f:.6f,s,1f,1f);}
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref god,"lpGod");Scribe_Values.Look(ref blessing,"lpBlessing");}
 }
 public class Manifest_Lathander : FaithManifest {
  public override string BlessText=>"作物生长+25%，殖民者心情+4（黎明的眷顾）。";public override string CurseText=>"作物生长-25%。";
 }
 public class Manifest_Tyr : FaithManifest {
  public override string BlessText=>"招募时囚犯抗拒下降×1.5。";public override string CurseText=>"罪行最多的殖民者良心不安（心情-8，1天）。";
  public override void Curse(Map map){var p=map.mapPawns.FreeColonistsSpawned.OrderByDescending(TwilightSkills.Crimes).FirstOrDefault();var t=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_GuiltyConscience");if(p!=null && t!=null)p.needs?.mood?.thoughts?.memories?.TryGainMemory(t);}
 }
 public class Manifest_Mystra : FaithManifest {
  public override string BlessText=>"殖民者心灵敏感度+10%，精神力恢复（冥想）+20%。";public override string CurseText=>"一名施法者的精神力被抽空。";
  public override void Curse(Map map){var p=map.mapPawns.FreeColonistsSpawned.Where(x=>x.psychicEntropy?.Psylink!=null && x.psychicEntropy.CurrentPsyfocus>0f).RandomElementWithFallback();if(p!=null){p.psychicEntropy.OffsetPsyfocusDirectly(-p.psychicEntropy.CurrentPsyfocus);Items.Mote(p,"精神力清空",new Color(.55f,.7f,1f));}}
 }
 public class Manifest_Kelemvor : FaithManifest {
  public override string BlessText=>"看到尸体不会难受；殖民者受到致命伤时有25%的几率免于一死（这次显圣里最多一次）。";public override string CurseText=>"地图上2～3具尸体消失了。";
  public override void Curse(Map map){foreach(var c in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Where(c=>c.Spawned).InRandomOrder().Take(Rand.RangeInclusive(2,3)).ToList()){FleckMaker.ThrowSmoke(c.DrawPos,map,1.2f);c.Destroy();}}
 }
 public class Manifest_Malar : FaithManifest {
  public override string BlessText=>"打猎时命中+15%，驯兽成功率×1.5。";public override string CurseText=>"两只野兽发狂。";
  public override void Curse(Map map){foreach(var a in map.mapPawns.AllPawnsSpawned.Where(a=>a.RaceProps.Animal && a.Faction==null && !a.Dead && !a.Downed && !a.InMentalState).InRandomOrder().Take(2).ToList())a.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Manhunter,"马拉尔的惩罚",true);}
 }
 public class Manifest_Talos : FaithManifest {
  public override string BlessText=>"雷暴里殖民地的建筑不会被雷劈。";public override string CurseText=>"立刻来一场雷暴，落雷专挑殖民地附近。";
  public override void Curse(Map map){TwilightSkills.Storm(map);for(int i=0;i<3;i++){var b=map.listerBuildings.allBuildingsColonist.RandomElementWithFallback();if(b==null)break;if(CellFinder.TryFindRandomCellNear(b.Position,map,4,c=>!c.Roofed(map),out var cell))map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map,cell));}}
 }
 public class Manifest_Bane : FaithManifest {
  public override string BlessText=>"奴隶的压制下降速度×0.5。";public override string CurseText=>"一名奴隶开始反抗。";
  public override void Curse(Map map){if(!ModsConfig.IdeologyActive)return;var s=map.mapPawns.SlavesOfColonySpawned.RandomElementWithFallback();if(s!=null)try{SlaveRebellionUtility.StartSlaveRebellion(s,true);}catch(Exception e){Log.Warning("[DND边缘大陆] 班恩降罚：奴隶反抗失败 "+e.Message);}}
 }
 public class Manifest_Shar : FaithManifest {
  public override string BlessText=>"在黑暗中不会难受；夜里（22点～5点）工作速度+10%。";public override string CurseText=>"一名殖民者失去了一段非血缘的关系。";
  public override void Curse(Map map){
   foreach(var p in map.mapPawns.FreeColonistsSpawned.InRandomOrder()){
    var rel=p.relations?.DirectRelations.Where(r=>r.otherPawn!=null && !r.def.familyByBloodRelation).RandomElementWithFallback();
    if(rel!=null){p.relations.RemoveDirectRelation(rel);Messages.Message("莎尔的惩罚："+p.LabelShort+"和"+rel.otherPawn.LabelShort+"的"+rel.def.label+"关系被遗忘了。",p,MessageTypeDefOf.NegativeEvent,false);return;}
   }
  }
 }
 public class Manifest_Surtr : FaithManifest {
  public override string BlessText=>"锻造速度+25%，锻炉燃料消耗×0.5。";public override string CurseText=>"殖民地的一处建筑起火。";
  public override void Curse(Map map){var b=map.listerBuildings.allBuildingsColonist.Where(x=>x.def.useHitPoints && x.GetStatValue(StatDefOf.Flammability)>0f).RandomElementWithFallback();if(b!=null)FireUtility.TryStartFireIn(b.Position,map,.5f,null);}
 }

 // ==================== 神选者 ====================
 public class Hediff_Chosen : HediffWithComps {
  public LP_DeityDef deity;
  public override string Label=>"神选者（"+(deity?.label??"?")+"）";
  public override string LabelBase=>Label;
  public override Color LabelColor=>deity?.color??base.LabelColor;
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);pawn.Drawer?.renderer?.SetAllGraphicsDirty();}
  public override void ExposeData(){base.ExposeData();Scribe_Defs.Look(ref deity,"lpDeity");}
 }
 public class PawnRenderNode_ChosenGlow : PawnRenderNode {
  public PawnRenderNode_ChosenGlow(Pawn pawn,PawnRenderNodeProperties props,PawnRenderTree tree):base(pawn,props,tree){}
  public override Color ColorFor(Pawn pawn){FaithUtility.IsChosen(pawn,out var g);var c=g?.color??Color.white;c.a=.35f;return c;}
 }

 // ==================== 属性（信徒小被动、遗物、赐福） ====================
 public class StatPart_Faith : StatPart {
  public string kind;
  static HashSet<ThingDef> smithingBenches;
  // 锻造台 = 由“锻造”工作类型的工作单给出者（原版锻造台、电锻造台、熔火锻炉……）。原版没有“锻造速度”属性，锻造加成挂在全局工作速度上、只在锻造时生效。
  public static bool SmithingBench(ThingDef d){
   if(smithingBenches==null)smithingBenches=new HashSet<ThingDef>(DefDatabase<WorkGiverDef>.AllDefs.Where(w=>w.workType?.defName=="Smithing" && w.fixedBillGiverDefs!=null).SelectMany(w=>w.fixedBillGiverDefs));
   return d!=null && smithingBenches.Contains(d);
  }
  public static bool Smithing(Pawn p){var t=p?.CurJob?.bill?.billStack?.billGiver as Thing;return t!=null && SmithingBench(t.def);}
  public static bool HuntingJob(Pawn p)=>p?.CurJobDef==JobDefOf.Hunt;
  float Factor(Pawn p,out string why){
   why=null;float f=1f;var c=Faith.Comp;if(p==null || !Faith.Enabled)return 1f;
   // 1.58.6：信徒的神只在 work / aim 分支里用到，挪进去再算；圣物用静态 DefRef。
   switch(kind){
    case "work":
     if(Items.Worn(p,MorningStar.Def)!=null || p.equipment?.Primary!=null && p.equipment.Primary.def==MorningStar.Def){int h=p.MapHeld==null?-1:GenLocalDate.HourOfDay(p.MapHeld);if(h>=6 && h<9){f*=1.05f;why="晨星（清晨）×1.05";}}
     if(c!=null && c.Blessed("Shar") && p.Faction?.IsPlayer==true && Faith.Night(p.MapHeld)){f*=1.1f;why=(why==null?"":why+"；")+"莎尔的赐福（夜里）×1.1";}
     if(Smithing(p)){
      if(FaithUtility.DevoteeGod(p)?.key=="Surtr"){f*=1.1f;why=(why==null?"":why+"；")+"苏尔特信徒（锻造）×1.1";}
      if(Items.Worn(p,ForgeHeart.Def)!=null){f*=1.3f;why=(why==null?"":why+"；")+"熔炉之心（锻造）×1.3";}
      if(c!=null && c.Blessed("Surtr") && p.Faction?.IsPlayer==true){f*=1.25f;why=(why==null?"":why+"；")+"苏尔特的赐福（锻造）×1.25";}
     }
     break;
    case "sensitivity":
     if(c!=null && c.Blessed("Mystra") && p.Faction?.IsPlayer==true){f*=1.1f;why="密斯特拉的赐福×1.1";}
     break;
    case "focus":
     if(c!=null && c.Blessed("Mystra") && p.Faction?.IsPlayer==true){f*=1.2f;why="密斯特拉的赐福×1.2";}
     if(Items.Has(p,InfernalPact.Def)){f*=1.2f;why=(why==null?"":why+"；")+"魔鬼契约×1.2";}
     break;
    case "aim":
     if(HuntingJob(p)){if(FaithUtility.DevoteeGod(p)?.key=="Malar"){f*=1.1f;why="马拉尔信徒（打猎）×1.1";}if(c!=null && c.Blessed("Malar") && p.Faction?.IsPlayer==true){f*=1.15f;why=(why==null?"":why+"；")+"马拉尔的赐福（打猎）×1.15";}}
     break;
    case "tame":
     if(c!=null && c.Blessed("Malar") && p.Faction?.IsPlayer==true){f*=1.5f;why="马拉尔的赐福×1.5";}
     break;
    case "dodge":
     if(Items.Worn(p,NightTear.Def)!=null && Faith.Dark(p))return 1f; // 闪避是加法，见 Offset
     break;
    case "slave":
     if(p.IsSlaveOfColony && p.MapHeld!=null){
      if(c!=null && c.Blessed("Bane")){f*=.5f;why="班恩的赐福×0.5";}
      if(p.MapHeld.mapPawns.FreeColonistsSpawned.Any(x=>Items.Worn(x,"LP_Relic_BlackHand")!=null)){f*=.5f;why=(why==null?"":why+"；")+"黑手护手×0.5";}
     }
     break;
   }
   return f;
  }
  static readonly DefRef<ThingDef> MorningStar=new DefRef<ThingDef>("LP_Relic_MorningStar"),ForgeHeart=new DefRef<ThingDef>("LP_Relic_ForgeHeart"),NightTear=new DefRef<ThingDef>("LP_Relic_NightTear");
  public static ThingDef NightTearDef=>NightTear.Def;
  static readonly DefRef<HediffDef> InfernalPact=new DefRef<HediffDef>("LP_InfernalPactActive");
  float Offset(Pawn p,out string why){why=null;if(kind=="dodge" && Faith.Enabled && Items.Worn(p,NightTear.Def)!=null && Faith.Dark(p)){why="暗夜之泪（黑暗中）+15%";return .15f;}return 0f;}
  public override void TransformValue(StatRequest req,ref float val){if(!(req.Thing is Pawn p))return;val=val*Factor(p,out _)+Offset(p,out _);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;Factor(p,out var a);Offset(p,out var b);return a==null && b==null?null:"信仰："+string.Join("；",new[]{a,b}.Where(s=>s!=null));}
  public static void Register(){
   void Add(string stat,string kind){var d=DefDatabase<StatDef>.GetNamedSilentFail(stat);if(d==null)return;if(d.parts==null)d.parts=new List<StatPart>();if(d.parts.Any(x=>x is StatPart_Faith f && f.kind==kind))return;var part=new StatPart_Faith{kind=kind,parentStat=d};d.parts.Add(part);}
   Add("WorkSpeedGlobal","work");Add("PsychicSensitivity","sensitivity");Add("MeditationFocusGain","focus");Add("ShootingAccuracyPawn","aim");Add("TameAnimalChance","tame");Add("MeleeDodgeChance","dodge");Add("SlaveSuppressionFallRate","slave");
  }
 }

 [StaticConstructorOnStartup]
 public static class FaithStartup {
  static FaithStartup(){
   if(GenCommandLine.CommandLineArgPassed("lichtest"))Faith.TestSuppressed=true; // 自测进程：信仰检查组自己临时打开
   StatPart_Faith.Register();Faith.ApplySettings();
   var water=DefDatabase<RecipeDef>.AllDefs.FirstOrDefault(r=>r.ProducedThingDef==Faith.WaterDef);if(water!=null)water.workerClass=typeof(RecipeWorker_BlessedWater);
   // 1.37.0 联动：奥术半神诞生（密斯特拉 -20）。魔网崩坏开始时 -10 挂在 GameConditionManager.RegisterCondition 上。
   NetherilAscension.ArcaneDemigodGranted+=p=>Faith.Gain(Faith.God("Mystra"),-20f,(p?.LabelShort??"有人")+"成为奥术半神");
  }
 }

 // ==================== Harmony：各神作为 ====================
 [HarmonyPatch(typeof(Pawn_RecordsTracker),nameof(Pawn_RecordsTracker.Increment))]
 public static class FaithRecords {
  static void Postfix(Pawn_RecordsTracker __instance,RecordDef def){
   var p=__instance.pawn;if(!Faith.Enabled || !Faith.PlayerColonist(p) || def==null)return;
   if(def==RecordDefOf.PlantsHarvested)Faith.Deed("Lathander",.05f,"收获作物");
   else if(def==RecordDefOf.FiresExtinguished)Faith.Deed("Talos",-.1f,p.LabelShort+"扑灭了火");
   else if(def==RecordDefOf.CorpsesBuried){Faith.Deed("Kelemvor",1f*IdeoBridge.DeedFactor("Kelemvor"),p.LabelShort+"安葬了尸体");Faith.Comp?.TrialProgress("Kelemvor",1f);}
   else if(def==RecordDefOf.AnimalsTamed)Faith.Deed("Malar",-1f,p.LabelShort+"驯服了动物");
  }
 }
 [HarmonyPatch(typeof(Plant),nameof(Plant.PlantCollected))]
 public static class FaithHarvest {
  static void Prefix(Plant __instance,Pawn by){if(Faith.Enabled && Faith.PlayerColonist(by) && __instance.def.plant?.harvestedThingDef!=null && __instance.def.plant.harvestedThingDef.IsNutritionGivingIngestible)Faith.Comp?.TrialProgress("Lathander",Math.Max(0,__instance.YieldNow()));}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class FaithKill {
  static void Prefix(Pawn __instance,out bool __state){__state=!__instance.Dead;}
  static void Postfix(Pawn __instance,DamageInfo? dinfo,bool __state){
   if(!__state || !__instance.Dead || !Faith.Enabled)return;
   try{FaithDeeds.OnKilled(__instance,dinfo);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 信仰击杀钩子："+e,8814213);}
  }
 }
 public static class FaithDeeds {
  public static void OnKilled(Pawn v,DamageInfo? dinfo){
   var killer=dinfo?.Instigator as Pawn;bool byPlayer=killer!=null && killer.Faction?.IsPlayer==true;
   if(byPlayer && Items.Undead(v) && v.Faction?.IsPlayer!=true){Faith.Deed("Lathander",2f,"击杀不死者");Faith.Deed("Kelemvor",2f,"击杀敌方不死者");}
   if(byPlayer && v.RaceProps.Animal){
    if(killer.CurJobDef==JobDefOf.Hunt){Faith.Deed("Malar",.5f,killer.LabelShort+"猎杀"+v.LabelShort);Faith.Comp?.TrialProgress("Malar",1f);}
    if(dinfo.HasValue && !dinfo.Value.Def.isRanged && killer.RaceProps.Humanlike)Faith.Deed("Malar",1f,killer.LabelShort+"近战击杀"+v.LabelShort);
   }
   if(byPlayer && dinfo.HasValue && (dinfo.Value.Def==Faith.Radiant || dinfo.Value.Def?.defName=="LP_HolyWater") && v.HostileTo(Faction.OfPlayer) && FaithUtility.Patron?.key=="Shar")FaithUtility.AddFavor(Faith.God("Shar"),-.5f,"神圣之光杀死了敌人");
   // 莎尔：殖民者失去亲人（死者是殖民者，殖民地里还有活着的血亲）。
   if(v.Faction?.IsPlayer==true && v.RaceProps.Humanlike && v.relations!=null && v.relations.FamilyByBlood.Any(r=>r!=null && !r.Dead && r.IsColonist && !r.InMentalState))Faith.Deed("Shar",2f,"殖民者失去了亲人");
   // 灰烬天平：战斗中死去（有攻击者），尸体申诉 30 天。
   if(Items.Worn(v,"LP_Relic_AshScales")!=null && killer!=null && !Items.IsLich(v)){var c=Faith.Comp;if(c!=null)c.appeals[v.ThingID]=Faith.Now+30*GenDate.TicksPerDay;}
  }
 }
 [HarmonyPatch(typeof(HistoryEventsManager),nameof(HistoryEventsManager.RecordEvent))]
 public static class FaithHistory {
  static void Postfix(HistoryEvent historyEvent){
   if(!Faith.Enabled)return;var n=historyEvent.def?.defName;if(n==null)return;
   if(n=="ExecutedPrisoner")Faith.Deed("Tyr",-3f,"处决囚犯");
   else if(n=="HarvestedOrgan")Faith.Deed("Tyr",-5f,"摘取器官");
   else if(n=="RitualDone"){try{if(historyEvent.args.Args.Any(a=>a.arg is Precept_Ritual r && r.def?.defName!=null && r.def.defName.Contains("Trial"))){Faith.Deed("Tyr",3f*IdeoBridge.DeedFactor("Tyr"),"审判仪式");Faith.Comp?.TrialProgress("Tyr",1f);}}catch{}}
  }
 }
 [HarmonyPatch(typeof(GenGuest),nameof(GenGuest.PrisonerRelease))]
 public static class FaithPrisonerRelease {static void Postfix(Pawn p){if(Faith.Enabled && p!=null){Faith.Deed("Tyr",3f*IdeoBridge.DeedFactor("Tyr"),"释放了"+p.LabelShort);Faith.Comp?.TrialProgress("Tyr",1f);}}}
 [HarmonyPatch(typeof(GenGuest),nameof(GenGuest.SlaveRelease))]
 public static class FaithSlaveRelease {static void Postfix(Pawn p){if(Faith.Enabled && p!=null)Faith.Deed("Bane",-3f,"释放了奴隶"+p.LabelShort);}}
 [HarmonyPatch(typeof(GenGuest),nameof(GenGuest.EnslavePrisoner))]
 public static class FaithEnslave {static void Postfix(Pawn prisoner){if(Faith.Enabled && prisoner!=null){Faith.Deed("Bane",3f,"奴役了"+prisoner.LabelShort);Faith.Comp?.TrialProgress("Bane",1f);}}}
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class FaithAbilityCast {
  static void Postfix(RimWorld.Ability __instance,bool __result){
   if(!__result || !Faith.Enabled || !Faith.PlayerColonist(__instance.pawn) || __instance.def==null)return;
   if(__instance.def.IsPsycast || __instance is Ability_Miracle){Faith.Deed("Mystra",.1f,__instance.pawn.LabelShort+"施放"+__instance.def.label);Faith.Comp?.TrialProgress("Mystra",1f);}
  }
 }
 [HarmonyPatch(typeof(Bill_Production),nameof(Bill_Production.Notify_IterationCompleted))]
 public static class FaithBillDone {
  static void Postfix(Bill_Production __instance,Pawn billDoer){
   if(!Faith.Enabled || __instance?.recipe==null)return;
   var product=__instance.recipe.ProducedThingDef;var bench=__instance.billStack?.billGiver as Thing;
   if(product!=null && product==Faith.WaterDef && FaithUtility.Patron!=null)FaithUtility.AddFavor(FaithUtility.Patron,-Faith.BlessedWaterCost,"祝圣圣水");
   if(!Faith.PlayerColonist(billDoer) || bench==null)return;
   if(bench.def.defName=="LP_AlchemyTable")Faith.Deed("Mystra",.5f,"炼金桌产物");
   else if(bench.def.defName==Workbenches.Enchanting || bench.def.defName==Workbenches.Scribe)Faith.Deed("Mystra",.5f,bench.def.label+"产物");
   if(product!=null && (product.IsWeapon || product.IsApparel) && StatPart_Faith.Smithing(billDoer)){Faith.Deed("Surtr",.5f,"锻造了"+product.label);Faith.Comp?.TrialProgress("Surtr",1f);}
  }
 }
 // 巫妖汲取灵魂（喂命匣）：凯兰沃 -3。
 [HarmonyPatch(typeof(Phylactery),nameof(Phylactery.Feed))]
 public static class FaithSoulDrain {static void Prefix(float amount){if(amount>0f && Faith.Enabled)Faith.Deed("Kelemvor",-3f,"汲取灵魂");}}
 // 复生：安息的尸体不能复生；玩家的巫妖复生 洛山达 -10、凯兰沃 -15；巨魔复活 凯兰沃 -5。
 [HarmonyPatch(typeof(ResurrectionUtility),nameof(ResurrectionUtility.TryResurrect))]
 public static class FaithResurrect {
  static bool Prefix(Pawn pawn,ref bool __result){if(pawn!=null && Faith.Comp?.rested.Contains(pawn.ThingID)==true){__result=false;return false;}return true;}
  static void Postfix(Pawn pawn,bool __result){
   if(!__result || pawn==null || !Faith.Enabled || pawn.Faction?.IsPlayer!=true)return;
   if(Items.IsLich(pawn)){Faith.Deed("Lathander",-10f,pawn.LabelShort+"以巫妖之身复生");Faith.Deed("Kelemvor",-15f,pawn.LabelShort+"以巫妖之身复生");}
   else if(Troll.Is(pawn))Faith.Deed("Kelemvor",-5f,"巨魔"+pawn.LabelShort+"复活");
  }
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.AddHediff),new[]{typeof(Hediff),typeof(BodyPartRecord),typeof(DamageInfo?),typeof(DamageWorker.DamageResult)})]
 public static class FaithFrostbite {static void Postfix(Pawn ___pawn,Hediff hediff){if(hediff?.def?.defName=="Frostbite" && Faith.Enabled && Faith.PlayerColonist(___pawn))Faith.Deed("Surtr",-1f,___pawn.LabelShort+"冻伤");}}
 [HarmonyPatch(typeof(WeatherEvent_LightningStrike),nameof(WeatherEvent_LightningStrike.FireEvent))]
 public static class FaithLightning {
  static readonly AccessTools.FieldRef<WeatherEvent_LightningStrike,IntVec3> strikeLoc=AccessTools.FieldRefAccess<WeatherEvent_LightningStrike,IntVec3>("strikeLoc");
  static readonly AccessTools.FieldRef<WeatherEvent,Map> mapRef=AccessTools.FieldRefAccess<WeatherEvent,Map>("map");
  static void Prefix(WeatherEvent_LightningStrike __instance){
   var c=Faith.Comp;var map=mapRef(__instance);if(c==null || map==null || !Faith.Enabled)return;
   // 塔洛斯的赐福：雷暴里殖民地建筑不挨雷。
   if(c.Blessed("Talos") && map.IsPlayerHome){var loc=strikeLoc(__instance);if(loc.IsValid && loc.InBounds(map) && (loc.GetEdifice(map)?.Faction?.IsPlayer==true || map.areaManager.Home[loc])){if(CellFinder.TryFindRandomCellNear(loc,map,30,x=>x.InBounds(map) && !x.Roofed(map) && !map.areaManager.Home[x],out var away))strikeLoc(__instance)=away;}}
   if(map.IsPlayerHome && Faith.Relevant(Faith.God("Talos"))){int day=GenDate.DaysPassed;if(c.talosDay!=day){c.talosDay=day;c.talosToday=0f;}if(c.talosToday<Faith.TalosDailyCap){c.talosToday+=1f;Faith.Deed("Talos",1f,"雷击落在地图上");}}
  }
 }
 // 魔网崩坏（1.37.0）开始：密斯特拉 -10。
 [HarmonyPatch(typeof(GameConditionManager),nameof(GameConditionManager.RegisterCondition))]
 public static class FaithWeaveCollapse {static void Postfix(GameCondition cond){if(cond?.def!=null && cond.def==NetherilAscension.CollapseDef && Faith.Enabled)Faith.Gain(Faith.God("Mystra"),-10f,"魔网崩坏");}}

 // ==================== Harmony：赐福、信徒被动、遗物 ====================
 [HarmonyPatch(typeof(Plant),"get_GrowthRate")]
 public static class FaithGrowth {
  static void Postfix(Plant __instance,ref float __result){
   if(__result<=0f || !__instance.Spawned)return;var c=Faith.Comp;if(c==null || !Faith.Enabled)return;var map=__instance.Map;
   if(map.IsPlayerHome){if(c.Blessed("Lathander"))__result*=1.25f;else if(c.Cursed("Lathander"))__result*=.75f;}
   if(Miracles.InDawn(map,__instance.Position))__result*=1.2f;
  }
 }
 [HarmonyPatch(typeof(Pawn_InteractionsTracker),nameof(Pawn_InteractionsTracker.SocialFightChance))]
 public static class FaithSocialFight {
  static void Postfix(Pawn ___pawn,ref float __result){if(__result<=0f || !Faith.Enabled)return;if(FaithUtility.DevoteeGod(___pawn)?.key=="Tyr")__result*=.75f;if(Items.Worn(___pawn,"LP_Relic_HandOfTyr")!=null)__result*=.5f;}
 }
 [HarmonyPatch(typeof(ThoughtWorker_Dark),"CurrentStateInternal")]
 public static class FaithDark {static void Postfix(Pawn p,ref ThoughtState __result){if(__result.Active && Faith.Enabled && (FaithUtility.DevoteeGod(p)?.key=="Shar" || (p.Faction?.IsPlayer==true && Faith.Comp?.Blessed("Shar")==true)))__result=false;}}
 // 看到尸体（凯兰沃信徒、凯兰沃赐福）与失去亲人（暗夜之泪）的心情不加。
 [HarmonyPatch(typeof(MemoryThoughtHandler),nameof(MemoryThoughtHandler.TryGainMemory),new[]{typeof(Thought_Memory),typeof(Pawn)})]
 public static class FaithMemoryBlock {
  static HashSet<ThoughtDef> family;
  static HashSet<ThoughtDef> Family=>family??(family=new HashSet<ThoughtDef>(DefDatabase<PawnRelationDef>.AllDefs.Where(r=>r.familyByBloodRelation || r==PawnRelationDefOf.Spouse || r==PawnRelationDefOf.Lover).SelectMany(r=>new[]{r.diedThought,r.diedThoughtFemale,r.killedThought,r.killedThoughtFemale}).Where(t=>t!=null)));
  static bool Prefix(MemoryThoughtHandler __instance,Thought_Memory newThought){
   var p=__instance.pawn;var d=newThought?.def;if(p==null || d==null || !Faith.Enabled)return true;
   if(MemoryDefs.ObservedCorpse(d)){if(FaithUtility.DevoteeGod(p)?.key=="Kelemvor" || (p.Faction?.IsPlayer==true && Faith.Comp?.Blessed("Kelemvor")==true))return false;}
   if(Family.Contains(d) && Items.Worn(p,StatPart_Faith.NightTearDef)!=null)return false;
   return true;
  }
 }
 [HarmonyPatch(typeof(CompRefuelable),nameof(CompRefuelable.ConsumeFuel))]
 public static class FaithForgeFuel {static void Prefix(CompRefuelable __instance,ref float amount){if(amount>0f && Faith.Comp?.Blessed("Surtr")==true && __instance.parent?.Faction?.IsPlayer==true && StatPart_Faith.SmithingBench(__instance.parent.def))amount*=.5f;}}
 // 提尔的赐福：每次劝说降低的抗拒 ×1.5。
 [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt),nameof(InteractionWorker_RecruitAttempt.Interacted))]
 public static class FaithTyrRecruit {
  static void Prefix(Pawn __1,out float __state){__state=__1?.guest?.resistance??-1f;}
  static void Postfix(Pawn __1,float __state){if(__state<0 || __1?.guest==null || Faith.Comp?.Blessed("Tyr")!=true)return;float drop=__state-__1.guest.resistance;if(drop>0)__1.guest.resistance=Mathf.Max(0f,__1.guest.resistance-drop*.5f);}
 }
 // 凯兰沃的赐福：殖民者受致命伤时 25% 免死（每次显圣最多一次）。
 [HarmonyPatch(typeof(DamageWorker_AddInjury),"FinalizeAndAddInjury",new Type[]{typeof(Pawn),typeof(Hediff_Injury),typeof(DamageInfo),typeof(DamageWorker.DamageResult)})]
 public static class FaithKelemvorReprieve {
  static void Prefix(Pawn __0,Hediff_Injury __1){
   var p=__0;var c=Faith.Comp;if(p==null || p.Dead || __1==null || c==null || c.kelemvorSaveUsed || !c.Blessed("Kelemvor") || p.Faction?.IsPlayer!=true || !p.RaceProps.Humanlike)return;
   if(!p.health.WouldDieAfterAddingHediff(__1) || Rand.Value>=.25f)return;
   c.kelemvorSaveUsed=true;__1.Severity=.01f;
   Fiends.Defer(()=>{if(!p.Dead){Messages.Message("凯兰沃把"+p.LabelShort+"从死亡边缘推了回来。",p,MessageTypeDefOf.PositiveEvent,false);if(p.Spawned)FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,1.5f);}});
  }
 }
 // 复生卷轴：灰烬天平的申诉期内不受“死亡超过1天/腐烂”限制（巫妖、死灵造物照旧不行）。
 [HarmonyPatch(typeof(ItemScrolls),nameof(ItemScrolls.CannotRevivify))]
 public static class FaithAppealRevivify {
  static void Postfix(Corpse c,ref string __result){
   if(__result==null || c?.InnerPawn==null)return;var p=c.InnerPawn;var comp=Faith.Comp;
   if(Faith.Comp?.rested.Contains(p.ThingID)==true){__result="这具尸体已经安息，不能复生。";return;}
   if(comp!=null && comp.appeals.TryGetValue(p.ThingID,out int until) && Faith.Now<until && !Items.Undead(p) && p.health.hediffSet.GetBrain()!=null && p.RaceProps.Humanlike)__result=null;
  }
 }
 // 汲取灵魂：已安息的尸体不行。
 [HarmonyPatch(typeof(Souls),nameof(Souls.Unfit))]
 public static class FaithRestedSoul {static void Postfix(Thing thing,ref string __result){if(__result==null && thing is Corpse c && c.InnerPawn!=null && Faith.Comp?.rested.Contains(c.InnerPawn.ThingID)==true)__result="这具尸体已经安息（凯兰沃），灵魂不能被汲取。";}}

 // ==================== 生成：派系固定信仰、随机特性互斥 ====================
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class FaithGeneration {
  [HarmonyPriority(Priority.Low)]
  static void Postfix(Pawn __result,PawnGenerationRequest request){
   if(__result?.story?.traits==null || !__result.RaceProps.Humanlike)return;
   try{
    FaithRaces.OnGenerated(__result);
    if(!Faith.Enabled || Faith.TestSuppressed || Necro.Construct(__result)!=null || Faith.Devotee==null)return;
    var ext=__result.Faction?.def?.GetModExtension<FaithExtension>();
    float chance=Faith.FactionDevoteeChance;if(FaithRaces.IsAasimar(__result) && __result.Faction?.def?.defName=="LP_FaerunColony")chance=.5f;
    if(ext!=null && ext.deities.Count>0 && !__result.Faction.IsPlayer && Rand.Chance(chance) && !FaithUtility.IsAtheist(__result) && FaithUtility.DevoteeGod(__result)==null){
     var god=ext.deities.RandomElement();if(god.devoteeDegree>0)__result.story.traits.GainTrait(new Trait(Faith.Devotee,god.devoteeDegree,true));
    }
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 信仰生成钩子："+e,8814214);}
  }
 }
 [HarmonyPatch(typeof(LichMod),nameof(LichMod.WriteSettings))]
 public static class FaithWriteSettings {static void Postfix(){Faith.ApplySettings();}}
}
