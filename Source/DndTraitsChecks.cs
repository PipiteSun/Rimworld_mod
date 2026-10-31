using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.62.0：Test.ps1 -Only DndTraits（性格特性与诅咒之力，22 项，含存读档）。VPE 部分在 -VPE -Only DndTraits（Bridges/VPE/Source/DndTraitsVPE.cs）。
 public partial class LichChecks {
  readonly List<Pawn> dtMade=new List<Pawn>();
  static string dtIds;static int dtCooldown,dtLycanNext;
  Pawn DtPawn(string xeno=null,Faction f=null,bool spawn=false,int dx=0,int dz=0){
   var x=xeno==null?XenotypeDefOf.Baseliner:DefDatabase<XenotypeDef>.GetNamed(xeno);
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,f??Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,forcedXenotype:x,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25f,fixedChronologicalAge:25f));
   dtMade.Add(p);
   foreach(var t in p.story.traits.allTraits.Where(t=>DndTraits.Is(t.def) || HeroTraits.IsSlot(t.def) || HeroTraits.IsDeed(t.def) || t.def==TraitDefOf.Psychopath || t.def==TraitDefOf.Kind || t.def==TraitDefOf.Bloodlust || t.def.defName=="PsychicSensitivity").ToList())p.story.traits.RemoveTrait(t);
   p.apparel?.DestroyAll();foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(inj);
   if(spawn)GenSpawn.Spawn(p,HtCell(dx,dz),map);
   return p;
  }
  static Trait DtGive(Pawn p,TraitDef d,int degree=0){var t=new Trait(d,degree,true);p.story.traits.GainTrait(t,true);return p.story.traits.allTraits.Contains(t)?t:null;}
  static ThingWithComps DtWeapon(ThingDef d,ThingDef stuff){return (ThingWithComps)ThingMaker.MakeThing(d,stuff);}

  void CheckDndTraits(){
   var s=LichMod.Settings;bool oP=s.dndPersonality,oC=s.dndCurses,oAdv=s.adventurer;int oR=s.dndPersonalityRate;
   bool oSup=DndTraits.TestSuppressed,oHsup=HeroTraits.TestSuppressed,oAsup=Adventurer.TestSuppressed;var oVpe=HeroDestiny.forceVPE;
   DndTraits.TestSuppressed=false;Adventurer.TestSuppressed=false;s.dndPersonality=true;s.dndCurses=true;s.dndPersonalityRate=100;s.adventurer=true;
   var comp=GameComponent_DndTraits.Get;var batch=new List<Pawn>();
   try{
    // 1. 定义：18 个性格特性 + 3 个诅咒，commonality 0；诅咒红色、占 LP_PowerTrait 名额；设置默认值；同一特性不同程度的互斥
    var pers=DndTraits.Personality.ToList();var curses=DndTraits.Curses.ToList();var ns=new Settings();
    bool zero=pers.Concat(curses).All(d=>Near(d.GetGenderSpecificCommonality(Gender.Male),0f) && !d.ConfigErrors().Any());
    bool red=curses.All(d=>d.exclusionTags.Contains("LP_Curse") && HeroTraits.IsSlot(d) && d.degreeDatas.All(x=>TraitRarity.Mark(d,x.degree)!="Curse" || x.label.Contains("#D05050")));
    var good=DtPawn();DtGive(good,DndTraitDefOf.LP_Morality,1);bool psy=DtGive(good,TraitDefOf.Psychopath)!=null;
    var evil=DtPawn();DtGive(evil,DndTraitDefOf.LP_Morality,-1);bool kind=DtGive(evil,TraitDefOf.Kind)!=null;
    var wildP=DtPawn();DtGive(wildP,DndTraitDefOf.LP_ArcaneGift);bool wild=DtGive(wildP,DndTraitDefOf.LP_WildMagic)!=null;
    Check(pers.Count==18 && curses.Count==7 && zero && red && ns.dndPersonality && ns.dndCurses && ns.dndPersonalityRate==100 && !psy && !kind && !wild && DndTraits.Info(good).morality==1,"18 personality + 7 curse traits load (commonality 0, curses red in the power slot), settings default on / 100%; 仁善 blocks 心理变态, 邪恶 blocks 善良, 奥术天赋 blocks 狂野魔法");
    // 2. 生成 200 人：每人性格特性 ≤2、阵营 ≤1、原版名额 ≤3，至少一个的比例 10%～45%
    for(int k=0;k<200;k++){var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:25f,fixedChronologicalAge:25f));batch.Add(p);}
    int maxP=batch.Max(DndTraits.PersonalityCount),maxA=batch.Max(p=>p.story.traits.allTraits.Count(t=>DndTraits.IsAlignment(t.def))),maxV=batch.Where(p=>DndTraits.PersonalityCount(p)>0).Select(p=>DndTraits.VanillaCap-DndTraits.Room(p)).DefaultIfEmpty(0).Max();
    float share=batch.Count(p=>DndTraits.PersonalityCount(p)>0)/(float)batch.Count;
    Check(maxP<=2 && maxA<=1 && maxV<=3 && share>=.1f && share<=.45f,"200 generated pawns: personality ≤2 (max "+maxP+"), alignment ≤1 (max "+maxA+"), pawns given one stay within 3 vanilla slots (max "+maxV+"), "+share.ToStringPercent()+" have at least one (target ~25%)");
    // 3. 派系加权：塞伊邪恶 ×3；1000 次抽取邪恶比例高于基准
    var thay=Find.FactionManager.FirstFactionOfDef(DefDatabase<FactionDef>.GetNamed("LP_FaerunHostile"));
    var tp=thay!=null?DtPawn(null,thay):null;var bp=DtPawn();var evilE=DndTraits.Table.First(e=>e.def==DndTraitDefOf.LP_Morality && e.degree==-1);
    int te=0,be=0;Rand.PushState(16203);try{for(int k=0;k<1000;k++){if(tp!=null && DndTraits.PickEntry(tp)==evilE)te++;if(DndTraits.PickEntry(bp)==evilE)be++;}}finally{Rand.PopState();}
    var ext=DefDatabase<FactionDef>.GetNamed("LP_FaerunHostile").GetModExtension<LP_TraitWeightExtension>();var kob=DtPawn("LP_Kobold");var servE=DndTraits.Table.First(e=>e.def==DndTraitDefOf.LP_DragonServant);
    Check(ext!=null && Near(ext.Factor(DndTraitDefOf.LP_Morality,-1),3f) && (tp==null || Near(DndTraits.Weight(tp,evilE),evilE.weight*3f) && te>be*1.5f) && Near(DndTraits.Weight(kob,servE),servE.weight*5f),"faction weights: 塞伊 evil ×3 ("+te+" vs baseline "+be+" of 1000 picks"+(tp==null?", Thay faction absent":"")+"), kobold 龙之眷从 ×5");
    // 4. 设置关闭后不生成
    s.dndPersonality=false;int off=0;for(int k=0;k<60;k++){var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false));batch.Add(p);if(DndTraits.PersonalityCount(p)>0)off++;}s.dndPersonality=true;
    Check(off==0,"setting off: 60 generated pawns get no personality trait ("+off+")");
    // 5. 仁善者成为巫妖：失去仁善
    var saint=DtPawn();DtGive(saint,DndTraitDefOf.LP_Morality,1);int lc=DndTraits.LichConversions;saint.health.AddHediff(Necro.Def<HediffDef>("Lich"));
    Check(!DndTraits.Has(saint,DndTraitDefOf.LP_Morality) && DndTraits.LichConversions==lc+1 && DndTraits.Info(saint)==null,"a 仁善 pawn who becomes a lich loses 仁善 (now neutral)");
    // 6. 惧龙 / 屠龙志士不生成于龙裔；巨人杀手不生成于巨人
    var db=DtPawn("LP_ForceDragon");var fg=DtPawn("LP_FireGiant");
    Check(DndTraits.Dragonborn(db) && !DndTraits.Allowed(db,DndTraitDefOf.LP_DragonStance,-1) && !DndTraits.Allowed(db,DndTraitDefOf.LP_DragonStance,1) && !DndTraits.Allowed(db,DndTraitDefOf.LP_DragonServant,0) && !DndTraits.Allowed(fg,DndTraitDefOf.LP_GiantStance,-1) && DndTraits.Allowed(fg,DndTraitDefOf.LP_GiantStance,1) && DndTraits.Allowed(bp,DndTraitDefOf.LP_DragonStance,1) && !DndTraits.Candidates(db).Any(e=>e.def==DndTraitDefOf.LP_DragonStance),"dragonborn never rolls 惧龙 / 屠龙志士 / 龙之眷从; fire giant never rolls 巨人杀手 (but can roll 巨人仆从)");
    // 7. 世界公民屏蔽种族关系的负面心情
    var rel=DefDatabase<ThoughtDef>.AllDefsListForReading.FirstOrDefault(d=>d.workerClass==typeof(ThoughtWorker_DNDRacialRelation) && d.stages!=null && d.stages.Count>0 && d.stages[0].baseMoodEffect<0f);
    var cosmo=DtPawn();DtGive(cosmo,DndTraitDefOf.LP_RaceAttitude,1);
    Check(rel!=null && !DndTraits.FilterRacial(cosmo,rel,ThoughtState.ActiveAtStage(0)).Active && DndTraits.FilterRacial(bp,rel,ThoughtState.ActiveAtStage(0)).Active,"世界公民 hides the negative racial relation mood ("+rel?.defName+"), others keep it");
    // 8. 类型伤害合计封顶 +60%
    var dragon=DtPawn("LP_ForceDragon",AdvEnemy);DndCreatureType.Forget(dragon);
    var slayer=DtPawn();DtGive(slayer,DndTraitDefOf.LP_DragonStance,1);var both=DtPawn();DtGive(both,DndTraitDefOf.LP_DragonStance,1);HeroTraits.Give(both,HeroDefOf.LP_FavoredEnemy,1);HeroTraits.Give(both,HeroDefOf.LP_DeedDragonslayer);
    var hit=new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,slayer,null,ThingDefOf.MeleeWeapon_Knife);
    float f1=dragon==null?0f:DndTraits.OutFactor(slayer,DndTraits.Info(slayer),dragon,hit),hero=dragon==null?0f:HeroCombat.TypeBonus(both,HeroTraits.Info(both),dragon,true),f2=dragon==null?0f:DndTraits.OutFactor(both,DndTraits.Info(both),dragon,hit);
    Check(dragon!=null && DndCreatureType.Is(dragon,DndType.Dragon) && Near(f1,1.2f) && Near(hero,.55f) && Near((1f+hero)*f2,1.6f),"type damage cap: 屠龙志士 alone ×"+f1+" vs a force dragon; with 1.58.0 bonuses +"+hero+" the total is capped at +60% (×"+((1f+hero)*f2).ToString("0.###")+")");
    // 9. 战术家光环：4 格内友军 +5%，两个战术家不叠加，远处的人没有
    var t1=DtPawn(null,null,true,0,2);var t2=DtPawn(null,null,true,1,2);var ally=DtPawn(null,null,true,0,3);var far=DtPawn(null,null,true,4,-4);
    DtGive(t1,DndTraitDefOf.LP_Tactician);DtGive(t2,DndTraitDefOf.LP_Tactician);t1.drafter.Drafted=true;t2.drafter.Drafted=true;DndTraits.RefreshAura(new[]{t1,t2});
    Check(Near(DndTraits.AuraHit(ally),.05f) && DndTraits.AuraHit(far)==0f && DndTraits.Aura.Count(x=>x==ally)==1,"战术家 aura: ally within 4 cells +5% hit, two tacticians do not stack, a pawn 7 cells away gets nothing");
    t1.drafter.Drafted=false;t2.drafter.Drafted=false;DndTraits.RefreshAura(new Pawn[0]);
    // 10. 狂野魔法：12 种浪涌逐项执行，没有人死
    var caster=DtPawn(null,null,true,-2,-2);DtGive(caster,DndTraitDefOf.LP_WildMagic);var foe=DtPawn(null,AdvEnemy,true,2,-2);var mate=DtPawn(null,null,true,-3,-2);
    var fails=new List<string>();foreach(var n in DndTraits.Surges)if(DndTraits.Surge(caster,n)==null)fails.Add(n);
    caster.GetAttachment(ThingDefOf.Fire)?.Destroy();
    Check(fails.Count==0 && !caster.Dead && !foe.Dead && !mate.Dead,"狂野魔法: all 12 surges run ("+(fails.Count==0?"ok":"failed "+string.Join(",",fails))+"), nobody died");
    // 11. 无 VPE 退化：奥术天赋 → 心灵敏感度 +10%
    HeroDestiny.forceVPE=false;var ag=DtPawn();float sp=StatDefOf.PsychicSensitivity.Worker.GetValue(StatRequest.For(ag));DtGive(ag,DndTraitDefOf.LP_ArcaneGift);
    float sa=StatDefOf.PsychicSensitivity.Worker.GetValue(StatRequest.For(ag));HeroDestiny.forceVPE=oVpe;
    Check(Near(sa-sp,.1f),"no VPE: 奥术天赋 gives +10% psychic sensitivity ("+sp.ToStringPercent()+" → "+sa.ToStringPercent()+")");
    // 12. 秘法上瘾：超过 5 天没用 -6，用了 +4
    var addict=DtPawn();DtGive(addict,DndTraitDefOf.LP_ArcaneCraving);var ar=DndTraits.Record(addict);var cd=DefDatabase<ThoughtDef>.GetNamed("LP_DT_ArcaneCraving");
    ar.lastMagic=DndTraits.Now-DndTraits.CravingTicks-10;int st0=DndTraits.MoodStage(addict,cd);DndTraits.ArcaneFix(addict);int st1=DndTraits.MoodStage(addict,cd);
    Check(st0==0 && st1==-1 && DndTraits.HasMemory(addict,"LP_DT_ArcaneFix") && DndTraits.MagicConsumable(DefDatabase<ThingDef>.GetNamed("LP_PotionHealing")) && DndTraits.MagicConsumable(DefDatabase<ThingDef>.GetNamed("LP_ScrollFireball")),"秘法上瘾: craving mood after 5 days, gone after a potion / scroll (+4 memory)");
    // 13. 复仇亡魂：被敌人杀死 12 小时后复活、冷却 60 天内不再复活、尸体被毁不复活
    var rev=DtPawn(null,null,true,-1,1);HeroTraits.Give(rev,DndTraitDefOf.LP_Revenant,0);Adventurer.TryAdd(rev,1,true);var killer=DtPawn(null,AdvEnemy,true,1,1);
    int pend=comp.pending.Count;rev.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,killer));bool queued=comp.pending.Count==pend+1 && DndTraits.Record(rev)?.killerId==killer.ThingID;
    int early=comp.ProcessRevenants(DndTraits.Now);foreach(var e in comp.pending)if(e.pawn==rev)e.tick=DndTraits.Now;int rose=comp.ProcessRevenants(DndTraits.Now);
    bool alive=!rev.Dead && Items.Has(rev,DndTraitDefOf.LP_RevenantBody) && DndTraits.Record(rev).revenantCooldown>DndTraits.Now+DndTraits.RevenantCooldown-100;
    var rev2=DtPawn(null,null,true,-1,-1);HeroTraits.Give(rev2,DndTraitDefOf.LP_Revenant,0);rev2.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,killer));rev2.Corpse?.Destroy();
    foreach(var e in comp.pending)if(e.pawn==rev2)e.tick=DndTraits.Now;comp.ProcessRevenants(DndTraits.Now);
    bool burnt=rev2.Dead && !comp.pending.Any(e=>e.pawn==rev2);
    var rev3=DtPawn(null,null,true,-2,1);HeroTraits.Give(rev3,DndTraitDefOf.LP_Revenant,0);DndTraits.EnsureRecord(rev3).revenantCooldown=DndTraits.Now+1000;rev3.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,killer));
    Check(queued && early==0 && rose==1 && alive && burnt && !comp.pending.Any(e=>e.pawn==rev3),"复仇亡魂: killed by an enemy → rises after 12 hours with 亡魂之躯 and a 60-day cooldown; a destroyed corpse never rises; on cooldown no rise is queued");
    // 14. 杀死凶手 → 安息之魂 + 下一级 50% 经验
    var ah=Adventurer.Get(rev);float xp0=ah?.xp??0f,span=DndTraits.NextLevelSpan(ah);killer.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,rev));
    Check(ah!=null && DndTraits.Degree(rev,DndTraitDefOf.LP_Revenant)==1 && ah.xp-xp0>=span*.5f-.01f && !Items.Has(rev,DndTraitDefOf.LP_RevenantBody) && DndTraits.Record(rev).killerFaction==AdvEnemy,"复仇亡魂 kills the killer → 安息之魂, +"+(ah==null?0f:ah.xp-xp0)+" xp (≥ half of "+span+", plus the normal kill xp), 亡魂之躯 gone");
    // 15. 兽化：到时强制化身，持续 6 小时
    var wolf=DtPawn(null,null,true,3,0);HeroTraits.Give(wolf,DndTraitDefOf.LP_Lycanthrope,1);var wr=DndTraits.Record(wolf);float meleeBase=wolf.GetStatValue(StatDefOf.MeleeDamageFactor);wr.lycanNext=DndTraits.Now-1;DndTraits.LycanTick(wolf,DndTraits.Info(wolf),DndTraits.Now);
    var form=DndTraits.Form(wolf);int dur=form?.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear??-1;bool reg=DndTraits.Beasts.Contains(wolf);
    float melee=wolf.GetStatValue(StatDefOf.MeleeDamageFactor);
    Check(form!=null && form.def==DndTraitDefOf.LP_Beastform_Wolf && form.forced && dur==DndTraits.BeastTicks && reg && melee>=1.9f*meleeBase && wr.lycanNext>DndTraits.Now,"兽化症·狼: forced into wolf form when due, lasts 6 hours ("+dur+" ticks), melee ×"+melee.ToString("0.##")+" (×"+(melee/meleeBase).ToString("0.##")+" of base "+meleeBase.ToString("0.##")+"), next attack rescheduled");
    // 16. 兽形减伤：普通钢刀 -50%，银刀不减，法术不减
    var hunter=DtPawn(null,AdvEnemy,true,4,0);var knife=ThingDefOf.MeleeWeapon_Knife;hunter.equipment.DestroyAllEquipment();hunter.equipment.AddEquipment(DtWeapon(knife,ThingDefOf.Steel));
    var wi=DndTraits.Info(wolf);float steel=DndTraits.InFactor(wolf,wi,hunter,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,hunter,null,knife));
    hunter.equipment.DestroyAllEquipment();hunter.equipment.AddEquipment(DtWeapon(knife,ThingDefOf.Silver));float silver=DndTraits.InFactor(wolf,wi,hunter,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,hunter,null,knife));
    float spellF=DndTraits.InFactor(wolf,wi,hunter,new DamageInfo(DamageDefOf.Burn,10f,0f,-1f,hunter));
    form.End();bool ended=DndTraits.Form(wolf)==null && !DndTraits.Beasts.Contains(wolf);
    Check(Near(steel,.5f) && Near(silver,1f) && Near(spellF,1f) && ended && ItemThrowables.HolyTarget(wolf),"beast form: steel knife ×"+steel+", silver knife ×"+silver+", spells ×"+spellF+"; form ends cleanly; holy water hurts a lycanthrope");
    // 17. 驯服野性：冒险者 10 级后可主动化身、不再强制、不失控
    var bear=DtPawn(null,null,true,-3,3);HeroTraits.Give(bear,DndTraitDefOf.LP_Lycanthrope,2);var bh=Adventurer.TryAdd(bear,1,true);if(bh!=null)bh.level=10;
    bool tamed=DndTraits.CheckTamed(bear);var br=DndTraits.Record(bear);br.lycanNext=DndTraits.Now-1;DndTraits.LycanTick(bear,DndTraits.Info(bear),DndTraits.Now);bool noForce=DndTraits.Form(bear)==null;
    bool shaped=DndTraits.Shapeshift(bear,false);var bf=DndTraits.Form(bear);
    Check(tamed && br.tamed && bear.abilities.GetAbility(DndTraitDefOf.LP_DT_Beastshape)!=null && noForce && shaped && bf!=null && !bf.forced && bf.def==DndTraitDefOf.LP_Beastform_Bear,"驯服野性 at adventurer level 10: ability granted, no forced change when due, voluntary bear form is not forced");
    bf?.End();
    // 18. 兽形覆盖层贴图：3 形 × 头 / 身 × 南东北，加两个能力图标
    var miss=new List<string>();foreach(var a in new[]{"Wolf","Bear","Tiger"})foreach(var part in new[]{"Head","Body"})foreach(var dir in new[]{"south","east","north"}){var path="LP/DndTraits/Beast_"+a+"_"+part+"_"+dir;if(ContentFinder<Texture2D>.Get(path,false)==null)miss.Add(path);}
    foreach(var n in new[]{"PsychicShock","Beastshape"})if(ContentFinder<Texture2D>.Get("LP/DndTraits/"+n,false)==null)miss.Add(n);
    Check(miss.Count==0,"beast overlay textures (3 forms × head/body × 3 facings) and 2 ability icons exist"+(miss.Count==0?"":": missing "+string.Join(",",miss)));
    // 19. 远域之触：心灵冲击眩晕 + 伤害；能力已给；心灵敏感度 +40%
    var seer=DtPawn(null,null,true,0,-4);float ss0=StatDefOf.PsychicSensitivity.Worker.GetValue(StatRequest.For(seer));HeroTraits.Give(seer,DndTraitDefOf.LP_FarRealmTouched);var mark=DtPawn(null,AdvEnemy,true,2,-4);var behind=DtPawn(null,AdvEnemy,true,-2,-4);
    float hp0=mark.health.summaryHealth.SummaryHealthPercent;int hitN=DndTraits.PsychicShock(seer,HtCell(3,-4));
    Check(hitN==1 && mark.stances.stunner.Stunned && !behind.stances.stunner.Stunned && mark.health.summaryHealth.SummaryHealthPercent<hp0 && seer.abilities.GetAbility(DndTraitDefOf.LP_DT_PsychicShock)!=null && StatDefOf.PsychicSensitivity.Worker.GetValue(StatRequest.For(seer))-ss0>=.35f,"远域之触: 心灵冲击 stuns and hurts the enemy in the cone (not the one behind), ability granted, psychic sensitivity +40%");
    // 20. 红色并入名额：已有金 / 紫不再获得红；红色出现率
    var gold=DtPawn();HeroTraits.Give(gold,HeroDefOf.LP_HeroicSpark);HeroTraits.TestKind=HeroTraits.KindCurse;HeroTraits.TestSuppressed=false;Trait again,fresh;var clean=DtPawn();
    try{again=HeroTraits.OnGenerated(gold);fresh=HeroTraits.OnGenerated(clean);}finally{HeroTraits.TestKind=null;HeroTraits.TestSuppressed=oHsup;}
    var bitten=DndTraits.Infect(gold,DndTraitDefOf.LP_Lycanthrope,1);var twice=DndTraits.Infect(clean,DndTraitDefOf.LP_Lycanthrope,2);
    int cu=0,cu0=0;Rand.PushState(16204);try{for(int k=0;k<10000;k++){if(HeroTraits.RollKind(HeroTraits.Tier.Common,1f,Rand.Value,1f)==HeroTraits.KindCurse)cu++;if(HeroTraits.RollKind(HeroTraits.Tier.Common,1f,Rand.Value,0f)==HeroTraits.KindCurse)cu0++;}}finally{Rand.PopState();}
    Check(again==null && bitten!=null && twice==null && fresh!=null && DndTraits.IsCurse(fresh.def) && HeroTraits.Has(gold,HeroDefOf.LP_HeroicSpark) && cu>=75 && cu<=225 && cu0==0,"curses count as power traits: generation rolls only once (a 英雄之资 pawn rolls nothing more), a bite still curses it (several power traits allowed) but never a second curse; a clean pawn gets "+HeroTraits.Plain(fresh)+"; common-race curse rate "+cu+"/10000 (1.5%), 0 when off");
    // 21. 仁善：制伏武器打倒敌人 +2；邪恶不吃处决心情，仁善额外 -5
    var kindly=DtPawn(null,null,true,-4,0);DtGive(kindly,DndTraitDefOf.LP_Morality,1);var thug=DtPawn(null,AdvEnemy,true,-4,1);
    var cudgel=DefDatabase<ThingDef>.GetNamed("LP_SubduingCudgel");for(int k=0;k<10 && !thug.Downed;k++)thug.TakeDamage(new DamageInfo(Nonlethal.SubdueDamage,12f,0f,-1f,kindly,null,cudgel));
    var exec=DefDatabase<ThoughtDef>.GetNamedSilentFail("KnowGuestExecuted")??DefDatabase<ThoughtDef>.AllDefsListForReading.FirstOrDefault(d=>DndTraits.Atrocity(d)==2 && d.IsMemory);
    var villain=DtPawn();DtGive(villain,DndTraitDefOf.LP_Morality,-1);
    if(exec!=null){villain.needs.mood.thoughts.memories.TryGainMemory(exec);kindly.needs.mood.thoughts.memories.TryGainMemory(exec);}
    Check(thug.Downed && DndTraits.HasMemory(kindly,"LP_DT_Mercy") && exec!=null && villain.needs.mood.thoughts.memories.GetFirstMemoryOfDef(exec)==null && kindly.needs.mood.thoughts.memories.GetFirstMemoryOfDef(exec)!=null && DndTraits.HasMemory(kindly,"LP_DT_Conscience"),"仁善 downing an enemy with a subduing weapon gets 手下留情; 邪恶 ignores "+exec?.defName+", 仁善 gets it plus 良心不安 [downed "+thug.Downed+" mercy "+DndTraits.HasMemory(kindly,"LP_DT_Mercy")+" evil "+(exec!=null && villain.needs.mood.thoughts.memories.GetFirstMemoryOfDef(exec)!=null)+" good "+(exec!=null && kindly.needs.mood.thoughts.memories.GetFirstMemoryOfDef(exec)!=null)+" conscience "+DndTraits.HasMemory(kindly,"LP_DT_Conscience")+"]");
   }finally{
    s.dndPersonality=oP;s.dndCurses=oC;s.dndPersonalityRate=oR;s.adventurer=oAdv;DndTraits.TestSuppressed=oSup;HeroTraits.TestSuppressed=oHsup;Adventurer.TestSuppressed=oAsup;HeroDestiny.forceVPE=oVpe;DndTraits.TestRoll=null;HeroTraits.TestKind=null;
    comp?.pending.RemoveAll(e=>dtMade.Contains(e.pawn));
    foreach(var p in batch)if(!p.Destroyed)p.Destroy();
    foreach(var p in dtMade){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();else if(!p.Destroyed)p.Destroy();}dtMade.Clear();
    DndTraits.Reset();comp?.MarkDirty();
   }
  }
  void PrepareDndTraitsSave(){
   bool oSup=DndTraits.TestSuppressed;DndTraits.TestSuppressed=false;
   try{
    var a=DtPawn(null,null,true,-1,3);HeroTraits.Give(a,DndTraitDefOf.LP_Revenant,0);var ra=DndTraits.Record(a);ra.killerId="LP_TestKiller";ra.killerName="测试凶手";ra.revenantCooldown=dtCooldown=DndTraits.Now+12345;
    var b=DtPawn(null,null,true,1,3);HeroTraits.Give(b,DndTraitDefOf.LP_Lycanthrope,3);var rb=DndTraits.Record(b);rb.tamed=true;rb.lycanNext=dtLycanNext=DndTraits.Now+777;DndTraits.SyncAbilities(b);
    DtGive(b,DndTraitDefOf.LP_Order,-1);
    dtIds=a.ThingID+","+b.ThingID;dtMade.Clear();
   }finally{DndTraits.TestSuppressed=oSup;}
  }
  void CheckDndTraitsAfterLoad(){
   var ids=(dtIds??"").Split(',');var all=map.mapPawns.AllPawnsSpawned.ToList();
   var a=all.FirstOrDefault(p=>p.ThingID==ids[0]);var b=ids.Length>1?all.FirstOrDefault(p=>p.ThingID==ids[1]):null;
   var ra=DndTraits.Record(a);var rb=DndTraits.Record(b);
   // 22. 存读档：凶手、冷却、驯服、下次发作、能力、性格特性
   Check(ra!=null && ra.killerId=="LP_TestKiller" && ra.killerName=="测试凶手" && ra.revenantCooldown==dtCooldown && rb!=null && rb.tamed && rb.lycanNext==dtLycanNext && DndTraits.Degree(b,DndTraitDefOf.LP_Lycanthrope)==3 && DndTraits.Degree(b,DndTraitDefOf.LP_Order)==-1 && b.abilities.GetAbility(DndTraitDefOf.LP_DT_Beastshape)!=null && DndTraits.Info(a)?.revenant==0,"save/load keeps the revenant's killer and cooldown, the lycanthrope's tamed state, next attack, ability and personality traits");
   a?.Destroy();b?.Destroy();
  }
 }
}
