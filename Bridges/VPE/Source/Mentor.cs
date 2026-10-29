using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using VEF.Abilities;
using VanillaPsycastsExpanded;

namespace LichPhylactery.VPEBridge {
 // 1.31.0 云游导师：殖民地有 DND 职业路径（战士/法师/牧师）的人时，一位同路径的导师来住 3 天（按殖民地已有路径的人数加权挑路径）。
 // 导师有该路径全部能力，VPE 等级 = 殖民地同路径最高等级 +3（不超过 VPE 上限）。每过一天教一次：同路径的殖民者各得“下一级所需经验”的 25%，
 // 走单独通道，不受 1.27.0 每日 50 的上限。第 3 天来信：付 500 银或 1 张卷轴再留 3 天（最多一次）。被殖民者攻击就离开，殖民者 -5“赶走了导师”。
 [StaticConstructorOnStartup]
 public static class MentorStartup {static MentorStartup(){Events2.MentorHarmed=Mentor.OnHarmed;}}

 public static class Mentor {
  public const int StayDays=3,ExtendDays=3,LevelBonus=3,SilverCost=500,AnswerHours=6;
  public const float TeachShare=.25f;
  // 1.36.0 加入德鲁伊、圣武士、术士、武僧导师。
  public static readonly string[] PathNames={Classes.FighterPath,Classes.WizardPath,Classes.ClericPath,Classes.DruidPath,Classes.PaladinPath,Classes.SorcererPath,Classes.MonkPath};
  static readonly Dictionary<string,string> Kinds=new Dictionary<string,string>{{Classes.FighterPath,"LP_Mentor_Fighter"},{Classes.WizardPath,"LP_Mentor_Wizard"},{Classes.DruidPath,"LP_Mentor_Druid"},{Classes.PaladinPath,"LP_Mentor_Paladin"},{Classes.SorcererPath,"LP_Mentor_Sorcerer"},{Classes.MonkPath,"LP_Mentor_Monk"}};
  public static string KindFor(string path)=>Kinds.TryGetValue(path,out var k)?k:"LP_Mentor_Cleric";
  static FactionDef scholars;
  public static FactionDef ScholarsDef=>scholars??(scholars=DefDatabase<FactionDef>.GetNamedSilentFail("LP_WanderingScholars"));
  public static MentorState State=>GameComp<MentorState>.Get();
  public static IEnumerable<Pawn> Students(Map map,LP_ClassPathDef path)=>map==null?Enumerable.Empty<Pawn>():map.mapPawns.FreeColonistsSpawned.Where(p=>Classes.HasPath(p,path));
  // 每条路径在殖民地里的人数（只算战士、法师、牧师）。
  public static Dictionary<LP_ClassPathDef,int> PathCounts(Map map){
   var d=new Dictionary<LP_ClassPathDef,int>();
   foreach(var name in PathNames){var path=Classes.Path(name);if(path==null)continue;int n=Students(map,path).Count();if(n>0)d[path]=n;}
   return d;
  }
  public static bool Visiting(Map map)=>State?.visits.Any(v=>v.map==map && !v.left)==true;
  public static bool CanFire(Map map)=>LichMod.Settings.wanderingMentor && map!=null && map.IsPlayerHome && ScholarsDef!=null && PathCounts(map).Count>0 && !Visiting(map);
  public static LP_ClassPathDef PickPath(Map map,float roll){
   var counts=PathCounts(map);if(counts.Count==0)return null;
   float total=counts.Values.Sum(),r=roll*total;
   foreach(var kv in counts){if(r<kv.Value)return kv.Key;r-=kv.Value;}
   return counts.Keys.Last();
  }
  public static int Level(Pawn p)=>Classes.Psycasts(p)?.level??0;
  public static int MaxLevel(Pawn p){var h=Classes.Psycasts(p);return h==null?100:Mathf.Max(1,(int)h.def.maxSeverity);}
  public static int TargetLevel(Map map,LP_ClassPathDef path,Pawn mentor)=>Mathf.Min(MaxLevel(mentor),Students(map,path).Select(Level).DefaultIfEmpty(0).Max()+LevelBonus);
  public static Faction Scholars(){
   if(ScholarsDef==null)return null;bool had=Factions.Of(ScholarsDef)!=null;
   var f=Factions.EnsureHidden(ScholarsDef);if(f!=null && !had)Factions.SetRelation(f,Faction.OfPlayer,0);
   return f;
  }
  public static Pawn MakeMentor(Map map,LP_ClassPathDef path){
   var f=Scholars();if(f==null)return null;
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(KindFor(path.defName)),f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true));
   foreach(var sd in new[]{path.requiredSkill,path.alternateSkill}){if(sd==null)continue;var skill=p.skills?.GetSkill(sd);if(skill!=null && !skill.TotallyDisabled && skill.Level<16)skill.Level=16;}
   if(p.GetMainPsylinkSource()==null)p.ChangePsylinkLevel(1,false);
   var h=Classes.Psycasts(p);
   if(h!=null){
    int target=TargetLevel(map,path,p);if(target>h.level)h.ChangeLevel(target-h.level,false);
    if(!h.unlockedPaths.Contains(path))h.UnlockPath(path);
   }
   var comp=p.GetComp<CompAbilities>();
   if(comp!=null)foreach(var a in path.abilities)if(!comp.HasAbility(a))comp.GiveAbility(a);
   return p;
  }
  public static MentorVisit Start(Map map,LP_ClassPathDef path=null){
   path=path??PickPath(map,Rand.Value);if(path==null)return null;
   var p=MakeMentor(map,path);if(p==null)return null;
   if(!Events2.EdgeCell(map,out var entry)){p.Destroy();return null;}
   GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,4),map);
   Events2.MarkVisitor(p,"mentor");
   if(!RCellFinder.TryFindRandomSpotJustOutsideColony(p,out var spot))spot=CellFinder.RandomClosewalkCellNear(map.Center,map,10);
   var lord=LordMaker.MakeNewLord(p.Faction,new LordJob_MentorVisit(spot),map,new[]{p});
   var v=new MentorVisit{map=map,mentor=p,lord=lord,path=path,arriveTick=Find.TickManager.TicksGame,maxTeach=StayDays};
   State?.visits.Add(v);
   return v;
  }
  // 教一次：同路径殖民者各得下一级所需经验的 25%（直接加进 VPE，不经过每日上限）。返回受教人数。
  public static float Share(Pawn p){var h=Classes.Psycasts(p);return h==null?0f:Hediff_PsycastAbilities.ExperienceRequiredForLevel(h.level)*TeachShare;}
  public static int Teach(MentorVisit v){
   int n=0;
   foreach(var s in Students(v.map,v.path).ToList()){var h=Classes.Psycasts(s);if(h==null)continue;h.GainExperience(Share(s),true);Adventurer.GainXP(s,Adventurer.LessonXP);n++;} // 1.48.0 每课冒险者经验 50
   v.taught++;
   if(n>0)Messages.Message(v.mentor.LabelShort+"给"+n+"名"+v.path.label+"上了一课。",new LookTargets(v.mentor),MessageTypeDefOf.PositiveEvent,false);
   return n;
  }
  public static void Leave(MentorVisit v){if(v.left)return;v.left=true;v.lord?.ReceiveMemo(LordJob_MentorVisit.LeaveMemo);}
  public static void Tick(MentorState s){
   int now=Find.TickManager.TicksGame;
   for(int i=s.visits.Count-1;i>=0;i--){
    var v=s.visits[i];
    if(v.mentor==null || v.mentor.Dead || v.mentor.Destroyed || v.map==null || !Find.Maps.Contains(v.map) || v.lord==null || !v.map.lordManager.lords.Contains(v.lord)){s.visits.RemoveAt(i);continue;}
    if(v.left)continue;
    if(v.taught<v.maxTeach && now>=v.arriveTick+(v.taught+1)*GenDate.TicksPerDay)Teach(v);
    if(v.taught<v.maxTeach)continue;
    if(!v.extended && !v.asked){AskStay(v);continue;}
    if(v.extended || now>=v.askTick+AnswerHours*GenDate.TicksPerHour)Leave(v);
   }
  }
  public static Letter_MentorLeaving AskStay(MentorVisit v){
   v.asked=true;v.askTick=Find.TickManager.TicksGame;
   var l=(Letter_MentorLeaving)LetterMaker.MakeLetter(DefDatabase<LetterDef>.GetNamed("LP_MentorChoiceLetter"));
   l.Label="导师要走了";l.visit=v;l.lookTargets=new LookTargets(v.mentor);l.relatedFaction=v.mentor.Faction;
   l.Text=v.mentor.LabelShort+"的三天课程结束了，正准备上路。\n\n付"+SilverCost+"银，或者送他1张卷轴，他愿意再留"+ExtendDays+"天，再教"+ExtendDays+"次（只能挽留一次）。\n\n"+AnswerHours+"小时内不答复，他就走了。";
   Find.LetterStack.ReceiveLetter(l);return l;
  }
  // 殖民地储存区（或家园区）里没被禁止的东西。
  static List<Thing> Owned(Map map,Func<Thing,bool> pred)=>map.listerThings.AllThings.Where(t=>t.Spawned && pred(t) && !t.IsForbidden(Faction.OfPlayer) && (t.IsInAnyStorage() || map.areaManager.Home[t.Position])).ToList();
  public static int Silver(Map map)=>Owned(map,t=>t.def==ThingDefOf.Silver).Sum(t=>t.stackCount);
  public static Thing Scroll(Map map)=>Owned(map,t=>t.def.defName.StartsWith("LP_Scroll")).FirstOrDefault();
  public static bool Extend(MentorVisit v,string pay){
   if(v==null || v.extended || v.left)return false;
   if(pay=="silver"){
    if(Silver(v.map)<SilverCost)return false;
    int left=SilverCost;foreach(var t in Owned(v.map,x=>x.def==ThingDefOf.Silver).OrderByDescending(x=>x.stackCount)){if(left<=0)break;int n=Math.Min(left,t.stackCount);t.SplitOff(n).Destroy();left-=n;}
   }else{var s=Scroll(v.map);if(s==null)return false;s.SplitOff(1).Destroy();}
   v.extended=true;v.maxTeach=StayDays+ExtendDays;
   Messages.Message(v.mentor.LabelShort+"答应再留"+ExtendDays+"天。",new LookTargets(v.mentor),MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  // 被殖民者打：导师离开，殖民者 -5。
  public static void OnHarmed(Pawn victim,Pawn attacker){
   var v=State?.visits.FirstOrDefault(x=>x.mentor==victim);if(v==null || v.left)return;
   Leave(v);v.drivenOff=true;
   var thought=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_MentorDrivenOff");
   if(thought!=null)foreach(var c in v.map.mapPawns.FreeColonistsSpawned)c.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
   Find.LetterStack.ReceiveLetter("赶走了导师",attacker.LabelShort+"对"+victim.LabelShort+"动了手。导师愤然离去。",LetterDefOf.NegativeEvent,victim);
  }
 }

 public class MentorVisit : IExposable {
  public Map map;public Pawn mentor;public Lord lord;public LP_ClassPathDef path;
  public int arriveTick,taught,maxTeach=Mentor.StayDays,askTick=-1;public bool asked,extended,left,drivenOff;
  public void ExposeData(){
   Scribe_References.Look(ref map,"map");Scribe_References.Look(ref mentor,"mentor");Scribe_References.Look(ref lord,"lord");
   Scribe_Defs.Look(ref path,"path");
   Scribe_Values.Look(ref arriveTick,"arriveTick");Scribe_Values.Look(ref taught,"taught");Scribe_Values.Look(ref maxTeach,"maxTeach",Mentor.StayDays);Scribe_Values.Look(ref askTick,"askTick",-1);
   Scribe_Values.Look(ref asked,"asked");Scribe_Values.Look(ref extended,"extended");Scribe_Values.Look(ref left,"left");Scribe_Values.Look(ref drivenOff,"drivenOff");
  }
 }
 public class MentorState : GameComponent {
  public List<MentorVisit> visits=new List<MentorVisit>();
  public MentorState(Game game){}
  public override void GameComponentTick(){if(visits.Count>0 && Find.TickManager.TicksGame%250==173)Mentor.Tick(this);}
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref visits,"lpMentorVisits",LookMode.Deep);if(Scribe.mode==LoadSaveMode.PostLoadInit)visits=visits?.Where(v=>v!=null).ToList()??new List<MentorVisit>();}
 }

 // 原版访客 lord 的变体：走到殖民地外的落脚点，在附近闲逛（不交易），收到离开的信号就走。被打成敌人也走。
 public class LordJob_MentorVisit : LordJob {
  public const string LeaveMemo="LP_MentorLeave";
  public IntVec3 spot;
  public LordJob_MentorVisit(){}
  public LordJob_MentorVisit(IntVec3 spot){this.spot=spot;}
  public override StateGraph CreateGraph(){
   var g=new StateGraph();
   var travel=new LordToil_Travel(spot);g.StartingToil=travel;
   var stay=new LordToil_DefendPoint(spot,10f,10f);g.AddToil(stay);
   var leave=new LordToil_ExitMap(LocomotionUrgency.Walk,false,true);g.AddToil(leave);
   var arrive=new Transition(travel,stay);arrive.AddTrigger(new Trigger_Memo("TravelArrived"));arrive.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerDay/4));g.AddTransition(arrive);
   var go=new Transition(stay,leave);go.AddSource(travel);go.AddTrigger(new Trigger_Memo(LeaveMemo));go.AddTrigger(new Trigger_BecamePlayerEnemy());g.AddTransition(go);
   return g;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref spot,"spot");}
 }

 public class Letter_MentorLeaving : ChoiceLetter {
  public MentorVisit visit;
  public override IEnumerable<DiaOption> Choices{get{
   if(ArchivedOnly || visit==null || visit.left || visit.extended){yield return Option_Close;yield break;}
   var silver=Choice("付"+Mentor.SilverCost+"银，请他再留"+Mentor.ExtendDays+"天",()=>Mentor.Extend(visit,"silver"));
   int have=Mentor.Silver(visit.map);if(have<Mentor.SilverCost)silver.Disable("白银不够（有 "+have+"）");yield return silver;
   var scroll=Choice("送他1张卷轴，请他再留"+Mentor.ExtendDays+"天",()=>Mentor.Extend(visit,"scroll"));if(Mentor.Scroll(visit.map)==null)scroll.Disable("殖民地里没有卷轴");yield return scroll;
   yield return Choice("让他走",()=>Mentor.Leave(visit));
   yield return Option_Postpone;
  }}
  DiaOption Choice(string text,Action act)=>new DiaOption(text){action=()=>{act?.Invoke();Find.LetterStack.RemoveLetter(this);},resolveTree=true};
  public override void ExposeData(){base.ExposeData();Scribe_Deep.Look(ref visit,"lpMentorVisitCopy");
   if(Scribe.mode==LoadSaveMode.PostLoadInit && visit?.mentor!=null){var live=Mentor.State?.visits.FirstOrDefault(v=>v.mentor==visit.mentor);if(live!=null)visit=live;}
  }
 }

 public class IncidentWorker_WanderingMentor : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms)=>Mentor.CanFire(parms.target as Map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;if(!Mentor.CanFire(map))return false;
   var v=Mentor.Start(map);if(v==null)return false;
   SendStandardLetter("云游导师","一位云游的"+v.path.label+"导师"+v.mentor.LabelShort+"（VPE 等级 "+Mentor.Level(v.mentor)+"）路过殖民地，愿意住"+Mentor.StayDays+"天。\n\n他每天给殖民地里同路径（"+v.path.label+"）的人上一课：每人得到下一级所需经验的25%（不受每天50点的上限限制）。\n\n别惹他——有人动手他就走，大家会很懊恼。",LetterDefOf.PositiveEvent,parms,new LookTargets(v.mentor));
   return true;
  }
 }
}
