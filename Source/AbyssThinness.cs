using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.54.0 位面薄弱度（规格 7.1）：每张地图 0～100。只在有过深渊仪式或开过深渊裂口的地图上计算（没激活时组件第一行就返回）。
 // 每天一次：自然 -2、每个驱魔结界 -1（最多 5 个）、开着的深渊裂口 +3、地图上每件恶魔学典籍 / 饱魂之牙 +0.2；然后掷深渊低语（≥20）和深渊入侵（≥60）。
 public static class Thinness {
  public const float Max=100f,DailyDecay=2f,WardDaily=1f,RiftDaily=3f,ItemDaily=.2f,KillGain=1f,RefineGain=3f,EventDrop=15f,WhisperMin=20f,IncursionMin=60f;
  public const int WardCap=5,EventCooldownDays=10;
  public static readonly float[] RitualGain={5f,10f,15f,25f};
  public static float? TestRoll;
  public static MapComponent_PlanarThinness Of(Map map)=>MapComp<MapComponent_PlanarThinness>.Get(map);
  public static float Get(Map map)=>Of(map)?.value??0f;
  public static void Activate(Map map){var c=Of(map);if(c!=null)c.active=true;}
  public static void Add(Map map,float amount,string reason){var c=Of(map);if(c==null || !c.active && amount>0f && reason==null)return;if(amount>0f)c.active=true;c.value=Mathf.Clamp(c.value+amount,0f,Max);}
  public static float WhisperChance(float v)=>v<WhisperMin?0f:(v-10f)/200f;
  public static float IncursionChance(float v)=>v<IncursionMin?0f:(v-50f)/400f;
  public static float Frequency=>AbyssSettings.ThinFrequency*Events2.Frequency;
 }

 public class MapComponent_PlanarThinness : MapComponent {
  public float value;public bool active;public int lastDaily=-1,whisperReady,incursionReady;
  public MapComponent_PlanarThinness(Map map):base(map){}
  public override void MapComponentTick(){
   if(!active)return;
   int now=Find.TickManager.TicksGame;if(lastDaily<0){lastDaily=now;return;}
   if(now-lastDaily<GenDate.TicksPerDay)return;lastDaily=now;Daily();
  }
  public int Glyphs=>Math.Min(Thinness.WardCap,MapComponent_Outsiders.Of(map)?.glyphs.Count(g=>g.Spawned)??0);
  public int AbyssRifts=>PortalEvent.Rifts(map).Count(r=>r.Comp?.plane?.holyClosable==true && !r.Comp.closed);
  public int Relics{get{int n=0;if(AbyssItems.Demonomicon!=null)n+=map.listerThings.ThingsOfDef(AbyssItems.Demonomicon).Count;if(AbyssItems.GorgedFang!=null)n+=map.listerThings.ThingsOfDef(AbyssItems.GorgedFang).Count;return n;}}
  public float DailyDelta=>Thinness.RiftDaily*AbyssRifts+Thinness.ItemDaily*Relics-Thinness.DailyDecay-Thinness.WardDaily*Glyphs;
  // 返回触发的事件（null = 没有）。
  public string Daily(){
   value=Mathf.Clamp(value+DailyDelta,0f,Thinness.Max);
   if(!AbyssSettings.Enabled || !map.IsPlayerHome)return null;
   int now=Find.TickManager.TicksGame;float f=Thinness.Frequency;
   if(now>=incursionReady && Roll()<Thinness.IncursionChance(value)*f && Fire("LP_AbyssIncursion")){incursionReady=now+Thinness.EventCooldownDays*GenDate.TicksPerDay;value=Mathf.Max(0f,value-Thinness.EventDrop);return "incursion";}
   if(now>=whisperReady && Roll()<Thinness.WhisperChance(value)*f && Fire("LP_AbyssWhisper")){whisperReady=now+Thinness.EventCooldownDays*GenDate.TicksPerDay;value=Mathf.Max(0f,value-Thinness.EventDrop);return "whisper";}
   return null;
  }
  static float Roll()=>Thinness.TestRoll??Rand.Value;
  bool Fire(string name){
   var def=DefDatabase<IncidentDef>.GetNamedSilentFail(name);if(def==null)return false;
   var parms=StorytellerUtility.DefaultParmsNow(def.category,map);parms.forced=true;
   return def.Worker.CanFireNow(parms) && def.Worker.TryExecute(parms);
  }
  public override void MapComponentOnGUI(){
   if(!active || LichMod.Settings?.abyssShowThinness!=true || Find.CurrentMap!=map || WorldRendererUtility.WorldSelected)return;
   var r=new Rect(UI.screenWidth/2f-90f,6f,180f,24f);
   Widgets.DrawBoxSolid(r,new Color(.1f,.05f,.12f,.6f));Text.Anchor=TextAnchor.MiddleCenter;Text.Font=GameFont.Small;
   Widgets.Label(r,"位面薄弱度 "+value.ToString("0"));Text.Anchor=TextAnchor.UpperLeft;
   TooltipHandler.TipRegion(r,"每天变化 "+DailyDelta.ToString("+0.#;-0.#")+"。≥20 可能有深渊低语，≥60 可能有深渊入侵。驱魔结界每个 -1/天（最多 5 个）。");
  }
  public override void ExposeData(){
   Scribe_Values.Look(ref value,"lpThinness");Scribe_Values.Look(ref active,"lpThinActive");Scribe_Values.Look(ref lastDaily,"lpThinDaily",-1);
   Scribe_Values.Look(ref whisperReady,"lpWhisperReady");Scribe_Values.Look(ref incursionReady,"lpIncursionReady");
  }
 }
}
