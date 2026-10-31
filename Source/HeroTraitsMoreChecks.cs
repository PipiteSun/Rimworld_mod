using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.79.0：Test.ps1 -Only MoreTraits（新增蓝 / 紫 / 红强力特性，12 项）。
 public partial class LichChecks {
  readonly List<Pawn> mtMade=new List<Pawn>();
  Pawn MtPawn(Faction f=null,bool spawn=false,int dx=0,int dz=0){var p=HtPawn(null,f,spawn,dx,dz);mtMade.Add(p);return p;}
  static TraitDef MtDef(string n)=>DefDatabase<TraitDef>.GetNamedSilentFail(n);
  static int MtInjuries(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count();

  void CheckMoreTraits(){
   var s=LichMod.Settings;bool oOn=s.adventurer,oCo=s.adventurerIsekaiCoexist,oHt=s.heroTraits,oCu=s.dndCurses;
   bool oSup=Adventurer.TestSuppressed,oHsup=HeroTraits.TestSuppressed;
   Adventurer.TestSuppressed=false;HeroTraits.TestSuppressed=false;s.adventurer=true;s.adventurerIsekaiCoexist=false;s.heroTraits=true;s.dndCurses=true;
   var enemy=Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f=>!f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer));
   try{
    // 1. 定义：11 个进紫色池（蓝 6、紫 5），4 个进诅咒池；commonality 0，名字颜色和档位一致
    var pu=MoreTraits.Purple.ToList();var cu=MoreTraits.Curses.ToList();
    bool defs=pu.Count==11 && cu.Count==4 && pu.All(d=>HeroTraits.IsPurple(d) && HeroTraits.IsSlot(d)) && cu.All(d=>DndTraits.IsCurse(d) && HeroTraits.IsSlot(d) && d.exclusionTags.Contains("LP_Curse"))
     && pu.Concat(cu).All(d=>Near(d.GetGenderSpecificCommonality(Gender.Male),0f) && !d.ConfigErrors().Any() && d.degreeDatas[0].label.StartsWith("<color="+TraitRarity.Hex(d,0)+">"));
    int blue=pu.Count(d=>TraitRarity.Tier(d,0)==2),epic=pu.Count(d=>TraitRarity.Tier(d,0)==3);
    Check(defs && blue==6 && epic==5 && cu.All(d=>TraitRarity.Mark(d,0)=="Curse" && TraitRarity.Hex(d,0)==TraitRarity.CurseHex),"15 new power traits: 6 blue + 5 purple in the purple pool, 4 red curses (LP_Curse), commonality 0, label colors follow the tier table");
    // 2. 随机池：人类的紫色候选含全部 11 个，诅咒候选含 4 个；种子 3000 次诅咒抽到 7 种
    var h=MtPawn();var pc=HeroTraits.Choices(h,HeroTraits.KindPurple).Select(c=>c.def).ToList();var cc=HeroTraits.Choices(h,HeroTraits.KindCurse).Select(c=>c.def).ToList();
    var seen=new HashSet<TraitDef>();Rand.PushState(17701);try{for(int k=0;k<3000;k++){var t=HeroTraits.Pick(h,HeroTraits.KindCurse);if(t!=null)seen.Add(t.def);}}finally{Rand.PopState();}
    Check(pu.All(pc.Contains) && cu.All(cc.Contains) && seen.Count==7,"generation pools: purple choices include all 11 new traits, curse choices the 4 new curses; 3000 seeded curse picks hit "+seen.Count+" kinds");
    // 3. 冲突：诅咒之间互斥（新诅咒顶掉旧诅咒），魔裔之血 × 法术抗体
    var x=MtPawn();HeroTraits.Give(x,MoreTraitDefOf.LP_Vampirism);HeroTraits.Give(x,MoreTraitDefOf.LP_Doomed);bool swap=!HeroTraits.Has(x,MoreTraitDefOf.LP_Vampirism) && HeroTraits.Has(x,MoreTraitDefOf.LP_Doomed);
    HeroTraits.Give(x,DndTraitDefOf.LP_Lycanthrope,1);bool swap2=HeroTraits.Has(x,DndTraitDefOf.LP_Lycanthrope) && !HeroTraits.Has(x,MoreTraitDefOf.LP_Doomed);
    var w=MtPawn();HeroTraits.Give(w,HeroDefOf.LP_SpellWarded);bool noBlood=!HeroTraits.Choices(w,HeroTraits.KindPurple).Any(c=>c.def==MoreTraitDefOf.LP_Spellblood);
    Check(swap && swap2 && noBlood,"conflicts: curses replace each other (吸血鬼之咒 → 厄运缠身 → 兽化症), 魔裔之血 never offered to a 法术抗体 pawn");
    // 4. 属性（同一个人前后对比）：疾风之足移速、万夫莫敌受伤倍率、空心之人社交、远瞳瞄准
    var st=MtPawn();float mv=st.GetStatValue(StatDefOf.MoveSpeed,true,-1),inc=st.GetStatValue(StatDefOf.IncomingDamageFactor,true,-1),soc=st.GetStatValue(StatDefOf.SocialImpact,true,-1),aim=st.GetStatValue(StatDefOf.AimingDelayFactor,true,-1);
    HeroTraits.Give(st,MoreTraitDefOf.LP_Fleetfoot);HeroTraits.Give(st,MoreTraitDefOf.LP_Juggernaut);HeroTraits.Give(st,MoreTraitDefOf.LP_EagleEyed);
    float mv2=st.GetStatValue(StatDefOf.MoveSpeed,true,-1),inc2=st.GetStatValue(StatDefOf.IncomingDamageFactor,true,-1),aim2=st.GetStatValue(StatDefOf.AimingDelayFactor,true,-1);
    var ho=MtPawn();float soc0=ho.GetStatValue(StatDefOf.SocialImpact,true,-1);HeroTraits.Give(ho,MoreTraitDefOf.LP_Hollow);float soc2=ho.GetStatValue(StatDefOf.SocialImpact,true,-1);
    Check(mv2>mv+.3f && Near(inc2,inc*.85f) && Near(aim2,aim*.85f) && soc2<soc0*.6f,"stats: 疾风之足 move "+mv.ToString("0.00")+"→"+mv2.ToString("0.00")+", 万夫莫敌 incoming ×"+(inc2/inc).ToString("0.00")+", 远瞳 aim ×"+(aim2/aim).ToString("0.00")+", 空心之人 social "+soc0.ToString("0.00")+"→"+soc2.ToString("0.00"));
    // 5. 加点：银舌 + 灵光两组上限各算各的；魔裔之血心灵 ×2；巧匠之手工作 ×1.5
    var c=MtPawn();var hc=HtAdv(c);HeroTraits.Give(c,HeroDefOf.LP_Insight);HeroTraits.Give(c,MoreTraitDefOf.LP_Silvertongue);HeroTraits.Give(c,MoreTraitDefOf.LP_Spellblood);hc.scores[3]=14;hc.scores[4]=12;
    int cha=HeroTraits.CapBonus(hc,5),intl=HeroTraits.CapBonus(hc,3),wis=HeroTraits.CapBonus(hc,4);float psy=HeroTraits.LegendMult(c,Legend.Psychic,1f);
    var ar=MtPawn();HeroTraits.Give(ar,MoreTraitDefOf.LP_Artisan);float work=HeroTraits.LegendMult(ar,Legend.Work,1f);
    Check(cha==2 && intl==2 && wis==0 && Near(psy,2f) && Near(work,1.5f),"ability caps: 银舌 魅力 +"+cha+" and 灵光 智力 +"+intl+" (感知 +"+wis+") both apply; 魔裔之血 心灵 ×"+psy+"; 巧匠之手 工作 ×"+work);
    // 6. 百折不挠：一次致命伤改为倒地（冷却状态 10 天），冷却中第二次照常死
    var un=MtPawn(null,true,10,-3);HeroTraits.Give(un,MoreTraitDefOf.LP_Unyielding);int saved=MoreTraits.Saved;
    un.Kill(new DamageInfo(DamageDefOf.Blunt,30f));bool lived=!un.Dead && MoreTraits.Saved==saved+1 && Items.Has(un,MoreTraitDefOf.LP_MT_UnyieldingSpent);
    un.Kill(new DamageInfo(DamageDefOf.Blunt,30f));
    Check(lived && un.Dead,"百折不挠: a lethal blow leaves the pawn downed with a 10-day cooldown hediff, the next one kills");
    if(enemy==null){Check(false,"no hostile humanlike faction for combat fixtures");return;}
    var knife=DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife");
    // 7. 处刑者：对重伤 / 倒地的敌人 ×1.5，健康的 ×1；亲手击杀后乘胜追击
    var ex=MtPawn(null,true,10,0);HeroTraits.Give(ex,MoreTraitDefOf.LP_Executioner);var foe=MtPawn(enemy,true,11,0);var exi=MoreTraits.Info(ex);
    float f1=MoreTraits.OutFactor(ex,exi,foe);foe.health.AddHediff(HediffDefOf.Anesthetic);foe.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic).Severity=1f;foe.health.CheckForStateChange(null,null);
    float f2=MoreTraits.OutFactor(ex,exi,foe);foe.Kill(new DamageInfo(DamageDefOf.Cut,99f,0f,-1f,ex,null,knife));
    Check(Near(f1,1f) && Near(f2,1.5f) && Items.Has(ex,MoreTraitDefOf.LP_MT_Momentum),"处刑者: healthy foe ×"+f1+", downed foe ×"+f2+", a kill grants 乘胜追击");
    // 8. 狱火烙印：火 ×0.25、寒 ×1.5；武器命中附 4 点火焰
    var hb=MtPawn(null,true,10,3);HeroTraits.Give(hb,MoreTraitDefOf.LP_Hellbrand);var hbi=MoreTraits.Info(hb);
    float fire=MoreTraits.InFactor(hbi,DamageDefOf.Burn),cold=MoreTraits.InFactor(hbi,HeroCombat.ElementDef(2)),cut=MoreTraits.InFactor(hbi,DamageDefOf.Cut);
    var tgt=MtPawn(enemy,true,11,3);int before=MtInjuries(tgt);tgt.TakeDamage(new DamageInfo(DamageDefOf.Cut,3f,0f,-1f,hb,null,knife));
    bool burned=tgt.health.hediffSet.hediffs.Any(i=>i.def.defName=="Burn");
    Check(Near(fire,.25f) && Near(cold,1.5f) && Near(cut,1f) && burned && MtInjuries(tgt)>=before+2,"狱火烙印: fire ×"+fire+", cold ×"+cold+", a knife hit also burns the target ("+(MtInjuries(tgt)-before)+" injuries)");
    // 9. 吸血鬼之咒：近战命中回复伤势；晒太阳判定与烧伤
    var va=MtPawn(null,true,10,6);HeroTraits.Give(va,MoreTraitDefOf.LP_Vampirism);va.TakeDamage(new DamageInfo(DamageDefOf.Cut,6f,0f,-1f,null));float hurt=va.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(i=>i.Severity);
    var prey=MtPawn(enemy,true,11,6);prey.TakeDamage(new DamageInfo(DamageDefOf.Cut,3f,0f,-1f,va,null,knife));float hurt2=va.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(i=>i.Severity);
    MoreTraits.Sun(va);bool sunOk=Items.Has(va,MoreTraitDefOf.LP_MT_Sunburn)==MoreTraits.InSun(va);
    Items.Give(va,"LP_MT_Sunburn",300);int vb=MtInjuries(va);MoreTraits.Burn(va);
    Check(hurt2<hurt-1.5f && sunOk && MtInjuries(va)>vb,"吸血鬼之咒: a melee hit heals "+(hurt-hurt2).ToString("0.0")+", sunlight check matches roof/sky (in sun "+MoreTraits.InSun(va)+"), sunburn burns");
    // 10. 厄运缠身：被打时（几率固定为 0）反伤一半；厄运降临磨损衣甲并给坏心情
    var dm=MtPawn(null,true,10,9);HeroTraits.Give(dm,MoreTraitDefOf.LP_Doomed);var hitter=MtPawn(enemy,true,11,9);int hb0=MtInjuries(hitter);
    MoreTraits.TestRoll=0f;try{dm.TakeDamage(new DamageInfo(DamageDefOf.Cut,6f,0f,-1f,hitter,null,knife));}finally{MoreTraits.TestRoll=null;}
    var parka=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_Parka"),ThingDefOf.Cloth);dm.apparel.Wear(parka);int hp=parka.HitPoints;var worn=MoreTraits.Doom(dm);
    Check(MtInjuries(hitter)>hb0 && worn==parka && parka.HitPoints<hp && DndTraits.HasMemory(dm,"LP_MT_Doom"),"厄运缠身: 厄运转嫁 hurts the attacker, 厄运降临 wears the parka "+hp+"→"+parka.HitPoints+" and adds the mood");
    // 11. 狱火索债心情；失去吸血鬼之咒时日光灼身一起去掉
    MoreTraits.HellDebt(hb);var vt=va.story.traits.GetTrait(MoreTraitDefOf.LP_Vampirism);va.story.traits.RemoveTrait(vt);
    Check(DndTraits.HasMemory(hb,"LP_MT_HellDebt") && !Items.Has(va,MoreTraitDefOf.LP_MT_Sunburn) && MoreTraits.Info(va)==null,"狱火索债 mood; removing 吸血鬼之咒 clears 日光灼身 and the cache");
    // 12. 周期组件：名单里只有带规则型特性的人
    var comp=GameComponent_MoreTraits.Get;comp?.Refresh();
    Check(comp!=null && comp.Holders.Contains(hb) && comp.Holders.Contains(dm) && !comp.Holders.Contains(st),"GameComponent_MoreTraits tracks only pawns with rule traits ("+(comp?.Holders.Count??0)+")");
   }finally{
    foreach(var p in mtMade)if(p!=null && !p.Destroyed){if(p.Dead)p.Corpse?.Destroy();else p.Destroy();}
    mtMade.Clear();s.adventurer=oOn;s.adventurerIsekaiCoexist=oCo;s.heroTraits=oHt;s.dndCurses=oCu;Adventurer.TestSuppressed=oSup;HeroTraits.TestSuppressed=oHsup;MoreTraits.ClearCache();
   }
  }
 }
}
