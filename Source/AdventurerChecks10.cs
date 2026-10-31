using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 1.81.0 热修3（职业与属性平衡，照 D&D）：Test.ps1 -Only Adventurer 的一部分，由 CheckAdventurer 在同一个 try 里调用。
 public partial class LichChecks {
  void CheckAdventurerBalance(){
   var s=LichMod.Settings;bool oldGap=s.adventurerLevelGap;
   var fighter=ClassSystem.Fighter;var monk=ClassSystem.Monk;var wiz=DefDatabase<LP_ClassDef>.GetNamedSilentFail("LP_Class_Wizard");
   var a=AdvPawn();var h=Adventurer.Get(a);
   try{
    s.adventurerLevelGap=true;if(h.classes==null)h.classes=new ClassData();h.classes.levels.Clear();
    // 1. 属性线性：力量 18 近战 ×1.4；感知 20 心灵敏感度 ×1.5
    var melee=Hediff_Adventurer.Table.First(e=>e.stat=="MeleeDamageFactor");var psy=Hediff_Adventurer.Table.First(e=>e.stat=="PsychicSensitivity");
    h.scores=new[]{18,10,10,10,20,10};h.level=1;h.Dirty();
    Check(melee.kind==Hediff_Adventurer.FxKind.Linear && psy.kind==Hediff_Adventurer.FxKind.Linear && Near(h.FxValue(melee,1f),1.4f) && Near(h.FxValue(psy,1f),1.5f) && Near(h.FxValue(psy,.5f),1.25f),"[hotfix3] linear: Str 18 melee ×"+h.FxValue(melee,1f).ToString("0.##")+" (1.4), Wis 20 psychic sensitivity ×"+h.FxValue(psy,1f).ToString("0.##")+" (1.5), half effect strength ×1.25");
    // 2. 体质对部位生命：5 级体质 14 无职业 = 1.2 × 1.3
    h.scores=new[]{10,10,14,10,10,10};h.level=5;h.Dirty();float p14=h.PartFactor(1f),c14=h.ConPart(1f);h.scores[Adventurer.Con]=20;float c20=h.ConPart(1f);h.scores[Adventurer.Con]=6;float c6=h.ConPart(1f);
    Check(Near(p14,1.16f*1.3f) && Near(c14,1.3f) && Near(c20,1.75f) && Near(c6,1f),"[hotfix3] Con part health linear +15%/modifier: level 5 Con 14 ×"+p14.ToString("0.###")+" (1.16×1.3, 1.93.0 unspent 4%), Con 20 ×1.75, Con 6 ×1");
    // 4. 物理职业伤害成长：战士 20 近战 ×1.6（Σ 职业等级 × 3%）、远程同样；施法职业 0；不乘属性效果强度
    h.scores=new[]{10,10,10,10,10,10};h.level=20;h.classes.levels[fighter]=20;h.Dirty();Martial.Refresh(a);
    float m1=h.BuildStage(1f).statFactors?.GetStatFactorFromList(StatDefOf.MeleeDamageFactor)??1f,m0=h.BuildStage(0f).statFactors?.GetStatFactorFromList(StatDefOf.MeleeDamageFactor)??1f;
    Check(Near(ClassSystem.MartialDamage(h),.6f) && Near(m1,1.6f) && Near(m0,1.6f) && Near(Martial.RangedDamage(a),1.6f) && Near(ClassSystem.DefaultDamagePer("LP_Class_Monk"),.02f) && ClassSystem.DefaultDamagePer("LP_Class_Wizard")==0f,"[hotfix3] fighter 20: melee ×"+m1.ToString("0.###")+" (also at 0% effect strength ×"+m0.ToString("0.###")+"), ranged ×"+Martial.RangedDamage(a).ToString("0.###")+"; monk +2%/level, wizard 0");
    // 5. 近战穿甲：战士 10 +0.15、武僧 10 +0.1
    h.classes.levels.Clear();h.classes.levels[fighter]=10;h.Dirty();Martial.Refresh(a);
    var d1=new DamageInfo(DamageDefOf.Cut,10f,.1f,-1f,a,null,null);Martial.Penetrate(ref d1,a);
    h.classes.levels.Clear();h.classes.levels[monk]=10;h.Dirty();Martial.Refresh(a);
    var d2=new DamageInfo(DamageDefOf.Cut,10f,.1f,-1f,a,null,null);Martial.Penetrate(ref d2,a);
    var pm=AccessTools.Method(typeof(Pawn),nameof(Pawn.PreApplyDamage));var pi=pm==null?null:Harmony.GetPatchInfo(pm);
    Check(Near(d1.ArmorPenetrationInt,.25f) && Near(d2.ArmorPenetrationInt,.2f) && pi!=null && pi.Postfixes.Any(x=>x.PatchMethod.DeclaringType==typeof(ClassDamageMods)),"[hotfix3] melee armor penetration: fighter 10 +"+(d1.ArmorPenetrationInt-.1f).ToString("0.###")+" (0.15), monk 10 +"+(d2.ArmorPenetrationInt-.1f).ToString("0.###")+" (0.1), hooked in ClassDamageMods");
    // 7. 等级差防御：低 10 级 ×0.8，低 30 级 ×0.5（封顶）；关掉设置 ×1
    var t=AdvPawn();var ht=Adventurer.Get(t);h.level=5;ht.level=15;
    float g=Adventurer.LevelGapFactor(a,t),rev=Adventurer.LevelGapFactor(t,a);s.adventurerLevelGap=false;float off=Adventurer.LevelGapFactor(a,t);s.adventurerLevelGap=true;
    var sm=AccessTools.Method(typeof(ShotReport),nameof(ShotReport.HitReportFor));var si=sm==null?null:Harmony.GetPatchInfo(sm);
    Check(Near(Adventurer.LevelGapFactor(10),.8f) && Near(Adventurer.LevelGapFactor(30),.5f) && Near(Adventurer.LevelGapFactor(0),1f) && Near(g,.8f) && Near(rev,1f) && Near(off,1f) && Adventurer.LevelGapFactor(a,null)==1f && si!=null && si.Postfixes.Any(x=>x.PatchMethod.DeclaringType==typeof(AdventurerShotLevelGap)),"[hotfix3] level gap defense: gap 10 ×0.8, gap 30 ×0.5, level 5 vs 15 ×"+g.ToString("0.##")+", higher attacker ×1, setting off ×1, ranged hit report patched");
    // 8. 职业强度：智力 20、1 级（熟练 +2）= +0.25
    h.classes.levels.Clear();h.level=1;h.scores=new[]{10,10,10,20,10,10};h.Dirty();Martial.Refresh(a);
    float cp=Adventurer.ClassPowerBonus(a,Adventurer.Int);
    Check(Near(cp,.25f),"[hotfix3] class power: Int 20 → +"+cp.ToString("0.###")+" (modifier 5 × 5%)");
    // 9. 施法职业等级成长：法师 10 → 奥术强度 +0.45（1.97.0 每级 5%，上限 +95%）；默认比例（其他强度）仍 +0.18、上限 +40%
    if(wiz!=null){h.level=10;h.classes.levels[wiz]=10;h.Dirty();}
    var arc=ClassLevelInfo.Part(DefDatabase<StatDef>.GetNamed("LP_ArcanePower"));
    float cb=wiz==null || arc==null?-1f:arc.LevelBonus(a),cd=wiz==null?-1f:ClassSystem.ClassBonus(a,wiz.mainSkill);
    Check(wiz!=null && arc!=null && Near(cb,.45f) && Near(cd,.18f) && Near(arc.LevelBonusFor(25),.95f) && Near(arc.LevelBonusFor(21),.95f) && Near(ClassSystem.MartialBonusFor(25),.4f),"[1.97.0] caster level growth: wizard 10 → arcane power +"+cb.ToString("0.###")+" (+5% per level above 1, capped +95%); default rate still +"+cd.ToString("0.###")+" (+2%, capped +40%)");
   }finally{
    s.adventurerLevelGap=oldGap;h.classes?.levels.Clear();h.Dirty();Martial.Refresh(a);
   }
   CheckAdventurerFix4();
  }
  // 1.81.0 热修4：疼痛缩放、法师 / 术士数值、法力上限、戏法、三个防御法术。
  static CompProperties_ClassEffect Fx4(string ability)=>DefDatabase<AbilityDef>.GetNamedSilentFail(ability)?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
  void CheckAdventurerFix4(){
   var s=LichMod.Settings;bool oldPain=s.adventurerPainScale;
   var a=AdvPawn();var h=Adventurer.Get(a);
   try{
    // 疼痛 ÷ 部位生命倍率
    s.adventurerPainScale=true;if(h.classes==null)h.classes=new ClassData();h.classes.levels.Clear();h.scores=new[]{10,10,20,10,10,10};h.level=20;h.Dirty();
    var torso=a.RaceProps.body.corePart;var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,a,torso);cut.Severity=10f;a.health.AddHediff(cut,torso);
    float f=Adventurer.PartHealthFactor(a),on=cut.PainOffset;s.adventurerPainScale=false;float off=cut.PainOffset;s.adventurerPainScale=true;
    a.health.RemoveHediff(cut);
    Check(f>1.9f && off>0f && Near(on*f,off) && Near(Adventurer.PainDivisor(null),1f),"[hotfix4] wound pain ÷ part-health factor: factor ×"+f.ToString("0.###")+", cut pain "+on.ToString("0.####")+" vs "+off.ToString("0.####")+" with the setting off (×2 part health → half pain)");
    // 法师数值
    var fb=Fx4("LP_Fireball");var lb=Fx4("LP_LightningBolt");var mm=Fx4("LP_MagicMissile");var mmDef=DefDatabase<AbilityDef>.GetNamedSilentFail("LP_MagicMissile");
    Check(fb!=null && Near(fb.radius,4f) && Near(fb.power,20f) && lb!=null && Near(lb.power,30f) && mm!=null && mm.cost==0 && mmDef.cooldownTicksRange.min==300,"[hotfix4] fireball radius 4 / 20×power, lightning bolt 30×power, magic missile is a cantrip (0 mana, 300 ticks)");
    // 法力上限：法师 20 = 22；气仍是 12
    var wiz=DefDatabase<LP_ClassDef>.GetNamedSilentFail("LP_Class_Wizard");h.classes.levels[wiz]=20;h.Dirty();
    Check(ClassSystem.MaxResource(h,wiz)==22 && ClassSystem.MaxMana(1)==3 && ClassSystem.MaxResource(20)==12,"[hotfix4] mana cap 2 + class level: wizard 20 = "+ClassSystem.MaxResource(h,wiz)+"; fury/ki/grace still 12 at 20");
    // 三个防御法术在法师表里，等级 1 / 3 / 5
    int Lv(string n)=>wiz.abilities.FirstOrDefault(x=>x.ability?.defName==n)?.level??-1;
    Check(Lv("LP_ShieldSpell")==1 && Lv("LP_MistyStep")==3 && Lv("LP_MirrorImage")==5 && Fx4("LP_MistyStep")?.effect=="Teleport" && Near(Fx4("LP_MistyStep").maxRange,10f),"[hotfix4] shield 1, misty step 3 (10-cell teleport), mirror image 5 in the wizard table");
    // 镜影挡一下、护盾吸收
    var foe=AdvPawn(null,AdvEnemy);
    Items.Give(a,"LP_MirrorImage",2500,3f);var d1=new DamageInfo(DamageDefOf.Bullet,12f,0f,-1f,foe);bool m1=ClassCasters.Ward(a,ref d1);
    var mi=a.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("LP_MirrorImage"));float left=mi?.Severity??0f;if(mi!=null)a.health.RemoveHediff(mi);
    Items.Give(a,"LP_ShieldSpell",600,20f);var d2=new DamageInfo(DamageDefOf.Bullet,15f,0f,-1f,foe);bool s1=ClassCasters.Ward(a,ref d2);var d3=new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,foe);bool s2=ClassCasters.Ward(a,ref d3);
    bool gone=a.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("LP_ShieldSpell"))==null;
    Check(m1 && Near(left,2f) && s1 && !s2 && Near(d3.Amount,5f) && gone,"[hotfix4] mirror image eats one hit (3 → "+left+"); shield 20 absorbs 15, then 5 of the next 10 and breaks");
    // 术士
    Check(Near(Fx4("LP_IceStorm")?.power??0f,14f) && Near(Fx4("LP_BurningHands")?.power??0f,18f),"[hotfix4] sorcerer: ice storm 14×power, burning hands 18×power");
   }finally{
    s.adventurerPainScale=oldPain;h.classes?.levels.Clear();h.Dirty();
   }
   CheckAdventurerFix5();
  }
  // 1.81.0 热修5：天赋倍率乘在整个线性效果上，种族亲和只放大正的调整值。
  void CheckAdventurerFix5(){
   var s=LichMod.Settings;bool oAff=s.adventurerAffinity;
   try{
    s.adventurerAffinity=true;
    var melee=Hediff_Adventurer.Table.First(e=>e.stat=="MeleeDamageFactor");var psy=Hediff_Adventurer.Table.First(e=>e.stat=="PsychicSensitivity");
    Hediff_Adventurer Make(XenotypeDef x,TraitDef t,int degree,int[] scores){
     var p=AdvPawn(x);foreach(var tr in p.story.traits.allTraits.Where(tr=>HeroTraits.IsSlot(tr.def) || HeroTraits.IsDeed(tr.def) || Talents.Is(tr.def)).ToList())p.story.traits.RemoveTrait(tr);HeroTraits.Invalidate(p);
     if(t==Talents.Gifted)p.story.traits.GainTrait(new Trait(t));else if(t!=null)HeroTraits.Give(p,t,degree);
     var h=Adventurer.Get(p);if(h.classes==null)h.classes=new ClassData();h.classes.levels.Clear();h.level=1;h.scores=scores;h.legendSpent=new int[Legend.Count];h.legendPoints=0;h.Dirty();return h;
    }
    var plain=Make(null,null,0,new[]{18,10,10,10,10,10});
    var pStr=Make(null,HeroDefOf.LP_Paragon,1,new[]{18,10,10,10,10,10});
    var orcStr=Make(DefDatabase<XenotypeDef>.GetNamed("LP_HalfOrc"),HeroDefOf.LP_Paragon,1,new[]{18,10,10,10,10,10});
    var gStr=Make(null,Talents.Gifted,0,new[]{18,10,10,10,10,10});
    float v0=plain.FxValue(melee,1f),v1=pStr.FxValue(melee,1f),v2=orcStr.FxValue(melee,1f),v3=gStr.FxValue(melee,1f);
    Check(Near(v0,1.4f) && Near(v1,3.5f) && Near(v2,4f) && Near(v3,2.1f) && Near(pStr.AMod(Adventurer.Str),4f) && Near(orcStr.AMod(Adventurer.Str),6f) && Near(Affinity.TalentMult(orcStr,Adventurer.Str),2.5f) && Near(Affinity.ScoreMult(orcStr,Adventurer.Str),2.5f),
     "[hotfix5] Str 18 melee: plain ×"+v0.ToString("0.###")+" (1.4), paragon ×"+v1.ToString("0.###")+" (3.5), half-orc strong + paragon ×"+v2.ToString("0.###")+" (1.6×2.5 = 4.0), gifted ×"+v3.ToString("0.###")+" (2.1); table effects still take the larger (×2.5)");
    string sheet=plain.EffectSheet(Adventurer.Str),src=Affinity.SourceTag(orcStr,Adventurer.Str);
    Check(sheet.Contains("近战伤害") && sheet.Contains("×1.4") && sheet.Contains("负重") && Affinity.SourceTag(plain,Adventurer.Str)=="" && src.Contains("种族擅长：调整值 ×1.5") && src.Contains("极致：整体 ×2.5"),"[hotfix5] tab effect table: Str 18 '"+sheet.Replace("\n"," | ")+"'; half-orc paragon source '"+src+"'");
    var pWis=Make(null,HeroDefOf.LP_Paragon,5,new[]{10,10,10,10,20,10});
    var pCon=Make(null,HeroDefOf.LP_Paragon,3,new[]{10,10,20,10,10,10});
    var pInt=Make(null,HeroDefOf.LP_Paragon,4,new[]{10,10,10,20,10,10});
    float ps=pWis.FxValue(psy,1f),psHalf=pWis.FxValue(psy,.5f),pf=pCon.PartFactor(1f),cp=Adventurer.ClassPowerBonus(pInt.pawn,Adventurer.Int);
    Check(Near(ps,3.75f) && Near(psHalf,2.375f) && Near(pf,4.375f) && Near(cp,2.125f),
     "[hotfix5] paragon: Wis 20 psychic sensitivity ×"+ps.ToString("0.###")+" (1.5×2.5 = 3.75, half effect strength ×2.375), Con 20 part health ×"+pf.ToString("0.###")+" (1.75×2.5 = 4.375, level 1), Int 20 class power +"+cp.ToString("0.###")+" (1.25×2.5 − 1 = 2.125)");
    var lowG=Make(null,Talents.Gifted,0,new[]{6,10,6,6,10,10});
    float lm=lowG.FxValue(melee,1f),lc=lowG.ConPart(1f),lp=Adventurer.ClassPowerBonus(lowG.pawn,Adventurer.Int);
    Check(Near(lm,.8f) && Near(lc,1f) && Near(lp,-.1f),"[hotfix5] talent never lifts a below-×1 linear effect: gifted Str 6 melee ×"+lm.ToString("0.##")+" (0.8), Con 6 part ×"+lc.ToString("0.##")+" (1), Int 6 class power "+lp.ToString("0.###")+" (−0.1)");
   }finally{s.adventurerAffinity=oAff;}
  }
 }
}
