using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.45.0 魔法物品体系：公共部分。奇物层按 AccessorySlot 分位置（戒指最多两枚不同的）；ApparelGate 限制种族/体型；
 // 条件效果用 Hediff_MagicItem（穿戴时由 CompWornHediff 加上，阶段按条件切换）；次数/每日次数用 CompMagicCharges；
 // 每小时一次的维护（再生戒指、万变印记、复苏核心）和照明在 MagicItemsState 里。
 public static class MagicItems {
  public const float SnareChance=.2f,HaloChance=.2f,HoodChance=.4f,HoodRadius=5f,TotemRadius=6f,BloodLossCap=.9f,FireballBurstChance=.1f,EagleRange=3f,AstralMishap=.05f,RevivalChance=.5f;
  public const int DeflectBreakTicks=300,RevivalDelay=60000,LichMarkGoodwill=-30;
  public static readonly string[] FreeActionHediffs={"LP_Tanglefoot","LP_FrostSlow","LP_Chilled","LP_FrostStaffChill"};
  public static Apparel WornWith<T>(Pawn p) where T:ThingComp{var list=p?.apparel?.WornApparel;if(list==null)return null;for(int i=0;i<list.Count;i++)if(list[i].GetComp<T>()!=null)return list[i];return null;}
  // 条件状态的来源：身上穿着带同一状态的 CompWornHediff 的衣物，或手持指定武器。
  public static bool HasSource(Pawn p,HediffDef def){
   if(p==null || def==null)return false;
   var ext=def.GetModExtension<MagicHediffExtension>();
   if(ext?.heldWeapon!=null)return p.equipment?.Primary?.def.defName==ext.heldWeapon && !p.Dead;
   var list=p.apparel?.WornApparel;if(list==null)return false;
   for(int i=0;i<list.Count;i++){var c=list[i].GetComp<CompWornHediff>();if(c!=null && c.Props.hediff==def)return true;}
   return false;
  }
  public static void Sync(Pawn p,HediffDef def){
   if(p?.health==null || def==null || p.Dead)return;
   bool want=HasSource(p,def);var h=p.health.hediffSet.GetFirstHediffOfDef(def);
   if(want && h==null)p.health.AddHediff(def);else if(!want && h!=null)p.health.RemoveHediff(h);
  }
  // 条件判定。
  // 1.58.6：结果只取决于武器定义，按定义记住（原来每次都做两次不区分大小写的 IndexOf 和一次 LINQ）。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<ThingDef,bool> bowDefs=new System.Collections.Concurrent.ConcurrentDictionary<ThingDef,bool>();
  public static bool HoldsBow(Pawn p){var w=p?.equipment?.Primary?.def;if(w==null || !w.IsRangedWeapon)return false;if(bowDefs.TryGetValue(w,out var b))return b;b=w.defName.IndexOf("Bow",StringComparison.OrdinalIgnoreCase)>=0 || w.weaponTags?.Any(t=>t.IndexOf("Bow",StringComparison.OrdinalIgnoreCase)>=0 || t=="Neolithic")==true;bowDefs[w]=b;return b;}
  public static readonly DefRef<ThingDef> NightGoggles=new DefRef<ThingDef>("LP_GogglesNight");
  public static bool TorsoArmored(Pawn p){var list=p?.apparel?.WornApparel;if(list==null)return false;foreach(var a in list){var ap=a.def.apparel;if(ap==null || !ap.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso))continue;if(ap.layers.Contains(ApparelLayerDefOf.Middle) || ap.layers.Contains(ApparelLayerDefOf.Shell))return true;}return false;}
  public static int TotemAllies(Pawn p){
   if(p?.Map==null || !p.Spawned)return 0;int n=0;var def=Items.Thing("LP_WaaaghTotem");
   foreach(var o in p.Map.mapPawns.AllPawnsSpawned){if(o==p || o.Dead || o.Faction!=p.Faction || !o.Position.InHorDistOf(p.Position,TotemRadius))continue;if(Items.Worn(o,def)!=null && CommonRaces.IsHalfOrc(o))n++;}
   return Math.Min(5,n);
  }
  // 单手：空手、近战武器不超过3kg、或手枪类（不超过2kg）的远程武器。
  public static bool OneHanded(Pawn p){var w=p?.equipment?.Primary;if(w==null)return true;float mass=w.GetStatValue(StatDefOf.Mass);return w.def.IsMeleeWeapon?mass<=3f:mass<=2f;}
  public static bool HidesFromDetection(Pawn p)=>Items.Worn(p,"LP_AmuletProofDetection")!=null;
  public static bool Fireproof(Pawn p)=>p?.equipment?.Primary?.def.GetModExtension<MagicWeaponExtension>()?.fireproofHolder==true;
  public static bool Night(Map map)=>map!=null && map.skyManager.CurSkyGlow<.5f;
  public static Faction Alliance=>Factions.Colony;
  public static bool AnyColonistWears(Map map,string def){var d=Items.Thing(def);if(d==null)return false;var list=map==null?PawnsFinder.AllMaps_FreeColonistsSpawned:map.mapPawns.FreeColonistsSpawned;foreach(var p in list)if(Items.Worn(p,d)!=null)return true;return false;}
  // 圣甲虫：抵挡一次生命汲取或死亡律令，用完变回一块玉石。
  public static bool ScarabResists(Pawn p){
   var a=Items.Worn(p,"LP_ScarabProtection");var c=a?.GetComp<CompMagicCharges>();if(c==null || !c.TryUse())return false;
   Items.Mote(p,"圣甲虫挡下（剩"+c.charges+"）",new Color(.55f,.9f,.6f));
   if(c.charges<=0){var map=p.MapHeld;var pos=p.PositionHeld;p.apparel.Remove(a);a.Destroy();if(map!=null){var jade=ThingMaker.MakeThing(ThingDefOf.Jade);jade.stackCount=1;GenPlace.TryPlaceThing(jade,pos,map,ThingPlaceMode.Near);}if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"的圣甲虫护符耗尽了魔力，变回一块普通的玉石。",p,MessageTypeDefOf.NeutralEvent,false);}
   return true;
  }
 }

 // ---------- 奇物位置 ----------
 public class AccessorySlot : DefModExtension {public string slot;public int max=1;}
 public static class MagicSlots {
  static readonly AccessorySlot ioun=new AccessorySlot{slot="Ioun",max=ItemWondrous.MaxIoun};
  static ApparelLayerDef layer;
  static ApparelLayerDef Layer=>layer??(layer=DefDatabase<ApparelLayerDef>.GetNamedSilentFail("LP_Accessory"));
  public static AccessorySlot Of(ThingDef d){if(d==null)return null;var e=d.GetModExtension<AccessorySlot>();if(e!=null)return e;return ItemWondrous.IsIoun(d)?ioun:null;}
  static bool OnlyAccessory(ThingDef d)=>d.apparel?.layers!=null && d.apparel.layers.Count>0 && d.apparel.layers.All(l=>l==Layer);
  // 两件都标了位置的奇物：位置不同就能同戴；同一位置只有允许多件（戒指）且不是同一种时才能同戴。没标位置的照原版规则。
  public static bool? Together(ThingDef a,ThingDef b){
   var sa=Of(a);var sb=Of(b);if(sa==null || sb==null || !OnlyAccessory(a) || !OnlyAccessory(b))return null;
   if(sa.slot!=sb.slot || sa.slot=="Ioun")return true; // 伊奥恩石可以戴两颗同样的（第三颗顶掉最早的，见 IounLimit）
   return a!=b && sa.max>1;
  }
 }
 [HarmonyPatch(typeof(ApparelUtility),nameof(ApparelUtility.CanWearTogether))]
 public static class AccessorySlotTogether {
  static void Postfix(ThingDef __0,ThingDef __1,ref bool __result){var t=MagicSlots.Together(__0,__1);if(t.HasValue)__result=t.Value;}
 }
 // 戒指：已经戴着两枚时再戴一枚，顶掉最早的那枚（伊奥恩石沿用自己的规则）。
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Wear),new Type[]{typeof(Apparel),typeof(bool),typeof(bool)})]
 public static class AccessorySlotLimit {
  static void Prefix(Pawn ___pawn,Apparel __0){
   var s=MagicSlots.Of(__0?.def);if(s==null || s.max<=1 || s.slot=="Ioun" || ___pawn?.apparel==null)return;
   var same=___pawn.apparel.WornApparel.Where(a=>a!=__0 && a.def!=__0.def && MagicSlots.Of(a.def)?.slot==s.slot).ToList();
   int max=s.max+HeroCombat.ExtraRings(___pawn); // 1.58.0 同调者：戒指多一枚
   for(int i=0;i<=same.Count-max;i++){var old=same[i];if(___pawn.Spawned)___pawn.apparel.TryDrop(old,out _,___pawn.PositionHeld,false);else{___pawn.apparel.Remove(old);___pawn.inventory?.innerContainer.TryAdd(old);}}
  }
 }

 // ---------- 穿戴限制（种族 / 体型） ----------
 public class ApparelGate : DefModExtension {
  public string race;public float minBodySize=0f,maxBodySize=999f;
  public string Blocked(Pawn p){
   if(p==null)return null;
   if(race!=null && !Items.Is(p,race))return "只有"+Items.RaceLabel(race)+"能穿戴。";
   float size=p.BodySize;
   if(size+.001f<minBodySize)return "需要体型"+minBodySize.ToString("0.#")+"以上才能穿戴。";
   if(size-.001f>maxBodySize)return "体型超过"+maxBodySize.ToString("0.#")+"就穿不上。";
   return null;
  }
 }
 public static class ApparelGates {public static string Blocked(Pawn p,ThingDef def)=>def?.GetModExtension<ApparelGate>()?.Blocked(p);}
 [HarmonyPatch(typeof(ApparelUtility),nameof(ApparelUtility.HasPartsToWear),new Type[]{typeof(Pawn),typeof(ThingDef)})]
 public static class ApparelGateParts {static void Postfix(Pawn __0,ThingDef __1,ref bool __result){if(__result && ApparelGates.Blocked(__0,__1)!=null)__result=false;}}
 [HarmonyPatch(typeof(Pawn_ApparelTracker),nameof(Pawn_ApparelTracker.Wear),new Type[]{typeof(Apparel),typeof(bool),typeof(bool)})]
 public static class ApparelGateWear {static bool Prefix(Pawn ___pawn,Apparel __0)=>ApparelGates.Blocked(___pawn,__0?.def)==null;}
 [HarmonyPatch(typeof(EquipmentUtility),nameof(EquipmentUtility.CanEquip),new Type[]{typeof(Thing),typeof(Pawn),typeof(string),typeof(bool)},new ArgumentType[]{ArgumentType.Normal,ArgumentType.Normal,ArgumentType.Out,ArgumentType.Normal})]
 public static class ApparelGateEquip {
  static void Postfix(Thing thing,Pawn pawn,ref string cantReason,ref bool __result){
   if(!__result)return;
   var why=ApparelGates.Blocked(pawn,thing?.def)??CursedItems.BlocksEquip(pawn,thing);
   if(why!=null){__result=false;cantReason=why;}
  }
 }

 // ---------- 条件状态 ----------
 public class MagicHediffExtension : DefModExtension {public string condition;public string heldWeapon;}
 public class CompProperties_WornHediff : CompProperties {public HediffDef hediff;public CompProperties_WornHediff(){compClass=typeof(CompWornHediff);}}
 public class CompWornHediff : ThingComp {
  public CompProperties_WornHediff Props=>(CompProperties_WornHediff)props;
  public override void Notify_Equipped(Pawn pawn){base.Notify_Equipped(pawn);MagicItems.Sync(pawn,Props.hediff);}
  public override void Notify_Unequipped(Pawn pawn){base.Notify_Unequipped(pawn);MagicItems.Sync(pawn,Props.hediff);}
 }
 public class Hediff_MagicItem : HediffWithComps {
  int cachedIndex=-1,cachedTick=-9999;
  // 1.58.6：扩展按定义缓存；其余条件（弓、无甲、厚岩顶、黑暗、元素）同一刻内只算一次——原来每次属性计算都重算（每次计算读两次 CurStage）。
  MagicHediffExtension ext;HediffDef extDef;int tickIndex=-1,tickStamp=int.MinValue;
  MagicHediffExtension Ext{get{if(extDef!=def){ext=def.GetModExtension<MagicHediffExtension>();extDef=def;}return ext;}}
  public override int CurStageIndex{get{
   int last=(def.stages?.Count??1)-1;if(last<=0)return 0;
   var cond=Ext?.condition;if(cond==null)return Math.Min(1,last);
   if(cond=="waaagh"){int now=Find.TickManager?.TicksGame??0;if(cachedIndex<0 || now-cachedTick>=120 || now<cachedTick){cachedTick=now;cachedIndex=Math.Min(last,MagicItems.TotemAllies(pawn));}return cachedIndex;}
   int tick=Find.TickManager?.TicksGame??0;if(tickIndex>=0 && tickStamp==tick)return tickIndex;
   int r=Index(cond,last);tickIndex=r;tickStamp=tick;return r;
  }}
  int Index(string cond,int last){
   switch(cond){
    case "smallBody":return pawn.BodySize<1.5f?1:0;
    case "bow":return MagicItems.HoldsBow(pawn)?1:0;
    case "unarmored":return MagicItems.TorsoArmored(pawn)?0:1;
    case "lichExempt":return Items.IsLich(pawn)?Math.Min(2,last):1;
    // 1.45.1 衣甲：矮人符文重铠（厚岩顶下）、暗影斗篷（暗处）、元素织袍（按元素血脉）。
    case "thickRoof":return Gene_DwarfStoneEndurance.UnderThickRoof(pawn)?1:0;
    case "dark":return MagicApparel.Dark(pawn)?1:0;
    case "element":return Math.Min(last,MagicApparel.ElementStage(pawn));
   }
   return Math.Min(1,last);
  }
  public override bool ShouldRemove=>!MagicItems.HasSource(pawn,def);
 }

 // ---------- 次数（每日次数、珠子、护盾、抵抗） ----------
 public class CompProperties_MagicCharges : CompProperties {
  public int maxCharges=1,minCharges=-1;public bool dailyReset;public string chargeLabel;
  public CompProperties_MagicCharges(){compClass=typeof(CompMagicCharges);}
 }
 public class CompMagicCharges : ThingComp {
  public int charges=-1,day=-1;
  public CompProperties_MagicCharges Props=>(CompProperties_MagicCharges)props;
  static int Today=>Find.TickManager==null?0:Find.TickManager.TicksGame/GenDate.TicksPerDay;
  public override void PostPostMake(){base.PostPostMake();charges=Props.minCharges>0?Rand.RangeInclusive(Props.minCharges,Props.maxCharges):Props.maxCharges;day=Today;}
  public void Refresh(){if(charges<0)charges=Props.maxCharges;if(Props.dailyReset && Today!=day){day=Today;charges=Props.maxCharges;}}
  public int Charges{get{Refresh();return charges;}}
  public bool TryUse(int n=1){Refresh();if(charges<n)return false;charges-=n;return true;}
  public override string TransformLabel(string label)=>Props.chargeLabel.NullOrEmpty()?label:label+"（"+Props.chargeLabel+" "+Charges+(Props.dailyReset?"/"+Props.maxCharges:"")+"）";
  public override string CompInspectStringExtra()=>Props.chargeLabel.NullOrEmpty()?null:Props.chargeLabel+"："+Charges+(Props.dailyReset?" / "+Props.maxCharges+"（每天恢复）":"");
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref charges,"lpCharges",-1);Scribe_Values.Look(ref day,"lpChargeDay",-1);}
 }

 // ---------- 头顶光环 ----------
 public class CompProperties_WornGlow : CompProperties {public string texPath;public CompProperties_WornGlow(){compClass=typeof(CompWornGlow);}}
 public class CompWornGlow : ThingComp {
  Material mat;
  public override void CompDrawWornExtras(){
   base.CompDrawWornExtras();var p=(parent as Apparel)?.Wearer;if(p==null || !p.Spawned || p.Dead)return;
   if(mat==null)mat=MaterialPool.MatFrom(((CompProperties_WornGlow)props).texPath,ShaderDatabase.MoteGlow,new Color(1f,.92f,.6f,.8f));
   var pos=p.DrawPos+new Vector3(0f,0f,.62f);pos.y=AltitudeLayer.MoteOverhead.AltitudeFor();
   Graphics.DrawMesh(MeshPool.plane10,Matrix4x4.TRS(pos,Quaternion.identity,new Vector3(.8f,1f,.8f)),mat,0);
  }
 }

 // ---------- 照明（流星戒指夜里、不灭火把）：在佩戴者脚下放一个看不见的光源，走动时跟着挪 ----------
 public class MagicLightExtension : DefModExtension {public bool nightOnly;}
 public static class MagicLights {
  public static bool Wants(Pawn p){
   var list=p?.apparel?.WornApparel;if(list==null || !p.Spawned || p.Dead)return false;
   for(int i=0;i<list.Count;i++){var e=list[i].def.GetModExtension<MagicLightExtension>();if(e!=null && (!e.nightOnly || MagicItems.Night(p.Map)))return true;}
   return false;
  }
 }

 // ---------- 魔法物品的全局状态 ----------
 public class RevivalEntry : IExposable {
  public Pawn pawn;public int tick;public float roll;
  public void ExposeData(){Scribe_References.Look(ref pawn,"pawn",true);Scribe_Values.Look(ref tick,"tick");Scribe_Values.Look(ref roll,"roll");}
 }
 public class MagicItemsState : GameComponent {
  public List<RevivalEntry> revivals=new List<RevivalEntry>();
  public bool sealGiven;public int harperCloaks;
  readonly Dictionary<Pawn,Thing> lights=new Dictionary<Pawn,Thing>();
  bool cleaned;
  public static MagicItemsState Get=>GameComp<MagicItemsState>.Get();
  public MagicItemsState(Game game){}
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;
   if(!cleaned){cleaned=true;CleanLights();}
   if(now%60==17)UpdateLights();
   if(now%250==91){Decanters.Refresh();ProcessRevivals(now);}
   if(now%GenDate.TicksPerHour==733)Hourly();
  }
  public void CleanLights(){var def=Items.Thing("LP_MagicLight");if(def==null || Find.Maps==null)return;foreach(var map in Find.Maps)foreach(var t in map.listerThings.ThingsOfDef(def).ToList())t.Destroy();lights.Clear();}
  public int UpdateLights(){
   var def=Items.Thing("LP_MagicLight");if(def==null || Find.Maps==null)return 0;
   foreach(var kv in lights.ToList()){if(!MagicLights.Wants(kv.Key)){if(kv.Value!=null && !kv.Value.Destroyed)kv.Value.Destroy();lights.Remove(kv.Key);}}
   foreach(var map in Find.Maps)foreach(var p in map.mapPawns.AllPawnsSpawned){
    if(!MagicLights.Wants(p))continue;
    lights.TryGetValue(p,out var light);
    if(light!=null && !light.Destroyed && light.Map==map && light.Position==p.Position)continue;
    if(light!=null && !light.Destroyed)light.Destroy();
    lights[p]=GenSpawn.Spawn(ThingMaker.MakeThing(def),p.Position,map);
   }
   return lights.Count;
  }
  public Thing LightOf(Pawn p)=>lights.TryGetValue(p,out var t) && t!=null && !t.Destroyed?t:null;
  // 每小时：再生戒指、万变印记、审判徽记的赠礼。
  public void Hourly(){
   foreach(var map in Find.Maps)foreach(var p in map.mapPawns.AllPawnsSpawned.ToList()){
    if(p.Dead || p.apparel==null)continue;
    var ring=Items.Worn(p,"LP_RingRegeneration");if(ring!=null)MagicEffects.Regenerate(p,ring);
    var change=Items.Worn(p,"LP_MarkChange");if(change!=null && !Items.IsLich(p) && change.GetComp<CompMagicCharges>()?.TryUse()==true)MagicEffects.Oddity(p,Rand.Value);
   }
   MagicEffects.TrySendInquisitorSeal(this);
  }
  public void Schedule(Pawn p,float roll){if(p==null || revivals.Any(r=>r.pawn==p))return;revivals.Add(new RevivalEntry{pawn=p,tick=Find.TickManager.TicksGame+MagicItems.RevivalDelay,roll=roll});}
  public void ProcessRevivals(int now){
   for(int i=revivals.Count-1;i>=0;i--){var r=revivals[i];if(r.pawn==null || !r.pawn.Dead || r.pawn.Corpse==null || r.pawn.Corpse.Destroyed){revivals.RemoveAt(i);continue;}if(now<r.tick)continue;revivals.RemoveAt(i);MagicEffects.RevivalCore(r.pawn,r.roll);}
  }
  public override void ExposeData(){
   Scribe_Collections.Look(ref revivals,"lpRevivals",LookMode.Deep);Scribe_Values.Look(ref sealGiven,"lpSealGiven");Scribe_Values.Look(ref harperCloaks,"lpHarperCloaks");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(revivals==null)revivals=new List<RevivalEntry>();revivals.RemoveAll(r=>r?.pawn==null);cleaned=false;}
  }
 }
}
