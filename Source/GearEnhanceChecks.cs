using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.85.0 附魔强化：Test.ps1 -Only GearEnhance。
 public partial class LichChecks {
  static string geSwordId;
  void CheckGearEnhance(){
   Func<string,ThingDef> D=n=>DefDatabase<ThingDef>.GetNamedSilentFail(n);Func<string,ResearchProjectDef> R=n=>DefDatabase<ResearchProjectDef>.GetNamedSilentFail(n);Func<string,RecipeDef> Rc=n=>DefDatabase<RecipeDef>.GetNamedSilentFail(n);
   bool oldSup=GearEnhance.TestSuppressed;GearEnhance.TestSuppressed=true;
   var made=new List<Thing>();
   try{
    // 1. 研究与配方
    var tab=DefDatabase<ResearchTabDef>.GetNamed(ResearchTab.Tab);var low=R(GearEnhance.LowResearch);var high=R(GearEnhance.HighResearch);
    Check(low!=null && high!=null && low.tab==tab && high.tab==tab && Near(low.ResearchViewX,4f) && Near(low.ResearchViewY,0f) && Near(high.ResearchViewX,5f) && Near(high.ResearchViewY,0f) && low.prerequisites.Select(p=>p.defName).SequenceEqual(new[]{"LP_Research_Enchanting"}) && high.prerequisites.Select(p=>p.defName).SequenceEqual(new[]{GearEnhance.LowResearch}),"gear enhancing research at (4,0) after enchanting and high enhancing at (5,0) after it, both in the DND tab");
    var rl=Rc(GearEnhance.LowRecipe);var rh=Rc(GearEnhance.HighRecipe);
    Check(rl!=null && rh!=null && rl.Worker is RecipeWorker_GearEnhance && rh.Worker is RecipeWorker_GearEnhance && rl.AllRecipeUsers.SequenceEqual(new[]{D(Workbenches.Enchanting)}) && rh.AllRecipeUsers.SequenceEqual(new[]{D(Workbenches.Enchanting)}) && rl.researchPrerequisite==low && rh.researchPrerequisite==high && !rl.ConfigErrors().Any() && !rh.ConfigErrors().Any() && rl.products.NullOrEmpty() && rh.products.NullOrEmpty(),"two enhancing recipes only on the enchanting table, gated by the two projects, no products: "+string.Join(";",rl?.ConfigErrors()??new string[0]));
    // 2. 组件挂在武器和衣物上，不挂在材料上
    Func<string,bool> has=n=>D(n)?.comps?.Any(c=>c is CompProperties_GearEnhance)==true;
    Check(has("MeleeWeapon_LongSword") && has("Gun_AssaultRifle") && has("Apparel_FlakVest") && has("Apparel_Parka") && !has("WoodLog") && !has("Steel") && !has("Silver") && !has("LP_Mithral"),"enhance comp on weapons and apparel, not on logs or materials");
    // 3. 属性
    var sword=ThingMaker.MakeThing(D("MeleeWeapon_LongSword"),ThingDefOf.Steel);var rifle=ThingMaker.MakeThing(D("Gun_AssaultRifle"));var vest=ThingMaker.MakeThing(D("Apparel_FlakVest"));made.Add(sword);made.Add(rifle);made.Add(vest);
    var dmg=StatDefOf.MeleeWeapon_DamageMultiplier;var rdmg=DefDatabase<StatDef>.GetNamed("RangedWeapon_DamageMultiplier");var accM=StatDefOf.AccuracyMedium;var sharp=StatDefOf.ArmorRating_Sharp;var val=StatDefOf.MarketValue;
    float d0=sword.GetStatValue(dmg),v0=sword.GetStatValue(val),r0=rifle.GetStatValue(rdmg),a0=rifle.GetStatValue(accM),s0=vest.GetStatValue(sharp),vs0=vest.GetStatValue(StatDefOf.ArmorRating_Blunt);string l0=sword.LabelNoCount;
    GearEnhance.Comp(sword).level=3;GearEnhance.Comp(rifle).level=4;GearEnhance.Comp(vest).level=5;foreach(var t in made)GearEnhance.ClearStatCache(t);
    float d1=sword.GetStatValue(dmg),v1=sword.GetStatValue(val),r1=rifle.GetStatValue(rdmg),a1=rifle.GetStatValue(accM),s1=vest.GetStatValue(sharp);
    Check(Mathf.Abs(d1/d0-1.18f)<.001f && Mathf.Abs(v1/v0-1.45f)<.01f && Mathf.Abs(r1/r0-1.24f)<.001f && Mathf.Abs(a1-Mathf.Min(a0+.06f,.99f))<.001f && Mathf.Abs(s1-(s0*1.25f+.05f))<.001f,"stats: +3 sword dmg ×"+(d1/d0).ToString("0.###")+" value ×"+(v1/v0).ToString("0.###")+", +4 rifle dmg ×"+(r1/r0).ToString("0.###")+" acc "+a0.ToString("0.###")+"→"+a1.ToString("0.###")+", +5 vest sharp "+s0.ToString("0.###")+"→"+s1.ToString("0.###"));
    Check(sword.LabelNoCount=="+3 "+l0 && GearEnhance.Comp(sword).CompInspectStringExtra().Contains("伤害+18%") && vest.def.IsApparel && GearEnhance.Effects(vest,5).Contains("护甲×1.25"),"label '"+sword.LabelNoCount+"' and inspect text show the enhancement");
    // 4. 工作单只接选了目标、等级在本档的装备
    GearEnhance.Comp(sword).level=0;GearEnhance.Comp(rifle).level=5;
    var bl=new Bill_Production(rl);var bh=new Bill_Production(rh);var silver=MakeItem("Silver",25);var mith=MakeItem("LP_Mithral",4);made.Add(silver);made.Add(mith);
    bool unmarked=bl.IsFixedOrAllowedIngredient(sword);GearEnhance.Comp(sword).target=2;GearEnhance.Comp(rifle).target=7;
    Check(!unmarked && bl.IsFixedOrAllowedIngredient(sword) && !bh.IsFixedOrAllowedIngredient(sword) && !bl.IsFixedOrAllowedIngredient(rifle) && bh.IsFixedOrAllowedIngredient(rifle) && bl.IsFixedOrAllowedIngredient(silver) && bl.IsFixedOrAllowedIngredient(mith) && !bh.IsFixedOrAllowedIngredient(mith),"low bill takes only marked +0..+4 gear, high bill only marked +5..+9; materials go to their own bill");
    // 5. 成功率
    var sharpPawn=Person();sharpPawn.skills.GetSkill(SkillDefOf.Intellectual).Level=20;sharpPawn.skills.GetSkill(SkillDefOf.Crafting).Level=20;var dull=Person();dull.skills.GetSkill(SkillDefOf.Intellectual).Level=0;dull.skills.GetSkill(SkillDefOf.Crafting).Level=0;
    float cHi=GearEnhance.Chance(9,sharpPawn,null),cLo=GearEnhance.Chance(9,dull,null),c0=GearEnhance.Chance(0,dull,null);
    Check(Mathf.Abs(cHi-.38f)<.001f && Mathf.Abs(cLo-.1f)<.001f && Mathf.Abs(GearEnhance.Chance(4,null,null)-.7f)<.001f && c0>.89f,"chance +9→+10: skill 20 "+cHi.ToStringPercent()+", skill 0 "+cLo.ToStringPercent()+"; +4→+5 base 70%");
    sharpPawn.Destroy();dull.Destroy();
    // 6. 结果：成功升级修满耐久并到目标清标记；低档失败不变；+5 失败不掉；+6 以上失败掉一级
    sword.HitPoints=10;GearEnhance.TestRoll=0f;var ok1=GearEnhance.Apply(sword,null,null);var ok2=GearEnhance.Apply(sword,null,null);
    Check(ok1==GearEnhance.Result.Success && ok2==GearEnhance.Result.Success && GearEnhance.Level(sword)==2 && GearEnhance.Comp(sword).target==0 && sword.HitPoints==sword.MaxHitPoints,"two successes: sword +2, hit points repaired, target reached and cleared");
    GearEnhance.TestRoll=.999f;var f3=GearEnhance.Apply(sword,null,null);GearEnhance.Comp(rifle).level=5;var f5=GearEnhance.Apply(rifle,null,null);int at5=GearEnhance.Level(rifle);GearEnhance.Comp(rifle).level=7;var f7=GearEnhance.Apply(rifle,null,null);
    Check(f3==GearEnhance.Result.Fail && GearEnhance.Level(sword)==2 && f5==GearEnhance.Result.Fail && at5==5 && f7==GearEnhance.Result.Drop && GearEnhance.Level(rifle)==6,"failures: +2 stays, +5 stays, +7 drops to +6");
    GearEnhance.TestRoll=null;
    // 7. 真的在附魔台上做一次：材料用掉，装备留下并升级
    foreach(var n in new[]{"LP_Research_Enchanting",GearEnhance.LowResearch})if(!R(n).IsFinished)Find.ResearchManager.FinishProject(R(n),false,null,false);
    Check(GearEnhance.Allowed>=GearEnhance.LowMax,"research unlocks targets up to +"+GearEnhance.Allowed);
    var spot=ItemSpot(3);var bench=(Building_WorkTable)ThingMaker.MakeThing(D(Workbenches.Enchanting),ThingDefOf.Steel);bench.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(bench,spot,map);made.Add(bench);
    var crafter=ItemPawn(bench.InteractionCell);crafter.workSettings.EnableAndInitialize();crafter.workSettings.SetPriority(WorkTypeDefOf.Crafting,1);crafter.skills.GetSkill(SkillDefOf.Intellectual).Level=12;crafter.skills.GetSkill(SkillDefOf.Crafting).Level=12;
    var blade=ThingMaker.MakeThing(D("MeleeWeapon_LongSword"),ThingDefOf.Steel);GearEnhance.Comp(blade).target=1;GenPlace.TryPlaceThing(blade,bench.InteractionCell+IntVec3.West,map,ThingPlaceMode.Near);blade.SetForbidden(false,false);made.Add(blade);
    GenPlace.TryPlaceThing(silver,bench.InteractionCell+IntVec3.East,map,ThingPlaceMode.Near);silver.SetForbidden(false,false);GenPlace.TryPlaceThing(mith,bench.InteractionCell+IntVec3.East*2,map,ThingPlaceMode.Near);mith.SetForbidden(false,false);
    float work=rl.workAmount;rl.workAmount=60;
    try{
     var b=new Bill_Production(rl){repeatMode=BillRepeatModeDefOf.RepeatCount,repeatCount=1};b.SetPawnRestriction(crafter);b.SetStoreMode(BillStoreModeDefOf.DropOnFloor,null);bench.BillStack.AddBill(b);
     foreach(var other in map.mapPawns.FreeColonistsSpawned.Where(x=>x!=crafter).ToList()){other.jobs.StopAll();if(other.drafter!=null)other.drafter.Drafted=true;}
     var giver=new WorkGiver_DoBill{def=DefDatabase<WorkGiverDef>.GetNamed("LP_DoBillsEnchanting")};Job j;FloatMenuMakerMap.makingFor=crafter;try{j=giver.JobOnThing(crafter,bench,true);}finally{FloatMenuMakerMap.makingFor=null;}
     if(j==null)foreach(var t in new[]{blade,silver,mith})Log.Message("[LICH-DIAG] "+t+" destroyed="+t.Destroyed+" spawned="+t.Spawned+" pos="+t.PositionHeld+" n="+t.stackCount+" allowed="+b.IsFixedOrAllowedIngredient(t)+" reach="+(t.Spawned && crafter.CanReserveAndReach(t,PathEndMode.ClosestTouch,Danger.Deadly))+" forbidden="+(t.Spawned && t.IsForbidden(crafter))+" bench="+bench.Position);
     Check(j!=null,"native work giver creates the enhancing job at the enchanting table; "+JobFailReason.Reason);
     GearEnhance.TestRoll=0f;crafter.jobs.StartJob(j,JobCondition.InterruptForced);for(int i=0;i<6000 && b.repeatCount>0;i++)Step(1);
     Check(b.repeatCount==0 && silver.Destroyed && mith.Destroyed && !blade.Destroyed && blade.Spawned && GearEnhance.Level(blade)==1 && GearEnhance.Comp(blade).target==0,"native bill uses up the silver and mithral, leaves the sword on the map at +"+GearEnhance.Level(blade));
     bench.BillStack.Delete(b);
    }finally{GearEnhance.TestRoll=null;rl.workAmount=work;foreach(var other in map.mapPawns.FreeColonistsSpawned.ToList())if(other.drafter!=null)other.drafter.Drafted=false;}
    // 8. 存读档：等级和目标保留
    GearEnhance.Comp(blade).level=4;GearEnhance.Comp(blade).target=5;geSwordId=blade.ThingID;made.Remove(blade);
   }finally{
    GearEnhance.TestRoll=null;GearEnhance.TestSuppressed=oldSup;
    foreach(var t in made)if(!t.Destroyed)t.Destroy();
   }
  }
  void CheckGearEnhanceAfterLoad(){
   var blade=map.listerThings.AllThings.FirstOrDefault(t=>t.ThingID==geSwordId);
   Check(blade!=null && GearEnhance.Level(blade)==4 && GearEnhance.Comp(blade).target==5 && blade.LabelNoCount.StartsWith("+4 "),"enhance level and target survive save/load: "+blade?.LabelNoCount);
   blade?.Destroy();
  }
 }
}
