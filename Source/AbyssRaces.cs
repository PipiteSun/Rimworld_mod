using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.54.0 深渊种族（Defs/Abyss_Races.xml，规格第四节）：劣魔、夸塞魔（动物魔宠）、巴路古拉、哈兹魔、魅魔/梦魔、坎比恩、纳巴苏、影魔。
 // 恶魔共用 LP_AbyssCore（判定 + 外来者扩展）；“不吃不睡”拆成 LP_AbyssUndying，魅魔不带（它要睡觉、同床才能梦境吸取）。
 public static class AbyssRaces {
  public const string Dretch="LP_Dretch",Barlgura="LP_Barlgura",Hezrou="LP_Hezrou",Succubus="LP_Succubus",Cambion="LP_Cambion",Nabassu="LP_Nabassu",ShadowDemon="LP_ShadowDemon",Quasit="LP_Quasit";
  public const float StenchRadius=2f,HezrouRadius=3f,LeapRange=8f,GlideRange=6f,SeerRadius=8f,AasimarRadius=5f,AltarRadius=5f,HolySymbolRadius=12f;
  public const int HezrouInterval=300,HezrouDC=13,NauseaTicks=600,HezrouImmuneTicks=3600,LeapDC=13,LeapStun=60,StillTicks=180,GazeDC=14,PetrifyTicks=600,KissCharmTicks=3600,KissImmuneTicks=GenDate.TicksPerDay;
  public const float KissResistance=15f,DreamRest=.2f,DreamHeal=.1f,NabassuRiseChance=.3f,DevourLayerMax=10f,GuiseOff=0f;
  public const int DreamHour=6,HungerDays=3,DevourRiseTicks=GenDate.TicksPerDay;
  static FactionDef hordeDef;static XenotypeDef cambion,succubus;
  public static FactionDef HordeDef=>hordeDef??(hordeDef=DefDatabase<FactionDef>.GetNamedSilentFail("LP_AbyssHorde"));
  public static XenotypeDef CambionXeno=>cambion??(cambion=DefDatabase<XenotypeDef>.GetNamedSilentFail(Cambion));
  public static XenotypeDef SuccubusXeno=>succubus??(succubus=DefDatabase<XenotypeDef>.GetNamedSilentFail(Succubus));
  public static Faction Horde(){var f=Factions.EnsureHidden(HordeDef);if(f!=null && !f.HostileTo(Faction.OfPlayer))Factions.SetRelation(f,Faction.OfPlayer,-100);return f;}
  public static bool Is(Pawn p,string key){
   if(p==null)return false;
   if(key==Quasit)return p.def.defName==Quasit;
   return p.genes?.Xenotype?.defName==key;
  }
  public static bool IsSuccubus(Pawn p)=>Races124.Has(p,"LP_SuccubusNature");
  public static bool IsCambion(Pawn p)=>Races124.Has(p,"LP_CambionBlood");
  public static void ClearCache(Pawn p){}
  public static Gene_SuccubusGuise Guise(Pawn p)=>p?.genes?.GenesListForReading.OfType<Gene_SuccubusGuise>().FirstOrDefault(g=>g.Active);
  public static bool Guised(Pawn p){var g=Guise(p);return g!=null && g.on;}

  // ---------- 生成 ----------
  public static Pawn Make(string kind,Faction f,int tile,Gender? gender=null){
   var k=DefDatabase<PawnKindDef>.GetNamedSilentFail(kind);if(k==null)return null;
   var req=new PawnGenerationRequest(k,f,PawnGenerationContext.NonPlayer,tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:k.RaceProps.Humanlike,fixedGender:gender,colonistRelationChanceFactor:0f);
   try{return PawnGenerator.GeneratePawn(req);}catch(Exception e){Log.Warning("[DND边缘大陆] 生成 "+kind+" 失败："+e.Message);return null;}
  }
  // 魅魔生成：60% 普通肤色（去掉绯红肤色基因）。
  public static void OnGenerated(Pawn p){
   if(!IsSuccubus(p))return;
   var skin=Races124.Gene("LP_SkinSuccubus");var g=skin==null?null:p.genes.GenesListForReading.FirstOrDefault(x=>x.def==skin);
   if(g!=null && Rand.ValueSeeded(p.thingIDNumber^0x5C0B)<.6f){p.genes.RemoveGene(g);p.story.skinColorOverride=null;}
  }
  // 化魔：只换异种基因（照 FaithRaces.Convert 的写法，但不再查 CannotConvert，调用方已经查过）。
  public static bool MakeCambion(Pawn p)=>ConvertTo(p,CambionXeno);
  public static bool ConvertTo(Pawn p,XenotypeDef x){
   if(p?.genes==null || x==null)return false;
   foreach(var g in p.genes.Xenogenes.ToList())p.genes.RemoveGene(g);
   p.genes.SetXenotypeDirect(x);
   foreach(var d in x.genes)p.genes.AddGene(d,true);
   if(x==SuccubusXeno)OnGenerated(p);
   p.Drawer?.renderer?.SetAllGraphicsDirty();
   return true;
  }

  // ---------- 劣魔 / 哈兹魔：恶臭 ----------
  public static bool NearDretch(Pawn p){
   if(!p.Spawned || Outsiders.Is(p))return false;var reg=MapComponent_Outsiders.Of(p.Map);if(reg==null || reg.list.Count==0)return false;
   foreach(var o in reg.list)if(o!=p && !o.Dead && Is(o,Dretch) && o.Position.InHorDistOf(p.Position,StenchRadius))return true;
   return false;
  }
  // 每 300 刻：3 格内敌对人形做体质豁免 DC 13，失败恶心 10 秒（不叠加），成功 1 分钟内不再检定。
  public static int HezrouStench(Pawn h,Dictionary<int,int> immune){
   if(h?.Map==null || h.Downed || h.Dead)return 0;var map=h.Map;int now=Find.TickManager.TicksGame,n=0;
   foreach(var c in GenRadial.RadialCellsAround(h.Position,HezrouRadius,true)){
    if(!c.InBounds(map))continue;var list=c.GetThingList(map);
    for(int i=0;i<list.Count;i++){
     if(!(list[i] is Pawn p) || p==h || p.Dead || !p.RaceProps.Humanlike || Outsiders.Is(p) || !p.HostileTo(h))continue;
     if(immune.TryGetValue(p.thingIDNumber,out int until) && now<until)continue;
     if(Items.Has(p,"LP_Nauseated"))continue;
     if(Legendary.Save(p,Adventurer.Con,HezrouDC,h)){immune[p.thingIDNumber]=now+HezrouImmuneTicks;continue;}
     Items.Give(p,"LP_Nauseated",NauseaTicks);n++;
    }
   }
   return n;
  }
  // ---------- 巴路古拉：跃击 ----------
  public static IntVec3 LandingNear(Pawn caster,LocalTargetInfo t){
   var map=caster.Map;var center=t.Cell;
   if(t.Thing==null)return Githyanki.CannotStep(caster,center)==null?center:IntVec3.Invalid;
   return GenAdj.CellsAdjacent8Way(t.Thing).Where(c=>Githyanki.CannotStep(caster,c)==null).OrderBy(c=>c.DistanceToSquared(caster.Position)).FirstOrDefault(IntVec3.Invalid);
  }
  public static int LeapImpact(Pawn caster){
   int n=0;var map=caster.Map;
   foreach(var c in GenAdj.CellsAdjacent8Way(caster)){if(!c.InBounds(map))continue;foreach(var t in c.GetThingList(map).ToList())if(t is Pawn p && !p.Dead && p.HostileTo(caster) && !Legendary.Save(p,Adventurer.Dex,LeapDC,caster)){p.stances?.stunner?.StunFor(LeapStun,caster,false,true,false);n++;}}
   for(int i=0;i<6;i++)FleckMaker.ThrowDustPuffThick(caster.Position.ToVector3Shifted(),map,1.6f,new Color(.45f,.35f,.25f));
   return n;
  }
  // ---------- 纳巴苏：石化凝视 ----------
  public static bool Gaze(Pawn caster,Pawn t){
   if(t==null || t.Dead)return false;
   if(Legendary.Save(t,Adventurer.Con,GazeDC,caster))return false;
   Items.Give(t,"LP_NabassuPetrify",PetrifyTicks);Items.Mote(t,"石化中",new Color(.7f,.7f,.68f));return true;
  }
  // 纳巴苏杀死的人形：灵魂被吞噬（不能复活），饱魂 +1 层（最多 10），24 小时后 30% 起身成劣魔。
  public static bool Devour(Pawn nabassu,Pawn victim){
   if(nabassu==null || victim==null || !Is(nabassu,Nabassu) || !victim.RaceProps.Humanlike || Items.Has(victim,"LP_SoulDevoured"))return false;
   var d=Items.Hediff("LP_SoulDevoured");if(d!=null)victim.health.AddHediff(d);
   AddGorge(nabassu,1);
   var st=AbyssState.Get;var corpse=victim.Corpse;
   if(st!=null && corpse!=null && Outsiders.Roll<NabassuRiseChance)st.pending.Add(new AbyssPending{corpse=corpse,tick=Find.TickManager.TicksGame+DevourRiseTicks,reason="devour"});
   Messages.Message(nabassu.LabelShort+"吞掉了"+victim.LabelShort+"的灵魂。",victim.Corpse!=null?(LookTargets)victim.Corpse:nabassu,MessageTypeDefOf.NegativeEvent,false);
   return true;
  }
  public static int Gorge(Pawn p)=>Mathf.RoundToInt(p?.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_SoulGorged"))?.Severity??0f);
  public static void AddGorge(Pawn p,int n){
   var d=Items.Hediff("LP_SoulGorged");if(d==null || n<=0)return;var h=p.health.hediffSet.GetFirstHediffOfDef(d);
   if(h==null){h=HediffMaker.MakeHediff(d,p);h.Severity=Math.Min(DevourLayerMax,n);p.health.AddHediff(h);}else h.Severity=Math.Min(DevourLayerMax,h.Severity+n);
  }
  public static bool SoulGone(Pawn p)=>Items.Has(p,"LP_SoulDevoured") || Items.Has(p,"LP_SoulMortgaged");

  // ---------- 魅魔 ----------
  public static bool KissImmune(Pawn s,Pawn t){var st=AbyssState.Get;return st!=null && st.kissImmune.TryGetValue(s.thingIDNumber+"_"+t.thingIDNumber,out int u) && Find.TickManager.TicksGame<u;}
  // 魅惑之吻：目标感知豁免，DC = 魅魔的魅力 DC。失败：敌人被魅惑 60 秒；囚犯招募抵抗 -15；殖民者对魅魔 +20 意见。成功：24 小时免疫。
  public static string Kiss(Pawn s,Pawn t){
   if(s==null || t==null || t.Dead || !t.RaceProps.Humanlike)return null;
   if(KissImmune(s,t))return "immune";
   if(Legendary.Save(t,Adventurer.Wis,Legendary.DC(s,Adventurer.Cha),s)){var st=AbyssState.Get;if(st!=null)st.kissImmune[s.thingIDNumber+"_"+t.thingIDNumber]=Find.TickManager.TicksGame+KissImmuneTicks;return "saved";}
   if(t.IsPrisonerOfColony && t.guest!=null){t.guest.resistance=Mathf.Max(0f,t.guest.resistance-KissResistance);Items.Mote(t,"抵抗 -"+KissResistance.ToString("0"),new Color(.9f,.5f,.8f));return "prisoner";}
   if(t.HostileTo(s)){
    var def=DefDatabase<MentalStateDef>.GetNamedSilentFail("LP_DragonCharmed");if(def==null)return "none";
    if(t.mindState.mentalStateHandler.TryStartMentalState(def,"魅惑之吻",forced:true,forceWake:true) && t.MentalState!=null)t.MentalState.forceRecoverAfterTicks=KissCharmTicks;
    Items.Mote(t,"被魅惑了",new Color(.9f,.5f,.8f));return "charmed";
   }
   var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_SuccubusKissed");if(th!=null)t.needs?.mood?.thoughts?.memories?.TryGainMemory(th,s);
   return "friend";
  }
  // 魔鬼之眼（Fiends.Sees）8 格内、阿斯莫 5 格内：伪装中的魅魔现形；来访的魅魔立刻揭晓。只遍历外来者登记表里的魅魔，附近的人按格子查。
  public static int CheckUnmasking(MapComponent_Outsiders reg){
   int n=0;var map=reg.map;
   foreach(var s in reg.list.ToList()){
    bool guised=Guised(s),visitor=Outsiders.Disguised(s);if(!guised && !visitor)continue;if(!s.Spawned || s.Dead)continue;
    var seer=Seer(s,map);if(seer==null)continue;
    if(visitor)AbyssEvents.RevealSuccubus(s,seer.LabelShort+"看穿了它的伪装");
    else{var g=Guise(s);g.Set(false);Messages.Message(seer.LabelShort+"看穿了"+s.LabelShort+"的变形伪装！",s,MessageTypeDefOf.NeutralEvent,false);}
    n++;
   }
   return n;
  }
  static Pawn Seer(Pawn s,Map map){
   foreach(var c in GenRadial.RadialCellsAround(s.Position,SeerRadius,true)){
    if(!c.InBounds(map))continue;var list=c.GetThingList(map);
    for(int i=0;i<list.Count;i++)if(list[i] is Pawn p && p!=s && !p.Dead && !p.Downed && (Fiends.Sees(p) || FaithRaces.IsAasimar(p) && p.Position.InHorDistOf(s.Position,AasimarRadius)))return p;
   }
   return null;
  }
  // 梦境吸取：每晚记下同床（同一张床的另一人）；每天 6 点结算。
  public static void DreamTick(Pawn s,DreamState d){
   if(s==null || s.Dead || !s.Spawned)return;
   int day=GenDate.DaysPassed;
   if(s.InBed()){var bed=s.CurrentBed();if(bed!=null)foreach(var o in bed.CurOccupants)if(o!=s && !o.Dead && o.RaceProps.Humanlike){d.partner=o;d.partnerDay=day;break;}}
   if(GenLocalDate.HourOfDay(s.Map)!=DreamHour || d.settledDay==day)return;
   d.settledDay=day;
   if(d.partner!=null && d.partnerDay>=day-1 && !d.partner.Dead){Drain(s,d.partner);d.hungry=0;}
   else{d.hungry++;if(d.hungry>=HungerDays){var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_SuccubusStarved");if(th!=null)s.needs?.mood?.thoughts?.memories?.TryGainMemory(th);}}
   d.partner=null;
  }
  public static void Drain(Pawn s,Pawn o){
   if(o.needs?.rest!=null)o.needs.rest.CurLevel=Mathf.Max(0f,o.needs.rest.CurLevel-DreamRest);
   AbyssTaint.Add(o,AbyssTaint.DreamGain,"梦境吸取");
   var nm=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_HauntedDreams");if(nm!=null)o.needs?.mood?.thoughts?.memories?.TryGainMemory(nm);
   foreach(var i in s.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>!x.IsPermanent()).ToList())i.Heal(i.Severity*DreamHeal+.5f);
   var sated=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_SuccubusSated");if(sated!=null)s.needs?.mood?.thoughts?.memories?.TryGainMemory(sated);
  }
  // 魅魔不能在圣坛 5 格内工作：圣坛附近的东西对它算“禁止”（只在地图上有魅魔时检查）。
  public static int succubi;
  public static bool NearAltar(Thing t){
   var map=t.MapHeld;if(map==null || Faith.AltarDef==null)return false;var list=map.listerThings.ThingsOfDef(Faith.AltarDef);
   for(int i=0;i<list.Count;i++)if(list[i].Position.InHorDistOf(t.PositionHeld,AltarRadius+1f))return true;
   return false;
  }
  public static bool SeesHolySymbol(Pawn s){
   if(!s.Spawned || Faith.SymbolDef==null)return false;
   foreach(var c in s.Map.mapPawns.FreeColonistsSpawned)if(c!=s && c.Position.InHorDistOf(s.Position,HolySymbolRadius) && Items.Worn(c,Faith.SymbolDef)!=null)return true;
   return false;
  }

  // ---------- 移速（影魔在亮处 ×0.7；敌对外来者在驱魔结界里 ×0.7），按 pawn 缓存 250 刻 ----------
  static readonly Dictionary<int,(int tick,float f)> moveCache=new Dictionary<int,(int,float)>();
  public static float MoveFactor(Pawn p){
   var e=Outsiders.Of(p);if(e==null || !p.Spawned)return 1f;
   int now=Find.TickManager.TicksGame;
   lock(moveCache){if(moveCache.TryGetValue(p.thingIDNumber,out var c) && now-c.tick<250 && now>=c.tick)return c.f;}
   float f=1f;
   if(e.lightBane && Outsiders.Lit(p))f*=Outsiders.LightSlow;
   if(p.Faction?.IsPlayer!=true && p.HostileTo(Faction.OfPlayer)){var reg=MapComponent_Outsiders.Of(p.Map);if(reg!=null && reg.glyphs.Count>0 && reg.Warded(p.Position))f*=Outsiders.WardSlow;}
   lock(moveCache){moveCache[p.thingIDNumber]=(now,f);}
   return f;
  }
 }
 public class DreamState : IExposable {
  public Pawn partner;public int partnerDay=-9,settledDay=-1,hungry;
  public void ExposeData(){Scribe_References.Look(ref partner,"partner");Scribe_Values.Look(ref partnerDay,"partnerDay",-9);Scribe_Values.Look(ref settledDay,"settledDay",-1);Scribe_Values.Look(ref hungry,"hungry");}
 }

 // ---------- 基因 ----------
 public class Gene_HezrouStench : Gene {
  int ticks;readonly Dictionary<int,int> immune=new Dictionary<int,int>();
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<AbyssRaces.HezrouInterval)return;ticks=0;if(Active && pawn.Spawned)AbyssRaces.HezrouStench(pawn,immune);}
 }
 // 静立隐形：3 秒不移动、不攻击 → 隐身；移动 / 攻击打破。
 public class Gene_BarlguraStill : Gene {
  int ticks,still;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((ticks+=delta)<60)return;int d=ticks;ticks=0;if(!Active || !pawn.Spawned)return;
   int now=Find.TickManager.TicksGame;bool calm=!pawn.Downed && !pawn.pather.Moving && now-pawn.LastAttackTargetTick>AbyssRaces.StillTicks && now-pawn.mindState.lastHarmTick>AbyssRaces.StillTicks;
   still=calm?still+d:0;
   bool want=still>=AbyssRaces.StillTicks;bool has=Items.Has(pawn,"LP_BarlguraHidden");
   if(want && !has)Items.Give(pawn,"LP_BarlguraHidden");else if(!want && has)Items.Remove(pawn,"LP_BarlguraHidden");
  }
 }
 // 变形伪装（开关）：隐藏角 / 翼 / 尾 / 绯红肤色。
 public class Gene_SuccubusGuise : Gene {
  public bool on;
  public void Set(bool v){
   on=v;var skin=Races124.Gene("LP_SkinSuccubus");
   if(pawn.story!=null){if(on)pawn.story.skinColorOverride=null;else if(skin!=null && pawn.genes.HasActiveGene(skin))pawn.story.skinColorOverride=skin.skinColorOverride;}
   pawn.Drawer?.renderer?.SetAllGraphicsDirty();
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(pawn.Faction?.IsPlayer!=true || pawn.Dead)yield break;
   yield return new Command_Toggle{defaultLabel="变形伪装",defaultDesc="开启时看起来是普通人：角、翼、尾和绯红肤色都藏起来。被魔鬼之眼（8 格）或阿斯莫（5 格）看到会自动解除。",icon=ContentFinder<Texture2D>.Get("Abyss/Abilities/Guise",false)??BaseContent.BadTex,isActive=()=>on,toggleAction=()=>Set(!on)};
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref on,"lpGuise");}
 }
 public class Gene_SuccubusDream : Gene {
  int ticks;public DreamState dream=new DreamState();
  public override void PostAdd(){base.PostAdd();AbyssRaces.succubi++;}
  public override void TickInterval(int delta){base.TickInterval(delta);if((ticks+=delta)<GenDate.TicksPerHour)return;ticks=0;if(Active)AbyssRaces.DreamTick(pawn,dream);}
  public override void ExposeData(){base.ExposeData();Scribe_Deep.Look(ref dream,"lpDream");if(dream==null)dream=new DreamState();if(Scribe.mode==LoadSaveMode.PostLoadInit)AbyssRaces.succubi++;}
 }
 // 恶魔的头部 / 身体附件：伪装时不画。
 public class PawnRenderNodeWorker_AbyssGene : PawnRenderNodeWorker_FlipWhenCrawling {
  public override bool CanDrawNow(PawnRenderNode node,PawnDrawParms parms)=>base.CanDrawNow(node,parms) && !AbyssRaces.Guised(parms.pawn);
 }
 public class PawnRenderNodeWorker_AbyssBody : PawnRenderNodeWorker_AttachmentBody {
  public override bool CanDrawNow(PawnRenderNode node,PawnDrawParms parms)=>base.CanDrawNow(node,parms) && !AbyssRaces.Guised(parms.pawn);
 }

 // ---------- 能力 ----------
 public class CompProperties_AbyssAbility : CompProperties_AbilityEffect {public string effect;public CompProperties_AbyssAbility(){compClass=typeof(CompAbilityEffect_Abyss);}}
 public class CompAbilityEffect_Abyss : CompAbilityEffect {
  public string Effect=>((CompProperties_AbyssAbility)props).effect;
  Pawn Caster=>parent.pawn;
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   if(!base.Valid(target,throwMessages))return false;string why=null;
   switch(Effect){
    case "leap":if(!AbyssRaces.LandingNear(Caster,target).IsValid)why="目标旁边没有能落脚的地方。";break;
    case "glide":why=Githyanki.CannotStep(Caster,target.Cell)??(target.Cell.DistanceTo(Caster.Position)>AbyssRaces.GlideRange+.5f?"超出滑翔距离（6格）。":null);break;
    case "kiss":if(target.Pawn==null || !target.Pawn.RaceProps.Humanlike)why="只能吻人形。";else if(AbyssRaces.KissImmune(Caster,target.Pawn))why=target.Pawn.LabelShort+"刚抵抗过它的魅惑（24 小时内免疫）。";break;
    case "gaze":if(target.Pawn==null)why="需要一个目标。";break;
   }
   if(why!=null && throwMessages)Messages.Message(why,MessageTypeDefOf.RejectInput,false);
   return why==null;
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   base.Apply(target,dest);
   switch(Effect){
    case "leap":{var c=AbyssRaces.LandingNear(Caster,target);if(c.IsValid && Githyanki.Step(Caster,c,0f))AbyssRaces.LeapImpact(Caster);break;}
    case "glide":Githyanki.Step(Caster,target.Cell,0f);break;
    case "kiss":{var r=AbyssRaces.Kiss(Caster,target.Pawn);if(r=="saved")Messages.Message(target.Pawn.LabelShort+"抵抗住了魅惑之吻。",target.Pawn,MessageTypeDefOf.NeutralEvent,false);break;}
    case "gaze":AbyssRaces.Gaze(Caster,target.Pawn);break;
   }
  }
  public override bool AICanTargetNow(LocalTargetInfo target){
   var t=target.Pawn;if(t==null || t.Dead || t.Downed || !t.HostileTo(Caster))return false;float d=t.Position.DistanceTo(Caster.Position);
   switch(Effect){case "leap":return d>=3f && d<=AbyssRaces.LeapRange;case "gaze":return !Items.Has(t,"LP_NabassuPetrify");case "kiss":return !AbyssRaces.KissImmune(Caster,t);}
   return false;
  }
 }

 // ---------- 纳巴苏的饱魂 / 夸塞魔 ----------
 // 饱魂：每层生命 +5%（按受到伤害 ÷(1+5%×层) 算）、伤害 +5%，最多 10 层。
 public class Hediff_SoulGorged : HediffWithComps {
  HediffStage stage;int stageFor=-1;
  public override HediffStage CurStage{get{
   int n=Mathf.RoundToInt(Severity);if(stage!=null && stageFor==n)return stage;stageFor=n;
   stage=new HediffStage{label=n+"层",statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.MeleeDamageFactor,value=1f+.05f*n},new StatModifier{stat=StatDefOf.IncomingDamageFactor,value=1f/(1f+.05f*n)}}};
   return stage;
  }}
  public override string LabelInBrackets=>Mathf.RoundToInt(Severity)+"层";
 }
 // 饱魂之牙：佩戴者每杀一个人形 +1 层（伤害 +1%，最多 10 层，死亡清零）。
 public class Hediff_FangGorged : HediffWithComps {
  HediffStage stage;int stageFor=-1;
  public override HediffStage CurStage{get{
   int n=Mathf.RoundToInt(Severity);if(stage!=null && stageFor==n)return stage;stageFor=n;
   stage=new HediffStage{label=n+"层",statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.MeleeDamageFactor,value=1f+.01f*n}}};
   return stage;
  }}
  public override string LabelInBrackets=>Mathf.RoundToInt(Severity)+"层";
 }
 // 戴着饱魂之牙：深渊腐化每天 +0.2%。
 public class Hediff_FangWearer : HediffWithComps {
  int acc;
  public override void TickInterval(int delta){base.TickInterval(delta);if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;if(!pawn.Dead)AbyssTaint.Add(pawn,AbyssTaint.FangDaily/24f);}
 }
 // 夸塞魔：不战斗时隐身；主人（同地图）心灵敏感度 +10%，腐化每天 +0.3%。
 public class Hediff_QuasitNature : HediffWithComps {
  public Pawn master;int acc,hour;
  public override bool Visible=>true;
  public override string LabelInBrackets=>master==null?null:"主人 "+master.LabelShort;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if(pawn.Dead || !pawn.Spawned)return;
   if((acc+=delta)>=60){acc=0;int now=Find.TickManager.TicksGame;bool fight=now-pawn.LastAttackTargetTick<600 || now-pawn.mindState.lastHarmTick<600;bool has=Items.Has(pawn,"LP_QuasitVeil");
    if(!fight && !has)Items.Give(pawn,"LP_QuasitVeil");else if(fight && has)Items.Remove(pawn,"LP_QuasitVeil");}
   if((hour+=delta)>=GenDate.TicksPerHour){hour=0;
    bool near=master!=null && !master.Dead && master.MapHeld==pawn.Map;
    if(master!=null && !master.Dead){Races124.Ensure(master,Items.Hediff("LP_QuasitBond"),near);if(near)AbyssTaint.Add(master,AbyssTaint.QuasitDaily/24f);}
   }
  }
  public override void Notify_PawnDied(DamageInfo? dinfo,Hediff culprit=null){base.Notify_PawnDied(dinfo,culprit);if(master!=null && !master.Dead)Races124.Ensure(master,Items.Hediff("LP_QuasitBond"),false);}
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref master,"lpMaster");}
 }
 public static class Quasits {
  public static Pawn Of(Pawn master)=>master?.MapHeld==null?null:master.MapHeld.mapPawns.AllPawnsSpawned.FirstOrDefault(p=>AbyssRaces.Is(p,AbyssRaces.Quasit) && !p.Dead && p.health.hediffSet.hediffs.OfType<Hediff_QuasitNature>().Any(h=>h.master==master));
  public static Pawn Summon(Pawn master,IntVec3 cell,Map map){
   var p=AbyssRaces.Make("LP_QuasitFamiliar",Faction.OfPlayer,map.Tile);if(p==null)return null;
   GenSpawn.Spawn(p,cell,map);
   var h=(Hediff_QuasitNature)HediffMaker.MakeHediff(Items.Hediff("LP_QuasitNature"),p);h.master=master;p.health.AddHediff(h);
   if(master.relations!=null && !master.relations.DirectRelationExists(PawnRelationDefOf.Bond,p))master.relations.AddDirectRelation(PawnRelationDefOf.Bond,p);
   if(p.playerSettings!=null){p.playerSettings.Master=master;p.playerSettings.followDrafted=true;p.playerSettings.followFieldwork=true;}
   return p;
  }
 }

 // ---------- 心情 ----------
 public class ThoughtWorker_DretchStench : ThoughtWorker {protected override ThoughtState CurrentStateInternal(Pawn p)=>AbyssRaces.NearDretch(p);}
 public class ThoughtWorker_SuccubusHolySymbol : ThoughtWorker {protected override ThoughtState CurrentStateInternal(Pawn p)=>AbyssRaces.IsSuccubus(p) && !AbyssRaces.Guised(p) && AbyssRaces.SeesHolySymbol(p);}

 // ---------- StatPart ----------
 public class StatPart_AbyssMove : StatPart {
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p){float f=AbyssRaces.MoveFactor(p);if(f!=1f)val*=f;}}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;float f=AbyssRaces.MoveFactor(p);return f==1f?null:"深渊（光照 / 驱魔结界）：×"+f.ToStringPercent();}
 }
 [StaticConstructorOnStartup]
 public static class AbyssStartup {
  static AbyssStartup(){
   var s=StatDefOf.MoveSpeed;if(s.parts==null)s.parts=new List<StatPart>();s.parts.Add(new StatPart_AbyssMove{parentStat=s});
   // 冒险者种族加值（1.48.0 的表，力敏体智感魅）。
   Adventurer.Racial[AbyssRaces.Cambion]=new[]{0,1,0,0,0,2};Adventurer.Racial[AbyssRaces.Succubus]=new[]{0,1,0,0,0,2};
   foreach(var k in new[]{AbyssRaces.Dretch,AbyssRaces.Barlgura,AbyssRaces.Hezrou,AbyssRaces.Nabassu,AbyssRaces.ShadowDemon})Adventurer.Racial[k]=k==AbyssRaces.Dretch?new[]{0,0,1,0,0,0}:new[]{2,0,1,0,0,0};
   Items.ColdIronBaneHooks.Add(AbyssRaces.IsCambion); // 坎比恩：寒铁 ×1.25（沿用精灵 / 灵能链接者那一档）
  }
 }

 // ---------- Harmony ----------
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class AbyssRaceOnGenerated {static void Postfix(Pawn __result){if(__result?.genes!=null)AbyssRaces.OnGenerated(__result);}}
 // 魅魔 / 梦魔与人生育：孩子是坎比恩（排在天界之子后面，覆盖它）。
 [HarmonyPatch(typeof(PregnancyUtility),nameof(PregnancyUtility.ApplyBirthOutcome))]
 public static class SuccubusCambionChild {
  [HarmonyPriority(Priority.Low)]
  static void Postfix(Thing __result){
   if(!(__result is Pawn baby) || baby.genes==null || AbyssRaces.CambionXeno==null)return;
   var mom=baby.GetMother();var dad=baby.GetFather();
   if(AbyssRaces.IsSuccubus(mom) || AbyssRaces.IsSuccubus(dad)){baby.genes.SetXenotype(AbyssRaces.CambionXeno);baby.Drawer?.renderer?.SetAllGraphicsDirty();}
  }
 }
 [HarmonyPatch(typeof(ForbidUtility),nameof(ForbidUtility.IsForbidden),new[]{typeof(Thing),typeof(Pawn)})]
 public static class SuccubusAltarForbid {
  static void Postfix(Thing t,Pawn pawn,ref bool __result){
   if(__result || AbyssRaces.succubi<=0 || pawn==null || t==null || !MapComponent_Outsiders.AnySpawnedSuccubus() || Outsiders.Of(pawn)==null || !AbyssRaces.IsSuccubus(pawn) || AbyssRaces.Guised(pawn))return; // 热路径：先查缓存的外来者扩展
   if(AbyssRaces.NearAltar(t))__result=true;
  }
 }
 // 噬魂 / 灵魂抵押：任何复活都失败。
 [HarmonyPatch(typeof(ResurrectionUtility),nameof(ResurrectionUtility.TryResurrect))]
 public static class SoulGoneResurrect {
  static bool Prefix(Pawn pawn,ref bool __result){
   if(pawn==null || !AbyssRaces.SoulGone(pawn))return true;
   __result=false;if(pawn.Faction?.IsPlayer==true)Messages.Message(pawn.LabelShort+"的灵魂已被吞噬（或抵押给了深渊），无法复活。",MessageTypeDefOf.RejectInput,false);
   return false;
  }
 }
 [HarmonyPatch(typeof(Souls),nameof(Souls.Unfit))]
 public static class SoulGoneOffering {static void Postfix(Thing thing,ref string __result){if(__result==null && thing is Corpse c && AbyssRaces.SoulGone(c.InnerPawn))__result="灵魂已被吞噬，没有可汲取的东西。";}}
}
