using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 野蛮人 / 游侠 / 游荡者（Test.ps1 -Only Classes，11 条 + 读档 1 条）。
 public partial class LichChecks {
  static string cls3SavedId;
  static LP_ClassDef Barb=>ClassMartial2.Barbarian;static LP_ClassDef Rang=>ClassMartial2.Ranger;static LP_ClassDef Rog=>ClassMartial2.Rogue;
  static DamageInfo Dmg(DamageDef d,Pawn by,float amount=10f)=>new DamageInfo(d,amount,0f,-1f,by);
  Pawn Cls3Pawn(int level,LP_ClassDef c,int classLevel,int melee=0,int shooting=0,int animals=0){
   var p=ClsPawn(level,melee,0,shooting);if(animals>0)p.skills.GetSkill(SkillDefOf.Animals).Level=animals;var h=Adventurer.Get(p);
   for(int i=0;i<classLevel;i++)ClassSystem.AddLevel(h,c);
   return p;
  }
  void CheckClasses3(){
   var classes=new[]{Barb,Rang,Rog};
   // 1. 定义：3 个职业、21 个能力（图标、等级档）、6 个传奇；三个强度属性；怒气是野蛮人的资源，另两个没有资源。
   var abs=classes.Where(c=>c!=null).SelectMany(c=>c.abilities).ToList();var legs=classes.Where(c=>c!=null).SelectMany(c=>c.legendary).ToList();
   Check(classes.All(c=>c!=null && c.martial && c.Background!=null) && abs.Count==21 && abs.All(e=>e.ability.abilityClass==typeof(Ability_Class) && e.ability.uiIcon!=null && e.ability.uiIcon!=BaseContent.BadTex && ClassSystem.AbilityLevels.Contains(e.level))
    && legs.Count==6 && legs.All(a=>a.uiIcon!=null && a.uiIcon!=BaseContent.BadTex) && new[]{"LP_RagePower","LP_HuntPower","LP_CunningPower"}.All(n=>DefDatabase<StatDef>.GetNamed(n).parts.Any(x=>x is StatPart_ClassPower))
    && Barb.resource==ClassResource.Rage && Rang.resource==ClassResource.None && Rog.resource==ClassResource.None,"barbarian / ranger / rogue: 21 abilities with icons, 6 legendaries, 3 power stats; rage for the barbarian, no resource for the others");
   // 2. 门槛：野蛮人格斗 5 不行、6 可以（狂暴、怒气 3）；游侠看射击或驯兽 6；游荡者看格斗或射击 6。
   var b=ClsPawn(1,5);var hb=Adventurer.Get(b);string w5=ClassSystem.CanAdd(hb,Barb);b.skills.GetSkill(SkillDefOf.Melee).Level=6;bool bOk=ClassSystem.AddLevel(hb,Barb);
   var rn=ClsPawn(1);rn.skills.GetSkill(SkillDefOf.Animals).Level=6;var rg=ClsPawn(1,0,0,6);
   Check(w5!=null && bOk && HasAb(b,"LP_Rage") && !HasAb(b,"LP_RecklessAttack") && Near(ClassSystem.Resource(hb,Barb),3f) && ClassSystem.CanAdd(Adventurer.Get(rn),Rang)==null && ClassSystem.AddLevel(Adventurer.Get(rg),Rog) && HasAb(rg,"LP_SneakAttack"),"gates: barbarian melee 5 refused / 6 → rage with 3 怒气; ranger opens at animals 6; rogue at shooting 6");
   // 3. 怒气：受到敌人伤害 +0.5、命中敌人 +0.1；脱战每 250 刻 −0.1；睡觉不恢复（Regen 跳过怒气）。
   var foe=AdvPawn(null,AdvEnemy);GenSpawn.Spawn(b,altar.InteractionCell+IntVec3.East*8,map);GenSpawn.Spawn(foe,b.Position+IntVec3.North,map);
   hb.classes.res[Barb]=0f;b.TakeDamage(Dmg(DamageDefOf.Blunt,foe,1f));float taken=ClassSystem.Resource(hb,Barb);
   ClassMartial2.Out(b,foe,Dmg(DamageDefOf.Blunt,b));float dealt=ClassSystem.Resource(hb,Barb);
   hb.classes.rageTick=Find.TickManager.TicksGame-ClassMartial2.CombatTicks-10;ClassMartial2.Tick250(hb);float decayed=ClassSystem.Resource(hb,Barb);ClassSystem.Regen(hb);
   Check(Near(taken,.5f) && Near(dealt,.6f) && Near(decayed,.5f) && Near(ClassSystem.Resource(hb,Barb),.5f),"rage: hit taken +0.5 ("+taken+"), hit dealt +0.1, out of combat −0.1 per 250 ticks, class Regen never adds 怒气");
   // 4. 狂暴：花 1 怒气；近战伤害 ×(1.15 + 0.1 × 狂怒)；利器 / 钝器伤害 ×0.7；狂暴中施法职业能力不能用。
   hb.classes.res[Barb]=2f;var rage=b.abilities.GetAbility(Ab("LP_Rage"));bool cast=rage.Activate(new LocalTargetInfo(b),new LocalTargetInfo(b));
   var rh=ClassMartial2.Rage(b);float pw=b.GetStatValue(DefDatabase<StatDef>.GetNamed("LP_RagePower"));float cut=ClassMartial2.In(b,null,Dmg(DamageDefOf.Cut,null));float burn=ClassMartial2.In(b,null,Dmg(DamageDefOf.Burn,null));
   string noSpell=ClassSystem.CannotCast(b,Ab("LP_MagicMissile"),Fx("LP_MagicMissile"));
   Check(cast && rh!=null && Near(ClassSystem.Resource(hb,Barb),1f) && Near(rh.Factor(StatDefOf.MeleeDamageFactor),1.15f+.1f*pw) && Near(cut,.7f) && Near(burn,1f) && noSpell=="狂暴中无法施法","rage: costs 1, melee damage ×"+rh?.Factor(StatDefOf.MeleeDamageFactor).ToString("0.00")+", cut ×0.7 (burn untouched), no caster abilities while raging");
   // 5. 物理职业：野蛮人 5 级给额外攻击第 1 档；游荡者 10 级没有档位，但算物理职业（战斗风格槽、强度合计）。
   var b5=Cls3Pawn(5,Barb,5,6);var r10=Cls3Pawn(10,Rog,10,6);
   Check(Martial.Tier(b5)==1 && Martial.Is(r10) && Martial.Tier(r10)==0 && Martial.Slots(r10)==1 && Near(Martial.Bonus(r10),ClassSystem.MartialBonusFor(10)),"barbarian 5 → extra attack tier 1; rogue 10 → martial (style slot, +18% power) but no extra attack");
   // 6. 宿敌：游侠 1 级一种、10 级两种（1.87.0 前 13）；人形宿敌 → 对人类伤害 ×1.2，不死宿敌不加；游侠的动物算动物伙伴。
   var ra=Cls3Pawn(13,Rang,1,0,6);var hr=Adventurer.Get(ra);int slots1=ClassMartial2.OpenFoeSlots(hr);for(int i=0;i<8;i++)ClassSystem.AddLevel(hr,Rang);int slots9=ClassMartial2.OpenFoeSlots(hr);ClassSystem.AddLevel(hr,Rang);int slots13=ClassMartial2.OpenFoeSlots(hr);
   ClassMartial2.PickFoe(hr,(int)DndType.Undead);float vsUndead=ClassMartial2.Out(ra,foe,Dmg(DamageDefOf.Cut,ra));bool picked=ClassMartial2.PickFoe(hr,ClassMartial2.Humanoid);bool third=ClassMartial2.PickFoe(hr,(int)DndType.Dragon);
   float vsHuman=ClassMartial2.Out(ra,foe,Dmg(DamageDefOf.Cut,ra));
   Check(slots1==1 && slots9==1 && slots13==2 && Near(vsUndead,1f) && picked && !third && Near(vsHuman,1.2f) && BeastLevels.Companion(ra),"favored foe: 1 pick at ranger 1 (still 1 at 9), 2 at 10; humanoid foe ×"+vsHuman.ToString("0.00")+" vs a human, undead foe nothing; a ranger counts as an animal companion master");
   // 7. 偷袭：目标盯着游荡者、身边没有友军 → 不触发；目标没盯着 → ×(1 + 偷袭加成)，同一秒不再触发；有友军贴着目标 → 触发。
   var ro=Cls3Pawn(5,Rog,5,6);var foe2=AdvPawn(null,AdvEnemy);GenSpawn.Spawn(ro,altar.InteractionCell+IntVec3.West*9,map);GenSpawn.Spawn(foe2,ro.Position+IntVec3.West,map);
   ClassMartial2.Reset();Martial.Refresh(ro);foe2.mindState.enemyTarget=ro;bool aware=!ClassMartial2.SneakOk(ro,foe2);foe2.mindState.enemyTarget=null;
   float cu=ro.GetStatValue(DefDatabase<StatDef>.GetNamed("LP_CunningPower"));float sneak=ClassMartial2.Out(ro,foe2,Dmg(DamageDefOf.Cut,ro));float again=ClassMartial2.Out(ro,foe2,Dmg(DamageDefOf.Cut,ro));
   foe2.mindState.enemyTarget=ro;var ally=ClsPawn(1);GenSpawn.Spawn(ally,foe2.Position+IntVec3.West,map);bool flank=ClassMartial2.SneakOk(ro,foe2);
   Check(aware && Near(sneak,1f+ClassMartial2.SneakBonus(5,cu)) && Near(again,1f) && flank,"sneak attack: not when the target watches the rogue alone; ×"+sneak.ToString("0.00")+" when it does not, once per second; an ally next to the target also allows it");
   // 8. 危险感知（野蛮人 5）：子弹闪开、爆炸 ×0.5；诡诈闪避（游荡者 5）：10 秒一次远程减半。
   ClassMartial2.RollOverride=0f;float dodge,boom;try{dodge=ClassMartial2.In(b5,null,Dmg(DamageDefOf.Bullet,null));boom=ClassMartial2.In(b5,null,Dmg(DamageDefOf.Bomb,null));}finally{ClassMartial2.RollOverride=null;}
   float u1=ClassMartial2.In(ro,null,Dmg(DamageDefOf.Bullet,null)),u2=ClassMartial2.In(ro,null,Dmg(DamageDefOf.Bullet,null));
   Check(dodge<0f && Near(boom,.5f) && Near(u1,.5f) && Near(u2,1f),"danger sense dodges a bullet and halves explosions; uncanny dodge halves one ranged hit per 10 seconds");
   // 9. 传奇：野蛮人 6 级不能学，7 级学狂战之魂（怒气上限 +2）；狂暴中得到狂战之魂状态（免疫击晕）。
   var lb=Cls3Pawn(20,Barb,6,12);var hl=Adventurer.Get(lb);hl.legendary=true;hl.level=25;ClassLegendary.SyncPoints(hl);var soul=Ab("LP_LegBerserkerSoul");string w8=ClassLegendary.CanLearn(hl,Barb,soul);
   ClassSystem.AddLevel(hl,Barb);bool learnt=ClassLegendary.Learn(hl,Barb,soul);ClassSystem.Tick250(hl);ClassEffects.Apply(lb,Ab("LP_Rage"),Fx("LP_Rage"),new LocalTargetInfo(lb));ClassSystem.Tick250(hl);
   Check(w8!=null && learnt && ClassSystem.MaxResource(hl,Barb)==ClassSystem.MaxResource(7)+2 && Items.Has(lb,"LP_LegBerserkerBuff") && ClassStunImmunity.Immune(lb),"legendary: barbarian 6 refused, 7 learns 狂战之魂 (+2 怒气 max); raging gives its stun-immune buff");
   // 10. 终极：野蛮人 15 + 格斗 15（1.87.0 前 20 + 18）→ 原初斗士：力量 +4（上限之后），怒气不衰减。
   var cb=Cls3Pawn(15,Barb,15,15);var hcb=Adventurer.Get(cb);int str=hcb.Eff(Adventurer.Str);hcb.classes.res[Barb]=5f;hcb.classes.rageTick=-99999;ClassMartial2.Tick250(hcb);
   int str0=Math.Min(hcb.scores[Adventurer.Str],hcb.Cap(Adventurer.Str))+CombatGifts.ScoreBonus(hcb,Adventurer.Str)+Feats.ScoreBonus(hcb,Adventurer.Str);
   Check(HasAb(cb,"LP_PrimalChampion") && str==str0+ClassMartial2.PrimalStr && Near(ClassSystem.Resource(hcb,Barb),5f),"primal champion at barbarian 15 + melee 15: strength "+str0+" → "+str+" over the cap, rage no longer decays");
   // 11. 主动能力都能放：狂暴、野性怒吼、猎人印记（伤害 ×(1 + 0.15 × 猎术)）、死亡印记、齐射、万箭齐发、刺杀、大地崩裂。
   var errors=new List<string>();var caster=ClsPawn(30,12,0,12);GenSpawn.Spawn(caster,altar.InteractionCell+IntVec3.South*9,map);var e1=AdvPawn(null,AdvEnemy);GenSpawn.Spawn(e1,caster.Position+IntVec3.South,map);
   float hm=0f;
   foreach(var n in new[]{"LP_Rage","LP_RangerMark","LP_LegDeathMark","LP_PrimalRoar","LP_Volley","LP_LegArrowStorm","LP_Assassinate","LP_LegEarthShatter"}){
    try{
     var tgt=n=="LP_Volley" || n=="LP_LegArrowStorm"?new LocalTargetInfo(e1.Position):n=="LP_Rage" || n=="LP_PrimalRoar" || n=="LP_LegEarthShatter"?new LocalTargetInfo(caster):new LocalTargetInfo(e1);
     ClassEffects.Apply(caster,Ab(n),Fx(n),tgt);
     if(n=="LP_RangerMark"){var m=ClassMartial2.Mark(e1,"LP_RangerMark");hm=m!=null && m.caster==caster?1f+ClassMartial2.MarkPer*m.Severity:0f;}
    }catch(Exception ex){errors.Add(n+": "+ex.GetType().Name+" "+ex.Message);}
   }
   Check(errors.Count==0 && hm>1f && Items.Has(caster,"LP_Rage"),"8 new actives run without errors; hunter's mark ×"+hm.ToString("0.00")+(errors.Count==0?"":": "+string.Join(" | ",errors)));
  }
  void PrepareClasses3Save(){
   var p=ClsPawn(6,10,0,8);advMade.Remove(p);var h=Adventurer.Get(p);
   for(int i=0;i<3;i++)ClassSystem.AddLevel(h,ClassMartial2.Barbarian);ClassSystem.AddLevel(h,ClassMartial2.Ranger);
   h.classes.res[ClassMartial2.Barbarian]=1.5f;h.classes.rageTick=777;ClassMartial2.PickFoe(h,ClassMartial2.Humanoid);
   var r=Items.Give(p,"LP_Rage",60000,1.2f) as Hediff_Rage;if(r!=null)r.relentlessUsed=true;
   GenSpawn.Spawn(p,altar.InteractionCell+IntVec3.North*6,map);cls3SavedId=p.ThingID;
   Check(HasAb(p,"LP_Rage") && HasAb(p,"LP_FavoredFoe") && r!=null,"save fixture: barbarian 3 / ranger 1 raging with a humanoid favored foe");
  }
  void CheckClasses3AfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==cls3SavedId);var h=Adventurer.Get(p);if(p!=null)Martial.Refresh(p);
   Check(h!=null && ClassSystem.Level(h,ClassMartial2.Barbarian)==3 && ClassSystem.Level(h,ClassMartial2.Ranger)==1 && Near(ClassSystem.Resource(h,ClassMartial2.Barbarian),1.5f) && h.classes.rageTick==777
    && h.classes.favored.Count==1 && h.classes.favored[0]==ClassMartial2.Humanoid && ClassMartial2.Rage(p)?.relentlessUsed==true && Martial.Info(p)?.barbarian==3 && HasAb(p,"LP_FavoredFoe"),"barbarian / ranger levels, 怒气, rage tick, favored foe and the rage state survive save/load"+(h==null?" (no pawn)":" [b"+ClassSystem.Level(h,ClassMartial2.Barbarian)+" r"+ClassSystem.Level(h,ClassMartial2.Ranger)+" res"+ClassSystem.Resource(h,ClassMartial2.Barbarian)+" tick"+h.classes.rageTick+" fav"+h.classes.favored.Count+" rage"+(ClassMartial2.Rage(p)?.relentlessUsed.ToString()??"none")+" info"+(Martial.Info(p)?.barbarian.ToString()??"null")+"]"));
   p?.Destroy();
  }
 }
}
