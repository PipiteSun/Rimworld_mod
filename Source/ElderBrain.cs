using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 一份传承记忆：被吞噬者的名字与各技能的自身经验。
 public class BrainMemory : IExposable {
  public string name;public Dictionary<SkillDef,float> xp=new Dictionary<SkillDef,float>();
  public float Total=>xp.Values.Sum();
  public SkillDef Best=>xp.Count==0?null:xp.OrderByDescending(kv=>kv.Value).First().Key;
  public static BrainMemory Of(Pawn p){
   if(p?.skills==null)return null;
   var m=new BrainMemory{name=p.LabelShort};
   foreach(var s in p.skills.skills){float v=MindFlayer.OwnXp(s);if(v>0)m.xp[s.def]=v;}
   return m.xp.Count>0?m:null;
  }
  public string Summary=>name+"（"+string.Join("、",xp.OrderByDescending(kv=>kv.Value).Take(3).Select(kv=>kv.Key.label+" "+(kv.Value*ElderBrain.InheritShare).ToString("0")))+"）";
  public void ExposeData(){Scribe_Values.Look(ref name,"name");Scribe_Collections.Look(ref xp,"xp",LookMode.Def,LookMode.Value);if(Scribe.mode==LoadSaveMode.PostLoadInit){xp=(xp??new Dictionary<SkillDef,float>()).Where(kv=>kv.Key!=null).ToDictionary(kv=>kv.Key,kv=>kv.Value);}}
 }

 public static class ElderBrain {
  public static readonly int[] Thresholds={1,4,10,20,35};
  public static readonly float[] TadpoleDays={5,4,3,2,1};
  public const int MaxMemories=5,MaxLevel=5,SummonLevel=3,SummonIntervalDays=10,ArriveRadius=4;
  public const float InheritShare=.8f,DestroyedCerebrumLoss=30f;
  static ThingDef def,jar;static HediffDef call,daze;static ThoughtDef destroyed,starving;
  public static ThingDef Def=>def??(def=DefDatabase<ThingDef>.GetNamed("LP_ElderBrain"));
  public static ThingDef Jar=>jar??(jar=DefDatabase<ThingDef>.GetNamed("LP_BrainJar"));
  public static HediffDef Call=>call??(call=DefDatabase<HediffDef>.GetNamed("LP_ElderBrainCall"));
  public static HediffDef Daze=>daze??(daze=DefDatabase<HediffDef>.GetNamed("LP_PsychicDaze"));
  public static ThoughtDef Destroyed=>destroyed??(destroyed=DefDatabase<ThoughtDef>.GetNamed("LP_ElderBrainDestroyed"));
  public static ThoughtDef Starving=>starving??(starving=DefDatabase<ThoughtDef>.GetNamed("LP_ElderBrainStarving"));
  public static int LevelFor(int brains){int l=0;for(int i=0;i<Thresholds.Length;i++)if(brains>=Thresholds[i])l=i+1;return l;}
  public static Building_ElderBrain On(Map map,Faction faction)=>map?.listerThings.ThingsOfDef(Def).OfType<Building_ElderBrain>().FirstOrDefault(b=>b.initialized && b.Faction==faction);
  public static IEnumerable<Pawn> FlayersOf(Faction faction)=>Find.Maps.SelectMany(m=>m.mapPawns.SpawnedPawnsInFaction(faction)).Where(p=>!p.Dead && MindFlayer.IsMindFlayer(p));
 }

 // 夺心魔在有上古脑的地图上吞噬人形大脑：计数并保存记忆。
 public static partial class MindFlayerHooks {
  static partial void OnDevoured(Pawn caster,Pawn victim,bool humanlike){
   if(humanlike && MindFlayer.IsMindFlayer(caster) && caster.Faction!=null)ElderBrain.On(caster.MapHeld,caster.Faction)?.AddBrain(victim);
   OnHalfIllithidDevoured(caster,victim,humanlike);
  }
  static partial void OnHalfIllithidDevoured(Pawn caster,Pawn victim,bool humanlike);
 }

 public class Building_ElderBrain : Building {
  public bool initialized;public int brains;public List<BrainMemory> memories=new List<BrainMemory>();
  public int tadpoleTicks,hungryTicks,summonTicks;int accumulated;
  public int Level=>initialized?ElderBrain.LevelFor(brains):0;
  public CompRefuelable Food=>GetComp<CompRefuelable>();
  public bool Fed=>Food==null || Food.HasFuel;
  public int TadpoleInterval=>Level<1?int.MaxValue:Mathf.RoundToInt(ElderBrain.TadpoleDays[Level-1]*GenDate.TicksPerDay);
  public static bool IsFlayerCorpse(Thing t)=>t is Corpse c && MindFlayerButcher.Yields(c.InnerPawn);
  public Corpse NearbyFlayerCorpse(){
   if(!Spawned)return null;
   foreach(var c in GenAdj.CellsOccupiedBy(this).Concat(GenAdj.CellsAdjacent8Way(this)))if(c.InBounds(Map))foreach(var t in c.GetThingList(Map))if(IsFlayerCorpse(t))return (Corpse)t;
   return null;
  }
  public bool Initialize(Corpse corpse){
   if(initialized || !IsFlayerCorpse(corpse))return false;
   initialized=true;brains=Math.Max(brains,1);corpse.Destroy();
   if(Faction==Faction.OfPlayer)Messages.Message("上古脑吞下了夺心魔的尸体，苏醒过来。",this,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  public void AddBrain(Pawn victim){
   if(!initialized)return;
   int before=Level;brains++;
   var m=BrainMemory.Of(victim);if(m!=null){memories.Add(m);while(memories.Count>ElderBrain.MaxMemories)memories.RemoveAt(0);}
   if(Level>before && Faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("上古脑成长","上古脑吸收了足够多的大脑，成长到"+Level+"级。蝌蚪产出更快了。"+(Level>=ElderBrain.SummonLevel && LichMod.Settings.elderBrainSummon?"\n\n它开始用心灵呼唤远方的流浪者。":""),LetterDefOf.PositiveEvent,this);
  }
  protected override void Tick(){
   base.Tick();
   if((accumulated+=1)<GenTicks.TickRareInterval)return;
   int elapsed=accumulated;accumulated=0;Process(elapsed);
  }
  public void Process(int elapsed){
   if(!Spawned)return;
   if(!initialized){var c=NearbyFlayerCorpse();if(c!=null)Initialize(c);return;}
   if(!Fed){
    if((hungryTicks+=elapsed)>=GenDate.TicksPerDay){hungryTicks-=GenDate.TicksPerDay;Starve();}
    return;
   }
   hungryTicks=0;
   if((tadpoleTicks+=elapsed)>=TadpoleInterval){tadpoleTicks=0;SpawnTadpole();}
   if(LichMod.Settings.elderBrainSummon && Level>=ElderBrain.SummonLevel){if((summonTicks+=elapsed)>=ElderBrain.SummonIntervalDays*GenDate.TicksPerDay){summonTicks=0;Summon();}}
  }
  public void Starve(){
   bool lost=memories.Count>0;if(lost)memories.RemoveAt(0);
   if(Faction!=null)foreach(var p in ElderBrain.FlayersOf(Faction).Where(p=>p.Map==Map).ToList())p.needs?.mood?.thoughts?.memories?.TryGainMemory(ElderBrain.Starving);
   if(Faction==Faction.OfPlayer)Messages.Message("上古脑断粮挨饿了一天"+(lost?"，丢失了一份传承记忆":"")+"。用草药、生肉或饭菜喂它。",this,MessageTypeDefOf.NegativeEvent,false);
  }
  public Thing SpawnTadpole(){
   var t=ThingMaker.MakeThing(Tadpoles.Item);GenPlace.TryPlaceThing(t,InteractionCellOrNear,Map,ThingPlaceMode.Near);
   if(Faction==Faction.OfPlayer)Messages.Message("上古脑产出了一条夺心魔蝌蚪。",t,MessageTypeDefOf.PositiveEvent,false);
   return t;
  }
  IntVec3 InteractionCellOrNear=>Position+IntVec3.South*2;
  public Thing FillJar(int index){
   if(index<0 || index>=memories.Count)return null;
   var jar=(Thing_BrainJar)ThingMaker.MakeThing(ElderBrain.Jar);jar.memory=memories[index];memories.RemoveAt(index);
   GenPlace.TryPlaceThing(jar,InteractionCellOrNear,Map,ThingPlaceMode.Near);return jar;
  }
  // 召唤：一个无派系流浪者从地图边缘恍惚地走来，走到上古脑旁边倒下。
  public Pawn Summon(){
   if(!CellFinder.TryFindRandomEdgeCellWith(c=>c.Standable(Map) && Map.reachability.CanReach(c,this,PathEndMode.Touch,TraverseParms.For(TraverseMode.PassDoors)),Map,CellFinder.EdgeRoadChance_Neutral,out IntVec3 edge))return null;
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Drifter??PawnKindDefOf.Colonist,null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,allowDowned:false,allowDead:false));
   if(p.Faction!=null)p.SetFaction(null);
   GenSpawn.Spawn(p,edge,Map);
   var h=(Hediff_ElderBrainCall)p.health.AddHediff(ElderBrain.Call);h.brain=this;h.Pull();
   if(Faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("上古脑的呼唤","上古脑用心灵呼唤了一个流浪者："+p.LabelShort+"正恍惚地走向它，到达后会倒在旁边。",LetterDefOf.NeutralEvent,p);
   return p;
  }
  public override void Destroy(DestroyMode mode=DestroyMode.Vanish){
   var faction=Faction;bool punish=initialized && mode==DestroyMode.KillFinalize;var map=Map;
   base.Destroy(mode);
   if(punish && faction!=null)Punish(faction,map);
  }
  // 被摧毁：所有本阵营夺心魔心情-15（10天）、损失30脑髓；同地图的夺心魔被心灵反噬击晕。
  public static void Punish(Faction faction,Map map){
   foreach(var p in ElderBrain.FlayersOf(faction).ToList()){
    p.needs?.mood?.thoughts?.memories?.TryGainMemory(ElderBrain.Destroyed);
    MindFlayer.Pay(p,Mathf.Min(MindFlayer.Points(p),ElderBrain.DestroyedCerebrumLoss));
    if(p.Map==map)p.stances?.stunner?.StunFor(300,null,false,false,false);
   }
   if(faction==Faction.OfPlayer)Find.LetterStack.ReceiveLetter("上古脑陨灭","上古脑被摧毁了。群脑的核心断裂，所有夺心魔都受到了心灵反噬：心情大降，脑髓流失。",LetterDefOf.NegativeEvent);
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(Faction!=Faction.OfPlayer)yield break;
   if(!initialized){yield return new Command_Action{defaultLabel="唤醒上古脑",defaultDesc="把一具夺心魔尸体放在上古脑旁边（相邻格子），上古脑会吞下它并苏醒。",icon=ContentFinder<Texture2D>.Get("MindFlayer/Abilities/DevourBrain",false)??BaseContent.BadTex,action=()=>{var c=NearbyFlayerCorpse();if(c==null || !Initialize(c))Messages.Message("上古脑旁边没有夺心魔尸体。",this,MessageTypeDefOf.RejectInput,false);}};yield break;}
   yield return new Command_Action{defaultLabel="灌装脑浆罐",defaultDesc="把一份传承记忆灌装成脑浆罐。夺心魔使用脑浆罐会继承其中80%的经验。上古脑最多保存5份记忆。",icon=ContentFinder<Texture2D>.Get("MindFlayer/Items/BrainJar",false)??BaseContent.BadTex,action=()=>Find.WindowStack.Add(new FloatMenu(memories.Select((m,i)=>new FloatMenuOption(m.Summary,()=>FillJar(i))).DefaultIfEmpty(new FloatMenuOption("没有传承记忆",null)).ToList()))};
  }
  public override string GetInspectString(){
   var s=base.GetInspectString();var lines=new List<string>();
   if(!initialized)lines.Add("沉睡中：把一具夺心魔尸体放在旁边唤醒它。");
   else{
    lines.Add("等级 "+Level+" / "+ElderBrain.MaxLevel+"；已吸收大脑 "+brains+(Level<ElderBrain.MaxLevel?"（下一级需要"+ElderBrain.Thresholds[Level]+"个）":""));
    lines.Add(Fed?"下一条蝌蚪："+((TadpoleInterval-tadpoleTicks)/(float)GenDate.TicksPerDay).ToString("0.#")+"天后":"挨饿中：停止产出蝌蚪，每天丢失一份记忆");
    lines.Add("传承记忆 "+memories.Count+" / "+ElderBrain.MaxMemories);
    if(LichMod.Settings.elderBrainSummon)lines.Add(Level>=ElderBrain.SummonLevel?"召唤：每"+ElderBrain.SummonIntervalDays+"天呼唤一个流浪者":"召唤：需要"+ElderBrain.SummonLevel+"级");
   }
   return string.IsNullOrEmpty(s)?string.Join("\n",lines):s+"\n"+string.Join("\n",lines);
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref initialized,"elderInitialized",false);Scribe_Values.Look(ref brains,"elderBrains",0);
   Scribe_Collections.Look(ref memories,"elderMemories",LookMode.Deep);
   Scribe_Values.Look(ref tadpoleTicks,"elderTadpoleTicks",0);Scribe_Values.Look(ref hungryTicks,"elderHungryTicks",0);Scribe_Values.Look(ref summonTicks,"elderSummonTicks",0);Scribe_Values.Look(ref accumulated,"elderTicks",0);
   if(Scribe.mode==LoadSaveMode.PostLoadInit)memories=memories?.Where(m=>m!=null).ToList()??new List<BrainMemory>();
  }
 }

 public class Hediff_ElderBrainCall : HediffWithComps {
  public Building_ElderBrain brain;int ticks;
  public override bool ShouldRemove=>base.ShouldRemove || brain==null || brain.Destroyed;
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<250)return;ticks=0;Pull();}
  public bool Arrived=>pawn.Spawned && brain!=null && brain.Spawned && brain.Map==pawn.Map && pawn.Position.InHorDistOf(brain.Position,ElderBrain.ArriveRadius);
  public void Pull(){
   if(pawn==null || pawn.Dead || !pawn.Spawned || brain==null || !brain.Spawned)return;
   if(Arrived){pawn.health.AddHediff(ElderBrain.Daze);pawn.health.RemoveHediff(this);return;}
   if(pawn.Downed || pawn.CurJobDef==JobDefOf.Goto)return;
   var target=CellFinder.RandomClosewalkCellNear(brain.Position,brain.Map,ElderBrain.ArriveRadius-1);
   var job=JobMaker.MakeJob(JobDefOf.Goto,target);job.locomotionUrgency=LocomotionUrgency.Walk;
   pawn.jobs.StartJob(job,JobCondition.InterruptForced);
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref brain,"elderBrain");Scribe_Values.Look(ref ticks,"elderCallTicks",0);}
 }

 public class Thing_BrainJar : ThingWithComps {
  public BrainMemory memory;
  public override string LabelNoCount=>base.LabelNoCount+(memory!=null?"（"+memory.name+"的记忆）":"");
  public override string GetInspectString(){var s=base.GetInspectString();if(memory==null)return s;var line="可继承："+string.Join("、",memory.xp.OrderByDescending(kv=>kv.Value).Select(kv=>kv.Key.label+" "+(kv.Value*ElderBrain.InheritShare).ToString("0")+"经验"));return string.IsNullOrEmpty(s)?line:s+"\n"+line;}
  public override void ExposeData(){base.ExposeData();Scribe_Deep.Look(ref memory,"brainMemory");}
  // 继承：每项技能获得记忆中80%的经验。
  public bool Inherit(Pawn p){
   if(memory==null || !MindFlayer.IsMindFlayer(p))return false;
   foreach(var kv in memory.xp)MindFlayer.GrantXp(p,kv.Key,kv.Value*ElderBrain.InheritShare);
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"吸收了"+memory.name+"的传承记忆。",p,MessageTypeDefOf.PositiveEvent,false);
   memory=null;return true;
  }
 }
 public class CompProperties_UseEffectInheritMemory : CompProperties_UseEffect {public CompProperties_UseEffectInheritMemory(){compClass=typeof(CompUseEffect_InheritMemory);}}
 public class CompUseEffect_InheritMemory : CompUseEffect {
  public override AcceptanceReport CanBeUsedBy(Pawn p){
   if(!MindFlayer.IsMindFlayer(p))return "只有夺心魔能吸收传承记忆";
   if((parent as Thing_BrainJar)?.memory==null)return "罐子是空的";
   return base.CanBeUsedBy(p);
  }
  public override void DoEffect(Pawn usedBy){base.DoEffect(usedBy);if((parent as Thing_BrainJar)?.Inherit(usedBy)==true && !parent.Destroyed)parent.Destroy();}
 }
}
