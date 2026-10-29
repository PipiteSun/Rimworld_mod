using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.24.0 寒冰魔与炎魔：只出现在塞伊远征军的袭击与据点里。击倒或打死都会“被遣返九狱”——不留活口、不留尸体，只留战利品。
 public static class Fiends {
  public const float AuraRadius=5f,BuffetRadius=4f,BurstRadius=4f,FlameAuraRadius=2f,FlameChance=.2f,ColdIronFactor=1.5f,HolyFactor=2f;
  public const int ChillMax=3,ChillTicks=1800,AuraInterval=60,FlameInterval=120,BuffetKnock=2,BuffetStun=120,BurstDamage=40,BurstDelay=60;
  public const int IceDevilMinPoints=1500,IceDevilMax=2,BalorMinPoints=3500,BalorMax=1;
  // 1.54.0：判定迁到外来者层（Outsiders.cs）：带 LP_OutsiderExtension 且 banishOnDown 的都算（九狱之躯、深渊之躯）。
  public static bool Is(Pawn p){var e=Outsiders.Of(p);return e!=null && e.banishOnDown;}
  public static bool IsIceDevil(Pawn p)=>Races124.Has(p,"LP_IceDevilCold");
  public static bool IsBalor(Pawn p)=>Races124.Has(p,"LP_BalorFlame");
  public static string Label(Pawn p)=>IsBalor(p)?"炎魔":IsIceDevil(p)?"寒冰魔":"魔鬼";
  static PawnKindDef iceKind,balorKind;
  public static PawnKindDef IceKind=>iceKind??(iceKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FH_IceDevil"));
  public static PawnKindDef BalorKind=>balorKind??(balorKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FH_Balor"));

  // ---------- 寒气侵体 ----------
  public static Hediff Chill(Pawn p,int layers){
   if(p==null || p.Dead || Is(p) || layers<=0 || FrostGiant.ChillImmune(p,"LP_Chilled"))return null;
   var def=Items.Hediff("LP_Chilled");if(def==null)return null;
   var h=p.health.hediffSet.GetFirstHediffOfDef(def);
   if(h==null){h=HediffMaker.MakeHediff(def,p);h.Severity=Mathf.Min(ChillMax,layers);p.health.AddHediff(h);}
   else h.Severity=Mathf.Min(ChillMax,h.Severity+layers);
   var timer=(h as HediffWithComps)?.TryGetComp<HediffComp_Disappears>();if(timer!=null)timer.ticksToDisappear=ChillTicks;
   return h;
  }
  public static int ChillLayers(Pawn p){var def=Items.Hediff("LP_Chilled");var h=def==null?null:p?.health?.hediffSet?.GetFirstHediffOfDef(def);return h==null?0:Mathf.RoundToInt(h.Severity);}
  // 寒气光环：5格内对寒冰魔敌对的角色每60刻叠一层（最多3层）。
  public static int ChillAura(Pawn devil){
   if(devil?.Map==null || devil.Downed || devil.Dead)return 0;int n=0;
   foreach(var p in Races124.PawnsNear(devil.Map,devil.Position,AuraRadius))if(p!=devil && p.HostileTo(devil) && !Is(p) && Chill(p,1)!=null)n++;
   return n;
  }
  // 烈焰光环：每120刻，2格内每个可燃物/角色20%概率着火（不分敌我，自己除外）。
  public static int FlameAura(Pawn balor,Func<float> roll){
   if(balor?.Map==null || balor.Downed || balor.Dead)return 0;var map=balor.Map;int n=0;
   foreach(var c in GenRadial.RadialCellsAround(balor.Position,FlameAuraRadius,true)){
    if(!c.InBounds(map))continue;
    foreach(var t in c.GetThingList(map).ToList()){
     if(t==balor || t is Fire || t.Destroyed || t.GetStatValue(StatDefOf.Flammability)<=0)continue;
     if(roll()>=FlameChance)continue;
     if(t is Pawn || t.def.category==ThingCategory.Item || t is Plant || t is Building){FireUtility.TryAttachFire(t,.4f,balor);if(t.HasAttachment(ThingDefOf.Fire) || t is Pawn)n++;}
    }
   }
   return n;
  }

  // ---------- 遣返九狱 ----------
  public static List<Thing> Loot(Pawn p){
   var list=new List<Thing>();
   var name=IsBalor(p)?"LP_BalorHeart":IsIceDevil(p)?"LP_IceDevilStinger":null;
   var def=name==null?null:Items.Thing(name);if(def!=null)list.Add(ThingMaker.MakeThing(def));
   return list;
  }
  public static void DropAt(IEnumerable<Thing> things,IntVec3 cell,Map map){foreach(var t in things.ToList())GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);}
  // 炎魔火鞭掉落时为优秀品质。
  public static void Excellent(Thing t){if(t is ThingWithComps w && w.def.defName=="LP_BalorWhip")w.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Outsider);}
  static void Smoke(IntVec3 cell,Map map){for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(cell.ToVector3Shifted()+new Vector3(Rand.Range(-.5f,.5f),0,Rand.Range(-.5f,.5f)),map,2.2f);FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,1.6f);}
  // 被击倒：武器与战利品落地，本体化作黑烟消失。炎魔另在1秒后爆炸。1.54.0：泛化为 Outsiders.Banish（仆从 / 束缚者不掉战利品，深渊恶魔多掉灵魂币等）。
  public static bool Banish(Pawn p)=>Outsiders.Banish(p);
  // 被打死：尸体立即消失，掉同样的战利品（武器原版已经掉在地上）。
  public static bool BanishCorpse(Pawn p)=>Outsiders.BanishCorpse(p);
  public static void Defer(Action a){var s=Items.State;if(s!=null)s.Defer(a);else a();}
  public static void Schedule(Map map,IntVec3 cell){var s=Races124.State;if(s!=null)s.bursts.Add(new DelayedBurst{map=map,cell=cell,tick=Find.TickManager.TicksGame+BurstDelay,label="炎魔"});}
  public static void Explode(DelayedBurst b){
   if(b?.map==null || !Find.Maps.Contains(b.map) || !b.cell.InBounds(b.map))return;
   GenExplosion.DoExplosion(b.cell,b.map,BurstRadius,DamageDefOf.Flame,null,BurstDamage,chanceToStartFire:.6f);
  }

  // ---------- 炎魔振翼 ----------
  public static IEnumerable<Pawn> BuffetTargets(Pawn balor)=>balor?.Map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(balor.Map,balor.Position,BuffetRadius).Where(p=>p!=balor && p.HostileTo(balor));
  public static int Buffet(Pawn balor){
   int n=0;
   foreach(var p in BuffetTargets(balor).ToList()){
    var dest=DNDPush.Destination(balor.Map,p.Position,(p.Position-balor.Position).ToVector3(),BuffetKnock);DNDPush.Move(p,dest);
    if(!p.Dead)p.stances?.stunner?.StunFor(BuffetStun,balor,false,true,false);n++;
   }
   for(int i=0;i<8;i++)FleckMaker.ThrowDustPuffThick(balor.Position.ToVector3Shifted(),balor.Map,2f,new Color(.35f,.2f,.18f));
   return n;
  }

  // ---------- 袭击点数门槛 ----------
  [ThreadStatic] public static bool abyssRaid;
  public static void TrimRaid(List<Pawn> list,PawnGroupMakerParms parms){
   if(parms?.faction?.def!=Factions.HostileDef)return;
   Cap(list,IceKind,parms.points>=IceDevilMinPoints?IceDevilMax:0);
   Cap(list,BalorKind,parms.points>=BalorMinPoints?BalorMax:0);
   if(BalorKind!=null && list.Any(p=>p.kindDef==BalorKind))abyssRaid=true;
  }
  static void Cap(List<Pawn> list,PawnKindDef kind,int max){
   if(kind==null)return;var have=list.Where(p=>p.kindDef==kind).ToList();
   for(int i=max;i<have.Count;i++){list.Remove(have[i]);if(Find.WorldPawns.Contains(have[i]))Find.WorldPawns.RemovePawn(have[i]);have[i].Destroy();}
  }

  // ---------- 魔鬼之眼：寒冰魔看得见隐身者 ----------
  // 1.58.6：原来每个前缀（包括每个小人每刻的 Pawn.Tick）都当场算一遍 Sees。现在前缀只把“正在行动的小人”压栈，
  // 到隐身判断真的返回“隐身”时才对栈里的人算 Sees——和原来“调用链上有人满足 Sees 就看得见”的规则一样。
  [ThreadStatic] static Pawn[] actors;[ThreadStatic] static int depth;
  public static int Push(Pawn p){var a=actors??(actors=new Pawn[16]);int d=depth;if(d>=a.Length){Array.Resize(ref actors,d*2);a=actors;}a[d]=p;depth=d+1;return d;}
  public static void Pop(int d){depth=d;}
  public static int Depth=>depth; // 自检：索敌结束后栈清空
  public static bool AnyActorSees(){var a=actors;for(int i=depth-1;i>=0;i--)if(Sees(a[i]))return true;return false;}
  // 1.28.0：伊尔斯维、提尔的神力灌注者也看得见隐身者。
  public static bool Sees(Pawn p)=>p!=null && (Races124.Has(p,"LP_IceDevilSight") || Twilight.SeesInvisible(p) || Outsiders.Of(p)?.plane==OutsiderPlane.Celestia || Feats2.Observer(p)); // 1.55.0：天界单位的真知；1.58.1 观察者
 }

 public class Gene_IceDevilCold : Gene {
  int ticks;
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<Fiends.AuraInterval)return;ticks=0;if(Active && pawn.Spawned)Fiends.ChillAura(pawn);}
 }
 public class Gene_BalorFlame : Gene {
  int ticks;
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<Fiends.FlameInterval)return;ticks=0;if(Active && pawn.Spawned)Fiends.FlameAura(pawn,()=>Rand.Value);}
 }

 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 // 倒地/死亡的结算还没走完，所以延到下一刻再移走（ItemsState.Defer）。
 public static class FiendBanishOnDowned {
  // 1.54.0：束缚者、戴锚链的不遣返；身边有带锚链的殖民者时自动锁住。
  static void Postfix(Pawn ___pawn){if(!___pawn.Dead && Outsiders.BanishOnDown(___pawn)){if(Outsiders.TryAutoShackle(___pawn))return;var p=___pawn;Fiends.Defer(()=>{if(!p.Dead && Outsiders.BanishOnDown(p))Fiends.Banish(p);});}}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class FiendBanishOnDeath {
  // 先在原地放下武器（炎魔火鞭定为优秀），再让原版处理死亡，最后移走尸体。1.54.0：戴锚链的照常留尸体。
  static void Prefix(Pawn __instance){if(!__instance.Dead && __instance.Spawned && Outsiders.VanishOnDeath(__instance) && __instance.equipment?.Primary!=null && __instance.equipment.TryDropEquipment(__instance.equipment.Primary,out var w,__instance.Position,false))Fiends.Excellent(w);}
  static void Postfix(Pawn __instance){if(__instance.Dead && Outsiders.VanishOnDeath(__instance)){var p=__instance;Fiends.Defer(()=>Fiends.BanishCorpse(p));}}
 }
 // 尾刺与霜刺长枪：命中追加一层寒气（霜刺长枪对魔鬼无效）。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class FiendChillOnHit {
  static void Postfix(Thing __instance,DamageInfo __0){
   if(!(__instance is Pawn victim) || victim.Dead)return;
   if(__0.WeaponLinkedHediff?.defName=="LP_IceDevilStingerWeapon" || __0.Weapon?.defName=="LP_FrostSpear")Fiends.Chill(victim,1);
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class FrostSpearVsFiends {
  static void Prefix(Pawn __instance,ref DamageInfo __0){if(__0.Weapon?.defName=="LP_FrostSpear" && Fiends.Is(__instance))__0.SetAmount(0f);}
 }
 [HarmonyPatch(typeof(AttackTargetFinder),nameof(AttackTargetFinder.BestAttackTarget))]
 public static class DevilSightTargeting {
  static void Prefix(IAttackTargetSearcher searcher,out int __state){__state=Fiends.Push(searcher?.Thing as Pawn);}
  static void Finalizer(int __state){Fiends.Pop(__state);}
 }
 [HarmonyPatch(typeof(AttackTargetFinder),"FindBestReachableMeleeTarget")]
 public static class DevilSightMelee {
  static void Prefix(Pawn searcherPawn,out int __state){__state=Fiends.Push(searcherPawn);}
  static void Finalizer(int __state){Fiends.Pop(__state);}
 }
 [HarmonyPatch(typeof(Verb),"TryCastNextBurstShot")]
 public static class DevilSightVerb {
  static void Prefix(Verb __instance,out int __state){__state=Fiends.Push(__instance.CasterPawn);}
  static void Finalizer(int __state){Fiends.Pop(__state);}
 }
 // 原版的隐身判断散在 AI 索敌、攻击工作的失败条件和“能否命中”里：寒冰魔自己的这一刻（思考、工作、攻击）都看得见隐身者。
 [HarmonyPatch(typeof(Verb),nameof(Verb.CanHitTargetFrom))]
 public static class DevilSightCanHit {
  static void Prefix(Verb __instance,out int __state){__state=Fiends.Push(__instance.CasterPawn);}
  static void Finalizer(int __state){Fiends.Pop(__state);}
 }
 [HarmonyPatch(typeof(Pawn),"Tick")]
 public static class DevilSightTick {
  static void Prefix(Pawn __instance,out int __state){__state=Fiends.Push(__instance);}
  static void Finalizer(int __state){Fiends.Pop(__state);}
 }
 [HarmonyPatch(typeof(InvisibilityUtility),nameof(InvisibilityUtility.IsPsychologicallyInvisible))]
 public static class DevilSightInvisibility {
  static void Postfix(ref bool __result){if(__result && Fiends.AnyActorSees())__result=false;}
 }
 // 有炎魔的袭击：信件标题加“深渊裂口”。
 [HarmonyPatch(typeof(IncidentWorker_Raid),"TryExecuteWorker")]
 public static class AbyssRaidReset {static void Prefix(){Fiends.abyssRaid=false;}static void Finalizer(){Fiends.abyssRaid=false;}}
 [HarmonyPatch(typeof(IncidentWorker_RaidEnemy),"GetLetterLabel")]
 public static class AbyssRaidLetter {
  static void Postfix(IncidentParms parms,ref string __result){if(Fiends.abyssRaid && parms?.faction?.def==Factions.HostileDef && __result!=null && !__result.Contains("深渊裂口"))__result="深渊裂口·"+__result;}
 }
}
