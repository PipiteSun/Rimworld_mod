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
 // 1.73.0：八个职业都移到主程序集（Source/Class*.cs，主程序集 -Only Classes 检查）。这里只剩 VPE 共存的检查：旧路径隐藏、旧存档迁移、导师、秘术残卷、冒险者 × VPE 路径点。
 public static partial class BridgeChecks {
  static int checks;static Map map;static IntVec3 center;static readonly List<Pawn> made=new List<Pawn>();
  static string savedPawnId,savedWallId;static float savedXp,savedGiven;
  static void Check(bool ok,string message){if(!ok)throw new Exception("[vpe bridge] "+message);checks++;Log.Message("[LICH-TEST] OK [vpe bridge] "+message);}
  static bool Near(float a,float b,float tol=.02f)=>Math.Abs(a-b)<=Math.Max(tol,Math.Abs(b)*.01f);
  static AbilityDef Def(string name)=>DefDatabase<AbilityDef>.GetNamed(name);
  static LP_ClassPathDef Fighter=>Classes.Path(Classes.FighterPath);
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
  public static int Run(){
   checks=0;map=Find.CurrentMap;made.Clear();Arena();
   try{MartialChecks();ClassesChecks();checks+=MentorChecks.Run();NetherilChecks();AdventurerChecks();}
   finally{foreach(var p in made)if(!p.Destroyed && p.ThingID!=savedMigrateId)p.Destroy();made.Clear();}
   return checks;
  }
  public static int AfterLoad(){checks=0;map=Find.CurrentMap;ClassesAfterLoad();checks+=MentorChecks.AfterLoad();return checks;}
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
  static Faction Enemy=>Find.FactionManager.AllFactionsListForReading.First(f=>f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && !f.def.hidden);
  static Pawn MakeEnemy(IntVec3 at)=>Make(0,0,0,false,Enemy,at);
  static Ability Learn(Pawn p,string ability){var comp=p.GetComp<CompAbilities>();var def=Def(ability);if(!comp.HasAbility(def))comp.GiveAbility(def);return comp.LearnedAbilities.First(a=>a.def==def);}
  static GlobalTargetInfo[] T(Thing t)=>new[]{new GlobalTargetInfo(t)};
  static GlobalTargetInfo[] T(IntVec3 c)=>new[]{new GlobalTargetInfo(c,map)};
  static bool Hurt(Pawn p,float injuryBefore)=>p.Dead || ClassMelee.InjurySum(p)>injuryBefore || p.health.hediffSet.GetMissingPartsCommonAncestors().Count>0;
  static void Wound(Pawn p,float amount,int count=1){var parts=p.RaceProps.body.AllParts.Where(x=>x.depth==BodyPartDepth.Outside && x.coverageAbs>0 && x.def.defName!="Head" && x.def.defName!="Neck" && x.def.defName!="Torso" && x.def.GetMaxHealth(p)>=amount*2f).ToList();for(int i=0;i<count;i++){var part=parts[i%parts.Count];var inj=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,p,part);inj.Severity=amount;p.health.AddHediff(inj,part);}}
 }
}
