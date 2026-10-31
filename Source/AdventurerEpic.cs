using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.49.0 冒险者等级第三阶段（见 ArtSource/Leveling-1.49.0-spec.md）：史诗恩赐（19 级）、传奇冒险者（20 级 + DND 里程碑）、半神属性上限、
 // 人形击杀里程碑、宿敌冠军事件、非人形 DND 生物的挑战等级（CR）。存档字段都在 Hediff_Adventurer 里；宿敌事件的排期在 GameComponent_Epic。
 // 击杀里程碑和宿敌冠军是“百战觉醒”的等价内容，不引用那个模组；检测到它（local.isekai.killmilestones）时这两项默认关。
 public class LP_EpicBoonDef : Def {
  public int order;public bool enemy=true;public List<StatModifier> statOffsets,statFactors;
 }

 public static class Epic {
  public const int MightBonus=4,LegendCapPer5=2,FateCooldownDays=60,MaxFollowers=4,NemesisMinLevel=12,NemesisLevelOffset=2,NemesisMinDays=45,NemesisMaxDays=60;
  public const float LegendSocial=1.1f,CombatHealFraction=.05f,KillMilestoneShare=.3f,NemesisShare=.5f;
  public static readonly int[] KillMilestones={25,50,100,250,500,1000};
  public const int BoonKills=1000;
  public const string IkmId="local.isekai.killmilestones";

  static LP_EpicBoonDef B(ref LP_EpicBoonDef c,string n)=>c??(c=DefDatabase<LP_EpicBoonDef>.GetNamedSilentFail(n));
  static LP_EpicBoonDef fate,immortal,might,speed,psyche,combat;
  public static LP_EpicBoonDef Fate=>B(ref fate,"LP_Boon_Fate");
  public static LP_EpicBoonDef Immortal=>B(ref immortal,"LP_Boon_Immortal");
  public static LP_EpicBoonDef Might=>B(ref might,"LP_Boon_Might");
  public static LP_EpicBoonDef Speed=>B(ref speed,"LP_Boon_Speed");
  public static LP_EpicBoonDef Psyche=>B(ref psyche,"LP_Boon_Psyche");
  public static LP_EpicBoonDef Combat=>B(ref combat,"LP_Boon_Combat");
  public static IEnumerable<LP_EpicBoonDef> Boons=>DefDatabase<LP_EpicBoonDef>.AllDefsListForReading.OrderBy(b=>b.order);
  public static bool Has(Pawn p,LP_EpicBoonDef b){if(b==null || p==null || !Adventurer.Enabled)return false;var h=Adventurer.Get(p);return h!=null && h.boons.Count>0 && h.boons.Contains(b);}
  static void Text(Pawn p,string s){if(p!=null && p.Spawned)MoteMaker.ThrowText(p.DrawPos,p.Map,s,new Color(1f,.8f,.3f),2.5f);}

  // ---------- 设置：-1 = 默认（检测到百战觉醒时关，否则开），0/1 = 玩家改过 ----------
  static bool? ikm;public static bool IkmActive=>ikm??(ikm=ModsConfig.IsActive(IkmId)).Value; // 1.58.6：启用的模组不会中途变，缓存（击杀路径）
  public static bool On(int v,bool ikm)=>v<0?!ikm:v==1;
  public static bool KillMilestonesOn=>Adventurer.Enabled && On(LichMod.Settings?.adventurerKillMilestones??-1,IkmActive);
  public static bool NemesisOn=>Adventurer.Enabled && On(LichMod.Settings?.adventurerNemesis??-1,IkmActive);

  // ---------- 传奇冒险者 ----------
  public static bool DndMilestone(string key)=>!key.NullOrEmpty() && !key.StartsWith("kills");
  public static bool IsLegend(Pawn p)=>Adventurer.Enabled && Adventurer.Get(p)?.legendary==true;
  public static bool CheckLegend(Hediff_Adventurer h){
   if(h==null || h.legendary || h.level<Adventurer.MaxLevel || !Adventurer.Enabled || !h.milestonesDone.Any(DndMilestone))return false;
   h.legendary=true;h.Dirty();State?.RefreshLegend();
   var p=h.pawn;
   Legend.Became(h); // 1.50.0：解锁 21～40 级，攒下的经验立即结算
   if(p.Faction!=null && p.Faction.IsPlayer && !p.Dead && !Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("传奇冒险者："+p.LabelShort,p.LabelShort+"已经是 20 级的冒险者，又完成过真正的冒险。从今天起，人们称其为“传奇冒险者”。\n\n殖民地全员心情 +3（与传奇同行）；"+p.LabelShort+"社交影响 +10%，囚犯、访客和商人对其好感 +10。\n\n传奇倒下时，全殖民地心情 −6（10 天）。"+(Legend.On?"\n\n传奇等级 21～40 已解锁："+p.LabelShort+"攒下的经验立即结算，每升一级得 2 个传奇属性点（在“冒险者”分页投点）。":""),LetterDefOf.PositiveEvent,p);
   return true;
  }
  public static bool ColonyHasLegend(){foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)if(IsLegend(p))return true;return false;}
  public static void LegendFell(Pawn p){
   var d=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_LegendFallen");if(d==null)return;
   foreach(var c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)if(c!=p)c.needs?.mood?.thoughts?.memories?.TryGainMemory(d);
   State?.RefreshLegend();
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("传奇陨落",p.LabelShort+"，殖民地的传奇冒险者，倒下了。",LetterDefOf.Death,p.Corpse!=null?new LookTargets(p.Corpse):LookTargets.Invalid);
  }

  // ---------- 命运之恩：致命伤改为倒地并止血，60 天 1 次 ----------
  public static bool FateReady(Pawn p,out Hediff_Adventurer h){h=Adventurer.Get(p);return h!=null && Has(p,Fate) && Find.TickManager.TicksGame-h.fateTick>=FateCooldownDays*GenDate.TicksPerDay;}
  public static bool TryFate(Pawn p,DamageInfo? dinfo,Hediff culprit){
   if(p==null || p.Dead || p.health==null || dinfo==null && culprit==null)return false;
   if(dinfo.HasValue && (dinfo.Value.Def==DamageDefOf.ExecutionCut || dinfo.Value.Def==DamageDefOf.SurgicalCut))return false;
   if(!FateReady(p,out var h))return false;
   if(!Rescue(p))return false;
   h.fateTick=Find.TickManager.TicksGame;
   Text(p,"命运之恩");
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"本该死去，命运之恩让其倒地幸存（伤口已止血）。"+FateCooldownDays+" 天后才能再次生效。",p,MessageTypeDefOf.PositiveEvent);
   return true;
  }
  // 1.58.0：救命的部分拆出来，预言之子的“命运未尽”共用（HeroDestiny.TryFate）。
  public static bool Rescue(Pawn p){
   var hs=p.health.hediffSet;
   // 1. 致命的缺失部位（心脏、大脑等）恢复
   foreach(var m in hs.hediffs.OfType<Hediff_MissingPart>().Where(m=>m.Part!=null && Vital(m.Part)).ToList())p.health.RemoveHediff(m);
   // 2. 伤口：要害部位最多剩一半伤害，其他部位至少留 1 点生命
   foreach(var g in hs.hediffs.OfType<Hediff_Injury>().Where(i=>i.Part!=null).GroupBy(i=>i.Part).ToList()){
    float max=g.Key.def.GetMaxHealth(p),dmg=g.Sum(i=>i.Severity),limit=Vital(g.Key)?max*.5f:Mathf.Max(0f,max-1f);
    if(dmg>limit && dmg>0f)foreach(var i in g)i.Severity*=limit/dmg;
   }
   var loss=hs.GetFirstHediffOfDef(HediffDefOf.BloodLoss);if(loss!=null && loss.Severity>.4f)loss.Severity=.4f;
   // 3. 止血
   foreach(var i in hs.hediffs.OfType<Hediff_Injury>().Where(i=>i.Bleeding).ToList())i.Tended(.5f,1f);
   if(p.health.ShouldBeDead())return false;
   var down=Items.Hediff("LP_BoonFateDowned");if(down!=null){var old=hs.GetFirstHediffOfDef(down);if(old!=null)p.health.RemoveHediff(old);p.health.AddHediff(down);}
   return true;
  }
  static bool Vital(BodyPartRecord part){var tags=part?.def?.tags;if(tags==null)return false;for(int i=0;i<tags.Count;i++)if(tags[i].vital)return true;return false;}

  // ---------- 战斗之恩：击倒/击杀敌人后自身每处伤口治疗该部位 5% 生命 ----------
  public static int CombatHeal(Pawn a,Pawn victim){
   if(a==null || a.Dead || victim==null || a==victim || !Has(a,Combat) || !victim.HostileTo(a))return 0;
   int n=0;
   foreach(var i in a.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>!i.IsPermanent() && i.Severity>0f).ToList()){
    float max=i.Part==null?10f:i.Part.def.GetMaxHealth(a);i.Heal(Mathf.Max(.5f,max*CombatHealFraction));n++;
   }
   if(n>0)Text(a,"战斗之恩");
   return n;
  }

  // ---------- 人形击杀里程碑 ----------
  // 1.50.0：20 级以上按传奇经验表（20 级非传奇的经验照常累积，按 21 级算）。
  public static float NextLevelXp(Hediff_Adventurer h)=>h==null || h.level>=Legend.MaxLevel?0f:Legend.Step(h.level+1);
  // 返回这次新达到的最高门槛（0 = 没有）。一次跨过多个门槛（例如旧存档）只给最高那个的经验；1000 杀另给 1 个恩赐点。
  public static int CheckKills(Pawn p){
   if(!KillMilestonesOn || p?.records==null)return 0;var h=Adventurer.Get(p);if(h==null)return 0;
   int kills=p.records.GetAsInt(RecordDefOf.KillsHumanlikes),best=0;
   int legendBonus=0;
   foreach(int t in KillMilestones)if(kills>=t && !h.milestonesDone.Contains("kills"+t)){h.milestonesDone.Add("kills"+t);best=t;if(t==BoonKills)h.boonPoints++;if(t>=500 && h.legendary)legendBonus+=Legend.KillBonusFor(h);}
   if(best==0)return 0;
   Legend.Grant(h,legendBonus); // 1.50.0：传奇的 500/1000 杀各 +3 传奇属性点（成为传奇前达到的由 Legend.Became 补）
   float xp=NextLevelXp(h)*KillMilestoneShare;if(xp>0f)Adventurer.GainXP(p,xp,false);
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)
    Find.LetterStack.ReceiveLetter(p.LabelShort+"：百战 "+best,p.LabelShort+"已经击倒了 "+kills+" 个人形敌人（里程碑 "+best+"）。\n\n获得冒险者经验 "+xp.ToString("0")+"（下一级所需的 30%）。"+(best==BoonKills?"\n\n千人斩：获得 1 个史诗恩赐点，可以再选一个恩赐。":"")+(legendBonus>0?"\n\n传奇属性点 +"+legendBonus+"。":""),LetterDefOf.PositiveEvent,p);
   return best;
  }

  // ---------- 宿敌冠军 ----------
  public static int NemesisLevel()=>Mathf.Min(Legend.EnemyOn?Legend.MaxLevel:Adventurer.MaxLevel,AdventurerEnemies.ColonyMaxLevel()+NemesisLevelOffset); // 1.50.0：可到传奇等级
  public static bool NemesisReady()=>NemesisOn && AdventurerEnemies.ColonyMaxLevel()>=NemesisMinLevel;
  // 冠军：剧情等级（不受上限）、至少 1 个专长、1 个史诗恩赐（不朽除外）。
  public static Hediff_Adventurer MakeChampion(Pawn p,int level){
   var h=Adventurer.Get(p)??Adventurer.TryAdd(p,1,true);if(h==null)return null;
   h.storyLevel=true;h.nemesis=true;Legend.RaiseEnemy(h,level);
   if(h.feats.Count==0){var f=Feats.All.Where(x=>!h.HasFeat(x) && x.Worker.Unmet(p,h)==null).ToList().RandomElementWithFallback();if(f!=null)h.AddFeat(f);}
   if(h.boons.Count==0){h.boonPoints=Math.Max(1,h.boonPoints);h.AutoBoon();}
   HeroTraits.EnemyBoss(p);HeroTraits.EnemyDeed(p); // 1.58.0：50% 金 / 紫强力特性，30% 一个传说事迹
   h.Dirty();return h;
  }
  public static Pawn SpawnNemesis(Map map){
   if(map==null)return null;var f=Ascension.FallbackEnemy();if(f==null)return null;
   var pawns=Ascension.RaidPawns(f,map,Ascension.Points(map,600f,.6f));
   var champ=pawns.Where(p=>Adventurer.Eligible(p)).OrderByDescending(p=>p.kindDef.factionLeader).ThenByDescending(p=>p.kindDef.combatPower).FirstOrDefault();
   if(champ==null){foreach(var p in pawns)Discard(p);return null;}
   var followers=pawns.Where(p=>p!=champ).OrderByDescending(p=>p.kindDef.combatPower).Take(MaxFollowers).ToList();
   foreach(var p in pawns)if(p!=champ && !followers.Contains(p))Discard(p);
   var h=MakeChampion(champ,NemesisLevel());
   var all=new List<Pawn>{champ};all.AddRange(followers);
   Ascension.Arrive(map,f,all);
   Find.LetterStack.ReceiveLetter("宿敌冠军："+champ.LabelShort,f.Name+"听说了殖民地冒险者的名声，派出了他们的冠军"+champ.LabelShort+"（"+(h!=null && h.level>Adventurer.MaxLevel?"传奇 Lv "+h.level:"冒险者 "+(h?.level??0)+" 级")+(h!=null && h.feats.Count>0?"，专长 "+string.Join("、",h.feats.Select(x=>x.label)):"")+(h!=null && h.boons.Count>0?"，"+string.Join("、",h.boons.Select(x=>x.label)):"")+HeroTraits.LetterNote(champ)+"）带着 "+followers.Count+" 名随从前来挑战。\n\n击杀冠军的人获得下一级所需经验的 50% 和一件魔法物品。",LetterDefOf.ThreatBig,new LookTargets(all.Cast<Thing>()),f);
   return champ;
  }
  static void Discard(Pawn p){if(p==null || p.Destroyed)return;if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);p.Destroy();}
  public static int attunerDrops; // 自检计数
  public static ThingDef RandomMagicItemDef()=>DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.category==ThingCategory.Item && d.tradeTags!=null && d.tradeTags.Contains("LP_MagicItem")).RandomElementWithFallback();
  // 击杀者：下一级所需经验 × 50%；魔法物品放在冠军倒下的地方（没有地图时给击杀者背包）。
  public static Thing NemesisReward(Pawn killer,Map map,IntVec3 at){
   float xp=0f;var h=Adventurer.Get(killer);
   bool legendPts=false;
   if(h!=null && killer.Faction!=null && killer.Faction.IsPlayer){xp=NextLevelXp(h)*NemesisShare;if(xp>0f)Adventurer.GainXP(killer,xp,false);if(h.legendary){Legend.Grant(h,Legend.NemesisBonus);legendPts=true;}}
   // 1.50.0：30% 掉一块传奇重铸之石
   Thing stone=null;if(Rand.Chance(Legend.StoneDropChance) && (stone=Legend.MakeStone())!=null){if(map!=null && at.IsValid)GenPlace.TryPlaceThing(stone,at,map,ThingPlaceMode.Near);else if(killer?.inventory!=null)killer.inventory.innerContainer.TryAdd(stone);}
   var d=RandomMagicItemDef();Thing item=null;
   if(d!=null){
    item=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);item.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Outsider);
    if(map!=null && at.IsValid)GenPlace.TryPlaceThing(item,at,map,ThingPlaceMode.Near);
    else if(killer?.inventory!=null)killer.inventory.innerContainer.TryAdd(item);
   }
   Growth.NemesisDrop(killer,map,at); // 1.58.3 25% 属性典籍、10% 传承武器
   // 1.58.0 同调者：击杀者 20% 多掉一件魔法物品。
   if(killer!=null && HeroCombat.ExtraRings(killer)>0 && (HeroCombat.TestRoll??Rand.Value)<HeroCombat.AttunerLoot){
    var d2=RandomMagicItemDef();if(d2!=null){var extra=ThingMaker.MakeThing(d2,d2.MadeFromStuff?GenStuff.DefaultStuffFor(d2):null);if(map!=null && at.IsValid)GenPlace.TryPlaceThing(extra,at,map,ThingPlaceMode.Near);else killer.inventory?.innerContainer.TryAdd(extra);attunerDrops++;}
   }
   if(killer!=null && !Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("宿敌冠军倒下了",killer.LabelShort+"击杀了宿敌冠军。"+(xp>0f?"\n\n获得冒险者经验 "+xp.ToString("0")+"。":"")+(legendPts?"\n\n传奇属性点 +"+Legend.NemesisBonus+"。":"")+(item!=null?"\n\n冠军留下了"+item.LabelCap+(stone!=null?"和"+stone.LabelCap:"")+"。":""),LetterDefOf.PositiveEvent,item!=null && item.Spawned?new LookTargets(item):new LookTargets(killer));
   return item;
  }

  // ---------- 挑战等级 CR（非人形 DND 生物，仅显示） ----------
  public static string CR(float combatPower){float v=combatPower/25f;if(v>=1f)return Mathf.Clamp(Mathf.RoundToInt(v),1,30).ToString();return v>=.5f?"1/2":"1/4";}
  public static bool ShowsCR(Pawn p)=>p?.RaceProps!=null && (Legendary.Is(p) || !p.RaceProps.Humanlike && p.def.defName.StartsWith("LP_") && (p.kindDef?.combatPower??0f)>0f); // 1.52.0：传奇单位（LP_LegendaryExtension）用 XML 的 cr

  public static GameComponent_Epic State=>GameComp<GameComponent_Epic>.Get();
 }

 // 排期：宿敌冠军每 45～60 天检查一次；传奇缓存（心情用）每 250 刻刷新。
 public class GameComponent_Epic : GameComponent {
  public int nextNemesisTick=-1;public bool anyLegend;
  public GameComponent_Epic(Game game){}
  public void RefreshLegend(){anyLegend=Epic.ColonyHasLegend();}
  public void Schedule(){nextNemesisTick=Find.TickManager.TicksGame+Mathf.RoundToInt(Rand.Range(Epic.NemesisMinDays,Epic.NemesisMaxDays)*GenDate.TicksPerDay);}
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;
   if(now%250==31)RefreshLegend();
   if(now%2000!=77)return;
   if(nextNemesisTick<0){Schedule();return;}
   if(now<nextNemesisTick)return;
   Schedule();
   if(!Epic.NemesisReady())return;
   var map=Find.Maps.Where(m=>m.IsPlayerHome && m.mapPawns.FreeColonistsSpawnedCount>0).RandomElementWithFallback();var def=DefDatabase<IncidentDef>.GetNamedSilentFail("LP_NemesisChampion");
   if(map==null || def==null)return;
   var parms=StorytellerUtility.DefaultParmsNow(def.category,map);
   if(def.Worker.CanFireNow(parms))def.Worker.TryExecute(parms);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref nextNemesisTick,"lpNextNemesis",-1);}
 }

 public class IncidentWorker_NemesisChampion : IncidentWorker {
  protected override bool CanFireNowSub(IncidentParms parms)=>parms.target is Map && Epic.NemesisReady() && Ascension.FallbackEnemy()!=null;
  protected override bool TryExecuteWorker(IncidentParms parms)=>Epic.SpawnNemesis(parms.target as Map)!=null;
 }

 // 与传奇同行（殖民地全员 +3）、传奇的名声（非本殖民地的人对传奇好感 +10）。
 public class ThoughtWorker_LegendCompanion : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.IsFreeColonist && Adventurer.Enabled && Epic.State?.anyLegend==true;
 }
 public class ThoughtWorker_LegendRenown : ThoughtWorker {
  protected override ThoughtState CurrentSocialStateInternal(Pawn p,Pawn other)=>other?.Faction!=null && other.Faction.IsPlayer && p.Faction!=other.Faction && Epic.IsLegend(other);
 }

 // ==================== Harmony ====================
 // 命运之恩：最先跑，成功时跳过原版死亡。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class EpicFate {
  [HarmonyPriority(Priority.First)]
  static bool Prefix(Pawn __instance,DamageInfo? dinfo,Hediff exactCulprit){
   if(__instance.Dead)return true;
   if(Feats2.TryOath(__instance,dinfo,exactCulprit))return false; // 1.58.1 命匣之誓
   if(HeroDestiny.TryFate(__instance,dinfo,exactCulprit))return false; // 1.58.0 预言之子“命运未尽”先判（冷却 20/12 天，比命运之恩短）；它救下了就不再用命运之恩
   if(Epic.Has(__instance,Epic.Fate) && Epic.TryFate(__instance,dinfo,exactCulprit))return false;
   return !MoreTraits.TryUnyielding(__instance,dinfo,exactCulprit); // 1.79.0 百折不挠：命运之恩没救下时才轮到它
  }
 }
 // 死亡后：传奇陨落、击杀里程碑、宿敌冠军奖励、战斗之恩。
 public class EpicKillState {public bool legend,nemesis,humanlike;public Pawn killer;public Map map;public IntVec3 at;}
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class EpicKill {
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out EpicKillState __state){
   __state=null;if(__instance.Dead || !Adventurer.Enabled)return;
   var h=Adventurer.Get(__instance);
   __state=new EpicKillState{legend=h!=null && h.legendary && __instance.Faction!=null && __instance.Faction.IsPlayer,nemesis=h!=null && h.nemesis,humanlike=__instance.RaceProps?.Humanlike==true,killer=dinfo?.Instigator as Pawn,map=__instance.MapHeld,at=__instance.PositionHeld};
  }
  static void Postfix(Pawn __instance,EpicKillState __state){
   if(__state==null || !__instance.Dead)return;
   if(__state.legend)Epic.LegendFell(__instance);
   var k=__state.killer;
   if(k!=null && k!=__instance){
    Epic.CombatHeal(k,__instance);
    if(__state.humanlike && k.Faction!=null && k.Faction.IsPlayer)Epic.CheckKills(k);
    if(__state.nemesis)Epic.NemesisReward(k.Faction!=null && k.Faction.IsPlayer?k:null,__state.map,__state.at);
   }else if(__state.nemesis)Epic.NemesisReward(null,__state.map,__state.at);
  }
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class EpicCombatDowned {
  static void Postfix(Pawn ___pawn,DamageInfo? dinfo){if(___pawn!=null && ___pawn.Downed && dinfo?.Instigator is Pawn a)Epic.CombatHeal(a,___pawn);}
 }
 // 不朽之恩：成年后不再衰老。
 [HarmonyPatch(typeof(Pawn_AgeTracker),"get_BiologicalTicksPerTick")]
 public static class EpicAgeless {static void Postfix(Pawn ___pawn,ref float __result){if(__result>0f && ___pawn?.ageTracker!=null && ___pawn.ageTracker.Adult && Epic.Has(___pawn,Epic.Immortal))__result=0f;}}
 // 挑战等级。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetInspectString))]
 public static class EpicCRInspect {
  static void Postfix(Pawn __instance,ref string __result){
   if(!Adventurer.Enabled || !Epic.ShowsCR(__instance))return;
   if(Legendary.Is(__instance)){var line=Legendary.InspectLine(__instance);__result=__result.NullOrEmpty()?line:line+"\n"+__result;return;} // 1.52.0：传奇单位放第一行，带冒险者等级
   string s="挑战等级 CR "+Epic.CR(__instance.kindDef.combatPower);
   __result=__result.NullOrEmpty()?s:__result+"\n"+s;
  }
 }

 // ---------- 设置（画在“事件”里） ----------
 public partial class Settings {
  public int adventurerKillMilestones=-1,adventurerNemesis=-1;
  void ExposeEpic(){Scribe_Values.Look(ref adventurerKillMilestones,"adventurerKillMilestones",-1);Scribe_Values.Look(ref adventurerNemesis,"adventurerNemesis",-1);}
 }
 public static class EpicSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;bool ikm=Epic.IkmActive;string note=ikm?"（检测到百战觉醒，默认关）":"";
   bool k=Epic.On(s.adventurerKillMilestones,ikm),was=k;
   l.CheckboxLabeled("冒险者击杀里程碑"+note,ref k,"殖民者的人形击杀达到 25、50、100、250、500、1000 时发信件，并一次性获得下一级所需经验的 30%；1000 杀额外得 1 个史诗恩赐点（可以选第二个恩赐）。\n检测到“百战觉醒”时默认关闭，避免两套里程碑重复。");
   if(k!=was)s.adventurerKillMilestones=k?1:0;
   bool n=Epic.On(s.adventurerNemesis,ikm);was=n;
   l.CheckboxLabeled("宿敌冠军"+note,ref n,"殖民地最高冒险者等级 ≥ 12 时，每 45～60 天敌对派系派出一名冠军（等级 = 殖民地最高 + 2，最高 20，带专长和史诗恩赐）和少量随从。击杀者获得下一级所需经验的 50% 和一件魔法物品。\n检测到“百战觉醒”时默认关闭。");
   if(n!=was)s.adventurerNemesis=n?1:0;
  }
 }
}
