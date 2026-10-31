using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
namespace LichPhylactery {
 // 1.72.0 职业体系（战士 / 武僧 / 圣武士，主程序集）：Test.ps1 -Only Classes（不装 VPE）。旧存档迁移在 VPE 桥的 -VPE -Only Classes。
 public partial class LichChecks {
  static string clsSavedId;
  Pawn ClsPawn(int level,int melee=0,int social=0,int shooting=0){
   var p=AdvPawn();var h=Adventurer.Get(p)??Adventurer.TryAdd(p,1,true);
   foreach(var s in DefDatabase<SkillDef>.AllDefsListForReading){var r=p.skills.GetSkill(s);if(!r.TotallyDisabled)r.Level=0;}
   p.skills.GetSkill(SkillDefOf.Melee).Level=melee;p.skills.GetSkill(SkillDefOf.Social).Level=social;p.skills.GetSkill(SkillDefOf.Shooting).Level=shooting;
   h.level=level;h.xp=Adventurer.XpTable[Mathf.Clamp(level,1,20)-1];h.scores=new[]{10,10,10,10,10,10};h.classes=new ClassData();h.fighterFeats=h.fighterFeatGranted=0;h.Dirty();
   p.equipment?.DestroyAllEquipment();
   return p;
  }
  bool HasAb(Pawn p,string n)=>p.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed(n))!=null;

