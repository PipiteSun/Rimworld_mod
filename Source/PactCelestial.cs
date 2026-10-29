using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.55.0 圣坛上的契约选项（规格第四节、1.6 解约第 1 条）：破契仪式、祈唤天界援军（狄瓦援军 / 守护天使 / 行星使降临）。
 // 全部挂在 Building_FaithAltar.ExtraGizmos（成神线的入口），不改 Faith.cs。另有坠落天使和堕天使的每日结算。
 public static class PactCelestial {
  public const float BreakCost=60f,BreakChance=.5f,BreakTaint=.3f,DevaCost=40f,DevaMin=50f,GuardianCost=90f,GuardianMin=90f,PlanetarCost=100f,PlanetarMin=99.99f,GuestFavor=30f,MantleChance=.1f;
  public const int DevaDays=2,PlanetarDays=1,GuestDays=10,TaintedLimit=3;
  public const float GuardianLeaveDaily=.05f,FallSeason=.15f,GuestFallDaily=.1f,GuestFallRadius=10f;
  public static float? TestRoll;public static float Roll=>TestRoll??Rand.Value;
  public static LP_DeityDef GoodPatron=>FaithUtility.Patron?.Good==true?FaithUtility.Patron:null;
  public static int Tainted(Map map=null)=>(map==null?AbyssBinding.PlayerPawns.Where(p=>p.RaceProps.Humanlike):map.mapPawns.FreeColonistsSpawned).Count(p=>AbyssTaint.Severity(p)>=AbyssTaint.Mark);
  public static bool HasGuardian()=>AbyssBinding.PlayerPawns.Any(p=>PactRaces.IsDeva(p) && Outsiders.Bound(p));

  // ---------- 破契仪式：60 神眷，50% 成功；失败签约人腐化 +30%、债务 +1 ----------
  public static string CannotBreak(PlanarPact p){
   var god=FaithUtility.Patron;if(god==null)return "殖民地没有守护神";if(god.Evil)return god.label+"不管这种事（恶神）";
   if(FaithUtility.GetFavor(god)<BreakCost)return god.label+"的神眷不足 "+BreakCost.ToString("0");
   return null;
  }
  public static bool Break(PlanarPact p){
   if(CannotBreak(p)!=null)return false;var god=FaithUtility.Patron;FaithUtility.AddFavor(god,-BreakCost,p.signer.LabelShort+"的破契仪式");
   if(p.signer.Spawned)FleckMaker.Static(p.signer.Position,p.signer.Map,FleckDefOf.PsycastAreaEffect,2.5f);
   if(Roll<BreakChance){Pacts.Unbind(p,god.label+"的破契仪式");return true;}
   AbyssTaint.Add(p.signer,BreakTaint,"破契仪式失败");Pacts.AddDebt(p,1,"破契仪式失败");
   Find.LetterStack.ReceiveLetter("破契仪式失败",god.label+"的光没能烧断"+p.patron.label+"的契约。契约反噬："+p.signer.LabelShort+"深渊腐化 +30%，债务 +1。",LetterDefOf.NegativeEvent,p.signer);
   return false;
  }

  // ---------- 祈唤天界援军 ----------
  public static string CannotInvoke(string what,Map map){
   if(!Pacts.Celestial)return "设置里关掉了天界祈唤";var god=GoodPatron;if(god==null)return "要善神做守护神";
   if(map==null || !map.mapPawns.FreeColonistsSpawned.Any(p=>FaithUtility.IsDevoteeOf(p,god)))return "地图上没有"+god.label+"的信徒";
   float f=FaithUtility.GetFavor(god);
   switch(what){
    case "deva":return f<DevaMin?"神眷要 ≥ "+DevaMin.ToString("0"):null;
    case "guardian":if(f<GuardianMin)return "神眷要 ≥ "+GuardianMin.ToString("0");if(Tainted()>0)return "殖民地里有人腐化到了恶魔之印（60%）";if(HasGuardian())return "已经有一位狄瓦束缚者了";if(AbyssBinding.BoundCount()>=AbyssBinding.BoundCap())return "束缚者已满";return null;
    case "planetar":{if(f<PlanetarMin)return "神眷要满 100";var c=Pacts.Comp;if(c!=null && Pacts.Now<c.planetarReadyTick)return "还要 "+(c.planetarReadyTick-Pacts.Now).ToStringTicksToPeriod()+"才能再请行星使";return null;}
   }
   return null;
  }
  public static List<Pawn> Invoke(string what,Building altar){
   var map=altar.Map;var god=GoodPatron;var made=new List<Pawn>();if(CannotInvoke(what,map)!=null)return made;
   var cell=altar.InteractionCell;var c=Pacts.Comp;
   switch(what){
    case "deva":{FaithUtility.AddFavor(god,-DevaCost,"祈唤狄瓦援军");var p=Make("LP_CE_Deva",cell,map);if(p!=null){AbyssBinding.MakeThrall(p,DevaDays*GenDate.TicksPerDay);made.Add(p);}break;}
    case "guardian":{FaithUtility.AddFavor(god,-GuardianCost,"祈唤守护天使");var p=Make("LP_CE_Deva",cell,map);if(p!=null){var lead=map.mapPawns.FreeColonistsSpawned.Where(x=>FaithUtility.IsDevoteeOf(x,god)).OrderByDescending(x=>x.skills?.GetSkill(SkillDefOf.Social)?.Level??0).FirstOrDefault();AbyssBinding.Bind(p,lead,100f);Items.Give(p,"LP_CelestialGuardian");AbyssBinding.SetLevel(p,11,true);made.Add(p);}break;}
    case "planetar":{FaithUtility.AddFavor(god,-PlanetarCost,"祈唤行星使");if(c!=null)c.planetarReadyTick=Pacts.Now+GenDate.TicksPerYear;var p=Make("LP_CE_Planetar",cell,map);if(p!=null){AbyssBinding.MakeThrall(p,PlanetarDays*GenDate.TicksPerDay);made.Add(p);}break;}
   }
   if(made.Count>0){FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,4f);Find.LetterStack.ReceiveLetter("天界援军",god.label+"回应了祈唤："+made[0].LabelShort+"（"+made[0].genes?.Xenotype?.label+"）从光里降到圣坛前。"+(what=="guardian"?"守护天使会留下来，不会挣脱束缚；但殖民地里腐化到恶魔之印的人多起来（≥3）时，它每天有 5% 会失望离开。":"它会听候差遣 "+(what=="deva"?DevaDays:PlanetarDays)+" 天。"),LetterDefOf.PositiveEvent,made[0]);}
   return made;
  }
  static Pawn Make(string kind,IntVec3 cell,Map map){var p=AbyssRaces.Make(kind,Faction.OfPlayer,map.Tile);if(p==null)return null;GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(cell,map,2),map);p.workSettings?.EnableAndInitialize();return p;}

  // ---------- 坠落天使（5.6） ----------
  public static Pawn FallenDeva(Map map){
   if(!DropCellFinder.TryFindRaidDropCenterClose(out var cell,map))cell=DropCellFinder.RandomDropSpot(map);
   var p=AbyssRaces.Make("LP_CE_Deva",Faction.OfPlayer,map.Tile);if(p==null)return null;
   var parts=p.health.hediffSet.GetNotMissingParts().Where(x=>x.depth==BodyPartDepth.Outside && x.coverageAbs>0f).InRandomOrder().Take(4).ToList();
   foreach(var part in parts){var inj=HediffMaker.MakeHediff(HediffDefOf.Cut,p,part);inj.Severity=Mathf.Min(6f,part.def.GetMaxHealth(p)*.4f);p.health.AddHediff(inj,part);}
   Items.Give(p,"LP_DevaStupor");Items.Give(p,"LP_FallenDevaGuest");
   DropPodUtility.DropThingsNear(cell,map,new List<Thing>{p},110,false,false,true,false,true,null);
   Find.LetterStack.ReceiveLetter("坠落天使","一道光从天上斜斜地砸下来：一名狄瓦重伤坠地，翅膀折断，昏迷不醒。\n\n把它的伤都包扎好，它就会醒来，作为客人留 "+GuestDays+" 天（可以征召）；离开时守护神神眷 +"+GuestFavor.ToString("0")+"，有 10% 会留下一件天界羽披。\n\n小心：腐化到恶魔之印的人靠近它（10 格内）时，它每天有 10% 堕落成敌对的艾林魔。",LetterDefOf.PositiveEvent,new TargetInfo(cell,map));
   return p;
  }
  public static void GuestRescued(Pawn p){
   Items.Remove(p,"LP_DevaStupor");AbyssBinding.MakeThrall(p,GuestDays*GenDate.TicksPerDay);
   Messages.Message(p.LabelShort+"醒了过来，向殖民地道谢：它会作为客人留 "+GuestDays+" 天。",p,MessageTypeDefOf.PositiveEvent,false);
  }
  public static void GuestLeaves(Pawn p){
   var god=GoodPatron;if(god!=null)FaithUtility.AddFavor(god,GuestFavor,p.LabelShort+"回到了天界");
   if(Roll<MantleChance && PactRaces.Mantle!=null && p.MapHeld!=null){var m=ThingMaker.MakeThing(PactRaces.Mantle);GenPlace.TryPlaceThing(m,p.PositionHeld,p.MapHeld,ThingPlaceMode.Near);Messages.Message(p.LabelShort+"临走时留下了一件天界羽披。",m,MessageTypeDefOf.PositiveEvent,false);}
  }
  public static Pawn Fall(Pawn deva,bool hostile){
   if(PactRaces.ErinyesXeno==null || deva==null || deva.Dead)return null;
   AbyssRaces.ConvertTo(deva,PactRaces.ErinyesXeno);Items.Remove(deva,"LP_CelestialGuardian");Items.Remove(deva,"LP_FallenDevaGuest");
   if(hostile){
    var t=deva.health.hediffSet.hediffs.OfType<Hediff_AbyssThrall>().FirstOrDefault();if(t!=null){t.breaking=true;deva.health.RemoveHediff(t);}
    var f=PactEvents.Legion();if(f!=null){deva.SetFaction(f);if(deva.Spawned && !deva.Downed)LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false,false,false),deva.Map,new List<Pawn>{deva});}
   }
   Find.LetterStack.ReceiveLetter("堕天使："+deva.LabelShort,deva.LabelShort+"身边的深渊气息太浓了：白金的羽毛一片片焦黑，"+deva.LabelShort+"成了一名艾林魔"+(hostile?"，转身朝殖民地拔出了刀。":"。它仍然听从束缚，但已经不属于天界了。"),hostile?LetterDefOf.ThreatBig:LetterDefOf.NegativeEvent,deva);
   return deva;
  }
  // 每天：守护天使失望离开、堕天使、坠落天使堕落。
  public static void Daily(int now){
   if(Find.Maps==null)return;var pawns=AbyssBinding.PlayerPawns.Where(p=>PactRaces.IsDeva(p)).ToList();if(pawns.Count==0)return;
   int tainted=Tainted();float fallDaily=1f-Mathf.Pow(1f-FallSeason,1f/15f);
   foreach(var d in pawns){
    if(d.Dead || d.Destroyed)continue;
    if(Items.Has(d,"LP_FallenDevaGuest")){if(d.Spawned && d.Map.mapPawns.FreeColonistsSpawned.Any(c=>c!=d && AbyssTaint.Severity(c)>=AbyssTaint.Mark && c.Position.InHorDistOf(d.Position,GuestFallRadius)) && Roll<GuestFallDaily)Fall(d,true);continue;}
    if(!Outsiders.Bound(d))continue;
    if(Items.Has(d,"LP_CelestialGuardian") && tainted>=TaintedLimit && Roll<GuardianLeaveDaily){PactRaces.LetLeaveCelestial(d);continue;}
    var b=AbyssBinding.Binding(d);bool masterFallen=b?.binder!=null && !b.binder.Dead && AbyssTaint.Severity(b.binder)>=AbyssTaint.Mark;
    if((masterFallen || tainted>=TaintedLimit) && Roll<fallDaily)Fall(d,false);
   }
  }
  static IEnumerable<Gizmo> AltarGizmos(Building_FaithAltar altar){
   var c=Pacts.Comp;var map=altar.Map;
   if(c!=null)foreach(var p in c.Active.Where(x=>x.signer?.MapHeld==map).ToList()){
    var pp=p;var why=CannotBreak(p);
    var cmd=new Command_Action{defaultLabel="破契仪式："+p.signer.LabelShort,defaultDesc="向守护神祈求斩断"+p.signer.LabelShort+"和"+p.patron.label+"的契约：花 "+BreakCost.ToString("0")+" 神眷，50% 成功。失败："+p.signer.LabelShort+"深渊腐化 +30%，债务 +1。",icon=p.patron.Icon,action=()=>Break(pp)};
    if(why!=null)cmd.Disable(why);yield return cmd;
   }
   if(!Pacts.Celestial || GoodPatron==null)yield break;
   foreach(var (key,label,cost,desc) in new[]{("deva","狄瓦援军",DevaCost,"神眷 ≥ 50 时可用：1 名狄瓦作为仆从听候差遣 2 天。"),("guardian","守护天使",GuardianCost,"神眷 ≥ 90、殖民地里没人腐化到恶魔之印、还没有狄瓦束缚者时可用：1 名狄瓦束缚者，服从度不会下降。之后腐化到恶魔之印的人 ≥3 时，它每天有 5% 失望离开。"),("planetar","行星使降临",PlanetarCost,"神眷满 100 时可用，一年一次：1 名行星使作为仆从降临 1 天（传奇抗性 2，天界之怒，驱散）。")}){
    var k=key;var cmd=new Command_Action{defaultLabel="祈唤天界援军："+label+"（"+cost.ToString("0")+" 神眷）",defaultDesc=desc,icon=ContentFinder<Texture2D>.Get("PlanarPact/Abilities/"+(key=="planetar"?"Wrath":key=="guardian"?"HealingTouch":"Deva"),false)??BaseContent.BadTex,action=()=>Invoke(k,altar)};
    var why=CannotInvoke(key,map);if(why!=null)cmd.Disable(why);yield return cmd;
   }
  }
  [StaticConstructorOnStartup]
  public static class Startup {static Startup(){Building_FaithAltar.ExtraGizmos.Add(AltarGizmos);}}
 }
 public class Hediff_FallenDevaGuest : HediffWithComps {
  int acc;bool rescued,done;
  public override bool Visible=>true;
  public override string LabelInBrackets=>!rescued?"等待救治":done?null:"客人";
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;if(pawn.Dead)return;
   if(!rescued){if(!pawn.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury i && !i.IsPermanent() && i.TendableNow())){rescued=true;var p=pawn;Fiends.Defer(()=>PactCelestial.GuestRescued(p));}return;}
   if(done)return;var t=pawn.health.hediffSet.hediffs.OfType<Hediff_AbyssThrall>().FirstOrDefault();
   if(t==null || t.ticksLeft<=GenDate.TicksPerHour*2){done=true;PactCelestial.GuestLeaves(pawn);}
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref rescued,"lpRescued");Scribe_Values.Look(ref done,"lpDone");}
 }
}
