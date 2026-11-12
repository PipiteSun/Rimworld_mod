using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.141.0：Test.ps1 -Only ProductionFeats（8 个生产专长，10 条 + 读档 1 条）。
 public partial class LichChecks {
  static string pfId;
  static float PfFactor(Hediff_Adventurer h,string stat)=>h.BuildStage(1f).statFactors?.GetStatFactorFromList(DefDatabase<StatDef>.GetNamed(stat))??1f;
  static float PfOffset(Hediff_Adventurer h,string stat)=>h.BuildStage(1f).statOffsets?.GetStatOffsetFromList(DefDatabase<StatDef>.GetNamed(stat))??0f;
  // 放一张工作台（找一块没有建筑和小人的空地）。
  Building_WorkTable PfBench(string defName){
   var d=DefDatabase<ThingDef>.GetNamed(defName);
   var cell=map.AllCells.Where(c=>c.x>14 && c.z>14 && c.x<map.Size.x-14 && c.z<map.Size.z-14 && CellRect.CenteredOn(c,5).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetFirstPawn(map)==null && x.GetFirstBuilding(map)==null)).OrderByDescending(c=>c.DistanceToSquared(map.Center)).First();
   var b=(Building_WorkTable)ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);b.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(b,cell,map);htThings.Add(b);return b;
  }
  readonly List<Thing> htThings=new List<Thing>();
  // 让小人「在某张台前工作」：当前 job 的目标 A 是这张台（Feats5.BenchOf 只看这个）。
  static void PfWorkAt(Pawn p,Building_WorkTable b){p.jobs.StopAll();if(b==null)return;p.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait,b),JobCondition.InterruptForced);}
  void CheckProductionFeats(){
   var s=LichMod.Settings;bool on=s.adventurer;s.adventurer=true;bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;bool f2=Feats2.TestSuppressed;Feats2.TestSuppressed=true;
   Rand.PushState(114101);
   try{CheckProductionFeatsInner();}
   finally{
    Rand.PopState();s.adventurer=on;Adventurer.TestSuppressed=sup;Feats2.TestSuppressed=f2;Feats2.TestRoll=null;
    foreach(var p in htMade){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();else if(!p.Destroyed)p.Destroy();}htMade.Clear();
    foreach(var t in htThings)if(!t.Destroyed)t.Destroy();htThings.Clear();
   }
  }
  void CheckProductionFeatsInner(){
   // 1. 8 个 def 都加载：图标在、defName 不以数字结尾、都附带属性、没有敌人标签、起源专长 3 个、顺序接在现有之后且不重复。
   {var feats=Feats5.Names.Select(n=>DefDatabase<LP_FeatDef>.GetNamedSilentFail(n)).ToList();
    bool ok=feats.All(f=>f!=null && f.Icon!=BaseContent.BadTex && !char.IsDigit(f.defName[f.defName.Length-1]) && f.BonusOptions.Count>0 && f.enemyTags==null && f.order>=49 && Feats2.Is(f));
    bool origins=feats.Count(f=>f!=null && f.origin)==3 && Feats5.PracticedHand.origin && Feats5.MasterAlchemist.origin && Feats5.GreenThumb.origin;
    bool defs=Items.Hediff("LP_FeatFabricated")!=null && DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_FeatMasterworkPride")!=null && DefDatabase<AbilityDef>.GetNamedSilentFail("LP_FeatGrowth")!=null && DefDatabase<AbilityDef>.GetNamedSilentFail("LP_FeatFabricate")!=null;
    bool text=feats.All(f=>f!=null && !f.description.Contains("d20") && !f.description.Contains("检定") && !f.description.Contains("掷骰"));
    Check(ok && origins && defs && text && feats.Select(f=>f.order).Distinct().Count()==8,"[1.141.0] 8 production feats load with icons, +1 ability, no enemy tags, distinct orders ≥49, 3 origin feats; hediff / thought / 2 abilities exist; no dice words (bad: "+string.Join(",",feats.Where(f=>f==null || f.Icon==BaseContent.BadTex).Select(f=>f?.defName??"null"))+")");}
   // 2. 学以致用：同样技能经验下冒险者经验 ×1.5；每日全额 80 → 120。
   {var w=HtPawn();var h=F2Adv(w);
    float Learn(float xp){h.workXpDay=-1;h.workXpToday=0f;h.workXpRem=0f;return AdventurerWork.Learned(w,SkillDefOf.Crafting,xp);}
    float plain=Learn(500f),plainBig=Learn(10000f);h.AddFeat(Feats5.PracticedHand);float boosted=Learn(500f),boostedBig=Learn(10000f);
    // 500 技能经验 = 10 点：10 → 15；10000 = 200 点：80 + 120×0.5 = 140 → 300 点里 120 全额 + 180×0.5 = 210
    Check(Near(plain,10f) && Near(boosted,15f) && Near(plainBig,140f) && Near(boostedBig,210f) && Near(Feats5.DailyFull(w),120f),"[1.141.0] 学以致用: work XP 10 → "+boosted.ToString("0.#")+" (×1.5); daily full 80 → 120 (200-point day "+plainBig.ToString("0")+" → "+boostedBig.ToString("0")+")");}
   // 3. 名匠：30% 几率提一档；传说不再提；大师级作品给「得意之作」；挂在 GenerateQualityCreatedByPawn 上。
   {var a=HtPawn();var h=F2Adv(a);h.AddFeat(Feats5.MasterArtisan);
    Feats2.TestRoll=0f;var up=Feats5.Boost(a,QualityCategory.Normal);var leg=Feats5.Boost(a,QualityCategory.Legendary);Feats2.TestRoll=.99f;var same=Feats5.Boost(a,QualityCategory.Normal);Feats2.TestRoll=0f;var top=Feats5.Boost(a,QualityCategory.Masterwork);Feats2.TestRoll=null;
    bool pride=a.needs.mood.thoughts.memories.Memories.Any(m=>m.def.defName=="LP_FeatMasterworkPride");
    var patched=Harmony.GetPatchInfo(Feats5Quality.TargetMethod())?.Postfixes?.Any(x=>x.PatchMethod.DeclaringType==typeof(Feats5Quality))==true;
    Check(up==QualityCategory.Good && same==QualityCategory.Normal && leg==QualityCategory.Legendary && top==QualityCategory.Legendary && pride && patched && Near(Feats5.BoostChance(a),.3f),"[1.141.0] 名匠: normal → "+up+" on the 30% roll, unchanged on 99%, legendary stays; masterwork → "+top+" gives 得意之作; quality postfix hooked");}
   // 4. 符文锻师：在符文铁砧前提档 25%、工作速度 ×1.4、返还四分之一金属；普通台不提；与名匠同时只提一档。
   {var r=HtPawn(null,null,true,0,0);var h=F2Adv(r);h.AddFeat(Feats5.RuneSmith);
    var anvil=PfBench("LP_RuneAnvil");float wsOff=r.GetStatValue(StatDefOf.WorkSpeedGlobal,true,-1);
    PfWorkAt(r,anvil);bool at=Feats5.AtRuneBench(r);float wsOn=r.GetStatValue(StatDefOf.WorkSpeedGlobal,true,-1);
    Feats2.TestRoll=0f;var up=Feats5.Boost(r,QualityCategory.Normal);
    var knife=DefDatabase<RecipeDef>.GetNamed("Make_MeleeWeapon_Knife");var steel=ThingMaker.MakeThing(ThingDefOf.Steel);steel.stackCount=40;
    var made=GenRecipe.MakeRecipeProducts(knife,r,new List<Thing>{steel},steel,null).ToList();Feats2.TestRoll=null;
    int refund=made.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount);bool weapon=made.Any(t=>t.def==ThingDefOf.MeleeWeapon_Knife);
    PfWorkAt(r,null);Feats2.TestRoll=0f;var plain=Feats5.Boost(r,QualityCategory.Normal);Feats2.TestRoll=null;
    h.AddFeat(Feats5.MasterArtisan);PfWorkAt(r,anvil);Feats2.TestRoll=0f;var both=Feats5.Boost(r,QualityCategory.Normal);Feats2.TestRoll=null;float chance=Feats5.BoostChance(r);PfWorkAt(r,null);
    foreach(var t in made)if(!t.Destroyed)t.Destroy();
    Check(at && up==QualityCategory.Good && plain==QualityCategory.Normal && both==QualityCategory.Good && Near(chance,.3f) && wsOff>0f && Near(wsOn/wsOff,1.4f) && refund==10 && weapon && StatDefOf.WorkSpeedGlobal.parts.Any(x=>x is StatPart_Feats5Bench),"[1.141.0] 符文锻师: at the rune anvil quality +1 (not at no bench), with 名匠 still only +1 (chance "+chance+"), work speed ×"+(wsOn/wsOff).ToString("0.##")+", 40 steel → "+refund+" refunded");}
   // 5. 筑城大师 / 丰收之手 / 外科圣手：属性数值；修理的属性部件挂在建造速度上。
   {var b=HtPawn();var hb=F2Adv(b);var g=HtPawn();var hg=F2Adv(g);var d=HtPawn();var hd=F2Adv(d);
    // 先量没有专长时的基准（冒险者状态本身可能已经带着这些属性的系数），再比倍率 / 差值。
    float cs0=PfFactor(hb,"ConstructionSpeed"),cc0=PfOffset(hb,"ConstructSuccessChance"),pw0=PfFactor(hg,"PlantWorkSpeed"),py0=PfOffset(hg,"PlantHarvestYield"),ms0=PfFactor(hd,"MedicalSurgerySuccessChance"),mo0=PfFactor(hd,"MedicalOperationSpeed");
    hb.AddFeat(Feats5.MasterBuilder);hb.featScore.Clear();hb.Dirty();hg.AddFeat(Feats5.GreenThumb);hg.featScore.Clear();hg.Dirty();hd.AddFeat(Feats5.Surgeon);hd.featScore.Clear();hd.Dirty();
    float cs=PfFactor(hb,"ConstructionSpeed")/cs0,cc=PfOffset(hb,"ConstructSuccessChance")-cc0,pw=PfFactor(hg,"PlantWorkSpeed")/pw0,py=PfOffset(hg,"PlantHarvestYield")-py0,ms=PfFactor(hd,"MedicalSurgerySuccessChance")/ms0,mo=PfFactor(hd,"MedicalOperationSpeed")/mo0;
    bool ability=g.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_FeatGrowth"))!=null;hg.RemoveFeat(Feats5.GreenThumb);bool gone=g.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_FeatGrowth"))==null;
    bool repairPart=StatDefOf.ConstructionSpeed.parts?.Any(x=>x is StatPart_Feats5Repair)==true;float rf=StatPart_Feats5Repair.Factor(b);
    Check(Near(cs,1.35f) && Near(cc,.15f) && repairPart && Near(rf,1f) && Near(pw,1.3f) && Near(py,.15f) && ability && gone && Near(ms,1.25f) && Near(mo,1.5f),"[1.141.0] 筑城大师 construction ×"+cs+" / success +"+cc+" / repair part "+repairPart+" (idle factor "+rf+"); 丰收之手 plant work ×"+pw+" / yield +"+py+", 催生 granted "+ability+" / removed "+gone+"; 外科圣手 surgery success ×"+ms+" / speed ×"+mo);}
   // 6. 丰收之手「催生」：6 格内作物生长 +0.2，上限 1，远处不动；每天一次。
   {var g=HtPawn(null,null,true,4,4);var h=F2Adv(g);h.AddFeat(Feats5.GreenThumb);
    Plant Sow(int dx,int dz,float growth){var c=g.Position+new IntVec3(dx,0,dz);var old=c.GetPlant(map);if(old!=null)old.Destroy();var pl=(Plant)GenSpawn.Spawn(ThingDefOf.Plant_Potato,c,map);pl.Growth=growth;htThings.Add(pl);return pl;}
    var near=Sow(1,0,.5f);var ripe=Sow(0,2,.9f);var far=Sow(8,0,.5f);
    int n=Feats5.Growth(g);int again=Feats5.Growth(g);
    Check(n>=2 && Near(near.Growth,.7f) && Near(ripe.Growth,1f) && Near(far.Growth,.5f) && again==0 && h.UsesLeft(Feats5.GreenThumb)==0,"[1.141.0] 催生: "+n+" plants within 6 cells grow +0.2 (0.5 → "+near.Growth.ToString("0.##")+", 0.9 → "+ripe.Growth.ToString("0.##")+"), the one 8 cells away stays "+far.Growth.ToString("0.##")+"; second use today does nothing");}
   // 7. 外科圣手：致命 / 灾难性失败分支不出现（Apply 返回 false），轻微失败照旧。
   {var sg=HtPawn();F2Adv(sg).AddFeat(Feats5.Surgeon);var pt=HtPawn();var part=pt.RaceProps.body.corePart;var rec=new RecipeDef{defName="LP_TestSurgery",deathOnFailedSurgeryChance=1f};
    object Outcome(Type t,int damage,bool toPart){var o=Activator.CreateInstance(t);var tr=Traverse.Create(o);tr.Field("failure").SetValue(true);if(t!=typeof(SurgeryOutcome_Death)){tr.Field("chance").SetValue(1f);tr.Field("totalDamage").SetValue(damage);tr.Field("applyEffectsToPart").SetValue(toPart);}return o;}
    bool Apply(object o)=>(bool)AccessTools.Method(o.GetType(),"Apply").Invoke(o,new object[]{1f,rec,sg,pt,part});
    bool death=Apply(Outcome(typeof(SurgeryOutcome_Death),0,true));bool big=Apply(Outcome(typeof(SurgeryOutcome_Failure),65,true));bool wild=Apply(Outcome(typeof(SurgeryOutcome_Failure),65,false));
    int before=pt.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count();bool minor=Apply(Outcome(typeof(SurgeryOutcome_Failure),20,true));int after=pt.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count();
    var plainDoc=HtPawn();F2Adv(plainDoc);bool severe=Feats5NoCatastrophicSurgery.Severe((SurgeryOutcome)Outcome(typeof(SurgeryOutcome_Failure),65,true)) && !Feats5NoCatastrophicSurgery.Severe((SurgeryOutcome)Outcome(typeof(SurgeryOutcome_Failure),20,true));
    Check(!death && !pt.Dead && !big && !wild && minor && after>before && before==0 && severe,"[1.141.0] 外科圣手: fatal / catastrophic / ridiculous outcomes are skipped (patient alive, "+before+" injuries), the minor one still applies ("+after+" injuries)");}
   // 8. 造物之手：前置要名匠；「造物术」hediff 让下一次品质至少优秀，然后消失；没在做账单时能力不可用。
   {var f=HtPawn();var h=F2Adv(f);h.level=12;h.scores[Adventurer.Int]=15;f.skills.GetSkill(SkillDefOf.Crafting).Level=15;h.pendingASI=2;h.Dirty();
    string why=h.CanTakeFeat(Feats5.FabricateFeat);h.AddFeat(Feats5.MasterArtisan);string ok=h.CanTakeFeat(Feats5.FabricateFeat);h.AddFeat(Feats5.FabricateFeat);
    bool ability=f.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_FeatFabricate")) is Ability_Fabricate;string need=Feats5.FabricateReason(f);
    f.skills.GetSkill(SkillDefOf.Crafting).Level=0;Items.Give(f,"LP_FeatFabricated");Feats2.TestRoll=.99f;var q=QualityUtility.GenerateQualityCreatedByPawn(f,SkillDefOf.Crafting);Feats2.TestRoll=null;bool gone=!Items.Has(f,"LP_FeatFabricated");
    Feats2.TestRoll=.99f;var q2=QualityUtility.GenerateQualityCreatedByPawn(f,SkillDefOf.Crafting);Feats2.TestRoll=null;
    Check(why!=null && why.Contains("名匠") && ok==null && ability && need==Feats5.FabricateNeed && q>=QualityCategory.Excellent && gone && q2<QualityCategory.Excellent,"[1.141.0] 造物之手: needs 名匠 first ("+why+"); 造物术 ability is granted and unusable away from a bench ("+need+"); the hediff floors the next quality at "+q+" then disappears (next: "+q2+")");}
   // 9. 炼金大师：炼金桌产品 30% 多一份，普通台不多；炼金桌工作速度 ×1.35。
   {var al=HtPawn(null,null,true,-4,-4);var h=F2Adv(al);al.skills.GetSkill(SkillDefOf.Crafting).Level=6;h.AddFeat(Feats5.MasterAlchemist);
    var table=PfBench("LP_AlchemyTable");var recipe=DefDatabase<RecipeDef>.GetNamed("LP_BrewPotionHealing");var pd=recipe.products[0].thingDef;int per=recipe.products[0].count;
    float wsOff=al.GetStatValue(StatDefOf.WorkSpeedGlobal,true,-1);PfWorkAt(al,table);float wsOn=al.GetStatValue(StatDefOf.WorkSpeedGlobal,true,-1);
    Feats2.TestRoll=0f;var made=GenRecipe.MakeRecipeProducts(recipe,al,new List<Thing>(),null,null).ToList();Feats2.TestRoll=null;int atTable=made.Where(t=>t.def==pd).Sum(t=>t.stackCount);
    PfWorkAt(al,null);Feats2.TestRoll=0f;var plain=GenRecipe.MakeRecipeProducts(recipe,al,new List<Thing>(),null,null).ToList();Feats2.TestRoll=null;int elsewhere=plain.Where(t=>t.def==pd).Sum(t=>t.stackCount);
    foreach(var t in made.Concat(plain))if(!t.Destroyed)t.Destroy();
    Check(atTable==per*2 && elsewhere==per && Near(wsOn/wsOff,1.35f),"[1.141.0] 炼金大师: at the alchemy table "+pd.label+" ×"+per+" → "+atTable+" on the 30% roll, elsewhere "+elsewhere+"; work speed ×"+(wsOn/wsOff).ToString("0.##"));}
   // 10. 名匠 / 造物之手 / 符文锻师 / 炼金大师的 ExtraPrereq 文本。
   {string a=Feats5.MasterArtisan.PrereqText(),f=Feats5.FabricateFeat.PrereqText(),r=Feats5.RuneSmith.PrereqText(),m=Feats5.MasterAlchemist.PrereqText();
    Check(a.Contains("敏捷或智力 13") && a.Contains("8 级") && f.Contains("已学名匠") && f.Contains("12 级") && r.Contains("力量或智力 13") && m.Contains("手工或医疗 6"),"[1.141.0] prereq text: 名匠「"+a+"」, 造物之手「"+f+"」, 符文锻师「"+r+"」, 炼金大师「"+m+"」");}
  }
  void PrepareProductionFeatsSave(){var p=HtPawn(null,null,true,6,-6);var h=F2Adv(p);h.AddFeat(Feats5.PracticedHand);h.AddFeat(Feats5.Surgeon);Items.Give(p,"LP_FeatFabricated");pfId=p.ThingID;htMade.Remove(p);}
  void CheckProductionFeatsAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==pfId);var h=p==null?null:Adventurer.Get(p);
   Check(h!=null && h.HasFeat(Feats5.PracticedHand) && h.HasFeat(Feats5.Surgeon) && Items.Has(p,"LP_FeatFabricated") && Near(Feats5.DailyFull(p),120f),"[1.141.0] after load: 学以致用 / 外科圣手 and the 造物术 hediff survive");
   if(p!=null)p.Destroy();
  }
 }
}
