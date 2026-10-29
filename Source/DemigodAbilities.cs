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
 // 1.40.0 神职能力（Defs/Ascension.xml，22 个）：原版 RimWorld.Ability，不依赖 VPE，消耗半神的神力。
 // 强度 S = 基础 × 等级强度（1 + 0.25×(等级-1)）× 心灵敏感度（下限 0.2，不封顶）；伤害另 × 永夜冠冕 1.15。
 // 需求写“× Isekai 伤害系数（1.30.1 法师/牧师用的同一帮助函数）”：设备上 1.30.1 的法师/牧师能力并没有乘 Isekai 系数（只有战士的近战走原版近战结算），所以这里同样不乘。
 public class CompProperties_DivineAbility : CompProperties_AbilityEffect {
  public string effect;public float cost,amount,radius;public int duration;
  public CompProperties_DivineAbility(){compClass=typeof(CompAbilityEffect_Divine);}
 }
 public class Ability_Divine : RimWorld.Ability {
  public Ability_Divine():base(){}
  public Ability_Divine(Pawn pawn):base(pawn){}
  public Ability_Divine(Pawn pawn,AbilityDef def):base(pawn,def){}
  public Ability_Divine(Pawn pawn,Precept sourcePrecept):base(pawn,sourcePrecept){}
  public Ability_Divine(Pawn pawn,Precept sourcePrecept,AbilityDef def):base(pawn,sourcePrecept,def){}
  public CompProperties_DivineAbility Props=>def.comps.OfType<CompProperties_DivineAbility>().FirstOrDefault();
  public override bool GizmoDisabled(out string reason){if(base.GizmoDisabled(out reason))return true;reason=DemigodAbilities.Blocked(pawn,def);return reason!=null;}
  public override AcceptanceReport CanCast{get{var b=base.CanCast;if(!b.Accepted)return b;var r=DemigodAbilities.Blocked(pawn,def);return r==null?b:(AcceptanceReport)r;}}
  public override bool Activate(LocalTargetInfo target,LocalTargetInfo dest){
   var h=Demigods.Of(pawn);if(h==null || DemigodAbilities.Blocked(pawn,def)!=null)return false;
   h.Power-=Props?.cost??0f;
   return base.Activate(target,dest);
  }
 }
 public class CompAbilityEffect_Divine : CompAbilityEffect {
  public new CompProperties_DivineAbility Props=>(CompProperties_DivineAbility)props;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){base.Apply(target,dest);DemigodAbilities.Apply(parent.pawn,Props,target);}
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   if(!base.Valid(target,throwMessages))return false;
   var p=target.Pawn;var c=parent.pawn;string why=null;
   switch(Props.effect){
    case "UnfadingDawn":if(p==null || p.HostileTo(c))why="只能对友方施放。";break;
    case "JudgmentHammer":case "WillCrush":case "JudgmentGate":if(p==null || !p.HostileTo(c))why="只能对敌人施放。";break;
    case "Dominate":if(p==null || !p.HostileTo(c) || !p.RaceProps.Humanlike)why="只能对人形敌人施放。";else if(p.GetStatValue(StatDefOf.PsychicSensitivity)<=0f)why="心灵失聪的人不受支配。";break;
    case "ShadowStep":if(!target.Cell.IsValid || !target.Cell.Standable(c.Map) || target.Cell.GetFirstPawn(c.Map)!=null)why="那里站不了人。";break;
   }
   if(why!=null && throwMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);
   return why==null;
  }
 }

 public static class DemigodAbilities {
  public const float CrownDamage=1.15f,MinPsychic=.2f;
  static List<AbilityDef> all;
  public static List<AbilityDef> All=>all??(all=DefDatabase<AbilityDef>.AllDefs.Where(d=>d.abilityClass==typeof(Ability_Divine)).ToList());
  static DamageDef electric,force,psychic;
  public static DamageDef Electric=>electric??(electric=DefDatabase<DamageDef>.GetNamedSilentFail("ElectricalBurn")??DamageDefOf.Burn);
  public static DamageDef Force=>force??(force=DefDatabase<DamageDef>.GetNamedSilentFail("LP_Force")??DamageDefOf.Blunt);
  public static DamageDef Psychic=>psychic??(psychic=DefDatabase<DamageDef>.GetNamedSilentFail("LP_SilverPsychic")??DamageDefOf.Blunt);

  // 强度：damage=true 时再 × 永夜冠冕。
  public static float Strength(Pawn p,bool damage=true){
   var h=Demigods.Of(p);float s=(h?.Strength??1f)*Mathf.Max(MinPsychic,p?.GetStatValue(StatDefOf.PsychicSensitivity)??1f);
   if(damage && Items.Worn(p,"LP_EternalNightCrown")!=null)s*=CrownDamage;
   return s;
  }
  public static string Blocked(Pawn p,AbilityDef def){
   var h=Demigods.Of(p);if(h==null)return "只有半神能施放。";
   var props=def.comps?.OfType<CompProperties_DivineAbility>().FirstOrDefault();if(props==null)return null;
   if(h.Power+.001f<props.cost)return "神力不足：需要"+props.cost.ToString("0")+"，现在"+h.Power.ToString("0")+"。";
   if(AntimagicAt(p.MapHeld,p.PositionHeld) && p.HostileTo(Faction.OfPlayer))return "反魔场里不能施放。";
   return null;
  }
  static IEnumerable<Pawn> Near(Map map,IntVec3 c,float r)=>map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(map,c,r).Where(o=>!o.Dead);
  static void Mote(Thing t,string s)=>Items.Mote(t,s,new Color(1f,.85f,.45f));
  static void Hit(Pawn c,Pawn t,DamageDef d,float amount){if(t==null || t.Dead || amount<=0f)return;t.TakeDamage(new DamageInfo(d,amount,.3f,-1f,c));}
  public static float HealAll(Pawn t,float amount){
   float healed=0f;
   foreach(var inj in t.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(i=>i.Severity).ToList()){if(amount<=0f)break;float d=Mathf.Min(inj.Severity,amount);inj.Heal(d);amount-=d;healed+=d;}
   return healed;
  }

  public static void Apply(Pawn c,CompProperties_DivineAbility pr,LocalTargetInfo target){
   if(c==null || pr==null)return;var map=c.Map;var t=target.Pawn;float S=Strength(c),H=Strength(c,false);
   switch(pr.effect){
    // ---- 黎明 ----
    case "DawnLight":
     foreach(var o in Near(map,c.Position,pr.radius).ToList()){
      if(!o.HostileTo(c)){if(HealAll(o,15f*H)>0f)FleckMaker.ThrowMetaIcon(o.Position,map,FleckDefOf.HealingCross);}
      else if(Items.Undead(o) || Faith.HolyVulnerable(o))Hit(c,o,Faith.Radiant,20f*S);
     }
     FleckMaker.Static(c.Position,map,FleckDefOf.PsycastAreaEffect,pr.radius);break;
    case "UnfadingDawn":if(t!=null){Items.Give(t,"LP_UnfadingDawn",pr.duration);Mote(t,"不灭晨曦");}break;
    // ---- 正义 ----
    case "JudgmentHammer":if(t!=null){Hit(c,t,Faith.Radiant,35f*S);if(!t.Dead)t.stances?.stunner.StunFor(120,c,false,true);FleckMaker.Static(t.Position,map,FleckDefOf.PsycastAreaEffect,1.5f);}break;
    case "JusticeShield":foreach(var o in Near(map,c.Position,pr.radius).Where(o=>!o.HostileTo(c) && o.RaceProps.Humanlike).ToList())Items.Give(o,"LP_JusticeShield",pr.duration);FleckMaker.Static(c.Position,map,FleckDefOf.PsycastAreaEffect,pr.radius);break;
    // ---- 暴政 ----
    case "Dominate":Dominate(c,t,pr.duration);break;
    case "CrownOfDread":foreach(var o in Near(map,c.Position,pr.radius).Where(o=>o.HostileTo(c) && !o.Downed).ToList())o.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.PanicFlee,"恐惧之冠",true,true);FleckMaker.Static(c.Position,map,FleckDefOf.PsycastAreaEffect,pr.radius);break;
    // ---- 风暴 ----
    case "ThunderWrath":
     for(int i=0;i<3;i++){
      var cell=i==0?target.Cell:CellFinder.RandomClosewalkCellNear(target.Cell,map,3);if(!cell.InBounds(map))continue;
      map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningFlash(map));FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(),map,2f);FleckMaker.ThrowMicroSparks(cell.ToVector3Shifted(),map);
      foreach(var o in Near(map,cell,1.5f).ToList())if(o!=c)Hit(c,o,Electric,25f*S);
     }
     break;
    case "CallWind":{
     var storm=DefDatabase<WeatherDef>.GetNamedSilentFail("RainyThunderstorm");if(storm!=null){map.weatherManager.TransitionTo(storm);try{Traverse.Create(map.weatherDecider).Field("curWeatherDuration").SetValue(GenDate.TicksPerDay);}catch{}}
     foreach(var o in map.mapPawns.SpawnedPawnsInFaction(c.Faction).ToList())Items.Give(o,"LP_CallWindHaste",pr.duration);
     break;}
    // ---- 火焰 ----
    case "Meteor":GenExplosion.DoExplosion(target.Cell,map,pr.radius,DamageDefOf.Flame,c,Mathf.RoundToInt(40f*S),ignoredThings:new List<Thing>{c});break;
    case "ForgeBlessing":foreach(var o in Near(map,c.Position,pr.radius).Where(o=>!o.HostileTo(c) && o.RaceProps.Humanlike).ToList())Items.Give(o,"LP_ForgeBlessing",pr.duration);FleckMaker.Static(c.Position,map,FleckDefOf.PsycastAreaEffect,pr.radius);break;
    // ---- 野性 ----
    case "PackCall":PackCall(c,4,pr.duration);break;
    case "PredatorForm":Items.Give(c,"LP_PredatorForm",pr.duration);Mote(c,"掠食者之形");break;
    // ---- 心灵 ----
    case "PsionicBlast":
     foreach(var o in Near(map,c.Position,pr.radius).Where(o=>o.HostileTo(c)).ToList()){if(o.GetStatValue(StatDefOf.PsychicSensitivity)<=0f)continue;o.stances?.stunner.StunFor(180,c,false,true);Hit(c,o,Psychic,15f*S);}
     FleckMaker.Static(c.Position,map,FleckDefOf.PsycastAreaEffect,pr.radius);break;
    case "WillCrush":if(t!=null && t.GetStatValue(StatDefOf.PsychicSensitivity)>0f)t.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Berserk,"意志碾压",true,true);break;
    // ---- 死亡 ----
    case "SoulRest":{
     var tr=Ascension.Tracker;
     foreach(var o in Near(map,c.Position,pr.radius).Where(o=>o.HostileTo(c)).ToList()){
      if(Items.Undead(o) || Necro.Construct(o)!=null)Hit(c,o,Faith.Radiant,40f*S);
      if(!o.Dead && o.Downed && tr!=null)tr.restUntil[o.ThingID]=Find.TickManager.TicksGame+pr.duration;
     }
     foreach(var corpse in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Where(x=>x.Position.InHorDistOf(c.Position,pr.radius) && x.InnerPawn!=null && x.InnerPawn.HostileTo(c)).ToList())if(tr!=null)tr.restUntil[corpse.InnerPawn.ThingID]=Find.TickManager.TicksGame+pr.duration;
     FleckMaker.Static(c.Position,map,FleckDefOf.PsycastAreaEffect,pr.radius);break;}
    case "JudgmentGate":if(t!=null){if(t.health.summaryHealth.SummaryHealthPercent<.3f){t.Kill(new DamageInfo(Faith.Radiant,999f,1f,-1f,c));Mote(t,"审判");}else Hit(c,t,Faith.Radiant,30f*S);}break;
    // ---- 魔法 ----
    case "WeaveMissiles":{
     var foes=Near(map,c.Position,15f).Where(o=>o.HostileTo(c) && !o.Downed).ToList();
     for(int i=0;i<5 && foes.Count>0;i++){var o=foes.RandomElement();Hit(c,o,Force,8f*S);FleckMaker.ThrowMicroSparks(o.DrawPos,map);if(o.Dead || o.Downed)foes.Remove(o);}
     break;}
    case "Antimagic":{var tr=Ascension.Tracker;tr?.zones.Add(new AntimagicZone{map=map,center=c.Position,radius=pr.radius,until=Find.TickManager.TicksGame+pr.duration});FleckMaker.Static(c.Position,map,FleckDefOf.PsycastAreaEffect,pr.radius);break;}
    // ---- 黑暗 ----
    case "OblivionFog":foreach(var o in Near(map,target.Cell,pr.radius).Where(o=>o.HostileTo(c)).ToList())Items.Give(o,"LP_OblivionFog",pr.duration);FleckMaker.ThrowSmoke(target.Cell.ToVector3Shifted(),map,pr.radius);break;
    case "ShadowStep":{
     var cell=target.Cell;if(!cell.IsValid || !cell.Standable(map) || cell.GetFirstPawn(map)!=null)break;
     FleckMaker.ThrowSmoke(c.DrawPos,map,1.2f);c.Position=cell;c.Notify_Teleported(true,false);FleckMaker.ThrowSmoke(c.DrawPos,map,1.2f);
     var inv=DefDatabase<HediffDef>.GetNamedSilentFail("PsychicInvisibility");if(inv!=null){var h=HediffMaker.MakeHediff(inv,c);var dis=h.TryGetComp<HediffComp_Disappears>();if(dis!=null)dis.ticksToDisappear=480;c.health.AddHediff(h);}
     break;}
    // ---- 不死 ----
    case "SoulHarvest":{
     var box=Necro.Lich(c)?.box;int n=0;
     foreach(var o in Near(map,c.Position,pr.radius).Where(o=>o.HostileTo(c) && o.Downed && o.RaceProps.Humanlike).ToList()){o.Kill(null);n++;}
     if(n>0 && box!=null && !box.Destroyed)box.Feed(50f*n);
     FleckMaker.Static(c.Position,map,FleckDefOf.PsycastAreaEffect,pr.radius);
     if(n>0)Messages.Message(c.LabelShort+"收割了 "+n+" 个灵魂"+(box!=null?"，命匣 +"+(50*n)+" 献祭经验":"")+"。",c,MessageTypeDefOf.NeutralEvent,false);
     break;}
    case "DeadArmy":
     for(int i=0;i<4;i++){
      Pawn s;try{s=FactionConstructs.MakeBound("Skeleton",c,c.Faction);}catch(Exception e){Log.Warning("[DND边缘大陆] 亡者大军："+e.Message);break;}
      var cell=CellFinder.RandomClosewalkCellNear(c.Position,map,3);GenSpawn.Spawn(s,cell,map);FleckMaker.ThrowSmoke(cell.ToVector3Shifted(),map,1f);
      Items.Give(s,"LP_SummonedTemp",pr.duration);
     }
     break;
   }
  }
  public static void Dominate(Pawn c,Pawn t,int ticks){
   if(t==null || t.Dead || !t.RaceProps.Humanlike || t.GetStatValue(StatDefOf.PsychicSensitivity)<=0f)return;
   var orig=t.Faction;var h=(Hediff_DivineDominated)Items.Give(t,"LP_DivineDominated",ticks);if(h==null)return;h.original=orig;
   t.GetLord()?.Notify_PawnLost(t,PawnLostCondition.ChangedFaction);
   t.SetFaction(c.Faction);t.mindState?.mentalStateHandler?.CurState?.RecoverFromState();
   Mote(t,"被支配");
  }
  public static void PackCall(Pawn c,int n,int ticks){
   var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_Worg")??DefDatabase<PawnKindDef>.GetNamedSilentFail("Warg");if(kind==null || c.Map==null)return;
   for(int i=0;i<n;i++){
    var w=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,c.Faction,PawnGenerationContext.NonPlayer,c.Map.Tile,forceGenerateNewPawn:true));
    var cell=CellFinder.RandomClosewalkCellNear(c.Position,c.Map,4);GenSpawn.Spawn(w,cell,c.Map);
    if(w.training!=null){foreach(var td in DefDatabase<TrainableDef>.AllDefs)if(w.training.CanAssignToTrain(td).Accepted)w.training.Train(td,null,true);}
    Items.Give(w,"LP_SummonedTemp",ticks);
   }
  }
  // 反魔场
  public static bool AntimagicAt(Map map,IntVec3 cell){var t=Ascension.Tracker;if(t==null || map==null || t.zones.Count==0)return false;int now=Find.TickManager.TicksGame;foreach(var z in t.zones)if(z.map==map && now<z.until && z.center.InHorDistOf(cell,z.radius))return true;return false;}
 }

 // 支配：到时回原派系。
 public class Hediff_DivineDominated : HediffWithComps {
  public Faction original;
  public override void PostRemoved(){
   base.PostRemoved();
   var p=pawn;var f=original;
   Fiends.Defer(()=>{
    if(p==null || p.Dead || p.Destroyed || f==null || f.defeated || p.Faction==f)return;
    p.GetLord()?.Notify_PawnLost(p,PawnLostCondition.ChangedFaction);p.SetFaction(f);
    if(p.Spawned && f.HostileTo(Faction.OfPlayer))LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false,false,false),p.Map,new List<Pawn>{p});
    Messages.Message(p.LabelShort+"挣脱了支配，回到了"+f.Name+"。",p,MessageTypeDefOf.NeutralEvent,false);
   });
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref original,"lpOriginal");}
 }
 // 召来的座狼与骷髅：到时离开/崩解。
 public class Hediff_SummonedTemp : HediffWithComps {
  public override void PostRemoved(){
   base.PostRemoved();var p=pawn;
   Fiends.Defer(()=>{
    if(p==null || p.Dead || p.Destroyed)return;
    if(p.Spawned)FleckMaker.ThrowSmoke(p.DrawPos,p.Map,1.5f);
    if(Necro.Construct(p)!=null){p.Kill(null);p.Corpse?.Destroy();}else p.Destroy();
   });
  }
 }

 // 不灭晨曦：24 小时内第一次致命伤 → 改成不致命并止血（同凯兰沃赐福的写法）。
 [HarmonyPatch(typeof(DamageWorker_AddInjury),"FinalizeAndAddInjury",new Type[]{typeof(Pawn),typeof(Hediff_Injury),typeof(DamageInfo),typeof(DamageWorker.DamageResult)})]
 public static class UnfadingDawnSave {
  static void Prefix(Pawn __0,Hediff_Injury __1){
   var p=__0;if(p==null || p.Dead || __1==null || !Items.Has(p,"LP_UnfadingDawn"))return;
   if(!p.health.WouldDieAfterAddingHediff(__1))return;
   __1.Severity=Mathf.Min(__1.Severity,Mathf.Max(.01f,p.health.hediffSet.GetPartHealth(__1.Part)-1f));
   if(p.health.WouldDieAfterAddingHediff(__1))__1.Severity=.01f;
   var h=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_UnfadingDawn"));if(h!=null)p.health.RemoveHediff(h);
   Fiends.Defer(()=>{if(p.Dead)return;foreach(var b in p.health.hediffSet.hediffs.Where(x=>x.Bleeding).ToList())b.Tended(1f,1f);if(p.Spawned)FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,1.5f);Messages.Message("不灭晨曦护住了"+p.LabelShort+"：致命的一击没有夺走生命，伤口止住了血。",p,MessageTypeDefOf.PositiveEvent,false);});
  }
 }
 // 熔炉祝福、天穹之矛的近战附加伤害。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class DivineMeleeExtra {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){
   var a=__instance.CasterPawn;var t=target.Pawn;if(a==null || t==null || t.Dead || t==a)return;
   if(Items.Has(a,"LP_ForgeBlessing"))t.TakeDamage(new DamageInfo(DamageDefOf.Burn,6f,0f,-1f,a));
   var w=a.equipment?.Primary;var sp=w?.TryGetComp<CompSkySpear>();
   if(sp!=null && __instance.EquipmentSource==w && !t.Dead)sp.Strike(a,t);
  }
 }
 // 反魔场：区域里的敌人放不出原版能力；神明附体者的技能停摆。
 [HarmonyPatch(typeof(RimWorld.Ability),"get_CanCast")]
 public static class AntimagicCast {static void Postfix(RimWorld.Ability __instance,ref AcceptanceReport __result){var p=__instance?.pawn;if(__result.Accepted && p!=null && p.Spawned && p.HostileTo(Faction.OfPlayer) && DemigodAbilities.AntimagicAt(p.Map,p.Position))__result="反魔场";}}
 [HarmonyPatch(typeof(TwilightSkills),"Tick")]
 public static class AntimagicVessel {static bool Prefix(Hediff_DivineVessel __0)=>__0?.pawn==null || !__0.pawn.Spawned || !DemigodAbilities.AntimagicAt(__0.pawn.Map,__0.pawn.Position);}
 // 神职能力冷却：圣徒圣印 ×0.8。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.StartCooldown))]
 public static class DivineCooldown {static void Prefix(RimWorld.Ability __instance,ref int ticks){if(ticks>0 && __instance is Ability_Divine && Items.Worn(__instance.pawn,"LP_SaintSeal")!=null)ticks=Mathf.Max(1,Mathf.RoundToInt(ticks*.8f));}}
 // 灵魂安息：6 小时内不能复生、不能被吞脑。
 [HarmonyPatch(typeof(ResurrectionUtility),nameof(ResurrectionUtility.TryResurrect))]
 public static class SoulRestResurrect {static bool Prefix(Pawn pawn,ref bool __result){var t=Ascension.Tracker;if(pawn!=null && t!=null && t.restUntil.TryGetValue(pawn.ThingID,out int u) && Find.TickManager.TicksGame<u){__result=false;return false;}return true;}}
 [HarmonyPatch(typeof(MindFlayer),nameof(MindFlayer.CannotDevour))]
 public static class SoulRestDevour {static void Postfix(Thing t,ref string __result){if(__result!=null)return;var p=t as Pawn??(t as Corpse)?.InnerPawn;var tr=Ascension.Tracker;if(p!=null && tr!=null && tr.restUntil.TryGetValue(p.ThingID,out int u) && Find.TickManager.TicksGame<u)__result="灵魂已经安息（死亡半神），大脑吞不下去。";}}
}