using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.128.0 法师新法术（戏法到 3 环）的效果：effect 名字以 WS_ 开头（Defs/WizardSpellbook.xml）。
 // 能复用的直接用原有效果：虚假生命 = PowerBuff + LP_ShieldSpell，通晓语言 / 闪现术 = SelfBuff，隐形术 / 飞行术 = TargetBuff，黑暗术 = 邪术师的 Warlock.Darkness，
 // 光亮术 = 光明领域的光球，活化死尸 = 巫妖的骷髅仆役（Constructs.Create），恐惧术的扇形 = ClassLegendary.Cone。
 // 控制类成功率：基础 × 目标等级差（每高 1 级 −5%，最低 10%，Subclass2.Chance），传奇生物 / 剧情首领再减半；心智类（定身、恐惧）心智抗性可以抵抗。
 [StaticConstructorOnStartup]
 public static class WizardSpells {
  public static readonly Color Arcane=new Color(.7f,.5f,1f);
  static int Now=>Find.TickManager.TicksGame;
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.HostileTo(a);
  static bool Friendly(Pawn a,Pawn t)=>t!=null && !t.Dead && !t.HostileTo(a);
  static IEnumerable<Pawn> Near(Map m,IntVec3 c,float r)=>m==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(m,c,r);
  public static float? RollOverride;static float Roll=>RollOverride??Rand.Value;
  public static float Chance(float b,Pawn caster,Pawn t){float c=Subclass2.Chance(b,caster,t)+CasterSpells.ChanceBonus(caster); /* 1.130.0 超魔·穿透 */if(ClassSubclass.Bossy(t))c*=.5f;return Mathf.Max(.05f,c);}
  public static DamageDef Frost=>DefCache<DamageDef>.Get("LP_Frost")??DamageDefOf.Frostbite;
  public static DamageDef Necrotic=>DefCache<DamageDef>.Get("LP_Necrotic")??DamageDefOf.Burn;
  public static readonly HashSet<string> NoAi=new HashSet<string>{"WS_Mending","WS_Prestidigitation","WS_Light","WS_MageHand","WS_FogCloud","WS_FindFamiliar","WS_Alarm","WS_DetectMagic","WS_Enhance","WS_ArcaneLock","WS_Knock","WS_DispelMagic","WS_ProtEnergy","WS_MagicCircle","WS_SpeakWithDead","WS_Sending","WS_AnimateDead"};
  static WizardSpells(){var skip=AccessTools.Field(typeof(NpcClasses),"Skip")?.GetValue(null) as HashSet<string>;if(skip!=null)foreach(var n in NoAi)skip.Add(n);} // 要开窗口 / 选物品 / 仪式类的不进自动施放

  // ---------- 选目标 ----------
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e?.effect==null || !e.effect.StartsWith("WS_"))return null;
   var t=target.Thing as Pawn;var th=target.Thing;
   switch(e.effect){
    case "WS_FireBolt":case "WS_RayOfFrost":case "WS_ShockingGrasp":case "WS_PoisonSpray":case "WS_ScorchingRay":case "WS_MindSpike":case "WS_VampiricTouch":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
    case "WS_HoldPerson":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";if(!t.RaceProps.Humanlike)return "只能对人形使用。";return null;
    case "WS_Enhance":case "WS_ProtEnergy":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(t.HostileTo(caster))return "只能对友军使用。";return null;
    case "WS_DispelMagic":if(t==null || t.Dead)return "需要选择一个活着的目标。";return null;
    case "WS_Mending":
     if(th==null || th is Pawn)return "需要选择一件物品或建筑。";if(!th.def.useHitPoints || th.HitPoints>=th.MaxHitPoints)return "它没有损坏。";return null;
    case "WS_MageHand":
     if(th==null || th.def.category!=ThingCategory.Item)return "需要选择一件物品。";if(th.GetStatValue(StatDefOf.Mass)*th.stackCount>10.01f)return "太重了（超过 10 千克）。";
     if(th.Position.AdjacentTo8WayOrInside(caster.Position))return "已经在身边了。";return null;
    case "WS_ArcaneLock":
     if(!(th is Building_Door d))return "只能锁门。";if(WizardLocks.Locked(d))return "这扇门已经锁上了。";return null;
    case "WS_Knock":return KnockCannot(caster,th);
    case "WS_AnimateDead":return AnimateCannot(th as Corpse);
    case "WS_SpeakWithDead":return SpeakCannot(caster,th as Corpse);
    case "WS_FindFamiliar":return Familiars.Cannot(caster);
    case "WS_Sending":return SendingCannot(caster);
   }
   return WizardSpells2.Invalid(caster,e,target); // 1.129.0
  }

  // ---------- 效果 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e?.effect==null || !e.effect.StartsWith("WS_"))return false;
   var t=target.Thing as Pawn;var cell=target.Cell;var map=p.Map;
   float pw=ClassEffects.Power(p,e);int dur=ClassEffects.Duration(p,e);float rad=ClassEffects.Radius(p,e);int up=Spellbook.CurUp;
   switch(e.effect){
    // 戏法
    case "WS_FireBolt":if(!Hostile(p,t))return true;Hit(p,t,DamageDefOf.Flame,pw,.2f);if(!t.Dead && Rand.Chance(.3f))t.TryAttachFire(.3f,p);Glow(t,true);return true;
    case "WS_RayOfFrost":if(!Hostile(p,t))return true;Hit(p,t,Frost,pw,.2f);if(!t.Dead)Items.Give(t,"LP_WS_Chilled",dur,1f);Items.Mote(t,"冷冻",new Color(.6f,.8f,1f));return true;
    case "WS_ShockingGrasp":
     if(!Hostile(p,t))return true;
     if(t.RaceProps.IsMechanoid){Hit(p,t,DamageDefOf.EMP,pw,.5f);if(!t.Dead)t.stances?.stunner?.StunFor(60,p);}else Hit(p,t,ClassCasters.Lightning??DamageDefOf.Burn,pw,.5f);
     Glow(t,false);return true;
    case "WS_PoisonSpray":if(!Hostile(p,t))return true;Hit(p,t,DefCache<DamageDef>.Get("LP_WS_Poison")??DamageDefOf.Burn,pw,.3f);if(t.Map!=null)FleckMaker.ThrowDustPuffThick(t.DrawPos,t.Map,1.5f,new Color(.5f,.8f,.3f));return true;
    case "WS_Mending":Mend(target.Thing,pw);Items.Mote(p,"修复术",Arcane);return true;
    case "WS_Prestidigitation":Prestidigitation(p,target,e.radius);return true;
    case "WS_Light":{var orb=Subclass3.LightOrb(p,cell);var life=orb?.TryGetComp<CompLifespan>();if(life!=null)life.age=Mathf.Max(0,life.Props.lifespanTicks-dur);return true;}
    case "WS_MageHand":MageHand(p,target.Thing);return true;
    // 1 环
    case "WS_FogCloud":if(map!=null && cell.InBounds(map))GasUtility.AddGas(cell,map,GasType.BlindSmoke,e.radius+up);Items.Mote(p,"云雾术",Arcane);return true;
    case "WS_Grease":SpellZone.Make(p,cell,"Grease",rad,dur);return true;
    case "WS_Thunderwave":Thunderwave(p,rad,pw);return true;
    case "WS_FindFamiliar":Familiars.Summon(p);return true;
    case "WS_Alarm":SpellZone.Make(p,p.Position,"Alarm",rad,dur);Items.Mote(p,"警报术",Arcane);return true;
    case "WS_DetectMagic":Items.Give(p,"LP_WS_DetectMagic",dur,1f);Hediff_WS.Reveal(p,e.radius);DetectLetter(p);return true;
    // 2 环
    case "WS_ScorchingRay":if(Hostile(p,t))ScorchingRay(p,t,3+up,pw);return true;
    case "WS_Shatter":Shatter(p,cell,rad,pw);return true;
    case "WS_Web":SpellZone.Make(p,cell,"Web",rad,dur);return true;
    case "WS_HoldPerson":if(Hostile(p,t))HoldPerson(p,t,1+up,dur);return true;
    case "WS_Darkness":if(cell.IsValid)Warlock.Darkness(p,cell,rad,dur);Items.Mote(p,"黑暗术",Arcane);return true;
    case "WS_MindSpike":if(!Hostile(p,t))return true;Hit(p,t,DefCache<DamageDef>.Get("LP_WS_Psychic")??DamageDefOf.Burn,pw,1f);if(!t.Dead){Items.Give(t,"LP_WS_MindSpiked",dur,1f);Hediff_WS.Uncloak(t);}Items.Mote(t,"心灵尖刺",Arcane);return true;
    case "WS_Enhance":if(Friendly(p,t))Enhance(p,t,dur);return true;
    case "WS_ArcaneLock":if(target.Thing is Building_Door door){WizardLocks.Lock(door);Items.Mote(door,"秘法锁",Arcane);}return true;
    case "WS_Knock":Knock(p,target.Thing);return true;
    // 3 环
    case "WS_DispelMagic":if(t!=null && !t.Dead){int n=Dispel(p,t);Items.Mote(t,n>0?"解除魔法 ×"+n:"没有可以解除的魔法",Arcane);}return true;
    case "WS_Fear":Fear(p,cell,e.range>0?e.range:8f,dur);return true;
    case "WS_Slow":Slow(p,def,cell,rad,dur);return true;
    case "WS_StinkingCloud":SpellZone.Make(p,cell,"Stink",rad,dur);return true;
    case "WS_VampiricTouch":if(Hostile(p,t))VampiricTouch(p,def,t,pw,dur);return true;
    case "WS_AnimateDead":AnimateDead(p,target.Thing as Corpse,1+up,dur);return true;
    case "WS_ProtEnergy":if(Friendly(p,t))ProtectionFromEnergy(p,t,dur);return true;
    case "WS_MagicCircle":MagicCircle(p,rad,dur);return true;
    case "WS_SpeakWithDead":SpeakWithDead(p,target.Thing as Corpse,pw);return true;
    case "WS_Sending":Sending(p);return true;
   }
   if(WizardSpells2.Apply(p,def,e,target))return true; // 1.129.0 4～9 环
   Log.ErrorOnce("[DND边缘大陆] 未知的法师法术效果："+e.effect,e.effect.GetHashCode());
   return true;
  }

  public static float Hit(Pawn p,Pawn t,DamageDef d,float amount,float ap){
   if(t==null || t.Dead || amount<=0f)return 0f;var r=t.TakeDamage(new DamageInfo(d,amount,ap,-1f,p));return r?.totalDamageDealt??0f;
  }
  static void Glow(Pawn t,bool fire){if(t?.Map==null)return;if(fire)FleckMaker.ThrowFireGlow(t.DrawPos,t.Map,1f);else FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,1f);}

  // 修复术：物品回 10% 耐久，建筑回 50 × 奥术强度。
  public static int Mend(Thing t,float buildingAmount){
   if(t==null || !t.def.useHitPoints)return 0;int before=t.HitPoints;
   int add=t is Building?Mathf.RoundToInt(buildingAmount):Mathf.Max(1,Mathf.RoundToInt(t.MaxHitPoints*.1f));
   t.HitPoints=Mathf.Min(t.MaxHitPoints,t.HitPoints+add);
   if(t.Spawned)FleckMaker.ThrowDustPuffThick(t.DrawPos,t.Map,1f,Arcane);
   return t.HitPoints-before;
  }
  // 奇术手法：清掉半径 3 内的污秽；选中的格子上的饭重新变新鲜。
  public static int Prestidigitation(Pawn p,LocalTargetInfo target,float radius){
   var map=p.Map;if(map==null)return 0;var c=target.Cell;int n=0;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true)){if(!cell.InBounds(map))continue;foreach(var f in cell.GetThingList(map).Where(x=>x.def.category==ThingCategory.Filth).ToList()){f.Destroy();n++;}}
   var meals=(target.Thing!=null?new List<Thing>{target.Thing}:new List<Thing>()).Concat(c.InBounds(map)?c.GetThingList(map):new List<Thing>()).Distinct();
   foreach(var m in meals){var rot=m.TryGetComp<CompRottable>();if(rot!=null && m.def.IsIngestible && m.def.ingestible.IsMeal){rot.RotProgress=0f;n++;}}
   if(c.InBounds(map))FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,2f,new Color(1f,.95f,.7f));
   Items.Mote(p,"奇术手法",Arcane);return n;
  }
  public static bool MageHand(Pawn p,Thing t){
   if(t==null || !t.Spawned || p.Map==null || t.Map!=p.Map)return false;
   var map=p.Map;FleckMaker.ThrowDustPuffThick(t.DrawPos,map,1f,Arcane);
   t.DeSpawn();if(!GenPlace.TryPlaceThing(t,p.Position,map,ThingPlaceMode.Near)){GenSpawn.Spawn(t,p.Position,map);}
   Items.Mote(p,"法师之手",Arcane);return true;
  }
  public static int Thunderwave(Pawn p,float radius,float amount){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var t in Near(map,p.Position,radius).ToList()){
    if(!Hostile(p,t))continue;Hit(p,t,DamageDefOf.Blunt,amount,.2f);n++;
    if(!t.Dead && t.Spawned)DNDPush.Move(t,DNDPush.Destination(map,t.Position,(t.Position-p.Position).ToVector3(),2));
   }
   FleckMaker.ThrowDustPuffThick(p.DrawPos,map,radius,new Color(.75f,.8f,1f));Items.Mote(p,"雷鸣波",Arcane);return n;
  }
  public static int ScorchingRay(Pawn p,Pawn t,int rays,float amount){
   var list=new List<Pawn>{t};list.AddRange(Near(t.Map,t.Position,5f).Where(x=>x!=t && Hostile(p,x) && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(t.Position)));
   int hits=0;
   for(int i=0;i<rays;i++){
    var v=list[i%list.Count];if(v.Dead || !v.Spawned)v=list.FirstOrDefault(x=>!x.Dead && x.Spawned);if(v==null)break;
    Hit(p,v,DamageDefOf.Flame,amount,.2f);Glow(v,true);hits++;
   }
   Items.Mote(p,"灼热射线 ×"+hits,Arcane);return hits;
  }
  static bool Solid(Pawn t)=>t.RaceProps.IsMechanoid || !t.RaceProps.IsFlesh;
  public static int Shatter(Pawn p,IntVec3 c,float radius,float amount){
   var map=p.Map;if(map==null || !c.IsValid)return 0;int n=0;
   foreach(var t in Near(map,c,radius).ToList())if(Hostile(p,t)){Hit(p,t,DamageDefOf.Blunt,amount*(Solid(t)?2f:1f),.3f);n++;}
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true)){
    if(!cell.InBounds(map))continue;var b=cell.GetEdifice(map);
    if(b!=null && b.Faction!=null && p.Faction!=null && b.Faction.HostileTo(p.Faction)){b.TakeDamage(new DamageInfo(DamageDefOf.Blunt,amount*2f,.3f,-1f,p));n++;}
   }
   FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,radius*1.5f,new Color(.8f,.8f,.9f));
   return n;
  }
  public static bool TryControl(Pawn p,Pawn t,float baseChance,bool mind){
   if(mind && Items.Has(t,"LP_WS_MindBlank")){Items.Mote(t,"心灵屏障",Arcane);return false;} // 1.129.0
   if(mind && DNDMind.TryResist(t)){Items.Mote(t,"抵抗了",Arcane);return false;}
   if(Roll>=Chance(baseChance,p,t)){Items.Mote(t,"挣脱了",Arcane);return false;}
   return true;
  }
  public static int HoldPerson(Pawn p,Pawn t,int count,int ticks){
   var list=new List<Pawn>{t};list.AddRange(Near(t.Map,t.Position,6f).Where(x=>x!=t && Hostile(p,x) && x.RaceProps.Humanlike && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(t.Position)).Take(count-1));
   int n=0;foreach(var x in list){if(!TryControl(p,x,.55f,true))continue;x.stances?.stunner?.StunFor(ticks,p);Items.Mote(x,"定身",Arcane);n++;}
   return n;
  }
  // 增强属性：先给力量（窗口里改选别的）。
  public static readonly string[] EnhanceHediffs={"LP_WS_Enhance_Str","LP_WS_Enhance_Dex","LP_WS_Enhance_Int","LP_WS_Enhance_Cha"};
  static readonly string[] EnhanceLabels={"力量：负重 ×1.5、采矿 ×1.3","敏捷：近战闪避 +10","智力：研究 ×1.2","魅力：社交影响 ×1.2"};
  public static void SetEnhance(Pawn t,int k,int ticks){foreach(var n in EnhanceHediffs)Items.Remove(t,n);Items.Give(t,EnhanceHediffs[Mathf.Clamp(k,0,3)],ticks,1f);Items.Mote(t,"增强属性",Arcane);}
  static void Enhance(Pawn p,Pawn t,int ticks){
   SetEnhance(t,0,ticks);if(!Spellbook.Player(p) || Adventurer.TestSuppressed)return;
   Find.WindowStack.Add(new Dialog_SpellChoice("增强属性："+t.LabelShort,"选一项属性，持续 1 小时。",Enumerable.Range(0,4).Select(i=>(EnhanceLabels[i],(string)null,(Action)(()=>SetEnhance(t,i,ticks)))).ToList()));
  }
  // 敲击术：打开古代容器 / 休眠舱，立刻破解，或震碎别人的门。
  public static string KnockCannot(Pawn p,Thing t){
   if(t==null)return "需要选择一个容器、门或能破解的东西。";
   if(t is Building_Casket c)return c.HasAnyContents?null:"里面是空的。";
   var hk=t.TryGetComp<CompHackable>();if(hk!=null)return hk.IsHacked?"已经破解过了。":null;
   if(t is Building_Door d)return d.Faction==p.Faction?"不用敲自己的门。":null;
   return "只能对密封容器、门或需要破解的东西使用。";
  }
  public static string Knock(Pawn p,Thing t){
   if(KnockCannot(p,t)!=null)return null;
   if(t is Building_Casket c){c.Open();Items.Mote(c,"敲击术：打开",Arcane);return "open";}
   var hk=t.TryGetComp<CompHackable>();if(hk!=null){hk.Hack(999999f,p);Items.Mote(t,"敲击术：破解",Arcane);return "hack";}
   if(t is Building_Door d){var map=d.Map;var pos=d.Position;d.Destroy(DestroyMode.KillFinalize);if(map!=null)FleckMaker.ThrowDustPuffThick(pos.ToVector3Shifted(),map,2f,Arcane);return "door";}
   return null;
  }
  // 解除魔法：敌人去掉 DND 的限时增益（和隐形），友军去掉 DND 的限时有害效果、魅惑 / 支配类精神状态和石化。
  static bool Timed(Hediff h)=>h is HediffWithComps hc && hc.TryGetComp<HediffComp_Disappears>()!=null;
  static bool Ours(Hediff h)=>h.def.defName.StartsWith("LP_") && !(h is Hediff_Injury) && !(h is Hediff_MissingPart) && !(h is Hediff_Adventurer);
  public static readonly HashSet<string> FriendDispel=new HashSet<string>{"LP_Petrifying","LP_NabassuPetrify","LP_WL_Hexed","LP_DivineDominated","LP_DragonCharmed","LP_MagicSleep","LP_WL_Darkened","LP_WS_Slowed","LP_WS_Chilled","LP_WS_MindSpiked","LP_Entangled"};
  public static int Dispel(Pawn p,Pawn t){
   int n=0;var set=t.health.hediffSet;
   if(t.HostileTo(p)){
    foreach(var h in set.hediffs.ToList())if(Ours(h) && !h.def.isBad && Timed(h) || h.TryGetComp<HediffComp_Invisibility>()!=null){t.health.RemoveHediff(h);n++;}
   }else{
    foreach(var h in set.hediffs.ToList())if(Ours(h) && h.def.isBad && (Timed(h) || FriendDispel.Contains(h.def.defName))){t.health.RemoveHediff(h);n++;}
    var ms=t.MentalStateDef;if(ms!=null && (ms==MentalStateDefOf.Wander_Psychotic || ms==MentalStateDefOf.Berserk || ms==MentalStateDefOf.PanicFlee)){t.MentalState?.RecoverFromState();n++;}
   }
   if(t.Map!=null)FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,1.5f);
   return n;
  }
  public static int Fear(Pawn p,IntVec3 toward,float range,int ticks){
   int n=0;foreach(var t in ClassLegendary.Cone(p,toward,range)){
    if(!t.RaceProps.Humanlike || t.Downed || t.mindState?.mentalStateHandler==null || !TryControl(p,t,.55f,true))continue;
    if(t.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.PanicFlee,"恐惧术",true,true)){if(t.MentalState!=null)t.MentalState.forceRecoverAfterTicks=ticks;Items.Mote(t,"恐惧",Arcane);n++;}
   }
   Items.Mote(p,"恐惧术",Arcane);return n;
  }
  public const int SlowMax=6;
  public static List<Pawn> Slow(Pawn p,AbilityDef def,IntVec3 c,float radius,int ticks){
   var got=new List<Pawn>();if(p.Map==null || !c.IsValid)return got;
   foreach(var t in Near(p.Map,c,radius).Where(x=>Hostile(p,x)).OrderBy(x=>x.Position.DistanceToSquared(c)).Take(SlowMax).ToList()){
    if(!TryControl(p,t,.6f,false))continue;Items.Give(t,"LP_WS_Slowed",ticks,1f);Items.Mote(t,"缓慢",Arcane);got.Add(t);
   }
   if(got.Count>0)Spellbook.Concentrate(p,def,got,"LP_WS_Slowed");
   return got;
  }
  public static float VampiricTouch(Pawn p,AbilityDef def,Pawn t,float amount,int ticks){
   float dealt=Hit(p,t,Necrotic,amount,.3f);float healed=ClassMelee.Heal(p,dealt*.5f);
   Items.Mote(p,"吸血 "+healed.ToString("0"),new Color(.55f,.8f,.55f));
   if(!Items.Has(p,"LP_WS_VampiricTouch")){Items.Give(p,"LP_WS_VampiricTouch",ticks,1f);Spellbook.Concentrate(p,def,new[]{p},"LP_WS_VampiricTouch");}
   return dealt;
  }
  public static string AnimateCannot(Corpse c){
   if(c==null || c.Destroyed)return "需要选择一具尸体。";var x=c.InnerPawn;if(x==null || !x.RaceProps.Humanlike)return "只能活化人形尸体。";
   if(Items.Undead(x) || Necro.Construct(x)!=null)return "亡灵的尸体无法再活化。";return null;
  }
  public static List<Pawn> AnimateDead(Pawn p,Corpse c,int count,int ticks){
   var got=new List<Pawn>();if(AnimateCannot(c)!=null || p.Map==null)return got;
   var list=new List<Corpse>{c};list.AddRange(GenRadial.RadialDistinctThingsAround(c.Position,c.Map,5f,true).OfType<Corpse>().Where(x=>x!=c && AnimateCannot(x)==null).OrderBy(x=>x.Position.DistanceToSquared(c.Position)).Take(count-1));
   foreach(var corpse in list){
    var map=corpse.Map;var pos=corpse.Position;
    try{corpse.Destroy();var sk=Constructs.Create("Skeleton",p,pos,map);Items.Give(sk,"LP_WS_Animated",ticks,1f);FleckMaker.ThrowDustPuffThick(pos.ToVector3Shifted(),map,2f,new Color(.55f,.8f,.55f));got.Add(sk);}
    catch(Exception ex){Log.Warning("[DND边缘大陆] 活化死尸失败："+ex.Message);}
   }
   if(got.Count>0)Items.Mote(p,"活化死尸 ×"+got.Count,Arcane);
   return got;
  }
  // 防护能量：五种伤害，按伤害名分类。
  public static readonly string[] ProtHediffs={"LP_WS_Prot_Fire","LP_WS_Prot_Cold","LP_WS_Prot_Lightning","LP_WS_Prot_Acid","LP_WS_Prot_Psychic"};
  static readonly string[] ProtLabels={"火焰","寒冷","闪电","强酸","心灵"};
  public static int EnergyKind(DamageDef d){
   if(d==null)return -1;var n=d.defName;
   if(d==DamageDefOf.Flame || d==DamageDefOf.Burn || n.Contains("Fire") || n.Contains("Flame") || n.Contains("Burn") || n.Contains("Hellfire"))return 0;
   if(d==DamageDefOf.Frostbite || n.Contains("Frost") || n.Contains("Cold") || n.Contains("Ice"))return 1;
   if(n.Contains("Lightning") || n.Contains("Electric") || n.Contains("Shock") || n.Contains("Thunder"))return 2;
   if(n.Contains("Acid") || n.Contains("Poison") || n.Contains("Tox") || n.Contains("Chemical"))return 3;
   if(ClassSubclass.Psychic(d))return 4;
   return -1;
  }
  public static void SetProtection(Pawn t,int k,int ticks){foreach(var n in ProtHediffs)Items.Remove(t,n);Items.Give(t,ProtHediffs[Mathf.Clamp(k,0,4)],ticks,1f);Items.Mote(t,"防护能量："+ProtLabels[Mathf.Clamp(k,0,4)],Arcane);}
  static void ProtectionFromEnergy(Pawn p,Pawn t,int ticks){
   SetProtection(t,0,ticks);if(!Spellbook.Player(p) || Adventurer.TestSuppressed)return;
   Find.WindowStack.Add(new Dialog_SpellChoice("防护能量："+t.LabelShort,"选一种伤害，1 小时内受到这种伤害 ×0.5。",Enumerable.Range(0,5).Select(i=>(ProtLabels[i],(string)null,(Action)(()=>SetProtection(t,i,ticks)))).ToList()));
  }
  // 魔法阵：恶魔魔鬼 / 亡灵 / 元素 / 妖精。
  public static readonly string[] CircleLabels={"恶魔与魔鬼","亡灵","元素生物","妖精"};
  public static bool CircleKind(Pawn t,int mode){
   if(t==null)return false;var ext=Outsiders.Of(t);
   switch(mode){
    case 0:return Outsiders.IsDemon(t) || Outsiders.IsDevil(t);
    case 1:return Items.Undead(t);
    case 2:return ext!=null && ext.plane==OutsiderPlane.Elemental || t.def.defName.Contains("Elemental");
    case 3:return ext!=null && ext.plane==OutsiderPlane.Feywild;
   }
   return false;
  }
  static void MagicCircle(Pawn p,float radius,int ticks){
   var z=SpellZone.Make(p,p.Position,"Circle",radius,ticks);if(z==null)return;Items.Mote(p,"魔法阵",Arcane);
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed)return;
   Find.WindowStack.Add(new Dialog_SpellChoice("魔法阵","选一类生物：这类敌对生物进不来，阵内友军受它们的伤害 ×0.5。",Enumerable.Range(0,4).Select(i=>(CircleLabels[i],(string)null,(Action)(()=>{z.mode=i;Items.Mote(p,"魔法阵："+CircleLabels[i],Arcane);}))).ToList()));
  }
  // 亡者交谈：敌方派系的人形尸体，每具一次；揭示一处据点，或 200 × 奥术强度研究。
  public static string SpeakCannot(Pawn p,Corpse c){
   if(c==null || c.Destroyed)return "需要选择一具尸体。";var x=c.InnerPawn;if(x==null || !x.RaceProps.Humanlike)return "只能和人形的亡者交谈。";
   if(x.Faction==null || x.Faction.IsPlayer || !x.Faction.HostileTo(Faction.OfPlayer))return "只能问敌方派系的亡者。";
   if(Spellbook.Data(p)?.spoken.Contains(c.thingIDNumber)==true)return "这具尸体已经问过了。";
   return null;
  }
  public static Settlement Reveal(Faction f,Pawn p){
   if(f==null)return null;int tile=p.Map?.Tile??-1;
   var list=Find.WorldObjects.Settlements.Where(s=>s.Faction==f).ToList();if(list.Count==0)return null;
   return tile<0?list.RandomElement():list.OrderBy(s=>Find.WorldGrid.ApproxDistanceInTiles(tile,s.Tile)).First();
  }
  public static float Research(Pawn p,float amount){
   var rm=Find.ResearchManager;var proj=rm?.GetProject();if(proj==null)return 0f;
   rm.AddProgress(proj,amount,p);return amount;
  }
  static void SpeakWithDead(Pawn p,Corpse c,float research){
   if(SpeakCannot(p,c)!=null)return;var d=Spellbook.Data(p);var f=c.InnerPawn.Faction;
   Action reveal=()=>{d?.spoken.Add(c.thingIDNumber);var s=Reveal(f,p);
    if(s==null)Messages.Message("亡者摇头：「"+f.Name+"」已经没有据点了。",p,MessageTypeDefOf.NeutralEvent,false);
    else Find.LetterStack.ReceiveLetter("亡者交谈",c.InnerPawn.LabelShort+"的亡魂说出了「"+f.Name+"」的一处据点："+s.LabelCap+"。",LetterDefOf.NeutralEvent,new LookTargets(s));};
   Action learn=()=>{d?.spoken.Add(c.thingIDNumber);float got=Research(p,research);Messages.Message(got>0f?"亡魂的学识：当前研究 +"+got.ToString("0")+"。":"没有正在进行的研究，亡魂的学识白白散去了。",p,got>0f?MessageTypeDefOf.PositiveEvent:MessageTypeDefOf.NeutralEvent,false);};
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){learn();return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("亡者交谈："+c.InnerPawn.LabelShort,"这具尸体只能问一次。",new List<(string,string,Action)>{("问出「"+f.Name+"」的一处据点",null,reveal),("问它生前的学识：当前研究 +"+research.ToString("0"),null,learn)}));
  }
  // 送讯术：每天一次，选一个派系打开原版通讯对话。
  public static string SendingCannot(Pawn p){
   var d=Spellbook.Data(p)??CasterSpells.BookFor(p,DefCache<AbilityDef>.Get("LP_WS_Sending"));if(d==null)return "不会送讯术";int left=Spellbook.SendingTicks-(Now-d.sendingTick);
   if(left>0)return "送讯术每天一次（还要 "+(left/2500f).ToString("0.#")+" 小时）。";
   if(!Factions(p).Any())return "没有可以联络的派系。";
   return null;
  }
  public static IEnumerable<Faction> Factions(Pawn p)=>Find.FactionManager.AllFactionsVisibleInViewOrder.Where(f=>!f.IsPlayer && !f.defeated && !f.Hidden && f.leader!=null);
  public static bool Contact(Pawn p,Faction f){
   var d=Spellbook.Data(p)??CasterSpells.BookFor(p,DefCache<AbilityDef>.Get("LP_WS_Sending"));if(d==null || f==null)return false;d.sendingTick=Now; /* 1.131.0 吟游诗人也能用送讯术 */
   if(!Adventurer.TestSuppressed)Find.WindowStack.Add(new Dialog_Negotiation(p,f,FactionDialogMaker.FactionDialogFor(p,f),true));
   return true;
  }
  public static void Sending(Pawn p){
   if(SendingCannot(p)!=null)return;
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){Contact(p,Factions(p).First());return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("送讯术","选一个派系，和它的首领通话。",Factions(p).Select(f=>(f.Name+"（"+f.def.label+"）",(string)null,(Action)(()=>Contact(p,f)))).ToList(),()=>{}));
  }
  // 侦测魔法：列出地图上的魔法物品和传奇品质的物品。
  public static bool Magic(Thing t){
   if(t?.def==null || t.def.category!=ThingCategory.Item)return false;
   var q=t.TryGetComp<CompQuality>();if(q!=null && q.Quality==QualityCategory.Legendary)return true;
   return t.def.defName.StartsWith("LP_") && t.def.tradeTags!=null && t.def.tradeTags.Any(x=>x.Contains("Magic") || x.Contains("Wondrous") || x.Contains("Relic") || x.Contains("Scroll") || x.Contains("Arcane") || x.Contains("Legend"));
  }
  public static List<Thing> MagicItems(Map map)=>map==null?new List<Thing>():map.listerThings.AllThings.Where(t=>t.Spawned && Magic(t)).ToList();
  static void DetectLetter(Pawn p){
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed)return;var items=MagicItems(p.Map);
   string text=items.Count==0?"这张地图上没有感知到魔法物品。":"感知到 "+items.Count+" 件魔法物品或传奇品质的物品：\n"+string.Join("\n",items.Take(20).Select(t=>"· "+t.LabelCap))+(items.Count>20?"\n……":"");
   Find.LetterStack.ReceiveLetter("侦测魔法",text,LetterDefOf.NeutralEvent,new LookTargets(items.Take(20)));
  }
 }

 // 法术状态（隐形、闪现、防护能量、飞行、侦测魔法、心灵尖刺、活化死尸）：计数用于伤害钩子的快速返回。
 public class Hediff_WS : HediffWithComps {
  public static int Live;int acc;
  // 1.129.0：施法者、强度（魅影杀手、反转重力）、被支配者的原阵营、变形时收起的武器、报警间隔。
  public Pawn caster;public float power;public Faction origFaction;public Thing stowed;public int lastAlarm=-999999;
  public static readonly HashSet<Pawn> Counterspellers=new HashSet<Pawn>();
  bool Is(string n)=>def.defName==n;
  public override void PostAdd(DamageInfo? dinfo){
   base.PostAdd(dinfo);Live++;if(Is("LP_WS_Fly"))Spellbook.Fliers.Add(pawn);if(Is("LP_WS_Counterspell"))Counterspellers.Add(pawn);
   if(Is("LP_WS_Polymorphed")){var eq=pawn.equipment?.Primary;if(eq!=null && pawn.inventory!=null && pawn.equipment.TryTransferEquipmentToContainer(eq,pawn.inventory.innerContainer))stowed=eq;pawn.jobs?.StopAll();}
  }
  public override void PostRemoved(){
   base.PostRemoved();if(Live>0)Live--;if(Is("LP_WS_Fly"))Spellbook.Fliers.Remove(pawn);if(Is("LP_WS_Counterspell"))Counterspellers.Remove(pawn);
   if((Is("LP_WS_Animated") || Is("LP_WS_Summoned") || Is("LP_WS_Hound")) && pawn!=null && !pawn.Destroyed){var p=pawn;Fiends.Defer(()=>Unmake(p));}
   if(Is("LP_WS_Polymorphed") && stowed is ThingWithComps w && pawn!=null && !pawn.Dead && pawn.inventory!=null && pawn.inventory.innerContainer.Contains(w) && pawn.equipment!=null && pawn.equipment.Primary==null){pawn.inventory.innerContainer.Remove(w);pawn.equipment.AddEquipment(w);}
   if(Is("LP_WS_Dominated") && pawn!=null && !pawn.Dead){var p=pawn;var f=origFaction;Fiends.Defer(()=>WizardSpells2.Release(p,f));}
   if(Is("LP_WS_Levitating") && pawn!=null && !pawn.Dead && pawn.Spawned){var p=pawn;var c=caster;float pw=power;Fiends.Defer(()=>{if(!p.Dead){WizardSpells.Hit(c,p,DamageDefOf.Blunt,pw,.2f);Items.Mote(p,"摔落",Spellbook.Color);}});}
  }
  public override void ExposeData(){
   base.ExposeData();Scribe_References.Look(ref caster,"lpWsCaster");Scribe_Values.Look(ref power,"lpWsPower");Scribe_References.Look(ref origFaction,"lpWsFaction");Scribe_References.Look(ref stowed,"lpWsStowed");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){Live++;if(Is("LP_WS_Fly"))Spellbook.Fliers.Add(pawn);if(Is("LP_WS_Counterspell"))Counterspellers.Add(pawn);}
  }
  public static void Unmake(Pawn p){
   if(p==null || p.Destroyed)return;var map=p.MapHeld;var pos=p.PositionHeld;
   Necro.Registry?.constructs.Remove(p);if(p.Spawned && map!=null){FilthMaker.TryMakeFilth(pos,map,ThingDefOf.Filth_Ash,2);Items.Mote(p,"散架",new Color(.55f,.8f,.55f));}
   p.Destroy();
  }
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<120)return;acc=0;
   if(Is("LP_WS_DetectMagic"))Reveal(pawn,30f);else if(Is("LP_WS_MindSpiked"))Uncloak(pawn);
   else if(Is("LP_WS_TrueSeeing")){Reveal(pawn,30f);Items.Remove(pawn,"LP_WL_Darkened");Items.Remove(pawn,"LP_WS_Blinded");}
   else if(Is("LP_WS_Phantasm"))WizardSpells2.PhantasmTick(this);
   else if(Is("LP_WS_Hound"))WizardSpells2.HoundTick(this);
  }
  public static int Uncloak(Pawn t){
   if(t?.health==null)return 0;int n=0;
   foreach(var h in t.health.hediffSet.hediffs.Where(x=>x.TryGetComp<HediffComp_Invisibility>()!=null).ToList()){t.health.RemoveHediff(h);n++;}
   if(n>0)Items.Mote(t,"现形",Spellbook.Color);return n;
  }
  public static int Reveal(Pawn p,float radius){
   if(p?.Map==null)return 0;int n=0;
   foreach(var x in Races124.PawnsNear(p.Map,p.Position,radius).ToList())if(x!=p && !x.Dead && x.HostileTo(p) && Uncloak(x)>0){Items.Give(x,"LP_Spotted",600);n++;}
   return n;
  }
 }

 // 魔宠（寻获魔宠）。
 public static class Familiars {
  public static readonly HashSet<Pawn> Masters=new HashSet<Pawn>();
  static int Now=>Find.TickManager.TicksGame;
  static readonly (string kind,string label)[] Options={("Cat","黑猫"),("Rat","老鼠"),("LP_Tressym","翼猫"),("LP_Pseudodrake","伪龙")};
  public static List<(PawnKindDef kind,string label)> Kinds()=>Options.Select(o=>(DefDatabase<PawnKindDef>.GetNamedSilentFail(o.kind),o.label)).Where(o=>o.Item1!=null).ToList();
  public static string Cannot(Pawn p){
   var d=Spellbook.Data(p);if(d==null)return "不是法师";
   if(d.familiar!=null && !d.familiar.Dead && !d.familiar.Destroyed)return "已经有一只魔宠了（"+d.familiar.LabelShort+"）。";
   if(d.familiarDied>=0 && Now-d.familiarDied<Spellbook.FamiliarRespawn)return "魔宠死后要 1 天才能重召（还要 "+((Spellbook.FamiliarRespawn-(Now-d.familiarDied))/2500f).ToString("0.#")+" 小时）。";
   if(p.Map==null)return "要在地图上施放。";
   if(Kinds().Count==0)return "没有可用的魔宠种类。";
   return null;
  }
  public static void Summon(Pawn p){
   if(Cannot(p)!=null)return;var kinds=Kinds();
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){Make(p,kinds[0].kind);return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("寻获魔宠","选择魔宠的形态。",kinds.Select(k=>(k.label,(string)null,(Action)(()=>Make(p,k.kind)))).ToList()));
  }
  public static Pawn Make(Pawn p,PawnKindDef kind){
   if(Cannot(p)!=null || kind==null)return null;var map=p.Map;var d=Spellbook.Data(p);
   var a=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,Faction.OfPlayer));
   var cell=CellFinder.RandomClosewalkCellNear(p.Position,map,2);GenSpawn.Spawn(a,cell,map);
   Items.Give(a,"LP_WS_Familiar");
   try{
    if(a.training!=null){if(a.training.CanAssignToTrain(TrainableDefOf.Tameness).Accepted)a.training.Train(TrainableDefOf.Tameness,p,true);if(a.training.CanAssignToTrain(TrainableDefOf.Obedience).Accepted){a.training.Train(TrainableDefOf.Obedience,p,true);a.playerSettings.Master=p;}}
    if(p.relations!=null && a.relations!=null && !p.relations.DirectRelationExists(PawnRelationDefOf.Bond,a))p.relations.AddDirectRelation(PawnRelationDefOf.Bond,a);
   }catch(Exception ex){Log.Warning("[DND边缘大陆] 魔宠训练 / 羁绊失败："+ex.Message);}
   d.familiar=a;d.familiarDied=-1;Items.Give(p,"LP_WS_FamiliarBond");Masters.Add(p);
   FleckMaker.ThrowDustPuffThick(a.DrawPos,map,2f,Spellbook.Color);Items.Mote(a,"魔宠",Spellbook.Color);
   return a;
  }
  public static void Tick(Pawn p,SpellbookData d){
   if(d.familiar==null){if(Masters.Contains(p)){Masters.Remove(p);Items.Remove(p,"LP_WS_FamiliarBond");}return;}
   if(d.familiar.Dead || d.familiar.Destroyed){
    d.familiar=null;d.familiarDied=Now;Masters.Remove(p);Items.Remove(p,"LP_WS_FamiliarBond");
    if(Spellbook.Player(p) && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"的魔宠死了：1 天后可以重新寻获魔宠。",p,MessageTypeDefOf.NegativeEvent,false);
    return;
   }
   Masters.Add(p);if(!Items.Has(p,"LP_WS_FamiliarBond"))Items.Give(p,"LP_WS_FamiliarBond");
  }
  public static bool Near(Pawn p){
   var d=Spellbook.Data(p);var f=d?.familiar;return f!=null && !f.Dead && f.Spawned && p.Spawned && f.Map==p.Map && f.Position.DistanceTo(p.Position)<=Spellbook.FamiliarNear;
  }
 }

 // 秘法锁：每张地图记着锁上的门（存档）。
 public class MapComponent_WizardLocks : MapComponent {
  public List<Thing> doors=new List<Thing>();
  public MapComponent_WizardLocks(Map map):base(map){}
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref doors,"lpWizardLocks",LookMode.Reference);if(Scribe.mode==LoadSaveMode.PostLoadInit){if(doors==null)doors=new List<Thing>();doors.RemoveAll(x=>x==null);WizardLocks.Count+=doors.Count;}}
 }
 public static class WizardLocks {
  public static int Count;public const float Factor=1f/3f;
  static MapComponent_WizardLocks Of(Map m)=>m?.GetComponent<MapComponent_WizardLocks>();
  public static bool Locked(Thing d)=>Count>0 && d?.Map!=null && Of(d.Map)?.doors.Contains(d)==true;
  public static void Lock(Thing d){var c=Of(d?.Map);if(c==null || c.doors.Contains(d))return;c.doors.Add(d);Count++;}
  public static void Unlock(Thing d){var c=Of(d?.Map);if(c!=null && c.doors.Remove(d) && Count>0)Count--;}
 }
 [HarmonyPatch]
 public static class WizardLockDamage {
  static System.Reflection.MethodBase TargetMethod()=>AccessTools.Method(typeof(Building_Door),nameof(Thing.PreApplyDamage));
  static void Prefix(Thing __instance,ref DamageInfo dinfo){if(WizardLocks.Count>0 && __instance is Building_Door && dinfo.Amount>0f && WizardLocks.Locked(__instance)){dinfo.SetAmount(dinfo.Amount*WizardLocks.Factor);}}
 }
 [HarmonyPatch(typeof(Building_Door),nameof(Building_Door.GetGizmos))]
 public static class WizardLockGizmo {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Building_Door __instance){
   foreach(var g in __result)yield return g;
   if(!WizardLocks.Locked(__instance))yield break;var d=__instance;
   yield return new Command_Action{defaultLabel="解除秘法锁",defaultDesc="这扇门上了秘法锁：受到的伤害 ÷3。点击解除。",icon=ContentFinder<Texture2D>.Get("LP/Classes/Subclass4/FortFortify",false)??BaseContent.BadTex,action=()=>{WizardLocks.Unlock(d);Items.Mote(d,"秘法锁解除",Spellbook.Color);}};
  }
 }
 [HarmonyPatch(typeof(Thing),nameof(Thing.DeSpawn))]
 public static class WizardLockDespawn {
  static void Prefix(Thing __instance){if(WizardLocks.Count>0 && __instance is Building_Door)WizardLocks.Unlock(__instance);}
 }

 // 法术区域：油腻、蛛网、臭云、警报、魔法阵。
 public class SpellZone : Thing {
  public string kind;public float radius;public int endTick;public Pawn caster;public Faction faction;public int mode,lastAlarm=-999999;
  public float power;public List<IntVec3> line; // 1.129.0 火墙（直线格）、黑触手 / 延时爆裂火球的伤害
  public string csDmg,csHed,csLabel;public float csAp;public int csTicks; // 1.130.0 施法职业法术的通用区域（kind 以 CS 开头，效果在 CasterEffects.ZoneTick）
  Dictionary<int,int> hit=new Dictionary<int,int>();
  public static readonly List<SpellZone> Circles=new List<SpellZone>();
  static int Now=>Find.TickManager.TicksGame;
  public static SpellZone Make(Pawn caster,IntVec3 c,string kind,float radius,int ticks,int mode=0){
   var map=caster?.Map;if(map==null || !c.IsValid || !c.InBounds(map))return null;var def=DefCache<ThingDef>.Get("LP_WS_SpellZone");if(def==null)return null;
   var z=(SpellZone)ThingMaker.MakeThing(def);z.kind=kind;z.radius=radius;z.endTick=Now+ticks;z.caster=caster;z.faction=caster.Faction;z.mode=mode;
   GenSpawn.Spawn(z,c,map);
   if(kind=="Grease"){var fuel=DefCache<ThingDef>.Get("Filth_Fuel");if(fuel!=null)foreach(var cell in z.Cells)FilthMaker.TryMakeFilth(cell,map,fuel);}
   z.Puff(true);if(!kind.StartsWith("CS"))Items.Mote(caster,Label(kind),Spellbook.Color);
   return z;
  }
  static string Label(string k){switch(k){case "Grease":return "油腻术";case "Web":return "蛛网术";case "Stink":return "臭云术";case "Alarm":return "警报术";case "Circle":return "魔法阵";case "FireWall":return "火墙术";case "Tentacles":return "黑触手";case "Antipathy":return "厌恶之地";case "Delayed":return "延时爆裂火球";}return k!=null && k.StartsWith("CS")?"法术区域":k;}
  public IEnumerable<IntVec3> Cells=>line!=null && line.Count>0?line.Where(c=>c.InBounds(Map)):GenRadial.RadialCellsAround(Position,radius,true).Where(c=>c.InBounds(Map));
  public bool Covers(IntVec3 c)=>line!=null && line.Count>0?line.Contains(c):c.InHorDistOf(Position,radius);
  public bool Foe(Pawn x)=>x!=null && !x.Dead && x.Spawned && (faction!=null?x.HostileTo(faction):caster!=null && x.HostileTo(caster));
  public IEnumerable<Pawn> Inside=>line!=null && line.Count>0?line.Where(c=>c.InBounds(Map)).SelectMany(c=>c.GetThingList(Map).OfType<Pawn>()).ToList():Races124.PawnsNear(Map,Position,radius).ToList();
  public override void SpawnSetup(Map map,bool respawningAfterLoad){base.SpawnSetup(map,respawningAfterLoad);if(kind=="Circle" && !Circles.Contains(this))Circles.Add(this);}
  public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish){Circles.Remove(this);base.DeSpawn(mode);}
  protected override void Tick(){
   base.Tick();if(!Spawned)return;
   if(Now>=endTick){End();return;}
   if(!this.IsHashIntervalTick(30))return;
   var map=Map;
   switch(kind){
    case "Grease":case "Web":
     if(Burning()){foreach(var c in Cells)FireUtility.TryStartFireIn(c,map,.6f,caster);Items.Mote(this,"着火了",new Color(1f,.6f,.3f));End();return;}
     foreach(var x in Inside){if(!Foe(x) || x.Downed)continue;
      if(kind=="Grease"){if(!Due(x,60))continue;if(Rand.Chance(.5f)){x.stances?.stunner?.StunFor(120,caster);Items.Mote(x,"滑倒",Spellbook.Color);}}
      else{if(!Due(x,300))continue;if(Rand.Chance(WizardSpells.Chance(.6f,caster,x))){x.stances?.stunner?.StunFor(240,caster);Items.Mote(x,"被蛛网粘住",Spellbook.Color);}}
     }
     break;
    case "Stink":
     foreach(var x in Inside){if(!Foe(x) || x.Downed || !x.RaceProps.IsFlesh || !Due(x,120))continue;if(Rand.Chance(.5f)){x.stances?.stunner?.StunFor(90,caster);Items.Mote(x,"呕吐",new Color(.55f,.8f,.3f));if(x.RaceProps.Humanlike && x.jobs!=null)x.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Vomit),JobCondition.InterruptForced,null,true);}}
     break;
    case "Alarm":
     if(Now-lastAlarm<2500)break;var foe=Inside.FirstOrDefault(x=>Foe(x) && !x.Downed);if(foe==null)break;lastAlarm=Now;
     if(caster!=null && !caster.Dead && !caster.Awake() && caster.jobs!=null)caster.jobs.EndCurrentJob(JobCondition.InterruptForced);
     if(faction!=null && faction.IsPlayer && !Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("警报术","有敌人闯进了"+(caster?.LabelShort??"法师")+"布下的警报术："+foe.LabelShort+"。",LetterDefOf.ThreatSmall,new LookTargets(foe));
     break;
    case "Circle":
     foreach(var x in Inside){if(!Foe(x) || !WizardSpells.CircleKind(x,mode))continue;Expel(x);}
     break;
    case "FireWall": // 1.129.0 火墙：墙上的敌人每秒受火焰伤害
     if(!this.IsHashIntervalTick(60))break;foreach(var x in Inside){if(!Foe(x))continue;WizardSpells.Hit(caster,x,DamageDefOf.Flame,power,.2f);}
     foreach(var c in Cells)if(Rand.Chance(.3f))FleckMaker.ThrowFireGlow(c.ToVector3Shifted(),map,.8f);
     break;
    case "Tentacles": // 黑触手：成功率 55% 缠住（定身 5 秒），被缠住的每秒受钝器伤害
     if(!this.IsHashIntervalTick(60))break;
     foreach(var x in Inside){if(!Foe(x) || x.Downed)continue;if(Due(x,300) && Rand.Chance(WizardSpells.Chance(.55f,caster,x))){x.stances?.stunner?.StunFor(300,caster);Items.Mote(x,"被触手缠住",Spellbook.Color);}
      if(x.stances?.stunner?.Stunned==true)WizardSpells.Hit(caster,x,DamageDefOf.Blunt,power,.2f);}
     break;
    case "Antipathy": // 厌恶之地：这一类敌对单位每 2 秒成功率 50% 被赶出去
     if(!this.IsHashIntervalTick(120))break;foreach(var x in Inside){if(!Foe(x) || !WizardSpells2.AntipathyKind(x,mode) || !Due(x,600))continue;if(Rand.Chance(.5f)){Expel(x);Items.Mote(x,"厌恶",Spellbook.Color);}}
     break;
    default:if(kind!=null && kind.StartsWith("CS"))CasterEffects.ZoneTick(this);break; // 1.130.0
   }
   if(this.IsHashIntervalTick(90))Puff(false);
  }
  public bool Due(Pawn x,int gap){hit.TryGetValue(x.thingIDNumber,out int t);if(t>0 && Now-t<gap)return false;hit[x.thingIDNumber]=Now;return true;}
  bool Burning(){foreach(var c in Cells)if(c.ContainsStaticFire(Map) || c.GetFirstThing<Fire>(Map)!=null)return true;return false;}
  public void Expel(Pawn x){
   var dir=(x.Position-Position).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(0f,0f,1f);
   int cells=Mathf.CeilToInt(radius-x.Position.DistanceTo(Position))+1;var dest=DNDPush.Destination(Map,x.Position,dir,cells);
   if(Covers(dest)){ // 这个方向被挡住：改放到阵外最近的空格
    var map=Map;var from=x.Position;IntVec3 best=IntVec3.Invalid;float bd=float.MaxValue;
    foreach(var c in GenRadial.RadialCellsAround(Position,radius+3f,true)){if(Covers(c) || !c.InBounds(map) || !c.Standable(map) || c.GetFirstPawn(map)!=null)continue;float dd=c.DistanceToSquared(from);if(dd<bd){bd=dd;best=c;}}
    if(best.IsValid)dest=best;
   }
   if(dest!=x.Position)DNDPush.Move(x,dest);x.stances?.stunner?.StunFor(30,caster);Items.Mote(x,"被魔法阵弹开",Spellbook.Color);
  }
  void Puff(bool all){
   var map=Map;if(map==null)return;Color c=kind=="Stink"?new Color(.5f,.75f,.25f):kind=="Web"?new Color(.9f,.9f,.9f):kind=="Grease"?new Color(.35f,.3f,.2f):kind=="Circle"?new Color(.6f,.75f,1f):new Color(.7f,.5f,1f);
   var cells=Cells.ToList();if(cells.Count==0)return;int n=all?Mathf.Min(cells.Count,12):kind=="Alarm"?1:3;
   for(int i=0;i<n;i++)FleckMaker.ThrowDustPuffThick(cells.RandomElement().ToVector3Shifted(),map,kind=="Stink"?2f:1f,c);
  }
  public void End(){
   if(kind=="Delayed" && Map!=null && caster!=null)GenExplosion.DoExplosion(Position,Map,radius,DamageDefOf.Flame,caster,damAmount:Mathf.RoundToInt(power),armorPenetration:.3f,explosionSound:DamageDefOf.Flame.soundExplosion); // 1.129.0 延时爆裂火球
   if(kind=="Grease" && Map!=null){var fuel=DefCache<ThingDef>.Get("Filth_Fuel");if(fuel!=null)foreach(var c in Cells.ToList())foreach(var f in c.GetThingList(Map).Where(t=>t.def==fuel).ToList())f.Destroy();}
   if(!Destroyed)Destroy();
  }
  public override void ExposeData(){
   base.ExposeData();Scribe_Values.Look(ref kind,"kind");Scribe_Values.Look(ref radius,"radius");Scribe_Values.Look(ref endTick,"endTick");Scribe_References.Look(ref caster,"caster");
   Scribe_References.Look(ref faction,"faction");Scribe_Values.Look(ref mode,"mode");Scribe_Values.Look(ref lastAlarm,"lastAlarm",-999999);
   Scribe_Values.Look(ref power,"power");Scribe_Collections.Look(ref line,"line",LookMode.Value);
   Scribe_Values.Look(ref csDmg,"csDmg");Scribe_Values.Look(ref csHed,"csHed");Scribe_Values.Look(ref csLabel,"csLabel");Scribe_Values.Look(ref csAp,"csAp");Scribe_Values.Look(ref csTicks,"csTicks");
  }
 }

 // 闪现（50% 落空）、防护能量（×0.5）、魔法阵（阵内友军受那类生物伤害 ×0.5）、隐形的人攻击就现形。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class WizardSpellDamage {
  // 1.131.0 性能热修：有任何法术状态在场时每次受伤要查 7～8 个状态，定义改成静态 DefRef（原来每次按名字查缓存字典）。
  static readonly DefRef<HediffDef> invisible=new DefRef<HediffDef>("LP_WS_Invisible"),blink=new DefRef<HediffDef>("LP_WS_Blink"),ethereal=new DefRef<HediffDef>("LP_WS_Ethereal"),mindBlank=new DefRef<HediffDef>("LP_WS_MindBlank"),foresight=new DefRef<HediffDef>("LP_WS_Foresight"),stoneskin=new DefRef<HediffDef>("LP_WS_Stoneskin");
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(Hediff_WS.Live<=0 && SpellZone.Circles.Count==0)return true;
   if(absorbed || dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return true;
   var v=__instance;var a=dinfo.Instigator as Pawn;
   if(Hediff_WS.Live>0){
    if(a!=null && a!=v && Items.Has(a,invisible.Def)){var att=a;Fiends.Defer(()=>{Items.Remove(att,"LP_WS_Invisible");Items.Mote(att,"现形",Spellbook.Color);});}
    if(a!=null && a!=v && a.HostileTo(v) && (HeroCombat.Melee(dinfo) || dinfo.Weapon!=null || dinfo.Def.isRanged || dinfo.Def.isExplosive) && Items.Has(v,blink.Def) && Rand.Chance(.5f)){Items.Mote(v,"闪现：落空",Spellbook.Color);absorbed=true;return false;}
    int k=WizardSpells.EnergyKind(dinfo.Def);if(k>=0 && Items.Has(v,WizardSpells.ProtHediffs[k]))dinfo.SetAmount(dinfo.Amount*.5f);
    // 1.129.0：以太化（不受伤；攻击就回来）、心灵屏障（免疫心灵伤害）、预警术（50% 躲开）、石肤术（利器 / 钝器 ×0.6）
    if(a!=null && a!=v && Items.Has(a,ethereal.Def)){var att=a;Fiends.Defer(()=>{Items.Remove(att,"LP_WS_Ethereal");Items.Mote(att,"回到物质位面",Spellbook.Color);});}
    if(Items.Has(v,ethereal.Def) || ClassSubclass.Psychic(dinfo.Def) && Items.Has(v,mindBlank.Def)){absorbed=true;return false;}
    if(a!=null && a!=v && a.HostileTo(v) && (HeroCombat.Melee(dinfo) || dinfo.Weapon!=null || dinfo.Def.isRanged || dinfo.Def.isExplosive) && Items.Has(v,foresight.Def) && Rand.Chance(.5f)){Items.Mote(v,"预警：躲开",Spellbook.Color);absorbed=true;return false;}
    var arm=dinfo.Def.armorCategory?.defName;if((arm=="Sharp" || arm=="Blunt") && Items.Has(v,stoneskin.Def))dinfo.SetAmount(dinfo.Amount*.6f);
   }
   if(SpellZone.Circles.Count>0 && a!=null && a.HostileTo(v))
    foreach(var z in SpellZone.Circles)if(z.Spawned && z.Map==v.Map && z.Covers(v.Position) && (z.faction==null || !v.HostileTo(z.faction)) && WizardSpells.CircleKind(a,z.mode)){dinfo.SetAmount(dinfo.Amount*.5f);break;}
   return true;
  }
 }
}
