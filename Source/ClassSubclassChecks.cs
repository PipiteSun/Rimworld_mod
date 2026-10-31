using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 子职业 + 通用能力（Test.ps1 -Only Classes，10 条 + 读档 1 条）。
 public partial class LichChecks {
  static string subSavedId,subSavedAnimal;
  static LP_SubclassDef Sub(string n)=>DefDatabase<LP_SubclassDef>.GetNamed(n);
  bool ScFree(IntVec3 c)=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null && c.GetEdifice(map)==null;
  IntVec3 ScCell(int dx,int dz){
   var root=(altar.InteractionCell+new IntVec3(dx,0,dz)).ClampInsideMap(map);
   bool Ok(IntVec3 c)=>ScFree(c) && GenAdj.CellsAdjacent8Way(new TargetInfo(c,map)).Count(ScFree)>=6;
   for(int r=4;r<=30;r+=6)if(CellFinder.TryFindRandomCellNear(root,map,r,Ok,out var c))return c;
   return CellFinder.RandomClosewalkCellNear(altar.InteractionCell,map,20,ScFree);
  }
  IntVec3 NextTo(Pawn p){foreach(var c in GenAdj.CellsAdjacent8Way(p))if(ScFree(c))return c;return CellFinder.RandomClosewalkCellNear(p.Position,map,2,c=>c!=p.Position && ScFree(c));}
  Pawn ScSpawn(Pawn p,IntVec3 c){GenSpawn.Spawn(p,c,map);return p;}
  Pawn ScFoe(Pawn near){var f=AdvPawn(null,AdvEnemy);GenSpawn.Spawn(f,NextTo(near),map);f.mindState.enemyTarget=null;f.mindState.lastHarmTick=-99999;f.mindState.lastAttackTargetTick=-99999;return f;}
  void CheckSubclasses(){
   var made=new List<Thing>();
   try{
    // 1. 定义：9 个子职业（每个职业 3 个，徽记，能力在 3/7/11/15）、3 组通用（7/11）；45 个能力（Ability_Class、图标、所属职业）。
    var subs=ClassSubclass.All.Where(s=>s.parent==Barb || s.parent==Rang || s.parent==Rog).ToList();/* 邪术师的宗主在 CheckWarlock */var gens=subs.Where(s=>s.general).ToList();var opts=subs.Where(s=>!s.general).ToList();var abs=subs.SelectMany(s=>s.abilities).ToList();
    Check(gens.Count==3 && opts.Count==9 && new[]{Barb,Rang,Rog}.All(c=>ClassSubclass.Options(c).Count()==3 && ClassSubclass.General(c)!=null)
     && opts.All(s=>s.Emblem!=null && s.Emblem!=s.parent.Background && s.abilities.Select(e=>e.level).Distinct().OrderBy(x=>x).SequenceEqual(new[]{3,7,11,15}))
     && gens.All(s=>s.abilities.Select(e=>e.level).OrderBy(x=>x).SequenceEqual(new[]{7,11})) && abs.Count==45 && abs.Select(e=>e.ability).Distinct().Count()==45
     && abs.All(e=>e.ability.abilityClass==typeof(Ability_Class) && e.ability.uiIcon!=null && e.ability.uiIcon!=BaseContent.BadTex && ClassSystem.ClassOf(e.ability)==ClassSubclass.SubOf(e.ability).parent)
     && DefDatabase<ThingDef>.GetNamed("LP_ThornTrapThing").graphic!=null && DefDatabase<ThingDef>.GetNamed("LP_ThornTrapThing").graphic.MatSingle.mainTexture!=BaseContent.BadTex,
     "subclasses: 9 (3 per class, emblems, abilities at 3/7/11/15) + 3 general sets (7/11); 45 abilities with icons that count as their class");
    // 2. 选择门槛：野蛮人 2 级不能选；3 级能选狂战士（立刻得到狂乱）；选定后不能换、不能选别的职业的子职业。
    var b=Cls3Pawn(5,Barb,2,8);var hb=Adventurer.Get(b);var bers=Sub(ClassSubclass.Berserker);string w2=ClassSubclass.CanPick(hb,bers);bool need2=ClassSubclass.NeedsPick(hb,Barb);
    ClassSystem.AddLevel(hb,Barb);bool need3=ClassSubclass.NeedsPick(hb,Barb) && ClassSubclass.Pending(hb)==1;bool picked=ClassSubclass.Pick(hb,bers);
    Check(w2!=null && !need2 && need3 && picked && HasAb(b,"LP_Frenzy") && !HasAb(b,"LP_MindlessRage") && ClassSubclass.CanPick(hb,Sub(ClassSubclass.Totem))!=null && ClassSubclass.CanPick(hb,Sub(ClassSubclass.Hunter))!=null
     && !ClassSubclass.NeedsPick(hb,Barb) && ClassSubclass.S(b,ClassSubclass.SFrenzy),"pick gate: barbarian 2 refused ("+w2+"), 3 picks 狂战士之道 → 狂乱 at once; no second pick, no other class's subclass");
    // 3. 发放：风暴先驱 6 级只有 3 级能力；7 级得到风暴之魂 + 野性直觉（通用）；15 级四个子职业能力 + 两个通用都有。
    var st=Cls3Pawn(15,Barb,6,8);var hs=Adventurer.Get(st);ClassSubclass.Pick(hs,Sub(ClassSubclass.Storm));bool at6=HasAb(st,"LP_StormAura") && !HasAb(st,"LP_StormSoul") && !HasAb(st,"LP_FeralInstinct");
    ClassSystem.AddLevel(hs,Barb);bool at7=HasAb(st,"LP_StormSoul") && HasAb(st,"LP_FeralInstinct") && !HasAb(st,"LP_RecklessCharge") && !HasAb(st,"LP_ShieldingStorm");
    for(int i=0;i<8;i++)ClassSystem.AddLevel(hs,Barb);
    Check(at6 && at7 && ClassSystem.Level(hs,Barb)==15 && new[]{"LP_StormAura","LP_StormSoul","LP_ShieldingStorm","LP_RagingStorm","LP_FeralInstinct","LP_RecklessCharge"}.All(n=>HasAb(st,n)),"grants: storm herald 6 has only the level-3 ability; 7 adds 风暴之魂 + 野性直觉 (general); 15 has all four + both general");
    // 4. 分支：图腾勇士选完就要选图腾（熊），选定不能改；猎人 3 级只有猎手之选，防御战术 7 级才能选。
    var tw=Cls3Pawn(3,Barb,3,8);var ht=Adventurer.Get(tw);ClassSubclass.Pick(ht,Sub(ClassSubclass.Totem));int pendT=ClassSubclass.Pending(ht);bool c0=ClassSubclass.Choose(ht,"totem",0);bool c1=ClassSubclass.Choose(ht,"totem",1);
    var hu=Cls3Pawn(7,Rang,3,0,8);var hh=Adventurer.Get(hu);ClassSubclass.Pick(hh,Sub(ClassSubclass.Hunter));var keys3=ClassSubclass.PendingChoices(hh).Select(t=>t.ch.key).ToList();ClassSubclass.Choose(hh,"prey",0);
    string def3=ClassSubclass.CanChoose(hh,"defense",1);for(int i=0;i<4;i++)ClassSystem.AddLevel(hh,Rang);bool def7=ClassSubclass.Choose(hh,"defense",1);
    Check(pendT==1 && c0 && !c1 && ClassSubclass.Choice(ht,"totem")==0 && keys3.Count==1 && keys3[0]=="prey" && def3!=null && def7 && ClassSubclass.Choice(hh,"defense")==1 && ClassSubclass.Pending(hh)==0,"choices: totem picked once (bear, cannot change); hunter 3 only offers 猎手之选, 防御战术 opens at 7 ("+def3+")");
    // 5. 狂战士：狂暴中免疫恐慌；威吓凝视吓跑敌人；狂暴结束后狂乱疲惫；狂暴冲撞撞倒路线上的敌人并落到终点。
    var be=Cls3Pawn(11,Barb,11,10);var hbe=Adventurer.Get(be);ClassSubclass.Pick(hbe,bers);ScSpawn(be,ScCell(14,6));var foe=ScFoe(be);
    ClassEffects.Apply(be,Ab("LP_Rage"),Fx("LP_Rage"),new LocalTargetInfo(be));bool blocked=!be.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"test",true);
    ClassMartial2.RollOverride=.99f;bool fled;try{fled=ClassSubclass.Intimidate(be,foe,600);}finally{ClassMartial2.RollOverride=null;}bool foeFled=foe.InMentalState;foe.MentalState?.RecoverFromState();
    Items.Remove(be,"LP_Rage");bool tired=Items.Has(be,"LP_FrenzyFatigue");
    var line=be.Position+IntVec3.East*6;var mid=be.Position+IntVec3.East*3;var foe2=AdvPawn(null,AdvEnemy);bool laid=mid.Standable(map) && mid.GetFirstPawn(map)==null && line.Standable(map) && line.GetFirstPawn(map)==null;
    if(laid)GenSpawn.Spawn(foe2,mid,map);int run=laid?ClassSubclass.RageCharge(be,line,1f):-1;
    Check(blocked && fled && foeFled && tired && (!laid || run>=1 && be.Position==line && (foe2.Dead || foe2.stances.stunner.Stunned)),"berserker: no panic while raging, intimidating presence routs a foe, frenzy fatigue after rage; reckless charge stuns the foe on the line and lands"+(laid?"":" (charge skipped: blocked cells)"));
    // 6. 图腾勇士·熊：狂暴中利器 ×0.5（不再叠 ×0.7）、灼烧也 ×0.5；兽之面相（熊）；灵魂行者召来一只灰熊（召唤物）。
    var tb=Cls3Pawn(11,Barb,11,10);var htb=Adventurer.Get(tb);ClassSubclass.Pick(htb,Sub(ClassSubclass.Totem));ClassSubclass.Choose(htb,"totem",0);ScSpawn(tb,ScCell(14,-6));
    ClassEffects.Apply(tb,Ab("LP_Rage"),Fx("LP_Rage"),new LocalTargetInfo(tb));float cut=ClassMartial2.In(tb,null,Dmg(DamageDefOf.Cut,null)),burn=ClassMartial2.In(tb,null,Dmg(DamageDefOf.Burn,null));
    ClassSystem.Tick250(htb);var bears=ClassSubclass.SpiritWalker(tb,1f);made.AddRange(bears);
    Check(Near(cut,.5f) && Near(burn,.5f) && Items.Has(tb,"LP_TotemAspect") && tb.GetStatValue(StatDefOf.CarryingCapacity)>0f && bears.Count==1 && bears[0].kindDef.defName=="Bear_Grizzly" && Items.Has(bears[0],"LP_Conjured"),"totem warrior (bear): raging cut ×"+cut.ToString("0.00")+" burn ×"+burn.ToString("0.00")+", aspect of the beast, spirit walker summons a conjured grizzly");
    // 6b. 召唤兽自己扑向附近的敌人（原版动物 AI 不会主动出击）。
    if(bears.Count>0){var foeB=ScFoe(tb);made.Add(foeB);var bear=bears[0];bear.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced,false);bool eng=HediffComp_Conjured.Engage(bear);
     Check(eng && bear.CurJobDef==JobDefOf.AttackMelee && bear.CurJob.targetA.Thing is Pawn bt && bt.HostileTo(bear),"conjured beasts attack a nearby hostile on their own: engaged "+eng+" job "+bear.CurJobDef?.defName+" → "+bear.CurJob?.targetA.Thing?.LabelShort);}
    // 7. 风暴先驱·冻原：光环伤到相邻敌人，相邻友军得到庇护（冰霜 ×0.75）；风暴之魂 冰霜 ×0.5。
    var sh=Cls3Pawn(11,Barb,11,10);var hsh=Adventurer.Get(sh);ClassSubclass.Pick(hsh,Sub(ClassSubclass.Storm));ClassSubclass.Choose(hsh,"element",2);ScSpawn(sh,ScCell(-14,6));
    var foe3=ScFoe(sh);var ally=ClsPawn(1);GenSpawn.Spawn(ally,NextTo(sh),map);float inj0=ClassMelee.InjurySum(foe3);
    string pre=sh.Spawned+"@"+sh.Position+" foe "+foe3.Spawned+"@"+foe3.Position+" ally "+ally.Spawned+"@"+ally.Position;
    int hitN=ClassSubclass.StormPulse(sh,ClassSubclass.Flags(sh));pre+=" | after "+sh.Spawned+" "+foe3.Spawned+" "+ally.Spawned;var frost=DamageDefOf.Frostbite; // 不爆炸的冰霜（危险感知另外让爆炸 ×0.5）
    float ward=ClassSubclass.Ward(ally,Dmg(frost,null)),soul=ClassMartial2.In(sh,null,Dmg(frost,null)),fire=ClassMartial2.In(sh,null,Dmg(DamageDefOf.Burn,null));
    Check(hitN>=1 && (foe3.Dead || ClassMelee.InjurySum(foe3)>inj0) && Items.Has(ally,"LP_StormShelter") && Near(ward,.75f) && Near(soul,.5f) && Near(fire,1f),"storm herald (tundra): aura hurts the adjacent foe ("+hitN+", injury "+inj0+" → "+ClassMelee.InjurySum(foe3)+" at "+sh.Position+"/"+foe3.Position+"/"+ally.Position+" hostile "+foe3.HostileTo(sh)+" flags "+ClassSubclass.Flags(sh)+" spawned "+foe3.Spawned+foe3.Dead+" pre "+pre+"), sheltered ally takes frost ×"+ward.ToString("0.00")+", storm soul frost ×"+soul.ToString("0.00")+" (burn ×"+fire.ToString("0.00")+")");
    // 8. 游荡者：暗杀（没进入战斗 → ×2 并偷袭；野性直觉的人不行）、致命一击、盗贼顺手牵羊、诡术师法力、反射闪避、稳固目标。
    var ra=Cls3Pawn(11,Rog,11,10);var hra=Adventurer.Get(ra);ClassSubclass.Pick(hra,Sub(ClassSubclass.Assassin));ScSpawn(ra,ScCell(-14,-6));var foe4=ScFoe(ra);
    float cu=ra.GetStatValue(DefDatabase<StatDef>.GetNamed("LP_CunningPower"));bool openOk=ClassSubclass.Opening(ra,foe4);float op=ClassMartial2.Out(ra,foe4,Dmg(DamageDefOf.Cut,ra));
    bool feral=!ClassSubclass.Opening(ra,st) && ClassSubclass.Unsurprisable(st);
    ClassMartial2.RollOverride=null;ClassMartial2.In(ra,null,Dmg(DamageDefOf.Bullet,null));float bomb=ClassMartial2.In(ra,null,Dmg(DamageDefOf.Bomb,null));
    ClassEffects.Apply(ra,Ab("LP_SteadyTarget"),Fx("LP_SteadyTarget"),new LocalTargetInfo(ra));foe4.mindState.enemyTarget=ra;bool steadySneak=ClassMartial2.SneakOk(ra,foe4) && ClassSubclass.SteadyHit(ra);
    var th=Cls3Pawn(3,Rog,3,8);ClassSubclass.Pick(Adventurer.Get(th),Sub(ClassSubclass.Thief));ScSpawn(th,NextTo(foe4));var med=ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial);med.stackCount=2;foe4.inventory.innerContainer.TryAdd(med);
    var got=ClassSubclass.Pilfer(th,foe4);bool stole=got!=null && got.def==ThingDefOf.MedicineIndustrial && th.inventory.innerContainer.Contains(got);
    var tr=Cls3Pawn(3,Rog,3,8);var htr=Adventurer.Get(tr);ClassSubclass.Pick(htr,Sub(ClassSubclass.Trickster));ScSpawn(tr,ScCell(-10,-9));var foe5=ScFoe(tr);float m0=ClassSubclass.Mana(htr);
    var mm=tr.abilities.GetAbility(Ab("LP_ATMagicMissile"));bool cast=mm.Activate(new LocalTargetInfo(foe5),new LocalTargetInfo(foe5));float m1=ClassSubclass.Mana(htr);htr.classes.sub.mana=.5f;mm.ResetCooldown();bool grey=mm.GizmoDisabled(out string greyWhy);
    Check(openOk && Near(op,2f*(1f+ClassMartial2.SneakBonus(11,cu))) && feral && Near(bomb,.5f) && steadySneak && stole && ClassSubclass.ManaMax(htr)==4 && Near(m0,4f) && cast && Near(m1,3f) && grey && greyWhy.Contains("法力不足"),
     "rogue: assassin opening ×"+op.ToString("0.00")+" (not vs 野性直觉), evasion bomb ×0.5, steady target allows a sneak attack, thief pilfers medicine, trickster mana 4 → 3 after magic missile, greyed at 0.5 ("+greyWhy+")");
    // 9. 游侠：荆棘陷阱最多 3 个；巨像杀手对受伤目标 ×1.25；兽王伙伴成长上限跟游侠等级（25）；幽域追踪者暗影闪避（每 5 秒一次）、钢铁意志不恐慌。
    var rr=Cls3Pawn(11,Rang,11,0,8);var hrr=Adventurer.Get(rr);ClassSubclass.Pick(hrr,Sub(ClassSubclass.Hunter));ClassSubclass.Choose(hrr,"prey",0);ScSpawn(rr,ScCell(10,12));
    for(int i=0;i<4;i++){var tc=ScCell(10+i*2,15);if(tc.IsValid)made.Add(ClassSubclass.PlaceTrap(rr,tc));}
    int traps=map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("LP_ThornTrapThing")).OfType<Building_ThornTrap>().Count(x=>x.owner==rr && !x.Destroyed);
    var foe6=ScFoe(rr);float hp0=foe6.health.summaryHealth.SummaryHealthPercent;float f0=ClassSubclass.Out(rr,foe6,Dmg(DamageDefOf.Cut,rr),true);foe6.TakeDamage(new DamageInfo(DamageDefOf.Cut,3f,0f,-1f,null,foe6.RaceProps.body.AllParts.First(x=>x.def.defName=="Torso")));float f1=ClassSubclass.Out(rr,foe6,Dmg(DamageDefOf.Cut,rr),true);
    var bm=Cls3Pawn(20,Rang,3,0,8);var hbm=Adventurer.Get(bm);ClassSubclass.Pick(hbm,Sub(ClassSubclass.BeastMaster));hbm.level=25;ScSpawn(bm,ScCell(-10,12));
    var dog=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefCache<PawnKindDef>.Get("Husky")??PawnKindDefOf.Thrumbo,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false));GenSpawn.Spawn(dog,NextTo(bm),map);made.Add(dog);
    bool bond=ClassSubclass.Bond(bm,dog);int cap=BeastLevels.GrowthCap(dog);
    var gs=Cls3Pawn(15,Rang,15,0,8);ClassSubclass.Pick(Adventurer.Get(gs),Sub(ClassSubclass.Gloom));ScSpawn(gs,ScCell(-10,15));var foe7=ScFoe(gs);
    float d1,d2;ClassMartial2.RollOverride=0f;try{d1=ClassSubclass.In(gs,foe7,Dmg(DamageDefOf.Cut,foe7));d2=ClassSubclass.In(gs,foe7,Dmg(DamageDefOf.Cut,foe7));}finally{ClassMartial2.RollOverride=null;}
    bool brave=!gs.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"test",true);ClassSystem.Tick250(Adventurer.Get(gs));
    Check(traps==3 && (Near(f0,1f) || hp0<.999f) && Near(f1,ClassSubclass.ColossusFactor) && bond && ClassSubclass.Companion(bm)==dog && dog.playerSettings?.Master==bm && cap==25 && d1<0f && d2>0f && brave && Items.Has(gs,"LP_IronMindBuff"),
     "ranger: 3 thorn traps max ("+traps+"), colossus slayer ×"+f1.ToString("0.00")+" vs a wounded foe (unhurt ×"+f0.ToString("0.00")+"), beast master companion grows to the ranger's level ("+cap+", bond "+bond+" in "+ClassSubclass.Companions.Contains(dog)+" lvl "+hbm.level+" master "+(dog.playerSettings?.Master==bm)+"), shadowy dodge once per 5 s ("+d1+" / "+d2+"), iron mind refuses panic ("+brave+")");
    // 10. 旧存档提醒：职业 3 级没选子职业的殖民者在提醒名单里，有“选择子职业”按钮；提醒后记下不重复。
    var old=Cls3Pawn(3,Barb,3,8);var ho=Adventurer.Get(old);ho.classes.sub.reminded.Clear();ScSpawn(old,ScCell(0,-14));
    bool listed=ClassSubclass.NeedingPick().Contains(old);bool gizmo=old.GetGizmos().OfType<Command_Action>().Any(g=>g.defaultLabel!=null && g.defaultLabel.StartsWith("选择子职业"));ClassSubclass.PromptLetter();
    Check(listed && gizmo && ho.classes.sub.reminded.Contains(Barb) && Current.Game.GetComponent<GameComponent_Subclasses>()!=null && new Dialog_ChooseSubclass(old)!=null,"old-save reminder: an unpicked barbarian 3 is listed, gets the 选择子职业 gizmo, and is marked reminded (listed "+listed+" gizmo "+gizmo+" reminded "+ho.classes.sub.reminded.Contains(Barb)+" comp "+(Current.Game.GetComponent<GameComponent_Subclasses>()!=null)+")");
   }finally{
    ClassMartial2.RollOverride=null;
    foreach(var t in made)if(t!=null && !t.Destroyed)t.Destroy();
    foreach(var t in map.listerThings.ThingsOfDef(DefDatabase<ThingDef>.GetNamed("LP_ThornTrapThing")).ToList())t.Destroy();
   }
  }
  void PrepareSubclassSave(){
   // 野蛮人 3（图腾·鹰）+ 游荡者 3（诡术师，法力 2.5）+ 游侠 3（兽王 + 伙伴）。
   var p=ClsPawn(9,10,0,8);advMade.Remove(p);var h=Adventurer.Get(p);
   for(int i=0;i<3;i++){ClassSystem.AddLevel(h,ClassMartial2.Barbarian);}for(int i=0;i<3;i++)ClassSystem.AddLevel(h,ClassMartial2.Rogue);for(int i=0;i<3;i++)ClassSystem.AddLevel(h,ClassMartial2.Ranger);
   ClassSubclass.Pick(h,Sub(ClassSubclass.Totem));ClassSubclass.Choose(h,"totem",1);ClassSubclass.Pick(h,Sub(ClassSubclass.Trickster));h.classes.sub.mana=2.5f;ClassSubclass.Pick(h,Sub(ClassSubclass.BeastMaster));
   GenSpawn.Spawn(p,altar.InteractionCell+IntVec3.North*9,map);subSavedId=p.ThingID;
   var dog=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefCache<PawnKindDef>.Get("Husky")??PawnKindDefOf.Thrumbo,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
   GenSpawn.Spawn(dog,altar.InteractionCell+IntVec3.North*10,map);ClassSubclass.Bond(p,dog);subSavedAnimal=dog.ThingID;
  }
  void CheckSubclassAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==subSavedId);var h=Adventurer.Get(p);var dog=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==subSavedAnimal);
   var d=h?.classes?.sub;
   Check(d!=null && d.picks.Count==3 && ClassSubclass.Has(h,ClassSubclass.Totem) && ClassSubclass.Has(h,ClassSubclass.Trickster) && ClassSubclass.Has(h,ClassSubclass.BeastMaster) && ClassSubclass.Choice(h,"totem")==1
    && Near(d.mana,2.5f) && dog!=null && d.companion==dog && HasAb(p,"LP_TotemSpiritRage") && HasAb(p,"LP_ATSleep") && HasAb(p,"LP_RangerCompanion"),
    "subclass picks, totem choice (eagle), trickster mana and the beast master's companion survive save/load"+(d==null?" (no data)":" [picks "+d.picks.Count+" mana "+d.mana+" companion "+(d.companion?.LabelShort??"null")+"]"));
   dog?.Destroy();p?.Destroy();
  }
 }
}
