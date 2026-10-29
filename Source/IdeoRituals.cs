using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.56.0「文化之神」第五、六节：神祇庆典、灰袍葬礼、神官和大祭司的圣坛按钮、血祭、神迹布道的神眷消耗。
 public class LP_FestivalExtension : DefModExtension {public LP_DeityDef deity;public string unforgettable;}

 // 庆典只能在圣坛（LP_FaithAltar）举行。
 public class RitualObligationTargetWorker_FaithAltar : RitualObligationTargetFilter {
  public RitualObligationTargetWorker_FaithAltar(){}
  public RitualObligationTargetWorker_FaithAltar(RitualObligationTargetFilterDef def):base(def){}
  public override IEnumerable<TargetInfo> GetTargets(RitualObligation obligation,Map map){foreach(var a in FaithUtility.Altars(map))yield return a;}
  protected override RitualTargetUseReport CanUseTargetInternal(TargetInfo target,RitualObligation obligation){
   if(!target.HasThing || target.Thing.def!=Faith.AltarDef)return false;
   return target.Thing.Faction?.IsPlayer==true?(RitualTargetUseReport)true:"需要在殖民地的圣坛举行。";
  }
  public override IEnumerable<string> GetTargetInfos(RitualObligation obligation){yield return Faith.AltarDef?.label??"圣坛";}
 }

 // 品质因子（规格 5.1）。kind 见 Ideo_Rituals.xml；consume* 只检查开始条件（不加品质），庆典结束时献上。
 public class RitualOutcomeComp_LPFestival : RitualOutcomeComp {
  public string kind;
  static readonly HarmonyLib.AccessTools.FieldRef<LordJob_Ritual,int> ticksPassed=HarmonyLib.AccessTools.FieldRefAccess<LordJob_Ritual,int>("ticksPassed");
  public override bool DataRequired=>false;
  public override bool Applies(LordJob_Ritual ritual)=>true;
  public static LP_FestivalExtension ExtOf(Precept_Ritual r)=>r?.outcomeEffect?.def?.GetModExtension<LP_FestivalExtension>();
  public float Value(Precept_Ritual ritual,TargetInfo target,IEnumerable<Pawn> participants,int startAbs,out string count){
   count=null;if(!IdeoBridge.Active || !target.IsValid)return 0f;var map=target.Map;var god=ExtOf(ritual)?.deity;var ps=participants?.Where(p=>p!=null).ToList()??new List<Pawn>();
   int hour=map==null?12:GenDate.HourOfDay(startAbs,Find.WorldGrid.LongLatOf(map.Tile).x);
   switch(kind){
    case "cleric":return ps.Any(IdeoBridge.IsCleric)?qualityOffset:0f;
    case "temple":return IdeoTemple.InTemple(target)?qualityOffset:0f;
    case "statue":return map!=null && GenRadial.RadialDistinctThingsAround(target.Cell,map,8f,true).Any(t=>IdeoTemple.IsStatue(t) && (IdeoTemple.StatueGod(t)==god || IdeoTemple.StatueGod(t)==null && ritual?.ideo!=null && IdeoBridge.HasPantheon(ritual.ideo)))?qualityOffset:0f;
    case "relic":return map!=null && god?.relic!=null && GenRadial.RadialDistinctThingsAround(target.Cell,map,8f,true).Any(t=>t.def==god.relic || (t is IThingHolder h && ThingOwnerUtility.GetAllThingsRecursively(h,false).Any(x=>x.def==god.relic)))?qualityOffset:0f;
    case "dawn":return hour>=6 && hour<18?qualityOffset:0f;
    case "night10":case "night20":return hour>=22 || hour<5?qualityOffset:0f;
    case "casters":{int n=ps.Count(IdeoBridge.IsCaster);count=n.ToString();return n*qualityOffset;}
    case "graves":return IdeoMapCache.For(map)?.buriedGrave==true?qualityOffset:0f;
    case "storm":return Faith.Storm(map)?qualityOffset:0f;
    case "slaves":{if(map==null)return 0f;int n=map.mapPawns.SlavesOfColonySpawned.Count(s=>ps.Contains(s) || s.Position.InHorDistOf(target.Cell,12f));count=n.ToString();return Mathf.Min(.2f,n*qualityOffset);}
    case "dark":return map!=null && map.glowGrid.GroundGlowAt(target.Cell)<.3f?qualityOffset:0f;
   }
   return 0f;
  }
  public override float QualityOffset(LordJob_Ritual ritual,RitualOutcomeComp_Data data){
   if(ritual==null)return 0f;return Value(ritual.Ritual,ritual.selectedTarget,ritual.assignments?.Participants,GenTicks.TicksAbs-ticksPassed(ritual),out _);
  }
  public override string GetDesc(LordJob_Ritual ritual=null,RitualOutcomeComp_Data data=null){float v=ritual==null?qualityOffset:QualityOffset(ritual,data);return label.CapitalizeFirst()+"：+"+v.ToStringPercent();}
  public override string GetDescAbstract(bool positive,float quality=-1f)=>label.CapitalizeFirst()+"：+"+(quality<0f?qualityOffset:quality).ToStringPercent();
  public override string GetBonusDescShort()=>"+"+qualityOffset.ToStringPercent();
  public override QualityFactor GetQualityFactor(Precept_Ritual ritual,TargetInfo ritualTarget,RitualObligation obligation,RitualRoleAssignments assignments,RitualOutcomeComp_Data data){
   if(kind.StartsWith("consume"))return null;
   float v=Value(ritual,ritualTarget,assignments?.Participants,GenTicks.TicksAbs,out string count);
   return new QualityFactor{label=label.CapitalizeFirst(),count=count,qualityChange="+"+v.ToStringPercent(),quality=v,positive=v>0f,present=v>0f,priority=1f};
  }
  public override IEnumerable<string> BlockingIssues(Precept_Ritual ritual,TargetInfo target,RitualRoleAssignments assignments){
   if(kind=="consumeBody" && IdeoRituals.FindOffering(target,"Malar").Count==0)yield return "圣坛3格内需要一具体型≥0.7的动物尸体（大猎的祭品）。";
   if(kind=="consumeValue" && IdeoRituals.FindOffering(target,"Surtr").Count==0)yield return "圣坛3格内需要市值合计≥300的武器或护甲（熔炉之火的祭品）。";
  }
 }

 public class RitualOutcomeEffectWorker_LPFestival : RitualOutcomeEffectWorker_FromQuality {
  public RitualOutcomeEffectWorker_LPFestival(){}
  public RitualOutcomeEffectWorker_LPFestival(RitualOutcomeEffectDef def):base(def){}
  protected override void ApplyExtraOutcome(Dictionary<Pawn,int> totalPresence,LordJob_Ritual jobRitual,RitualOutcomePossibility outcome,out string extraOutcomeDesc,ref LookTargets letterLookTargets){
   base.ApplyExtraOutcome(totalPresence,jobRitual,outcome,out extraOutcomeDesc,ref letterLookTargets);
   try{string s=IdeoRituals.FestivalOutcome(def,totalPresence.Keys.ToList(),jobRitual.selectedTarget,jobRitual.Organizer,outcome.positivityIndex,jobRitual.Ritual?.ideo);if(!s.NullOrEmpty())extraOutcomeDesc=(extraOutcomeDesc.NullOrEmpty()?"":extraOutcomeDesc+"\n\n")+s;}
   catch(Exception e){Log.Error("[DND边缘大陆] 庆典结算出错："+e);}
  }
 }
 public class RitualOutcomeEffectWorker_LPFuneral : RitualOutcomeEffectWorker_FromQuality {
  public RitualOutcomeEffectWorker_LPFuneral(){}
  public RitualOutcomeEffectWorker_LPFuneral(RitualOutcomeEffectDef def):base(def){}
  protected override void ApplyExtraOutcome(Dictionary<Pawn,int> totalPresence,LordJob_Ritual jobRitual,RitualOutcomePossibility outcome,out string extraOutcomeDesc,ref LookTargets letterLookTargets){
   base.ApplyExtraOutcome(totalPresence,jobRitual,outcome,out extraOutcomeDesc,ref letterLookTargets);
   try{
    var t=jobRitual.obligation?.targetA.Thing;var dead=t as Pawn??(t as Corpse)?.InnerPawn??(jobRitual.selectedTarget.Thing as Building_Grave)?.Corpse?.InnerPawn;
    string s=IdeoRituals.FuneralOutcome(dead,outcome.positivityIndex);if(!s.NullOrEmpty())extraOutcomeDesc=(extraOutcomeDesc.NullOrEmpty()?"":extraOutcomeDesc+"\n\n")+s;
   }catch(Exception e){Log.Error("[DND边缘大陆] 灰袍葬礼结算出错："+e);}
  }
 }

 public static class IdeoRituals {
  public static float FestivalFavor(int index)=>index>=2?12f:index==1?6f:index==-1?2f:index<=-2?-3f:0f;
  static void Give(IEnumerable<Pawn> ps,string hediff){var d=DefDatabase<HediffDef>.GetNamedSilentFail(hediff);if(d==null)return;foreach(var p in ps)if(p?.health!=null && !p.Dead){var old=p.health.hediffSet.GetFirstHediffOfDef(d);if(old!=null)p.health.RemoveHediff(old);p.health.AddHediff(d);}}
  // 5.2 结果：这场庆典的神（不是守护神也加，不受 Relevant 限制）；难忘时各神的奖励；大猎 / 熔炉之火献上祭品。返回附加到信里的文字。
  public static string FestivalOutcome(RitualOutcomeEffectDef def,List<Pawn> participants,TargetInfo target,Pawn organizer,int index,Ideo ideo){
   var ext=def?.GetModExtension<LP_FestivalExtension>();var god=ext?.deity;if(god==null || !IdeoBridge.Active)return null;
   var sb=new System.Text.StringBuilder();var map=target.Map;var ps=participants.Where(p=>p!=null && !p.Dead).ToList();
   if(god.key=="Malar" || god.key=="Surtr"){var off=FindOffering(target,god.key);if(off.Count>0){foreach(var t in off)if(!t.Destroyed)t.Destroy();sb.AppendLine("祭品被献给了"+god.label+"。");}}
   float f=FestivalFavor(index);
   if(god.key=="Tyr" && f>0f && map!=null && GenLocalDate.Year(map)%4==0){f*=1.5f;sb.AppendLine("今年是大盾会：神眷×1.5。");}
   if(f!=0f){if(f>0f)Faith.Gain(god,f,"庆典："+def.LabelCap);else FaithUtility.AddFavor(god,f,"庆典："+def.LabelCap);sb.AppendLine(god.label+"的神眷"+(f>0?"+":"")+(f>0?f*Faith.GainFactor:f).ToString("0.#")+"。");}
   var st=IdeoState.Get;if(st!=null && ideo!=null && IdeoBridge.HasPantheon(ideo) && !st.pantheonDone.Contains(god.defName))st.pantheonDone.Add(god.defName);
   if(index>=2){
    var c=Faith.Comp;
    switch(god.key){
     case "Lathander":case "Talos":if(c!=null)FaithManifest.Start(c,god,true);break;
     case "Tyr":Give(new[]{organizer??ps.FirstOrDefault()},"LP_SworeOath");break;
     case "Mystra":foreach(var p in ps)p.psychicEntropy?.OffsetPsyfocusDirectly(1f);Give(ps,"LP_WeaveNight");break;
     case "Kelemvor":{
      if(c!=null && map!=null)foreach(var corpse in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().ToList())if(corpse.InnerPawn?.RaceProps.Humanlike==true && !c.rested.Contains(corpse.InnerPawn.ThingID))c.rested.Add(corpse.InnerPawn.ThingID);
      var t=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_Mem_KelemvorPeace");if(t!=null)foreach(var p in ps)p.needs?.mood?.thoughts?.memories?.TryGainMemory(t);break;}
     case "Malar":Give(ps,"LP_GreatHunt");break;
     case "Bane":if(map!=null)foreach(var s in map.mapPawns.SlavesOfColonySpawned){var n=s.needs?.TryGetNeed<Need_Suppression>();if(n!=null)n.CurLevel=Mathf.Min(1f,n.CurLevel+.2f);}break;
     case "Shar":Give(ps,"LP_SharShelter");break;
     case "Surtr":Give(ps,"LP_ForgeFire");break;
    }
    if(!ext.unforgettable.NullOrEmpty())sb.AppendLine("难忘的庆典："+ext.unforgettable);
   }
   return sb.ToString().TrimEnd();
  }
  // 大猎：3 格内一具体型 ≥0.7 的动物尸体；熔炉之火：3 格内市值合计 ≥300 的武器 / 护甲（从贵到便宜取够为止）。不够返回空表。
  public static List<Thing> FindOffering(TargetInfo target,string key){
   var res=new List<Thing>();var map=target.Map;if(map==null || !target.IsValid)return res;
   var near=GenRadial.RadialDistinctThingsAround(target.Cell,map,3.9f,true).Where(t=>t.Spawned && t.def.category==ThingCategory.Item).ToList();
   if(key=="Malar"){var c=near.OfType<Corpse>().FirstOrDefault(x=>x.InnerPawn?.RaceProps.Animal==true && x.InnerPawn.BodySize>=.7f);if(c!=null)res.Add(c);}
   else if(key=="Surtr"){float sum=0f;foreach(var t in near.Where(t=>t.def.IsWeapon || t.def.IsApparel).OrderByDescending(t=>t.MarketValue*t.stackCount)){if(sum>=300f)break;res.Add(t);sum+=t.MarketValue*t.stackCount;}if(sum<300f)res.Clear();}
   return res;
  }
  // 5.4：「不错」及以上 死者安息 + 凯兰沃 +2；「难忘」再 +3。
  public static string FuneralOutcome(Pawn dead,int index){
   if(index<1 || !IdeoBridge.Active)return null;var c=Faith.Comp;var kel=Faith.God("Kelemvor");if(c==null)return null;
   if(dead!=null && !c.rested.Contains(dead.ThingID))c.rested.Add(dead.ThingID);
   float f=index>=2?5f:2f;Faith.Gain(kel,f,"灰袍葬礼");
   return (dead!=null?dead.LabelShort+"的灵魂被凯兰沃接走，得到了安息。":"")+"凯兰沃的神眷+"+(f*Faith.GainFactor).ToString("0.#")+"。";
  }
 }

 // ==================== 圣坛按钮（众神殿庆典、集体祈祷、求取神谕、血祭） ====================
 public static class IdeoAltar {
  public const int MaxPantheonFestivals=4,OracleCooldownDays=15,OracleDeadlineDays=10,MassPrayerCapHours=6;public const float BloodFavor=15f;
  static Texture2D Tex(string p)=>ContentFinder<Texture2D>.Get(p,false)??BaseContent.BadTex;
  public static void Register(){Building_FaithAltar.ExtraGizmos.Add(Gizmos);}
  // 神谕：守护神“最想要的作为”= Faith.Deed 的理由里含这个关键词。
  public static readonly Dictionary<string,(string key,string desc)[]> Wants=new Dictionary<string,(string,string)[]>{
   {"Lathander",new[]{("收获作物","收获作物"),("击杀不死者","击杀不死者")}},
   {"Tyr",new[]{("审判仪式","举行一场审判仪式"),("释放了","释放一名囚犯")}},
   {"Mystra",new[]{("施放","施放一次法术或神术"),("产物","在炼金桌、附魔台或抄写台完成一件作品")}},
   {"Kelemvor",new[]{("安葬了尸体","安葬一具尸体"),("击杀敌方不死者","击杀一个敌方不死者")}},
   {"Malar",new[]{("猎杀","猎杀一只动物"),("近战击杀","近战杀死一只动物")}},
   {"Talos",new[]{("雷击落在地图上","让雷霆落在家园上（等一场雷暴）")}},
   {"Bane",new[]{("奴役了","奴役一名囚犯")}},
   {"Shar",new[]{("失去了亲人","让殖民地尝到失去血亲的滋味")}},
   {"Surtr",new[]{("锻造了","锻造一件武器或护甲")}},
  };
  static IEnumerable<Gizmo> Gizmos(Building_FaithAltar altar){
   if(!IdeoBridge.Linked || altar.Map==null)yield break;
   var ideo=IdeoBridge.PlayerIdeo;var info=IdeoBridge.Of(ideo);var st=IdeoState.Get;if(ideo==null || st==null)yield break;
   int now=IdeoBridge.Now;
   if(info.pantheon){
    var cmd=new Command_Action{defaultLabel="举办庆典",defaultDesc="众神殿文化可以在圣坛为任何一位可信奉的神举办庆典（不带义务，随时可以开始）：每位神每年一次，每年最多"+MaxPantheonFestivals+"场不同的神。第一次为某位神办庆典时，这位神的庆典会加进文化。\n\n今年已办："+(st.pantheonDone.Count==0?"无":string.Join("、",st.pantheonDone.Select(n=>DefDatabase<LP_DeityDef>.GetNamedSilentFail(n)?.label??n))),icon=Tex("UI/Memes/LP/Pantheon"),action=()=>Find.WindowStack.Add(new FloatMenu(PantheonOptions(altar,ideo,st)))};
    if(st.pantheonDone.Count>=MaxPantheonFestivals)cmd.Disable("今年已经办了"+MaxPantheonFestivals+"场庆典。");
    yield return cmd;
   }
   var cleric=IdeoBridge.Cleric(ideo);
   if(cleric!=null && cleric.MapHeld==altar.Map){
    var cmd=new Command_Action{defaultLabel="集体祈祷",defaultDesc=cleric.LabelShort+"召集地图上所有空闲的同文化信徒到圣坛祈祷。之后"+MassPrayerCapHours+"小时内祈祷神眷的每日上限×1.5。冷却1天。",icon=Tex("UI/Abilities/LP/MassPrayer"),action=()=>MassPrayer(altar)};
    if(now<st.massPrayerNext)cmd.Disable("冷却中，还要"+((st.massPrayerNext-now)/(float)GenDate.TicksPerHour).ToString("0.#")+"小时。");
    else if(FaithUtility.Patron==null)cmd.Disable("殖民地还没有守护神。");
    yield return cmd;
   }
   var moralist=IdeoBridge.Moralist(ideo);
   if(info.Theist && moralist!=null && moralist.MapHeld==altar.Map){
    var cmd=new Command_Action{defaultLabel="求取神谕",defaultDesc=moralist.LabelShort+"（大祭司）向守护神求取神谕：预言下一次季度显圣，并说出守护神当前最想要的作为（"+OracleDeadlineDays+"天内做到一次，额外神眷+5）。冷却"+OracleCooldownDays+"天。",icon=Tex("UI/Abilities/LP/Oracle"),action=()=>Oracle(moralist)};
    if(now<st.oracleNext)cmd.Disable("冷却中，还要"+((st.oracleNext-now)/(float)GenDate.TicksPerDay).ToString("0.#")+"天。");
    else if(FaithUtility.Patron==null)cmd.Disable("殖民地还没有守护神。");
    yield return cmd;
   }
   if(IdeoDefs.OfferingsBlood!=null && ideo.HasPrecept(IdeoDefs.OfferingsBlood)){
    var patron=FaithUtility.Patron;
    var cmd=new Command_Action{defaultLabel="血祭",defaultDesc="选一名囚犯交付处决（原版处决作业）。囚犯被处决后守护神神眷+"+BloodFavor+"。只在守护神是恶神时可用。",icon=Tex("Faith/Miracles/Dread"),action=()=>Find.WindowStack.Add(new FloatMenu(BloodOptions(altar)))};
    if(patron==null)cmd.Disable("殖民地还没有守护神。");else if(!patron.Evil)cmd.Disable(patron.label+"是"+patron.AlignmentLabel+"神，不接受血祭。");
    yield return cmd;
   }
  }
  static List<FloatMenuOption> PantheonOptions(Building_FaithAltar altar,Ideo ideo,IdeoState st){
   var list=new List<FloatMenuOption>();
   foreach(var g in FaithUtility.Worshippable){
    var pd=IdeoBridge.FestivalOf(g);if(pd==null)continue;string label=g.label+"："+pd.label;
    if(st.pantheonDone.Contains(g.defName)){list.Add(new FloatMenuOption(label+"（今年已办）",null));continue;}
    list.Add(new FloatMenuOption(label,()=>StartFestival(ideo,pd,altar),g.Icon,g.color));
   }
   return list;
  }
  public static Precept_Ritual FestivalPrecept(Ideo ideo,PreceptDef pd,bool add){
   var r=ideo.PreceptsListForReading.OfType<Precept_Ritual>().FirstOrDefault(x=>x.def==pd);
   if(r==null && add){r=(Precept_Ritual)PreceptMaker.MakePrecept(pd);ideo.AddPrecept(r,true,null,pd.ritualPatternBase);}
   return r;
  }
  static void StartFestival(Ideo ideo,PreceptDef pd,Building_FaithAltar altar){var r=FestivalPrecept(ideo,pd,true);r?.ShowRitualBeginWindow(altar,null,null,null);}
  public static int MassPrayer(Building_FaithAltar altar){
   var st=IdeoState.Get;var ideo=IdeoBridge.PlayerIdeo;if(st==null || ideo==null)return 0;int n=0;
   st.massPrayerNext=IdeoBridge.Now+GenDate.TicksPerDay;st.massPrayerCapUntil=IdeoBridge.Now+MassPrayerCapHours*GenDate.TicksPerHour;
   foreach(var p in altar.Map.mapPawns.FreeColonistsSpawned.ToList()){
    if(p.Ideo!=ideo || p.Drafted || p.Downed || p.InMentalState || FaithUtility.IsAtheist(p))continue;
    var a=FaithUtility.Altars(p.Map).OfType<Building_FaithAltar>().Where(x=>!x.IsForbidden(p)).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault(x=>Faith.PrayJobFor(p,x)!=null);
    var job=a==null?null:Faith.PrayJobFor(p,a);if(job!=null && p.jobs.TryTakeOrderedJob(job,JobTag.Misc))n++;
   }
   Messages.Message("集体祈祷："+n+"人前往圣坛祈祷。",altar,MessageTypeDefOf.NeutralEvent,false);
   return n;
  }
  public static string OracleText(LP_DeityDef god,out string key){
   key=null;var c=Faith.Comp;if(god==null || c==null)return null;
   float v=FaithUtility.GetFavor(god);int days=c.nextManifestTick<0?-1:Mathf.CeilToInt((c.nextManifestTick-IdeoBridge.Now)/(float)GenDate.TicksPerDay);
   string verdict=v>=Faith.BlessingThreshold?"赐福":v<=Faith.CurseThreshold?"降罚":"沉默（既不赐福也不降罚）";
   var sb=new System.Text.StringBuilder();
   if(days<0)sb.AppendLine("季度显圣的时间还没有定下。");
   else if(days>7)sb.AppendLine("下一次季度显圣在"+days+"天后。现在"+god.label+"的神眷是"+v.ToString("0.#")+"，若保持则会是"+verdict+"。");
   else sb.AppendLine("下一次季度显圣在"+Math.Max(0,days)+"天后，"+god.label+"会"+verdict+"（神眷"+v.ToString("0.#")+"）。");
   if(Wants.TryGetValue(god.key,out var wants) && wants.Length>0){var w=wants.RandomElement();key=w.key;sb.AppendLine();sb.Append(god.label+"想要你们"+w.desc+"。"+OracleDeadlineDays+"天内做到一次，神眷额外+5。");}
   return sb.ToString().TrimEnd();
  }
  public static void Oracle(Pawn moralist){
   var st=IdeoState.Get;var god=FaithUtility.Patron;if(st==null || god==null)return;
   string text=OracleText(god,out string key);
   st.oracleNext=IdeoBridge.Now+OracleCooldownDays*GenDate.TicksPerDay;st.oracleGod=god;st.oracleKey=key;st.oracleUntil=key==null?-1:IdeoBridge.Now+OracleDeadlineDays*GenDate.TicksPerDay;
   Find.LetterStack.ReceiveLetter("神谕："+god.label,moralist.LabelShort+"在圣坛前求得了"+god.FullLabel+"的神谕：\n\n"+text,LetterDefOf.NeutralEvent,moralist);
  }
  static List<FloatMenuOption> BloodOptions(Building_FaithAltar altar){
   var st=IdeoState.Get;var list=new List<FloatMenuOption>();
   foreach(var p in altar.Map.mapPawns.PrisonersOfColonySpawned){
    var pp=p;bool marked=st!=null && st.bloodVictims.Contains(p.thingIDNumber);
    list.Add(new FloatMenuOption(p.LabelShort+(marked?"（已交付）":""),marked?(Action)null:()=>MarkBlood(pp)));
   }
   if(list.Count==0)list.Add(new FloatMenuOption("地图上没有囚犯",null));
   return list;
  }
  public static void MarkBlood(Pawn prisoner){
   var st=IdeoState.Get;if(st==null || prisoner?.guest==null)return;
   if(!st.bloodVictims.Contains(prisoner.thingIDNumber))st.bloodVictims.Add(prisoner.thingIDNumber);
   try{prisoner.guest.SetExclusiveInteraction(PrisonerInteractionModeDefOf.Execution);}catch(Exception e){Log.Warning("[DND边缘大陆] 血祭：设置处决失败 "+e.Message);}
   Messages.Message(prisoner.LabelShort+"被交付血祭：典狱官会处决他，完成后守护神神眷+"+BloodFavor+"。",prisoner,MessageTypeDefOf.NeutralEvent,false);
  }
  public static void OnKilled(Pawn v){
   var st=IdeoState.Get;if(st==null || v==null || !st.bloodVictims.Remove(v.thingIDNumber))return;
   var god=FaithUtility.Patron;if(god==null || !god.Evil)return;
   Faith.Gain(god,BloodFavor,"血祭"+v.LabelShort);Messages.Message(god.label+"收下了血祭（神眷+"+(BloodFavor*Faith.GainFactor).ToString("0.#")+"）。",MessageTypeDefOf.PositiveEvent,false);
  }
 }

 // ==================== 神迹布道：额外消耗守护神神眷 ====================
 public class CompProperties_AbilityFavorCost : CompProperties_AbilityEffect {public float cost=5f;public CompProperties_AbilityFavorCost(){compClass=typeof(CompAbilityEffect_FavorCost);}}
 public class CompAbilityEffect_FavorCost : CompAbilityEffect {
  public CompProperties_AbilityFavorCost Props=>(CompProperties_AbilityFavorCost)props;
  public override bool GizmoDisabled(out string reason){
   var god=FaithUtility.Patron;
   if(!Faith.Enabled){reason="信仰系统已关闭。";return true;}
   if(god==null){reason="殖民地还没有守护神。";return true;}
   if(FaithUtility.GetFavor(god)<Props.cost){reason=god.label+"的神眷不足"+Props.cost+"。";return true;}
   return base.GizmoDisabled(out reason);
  }
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){base.Apply(target,dest);var god=FaithUtility.Patron;if(god!=null)FaithUtility.AddFavor(god,-Props.cost,parent.pawn.LabelShort+"神迹布道");}
 }
}
