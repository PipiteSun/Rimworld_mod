using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.56.0「文化之神」（ArtSource/Ideology-1.56.0-spec.md）：文化 DLC 第一批。Defs/贴图/补丁在 Bridges/Ideology（只在启用文化时加载），代码都在主程序集。
 // 入口全部由 IdeoBridge.Active 挡住（文化启用且 LP_Meme_Pantheon 已加载）；对文化类型的 Harmony 补丁只在 Active 时由 IdeoPatches.Apply 打上。
 // 没有每 tick 逻辑：IdeoState 每 2500 tick 同步一次，日结算挂在 WorldComponent_Faith.Daily，心情 worker 只读 IdeoMapCache。
 // 本文件：扩展、IdeoBridge（给 Faith.cs 等的乘数和挂点）、IdeoEvents、存档 IdeoState、同步、设置、派系文化重建。
 public class LP_IdeoDeityExtension : DefModExtension {
  public LP_DeityDef deity;public string deityTitle;public Gender gender=Gender.Male;
  public Quadrum festivalQuadrum=Quadrum.Aprimay;public int festivalDay=1;public PreceptDef festivalPrecept;public ThingDef statue;
 }
 public class LP_StatueExtension : DefModExtension {public LP_DeityDef deity;}
 public class LP_FactionIdeoExtension : DefModExtension {public List<PreceptDef> precepts=new List<PreceptDef>();}

 // ---------- 设置（左栏“文化”分组） ----------
 public partial class Settings {
  public bool ideoLink=true,ideoCertainty=true,ideoPatronLock=true;public float ideoNpcWeight=.3f;
  void ExposeIdeo(){
   Scribe_Values.Look(ref ideoLink,"lpIdeoLink",true);Scribe_Values.Look(ref ideoCertainty,"lpIdeoCertainty",true);Scribe_Values.Look(ref ideoPatronLock,"lpIdeoPatronLock",true);Scribe_Values.Look(ref ideoNpcWeight,"lpIdeoNpcWeight",.3f);
   ideoNpcWeight=Mathf.Clamp01(ideoNpcWeight);
  }
 }
 public static class IdeoSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;l.GapLine();l.Label("文化");
   if(!ModsConfig.IdeologyActive){GUI.color=Color.gray;l.Label("未启用文化 DLC");GUI.color=Color.white;return;}
   l.CheckboxLabeled("文化联动",ref s.ideoLink,"DND 模因的被动、DND 戒律的心情、确信度漂移、改宗修正、守护神锁定、神像效果。关闭后这些全部停用（模因和戒律仍然加载，存档不会报错）；派系补的模因保留。");
   l.Label("DND 模因出现在随机 NPC 文化的权重："+s.ideoNpcWeight.ToString("0.00")+"（0～1；伊尔斯维、维拉基斯两个 NPC 专用模因永远不随机出现）");s.ideoNpcWeight=Mathf.Round(l.Slider(s.ideoNpcWeight,0f,1f)*20f)/20f;
   l.CheckboxLabeled("神眷影响确信度",ref s.ideoCertainty,"信奉带神祇模因（或众神殿）的主文化的殖民者，每天按守护神神眷改变确信度：≥80 +1%，≥50 +0.5%，≤-30 -1%；季度显圣赐福 +5%、降罚 -5%；神选仪式 +3%。");
   l.CheckboxLabeled("文化锁定守护神",ref s.ideoPatronLock,"主文化带神祇模因时，殖民地的守护神就是那位神，不能在圣坛上手动换；重塑文化换了神时自动改奉（旧神神眷只扣15，没有冷却）。");
   bool playing=Current.ProgramState==ProgramState.Playing && Find.FactionManager!=null;
   if(l.ButtonText(playing?"重建 DND 派系文化":"重建 DND 派系文化（需要在游戏中）") && playing){
    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("给缺少 DND 模因的 DND 派系（费伦拓荒同盟、塞伊远征军、余烬氏族、幽暗殖巢、猎巫狂热分子、拉洛克、凯兰沃教会）按派系定义重新生成一个文化，设为主文化。旧文化保留：已有的 NPC 仍信旧文化，之后新出现的人用新文化。\n\n用于 1.56.0 之前的存档。继续吗？",()=>IdeoFactions.Rebuild(true)));
   }
  }
 }

 // ==================== 对外接口 ====================
 public static class IdeoBridge {
  static bool? active;internal static bool ready;
  // 没装文化 / 文化桥没加载时为假。启动完成前不缓存（防止加载 Def 时被提前算成假）。
  public static bool Active{get{if(active.HasValue)return active.Value;if(!ready)return false;active=ModsConfig.IdeologyActive && DefDatabase<MemeDef>.GetNamedSilentFail("LP_Meme_Pantheon")!=null;return active.Value;}}
  // 自测进程里除文化检查组外关闭联动（IdeoStartup.TestSuppressed），免得随机的玩家文化（可能抽到「每季献祭」等戒律）改动信仰检查的数字。
  public static bool Linked=>Active && LichMod.Settings?.ideoLink!=false && !IdeoStartup.TestSuppressed;
  public static int Now=>Find.TickManager?.TicksGame??0;

  // ---------- 模因缓存 ----------
  public class Info {public LP_DeityDef deity;public LP_IdeoDeityExtension ext;public MemeDef deityMeme;public bool pantheon,defiant,weave,undeath,ordning;public bool Theist=>deity!=null || pantheon;}
  static readonly Dictionary<Ideo,Info> cache=new Dictionary<Ideo,Info>();
  static readonly Dictionary<MemeDef,LP_IdeoDeityExtension> extOf=new Dictionary<MemeDef,LP_IdeoDeityExtension>();
  static readonly Info None=new Info();
  public static LP_IdeoDeityExtension Ext(MemeDef m){if(m==null)return null;if(!extOf.TryGetValue(m,out var e)){e=m.GetModExtension<LP_IdeoDeityExtension>();extOf[m]=e;}return e;}
  public static Info Of(Ideo ideo){
   if(ideo==null || !Active)return None;
   if(cache.TryGetValue(ideo,out var i))return i;
   i=new Info();
   foreach(var m in ideo.memes){
    var e=Ext(m);if(e?.deity!=null && i.deity==null){i.deity=e.deity;i.ext=e;i.deityMeme=m;}
    switch(m.defName){case "LP_Meme_Pantheon":i.pantheon=true;break;case "LP_Meme_Defiant":i.defiant=true;break;case "LP_Meme_Weave":i.weave=true;break;case "LP_Meme_Undeath":i.undeath=true;break;case "LP_Meme_Ordning":i.ordning=true;break;}
   }
   cache[ideo]=i;return i;
  }
  public static void Invalidate()=>cache.Clear();
  public static LP_DeityDef DeityOf(Ideo ideo)=>Of(ideo).deity;
  public static bool HasPantheon(Ideo ideo)=>Of(ideo).pantheon;
  public static bool HasMeme(Pawn p,MemeDef m)=>m!=null && p?.Ideo!=null && Active && p.Ideo.memes.Contains(m);
  public static Info OfPawn(Pawn p)=>Of(p?.Ideo);
  public static Ideo PlayerIdeo=>Active?Faction.OfPlayerSilentFail?.ideos?.PrimaryIdeo:null;
  public static Info Player=>Of(PlayerIdeo);
  public static LP_IdeoDeityExtension FestivalExt(PreceptDef d){if(d==null)return null;foreach(var m in DefDatabase<MemeDef>.AllDefsListForReading){var e=Ext(m);if(e?.festivalPrecept==d)return e;}return null;}
  public static LP_IdeoDeityExtension DndExtFor(LP_DeityDef god){foreach(var m in DefDatabase<MemeDef>.AllDefsListForReading){var e=Ext(m);if(e!=null && e.deity==god)return e;}return null;}
  public static PreceptDef FestivalOf(LP_DeityDef god){foreach(var m in DefDatabase<MemeDef>.AllDefsListForReading){var e=Ext(m);if(e!=null && e.deity==god && e.festivalPrecept!=null)return e.festivalPrecept;}return null;}

  // ---------- 守护神 ----------
  public static string PatronLockReason(LP_DeityDef god){
   if(!Linked || LichMod.Settings?.ideoPatronLock==false)return null;
   var ideo=PlayerIdeo;var d=DeityOf(ideo);if(d==null || !d.worshippable || god==d)return null;
   return "守护神由文化「"+ideo.name+"」决定（"+d.label+"）。";
  }
  public static float PatronCooldownFactor{get{if(!Linked)return 1f;var i=Player;return i.pantheon && i.deity==null?.5f:1f;}}

  // ---------- 祈祷、神术、作为 ----------
  public static float PrayerFactor(Pawn p){
   if(!Linked || p?.ideo==null)return 1f;var i=OfPawn(p);if(!i.Theist)return 1f;
   float f=.5f+p.ideo.Certainty;if(i.pantheon)f*=1.25f;if(IdeoTemple.InTemple(p))f*=1.25f;
   return f;
  }
  public static float PrayerCapFactor=>Linked && IdeoState.Get!=null && Now<IdeoState.Get.massPrayerCapUntil?1.5f:1f;
  public static ThoughtDef PrayerThought(Pawn p){if(!Linked || !IdeoTemple.GrandTemple(p))return null;return DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_Mem_TemplePrayer");}
  public static float MiracleCostFactor(Pawn p)=>Linked && IsCleric(p)?.75f:1f;
  public static float DeedFactor(string key)=>Linked && DeityOf(PlayerIdeo)?.key==key?2f:1f;
  public static float ScrollXpFactor(Pawn p)=>Linked && OfPawn(p).weave?1.15f:1f;
  public static float DefiantTaken(Pawn p)=>Linked && OfPawn(p).defiant?.75f:1f;
  public static float OfferingFactor=>Linked && PlayerIdeo?.HasPrecept(IdeoDefs.OfferingsQuarterly)==true?1.5f:1f;
  public static void OfferingDone(){var st=IdeoState.Get;if(st!=null && Active)st.lastOfferingTick=Now;}
  // 成神观「神圣使命」：和半神同文化的殖民者（不是信徒的）每人每天 +0.25 神力。
  public static float ExtraDevoteeGain(Pawn demigod){
   if(!Linked || demigod?.Ideo==null || IdeoDefs.ApotheosisSacred==null || !demigod.Ideo.HasPrecept(IdeoDefs.ApotheosisSacred))return 0f;
   var gods=Demigods.Of(demigod)?.Portfolio?.Gods.ToList()??new List<LP_DeityDef>();int n=0;
   foreach(var c in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists){if(c==demigod || c.Ideo!=demigod.Ideo)continue;var g=FaithUtility.DevoteeGod(c);if(g!=null && gods.Contains(g))continue;n++;}
   return .25f*n;
  }
  public static bool IsCleric(Pawn p){var r=p?.Ideo?.GetRole(p);return r?.def==IdeoDefs.Cleric;}
  public static Pawn Cleric(Ideo ideo)=>ideo?.RolesListForReading.FirstOrDefault(r=>r.def==IdeoDefs.Cleric)?.ChosenPawnSingle();
  public static Pawn Moralist(Ideo ideo)=>ideo?.RolesListForReading.FirstOrDefault(r=>r.def==IdeoDefs.Moralist)?.ChosenPawnSingle();
  public static bool IsGiant(Pawn p)=>p!=null && p.RaceProps.Humanlike && (GiantMigration.IsGiant(p) || p.BodySize>=1.5f || BodySize.Size(p)>=1.5f);
  public static bool IsCaster(Pawn p){
   if(p==null)return false;if(Feats.IsCaster(p) || Items.IsLich(p) || p.GetPsylinkLevel()>=1)return true;
   var list=p.abilities?.abilities;if(list!=null)for(int i=0;i<list.Count;i++)if(IsSpell(list[i]))return true;return false;
  }
  public static bool IsSpell(RimWorld.Ability a)=>a is Ability_Miracle || a is LichPsycast || a is Ability_Divine;
  public static bool HasMagicItem(Pawn p){
   var ap=p?.apparel?.WornApparel;if(ap!=null)for(int i=0;i<ap.Count;i++)if(Outsiders.MagicWeapon(ap[i].def))return true;
   var eq=p?.equipment?.Primary;return eq!=null && Outsiders.MagicWeapon(eq.def);
  }

  // ---------- 神谕作为 ----------
  public static void OnDeed(LP_DeityDef god,float amount,string reason){
   var st=IdeoState.Get;if(!Linked || st==null || god==null || amount<=0f || st.oracleGod!=god || st.oracleKey.NullOrEmpty() || Now>st.oracleUntil || reason==null || !reason.Contains(st.oracleKey))return;
   st.oracleKey=null;Faith.Gain(god,5f,"完成神谕的指示");
   Messages.Message(god.label+"满意地回应了：神谕的指示完成了（神眷+5）。",MessageTypeDefOf.PositiveEvent,false);
  }

  // ---------- 日结算（WorldComponent_Faith.Daily 调用） ----------
  public static void Daily(){
   if(!Linked)return;
   try{
    var st=IdeoState.Get;var ideo=PlayerIdeo;var info=Of(ideo);var patron=FaithUtility.Patron;
    // 4.1 确信度漂移；永夜不朽的巫妖 +1%。
    if(ideo!=null){
     float v=patron==null?0f:FaithUtility.GetFavor(patron);float drift=v>=80f?.01f:v>=50f?.005f:v<=-30f?-.01f:0f;
     foreach(var p in Faith.Colonists.ToList()){
      if(p.ideo==null || p.Ideo!=ideo)continue;
      if(info.Theist && drift!=0f && LichMod.Settings?.ideoCertainty!=false)p.ideo.OffsetCertainty(drift);
      if(info.undeath && Items.IsLich(p))p.ideo.OffsetCertainty(.01f);
     }
    }
    // 7.1 守护神（众神殿文化：众神像）的神像在任一家园：每天 +0.3，只算一次。
    if(patron!=null && (IdeoTemple.StatueAtHome(patron) || (info.pantheon && IdeoTemple.StatueAtHome(null))))Faith.Gain(patron,.3f,"家园里的神像");
    // 7.3 主文化的圣物在家园地图的圣物柜里：该遗物所属神每天 +0.5。
    if(ideo!=null)foreach(var r in ideo.PreceptsListForReading.OfType<Precept_Relic>()){
     var t=IdeoFields.GeneratedRelic(r);if(t==null || t.Destroyed)continue;var god=FaithUtility.AllDeities.FirstOrDefault(d=>d.relic==t.def);if(god==null)continue;
     if(t.ParentHolder is CompRelicContainer comp && comp.parent.MapHeld?.IsPlayerHome==true)Faith.Gain(god,.5f,t.LabelNoCount+"供在圣物柜里");
    }
    if(st!=null){
     if(st.oracleUntil>0 && Now>st.oracleUntil){st.oracleKey=null;st.oracleGod=null;st.oracleUntil=-1;}
     int year=GenDate.Year(GenTicks.TicksAbs,0f);if(st.pantheonYear!=year){st.pantheonYear=year;st.pantheonDone.Clear();}
    }
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 文化日结算出错："+e,8815601);}
  }
  public static void OnManifest(bool blessing){if(Linked && LichMod.Settings?.ideoCertainty!=false)OffsetMembers(blessing?.05f:-.05f);}
  public static void OnChosen(Pawn p){if(Linked && LichMod.Settings?.ideoCertainty!=false)OffsetMembers(.03f);}
  static void OffsetMembers(float x){var ideo=PlayerIdeo;if(ideo==null || !Of(ideo).Theist)return;foreach(var p in Faith.Colonists.ToList())if(p.ideo!=null && p.Ideo==ideo)p.ideo.OffsetCertainty(x);}
 }

 // 1.56.0：几个 Def 的缓存（只在 Active 时有值）。
 public static class IdeoDefs {
  static PreceptDef cleric,moralist,quarterly,blood,apotheosis,grey,funeral;static MemeDef pantheon;
  public static PreceptDef Cleric=>cleric??(cleric=DefDatabase<PreceptDef>.GetNamedSilentFail("LP_Role_Cleric"));
  public static PreceptDef Moralist=>moralist??(moralist=DefDatabase<PreceptDef>.GetNamedSilentFail("IdeoRole_Moralist"));
  public static PreceptDef OfferingsQuarterly=>quarterly??(quarterly=DefDatabase<PreceptDef>.GetNamedSilentFail("LP_Offerings_Quarterly"));
  public static PreceptDef OfferingsBlood=>blood??(blood=DefDatabase<PreceptDef>.GetNamedSilentFail("LP_Offerings_Blood"));
  public static PreceptDef ApotheosisSacred=>apotheosis??(apotheosis=DefDatabase<PreceptDef>.GetNamedSilentFail("LP_Apotheosis_Sacred"));
  public static PreceptDef GreyFuneral=>grey??(grey=DefDatabase<PreceptDef>.GetNamedSilentFail("LP_Funeral_GreyRobe"));
  public static PreceptDef Funeral=>funeral??(funeral=DefDatabase<PreceptDef>.GetNamedSilentFail("Funeral"));
  public static MemeDef Pantheon=>pantheon??(pantheon=DefDatabase<MemeDef>.GetNamedSilentFail("LP_Meme_Pantheon"));
  public static IEnumerable<MemeDef> DndMemes=>DefDatabase<MemeDef>.AllDefsListForReading.Where(m=>m.defName.StartsWith("LP_Meme_"));
 }
 public static class IdeoFields {
  static readonly AccessTools.FieldRef<Precept_Relic,Thing> generated=AccessTools.FieldRefAccess<Precept_Relic,Thing>("generatedRelic");
  static readonly AccessTools.FieldRef<Precept_Relic,bool> relicGenerated=AccessTools.FieldRefAccess<Precept_Relic,bool>("relicGenerated");
  public static Thing GeneratedRelic(Precept_Relic r)=>r==null?null:generated(r);
  public static bool RelicGenerated(Precept_Relic r)=>r!=null && relicGenerated(r);
 }

 // ==================== 历史事件（主程序集原有的地方调用；没装文化直接 return） ====================
 public static class IdeoEvents {
  static readonly Dictionary<int,int> lastSpell=new Dictionary<int,int>();
  public static void Record(string defName,Pawn doer){
   if(doer==null || !IdeoBridge.Linked || Current.ProgramState!=ProgramState.Playing)return;
   try{
    var d=DefDatabase<HistoryEventDef>.GetNamedSilentFail(defName);if(d==null)return;
    if(defName=="LP_HE_SpellCast"){int now=IdeoBridge.Now;if(lastSpell.TryGetValue(doer.thingIDNumber,out int t) && now-t<2500)return;lastSpell[doer.thingIDNumber]=now;}
    Find.HistoryEventsManager.RecordEvent(new HistoryEvent(d,doer.Named(HistoryEventArgsNames.Doer)));
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 文化历史事件 "+defName+"："+e,8815602);}
  }
  public static void ResetSpellLimit()=>lastSpell.Clear();
 }

 // ==================== 存档 + 2500 tick 同步 ====================
 public class IdeoState : GameComponent {
  public int lastOfferingTick=int.MinValue,massPrayerNext=-1,massPrayerCapUntil=-1,oracleNext=-1,oracleUntil=-1,pantheonYear=-1;
  public LP_DeityDef oracleGod;public string oracleKey;
  public List<string> pantheonDone=new List<string>();
  public List<int> renamedRoles=new List<int>(),relicSwapped=new List<int>(),bloodVictims=new List<int>();
  public Dictionary<int,string> founderXeno=new Dictionary<int,string>();
  public static IdeoState Get=>GameComp<IdeoState>.Get(); // 1.58.6：缓存
  public IdeoState(Game game){}
  public override void GameComponentTick(){
   if(Find.TickManager.TicksGame%2500!=1234 || !IdeoBridge.Active)return;
   try{Sync();}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 文化同步出错："+e,8815603);}
  }
  public override void FinalizeInit(){base.FinalizeInit();IdeoBridge.Invalidate();}
  // 规格 1.4 / 1.5 / 6.2 / 7.3：模因变了就清缓存；神变了就改奉；大祭司改名；神名；圣物换成神赐遗物；灰袍葬礼替换原版葬礼；记创始族裔。
  public void Sync(){
   if(!IdeoBridge.Active)return;
   IdeoBridge.Invalidate();
   if(lastOfferingTick==int.MinValue)lastOfferingTick=IdeoBridge.Now;
   var ideo=IdeoBridge.PlayerIdeo;if(ideo==null)return;
   var info=IdeoBridge.Of(ideo);
   IdeoSync.FixIdeo(ideo,this);
   if(!founderXeno.ContainsKey(ideo.id)){var x=Faith.Colonists.Where(p=>p.Ideo==ideo).Select(p=>p.genes?.Xenotype?.defName??"Baseliner").GroupBy(s=>s).OrderByDescending(g=>g.Count()).FirstOrDefault()?.Key;if(x!=null)founderXeno[ideo.id]=x;}
   if(!IdeoBridge.Linked || LichMod.Settings?.ideoPatronLock==false || !Faith.Enabled)return;
   var god=info.deity;var c=Faith.Comp;
   if(god!=null && god.worshippable && c!=null && c.patron!=god){
    var old=c.patron;c.SetPatron(god,15f,false);
    Find.LetterStack.ReceiveLetter("殖民地正式改奉"+god.label,"殖民地的主文化「"+ideo.name+"」信奉"+god.FullLabel+"，殖民地的守护神随之定为"+god.label+"。"+(old!=null?"\n\n"+old.label+"的神眷-15（文化改宗，不设冷却）。":""),LetterDefOf.NeutralEvent);
   }
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref lastOfferingTick,"lpIdeoLastOffering",int.MinValue);Scribe_Values.Look(ref massPrayerNext,"lpIdeoMassPrayerNext",-1);Scribe_Values.Look(ref massPrayerCapUntil,"lpIdeoMassPrayerCap",-1);
   Scribe_Values.Look(ref oracleNext,"lpIdeoOracleNext",-1);Scribe_Values.Look(ref oracleUntil,"lpIdeoOracleUntil",-1);Scribe_Defs.Look(ref oracleGod,"lpIdeoOracleGod");Scribe_Values.Look(ref oracleKey,"lpIdeoOracleKey");
   Scribe_Values.Look(ref pantheonYear,"lpIdeoPantheonYear",-1);Scribe_Collections.Look(ref pantheonDone,"lpIdeoPantheonDone",LookMode.Value);
   Scribe_Collections.Look(ref renamedRoles,"lpIdeoRenamedRoles",LookMode.Value);Scribe_Collections.Look(ref relicSwapped,"lpIdeoRelicSwapped",LookMode.Value);Scribe_Collections.Look(ref bloodVictims,"lpIdeoBloodVictims",LookMode.Value);
   Scribe_Collections.Look(ref founderXeno,"lpIdeoFounderXeno",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    if(pantheonDone==null)pantheonDone=new List<string>();if(renamedRoles==null)renamedRoles=new List<int>();if(relicSwapped==null)relicSwapped=new List<int>();if(bloodVictims==null)bloodVictims=new List<int>();if(founderXeno==null)founderXeno=new Dictionary<int,string>();
   }
  }
 }

 // ==================== 文化内容修正（生成时和同步时都调用，可重复调用） ====================
 public static class IdeoSync {
  public const string HighPriest="大祭司";
  public static void FixIdeo(Ideo ideo,IdeoState st){
   if(ideo==null || !IdeoBridge.Active)return;
   var info=IdeoBridge.Of(ideo);
   FixDeities(ideo);
   GreyRobeFuneral(ideo);
   if(st==null)return;
   // 6.2 大祭司：只改一次；玩家手动改名后不再覆盖。
   if(info.Theist){var r=ideo.RolesListForReading.FirstOrDefault(x=>x.def==IdeoDefs.Moralist);if(r!=null && !st.renamedRoles.Contains(r.Id)){r.SetName(HighPriest);st.renamedRoles.Add(r.Id);}}
   // 7.3 圣物：文化有神祇模因、圣物还没生成过时，换成该神的遗物（每个文化只做一次）。
   if(info.deity?.relic!=null && !st.relicSwapped.Contains(ideo.id)){SwapRelic(ideo,info.deity);st.relicSwapped.Add(ideo.id);}
  }
  public static bool SwapRelic(Ideo ideo,LP_DeityDef god){
   var r=ideo.PreceptsListForReading.OfType<Precept_Relic>().FirstOrDefault();
   if(r==null || god?.relic==null || IdeoFields.RelicGenerated(r) || r.ThingDef==god.relic)return false;
   try{r.ThingDef=god.relic;r.ClearTipCache();return true;}catch(Exception e){Log.Warning("[DND边缘大陆] 圣物换成神赐遗物失败："+e.Message);return false;}
  }
  // 2.2 固定神名：第一位神是模因的神；众神殿另加 2 位（没有主神时 3 位）随机可信奉的善 / 中立神。没有神的结构（意识形态类）不动。
  public static void FixDeities(Ideo ideo){
   if(!(ideo?.foundation is IdeoFoundation_Deity f))return;var list=f.DeitiesListForReading;if(list==null || list.Count==0)return;
   var info=IdeoBridge.Of(ideo);if(!info.Theist)return;
   if(info.deity!=null && list[0].name==info.deity.label && (!info.pantheon || list.Count>=3))return;
   if(info.deity==null && list.Any(d=>FaithUtility.AllDeities.Any(g=>g.label==d.name)))return;
   var gods=new List<(LP_DeityDef god,string title,Gender gender,MemeDef meme)>();
   if(info.deity!=null)gods.Add((info.deity,info.ext.deityTitle??info.deity.epithet,info.ext.gender,info.deityMeme));
   if(info.pantheon){
    var pool=FaithUtility.Worshippable.Where(g=>g.alignment!=DeityAlignment.Evil && g!=info.deity).InRandomOrder().Take(info.deity==null?3:2);
    foreach(var g in pool){var e=IdeoBridge.DndExtFor(g);gods.Add((g,e?.deityTitle??g.epithet,e?.gender??Gender.Male,IdeoDefs.Pantheon));}
   }
   var made=new List<IdeoFoundation_Deity.Deity>();
   foreach(var g in gods)made.Add(new IdeoFoundation_Deity.Deity{name=g.god.label,type=g.title??"",gender=g.gender,relatedMeme=g.meme,iconPath=g.god.iconPath.NullOrEmpty()?"UI/Deities/DeityGeneric":g.god.iconPath});
   f.SetDeities(made);
  }
  // 5.4 灰袍葬礼代替原版葬礼。
  public static void GreyRobeFuneral(Ideo ideo){
   if(IdeoDefs.GreyFuneral==null || IdeoDefs.Funeral==null || !ideo.HasPrecept(IdeoDefs.GreyFuneral))return;
   foreach(var p in ideo.PreceptsListForReading.Where(x=>x.def==IdeoDefs.Funeral).ToList())ideo.RemovePrecept(p);
  }
 }
 public static class IdeoFactions {
  // 8.2：派系扩展里列出的戒律替换进对应议题。
  public static void ApplyPrecepts(Ideo ideo,FactionDef fd){
   var ext=fd?.GetModExtension<LP_FactionIdeoExtension>();if(ideo==null || ext==null)return;
   foreach(var d in ext.precepts){
    if(d==null || ideo.HasPrecept(d))continue;
    if(d.issue!=null && !d.issue.allowMultiplePrecepts)foreach(var old in ideo.PreceptsListForReading.Where(x=>x.def.issue==d.issue).ToList())ideo.RemovePrecept(old);
    ideo.AddPrecept(PreceptMaker.MakePrecept(d),true,fd,null);
   }
  }
  // 和原版 FactionGenerator 新建派系时一样的参数（原版的 IdeoGenerationParmsForFaction_BackCompatibility 不带 forcedMemes）。
  public static IdeoGenerationParms ParmsFor(FactionDef d)=>new IdeoGenerationParms(d,false,null,null,d.forcedMemes,false,false,false,d.fixedIdeo,d.ideoName,d.styles,d.deityPresets,d.hiddenIdeo,d.ideoDescription,d.requiredPreceptsOnly);
  // 8.3：主文化缺任一应有 DND 模因的 DND 派系，按派系定义重新生成文化。
  public static List<Faction> Rebuild(bool letter){
   var done=new List<Faction>();if(!IdeoBridge.Active || Find.FactionManager==null)return done;
   foreach(var f in Find.FactionManager.AllFactionsListForReading.ToList()){
    if(f.IsPlayer || f.def.forcedMemes==null || f.ideos==null)continue;
    var want=f.def.forcedMemes.Where(m=>m!=null && m.defName.StartsWith("LP_Meme_")).ToList();if(want.Count==0)continue;
    var old=f.ideos.PrimaryIdeo;if(old!=null && want.All(old.HasMeme))continue;
    try{
     var parms=ParmsFor(f.def);
     var ideo=IdeoGenerator.GenerateIdeo(parms);Find.IdeoManager.Add(ideo);
     f.ideos.SetPrimary(ideo);var minor=f.ideos.IdeosMinorListForReading;if(old!=null && minor!=null && !minor.Contains(old) && f.ideos.PrimaryIdeo!=old)minor.Add(old);
     done.Add(f);
    }catch(Exception e){Log.Error("[DND边缘大陆] 重建"+f.Name+"的文化失败："+e);}
   }
   IdeoBridge.Invalidate();
   if(letter)Find.LetterStack.ReceiveLetter("重建 DND 派系文化",done.Count==0?"所有 DND 派系的主文化都已经带着应有的 DND 模因，不需要重建。":"重建了这些派系的文化：\n\n"+string.Join("\n",done.Select(f=>"· "+f.Name+"："+f.ideos.PrimaryIdeo?.name))+"\n\n旧文化保留：已有的 NPC 仍信旧文化，之后新出现的人用新文化。",LetterDefOf.NeutralEvent);
   return done;
  }
 }

 // ==================== 启动 ====================
 [StaticConstructorOnStartup]
 public static class IdeoStartup {
  public static readonly Dictionary<MemeDef,float> baseWeight=new Dictionary<MemeDef,float>();
  public static bool TestSuppressed;
  static IdeoStartup(){
   IdeoBridge.ready=true;
   if(!IdeoBridge.Active)return;
   if(GenCommandLine.CommandLineArgPassed("lichtest"))TestSuppressed=true; // 自测进程：除文化检查组外随机文化不带 DND 模因
   foreach(var m in IdeoDefs.DndMemes)baseWeight[m]=m.randomizationSelectionWeightFactor;
   ApplySettings();
   IdeoPrecepts.Register();
   IdeoAltar.Register();
   try{IdeoPatches.Apply(new Harmony("local.lich.phylactery.ideology"));}catch(Exception e){Log.Error("[DND边缘大陆] 文化补丁失败："+e);}
  }
  // 2.1 DND 模因在随机 NPC 文化里的权重 = XML 值 × 设置；NPC 专用的（XML 是 0）永远 0。
  public static void ApplySettings(){
   if(!IdeoBridge.Active)return;float w=TestSuppressed?0f:LichMod.Settings?.ideoNpcWeight??.3f;
   foreach(var kv in baseWeight)kv.Key.randomizationSelectionWeightFactor=kv.Value*w;
  }
 }
 [HarmonyPatch(typeof(LichMod),nameof(LichMod.WriteSettings))]
 public static class IdeoWriteSettings {static void Postfix(){IdeoStartup.ApplySettings();IdeoBridge.Invalidate();}}
}
