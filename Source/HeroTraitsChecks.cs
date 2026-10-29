using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.58.0：Test.ps1 -Only HeroTraits（强力特性，23 项）。-Isekai -Only HeroTraits 另查 Isekai 互斥。
 public partial class LichChecks {
  readonly List<Pawn> htMade=new List<Pawn>();
  static string htSaved;static string htIds;
  IntVec3 htBase=IntVec3.Invalid;
  IntVec3 HtCell(int dx,int dz){
   if(!htBase.IsValid)htBase=map.AllCells.Where(c=>c.x>20 && c.z>20 && c.x<map.Size.x-20 && c.z<map.Size.z-20 && CellRect.CenteredOn(c,4).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetFirstPawn(map)==null && x.GetFirstBuilding(map)==null)).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
   return htBase+new IntVec3(dx,0,dz);
  }
  Pawn HtPawn(string xeno=null,Faction f=null,bool spawn=false,int dx=0,int dz=0){
   var x=xeno==null?XenotypeDefOf.Baseliner:DefDatabase<XenotypeDef>.GetNamed(xeno);
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,f??Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:x,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25f,fixedChronologicalAge:25f));
   htMade.Add(p);
   foreach(var t in p.story.traits.allTraits.Where(t=>HeroTraits.IsSlot(t.def) || HeroTraits.IsDeed(t.def)).ToList())p.story.traits.RemoveTrait(t);
   p.apparel?.DestroyAll();foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(inj); // 生成时的旧伤疤会干扰伤口计数
   var h=Adventurer.Get(p);if(h!=null){h.scores=new[]{10,10,10,10,10,10};h.legendSpent=new int[Legend.Count];h.legendPoints=0;h.Dirty();}
   if(spawn)GenSpawn.Spawn(p,HtCell(dx,dz),map);
   return p;
  }
  static Hediff_Adventurer HtAdv(Pawn p)=>Adventurer.Get(p)??Adventurer.TryAdd(p,1,true);
  static DamageInfo HtHit(DamageDef d,float amount,Pawn by,ThingDef weapon)=>new DamageInfo(d,amount,0f,-1f,by,null,weapon);

  void CheckHeroTraits(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oCo=s.adventurerIsekaiCoexist,oAff=s.adventurerAffinity,oLeg=s.adventurerLegend,oAuto=s.adventurerAutoASI,oHt=s.heroTraits,oHe=s.heroTraitsEnemies;int oRate=s.heroTraitRate,oFx=s.adventurerEffectPercent;
   bool oSup=Adventurer.TestSuppressed,oHsup=HeroTraits.TestSuppressed;
   Adventurer.TestSuppressed=false;HeroTraits.TestSuppressed=false;
   s.adventurer=true;s.adventurerIsekaiCoexist=false;s.adventurerAffinity=true;s.adventurerLegend=true;s.adventurerAutoASI=false;s.heroTraits=true;s.heroTraitsEnemies=true;s.heroTraitRate=100;s.adventurerEffectPercent=100;
   var comp=GameComponent_HeroTraits.Get;
   try{
    // 1. 定义：26 个特性 commonality 0，金 / 紫占名额，事迹不占；名字上色；设置默认值
    var allDefs=HeroTraits.Gold.Concat(HeroTraits.Purple).Concat(HeroTraits.Deeds).ToList();
    bool labels=allDefs.All(d=>d.degreeDatas.All(x=>TraitRarity.Hex(d,x.degree)!=null && x.label.StartsWith("<color="+TraitRarity.Hex(d,x.degree)+">"))) && HeroTraits.Deeds.All(d=>d.degreeDatas.All(x=>x.label.Contains("#50C0C0"))); // 2026-10-29 颜色按稀有度
    var ns=new Settings();
    Check(allDefs.Count==26 && allDefs.All(d=>Near(d.GetGenderSpecificCommonality(Gender.Male),0f)) && HeroTraits.Gold.Concat(HeroTraits.Purple).All(d=>d.exclusionTags==null || !d.exclusionTags.Contains(HeroTraits.PowerTag)) && HeroTraits.Deeds.All(d=>d.exclusionTags.NullOrEmpty()) && labels && comp!=null && ns.heroTraits && ns.heroTraitsEnemies && ns.heroTraitRate==100 && ns.heroTraitMax==3 && ns.heroTraitExtra==25,"26 power traits load (6 gold, 13 purple, 7 deeds), commonality 0, gold/purple share the LP_PowerTrait slot, colored labels, settings default on / 100%");
    // 2. 出现率：人类掷 10000 次（固定随机种子），金 1% / 紫 4% / 天赋 2% 都在 ±50% 内；300% 时翻三倍；生成后缀确实会给
    int g=0,pu=0,ta=0,g3=0;Rand.PushState(15801);try{for(int i=0;i<10000;i++){int k=HeroTraits.RollKind(HeroTraits.Tier.Common,1f,Rand.Value);if(k==1)g++;else if(k==2)pu++;else if(k==3)ta++;}for(int i=0;i<10000;i++)if(HeroTraits.RollKind(HeroTraits.Tier.Common,3f,Rand.Value)==1)g3++;}finally{Rand.PopState();}
    HeroTraits.TestKind=HeroTraits.KindGold;Pawn forced;try{forced=HtPawnRaw();}finally{HeroTraits.TestKind=null;}
    var ft=HeroTraits.SlotTrait(forced);
    Check(g>=50 && g<=150 && pu>=200 && pu<=600 && ta>=100 && ta<=300 && g3>=150 && g3<=450 && ft!=null && HeroTraits.IsGold(ft.def),"human roll rates (10000 seeded): gold "+g+" (1%), purple "+pu+" (4%), talent "+ta+" (2%), gold at 300% "+g3+"; the generation postfix gives a forced gold roll ("+HeroTraits.Plain(ft)+")");
    // 2b. 1.66.0 热修：可以有多个强力特性（默认最多 3 个）；满了再给会取代最早的；天命之间仍互斥；额外几率 100% 时补到上限
    {
     int oMax=s.heroTraitMax,oEx=s.heroTraitExtra;
     try{
      s.heroTraitMax=3;var multi=HtPawn();HeroTraits.Give(multi,HeroDefOf.LP_HeroicSpark);HeroTraits.Give(multi,HeroDefOf.LP_Edge);HeroTraits.Give(multi,HeroDefOf.LP_Prophesied);
      int three=HeroTraits.Count(multi);HeroTraits.Give(multi,HeroDefOf.LP_WeaveChild);bool destinySwap=!HeroTraits.Has(multi,HeroDefOf.LP_Prophesied) && HeroTraits.Has(multi,HeroDefOf.LP_WeaveChild) && HeroTraits.Count(multi)==3;
      HeroTraits.Give(multi,HeroDefOf.LP_Hardened);bool capSwap=HeroTraits.Count(multi)==3 && HeroTraits.Has(multi,HeroDefOf.LP_Hardened) && !HeroTraits.Has(multi,HeroDefOf.LP_HeroicSpark);
      var inf=HeroTraits.Info(multi);bool flags=inf!=null && inf.edge && inf.weave && inf.hardened && inf.destiny==HeroDefOf.LP_WeaveChild;
      s.heroTraitExtra=100;var ex=HtPawn();HeroTraits.Give(ex,HeroDefOf.LP_Edge);int got=HeroTraits.Extra(ex);int exCount=HeroTraits.Count(ex);
      Check(three==3 && destinySwap && capSwap && flags && got==2 && exCount==3,"several power traits: 英雄之资 + 锋芒 + 预言之子 all kept ("+three+"); a second destiny replaces the first; a 4th at the cap (3) replaces the oldest; effect flags see all of them; 100% extra fills up to 3 ("+exCount+")");
     }finally{s.heroTraitMax=oMax;s.heroTraitExtra=oEx;}
    }
    // 3. 传奇种族不出金色；紫色只有反骨、勤勉和六个强化天赋
    var giant=HtPawn("LP_FireGiant");int gg=0;Rand.PushState(15802);try{for(int i=0;i<10000;i++)if(HeroTraits.RollKind(HeroTraits.TierOf(giant),3f,Rand.Value)==1)gg++;}finally{Rand.PopState();}
    var gch=HeroTraits.Choices(giant,HeroTraits.KindPurple).Select(c=>c.def).ToList();
    Check(HeroTraits.TierOf(giant)==HeroTraits.Tier.Legendary && gg==0 && HeroTraits.Choices(giant,HeroTraits.KindTalent).Count>0 && gch.Count==8 && !gch.Contains(HeroDefOf.LP_Edge) && !gch.Contains(HeroDefOf.LP_GiftedScore) && gch.Contains(HeroDefOf.LP_DefyFate) && gch.Contains(HeroDefOf.LP_Diligent) && HeroTraits.TierOf(HtPawn("LP_Elf"))==HeroTraits.Tier.Medium && HeroTraits.TierOf(HtPawn("LP_Kobold"))==HeroTraits.Tier.Common,"fire giant (legendary tier) never rolls gold ("+gg+"/10000 at 300%) and its purple pool is the 8 allowed (defy, diligent, 6 rule traits); elf = medium, kobold = common");
    // 4. 1.66.0 热修：强力特性可以并存（默认上限 3）；已有强力特性时生成不再掷；事迹不占名额
    var one=HtPawn();HeroTraits.Give(one,HeroDefOf.LP_Edge);one.story.traits.GainTrait(new Trait(Talents.Tempered));HeroTraits.Give(one,HeroDefOf.LP_HeroicSpark);HeroTraits.Give(one,HeroDefOf.LP_DeedDragonslayer);
    HeroTraits.TestKind=HeroTraits.KindPurple;Trait again;try{again=HeroTraits.OnGenerated(one);}finally{HeroTraits.TestKind=null;}
    Check(HeroTraits.Count(one)==3 && HeroTraits.Has(one,HeroDefOf.LP_Edge) && HeroTraits.Has(one,HeroDefOf.LP_HeroicSpark) && HeroTraits.Has(one,HeroDefOf.LP_DeedDragonslayer) && again==null,"power traits stack: 锋芒 + 百炼成钢 + 英雄之资 kept together (3 = cap), a deed does not count, a second generation roll is skipped");
    // 5. 倍率取较大不相乘：火巨人 + 锋芒 = 原值 ×1.75；人类 + 锋芒 = ×1.5
    HeroTraits.Give(giant,HeroDefOf.LP_Edge);var hgi=HtAdv(giant);var human=HtPawn();HeroTraits.Give(human,HeroDefOf.LP_Edge);var hhu=HtAdv(human);
    Check(Near(Affinity.LegendMult(hgi,Legend.Melee),1.75f) && Near(Affinity.LegendMult(hhu,Legend.Melee),1.5f) && Near(Affinity.LegendMult(hhu,Legend.Ranged),1.5f) && Near(Affinity.LegendMult(hhu,Legend.Health),1.1f),"max(race, trait), never multiplied: fire giant + 锋芒 legend melee ×"+Affinity.LegendMult(hgi,Legend.Melee).ToString("0.##")+" (race value), human + 锋芒 ×1.5, human health stays ×1.1");
    // 6. 英雄之资：21 级 3 个传奇属性点、属性上限 22、属性提升 3 点、所有传奇项 ×1.5
    var sp=HtPawn();HeroTraits.Give(sp,HeroDefOf.LP_HeroicSpark);var hsp=HtAdv(sp);hsp.Setup(new[]{10,10,10,10,10,10},20);hsp.legendary=true;int lp20=hsp.legendPoints;Adventurer.GainXP(sp,Legend.XpFor(21)-hsp.xp,false);
    Check(hsp.level==21 && hsp.legendPoints-lp20==3 && hsp.Cap(Adventurer.Str)==22 && hsp.ASIPoints==3 && Near(Affinity.LegendMult(hsp,Legend.Work),1.5f),"英雄之资: level 21 gives 3 legend points ("+(hsp.legendPoints-lp20)+"), score cap 22, ASI 3 points, every legend stat ×1.5");
    // 7. 极致·力量：上限 26、力量 ×2.5、近战 / 血量 ×2
    var pa=HtPawn();HeroTraits.Give(pa,HeroDefOf.LP_Paragon,1);var hpa=HtAdv(pa);
    Check(hpa.Cap(Adventurer.Str)==26 && hpa.Cap(Adventurer.Dex)==20 && Near(Affinity.ScoreMult(hpa,Adventurer.Str),2.5f) && Near(Affinity.LegendMult(hpa,Legend.Melee),2f) && Near(Affinity.LegendMult(hpa,Legend.Health),2f) && Near(Affinity.LegendMult(hpa,Legend.Work),1.1f),"极致·力量: Str cap 26 (Dex 20), Str ×2.5, legend melee/health ×2");
    // 8. 勤勉：6 级多一次属性提升（共 2 次），普通人 1 次
    var di=HtPawn();HeroTraits.Give(di,HeroDefOf.LP_Diligent);var hdi=HtAdv(di);hdi.Setup(new[]{10,10,10,10,10,10},6);var nd=HtPawn();var hnd=HtAdv(nd);hnd.Setup(new[]{10,10,10,10,10,10},6);
    Check(hdi.pendingASI==2 && hnd.pendingASI==1,"勤勉: level 6 has 2 ability score improvements (normal 1)");
    // 9. 元素之触·火：武器命中额外 3 点灼烧，火焰伤害 -50%，火元素裔 -75%
    var fire=HtPawn(null,null,true,0,0);HeroTraits.Give(fire,HeroDefOf.LP_ElementTouched,1);var foe=HtPawn(null,AdvEnemy,true,1,0);
    int burns=foe.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count(i=>i.def.defName=="Burn");
    foe.TakeDamage(HtHit(DamageDefOf.Blunt,2f,fire,fire.def));int burns2=foe.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count(i=>i.def.defName=="Burn");
    var gen=HtPawn("LP_GenasiFire");HeroTraits.Give(gen,HeroDefOf.LP_ElementTouched,1);
    float fr=HeroCombat.InFactor(fire,HeroTraits.Info(fire),foe,HtHit(DamageDefOf.Flame,10f,foe,null)),gr=HeroCombat.InFactor(gen,HeroTraits.Info(gen),foe,HtHit(DamageDefOf.Flame,10f,foe,null));
    Check(burns2>burns && Near(fr,.5f) && Near(gr,.25f) && HeroCombat.Element(DamageDefOf.Flame)==1 && HeroCombat.Element(DefDatabase<DamageDef>.GetNamed("AcidBurn"))==4,"元素之触·火: a punch adds a 3-point burn ("+burns+" → "+burns2+" burns), fire damage ×"+fr+", fire genasi ×"+gr);
    // 10. 法术抗体：敌方不带武器的伤害 ×0.65，武器伤害不变；心灵控制 50%
    var wa=HtPawn();HeroTraits.Give(wa,HeroDefOf.LP_SpellWarded);var wi=HeroTraits.Info(wa);
    float spell=HeroCombat.InFactor(wa,wi,foe,HtHit(Pacts.Eldritch,10f,foe,null)),sword=HeroCombat.InFactor(wa,wi,foe,HtHit(DamageDefOf.Cut,10f,foe,ThingDefOf.MeleeWeapon_Knife));
    HeroCombat.TestRoll=.2f;bool r1=DNDMind.TryResist(wa);HeroCombat.TestRoll=.8f;bool r2=HeroCombat.MindResist(wa);HeroCombat.TestRoll=null;
    Check(Near(spell,.65f) && Near(sword,1f) && r1 && !r2,"法术抗体: enemy spell damage ×"+spell+", weapon ×"+sword+"; mind effects resisted on a 50% roll");
    // 11. 决斗者：身边一个敌人 近战命中 +25%，三个 -10%
    var du=HtPawn(null,null,true,3,3);HeroTraits.Give(du,HeroDefOf.LP_Duelist);var e1=HtPawn(null,AdvEnemy,true,4,3);
    HeroCombat.RefreshDuelists(new List<Pawn>{du});float solo=HeroCombat.MeleeHit(du,e1);
    HtPawn(null,AdvEnemy,true,2,3);HtPawn(null,AdvEnemy,true,3,4);HeroCombat.RefreshDuelists(new List<Pawn>{du});float mob=HeroCombat.MeleeHit(du,e1);
    Check(Near(solo,.25f) && HeroCombat.DuelCount(du)==3 && Near(mob,-.1f),"决斗者: one adjacent enemy → melee hit +"+solo+", three → "+mob);
    // 12. 宿敌猎人·龙：对龙伤害 +25%、命中 +15%；对普通人 0
    var fh=HtPawn();HeroTraits.Give(fh,HeroDefOf.LP_FavoredEnemy,1);var fi=HeroTraits.Info(fh);var dragon=HtPawn("LP_ForceDragon",AdvEnemy);DndCreatureType.Forget(dragon);
    Check(DndCreatureType.Is(dragon,DndType.Dragon) && Near(HeroCombat.TypeBonus(fh,fi,dragon,true),.25f) && Near(HeroCombat.MeleeHit(fh,dragon),.15f) && Near(HeroCombat.TypeBonus(fh,fi,foe,true),0f),"宿敌猎人·龙: +25% damage and +15% melee hit against a force dragon, nothing against a plain human");
    // 13. 类型加成合计封顶 60%：宿敌克星 40% + 屠龙者 30% = 70% → 60%
    HeroTraits.Give(fh,HeroDefOf.LP_DeedDragonslayer);fi=HeroTraits.Info(fh);fi.rec.favoredBane=true;float cap=HeroCombat.TypeBonus(fh,fi,dragon,true);fi.rec.favoredBane=false;
    Check(Near(cap,.6f) && Near(HeroCombat.TypeBonus(fh,fi,dragon,true),.55f),"type bonuses from 1.58 traits cap at +60% (bane 40% + dragonslayer 30% → "+cap+"), without bane 55%");
    // 14. 天命升阶：冒险者 10 级 → 显现，传奇 → 升华（得到魔网涌动）
    var wv=HtPawn();HeroTraits.Give(wv,HeroDefOf.LP_WeaveChild);var hwv=HtAdv(wv);var wr=HeroTraits.Record(wv);
    bool s0=wr.stage==0 && !HeroDestiny.CheckStage(wv,HeroTraits.Info(wv),wr);hwv.level=10;bool s1=HeroDestiny.CheckStage(wv,HeroTraits.Info(wv),wr) && wr.stage==1;
    hwv.legendary=true;bool s2=HeroDestiny.CheckStage(wv,HeroTraits.Info(wv),wr) && wr.stage==2 && wv.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_HeroWeaveSurge"))!=null;
    Check(s0 && s1 && s2 && HeroDestiny.WeaveImmune(wv),"destiny stages: awakened at level 1, manifest at adventurer level 10, ascended as a legendary adventurer (魔网涌动 granted, weave collapse immunity)");
    // 15. 预言之子：命运未尽救下一次致命伤，冷却中第二次照常死
    var pr=HtPawn(null,null,true,-3,-3);HeroTraits.Give(pr,HeroDefOf.LP_Prophesied);var prr=HeroTraits.Record(pr);int saved=HeroDestiny.Saved;
    pr.Kill(new DamageInfo(DamageDefOf.Blunt,30f));bool lived=!pr.Dead && HeroDestiny.Saved==saved+1 && prr.fateTick!=int.MinValue && !HeroDestiny.FateReady(pr,prr);
    pr.Kill(new DamageInfo(DamageDefOf.Blunt,30f));
    Check(lived && pr.Dead && prr.prophecy>=0 && prr.prophecy<HeroDestiny.Prophecies.Length,"预言之子: 命运未尽 turns a lethal blow into downed (20-day cooldown), the second blow while on cooldown kills; prophecy "+prr.prophecy);
    // 16. 命运未尽与命运之恩：同一次只触发一个（先用冷却短的预言），预言冷却时才轮到恩赐
    var bo=HtPawn(null,null,true,-3,3);HeroTraits.Give(bo,HeroDefOf.LP_Prophesied);var hbo=HtAdv(bo);hbo.AddBoon(Epic.Fate);int boonTick=hbo.fateTick;saved=HeroDestiny.Saved;
    bo.Kill(new DamageInfo(DamageDefOf.Blunt,30f));bool first=!bo.Dead && HeroDestiny.Saved==saved+1 && hbo.fateTick==boonTick;
    bo.Kill(new DamageInfo(DamageDefOf.Blunt,30f));bool second=!bo.Dead && HeroDestiny.Saved==saved+1 && hbo.fateTick!=boonTick;
    bo.Kill(new DamageInfo(DamageDefOf.Blunt,30f));
    Check(first && second && bo.Dead,"命运未尽 and 命运之恩 never fire together: the prophecy saves first (boon untouched), the boon saves the next one, the third blow kills");
    // 17. 邪契者偿契：连续两次失败降一阶（宗主之怒禁用魔能爆），觉醒阶再连败特性消失
    var pk=HtPawn();HeroTraits.Give(pk,HeroDefOf.LP_PactDestiny,1);var hpk=HtAdv(pk);hpk.level=10;var pki=HeroTraits.Info(pk);var pkr=pki.rec;HeroDestiny.CheckStage(pk,pki,pkr);
    void Fail(){int now=Find.TickManager.TicksGame;HeroDestiny.StartDemand(pk,HeroTraits.Info(pk),pkr,now);pkr.demandDeadline=now-1;HeroDestiny.PactTick(pk,HeroTraits.Info(pk),pkr,now);}
    Fail();var blast=pk.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_HeroPactBlast"));bool wrathOff=blast!=null && blast.CompOfType<CompAbilityEffect_HeroPower>().GizmoDisabled(out _);int afterOne=pkr.stage;
    Fail();bool demoted=pkr.stage==0 && pkr.demoted && !HeroDestiny.CheckStage(pk,HeroTraits.Info(pk),pkr);
    Fail();Fail();
    Check(afterOne==1 && wrathOff && demoted && !HeroTraits.Has(pk,HeroDefOf.LP_PactDestiny) && pk.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_PactForsaken"))!=null,"邪契者: a failed payment gives 宗主之怒 (blast disabled), two in a row demote manifest → awakened (no re-advance until paid), two more at awakened remove the trait (背弃之契)");
    // 18. 偿契成功：处决记录满足邪魔的要求，恢复升阶
    var pk2=HtPawn();HeroTraits.Give(pk2,HeroDefOf.LP_PactDestiny,1);var pk2r=HeroTraits.Record(pk2);int t0=Find.TickManager.TicksGame;HeroDestiny.StartDemand(pk2,HeroTraits.Info(pk2),pk2r,t0);pk2r.demoted=true;comp.lastExecutionTick=t0;HeroDestiny.PactTick(pk2,HeroTraits.Info(pk2),pk2r,t0);
    Check(pk2r.demand==null && !pk2r.demoted && pk2r.pactFails==0 && pk2r.nextDemandTick>=t0+HeroDestiny.DemandMin*GenDate.TicksPerDay,"偿契: an execution after the demand satisfies the fiend patron, clears the demotion and schedules the next demand in 15-20 days");
    comp.lastExecutionTick=int.MinValue;
    // 19. 神明干预：神之勇士（洛山达，升华）在 3 名殖民者倒地时自动触发一次，治愈伤口；冷却中不再触发
    var ch=HtPawn(null,null,true,-2,-2);HeroTraits.Give(ch,HeroDefOf.LP_DivineChampion,1);var hch=HtAdv(ch);hch.legendary=true;HeroDestiny.CheckStage(ch,HeroTraits.Info(ch),HeroTraits.Record(ch));comp.Refresh();
    int iv=HeroDestiny.Interventions;var downed=new List<Pawn>();
    for(int k=0;k<3;k++){var d=HtPawn(null,null,true,-1+k,-4);var cut=HediffMaker.MakeHediff(HediffDefOf.Cut,d,d.RaceProps.body.corePart);cut.Severity=4f;d.health.AddHediff(cut);d.health.AddHediff(HediffDefOf.Anesthetic);downed.Add(d);}
    bool allDown=downed.All(d=>d.Downed),healed=downed.All(d=>d.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity)<3f);int fired=HeroDestiny.Interventions-iv;
    bool again2=HeroDestiny.CheckIntervention(map),ready=HeroDestiny.DivineReady(HeroTraits.Record(ch)),undead=HeroDestiny.ChampionTarget(1,HtPawnUndead());
    Check(fired==1 && allDown && healed && !again2 && !ready && undead,"神明干预: three downed colonists trigger Lathander's dawn once (wounds healed), then it waits a quadrum; Lathander's champion targets undead (fired "+fired+", down "+allDown+", healed "+healed+", again "+again2+", ready "+ready+", undead "+undead+", sums "+string.Join("/",downed.Select(d=>d.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity).ToString("0.##")))+", god "+HeroDestiny.ChampionGod(1)?.key+", champions "+comp.champions.Count+", stage "+HeroTraits.Record(ch).stage+")");
    foreach(var d in downed)d.Destroy();ch.Destroy();comp.Refresh();
    // 20. 屠龙者：参与击杀一条龙首领 → 授予，传奇冒险者 +5 传奇属性点
    var ds=HtPawn();var hds=HtAdv(ds);hds.legendary=true;int lp=hds.legendPoints;var boss=HtPawn("LP_ForceDragon",AdvEnemy);HtAdv(boss).storyLevel=true;
    HeroDeeds.OnKilled(boss,new HeroKillState{killer=ds,killerMelee=true});
    Check(HeroTraits.Has(ds,HeroDefOf.LP_DeedDragonslayer) && hds.legendPoints-lp==HeroDeeds.DragonPoints && !HeroTraits.HasSlot(ds),"屠龙者: taking part in a force-dragon boss kill grants the deed and +"+(hds.legendPoints-lp)+" legend points to a legendary adventurer (no slot used)");
    // 21. 绝境英雄：袭击中只剩一人能战斗且守住 → 授予，+1 次属性提升；25% 觉醒英雄之资
    var ls=HtPawn(null,null,true,2,-2);var hls=HtAdv(ls);int asi=hls.pendingASI;var raid=new RaidRecord{map=map,points=500f,start=Find.TickManager.TicksGame};
    var others=map.mapPawns.FreeColonistsSpawned.Where(c=>c!=ls && !c.Downed).ToList();foreach(var c in others)c.health.AddHediff(HediffDefOf.Anesthetic);
    HeroDeeds.SampleRaids(new List<RaidRecord>{raid});var lone=raid.lastStand;
    foreach(var c in others){var an=c.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic);if(an!=null)c.health.RemoveHediff(an);}
    HeroDeeds.TestRoll=.1f;HeroDeeds.RaidDefended(raid);HeroDeeds.TestRoll=null;
    Check(lone==ls && HeroTraits.Has(ls,HeroDefOf.LP_LastStand) && hls.pendingASI==asi+1 && HeroTraits.Has(ls,HeroDefOf.LP_HeroicSpark),"绝境英雄: the only colonist still able to fight during a defended raid gets the deed, +1 ASI, and on the 25% roll 英雄之资");
    // 22. 敌人：剧情首领 50% 带强力特性（人类优先金色），关掉“敌人携带强力特性”就没有；冠军 30% 事迹
    var eb=HtPawn(null,AdvEnemy);var et=HeroTraits.EnemyBoss(eb,.1f);var eb2=HtPawn(null,AdvEnemy);var none=HeroTraits.EnemyBoss(eb2,.9f);
    s.heroTraitsEnemies=false;var off=HeroTraits.EnemyBoss(HtPawn(null,AdvEnemy),.1f);s.heroTraitsEnemies=true;var deed=HeroTraits.EnemyDeed(eb2,.1f);
    Check(et!=null && (HeroTraits.IsGold(et.def) || DndTraits.IsCurse(et.def)) && none==null && off==null && deed!=null && HeroTraits.IsDeed(deed),"enemy bosses: a human boss rolls gold (or the 15% red curse) on the 50% chance ("+HeroTraits.Plain(et)+"), nothing above it or with the setting off; champions get a deed on the 30% roll");
    // 23. 快速返回：没有 1.58 特性的人缓存成 null
    var plain=HtPawn();
    Check(HeroTraits.Info(plain)==null && HeroTraits.Info(plain)==null && HeroCombat.TypeBonus(plain,null,dragon,true)==0f && Near(HeroCombat.MeleeHit(plain,dragon),0f),"pawns without 1.58 traits are cached as null (every hot-path hook returns on the first lookup)");
    CheckHeroTraitsIsekai();
   }finally{
    s.adventurer=oOn;s.adventurerIsekaiCoexist=oCo;s.adventurerAffinity=oAff;s.adventurerLegend=oLeg;s.adventurerAutoASI=oAuto;s.heroTraits=oHt;s.heroTraitsEnemies=oHe;s.heroTraitRate=oRate;s.adventurerEffectPercent=oFx;
    HeroTraits.TestKind=null;HeroCombat.TestRoll=null;HeroDeeds.TestRoll=null;
    foreach(var p in htMade){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();else if(!p.Destroyed)p.Destroy();}htMade.Clear();comp?.Refresh();
    Adventurer.TestSuppressed=oSup;HeroTraits.TestSuppressed=oHsup;
   }
  }
  Pawn HtPawnRaw(){var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25f,fixedChronologicalAge:25f));htMade.Add(p);return p;}
  Pawn HtPawnUndead(){var skel=FactionConstructs.MakeBound("Skeleton",HtPawn(null,AdvEnemy),AdvEnemy);htMade.Add(skel);DndCreatureType.Forget(skel);return skel;}
  // -Isekai -Only HeroTraits：互斥写进双方的 conflictingTraits；天赋·力量与“力大”按 degree 互斥。
  void CheckHeroTraitsIsekai(){
   if(!ModsConfig.IsActive(Adventurer.IsekaiId)){Check(DefDatabase<TraitDef>.GetNamedSilentFail("Isekai_Protagonist")==null,"Isekai not loaded: the mutual-exclusion patch is not applied (Bridges/Isekai)");return;}
   TraitDef I(string n)=>DefDatabase<TraitDef>.GetNamed(n);
   bool defs=HeroTraits.Gold.All(d=>new[]{"Isekai_Protagonist","Isekai_Antagonist","Isekai_Regressor","Isekai_SummonedHero","Isekai_Reincarnated","Isekai_HollowCore","Isekai_SealedPower"}.All(n=>d.conflictingTraits.Contains(I(n)) && I(n).conflictingTraits.Contains(d)))
    && HeroDefOf.LP_TotemSpirit.ConflictsWith(I("Isekai_BerserkerBlood")) && I("Isekai_BerserkerBlood").conflictingTraits.Contains(HeroDefOf.LP_TotemSpirit) && HeroDefOf.LP_SpellWarded.ConflictsWith(I("Isekai_Enlightened")) && I("Isekai_Enlightened").conflictingTraits.Contains(HeroDefOf.LP_SpellWarded);
   var p=HtPawn();foreach(var t in p.story.traits.allTraits.Where(t=>t.def.defName.StartsWith("Isekai_")).ToList())p.story.traits.RemoveTrait(t);
   p.story.traits.GainTrait(new Trait(I("Isekai_Mighty")));int blocked=HeroIsekai.Blocked;HeroTraits.Give(p,HeroDefOf.LP_GiftedScore,1);bool strBlocked=!HeroTraits.Has(p,HeroDefOf.LP_GiftedScore) && HeroIsekai.Blocked==blocked+1;
   HeroTraits.Give(p,HeroDefOf.LP_GiftedScore,2);bool dexOk=HeroTraits.Has(p,HeroDefOf.LP_GiftedScore);
   bool noStrChoice=!HeroTraits.Choices(p,HeroTraits.KindPurple).Any(c=>c.def==HeroDefOf.LP_GiftedScore && c.degrees.Contains(1));
   Check(defs && strBlocked && dexOk && noStrChoice,"Isekai: destinies / 英雄之资 / 极致 conflict both ways with the 7 Isekai golden archetypes, 图腾之魂 × 狂战士之血, 法术抗体 × 开悟; 天赋·力量 is blocked (and never rolled) on a 力大 pawn while 天赋·敏捷 is fine");
  }

  // ---------- 存读档：特性、阶段、预言、偿契计时、宿敌派系 ----------
  static string HtSummary(Pawn p){
   var r=HeroTraits.Record(p);if(r==null)return "no record";
   return string.Join(",",p.story.traits.allTraits.Where(t=>HeroTraits.Is(t.def)).Select(t=>t.def.defName+":"+t.Degree))+"|"+r.stage+"|"+r.prophecy+"|"+r.fateTick+"|"+r.demand+"|"+r.demandStart+"|"+r.demandDeadline+"|"+r.nextDemandTick+"|"+r.pactFails+"|"+r.meditateTicks+"|"+(r.favoredFaction?.loadID??-1)+"|"+r.favoredKills+"|"+r.giantKills;
  }
  void PrepareHeroTraitsSave(){
   var a=HtPawn(null,null,true,0,2);HeroTraits.Give(a,HeroDefOf.LP_Prophesied);var ra=HeroTraits.Record(a);ra.prophecy=3;ra.stage=1;ra.fateTick=4321;HeroTraits.Give(a,HeroDefOf.LP_Giantbane);ra.giantKills=2;
   var b=HtPawn(null,null,true,1,2);HeroTraits.Give(b,HeroDefOf.LP_PactDestiny,3);var rb=HeroTraits.Record(b);rb.demand="old";rb.demandStart=1111;rb.demandDeadline=2222;rb.nextDemandTick=3333;rb.pactFails=1;rb.meditateTicks=500;
   var c=HtPawn(null,null,true,2,2);HeroTraits.Give(c,HeroDefOf.LP_FavoredEnemy,7);var rc=HeroTraits.Record(c);rc.favoredFaction=AdvEnemy;rc.favoredKills=7;
   htIds=a.ThingID+","+b.ThingID+","+c.ThingID;htSaved=HtSummary(a)+"#"+HtSummary(b)+"#"+HtSummary(c);htMade.Clear();
  }
  void CheckHeroTraitsAfterLoad(){
   var ids=htIds.Split(',');var ps=ids.Select(id=>map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==id)).ToList();
   string now=ps.All(p=>p!=null)?string.Join("#",ps.Select(HtSummary)):"missing";
   Check(now==htSaved && HeroTraits.Info(ps[0])?.prophesied==true && HeroTraits.Info(ps[1])?.pact==3,"power traits, destiny stage, prophecy, fate cooldown, pact demand timers, favored faction and deed counters survive save/load ("+htSaved+")");
   foreach(var p in ps)p?.Destroy();
  }
 }
}
