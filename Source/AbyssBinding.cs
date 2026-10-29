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
 // 1.54.0 仆从与束缚（规格第三节）。仆从 LP_AbyssThrall：限时、不算心情、不算袭击点数，到期遣返；
 // 束缚者 LP_PlanarBinding：永久殖民者，severity = 服从度 0～100，每天衰减，降到 0 挣脱成深渊魔群。
 public static class AbyssBinding {
  public const float DemonDecay=2f,ScrollFactor=.5f,NabassuFactor=1.5f,FallenMasterFactor=1.5f,ReassertGain=8f,CircleGain=3f,MasterDeathLoss=40f,ColdIronLoss=5f,WarnAt=20f,MinStart=40f;
  public const float BalorBreakChance=.3f;
  public static int thralls; // 存在过的仆从状态数（只用来决定袭击点数补丁要不要过滤，不必精确）
  public static IEnumerable<Pawn> PlayerPawns=>PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
  public static int BoundCount()=>PlayerPawns.Count(Outsiders.Bound);
  // 同时最多 1 + 殖民地最高冒险者等级 / 5 个束缚者，再加设置的额外上限。
  public static int BoundCap()=>1+AdventurerEnemies.ColonyMaxLevel()/5+(LichMod.Settings?.abyssBoundExtra??0);
  public static int ThrallCount()=>PlayerPawns.Count(Outsiders.Thrall);
  public static int ThrallCap=>LichMod.Settings?.abyssThrallCap??8;
  public static Hediff_PlanarBinding Binding(Pawn p){var list=p?.health?.hediffSet?.hediffs;if(list!=null)for(int i=0;i<list.Count;i++)if(list[i] is Hediff_PlanarBinding h)return h;return null;}
  public static float Obedience(Pawn p)=>Binding(p)?.Severity??0f;
  public static Pawn PawnById(int id){
   if(id<0)return null;
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawns)if(p.thingIDNumber==id)return p;
   foreach(var p in Find.WorldPawns.AllPawnsAliveOrDead)if(p.thingIDNumber==id)return p;
   return null;
  }
  // 冒险者等级：固定等级（不受殖民地上限影响）。
  public static void SetLevel(Pawn p,int level,bool player){
   if(level<=0 || p==null)return;var h=Adventurer.Get(p)??Adventurer.TryAdd(p,1,true);if(h==null)return;
   h.storyLevel=true;h.RaiseTo(level);if(!player || Adventurer.AutoASI(p)){if(h.pendingASI>0)h.AutoAssign();}h.Dirty();
  }
  static void Join(Pawn p){
   if(p.Faction!=Faction.OfPlayer)p.SetFaction(Faction.OfPlayer);
   if(p.guest!=null && p.guest.GuestStatus!=null)p.guest.SetGuestStatus(null);
   p.GetLord()?.RemovePawn(p);
   if(p.InMentalState)p.MentalState.RecoverFromState();
   p.workSettings?.EnableAndInitialize();
  }
  public static Hediff_PlanarBinding Bind(Pawn p,Pawn binder,float obedience){
   if(p==null || Outsiders.BoundDef==null)return null;
   Join(p);
   var h=Binding(p)??(Hediff_PlanarBinding)p.health.AddHediff(Outsiders.BoundDef);
   h=Binding(p);if(h==null)return null;
   h.binder=binder;h.Severity=Mathf.Clamp(obedience,1f,100f);h.warned=false;
   if(!AbyssRaces.IsSuccubus(p) && !Races124.Has(p,"Sterile") && HediffDefOf.Sterilized!=null && !Items.Has(p,HediffDefOf.Sterilized))p.health.AddHediff(HediffDefOf.Sterilized);
   return h;
  }
  public static Hediff_AbyssThrall MakeThrall(Pawn p,int ticks){
   if(p==null || Outsiders.ThrallDef==null)return null;
   Join(p);
   var h=(Hediff_AbyssThrall)p.health.AddHediff(Outsiders.ThrallDef);h=p.health.hediffSet.hediffs.OfType<Hediff_AbyssThrall>().FirstOrDefault();
   if(h!=null)h.ticksLeft=ticks;
   p.needs?.AddOrRemoveNeedsAsAppropriate();
   return h;
  }
  public static float DailyDecay(Pawn p,Hediff_PlanarBinding h){
   if(PactRaces.NoDecay(p))return 0f; // 1.55.0：九狱单位守契约、林仙、守护天使、契约借来的守卫
   float d=DemonDecay;
   if(TrueNames.HeldFor(p))d*=ScrollFactor;
   if(AbyssRaces.Is(p,AbyssRaces.Nabassu))d*=NabassuFactor;
   if(h?.binder!=null && !h.binder.Dead && AbyssTaint.Severity(h.binder)>=AbyssTaint.Mark)d*=FallenMasterFactor;
   return d;
  }
  public static bool CircleBed(Pawn p){var bed=p?.ownership?.OwnedBed;return bed!=null && bed.Spawned && AbyssRitual.InBindingCircle(bed);}
  public static void ColdIronHit(Pawn p,DamageInfo d){
   var w=(d.Instigator as Pawn)?.equipment?.Primary;if(w==null || d.Weapon!=w.def || w.Stuff==null || w.Stuff!=ItemMaterials.ColdIron)return;
   if((d.Instigator as Pawn).Faction?.IsPlayer!=true)return;
   var h=Binding(p);if(h!=null)h.Change(-ColdIronLoss,"被寒铁武器打到");
  }
  public static int OnMasterDied(Pawn master){
   int n=0;foreach(var p in PlayerPawns.ToList()){var h=Binding(p);if(h?.binder!=master)continue;h.Change(-MasterDeathLoss,"主人"+master.LabelShort+"死了");n++;}
   return n;
  }
  public static void BreakFree(Pawn p,string why){
   if(PactRaces.Renegotiate(p))return; // 1.55.0：九狱单位不开打，要求重新谈判
   var h=Binding(p);if(h!=null){h.breaking=true;p.health.RemoveHediff(h);}
   var t=p.health.hediffSet.hediffs.OfType<Hediff_AbyssThrall>().FirstOrDefault();if(t!=null){t.breaking=true;p.health.RemoveHediff(t);}
   var f=AbyssRaces.Horde();if(f==null)return;
   p.SetFaction(f);p.needs?.AddOrRemoveNeedsAsAppropriate();
   if(p.Spawned){if(p.Downed){}else LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false,false,false),p.Map,new List<Pawn>{p});}
   Find.LetterStack.ReceiveLetter("挣脱束缚："+p.LabelShort,p.LabelShort+why+"。它不再服从任何人，成了深渊魔群的一员，正转身扑向殖民地！",LetterDefOf.ThreatBig,p);
  }
 }

 // ---------- 仆从 ----------
 public class Hediff_AbyssThrall : HediffWithComps {
  public int ticksLeft=GenDate.TicksPerDay;public bool breaking;
  public override string LabelInBrackets=>"还能留 "+ticksLeft.ToStringTicksToPeriod(true,true,false);
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);AbyssBinding.thralls++;}
  public override void TickInterval(int delta){
   base.TickInterval(delta);if(pawn.Dead)return;
   ticksLeft-=delta;if(ticksLeft>0)return;ticksLeft=int.MaxValue/2;
   var p=pawn;Fiends.Defer(()=>Expire(p));
  }
  // 到期：遣返（装备掉落）。炎魔 30% 挣脱，当场开打。
  public static void Expire(Pawn p){
   if(p==null || p.Dead || p.Destroyed)return;
   if(Fiends.IsBalor(p) && Outsiders.Roll<AbyssBinding.BalorBreakChance){AbyssBinding.BreakFree(p,"的召唤时限到了，却没有回去——它挣断了深渊仪式的约束");return;}
   if(p.Spawned && Fiends.Is(p))Outsiders.Banish(p);else{p.health.RemoveHediff(p.health.hediffSet.hediffs.OfType<Hediff_AbyssThrall>().First());}
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticksLeft,"lpTicksLeft",GenDate.TicksPerDay);if(Scribe.mode==LoadSaveMode.PostLoadInit)AbyssBinding.thralls++;}
 }

 // ---------- 束缚者 ----------
 public class Hediff_PlanarBinding : HediffWithComps {
  public Pawn binder;public int reassertDay=-1;public bool warned,breaking;int acc;long frozenAge=-1; // 年龄冻结：每小时把生物年龄拨回束缚时的值（不加全局补丁）
  public override string LabelInBrackets=>"服从 "+Severity.ToString("0");
  public override bool ShouldRemove=>false;
  public override string TipStringExtra{get{
   var lines=new List<string>{"服从度每天 -"+AbyssBinding.DailyDecay(pawn,this).ToString("0.#")+(TrueNames.HeldFor(pawn)?"（持有它的真名卷：减半）":"")+"。"};
   lines.Add("主人："+(binder==null?"无":binder.LabelShort+(binder.Dead?"（已死）":"")));
   lines.Add("主人对它“重申真名”每天一次 +8；它的床在束缚法阵里每天 +3；主人死亡 -40；被殖民地的寒铁武器打到 -5。降到 0 会挣脱。");
   return string.Join("\n",lines);
  }}
  public void Change(float delta,string why){
   if(breaking || pawn.Dead)return;
   Severity=Mathf.Clamp(Severity+delta,0f,100f);
   if(delta<=-5f && pawn.Faction?.IsPlayer==true)Messages.Message(pawn.LabelShort+"的服从度 "+delta.ToString("0")+"（"+why+"）。",pawn,MessageTypeDefOf.NegativeEvent,false);
   Check();
  }
  void Check(){
   if(Severity>=WarnReset)warned=false;
   if(!warned && Severity<AbyssBinding.WarnAt && Severity>0f){warned=true;Find.LetterStack.ReceiveLetter("它在试探束缚："+pawn.LabelShort,pawn.LabelShort+"的服从度降到了"+Severity.ToString("0")+"。再这样下去它会挣脱。让主人对它重申真名、把它的床放进束缚法阵，或者拿到它的真名卷。",LetterDefOf.NegativeEvent,pawn);}
   if(Severity<=0.01f){var p=pawn;Fiends.Defer(()=>{if(!p.Dead && Outsiders.Bound(p))AbyssBinding.BreakFree(p,"的服从度耗尽了");});}
  }
  const float WarnReset=30f;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;
   if(pawn.Dead || breaking)return;
   if(pawn.ageTracker!=null){if(frozenAge<0)frozenAge=pawn.ageTracker.AgeBiologicalTicks;else pawn.ageTracker.AgeBiologicalTicks=frozenAge;}
   float d=-AbyssBinding.DailyDecay(pawn,this)/24f;if(AbyssBinding.CircleBed(pawn))d+=AbyssBinding.CircleGain/24f;
   Severity=Mathf.Clamp(Severity+d,0f,100f);Check();
  }
  public override void ExposeData(){
   base.ExposeData();Scribe_References.Look(ref binder,"lpBinder",true);Scribe_Values.Look(ref reassertDay,"lpReassertDay",-1);Scribe_Values.Look(ref warned,"lpWarned");Scribe_Values.Look(ref acc,"lpAcc");Scribe_Values.Look(ref frozenAge,"lpFrozenAge",-1L);
  }
 }
 // 重申真名：主人对自己的束缚者，每天最多一次，服从 +8。
 public class InteractionWorker_ReassertName : InteractionWorker {
  public override float RandomSelectionWeight(Pawn initiator,Pawn recipient){
   var h=AbyssBinding.Binding(recipient);if(h==null || h.binder!=initiator || h.reassertDay==GenDate.DaysPassed)return 0f;
   return 6f;
  }
  public override void Interacted(Pawn initiator,Pawn recipient,List<RulePackDef> extraSentencePacks,out string letterText,out string letterLabel,out LetterDef letterDef,out LookTargets lookTargets){
   letterText=null;letterLabel=null;letterDef=null;lookTargets=null;
   var h=AbyssBinding.Binding(recipient);if(h==null)return;h.reassertDay=GenDate.DaysPassed;h.Change(AbyssBinding.ReassertGain,"重申真名");
  }
 }

 // ---------- Harmony ----------
 // 仆从没有心情需求：写在 LP_AbyssThrall 的阶段里（disablesNeeds Mood），加上状态后刷新需求。
 // 仆从死了殖民者不难过。
 [HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility),nameof(PawnDiedOrDownedThoughtsUtility.TryGiveThoughts),new[]{typeof(Pawn),typeof(DamageInfo?),typeof(PawnDiedOrDownedThoughtsKind)})]
 public static class ThrallNoGrief {static bool Prefix(Pawn victim)=>!(AbyssBinding.thralls>0 && Outsiders.Thrall(victim));}
 // 仆从不计入袭击点数（故事叙述者按 PlayerPawnsForStoryteller 算殖民者部分）。
 [HarmonyPatch(typeof(Map),nameof(Map.PlayerPawnsForStoryteller),MethodType.Getter)]
 public static class ThrallNoThreat {static void Postfix(ref IEnumerable<Pawn> __result){if(AbyssBinding.thralls>0)__result=__result.Where(p=>!Outsiders.Thrall(p));}}
}
