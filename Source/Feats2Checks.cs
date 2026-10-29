using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.58.1：Test.ps1 -Only Feats2（专长第二批、种族专长、起源专长，20 条）。-VPE -Only Feats2 另跑桥里的魔法学徒 / 仪式施法者。
 public partial class LichChecks {
  static string f2Saved,f2Id;
  Hediff_Adventurer F2Adv(Pawn p){var h=Adventurer.Get(p)??Adventurer.TryAdd(p,1,true);foreach(var f in h.feats.ToList())h.RemoveFeat(f);h.originFeat=null;h.originFeatGiven=false;return h;}
  static LP_FeatDef FD(string n)=>DefDatabase<LP_FeatDef>.GetNamed(n);
  static int Burns(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count(i=>i.def.defName=="Burn");

  void CheckFeats2(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oOrigin=s.originFeats;bool oSup=Adventurer.TestSuppressed,oF2=Feats2.TestSuppressed;
   Adventurer.TestSuppressed=false;s.adventurer=true;s.originFeats=true;
   try{
    // 1. 定义：27 个新专长（通用 18 个 def = 13 种，元素使 4 支、坚毅心智 3 支），种族 9；图标都在；元素使 / 坚毅心智同组。1.67.0 再加 8 个（斩 / 刺 / 碎同组）= 35
    var news=DefDatabase<LP_FeatDef>.AllDefsListForReading.Where(Feats2.Is).ToList();
    Check(news.Count==35 && news.Count(f=>f.exclusiveGroup=="WeaponType")==3 && news.Count(f=>f.Racial)==9 && news.All(f=>f.Icon!=BaseContent.BadTex) && news.Count(f=>f.exclusiveGroup=="ElementalAdept")==4 && news.Count(f=>f.exclusiveGroup=="Resilient")==3 && Feats2.OriginPool.All(n=>FD(n)!=null) && new Settings().originFeats,"35 feats from 1.58.1 / 1.67.0 (26 general defs, 9 racial) with icons; adept / resilient variants are exclusive groups; origin pool and setting default on (got "+news.Count+", bad icons "+news.Count(f=>f.Icon==BaseContent.BadTex)+")");
    // 2. 种族专长只对对应种族可选（分页也只显示给对应种族）；巫妖专长看巫妖基因
    var human=HtPawn();var hh=F2Adv(human);hh.pendingASI=3;var dwarf=HtPawn("LP_HillDwarf");var hd=F2Adv(dwarf);hd.pendingASI=1;
    var dwf=FD("LP_Feat_DwarvenFortitude");
    Check(hh.CanTakeFeat(dwf)!=null && !Feats2.Visible(dwf,human) && hd.CanTakeFeat(dwf)==null && Feats2.Visible(dwf,dwarf) && hh.CanTakeFeat(FD("LP_Feat_PhylacteryOath"))!=null && Feats2.Visible(FD("LP_Feat_TavernBrawler"),human),"racial feats: a human cannot take or see 矮人坚毅, a hill dwarf can; 命匣之誓 needs a lich; general feats stay visible");
    // 3. 同组互斥：元素使只能选一种
    hh.AddFeat(FD("LP_Feat_ElementalAdept_Fire"));
    Check(hh.CanTakeFeat(FD("LP_Feat_ElementalAdept_Cold"))?.Contains("同类")==true,"元素使: after 火 the other elements are refused (one element only)");
    // 4. 元素使·火：火巨人身上也烧得伤（基因的火焰系数 0 被无视），火焰伤害 +10%
    var giant=HtPawn("LP_FireGiant",AdvEnemy,true,0,0);var mage=HtPawn(null,null,true,1,0);var hm=F2Adv(mage);
    giant.TakeDamage(new DamageInfo(DamageDefOf.Flame,10f,0f,-1f,mage));int b0=Burns(giant);
    hm.AddFeat(FD("LP_Feat_ElementalAdept_Fire"));giant.TakeDamage(new DamageInfo(DamageDefOf.Flame,10f,0f,-1f,mage));int b1=Burns(giant);
    float bonus=Feats2.OutFactor(mage,HeroTraits.Info(mage),giant,new DamageInfo(DamageDefOf.Flame,10f,0f,-1f,mage));
    Check(b0==0 && b1>0 && Near(bonus,1.25f),"元素使·火: flame does nothing to a fire giant ("+b0+" burns) until the caster is an adept ("+b1+" burns), and fire damage ×"+bonus);
    // 5. 观察者：看得见心灵隐形的人
    var watcher=HtPawn(null,null,true,2,0);var hw=F2Adv(watcher);hw.scores[Adventurer.Int]=14;hw.Dirty();var hider=HtPawn(null,AdvEnemy,true,3,0);hider.health.AddHediff(MindFlayerPowers.Unseen);
    bool hidden=hider.IsPsychologicallyInvisible();float rs0=hw.CurStage.statFactors?.GetStatFactorFromList(StatDefOf.ResearchSpeed)??1f;hw.AddFeat(Feats2.Observant);bool seen;int sd=Fiends.Push(watcher);try{seen=!hider.IsPsychologicallyInvisible();}finally{Fiends.Pop(sd);}
    hw.featScore.Clear();hw.Dirty(); // 1.67.0 去掉附带的智力/感知 +1，只量专长本身
    float rs1=hw.CurStage.statFactors?.GetStatFactorFromList(StatDefOf.ResearchSpeed)??1f;
    Check(hidden && seen && Fiends.Sees(watcher) && rs0>0f && Near(rs1/rs0,1.25f),"观察者: an invisible enemy is invisible to others but seen by the observant colonist; research ×1.25 (hidden "+hidden+", seen "+seen+", research "+rs0.ToString("0.###")+" → "+rs1.ToString("0.###")+")");
    // 6. 技能专家：3 个技能兴趣各 +1 级
    var sk=HtPawn();var hsk=F2Adv(sk);foreach(var r in sk.skills.skills){r.passion=Passion.None;if(!r.TotallyDisabled)r.Level=5;}int before=sk.skills.skills.Count(r=>r.passion!=Passion.None);hsk.AddFeat(Feats2.Skilled);
    Check(sk.skills.skills.Count(r=>r.passion==Passion.Minor)==3 && before==0 && sk.skills.skills.Count(r=>r.passion==Passion.Minor && r.Level==8)==3,"技能专家: three skills go from no passion to minor and +3 levels (5 → 8)");
    // 7. 工匠：买武器 / 衣甲 -10%，卖东西和买食物不变
    var cr=HtPawn();F2Adv(cr).AddFeat(Feats2.Crafter);
    Check(Near(Feats2CrafterPrice.Factor(cr,ThingDefOf.MeleeWeapon_Knife,TradeAction.PlayerBuys),.8f) && Near(Feats2CrafterPrice.Factor(cr,ThingDefOf.Apparel_Parka,TradeAction.PlayerBuys),.8f) && Near(Feats2CrafterPrice.Factor(cr,ThingDefOf.MeleeWeapon_Knife,TradeAction.PlayerSells),1f) && Near(Feats2CrafterPrice.Factor(cr,ThingDefOf.MealSimple,TradeAction.PlayerBuys),1f) && Near(Feats2CrafterPrice.Factor(human,ThingDefOf.MeleeWeapon_Knife,TradeAction.PlayerBuys),1f),"工匠: buying weapons and apparel ×0.8; selling, food and other negotiators unchanged");
    // 8. 毒师：涂毒消耗 1 份草药，近战命中附加蛛后之毒，次数 -1
    var po=HtPawn(null,null,true,0,2);var hpo=F2Adv(po);hpo.AddFeat(Feats2.Poisoner);var herb=ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);herb.stackCount=2;po.inventory.innerContainer.TryAdd(herb);
    bool coated=Feats2.Coat(po);var victim=HtPawn(null,AdvEnemy,true,1,2);victim.TakeDamage(new DamageInfo(DamageDefOf.Cut,1f,0f,-1f,po,null,ThingDefOf.MeleeWeapon_Knife));
    var blade=po.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_FeatPoisonedBlade"));
    Check(coated && po.inventory.innerContainer.TotalStackCountOfDef(ThingDefOf.MedicineHerbal)==1 && Items.Has(victim,"LP_DrowVenom") && blade!=null && Near(blade.Severity,19f) && po.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_FeatCoatBlade"))!=null,"毒师: coating uses 1 herbal medicine, the next melee hit poisons (蛛后之毒), 19 charges left");
    // 9. 仪式施法者：冷却最久的能力冷却完毕，下一次施法不进冷却
    var rc=HtPawn();var hrc=F2Adv(rc);hrc.AddFeat(FD("LP_Feat_Actor"));hrc.AddFeat(Feats2.RitualCaster);
    var dis=rc.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("LP_FeatDisguise"));dis.StartCooldown(60000);bool cd=dis.OnCooldown;
    Feats2.Ritual(rc);bool reset=!dis.OnCooldown && Feats2.Primed(rc);dis.StartCooldown(60000);bool free=dis.CooldownTicksRemaining<=0;
    Check(cd && reset && free,"仪式施法者: the ritual resets the longest cooldown and the next cast does not go on cooldown (cd "+cd+", reset "+reset+", free "+free+", left "+dis.CooldownTicksRemaining+")");
    // 10. 酒馆斗殴者：徒手 ×1.5，命中推开 1 格
    var br=HtPawn(null,null,true,-2,-2);F2Adv(br).AddFeat(Feats2.Brawler);var foe=HtPawn(null,AdvEnemy,true,-1,-2);var at=foe.Position;
    float fist=Feats2.OutFactor(br,HeroTraits.Info(br),foe,new DamageInfo(DamageDefOf.Blunt,5f,0f,-1f,br,null,br.def));
    Feats2.TestRoll=0f;try{foe.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1f,0f,-1f,br,null,br.def));}finally{Feats2.TestRoll=null;}
    Check(Near(fist,2.5f) && foe.Position!=at && foe.Position.DistanceTo(br.Position)>at.DistanceTo(br.Position),"酒馆斗殴者: unarmed damage ×"+fist+" and a punch shoves the enemy one cell ("+at+" → "+foe.Position+")");
    // 11. 起源专长按背景表：侍僧 → 魔法学徒，罪犯 → 警觉，隐士 → 治疗者，没有背景 → 技能专家，5% → 幸运
    Pawn Bg(TraitDef t){var p=HtPawn();if(t!=null)p.story.traits.GainTrait(new Trait(t));return p;}
    Check(Feats2.PickOrigin(Bg(BackgroundTraits.Acolyte),.5f)==Feats2.MagicInitiate && Feats2.PickOrigin(Bg(BackgroundTraits.Criminal),.5f)==Feats.Alert && Feats2.PickOrigin(Bg(BackgroundTraits.Hermit),.5f)==Feats.Healer && Feats2.PickOrigin(Bg(BackgroundTraits.Noble),.5f)==Feats2.Skilled && Feats2.PickOrigin(Bg(null),.5f)==Feats2.Skilled && Feats2.PickOrigin(Bg(BackgroundTraits.Acolyte),.01f)==Feats.Lucky,"origin feat table: acolyte → 魔法学徒, criminal → 警觉, hermit → 治疗者, noble / no background → 技能专家, the 5% roll → 幸运");
    // 12. 发放：只发一次；已有同名的改发技能专家；起源专长不占属性提升
    var og=Bg(BackgroundTraits.Criminal);var hog=F2Adv(og);hog.AddFeat(Feats.Alert);int asi=hog.pendingASI;
    var got=Feats2.GiveOrigin(hog,.5f,true);var again=Feats2.GiveOrigin(hog,.5f,true);
    Check(got==Feats2.Skilled && again==null && hog.originFeat==Feats2.Skilled && hog.originFeatGiven && hog.pendingASI==asi && hog.feats.Count==2,"origin feat: a criminal who already has 警觉 gets 技能专家 instead, free (no ASI used), and only once");
    // 13. 老存档补发：没有标记的人补一次，再读档不补；生成时（开关开着）也会发
    var oldp=HtPawn();var hold=F2Adv(oldp);int n1=Feats2.BackfillOrigins(new[]{oldp},true),n2=Feats2.BackfillOrigins(new[]{oldp},true);
    Feats2.TestSuppressed=false;Pawn fresh;try{fresh=HtPawnRaw();}finally{Feats2.TestSuppressed=oF2;}var hf=Adventurer.Get(fresh);
    Check(n1==1 && n2==0 && hold.originFeat!=null && hf!=null && hf.originFeatGiven && hf.originFeat!=null && hf.HasFeat(hf.originFeat),"old saves get one origin feat on load (then never again); new pawns get one at generation ("+hf?.originFeat?.label+")");
    // 14. 魔法学徒（没装 VPE 时）：获得魔能飞弹，失去专长收回
    var mi=HtPawn();var hmi=F2Adv(mi);hmi.AddFeat(Feats2.MagicInitiate);bool vpe=Feats2.InitiateHook!=null;bool missile=mi.abilities.GetAbility(Feats2.MissileDef)!=null;hmi.RemoveFeat(Feats2.MagicInitiate);
    Check(vpe || missile && mi.abilities.GetAbility(Feats2.MissileDef)==null,"魔法学徒 without VPE: gains 魔能飞弹, losing the feat removes it"+(vpe?"（VPE loaded: checked in -VPE -Only Feats2）":""));
    // 15. 厨师：做饭 20% 多一份点心
    var chef=HtPawn();var hc=F2Adv(chef);chef.skills.GetSkill(SkillDefOf.Cooking).Level=8;hc.AddFeat(Feats2.Chef);
    var meal=DefDatabase<RecipeDef>.GetNamed("CookMealSimple");List<Thing> made;Feats2.TestRoll=0f;try{made=GenRecipe.MakeRecipeProducts(meal,chef,new List<Thing>{ThingMaker.MakeThing(ThingDefOf.RawPotatoes)},null,null).ToList();}finally{Feats2.TestRoll=null;}
    Check(Feats2ChefSnack.Meal(meal) && made.Any(t=>t.def.defName=="LP_FeatSnack") && made.Any(t=>t.def==ThingDefOf.MealSimple),"厨师: cooking a meal adds a 点心 on the 20% roll");
    foreach(var t in made)t.Destroy();
    // 16. 矮人坚毅：每天第一次喝酒治 15 点伤
    hd.AddFeat(dwf);var cut=HediffMaker.MakeHediff(HediffDefOf.Cut,dwarf,dwarf.RaceProps.body.corePart);cut.Severity=20f;dwarf.health.AddHediff(cut);
    var beer=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Beer"));beer.Ingested(dwarf,0f);float sev=dwarf.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity);
    Check(Near(sev,0f) && dwarf.GetStatValue(StatDefOf.ToxicResistance)>=.5f,"矮人坚毅: the first beer of the day heals 40 points (20 → "+sev+"), toxic resistance +50%");
    // 17. 好运连连：冷却 2 天，3 格内一个友军得到“借来的好运”
    var hl=HtPawn("LP_HalflingLightfoot",null,true,-4,0);F2Adv(hl).AddFeat(FD("LP_Feat_BountifulLuck"));var buddy=HtPawn(null,null,true,-3,0);
    var cdh=OncePerCooldown.StartCooldown(hl,DefDatabase<GeneDef>.GetNamed("LP_HalflingLuck"));int left=cdh.TryGetComp<HediffComp_Disappears>()?.ticksToDisappear??0;
    Check(Math.Abs(left-GenDate.TicksPerDay)<=1 && Items.Has(buddy,"LP_BorrowedLuck"),"好运连连: halfling luck cooldown 1 day ("+left+" ticks) and an adjacent ally gets 借来的好运");
    // 18. 炼狱之怒：挨打后下一击多 8 点火焰伤害
    var tf=HtPawn("LP_Tiefling",null,true,-4,3);F2Adv(tf).AddFeat(FD("LP_Feat_InfernalWrath"));var en=HtPawn(null,AdvEnemy,true,-3,3);
    tf.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1f,0f,-1f,en,null,en.def));bool armed=Feats2.WrathArmed(tf);int eb=Burns(en);
    en.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1f,0f,-1f,tf,null,tf.def));
    Check(armed && !Feats2.WrathArmed(tf) && Burns(en)>eb,"炼狱之怒: being hit arms it, the next attack adds a 15-point fire burn");
    // 19. 精灵精准 ×1.08 起；嗜血再生近战击杀治 30 点
    var elf=HtPawn("LP_Elf",null,true,4,4);F2Adv(elf).AddFeat(FD("LP_Feat_ElvenAccuracy"));float ef=Feats2.ShotFactor(elf,HeroTraits.Info(elf));
    var troll=HtPawn("LP_Troll");F2Adv(troll).AddFeat(FD("LP_Feat_BloodRegen"));var cut2=HediffMaker.MakeHediff(HediffDefOf.Cut,troll,troll.RaceProps.body.corePart);cut2.Severity=40f;troll.health.AddHediff(cut2);
    Feats2.Killed(troll,HeroTraits.Info(troll),true);float tsev=troll.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity);
    Check(ef>=1.15f-.001f && ef<=1.15f*1.1f+.001f && Near(tsev,0f) && Feats2.BloodRegenHeal==60,"精灵精准: aim ×"+ef.ToString("0.###")+"; 嗜血再生: a melee kill heals up to 60 (40 → "+tsev+")");
   }finally{
    s.adventurer=oOn;s.originFeats=oOrigin;Feats2.TestRoll=null;Feats2.TestSuppressed=oF2;
    foreach(var p in htMade){if(p.Dead && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();else if(!p.Destroyed)p.Destroy();}htMade.Clear();
    Adventurer.TestSuppressed=oSup;
   }
  }
  // 20. 存读档：起源专长、标记、涂毒次数
  void PrepareFeats2Save(){
   Adventurer.TestSuppressed=false;
   try{
    var p=HtPawn(null,null,true,0,-3);var h=F2Adv(p);h.AddFeat(Feats2.Poisoner);var g=Feats2.GiveOrigin(h,.01f,true);var b=Items.Give(p,"LP_FeatPoisonedBlade");b.Severity=7f;
    f2Id=p.ThingID;f2Saved=string.Join(",",h.feats.Select(f=>f.defName))+"|"+h.originFeat?.defName+"|"+h.originFeatGiven+"|7";htMade.Clear();
   }finally{Adventurer.TestSuppressed=true;}
  }
  void CheckFeats2AfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==f2Id);var h=Adventurer.Get(p);
   var b=p?.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_FeatPoisonedBlade"));
   string now=h==null?"missing":string.Join(",",h.feats.Select(f=>f.defName))+"|"+h.originFeat?.defName+"|"+h.originFeatGiven+"|"+(b==null?0:Mathf.RoundToInt(b.Severity));
   Check(now==f2Saved && HeroTraits.Info(p)?.poisoner==true,"feats, origin feat and its flag, poisoned-blade charges survive save/load ("+f2Saved+")");
   p?.Destroy();
  }
 }
}