  void CheckClasses(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=false;var s=LichMod.Settings;bool oldPsy=s.classPsychic,oldEnt=s.classEntropy,oldOn=s.adventurer;s.adventurer=true;
   try{
    // 1. 定义：3 个职业、34 个原版能力（Ability_Class + 图标），等级都在 1/3/5/7/10/13/15（1.87.0）；三个强度属性在主程序集；设置默认值。
    var all=new[]{ClassSystem.Fighter,ClassSystem.Monk,ClassSystem.Paladin}.Where(c=>c!=null).ToList();var abilities=all.SelectMany(c=>c.abilities).ToList(); // 1.73.0：只看物理三职业（施法职业在 CheckClassesCasters）
    Check(all.Count==3 && ClassSystem.Fighter!=null && ClassSystem.Monk!=null && ClassSystem.Paladin!=null && abilities.Count==34 && abilities.All(e=>e.ability.abilityClass==typeof(Ability_Class) && e.ability.uiIcon!=null && e.ability.uiIcon!=BaseContent.BadTex && ClassSystem.AbilityLevels.Contains(e.level) && e.ability.comps.OfType<CompProperties_ClassEffect>().Any())
     && new[]{"LP_MartialPower","LP_MarksmanPower","LP_DivinePower"}.All(n=>DefDatabase<StatDef>.GetNamed(n).parts.Any(x=>x is StatPart_ClassPower)) && all.All(c=>c.Background!=null),"3 classes with 34 vanilla abilities (icons, levels 1/3/5/7/10/13/15) and the 3 power stats live in the main assembly");
    Check(new LichPhylactery.Settings().classPsychic && !new LichPhylactery.Settings().classEntropy && (ModsConfig.IsActive(VPEId) || DefDatabase<IncidentDef>.GetNamedSilentFail("LP_WanderingMentor")==null),"settings default: psychic factor on, entropy off; without VPE the class system runs alone");
    // 2. 职业点：每个冒险者等级 1 点，最多 20；传奇等级不给。
    var a=ClsPawn(1,6);var ha=Adventurer.Get(a);int p1=ClassSystem.Points(ha);ha.level=4;int p4=ClassSystem.Points(ha);ha.level=25;int p25=ClassSystem.Budget(ha);ha.level=4;
    Check(p1==1 && p4==4 && p25==20,"class points: level 1 → 1, level 4 → 4, legendary 25 still capped at 20");
    // 3. 第一个职业：格斗 5 不行、6 可以；学会后 1 级能力自动获得，资源满 3。
    a.skills.GetSkill(SkillDefOf.Melee).Level=5;string why5=ClassSystem.CanAdd(ha,ClassSystem.Fighter);a.skills.GetSkill(SkillDefOf.Melee).Level=6;
    bool add=ClassSystem.AddLevel(ha,ClassSystem.Fighter);
    Check(why5!=null && add && ClassSystem.Level(ha,ClassSystem.Fighter)==1 && HasAb(a,"LP_SecondWind") && HasAb(a,"LP_Charge") && HasAb(a,"LP_SteadyAim") && !HasAb(a,"LP_BattleCry") && Near(ClassSystem.Resource(ha,ClassSystem.Fighter),3f) && ClassSystem.Points(ha)==3,"first class: melee 5 refused ("+why5+"), melee 6 → fighter 1 with second wind / charge / steady aim and 3 fury");
    // 4. 兼修：冒险者 2 级不行；3 级要社交 8；巫妖不能当圣武士；一次 1 点。
    var b=ClsPawn(2,0,8,10);var hb=Adventurer.Get(b);ClassSystem.AddLevel(hb,ClassSystem.Fighter);string lv2=ClassSystem.CanAdd(hb,ClassSystem.Paladin);
    hb.level=3;b.skills.GetSkill(SkillDefOf.Social).Level=7;string soc7=ClassSystem.CanAdd(hb,ClassSystem.Paladin);b.skills.GetSkill(SkillDefOf.Social).Level=8;bool multi=ClassSystem.AddLevel(hb,ClassSystem.Paladin);
    var lich=ClsPawn(5,10,10);lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);
    Check(lv2!=null && soc7!=null && multi && ClassSystem.Level(hb,ClassSystem.Paladin)==1 && ClassSystem.Total(hb)==2 && ClassSystem.CanAdd(Adventurer.Get(lich),ClassSystem.Paladin)=="亡灵无法立下圣誓","multiclass (fighter by shooting 10): paladin needs adventurer 3 and melee or social 8; a lich cannot be a paladin");
    // 5. 等级到了给能力；终极另要技能 15（1.87.0 前 18）。
    var c=ClsPawn(20,10);var hc=Adventurer.Get(c);for(int i=0;i<20;i++)ClassSystem.AddLevel(hc,ClassSystem.Fighter);
    bool mid=HasAb(c,"LP_WeaponMastery") && HasAb(c,"LP_GuardStance") && !HasAb(c,"LP_SurgeOfBattle");c.skills.GetSkill(SkillDefOf.Melee).Level=15;ClassSystem.Sync(hc);
    Check(ClassSystem.Level(hc,ClassSystem.Fighter)==20 && mid && HasAb(c,"LP_SurgeOfBattle") && !HasAb(c,"LP_DeadEye") && ClassSystem.MaxResource(hc,ClassSystem.Fighter)==12 && Martial.Tier(c)==3,"fighter 20: level-13 abilities at melee 10, surge of battle only at melee 15 (dead eye still needs shooting 15); 12 fury max, extra attack tier 3");
    // 5c. 1.87.0：终极在战士 15 级 + 格斗 15（14 级还没有）；额外攻击第 2 档 10 级、第 3 档 15 级。
    {var c15=ClsPawn(15,15);var h15=Adventurer.Get(c15);int t9=0,t10=0,t14=0;bool cap14=true;
     for(int i=1;i<=15;i++){ClassSystem.AddLevel(h15,ClassSystem.Fighter);if(i==9)t9=Martial.Tier(c15);if(i==10)t10=Martial.Tier(c15);if(i==14){t14=Martial.Tier(c15);cap14=HasAb(c15,"LP_SurgeOfBattle");}}
     Check(!cap14 && ClassSystem.Level(h15,ClassSystem.Fighter)==15 && HasAb(c15,"LP_SurgeOfBattle") && HasAb(c15,"LP_GuardStance") && !HasAb(c15,"LP_DeadEye"),"capstone at fighter 15 + melee 15 (surge of battle; none at fighter 14, dead eye still needs shooting 15)");
     Check(t9==1 && t10==2 && t14==2 && Martial.Tier(c15)==3,"extra attack: fighter 9 → tier 1, 10 → tier 2, 14 → tier 2, 15 → tier 3 (was 11 / 20)");}
    // 5d. 1.87.0 老存档：等级表版本 0、战士 10 级只有旧表的能力 → 一次 Tick250 补上新表 10 级的能力，什么都不收回；新建的数据直接是新版本。
    {var os=ClsPawn(10,10);var hos=Adventurer.Get(os);hos.classes.levels[ClassSystem.Fighter]=10;hos.classes.table=0;hos.fighterFeatGranted=ClassSystem.FighterFeatsDue(10);
     foreach(var e in ClassSystem.Fighter.abilities)if(e.level<=7 && os.abilities.GetAbility(e.ability)==null)os.abilities.GainAbility(e.ability);
     int n0=os.abilities.abilities.Count;bool pre=!HasAb(os,"LP_Whirlwind");ClassSystem.Tick250(hos);int n1=os.abilities.abilities.Count;ClassSystem.Tick250(hos);
     Check(pre && hos.classes.table==ClassSystem.TableVersion && HasAb(os,"LP_Whirlwind") && HasAb(os,"LP_DisarmingStrike") && HasAb(os,"LP_RapidFire") && !HasAb(os,"LP_GuardStance") && n1==n0+3 && os.abilities.abilities.Count==n1 && new ClassData().table==ClassSystem.TableVersion,
      "old save (table 0, fighter 10 with the old level-9 set): one Tick250 grants the 3 level-10 abilities, nothing removed or repeated ("+n0+" → "+n1+")");}
    // 5b. 2026-10-31 战士奖励专长：战士 1、2、4…20 级各 1 个（20 级共 11 个），免费选、不占属性提升；老存档按水位线补发；NPC 自动选。
    {int f20=hc.fighterFeats;hc.pendingASI=0;var pick=Feats.All.FirstOrDefault(x=>hc.CanTakeFeat(x)==null);bool took=pick!=null && hc.TakeFeat(pick) && hc.fighterFeats==f20-1 && hc.pendingASI==0;
     var o=ClsPawn(7,10);var ho=Adventurer.Get(o);ho.classes.levels[ClassSystem.Fighter]=3;ho.fighterFeats=0;ho.fighterFeatGranted=0;int back=ClassSystem.SyncFighterFeats(ho,true);int again=ClassSystem.SyncFighterFeats(ho);
     ho.classes.levels[ClassSystem.Fighter]=4;int at4=ClassSystem.SyncFighterFeats(ho);bool pend=Letter_AdventurerLevel.Pending(ho).Contains("战士奖励专长");
     var n=ClsPawn(7,10);n.SetFaction(null);var hn=Adventurer.Get(n);int nf0=hn.feats.Count;ClassSystem.SetLevel(hn,ClassSystem.Fighter,6);
     Check(f20==11 && hc.fighterFeatGranted==11 && took && back==2 && again==0 && at4==1 && ho.fighterFeats==3 && pend && ClassSystem.FighterFeatsDue(7)==4 && hn.fighterFeats==0 && hn.feats.Count==nf0+4,"fighter bonus feats: fighter 20 → 11, a pick uses them before ability score improvements ("+pick?.defName+"); an old fighter 3 save gets 2 once, fighter 4 one more; NPC fighter 6 auto-picks 4 ("+(hn.feats.Count-nf0)+")");}
    // 6. 施放：扣资源、生效；资源不够时按钮灰掉。
    GenSpawn.Spawn(a,altar.InteractionCell+IntVec3.East*3,map);
    var sw=a.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_SecondWind"));
    bool cast=sw.Activate(new LocalTargetInfo(a),new LocalTargetInfo(a));
    float afterCast=ClassSystem.Resource(ha,ClassSystem.Fighter);ha.classes.res[ClassSystem.Fighter]=.5f;sw.ResetCooldown();bool grey=sw.GizmoDisabled(out string greyWhy);
    Check(cast && Items.Has(a,"LP_SecondWind") && Near(afterCast,2f) && grey && greyWhy.Contains("斗志不足"),"second wind spends 1 fury and heals; with 0.5 fury the button is disabled ("+greyWhy+")");
    // 7. 资源恢复：武僧冥想 +0.2 气；圣武士非信徒冥想 +0.2、信徒 ×1.25。
    var m=ClsPawn(3,8,0);var hm=Adventurer.Get(m);ClassSystem.AddLevel(hm,ClassSystem.Monk);hm.classes.res[ClassSystem.Monk]=0f;GenSpawn.Spawn(m,altar.InteractionCell+IntVec3.West*3,map);
    m.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Meditate,m.Position),JobCondition.InterruptForced);bool med=ClassSystem.Meditating(m);ClassSystem.Regen(hm);float ki=ClassSystem.Resource(hm,ClassSystem.Monk);
    m.jobs.EndCurrentJob(JobCondition.InterruptForced);ClassSystem.Regen(hm);
    Check(med && Near(ki,ClassSystem.MeditateRegen) && Near(ClassSystem.Resource(hm,ClassSystem.Monk),ki) && Near(ClassSystem.MaxResource(1),3f) && Near(ClassSystem.MaxResource(20),12f),"ki: +0.2 per 250 ticks while meditating, nothing while awake and idle; max 3 at level 1, 12 at level 20");
    // 8. 强度：物理职业等级合计 11 → +0.2；心灵系数设置开 / 关；战士能力不乘。
    var d=ClsPawn(12,10);var hd=Adventurer.Get(d);for(int i=0;i<11;i++)ClassSystem.AddLevel(hd,ClassSystem.Monk);
    float mp=d.GetStatValue(DefDatabase<StatDef>.GetNamed("LP_MartialPower"));
    var deflect=DefDatabase<AbilityDef>.GetNamed("LP_DeflectMissiles").comps.OfType<CompProperties_ClassEffect>().First();var swp=DefDatabase<AbilityDef>.GetNamed("LP_SecondWind").comps.OfType<CompProperties_ClassEffect>().First();
    float sens=d.GetStatValue(StatDefOf.PsychicSensitivity);s.classPsychic=true;float on=ClassEffects.Power(d,deflect);s.classPsychic=false;float off=ClassEffects.Power(d,deflect);s.classPsychic=true;
    Check(Near(mp,ClassSystem.Power(10)+Adventurer.ClassPowerBonus(d,ClassSystem.SkillAbility(SkillDefOf.Melee,d))+.2f) && Near(on,10f*mp*Mathf.Max(.2f,sens)) && Near(off,10f*mp) && Near(ClassSystem.PsychicFactor(d,swp),1f),"martial power +0.2 at monk 11; deflect power ×psychic sensitivity only while the setting is on; fighter never");
    // 9. 升级信件和窗口：待选里写职业点；选职业窗口能建。
    Check(Letter_AdventurerLevel.Pending(ha).Contains("职业点") && new Dialog_ChooseClass(a)!=null && GameComponent_Adventurer.LevelGains(a,ha,3,4).Contains("职业点"),"level-up letter lists class points; the class dialog opens");
    CheckClassesCasters(); // 1.73.0 法师 / 牧师 / 德鲁伊 / 术士 / 器械师
    CheckClassesLegendary(); // 1.74.0 传奇职业能力
    CheckClasses3(); // 野蛮人 / 游侠 / 游荡者
    CheckSubclasses(); // 子职业 + 通用能力
    CheckWarlock(); // 邪术师
    CheckClassUI(); // 1.89.0 部位生命 / 职业强度前后对比、资源上限公式
   }finally{
    s.classPsychic=oldPsy;s.classEntropy=oldEnt;s.adventurer=oldOn;
    foreach(var p in advMade)if(!p.Destroyed)p.Destroy();advMade.Clear();Adventurer.TestSuppressed=sup;
   }
  }
  // 1.73.0：施法职业（≤ 10 条）。
  static LP_ClassDef Cls(string n)=>DefDatabase<LP_ClassDef>.GetNamed("LP_Class_"+n);
  void CheckClassesCasters(){
   var s=LichMod.Settings;bool oldArt=s.dndWeaponsNeedArtificer;
   try{
    // 1. 定义：8 个职业、79 个原版能力；五个强度属性在主程序集（天赋强度带血脉）；冒险者手册在主程序集。
    var all=ClassSystem.All;var abilities=all.SelectMany(c=>c.abilities).ToList();
    Check(all.Count==12 && abilities.Count==110 && abilities.Select(e=>e.ability).Distinct().Count()==110 && abilities.All(e=>e.ability.abilityClass==typeof(Ability_Class) && e.ability.uiIcon!=null && e.ability.uiIcon!=BaseContent.BadTex) && all.All(c=>c.Background!=null && c.primary!=null)
     && new[]{"LP_ArcanePower","LP_PrimalPower","LP_VerdantPower","LP_InnatePower","LP_ArtificePower"}.All(n=>DefDatabase<StatDef>.GetNamed(n).parts.Any(x=>x is StatPart_ClassPower)) && DefDatabase<StatDef>.GetNamed("LP_InnatePower").parts.Any(x=>x is StatPart_SorcererBloodline)
     && DefDatabase<ThingDef>.GetNamed("LP_AdventurersManual").GetCompProperties<CompProperties_UseEffect_Manual>()!=null,"12 classes, 110 vanilla abilities with icons (1.81.0 hotfix4 +3 wizard defences; +7 warlock), 5 caster power stats (innate has the bloodline part) and the manual live in the main assembly");
    // 2. 门槛：德鲁伊看种植或驯兽 6；巫妖不能当德鲁伊；术士血脉（力能龙裔）社交 4 就行，人类不行。
    var dr=ClsPawn(1);dr.skills.GetSkill(SkillDefOf.Animals).Level=6;var hdr=Adventurer.Get(dr);
    var lich=ClsPawn(1);lich.skills.GetSkill(SkillDefOf.Plants).Level=10;lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);
    var fd=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_ForceDragon");var dragon=fd==null?null:AdvPawn(fd);var hdg=dragon==null?null:Adventurer.Get(dragon);
    if(hdg!=null){foreach(var sk in DefDatabase<SkillDef>.AllDefsListForReading){var r=dragon.skills.GetSkill(sk);if(!r.TotallyDisabled)r.Level=0;}dragon.skills.GetSkill(SkillDefOf.Social).Level=4;hdg.level=1;hdg.scores=new[]{10,10,10,10,10,10};hdg.classes=new ClassData();hdg.Dirty();}
    var human=ClsPawn(1,0,4);
    Check(ClassSystem.CanAdd(hdr,Cls("Druid"))==null && ClassSystem.CanAdd(Adventurer.Get(lich),Cls("Druid"))=="死亡之躯与自然相斥" && (hdg==null || ClassSystem.Bloodline(dragon) && ClassSystem.CanAdd(hdg,Cls("Sorcerer"))==null) && ClassSystem.CanAdd(Adventurer.Get(human),Cls("Sorcerer"))!=null,"druid opens at animals 6; a lich cannot be a druid; a force dragon sorcerer opens at social 4, a human does not");
    // 3. 神恩：圣武士 + 牧师共用一条，上限按两职业等级之和；法力每个职业一条。
    var pc=ClsPawn(6,10,10);var hpc=Adventurer.Get(pc);pc.skills.GetSkill(SkillDefOf.Intellectual).Level=10;
    ClassSystem.AddLevel(hpc,ClassSystem.Paladin);ClassSystem.AddLevel(hpc,ClassSystem.Paladin);ClassSystem.AddLevel(hpc,Cls("Cleric"));
    float g0=ClassSystem.Resource(hpc,Cls("Cleric"));ClassSystem.AddResource(hpc,Cls("Cleric"),-1f);
    ClassSystem.AddLevel(hpc,Cls("Wizard"));
    Check(Near(g0,4f) && ClassSystem.MaxResource(hpc,ClassSystem.Paladin)==4 && Near(ClassSystem.Resource(hpc,ClassSystem.Paladin),3f) && hpc.classes.res.Count==2 && Near(ClassSystem.Resource(hpc,Cls("Wizard")),3f) && ClassSystem.MaxResource(hpc,Cls("Wizard"))==3,"paladin 2 + cleric 1 share one 神恩 bar (max 4, spending as cleric drains it); wizard 1 has its own 3 法力");
    // 4. 器械师门槛：不会器械师做不了；1 级能做奥术工坊那一档；5 级能做符文铭刻那一档；山岳之怒要学会山岳锻造；设置关掉谁都能做。
    RecipeDef R(string thing)=>DefDatabase<RecipeDef>.AllDefsListForReading.First(r=>r.ProducedThingDef?.defName==thing);
    var staff=R("LP_SleepStaff");var rep=R("LP_RunicRepeater");var wrath=R("LP_MountainWrath");
    var smith=ClsPawn(20);foreach(var sk in DefDatabase<SkillDef>.AllDefsListForReading){var r=smith.skills.GetSkill(sk);if(!r.TotallyDisabled)r.Level=20;}var hs=Adventurer.Get(smith);
    s.dndWeaponsNeedArtificer=true;bool none=!staff.PawnSatisfiesSkillRequirements(smith);
    ClassSystem.AddLevel(hs,Artificer.Class);bool l1=staff.PawnSatisfiesSkillRequirements(smith) && !rep.PawnSatisfiesSkillRequirements(smith);
    for(int i=0;i<4;i++)ClassSystem.AddLevel(hs,Artificer.Class);bool l5=rep.PawnSatisfiesSkillRequirements(smith) && !wrath.PawnSatisfiesSkillRequirements(smith);
    for(int i=0;i<8;i++)ClassSystem.AddLevel(hs,Artificer.Class);bool l13=wrath.PawnSatisfiesSkillRequirements(smith) && ClassSystem.Knows(smith,Artificer.MountainForge);
    var plain=ClsPawn(1);foreach(var sk in DefDatabase<SkillDef>.AllDefsListForReading){var r=plain.skills.GetSkill(sk);if(!r.TotallyDisabled)r.Level=20;}s.dndWeaponsNeedArtificer=false;bool off=wrath.PawnSatisfiesSkillRequirements(plain);s.dndWeaponsNeedArtificer=true;
    Check(none && l1 && l5 && l13 && off && staff.ProducedThingDef.description.Contains("器械师 1 级"),"artificer gate: none → no; level 1 → sleep staff; level 5 → runic repeater; level 13 (山岳锻造) → mountain wrath; setting off → anyone");
    // 5. 施法职业强度：法师 11 → 奥术强度 +0.5（1.97.0 每级 5%）；法师能力乘心灵敏感度（设置开着），器械师不乘。
    var wz=ClsPawn(12);wz.skills.GetSkill(SkillDefOf.Intellectual).Level=10;var hw=Adventurer.Get(wz);for(int i=0;i<11;i++)ClassSystem.AddLevel(hw,Cls("Wizard"));
    float ap=wz.GetStatValue(DefDatabase<StatDef>.GetNamed("LP_ArcanePower"));
    var mm=DefDatabase<AbilityDef>.GetNamed("LP_MagicMissile").comps.OfType<CompProperties_ClassEffect>().First();var qr=DefDatabase<AbilityDef>.GetNamed("LP_QuickRepair").comps.OfType<CompProperties_ClassEffect>().First();
    Check(Near(ap,ClassSystem.Power(10)+Adventurer.ClassPowerBonus(wz,Adventurer.Int,Adventurer.ArcaneAbilityPer)+.5f) &&Near(ClassSystem.PsychicFactor(wz,mm),Mathf.Max(.2f,wz.GetStatValue(StatDefOf.PsychicSensitivity))) && Near(ClassSystem.PsychicFactor(wz,qr),1f) && HasAb(wz,"LP_LightningBolt") && HasAb(wz,"LP_WallOfStone") && !HasAb(wz,"LP_Teleport"),"wizard 11: arcane power +0.5 (1.97.0: 5% per level), magic missile ×psychic sensitivity, artificer never; lightning bolt (7) and wall of stone (10), teleport (13) not yet");
    // 6. 施放：治疗伤口扣 1 法力？（牧师用神恩）治好友军；器械师的快速维修不耗资源。
    GenSpawn.Spawn(pc,altar.InteractionCell+IntVec3.South*3,map);var ally=ClsPawn(1);GenSpawn.Spawn(ally,altar.InteractionCell+IntVec3.South*4,map);
    ally.TakeDamage(new DamageInfo(DamageDefOf.Cut,8f,0f,-1f,null,ally.RaceProps.body.AllParts.First(x=>x.def.defName=="Torso")));float inj=ClassMelee.InjurySum(ally);float grace=ClassSystem.Resource(hpc,Cls("Cleric"));
    var cure=pc.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_CureWounds"));bool cast=cure.Activate(new LocalTargetInfo(ally),new LocalTargetInfo(ally));
    var rep2=smith.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_QuickRepair"));
    Check(cast && ClassMelee.InjurySum(ally)<inj && Near(ClassSystem.Resource(hpc,Cls("Cleric")),grace-1f) && ClassSystem.CannotCast(smith,rep2.def,((Ability_Class)rep2).Props)==null,"cure wounds heals an ally and spends 1 shared 神恩; artificer abilities cost nothing");
    // 7. 术士：强效 ×1.5 并用掉一次；魔涌时术士法术不耗法力。
    var so=ClsPawn(9,0,10);var hso=Adventurer.Get(so);for(int i=0;i<9;i++)ClassSystem.AddLevel(hso,Cls("Sorcerer"));
    var bolt=DefDatabase<AbilityDef>.GetNamed("LP_ChaosBolt").comps.OfType<CompProperties_ClassEffect>().First();float p0=ClassEffects.Power(so,bolt);
    Items.Give(so,"LP_Empowered",1800,2f);float p1=ClassEffects.Power(so,bolt);ClassCasters.ConsumeEmpower(so);float left=so.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_Empowered"))?.Severity??0f;
    hso.classes.res[Cls("Sorcerer")]=0f;bool broke=ClassSystem.CannotCast(so,DefDatabase<AbilityDef>.GetNamed("LP_ChaosBolt"),bolt)!=null;Items.Give(so,"LP_SorcerousSurge",600,1f);bool free=ClassSystem.CannotCast(so,DefDatabase<AbilityDef>.GetNamed("LP_ChaosBolt"),bolt)==null;
    Check(Near(p1,p0*1.5f) && Near(left,1f) && broke && free,"sorcerer: empowered ×1.5 and one charge used; surge makes sorcerer spells free");
    // 8. 冒险者手册：能用一次，给一次门槛豁免；没用掉之前不能再读。
    var reader=ClsPawn(3);var manual=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_AdventurersManual"));var use=manual.TryGetComp<CompUseEffect_Manual>();
    bool ok1=use.CanBeUsedBy(reader).Accepted && CompUseEffect_Manual.Apply(reader);bool twice=!use.CanBeUsedBy(reader).Accepted;bool took=ClassSystem.AddLevel(Adventurer.Get(reader),Cls("Wizard")) && Adventurer.Get(reader).classes.freeUnlocks==0;manual.Destroy();
    Check(ok1 && twice && took,"adventurer\u0027s manual: one waiver (wizard at intellectual 0), cannot read another until it is used");
    // 9. 侍僧的圣武士：冒险者经验 ×1.25。
    var aco=ClsPawn(3,10);var tr=DefDatabase<TraitDef>.GetNamedSilentFail("LP_Background_Acolyte");float x0=ClassSystem.XpFactor(aco);
    if(tr!=null)aco.story.traits.GainTrait(new Trait(tr));ClassSystem.AddLevel(Adventurer.Get(aco),ClassSystem.Paladin);
    Check(Near(x0,1f) && (tr==null || Near(ClassSystem.XpFactor(aco),1.25f)),"an acolyte paladin earns adventurer XP ×1.25 (was the VPE path bonus)");
   }finally{s.dndWeaponsNeedArtificer=oldArt;}
  }
  // 1.74.0 传奇职业能力（9 条）。
  static AbilityDef Ab(string n)=>DefDatabase<AbilityDef>.GetNamed(n);
  static CompProperties_ClassEffect Fx(string n)=>Ab(n).comps.OfType<CompProperties_ClassEffect>().First();
  void CheckClassesLegendary(){
   // 1. 定义：16 个，每个职业 1 主动 + 1 被动，都有图标。
   var legs=ClassSystem.All.SelectMany(c=>c.legendary).ToList();
   Check(legs.Count==24 && legs.Distinct().Count()==24 && ClassSystem.All.All(c=>c.legendary.Count==2 && c.legendary.Count(a=>a.comps.OfType<CompProperties_ClassEffect>().First().passive)==1) && legs.All(a=>a.abilityClass==typeof(Ability_Class) && a.uiIcon!=null && a.uiIcon!=BaseContent.BadTex && ClassLegendary.ClassOf(a)!=null),"24 legendary class abilities (warlock +2): one active and one passive per class, all with icons");
   // 2. 传奇职业点：25 / 30 / 35 / 40 级各 1 点（升级时发）。
   var a=ClsPawn(20,10);var ha=Adventurer.Get(a);ha.legendary=true;ha.level=24;ha.xp=Legend.XpFor(24);int at24=ClassLegendary.SyncPoints(ha);
   ha.LevelUp();int at25=ClassLegendary.Points(ha);for(int i=0;i<15;i++)ha.LevelUp();
   Check(at24==0 && at25==1 && ha.level==40 && ClassLegendary.Points(ha)==4 && ha.classes.legendaryGranted==4 && ClassLegendary.SyncPoints(ha)==0,"legendary class points at 25 / 30 / 35 / 40 (1 → 4), never more");
   // 3. 老存档补发：37 级、没有记录 → 补 3 点，只补一次。
   var o=ClsPawn(20);var ho=Adventurer.Get(o);ho.legendary=true;ho.level=37;ClassSystem.Tick250(ho);int once=ho.classes.legendaryPoints;ClassSystem.Tick250(ho);
   Check(once==3 && ho.classes.legendaryPoints==3,"an old level-37 save gets 3 legendary points once");
   // 4. 门槛（1.87.0）：战士 6 级不行；7 级学第一个；同职业第二个要 10 级；点数用完不能学。
   var f=ClsPawn(20,12);var hf=Adventurer.Get(f);hf.legendary=true;hf.level=30;ClassLegendary.SyncPoints(hf);for(int i=0;i<6;i++)ClassSystem.AddLevel(hf,ClassSystem.Fighter);
   var un=Ab("LP_LegUnyielding");var ta=Ab("LP_LegThousandArmies");string w6=ClassLegendary.CanLearn(hf,ClassSystem.Fighter,un);
   ClassSystem.AddLevel(hf,ClassSystem.Fighter);int max7=ClassSystem.MaxResource(hf,ClassSystem.Fighter);
   bool first=ClassLegendary.Learn(hf,ClassSystem.Fighter,un);string w7=ClassLegendary.CanLearn(hf,ClassSystem.Fighter,ta);
   for(int i=0;i<2;i++)ClassSystem.AddLevel(hf,ClassSystem.Fighter);string w9=ClassLegendary.CanLearn(hf,ClassSystem.Fighter,ta);ClassSystem.AddLevel(hf,ClassSystem.Fighter);bool second=ClassLegendary.Learn(hf,ClassSystem.Fighter,ta);
   Check(w6!=null && first && w7!=null && w7.Contains("10") && w9!=null && second && ClassLegendary.Points(hf)==0 && HasAb(f,"LP_LegUnyielding") && HasAb(f,"LP_LegThousandArmies"),"legendary gate: fighter 6 refused, 7 learns the first, the second needs 10 ("+w7+"), two points spent");
   // 5. 资源上限：每个传奇能力 +2。
   Check(max7==6 && ClassSystem.MaxResource(hf,ClassSystem.Fighter)==ClassSystem.MaxResource(10)+4,"each legendary ability adds +2 to the class resource (fighter 7: 6 → 8; fighter 10 with two: "+ClassSystem.MaxResource(hf,ClassSystem.Fighter)+")");
   // 5b. 1.87.0：战士 10 + 武僧 10 的传奇 40 级能用满 4 点传奇职业点。
   {var sp=ClsPawn(20,12);var hsp=Adventurer.Get(sp);hsp.legendary=true;hsp.level=40;ClassLegendary.SyncPoints(hsp);for(int i=0;i<10;i++){ClassSystem.AddLevel(hsp,ClassSystem.Fighter);ClassSystem.AddLevel(hsp,ClassSystem.Monk);}
    int learnt=ClassSystem.Fighter.legendary.Concat(ClassSystem.Monk.legendary).Count(x=>ClassLegendary.Learn(hsp,ClassLegendary.ClassOf(x),x));
    Check(ClassSystem.Level(hsp,ClassSystem.Fighter)==10 && ClassSystem.Level(hsp,ClassSystem.Monk)==10 && learnt==4 && ClassLegendary.Points(hsp)==0,"a fighter 10 / monk 10 split spends all 4 legendary class points ("+learnt+" learnt)");}
   // 6. 被动：无相之身 = 闪避 +25%、免疫击晕；不屈战魂每天一次。
   var m=ClsPawn(20,12);var hm=Adventurer.Get(m);hm.legendary=true;hm.level=25;ClassLegendary.SyncPoints(hm);for(int i=0;i<9;i++)ClassSystem.AddLevel(hm,ClassSystem.Monk);
   float dodge0=m.GetStatValue(StatDefOf.MeleeDodgeChance);ClassLegendary.Learn(hm,ClassSystem.Monk,Ab("LP_LegFormless"));GenSpawn.Spawn(m,altar.InteractionCell+IntVec3.East*4,map);
   m.stances.stunner.StunFor(300,null);bool stunned=m.stances.stunner.Stunned;
   Check(Items.Has(m,"LP_LegFormlessBuff") && m.GetStatValue(StatDefOf.MeleeDodgeChance)>dodge0 && !stunned,"无相之身: +25% dodge buff and stun immunity");
   // 7. 主动：天震掌击晕普通敌人；剧情首领在 50% 的挣脱里不被击晕。
   var foe=AdvPawn(null,AdvEnemy);GenSpawn.Spawn(foe,m.Position+IntVec3.North,map);var boss=AdvPawn(null,AdvEnemy);GenSpawn.Spawn(boss,m.Position+IntVec3.South,map);var hb=Adventurer.Get(boss);if(hb!=null)hb.storyLevel=true;
   bool hit=ClassLegendary.HeavenPalm(m,foe,1f,600,.9f);bool resisted=hb==null || !ClassLegendary.HeavenPalm(m,boss,1f,600,.1f);
   Check(hit && (foe.Dead || foe.stances.stunner.Stunned) && resisted,"天震掌 stuns a normal enemy; a story boss shakes it off on a 50% roll");
   // 8. 资源消耗：法术强效（法师 −1、最少 1，奥术 ×1.3）、神圣守护（复生术 2、神恩强度 +0.2）、永恒德鲁伊（荒野变形 0、不再衰老）。
   var mg=ClsPawn(30,0,12);var hg=Adventurer.Get(mg);hg.legendary=true;hg.level=40;ClassLegendary.SyncPoints(hg);mg.skills.GetSkill(SkillDefOf.Intellectual).Level=12;mg.skills.GetSkill(SkillDefOf.Plants).Level=12;
   for(int i=0;i<9;i++)ClassSystem.AddLevel(hg,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Wizard"));for(int i=0;i<9;i++)ClassSystem.AddLevel(hg,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Cleric"));
   float fb0=ClassEffects.Power(mg,Fx("LP_Fireball"));float div0=mg.GetStatValue(DefDatabase<StatDef>.GetNamed("LP_DivinePower"));
   ClassLegendary.Learn(hg,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Wizard"),Ab("LP_LegSpellMastery"));ClassLegendary.Learn(hg,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Cleric"),Ab("LP_LegDivineGuardian"));
   var dru=ClsPawn(20);var hd=Adventurer.Get(dru);hd.legendary=true;hd.level=25;ClassLegendary.SyncPoints(hd);dru.skills.GetSkill(SkillDefOf.Plants).Level=12;for(int i=0;i<9;i++)ClassSystem.AddLevel(hd,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Druid"));
   float age0=dru.ageTracker.BiologicalTicksPerTick;ClassLegendary.Learn(hd,DefDatabase<LP_ClassDef>.GetNamed("LP_Class_Druid"),Ab("LP_LegTimelessDruid"));
   Check(ClassLegendary.Cost(mg,Ab("LP_Fireball"),Fx("LP_Fireball"))==1 && ClassLegendary.Cost(mg,Ab("LP_MagicMissile"),Fx("LP_MagicMissile"))==0 && Near(ClassEffects.Power(mg,Fx("LP_Fireball")),fb0*1.3f)
    && ClassLegendary.Cost(mg,Ab("LP_Raise"),Fx("LP_Raise"))==2 && Near(mg.GetStatValue(DefDatabase<StatDef>.GetNamed("LP_DivinePower")),div0+.2f)
    && ClassLegendary.Cost(dru,Ab("LP_WildShape"),Fx("LP_WildShape"))==0 && age0>0f && dru.ageTracker.BiologicalTicksPerTick==0f,"spell mastery (fireball 2 → 1, cantrip magic missile stays 0, ×1.3), divine guardian (raise 2, divine power +0.2), timeless druid (wild shape free, no aging)");
   // 9. 主动能力都能放（不报错）：千军破、流星爆、自然之怒、魔力奔流、战争傀儡、神迹干预、神圣化身。
   var errors=new List<string>();int ran=0;var caster=ClsPawn(30,12,12);GenSpawn.Spawn(caster,altar.InteractionCell+IntVec3.West*4,map);var e1=AdvPawn(null,AdvEnemy);GenSpawn.Spawn(e1,caster.Position+IntVec3.West,map);
   foreach(var n in new[]{"LP_LegThousandArmies","LP_LegMeteorSwarm","LP_LegNatureWrath","LP_LegArcaneSurge","LP_LegWarGolem","LP_LegDivineIntervention","LP_LegDivineIncarnation"}){
    var free=CellFinder.RandomClosewalkCellNear(caster.Position,map,4,c=>c!=caster.Position && c.GetFirstPawn(map)==null && c.GetEdifice(map)==null && c.Standable(map));var far=CellFinder.RandomClosewalkCellNear((caster.Position+IntVec3.West*16).ClampInsideMap(map),map,3); // 1.99.0：施法者靠西边时这一格会出界（日志 ThingsListAt out of bounds）
    try{var tgt=n=="LP_LegWarGolem"?new LocalTargetInfo(free):n=="LP_LegMeteorSwarm" || n=="LP_LegNatureWrath"?new LocalTargetInfo(far):new LocalTargetInfo(e1.Position);ClassEffects.Apply(caster,Ab(n),Fx(n),tgt);ran++;}
    catch(Exception ex){errors.Add(n+": "+ex.GetType().Name+" "+ex.Message);}
   }
   bool torrent=Items.Has(caster,"LP_LegArcaneTorrent") && ClassSystem.FreeCast(caster,Fx("LP_Fireball"));var golem=map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("LP_RuneWarGolem")).FirstOrDefault();
   Check(errors.Count==0 && ran==7 && torrent && golem is Building_RuneTurret gt && gt.power>1f && Items.Has(caster,"LP_LegDivineAura"),"7 legendary actives run without errors; arcane torrent makes spells free; the war golem turret spawns"+(errors.Count==0?"":": "+string.Join(" | ",errors)));
   golem?.Destroy();
   foreach(var t in map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("LP_RuneWarGolem")).ToList())t.Destroy();
   // 10. 冲锋（hotfix）：远处起跳要把人装进飞行物生成在地图上，不能“消失”（MakeFlyer 之后 caster.Map 已是 null）。
   var ch=ClsPawn(3,10);var chFrom=CellFinder.RandomClosewalkCellNear(altar.InteractionCell+IntVec3.South*8,map,3,c=>c.GetFirstPawn(map)==null && c.Standable(map));GenSpawn.Spawn(ch,chFrom,map);
   var chFoe=AdvPawn(null,AdvEnemy);var chTo=CellFinder.RandomClosewalkCellNear(chFrom+IntVec3.East*5,map,1,c=>c.GetFirstPawn(map)==null && c.Standable(map));GenSpawn.Spawn(chFoe,chTo,map);
   bool chStarted=!chFoe.Position.AdjacentTo8WayOrInside(ch.Position) && Charge.Start(ch,chFoe,1.3f);
   var chFlyer=map.listerThings.AllThings.OfType<LP_ChargeFlyer>().FirstOrDefault(f=>f.FlyingPawn==ch);
   Check(chStarted && (ch.Spawned || chFlyer!=null && chFlyer.Spawned),"冲锋 from 5 cells: the charger stays on the map (spawned charge flyer carrying them), not vanished");
   if(chFlyer!=null && !ch.Spawned){chFlyer.GetDirectlyHeldThings().TryDrop(ch,chFlyer.Position,map,ThingPlaceMode.Near,out _);chFlyer.Destroy();}
  }
  void PrepareClassesSave(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=false;
   try{
    var p=ClsPawn(6,10,8);advMade.Remove(p);var h=Adventurer.Get(p);
    for(int i=0;i<3;i++)ClassSystem.AddLevel(h,ClassSystem.Fighter);ClassSystem.AddLevel(h,ClassSystem.Paladin);
    h.classes.res[ClassSystem.Fighter]=2.5f;h.classes.kept.Add(DefDatabase<AbilityDef>.GetNamed("LP_Whirlwind"));h.classes.freeUnlocks=1;
    h.classes.legendaryPoints=2;h.classes.legendaryGranted=3;h.classes.legendary.Add(DefDatabase<AbilityDef>.GetNamed("LP_LegUnyielding"));h.classes.legendaryTicks["LP_LegUnyielding"]=1234;ClassSystem.Sync(h); // 1.74.0
    GenSpawn.Spawn(p,altar.InteractionCell+IntVec3.North*3,map);clsSavedId=p.ThingID;
    Check(HasAb(p,"LP_Whirlwind") && HasAb(p,"LP_BattleCry"),"save fixture: fighter 3 / paladin 1 with a kept whirlwind");
    PrepareClasses3Save(); // 野蛮人 / 游侠
    PrepareSubclassSave(); // 子职业
   }finally{Adventurer.TestSuppressed=sup;}
  }
  void CheckClassesAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==clsSavedId);var h=Adventurer.Get(p);
   Check(h!=null && ClassSystem.Level(h,ClassSystem.Fighter)==3 && ClassSystem.Level(h,ClassSystem.Paladin)==1 && Near(ClassSystem.Resource(h,ClassSystem.Fighter),2.5f) && h.classes.kept.Count==1 && h.classes.freeUnlocks==1 && h.classes.legendaryPoints==2 && h.classes.legendaryGranted==3 && h.classes.legendary.Count==1 && h.classes.legendaryTicks.TryGetValue("LP_LegUnyielding",out int lt) && lt==1234 && HasAb(p,"LP_LegUnyielding") && HasAb(p,"LP_Whirlwind") && HasAb(p,"LP_DivineSmite") && ClassSystem.Points(h)==2,"class levels, resources, kept abilities, manual waiver, legendary points / abilities and the vanilla abilities survive save/load");
   p?.Destroy();
   CheckClasses3AfterLoad();
   CheckSubclassAfterLoad();
  }
 }
}
