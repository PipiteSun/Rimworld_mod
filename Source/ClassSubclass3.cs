using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.106.0 武僧子职业（Peter 的设计，等级 3 / 6 / 11 / 17，Defs/Subclasses3.xml）：暗影宗、散打宗。
 // 被动按能力位（Users），每 250 刻在 Tick250 更新；伤害挂钩（Subclass3Damage）第一步是空集合判断。
 public static class Subclass3 {
  public const string Shadow="LP_Sub_ShadowWay",OpenHand="LP_Sub_OpenHandWay",Evocation="LP_Sub_Evocation",Scribes="LP_Sub_Scribes",Dragon="LP_Sub_DragonWay",Land="LP_Sub_CircleLand",Spores="LP_Sub_CircleSpores",Stars="LP_Sub_CircleStars",Life="LP_Sub_LifeDomain",Light="LP_Sub_LightDomain",Forge="LP_Sub_ForgeDomain";
  public static readonly Color ShadowColor=new Color(.45f,.4f,.6f),OpenColor=new Color(.95f,.8f,.4f);
  public const float DarkGlow=.3f,StepRange=12f,TranquilChance=.5f,TechChance=.7f,PalmChance=.5f,PalmBoss=.25f,PalmDamage=55f;
  public const int OpportunistGap=120,TechStun=90,SealTicks=360;

  public const int FDarkSight=1,FOpportunist=2,FTechnique=4,FTranquil=8,FSavant=16,FSculpt=32,FPotent=64,FEmpowered=128,FQuill=256,FTome=512,FScrivener=1024,FOneWord=2048,FDisciple=4096,FWings=8192,FAscendant=16384,FLandStride=1<<15,FNatureWard=1<<16,FSanctuary=1<<17,FHalo=1<<18,FInfest=1<<19,FFungalBody=1<<20,FStarMap=1<<21,FOmen=1<<22,FFullStars=1<<23,FLandSpell=1<<24,FLife=1<<25,FDivineStrike=1<<26,FFlare=1<<27,FFlareAlly=1<<28,FForgeSoul=1<<29,FForgeSaint=1<<30;
  static readonly Dictionary<string,int> FlagOf=new Dictionary<string,int>{{"LP_SH_ShadowArts",FDarkSight},{"LP_SH_Opportunist",FOpportunist},{"LP_OH_Technique",FTechnique},{"LP_OH_Tranquility",FTranquil},
   {"LP_EV_Savant",FSavant},{"LP_EV_Sculpt",FSculpt},{"LP_EV_Potent",FPotent},{"LP_EV_Empowered",FEmpowered},{"LP_SC_Quill",FQuill},{"LP_SC_Tome",FTome},{"LP_SC_MasterScrivener",FScrivener},{"LP_SC_OneWord",FOneWord},
   {"LP_DM_Disciple",FDisciple},{"LP_DM_Wings",FWings},{"LP_DM_Ascendant",FAscendant},
   {"LP_LD_LandSpell",FLandSpell},{"LP_LD_LandStride",FLandStride},{"LP_LD_NatureWard",FNatureWard},{"LP_LD_Sanctuary",FSanctuary},
   {"LP_SP_Halo",FHalo},{"LP_SP_Infestation",FInfest},{"LP_SP_FungalBody",FFungalBody},{"LP_ST_StarBolt",FStarMap},{"LP_ST_CosmicOmen",FOmen},{"LP_ST_FullOfStars",FFullStars},
   {"LP_LF_Disciple",FLife},{"LP_LF_DivineStrike",FDivineStrike},{"LP_FG_DivineStrike",FDivineStrike},{"LP_LT_WardingFlare",FFlare},{"LP_LT_ImprovedFlare",FFlareAlly},{"LP_FG_Soul",FForgeSoul},{"LP_FG_Saint",FForgeSaint}};
  public static readonly Dictionary<int,int> Users=new Dictionary<int,int>();
  public static readonly HashSet<Pawn> Opportunists=new HashSet<Pawn>(),Wingers=new HashSet<Pawn>(); // Wingers：龙翼翱翔（乘风步期间走路无视地形，ClassWoodstride）
  public static int Flags(Pawn p){if(p==null || Users.Count==0)return 0;Users.TryGetValue(p.thingIDNumber,out int f);return f;}
  public static bool S(Pawn p,int f)=>(Flags(p) & f)!=0;
  static int Now=>Find.TickManager.TicksGame;
  static float Roll=>ClassMartial2.RollOverride??Rand.Value;
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.HostileTo(a);
  static bool Mine(Hediff_Adventurer h)=>ClassSubclass.Has(h,Shadow) || ClassSubclass.Has(h,OpenHand) || ClassSubclass.Has(h,Evocation) || ClassSubclass.Has(h,Scribes) || ClassSubclass.Has(h,Dragon) || ClassSubclass.Has(h,Land) || ClassSubclass.Has(h,Spores) || ClassSubclass.Has(h,Stars) || ClassSubclass.Has(h,Life) || ClassSubclass.Has(h,Light) || ClassSubclass.Has(h,Forge);
  static void Passive(Pawn p,string hediff,bool on){if(on){if(!Items.Has(p,hediff))Items.Give(p,hediff);}else if(Items.Has(p,hediff))Items.Remove(p,hediff);}
  public static bool DarkCell(Map map,IntVec3 c)=>map!=null && c.InBounds(map) && map.glowGrid.GroundGlowAt(c)<DarkGlow;
  public static bool InDark(Pawn p)=>p.Spawned && DarkCell(p.Map,p.Position);

  public static int Refresh(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return 0;int f=0;
   if(!p.Dead && Mine(h)){var list=p.abilities?.abilities;if(list!=null)for(int k=0;k<list.Count;k++)if(FlagOf.TryGetValue(list[k].def.defName,out int b))f|=b;}
   if(f==0)Users.Remove(p.thingIDNumber);else Users[p.thingIDNumber]=f;
   if((f & FOpportunist)!=0)Opportunists.Add(p);else Opportunists.Remove(p);
   return f;
  }
  // ClassSystem.Tick250 → 这里。
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;if(p==null)return;
   if(Users.Count==0 && !Mine(h))return;
   if(p.Dead){Users.Remove(p.thingIDNumber);Opportunists.Remove(p);return;}
   int f=Refresh(h);
   bool dark=(f & FDarkSight)!=0 && InDark(p);
   if(dark){if(!Items.Has(p,"LP_SH_DarkSight"))Items.Give(p,"LP_SH_DarkSight");Items.Remove(p,"LP_WL_Darkened");}else if(Items.Has(p,"LP_SH_DarkSight"))Items.Remove(p,"LP_SH_DarkSight");
   if(Items.Has(p,"LP_SH_Cloaked") && !InDark(p)){Items.Remove(p,"LP_SH_Cloaked");Items.Mote(p,"现形",ShadowColor);}
   if((f & FTranquil)!=0 && !p.Awake() && !Items.Has(p,"LP_OH_Tranquil"))Items.Give(p,"LP_OH_Tranquil"); // 宁神境：睡一觉醒来就有，出手才结束
   Passive(p,"LP_EV_SavantBuff",(f & FSavant)!=0);Passive(p,"LP_SC_QuillBuff",(f & FQuill)!=0);
   var d=ClassSubclass.Data(h);
   if(!p.Awake() && d!=null){d.ticks.Remove("ovN");d.ticks.Remove("oneWord");if((f & FScrivener)!=0)d.ticks["scrollSlept"]=1;} // 睡觉 = 长休：超限导能、一字同心刷新；大书士醒来时给卷轴
   if(p.Awake() && d!=null && d.ticks.Remove("scrollSlept") && (f & FScrivener)!=0)MasterScroll(p);
   if((f & FWings)!=0 && Items.Has(p,"LP_StepOfTheWind") || (f & FLandStride)!=0 || (f & FFullStars)!=0 && Items.Has(p,"LP_ST_Form"))Wingers.Add(p);else Wingers.Remove(p);
   if((f & (FLandStride|FNatureWard|FSanctuary|FHalo|FInfest|FFungalBody|FStarMap|FOmen|FFullStars|FLandSpell))!=0)DruidTick(p,h,f);
   Passive(p,"LP_LF_DiscipleBuff",(f & FLife)!=0);Passive(p,"LP_FG_CraftBuff",ClassSubclass.Has(h,Forge));
   if(Items.Has(p,"LP_LT_CoronaOn") && p.Spawned && !p.Downed)Corona(p);
   Passive(p,"LP_DM_PresenceBuff",(f & FDisciple)!=0);
   if((f & FAscendant)!=0 && p.Spawned)foreach(var x in Races124.PawnsNear(p.Map,p.Position,3f).ToList())if(Hostile(p,x))Items.Give(x,"LP_Spotted",300); // 盲视
   if(Items.Has(p,"LP_SC_Manifest") && p.Spawned)foreach(var x in Races124.PawnsNear(p.Map,p.Position,15f).ToList())if(Hostile(p,x))Items.Give(x,"LP_Spotted",300);
  }

  // ---------- 塑能学派 ----------
  static readonly HashSet<string> EvoEffects=new HashSet<string>{"MagicMissile","Fireball","LightningBolt","Disintegrate"};
  public static bool Evo(CompProperties_ClassEffect e)=>e!=null && EvoEffects.Contains(e.effect);
  // 伤害倍率（ClassEffects.Power）：强力戏法 魔法飞弹 ×1.5；强效塑能 + 智力调整值 × 10%；超限导能 ×2。
  public static float PowerFactor(Pawn p,CompProperties_ClassEffect e){
   if(Users.Count==0 || e==null)return 1f;int f=Flags(p);if(f==0)return 1f;float g=1f;
   if((f & FLife)!=0 && (e.effect=="Heal" || e.effect=="MassHeal"))g*=ClassSystem.Knows(p,"LP_LF_Supreme")?1.3f*1.5f:1.3f; // 生命门徒、至高治疗
   if(e.effect=="SacredFlame" && ClassSystem.Knows(p,"LP_LT_Potent"))g*=1f+.1f*Mathf.Max(0,Adventurer.Mod(Adventurer.Score(p,Adventurer.Wis))); // 强力施法
   if((f & FStarMap)!=0 && e.stat!=null && (e.stat.defName=="LP_VerdantPower" || e.stat.defName=="LP_PrimalPower") && NightOutdoors(p))g*=1.25f; // 星图：夜里户外
   if(!Evo(e))return g;
   if((f & FPotent)!=0 && e.effect=="MagicMissile")g*=1.5f;
   if((f & FEmpowered)!=0)g*=1f+.1f*Mathf.Max(0,Adventurer.Mod(Adventurer.Score(p,Adventurer.Int)));
   if(Items.Has(p,"LP_EV_Overchannel"))g*=2f;
   return g;
  }
  public static void AfterCast(Ability_Class a){
   var p=a.pawn;if(p==null || Users.Count==0)return;var e=a.Props;
   if(e?.effect=="S3Breath" || e?.effect=="S3Aspect")DragonPaid(p,e); // 施放前后次数没变：还有免费次数 = 这次没付气
   if(e?.effect=="S3StarBolt")StarPaid(p);
   if(e?.effect=="S3Corona" && ChannelLeft(p)>0)ChannelUse(p);
   if((e?.effect=="Heal" || e?.effect=="MassHeal") && ClassSystem.Knows(p,"LP_LF_BlessedHealer")){float h=ClassMelee.Heal(p,2f+e.cost);if(h>0f)Items.Mote(p,"受祝的治疗者",OpenColor);}
   if(Evo(e) && Items.Has(p,"LP_EV_Overchannel")){
    Items.Remove(p,"LP_EV_Overchannel");var d=ClassSubclass.Data(p);if(d==null)return;d.ticks.TryGetValue("ovN",out int n);d.ticks["ovN"]=n+1;
    if(n>0){float back=13f*(n+1);Busy=true;try{p.TakeDamage(new DamageInfo(Warlock.ForceDamage,back,1f,-1f,null));}finally{Busy=false;}Items.Mote(p,"超限反噬 "+back.ToString("0"),ShadowColor);}
   }
   if(Items.Has(p,"LP_SC_Manifest") && ClassSystem.ClassOf(a.def)==wizard.Def && e?.cost>0){var h=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_SC_Manifest"));if(h!=null){h.Severity-=1f;if(h.Severity<.5f)p.health.RemoveHediff(h);}}
  }
  static readonly DefRef<LP_ClassDef> wizard=new DefRef<LP_ClassDef>("LP_Class_Wizard");
  // 思维具现：剩余次数内法师法术不耗法力（ClassSystem.FreeCast）。
  public static bool Free(Pawn p,CompProperties_ClassEffect props)=>Users.Count>0 && props!=null && (props.stat!=null && props.stat.defName=="LP_ArcanePower" && Items.Has(p,"LP_SC_Manifest") || DragonFree(p,props) || props.effect=="S3StarBolt" && StarBoltLeft(p)>0 || props.effect=="S3Corona" && ChannelLeft(p)>0);

  // ---------- 书士会·觉醒魔典：火球、闪电束换伤害类型 ----------
  public static readonly string[] TomeModes={"原样","火焰","寒冰","闪电","奥术力"};
  public static int TomeMode(Pawn p){var d=ClassSubclass.Data(p);if(d==null || !S(p,FTome))return 0;d.ticks.TryGetValue("tome",out int m);return Mathf.Clamp(m,0,4);}
  public static DamageDef Retype(Pawn p,DamageDef d){
   if(Users.Count==0)return d;switch(TomeMode(p)){
    case 1:return DamageDefOf.Flame;case 2:return DefCache<DamageDef>.Get("LP_Frost")??DamageDefOf.Frostbite;case 3:return ClassCasters.Lightning??DamageDefOf.Burn;case 4:return Warlock.ForceDamage;}
   return d;
  }
  // 大书士：睡醒时背包里多一张卷轴（火球、真知、防护邪恶、传送里随机）。
  static readonly string[] Scrolls={"LP_ScrollFireball","LP_ScrollTrueSeeing","LP_ScrollProtectionEvil","LP_ScrollTeleport"};
  public static Thing MasterScroll(Pawn p){
   var def=DefCache<ThingDef>.Get(Scrolls.RandomElement());if(def==null || p.inventory==null)return null;var th=ThingMaker.MakeThing(def);
   if(!p.inventory.innerContainer.TryAdd(th)){if(p.Spawned)GenPlace.TryPlaceThing(th,p.Position,p.Map,ThingPlaceMode.Near);else{th.Destroy();return null;}}
   Items.Mote(p,"大书士："+def.label,ClassCasters.Arcane);return th;
  }
  public static void Manifest(Pawn p,int ticks){var h=Items.Give(p,"LP_SC_Manifest",ticks,3f);if(h!=null)h.Severity=3f;Items.Mote(p,"思维具现",ClassCasters.Arcane);}

  // ---------- 散打技巧：疾风连击每一拳的附加效果（ClassEffects 的 Flurry） ----------
  public static readonly string[] TechModes={"击倒","推开","封招"};
  public static int Mode(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;d.ticks.TryGetValue("ohMode",out int m);return Mathf.Clamp(m,0,2);}
  public static void FlurryHit(Pawn p,Pawn t){
   if(Users.Count==0 || !S(p,FTechnique) || t==null || t.Dead || !t.Spawned)return;
   int m=Mode(p);
   if(m==2){Items.Give(t,"LP_OH_Sealed",SealTicks);return;}
   if(Roll>=Subclass2.Chance(TechChance,p,t)){Items.Mote(t,"稳住了",OpenColor);return;}
   if(m==0){t.stances?.stunner?.StunFor(TechStun,p);Items.Mote(t,"击倒",OpenColor);}
   else{var dest=DNDPush.Destination(t.Map,t.Position,(t.Position-p.Position).ToVector3(),3);DNDPush.Move(t,dest);Items.Mote(t,"推开",OpenColor);}
  }

  // ---------- 主动能力 ----------
  static readonly HashSet<string> Known=new HashSet<string>{"S3ShadowArts","S3ShadowStep","S3Cloak","S3TechMode","S3Wholeness","S3Palm","S3Detonate","S3Tome","S3Manifest","S3DragonEl","S3Breath","S3Aspect",
   "S3LandSpell","S3NatRecover","S3TerraMode","S3Terraform","S3Halo","S3RotTouch","S3Symbiotic","S3StarBolt","S3FormMode","S3StarryForm","S3Preserve","S3Light","S3Dawn","S3Corona","S3ForgeBless","S3Artisan"};
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(!Known.Contains(e.effect))return null;var t=target.Thing as Pawn;var map=caster.Map;
   switch(e.effect){
    case "S3ShadowArts":if(map==null || !target.Cell.IsValid || !target.Cell.InBounds(map))return "需要选择一个位置。";return null;
    case "S3ShadowStep":{
     if(!InDark(caster))return "自己要站在暗处（光照低于 30%）。";var c=target.Cell;
     if(map==null || !c.IsValid || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.GetFirstPawn(map)!=null)return "需要一个空着的、能站立的格子。";
     if(!DarkCell(map,c))return "目的地也要在暗处。";
     if(caster.Position.DistanceTo(c)>StepRange)return "超出距离（"+StepRange.ToString("0")+" 格）。";
     if(!GenSight.LineOfSight(caster.Position,c,map,true))return "要看得见目的地。";
     return null;}
    case "S3Cloak":return InDark(caster)?null:"要站在暗处（光照低于 30%）才能化身幽影。";
    case "S3Palm":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
    case "S3Detonate":return Palmed(caster)==null?"现在没有身中渗透劲的敌人。":null;
    case "S3Breath":case "S3LandSpell":if(map==null || !target.Cell.IsValid || !target.Cell.InBounds(map))return "需要选择一个位置。";return null;
    case "S3Terraform":{if(map==null || !target.Cell.IsValid || !target.Cell.InBounds(map))return "需要选择一个位置。";if(!GenRadial.RadialCellsAround(target.Cell,ClassEffects.Radius(caster,e),true).Any(c=>Terraformable(map,c)))return "这里没有能改的自然地面（地板、建筑、物品、小人、熔岩之类不能改）。";return null;}
    case "S3Light":if(map==null || !target.Cell.IsValid || !target.Cell.InBounds(map) || !target.Cell.Standable(map))return "需要一个空地。";return null;
    case "S3ForgeBless":return BlessTarget(caster)==null?"手里没有武器、身上也没有护甲可以祝福。":null;
    case "S3Artisan":return caster.Map==null?"需要在地图上。":(caster.Faction!=null && caster.Faction.IsPlayer && caster.Map.listerThings.ThingsOfDef(ThingDefOf.Steel).Where(x=>!x.IsForbidden(Faction.OfPlayer)).Sum(x=>x.stackCount)<50?"地图上没被禁止的钢不够 50。":null);
    case "S3RotTouch":case "S3StarBolt":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
   }
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(!Known.Contains(e.effect))return false;
   var t=target.Thing as Pawn;float s=ClassEffects.Strength(p,e);int dur=ClassEffects.Duration(p,e);float rad=ClassEffects.Radius(p,e);
   switch(e.effect){
    case "S3ShadowArts":Warlock.Darkness(p,target.Cell,rad,dur);Items.Mote(p,"暗影技艺",ShadowColor);break;
    case "S3ShadowStep":if(Invalid(p,e,target)==null){FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,1.5f,ShadowColor);Races124.Blink(p,target.Cell);Items.Give(p,"LP_SH_StepStrike",300);Items.Mote(p,"暗影步",ShadowColor);}break;
    case "S3Cloak":if(InDark(p)){Items.Give(p,"LP_SH_Cloaked");Items.Mote(p,"幽影斗篷",ShadowColor);}break;
    case "S3TechMode":{var d=ClassSubclass.Data(p);if(d!=null){int m=(Mode(p)+1)%3;d.ticks["ohMode"]=m;Items.Mote(p,"散打技巧："+TechModes[m],OpenColor);}break;}
    case "S3Wholeness":{float healed=ClassMelee.Heal(p,3f*ClassSystem.Level(p,ClassSystem.Monk));Items.Mote(p,"混元体 "+healed.ToString("0"),OpenColor);break;}
    case "S3Palm":if(Hostile(p,t))Plant(p,t,dur);break;
    case "S3Detonate":Detonate(p,s);break;
    case "S3Tome":{var d=ClassSubclass.Data(p);if(d!=null){int m=(TomeMode(p)+1)%5;d.ticks["tome"]=m;Items.Mote(p,"觉醒魔典："+TomeModes[m],ClassCasters.Arcane);}break;}
    case "S3Manifest":Manifest(p,dur);break;
    case "S3DragonEl":{var d=ClassSubclass.Data(p);if(d!=null){int m=(DragonMode(p)+1)%5;d.ticks["dmEl"]=m;Items.Mote(p,"龙之打击："+DragonModes[m],OpenColor);}break;}
    case "S3Breath":Breath(p,target.Cell,s);break;
    case "S3Aspect":Aspect(p,s,dur);break;
    case "S3LandSpell":LandSpell(p,target.Cell,s);break;
    case "S3NatRecover":{var h=Adventurer.Get(p);if(h!=null){var c=druid.Def;ClassSystem.AddResource(h,c,ClassSystem.MaxResource(h,c)*.5f);}Items.Mote(p,"自然恢复",LandColor);break;}
    case "S3TerraMode":{var d=ClassSubclass.Data(p);if(d!=null){int m=(TerraMode(p)+1)%TerraModes.Length;d.ticks["terra"]=m;Items.Mote(p,"大地塑形："+TerraModes[m],LandColor);}break;}
    case "S3Terraform":Terraform(p,target.Cell,rad+(ClassSystem.Knows(p,"LP_LD_NatureWard")?1f:0f));break;
    case "S3Halo":if(HaloOn(p)){Items.Remove(p,"LP_SP_HaloOn");Items.Mote(p,"收起死亡光环",SporeColor);}else{Items.Give(p,"LP_SP_HaloOn");Items.Mote(p,"死亡光环",SporeColor);}break;
    case "S3RotTouch":if(Hostile(p,t)){t.TakeDamage(new DamageInfo(Necrotic,ClassEffects.Power(p,e),.3f,-1f,p));if(!t.Dead)Items.Give(t,"LP_SP_Rotting",2500);Items.Mote(t,"腐烂之触",SporeColor);}break;
    case "S3Symbiotic":{Items.Give(p,"LP_SP_SymbioticBuff",dur);var sh=Items.Give(p,"LP_ShieldSpell",dur,4f*ClassSystem.Level(p,druid.Def));if(sh!=null)sh.Severity=4f*ClassSystem.Level(p,druid.Def);Items.Mote(p,"共生实体",SporeColor);break;}
    case "S3StarBolt":if(Hostile(p,t)){t.TakeDamage(new DamageInfo(PaladinCore.Radiant,ClassEffects.Power(p,e),.3f,-1f,p));if(!t.Dead)Items.Give(t,"LP_ST_Guided",600);Items.Mote(t,"指引箭",StarColor);}break;
    case "S3FormMode":{var d=ClassSubclass.Data(p);if(d!=null){int m=(FormMode(p)+1)%3;d.ticks["stForm"]=m;var h=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ST_Form"));if(h!=null)h.Severity=m+1f;Items.Mote(p,"星象形态："+FormModes[m],StarColor);}break;}
    case "S3Preserve":Preserve(p,ClassSystem.Level(p,cleric.Def));break;
    case "S3Light":LightOrb(p,target.Cell);break;
    case "S3Dawn":Dawn(p,s);break;
    case "S3Corona":Items.Give(p,"LP_LT_CoronaOn",dur);Items.Mote(p,"晨光之冕",ClassCasters.Divine);break;
    case "S3ForgeBless":Items.Mote(p,ForgeBless(p)??"没有可以祝福的装备",ClassCasters.Artisan);break;
    case "S3Artisan":Artisan(p,-1);break;
    case "S3StarryForm":{var h=Items.Give(p,"LP_ST_Form",dur,FormMode(p)+1f);if(h!=null)h.Severity=FormMode(p)+1f;Items.Mote(p,"星象形态："+FormModes[FormMode(p)],StarColor);break;}
   }
   return true;
  }
  // 渗透劲：一次近战结算 + 种下劲力（持续 武僧等级 天；同时只能有一个）。
  public static Hediff_ClassMark Plant(Pawn p,Pawn t,int days){
   ClassMelee.Strike(p,t);if(t.Dead)return null;
   var old=Palmed(p);if(old!=null && old!=t)Items.Remove(old,"LP_OH_Palm");
   int ticks=Mathf.Max(1,ClassSystem.Level(p,ClassSystem.Monk))*GenDate.TicksPerDay;
   var m=ClassMartial2.MarkTarget(p,t,"LP_OH_Palm",ticks,1f,OpenColor,"渗透劲");
   var d=ClassSubclass.Data(p);if(d!=null)d.ticks["ohPalm"]=t.thingIDNumber;
   return m;
  }
  public static Pawn Palmed(Pawn p){
   var d=ClassSubclass.Data(p);if(d==null || !d.ticks.TryGetValue("ohPalm",out int id) || p.MapHeld==null)return null;
   var t=p.MapHeld.mapPawns.AllPawns.FirstOrDefault(x=>x.thingIDNumber==id);
   var m=t==null?null:ClassMartial2.Mark(t,"LP_OH_Palm");return m!=null && m.caster==p && !t.Dead?t:null;
  }
  // 引爆：成功率 50%（首领 25%，目标每比你高 1 级 −5%）直接倒下；否则受 55 × 战技强度 力场伤害。
  public static bool Detonate(Pawn p,float s){
   var t=Palmed(p);if(t==null)return false;Items.Remove(t,"LP_OH_Palm");ClassSubclass.Data(p)?.ticks.Remove("ohPalm");
   float chance=Subclass2.Chance(ClassSubclass.Bossy(t)?PalmBoss:PalmChance,p,t);
   if(Roll<chance){HealthUtility.DamageUntilDowned(t,false);Items.Mote(t,"渗透劲：倒下",OpenColor);return true;}
   t.TakeDamage(new DamageInfo(Warlock.ForceDamage,PalmDamage*s,1f,-1f,p));Items.Mote(t,"渗透劲",OpenColor);return false;
  }

  // ---------- 伤害（Subclass3Damage） ----------
  public static bool Busy;
  public static float DamageFactor(Pawn v,DamageInfo d){
   var a=d.Instigator as Pawn;bool hostile=a!=null && a!=v && a.HostileTo(v);
   // 法术塑形：塑能学派自己的法术（没有武器的火焰、闪电、寒冰、奥术力）不伤友军。
   if(!hostile && a!=null && a!=v && d.Weapon==null && S(a,FSculpt) && Subclass2.IsSpell(d.Def)){Items.Mote(v,"法术塑形",ClassCasters.Arcane);return 0f;}
   // 一字同心：一天一次，伤势已过半时挡下一次 10 点以上的伤害，代价是 3～18 点法力。
   if(!Busy && S(v,FOneWord) && d.Amount>=10f && v.health.summaryHealth.SummaryHealthPercent<.5f){var vd=ClassSubclass.Data(v);if(vd!=null && !vd.ticks.ContainsKey("oneWord")){vd.ticks["oneWord"]=1;var hv=Adventurer.Get(v);if(hv!=null)ClassSystem.AddResource(hv,wizard.Def,-Rand.RangeInclusive(3,18));Items.Mote(v,"一字同心",ClassCasters.Arcane);return 0f;}}
   // 古龙姿·抗性：所选元素伤害 ×0.5。
   var ar=Items.Hediff("LP_DM_AspectResist");var arh=ar==null?null:v.health?.hediffSet?.GetFirstHediffOfDef(ar);
   float pre=arh!=null && DragonElementOf(d.Def)==Mathf.RoundToInt(arh.Severity)-1?.5f:1f;
   if(!hostile)return pre;float g=pre;
   // 攻击方：出手就解除幽影斗篷、宁神境；暗影步之后的第一击 ×1.5。
   if(Items.Has(a,"LP_SH_Cloaked"))Items.Remove(a,"LP_SH_Cloaked");
   if(Items.Has(a,"LP_OH_Tranquil"))Items.Remove(a,"LP_OH_Tranquil");
   if(ClassMartial2.Melee(d) && Items.Has(a,"LP_SH_StepStrike")){Items.Remove(a,"LP_SH_StepStrike");g*=1.5f;}
   // 共生实体：近战附加坏死；被指引箭照亮的目标下一下 ×1.25。
   if(!Busy && ClassMartial2.Melee(d) && Items.Has(a,"LP_SP_SymbioticBuff")){float amt=4f*Verdant(a);var tv=v;var me=a;Fiends.Defer(()=>{if(tv.Dead || !tv.Spawned)return;Busy=true;try{tv.TakeDamage(new DamageInfo(Necrotic,amt,.2f,-1f,me));}finally{Busy=false;}});}
   if(!Busy && Items.Has(v,"LP_ST_Guided")){Items.Remove(v,"LP_ST_Guided");g*=1.25f;}
   // 神圣打击（生命：光耀；熔炉：火焰）：每 2 秒第一次武器命中 +8 × 强度（14 级起 16）。
   if(!Busy && d.Weapon!=null && S(a,FDivineStrike)){var sd=ClassSubclass.Data(a);sd.ticks.TryGetValue("dsT",out int last);if(last<=0 || Now-last>=120 || Now<last){sd.ticks["dsT"]=Math.Max(1,Now);float amt=(ClassSystem.Level(a,cleric.Def)>=14?16f:8f)*Divine(a);var def=ClassSubclass.Has(a,Forge)?DamageDefOf.Flame:PaladinCore.Radiant;var tv=v;var me=a;Fiends.Defer(()=>{if(tv.Dead || !tv.Spawned)return;Busy=true;try{tv.TakeDamage(new DamageInfo(def,amt,.3f,-1f,me));}finally{Busy=false;}});}}
   // 守护之光
   if(!Busy && Users.Count>0 && Flare(v,a))return 0f;
   // 熔炉之魂 / 熔炉圣徒
   if(S(v,FForgeSaint) && (d.Def==DamageDefOf.Flame || d.Def==DamageDefOf.Burn))return 0f;
   if(S(v,FForgeSoul) && ClassSubclass.Element(d.Def)==0)g*=.5f;
   if(S(v,FForgeSoul) && HeavyArmor(v))g*=S(v,FForgeSaint) && !Subclass2.IsSpell(d.Def)?.5f:.9f;
   // 自然庇护所：动物伤不了你。
   if(S(v,FSanctuary) && a.RaceProps.Animal){Items.Mote(v,"自然庇护",LandColor);return 0f;}
   if(S(v,FFungalBody) && d.Def.armorCategory?.defName=="Sharp")g*=.75f;
   if(S(v,FFullStars) && Items.Has(v,"LP_ST_Form") && !ClassSubclass.Psychic(d.Def))g*=.5f;
   if(Users.Count>0 && v.Spawned)foreach(var kv in Users){if((kv.Value & FOmen)==0)continue;var sg=v.Map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.thingIDNumber==kv.Key);if(sg==null || !sg.Position.InHorDistOf(v.Position,10f))continue;var od=ClassSubclass.Data(sg);if(od==null || !od.ticks.TryGetValue("omen",out int om))continue;
    if(om==1 && v.Faction==sg.Faction)g*=.9f;else if(om==2 && a.HostileTo(sg)==false && v.HostileTo(sg))g*=1.1f;break;}
   // 龙之打击：徒手命中附加 4 × 战技强度 所选元素伤害。
   if(!Busy && S(a,FDisciple) && ClassMartial2.Melee(d) && MonkCore.Unarmed(a)){float amt=4f*MonkCore.Strength(a);var def=DragonDamage(a);var tv=v;var me=a;Fiends.Defer(()=>{if(tv.Dead || !tv.Spawned)return;Busy=true;try{tv.TakeDamage(new DamageInfo(def,amt,.2f,-1f,me));}finally{Busy=false;}});}
   // 受击方：宁神境 50% 的成功率这一下落空（目标每比施法者高 1 级 −5%）。
   if(Items.Has(v,"LP_OH_Tranquil") && Roll<Subclass2.Chance(TranquilChance,v,a)){Items.Mote(v,"宁神境",OpenColor);return 0f;}
   // 灵机一斩：身边 1 格的敌人被别人打中，暗影武僧补一击（每 2 秒一次）。
   if(!Busy && Opportunists.Count>0 && v.Spawned)foreach(var o in Opportunists){
    if(o==null || o==a || o.Dead || o.Downed || !o.Spawned || o.Map!=v.Map || !o.HostileTo(v) || !o.Position.AdjacentTo8WayOrInside(v.Position))continue;
    var sd=ClassSubclass.Data(o);if(sd==null)continue;sd.ticks.TryGetValue("opp",out int last);if(last>0 && Now-last<OpportunistGap && Now>=last)continue;
    sd.ticks["opp"]=Math.Max(1,Now);var me=o;var tgt=v;
    Fiends.Defer(()=>{if(me.Dead || me.Downed || tgt.Dead || !tgt.Spawned)return;Busy=true;try{ClassMelee.Strike(me,tgt);}finally{Busy=false;}Items.Mote(me,"灵机一斩",ShadowColor);});
    break;
   }
   return g;
  }
  public static void Reset(){Users.Clear();Opportunists.Clear();Wingers.Clear();Busy=false;}

  // ---------- 神龙宗（1.108.0） ----------
  public static readonly string[] DragonModes={"火焰","寒冰","闪电","强酸","毒素"};
  static readonly string[] DragonDefs={"Flame","LP_Frost","LP_Lightning","LP_OozeAcid","LP_DragonPoison"};
  public static int DragonMode(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;d.ticks.TryGetValue("dmEl",out int m);return Mathf.Clamp(m,0,4);}
  public static DamageDef DragonDamage(Pawn p){var n=DragonDefs[DragonMode(p)];return n=="Flame"?DamageDefOf.Flame:DefCache<DamageDef>.Get(n)??DamageDefOf.Flame;}
  static int Day=>GenDate.DaysPassed+1;
  public static int BreathMax(Pawn p)=>2+Mathf.Max(0,ClassSystem.Level(p,ClassSystem.Monk)-1)/4; // 熟练加值
  public static int BreathLeft(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;d.ticks.TryGetValue("dmDay",out int day);d.ticks.TryGetValue("dmUsed",out int u);if(day!=Day)u=0;return Mathf.Max(0,BreathMax(p)-u);}
  public static bool AspectFree(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return false;d.ticks.TryGetValue("dmAspect",out int day);return day!=Day;}
  public static bool DragonFree(Pawn p,CompProperties_ClassEffect props)=>props!=null && (props.effect=="S3Breath" && BreathLeft(p)>0 || props.effect=="S3Aspect" && AspectFree(p));
  static void DragonPaid(Pawn p,CompProperties_ClassEffect e){
   var d=ClassSubclass.Data(p);if(d==null || e==null)return;
   if(e.effect=="S3Breath" && BreathLeft(p)>0){d.ticks.TryGetValue("dmDay",out int day);d.ticks.TryGetValue("dmUsed",out int u);if(day!=Day)u=0;d.ticks["dmDay"]=Day;d.ticks["dmUsed"]=u+1;}
   else if(e.effect=="S3Aspect" && AspectFree(p))d.ticks["dmAspect"]=Day;
  }
  // 龙之息：锥形（神龙身 17 级起更长），每个敌人受 武艺骰 × 战技强度 的所选元素伤害，成功率 50% 闪开一半（目标每比你高 1 级闪开率 +5%）。
  public static int Breath(Pawn p,IntVec3 target,float s){
   var map=p.Map;if(map==null || !target.IsValid)return 0;int lv=ClassSystem.Level(p,ClassSystem.Monk);
   float range=lv>=17?12f:6f,amount=(lv>=17?24f:lv>=11?18f:12f)*s;var def=DragonDamage(p);int n=0;
   var cells=Dragons.ShapeCells(map,p.Position,target,range,false,Dragons.ConeHalfAngle,p.Rotation);
   foreach(var c in cells)FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,.8f,Color.Lerp(Color.white,OpenColor,.5f));
   var hit=new HashSet<Pawn>();
   foreach(var c in cells)foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList()){
    if(!Hostile(p,t) || !hit.Add(t))continue;float a=Roll<Subclass2.Chance(.5f,p,t)?amount:amount*.5f;
    t.TakeDamage(new DamageInfo(def,a,.2f,-1f,p));n++;
   }
   Items.Mote(p,"龙之息·"+DragonModes[DragonMode(p)],OpenColor);return n;
  }
  // 古龙姿：1 分钟灵光——半径 3 内友军（含自己）所选元素伤害 ×0.5；敌人成功率 50% 恐慌；神龙身再对敌人爆发 16 × 战技强度 元素伤害。
  public static int Aspect(Pawn p,float s,int ticks){
   var map=p.Map;if(map==null)return 0;var def=DragonDamage(p);int n=0;bool fury=S(p,FAscendant);
   foreach(var x in Races124.PawnsNear(map,p.Position,3f).ToList()){
    if(x.Dead)continue;
    if(!x.HostileTo(p)){if(x.Faction==p.Faction){var h=Items.Give(x,"LP_DM_AspectResist",ticks,DragonMode(p)+1f);if(h!=null)h.Severity=DragonMode(p)+1f;}continue;}
    if(Roll<Subclass2.Chance(.5f,p,x)){ClassSubclass.Fear(x,600,"古龙姿");n++;}
    if(fury)x.TakeDamage(new DamageInfo(def,16f*s,.2f,-1f,p));
   }
   FleckMaker.ThrowDustPuffThick(p.DrawPos,map,3f,OpenColor);Items.Mote(p,fury?"古龙姿·怒气爆发":"古龙姿",OpenColor);return n;
  }
  // ==================== 1.109.0 德鲁伊：大地、孢子、群星结社 ====================
  static readonly DefRef<LP_ClassDef> druid=new DefRef<LP_ClassDef>("LP_Class_Druid");
  public static readonly Color LandColor=new Color(.45f,.75f,.4f),SporeColor=new Color(.6f,.55f,.4f),StarColor=new Color(.7f,.8f,1f);
  static float Verdant(Pawn p){var s=DefCache<StatDef>.Get("LP_VerdantPower");return (s==null?1f:p.GetStatValue(s))*ClassSystem.PsychicFactor(p,psy);}
  static readonly CompProperties_ClassEffect psy=new CompProperties_ClassEffect{psychic=true};
  static DamageDef Necrotic=>DefCache<DamageDef>.Get("LP_Necrotic")??DamageDefOf.Burn;
  static void DruidTick(Pawn p,Hediff_Adventurer h,int f){
   var d=ClassSubclass.Data(h);
   if((f & FLandStride)!=0){Items.Remove(p,"LP_Entangled");Passive(p,"LP_LD_StrideBuff",true);}
   if((f & FNatureWard)!=0){Items.Remove(p,"FoodPoisoning");var tox=p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);if(tox!=null)p.health.RemoveHediff(tox);Passive(p,"LP_LD_WardBuff",true);}
   if((f & FFungalBody)!=0){var tox=p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);if(tox!=null)p.health.RemoveHediff(tox);Passive(p,"LP_SP_BodyBuff",true);if(p.InMentalState && ClassSubclass.FearState(p.MentalStateDef))p.MentalState.RecoverFromState();}
   if((f & FStarMap)!=0)Passive(p,"LP_ST_MapBuff",true);
   if(!p.Spawned || p.Downed)return;
   if((f & FSanctuary)!=0 && !p.Position.GetTerrain(p.Map).layerable)ClassMelee.Heal(p,1f); // 站在自然地面上
   if((f & FHalo)!=0 && Items.Has(p,"LP_SP_HaloOn"))Halo(p);
   if((f & FOmen)!=0 && d!=null){if(!p.Awake())d.ticks.Remove("omenDay");else if(!d.ticks.TryGetValue("omenDay",out int od) || od!=Day){d.ticks["omenDay"]=Day;d.ticks["omen"]=Rand.Bool?1:2;Items.Mote(p,d.ticks["omen"]==1?"宇宙预兆：吉":"宇宙预兆：凶",StarColor);}}
   if(Items.Has(p,"LP_ST_Form"))StarryTick(p,f);
  }

  // ---------- 大地法术：按地块气候（不认具体生物群系，Alpha Biomes 等模组的生物群系照样分得出） ----------
  public static readonly string[] Climates={"寒带","温带","沙漠","湿热"};
  public static int Climate(Map map){
   if(map==null)return 1;var tile=Find.WorldGrid[map.Tile];float temp=tile.temperature,rain=tile.rainfall;var bn=map.Biome?.defName??"";
   if(temp<0f || bn.Contains("Tundra") || bn.Contains("Ice"))return 0;
   if(bn.Contains("Desert") || temp>15f && rain<600f)return 2;
   if(bn.Contains("Swamp") || bn.Contains("Marsh") || bn.Contains("Jungle") || rain>1600f)return 3;
   return 1;
  }
  public static int LandSpell(Pawn p,IntVec3 c,float s){
   var map=p.Map;if(map==null)return 0;int cl=Climate(map);bool up=ClassSystem.Knows(p,"LP_LD_NatureWard");int n=0;
   if(cl==2){ // 烈日光柱：直线 10 格
    foreach(var cell in GenSight.PointsOnLineOfSight(p.Position,c).Take(10)){if(!cell.InBounds(map))continue;FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(),map,.8f);
     foreach(var t in cell.GetThingList(map).OfType<Pawn>().ToList())if(Hostile(p,t)){float amt=15f*s*(up && map.skyManager.CurSkyGlow>.5f?1.5f:1f);t.TakeDamage(new DamageInfo(DamageDefOf.Flame,amt,.2f,-1f,p));if(!t.Dead)Items.Give(t,"LP_WL_Darkened",180,1f);n++;}}
    Items.Mote(p,"烈日光柱",LandColor);return n;
   }
   float rad=up?5f:3f;
   foreach(var t in Races124.PawnsNear(map,c,cl==0?3f:rad).ToList()){
    if(!Hostile(p,t))continue;n++;
    if(cl==0){t.TakeDamage(new DamageInfo(DefCache<DamageDef>.Get("LP_Frost")??DamageDefOf.Frostbite,8f*s,.1f,-1f,p));if(t.Dead)continue;Items.Give(t,"LP_LD_Frozen",600,up?2f:1f);if(up && Roll<Subclass2.Chance(.5f,p,t))t.stances?.stunner?.StunFor(180,p);}
    else if(cl==1){t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,6f*s,.1f,-1f,p));if(!t.Dead)Items.Give(t,"LP_Entangled",600,t.BodySize>=2f?2f:1f);}
    else{HealthUtility.AdjustSeverity(t,HediffDefOf.ToxicBuildup,(up?.25f:.15f)*s);}
   }
   FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,3f,LandColor);Items.Mote(p,cl==0?"寒霜之握":cl==1?"缠根":"疫病之雾",LandColor);return n;
  }
  // ---------- 大地塑形：永久改地形 ----------
  public static readonly string[] TerraModes={"深水","浅水","沼泽","泥地","土壤","沃土","沙地"};
  static readonly string[] TerraDefs={"WaterDeep","WaterShallow","Marsh","Mud","Soil","SoilRich","Sand"};
  public static int TerraMode(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;d.ticks.TryGetValue("terra",out int m);return Mathf.Clamp(m,0,TerraDefs.Length-1);}
  // 能改的格子：自然地面（不是玩家铺的地板）、没有建筑和小人、不是熔岩 / 丙烷这类有特殊机制的地形（Alpha Biomes）。
  public static bool Terraformable(Map map,IntVec3 c){
   if(!c.InBounds(map))return false;var t=c.GetTerrain(map);if(t==null || t.layerable || t.BuildableByPlayer)return false;
   var n=t.defName;if(n.Contains("Lava") || n.Contains("Propane") || n.Contains("Chasm") || n.Contains("Void"))return false;
   if(c.GetEdifice(map)!=null || c.GetFirstPawn(map)!=null)return false;
   foreach(var th in c.GetThingList(map))if(th.def.category==ThingCategory.Building || th.def.category==ThingCategory.Item)return false;
   return true;
  }
  public static int Terraform(Pawn p,IntVec3 center,float radius){
   var map=p.Map;if(map==null)return 0;var def=DefCache<TerrainDef>.Get(TerraDefs[TerraMode(p)]);if(def==null)return 0;int n=0;
   foreach(var c in GenRadial.RadialCellsAround(center,radius,true)){
    if(!Terraformable(map,c) || c.GetTerrain(map)==def)continue;
    if(!def.passability.Equals(Traversability.Standable) || def.IsWater)foreach(var pl in c.GetThingList(map).OfType<Plant>().ToList())pl.Destroy();
    map.terrainGrid.SetTerrain(c,def);n++;
   }
   FleckMaker.ThrowDustPuffThick(center.ToVector3Shifted(),map,3f,LandColor);Items.Mote(p,"大地塑形："+TerraModes[TerraMode(p)],LandColor);return n;
  }

  // ---------- 孢子 ----------
  static bool NearFungus(Pawn p){foreach(var th in GenRadial.RadialDistinctThingsAround(p.Position,p.Map,3f,true))if(th is Plant pl){var n=pl.def.defName;if(n.Contains("Agarilux") || n.Contains("Bryolux") || n.Contains("Glowstool") || n.Contains("Fung") || n.Contains("Mushroom") || n.Contains("Myco"))return true;}return false;}
  public static int Halo(Pawn p){
   float amt=3f*Verdant(p)*(Items.Has(p,"LP_SP_SymbioticBuff")?2f:1f)*(NearFungus(p)?1.5f:1f);int n=0;
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,2f).ToList())if(Hostile(p,t)){Busy=true;try{t.TakeDamage(new DamageInfo(Necrotic,amt,1f,-1f,p));}finally{Busy=false;}n++;}
   return n;
  }
  public static bool HaloOn(Pawn p)=>Items.Has(p,"LP_SP_HaloOn");
  public static int PuppetCount(Map map)=>map==null?0:map.mapPawns.AllPawnsSpawned.Count(x=>Items.Has(x,"LP_SP_Puppet"));
  // 真菌滋长：10 格内的人形敌人死后，成功率 50% 长出真菌站起来为你打 1 天（装备先掉在地上，到期化成腐土）。
  public static void Killed(Pawn victim,Pawn killer){
   if(!victim.RaceProps.Humanlike || victim.MapHeld==null || Items.IsLich(victim))return;var map=victim.MapHeld;var pos=victim.PositionHeld;
   Pawn druidP=null;foreach(var kv in Users)if((kv.Value & FInfest)!=0){var d=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.thingIDNumber==kv.Key);if(d!=null && !d.Downed && d.Position.InHorDistOf(pos,10f) && victim.HostileTo(d)){druidP=d;break;}}
   if(druidP==null || PuppetCount(map)>=4 || Roll>=.5f)return;var dr=druidP;
   Fiends.Defer(()=>Raise(dr,victim));
  }
  public static Pawn Raise(Pawn druidP,Pawn victim){
   var corpse=victim.Corpse;if(corpse==null || !corpse.Spawned)return null;var map=corpse.Map;var pos=corpse.Position;
   victim.equipment?.DropAllEquipment(pos,false);victim.apparel?.DropAll(pos,false);victim.inventory?.DropAllNearPawn(pos);
   if(!ResurrectionUtility.TryResurrect(victim,new ResurrectionParams{removeDiedThoughts=true}))return null;
   if(victim.Faction!=druidP.Faction)victim.SetFaction(druidP.Faction);
   var h=Items.Give(victim,"LP_Conjured");var comp=(h as HediffWithComps)?.TryGetComp<HediffComp_Conjured>();if(comp!=null)comp.ticksLeft=GenDate.TicksPerDay;
   Items.Give(victim,"LP_SP_Puppet");Items.Mote(victim,"真菌傀儡",SporeColor);return victim;
  }

  // ---------- 群星 ----------
  public static int StarBoltMax(Pawn p)=>2+Mathf.Max(0,ClassSystem.Level(p,druid.Def)-1)/4;
  public static int StarBoltLeft(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;d.ticks.TryGetValue("stDay",out int day);d.ticks.TryGetValue("stUsed",out int u);if(day!=Day)u=0;return Mathf.Max(0,StarBoltMax(p)-u);}
  static void StarPaid(Pawn p){var d=ClassSubclass.Data(p);if(d==null || StarBoltLeft(p)<=0)return;d.ticks.TryGetValue("stDay",out int day);d.ticks.TryGetValue("stUsed",out int u);if(day!=Day)u=0;d.ticks["stDay"]=Day;d.ticks["stUsed"]=u+1;}
  public static bool NightOutdoors(Pawn p)=>p.Spawned && !p.Position.Roofed(p.Map) && p.Map.skyManager.CurSkyGlow<.4f;
  public static readonly string[] FormModes={"弓手","圣杯","巨龙"};
  public static int FormMode(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;d.ticks.TryGetValue("stForm",out int m);return Mathf.Clamp(m,0,2);}
  static void StarryTick(Pawn p,int f){
   float s=Verdant(p);int m=FormMode(p);
   if(m==0){var t=Races124.PawnsNear(p.Map,p.Position,15f).Where(x=>Hostile(p,x) && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault();if(t!=null){Busy=true;try{t.TakeDamage(new DamageInfo(PaladinCore.Radiant,8f*s,.3f,-1f,p));}finally{Busy=false;}Items.Mote(t,"星光箭",StarColor);}}
   else if(m==1){var a=Races124.PawnsNear(p.Map,p.Position,6f).Where(x=>!x.Dead && x.Faction==p.Faction && x.health.summaryHealth.SummaryHealthPercent<.999f).OrderBy(x=>x.health.summaryHealth.SummaryHealthPercent).FirstOrDefault();if(a!=null)ClassMelee.Heal(a,4f*s);}
  }
  // ==================== 1.110.0 牧师：生命、光明、熔炉领域 ====================
  static readonly DefRef<LP_ClassDef> cleric=new DefRef<LP_ClassDef>("LP_Class_Cleric");
  // 引导神力：每天 1 次（6 级 2 次、18 级 3 次），不耗神恩。
  static readonly HashSet<string> ChannelAbilities=new HashSet<string>{"LP_LF_Preserve","LP_LT_Dawn"};
  public static int ChannelMax(Pawn p){int lv=ClassSystem.Level(p,cleric.Def);return lv<=0?0:1+(lv>=6?1:0)+(lv>=18?1:0);}
  public static int ChannelLeft(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;d.ticks.TryGetValue("chDay",out int day);d.ticks.TryGetValue("chUsed",out int u);if(day!=Day)u=0;return Mathf.Max(0,ChannelMax(p)-u);}
  public static void ChannelUse(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return;d.ticks.TryGetValue("chDay",out int day);d.ticks.TryGetValue("chUsed",out int u);if(day!=Day)u=0;d.ticks["chDay"]=Day;d.ticks["chUsed"]=u+1;}
  public static string CannotCast(Pawn p,AbilityDef def,CompProperties_ClassEffect props)=>def!=null && ChannelAbilities.Contains(def.defName) && ChannelLeft(p)<=0?"今天的引导神力用完了":null;
  public static bool Pay(Pawn p,AbilityDef def,CompProperties_ClassEffect props){if(def==null || !ChannelAbilities.Contains(def.defName))return false;ChannelUse(p);return true;}
  public static string CostText(Pawn p,AbilityDef def)=>def!=null && ChannelAbilities.Contains(def.defName)?"消耗：引导神力 1（今天还剩 "+ChannelLeft(p)+" / "+ChannelMax(p)+"）":null;
  static float Divine(Pawn p){var s=DefCache<StatDef>.Get("LP_DivinePower");return (s==null?1f:p.GetStatValue(s))*ClassSystem.PsychicFactor(p,psy)*(ClassSystem.Devotee(p)?1.2f:1f);}

  // 保全生命：半径 6 内友军分 5 × 牧师等级 的治疗，先给伤得最重的，每人最多治到一半生命。
  public static float Preserve(Pawn p,int level){
   float pool=5f*Mathf.Max(1,level),total=0f;if(p.Map==null)return 0f;
   foreach(var a in Races124.PawnsNear(p.Map,p.Position,6f).Where(x=>!x.Dead && x.Faction==p.Faction).OrderBy(x=>x.health.summaryHealth.SummaryHealthPercent).ToList()){
    if(pool<=0f)break;if(a.health.summaryHealth.SummaryHealthPercent>=.5f)continue;
    float h=ClassMelee.Heal(a,Mathf.Min(pool,ClassMelee.InjurySum(a)*.5f+1f));pool-=h;total+=h;if(h>0f)Items.Mote(a,"保全生命 "+h.ToString("0"),ClassCasters.Divine);
   }
   return total;
  }
  // 光亮术：一个格子发光 1 天。
  public static Thing LightOrb(Pawn p,IntVec3 c){var def=DefCache<ThingDef>.Get("LP_LT_LightOrb");if(def==null || p.Map==null)return null;var th=GenSpawn.Spawn(ThingMaker.MakeThing(def),c,p.Map);Items.Mote(p,"光亮术",ClassCasters.Divine);return th;}
  // 破晓之光：半径 6 驱散黑暗与隐形，敌人受 10 × 强度 + 牧师等级 光耀（6 级起对亡灵、邪魔 ×2）。
  public static int Dawn(Pawn p,float s){
   var map=p.Map;if(map==null)return 0;int n=0;float amt=10f*s+ClassSystem.Level(p,cleric.Def);
   foreach(var x in Races124.PawnsNear(map,p.Position,6f).ToList()){
    Items.Remove(x,"LP_WL_Darkened");if(x.Dead || !Hostile(p,x))continue;
    foreach(var n2 in new[]{"LP_SH_Cloaked","LP_ATInvisible"})Items.Remove(x,n2);Items.Give(x,"LP_Spotted",3600);
    float a=amt*(ClassSystem.Knows(p,"LP_LT_ImprovedFlare") && PaladinCore.HolyVulnerable(x)?2f:1f);Busy=true;try{x.TakeDamage(new DamageInfo(PaladinCore.Radiant,a,.4f,-1f,p));}finally{Busy=false;}n++;
   }
   FleckMaker.ThrowLightningGlow(p.DrawPos,map,4f);Items.Mote(p,"破晓之光",ClassCasters.Divine);return n;
  }
  static void Corona(Pawn p){
   float amt=8f*Divine(p);
   foreach(var x in Races124.PawnsNear(p.Map,p.Position,6f).ToList()){if(!Hostile(p,x) || x.Downed)continue;Items.Give(x,"LP_LT_Dazzled",300);Busy=true;try{x.TakeDamage(new DamageInfo(PaladinCore.Radiant,amt,.4f,-1f,p));}finally{Busy=false;}}
  }
  public static int FlareMax(Pawn p)=>Mathf.Max(1,Adventurer.Mod(Adventurer.Score(p,Adventurer.Wis)));
  public static int FlareLeft(Pawn p){var d=ClassSubclass.Data(p);if(d==null)return 0;d.ticks.TryGetValue("flDay",out int day);d.ticks.TryGetValue("flUsed",out int u);if(day!=Day)u=0;return Mathf.Max(0,FlareMax(p)-u);}
  static void FlareUse(Pawn p){var d=ClassSubclass.Data(p);d.ticks.TryGetValue("flDay",out int day);d.ticks.TryGetValue("flUsed",out int u);if(day!=Day)u=0;d.ticks["flDay"]=Day;d.ticks["flUsed"]=u+1;}
  // 守护之光：15 格内的光明牧师用掉一次，成功率 50%（攻击者每比牧师高 1 级 −5%）这一下被闪得落空。
  static bool Flare(Pawn v,Pawn a){
   foreach(var kv in Users){
    if((kv.Value & FFlare)==0)continue;Pawn c=kv.Key==v.thingIDNumber?v:null;
    if(c==null){if((kv.Value & FFlareAlly)==0 || !v.Spawned)continue;c=v.Map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.thingIDNumber==kv.Key);if(c==null || c.Faction!=v.Faction || !c.Position.InHorDistOf(v.Position,15f))continue;}
    if(c.Downed || FlareLeft(c)<=0)continue;FlareUse(c);
    if(Roll<Subclass2.Chance(.5f,c,a)){Items.Mote(a,"守护之光",ClassCasters.Divine);if(a.Spawned)FleckMaker.ThrowLightningGlow(a.DrawPos,a.Map,1.5f);return true;}
    return false;
   }
   return false;
  }
  // 熔炉祝福：手里的武器（没有就护甲最好的一件衣服）附魔强化 +1；换一件祝福时，上一件的 +1 收回。
  public static Thing BlessTarget(Pawn p){var w=p.equipment?.Primary;if(w!=null && GearEnhance.Comp(w)!=null)return w;return p.apparel?.WornApparel.Where(x=>GearEnhance.Comp(x)!=null).OrderByDescending(x=>x.GetStatValue(StatDefOf.ArmorRating_Sharp)).FirstOrDefault();}
  public static string ForgeBless(Pawn p){
   var t=BlessTarget(p);var c=GearEnhance.Comp(t);if(c==null)return null;var d=ClassSubclass.Data(p);
   if(d!=null && d.ticks.TryGetValue("forgeId",out int oldId) && oldId!=t.thingIDNumber){
    Thing old=p.equipment?.AllEquipmentListForReading.FirstOrDefault(x=>x.thingIDNumber==oldId) as Thing??p.apparel?.WornApparel.FirstOrDefault(x=>x.thingIDNumber==oldId);
    if(old==null && p.MapHeld!=null)old=p.MapHeld.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).FirstOrDefault(x=>x.thingIDNumber==oldId);
    var oc=GearEnhance.Comp(old);if(oc!=null && oc.level>0){oc.level--;GearEnhance.ClearStatCache(old);}
   }
   else if(d!=null && d.ticks.TryGetValue("forgeId",out int same) && same==t.thingIDNumber)return "这件已经受过熔炉祝福";
   if(c.level>=GearEnhance.Max)return "已经强化到 +"+GearEnhance.Max;
   c.level++;GearEnhance.ClearStatCache(t);if(d!=null)d.ticks["forgeId"]=t.thingIDNumber;return "熔炉祝福："+t.LabelNoCount;
  }
  // 工匠祝福：50 钢变出一件优秀品质的长剑 / 长矛 / 钉锤（玩家弹窗选，测试直接给编号）。
  static readonly string[] ArtisanItems={"MeleeWeapon_LongSword","MeleeWeapon_Spear","MeleeWeapon_Mace"};
  public static Thing Artisan(Pawn p,int pick){
   if(pick<0 && p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed){
    var opts=new List<FloatMenuOption>();for(int i=0;i<ArtisanItems.Length;i++){int k=i;var def=DefCache<ThingDef>.Get(ArtisanItems[i]);if(def!=null)opts.Add(new FloatMenuOption(def.LabelCap,()=>Artisan(p,k)));}
    Find.WindowStack.Add(new FloatMenu(opts));return null;
   }
   var d2=DefCache<ThingDef>.Get(ArtisanItems[Mathf.Clamp(pick,0,ArtisanItems.Length-1)]);var map=p.Map;if(d2==null || map==null)return null;
   if(p.Faction!=null && p.Faction.IsPlayer){int need=50;foreach(var x in map.listerThings.ThingsOfDef(ThingDefOf.Steel).Where(x=>!x.IsForbidden(Faction.OfPlayer)).ToList()){if(need<=0)break;int k=Math.Min(need,x.stackCount);x.SplitOff(k).Destroy();need-=k;}}
   var th=ThingMaker.MakeThing(d2,ThingDefOf.Steel);th.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Colony);
   GenPlace.TryPlaceThing(th,p.Position,map,ThingPlaceMode.Near);Items.Mote(p,"工匠祝福："+th.LabelNoCount,ClassCasters.Artisan);return th;
  }
  static bool HeavyArmor(Pawn v)=>v.GetStatValue(StatDefOf.ArmorRating_Sharp)>=.6f;
  public static int DragonElementOf(DamageDef d){if(d==null)return -1;for(int i=0;i<DragonDefs.Length;i++){if(DragonDefs[i]=="Flame"?d==DamageDefOf.Flame || d==DamageDefOf.Burn:d.defName==DragonDefs[i])return i;}return -1;}
 }
 public class GameComponent_Subclass3 : GameComponent {
  public GameComponent_Subclass3(Game game){}
  public override void FinalizeInit(){base.FinalizeInit();Subclass3.Reset();}
 }

 // 真菌滋长：身边的人形敌人死后可能长成真菌傀儡。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class Subclass3Kill {
  static void Postfix(Pawn __instance,DamageInfo? dinfo){if(Subclass3.Users.Count==0 || !__instance.Dead)return;Subclass3.Killed(__instance,dinfo?.Instigator as Pawn);}
 }
 // 真菌之躯：不会恐惧逃跑。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class Subclass3Mind {
  static bool Prefix(Pawn ___pawn,MentalStateDef stateDef,ref bool __result){
   if(Subclass3.Users.Count==0 || stateDef==null || !Subclass3.S(___pawn,Subclass3.FFungalBody) || !ClassSubclass.FearState(stateDef))return true;__result=false;return false;
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class Subclass3Damage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(Subclass3.Users.Count==0)return;
   if(dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   float f=Subclass3.DamageFactor(__instance,dinfo);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
}
