using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.119.0 扭曲肉资源化 F 节：血肉塑形术（孵化槽、壁垒、畸变巨像、死灵滋养、巫妖吞食）。
 public static class FleshWorks {
  public const int MinionFeed=30,MinionMax=4,IncubateTicks=GenDate.TicksPerHour*6,MinionDays=3,GolemMinionDays=1,GolemMinions=2;
  public const int NourishMeat=5,DevourMeat=10;public const float DevourRadius=12f;
  public static readonly string[] NecroAbilities={"LP_SoulBolt","LP_FearGaze","LP_PowerWordKill","LP_IceRay","LP_MendConstruct"};
  public static PawnKindDef MinionKind=>DefCache<PawnKindDef>.Get("Fingerspike");
  public static Pawn SpawnMinion(Map map,IntVec3 near,int days){
   var kind=MinionKind;var h=DefCache<HediffDef>.Get("LP_FleshMinion");if(kind==null || h==null || map==null)return null;
   Pawn p;try{p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false));}catch(Exception e){Log.Warning("[DND边缘大陆] 血肉兽生成失败："+e.Message);return null;}
   var cell=CellFinder.StandableCellNear(near,map,4);if(!cell.IsValid)cell=near;
   GenSpawn.Spawn(p,cell,map);
   var hd=p.health.AddHediff(h);var comp=hd.TryGetComp<HediffComp_FleshMinion>();if(comp!=null)comp.ticksLeft=days*GenDate.TicksPerDay;
   var filth=DefCache<ThingDef>.Get("Filth_TwistedFlesh");if(filth!=null)FilthMaker.TryMakeFilth(cell,map,filth,2);
   return p;
  }
  public static bool IsMinion(Pawn p)=>p?.health?.hediffSet?.hediffs.Any(x=>x.def.defName=="LP_FleshMinion")==true;

  // ---------- F5 死灵滋养：知识死灵（魂封大脑 / 眼睛 / 手臂 / 心脏）每天吃 5 份扭曲肉，工作效率 +10% ----------
  public static bool IsScholar(Pawn p)=>Necro.Construct(p)?.kind=="Scholar";
  public static bool Nourishing(Pawn p){var c=TwistedMeat.Comp;return p!=null && c!=null && c.nourished.Contains(p.thingIDNumber);}
  public static bool Feed(Pawn p){
   if(p==null || !p.Spawned)return false;var h=DefCache<HediffDef>.Get("LP_FleshNourished");if(h==null)return false;
   var stock=TwistedMeat.Nearby(p.Map,p.Position,0f).Where(TwistedMeat.IsMeat).ToList();if(stock.Sum(t=>t.stackCount)<NourishMeat)return false;
   TwistedMeat.Take(stock,NourishMeat);TwistedMeat.Consumed(NourishMeat);
   var old=p.health.hediffSet.GetFirstHediffOfDef(h);if(old!=null)p.health.RemoveHediff(old);p.health.AddHediff(h);return true;
  }
  public static void HourTick(GameComponent_TwistedMeat c){
   if(c.nourishDay==TwistedMeat.Day)return;c.nourishDay=TwistedMeat.Day;
   FleshRegen.Daily();
   foreach(var map in Find.Maps)foreach(var p in map.mapPawns.AllPawnsSpawned.ToList())if(Nourishing(p) && !p.Dead)Feed(p);
   c.nourished.RemoveAll(id=>!Find.Maps.Any(m=>m.mapPawns.AllPawnsSpawned.Any(p=>p.thingIDNumber==id)) && !Find.WorldPawns.AllPawnsAlive.Any(p=>p.thingIDNumber==id));
  }

  // ---------- F6 巫妖 / 死灵之躯吞食扭曲肉：每 10 份恢复一个死灵能力的冷却 ----------
  public static Ability CoolingNecro(Pawn p)=>p?.abilities?.AllAbilitiesForReading.Where(a=>a.CooldownTicksRemaining>0 && (NecroAbilities.Contains(a.def.defName) || a.def.defName.StartsWith("LP_Necro"))).OrderByDescending(a=>a.CooldownTicksRemaining).FirstOrDefault();
  public static bool CanDevour(Pawn p)=>p!=null && (Necro.Lich(p)!=null || Items.Undead(p) && Necro.Construct(p)==null);
  public static string DevourBlocked(Pawn p){
   if(!CanDevour(p))return "只有巫妖和死灵之躯能吞食";
   if(!p.Spawned || p.Downed)return p.LabelShort+"现在不能吞食";
   if(CoolingNecro(p)==null)return "没有正在冷却的死灵能力";
   if(TwistedMeat.Nearby(p.Map,p.Position,DevourRadius).Where(TwistedMeat.IsMeat).Sum(t=>t.stackCount)<DevourMeat)return "12 格内的扭曲肉不够 "+DevourMeat+" 份";
   return null;
  }
  public static Ability Devour(Pawn p){
   if(DevourBlocked(p)!=null)return null;var a=CoolingNecro(p);
   TwistedMeat.Take(TwistedMeat.Nearby(p.Map,p.Position,DevourRadius).Where(TwistedMeat.IsMeat).ToList(),DevourMeat);TwistedMeat.Consumed(DevourMeat);
   a.ResetCooldown();if(TwistedMeat.AteTwisted!=null)p.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(TwistedMeat.AteTwisted);
   Items.Mote(p,a.def.LabelCap+" 冷却恢复",new Color(.8f,.4f,.45f));return a;
  }
 }

 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class FleshWorksPawnGizmos {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> gizmos,Pawn __instance){
   foreach(var g in gizmos)yield return g;
   var p=__instance;if(!TwistedMeat.Active || p.Faction!=Faction.OfPlayer || !p.Spawned)yield break;
   if(FleshWorks.IsScholar(p) && DefCache<ResearchProjectDef>.Get("LP_Research_FleshShaping")?.IsFinished==true){
    bool on=FleshWorks.Nourishing(p);
    yield return new Command_Toggle{defaultLabel="扭曲肉滋养",defaultDesc="打开后每天从地图上取 "+FleshWorks.NourishMeat+" 份扭曲肉喂给这名知识死灵：当天工作效率 +10%。没有扭曲肉就不喂，没有惩罚。",icon=TwistedMeat.Icon("TwistedMeat/NourishGizmo"),isActive=()=>on,toggleAction=()=>{var c=TwistedMeat.Comp;if(c==null)return;if(c.nourished.Contains(p.thingIDNumber))c.nourished.Remove(p.thingIDNumber);else{c.nourished.Add(p.thingIDNumber);FleshWorks.Feed(p);}}};
   }
   if(FleshWorks.CanDevour(p)){
    var cmd=new Command_Action{defaultLabel="吞食扭曲肉",defaultDesc="吞下 12 格内的 "+FleshWorks.DevourMeat+" 份扭曲肉：冷却最久的一个死灵能力（灵魂箭、恐惧凝视、律令死亡、寒冰射线、修补造物）立刻恢复。死灵之躯不会因此不舒服。",icon=TwistedMeat.Icon("TwistedMeat/DevourGizmo"),action=()=>FleshWorks.Devour(p)};
    string why=FleshWorks.DevourBlocked(p);if(why!=null)cmd.Disable(why);yield return cmd;
   }
  }
 }

 // ---------- F1 血肉孵化槽 ----------
 public class Building_FleshIncubator : Building {
  public int progress;public List<Pawn> minions=new List<Pawn>();
  public CompRefuelable Fuel=>GetComp<CompRefuelable>();
  public int Alive{get{minions.RemoveAll(p=>p==null || p.Dead || p.Destroyed);return minions.Count;}}
  public override void TickRare(){
   base.TickRare();if(!Spawned)return;var f=Fuel;
   if(f==null || f.Fuel<FleshWorks.MinionFeed || Alive>=FleshWorks.MinionMax){progress=0;return;}
   progress+=GenTicks.TickRareInterval;if(progress>=FleshWorks.IncubateTicks)Hatch();
  }
  public Pawn Hatch(){
   var f=Fuel;if(f==null || f.Fuel<FleshWorks.MinionFeed || Alive>=FleshWorks.MinionMax)return null;
   var p=FleshWorks.SpawnMinion(Map,Position,FleshWorks.MinionDays);progress=0;if(p==null)return null;
   f.ConsumeFuel(FleshWorks.MinionFeed);TwistedMeat.Consumed(FleshWorks.MinionFeed);minions.Add(p);
   Messages.Message("血肉孵化槽孵出了一只"+p.LabelShort+"。",p,MessageTypeDefOf.NeutralEvent,false);return p;
  }
  public override string GetInspectString(){
   var s=base.GetInspectString();var lines=new List<string>();if(!s.NullOrEmpty())lines.Add(s);
   lines.Add("孵出的血肉兽："+Alive+" / "+FleshWorks.MinionMax);
   if(Fuel!=null && Fuel.Fuel>=FleshWorks.MinionFeed && Alive<FleshWorks.MinionMax)lines.Add("孵化中："+((float)progress/FleshWorks.IncubateTicks).ToStringPercent());
   return string.Join("\n",lines);
  }
  // 2026-11-06 审查：烂掉的血肉兽连尸体一起销毁，存档前不先清掉会写进已销毁的引用（读档时报“无法解析引用”）。
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref progress,"lpProgress");if(Scribe.mode==LoadSaveMode.Saving)minions?.RemoveAll(p=>p==null || p.Dead || p.Destroyed);Scribe_Collections.Look(ref minions,"lpMinions",LookMode.Reference);if(Scribe.mode==LoadSaveMode.PostLoadInit && minions==null)minions=new List<Pawn>();}
 }
 // 血肉兽仆从：到时间就烂掉。
 public class HediffCompProperties_FleshMinion : HediffCompProperties {public HediffCompProperties_FleshMinion(){compClass=typeof(HediffComp_FleshMinion);}}
 public class HediffComp_FleshMinion : HediffComp {
  public int ticksLeft=GenDate.TicksPerDay*3;
  public override string CompLabelInBracketsExtra=>ticksLeft.ToStringTicksToPeriod();
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){
   ticksLeft-=delta;if(ticksLeft>0)return;var p=Pawn;if(p==null || p.Dead)return;
   var map=p.MapHeld;var cell=p.PositionHeld;p.Kill(null);
   if(p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();
   var filth=DefCache<ThingDef>.Get("Filth_TwistedFlesh");if(map!=null && filth!=null)FilthMaker.TryMakeFilth(cell,map,filth,3);
  }
  public override void CompExposeData(){base.CompExposeData();Scribe_Values.Look(ref ticksLeft,"lpTicksLeft");}
 }

 // ---------- F2 血肉壁垒：每天自修 10%，酸液伤害 ×2 ----------
 public class Building_FleshWall : Building {
  public const float RepairPerDay=.1f,AcidFactor=2f;float repair;
  public override void TickRare(){
   base.TickRare();if(HitPoints>=MaxHitPoints){repair=0f;return;}
   repair+=MaxHitPoints*RepairPerDay*GenTicks.TickRareInterval/GenDate.TicksPerDay;
   if(repair>=1f){int n=Mathf.FloorToInt(repair);repair-=n;HitPoints=Mathf.Min(MaxHitPoints,HitPoints+n);if(Spawned)Map.listerBuildingsRepairable.Notify_BuildingRepaired(this);}
  }
  public static bool IsAcid(DamageDef d)=>d!=null && d.defName.IndexOf("Acid",StringComparison.OrdinalIgnoreCase)>=0;
  public override void PreApplyDamage(ref DamageInfo dinfo,out bool absorbed){if(IsAcid(dinfo.Def))dinfo.SetAmount(dinfo.Amount*AcidFactor);base.PreApplyDamage(ref dinfo,out absorbed);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref repair,"lpRepair");}
 }

 // ---------- F4 畸变巨像 ----------
 public class HediffCompProperties_AberrantGolem : HediffCompProperties {public HediffCompProperties_AberrantGolem(){compClass=typeof(HediffComp_AberrantGolem);}}
 public class HediffComp_AberrantGolem : HediffComp {
  public const float HealPerHour=1.5f;
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){
   var p=Pawn;if(p==null || p.Dead || !p.IsHashIntervalTick(GenDate.TicksPerHour,delta))return;
   var inj=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h=>!h.IsPermanent()).OrderByDescending(h=>h.Severity).FirstOrDefault();inj?.Heal(HealPerHour);
  }
  public override void Notify_PawnDied(DamageInfo? dinfo,Hediff culprit){
   base.Notify_PawnDied(dinfo,culprit);var p=Pawn;var map=p?.MapHeld;if(map==null)return;var cell=p.PositionHeld;
   for(int i=0;i<FleshWorks.GolemMinions;i++)FleshWorks.SpawnMinion(map,cell,FleshWorks.GolemMinionDays);
   Messages.Message(p.LabelShort+"的残躯裂成了 "+FleshWorks.GolemMinions+" 只小血肉兽。",new LookTargets(cell,map),MessageTypeDefOf.NeutralEvent,false);
  }
 }
 public static class AberrantGolem {
  public const string Recipe="LP_MakeAberrantGolem";
  public static IEnumerable<Thing> Make(Building_Altar altar){
   var soul=altar?.Box?.soul;if(soul==null)yield break;
   var seed=ConstructSeed.Make("Golem",soul);var c=TwistedMeat.Comp;if(c!=null && !c.aberrantSeeds.Contains(seed.thingIDNumber))c.aberrantSeeds.Add(seed.thingIDNumber);
   yield return seed;
  }
  public static bool Marked(Thing seed){var c=TwistedMeat.Comp;return seed!=null && c!=null && c.aberrantSeeds.Contains(seed.thingIDNumber);}
  public static void MakeAberrant(Pawn golem){var h=DefCache<HediffDef>.Get("LP_AberrantGolem");if(golem!=null && h!=null && !golem.health.hediffSet.HasHediff(h))golem.health.AddHediff(h);}
 }
 // 挂在 GenRecipe.MakeRecipeProducts 的后缀上（死灵仪式台的前缀已经把结果换成 Rituals.Complete 的空迭代器；Complete 本身是小迭代器桩，会被内联，不能直接补）。
 [HarmonyPatch(typeof(GenRecipe),nameof(GenRecipe.MakeRecipeProducts))]
 public static class AberrantGolemComplete {
  static void Postfix(RecipeDef recipeDef,Pawn worker,List<Thing> ingredients,IBillGiver billGiver,ref IEnumerable<Thing> __result){
   var recipe=recipeDef;var altar=billGiver as Building_Altar;if(recipe?.defName!=AberrantGolem.Recipe)return;
   string blocked=Rituals.Blocked(recipe,altar,worker,ingredients);if(blocked!=null)throw new InvalidOperationException("Ritual completion was not validated: "+blocked);
   __result=AberrantGolem.Make(altar);
  }
 }
 [HarmonyPatch(typeof(ConstructSeed),nameof(ConstructSeed.Awaken))]
 public static class AberrantGolemAwaken {
  static void Prefix(ConstructSeed __instance,out int __state){__state=AberrantGolem.Marked(__instance)?__instance.thingIDNumber:-1;}
  static void Postfix(bool __result,Pawn result,int __state){
   if(__state<0 || !__result || result==null)return;AberrantGolem.MakeAberrant(result);TwistedMeat.Comp?.aberrantSeeds.Remove(__state);
  }
 }
 [HarmonyPatch(typeof(ConstructSeed),nameof(ConstructSeed.GetInspectString))]
 public static class AberrantSeedInspect {static void Postfix(ConstructSeed __instance,ref string __result){if(AberrantGolem.Marked(__instance))__result+="\n畸变巨像：唤醒后带“畸变血肉”（受伤 -17%、缓慢自愈、死亡时裂成 2 只小血肉兽）。";}}
}
