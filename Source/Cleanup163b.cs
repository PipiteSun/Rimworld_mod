using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.63.0 清理版第二批：半巫妖谈判、吞眼魔脑髓、拉洛克使者卖情报、纳巴苏挑离群目标、敌方卓尔用黑暗术。

 // ---------- 巫妖与半巫妖谈判 ----------
 // 本方巫妖 10 格内有没被激怒（没受过伤）的半巫妖：成功率 50% + 社交×3% + 冒险者等级×1%（最多 90%）。
 // 成功：半巫妖离开地图，留下 1 份秘术残卷或 1 块神迹残片；失败：它照常敌对，并立刻做一次传奇动作。每个半巫妖只能谈一次。
 public static class DemilichParley {
  public const float Radius=10f,Base=.5f,PerSocial=.03f,PerLevel=.01f,Cap=.9f;
  public static float Chance(Pawn lich){
   int social=lich?.skills?.GetSkill(SkillDefOf.Social)?.Level??0;int level=Adventurer.Get(lich)?.level??0;
   return Mathf.Min(Cap,Base+PerSocial*social+PerLevel*level);
  }
  public static Hediff_MonsterBody Body(Pawn d)=>d?.health?.hediffSet?.hediffs.OfType<Hediff_MonsterBody>().FirstOrDefault();
  public static bool Provoked(Pawn d)=>d.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury);
  public static bool CanParley(Pawn lich,Pawn d)=>lich?.Map!=null && d!=null && d.Map==lich.Map && Monsters.Is(d,Monsters.Demilich) && !d.Dead && !d.Downed && Body(d)?.parleyed==false && !Provoked(d) && d.Position.InHorDistOf(lich.Position,Radius);
  public static IEnumerable<Pawn> Candidates(Pawn lich)=>lich?.Map==null?Enumerable.Empty<Pawn>():lich.Map.mapPawns.AllPawnsSpawned.Where(d=>CanParley(lich,d)).ToList();
  // 返回 true = 谈成。
  public static bool Resolve(Pawn lich,Pawn d,float roll){
   var body=Body(d);if(body==null || body.parleyed)return false;body.parleyed=true;
   if(roll<Chance(lich)){
    var map=d.Map;var at=d.Position;
    Thing gift;Rand.PushState(d.thingIDNumber^0x163B);try{gift=Rand.Bool?Netheril.MakeArcane():null;}finally{Rand.PopState();}
    if(gift==null && Faith.ShardDef!=null)gift=ThingMaker.MakeThing(Faith.ShardDef);
    if(gift!=null)GenPlace.TryPlaceThing(gift,at,map,ThingPlaceMode.Near);
    FleckMaker.Static(at,map,FleckDefOf.PsycastAreaEffect,2f);
    d.DeSpawn();if(!Find.WorldPawns.Contains(d))Find.WorldPawns.PassToWorld(d,PawnDiscardDecideMode.Discard);
    if(!Adventurer.TestSuppressed)Messages.Message(lich.LabelShort+"和半巫妖谈妥了：它沉回了墓穴深处，留下了"+(gift?.LabelShort??"一句咒语")+"。",gift!=null?new LookTargets(gift):new LookTargets(lich),MessageTypeDefOf.PositiveEvent,false);
    return true;
   }
   var ext=Legendary.Ext(d);
   var w=ext?.actions.Select(a=>Legendary.Worker(a.action)).FirstOrDefault(x=>x!=null && x.CanDo(d,lich));
   w?.Do(d,lich);
   if(!Adventurer.TestSuppressed)Messages.Message("半巫妖拒绝了"+lich.LabelShort+"："+(w!=null?"它立刻发动了“"+w.Label+"”！":"它被激怒了！"),d,MessageTypeDefOf.ThreatBig,false);
   return false;
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class DemilichParleyGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   foreach(var g in __result)yield return g;
   var p=__instance;
   if(p==null || !p.Spawned || p.Faction!=Faction.OfPlayer || Necro.Lich(p)==null || Find.Selector.SingleSelectedThing!=p || !Monsters.DemilichOn)yield break;
   foreach(var d in DemilichParley.Candidates(p)){
    var target=d;
    yield return new Command_Action{defaultLabel="与半巫妖谈判（成功率 "+DemilichParley.Chance(p).ToStringPercent("0")+"）",defaultDesc="巫妖对巫妖：劝"+d.LabelShort+"放过你们。成功率 50% + 社交×3% + 冒险者等级×1%，最多 90%。\n成功：它回到墓穴深处，留下 1 份秘术残卷或 1 块神迹残片。\n失败：它立刻发动一次传奇动作。每个半巫妖只能谈一次。",
     icon=ContentFinder<Texture2D>.Get("UI/Commands/AttackMelee",false)??BaseContent.BadTex,action=()=>DemilichParley.Resolve(p,target,Rand.Value)};
   }
  }
 }

 // ---------- 夺心魔吞噬眼魔脑髓 ----------
 // 吞噬眼魔（活体或新鲜尸体）后脑髓补满，并得到 10 天“眼魔之识”（心灵敏感度 +20%、能力冷却 -20%）。
 public static class BeholderBrain {
  public const float CooldownFactor=.8f;
  static HediffDef insight;public static HediffDef Insight=>insight??(insight=DefDatabase<HediffDef>.GetNamedSilentFail("LP_BeholderInsight"));
  public static void Apply(Pawn flayer){
   var g=MindFlayer.Cerebrum(flayer);if(g!=null){g.Value=g.Max;g.SyncHunger();}
   if(Insight==null)return;var h=flayer.health.hediffSet.GetFirstHediffOfDef(Insight);
   if(h==null)flayer.health.AddHediff(Insight);else{var c=h.TryGetComp<HediffComp_Disappears>();if(c!=null)c.ticksToDisappear=c.Props.disappearsAfterTicks.max;}
  }
 }
 [HarmonyPatch(typeof(MindFlayer),nameof(MindFlayer.Devour))]
 public static class BeholderBrainDevour {
  static void Prefix(Thing t,out bool __state){var p=t as Pawn??(t as Corpse)?.InnerPawn;__state=Monsters.Is(p,Monsters.Beholder);}
  static void Postfix(Pawn caster,float __result,bool __state){if(__state && __result>0f && caster!=null && !caster.Dead){BeholderBrain.Apply(caster);if(caster.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(caster.LabelShort+"吞下了眼魔的脑髓：脑髓补满，得到 10 天“眼魔之识”。",caster,MessageTypeDefOf.PositiveEvent,false);}}
 }
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.StartCooldown))]
 public static class BeholderInsightCooldown {
  static void Prefix(RimWorld.Ability __instance,ref int ticks){if(ticks>0 && BeholderBrain.Insight!=null && __instance?.pawn?.health?.hediffSet?.HasHediff(BeholderBrain.Insight)==true)ticks=Math.Max(1,Mathf.RoundToInt(ticks*BeholderBrain.CooldownFactor));}
 }

 // ---------- 拉洛克使者卖情报 ----------
 // 使者来访时信件多一个选项“购买情报（1500 白银）”：揭示一个眼魔巢穴 / 无尽墓穴 / 龙巢（已经有显露的地点就指给你看，没有就按原来的条件生成；殖民地等级不够的不出）。每次来访一次。
 public static class LarlochIntel {
  public const int Price=1500,BeholderLevel=16,TombLevel=24;
  public static List<string> Kinds(){
   var l=new List<string>();int lv=AdventurerEnemies.ColonyMaxLevel();
   if(Monsters.BeholderOn && lv>=BeholderLevel)l.Add(Monsters.Beholder);
   if(Monsters.DemilichOn && lv>=TombLevel)l.Add(Monsters.Demilich);
   if(Dragons.LairOn && lv>=Dragons.AncientLevel)l.Add("dragon");
   return l;
  }
  public static int Silver(Map map)=>map==null?0:map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t=>!t.IsForbidden(Faction.OfPlayer) && t.Spawned && map.areaManager.Home[t.Position]).Sum(t=>t.stackCount);
  public static string Blocked(NetherilQuestState st,Map map){
   if(st?.envoy!=null && st.intelEnvoyId==st.envoy.thingIDNumber)return "这次来访已经买过情报了";
   if(Kinds().Count==0)return "拉洛克没有你们够格知道的情报（殖民地最高冒险者等级不够，或传奇怪 / 真龙内容没开）";
   if(Silver(map)<Price)return "家园区里的白银不够 "+Price;
   return null;
  }
  public static string Buy(NetherilQuestState st,Map map,string kind=null){
   if(Blocked(st,map)!=null)return null;
   int left=Price;foreach(var t in map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t=>!t.IsForbidden(Faction.OfPlayer) && t.Spawned && map.areaManager.Home[t.Position]).ToList()){if(left<=0)break;int take=Math.Min(left,t.stackCount);t.SplitOff(take).Destroy();left-=take;}
   if(st.envoy!=null)st.intelEnvoyId=st.envoy.thingIDNumber;
   kind=kind??Kinds().RandomElement();const string why="拉洛克的使者收下白银，低声说出了一个地点";
   if(kind=="dragon"){
    var have=Dragons.State?.lairs.FirstOrDefault(l=>l.site!=null && !l.site.Destroyed);
    if(have!=null){Find.LetterStack.ReceiveLetter("龙巢情报",why+"：那座已经显露的龙巢。",LetterDefOf.NeutralEvent,new LookTargets(have.site));return "dragon";}
    return Dragons.RevealLair(Dragons.Colors.RandomElement(),2,why)!=null?"dragon":null;
   }
   var old=Monsters.State?.sites.FirstOrDefault(s=>s.kind==kind && s.site!=null && !s.site.Destroyed);
   if(old!=null){Find.LetterStack.ReceiveLetter(kind==Monsters.Beholder?"眼魔巢穴情报":"无尽墓穴情报",why+"：那处已经显露的地点。",LetterDefOf.NeutralEvent,new LookTargets(old.site));return kind;}
   return Monsters.RevealSite(kind,why)!=null?kind:null;
  }
  public static DiaOption Option(NetherilQuestState st,Map map,Action unused){
   var o=new DiaOption("购买情报（"+Price+" 白银）"){action=()=>{if(Buy(st,map)==null)Messages.Message("没能买到情报。",MessageTypeDefOf.RejectInput,false);},resolveTree=true};
   var why=Blocked(st,map);if(why!=null)o.Disable(why);
   return o;
  }
 }

 // ---------- 纳巴苏：优先攻击、凝视离群的目标 ----------
 // 离群 = 5 格内没有和它同派系、没倒地的人。
 public static class NabassuTactics {
  public const float Alone=5f,SearchRadius=30f;
  public static bool Isolated(Pawn t)=>t?.Map!=null && !GenRadial.RadialDistinctThingsAround(t.Position,t.Map,Alone,true).OfType<Pawn>().Any(o=>o!=t && !o.Dead && !o.Downed && o.Faction!=null && o.Faction==t.Faction);
  public static Pawn IsolatedTarget(Pawn nabassu)=>nabassu?.Map==null?null:nabassu.Map.mapPawns.AllPawnsSpawned.Where(t=>!t.Dead && !t.Downed && t.HostileTo(nabassu) && t.Position.InHorDistOf(nabassu.Position,SearchRadius) && Isolated(t) && nabassu.CanReach(t,PathEndMode.Touch,Danger.Deadly)).OrderBy(t=>t.Position.DistanceToSquared(nabassu.Position)).FirstOrDefault();
 }
 [HarmonyPatch(typeof(AttackTargetFinder),nameof(AttackTargetFinder.BestAttackTarget))]
 public static class NabassuTargetPick {
  static void Postfix(IAttackTargetSearcher searcher,ref IAttackTarget __result){
   if(!(searcher?.Thing is Pawn n) || !AbyssRaces.Is(n,AbyssRaces.Nabassu) || n.Faction==Faction.OfPlayer)return;
   if(__result?.Thing is Pawn cur && NabassuTactics.Isolated(cur))return;
   var alone=NabassuTactics.IsolatedTarget(n);if(alone!=null)__result=alone;
  }
 }
 // 石化凝视：附近有离群的目标时只凝视离群的。
 [HarmonyPatch(typeof(CompAbilityEffect_Abyss),nameof(CompAbilityEffect_Abyss.AICanTargetNow))]
 public static class NabassuGazePick {
  static void Postfix(CompAbilityEffect_Abyss __instance,LocalTargetInfo target,ref bool __result){
   if(!__result || !(__instance.parent?.pawn is Pawn n) || !AbyssRaces.Is(n,AbyssRaces.Nabassu) || !(target.Pawn is Pawn t))return;
   if(!NabassuTactics.Isolated(t) && NabassuTactics.IsolatedTarget(n)!=null)__result=false;
  }
 }

 // ---------- 敌方卓尔遇敌后施放黑暗术 ----------
 // 能力改为 AI 可用（冷却 1 天 = 每人每战最多一次）；只对身边 3 格内还有至少 2 名它的敌人的目标放（对方密集处）。
 public static class DrowDarknessAI {
  public const int MinCluster=3;
  public static int Cluster(Pawn caster,IntVec3 c,float r)=>caster?.Map==null?0:GenRadial.RadialDistinctThingsAround(c,caster.Map,r,true).OfType<Pawn>().Count(p=>!p.Dead && !p.Downed && p.HostileTo(caster));
 }
}
