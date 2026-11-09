using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.100.0 职业水晶的效果（能力在 Defs/ManaLake_Arcana.xml）。主动能力走 ClassEffects.Apply 的末尾，被动是各处的挂钩：
 // 伤害（ArcanaDamage）、击杀（ArcanaKill）、倒地（ArcanaDowned）、陷阱（ArcanaTrap）、炮台（ArcanaTurret），以及每 250 刻的 Tick250。
 public static class ArcanaEffects {
  public static readonly Color Lake=new Color(.45f,.75f,1f),Holy=new Color(1f,.95f,.6f);
  public const int CamouflageTicks=240,EternalCombat=600;
  static readonly Dictionary<int,(IntVec3 cell,int since)> still=new Dictionary<int,(IntVec3,int)>();
  public static void Reset(){still.Clear();}
  static bool Hostile(Pawn caster,Pawn t)=>AnyTarget.Hostile(caster,t); // 1.136.0 热修：手动选的目标不分敌我
  static bool Friendly(Pawn caster,Pawn t)=>AnyTarget.Friendly(caster,t);
  static IEnumerable<Pawn> Near(Map map,IntVec3 c,float r)=>map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(map,c,r);
  public static bool Steadfast(Pawn p)=>Items.Has(p,"LP_ACB_Immovable") || Items.Has(p,"LP_ACP_CloudBody") || Items.Has(p,"LP_ACB_Unstoppable");
  public static float Health(Pawn p)=>p?.health?.summaryHealth?.SummaryHealthPercent??1f;

  // ---------- 选目标 ----------
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e.effect!=null && e.effect.Length>3 && e.effect[2]=='_'){if(e.effect.StartsWith("LO_"))return LakeLore.Invalid(caster,e,target);if(e.effect.StartsWith("LS_"))return LakeSeals.Invalid(caster,e,target);} // 1.133.0 通用秘传、湖印
   var t=target.Thing as Pawn;var map=caster.Map;
   switch(e.effect){
    case "ArCloudStep":case "ArForceField":
     if(map==null || !target.Cell.InBounds(map) || !target.Cell.Standable(map))return "需要一处能站立的空地。";
     if(target.Cell.GetFirstPawn(map)!=null && target.Cell!=caster.Position)return "那里有人。";
     if(e.effect=="ArForceField" && target.Cell.GetFirstBuilding(map)!=null)return "那里有建筑。";
     return null;
    case "ArSkyFall":if(map==null || !target.Cell.InBounds(map) || !target.Cell.Walkable(map))return "需要一处能落脚的地方。";return null;
    case "ArCloudHand":case "ArChain":case "ArPolymorph":case "ArGaleArrow":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";
     if(!t.HostileTo(caster))return "只能对敌人使用。";
     if(e.effect=="ArPolymorph"){if(ClassMartial2.Boss(t))return "传奇生物、剧情首领和体型 3 以上的目标不受影响。";if(t.RaceProps.IsMechanoid)return "机械体不受影响。";}
     return null;
    case "ArSimulacrum":
     if(t==null || t.Dead || !t.RaceProps.Humanlike)return "需要选择一名活着的人形。";
     if(t.Faction!=caster.Faction)return "只能复制己方的人。";
     if(Items.Has(t,"LP_ACB_Simulacrum"))return "不能复制拟像。";
     return null;
    case "ArTrueRes":return TrueResCannot(target.Thing as Corpse);
    case "ArTreeStride":{
     if(map==null)return "不在地图上。";
     if(!TreeNear(map,caster.Position,2))return "要站在树旁边（2 格内）。";
     if(!TreeNear(map,target.Cell,1))return "目的地要选一棵树。";
     return null;}
    case "ArSourceBurst":{var s=Sorcerer;var h=Adventurer.Get(caster);if(s==null || h==null || ClassSystem.Resource(h,s)<1f)return "术士法力不足（至少 1 点）。";return null;}
    case "ArReplicate":return ReplicateCannot(caster,target.Thing);
   }
   return null;
  }

  // ---------- 施放 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e.effect!=null && e.effect.Length>3 && e.effect[2]=='_'){if(e.effect.StartsWith("LO_"))return LakeLore.Apply(p,def,e,target);if(e.effect.StartsWith("LS_"))return LakeSeals.Apply(p,def,e,target);} // 1.133.0 通用秘传、湖印
   var t=target.Thing as Pawn;float s=ClassEffects.Strength(p,e);
   switch(e.effect){
    case "ArWarlord":Warlord(p,ClassEffects.Duration(p,e));return true;
    case "ArCloudStep":CloudStep(p,target.Cell,ClassEffects.Power(p,e),ClassEffects.Radius(p,e));return true;
    case "ArCloudHand":if(Hostile(p,t))CloudHand(p,t,ClassEffects.Power(p,e));return true;
    case "ArSkyFall":SkyFall(p,target.Cell,ClassEffects.Power(p,e),ClassEffects.Radius(p,e),ClassEffects.Duration(p,e));return true;
    case "ArChain":if(Hostile(p,t)){Items.Give(t,"LP_ACB_Chained",ClassEffects.Duration(p,e),ClassEffects.Power(p,e));Items.Mote(t,"天罚之链",Holy);}return true;
    case "ArSanctuary":ArcanaZones.Add(p,ArcanaZones.Sanctuary,target.Cell,ClassEffects.Radius(p,e),ClassEffects.Duration(p,e),ClassEffects.Power(p,e));Items.Mote(p,"圣域",Holy);return true;
    case "ArAngel":Angel(p,ClassEffects.Duration(p,e));return true;
    case "ArPrismWall":ArcanaZones.Add(p,ArcanaZones.Prism,target.Cell,0f,ClassEffects.Duration(p,e),ClassEffects.Power(p,e));Items.Mote(p,"棱光墙",Lake);return true;
    case "ArSimulacrum":if(t!=null)Simulacrum(p,t,ClassEffects.Duration(p,e));return true;
    case "ArPolymorph":if(Hostile(p,t))Polymorph(p,t);return true;
    case "ArDivineWord":DivineWord(p,ClassEffects.Radius(p,e),ClassEffects.Duration(p,e));return true;
    case "ArHolyAura":foreach(var a in Near(p.Map,p.Position,ClassEffects.Radius(p,e)).ToList())if(Friendly(p,a) && (a.RaceProps.Humanlike || a==p))Items.Give(a,"LP_ACB_HolyAura",ClassEffects.Duration(p,e));Items.Mote(p,"神圣光环",Holy);return true;
    case "ArTrueRes":TrueResurrect(p,target.Thing as Corpse);return true;
    case "ArTreeStride":TreeStride(p,target.Cell);return true;
    case "ArAnimalShapes":AnimalShapes(p,ClassEffects.Radius(p,e),ClassEffects.Duration(p,e),s);return true;
    case "ArSourceBurst":SourceBurst(p,target.Cell,ClassEffects.Power(p,e),ClassEffects.Radius(p,e));return true;
    case "ArReplicate":Replicate(p,target.Thing);return true;
    case "ArForceField":ForceField(p,target.Cell);return true;
    case "ArGaleArrow":if(Hostile(p,t))GaleArrow(p,t);return true;
    case "ArMassFear":MassFear(p,target.Cell,ClassEffects.Radius(p,e),ClassEffects.Duration(p,e),Rand.Value);return true;
    case "ArGuardian":Guardian(p,ClassEffects.Duration(p,e));return true;
   }
   return false;
  }

  // ---------- 战士 ----------
  public static int Warlord(Pawn p,int ticks){
   int n=0;if(p.Map==null)return 0;
   foreach(var a in p.Map.mapPawns.SpawnedPawnsInFaction(p.Faction).ToList())if(a.RaceProps.Humanlike && !a.Dead){Items.Give(a,"LP_ACB_Warlord",ticks);n++;}
   Items.Mote(p,"万军之主",ClassEffects.MartialColor);return n;
  }
  // ---------- 武僧 ----------
  public static int Quake(Pawn p,IntVec3 c,float power,float radius,int stun,Map map){
   int n=0;if(map==null)return 0;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(map))FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1.2f,new Color(.85f,.9f,1f));
   foreach(var t in Near(map,c,radius).ToList()){if(!Hostile(p,t))continue;t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,power,0f,-1f,p));if(!t.Dead)t.stances?.stunner?.StunFor(stun,p);n++;}
   return n;
  }
  public static int CloudStep(Pawn p,IntVec3 c,float power,float radius){
   var map=p.Map;if(map==null)return 0;
   FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,Color.white);Races124.Blink(p,c);
   Items.Mote(p,"攀云步",MonkCore.Ki);
   return Quake(p,c,power,radius,60,map);
  }
  public static void CloudHand(Pawn p,Pawn t,float power){
   if(p.Map!=null){var line=DefCache<FleckDef>.Get("PsycastPsychicLine");if(line!=null)FleckMaker.ConnectingLine(p.DrawPos,t.DrawPos,line,p.Map);}
   t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,power,.5f,-1f,p));
   if(!t.Dead && t.Spawned){var dest=DNDPush.Destination(t.Map,t.Position,(t.Position-p.Position).ToVector3(),3);DNDPush.Move(t,dest);}
   Items.Mote(t,"云手",MonkCore.Ki);
  }
  public static bool SkyFall(Pawn p,IntVec3 c,float power,float radius,int stun){
   var map=p.Map;if(map==null)return false;var origin=p.Position;
   var def=DefDatabase<ThingDef>.GetNamedSilentFail("LP_SkyFallFlyer");
   Items.Mote(p,"九霄坠击",MonkCore.Ki);
   var flyer=def==null?null:PawnFlyer.MakeFlyer(def,p,c,null,null) as LP_SkyFallFlyer;
   if(flyer==null){if(!p.Spawned)GenSpawn.Spawn(p,origin,map);Races124.Blink(p,c);Quake(p,c,power,radius,stun,map);return true;}
   flyer.power=power;flyer.radius=radius;flyer.stun=stun;
   GenSpawn.Spawn(flyer,origin,map);
   return true;
  }
  // ---------- 圣武士 ----------
  public static Pawn Angel(Pawn p,int ticks){
   var kind=DefCache<PawnKindDef>.Get("LP_AC_AngelKind");var xeno=DefCache<XenotypeDef>.Get(PactRaces.Deva);
   var a=Summon(p,kind,xeno,ticks,true);
   if(a!=null){Items.Give(a,"LP_ACB_Angelic");Items.Mote(a,"天使降临",Holy);}
   return a;
  }
  // ---------- 召唤（天使、拟像、异界护卫、灵魂契约都用 LP_Conjured 计时） ----------
  public static Pawn Summon(Pawn caster,PawnKindDef kind,XenotypeDef xeno,int ticks,bool adult){
   var map=caster?.Map;if(kind==null || map==null)return null;
   var req=new PawnGenerationRequest(kind,caster.Faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,colonistRelationChanceFactor:0f,forcedXenotype:xeno,developmentalStages:DevelopmentalStage.Adult);
   var w=PawnGenerator.GeneratePawn(req);
   GenSpawn.Spawn(w,CellFinder.RandomClosewalkCellNear(caster.Position,map,2),map);
   Bind(w,caster,ticks);
   FleckMaker.ThrowDustPuffThick(w.DrawPos,map,2f,Lake);
   return w;
  }
  public static void Bind(Pawn w,Pawn master,int ticks){
   if(w.RaceProps.Animal && w.training!=null){
    foreach(var td in new[]{TrainableDefOf.Tameness,TrainableDefOf.Obedience,TrainableDefOf.Release})if(td!=null && w.training.CanAssignToTrain(td).Accepted)w.training.Train(td,null,true);
    if(w.playerSettings!=null)w.playerSettings.Master=master;
   }
   var h=Items.Give(w,"LP_Conjured") as HediffWithComps;var c=h?.TryGetComp<HediffComp_Conjured>();if(c!=null)c.ticksLeft=ticks;
   HediffComp_Conjured.Engage(w);
  }
  // ---------- 法师 ----------
  public static Pawn Simulacrum(Pawn p,Pawn src,int ticks){
   var map=p.Map;if(map==null)return null;
   var req=new PawnGenerationRequest(PawnKindDefOf.Colonist,p.Faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,colonistRelationChanceFactor:0f,fixedGender:src.gender,forcedXenotype:src.genes?.Xenotype,developmentalStages:DevelopmentalStage.Adult);
   var c=PawnGenerator.GeneratePawn(req);
   if(c.skills!=null && src.skills!=null)foreach(var sk in src.skills.skills){var r=c.skills.GetSkill(sk.def);if(r!=null && !r.TotallyDisabled)r.Level=sk.Level;}
   c.Name=new NameSingle(src.LabelShort+"的拟像");
   GenSpawn.Spawn(c,CellFinder.RandomClosewalkCellNear(src.Position,map,2),map);
   Items.Give(c,"LP_ACB_Simulacrum");Bind(c,p,ticks);
   FleckMaker.ThrowDustPuffThick(c.DrawPos,map,2f,new Color(.75f,.9f,1f));Items.Mote(c,"拟像术",Lake);
   return c;
  }
  public static Pawn Polymorph(Pawn p,Pawn t){
   var map=t.Map;if(map==null)return null;var cell=t.Position;
   var kind=DefCache<PawnKindDef>.Get(t.BodySize>=1.5f?"Bear_Grizzly":"Wolf_Timber");if(kind==null)return null;
   var w=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,p.Faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:4,fixedChronologicalAge:4));
   string was=t.LabelShort;
   t.Destroy();
   GenSpawn.Spawn(w,cell,map);
   if(w.training!=null){foreach(var td in new[]{TrainableDefOf.Tameness,TrainableDefOf.Obedience,TrainableDefOf.Release})if(td!=null && w.training.CanAssignToTrain(td).Accepted)w.training.Train(td,null,true);if(w.playerSettings!=null)w.playerSettings.Master=p;}
   FleckMaker.ThrowDustPuffThick(w.DrawPos,map,2.5f,new Color(.8f,.6f,1f));Items.Mote(w,"真实变形术",Lake);
   if(p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(was+"被真实变形术变成了一只"+w.def.label+"。",w,MessageTypeDefOf.PositiveEvent,false);
   return w;
  }
  // ---------- 牧师 ----------
  public static int DivineWord(Pawn p,float radius,int stun){
   int n=0;
   foreach(var t in Near(p.Map,p.Position,radius).ToList()){
    if(!Hostile(p,t))continue;n++;
    switch(WordOutcome(Health(t),ClassMartial2.Boss(t))){
     case 2:t.Kill(new DamageInfo(DefCache<DamageDef>.Get("LP_Radiant")??DamageDefOf.Burn,999f,99f,-1f,p));break;
     case 1:t.stances?.stunner?.StunFor(stun,p);break;
     default:Items.Give(t,"LP_ACB_Blinded",3600);break;
    }
   }
   Items.Mote(p,"神之圣言",Holy);return n;
  }
  // 0 失明、1 击晕、2 死亡（首领只会失明）。
  public static int WordOutcome(float hp,bool boss)=>boss?0:hp<.2f?2:hp<.5f?1:0;
  public const int TrueResMaxAge=3600000;
  public static string TrueResCannot(Corpse c){
   if(c==null || c.InnerPawn==null)return "需要选择一具尸体。";var p=c.InnerPawn;
   if(!p.RaceProps.Humanlike)return "只能复活人形。";
   if(Items.Undead(p) || Necro.Construct(p)!=null)return "巫妖和死灵造物不受影响。";
   if(c.Age>TrueResMaxAge)return "死亡已经超过 1 年。";
   return null;
  }
  public static bool TrueResurrect(Pawn caster,Corpse c){
   if(TrueResCannot(c)!=null)return false;var p=c.InnerPawn;
   if(!ResurrectionUtility.TryResurrect(p,new ResurrectionParams{removeDiedThoughts=true,restoreMissingParts=true}))return false;
   foreach(var mp in p.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().ToList())p.health.RestorePart(mp.Part);
   Items.Mote(p,"真实复活",Holy);
   if(p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"在真实复活的圣光中归来，失去的肢体也一并长回。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  // ---------- 德鲁伊 ----------
  public static bool IsTree(Thing t)=>t is Plant pl && pl.def.plant!=null && pl.def.plant.IsTree;
  public static bool TreeNear(Map map,IntVec3 c,float r){foreach(var cell in GenRadial.RadialCellsAround(c,r,true)){if(!cell.InBounds(map))continue;foreach(var t in cell.GetThingList(map))if(IsTree(t))return true;}return false;}
  public static int TreeStride(Pawn p,IntVec3 dest){
   var map=p.Map;if(map==null)return 0;
   var party=new List<Pawn>{p};foreach(var a in Near(map,p.Position,2f).ToList())if(party.Count<4 && a!=p && Friendly(p,a) && !a.Downed && a.Faction==p.Faction)party.Add(a);
   int n=0;
   foreach(var a in party){
    var cell=CellFinder.RandomClosewalkCellNear(dest,map,2,c=>c.Standable(map) && c.GetFirstPawn(map)==null);
    FleckMaker.ThrowDustPuffThick(a.DrawPos,map,1.5f,ClassCasters.Nature);Races124.Blink(a,cell);FleckMaker.ThrowDustPuffThick(a.DrawPos,map,1.5f,ClassCasters.Nature);n++;
   }
   Items.Mote(p,"横越植物",ClassCasters.Nature);return n;
  }
  public static int AnimalShapes(Pawn p,float radius,int ticks,float s){
   int n=0;var list=new List<Pawn>{p};foreach(var a in Near(p.Map,p.Position,radius).ToList())if(list.Count<5 && a!=p && a.RaceProps.Humanlike && a.Faction==p.Faction && !a.Downed && !a.Dead)list.Add(a);
   foreach(var a in list){Items.Give(a,"LP_WildShape",ticks,s);Items.Mote(a,"兽形术",ClassCasters.Nature);n++;}
   return n;
  }
  // ---------- 术士 ----------
  static LP_ClassDef Sorcerer=>DefCache<LP_ClassDef>.Get("LP_Class_Sorcerer");
  public static int SourceBurst(Pawn p,IntVec3 c,float power,float radius){
   var h=Adventurer.Get(p);var s=Sorcerer;if(h==null || s==null)return 0;
   float mana=Mathf.Floor(ClassSystem.Resource(h,s));if(mana<1f)return 0;ClassSystem.AddResource(h,s,-mana);
   var map=p.Map;int n=0;if(map==null)return 0;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(map))FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(),map,1.2f);
   var dmg=DefCache<DamageDef>.Get("LP_ArcaneForce")??DamageDefOf.Bomb;
   foreach(var t in Near(map,c,radius).ToList()){if(!Hostile(p,t))continue;t.TakeDamage(new DamageInfo(dmg,power*mana,.4f,-1f,p));n++;}
   Items.Mote(p,"源能爆发 ×"+mana.ToString("0"),ClassCasters.Chaos);return n;
  }
  // ---------- 器械师 ----------
  public static bool Replicable(Thing t)=>t!=null && t.def.defName.StartsWith("LP_") && t.def.stackLimit==1 && (t.def.IsApparel || t.def.IsWeapon || MagicSlots.Of(t.def)!=null) && t.def.tradeability!=Tradeability.None && !(t.TryGetQuality(out var q) && q==QualityCategory.Legendary);
  public static int ReplicateCost(Thing t)=>Mathf.CeilToInt(t.MarketValue*.5f);
  public static int SilverOn(Map map)=>map==null?0:map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(x=>!x.IsForbidden(Faction.OfPlayer)).Sum(x=>x.stackCount);
  public static string ReplicateCannot(Pawn caster,Thing t){
   if(t==null || t is Pawn || t is Corpse)return "需要选择一件物品。";
   if(!Replicable(t))return "只能复刻 DND 服装、武器或奇物（传说品质、不能交易的除外）。";
   int cost=ReplicateCost(t);if(SilverOn(caster.Map)<cost)return "地图上的白银不够（需要 "+cost+"）。";
   return null;
  }
  public static Thing Replicate(Pawn p,Thing src){
   var map=p.Map;if(ReplicateCannot(p,src)!=null)return null;
   int cost=ReplicateCost(src);
   foreach(var s in map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(x=>!x.IsForbidden(Faction.OfPlayer)).ToList()){if(cost<=0)break;int take=Math.Min(cost,s.stackCount);s.SplitOff(take).Destroy();cost-=take;}
   var copy=ThingMaker.MakeThing(src.def,src.Stuff);
   if(src.TryGetQuality(out var q))copy.TryGetComp<CompQuality>()?.SetQuality(q,ArtGenerationContext.Colony);
   GenPlace.TryPlaceThing(copy,src.PositionHeld,map,ThingPlaceMode.Near);
   Items.Mote(p,"完美复刻",new Color(.6f,.8f,1f));
   return copy;
  }
  public static Thing ForceField(Pawn p,IntVec3 c){
   var def=DefCache<ThingDef>.Get("LP_ArcaneForceField");if(def==null || p.Map==null)return null;
   var t=ThingMaker.MakeThing(def);t.SetFaction(p.Faction);GenSpawn.Spawn(t,c,p.Map);
   FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),p.Map,2f,Lake);Items.Mote(p,"力场发生器",Lake);return t;
  }
  // ---------- 游侠 ----------
  public static int GaleArrow(Pawn p,Pawn t){
   int n=0;var targets=new List<Pawn>{t};
   foreach(var o in Near(t.Map,t.Position,8f).Where(x=>Hostile(p,x) && x!=t).OrderBy(x=>x.Position.DistanceToSquared(t.Position)).Take(2))targets.Add(o);
   foreach(var x in targets)if(Martial.Fire(p,x,new ShotMod{caster=p},true)!=null)n++;
   Items.Mote(p,"疾风箭",ClassMartial2.HuntColor);return n;
  }
  // ---------- 邪术师 ----------
  public static int MassFear(Pawn p,IntVec3 c,float radius,int ticks,float roll){
   int n=0;
   foreach(var t in Near(p.Map,c,radius).ToList()){if(!Hostile(p,t))continue;bool boss=ClassMartial2.Boss(t);if(boss && roll<ClassLegendary.BossStunChance){Items.Mote(t,"挣脱",Color.gray);continue;}if(ClassSubclass.Fear(t,ticks,"群体恐惧",true))n++;}
   Items.Mote(p,"群体恐惧",new Color(.7f,.4f,.9f));return n;
  }
  public static Pawn Guardian(Pawn p,int ticks){
   string kind=ClassSubclass.Has(p,Warlock.Archfey)?"LP_PixieFamiliar":ClassSubclass.Has(p,Warlock.Fiend)?"LP_ImpFamiliar":"LP_QuasitFamiliar";
   var w=Summon(p,DefCache<PawnKindDef>.Get(kind)??DefCache<PawnKindDef>.Get("LP_ImpFamiliar"),null,ticks,true);
   if(w!=null)Items.Mote(w,"异界护卫",new Color(.7f,.4f,.9f));
   return w;
  }

  // ---------- 施放其他职业能力之后 ----------
  public static void AfterClassCast(Ability_Class a,LocalTargetInfo target){
   var p=a.pawn;var e=a.Props;if(p==null || e==null)return;
   if(e.effect=="Smite" && LakeArcana.Has(p,"RadiantShield")){
    var leg=DefCache<AbilityDef>.Get("LP_AC_RadiantShield");var props=leg?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
    float amount=props==null?30f:ClassEffects.Power(p,props);Items.Give(p,"LP_ACB_Shield",3600,amount);Items.Mote(p,"光耀护盾",Holy);
   }
   if((e.effect=="Heal" || e.effect=="MassHeal") && LakeArcana.Has(p,"HolyWordHeal")){
    var list=new List<Pawn>();
    if(e.effect=="Heal"){if(target.Thing is Pawn t)list.Add(t);}
    else foreach(var x in Near(p.Map,p.Position,ClassEffects.Radius(p,e)).ToList())if(Friendly(p,x))list.Add(x);
    foreach(var x in list)CureOne(x);
   }
  }
  public static bool CureOne(Pawn t){
   var bad=t?.health?.hediffSet?.hediffs.FirstOrDefault(h=>ClassCasters.IsDisease(h) || h is Hediff_Addiction);
   if(bad==null)return false;t.health.RemoveHediff(bad);Items.Mote(t,"圣言："+bad.LabelCap,Holy);return true;
  }

  // ---------- 被动（每 250 刻） ----------
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;var d=h.classes;
   // 再生之环
   if(LakeArcana.Has(p,"RegenCircle")){
    bool shaped=Items.Has(p,"LP_WildShape");
    if(shaped){var leg=DefCache<AbilityDef>.Get("LP_AC_RegenCircle");var props=leg?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();ClassMelee.Heal(p,props==null?2f:ClassEffects.Power(p,props));}
    else if(d.arcanaShaped){ClassMelee.Heal(p,ClassMelee.InjurySum(p)*.2f);Items.Mote(p,"再生之环",ClassCasters.Nature);}
    d.arcanaShaped=shaped;
   }
   // 不可阻挡 / 永恒狂怒
   bool raging=ClassMartial2.Raging(p);
   bool stop=raging && LakeArcana.Has(p,"Unstoppable");if(stop && !Items.Has(p,"LP_ACB_Unstoppable"))Items.Give(p,"LP_ACB_Unstoppable");else if(!stop)Items.Remove(p,"LP_ACB_Unstoppable");
   if(raging && LakeArcana.Has(p,"EternalRage") && InCombat(p,h)){var r=ClassMartial2.Rage(p);var timer=r?.TryGetComp<HediffComp_Disappears>();if(timer!=null && timer.ticksToDisappear<EternalCombat)timer.ticksToDisappear=EternalCombat;}
   // 兽伴进化
   if(LakeArcana.Has(p,"BeastEvolve")){var c=ClassSubclass.Companion(p);if(c!=null && !c.Dead && !Items.Has(c,"LP_ACB_EvolvedBeast"))Items.Give(c,"LP_ACB_EvolvedBeast");}
   // 自然伪装：征召、没在走、4 秒没动
   if(LakeArcana.Has(p,"Camouflage"))Camouflage(p);
  }
  public static bool InCombat(Pawn p,Hediff_Adventurer h){int now=Find.TickManager.TicksGame;var m=p.mindState;return h?.classes!=null && now-h.classes.rageTick<EternalCombat || m!=null && (now-m.lastHarmTick<EternalCombat || now-p.LastAttackTargetTick<EternalCombat);}
  public static void Camouflage(Pawn p){
   int now=Find.TickManager.TicksGame;
   if(!p.Spawned || !p.Drafted || p.pather?.Moving==true){still.Remove(p.thingIDNumber);if(p.pather?.Moving==true)Items.Remove(p,"LP_ACB_Camouflage");return;}
   if(!still.TryGetValue(p.thingIDNumber,out var s) || s.cell!=p.Position){still[p.thingIDNumber]=(p.Position,now);return;}
   if(now-s.since>=CamouflageTicks && !Items.Has(p,"LP_ACB_Camouflage")){Items.Give(p,"LP_ACB_Camouflage");Items.Mote(p,"伪装",ClassMartial2.HuntColor);}
  }
  public static readonly HashSet<string> Elemental=new HashSet<string>{"Flame","Burn","Frostbite","LP_Frost","LP_Lightning","LP_Shock","LP_AlchemyAcid","LP_OozeAcid","AcidBurn","ElectricalBurn"};
 }

 // 剑圣之域：每 30 刻对 3 格内最近的一名敌人近战结算一次（伤害 × 战技强度，用旋风斩的增伤状态）。
 public class Hediff_SwordDomain : HediffWithComps {
  int acc;
  public const float Range=3f;public const int Interval=30;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if(pawn==null || !pawn.Spawned || pawn.Downed || pawn.Dead)return;
   if((acc+=delta)<Interval)return;acc=0;
   var t=Races124.PawnsNear(pawn.Map,pawn.Position,Range).Where(x=>x!=pawn && !x.Dead && !x.Downed && x.HostileTo(pawn)).OrderBy(x=>x.Position.DistanceToSquared(pawn.Position)).FirstOrDefault();
   if(t==null)return;
   Whirlwind.Sweep(pawn,Severity,new List<Pawn>{t});
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref acc,"lpSwordAcc");}
 }
 // 护盾：严重度就是还能吸收的点数（ArcanaDamage 里扣）。
 public class Hediff_ArcanaShield : HediffWithComps {
  public override bool ShouldRemove=>base.ShouldRemove || Severity<.5f;
  public override string LabelInBrackets=>Severity.ToString("0");
 }
 public class LP_SkyFallFlyer : PawnFlyer {
  public float power,radius=4f;public int stun=300;
  protected override void RespawnPawn(){var p=FlyingPawn;var map=Map;var cell=DestinationPos.ToIntVec3();base.RespawnPawn();if(p!=null)ArcanaEffects.Quake(p,p.Spawned?p.Position:cell,power,radius,stun,p.MapHeld??map);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref power,"lpSkyPower");Scribe_Values.Look(ref radius,"lpSkyRadius",4f);Scribe_Values.Look(ref stun,"lpSkyStun",300);}
 }

 // ---------- 地面区域：圣域、棱光墙 ----------
 public class ArcanaZone : IExposable {
  public int kind;public IntVec3 center;public float radius,power;public int ticksLeft;public Pawn caster;public Faction faction;public List<IntVec3> cells=new List<IntVec3>();
  public void ExposeData(){Scribe_Values.Look(ref kind,"kind");Scribe_Values.Look(ref center,"center");Scribe_Values.Look(ref radius,"radius");Scribe_Values.Look(ref power,"power");Scribe_Values.Look(ref ticksLeft,"ticksLeft");Scribe_References.Look(ref caster,"caster",true);Scribe_References.Look(ref faction,"faction");Scribe_Collections.Look(ref cells,"cells",LookMode.Value);if(Scribe.mode==LoadSaveMode.PostLoadInit && cells==null)cells=new List<IntVec3>();}
 }
 public class ArcanaZones : MapComponent {
  public const int Sanctuary=1,Prism=2,Blade=3,Alarm=4,Interval=60,WallLength=10,BladeLength=8; // 1.133.0 剑刃屏障（8 格刃墙）、警报术（湖印）
  public List<ArcanaZone> zones=new List<ArcanaZone>();int acc;
  public ArcanaZones(Map map):base(map){}
  public static ArcanaZone Add(Pawn caster,int kind,IntVec3 c,float radius,int ticks,float power){
   var comp=caster?.Map?.GetComponent<ArcanaZones>();if(comp==null)return null;
   var z=new ArcanaZone{kind=kind,center=c,radius=radius,power=power,ticksLeft=ticks,caster=caster,faction=caster.Faction};
   if(kind==Prism || kind==Blade){var dir=(c-caster.Position).ToVector3();dir.y=0;if(dir.sqrMagnitude<.01f)dir=new Vector3(0,0,1);dir.Normalize();var perp=new Vector3(-dir.z,0,dir.x);int len=kind==Blade?BladeLength:WallLength;
    for(int i=-len/2;i<len/2;i++){var cell=(c.ToVector3Shifted()+perp*i).ToIntVec3();if(cell.InBounds(caster.Map) && !z.cells.Contains(cell))z.cells.Add(cell);}}
   comp.zones.Add(z);return z;
  }
  bool Hostile(ArcanaZone z,Pawn t)=>t!=null && !t.Dead && (z.faction==null?t.HostileTo(Faction.OfPlayer):t.HostileTo(z.faction));
  public override void MapComponentTick(){
   if(zones.Count==0 || ++acc<Interval)return;acc=0;
   for(int i=zones.Count-1;i>=0;i--){var z=zones[i];z.ticksLeft-=Interval;if(z.ticksLeft<=0){zones.RemoveAt(i);continue;}Pulse(z);}
  }
  public void Pulse(ArcanaZone z){
   var holy=DefCache<DamageDef>.Get("LP_Radiant")??DamageDefOf.Burn;
   if(z.kind==Sanctuary){
    foreach(var c in GenRadial.RadialCellsAround(z.center,z.radius,true))if(c.InBounds(map) && Rand.Chance(.15f))FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),map,.6f);
    foreach(var t in Races124.PawnsNear(map,z.center,z.radius).ToList()){
     if(Hostile(z,t)){if(PactRaces.Fiendish(t))t.TakeDamage(new DamageInfo(holy,z.power,0f,-1f,z.caster));}
     else ClassMelee.Heal(t,z.power*.4f);
    }
   }else if(z.kind==Prism){
    var kinds=new[]{DamageDefOf.Flame,DefCache<DamageDef>.Get("LP_Frost")??DamageDefOf.Frostbite,DefCache<DamageDef>.Get("LP_Lightning")??DamageDefOf.Burn,DefCache<DamageDef>.Get("LP_AlchemyAcid")??DamageDefOf.AcidBurn};
    foreach(var c in z.cells){
     if(!c.InBounds(map))continue;if(Rand.Chance(.3f))FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),map,.7f);
     foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList())if(Hostile(z,t))foreach(var k in kinds){if(t.Dead)break;t.TakeDamage(new DamageInfo(k,z.power,0f,-1f,z.caster));}
    }
   }else if(z.kind==Blade){ // 1.133.0 剑刃屏障：墙上的敌人每秒割伤 + 减速
    foreach(var c in z.cells){
     if(!c.InBounds(map))continue;if(Rand.Chance(.25f))FleckMaker.ThrowMicroSparks(c.ToVector3Shifted(),map);
     foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList())if(Hostile(z,t)){t.TakeDamage(new DamageInfo(DamageDefOf.Cut,z.power,.2f,-1f,z.caster));if(!t.Dead)Items.Give(t,"LP_LO_Slowed",180);}
    }
   }else if(z.kind==Alarm){ // 1.133.0 警报术：敌人进入就发信、叫醒殖民者，触发一次就消失
    var foe=Races124.PawnsNear(map,z.center,z.radius).FirstOrDefault(t=>Hostile(z,t) && !t.Downed);
    if(foe!=null){z.ticksLeft=0;LakeSeals.AlarmTriggered(map,foe,z.center);}
   }
  }
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref zones,"lpArcanaZones",LookMode.Deep);if(Scribe.mode==LoadSaveMode.PostLoadInit && zones==null)zones=new List<ArcanaZone>();}
 }

 // ---------- 挂钩 ----------
 // 斩铁式、万军之主、血怒、伪装、猎神之眼、天罚之链（攻击者）；护盾、元素化身、神圣光环（受击者）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class ArcanaDamage {
  static bool busy;
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(!LakeArcana.Touched || busy || dinfo.Def==null || dinfo.Amount<=0f)return true;
   var v=__instance;var a=dinfo.Instigator as Pawn;
   if(Items.Has(v,"LP_ACB_ElementalForm") && ArcanaEffects.Elemental.Contains(dinfo.Def.defName)){absorbed=true;return false;}
   if(a!=null && a!=v && dinfo.Def.harmsHealth){
    float f=1f;
    if(Items.Has(a,"LP_ACB_SteelCut"))dinfo.SetIgnoreArmor(true);
    if(Items.Has(a,"LP_ACB_Warlord"))f*=1.15f;
    if(ClassMartial2.Raging(a) && LakeArcana.Has(a,"BloodFury"))f*=1f+Mathf.Min(.5f,.5f*(1f-ArcanaEffects.Health(a)));
    if(Items.Has(a,"LP_ACB_Camouflage")){f*=2f;Items.Remove(a,"LP_ACB_Camouflage");}
    if(Items.Has(a,"LP_ACB_HuntGodEye") && ClassMartial2.Favored(a,v))f*=1.5f;
    if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
    var chain=Items.Has(a,"LP_ACB_Chained")?a.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ACB_Chained")):null;
    if(chain!=null){busy=true;try{a.TakeDamage(new DamageInfo(DefCache<DamageDef>.Get("LP_Radiant")??DamageDefOf.Burn,chain.Severity,0f,-1f,v));}finally{busy=false;}}
    if(Items.Has(v,"LP_ACB_HolyAura") && !dinfo.Def.isRanged && PactRaces.Fiendish(a))Items.Give(a,"LP_ACB_Blinded",600);
   }
   var shield=Items.Has(v,"LP_ACB_Shield")?v.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ACB_Shield")):null;
   if(shield!=null && dinfo.Def.harmsHealth){
    float take=Mathf.Min(shield.Severity,dinfo.Amount);shield.Severity-=take;
    if(take>=dinfo.Amount-.01f){absorbed=true;Items.Mote(v,"护盾",ArcanaEffects.Holy);return false;}
    dinfo.SetAmount(dinfo.Amount-take);
   }
   return true;
  }
 }
 // 无影、灵魂收割、灵魂契约。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class ArcanaKill {
  public static float? TestRoll;
  static void Postfix(Pawn __instance,DamageInfo? dinfo){
   if(!LakeArcana.Touched || dinfo==null)return;var k=dinfo.Value.Instigator as Pawn;var v=__instance;if(k==null || k==v || k.Dead)return;
   if(LakeArcana.Has(k,"Shadowless")){var inv=DefCache<HediffDef>.Get("PsychicInvisibility");if(inv!=null){var h=HediffMaker.MakeHediff(inv,k);var dis=h.TryGetComp<HediffComp_Disappears>();if(dis!=null)dis.ticksToDisappear=180;k.health.AddHediff(h);Items.Mote(k,"无影",ClassMartial2.ShadowColor);}}
   if(dinfo.Value.Def?.defName=="LP_Eldritch" && LakeArcana.Has(k,"SoulHarvest")){var c=DefCache<LP_ClassDef>.Get("LP_Class_Warlock");var h=Adventurer.Get(k);if(c!=null && h!=null){ClassSystem.AddResource(h,c,1f);Items.Mote(k,"灵魂收割",new Color(.7f,.4f,.9f));}}
   if(v.RaceProps.Humanlike && v.Faction!=null && k.Faction!=null && v.Faction.HostileTo(k.Faction) && !ClassMartial2.Boss(v) && !Items.Undead(v) && LakeArcana.Has(k,"SoulPact") && (TestRoll??Rand.Value)<.5f)SoulPact(k,v);
  }
  public static bool SoulPact(Pawn k,Pawn v){
   var corpse=v.Corpse;if(corpse==null || !corpse.Spawned)return false;
   if(!ResurrectionUtility.TryResurrect(v,new ResurrectionParams{removeDiedThoughts=true}))return false;
   if(v.Faction!=k.Faction)v.SetFaction(k.Faction);
   v.guest?.SetGuestStatus(null);
   ArcanaEffects.Bind(v,k,180000);Items.Mote(v,"灵魂契约",new Color(.7f,.4f,.9f));
   return true;
  }
 }
 // 不动如山、永恒狂怒：不会倒地（伤势过重照样死）。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.ShouldBeDowned))]
 public static class ArcanaDowned {
  static void Postfix(Pawn_HealthTracker __instance,Pawn ___pawn,ref bool __result){
   if(!__result || !LakeArcana.Touched || ___pawn==null || ___pawn.Dead)return;
   if(Items.Has(___pawn,"LP_ACB_Immovable") || ClassMartial2.Raging(___pawn) && LakeArcana.Has(___pawn,"EternalRage"))__result=false;
  }
 }
 // 陷阱大师：不触发陷阱。
 [HarmonyPatch(typeof(Building_Trap),"CheckSpring")]
 public static class ArcanaTrap {static bool Prefix(Pawn __0)=>!(LakeArcana.Touched && LakeArcana.Has(__0,"TrapMaster"));}
 // 灵魂之核：符文炮台、战争傀儡伤害 ×1.5。
 [HarmonyPatch(typeof(ClassCasters),nameof(ClassCasters.PlaceTurret))]
 public static class ArcanaTurret {static void Prefix(Pawn caster,ref float power){if(LakeArcana.Touched && LakeArcana.Has(caster,"SoulCore"))power*=1.5f;}}
 [HarmonyPatch(typeof(ClassLegendary),nameof(ClassLegendary.WarGolem))]
 public static class ArcanaGolem {static void Prefix(Pawn p,ref float power){if(LakeArcana.Touched && LakeArcana.Has(p,"SoulCore"))power*=1.5f;}}
}
