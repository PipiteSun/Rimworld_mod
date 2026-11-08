using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.124.0 奥能动力甲：Test.ps1 -Only PowerArmor。
 public partial class LichChecks {
  static int paSaveId=-1;
  Pawn PaFoe(IntVec3 c){
   var faction=Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f=>!f.IsPlayer && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer))??Find.FactionManager.OfAncientsHostile;
   var p=Person();p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();p.SetFactionDirect(faction);GenSpawn.Spawn(p,c,map);p.stances?.stunner?.StopStun();return p;
  }
  static Apparel PaWear(Pawn p,string def){var a=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(def));p.apparel.Wear(a,false);return a;}
  void CheckPowerArmor(){
   Func<string,ThingDef> D=n=>DefDatabase<ThingDef>.GetNamedSilentFail(n);Func<string,ResearchProjectDef> R=n=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail(n);
   var made=new List<Thing>();
   try{
    // 1. 研究：DND 页 (4,3.5) → (5,3.5) → (6,3.5)，前置接骑士锻造 / 传奇战甲
    var tab=DefDatabase<ResearchTabDef>.GetNamed(ResearchTab.Tab);var r1=R("LP_Research_ArcanePowerArmor");var r2=R("LP_Research_ArcaneKnightArmor");var r3=R("LP_Research_OathBastion");
    Check(r1!=null && r2!=null && r3!=null && r1.tab==tab && r2.tab==tab && r3.tab==tab && Near(r1.ResearchViewX,4f) && Near(r2.ResearchViewX,5f) && Near(r3.ResearchViewX,6f) && Near(r1.ResearchViewY,3.5f) && Near(r2.ResearchViewY,3.5f) && Near(r3.ResearchViewY,3.5f)
     && r1.prerequisites.Any(p=>p.defName=="LP_Research_KnightForging") && r2.prerequisites.Contains(r1) && r2.prerequisites.Any(p=>p.defName=="LP_Research_LegendaryArmor") && r3.prerequisites.Contains(r2),"power armor research 奥能动力学 → 秘铸骑士甲 → 誓约堡垒 at (4..6,3.5) in the DND tab after knight forging / legendary armor");
    // 2. 盔甲、头盔、电池都只在奥械锻炉上做，按档次挂研究；贴图（穿着 + 遮罩 + 图标）都在
    var armors=new[]{"LP_PA_SquireArmor","LP_PA_KnightArmor","LP_PA_OathArmor","LP_PA_ArcanistArmor","LP_PA_ArtificerArmor","LP_PA_BastionArmor"};var helms=new[]{"LP_PA_SquireHelm","LP_PA_KnightHelm","LP_PA_BastionHelm"};
    var foundry=D(PowerArmor.FoundryDef);var bad=new List<string>();
    foreach(var n in armors.Concat(helms).Concat(new[]{PowerArmor.CellDef})){
     var d=D(n);var rec=DefDatabase<RecipeDef>.AllDefs.FirstOrDefault(r=>r.ProducedThingDef==d);
     if(d==null || rec==null || !rec.AllRecipeUsers.SequenceEqual(new[]{foundry}) || rec.researchPrerequisite==null || rec.researchPrerequisite.tab!=tab || d.ConfigErrors().Any() || rec.ConfigErrors().Any())bad.Add(n+":"+string.Join(";",d?.ConfigErrors()??new string[0]));
    }
    foreach(var n in armors){var d=D(n);var path=d.apparel.wornGraphicPath;foreach(var b in new[]{"Male","Female","Thin","Fat","Hulk"})foreach(var v in new[]{"south","east","north"})if(ContentFinder<Texture2D>.Get(path+"_"+b+"_"+v,false)==null || ContentFinder<Texture2D>.Get(path+"_"+b+"_"+v+"_m",false)==null)bad.Add(path+"_"+b+"_"+v);}
    foreach(var n in helms){var path=D(n).apparel.wornGraphicPath;foreach(var v in new[]{"south","east","north"})if(ContentFinder<Texture2D>.Get(path+"_"+v,false)==null)bad.Add(path+"_"+v);}
    foreach(var n in armors.Concat(helms)){var d=D(n);if(d.uiIcon==null || d.uiIcon==BaseContent.BadTex)bad.Add(n+" icon");}
    foreach(var n in new[]{"LP/PowerArmor/UI/Leap","LP/PowerArmor/UI/Field","LP/PowerArmor/UI/Overload","LP/PowerArmor/UI/Charge","LP/PowerArmor/Foundry/ArcaneFoundry_south","LP/PowerArmor/Foundry/ArcaneFoundry_east","LP/PowerArmor/Cradle/ArcaneCradle"})if(ContentFinder<Texture2D>.Get(n,false)==null)bad.Add(n);
    Check(bad.Count==0 && D(PowerArmor.CradleDef)?.researchPrerequisites?.Contains(r1)==true && foundry?.researchPrerequisites?.Contains(r1)==true,"6 armors, 3 helmets and the arcane cell are made only at the arcane foundry, gated by tab research; 5 bodies × 3 views + masks + icons load: "+string.Join(",",bad));
    // 3. 数值对标：侍从甲≈海军装甲，骑士甲高于重甲、移速惩罚更小，堡垒最高、只有体型 1 以上能穿
    Func<string,StatDef,float> St=(n,s)=>D(n).GetStatValueAbstract(s);Func<string,float> Mv=n=>D(n).equippedStatOffsets?.FirstOrDefault(m=>m.stat==StatDefOf.MoveSpeed)?.value??0f;
    var sharp=StatDefOf.ArmorRating_Sharp;float marine=St("Apparel_PowerArmor",sharp);float cata=D("Apparel_ArmorCataphract")!=null?St("Apparel_ArmorCataphract",sharp):1.2f;
    var gate=D("LP_PA_BastionArmor").GetModExtension<ApparelGate>();
    Check(Mathf.Abs(St("LP_PA_SquireArmor",sharp)/marine-1f)<.05f && St("LP_PA_KnightArmor",sharp)>cata && Mv("LP_PA_KnightArmor")>-.5f && armors.All(n=>St("LP_PA_BastionArmor",sharp)>=St(n,sharp)) && Mv("LP_PA_BastionArmor")<=-.8f && gate!=null && Near(gate.minBodySize,1f),"stats: squire "+St("LP_PA_SquireArmor",sharp)+" vs marine "+marine+", knight "+St("LP_PA_KnightArmor",sharp)+" vs cataphract "+cata+", bastion "+St("LP_PA_BastionArmor",sharp)+" move "+Mv("LP_PA_BastionArmor")+", body size ≥ 1");
    // 4. 穿上：核心满；配套头盔上限 +20%；按钮（能量条、推进跃击、充能）
    var spot=ItemSpot(4);var p=SharedPawn();Place(p,spot);p.jobs.StopAll();made.Add(p);
    var armor=PaWear(p,"LP_PA_SquireArmor");var core=armor.GetComp<CompArcaneCore>();float bare=core.Max;
    PaWear(p,"LP_PA_SquireHelm");var giz=PowerArmor.GizmosFor(p);
    Check(PowerArmor.Core(p)==core && Near(bare,100f) && Near(core.Max,120f) && Near(core.Energy,100f) && PowerArmor.Live.Contains(core) && giz.OfType<Gizmo_ArcaneCore>().Count()==1 && giz.OfType<Command_Action>().Any(g=>g.defaultLabel=="推进跃击") && giz.OfType<Command_Action>().Any(g=>g.defaultLabel=="充能") && !giz.OfType<Command_Action>().Any(g=>g.defaultLabel=="过载冲击"),"squire armor worn: core 100 (120 with helmet), energy bar, leap and charge buttons, no overload");
    // 5. 受击扣能量；征召每 250 刻 -1
    core.Energy=100f;p.TakeDamage(new DamageInfo(DamageDefOf.Blunt,20f,0f,-1f,null));float afterHit=core.Energy;
    p.drafter.Drafted=true;PowerArmor.Tick250();float afterDraft=core.Energy;p.drafter.Drafted=false;
    Check(afterHit>=93f && afterHit<=96f && Near(afterHit-afterDraft,1f),"20 blunt damage drains about 5 (0.25/pt of the incoming amount after other damage factors: "+afterHit+"), one drafted tick drains 1 ("+afterDraft+")");
    // 6. 耗尽：铁壳（移动、操作下降），能力不能用；电池充回来铁壳消失
    float move0=p.health.capacities.GetLevel(PawnCapacityDefOf.Moving);core.Energy=0f;PowerArmor.Sync(p);float move1=p.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
    bool husk=Items.Has(p,PowerArmor.HuskHediff);string leapWhy=PowerArmor.CannotLeap(p,core);
    var cell=MakeItem(PowerArmor.CellDef);made.Add(cell);var use=cell.TryGetComp<CompUseEffect_ArcaneCell>();bool canUse=use.CanBeUsedBy(p).Accepted;use.DoEffect(p);
    Check(husk && move1<move0-.3f && leapWhy!=null && canUse && Near(core.Energy,60f) && !Items.Has(p,PowerArmor.HuskHediff),"empty core: husk (moving "+move0.ToString("0.##")+"→"+move1.ToString("0.##")+"), leap disabled; an arcane cell restores 60 and clears the husk");
    // 7. 背包里的电池：充能按钮立刻换上
    var carried=MakeItem(PowerArmor.CellDef,2);p.inventory.innerContainer.TryAdd(carried);core.Energy=10f;bool swapped=PowerArmor.UseCarriedCell(p,core);
    Check(swapped && Near(core.Energy,70f) && PowerArmor.CarriedCell(p)?.stackCount==1,"carried cell swapped instantly: energy 10 → "+core.Energy+", one cell left");
    p.inventory.innerContainer.ClearAndDestroyContents();
    // 8. 推进跃击：落点进飞行物、人落到目标格；落地震倒相邻敌人（成功率按 TestRoll）
    core.Energy=core.Max;var target=spot+new IntVec3(5,0,0);var foe=PaFoe(target+IntVec3.North);made.Add(foe);PowerArmor.TestRoll=0f;
    p.drafter.Drafted=true;bool leapt=PowerArmor.Leap(p,core,target);Step(240);p.drafter.Drafted=false;float leapCost=core.Max-core.Energy;
    Check(leapt && p.Spawned && p.Position.DistanceTo(target)<1.5f && leapCost>=PowerArmor.LeapCost-.01f && leapCost<=PowerArmor.LeapCost+5f && PowerArmor.lastLandStunned==1 && foe.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury) && PowerArmor.CannotLeap(p,core)!=null,"leap lands at "+p.Position+" (target "+target+"), costs 20 ("+leapCost+", stunned "+PowerArmor.lastLandStunned+", foe hurt "+foe.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury)+", foe hostile "+foe.HostileTo(p)+", foe at "+foe.Position+", cd "+PowerArmor.CannotLeap(p,core)+"), stuns the adjacent foe, then cools down");
    // 9. 骑士甲：奥能力场挡下伤害并按 0.5/点扣能量；圣誓型 0.35
    p.apparel.Remove(armor);armor.Destroy();var knight=PaWear(p,"LP_PA_KnightArmor");var kc=knight.GetComp<CompArcaneCore>();int inj0=p.health.hediffSet.hediffs.Count(h=>h is Hediff_Injury);
    bool fieldOn=PowerArmor.Field(p,kc);float e0=kc.Energy;p.TakeDamage(new DamageInfo(DamageDefOf.Cut,20f,0f,-1f,null));int inj1=p.health.hediffSet.hediffs.Count(h=>h is Hediff_Injury);
    Check(fieldOn && Items.Has(p,PowerArmor.FieldHediff) && inj1==inj0 && Near(e0-kc.Energy,10f) && Near(kc.Max,180f) && Near(D("LP_PA_OathArmor").GetCompProperties<CompProperties_ArcaneCore>().fieldCost,.35f),"knight field absorbs a 20 cut (no injury) for 10 energy; knight core 150 → 180 with helmet; oath field costs 0.35/pt");
    Items.Remove(p,PowerArmor.FieldHediff);
    // 10. 秘法炮手型：奥术强度 +0.2；过载冲击震晕 3.9 格内的敌人
    var arcStat=DefDatabase<StatDef>.GetNamed("LP_ArcanePower");float ap0=p.GetStatValue(arcStat);p.apparel.Remove(knight);knight.Destroy();var arc=PaWear(p,"LP_PA_ArcanistArmor");var ac=arc.GetComp<CompArcaneCore>();float ap1=p.GetStatValue(arcStat);
    var foe2=PaFoe(CellFinder.StandableCellNear(p.Position,map,2.5f,c=>c!=p.Position && c.GetFirstPawn(map)==null) is var fc && fc.IsValid?fc:p.Position+IntVec3.South);made.Add(foe2);foe2.stances.stunner.StopStun();
    int stunned=PowerArmor.Overload(p,ac);
    Check(Mathf.Abs(ap1-ap0-.2f)<.011f && stunned>=1 && foe2.stances.stunner.Stunned && !PowerArmor.GizmosFor(p).OfType<Command_Action>().Any(g=>g.defaultLabel=="推进跃击"),"arcanist armor: arcane power "+ap0.ToString("0.##")+"→"+ap1.ToString("0.##")+", overload stuns "+stunned+" foe(s), no leap");
    PowerArmor.TestRoll=null;foe.Destroy();foe2.Destroy();
    // 11. 奇械型：核心 225（配头盔 270），植入物调谐位 +1
    int att0=DndImplants.AttuneMax(p);p.apparel.Remove(arc);arc.Destroy();var art=PaWear(p,"LP_PA_ArtificerArmor");int att1=DndImplants.AttuneMax(p);
    Check(att1==att0+1 && Near(art.GetComp<CompArcaneCore>().Max,270f),"artificer armor: attunement slots "+att0+"→"+att1+", core 225 (270 with helmet)");
    // 12. 充能座：通电时去交互格充能
    var cradle=ThingMaker.MakeThing(D(PowerArmor.CradleDef));cradle.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(cradle,spot+new IntVec3(-3,0,3),map,Rot4.South);made.Add(cradle);
    var power=cradle.TryGetComp<CompPowerTrader>();power.PowerOn=true;var artc=art.GetComp<CompArcaneCore>();artc.Energy=50f;
    bool ordered=PowerArmor.OrderCharge(p,cradle);Step(1500);float charged=artc.Energy;
    power.PowerOn=false;string unpowered=PowerArmor.CannotCharge(p,cradle);
    Check(ordered && charged>=50f+PowerArmor.CradleRate*2f && unpowered!=null,"cradle: charge job started, energy 50 → "+charged.ToString("0")+"; refused when unpowered ("+unpowered+")");
    p.jobs.StopAll();
    // 13. NPC：骑士、圣武士少量穿戴；誓约堡垒只给体型 1 以上的人
    var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FC_Knight");var npc=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true));made.Add(npc);
    foreach(var a in npc.apparel.WornApparel.Where(a=>a.GetComp<CompArcaneCore>()!=null || a.GetComp<CompArcaneHelm>()!=null).ToList())npc.apparel.Remove(a);
    string got=PowerArmor.Outfit(npc,0f);
    Check(kind!=null && got=="LP_PA_SquireArmor" && Items.Worn(npc,"LP_PA_SquireArmor")!=null && Items.Worn(npc,"LP_PA_SquireHelm")!=null && PowerArmor.Outfit(npc,.99f)==null && ApparelGates.Blocked(npc,D("LP_PA_BastionArmor"))==(npc.BodySize<1f?"需要体型1以上才能穿戴。":null),"knight NPC with roll 0 gets squire armor + helm, roll .99 gets nothing; bastion gate follows body size "+npc.BodySize);
    // 存读档：留一件能量 37 的奇械甲
    artc.Energy=37f;paSaveId=p.thingIDNumber;made.Remove(p);
   }
   finally{PowerArmor.TestRoll=null;foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn pw && pw.Dead){if(pw.Corpse!=null && !pw.Corpse.Destroyed)pw.Corpse.Destroy();}else t.Destroy();}}
  }
  void CheckPowerArmorAfterLoad(){
   var p=map.mapPawns.AllPawns.FirstOrDefault(x=>x.thingIDNumber==paSaveId);var c=PowerArmor.Core(p);
   Check(p!=null && c!=null && Near(c.Energy,37f) && PowerArmor.Live.Contains(c) && c.parent.def.defName=="LP_PA_ArtificerArmor","after load the artificer armor keeps 37 energy and is registered for ticking");
  }
 }
}
