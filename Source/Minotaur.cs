using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.18.0 牛头人：蛮牛冲撞、犄角、迷宫感、牛头人体型（体型由共享的 SmallBodyExtension 实现），与巨魔并肩作战。
 public static class Minotaur {
  public const int ChargeCells=8,KnockCells=2,StunTicks=60;
  public const float ChargeDamage=15f,TrollBondFactor=1.05f;
  static XenotypeDef xenotype,troll;static GeneDef horns,maze,stature;static HediffDef hornWeapon,mazeHediff,appetite;
  public static XenotypeDef Xenotype=>xenotype??(xenotype=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_Minotaur"));
  public static XenotypeDef TrollXenotype=>troll??(troll=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_Troll"));
  public static GeneDef HornsGene=>horns??(horns=DefDatabase<GeneDef>.GetNamedSilentFail("LP_MinotaurHorns"));
  public static GeneDef MazeGene=>maze??(maze=DefDatabase<GeneDef>.GetNamedSilentFail("LP_MinotaurMaze"));
  public static GeneDef StatureGene=>stature??(stature=DefDatabase<GeneDef>.GetNamedSilentFail("LP_MinotaurStature"));
  public static HediffDef HornWeapon=>hornWeapon??(hornWeapon=DefDatabase<HediffDef>.GetNamed("LP_MinotaurHornWeapon"));
  public static HediffDef MazeHediff=>mazeHediff??(mazeHediff=DefDatabase<HediffDef>.GetNamed("LP_MinotaurMaze"));
  public static HediffDef Appetite=>appetite??(appetite=DefDatabase<HediffDef>.GetNamed("LP_MinotaurAppetite"));
  public static bool Has(Pawn p,GeneDef gene)=>gene!=null && p?.genes?.HasActiveGene(gene)==true;
  public static void Ensure(Pawn p,HediffDef def,bool want){
   if(p?.health==null || p.Dead)return;var h=p.health.hediffSet.GetFirstHediffOfDef(def);
   if(want && h==null)p.health.AddHediff(def);else if(!want && h!=null)p.health.RemoveHediff(h);
  }

  // ---------- 蛮牛冲撞 ----------
  // 从施法者出发、朝目标方向的直线格子（最多8格，不超过目标距离）。
  // 1.22.0：牛头人戴角盔冲撞+3格；角盔给其他人的弱化冲撞也走这里（cells、damageFactor）。
  public static int ChargeRange(Pawn caster)=>ChargeCells+(RaceGear.HelmBoost(caster)?3:0);
  public static List<IntVec3> ChargeLine(Pawn caster,IntVec3 target)=>ChargeLine(caster,target,ChargeRange(caster));
  public static List<IntVec3> ChargeLine(Pawn caster,IntVec3 target,int range){
   var cells=new List<IntVec3>();var origin=caster.Position;Vector3 dir=(target-origin).ToVector3();dir.y=0;
   if(dir.sqrMagnitude<.01f)return cells;
   int n=Mathf.Min(range,Mathf.CeilToInt(dir.magnitude-.01f));dir.Normalize();
   for(int i=1;i<=n;i++){var c=(origin.ToVector3Shifted()+dir*i).ToIntVec3();if(c!=origin && (cells.Count==0 || cells[cells.Count-1]!=c))cells.Add(c);}
   return cells;
  }
  public struct ChargeResult {public IntVec3 end;public Pawn hit;public IntVec3 knockedTo;public float damage;}
  // 冲过去：撞墙/建筑/不可站立处或任何角色就停下；挡路的是敌人则撞击。建筑不受伤害。
  public static ChargeResult Charge(Pawn caster,IntVec3 target)=>Charge(caster,target,ChargeRange(caster),1f);
  public static ChargeResult Charge(Pawn caster,IntVec3 target,int range,float damageFactor){
   var map=caster.Map;var r=new ChargeResult{end=caster.Position,knockedTo=IntVec3.Invalid};
   foreach(var c in ChargeLine(caster,target,range)){
    if(!c.InBounds(map))break;
    var other=c.GetFirstPawn(map);
    if(other!=null && other!=caster){if(!other.Dead && other.HostileTo(caster))r.hit=other;break;}
    if(!c.Standable(map) || !GenSight.LineOfSight(r.end,c,map,true))break;
    r.end=c;
   }
   if(r.end!=caster.Position){caster.pather?.StopDead();caster.Position=r.end;caster.Notify_Teleported(false,true);}
   for(int i=0;i<3;i++)FleckMaker.ThrowDustPuff(caster.Position.ToVector3Shifted(),map,1.2f);
   if(r.hit!=null){
    var p=r.hit;Vector3 dir=(target-caster.Position).ToVector3();if(dir.sqrMagnitude<.01f)dir=(p.Position-caster.Position).ToVector3();
    r.knockedTo=DNDPush.Destination(map,p.Position,dir,KnockCells);DNDPush.Move(p,r.knockedTo);
    r.damage=ChargeDamage*caster.BodySize*damageFactor;
    p.TakeDamage(new DamageInfo(DamageDefOf.Blunt,r.damage,0f,(p.Position-caster.Position).AngleFlat,caster));
    if(!p.Dead)p.stances?.stunner?.StunFor(StunTicks,caster,false,false,false);
    if(p.Spawned)MoteMaker.ThrowText(p.DrawPos,p.Map,"蛮牛冲撞！",new Color(1f,.6f,.35f));
   }
   return r;
  }

  // ---------- 与巨魔并肩 ----------
  // 本殖民地同一地图上同时有牛头人和巨魔自由殖民者时：征召状态的牛头人、巨魔近战伤害+5%。
  public static float TrollBond(Pawn caster){
   if(caster==null || !caster.Drafted || !caster.IsFreeColonist || caster.Map==null)return 1f;
   // 巨魔一方按激活的巨魔再生基因认（1.20.0），牛头人一方仍按异种型。
   bool minotaur=Xenotype!=null && caster.genes?.Xenotype==Xenotype,troll=Troll.Is(caster);
   if(minotaur){foreach(var p in caster.Map.mapPawns.FreeColonistsSpawned)if(p!=caster && Troll.Is(p))return TrollBondFactor;}
   if(troll){var census=RacialRelations.Tracker?.Census(caster.Map);if(census!=null && census.ColonistCount(Xenotype)-(minotaur?1:0)>0)return TrollBondFactor;}
   return 1f;
  }
 }

 public class Gene_MinotaurHorns : Gene {
  int ticks;
  public override void PostAdd(){base.PostAdd();Minotaur.Ensure(pawn,Minotaur.HornWeapon,true);}
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<2500)return;ticks=0;if(Active)Minotaur.Ensure(pawn,Minotaur.HornWeapon,true);}
  public override void PostRemove(){Minotaur.Ensure(pawn,Minotaur.HornWeapon,false);base.PostRemove();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticks,"hornTicks",0);}
 }
 public class Hediff_MinotaurHorns : HediffWithComps {
  public override bool ShouldRemove=>base.ShouldRemove || !Minotaur.Has(pawn,Minotaur.HornsGene);
 }

 public class Gene_MinotaurMaze : Gene {
  int ticks;
  public static bool Indoors(Pawn p)=>p.Spawned && p.Position.Roofed(p.Map);
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<250)return;ticks=0;Sync();}
  public void Sync()=>Minotaur.Ensure(pawn,Minotaur.MazeHediff,Active && Indoors(pawn));
  public override void PostRemove(){Minotaur.Ensure(pawn,Minotaur.MazeHediff,false);base.PostRemove();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticks,"mazeTicks",0);}
 }
 public class Hediff_MinotaurMaze : HediffWithComps {
  public override bool ShouldRemove=>base.ShouldRemove || !Minotaur.Has(pawn,Minotaur.MazeGene);
 }

 public class Gene_MinotaurStature : Gene {
  int ticks;
  public override void PostAdd(){base.PostAdd();Minotaur.Ensure(pawn,Minotaur.Appetite,true);}
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<2500)return;ticks=0;if(Active)Minotaur.Ensure(pawn,Minotaur.Appetite,true);}
  public override void PostRemove(){Minotaur.Ensure(pawn,Minotaur.Appetite,false);base.PostRemove();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticks,"statureTicks",0);}
 }
 public class Hediff_MinotaurAppetite : Hediff {
  public override bool ShouldRemove=>base.ShouldRemove || !Minotaur.Has(pawn,Minotaur.StatureGene);
 }

 // ---------- 牛头人与吉斯洋基的主动能力 ----------
 public class CompProperties_MinotaurGithAbility : CompProperties_AbilityEffect {
  public string effect;public float psyfocusCost;
  public CompProperties_MinotaurGithAbility(){compClass=typeof(CompMinotaurGithAbility);}
 }
 public class CompMinotaurGithAbility : CompAbilityEffect {
  public CompProperties_MinotaurGithAbility Props=>(CompProperties_MinotaurGithAbility)props;
  Pawn Caster=>parent.pawn;
  public override bool GizmoDisabled(out string reason){
   if(Props.effect=="astral" && !Githyanki.CanPayStep(Caster,Props.psyfocusCost)){reason="精神力不足：需要"+Props.psyfocusCost.ToStringPercent()+"。";return true;}
   return base.GizmoDisabled(out reason);
  }
  public string Invalid(LocalTargetInfo target){
   var map=Caster?.Map;if(map==null)return "施法者不在地图上。";
   var c=target.Cell;if(!c.IsValid || !c.InBounds(map))return "需要地图上的目标位置。";
   switch(Props.effect){
    case "charge":
     if(c==Caster.Position)return "需要冲撞的方向。";
     return c.DistanceTo(Caster.Position)>Minotaur.ChargeRange(Caster)+.5f?"超出冲撞距离（"+Minotaur.ChargeRange(Caster)+"格）。":null;
    case "astral":return Githyanki.CannotStep(Caster,c);
   }
   return "未知的能力。";
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   string reason=Invalid(target);
   if(reason!=null && throwMessages)Messages.Message(reason,MessageTypeDefOf.RejectInput,false);
   return reason==null && base.Valid(target,throwMessages);
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   if(Invalid(target)!=null)return;
   switch(Props.effect){
    case "charge":Minotaur.Charge(Caster,target.Cell);break;
    case "astral":Githyanki.Step(Caster,target.Cell,Props.psyfocusCost);break;
   }
  }
  public override void DrawEffectPreview(LocalTargetInfo target){
   if(Caster?.Map==null || !target.Cell.IsValid)return;
   if(Props.effect=="charge")GenDraw.DrawFieldEdges(Minotaur.ChargeLine(Caster,target.Cell));
  }
  public override string ExtraLabelMouseAttachment(LocalTargetInfo target)=>Props.effect=="astral" && Props.psyfocusCost>0 && Caster.GetPsylinkLevel()>0?"精神力 "+Props.psyfocusCost.ToStringPercent():null;
 }

 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply")]
 public static class MinotaurTrollBond {
  static IEnumerable<DamageInfo> Postfix(IEnumerable<DamageInfo> values,Verb_MeleeAttackDamage __instance){
   float f=Minotaur.TrollBond(__instance.CasterPawn);
   foreach(var d in values){if(f!=1f){var copy=d;copy.SetAmount(d.Amount*f);yield return copy;}else yield return d;}
  }
 }
}
