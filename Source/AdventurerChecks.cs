using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.48.0：Test.ps1 -Only Adventurer。冒险者等级：生成（标准数组 + 种族 + 背景）、种族表、经验表、击杀分配、袭击分配、属性提升、属性效果、学习速度、
 // 部位生命、里程碑、对象范围（死灵造物、13 岁）、旧存档补生成（只一次）、存读档。-Isekai 档另跑 CheckAdventurerIsekai（共存默认值）。
 public partial class LichChecks {
  static string advSavedId;static int advSavedLevel;static float advSavedXp;static string advSavedScores,advSavedMilestones;
  readonly List<Pawn> advMade=new List<Pawn>();
  Pawn AdvPawn(XenotypeDef x=null,Faction f=null,float age=25f){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,f??Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:x??XenotypeDefOf.Baseliner,mustBeCapableOfViolence:age>=13f,forceNoBackstory:true,developmentalStages:age<13f?DevelopmentalStage.Child:DevelopmentalStage.Adult,fixedBiologicalAge:age,fixedChronologicalAge:age));
   advMade.Add(p);return p;
  }
  static string Join(int[] a)=>string.Join(",",a);
  Faction AdvEnemy=>Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f=>!f.IsPlayer && !f.Hidden && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer));

  void CheckAdventurer(){
   var s=LichMod.Settings;bool oldOn=s.adventurer,oldAuto=s.adventurerAutoASI,oldCo=s.adventurerIsekaiCoexist;int oldXp=s.adventurerXpPercent,oldFx=s.adventurerEffectPercent;
   // 装了 Isekai 时（-Isekai -Only Adventurer）先关掉共存模式，六维效果的检查才有意义；共存本身在最后的 CheckAdventurerIsekai 里查。
   Adventurer.TestSuppressed=false;s.adventurer=true;s.adventurerAutoASI=false;s.adventurerXpPercent=100;s.adventurerEffectPercent=100;s.adventurerIsekaiCoexist=false;
   try{
    Check(Adventurer.Def!=null && Adventurer.Def.hediffClass==typeof(Hediff_Adventurer) && !Adventurer.Def.isBad && !Adventurer.Def.everCurableByItem,"LP_Adventurer hediff definition (not bad, not curable)");
    Check(ThingDefOf.Human.inspectorTabsResolved.Any(t=>t is ITab_Adventurer),"冒险者 inspect tab is attached to humans");
    // 1.52.0 热修：设置窗口两栏的分组都在表里（1.58.0 起 18 个）（各画一次、不会自动换栏），冒险者在右栏第一位。
    var sg=LichMod.SettingsLeft.Concat(LichMod.SettingsRight).ToList();
    Check(sg.Count==19 && sg.All(g=>g.draw!=null) && sg.Select(g=>g.draw.Method).Distinct().Count()==19 && sg.Select(g=>g.name).Distinct().Count()==19 && LichMod.SettingsRight[0].name=="冒险者" && new[]{"命匣等","事件","信仰","成神","冒险者","六臂蛇人","五色龙","DND 特性"}.All(n=>sg.Any(g=>g.name==n)),"settings window lists all 18 groups once (1.58.0 DND 特性), 冒险者 first in the right column");
    // 1. 新殖民者：标准数组 + 种族 + 背景
    var a=AdvPawn();var h=Adventurer.Get(a);
    Check(h!=null && h.level==1 && h.initialized && Near(h.xp,0f) && !h.Visible,"new colonist gets a hidden level-1 LP_Adventurer");
    var bas=Adventurer.BaseArray(a);var rac=Adventurer.RacialBonus(a,bas);var tr=Adventurer.TraitBonus(a);
    Check(bas.OrderByDescending(x=>x).SequenceEqual(Adventurer.StandardArray) && rac.Sum()==2 && rac.All(x=>x==0 || x==1) && h.scores.SequenceEqual(Enumerable.Range(0,6).Select(i=>Math.Min(20,bas[i]+rac[i]+tr[i]))),"scores = standard array + human (+1 to top two) + traits ("+Join(h.scores)+" = "+Join(bas)+" + "+Join(rac)+" + "+Join(tr)+")");
    foreach(var sk in DefDatabase<SkillDef>.AllDefsListForReading)a.skills.GetSkill(sk).Level=0;
    a.skills.GetSkill(SkillDefOf.Shooting).Level=20;a.skills.GetSkill(SkillDefOf.Social).Level=18;a.skills.GetSkill(SkillDefOf.Melee).Level=1;
    bas=Adventurer.BaseArray(a);
    Check(bas[Adventurer.Dex]==15 && bas[Adventurer.Cha]==14 && bas[Adventurer.Con]==13,"standard array follows the skill order (shooting 20 → Dex 15, social 18 → Cha 14, constitution key 5 → 13): "+Join(bas));
    int strBefore=Adventurer.TraitBonus(a)[Adventurer.Str];a.story.traits.GainTrait(new Trait(BackgroundTraits.Soldier));
    Check(Adventurer.TraitBonus(a)[Adventurer.Str]==strBefore+1,"soldier background gives +1 Str");
    // 2. 种族
    var orc=AdvPawn(DefDatabase<XenotypeDef>.GetNamed("LP_HalfOrc"));var ogre=AdvPawn(DefDatabase<XenotypeDef>.GetNamed("LP_Ogre"));
    var orcBase=Adventurer.BaseArray(orc);var orcRac=Adventurer.RacialBonus(orc,orcBase);var ogreRac=Adventurer.RacialBonus(ogre,Adventurer.BaseArray(ogre));
    Check(orcRac.SequenceEqual(new[]{2,0,1,0,0,0}) && ogreRac[Adventurer.Int]==-2 && ogreRac[Adventurer.Str]==3,"racial bonuses: half-orc Str+2 Con+1 ("+Join(orcRac)+"), ogre Str+3 Int-2 ("+Join(ogreRac)+")");
    var orcH=Adventurer.Get(orc);var orcTr=Adventurer.TraitBonus(orc);
    Check(orcH!=null && orcH.scores[Adventurer.Str]==Math.Min(20,orcBase[Adventurer.Str]+2+orcTr[Adventurer.Str]),"half-orc generated scores include the racial Str +2");
    Check(new[]{"LP_Elf","LP_Dwarf","LP_FireGiant","LP_Troll","LP_MindFlayer","LP_Githyanki","LP_GenasiFire","LP_Aasimar","LP_Tiefling","LP_Oozefolk","LP_Titan","LP_Genie","LP_Aarakocra","LP_Loxodon","LP_Minotaur","LP_ForceDragon","LP_StormGiant","LP_CloudGiant","LP_FrostGiant","LP_GenasiWater","LP_GenasiEarth","LP_GenasiAir","LP_AwakenedTitan","LP_EttinKin","LP_CyclopsKin","LP_MarilithKin"}.All(n=>DefDatabase<XenotypeDef>.GetNamedSilentFail(n)!=null && Adventurer.Racial.ContainsKey(n)),"every DND xenotype has a racial bonus entry");
    // 3. 经验表
    var b=AdvPawn();var hb=Adventurer.Get(b);
    Adventurer.GainXP(b,2200f);
    Check(hb.level==5 && hb.Proficiency==3 && Adventurer.Proficiency(1)==2 && Adventurer.Proficiency(17)==6 && Adventurer.LevelForXp(118300f)==20,"2200 XP → level 5, proficiency +3 (table 1→+2 … 17→+6)");
    // 5. 属性提升
    Check(hb.pendingASI==1,"level 4 gives one ability score improvement");
    hb.scores[Adventurer.Str]=19;
    Check(!hb.ApplyASI(Adventurer.Str) && hb.scores[Adventurer.Str]==19 && hb.pendingASI==1,"+2 is refused when it would exceed 20");
    hb.scores[Adventurer.Str]=18;
    Check(hb.ApplyASI(Adventurer.Str) && hb.scores[Adventurer.Str]==20 && hb.pendingASI==0,"+2 raises 18 to 20 and uses the improvement");
    s.adventurerAutoASI=true;hb.scores=new[]{12,16,13,10,10,8};Adventurer.GainXP(b,11300f-hb.xp);
    Check(hb.level>=8 && hb.pendingASI==0 && hb.scores[Adventurer.Dex]==18,"colonist auto-assign (setting on) puts +2 on the highest score: "+Join(hb.scores)+" lv "+hb.level);
    s.adventurerAutoASI=false;
    // 4. 击杀分配
    var enemy=AdvEnemy;Check(enemy!=null,"a hostile humanlike faction exists for the kill check");
    var c1=AdvPawn();var c2=AdvPawn();var v=AdvPawn(null,enemy);
    GenSpawn.Spawn(v,altar.InteractionCell+IntVec3.East*4,map);
    float x1=Adventurer.Get(c1).xp,x2=Adventurer.Get(c2).xp,cp=v.kindDef.combatPower;
    Adventurer.RecordDamage(v,c1,10f);Adventurer.RecordDamage(v,c2,10f);v.Kill(null);
    Check(v.Dead && cp>0f && Near(Adventurer.Get(c1).xp-x1,cp*.5f) && Near(Adventurer.Get(c2).xp-x2,cp*.5f),"kill XP is shared by damage: two colonists with half the damage each get combatPower × 0.5 ("+cp+")");
    var v2=AdvPawn(null,enemy);v2.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);float x3=Adventurer.Get(c1).xp;Adventurer.RecordDamage(v2,c1,10f);
    Adventurer.State.damage.Remove(v2);Check(!Adventurer.KillCounts(v2) && Near(Adventurer.Get(c1).xp,x3),"prisoners give no kill XP");
    // 守住袭击：点数 × 0.25 平分给参战的人。
    float y1=Adventurer.Get(c1).xp,y2=Adventurer.Get(c2).xp;
    var raid=new RaidRecord{map=map,points=400f,start=Find.TickManager.TicksGame};raid.fighters.Add(c1);raid.fighters.Add(c2);Adventurer.State.raids.Add(raid);
    int shared=Adventurer.State.Resolve(raid);
    Check(shared==2 && Near(Adventurer.Get(c1).xp-y1,50f) && Near(Adventurer.Get(c2).xp-y2,50f) && !Adventurer.State.raids.Contains(raid),"defended raid: 400 points × 0.25 split between 2 fighters");
    // 里程碑每人每 key 一次；经验倍率
    float m1=Adventurer.Milestone(c1,"testMilestone",100f),m2=Adventurer.Milestone(c1,"testMilestone",100f);
    s.adventurerXpPercent=200;float doubled=Adventurer.GainXP(c2,100f);s.adventurerXpPercent=100;
    Check(Near(m1,100f) && m2==0f && Near(doubled,200f),"milestone pays once per key; XP multiplier 200% doubles XP");
    // 训练只到 3 级
    var t=AdvPawn();Adventurer.GainTrainingXP(t,250f);Adventurer.GainTrainingXP(t,250f);
    Check(Adventurer.Get(t).level==3 && Near(Adventurer.Get(t).xp,300f),"training XP stops at level 3");
    // 6. 属性效果
    var c=AdvPawn();var hc=Adventurer.Get(c);var melee=StatDefOf.MeleeDamageFactor;
    hc.scores=new[]{10,10,10,10,10,10};hc.Dirty();float baseMelee=c.GetStatValue(melee);
    hc.scores[Adventurer.Str]=16;hc.Dirty();float str16=c.GetStatValue(melee);
    s.adventurerEffectPercent=0;float off=c.GetStatValue(melee);s.adventurerEffectPercent=100;
    Check(Near(str16/baseMelee,2.3f) && Near(off/baseMelee,1f),"Str 16 → melee damage ×2.3 (3.5 carrying-capacity curve); effect strength 0% → ×1 ("+baseMelee+" / "+str16+" / "+off+")");
    float carry0=c.GetStatValue(StatDefOf.CarryingCapacity);hc.scores[Adventurer.Str]=10;hc.Dirty();float carry1=c.GetStatValue(StatDefOf.CarryingCapacity);
    Check(Near(carry0/carry1,2.3f),"Str 16 → carrying capacity ×2.3");
    // 1.65.0 曲线：10 = ×1，15 = ×2，20 = ×4，24 = ×7，30 = ×16；低于 10 = 属性/10
    Check(Near(Adventurer.Curve(10),1f) && Near(Adventurer.Curve(11),1.15f) && Near(Adventurer.Curve(15),2f) && Near(Adventurer.Curve(20),4f) && Near(Adventurer.Curve(24),7f) && Near(Adventurer.Curve(30),16f) && Near(Adventurer.Curve(8),.8f),"score curve: 10 ×1, 11 ×1.15, 15 ×2, 20 ×4, 24 ×7, 30 ×16, 8 ×0.8");
    hc.scores[Adventurer.Str]=20;hc.AddBoon(Epic.Might,Adventurer.Str); // 威能之恩：上限 24、力量 20 → 24
    float str24=c.GetStatValue(melee),carry24=c.GetStatValue(StatDefOf.CarryingCapacity);int eff24=hc.Eff(Adventurer.Str);
    hc.boons.Remove(Epic.Might);hc.mightAbility=-1;hc.scores[Adventurer.Str]=10;hc.Dirty();
    Check(eff24==24 && Near(str24/baseMelee,7f) && carry24/carry1>=7f-.01f,"Str 24 → melee damage ×7 and carrying capacity at least ×7 (超凡 adds its own carry factor) of Str 10 ("+(str24/baseMelee).ToString("0.##")+" / "+(carry24/carry1).ToString("0.##")+")");
    // 部位生命：5 级、体质 14 → (1 + 4×1%) × 1.75 = ×1.82
    hc.level=5;hc.scores[Adventurer.Con]=14;hc.Dirty();var torso=c.RaceProps.body.corePart;
    float withAdv=PartHealth.Maximum(torso,c);Adventurer.forcedScale=0f;float without=PartHealth.Maximum(torso,c);Adventurer.forcedScale=-1f;
    Check(Near(Adventurer.PartHealthFactor(c),1.82f) && Math.Abs(withAdv-Mathf.Ceil(without*1.82f))<=1f,"level 5 Con 14 → part health ×1.82 ("+without+" → "+withAdv+")");
    hc.scores[Adventurer.Con]=6;hc.level=1;
    Check(Near(Adventurer.PartHealthFactor(c),1f),"negative constitution never lowers part health");
    // 7. 学习速度
    var sk2=c.skills.GetSkill(SkillDefOf.Melee);sk2.Level=0;sk2.xpSinceLastLevel=0f;
    hc.scores[Adventurer.Str]=10;c.skills.Learn(SkillDefOf.Melee,100f,false,true);float plain=sk2.xpSinceLastLevel;
    sk2.xpSinceLastLevel=0f;hc.scores[Adventurer.Str]=16;c.skills.Learn(SkillDefOf.Melee,100f,false,true);float strong=sk2.xpSinceLastLevel;
    hc.scores[Adventurer.Str]=3;float weak=Adventurer.LearnFactor(c,SkillDefOf.Melee);
    hc.scores[Adventurer.Str]=1;s.adventurerEffectPercent=200;float floor=Adventurer.LearnFactor(c,SkillDefOf.Melee);s.adventurerEffectPercent=100;
    Check(plain>0f && Near(strong/plain,2.3f) && Near(weak,.5f) && Near(floor,.5f) && Near(Adventurer.LearnFactor(c,SkillDefOf.Crafting),1f),"skill learning ×(score multiplier): Str 16 ×"+(strong/plain).ToString("0.###")+", Str 3 ×0.3 floored to ×"+weak.ToString("0.###")+", crafting unaffected");
    // 9. 对象范围
    var child=AdvPawn(null,enemy,8f);
    Check(Adventurer.Get(child)==null,"children under 13 have no adventurer level");
    child.ageTracker.AgeBiologicalTicks=13L*GenDate.TicksPerYear+1000L;
    AccessTools.Method(typeof(Pawn_AgeTracker),"BirthdayBiological").Invoke(child.ageTracker,new object[]{13});
    Check(Adventurer.Get(child)?.level==1,"the 13th birthday (growth moment) adds level 1");
    var master=AdvPawn(null,Factions.Hostile??enemy);var skel=FactionConstructs.MakeBound("Skeleton",master,Factions.Hostile??enemy);advMade.Add(skel);
    Check(Necro.Construct(skel)!=null && Adventurer.Get(skel)==null && !Adventurer.Eligible(skel) && Adventurer.TryAdd(skel,1,true)==null,"necromantic constructs have no adventurer level");
    // 10. 旧存档补生成只发生一次
    var old=AdvPawn();old.health.RemoveHediff(Adventurer.Get(old));old.records.AddTo(RecordDefOf.KillsHumanlikes,25);
    var st=Adventurer.State;bool wasBackfilled=st.backfilled;st.backfilled=false;
    int n1=st.TryBackfill(new[]{old});var ho=Adventurer.Get(old);
    Check(n1==1 && ho!=null && ho.level==3 && Near(ho.xp,300f) && st.backfilled,"old-save backfill: 25 humanlike kills → level 3 at 300 XP");
    old.health.RemoveHediff(ho);int n2=st.TryBackfill(new[]{old});
    Check(n2==0 && Adventurer.Get(old)==null,"backfill runs only once per save");
    st.backfilled=wasBackfilled;
    // 关掉总开关：不再获得经验
    s.adventurer=false;float none=Adventurer.GainXP(c2,500f);s.adventurer=true;
    Check(none==0f,"master switch off stops XP");
    CheckAdventurerStage2(); // 1.48.1 敌人等级与专长（AdventurerChecks2.cs）
    CheckAdventurerStage3(); // 1.49.0 史诗恩赐与传奇（AdventurerChecks3.cs）
    CheckAdventurerStage4(); // 1.50.0 传奇等级与传奇属性点（AdventurerChecks4.cs）
    CheckAdventurerStage5(); // 1.51.0 种族加点亲和与稀有天赋（AdventurerChecks5.cs）
    CheckAdventurerStage6(); // 1.52.0 热修：冒险者分页（AdventurerChecks6.cs）
    CheckAdventurerWork(); // 1.61.0 热修：日常工作经验（AdventurerChecks7.cs）
   }finally{
    s.adventurer=oldOn;s.adventurerAutoASI=oldAuto;s.adventurerXpPercent=oldXp;s.adventurerEffectPercent=oldFx;s.adventurerIsekaiCoexist=oldCo;Adventurer.forcedScale=-1f;
    foreach(var p in advMade)if(!p.Destroyed && p.ThingID!=advSavedId)p.Destroy();
    advMade.Clear();
    Adventurer.TestSuppressed=true;
   }
   CheckAdventurerIsekai();
  }
  // 8. 存读档
  void PrepareAdventurerSave(){
   Adventurer.TestSuppressed=false;
   try{
    var p=AdvPawn();advMade.Remove(p);GenSpawn.Spawn(p,altar.InteractionCell+IntVec3.West*3,map);
    var h=Adventurer.Get(p);Adventurer.GainXP(p,9000f);h.scores=new[]{16,12,14,10,13,8};h.pendingASI=1;Adventurer.Milestone(p,"portal",1f);h.vpeBanked=2;
    Adventurer.State.backfilled=true; // 自测进程里补生成被抑制，这里手动置位，检查存档
    advSavedId=p.ThingID;advSavedLevel=h.level;advSavedXp=h.xp;advSavedScores=Join(h.scores);advSavedMilestones=string.Join(",",h.milestonesDone);
    Check(advSavedLevel==7,"save fixture is level 7");
    // 1.48.1 专长与今天的次数
    h.AddFeat(Feats.Lucky);h.AddFeat(Feats.Alert);h.TryUse(Feats.Lucky);h.TryUse(Feats.Lucky);
    advSavedFeats=string.Join(",",h.feats.Select(f=>f.defName));advSavedLuckyLeft=h.UsesLeft(Feats.Lucky);
    Check(advSavedLuckyLeft==1,"save fixture has lucky + alert with 1 lucky use left");
    h.workXpToday=23.5f;h.workXpDay=1234;h.workXpRem=.75f;h.workXpTotal=77f; // 1.61.0 热修：日常工作经验
    PrepareEpicSave(h); // 1.49.0
    PrepareLegendSave(h); // 1.50.0
    PrepareAffinitySave(h); // 1.51.0
   }finally{Adventurer.TestSuppressed=true;}
  }
  void CheckAdventurerAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==advSavedId);var h=Adventurer.Get(p);
   Check(h!=null && h.level==advSavedLevel && Near(h.xp,advSavedXp) && Join(h.scores)==advSavedScores && string.Join(",",h.milestonesDone)==advSavedMilestones && h.pendingASI==1 && h.vpeBanked==2 && h.initialized,"adventurer level, XP, scores, milestones and pending ASI survive save/load");
   Check(Adventurer.State!=null && Adventurer.State.backfilled,"backfill flag is saved");
   Check(string.Join(",",h.feats.Select(f=>f.defName))==advSavedFeats && h.UsesLeft(Feats.Lucky)==advSavedLuckyLeft,"feats and today's remaining uses survive save/load ("+advSavedFeats+", lucky "+h.UsesLeft(Feats.Lucky)+")");
   Check(Near(h.workXpToday,23.5f) && h.workXpDay==1234 && Near(h.workXpRem,.75f) && Near(h.workXpTotal,77f),"work XP today / day / remainder / total survive save/load");
   CheckEpicAfterLoad(h); // 1.49.0
   CheckLegendAfterLoad(h); // 1.50.0
   CheckAffinityAfterLoad(h); // 1.51.0
   p.Destroy();Epic.State?.RefreshLegend();
  }

  // -Isekai 档（-lichitemsonly）：检测到 Isekai 时默认“共存模式”，六维效果关、等级照常。
  void CheckAdventurerIsekai(){
   bool isekai=ModsConfig.IsActive(Adventurer.IsekaiId);
   Check(Adventurer.IsekaiActive==isekai && new Settings().adventurerIsekaiCoexist,"Isekai detection ("+isekai+"); coexistence mode is on by default");
   if(!isekai)return;
   var s=LichMod.Settings;bool oldCo=s.adventurerIsekaiCoexist,oldOn=s.adventurer;int oldFx=s.adventurerEffectPercent;
   Adventurer.TestSuppressed=false;s.adventurer=true;s.adventurerEffectPercent=100;s.adventurerIsekaiCoexist=true;
   try{
    var p=AdvPawn();var h=Adventurer.Get(p);
    Check(h!=null && Adventurer.EffectsHandedToIsekai && Adventurer.EffectScale==0f,"with Isekai, coexistence turns the six-score effects off");
    Adventurer.GainXP(p,900f);
    Check(h.level==4 && h.pendingASI==1,"levels, XP and ASI still work with Isekai");
    h.scores=new[]{10,10,10,10,10,10};h.Dirty();float base10=p.GetStatValue(StatDefOf.MeleeDamageFactor);h.scores[0]=18;h.Dirty();float base18=p.GetStatValue(StatDefOf.MeleeDamageFactor);
    s.adventurerIsekaiCoexist=false;float on18=p.GetStatValue(StatDefOf.MeleeDamageFactor);
    Check(Near(base18,base10) && Near(on18/base10,3f) && Near(Adventurer.LearnFactor(p,SkillDefOf.Melee),3f),"coexistence off → Str 18 gives melee ×3 again");
   }finally{
    s.adventurerIsekaiCoexist=oldCo;s.adventurer=oldOn;s.adventurerEffectPercent=oldFx;
    foreach(var p in advMade)if(!p.Destroyed)p.Destroy();advMade.Clear();Adventurer.TestSuppressed=true;
   }
  }
 }
}
