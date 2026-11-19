using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 2026-11-19 心灵术士、枪手（Test.ps1 -Only NewClasses）。
 public partial class LichChecks {
  void CheckNewClasses(){
   var PS=Psion.Class;var GS=Gunslinger.Class;var made=new List<Pawn>();
   try{
    // 1. 定义：两个职业、各 4 个子职业、能力都有图标、强度属性、心灵术士法术表的法术都找得到。
    var psSubs=ClassSubclass.Options(PS).ToList();var gsSubs=ClassSubclass.Options(GS).ToList();
    var psAbs=PS.abilities.Select(e=>e.ability).Concat(psSubs.SelectMany(s=>s.abilities.Select(e=>e.ability))).Concat(PS.legendary).ToList();
    var gsAbs=GS.abilities.Select(e=>e.ability).Concat(gsSubs.SelectMany(s=>s.abilities.Select(e=>e.ability))).Concat(GS.legendary).ToList();
    var list=CasterSpells.ListOf(PS);
    bool icons=psAbs.Concat(gsAbs).All(a=>a!=null && a.abilityClass==typeof(Ability_Class) && a.uiIcon!=null && a.uiIcon!=BaseContent.BadTex);
    Check(PS!=null && GS!=null && PS.caster && PS.resource==ClassResource.Mana && PS.subclassLevel==3 && GS.martial && GS.resource==ClassResource.Grit && psSubs.Count==4 && gsSubs.Count==4
     && psSubs.All(s=>s.choices.Count>=6) && icons && list!=null && list.Table.Count==list.spells.Count && DefDatabase<StatDef>.GetNamed("LP_PsionicPower").parts.Any(x=>x is StatPart_ClassPower)
     && DefDatabase<StatDef>.GetNamed("LP_GunPower").parts.Any(x=>x is StatPart_ClassPower) && ClassSystem.ResourceLabel(ClassResource.Grit)=="胆识",
     "new classes: psion (caster, mana, 4 subclasses with 6 discipline choices, "+(list?.Table.Count??0)+"/"+(list?.spells.Count??0)+" spells) and gunslinger (martial, grit, 4 ways); "+psAbs.Count+"+"+gsAbs.Count+" abilities with icons");
    // 2. 心灵术士：智识 6 学 1 级 → 念动推拉等；灵能点 4，花 1 剩 3；5 级 6 点二档；法力 2 + 等级；灵能值在范围里。
    var a=ClsPawn(5);a.SetFactionDirect(Faction.OfPlayer);a.skills.GetSkill(SkillDefOf.Intellectual).Level=6;var ha=Adventurer.Get(a);bool add=ClassSystem.AddLevel(ha,PS);
    int p1=Psion.Points(ha);bool spent=Psion.Spend(a,1);int p2=Psion.Points(ha);
    for(int i=0;i<4;i++)ClassSystem.AddLevel(ha,PS);int p5=Psion.MaxPoints(ha);int tier=Psion.Tier(a);Psion.RollOverride=0f;float lo=Psion.Roll(a,false);Psion.RollOverride=1f;float hi=Psion.Roll(a,false);Psion.RollOverride=-1f;
    Check(add && HasAb(a,"LP_PS_Propel") && HasAb(a,"LP_PS_Telepathy") && p1==4 && spent && p2==3 && Psion.Level(ha)==5 && p5==6 && tier==2 && Near(lo,3f) && Near(hi,10f) && ClassSystem.MaxResource(ha,PS)==7 && HasAb(a,"LP_PS_Restoration"),
     "psion: level 1 grants 念动推拉 / 心灵感应, 4 灵能点 (spend 1 → 3), level 5 → 6 points, tier 2 rolls 3～10, mana 7 ("+p1+" "+p2+" "+p5+" "+lo+"～"+hi+")");
    // 3. 子职业与修行：血肉塑形者 → 柔变之躯、治疗伤口常备；同一修行不能修两次；心灵感应者的常备法术不给。
    bool picked=ClassSubclass.Pick(ha,DefDatabase<LP_SubclassDef>.GetNamed(Psion.Metamorph));bool c1=ClassSubclass.Choose(ha,"psd1",Psion.DBio);string dup=ClassSubclass.CanChoose(ha,"psd2",Psion.DBio);bool c2=ClassSubclass.Choose(ha,"psd2",Psion.DGuards);
    ClassSystem.Sync(ha);var book=CasterSpells.Book(ha,list,true);var cure=DefDatabase<AbilityDef>.GetNamed("LP_CureWounds");var counter=DefDatabase<AbilityDef>.GetNamed("LP_WS_Counterspell");
    Psion.Tick250(ha);
    Check(picked && c1 && dup!=null && c2 && Psion.Discipline(ha,Psion.DBio) && Psion.Discipline(ha,Psion.DGuards) && HasAb(a,"LP_PS_MutableForm") && HasAb(a,"LP_PS_Guards")
     && CasterSpells.Active(ha,list,book,cure) && !CasterSpells.Active(ha,list,book,counter) && !CasterSpells.Preparable(ha,list).Contains(cure),
     "psion subclass: metamorph grants 柔变之躯, 治疗伤口 always prepared (not preparable), 反制法术 not; disciplines can't repeat ("+dup+"); 灵能守护 button");
    // 4. 枪手：火器分类（左轮短枪、栓动长枪、加特林重火器；短弓、手雷不算）；射击 6 学 1 级；胆识 3（等级 1）；击杀 +1。
    var rev=DefDatabase<ThingDef>.GetNamedSilentFail("Gun_Revolver");var bolt=DefDatabase<ThingDef>.GetNamedSilentFail("Gun_BoltActionRifle");var mini=DefDatabase<ThingDef>.GetNamedSilentFail("Gun_Minigun");
    var bow=DefDatabase<ThingDef>.GetNamedSilentFail("Bow_Short");var nade=DefDatabase<ThingDef>.GetNamedSilentFail("Weapon_GrenadeFrag");
    var g=ClsPawn(5,0,0,6);var hg=Adventurer.Get(g);bool gadd=ClassSystem.AddLevel(hg,GS);float grit0=ClassSystem.Resource(hg,GS);
    var foe=ClsPawn(1);foe.SetFactionDirect(Find.FactionManager.FirstFactionOfDef(FactionDefOf.Pirate)??Find.FactionManager.AllFactions.First(f=>f.HostileTo(Faction.OfPlayer)));
    g.SetFactionDirect(Faction.OfPlayer);Gunslinger.Tick250(hg);ClassSystem.AddResource(hg,GS,-1f);float before=ClassSystem.Resource(hg,GS);
    var gun=rev==null?null:ThingMaker.MakeThing(rev);Gunslinger.Killed(foe,g,new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,g,null,rev));float after=ClassSystem.Resource(hg,GS);
    Check(Gunslinger.KindOf(rev)==Gunslinger.Kind.Short && Gunslinger.KindOf(bolt)==Gunslinger.Kind.Long && Gunslinger.KindOf(mini)==Gunslinger.Kind.Heavy && !Gunslinger.Firearm(bow) && !Gunslinger.Firearm(nade)
     && gadd && HasAb(g,"LP_GS_ViolentShot") && Near(grit0,3f) && Near(after,before+1f) && ClassSystem.CannotCast(g,DefDatabase<AbilityDef>.GetNamed("LP_GS_ViolentShot"),DefDatabase<AbilityDef>.GetNamed("LP_GS_ViolentShot").comps.OfType<CompProperties_ClassEffect>().First())!=null,
     "gunslinger: revolver short / bolt rifle long / minigun heavy, bow & grenade not firearms; level 1 → 暴力射击 (needs a gun), 3 胆识, firearm kill +1 ("+grit0+" "+before+"→"+after+")");
    // 5. 枪手是物理职业：5 级有额外攻击；等级合计算进物理职业加成。
    for(int i=0;i<4;i++)ClassSystem.AddLevel(hg,GS);Martial.Refresh(g);
    Check(Martial.Is(g) && Martial.Tier(g)==1 && (Martial.Info(g)?.total??0)==5,"gunslinger counts as a martial class: extra attack tier 1 at level 5, total 5");
   }
   finally{foreach(var x in made)if(x!=null && !x.Destroyed)x.Destroy();}
  }
 }
}
