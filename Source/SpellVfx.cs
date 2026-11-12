using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.121.0 法术特效：本模组所有能力（AbilityDef）施放时自动播放特效，不用逐个改 XML。
 // 预热：脚下法阵 + 向内汇聚的光点；施放：施法者身上闪光 / 扭曲波；有目标：光球飞向目标（雷电是闪电链），落点按元素爆发；
 // 范围法术按半径放大光环；自身增益是升起的符文柱；近战是弧光斩击。元素由能力名 / 描述关键词 + 所在文件推断，
 // 也可以在 AbilityDef 上加 <modExtensions><li Class="LichPhylactery.SpellVfxExtension"><theme>Fire</theme></li></modExtensions> 指定。
 // 贴图自绘（Textures/LP/Vfx，ArtSource/SpellVfx/draw_vfx.py），风格参考 VPE 但不依赖 VPE。设置里可以关闭或调大小。
 public enum VfxTheme { Arcane, Fire, Frost, Lightning, Necrotic, Holy, Heal, Shadow, Poison, Blood, Nature, Psychic, Force, Martial, Infernal, Eldritch,
  Divination, Abjuration, Conjuration, Transmutation, Enchantment, Illusion, Song, Moon, Wind } // 1.144.0 后九种：按法术学派 / 吟游诗人 / 月光与风
 public enum VfxKind { Self, Target, Melee, Projectile, Jump }
 public class SpellVfxExtension : DefModExtension { public VfxTheme theme=VfxTheme.Arcane; public bool off; }

 [DefOf]
 public static class LP_VfxDefOf {
  public static FleckDef LP_Vfx_Circle,LP_Vfx_Glow,LP_Vfx_Orb,LP_Vfx_Ring,LP_Vfx_Spark,LP_Vfx_Rune,LP_Vfx_Flake,LP_Vfx_Beam,LP_Vfx_Bolt,LP_Vfx_Slash,LP_Vfx_Smoke,LP_Vfx_Wave;
  static LP_VfxDefOf(){DefOfHelper.EnsureInitializedInCtor(typeof(LP_VfxDefOf));}
 }

 public partial class Settings {
  public bool spellVfx=true;public int spellVfxScale=100;
  void ExposeVfx(){Scribe_Values.Look(ref spellVfx,"spellVfx",true);Scribe_Values.Look(ref spellVfxScale,"spellVfxScale",100);spellVfxScale=Math.Max(50,Math.Min(200,spellVfxScale));}
 }
 public static class SpellVfxSettings {
  public static void Draw(Listing_Standard l){
   l.Label("<b>法术特效</b>");
   l.CheckboxLabeled("施放 DND 法术与职业能力时显示特效",ref LichMod.Settings.spellVfx,"预热时脚下出现法阵，施放时有光球飞向目标、落点按元素（火、冰、雷、死灵、神圣、治疗、暗影、毒、血、自然、心灵、力场、近战等）爆发；范围法术的光环按半径放大。只是视觉效果，不影响数值。");
   l.Label("特效大小："+LichMod.Settings.spellVfxScale+"%");LichMod.Settings.spellVfxScale=(int)l.Slider(LichMod.Settings.spellVfxScale,50,200);
   l.GapLine();
  }
 }

 public class VfxProfile { public VfxTheme theme; public VfxKind kind; public bool off; public float radius; }

 public static class SpellVfx {
  public static bool On=>LichMod.Settings?.spellVfx!=false;
  internal static float K=>(LichMod.Settings?.spellVfxScale??100)/100f;
  static readonly Dictionary<AbilityDef,VfxProfile> cache=new Dictionary<AbilityDef,VfxProfile>();
  static string ownPackage;
  public static bool Ours(AbilityDef d){
   if(d==null)return false;
   if(ownPackage==null)ownPackage=LoadedModManager.GetMod<LichMod>()?.Content?.PackageId??"local.lich.phylactery";
   return d.modContentPack!=null && string.Equals(d.modContentPack.PackageId,ownPackage,StringComparison.OrdinalIgnoreCase);
  }
  public static VfxProfile Profile(AbilityDef d){
   if(d==null)return null;
   if(cache.TryGetValue(d,out var p))return p;
   p=Ours(d)?Build(d):null;cache[d]=p;return p;
  }

  public static Color ColorOf(VfxTheme t){
   switch(t){
    case VfxTheme.Fire:return new Color(1f,.55f,.15f);
    case VfxTheme.Frost:return new Color(.55f,.85f,1f);
    case VfxTheme.Lightning:return new Color(.6f,.75f,1f);
    case VfxTheme.Necrotic:return new Color(.45f,1f,.55f);
    case VfxTheme.Holy:return new Color(1f,.88f,.45f);
    case VfxTheme.Heal:return new Color(.55f,1f,.6f);
    case VfxTheme.Shadow:return new Color(.55f,.35f,.85f);
    case VfxTheme.Poison:return new Color(.6f,.95f,.2f);
    case VfxTheme.Blood:return new Color(1f,.15f,.2f);
    case VfxTheme.Nature:return new Color(.45f,.85f,.3f);
    case VfxTheme.Psychic:return new Color(.95f,.45f,1f);
    case VfxTheme.Force:return new Color(.85f,.95f,1f);
    case VfxTheme.Martial:return new Color(1f,.7f,.4f);
    case VfxTheme.Infernal:return new Color(1f,.3f,.1f);
    case VfxTheme.Eldritch:return new Color(.5f,1f,.8f);
    case VfxTheme.Divination:return new Color(1f,.95f,.7f);
    case VfxTheme.Abjuration:return new Color(.6f,.85f,1f);
    case VfxTheme.Conjuration:return new Color(.75f,.5f,1f);
    case VfxTheme.Transmutation:return new Color(.4f,.95f,.75f);
    case VfxTheme.Enchantment:return new Color(1f,.55f,.85f);
    case VfxTheme.Illusion:return new Color(.8f,.6f,1f);
    case VfxTheme.Song:return new Color(1f,.75f,.95f);
    case VfxTheme.Moon:return new Color(.85f,.9f,1f);
    case VfxTheme.Wind:return new Color(.8f,1f,.95f);
    default:return new Color(.45f,.65f,1f);
   }
  }
  internal static Color Smoke(VfxTheme t){
   switch(t){
    case VfxTheme.Necrotic:return new Color(.1f,.25f,.12f,.8f);
    case VfxTheme.Shadow:return new Color(.08f,.04f,.12f,.85f);
    case VfxTheme.Poison:return new Color(.3f,.45f,.08f,.7f);
    case VfxTheme.Blood:return new Color(.35f,.02f,.04f,.75f);
    case VfxTheme.Infernal:return new Color(.12f,.05f,.04f,.8f);
    case VfxTheme.Fire:return new Color(.2f,.18f,.16f,.6f);
    case VfxTheme.Eldritch:return new Color(.05f,.2f,.18f,.75f);
    default:return new Color(.5f,.5f,.55f,.5f);
   }
  }

  // ===== 分类 =====
  static readonly (VfxTheme t,Regex r)[] Keys={
   (VfxTheme.Heal,new Regex("治疗|治愈|疗伤|愈合|回复|恢复生命|再生|复苏|复活|回气|圣疗|急救")),
   (VfxTheme.Fire,new Regex("火|炎|焰|灼|燃|熔|烈日|陨")),
   (VfxTheme.Frost,new Regex("冰|霜|寒|雪|冻")),
   (VfxTheme.Lightning,new Regex("雷|电|风暴|霹")),
   (VfxTheme.Necrotic,new Regex("死灵|亡灵|腐朽|骸|骨|凋|尸|枯萎|吸取生命|噬魂|灵魂|不死|巫妖")),
   (VfxTheme.Infernal,new Regex("地狱|恶魔|魔鬼|深渊|邪魔")),
   (VfxTheme.Psychic,new Regex("心灵|精神|魅惑|蛊惑|恐惧|幻象|幻术|催眠|支配|意念|夺心")),
   (VfxTheme.Holy,new Regex("神圣|神力|神明|神罚|圣光|圣|光明|祝福|天使|裁决|辉|誓")),
   (VfxTheme.Shadow,new Regex("暗影|阴影|黑暗|暗|影|夜|隐")),
   (VfxTheme.Poison,new Regex("毒|酸|腐蚀|孢")),
   (VfxTheme.Blood,new Regex("血")),
   (VfxTheme.Nature,new Regex("藤|荆棘|植物|野兽|自然|树|大地|岩|石|土|地震|德鲁伊")),
   (VfxTheme.Eldritch,new Regex("契约|宗主|邪术|异界|星界")),
   (VfxTheme.Force,new Regex("力场|护盾|屏障|冲击|击退|传送|跃迁|时空|闪现|迅捷|加速|音波|雷鸣")),
  };
  static VfxTheme? FileTheme(string f){
   if(string.IsNullOrEmpty(f))return null;
   if(f.Contains("MindFlayer"))return VfxTheme.Psychic;
   if(f.Contains("Abyss") || f.Contains("Fiend"))return VfxTheme.Infernal;
   if(f.Contains("Faith") || f.Contains("Ascension"))return VfxTheme.Holy;
   if(f.Contains("Pact") || f.Contains("Warlock"))return VfxTheme.Eldritch;
   if(f.Contains("Ooze"))return VfxTheme.Poison;
   if(f.Contains("LichSouls") || f=="Abilities.xml" || f.Contains("Necro"))return VfxTheme.Necrotic;
   if(f.Contains("FrostGiant"))return VfxTheme.Frost;
   if(f.Contains("StormGiant"))return VfxTheme.Lightning;
   if(f.Contains("ForceDragon") || f.Contains("CloudGiant"))return VfxTheme.Force;
   return null;
  }
  public static VfxTheme Classify(AbilityDef d){
   var ext=d.GetModExtension<SpellVfxExtension>();if(ext!=null)return ext.theme;
   if(SpellVfx2.Override.TryGetValue(d.defName,out var ov))return ov; // 1.144.0 逐个指定
   var score=new Dictionary<VfxTheme,int>();
   void Add(VfxTheme t,int n){score.TryGetValue(t,out var o);score[t]=o+n;}
   string label=d.label??"",desc=d.description??"";
   string head=desc.Length>24?desc.Substring(0,24):desc;
   foreach(var k in Keys){
    if(k.r.IsMatch(label))Add(k.t,6);
    if(k.r.IsMatch(head))Add(k.t,2);
    else if(k.r.IsMatch(desc))Add(k.t,1);
   }
   var ft=FileTheme(d.fileName);if(ft!=null)Add(ft.Value,3);
   var ce=d.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
   bool martial=ce?.stat!=null && (ce.stat.defName=="LP_MartialPower" || ce.stat.defName=="LP_MarksmanPower");
   if(martial)Add(VfxTheme.Martial,4);
   if(score.Count==0)return martial?VfxTheme.Martial:SpellVfx2.Fallback(d);
   int best=score.Values.Max();
   if(best<2)return martial?VfxTheme.Martial:SpellVfx2.Fallback(d);
   foreach(var k in Keys)if(score.TryGetValue(k.t,out var v) && v==best)return k.t; // 同分按关键词表顺序
   return score.First(kv=>kv.Value==best).Key;
  }
  static readonly Dictionary<Type,FieldInfo> radiusField=new Dictionary<Type,FieldInfo>();
  static float CompRadius(AbilityDef d){
   if(d.comps==null)return 0;
   foreach(var c in d.comps){
    if(c is CompProperties_ClassEffect)continue; // 职业能力施放时按强度算
    var t=c.GetType();
    if(!radiusField.TryGetValue(t,out var f)){f=t.GetField("radius",BindingFlags.Public|BindingFlags.Instance);if(f!=null && f.FieldType!=typeof(float))f=null;radiusField[t]=f;}
    if(f!=null){var v=(float)f.GetValue(c);if(v>0)return v;}
   }
   return 0;
  }
  static VfxProfile Build(AbilityDef d){
   var p=new VfxProfile{theme=Classify(d)};
   var ext=d.GetModExtension<SpellVfxExtension>();
   var ce=d.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
   p.off=ext?.off==true || ce?.passive==true;
   var vp=d.verbProperties;var vc=vp?.verbClass;
   if(vc!=null && typeof(Verb_LaunchProjectile).IsAssignableFrom(vc))p.kind=VfxKind.Projectile;
   else if(vc!=null && typeof(Verb_CastAbilityJump).IsAssignableFrom(vc))p.kind=VfxKind.Jump;
   else if(vp==null || !d.targetRequired || vp.range<=0.01f)p.kind=VfxKind.Self;
   else if(vp.range<=2.5f && d.hostile)p.kind=VfxKind.Melee;
   else p.kind=VfxKind.Target;
   p.radius=CompRadius(d);
   return p;
  }

  // ===== 播放 =====
  internal static void Fleck(Map map,FleckDef def,Vector3 pos,float scale,Color col,float rot=0,float rotRate=0,float? solid=null,float vAngle=0,float vSpeed=0){
   if(map==null || def==null || !pos.ShouldSpawnMotesAt(map))return;
   var data=FleckMaker.GetDataStatic(pos,map,def,scale);
   data.instanceColor=col;data.rotation=rot;data.rotationRate=rotRate;
   if(solid!=null)data.solidTimeOverride=solid.Value;
   if(vSpeed>0){data.velocityAngle=vAngle;data.velocitySpeed=vSpeed;}
   map.flecks.CreateFleck(data);
  }
  internal static void Line(Map map,FleckDef def,Vector3 a,Vector3 b,float width,Color col){
   if(map==null || !a.ShouldSpawnMotesAt(map))return;
   var dir=b-a;dir.y=0;float len=dir.magnitude;if(len<.3f)return;
   var data=FleckMaker.GetDataStatic(a+dir*.5f,map,def,1f);
   data.exactScale=new Vector3(len,1f,width);
   data.rotation=-Mathf.Atan2(dir.z,dir.x)*Mathf.Rad2Deg;
   data.instanceColor=col;
   map.flecks.CreateFleck(data);
  }
  internal static float AngleTo(Vector3 a,Vector3 b)=>Mathf.Atan2(b.x-a.x,b.z-a.z)*Mathf.Rad2Deg;
  internal static Color A(Color c,float a){c.a=a;return c;}
  internal static void Burst(Map map,FleckDef def,Vector3 pos,int n,float speedMin,float speedMax,float size,Color col){
   for(int i=0;i<n;i++)Fleck(map,def,pos+new Vector3(Rand.Range(-.2f,.2f),0,Rand.Range(-.2f,.2f)),size*Rand.Range(.7f,1.3f),col,Rand.Range(0f,360f),Rand.Range(-200f,200f),null,Rand.Range(0f,360f),Rand.Range(speedMin,speedMax));
  }
  internal static void Rising(Map map,FleckDef def,Vector3 pos,int n,float spread,float size,Color col){
   for(int i=0;i<n;i++)Fleck(map,def,pos+new Vector3(Rand.Range(-spread,spread),0,Rand.Range(-spread*.6f,spread*.4f)),size*Rand.Range(.7f,1.2f),col,0,0,Rand.Range(.2f,.5f),Rand.Range(-15f,15f),Rand.Range(.6f,1.3f));
  }

  // 预热：脚下法阵（近战是一团气）
  public static void Warmup(Pawn caster,VfxProfile p){
   var map=caster.Map;if(map==null)return;var pos=caster.DrawPos;var col=ColorOf(p.theme);float k=K;
   if(p.theme==VfxTheme.Martial){Fleck(map,LP_VfxDefOf.LP_Vfx_Glow,pos,2f*k,A(col,.55f));return;}
   Fleck(map,LP_VfxDefOf.LP_Vfx_Circle,pos,2.4f*k,A(col,.9f),Rand.Range(0f,360f),90f);
   Fleck(map,LP_VfxDefOf.LP_Vfx_Glow,pos,2.2f*k,A(col,.6f));
  }
  // 预热期间：光点从四周汇向施法者
  public static void WarmupTick(Pawn caster,VfxProfile p){
   var map=caster.Map;if(map==null || p.theme==VfxTheme.Martial)return;var c=caster.DrawPos;var col=ColorOf(p.theme);float k=K;
   for(int i=0;i<2;i++){
    float ang=Rand.Range(0f,360f),r=Rand.Range(1.1f,1.6f)*k;
    var from=c+Quaternion.AngleAxis(ang,Vector3.up)*Vector3.forward*r;
    Fleck(map,LP_VfxDefOf.LP_Vfx_Spark,from,.45f*k,col,0,0,.12f,AngleTo(from,c),r/.3f);
   }
  }

  public static void Cast(Pawn caster,Vector3 from,Map map,VfxProfile p,LocalTargetInfo target,float radius,bool casterOnly=false){
   if(map==null)return;
   var col=ColorOf(p.theme);float k=K;
   // 施法者身上
   if(p.theme==VfxTheme.Martial){
    Fleck(map,LP_VfxDefOf.LP_Vfx_Glow,from,2.2f*k,A(col,.7f));
    for(int i=0;i<3;i++)FleckMaker.ThrowDustPuffThick(from+new Vector3(Rand.Range(-.4f,.4f),0,Rand.Range(-.4f,.4f)),map,Rand.Range(.8f,1.3f),new Color(1,1,1,.6f));
   }else{
    Fleck(map,LP_VfxDefOf.LP_Vfx_Glow,from,3f*k,col);
    Fleck(map,LP_VfxDefOf.LP_Vfx_Wave,from,1.2f*k,Color.white);
   }
   if(casterOnly)return; // 1.144.0 有专属特效的法术只放施法者身上这一下
   bool selfTarget=!target.IsValid || target.Thing==caster || (target.Cell==caster.Position && p.kind==VfxKind.Self);
   switch(p.kind){
    case VfxKind.Projectile:return; // 自带弹丸
    case VfxKind.Jump:
     if(target.IsValid)SpellVfxQueue.Add(14,()=>Impact(map,target.CenterVector3,p,Math.Max(radius,1.2f),false));
     return;
    case VfxKind.Melee:
     if(target.IsValid)Impact(map,target.Thing?.DrawPos??target.CenterVector3,p,radius,true);
     return;
   }
   if(selfTarget){Aura(map,caster.DrawPos,p,radius);return;}
   Vector3 to=target.Thing?.DrawPos??target.CenterVector3;
   float dist=(to-from).MagnitudeHorizontal();
   if(p.theme==VfxTheme.Lightning){
    Line(map,LP_VfxDefOf.LP_Vfx_Bolt,from,to,1.2f*k,col);Line(map,LP_VfxDefOf.LP_Vfx_Beam,from,to,.8f*k,A(col,.6f));
    Impact(map,to,p,radius,false);return;
   }
   if(p.theme==VfxTheme.Martial){ // 战技远程（射击支）：一道光线
    Line(map,LP_VfxDefOf.LP_Vfx_Beam,from,to,.5f*k,A(col,.8f));Impact(map,to,p,radius,false);return;
   }
   // 光球飞过去，路上留下光点，到达时爆发
   const float speed=28f;float secs=Mathf.Min(dist/speed,.7f);int ticks=Mathf.Max(1,Mathf.RoundToInt(secs*60f));
   float ang=AngleTo(from,to),sp=dist/Math.Max(secs,.01f);
   Fleck(map,LP_VfxDefOf.LP_Vfx_Orb,from,2.2f*k,col,0,0,secs,ang,sp);
   Fleck(map,LP_VfxDefOf.LP_Vfx_Orb,from,1f*k,Color.white,0,0,secs,ang,sp);
   for(int i=1;i<=8;i++){float f=i/9f;int t=Mathf.RoundToInt(ticks*f);var at=Vector3.Lerp(from,to,f);SpellVfxQueue.Add(t,()=>Fleck(map,LP_VfxDefOf.LP_Vfx_Spark,at,.6f*k,col,0,0,.15f,Rand.Range(0f,360f),.5f));}
   SpellVfxQueue.Add(ticks,()=>Impact(map,to,p,radius,false));
  }

  // 自身：符文柱 + 光环
  static void Aura(Map map,Vector3 pos,VfxProfile p,float radius){
   var col=ColorOf(p.theme);float k=K;
   if(p.theme==VfxTheme.Martial){
    Fleck(map,LP_VfxDefOf.LP_Vfx_Ring,pos,(radius>0?radius*.5f:.6f)*k,col);
    Burst(map,LP_VfxDefOf.LP_Vfx_Spark,pos,8,1.5f,3f,.6f*k,col);
    return;
   }
   Fleck(map,LP_VfxDefOf.LP_Vfx_Ring,pos,(radius>0?radius*.5f:.5f)*k,col);
   if(radius>0)Fleck(map,LP_VfxDefOf.LP_Vfx_Wave,pos,radius*.8f,Color.white);
   Rising(map,p.theme==VfxTheme.Frost?LP_VfxDefOf.LP_Vfx_Flake:LP_VfxDefOf.LP_Vfx_Rune,pos,5,.6f,.55f*k,col);
   Rising(map,LP_VfxDefOf.LP_Vfx_Spark,pos,6,.7f,.5f*k,col);
   Extras(map,pos,p.theme,1f);
  }

  public static void Impact(Map map,Vector3 pos,VfxProfile p,float radius,bool melee){
   if(map==null)return;var col=ColorOf(p.theme);float k=K;
   if(melee){
    float rot=Rand.Range(0f,360f);
    Fleck(map,LP_VfxDefOf.LP_Vfx_Slash,pos,1.8f*k,col,rot);
    Fleck(map,LP_VfxDefOf.LP_Vfx_Slash,pos,1.4f*k,Color.white,rot+25f);
   }
   Fleck(map,LP_VfxDefOf.LP_Vfx_Glow,pos,Math.Max(2.6f,radius*2f)*k,col);
   Fleck(map,LP_VfxDefOf.LP_Vfx_Ring,pos,(radius>0?radius*.45f:.35f)*k,col);
   if(radius>0)Fleck(map,LP_VfxDefOf.LP_Vfx_Wave,pos,radius*.9f,Color.white);
   Burst(map,LP_VfxDefOf.LP_Vfx_Spark,pos,radius>0?14:9,2f,radius>0?radius*3f:4f,.6f*k,col);
   if(p.theme==VfxTheme.Martial || melee){FleckMaker.ThrowDustPuffThick(pos,map,1f*k,new Color(1,1,1,.5f));}
   Extras(map,pos,p.theme,Math.Max(1f,radius*.6f));
  }

  internal static void Extras(Map map,Vector3 pos,VfxTheme t,float s){
   var col=ColorOf(t);float k=K;int n=Mathf.Clamp(Mathf.RoundToInt(3*s),2,10);
   switch(t){
    case VfxTheme.Fire:case VfxTheme.Infernal:
     FleckMaker.ThrowFireGlow(pos,map,1.5f*k);for(int i=0;i<n;i++)FleckMaker.ThrowMicroSparks(pos+new Vector3(Rand.Range(-s,s)*.5f,0,Rand.Range(-s,s)*.5f),map);
     Burst(map,LP_VfxDefOf.LP_Vfx_Smoke,pos,n,.3f,.8f,1.2f*k,Smoke(t));break;
    case VfxTheme.Frost:
     Burst(map,LP_VfxDefOf.LP_Vfx_Flake,pos,n+2,.8f,2.2f,.6f*k,col);FleckMaker.ThrowAirPuffUp(pos,map);break;
    case VfxTheme.Lightning:
     FleckMaker.ThrowLightningGlow(pos,map,1.5f*k);
     for(int i=0;i<2;i++){var o=new Vector3(Rand.Range(-1f,1f),0,Rand.Range(-1f,1f))*s;Line(map,LP_VfxDefOf.LP_Vfx_Bolt,pos-o,pos+o,.8f*k,col);}break;
    case VfxTheme.Necrotic:case VfxTheme.Shadow:case VfxTheme.Poison:case VfxTheme.Blood:case VfxTheme.Eldritch:
     Burst(map,LP_VfxDefOf.LP_Vfx_Smoke,pos,n,.2f,.7f,1.3f*k,Smoke(t));
     if(t!=VfxTheme.Blood && t!=VfxTheme.Poison)Rising(map,LP_VfxDefOf.LP_Vfx_Rune,pos,2,.5f,.45f*k,col);break;
    case VfxTheme.Heal:
     for(int i=0;i<Math.Min(n,4);i++)FleckMaker.ThrowMetaIcon((pos+new Vector3(Rand.Range(-.5f,.5f),0,Rand.Range(-.5f,.5f))).ToIntVec3(),map,FleckDefOf.HealingCross);
     Rising(map,LP_VfxDefOf.LP_Vfx_Spark,pos,6,.6f,.5f*k,col);break;
    case VfxTheme.Holy:
     Rising(map,LP_VfxDefOf.LP_Vfx_Rune,pos,3,.6f,.5f*k,col);Fleck(map,LP_VfxDefOf.LP_Vfx_Glow,pos,4f*k,A(Color.white,.6f));break;
    case VfxTheme.Nature:
     FleckMaker.ThrowDustPuffThick(pos,map,1.2f*k,new Color(.6f,.5f,.35f,.7f));Rising(map,LP_VfxDefOf.LP_Vfx_Spark,pos,5,.6f,.5f*k,col);break;
    case VfxTheme.Psychic:case VfxTheme.Arcane:case VfxTheme.Divination:case VfxTheme.Enchantment:case VfxTheme.Illusion:case VfxTheme.Conjuration:case VfxTheme.Abjuration:case VfxTheme.Transmutation:
     Rising(map,LP_VfxDefOf.LP_Vfx_Rune,pos,3,.6f,.45f*k,col);break;
    case VfxTheme.Song:
     Rising(map,LP_VfxDefOf.LP_Vfx_Rune,pos,2,.6f,.45f*k,col);SpellVfx2.Notes(map,pos,3,col);break;
    case VfxTheme.Moon:
     Rising(map,LP_VfxDefOf.LP_Vfx_Spark,pos,6,.6f,.5f*k,col);Fleck(map,LP_VfxDefOf.LP_Vfx_Glow,pos,3f*k,A(Color.white,.5f));break;
    case VfxTheme.Wind:
     for(int i=0;i<3;i++)FleckMaker.ThrowAirPuffUp(pos,map);SpellVfx2.Swirl(map,pos,1.2f*s*k,A(col,.6f));break;
    case VfxTheme.Force:
     SpellVfxQueue.Add(6,()=>Fleck(map,LP_VfxDefOf.LP_Vfx_Ring,pos,.5f*s*k,col));for(int i=0;i<3;i++)FleckMaker.ThrowAirPuffUp(pos,map);break;
   }
  }

  // 测试用：最近一次播放
  public static AbilityDef LastDef;public static int LastTick=-1;public static int Played;
 }

 // 1.121.0（Peter）：瞄准时显示伤害范围——圆形（火球等，按实际半径，职业能力随强度变）、锥形 / 扇形（燃烧之手、龙息等）、
 // 直线（闪电束这类“线上每个敌人”，以及解离、射线类的射线路径）。形状从组件半径和描述（“半径 N”“锥形 N 格”“90°”“线上”“射线”）推断。
 public enum AimShape { None, Circle, Cone, Line }
 public class AimPreview { public AimShape shape; public float radius,length,halfAngle=45f; public bool classRadius; }
 public static class SpellAim {
  static readonly Dictionary<AbilityDef,AimPreview> cache=new Dictionary<AbilityDef,AimPreview>();
  static readonly Regex RadiusRx=new Regex(@"半径\s*(\d+(?:\.\d+)?)");
  static readonly Regex ConeRx=new Regex(@"(?:(\d+(?:\.\d+)?)\s*格(?:长)?的?\s*(?:锥形|扇形))|(?:(?:锥形|扇形)(?:区域)?\s*[（(]?\s*(\d+(?:\.\d+)?)\s*格)");
  static readonly Regex DegRx=new Regex(@"(\d+)\s*°");
  static readonly Regex LineRx=new Regex("线上|直线|射线|光束|一道闪电");
  public static AimPreview Of(AbilityDef d){
   if(d==null)return null;
   if(cache.TryGetValue(d,out var a))return a;
   a=SpellVfx.Ours(d)?Build(d):null;cache[d]=a;return a;
  }
  static AimPreview Build(AbilityDef d){
   var a=new AimPreview();var prof=SpellVfx.Profile(d);
   if(prof==null || prof.kind==VfxKind.Self || prof.kind==VfxKind.Melee)return a;
   string desc=d.description??"";var ce=d.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
   float range=d.verbProperties?.range??0f;
   var cm=ConeRx.Match(desc);
   if(desc.Contains("锥形") || desc.Contains("扇形")){
    a.shape=AimShape.Cone;
    float len=0;if(cm.Success)float.TryParse(cm.Groups[1].Success?cm.Groups[1].Value:cm.Groups[2].Value,out len);
    a.length=len>0?len:(ce?.radius>0?ce.radius:Math.Max(5f,Math.Min(range,9f)));
    var dg=DegRx.Match(desc);if(dg.Success && float.TryParse(dg.Groups[1].Value,out var deg))a.halfAngle=deg/2f;
    else if(d.fileName!=null && (d.fileName.Contains("Faith") || d.fileName.Contains("DndTraits")))a.halfAngle=30f;
    return a;
   }
   if(ce!=null && ce.radius>0){a.shape=AimShape.Circle;a.radius=ce.radius;a.classRadius=true;return a;}
   if(prof.radius>0){a.shape=AimShape.Circle;a.radius=prof.radius;return a;}
   var rm=RadiusRx.Match(desc);
   if(rm.Success && float.TryParse(rm.Groups[1].Value,out var r) && r>0 && r<=20){a.shape=AimShape.Circle;a.radius=r;return a;}
   if(ce?.effect=="LightningBolt" || LineRx.IsMatch(desc)){a.shape=AimShape.Line;a.length=range;return a;}
   return a;
  }
  static readonly List<IntVec3> cells=new List<IntVec3>();
  // 2026-11-06 审查：瞄准时每帧都会调 Cells / Draw。原来每帧扫两遍组件列表（OfType）、重算两次职业半径、重新铺格子（直线还走 LINQ 去重）。
  // 现在组件按定义缓存；施法者位置、目标格、半径、能力都没变时直接用上一帧的格子。
  static readonly Dictionary<AbilityDef,CompProperties_ClassEffect> effects=new Dictionary<AbilityDef,CompProperties_ClassEffect>();
  static CompProperties_ClassEffect Effect(AbilityDef d){if(d==null)return null;if(!effects.TryGetValue(d,out var ce))effects[d]=ce=d.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();return ce;}
  static float RadiusFor(Pawn caster,AbilityDef d,AimPreview a){float r=a.radius;if(a.classRadius){var ce=Effect(d);if(ce!=null)r=ClassEffects.Radius(caster,ce);}return Mathf.Min(r,GenRadial.MaxRadialPatternRadius-1f);}
  static AbilityDef lastDef;static Map lastMap;static IntVec3 lastFrom=IntVec3.Invalid,lastTo=IntVec3.Invalid;static float lastR=-1f;
  public static List<IntVec3> Cells(Pawn caster,AbilityDef d,AimPreview a,LocalTargetInfo target){
   var map=caster?.Map;if(map==null || !target.IsValid){cells.Clear();lastDef=null;return cells;}
   IntVec3 from=caster.Position,to=target.Cell;float r=a.shape==AimShape.Circle?RadiusFor(caster,d,a):a.length;
   if(d==lastDef && map==lastMap && from==lastFrom && to==lastTo && r==lastR)return cells;
   cells.Clear();lastDef=d;lastMap=map;lastFrom=from;lastTo=to;lastR=r;
   switch(a.shape){
    case AimShape.Circle:
     foreach(var c in GenRadial.RadialCellsAround(to,r,true))if(c.InBounds(map))cells.Add(c);
     break;
    case AimShape.Cone:{
     var dir=(to-from).ToVector3();if(dir.sqrMagnitude<.01f)break;
     foreach(var c in GenRadial.RadialCellsAround(from,Mathf.Min(a.length,GenRadial.MaxRadialPatternRadius-1f),false)){
      if(!c.InBounds(map))continue;if(Vector3.Angle(dir,(c-from).ToVector3())<=a.halfAngle)cells.Add(c);
     }
     break;}
    case AimShape.Line:
     foreach(var c in GenSight.PointsOnLineOfSight(from,to))if(c!=from && c.InBounds(map) && !cells.Contains(c))cells.Add(c);
     if(to!=from && to.InBounds(map) && !cells.Contains(to))cells.Add(to);
     break;
   }
   return cells;
  }
  public static void Draw(Pawn caster,AbilityDef d,LocalTargetInfo target){
   var a=Of(d);if(a==null || a.shape==AimShape.None || caster?.Map==null || !target.IsValid)return;
   var col=SpellVfx.ColorOf(SpellVfx.Profile(d).theme);
   if(a.shape==AimShape.Circle)GenDraw.DrawRadiusRing(target.Cell,RadiusFor(caster,d,a),col);
   var list=Cells(caster,d,a,target);
   if(list.Count>0)GenDraw.DrawFieldEdges(list,col);
  }
 }
 [HarmonyPatch(typeof(Verb_CastAbility),nameof(Verb_CastAbility.DrawHighlight))]
 public static class SpellAim_DrawHighlight {
  static void Postfix(Verb_CastAbility __instance,LocalTargetInfo target){
   try{if(__instance?.ability!=null && __instance.CasterPawn!=null)SpellAim.Draw(__instance.CasterPawn,__instance.ability.def,target);}catch(Exception e){Log.ErrorOnce("[LP] 范围指示出错："+e,0x5f7a1c7);}
  }
 }

 // 延迟播放（光球到达后爆发）。不存档。
 public class SpellVfxQueue : GameComponent {
  static readonly List<(int tick,Action act)> q=new List<(int,Action)>();
  public SpellVfxQueue(Game game){q.Clear();}
  public static int Pending=>q.Count;
  public static void Add(int delay,Action a){if(delay<=0 || Find.TickManager==null){Safe(a);return;}q.Add((Find.TickManager.TicksGame+delay,a));}
  static void Safe(Action a){try{a();}catch(Exception e){Log.ErrorOnce("[LP] 法术特效出错："+e,0x5f7a1c3);}}
  public override void GameComponentTick(){
   if(q.Count==0)return;int now=Find.TickManager.TicksGame;
   for(int i=q.Count-1;i>=0;i--)if(q[i].tick<=now){var a=q[i].act;q.RemoveAt(i);Safe(a);}
   if(q.Count>400)q.RemoveRange(0,q.Count-400);
  }
 }

 [HarmonyPatch(typeof(Ability),nameof(Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class SpellVfx_Activate {
  static void Prefix(Ability __instance,out (Vector3 pos,Map map) __state){var p=__instance?.pawn;__state=p!=null && p.Spawned?(p.DrawPos,p.Map):(Vector3.zero,null);}
  static void Postfix(Ability __instance,LocalTargetInfo target,bool __result,(Vector3 pos,Map map) __state){
   if(!__result || __state.map==null || !SpellVfx.On)return;
   try{
    var prof=SpellVfx.Profile(__instance.def);if(prof==null || prof.off)return;
    float r=prof.radius;
    var ce=__instance.def.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
    if(ce!=null && ce.radius>0)r=ClassEffects.Radius(__instance.pawn,ce);
    bool sig=SpellVfx2.Has(__instance.def);
    SpellVfx.Cast(__instance.pawn,__state.pos,__state.map,prof,target,Mathf.Min(r,12f),sig);
    if(sig)SpellVfx2.Play(__instance.pawn,__state.pos,__state.map,__instance.def,prof,target,Mathf.Min(r,12f));
    SpellVfx2.OnCast(__instance.pawn,__instance.def,prof);
    SpellVfx.LastDef=__instance.def;SpellVfx.LastTick=Find.TickManager.TicksGame;SpellVfx.Played++;
   }catch(Exception e){Log.ErrorOnce("[LP] 法术特效出错："+e,0x5f7a1c4);}
  }
 }

 // 预热：开始时法阵，之后每 5 刻汇聚光点
 [HarmonyPatch(typeof(Stance_Warmup),MethodType.Constructor,new[]{typeof(int),typeof(LocalTargetInfo),typeof(Verb)})]
 public static class SpellVfx_WarmupStart {
  static void Postfix(Verb verb){
   if(!SpellVfx.On || !(verb is Verb_CastAbility va) || !(verb.caster is Pawn p) || !p.Spawned)return;
   try{var prof=SpellVfx.Profile(va.ability?.def);if(prof!=null && !prof.off)SpellVfx.Warmup(p,prof);}catch(Exception e){Log.ErrorOnce("[LP] 法术特效出错："+e,0x5f7a1c5);}
  }
 }
 [HarmonyPatch(typeof(Stance_Warmup),nameof(Stance_Warmup.StanceTick))]
 public static class SpellVfx_WarmupTick {
  static void Postfix(Stance_Warmup __instance){
   if(!SpellVfx.On || __instance.ticksLeft%5!=0 || !(__instance.verb is Verb_CastAbility va) || !(va.caster is Pawn p) || !p.Spawned)return;
   try{var prof=SpellVfx.Profile(va.ability?.def);if(prof!=null && !prof.off)SpellVfx.WarmupTick(p,prof);}catch(Exception e){Log.ErrorOnce("[LP] 法术特效出错："+e,0x5f7a1c6);}
  }
 }
}
