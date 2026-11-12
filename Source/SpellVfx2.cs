using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.144.0 法术特效第二批（Peter）：
 //  1）原来认不出元素、只放蓝色奥术特效的法术逐个指定元素（Override），其余按法术学派 / 职业兜底（Fallback）；
 //  2）持续性法术（法术区域 SpellZone、绕身光环）在持续期间一直有场地特效，区域消失或状态没了就停；
 //  3）4 环以上的法术各有一套专属特效（Sig），施放时取代通用的“光球 + 爆发”，施法者身上的闪光照旧。
 // 贴图 Textures/LP/Vfx（ArtSource/SpellVfx/draw_vfx2.py），FleckDef 在 Defs/SpellVfx.xml。设置里的“法术特效”开关和大小照样生效。
 public static class SpellVfx2 {
  // ===== 1）元素 =====
  public static readonly Dictionary<string,VfxTheme> Override=new Dictionary<string,VfxTheme>{
   {"LP_FeatFabricate",VfxTheme.Transmutation},
   {"LP_CS_Thaumaturgy",VfxTheme.Holy},{"LP_CS_Resistance",VfxTheme.Abjuration},{"LP_CS_Command",VfxTheme.Enchantment},{"LP_CS_CreateWater",VfxTheme.Frost},
   {"LP_CS_PurifyFood",VfxTheme.Holy},{"LP_CS_GuidingBolt",VfxTheme.Holy},{"LP_CS_Aid",VfxTheme.Holy},{"LP_CS_SpiritualWeapon",VfxTheme.Holy},{"LP_CS_SpiritGuardians",VfxTheme.Holy},
   {"LP_CS_CreateFood",VfxTheme.Conjuration},{"LP_CS_Daylight",VfxTheme.Holy},{"LP_CS_RemoveCurse",VfxTheme.Abjuration},{"LP_CS_FreedomOfMovement",VfxTheme.Abjuration},
   {"LP_CS_GreaterRestoration",VfxTheme.Heal},{"LP_CS_HeroesFeast",VfxTheme.Holy},{"LP_CS_ConjureCelestial",VfxTheme.Holy},{"LP_CS_Goodberry",VfxTheme.Nature},
   {"LP_CS_Jump",VfxTheme.Transmutation},{"LP_CS_Moonbeam",VfxTheme.Moon},{"LP_CS_FindTraps",VfxTheme.Divination},{"LP_CS_AnimalMessenger",VfxTheme.Nature},
   {"LP_CS_WindWall",VfxTheme.Wind},{"LP_CS_InsectPlague",VfxTheme.Nature},{"LP_CS_Awaken",VfxTheme.Nature},{"LP_CS_Reincarnate",VfxTheme.Nature},{"LP_CS_WindWalk",VfxTheme.Wind},
   {"LP_CS_Whirlwind",VfxTheme.Wind},{"LP_CS_AnimalShapes",VfxTheme.Nature},{"LP_CS_DragonsBreath",VfxTheme.Fire},{"LP_CS_SpiderClimb",VfxTheme.Transmutation},
   {"LP_CS_HungerOfHadar",VfxTheme.Eldritch},{"LP_CS_PowerWordStun",VfxTheme.Enchantment},{"LP_CS_Heroism",VfxTheme.Enchantment},{"LP_CS_Tongues",VfxTheme.Divination},
   {"LP_CS_Compulsion",VfxTheme.Enchantment},{"LP_CS_OttoDance",VfxTheme.Enchantment},{"LP_CS_MagicWeapon",VfxTheme.Transmutation},{"LP_CS_FindSteed",VfxTheme.Holy},
   {"LP_CS_SmiteBrand",VfxTheme.Holy},{"LP_CS_CrusadersMantle",VfxTheme.Holy},{"LP_CS_SmiteBlind",VfxTheme.Holy},{"LP_CS_SmiteBanish",VfxTheme.Force},{"LP_CS_Ensnaring",VfxTheme.Nature},
   {"LP_CS_PassWithoutTrace",VfxTheme.Shadow},{"LP_CS_ConjureBarrage",VfxTheme.Martial},{"LP_CS_ConjureVolley",VfxTheme.Martial},{"LP_CS_AbsorbElements",VfxTheme.Abjuration},
   {"LP_CS_Identify",VfxTheme.Divination},{"LP_CS_HeatMetal",VfxTheme.Fire},{"LP_CS_Levitate",VfxTheme.Wind},
   {"LP_AC_IrresistibleDance",VfxTheme.Song},{"LP_AC_PowerWordStun",VfxTheme.Enchantment},{"LP_AC_Foresight",VfxTheme.Divination},{"LP_AC_ReverseGravity",VfxTheme.Transmutation},
   {"LP_AC_Maze",VfxTheme.Conjuration},{"LP_AC_TimeStop",VfxTheme.Transmutation},{"LP_AC_PowerWordKill",VfxTheme.Necrotic},{"LP_AC_Wish",VfxTheme.Conjuration},{"LP_AC_SunArrow",VfxTheme.Holy},
   {"LP_LS_Mending",VfxTheme.Transmutation},{"LP_LS_PurifyFood",VfxTheme.Holy},{"LP_LS_Alarm",VfxTheme.Abjuration},{"LP_LS_Longstrider",VfxTheme.Wind},{"LP_LS_ArcaneLock",VfxTheme.Abjuration},
   {"LP_LS_Augury",VfxTheme.Divination},{"LP_LS_FalseLife",VfxTheme.Necrotic},{"LP_LS_CreateFood",VfxTheme.Conjuration},{"LP_LS_CalmMind",VfxTheme.Enchantment},
   {"LP_LS_Counterspell",VfxTheme.Abjuration},{"LP_LS_TinyHut",VfxTheme.Abjuration},
   {"LP_DS_Favored",VfxTheme.Holy},{"LP_CW_Domain",VfxTheme.Force},{"LP_CW_Legion",VfxTheme.Conjuration},{"LP_MO_CombatShape",VfxTheme.Nature},{"LP_VN_AbjureEnemy",VfxTheme.Holy},
   {"LP_VN_SoulOfVengeance",VfxTheme.Blood},{"LP_WD_Avatar",VfxTheme.Holy},{"LP_KD_ReadThoughts",VfxTheme.Psychic},{"LP_KD_Visions",VfxTheme.Divination},{"LP_HX_Curse",VfxTheme.Shadow},
   {"LP_DI_ThirdEye",VfxTheme.Divination},{"LP_AJ_ArcaneWard",VfxTheme.Abjuration},{"LP_AJ_ProjectedWard",VfxTheme.Abjuration},{"LP_SB_ElegantManeuver",VfxTheme.Martial},
   {"LP_SB_MasterDuelist",VfxTheme.Martial},{"LP_SN_PsychicBlades",VfxTheme.Psychic},{"LP_SN_SoulThrow",VfxTheme.Psychic},{"LP_SN_PsychicVeil",VfxTheme.Psychic},
   {"LP_WS_Mending",VfxTheme.Transmutation},{"LP_WS_Prestidigitation",VfxTheme.Transmutation},{"LP_WS_Light",VfxTheme.Divination},{"LP_WS_MageHand",VfxTheme.Conjuration},
   {"LP_WS_FogCloud",VfxTheme.Wind},{"LP_WS_Grease",VfxTheme.Conjuration},{"LP_WS_FindFamiliar",VfxTheme.Conjuration},{"LP_WS_Alarm",VfxTheme.Abjuration},
   {"LP_WS_ComprehendLanguages",VfxTheme.Divination},{"LP_WS_DetectMagic",VfxTheme.Divination},{"LP_WS_Web",VfxTheme.Conjuration},{"LP_WS_HoldPerson",VfxTheme.Enchantment},
   {"LP_WS_EnhanceAbility",VfxTheme.Transmutation},{"LP_WS_ArcaneLock",VfxTheme.Abjuration},{"LP_WS_Knock",VfxTheme.Force},{"LP_WS_DispelMagic",VfxTheme.Abjuration},
   {"LP_WS_Fly",VfxTheme.Wind},{"LP_WS_Slow",VfxTheme.Transmutation},{"LP_WS_ProtectionFromEnergy",VfxTheme.Abjuration},{"LP_WS_MagicCircle",VfxTheme.Abjuration},
   {"LP_WS_Sending",VfxTheme.Divination},{"LP_WS_Counterspell",VfxTheme.Abjuration},{"LP_WS_Polymorph",VfxTheme.Transmutation},{"LP_WS_BlackTentacles",VfxTheme.Eldritch},
   {"LP_WS_Banishment",VfxTheme.Abjuration},{"LP_WS_Confusion",VfxTheme.Enchantment},{"LP_WS_Fabricate",VfxTheme.Transmutation},{"LP_WS_FaithfulHound",VfxTheme.Conjuration},
   {"LP_WS_HoldMonster",VfxTheme.Enchantment},{"LP_WS_ModifyMemory",VfxTheme.Enchantment},{"LP_WS_Scrying",VfxTheme.Divination},{"LP_WS_LegendLore",VfxTheme.Divination},
   {"LP_WS_TrueSeeing",VfxTheme.Divination},{"LP_WS_MoveEarth",VfxTheme.Nature},{"LP_WS_DimensionDoor",VfxTheme.Conjuration},{"LP_WS_ReverseGravity",VfxTheme.Transmutation},
   {"LP_WS_Etherealness",VfxTheme.Illusion},{"LP_WS_MagnificentMansion",VfxTheme.Conjuration},{"LP_WS_Maze",VfxTheme.Conjuration},{"LP_WS_Feeblemind",VfxTheme.Enchantment},
   {"LP_WS_Antipathy",VfxTheme.Enchantment},{"LP_WS_TimeStop",VfxTheme.Transmutation},{"LP_WS_Wish",VfxTheme.Conjuration},{"LP_WS_PowerWordKill",VfxTheme.Necrotic},
   {"LP_WS_Foresight",VfxTheme.Divination},{"LP_WS_MassPolymorph",VfxTheme.Transmutation},
  };
  static readonly Dictionary<string,VfxTheme> Schools=new Dictionary<string,VfxTheme>{
   {"塑能",VfxTheme.Force},{"惑控",VfxTheme.Enchantment},{"预言",VfxTheme.Divination},{"防护",VfxTheme.Abjuration},
   {"咒法",VfxTheme.Conjuration},{"变化",VfxTheme.Transmutation},{"幻术",VfxTheme.Illusion},{"死灵",VfxTheme.Necrotic}};
  // 关键词认不出来时：吟游诗人 → 歌声；法术学派；都没有就是奥术。
  public static VfxTheme Fallback(AbilityDef d){
   if(d.defName.StartsWith("LP_BD_"))return VfxTheme.Song;
   string school=d.GetModExtension<WizardSpellExt>()?.school??d.GetModExtension<CasterSpellExt>()?.school;
   if(school!=null)foreach(var kv in Schools)if(school.Contains(kv.Key))return kv.Value;
   return VfxTheme.Arcane;
  }

  // ===== 小工具 =====
  public class Ctx { public Map map;public Pawn caster;public Vector3 from,to;public Pawn tp;public float r;public Color col;public float k;public bool self;public VfxTheme theme; }
  static readonly Dictionary<string,FleckDef> defs=new Dictionary<string,FleckDef>();
  static FleckDef D(string n){if(!defs.TryGetValue(n,out var d))defs[n]=d=DefDatabase<FleckDef>.GetNamedSilentFail("LP_Vfx_"+n);return d;}
  static Color C(float r,float g,float b,float a=1f)=>new Color(r,g,b,a);
  static Color A(Color c,float a){c.a=a;return c;}
  static void Later(int t,Action a)=>SpellVfxQueue.Add(t,a);
  static Vector3 Disc(Vector3 c,float r){var v=Rand.InsideUnitCircle*r;return c+new Vector3(v.x,0,v.y);}
  static Vector3 Up(float z)=>new Vector3(0,0,z);
  static void I(Ctx c,string n,Vector3 pos,float size,Color col,float rot=0,float rotRate=0,float solid=-1,float vAng=0,float vSpd=0)=>
   SpellVfx.Fleck(c.map,D(n),pos,size*c.k,col,rot,rotRate,solid<0?(float?)null:solid,vAng,vSpd);
  static void Glow(Ctx c,Vector3 p,float s,Color col)=>I(c,"Glow",p,s,col);
  static void Ring(Ctx c,Vector3 p,float s,Color col)=>I(c,"Ring",p,s,col);
  static void Wave(Ctx c,Vector3 p,float s)=>I(c,"Wave",p,s,Color.white);
  static void Line(Ctx c,string n,Vector3 a,Vector3 b,float w,Color col)=>SpellVfx.Line(c.map,D(n),a,b,w*c.k,col);
  static void Sparks(Ctx c,Vector3 p,int n,float spd,Color col)=>SpellVfx.Burst(c.map,D("Spark"),p,n,spd*.5f,spd,.6f*c.k,col);
  static void Smoke(Ctx c,Vector3 p,int n,Color col)=>SpellVfx.Burst(c.map,D("Smoke"),p,n,.2f,.8f,1.4f*c.k,col);
  static void Rise(Ctx c,string n,Vector3 p,int count,float spread,float size,Color col)=>SpellVfx.Rising(c.map,D(n),p,count,spread,size*c.k,col);
  // 光柱：底部落在 p，向上 h 格。
  static void Pillar(Ctx c,Vector3 p,float h,float w,Color col,float solid=.5f){
   var map=c.map;var d=D("Pillar");if(map==null || d==null || !p.ShouldSpawnMotesAt(map))return;
   var data=FleckMaker.GetDataStatic(p+Up(h*c.k*.5f),map,d,1f);data.exactScale=new Vector3(w*c.k,1f,h*c.k);data.instanceColor=col;data.solidTimeOverride=solid;map.flecks.CreateFleck(data);
  }
  static void Sky(Ctx c,Vector3 p,Color col){Line(c,"Bolt",p+Up(7f),p,1.4f,col);Line(c,"Beam",p+Up(7f),p,.6f,A(col,.6f));FleckMaker.ThrowLightningGlow(p,c.map,1.6f*c.k);Glow(c,p,2.6f,col);}
  static void Tentacles(Ctx c,Vector3 center,float rad,int n,Color col,int stagger=3,float solid=.9f){
   for(int i=0;i<n;i++){int t=i*stagger;Later(t,()=>{var p=Disc(center,rad);float s=Rand.Range(1.3f,2.1f);I(c,"Tentacle",p+Up(s*c.k*.45f),s,col,Rand.Range(-25f,25f),0,solid);Smoke(c,p,1,C(.05f,.03f,.08f,.7f));});}
  }
  static void Rain(Ctx c,string n,Vector3 center,float rad,int count,Color col,int span,float size=1.6f){
   for(int i=0;i<count;i++){int t=Rand.Range(0,span);Later(t,()=>{var p=Disc(center,rad);I(c,n,p+Up(3f),size,col,180f,0,.18f,180f,14f);Later(13,()=>{Glow(c,p,.9f,col);Sparks(c,p,3,2f,col);});});}
  }
  // 锁链从 p 向四周甩出（锁链贴图只能按线段画）。
  static void Chains(Ctx c,Vector3 p,int n,float len,Color col){float o=Rand.Range(0f,360f);for(int i=0;i<n;i++){var q=p+Quaternion.AngleAxis(o+360f*i/n,Vector3.up)*Vector3.forward*len*c.k;Line(c,"Chain",p,q,.5f,col);}}
  static void Nova(Ctx c,string n,Vector3 p,int count,float speed,float size,Color col,float solid=.25f){
   for(int i=0;i<count;i++){float a=360f*i/count+Rand.Range(-8f,8f);I(c,n,p,size,col,a,0,solid,a,speed);}
  }
  static void Orbit(Ctx c,string n,Vector3 p,float rad,int count,float size,Color col,float rotRate=0,float solid=.5f){
   float o=Rand.Range(0f,360f);
   for(int i=0;i<count;i++){float a=o+360f*i/count;var q=p+Quaternion.AngleAxis(a,Vector3.up)*Vector3.forward*rad*c.k;I(c,n,q,size,col,a,rotRate,solid);}
  }
  static void Icon(Ctx c,string n,Vector3 p,float size,Color col,float rotRate=0,float solid=.6f)=>I(c,n,p,size,col,0,rotRate,solid);
  static void Fire(Ctx c,Vector3 p,float s){FleckMaker.ThrowFireGlow(p,c.map,s*c.k);for(int i=0;i<3;i++)FleckMaker.ThrowMicroSparks(Disc(p,s*.4f),c.map);}
  static void Dust(Ctx c,Vector3 p,float s,Color col)=>FleckMaker.ThrowDustPuffThick(p,c.map,s*c.k,col);
  static void Frost(Ctx c,Vector3 p,int n)=>SpellVfx.Burst(c.map,D("Flake"),p,n,.8f,2.4f,.6f*c.k,C(.65f,.9f,1f));
  // 给 SpellVfx.Extras 用。
  public static void Notes(Map map,Vector3 p,int n,Color col){var d=DefDatabase<FleckDef>.GetNamedSilentFail("LP_Vfx_Note");if(d==null)return;for(int i=0;i<n;i++)SpellVfx.Fleck(map,d,p+new Vector3(Rand.Range(-.6f,.6f),0,Rand.Range(-.3f,.3f)),Rand.Range(.4f,.6f)*SpellVfx.K,col,Rand.Range(-15f,15f),0,.5f,Rand.Range(-25f,25f),Rand.Range(.5f,1f));}
  public static void Swirl(Map map,Vector3 p,float s,Color col){var d=DefDatabase<FleckDef>.GetNamedSilentFail("LP_Vfx_Swirl");if(d!=null)SpellVfx.Fleck(map,d,p,s,col,Rand.Range(0f,360f),-240f);}

  // ===== 3）专属特效（4 环以上） =====
  static readonly Color Black=C(.12f,.05f,.18f),Gold=C(1f,.85f,.4f),Silver=C(.9f,.93f,1f),Violet=C(.7f,.45f,1f),Green=C(.45f,1f,.5f),Toxic=C(.6f,.95f,.2f),Ember=C(1f,.45f,.12f),Ice=C(.6f,.88f,1f),Pink=C(1f,.55f,.85f),Teal=C(.35f,.95f,.8f),Brown=C(.65f,.5f,.3f),Red=C(1f,.18f,.18f),Pale=C(.85f,1f,.9f),Abyss=C(.25f,.08f,.35f);
  static readonly Dictionary<string,Action<Ctx>> Sig=new Dictionary<string,Action<Ctx>>{
   // ---- 4 环 ----
   {"LP_CS_ConjureWoodland",c=>{Rise(c,"Leaf",c.to,10,1.4f,.8f,Green);Ring(c,c.to,1.4f,Green);Dust(c,c.to,1.6f,C(.5f,.6f,.3f,.6f));}},
   {"LP_CS_Divination",c=>{Icon(c,"Eye",c.from+Up(1.6f),2.2f,Gold,0,.9f);Rise(c,"Spark",c.from,10,.8f,.5f,Gold);Ring(c,c.from,1f,Gold);}},
   {"LP_CS_DominateBeast",c=>{Line(c,"Chain",c.from,c.to,.7f,Pink);Icon(c,"Mask",c.to+Up(1f),1.2f,Pink);Rise(c,"Rune",c.to,4,.5f,.5f,Pink);}},
   {"LP_CS_FreedomOfMovement",c=>{Chains(c,c.to,4,1.4f,Silver);Wave(c,c.to,1.4f);Rise(c,"Feather",c.to,4,.6f,.7f,Silver);}},
   {"LP_CS_GuardianOfFaith",c=>{Pillar(c,c.to,5f,1.6f,Gold,.8f);Icon(c,"Shard",c.to+Up(1.5f),2.4f,Gold,0,.9f);Ring(c,c.to,1.6f,Gold);}},
   {"LP_CS_StoneShape",c=>{Icon(c,"Crack",c.to,2.2f,Brown,0,.8f);Dust(c,c.to,2f,C(.55f,.5f,.45f,.8f));Nova(c,"Shard",c.to,6,2.5f,.6f,Brown);}},
   {"LP_CS_Compulsion",c=>{Icon(c,"Swirl",c.to,c.r*.8f+1f,Pink,-200f);Notes(c.map,c.to,4,Pink);Ring(c,c.to,c.r*.45f,Pink);}},
   {"LP_CS_GreaterInvisibility",c=>{Icon(c,"Mask",c.to+Up(.8f),1.4f,A(Silver,.6f));Rise(c,"Spark",c.to,12,.6f,.4f,A(Silver,.5f));Wave(c,c.to,1.2f);}},
   {"LP_CS_SmiteStagger",c=>{Icon(c,"Sun",c.from,2f,Gold);I(c,"Slash",c.from,2.2f,Gold,Rand.Range(0f,360f));Rise(c,"Rune",c.from,3,.4f,.5f,Gold);}},
   {"LP_WS_ArcaneEye",c=>{Icon(c,"Eye",c.to+Up(1.2f),2f,Violet,0,1.2f);Ring(c,c.to,1.2f,Violet);Sparks(c,c.to,8,2f,Violet);}},
   {"LP_WS_Banishment",c=>{Icon(c,"Portal",c.to,2.4f,Abyss,-120f,.8f);Later(30,()=>{Glow(c,c.to,2f,Violet);Nova(c,"Spark",c.to,10,3f,.6f,Violet);});}},
   {"LP_WS_BlackTentacles",c=>{Icon(c,"Crack",c.to,c.r*.9f+1f,Black,0,1f);Tentacles(c,c.to,c.r,9,Black,3);Smoke(c,c.to,6,C(.06f,.02f,.1f,.85f));}},
   {"LP_WS_Blight",c=>{Icon(c,"Skull",c.to+Up(.6f),1.2f,Toxic);Smoke(c,c.to,5,C(.25f,.35f,.05f,.75f));Rise(c,"Leaf",c.to,5,.6f,.5f,C(.45f,.35f,.15f));}},
   {"LP_WS_Confusion",c=>{Icon(c,"Swirl",c.to,c.r+1f,Violet,300f);Icon(c,"Mask",c.to+Up(1f),1.2f,Pink);Nova(c,"Note",c.to,6,1.6f,.5f,Pink);}},
   {"LP_WS_Fabricate",c=>{Icon(c,"Clock",c.to,1.6f,Teal,-360f,.5f);Nova(c,"Spark",c.to,12,2.5f,.5f,Teal);Dust(c,c.to,1.2f,C(.8f,.8f,.8f,.5f));}},
   {"LP_WS_FaithfulHound",c=>{Icon(c,"Glow",c.from,2f,A(Silver,.5f));Rise(c,"Spark",c.from,8,.8f,.4f,Silver);Orbit(c,"Rune",c.from,1f,4,.5f,Silver,90f);}},
   {"LP_WS_PhantasmalKiller",c=>{Icon(c,"Mask",c.to+Up(1.3f),1.8f,Violet,0,.9f);Icon(c,"Skull",c.to+Up(1.3f),1.1f,A(Black,.9f),0,.9f);Smoke(c,c.to,4,C(.1f,.05f,.15f,.8f));}},
   {"LP_WS_Polymorph",c=>{Icon(c,"Swirl",c.to,2f,Teal,-360f);Rise(c,"Leaf",c.to,5,.5f,.5f,Teal);Later(20,()=>{Glow(c,c.to,2f,Teal);Dust(c,c.to,1.2f,C(1f,1f,1f,.6f));});}},
   {"LP_WS_Stoneskin",c=>{Icon(c,"Hex",c.to,1.6f,Brown,0,.8f);Nova(c,"Shard",c.to,8,1.5f,.5f,C(.7f,.65f,.55f));Dust(c,c.to,1.2f,C(.6f,.55f,.5f,.7f));}},
   {"LP_WS_WallOfFire",c=>{for(int i=0;i<9;i++){var p=Vector3.Lerp(c.to+(c.to-c.from).normalized*-4f,c.to+(c.to-c.from).normalized*4f,i/8f);p=c.to+Quaternion.AngleAxis(90,Vector3.up)*(p-c.to);int t=i*2;Later(t,()=>{Pillar(c,p,2.4f,.9f,Ember,.4f);Fire(c,p,1.2f);});}}},
   // ---- 5 环 ----
   {"LP_CS_Awaken",c=>{Icon(c,"Eye",c.to+Up(1f),1.4f,Green);Rise(c,"Leaf",c.to,8,.7f,.6f,Green);Ring(c,c.to,1f,Gold);}},
   {"LP_CS_ContactOtherPlane",c=>{Icon(c,"Portal",c.from+Up(1.5f),2.2f,Abyss,200f,.9f);Line(c,"Beam",c.from+Up(1.5f),c.from,.6f,Violet);Icon(c,"Eye",c.from+Up(1.5f),1f,Teal,0,.9f);}},
   {"LP_CS_ControlEnvironment",c=>{Icon(c,"Swirl",c.from,4f,Wind(c),-120f,1f);for(int i=0;i<3;i++){int t=i*12;Later(t,()=>Sky(c,Disc(c.from,4f),C(.7f,.8f,1f)));}}},
   {"LP_CS_Dream",c=>{Icon(c,"Mask",c.to+Up(1f),1.4f,C(.6f,.6f,1f));Rise(c,"Feather",c.to,4,.6f,.6f,C(.7f,.7f,1f));Smoke(c,c.to,3,C(.4f,.4f,.7f,.4f));}},
   {"LP_CS_FlameStrike",c=>{Pillar(c,c.to,7f,c.r*1.2f+1f,Ember,.25f);Pillar(c,c.to,7f,.6f,Gold,.3f);Later(8,()=>{Fire(c,c.to,2.4f);Ring(c,c.to,c.r*.5f,Ember);Nova(c,"Sun",c.to,1,0f,2f,Gold);});}},
   {"LP_CS_GreaterRestoration",c=>{Pillar(c,c.to,4f,1f,Pale,.5f);Rise(c,"Feather",c.to,5,.5f,.6f,Pale);Chains(c,c.to,3,1.2f,A(Silver,.6f));}},
   {"LP_CS_Hallow",c=>{Icon(c,"Circle",c.to,c.r*.9f+1f,Gold,30f,1.2f);Pillar(c,c.to,6f,1.2f,Gold,.8f);Rise(c,"Rune",c.to,8,1.5f,.6f,Gold);}},
   {"LP_CS_InsectPlague",c=>{for(int i=0;i<24;i++){int t=i;Later(t,()=>{var p=Disc(c.to,c.r);I(c,"Insect",p,.35f,C(.3f,.25f,.1f),Rand.Range(0f,360f),0,.4f,Rand.Range(0f,360f),Rand.Range(1f,3f));});}Smoke(c,c.to,4,C(.25f,.2f,.1f,.6f));}},
   {"LP_CS_RaiseDead",c=>{Icon(c,"Sun",c.to,1.8f,Gold);Pillar(c,c.to,5f,1f,Gold,.7f);Rise(c,"Feather",c.to,6,.5f,.6f,Gold);}},
   {"LP_CS_Reincarnate",c=>{Icon(c,"Swirl",c.to,2f,Green,-180f);Rise(c,"Leaf",c.to,10,.6f,.6f,Green);Later(25,()=>Glow(c,c.to,3f,Green));}},
   {"LP_CS_TreeStride",c=>{Rise(c,"Leaf",c.from,8,.6f,.6f,Green);Icon(c,"Portal",c.from,1.6f,Green,180f);Later(10,()=>{Rise(c,"Leaf",c.to,8,.6f,.6f,Green);Glow(c,c.to,2f,Green);});}},
   {"LP_CS_AnimateObjects",c=>{Nova(c,"Rune",c.to,6,2f,.5f,Teal);Orbit(c,"Shard",c.to,1.2f,5,.6f,Teal,120f);Glow(c,c.to,2f,Teal);}},
   {"LP_CS_BigbysHand",c=>{Icon(c,"Hand",c.to+Up(1.4f),2.6f,Violet,0,.35f);Later(15,()=>{Wave(c,c.to,2f);Dust(c,c.to,1.6f,C(.8f,.8f,.9f,.6f));Glow(c,c.to,2f,Violet);});}},
   {"LP_CS_ConjureVolley",c=>Rain(c,"Arrow",c.to,c.r>0?c.r:4f,26,Gold,40,1.8f)},
   {"LP_CS_Creation",c=>{Icon(c,"Hex",c.to,1.2f,Teal,90f);Rise(c,"Shard",c.to,5,.4f,.5f,Teal);Glow(c,c.to,1.6f,Teal);}},
   {"LP_CS_SmiteBanish",c=>{Icon(c,"Portal",c.from,1.6f,Silver,-200f,.4f);I(c,"Slash",c.from,2.4f,Silver,Rand.Range(0f,360f));Wave(c,c.from,1.2f);}},
   {"LP_CS_SwiftQuiver",c=>{Orbit(c,"Arrow",c.from,1f,6,.8f,Gold,0,.6f);Rise(c,"Spark",c.from,6,.6f,.4f,Gold);}},
   {"LP_WS_ConeOfCold",c=>{var dir=(c.to-c.from);dir.y=0;float ang=SpellVfx.AngleTo(c.from,c.to);for(int i=0;i<16;i++){float a=ang+Rand.Range(-30f,30f);I(c,"Shard",c.from,Rand.Range(.5f,.9f),Ice,a,0,.3f,a,Rand.Range(10f,16f));}SpellVfx.Burst(c.map,D("Flake"),c.from+dir.normalized*2f,16,3f,7f,.7f*c.k,Ice);Later(12,()=>Frost(c,c.to,10));}},
   {"LP_WS_ConjureElemental",c=>{Icon(c,"Portal",c.to,2f,Ember,240f,.8f);Later(20,()=>{Fire(c,c.to,1.6f);Frost(c,c.to,6);Dust(c,c.to,1.4f,C(.6f,.5f,.4f,.6f));Glow(c,c.to,2.6f,Gold);});}},
   {"LP_WS_DominatePerson",c=>{Line(c,"Chain",c.from,c.to,.8f,Pink);Icon(c,"Eye",c.to+Up(1.2f),1.4f,Pink);Icon(c,"Swirl",c.to,1.4f,Pink,-300f);}},
   {"LP_WS_HoldMonster",c=>{Icon(c,"Hex",c.to,1.8f,Pink,0,.8f);Chains(c,c.to,4,1.3f,Pink);Glow(c,c.to,2f,Pink);}},
   {"LP_WS_LegendLore",c=>{Icon(c,"Rune",c.from+Up(1.4f),1.4f,Gold,90f,1f);Rise(c,"Rune",c.from,8,.8f,.5f,Gold);Icon(c,"Eye",c.from+Up(1.4f),.8f,Gold,0,1f);}},
   {"LP_WS_ModifyMemory",c=>{Icon(c,"Mask",c.to+Up(1f),1.3f,Pink);Icon(c,"Clock",c.to+Up(1f),1.6f,A(Pink,.6f),-300f);Smoke(c,c.to,2,C(.6f,.4f,.6f,.4f));}},
   {"LP_WS_Scrying",c=>{Icon(c,"Eye",c.from+Up(1.5f),2.4f,Ice,0,1.4f);Ring(c,c.from,1.2f,Ice);Icon(c,"Dome",c.from+Up(1.5f),1.2f,A(Ice,.5f),0,1.4f);}},
   {"LP_WS_Telekinesis",c=>{Line(c,"Beam",c.from,c.to,.5f,Violet);Icon(c,"Hand",c.to+Up(.8f),1.6f,A(Violet,.7f),0,.6f);Nova(c,"Spark",c.to,8,2f,.5f,Violet);}},
   {"LP_WS_TeleportationCircle",c=>{Icon(c,"Circle",c.from,3f,Violet,120f,1.2f);Icon(c,"Portal",c.from,2f,Violet,-240f,1f);Rise(c,"Rune",c.from,8,1f,.5f,Violet);}},
   // ---- 6 环 ----
   {"LP_CS_BladeBarrier",c=>{for(int i=0;i<10;i++){var p=c.to+Quaternion.AngleAxis(SpellVfx.AngleTo(c.from,c.to)+90f,Vector3.up)*Vector3.forward*(i-4.5f);int t=i*2;Later(t,()=>I(c,"Blade",p,.9f,Gold,Rand.Range(0f,360f),720f,.6f));}}},
   {"LP_CS_BloodlineBreath",c=>{float ang=SpellVfx.AngleTo(c.from,c.to);for(int i=0;i<18;i++){float a=ang+Rand.Range(-28f,28f);I(c,"Glow",c.from,Rand.Range(1f,1.8f),c.col,0,0,.25f,a,Rand.Range(8f,14f));}Later(10,()=>{Glow(c,c.to,3f,c.col);Sparks(c,c.to,12,4f,c.col);});}},
   {"LP_CS_Eyebite",c=>{Icon(c,"Eye",c.from+Up(1.4f),1.8f,Red,0,1f);Line(c,"Beam",c.from+Up(1.4f),c.to,.4f,Red);}},
   {"LP_CS_Heal",c=>{Pillar(c,c.to,5f,1.4f,Green,.6f);Rise(c,"Feather",c.to,6,.6f,.6f,Green);Rise(c,"Spark",c.to,10,.7f,.5f,Green);}},
   {"LP_CS_HeroesFeast",c=>{Icon(c,"Circle",c.from,3.2f,Gold,40f,1f);Rise(c,"Sun",c.from,3,1.5f,.8f,Gold);Rise(c,"Spark",c.from,14,2f,.5f,Gold);}},
   {"LP_CS_SummonFiend",c=>{Icon(c,"Portal",c.to,2.6f,C(1f,.25f,.1f),-260f,1f);Later(25,()=>{Fire(c,c.to,2f);Smoke(c,c.to,6,C(.15f,.05f,.04f,.85f));Icon(c,"Skull",c.to+Up(1f),1.4f,Ember);});}},
   {"LP_CS_WindWalk",c=>{Icon(c,"Swirl",c.from,3f,Pale,-300f);for(int i=0;i<4;i++)FleckMaker.ThrowAirPuffUp(Disc(c.from,1.5f),c.map);Rise(c,"Feather",c.from,8,2f,.6f,Pale);}},
   {"LP_CS_MassSuggestion",c=>{Nova(c,"Note",c.from,8,2.4f,.6f,Pink);Icon(c,"Swirl",c.from,4f,A(Pink,.5f),-120f);Wave(c,c.from,3f);}},
   {"LP_CS_OttoDance",c=>{Orbit(c,"Note",c.to,1f,6,.6f,Pink,180f);Icon(c,"Swirl",c.to,1.4f,Pink,600f);Sparks(c,c.to,8,2f,Pink);}},
   {"LP_WS_ChainLightning",c=>{Line(c,"Bolt",c.from,c.to,1.4f,C(.6f,.75f,1f));var prev=c.to;for(int i=1;i<=3;i++){var nxt=Disc(c.to,4f);var a=prev;int t=i*6;Later(t,()=>{Line(c,"Bolt",a,nxt,1.1f,C(.6f,.75f,1f));FleckMaker.ThrowLightningGlow(nxt,c.map,1.2f);});prev=nxt;}Glow(c,c.to,2.4f,C(.6f,.75f,1f));}},
   {"LP_WS_CircleOfDeath",c=>{Icon(c,"Circle",c.to,c.r+1.5f,Green,-60f,.8f);Ring(c,c.to,c.r*.5f,Green);Nova(c,"Skull",c.to,8,c.r*1.5f+1f,.6f,Green,.4f);Smoke(c,c.to,6,C(.1f,.25f,.12f,.8f));}},
   {"LP_WS_Contingency",c=>{Icon(c,"Clock",c.from+Up(1.2f),1.4f,Violet,90f,1f);Icon(c,"Hex",c.from,1.4f,Violet);Rise(c,"Rune",c.from,4,.5f,.5f,Violet);}},
   {"LP_WS_CreateUndead",c=>{Icon(c,"Circle",c.to,2f,Green,-90f);Rise(c,"Skull",c.to,3,.5f,.6f,Green);Smoke(c,c.to,5,C(.1f,.25f,.12f,.8f));}},
   {"LP_WS_FleshToStone",c=>{Line(c,"Beam",c.from,c.to,.5f,C(.7f,.65f,.55f));Icon(c,"Crack",c.to,1.6f,C(.7f,.65f,.55f));Dust(c,c.to,1.4f,C(.6f,.55f,.5f,.8f));Nova(c,"Shard",c.to,6,1.5f,.5f,C(.7f,.65f,.55f));}},
   {"LP_WS_MoveEarth",c=>{Icon(c,"Crack",c.to,3f,Brown,0,1f);for(int i=0;i<5;i++)Dust(c,Disc(c.to,2.5f),2f,C(.55f,.45f,.35f,.8f));Nova(c,"Shard",c.to,10,3f,.7f,Brown);}},
   {"LP_WS_Sunbeam",c=>{Line(c,"Beam",c.from,c.to,1.6f,Gold);Line(c,"Beam",c.from,c.to,.6f,Color.white);Icon(c,"Sun",c.from,1.6f,Gold);Icon(c,"Sun",c.to,2.2f,Gold);}},
   {"LP_WS_TrueSeeing",c=>{Icon(c,"Eye",c.to+Up(1.2f),1.6f,Gold,0,1f);Wave(c,c.to,2f);Rise(c,"Spark",c.to,8,.6f,.4f,Gold);}},
   {"LP_WS_WallOfForce",c=>{for(int i=0;i<7;i++){var p=c.to+Quaternion.AngleAxis(SpellVfx.AngleTo(c.from,c.to)+90f,Vector3.up)*Vector3.forward*(i-3f);int t=i*2;Later(t,()=>{I(c,"Hex",p,1f,A(Silver,.8f),0,0,1f);Pillar(c,p,2f,.8f,A(Silver,.5f),.8f);});}}},
   // ---- 7 环 ----
   {"LP_CS_ConjureCelestial",c=>{Pillar(c,c.to,8f,1.6f,Gold,.8f);Later(15,()=>{Rise(c,"Feather",c.to,10,1f,.8f,Gold);Icon(c,"Sun",c.to+Up(1f),2.4f,Gold);});}},
   {"LP_CS_DivineWord",c=>{Nova(c,"Rune",c.from,12,6f,.8f,Gold);Wave(c,c.from,c.r>0?c.r:6f);Icon(c,"Sun",c.from+Up(.5f),3f,Gold);}},
   {"LP_CS_PsychicBlast",c=>{Icon(c,"Eye",c.to+Up(1f),1.4f,C(.95f,.45f,1f));for(int i=0;i<3;i++){int t=i*6;Later(t,()=>{Ring(c,c.to,(c.r>0?c.r:4f)*.35f,C(.95f,.45f,1f));Wave(c,c.to,(c.r>0?c.r:4f)*.8f);});}}},
   {"LP_CS_Resurrection",c=>{Pillar(c,c.to,9f,1.8f,Gold,1f);Icon(c,"Circle",c.to,2.4f,Gold,40f,1f);Later(20,()=>{Rise(c,"Feather",c.to,10,.8f,.8f,Gold);Glow(c,c.to,4f,Color.white);});}},
   {"LP_CS_Whirlwind",c=>{for(int i=0;i<6;i++){int t=i*8;Later(t,()=>{Icon(c,"Swirl",c.to+Up(i*.25f),2.4f+i*.3f,A(Pale,.7f),-500f,.3f);FleckMaker.ThrowAirPuffUp(Disc(c.to,1f),c.map);Dust(c,c.to,1.4f,C(.7f,.7f,.6f,.5f));});}}},
   {"LP_CS_ProjectImage",c=>{Icon(c,"Mask",c.to+Up(1f),1.6f,A(Violet,.7f),0,1f);Line(c,"Beam",c.from,c.to,.3f,A(Violet,.5f));Rise(c,"Spark",c.to,8,.6f,.4f,Violet);}},
   {"LP_WS_DelayedBlastFireball",c=>{Line(c,"Beam",c.from,c.to,.4f,Ember);Icon(c,"Sun",c.to,1.6f,Ember,300f,1.2f);Fire(c,c.to,1.2f);}},
   {"LP_WS_DimensionDoor",c=>{Icon(c,"Portal",c.from,2.2f,Violet,-300f,.6f);Later(8,()=>{Icon(c,"Portal",c.to,2.2f,Violet,300f,.6f);Nova(c,"Spark",c.to,12,3f,.5f,Violet);});}},
   {"LP_WS_Etherealness",c=>{Icon(c,"Dome",c.from,1.6f,A(Violet,.5f),0,1f);Rise(c,"Spark",c.from,14,.8f,.4f,A(Silver,.6f));Smoke(c,c.from,3,C(.5f,.4f,.7f,.35f));}},
   {"LP_WS_FingerOfDeath",c=>{Line(c,"Beam",c.from,c.to,.7f,Green);Line(c,"Bolt",c.from,c.to,.6f,C(.2f,.6f,.3f));Icon(c,"Skull",c.to+Up(1.2f),1.8f,Green,0,.9f);Smoke(c,c.to,5,C(.1f,.25f,.12f,.85f));}},
   {"LP_WS_MagnificentMansion",c=>{Icon(c,"Portal",c.from,2.4f,Gold,120f,1f);Icon(c,"Hex",c.from,2.4f,Gold);Rise(c,"Rune",c.from,6,1f,.6f,Gold);}},
   {"LP_WS_PlaneShift",c=>{Icon(c,"Portal",c.to,3f,Teal,-360f,1f);Icon(c,"Circle",c.to,3f,Teal,90f,1f);Later(20,()=>Nova(c,"Spark",c.to,16,4f,.5f,Teal));}},
   {"LP_WS_ReverseGravity",c=>{Icon(c,"Circle",c.to,c.r+1f,Teal,-90f,1.4f);for(int i=0;i<16;i++){int t=i*2;Later(t,()=>{var p=Disc(c.to,c.r);I(c,"Shard",p,.5f,Teal,0,0,.4f,0,3f);Dust(c,p,.8f,C(.7f,.7f,.7f,.5f));});}}},
   // ---- 8 环 ----
   {"LP_CS_AnimalShapes",c=>{Icon(c,"Swirl",c.from,4f,Green,-300f);Nova(c,"Leaf",c.from,12,3f,.6f,Green);Wave(c,c.from,3f);}},
   {"LP_CS_DivineColumn",c=>{Pillar(c,c.to,10f,(c.r>0?c.r:2f)*1.2f,Gold,.35f);Pillar(c,c.to,10f,.8f,Color.white,.4f);Later(10,()=>{Icon(c,"Sun",c.to,3f,Gold);Wave(c,c.to,3f);Rise(c,"Rune",c.to,6,1f,.6f,Gold);});}},
   {"LP_CS_Earthquake",c=>{float rr=c.r>0?c.r:6f;Icon(c,"Crack",c.to,rr*.9f,Brown,0,1.5f);for(int i=0;i<10;i++){int t=i*4;Later(t,()=>{var p=Disc(c.to,rr);Dust(c,p,2f,C(.55f,.45f,.35f,.8f));Nova(c,"Shard",p,3,2f,.5f,Brown);});}Find.CameraDriver?.shaker?.DoShake(.4f*c.k);}},
   {"LP_CS_ElementalStorm",c=>{Icon(c,"Swirl",c.to,(c.r>0?c.r:5f)+1f,A(Gold,.6f),-200f,1.2f);Later(5,()=>Fire(c,Disc(c.to,2f),1.4f));Later(12,()=>Frost(c,Disc(c.to,2f),8));Later(20,()=>Sky(c,Disc(c.to,2f),C(.6f,.75f,1f)));}},
   {"LP_CS_HolyAura",c=>{Icon(c,"Dome",c.from,3f,A(Gold,.5f),0,1f);Orbit(c,"Feather",c.from,1.6f,6,.7f,Gold,60f,1f);Rise(c,"Rune",c.from,6,1.4f,.5f,Gold);}},
   {"LP_CS_PowerWordStun",c=>{Icon(c,"Rune",c.to+Up(1.2f),1.8f,Pink,0,.8f);Wave(c,c.to,1.6f);Nova(c,"Spark",c.to,12,4f,.6f,Pink);}},
   {"LP_WS_Antipathy",c=>{Icon(c,"Circle",c.from,(c.r>0?c.r:6f)*.6f,Pink,30f,1.4f);Icon(c,"Mask",c.from+Up(1.2f),1.4f,Pink);Wave(c,c.from,(c.r>0?c.r:6f)*.6f);}},
   {"LP_WS_Clone",c=>{Icon(c,"Mask",c.to+Up(1f),1.4f,Teal);Icon(c,"Shard",c.to+Up(1f),1.2f,A(Teal,.6f));Rise(c,"Spark",c.to,10,.6f,.5f,Teal);Icon(c,"Hex",c.to,1.6f,Teal);}},
   {"LP_WS_DominateMonster",c=>{for(int i=0;i<3;i++){var a=Disc(c.to,1.5f);Line(c,"Chain",a,c.to,.7f,Pink);}Icon(c,"Eye",c.to+Up(1.4f),2f,Pink,0,.9f);Icon(c,"Swirl",c.to,2f,Pink,-300f);}},
   {"LP_WS_Feeblemind",c=>{Icon(c,"Mask",c.to+Up(1.2f),1.6f,Pink,0,.6f);Later(10,()=>{Nova(c,"Shard",c.to+Up(1.2f),8,2f,.4f,Pink);Smoke(c,c.to,3,C(.5f,.3f,.5f,.5f));});}},
   {"LP_WS_ForceCage",c=>{Icon(c,"Hex",c.to,2.4f,Silver,0,1.4f);for(int i=0;i<6;i++){var p=c.to+Quaternion.AngleAxis(60f*i,Vector3.up)*Vector3.forward*1.1f;Pillar(c,p,2.4f,.4f,A(Silver,.8f),1.2f);}}},
   {"LP_WS_Maze",c=>{Icon(c,"Hex",c.to,2f,Violet,180f,.6f);Icon(c,"Portal",c.to,1.8f,Violet,-360f,.6f);Later(15,()=>Nova(c,"Rune",c.to,6,2f,.5f,Violet));}},
   {"LP_WS_MindBlank",c=>{Icon(c,"Dome",c.to,1.8f,A(Silver,.6f),0,1f);Icon(c,"Eye",c.to+Up(1f),1.2f,A(Silver,.6f));Wave(c,c.to,1.2f);}},
   {"LP_WS_Sunburst",c=>{float rr=c.r>0?c.r:6f;Icon(c,"Sun",c.to,rr*.9f,Gold,90f,.5f);Nova(c,"Sun",c.to,8,rr*2f,.8f,Gold);Wave(c,c.to,rr);Glow(c,c.to,rr*1.6f,A(Color.white,.8f));}},
   // ---- 9 环 ----
   {"LP_CS_BloodlineAwakening",c=>{Pillar(c,c.from,8f,1.6f,c.col,.8f);Icon(c,"Swirl",c.from,3f,c.col,-400f);Nova(c,"Glow",c.from,10,4f,.8f,c.col);}},
   {"LP_CS_MassHeal9",c=>{float rr=c.r>0?c.r:6f;Icon(c,"Circle",c.from,rr*.6f,Green,40f,1f);for(int i=0;i<8;i++){var p=Disc(c.from,rr);int t=i*3;Later(t,()=>Pillar(c,p,4f,.8f,Green,.4f));}Rise(c,"Feather",c.from,10,2f,.7f,Green);}},
   {"LP_CS_StormOfVengeance",c=>{float rr=c.r>0?c.r:8f;Icon(c,"Swirl",c.to,rr+2f,C(.25f,.25f,.35f,.8f),-120f,2f);for(int i=0;i<8;i++){int t=i*6;Later(t,()=>Sky(c,Disc(c.to,rr),C(.6f,.75f,1f)));}}},
   {"LP_CS_TreeOfLife",c=>{Pillar(c,c.to,6f,1.4f,Green,1f);Icon(c,"Circle",c.to,2.4f,Green,30f,1f);Rise(c,"Leaf",c.to,14,1.2f,.7f,Green);}},
   {"LP_CS_TrueResurrection",c=>{Pillar(c,c.to,12f,2.4f,Gold,1.2f);Pillar(c,c.to,12f,.8f,Color.white,1.2f);Icon(c,"Sun",c.to+Up(1f),3f,Gold,60f,1f);Later(30,()=>{Rise(c,"Feather",c.to,14,1.2f,.8f,Gold);Wave(c,c.to,3f);});}},
   {"LP_CS_PowerWordHeal",c=>{Icon(c,"Rune",c.to+Up(1.2f),1.8f,Green,0,.8f);Pillar(c,c.to,5f,1.2f,Green,.6f);Nova(c,"Feather",c.to,8,2f,.6f,Green);}},
   {"LP_WS_Foresight",c=>{Icon(c,"Eye",c.to+Up(1.4f),2.2f,Gold,0,1.2f);Icon(c,"Clock",c.to,1.8f,A(Gold,.6f),120f,1.2f);Rise(c,"Spark",c.to,10,.6f,.4f,Gold);}},
   {"LP_WS_Gate",c=>{Icon(c,"Circle",c.to,3.6f,Violet,60f,2f);Icon(c,"Portal",c.to,3.2f,Abyss,-200f,2f);for(int i=0;i<4;i++){int t=i*10;Later(t,()=>Nova(c,"Spark",c.to,10,4f,.6f,Violet));}}},
   {"LP_WS_MassPolymorph",c=>{float rr=c.r>0?c.r:4f;Icon(c,"Swirl",c.to,rr+1f,Teal,-360f,.8f);for(int i=0;i<8;i++){var p=Disc(c.to,rr);int t=i*3;Later(t,()=>{Dust(c,p,1.2f,C(1f,1f,1f,.6f));Rise(c,"Leaf",p,2,.3f,.5f,Teal);});}}},
   {"LP_WS_PowerWordKill",c=>{Icon(c,"Rune",c.to+Up(1.4f),2f,Red,0,.6f);Later(12,()=>{Icon(c,"Skull",c.to+Up(1.2f),2.2f,C(.1f,.05f,.08f),0,1f);Glow(c,c.to,2.4f,Red);Smoke(c,c.to,6,C(.12f,.02f,.04f,.9f));});}},
   {"LP_WS_TimeStop",c=>{Icon(c,"Clock",c.from,8f,A(Silver,.55f),0,2.5f);Icon(c,"Clock",c.from,4f,A(Gold,.6f),-30f,2.5f);Wave(c,c.from,12f);Glow(c,c.from,8f,A(Silver,.35f));}},
   {"LP_WS_Wish",c=>{Icon(c,"Sun",c.from+Up(1.5f),3f,Gold,120f,1.2f);Nova(c,"Spark",c.from,24,5f,.7f,Gold);Icon(c,"Circle",c.from,3f,Gold,90f,1.2f);Rise(c,"Rune",c.from,10,1.5f,.6f,Gold);}},
  };
  static Color Wind(Ctx c)=>A(C(.8f,1f,.95f),.7f);
  // 秘传与湖印里同名的高环法术沿用法师版的专属特效。
  static readonly Dictionary<string,string> Alias=new Dictionary<string,string>{
   {"LP_AC_ReverseGravity","LP_WS_ReverseGravity"},{"LP_AC_Maze","LP_WS_Maze"},{"LP_AC_TimeStop","LP_WS_TimeStop"},{"LP_AC_PowerWordKill","LP_WS_PowerWordKill"},
   {"LP_AC_Wish","LP_WS_Wish"},{"LP_AC_Foresight","LP_WS_Foresight"},{"LP_AC_PowerWordStun","LP_CS_PowerWordStun"},{"LP_AC_IrresistibleDance","LP_CS_OttoDance"}};
  static Action<Ctx> SigOf(AbilityDef d){if(d==null)return null;if(Sig.TryGetValue(d.defName,out var a))return a;if(Alias.TryGetValue(d.defName,out var n) && Sig.TryGetValue(n,out a))return a;return null;}
  public static bool Has(AbilityDef d)=>SigOf(d)!=null;
  public static int SignatureCount=>Sig.Count;
  public static IEnumerable<string> SignatureNames=>Sig.Keys;
  public static void Play(Pawn caster,Vector3 from,Map map,AbilityDef def,VfxProfile p,LocalTargetInfo target,float radius){
   var act=SigOf(def);if(act==null || map==null)return;
   var to=target.IsValid?(target.Thing?.DrawPos??target.CenterVector3):from;
   var c=new Ctx{map=map,caster=caster,from=from,to=to,tp=target.Pawn,r=radius,col=SpellVfx.ColorOf(p.theme),k=SpellVfx.K,self=!target.IsValid || target.Thing==caster,theme=p.theme};
   act(c);LastSignature=def;
  }
  public static AbilityDef LastSignature;

  // ===== 2）持续区域 =====
  public static int ZoneTicks;
  public static void ZoneTick(SpellZone z){
   if(!SpellVfx.On || z?.Map==null || z.kind==null)return;
   try{
    var c=new Ctx{map=z.Map,from=z.DrawPos,to=z.DrawPos,r=z.radius,k=SpellVfx.K};
    var pos=z.DrawPos;float r=Math.Max(.6f,z.radius);
    Vector3 Cell(){if(z.line!=null && z.line.Count>0)return z.line.RandomElement().ToVector3Shifted();return Disc(pos,r);}
    ZoneTicks++;
    switch(z.kind){
     case "Tentacles":Tentacles(c,pos,r,2,Black,4,.7f);if(Rand.Chance(.3f))Smoke(c,Disc(pos,r),1,C(.06f,.02f,.1f,.7f));break;
     case "FireWall":for(int i=0;i<2;i++){var p=Cell();Pillar(c,p,1.8f,.7f,Ember,.25f);Fire(c,p,.9f);}break;
     case "Delayed":Glow(c,pos,1.4f+Rand.Value*.6f,Ember);if(Rand.Chance(.5f))Icon(c,"Sun",pos,1f,Ember,300f,.2f);break;
     case "Antipathy":if(z.IsHashIntervalTick(120))Icon(c,"Circle",pos,r*.5f,A(Pink,.35f),20f,1.2f);break;
     case "Circle":if(z.IsHashIntervalTick(120))Icon(c,"Circle",pos,r*.6f,A(C(.6f,.75f,1f),.5f),30f,1.2f);break;
     case "Grease":if(Rand.Chance(.3f))Glow(c,Cell(),.8f,A(C(.35f,.3f,.2f),.5f));break;
     case "Web":if(Rand.Chance(.3f))Icon(c,"Hex",Cell(),.6f,A(Silver,.4f));break;
     case "Stink":Smoke(c,Cell(),1,C(.4f,.55f,.15f,.45f));break;
     case "CS_Silence":if(z.IsHashIntervalTick(60))Icon(c,"Dome",pos,r*.55f,A(C(.75f,.7f,1f),.25f),0,1f);break;
     case "CS_Guardian":Pillar(c,pos,4f,1.1f,A(Gold,.6f),.3f);if(Rand.Chance(.4f))Rise(c,"Spark",pos,2,.6f,.4f,Gold);break;
     case "CS_Hallow":Rise(c,"Rune",Cell(),1,.3f,.5f,Gold);if(z.IsHashIntervalTick(120))Icon(c,"Circle",pos,r*.55f,A(Gold,.4f),20f,1.2f);break;
     case "CS_Blades":for(int i=0;i<2;i++)I(c,"Blade",Cell(),.8f,Gold,Rand.Range(0f,360f),900f,.3f);break;
     case "CS_Moon":Pillar(c,pos,6f,r*1.1f+.5f,A(Silver,.7f),.3f);if(Rand.Chance(.5f))Rise(c,"Spark",Cell(),2,.3f,.4f,Silver);break;
     case "CS_Spikes":for(int i=0;i<2;i++){var p=Cell();I(c,"Shard",p+Up(.25f),.45f,C(.45f,.6f,.25f),Rand.Range(-15f,15f),0,.4f);}break;
     case "CS_Fire":Glow(c,pos,2f,Ember);Fire(c,pos,1.1f);break;
     case "CS_Wind":for(int i=0;i<2;i++){var p=Cell();FleckMaker.ThrowAirPuffUp(p,c.map);}if(Rand.Chance(.4f))Swirl(c.map,Cell(),.8f*c.k,A(Pale,.4f));break;
     case "CS_Insects":for(int i=0;i<4;i++)I(c,"Insect",Cell(),.3f,C(.3f,.25f,.1f),Rand.Range(0f,360f),0,.35f,Rand.Range(0f,360f),Rand.Range(1f,3f));break;
     case "CS_Storm":if(Rand.Chance(.35f))Sky(c,Cell(),C(.6f,.75f,1f));else Smoke(c,Cell(),1,C(.25f,.25f,.3f,.4f));break;
     case "CS_Heal":Rise(c,"Leaf",Cell(),1,.3f,.5f,Green);if(Rand.Chance(.5f))Rise(c,"Spark",pos,2,r*.5f,.4f,Green);break;
     case "CS_Elements":switch(Rand.Range(0,3)){case 0:Fire(c,Cell(),1f);break;case 1:Frost(c,Cell(),3);break;default:if(Rand.Chance(.4f))Sky(c,Cell(),C(.6f,.75f,1f));break;}break;
     case "CS_Hunger":Smoke(c,Cell(),2,C(.03f,.01f,.06f,.85f));if(Rand.Chance(.25f))Tentacles(c,pos,r,1,Abyss,1,.5f);if(z.IsHashIntervalTick(120))Icon(c,"Eye",pos+Up(.5f),1f,A(C(.6f,1f,.8f),.5f),0,1f);break;
     default:if(z.kind.StartsWith("CS_") && Rand.Chance(.3f))Rise(c,"Spark",Cell(),1,.3f,.4f,C(.6f,.75f,1f));break;
    }
   }catch(Exception e){Log.ErrorOnce("[LP] 区域特效出错："+e,0x5f7a1d1);}
  }

  // ===== 2）绕身光环：状态还在就一直有 =====
  class Aura { public Pawn pawn;public HediffDef hed;public string kind;public int start,until; }
  static readonly List<Aura> auras=new List<Aura>();
  static readonly Dictionary<string,string> AuraKinds=new Dictionary<string,string>{
   {"LP_CS_SpiritualWeapon","weapon"},{"LP_CS_SpiritGuardians","guardians"},{"LP_CS_Eyebite","eyebite"},{"LP_CS_CrusadersMantle","mantle"},
   {"LP_CS_HolyAura","holy"},{"LP_WS_Etherealness","ether"}};
  public static int AuraCount=>auras.Count;
  public static void OnCast(Pawn caster,AbilityDef def,VfxProfile p){
   if(caster==null || def==null || !AuraKinds.TryGetValue(def.defName,out var kind))return;
   var ce=def.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
   var hed=ce?.hediff!=null?DefDatabase<HediffDef>.GetNamedSilentFail(ce.hediff):null;
   int dur=ce!=null && ce.duration>0?ce.duration:3600;
   auras.RemoveAll(a=>a.pawn==caster && a.kind==kind);
   int now=Find.TickManager.TicksGame;auras.Add(new Aura{pawn=caster,hed=hed,kind=kind,start=now,until=now+Math.Min(dur,60000)});
  }
  public static void TickAuras(){
   if(auras.Count==0)return;int now=Find.TickManager.TicksGame;
   for(int i=auras.Count-1;i>=0;i--){
    var a=auras[i];var p=a.pawn;
    if(now>a.until || p==null || p.Dead || p.Destroyed || (a.hed!=null && now-a.start>60 && p.health?.hediffSet?.HasHediff(a.hed)!=true)){auras.RemoveAt(i);continue;}
    if(!SpellVfx.On || !p.Spawned || (now+p.thingIDNumber)%20!=0)continue;
    try{
     var c=new Ctx{map=p.Map,from=p.DrawPos,to=p.DrawPos,k=SpellVfx.K};var pos=p.DrawPos;
     switch(a.kind){
      case "weapon":{var q=pos+Quaternion.AngleAxis(now*6f%360f,Vector3.up)*Vector3.forward*1.1f*c.k;I(c,"Rune",q+Up(.4f),.6f,Gold,0,0,.25f);Glow(c,q+Up(.4f),.7f,A(Gold,.6f));break;}
      case "guardians":Orbit(c,"Feather",pos,2.4f,3,.6f,A(Gold,.8f),120f,.3f);break;
      case "eyebite":I(c,"Eye",pos+Up(1.3f),.8f,A(Red,.8f),0,0,.3f);break;
      case "mantle":I(c,"Ring",pos,.7f,A(Gold,.5f));Rise(c,"Spark",pos,1,.6f,.4f,Gold);break;
      case "holy":Orbit(c,"Feather",pos,1.4f,2,.5f,A(Gold,.7f),60f,.3f);break;
      case "ether":Rise(c,"Spark",pos,2,.5f,.35f,A(Silver,.5f));break;
     }
    }catch(Exception e){Log.ErrorOnce("[LP] 光环特效出错："+e,0x5f7a1d2);}
   }
  }
  // 状态刚施放时可能还没挂上：前 60 刻不因为没有状态而结束（见 TickAuras）。
  public static void ResetRuntime(){auras.Clear();}
 }
 // 光环列表不存档：读档或新开局清空。
 public class SpellVfxAuras : GameComponent {
  public SpellVfxAuras(Game game){SpellVfx2.ResetRuntime();}
  public override void GameComponentTick(){SpellVfx2.TickAuras();}
 }
}
