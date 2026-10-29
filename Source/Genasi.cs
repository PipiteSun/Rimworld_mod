using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.24.0 元素裔：四个异种型共用本源“元素血脉”，各带一条元素血脉基因（互斥标签 DND_Element；巨灵复用这四条）。
 public static class Genasi {
  public static readonly string[] Elements={"Fire","Water","Earth","Air"};
  public const int SparkRange=6,TideRange=8,LeapRange=8,SoakTicks=600;
  public const float SparkFire=.3f;
  public static GeneDef Core=>Races124.Gene("LP_GenasiCore");
  public static GeneDef Bloodline(string element)=>Races124.Gene("LP_Element"+element);
  public static XenotypeDef Xenotype(string element)=>Races124.Xeno("LP_Genasi"+element);
  public static GeneDef Skin(string element){switch(element){case "Fire":return Races124.Gene("LP_SkinEmber");case "Water":return Races124.Gene("LP_SkinTide");case "Earth":return Races124.Gene("LP_SkinStone");case "Air":return Races124.Gene("LP_SkinSky");}return null;}
  public static string Label(string element){switch(element){case "Fire":return "火";case "Water":return "水";case "Earth":return "土";case "Air":return "风";}return element;}
  public static bool IsGenasi(Pawn p)=>CommonRaces.Has(p,Core);
  // 激活的元素血脉（元素裔与巨灵都有）；没有时为 null。
  // 1.58.6：四个血脉基因用静态 DefRef（原来每次拼 4 个字符串再按名字查）。顺序不变：火、水、土、风取第一个。
  static readonly DefRef<GeneDef>[] bloodlines=Elements.Select(e=>new DefRef<GeneDef>("LP_Element"+e)).ToArray();
  public static string Element(Pawn p){if(p?.genes==null)return null;for(int i=0;i<Elements.Length;i++)if(CommonRaces.Has(p,bloodlines[i].Def))return Elements[i];return null;}
  public static string ElementOfAbility(AbilityDef def){
   switch(def?.defName){case "LP_ElementSpark":return "Fire";case "LP_ElementTide":return "Water";case "LP_ElementWindLeap":return "Air";}
   return null;
  }
  public static bool IsBloodlineAbility(AbilityDef def)=>ElementOfAbility(def)!=null;
  // 对应元素符加强本血脉的被动。
  public static bool Boosted(Pawn p,string element)=>Element(p)==element && ElementalFocus.Worn(p,element)!=null;

  // ---------- 火花：6格内的一个目标点燃（小火） ----------
  public static string CannotSpark(Pawn caster,LocalTargetInfo t){
   var map=caster.Map;var c=t.Cell;
   if(!c.IsValid || !c.InBounds(map))return "需要地图上的目标。";
   if(c.DistanceTo(caster.Position)>SparkRange+.5f)return "超出火花距离（6格）。";
   if(!GenSight.LineOfSight(caster.Position,c,map,true))return "看不到目标。";
   return null;
  }
  public static bool Spark(Pawn caster,LocalTargetInfo t){
   var map=caster.Map;if(CannotSpark(caster,t)!=null)return false;
   FleckMaker.Static(t.Cell,map,FleckDefOf.ShotFlash,1.4f);
   if(t.Thing!=null && !t.Thing.Destroyed){
    if(t.Thing is Pawn p && p.GetStatValue(StatDefOf.Flammability)<=0){Items.Mote(p,"不受火焰影响",new Color(1f,.7f,.4f));return false;}
    FireUtility.TryAttachFire(t.Thing,SparkFire,caster);
    return t.Thing.HasAttachment(ThingDefOf.Fire);
   }
   return FireUtility.TryStartFireIn(t.Cell,map,SparkFire,caster);
  }

  // ---------- 水流：3×3（佩戴潮汐符 5×5）灭火，格内敌人湿透 ----------
  public static int TideRadius(Pawn caster)=>Boosted(caster,"Water")?2:1;
  public static int Tide(Pawn caster,IntVec3 center){
   var map=caster.Map;var rect=CellRect.CenteredOn(center,TideRadius(caster)).ClipInsideMap(map);int soaked=0;
   foreach(var c in rect.Cells){
    foreach(var t in c.GetThingList(map).ToList()){
     if(t is Fire f){f.Destroy();continue;}
     if(t is Pawn p){
      var fire=p.GetAttachment(ThingDefOf.Fire);if(fire!=null && !fire.Destroyed)fire.Destroy();
      if(!p.Dead && p.HostileTo(caster)){Items.Give(p,"LP_Soaked",SoakTicks);soaked++;}
     }
    }
    if(Rand.Chance(.6f))FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,1f,new Color(.55f,.75f,1f));
   }
   return soaked;
  }

  // ---------- 风跃：8格内、需要视线 ----------
  public static string CannotLeap(Pawn caster,IntVec3 c){
   var r=Races124.BasicLanding(caster,c,LeapRange);if(r!=null)return r;
   if(!GenSight.LineOfSight(caster.Position,c,caster.Map,true))return "风跃需要视线，不能穿墙。";
   return null;
  }
  public static bool Leap(Pawn caster,IntVec3 c){
   if(CannotLeap(caster,c)!=null)return false;var map=caster.Map;var from=caster.Position;
   for(int i=0;i<4;i++)FleckMaker.ThrowDustPuffThick(from.ToVector3Shifted(),map,1.2f,new Color(.85f,.9f,.95f));
   Races124.Blink(caster,c);
   for(int i=0;i<4;i++)FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,1.2f,new Color(.85f,.9f,.95f));
   FleckMaker.ThrowAirPuffUp(c.ToVector3Shifted(),map);
   return true;
  }
 }

 // 元素符被动（佩戴者与血脉是否一致两种情况）和风羽披风的移速，挂在原版属性上。
 public class StatPart_ElementalFocus : StatPart {
  static float Factor(Pawn p,StatDef stat)=>ElementalFocus.StatFactor(p,stat)*RaceItems124.CloakFactor(p,stat);
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p)val*=Factor(p,parentStat);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;float f=Factor(p,parentStat);return f==1f?null:"元素符/风羽披风：×"+f.ToStringPercent();}
 }
 public static class ElementalFocus {
  public const float CooldownFactor=.5f;
  public static ThingDef Def(string element)=>Items.Thing("LP_ElementalFocus_"+element);
  public static Apparel Worn(Pawn p,string element)=>Items.Worn(p,Def(element));
  // 本血脉佩戴：挖矿 1.3→1.6、移速 1.1→1.2（只算超出血脉本身的那部分）；其他人：挖矿×1.15、移速×1.05、可燃性×0.5。
  // 1.58.6：先看是哪个属性、戴没戴对应的元素符，戴了才算血脉（原来每个穿衣服的人每次算移速都先拼 4～5 个字符串、扫 4 次基因）。结果不变。
  static readonly DefRef<ThingDef> earth=new DefRef<ThingDef>("LP_ElementalFocus_Earth"),air=new DefRef<ThingDef>("LP_ElementalFocus_Air"),fire=new DefRef<ThingDef>("LP_ElementalFocus_Fire");
  public static float StatFactor(Pawn p,StatDef stat){
   if(p?.apparel==null || p.apparel.WornApparelCount==0)return 1f;
   if(stat==StatDefOf.MiningSpeed){if(Items.Worn(p,earth.Def)!=null)return 1f*(Genasi.Element(p)=="Earth"?1.6f/1.3f:1.15f);}
   else if(stat==StatDefOf.MoveSpeed){if(Items.Worn(p,air.Def)!=null)return 1f*(Genasi.Element(p)=="Air"?1.2f/1.1f:1.05f);}
   else if(stat==StatDefOf.Flammability){if(Items.Worn(p,fire.Def)!=null && Genasi.Element(p)!="Fire")return 1f*.5f;}
   return 1f;
  }
  // 炽焰血脉佩戴炽焰符：火焰伤害再×0.5（合计×0.25）。
  public static bool FireBoost(Pawn p)=>Genasi.Element(p)=="Fire" && Worn(p,"Fire")!=null;
 }
 // 岩石血脉：采矿产量+10%，以100%为上限（本来就超过100%的人不变）。写在这里而不是基因的 statOffsets 里，才能精确封顶。
 public class StatPart_EarthYieldCap : StatPart {
  public const float Bonus=.1f;
  public override void TransformValue(StatRequest req,ref float val){if(val<1f && req.Thing is Pawn p && Genasi.Element(p)=="Earth")val=Mathf.Min(1f,val+Bonus);}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Genasi.Element(p)=="Earth"?"岩石血脉：+10%（最高100%）":null;
 } [StaticConstructorOnStartup]
 public static class ElementalFocusStartup {
  static ElementalFocusStartup(){
   foreach(var stat in new[]{StatDefOf.MiningSpeed,StatDefOf.MoveSpeed,StatDefOf.Flammability}){
    if(stat.parts==null)stat.parts=new List<StatPart>();
    if(!stat.parts.Any(p=>p is StatPart_ElementalFocus))stat.parts.Add(new StatPart_ElementalFocus{parentStat=stat});
   }
   var yield=StatDefOf.MiningYield;if(yield.parts==null)yield.parts=new List<StatPart>();if(!yield.parts.Any(p=>p is StatPart_EarthYieldCap))yield.parts.Add(new StatPart_EarthYieldCap{parentStat=yield});
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class ElementalFocusFire {
  static void Prefix(Pawn __instance,ref DamageInfo __0){
   if(__0.Def==null || (__0.Def!=DamageDefOf.Flame && __0.Def!=DamageDefOf.Burn) || !ElementalFocus.FireBoost(__instance))return;
   __0.SetAmount(__0.Amount*.5f);
  }
 }
}
