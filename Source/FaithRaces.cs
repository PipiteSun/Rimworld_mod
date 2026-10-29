using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.39.0 阿斯莫与提夫林（Defs/Faith_Races.xml）。种族按本源基因判断：天界之责 = 阿斯莫，魔鬼血统 = 提夫林。
 // 生成时（含派系、开局、神选转换）把默认的启示/肤色/发色在同类里按小人编号重新挑一个；神选仪式只换异种基因、内生基因保留。
 public static class FaithRaces {
  public const float UndeadBane=1.25f,PactDamage=1.2f,RebukeChance=.25f,RebukeDamage=10f,ConsumptionRadius=2f,ConsumptionDamage=2f,ShroudRadius=4f,CelestialChildChance=.1f,CelestialChildFavor=60f;
  public const int RebukeCooldown=600,PactTicks=60000,OverdrawTicks=30000,ShroudTicks=480,StrangerTicks=3600000;
  public static readonly string[] Revelations={"LP_Gene_RadiantSoul","LP_Gene_RadiantConsumption","LP_Gene_NecroticShroud"};
  public static readonly string[] AasimarSkins={"LP_SkinAasimarGold","LP_SkinAasimarSilver"},AasimarHair={"LP_HairAasimarGold","LP_HairAasimarSilver"},TieflingSkins={"Skin_DeepRed","Skin_Purple"};
  static GeneDef lineage,blood,rebuke;static XenotypeDef aasimar,tiefling;
  public static GeneDef Lineage=>lineage??(lineage=DefDatabase<GeneDef>.GetNamedSilentFail("LP_Gene_CelestialLineage"));
  public static GeneDef Blood=>blood??(blood=DefDatabase<GeneDef>.GetNamedSilentFail("LP_Gene_FiendishBlood"));
  public static GeneDef Rebuke=>rebuke??(rebuke=DefDatabase<GeneDef>.GetNamedSilentFail("LP_Gene_HellishRebuke"));
  public static XenotypeDef Aasimar=>aasimar??(aasimar=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_Aasimar"));
  public static XenotypeDef Tiefling=>tiefling??(tiefling=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_Tiefling"));
  public static bool IsAasimar(Pawn p)=>Lineage!=null && p?.genes!=null && p.genes.HasActiveGene(Lineage);
  public static bool IsTiefling(Pawn p)=>Blood!=null && p?.genes!=null && p.genes.HasActiveGene(Blood);

  // ---------- 神选仪式的血脉转换 ----------
  public static XenotypeDef ConversionFor(LP_DeityDef god)=>god==null?null:god.Good?Aasimar:god.Evil?Tiefling:null;
  public static string CannotConvert(Pawn p){
   if(p==null)return "没有人";
   if(Items.IsLich(p))return "巫妖的身体不再属于凡人";
   if(MindFlayer.IsMindFlayer(p))return "夺心魔的身体不能改换血脉";
   if(Races124.IsGenie(p))return "巨灵的身体不能改换血脉";
   if(Races124.IsTitan(p))return "泰坦的身体不能改换血脉";
   if(FrostGiant.Active(p) || FireGiant.Active(p))return "巨人的血脉不能改换";
   if(NetherilAscension.IsArcaneDemigod(p))return "奥术半神的身体不能改换血脉";
   if(p.genes==null)return "没有基因";
   return null;
  }
  public static string ConversionBlocked(Pawn p,LP_DeityDef god){
   var x=ConversionFor(god);if(x==null)return god?.label+"不改换信徒的血脉";
   if(Faith.Planetouched!=null && !Faith.Planetouched.IsFinished)return "需要先研究“天界与地狱血统”";
   if(p.genes?.Xenotype==x)return p.LabelShort+"已经是"+x.label+"了";
   return CannotConvert(p);
  }
  // 只替换异种基因并设异种型；内生基因保留。
  public static bool Convert(Pawn p,XenotypeDef x){
   if(p?.genes==null || x==null || CannotConvert(p)!=null)return false;
   foreach(var g in p.genes.Xenogenes.ToList())p.genes.RemoveGene(g);
   p.genes.SetXenotypeDirect(x);
   foreach(var d in x.genes)p.genes.AddGene(d,true);
   Reroll(p,true);
   p.Drawer?.renderer?.SetAllGraphicsDirty();
   return true;
  }

  // ---------- 生成 ----------
  public static void OnGenerated(Pawn p){if(p?.genes!=null && (p.genes.Xenotype==Aasimar || p.genes.Xenotype==Tiefling))Reroll(p,false);}
  // 把默认的（列表里第一个）换成同类里按编号挑出来的那个。
  public static void Reroll(Pawn p,bool xeno){
   if(p.genes.Xenotype==Aasimar){Swap(p,Revelations,1);Swap(p,AasimarSkins,2);Swap(p,AasimarHair,3);}
   else if(p.genes.Xenotype==Tiefling)Swap(p,TieflingSkins,4);
  }
  static void Swap(Pawn p,string[] names,int salt){
   var defs=names.Select(n=>DefDatabase<GeneDef>.GetNamedSilentFail(n)).Where(d=>d!=null).ToList();if(defs.Count<2)return;
   var own=p.genes.GenesListForReading.FirstOrDefault(g=>g.def==defs[0]);if(own==null)return;
   var pick=defs[Mathf.Clamp((int)(Rand.ValueSeeded(p.thingIDNumber^(0x5A17*salt))*defs.Count),0,defs.Count-1)];if(pick==defs[0])return;
   bool xeno=p.genes.Xenogenes.Contains(own);p.genes.RemoveGene(own);p.genes.AddGene(pick,xeno);
  }
  // 天界之子：守护神善神且 ≥60，新生儿 10% 阿斯莫；恶神 ≥60，10% 提夫林。
  public static XenotypeDef CelestialChildFor(LP_DeityDef patron)=>patron==null || FaithUtility.GetFavor(patron)<CelestialChildFavor?null:ConversionFor(patron);
  public static bool MakeCelestialChild(Pawn baby,XenotypeDef x){
   if(baby?.genes==null || x==null)return false;
   baby.genes.SetXenotype(x);Reroll(baby,false);
   Find.LetterStack.ReceiveLetter("天界之子",baby.LabelShort+"降生时身上带着"+(x==Aasimar?"天界的光":"九狱的印记")+"："+FaithUtility.Patron?.label+"的神力让孩子成了"+x.label+"。",LetterDefOf.PositiveEvent,baby);
   return true;
  }

  // ---------- 能力 ----------
  public static float HealingAmount(Pawn caster){int med=caster.skills?.GetSkill(SkillDefOf.Medicine)?.Level??0,soc=caster.skills?.GetSkill(SkillDefOf.Social)?.Level??0;return med/2f+soc/2f;}
  public static int HealingHands(Pawn caster,Pawn t){
   if(t==null)return 0;float a=HealingAmount(caster);int n=0;
   foreach(var i in t.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>!x.IsPermanent()).ToList()){i.Heal(a);n++;}
   if(t.Spawned)FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,.8f);Items.Mote(t,"治疗之手",new Color(1f,.9f,.6f));
   return n;
  }
  public static int Shroud(Pawn c){
   int n=0;if(c?.Map==null)return 0;
   foreach(var o in Races124.PawnsNear(c.Map,c.Position,ShroudRadius).Where(o=>o!=c && !o.Dead && !o.Downed && o.HostileTo(c) && o.RaceProps.IsFlesh && !Items.Undead(o)).ToList())if(Miracles.Flee(o,ShroudTicks,"亡者裹布"))n++;
   if(c.Spawned)FleckMaker.Static(c.Position,c.Map,FleckDefOf.PsycastAreaEffect,ShroudRadius);
   return n;
  }
  public static void Pact(Pawn c){
   var t=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_InfernalBargain");if(t!=null)c.needs?.mood?.thoughts?.memories?.TryGainMemory(t);
   Items.Give(c,"LP_InfernalPactActive",PactTicks);Items.Mote(c,"魔鬼契约",new Color(.85f,.3f,.25f));
  }
  [ThreadStatic] static bool rebuking;
  public static bool TryRebuke(Pawn victim,DamageInfo dinfo,float dealt){
   if(rebuking || dealt<=0f || victim==null || victim.Dead || !(dinfo.Instigator is Pawn a) || a==victim || a.Dead || dinfo.Def==null || dinfo.Def.isRanged || dinfo.Def.isExplosive)return false;
   if(Rebuke==null || !victim.genes.HasActiveGene(Rebuke) || !victim.Spawned || !a.Spawned || !a.Position.InHorDistOf(victim.Position,1.9f))return false;
   var g=victim.genes.GetGene(Rebuke) as Gene_HellishRebuke;int now=Find.TickManager.TicksGame;if(g!=null && now<g.readyTick)return false;
   if(Rand.Value>=RebukeChance)return false;
   if(g!=null)g.readyTick=now+RebukeCooldown;
   rebuking=true;try{a.TakeDamage(new DamageInfo(DamageDefOf.Burn,RebukeDamage,0f,-1f,victim));}finally{rebuking=false;}
   Items.Mote(victim,"地狱叱喝",new Color(1f,.4f,.2f));
   return true;
  }
 }
 public class CompProperties_Planetouched : CompProperties_AbilityEffect {public string effect;public CompProperties_Planetouched(){compClass=typeof(CompAbilityEffect_Planetouched);}}
 public class CompAbilityEffect_Planetouched : CompAbilityEffect {
  public new CompProperties_Planetouched Props=>(CompProperties_Planetouched)props;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   base.Apply(target,dest);var c=parent.pawn;
   switch(Props.effect){case "HealingHands":FaithRaces.HealingHands(c,target.Pawn);break;case "NecroticShroud":FaithRaces.Shroud(c);break;case "InfernalPact":FaithRaces.Pact(c);break;}
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   if(!base.Valid(target,throwMessages))return false;
   if(Props.effect=="HealingHands" && (target.Pawn==null || target.Pawn.HostileTo(parent.pawn))){if(throwMessages)Messages.Message("只能治疗友军。",MessageTypeDefOf.RejectInput,false);return false;}
   return true;
  }
 }
 public class Gene_HellishRebuke : Gene {public int readyTick;public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref readyTick,"lpReadyTick");}}
 // 光辉消耗：开着时 2 格内敌人每秒 2 神圣伤害，自己每 10 秒 1 点烧伤。
 public class Gene_RadiantConsumption : Gene {
  public bool on;int acc,self;
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(pawn.Faction?.IsPlayer!=true || pawn.Dead)yield break;
   yield return new Command_Toggle{defaultLabel="光辉消耗",defaultDesc="开着时2格内的敌人每秒受2点神圣伤害（不死者与邪魔×2），自己每10秒受1点烧伤。",icon=ContentFinder<Texture2D>.Get("Faith/Abilities/RadiantConsumption",false)??BaseContent.BadTex,isActive=()=>on,toggleAction=()=>on=!on};
  }
  public override void TickInterval(int delta){
   base.TickInterval(delta);if(!on || !Active || pawn.Dead || !pawn.Spawned || pawn.Downed)return;
   acc+=delta;self+=delta;if(acc<60)return;acc=0;Pulse();
   if(self>=600){self=0;pawn.TakeDamage(new DamageInfo(DamageDefOf.Burn,1f,0f,-1f));}
  }
  public int Pulse(){
   int n=0;foreach(var o in Races124.PawnsNear(pawn.Map,pawn.Position,FaithRaces.ConsumptionRadius).Where(o=>o!=pawn && !o.Dead && o.HostileTo(pawn)).ToList()){o.TakeDamage(new DamageInfo(Faith.Radiant,FaithRaces.ConsumptionDamage*(Faith.HolyVulnerable(o)?2f:1f),0f,-1f,pawn));n++;}
   if(n>0)FleckMaker.ThrowLightningGlow(pawn.DrawPos,pawn.Map,.6f);
   return n;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref on,"lpOn");}
 }
 public class Hediff_InfernalPact : HediffWithComps {
  public override void PostRemoved(){base.PostRemoved();if(pawn!=null && !pawn.Dead && pawn.health!=null)Items.Give(pawn,"LP_PactOverdraw",FaithRaces.OverdrawTicks);}
 }

 // ---------- 心情 ----------
 public class ThoughtWorker_AasimarSeesUndead : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!p.Spawned || !FaithRaces.IsAasimar(p))return false;bool lich=def.defName=="LP_AasimarSeesLich";
   foreach(var o in p.Map.mapPawns.AllPawnsSpawned)if(o!=p && !o.Dead && (lich?Items.IsLich(o):Necro.Construct(o)!=null))return true;
   return false;
  }
 }
 public class ThoughtWorker_LichSeesAasimar : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){if(!p.Spawned || !Items.IsLich(p))return false;foreach(var o in p.Map.mapPawns.AllPawnsSpawned)if(o!=p && !o.Dead && FaithRaces.IsAasimar(o))return true;return false;}
 }
 public class ThoughtWorker_TieflingStranger : ThoughtWorker {
  public static bool New(Pawn t)=>(t.records?.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal)??0)<FaithRaces.StrangerTicks;
  protected override ThoughtState CurrentStateInternal(Pawn p){if(!p.Spawned || p.Faction?.IsPlayer!=true || FaithRaces.IsTiefling(p))return false;return AnyNewTiefling(p.Map);}
  // 1.58.6：看的人自己不是提夫林，所以结果只取决于地图（原来每个殖民者都把全体殖民者的基因扫一遍）；同一张地图同一刻只算一次。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<int,(int tick,Map map,bool any)> memo=new System.Collections.Concurrent.ConcurrentDictionary<int,(int,Map,bool)>();
  static bool AnyNewTiefling(Map map){
   int now=Find.TickManager.TicksGame;if(memo.TryGetValue(map.uniqueID,out var m) && m.tick==now && m.map==map)return m.any;
   bool any=false;foreach(var o in map.mapPawns.FreeColonistsSpawned)if(FaithRaces.IsTiefling(o) && New(o)){any=true;break;}
   memo[map.uniqueID]=(now,map,any);return any;
  }
 }
 public class ThoughtWorker_AasimarTiefling : ThoughtWorker {
  protected override ThoughtState CurrentSocialStateInternal(Pawn p,Pawn other){
   if(!FaithRaces.IsAasimar(p) || !FaithRaces.IsTiefling(other))return false;
   bool friends=!ThoughtWorker_TieflingStranger.New(p) && !ThoughtWorker_TieflingStranger.New(other) && p.Faction?.IsPlayer==true && other.Faction?.IsPlayer==true;
   return ThoughtState.ActiveAtStage(friends?1:0);
  }
 }

 // ---------- Harmony ----------
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.PostApplyDamage))]
 public static class FaithHellishRebuke {static void Postfix(Pawn ___pawn,DamageInfo dinfo,float totalDamageDealt){if(___pawn?.genes!=null)FaithRaces.TryRebuke(___pawn,dinfo,totalDamageDealt);}}
 [HarmonyPatch(typeof(PregnancyUtility),nameof(PregnancyUtility.ApplyBirthOutcome))]
 public static class FaithCelestialChild {
  static void Postfix(Thing __result){
   if(!(__result is Pawn baby) || baby.Faction?.IsPlayer!=true || !Faith.Enabled)return;
   var x=FaithRaces.CelestialChildFor(FaithUtility.Patron);if(x!=null && Rand.Chance(FaithRaces.CelestialChildChance))FaithRaces.MakeCelestialChild(baby,x);
  }
 }
}
