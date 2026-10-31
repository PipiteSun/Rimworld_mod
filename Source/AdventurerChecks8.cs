using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.75.0 非战斗经验：Test.ps1 -Only Adventurer 的治疗与手术、交易、建筑完工、研究完成、重伤倒地生还部分。由 CheckAdventurer 在同一个 try 里调用；存读档在 PrepareAdventurerSave / CheckAdventurerAfterLoad。
 public partial class LichChecks {
  static bool FailSurgery(Pawn patient,ref bool __result){if(patient!=surgerySubject)return true;__result=true;return false;}
  IntVec3 ExFreeCell(int skip){
   int n=0;foreach(var c in GenRadial.RadialCellsAround(altar.InteractionCell,12f,false))if(c.InBounds(map) && c.Standable(map) && c.GetThingList(map).Count==0 && c.GetEdifice(map)==null && n++>=skip)return c;
   return IntVec3.Invalid;
  }
  void ResetExtra(Hediff_Adventurer h){h.exDay=-1;for(int i=0;i<AdventurerExtraXp.Count;i++){h.exToday[i]=0f;h.exTotal[i]=0f;}h.exDownPower=-1f;h.exSurviveTick=-9999999;}
  void CheckAdventurerExtraXp(){
   var s=LichMod.Settings;float oldMult=s.adventurerNonCombatXp;bool oh=s.adventurerXpHeal,ot=s.adventurerXpTrade,ob=s.adventurerXpBuild,or=s.adventurerXpResearch,os=s.adventurerXpSurvive;
   var spawned=new List<Thing>();
   try{
    s.adventurerNonCombatXp=1f;s.adventurerXpHeal=s.adventurerXpTrade=s.adventurerXpBuild=s.adventurerXpResearch=s.adventurerXpSurvive=true;
    Func<Pawn,float> Scale=p=>Adventurer.XpScale*Talents.XpFactor(Adventurer.Get(p))*ClassSystem.XpFactor(p);
    // 0. 补丁都挂上了
    var targets=new[]{AccessTools.Method(typeof(Recipe_Surgery),"CheckSurgeryFail"),AccessTools.Method(typeof(TendUtility),nameof(TendUtility.DoTend)),AccessTools.Method(typeof(TradeDeal),nameof(TradeDeal.TryExecute)),AccessTools.Method(typeof(Frame),nameof(Frame.CompleteConstruction)),
     AccessTools.Method(typeof(ResearchManager),nameof(ResearchManager.ResearchPerformed)),AccessTools.Method(typeof(ResearchManager),nameof(ResearchManager.FinishProject)),AccessTools.Method(typeof(Pawn_HealthTracker),"MakeDowned"),AccessTools.Method(typeof(Pawn_HealthTracker),"MakeUndowned")};
    var missing=targets.Where(m=>m==null || Harmony.GetPatchInfo(m)?.Owners.Contains("local.lich.phylactery")!=true).Select(m=>m?.Name??"null").ToList();
    Check(missing.Count==0,"1.75.0 non-combat XP: all 8 Harmony targets exist in 1.7 and are patched"+(missing.Count>0?" (missing: "+string.Join(",",missing)+")":""));
    // 1. 手术（走真正的手术流程，成功 / 失败由测试补丁决定）
    var doc=AdvPawn();var hd=Adventurer.Get(doc);ResetExtra(hd);GenSpawn.Spawn(doc,ExFreeCell(0),map);spawned.Add(doc);
    var pat=AdvPawn();GenSpawn.Spawn(pat,ExFreeCell(1),map);spawned.Add(pat);float sc=Scale(doc);
    var recipe=DefDatabase<RecipeDef>.GetNamed("InstallPainstopper");var probe=new Harmony("local.lich.extraxptest");var csf=targets[0];
    float okGot,failGot;surgerySubject=pat;
    try{
     var part=recipe.Worker.GetPartsToApplyOn(pat,recipe).First();
     List<Thing> Ing()=>recipe.ingredients.Select(i=>ThingMaker.MakeThing(i.filter.AllowedThingDefs.First())).ToList();
     probe.Patch(csf,prefix:new HarmonyMethod(typeof(LichChecks),nameof(DeterministicSurgery)));
     var ing=Ing();var bill=new Bill_Medical(recipe,ing);pat.health.surgeryBills.AddBill(bill);float x0=hd.xp;
     recipe.Worker.ApplyOnPawn(pat,part,doc,ing,bill);okGot=hd.xp-x0;pat.health.surgeryBills.Delete(bill);foreach(var t in ing)if(!t.Destroyed)t.Destroy();
     probe.Unpatch(csf,HarmonyPatchType.Prefix,probe.Id);probe.Patch(csf,prefix:new HarmonyMethod(typeof(LichChecks),nameof(FailSurgery)));
     var ing2=Ing();var bill2=new Bill_Medical(recipe,ing2);pat.health.surgeryBills.AddBill(bill2);x0=hd.xp;
     recipe.Worker.ApplyOnPawn(pat,part,doc,ing2,bill2);failGot=hd.xp-x0;pat.health.surgeryBills.Delete(bill2);foreach(var t in ing2)if(!t.Destroyed)t.Destroy();
    }finally{surgerySubject=null;probe.Unpatch(csf,HarmonyPatchType.Prefix,probe.Id);}
    Check(Near(okGot,30f*sc) && Near(failGot,0f) && Near(AdventurerExtraXp.Today(hd,AdventurerExtraXp.Heal),30f),"successful surgery → surgeon +30 (×"+sc.ToString("0.##")+" = "+okGot+"); failed surgery → 0");
    // 救治：6 小时内会失血而死的才算（真正调用 TendUtility.DoTend）
    var cut=DefDatabase<HediffDef>.GetNamed("Cut");var torso=pat.RaceProps.body.corePart;
    var light=HediffMaker.MakeHediff(cut,pat,torso);light.Severity=1f;pat.health.AddHediff(light,torso);bool lightCrit=AdventurerExtraXp.Critical(pat);
    float t0=hd.xp;TendUtility.DoTend(doc,pat,null);float lightGot=hd.xp-t0;
    var bl=HediffMaker.MakeHediff(HediffDefOf.BloodLoss,pat);bl.Severity=.9f;pat.health.AddHediff(bl);
    foreach(var bp in pat.health.hediffSet.GetNotMissingParts().Where(x=>x.depth==BodyPartDepth.Outside && x.def.hitPoints>=20).Take(4)){var w=HediffMaker.MakeHediff(cut,pat,bp);w.Severity=8f;pat.health.AddHediff(w,bp);}
    int ticks=HealthUtility.TicksUntilDeathDueToBloodLoss(pat);bool crit=AdventurerExtraXp.Critical(pat);t0=hd.xp;TendUtility.DoTend(doc,pat,null);float critGot=hd.xp-t0;
    Check(!lightCrit && Near(lightGot,0f) && crit && Near(critGot,15f*sc),"tending a light cut gives nothing; tending a patient "+(ticks/2500f).ToString("0.#")+" h from bleeding out → +15");
    // 上限 150
    hd.exToday[AdventurerExtraXp.Heal]=140f;float capBase=AdventurerExtraXp.Surgery(doc,pat,false)/sc;float capped=AdventurerExtraXp.Surgery(doc,pat,false);
    int day=Feats.LocalDay(doc);hd.exDay=day-1;float nextDay=AdventurerExtraXp.Surgery(doc,pat,false)/sc;
    Check(Near(capBase,10f) && capped==0f && Near(nextDay,30f) && Near(hd.exToday[AdventurerExtraXp.Heal],30f),"healing XP stops at 150 a day (140 + 30 → +10, then 0); a new day resets it");
    // 2. 交易
    var neg=AdvPawn();var hn=Adventurer.Get(neg);ResetExtra(hn);float sn=Scale(neg);
    float tr1=AdventurerExtraXp.Traded(neg,2500f)/sn;hn.exToday[AdventurerExtraXp.Trade]=90f;float tr2=AdventurerExtraXp.Traded(neg,2500f)/sn;float tr3=AdventurerExtraXp.Traded(neg,500f);
    Check(Near(tr1,25f) && Near(tr2,10f) && tr3==0f && Near(AdventurerExtraXp.DealValue(null),0f),"trade: 2500 silver of goods → +25; cap 100 a day (90 → +10, then 0)");
    // 3. 建筑完工（真正的框架完工）
    var bw=AdvPawn();var hw=Adventurer.Get(bw);ResetExtra(hw);GenSpawn.Spawn(bw,ExFreeCell(2),map);spawned.Add(bw);float sw=Scale(bw);
    var cell=ExFreeCell(3);var frame=(Frame)ThingMaker.MakeThing(ThingDefOf.Wall.frameDef,ThingDefOf.Steel);frame.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(frame,cell,map);
    float work=frame.WorkToBuild;float b0=hw.xp;frame.CompleteConstruction(bw);float wallGot=(hw.xp-b0)/sw;var wall=cell.GetEdifice(map);if(wall!=null)spawned.Add(wall);
    float big=AdventurerExtraXp.Built(bw,5000f)/sw;hw.exToday[AdventurerExtraXp.Build]=99f;float bcap=AdventurerExtraXp.Built(bw,5000f)/sw;
    Check(wall!=null && wall.def==ThingDefOf.Wall && Near(wallGot,AdventurerExtraXp.BuildBase(work)) && wallGot>=2f && Near(big,5f) && Near(bcap,1f) && AdventurerExtraXp.BuildBase(0f)==0f,"construction: a steel wall ("+work+" work) → +"+wallGot+" (minimum 2); 5000 work → +5; cap 100 a day (99 → +1)");
    // 4. 研究完成：按出力比例
    var proj=DefDatabase<ResearchProjectDef>.AllDefsListForReading.First(r=>!r.IsFinished && r.Cost>0f);var st=ResearchShares.State;
    var ra=AdvPawn();var rb=AdvPawn();var foe=AdvPawn(null,AdvEnemy);var hra=Adventurer.Get(ra);var hrb=Adventurer.Get(rb);float fx0=Adventurer.Get(foe)?.xp??0f;
    st.Add(proj,ra,300f);st.Add(proj,rb,100f);st.Add(proj,foe,100f);st.Add(proj,ra,0f);
    float a0=hra.xp,c0=hrb.xp,pool=proj.Cost*AdventurerExtraXp.ResearchShare;int paid=AdventurerExtraXp.ResearchFinished(proj);
    Check(paid==2 && Near((hra.xp-a0)/Scale(ra),pool*.6f) && Near((hrb.xp-c0)/Scale(rb),pool*.2f) && Near(Adventurer.Get(foe)?.xp??0f,fx0) && Near(st.Of(proj,ra),0f) && AdventurerExtraXp.ResearchFinished(proj)==0,"research "+proj.defName+" (cost "+proj.Cost+"): 5% = "+pool+" split 300/100/100 → colonists get 60% / 20%, the non-colonist's share is not paid; shares are cleared");
    // 5. 重伤倒地生还
    var sv=AdvPawn();var hs=Adventurer.Get(sv);ResetExtra(hs);float ss=Scale(sv);var enemy=AdvPawn(null,AdvEnemy);float cp=enemy.kindDef.combatPower;
    AdventurerExtraXp.Downed(sv,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,enemy));float rec=hs.exDownPower;float sv1=AdventurerExtraXp.Undowned(sv)/ss;
    AdventurerExtraXp.Downed(sv,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,enemy));float sv2=AdventurerExtraXp.Undowned(sv);
    hs.exSurviveTick=Find.TickManager.TicksGame-AdventurerExtraXp.SurviveCooldown-1;AdventurerExtraXp.Downed(sv,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,enemy));float sv3=AdventurerExtraXp.Undowned(sv)/ss;
    Check(Near(rec,cp) && Near(sv1,50f+.5f*cp) && sv2==0f && Near(sv3,50f+.5f*cp),"downed by a hostile (combat power "+cp+") then up again → +"+sv1+"; again within 3 days → 0; after 3 days → paid again");
    hs.exSurviveTick=-9999999;var mate=AdvPawn();
    AdventurerExtraXp.Downed(sv,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,sv));float self=AdventurerExtraXp.Undowned(sv);
    AdventurerExtraXp.Downed(sv,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,mate));float friendly=AdventurerExtraXp.Undowned(sv);
    AdventurerExtraXp.Downed(sv,null);float noDinfo=AdventurerExtraXp.Undowned(sv);
    Check(self==0f && friendly==0f && noDinfo==0f && hs.exDownPower<0f,"self-inflicted, friendly-fire and no-attacker downs give nothing");
    // 6. 开关、倍率
    s.adventurerXpHeal=false;float offH=AdventurerExtraXp.Surgery(doc,pat,false);s.adventurerXpHeal=true;
    s.adventurerXpTrade=false;float offT=AdventurerExtraXp.Traded(neg,1000f);s.adventurerXpTrade=true;
    s.adventurerXpBuild=false;float offB=AdventurerExtraXp.Built(bw,5000f);s.adventurerXpBuild=true;
    s.adventurerXpResearch=false;st.Add(proj,ra,100f);int offR=AdventurerExtraXp.ResearchFinished(proj);s.adventurerXpResearch=true;
    s.adventurerXpSurvive=false;AdventurerExtraXp.Downed(sv,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,enemy));float offS=AdventurerExtraXp.Undowned(sv);s.adventurerXpSurvive=true;
    string offTip=null;s.adventurerXpTrade=false;offTip=ITab_Adventurer.XpSourcesTip(hn);s.adventurerXpTrade=true;
    Check(offH==0f && offT==0f && offB==0f && offR==0 && offS==0f && offTip.Contains("交易：已关闭"),"each of the five toggles off → 0 XP (tooltip says 交易：已关闭)");
    ResetExtra(hn);s.adventurerNonCombatXp=2f;float dbl=AdventurerExtraXp.Traded(neg,1000f)/sn;float dToday=hn.exToday[AdventurerExtraXp.Trade];
    s.adventurerNonCombatXp=0f;float zero=AdventurerExtraXp.Traded(neg,1000f);string zeroTip=ITab_Adventurer.XpSourcesTip(hn);s.adventurerNonCombatXp=1f;
    Check(Near(dbl,20f) && Near(dToday,10f) && zero==0f && zeroTip.Contains("研究完成：已关闭"),"非战斗经验倍率 ×2 → 1000 silver gives +20 (daily cap counts the base 10); ×0 turns all five off");
    // 悬停说明
    hd.exDay=Feats.LocalDay(doc);hd.exToday[AdventurerExtraXp.Heal]=45f;string tip=ITab_Adventurer.XpSourcesTip(hd);
    Check(AdventurerExtraXp.Names.All(n=>tip.Contains("· "+n+"：")) && tip.Contains("今日 45 / 150") && !tip.Contains("已关闭") && !tip.Contains("d20") && !tip.Contains("检定"),"XP tooltip has a line for each of the five sources (healing today 45 / 150)");
   }finally{
    s.adventurerNonCombatXp=oldMult;s.adventurerXpHeal=oh;s.adventurerXpTrade=ot;s.adventurerXpBuild=ob;s.adventurerXpResearch=or;s.adventurerXpSurvive=os;
    foreach(var t in spawned)if(!t.Destroyed)t.Destroy();
   }
  }
  static ResearchProjectDef exSavedProj;
  void PrepareExtraXpSave(Pawn p,Hediff_Adventurer h){
   h.exDay=4321;h.exToday=new[]{1f,2f,3f,4f,5f};h.exTotal=new[]{10f,20f,30f,40f,50f};h.exDownPower=77f;h.exSurviveTick=-5;
   exSavedProj=DefDatabase<ResearchProjectDef>.AllDefsListForReading.First(r=>!r.IsFinished && r.Cost>0f);ResearchShares.State.Add(exSavedProj,p,123f);
  }
  void CheckExtraXpAfterLoad(Pawn p,Hediff_Adventurer h){
   var st=ResearchShares.State;
   Check(h.exDay==4321 && string.Join(",",h.exToday)=="1,2,3,4,5" && string.Join(",",h.exTotal)=="10,20,30,40,50" && Near(h.exDownPower,77f) && h.exSurviveTick==-5 && st!=null && Near(st.Of(exSavedProj,p),123f),"non-combat XP counters, pending down record and research shares survive save/load");
   st?.shares.RemoveAll(e=>e.pawn==p);
  }
 }
}
