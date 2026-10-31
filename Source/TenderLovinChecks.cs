using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.96.0 温存（Test.ps1 -Only TenderLovin；-Highmate 另加原版异种扩展·优侣种，查自动补能力和欢爱依赖需求）。
 public partial class LichChecks {
  Pawn TlPawn(Gender g){
   var req=new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,forceGenerateNewPawn:true,fixedGender:g,fixedBiologicalAge:25f,fixedChronologicalAge:25f,canGeneratePawnRelations:false);
   var p=PawnGenerator.GeneratePawn(req);
   // 去掉所有特性：无性恋、毒舌、丑陋之类会让随机出来的人好感低于 0，检查就不稳定
   foreach(var t in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(t);
   if(p.InMentalState)p.MentalState.RecoverFromState();
   return p;
  }
  static string tlOldSaveId;
  // 旧档：存档前只有放荡交欢、没有温存的人，读档后补上温存
  void PrepareTenderLovinSave(){
   var vre=DefDatabase<AbilityDef>.GetNamedSilentFail(TenderLovin.VreAbility);if(vre==null)return;
   var p=TlPawn(Gender.Female);p.abilities.GainAbility(vre);p.abilities.abilities.RemoveAll(a=>a.def==TenderLovin.Def);
   GenSpawn.Spawn(p,ScCell(8,-8),map);tlOldSaveId=p.ThingID;
  }
  void CheckTenderLovinAfterLoad(){
   if(tlOldSaveId==null)return;var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==tlOldSaveId);
   Check(p!=null && TenderLovin.HasVre(p) && p.abilities.GetAbility(TenderLovin.Def)!=null,"old save: a pawn with only 放荡交欢 gets 温存 on load");
   if(p!=null && !p.Destroyed)p.Destroy();
  }
  static string Rels(Pawn p)=>string.Join(",",p.relations.DirectRelations.Select(r=>r.def.defName+":"+r.otherPawn.ThingID).OrderBy(s=>s));
  void CheckTenderLovin(){
   var made=new List<Pawn>();bool oldPreg=TenderLovin.TestNoPregnancy;TenderLovin.TestNoPregnancy=true;
   try{
    var vre=DefDatabase<AbilityDef>.GetNamedSilentFail(TenderLovin.VreAbility);
    // 1. 能力定义、私有字段反射都在；没有放荡交欢的人不会被补上温存
    var plain=TlPawn(Gender.Female);made.Add(plain);TenderLovin.Sync(plain);
    Check(TenderLovin.Def!=null && TenderLovin.Def.comps.Any(c=>c is CompProperties_TenderLovin) && TenderLovin.Job!=null && TenderLovin.PartnerJob!=null && plain.abilities.GetAbility(TenderLovin.Def)==null,"温存 ability/job defs load; a pawn without 放荡交欢 does not get it (VRE Highmate "+(vre==null?"not loaded":"loaded")+")");
    // 2. 有优侣种时：得到放荡交欢就自动补上温存，失去就一起去掉
    if(vre!=null){
     var hm=TlPawn(Gender.Female);made.Add(hm);hm.abilities.GainAbility(vre);bool got=hm.abilities.GetAbility(TenderLovin.Def)!=null;
     hm.abilities.RemoveAbility(vre);bool gone=hm.abilities.GetAbility(TenderLovin.Def)==null;
     var hx=DefDatabase<XenotypeDef>.GetNamedSilentFail("Highmate");var born=hx==null?null:PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forceGenerateNewPawn:true,fixedBiologicalAge:25f,fixedChronologicalAge:25f,forcedXenotype:hx));if(born!=null)made.Add(born);
     bool bornBoth=born==null || !TenderLovin.HasVre(born) || born.abilities.GetAbility(TenderLovin.Def)!=null;
     // 基因途径：加上极高性欲（带放荡交欢）也会补上温存，去掉基因一起去掉
     var gd=DefDatabase<GeneDef>.GetNamedSilentFail("VRE_Libido_VeryHigh");
     if(gd!=null){var gp=TlPawn(Gender.Female);made.Add(gp);var gene=gp.genes.AddGene(gd,true);bool gBoth=TenderLovin.HasVre(gp) && gp.abilities.GetAbility(TenderLovin.Def)!=null;gp.genes.RemoveGene(gene);bool gGone=!TenderLovin.HasVre(gp) && gp.abilities.GetAbility(TenderLovin.Def)==null;
      Check(gBoth && gGone,"very-high-libido gene grants 放荡交欢 and 温存 together, removing the gene removes both");}
     Check(got && gone && bornBoth,"gaining 放荡交欢 adds 温存, losing it removes 温存; a generated Highmate"+(born!=null && TenderLovin.HasVre(born)?" has both":" (no VRE ability on this one)"));
    }
    // 3. 能不能用：敌人、未成年、无性恋、血亲、好感低于 0 都不行；同派系成年人可以
    var a=TlPawn(Gender.Female);var b=TlPawn(Gender.Male);var c=TlPawn(Gender.Male);var d=TlPawn(Gender.Female);made.AddRange(new[]{a,b,c,d});
    var kid=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forceGenerateNewPawn:true,fixedBiologicalAge:12f,fixedChronologicalAge:12f,developmentalStages:DevelopmentalStage.Child));made.Add(kid);
    var ace=TlPawn(Gender.Male);ace.story.traits.GainTrait(new Trait(TraitDefOf.Asexual));made.Add(ace);
    var sib=TlPawn(Gender.Male);sib.relations.AddDirectRelation(PawnRelationDefOf.Parent,a);made.Add(sib);
    a.relations.AddDirectRelation(PawnRelationDefOf.Lover,c);b.relations.AddDirectRelation(PawnRelationDefOf.Spouse,d);
    Check(TenderLovin.Reason(a,b)==null && TenderLovin.Reason(a,kid)!=null && TenderLovin.Reason(a,ace)!=null && TenderLovin.Reason(a,sib)!=null && TenderLovin.Reason(a,a)!=null,"targets: adult same-faction ok ("+(TenderLovin.Reason(a,b)??"ok")+"); child ("+TenderLovin.Reason(a,kid)+"), asexual ("+TenderLovin.Reason(a,ace)+"), family ("+TenderLovin.Reason(a,sib)+") and self refused");
    // 4. 实际施放：a（有恋人 c）对 b（有配偶 d）施放，走完原版时长；双方得到滚床单心情，四个人的关系一条不变
    var ab=a.abilities.GetAbility(TenderLovin.Def);if(ab==null){a.abilities.GainAbility(TenderLovin.Def);ab=a.abilities.GetAbility(TenderLovin.Def);}
    var cell=ScCell(-8,-8);GenSpawn.Spawn(a,cell,map);GenSpawn.Spawn(b,NextTo(a),map);
    string ra=Rels(a),rb=Rels(b),rc=Rels(c),rd=Rels(d);int tick0=Find.TickManager.TicksGame;
    a.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.GotSomeLovin);b.needs.mood.thoughts.memories.RemoveMemoriesOfDef(ThoughtDefOf.GotSomeLovin);
    bool canCast=ab.CanApplyOn(new LocalTargetInfo(b));ab.Activate(new LocalTargetInfo(b),new LocalTargetInfo(b));
    bool started=a.CurJobDef==TenderLovin.Job;bool partnerJoined=false;
    for(int i=0;i<3200 && a.CurJobDef==TenderLovin.Job;i++){Find.TickManager.DoSingleTick();if(b.CurJobDef==TenderLovin.PartnerJob)partnerJoined=true;}
    var ma=a.needs.mood.thoughts.memories.Memories.FirstOrDefault(m=>m.def==ThoughtDefOf.GotSomeLovin);var mb=b.needs.mood.thoughts.memories.Memories.FirstOrDefault(m=>m.def==ThoughtDefOf.GotSomeLovin);
    Check(canCast && started && partnerJoined && ma!=null && mb!=null && ma.otherPawn==b && mb.otherPawn==a && ma.MoodOffset()>0,"温存 runs as a job with the partner joining; both get 滚床单 ("+(ma==null?"none":"+"+ma.MoodOffset().ToString("0"))+") after "+(Find.TickManager.TicksGame-tick0)+" ticks");
    Check(Rels(a)==ra && Rels(b)==rb && Rels(c)==rc && Rels(d)==rd && a.relations.DirectRelationExists(PawnRelationDefOf.Lover,c) && b.relations.DirectRelationExists(PawnRelationDefOf.Spouse,d) && b.CurJobDef!=TenderLovin.PartnerJob,"lover a–c and spouses b–d stay intact; partner job ended");
    Check(a.mindState.canLovinTick>Find.TickManager.TicksGame && b.mindState.canLovinTick>Find.TickManager.TicksGame && ab.OnCooldown,"next-lovin timer reset for both and 温存 is on cooldown");
    // 5. 有优侣种的欢爱依赖需求时会被补满（挂在原版下次亲热计时上）
    var needDef=DefDatabase<NeedDef>.GetNamedSilentFail("VRE_Lovin");
    if(needDef!=null){
     var e=TlPawn(Gender.Female);var f=TlPawn(Gender.Male);made.Add(e);made.Add(f);
     var dep=DefDatabase<GeneDef>.GetNamedSilentFail("VRE_LovinDependency");if(dep!=null)e.genes.AddGene(dep,true);
     var need=e.needs.TryGetNeed(needDef);if(need!=null)need.CurLevel=.1f;
     TenderLovin.Outcome(e,f);
     Check(need!=null && need.CurLevel>.99f,"Highmate lovin' dependency need refilled by 温存 ("+(need==null?"no need":need.CurLevel.ToString("0.##"))+")");
    }
   }finally{
    TenderLovin.TestNoPregnancy=oldPreg;
    foreach(var p in made)if(!p.Destroyed)p.Destroy();
   }
  }
 }
}
