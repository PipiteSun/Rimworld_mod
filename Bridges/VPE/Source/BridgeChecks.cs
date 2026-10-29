using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.27.0 职业路径自检：只在同时启用 VPE 时由 LichChecks 通过反射调用（-lichtest / -lichclassesonly）。
 public static partial class BridgeChecks {
  static int checks;static Map map;static IntVec3 center;static readonly List<Pawn> made=new List<Pawn>();
  static string savedPawnId,savedWallId;static float savedXp,savedGiven;
  static void Check(bool ok,string message){if(!ok)throw new Exception("[vpe bridge] "+message);checks++;Log.Message("[LICH-TEST] OK [vpe bridge] "+message);}
  static bool Near(float a,float b,float tol=.02f)=>Math.Abs(a-b)<=Math.Max(tol,Math.Abs(b)*.01f);
  static AbilityDef Def(string name)=>DefDatabase<AbilityDef>.GetNamed(name);
  static LP_ClassPathDef Fighter=>Classes.Path(Classes.FighterPath);
  static LP_ClassPathDef Wizard=>Classes.Path(Classes.WizardPath);
  static LP_ClassPathDef Cleric=>Classes.Path(Classes.ClericPath);
  static LP_ClassPathDef ArtificerPath=>Artificer.Path;

  // 1.53.0：-VPE -Only <组名>（逗号分隔）：只跑 <组名>Checks()，再跑 <组名>PrepareSave()；读档后 <组名>AfterLoad()。
  public static int RunOnly(string groups){
   checks=0;map=Find.CurrentMap;made.Clear();Arena();
   var names=Groups(groups);
   try{
    foreach(var g in names)Invoke(g+"Checks",true);
    foreach(var g in names)Invoke(g+"PrepareSave",false);
   }finally{foreach(var p in made)if(!p.Destroyed && !keep.Contains(p.ThingID))p.Destroy();made.Clear();}
   return checks;
  }
  public static int AfterLoadOnly(string groups){checks=0;map=Find.CurrentMap;foreach(var g in Groups(groups))Invoke(g+"AfterLoad",false);return checks;}
  static readonly HashSet<string> keep=new HashSet<string>(); // -Only 组要存档的人
  static List<string> Groups(string s)=>(s??"").Split(',').Select(x=>x.Trim()).Where(x=>x.Length>0).ToList();
  static void Invoke(string name,bool required){
   var m=typeof(BridgeChecks).GetMethod(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public);
   if(m==null || m.GetParameters().Length>0){if(required)throw new Exception("unknown VPE bridge check group for -Only: "+name);return;}
   try{m.Invoke(null,null);}
   catch(System.Reflection.TargetInvocationException e){if(e.InnerException==null)throw;System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();}
  }
  static void Arena(){
   var inner=map.AllCells.Where(c=>c.x>15 && c.z>15 && c.x<map.Size.x-15 && c.z<map.Size.z-15).OrderBy(c=>c.DistanceToSquared(map.Center)).ToList();
   center=inner.FirstOrDefault(c=>CellRect.CenteredOn(c,8).Cells.All(x=>x.Standable(map) && !x.Fogged(map) && !x.Roofed(map) && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth)));
   if(center==IntVec3.Zero)center=inner.First(c=>CellRect.CenteredOn(c,8).Cells.All(x=>x.Walkable(map) && !x.Fogged(map) && x.GetEdifice(map)==null && x.GetFirstPawn(map)==null));
   foreach(var c in CellRect.CenteredOn(center,8).Cells)foreach(var t in c.GetThingList(map).Where(t=>t is Plant || t.def.category==ThingCategory.Item).ToList())t.Destroy();
  }
  public static int Run(){
   checks=0;map=Find.CurrentMap;made.Clear();
   var inner=map.AllCells.Where(c=>c.x>15 && c.z>15 && c.x<map.Size.x-15 && c.z<map.Size.z-15).OrderBy(c=>c.DistanceToSquared(map.Center)).ToList();
   // 先找完全空旷、无屋顶的 17×17 区域；随机地图上找不到时放宽为：没有建筑和人物、不在迷雾里（植物和物品会被清掉）。
   center=inner.FirstOrDefault(c=>CellRect.CenteredOn(c,8).Cells.All(x=>x.Standable(map) && !x.Fogged(map) && !x.Roofed(map) && x.GetThingList(map).All(t=>t is Plant || t.def.category==ThingCategory.Filth)));
   if(center==IntVec3.Zero)center=inner.First(c=>CellRect.CenteredOn(c,8).Cells.All(x=>x.Walkable(map) && !x.Fogged(map) && x.GetEdifice(map)==null && x.GetFirstPawn(map)==null));
   foreach(var c in CellRect.CenteredOn(center,8).Cells)foreach(var t in c.GetThingList(map).Where(t=>t is Plant || t.def.category==ThingCategory.Item).ToList())t.Destroy();
   try{
    Definitions();Unlocking();Stats();FighterChecks();WizardChecks();ClericChecks();PsychicChecks();ArtificerChecks();ManualChecks();Experience();MarilithChecks();checks+=MentorChecks.Run();Classes2Checks();NetherilChecks();AdventurerChecks();AntiMagicChecks();PrepareSave();Classes2PrepareSave();
   }finally{foreach(var p in made)if(!p.Destroyed && p.ThingID!=savedPawnId)p.Destroy();made.Clear();}
   return checks;
  }
  public static int AfterLoad(){
   checks=0;map=Find.CurrentMap;
   Classes2AfterLoad(); // 1.36.0
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==savedPawnId);
   Check(p!=null,"class pawn survives save/load");
   var comp=p.GetComp<CompAbilities>();
   Check(comp.HasAbility(Def("LP_SecondWind")) && comp.HasAbility(Def("LP_GuardStance")),"learned class abilities survive save/load");
   Check(Classes.HasPath(p,Fighter) && Near(Classes.Psycasts(p).experience,savedXp),"unlocked class path and VPE experience survive save/load");
   Check(Items.Has(p,"LP_GuardStance") && Items.TicksLeft(p,"LP_GuardStance")>0,"guard stance hediff survives save/load");
   Check(Near(Classes.State.GivenToday(p),savedGiven),"daily class experience counter survives save/load");
   var wall=map.listerThings.AllThings.FirstOrDefault(t=>t.ThingID==savedWallId);
   Check(wall!=null && wall.def.defName=="LP_ConjuredWall","conjured wall survives save/load");
   wall.Destroy();p.Destroy();
   checks+=MentorChecks.AfterLoad(); // 1.31.0 云游导师
   return checks;
  }

  static Pawn Make(int melee=0,int intellectual=0,int social=0,bool psylink=true,Faction faction=null,IntVec3? at=null){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,faction??Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25,fixedChronologicalAge:25,forcedXenotype:XenotypeDefOf.Baseliner));
   made.Add(p);
   foreach(var s in DefDatabase<SkillDef>.AllDefsListForReading){var r=p.skills.GetSkill(s);r.Level=0;r.passion=Passion.None;r.xpSinceLastLevel=0;}
   p.skills.GetSkill(SkillDefOf.Melee).Level=melee;p.skills.GetSkill(SkillDefOf.Intellectual).Level=intellectual;p.skills.GetSkill(SkillDefOf.Social).Level=social;
   foreach(var h in p.health.hediffSet.hediffs.ToList())if(h.def.isBad)p.health.RemoveHediff(h);
   p.apparel?.DestroyAll();
   // 1.30.1：法师、牧师吃心灵敏感度，去掉随机的心灵敏感特性，让夹具的敏感度固定（100%）。
   foreach(var tr in p.story.traits.allTraits.Where(t=>t.def.defName=="PsychicSensitivity").ToList())p.story.traits.RemoveTrait(tr);
   GenSpawn.Spawn(p,at??center,map);
   if(psylink){p.ChangePsylinkLevel(1,false);p.psychicEntropy.OffsetPsyfocusDirectly(1f);}
   return p;
  }
  // 1.52.1 眼魔反魔法锥压住 VPE 施法（需要 HAR 的眼魔；没有 HAR 时跳过）。
  static void AntiMagicChecks(){
   if(!Monsters.HarAvailable)return;
   var caster=Make(0,10,0);var ab=Learn(caster,"LP_SecondWind");var ah=Adventurer.Get(caster)??Adventurer.TryAdd(caster,13,true);if(ah!=null)ah.level=13;bool baseOk=ab.IsEnabledForPawn(out string baseWhy);
   var b=Monsters.Make(Monsters.Beholder,null,map.Tile);made.Add(b);GenSpawn.Spawn(b,center+IntVec3.East*6,map);b.Rotation=Rot4.West;
   var mc=map.GetComponent<MapComponent_Legendary>();mc.Refresh();
   bool blocked=!ab.IsEnabledForPawn(out string why);bool cone=Monsters.InAntiMagic(caster);
   b.Rotation=Rot4.East;bool free=!Monsters.InAntiMagic(caster) && (!baseOk || ab.IsEnabledForPawn(out _));
   b.DeSpawn();mc.Refresh();
   var patched=HarmonyLib.Harmony.GetPatchInfo(HarmonyLib.AccessTools.Method(typeof(Ability),nameof(Ability.IsEnabledForPawn)))?.Postfixes.Any(x=>x.PatchMethod.DeclaringType==typeof(AntiMagicVPEEnabled))==true;
   Check(cone && free && patched && (!baseOk || blocked && why!=null && why.Contains("反魔法")),"beholder anti-magic cone disables a VPE ability in front of it, not behind it (ability enabled before: "+baseOk+(baseOk?"":" — "+baseWhy)+")");
  }
  static Faction Enemy=>Find.FactionManager.AllFactionsListForReading.First(f=>f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && !f.def.hidden);
  static Pawn MakeEnemy(IntVec3 at)=>Make(0,0,0,false,Enemy,at);
  static Ability Learn(Pawn p,string ability){var comp=p.GetComp<CompAbilities>();var def=Def(ability);if(!comp.HasAbility(def))comp.GiveAbility(def);return comp.LearnedAbilities.First(a=>a.def==def);}
  static T Learn<T>(Pawn p,string ability) where T:Ability=>(T)Learn(p,ability);
  static GlobalTargetInfo[] T(Thing t)=>new[]{new GlobalTargetInfo(t)};
  static GlobalTargetInfo[] T(IntVec3 c)=>new[]{new GlobalTargetInfo(c,map)};
  // 受伤：死亡、伤势增加，或者有部位被打掉（被打掉的部位不留伤势）。
  static bool Hurt(Pawn p,float injuryBefore)=>p.Dead || ClassAbility.InjurySum(p)>injuryBefore || p.health.hediffSet.GetMissingPartsCommonAncestors().Count>0;
  static void Wound(Pawn p,float amount,int count=1){var parts=p.RaceProps.body.AllParts.Where(x=>x.depth==BodyPartDepth.Outside && x.coverageAbs>0 && x.def.defName!="Head" && x.def.defName!="Neck" && x.def.defName!="Torso" && x.def.GetMaxHealth(p)>=amount*2f).ToList();for(int i=0;i<count;i++){var part=parts[i%parts.Count];var inj=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,p,part);inj.Severity=amount;p.health.AddHediff(inj,part);}}

  // 1.33.0：六臂蛇人（需要 HAR）能走职业路径；旋风斩的命中不连锁副手追加攻击。
  static void MarilithChecks(){
   if(!Marilith.Loaded)return;
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_MarilithColonist"),Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:25,fixedChronologicalAge:25,forceNoBackstory:true));made.Add(p);
   foreach(var tr in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(tr);
   foreach(var s in new[]{SkillDefOf.Melee,SkillDefOf.Intellectual,SkillDefOf.Social})p.skills.GetSkill(s).Level=6;
   GenSpawn.Spawn(p,center+IntVec3.North*3,map);p.ChangePsylinkLevel(1,false);
   Check(Marilith.Is(p) && Classes.Psycasts(p)!=null && Fighter.CanPawnUnlock(p) && Wizard.CanPawnUnlock(p) && Cleric.CanPawnUnlock(p),"a six-armed marilith can take the fighter, wizard and cleric paths (psycasts="+(Classes.Psycasts(p)!=null)+" f="+Fighter.CanPawnUnlock(p)+" w="+Wizard.CanPawnUnlock(p)+" c="+Cleric.CanPawnUnlock(p)+" disabled="+string.Join(",",p.skills.skills.Where(s=>s.TotallyDisabled).Select(s=>s.def.defName))+")");
   foreach(var d in new[]{"MeleeWeapon_Knife","MeleeWeapon_Gladius"})p.inventory.innerContainer.TryAdd(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(d),ThingDefOf.Steel));
   var foe=MakeEnemy(p.Position+IntVec3.East);int s0=MarilithOffhand.Strikes;int hits=Ability_Whirlwind.Sweep(p,1f,new List<Pawn>{foe});
   Check(Marilith.OffhandSlots(p)==2 && hits==1 && MarilithOffhand.Strikes==s0,"whirlwind hits do not chain the marilith off-hand strikes");
  }
  static void Definitions(){
   var paths=Classes.Paths.ToList();
   Check(paths.Count==8 && Fighter!=null && Wizard!=null && Cleric!=null && ArtificerPath!=null && new[]{Classes.DruidPath,Classes.PaladinPath,Classes.SorcererPath,Classes.MonkPath}.All(d=>Classes.Path(d)!=null),"eight class paths exist as LP_ClassPathDef (1.36.0 druid/paladin/sorcerer/monk)");
   Check(Fighter.requiredSkill==SkillDefOf.Melee && Wizard.requiredSkill==SkillDefOf.Intellectual && Cleric.requiredSkill==SkillDefOf.Social && ArtificerPath.requiredSkill==SkillDefOf.Crafting && paths.All(p=>p.requiredSkillLevel==6),"paths key on melee / intellectual / social / crafting at level 6");
   Check(paths.All(p=>p.tab=="DND职业" && !p.ignoreLockRestrictionsForNeurotrainers),"paths sit in the DND职业 tab and neurotrainers respect the lock");
   var need=new Dictionary<int,int>{{1,6},{2,9},{3,12},{4,15},{5,18}};int n=0;
   foreach(var path in paths){
    int want=path==Fighter?16:9; // 1.53.0 战士射击支 + 冲锋、缴械击
    Check(path.abilityLevelsInOrder!=null && path.abilityLevelsInOrder.Length==5 && path.abilities.Count==want,path.defName+" has "+want+" abilities over 5 levels");
    foreach(var ab in path.abilities){
     var e=ab.Psycast() as LP_ClassAbilityExt;
     Check(e!=null && e.path==path && e.requiredSkillLevel==need[e.level],ab.defName+" requires skill "+need[e.level]+" at level "+e.level);
     Check(ab.abilityClass!=null && typeof(ClassAbility).IsAssignableFrom(ab.abilityClass),ab.defName+" uses a class ability worker");
     Check(!ab.iconPath.NullOrEmpty() && ContentFinder<Texture2D>.Get(ab.iconPath,false)!=null,ab.defName+" has an icon");
     n++;
    }
   }
   Check(n==79,"79 class abilities in total (1.53.0)");
   foreach(var s in new[]{"LP_MartialPower","LP_ArcanePower","LP_DivinePower","LP_ArtificePower","LP_PrimalPower","LP_VerdantPower","LP_InnatePower","LP_MarksmanPower"})Check(Classes.Stat(s)!=null && Classes.Stat(s).parts.Any(x=>x is StatPart_ClassSkill),s+" stat exists with the skill part");
   foreach(var d in new[]{"LP_ArcaneForce","LP_Lightning","LP_Radiant"})Check(DefDatabase<DamageDef>.GetNamedSilentFail(d)!=null,d+" damage exists");
   Check(DefDatabase<ThingDef>.GetNamedSilentFail("LP_AdventurersManual")?.GetCompProperties<CompProperties_UseEffect_Manual>()!=null,"adventurer's manual exists with its use effect");
   Check(DefDatabase<RecipeDef>.GetNamedSilentFail("LP_MakeAdventurersManual")?.recipeUsers.Any(u=>u.defName=="LP_ScribeDesk")==true,"manual recipe sits on the scribe desk");
   Check(DefDatabase<ThingDef>.GetNamedSilentFail("LP_ConjuredWall")!=null && DefDatabase<ThingDef>.GetNamedSilentFail("LP_MagicMissileBullet")!=null,"conjured wall and magic missile projectile exist");
   Check(DefDatabase<ThingDef>.AllDefsListForReading.Count(t=>t.defName.StartsWith(ThingDefGenerator_Neurotrainer.PsytrainerDefPrefix+"_LP_"))==79,"VPE generated a neurotrainer for every class ability");
  }
  static void Unlocking(){
   var p=Make(5,0,0);var h=Classes.Psycasts(p);
   Check(h!=null,"psylink gives the VPE ability hediff");
   Check(!Fighter.CanPawnUnlock(p),"fighter path locked at melee 5");
   p.skills.GetSkill(SkillDefOf.Melee).Level=6;
   Check(Fighter.CanPawnUnlock(p) && !Wizard.CanPawnUnlock(p) && !Cleric.CanPawnUnlock(p),"fighter path unlockable at melee 6, others still locked");
   h.UnlockPath(Fighter);
   Check(Classes.HasPath(p,Fighter),"path unlock recorded");
   var comp=p.GetComp<CompAbilities>();
   Check(Def("LP_SecondWind").Psycast().PrereqsCompleted(comp) && Def("LP_BattleCry").Psycast().PrereqsCompleted(comp),"level 1 abilities learnable at melee 6");
   Check(!Def("LP_ActionSurge").Psycast().PrereqsCompleted(comp),"level 2 ability blocked by missing prerequisite");
   comp.GiveAbility(Def("LP_SecondWind"));
   p.skills.GetSkill(SkillDefOf.Melee).Level=8;
   Check(!Def("LP_ActionSurge").Psycast().PrereqsCompleted(comp) && !Def("LP_ActionSurge").Psycast().PrereqsCompleted(p),"level 2 ability blocked at melee 8 even with prerequisite learned");
   p.skills.GetSkill(SkillDefOf.Melee).Level=9;
   Check(Def("LP_ActionSurge").Psycast().PrereqsCompleted(comp) && Def("LP_ActionSurge").Psycast().PrereqsCompleted(p),"level 2 ability learnable at melee 9");
   // 神经成型器照样卡技能。
   var trainer=DefDatabase<ThingDef>.GetNamed(ThingDefGenerator_Neurotrainer.PsytrainerDefPrefix+"_LP_WeaponMastery");
   var item=ThingMaker.MakeThing(trainer);var use=item.TryGetComp<CompPsytrainer>();
   Check(!use.CanBeUsedBy(p).Accepted,"weapon mastery neurotrainer refused at melee 9");
   p.skills.GetSkill(SkillDefOf.Melee).Level=12;
   Check(use.CanBeUsedBy(p).Accepted,"weapon mastery neurotrainer accepted at melee 12");
   item.Destroy();
   Check(CompUseEffect_Manual.Choose(p,out _)==Classes.Path(Classes.PaladinPath),"manual offers the paladin path next to a fighter at melee 12 (1.36.0 paladin/monk share melee)");
  }
  static void Stats(){
   var p=Make(6,10,20);
   Check(Near(p.GetStatValue(Classes.Stat("LP_MartialPower")),.8f) && Near(p.GetStatValue(Classes.Stat("LP_ArcanePower")),1f) && Near(p.GetStatValue(Classes.Stat("LP_DivinePower")),1.5f),"class power stats read 0.8 / 1.0 / 1.5 at skills 6 / 10 / 20");
   Check(Near(Classes.Power(null,SkillDefOf.Melee),.5f),"pawns without skills count as power 0.5");
  }
  static void FighterChecks(){
   var weak=Make(6);var strong=Make(20);
   var w1=Learn<Ability_SecondWind>(weak,"LP_SecondWind");var w2=Learn<Ability_SecondWind>(strong,"LP_SecondWind");
   Check(Near(w1.Power,16f) && Near(w2.Power,30f),"second wind power scales 16 -> 30 with melee 6 -> 20");
   Wound(weak,10,3);Wound(strong,10,3);
   float before1=ClassAbility.InjurySum(weak),before2=ClassAbility.InjurySum(strong);
   w1.Apply(T(weak));w2.Apply(T(strong));
   var regen1=(weak.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_SecondWind")) as HediffWithComps).TryGetComp<HediffComp_ClassRegen>();
   var regen2=(strong.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_SecondWind")) as HediffWithComps).TryGetComp<HediffComp_ClassRegen>();
   for(int i=0;i<10;i++){regen1.Pulse();regen2.Pulse();}
   Check(Near(before1-ClassAbility.InjurySum(weak),16f,.5f) && Near(before2-ClassAbility.InjurySum(strong),30f,.5f),"second wind heals its full power over ten pulses");
   // 战吼：范围与时长随战技强度。
   var cry=Learn<Ability_AreaEnemies>(strong,"LP_BattleCry");
   Check(Near(cry.Radius,6f) && cry.Duration==450,"battle cry radius 6 and duration 450 at melee 20");
   var foe=MakeEnemy(center+new IntVec3(3,0,0));var far=MakeEnemy(center+new IntVec3(9,0,0));
   cry.Apply(null);
   Check(Items.Has(foe,"LP_Shaken") && Items.TicksLeft(foe,"LP_Shaken")==450 && !Items.Has(far,"LP_Shaken"),"battle cry shakes enemies inside the radius only");
   // 绊摔：击晕 + 击退。
   var trip=Learn<Ability_Trip>(strong,"LP_Trip");
   foe.Position=center+new IntVec3(1,0,0);foe.Notify_Teleported(false,true);
   trip.Apply(T(foe));
   Check(foe.stances.stunner.Stunned && foe.Position==center+new IntVec3(2,0,0),"trip stuns and knocks the target back one cell");
   // 武器专精分档。
   var mastery=Learn<Ability_SelfBuff>(weak,"LP_WeaponMastery");mastery.Apply(null);
   var m2=Learn<Ability_SelfBuff>(strong,"LP_WeaponMastery");m2.Apply(null);
   Check(Near(weak.GetStatValue(DefDatabase<StatDef>.GetNamed("VEF_MeleeAttackDamageFactor")),1.24f) && Near(strong.GetStatValue(DefDatabase<StatDef>.GetNamed("VEF_MeleeAttackDamageFactor")),1.45f),"weapon mastery tiers 1.24 at melee 6 and 1.45 at melee 20");
   // 不屈意志免疫击晕。
   Learn<Ability_SelfBuff>(strong,"LP_Indomitable").Apply(null);
   strong.stances.stunner.StunFor(300,foe);
   Check(!strong.stances.stunner.Stunned,"indomitable will ignores stuns");
   Items.Remove(strong,"LP_Indomitable");
   // 旋风斩打到相邻敌人。
   var adj=MakeEnemy(center+new IntVec3(-1,0,0));float foeBefore=ClassAbility.InjurySum(adj);
   Learn<Ability_Whirlwind>(strong,"LP_Whirlwind").Apply(null);
   Check(Hurt(adj,foeBefore),"whirlwind damages adjacent enemies");
   // 1.30.1：旋风斩走原版近战伤害结算：× 战技强度，并跟着小人的近战伤害系数变化（Isekai 的精炼、力量也挂在这个系数上）。
   Check(!Items.Has(strong,"LP_WhirlwindFury"),"whirlwind fury is gone after the sweep");
   float h1=Ability_Whirlwind.HitAmount(strong,adj,1f),h15=Ability_Whirlwind.HitAmount(strong,adj,1.5f);
   Check(h1>0f && Near(h15/h1,1.5f,.02f),"whirlwind hit scales with martial power through the melee damage path ("+h1.ToString("0.#")+" -> "+h15.ToString("0.#")+")");
   float mdf=strong.GetStatValue(StatDefOf.MeleeDamageFactor);Items.Give(strong,"LP_PotionGiantStrength",600);float mdf2=strong.GetStatValue(StatDefOf.MeleeDamageFactor);
   float h15b=Ability_Whirlwind.HitAmount(strong,adj,1.5f);Items.Remove(strong,"LP_PotionGiantStrength");
   Check(mdf2>mdf && Near(h15b/h15,mdf2/mdf,.02f),"whirlwind follows the pawn's melee damage factor (x"+(mdf2/mdf).ToString("0.##")+": "+h15.ToString("0.#")+" -> "+h15b.ToString("0.#")+")");
   // 守护姿态分担伤害。
   var ally=Make(0,0,0,false,null,center+new IntVec3(0,0,2));
   Learn<Ability_SelfBuff>(strong,"LP_GuardStance").Apply(null);
   Check(GuardStanceShare.Guardian(ally)==strong,"guard stance guardian found within 3 cells");
   float guardBefore=ClassAbility.InjurySum(strong);
   // 1.7 实际伤害有浮动、会分散到多个部位，所以直接看 PreApplyDamage 之后剩下的伤害量。
   var hit=new DamageInfo(DamageDefOf.Cut,10f,999f,-1f,foe,ally.RaceProps.body.AllParts.First(x=>x.def.defName=="Torso"));ally.PreApplyDamage(ref hit,out _);
   Check(Near(hit.Amount,7f) && ClassAbility.InjurySum(strong)-guardBefore>0f,"guard stance moves 30% of an ally's damage to the guardian");
   // 战意再临：击杀重置冷却。
   Learn<Ability_SelfBuff>(strong,"LP_SurgeOfBattle").Apply(null);
   trip.cooldown=Find.TickManager.TicksGame+5000;
   Check(SurgeKillReset.Reset(strong)==1 && trip.cooldown==0,"surge of battle resets fighter cooldowns on a kill");
   if(!foe.Dead)foe.Destroy();if(!far.Dead)far.Destroy();if(!adj.Destroyed)adj.Destroy();
   Classes.Psycasts(strong).UnlockPath(Fighter);savedPawnId=strong.ThingID;
  }
  static void WizardChecks(){
   var w=Make(0,20,0);var h=Classes.Psycasts(w);h.UnlockPath(Wizard);
   var missile=Learn<Ability_MagicMissile>(w,"LP_MagicMissile");
   Check(Near(missile.Power,6f) && Near(missile.Strength,1.5f),"magic missile 6 per bolt at intellectual 20");
   var foe=MakeEnemy(center+new IntVec3(5,0,0));
   int before=map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("LP_MagicMissileBullet")).Count;
   missile.Apply(T(foe));
   Check(map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("LP_MagicMissileBullet")).Count==before+3,"magic missile launches three bolts");
   foreach(var b in map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("LP_MagicMissileBullet")).ToList())b.Destroy();
   var fireball=Learn<Ability_Fireball>(w,"LP_Fireball");
   Check(Near(fireball.Radius,2.5f) && Near(fireball.Power,22.5f),"fireball radius 2.5, damage 22.5 at intellectual 20");
   var sleep=Learn<Ability_Sleep>(w,"LP_Sleep");
   Check(sleep.Duration==720,"sleep lasts 720 ticks at intellectual 20");
   sleep.Apply(T(foe));
   Check(Items.Has(foe,"LP_MagicSleep") && Items.TicksLeft(foe,"LP_MagicSleep")==720 && foe.Downed,"sleep downs the target for its duration");
   Check(Ability_Sleep.Cannot(foe)!=null,"sleeping target cannot be put to sleep again");
   Items.Remove(foe,"LP_MagicSleep");
   var bolt=Learn<Ability_LightningBolt>(w,"LP_LightningBolt");
   float foeBefore=ClassAbility.InjurySum(foe);
   int hits=Ability_LightningBolt.Strike(w,center+new IntVec3(8,0,0),bolt.Power);
   Check(hits>=1 && Hurt(foe,foeBefore),"lightning bolt hits the enemy on its line");
   var wall=Learn<Ability_WallOfStone>(w,"LP_WallOfStone");
   var walls=Ability_WallOfStone.Conjure(w,center+new IntVec3(0,0,-6),Mathf.RoundToInt(wall.Power));
   Check(walls.Count==5 && walls.All(x=>x.HitPoints==300 && x.Faction==Faction.OfPlayer),"wall of stone conjures five segments at 300 hit points for intellectual 20");
   Check(walls.All(x=>x.Position.z==center.z-6) && walls.Select(x=>x.Position.x).OrderBy(x=>x).SequenceEqual(Enumerable.Range(center.x-2,5)),"wall runs perpendicular to the casting direction");
   Check(walls[0].TryGetComp<CompLifespan>()!=null,"conjured wall expires through its lifespan comp");
   savedWallId=walls[0].ThingID;for(int i=1;i<walls.Count;i++)walls[i].Destroy();
   var tp=Learn<Ability_Teleport>(w,"LP_Teleport");
   Check(Near(tp.GetRangeForPawn(),30f),"teleport range 30 at intellectual 20");
   var dest=center+new IntVec3(-4,0,4);
   Check(tp.ValidateTarget(dest,false),"teleport accepts an empty standable cell");
   tp.Apply(T(dest));
   Check(w.Position==dest,"teleport moves the caster");
   var dis=Learn<Ability_Disintegrate>(w,"LP_Disintegrate");
   Check(Near(dis.Power,90f),"disintegrate 90 damage at intellectual 20");
   var victim=MakeEnemy(center+new IntVec3(6,0,0));
   dis.Apply(T(victim));
   Check(victim.Dead || ClassAbility.InjurySum(victim)>=40f || victim.health.hediffSet.GetMissingPartsCommonAncestors().Count>0,"disintegrate deals heavy damage");
   Check(!victim.Dead || victim.Corpse==null || victim.Corpse.Destroyed,"disintegrated humanlike leaves no corpse");
   if(!foe.Dead)foe.Destroy();if(!victim.Dead && !victim.Destroyed)victim.Destroy();
  }
  static void ClericChecks(){
   var c=Make(0,0,20);var h=Classes.Psycasts(c);h.UnlockPath(Cleric);
   var ally=Make(0,0,0,false,null,center+new IntVec3(2,0,0));
   var cure=Learn<Ability_Heal>(c,"LP_CureWounds");
   Check(Near(cure.Power,30f),"cure wounds heals 30 at social 20");
   Wound(ally,12,4);float before=ClassAbility.InjurySum(ally);
   cure.Apply(T(ally));
   Check(Near(before-ClassAbility.InjurySum(ally),30f,.5f),"cure wounds heals its power");
   var bless=Learn<Ability_TargetBuff>(c,"LP_Bless");
   bless.Apply(T(ally));
   Check(Items.Has(ally,"LP_Blessed") && Items.TicksLeft(ally,"LP_Blessed")==3750 && ally.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_BlessedThought"))!=null,"bless applies the hediff for 3750 ticks and the mood");
   var restore=Learn<Ability_LesserRestoration>(c,"LP_LesserRestoration");
   var flu=HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Flu"),ally);flu.Severity=.5f;ally.health.AddHediff(flu);
   Wound(ally,8,1);
   Check(ally.health.hediffSet.BleedRateTotal>0f,"test ally bleeds before restoration");
   restore.Apply(T(ally));
   Check(!ally.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("Flu")) && Items.Has(ally,"LP_StaunchBleeding") && Near(ally.health.hediffSet.BleedRateTotal,0f),"lesser restoration removes the disease and stops bleeding");
   var guide=Learn<Ability_TargetBuff>(c,"LP_Guidance");float workBefore=ally.GetStatValue(StatDefOf.WorkSpeedGlobal);guide.Apply(T(ally));
   Check(Items.Has(ally,"LP_Guided") && Near(ally.GetStatValue(StatDefOf.WorkSpeedGlobal)/workBefore,1.15f,.03f),"guidance raises global work speed by 15%");
   var flame=Learn<Ability_SacredFlame>(c,"LP_SacredFlame");
   var foe=MakeEnemy(center+new IntVec3(-3,0,0));var lich=MakeEnemy(center+new IntVec3(-3,0,2));
   lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);
   Check(Classes.Undead(lich) && Classes.HolyVulnerable(lich) && !Classes.HolyVulnerable(foe),"lich bloodline counts as undead for holy damage");
   Check(Near(Ability_SacredFlame.Amount(foe,flame.Power),30f) && Near(Ability_SacredFlame.Amount(lich,flame.Power),60f),"sacred flame 30 normally, 60 against undead");
   float foeBefore=ClassAbility.InjurySum(foe);flame.Apply(T(foe));
   Check(Hurt(foe,foeBefore),"sacred flame damages its target");
   var turn=Learn<Ability_TurnUndead>(c,"LP_TurnUndead");
   Check(Near(turn.Radius,9f) && turn.Duration==900,"turn undead radius 9 and 900 ticks at social 20");
   Check(Ability_TurnUndead.Turn(c,lich,900)=="shaken" && Items.Has(lich,"LP_Shaken"),"turn undead only shakes a lich");
   var mass=Learn<Ability_MassHeal>(c,"LP_MassHeal");
   var ally2=Make(0,0,0,false,null,center+new IntVec3(0,0,3));Wound(ally,10,3);Wound(ally2,10,3);
   float b1=ClassAbility.InjurySum(ally),b2=ClassAbility.InjurySum(ally2);
   mass.Apply(null);
   Check(Near(b1-ClassAbility.InjurySum(ally),22.5f,.5f) && Near(b2-ClassAbility.InjurySum(ally2),22.5f,.5f),"mass heal heals every ally in range for 22.5");
   var ward=Learn<Ability_DeathWard>(c,"LP_DeathWard");ward.Apply(T(ally2));
   Check(Items.Has(ally2,"LP_DeathWard") && Near(ally2.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_DeathWard")).Severity,45f),"death ward stores 45 healing at social 20");
   Check(!DeathWardTrigger.TryTrigger(ally2),"death ward stays while the ally stands");
   Wound(ally2,10,3);
   var anesthetic=HediffMaker.MakeHediff(HediffDefOf.Anesthetic,ally2);anesthetic.Severity=1f;ally2.health.AddHediff(anesthetic);
   Check(!Items.Has(ally2,"LP_DeathWard") || DeathWardTrigger.TryTrigger(ally2),"death ward fires when the ally would go down");
   Check(!Items.Has(ally2,"LP_DeathWard") && Near(ClassAbility.InjurySum(ally2),0f,.5f),"death ward consumed and wounds healed");
   ally2.health.RemoveHediff(anesthetic);
   var raise=Learn<Ability_Raise>(c,"LP_Raise");
   var dead=Make(0,0,0,false,null,center+new IntVec3(3,0,3));dead.Kill(null);
   var corpse=dead.Corpse;
   Check(corpse!=null && Ability_Raise.Cannot(corpse)==null,"fresh humanlike corpse can be raised");
   c.psychicEntropy.OffsetPsyfocusDirectly(-.5f);
   Check(!raise.IsEnabledForPawn(out _),"raise needs nearly full psyfocus");
   c.psychicEntropy.OffsetPsyfocusDirectly(1f);
   raise.Apply(T(corpse));
   Check(!dead.Dead && dead.Spawned && Items.Has(dead,"LP_RevivifyWeakness"),"raise resurrects the corpse with revivify weakness");
   if(!foe.Dead)foe.Destroy();lich.Destroy();
  }
  // 1.30.1 法师、牧师乘心灵敏感度（下限 0.2，不封顶）；战士不乘；持续时间与半径不变。
  static Hediff SetSensitivity(Pawn p,float target){
   float now=p.GetStatValue(StatDefOf.PsychicSensitivity);if(Mathf.Abs(now-target)<.001f)return null;
   var def=new HediffDef{defName="LP_TestSensitivity",label="测试敏感度",hediffClass=typeof(HediffWithComps),isBad=false,scenarioCanAdd=false,
    stages=new List<HediffStage>{new HediffStage{statOffsets=new List<StatModifier>{new StatModifier{stat=StatDefOf.PsychicSensitivity,value=target-now}}}}};
   var h=HediffMaker.MakeHediff(def,p);p.health.AddHediff(h);return h;
  }
  static void PsychicChecks(){
   Check(Near(Classes.PsychicScale(0f),.2f) && Near(Classes.PsychicScale(.5f),.5f) && Near(Classes.PsychicScale(1f),1f) && Near(Classes.PsychicScale(3f),3f) && Near(Classes.PsychicScale(5f),5f),"class psychic scale: 0 -> 0.2, then 0.5 / 1 / 3 / 5 uncapped");
   foreach(var s in new[]{.5f,1f,3f}){
    var w=Make(0,20,0);var c=Make(0,0,20);var f=Make(20,0,0);
    var fixtures=new[]{(w,SetSensitivity(w,s)),(c,SetSensitivity(c,s)),(f,SetSensitivity(f,s))};
    Check(Near(w.GetStatValue(StatDefOf.PsychicSensitivity),s) && Near(c.GetStatValue(StatDefOf.PsychicSensitivity),s) && Near(f.GetStatValue(StatDefOf.PsychicSensitivity),s),"sensitivity fixture at "+s.ToStringPercent());
    var fb=Learn<Ability_Fireball>(w,"LP_Fireball");var mm=Learn<Ability_MagicMissile>(w,"LP_MagicMissile");var dis=Learn<Ability_Disintegrate>(w,"LP_Disintegrate");var sleep=Learn<Ability_Sleep>(w,"LP_Sleep");
    Check(Near(fb.Power,22.5f*s) && Near(dis.Power,90f*s) && Near(mm.Strength,1.5f*s) && Near(fb.Radius,2.5f) && sleep.Duration==720,"wizard damage x"+s+" at sensitivity "+s.ToStringPercent()+" (fireball "+fb.Power.ToString("0.##")+"), radius and duration unchanged");
    var cure=Learn<Ability_Heal>(c,"LP_CureWounds");var mass=Learn<Ability_MassHeal>(c,"LP_MassHeal");var flame=Learn<Ability_SacredFlame>(c,"LP_SacredFlame");var bless=Learn<Ability_TargetBuff>(c,"LP_Bless");
    Check(Near(cure.Power,30f*s) && Near(mass.Power,22.5f*s) && Near(flame.Power,30f*s) && bless.Duration==3750,"cleric healing and damage x"+s+" at sensitivity "+s.ToStringPercent()+" (cure "+cure.Power.ToString("0.##")+"), durations unchanged");
    var ally=Make(0,0,0,false,null,center+new IntVec3(2,0,2));Wound(ally,12,4);float before=ClassAbility.InjurySum(ally);cure.Apply(T(ally));
    Check(Near(before-ClassAbility.InjurySum(ally),Mathf.Min(before,30f*s),.5f),"cure wounds actually heals "+(30f*s).ToString("0.#")+" at sensitivity "+s.ToStringPercent());
    var sw=Learn<Ability_SecondWind>(f,"LP_SecondWind");
    Check(Near(sw.Power,30f) && Near(sw.Strength,1.5f),"fighter abilities ignore sensitivity "+s.ToStringPercent());
    // 运行时临时造的状态定义不能进存档：检查完就摘掉。
    foreach(var (pawn,h) in fixtures)if(h!=null && pawn.health.hediffSet.hediffs.Contains(h))pawn.health.RemoveHediff(h);
   }
  }
  // 1.29.0 器械师：路径门槛、器械强度、制作权限、被动（品质、共鸣）、魔力充能、过载射击、快速维修、符文炮台。
  static void ArtificerChecks(){
   ThingDef TD(string n)=>DefDatabase<ThingDef>.GetNamed(n);
   RecipeDef R(string thing)=>DefDatabase<RecipeDef>.AllDefsListForReading.First(r=>r.ProducedThingDef?.defName==thing);
   var a=Make(0,10,0);var craft=a.skills.GetSkill(SkillDefOf.Crafting);craft.Level=5;
   Check(!ArtificerPath.CanPawnUnlock(a),"artificer path locked at crafting 5");
   craft.Level=20;
   Check(ArtificerPath.CanPawnUnlock(a),"artificer path unlockable at crafting 20");
   Classes.Psycasts(a).UnlockPath(ArtificerPath);
   Check(Near(a.GetStatValue(Classes.Stat("LP_ArtificePower")),1.5f),"artifice power reads 1.5 at crafting 20");
   var staff=R("LP_FireStaff");var musket=R("LP_RunicMusket");var silver=R("LP_SilverPistol");var hammer=R("LP_HammerCannon");var wrath=R("LP_MountainWrath");
   Check(!staff.PawnSatisfiesSkillRequirements(a) && !musket.PawnSatisfiesSkillRequirements(a) && !silver.PawnSatisfiesSkillRequirements(a) && !wrath.PawnSatisfiesSkillRequirements(a),"with VPE the forge weapons need artificer passives");
   var workshop=Learn(a,Artificer.Workshop);
   Check(!workshop.ShowGizmoOnPawn(),"passive abilities show no button");
   Check(staff.PawnSatisfiesSkillRequirements(a) && musket.PawnSatisfiesSkillRequirements(a) && R("LP_RunicPistol").PawnSatisfiesSkillRequirements(a) && !silver.PawnSatisfiesSkillRequirements(a),"arcane workshop unlocks the staves, runic pistol and runic musket only");
   Learn(a,Artificer.Inscription);
   Check(silver.PawnSatisfiesSkillRequirements(a) && hammer.PawnSatisfiesSkillRequirements(a) && R("LP_RunicRepeater").PawnSatisfiesSkillRequirements(a) && !wrath.PawnSatisfiesSkillRequirements(a),"rune inscription unlocks group D, hammer cannon and repeater");
   Learn(a,Artificer.MountainForge);
   Check(wrath.PawnSatisfiesSkillRequirements(a),"mountain forge unlocks mountain's wrath");
   Check(R("LP_AdventurersManual").PawnSatisfiesSkillRequirements(Make(0,0,0)),"unrelated recipes are not gated");
   // 1.43.0：DND 近战武器和精灵长弓也要器械师被动；设置可关。
   {
    var smith=Make(0,0,0,false);smith.skills.GetSkill(SkillDefOf.Crafting).Level=20;
    var sword=R("LP_SilverSword");var trident=R("LP_StormTrident");var molten=DefDatabase<RecipeDef>.GetNamed("Make_LP_MoltenMaul");var frostAxe=DefDatabase<RecipeDef>.GetNamed("LP_MakeFrostGiantAxe");
    Check(!sword.PawnSatisfiesSkillRequirements(smith) && !trident.PawnSatisfiesSkillRequirements(smith) && Artificer.Needed(molten)==Artificer.Inscription && Artificer.Needed(frostAxe)==Artificer.Workshop,"a crafting-20 pawn without artificer passives cannot craft silver sword or storm trident");
    var setting=LichMod.Settings.dndWeaponsNeedArtificer;
    try{
     LichMod.Settings.dndWeaponsNeedArtificer=false;
     Check(trident.PawnSatisfiesSkillRequirements(smith) && Artificer.MayCraft(smith,molten),"with the setting off anyone may craft the DND weapons");
    }finally{LichMod.Settings.dndWeaponsNeedArtificer=setting;}
    LichMod.Settings.dndWeaponsNeedArtificer=true;
    try{
     Learn(smith,Artificer.Workshop);
     Check(sword.PawnSatisfiesSkillRequirements(smith) && frostAxe.PawnSatisfiesSkillRequirements(smith) && !trident.PawnSatisfiesSkillRequirements(smith),"arcane workshop unlocks silver sword and frost giant axe but not storm trident");
     Learn(smith,Artificer.Inscription);
     Check(trident.PawnSatisfiesSkillRequirements(smith) && molten.PawnSatisfiesSkillRequirements(smith),"rune inscription unlocks storm trident and molten maul");
    }finally{LichMod.Settings.dndWeaponsNeedArtificer=setting;}
    Check(new[]{"LP_Doom","LP_Skyfire","LP_SkySpear","LP_Godsbane","LP_BalorWhip"}.All(n=>!Artificer.RequiredAbility.ContainsKey(n)) && !Artificer.RequiredAbility.Keys.Any(n=>n.StartsWith("LP_Relic_")),"legendary weapons are not gated");
    Check(TD("LP_StormTrident").description.Contains(Artificer.NeedPrefix+"「") && trident.description.Contains(Artificer.NeedPrefix) && TD("LP_SilverSword").description.Split(new[]{Artificer.NeedPrefix},StringSplitOptions.None).Length==2,"gated weapon and recipe descriptions name the needed artificer passive once");
    // 1.45.0 魔化兵器。
    Check(new[]{"LP_FlameTongue","LP_FrostBrand","LP_DragonSlayer","LP_DefenderSword"}.All(n=>Artificer.RequiredAbility[n]==Artificer.Inscription) && Artificer.RequiredAbility["LP_JavelinLightning"]==Artificer.Workshop && !Artificer.RequiredAbility.ContainsKey("LP_VorpalSword") && !Artificer.RequiredAbility.ContainsKey("LP_SwordVengeance") && TD("LP_FlameTongue").description.Contains(Artificer.NeedPrefix),"1.45.0 magic weapons need rune inscription (javelin: arcane workshop); vorpal and vengeance swords are not craftable");
   }
   // 品质：大师工艺 +1，与矮人叠加，最高传说。
   var musketDef=TD("LP_RunicMusket");
   Check(Arcana.CraftedQuality(QualityCategory.Normal,a,musketDef)==QualityCategory.Normal,"no quality bonus before master craft");
   Learn(a,Artificer.MasterCraft);
   Check(Arcana.CraftedQuality(QualityCategory.Normal,a,musketDef)==QualityCategory.Good && Arcana.CraftedQuality(QualityCategory.Legendary,a,TD("LP_FireStaff"))==QualityCategory.Legendary,"master craft adds one quality level, capped at legendary");
   var dwarf=Make(0,0,0);dwarf.genes.SetXenotype(DefDatabase<XenotypeDef>.GetNamed("LP_Dwarf"));
   Check(CommonRaces.IsDwarf(dwarf) && Arcana.CraftedQuality(QualityCategory.Masterwork,dwarf,musketDef)==QualityCategory.Masterwork && Arcana.CraftedQuality(QualityCategory.Good,dwarf,musketDef)==QualityCategory.Excellent,"dwarf bonus alone stops at masterwork");
   Learn(dwarf,Artificer.MasterCraft);
   Check(Arcana.CraftedQuality(QualityCategory.Excellent,dwarf,musketDef)==QualityCategory.Legendary && Arcana.CraftedQuality(QualityCategory.Masterwork,dwarf,musketDef)==QualityCategory.Legendary,"dwarf and master craft stack up to legendary");
   // 魔力共鸣：F、G 敏感度保底 150%。
   Check(Arcana.Floor(a,"G")==0f,"no resonance floor before learning it");
   Learn(a,Artificer.Resonance);
   Check(Near(Arcana.Floor(a,"G"),1.5f) && Near(Arcana.Floor(a,"F"),1.5f) && Arcana.Floor(a,"D")==0f && Arcana.Sensitivity(a,"G")>=1.5f,"arcane resonance floors staff and dwarf gun sensitivity at 150%");
   // 魔力充能：+0.5，只对 F、G。
   var ally=Make(0,0,0,false,null,center+new IntVec3(2,0,-2));var gunF=TD("LP_RunicMusket");var gunD=TD("LP_SilverPistol");
   float before=Arcana.FactorFor(ally,gunF);
   var charge=Learn<Ability_ArcaneCharge>(a,"LP_ArcaneCharge");
   Check(charge.Duration==1800,"arcane charge lasts 30 seconds");
   charge.Apply(T(ally));
   Check(Items.Has(ally,"LP_ArcaneCharged") && Items.TicksLeft(ally,"LP_ArcaneCharged")==1800 && Near(Arcana.FactorFor(ally,gunF),before+.5f) && Near(Arcana.FactorFor(ally,gunD),1f),"arcane charge adds 0.5 to the factor of group F/G only ("+before.ToString("0.##")+" -> "+Arcana.FactorFor(ally,gunF).ToString("0.##")+")");
   // 过载射击：下一次远程命中 ×(1+器械强度)，武器掉 5% 耐久。
   var gun=(ThingWithComps)ThingMaker.MakeThing(gunF);a.equipment.DestroyAllEquipment();a.equipment.AddEquipment(gun);
   Learn<Ability_SelfBuff>(a,"LP_Overcharge").Apply(null);
   Check(Items.Has(a,"LP_Overcharge") && Items.TicksLeft(a,"LP_Overcharge")==600,"overcharge lasts 10 seconds");
   var melee=new DamageInfo(DamageDefOf.Blunt,10f,0f,-1f,a,null,null);
   Check(Artificer.Overcharge(ref melee)==1f && Items.Has(a,"LP_Overcharge"),"overcharge ignores non-ranged hits");
   int hp=gun.HitPoints;var shot=new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,a,null,gunF);
   float f=Artificer.Overcharge(ref shot);
   Check(Near(f,2.5f) && Near(shot.Amount,25f) && !Items.Has(a,"LP_Overcharge") && gun.HitPoints==hp-Mathf.RoundToInt(gun.MaxHitPoints*.05f),"overcharge multiplies the next ranged hit by 1 + power and wears the gun 5%");
   // 快速维修：武器和服装各 +25%。
   var vest=(Apparel)ThingMaker.MakeThing(TD("Apparel_FlakVest"));a.apparel.Wear(vest);
   gun.HitPoints=gun.MaxHitPoints/2;vest.HitPoints=vest.MaxHitPoints/2;int gunBefore=gun.HitPoints,vestBefore=vest.HitPoints;
   Learn<Ability_QuickRepair>(a,"LP_QuickRepair").Apply(T(a));
   Check(gun.HitPoints==gunBefore+Mathf.RoundToInt(gun.MaxHitPoints*.25f) && vest.HitPoints==vestBefore+Mathf.RoundToInt(vest.MaxHitPoints*.25f),"quick repair restores 25% to the weapon and each apparel");
   // 符文炮台：符文火铳数值 × 器械强度，60 秒。
   var turretAbility=Learn<Ability_RuneTurret>(a,"LP_RuneTurretAbility");
   var cell=center+new IntVec3(-3,0,-3);
   Check(Ability_RuneTurret.Cannot(map,cell)==null,"rune turret target cell is free");
   turretAbility.Apply(T(cell));
   var turret=cell.GetFirstThing<Building_RuneTurret>(map);
   Check(turret!=null && Near(turret.power,1.5f) && turret.Faction==Faction.OfPlayer && turret.TryGetComp<CompLifespan>()?.Props.lifespanTicks==3600,"rune turret placed with power 1.5 for 60 seconds");
   Check(turret.GunCompEq?.parent?.def.defName=="LP_RuneTurretGun" && Near(turret.GunCompEq.PrimaryVerb.verbProps.range,32f),"rune turret mounts the runic musket gun (range 32)");
   var bullet=(LP_ArcaneBullet)GenSpawn.Spawn(TD("LP_Bullet_RuneTurret"),cell,map);
   bullet.Launch(turret,turret.DrawPos,center+new IntVec3(-3,0,6),center+new IntVec3(-3,0,6),ProjectileHitFlags.None,false,turret.GunCompEq.parent);
   Check(bullet.RawDamage==18 && bullet.DamageAmount==Arcana.Scaled(18,1.5f),"rune turret bullet deals 18 x artifice power ("+bullet.DamageAmount+")");
   bullet.Destroy();turret.Destroy();
   Check(Ability_RuneTurret.Cannot(map,center)!=null,"rune turret refuses an occupied cell");
  }
  static void ManualChecks(){
   var manual=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_AdventurersManual"));var use=manual.TryGetComp<CompUseEffect_Manual>();
   var novice=Make(0,7,0,false);
   Check(!use.CanBeUsedBy(novice).Accepted,"manual refused without psylink at intellectual 7");
   novice.skills.GetSkill(SkillDefOf.Intellectual).Level=8;
   Check(use.CanBeUsedBy(novice).Accepted && CompUseEffect_Manual.Choose(novice,out _)==Wizard,"manual accepted at intellectual 8 and picks the wizard path");
   var path=CompUseEffect_Manual.Apply(novice);
   Check(path==Wizard && novice.GetPsylinkLevel()==1 && Classes.HasPath(novice,Wizard),"manual grants psylink 1 and unlocks the wizard path");
   // 1.36.0 术士的副技能也是智识：智识 8 的法师下一本手册领悟术士。
   Check(CompUseEffect_Manual.Choose(novice,out _)==Classes.Path(Classes.SorcererPath),"manual next offers the sorcerer path (intellectual is its alternate skill)");
   CompUseEffect_Manual.Apply(novice);
   Check(!use.CanBeUsedBy(novice).Accepted,"manual refused again when no other skill reaches 6");
   novice.skills.GetSkill(SkillDefOf.Social).Level=6;
   Check(CompUseEffect_Manual.Choose(novice,out _)==Cleric,"linked pawn with social 6 can learn the cleric path from a manual");
   CompUseEffect_Manual.Apply(novice);
   Check(Classes.HasPath(novice,Cleric) && novice.GetPsylinkLevel()==1,"second manual unlocks a path without another psylink level");
   manual.Destroy();
  }
  static void Experience(){
   var p=map.mapPawns.AllPawnsSpawned.First(x=>x.ThingID==savedPawnId);var h=Classes.Psycasts(p);
   h.experience=0f;// 从 0 开始，这一段最多加 50，不会跨过 VPE 的升级门槛（升级时会扣掉经验）。
   float before=h.experience;float given=Classes.State.GivenToday(p);
   p.skills.Learn(SkillDefOf.Melee,1000f);
   Check(Near(h.experience-before,10f) && Near(Classes.State.GivenToday(p),given+10f),"1000 melee xp gives 10 VPE experience to a fighter");
   before=h.experience;p.skills.Learn(SkillDefOf.Intellectual,1000f);
   Check(Near(h.experience,before),"intellectual xp gives a fighter nothing");
   before=h.experience;p.skills.Learn(SkillDefOf.Melee,100f,true);
   Check(Near(h.experience,before),"direct skill xp (neurotrainers) gives nothing");
   before=h.experience;p.skills.Learn(SkillDefOf.Melee,9000f);
   Check(Near(h.experience-before,Classes.XPDailyCap-given-10f) && Near(Classes.State.GivenToday(p),Classes.XPDailyCap),"daily cap of 50 VPE experience from skills");
   var other=Make(20,0,0);float o=Classes.Psycasts(other).experience;other.skills.Learn(SkillDefOf.Melee,1000f);
   Check(Near(Classes.Psycasts(other).experience,o),"pawn without the fighter path gains nothing from melee xp");
   // 1.32.0 背景特性：侍僧的牧师路径经验 ×1.25（每日上限仍按原经验计）。
   var acolyte=Make(0,0,10);var ha=Classes.Psycasts(acolyte);ha.UnlockPath(Cleric);ha.experience=0f;
   foreach(var t in acolyte.story.traits.allTraits.Where(t=>BackgroundTraits.IsBackground(t.def)).ToList())acolyte.story.traits.RemoveTrait(t);
   acolyte.story.traits.GainTrait(new Trait(BackgroundTraits.Acolyte));float ga=Classes.State.GivenToday(acolyte);
   acolyte.skills.Learn(SkillDefOf.Social,1000f);
   Check(Near(ha.experience,12.5f) && Near(Classes.State.GivenToday(acolyte),ga+10f),"acolyte background: 1000 social xp gives a cleric 12.5 VPE experience (x1.25); the daily counter still counts 10 ("+ha.experience+")");
  }
  static void PrepareSave(){
   var p=map.mapPawns.AllPawnsSpawned.First(x=>x.ThingID==savedPawnId);
   Learn(p,"LP_GuardStance");
   if(!Items.Has(p,"LP_GuardStance"))Items.Give(p,"LP_GuardStance",1800,1.5f);
   savedXp=Classes.Psycasts(p).experience;savedGiven=Classes.State.GivenToday(p);
  }
 }
}
