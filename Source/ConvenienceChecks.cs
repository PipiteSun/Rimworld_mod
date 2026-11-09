using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.136.0 便利三件：Test.ps1 -Only Convenience。
 // 自动施法（未征召开关只给非敌对能力、资源门槛、未征召时真的放治疗、门槛挡住）、头像资源条的主资源、怪物图鉴（只记本模组怪物、击杀分级、传说伤害 +5%、条目文字、存读档）、
 // DND 角色卡（分页挂在人形上、六维 / 职业 / 强度来源都有内容）。
 public partial class LichChecks {
  static int cvSavedKills;static string cvSavedKind;
  void CheckConvenience(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool on=s.adventurer;s.adventurer=true;
   Rand.PushState(113601);
   try{CheckConvenienceInner();}finally{Rand.PopState();Adventurer.TestSuppressed=sup;s.adventurer=on;MlClean();}
  }
  void CheckConvenienceInner(){
   var cleric=DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Cleric");
   // 1. 设置存取与分类：未征召开关只给非敌对能力；门槛 0～90%，每个能力分开记。
   {var p=MlClassPawn(cleric,5,5,-10,10);var cure=p.abilities.GetAbility(Ab("LP_CureWounds")) as Ability_Class;var h=Adventurer.Get(p);
    var mm=Ab("LP_AM_PsychicSpike");
    AutoCastPlus.SetUndrafted(p,cure.def,true);AutoCastPlus.SetThreshold(p,cure.def,40);AutoCastPlus.SetThreshold(p,mm,200);
    bool stored=AutoCastPlus.Undrafted(p,cure.def) && AutoCastPlus.Threshold(p,cure.def)==40 && AutoCastPlus.Threshold(p,mm)==90 && !AutoCastPlus.Undrafted(p,mm);
    AutoCastPlus.SetUndrafted(p,cure.def,false);bool off=!AutoCastPlus.Undrafted(p,cure.def) && AutoCastPlus.Threshold(p,cure.def)==40;
    Check(stored && off && AutoCastPlus.Helpful(cure) && mm.hostile,"[1.136.0] autocast settings per ability: undrafted flag + threshold (clamped to 90%), heals count as helpful, attacks don't");}
   // 2. 未征召时自动施放：平时也会给受伤的队友放治疗；资源低于门槛就不放。
   {var p=MlClassPawn(cleric,5,5,-8,10);var h=Adventurer.Get(p);p.jobs.StopAll();var cure=Ab("LP_CureWounds");
    var ally=MlPawn(Faction.OfPlayer,-7,10);ally.TakeDamage(new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,null,ally.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso)));float i0=ClassMelee.InjurySum(ally);
    AutoCastPlus.SetUndrafted(p,cure,true);ClassSystem.AddResource(h,cleric,99f);bool calm=AutoCastPlus.ReadyCalm(p);
    var cast=ClassAutoCast.Tick(h,true);float i1=ClassMelee.InjurySum(ally);
    p.abilities.GetAbility(cure).ResetCooldown();ClassSystem.AddResource(h,cleric,-99f);ClassSystem.AddResource(h,cleric,1f);AutoCastPlus.SetThreshold(p,cure,90);
    var none=ClassAutoCast.Tick(h,true);
    Check(calm && cast!=null && cast.def==cure && i1<i0 && none==null,"[1.136.0] undrafted autocast heals an injured ally while calm ("+i0.ToString("0.0")+" → "+i1.ToString("0.0")+"); below the resource threshold it doesn't cast");}
   // 3. 头像资源条的主资源：牧师 = 神恩；游侠 = 法术能量；只有湖印的人 = 湖息。
   {var c=MlClassPawn(cleric,5,5,-6,10);var r=MlClassPawn(DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Ranger"),5,5,-5,10);
    var q=LlCaster(3,10,-4,10);var hq=Adventurer.Get(q);hq.classes.levels.Clear();LakeSeals.Learn(q,LS("Thunderwave"));
    var rc=AutoCastPlus.Main(c);var rr=AutoCastPlus.Main(r);var rq=AutoCastPlus.Main(q);
    Check(rc.label=="神恩" && rc.max>0f && rr.label=="法术能量" && rr.max>0f && rq.label=="湖息" && Near(rq.max,3f) && LichMod.Settings.resourceBar,"[1.136.0] portrait bar shows the main resource: "+rc.label+" "+rc.cur+"/"+rc.max+", "+rr.label+" "+rr.max+", "+rq.label+" "+rq.max);}
   // 4. 怪物图鉴：只记本模组的怪物 / 野兽（不记人）；玩家打死才算击杀；分级 1 / 3 / 10 / 25；传说后伤害 +5%。
   {var st=Bestiary.State;var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_Owlbear")??DefDatabase<PawnKindDef>.GetNamed("LP_Worg");
    var beast=PawnGenerator.GeneratePawn(kind);GenSpawn.Spawn(beast,MlCell(-10,12),map);mlMade.Add(beast);
    var human=MlPawn(MlEnemy,-9,12);var hunter=MlPawn(Faction.OfPlayer,-8,12);
    int k0=Bestiary.Kills(kind.defName);beast.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,hunter));int k1=Bestiary.Kills(kind.defName);
    var wild=PawnGenerator.GeneratePawn(kind);GenSpawn.Spawn(wild,MlCell(-10,14),map);mlMade.Add(wild);wild.Kill(null);int k2=Bestiary.Kills(kind.defName);
    bool humanNo=!Bestiary.Eligible(human);
    var live=PawnGenerator.GeneratePawn(kind);GenSpawn.Spawn(live,MlCell(-10,16),map);mlMade.Add(live);
    st.kills[kind.defName]=24;var d1=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,hunter);var a1=new object[]{live,d1};HarmonyLib.AccessTools.Method(typeof(BestiaryDamage),"Prefix").Invoke(null,a1);float x24=((DamageInfo)a1[1]).Amount;
    st.kills[kind.defName]=25;var a2=new object[]{live,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,hunter)};HarmonyLib.AccessTools.Method(typeof(BestiaryDamage),"Prefix").Invoke(null,a2);float x25=((DamageInfo)a2[1]).Amount;
    bool texts=!Bestiary.Weakness(kind).NullOrEmpty() && !Bestiary.Drops(kind).NullOrEmpty() && Bestiary.LoreText(kind).Contains("5%");
    Check(k1==k0+1 && k2==k1 && humanNo && Near(x24,10f) && Near(x25,10.5f) && texts && Bestiary.Known(0).Contains(kind.defName) && DefDatabase<MainButtonDef>.GetNamedSilentFail("LP_Bestiary")!=null,
     "[1.136.0] bestiary: player kills of mod creatures count ("+k0+"→"+k1+", wild death "+k2+"), humans aren't tracked, 25 kills = +5% damage ("+x24+" → "+x25+"), entry texts, main button");
    cvSavedKind=kind.defName;cvSavedKills=Bestiary.Kills(kind.defName);}
   // 5. 观察：地图上遇见过的本模组生物也会记下名字（没击杀时只解锁名字）。
   {var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_BlinkDog")??DefDatabase<PawnKindDef>.GetNamed("LP_WinterWolf");var b=PawnGenerator.GeneratePawn(kind);GenSpawn.Spawn(b,MlCell(-6,14),map);mlMade.Add(b);
    Bestiary.Record(b,false);var st=Bestiary.State;
    Check(st.seen.Contains(kind.defName) && Bestiary.Known(0).Contains(kind.defName) && st.cat.ContainsKey(kind.defName),"[1.136.0] bestiary records creatures seen on the map ("+kind.label+", category "+Bestiary.Cats[st.cat[kind.defName]]+")");}
   // 6. DND 角色卡：分页挂在人形上；六维 6 行带调整值、职业行带子职业、强度行带分项来源说明。
   {var p=S2Pawn("LP_Class_Sorcerer",5,Subclass5.ShadowMagic);ClassSystem.AddLevel(Adventurer.Get(p),cleric);GenSpawn.Spawn(p,MlCell(-4,14),map);mlMade.Add(p);
    var secs=DndCard.Build(p);var six=secs.FirstOrDefault(x=>x.title=="六维属性");var cls=secs.FirstOrDefault(x=>x.title=="职业");var pw=secs.FirstOrDefault(x=>x.title.StartsWith("职业强度"));
    bool tab=ThingDefOf.Human.inspectorTabsResolved.Any(t=>t is ITab_DndCard);
    Check(tab && six!=null && six.lines.Count==6 && six.lines.All(l=>l.value.Contains("调整值")) && cls!=null && cls.lines.Any(l=>l.value.Contains("暗影魔法")) && pw!=null && pw.lines.Count>=2 && pw.lines.All(l=>!l.tip.NullOrEmpty()) && secs.Any(x=>x.title=="秘传与湖印") && DndCard.Height(secs)>200f,
     "[1.136.0] DND character sheet tab on humans: 6 scores with modifiers, classes with subclass, "+(pw?.lines.Count??0)+" power lines with breakdowns, arcana & seals");}
  }
  void PrepareConvenienceSave(){var st=Bestiary.State;if(cvSavedKind!=null)st.kills[cvSavedKind]=cvSavedKills=7;}
  void CheckConvenienceAfterLoad(){Check(cvSavedKind==null || Bestiary.Kills(cvSavedKind)==7 && Bestiary.State.cat.ContainsKey(cvSavedKind),"[1.136.0] save/reload keeps bestiary kills ("+(cvSavedKind==null?"-":Bestiary.Kills(cvSavedKind).ToString())+")");}

  // 1.136.0 热修：自测通过后选中一名会治疗伤口的牧师（征召中），截一张能力栏，看自动施放角标有没有挡住图标。
  static int cvFrames;static Pawn cvPawn;
  static bool CaptureAbilityBar(){
   if(cvFrames==0)return false;cvFrames++;var map=Find.CurrentMap;
   try{
    if(cvFrames==10){
     cvPawn=map.mapPawns.FreeColonistsSpawned.FirstOrDefault();Adventurer.TestSuppressed=false;LichMod.Settings.adventurer=true;
     if(cvPawn!=null){var h=Adventurer.Get(cvPawn)??Adventurer.TryAdd(cvPawn,10,true);h.level=10;h.Dirty();if(h.classes==null)h.classes=new ClassData();
      ClassSystem.SetLevel(h,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Cleric"),10);ClassSystem.Sync(h);
      var cure=DefDatabase<AbilityDef>.GetNamed("LP_CureWounds");ClassAutoCast.Set(cvPawn,cure,true);AutoCastPlus.SetUndrafted(cvPawn,cure,true);
      if(cvPawn.drafter!=null)cvPawn.drafter.Drafted=true;Find.Selector.ClearSelection();Find.Selector.Select(cvPawn);}
     Messages.Clear();
    }
    if(cvFrames==40)ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"CV-AbilityBar.png"));
    if(cvFrames>=50){Log.Message("[LICH-UI] ability bar captured");Application.Quit();}
   }catch(Exception e){Log.Error("[LICH-UI] ability bar capture: "+e);Application.Quit();}
   return true;
  }
 }
}
