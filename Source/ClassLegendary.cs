using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.74.0 传奇职业能力（见 ArtSource/Classes-1.74.0-spec.md / report.md）：传奇冒险者 25 / 30 / 35 / 40 级各 1 点传奇职业点（老存档按当前等级补发一次，靠 legendaryGranted 计数）。
 // 每个职业 2 个（LP_ClassDef.legendary：1 主动 + 1 被动），学第一个要那个职业 7 级，同职业第二个 10 级（1.87.0 前 9 / 17；10+10 兼修可以用满 4 点）；每学一个，那个职业的资源上限 +2。
 // 被动的效果：属性（StatPart_LegendaryClassPower）、状态（无相之身、血脉觉醒，在 Tick250 里给 / 收）、挂钩（不屈战魂、誓约不灭、永恒德鲁伊、魂铸）和资源消耗（法术强效、神圣守护、永恒德鲁伊）。
 public static class ClassLegendary {
  public static readonly int[] Thresholds={25,30,35,40};
  public const int FirstLevel=7,SecondLevel=10,ResourceBonus=2,OathRadius=8,OathSave=2;
  public const float MasteryFactor=1.3f,SoulforgeLegendChance=.1f,UnyieldingHeal=.5f,BossStunChance=.5f;
  static Dictionary<AbilityDef,LP_ClassDef> byAbility;static int stamp=-1;
  public static LP_ClassDef ClassOf(AbilityDef d){
   if(byAbility==null || stamp!=DefDatabase<LP_ClassDef>.DefCount){byAbility=new Dictionary<AbilityDef,LP_ClassDef>();foreach(var c in ClassSystem.All)foreach(var a in c.legendary)if(a!=null)byAbility[a]=c;stamp=DefDatabase<LP_ClassDef>.DefCount;}
   return d!=null && byAbility.TryGetValue(d,out var c2)?c2:null;
  }
  public static bool IsLegendary(AbilityDef d)=>ClassOf(d)!=null;
  // ---------- 传奇职业点 ----------
  public static int Due(Hediff_Adventurer h){int n=0;if(h!=null)foreach(var t in Thresholds)if(h.level>=t)n++;return n;}
  public static int SyncPoints(Hediff_Adventurer h){
   if(h?.classes==null)return 0;int due=Due(h),add=due-h.classes.legendaryGranted;if(add<=0)return 0;
   h.classes.legendaryGranted=due;h.classes.legendaryPoints+=add;return add;
  }
  public static int Points(Hediff_Adventurer h)=>h?.classes?.legendaryPoints??0;
  public static int Count(Hediff_Adventurer h,LP_ClassDef c){if(h?.classes==null || c==null)return 0;int n=0;foreach(var a in h.classes.legendary)if(ClassOf(a)==c)n++;return n;}
  public static bool Knows(Hediff_Adventurer h,AbilityDef d)=>h?.classes!=null && d!=null && h.classes.legendary.Contains(d);
  public static int NeededLevel(Hediff_Adventurer h,LP_ClassDef c)=>Count(h,c)==0?FirstLevel:SecondLevel;
  public static string CanLearn(Hediff_Adventurer h,LP_ClassDef c,AbilityDef d){
   if(h?.classes==null || c==null || d==null || !c.legendary.Contains(d))return "没有这个能力";
   if(Knows(h,d))return "已经学会";
   if(Points(h)<=0)return "没有传奇职业点（传奇冒险者 25 / 30 / 35 / 40 级各 1 点）";
   int need=NeededLevel(h,c);if(ClassSystem.Level(h,c)<need)return "需要"+c.label+" "+need+" 级"+(need==SecondLevel?"（同职业的第二个传奇能力）":"");
   return null;
  }
  public static bool Learn(Hediff_Adventurer h,LP_ClassDef c,AbilityDef d,bool message=false){
   if(CanLearn(h,c,d)!=null)return false;
   h.classes.legendaryPoints--;h.classes.legendary.Add(d);
   if(h.pawn?.abilities!=null && h.pawn.abilities.GetAbility(d)==null)h.pawn.abilities.GainAbility(d);
   if(c.resource!=ClassResource.None)ClassSystem.AddResource(h,c,ResourceBonus); // 新的上限那 2 点直接补上
   Passives(h);
   if(message && h.pawn?.Faction!=null && h.pawn.Faction.IsPlayer)Messages.Message(h.pawn.LabelShort+"领悟了传奇能力「"+d.label+"」（"+c.label+"）",h.pawn,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  // 资源上限加成：池里所有职业学会的传奇能力数 × 2（神恩共用池里牧师、圣武士都算）。
  public static int ResourceFor(Hediff_Adventurer h,LP_ClassDef c){
   if(h?.classes==null || h.classes.legendary.Count==0 || c==null)return 0;var key=ClassSystem.PoolKey(c);int n=0;
   foreach(var a in h.classes.legendary){var o=ClassOf(a);if(o!=null && ClassSystem.PoolKey(o)==key)n++;}
   return n*ResourceBonus;
  }

  // ---------- 资源消耗的修改 ----------
  static readonly DefRef<AbilityDef> mastery=new DefRef<AbilityDef>("LP_LegSpellMastery"),guardian=new DefRef<AbilityDef>("LP_LegDivineGuardian"),timeless=new DefRef<AbilityDef>("LP_LegTimelessDruid");
  public static bool HasLeg(Pawn p,DefRef<AbilityDef> r){var d=r.Def;return d!=null && Knows(Adventurer.Get(p),d);}
  public static bool HasLeg(Pawn p,string name){var d=DefCache<AbilityDef>.Get(name);return d!=null && Knows(Adventurer.Get(p),d);}
  public static int Cost(Pawn p,AbilityDef def,CompProperties_ClassEffect props){
   int cost=props?.cost??0;if(cost<=0 || def==null)return cost;
   if(def.defName=="LP_WildShape" && HasLeg(p,timeless))return 0;
   if(def.defName=="LP_Raise" && HasLeg(p,guardian))cost=2;
   if(ClassSystem.ClassOf(def)?.defName=="LP_Class_Wizard" && HasLeg(p,mastery))cost=Math.Max(1,cost-1);
   int fr=ClassFeats.CostReduce(p,def);if(fr>0)cost=Math.Max(1,cost-fr); // 1.88.0 职业专属专长
   return cost;
  }
  public static float PowerFactor(Pawn p,CompProperties_ClassEffect e)=>e?.stat!=null && e.stat.defName=="LP_ArcanePower" && HasLeg(p,mastery)?MasteryFactor:1f;
  public static bool Torrent(Pawn p)=>Items.Has(p,"LP_LegArcaneTorrent");
  public static void AfterCast(Ability_Class a){
   var p=a.pawn;var e=a.Props;if(p==null || e==null || e.effect=="ArcaneTorrent" || e.cost<=0)return;
   var def=Items.Hediff("LP_LegArcaneTorrent");var h=def==null?null:p.health.hediffSet.GetFirstHediffOfDef(def);if(h==null)return;
   a.ResetCooldown();h.Severity-=1f;if(h.Severity<.5f)p.health.RemoveHediff(h);
   Items.Mote(p,"魔力奔流",ClassCasters.Chaos);
  }

  // ---------- 被动（每 250 刻，职业者自己的冒险者状态里） ----------
  public static readonly HashSet<Pawn> OathHolders=new HashSet<Pawn>();
  static readonly DefRef<AbilityDef> formless=new DefRef<AbilityDef>("LP_LegFormless"),awakening=new DefRef<AbilityDef>("LP_LegBloodlineAwakening"),oath=new DefRef<AbilityDef>("LP_LegUndyingOath");
  public static void Passives(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null || h.classes==null)return;
   if(h.classes.legendary.Count==0){OathHolders.Remove(p);return;}
   if(p.Dead){OathHolders.Remove(p);return;}
   bool f=Knows(h,formless.Def);if(f && !Items.Has(p,"LP_LegFormlessBuff"))Items.Give(p,"LP_LegFormlessBuff");else if(!f)Items.Remove(p,"LP_LegFormlessBuff");
   bool b=Knows(h,awakening.Def) && ClassSystem.Bloodline(p);if(b && !Items.Has(p,"LP_LegBloodlineAwake"))Items.Give(p,"LP_LegBloodlineAwake");else if(!b)Items.Remove(p,"LP_LegBloodlineAwake");
   if(Knows(h,oath.Def))OathHolders.Add(p);else OathHolders.Remove(p);
  }
  // 誓约不灭：自己或 8 格内同阵营有誓约的圣武士。
  public static bool OathNear(Pawn p){
   if(OathHolders.Count==0 || p==null)return false;
   foreach(var o in OathHolders){if(o==null || o.Dead || o.Destroyed)continue;if(o==p)return true;if(o.Spawned && p.Spawned && o.Map==p.Map && o.Faction!=null && o.Faction==p.Faction && o.Position.InHorDistOf(p.Position,OathRadius))return true;}
   return false;
  }
  // 不屈战魂：每天一次，本应倒地时治好一半伤势、斗志回满（在死亡守卫之后检查）。
  static readonly DefRef<AbilityDef> unyielding=new DefRef<AbilityDef>("LP_LegUnyielding");
  public static bool TryUnyielding(Pawn p){
   if(p==null || p.Dead || p.Downed)return false;var h=Adventurer.Get(p);var d=unyielding.Def;
   if(h?.classes==null || h.classes.legendary.Count==0 || !Knows(h,d) || !ClassCasters.WouldDown(p))return false;
   h.classes.legendaryTicks.TryGetValue(d.defName,out int last);int now=Find.TickManager.TicksGame;
   if(last>0 && now-last<GenDate.TicksPerDay)return false;
   h.classes.legendaryTicks[d.defName]=now;
   ClassMelee.Heal(p,ClassMelee.InjurySum(p)*UnyieldingHeal);
   ClassSystem.AddResource(h,ClassSystem.Fighter,999f);
   Items.Mote(p,"不屈战魂",ClassEffects.MartialColor);
   if(p.Faction==Faction.OfPlayer)Messages.Message(p.LabelShort+"的不屈战魂：本该倒下，却又站了起来。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  // 魂铸：DND 武器品质 +1，10% 直接传说。
  public static QualityCategory Soulforged(QualityCategory q,float roll){if(roll<SoulforgeLegendChance)return QualityCategory.Legendary;return q>=QualityCategory.Legendary?q:q+1;}

  // ---------- 主动 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   switch(e.effect){
    case "ThousandArmies":ThousandArmies(p,target.Cell,e);return true;
    case "HeavenPalm":{var t=target.Thing as Pawn;if(t!=null && !t.Dead && t.HostileTo(p))HeavenPalm(p,t,ClassEffects.Power(p,e),ClassEffects.Duration(p,e),Rand.Value);return true;}
    case "MeteorSwarm":MeteorSwarm(p,target.Cell,ClassEffects.Radius(p,e),ClassEffects.Power(p,e));return true;
    case "DivineIntervention":DivineIntervention(p);return true;
    case "NatureWrath":NatureWrath(p,target.Cell,ClassEffects.Radius(p,e),ClassEffects.Power(p,e),ClassEffects.Duration(p,e));return true;
    case "ArcaneTorrent":Items.Give(p,"LP_LegArcaneTorrent",60000,3f);Items.Mote(p,"魔力奔流",ClassCasters.Chaos);return true;
    case "WarGolem":WarGolem(p,target.Cell,ClassEffects.Power(p,e));return true;
   }
   return false;
  }
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   switch(e.effect){
    case "HeavenPalm":{var t=target.Thing as Pawn;if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;}
    case "WarGolem":return ClassCasters.TurretCannot(caster.Map,target.Cell);
   }
   return null;
  }
  public static List<Pawn> Cone(Pawn p,IntVec3 toward,float range){
   var map=p.Map;var l=new List<Pawn>();if(map==null)return l;
   var dir=(toward-p.Position).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(0,0,1);
   foreach(var t in Races124.PawnsNear(map,p.Position,range).ToList())if(t!=p && !t.Dead && t.HostileTo(p) && Vector3.Angle(dir,(t.Position-p.Position).ToVector3())<=45f)l.Add(t);
   return l;
  }
  // 千军破：近战 5 格扇形每人一次 ×3 近战结算；远程武器射程内扇形每人一发 ×3 必中子弹。
  public static int ThousandArmies(Pawn p,IntVec3 toward,CompProperties_ClassEffect e){
   var verb=Martial.RangedVerb(p);int n=0;
   if(verb?.Projectile!=null){
    foreach(var t in Cone(p,toward,Mathf.Min(verb.EffectiveRange,30f))){if(Martial.Fire(p,t,new ShotMod{damage=e.power,caster=p},true)!=null)n++;}
   }else{
    Items.Give(p,"LP_WhirlwindFury",60,e.power);
    try{foreach(var t in Cone(p,toward,e.radius)){ClassMelee.Strike(p,t,8f*e.power);n++;}}
    finally{Items.Remove(p,"LP_WhirlwindFury");}
   }
   Items.Mote(p,"千军破",ClassEffects.MartialColor);
   return n;
  }
  public static bool Boss(Pawn t)=>t!=null && (Legendary.Is(t) || Adventurer.Get(t)?.storyLevel==true);
  // 天震掌：近战一击 + 躯干钝器 + 击晕；传奇怪物 / 剧情首领有 50% 成功率挣脱击晕。返回是否击晕。
  public static bool HeavenPalm(Pawn p,Pawn t,float amount,int stun,float roll){
   ClassMelee.Strike(p,t);if(t.Dead)return false;
   t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,amount,.8f,-1f,p,t.RaceProps.body.corePart));
   if(t.Dead)return false;
   if(Boss(t) && roll<BossStunChance){Items.Mote(t,"挣脱",MonkCore.Ki);return false;}
   t.stances?.stunner?.StunFor(stun,p);
   if(t.Spawned)FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,2f);
   Items.Mote(t,"天震掌",MonkCore.Ki);
   return true;
  }
  public static int MeteorSwarm(Pawn p,IntVec3 c,float radius,float amount){
   var map=p.Map;if(map==null || !c.IsValid)return 0;int n=0;
   for(int i=0;i<4;i++){
    var cell=i==0?c:CellFinder.RandomClosewalkCellNear(c,map,Mathf.Max(1,Mathf.RoundToInt(radius)));
    if(!cell.InBounds(map))continue;
    GenExplosion.DoExplosion(cell,map,2.5f,DamageDefOf.Flame,p,damAmount:Mathf.RoundToInt(amount),armorPenetration:.4f);n++;
   }
   return n;
  }
  public static int DivineIntervention(Pawn p){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var c in map.mapPawns.FreeColonistsSpawned.ToList()){
    ClassMelee.Heal(c,9999f);
    foreach(var d in c.health.hediffSet.hediffs.Where(ClassCasters.IsDisease).ToList())c.health.RemoveHediff(d);
    n++;
   }
   foreach(var corpse in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().ToList())
    if(corpse.InnerPawn?.Faction==Faction.OfPlayer && ClassCasters.RaiseCannot(corpse)==null && ClassCasters.Raise(corpse))n++;
   var god=FaithUtility.DevoteeGod(p)??FaithUtility.Patron;
   Messages.Message((god!=null?god.label:"神明")+"回应了"+p.LabelShort+"的祈祷：伤者痊愈，亡者复苏。",p,MessageTypeDefOf.PositiveEvent,false);
   if(p.Spawned)FleckMaker.ThrowLightningGlow(p.DrawPos,map,4f);
   return n;
  }
  public static int NatureWrath(Pawn p,IntVec3 c,float radius,float amount,int ticks){
   var map=p.Map;if(map==null || !c.IsValid)return 0;
   var foes=Races124.PawnsNear(map,c,radius).Where(t=>t!=p && !t.Dead && t.HostileTo(p)).ToList();
   for(int i=0;i<4;i++){var at=foes.Count>0?foes[i%foes.Count].Position:CellFinder.RandomClosewalkCellNear(c,map,Mathf.Max(1,Mathf.RoundToInt(radius)));ClassCasters.CallLightning(p,at,amount);}
   foreach(var t in foes)if(!t.Dead)Items.Give(t,"LP_Entangled",ticks,t.BodySize>=2f?2f:1f);
   return foes.Count;
  }
  public static Building_RuneTurret WarGolem(Pawn p,IntVec3 cell,float power){
   var def=DefCache<ThingDef>.Get("LP_RuneWarGolem");var map=p?.Map;if(def==null || ClassCasters.TurretCannot(map,cell)!=null)return null;
   var t=(Building_RuneTurret)ThingMaker.MakeThing(def);t.power=power;t.SetFaction(p.Faction);
   GenSpawn.Spawn(t,cell,map);FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,3f,new Color(.85f,.75f,.45f));
   return t;
  }
 }

 // 血脉觉醒：受到血脉对应元素的伤害 ×0.5（按异种 / 六臂蛇人 / 坎比恩查）。
 public class Hediff_BloodlineAwake : HediffWithComps {
  HediffStage stage;
  public static readonly Dictionary<string,string[]> Elements=new Dictionary<string,string[]>{
   {"LP_ForceDragon",new[]{"LP_Force"}},{"LP_GenasiFire",new[]{"Flame","Burn"}},{"LP_Genie",new[]{"Flame","Burn"}},{"LP_Cambion",new[]{"Flame","Burn"}},
   {"LP_GenasiWater",new[]{"Frostbite","LP_Frost"}},{"LP_GenasiEarth",new[]{"Blunt"}},{"LP_Titan",new[]{"Blunt"}},
   {"LP_GenasiAir",new[]{"LP_Lightning","EMP"}},{"LP_StormGiant",new[]{"LP_Lightning","EMP"}},{"LP_CloudGiant",new[]{"LP_Lightning","EMP"}}};
  public static string[] For(Pawn p){
   if(p==null)return new string[0];
   if(Marilith.Is(p))return new[]{"Cut"};
   if(AbyssRaces.IsCambion(p))return Elements["LP_Cambion"];
   var x=p.genes?.Xenotype?.defName;return x!=null && Elements.TryGetValue(x,out var e)?e:new string[0];
  }
  public override HediffStage CurStage{get{
   if(stage==null){
    stage=new HediffStage{damageFactors=new List<DamageFactor>()};
    foreach(var n in For(pawn)){var d=DefCache<DamageDef>.Get(n);if(d!=null)stage.damageFactors.Add(new DamageFactor{damageDef=d,factor=.5f});}
   }
   return stage;
  }}
 }
 // 传奇被动的强度加成（神圣守护：神恩强度 +0.2；血脉觉醒：天赋强度 +0.2）。
 public class StatPart_LegendaryClassPower : StatPart {
  public string ability;public float bonus=.2f;
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && ClassLegendary.HasLeg(p,ability))val+=bonus;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && ClassLegendary.HasLeg(p,ability)?"传奇「"+(DefCache<AbilityDef>.Get(ability)?.label??ability)+"」：+"+bonus.ToStringPercent():null;
 }

 // ==================== Harmony ====================
 // 誓约不灭：豁免 +2（= 成功率 +10%）。
 [HarmonyPatch(typeof(Legendary),nameof(Legendary.SaveBonus))]
 public static class LegendaryOathSave {
  static void Postfix(Pawn p,ref int __result){if(ClassLegendary.OathHolders.Count>0 && ClassLegendary.OathNear(p))__result+=ClassLegendary.OathSave;}
 }
 // 永恒德鲁伊：成年后不再衰老。
 [HarmonyPatch(typeof(Pawn_AgeTracker),"get_BiologicalTicksPerTick")]
 public static class LegendaryTimeless {
  static readonly DefRef<AbilityDef> timeless=new DefRef<AbilityDef>("LP_LegTimelessDruid");
  static void Postfix(Pawn ___pawn,ref float __result){
   if(__result<=0f || ___pawn==null)return;var h=Adventurer.Get(___pawn);
   if(h?.classes==null || h.classes.legendary.Count==0 || !ClassLegendary.Knows(h,timeless.Def) || !___pawn.ageTracker.Adult)return;
   __result=0f;
  }
 }
 // 魂铸：DND 武器（器械师门槛表）品质 +1，10% 传说。
 [HarmonyPatch(typeof(GenRecipe),"PostProcessProduct")]
 public static class LegendarySoulforge {
  static void Postfix(Thing __result,Pawn worker){
   if(__result==null || worker==null || !Artificer.RequiredAbility.ContainsKey(__result.def.defName) || !ClassLegendary.HasLeg(worker,"LP_LegSoulforge"))return;
   var q=__result.TryGetComp<CompQuality>();if(q==null)return;
   var nq=ClassLegendary.Soulforged(q.Quality,Rand.Value);if(nq!=q.Quality)q.SetQuality(nq,ArtGenerationContext.Colony);
  }
 }
}
