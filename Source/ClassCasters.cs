using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LichPhylactery {
 // 1.73.0 施法职业（法师、牧师、德鲁伊、术士、器械师）的能力效果，从 VPE 桥（Abilities.cs、Druid.cs、Sorcerer.cs、Artificer.cs）移来，数值不变。
 // 由 ClassEffects.Apply / Invalid 转过来（effect 名字见 Defs/Classes2.xml）；施放后的处理（超魔、魔涌、召雷冷却、魔网之子 / 仪式余韵）在 AfterCast。
 public static class ClassCasters {
  public static readonly Color Arcane=new Color(.7f,.5f,1f),Divine=new Color(1f,.9f,.5f),Nature=new Color(.55f,.85f,.45f),Chaos=new Color(.9f,.45f,.75f),Artisan=new Color(.95f,.8f,.45f);
  static bool Hostile(Pawn caster,Pawn t)=>t!=null && t!=caster && !t.Dead && t.HostileTo(caster);
  static bool Friendly(Pawn caster,Pawn t)=>t!=null && !t.Dead && !t.HostileTo(caster);
  static float Stat(Pawn p,CompProperties_ClassEffect e)=>ClassSystem.StatValue(p,e);

  // ---------- 选目标 ----------
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;
   switch(e.effect){
    case "Heal":case "TargetBuff":case "LesserRestoration":case "DeathWard":case "QuickRepair":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";
     if(t.HostileTo(caster))return "只能对友军使用。";
     return null;
    case "MagicMissile":case "SacredFlame":case "ChaosBolt":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";
     if(!t.HostileTo(caster))return "只能对敌人使用。";
     return null;
    case "Sleep":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";
     if(!t.HostileTo(caster))return "只能对敌人使用。";
     return SleepCannot(t);
    case "Disintegrate":
     if(t!=null)return t.Dead?"需要选择一个活着的目标。":!t.HostileTo(caster)?"只能对敌人使用。":null;
     if(target.Thing is Building b && (b.Faction==null || b.Faction!=caster.Faction))return null;
     return "只能对敌人或别人的建筑使用。";
    case "Raise":return RaiseCannot(target.Thing as Corpse);
    case "AnimalFriendship":return FriendshipCannot(t);
    case "CharmPerson":
     if(t==null || t.Dead || !t.RaceProps.Humanlike)return "只能对人形使用。";
     if(!t.HostileTo(caster) && !t.IsPrisonerOfColony)return "只能对敌人或囚犯使用。";
     return null;
    case "Teleport":{
     var map=caster.Map;var c=target.Cell;
     if(map==null || !c.IsValid || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map) || c.GetFirstPawn(map)!=null)return "需要一个空着的、能站立的格子。";
     float r=Mathf.Min(e.maxRange>0f?e.maxRange:999f,e.range*Stat(caster,e));
     return caster.Position.DistanceTo(c)>r?"超出传送距离（"+r.ToString("0.#")+" 格）。":null;}
    case "DragonWings":return Races124.BasicLanding(caster,target.Cell,Mathf.RoundToInt(e.range*Stat(caster,e)));
    case "RuneTurret":return TurretCannot(caster.Map,target.Cell);
   }
   return null;
  }

  // ---------- 效果 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(!Known.Contains(e.effect))return false;
   var t=target.Thing as Pawn;
   ApplyOne(p,def,e,target);
   // 超魔·孪生：单体术士法术多打主目标 6 格内另一个敌人。
   if(e.single && t!=null && Items.Has(p,"LP_Twinned")){var x=TwinTarget(p,t);if(x!=null){ApplyOne(p,def,e,x);Items.Remove(p,"LP_Twinned");}}
   return true;
  }
  static readonly HashSet<string> Known=new HashSet<string>{"MagicMissile","Fireball","Sleep","LightningBolt","TargetBuff","WallOfStone","Teleport","Disintegrate","Heal","LesserRestoration","SacredFlame","TurnUndead","MassHeal","DeathWard","Raise",
   "AnimalFriendship","Entangle","ConjureAnimals","WildShape","PlantGrowth","BeastCommand","CallLightning","ControlWeather","ChaosBolt","BurningHands","CharmPerson","Twin","DragonWings","IceStorm","MassSuggestion","Empower","QuickRepair","RuneTurret","MirrorImage"};
  static void ApplyOne(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;var cell=target.Cell;float s=ClassEffects.Strength(p,e),pw=ClassEffects.Power(p,e);int dur=ClassEffects.Duration(p,e);float rad=ClassEffects.Radius(p,e);
   switch(e.effect){
    // 法师
    case "MagicMissile":if(Hostile(p,t))MagicMissile(p,t,s);return;
    case "Fireball":if(cell.IsValid && p.Map!=null)GenExplosion.DoExplosion(cell,p.Map,rad,DamageDefOf.Flame,p,damAmount:Mathf.RoundToInt(pw),armorPenetration:.3f);return;
    case "Sleep":if(SleepCannot(t)!=null)return;if(DNDMind.TryResist(t)){Items.Mote(t,"抵抗了睡眠术",Arcane);return;}Items.Give(t,"LP_MagicSleep",dur,s);Items.Mote(t,"睡眠",Arcane);return;
    case "LightningBolt":if(cell.IsValid && p.Map!=null)LightningBolt(p,cell,pw);return;
    case "TargetBuff":
     if(!Friendly(p,t))return;Items.Give(t,e.hediff,dur,e.fixedSeverity?1f:s);
     if(e.hediff=="LP_Blessed"){var th=DefCache<ThoughtDef>.Get("LP_BlessedThought");if(th!=null)t.needs?.mood?.thoughts?.memories?.TryGainMemory(th);}
     Items.Mote(t,def.LabelCap,e.hediff=="LP_Haste"?Arcane:e.hediff=="LP_Barkskin"?Nature:Divine);return;
    case "WallOfStone":if(cell.IsValid && p.Map!=null)WallOfStone(p,cell,Mathf.RoundToInt(pw));return;
    case "Teleport":if(!cell.IsValid || p.Map==null || !cell.Standable(p.Map))return;FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,2f,Arcane);Races124.Blink(p,cell);FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,2f,Arcane);return;
    case "Disintegrate":Disintegrate(p,target.Thing,pw);return;
    case "MirrorImage":Items.Give(p,"LP_MirrorImage",dur,MirrorImages);Items.Mote(p,"镜影术",Arcane);return; // 1.81.0 热修4
    // 牧师
    case "Heal":if(Friendly(p,t)){float h=ClassMelee.Heal(t,pw);Items.Mote(t,"治疗 "+h.ToString("0"),Divine);}return;
    case "LesserRestoration":if(Friendly(p,t))LesserRestoration(t,dur);return;
    case "SacredFlame":if(Hostile(p,t)){t.TakeDamage(new DamageInfo(PaladinCore.Radiant,PaladinCore.HolyAmount(t,pw),.4f,-1f,p));if(t.Map!=null)FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,1.5f);}return;
    case "TurnUndead":if(p.Map!=null)foreach(var u in Races124.PawnsNear(p.Map,p.Position,rad).ToList())if(Hostile(p,u) && Items.Undead(u))TurnUndead(p,u,dur);Items.Mote(p,"驱散亡灵",Divine);return;
    case "MassHeal":if(p.Map!=null)foreach(var a in Races124.PawnsNear(p.Map,p.Position,rad).ToList()){if(!Friendly(p,a))continue;float h=ClassMelee.Heal(a,pw);if(h>0f)Items.Mote(a,"治疗 "+h.ToString("0"),Divine);}return;
    case "DeathWard":if(Friendly(p,t)){Items.Give(t,"LP_DeathWard",dur,pw);Items.Mote(t,"死亡守卫",Divine);}return;
    case "Raise":Raise(target.Thing as Corpse);return;
    // 德鲁伊
    case "AnimalFriendship":Befriend(p,t,s,Rand.Value);return;
    case "Entangle":if(cell.IsValid)Entangle(p,cell,rad,dur);return;
    case "ConjureAnimals":ConjureAnimals(p,s>=1.25f?3:2);Items.Mote(p,"召唤野兽",Nature);return;
    case "WildShape":Items.Give(p,"LP_WildShape",dur,s);Items.Mote(p,"荒野变形",Nature);return;
    case "PlantGrowth":if(cell.IsValid)PlantGrowth(p.Map,cell,rad,pw);return;
    case "BeastCommand":BeastCommand(p,rad,dur,s);Items.Mote(p,"兽王号令",Nature);return;
    case "CallLightning":if(cell.IsValid)CallLightning(p,cell,pw*(Stormy(p.Map)?1.5f:1f));return;
    case "ControlWeather":ControlWeather(p);return;
    // 术士
    case "ChaosBolt":if(!Hostile(p,t))return;Bolt(p,t,pw,Rand.Range(0,BoltKinds.Length));
     if(Rand.Chance(.125f)){var next=Races124.PawnsNear(t.Map,t.Position,6f).FirstOrDefault(x=>x!=t && Hostile(p,x));if(next!=null)Bolt(p,next,pw,Rand.Range(0,BoltKinds.Length));}return;
    case "BurningHands":if(cell.IsValid)BurningHands(p,cell,6f,pw);return;
    case "CharmPerson":Charm(p,t,dur,pw);return;
    case "Twin":Items.Give(p,"LP_Twinned",1800,1f);Items.Mote(p,"超魔·孪生",Chaos);return;
    case "DragonWings":if(cell.IsValid)DragonWings(p,cell);return;
    case "IceStorm":if(cell.IsValid)IceStorm(p,cell,rad,pw);return;
    case "MassSuggestion":if(cell.IsValid && p.Map!=null)foreach(var x in Races124.PawnsNear(p.Map,cell,rad).ToList())if(x.RaceProps.Humanlike && Hostile(p,x))Charm(p,x,dur,0f);return;
    case "Empower":Items.Give(p,"LP_Empowered",1800,2f);Items.Mote(p,"超魔·强效",Chaos);return;
    // 器械师
    case "QuickRepair":if(Friendly(p,t)){int n=Artificer.Repair(t);Items.Mote(t,"修好 "+n+" 件",Artisan);}return;
    case "RuneTurret":PlaceTurret(p,cell,s);return;
   }
  }

  // 施放之后：术士强效消耗、魔涌（冷却 ×0.25、狂野魔法）、召雷术雨天冷却减半、魔网之子 / 仪式余韵不进冷却。
  static readonly AccessTools.FieldRef<Ability,int> CooldownEnd=AccessTools.FieldRefAccess<Ability,int>("cooldownEndTick");
  static readonly AccessTools.FieldRef<Ability,int> CooldownDuration=AccessTools.FieldRefAccess<Ability,int>("cooldownDuration");
  public static void ScaleCooldown(Ability a,float f){
   int now=Find.TickManager.TicksGame,left=CooldownEnd(a)-now;if(left<=0 || f==1f)return;
   int n=Mathf.Max(1,Mathf.RoundToInt(left*f));CooldownEnd(a)=now+n;CooldownDuration(a)=n;
  }
  public static void AfterCast(Ability_Class a){
   var p=a.pawn;var e=a.Props;if(p==null || e==null)return;
   if(e.sorcery){
    if(e.effect!="Empower")ConsumeEmpower(p);
    if(Surging(p)){ScaleCooldown(a,.25f);if(Rand.Chance(.2f))WildSurge(p,Rand.Range(0,8));}
   }
   if(e.effect=="CallLightning" && Stormy(p.Map))ScaleCooldown(a,.5f);
   var i=HeroTraits.Info(p);
   if(i!=null){
    if(i.ritual && Feats2.Primed(p)){a.ResetCooldown();Items.Remove(p,"LP_RitualPrimed");Items.Mote(p,"仪式余韵：不进冷却",new Color(.6f,.55f,.85f));}
    else if(e.cost>0 && HeroDestiny.VpeSkipCooldown(p)){a.ResetCooldown();Items.Mote(p,"魔网之子：不进冷却",new Color(.5f,.7f,1f));}
   }
  }

  // ---------- 法师 ----------
  // 1.81.0 热修4 防御法术：镜影（严重度 = 分身数，每次被敌人攻击命中消耗一个、这一下落空）、护盾术（严重度 = 剩余吸收量）。挂在 WizardWards（Pawn.PreApplyDamage 前缀）。
  public const float MirrorImages=3f;
  static readonly DefRef<HediffDef> mirrorDef=new DefRef<HediffDef>("LP_MirrorImage"),shieldDef=new DefRef<HediffDef>("LP_ShieldSpell");
  // 返回 true = 整下伤害被挡掉。
  public static bool Ward(Pawn v,ref DamageInfo d){
   var set=v.health?.hediffSet;if(set==null)return false;
   var md=mirrorDef.Def;var mirror=md==null?null:set.GetFirstHediffOfDef(md);
   if(mirror!=null && d.Instigator!=null && d.Instigator!=v && d.Instigator.HostileTo(v) && (HeroCombat.Melee(d) || d.Weapon!=null || d.Def.isRanged || d.Def.isExplosive)){ // 只算攻击（近战、武器、投射物、爆炸），不算着火等持续伤害
    if(mirror.Severity<=1.01f)v.health.RemoveHediff(mirror);else mirror.Severity-=1f;
    Items.Mote(v,"镜影替身",Arcane);return true;
   }
   var sd=shieldDef.Def;var shield=sd==null?null:set.GetFirstHediffOfDef(sd);if(shield==null)return false;
   float take=Mathf.Min(shield.Severity,d.Amount);if(take<=0f)return false;
   if(shield.Severity-take<=.01f)v.health.RemoveHediff(shield);else shield.Severity-=take;
   if(take>=d.Amount-.001f){Items.Mote(v,"护盾吸收",Arcane);return true;}
   d.SetAmount(d.Amount-take);return false;
  }
  public static int MagicMissile(Pawn caster,Pawn t,float strength){
   var bullet=DefCache<ThingDef>.Get("LP_MagicMissileBullet");if(bullet==null || t==null || !t.Spawned || caster.Map==null)return 0;
   for(int i=0;i<3;i++){
    var proj=(Projectile)GenSpawn.Spawn(bullet,caster.Position,caster.Map);
    if(proj is MagicMissileBullet mm)mm.strength=strength;
    var origin=caster.DrawPos+new Vector3(Rand.Range(-.4f,.4f),0f,Rand.Range(-.4f,.4f));
    proj.Launch(caster,origin,t,t,ProjectileHitFlags.IntendedTarget);
   }
   return 3;
  }
  public static string SleepCannot(Pawn t){
   if(t==null || t.Dead)return "需要活着的目标。";
   if(t.RaceProps.IsMechanoid)return "睡眠术对机械体无效。";
   if(t.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness)<.3f)return "目标的意识已经太低。";
   return null;
  }
  public static DamageDef Lightning=>DefCache<DamageDef>.Get("LP_Lightning");
  public static int LightningBolt(Pawn caster,IntVec3 target,float power){
   var map=caster.Map;int hits=0;
   foreach(var c in GenSight.PointsOnLineOfSight(caster.Position,target).Concat(Gen.YieldSingle(target)).Distinct()){
    if(c==caster.Position || !c.InBounds(map))continue;
    FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),map,1f);
    foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList()){
     if(!Hostile(caster,t))continue;bool mech=t.RaceProps.IsMechanoid;
     t.TakeDamage(new DamageInfo(mech?DamageDefOf.EMP:(Lightning??DamageDefOf.Burn),power,.5f,-1f,caster));
     if(!mech)t.stances?.stunner?.StunFor(60,caster);
     hits++;
    }
   }
   return hits;
  }
  public static List<Thing> WallOfStone(Pawn caster,IntVec3 center,int hitPoints){
   var def=DefCache<ThingDef>.Get("LP_ConjuredWall");var made=new List<Thing>();if(def==null)return made;
   var map=caster.Map;var dir=center-caster.Position;bool alongZ=Math.Abs(dir.x)>=Math.Abs(dir.z);
   for(int k=-2;k<=2;k++){
    var c=alongZ?new IntVec3(center.x,0,center.z+k):new IntVec3(center.x+k,0,center.z);
    if(!c.InBounds(map) || c.Fogged(map) || c.GetEdifice(map)!=null || !c.Walkable(map))continue;
    if(c.GetThingList(map).Any(t=>t.def.category==ThingCategory.Pawn || t.def.category==ThingCategory.Item || t.def.category==ThingCategory.Building || t.def.category==ThingCategory.Plant && t.def.plant.IsTree))continue;
    var wall=ThingMaker.MakeThing(def,ThingDefOf.BlocksGranite);wall.SetFaction(caster.Faction);GenSpawn.Spawn(wall,c,map);
    wall.HitPoints=Mathf.Clamp(hitPoints,1,wall.MaxHitPoints);
    FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,2f,new Color(.6f,.55f,.5f));
    made.Add(wall);
   }
   return made;
  }
  public static void Disintegrate(Pawn caster,Thing thing,float power){
   if(thing==null || thing.Destroyed)return;var dmg=DefCache<DamageDef>.Get("LP_ArcaneForce")??DamageDefOf.Bomb;
   var line=DefCache<FleckDef>.Get("PsycastPsychicLine");if(line!=null && caster.Map!=null)FleckMaker.ConnectingLine(caster.DrawPos,thing.DrawPos,line,caster.Map);
   if(thing is Pawn t){
    var map=t.Map;var cell=t.Position;bool humanlike=t.RaceProps.Humanlike;
    t.TakeDamage(new DamageInfo(dmg,power*(t.RaceProps.IsMechanoid?2f:1f),.5f,-1f,caster));
    if(t.Dead && humanlike && t.Corpse!=null && !t.Corpse.Destroyed){t.Corpse.Destroy();if(map!=null)FilthMaker.TryMakeFilth(cell,map,ThingDefOf.Filth_Ash,3);}
   }else thing.TakeDamage(new DamageInfo(dmg,power*2f,.5f,-1f,caster));
  }

  // ---------- 牧师 ----------
  public static bool IsDisease(Hediff h)=>h.def.isBad && h.def.makesSickThought && !(h is Hediff_Injury) && !(h is Hediff_MissingPart) && !(h is Hediff_Addiction);
  public static void LesserRestoration(Pawn t,int ticks){
   var disease=t.health.hediffSet.hediffs.Where(IsDisease).OrderByDescending(h=>h.Severity).FirstOrDefault();
   if(disease!=null){t.health.RemoveHediff(disease);Items.Mote(t,"复原："+disease.LabelBase,Divine);}
   Items.Give(t,"LP_StaunchBleeding",ticks);
  }
  public static string TurnUndead(Pawn caster,Pawn t,int ticks){
   if(Items.IsLich(t)){Items.Give(t,"LP_Shaken",ticks);return "shaken";}
   if(t.RaceProps.Humanlike && t.mindState!=null && Necro.Construct(t)==null && t.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"被神圣之光驱散",true,true))return "flee";
   t.stances?.stunner?.StunFor(ticks,caster);return "stun";
  }
  public const int RaiseMaxAge=180000;
  public static string RaiseCannot(Corpse c){
   var p=c?.InnerPawn;if(p==null || !p.RaceProps.Humanlike)return "需要人形尸体。";
   if(Items.Undead(p) || NecromancyGenes.HasBody(p))return "复生术对巫妖和死灵造物无效。";
   if(c.Age>RaiseMaxAge)return "死亡超过 3 天，灵魂已经走远。";
   if(p.health.hediffSet.GetBrain()==null)return "大脑已经不在了，无法复生。";
   return null;
  }
  public static bool Raise(Corpse c){
   if(RaiseCannot(c)!=null)return false;var p=c.InnerPawn;
   if(!ResurrectionUtility.TryResurrect(p,new ResurrectionParams{removeDiedThoughts=true}))return false;
   Items.Give(p,"LP_RevivifyWeakness");
   Messages.Message(p.LabelShort+"在复生术的光芒中重新睁开了眼睛。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  public static bool WouldDown(Pawn p)=>p.health.InPainShock || !p.health.capacities.CanBeAwake || !p.health.capacities.CapableOf(PawnCapacityDefOf.Moving);
  static readonly DefRef<HediffDef> deathWard=new DefRef<HediffDef>("LP_DeathWard");
  public static bool TryDeathWard(Pawn p){
   if(p==null || p.Dead || p.Downed)return false;
   var ward=deathWard.Def;var h=ward==null?null:p.health.hediffSet.GetFirstHediffOfDef(ward);
   if(h==null || !WouldDown(p))return false;
   float amount=h.Severity;p.health.RemoveHediff(h);ClassMelee.Heal(p,amount);
   Items.Mote(p,"死亡守卫",Divine);
   if(p.Faction==Faction.OfPlayer)Messages.Message("死亡守卫护住了"+p.LabelShort+"。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }

  // ---------- 德鲁伊 ----------
  public static string FriendshipCannot(Pawn t){
   if(t==null || t.Dead)return "需要活着的动物。";
   if(!t.RaceProps.Animal)return "只能对动物使用。";
   if(t.Faction!=null && !t.InMentalState)return "这只动物已经有主人了。";
   return null;
  }
  public static float FriendshipChance(float strength,Pawn t)=>Mathf.Min(.9f,.35f*strength*(1f-t.GetStatValue(StatDefOf.Wildness)/2f));
  public static string Befriend(Pawn caster,Pawn t,float strength,float roll){
   if(FriendshipCannot(t)!=null)return "invalid";
   if(t.InMentalState && t.MentalStateDef!=null && t.MentalStateDef.IsAggro){t.MentalState.RecoverFromState();Items.Mote(t,"平静下来",Nature);return "calmed";}
   if(t.Faction!=null)return "owned";
   if(roll<FriendshipChance(strength,t)){InteractionWorker_RecruitAttempt.DoRecruit(caster,t);Items.Mote(t,"被驯服了",Nature);return "tamed";}
   t.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.PanicFlee,"德鲁伊的安抚",true);
   Items.Mote(t,"安抚失败",Nature);return "fled";
  }
  public static int Entangle(Pawn caster,IntVec3 c,float radius,int ticks){
   var map=caster.Map;if(map==null)return 0;int n=0;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(map))FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1f,new Color(.4f,.6f,.3f));
   foreach(var t in Races124.PawnsNear(map,c,radius).ToList()){if(t==caster || !t.HostileTo(caster))continue;Items.Give(t,"LP_Entangled",ticks,t.BodySize>=2f?2f:1f);n++;}
   return n;
  }
  public static List<Pawn> ConjureAnimals(Pawn caster,int count){
   var made=new List<Pawn>();var kind=DefCache<PawnKindDef>.Get("Wolf_Timber");var map=caster.Map;
   if(kind==null || map==null)return made;
   for(int i=0;i<count;i++){
    var w=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,caster.Faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:3,fixedChronologicalAge:3));
    GenSpawn.Spawn(w,CellFinder.RandomClosewalkCellNear(caster.Position,map,2),map);
    Items.Give(w,"LP_Conjured");
    if(w.training!=null && w.Faction==Faction.OfPlayer){
     foreach(var td in new[]{TrainableDefOf.Tameness,TrainableDefOf.Obedience,TrainableDefOf.Release})if(td!=null && w.training.CanAssignToTrain(td).Accepted)w.training.Train(td,null,true);
     if(w.playerSettings!=null)w.playerSettings.Master=caster;
    }
    FleckMaker.ThrowDustPuffThick(w.DrawPos,map,2f,Nature);
    HediffComp_Conjured.Engage(w);
    made.Add(w);
   }
   return made;
  }
  public static int PlantGrowth(Map map,IntVec3 c,float radius,float amount){
   if(map==null)return 0;int n=0;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true)){
    if(!cell.InBounds(map))continue;var plant=cell.GetPlant(map);if(plant==null)continue;
    plant.Growth=Mathf.Min(1f,plant.Growth+amount);plant.Blight?.Destroy();plant.DirtyMapMesh(map);
    if(n%4==0)FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1f,Nature);
    n++;
   }
   return n;
  }
  public static int BeastCommand(Pawn caster,float radius,int ticks,float strength){
   if(caster.Map==null)return 0;int n=0;
   foreach(var t in Races124.PawnsNear(caster.Map,caster.Position,radius).ToList()){if(!t.RaceProps.Animal || t.Faction==null || t.Faction!=caster.Faction)continue;Items.Give(t,"LP_BeastFrenzy",ticks,strength);n++;}
   return n;
  }
  public static bool Stormy(Map map)=>map?.weatherManager?.curWeather!=null && map.weatherManager.curWeather.rainRate>0f;
  public static int CallLightning(Pawn caster,IntVec3 c,float amount){
   var map=caster.Map;if(map==null)return 0;int n=0;
   FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),map,3f);FleckMaker.ThrowMicroSparks(c.ToVector3Shifted(),map);
   SoundDefOf.Thunder_OnMap?.PlayOneShot(new TargetInfo(c,map));
   foreach(var t in Races124.PawnsNear(map,c,1.9f).ToList()){
    if(t==caster || !t.HostileTo(caster))continue;bool mech=t.RaceProps.IsMechanoid;
    t.TakeDamage(new DamageInfo(mech?DamageDefOf.EMP:(Lightning??DamageDefOf.Burn),amount,.5f,-1f,caster));n++;
   }
   return n;
  }
  public static readonly string[] BadConditions={"ToxicFallout","ColdSnap","HeatWave","VolcanicWinter","Flashstorm","NoxiousHaze","Aurora_Bad"};
  public static List<WeatherDef> WeatherChoices(Map map){
   var l=new List<WeatherDef>();
   foreach(var n in new[]{"Clear","Rain","Fog","RainyThunderstorm"}){var w=DefDatabase<WeatherDef>.GetNamedSilentFail(n);if(w!=null)l.Add(w);}
   if(map!=null && map.mapTemperature.OutdoorTemp<=0f){var s=DefDatabase<WeatherDef>.GetNamedSilentFail("SnowGentle");if(s!=null)l.Add(s);}
   return l;
  }
  // 掌控天气：结束一个负面天气事件，再打开窗口选天气（1.73.0 起是窗口，不用浮动菜单）。
  public static void ControlWeather(Pawn p){
   var map=p.Map;if(map==null)return;
   var ended=EndBadCondition(map);
   if(ended!=null)Messages.Message(p.LabelShort+"驱散了"+ended+"。",p,MessageTypeDefOf.PositiveEvent,false);
   if(!Adventurer.TestSuppressed)Find.WindowStack.Add(new Dialog_ControlWeather(map));
  }
  public static void SetWeather(Map map,WeatherDef w){map.weatherManager.TransitionTo(w);Traverse.Create(map.weatherDecider).Field("curWeatherDuration").SetValue(60000);}
  public static string EndBadCondition(Map map){
   foreach(var mgr in new[]{map.gameConditionManager,Find.World?.gameConditionManager}){
    if(mgr==null)continue;
    var c=mgr.ActiveConditions.FirstOrDefault(x=>BadConditions.Contains(x.def.defName) && !x.Permanent && x.quest==null);
    if(c!=null){string label=c.LabelCap;c.End();return label;}
   }
   return null;
  }

  // ---------- 术士 ----------
  public static bool Surging(Pawn p)=>Items.Has(p,"LP_SorcerousSurge");
  public static void ConsumeEmpower(Pawn p){var def=Items.Hediff("LP_Empowered");var h=def==null?null:p.health.hediffSet.GetFirstHediffOfDef(def);if(h==null)return;h.Severity-=1f;if(h.Severity<.5f)p.health.RemoveHediff(h);}
  public static Pawn TwinTarget(Pawn caster,Pawn first)=>first?.Map==null?null:Races124.PawnsNear(first.Map,first.Position,6f).Where(x=>x!=first && Hostile(caster,x)).OrderBy(x=>x.Position.DistanceToSquared(first.Position)).FirstOrDefault();
  public static readonly string[] WildLabels={"治愈之光","隐形","灵能共鸣","繁花","自燃","乱序传送","熵涌","混乱精魂"};
  public static int WildSurge(Pawn p,int pick){
   var map=p.Map;
   switch(pick){
    case 0:ClassMelee.Heal(p,20f);break;
    case 1:Items.Give(p,"LP_WildInvisible",300,1f);break;
    case 2:if(map!=null)foreach(var a in Races124.PawnsNear(map,p.Position,6f).Where(x=>x.Faction==p.Faction))a.psychicEntropy?.OffsetPsyfocusDirectly(.05f);break;
    case 3:if(map!=null)foreach(var c in GenRadial.RadialCellsAround(p.Position,2.5f,true))if(c.InBounds(map))FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,1f,new Color(1f,.6f,.85f));break;
    case 4:p.TryAttachFire(.3f,p);break;
    case 5:if(map!=null && CellFinder.TryFindRandomCellNear(p.Position,map,5,c=>c.Standable(map) && c.GetFirstPawn(map)==null,out var cell))Races124.Blink(p,cell);break;
    case 6:p.psychicEntropy?.TryAddEntropy(20f,null,false,true);break;
    case 7:ChaosSpirit(p);break;
   }
   Items.Mote(p,"狂野魔法："+WildLabels[pick],Chaos);
   return pick;
  }
  static void ChaosSpirit(Pawn p){
   var map=p.Map;if(map==null)return;
   var kind=DefCache<PawnKindDef>.Get("Fox_Fennec")??DefCache<PawnKindDef>.Get("Rat");if(kind==null)return;
   var s=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true));
   GenSpawn.Spawn(s,CellFinder.RandomClosewalkCellNear(p.Position,map,3),map);
   var h=Items.Give(s,"LP_Conjured");var comp=(h as HediffWithComps)?.TryGetComp<HediffComp_Conjured>();if(comp!=null)comp.ticksLeft=1800;
   s.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Manhunter,"混乱精魂",true);
  }
  public static readonly string[] BoltKinds={"Flame","LP_Lightning","LP_ArcaneForce","LP_Frost"};
  public static string BoltKindFor(Pawn caster,int roll)=>caster?.genes?.Xenotype?.defName=="LP_ForceDragon" && DefCache<DamageDef>.Get("LP_Force")!=null?"LP_Force":BoltKinds[roll%BoltKinds.Length];
  public static string Bolt(Pawn caster,Pawn t,float amount,int roll){
   var kind=BoltKindFor(caster,roll);var dmg=DefCache<DamageDef>.Get(kind)??DamageDefOf.Burn;
   if(caster.Map!=null){var line=DefCache<FleckDef>.Get("PsycastPsychicLine");if(line!=null)FleckMaker.ConnectingLine(caster.DrawPos,t.DrawPos,line,caster.Map);}
   t.TakeDamage(new DamageInfo(dmg,amount,.3f,-1f,caster));
   if(!t.Dead){if(kind=="Flame")t.TryAttachFire(.3f,caster);else if(kind=="LP_Lightning")t.stances?.stunner?.StunFor(30,caster);else if(kind=="LP_Frost")Items.Give(t,"LP_FrostStaffChill",180);}
   return kind;
  }
  public static List<IntVec3> ConeCells(IntVec3 from,IntVec3 toward,float range){
   var dir=(toward-from).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(0,0,1);dir.Normalize();
   var l=new List<IntVec3>();
   foreach(var c in GenRadial.RadialCellsAround(from,range,false)){var v=(c-from).ToVector3();if(Vector3.Angle(dir,v)<=45f)l.Add(c);}
   return l;
  }
  public static int BurningHands(Pawn caster,IntVec3 toward,float range,float amount){
   var map=caster.Map;if(map==null)return 0;int n=0;
   foreach(var c in ConeCells(caster.Position,toward,range)){
    if(!c.InBounds(map))continue;FleckMaker.ThrowFireGlow(c.ToVector3Shifted(),map,.6f);
    foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList())if(t!=caster && t.HostileTo(caster)){t.TakeDamage(new DamageInfo(DamageDefOf.Flame,amount,.2f,-1f,caster));if(!t.Dead)t.TryAttachFire(.3f,caster);n++;}
    if(Rand.Chance(.3f))FireUtility.TryStartFireIn(c,map,.3f,caster);
   }
   return n;
  }
  public static string Charm(Pawn caster,Pawn t,int ticks,float resistanceCut){
   if(t==null || t.Dead || !t.RaceProps.Humanlike)return "invalid";
   if(DNDMind.TryResist(t)){Items.Mote(t,"抵抗了魅惑",Chaos);return "resisted";}
   if(t.IsPrisonerOfColony && t.guest!=null){t.guest.resistance=Mathf.Max(0f,t.guest.resistance-resistanceCut);Items.Mote(t,"抵抗 -"+resistanceCut.ToString("0"),Chaos);return "prisoner";}
   if(!t.HostileTo(caster))return "invalid";
   if(t.mindState?.mentalStateHandler!=null && t.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Wander_Psychotic,"被魅惑",true,true)){
    if(t.MentalState!=null)t.MentalState.forceRecoverAfterTicks=ticks;
    Items.Mote(t,"恍惚",Chaos);return "charmed";
   }
   return "failed";
  }
  public static bool DragonWings(Pawn p,IntVec3 cell){
   var map=p.Map;if(map==null || !cell.InBounds(map))return false;
   Items.Give(p,"LP_DragonWingsLanded",900,1f);
   var flyer=PawnFlyer.MakeFlyer(ThingDefOf.PawnFlyer,p,cell,null,null);
   if(flyer==null){Races124.Blink(p,cell);return true;}
   GenSpawn.Spawn(flyer,cell,map);
   return true;
  }
  public static int IceStorm(Pawn caster,IntVec3 c,float radius,float amount){
   var map=caster.Map;if(map==null)return 0;int n=0;var frost=DefCache<DamageDef>.Get("LP_Frost")??DamageDefOf.Frostbite;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true)){if(!cell.InBounds(map))continue;FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1f,new Color(.75f,.9f,1f));map.snowGrid.AddDepth(cell,.4f);}
   foreach(var t in Races124.PawnsNear(map,c,radius).ToList()){
    if(t==caster || !t.HostileTo(caster))continue;
    t.TakeDamage(new DamageInfo(frost,amount,.2f,-1f,caster));
    if(!t.Dead)t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,amount*.5f,.1f,-1f,caster));
    if(!t.Dead)Items.Give(t,"LP_FrostStaffChill",360);
    n++;
   }
   return n;
  }

  // ---------- 器械师 ----------
  public static string TurretCannot(Map map,IntVec3 c){
   if(map==null || !c.IsValid || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map))return "需要一个能站立的空格子。";
   if(c.GetEdifice(map)!=null || c.GetFirstPawn(map)!=null)return "这个格子被占着。";
   return null;
  }
  public static Building_RuneTurret PlaceTurret(Pawn caster,IntVec3 cell,float power){
   var def=DefCache<ThingDef>.Get("LP_RuneTurret");var map=caster?.Map;if(def==null || TurretCannot(map,cell)!=null)return null;
   var t=(Building_RuneTurret)ThingMaker.MakeThing(def);t.power=power;t.SetFaction(caster.Faction);
   GenSpawn.Spawn(t,cell,map);FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,2f,new Color(.6f,.8f,1f));
   return t;
  }
 }

 // 掌控天气的窗口（Peter 的游戏里浮动菜单选不了，改成按钮）。
 public class Dialog_ControlWeather : Window {
  readonly Map map;
  public override Vector2 InitialSize=>new Vector2(360f,330f);
  public Dialog_ControlWeather(Map m){map=m;doCloseX=true;closeOnClickedOutside=true;absorbInputAroundWindow=true;}
  public override void DoWindowContents(Rect r){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(r.x,r.y,r.width,32f),"选择天气（维持 1 天）");Text.Font=GameFont.Small;
   float y=r.y+40f;
   foreach(var w in ClassCasters.WeatherChoices(map)){if(Widgets.ButtonText(new Rect(r.x,y,r.width,34f),w.LabelCap)){ClassCasters.SetWeather(map,w);Close();}y+=40f;}
  }
 }

 // 镜影 / 护盾状态的类：Live = 地图上还在的个数（快速返回用；读档时补计，跨存档只会多不会少，多了也只是少一次快速返回）。
 public class Hediff_Ward : HediffWithComps {
  public static int Live;
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);Live++;}
  public override void PostRemoved(){base.PostRemoved();if(Live>0)Live--;}
  public override void ExposeData(){base.ExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit)Live++;}
 }
 // 1.81.0 热修4：镜影术、护盾术（Pawn.PreApplyDamage 前缀；没有人带这两个状态时第一步就返回）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class WizardWards {
  [HarmonyPriority(Priority.High)]
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(Hediff_Ward.Live<=0 || absorbed || dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return true;
   if(ClassCasters.Ward(__instance,ref dinfo)){absorbed=true;return false;}
   return true;
  }
 }
 // 魔法飞弹：1.7 的 Projectile 没有伤害倍率，命中后按 (奥术强度 − 1) 补一次同类伤害。
 public class MagicMissileBullet : Bullet {
  public float strength=1f;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref strength,"lpStrength",1f);}
  protected override void Impact(Thing hitThing,bool blockedByShield=false){
   base.Impact(hitThing,blockedByShield);
   int extra=Mathf.RoundToInt(def.projectile.GetDamageAmount(equipment,null)*(strength-1f));
   if(extra>0 && !blockedByShield && hitThing!=null && !hitThing.Destroyed)hitThing.TakeDamage(new DamageInfo(def.projectile.damageDef,extra,ArmorPenetration,ExactRotation.eulerAngles.y,launcher,null,equipmentDef,DamageInfo.SourceCategory.ThingOrUnknown,intendedTarget.Thing));
  }
 }
 // 熊形：近战伤害 × 严重度（野性强度），远程武器先收进背包，变回时装回。
 public class Hediff_WildShape : Hediff_ScaledBuff {
  public Thing stowed;
  public override void PostAdd(DamageInfo? dinfo){
   base.PostAdd(dinfo);
   var w=pawn.equipment?.Primary;
   if(w!=null && w.def.IsRangedWeapon && pawn.inventory!=null && pawn.equipment.TryTransferEquipmentToContainer(w,pawn.inventory.innerContainer))stowed=w;
   if(pawn.Spawned)FleckMaker.ThrowDustPuffThick(pawn.DrawPos,pawn.Map,3f,new Color(.55f,.4f,.25f));
  }
  public override void PostRemoved(){
   base.PostRemoved();
   if(stowed!=null && !stowed.Destroyed && pawn.inventory!=null && pawn.inventory.innerContainer.Contains(stowed) && pawn.equipment!=null && pawn.equipment.Primary==null && stowed is ThingWithComps eq){pawn.inventory.innerContainer.Remove(eq);pawn.equipment.AddEquipment(eq);}
   stowed=null;
   if(pawn.Spawned)FleckMaker.ThrowDustPuffThick(pawn.DrawPos,pawn.Map,3f,new Color(.55f,.4f,.25f));
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(pawn.Faction==Faction.OfPlayer)yield return new Command_Action{defaultLabel="变回原形",defaultDesc="结束荒野变形，恢复人形并装回收起的武器。",icon=ContentFinder<Texture2D>.Get("LP/Classes/Druid/WildShape",false)??BaseContent.BadTex,action=()=>pawn.health.RemoveHediff(this)};
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref stowed,"lpStowed");}
 }
 // 召唤物的寿命：时间到就消失（不留尸体）。
 public class HediffCompProperties_Conjured : HediffCompProperties {
  public int ticks=60000;
  public HediffCompProperties_Conjured(){compClass=typeof(HediffComp_Conjured);}
 }
 public class HediffComp_Conjured : HediffComp {
  public int ticksLeft=-1;
  public HediffCompProperties_Conjured Props=>(HediffCompProperties_Conjured)props;
  public override void CompPostMake(){base.CompPostMake();ticksLeft=Props.ticks;}
  public override void CompPostTickInterval(ref float severityAdjustment,int delta){
   base.CompPostTickInterval(ref severityAdjustment,delta);
   if(ticksLeft<0)ticksLeft=Props.ticks;
   ticksLeft-=delta;if(ticksLeft<=0){Vanish(Pawn);return;}
   scanIn-=delta;if(scanIn<=0){scanIn=30;Engage(Pawn);}
  }
  int scanIn;
  // 召唤兽是普通动物，原版 AI 不会主动找敌人：每半秒找最近的可达敌人扑上去（征召、精神状态、倒地时不管）。
  public const float EngageRadius=30f;
  public static bool Engage(Pawn p){
   if(p==null || !p.Spawned || p.Dead || p.Downed || p.InMentalState || p.Drafted || p.jobs==null || p.mindState==null)return false;
   var cur=p.CurJob;if(cur!=null && (cur.def==JobDefOf.AttackMelee || cur.def==JobDefOf.AttackStatic) && cur.targetA.Thing is Pawn ct && !ct.Dead && !ct.Downed)return false;
   var t=Target(p);if(t==null)return false;
   var job=JobMaker.MakeJob(JobDefOf.AttackMelee,t);job.expiryInterval=600;job.checkOverrideOnExpire=true;job.killIncappedTarget=false;
   p.mindState.enemyTarget=t;
   p.jobs.StartJob(job,JobCondition.InterruptForced,null,false,true);
   return true;
  }
  public static Pawn Target(Pawn p){
   Pawn best=null;float bd=EngageRadius*EngageRadius;
   foreach(var a in p.Map.attackTargetsCache.GetPotentialTargetsFor(p)){
    if(!(a.Thing is Pawn t) || t==p || t.Dead || t.Downed || !t.Spawned || t.IsPrisoner || !t.HostileTo(p) || a.ThreatDisabled(p))continue;
    float d=(t.Position-p.Position).LengthHorizontalSquared;if(d>=bd)continue;
    if(!p.CanReach(t,PathEndMode.Touch,Danger.Deadly))continue;
    best=t;bd=d;
   }
   return best;
  }
  public static void Vanish(Pawn p){if(p==null || p.Destroyed)return;if(p.Spawned)FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,2f,new Color(.6f,.9f,.6f));p.Destroy();}
  public override string CompLabelInBracketsExtra=>ticksLeft>0?ticksLeft.ToStringTicksToPeriod():null;
  public override void CompExposeData(){base.CompExposeData();Scribe_Values.Look(ref ticksLeft,"lpConjuredTicks",-1);}
 }
 // 急速术结束后的疲乏。
 public class HediffCompProperties_GiveOnRemoved : HediffCompProperties {
  public string hediff;public int ticks=2500;
  public HediffCompProperties_GiveOnRemoved(){compClass=typeof(HediffComp_GiveOnRemoved);}
 }
 public class HediffComp_GiveOnRemoved : HediffComp {
  public HediffCompProperties_GiveOnRemoved Props=>(HediffCompProperties_GiveOnRemoved)props;
  public override void CompPostPostRemoved(){base.CompPostPostRemoved();if(Pawn!=null && !Pawn.Dead)Items.Give(Pawn,Props.hediff,Props.ticks);}
 }
 // 符文炮台：伤害倍率 = 放置时的器械强度。
 public class Building_RuneTurret : Building_TurretGun, IArcanePowerSource {
  public float power=1f;
  public float ArcanePower=>power;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref power,"lpPower",1f);}
  public override string GetInspectString(){var s=base.GetInspectString();return (s.NullOrEmpty()?"":s+"\n")+"器械强度 ×"+power.ToString("0.##");}
 }

 // 器械师（1.29.0 起在 VPE 桥，1.73.0 移来）：DND 武器的制作门槛、魔力共鸣、大师工艺、维修、过载射击。
 [StaticConstructorOnStartup]
 public static class Artificer {
  public const string Workshop="LP_ArcaneWorkshop",Inscription="LP_RuneInscription",MasterCraft="LP_MasterCraft",MountainForge="LP_MountainForge",Resonance="LP_ArcaneResonance";
  public const float RepairFraction=.25f,OverchargeWear=.05f,ResonanceFloor=1.5f;
  static readonly DefRef<LP_ClassDef> cls=new DefRef<LP_ClassDef>("LP_Class_Artificer");
  public static LP_ClassDef Class=>cls.Def;
  // 门槛的器械师等级：奥术工坊 1 级，符文铭刻 5 级；山岳锻造要学会这个能力。
  public static readonly Dictionary<string,int> TierLevel=new Dictionary<string,int>{{Workshop,1},{Inscription,5}};
  public static readonly Dictionary<string,string> RequiredAbility=new Dictionary<string,string>{
   {"LP_MissileStaff",Workshop},{"LP_FireStaff",Workshop},{"LP_FrostStaff",Workshop},{"LP_RunicPistol",Workshop},{"LP_RunicMusket",Workshop},
   {"LP_SilverPistol",Inscription},{"LP_LavaPiercer",Inscription},{"LP_ThunderGun",Inscription},{"LP_HammerCannon",Inscription},{"LP_RunicRepeater",Inscription},
   {"LP_MountainWrath",MountainForge},
   {"LP_OgreClub",Workshop},{"LP_DwarvenThrowingHammer",Workshop},{"LP_ElvenLongbow",Workshop},{"LP_OrcishGreataxe",Workshop},{"LP_SilverSword",Workshop},{"LP_FrostGiantAxe",Workshop},
   {"LP_FrostSpear",Inscription},{"LP_EmpyreanBlade",Inscription},{"LP_CloudMorningstar",Inscription},{"LP_StormTrident",Inscription},{"LP_FireGiantMaul",Inscription},{"LP_MoltenMaul",Inscription},
   {"LP_CyclopsMaul",Workshop},
   {"LP_FlameTongue",Inscription},{"LP_FrostBrand",Inscription},{"LP_DragonSlayer",Inscription},{"LP_DefenderSword",Inscription},{"LP_JavelinLightning",Workshop},
   {"LP_SubduingCudgel",Workshop},{"LP_NetCrossbow",Workshop},{"LP_SleepBow",Workshop},{"LP_SleepStaff",Workshop},{"LP_SleepDustPouch",Workshop}};
  public const string NeedPrefix="制作需要：器械师";
  static Artificer(){
   Arcana.SensitivityFloorHooks.Add((p,g)=>Arcana.Magic(g) && Knows(p,Resonance)?ResonanceFloor:0f);
   Arcana.QualityBonusHooks.Add((p,d)=>Knows(p,MasterCraft)?1:0);
   AnnotateDescriptions();
  }
  public static string NeedText(string ability){var a=DefCache<AbilityDef>.Get(ability);string l=a?.label??ability;return TierLevel.TryGetValue(ability,out int lv)?"「"+l+"」（器械师 "+lv+" 级）":"「"+l+"」";}
  static void AnnotateDescriptions(){
   foreach(var kv in RequiredAbility){
    var thing=DefDatabase<ThingDef>.GetNamedSilentFail(kv.Key);if(thing==null)continue;
    var line=NeedPrefix+NeedText(kv.Value)+"。";
    thing.description=Append(thing.description,line);
    foreach(var r in DefDatabase<RecipeDef>.AllDefsListForReading)if(r.ProducedThingDef==thing)r.description=Append(r.description,line);
   }
  }
  static string Append(string text,string line)=>text.NullOrEmpty()?line:text.Contains(line)?text:text+"\n\n"+line;
  public static bool Knows(Pawn p,string ability)=>ClassSystem.Knows(p,ability);
  public static string Needed(RecipeDef r){var product=r?.ProducedThingDef;return product!=null && RequiredAbility.TryGetValue(product.defName,out var a)?a:null;}
  public static bool Meets(Pawn p,string need){if(need==null)return true;if(Knows(p,need))return true;return TierLevel.TryGetValue(need,out int lv) && ClassSystem.Level(p,Class)>=lv;}
  // 设置“DND武器需要器械师”关掉时谁都能做。
  public static bool MayCraft(Pawn p,RecipeDef r){if(LichMod.Settings!=null && !LichMod.Settings.dndWeaponsNeedArtificer)return true;return Meets(p,Needed(r));}
  public static int Repair(Pawn t){
   int n=0;var items=new List<Thing>();
   if(t?.equipment?.Primary!=null)items.Add(t.equipment.Primary);
   if(t?.apparel!=null)items.AddRange(t.apparel.WornApparel);
   foreach(var item in items){
    if(!item.def.useHitPoints || item.HitPoints>=item.MaxHitPoints)continue;
    item.HitPoints=Math.Min(item.MaxHitPoints,item.HitPoints+Math.Max(1,Mathf.RoundToInt(item.MaxHitPoints*RepairFraction)));n++;
   }
   return n;
  }
  static readonly DefRef<HediffDef> overcharge=new DefRef<HediffDef>("LP_Overcharge");
  public static float Overcharge(ref DamageInfo dinfo){
   var shooter=dinfo.Instigator as Pawn;if(shooter==null || dinfo.Weapon==null || !dinfo.Weapon.IsRangedWeapon || dinfo.Amount<=0f)return 1f;
   var def=overcharge.Def;var h=def==null?null:shooter.health?.hediffSet?.GetFirstHediffOfDef(def);if(h==null)return 1f;
   float f=1f+h.Severity;dinfo.SetAmount(dinfo.Amount*f);
   shooter.health.RemoveHediff(h);
   var gun=shooter.equipment?.Primary;
   if(gun!=null && gun.def.useHitPoints)gun.HitPoints=Math.Max(1,gun.HitPoints-Math.Max(1,Mathf.RoundToInt(gun.MaxHitPoints*OverchargeWear)));
   return f;
  }
 }

 // 冒险者手册（1.27.0 起在 VPE 桥，1.73.0 移来）：一次职业门槛豁免（下一个职业不看技能 / 兼修门槛）+ 训练经验。
 public class CompProperties_UseEffect_Manual : CompProperties_UseEffect {
  public CompProperties_UseEffect_Manual(){compClass=typeof(CompUseEffect_Manual);}
 }
 public class CompUseEffect_Manual : CompUseEffect {
  public static string Cannot(Pawn p){
   var h=Adventurer.Get(p);if(h==null)return "需要有冒险者等级的人。";
   if(h.classes==null)h.classes=new ClassData();
   if(h.classes.freeUnlocks>0)return "上一本手册的豁免还没用掉。";
   if(h.classes.levels.Count>=ClassSystem.MaxClasses)return "已经兼修了 "+ClassSystem.MaxClasses+" 个职业。";
   return null;
  }
  public override AcceptanceReport CanBeUsedBy(Pawn p){
   var b=base.CanBeUsedBy(p);if(!b.Accepted)return b;
   if(p.Dead || p.Downed)return "需要清醒。";
   var why=Cannot(p);return why==null?AcceptanceReport.WasAccepted:(AcceptanceReport)why;
  }
  public override void DoEffect(Pawn usedBy){base.DoEffect(usedBy);Apply(usedBy);}
  public static bool Apply(Pawn p){
   var why=Cannot(p);if(why!=null){Messages.Message(why,p,MessageTypeDefOf.RejectInput,false);return false;}
   Adventurer.Get(p).classes.freeUnlocks++;
   Adventurer.GainTrainingXP(p,Adventurer.ManualXP);
   Messages.Message(p.LabelShort+"读完冒险者手册：下一个冒险者职业不看技能门槛。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
 }

 // ==================== Harmony ====================
 [HarmonyPatch(typeof(RecipeDef),nameof(RecipeDef.PawnSatisfiesSkillRequirements))]
 public static class ArtificerRecipeGate {
  static void Postfix(RecipeDef __instance,Pawn pawn,ref bool __result){if(__result && pawn!=null && !Artificer.MayCraft(pawn,__instance))__result=false;}
 }
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class ArtificerOverchargeHit {
  static void Prefix(ref DamageInfo __0){if(__0.Instigator is Pawn)Artificer.Overcharge(ref __0);}
 }
 // 死亡守卫：本应因伤倒地时治疗并消耗守卫（倒地判定在 CheckForStateChange 里，所以在它之前检查）。
 [HarmonyPatch(typeof(Pawn_HealthTracker),"CheckForStateChange")]
 public static class ClassDeathWard {
  static void Prefix(Pawn ___pawn){if(!ClassCasters.TryDeathWard(___pawn))ClassLegendary.TryUnyielding(___pawn);} // 1.74.0 不屈战魂在死亡守卫之后
 }
 // 召唤物死了不留尸体。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class ConjuredNoCorpse {
  static void Postfix(Pawn __instance){
   if(!__instance.Dead || !Martial.Conjured(__instance))return;
   var c=__instance.Corpse;if(c!=null && !c.Destroyed){if(c.Spawned)FleckMaker.ThrowDustPuffThick(c.DrawPos,c.Map,2f,new Color(.6f,.9f,.6f));c.Destroy();}
  }
 }
}
