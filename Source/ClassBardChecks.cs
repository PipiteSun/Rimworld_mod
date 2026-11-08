using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
namespace LichPhylactery {
 // 1.123.0 吟游诗人：Test.ps1 -Only Bard。
 public partial class LichChecks {
  static LP_ClassDef BD=>Bard.Class;
  Pawn BdPawn(int level,int bdLevel,int social=10){var p=ClsPawn(level,0,social);var h=Adventurer.Get(p);for(int i=0;i<bdLevel;i++)ClassSystem.AddLevel(h,BD);return p;}
  Pawn BdAlly(IntVec3 c){var p=ClsPawn(1);GenSpawn.Spawn(p,c,map);return p;}
  static float BdFactor(Pawn victim,Pawn attacker,DamageDef def=null,Thing weapon=null){
   var d=new DamageInfo(def??DamageDefOf.Cut,10f,0f,-1f,attacker,null,weapon?.def);var args=new object[]{victim,d};
   AccessTools.Method(typeof(BardDamage),"Prefix").Invoke(null,args);return ((DamageInfo)args[1]).Amount/10f;
  }
  void CheckBard(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=false;var s=LichMod.Settings;bool oldOn=s.adventurer;s.adventurer=true;var made=new List<Thing>();
   try{
    // 1. 定义：13 个职业能力（1/1/1/3/3/5/5/7/7/10/13/13/15，终极要社交 15），4 个学院（3/7/11/15，学识 3 个分支、其余 2 个），2 个传奇，图标、徽记；
    //    魔法奥秘的 16 个选项和能力一一对应；激昂强度；乐韵；2 个专长、4 颗水晶。
    var subs=ClassSubclass.Options(BD).ToList();var abs=BD.abilities.Select(e=>e.ability).Concat(subs.SelectMany(x=>x.abilities.Select(e=>e.ability))).Concat(BD.legendary).ToList();
    var sec2=subs.FirstOrDefault()?.choices.FirstOrDefault(c=>c.key=="secret2");
    bool labels=sec2!=null && sec2.options.Count==16 && sec2.tips.Count==16 && Enumerable.Range(0,16).All(i=>Bard.SecretOption(i)?.label==sec2.options[i]);
    var crystals=DefDatabase<AbilityDef>.AllDefsListForReading.Where(a=>a.GetModExtension<ArcanumExt>()?.cls==BD).ToList();
    Check(BD!=null && BD.resource==ClassResource.Melody && BD.caster && BD.subclassLevel==3 && BD.order==13 && BD.abilities.Select(e=>e.level).SequenceEqual(new[]{1,1,1,3,3,5,5,7,7,10,13,13,15}) && BD.abilities.Last().capstoneSkill==SkillDefOf.Social
     && subs.Count==4 && subs.All(x=>x.Emblem!=null && x.Emblem!=BD.Background && x.abilities.Select(e=>e.level).SequenceEqual(new[]{3,7,11,15}) && x.choices.Count==(x.defName==Bard.Lore?3:2))
     && abs.Count==31 && abs.Distinct().Count()==31 && abs.All(a=>a.abilityClass==typeof(Ability_Class) && a.uiIcon!=null && a.uiIcon!=BaseContent.BadTex) && BD.Background!=null && labels
     && Bard.Power?.parts.Any(x=>x is StatPart_ClassPower)==true && ClassSystem.ResourceLabel(ClassResource.Melody)=="乐韵" && ClassFeats.For(BD).Count()==2 && crystals.Count==4 && crystals.All(a=>LakeArcana.CrystalFor(a)!=null && a.uiIcon!=BaseContent.BadTex),
     "bard: 13 class abilities (1/1/1/3/3/5/5/7/7/10/13/13/15, capstone needs social 15), 4 colleges × 4 (3/7/11/15, lore 3 choices), 2 legendaries, icons, 16 magical-secret options match their spells, 激昂强度, 乐韵, 2 feats, 4 crystals");
    // 2. 门槛与乐韵：社交 5 不行、6 可以 → 3 个 1 级能力；乐韵上限 2 + 等级 + 魅力调整值（最少 2）；醒着 +0.0104 / 250 刻；聊天 +0.2。
    var a=ClsPawn(1,0,5);var ha=Adventurer.Get(a);string w5=ClassSystem.CanAdd(ha,BD);a.skills.GetSkill(SkillDefOf.Social).Level=6;bool add=ClassSystem.AddLevel(ha,BD);
    int m10=ClassSystem.MaxResource(ha,BD);ha.scores=new[]{10,10,10,10,10,16};ha.Dirty();int m16=ClassSystem.MaxResource(ha,BD);ha.scores=new[]{10,10,10,10,10,6};ha.Dirty();int m6=ClassSystem.MaxResource(ha,BD);ha.scores=new[]{10,10,10,10,10,10};ha.Dirty();
    GenSpawn.Spawn(a,ScCell(0,-14),map);made.Add(a);ha.classes.res[BD]=0f;Bard.Tick250(ha);float awake=ClassSystem.Resource(ha,BD);
    var other=BdAlly(NextTo(a));made.Add(other);float before=ClassSystem.Resource(ha,BD);Bard.Chatted(other,a);float chat=ClassSystem.Resource(ha,BD)-before;
    Check(w5!=null && add && HasAb(a,"LP_BD_ViciousMockery") && HasAb(a,"LP_BD_Inspiration") && HasAb(a,"LP_BD_HealingWord") && !HasAb(a,"LP_BD_HideousLaughter") && m10==3 && m16==6 && m6==2 && Near(awake,Bard.AwakeRegen) && Near(chat,Bard.ChatGain)
     && ClassSystem.ResourceTip(BD).Contains("乐韵"),"bard gate: social 5 refused, 6 → 恶毒嘲讽 / 吟游激励 / 治疗真言; 乐韵 max 3 (CHA 10) / 6 (CHA 16) / 2 (CHA 6, floor); awake +"+awake.ToString("0.####")+" per 250 ticks; a chat +"+chat.ToString("0.##"));
    // 3. 激昂强度：社交 10、吟游 11 → 0.5 + 0.5 + 魅力 + 0.5（每级 5%）；艺术更高时用艺术。
    var pw=BdPawn(11,11);float bp=pw.GetStatValue(Bard.Power);pw.skills.GetSkill(SkillDefOf.Artistic).Level=16;float art=pw.GetStatValue(Bard.Power);
    Check(Near(bp,ClassSystem.Power(10)+Adventurer.ClassPowerBonus(pw,Adventurer.Cha,Adventurer.InnateAbilityPer)+.5f) && Near(art-bp,.3f) && HasAb(pw,"LP_BD_MagicalSecrets") && !HasAb(pw,"LP_BD_Mislead"),"激昂强度 at social 10, bard 11: "+bp.ToString("0.00")+" (level +50%); artistic 16 takes over (+"+(art-bp).ToString("0.00")+")");
    // 4. 吟游激励：给友军（不能给自己），花 1 点乐韵，强度越高档位越高；勇气学院另给战斗激励。
    var b=BdPawn(5,5);var hb=Adventurer.Get(b);GenSpawn.Spawn(b,ScCell(8,-14),map);made.Add(b);var ally=BdAlly(NextTo(b));made.Add(ally);
    var insp=b.abilities.GetAbility(Ab("LP_BD_Inspiration"));string self=ClassEffects.Invalid(b,Fx("LP_BD_Inspiration"),new LocalTargetInfo(b));float r0=ClassSystem.Resource(hb,BD);
    float ws0=ally.GetStatValue(StatDefOf.WorkSpeedGlobal,true,-1);Bard.TestRoll=.99f;bool cast=insp.Activate(new LocalTargetInfo(ally),new LocalTargetInfo(ally));var ih=ally.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_BD_Inspired")) as Hediff_BardInspired;
    Check(cast && self!=null && ih!=null && ih.caster==b && Near(ClassSystem.Resource(hb,BD),r0-1f) && ally.GetStatValue(StatDefOf.WorkSpeedGlobal,true,-1)>ws0+.05f && !Items.Has(ally,"LP_BD_ValorInspired"),"吟游激励: ally gets the buff (work speed "+ws0.ToStringPercent()+" → "+ally.GetStatValue(StatDefOf.WorkSpeedGlobal,true,-1).ToStringPercent()+"), 1 乐韵 spent, cannot target self ("+self+")");
    // 5. 恶毒嘲讽：心灵伤害 + 动摇；狂笑术：成功倒地眩晕，挨打就醒；失败只动摇。
    var foe=ScFoe(b);made.Add(foe);float hp0=foe.health.summaryHealth.SummaryHealthPercent;Bard.Mock(b,foe,6f,360);
    bool mocked=Items.Has(foe,"LP_BD_Mocked") && foe.health.summaryHealth.SummaryHealthPercent<hp0;Items.Remove(foe,"LP_BD_Mocked");
    Bard.TestRoll=0f;bool laugh=Bard.Laughter(b,foe,1f,600);bool stunned=foe.stances.stunner.Stunned && Items.Has(foe,"LP_BD_Laughing");
    foe.TakeDamage(new DamageInfo(DamageDefOf.Blunt,1f,0f,-1f,b));FlushDeferred();bool woke=!Items.Has(foe,"LP_BD_Laughing") && !foe.stances.stunner.Stunned;
    Bard.TestRoll=.99f;bool fail=!Bard.Laughter(b,foe,1f,600) && Items.Has(foe,"LP_BD_Mocked");
    Check(mocked && laugh && stunned && woke && fail && Bard.Chance(Bard.StdBase,1f,foe)<=.55f+.001f && Bard.Chance(Bard.StdBase,2f,foe)>Bard.Chance(Bard.StdBase,1f,foe),"恶毒嘲讽 hurts and shakes; 狂笑术 knocks down (stunned) until hit, a failed roll only shakes; success chance grows with power ("+Bard.Chance(Bard.StdBase,1f,foe).ToStringPercent()+" → "+Bard.Chance(Bard.StdBase,2f,foe).ToStringPercent()+")");
    // 6. 反迷惑之歌：惊恐逃跑的友军回过神，得到 1 天的心智抗性；困惑术：困惑的敌人每 2 秒发呆 / 乱走。
    ally.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"test",true,true);bool panicked=ally.InMentalState;Bard.Countercharm(b,15f,60000,1f);
    Bard.TestRoll=0f;int conf=Bard.Confusion(b,foe.Position,2f,600,1f);Hediff_BardConfused.Act(foe,0);bool daze=foe.stances.stunner.Stunned;foe.stances.stunner.StopStun();Hediff_BardConfused.Act(foe,1);bool wander=foe.CurJobDef==JobDefOf.GotoWander;
    Check(panicked && !ally.InMentalState && Items.Has(ally,"LP_BD_Countercharmed") && conf>=1 && Items.Has(foe,"LP_BD_Confused") && daze && wander,"反迷惑之歌 ends panic and grants 1 day of mind resistance; 困惑术 confuses the foe (daze → stunned, wander → "+foe.CurJobDef?.defName+")");
    Items.Remove(foe,"LP_BD_Confused");Items.Remove(foe,"LP_BD_Mocked");
    // 7. 魔法奥秘：10 级选火球术 → 学会，算吟游诗人的（扣乐韵、用激昂强度）；不能重复；会法师的人还算法师的。
    var w=BdPawn(13,13);var hw=Adventurer.Get(w);ClassSubclass.Pick(hw,Sub(Bard.Lore));bool chose=ClassSubclass.Choose(hw,"secret1",0);ClassSystem.Sync(hw);
    var fbDef=Ab("LP_Fireball");var fbFx=Fx("LP_Fireball");hw.classes.res[BD]=0f;string poor=ClassSystem.CannotCast(w,fbDef,fbFx);
    string dup2=ClassSubclass.CanChoose(hw,"secret2",0),dup3=ClassSubclass.CanChoose(hw,"secret3",0);bool heal=ClassSubclass.Choose(hw,"secret2",12);ClassSystem.Sync(hw);
    var wz=BdPawn(13,10);var hz=Adventurer.Get(wz);wz.skills.GetSkill(SkillDefOf.Intellectual).Level=10;ClassSystem.AddLevel(hz,Cls("Wizard"));ClassSubclass.Pick(hz,Sub(Bard.Valor));ClassSubclass.Choose(hz,"secret1",0);ClassSystem.Sync(hz);
    Check(chose && HasAb(w,"LP_Fireball") && ClassSystem.CastClassOf(w,fbDef)==BD && Bard.StatFor(w,fbFx)==Bard.Power && poor!=null && poor.Contains("乐韵") && dup2!=null && dup3!=null && heal && HasAb(w,"LP_MassHeal")
     && ClassSystem.CastClassOf(wz,fbDef)==Cls("Wizard") && Bard.StatFor(wz,fbFx)==fbFx.stat,"magical secrets: fireball learned and paid with 乐韵 (\""+poor+"\"), 激昂强度; no repeats (\""+dup2+"\" / \""+dup3+"\"); 群体治疗 at 13; a bard-wizard keeps wizard rules");
    // 8. 学识学院：刻薄之语让敌人伤害 ×0.75；博闻强识（研究 + 3 个技能兴趣）；无双技艺品质 +0.3 档。
    var lo=BdPawn(15,15);var hl=Adventurer.Get(lo);ClassSubclass.Pick(hl,Sub(Bard.Lore));ClassSystem.Sync(hl);
    int minor0=lo.skills.skills.Count(r=>r.passion!=Passion.None);Bard.Tick250(hl);int minor1=lo.skills.skills.Count(r=>r.passion!=Passion.None);
    Items.Give(foe,"LP_BD_CuttingWords",1200,1f);float cut=BdFactor(b,foe);Items.Remove(foe,"LP_BD_CuttingWords");
    Check(Items.Has(lo,"LP_BD_LorePassive") && Items.Has(lo,"LP_BD_PeerlessPassive") && minor1==Math.Min(minor0+3,lo.skills.skills.Count(r=>!r.TotallyDisabled)) && Near(Bard.QualityTiers(lo),.3f) && Near(ArtificerCraftQuality.Tiers(lo),.3f) && Near(cut,.75f),
     "lore: 博闻强识 / 无双技艺 passives, 3 skills gain passion ("+minor0+" → "+minor1+"), crafting +0.3 tier, 刻薄之语 cuts the foe's damage to ×"+cut);
    // 9. 勇气学院：吟游激励另给战斗激励；施放后战斗魔法 ×1.25（打中一次就用掉）。
    var v=BdPawn(11,11);var hv=Adventurer.Get(v);ClassSubclass.Pick(hv,Sub(Bard.Valor));ClassSystem.Sync(hv);GenSpawn.Spawn(v,NextTo(b),map);made.Add(v);Bard.Tick250(hv);
    Bard.Give(v,ally,1f,600,false);bool valorBuff=Items.Has(ally,"LP_BD_ValorInspired");
    var mock=v.abilities.GetAbility(Ab("LP_BD_ViciousMockery"));bool mc=mock.Activate(new LocalTargetInfo(foe),new LocalTargetInfo(foe));bool bm=Items.Has(v,"LP_BD_BattleMagicBuff");float bf=BdFactor(foe,v);FlushDeferred();
    Check(valorBuff && Items.Has(v,"LP_BD_ValorPassive") && Items.Has(v,"LP_BD_ExtraAttackPassive") && mc && bm && Near(bf,1.25f) && !Items.Has(v,"LP_BD_BattleMagicBuff"),"valor: combat inspiration on allies, +3 skills / extra attack passives; 战斗魔法 after a cast ×"+bf+" then used up");
    // 10. 剑舞学院：切换招式；防御剑花给护盾。
    var sw=BdPawn(3,3);var hs=Adventurer.Get(sw);ClassSubclass.Pick(hs,Sub(Bard.Swords));ClassSystem.Sync(hs);GenSpawn.Spawn(sw,NextTo(foe),map);made.Add(sw);Bard.Tick250(hs);
    int m1=Bard.CycleMode(sw);for(int i=0;i<5;i++)Bard.CycleMode(sw);Bard.TestRoll=0f;bool fl=Bard.Flourish(sw,foe,5f,DamageDefOf.Cut);FlushDeferred();
    Check(m1==1 && Bard.Mode(sw)==0 && fl && Items.Has(sw,"LP_FeatInspired") && Items.Has(sw,"LP_BD_BladePassive") && Bard.Tip(sw,Ab("LP_BD_BladeFlourish")).Contains("防御"),"swords: modes cycle (6 states), a defensive flourish grants the shield, tooltip shows the mode");
    // 11. 雄辩学院：激励转给下一个人；和中立派系的人聊天好感 +1（每天最多 5）；停战之言让袭击队伍离开。
    var el=BdPawn(15,15);var he=Adventurer.Get(el);ClassSubclass.Pick(he,Sub(Bard.Eloquence));ClassSystem.Sync(he);GenSpawn.Spawn(el,NextTo(b),map);made.Add(el);Bard.Tick250(he);
    var x1=BdAlly(NextTo(el));made.Add(x1);var x2=BdAlly(NextTo(el));made.Add(x2);var given=Bard.Give(el,x1,1f,600,false);var passed=Bard.Pass(given);
    var neutral=Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f=>!f.IsPlayer && !f.Hidden && f.HasGoodwill && f.def.humanlikeFaction && !f.HostileTo(Faction.OfPlayer));
    int gw0=neutral?.PlayerGoodwill??0;int gained=0;Pawn visitor=null;
    if(neutral!=null){visitor=AdvPawn(null,neutral);made.Add(visitor);for(int i=0;i<7;i++)gained+=Bard.Chatted(el,visitor);}
    var raider=ScFoe(el);made.Add(raider);LordMaker.MakeNewLord(raider.Faction,new LordJob_AssaultColony(raider.Faction),map,new List<Pawn>{raider});string why=Bard.TruceCannot(el,raider);
    Bard.TestRoll=0f;bool truce=Bard.Truce(el,raider,1f);
    Check(passed!=null && given.moved && Items.Has(passed,"LP_BD_Inspired") && (neutral==null || gained==5 && neutral.PlayerGoodwill>=gw0+4) && why==null && truce && raider.GetLord()?.LordJob is LordJob_ExitMapBest && Items.Has(el,"LP_BD_SilverPassive"),
     "eloquence: inspiration passes on once, chatting with "+(neutral?.Name??"no neutral faction")+" +"+gained+" goodwill (cap 5/day), 停战之言 sends the raid home");
    // 12. 创世之言：友方治好所有伤口；残血敌人直接死亡。
    ally.TakeDamage(new DamageInfo(DamageDefOf.Cut,8f,0f,-1f,null,ally.RaceProps.body.AllParts.First(x=>x.def.defName=="Torso")));Bard.WordHeal(b,ally);float left=ClassMelee.InjurySum(ally);
    var low=ScFoe(b);made.Add(low);for(int i=0;i<20 && !low.Dead && low.health.summaryHealth.SummaryHealthPercent>.6f;i++)low.TakeDamage(new DamageInfo(DamageDefOf.Cut,6f,0f,-1f,null,low.health.hediffSet.GetNotMissingParts().Where(x=>x.coverageAbs>0f && x.depth==BodyPartDepth.Outside && x.def.defName!="Head" && x.def.defName!="Neck").RandomElement()));
    bool killed=low.Dead || Bard.WordKill(b,low,2f,40f) && low.Dead;
    Check(Near(left,0f) && killed,"创世之言: heals every wound ("+left+" left); kills a foe under 70% health at power 2");
    // 13. 传奇：不朽传奇乐韵上限 +5、激励覆盖 12 格；终焉之歌全图友方 + 吓跑敌人。
    var lg=BdPawn(20,15);var hg=Adventurer.Get(lg);GenSpawn.Spawn(lg,NextTo(b),map);made.Add(lg);int mx0=ClassSystem.MaxResource(hg,BD);hg.classes.legendaryPoints=2;
    ClassLegendary.Learn(hg,BD,Ab("LP_LegUndyingLegend"));ClassLegendary.Learn(hg,BD,Ab("LP_LegFinalSong"));Bard.Tick250(hg);int mx1=ClassSystem.MaxResource(hg,BD);
    int many=Bard.Inspire(lg,ally,1f);int fin=Bard.Finale(lg,1800);Bard.TestRoll=0f;var foe3=ScFoe(lg);made.Add(foe3);int fled=Bard.FinaleFearTick(lg);
    Check(mx1==mx0+7 && many>=3 && fin>=3 && Items.Has(ally,"LP_BD_FinalSong") && Items.Has(lg,"LP_BD_FinaleAura") && fled>=1,"legendary: 不朽传奇 + 终焉之歌 raise 乐韵 max "+mx0+" → "+mx1+" (+2 +2 +3), group inspiration hits "+many+", 终焉之歌 buffs "+fin+" allies and scares "+fled+" foes");
    // 14. 水晶：不可抗拒之舞、力量真言·震慑、群体隐形（攻击就现形）。
    Bard.TestRoll=0f;foe3.stances.stunner.StopStun();bool dance=Bard.Dance(lg,foe3,1f,600) && Items.Has(foe3,"LP_ACB_Dancing") && foe3.stances.stunner.Stunned;
    foe.stances.stunner.StopStun();int stun=Bard.PowerWordStun(lg,foe.Position,1f,360);int invis=Bard.MassInvisibility(lg,10f,1800);bool hidden=Bard.Invisible.Contains(ally);BdFactor(foe,ally);FlushDeferred();
    Check(dance && stun>=1 && foe.stances.stunner.Stunned && invis>=2 && hidden && !Bard.Invisible.Contains(ally) && !Items.Has(ally,"PsychicInvisibility"),"crystals: 不可抗拒之舞 holds a foe, 力量真言·震慑 stuns, 群体隐形 hides "+invis+" allies and an attack reveals");
    // 15. 传颂之歌：选中的派系好感 +(5 + 5 × 强度)，殖民者心情。专长：天生歌喉 乐韵 +3、获取 ×1.5。
    int bg0=neutral?.PlayerGoodwill??0;int amount=neutral==null?0:Bard.Ballad(b,neutral);
    var fv=BdPawn(7,7);var hf=Adventurer.Get(fv);int rm0=ClassSystem.MaxResource(hf,BD);hf.AddFeat(FD("LP_Feat_Class_BardVoice"));int rm1=ClassSystem.MaxResource(hf,BD);hf.classes.res[BD]=0f;float gain=ClassSystem.AddResource(hf,BD,1f);
    Check((neutral==null || amount==Bard.BalladAmount(b.GetStatValue(Bard.Power),Bard.Notorious(b)!=null) && neutral.PlayerGoodwill>=Math.Min(100,bg0+amount)-1) && rm1==rm0+3 && Near(gain,1.5f),"传颂之歌 +"+amount+" goodwill; 天生歌喉 乐韵 max "+rm0+" → "+rm1+", gain ×1.5");
    // 16. NPC：巡演的吟游诗人是 3～7 级吟游诗人，自动选学院，乐韵满。
    var npc=AdvPawn(null,neutral??AdvEnemy);made.Add(npc);bool mk=Bard.MakeNpcBard(npc,5);var hn=Adventurer.Get(npc);
    Check(mk && Bard.Level(npc)==5 && ClassSubclass.Picked(hn,BD)!=null && HasAb(npc,"LP_BD_Countercharm") && Near(ClassSystem.Resource(hn,BD),ClassSystem.MaxResource(hn,BD)),"NPC bard: level 5, college "+ClassSubclass.Picked(hn,BD)?.label+", full 乐韵");
   }finally{
    Bard.TestRoll=null;foreach(var t in made)if(!t.Destroyed){if(t is Pawn p)p.GetLord()?.RemovePawn(p);t.Destroy();}foreach(var p in advMade)if(!p.Destroyed)p.Destroy();advMade.Clear();
    s.adventurer=oldOn;Adventurer.TestSuppressed=sup;
   }
  }
 }
}
