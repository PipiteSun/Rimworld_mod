using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.112.0 DND 植入物（植入物构思文档，Defs/Implants_DND.xml，由 ArtSource/Implants/gen_implants.py 生成）。
 // 调谐：稀有档和神器档植入物占调谐位，每人 3 个，器械师 11 级 +1、18 级再 +1；普通档不占位。
 // 效果按植入物位（Flags，按需扫一次 hediff 后缓存；增删植入物时作废）；Live = 存在的植入物数，0 时所有挂钩第一步就返回。
 public class ImplantExtension : DefModExtension {public bool attune;public string flag;public bool ionStone;public bool graft,noCelestial;public string group;public int element=-1;public AbilityDef ability,ability2;public IEnumerable<AbilityDef> Abilities{get{if(ability!=null)yield return ability;if(ability2!=null)yield return ability2;}}} // 1.113.0 嫁接物（活体专用）、互斥组、龙的元素、附带能力
 public class ArtificerSurgeryExtension : DefModExtension {}
 public static class DndImplants {
  public const int AttuneBase=3,StoneMax=3,ScoreCapPlain=20;
  public const float ColdIronFactor=1.3f,AdamantPartFactor=.5f,AdamantPen=.25f,SurgeonBonus=.15f,TuneStep=.03f,TuneMax=.15f,InfuseBonus=.1f;
  public const long FInfused=1,FColdArm=2,FMithralLeg=4,FAdamArm=8,FClockEye=16,FMithralHeart=32,
   FStr=64,FDex=128,FCon=256,FInt=512,FWis=1024,FCha=2048,FSustain=4096,FRegen=8192,FMastery=16384,FAbsorb=32768,FAlert=65536,
   FHellHeart=1L<<17,FBoneCage=1L<<18,FDeadEar=1L<<19,FSoulJaw=1L<<20,FBeholder=1L<<21,FDemonArm=1L<<22,FTroll=1L<<23,FScale=1L<<24,FBreath=1L<<25,FVecnaHand=1L<<26,FVecnaEye=1L<<27;
  static readonly Dictionary<string,long> FlagNames=new Dictionary<string,long>{{"Infused",FInfused},{"ColdArm",FColdArm},{"MithralLeg",FMithralLeg},{"AdamArm",FAdamArm},{"ClockEye",FClockEye},{"MithralHeart",FMithralHeart},
   {"Str",FStr},{"Dex",FDex},{"Con",FCon},{"Int",FInt},{"Wis",FWis},{"Cha",FCha},{"Sustain",FSustain},{"Regen",FRegen},{"Mastery",FMastery},{"Absorb",FAbsorb},{"Alert",FAlert},
   {"HellHeart",FHellHeart},{"BoneCage",FBoneCage},{"DeadEar",FDeadEar},{"SoulJaw",FSoulJaw},{"Beholder",FBeholder},{"DemonArm",FDemonArm},{"Troll",FTroll},{"Scale",FScale},{"Breath",FBreath},{"VecnaHand",FVecnaHand},{"VecnaEye",FVecnaEye}};
  public static readonly long[] ScoreFlags={FStr,FDex,FCon,FInt,FWis,FCha};
  public static int Live;
  static readonly Dictionary<int,long> cache=new Dictionary<int,long>();
  public static ImplantExtension Ext(HediffDef d)=>d?.GetModExtension<ImplantExtension>();
  public static long FlagOf(HediffDef d){var e=Ext(d);return e?.flag!=null && FlagNames.TryGetValue(e.flag,out long f)?f:0;}
  public static long Flags(Pawn p){
   if(Live<=0 || p?.health?.hediffSet==null)return 0;
   if(cache.TryGetValue(p.thingIDNumber,out long f))return f;
   f=0;var set=p.health.hediffSet;foreach(var h in set.hediffs){var x=FlagOf(h.def);if(x!=0 && (h.Part==null || !set.PartIsMissing(h.Part)))f|=x;}
   cache[p.thingIDNumber]=f;return f;
  }
  public static bool Has(Pawn p,long f)=>(Flags(p) & f)!=0;
  public static void Dirty(Pawn p){if(p==null)return;cache.Remove(p.thingIDNumber);Adventurer.Get(p)?.Dirty();}
  public static void Reset(){cache.Clear();Live=0;foreach(var p in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)if(p?.health?.hediffSet!=null)foreach(var h in p.health.hediffSet.hediffs)if(Ext(h.def)!=null)Live++;}

  // ---------- 调谐 ----------
  static readonly DefRef<LP_ClassDef> artificer=new DefRef<LP_ClassDef>("LP_Class_Artificer");
  public static int AttuneMax(Pawn p){int lv=ClassSystem.Level(p,artificer.Def);return AttuneBase+(lv>=11?1:0)+(lv>=18?1:0);}
  public static int Attuned(Pawn p)=>p?.health?.hediffSet==null?0:p.health.hediffSet.hediffs.Count(h=>Ext(h.def)?.attune==true);
  public static int Stones(Pawn p)=>p?.health?.hediffSet==null?0:p.health.hediffSet.hediffs.Count(h=>Ext(h.def)?.ionStone==true);
  // 能不能再装这个植入物：返回 null 表示可以。
  public static string CanAttune(Pawn p,HediffDef d){
   var e=Ext(d);if(p==null || e==null)return null;
   if(e.ionStone && Stones(p)>=StoneMax)return "额骨上最多镶 "+StoneMax+" 颗伊奥恩石";
   if(e.group!=null && p.health.hediffSet.hediffs.Any(h=>Ext(h.def)?.group==e.group))return "同一类的嫁接物只能装一件";
   if(e.noCelestial && (FaithRaces.IsAasimar(p) || PactRaces.Celestial(p)))return "天界血统容不下恶魔的血肉";
   if(e.attune && Attuned(p)>=AttuneMax(p))return "调谐位满了（"+Attuned(p)+" / "+AttuneMax(p)+"）";
   return null;
  }

  // ---------- 六维 / 熟练 ----------
  // 属性石：对应属性 +2，最多加到 20；那项属性超凡时不受限。
  public static int ScoreBonus(Hediff_Adventurer h,int a,int current){
   if(Live<=0 || h?.pawn==null || a<0 || a>5)return 0;long f=Flags(h.pawn);int b=0;
   if((f & ScoreFlags[a])!=0)b=Exalted.Has(h.pawn,a)?2:Mathf.Clamp(ScoreCapPlain-current,0,2);
   if(a==Adventurer.Str && (f & FVecnaHand)!=0)b=Mathf.Max(b,VecnaStr-current); // 维克那之手：力量至少 20
   return b;
  }
  // ---------- 1.114.0 维克那神器 ----------
  public const int VecnaStr=20,VecnaCurseHours=1440,KasFirstMin=3,KasFirstMax=7,KasGapDays=30;
  public const float VecnaCurseChance=.1f,KasSwordFactor=2f,VecnaCapacity=1.5f;
  public static bool Vecna(Pawn p)=>Has(p,FVecnaHand|FVecnaEye);
  public static bool VecnaWill(Pawn p)=>Live>0 && (Flags(p) & (FVecnaHand|FVecnaEye))==(FVecnaHand|FVecnaEye);
  public static bool ArtifactOn(Pawn p,BodyPartRecord part)=>Live>0 && part!=null && p.health.hediffSet.hediffs.Any(h=>h.Part==part && Ext(h.def)?.group=="Vecna");
  // 世上同一件神器只有一件：地图上没有这件东西、也没人装着时才会掉落。
  public static bool ArtifactFree(string def){
   var d=DefCache<ThingDef>.Get(def);if(d==null)return false;
   foreach(var m in Find.Maps)if(m.listerThings.ThingsOfDef(d).Count>0)return false;
   foreach(var p in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)if(p?.health?.hediffSet!=null && p.health.hediffSet.hediffs.Any(h=>h.def.spawnThingOnRemoved==d) || p?.inventory!=null && p.inventory.innerContainer.Any(t=>t.def==d))return false;
   return true;
  }
  public static string RollArtifact(float chance){if(!Rand.Chance(chance))return null;var l=new[]{"LP_Vecna_Hand","LP_Vecna_Eye"}.Where(ArtifactFree).ToList();return l.Count==0?null:l.RandomElement();}
  public static string FrostTouch(Pawn p,Pawn t){if(t==null || t.Dead)return null;t.TakeDamage(new DamageInfo(DefCache<DamageDef>.Get("LP_Frost")??DamageDefOf.Frostbite,30f,.4f,-1f,p));FleshImplants.Chill(t,.15f);Items.Mote(t,"寒冰触",new Color(.6f,.8f,1f));return "frost";}
  public static bool Command(Pawn p,Pawn t)=>t!=null && Items.Undead(t) && MindFlayerPowers.Dominate(p,t);
  // 叛徒卡斯：带着卡斯之剑来抢神器。
  public static Pawn KasRaid(Map map){
   if(map==null)return null;var f=Factions.Hostile??Find.FactionManager.RandomEnemyFaction(false,false,true,TechLevel.Industrial);if(f==null || !Events2.EdgeCell(map,out var entry))return null;
   var kind=DefCache<PawnKindDef>.Get("LP_Kas");if(kind==null)return null;
   var kas=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedGender:Gender.Male));
   kas.Name=new NameSingle("叛徒卡斯");kas.equipment?.DestroyAllEquipment();var sword=DefCache<ThingDef>.Get("LP_SwordOfKas");if(sword!=null)kas.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(sword));Items.Give(kas,"LP_KasBlood");
   var pawns=new List<Pawn>{kas};
   try{pawns.AddRange(PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,tile=map.Tile,faction=f,points=Mathf.Max(200f,StorytellerUtility.DefaultThreatPointsNow(map)*.6f)},false));}catch(Exception e){Log.Warning("[DND边缘大陆] 卡斯的随从生成失败："+e.Message);}
   foreach(var p in pawns)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,6),map);
   LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,true,false,false,false,false,false),map,pawns);
   Find.LetterStack.ReceiveLetter("叛徒卡斯","维克那的手和眼又聚到了一起——当年背叛并斩伤维克那的卡斯感觉到了。他带着卡斯之剑和一队人马杀了过来，要毁掉（或者夺走）神器。\n\n卡斯之剑对维克那神器的宿主伤害翻倍。打倒卡斯就能拿到这把剑。",LetterDefOf.ThreatBig,new LookTargets(kas),f);
   return kas;
  }
  public static int ProfBonus(Pawn p)=>Live>0 && Has(p,FMastery)?1:0;
  public static bool Alert(Pawn p)=>Live>0 && Has(p,FAlert);

  // ---------- 伤害 ----------
  public static float DamageFactor(Pawn v,DamageInfo d){
   float g=1f;var a=d.Instigator as Pawn;
   if(a!=null && a!=v && a.HostileTo(v) && ClassMartial2.Melee(d) && Has(a,FColdArm) && (ItemMaterials.ColdIronBane(v) || Fiends.Is(v)))g*=ColdIronFactor; // 寒铁义臂
   if(d.Weapon?.defName=="LP_SwordOfKas" && Vecna(v))g*=KasSwordFactor; // 卡斯之剑：对维克那神器宿主 ×2
   if(d.Weapon==null && Subclass2.IsSpell(d.Def) && Has(v,FAbsorb) && Absorb(v)){Items.Mote(v,"吸收之石",new Color(.7f,.6f,1f));return 0f;} // 吸收之石：每天抵消一次
   // 1.113.0 嫁接与血肉：骸骨肋笼 利器 ×0.8；龙鳞皮下甲 本色元素 ×0.5、对立元素 ×1.2；巨魔再生腺 受火 / 酸后再生停 12 小时；恶魔之臂 近战附加 5 火焰。
   if(Has(v,FBoneCage) && d.Def.armorCategory?.defName=="Sharp")g*=BoneCageSharp;
   if(Has(v,FScale)){int el=ElementOf(v,"Scale"),hit=Subclass3.DragonElementOf(d.Def);if(el>=0 && hit>=0){if(hit==el)g*=ScaleResist;else if(hit==Opposite[el])g*=ScaleWeak;}}
   if(Has(v,FTroll) && (d.Def==DamageDefOf.Flame || d.Def==DamageDefOf.Burn || Toxicish(d.Def)))Items.Give(v,"LP_GraftTrollStalled",GenDate.TicksPerHour*12);
   if(a!=null && a!=v && a.HostileTo(v) && ClassMartial2.Melee(d) && Has(a,FDemonArm) && !busy){var tv=v;var me=a;Fiends.Defer(()=>{if(tv.Dead || !tv.Spawned)return;busy=true;try{tv.TakeDamage(new DamageInfo(DamageDefOf.Flame,DemonFire,.1f,-1f,me));}finally{busy=false;}});}
   return g;
  }
  static bool busy;
  public const float BoneCageSharp=.8f,ScaleResist=.5f,ScaleWeak=1.2f,DemonFire=5f,BoneCageHealth=1.5f,BerserkChance=.01f,SoulFocus=.1f,DeadSpeakShare=.1f;
  public const int NecrosisHours=72,TrollRegrowHours=360;
  // 龙的元素：0 红·火焰，1 白·寒冰，2 蓝·闪电，3 黑·强酸，4 绿·毒素（和神龙宗的元素编号一致：火焰、寒冰、闪电、强酸、毒素）。
  public static readonly string[] ElementLabels={"火焰","寒冰","闪电","强酸","毒素"};
  public static readonly int[] Opposite={1,0,3,2,0};
  static bool Toxicish(DamageDef d)=>d!=null && (d.defName=="LP_OozeAcid" || d.defName=="LP_AlchemyAcid" || d.defName=="AcidBurn");
  public static int ElementOf(Pawn p,string flag){foreach(var h in p.health.hediffSet.hediffs){var e=Ext(h.def);if(e?.flag==flag)return e.element;}return -1;}
  // 骸骨肋笼：躯干最大生命 ×1.5（PartHealth.WithoutReinforcement）。
  public static float PartFactor(BodyPartRecord part,Pawn p)=>Live>0 && part!=null && part.parent==null && Has(p,FBoneCage)?BoneCageHealth:1f;
  static bool Absorb(Pawn v){
   var h=v.health.hediffSet.hediffs.FirstOrDefault(x=>Ext(x.def)?.flag=="Absorb");var c=(h as HediffWithComps)?.TryGetComp<HediffComp_DndImplant>();
   if(c==null || c.day==GenDate.DaysPassed+1)return false;c.day=GenDate.DaysPassed+1;return true;
  }
  // 精金义臂：这条手臂上（肩、臂、手、手指）受的伤 ×0.5。
  public static bool UnderAdamant(Pawn p,BodyPartRecord part){
   if(part==null || !Has(p,FAdamArm))return false;
   var roots=p.health.hediffSet.hediffs.Where(h=>Ext(h.def)?.flag=="AdamArm" && h.Part!=null).Select(h=>h.Part).ToList();
   for(var x=part;x!=null;x=x.parent)if(roots.Contains(x))return true;return false;
  }

  // ---------- 器械师调校、注能、再生 ----------
  public static float TuneBonus(Pawn p){int lv=ClassSystem.Level(p,artificer.Def);return Mathf.Min(TuneMax,TuneStep*(lv/4));}
  // 部位对应的能力（成对的部位按一半算）。
  public static PawnCapacityDef CapacityOf(BodyPartRecord part,out float weight){
   weight=.5f;for(var x=part;x!=null;x=x.parent){
    switch(x.def.defName){
     case "Shoulder":case "Arm":case "Hand":return PawnCapacityDefOf.Manipulation;
     case "Leg":case "Foot":return PawnCapacityDefOf.Moving;
     case "Eye":return PawnCapacityDefOf.Sight;
     case "Ear":return PawnCapacityDefOf.Hearing;
     case "Heart":weight=1f;return PawnCapacityDefOf.BloodPumping;
     case "Lung":return PawnCapacityDefOf.Breathing;
     case "Kidney":return PawnCapacityDefOf.BloodFiltration;
     case "Jaw":weight=1f;return PawnCapacityDefOf.Talking;
    }
   }
   return null;
  }
  public static void TuneTick(Pawn p){
   var parts=p.health.hediffSet.hediffs.Where(h=>Ext(h.def)?.flag!=null && h is Hediff_AddedPart && h.Part!=null).ToList();float b=TuneBonus(p);
   var tuned=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ArtificerTuned")) as Hediff_CapBonus;
   if(parts.Count==0 || b<=0f){if(tuned!=null)p.health.RemoveHediff(tuned);return;}
   var caps=new Dictionary<PawnCapacityDef,float>();foreach(var h in parts){var c=CapacityOf(h.Part,out float w);if(c==null)continue;caps.TryGetValue(c,out float v);caps[c]=v+b*w;}
   if(tuned==null)tuned=Items.Give(p,"LP_ArtificerTuned") as Hediff_CapBonus;tuned?.Set(caps);
  }
  // 巨魔再生腺：长回一个缺失的小部位（覆盖面积最小的那个）。
  public static BodyPartRecord RegrowSmall(Pawn p){
   var miss=p.health.hediffSet.GetMissingPartsCommonAncestors().Where(x=>x.Part!=null && x.Part.parent!=null).OrderBy(x=>x.Part.coverageAbsWithChildren).FirstOrDefault();
   if(miss==null)return null;var part=miss.Part;p.health.RestorePart(part,null,true);Items.Mote(p,"再生腺：长回"+part.Label,new Color(.5f,.8f,.5f));return part;
  }
  public static readonly string[] RayModes={"死灵射线","石化射线","魅惑射线"};
  public static int RayMode(Pawn p){foreach(var h in p.health.hediffSet.hediffs)if(Ext(h.def)?.flag=="Beholder"){var c=(h as HediffWithComps)?.TryGetComp<HediffComp_DndImplant>();if(c!=null)return Mathf.Clamp(c.mode,0,2);}return 0;}
  // 眼魔之眼射线：死灵 30 坏死伤害 / 开始石化 / 魅惑（恍惚 25 秒）。
  public static string Ray(Pawn p,Pawn t){
   if(t==null || t.Dead)return null;int m=RayMode(p);
   if(m==0){t.TakeDamage(new DamageInfo(DefCache<DamageDef>.Get("LP_Necrotic")??DamageDefOf.Burn,30f,.3f,-1f,p));Items.Mote(t,RayModes[0],new Color(.5f,.8f,.4f));return RayModes[0];}
   if(m==1){bool ok=ItemScrolls.StartPetrify(p,t);if(!ok)Items.Mote(t,"石化无效",Color.gray);return ok?RayModes[1]:null;}
   var r=ClassCasters.Charm(p,t,1500,10f);return r=="charmed" || r=="prisoner"?RayModes[2]:null;
  }
  // 龙息腺：锥形 6 格，敌人受 30 点龙心颜色的元素伤害。
  public static int Breath(Pawn p,IntVec3 target){
   var map=p.Map;if(map==null || !target.IsValid)return 0;int el=Mathf.Max(0,ElementOf(p,"Breath"));var def=el==0?DamageDefOf.Flame:DefCache<DamageDef>.Get(new[]{"Flame","LP_Frost","LP_Lightning","LP_OozeAcid","LP_DragonPoison"}[el])??DamageDefOf.Flame;
   var cells=Dragons.ShapeCells(map,p.Position,target,6f,false,Dragons.ConeHalfAngle,p.Rotation);int n=0;var hit=new HashSet<Pawn>();
   foreach(var c in cells){FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,.8f,new Color(1f,.7f,.4f));foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList())if(t!=p && !t.Dead && t.HostileTo(p) && hit.Add(t)){t.TakeDamage(new DamageInfo(def,30f,.2f,-1f,p));n++;}}
   Items.Mote(p,"龙息·"+ElementLabels[el],new Color(1f,.7f,.4f));return n;
  }
  // 亡者之语：从人形尸体学到它最高那项技能总经验的 10%（每具尸体一次）。
  public static string DeadSpeak(Pawn p,Corpse c){
   var dead=c?.InnerPawn;var s=GameComp<GameComponent_DndImplants>.Get();if(dead?.skills==null || p.skills==null || s==null || !s.spoken.Add(dead.thingIDNumber))return null;
   var best=dead.skills.skills.Where(x=>!x.TotallyDisabled).OrderByDescending(x=>x.XpTotalEarned+x.xpSinceLastLevel).FirstOrDefault();if(best==null)return null;
   var mine=p.skills.GetSkill(best.def);if(mine==null || mine.TotallyDisabled)return null;float xp=(best.XpTotalEarned+best.xpSinceLastLevel)*DeadSpeakShare;mine.Learn(xp,true);
   Items.Mote(p,"亡者之语："+best.def.label+" +"+xp.ToString("0"),new Color(.6f,.8f,.7f));return best.def.defName;
  }
  public static Hediff_Injury Regen(Pawn p){
   var inj=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>!x.IsPermanent()).OrderByDescending(x=>x.Severity).FirstOrDefault();
   if(inj!=null){inj.Heal(inj.Severity);Items.Mote(p,"再生之石",new Color(.6f,.9f,.6f));}return inj;
  }
  public static bool Infusable(Pawn p,Hediff h)=>h is Hediff_AddedPart && h.Part!=null && Ext(h.def)==null && h.def.addedPartProps!=null && !p.health.hediffSet.hediffs.Any(x=>x.Part==h.Part && x.def.defName=="LP_Infused");
  public static Hediff Infuse(Pawn p,BodyPartRecord part){
   var def=Items.Hediff("LP_Infused");if(def==null || part==null)return null;var h=(Hediff_CapBonus)HediffMaker.MakeHediff(def,p,part);
   var c=CapacityOf(part,out float w);if(c!=null)h.Set(new Dictionary<PawnCapacityDef,float>{{c,InfuseBonus*w}});p.health.AddHediff(h,part);return h;
  }
 }

 // 植入物共用的组件：登记数量、作废缓存、每小时的调校 / 再生。
 public class HediffCompProperties_DndImplant : HediffCompProperties {public HediffCompProperties_DndImplant(){compClass=typeof(HediffComp_DndImplant);}}
 public class HediffComp_DndImplant : HediffComp {
  public int day,acc,regen,mode=-1,necro,hours;
  public override void CompExposeData(){base.CompExposeData();Scribe_Values.Look(ref day,"lpDay");Scribe_Values.Look(ref acc,"lpAcc");Scribe_Values.Look(ref regen,"lpRegen");Scribe_Values.Look(ref mode,"lpMode",-1);Scribe_Values.Look(ref necro,"lpNecro");Scribe_Values.Look(ref hours,"lpHours");}
  public override void CompPostPostAdd(DamageInfo? dinfo){
   base.CompPostPostAdd(dinfo);DndImplants.Live++;DndImplants.Dirty(Pawn);var e=DndImplants.Ext(parent.def);
   if(e?.flag=="Beholder" && mode<0)mode=Rand.RangeInclusive(0,2); // 眼魔之眼：死灵 / 石化 / 魅惑，装上时定下
   if(e!=null && Pawn.abilities!=null)foreach(var ab in e.Abilities)if(Pawn.abilities.GetAbility(ab)==null)Pawn.abilities.GainAbility(ab);
  }
  public override void CompPostPostRemoved(){
   base.CompPostPostRemoved();if(DndImplants.Live>0)DndImplants.Live--;DndImplants.Dirty(Pawn);if(parent.def.addedPartProps!=null)DndImplants.TuneTick(Pawn);
   if(DndImplants.Ext(parent.def)?.group=="Vecna" && !Pawn.Dead){var th=DefCache<ThoughtDef>.Get("LP_VecnaLoss");if(th!=null)Pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(th);} // 截下神器：心情 −15，30 天
   var ex=DndImplants.Ext(parent.def);if(ex!=null)foreach(var ab in ex.Abilities.ToList())if(Pawn.abilities?.GetAbility(ab)!=null && !Pawn.health.hediffSet.hediffs.Any(h=>h!=parent && DndImplants.Ext(h.def)?.Abilities.Contains(ab)==true))Pawn.abilities.RemoveAbility(ab);
  }
  public override string CompLabelInBracketsExtra=>mode>=0?DndImplants.RayModes[Mathf.Clamp(mode,0,2)]:null;
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){
   base.CompPostTickInterval(ref severityAdjustment,delta);if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;var p=Pawn;if(p==null || p.Dead)return;var e=DndImplants.Ext(parent.def);
   if(parent is Hediff_AddedPart)DndImplants.TuneTick(p);
   if(e?.flag=="Regen" && (regen+=1)>=24){regen=0;DndImplants.Regen(p);}
   // 嫁接物在巫妖（没有活的血肉）身上 3 天坏死。
   if(e!=null && e.graft && Items.IsLich(p) && (necro+=1)>=DndImplants.NecrosisHours){Messages.Message(p.LabelShort+"身上的"+parent.LabelBase+"没有活的血肉供养，坏死脱落了。",p,MessageTypeDefOf.NegativeHealthEvent,false);p.health.RemoveHediff(parent);return;}
   hours++;
   if(e?.flag=="DemonArm" && hours%24==0 && Rand.Chance(DndImplants.BerserkChance) && p.Spawned && !p.Downed)p.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Berserk,"恶魔之臂的狂怒",true);
   if(e?.flag=="Troll" && !Items.Has(p,"LP_GraftTrollStalled") && hours>=DndImplants.TrollRegrowHours){hours=0;DndImplants.RegrowSmall(p);}
   // 维克那之手：每 60 天 10%（巫妖 5%）陷入“邪恶意志”的杀戮狂暴。
   if(e?.flag=="VecnaHand" && hours%DndImplants.VecnaCurseHours==0 && Rand.Chance(DndImplants.VecnaCurseChance*(Items.IsLich(p)?.5f:1f)) && p.Spawned && !p.Downed){p.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Berserk,"维克那的邪恶意志",true);Messages.Message("维克那的邪恶意志占据了"+p.LabelShort+"。",p,MessageTypeDefOf.ThreatBig,false);}
  }
 }
 // 按能力加点的状态（器械师调校、注能）：能力加成存在严重度之外，现算一个阶段。
 public class Hediff_CapBonus : HediffWithComps {
  public Dictionary<PawnCapacityDef,float> caps=new Dictionary<PawnCapacityDef,float>();HediffStage stage;
  public void Set(Dictionary<PawnCapacityDef,float> c){
   if(c.Count==caps.Count && c.All(kv=>caps.TryGetValue(kv.Key,out float v) && Mathf.Approximately(v,kv.Value)))return;
   caps=new Dictionary<PawnCapacityDef,float>(c);stage=null;pawn?.health?.Notify_HediffChanged(this);
  }
  public override HediffStage CurStage=>stage??(stage=new HediffStage{capMods=caps.Select(kv=>new PawnCapacityModifier{capacity=kv.Key,offset=kv.Value}).ToList()});
  public override string LabelInBrackets=>string.Join("，",caps.Select(kv=>kv.Key.LabelCap+" +"+kv.Value.ToStringPercent()));
  public override void ExposeData(){
   base.ExposeData();Scribe_Collections.Look(ref caps,"lpCaps",LookMode.Def,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){caps=caps??new Dictionary<PawnCapacityDef,float>();stage=null;}
  }
  // 注能：所在的义体被拆掉就跟着消失。
  public override bool ShouldRemove=>base.ShouldRemove || def.defName=="LP_Infused" && (Part==null || !pawn.health.hediffSet.hediffs.Any(h=>h is Hediff_AddedPart && h.Part==Part));
 }

 // ---------- 手术 ----------
 public class Recipe_InstallDndPart : Recipe_InstallArtificialBodyPart {
  public override bool AvailableOnNow(Thing thing,BodyPartRecord part=null)=>base.AvailableOnNow(thing,part) && DndImplants.CanAttune(thing as Pawn,recipe.addsHediff)==null;
  public override void ApplyOnPawn(Pawn pawn,BodyPartRecord part,Pawn billDoer,List<Thing> ingredients,Bill bill){if(DndRecipe.Refuse(pawn,recipe))return;base.ApplyOnPawn(pawn,part,billDoer,ingredients,bill);}
 }
 // 维克那之手 / 之眼：只装在左边；维克那之手要先截掉自己的左手。
 public class Recipe_InstallVecna : Recipe_InstallDndPart {
  public static bool Left(BodyPartRecord part)=>part?.untranslatedCustomLabel!=null && part.untranslatedCustomLabel.IndexOf("left",StringComparison.OrdinalIgnoreCase)>=0;
  public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn,RecipeDef recipe){
   var set=pawn.health.hediffSet;bool hand=recipe.addsHediff?.defName=="LP_Vecna_Hand";
   foreach(var part in pawn.RaceProps.body.AllParts){
    if(!Left(part) || !recipe.appliedOnFixedBodyParts.Contains(part.def))continue;
    if(hand){if(set.PartIsMissing(part) && (part.parent==null || !set.PartIsMissing(part.parent)) && !set.hediffs.Any(h=>h is Hediff_AddedPart && h.Part==part))yield return part;}
    else if(!set.PartIsMissing(part) || part.parent==null || !set.PartIsMissing(part.parent))yield return part;
   }
  }
 }
 public class Recipe_InstallDndImplant : Recipe_InstallImplant {
  public override bool AvailableOnNow(Thing thing,BodyPartRecord part=null)=>base.AvailableOnNow(thing,part) && DndImplants.CanAttune(thing as Pawn,recipe.addsHediff)==null;
  public override void ApplyOnPawn(Pawn pawn,BodyPartRecord part,Pawn billDoer,List<Thing> ingredients,Bill bill){if(DndRecipe.Refuse(pawn,recipe))return;base.ApplyOnPawn(pawn,part,billDoer,ingredients,bill);}
 }
 public static class DndRecipe {
  // 开刀时调谐位已经满了（排了多张手术单）：不装，把植入物还回来。
  public static bool Refuse(Pawn pawn,RecipeDef recipe){
   var why=DndImplants.CanAttune(pawn,recipe.addsHediff);if(why==null)return false;
   Messages.Message(pawn.LabelShort+"装不上"+recipe.addsHediff.label+"："+why+"。",pawn,MessageTypeDefOf.RejectInput,false);
   var item=recipe.addsHediff.spawnThingOnRemoved;if(item!=null && pawn.MapHeld!=null)GenPlace.TryPlaceThing(ThingMaker.MakeThing(item),pawn.PositionHeld,pawn.MapHeld,ThingPlaceMode.Near);
   return true;
  }
 }
 // 器械师注能：给原版仿生 / 考古科技部件注能，效率 +10%（按部位折算到能力）。
 public class Recipe_InfuseBionic : Recipe_Surgery {
  public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn,RecipeDef recipe){foreach(var h in pawn.health.hediffSet.hediffs.ToList())if(DndImplants.Infusable(pawn,h))yield return h.Part;}
  public override void ApplyOnPawn(Pawn pawn,BodyPartRecord part,Pawn billDoer,List<Thing> ingredients,Bill bill){
   if(billDoer!=null && CheckSurgeryFail(billDoer,pawn,ingredients,part,bill))return;
   if(!pawn.health.hediffSet.hediffs.Any(h=>h.Part==part && DndImplants.Infusable(pawn,h)))return;
   DndImplants.Infuse(pawn,part);
  }
 }

 public class GameComponent_DndImplants : GameComponent {
  public HashSet<int> spoken=new HashSet<int>(); // 亡者之语用过的尸体（按原小人 thingIDNumber）
  public int kasNext=-1; // 下一次卡斯来袭（维克那的意志：两件神器装在同一个殖民者身上）
  public GameComponent_DndImplants(Game game){}
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref spoken,"spoken",LookMode.Value);Scribe_Values.Look(ref kasNext,"kasNext",-1);if(Scribe.mode==LoadSaveMode.PostLoadInit && spoken==null)spoken=new HashSet<int>();}
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;if(now%GenDate.TicksPerHour!=37 || DndImplants.Live<=0)return;
   var holder=PawnsFinder.AllMaps_FreeColonistsSpawned.FirstOrDefault(DndImplants.VecnaWill);
   if(holder==null){kasNext=-1;return;}
   if(kasNext<0){kasNext=now+Rand.RangeInclusive(DndImplants.KasFirstMin,DndImplants.KasFirstMax)*GenDate.TicksPerDay;return;}
   if(now>=kasNext){kasNext=now+DndImplants.KasGapDays*GenDate.TicksPerDay;DndImplants.KasRaid(holder.Map);}
  }
  public override void FinalizeInit(){base.FinalizeInit();DndImplants.Reset();}
 }

 // ---------- 植入物带来的能力：眼魔射线、龙息、亡者之语 ----------
 public class CompProperties_GraftAbility : CompProperties_AbilityEffect {public string effect;public CompProperties_GraftAbility(){compClass=typeof(CompAbilityEffect_Graft);}}
 public class CompAbilityEffect_Graft : CompAbilityEffect {
  public new CompProperties_GraftAbility Props=>(CompProperties_GraftAbility)props;
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   if(!base.Valid(target,throwMessages))return false;string why=null;var p=parent.pawn;
   switch(Props.effect){
    case "ray":case "frost":if(target.Pawn==null || target.Pawn.Dead || !target.Pawn.HostileTo(p))why="需要选择一个敌人。";break;
    case "command":if(target.Pawn==null || target.Pawn.Dead || !target.Pawn.HostileTo(p) || !Items.Undead(target.Pawn))why="需要选择一个敌方亡灵。";break;
    case "dominate":why=MindFlayerPowers.CannotDominate(p,target.Pawn);break;
    case "breath":if(!target.Cell.IsValid || p.Map==null || !target.Cell.InBounds(p.Map))why="需要选择一个方向。";break;
    case "speak":{var c=target.Thing as Corpse;var s=GameComp<GameComponent_DndImplants>.Get();if(c?.InnerPawn==null || !c.InnerPawn.RaceProps.Humanlike || c.InnerPawn.skills==null)why="需要一具人形尸体。";else if(s!=null && s.spoken.Contains(c.InnerPawn.thingIDNumber))why="这具尸体已经说过话了。";break;}
   }
   if(why!=null && throwMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);return why==null;
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   base.Apply(target,dest);var p=parent.pawn;
   switch(Props.effect){case "frost":DndImplants.FrostTouch(p,target.Pawn);break;case "command":if(!DndImplants.Command(p,target.Pawn))Items.Mote(target.Pawn,"没有听令",Color.gray);break;case "dominate":MindFlayerPowers.Dominate(p,target.Pawn);break;case "ray":DndImplants.Ray(p,target.Pawn);break;case "breath":DndImplants.Breath(p,target.Cell);break;case "speak":DndImplants.DeadSpeak(p,target.Thing as Corpse);break;}
  }
 }
 // 别人对装着这件植入物的人的看法（眼魔之眼 −10、龙息腺 −5）。
 public class ThoughtWorker_GraftOther : ThoughtWorker {
  protected override ThoughtState CurrentSocialStateInternal(Pawn p,Pawn other)=>DndImplants.Live>0 && other!=null && other!=p && def.hediff!=null && p.RaceProps.Humanlike && other.RaceProps.Humanlike && other.health.hediffSet.HasHediff(def.hediff)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }
 // 维克那的意志：两件神器都装在巫妖身上、并且有死灵之躯时，命匣复活 3 天 → 1 天。
 [HarmonyPatch(typeof(Phylactery),nameof(Phylactery.ScheduleRevival))]
 public static class DndImplantVecnaRevival {
  static void Postfix(Phylactery __instance){var s=__instance.soul;if(__instance.revivalTick<0 || s==null || !DndImplants.VecnaWill(s) || !NecromancyGenes.HasBody(s))return;__instance.revivalTick=Math.Min(__instance.revivalTick,Find.TickManager.TicksGame+GenDate.TicksPerDay);}
 }
 // 吞魂之颌：咬死活物时回复 10% 精神力。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class DndImplantSoulJaw {
  static void Postfix(Pawn __instance,DamageInfo? dinfo){
   if(DndImplants.Live<=0 || !__instance.Dead || dinfo==null || !__instance.RaceProps.IsFlesh)return;var k=dinfo.Value.Instigator as Pawn;
   if(k?.psychicEntropy==null || dinfo.Value.WeaponLinkedHediff?.defName!="LP_Flesh_SoulJaw")return;
   k.psychicEntropy.OffsetPsyfocusDirectly(DndImplants.SoulFocus);Items.Mote(k,"吞魂",new Color(.6f,.8f,.7f));
  }
 }

 // ==================== Harmony ====================
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class DndImplantDamage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(DndImplants.Live<=0 || dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   float f=DndImplants.DamageFactor(__instance,dinfo);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
 [HarmonyPatch(typeof(DamageWorker_AddInjury),"FinalizeAndAddInjury",new[]{typeof(Pawn),typeof(Hediff_Injury),typeof(DamageInfo),typeof(DamageWorker.DamageResult)})]
 public static class DndImplantAdamantPart {
  static void Prefix(Pawn pawn,Hediff_Injury injury){if(DndImplants.Live>0 && injury!=null && DndImplants.UnderAdamant(pawn,injury.Part))injury.Severity*=DndImplants.AdamantPartFactor;}
 }
 // 精金义臂：近战护甲穿透 +25%。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply")]
 public static class DndImplantAdamantPen {
  static readonly FieldInfo pen=AccessTools.Field(typeof(DamageInfo),"armorPenetrationInt");
  static void Postfix(Verb_MeleeAttackDamage __instance,ref IEnumerable<DamageInfo> __result){if(DndImplants.Live>0 && DndImplants.Has(__instance.CasterPawn,DndImplants.FAdamArm))__result=Wrap(__result);}
  static IEnumerable<DamageInfo> Wrap(IEnumerable<DamageInfo> src){foreach(var d in src){object box=d;pen.SetValue(box,d.ArmorPenetrationInt+DndImplants.AdamantPen);yield return (DamageInfo)box;}}
 }
 // 秘银义腿：衣物的移速惩罚减半。
 [HarmonyPatch(typeof(StatWorker),nameof(StatWorker.StatOffsetFromGear))]
 public static class DndImplantMithralLeg {
  static void Postfix(Thing gear,StatDef stat,ref float __result){if(__result<0f && DndImplants.Live>0 && stat==StatDefOf.MoveSpeed && gear is Apparel ap && DndImplants.Has(ap.Wearer,DndImplants.FMithralLeg))__result*=.5f;}
 }
 // 精工机关眼、维克那之眼：光照不足时没有惩罚。
 [HarmonyPatch(typeof(StatPart_Glow),"ActiveFor")]
 public static class DndImplantClockEye {
  static void Postfix(Thing t,ref bool __result){if(__result && DndImplants.Live>0 && t is Pawn p && DndImplants.Has(p,DndImplants.FClockEye|DndImplants.FVecnaEye))__result=false;}
 }
 // 器械师主刀装 DND 植入物、给义体注能：成功率 +15%。
 [HarmonyPatch(typeof(SurgeryOutcomeEffectDef),nameof(SurgeryOutcomeEffectDef.GetQuality))]
 public static class DndImplantSurgeon {
  static readonly DefRef<LP_ClassDef> artificer=new DefRef<LP_ClassDef>("LP_Class_Artificer");
  static void Postfix(RecipeDef recipe,Pawn surgeon,ref float __result){if(recipe!=null && surgeon!=null && (DndImplants.Ext(recipe.addsHediff)!=null || recipe.HasModExtension<ArtificerSurgeryExtension>()) && ClassSystem.Level(surgeon,artificer.Def)>0)__result=Mathf.Min(Mathf.Max(__result,.98f),__result+DndImplants.SurgeonBonus);}
 }
 // 注能手术只有器械师能做。
 [HarmonyPatch(typeof(RecipeDef),nameof(RecipeDef.PawnSatisfiesSkillRequirements))]
 public static class ArtificerSurgeryGate {
  static readonly DefRef<LP_ClassDef> artificer=new DefRef<LP_ClassDef>("LP_Class_Artificer");
  static void Postfix(RecipeDef __instance,Pawn pawn,ref bool __result){if(__result && pawn!=null && __instance.HasModExtension<ArtificerSurgeryExtension>() && ClassSystem.Level(pawn,artificer.Def)<=0)__result=false;}
 }
}
