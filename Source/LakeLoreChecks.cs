using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.133.0 魔力湖泊第三、四批：Test.ps1 -Only LakeLore。
 // 通用秘传（定义、门槛、吸收成功率与失败、共用秘传槽、强度、每个主动施放一次、被动、许愿术三个安全愿望与大愿望代价、迷宫术存读档）
 // 和湖印（定义、门槛与成功率、槽位与倍率、湖息消耗与回复、每个湖印法术施放一次、被动状态、澄湖结晶、货单）。
 public partial class LichChecks {
  static string llMazeId,llCasterId;
  static AbilityDef LS(string k)=>DefDatabase<AbilityDef>.GetNamed("LP_LS_"+k);
  Pawn LlCaster(int level,int intScore,int dx,int dz){var p=MlClassPawn(MlClass("Wizard"),level,Mathf.Min(level,20),dx,dz);var h=Adventurer.Get(p);for(int i=0;i<6;i++)h.scores[i]=intScore;h.Dirty();p.skills.GetSkill(SkillDefOf.Intellectual).Level=12;return p;}

  void CheckLakeLore(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool on=s.adventurer;float mult=s.lakeSlotMult;s.adventurer=true;s.lakeSlotMult=1f;
   Rand.PushState(113301);
   try{CheckLakeLoreInner();}
   finally{Rand.PopState();Adventurer.TestSuppressed=sup;s.adventurer=on;s.lakeSlotMult=mult;StockGenerator_Arcana.TestGoodwill=null;StockGenerator_LoreWish.TestRoll=null;Legendary.TestD20=null;LakeTime.End();MlClean();}
  }
  void CheckLakeLoreInner(){
   string[] bad={"d20","D20","掷骰","检定","DC","豁免 +"};
   // 1. 定义：通用秘传 12 法术 + 2 武技 + 4 被动；湖印 14 法术 + 10 被动；每个一枚结晶、图标、价格照文档；澄湖结晶 300 银；没有骰子字样。
   {var lore=LakeArcana.All.Where(a=>LakeArcana.Ext(a).Universal).ToList();var seals=LakeSeals.All;
    int sp=lore.Count(a=>LakeArcana.Ext(a).kind=="spell"),ma=lore.Count(a=>LakeArcana.Ext(a).Martial),pa=lore.Count(a=>LakeArcana.Ext(a).Passive);
    bool icons=lore.Concat(seals).All(a=>a.uiIcon!=null && a.uiIcon!=BaseContent.BadTex);
    bool crystals=lore.All(a=>LakeArcana.CrystalFor(a) is ThingDef c && Mathf.Abs(c.BaseMarketValue-LakeArcana.Ext(a).price)<1f) && seals.All(a=>LakeSeals.CrystalFor(a) is ThingDef c && Mathf.Abs(c.BaseMarketValue-LakeSeals.Ext(a).price)<1f);
    var texts=lore.Concat(seals).Select(a=>a.description).Concat(LakeArcana.Crystals.Concat(LakeSeals.Crystals).Select(c=>c.description));
    bool words=!texts.Any(t=>bad.Any(b=>t.Contains(b)));var clear=DefDatabase<ThingDef>.GetNamedSilentFail("LP_ClearLakeCrystal");
    Check(sp==12 && ma==2 && pa==4 && seals.Count(a=>!LakeSeals.Passive(a))==14 && seals.Count(LakeSeals.Passive)==10 && icons && crystals && words && clear!=null && Mathf.Abs(clear.BaseMarketValue-300f)<1f && LakeArcana.Ext(MlAb("Wish")).price==50000,
     "[1.133.0] defs: 12 universal spells + 2 techniques + 4 passives, 14 seal spells + 10 seal passives, crystals priced per the design, clear-lake crystal 300, icons, no dice words ("+sp+"/"+ma+"/"+pa+" "+icons+" "+crystals+" "+words+")");}
   // 2. 通用秘传门槛：冒险者 11 级、施法属性 15；武技另需格斗或射击 10；被动看最高属性。
   {var p=LlCaster(10,15,0,0);var h=Adventurer.Get(p);string a=LakeArcana.CannotLearn(p,MlAb("ChainLightning"));
    h.level=11;h.scores[Adventurer.Int]=14;h.Dirty();string b=LakeArcana.CannotLearn(p,MlAb("ChainLightning"));
    h.scores[Adventurer.Int]=15;h.Dirty();string c=LakeArcana.CannotLearn(p,MlAb("ChainLightning"));
    h.scores[Adventurer.Str]=16;h.Dirty();p.skills.GetSkill(SkillDefOf.Melee).Level=5;p.skills.GetSkill(SkillDefOf.Shooting).Level=5;string d=LakeArcana.CannotLearn(p,MlAb("BoneBreak"));
    p.skills.GetSkill(SkillDefOf.Melee).Level=10;string e=LakeArcana.CannotLearn(p,MlAb("BoneBreak"));
    Check(a!=null && a.Contains("11") && b!=null && b.Contains("15") && c==null && d!=null && d.Contains("格斗") && e==null,"[1.133.0] universal gates: adventurer 11, casting score 15, techniques also melee/shooting 10 ("+a+" / "+b+" / "+d+")");}
   // 3. 吸收成功率：80% + 属性超过门槛每点 5%（6 环智力 17 → 90%；9 环魅力 18 → 50%；智力 20 → 100% 封顶）；失败秘传反噬、7 天再试、结晶不算学会；成功占秘传槽。
   {var p=LlCaster(17,17,2,0);var h=Adventurer.Get(p);float c6=LakeLore.AbsorbChance(p,MlAb("ChainLightning"));h.scores[Adventurer.Cha]=18;h.Dirty();float c9=LakeLore.AbsorbChance(p,MlAb("PowerWordKill"));
    h.scores[Adventurer.Int]=20;h.Dirty();float cMax=LakeLore.AbsorbChance(p,MlAb("ChainLightning"));
    h.scores[Adventurer.Int]=17;h.Dirty();bool fail=LakeLore.TryAbsorb(p,MlAb("ChainLightning"),.999f,false);bool backlash=Items.Has(p,"LP_LO_Backlash");string locked=LakeArcana.CannotLearn(p,MlAb("ChainLightning"));
    LakeSeals.Data(h).loreFail=0;bool ok=LakeLore.TryAbsorb(p,MlAb("ChainLightning"),0f,false);
    Check(Near(c6,.9f) && Near(c9,.5f) && Near(cMax,1f) && !fail && backlash && locked!=null && locked.Contains("再试") && ok && LakeArcana.Knows(p,MlAb("ChainLightning")) && LakeArcana.Used(h)==1,
     "[1.133.0] absorb chance 80% + 5%/point over the gate ("+c6.ToStringPercent()+", 9th "+c9.ToStringPercent()+", cap "+cMax.ToStringPercent()+"); failure = backlash and 7-day lock, success uses an arcanum slot ("+fail+" "+backlash+" "+locked+" "+ok+" "+LakeArcana.Used(h)+" "+LakeArcana.CannotLearn(p,MlAb("Heal"))+")");}
   // 4. 秘传槽和职业水晶共用：冒险者 11 级只有 1 个，学了通用秘传就学不了职业水晶。
   {var p=LlCaster(13,18,4,0);var h=Adventurer.Get(p);h.level=11;h.Dirty();ClassSystem.SetLevel(h,MlClass("Wizard"),11);
    bool a=LakeArcana.Learn(p,MlAb("Heal"));string b=LakeArcana.CannotLearn(p,LakeArcana.OfClass(MlClass("Wizard")).First());
    Check(a && b!=null && b.Contains("秘传槽已满"),"[1.133.0] universal secrets share the 4 arcanum slots with class crystals ("+b+")");}
   // 5. 强度：属性强度 1 + 调整值 × 5%（最多 +50%）和自己职业强度取较高；法术乘心灵敏感度，武技不乘。
   {var p=LlCaster(17,20,6,0);var h=Adventurer.Get(p);float attr=LakeLore.AttrStrength(p,LakeArcana.Ext(MlAb("ChainLightning")));float cls=LakeLore.ClassStrength(p);
    float psy=Mathf.Max(ClassSystem.PsychicMin,p.GetStatValue(StatDefOf.PsychicSensitivity));float spell=LakeLore.Strength(p,MlAb("ChainLightning")),tech=LakeLore.Strength(p,MlAb("BoneBreak"));
    h.scores[Adventurer.Int]=30;h.Dirty();float capped=LakeLore.AttrStrength(p,LakeArcana.Ext(MlAb("ChainLightning")));
    Check(Near(attr,1.25f) && capped>=attr-.001f && capped<=1.5f+.001f && Near(spell,Mathf.Max(attr,cls)*psy) && Near(tech,Mathf.Max(LakeLore.AttrStrength(p,LakeArcana.Ext(MlAb("BoneBreak"))),cls)),"[1.133.0] strength = max(1 + 5%/modifier capped +50%, own class power) × psychic for spells ("+attr.ToString("0.00")+" / class "+cls.ToString("0.00")+" → "+spell.ToString("0.00")+" psy "+psy.ToString("0.00")+" tech "+tech.ToString("0.00")+" cap "+capped.ToString("0.00")+")");}
   // 6. 每个通用秘传主动施放一次不报错，并有效果。
   {var p=LlCaster(17,18,8,0);var gun=ThingMaker.MakeThing(ThingDef.Named("Gun_AssaultRifle"));p.equipment.AddEquipment((ThingWithComps)gun);
    var fails=new List<string>();int n=0;
    foreach(var a in LakeArcana.All.Where(x=>LakeArcana.Ext(x).Universal && !LakeArcana.Ext(x).Passive)){
     var e=a.comps.OfType<CompProperties_ClassEffect>().First();
     var foe=MlPawn(MlEnemy,10,0);var ally=MlPawn(Faction.OfPlayer,9,1);ally.TakeDamage(new DamageInfo(DamageDefOf.Cut,8f,0f,-1f,null,ally.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso)));
     LocalTargetInfo t=e.effect=="LO_Heal" || e.effect=="LO_Regenerate" || e.effect=="LO_MindBlank"?(LocalTargetInfo)ally:e.effect=="LO_BladeBarrier" || e.effect=="LO_ReverseGravity" || e.effect=="LO_Sunburst"?(LocalTargetInfo)foe.Position:e.effect=="LO_SunArrow"?(LocalTargetInfo)foe.Position:e.effect=="LO_TimeStop" || e.effect=="LO_Wish" || e.effect=="LO_BoneBreak"?(LocalTargetInfo)p:(LocalTargetInfo)foe;
     try{string why=LakeLore.Invalid(p,e,t);if(why!=null){fails.Add(a.label+"："+why);continue;}if(!ArcanaEffects.Apply(p,a,e,t))fails.Add(a.label+" 未处理");else n++;}
     catch(Exception ex){fails.Add(a.label+" 异常 "+ex.GetType().Name+" "+ex.Message);}
     LakeTime.End();map.GetComponent<LakeMaze>().ReturnAll();
    }
    Check(fails.Count==0 && n==14,"[1.133.0] all 14 universal actives cast once without errors ("+n+")"+(fails.Count>0?": "+string.Join("；",fails):""));}
   // 7. 几个关键效果：连锁闪电跳 4 个敌人；真言·杀 40% 以下当场死亡；死亡之指杀死的人形变成亡仆；时间停止击晕全图、施法者伤人提前结束。
   {var p=LlCaster(17,18,0,4);var foes=Enumerable.Range(0,5).Select(i=>MlPawn(MlEnemy,6+i,4)).ToList();int jumps=LakeLore.ChainLightning(p,foes[0],5f,6f);
    var weak=MlPawn(MlEnemy,6,6);foreach(var part in weak.health.hediffSet.GetNotMissingParts().Where(x=>x.depth==BodyPartDepth.Outside && (x.def.tags==null || !x.def.tags.Any(tg=>tg.vital))).ToList()){if(ArcanaEffects.Health(weak)<=.35f)break;var inj=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,weak,part);inj.Severity=Mathf.Max(1f,weak.health.hediffSet.GetPartHealth(part)*.8f);weak.health.AddHediff(inj,part);}
    float hp=ArcanaEffects.Health(weak);bool killed=weak.Dead?false:hp<=.4f?LakeLore.PowerWordKill(p,weak,40f) && weak.Dead:!LakeLore.PowerWordKill(p,weak,1f) && !weak.Dead;
    var victim=MlPawn(MlEnemy,8,6);victim.Kill(new DamageInfo(DamageDefOf.Cut,30f,99f,-1f,p));bool died=victim.Dead,hasCorpse=victim.Corpse!=null && victim.Corpse.Spawned;bool raised=victim.Dead && LakeLore.RaiseThrall(p,victim) && !victim.Dead && victim.Faction==p.Faction && Items.Has(victim,"LP_LO_Thrall");
    int frozen=LakeTime.Start(p,360);bool stunned=foes[1].stances.stunner.Stunned;foes[2].TakeDamage(new DamageInfo(DamageDefOf.Cut,1f,0f,-1f,p));bool ended=!LakeTime.Active && !foes[1].stances.stunner.Stunned;
    Check(jumps==4 && killed && raised && frozen>=5 && stunned && ended,"[1.133.0] chain lightning hits 4, power word kill ≤40% ("+hp.ToStringPercent()+"), finger of death raises a thrall, time stop freezes the map and ends when the caster strikes ("+jumps+" "+killed+" "+raised+" "+frozen+" "+stunned+" "+ended+" died "+died+" corpse "+hasCorpse+")");}
   // 8. 被动：秘法壁垒豁免 +3（= 成功率 +15%）、法术伤害 ×0.6；传奇抵抗每天 3 次把失败改成功；第二心脏本应死亡时活下来（10 天一次）。
   {var p=LlCaster(17,18,0,8);var h=Adventurer.Get(p);int before=Legendary.SaveBonus(p,Adventurer.Wis,null);LakeArcana.Learn(p,MlAb("ArcaneBulwark"));int after=Legendary.SaveBonus(p,Adventurer.Wis,null);
    float hp0=p.health.summaryHealth.SummaryHealthPercent;var foe=MlPawn(MlEnemy,3,8);
    h.level=20;h.Dirty();LakeArcana.Learn(p,MlAb("LegendaryResistance"));Legendary.TestD20=1;int saved=0;for(int i=0;i<4;i++)if(Legendary.Save(p,Adventurer.Wis,30))saved++;Legendary.TestD20=null;
    var q=LlCaster(20,18,2,8);LakeArcana.Learn(q,MlAb("SecondHeart"));q.TakeDamage(new DamageInfo(DamageDefOf.Cut,99f,999f,-1f,null,q.health.hediffSet.GetBrain()));bool alive=!q.Dead;
    Check(after-before==3 && saved==3 && alive,"[1.133.0] passives: arcane bulwark saves +3 (+15%), legendary resistance flips 3 failures a day ("+saved+"/4), second heart survives a lethal hit ("+alive+")");}
   // 9. 许愿术三个安全愿望：施放没学过的 8 环以下秘传（反转重力）、全体痊愈、变出物品（3000 银以内）；大愿望 33% 失去许愿术并虚弱。
   {var p=LlCaster(17,18,4,8);var foe=MlPawn(MlEnemy,8,8);var hurt=MlPawn(Faction.OfPlayer,5,9);hurt.TakeDamage(new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,null,hurt.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso)));
    bool cast=LakeLore.WishCast(p,MlAb("ReverseGravity"),foe.Position) && Items.Has(foe,"LP_LO_Levitated");bool noNine=!LakeLore.WishSpells.Any(a=>LakeArcana.Ext(a).ring>8);
    int healed=LakeLore.WishHealAll(Find.CurrentMap);bool clean=!hurt.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any();
    int count=LakeLore.WishItems(p,"Plasteel");float value=count*ThingDefOf.Plasteel.BaseMarketValue;
    LakeArcana.Learn(p,MlAb("Wish"));bool keep=!LakeLore.GreatWishCost(p,.9f) && LakeArcana.Knows(p,MlAb("Wish"));bool lose=LakeLore.GreatWishCost(p,.1f) && !LakeArcana.Knows(p,MlAb("Wish")) && Items.Has(p,"LP_LO_Weakened");
    Check(cast && noNine && healed>=1 && clean && count>0 && value<=3000f+1f && keep && lose,"[1.133.0] wish: three safe wishes (unlearned reverse gravity, heal everyone, "+count+" plasteel ≤3000), great wish 33% loses the spell ("+cast+" "+clean+" "+keep+" "+lose+")");}
   // 10. 湖印门槛与成功率：二阶要冒险者 5 级、智识 6；三阶智识 10 → 90%；巫妖 100%；失败湖音回响；动物不能学、心灵聋只能学被动。
   {var p=LlCaster(3,10,0,10);p.skills.GetSkill(SkillDefOf.Intellectual).Level=10;string a=LakeSeals.CannotLearn(p,LS("FalseLife"));var h=Adventurer.Get(p);h.level=5;h.Dirty();p.skills.GetSkill(SkillDefOf.Intellectual).Level=5;string b=LakeSeals.CannotLearn(p,LS("FalseLife"));
    p.skills.GetSkill(SkillDefOf.Intellectual).Level=10;h.level=10;h.Dirty();float c3=LakeSeals.AbsorbChance(p,LS("Counterspell")),c2=LakeSeals.AbsorbChance(p,LS("FalseLife")),c1=LakeSeals.AbsorbChance(p,LS("Mending"));
    bool fail=!LakeSeals.Absorb(p,LS("Counterspell"),.99f,false) && Items.Has(p,"LP_LS_Echo") && !LakeSeals.Knows(h,LS("Counterspell"));
    var animal=PawnGenerator.GeneratePawn(PawnKindDefOf.Thrumbo);string an=LakeSeals.CannotLearn(animal,LS("Mending"));
    Check(a!=null && a.Contains("5") && b!=null && b.Contains("智识") && Near(c3,.9f) && Near(c2,1f) && Near(c1,1f) && fail && an!=null,"[1.133.0] seal gates (tier 2: adventurer 5, intellectual 6), chance tier 3 = 70% + 2%×intellectual ("+c3.ToStringPercent()+"), failure echoes, animals can't learn");}
   // 11. 湖印槽位：冒险者 1 级法术 1 / 被动 1；7 级法术 2；倍率 ×2 翻倍；满了学不了；澄湖结晶抹掉后空出来。
   {var p=LlCaster(1,10,2,10);var h=Adventurer.Get(p);p.skills.GetSkill(SkillDefOf.Intellectual).Level=10;
    bool a=LakeSeals.Absorb(p,LS("Mending"),0f,false);string full=LakeSeals.CannotLearn(p,LS("Longstrider"));int s1=LakeSeals.SpellSlots(h);
    h.level=7;h.Dirty();int s7=LakeSeals.SpellSlots(h),p7=LakeSeals.PassiveSlots(h);LichMod.Settings.lakeSlotMult=2f;int s7x=LakeSeals.SpellSlots(h);LichMod.Settings.lakeSlotMult=.5f;int s7h=LakeSeals.SpellSlots(h);LichMod.Settings.lakeSlotMult=1f;
    h.level=1;h.Dirty();bool forgot=LakeSeals.Forget(p,LS("Mending")) && p.abilities.GetAbility(LS("Mending"))==null && LakeSeals.CannotLearn(p,LS("Longstrider"))==null;
    h.level=24;h.Dirty();int s24=LakeSeals.SpellSlots(h),p24=LakeSeals.PassiveSlots(h);
    Check(a && full!=null && full.Contains("已满") && s1==1 && s7==2 && p7==1 && s7x==4 && s7h==1 && forgot && s24==4 && p24==3,"[1.133.0] seal slots: spells 1 + level/7 (max 4), passives 1 + level/12 (max 3), ×2 / ×0.5 setting, full blocks, clear-lake frees ("+s1+","+s7+","+s7x+","+s7h+","+s24+"/"+p24+")");}
   // 12. 湖息：上限 3，雷鸣波扣 2；不够时不能施放；24 小时回 1；湖心上限 4、回复 ×1.5。
   {var p=LlCaster(5,10,4,10);var h=Adventurer.Get(p);p.skills.GetSkill(SkillDefOf.Intellectual).Level=10;LakeSeals.Learn(p,LS("Thunderwave"));var x=LakeSeals.Data(h);
    float b0=LakeSeals.Breath(h);bool paid=LakeSeals.Pay(p,LS("Thunderwave"));float b1=x.breath;string blocked=LakeSeals.CannotCast(p,LS("Thunderwave"));
    x.breathTick=Find.TickManager.TicksGame-60000;float b2=LakeSeals.Breath(h);
    h.level=10;h.Dirty();LakeSeals.Learn(p,LS("LakeHeart"));LakeSeals.Sync(h);float max=LakeSeals.BreathMax(h);int per=LakeSeals.Period(h);
    Check(Near(b0,3f) && paid && Near(b1,1f) && blocked!=null && blocked.Contains("湖息") && Near(b2,2f) && Near(max,4f) && per==40000,"[1.133.0] lake breath: max 3, thunderwave costs 2, blocked when short, +1 per 24h, lake heart max 4 / ×1.5 regen ("+b0+"→"+b1+"→"+b2+", max "+max+")");}
   // 13. 每个湖印法术施放一次不报错（湖息足够），被动学会后挂上状态。
   {var p=LlCaster(12,14,6,10);var h=Adventurer.Get(p);p.skills.GetSkill(SkillDefOf.Intellectual).Level=12;var fails=new List<string>();int n=0;var x=LakeSeals.Data(h);
    var door=(Building_Door)ThingMaker.MakeThing(ThingDefOf.Door,ThingDefOf.WoodLog);door.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(door,MlCell(6,12),map);mlMade.Add(door);
    var sword=ThingMaker.MakeThing(ThingDef.Named("MeleeWeapon_LongSword"),ThingDefOf.Steel);sword.HitPoints=sword.MaxHitPoints/2;GenPlace.TryPlaceThing(sword,MlCell(7,11),map,ThingPlaceMode.Near);mlMade.Add(sword);
    foreach(var a in LakeSeals.All.Where(d=>!LakeSeals.Passive(d))){
     x.breath=9f;var e=a.comps.OfType<CompProperties_ClassEffect>().First();var foe=MlPawn(MlEnemy,10,11);var ally=MlPawn(Faction.OfPlayer,7,10);
     LocalTargetInfo t=e.effect=="LS_Mending"?(LocalTargetInfo)sword:e.effect=="LS_ArcaneLock"?(LocalTargetInfo)door:e.effect=="LS_Counterspell"?(LocalTargetInfo)foe:e.effect=="LS_Alarm" || e.effect=="LS_Shatter" || e.effect=="LS_Thunderwave"?(LocalTargetInfo)foe.Position:a.targetRequired?(LocalTargetInfo)ally:(LocalTargetInfo)p;
     try{string why=LakeSeals.Invalid(p,e,t);if(why!=null){fails.Add(a.label+"："+why);continue;}if(!ArcanaEffects.Apply(p,a,e,t))fails.Add(a.label+" 未处理");else n++;}catch(Exception ex){fails.Add(a.label+" 异常 "+ex.Message);}
    }
    foreach(var a in LakeSeals.All.Where(LakeSeals.Passive))if(!x.seals.Contains(a))x.seals.Add(a);LakeSeals.Sync(h);
    bool hediffs=LakeSeals.All.Where(LakeSeals.Passive).All(a=>Items.Has(p,LakeSeals.PassiveHediff(a)));
    Check(fails.Count==0 && n==14 && hediffs && sword.HitPoints>sword.MaxHitPoints/2 && LakeLocks.Locked(door),"[1.133.0] all 14 seal spells cast once without errors ("+n+"), passive seals give their hediffs, mending repairs, arcane lock locks"+(fails.Count>0?": "+string.Join("；",fails):""));}
   // 14. 货单：好感 0 的湖印不重复、不含三阶；盟友时出三阶；通用秘传生成器只出通用秘传（不含许愿术）；许愿术每个存档最多一次。
   {var sg=new StockGenerator_LakeSeal{countRange=new IntRange(8,8)};StockGenerator_Arcana.TestGoodwill=0;var low=sg.GenerateThings(map.Tile).ToList();
    bool distinct=low.Select(t=>t.def).Distinct().Count()==low.Count,noT3=low.All(t=>LakeSeals.Ext(LakeSeals.AbilityOf(t.def)).tier<3);
    StockGenerator_Arcana.TestGoodwill=100;var hiSeals=new StockGenerator_LakeSeal{countRange=new IntRange(24,24)}.GenerateThings(map.Tile).ToList();bool t3=hiSeals.Any(t=>LakeSeals.Ext(LakeSeals.AbilityOf(t.def)).tier==3);
    var ug=new StockGenerator_Arcana{countRange=new IntRange(6,6),rings=new IntRange(6,9),minGoodwill=75,universal=true};var ul=ug.GenerateThings(map.Tile).ToList();
    bool uni=ul.Count>0 && ul.All(t=>LakeArcana.Ext(LakeArcana.AbilityOf(t.def)).Universal && LakeArcana.AbilityOf(t.def).defName!=LakeLore.WishName);
    var gc=GameComponent_Arcana.Instance;bool seen=gc.wishSeen;gc.wishSeen=false;StockGenerator_LoreWish.TestRoll=0f;var w1=new StockGenerator_LoreWish().GenerateThings(map.Tile).ToList();var w2=new StockGenerator_LoreWish().GenerateThings(map.Tile).ToList();gc.wishSeen=seen;
    Check(low.Count==8 && distinct && noT3 && t3 && uni && w1.Count==1 && w2.Count==0,"[1.133.0] stock: seal crystals drawn without repeats, tier 3 only for allies, universal generator never sells wish, wish crystal at most once per save ("+low.Count+" "+distinct+" "+noT3+" "+t3+" "+uni+" "+w1.Count+"/"+w2.Count+")");}
   // 15. 心灵聋：能学湖印被动，不能学湖印法术；冒险者页签行和设置能画出数据（不画 GUI，只调数据部分）。
   {var p=LlCaster(5,10,8,10);p.skills.GetSkill(SkillDefOf.Intellectual).Level=10;var deaf=DefDatabase<TraitDef>.GetNamedSilentFail("PsychicSensitivity");
    if(deaf!=null){foreach(var tr in p.story.traits.allTraits.Where(t=>t.def==deaf).ToList())p.story.traits.RemoveTrait(tr);p.story.traits.GainTrait(new Trait(deaf,-2));}
    bool isDeaf=p.GetStatValue(StatDefOf.PsychicSensitivity)<=0f;string sp=LakeSeals.CannotLearn(p,LS("Mending")),pa=LakeSeals.CannotLearn(p,LS("LakeBody"));
    Check(!isDeaf || sp!=null && sp.Contains("心灵聋") && pa==null,"[1.133.0] psychically deaf pawns can learn seal passives but not seal spells (deaf "+isDeaf+": "+sp+")");}
  }

  // ---------- 存读档：迷宫术里的目标仍在 ----------
  void PrepareLakeLoreSave(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;
   try{
    var caster=ClsPawn(17,10,10,10);GenSpawn.Spawn(caster,MlCell(-6,-6),map);
    var foe=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,MlEnemy,forceGenerateNewPawn:true,canGeneratePawnRelations:false));GenSpawn.Spawn(foe,MlCell(-4,-6),map);
    LakeMaze.Banish(caster,foe,99999999);llMazeId=foe.ThingID;llCasterId=caster.ThingID;
    var p=LlCaster(5,10,-8,-6);p.skills.GetSkill(SkillDefOf.Intellectual).Level=10;LakeSeals.Learn(p,LS("Mending"));LakeSeals.Data(Adventurer.Get(p)).breath=1.5f;
    mlMade.Remove(p);
   }finally{Adventurer.TestSuppressed=sup;}
  }
  void CheckLakeLoreAfterLoad(){
   var comp=map.GetComponent<LakeMaze>();var e=comp?.list.FirstOrDefault(x=>x.pawn?.ThingID==llMazeId);
   bool held=e!=null && !e.pawn.Destroyed && !e.pawn.Spawned && e.caster?.ThingID==llCasterId;
   comp?.ReturnAll();var back=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==llMazeId);
   var sealed1=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>Adventurer.Get(x)?.classes?.lake?.seals.Contains(LS("Mending"))==true);
   Check(held && back!=null && sealed1!=null && Near(Adventurer.Get(sealed1).classes.lake.breath,1.5f) && LakeLore.Live,"[1.133.0] save/reload: the maze keeps its target off-map and returns it; seals and lake breath persist ("+held+" "+(back!=null)+" "+(sealed1!=null)+")");
  }
 }
}
