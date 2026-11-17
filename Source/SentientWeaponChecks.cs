using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.168.0 人格武器：Test.ps1 -Only SentientWeapon。
 public partial class LichChecks {
  static string swBladeId,swPawnId,swPersona,swName;static float swRes;
  void CheckSentientWeapon(){
   Func<string,ThingDef> D=n=>DefDatabase<ThingDef>.GetNamedSilentFail(n);
   bool oldSup=GearEnhance.TestSuppressed;GearEnhance.TestSuppressed=true;
   var made=new List<Thing>();
   try{
    // 1. 人格定义与组件
    var ps=SentientWeapon.Personas;
    Check(ps.Count==8 && ps.All(d=>d.stages?.Count==4 && d.stages[0].statOffsets.NullOrEmpty() && d.stages[0].statFactors.NullOrEmpty() && !d.ConfigErrors().Any()) && DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_PersonaBond")!=null,"8 personas with 4 stages (stage 0 no bonus), defs clean: "+string.Join(",",ps.Select(d=>d.defName)));
    Func<string,bool> has=n=>D(n)?.comps?.Any(c=>c is CompProperties_SentientWeapon)==true;
    Check(has("MeleeWeapon_LongSword") && has("Gun_AssaultRifle") && !has("Apparel_Parka") && !has("Steel"),"persona comp on weapons only");
    // 2. 唤醒：要 +5；失败不给人格；成功给人格和名字
    var spot=ItemSpot(3);
    var blade=ThingMaker.MakeThing(D("MeleeWeapon_LongSword"),ThingDefOf.Steel);made.Add(blade);
    GearEnhance.Comp(blade).level=4;string low=SentientWeapon.Blocked(blade,map);
    GearEnhance.Comp(blade).level=5;GearEnhance.ClearStatCache(blade);
    SentientWeapon.TestRoll=.999f;var f=SentientWeapon.Awaken(blade,null,null);bool none=!SentientWeapon.Awake(blade);
    SentientWeapon.TestRoll=0f;var s=SentientWeapon.Awaken(blade,null,null,DefDatabase<HediffDef>.GetNamed("LP_Persona_Bloodthirsty"));
    var c=SentientWeapon.Comp(blade);
    Check(low!=null && low.Contains("+5") && f==GearEnhance.Result.Fail && none && s==GearEnhance.Result.Success && c.persona.defName=="LP_Persona_Bloodthirsty" && !c.weaponName.NullOrEmpty() && blade.LabelNoCount.StartsWith("「"+c.weaponName+"」+5 ") && c.CompInspectStringExtra().Contains("嗜血"),"awaken needs +5 ("+low+"), fail gives nothing, success: '"+blade.LabelNoCount+"'");
    // 3. 装备即结羁绊，拿着有加成和心情
    var p=Person();GenSpawn.Spawn(p,spot,map);made.Add(p);p.equipment.DestroyAllEquipment();
    float dmg0=p.GetStatValue(StatDefOf.MeleeDamageFactor);
    p.equipment.AddEquipment((ThingWithComps)blade);
    var h=SentientWeapon.BondOf(p);float dmg1=p.GetStatValue(StatDefOf.MeleeDamageFactor);
    var thought=DefDatabase<ThoughtDef>.GetNamed("LP_PersonaBond");var ts=thought.Worker.CurrentState(p);
    Check(c.bonded==p && h!=null && h.def==c.persona && h.weapon==blade && h.Holding && h.CurStageIndex==1 && Mathf.Abs(dmg1/dmg0-1.1f)<.001f && ts.Active && ts.StageIndex==0,"equip bonds: hediff "+h?.LabelCap+", melee dmg ×"+(dmg1/dmg0).ToString("0.###")+", mood "+ts.Active);
    // 4. 别人不能装备
    var other=Person();made.Add(other);bool otherCan=EquipmentUtility.CanEquip(blade,other,out string why);
    Check(!otherCan && why!=null && why.Contains(p.LabelShort),"other pawn can't equip: "+why);
    // 5. 共鸣：击杀人形 +2；到 30 苏醒，加成变强
    var victim=Person();GenSpawn.Spawn(victim,spot+IntVec3.East*2,map);made.Add(victim);
    victim.Kill(new DamageInfo(DamageDefOf.Cut,10f,instigator:p));float afterKill=c.resonance;
    c.resonance=30f;h.Refresh();float dmg2=p.GetStatValue(StatDefOf.MeleeDamageFactor);var ts2=thought.Worker.CurrentState(p);
    Check(Mathf.Abs(afterKill-2f)<.01f && c.Tier==2 && h.CurStageIndex==2 && Mathf.Abs(dmg2/dmg0-1.2f)<.001f && ts2.StageIndex==1,"kill +2 resonance ("+afterKill+"), 30 → 苏醒 ×"+(dmg2/dmg0).ToString("0.###"));
    // 6. 每拿一天 +1
    float r0=c.resonance;for(int i=0;i<10;i++)h.TickInterval(250);float r1=c.resonance;
    Check(Mathf.Abs((r1-r0)-2500f/GenDate.TicksPerDay)<.001f,"holding adds resonance over time: +"+(r1-r0).ToString("0.###")+" per 2500 ticks");
    // 7. 放下：分离阶段无加成；再拿起恢复
    p.equipment.Remove((ThingWithComps)blade);h.Refresh();float dmg3=p.GetStatValue(StatDefOf.MeleeDamageFactor);int st3=h.CurStageIndex;bool moodOff=!thought.Worker.CurrentState(p).Active;
    p.equipment.AddEquipment((ThingWithComps)blade);
    Check(st3==0 && Mathf.Abs(dmg3-dmg0)<.001f && moodOff && SentientWeapon.BondOf(p)?.CurStageIndex==2,"unequipped = 分离 (no bonus, no mood), re-equip restores stage 2");
    // 8. 重塑：换人格，状态跟着换，共鸣保留
    float keep=c.resonance;SentientWeapon.Awaken(blade,null,null,DefDatabase<HediffDef>.GetNamed("LP_Persona_Sage"));var h2=SentientWeapon.BondOf(p);
    Check(c.persona.defName=="LP_Persona_Sage" && h2!=null && h2.def==c.persona && p.health.hediffSet.hediffs.OfType<Hediff_SentientBond>().Count()==1 && Mathf.Abs(c.resonance-keep)<.01f && c.bonded==p,"reshape swaps persona hediff ("+h2?.def.defName+"), resonance kept "+c.resonance.ToString("0.#"));
    // 9. 解除羁绊：状态没了、心情变差、别人能装备并结羁绊
    c.Unbond(true);bool gone=SentientWeapon.BondOf(p)==null;bool sad=p.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_PersonaBondBroken"))!=null;
    p.equipment.Remove((ThingWithComps)blade);GenSpawn.Spawn(other,spot+IntVec3.North*2,map);bool can2=EquipmentUtility.CanEquip(blade,other,out _);other.equipment.DestroyAllEquipment();other.equipment.AddEquipment((ThingWithComps)blade);
    Check(gone && sad && can2 && c.bonded==other && SentientWeapon.BondOf(other)!=null,"unbond: hediff gone "+gone+", sad "+sad+", new pawn bonds "+(c.bonded==other));
    // 10. 羁绊者死亡，武器空出来
    other.Kill(null);
    Check(c.bonded==null,"bonded pawn's death frees the weapon");
    // 11. 附魔台：材料不够被挡，够了扣材料唤醒
    var bench=(Building_WorkTable)ThingMaker.MakeThing(D(Workbenches.Enchanting),ThingDefOf.Steel);bench.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(bench,ItemSpot(3),map);made.Add(bench);
    var op=ItemPawn(bench.InteractionCell);op.skills.GetSkill(SkillDefOf.Intellectual).Level=12;
    var rifle=ThingMaker.MakeThing(D("Gun_AssaultRifle"));GearEnhance.Comp(rifle).level=6;GenPlace.TryPlaceThing(rifle,bench.InteractionCell+IntVec3.West,map,ThingPlaceMode.Near);rifle.SetForbidden(false,false);made.Add(rifle);
    string short0=SentientWeapon.Blocked(rifle,map);
    var ad=MakeItem("LP_Adamantine",SentientWeapon.AdamantineCost);var gold=MakeItem("Gold",SentientWeapon.GoldCost);
    foreach(var t in new[]{ad,gold}){GenPlace.TryPlaceThing(t,bench.InteractionCell+IntVec3.East,map,ThingPlaceMode.Near);t.SetForbidden(false,false);}
    int adHave=GearEnhanceBench.CountOnMap(map,SentientWeapon.Adamantine),goldHave=GearEnhanceBench.CountOnMap(map,ThingDefOf.Gold);
    SentientWeapon.TestRoll=0f;var br=SentientWeapon.TryAwaken(rifle,bench);
    Check(short0=="材料不够" && br==GearEnhance.Result.Success && SentientWeapon.Awake(rifle) && adHave-GearEnhanceBench.CountOnMap(map,SentientWeapon.Adamantine)==SentientWeapon.AdamantineCost && goldHave-GearEnhanceBench.CountOnMap(map,ThingDefOf.Gold)==SentientWeapon.GoldCost && Mathf.Abs(SentientWeapon.Chance(null,null)-.6f)<.001f,"bench awaken: blocked '"+short0+"' without mats, then consumes "+SentientWeapon.AdamantineCost+" adamantine + "+SentientWeapon.GoldCost+" gold → "+rifle.LabelNoCount);
    // 11b. 1.170.0 封魂入器：囚犯死去，武器按他最强技能得到人格和名字、初始共鸣，羁绊者拿着有「器灵在哀嚎」
    var pris=Person();foreach(var sk in pris.skills.skills)sk.Level=2;pris.skills.GetSkill(SkillDefOf.Shooting).Level=16;
    if(pris.story?.traits!=null)foreach(var tr in pris.story.traits.allTraits.ToList())pris.story.traits.RemoveTrait(tr);
    pris.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);GenSpawn.Spawn(pris,ItemSpot(2),map);made.Add(pris);
    var bow=ThingMaker.MakeThing(D("Gun_AssaultRifle"));GearEnhance.Comp(bow).level=5;GenPlace.TryPlaceThing(bow,bench.InteractionCell+IntVec3.West,map,ThingPlaceMode.Near);bow.SetForbidden(false,false);made.Add(bow);
    bool listedP=SentientWeapon.Prisoners(map).Contains(pris);string noMats=SentientWeapon.BlockedSoul(bow,map);
    foreach(var t in new[]{MakeItem("LP_Adamantine",SentientWeapon.AdamantineCost),MakeItem("Gold",SentientWeapon.GoldCost)}){GenPlace.TryPlaceThing(t,bench.InteractionCell+IntVec3.East,map,ThingPlaceMode.Near);t.SetForbidden(false,false);}
    string pname=pris.Name.ToStringShort;var sr=SentientWeapon.TryBindSoul(bow,pris,bench);var bc=SentientWeapon.Comp(bow);
    var wielder=Person();GenSpawn.Spawn(wielder,ItemSpot(2),map);made.Add(wielder);wielder.equipment.DestroyAllEquipment();if(bow.Spawned)bow.DeSpawn();wielder.equipment.AddEquipment((ThingWithComps)bow);
    var tor=DefDatabase<ThoughtDef>.GetNamed("LP_PersonaTorment").Worker.CurrentState(wielder);
    Check(listedP && noMats=="材料不够" && sr==GearEnhance.Result.Success && pris.Dead && bc.persona.defName=="LP_Persona_Hunter" && bc.soulName==pname && bow.LabelNoCount.StartsWith("「"+pname+"」") && Mathf.Abs(bc.resonance-32f)<.01f && bc.Tier==2 && bc.bonded==wielder && tor.Active && bc.CompInspectStringExtra().Contains("器灵"),"soul bind: prisoner listed, blocked '"+noMats+"' without mats, then "+pname+" dead "+pris.Dead+" → "+bow.LabelNoCount+" "+bc.persona.defName+" res "+bc.resonance+", torment mood "+tor.Active+", bonded "+(bc.bonded==wielder)+", hediff "+SentientWeapon.BondOf(wielder)?.def.defName+" holding "+SentientWeapon.BondOf(wielder)?.Holding+" tier "+bc.Tier+" inspect "+bc.CompInspectStringExtra().Contains("器灵"));
    SentientWeapon.TestRoll=0f;SentientWeapon.Awaken(bow,null,null);
    Check(bc.soulName==null && !DefDatabase<ThoughtDef>.GetNamed("LP_PersonaTorment").Worker.CurrentState(wielder).Active,"reshaping releases the bound soul");
    // 12. 存档用：一把有人格、有羁绊的剑留在地图上
    var keeper=Person();GenSpawn.Spawn(keeper,ItemSpot(2),map);keeper.equipment.DestroyAllEquipment();
    var kb=ThingMaker.MakeThing(D("MeleeWeapon_LongSword"),ThingDefOf.Steel);GearEnhance.Comp(kb).level=5;SentientWeapon.Awaken(kb,null,null,DefDatabase<HediffDef>.GetNamed("LP_Persona_Guardian"));
    keeper.equipment.AddEquipment((ThingWithComps)kb);var kc=SentientWeapon.Comp(kb);kc.resonance=42.5f;SentientWeapon.BondOf(keeper).Refresh();
    swBladeId=kb.ThingID;swPawnId=keeper.ThingID;swPersona=kc.persona.defName;swName=kc.weaponName;swRes=kc.resonance;
    Check(kc.bonded==keeper,"save fixture bonded");
   }finally{
    SentientWeapon.TestRoll=null;GearEnhance.TestSuppressed=oldSup;
    foreach(var t in made)if(!t.Destroyed)t.Destroy();
   }
  }
  void CheckSentientWeaponAfterLoad(){
   var p=map.mapPawns.AllPawns.FirstOrDefault(x=>x.ThingID==swPawnId);var w=p?.equipment?.Primary;var c=SentientWeapon.Comp(w);var h=SentientWeapon.BondOf(p);
   Check(c!=null && w.ThingID==swBladeId && c.persona?.defName==swPersona && c.weaponName==swName && Mathf.Abs(c.resonance-swRes)<.01f && c.bonded==p && h!=null && h.weapon==w && h.CurStageIndex==2,"persona weapon survives save/load: "+w?.LabelNoCount+", bond "+(c?.bonded==p)+", hediff stage "+h?.CurStageIndex);
   p?.Destroy();
  }
 }
}
