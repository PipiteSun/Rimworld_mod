using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.56.0「文化之神」：模因被动和戒律心情（规格 2.3 / 2.4 / 第三节 / 7.1）。
 // 心情 worker 只读 IdeoMapCache（每 2500 tick 刷新一次的地图级计数），StatPart 只读 pawn 文化的缓存模因集合和地图时间，不扫地图、不分配。

 // ==================== 地图缓存 ====================
 public class IdeoMapCache : MapComponent {
  static readonly Dictionary<Map,IdeoMapCache> all=new Dictionary<Map,IdeoMapCache>();
  static List<ThingDef> statueDefs,beastDefs;static ThingDef elderBrain;
  public int refreshedTick=int.MinValue,constructs,beasts,giants,xenotypes;
  public bool lich,phylactery,elderBrainHere,enchanting,altar,demigod,buriedGrave,pantheonStatue;
  public readonly HashSet<LP_DeityDef> statues=new HashSet<LP_DeityDef>();
  public IdeoMapCache(Map map):base(map){}
  public static IdeoMapCache For(Map m){
   if(m==null || !IdeoBridge.Active)return null;
   if(!all.TryGetValue(m,out var c)){c=m.GetComponent<IdeoMapCache>();if(c==null)return null;if(all.Count>Find.Maps.Count+2)all.Clear();all[m]=c;}
   if(IdeoBridge.Now-c.refreshedTick>=2500)c.Refresh();
   return c;
  }
  public override void MapComponentTick(){if(Find.TickManager.TicksGame%2500==777 && IdeoBridge.Active)Refresh();}
  public static List<ThingDef> StatueDefs=>statueDefs??(statueDefs=DefDatabase<ThingDef>.AllDefs.Where(d=>d.GetModExtension<LP_StatueExtension>()!=null).ToList());
  static bool SacredBeast(ThingDef d){if(beastDefs==null)beastDefs=DefDatabase<ThingDef>.AllDefs.Where(x=>x.race!=null && x.race.Animal && (x.defName.StartsWith("Wolf_") || x.defName.StartsWith("Bear_") || x.defName=="LP_WinterWolf")).ToList();return beastDefs.Contains(d);}
  public void Refresh(){
   refreshedTick=IdeoBridge.Now;
   var cols=map.mapPawns.FreeColonistsSpawned;
   constructs=Necro.Registry?.constructs.Count(p=>p!=null && !p.Dead && p.MapHeld==map && p.Faction?.IsPlayer==true)??0;
   beasts=0;foreach(var a in map.mapPawns.SpawnedColonyAnimals)if(SacredBeast(a.def))beasts++;
   giants=0;lich=false;demigod=false;var xs=new HashSet<string>();
   foreach(var p in cols){
    if(Necro.Construct(p)!=null)continue;
    if(IdeoBridge.IsGiant(p))giants++;if(Items.IsLich(p))lich=true;if(Demigods.Of(p)!=null)demigod=true;
    xs.Add(p.genes?.Xenotype?.defName??p.genes?.CustomXenotype?.name??"Baseliner");
   }
   xenotypes=xs.Count;
   phylactery=Necro.Registry?.boxes.Any(b=>b!=null && !b.Destroyed && b.MapHeld==map)==true;
   if(elderBrain==null)elderBrain=DefDatabase<ThingDef>.GetNamedSilentFail("LP_ElderBrain");
   elderBrainHere=elderBrain!=null && map.listerThings.ThingsOfDef(elderBrain).Count>0;
   var ench=DefDatabase<ThingDef>.GetNamedSilentFail(Workbenches.Enchanting);enchanting=ench!=null && map.listerThings.ThingsOfDef(ench).Any(t=>t.Faction?.IsPlayer==true);
   altar=FaithUtility.Altars(map).Any();
   statues.Clear();pantheonStatue=false;
   foreach(var d in StatueDefs)foreach(var t in map.listerThings.ThingsOfDef(d)){if(t.Faction?.IsPlayer!=true)continue;var g=d.GetModExtension<LP_StatueExtension>().deity;if(g==null)pantheonStatue=true;else statues.Add(g);}
   buriedGrave=map.listerThings.ThingsInGroup(ThingRequestGroup.Grave).OfType<Building_Grave>().Any(g=>g.HasCorpse);
  }
 }

 // ==================== 戒律的情境心情 ====================
 public class ThoughtWorker_LPPrecept : ThoughtWorker_Precept {
  protected override ThoughtState ShouldHaveThought(Pawn p){
   if(!IdeoBridge.Linked || p?.Faction?.IsPlayer!=true || !p.RaceProps.Humanlike || Necro.Construct(p)!=null)return false;
   var c=IdeoMapCache.For(p.MapHeld);if(c==null)return false;
   switch(def.defName){
    case "LP_Thought_Undead_Constructs":return c.constructs<=0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(Math.Min(c.constructs,5)-1);
    case "LP_Thought_Undead_Lich":return c.lich && !Items.IsLich(p);
    case "LP_Thought_Undead_Phylactery":return c.phylactery;
    case "LP_Thought_Giants_Feared":{if(IdeoBridge.IsGiant(p) || c.giants<=0)return false;return ThoughtState.ActiveAtStage(Math.Min(c.giants,3)-1);}
    case "LP_Thought_Giants_Supreme":return ThoughtState.ActiveAtStage(IdeoBridge.IsGiant(p)?0:1);
    case "LP_Thought_Arcane_Item":return IdeoBridge.HasMagicItem(p);
    case "LP_Thought_Arcane_Enchanting":return c.enchanting;
    case "LP_Thought_Arcane_NotBlessed":return !IdeoBridge.IsCaster(p);
    case "LP_Thought_Races_Human":return ThoughtState.ActiveAtStage(p.genes==null || (p.genes.Xenotype==XenotypeDefOf.Baseliner && p.genes.CustomXenotype==null)?0:1);
    case "LP_Thought_Races_Many":return c.xenotypes>=4;
    case "LP_Thought_Races_Own":{var st=IdeoState.Get;if(st==null || p.Ideo==null || !st.founderXeno.TryGetValue(p.Ideo.id,out var x))return false;var mine=p.genes?.Xenotype?.defName??p.genes?.CustomXenotype?.name??"Baseliner";return ThoughtState.ActiveAtStage(mine==x?0:1);}
    case "LP_Thought_Brain_ElderBrain":return c.elderBrainHere;
    case "LP_Thought_Apotheosis_Demigod":return c.demigod && Demigods.Of(p)==null;
    case "LP_Thought_Offerings_Unfed":{var st=IdeoState.Get;return st!=null && st.lastOfferingTick!=int.MinValue && IdeoBridge.Now-st.lastOfferingTick>Faith.ManifestDays*GenDate.TicksPerDay;}
   }
   return false;
  }
 }

 // ==================== 模因被动的心情（不挂在戒律上，只看小人自己的文化） ====================
 public class ThoughtWorker_LPMeme : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!IdeoBridge.Linked || p?.Faction?.IsPlayer!=true || p.Ideo==null || !p.RaceProps.Humanlike || Necro.Construct(p)!=null)return false;
   var i=IdeoBridge.OfPawn(p);string key=i.deity?.key;
   switch(def.defName){
    case "LP_Thought_LathanderConstructs":{if(key!="Lathander")return false;var c=IdeoMapCache.For(p.MapHeld);return c==null || c.constructs<=0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(Math.Min(c.constructs,5)-1);}
    case "LP_Thought_MystraGift":return key=="Mystra" && IdeoBridge.HasMagicItem(p);
    case "LP_Thought_MalarBeasts":{if(key!="Malar")return false;var c=IdeoMapCache.For(p.MapHeld);return c==null || c.beasts<=0?ThoughtState.Inactive:ThoughtState.ActiveAtStage(Math.Min(c.beasts,3)-1);}
    case "LP_Thought_TalosStorm":return key=="Talos" && p.Spawned && Faith.Storm(p.Map);
    case "LP_Thought_DefiantShrine":{if(!i.defiant)return false;var c=IdeoMapCache.For(p.MapHeld);return c!=null && c.altar;}
    case "LP_Thought_DeityStatue":return i.deity==null?ThoughtState.Inactive:ThoughtState.ActiveAtStage(IdeoTemple.StatueAtHome(i.deity)?1:0);
   }
   return false;
  }
 }

 // ==================== 属性 ====================
 public class StatPart_LPIdeo : StatPart {
  public string kind;
  static HediffDef forgeFire,swore;static MemeDef bane;
  float Factor(Pawn p,out string why){
   why=null;if(p?.ideo==null || !IdeoBridge.Linked)return 1f;
   var i=IdeoBridge.OfPawn(p);float f=1f;
   switch(kind){
    case "work":{
     string key=i.deity?.key;
     if(key=="Lathander" && p.MapHeld!=null){int h=GenLocalDate.HourOfDay(p.MapHeld);if(h>=5 && h<8){f*=1.05f;why="晨主之光（清晨）×1.05";}}
     else if(key=="Shar" && p.MapHeld!=null && Faith.Night(p.MapHeld)){f*=1.08f;why="夜之女士（夜里）×1.08";}
     if(StatPart_Faith.Smithing(p)){
      if(key=="Surtr"){f*=1.1f;why=(why==null?"":why+"；")+"熔炉巨人（锻造）×1.1";}
      if(forgeFire==null)forgeFire=DefDatabase<HediffDef>.GetNamedSilentFail("LP_ForgeFire");
      if(forgeFire!=null && p.health.hediffSet.HasHediff(forgeFire)){f*=1.25f;why=(why==null?"":why+"；")+"熔炉之火（锻造）×1.25";}
     }
     break;}
    case "slave":{
     if(!p.IsSlave)break;if(bane==null)bane=DefDatabase<MemeDef>.GetNamedSilentFail("LP_Meme_Bane");
     var owner=p.Faction?.ideos?.PrimaryIdeo;if(owner!=null && IdeoBridge.DeityOf(owner)?.key=="Bane"){f*=.7f;why="黑暗之主×0.7";}
     break;}
    case "social":
     if(p.Faction?.IsPlayer==true && IdeoBridge.Player.ordning && IdeoBridge.IsGiant(p)){f*=1.1f;why="巨人位阶×1.1";}
     break;
   }
   return f;
  }
  float Offset(Pawn p,out string why){why=null;if(kind=="mind" && p?.ideo!=null && IdeoBridge.Linked && IdeoBridge.OfPawn(p).defiant){why="不跪之人+5%";return .05f;}return 0f;}
  public override void TransformValue(StatRequest req,ref float val){if(!(req.Thing is Pawn p) || p.ideo==null)return;val=val*Factor(p,out _)+Offset(p,out _);}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p) || p.ideo==null)return null;Factor(p,out var a);Offset(p,out var b);return a==null && b==null?null:"文化："+string.Join("；",new[]{a,b}.Where(s=>s!=null));}
 }
 public static class IdeoPrecepts {
  public static void Register(){
   void Add(string stat,string kind){var d=DefDatabase<StatDef>.GetNamedSilentFail(stat);if(d==null)return;if(d.parts==null)d.parts=new List<StatPart>();if(d.parts.Any(x=>x is StatPart_LPIdeo s && s.kind==kind))return;d.parts.Add(new StatPart_LPIdeo{kind=kind,parentStat=d});}
   Add("WorkSpeedGlobal","work");Add("SlaveSuppressionFallRate","slave");Add("LP_MindResist","mind");Add("SocialImpact","social");
  }
 }

 // ==================== 神祇对立（派系好感） ====================
 public class GoodwillSituationWorker_LPDeityRivalry : GoodwillSituationWorker {
  static readonly (string a,string b,int v)[] Pairs={("Lathander","Shar",-30),("Lathander","Undeath",-30),("Tyr","Bane",-30),("Mystra","Shar",-30),("Kelemvor","Undeath",-40)};
  static IEnumerable<string> Keys(IdeoBridge.Info i){if(i.deity!=null)yield return i.deity.key;if(i.undeath)yield return "Undeath";}
  public static int Offset(Ideo mine,Ideo theirs){
   if(!IdeoBridge.Linked || mine==null || theirs==null)return 0;
   var a=Keys(IdeoBridge.Of(mine)).ToList();var b=Keys(IdeoBridge.Of(theirs)).ToList();if(a.Count==0 || b.Count==0)return 0;int worst=0;
   foreach(var x in Pairs)if((a.Contains(x.a) && b.Contains(x.b)) || (a.Contains(x.b) && b.Contains(x.a)))worst=Math.Min(worst,x.v);
   return worst;
  }
  public override int GetNaturalGoodwillOffset(Faction other)=>other==null || other.IsPlayer?0:Offset(IdeoBridge.PlayerIdeo,other.ideos?.PrimaryIdeo);
 }
}
