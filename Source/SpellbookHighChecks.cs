using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.129.0 法师 4～9 环：在 CheckSpellbook 末尾跑（-Only Spellbook），13 条。
 public partial class LichChecks {
  void SbHigh(List<Thing> made){
   // H1. 定义：反制法术 + 51 个 4～9 环（11 / 9 / 9 / 7 / 8 / 6），法力 2/3/4/5/6/9，等级 7…17，仪式 10、专注 10；8 环每天 2 次、9 环每天 1 次、次元门 2 次；塑能学派覆盖新塑能法术；力场墙、传送法阵。
   var hi=Spellbook.New.Where(a=>Spellbook.Circle(a)>=4).ToList();var by=hi.GroupBy(Spellbook.Circle).ToDictionary(g=>g.Key,g=>g.Count());
   bool counts=hi.Count==50 && by.TryGetValue(4)==11 && by.TryGetValue(5)==9 && by.TryGetValue(6)==9 && by.TryGetValue(7)==7 && by.TryGetValue(8)==8 && by.TryGetValue(9)==6 && HasDef("LP_WS_Counterspell") && Spellbook.Circle(Ab("LP_WS_Counterspell"))==3;
   bool mana=Enumerable.Range(4,6).Select(Spellbook.ManaFor).SequenceEqual(new[]{2,3,4,5,6,9}) && Enumerable.Range(4,6).Select(Spellbook.LevelFor).SequenceEqual(new[]{7,9,11,13,15,17}) && Spellbook.MaxCircle(17)==9 && Spellbook.MaxCircle(16)==8 && Spellbook.MaxCircle(20)==9;
   bool tags=hi.Count(Spellbook.Ritual)==10 && hi.Count(Spellbook.Concentration)==10 && Spellbook.DailyCap(Ab("LP_WS_Maze"))==2 && Spellbook.DailyCap(Ab("LP_WS_Wish"))==1 && Spellbook.DailyCap(Ab("LP_WS_DimensionDoor"))==2 && Spellbook.DailyCap(Ab("LP_WS_ChainLightning"))==0;
   bool evo=new[]{"LP_WS_WallOfFire","LP_WS_ConeOfCold","LP_WS_ChainLightning","LP_WS_Sunbeam","LP_WS_DelayedBlastFireball","LP_WS_Sunburst"}.All(n=>Spellbook.School(Ab(n))=="塑能" && Subclass3.Evo(Fx(n)));
   bool icons=hi.All(a=>a.uiIcon!=null && a.uiIcon!=BaseContent.BadTex && Fx(a.defName).cost==Spellbook.ManaFor(Spellbook.Circle(a)));
   Check(counts && mana && tags && evo && icons && DefCache<ThingDef>.Get("LP_WS_ForceWall")!=null && DefCache<ThingDef>.Get("LP_WS_TeleportCircle")!=null,
    "[counts "+counts+" mana "+mana+" tags "+tags+" evo "+evo+" icons "+icons+"] circles 4-9: 50 spells (11/9/9/7/8/6) + counterspell, mana 2/3/4/5/6/9 at wizard 7…17, 10 rituals, 10 concentration, 8th 2/day, 9th 1/day, dimension door 2/day");
   // H2. 每天次数：8 环两个法术合计 2 次；9 环 1 次；仪式的冷却按天（急制术）。
   var w=SbWizard(17,14,20);made.Add(w);var h=Adventurer.Get(w);h.level=20;h.Dirty();GenSpawn.Spawn(w,ScCell(18,0),map);h.classes.res[Spellbook.Wizard]=ClassSystem.MaxResource(h,Spellbook.Wizard);
   foreach(var n in new[]{"LP_WS_Maze","LP_WS_Sunburst","LP_WS_Wish","LP_WS_TimeStop","LP_WS_ChainLightning","LP_WS_Fabricate","LP_WS_DelayedBlastFireball","LP_WS_Counterspell"})Spellbook.Write(h,Ab(n));
   var d=Spellbook.Data(h);foreach(var a in Spellbook.BookList(h))if(!d.prepared.Contains(a))d.prepared.Add(a);
   string ok8=Spellbook.CannotCast(w,Ab("LP_WS_Maze"));Spellbook.UseDaily(w,Ab("LP_WS_Maze"));Spellbook.UseDaily(w,Ab("LP_WS_Sunburst"));string full8=Spellbook.CannotCast(w,Ab("LP_WS_Maze"));
   string ok9=Spellbook.CannotCast(w,Ab("LP_WS_TimeStop"));Spellbook.UseDaily(w,Ab("LP_WS_Wish"));string full9=Spellbook.CannotCast(w,Ab("LP_WS_TimeStop"));
   Check(ok8==null && full8!=null && full8.Contains("用完") && ok9==null && full9!=null && Spellbook.CannotCast(w,Ab("LP_WS_ChainLightning"))==null,"daily limits: two 8th-circle casts use up 8th for the day ("+full8+"), one 9th uses up 9th, other circles unaffected");
   d.daily.Clear();
   // H3. 反制法术：敌人施放的能力被打断（不生效、能力进冷却）；高环位成功率 −15%/环。
   WizardCounterspell.RollOverride=0f;Items.Give(w,"LP_WS_Counterspell",1800,1f);var foe=ScFoe(w);made.Add(foe);foe.abilities.GainAbility(Ab("LP_WS_FireBolt"));var fab=foe.abilities.GetAbility(Ab("LP_WS_FireBolt"));
   float hw=SbHurt(w);bool act=fab.Activate(new LocalTargetInfo(w),new LocalTargetInfo(w));bool countered=!act && !Items.Has(w,"LP_WS_Counterspell") && Near(SbHurt(w),hw) && fab.OnCooldown;WizardCounterspell.RollOverride=null;
   Check(countered && Near(WizardCounterspell.Chance(Ab("LP_WS_TimeStop")),.1f) && Near(WizardCounterspell.Chance(Ab("LP_WS_FireBolt")),1f),"counterspell: an enemy fire bolt within 20 cells is cancelled (stance used up, ability on cooldown); a 9th-circle spell only "+WizardCounterspell.Chance(Ab("LP_WS_TimeStop")).ToStringPercent());
   // H4. 变形术：敌人变兔形（武器收进背包、能力放不了），结束时拿回武器；友军变灰熊（荒野变形）。
   WizardSpells.RollOverride=0f;var f2=ScFoe(w);made.Add(f2);var gun=f2.equipment?.Primary;var cast=f2.abilities;f2.abilities.GainAbility(Ab("LP_WS_FireBolt"));
   bool poly=WizardSpells2.Polymorph(w,f2,3600) && Items.Has(f2,"LP_WS_Polymorphed") && f2.equipment.Primary==null && !f2.abilities.GetAbility(Ab("LP_WS_FireBolt")).CanCast.Accepted;
   Items.Remove(f2,"LP_WS_Polymorphed");bool back=gun==null || f2.equipment.Primary==gun;
   var ally=SbWizard(1);made.Add(ally);GenSpawn.Spawn(ally,NextTo(w),map);ClassEffects.Apply(w,Ab("LP_WS_Polymorph"),Fx("LP_WS_Polymorph"),new LocalTargetInfo(ally));bool bear=Items.Has(ally,"LP_WildShape");
   Check(poly && back && bear,"polymorph: a foe becomes a rabbit (weapon stowed: "+(gun?.LabelCap??"none")+", abilities blocked) and gets the weapon back; an ally becomes a bear (wild shape)");
   // H5. 放逐 / 迷宫：暂时离开地图，到时（或失去专注）回到原地；异界传送永久送走。
   var f3=ScFoe(w);made.Add(f3);var at=f3.Position;bool gone=Banishment.Banish(f3,60,"放逐") && !f3.Spawned && Banishment.IsBanished(f3);
   Banishment.Return(f3);bool returned=f3.Spawned && f3.Position.InHorDistOf(at,2f) && !Banishment.IsBanished(f3);
   var f4=ScFoe(w);made.Add(f4);ClassEffects.Apply(w,Ab("LP_WS_PlaneShift"),Fx("LP_WS_PlaneShift"),new LocalTargetInfo(f4));bool shifted=f4.Destroyed;
   var f5=ScFoe(w);made.Add(f5);ClassEffects.Apply(w,Ab("LP_WS_Banishment"),Fx("LP_WS_Banishment"),new LocalTargetInfo(f5));bool concB=d.conc==Ab("LP_WS_Banishment") && !f5.Spawned;Spellbook.EndConc(w,d,false);bool early=f5.Spawned;
   Check(gone && returned && shifted && concB && early,"banishment: a foe leaves the map and returns to the same spot; banishment holds while concentrating and ends early when concentration drops; plane shift removes a foe for good");
   // H6. 支配：敌人临时加入殖民地（专注），结束回原阵营。
   var f6=ScFoe(w);made.Add(f6);var orig=f6.Faction;bool dom=WizardSpells2.Dominate(w,Ab("LP_WS_DominatePerson"),f6,3600,.5f) && f6.Faction==Faction.OfPlayer && d.conc==Ab("LP_WS_DominatePerson");
   Spellbook.EndConc(w,d,false);WizardSpells2.Release(f6,orig);bool rel=f6.Faction==orig && !Items.Has(f6,"LP_WS_Dominated");
   Check(dom && rel,"dominate person: a foe fights for the colony while concentrating, then returns to "+orig?.Name);
   // H7. 区域：火墙是 8 格直线、伤敌；黑触手；延时爆裂火球按蓄力秒数炸开（不加法力）；厌恶之地分类。
   var fwCell=ScCell(26,6);var fw=SpellZone.Make(w,fwCell,"FireWall",.5f,3600);if(fw!=null){fw.line=WizardSpells2.Line(w,fwCell,8);made.Add(fw);}
   var dbf=Ab("LP_WS_DelayedBlastFireball");Spellbook.SetUp(w,dbf,9);int charge=Spellbook.UpLevel(w,dbf);int dbfCost=ClassLegendary.Cost(w,dbf,Fx("LP_WS_DelayedBlastFireball"));Spellbook.SetUp(w,dbf,0);
   bool kinds=WizardSpells2.AntipathyKind(foe,0) && !WizardSpells2.AntipathyKind(foe,3);
   Check(fw!=null && fw.Cells.Count()==8 && fw.Covers(fw.line[3]) && !fw.Covers(fw.line[0]+IntVec3.North*3+IntVec3.East*3) && charge==5 && dbfCost==5 && kinds,"zones: wall of fire is an 8-cell line; delayed blast fireball charges up to 5 s at no extra mana (cost "+dbfCost+"); antipathy sorts creatures by kind");
   fw?.Destroy();
   // H8. 伤害法术：连锁闪电跳向其他敌人；死亡法阵只伤敌人；阳炎爆致盲；锥形寒冰。
   var g1=ScFoe(w);made.Add(g1);var g2=ScFoe(w);made.Add(g2);foreach(var g in new[]{g1,g2})g.apparel?.DestroyAll();
   int chain=WizardSpells2.ChainLightning(w,g1,5f);float allyHurt=SbHurt(ally);ClassEffects.Apply(w,Ab("LP_WS_CircleOfDeath"),Fx("LP_WS_CircleOfDeath"),new LocalTargetInfo(w.Position));bool allySafe=Near(SbHurt(ally),allyHurt);
   var g3=ScFoe(w);made.Add(g3);ClassEffects.Apply(w,Ab("LP_WS_Sunburst"),Fx("LP_WS_Sunburst"),new LocalTargetInfo(g3.Position));bool blind=g3.Dead || Items.Has(g3,"LP_WS_Blinded");
   Check(chain>=2 && allySafe && blind,"damage spells: chain lightning hit "+chain+" foes; circle of death spares the ally; sunburst blinds");
   // H9. 石化术：三道石痕后开始石化（石化卷轴）。
   var f7=ScFoe(w);made.Add(f7);for(int i=0;i<3;i++)WizardSpells2.FleshToStone(w,f7);
   Check(Items.Has(f7,"LP_Petrifying") || f7.Destroyed || !f7.Spawned,"flesh to stone: the third mark starts the petrifying of the 石化卷轴");
   WizardSpells.RollOverride=null;
   // H10. 应急术：倒地或伤势过半时自动施放存的法术（护盾术）。
   var c=SbWizard(11);made.Add(c);GenSpawn.Spawn(c,ScCell(22,-6),map);c.apparel?.DestroyAll();WizardSpells2.SetContingency(c,Ab("LP_ShieldSpell"),600000);
   for(int i=0;i<30 && !c.Dead && !c.Downed && c.health.summaryHealth.SummaryHealthPercent>.5f;i++)c.TakeDamage(new DamageInfo(DamageDefOf.Cut,6f,0f,-1f,foe,c.health.hediffSet.GetNotMissingParts().Where(x=>x.coverageAbs>0f && x.depth==BodyPartDepth.Outside).RandomElement()));
   bool fired=!c.Dead && Items.Has(c,"LP_ShieldSpell") && Spellbook.Data(c).contingency==null;
   Check(fired,"contingency: the stored 护盾术 fires by itself once the wizard is badly hurt");
   // H11. 力场：力场墙每格耐久 300 × 强度、到时消散；力场牢笼围成一圈；时间停止定住别人、法术不进冷却。
   var walls=WizardSpells2.ForceWall(w,WizardSpells2.Line(w,ScCell(-20,18),8),450,1800);made.AddRange(walls);
   var cage=WizardSpells2.ForceCage(w,ScCell(-26,26),2f,1200);made.AddRange(cage);
   var f8=ScFoe(w);made.Add(f8);WizardSpells2.TimeStop(w,360);bool stopped=f8.stances.stunner.Stunned && !w.stances.stunner.Stunned && Items.Has(w,"LP_WS_TimeStop");
   var cl=w.abilities.GetAbility(Ab("LP_WS_ChainLightning"));cl.StartCooldown(1800);Spellbook.AfterCast(cl as Ability_Class);bool nocd=!cl.OnCooldown;
   Check(walls.Count>=6 && walls.All(x=>x.HitPoints==450) && cage.Count>=8 && stopped && nocd,"force: wall of force "+walls.Count+" cells at 450 HP, force cage ring "+cage.Count+" cells; time stop freezes others and wizard spells skip cooldown");
   // H12. 祈愿术：全体痊愈；反噬（33%）；传说魔法物品。
   var hurt=SbWizard(1);made.Add(hurt);GenSpawn.Spawn(hurt,ScCell(14,-10),map);hurt.apparel?.DestroyAll();hurt.TakeDamage(new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,foe,hurt.health.hediffSet.GetNotMissingParts().First(x=>x.coverageAbs>0f && x.depth==BodyPartDepth.Outside)));
   float before=SbHurt(hurt);WizardSpells2.WishHeal(w);float after=SbHurt(hurt);WizardSpells2.WishRoll=0f;string bl=WizardSpells2.Backlash(w);WizardSpells2.WishRoll=.99f;string none=WizardSpells2.Backlash(w);WizardSpells2.WishRoll=null;
   var item=WizardSpells2.MagicItem(QualityCategory.Legendary);if(item!=null)made.Add(item);
   Check(before>0f && Near(after,0f) && bl!="none" && Items.Has(w,"LP_WS_WishStress") && none=="none" && item!=null && item.TryGetComp<CompQuality>()?.Quality!=QualityCategory.Normal,"wish: heals every colonist ("+before.ToString("0")+" → 0); backlash "+bl+"; a magic item "+item?.LabelCap);
   // H13. 急制术：工作台的第一个账单立即完成一件，材料从地图上扣。
   var spot=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("CraftingSpot"));spot.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(spot,ScCell(-8,-14),map);made.Add(spot);
   var r=spot.def.AllRecipes.FirstOrDefault(x=>x.products!=null && x.products.Count==1 && x.ingredients.Count==1 && x.ingredients[0].IsFixedIngredient && x.workAmount<=3000f && x.researchPrerequisite==null && !x.products[0].thingDef.MadeFromStuff);
   var bill=r==null?null:(Bill_Production)r.MakeNewBill();if(bill!=null)((Building_WorkTable)spot).billStack.AddBill(bill);
   Thing mat=null;if(r!=null){mat=ThingMaker.MakeThing(r.ingredients[0].FixedIngredient);mat.stackCount=Mathf.Min(mat.def.stackLimit,Mathf.CeilToInt(r.ingredients[0].GetBaseCount())+5);GenPlace.TryPlaceThing(mat,spot.Position,map,ThingPlaceMode.Near);made.Add(mat);}
   int matBefore=mat==null?0:mat.stackCount;var products=r==null?new List<Thing>():WizardSpells2.Fabricate(w,spot,3000f);made.AddRange(products);
   Check(r!=null && products.Count==1 && products[0].def==r.products[0].thingDef && (mat.Destroyed || mat.stackCount<matBefore),"[fail "+WizardSpells2.FabricateFail+" mat "+(mat==null?"null":mat.def.defName+" "+mat.stackCount+" spawned "+mat.Spawned+" ing "+r.ingredients[0].filter.Allows(mat.def)+" bill "+bill.ingredientFilter.Allows(mat.def)+" fixed "+r.fixedIngredientFilter.Allows(mat.def)+" forb "+mat.IsForbidden(Faction.OfPlayer)+" per "+r.IngredientValueGetter.ValuePerUnitOf(mat.def)+" cat "+mat.def.category)+"] fabricate: crafting spot bill «"+r?.label+"» finished at once ("+products.FirstOrDefault()?.LabelCap+"), ingredients taken");
  }
  static bool HasDef(string n)=>DefDatabase<AbilityDef>.GetNamedSilentFail(n)!=null;
 }
}
