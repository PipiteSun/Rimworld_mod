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
 // 1.130.0 施法职业法术表：Test.ps1 -Only CasterSpells。
 public partial class LichChecks {
  static string csSavedId;
  static LP_ClassDef CsClass(string n)=>DefDatabase<LP_ClassDef>.GetNamed("LP_Class_"+n);
  static CasterListDef CsList(string n)=>DefDatabase<CasterListDef>.GetNamed("LP_CSL_"+n);
  Pawn CsPawn(string cls,int lvl,int score=14,int adv=-1){
   var p=ClsPawn(adv>0?adv:Mathf.Max(lvl,1));var h=Adventurer.Get(p);h.scores=new[]{10,10,10,score,score,score};h.Dirty();
   foreach(var s in new[]{SkillDefOf.Intellectual,SkillDefOf.Social,SkillDefOf.Plants,SkillDefOf.Animals,SkillDefOf.Crafting,SkillDefOf.Melee})p.skills.GetSkill(s).Level=10;
   ClassSystem.SetLevel(h,CsClass(cls),lvl);CasterSpells.Sync(h);p.apparel?.DestroyAll();return p;
  }
  static float CsHurt(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(x=>x.Severity);
  void CheckCasterSpells(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool oldOn=s.adventurer;s.adventurer=true;var made=new List<Thing>();
   try{
    // 1. 定义：新法术都是职业能力、有图标、环位、费用 = 法力表；都在法术表里；四张表都解析得开；每个职业每个环位至少 2 个法术、至少一个工具法术；说明里没有骰子 / 检定字样。
    var news=CasterSpells.AllNew.ToList();var lists=CasterSpells.Lists;
    var bad=news.Where(a=>!(a.abilityClass==typeof(Ability_Class) && a.uiIcon!=null && a.uiIcon!=BaseContent.BadTex && CasterSpells.CExt(a).circle>=0 && Fx(a.defName).cost==Spellbook.ManaFor(CasterSpells.CExt(a).circle) && lists.Any(l=>l.Table.ContainsKey(a)) && ClassSystem.ClassOf(a)!=null)).Select(a=>a.defName).ToList();
    var thin=new List<string>();foreach(var l in lists)for(int c=0;c<=9;c++){int n=l.Table.Count(kv=>kv.Value==c);if(n<2 && !(c==0 && !l.cantrips) && !(l.Half && c>5))thin.Add(l.defName+":"+c+"="+n);}
    var noUtil=lists.Where(l=>!l.Table.Keys.Any(a=>!a.hostile)).Select(l=>l.defName).ToList();
    var dice=new System.Text.RegularExpressions.Regex("d20|掷骰|检定|DC|豁免\\s*[+＋]");var diced=news.Where(a=>dice.IsMatch(a.description??"")).Select(a=>a.defName).ToList();
    int lcount=lists.Count(l=>new[]{"LP_CSL_Cleric","LP_CSL_Druid","LP_CSL_Sorcerer","LP_CSL_Warlock","LP_CSL_Bard","LP_CSL_Paladin","LP_CSL_Ranger","LP_CSL_Artificer"}.Contains(l.defName));var oldIcon=news.Where(a=>a.iconPath==null || !a.iconPath.StartsWith("LP/Classes/CasterSpells/")).Select(a=>a.defName).ToList();
    Check(news.Count>=118 && bad.Count==0 && thin.Count==0 && noUtil.Count==0 && diced.Count==0 && lcount==8 && oldIcon.Count==0 && lists.All(l=>l.Table.Count==l.spells.Count) && lists.Where(l=>l.Half).All(l=>l.Table.Values.All(c=>c>=(l.cantrips?0:1) && c<=5)),
     "[new "+news.Count+" oldIcon "+string.Join(",",oldIcon)+" bad "+string.Join(",",bad)+" thin "+string.Join(",",thin)+" noUtil "+string.Join(",",noUtil)+" dice "+string.Join(",",diced)+" lists "+lcount+"] caster spell defs (1.131.0: 118, all with new CasterSpells icons): class abilities with circle and mana-table cost, all listed; 8 class lists resolve (half casters circles 1-5 only); ≥2 spells per circle and a utility spell per class; no dice wording");
    // 2. 环位表：全施法者同法师；半施法者 2/5/9/13/17 级（器械师 1 级一环）；邪术师 1～5 环 + 秘法玄奥 11/13/15/17 级。
    CasterListDef cl=CsList("Cleric"),dl=CsList("Druid"),sl=CsList("Sorcerer"),wl=CsList("Warlock");
    var half=new CasterListDef{caster="half"};var art=new CasterListDef{caster="artificer"};
    bool tab=CasterSpells.MaxCircle(cl,1)==1 && CasterSpells.MaxCircle(cl,3)==2 && CasterSpells.MaxCircle(cl,17)==9 && CasterSpells.MaxCircle(half,1)==0 && CasterSpells.MaxCircle(half,2)==1 && CasterSpells.MaxCircle(half,5)==2 && CasterSpells.MaxCircle(half,17)==5
     && CasterSpells.MaxCircle(art,1)==1 && CasterSpells.MaxCircle(wl,17)==5 && CasterSpells.MaxCircle(wl,9)==5 && CasterSpells.ArcanumLevel(6)==11 && CasterSpells.ArcanumLevel(9)==17 && CasterSpells.LevelFor(half,3)==9 && CasterSpells.LevelFor(wl,7)==13;
    Check(tab,"circle tables: full casters like the wizard (1/3/…/17), half casters 2/5/9/13/17 (artificer 1st circle at 1), warlock pact magic up to circle 5 plus arcanum at 11/13/15/17");
    // 3. 牧师备法：3 级感知 14 → 可备 3 + 2 = 5；能备一、二环，不能备三环；只有备好的给按钮；满了不能再备；备完锁定，睡醒解锁；取消准备收回按钮。
    var cle=CsPawn("Cleric",3);made.Add(cle);GenSpawn.Spawn(cle,ScCell(0,6),map);var ch=Adventurer.Get(cle);var cb=CasterSpells.Book(ch,cl,false);
    var hw=Ab("LP_CS_HealingWord");int cap=CasterSpells.PrepCap(ch,cl);var prep=CasterSpells.Preparable(ch,cl).ToList();bool noThree=!prep.Any(a=>CasterSpells.CircleIn(cl,a)>=3) && prep.Contains(Ab("LP_CS_Aid"));
    bool before=!HasAb(cle,"LP_CS_HealingWord");bool on=CasterSpells.SetPrepared(ch,cl,hw,true);bool granted=HasAb(cle,"LP_CS_HealingWord") && !Spellbook.Hidden(cle,hw) && cb.granted.Contains(hw);
    foreach(var a in prep)if(CasterSpells.PreparedCount(ch,cl)<cap)CasterSpells.SetPrepared(ch,cl,a,true);string full=CasterSpells.CannotToggle(ch,cl,prep.First(a=>!cb.prepared.Contains(a) && CasterSpells.Counted(cl,a)),true);
    cb.mayPrepare=false;string locked=CasterSpells.CannotToggle(ch,cl,hw,false);cb.wasAsleep=true;CasterSpells.Tick250(ch);bool woke=cb.mayPrepare;
    CasterSpells.SetPrepared(ch,cl,Ab("LP_CS_Command"),false);bool removed=!HasAb(cle,"LP_CS_Command") && !cb.granted.Contains(Ab("LP_CS_Command"));bool ownBless=HasAb(cle,"LP_Bless") && CasterSpells.OwnAbility(ch,Ab("LP_Bless"));
    Check(cap==5 && noThree && before && on && granted && full!=null && full.Contains("准备满") && locked!=null && woke && removed && ownBless,"[cap "+cap+" prep "+prep.Count+" full "+full+" locked "+(locked!=null)+" woke "+woke+" removed "+removed+"] cleric prepares: 3 + WIS 14 mod 2 = 5; circles 1-2 only; prepared spells get buttons, the rest none; full list refuses; locked until waking; unpreparing takes the button away; own class abilities stay");
    // 4. 牧师施法：治疗真言扣 1 点神恩、治好友军、用神恩强度；牧师备上法师的解除魔法：算牧师、按三环收 2 点神恩。
    var ally=CsPawn("Cleric",1,10);made.Add(ally);GenSpawn.Spawn(ally,NextTo(cle),map);ally.TakeDamage(new DamageInfo(DamageDefOf.Cut,8f,0f,-1f,null));float hurt0=CsHurt(ally);
    ch.classes.res[ClassSystem.PoolKey(CsClass("Cleric"))]=ClassSystem.MaxResource(ch,CsClass("Cleric"));float g0=ClassSystem.Resource(ch,CsClass("Cleric"));
    var hwAb=cle.abilities.GetAbility(hw);bool cast=hwAb!=null && hwAb.Activate(new LocalTargetInfo(ally),new LocalTargetInfo(ally));float g1=ClassSystem.Resource(ch,CsClass("Cleric"));float hurt1=CsHurt(ally);
    var stat=CasterSpells.StatFor(cle,Fx("LP_CS_HealingWord"));
    ClassSystem.SetLevel(ch,CsClass("Cleric"),5);cb.mayPrepare=true;cb.prepared.Clear();CasterSpells.Sync(ch);var dm=Ab("LP_WS_DispelMagic");bool dmOn=CasterSpells.SetPrepared(ch,cl,dm,true);
    var dmCls=ClassSystem.CastClassOf(cle,dm);int dmCost=ClassLegendary.Cost(cle,dm,Fx("LP_WS_DispelMagic"));var dmStat=CasterSpells.StatFor(cle,Fx("LP_WS_DispelMagic"));
    Check(cast && Near(g0-g1,1f) && hurt1<hurt0 && stat?.defName=="LP_DivinePower" && dmOn && HasAb(cle,"LP_WS_DispelMagic") && dmCls==CsClass("Cleric") && dmCost==2 && dmStat?.defName=="LP_DivinePower" && !Spellbook.Hidden(cle,dm),
     "[cast "+cast+" grace "+g0+"→"+g1+" hurt "+hurt0.ToString("0.#")+"→"+hurt1.ToString("0.#")+" stat "+stat?.defName+" dm "+dmOn+" "+dmCls?.defName+" cost "+dmCost+"] cleric casts: healing word spends 1 grace, heals and uses divine power; a prepared wizard dispel magic counts as cleric, costs 2 grace (circle 3)");
    // 5. 德鲁伊：1 级 3 个戏法、第 4 个拒绝；荆棘鞭把敌人拉近；备上牧师的疗伤术（职业能力复用）算德鲁伊、扣德鲁伊法力；良莓术变出 10 颗良莓（一顿饭的营养）。
    var dru=CsPawn("Druid",1);made.Add(dru);GenSpawn.Spawn(dru,ScCell(-6,6),map);var dh=Adventurer.Get(dru);var db=CasterSpells.Book(dh,dl,false);
    bool c1=CasterSpells.LearnCantrip(dh,dl,Ab("LP_CS_ThornWhip")),c2=CasterSpells.LearnCantrip(dh,dl,Ab("LP_CS_ProduceFlame")),c3=CasterSpells.LearnCantrip(dh,dl,Ab("LP_CS_Druidcraft")),c4=CasterSpells.LearnCantrip(dh,dl,Ab("LP_CS_MoldEarth"));
    var foe=ScFoe(dru);made.Add(foe);foe.apparel?.DestroyAll();foe.Position=dru.Position+new IntVec3(5,0,0);foe.Notify_Teleported();float d0=foe.Position.DistanceTo(dru.Position);
    CasterEffects.Bolt(dru,Ab("LP_CS_ThornWhip"),CasterSpells.CExt(Ab("LP_CS_ThornWhip")),foe,7f,0,1f);float d1=foe.Dead?0f:foe.Position.DistanceTo(dru.Position);
    var cw=Ab("LP_CureWounds");bool cwOn=CasterSpells.SetPrepared(dh,dl,cw,true);var cwCls=ClassSystem.CastClassOf(dru,cw);dh.classes.res[CsClass("Druid")]=3f;
    var cwAb=dru.abilities.GetAbility(cw);var hurtD=CsPawn("Cleric",1,10);made.Add(hurtD);GenSpawn.Spawn(hurtD,NextTo(dru),map);hurtD.TakeDamage(new DamageInfo(DamageDefOf.Cut,8f,0f,-1f,null));
    bool cwCast=cwAb!=null && cwAb.Activate(new LocalTargetInfo(hurtD),new LocalTargetInfo(hurtD));float mana=ClassSystem.Resource(dh,CsClass("Druid"));
    var berries=CasterEffects.SpawnThings(dru,"LP_CS_Goodberry",10,dru.Position);made.AddRange(berries);int nb=berries.Sum(t=>t.stackCount);var bdef=DefCache<ThingDef>.Get("LP_CS_Goodberry");
    Check(c1 && c2 && c3 && !c4 && d1<d0 && cwOn && cwCls==CsClass("Druid") && cwCast && Near(mana,2f) && nb==10 && bdef!=null && Near(bdef.GetStatValueAbstract(StatDefOf.Nutrition),.9f),
     "[cantrips "+c1+c2+c3+c4+" pull "+d0.ToString("0.#")+"→"+d1.ToString("0.#")+" cure "+cwOn+" "+cwCls?.defName+" cast "+cwCast+" mana "+mana+" berries "+nb+"] druid: 3 cantrips at level 1; thorn whip pulls; the cleric's cure wounds prepared by a druid counts as druid and spends druid mana; goodberry makes 10 meal-sized berries");
    // 6. 术士已知法术：1 级 2 个、只能一环、第 3 个拒绝；2 级多 1 个；替换（每级 1 次）空出名额；隐藏后没有按钮。
    var sor=CsPawn("Sorcerer",1);made.Add(sor);GenSpawn.Spawn(sor,ScCell(6,6),map);var sh=Adventurer.Get(sor);var sb=CasterSpells.Book(sh,sl,false);
    int due1=CasterSpells.PicksDue(sh,sl);var cands=CasterSpells.PickCandidates(sh,sl).ToList();bool p1=CasterSpells.Pick(sh,sl,Ab("LP_ShieldSpell")),p2=CasterSpells.Pick(sh,sl,Ab("LP_WS_Thunderwave")),p3=CasterSpells.Pick(sh,sl,Ab("LP_Sleep"));
    bool known=HasAb(sor,"LP_ShieldSpell") && HasAb(sor,"LP_WS_Thunderwave") && ClassSystem.CastClassOf(sor,Ab("LP_ShieldSpell"))==CsClass("Sorcerer");string noSwap=CasterSpells.CannotForget(sh,sl,Ab("LP_ShieldSpell"));
    ClassSystem.SetLevel(sh,CsClass("Sorcerer"),2);int due2=CasterSpells.PicksDue(sh,sl);bool fg=CasterSpells.Forget(sh,sl,Ab("LP_WS_Thunderwave"));int due3=CasterSpells.PicksDue(sh,sl);bool gone=!HasAb(sor,"LP_WS_Thunderwave");string twice=CasterSpells.CannotForget(sh,sl,Ab("LP_ShieldSpell"));
    sb.hiddenSpells.Add(Ab("LP_ShieldSpell"));bool hid=Spellbook.Hidden(sor,Ab("LP_ShieldSpell")) && !sor.abilities.GetAbility(Ab("LP_ShieldSpell")).GetGizmos().Any();sb.hiddenSpells.Clear();
    Check(due1==2 && cands.All(a=>CasterSpells.CircleIn(sl,a)==1) && !cands.Contains(Ab("LP_BurningHands")) && p1 && p2 && !p3 && known && noSwap!=null && due2==1 && fg && due3==2 && gone && twice!=null && hid,
     "[due "+due1+"/"+due2+"/"+due3+" picks "+p1+p2+p3+" swap "+fg+" hid "+hid+"] sorcerer known spells: 2 at level 1 (circle 1, own class abilities not offered), a 3rd refused; +1 at level 2; one swap per level frees a pick; hidden spells lose their button");
    // 7. 超魔：术士 3 级得到超魔·穿透、5 级扩展；穿透让控制成功率 +25%，放下一个法术就用掉；写进已知的火球术算术士、扣 2 点术士法力、用天生强度。
    ClassSystem.SetLevel(sh,CsClass("Sorcerer"),5);CasterSpells.Sync(sh);bool meta=HasAb(sor,"LP_CS_MetaPierce") && HasAb(sor,"LP_CS_MetaExtend") && !HasAb(sor,"LP_CS_MetaQuicken") && CasterSpells.KnownCount(sh,sl)==1;
    var foe2=ScFoe(sor);made.Add(foe2);float ch0=WizardSpells.Chance(.5f,sor,foe2);Items.Give(sor,"LP_CS_MetaPierce",1800,1f);float ch1=WizardSpells.Chance(.5f,sor,foe2);
    CasterSpells.Pick(sh,sl,Ab("LP_Fireball"));var fbAb=sor.abilities.GetAbility(Ab("LP_Fireball")) as Ability_Class;CasterSpells.AfterCast(fbAb);bool used=!Items.Has(sor,"LP_CS_MetaPierce");
    sh.classes.res[CsClass("Sorcerer")]=5f;bool fbCast=fbAb!=null && fbAb.Activate(new LocalTargetInfo(ScCell(20,20)),new LocalTargetInfo(ScCell(20,20)));float sm=ClassSystem.Resource(sh,CsClass("Sorcerer"));
    Check(meta && Near(ch1-ch0,.25f) && used && fbCast && Near(sm,3f) && ClassSystem.CastClassOf(sor,Ab("LP_Fireball"))==CsClass("Sorcerer") && CasterSpells.StatFor(sor,Fx("LP_Fireball"))?.defName=="LP_InnatePower",
     "[meta "+meta+" chance "+ch0.ToString("0.##")+"→"+ch1.ToString("0.##")+" used "+used+" cast "+fbCast+" mana "+sm+"] metamagic: pierce at 3 / extend at 5 (not counted as known); pierce adds 25% control chance and is consumed by the next spell; a known fireball casts as sorcerer for 2 mana with innate power");
    // 8. 邪术师：契约法力；7 级能选四环的次元门（按四环收 2 点）；11 级一个秘法玄奥待选，选目光术：不花资源、每天 1 次。
    var wa=CsPawn("Warlock",7);made.Add(wa);GenSpawn.Spawn(wa,ScCell(-6,12),map);var wh=Adventurer.Get(wa);var wb=CasterSpells.Book(wh,wl,false);
    var dd=Ab("LP_WS_DimensionDoor");bool ddPick=CasterSpells.Pick(wh,wl,dd);int ddCircle=CasterSpells.CircleFor(wa,dd),ddCost=ClassLegendary.Cost(wa,dd,Fx("LP_WS_DimensionDoor"));int arc7=CasterSpells.ArcanumDue(wh,wl);
    ClassSystem.SetLevel(wh,CsClass("Warlock"),11);int arc11=CasterSpells.ArcanumDue(wh,wl);var eb=Ab("LP_CS_Eyebite");string tooHigh=CasterSpells.CannotArcanum(wh,wl,Ab("LP_WS_ForceCage"));bool arcOk=CasterSpells.PickArcanum(wh,wl,eb);
    int ebCost=ClassLegendary.Cost(wa,eb,Fx("LP_CS_Eyebite"));int ebCap=Spellbook.DailyCap(wa,eb);string free=ClassSystem.CannotCast(wa,eb,Fx("LP_CS_Eyebite"));Spellbook.UseDaily(wa,eb);string spent=ClassSystem.CannotCast(wa,eb,Fx("LP_CS_Eyebite"));
    Check(ddPick && ddCircle==4 && ddCost==2 && arc7==0 && arc11==1 && tooHigh!=null && arcOk && HasAb(wa,"LP_CS_Eyebite") && ebCost==0 && ebCap==1 && free==null && spent!=null && spent.Contains("次数"),
     "[dd "+ddPick+" circle "+ddCircle+" cost "+ddCost+" arc "+arc7+"/"+arc11+" eyebite "+arcOk+" cost "+ebCost+" cap "+ebCap+" spent "+spent+"] warlock: dimension door at circle 4 for 2 pact mana; arcanum at 11 (7th-circle refused), eyebite free and once a day");
    // 9. 伤害法术：亡者丧钟对伤者加倍；焰击术只打敌人；龙息术锥形；神罚之柱眩晕。
    var foe3=ScFoe(cle);made.Add(foe3);foe3.apparel?.DestroyAll();foreach(var hi in foe3.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())foe3.health.RemoveHediff(hi);bool fresh=!CasterEffects.Matches("Wounded",foe3);foe3.TakeDamage(new DamageInfo(DamageDefOf.Cut,2f,0f,-1f,null));bool wounded=CasterEffects.Matches("Wounded",foe3);
    float h3=CsHurt(foe3);var fsx=CasterSpells.CExt(Ab("LP_CS_FlameStrike"));int hits=CasterEffects.Area(cle,Ab("LP_CS_FlameStrike"),fsx,foe3.Position,2.5f,6f,0);float h4=foe3.Dead?999f:CsHurt(foe3);
    float allyH=CsHurt(ally);var foe4=ScFoe(sor);made.Add(foe4);foe4.apparel?.DestroyAll();foe4.Position=sor.Position+new IntVec3(3,0,0);foe4.Notify_Teleported();
    int cone=CasterEffects.Cone(sor,Ab("LP_CS_DragonsBreath"),CasterSpells.CExt(Ab("LP_CS_DragonsBreath")),foe4.Position,6f,5f,0);
    var foe5=ScFoe(cle);made.Add(foe5);CasterEffects.Area(cle,Ab("LP_CS_DivineColumn"),CasterSpells.CExt(Ab("LP_CS_DivineColumn")),foe5.Position,1f,1f,0);bool stunned=foe5.Dead || foe5.stances.stunner.Stunned;
    Check(fresh && wounded && hits>=1 && h4>h3 && cone>=1 && stunned && CasterEffects.Breath(sor)==DamageDefOf.Flame,"[wounded "+fresh+wounded+" area hits "+hits+" hurt "+h3.ToString("0.#")+"→"+h4.ToString("0.#")+" ally "+allyH.ToString("0.#")+" cone "+cone+" stun "+stunned+"] damage spells: toll the dead knows wounded targets; flame strike hits foes; dragon's breath cone (fire without a bloodline); divine column stuns");
    // 10. 控制：命令术成功 / 失败；强力震慑只对伤势过半的敌人生效。
    WizardSpells.RollOverride=0f;var foe6=ScFoe(cle);made.Add(foe6);int cmd=CasterEffects.Hex(cle,Ab("LP_CS_Command"),Fx("LP_CS_Command"),CasterSpells.CExt(Ab("LP_CS_Command")),foe6,foe6.Position,0f,600,1f,0);
    WizardSpells.RollOverride=.99f;var foe7=ScFoe(cle);made.Add(foe7);int cmdFail=CasterEffects.Hex(cle,Ab("LP_CS_Command"),Fx("LP_CS_Command"),CasterSpells.CExt(Ab("LP_CS_Command")),foe7,foe7.Position,0f,600,1f,0);WizardSpells.RollOverride=null;
    var pws=CasterSpells.CExt(Ab("LP_CS_PowerWordStun"));var foe8=ScFoe(wa);made.Add(foe8);int pw1=CasterEffects.Hex(wa,Ab("LP_CS_PowerWordStun"),Fx("LP_CS_PowerWordStun"),pws,foe8,foe8.Position,0f,0,1f,0);
    Check(cmd==1 && cmdFail==0 && pw1==0 && !foe8.stances.stunner.Stunned,"[command "+cmd+"/"+cmdFail+" pws "+pw1+"] control: command lands on a good roll and fails on a bad one; power word stun ignores a healthy foe");
    // 11. 区域与专注（没有法师法术书的人）：荆棘丛生登记在德鲁伊的法术表数据里，受 12 点伤害维持失败就收掉区域；静默术里放不了法术。
    ClassSystem.SetLevel(dh,CsClass("Druid"),3);CasterSpells.Sync(dh);var sg=Ab("LP_CS_SpikeGrowth");var z=CasterEffects.Zone(dru,sg,Fx("LP_CS_SpikeGrowth"),CasterSpells.CExt(sg),ScCell(-12,6),3f,3600,4f);if(z!=null)made.Add(z);
    bool concOn=Spellbook.ConcBook(dru)==db && db.conc==sg && Spellbook.Concentrating.Contains(dru);Spellbook.RollOverride=.99f;bool broke=Spellbook.Damaged(dru,12f) && (z==null || z.Destroyed) && db.conc==null;Spellbook.RollOverride=null;
    var sil=SpellZone.Make(cle,cle.Position,"CS_Silence",3f,3600);if(sil!=null)made.Add(sil);CasterEffects.ZoneTick(sil);string silenced=ClassSystem.CannotCast(cle,hw,Fx("LP_CS_HealingWord"));sil?.Destroy();Items.Remove(cle,"LP_CS_Silenced");
    Check(z!=null && z.kind=="CS_Spikes" && concOn && broke && silenced!=null && silenced.Contains("静默"),"[zone "+z?.kind+" conc "+concOn+" broke "+broke+" silence "+silenced+"] zones and concentration for non-wizards: spike growth concentrates in the druid book and ends when concentration breaks; silence blocks casting");
    // 12. 支援：援助术的临时生命先挡伤害；自由行动解除减速；蛛行术无视地形；虔诚护盾加护甲。
    var pre=AccessTools.Method(typeof(CasterSpellDamage),"Prefix");CasterEffects.TempHp(ally,10f,2500);
    var a1=new object[]{ally,new DamageInfo(DamageDefOf.Cut,6f,0f,-1f,foe3),false};pre.Invoke(null,a1);var a2=new object[]{ally,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,foe3),false};pre.Invoke(null,a2);
    bool temp=(bool)a1[2] && Near(((DamageInfo)a2[1]).Amount,6f);Items.Give(ally,"LP_WS_Slowed",600,1f);Items.Give(ally,"LP_CS_Freedom",2500,1f);bool freed=!Items.Has(ally,"LP_WS_Slowed");
    Items.Give(ally,"LP_CS_SpiderClimb",2500,1f);bool climb=Spellbook.Fliers.Contains(ally);Items.Remove(ally,"LP_CS_SpiderClimb");bool climbOff=!Spellbook.Fliers.Contains(ally);
    float ar0=ally.GetStatValue(StatDefOf.ArmorRating_Sharp);Items.Give(ally,"LP_CS_ShieldOfFaith",2500,1f);float ar1=ally.GetStatValue(StatDefOf.ArmorRating_Sharp);
    Check(temp && freed && climb && climbOff && ar1-ar0>.15f,"[temp "+temp+" freed "+freed+" climb "+climb+climbOff+" armor "+ar0.ToString("0.##")+"→"+ar1.ToString("0.##")+"] support: aid's temporary HP absorbs first; freedom of movement clears slows; spider climb ignores terrain; shield of faith adds armor");
    // 13. 复活与复原：复苏术（1 天内）、死者复活（超过 10 天不行）、高等复原术去掉旧伤疤、移除诅咒去掉咒术。
    var dead=ScFoe(cle);dead.Kill(null);var corpse=dead.Corpse;var rv=CasterSpells.CExt(Ab("LP_CS_Revivify"));string rvWhy=CasterEffects.ReviveCannot(corpse,rv);bool rvOk=CasterEffects.Revive(cle,rv,corpse) && !dead.Dead;made.Add(dead);
    var dead2=ScFoe(cle);dead2.Kill(null);var corpse2=dead2.Corpse;made.Add(corpse2);corpse2.timeOfDeath=Find.TickManager.TicksGame-11*60000;string old=CasterEffects.ReviveCannot(corpse2,CasterSpells.CExt(Ab("LP_CS_RaiseDead")));
    var scarT=CsPawn("Cleric",1,10);made.Add(scarT);GenSpawn.Spawn(scarT,NextTo(cle),map);foreach(var hi in scarT.health.hediffSet.hediffs.Where(x=>x is Hediff_Injury || x.def.isBad && x.def.chronic).ToList())scarT.health.RemoveHediff(hi);var part=scarT.RaceProps.body.AllParts.First(x=>x.def.defName=="Torso");var scar=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,scarT,part);scar.Severity=3f;scar.TryGetComp<HediffComp_GetsPermanent>().IsPermanent=true;scarT.health.AddHediff(scar);
    bool perm=scar.IsPermanent();string gr=CasterEffects.Restore(cle,"greater",scarT);bool scarGone=!scarT.health.hediffSet.hediffs.Contains(scar);
    Items.Give(scarT,"LP_WL_Hexed",2500,1f);CasterEffects.Restore(cle,"curse",scarT);bool hexGone=!Items.Has(scarT,"LP_WL_Hexed");
    Check(rvWhy==null && rvOk && old!=null && old.Contains("10 天") && perm && scarGone && hexGone,"[revive "+rvWhy+" "+rvOk+" old "+old+" perm "+perm+" restore "+gr+" hex "+hexGone+"] revive & restore: revivify a fresh corpse; raise dead refuses an 11-day-old one; greater restoration removes a scar; remove curse lifts hex");
    // 14. 造物与工具：造粮术 15 份简单餐；召唤林地生物 2 个（召唤物）；塑石术立石墙、塑土术挖开岩石；造水术灭火。
    var meals=CasterEffects.SpawnThings(cle,"MealSimple",15,cle.Position);made.AddRange(meals);int nm=meals.Sum(t=>t.stackCount);
    var wood=CasterEffects.Summon(dru,Ab("LP_CS_ConjureWoodland"),CasterSpells.CExt(Ab("LP_CS_ConjureWoodland")),dru.Position,2,2500);made.AddRange(wood);bool woodOk=wood.Count==2 && wood.All(x=>x.Faction==Faction.OfPlayer && Items.Has(x,"LP_WS_Summoned"));
    var wc=ScCell(-14,14);foreach(var t in wc.GetThingList(map).ToList())if(!(t is Pawn))t.Destroy();var wall=CasterEffects.StoneShape(dru,wc);if(wall!=null)made.Add(wall);bool walled=wall!=null && wc.GetEdifice(map)!=null;
    var rc=ScCell(-14,16);foreach(var t in rc.GetThingList(map).ToList())if(!(t is Pawn))t.Destroy();var rock=GenSpawn.Spawn(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Granite")),rc,map);string dug=CasterEffects.MoldEarth(dru,rc);bool rockGone=rc.GetEdifice(map)==null;
    foreach(var t in rc.GetThingList(map).ToList())if(t.def.category==ThingCategory.Item)made.Add(t);
    var fc=ScCell(-16,10);var fire=(Fire)ThingMaker.MakeThing(ThingDefOf.Fire);fire.fireSize=.5f;GenSpawn.Spawn(fire,fc,map);int put=CasterEffects.CreateWater(cle,fc,3f);bool noFire=!GenRadial.RadialDistinctThingsAround(fc,map,3f,true).OfType<Fire>().Any();
    Check(nm==15 && woodOk && walled && dug=="dig" && rockGone && put>=1 && noFire,"[meals "+nm+" woodland "+wood.Count+" wall "+walled+" dig "+dug+" fire "+put+"] utility: create food 15 simple meals; woodland beings summoned for an hour; stone shape raises a wall, mold earth digs rock; create water puts out fire");
    // 15. NPC 不建法术表：不属于玩家的牧师没有法术表数据，也拿不到法术表里的法术。
    var npc=CsPawn("Cleric",5);made.Add(npc);npc.SetFaction(AdvEnemy);var nh=Adventurer.Get(npc);nh.classes.casterBooks.Clear();int nn=CasterSpells.Sync(nh);
    Check(nn==0 && nh.classes.casterBooks.Count==0 && !HasAb(npc,"LP_CS_HealingWord"),"NPC casters get no spell list data and none of the new spells");
    // 16. 圣武士（备法半施法者）：1 级没有法术、2 级一环；炽焰斩下一次近战命中附加火焰并用掉；魔法武器每次命中（远程也算）都附加、不用掉；闪电箭只对远程。
    var pl=CsList("Paladin");var pal=CsPawn("Paladin",1);made.Add(pal);GenSpawn.Spawn(pal,ScCell(10,10),map);var ph=Adventurer.Get(pal);
    int prep1=CasterSpells.Preparable(ph,pl).Count();int cap1=CasterSpells.PrepCap(ph,pl);ClassSystem.SetLevel(ph,CsClass("Paladin"),2);CasterSpells.Sync(ph);bool blazeOn=CasterSpells.SetPrepared(ph,pl,Ab("LP_CS_SmiteBlaze"),true);
    var foe9=ScFoe(pal);made.Add(foe9);foe9.apparel?.DestroyAll();float f9=CsHurt(foe9);CasterEffects.Imbue(pal,Ab("LP_CS_SmiteBlaze"),Fx("LP_CS_SmiteBlaze"),pal,600,6f);int hit1=CasterEffects.OnHitFire(pal,foe9,true);bool used1=!Items.Has(pal,"LP_CS_SmiteBlaze");float f9b=foe9.Dead?999f:CsHurt(foe9);
    CasterEffects.Imbue(pal,Ab("LP_CS_MagicWeapon"),Fx("LP_CS_MagicWeapon"),pal,2500,2f);int hit2=foe9.Dead?1:CasterEffects.OnHitFire(pal,foe9,false);bool kept=Items.Has(pal,"LP_CS_MagicWeapon");Items.Remove(pal,"LP_CS_MagicWeapon");
    CasterEffects.Imbue(pal,Ab("LP_CS_LightningArrow"),Fx("LP_CS_LightningArrow"),pal,600,2f);int hit3=foe9.Dead?0:CasterEffects.OnHitFire(pal,foe9,true);bool arrowKept=Items.Has(pal,"LP_CS_LightningArrow");Items.Remove(pal,"LP_CS_LightningArrow");
    Check(prep1==0 && cap1==1+2 && blazeOn && HasAb(pal,"LP_CS_SmiteBlaze") && !pl.cantrips && hit1==1 && used1 && f9b>f9 && hit2==1 && kept && hit3==0 && arrowKept,
     "[prep1 "+prep1+" cap "+cap1+" blaze "+blazeOn+" hits "+hit1+"/"+hit2+"/"+hit3+" used "+used1+" kept "+kept+" hurt "+f9.ToString("0.#")+"→"+f9b.ToString("0.#")+"] paladin: no spells at 1, circle 1 at 2 (prep cap ceil(lvl/2)+CHA); blazing smite fires once on a melee hit; magic weapon on every hit incl. ranged; lightning arrow ignores melee");
    // 17. 游侠（已知半施法者，法术能量）：1 级没有可选、2 级一环；诱捕打击花 1 点法术能量（上限 2 + 等级），不够就放不了，睡醒回满；悬停写法术能量。
    var rl=CsList("Ranger");var rng=CsPawn("Ranger",1);made.Add(rng);GenSpawn.Spawn(rng,ScCell(12,6),map);var rh=Adventurer.Get(rng);int rc1=CasterSpells.PickCandidates(rh,rl).Count();
    ClassSystem.SetLevel(rh,CsClass("Ranger"),2);CasterSpells.Sync(rh);bool ens=CasterSpells.Pick(rh,rl,Ab("LP_CS_Ensnaring"));var rb=CasterSpells.Book(rh,rl,false);var ensAb=rng.abilities.GetAbility(Ab("LP_CS_Ensnaring"));
    bool rCast=ensAb!=null && ensAb.Activate(new LocalTargetInfo(rng),new LocalTargetInfo(rng));float rSpent=rb.energy;string tip=CasterSpells.CostTip(rng,Ab("LP_CS_Ensnaring"),Fx("LP_CS_Ensnaring"));
    rb.energy=4f;string empty=ClassSystem.CannotCast(rng,Ab("LP_CS_Ensnaring"),Fx("LP_CS_Ensnaring"));rb.wasAsleep=true;CasterSpells.Tick250(rh);float refill=CasterSpells.EnergyLeft(rh,rl);
    Check(rc1==0 && ens && rCast && Near(rSpent,1f) && Items.Has(rng,"LP_CS_Ensnaring") && tip!=null && tip.Contains("法术能量") && empty!=null && empty.Contains("法术能量") && Near(refill,4f),
     "[cands1 "+rc1+" pick "+ens+" cast "+rCast+" spent "+rSpent+" tip "+tip+" empty "+empty+" refill "+refill+"] ranger: nothing to learn at 1, circle 1 at 2; ensnaring strike spends 1 spell energy (max 2 + level), refused when empty, refilled on waking");
    // 18. 器械师：1 级就有一环、戏法 3 个、可备 1 + 智力调整值；造物术 等级 × 10；吸收元素把火焰伤害减半。
    var al=CsList("Artificer");var artP=CsPawn("Artificer",1);made.Add(artP);GenSpawn.Spawn(artP,ScCell(14,4),map);var arh=Adventurer.Get(artP);
    bool artOk=CasterSpells.Preparable(arh,al).Contains(Ab("LP_CS_AbsorbElements")) && CasterSpells.PrepCap(arh,al)==3 && CasterSpells.CantripCap(arh,al)==3 && CasterEffects.CreationAmount(artP,"Steel")==10;
    CasterEffects.Imbue(artP,Ab("LP_CS_AbsorbElements"),Fx("LP_CS_AbsorbElements"),artP,600,8f);var ap=new object[]{artP,new DamageInfo(DamageDefOf.Flame,10f,0f,-1f,foe9),false};AccessTools.Method(typeof(CasterSpellDamage),"Prefix").Invoke(null,ap);float absorbed=((DamageInfo)ap[1]).Amount;
    Check(artOk && Near(absorbed,5f),"[ok "+artOk+" flame "+absorbed+"] artificer: circle 1 at level 1, 3 cantrips, prep 1 + INT mod; creation makes level × 10; absorb elements halves elemental damage");
    // 19. 吟游诗人：已知法术；10 级 2 个秘法奥秘（任意职业的表），14 级 4 个；学来的复苏术算吟游诗人、按三环扣 2 点乐韵、用激昂强度。
    var bl=CsList("Bard");var bd=CsPawn("Bard",10);made.Add(bd);GenSpawn.Spawn(bd,ScCell(-10,12),map);var bh=Adventurer.Get(bd);
    int sc10=CasterSpells.SecretCap(bh,bl);var scands=CasterSpells.SecretCandidates(bh,bl).ToList();var rev=Ab("LP_CS_Revivify");bool sp1=CasterSpells.PickSecret(bh,bl,rev);
    var revCls=ClassSystem.CastClassOf(bd,rev);int revCost=ClassLegendary.Cost(bd,rev,Fx("LP_CS_Revivify"));var revStat=CasterSpells.StatFor(bd,Fx("LP_CS_Revivify"));ClassSystem.SetLevel(bh,CsClass("Bard"),14);int sc14=CasterSpells.SecretCap(bh,bl);
    Check(sc10==2 && scands.Contains(rev) && scands.Contains(Ab("LP_WS_ConeOfCold")) && !scands.Any(a=>CasterSpells.AnyCircle(a)>5) && sp1 && HasAb(bd,"LP_CS_Revivify") && revCls==CsClass("Bard") && revCost==2 && revStat?.defName=="LP_BardicPower" && sc14==4,
     "[cap "+sc10+"/"+sc14+" cands "+scands.Count+" pick "+sp1+" "+revCls?.defName+" cost "+revCost+" stat "+revStat?.defName+"] bard magical secrets: 2 at 10, 4 at 14, from any class list up to circle 5; a secret revivify casts as bard for 2 melody with bardic power");
    // 20. 吟游诗人 / 游侠工具：高等隐形施法也不现形；英雄气概补临时生命；迷惑术把敌人推开；无踪步让远程攻击落空；寻获坐骑召来认主的战马，第二匹拒绝。
    var gi=Items.Give(ally,"LP_CS_GreaterInvisible",600,1f);Spellbook.AfterCast(ally.abilities.abilities.OfType<Ability_Class>().FirstOrDefault());bool stillInv=Items.Has(ally,"LP_CS_GreaterInvisible") && (gi as HediffWithComps)?.TryGetComp<HediffComp_Invisibility>()!=null;Items.Remove(ally,"LP_CS_GreaterInvisible");
    Items.Remove(ally,"LP_CS_TempHP");var hero=Items.Give(ally,"LP_CS_Heroism",600,1f) as Hediff_CS;hero.power=6f;CasterEffects.HediffTick(hero);var tmp=ally.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_CS_TempHP")) as Hediff_CS;
    WizardSpells.RollOverride=0f;var foe10=ScFoe(bd);made.Add(foe10);float dd0=foe10.Position.DistanceTo(bd.Position);int comp=CasterEffects.Hex(bd,Ab("LP_CS_Compulsion"),Fx("LP_CS_Compulsion"),CasterSpells.CExt(Ab("LP_CS_Compulsion")),null,bd.Position,4f,0,1f,0);float dd1=foe10.Position.DistanceTo(bd.Position);WizardSpells.RollOverride=null;
    Items.Give(ally,"LP_CS_PassTrace",600,1f);CasterEffects.RollOverride=0f;var pt=new object[]{ally,new DamageInfo(DamageDefOf.Bullet,8f,0f,-1f,foe10),false};AccessTools.Method(typeof(CasterSpellDamage),"Prefix").Invoke(null,pt);CasterEffects.RollOverride=null;bool missed=(bool)pt[2];
    ClassSystem.SetLevel(ph,CsClass("Paladin"),5);CasterSpells.Sync(ph);var steed=CasterEffects.Steed(pal,Ab("LP_CS_FindSteed"));if(steed!=null)made.Add(steed);string second=ClassEffects.Invalid(pal,Fx("LP_CS_FindSteed"),new LocalTargetInfo(pal));
    Check(stillInv && tmp!=null && Near(tmp.pool,6f) && comp>=1 && missed && steed!=null && steed.Faction==Faction.OfPlayer && pal.relations.DirectRelationExists(PawnRelationDefOf.Bond,steed) && Items.Has(steed,"LP_CS_Steed") && second!=null,
     "[inv "+stillInv+" temp "+tmp?.pool+" compel "+comp+" "+dd0.ToString("0.#")+"→"+dd1.ToString("0.#")+" miss "+missed+" steed "+(steed!=null)+" fac "+steed?.Faction?.IsPlayer+" bond "+(steed!=null && pal.relations.DirectRelationExists(PawnRelationDefOf.Bond,steed))+" hed "+(steed!=null && Items.Has(steed,"LP_CS_Steed"))+" second "+second+"] greater invisibility survives casting; heroism tops up temp HP; compulsion pushes foes away; pass without trace dodges a shot; find steed gives a bonded colony horse (horses cannot take a master), only one");
   }finally{
    WizardSpells.RollOverride=null;Spellbook.RollOverride=null;CasterEffects.RollOverride=null;
    foreach(var t in made)if(t!=null && !t.Destroyed){if(t is Pawn p)p.GetLord()?.RemovePawn(p);t.Destroy();}foreach(var p in advMade)if(!p.Destroyed)p.Destroy();advMade.Clear();
    s.adventurer=oldOn;Adventurer.TestSuppressed=sup;
   }
  }
  void PrepareCasterSpellsSave(){
   var s=LichMod.Settings;s.adventurer=true;
   var p=CsPawn("Warlock",11);var h=Adventurer.Get(p);var l=CsList("Warlock");CasterSpells.LearnCantrip(h,l,Ab("LP_CS_ChillTouch"));CasterSpells.Pick(h,l,Ab("LP_CS_ArmsOfHadar"));CasterSpells.PickArcanum(h,l,Ab("LP_CS_Eyebite"));
   ClassSystem.SetLevel(h,CsClass("Cleric"),3);CasterSpells.Sync(h);var cl=CsList("Cleric");CasterSpells.SetPrepared(h,cl,Ab("LP_CS_HealingWord"),true);CasterSpells.Book(h,cl,false).mayPrepare=false;
   Spellbook.UseDaily(p,Ab("LP_CS_Eyebite"));
   GenSpawn.Spawn(p,altar.InteractionCell+IntVec3.North*6,map);csSavedId=p.ThingID;advMade.Remove(p);
  }
  void CheckCasterSpellsAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==csSavedId);var h=Adventurer.Get(p);var wb=CasterSpells.Book(h,CsList("Warlock"),false);var cb=CasterSpells.Book(h,CsList("Cleric"),false);
   Check(wb!=null && cb!=null && wb.cantrips.Contains(Ab("LP_CS_ChillTouch")) && wb.book.Contains(Ab("LP_CS_ArmsOfHadar")) && wb.arcanum.Contains(Ab("LP_CS_Eyebite")) && cb.prepared.Contains(Ab("LP_CS_HealingWord")) && !cb.mayPrepare
    && HasAb(p,"LP_CS_HealingWord") && HasAb(p,"LP_CS_Eyebite") && Spellbook.UsedToday(p,Ab("LP_CS_Eyebite"))==1 && cb.granted.Contains(Ab("LP_CS_HealingWord")),"caster spell lists survive save/load: cantrip, known spell, arcanum, prepared spell, prepare lock, granted buttons, daily arcanum use");
   p?.Destroy();
  }
 }
}
