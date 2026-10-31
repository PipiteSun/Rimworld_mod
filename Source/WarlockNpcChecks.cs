using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 邪术师（Test.ps1 -Only Classes，6 条）+ NPC 职业（-Only NpcClasses，6 条 + 读档 1 条）。
 public partial class LichChecks {
  static string npcSavedId;
  static LP_ClassDef WL=>Warlock.Class;
  Pawn WlPawn(int level,int wlLevel,int social=10){var p=ClsPawn(level,0,social);var h=Adventurer.Get(p);for(int i=0;i<wlLevel;i++)ClassSystem.AddLevel(h,WL);return p;}
  void CheckWarlock(){
   var made=new List<Pawn>();
   try{
    // 1. 定义：7 个职业能力（1.87.0：1/3/5/7/11/13/15，秘法奥秘 11 / 13）、3 个宗主各 5 个（1/3/7/11/15）、2 个传奇，都有图标；契约强度；契约法力 2 + 等级/4。
    var subs=ClassSubclass.Options(WL).ToList();var abs=WL.abilities.Select(e=>e.ability).Concat(subs.SelectMany(s=>s.abilities.Select(e=>e.ability))).Concat(WL.legendary).ToList();
    Check(WL!=null && WL.resource==ClassResource.Pact && WL.caster && WL.subclassLevel==1 && WL.abilities.Select(e=>e.level).SequenceEqual(new[]{1,3,5,7,11,13,15}) && subs.Count==3
     && subs.All(s=>s.Emblem!=null && s.Emblem!=WL.Background && s.abilities.Select(e=>e.level).SequenceEqual(new[]{1,3,7,11,15}) && s.choices.Count==2) && abs.Count==24 && abs.Distinct().Count()==24
     && abs.All(a=>a.abilityClass==typeof(Ability_Class) && a.uiIcon!=null && a.uiIcon!=BaseContent.BadTex) && WL.Background!=null && DefDatabase<StatDef>.GetNamed("LP_PactPower").parts.Any(x=>x is StatPart_ClassPower)
     && Warlock.MaxPact(1)==2 && Warlock.MaxPact(4)==3 && Warlock.MaxPact(20)==7 && ClassSystem.ResourceLabel(ClassResource.Pact)=="契约法力",
     "warlock: 7 class abilities (1/3/5/7/11/13/15), 3 patrons × 5 (1/3/7/11/15, emblems, 2 invocation choices), 2 legendaries, icons, 契约强度; 契约法力 max 2/3/7 at 1/4/20");
    // 2. 门槛与宗主：社交 5 不行、6 可以 → 魔能爆，1 级就要选宗主；选邪魔立刻得到黑暗祝福；不能再选别的宗主。
    var a=ClsPawn(1,0,5);var ha=Adventurer.Get(a);string w5=ClassSystem.CanAdd(ha,WL);a.skills.GetSkill(SkillDefOf.Social).Level=6;bool add=ClassSystem.AddLevel(ha,WL);
    bool need=ClassSubclass.NeedsPick(ha,WL);bool picked=ClassSubclass.Pick(ha,Sub(Warlock.Fiend));
    Check(w5!=null && add && HasAb(a,"LP_WL_EldritchBlast") && need && picked && HasAb(a,"LP_WL_DarkBlessing") && !HasAb(a,"LP_WL_HellishRebuke") && ClassSubclass.CanPick(ha,Sub(Warlock.Archfey))!=null
     && Near(ClassSystem.Resource(ha,WL),2f),"warlock gate: social 5 refused, 6 → 魔能爆 + patron pick at level 1; 邪魔 gives 黑暗祝福 at once, no second patron; 2 契约法力");
    // 3. 短休：用光后冥想 9 次 250 刻还没满，第 10 次（满 1 小时）回满；中断会重来；不在短休时不恢复；睡觉也算。
    GenSpawn.Spawn(a,ScCell(0,-12),map);made.Add(a);ha.classes.res[WL]=0f;
    a.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Meditate,a.Position),JobCondition.InterruptForced);bool med=Warlock.Resting(a);
    for(int i=0;i<9;i++)Warlock.Tick250(ha);float at9=ClassSystem.Resource(ha,WL);Warlock.Tick250(ha);float at10=ClassSystem.Resource(ha,WL);
    a.jobs.EndCurrentJob(JobCondition.InterruptForced);ha.classes.res[WL]=0f;for(int i=0;i<5;i++)Warlock.Rest(ha,true);Warlock.Rest(ha,false);for(int i=0;i<5;i++)Warlock.Rest(ha,true);float broken=ClassSystem.Resource(ha,WL);
    for(int i=0;i<12;i++)ClassSystem.Regen(ha);float idle=ClassSystem.Resource(ha,WL);
    Check(med && Near(at9,0f) && Near(at10,2f) && Near(broken,0f) && Near(idle,0f),"契约法力: 9 × 250 ticks of meditation → 0, the 10th (1 hour) refills to 2; an interrupted rest starts over; no regen outside a short rest");
    // 4. 魔能爆：1/5/10/15 级 1/2/3/4 道（1.87.0 前 11 / 17）；邪术师 5 级对敌人打出 2 道；射程 24，魔能长矛 36。
    var b=WlPawn(5,5);var hb=Adventurer.Get(b);ClassSubclass.Pick(hb,Sub(Warlock.GreatOldOne));GenSpawn.Spawn(b,ScCell(6,-12),map);made.Add(b);
    var foe=ScFoe(b);made.Add(foe);float hp0=foe.health.summaryHealth.SummaryHealthPercent;int beams=Warlock.Blast(b,foe,8f);
    var far=new LocalTargetInfo(foe);var fx=Fx("LP_WL_EldritchBlast");string farWhy=null;
    if(foe.Spawned){var c30=CellFinder.RandomClosewalkCellNear(b.Position+IntVec3.East*30,map,3,c=>c.GetFirstPawn(map)==null);if(c30.IsValid && b.Position.DistanceTo(c30)>24.5f && b.Position.DistanceTo(c30)<36f){foe.Position=c30;foe.Notify_Teleported();farWhy=ClassEffects.Invalid(b,fx,far);}}
    bool spear=ClassSubclass.Choose(hb,"invoc1",Warlock.Spear);string spearWhy=farWhy==null?null:ClassEffects.Invalid(b,fx,far);
    Check(Warlock.Beams(1)==1 && Warlock.Beams(5)==2 && Warlock.Beams(9)==2 && Warlock.Beams(10)==3 && Warlock.Beams(14)==3 && Warlock.Beams(15)==4 && beams==2 && (foe.Dead || foe.health.summaryHealth.SummaryHealthPercent<hp0)
     && spear && Near(Warlock.Range(b),36f) && (farWhy==null || farWhy.Contains("24") && spearWhy==null),"魔能爆: 1/2/3/4 beams at warlock 1/5/10/15; warlock 5 fires 2 beams that hurt; range 24, 魔能长矛 36 ("+(farWhy??"far cell not found")+")");
    // 5. 魔能祈唤：第二次不能和第一次相同；苦痛魔能爆每道 + 魅力调整值 × 2（至少 +2）。
    string same=ClassSubclass.CanChoose(hb,"invoc2",Warlock.Spear);hb.level=9;for(int i=0;i<4;i++)ClassSystem.AddLevel(hb,WL);bool ago=ClassSubclass.Choose(hb,"invoc2",Warlock.Agonizing);
    hb.scores=new[]{10,10,10,10,10,16};hb.Dirty();
    Check(same!=null && ago && Warlock.Invocation(b,Warlock.Agonizing) && Warlock.AgonizingBonus(b)>=2f && Near(Warlock.AgonizingBonus(b),Mathf.Max(2f,2f*Adventurer.Mod(Adventurer.Score(b,Adventurer.Cha)))) && Warlock.Beams(b)==2,"invocations: the second cannot repeat 魔能长矛 ("+same+"); 苦痛魔能爆 adds max(2, 2 × CHA mod) = "+Warlock.AgonizingBonus(b)+" per beam");
    // 6. 咒术：被咒的敌人受到施咒者的伤害 ×1.25（别人打不加），心智抗性豁免 −15%、属性豁免 −3（每点 5%）。
    var foe2=foe.Dead || !foe.Spawned?ScFoe(b):foe;if(foe2!=foe)made.Add(foe2);
    float save0=Legendary.SaveChance(foe2,Adventurer.Wis,15);ClassEffects.Apply(b,Ab("LP_WL_Hex"),Fx("LP_WL_Hex"),new LocalTargetInfo(foe2));
    float mine=Warlock.DamageFactor(foe2,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,b)),other=Warlock.DamageFactor(foe2,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,a));float save1=Legendary.SaveChance(foe2,Adventurer.Wis,15);
    Check(Warlock.Hex(foe2)?.caster==b && Near(mine,1.25f) && Near(other,1f) && Near(save0-save1,save0<=0f?0f:Mathf.Min(save0,.15f)) && Near(Warlock.SavePenalty(foe2),.15f),"咒术: ×1.25 damage from the hexer only, saving chance −15% ("+save0.ToStringPercent()+" → "+save1.ToStringPercent()+")");
   }finally{foreach(var p in made)if(!p.Destroyed)p.Destroy();}
  }

  // ---------- NPC 职业 ----------
  Faction NpcFaction(Func<Faction,bool> f)=>Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x=>!x.IsPlayer && x.def.humanlikeFaction && f(x));
  Pawn NpcPawn(Faction f,int level,int melee=8,int social=0,int intel=0){
   var p=ClsPawn(level,melee,social);p.SetFaction(f);var h=Adventurer.Get(p);h.classes=null;
   if(intel>0)p.skills.GetSkill(SkillDefOf.Intellectual).Level=intel;return p;
  }
  void CheckNpcClasses(){
   var s=LichMod.Settings;bool oN=s.npcClasses,oC=s.npcClassCasting,oA=s.adventurer,oSup=NpcClasses.TestSuppressed,oAdv=Adventurer.TestSuppressed;
   s.npcClasses=s.npcClassCasting=s.adventurer=true;NpcClasses.TestSuppressed=false;Adventurer.TestSuppressed=false;var made=new List<Thing>();
   try{
    var enemy=AdvEnemy;var tribe=NpcFaction(x=>x.def.techLevel<=TechLevel.Neolithic);
    // 1. 设置默认开；关掉时 NPC 不拿职业（打开后下一次 250 刻补上）；玩家的人永远不自动分。
    var off=NpcPawn(enemy,6);s.npcClasses=false;NpcClasses.Tick(Adventurer.Get(off));bool noneOff=Adventurer.Get(off).classes==null || !Adventurer.Get(off).classes.Any;
    s.npcClasses=true;NpcClasses.Tick(Adventurer.Get(off));var mine=ClsPawn(6,8);NpcClasses.Tick(Adventurer.Get(mine));
    Check(new Settings().npcClasses && new Settings().npcClassCasting && noneOff && ClassSystem.Total(Adventurer.Get(off))==6 && Adventurer.Get(off).classes.npc && !ClassSystem.Any(mine),
     "settings default on; 敌人拥有职业 off → no class, on → the next tick assigns 6 levels; colonists are never auto-assigned");
    // 2. 派系风格：部落 → 野蛮人 / 德鲁伊 / 游侠；塞伊兵种 → 法师 / 邪术师 / 牧师；没有风格的派系按最高技能（智识 12 → 法师）。
    var fl=tribe==null?null:NpcClasses.Flavor(tribe);var thayKind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_ThayEliteMage");var thay=NpcClasses.Flavor(enemy,thayKind);
    var tp=tribe==null?null:NpcPawn(tribe,4,10);if(tp!=null)NpcClasses.Assign(Adventurer.Get(tp),1f);var tc=tp==null?null:ClassSystem.MainClass(tp);
    var plain=NpcFaction(x=>NpcClasses.Flavor(x)==null);var sk=plain==null?null:NpcPawn(plain,3,0,0,12);if(sk!=null)NpcClasses.Assign(Adventurer.Get(sk),1f);
    Check((fl==null || fl.Count==3 && fl.Contains(Barb) && tc!=null && fl.Contains(tc)) && (thayKind==null || thay!=null && thay.Contains(WL) && thay.Contains(Cls("Wizard")) && thay.Contains(Cls("Cleric"))) && (sk==null || ClassSystem.MainClass(sk)?.SkillLevel(sk)==12),
     "faction flavor: tribe → "+tc?.label+" (barbarian/druid/ranger), Thay kinds → wizard/warlock/cleric, plain faction by best skill (intellectual 12) → "+(sk==null?"n/a":ClassSystem.MainClass(sk)?.label));
    // 3. 等级分配：两个职业时第二个 1/3（12 → 8 + 4）；子职业到等级随机选好；资源满；以后升级新点数加到主职业。
    var two=NpcPawn(enemy,12,10);var h2=Adventurer.Get(two);NpcClasses.Assign(h2,0f);var cl=ClassSystem.Taken(h2).ToList();
    bool subs=cl.All(c=>!ClassSubclass.Options(c).Any() || ClassSystem.Level(h2,c)<ClassSubclass.PickLevelFor(c) || ClassSubclass.Picked(h2,c)!=null) && !ClassSubclass.PendingChoices(h2).Any();
    bool full=cl.All(c=>c.resource==ClassResource.None || Near(ClassSystem.Resource(h2,c),ClassSystem.MaxResource(h2,c)));
    var main=ClassSystem.MainClass(two);int before=ClassSystem.Level(h2,main);h2.level=14;NpcClasses.Tick(h2);
    Check(NpcClasses.Split(12,true)==(8,4) && NpcClasses.Split(1,true)==(1,0) && NpcClasses.Split(7,false)==(7,0) && cl.Count==2 && ClassSystem.Total(h2)==14 && subs && full && ClassSystem.Level(h2,main)==before+2,
     "level split: 12 → 8 + 4 for two classes; subclasses / branches picked, resources full; levelling to 14 adds 2 to the main class ("+NpcClasses.Summary(h2)+")");
    // 4. 传奇：冒险者 26 级的 NPC 学会 1 个传奇职业能力。
    var leg=NpcPawn(enemy,20,12);var hl=Adventurer.Get(leg);hl.legendary=true;hl.level=26;NpcClasses.Assign(hl,1f);
    Check(hl.classes.legendary.Count==1 && ClassSystem.Total(hl)==20 && HasAb(leg,hl.classes.legendary[0].defName),"a level-26 NPC has 20 class levels and 1 legendary class ability ("+hl.classes.legendary.FirstOrDefault()?.label+")");
    // 5. 战斗施放：敌对邪术师 NPC 看到身边的殖民者 → 开始施放一个职业能力（魔能爆等）；不在战斗中不放；设置关掉不放。
    var caster=NpcPawn(enemy,5,0,10);var hc=Adventurer.Get(caster);ClassSystem.SetLevel(hc,WL,5);NpcClasses.Finish(hc);hc.classes.npc=true;GenSpawn.Spawn(caster,ScCell(-10,10),map);made.Add(caster);
    var target=ClsPawn(1);GenSpawn.Spawn(target,NextTo(caster),map);made.Add(target);
    caster.mindState.enemyTarget=null;caster.mindState.lastHarmTick=-99999;caster.mindState.lastAttackTargetTick=-99999;var calm=NpcClasses.TryCast(caster);
    caster.mindState.enemyTarget=target;s.npcClassCasting=false;NpcClasses.Tick(hc);bool offCast=caster.CurJob?.ability!=null;s.npcClassCasting=true;
    var used=NpcClasses.TryCast(caster);
    Check(calm==null && !offCast && used!=null && ClassSystem.ClassOf(used.def)==WL && caster.CurJob?.ability==used,"an enemy warlock in combat starts casting a class ability ("+used?.def.label+"); none out of combat or with 敌人施放职业能力 off");
    // 6. 治疗：NPC 牧师先救倒地的友军。
    var cleric=NpcPawn(enemy,5,0,10);var hcl=Adventurer.Get(cleric);ClassSystem.SetLevel(hcl,Cls("Cleric"),5);NpcClasses.Finish(hcl);hcl.classes.npc=true;GenSpawn.Spawn(cleric,ScCell(10,10),map);made.Add(cleric);
    var ally=NpcPawn(enemy,1);GenSpawn.Spawn(ally,NextTo(cleric),map);made.Add(ally);
    HealthUtility.DamageUntilDowned(ally,false);
    float inj=ClassMelee.InjurySum(ally);cleric.mindState.lastHarmTick=Find.TickManager.TicksGame;var heal=ally.Dead?null:NpcClasses.TryCast(cleric,true);
    Check(!ally.Dead && heal!=null && ClassMelee.InjurySum(ally)<inj,"an NPC cleric in combat heals its downed ally first ("+heal?.def.label+", "+inj.ToString("0")+" → "+ClassMelee.InjurySum(ally).ToString("0")+")");
   }finally{
    s.npcClasses=oN;s.npcClassCasting=oC;s.adventurer=oA;NpcClasses.TestSuppressed=oSup;Adventurer.TestSuppressed=oAdv;
    foreach(var t in made)if(!t.Destroyed)t.Destroy();foreach(var p in advMade)if(!p.Destroyed)p.Destroy();advMade.Clear();
   }
  }
  void PrepareNpcClassesSave(){
   // 敌对 NPC：邪术师 9（旧日支配者、魔能长矛 + 魔鬼视觉，契约法力 1.5）+ 战士 3，分过职业。
   var p=ClsPawn(12,8,10);advMade.Remove(p);p.SetFaction(AdvEnemy);var h=Adventurer.Get(p);h.classes=new ClassData{npc=true};
   ClassSystem.SetLevel(h,WL,9);ClassSystem.SetLevel(h,ClassSystem.Fighter,3);ClassSubclass.Pick(h,Sub(Warlock.GreatOldOne));ClassSubclass.Choose(h,"invoc1",Warlock.Spear);ClassSubclass.Choose(h,"invoc2",Warlock.DevilSight);
   h.classes.res[WL]=1.5f;GenSpawn.Spawn(p,ScCell(-14,-14),map);npcSavedId=p.ThingID;
  }
  void CheckNpcClassesAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==npcSavedId);var h=Adventurer.Get(p);
   Check(h?.classes!=null && h.classes.npc && ClassSystem.Level(h,WL)==9 && ClassSystem.Level(h,ClassSystem.Fighter)==3 && ClassSubclass.Has(h,Warlock.GreatOldOne) && ClassSubclass.Choice(h,"invoc1")==Warlock.Spear && ClassSubclass.Choice(h,"invoc2")==Warlock.DevilSight
    && Near(ClassSystem.Resource(h,WL),1.5f) && HasAb(p,"LP_WL_Hex") && HasAb(p,"LP_WL_EntropicWard"),"NPC classes, warlock patron, both invocations and 契约法力 1.5 survive save/load"+(h?.classes==null?" (no data)":" ["+NpcClasses.Summary(h)+"]"));
   p?.Destroy();
  }
 }
}
