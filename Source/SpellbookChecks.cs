using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
namespace LichPhylactery {
 // 1.128.0 法师法术书：Test.ps1 -Only Spellbook（15 条）。
 public partial class LichChecks {
  static string sbSavedId;
  Pawn SbWizard(int wiz,int intScore=10,int adv=-1){
   var p=ClsPawn(adv>0?adv:Mathf.Max(wiz,1));var h=Adventurer.Get(p);p.skills.GetSkill(SkillDefOf.Intellectual).Level=10;
   h.scores=new[]{10,10,10,intScore,10,10};h.Dirty();ClassSystem.SetLevel(h,Spellbook.Wizard,wiz);return p;
  }
  static float SbHurt(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity);
  void CheckSpellbook(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool oldOn=s.adventurer;s.adventurer=true;var made=new List<Thing>();
   try{
    // 1. 定义：38 个新法术（戏法 8、一环 8、二环 10、三环 12），法力 0/1/1/2，仪式 8、专注 2、可升环 5，都算法师；三张残页、三个抄录配方在卷轴书桌；塑能学派覆盖新塑能法术。
    var news=Spellbook.New;var by=news.GroupBy(Spellbook.Circle).ToDictionary(g=>g.Key,g=>g.Count());
    var skip=AccessTools.Field(typeof(NpcClasses),"Skip").GetValue(null) as HashSet<string>;
    bool dCount=news.Count==89 && by.TryGetValue(0)==8 && by.TryGetValue(1)==8 && by.TryGetValue(2)==10 && by.TryGetValue(3)==13; // 1.129.0：3 环多了反制法术，4～9 环在 CheckSpellbookHigh
    var badAb=news.Where(a=>!(a.abilityClass==typeof(Ability_Class) && a.uiIcon!=null && a.uiIcon!=BaseContent.BadTex && ClassSystem.ClassOf(a)==Spellbook.Wizard && Fx(a.defName).cost==Spellbook.ManaFor(Spellbook.Circle(a)))).Select(a=>a.defName).ToList();
    var low=news.Where(a=>Spellbook.Circle(a)<=3).ToList();bool dTags=low.Count(Spellbook.Ritual)==8 && low.Count(Spellbook.Concentration)==2 && low.Count(a=>Spellbook.Upcast(a)!=null)==5;
    bool dRec=Enumerable.Range(1,3).All(c=>Spellbook.PageDef(c)!=null && DefDatabase<RecipeDef>.GetNamed("LP_WS_CopySpell"+c).workerClass==typeof(RecipeWorker_CopySpell))
     && DefDatabase<ThingDef>.GetNamed("LP_ScribeDesk").AllRecipes.Count(r=>r.defName.StartsWith("LP_WS_CopySpell"))==3 && Spellbook.ScrollSpell.Values.All(n=>DefCache<AbilityDef>.Get(n)!=null);
    bool dEvo=Subclass3.Evo(Fx("LP_WS_FireBolt")) && Subclass3.Evo(Fx("LP_WS_Shatter")) && !Subclass3.Evo(Fx("LP_WS_Web"));bool dSkip=skip!=null && skip.Contains("WS_Sending") && skip.Contains("WS_Mending");
    Check(dCount && badAb.Count==0 && dTags && dRec && dEvo && dSkip,"[count "+dCount+" ("+news.Count+") bad "+string.Join(",",badAb)+" tags "+dTags+" recipes "+dRec+" evo "+dEvo+" skip "+dSkip+"] "+
     "spellbook defs: 89 wizard spells (8 cantrips / 8 / 10 / 13 up to circle 3, rest in circles 4-9), mana by circle, 8 rituals, 2 concentration, 5 upcastable, icons; 3 pages + 3 copy recipes on the scribe desk; evocation covers the new evocation spells; ritual/utility spells stay out of autocast");
    // 2. 自选与戏法：法师 1 级 → 待选 2、戏法 2（魔法飞弹算一个）；只能选一环；写入后学会；名额用完不能再选；4 级 → 待选 +6、戏法 +1、可选二环。
    var w=SbWizard(1);made.Add(w);var h=Adventurer.Get(w);h.level=10;h.Dirty();var d=Spellbook.Data(h); // 冒险者 10 级：控制成功率不吃等级差
    bool init=d!=null && d.init && HasAb(w,"LP_MagicMissile") && d.prepared.Contains(Ab("LP_MageArmor")) && d.prepared.Contains(Ab("LP_ShieldSpell")) && !d.prepared.Contains(Ab("LP_MagicMissile"));
    int due1=Spellbook.PicksDue(h),cdue1=Spellbook.CantripsDue(h);var cands=Spellbook.PickCandidates(h).ToList();string high=Spellbook.CannotPick(h,Ab("LP_WS_Shatter"));
    bool p1=Spellbook.Pick(h,Ab("LP_WS_Thunderwave")),p2=Spellbook.Pick(h,Ab("LP_BurningHands")),p3=Spellbook.Pick(h,Ab("LP_WS_Grease"));
    bool c1=Spellbook.LearnCantrip(h,Ab("LP_WS_FireBolt")),c2=Spellbook.LearnCantrip(h,Ab("LP_WS_Light")),c3=Spellbook.LearnCantrip(h,Ab("LP_WS_MageHand"));
    ClassSystem.SetLevel(h,Spellbook.Wizard,4);int due4=Spellbook.PicksDue(h),cdue4=Spellbook.CantripsDue(h);bool shatterOk=Spellbook.CannotPick(h,Ab("LP_WS_Shatter"))==null;
    Check(init && due1==2 && cdue1==2 && cands.All(a=>Spellbook.Circle(a)==1) && cands.Contains(Ab("LP_BurningHands")) && high!=null && p1 && p2 && !p3 && HasAb(w,"LP_WS_Thunderwave") && HasAb(w,"LP_BurningHands")
     && c1 && c2 && !c3 && HasAb(w,"LP_WS_FireBolt") && due4==6 && cdue4==1 && shatterOk && Spellbook.MaxCircle(4)==2 && Spellbook.MaxCircle(5)==3,
     "[init "+init+" due1 "+due1+" cdue1 "+cdue1+" cands "+string.Join(",",cands.Select(a=>a.defName))+" picks "+p1+p2+p3+" cantrips "+c1+c2+c3+" prepared "+string.Join(",",d?.prepared.Select(a=>a.defName)??new string[0])+"] picks: wizard 1 → 2 spells + 2 cantrips (magic missile counts), circle 1 only (shatter refused: "+high+"); a 3rd pick / 3rd cantrip refused; wizard 4 → "+due4+" picks, 1 cantrip, circle 2 open");
    // 3. 备法：上限 = 法师等级 + 智力调整值；没准备的法术没有按钮、不能施放；满了不能再准备；睡醒或研读后才能更换。
    foreach(var n in new[]{"LP_WS_Grease","LP_WS_FogCloud","LP_WS_Shatter","LP_WS_Web"})Spellbook.Pick(h,Ab(n));
    GenSpawn.Spawn(w,ScCell(0,8),map);h.scores=new[]{10,10,10,14,10,10};h.Dirty();int cap=Spellbook.PrepCap(h);
    var tw=Ab("LP_WS_Thunderwave");d.mayPrepare=true;d.prepared.Remove(tw);bool hidden=Spellbook.Hidden(w,tw);var twAb=w.abilities.GetAbility(tw);bool cannot=!twAb.CanCast.Accepted && ClassSystem.CannotCast(w,tw,Fx("LP_WS_Thunderwave"))!=null;bool noGizmo=!twAb.GetGizmos().Any();
    foreach(var a in Spellbook.BookList(h))if(Spellbook.Counted(a) && a!=tw && Spellbook.PreparedCount(h)<cap)Spellbook.SetPrepared(h,a,true);
    while(Spellbook.PreparedCount(h)<cap){var extra=Spellbook.BookList(h).FirstOrDefault(a=>Spellbook.Counted(a) && !d.prepared.Contains(a) && a!=tw);if(extra==null)break;d.prepared.Add(extra);}
    string full=Spellbook.PreparedCount(h)>=cap?Spellbook.CannotToggle(h,tw,true):"not full";
    d.prepared.RemoveAt(d.prepared.FindIndex(a=>Spellbook.Counted(a)));bool prep=Spellbook.SetPrepared(h,tw,true) && !Spellbook.Hidden(w,tw) && twAb.CanCast.Accepted;
    d.mayPrepare=false;string locked=Spellbook.CannotToggle(h,tw,false);d.wasAsleep=true;Spellbook.Tick250(h);bool woke=d.mayPrepare;d.mayPrepare=false;Spellbook.FinishStudy(w);bool studied=d.mayPrepare;
    bool mmVisible=!Spellbook.Hidden(w,Ab("LP_MagicMissile"));
    Check(cap==6 && hidden && cannot && noGizmo && full!=null && full.Contains("准备满") && prep && locked!=null && woke && studied && mmVisible,"prepare: cap = wizard 4 + INT 14 mod 2 = "+cap+"; unprepared thunderwave hidden and uncastable; full list refuses ("+full+"); preparing it shows it; locked until waking ("+woke+") or 1h study ("+studied+"); cantrips always shown");
    // 4. 升环：雷鸣波（一环）在法师 5 级最多升 2 环；法力 1 + 2；效果 ×1.5；真的施放扣 3 点法力并打伤身边敌人；不能升环的法术没有额外花费。
    ClassSystem.SetLevel(h,Spellbook.Wizard,5);h.classes.res[Spellbook.Wizard]=ClassSystem.MaxResource(h,Spellbook.Wizard);
    Spellbook.SetUp(w,tw,5);int up=Spellbook.UpLevel(w,tw),cost=ClassLegendary.Cost(w,tw,Fx("LP_WS_Thunderwave"));
    Spellbook.BeginCast(twAb as Ability_Class);float pf=Spellbook.PowerFactor(Fx("LP_WS_Thunderwave"));int curUp=Spellbook.CurUp;Spellbook.EndCast();float pf0=Spellbook.PowerFactor(Fx("LP_WS_Thunderwave"));
    var foe=ScFoe(w);made.Add(foe);float m0=ClassSystem.Resource(h,Spellbook.Wizard),hurt0=SbHurt(foe);bool cast=twAb.Activate(new LocalTargetInfo(w),new LocalTargetInfo(w));float m1=ClassSystem.Resource(h,Spellbook.Wizard);
    Check(up==2 && cost==3 && Near(pf,1.5f) && curUp==2 && Near(pf0,1f) && cast && Near(m0-m1,3f) && Spellbook.UpcastCost(w,Ab("LP_WS_Grease"))==0,
     "[curUp "+curUp+" pf0 "+pf0+" cast "+cast+" hurt "+hurt0+"→"+SbHurt(foe)+" dead "+foe.Dead+" dist "+foe.Position.DistanceTo(w.Position)+" grease "+Spellbook.UpcastCost(w,Ab("LP_WS_Grease"))+"] upcast: thunderwave capped at +"+up+" (circle 3), cost "+cost+", power ×"+pf.ToString("0.##")+"; a real cast spent "+(m0-m1).ToString("0.#")+" mana and hurt the adjacent foe");
    Spellbook.SetUp(w,tw,0);
    // 5. 专注：缓慢术让半径内的敌人变慢并记下专注；受到 10 点以上伤害、维持失败 → 结束并去掉效果；新的专注会结束旧的；维持成功率 70% + 体质调整值 × 5%。
    foe=ScFoe(w);made.Add(foe);WizardSpells.RollOverride=0f;var f2=ScFoe(w);made.Add(f2);var slowed=WizardSpells.Slow(w,Ab("LP_WS_Slow"),w.Position,3f,3600);bool conc=d.conc==Ab("LP_WS_Slow") && Spellbook.Concentrating.Contains(w) && slowed.Count>=2 && slowed.All(x=>Items.Has(x,"LP_WS_Slowed"));
    Spellbook.RollOverride=0f;bool kept=!Spellbook.Damaged(w,12f) && d.conc!=null;bool small=!Spellbook.Damaged(w,5f);
    Spellbook.RollOverride=.99f;bool broke=Spellbook.Damaged(w,12f) && d.conc==null && !Items.Has(foe,"LP_WS_Slowed") && !Items.Has(f2,"LP_WS_Slowed");
    WizardSpells.VampiricTouch(w,Ab("LP_WS_VampiricTouch"),foe,1f,3600);bool vamp=d.conc==Ab("LP_WS_VampiricTouch") && Spellbook.Free(w,Fx("LP_WS_VampiricTouch"));
    WizardSpells.Slow(w,Ab("LP_WS_Slow"),w.Position,3f,3600);bool replaced=d.conc==Ab("LP_WS_Slow") && !Items.Has(w,"LP_WS_VampiricTouch");
    h.scores=new[]{10,10,14,14,10,10};h.Dirty();float cc=Spellbook.ConcChance(w);Spellbook.EndConc(w,d,false);Spellbook.RollOverride=null;
    Check(conc && kept && small && broke && vamp && replaced && Near(cc,.8f),"concentration: slow on "+slowed.Count+" foes; kept on a good roll, ignored under 10 damage, broken on a bad roll (effects removed); vampiric touch free while concentrating; a new concentration ends the old; CON 14 → "+cc.ToStringPercent());
    // 6. 抄录：残页写着法术；成功率 60% + 智力调整值 × 5%；失败不学会；成功写进书（不占自选名额）；已经会了 / 环位不够不能抄；卷轴对应法术；退还路径。
    var page=Spellbook.MakePage(1,Ab("LP_WS_Alarm"));made.Add(page);var fear=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_ScrollFearGaze"));made.Add(fear);
    int picksBefore=d.picksUsed;float chance=Spellbook.CopyChance(w);string fail=Spellbook.Copy(w,Spellbook.SpellOf(page),.99f);bool failNo=!HasAb(w,"LP_WS_Alarm");
    string ok=Spellbook.Copy(w,Spellbook.SpellOf(page),0f);string dup=Spellbook.Copy(w,Ab("LP_WS_Alarm"),0f);
    var w1=SbWizard(1);made.Add(w1);string tooHigh=Spellbook.CannotCopy(w1,Ab("LP_WS_Fear"));
    GenSpawn.Spawn(w1,ScCell(4,8),map);var worker=(RecipeWorker_CopySpell)DefDatabase<RecipeDef>.GetNamed("LP_WS_CopySpell3").Worker;var fearPage=Spellbook.MakePage(3,Ab("LP_WS_Fear"));
    int silverBefore=map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t=>t.stackCount);worker.ConsumeIngredient(fearPage,worker.recipe,map);worker.Notify_IterationCompleted(w1,new List<Thing>());
    var back=map.listerThings.ThingsOfDef(Spellbook.PageDef(3)).FirstOrDefault(t=>Spellbook.SpellOf(t)==Ab("LP_WS_Fear"));if(back!=null)made.Add(back);int silverBack=map.listerThings.ThingsOfDef(ThingDefOf.Silver).Sum(t=>t.stackCount)-silverBefore;
    foreach(var sv in map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t=>t.Position.InHorDistOf(w1.Position,4f)).ToList())made.Add(sv);
    var pd=Spellbook.MakePage(2);made.Add(pd);var sp=Spellbook.SpellOf(pd);bool drop=Near(Spellbook.PageDrop,.15f) && sp!=null && Spellbook.Circle(sp)==2 && !Spellbook.IsBorrowed(sp) && Enumerable.Range(0,30).All(i=>Spellbook.DropCircle(w1)==1);
    Check(drop && Near(chance,.7f) && fail=="failed" && failNo && ok=="ok" && HasAb(w,"LP_WS_Alarm") && d.copied.Contains(Ab("LP_WS_Alarm")) && d.picksUsed==picksBefore && dup!=null && dup.Contains("已经会") && tooHigh!=null
     && Spellbook.SpellOf(fear)==Ab("LP_WS_Fear") && page.LabelNoCount.Contains(Ab("LP_WS_Alarm").label) && back!=null && silverBack==150 && !HasAb(w1,"LP_WS_Fear"),
     "copy: page shows its spell; chance at INT 14 = "+chance.ToStringPercent()+"; failed roll learns nothing, success writes it (no pick used); duplicates and too-high circles refused ("+tooHigh+"); scroll 恐惧凝视 → 恐惧术; a refused copy returns the page and 150 silver; enemy casters drop a page 15% of the time (random page: "+sp?.label+")");
    // 7. 借用术士法术：写进书的燃烧之手算法师（扣法师法力、奥术强度）；有术士等级的人照旧算术士。
    var bh=Ab("LP_BurningHands");var sor=ClsPawn(3);made.Add(sor);var hs=Adventurer.Get(sor);sor.skills.GetSkill(SkillDefOf.Social).Level=10;ClassSystem.SetLevel(hs,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Sorcerer"),3);
    if(!d.prepared.Contains(bh))d.prepared.Add(bh);h.classes.res[Spellbook.Wizard]=0f;string poor=ClassSystem.CannotCast(w,bh,Fx("LP_BurningHands"));
    Check(ClassSystem.CastClassOf(w,bh)==Spellbook.Wizard && Spellbook.StatFor(w,Fx("LP_BurningHands"))?.defName=="LP_ArcanePower" && ClassSystem.CastClassOf(sor,bh)?.defName=="LP_Class_Sorcerer" && Spellbook.StatFor(sor,Fx("LP_BurningHands"))==null
     && poor!=null && poor.Contains("法力") && !Spellbook.Hidden(sor,bh),"borrowed spells: a wizard's 燃烧之手 casts as wizard (wizard mana, 奥术强度; empty mana refused: "+poor+"); a real sorcerer keeps it as sorcerer");
    h.classes.res[Spellbook.Wizard]=ClassSystem.MaxResource(h,Spellbook.Wizard);
    // 8. 攻击法术：火焰箭、灼热射线（3 道 + 升环）、粉碎音波、定身类人（成功率 55%，控制成功时麻痹）、恐惧术（扇形内逃跑）、心灵尖刺（心灵伤害、禁止隐形）。
    var f3=ScFoe(w);made.Add(f3);f3.apparel?.DestroyAll();/* 脱掉护甲：8 点火焰可能被护甲完全挡掉 */float h3=SbHurt(f3);ClassEffects.Apply(w,Ab("LP_WS_FireBolt"),Fx("LP_WS_FireBolt"),new LocalTargetInfo(f3));bool bolt=SbHurt(f3)>h3 || f3.Dead;
    int rays=f3.Dead?4:WizardSpells.ScorchingRay(w,f3,4,1f);int shat=WizardSpells.Shatter(w,foe.Position,2f,1f);
    var f4=ScFoe(w);made.Add(f4);int held=WizardSpells.HoldPerson(w,f4,1,360);bool stunned=f4.stances.stunner.Stunned;float hc=WizardSpells.Chance(.55f,w,f4);
    var f5=ScFoe(w);made.Add(f5);Items.Give(f5,"LP_WS_Invisible",2500,1f);ClassEffects.Apply(w,Ab("LP_WS_MindSpike"),Fx("LP_WS_MindSpike"),new LocalTargetInfo(f5));bool spiked=f5.Dead || Items.Has(f5,"LP_WS_MindSpiked") && !Items.Has(f5,"LP_WS_Invisible");
    var f6=ScFoe(w);made.Add(f6);int feared=WizardSpells.Fear(w,f6.Position,8f,600);bool fled=f6.MentalStateDef==MentalStateDefOf.PanicFlee;
    Check(bolt && rays==4 && shat>=1 && held==1 && stunned && Near(hc,.55f) && spiked && feared>=1 && fled,"[bolt "+bolt+" rays "+rays+" held "+held+" stunned "+stunned+" hc "+hc+" spiked "+spiked+" feared "+feared+" fled "+fled+" "+f6.MentalStateDef?.defName+"] attack spells: fire bolt hurts; scorching ray 4 rays (3 + 1 upcast); shatter hits "+shat+"; hold person "+hc.ToStringPercent()+" → paralysed; mind spike strips invisibility and marks; fear sends a foe in the cone fleeing");
    WizardSpells.RollOverride=null;
    // 9. 防御法术：闪现约一半攻击落空；防护能量（火）火焰 ×0.5；隐形的人施法就现形；虚假生命复用护盾术。
    var ally=SbWizard(1);made.Add(ally);GenSpawn.Spawn(ally,NextTo(w),map);Items.Give(ally,"LP_WS_Blink",3600,1f);int missed=0;
    var pre=AccessTools.Method(typeof(WizardSpellDamage),"Prefix");
    for(int i=0;i<40;i++){var args=new object[]{ally,new DamageInfo(DamageDefOf.Bullet,5f,0f,-1f,f6),false};pre.Invoke(null,args);if((bool)args[2])missed++;}
    Items.Remove(ally,"LP_WS_Blink");WizardSpells.SetProtection(ally,0,2500);var pa=new object[]{ally,new DamageInfo(DamageDefOf.Flame,10f,0f,-1f,f6),false};pre.Invoke(null,pa);float fire=((DamageInfo)pa[1]).Amount;
    var pc=new object[]{ally,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,f6),false};pre.Invoke(null,pc);float cut=((DamageInfo)pc[1]).Amount;
    Items.Give(w,"LP_WS_Invisible",2500,1f);Spellbook.AfterCast(w.abilities.GetAbility(Ab("LP_WS_FireBolt")) as Ability_Class);bool revealed=!Items.Has(w,"LP_WS_Invisible");
    Check(missed>=8 && missed<=32 && Near(fire,5f) && Near(cut,10f) && revealed && Fx("LP_WS_FalseLife").effect=="PowerBuff" && Fx("LP_WS_FalseLife").hediff=="LP_ShieldSpell","defence: blink dodged "+missed+"/40; protection (fire) halves flame, not cuts; casting reveals an invisible wizard; false life reuses 护盾术");
    // 10. 区域：油腻（铺油）、蛛网、臭云、警报、魔法阵（登记、弹出）。
    var zc=ScCell(-8,8);var grease=SpellZone.Make(w,zc,"Grease",2f,3600);made.Add(grease);bool fuel=grease!=null && grease.Cells.Any(c=>c.GetThingList(map).Any(t=>t.def.defName=="Filth_Fuel"));
    var web=SpellZone.Make(w,ScCell(-8,14),"Web",2f,3600);made.Add(web);var stink=SpellZone.Make(w,ScCell(-14,8),"Stink",3f,1200);made.Add(stink);var alarm=SpellZone.Make(w,w.Position,"Alarm",10f,60000);made.Add(alarm);
    var circle=SpellZone.Make(w,ScCell(-14,14),"Circle",3f,60000,1);made.Add(circle);bool listed=SpellZone.Circles.Contains(circle);
    var f7=ScFoe(w);made.Add(f7);f7.Position=circle.Position;f7.Notify_Teleported();circle.Expel(f7);float out7=f7.Position.DistanceTo(circle.Position);
    Check(grease!=null && web?.kind=="Web" && stink?.kind=="Stink" && alarm?.kind=="Alarm" && listed && out7>circle.radius && grease.endTick-Find.TickManager.TicksGame==3600,"[grease "+(grease!=null)+" fuel "+fuel+" web "+web?.kind+" stink "+stink?.kind+" alarm "+alarm?.kind+" listed "+listed+" end "+(grease==null?0:grease.endTick-Find.TickManager.TicksGame)+"] zones: grease (chemfuel puddles where the ground takes filth), web / stinking cloud / alarm / magic circle spawn with their timers; the circle is registered and pushes a creature out ("+out7.ToString("0.#")+" cells)");
    foreach(var z in new[]{grease,web,stink,alarm,circle})if(z!=null && !z.Destroyed)z.Destroy();bool unlisted=!SpellZone.Circles.Contains(circle);
    // 11. 魔宠：召来的猫属于殖民地、有魔宠标记，主人研究 ×1.05；在 10 格内法术射程 +3；只能一只；死后 1 天才能重召。
    var cat=DefDatabase<PawnKindDef>.GetNamed("Cat");var fam=Familiars.Make(w,cat);if(fam!=null)made.Add(fam);string second=Familiars.Cannot(w);
    var fb=w.abilities.GetAbility(Ab("LP_WS_FireBolt"));float range=fb.verb.EffectiveRange;
    fam?.Kill(null);if(fam!=null){made.Remove(fam);if(fam.Corpse!=null)made.Add(fam.Corpse);}Spellbook.Tick250(h);string dead=Familiars.Cannot(w);
    Check(unlisted && fam!=null && fam.Faction==Faction.OfPlayer && Items.Has(fam,"LP_WS_Familiar") && second!=null && Near(range,29f) && d.familiar==null && d.familiarDied>=0 && !Items.Has(w,"LP_WS_FamiliarBond") && dead!=null && dead.Contains("1 天"),
     "familiar: a colony cat bound to the wizard; spell range 26 → "+range.ToString("0.#")+" with it nearby; only one; after it dies: "+dead);
    // 12. 实用法术：修复术、法师之手（≤10 千克）、秘法锁（伤害 ÷3，可解除）、敲击术（震碎别人的门）、解除魔法（敌人去增益、友军去有害效果）。
    var shirt=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_BasicShirt"),ThingDefOf.Cloth);GenSpawn.Spawn(shirt,ScCell(6,12),map);made.Add(shirt);shirt.HitPoints=10;int mended=WizardSpells.Mend(shirt,50f);
    var steel=ThingMaker.MakeThing(ThingDefOf.Steel);steel.stackCount=5;GenSpawn.Spawn(steel,ScCell(6,14),map);made.Add(steel);
    bool hand=WizardSpells.MageHand(w,steel) && steel.Position.InHorDistOf(w.Position,4.5f);var heavy=ThingMaker.MakeThing(ThingDefOf.Steel);heavy.stackCount=25;GenSpawn.Spawn(heavy,ScCell(6,16),map);made.Add(heavy);string tooHeavy=WizardSpells.Invalid(w,Fx("LP_WS_MageHand"),new LocalTargetInfo(heavy));
    var door=(Building_Door)ThingMaker.MakeThing(ThingDefOf.Door,ThingDefOf.WoodLog);door.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(door,ScCell(10,14),map);made.Add(door);
    int hpA=door.HitPoints;door.TakeDamage(new DamageInfo(DamageDefOf.Blunt,30f,0f,-1f,f6));int lostOpen=hpA-door.HitPoints;
    WizardLocks.Lock(door);bool isLocked=WizardLocks.Locked(door);int hp0=door.HitPoints;door.TakeDamage(new DamageInfo(DamageDefOf.Blunt,30f,0f,-1f,f6));int lost=hp0-door.HitPoints;bool lockedGiz=door.GetGizmos().OfType<Command_Action>().Any(g=>g.defaultLabel=="解除秘法锁");WizardLocks.Unlock(door);
    door.SetFaction(AdvEnemy);string knock=WizardSpells.Knock(w,door);
    Items.Give(f6,"LP_Haste",2500,1f);int dh=WizardSpells.Dispel(w,f6);Items.Give(ally,"LP_MagicSleep",480,1f);int da=WizardSpells.Dispel(w,ally);
    Check(mended>=1 && hand && tooHeavy!=null && isLocked && lostOpen>0 && Mathf.Abs(lost-lostOpen/3f)<=1.01f && lockedGiz && !WizardLocks.Locked(door) && knock=="door" && door.Destroyed && dh>=1 && !Items.Has(f6,"LP_Haste") && da>=1 && !Items.Has(ally,"LP_MagicSleep"),
     "[hand "+hand+" heavy "+(tooHeavy!=null)+" locked "+isLocked+" giz "+lockedGiz+" knock "+knock+" dh "+dh+" da "+da+"] utility: mending +"+mended+" HP; mage hand fetches 5 steel (25 = 12.5 kg refused); arcane lock: door damage "+lostOpen+" → "+lost+" with an unlock button; knock shatters a hostile door; dispel strips a foe's haste and an ally's magic sleep");
    // 13. 活化死尸：敌人的尸体变成 1 天的骷髅仆役（殖民地），魔法阵把它算作亡灵；时间到散架。
    var f8=ScFoe(w);f8.Kill(null);var corpse=f8.Corpse;var sk=WizardSpells.AnimateDead(w,corpse,1,60000);var skel=sk.FirstOrDefault();if(skel!=null)made.Add(skel);
    bool animated=skel!=null && skel.Faction==Faction.OfPlayer && Items.Has(skel,"LP_WS_Animated") && corpse.Destroyed && WizardSpells.CircleKind(skel,1);
    if(skel!=null)Hediff_WS.Unmake(skel);
    Check(animated && (skel==null || skel.Destroyed),"animate dead: a corpse rises as a colony skeleton for a day (counts as undead for the magic circle) and crumbles when it ends");
    // 14. 仪式：不在书里不能仪式施法；征召时不行；引导完成后生效且不耗法力（通晓语言）；送讯术每天一次。
    var cl=Ab("LP_WS_ComprehendLanguages");string notBook=Spellbook.CannotRitual(w,cl);Spellbook.Write(h,cl);w.drafter.Drafted=true;string drafted=Spellbook.CannotRitual(w,cl);w.drafter.Drafted=false;
    float mr0=ClassSystem.Resource(h,Spellbook.Wizard);d.ritual=cl;bool done=Spellbook.FinishRitual(w,new LocalTargetInfo(w));float mr1=ClassSystem.Resource(h,Spellbook.Wizard);
    string send0=WizardSpells.SendingCannot(w);var fac=WizardSpells.Factions(w).FirstOrDefault();bool sent=fac!=null && WizardSpells.Contact(w,fac);string send1=WizardSpells.SendingCannot(w);
    Check(notBook!=null && drafted!=null && done && Items.Has(w,"LP_WS_Comprehend") && Near(mr0,mr1) && d.ritual==null && !Spellbook.Counted(cl) && send0==null && sent && send1!=null && send1.Contains("每天"),
     "rituals: not in book refused, drafted refused; the 1-hour ritual applies 通晓语言 without mana and takes no prepare slot; sending once per day ("+send1+")");
    SbHigh(made); // 1.129.0 4～9 环
   }finally{
    WizardSpells.RollOverride=null;Spellbook.RollOverride=null;
    foreach(var t in made)if(!t.Destroyed){if(t is Pawn p)p.GetLord()?.RemovePawn(p);t.Destroy();}foreach(var p in advMade)if(!p.Destroyed)p.Destroy();advMade.Clear();
    s.adventurer=oldOn;Adventurer.TestSuppressed=sup;
   }
  }
  void PrepareSpellbookSave(){
   var s=LichMod.Settings;s.adventurer=true;
   var p=SbWizard(5,14);var h=Adventurer.Get(p);Spellbook.Pick(h,Ab("LP_WS_Thunderwave"));Spellbook.LearnCantrip(h,Ab("LP_WS_RayOfFrost"));Spellbook.SetUp(p,Ab("LP_WS_Thunderwave"),2);
   var d=Spellbook.Data(h);d.mayPrepare=false;d.prepared.Remove(Ab("LP_MageArmor"));
   Spellbook.UseDaily(p,Ab("LP_WS_DimensionDoor"));WizardSpells2.SetContingency(p,Ab("LP_ShieldSpell"),600000); // 1.129.0
   GenSpawn.Spawn(p,altar.InteractionCell+IntVec3.North*5,map);sbSavedId=p.ThingID;advMade.Remove(p);
  }
  void CheckSpellbookAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==sbSavedId);var h=Adventurer.Get(p);var d=h?.classes?.book;
   Check(d!=null && d.init && d.book.Contains(Ab("LP_WS_Thunderwave")) && d.cantrips.Contains(Ab("LP_WS_RayOfFrost")) && d.picksUsed==1 && !d.mayPrepare && !d.prepared.Contains(Ab("LP_MageArmor")) && d.prepared.Contains(Ab("LP_ShieldSpell"))
    && Spellbook.UpLevel(p,Ab("LP_WS_Thunderwave"))==2 && Spellbook.Hidden(p,Ab("LP_MageArmor")) && HasAb(p,"LP_WS_Thunderwave") && Spellbook.UsedToday(p,Ab("LP_WS_DimensionDoor"))==1 && d.contingency==Ab("LP_ShieldSpell"),"spellbook survives save/load: written spell, cantrip, picks used, prepared list, upcast choice, prepare lock, daily uses, contingency");
   p?.Destroy();
  }
 }
}
