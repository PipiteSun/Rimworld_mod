using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.140.0 热门子职业 15 个：Test.ps1 -Only Subclass6（定义 + 每个子职业的核心效果 + 读档）。
 public partial class LichChecks {
  static string s6Id;
  Pawn S6Pawn(string cls,int lvl,string sub,int dx,int dz){
   var p=S2Pawn(cls,lvl,sub,Mathf.Max(lvl,1));p.apparel?.DestroyAll();if(!p.Spawned)GenSpawn.Spawn(p,MlCell(dx,dz),map);mlMade.Add(p);
   foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())p.health.RemoveHediff(inj);
   Subclass6.Tick250(Adventurer.Get(p));return p;
  }
  Pawn S6Foe(Pawn near,int dx,int dz){var f=AdvPawn(null,AdvEnemy);f.apparel?.DestroyAll();GenSpawn.Spawn(f,MlCell(dx,dz),map);mlMade.Add(f);f.mindState.enemyTarget=null;return f;}
  void S6Hurt(Pawn p,float amount){p.TakeDamage(new DamageInfo(DamageDefOf.Cut,amount,1f,-1f,null,p.health.hediffSet.GetNotMissingParts().First(x=>x.def==BodyPartDefOf.Torso)));}
  static DamageInfo S6Melee(Pawn a,float amt=10f)=>new DamageInfo(DamageDefOf.Cut,amt,0f,-1f,a,null,DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife"));

  void CheckSubclass6(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool on=s.adventurer;s.adventurer=true;
   Rand.PushState(114001);
   try{CheckSubclass6Inner();}
   finally{Rand.PopState();Adventurer.TestSuppressed=sup;s.adventurer=on;Subclass6.TestRoll=null;Legendary.TestD20=null;MlClean();Subclass6.Reset();}
  }
  void CheckSubclass6Inner(){
   LP_ClassDef C(string n)=>DefDatabase<LP_ClassDef>.GetNamed("LP_Class_"+n);
   // 0. 定义：15 个子职业挂对父职业、能力等级照各职业的节奏、徽记与图标都在、不写 d20。
   {var subs=Subclass6.Names.Select(n=>DefDatabase<LP_SubclassDef>.GetNamedSilentFail(n)).ToList();
    var levels=new Dictionary<string,int[]>{{"Druid",new[]{2,6,10,14}},{"Wizard",new[]{2,6,10,14}},{"Cleric",new[]{1,2,6,8,17}},{"Warlock",new[]{1,3,7,11,15}},{"Fighter",new[]{3,7,11,15}},{"Paladin",new[]{3,7,11,15}},{"Rogue",new[]{3,7,11,15}}};
    bool ok=subs.All(x=>x!=null && x.parent!=null && x.Emblem!=null && x.Emblem!=x.parent.Background && x.abilities.Count>=4 && x.abilities.All(e=>e.ability!=null && e.ability.uiIcon!=BaseContent.BadTex && e.ability.abilityClass==typeof(Ability_Class) && levels[x.parent.defName.Substring(9)].Contains(e.level)) && ClassSystem.ClassOf(x.abilities[0].ability)==x.parent);
    bool words=!subs.Where(x=>x!=null).SelectMany(x=>x.abilities.Select(e=>e.ability.description).Append(x.description)).Any(t=>t.Contains("d20") || t.Contains("掷骰") || t.Contains("检定"));
    int wiz=ClassSubclass.Options(C("Wizard")).Count(),cle=ClassSubclass.Options(C("Cleric")).Count(),rog=ClassSubclass.Options(C("Rogue")).Count();
    Check(ok && words && subs.Count==15 && wiz>=7 && cle>=6 && rog>=5 && subs.Count(x=>x.parent==C("Wizard"))==4 && subs.Count(x=>x.parent==C("Cleric"))==3 && subs.Count(x=>x.parent==C("Paladin"))==2 && subs.Count(x=>x.parent==C("Warlock"))==2 && subs.Count(x=>x.parent==C("Rogue"))==2,
     "[1.140.0] 15 subclasses (4 wizard, 3 cleric, 2 paladin, 2 warlock, 2 rogue, moon druid, champion) with emblems, icons, class-paced levels and no dice words (wizard options "+wiz+", cleric "+cle+", rogue "+rog+")");}
   // 1. 勇士：被动状态自动挂上；精益求精 10%（15 级 15%）双倍伤害；卓越生存者伤重时自愈。
   {var p=S6Pawn("LP_Class_Fighter",15,Subclass6.Champion,-10,-10);var foe=S6Foe(p,-9,-10);
    bool buffs=Items.Has(p,"LP_CP_AthleteBuff") && Items.Has(p,"LP_CP_StyleBuff");
    var d=S6Melee(p);Subclass6.TestRoll=.12f;float crit=Subclass6.Outgoing(p,foe,ref d);Subclass6.TestRoll=.2f;float no=Subclass6.Outgoing(p,foe,ref d);
    S6Hurt(p,30f);float i0=ClassMelee.InjurySum(p);Subclass6.Tick250(Adventurer.Get(p));float i1=ClassMelee.InjurySum(p);
    Check(buffs && Near(crit,2f) && Near(no,1f) && i1<i0,"[1.140.0] champion: athlete + second style hediffs, improved critical ×2 at 12% roll (15% at 15), survivor heals "+i0.ToString("0.0")+" → "+i1.ToString("0.0"));}
   // 2. 月亮结社：战斗变形挂状态；兽形里原初打击 +30% 穿透；千面变形变形中自愈。
   {var p=S6Pawn("LP_Class_Druid",14,Subclass6.Moon,-6,-10);var foe=S6Foe(p,-5,-10);
    bool cast=S2Cast(p,"LP_MO_CombatShape",p);bool form=Items.Has(p,"LP_MO_BeastForm");
    var d=S6Melee(p);Subclass6.TestRoll=.9f;Subclass6.Outgoing(p,foe,ref d);float pen=d.ArmorPenetrationInt;
    S6Hurt(p,10f);float i0=ClassMelee.InjurySum(p);Subclass6.Tick250(Adventurer.Get(p));float i1=ClassMelee.InjurySum(p);
    Check(cast && form && Near(pen,.3f) && i1<i0,"[1.140.0] moon druid: combat wild shape, primal strike armor pen +0.3 in form, thousand forms heals "+i0.ToString("0.0")+" → "+i1.ToString("0.0"));}
   // 3. 奉献之誓：奉献灵光给身边友军心智抗性；净化之触解除震慑；神圣武器挂上后命中记一次追加。
   {var p=S6Pawn("LP_Class_Paladin",15,Subclass6.Devotion,-2,-10);var ally=MlPawn(Faction.OfPlayer,-1,-10);var foe=S6Foe(p,-3,-10);
    bool aura=Items.Has(p,"LP_DT_AuraOn");var comp=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_DT_AuraOn"))?.TryGetComp<HediffComp_ClassAura>();comp?.Pulse();bool allyBuff=Items.Has(ally,"LP_DT_AuraBuff");
    Items.Give(ally,"LP_Shaken",600,1f);string pur=Subclass6.Purify(p,ally);bool clean=!Items.Has(ally,"LP_Shaken");
    bool sw=S2Cast(p,"LP_DT_SacredWeapon",p) && Items.Has(p,"LP_DT_SacredWeaponBuff");Subclass6.AfterMeleeHit(p,foe,S6Melee(p));bool due=ClassSubclass.Data(p).ticks.ContainsKey("swT");
    Check(aura && allyBuff && clean && sw && due,"[1.140.0] devotion: aura of devotion buffs an ally, purity of spirit clears 震慑 ("+pur+"), sacred weapon buff + strike timer");}
   // 4. 复仇之誓：复仇誓言标记后，无情复仇者对誓仇目标 ×1.2 并加速。
   {var p=S6Pawn("LP_Class_Paladin",7,Subclass6.Vengeance,2,-10);var foe=S6Foe(p,3,-10);
    bool vow=S2Cast(p,"LP_VN_VowOfEnmity",foe);var en=foe.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_Enmity")) as Hediff_Enmity;
    var d=S6Melee(p);Subclass6.TestRoll=.9f;float f=Subclass6.Outgoing(p,foe,ref d);
    Check(vow && en!=null && en.caster==p && Near(f,1.2f) && Items.Has(p,"LP_VN_Relentless"),"[1.140.0] vengeance: vow of enmity marks the foe, relentless avenger ×1.2 + speed buff");}
   // 5. 战争领域：战争祭司被动；导引打击、战神化身状态。
   {var p=S6Pawn("LP_Class_Cleric",17,Subclass6.War,6,-10);
    bool priest=Items.Has(p,"LP_WD_PriestBuff");bool g=S2Cast(p,"LP_WD_GuidedStrike",p) && Items.Has(p,"LP_WD_GuidedBuff");bool av=S2Cast(p,"LP_WD_Avatar",p) && Items.Has(p,"LP_WD_AvatarBuff");
    Check(priest && g && av && p.GetStatValue(StatDefOf.MeleeHitChance)>0f,"[1.140.0] war domain: war priest passive, guided strike and avatar of battle buffs");}
   // 6. 知识领域：学识祝福；强效施法神术 ×1.15；读心标记敌人。
   {var p=S6Pawn("LP_Class_Cleric",8,Subclass6.Knowledge,10,-10);var foe=S6Foe(p,11,-10);
    bool bless=Items.Has(p,"LP_KD_BlessBuff");float pf=Subclass6.PowerFactor(p,Fx("LP_WD_WarGodBlessing"));
    bool read=S2Cast(p,"LP_KD_ReadThoughts",foe) && foe.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_KD_Read")) is Hediff_ClassMark m && m.caster==p;
    Check(bless && Near(pf,1.15f) && read,"[1.140.0] knowledge domain: blessings of knowledge, potent spellcasting ×"+pf.ToString("0.00")+", read thoughts marks the foe");}
   // 7. 坟墓领域：通往坟墓标记后，友军的下一击 ×2；强效施法。
   {var p=S6Pawn("LP_Class_Cleric",8,Subclass6.Grave,-10,-6);var foe=S6Foe(p,-9,-6);var ally=MlPawn(Faction.OfPlayer,-11,-6);
    bool mark=S2Cast(p,"LP_GD_PathToGrave",foe) && Items.Has(foe,"LP_GD_Marked");float f=Subclass6.Incoming(foe,ally,S6Melee(ally));float pf=Subclass6.PowerFactor(p,Fx("LP_WD_WarGodBlessing"));
    Check(mark && Near(f,2f) && Near(pf,1.15f) && Items.Has(p,"LP_GD_EyesBuff"),"[1.140.0] grave domain: path to the grave doubles an ally's next hit (×"+f+"), potent spellcasting, eyes of the grave");}
   // 8. 咒剑宗主：诅咒标记；诅咒大师 ×1.5；目标死时回血。
   {var p=S6Pawn("LP_Class_Warlock",15,Subclass6.Hexblade,-6,-6);var foe=S6Foe(p,-5,-6);
    bool curse=S2Cast(p,"LP_HX_Curse",foe) && foe.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_HX_Cursed")) is Hediff_ClassMark m && m.caster==p;
    float f=Subclass6.Incoming(foe,p,S6Melee(p))*1.25f;S6Hurt(p,12f);float i0=ClassMelee.InjurySum(p);Subclass6.Killed(foe,null);float i1=ClassMelee.InjurySum(p);
    Check(curse && Near(f,1.5f) && i1<i0 && Items.Has(p,"LP_HX_WarriorBuff"),"[1.140.0] hexblade: curse marks, master of hexes ×1.5 total, caster heals when the cursed dies ("+i0.ToString("0.0")+" → "+i1.ToString("0.0")+")");}
   // 9. 天界宗主：天界抗性给自己和友军护盾 6 + 等级；光耀之魂治疗 ×1.2。
   {var p=S6Pawn("LP_Class_Warlock",7,Subclass6.Celestial,-2,-6);var ally=MlPawn(Faction.OfPlayer,-1,-6);
    int n=Subclass6.CelestialWard(p,4f,2500);var sh=ally.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ACB_Shield"));float pf=Subclass6.PowerFactor(p,Fx("LP_CE_HealingLight"));
    Check(n>=2 && sh!=null && Near(sh.Severity,13f) && Near(pf,1.2f),"[1.140.0] celestial: resilience shields "+n+" (ally shield "+sh?.Severity.ToString("0")+"), radiant soul ×1.2");}
   // 10. 死灵学派：收割亡魂杀敌回血；亡灵统御只对亡灵；不惧死亡被动。
   {var p=S6Pawn("LP_Class_Wizard",14,Subclass6.Necromancy,2,-6);var foe=S6Foe(p,3,-6);
    S6Hurt(p,25f);float i0=ClassMelee.InjurySum(p);Subclass6.Killed(foe,p);float i1=ClassMelee.InjurySum(p);
    string why=Subclass6.Invalid(p,Fx("LP_NM_CommandUndead"),foe);
    Check(i1<i0 && Near(i0-i1,19f) && why=="只能对亡灵使用。" && Items.Has(p,"LP_NM_InuredBuff"),"[1.140.0] necromancy: grim harvest heals 5 + level ("+(i0-i1).ToString("0")+"), command undead refuses the living, inured to undeath");}
   // 11. 预言学派：预兆每天 3 次（更强预兆）重判，第 4 次不行。
   {var p=S6Pawn("LP_Class_Wizard",14,Subclass6.Divination,6,-6);
    bool a=Subclass6.Portent(p,Adventurer.Wis,5,null,0f),b=Subclass6.Portent(p,Adventurer.Wis,5,null,0f),c=Subclass6.Portent(p,Adventurer.Wis,5,null,0f),d=Subclass6.Portent(p,Adventurer.Wis,5,null,0f);
    Check(a && b && c && !d && Subclass6.DailyLeft(p,"portent",3)==0,"[1.140.0] divination: greater portent rerolls three failed saves a day, not a fourth");}
   // 12. 防护学派：奥术守护 8 + 2 × 等级；两个被动。
   {var p=S6Pawn("LP_Class_Wizard",14,Subclass6.Abjuration,10,-6);var ally=MlPawn(Faction.OfPlayer,11,-6);
    float amt=Subclass6.GiveWard(p,ally,2500);var sh=ally.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_ACB_Shield"));
    Check(Near(amt,36f) && sh!=null && Near(sh.Severity,36f) && Items.Has(p,"LP_AJ_ImprovedBuff") && Items.Has(p,"LP_AJ_ResistBuff"),"[1.140.0] abjuration: projected ward 36 at wizard 14, improved abjuration + spell resistance passives");}
   // 13. 幻术学派：幻术学者被动；幻影自我每天一次闪开并治疗。
   {var p=S6Pawn("LP_Class_Wizard",14,Subclass6.Illusion,-10,-2);S6Hurt(p,20f);var at=p.Position;float i0=ClassMelee.InjurySum(p);
    bool first=Subclass6.TryStand(p);float i1=ClassMelee.InjurySum(p);bool second=Subclass6.TryStand(p);
    Check(Items.Has(p,"LP_IU_SavantBuff") && first && !second && i1<i0 && p.Position!=at,"[1.140.0] illusion: savant passive, illusory self blinks away once a day ("+i0.ToString("0.0")+" → "+i1.ToString("0.0")+")");}
   // 14. 风流剑客：华丽步法；单挑 ×1.2，身边有友军就不算。
   {var p=S6Pawn("LP_Class_Rogue",15,Subclass6.Swashbuckler,-6,-2);var foe=S6Foe(p,-5,-2);
    var d=S6Melee(p);float alone=Subclass6.Outgoing(p,foe,ref d);var ally=MlPawn(Faction.OfPlayer,-4,-2);float crowd=Subclass6.Outgoing(p,foe,ref d);
    bool el=S2Cast(p,"LP_SB_ElegantManeuver",p) && Items.Has(p,"LP_SB_ElegantBuff");
    Check(Items.Has(p,"LP_SB_FootworkBuff") && Near(alone,1.2f) && Near(crowd,1f) && el,"[1.140.0] swashbuckler: fancy footwork, rakish audacity ×1.2 only when dueling alone, elegant maneuver");}
   // 15. 魂刃：灵能之力被动；魂刃投掷与撕裂心灵造成伤害。
   {var p=S6Pawn("LP_Class_Rogue",15,Subclass6.Soulknife,-2,-2);var foe=S6Foe(p,-1,-2);
    float i0=foe.health.summaryHealth.SummaryHealthPercent;bool t1=S2Cast(p,"LP_SN_SoulThrow",foe);float i1=foe.Dead?0f:foe.health.summaryHealth.SummaryHealthPercent;bool t2=!foe.Dead && S2Cast(p,"LP_SN_RendMind",foe);float i2=foe.Dead?0f:foe.health.summaryHealth.SummaryHealthPercent;
    Check(Items.Has(p,"LP_SN_PowerBuff") && t1 && i1<i0 && (foe.Dead || t2 && i2<i1),"[1.140.0] soulknife: psionic power, soul throw and rend mind hurt the foe ("+i0.ToString("0")+" → "+i1.ToString("0")+" → "+i2.ToString("0")+")");}
   // 16. 不会误伤：没人学这些子职业时 Users 空、各入口直接返回。
   {var plain=MlPawn(Faction.OfPlayer,2,-2);var d=S6Melee(plain);Subclass6.Users.Remove(plain);
    Check(!Subclass6.Users.Contains(plain) && Near(Subclass6.Incoming(plain,null,d),1f) && Subclass6.PowerFactor(plain,Fx("LP_WD_WarGodBlessing"))==1f && !Subclass6.TryStand(plain),"[1.140.0] no subclass → no effect");}
  }
  void PrepareSubclass6Save(){
   var p=S6Pawn("LP_Class_Fighter",15,Subclass6.Champion,14,14);s6Id=p.ThingID;mlMade.Remove(p);
  }
  void CheckSubclass6AfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==s6Id);var h=p==null?null:Adventurer.Get(p);
   if(h!=null)Subclass6.Tick250(h);
   Check(p!=null && h!=null && ClassSubclass.Has(h,Subclass6.Champion) && ClassSystem.Knows(p,"LP_CP_Survivor") && Items.Has(p,"LP_CP_AthleteBuff") && Subclass6.Users.Contains(p),"[1.140.0] after load: champion keeps the subclass, abilities and passive hediffs");
   if(p!=null)p.Destroy();
  }
 }
}
