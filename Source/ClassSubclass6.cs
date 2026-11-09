using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.140.0 热门子职业 15 个（Defs/Subclasses6.xml）：月亮结社、勇士、奉献 / 复仇之誓、战争 / 知识 / 坟墓领域、咒剑 / 天界宗主、死灵 / 预言 / 防护 / 幻术学派、风流剑客 / 魂刃。
 // 和 1.135.0 的 Subclass5 一样挂在 LP_SubclassDef 框架上：主动效果走 ClassEffects.Apply / Invalid 链（S6_ 开头），被动在 ClassSystem.Tick250（Subclass6.Tick250），
 // 威力系数在 ClassEffects.Power（Subclass6.PowerFactor）。所有补丁第一步都是 Users 空集合判断，没人学这些子职业时零开销。
 public static class Subclass6 {
  public const string Moon="LP_Sub_CircleMoon",Champion="LP_Sub_Champion",Devotion="LP_Sub_Devotion",Vengeance="LP_Sub_Vengeance",War="LP_Sub_WarDomain",Knowledge="LP_Sub_KnowledgeDomain",Grave="LP_Sub_GraveDomain",
   Hexblade="LP_Sub_Hexblade",Celestial="LP_Sub_Celestial",Necromancy="LP_Sub_Necromancy",Divination="LP_Sub_Divination",Abjuration="LP_Sub_Abjuration",Illusion="LP_Sub_Illusion",Swashbuckler="LP_Sub_Swashbuckler",Soulknife="LP_Sub_Soulknife";
  public static readonly string[] Names={Moon,Champion,Devotion,Vengeance,War,Knowledge,Grave,Hexblade,Celestial,Necromancy,Divination,Abjuration,Illusion,Swashbuckler,Soulknife};
  public const float PrimalPen=.3f,CritChance=.1f,CritChanceHigh=.15f,CritFactor=2f,SurvivorThreshold=20f,SurvivorBase=5f,FormHealBase=3f,FormHealPerLevel=.3f,AvengerFactor=1.2f,PotentFactor=1.15f,
   HexFactor=1.25f,HexMasterFactor=1.5f,HexMasterGuard=.8f,RadiantSoul=1.2f,AudacityFactor=1.2f,DivinationCooldown=.85f,GraveMarkFactor=2f,KeeperBase=10f,HarvestBase=5f,WardBase=8f,WardPerLevel=2f,CelestialWardBase=6f;
  public const int StrikeInterval=120,RelentlessTicks=300,KeeperInterval=600,PortentPerDay=2,GreaterPortentPerDay=3,VisionStun=180,RendStun=180;
  // 被动对应的永久状态：有能力就挂、失去就摘（Tick250）。
  static readonly (string ability,string hediff)[] Permanent={("LP_CP_Athlete","LP_CP_AthleteBuff"),("LP_CP_SecondStyle","LP_CP_StyleBuff"),("LP_DT_AuraDevotion","LP_DT_AuraOn"),("LP_WD_WarPriest","LP_WD_PriestBuff"),("LP_KD_Blessings","LP_KD_BlessBuff"),
   ("LP_GD_EyesOfGrave","LP_GD_EyesBuff"),("LP_HX_Warrior","LP_HX_WarriorBuff"),("LP_NM_InuredToUndeath","LP_NM_InuredBuff"),("LP_AJ_ImprovedAbjuration","LP_AJ_ImprovedBuff"),("LP_AJ_SpellResistance","LP_AJ_ResistBuff"),
   ("LP_IU_ImprovedMinor","LP_IU_SavantBuff"),("LP_SB_FancyFootwork","LP_SB_FootworkBuff"),("LP_SN_PsionicPower","LP_SN_PowerBuff")};
  public static readonly Color Moonlight=new Color(.75f,.8f,.95f),Gold=new Color(.85f,.7f,.35f),Holy=new Color(.95f,.92f,.75f),Blood=new Color(.6f,.25f,.3f),Iron=new Color(.75f,.3f,.25f),Lore=new Color(.4f,.55f,.85f),Grey=new Color(.55f,.6f,.55f),
   Hex=new Color(.55f,.45f,.7f),Sun=new Color(.95f,.85f,.55f),Rot=new Color(.35f,.5f,.35f),Sight=new Color(.75f,.7f,.95f),Ward=new Color(.5f,.7f,.95f),Veil=new Color(.85f,.55f,.85f),Flair=new Color(.85f,.45f,.35f),Psi=new Color(.6f,.5f,.9f);
  static readonly DefRef<LP_ClassDef> druid=new DefRef<LP_ClassDef>("LP_Class_Druid"),fighter=new DefRef<LP_ClassDef>("LP_Class_Fighter"),cleric=new DefRef<LP_ClassDef>("LP_Class_Cleric"),warlock=new DefRef<LP_ClassDef>("LP_Class_Warlock"),wizard=new DefRef<LP_ClassDef>("LP_Class_Wizard");
  public static readonly HashSet<Pawn> Users=new HashSet<Pawn>();
  static readonly Dictionary<string,int> day=new Dictionary<string,int>(); // "键:thingID" → 日子 × 100 + 次数
  public static void Reset(){Users.Clear();day.Clear();lastHit.Clear();}
  static int Today=>GenDate.DaysPassed;
  static int Now=>Find.TickManager.TicksGame;
  public static float? TestRoll;
  static float Roll=>TestRoll??Rand.Value;
  public static bool Has(Pawn p,string sub)=>p!=null && Users.Count>0 && Users.Contains(p) && ClassSubclass.Has(p,sub);
  public static bool Knows(Pawn p,string ability)=>Users.Count>0 && p!=null && Users.Contains(p) && ClassSystem.Knows(p,ability);
  static bool UseDaily(Pawn p,string key,int max){string k=key+":"+p.thingIDNumber;day.TryGetValue(k,out int v);int d=v/100,n=v%100;if(d!=Today){d=Today;n=0;}if(n>=max)return false;day[k]=d*100+n+1;return true;}
  public static int DailyLeft(Pawn p,string key,int max){string k=key+":"+p.thingIDNumber;day.TryGetValue(k,out int v);return v/100!=Today?max:Mathf.Max(0,max-v%100);}
  static bool Hostile(Pawn a,Pawn t)=>AnyTarget.Hostile(a,t);
  static bool Friendly(Pawn a,Pawn t)=>AnyTarget.Friendly(a,t);
  public static int Level(Pawn p,DefRef<LP_ClassDef> c)=>c.Def==null?0:ClassSystem.Level(p,c.Def);
  public static DamageDef PsiDamage=>DefCache<DamageDef>.Get("LP_ArcaneForce")??DamageDefOf.Blunt; // 魂刃：力场伤害（银剑灵能会直奔要害）
  public static bool InForm(Pawn p)=>Items.Has(p,"LP_MO_BeastForm") || Items.Has(p,"LP_MO_ElementalForm");

  // ---------- 被动（每 250 刻） ----------
  public static void Tick250(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null || p.Dead)return;
   bool any=false;for(int i=0;i<Names.Length;i++)if(ClassSubclass.Has(h,Names[i])){any=true;break;}
   if(!any){if(Users.Remove(p))foreach(var (a,hd) in Permanent)Items.Remove(p,hd);return;}
   Users.Add(p);
   foreach(var (a,hd) in Permanent){bool k=ClassSystem.Knows(p,a);if(k && !Items.Has(p,hd))Items.Give(p,hd);else if(!k && Items.Has(p,hd))Items.Remove(p,hd);}
   if(ClassSystem.Knows(p,"LP_MO_ThousandForms") && InForm(p)){float amt=FormHealBase+FormHealPerLevel*Level(p,druid);if(ClassMelee.Heal(p,amt)>0f)Items.Mote(p,"千面变形",Moonlight);}
   if(ClassSystem.Knows(p,"LP_CP_Survivor") && ClassMelee.InjurySum(p)>SurvivorThreshold){float amt=SurvivorBase+Mathf.Max(0f,h.AModRace(Adventurer.Con));if(ClassMelee.Heal(p,amt)>0f)Items.Mote(p,"卓越生存者",Gold);}
  }
  // ---------- 威力 ----------
  public static float PowerFactor(Pawn p,CompProperties_ClassEffect e){
   if(Users.Count==0 || e==null || p==null || !Users.Contains(p))return 1f;float f=1f;
   if(e.stat!=null && e.stat.defName=="LP_DivinePower" && (ClassSystem.Knows(p,"LP_KD_PotentSpellcasting") || ClassSystem.Knows(p,"LP_GD_Potent")))f*=PotentFactor;
   if((e.effect=="Heal" || e.effect=="SacredFlame") && e.stat!=null && e.stat.defName=="LP_PactPower" && ClassSystem.Knows(p,"LP_CE_RadiantSoul"))f*=RadiantSoul;
   return f;
  }
  // 施放之后：专家预言家（法师法术冷却 ×0.85）、死亡哨兵（重伤友军的治疗再来一次）。
  public static void AfterCast(Ability_Class a,LocalTargetInfo target){
   var p=a?.pawn;var e=a?.Props;if(p==null || e==null || Users.Count==0 || !Users.Contains(p))return;
   if(e.stat!=null && e.stat.defName=="LP_ArcanePower" && ClassSystem.Knows(p,"LP_DI_ExpertDivination"))ClassCasters.ScaleCooldown(a,DivinationCooldown);
   if(ClassSystem.Knows(p,"LP_GD_SentinelAtDeath") && e.stat!=null && e.stat.defName=="LP_DivinePower" && (e.effect=="Heal" || e.effect=="CS_Heal") && target.Thing is Pawn t && t!=p && ClassMelee.InjurySum(t)>SurvivorThreshold){
    float h=ClassMelee.Heal(t,ClassEffects.Power(p,e));if(h>0f)Items.Mote(t,"死亡哨兵 +"+h.ToString("0"),Grey);
   }
  }
  // ---------- 选目标 ----------
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;
   switch(e.effect){
    case "S6_Purify":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(t.HostileTo(caster))return "只能对友军使用。";return null;
    case "S6_ReadThoughts":case "S6_HexCurse":case "S6_GraveMark":case "S6_SoulThrow":case "S6_RendMind":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
    case "S6_CommandUndead":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";
     if(!IsUndead(t))return "只能对亡灵使用。";return null;
    case "S6_Thralls":return WizardSpells.AnimateCannot(target.Thing as Corpse);
    case "S6_Ward":if(e.range>0f || t==null)return t==null?"需要选择一个活着的目标。":null;return t.HostileTo(caster)?"只能对友军使用。":null;
   }
   return null;
  }
  public static bool IsUndead(Pawn t)=>t!=null && (Items.Undead(t) || UndeadHorde.Is(t) || Monsters.KindOf(t)==Monsters.DeathKnight || Monsters.KindOf(t)==Monsters.Demilich);
  // ---------- 施放 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;int dur=ClassEffects.Duration(p,e);float pw=ClassEffects.Power(p,e);float rad=ClassEffects.Radius(p,e);
   switch(e.effect){
    case "S6_Purify":if(t!=null)Purify(p,t);return true;
    case "S6_AvengingAngel":Items.Give(p,"LP_VN_AngelBuff",dur);ClassEffects.BattleCry(p,rad,600,ClassEffects.Strength(p,e));Items.Mote(p,"复仇天使",Blood);return true;
    case "S6_ReadThoughts":if(t!=null && Hostile(p,t))ReadThoughts(p,t,dur);return true;
    case "S6_Visions":Visions(p,rad,dur);return true;
    case "S6_GraveMark":if(t!=null && Hostile(p,t))ClassMartial2.MarkTarget(p,t,"LP_GD_Marked",dur,1f,Grey,"通往坟墓");return true;
    case "S6_HexCurse":if(t!=null && Hostile(p,t))ClassMartial2.MarkTarget(p,t,"LP_HX_Cursed",dur,1f,Hex,"咒剑诅咒");return true;
    case "S6_CelestialWard":CelestialWard(p,rad,dur);return true;
    case "S6_Thralls":Thralls(p,target.Thing as Corpse);return true;
    case "S6_CommandUndead":if(t!=null && Hostile(p,t))CommandUndead(p,t,dur);return true;
    case "S6_Ward":{var who=t??p;if(who!=p && !Friendly(p,who))return true;GiveWard(p,who,dur);return true;}
    case "S6_Phantasm":Phantasm(p,rad,dur);return true;
    case "S6_SoulThrow":if(t!=null && Hostile(p,t)){t.TakeDamage(new DamageInfo(PsiDamage,pw,.5f,-1f,p));Items.Mote(t,"魂刃投掷",Psi);}return true;
    case "S6_RendMind":if(t!=null && Hostile(p,t)){t.TakeDamage(new DamageInfo(PsiDamage,pw,.5f,-1f,p));if(!t.Dead && !DNDMind.TryResist(t))t.stances?.stunner?.StunFor(RendStun,p);Items.Mote(t,"撕裂心灵",Psi);}return true;
   }
   return false;
  }
  public static string Purify(Pawn p,Pawn t){
   var parts=new List<string>();
   var s=PaladinCore.Cleanse(t,1f);if(s!=null)parts.Add(s);
   if(Items.Has(t,"LP_Shaken")){Items.Remove(t,"LP_Shaken");parts.Add("解除震慑");}
   if(t.InMentalState && HeroismFearImmunity.Fear(t.MentalStateDef)){t.MentalState.RecoverFromState();parts.Add("驱散恐惧");}
   string text=parts.Count==0?"净化之触":string.Join("、",parts);Items.Mote(t,text,Holy);return text;
  }
  public static bool ReadThoughts(Pawn p,Pawn t,int ticks){
   ClassMartial2.MarkTarget(p,t,"LP_KD_Read",ticks,1f,Lore,"读心");
   bool shaken=t.RaceProps.Humanlike && !DNDMind.TryResist(t);if(shaken)Items.Give(t,"LP_Shaken",600,1f);
   return shaken;
  }
  public static int Visions(Pawn p,float radius,int ticks){
   int n=0;if(p.Map==null)return 0;
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,radius).ToList()){if(t==p || !Hostile(p,t) || t.Dead)continue;Items.Give(t,"LP_Shaken",ticks,1f);if(!DNDMind.TryResist(t))t.stances?.stunner?.StunFor(VisionStun,p);n++;}
   Items.Mote(p,"过往之景",Lore);return n;
  }
  public static int CelestialWard(Pawn p,float radius,int ticks){
   float amount=CelestialWardBase+Level(p,warlock);int n=0;LakeArcana.Touched=true;
   Items.Give(p,"LP_ACB_Shield",ticks,amount);n++;
   if(p.Map!=null)foreach(var a in Races124.PawnsNear(p.Map,p.Position,radius).ToList()){if(a==p || a.Dead || !Friendly(p,a))continue;Items.Give(a,"LP_ACB_Shield",ticks,amount);n++;}
   Items.Mote(p,"天界抗性 "+amount.ToString("0"),Sun);return n;
  }
  public static float GiveWard(Pawn caster,Pawn who,int ticks){
   float amount=WardBase+WardPerLevel*Level(caster,wizard);LakeArcana.Touched=true;
   Items.Give(who,"LP_ACB_Shield",ticks,amount);Items.Mote(who,"奥术守护 "+amount.ToString("0"),Ward);return amount;
  }
  public static List<Pawn> Thralls(Pawn p,Corpse c){var got=WizardSpells2.Raise(p,c,1,"死灵仆役");return got;}
  public static bool CommandUndead(Pawn p,Pawn t,int ticks){
   var r=ClassCasters.Charm(p,t,ticks,0f);Items.Mote(t,r??"亡灵统御",Rot);return r==null;
  }
  public static int Phantasm(Pawn p,float radius,int ticks){
   int n=0;if(p.Map==null)return 0;
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,radius).ToList()){if(t==p || !Hostile(p,t) || t.Dead)continue;if(DNDMind.TryResist(t))continue;if(ClassSubclass.Fear(t,ticks,"变化幻象"))n++;}
   Items.Mote(p,"变化幻象",Veil);return n;
  }

  // ---------- 不倒（灼热复仇、幻影自我） ----------
  public static bool TryStand(Pawn p){
   if(p==null || p.Dead || Users.Count==0 || !Users.Contains(p))return false;
   if(ClassSystem.Knows(p,"LP_CE_SearingVengeance") && UseDaily(p,"searing",1)){
    ClassMelee.Heal(p,ClassMelee.InjurySum(p)*.5f);Items.Give(p,"LP_StaunchBleeding",2500);
    var e=DefCache<AbilityDef>.Get("LP_CE_SearingVengeance")?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();float pw=e==null?10f:ClassEffects.Power(p,e);
    if(p.Map!=null)foreach(var t in Races124.PawnsNear(p.Map,p.Position,4f).ToList()){if(t==p || t.Dead || !t.HostileTo(p))continue;t.TakeDamage(new DamageInfo(PaladinCore.Radiant,PaladinCore.HolyAmount(t,pw),.4f,-1f,p));}
    Items.Mote(p,"灼热复仇",Sun);return true;
   }
   if(ClassSystem.Knows(p,"LP_IU_IllusorySelf") && UseDaily(p,"illusorySelf",1)){
    ClassMelee.Heal(p,ClassMelee.InjurySum(p)*.25f);Items.Give(p,"LP_StaunchBleeding",2500);
    var map=p.Map;
    if(map!=null && p.Spawned){var from=p.Position;if(CellFinder.TryFindRandomCellNear(p.Position,map,5,c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null && c!=p.Position,out var cell)){FleckMaker.ThrowDustPuffThick(p.DrawPos,map,1.5f,Veil);Races124.Blink(p,cell);FleckMaker.ThrowDustPuffThick(from.ToVector3Shifted(),map,1.5f,Veil);}}
    Items.Mote(p,"幻影自我",Veil);return true;
   }
   return false;
  }
  // ---------- 预兆：自己的豁免失败时重新判定（每天 2 / 3 次） ----------
  public static bool Portent(Pawn v,int a,int dc,Pawn source,float? roll=null){
   if(Users.Count==0 || v==null || !Users.Contains(v))return false;
   int max=ClassSystem.Knows(v,"LP_DI_GreaterPortent")?GreaterPortentPerDay:ClassSystem.Knows(v,"LP_DI_Portent")?PortentPerDay:0;
   if(max<=0 || !UseDaily(v,"portent",max))return false;
   bool ok=(roll??TestRoll??Rand.Value)<Legendary.SaveChance(v,a,dc,source);
   Items.Mote(v,ok?"预兆：重判成功":"预兆：重判失败",Sight);return ok;
  }
  // ---------- 杀敌：收割亡魂、咒剑诅咒回血、灵魂守护 ----------
  public static void Killed(Pawn victim,Pawn killer){
   if(Users.Count==0 || victim==null)return;var map=victim.MapHeld;var at=victim.PositionHeld;
   if(killer!=null && Users.Contains(killer) && !killer.Dead){
    if(ClassSystem.Knows(killer,"LP_NM_GrimHarvest") && victim.HostileTo(killer) && (map==null || killer.Map==map && killer.Position.DistanceTo(at)<=10f)){float h=ClassMelee.Heal(killer,HarvestBase+Level(killer,wizard));if(h>0f)Items.Mote(killer,"收割亡魂 +"+h.ToString("0"),Rot);}
   }
   var cursed=victim.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_HX_Cursed")) as Hediff_ClassMark;
   if(cursed?.caster!=null && !cursed.caster.Dead && Users.Contains(cursed.caster)){var c=cursed.caster;float h=ClassMelee.Heal(c,HarvestBase+Level(c,warlock));if(h>0f)Items.Mote(c,"咒剑诅咒 +"+h.ToString("0"),Hex);}
   if(map==null)return;
   foreach(var k in Users.ToList()){
    if(k==null || k.Dead || k.Map!=map || !ClassSystem.Knows(k,"LP_GD_KeeperOfSouls") || !victim.HostileTo(k) || k.Position.DistanceTo(at)>10f)continue;
    var sd=ClassSubclass.Data(k);if(sd==null)continue;sd.ticks.TryGetValue("keeper",out int last);if(last>0 && Now-last<KeeperInterval && Now>=last)continue;sd.ticks["keeper"]=Math.Max(1,Now);
    float amt=KeeperBase+Level(k,cleric);ClassMelee.Heal(k,amt);
    foreach(var a in Races124.PawnsNear(map,k.Position,4f).ToList()){if(a==k || a.Dead || a.HostileTo(k))continue;ClassMelee.Heal(a,amt);}
    Items.Mote(k,"灵魂守护",Grey);
   }
  }
  // ---------- 伤害（攻击者一侧） ----------
  static readonly Dictionary<int,DamageDef> lastHit=new Dictionary<int,DamageDef>();
  public static bool Weapon(DamageInfo d)=>d.Weapon!=null && d.Def!=null && !d.Def.isExplosive && (d.Weapon.IsWeapon || d.Weapon.race!=null);
  public static bool MeleeHit(DamageInfo d)=>Weapon(d) && !d.Def.isRanged && !(d.Weapon.IsRangedWeapon);
  public static int Crits;
  static readonly AccessTools.StructFieldRef<DamageInfo,float> pen=AccessTools.StructFieldRefAccess<DamageInfo,float>("armorPenetrationInt");
  public static void SetPen(ref DamageInfo d,float v){pen(ref d)=v;}
  public static bool Alone(Pawn a,Pawn v){
   if(v?.Map==null)return true;
   foreach(var c in GenAdj.CellsAdjacent8Way(v)){if(!c.InBounds(v.Map))continue;var x=c.GetFirstPawn(v.Map);if(x!=null && x!=a && x!=v && !x.Dead && !x.HostileTo(a) && x.Faction==a.Faction)return false;}
   return true;
  }
  // 返回伤害倍率；穿透写回 dinfo。
  public static float Outgoing(Pawn a,Pawn v,ref DamageInfo d){
   float f=1f;
   if(ClassSystem.Knows(a,"LP_CP_ImprovedCritical") && Weapon(d) && v.HostileTo(a)){float ch=ClassSystem.Knows(a,"LP_CP_Survivor")?CritChanceHigh:CritChance;if(Roll<ch){f*=CritFactor;Crits++;Items.Mote(a,"精益求精！",Gold);}}
   if(ClassSystem.Knows(a,"LP_MO_PrimalStrike") && InForm(a) && MeleeHit(d))SetPen(ref d,d.ArmorPenetrationInt+PrimalPen);
   if(ClassSystem.Knows(a,"LP_VN_RelentlessAvenger") && MeleeHit(d) && v.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_Enmity")) is Hediff_Enmity en && en.caster==a){f*=AvengerFactor;Items.Give(a,"LP_VN_Relentless",RelentlessTicks);}
   if(ClassSystem.Knows(a,"LP_SB_RakishAudacity") && MeleeHit(d) && v.HostileTo(a) && Alone(a,v))f*=AudacityFactor;
   return f;
  }
  // 受害者一侧：通往坟墓（下一击 ×2，用掉）、诅咒大师（×1.5 代替 ×1.25；被诅咒者打施法者 ×0.8）。
  public static float Incoming(Pawn v,Pawn a,DamageInfo d){
   float f=1f;var set=v.health?.hediffSet;if(set==null)return 1f;
   if(a!=null && a!=v && d.Def.harmsHealth){
    var gm=set.GetFirstHediffOfDef(Items.Hediff("LP_GD_Marked"));if(gm!=null && (gm as Hediff_ClassMark)?.caster!=null && !a.HostileTo((gm as Hediff_ClassMark).caster)){f*=GraveMarkFactor;var vv=v;var hh=gm;Fiends.Defer(()=>{if(!vv.Dead && hh.pawn==vv)vv.health.RemoveHediff(hh);});Items.Mote(v,"通往坟墓！",Grey);}
    var cu=set.GetFirstHediffOfDef(Items.Hediff("LP_HX_Cursed")) as Hediff_ClassMark;if(cu?.caster!=null && ClassSystem.Knows(cu.caster,"LP_HX_MasterOfHexes"))f*=HexMasterFactor/HexFactor;
   }
   if(a!=null && Users.Contains(v) && ClassSystem.Knows(v,"LP_HX_MasterOfHexes") && a.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_HX_Cursed")) is Hediff_ClassMark ac && ac.caster==v)f*=HexMasterGuard;
   return f;
  }
  // 武器命中后：神圣武器（光耀）、神圣打击·武器（同类伤害），每 2 秒一次。
  public static bool Busy;
  public static void AfterMeleeHit(Pawn a,Pawn v,DamageInfo d){
   if(Busy || a==null || v==null || v.Dead || Users.Count==0 || !Users.Contains(a) || !v.HostileTo(a) || !Weapon(d))return;
   var sd=ClassSubclass.Data(a);if(sd==null)return;
   var sw=a.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_DT_SacredWeaponBuff"));
   if(sw!=null && Due(sd,"swT")){float amt=PaladinCore.HolyAmount(v,sw.Severity);var tv=v;var me=a;Fiends.Defer(()=>{if(tv.Dead || !tv.Spawned)return;Busy=true;try{tv.TakeDamage(new DamageInfo(PaladinCore.Radiant,amt,.3f,-1f,me));}finally{Busy=false;}});Items.Mote(v,"神圣武器",Holy);}
   if(ClassSystem.Knows(a,"LP_WD_DivineStrike") && Due(sd,"wdT")){var dp=DefCache<StatDef>.Get("LP_DivinePower");float amt=(Level(a,cleric)>=14?16f:8f)*(dp==null?1f:a.GetStatValue(dp));var def=d.Def??DamageDefOf.Blunt;var tv=v;var me=a;Fiends.Defer(()=>{if(tv.Dead || !tv.Spawned)return;Busy=true;try{tv.TakeDamage(new DamageInfo(def,amt,.3f,-1f,me));}finally{Busy=false;}});Items.Mote(v,"神圣打击",Iron);}
  }
  static bool Due(SubclassData sd,string key){sd.ticks.TryGetValue(key,out int last);if(last>0 && Now-last<StrikeInterval && Now>=last)return false;sd.ticks[key]=Math.Max(1,Now);return true;}
  public static readonly HashSet<string> NoAi=new HashSet<string>{"S6_Thralls","S6_CommandUndead","S6_Purify"};
 }

 // ==================== 挂钩 ====================
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class Subclass6Damage {
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(Subclass6.Users.Count==0 || dinfo.Def==null || dinfo.Amount<=0f || Subclass6.Busy)return true;
   var v=__instance;var a=dinfo.Instigator as Pawn;float f=1f;
   if(a!=null && a!=v && Subclass6.Users.Contains(a))f*=Subclass6.Outgoing(a,v,ref dinfo);
   f*=Subclass6.Incoming(v,a,dinfo);
   if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
   return true;
  }
 }
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class Subclass6MeleeHit {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){
   if(Subclass6.Users.Count==0)return;var a=__instance.CasterPawn;var t=target.Thing as Pawn;if(a==null || t==null)return;
   Subclass6.AfterMeleeHit(a,t,new DamageInfo(__instance.verbProps.meleeDamageDef??DamageDefOf.Blunt,1f,0f,-1f,a,null,__instance.EquipmentSource?.def??a.def));
  }
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),"CheckForStateChange")]
 public static class Subclass6Stand {
  [HarmonyPriority(Priority.High)]
  static void Prefix(Pawn ___pawn){if(Subclass6.Users.Count>0 && Subclass6.Users.Contains(___pawn) && !___pawn.Dead && !___pawn.Downed && ClassCasters.WouldDown(___pawn))Subclass6.TryStand(___pawn);}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class Subclass6Kill {
  static bool Prefix(Pawn __instance,DamageInfo? dinfo,out Pawn __state){
   __state=null;if(Subclass6.Users.Count==0 || __instance.Dead)return true;
   __state=dinfo?.Instigator as Pawn;
   if(Subclass6.Users.Contains(__instance) && Subclass6.TryStand(__instance) && !__instance.health.ShouldBeDead())return false;
   return true;
  }
  static void Postfix(Pawn __instance,Pawn __state){if(Subclass6.Users.Count>0 && __instance.Dead)Subclass6.Killed(__instance,__state);}
 }
 [HarmonyPatch(typeof(Legendary),nameof(Legendary.Save))]
 public static class Subclass6Portent {static void Postfix(Pawn p,int a,int dc,Pawn source,ref bool __result){if(!__result && Subclass6.Users.Count>0 && Subclass6.Portent(p,a,dc,source))__result=true;}}
 [HarmonyPatch(typeof(NpcClasses),nameof(NpcClasses.AiUsable))]
 public static class Subclass6NoAi {static void Postfix(Ability_Class a,ref bool __result){if(__result && a?.Props!=null && Subclass6.NoAi.Contains(a.Props.effect))__result=false;}}

 // 读旧存档：给已到选择等级、还没选子职业的殖民者发一封信。新游戏不发。
 public class GameComponent_Subclass6 : GameComponent {
  public bool notified;bool pending;
  public GameComponent_Subclass6(Game g){Subclass6.Reset();}
  public override void StartedNewGame(){base.StartedNewGame();notified=true;}
  public override void LoadedGame(){base.LoadedGame();Subclass6.Reset();if(!notified)pending=true;}
  public override void GameComponentTick(){
   if(!pending)return;pending=false;notified=true;
   if(Adventurer.TestSuppressed)return;
   var classes=new[]{"LP_Class_Druid","LP_Class_Fighter","LP_Class_Paladin","LP_Class_Cleric","LP_Class_Warlock","LP_Class_Wizard","LP_Class_Rogue"}.Select(n=>DefDatabase<LP_ClassDef>.GetNamedSilentFail(n)).Where(c=>c!=null).ToList();
   var who=PawnsFinder.AllMaps_FreeColonists.Where(p=>{var h=Adventurer.Get(p);return h!=null && classes.Any(c=>ClassSubclass.NeedsPick(h,c));}).ToList();
   if(who.Count==0)return;
   Find.LetterStack.ReceiveLetter("新的子职业","1.140.0 多了 15 个子职业：月亮结社、勇士、奉献之誓、复仇之誓、战争 / 知识 / 坟墓领域、咒剑 / 天界宗主、死灵 / 预言 / 防护 / 幻术学派、风流剑客、魂刃。\n\n还没选子职业的人："+string.Join("、",who.Select(p=>p.LabelShort))+"。选中他们，用「选择子职业」按钮打开窗口。",LetterDefOf.NeutralEvent,new LookTargets(who.Cast<Thing>()));
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref notified,"lpSub6Notified");}
 }
}
