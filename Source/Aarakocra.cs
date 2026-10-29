using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.24.0 鸟人：羽翼（翱翔：不改寻路，做成穿墙位移）、空心骨、天生飞禽（怕封闭）。
 public static class Aarakocra {
  public const int SoarRange=12,CloakRange=4,CagedTicks=2500,FreeTicks=417;
  public const float CloakCooldownFactor=.75f;
  public static bool CloakBoost(Pawn p)=>Races124.IsAarakocra(p) && Items.Worn(p,"LP_FeatherCloak")!=null;
  public static int Range(Pawn p)=>SoarRange+(CloakBoost(p)?CloakRange:0);
  // 翱翔：忽略墙、栅栏、水和建筑（不要求视线），但不能落到厚岩顶（山体）下。
  public static string CannotSoar(Pawn caster,IntVec3 c){
   var r=Races124.BasicLanding(caster,c,Range(caster));if(r!=null)return r;
   if(c.GetRoof(caster.Map)?.isThickRoof==true)return "不能飞进山体下面。";
   return null;
  }
  public static bool Soar(Pawn caster,IntVec3 c){
   if(CannotSoar(caster,c)!=null)return false;var map=caster.Map;var from=caster.Position;
   // 沿途一串羽毛（尘土）表示飞过。
   var a=from.ToVector3Shifted();var b=c.ToVector3Shifted();int steps=Mathf.Max(2,Mathf.CeilToInt((b-a).magnitude));
   for(int i=0;i<=steps;i++){var v=Vector3.Lerp(a,b,i/(float)steps);if(v.ToIntVec3().InBounds(map))FleckMaker.ThrowDustPuffThick(v,map,.8f,new Color(.92f,.86f,.74f));}
   Races124.Blink(caster,c);FleckMaker.ThrowAirPuffUp(b,map);
   return true;
  }
 }
 // 天生飞禽：在有屋顶的地方醒着连续待满1小时，心情“囚于屋檐”；到无屋顶处待满约10分钟就消失。睡觉不算。
 public class Gene_AarakocraSkyborn : Gene {
  public int roofedTicks,freeTicks;int ticks;
  public bool Caged=>roofedTicks>=Aarakocra.CagedTicks;
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<250)return;int n=ticks;ticks=0;Advance(n);}
  public void Advance(int n){
   if(!Active || !pawn.Spawned){return;}
   bool roofed=pawn.Position.Roofed(pawn.Map);
   if(roofed){if(pawn.Awake())roofedTicks+=n;freeTicks=0;}
   else{freeTicks+=n;if(freeTicks>=Aarakocra.FreeTicks)roofedTicks=0;}
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref roofedTicks,"skyRoofedTicks",0);Scribe_Values.Look(ref freeTicks,"skyFreeTicks",0);}
 }
 public class ThoughtWorker_AarakocraCaged : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>p.genes?.GetFirstGeneOfType<Gene_AarakocraSkyborn>() is Gene_AarakocraSkyborn g && g.Active && g.Caged?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }
 // 在无屋顶处干活：天空在上。
 public class ThoughtWorker_AarakocraOpenSky : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>Races124.Has(p,"LP_AarakocraSkyborn") && p.Spawned && !p.Position.Roofed(p.Map) && p.CurJob?.workGiverDef!=null?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }
}
