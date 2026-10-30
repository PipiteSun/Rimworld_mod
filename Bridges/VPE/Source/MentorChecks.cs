using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;
using VEF.Abilities;
using VanillaPsycastsExpanded;

namespace LichPhylactery.VPEBridge {
 // 1.31.0 云游导师自检：由 BridgeChecks 在 VPE 环境里调用（Test.ps1 -VPE）。
 public static class MentorChecks {
  static int checks;static Map map;static readonly List<Thing> made=new List<Thing>();
  static string savedMentorId;static int savedTaught;static bool savedExtended;
  static void Check(bool ok,string message){if(!ok)throw new Exception("[vpe mentor] "+message);checks++;Log.Message("[LICH-TEST] OK [vpe mentor] "+message);}
  static bool Near(float a,float b)=>Math.Abs(a-b)<=Math.Max(.02f,Math.Abs(b)*.01f);
  static LP_ClassPathDef Fighter=>Classes.Path(Classes.FighterPath);
  static LP_ClassPathDef Wizard=>Classes.Path(Classes.WizardPath);
  static Pawn Student(LP_ClassPathDef path,int level){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25,fixedChronologicalAge:25,forcedXenotype:XenotypeDefOf.Baseliner));
   p.skills.GetSkill(path.requiredSkill).Level=12;GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,8),map);made.Add(p);
   // 1.73.0：学生按冒险者职业等级算。
   var a=Adventurer.Get(p)??Adventurer.TryAdd(p,level,true);a.level=Math.Max(a.level,level);a.xp=Adventurer.XpTable[Math.Min(20,a.level)-1];ClassSystem.SetLevel(a,Mentor.ClassFor(path),level);
   return p;
  }
  static void Track(MentorVisit v){if(v?.mentor!=null)made.Add(v.mentor);}
  // 一堆超过叠放上限时拆成几堆，各占一格，全部划进家园区。返回最后一堆。
  static Thing Put(ThingDef def,int n){Thing last=null;while(n>0){var t=ThingMaker.MakeThing(def);t.stackCount=Math.Min(n,def.stackLimit);n-=t.stackCount;var c=CellFinder.RandomClosewalkCellNear(map.Center,map,5,x=>x.GetFirstItem(map)==null);GenSpawn.Spawn(t,c,map);map.areaManager.Home[t.Position]=true;t.SetForbidden(false,false);made.Add(t);last=t;}return last;}

  public static int Run(){
   checks=0;map=Find.CurrentMap;made.Clear();
   var letters=new HashSet<Letter>(Find.LetterStack.LettersListForReading);var lords=new HashSet<Lord>(map.lordManager.lords);
   bool setting=LichMod.Settings.wanderingMentor;
   try{Body();}
   finally{
    LichMod.Settings.wanderingMentor=setting;
    Mentor.State?.visits.RemoveAll(v=>v.mentor==null || v.mentor.ThingID!=savedMentorId);
    foreach(var l in map.lordManager.lords.Where(l=>!lords.Contains(l) && !l.ownedPawns.Any(p=>p.ThingID==savedMentorId)).ToList())map.lordManager.RemoveLord(l);
    foreach(var t in made)if(t!=null && !t.Destroyed && t.ThingID!=savedMentorId)t.Destroy();made.Clear();
    foreach(var l in Find.LetterStack.LettersListForReading.Where(l=>!letters.Contains(l)).ToList())Find.LetterStack.RemoveLetter(l);
   }
   return checks;
  }
  static void Body(){
   var def=DefDatabase<IncidentDef>.GetNamedSilentFail("LP_WanderingMentor");
   Check(def!=null && def.category==IncidentCategoryDefOf.Misc && def.minRefireDays==30 && def.Worker is IncidentWorker_DND,"wandering mentor event: misc, 30 day refire, DND frequency");
   Check(Mentor.ScholarsDef!=null && Mentor.ScholarsDef.hidden && !Mentor.ScholarsDef.permanentEnemy,"mentors belong to a hidden neutral faction");
   foreach(var n in Mentor.PathNames)Check(DefDatabase<PawnKindDef>.GetNamedSilentFail(Mentor.KindFor(n))!=null,"mentor pawn kind for "+n);
   var f1=Student(Fighter,4);var f2=Student(Fighter,2);var w1=Student(Wizard,6);
   Check(Mentor.CanFire(map),"mentor can fire when colonists have class paths");
   LichMod.Settings.wanderingMentor=false;Check(!Mentor.CanFire(map),"mentor setting turns the event off");LichMod.Settings.wanderingMentor=true;
   // 按人数加权挑路径。
   var counts=Mentor.PathCounts(map);float total=counts.Values.Sum(),acc=0;bool weighted=true;
   foreach(var kv in counts){float lo=acc/total,hi=(acc+kv.Value)/total;if(Mentor.PickPath(map,lo+.001f)!=kv.Key || Mentor.PickPath(map,hi-.001f)!=kv.Key)weighted=false;acc+=kv.Value;}
   Check(weighted && counts[Fighter]>=2 && counts[Wizard]>=1,"mentor path is weighted by how many colonists follow it ("+string.Join(",",counts.Select(kv=>kv.Key.defName+":"+kv.Value))+")");
   // 导师：同路径全部能力，等级 = 同路径最高 +3。
   var v=Mentor.Start(map,Wizard);Track(v);
   int best=Mentor.Students(map,Wizard).Max(s=>Mentor.Level(s,Wizard));var wc=Mentor.ClassFor(Wizard);
   Check(v!=null && v.mentor.Spawned && v.mentor.Faction?.def==Mentor.ScholarsDef && !v.mentor.HostileTo(Faction.OfPlayer) && ClassSystem.Level(v.mentor,wc)>0,"a wizard mentor arrives as a neutral visitor with wizard class levels");
   Check(Mentor.Level(v.mentor,Wizard)==Math.Min(ClassSystem.CapLevel,best+Mentor.LevelBonus),"mentor class level = best colonist in the class +3 ("+Mentor.Level(v.mentor,Wizard)+" vs "+best+")");
   Check(wc.abilities.Where(e=>e.level<=Mentor.Level(v.mentor,Wizard) && e.capstoneSkill==null).All(e=>v.mentor.abilities.GetAbility(e.ability)!=null),"mentor knows every wizard ability up to its class level");
   Check(v.lord?.LordJob is LordJob_MentorVisit,"mentor stays with a visitor lord (no trading)");
   Check(!Mentor.CanFire(map),"only one mentor at a time");
   // 每天一课：25%，不受每日上限。
   var aw=Adventurer.Get(w1);var af=Adventurer.Get(f1);float x0=aw.xp,f0=af.xp;float share=Mentor.AdventurerShare(w1);bool shareOk=Near(share,(aw.NextXp-Legend.XpFor(aw.level))*.25f);
   v.arriveTick=Find.TickManager.TicksGame-GenDate.TicksPerDay-1;Mentor.Tick(Mentor.State);
   Check(v.taught==1 && share>0 && aw.xp-x0>=share && Near(af.xp,f0),"day 1: wizards gain adventurer XP (25% of the next level + 50 = "+(aw.xp-x0).ToString("0")+"); fighters gain nothing");
   Check(shareOk,"lesson size is 25% of the adventurer XP needed for the next level");
   Mentor.Tick(Mentor.State);Check(v.taught==1,"one lesson per day");
   v.taught=Mentor.StayDays-1;v.arriveTick=Find.TickManager.TicksGame-Mentor.StayDays*GenDate.TicksPerDay-1;Mentor.Tick(Mentor.State);
   Check(v.taught==Mentor.StayDays,"three lessons over three days");
   Mentor.Tick(Mentor.State);var letter=Find.LetterStack.LettersListForReading.OfType<Letter_MentorLeaving>().LastOrDefault();
   Check(v.asked && letter?.visit==v && letter.Label.ToString()=="导师要走了","day 3: the mentor is leaving letter");
   // 挽留：500 银，扣费、再留 3 天，只能一次。
   foreach(var t in map.listerThings.ThingsOfDef(ThingDefOf.Silver).ToList())if(t.Spawned)t.DeSpawn();
   var opts=letter.Choices.ToList();Check(opts[0].disabled,"silver option is greyed out without 500 silver");
   var put=Put(ThingDefOf.Silver,600);int silver=Mentor.Silver(map);bool ext=Mentor.Extend(v,"silver");
   Check(ext && v.extended && v.maxTeach==Mentor.StayDays+Mentor.ExtendDays && Mentor.Silver(map)==silver-Mentor.SilverCost,"paying 500 silver keeps the mentor 3 more days (had "+silver+", now "+Mentor.Silver(map)+", extend "+ext+", left "+v.left+", put "+put.Spawned+"/"+put.stackCount+")");
   Check(!Mentor.Extend(v,"silver"),"a mentor can only be kept once");
   v.arriveTick=Find.TickManager.TicksGame-(Mentor.StayDays+1)*GenDate.TicksPerDay-1;Mentor.Tick(Mentor.State);Check(v.taught==Mentor.StayDays+1 && !v.left,"the extension brings more lessons");
   v.taught=v.maxTeach;Mentor.Tick(Mentor.State);Check(v.left,"after the extra lessons the mentor leaves");
   // 卷轴挽留。
   var v2=Mentor.Start(map,Fighter);Track(v2);v2.taught=v2.maxTeach;Mentor.Tick(Mentor.State);
   var scroll=Put(DefDatabase<ThingDef>.GetNamed("LP_ScrollFireball"),1);
   Check(Mentor.Extend(v2,"scroll") && scroll.Destroyed && v2.extended,"giving a scroll keeps the mentor 3 more days");
   // 不答复：几个小时后离开。
   var v3=Mentor.Start(map,Fighter);Track(v3);v3.taught=v3.maxTeach;Mentor.Tick(Mentor.State);v3.askTick-=Mentor.AnswerHours*GenDate.TicksPerHour+1;Mentor.Tick(Mentor.State);
   Check(v3.left && !v3.extended,"no answer: the mentor leaves");
   // 被打：离开，殖民者 -5。
   var v4=Mentor.Start(map,Fighter);Track(v4);
   v4.mentor.TakeDamage(new DamageInfo(DamageDefOf.Cut,3f,0f,-1f,f2));
   var thought=DefDatabase<ThoughtDef>.GetNamed("LP_MentorDrivenOff");
   Check(v4.left && v4.drivenOff && f1.needs.mood.thoughts.memories.GetFirstMemoryOfDef(thought)!=null && Near(thought.stages[0].baseMoodEffect,-5f),"attacking the mentor drives him off: colonists -5 mood");
   foreach(var c in map.mapPawns.FreeColonistsSpawned)c.needs?.mood?.thoughts?.memories?.RemoveMemoriesOfDef(thought);
   // 存档：留一位导师。
   var v5=Mentor.Start(map,Fighter);v5.taught=1;v5.extended=true;v5.maxTeach=6;savedMentorId=v5.mentor.ThingID;savedTaught=1;savedExtended=true;
  }
  public static int AfterLoad(){
   checks=0;map=Find.CurrentMap;
   var v=Mentor.State?.visits.FirstOrDefault(x=>x.mentor?.ThingID==savedMentorId);
   Check(v!=null && v.taught==savedTaught && v.extended==savedExtended && v.maxTeach==6 && v.path==Fighter && v.lord!=null && v.mentor.Spawned,"mentor visit (lessons, extension, path, lord) survives save/load");
   if(v!=null){Mentor.State.visits.Remove(v);if(v.lord!=null && map.lordManager.lords.Contains(v.lord))map.lordManager.RemoveLord(v.lord);v.mentor.Destroy();}
   return checks;
  }
 }
}
