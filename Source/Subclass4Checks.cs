using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.111.0 器械师子职业：炼金师、装甲师、堡垒工程师（Test.ps1 -Only Subclass4）。
 public partial class LichChecks {
  void CheckSubclass4(){
   var made=new List<Thing>();
   Thing Put(Thing t){made.Add(t);return t;}
   Pawn Add(Pawn x){made.Add(x);return x;}
   try{
    // 1. 定义：3 个子职业（徽记、能力在 3/7/11/15）、18 个能力有图标并算器械师；药剂会坏、带精炼组件；6 个配方在药物实验室、只给炼金师；3 个建筑有贴图。
    var names=new[]{Subclass4.Alchemist,Subclass4.Armorer,Subclass4.Fortress};var subs=names.Select(n=>DefDatabase<LP_SubclassDef>.GetNamed(n)).ToList();var abs=subs.SelectMany(s=>s.abilities).ToList();
    var potions=Subclass4.Potions.Select(n=>DefDatabase<ThingDef>.GetNamed(n)).ToList();var recipes=DefDatabase<RecipeDef>.AllDefs.Where(r=>r.HasModExtension<AlchemistRecipeExtension>()).ToList();
    var blds=new[]{"LP_RuneMine","LP_ArcaneCapacitor","LP_FortressCore"}.Select(n=>DefDatabase<ThingDef>.GetNamed(n)).ToList();
    Check(subs.All(s=>s.Emblem!=null && s.Emblem!=s.parent.Background && s.parent.defName=="LP_Class_Artificer" && s.abilities.Select(e=>e.level).Distinct().OrderBy(x=>x).SequenceEqual(new[]{3,7,11,15})) && abs.Count==18 && abs.Select(e=>e.ability).Distinct().Count()==18
     && abs.All(e=>e.ability.abilityClass==typeof(Ability_Class) && e.ability.uiIcon!=null && e.ability.uiIcon!=BaseContent.BadTex && ClassSystem.ClassOf(e.ability)==ClassSubclass.SubOf(e.ability).parent)
     && potions.All(d=>d.HasComp(typeof(CompRottable)) && d.HasComp(typeof(CompAlchemistPotion)) && d.IsIngestible && d.graphic.MatSingle.mainTexture!=BaseContent.BadTex) && recipes.Count==6 && recipes.All(r=>r.recipeUsers.Any(u=>u.defName=="DrugLab"))
     && blds.All(d=>d.graphic.MatSingle.mainTexture!=BaseContent.BadTex),"subclass4: 3 artificer subclasses (3/7/11/15), 18 abilities with icons, 6 perishable potions, 6 drug-lab recipes, 3 textured buildings");
    // 2. 炼金师 15：每天 3 瓶精炼药剂；指定药剂循环；治愈药剂治伤；迅捷药剂持续 1.3 天；复原试剂治流感；大复原长回手；免疫火焰。
    var al=Add(S2Pawn("LP_Class_Artificer",15,Subclass4.Alchemist,15));al.SetFaction(Faction.OfPlayer);ScSpawn(al,ScCell(-10,0));var hal=Adventurer.Get(al);Subclass4.Refresh(hal);
    ClassSubclass.Data(hal).ticks.Remove("alDay");Subclass4.Tick250(hal);var got=al.inventory.innerContainer.Where(t=>t.TryGetComp<CompAlchemistPotion>()!=null).ToList();int n0=got.Sum(t=>t.stackCount);bool refined=got.All(t=>t.TryGetComp<CompAlchemistPotion>().refined);
    S2Cast(al,"LP_AL_PickPotion",al);int pick=Subclass4.Pick(al);
    al.TakeDamage(new DamageInfo(DamageDefOf.Cut,15f,0f,-1f,null,al.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso)));float hurt0=ClassMelee.InjurySum(al);Subclass4.Drink(al,"heal",1.3f);float hurt1=ClassMelee.InjurySum(al);
    Subclass4.Drink(al,"Swift",1.3f);int swift=Items.TicksLeft(al,"LP_ALB_Swift");
    al.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Flu"));string cured=Subclass4.Drink(al,"restore",1f);bool flu=al.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("Flu"));
    var hand=al.health.hediffSet.GetNotMissingParts().First(x=>x.def.defName=="Hand");al.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,al,hand),hand);bool lost=al.health.hediffSet.PartIsMissing(hand);
    S2Cast(al,"LP_AL_GreaterRestorative",al);var greater=al.inventory.innerContainer.FirstOrDefault(t=>t.def.defName=="LP_ALP_GreaterRestorative");Subclass4.Drink(al,"greater",1f);bool back=!al.health.hediffSet.PartIsMissing(hand);
    float fire=Subclass4.DamageFactor(al,Dmg(DamageDefOf.Flame,null));
    Check(n0==3 && refined && pick==1 && hurt1<hurt0 && swift>70000 && cured!=null && !flu && lost && greater!=null && back && Near(fire,0f) && Items.Has(al,"LP_AL_SavantBuff") && Items.Has(al,"LP_AL_ResistBuff"),"alchemist 15: 3 refined potions a day ("+n0+"), pick cycles, healing ("+hurt0+"→"+hurt1+"), swift lasts "+swift+", restorative cured "+cured+", greater restorative regrows a hand, immune to fire");
    // 3. 配方门槛：器械师 11 级的炼金师能做实验药剂，别的器械师不行；炼金师 11 级毒、酸 ×0.5；做出来的药剂是精炼的。
    var al11=Add(S2Pawn("LP_Class_Artificer",11,Subclass4.Alchemist,11));var bs=Add(S2Pawn("LP_Class_Artificer",11,Subclass2.BattleSmith,11));Subclass4.Refresh(Adventurer.Get(al11));
    var rec=DefDatabase<RecipeDef>.GetNamed("LP_Make_ALP_Swift");bool canAl=rec.PawnSatisfiesSkillRequirements(al11),canBs=rec.PawnSatisfiesSkillRequirements(bs);
    float acid=Subclass4.DamageFactor(al11,Dmg(DefDatabase<DamageDef>.GetNamed("LP_OozeAcid"),null));
    var prod=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_ALP_Swift"));prod.TryGetComp<CompAlchemistPotion>().Notify_RecipeProduced(al11);
    Check(canAl && !canBs && Near(acid,.5f) && prod.TryGetComp<CompAlchemistPotion>().refined && prod.LabelNoCount.Contains("精炼"),"alchemist recipes: alchemist can brew, battle smith can't; acid ×"+acid+"; brewed potion is refined");
    // 4. 装甲师 15：没有工作台时拒绝；把防弹衣改成奥术装甲（强化 +2）；守护者力场 (10+2×15)×2；渗透者闪电发射器打没开过火的敌人 12×1.5×2；远程闪避；装甲打不烂。
    var ar=Add(S2Pawn("LP_Class_Artificer",15,Subclass4.Armorer,15));ar.SetFaction(Faction.OfPlayer);ScSpawn(ar,ScCell(10,0));var har=Adventurer.Get(ar);Subclass4.Refresh(har);
    var vest=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_FlakVest"));ar.apparel.Wear(vest,false);int enh0=GearEnhance.Level(vest);
    string noBench=Subclass4.Invalid(ar,Fx("LP_AR_ArcaneArmor"),ar);
    var bench=Put(GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("CraftingSpot")),NextTo(ar),map));string okBench=Subclass4.Invalid(ar,Fx("LP_AR_ArcaneArmor"),ar);
    bool conv=S2Cast(ar,"LP_AR_ArcaneArmor",ar) && Subclass4.ArcaneArmor(ar)==vest;int enh1=GearEnhance.Level(vest);Subclass4.Tick250(har);bool on=Items.Has(ar,"LP_AR_ArmorOn");
    S2Cast(ar,"LP_AR_Field",ar);var sh=ar.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ShieldSpell"));
    S2Cast(ar,"LP_AR_ArmorMode",ar);bool infil=Subclass4.Infiltrator(ar);var foe=Add(ScFoe(ar));float launch=Subclass4.LauncherDamage(ar,foe)/Subclass4.Artifice(ar);
    float evade;ClassMartial2.RollOverride=0f;try{evade=Subclass4.DamageFactor(ar,new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,foe));}finally{ClassMartial2.RollOverride=null;}
    vest.TakeDamage(new DamageInfo(DamageDefOf.Cut,9999f,0f,-1f,null));bool kept=!vest.Destroyed && vest.HitPoints>=1;
    Check(noBench!=null && okBench==null && conv && enh1==enh0+2 && on && sh!=null && Near(sh.Severity,80f) && infil && Near(launch,36f) && Near(evade,0f) && kept,"armorer 15: needs a workbench ("+noBench+"), flak vest becomes arcane armor (+"+enh0+"→+"+enh1+"), field "+(sh?.Severity??0f)+", launcher ×"+launch+" on a fresh foe, ranged evade, armor can't break");
    // 5. 装甲师·完美装甲：守护者把敌人拉到身边；渗透者命中后标记，队友下一击 ×1.5。
    S2Cast(ar,"LP_AR_ArmorMode",ar);var far=Add(AdvPawn(null,AdvEnemy));GenSpawn.Spawn(far,CellFinder.RandomClosewalkCellNear(ar.Position+new IntVec3(4,0,0),map,1,c=>c.Standable(map) && c.GetFirstPawn(map)==null && !c.AdjacentTo8WayOrInside(ar.Position)),map);
    var pulled=Subclass4.Pull(ar);bool adj=pulled!=null && pulled.Position.AdjacentTo8WayOrInside(ar.Position) && Items.Has(pulled,"LP_Taunted");
    S2Cast(ar,"LP_AR_ArmorMode",ar);Subclass4.DamageFactor(foe,Dmg(DamageDefOf.Cut,ar));bool marked=Items.Has(foe,"LP_AR_Marked");var mate=Add(S2Pawn("LP_Class_Fighter",1,null));mate.SetFaction(Faction.OfPlayer);float bonus=Subclass4.DamageFactor(foe,Dmg(DamageDefOf.Cut,mate));
    Check(adj && marked && Near(bonus,1.5f) && !Items.Has(foe,"LP_AR_Marked"),"armorer perfected: guardian pulls a foe beside and taunts, infiltrator marks and the next ally hit ×"+bonus);
    // 6. 堡垒工程师 15：加固墙最大生命 ×1.5；符文地雷最多 3 个、踩中伤人；战地加固让墙受伤 ×0.25；奥术电容上限 5、让炮台射速 ×1.15；全线升级炮台伤害 ×1.3；要塞核心修墙。
    var fe=Add(S2Pawn("LP_Class_Artificer",15,Subclass4.Fortress,15));fe.SetFaction(Faction.OfPlayer);ScSpawn(fe,ScCell(0,12));var hfe=Adventurer.Get(fe);Subclass4.Refresh(hfe);
    var wall=Put(GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall,ThingDefOf.Steel),ScCell(4,16),map));float wmax0=wall.def.GetStatValueAbstract(StatDefOf.MaxHitPoints,wall.Stuff);Subclass4.Reinforce(wall);int whpR=wall.HitPoints;float wmax1=wall.MaxHitPoints;
    var m1=Subclass4.PlaceMine(fe,ScCell(-4,16));var m2=Subclass4.PlaceMine(fe,ScCell(-6,16));var m3=Subclass4.PlaceMine(fe,ScCell(-8,16));foreach(var m in new Thing[]{m1,m2,m3})if(m!=null)made.Add(m);
    string fourth=Subclass4.Invalid(fe,Fx("LP_FE_RuneMine"),new LocalTargetInfo(ScCell(-10,16)));var foe2=Add(ScFoe(fe));float hp0=ClassMelee.InjurySum(foe2);Subclass4.MineBlast(m1,foe2);float hp1=ClassMelee.InjurySum(foe2);
    int fort=Subclass4.Fortify(fe,wall.Position);int whp0=wall.HitPoints;wall.TakeDamage(new DamageInfo(DamageDefOf.Blunt,40f,0f,-1f,null));int lostHp=whp0-wall.HitPoints;
    int cap=Subclass4.CapacitorLimit();var turret=(Building_TurretGun)Put(GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Turret_MiniTurret"),ThingDefOf.Steel),ScCell(8,20),map));turret.SetFaction(Faction.OfPlayer);
    var cd=AccessTools.Method(typeof(Building_TurretGun),"BurstCooldownTime");float rate0=(float)cd.Invoke(turret,null);
    var capB=Put(GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_ArcaneCapacitor")),turret.Position+IntVec3.East*2,map));capB.SetFaction(Faction.OfPlayer);float rate1=(float)cd.Invoke(turret,null);
    var core=(Building_FortressCore)Put(GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FortressCore")),ScCell(8,26),map));core.SetFaction(Faction.OfPlayer);
    int up=Subclass4.Upgrade(core);float tdmg=Subclass4.DamageFactor(foe2,new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,turret));
    wall.HitPoints=wall.MaxHitPoints/2;int wh0=wall.HitPoints;bool inRange=wall.Position.InHorDistOf(core.Position,Subclass4.CoreRadius);Subclass4.CoreRepair(core);int wh1=wall.HitPoints;
    Check(Near(wmax1/wmax0,1.5f) && whpR==Mathf.RoundToInt(wmax0*1.5f) && m3!=null && fourth!=null && hp1>hp0 && fort>=1 && lostHp>0 && lostHp<=16 && cap==5 && rate1<rate0*.9f && up>=1 && Near(tdmg,1.3f) && (!inRange || wh1>wh0),
     "fort engineer 15: reinforced wall max ×"+(wmax1/wmax0)+", 3 mines then refuse ("+fourth+"), mine hurts, fortify ("+fort+") takes "+lostHp+" of 40, 5 capacitors, turret cooldown "+rate0+"→"+rate1+", upgrade ×"+tdmg+", core repairs "+wh0+"→"+wh1);
    // 7. 轻盈药剂：走路无视地形（登记到 LightFeet，药效消失时移除）。
    Subclass4.Drink(mate,"Light",1f);bool light=Subclass4.LightFeet.Contains(mate);Items.Remove(mate,"LP_ALB_Light");
    Check(light && !Subclass4.LightFeet.Contains(mate),"light potion registers the drinker for terrain-free walking and unregisters when it ends");
   }finally{
    ClassMartial2.RollOverride=null;
    foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn pp && pp.Dead){pp.Corpse?.Destroy();continue;}t.Destroy();}
   }
  }
 }
}
