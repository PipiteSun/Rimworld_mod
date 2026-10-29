using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.58.1 专长第二批 + 种族专长 + 起源专长（见 ArtSource/TraitsFeats-1.58.1-spec.md）。定义在 Defs/Adventurer_Feats2.xml。
 // 热路径（伤害、命中、射击）走 1.58.0 的 HeroInfo 缓存：专长的标记在 Feats2.Build 里合进去，AddFeat / RemoveFeat 时失效。
 public static class Feats2 {
  // 1.67.0 专长加强：数值约翻倍（旧值见 ArtSource/backup-1.66.0）。
  public const float BrawlerFactor=2.5f,BrawlerSocial=2f,BrawlerPush=.4f,CrafterDiscount=.8f,AdeptBonus=1.25f,ElvenAim=1.15f,ElvenBonus=1.1f,MagmaIgnite=.5f,MagmaForge=.2f,DragonFearChance=.4f,GnomeToy=1.4f,OriginLucky=.05f,MusicRadius=10f,ChefChance=.35f;
  public const int MissileDamage=8,InfernalDamage=15,InfernalCooldown=300,BloodRegenHeal=60,DwarfHeal=40,DragonFearTicks=480,PoisonCharges=20,SkilledCount=3,SkilledLevels=3,MusicInspireCount=3,BrawlerStunTicks=60;
  // 1.67.0 新专长：斩击者 / 穿刺者 / 粉碎者（三选一）、法师杀手。
  public const float WeaponTypeDamage=1.2f,SlashSlowChance=.25f,PierceCritChance=.2f,CrushPushChance=.25f,MageSlayerDamage=1.3f;
  public const int SlashSlowTicks=180,CrushStunTicks=60,MageSlayerStunTicks=60;
  public static int MissileDamageFor(Pawn p)=>MissileDamage+Math.Min(Adventurer.Get(p)?.level??0,Adventurer.MaxLevel);
  public static float? TestRoll;static float Roll=>TestRoll??Rand.Value;
  public static bool TestSuppressed; // 自测进程：除 Feats2 组外不发起源专长
  public static bool OriginOn=>LichMod.Settings?.originFeats??true;
  static LP_FeatDef F(string n)=>DefDatabase<LP_FeatDef>.GetNamedSilentFail(n);
  public static LP_FeatDef MagicInitiate=>F("LP_Feat_MagicInitiate");public static LP_FeatDef Brawler=>F("LP_Feat_TavernBrawler");public static LP_FeatDef Crafter=>F("LP_Feat_Crafter");
  public static LP_FeatDef Skilled=>F("LP_Feat_Skilled");public static LP_FeatDef Musician=>F("LP_Feat_Musician");public static LP_FeatDef Observant=>F("LP_Feat_Observant");
  public static LP_FeatDef Poisoner=>F("LP_Feat_Poisoner");public static LP_FeatDef RitualCaster=>F("LP_Feat_RitualCaster");public static LP_FeatDef Chef=>F("LP_Feat_Chef");
  public static LP_FeatDef Adept(int e)=>F("LP_Feat_ElementalAdept_"+(e==1?"Fire":e==2?"Cold":e==3?"Lightning":"Acid"));
  static HashSet<LP_FeatDef> mine;
  public static bool Is(LP_FeatDef f){if(f==null)return false;if(mine==null)mine=new HashSet<LP_FeatDef>(DefDatabase<LP_FeatDef>.AllDefsListForReading.Where(d=>d.order>=13));return mine.Contains(f);}

  // ---------- 缓存标记 ----------
  public static HeroInfo Build(Pawn p,HeroInfo i){
   var h=Adventurer.Get(p);if(h==null || h.feats.Count==0)return i;
   foreach(var f in h.feats){
    if(f==Feats.GreatWeapon || f==Feats.Sharpshooter){if(i==null)i=new HeroInfo();if(f==Feats.GreatWeapon)i.gwm=true;else i.sharp=true;continue;} // 1.67.0 伤害加成走同一个缓存
    if(!Is(f))continue;if(i==null)i=new HeroInfo();
    switch(f.defName){
     case "LP_Feat_TavernBrawler":i.brawler=true;break;case "LP_Feat_Crafter":i.crafter=true;break;case "LP_Feat_Observant":i.observant=true;break;
     case "LP_Feat_Musician":i.musician=true;break;case "LP_Feat_RitualCaster":i.ritual=true;break;case "LP_Feat_Poisoner":i.poisoner=true;break;
     case "LP_Feat_MagmaGrip":i.magma=true;break;case "LP_Feat_BloodRegen":i.bloodRegen=true;break;case "LP_Feat_InfernalWrath":i.infernal=true;break;
     case "LP_Feat_ElvenAccuracy":i.elven=true;break;case "LP_Feat_DragonFear":i.dragonFear=true;break;case "LP_Feat_PhylacteryOath":i.oath=true;break;
     case "LP_Feat_BountifulLuck":i.bountiful=true;break;case "LP_Feat_DwarvenFortitude":i.dwarven=true;break;
     case "LP_Feat_Slasher":i.weaponType=1;break;case "LP_Feat_Piercer":i.weaponType=2;break;case "LP_Feat_Crusher":i.weaponType=3;break;case "LP_Feat_MageSlayer":i.mageSlayer=true;break;
    }
    if(f.element>0)i.adept=f.element;
   }
   return i;
  }
  // 专长效果只在冒险者总开关开着时生效（同 1.48.1）。
  public static HeroInfo On(Pawn p)=>Adventurer.Enabled?HeroTraits.Info(p):null;
  // 观察者：看得见隐身（Fiends.Sees 每刻都会被调用，所以用集合）。
  public static readonly HashSet<int> Observers=new HashSet<int>();
  public static void Sync(Pawn p){if(p==null)return;var i=HeroTraits.Info(p);if(i!=null && i.observant)Observers.Add(p.thingIDNumber);else Observers.Remove(p.thingIDNumber);}
  public static bool Observer(Pawn p)=>Observers.Count>0 && p!=null && Observers.Contains(p.thingIDNumber) && Adventurer.Enabled;

  // ---------- 前置 ----------
  public static bool RaceOk(LP_FeatDef f,Pawn p){
   if(f.requireLich)return Items.IsLich(p);
   if(f.requiredXenotypesAny.NullOrEmpty() && f.requiredGenesAny.NullOrEmpty())return true;
   var x=p?.genes?.Xenotype;if(x!=null && f.requiredXenotypesAny!=null && f.requiredXenotypesAny.Contains(x))return true;
   if(f.requiredGenesAny!=null && p?.genes!=null)foreach(var g in f.requiredGenesAny)if(p.genes.HasActiveGene(g))return true;
   return false;
  }
  public static string RaceLabel(LP_FeatDef f)=>f.requireLich?"巫妖":string.Join("、",(f.requiredXenotypesAny??new List<XenotypeDef>()).Select(x=>x.label).Concat((f.requiredGenesAny??new List<GeneDef>()).Select(g=>g.label)));
  public static string RaceUnmet(LP_FeatDef f,Pawn p)=>RaceOk(f,p)?null:"只有"+RaceLabel(f)+"能选";
  public static string GroupUnmet(LP_FeatDef f,Hediff_Adventurer h){if(f.exclusiveGroup.NullOrEmpty())return null;var o=h.feats.FirstOrDefault(x=>x!=f && x.exclusiveGroup==f.exclusiveGroup);return o==null?null:"已经选了"+o.label+"（同类只能选一个）";}
  // 分页：种族专长只对对应种族显示。
  public static bool Visible(LP_FeatDef f,Pawn p)=>!f.Racial || RaceOk(f,p);
  public static void GrantAbility(Pawn p,AbilityDef d,bool on){if(d==null || p?.abilities==null)return;bool has=p.abilities.GetAbility(d)!=null;if(on && !has)p.abilities.GainAbility(d);else if(!on && has)p.abilities.RemoveAbility(d);}
  static int Skill(Pawn p,SkillDef s){var r=p?.skills?.GetSkill(s);return r==null || r.TotallyDisabled?0:r.Level;}

  // ---------- 起源专长 ----------
  public static readonly string[] OriginPool={"LP_Feat_Alert","LP_Feat_MagicInitiate","LP_Feat_Healer","LP_Feat_Lucky","LP_Feat_TavernBrawler","LP_Feat_Crafter","LP_Feat_Skilled","LP_Feat_Musician"};
  // 背景特性表；没有就按背景故事技能加成最高的一项；5% 改为幸运。
  public static LP_FeatDef PickOrigin(Pawn p,float roll){
   if(roll<OriginLucky)return Feats.Lucky;
   if(BackgroundTraits.Has(p,BackgroundTraits.Acolyte) || BackgroundTraits.Has(p,BackgroundTraits.Sage))return MagicInitiate;
   if(BackgroundTraits.Has(p,BackgroundTraits.Criminal) || BackgroundTraits.Has(p,BackgroundTraits.Soldier))return Feats.Alert;
   if(BackgroundTraits.Has(p,BackgroundTraits.FolkHero))return Brawler;
   if(BackgroundTraits.Has(p,BackgroundTraits.Noble))return Skilled;
   if(BackgroundTraits.Has(p,BackgroundTraits.Hermit))return Feats.Healer;
   if(BackgroundTraits.Has(p,BackgroundTraits.Outlander))return Feats.Lucky;
   var gains=new Dictionary<SkillDef,int>();
   foreach(var b in new[]{p.story?.Childhood,p.story?.Adulthood})if(b?.skillGains!=null)foreach(var g in b.skillGains)if(g.skill!=null){gains.TryGetValue(g.skill,out int v);gains[g.skill]=v+g.amount;}
   if(gains.Count==0 || gains.Values.Max()<=0)return Skilled;
   var best=gains.OrderByDescending(kv=>kv.Value).ThenBy(kv=>kv.Key.listOrder).First().Key;
   if(best==SkillDefOf.Shooting)return Feats.Alert;
   if(best==SkillDefOf.Melee)return Brawler;
   if(best==SkillDefOf.Medicine)return Feats.Healer;
   if(best==SkillDefOf.Social)return Musician;
   if(best==SkillDefOf.Intellectual)return MagicInitiate;
   if(best==SkillDefOf.Crafting || best==SkillDefOf.Construction)return Crafter;
   return Skilled;
  }
  public static int Given;
  // 免费发一个（不占属性提升）；已有同名专长改发技能专家，技能专家也有就不发。
  public static LP_FeatDef GiveOrigin(Hediff_Adventurer h,float? roll=null,bool force=false){
   if(h?.pawn==null || h.originFeatGiven || !force && (TestSuppressed || !OriginOn))return null;
   var p=h.pawn;if(!p.RaceProps.Humanlike || Necro.Construct(p)!=null)return null;
   h.originFeatGiven=true;
   var f=PickOrigin(p,roll??Rand.Value);if(f==null)return null;
   if(h.HasFeat(f))f=Skilled;if(f==null || h.HasFeat(f))return null;
   h.AddFeat(f);h.originFeat=f;Given++;return f;
  }
  // 老存档读档：没有标记的人补发一次。
  public static int BackfillOrigins(IEnumerable<Pawn> pawns,bool force=false){
   if(!force && (TestSuppressed || !OriginOn))return 0;int n=0;
   foreach(var p in pawns.ToList()){var h=Adventurer.Get(p);if(h!=null && !h.originFeatGiven && GiveOrigin(h,null,force)!=null)n++;}
   return n;
  }

  // ---------- 魔法学徒、技能专家 ----------
  public static Func<Pawn,string> InitiateHook; // VPE 桥：学会所选路径的第一个能力，返回能力名
  public static AbilityDef MissileDef=>DefDatabase<AbilityDef>.GetNamedSilentFail("LP_FeatMagicMissile");
  public static int RaiseSkills(Pawn p,int n){
   if(p?.skills==null)return 0;int done=0;
   foreach(var r in p.skills.skills.Where(r=>!r.TotallyDisabled && (int)r.passion<(int)Passion.Major).OrderByDescending(r=>r.Level).ThenBy(r=>r.def.listOrder).Take(n).ToList()){r.passion=r.passion==Passion.None?Passion.Minor:Passion.Major;r.Level=Math.Min(SkillRecord.MaxLevel,r.Level+SkilledLevels);done++;} // 1.67.0 这 3 个技能各 +3 级
   return done;
  }

  // ---------- 伤害与命中（HeroCombat 调用） ----------
  public static bool Unarmed(DamageInfo d)=>d.Weapon!=null && d.Weapon.race!=null && d.Def!=null && !d.Def.isRanged;
  public static bool Bludgeon(DamageInfo d)=>Unarmed(d) || d.Weapon!=null && d.Weapon.IsMeleeWeapon && d.Def==DamageDefOf.Blunt;
  // 攻击者一侧：酒馆斗殴者、元素使 +25%（社交斗殴的对手不是敌人，所以在敌对判断之前）。
  // 1.67.0：巨武器大师（双手近战 ×1.25）、神射手（远程武器 ×1.15）、斩 / 刺 / 碎（对应伤害 ×1.2，穿刺者 20% 双倍）、法师杀手（对施法者 ×1.3）。
  public static float OutFactor(Pawn a,HeroInfo i,Pawn v,DamageInfo d){
   if(!Adventurer.Enabled)return 1f;float f=1f;
   if(i.brawler && Unarmed(d))f*=a.MentalStateDef==MentalStateDefOf.SocialFighting?BrawlerSocial:BrawlerFactor;
   if(i.adept>0 && HeroCombat.Element(d.Def)==i.adept)f*=AdeptBonus;
   if(HeroCombat.extra || v==null || !v.HostileTo(a))return f;
   if(i.gwm && d.Weapon!=null && d.Weapon.IsMeleeWeapon && !d.Def.isRanged && Feats.TwoHanded(a))f*=Feats.GreatWeaponDamage;
   if(i.sharp && d.Weapon!=null && d.Weapon.IsRangedWeapon)f*=Feats.SharpshooterDamage;
   if(i.weaponType>0 && WeaponType(d)==i.weaponType){
    f*=WeaponTypeDamage;
    if(i.weaponType==2 && Roll<PierceCritChance){f*=2f;Crits++;Items.Mote(a,"穿刺！",new Color(1f,.85f,.5f));}
   }
   if(i.mageSlayer && IsMage(v))f*=MageSlayerDamage;
   return f;
  }
  // 1 斩（利器）/ 2 刺（刺击、子弹、箭）/ 3 碎（钝器），只算武器或徒手的攻击。
  public static int WeaponType(DamageInfo d){
   if(d.Def==null || d.Weapon==null || !(d.Weapon.IsWeapon || d.Weapon.race!=null))return 0;var n=d.Def.defName;
   if(d.Def==DamageDefOf.Cut || n=="Scratch")return 1;
   if(d.Def==DamageDefOf.Stab || d.Def==DamageDefOf.Bite || d.Def.isRanged && d.Def.armorCategory==DamageArmorCategoryDefOf.Sharp)return 2;
   if(d.Def==DamageDefOf.Blunt || n=="Crush")return 3;
   return 0;
  }
  // 法师杀手眼里的施法者：DND 职业 / 圣徽、心灵能力者、敌方法系兵种。
  public static bool IsMage(Pawn v){
   if(v==null)return false;if(Feats.IsCaster(v) || v.HasPsylink)return true;
   var e=Adventurer.KindExt(v);return e!=null && (e.role=="caster" || e.Primary==Adventurer.Int || e.Primary==Adventurer.Wis || e.Primary==Adventurer.Cha);
  }
  public static int Crits,Slowed,Crushed,Disrupted;
  public static bool AdeptIgnores(Pawn a,DamageDef d){var i=On(a);return i!=null && i.adept>0 && HeroCombat.Element(d)==i.adept;}
  static readonly Dictionary<int,int> wrathReady=new Dictionary<int,int>(),wrathUsed=new Dictionary<int,int>();
  public static int Pushed,Ignited,Feared,Poisoned;
  public static void AfterHit(Pawn v,Pawn a,HeroInfo ai,DamageInfo d){
   if(!Adventurer.Enabled || !v.HostileTo(a))return;
   if(ai.brawler && Unarmed(d) && !v.Dead && v.Spawned && Roll<BrawlerPush){var dest=DNDPush.Destination(v.Map,v.Position,(v.Position-a.Position).ToVector3(),1);if(dest!=v.Position){DNDPush.Move(v,dest);Pushed++;}if(!v.Dead && !v.Downed)v.stances?.stunner?.StunFor(BrawlerStunTicks,a,false,true,false);}
   if(ai.weaponType==1 && WeaponType(d)==1 && !v.Dead && Roll<SlashSlowChance){Items.Give(v,"LP_FeatSlashed",SlashSlowTicks);Slowed++;}
   if(ai.weaponType==3 && WeaponType(d)==3 && !v.Dead && v.Spawned && Roll<CrushPushChance){var dest=DNDPush.Destination(v.Map,v.Position,(v.Position-a.Position).ToVector3(),1);if(dest!=v.Position)DNDPush.Move(v,dest);if(!v.Dead && !v.Downed)v.stances?.stunner?.StunFor(CrushStunTicks,a,false,true,false);Crushed++;}
   if(ai.mageSlayer && !v.Dead && !v.Downed && Feats.Casting(v)){v.jobs?.EndCurrentJob(JobCondition.InterruptForced);v.stances?.stunner?.StunFor(MageSlayerStunTicks,a,false,true,false);Disrupted++;Items.Mote(v,"施法被打断",new Color(.7f,.7f,1f));}
   if(ai.magma && Bludgeon(d) && !v.Dead && Roll<MagmaIgnite){FireUtility.TryAttachFire(v,.3f,a);Ignited++;}
   if(ai.dragonFear && d.Weapon==null && !v.Dead && !v.Downed && !HeroDestiny.Boss(v) && Roll<DragonFearChance && v.mindState?.mentalStateHandler!=null){
    if(v.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"龙威",true,true) && v.MentalState!=null){v.MentalState.forceRecoverAfterTicks=DragonFearTicks;Feared++;}
   }
   if(ai.poisoner && HeroCombat.Melee(d) && !v.Dead){var h=a.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_FeatPoisonedBlade"));if(h!=null && DrowPoisonStrike.Poison(v)){Poisoned++;h.Severity-=1f;if(h.Severity<.5f)a.health.RemoveHediff(h);}}
   if(ai.infernal && WrathArmed(a)){int now=GenTicks.TicksGame;wrathUsed[a.thingIDNumber]=now;wrathReady.Remove(a.thingIDNumber);HeroCombat.extra=true;try{if(!v.Dead)v.TakeDamage(new DamageInfo(DamageDefOf.Flame,InfernalDamage,0f,-1f,a));}finally{HeroCombat.extra=false;}Items.Mote(a,"炼狱之怒",new Color(1f,.5f,.3f));}
  }
  // 受害者一侧：炼狱之怒被打后蓄势（10 秒冷却）。
  public static void Struck(Pawn v,HeroInfo vi,Pawn a){
   if(!Adventurer.Enabled || !vi.infernal || !a.HostileTo(v))return;int now=GenTicks.TicksGame;
   if(wrathUsed.TryGetValue(v.thingIDNumber,out int used) && now>=used && now-used<InfernalCooldown)return;
   wrathReady[v.thingIDNumber]=now;
  }
  public static bool WrathArmed(Pawn a)=>wrathReady.ContainsKey(a.thingIDNumber);
  public static float ShotFactor(Pawn p,HeroInfo i){
   if(!i.elven || !Adventurer.Enabled)return 1f;float f=ElvenAim;var map=p.Map;if(map==null)return f;
   bool drow=p.genes?.Xenotype?.defName=="LP_Drow";float glow=map.glowGrid.GroundGlowAt(p.Position);
   if(drow?glow<.3f:map.skyManager.CurSkyGlow>=.5f)f*=ElvenBonus;
   return f;
  }
  public static int Healed;
  public static float Heal(Pawn p,float amount){
   float left=amount;foreach(var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>!x.IsPermanent()).OrderByDescending(x=>x.Severity).ToList()){if(left<=0f)break;float take=Mathf.Min(left,inj.Severity);inj.Heal(take);left-=take;}
   if(amount-left>0f)Healed++;return amount-left;
  }
  public static void Killed(Pawn k,HeroInfo ki,bool melee){if(ki.bloodRegen && melee && Adventurer.Enabled && !k.Dead){if(Heal(k,BloodRegenHeal)>0f)Items.Mote(k,"嗜血再生",new Color(.8f,.3f,.3f));}}

  // ---------- 命匣之誓：命匣所在地图上，即死改为倒地 ----------
  public static int Oaths;
  public static bool TryOath(Pawn p,DamageInfo? dinfo,Hediff culprit){
   var i=On(p);if(i==null || !i.oath || p.Dead || p.health==null)return false;
   var box=Necro.Lich(p)?.box;if(box==null || box.Destroyed || box.MapHeld==null || box.MapHeld!=p.MapHeld)return false;
   var head=p.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().FirstOrDefault(m=>m.Part!=null && m.Part.def.defName=="Head");
   bool instant=dinfo.HasValue && dinfo.Value.Amount>=999f || head!=null;if(!instant)return false;
   if(head!=null)p.health.RestorePart(head.Part);
   if(!Epic.Rescue(p))return false;
   Oaths++;Items.Mote(p,"命匣之誓",new Color(.6f,.9f,.7f));return true;
  }

  // ---------- 好运连连 ----------
  public static readonly HashSet<Pawn> borrowed=new HashSet<Pawn>();
  public static int ShareLuck(Pawn p){
   var i=On(p);if(i==null || !i.bountiful || p.Map==null)return 0;
   var o=GenRadial.RadialDistinctThingsAround(p.Position,p.Map,3f,true).OfType<Pawn>().FirstOrDefault(x=>x!=p && !x.Dead && x.Faction!=null && x.Faction==p.Faction && x.RaceProps.Humanlike && !Items.Has(x,"LP_BorrowedLuck"));
   if(o==null)return 0;Items.Give(o,"LP_BorrowedLuck");borrowed.Add(o);Items.Mote(o,"借来的好运",new Color(.6f,.85f,.5f));return 1;
  }
  public static bool UseBorrowedLuck(Pawn p,Hediff_Injury injury){
   if(borrowed.Count==0 || !borrowed.Contains(p))return false;
   if(!Items.Has(p,"LP_BorrowedLuck")){borrowed.Remove(p);return false;}
   if(!p.health.WouldDieAfterAddingHediff(injury) && (p.Downed || !p.health.WouldBeDownedAfterAddingHediff(injury)))return false;
   injury.Severity=Mathf.Max(.01f,injury.Severity*.1f);Items.Remove(p,"LP_BorrowedLuck");borrowed.Remove(p);Items.Mote(p,"真走运！",new Color(1f,.9f,.4f));return true;
  }

  // ---------- 音乐家（250 刻，只看正在演奏的音乐家） ----------
  public static int Inspired;
  public static int Music(List<Pawn> musicians){
   int n=0;var tune=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_RousingTune");
   foreach(var m in musicians){
    if(m.Dead || !m.Spawned || m.CurJobDef!=JobDefOf.Play_MusicalInstrument || !Adventurer.Enabled)continue;
    var listeners=m.Map.mapPawns.FreeColonistsSpawned.Where(c=>c!=m && !c.Dead && c.Position.InHorDistOf(m.Position,MusicRadius)).ToList();
    foreach(var c in listeners){if(tune!=null)c.needs?.mood?.thoughts?.memories?.TryGainMemory(tune);n++;}
    if(listeners.Count>0 && Adventurer.Get(m)?.TryUse(Musician)==true){foreach(var t in listeners.InRandomOrder().Take(MusicInspireCount).ToList()){Items.Give(t,"LP_MusicInspired");Inspired++;}} // 1.67.0 激励 3 人
   }
   return n;
  }

  // ---------- 仪式施法者 ----------
  public static Func<Pawn,string> RitualResetHook; // VPE 桥：重置冷却剩得最久的 VPE 能力，返回能力名
  public static bool Primed(Pawn p)=>Items.Has(p,"LP_RitualPrimed");
  public static RimWorld.Ability LongestCooldown(Pawn p)=>p?.abilities?.AllAbilitiesForReading.Where(a=>a.OnCooldown && a.def.defName!="LP_FeatRitualCast").OrderByDescending(a=>a.CooldownTicksRemaining).FirstOrDefault();
  public static bool AnyOnCooldown(Pawn p)=>LongestCooldown(p)!=null || RitualCooldownHook?.Invoke(p)==true;
  public static Func<Pawn,bool> RitualCooldownHook;
  public static string Ritual(Pawn p){
   string label=null;var a=LongestCooldown(p);
   var vpe=RitualResetHook?.Invoke(p);
   if(a!=null){a.ResetCooldown();label=a.def.label;}
   if(vpe!=null)label=label==null?vpe:label+"、"+vpe;
   Items.Give(p,"LP_RitualPrimed");Items.Mote(p,"仪式施放",new Color(.6f,.55f,.85f));
   return label;
  }

  // ---------- 毒师 ----------
  public static Thing Herb(Pawn p){
   var d=ThingDefOf.MedicineHerbal;var inv=p.inventory?.innerContainer?.FirstOrDefault(t=>t.def==d);if(inv!=null)return inv;
   return p.Map?.listerThings.ThingsOfDef(d).FirstOrDefault(t=>!t.IsForbidden(Faction.OfPlayer));
  }
  public static bool Coat(Pawn p){var h=Herb(p);if(h==null)return false;h.SplitOff(1).Destroy();var b=Items.Give(p,"LP_FeatPoisonedBlade");if(b!=null)b.Severity=PoisonCharges;return true;}

  // ---------- 演员 ----------
  public static readonly HashSet<Pawn> disguised=new HashSet<Pawn>();
 }

 // ---------- 前置 / 获得时的专长 ----------
 public class FeatWorker_MagicInitiate : FeatWorker {
  public override void Gained(Pawn p,Hediff_Adventurer h){
   base.Gained(p,h);
   var learned=Feats2.InitiateHook?.Invoke(p);
   if(learned==null)Feats2.GrantAbility(p,Feats2.MissileDef,true);
  }
  public override void Lost(Pawn p,Hediff_Adventurer h){base.Lost(p,h);Feats2.GrantAbility(p,Feats2.MissileDef,false);}
 }
 public class FeatWorker_Skilled : FeatWorker {public override void Gained(Pawn p,Hediff_Adventurer h){base.Gained(p,h);Feats2.RaiseSkills(p,Feats2.SkilledCount);}}
 public class FeatWorker_Musician : FeatWorker {
  public override string ExtraPrereq=>"魅力 13 或艺术 6";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(h.scores[Adventurer.Cha]>=13 || p?.skills?.GetSkill(SkillDefOf.Artistic)?.Level>=6?null:"需要魅力 13 或艺术 6");
 }
 public class FeatWorker_ElementalAdept : FeatWorker {
  public override string ExtraPrereq=>"有 DND 职业路径或佩戴圣徽";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(Feats.IsCaster(p)?null:"需要 DND 职业路径或佩戴圣徽");
 }
 public class FeatWorker_Observant : FeatWorker {
  public override string ExtraPrereq=>"智力或感知 13";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(h.scores[Adventurer.Int]>=13 || h.scores[Adventurer.Wis]>=13?null:"需要智力或感知 13");
 }
 public class FeatWorker_Poisoner : FeatWorker {
  public override string ExtraPrereq=>"手工或医疗 6";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(p?.skills?.GetSkill(SkillDefOf.Crafting)?.Level>=6 || p?.skills?.GetSkill(SkillDefOf.Medicine)?.Level>=6?null:"需要手工或医疗 6");
 }
 public class FeatWorker_RitualCaster : FeatWorker {
  public override string ExtraPrereq=>"智力或感知 13，有 DND 职业路径或佩戴圣徽";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(h.scores[Adventurer.Int]<13 && h.scores[Adventurer.Wis]<13?"需要智力或感知 13":Feats.IsCaster(p)?null:"需要 DND 职业路径或佩戴圣徽");
 }

 // 1.67.0 穿刺者：近战或射击 6。
 public class FeatWorker_Piercer : FeatWorker {
  public override string ExtraPrereq=>"近战或射击 6";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(p?.skills?.GetSkill(SkillDefOf.Melee)?.Level>=6 || p?.skills?.GetSkill(SkillDefOf.Shooting)?.Level>=6?null:"需要近战或射击 6");
 }

 // ---------- 能力效果 ----------
 public class CompProperties_FeatPower : CompProperties_AbilityEffect {public string effect;public CompProperties_FeatPower(){compClass=typeof(CompAbilityEffect_FeatPower);}}
 public class CompAbilityEffect_FeatPower : CompAbilityEffect {
  string Effect=>((CompProperties_FeatPower)props).effect;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   base.Apply(target,dest);var p=parent.pawn;
   switch(Effect){
    case "missile":if(target.Pawn!=null){var t=target.Pawn;if(p.Map!=null){var a=p.DrawPos;var b=t.DrawPos;for(int k=1;k<=4;k++)FleckMaker.ThrowLightningGlow(a+(b-a)*(k/5f),p.Map,.5f);}t.TakeDamage(new DamageInfo(Pacts.Eldritch,Feats2.MissileDamageFor(p),.2f,-1f,p));}break;
    case "disguise":Items.Give(p,"LP_FeatDisguised");break;
    case "calm":{var t=target.Pawn;if(t?.MentalState!=null){t.MentalState.RecoverFromState();Items.Mote(t,"平静下来了",new Color(.7f,.85f,1f));}break;}
    case "coat":if(!Feats2.Coat(p))Messages.Message("身上和地图上都没有草药。",p,MessageTypeDefOf.RejectInput,false);break;
    case "ritual":{var l=Feats2.Ritual(p);if(p.Faction?.IsPlayer==true && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"完成了仪式"+(l!=null?"：“"+l+"”冷却完毕":"")+"，下一次施法不进冷却、不加心灵熵。",p,MessageTypeDefOf.PositiveEvent,false);break;}
   }
  }
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){
   if(Effect=="calm"){var t=target.Pawn;if(t==null || t.MentalState==null || t.Faction==null || !t.Faction.IsPlayer){if(throwMessages)Messages.Message("只能安抚正在精神崩溃的殖民者。",MessageTypeDefOf.RejectInput,false);return false;}}
   return base.Valid(target,throwMessages);
  }
  public override bool AICanTargetNow(LocalTargetInfo target){var t=target.Pawn;return Effect=="missile" && t!=null && !t.Dead && !t.Downed && t.HostileTo(parent.pawn);}
  public override bool GizmoDisabled(out string reason){
   if(Effect=="coat" && Feats2.Herb(parent.pawn)==null){reason="身上和地图上都没有草药。";return true;}
   if(Effect=="ritual" && !Feats2.AnyOnCooldown(parent.pawn)){reason="没有能力在冷却中。";return true;}
   return base.GizmoDisabled(out reason);
  }
 }
 public class Hediff_FeatDisguised : HediffWithComps {
  public override void PostAdd(DamageInfo? d){base.PostAdd(d);if(pawn!=null)Feats2.disguised.Add(pawn);}
  public override void PostRemoved(){base.PostRemoved();if(pawn!=null)Feats2.disguised.Remove(pawn);}
  public override void TickInterval(int delta){base.TickInterval(delta);if(pawn!=null)Feats2.disguised.Add(pawn);}
 }

 // ---------- 侏儒机巧：做发条玩具和机械类物品时一般劳动 ×1.2 ----------
 public class StatPart_GnomishCunning : StatPart {
  static ThingCategoryDef manufactured;
  public static bool Mechanical(Pawn p){
   var r=p.CurJob?.bill?.recipe;if(r==null)return false;
   return mechanical.GetOrAdd(r,Compute); // 1.58.6：做工时每刻都会算 GeneralLaborSpeed；结果只取决于配方，按配方记住
  }
  static readonly System.Collections.Concurrent.ConcurrentDictionary<RecipeDef,bool> mechanical=new System.Collections.Concurrent.ConcurrentDictionary<RecipeDef,bool>();
  static bool Compute(RecipeDef r){
   if(r.defName=="LP_MakeClockworkToy")return true;
   if(manufactured==null)manufactured=DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Manufactured");
   return r.products!=null && r.products.Any(c=>c.thingDef==ThingDefOf.ComponentIndustrial || c.thingDef==ThingDefOf.ComponentSpacer || manufactured!=null && c.thingDef.thingCategories!=null && c.thingDef.thingCategories.Contains(manufactured));
  }
  static readonly DefRef<LP_FeatDef> gnomish=new DefRef<LP_FeatDef>("LP_Feat_GnomishCunning");
  static bool Has(Pawn p)=>Feats.Has(p,gnomish.Def);
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p && Mechanical(p) && Has(p))val*=Feats2.GnomeToy;}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Mechanical(p) && Has(p)?"侏儒机巧（机械类）：×"+(Feats2.GnomeToy*100f).ToString("0")+"%":null;
 }
 [StaticConstructorOnStartup]
 public static class Feats2Startup {
  static Feats2Startup(){
   var s=DefDatabase<StatDef>.GetNamedSilentFail("GeneralLaborSpeed");
   if(s!=null){if(s.parts==null)s.parts=new List<StatPart>();if(!s.parts.Any(x=>x is StatPart_GnomishCunning)){s.parts.Add(new StatPart_GnomishCunning{parentStat=s});s.immutable=false;}}
   if(GenCommandLine.CommandLineArgPassed("lichtest"))Feats2.TestSuppressed=true;
  }
 }

 // ==================== Harmony ====================
 // 元素使：目标对该元素的抗性（基因 / 状态的伤害系数 < 1）不算。快速返回：系数 ≥ 1 或没有施法者。
 [HarmonyPatch(typeof(Pawn_GeneTracker),nameof(Pawn_GeneTracker.FactorForDamage))]
 public static class Feats2GeneFactor {static void Postfix(DamageInfo __0,ref float __result){if(__result<1f && __0.Instigator is Pawn a && Feats2.AdeptIgnores(a,__0.Def))__result=1f;}}
 [HarmonyPatch(typeof(HediffSet),nameof(HediffSet.FactorForDamage))]
 public static class Feats2HediffFactor {static void Postfix(DamageInfo __0,ref float __result){if(__result<1f && __0.Instigator is Pawn a && Feats2.AdeptIgnores(a,__0.Def))__result=1f;}}
 // 工匠：向商人买工具、武器、衣甲 -20%。
 [HarmonyPatch(typeof(Tradeable),nameof(Tradeable.GetPriceFor))]
 public static class Feats2CrafterPrice {
  public static bool Discounted(ThingDef d)=>d!=null && (d.IsWeapon || d.IsApparel || d.thingCategories!=null && d.thingCategories.Any(c=>c.defName.Contains("Tool")));
  public static float Factor(Pawn n,ThingDef d,TradeAction a){if(a!=TradeAction.PlayerBuys || n==null)return 1f;var i=Feats2.On(n);return i!=null && i.crafter && Discounted(d)?Feats2.CrafterDiscount:1f;}
  static void Postfix(Tradeable __instance,TradeAction __0,ref float __result){
   if(__0!=TradeAction.PlayerBuys || TradeSession.playerNegotiator==null)return;
   float f=Factor(TradeSession.playerNegotiator,__instance.ThingDef,__0);if(f!=1f)__result*=f;
  }
 }
 // 演员的伪装：敌人选射击目标时分数 ×0.1。快速返回：没有人在伪装。
 [HarmonyPatch(typeof(AttackTargetFinder),"GetShootingTargetScore")]
 public static class Feats2DisguiseTarget {static void Postfix(IAttackTarget __0,ref float __result){if(Feats2.disguised.Count>0 && __0?.Thing is Pawn p && Feats2.disguised.Contains(p))__result*=.1f;}}
 // 厨师：做饭 35% 多一份点心。
 [HarmonyPatch(typeof(GenRecipe),nameof(GenRecipe.MakeRecipeProducts))]
 public static class Feats2ChefSnack {
  public static int Made;
  public static bool Meal(RecipeDef r)=>r?.products!=null && r.products.Any(c=>c.thingDef?.ingestible!=null && c.thingDef.ingestible.IsMeal);
  static void Postfix(RecipeDef __0,Pawn __1,ref IEnumerable<Thing> __result){
   if(__1==null || !Meal(__0) || !Feats.Has(__1,Feats2.Chef))return;
   if((Feats2.TestRoll??Rand.Value)>=Feats2.ChefChance)return;
   var d=DefDatabase<ThingDef>.GetNamedSilentFail("LP_FeatSnack");if(d==null)return;
   Made++;__result=__result.Concat(new[]{ThingMaker.MakeThing(d)}).ToList();
  }
 }
 // 矮人坚毅：每天第一次喝酒立刻治 40 点伤。
 [HarmonyPatch(typeof(Thing),nameof(Thing.Ingested))]
 public static class Feats2DwarfAle {
  static void Postfix(Thing __instance,Pawn __0){
   var i=Feats2.On(__0);if(i==null || !i.dwarven)return;
   if(__instance.def.GetCompProperties<CompProperties_Drug>()?.chemical!=ChemicalDefOf.Alcohol)return;
   if(Adventurer.Get(__0)?.TryUse(DefDatabase<LP_FeatDef>.GetNamedSilentFail("LP_Feat_DwarvenFortitude"))==true && Feats2.Heal(__0,Feats2.DwarfHeal)>0f)Items.Mote(__0,"矮人坚毅",new Color(.9f,.75f,.4f));
  }
 }
 // 熔岩之握：在熔火锻炉做东西时 20% 再提升一档。
 [HarmonyPatch]
 public static class Feats2MagmaForge {
  static System.Reflection.MethodBase TargetMethod()=>AccessTools.GetDeclaredMethods(typeof(QualityUtility)).Find(m=>m.Name==nameof(QualityUtility.GenerateQualityCreatedByPawn) && m.GetParameters().Length>=2 && m.GetParameters()[0].ParameterType==typeof(Pawn) && m.GetParameters()[1].ParameterType==typeof(SkillDef));
  [HarmonyPriority(Priority.Low)]
  static void Postfix(Pawn __0,ref QualityCategory __result){
   var i=Feats2.On(__0);if(i==null || !i.magma || __result>=QualityCategory.Legendary)return;
   if((__0.CurJob?.GetTarget(TargetIndex.A).Thing as Building)?.def?.defName!="LP_FireGiantForge")return;
   if((Feats2.TestRoll??Rand.Value)<Feats2.MagmaForge)__result++;
  }
 }

 // ---------- 设置（画在“DND 特性”分组里） ----------
 public partial class Settings {
  public bool originFeats=true;
  void ExposeFeats2(){Scribe_Values.Look(ref originFeats,"lpOriginFeats",true);}
 }
}
