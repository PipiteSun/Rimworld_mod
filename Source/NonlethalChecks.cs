using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.61.0：Test.ps1 -Only Nonlethal（制伏武器）：定义与贴图、短棍打到倒地零伤口打不死、恢复节奏、心智抗性 / 免疫、体型、束缚网、猎人排除、
 // 抓俘虏后按时醒来、安眠粉囊范围（含友军）、设置倍率、存读档。器械师门槛在 -VPE -Only Nonlethal（Bridges/VPE/Source/NonlethalVPEChecks.cs）。
 public partial class LichChecks {
  static string nlPawnId;static float nlSaved;static int nlSavedTick;
  static int NlWounds(Pawn p)=>p.health.hediffSet.hediffs.Count(h=>h is Hediff_Injury || h is Hediff_MissingPart);
  static HediffComp_SubdueRecover NlComp(Pawn p)=>(p.health.hediffSet.GetFirstHediffOfDef(Nonlethal.SubduedDef) as HediffWithComps)?.TryGetComp<HediffComp_SubdueRecover>();
  static DamageInfo NlHit(DamageDef d,float amount,ThingDef weapon=null)=>new DamageInfo(d,amount,0f,-1f,null,null,weapon);
  static ThingWithComps NlMake(string name){var d=DefDatabase<ThingDef>.GetNamed(name);return (ThingWithComps)ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);}
  static void NlEquip(Pawn p,string name){p.equipment.DestroyAllEquipment();p.equipment.AddEquipment(NlMake(name));}

  void CheckNonlethal(){
   var setting=LichMod.Settings.subdueDamagePercent;LichMod.Settings.subdueDamagePercent=100;
   var temp=new List<Thing>();
   try{
    // 1. 定义、贴图、研究、设置
    var weapons=Nonlethal.Weapons.Select(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)).ToList();
    var projs=new[]{"LP_Proj_Net","LP_Proj_SleepArrow","LP_Bolt_Sleep","LP_Proj_SleepDust"}.Select(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)).ToList();
    var sub=Nonlethal.SubduedDef;var net=DefDatabase<HediffDef>.GetNamedSilentFail("LP_Netted");
    bool defs=weapons.All(d=>d!=null && !d.ConfigErrors().Any()) && projs.All(d=>d?.projectile!=null) && sub!=null && net!=null && Near(sub.maxSeverity,Nonlethal.Cap) && sub.stages.Count==4
     && sub.stages.Last().capMods.Any(c=>c.capacity==PawnCapacityDefOf.Consciousness && Near(c.setMax,.1f)) && !sub.tendable && sub.injuryProps==null
     && Nonlethal.SubdueDamage?.Worker is DamageWorker_LPSubdue && Nonlethal.SleepDamage?.Worker is DamageWorker_LPSubdue && Nonlethal.SubdueDamage.armorCategory?.defName=="Blunt" && Nonlethal.SleepDamage.armorCategory==null;
    bool tex=new[]{"SubduingCudgel","NetCrossbow","SleepBow","SleepStaff","SleepDustPouch","Proj_Net","Proj_SleepArrow","Proj_SleepBolt","Proj_Dust"}.All(n=>ContentFinder<Texture2D>.Get("LP/Nonlethal/"+n,false)!=null);
    var research=DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LP_Research_Subduing");
    RecipeDef R(ThingDef t)=>DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.ProducedThingDef==t);
    var anvil=DefDatabase<ThingDef>.GetNamed("LP_RuneAnvil");var table=DefDatabase<ThingDef>.GetNamed("LP_EnchantingTable");
    var badRecipes=weapons.Where(w=>{var r=R(w);if(r==null)return true;var req=new List<ResearchProjectDef>();if(r.researchPrerequisite!=null)req.Add(r.researchPrerequisite);if(r.researchPrerequisites!=null)req.AddRange(r.researchPrerequisites);
      return !(req.Contains(research) && (w.defName=="LP_SleepStaff"?r.AllRecipeUsers.Contains(table) && req.Any(x=>x.defName=="LP_ArcaneStaves"):r.AllRecipeUsers.Contains(anvil)));}) // 深渊熔炉（1.54.0）本来就能做所有符文铁砧的配方

     .Select(w=>{var r=R(w);return w.defName+":"+r?.researchPrerequisite?.defName+"+"+string.Join("/",r?.researchPrerequisites?.Select(x=>x.defName)??new string[0])+"@"+string.Join("/",r?.AllRecipeUsers.Select(x=>x.defName)??new string[0]);}).ToList();
    bool recipes=badRecipes.Count==0;
    bool res=research!=null && research.tab?.defName==ResearchTab.Tab && (int)research.baseCost==800 && Near(research.ResearchViewX,1f) && Near(research.ResearchViewY,4.9f) && research.prerequisites?.Count==1 && research.prerequisites[0].defName=="LP_Research_RuneSmithing";
    bool npc=weapons.All(w=>w.weaponTags!=null && w.weaponTags.SequenceEqual(new[]{"LP_Nonlethal"}) && (w.tradeTags==null || !w.tradeTags.Any(t=>t=="WeaponMelee" || t=="WeaponRanged" || t=="LP_Staff"))) && !DefDatabase<PawnKindDef>.AllDefs.Any(k=>k.weaponTags!=null && k.weaponTags.Contains("LP_Nonlethal"));
    var staff=weapons[3];
    bool staffOk=Arcana.Group(staff)=="G" && staff.Verbs[0].verbClass==typeof(Verb_ArcaneShoot) && projs[2].thingClass==typeof(LP_ArcaneBullet) && projs[2].projectile.damageDef==Nonlethal.SleepDamage && projs[0].thingClass==typeof(Projectile_LPNet) && Near(projs[3].projectile.explosionRadius,2.4f) && projs[3].projectile.damageDef==Nonlethal.SleepDamage;
    bool desc=weapons.All(w=>w.description.Contains("非致命：只会让目标昏迷倒地，不会造成伤口，也不会杀死目标。适合抓俘虏。"));
    Check(defs && tex && recipes && res && npc && staffOk && desc && LichMod.SettingsLeft.Length+LichMod.SettingsRight.Length==19,
     "nonlethal defs load: 2 damage types (no armor for sleep magic), LP_Subdued 4 stages cap 1.3 (consciousness max 10%, no tending), LP_Netted, 5 weapons + 4 projectiles, 9 textures; research 制伏战术 (1,4.9) after 符文锻造 cost 800; rune anvil recipes, sleep staff on the enchanting table also needs 奥术法杖; no NPC weapon / general trade tags; still 19 settings groups "+string.Join(",",badRecipes));
    // 2. 短棍：人类 3–4 下倒地，零伤口、活着；再打 20 下还活着
    var a=HtPawn(null,null,true,0,0);var cudgel=DefDatabase<ThingDef>.GetNamed("LP_SubduingCudgel");
    float first=0f;int hits=0;bool early=false;
    while(!a.Downed && hits<10){float before=Nonlethal.Severity(a);a.TakeDamage(NlHit(Nonlethal.SubdueDamage,12f,cudgel));if(hits==0)first=Nonlethal.Severity(a)-before;hits++;if(a.Downed && Nonlethal.Severity(a)<Nonlethal.DownedAt)early=true;}
    bool downed=a.Downed && !a.Dead && NlWounds(a)==0 && a.health.hediffSet.BleedRateTotal==0f && a.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness)<=.1001f;
    float sev=Nonlethal.Severity(a);
    for(int i=0;i<20;i++)a.TakeDamage(NlHit(Nonlethal.SubdueDamage,12f,cudgel));
    Check(downed && !early && hits>=3 && hits<=4 && Near(first,.3f) && !a.Dead && a.Downed && NlWounds(a)==0 && Near(Nonlethal.Severity(a),sev),
     "cudgel (12) on a human: +"+first.ToString("0.00")+" per hit, downed after "+hits+" hits with zero injuries and consciousness ≤10%; 20 more hits change nothing, still alive");
    // 3. 恢复：1 小时内不消退，之后每小时 −0.1，醒来
    var h=a.health.hediffSet.GetFirstHediffOfDef(Nonlethal.SubduedDef);var comp=NlComp(a);
    h.Severity=Nonlethal.Cap;comp.Notify_Hit();int now=Find.TickManager.TicksGame;
    float adj0=0f;comp.lastHitTick=now-2400;comp.CompPostTickInterval(ref adj0,250);
    float adj1=0f;comp.lastHitTick=now-2500;comp.CompPostTickInterval(ref adj1,2500);
    comp.Notify_Hit();Step(600);float held=h.Severity;
    comp.lastHitTick=now-2500;Step(500);float decayed=h.Severity;
    h.Severity=1.004f;comp.lastHitTick=-1;Step(250);
    bool woke=!a.Downed && !a.Dead && a.health.hediffSet.HasHediff(Nonlethal.SubduedDef);
    Check(adj0==0f && Near(adj1,-.1f) && Near(held,Nonlethal.Cap) && decayed<Nonlethal.Cap-.01f && decayed>Nonlethal.Cap-.04f && woke && NlWounds(a)==0,
     "recovery: no decay within the first hour after the last hit (600 ticks held "+held.ToString("0.000")+"), then −0.1/hour (500 ticks → "+decayed.ToString("0.000")+"); dropping below 100% wakes the pawn (1.3 → ≈4 h unconscious)");
    // 4. 心智抗性 / 免疫
    var human=HtPawn();var elf=HtPawn("LP_Elf");
    float ih=Nonlethal.Subdue(human,NlHit(Nonlethal.SleepDamage,10f),false),ie=Nonlethal.Subdue(elf,NlHit(Nonlethal.SleepDamage,10f),false);
    float r=DNDMind.Resistance(elf);float ieBlunt=Nonlethal.Subdue(elf,NlHit(Nonlethal.SubdueDamage,10f),false);
    bool elfOk=Near(r,.5f) && Near(ie,Nonlethal.Increment(10f,elf.BodySize,elf.HealthScale)*.5f) && Near(ieBlunt,Nonlethal.Increment(10f,elf.BodySize,elf.HealthScale)) && Near(ih,Nonlethal.Increment(10f,human.BodySize,human.HealthScale));
    var mechKind=DefDatabase<PawnKindDef>.GetNamed("Mech_Scyther");
    var mech=PawnGenerator.GeneratePawn(new PawnGenerationRequest(mechKind,Faction.OfMechanoids,forceGenerateNewPawn:true));htMade.Add(mech);
    var construct=HtPawn();construct.health.AddHediff(Necro.Def<HediffDef>("Construct"));
    var lich=HtPawn();lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);
    var mind=HtPawn();
    float im=Nonlethal.Subdue(mech,NlHit(Nonlethal.SubdueDamage,30f),false),ic=Nonlethal.Subdue(construct,NlHit(Nonlethal.SubdueDamage,30f),false),il=Nonlethal.Subdue(lich,NlHit(Nonlethal.SleepDamage,30f),false);
    Check(elfOk && im==0f && ic==0f && il==0f && Nonlethal.Immune(mech)==Nonlethal.ImmuneText && Nonlethal.Immune(construct)==Nonlethal.ImmuneText && Nonlethal.Immune(lich)==Nonlethal.ImmuneText && Nonlethal.Immune(a)==null && Nonlethal.MindFactor(mind,Nonlethal.SleepDamage)==1f && Nonlethal.Severity(mech)==0f,
     "elf (心智抗性 "+r.ToStringPercent()+") takes half from sleep magic ("+ie.ToString("0.000")+" vs human "+ih.ToString("0.000")+") but full from the cudgel; mechanoid, 死灵造物 and 巫妖 are immune (「免疫制伏」)");
    // 5. 体型：体型 2 增量减半；火巨人按体型 × 生命值倍率
    var giant=HtPawn("LP_FireGiant");var gd=NlHit(Nonlethal.SubdueDamage,12f,cudgel);
    float ig=Nonlethal.Subdue(giant,gd,false);float scale=giant.BodySize*giant.HealthScale;
    Check(Near(Nonlethal.Increment(12f,2f,1f),Nonlethal.Increment(12f,1f,1f)/2f) && Near(Nonlethal.Increment(12f,1f,2f),.15f) && scale>=2f && ig>0f && Near(ig,Nonlethal.Increment(12f*Nonlethal.ArmorFactor(giant,gd),giant.BodySize,giant.HealthScale)) && ig<=first/2f+.001f,
     "body size 2 halves the increment; fire giant (size "+giant.BodySize.ToString("0.##")+" × health "+giant.HealthScale.ToString("0.##")+") gets +"+ig.ToString("0.000")+" from a cudgel hit vs human +"+first.ToString("0.00"));
    // 6. 束缚网：实际射出一张网，眩晕 + 缚网（移动 −50%）+ 制伏
    var shooter=HtPawn(null,null,true,-3,3);var target=HtPawn(null,null,true,0,3);float move0=target.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
    var bow=NlMake("LP_NetCrossbow");temp.Add(bow);
    var proj=(Projectile)GenSpawn.Spawn(DefDatabase<ThingDef>.GetNamed("LP_Proj_Net"),shooter.Position,map);
    proj.Launch(shooter,shooter.DrawPos,target,target,ProjectileHitFlags.IntendedTarget,false,bow);
    for(int i=0;i<120 && !proj.Destroyed;i++)Step(1);
    bool stunned=target.stances.stunner.Stunned;target.health.capacities.Notify_CapacityLevelsDirty();float move1=target.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
    int left=Items.TicksLeft(target,"LP_Netted");
    Check(proj.Destroyed && stunned && left>300 && left<=360 && move1<=move0-.45f && Near(Nonlethal.Severity(target),Nonlethal.Increment(8f,target.BodySize,target.HealthScale)) && NlWounds(target)==0,
     "net bolt hits: stunned, 缚网 for "+left+" ticks (moving "+move0.ToStringPercent()+" → "+move1.ToStringPercent()+"), +"+Nonlethal.Severity(target).ToString("0.00")+" subdued, no wound");
    // 7. 猎人：拿制伏武器不算有猎具
    var hm=AccessTools.Method(typeof(WorkGiver_HunterHunt),"HasHuntingWeapon");object wg=hm.IsStatic?null:new WorkGiver_HunterHunt();
    bool Hunts(Pawn p)=>(bool)hm.Invoke(wg,new object[]{p});
    var hunter=HtPawn();NlEquip(hunter,"Bow_Short");bool vanilla=Hunts(hunter);
    NlEquip(hunter,"LP_SleepBow");bool sleepBow=Hunts(hunter);NlEquip(hunter,"LP_NetCrossbow");bool netBow=Hunts(hunter);NlEquip(hunter,"LP_SleepStaff");bool staffHunt=Hunts(hunter);
    Check(vanilla && !sleepBow && !netBow && !staffHunt,"hunters: a short bow counts as a hunting weapon, the sleep bow / net crossbow / sleep staff do not");
    // 8. 抓俘虏：敌人被打昏、活着，抓走后还在昏迷，按时醒来、仍是俘虏
    var enemy=Find.FactionManager.AllFactions.First(f=>!f.IsPlayer && !f.defeated && !f.Hidden && f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction);
    var foe=HtPawn(null,enemy,true,3,-3);
    int fh=0;while(!foe.Downed && fh<10){foe.TakeDamage(NlHit(Nonlethal.SleepDamage,16f));fh++;}
    bool capturable=foe.Downed && !foe.Dead && NlWounds(foe)==0 && foe.HostileTo(Faction.OfPlayer);
    foe.guest.CapturedBy(Faction.OfPlayer,a);
    bool kept=foe.IsPrisonerOfColony && foe.Downed && foe.health.hediffSet.HasHediff(Nonlethal.SubduedDef);
    var fc=NlComp(foe);foe.health.hediffSet.GetFirstHediffOfDef(Nonlethal.SubduedDef).Severity=1.004f;fc.lastHitTick=-1;Step(250);
    Check(capturable && kept && !foe.Downed && !foe.Dead && foe.IsPrisonerOfColony,"enemy put down by "+fh+" sleep bolts (16) is downed, alive and unhurt — capturable; the prisoner keeps 制伏 and wakes up still a prisoner");
    // 9. 安眠粉囊：半径 2.4 内所有血肉生物（包括友军）都中，范围外不中，没有伤口
    // 都放在 HtCell 保证空旷的 9×9 里（外面可能是石墙、树），离倒地的 a (0,0) 3 格以上
    var c=HtCell(-3,-1);var f1=HtPawn(null,null,true,-3,-1);var f2=HtPawn(null,null,true,-1,-1);var far=HtPawn(null,null,true,-3,4);
    GenExplosion.DoExplosion(c,map,2.4f,Nonlethal.SleepDamage,shooter,22);Step(30);
    Check(Near(Nonlethal.Severity(f1),Nonlethal.Increment(22f,f1.BodySize,f1.HealthScale)) && Nonlethal.Severity(f2)>0f && Nonlethal.Severity(far)==0f && NlWounds(f1)+NlWounds(f2)==0,
     "sleep dust burst (22, radius 2.4) subdues our own pawns in range (+"+Nonlethal.Severity(f1).ToString("0.00")+" at the center, +"+Nonlethal.Severity(f2).ToString("0.00")+" 2 cells out), none 5 cells out ("+Nonlethal.Severity(far).ToString("0.00")+"), no wounds ("+(NlWounds(f1)+NlWounds(f2))+")");
    // 10. 设置倍率
    var s50=HtPawn();var s200=HtPawn();
    LichMod.Settings.subdueDamagePercent=50;float i50=Nonlethal.Subdue(s50,NlHit(Nonlethal.SubdueDamage,12f),false);
    LichMod.Settings.subdueDamagePercent=200;float i200=Nonlethal.Subdue(s200,NlHit(Nonlethal.SubdueDamage,12f),false);
    LichMod.Settings.subdueDamagePercent=100;
    Check(Near(i50,.15f) && Near(i200,.6f),"制伏伤害倍率 50% → +"+i50.ToString("0.00")+", 200% → +"+i200.ToString("0.00")+" per cudgel hit");
   }finally{
    LichMod.Settings.subdueDamagePercent=setting;
    foreach(var t in temp)if(!t.Destroyed)t.Destroy();
    foreach(var p in htMade)if(!p.Destroyed)p.Destroy();htMade.Clear();
   }
  }
  // 11. 存读档：制伏严重度与最后受击时刻都在
  void PrepareNonlethalSave(){
   var p=HtPawn(null,null,true,-2,-2);htMade.Clear();
   Nonlethal.Subdue(p,NlHit(Nonlethal.SubdueDamage,24f),false);
   nlPawnId=p.ThingID;nlSaved=Nonlethal.Severity(p);nlSavedTick=NlComp(p).lastHitTick;
  }
  void CheckNonlethalAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==nlPawnId);
   Check(p!=null && Near(Nonlethal.Severity(p),nlSaved) && NlComp(p)?.lastHitTick==nlSavedTick,"制伏 severity "+nlSaved.ToString("0.00")+" and last-hit tick survive save/load");
   p?.Destroy();
  }
 }
}
