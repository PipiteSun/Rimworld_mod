using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.38.0 流浆人（DND Plasmoid）：异种型+基因，不走 HAR。种族判定按本源 LP_OozeCore 激活（Is 带缓存，基因变化时清掉）。
 // 原生质条照原版 Gene_Resource（0～1，×100 显示）；五个能力共用 CompOozeAbility，消耗都在 Oozefolk 的静态方法里扣。
 public static class Oozefolk {
  public const float Max=100f,StartPoints=30f,MealGain=3f,BandMaxBonus=20f;
  public const float HumanGain=25f,HumanFood=.5f,AnimalPerSize=15f,AnimalGainCap=30f,AnimalFoodPerSize=.3f,AnimalFoodCap=.6f,RotFactor=.5f;
  public const float FissionCost=80f,FissionFood=.25f,SeepCost=10f,ReshapeCost=40f,ExtractCost=25f;
  public const float AcidFactor=.25f,BleedFactor=.5f,ColdFactor=1.5f,ManipulationFloor=.6f,PrisonBreakFactor=2f,ApparelAcidFactor=2f;
  public const float SluggishTemp=0f,BandSluggishTemp=-15f,WitnessRange=15f;
  public const int SeepRange=3,SluggishTicks=2500,ButcherGel=3;
  public static readonly string[] SkinNames={"LP_SkinOozeGreen","LP_SkinOozeBlue","LP_SkinOozeAmber","LP_SkinOozeViolet"};
  // 重塑能恢复的外部部位（大的优先）：不含眼睛和内脏。
  public static readonly string[] ReshapeParts={"Hand","Foot","Finger","Toe","Ear","Nose"};

  public static GeneDef Core=>Races124.Gene("LP_OozeCore");
  public static GeneDef ProtoGene=>Races124.Gene("LP_OozeProtoplasm");
  public static GeneDef DigestGene=>Races124.Gene("LP_OozeDigest");
  public static GeneDef PseudopodGene=>Races124.Gene("LP_OozePseudopod");
  public static GeneDef AmorphousGene=>Races124.Gene("LP_OozeAmorphous");
  public static GeneDef SplitGene=>Races124.Gene("LP_OozeSplit");
  public static GeneDef ChillGene=>Races124.Gene("LP_OozeChill");
  public static GeneDef DesiccateGene=>Races124.Gene("LP_OozeDesiccate");
  public static XenotypeDef Xenotype=>Races124.Xeno("LP_Oozefolk");
  public static AbilityDef Engulfing=>Races124.Ability("LP_OozeEngulf");
  public static AbilityDef Fissioning=>Races124.Ability("LP_OozeFission");
  public static AbilityDef Seeping=>Races124.Ability("LP_OozeSeep");
  public static ThingDef Gel=>Items.Thing("LP_ProtoplasmGel");
  public static ThingDef Band=>Items.Thing("LP_StabilizerBand");
  static DamageDef acid;static ThoughtDef witnessed;static List<GeneDef> skins;
  public static DamageDef Acid=>acid??(acid=DefDatabase<DamageDef>.GetNamedSilentFail("LP_OozeAcid"));
  public static ThoughtDef Witnessed=>witnessed??(witnessed=DefDatabase<ThoughtDef>.GetNamed("LP_WitnessedEngulf"));
  public static List<GeneDef> Skins=>skins??(skins=SkinNames.Select(n=>DefDatabase<GeneDef>.GetNamedSilentFail(n)).Where(g=>g!=null).ToList());

  // ---------- 种族判定（缓存；属性、寻路等钩子可能不在主线程） ----------
  static readonly System.Collections.Concurrent.ConcurrentDictionary<int,bool> cache=new System.Collections.Concurrent.ConcurrentDictionary<int,bool>();
  public static bool Is(Pawn p){
   if(p?.genes==null)return false;
   if(cache.TryGetValue(p.thingIDNumber,out bool v))return v;
   v=Core!=null && p.genes.HasActiveGene(Core);cache[p.thingIDNumber]=v;return v;
  }
  public static void Invalidate(Pawn p){if(p!=null)cache.TryRemove(p.thingIDNumber,out _);}
  public static void ClearCache()=>cache.Clear();
  public static bool Has(Pawn p,GeneDef gene)=>CommonRaces.Has(p,gene);

  // ---------- 原生质 ----------
  public static Gene_OozeProtoplasm Proto(Pawn p){var g=ProtoGene==null?null:p?.genes?.GetGene(ProtoGene) as Gene_OozeProtoplasm;return g!=null && g.Active?g:null;}
  public static float Points(Pawn p)=>(Proto(p)?.Value??0f)*Max;
  public static float MaxPoints(Pawn p)=>(Proto(p)?.Max??0f)*Max;
  public static void Gain(Pawn p,float points){var g=Proto(p);if(g!=null)g.Value=Mathf.Min(g.Max,g.Value+points/Max);}
  public static void SetPoints(Pawn p,float points){var g=Proto(p);if(g!=null)g.Value=Mathf.Clamp(points/Max,0f,g.Max);}
  public static bool CanPay(Pawn p,float points)=>Points(p)+.001f>=points;
  public static bool Pay(Pawn p,float points){var g=Proto(p);if(g==null || !CanPay(p,points))return false;g.Value=Mathf.Max(0f,g.Value-points/Max);return true;}
  public static string NotEnough(float cost)=>"原生质不足（需要"+cost.ToString("0")+"）";
  // 稳形护带只对流浆人有效。
  public static bool BandWorn(Pawn p){
   var band=Band;var worn=p?.apparel?.WornApparel;if(band==null || worn==null || !Is(p))return false;
   for(int i=0;i<worn.Count;i++)if(worn[i].def==band)return true;
   return false;
  }
  public static void Mote(Thing t,string text)=>MindFlayer.Mote(t,text,new Color(.6f,.9f,.55f));
  static void Ooze(Map map,IntVec3 c,int n){
   if(map==null || !c.IsValid)return;
   for(int i=0;i<n;i++)FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,1f,new Color(.55f,.8f,.5f));
   FilthMaker.TryMakeFilth(c,map,ThingDefOf.Filth_Slime);
  }

  // ---------- 伤害 ----------
  public static bool IsAcid(DamageDef def)=>def!=null && (def.defName.IndexOf("Acid",StringComparison.OrdinalIgnoreCase)>=0 || (def.hediff!=null && def.hediff.defName.IndexOf("Acid",StringComparison.OrdinalIgnoreCase)>=0));
  public static bool IsCold(DamageDef def){
   if(def==null)return false;var n=def.defName;
   return def==DamageDefOf.Frostbite || n.IndexOf("Frost",StringComparison.OrdinalIgnoreCase)>=0 || n.IndexOf("Cold",StringComparison.OrdinalIgnoreCase)>=0 || n.IndexOf("Freez",StringComparison.OrdinalIgnoreCase)>=0;
  }
  // 本模组加在原版之上的倍率（酸蚀、寒冷）；基因里的 damageFactors（钝击、火焰）由原版结算。
  public static float ExtraDamageFactor(Pawn p,DamageDef def){
   float f=1f;if(p==null || def==null)return f;
   if(IsAcid(def) && Is(p))f*=AcidFactor;
   if(IsCold(def) && Has(p,ChillGene))f*=ColdFactor;
   return f;
  }
  // 总倍率（报告和自测用）：原版基因倍率×本模组倍率。
  public static float DamageFactor(Pawn p,DamageDef def)=>(p?.genes?.FactorForDamage(new DamageInfo(def,1f))??1f)*ExtraDamageFactor(p,def);
  public static readonly string[] ColdHediffs={"LP_FrostSlow","LP_FrostStaffChill","LP_Chilled"};
  // 寒霜迟缓、寒霜缠身、寒气侵体的持续时间×150%。
  public static void StretchCold(Pawn p,Hediff h){
   if(h==null || !Has(p,ChillGene) || !ColdHediffs.Contains(h.def.defName))return;
   var timer=(h as HediffWithComps)?.TryGetComp<HediffComp_Disappears>();
   if(timer!=null)timer.ticksToDisappear=Mathf.RoundToInt(timer.ticksToDisappear*ColdFactor);
  }

  // ---------- 凝滞 ----------
  public static float SluggishThreshold(Pawn p)=>BandWorn(p)?BandSluggishTemp:SluggishTemp;
  // 所在处低于阈值：加上（或刷新为1小时的）凝滞；回暖后1小时自然消失。temp 只给自测用。
  public static bool SyncChill(Pawn p,float? temp=null){
   if(p==null || p.Dead || !Has(p,ChillGene))return false;
   float t=temp??p.AmbientTemperature;
   if(t>=SluggishThreshold(p))return false;
   return Items.Give(p,"LP_OozeSluggish",SluggishTicks)!=null;
  }

  // ---------- 吞没 ----------
  public static string CannotEngulf(Pawn caster,Thing t){
   if(!(t is Corpse c) || c.InnerPawn==null)return "需要一具尸体。";
   var p=c.InnerPawn;
   if(p.RaceProps.IsMechanoid)return "机械体不能吞没。";
   if(!p.RaceProps.IsFlesh)return "这具尸体不是血肉之躯。";
   if(Necro.Undead(p) || NecromancyGenes.HasBody(p))return "巫妖与死灵造物的尸体不能吞没。";
   if(Is(p))return "不能吞没同族流浆人的尸体。";
   if(c.GetRotStage()==RotStage.Dessicated)return "干尸里已经没有可以消化的东西了。";
   return null;
  }
  // 人形+25、饱食+50%；动物 体型×15（≤30）、体型×30%（≤60%）；开始腐烂减半。
  public static float EngulfValue(Corpse c,out float food){
   var p=c.InnerPawn;float v;
   if(p.RaceProps.Humanlike){v=HumanGain;food=HumanFood;}
   else{v=Mathf.Min(AnimalGainCap,p.BodySize*AnimalPerSize);food=Mathf.Min(AnimalFoodCap,p.BodySize*AnimalFoodPerSize);}
   if(c.GetRotStage()==RotStage.Rotting){v*=RotFactor;food*=RotFactor;}
   return v;
  }
  // 目击者：15格内看得见、醒着的殖民者（食人者、精神病态、流浆人不在乎）。
  public static List<Pawn> EngulfWitnesses(Pawn caster,IntVec3 at,Map map){
   var list=new List<Pawn>();var cannibal=DefDatabase<TraitDef>.GetNamedSilentFail("Cannibal");
   foreach(var w in map.mapPawns.FreeColonistsSpawned){
    if(w==caster || w.Dead || w.needs?.mood==null || !w.Awake() || Is(w))continue;
    if(w.story?.traits!=null && ((cannibal!=null && w.story.traits.HasTrait(cannibal)) || w.story.traits.HasTrait(TraitDefOf.Psychopath)))continue;
    if(!w.Position.InHorDistOf(at,WitnessRange) || !GenSight.LineOfSight(w.Position,at,map,true))continue;
    list.Add(w);
   }
   return list;
  }
  // 返回获得的原生质；-1 表示不能吞没。衣物、武器和背包掉在原地，尸体消失。
  public static float Engulf(Pawn caster,Corpse c){
   if(caster==null || CannotEngulf(caster,c)!=null)return -1f;
   var p=c.InnerPawn;bool human=p.RaceProps.Humanlike;float v=EngulfValue(c,out float food);
   var map=c.MapHeld;var at=c.PositionHeld;
   var witnesses=human && map!=null?EngulfWitnesses(caster,at,map):new List<Pawn>();
   if(c.AnythingToStrip())c.Strip(true);
   c.Destroy();
   Gain(caster,v);
   var need=caster.needs?.food;if(need!=null)need.CurLevel=Mathf.Min(need.MaxLevel,need.CurLevel+need.MaxLevel*food);
   foreach(var w in witnesses)w.needs.mood.thoughts.memories.TryGainMemory(Witnessed,caster);
   // 吞没人形尸体算吃人肉：记原版历史事件（意识形态的戒律由此生效）；没开意识形态时给原版“吃了人肉”的心情。
   if(human){
    if(ModsConfig.IdeologyActive){
     Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.AteHumanMeat,caster.Named(HistoryEventArgsNames.Doer)));
     Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.AteHumanMeatDirect,caster.Named(HistoryEventArgsNames.Doer)));
    }else{
     var cannibal=DefDatabase<TraitDef>.GetNamedSilentFail("Cannibal");
     var thought=DefDatabase<ThoughtDef>.GetNamedSilentFail(cannibal!=null && caster.story?.traits?.HasTrait(cannibal)==true?"AteHumanlikeMeatDirectCannibal":"AteHumanlikeMeatDirect");
     if(thought!=null)caster.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
    }
   }
   Ooze(map,at,3);
   Mote(caster,"吞没 +"+v.ToString("0")+" 原生质");
   return v;
  }

  // ---------- 分裂 ----------
  public static string CannotFission(Pawn p){
   if(p==null || p.Dead)return "已经死了。";
   if(!p.DevelopmentalStage.Adult())return "只有成年流浆人能分裂。";
   if(!p.Spawned)return "只能在地图上分裂。";
   var food=p.needs?.food;if(food!=null && food.CurLevelPercentage+.0001f<FissionFood)return "饱食度不足（需要25%）。";
   if(!CanPay(p,FissionCost))return NotEnough(FissionCost);
   return null;
  }
  public static GeneDef RandomSkin(int seed){var list=Skins;return list.Count==0?null:list[Mathf.Clamp((int)(Rand.ValueSeeded(seed)*list.Count),0,list.Count-1)];}
  // 生下一个新生儿：同异种型、复制父体全部内源基因（外源不复制）、肤色随机、父体是唯一的亲代、同派系。
  public static Pawn Fission(Pawn parent){
   if(CannotFission(parent)!=null)return null;
   var faction=parent.Faction;var last=(parent.Name as NameTriple)?.Last;
   var req=new PawnGenerationRequest(PawnKindDefOf.Colonist,faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDowned:true,canGeneratePawnRelations:false,allowFood:false,allowAddictions:false,fixedLastName:last,forcedXenotype:parent.genes.Xenotype??Xenotype,developmentalStages:DevelopmentalStage.Newborn,fixedBiologicalAge:0f,fixedChronologicalAge:0f,forceNoGear:true);
   var baby=PawnGenerator.GeneratePawn(req);
   CopyGenes(parent,baby);
   if(baby.story?.traits!=null)foreach(var t in baby.story.traits.allTraits.Where(t=>BackgroundTraits.IsBackground(t.def)).ToList())baby.story.traits.RemoveTrait(t);
   OozeState.Get?.splitBorn.Add(baby.thingIDNumber);
   if(!PawnUtility.TrySpawnHatchedOrBornPawn(baby,parent)){Find.WorldPawns.PassToWorld(baby,RimWorld.Planet.PawnDiscardDecideMode.Discard);return null;}
   if(baby.relations!=null && !baby.relations.DirectRelationExists(PawnRelationDefOf.Parent,parent))baby.relations.AddDirectRelation(PawnRelationDefOf.Parent,parent);
   Pay(parent,FissionCost);
   var food=parent.needs?.food;if(food!=null)food.CurLevel=Mathf.Max(0f,food.CurLevel-food.MaxLevel*FissionFood);
   Items.Give(parent,"LP_OozeSplitWeak");
   Ooze(parent.Map,parent.Position,4);
   if(faction==Faction.OfPlayer)Messages.Message(parent.LabelShort+"分裂出了一个新生的流浆人。",baby,MessageTypeDefOf.PositiveEvent,false);
   return baby;
  }
  public static void CopyGenes(Pawn parent,Pawn baby){
   if(baby?.genes==null || parent?.genes==null)return;
   foreach(var g in baby.genes.Xenogenes.ToList())baby.genes.RemoveGene(g);
   foreach(var g in baby.genes.Endogenes.ToList())baby.genes.RemoveGene(g);
   foreach(var g in parent.genes.Endogenes)if(!Skins.Contains(g.def))baby.genes.AddGene(g.def,false);
   var skin=RandomSkin(baby.thingIDNumber^0x0E21);if(skin!=null)baby.genes.AddGene(skin,false);
   baby.genes.SetXenotypeDirect(parent.genes.Xenotype??Xenotype);
   baby.genes.xenotypeName=parent.genes.xenotypeName;baby.genes.iconDef=parent.genes.iconDef;
   Invalidate(baby);baby.Drawer?.renderer?.SetAllGraphicsDirty();
  }

  // ---------- 渗过 ----------
  // 起点到终点之间的格子（不含两端），整数直线。
  public static List<IntVec3> Between(IntVec3 a,IntVec3 b){
   var list=new List<IntVec3>();int dx=Math.Abs(b.x-a.x),dz=Math.Abs(b.z-a.z),sx=a.x<b.x?1:-1,sz=a.z<b.z?1:-1,err=dx-dz,x=a.x,z=a.z;
   while(x!=b.x || z!=b.z){int e2=2*err;if(e2>-dz){err-=dz;x+=sx;}if(e2<dx){err+=dx;z+=sz;}if(x==b.x && z==b.z)break;list.Add(new IntVec3(x,0,z));}
   return list;
  }
  static string Landing(Pawn caster,IntVec3 c){
   var map=caster.Map;
   if(!c.IsValid || !c.InBounds(map))return "需要地图上的目标位置。";
   if(c.Fogged(map))return "不能渗进未探索的地方。";
   if(!c.Standable(map))return "那里没有可以站立的地方。";
   if(c.GetFirstPawn(map)!=null)return "那里已经有人了。";
   if(c.DistanceTo(caster.Position)>SeepRange+.5f)return "超出渗过距离（"+SeepRange+"格）。";
   return null;
  }
  // 从起点到门的路上不能有墙；终点取门另一侧离直线最近的空格。
  static IntVec3 OtherSide(Pawn caster,Building_Door door){
   var map=caster.Map;var d=door.Position;var from=caster.Position;Vector3 dir=(d-from).ToVector3();
   if(dir.sqrMagnitude<.01f)return IntVec3.Invalid;
   foreach(var c in Between(from,d))if(!c.Walkable(map) && c.GetDoor(map)==null)return IntVec3.Invalid;
   foreach(var n in GenAdj.CardinalDirections.Select(o=>d+o).Where(n=>Vector3.Dot((n-d).ToVector3(),dir)>.01f).OrderByDescending(n=>Vector3.Dot((n-d).ToVector3(),dir.normalized)))
    if(Landing(caster,n)==null)return n;
   return IntVec3.Invalid;
  }
  // 目标：3格内、和自己之间正好隔一扇门（任何门：锁着的、敌人的都行）；或者直接点门，落到门的另一侧。不能穿墙。
  public static string CannotSeep(Pawn caster,LocalTargetInfo t,out IntVec3 dest){
   dest=IntVec3.Invalid;var map=caster?.Map;if(map==null)return "施法者不在地图上。";
   var c=t.Cell;if(!c.IsValid || !c.InBounds(map))return "需要地图上的目标位置。";
   if(c.DistanceTo(caster.Position)>SeepRange+.5f)return "超出渗过距离（"+SeepRange+"格）。";
   var door=c.GetDoor(map);
   if(door!=null){dest=OtherSide(caster,door);return dest.IsValid?null:"门的另一侧没有可以站的地方（或者路上隔着墙）。";}
   var r=Landing(caster,c);if(r!=null)return r;
   int doors=0;
   foreach(var x in Between(caster.Position,c)){if(x.GetDoor(map)!=null){doors++;continue;}if(!x.Walkable(map))return "中间隔着墙，渗不过去（只能穿门）。";}
   if(doors==0)return "和目标之间没有门。";
   if(doors>1)return "一次只能渗过一扇门。";
   dest=c;return null;
  }
  // 位移复用吉斯洋基的星界跳跃（不耗精神力），两端留下胶滴。
  public static bool Seep(Pawn caster,LocalTargetInfo t){
   if(CannotSeep(caster,t,out var dest)!=null || !CanPay(caster,SeepCost))return false;
   var map=caster.Map;var from=caster.Position;
   if(!Githyanki.Step(caster,dest,0f))return false;
   Pay(caster,SeepCost);
   Ooze(map,from,3);Ooze(map,dest,3);
   return true;
  }

  // ---------- 重塑 ----------
  public static Hediff ReshapeTarget(Pawn p){
   var set=p?.health?.hediffSet;if(set==null)return null;
   var missing=set.GetMissingPartsCommonAncestors().Where(h=>h.Part!=null && ReshapeParts.Contains(h.Part.def.defName)).OrderByDescending(h=>h.Part.coverageAbsWithChildren).ThenBy(h=>h.Part.Index).FirstOrDefault();
   if(missing!=null)return missing;
   return set.hediffs.OfType<Hediff_Injury>().Where(h=>h.IsPermanent()).OrderByDescending(h=>h.Severity).FirstOrDefault();
  }
  // 返回修好的那一处（日志/消息用）；null 表示没有可重塑的。
  public static string Reshape(Pawn p){
   var h=ReshapeTarget(p);if(h==null || !Pay(p,ReshapeCost))return null;
   string label;
   if(h is Hediff_MissingPart){label=h.Part.LabelShort;p.health.RestorePart(h.Part,null,true);}
   else{label=h.Part?.LabelShort+"的"+h.LabelBase;p.health.RemoveHediff(h);}
   Ooze(p.MapHeld,p.PositionHeld,2);
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"用原生质重塑了"+label+"。",p,MessageTypeDefOf.PositiveEvent,false);
   return label;
  }

  // ---------- 析出凝胶 ----------
  public static Thing ExtractGel(Pawn p){
   if(p==null || !p.Spawned || Gel==null || !Pay(p,ExtractCost))return null;
   var gel=ThingMaker.MakeThing(Gel);
   GenPlace.TryPlaceThing(gel,p.Position,p.Map,ThingPlaceMode.Near);
   Ooze(p.Map,p.Position,2);
   return gel;
  }

  // ---------- 出生时的外观：肤色四选一、体型只有瘦/标准 ----------
  public static void OnGenerated(Pawn p){
   if(!Is(p))return;
   var green=Skins.FirstOrDefault();var own=p.genes.Endogenes.FirstOrDefault(g=>Skins.Contains(g.def));
   if(own!=null && own.def==green){var pick=RandomSkin(p.thingIDNumber^0x0E5C);if(pick!=null && pick!=green){p.genes.RemoveGene(own);p.genes.AddGene(pick,false);}}
   var story=p.story;
   if(story!=null && p.DevelopmentalStage.Adult() && (story.bodyType==BodyTypeDefOf.Hulk || story.bodyType==BodyTypeDefOf.Fat))
    story.bodyType=Rand.ValueSeeded(p.thingIDNumber^0x0B0D)<.5f?BodyTypeDefOf.Thin:(p.gender==Gender.Female?BodyTypeDefOf.Female:BodyTypeDefOf.Male);
   p.Drawer?.renderer?.SetAllGraphicsDirty();
  }
 }

 // ==================== 基因 ====================
 public class Gene_OozeProtoplasm : Gene_Resource {
  int ticks;
  public override float InitialResourceMax=>1f;
  public override float MinLevelForAlert=>.1f;
  protected override Color BarColor=>new Color(.42f,.66f,.38f);
  protected override Color BarHighlightColor=>new Color(.62f,.86f,.56f);
  // 稳形护带：上限+20。
  public override float Max=>base.Max+(Oozefolk.BandWorn(pawn)?Oozefolk.BandMaxBonus/Oozefolk.Max:0f);
  public float Points=>Value*Oozefolk.Max;
  public override void PostAdd(){base.PostAdd();Value=Oozefolk.StartPoints/Oozefolk.Max;}
  // 不自然流失；摘下护带后把超出上限的部分削掉。
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<250)return;ticks=0;if(Value>Max)Value=Max;}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticks,"oozeTicks",0);}
 }
 public class GeneGizmo_OozeProtoplasm : GeneGizmo_Resource {
  static bool dragging;
  readonly Gene_Resource resource;
  public GeneGizmo_OozeProtoplasm(Gene_Resource gene,List<IGeneResourceDrain> drainGenes,Color barColor,Color barHighlightColor):base(gene,drainGenes,barColor,barHighlightColor){resource=gene;}
  protected override bool DraggingBar{get=>dragging;set=>dragging=value;}
  protected override bool IsDraggable=>false;
  protected override string Title=>"原生质";
  protected override string GetTooltip()=>"原生质："+(resource.Value*Oozefolk.Max).ToString("0")+" / "+(resource.Max*Oozefolk.Max).ToString("0")+"\n不会自然流失。每吃一顿+3；吞没人形尸体+25，动物按体型×15（最多30），开始腐烂的减半。\n分裂80、重塑40、析出凝胶25、渗过10。";
 }
 public class Gene_OozeDigest : Gene {
  // 每吃一顿（任何提供营养的食物，药物除外）+3 原生质。
  public override void Notify_IngestedThing(Thing thing,int numTaken){
   base.Notify_IngestedThing(thing,numTaken);
   if(!Active || thing?.def==null || !thing.def.IsNutritionGivingIngestible || thing.def.IsDrug)return;
   Oozefolk.Gain(pawn,Oozefolk.MealGain);
  }
 }
 public class Gene_OozeChill : Gene {
  int ticks;
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<250)return;ticks=0;if(Active)Oozefolk.SyncChill(pawn);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref ticks,"oozeChillTicks",0);}
 }
 public class Hediff_OozeSluggish : HediffWithComps {
  public override bool ShouldRemove=>base.ShouldRemove || !Oozefolk.Has(pawn,Oozefolk.ChillGene);
 }

 // ==================== 能力 ====================
 public class CompProperties_OozeAbility : CompProperties_AbilityEffect {
  public string effect;public float cost;
  public CompProperties_OozeAbility(){compClass=typeof(CompOozeAbility);}
 }
 public class CompOozeAbility : CompAbilityEffect {
  public CompProperties_OozeAbility Props=>(CompProperties_OozeAbility)props;
  Pawn Caster=>parent.pawn;
  public override bool GizmoDisabled(out string reason){
   if(Props.cost>0 && !Oozefolk.CanPay(Caster,Props.cost)){reason=Oozefolk.NotEnough(Props.cost);return true;}
   if(Props.effect=="fission"){reason=Oozefolk.CannotFission(Caster);if(reason!=null)return true;}
   if(Props.effect=="reshape" && Oozefolk.ReshapeTarget(Caster)==null){reason="没有缺失的外部部位，也没有永久伤疤。";return true;}
   return base.GizmoDisabled(out reason);
  }
  public string Invalid(LocalTargetInfo target){
   if(Caster?.Map==null)return "施法者不在地图上。";
   switch(Props.effect){
    case "engulf":return Oozefolk.CannotEngulf(Caster,target.Thing);
    case "seep":return Oozefolk.CannotSeep(Caster,target,out _);
    case "fission":return Oozefolk.CannotFission(Caster);
    case "reshape":return Oozefolk.ReshapeTarget(Caster)==null?"没有需要重塑的地方。":null;
    case "extract":return Oozefolk.CanPay(Caster,Oozefolk.ExtractCost)?null:Oozefolk.NotEnough(Oozefolk.ExtractCost);
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
    case "engulf":Oozefolk.Engulf(Caster,target.Thing as Corpse);break;
    case "seep":Oozefolk.Seep(Caster,target);break;
    case "fission":Oozefolk.Fission(Caster);break;
    case "reshape":Oozefolk.Reshape(Caster);break;
    case "extract":Oozefolk.ExtractGel(Caster);break;
   }
  }
  public override string ExtraLabelMouseAttachment(LocalTargetInfo target)=>Props.cost>0?"原生质 "+Props.cost.ToString("0")+" / "+Oozefolk.Points(Caster).ToString("0"):null;
 }
 // 右键尸体：吞没（2小时，复用能力的施放任务）。
 public class FloatMenuOptionProvider_OozeEngulf : FloatMenuOptionProvider {
  protected override bool Drafted=>true;
  protected override bool Undrafted=>true;
  protected override bool Multiselect=>false;
  protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing,FloatMenuContext context){
   var pawn=context.FirstSelectedPawn;
   if(!(clickedThing is Corpse corpse) || pawn==null || pawn.Faction!=Faction.OfPlayer)return null;
   var ability=pawn.abilities?.GetAbility(Oozefolk.Engulfing);if(ability==null)return null;
   string label="吞没"+corpse.InnerPawn?.LabelShort+"（2小时）";
   var why=Oozefolk.CannotEngulf(pawn,corpse);if(why!=null)return new FloatMenuOption(label+"："+why,null);
   if(ability.GizmoDisabled(out string reason))return new FloatMenuOption(label+"："+reason,null);
   if(!pawn.CanReach(corpse,PathEndMode.Touch,Danger.Deadly))return new FloatMenuOption(label+"：无法到达",null);
   return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label,()=>pawn.jobs.TryTakeOrderedJob(ability.GetJob(corpse,corpse),JobTag.Misc)),pawn,corpse);
  }
 }

 // ==================== 存档状态 ====================
 public class OozeState : GameComponent {
  public HashSet<int> splitBorn=new HashSet<int>();
  public OozeState(Game game){}
  public static OozeState Get=>GameComp<OozeState>.Get();
  public static bool SplitBorn(Pawn p)=>p!=null && Get?.splitBorn.Contains(p.thingIDNumber)==true;
  public override void FinalizeInit(){Oozefolk.ClearCache();}
  public override void ExposeData(){
   var list=splitBorn.ToList();Scribe_Collections.Look(ref list,"lpOozeSplitBorn",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.LoadingVars || Scribe.mode==LoadSaveMode.PostLoadInit)splitBorn=new HashSet<int>(list??new List<int>());
  }
 }

 // ==================== 剧本：实验池逃亡 ====================
 // 第一个流浆人开局原生质 protoplasm；第 raidDay 天塞伊远征军来一支固定 raidPoints 点的追捕队（照 1.24.0 的限时袭击，走同一个 ScheduledRaid 队列）。
 public class ScenPart_OozeEscape : ScenPart {
  public float protoplasm=60,raidDay=6,raidPoints=300;public string letterLabel;
  string pBuf,dBuf,rBuf;
  public const string Hunter="LP_FaerunHostile";
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref protoplasm,"protoplasm",60);Scribe_Values.Look(ref raidDay,"raidDay",6);Scribe_Values.Look(ref raidPoints,"raidPoints",300);Scribe_Values.Look(ref letterLabel,"letterLabel");}
  public override string Summary(Scenario scen)=>"一名流浆人开局原生质"+protoplasm.ToString("0")+"；第"+raidDay.ToString("0.#")+"天塞伊远征军派来约"+raidPoints.ToString("0")+"点的追捕队"+(letterLabel.NullOrEmpty()?"":"——“"+letterLabel+"”")+"。";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(protoplasm<0 || protoplasm>Oozefolk.Max)yield return "protoplasm must be 0-100";if(raidDay<0)yield return "raidDay must not be negative";if(raidPoints<=0)yield return "raidPoints must be positive";}
  public override void DoEditInterface(Listing_ScenEdit listing){
   var r=listing.GetScenPartRect(this,RowHeight*4f);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,0,4),"原生质 ",ref protoplasm,ref pBuf,0,Oozefolk.Max);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,1,4),"追捕天数 ",ref raidDay,ref dBuf,0,60);
   Widgets.TextFieldNumericLabeled(ScenEdit.Row(r,2,4),"追捕点数 ",ref raidPoints,ref rBuf,50,5000);
   letterLabel=Widgets.TextField(ScenEdit.Row(r,3,4),letterLabel??"");
  }
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(map,ScenEdit.Starters(map).ToList(),protoplasm,raidDay,raidPoints,letterLabel);}
  public static ScheduledRaid Apply(Map map,List<Pawn> starters,float protoplasm,float day,float points,string label){
   var first=starters.FirstOrDefault(Oozefolk.Is);if(first!=null)Oozefolk.SetPoints(first,protoplasm);
   var ev=Tribute.Events;var faction=DefDatabase<FactionDef>.GetNamedSilentFail(Hunter);if(ev==null || faction==null)return null;
   var raid=new ScheduledRaid{faction=faction,fireTick=Find.TickManager.TicksGame+(int)(day*GenDate.TicksPerDay),pointsFactor=1f,fixedPoints=points,letterLabel=label};
   ev.scheduledRaids.Add(raid);return raid;
  }
 }

 // ==================== 设置 ====================
 public partial class Settings {
  public int oozeSplitDays=30;
  void ExposeOozefolk(){Scribe_Values.Look(ref oozeSplitDays,"oozeSplitDays",30);oozeSplitDays=Math.Max(15,Math.Min(60,oozeSplitDays));}
 }
 public static class OozefolkSettings {
  public static void Draw(Listing_Standard l){
   l.GapLine();l.Label("流浆人分裂冷却："+LichMod.Settings.oozeSplitDays+"天");
   LichMod.Settings.oozeSplitDays=(int)l.Slider(LichMod.Settings.oozeSplitDays,15,60);
  }
 }

 // ==================== 补丁 ====================
 [HarmonyPatch(typeof(Pawn_GeneTracker),"Notify_GenesChanged")]
 public static class OozeGeneCache {static void Postfix(Pawn_GeneTracker __instance){Oozefolk.Invalidate(__instance?.pawn);}}

 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class OozeOnGeneration {static void Postfix(Pawn __result){Oozefolk.OnGenerated(__result);}}

 // 酸蚀×25%、寒冷×150%（钝击、火焰在基因的 damageFactors 里）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class OozeDamage {
  static void Prefix(Pawn __instance,ref DamageInfo __0){float f=Oozefolk.ExtraDamageFactor(__instance,__0.Def);if(f!=1f)__0.SetAmount(__0.Amount*f);}
 }
 // 伪足的酸蚀：被击中者身上的衣物受到的伤害×200%。
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class OozeApparelAcid {
  static void Prefix(Thing __instance,ref DamageInfo __0){if(__0.Def!=null && __0.Def==Oozefolk.Acid && __instance is Apparel a && a.Wearer!=null)__0.SetAmount(__0.Amount*Oozefolk.ApparelAcidFactor);}
 }
 // 出血×50%。
 [HarmonyPatch(typeof(HediffSet),"CalculateBleedRate")]
 public static class OozeBleed {static void Postfix(HediffSet __instance,ref float __result){if(__result>0f && Oozefolk.Is(__instance.pawn))__result*=Oozefolk.BleedFactor;}}
 // 伤口不感染。
 [HarmonyPatch(typeof(HediffComp_Infecter),"CheckMakeInfection")]
 public static class OozeNoInfection {static bool Prefix(HediffComp_Infecter __instance)=>!Oozefolk.Is(__instance.Pawn);}
 // 不留疤：伤口不会变成永久伤。
 [HarmonyPatch(typeof(HediffComp_GetsPermanent),nameof(HediffComp_GetsPermanent.IsPermanent),MethodType.Setter)]
 public static class OozeNoScar {static bool Prefix(HediffComp_GetsPermanent __instance,bool value)=>!value || !Oozefolk.Is(__instance.Pawn);}
 // 伪足：操作能力不低于60%（不超过意识水平），写法同象鼻。
 [HarmonyPatch(typeof(PawnCapacityWorker_Manipulation),nameof(PawnCapacityWorker_Manipulation.CalculateCapacityLevel))]
 public static class OozePseudopodManipulation {
  static void Postfix(HediffSet diffSet,ref float __result){
   if(__result>=Oozefolk.ManipulationFloor)return;var p=diffSet?.pawn;if(!Oozefolk.Has(p,Oozefolk.PseudopodGene))return;
   float consciousness=PawnCapacityUtility.CalculateCapacityLevel(diffSet,PawnCapacityDefOf.Consciousness,null,false);
   __result=Mathf.Max(__result,Mathf.Min(Oozefolk.ManipulationFloor,consciousness));
  }
 }
 // 无定形：越狱几率×2（与罪犯背景等相乘），越狱途中任何门都能打开。
 [HarmonyPatch(typeof(PrisonBreakUtility),nameof(PrisonBreakUtility.InitiatePrisonBreakMtbDays))]
 public static class OozePrisonBreak {
  static void Postfix(Pawn pawn,System.Text.StringBuilder sb,ref float __result){
   if(__result<=0f || !Oozefolk.Has(pawn,Oozefolk.AmorphousGene))return;
   __result/=Oozefolk.PrisonBreakFactor;if(sb!=null){sb.AppendLineIfNotEmpty();sb.Append("  - 无定形（流浆人）: "+Oozefolk.PrisonBreakFactor.ToStringPercent());}
  }
 }
 [HarmonyPatch(typeof(Building_Door),nameof(Building_Door.PawnCanOpen))]
 public static class OozeDoorBreak {
  static void Postfix(Pawn p,ref bool __result){if(!__result && p!=null && p.IsPrisoner && Oozefolk.Has(p,Oozefolk.AmorphousGene) && PrisonBreakUtility.IsPrisonBreaking(p))__result=true;}
 }
 // 屠宰流浆人尸体：3份原生质凝胶，不出肉和皮。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.ButcherProducts))]
 public static class OozeButcher {
  static IEnumerable<Thing> Postfix(IEnumerable<Thing> values,Pawn __instance){
   if(!Oozefolk.Is(__instance) || Oozefolk.Gel==null){foreach(var t in values)yield return t;yield break;}
   var gel=ThingMaker.MakeThing(Oozefolk.Gel);gel.stackCount=Oozefolk.ButcherGel;yield return gel;
  }
 }
 // 寒霜迟缓、寒霜缠身（Items.Give）和寒气侵体（Fiends.Chill）持续时间×150%。
 [HarmonyPatch(typeof(Items),nameof(Items.Give))]
 public static class OozeColdHediff {static void Postfix(Pawn p,Hediff __result){Oozefolk.StretchCold(p,__result);}}
 [HarmonyPatch(typeof(Fiends),nameof(Fiends.Chill))]
 public static class OozeColdChill {static void Postfix(Pawn p,Hediff __result){Oozefolk.StretchCold(p,__result);}}
 // 分裂冷却按设置（15～60天）。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.StartCooldown))]
 public static class OozeFissionCooldown {
  static void Prefix(RimWorld.Ability __instance,ref int ticks){if(ticks>0 && __instance?.def!=null && __instance.def==Oozefolk.Fissioning)ticks=LichMod.Settings.oozeSplitDays*GenDate.TicksPerDay;}
 }
 // 分裂出来的孩子不进背景特性池。
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.GainTrait))]
 public static class OozeSplitBornTraits {
  static bool Prefix(Pawn ___pawn,Trait trait)=>trait==null || !BackgroundTraits.IsBackground(trait.def) || !OozeState.SplitBorn(___pawn);
 }
 // 万物消化：吃生食不会不开心（挂在原版 nullifyingGenes 上）。
 [StaticConstructorOnStartup]
 public static class OozeStartup {
  static OozeStartup(){
   var raw=ThoughtDefOf.AteRawFood;var gene=Oozefolk.DigestGene;
   if(raw!=null && gene!=null){if(raw.nullifyingGenes==null)raw.nullifyingGenes=new List<GeneDef>();if(!raw.nullifyingGenes.Contains(gene))raw.nullifyingGenes.Add(gene);}
  }
 }
}
