using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.45.1 魔法盔甲衣服：纹章色（意识形态色 → 派系色 → 白金）、7 个套装、派系换装（必穿件在 Patches/MagicApparel.xml，概率件在这里）、
 // 各件的小效果（旗帜、威吓、红袍威势、暗处闪避、林中掩护、偷袭、再站起来、龙骑士……）。体型门槛用 1.45.0 的 ApparelGate。
 public class HeraldryExtension : DefModExtension {public bool element;}
 public class EmpStunImmunity : DefModExtension {}
 public class AltarForgeExtension : DefModExtension {public ThingDef product,stuff;public float experience;}
 public class CompProperties_ApparelSetPiece : CompProperties {public CompProperties_ApparelSetPiece(){compClass=typeof(CompApparelSetPiece);}}
 public class CompApparelSetPiece : ThingComp {
  public override void Notify_Equipped(Pawn pawn){base.Notify_Equipped(pawn);MagicApparel.SyncSets(pawn);}
  public override void Notify_Unequipped(Pawn pawn){base.Notify_Unequipped(pawn);MagicApparel.SyncSets(pawn);}
 }
 [StaticConstructorOnStartup]
 public static partial class MagicApparel {
  public static readonly Color WhiteGold=new Color(.95f,.88f,.62f);
  public const float BannerRadius=5f,BannerOffset=.02f,IntimidateRadius=3f,IntimidateOffset=.05f,RedRobeSpeed=1.1f,RiseHealth=.3f,DragonBreathFactor=.85f,SneakFactor=1.5f;
  public const int RiseCooldown=3*GenDate.TicksPerDay,RedRobeGoodwill=-5;
  public static readonly Dictionary<string,(string[] pieces,string ability)> Sets=new Dictionary<string,(string[],string)>{
   {"LP_Set_SilverOrder",(new[]{"LP_PaladinPlate","LP_GreatHelmOath","LP_KnightMantle"},"LP_AS_HoldOath")},
   {"LP_Set_Ranger",(new[]{"LP_RangerLeathers","LP_WoodwalkerCloak"},null)},
   {"LP_Set_Shadow",(new[]{"LP_ShadowweaveCloak","LP_RogueMask","LP_BootsElvenkind"},null)},
   {"LP_Set_Archmage",(new[]{"LP_ArchmageRobe","LP_WizardHat"},null)},
   {"LP_Set_Dawn",(new[]{"LP_MorninglordVestments","LP_BattleClericBreastplate"},"LP_AS_TurnUndead")},
   {"LP_Set_Deathknight",(new[]{"LP_DeathknightPlate","LP_HelmRestlessDead"},null)},
   {"LP_Set_Dragonknight",(new[]{"LP_DragonknightArmor","LP_DragonHeadHelm"},null)}};
  // 派系换装的概率件（必穿件写在 Patches/MagicApparel.xml 的 apparelRequired 里）。
  public static readonly Dictionary<string,(string def,float chance)[]> Outfits=new Dictionary<string,(string,float)[]>{
   {"LP_FC_Guard",new[]{("LP_ChainShirt",.5f)}},
   {"LP_FC_Archer",new[]{("LP_ChainShirt",.3f),("LP_RangerLeathers",.3f)}},
   {"LP_FC_Villager",new[]{("LP_AdventurerClothes",.4f)}},
   {"LP_FC_Trader",new[]{("LP_AdventurerClothes",.5f),("LP_CourtAttire",.15f)}},
   {"LP_FC_DwarfGuard",new[]{("LP_DwarvenRuneplate",.35f),("LP_BeardedHelm",.5f)}},
   {"LP_FC_AarakocraScout",new[]{("LP_SkyguardHarness",.6f)}},
   {"LP_FC_Genasi",new[]{("LP_ElementalWeave",.5f)}},
   {"LP_FH_OrcWarrior",new[]{("LP_ChainShirt",.6f)}},
   {"LP_FH_GithMercenary",new[]{("LP_GithCrestedHelm",.5f)}},
   {"LP_FH_RedTrader",new[]{("LP_RedWizardRobe",.7f)}},
   {"LP_BN_Broker",new[]{("LP_ShadowweaveCloak",.6f),("LP_RogueMask",.5f)}},
   {"LP_BN_Enforcer",new[]{("LP_ShadowweaveCloak",.4f),("LP_RogueMask",.4f)}}};
  static MagicApparel(){
   AddPart(StatDefOf.MentalBreakThreshold,new StatPart_KnightBanner());
   AddPart(StatDefOf.MoveSpeed,new StatPart_RedRobeCommand());
   AddPart(DefDatabase<StatDef>.GetNamedSilentFail("MeleeHitChance"),new StatPart_OgreIntimidation());
   MagicEffects.DeathHooks.Add(p=>Complete(p,"LP_Set_Deathknight"));
  }
  static void AddPart(StatDef stat,StatPart part){if(stat==null)return;if(stat.parts==null)stat.parts=new List<StatPart>();part.parentStat=stat;stat.parts.Add(part);}
  // ---------- 纹章色 ----------
  public static Color Herald(Pawn p,Apparel a){
   if(a?.def.modExtensions!=null && a.def.modExtensions.OfType<HeraldryExtension>().Any(e=>e.element))return ElementColor(Genasi.Element(p)); // 基类也带一个 HeraldryExtension，所以逐个找
   if(ModsConfig.IdeologyActive && p?.Ideo!=null)return p.Ideo.ApparelColor;
   var f=p?.Faction;if(f!=null && !f.IsPlayer)return f.Color;
   return WhiteGold;
  }
  public static Color ElementColor(string e){switch(e){case "Fire":return new Color(.86f,.38f,.16f);case "Water":return new Color(.24f,.52f,.8f);case "Earth":return new Color(.62f,.46f,.24f);case "Air":return new Color(.9f,.93f,.96f);}return new Color(.55f,.55f,.55f);}
  // ---------- 套装 ----------
  public static bool Complete(Pawn p,string set){if(p?.apparel==null || !Sets.TryGetValue(set,out var s))return false;foreach(var piece in s.pieces)if(Items.Worn(p,piece)==null)return false;return true;}
  public static bool Complete(Pawn p,HediffDef set)=>set!=null && Complete(p,set.defName);
  public static void SyncSets(Pawn p){
   if(p?.health==null || p.Dead)return;
   foreach(var kv in Sets){
    var def=Items.Hediff(kv.Key);if(def==null)continue;bool want=Complete(p,kv.Key);var h=p.health.hediffSet.GetFirstHediffOfDef(def);
    if(want && h==null)p.health.AddHediff(def);else if(!want && h!=null)p.health.RemoveHediff(h);
    var ab=kv.Value.ability==null?null:DefDatabase<AbilityDef>.GetNamedSilentFail(kv.Value.ability);
    if(ab!=null && p.abilities!=null){bool has=p.abilities.GetAbility(ab)!=null;if(want && !has)p.abilities.GainAbility(ab);else if(!want && has)p.abilities.RemoveAbility(ab);}
   }
  }
  public static Hediff_ApparelSet SetHediff(Pawn p,string set)=>p?.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff(set)) as Hediff_ApparelSet;
  // ---------- 卷轴 ----------
  public static float ScrollFailFactor(Pawn p)=>Items.Worn(p,"LP_ArchmageRobe")!=null || Items.Worn(p,"LP_RedWizardRobe")!=null?.5f:1f;
  public static bool KeepScroll(Pawn p,float roll){
   var h=SetHediff(p,"LP_Set_Archmage");if(h==null)return false;int day=Find.TickManager.TicksGame/GenDate.TicksPerDay;if(h.lastFreeDay==day)return false;h.lastFreeDay=day;
   if(roll>=.5f)return false;Items.Mote(p,"大法师：卷轴没有消耗",new Color(.6f,.75f,1f));return true;
  }
  // ---------- 死亡骑士：再站起来 ----------
  public static bool TryRise(Pawn p){
   var h=SetHediff(p,"LP_Set_Deathknight");if(h==null || p.Dead || !p.Downed || Items.IsLich(p))return false;
   int now=Find.TickManager.TicksGame;if(h.lastRise>=0 && now-h.lastRise<RiseCooldown)return false;
   h.lastRise=now;
   foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>!i.IsPermanent()).ToList())inj.Heal(inj.Severity*(1f-RiseHealth));
   var loss=p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);if(loss!=null)loss.Severity=Math.Min(loss.Severity,.2f);
   p.health.CheckForStateChange(null,null);
   Items.Mote(p,"死亡骑士：再站起来",new Color(.55f,1f,.6f));
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"倒下又站了起来——死亡骑士的铠甲不让他倒下。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  // ---------- 红袍 ----------
  public static bool RedRobeSeen(Map map,Faction visitor){
   var a=MagicItems.Alliance;if(visitor==null || visitor!=a || map==null || a.HostileTo(Faction.OfPlayer))return false;
   var wearers=map.mapPawns.FreeColonistsSpawned.Where(p=>Items.Worn(p,"LP_RedWizardRobe")!=null).ToList();if(wearers.Count==0)return false;
   a.TryAffectGoodwillWith(Faction.OfPlayer,RedRobeGoodwill,true,true,null,wearers[0]);
   Messages.Message("同盟的商队看到"+wearers[0].LabelShort+"穿着塞伊红袍，脸色很难看：同盟好感"+RedRobeGoodwill+"。",wearers[0],MessageTypeDefOf.NegativeEvent,false);
   return true;
  }
  // ---------- 元素织袍 ----------
  public static float ElementFactor(Pawn p,DamageDef d){
   if(d==null || Items.Worn(p,"LP_ElementalWeave")==null)return 1f;var e=Genasi.Element(p);
   if(e=="Fire" && Troll.Burns(d) && d.defName.IndexOf("Acid",StringComparison.OrdinalIgnoreCase)<0)return .8f;
   if(e=="Water" && (d==DamageDefOf.Frostbite || d.defName.IndexOf("Frost",StringComparison.OrdinalIgnoreCase)>=0 || d.defName.IndexOf("Cold",StringComparison.OrdinalIgnoreCase)>=0))return .8f;
   if(e=="Earth" && d.armorCategory?.defName=="Blunt")return .8f;
   return 1f;
  }
  public static int ElementStage(Pawn p){switch(Genasi.Element(p)){case "Fire":return 1;case "Water":return 2;case "Earth":return 3;case "Air":return 4;}return 0;}
  // ---------- 射击修正：林中掩护、暗处 ----------
  public static bool InCover(Pawn p){var plant=p?.Spawned==true?p.Position.GetPlant(p.Map):null;return plant!=null && (plant.def.plant?.IsTree==true || plant.def.fillPercent>=.15f || plant.def.defName.IndexOf("Bush",StringComparison.OrdinalIgnoreCase)>=0);}
  public static bool Dark(Pawn p)=>p?.Spawned==true && p.Map.glowGrid.GroundGlowAt(p.Position,false,false)<.3f;
  static readonly DefRef<ThingDef> Woodwalker=new DefRef<ThingDef>("LP_WoodwalkerCloak"),Shadowweave=new DefRef<ThingDef>("LP_ShadowweaveCloak"); // 1.58.6：命中报告每发子弹都查
  public static float TargetFactor(Pawn t){
   if(t?.apparel==null)return 1f;float f=1f;
   if(Items.Worn(t,Woodwalker.Def)!=null && InCover(t))f*=.9f;
   if(Items.Worn(t,Shadowweave.Def)!=null && Dark(t))f*=.8f;
   return f;
  }
  // ---------- 偷袭 ----------
  public static bool Unaware(Pawn t){if(t==null || t.Dead || t.Drafted)return false;int now=Find.TickManager.TicksGame;var ms=t.mindState;if(ms==null)return true;return now-ms.lastAttackTargetTick>600 && now-ms.lastHarmTick>600;}
  // ---------- 派系换装 ----------
  public static int Outfit(Pawn p){
   var kind=p?.kindDef?.defName;if(kind==null || p.apparel==null || !Outfits.TryGetValue(kind,out var list))return 0;int n=0;
   for(int i=0;i<list.Length;i++){
    if(Rand.ValueSeeded(p.thingIDNumber^(0x4D41+i*977))>=list[i].chance)continue;
    var def=Items.Thing(list[i].def);if(def==null || ApparelGates.Blocked(p,def)!=null || !ApparelUtility.HasPartsToWear(p,def))continue;
    var stuff=def.MadeFromStuff?GenStuff.RandomStuffByCommonalityFor(def,p.Faction?.def.techLevel??TechLevel.Medieval):null;if(def.MadeFromStuff && stuff==null)stuff=GenStuff.DefaultStuffFor(def);
    var a=(Apparel)ThingMaker.MakeThing(def,stuff);
    foreach(var old in p.apparel.WornApparel.Where(w=>!ApparelUtility.CanWearTogether(def,w.def,p.RaceProps.body)).ToList()){p.apparel.Remove(old);old.Destroy();}
    p.apparel.Wear(a,false);n++;
   }
   return n;
  }
  public static Thing GiveHarperCloak(Map map){var def=Items.Thing("LP_HarperCloak");if(def==null || map==null)return null;var t=ThingMaker.MakeThing(def);DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map),map,new List<Thing>{t},110,false,false,true);return t;}
 }

 public class Hediff_ApparelSet : HediffWithComps {
  public int lastRise=-1,lastFreeDay=-1;public List<int> struck=new List<int>();
  int accumulated;
  public override bool ShouldRemove=>!MagicApparel.Complete(pawn,def);
  public override void TickInterval(int delta){
   base.TickInterval(delta);accumulated+=delta;if(accumulated<60)return;accumulated=0;
   if(def.defName=="LP_Set_Deathknight" && pawn.Downed && !pawn.Dead)MagicApparel.TryRise(pawn);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref lastRise,"lpLastRise",-1);Scribe_Values.Look(ref lastFreeDay,"lpLastFreeDay",-1);Scribe_Collections.Look(ref struck,"lpStruck",LookMode.Value);if(Scribe.mode==LoadSaveMode.PostLoadInit && struck==null)struck=new List<int>();}
 }
 public class CompProperties_SetAbility : CompProperties_AbilityEffect {public string effect;public float radius;public CompProperties_SetAbility(){compClass=typeof(CompSetAbility);}}
 public class CompSetAbility : CompAbilityEffect {
  public CompProperties_SetAbility Props=>(CompProperties_SetAbility)props;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   var caster=parent.pawn;if(caster?.Map==null)return;
   if(Props.effect=="holdoath"){Items.Give(caster,"LP_HoldOath");Items.Mote(caster,"坚守誓约",new Color(1f,.92f,.6f));}
   else if(Props.effect=="turnundead")TurnUndead(caster,Props.radius);
  }
  public static int TurnUndead(Pawn caster,float radius){
   int n=0;var holy=DefDatabase<DamageDef>.GetNamedSilentFail("LP_Radiant")??DamageDefOf.Burn;
   foreach(var p in caster.Map.mapPawns.AllPawnsSpawned.Where(p=>!p.Dead && p.HostileTo(caster) && p.Position.InHorDistOf(caster.Position,radius)).ToList()){
    if(Necro.Construct(p)!=null){p.TakeDamage(new DamageInfo(holy,30f,1f,-1f,caster));n++;}
    else if(Items.IsLich(p) || Items.Worn(p,"LP_DeathknightPlate")!=null){p.stances?.stunner?.StunFor(120,caster,false,true,false);n++;}
   }
   FleckMaker.Static(caster.Position,caster.Map,FleckDefOf.PsycastAreaEffect,radius);
   return n;
  }
  public override void DrawEffectPreview(LocalTargetInfo target){if(Props.radius>0)GenDraw.DrawRadiusRing(parent.pawn.Position,Props.radius);}
 }

 // ---------- 属性 ----------
 public static partial class MagicApparel {
  // 1.58.6：两个按人缓存的属性部件原来从不清理；超过 2000 条时只删已经过期（本来也会重算）的条目，结果不变。
  internal static void Store(Dictionary<int,(int tick,bool on)> cache,int id,int now,bool on,int window){
   if(cache.Count>2000){var old=new List<int>();foreach(var kv in cache)if(now-kv.Value.tick>=window || now<kv.Value.tick)old.Add(kv.Key);foreach(var k in old)cache.Remove(k);}
   cache[id]=(now,on);
  }
 }
 public class StatPart_KnightBanner : StatPart {
  static readonly Dictionary<int,(int tick,bool on)> cache=new Dictionary<int,(int,bool)>();
  public static bool Under(Pawn p){
   if(p?.Map==null || !p.Spawned)return false;int now=Find.TickManager.TicksGame;
   if(cache.TryGetValue(p.thingIDNumber,out var c) && now-c.tick<250 && now>=c.tick)return c.on;
   var def=Items.Thing("LP_KnightMantle");bool on=false;
   if(def!=null)foreach(var o in p.Map.mapPawns.AllPawnsSpawned)if(o.Faction==p.Faction && !o.Dead && o.Position.InHorDistOf(p.Position,MagicApparel.BannerRadius) && Items.Worn(o,def)!=null){on=true;break;}
   MagicApparel.Store(cache,p.thingIDNumber,now,on,250);return on;
  }
  public static void Reset()=>cache.Clear();
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Under(p))val-=MagicApparel.BannerOffset;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Under(p)?"骑士披风的旗帜: -"+MagicApparel.BannerOffset.ToStringPercent():null;
 }
 public class StatPart_RedRobeCommand : StatPart {
  public static bool Commanded(Pawn p){var m=Necro.Construct(p)?.master;return m!=null && Items.Worn(m,"LP_RedWizardRobe")!=null;}
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Commanded(p))val*=MagicApparel.RedRobeSpeed;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Commanded(p)?"塞伊的威势: x"+MagicApparel.RedRobeSpeed.ToStringPercent():null;
 }
 public class StatPart_OgreIntimidation : StatPart {
  static readonly Dictionary<int,(int tick,bool on)> cache=new Dictionary<int,(int,bool)>();
  public static bool Cowed(Pawn p){
   if(p?.Map==null || !p.Spawned)return false;int now=Find.TickManager.TicksGame;
   if(cache.TryGetValue(p.thingIDNumber,out var c) && now-c.tick<60 && now>=c.tick)return c.on;
   // 1.58.6：先比距离、再看有没有披战利品，最后才判断敌对（原来每个人都先算 HostileTo）。
   var def=Items.Thing("LP_OgreTrophyHide");bool on=false;
   if(def!=null)foreach(var o in p.Map.mapPawns.AllPawnsSpawned)if(!o.Dead && o.Position.InHorDistOf(p.Position,MagicApparel.IntimidateRadius) && Items.Worn(o,def)!=null && o.HostileTo(p)){on=true;break;}
   MagicApparel.Store(cache,p.thingIDNumber,now,on,60);return on;
  }
  public static void Reset()=>cache.Clear();
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Cowed(p))val-=MagicApparel.IntimidateOffset;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Cowed(p)?"被食人魔战利品吓到: -"+MagicApparel.IntimidateOffset.ToStringPercent():null;
 }
 // ---------- 心情 ----------
 public class ThoughtWorker_DeathknightWhispers : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p)=>Items.Worn(p,"LP_DeathknightPlate")!=null && !Items.Undead(p)?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
 }
 public class ThoughtWorker_BardMusic : ThoughtWorker {
  static readonly Dictionary<int,(int tick,List<IntVec3> spots)> census=new Dictionary<int,(int,List<IntVec3>)>();
  public static bool Playing(Pawn p)=>p.CurJobDef?.defName=="Play_MusicalInstrument" && Items.Worn(p,"LP_BardFinery")!=null;
  protected override ThoughtState CurrentStateInternal(Pawn p){
   if(!p.Spawned || !p.RaceProps.Humanlike)return ThoughtState.Inactive;var map=p.Map;int now=Find.TickManager.TicksGame;
   if(!census.TryGetValue(map.uniqueID,out var c) || now-c.tick>=250 || now<c.tick){c=(now,map.mapPawns.AllPawnsSpawned.Where(Playing).Select(b=>b.Position).ToList());census[map.uniqueID]=c;}
   return c.spots.Any(s=>s!=p.Position && s.InHorDistOf(p.Position,8f))?ThoughtState.ActiveAtStage(0):ThoughtState.Inactive;
  }
 }

 // ---------- 补丁 ----------
 // 纹章色：穿着时把遮罩绿色区的第二色换成意识形态色 / 派系色（元素织袍按元素）。
 [HarmonyPatch(typeof(ApparelGraphicRecordGetter),nameof(ApparelGraphicRecordGetter.TryGetGraphicApparel))]
 public static class HeraldryGraphic {
  static void Postfix(Apparel apparel,ref ApparelGraphicRecord rec,bool __result){
   if(!__result || rec.graphic==null || apparel?.def.GetModExtension<HeraldryExtension>()==null)return;
   var g=rec.graphic;var two=MagicApparel.Herald(apparel.Wearer,apparel);
   rec.graphic=GraphicDatabase.Get<Graphic_Multi>(g.path,g.Shader,g.drawSize,g.color,two);
  }
 }
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.Notify_DamageApplied))]
 public static class ApparelEmpImmunity {static bool Prefix(Pawn ___parentPawn,DamageInfo __0)=>__0.Def!=DamageDefOf.EMP || ___parentPawn?.apparel==null || !___parentPawn.apparel.WornApparel.Any(a=>a.def.HasModExtension<EmpStunImmunity>());}
 // 矮人穿符文重铠：移速惩罚减半。
 [HarmonyPatch(typeof(StatWorker),nameof(StatWorker.StatOffsetFromGear))]
 public static class DwarfRuneplateSpeed {static void Postfix(Thing gear,StatDef stat,ref float __result){if(__result<0 && stat==StatDefOf.MoveSpeed && gear?.def.defName=="LP_DwarvenRuneplate" && CommonRaces.IsDwarf((gear as Apparel)?.Wearer))__result*=.5f;}}
 // 林行者斗篷、暗影斗篷：被远程命中的概率。
 [HarmonyPatch(typeof(ShotReport),nameof(ShotReport.HitReportFor))]
 public static class MagicApparelShotReport {
  static readonly AccessTools.StructFieldRef<ShotReport,float> size=AccessTools.StructFieldRefAccess<ShotReport,float>("factorFromTargetSize");
  static void Postfix(LocalTargetInfo target,ref ShotReport __result){float f=MagicApparel.TargetFactor(target.Thing as Pawn);if(f!=1f)size(ref __result)*=f;}
 }
 // 影行者：偷袭。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"DamageInfosToApply")]
 public static class ShadowSneakAttack {
  static IEnumerable<DamageInfo> Postfix(IEnumerable<DamageInfo> values,Verb_MeleeAttackDamage __instance,LocalTargetInfo target){
   var caster=__instance.CasterPawn;var victim=target.Pawn;var h=MagicApparel.SetHediff(caster,"LP_Set_Shadow");
   bool sneak=h!=null && victim!=null && !h.struck.Contains(victim.thingIDNumber) && MagicApparel.Unaware(victim);
   if(sneak){h.struck.Add(victim.thingIDNumber);if(h.struck.Count>200)h.struck.RemoveAt(0);Items.Mote(victim,"偷袭！",new Color(.8f,.3f,.3f));}
   foreach(var d in values){if(sneak){var x=d;x.SetAmount(d.Amount*MagicApparel.SneakFactor);yield return x;}else yield return d;}
  }
 }
 // 死亡骑士铠甲：近战攻击者10%恐惧；龙骑士：受伤时己方龙冲向攻击者。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.PostApplyDamage))]
 public static class MagicApparelOnHurt {
  static readonly Dictionary<int,int> dragonCalls=new Dictionary<int,int>();
  static void Postfix(Pawn ___pawn,DamageInfo dinfo,float totalDamageDealt){
   var p=___pawn;if(p?.apparel==null || p.Dead || !(dinfo.Instigator is Pawn att) || att==p || !att.HostileTo(p))return;
   if(!ForceDragon.IsRanged(dinfo) && Items.Worn(p,"LP_DeathknightPlate")!=null && Rand.Chance(.1f) && !Items.IsMinotaur(att) && !Races124.IsLoxodon(att))LichSpells.Frighten(p,att);
   if(totalDamageDealt>0 && MagicApparel.SetHediff(p,"LP_Set_Dragonknight")!=null && p.Spawned)CallDragons(p,att);
  }
  public static int CallDragons(Pawn knight,Pawn att){
   int n=0,now=Find.TickManager.TicksGame;
   foreach(var d in knight.Map.mapPawns.SpawnedPawnsInFaction(knight.Faction).ToList()){
    if(d==knight || !ForceDragon.Active(d) || d.Downed || d.Drafted || d.InMentalState || !d.Position.InHorDistOf(knight.Position,30f))continue;
    if(dragonCalls.TryGetValue(d.thingIDNumber,out var t) && now-t<600)continue;
    dragonCalls[d.thingIDNumber]=now;d.jobs?.StartJob(JobMaker.MakeJob(JobDefOf.AttackMelee,att),JobCondition.InterruptForced);n++;
   }
   return n;
  }
 }
 // 冷却：巫妖穿大法师长袍死灵冲击-10%；吉斯洋基戴战冠星界跳跃-10%；龙骑士身边的龙吐息-15%。
 [HarmonyPatch(typeof(Ability),nameof(Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class MagicApparelCooldowns {
  static void Postfix(Ability __instance,bool __result){
   if(!__result || __instance.CooldownTicksRemaining<=0)return;var p=__instance.pawn;var name=__instance.def.defName;float f=1f;
   if(name=="LP_NecroticBlast" && Items.IsLich(p) && Items.Worn(p,"LP_ArchmageRobe")!=null)f*=.9f;
   if(name=="LP_AstralStep" && Items.IsGith(p) && Items.Worn(p,"LP_GithCrestedHelm")!=null)f*=.9f;
   if(name=="LP_ForceBreath" && p?.Map!=null && p.Map.mapPawns.AllPawnsSpawned.Any(o=>o.Faction==p.Faction && o.Position.InHorDistOf(p.Position,10f) && MagicApparel.SetHediff(o,"LP_Set_Dragonknight")!=null))f*=MagicApparel.DragonBreathFactor;
   if(f!=1f)__instance.StartCooldown(Mathf.RoundToInt(__instance.CooldownTicksRemaining*f));
  }
 }
 // 巫妖裹尸长袍：命匣献祭经验+10%。
 [HarmonyPatch(typeof(Phylactery),nameof(Phylactery.Feed))]
 public static class LichShroudFeed {static void Prefix(Phylactery __instance,ref float amount){if(amount>0 && Items.Worn(__instance.soul,"LP_LichShroud")!=null)amount*=1.1f;}}
 // 龙骑士战甲：龙不嫌弃同住的骑士。
 [HarmonyPatch(typeof(ThoughtWorker_DragonSharedRoom),nameof(ThoughtWorker_DragonSharedRoom.Shares))]
 public static class DragonknightRoommate {
  static void Postfix(Pawn p,ref bool __result){
   if(!__result)return;var room=p.ownership?.OwnedBed?.GetRoom();if(room==null)return;
   __result=DragonHoard.BedOwners(room).Any(o=>o!=p && !ForceDragon.Active(o) && Items.Worn(o,"LP_DragonknightArmor")==null);
  }
 }
 // 鸟人羽翼轻甲：翱翔落地后3秒闪避+10。
 [HarmonyPatch(typeof(Aarakocra),nameof(Aarakocra.Soar))]
 public static class SkyguardSoar {static void Postfix(Pawn caster,bool __result){if(__result && Items.Worn(caster,"LP_SkyguardHarness")!=null)Items.Give(caster,"LP_SkyguardDodge",180);}}
 // 竖琴手斗篷：商队快5%；竖琴手预警附赠斗篷（最多3件）。
 [HarmonyPatch(typeof(CaravanTicksPerMoveUtility),nameof(CaravanTicksPerMoveUtility.GetTicksPerMove),new[]{typeof(Caravan),typeof(System.Text.StringBuilder)})]
 public static class HarperCloakCaravan {static void Postfix(Caravan caravan,ref int __result){if(caravan?.PawnsListForReading?.Any(p=>Items.Worn(p,"LP_HarperCloak")!=null)==true)__result=Mathf.Max(1,Mathf.RoundToInt(__result*.95f));}}
 [HarmonyPatch(typeof(Harper),nameof(Harper.TryDelay))]
 public static class HarperCloakGift {
  static void Postfix(IncidentParms parms,bool __result){
   var s=MagicItemsState.Get;if(!__result || s==null || s.harperCloaks>=3 || !Harper.Active() || !(parms?.target is Map map))return;
   if(MagicApparel.GiveHarperCloak(map)!=null){s.harperCloaks++;Messages.Message("竖琴手的密信里还夹着一件旅人斗篷——竖琴手的标志。",MessageTypeDefOf.PositiveEvent,false);}
  }
 }
 // 塞伊红袍：同盟商队来访时看到红袍，好感-5（每次来访一次）。
 [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival),"TryExecuteWorker")]
 public static class RedRobeCaravan {static void Postfix(IncidentParms parms,bool __result){if(__result)MagicApparel.RedRobeSeen(parms?.target as Map,parms?.faction);}}
 // 游侠套装：野生动物不主动攻击。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.ThreatDisabled))]
 public static class RangerWildAnimals {static void Postfix(Pawn __instance,IAttackTargetSearcher __0,ref bool __result){if(!__result && __0?.Thing is Pawn a && a.RaceProps.Animal && a.Faction==null && __instance.health?.hediffSet?.GetFirstHediffOfDef(Ranger.Def) is Hediff_ApparelSet)__result=true;}static readonly DefRef<HediffDef> Ranger=new DefRef<HediffDef>("LP_Set_Ranger");} // 1.58.6：野兽索敌时每个目标都会问，用静态 Def
 // 派系换装（概率件）。
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class MagicApparelOutfit {static void Postfix(Pawn __result){try{if(__result?.Faction!=null && !__result.Faction.IsPlayer)MagicApparel.Outfit(__result);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 派系换装出错："+e,9127461);}}}
}
