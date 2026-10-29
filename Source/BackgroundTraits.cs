using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.32.0 背景特性（Defs/Traits_Backgrounds.xml）：8 个 DND 背景进原版特性随机池，exclusionTags LP_Background 保证一人最多一个。
 // 出现权重 = 0.3 × 设置页“背景特性出现率”（关掉开关为 0，已有的保留）。死灵造物不会有背景（生成时清掉全部特性，这里再挡一次 GainTrait）。
 public static class BackgroundTraits {
  public const string Tag="LP_Background";
  public const float BaseCommonality=.3f;
  public static readonly string[] Names={"Acolyte","Criminal","FolkHero","Noble","Sage","Soldier","Hermit","Outlander"};
  static readonly Dictionary<string,TraitDef> defs=new Dictionary<string,TraitDef>();
  public static TraitDef Def(string name){if(!defs.TryGetValue(name,out var d) || d==null)defs[name]=d=DefDatabase<TraitDef>.GetNamedSilentFail("LP_Background_"+name);return d;}
  public static TraitDef Acolyte=>Def("Acolyte");public static TraitDef Criminal=>Def("Criminal");public static TraitDef FolkHero=>Def("FolkHero");public static TraitDef Noble=>Def("Noble");
  public static TraitDef Sage=>Def("Sage");public static TraitDef Soldier=>Def("Soldier");public static TraitDef Hermit=>Def("Hermit");public static TraitDef Outlander=>Def("Outlander");
  public static IEnumerable<TraitDef> All=>Names.Select(Def).Where(d=>d!=null);
  public static bool IsBackground(TraitDef d)=>d?.exclusionTags!=null && d.exclusionTags.Contains(Tag);
  public static bool Has(Pawn p,TraitDef d)=>d!=null && p?.story?.traits!=null && p.story.traits.HasTrait(d);
  public static int Count(Pawn p)=>p?.story?.traits==null?0:p.story.traits.allTraits.Count(t=>IsBackground(t.def));

  // ---------- 设置 ----------
  // 自测期间（LichChecks）除背景特性检查组外不生成背景，免得随机背景改动其他检查用到的小人。
  public static bool TestSuppressed;
  public static float Commonality{get{var s=LichMod.Settings;return TestSuppressed?0f:s==null?BaseCommonality:s.backgroundTraits?BaseCommonality*s.backgroundTraitPercent/100f:0f;}}
  // TraitDef.commonality 是私有字段：用 FieldRef 读写。
  static readonly AccessTools.FieldRef<TraitDef,float> commonalityRef=AccessTools.FieldRefAccess<TraitDef,float>("commonality");
  public static float Weight(TraitDef d)=>commonalityRef(d);
  public static void SetWeight(TraitDef d,float w)=>commonalityRef(d)=w;
  public static void ApplySettings(){float c=Commonality;foreach(var d in All)SetWeight(d,c);}

  // ---------- 缓存（测试会先清掉） ----------
  public static void ForgetCaches(){ThoughtWorker_HeroPresent.Forget();ThoughtWorker_SageNoStudy.Forget();soldierStamp.Clear();soldierBuddy.Clear();}

  // 侍僧：仪式正常结束时给在场的侍僧“神圣时光”。
  public static void GiveHolyTime(IEnumerable<Pawn> pawns){
   var thought=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_BG_HolyTime");if(thought==null || pawns==null)return;
   foreach(var p in pawns.ToList())if(p!=null && !p.Dead && Has(p,Acolyte))p.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
  }

  // 民间英雄倒地：全殖民地（各地图上的自由殖民者）-3，1 天。
  public static void HeroDowned(Pawn hero){
   if(hero==null || hero.Faction==null || !hero.Faction.IsPlayer || !Has(hero,FolkHero))return; // 1.52.0 热修：世界生成时没有玩家阵营，不能用 Faction.OfPlayer
   var thought=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_BG_HeroFallen");if(thought==null)return;
   // 各地图上和玩家商队里的自由殖民者（不用 PawnsFinder 的缓存名单：同一刻刚生成的小人可能还不在里面）。
   var colony=new List<Pawn>();foreach(var m in Find.Maps)colony.AddRange(m.mapPawns.FreeColonistsSpawned);
   foreach(var c in Find.WorldObjects.Caravans)if(c.IsPlayerControlled)foreach(var p in c.PawnsListForReading)if(p.IsFreeColonist)colony.Add(p);
   foreach(var p in colony)if(p!=hero && !p.Dead)p.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
  }

  // 罪犯：作为囚犯越狱几率 ×2；殖民地有罪犯背景的自由殖民者时，囚犯越狱几率 ×0.75。返回的是几率倍数（MTB 除以它）。
  public const float CriminalBreakChance=2f,CriminalWardenChance=.75f,CriminalRecruitFactor=1.3f,CriminalBlackNetPrice=.1f;
  public static bool ColonyHasCriminal(Map map){if(map==null)return false;var list=map.mapPawns.FreeColonistsSpawned;for(int i=0;i<list.Count;i++)if(Has(list[i],Criminal))return true;return false;}

  // 士兵：3 格内有另一名士兵背景的同阵营友军（缓存 120 刻）。
  public const float SoldierAimFactor=.9f,SoldierHitFactor=1.05f;
  static StatDef cohesion;public static StatDef Cohesion=>cohesion??(cohesion=DefDatabase<StatDef>.GetNamedSilentFail("LP_SoldierCohesion"));
  static readonly Dictionary<int,int> soldierStamp=new Dictionary<int,int>();
  static readonly Dictionary<int,bool> soldierBuddy=new Dictionary<int,bool>();
  public static bool SoldierBuddy(Pawn p){
   if(!Has(p,Soldier) || !p.Spawned || p.Faction==null)return false;
   int now=Find.TickManager.TicksGame;
   if(soldierStamp.TryGetValue(p.thingIDNumber,out int t) && now>=t && now-t<120)return soldierBuddy[p.thingIDNumber];
   bool found=false;
   foreach(var o in p.Map.mapPawns.SpawnedPawnsInFaction(p.Faction))if(o!=p && !o.Downed && !o.Dead && o.Position.DistanceToSquared(p.Position)<=9 && Has(o,Soldier)){found=true;break;}
   soldierStamp[p.thingIDNumber]=now;soldierBuddy[p.thingIDNumber]=found;return found;
  }

  // 局外人：野生植物 = 不是玩家种下的（sown 为假）。
  public const float OutlanderSpeed=1.1f,OutlanderWildYield=1.25f;public const int OutlanderCoopedTicks=3*GenDate.TicksPerDay;
  public static bool HarvestingWild(Pawn p)=>p.CurJob?.targetA.Thing is Plant plant && !plant.sown;
  public static CompBackground Comp(Pawn p)=>p?.GetComp<CompBackground>();

  // 隐士、贵族：按自己床所在的房间判断（床在野外或没有床时不算）。
  public static Room BedRoom(Pawn p){var bed=p?.ownership?.OwnedBed;if(bed==null || !bed.Spawned)return null;var room=bed.GetRoom();return room==null || room.PsychologicallyOutdoors?null:room;}
  // 原版“体面”（decent）从印象 40 开始。
  public const float DecentImpressiveness=40f;

  // VPE：侍僧的牧师路径、贤者的法师路径经验 ×1.25（联动程序集调用）。
  public static float ClassXpFactor(Pawn p,string pathDefName)=>DndTraits.ClassXp(p,pathDefName,(pathDefName=="LP_Path_Cleric" && Has(p,Acolyte) || pathDefName=="LP_Path_Wizard" && Has(p,Sage)?1.25f:1f)*HeroDestiny.ClassXpFactor(p,pathDefName)); // 1.58.0 神之勇士：牧师 / 圣武士 ×1.3；1.62.0 职业天赋 ×1.25 取较高
 }

 [StaticConstructorOnStartup]
 public static class BackgroundStartup {
  static BackgroundStartup(){
   BackgroundTraits.ApplySettings();
   // 战斗、移速、采集、交易都用同一个 StatPart，按所属属性分别处理。
   var cohesion=BackgroundTraits.Cohesion;
   foreach(var stat in new[]{StatDefOf.AimingDelayFactor,cohesion,StatDefOf.MoveSpeed,StatDefOf.PlantHarvestYield,StatDefOf.TradePriceImprovement}){
    if(stat==null)continue;if(stat.parts==null)stat.parts=new List<StatPart>();
    if(!stat.parts.Any(x=>x is StatPart_Background))stat.parts.Add(new StatPart_Background{parentStat=stat});
    // 载入时没有 StatPart 的属性会被标成不可变并永久缓存（士兵协同就是）：加了 StatPart 后取消标记。
    stat.immutable=false;
   }
   // 射击精度、近战命中在 StatPart 之后还有原版技能曲线：士兵协同挂在 postProcessStatFactors 上，曲线之后再乘，最后仍受原版上下限约束。
   if(cohesion!=null)foreach(var stat in new[]{StatDefOf.ShootingAccuracyPawn,StatDefOf.MeleeHitChance}){
    if(stat.postProcessStatFactors==null)stat.postProcessStatFactors=new List<StatDef>();
    if(!stat.postProcessStatFactors.Contains(cohesion))stat.postProcessStatFactors.Add(cohesion);
   }
   // 局外人的屋顶计时存在每个类人小人的组件里（只有局外人才计时，默认值不写进存档）。
   foreach(var def in DefDatabase<ThingDef>.AllDefsListForReading)if(def.race!=null && def.race.Humanlike){
    if(def.comps==null)def.comps=new List<CompProperties>();
    if(!def.comps.Any(c=>c.compClass==typeof(CompBackground)))def.comps.Add(new CompProperties(typeof(CompBackground)));
   }
  }
 }

 public class StatPart_Background : StatPart {
  float Factor(Pawn p,out float offset,out string why){
   offset=0f;why=null;
   if(p?.story?.traits==null)return 1f;
   if(parentStat==StatDefOf.AimingDelayFactor){if(p.Drafted && BackgroundTraits.Has(p,BackgroundTraits.Soldier)){why="士兵背景（征召中）";return BackgroundTraits.SoldierAimFactor;}}
   else if(parentStat==BackgroundTraits.Cohesion){if(BackgroundTraits.SoldierBuddy(p)){why="士兵背景（3格内有士兵战友）";return BackgroundTraits.SoldierHitFactor;}}
   else if(parentStat==StatDefOf.MoveSpeed){if(BackgroundTraits.Has(p,BackgroundTraits.Outlander) && BackgroundTraits.Comp(p)?.outdoors==true){why="局外人背景（野外）";return BackgroundTraits.OutlanderSpeed;}}
   else if(parentStat==StatDefOf.PlantHarvestYield){if(BackgroundTraits.Has(p,BackgroundTraits.Outlander) && BackgroundTraits.HarvestingWild(p)){why="局外人背景（野生植物）";return BackgroundTraits.OutlanderWildYield;}}
   else if(parentStat==StatDefOf.TradePriceImprovement){if(TradeSession.Active && TradeSession.trader?.Faction?.def!=null && TradeSession.trader.Faction.def==Factions.BlackNetDef && BackgroundTraits.Has(p,BackgroundTraits.Criminal)){why="罪犯背景（黑网商队）";offset=BackgroundTraits.CriminalBlackNetPrice;}}
   return 1f;
  }
  public override void TransformValue(StatRequest req,ref float val){
   if(!(req.Thing is Pawn p))return;
   float f=Factor(p,out float offset,out _);
   val*=f;
   val+=offset;
  }
  public override string ExplanationPart(StatRequest req){
   if(!(req.Thing is Pawn p))return null;
   float f=Factor(p,out float offset,out string why);
   if(why==null)return null;
   return offset!=0f?why+": +"+offset.ToStringPercent():why+": x"+f.ToStringPercent();
  }
 }

 // 局外人：每 60 刻看一次是否在“无屋顶且不在居住区”的地方（移速加成），以及连续待在屋顶下的时间（存档保留）。
 public class CompBackground : ThingComp {
  public int roofedTicks;public bool outdoors;int pending;
  public override void CompTickInterval(int delta){pending+=delta;if(pending<60)return;Refresh(pending);pending=0;}
  public void Refresh(int elapsed){
   var p=parent as Pawn;
   if(!BackgroundTraits.Has(p,BackgroundTraits.Outlander)){roofedTicks=0;outdoors=false;return;}
   if(p.Dead)return;
   // 不在地图上（商队、运输舱）算在野外。
   if(!p.Spawned){outdoors=false;roofedTicks=0;return;}
   bool roofed=p.Position.Roofed(p.Map);
   outdoors=!roofed && !p.Map.areaManager.Home[p.Position];
   roofedTicks=roofed?roofedTicks+elapsed:0;
  }
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref roofedTicks,"lpOutlanderRoofedTicks",0);}
 }

 // ---------- 心情 ----------
 // 英雄在此：每张地图缓存“被征召的民间英雄”名单，250 刻刷新一次；每个殖民者只和名单里的人比距离。
 public class ThoughtWorker_HeroPresent : ThoughtWorker {
  public const int Radius=6;
  static readonly Dictionary<int,List<Pawn>> heroes=new Dictionary<int,List<Pawn>>();
  static readonly Dictionary<int,int> stamp=new Dictionary<int,int>();
  public static void Forget(){heroes.Clear();stamp.Clear();}
  static List<Pawn> Heroes(Map map){
   int now=Find.TickManager.TicksGame;
   if(!stamp.TryGetValue(map.uniqueID,out int t) || now<t || now-t>=250 || !heroes.ContainsKey(map.uniqueID)){
    var list=new List<Pawn>();foreach(var p in map.mapPawns.FreeColonistsSpawned)if(p.Drafted && BackgroundTraits.Has(p,BackgroundTraits.FolkHero))list.Add(p);
    heroes[map.uniqueID]=list;stamp[map.uniqueID]=now;
   }
   return heroes[map.uniqueID];
  }
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!p.Spawned || !p.IsColonist || p.Map==null)return false;
   foreach(var h in Heroes(p.Map))if(h!=p && h.Spawned && h.Map==p.Map && h.Position.DistanceToSquared(p.Position)<=Radius*Radius)return true;
   return false;
  }
 }
 public class ThoughtWorker_NobleBedroom : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!BackgroundTraits.Has(p,BackgroundTraits.Noble))return false;
   var room=BackgroundTraits.BedRoom(p);
   return room!=null && room.GetStat(RoomStatDefOf.Impressiveness)<BackgroundTraits.DecentImpressiveness;
  }
 }
 // 无书可读：整个殖民地（所有地图）没有任何书架和研究台，250 刻缓存一次。
 public class ThoughtWorker_SageNoStudy : ThoughtWorker {
  static int stamp=-1;static bool hasStudy;
  public static void Forget(){stamp=-1;}
  public static bool ColonyHasStudy(){
   int now=Find.TickManager.TicksGame;
   if(stamp<0 || now<stamp || now-stamp>=250){
    hasStudy=false;stamp=now;
    foreach(var map in Find.Maps){foreach(var b in map.listerBuildings.allBuildingsColonist)if(b is Building_Bookcase || b is Building_ResearchBench){hasStudy=true;break;}if(hasStudy)break;}
   }
   return hasStudy;
  }
  protected override ThoughtState CurrentStateInternal(Pawn p)=>BackgroundTraits.Has(p,BackgroundTraits.Sage) && p.Faction==Faction.OfPlayer && !ColonyHasStudy();
 }
 // 隐士：自己的床在一间只有自己的卧室里 +3（第 0 档）；床所在房间还有别人的床、或者和别人同睡一张床 -4（第 1 档）。
 public class ThoughtWorker_HermitRoom : ThoughtWorker {
  public static int Stage(Pawn p){
   var room=BackgroundTraits.BedRoom(p);if(room==null)return -1;
   var bed=p.ownership.OwnedBed;
   // 营房的 Room.Owners 是空的：直接看房间里每张非医疗床有没有别的主人。
   foreach(var b in room.ContainedBeds)if(!b.Medical && b.OwnersForReading.Any(o=>o!=p))return 1;
   return room.Role==RoomRoleDefOf.Bedroom?0:-1;
  }
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!BackgroundTraits.Has(p,BackgroundTraits.Hermit))return false;
   int s=Stage(p);return s<0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(s);
  }
 }
 public class ThoughtWorker_OutlanderCooped : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>BackgroundTraits.Has(p,BackgroundTraits.Outlander) && (BackgroundTraits.Comp(p)?.roofedTicks??0)>=BackgroundTraits.OutlanderCoopedTicks;
 }

 // ---------- Harmony ----------
 // 死灵造物不拿背景特性。
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.GainTrait))]
 public static class Background_GainTrait {
  static bool Prefix(Pawn ___pawn,Trait trait){
   if(trait==null || !BackgroundTraits.IsBackground(trait.def))return true;
   var p=___pawn;
   return p!=null && p.RaceProps.Humanlike && Necro.Construct(p)==null;
  }
 }
 // 侍僧：仪式正常结束、给参与者发心情的那一步（LordJob_Ritual.ApplyOutcome 里调用 AddParticipantThoughts，取消或无人参加时不会走到）。
 [HarmonyPatch(typeof(LordJob_Ritual),"AddParticipantThoughts")]
 public static class Background_RitualThoughts {
  static void Postfix(Dictionary<Pawn,int> ___totalPresenceTmp){if(___totalPresenceTmp!=null)BackgroundTraits.GiveHolyTime(___totalPresenceTmp.Keys);}
 }
 // 民间英雄倒地。
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class Background_HeroDowned {
  static void Postfix(Pawn ___pawn){if(___pawn!=null && ___pawn.health.Downed)BackgroundTraits.HeroDowned(___pawn);}
 }
 // 罪犯：越狱平均间隔除以几率倍数。
 [HarmonyPatch(typeof(PrisonBreakUtility),nameof(PrisonBreakUtility.InitiatePrisonBreakMtbDays))]
 public static class Background_PrisonBreak {
  static void Postfix(Pawn pawn,StringBuilder sb,ref float __result){
   if(__result<=0f || pawn==null)return;
   if(BackgroundTraits.Has(pawn,BackgroundTraits.Criminal)){__result/=BackgroundTraits.CriminalBreakChance;if(sb!=null){sb.AppendLineIfNotEmpty();sb.Append("  - 罪犯背景: "+BackgroundTraits.CriminalBreakChance.ToStringPercent());}}
   if(BackgroundTraits.ColonyHasCriminal(pawn.MapHeld)){__result/=BackgroundTraits.CriminalWardenChance;if(sb!=null){sb.AppendLineIfNotEmpty();sb.Append("  - 殖民地有罪犯背景的殖民者: "+BackgroundTraits.CriminalWardenChance.ToStringPercent());}}
  }
 }
 // 罪犯囚犯：招募谈话让抵抗多降 30%。
 [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt),nameof(InteractionWorker_RecruitAttempt.Interacted))]
 public static class Background_RecruitResistance {
  static void Prefix(Pawn recipient,out float __state){__state=recipient?.guest?.resistance??-1f;}
  static void Postfix(Pawn recipient,float __state){
   if(__state<=0f || recipient?.guest==null || !BackgroundTraits.Has(recipient,BackgroundTraits.Criminal))return;
   float drop=__state-recipient.guest.resistance;
   if(drop>0f)recipient.guest.resistance=Math.Max(0f,recipient.guest.resistance-drop*(BackgroundTraits.CriminalRecruitFactor-1f));
  }
 }
 // 贵族：荣誉 ×1.2。
 [HarmonyPatch(typeof(Pawn_RoyaltyTracker),nameof(Pawn_RoyaltyTracker.GainFavor))]
 public static class Background_NobleFavor {
  static void Prefix(Pawn ___pawn,ref int amount){if(amount>0 && BackgroundTraits.Has(___pawn,BackgroundTraits.Noble))amount=Mathf.RoundToInt(amount*1.2f);}
 }
 // 隐士：原版每次想主动搭话时一半概率作罢（当作已经聊过，原版随后重新按平均间隔等待），主动社交频率约 ×0.5。
 [HarmonyPatch(typeof(Pawn_InteractionsTracker),"TryInteractRandomly")]
 public static class Background_HermitInteract {
  static bool Prefix(Pawn ___pawn,ref bool __result){
   if(!BackgroundTraits.Has(___pawn,BackgroundTraits.Hermit) || !Rand.Chance(.5f))return true;
   __result=true;return false;
  }
 }

 public partial class Settings {
  public bool backgroundTraits=true;public int backgroundTraitPercent=100;
  void ExposeBackgrounds(){Scribe_Values.Look(ref backgroundTraits,"backgroundTraits",true);Scribe_Values.Look(ref backgroundTraitPercent,"backgroundTraitPercent",100);backgroundTraitPercent=Math.Max(0,Math.Min(300,backgroundTraitPercent));ExposeBackstories();}
 }
 public static class BackgroundSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.GapLine();l.Label("背景特性");
   l.CheckboxLabeled("背景特性",ref s.backgroundTraits,"侍僧、罪犯、民间英雄、贵族、贤者、士兵、隐士、局外人 8 个背景特性进入原版特性随机池，一个人最多一个背景。关闭后不再生成新的背景特性，已有的保留。立即生效。");
   l.Label("背景特性出现率："+s.backgroundTraitPercent+"%（每个背景的出现权重 = 0.3 × 此值）");
   s.backgroundTraitPercent=Mathf.RoundToInt(l.Slider(s.backgroundTraitPercent,0,300)/5f)*5;
   BackgroundTraits.ApplySettings();
   // 1.46.0 DND 背景故事放在背景特性组下面。
   BackstorySettings.Draw(l);
  }
 }
}
