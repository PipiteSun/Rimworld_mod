using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
namespace LichPhylactery {
 // 1.112.0 DND 植入物（Test.ps1 -Only Implants）。
 public partial class LichChecks {
  Hediff Implant(Pawn p,string def,string part){var d=DefDatabase<HediffDef>.GetNamed(def);var bp=part==null?null:p.health.hediffSet.GetNotMissingParts().First(x=>x.def.defName==part);var h=HediffMaker.MakeHediff(d,p,bp);p.health.AddHediff(h,bp);return h;}
  void CheckImplants(){
   var made=new List<Thing>();
   Pawn Add(Pawn x){made.Add(x);return x;}
   try{
    // 1. 定义：7 件义肢、11 颗颅石 + 原石有贴图；安装手术用 DND 的工人类；注能义肢研究在 (6, 0.7)；原石进了魔法战利品。
    var parts=DefDatabase<HediffDef>.AllDefs.Where(d=>DndImplants.Ext(d)!=null).ToList();var items=parts.Select(d=>d.spawnThingOnRemoved).ToList();
    var installs=DefDatabase<RecipeDef>.AllDefs.Where(r=>DndImplants.Ext(r.addsHediff)!=null).ToList();var res=DefDatabase<ResearchProjectDef>.GetNamed("LP_Research_InfusedProsthetics");
    Check(parts.Count==37 && parts.Count(d=>DndImplants.Ext(d).ionStone)==11 && items.All(t=>t!=null && t.graphic.MatSingle.mainTexture!=BaseContent.BadTex) && DefDatabase<ThingDef>.GetNamed("LP_IounBlank").graphic.MatSingle.mainTexture!=BaseContent.BadTex
     && installs.Count==37 && installs.All(r=>typeof(Recipe_InstallDndPart).IsAssignableFrom(r.workerClass) || r.workerClass==typeof(Recipe_InstallDndImplant)) && Near(res.ResearchViewX,6f) && Near(res.ResearchViewY,.7f) && IsekaiLoot.MagicLoot.Contains("LP_IounBlank"),
     "implants: 37 implants (7 parts, 11 ioun stones, 4 flesh, 13 grafts, 2 vecna) textured, 37 install surgeries gated by attunement, research at (6,0.7), blank stone in magic loot");
    // 2. 属性石：力量 14 → 16；19 → 20（不超过 20）；精通之石熟练 +1；警觉之石不会被偷袭。
    var a=Add(S2Pawn("LP_Class_Fighter",1,null));var ha=Adventurer.Get(a);ha.scores[Adventurer.Str]=14;ha.scores[Adventurer.Dex]=19;ha.Dirty();int s0=ha.Eff(Adventurer.Str),pr0=ha.Proficiency;
    Implant(a,"LP_Ioun_Str","Skull");Implant(a,"LP_Ioun_Dex","Skull");Implant(a,"LP_Ioun_Mastery","Skull");int s1=ha.Eff(Adventurer.Str),d1=ha.Eff(Adventurer.Dex),pr1=ha.Proficiency;
    string fourth=DndImplants.CanAttune(a,DefDatabase<HediffDef>.GetNamed("LP_Ioun_Alert"));
    Check(s0==14 && s1==16 && d1==20 && pr1==pr0+1 && fourth!=null && DndImplants.Attuned(a)==3,"ioun stones: str 14→"+s1+", dex 19→"+d1+", proficiency +1, a fourth stone is refused ("+fourth+")");
    // 3. 调谐：普通人 3 个；器械师 11 级 4 个；不占位的注能义臂随便装。
    var art=Add(S2Pawn("LP_Class_Artificer",11,null,11));Implant(art,"LP_Imp_MithralLeg","Leg");Implant(art,"LP_Imp_AdamantArm","Shoulder");Implant(art,"LP_Imp_MithralHeart","Heart");
    string fourth2=DndImplants.CanAttune(art,DefDatabase<HediffDef>.GetNamed("LP_Ioun_Con"));string plain=DndImplants.CanAttune(art,DefDatabase<HediffDef>.GetNamed("LP_Imp_InfusedArm"));
    Implant(art,"LP_Ioun_Con","Skull");string fifth=DndImplants.CanAttune(art,DefDatabase<HediffDef>.GetNamed("LP_Ioun_Wis"));
    Check(DndImplants.AttuneMax(a)==3 && DndImplants.AttuneMax(art)==4 && fourth2==null && plain==null && fifth!=null,"attunement: 3 slots, artificer 11 gets 4; plain parts don't take a slot; the fifth attuned implant is refused");
    // 4. 精金义臂：手臂上的伤 ×0.5；秘银义腿：衣物移速惩罚减半；寒铁义臂：打灵能者 ×1.3；吸收之石每天一次；精工机关眼不受光照惩罚。
    var armPart=art.health.hediffSet.hediffs.First(h=>h.def.defName=="LP_Imp_AdamantArm").Part;if(!DndImplants.UnderAdamant(art,armPart) || DndImplants.UnderAdamant(art,art.RaceProps.body.corePart))armPart=null;
    var vest=(Apparel)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Apparel_PlateArmor"),ThingDefOf.Steel);float raw=0f;foreach(var m in vest.def.equippedStatOffsets??new List<StatModifier>())if(m.stat==StatDefOf.MoveSpeed)raw+=m.value;
    float gear=StatWorker.StatOffsetFromGear(vest,StatDefOf.MoveSpeed);art.apparel.Wear(vest,false);float worn=StatWorker.StatOffsetFromGear(vest,StatDefOf.MoveSpeed);
    var ca=Add(S2Pawn("LP_Class_Fighter",1,null));Implant(ca,"LP_Imp_ColdIronArm","Shoulder");Implant(ca,"LP_Ioun_Absorb","Skull");Implant(ca,"LP_Imp_ClockEye","Eye");ca.SetFaction(Faction.OfPlayer);
    var psy=Add(S2Pawn("LP_Class_Fighter",1,null));psy.SetFaction(Find.FactionManager.FirstFactionOfDef(FactionDefOf.Pirate)??Find.FactionManager.AllFactions.First(f=>f.HostileTo(Faction.OfPlayer)));
    psy.health.AddHediff(HediffDefOf.PsychicAmplifier,psy.health.hediffSet.GetBrain());Traverse.Create(psy.psychicEntropy).Field("psylinkCachedForTick").SetValue(-1);
    float cold=DndImplants.DamageFactor(psy,Dmg(DamageDefOf.Cut,ca));float ab1=DndImplants.DamageFactor(ca,Dmg(DefDatabase<DamageDef>.GetNamed("LP_Lightning"),psy)),ab2=DndImplants.DamageFactor(ca,Dmg(DefDatabase<DamageDef>.GetNamed("LP_Lightning"),psy));
    bool glow=AccessTools.Method(typeof(StatPart_Glow),"ActiveFor")!=null && !(bool)AccessTools.Method(typeof(StatPart_Glow),"ActiveFor").Invoke(new StatPart_Glow(),new object[]{ca});
    Check(armPart!=null && raw<0f && Near(worn,gear*.5f) && Near(cold,1.3f) && Near(ab1,0f) && Near(ab2,1f) && glow,"adamant arm shields its hand ("+(armPart!=null)+"/"+raw+"/"+ab1+"/"+ab2+"/"+glow+"), mithral leg halves armor slowdown ("+gear+"→"+worn+"), cold iron ×"+cold+" on a psycaster, absorb once a day, clockwork eye ignores darkness");
    // 5. 注能：原版仿生臂注能后操作 +5%（一半折算）；器械师调校：器械师 12 级装着 DND 义肢 → 每条 +4.5%；拆掉仿生臂注能消失。
    var bio=Add(S2Pawn("LP_Class_Artificer",12,null,12));var bionic=Implant(bio,"BionicArm","Shoulder");bool can=DndImplants.Infusable(bio,bionic);var inf=DndImplants.Infuse(bio,bionic.Part) as Hediff_CapBonus;
    float infM=inf?.caps.TryGetValue(PawnCapacityDefOf.Manipulation,out float v1)==true?v1:0f;Implant(bio,"LP_Imp_InfusedLeg","Leg");DndImplants.TuneTick(bio);
    var tuned=bio.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ArtificerTuned")) as Hediff_CapBonus;float tm=tuned?.caps.TryGetValue(PawnCapacityDefOf.Moving,out float v2)==true?v2:0f;
    bio.health.RemoveHediff(bionic);bio.health.hediffSet.DirtyCache();bool gone=inf.ShouldRemove;
    Check(can && Near(infM,.05f) && Near(tm,.045f) && gone && !DndImplants.Infusable(bio,bio.health.hediffSet.hediffs.First(h=>h.def.defName=="LP_Imp_InfusedLeg")),"infusion: bionic arm +"+infM+" manipulation, artificer 12 tunes a DND leg +"+tm+" moving, infusion goes with the bionic, DND parts can't be infused");
    // 6. 再生之石：治好最重的一处伤；维生之石饥饿 ×0.2。
    var rg=Add(S2Pawn("LP_Class_Fighter",1,null));Implant(rg,"LP_Ioun_Regen","Skull");Implant(rg,"LP_Ioun_Sustain","Skull");
    var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,rg,rg.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso));cut.Severity=8f;rg.health.AddHediff(cut);float i0=ClassMelee.InjurySum(rg);DndImplants.Regen(rg);float i1=ClassMelee.InjurySum(rg);
    Check(i0>0f && i1<i0 && Near(rg.health.hediffSet.HungerRateFactor,.2f),"regen stone heals the worst wound ("+i0+"→"+i1+""+"), sustenance hunger ×"+rg.health.hediffSet.HungerRateFactor);
    // 7. 血肉扩充：骸骨肋笼 躯干生命 ×1.5、利器 ×0.8；冥火之心 流血 ×0.2；亡者之语 学到尸体最高技能 10%（同一具只能一次）；吞魂之颌 咬死活物回精神力。
    var fl=Add(S2Pawn("LP_Class_Fighter",1,null));fl.SetFaction(Faction.OfPlayer);ScSpawn(fl,ScCell(-14,4));var torso=fl.RaceProps.body.corePart;float tmax0=PartHealth.Maximum(torso,fl);
    Implant(fl,"LP_Flesh_BoneCage","Torso");float tmax1=PartHealth.Maximum(torso,fl);float sharp=DndImplants.DamageFactor(fl,Dmg(DamageDefOf.Cut,null));
    var heart=Implant(fl,"LP_Flesh_HellHeart","Heart");Implant(fl,"LP_Flesh_DeadEar","Ear");Implant(fl,"LP_Flesh_SoulJaw","Jaw");
    var victim=Add(ScFoe(fl));victim.skills.GetSkill(SkillDefOf.Shooting).Level=12;victim.Kill(null);var corpse=victim.Corpse;
    float xp0=fl.skills.GetSkill(SkillDefOf.Shooting).XpTotalEarned+fl.skills.GetSkill(SkillDefOf.Shooting).xpSinceLastLevel;string learned=DndImplants.DeadSpeak(fl,corpse);float xp1=fl.skills.GetSkill(SkillDefOf.Shooting).XpTotalEarned+fl.skills.GetSkill(SkillDefOf.Shooting).xpSinceLastLevel;string again=DndImplants.DeadSpeak(fl,corpse);
    var prey=Add(ScFoe(fl));fl.psychicEntropy?.OffsetPsyfocusDirectly(-1f);float pf0=fl.psychicEntropy?.CurrentPsyfocus??0f;var bite=new DamageInfo(DamageDefOf.Bite,999f,0f,-1f,fl);bite.SetWeaponHediff(DefDatabase<HediffDef>.GetNamed("LP_Flesh_SoulJaw"));prey.Kill(bite);float pf1=fl.psychicEntropy?.CurrentPsyfocus??0f;
    Check(tmax1/tmax0>1.45f && tmax1/tmax0<1.56f && Near(sharp,.8f) && Near(heart.CurStage.totalBleedFactor,.2f) && fl.abilities.GetAbility(Ab("LP_DeadSpeak"))!=null && learned=="Shooting" && xp1>xp0 && again==null && pf1>pf0,"flesh implants: bone cage torso ×"+(tmax1/tmax0)+" and sharp ×"+sharp+", hell heart bleed ×0.2, dead speak learned "+learned+" ("+xp0+"→"+xp1+") once, soul jaw restores focus "+pf0+"→"+pf1);
    // 8. 嫁接：眼魔之眼给射线能力；赤龙鳞 火焰 ×0.5、寒冰 ×1.2，第二件龙鳞被拒；巨魔再生腺 受火后停滞、长回手指；龙息喷到敌人；装在巫妖身上 72 小时坏死。
    var gr=Add(S2Pawn("LP_Class_Fighter",1,null));gr.SetFaction(Faction.OfPlayer);ScSpawn(gr,ScCell(14,4));Implant(gr,"LP_Graft_BeholderEye","Eye");int mode=DndImplants.RayMode(gr);
    Implant(gr,"LP_Graft_ScaleRed","Torso");float fireF=DndImplants.DamageFactor(gr,Dmg(DamageDefOf.Flame,null)),frostF=DndImplants.DamageFactor(gr,Dmg(DefDatabase<DamageDef>.GetNamed("LP_Frost"),null));string second=DndImplants.CanAttune(gr,DefDatabase<HediffDef>.GetNamed("LP_Graft_ScaleBlue"));
    Implant(gr,"LP_Graft_TrollGland","Torso");DndImplants.DamageFactor(gr,Dmg(DamageDefOf.Flame,null));bool stalled=Items.Has(gr,"LP_GraftTrollStalled");
    var finger=gr.health.hediffSet.GetNotMissingParts().First(x=>x.def.defName=="Finger");gr.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,gr,finger),finger);var grown=DndImplants.RegrowSmall(gr);
    Implant(gr,"LP_Graft_BreathRed","Neck");var foe=Add(ScFoe(gr));float fh0=ClassMelee.InjurySum(foe);int burned=DndImplants.Breath(gr,foe.Position);
    var lich=Add(S2Pawn("LP_Class_Fighter",1,null));lich.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);var arm=(HediffWithComps)Implant(lich,"LP_Graft_DemonArm","Shoulder");var comp=arm.TryGetComp<HediffComp_DndImplant>();
    float sev=0f;for(int i=0;i<DndImplants.NecrosisHours && !lich.Dead && lich.health.hediffSet.hediffs.Contains(arm);i++)comp.CompPostTickInterval(ref sev,GenDate.TicksPerHour);bool rotted=Items.IsLich(lich) && !lich.health.hediffSet.hediffs.Contains(arm);
    Check(gr.abilities.GetAbility(Ab("LP_GraftRay"))!=null && mode>=0 && Near(fireF,.5f) && Near(frostF,1.2f) && second!=null && stalled && grown!=null && burned>=1 && gr.abilities.GetAbility(Ab("LP_GraftBreath"))!=null && rotted,"grafts: beholder ray ("+DndImplants.RayModes[mode]+"), red scale fire ×"+fireF+" frost ×"+frostF+", one scale only, troll gland stalls on fire and regrows a finger, breath hits "+burned+", grafts rot on a lich");
    // 9. 维克那（1.114.0）：左手完好时不能装手，截掉后能装；力量至少 20、得到寒冰触和指挥亡灵；双件触发维克那的意志；神器部位不能强化；拆下来心情 −15；卡斯带着卡斯之剑来袭，剑对宿主 ×2。
    var vc=Add(S2Pawn("LP_Class_Fighter",1,null));vc.SetFaction(Faction.OfPlayer);ScSpawn(vc,ScCell(0,-14));var hv=Adventurer.Get(vc);hv.scores[Adventurer.Str]=12;hv.Dirty();
    var rh=DefDatabase<RecipeDef>.GetNamed("LP_Install_LP_Vecna_Hand");int before=rh.Worker.GetPartsToApplyOn(vc,rh).Count();
    var lhand=vc.RaceProps.body.AllParts.First(x=>x.def.defName=="Hand" && Recipe_InstallVecna.Left(x));vc.health.AddHediff(HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart,vc,lhand),lhand);int after=rh.Worker.GetPartsToApplyOn(vc,rh).Count();
    Implant(vc,"LP_Vecna_Hand","Hand");int str=hv.Eff(Adventurer.Str);bool abs=vc.abilities.GetAbility(Ab("LP_VecnaFrostTouch"))!=null && vc.abilities.GetAbility(Ab("LP_VecnaCommand"))!=null;
    var leye=vc.RaceProps.body.AllParts.First(x=>x.def.defName=="Eye" && Recipe_InstallVecna.Left(x));var veye=Implant(vc,"LP_Vecna_Eye","Eye");bool will=DndImplants.VecnaWill(vc);bool noLimb=DndImplants.ArtifactOn(vc,veye.Part);
    var kas=DndImplants.KasRaid(map);var kasCrew=kas?.GetLord()?.ownedPawns.ToList()??new List<Pawn>();foreach(var k in kasCrew)made.Add(k);
    float kasF=kas==null?0f:DndImplants.DamageFactor(vc,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,kas,null,kas.equipment?.Primary?.def));
    vc.health.RemoveHediff(veye);bool loss=vc.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_VecnaLoss"))!=null;string roll=DndImplants.RollArtifact(1f);
    Check(before==0 && after==1 && str==20 && abs && will && noLimb && kas!=null && kas.equipment?.Primary?.def.defName=="LP_SwordOfKas" && Items.Has(kas,"LP_KasBlood") && Near(kasF,2f) && loss && roll=="LP_Vecna_Eye" && !DndImplants.VecnaWill(vc),
     "vecna: hand needs the left hand cut off ("+before+"→"+after+"), str "+str+", hand abilities, will with both, no lich strengthening, Kas raids with his sword (×"+kasF+" vs the holder), losing the eye hurts mood, a free artifact can drop again ("+roll+")");
   }finally{
    foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn pp && pp.Dead){pp.Corpse?.Destroy();continue;}t.Destroy();}
   }
  }
 }
}
