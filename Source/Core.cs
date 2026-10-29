using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 public partial class Settings : ModSettings {
  public int levelCap=30,softCap=10;
  public bool trollRevival=true;public int trollRevivalLimit=3;
  public int dragonAgePercent=100,dragonEggHatchDays=30;public bool hoardTheftRage=true;
  public bool brainHungerRage=true,elderBrainSummon=false;public int ceremorphDays=5;
  public bool factionColony=true,factionHostile=true,factionEmber=true,factionIllithid=true,factionBlackNet=true,factionRedHawk=true,harperWarning=true,decapTrollBerserk=true,zealotRaids=true,redRobeRecruit=true,tributeDemand=true;
  public override void ExposeData(){ExposeFactions();ExposeItems();ExposeBodySize();ExposeRaces124();ExposeBeasts();ExposeEvents();ExposeGuns();ExposeGiants();ExposeBackgrounds();ExposeHeroTraits();ExposeMarilith();ExposeOozefolk();ExposeFaith();ExposeIdeo();ExposeAscension();ExposeAdventurer();ExposeChromatic();ExposeAbyss();ExposeMythal();ExposeCleanup();Scribe_Values.Look(ref brainHungerRage,"brainHungerRage",true);Scribe_Values.Look(ref elderBrainSummon,"elderBrainSummon",false);Scribe_Values.Look(ref ceremorphDays,"ceremorphDays",5);ceremorphDays=Math.Max(1,Math.Min(15,ceremorphDays));Scribe_Values.Look(ref levelCap,"levelCap",30);Scribe_Values.Look(ref softCap,"softCap",10);Scribe_Values.Look(ref trollRevival,"trollRevival",true);Scribe_Values.Look(ref trollRevivalLimit,"trollRevivalLimit",3);Scribe_Values.Look(ref dragonAgePercent,"dragonAgePercent",100);Scribe_Values.Look(ref dragonEggHatchDays,"dragonEggHatchDays",30);Scribe_Values.Look(ref hoardTheftRage,"hoardTheftRage",true);dragonAgePercent=Math.Max(10,Math.Min(300,dragonAgePercent));dragonEggHatchDays=Math.Max(5,Math.Min(120,dragonEggHatchDays));trollRevivalLimit=Math.Max(1,Math.Min(10,trollRevivalLimit));levelCap=Math.Max(1,Math.Min(200,levelCap));softCap=Math.Max(1,Math.Min(levelCap,softCap));}
  void ExposeFactions(){Scribe_Values.Look(ref factionColony,"factionColony",true);Scribe_Values.Look(ref factionHostile,"factionHostile",true);Scribe_Values.Look(ref factionEmber,"factionEmber",true);Scribe_Values.Look(ref factionIllithid,"factionIllithid",true);Scribe_Values.Look(ref factionBlackNet,"factionBlackNet",true);Scribe_Values.Look(ref factionRedHawk,"factionRedHawk",true);Scribe_Values.Look(ref harperWarning,"harperWarning",true);Scribe_Values.Look(ref decapTrollBerserk,"decapTrollBerserk",true);Scribe_Values.Look(ref zealotRaids,"zealotRaids",true);Scribe_Values.Look(ref redRobeRecruit,"redRobeRecruit",true);Scribe_Values.Look(ref tributeDemand,"tributeDemand",true);}
 }
 public class LichMod : Mod {
  public static Settings Settings;
  public LichMod(ModContentPack content):base(content){Settings=GetSettings<Settings>();new Harmony("local.lich.phylactery").PatchAll();}
  // 设置文件按“Mod_文件夹名_类名.xml”保存，与这里的标题无关：改标题不影响已保存的设置。
  public override string SettingsCategory()=>"DND边缘大陆";
  // 1.31.0：两栏内容超过窗口高度，放进滚动视图（高度按上次绘制时较长那一栏算）。
  // 1.52.0 热修：Listing_Standard 一栏超过列表矩形高度时会自己换到下一栏（第三、四栏画在窗口右边外面），高度又是按换栏后的短栏量的，
  // 于是派系、事件、信仰、成神、冒险者等分组整组看不见。现在两栏各用一个足够高的 Listing（不会自动换栏），量出真实高度给滚动视图；顶部一排按钮跳到各分组。
  static Vector2 settingsScroll;static float settingsHeight=1200f;const float SettingsTall=100000f;
  public static readonly (string name,Action<Listing_Standard> draw)[] SettingsLeft={("命匣等",DrawCore),("物品",ItemSettings.Draw),("神裔",Races124Settings.Draw),("巨人",GiantSettings.Draw),("背景特性",BackgroundSettings.Draw),("DND 特性",HeroTraitSettings.Draw),("六臂蛇人",MarilithSettings.Draw),("流浆人",OozefolkSettings.Draw),("信仰",FaithSettings.Draw),("成神",AscensionSettings.Draw),("文化",IdeoSettings.Draw)};
  public static readonly (string name,Action<Listing_Standard> draw)[] SettingsRight={("冒险者",AdventurerSettings.Draw),("派系",FactionSettings.Draw),("野兽",BeastSettings.Draw),("事件",EventSettings.Draw),("五色龙",ChromaticSettings.Draw),("深渊",AbyssSettings.Draw),("枪械",GunSettings.Draw),("迷锁地牢",MythalSettings.Draw)};
  public static readonly Dictionary<string,float> SettingsAnchor=new Dictionary<string,float>();
  public static float SettingsContentHeight=>settingsHeight;
  static float DrawSettingsColumn(Rect col,(string name,Action<Listing_Standard> draw)[] groups){var l=new Listing_Standard();l.Begin(new Rect(col.x,col.y,col.width,SettingsTall));foreach(var g in groups){SettingsAnchor[g.name]=l.CurHeight;g.draw(l);}float h=l.CurHeight;l.End();return h;}
  public override void DoSettingsWindowContents(Rect rect){
   var all=SettingsRight.Concat(SettingsLeft).ToArray();float bw=84f,bh=26f;int per=Math.Max(1,(int)(rect.width/(bw+4f)));int rows=(all.Length+per-1)/per;
   for(int i=0;i<all.Length;i++){var b=new Rect(rect.x+(i%per)*(bw+4f),rect.y+(i/per)*(bh+4f),bw,bh);if(Widgets.ButtonText(b,all[i].name) && SettingsAnchor.TryGetValue(all[i].name,out var y))settingsScroll.y=y;}
   float top=rows*(bh+4f)+4f;var outer=new Rect(rect.x,rect.y+top,rect.width,rect.height-top);
   var view=new Rect(0f,0f,outer.width-20f,Math.Max(outer.height,settingsHeight));Widgets.BeginScrollView(outer,ref settingsScroll,view);
   float cw=(view.width-24f)/2f;float left=DrawSettingsColumn(new Rect(0f,0f,cw,view.height),SettingsLeft);float right=DrawSettingsColumn(new Rect(cw+24f,0f,cw,view.height),SettingsRight);
   settingsHeight=Math.Max(left,right)+30f;Widgets.EndScrollView();
  }
  static void DrawCore(Listing_Standard l){CleanupSettings.Draw(l);l.Label("命匣等级上限："+Settings.levelCap);Settings.levelCap=(int)l.Slider(Settings.levelCap,1,200);l.Label("软上限："+Settings.softCap+"（超过后成长为四分之一，升级费用继续提高）");Settings.softCap=(int)l.Slider(Settings.softCap,1,Settings.levelCap);l.Label("降低上限不删除已有经验；等级收益按当前设置截断。恢复上限后恢复原有成长。");l.GapLine();l.CheckboxLabeled("巨魔不烧就会复活",ref Settings.trollRevival,"死去的巨魔若死前6小时内没受火焰或酸液伤害、大脑完好，尸体保持新鲜约1天后原地复活。对敌方巨魔同样有效。");l.Label("每个巨魔一生最多复活："+Settings.trollRevivalLimit+"次");Settings.trollRevivalLimit=(int)l.Slider(Settings.trollRevivalLimit,1,10);l.GapLine();l.CheckboxLabeled("巨躯规则：体型影响生命与近战",ref Settings.bodySizeRule,"火巨人、巨魔、牛头人和力能龙裔（按龙龄）体型越大，生命总量和近战伤害越高：生命×(1+0.5×强度×(体型倍率−1))，近战×(1+0.3×强度×(体型倍率−1))。成年火巨人与龙体基因的生命不低于×150%（与体型倍率取较大值），所以强度只会提高、不会降低它们的生命；火巨人儿童随年龄增长。死灵造物不适用，小体型不减。关闭（或强度0%）时没有近战加成，火巨人与龙体的生命为旧的×150%（取较大值）。");l.Label("巨躯强度："+Settings.bodySizeStrength+"%（同时缩放生命和近战系数）");Settings.bodySizeStrength=(int)l.Slider(Settings.bodySizeStrength,0,200);l.GapLine();l.Label("力能龙裔龙龄晋升门槛："+Settings.dragonAgePercent+"%（同时缩放所需天数与宝库价值）");Settings.dragonAgePercent=(int)l.Slider(Settings.dragonAgePercent,10,300);l.CheckboxLabeled("宝库被窃狂暴",ref Settings.hoardTheftRage,"外人从力能龙裔的宝库里拿走东西时，力能龙裔对小偷陷入杀意狂暴。关闭后只会产生负面心情。");l.Label("力能龙蛋孵化天数："+Settings.dragonEggHatchDays+"天（宝库越富有孵得越快）");Settings.dragonEggHatchDays=(int)l.Slider(Settings.dragonEggHatchDays,5,120);l.GapLine();l.CheckboxLabeled("夺心魔脑饥狂乱",ref Settings.brainHungerRage,"脑髓归零的夺心魔每小时有15%概率陷入狂乱。关闭后只有虚弱和负面心情。");l.Label("蝌蚪化形天数："+Settings.ceremorphDays+"天（被寄生者完全化形为夺心魔所需的基础天数）");Settings.ceremorphDays=(int)l.Slider(Settings.ceremorphDays,1,15);l.CheckboxLabeled("上古脑召唤",ref Settings.elderBrainSummon,"3级及以上的上古脑每隔一段时间用心灵呼唤一个流浪者：对方会恍惚地走到上古脑旁边倒下。默认关闭。");}
  public override void WriteSettings(){base.WriteSettings();Factions.ApplyToggles();BodySize.ApplySettings();Beasts.ApplySettings();BackgroundTraits.ApplySettings();Adventurer.ApplySettings();}
 }
 public static class Necro {
  public static T Def<T>(string name) where T:Def=>DefDatabase<T>.GetNamed("LP_"+name);
  // These lookups also run on ordinary pawns through health/path/skill hooks.
  // Read the live list without allocating iterators or retaining stale pawn caches.
  // 1.58.6: indexed per pawn (PerfCache.cs HediffIndex) - pawns that never had the hediff return null without scanning.
  public static Hediff_Lich Lich(Pawn p)=>HediffIndex<Hediff_Lich>.Get(p);
  public static Hediff_Construct Construct(Pawn p)=>HediffIndex<Hediff_Construct>.Get(p);
  public static bool Undead(Pawn p)=>Lich(p)!=null || Construct(p)!=null;
  public static Registry Registry=>GameComp<Registry>.Get();
  public static Texture2D Icon(string path)=>ContentFinder<Texture2D>.Get("Necromancy/"+path);
  public static float EffectiveLevel(int level){int n=Math.Min(level,LichMod.Settings.levelCap),s=LichMod.Settings.softCap;return Math.Min(n,s)+Math.Max(0,n-s)*.25f;}
  public static float Requirement(int level)=>100f*(float)Math.Pow(1.22,Math.Min(200,Math.Max(1,level))-1);
  public static float Integrity(Corpse corpse){
   Pawn p=corpse?.InnerPawn;if(p==null || !p.RaceProps.IsFlesh || Undead(p))return 0;
   float value=0,total=0;
   foreach(var part in p.RaceProps.body.AllParts){float w=Math.Max(.001f,part.coverageAbs);total+=w;if(!p.health.hediffSet.PartIsMissing(part))value+=w*Mathf.Clamp01(p.health.hediffSet.GetPartHealth(part)/Math.Max(1,part.def.GetMaxHealth(p)));}
   float rot=corpse.GetRotStage()==RotStage.Dessicated?.1f:corpse.GetRotStage()==RotStage.Rotting?.5f:1f;
   return total<=0?0:100f*Mathf.Clamp(p.BodySize,.1f,4f)*value/total*rot;
  }
  public static bool Pay(Pawn p,float points,float heat=0){var h=Lich(p);if(h==null || !h.Active || p.psychicEntropy==null || !p.psychicEntropy.IsPsychicallySensitive || h.Points+.0001f<points || p.psychicEntropy.WouldOverflowEntropy(heat))return false;if(heat>0 && !p.psychicEntropy.TryAddEntropy(heat,null,true,false))return false;p.psychicEntropy.OffsetPsyfocusDirectly(-points/h.Power);return true;}
  public static float Mentoring(Pawn master){float s=Math.Max(0,master?.GetStatValue(StatDefOf.PsychicSensitivity)??0);return 1f+1.5f*s/(s+2f);}
  public static void Refresh(Pawn p){p.health.capacities.Notify_CapacityLevelsDirty();p.Drawer?.renderer?.SetAllGraphicsDirty();p.needs?.AddOrRemoveNeedsAsAppropriate();}
  public static List<BodyPartRecord> Missing(Pawn p)=>p.health.hediffSet.GetMissingPartsCommonAncestors().Select(x=>x.Part).Where(b=>b!=p.RaceProps.body.corePart && (b.parent==null || !p.health.hediffSet.PartIsMissing(b.parent)) && !p.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(b)).ToList();
  public static IEnumerable<Pawn> LivingConstructs(Pawn master)=>Registry==null?Enumerable.Empty<Pawn>():Registry.constructs.Where(p=>p!=null && !p.Destroyed && !p.Dead && Construct(p)?.master==master);
  public static int Used(Pawn master)=>LivingConstructs(master).Sum(p=>Construct(p).Cost);
 }
 public partial class Registry : GameComponent {
  public List<Phylactery> boxes=new List<Phylactery>();
  public List<Pawn> constructs=new List<Pawn>();
  public Registry(Game game){}
  public override void FinalizeInit(){base.FinalizeInit();MigrateBodySize();FinalizeSatiety();FireGiant.MigrateLoadedGame();TrollAppearance.MigrateLoadedGame();ForceDragon.MigrateLoadedGame();ReconcileConstructs();foreach(var box in boxes.ToList()){var h=Necro.Lich(box?.soul);h?.RemoveObsoleteEnhancements();h?.RemoveSoulBolt();h?.UpgradeNecromancyGenes();h?.SyncSpells();box?.ScheduleRevival();}foreach(var p in constructs.ToList()){Necro.Construct(p)?.UpgradeColonist();Necro.Construct(p)?.Maintain(0);}foreach(var map in Find.Maps)foreach(var altar in map.listerThings.ThingsOfDef(Necro.Def<ThingDef>("Altar")).OfType<Building_Altar>())altar.UpgradeFleshRecipes();}
  public override void ExposeData(){ExposeBodySize();ExposeSatiety();Scribe_Collections.Look(ref boxes,"lichPhylacteries",LookMode.Reference);Scribe_Collections.Look(ref constructs,"lichConstructs",LookMode.Reference);if(Scribe.mode==LoadSaveMode.PostLoadInit){boxes=boxes?.Where(x=>x!=null && !x.Destroyed).Distinct().ToList()??new List<Phylactery>();constructs=constructs?.Where(x=>x!=null && !x.Destroyed).Distinct().ToList()??new List<Pawn>();}}
  public override void GameComponentTick(){if(Find.TickManager.TicksGame%60!=0)return;foreach(var b in boxes.ToList())if(b==null || b.Destroyed)boxes.Remove(b);else b.ProcessRevival();constructs.RemoveAll(p=>p==null || p.Destroyed);}
 }
 public partial class Phylactery : ThingWithComps,IThingHolder {
  public Pawn soul;
  public int level=1;
  public float experience;
  public ThingOwner<Pawn> souls;
  public Phylactery(){souls=new ThingOwner<Pawn>(this,true,LookMode.Deep,false);}
  public float Effective=>Necro.EffectiveLevel(level);
  public int Capacity=>4+(int)(Effective*2);
  public void Register(){if(Necro.Registry!=null && !Necro.Registry.boxes.Contains(this))Necro.Registry.boxes.Add(this);}
  public override void SpawnSetup(Map map,bool respawningAfterLoad=false){base.SpawnSetup(map,respawningAfterLoad);Register();}
  public void Feed(float amount){if(level>=LichMod.Settings.levelCap)return;experience+=Math.Max(0,amount);while(level<LichMod.Settings.levelCap && experience>=Necro.Requirement(level)){experience-=Necro.Requirement(level);level++;}Necro.Lich(soul)?.ClampFocus();Necro.Lich(soul)?.SyncSpells();}
  public bool Capture(){if(Destroyed || soul==null || !soul.Dead || soul.Discarded)return false;if(souls.Contains(soul))return true;Corpse corpse=soul.Corpse;if(corpse!=null){corpse.GetDirectlyHeldThings().Remove(soul);if(!corpse.Destroyed)corpse.Destroy();}else if(soul.Spawned)soul.DeSpawn();if(Find.WorldPawns.Contains(soul))Find.WorldPawns.RemovePawn(soul);return souls.TryAddOrTransfer(soul,false);}
  public bool Rebuild(Building_Altar altar)=>TryAutoRevive();
  public override string GetInspectString()=>base.GetInspectString()+"\n命匣主人："+(soul?.LabelShort??"未绑定")+"\n等级 "+level+" / "+LichMod.Settings.levelCap+"；献祭 "+experience.ToString("0.#")+" / "+Necro.Requirement(level).ToString("0.#")+"\n统御 "+Necro.Used(soul)+" / "+Capacity+RevivalStatus;
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref soul,"boundSoul",true);Scribe_Values.Look(ref level,"level",1);Scribe_Values.Look(ref experience,"experience",0);Scribe_Values.Look(ref revivalTick,"revivalTick",-1);Scribe_Deep.Look(ref souls,"souls",this);if(Scribe.mode==LoadSaveMode.PostLoadInit){level=Math.Max(1,level);if(souls!=null)souls.removeContentsIfDestroyed=false;if(souls==null)souls=new ThingOwner<Pawn>(this,true,LookMode.Deep,false);}}
  public ThingOwner GetDirectlyHeldThings()=>souls;
  public void GetChildHolders(List<IThingHolder> outChildren){ThingOwnerUtility.AppendThingHoldersFromThings(outChildren,souls);}
  public override void Destroy(DestroyMode mode=DestroyMode.Vanish){base.Destroy(mode);if(soul!=null){Messages.Message(soul.LabelShort+"的命匣已毁：无法再通过此命匣复生。",MessageTypeDefOf.NegativeEvent,false);Necro.Lich(soul)?.ClampFocus();}souls.ClearAndDestroyContentsOrPassToWorld();}
 }
 public class Gene_Lich : Gene {
  public override IEnumerable<Gizmo> GetGizmos(){if(pawn.Faction!=Faction.OfPlayer || Necro.Lich(pawn)!=null)yield break;yield return new Command_Action{defaultLabel="巫妖潜能：尚未绑定",defaultDesc="此超凡基因提供巫妖仪式资格与潜能。建造死灵仪式台，由此角色执行巫妖仪式，才能建立命匣与不死之身。原有内源和异种基因均保留。",icon=Necro.Icon("Icons/LichCore"),action=()=>Messages.Message("在死灵仪式台添加巫妖仪式工作单，并把执行者限制为此角色。",MessageTypeDefOf.NeutralEvent,false)};}
 }
 public partial class Hediff_Lich : Hediff {
  public Phylactery box;
  public List<GeneDef> originalEndogenes=new List<GeneDef>(),originalXenogenes=new List<GeneDef>();
  public BodyPartRecord regrowing;
  public float regrowth;
  public bool autoRepair=true;
  int accumulated;
  public float RepairMultiplier=>Math.Max(0,pawn.GetStatValue(StatDefOf.PsychicSensitivity));
  public void RemoveSoulBolt(){var ability=Necro.Def<AbilityDef>("SoulBolt");if(pawn.abilities?.GetAbility(ability)!=null)pawn.abilities.RemoveAbility(ability);}
  public bool Active=>!pawn.Dead && box!=null && !box.Destroyed && box.soul==pawn;
  public float Power=>100f+(box!=null && !box.Destroyed?box.Effective*10f:0);
  public float Reserved {get{float sum=0;var list=pawn.health.hediffSet.hediffs;for(int i=0;i<list.Count;i++)if(list[i] is Hediff_Limb h)sum+=h.Severity*5f;return sum;}}
  public float FocusCeiling=>Math.Max(.1f,1f-Reserved/Power);
  public float Points=>(pawn.psychicEntropy?.CurrentPsyfocus??0)*Power;
  public override bool ShouldRemove=>false;
  public void ClampFocus(){if(pawn.psychicEntropy==null)return;float ceiling=FocusCeiling;if(pawn.psychicEntropy.CurrentPsyfocus>ceiling+.000001f)pawn.psychicEntropy.OffsetPsyfocusDirectly(ceiling-pawn.psychicEntropy.CurrentPsyfocus);}
  public override string LabelInBrackets=>"精神力 "+Points.ToString("0.#")+" / "+(Power*FocusCeiling).ToString("0.#")+"；占用 "+Reserved.ToString("0");
  public override string TipStringExtra=>"原版精神力条对应 "+Power.ToString("0.#")+" 点。肢体强化占用 "+Reserved+" 点上限。\n原有内源基因 "+originalEndogenes.Count+" 项、异种基因 "+originalXenogenes.Count+" 项，均原样保留。\n"+(Active?"命匣等级 "+box.level+"；统御 "+Necro.Used(pawn)+" / "+box.Capacity:"命匣失联：修复、施法与造物统御暂停。")+(regrowing!=null?"\n重塑 "+regrowing.Label+"："+(regrowth/600f).ToString("0")+"%":"")+SpellStatus;
  HediffStage cachedStage;float cachedEffective;
  public override HediffStage CurStage {get{float effective=Active?box.Effective:0;if(cachedStage!=null && cachedEffective==effective)return cachedStage;cachedEffective=effective;return cachedStage=new HediffStage{painFactor=0,totalBleedFactor=0,hungerRateFactor=0,restFallFactor=0,statFactors=new List<StatModifier>{new StatModifier{stat=StatDefOf.InjuryHealingFactor,value=0}},statOffsets=new List<StatModifier>{new StatModifier{stat=StatDefOf.PsychicEntropyMax,value=effective*6f},new StatModifier{stat=StatDefOf.PsychicEntropyRecoveryRate,value=effective*.06f}}};}}
  public override void TickInterval(int delta){base.TickInterval(delta);DrainTick(delta);accumulated+=delta;if(accumulated<250)return;int elapsed=accumulated;accumulated=0;Maintain(elapsed);}
  public void Maintain(int elapsed){
   RemoveObsoleteEnhancements();RemoveSoulBolt();SyncSpells();ClampFocus();if(!Active || pawn.psychicEntropy==null)return;
   float upkeep=2f+Necro.LivingConstructs(pawn).Sum(p=>Necro.Construct(p).Upkeep);
   float gain=(8f+box.Effective*1.5f-upkeep)*elapsed/60000f;
   // OffsetPsyfocusDirectly is already followed by ReservePsyfocus.ClampFocus.
   pawn.psychicEntropy.OffsetPsyfocusDirectly(gain/Power);
   if(!autoRepair)return;
   float sensitivity=RepairMultiplier;if(sensitivity<=0)return;
   var injuries=pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(i=>i.Severity).ToList();
   if(injuries.Count>0){
    float budget=Math.Min(injuries.Sum(i=>i.Severity),elapsed/250f*sensitivity);
    if(budget>0 && Necro.Pay(pawn,budget/sensitivity*.5f)){
     foreach(var injury in injuries){float heal=Math.Min(injury.Severity,budget);injury.Heal(heal);budget-=heal;if(budget<=0)break;}
    }
    return;
   }
   var missing=Necro.Missing(pawn);if(regrowing!=null && !missing.Contains(regrowing)){regrowing=null;regrowth=0;}
   if(regrowing==null){regrowing=missing.FirstOrDefault();regrowth=0;}
   if(regrowing!=null){float progress=Math.Min(Math.Max(0,60000f-regrowth),elapsed*sensitivity);if(Necro.Pay(pawn,progress/sensitivity/1000f)){regrowth+=progress;if(regrowth>=60000){pawn.health.RestorePart(regrowing);regrowing=null;regrowth=0;}}}
  }
  public bool Strengthen(BodyPartRecord part){if(!CanStrengthenPart(part) || Reserved+5f>Power-Math.Max(10f,Power*.1f))return false;var limb=pawn.health.hediffSet.hediffs.OfType<Hediff_Limb>().FirstOrDefault(h=>h.Part==part);if(limb?.Severity>=5 || !Necro.Pay(pawn,5,2))return false;if(limb==null){limb=(Hediff_Limb)pawn.health.AddHediff(Necro.Def<HediffDef>("Limb"),part);limb.Severity=0;}limb.Severity++;ClampFocus();Necro.Refresh(pawn);return true;}
  public override IEnumerable<Gizmo> GetGizmos(){
   if(pawn.Faction!=Faction.OfPlayer || pawn.Dead)yield break;
   yield return new Command_Toggle{defaultLabel="精神力修复",defaultDesc="伤口修复量与断肢重塑速度按心灵敏感度线性提升。100%时每250刻修复1点伤害、消耗0.5点精神力；每个缺失部位需一天与60点精神力。200%时效果翻倍，完成同样修复的总消耗减半。优先较重伤口，治好后剩余治疗量继续用于其他伤口；按实际总治疗量扣费。精神力不足或心灵敏感度为0时暂停。当前倍率："+RepairMultiplier.ToString("0.##")+"倍。",icon=Necro.Icon("Icons/PsychicRepair"),isActive=()=>autoRepair,toggleAction=()=>autoRepair=!autoRepair};
   yield return new Command_Action{defaultLabel="强化肢体",defaultDesc="仅强化主要器官和肢体：头部、躯干、脊椎、手臂、腿、大脑、心脏、肺、肝、肾、胃、眼睛和耳朵。每层+20%该部位生命上限、+12%部位效率，占用5点精神力上限；立即消耗5点精神力与2心灵熵。每部位最多5层。",icon=Necro.Icon("Icons/StrengthenLimb"),action=()=>Find.WindowStack.Add(new FloatMenu(StrengthenableParts.Select(p=>new FloatMenuOption(StrengthenLabel(p),()=>{if(!Strengthen(p))Messages.Message("精神力、心灵熵空间或可用上限不足，或部位已满5层。",MessageTypeDefOf.RejectInput,false);})).ToList()))};
   yield return new Command_Action{defaultLabel="解除肢体强化",defaultDesc="选择一个部位解除全部强化，恢复原生命上限并释放精神力上限；不返还精神力。重伤超过原生命上限时，须先修复才能解除。",icon=Necro.Icon("Icons/ReleaseLimb"),action=()=>Find.WindowStack.Add(new FloatMenu(pawn.health.hediffSet.hediffs.OfType<Hediff_Limb>().Select(h=>new FloatMenuOption(h.Part.Label+"（"+h.Severity+"层）",()=>{if(!Release(h))Messages.Message("此部位伤势过重，请先修复，再解除强化。",MessageTypeDefOf.RejectInput,false);})).DefaultIfEmpty(new FloatMenuOption("没有强化部位",null)).ToList()))};foreach(var g in SpellGizmos())yield return g;
  }
  public override void ExposeData(){base.ExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit)HediffIndex<Hediff_Lich>.Add(this);Scribe_References.Look(ref box,"phylactery");Scribe_Collections.Look(ref originalEndogenes,"originalEndogenes",LookMode.Def);Scribe_Collections.Look(ref originalXenogenes,"originalXenogenes",LookMode.Def);Scribe_BodyParts.Look(ref regrowing,"regrowing");Scribe_Values.Look(ref regrowth,"regrowth");Scribe_Values.Look(ref autoRepair,"autoRepair",true);Scribe_Values.Look(ref accumulated,"maintenanceTicks");Scribe_Values.Look(ref necromancyGeneVersion,"necromancyGeneVersion",0);ExposeSpells();}
 }
 public class Hediff_Limb : Hediff {
  public override bool ShouldRemove=>Part==null || pawn.health.hediffSet.PartIsMissing(Part) || Necro.Lich(pawn)==null;
  HediffStage cachedStage;float cachedSeverity;
  public override HediffStage CurStage {get{float severity=Math.Min(5,Severity);if(cachedStage==null || cachedSeverity!=severity){cachedSeverity=severity;cachedStage=new HediffStage{partEfficiencyOffset=severity*.12f};}return cachedStage;}}
  public override string LabelInBrackets=>Severity.ToString("0")+"层；生命上限+"+(Math.Min(5,Severity)*20).ToString("0")+"%；占用"+(Severity*5)+"点";
 }
 [HarmonyPatch(typeof(Corpse),nameof(Corpse.Destroy))]
 public static class SaveSoulBeforeCorpseLost {
  static void Prefix(Corpse __instance){Pawn p=__instance.InnerPawn;var box=Necro.Lich(p)?.box;if(p?.Dead==true && box!=null && !box.Destroyed && box.soul==p && !NetherilQuest.PhylacteryLost(p)){__instance.GetDirectlyHeldThings().Remove(p);if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);box.souls.TryAddOrTransfer(p,false);}}
 }
 [HarmonyPatch(typeof(Pawn_PsychicEntropyTracker),nameof(Pawn_PsychicEntropyTracker.OffsetPsyfocusDirectly))]
 public static class ReservePsyfocus {static void Postfix(Pawn_PsychicEntropyTracker __instance)=>Necro.Lich(__instance.Pawn)?.ClampFocus();}
 [HarmonyPatch(typeof(Pawn_NeedsTracker),"ShouldHaveNeed")]
 public static class UndeadNeeds {
  static void Postfix(Pawn ___pawn,NeedDef nd,ref bool __result){if(!__result || Necro.Lich(___pawn)==null)return;if(nd==NeedDefOf.Food || nd==NeedDefOf.Rest)__result=false;}
 }
}
