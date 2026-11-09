using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.133.0 魔力湖泊第三批：通用秘传（Defs/ManaLake_Lore.xml，ArcanumExt.cls 为空）。复用 1.100.0 的秘传槽、结晶吸收流程、1 小时公共冷却和冒险者页签。
 // 门槛：冒险者 11 / 13 / 15 / 17 级、施法属性 15 / 16 / 17 / 18（武技看力量或敏捷，另需格斗或射击 10 / 12 / 14 / 16；被动看最高的一项属性）。
 // 吸收成功率 80 / 70 / 60 / 50% + 属性每超过门槛 1 点 5%，最高 100%；失败结晶不碎，秘传反噬 3 天，7 天后才能再试。
 // 强度 = max(属性强度 1 + 调整值 × 5%（最多 +50%），自己所学职业的最高职业强度)；法术再乘心灵敏感度（最低 ×0.2）。超魔、魔力奔流、法术强效都不作用于秘传（效果不走 ClassEffects.Power）。
 public static class LakeLore {
  public const string WishName="LP_AC_Wish";
  public const int FailLock=7*60000,TimeStopTicks=360,LegendResistPerDay=3,HeartCooldown=10*60000;
  public const float WishRisk=1f/3f,WishItemValue=3000f;
  // 任何人学过通用秘传 / 湖印、或者有人被沉默 / 时间停止时才打开各处挂钩（读档后在 Rescan 里重新判断）。
  public static bool Live;
  public static void Reset(){Live=false;LakeTime.End();}
  public static void Rescan(){
   if(Current.Game==null)return;
   foreach(var p in PawnsFinder.AllMapsWorldAndTemporary_Alive){var c=Adventurer.Get(p)?.classes;if(c==null)continue;if(c.lake!=null && c.lake.seals.Count>0 || c.arcana.Any(a=>LakeArcana.Ext(a)?.Universal==true)){Live=true;LakeArcana.Touched=true;return;}}
  }

  // ---------- 门槛与吸收 ----------
  public static int LevelFor(int ring)=>ring<=6?11:ring==7?13:ring==8?15:17;
  public static int ScoreGate(int ring)=>ring<=6?15:ring==7?16:ring==8?17:18;
  public static int SkillGate(int ring)=>ring<=6?10:ring==7?12:ring==8?14:16;
  public static float BaseChance(int ring)=>ring<=6?.8f:ring==7?.7f:ring==8?.6f:.5f;
  public static int AbilityIdx(string a){switch(a){case "Str":return Adventurer.Str;case "Dex":return Adventurer.Dex;case "Con":return Adventurer.Con;case "Int":return Adventurer.Int;case "Wis":return Adventurer.Wis;case "Cha":return Adventurer.Cha;}return -1;}
  public static string AbilityText(ArcanumExt e)=>e.Martial?"力量或敏捷":e.Passive || AbilityIdx(e.ability)<0?"最高的一项属性":Adventurer.Names[AbilityIdx(e.ability)];
  // 门槛用的属性值：法术看施法属性，武技看力量 / 敏捷较高，被动看最高。
  public static int Score(Hediff_Adventurer h,ArcanumExt e){
   if(h==null || e==null)return 0;
   if(e.Martial)return Mathf.Max(h.Eff(Adventurer.Str),h.Eff(Adventurer.Dex));
   int i=AbilityIdx(e.ability);if(i>=0 && !e.Passive)return h.Eff(i);
   int best=0;for(int k=0;k<6;k++)best=Mathf.Max(best,h.Eff(k));return best;
  }
  public static int MartialSkill(Pawn p)=>p?.skills==null?0:Mathf.Max(p.skills.GetSkill(SkillDefOf.Melee).Level,p.skills.GetSkill(SkillDefOf.Shooting).Level);
  public static string CannotLearn(Pawn p,Hediff_Adventurer h,ArcanumExt e){
   int lv=LevelFor(e.ring);if(h.level<lv)return "需要冒险者 "+lv+" 级（"+e.ring+" 环通用秘传）";
   int gate=ScoreGate(e.ring);if(Score(h,e)<gate)return "需要"+AbilityText(e)+" "+gate+"（现在 "+Score(h,e)+"）";
   if(e.Martial && MartialSkill(p)<SkillGate(e.ring))return "武技另需格斗或射击 "+SkillGate(e.ring)+" 级";
   var d=h.classes?.lake;if(d!=null){int left=d.loreFail+FailLock-Find.TickManager.TicksGame;if(d.loreFail>0 && left>0)return "上次吸收失败，还要 "+left.ToStringTicksToPeriod()+"才能再试";}
   return null;
  }
  public static float AbsorbChance(Pawn p,AbilityDef d){var e=LakeArcana.Ext(d);var h=Adventurer.Get(p);if(e==null || h==null)return 0f;return Mathf.Clamp01(BaseChance(e.ring)+.05f*(Score(h,e)-ScoreGate(e.ring)));}
  public static string ChanceText(Pawn p,AbilityDef d)=>"吸收成功率 "+AbsorbChance(p,d).ToStringPercent();
  // 结晶吸收：成功学会（返回 true，调用方用掉结晶）；失败秘传反噬 3 天、7 天后再试，结晶不碎。
  public static bool TryAbsorb(Pawn p,AbilityDef d,float roll,bool message){
   if(LakeArcana.CannotLearn(p,d)!=null)return false;
   float ch=AbsorbChance(p,d);
   if(roll<ch){bool ok=LakeArcana.Learn(p,d,message);if(ok)Live=true;return ok;}
   var h=Adventurer.Get(p);LakeSeals.Data(h).loreFail=Find.TickManager.TicksGame;Items.Give(p,"LP_LO_Backlash");
   if(message && p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"没能吸收「"+d.label+"」（成功率 "+ch.ToStringPercent()+"）：秘传反噬 3 天，结晶还在，7 天后可以再试。",p,MessageTypeDefOf.NegativeEvent,false);
   return false;
  }

  // ---------- 强度 ----------
  public static float AttrStrength(Pawn p,ArcanumExt e){var h=Adventurer.Get(p);if(h==null)return 1f;return 1f+Mathf.Clamp(Adventurer.Mod(Score(h,e))*.05f,0f,.5f);}
  public static float ClassStrength(Pawn p){
   var h=Adventurer.Get(p);if(h?.classes==null)return 0f;float best=0f;
   foreach(var c in ClassSystem.Taken(h))foreach(var s in ClassLevelInfo.Stats(c))best=Mathf.Max(best,p.GetStatValue(s));
   return best;
  }
  public static float Strength(Pawn p,AbilityDef def){
   var e=LakeArcana.Ext(def);if(p==null || e==null)return 1f;
   float s=Mathf.Max(AttrStrength(p,e),ClassStrength(p));
   if(!e.Martial)s*=Mathf.Max(ClassSystem.PsychicMin,p.GetStatValue(StatDefOf.PsychicSensitivity));
   return s;
  }

  // ---------- 施放前检查（所有职业能力都会问：沉默） ----------
  public static string CannotCast(Pawn p,AbilityDef def){
   if(Items.Has(p,"LP_LS_Silenced"))return "被反制法术沉默了";
   return null;
  }
  static bool Hostile(Pawn c,Pawn t)=>AnyTarget.Hostile(c,t); // 1.136.0 热修：手动选的目标不分敌我
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;var map=caster.Map;
   switch(e.effect){
    case "LO_ChainLightning":case "LO_FingerOfDeath":case "LO_Maze":case "LO_PowerWordKill":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
    case "LO_Heal":case "LO_Regenerate":case "LO_MindBlank":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(t.HostileTo(caster))return "只能对友军使用。";return null;
    case "LO_BladeBarrier":case "LO_ReverseGravity":case "LO_Sunburst":
     if(map==null || !target.Cell.InBounds(map))return "需要选择地图上的一处。";return null;
    case "LO_SunArrow":if(Martial.RangedVerb(caster)?.GetProjectile()==null)return "需要远程武器。";if(map==null || !target.Cell.InBounds(map) || target.Cell==caster.Position)return "需要选择一个方向。";return null;
   }
   return null;
  }

  // ---------- 施放 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   Live=true;LakeArcana.Touched=true;
   var t=target.Thing as Pawn;float s=Strength(p,def);
   switch(e.effect){
    case "LO_ChainLightning":if(Hostile(p,t))ChainLightning(p,t,e.power*s,e.range>0f?e.range:6f);return true;
    case "LO_BladeBarrier":ArcanaZones.Add(p,ArcanaZones.Blade,target.Cell,0f,e.duration,e.power*s);Items.Mote(p,"剑刃屏障",ArcanaEffects.Lake);return true;
    case "LO_Heal":if(t!=null)Heal(t,e.power*s);return true;
    case "LO_FingerOfDeath":if(Hostile(p,t))FingerOfDeath(p,t,e.power*s);return true;
    case "LO_Regenerate":if(t!=null)Regenerate(t,e.duration);return true;
    case "LO_ReverseGravity":ReverseGravity(p,target.Cell,e.radius,e.duration,e.power*s);return true;
    case "LO_Sunburst":Sunburst(p,target.Cell,e.radius,e.power*s,e.duration);return true;
    case "LO_MindBlank":if(t!=null){Items.Give(t,"LP_LO_MindBlank",e.duration);Items.Mote(t,"心灵空白",ArcanaEffects.Lake);}return true;
    case "LO_Maze":if(Hostile(p,t))LakeMaze.Banish(p,t,ClassMartial2.Boss(t)?600:e.duration);return true;
    case "LO_TimeStop":LakeTime.Start(p,e.duration>0?e.duration:TimeStopTicks);return true;
    case "LO_PowerWordKill":if(Hostile(p,t))PowerWordKill(p,t,e.power*s);return true;
    case "LO_Wish":Wish(p,def);return true;
    case "LO_BoneBreak":Items.Give(p,"LP_LO_BoneBreak",e.duration,e.power*s);Items.Mote(p,"断骨击",ClassEffects.MartialColor);return true;
    case "LO_SunArrow":SunArrow(p,target.Cell,e.power*s,e.range>0f?e.range:40f);return true;
   }
   return false;
  }
  static DamageDef Dmg(string n,DamageDef fallback)=>DefCache<DamageDef>.Get(n)??fallback;
  public static int ChainLightning(Pawn p,Pawn first,float power,float jump){
   var hit=new List<Pawn>();var cur=first;var map=p.Map;var line=DefCache<FleckDef>.Get("PsycastPsychicLine");Vector3 from=p.DrawPos;
   while(cur!=null && hit.Count<4){
    hit.Add(cur);if(map!=null){if(line!=null)FleckMaker.ConnectingLine(from,cur.DrawPos,line,map);FleckMaker.ThrowLightningGlow(cur.DrawPos,map,1.5f);}
    from=cur.DrawPos;var at=cur.Position;var m=cur.MapHeld??map;
    cur.TakeDamage(new DamageInfo(Dmg("LP_Lightning",DamageDefOf.Burn),power*(cur.RaceProps.IsMechanoid?1.5f:1f),.2f,-1f,p));
    cur=m==null?null:Races124.PawnsNear(m,at,jump).Where(x=>Hostile(p,x) && !hit.Contains(x)).OrderBy(x=>x.Position.DistanceToSquared(at)).FirstOrDefault();
   }
   Items.Mote(p,"连锁闪电",new Color(.6f,.8f,1f));return hit.Count;
  }
  public static bool BlindHediff(Hediff h)=>h!=null && h.def.isBad && h.def.stages!=null && h.def.stages.Any(s=>s.capMods!=null && s.capMods.Any(c=>c.capacity==PawnCapacityDefOf.Sight && c.setMax<.5f)) && !(h is Hediff_Injury) && !(h is Hediff_MissingPart);
  public static float Heal(Pawn t,float frac){
   float amount=ClassMelee.InjurySum(t)*Mathf.Clamp01(frac);ClassMelee.Heal(t,amount);
   foreach(var h in t.health.hediffSet.hediffs.Where(h=>BlindHediff(h) || ClassCasters.IsDisease(h) || h.def==HediffDefOf.FoodPoisoning).ToList())t.health.RemoveHediff(h);
   Items.Mote(t,"治愈术",ArcanaEffects.Holy);return amount;
  }
  public static bool FingerOfDeath(Pawn p,Pawn t,float power){
   t.TakeDamage(new DamageInfo(Dmg("LP_Necrotic",DamageDefOf.Burn),power,.5f,-1f,p));Items.Mote(t,"死亡之指",new Color(.5f,.8f,.5f));
   if(!t.Dead || !t.RaceProps.Humanlike || Items.Undead(t))return false;
   return RaiseThrall(p,t);
  }
  public static bool RaiseThrall(Pawn p,Pawn t){
   if(t.Corpse==null || !t.Corpse.Spawned)return false;
   if(!ResurrectionUtility.TryResurrect(t,new ResurrectionParams{removeDiedThoughts=true,restoreMissingParts=true}))return false;
   if(t.Faction!=p.Faction)t.SetFaction(p.Faction);t.guest?.SetGuestStatus(null);
   Items.Give(t,"LP_LO_Thrall");Items.Mote(t,"亡仆",new Color(.5f,.8f,.5f));
   if(p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(t.LabelShort+"被死亡之指杀死，又作为亡仆爬了起来。",t,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  public static bool Regenerate(Pawn t,int ticks){
   var scar=t.health.hediffSet.hediffs.OfType<Hediff_Injury>().FirstOrDefault(i=>i.IsPermanent());if(scar!=null)t.health.RemoveHediff(scar);
   var h=Items.Give(t,"LP_LO_Regenerating") as Hediff_LoreRegen;if(h!=null)h.left=ticks;
   Items.Mote(t,"再生术",new Color(.6f,.95f,.6f));return scar!=null;
  }
  public static int ReverseGravity(Pawn p,IntVec3 c,float radius,int ticks,float power){
   int n=0;var map=p.Map;if(map==null)return 0;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(map) && Rand.Chance(.3f))FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1f,new Color(.75f,.6f,1f));
   foreach(var t in Races124.PawnsNear(map,c,radius).ToList()){if(!Hostile(p,t))continue;t.stances?.stunner?.StunFor(ticks,p,false);var h=Items.Give(t,"LP_LO_Levitated",-1,power) as Hediff_LoreLevitate;if(h!=null){h.left=ticks;h.caster=p;}n++;}
   Items.Mote(p,"反转重力",new Color(.75f,.6f,1f));return n;
  }
  public static int Sunburst(Pawn p,IntVec3 c,float radius,float power,int blind){
   int n=0;var map=p.Map;if(map==null)return 0;var holy=Dmg("LP_Radiant",DamageDefOf.Burn);
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(map) && Rand.Chance(.35f))FleckMaker.ThrowLightningGlow(cell.ToVector3Shifted(),map,1.2f);
   foreach(var t in Races124.PawnsNear(map,c,radius).ToList()){
    if(t==p || t.Dead)continue;
    t.TakeDamage(new DamageInfo(holy,power*(Items.Undead(t)?2f:1f),.3f,-1f,p));
    if(!t.Dead && !TrueSighted(t))Items.Give(t,"LP_ACB_Blinded",blind);n++;
   }
   Items.Mote(p,"炎阳爆",ArcanaEffects.Holy);return n;
  }
  public static bool PowerWordKill(Pawn p,Pawn t,float power){
   if(!ClassMartial2.Boss(t) && ArcanaEffects.Health(t)<=.4f){t.Kill(new DamageInfo(Dmg("LP_WS_Psychic",DamageDefOf.Burn),999f,99f,-1f,p));Items.Mote(t,"真言·杀",new Color(.85f,.5f,.9f));return true;}
   t.TakeDamage(new DamageInfo(Dmg("LP_WS_Psychic",DamageDefOf.Burn),power,1f,-1f,p));Items.Mote(t,"真言·杀",new Color(.85f,.5f,.9f));return false;
  }
  // 贯日一箭：从施法者朝目标方向 40 格的直线上，所有敌人各吃一次 4 倍武器伤害。
  public static int SunArrow(Pawn p,IntVec3 toward,float mult,float range){
   var map=p.Map;var verb=Martial.RangedVerb(p);var proj=verb?.GetProjectile();if(map==null || proj?.projectile==null)return 0;
   var weapon=p.equipment?.Primary;float dmg=proj.projectile.GetDamageAmount(weapon)*mult,ap=proj.projectile.GetArmorPenetration(weapon);var def=proj.projectile.damageDef??DamageDefOf.Bullet;
   var dir=(toward-p.Position).ToVector3();dir.y=0f;if(dir.sqrMagnitude<.01f)return 0;dir.Normalize();
   var hit=new HashSet<Pawn>();Vector3 start=p.Position.ToVector3Shifted();IntVec3 last=p.Position;
   for(float i=1f;i<=range;i+=.5f){
    var cell=(start+dir*i).ToIntVec3();if(!cell.InBounds(map))break;last=cell;
    foreach(var t in cell.GetThingList(map).OfType<Pawn>().ToList())if(Hostile(p,t) && hit.Add(t))t.TakeDamage(new DamageInfo(def,dmg,ap,dir.AngleFlat(),p,null,weapon?.def));
   }
   var line=DefCache<FleckDef>.Get("PsycastPsychicLine");if(line!=null)FleckMaker.ConnectingLine(p.DrawPos,last.ToVector3Shifted(),line,map);
   Items.Mote(p,"贯日一箭",new Color(1f,.85f,.4f));return hit.Count;
  }
  // 断骨击：打断一条腿或一只手臂（骨折 10 天）；传奇生物改为钝器重击。
  public static bool BreakBone(Pawn a,Pawn v,float power){
   if(v==null || v.Dead)return false;
   if(ClassMartial2.Boss(v)){v.TakeDamage(new DamageInfo(DamageDefOf.Blunt,power,0f,-1f,a));Items.Mote(v,"断骨击",ClassEffects.MartialColor);return true;}
   var frac=Items.Hediff("LP_LO_Fracture");if(frac==null)return false;
   var parts=v.health.hediffSet.GetNotMissingParts().Where(x=>x.def.tags!=null && (x.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore) || x.def.tags.Contains(BodyPartTagDefOf.ManipulationLimbCore)) && !v.health.hediffSet.hediffs.Any(h=>h.def==frac && h.Part==x)).ToList();
   if(parts.Count==0)return false;
   var part=parts.RandomElement();v.health.AddHediff(HediffMaker.MakeHediff(frac,v,part),part);
   Items.Mote(v,"骨折："+part.LabelCap,ClassEffects.MartialColor);return true;
  }

  // ---------- 许愿术 ----------
  public static List<AbilityDef> WishSpells=>LakeArcana.All.Where(d=>{var e=LakeArcana.Ext(d);return e!=null && e.Universal && e.kind=="spell" && e.ring<=8;}).OrderBy(d=>LakeArcana.Ext(d).ring).ToList();
  static void Wish(Pawn p,AbilityDef def){
   if(Adventurer.TestSuppressed || p.Faction!=Faction.OfPlayer)return;
   Find.WindowStack.Add(new Dialog_LoreWish(p,def));
  }
  // 安全愿望一：直接施放一个 8 环或以下的通用秘传法术（不需要学过，不进那个法术的冷却）。
  public static bool WishCast(Pawn p,AbilityDef spell,LocalTargetInfo target){
   var e=spell?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();if(e==null)return false;
   if(Invalid(p,e,target)!=null)return false;
   return Apply(p,spell,e,target);
  }
  // 安全愿望二：本地图所有殖民者伤势全愈、疾病清除。
  public static int WishHealAll(Map map){
   int n=0;if(map==null)return 0;
   foreach(var c in map.mapPawns.FreeColonistsSpawned.ToList()){
    foreach(var h in c.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury inj && !inj.IsPermanent() || ClassCasters.IsDisease(h) || h.def==HediffDefOf.FoodPoisoning || h.def==HediffDefOf.BloodLoss).ToList())c.health.RemoveHediff(h);
    Items.Mote(c,"许愿：痊愈",ArcanaEffects.Holy);n++;
   }
   return n;
  }
  // 安全愿望三：变出市场价合计 3000 银以内的一种物品。
  public static readonly string[] WishGoods={"MedicineUltratech","ComponentSpacer","Plasteel","Gold","MealSurvivalPack","Uranium"};
  public static int WishItems(Pawn p,string thing){
   var d=DefDatabase<ThingDef>.GetNamedSilentFail(thing);if(d==null || p.Map==null)return 0;
   int count=Mathf.Max(1,Mathf.FloorToInt(WishItemValue/Mathf.Max(.01f,d.BaseMarketValue)));int left=count;
   while(left>0){var t=ThingMaker.MakeThing(d);t.stackCount=Mathf.Min(left,d.stackLimit);left-=t.stackCount;GenPlace.TryPlaceThing(t,p.Position,p.Map,ThingPlaceMode.Near);}
   Items.Mote(p,"许愿："+d.label,ArcanaEffects.Lake);return count;
  }
  // 大愿望：复活、永久 +1 属性、驱散长期事件；33% 失去许愿术并虚弱 10 天。
  public static bool WishRevive(Pawn p,Pawn dead)=>CasterEffects.Resurrect(p,dead);
  public static bool WishScore(Pawn p,int a){var h=Adventurer.Get(p);if(h==null || a<0 || a>5 || h.scores[a]>=30)return false;h.scores[a]++;h.Dirty();Items.Mote(p,"许愿："+Adventurer.Names[a]+" +1",ArcanaEffects.Lake);return true;}
  public static List<GameCondition> LongConditions(Map map)=>map?.gameConditionManager?.ActiveConditions.Where(c=>c.Permanent || c.TicksLeft>=60000).ToList()??new List<GameCondition>();
  public static bool WishEndCondition(GameCondition c){if(c==null)return false;c.End();return true;}
  public static bool GreatWishCost(Pawn p,float roll){
   if(roll>=WishRisk)return false;
   var h=Adventurer.Get(p);var w=DefCache<AbilityDef>.Get(WishName);
   if(h?.classes!=null && w!=null){h.classes.arcana.Remove(w);p.abilities?.RemoveAbility(w);}
   Items.Give(p,"LP_LO_Weakened");
   if(p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("许愿的代价",p.LabelShort+"的大愿望实现了，但魔网收回了许愿术：永久失去许愿术，并虚弱 10 天。",LetterDefOf.NegativeEvent,p);
   return true;
  }

  // ---------- 被动（每 250 刻，学过秘传的人） ----------
  public static bool TrueSighted(Pawn p)=>LakeArcana.Has(p,"TrueSight");
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;if(p==null || !p.Spawned)return;
   if(LakeArcana.Has(p,"TrueSight")){
    foreach(var b in p.health.hediffSet.hediffs.Where(BlindHediff).ToList())p.health.RemoveHediff(b);
    Reveal(p,30f);
   }
  }
  // 真视 / 魔网感知：范围里隐形的敌人现形，并被识破 1 小时。
  public static int Reveal(Pawn p,float radius){
   int n=0;if(p.Map==null)return 0;var inv=DefCache<HediffDef>.Get("PsychicInvisibility");
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,radius).ToList()){
    if(!Hostile(p,t))continue;bool hidden=false;
    foreach(var hd in t.health.hediffSet.hediffs)if(hd is HediffWithComps hc && hc.comps!=null && hc.comps.Any(c=>c is HediffComp_Invisibility)){hidden=true;break;}
    if(!hidden)continue;
    var ih=inv==null?null:t.health.hediffSet.GetFirstHediffOfDef(inv);if(ih!=null)t.health.RemoveHediff(ih);
    if(!Items.Has(t,"LP_Spotted"))Items.Give(t,"LP_Spotted",2500);Items.Mote(t,"识破",ArcanaEffects.Lake);n++;
   }
   return n;
  }
  public static bool LegendaryResist(Pawn p){
   if(!Live || !LakeArcana.Has(p,"LegendaryResistance"))return false;
   var d=LakeSeals.Data(Adventurer.Get(p));if(d==null)return false;int day=GenDate.DaysPassed;
   if(d.resistDay!=day){d.resistDay=day;d.resistUsed=0;}
   if(d.resistUsed>=LegendResistPerDay)return false;
   d.resistUsed++;Items.Mote(p,"传奇抵抗（"+(LegendResistPerDay-d.resistUsed)+"）",new Color(1f,.85f,.4f));return true;
  }
  // 第二心脏：本应死亡时改为倒地并止血（10 天一次）。补回致命部位、治好一半伤势、失血减半；还是活不了就照常死亡、不用掉次数。
  public static bool SecondHeart(Pawn p){
   if(!Live || p==null || p.Destroyed || !LakeArcana.Has(p,"SecondHeart"))return false;
   var d=LakeSeals.Data(Adventurer.Get(p));if(d==null)return false;int now=Find.TickManager.TicksGame;
   if(d.heartTick>0 && now-d.heartTick<HeartCooldown)return false;
   foreach(var mp in p.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().Where(m=>m.Part!=null && m.Part.def.tags!=null && m.Part.def.tags.Any(tg=>tg.vital)).ToList())p.health.RestorePart(mp.Part);
   ClassMelee.Heal(p,ClassMelee.InjurySum(p)*.5f);
   var bl=p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);if(bl!=null)bl.Severity=Mathf.Min(bl.Severity,.4f);
   Items.Give(p,"LP_StaunchBleeding",2500);
   if(p.health.ShouldBeDead())return false;
   d.heartTick=now;Items.Mote(p,"第二心脏",new Color(1f,.5f,.5f));
   if(p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"的第二心脏跳了起来：本该死去，却只是倒下了。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  // 法术、灵能、魔法伤害（秘法壁垒 -40%、奥术抗性 -15%）。
  public static readonly HashSet<string> MagicDamage=new HashSet<string>{"LP_ArcaneForce","LP_Lightning","LP_Radiant","LP_Force","LP_SilverPsychic","LP_Frost","LP_Necrotic","LP_Shock","LP_Eldritch","LP_DivineSmite","LP_WS_Poison","LP_WS_Psychic","LP_SleepMagic","LP_DragonPoison","Psychic"};
  public static bool Magic(DamageInfo d)=>d.Def!=null && MagicDamage.Contains(d.Def.defName) || d.Weapon!=null && HeroCombat.MagicThing(d.Weapon) || d.Instigator is Pawn a && a.CurJob?.ability!=null;
 }

 // ---------- 时间停止（简化：除施法者外本地图所有人击晕 6 秒、弹丸停在半空；施法者伤害别人提前结束） ----------
 public static class LakeTime {
  public static Pawn caster;public static Map map;public static int until;public static readonly List<Pawn> frozen=new List<Pawn>();
  public static bool Active=>caster!=null;
  public static int Start(Pawn p,int ticks){
   End();if(p?.Map==null)return 0;caster=p;map=p.Map;until=Find.TickManager.TicksGame+ticks;LakeLore.Live=true;
   foreach(var t in map.mapPawns.AllPawnsSpawned.ToList()){if(t==p || t.Dead)continue;int n=ClassMartial2.Boss(t)?ticks/2:ticks;t.stances?.stunner?.StunFor(n,p,false,false);frozen.Add(t);}
   Items.Mote(p,"时间停止",new Color(.8f,.85f,1f));
   if(p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"让时间停了下来（6 秒）。",p,MessageTypeDefOf.NeutralEvent,false);
   return frozen.Count;
  }
  public static void Tick(){if(!Active)return;if(Find.TickManager.TicksGame>=until || caster.Dead || !caster.Spawned || caster.Map!=map)End();}
  public static void End(){
   if(caster==null){frozen.Clear();return;}
   foreach(var t in frozen)if(t!=null && !t.Dead && t.Spawned)t.stances?.stunner?.StopStun();
   frozen.Clear();caster=null;map=null;
  }
  public static bool Frozen(Projectile pr)=>Active && pr!=null && pr.Map==map && pr.Launcher!=caster;
 }
 [HarmonyPatch]
 public static class LakeTimeProjectiles {
  static IEnumerable<System.Reflection.MethodBase> TargetMethods(){
   foreach(var n in new[]{"Tick","TickInterval"}){var m=AccessTools.DeclaredMethod(typeof(Projectile),n);if(m!=null)yield return m;}
  }
  static bool Prefix(Projectile __instance)=>!LakeTime.Frozen(__instance);
 }

 // ---------- 迷宫术：把目标安全地暂存在地图组件里（存档时整个保存），到时间、施法者死亡或地图卸载时放回 ----------
 public class LakeMaze : MapComponent {
  public class Entry : IExposable {
   public Pawn pawn,caster;public IntVec3 cell;public int back;public Lord lord;
   public void ExposeData(){Scribe_Deep.Look(ref pawn,"pawn");Scribe_References.Look(ref caster,"caster");Scribe_Values.Look(ref cell,"cell");Scribe_Values.Look(ref back,"back");Scribe_References.Look(ref lord,"lord");}
  }
  public List<Entry> list=new List<Entry>();int acc;
  public LakeMaze(Map map):base(map){}
  public static LakeMaze Of(Map m)=>m?.GetComponent<LakeMaze>();
  public static bool Banish(Pawn caster,Pawn t,int ticks){
   var map=t?.Map;var comp=Of(map);if(comp==null || t.Dead || !t.Spawned)return false;
   var lord=t.GetLord();lord?.RemovePawn(t);
   t.jobs?.StopAll();t.pather?.StopDead();
   var cell=t.Position;FleckMaker.ThrowDustPuffThick(t.DrawPos,map,2f,new Color(.7f,.6f,1f));Items.Mote(t,"迷宫术",new Color(.7f,.6f,1f));
   if(Find.Selector.IsSelected(t))Find.Selector.Deselect(t);
   t.DeSpawn(DestroyMode.Vanish);
   comp.list.Add(new Entry{pawn=t,caster=caster,cell=cell,back=Find.TickManager.TicksGame+ticks,lord=lord});LakeLore.Live=true;
   return true;
  }
  public bool Holds(Pawn p)=>list.Any(e=>e.pawn==p);
  public override void MapComponentTick(){
   if(list.Count==0 || ++acc<30)return;acc=0;int now=Find.TickManager.TicksGame;
   for(int i=list.Count-1;i>=0;i--){var e=list[i];if(now>=e.back || e.caster==null || e.caster.Dead || e.caster.Destroyed)Return(i);}
  }
  public void ReturnAll(){for(int i=list.Count-1;i>=0;i--)Return(i);}
  public void Return(int i){
   var e=list[i];list.RemoveAt(i);var p=e.pawn;if(p==null || p.Destroyed || p.Spawned)return;
   var cell=e.cell.InBounds(map) && e.cell.Standable(map) && e.cell.GetFirstPawn(map)==null?e.cell:CellFinder.StandableCellNear(e.cell,map,6);
   if(!cell.IsValid)cell=CellFinder.RandomClosewalkCellNear(e.cell,map,8);
   GenSpawn.Spawn(p,cell,map);
   if(e.lord!=null && map.lordManager.lords.Contains(e.lord) && !p.Dead)e.lord.AddPawn(p);
   FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,new Color(.7f,.6f,1f));Items.Mote(p,"从迷宫归来",new Color(.7f,.6f,1f));
  }
  // 地图卸载：地图已经不能放人了，交给世界（不会凭空消失）。
  public override void MapRemoved(){
   foreach(var e in list){var p=e.pawn;if(p==null || p.Destroyed || p.Spawned)continue;if(!Find.WorldPawns.Contains(p))Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.Decide);}
   list.Clear();base.MapRemoved();
  }
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref list,"lpLakeMaze",LookMode.Deep);if(Scribe.mode==LoadSaveMode.PostLoadInit){if(list==null)list=new List<Entry>();list.RemoveAll(e=>e?.pawn==null);if(list.Count>0)LakeLore.Live=true;}}
 }

 // 再生术：倒数 3 天后长回一个失去的肢体或器官。
 public class Hediff_LoreRegen : HediffWithComps {
  public int left=180000;
  public override string LabelInBrackets=>left.ToStringTicksToPeriod();
  public override void TickInterval(int delta){
   base.TickInterval(delta);left-=delta;if(left>0 || pawn==null || pawn.Dead)return;
   var miss=pawn.health.hediffSet.GetMissingPartsCommonAncestors().FirstOrDefault();
   if(miss!=null){pawn.health.RestorePart(miss.Part);Items.Mote(pawn,"长回了"+miss.Part.LabelCap,new Color(.6f,.95f,.6f));}
   pawn.health.RemoveHediff(this);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref left,"lpLeft",180000);}
 }
 // 反转重力：倒数结束落地，受到严重度点钝器伤害。
 public class Hediff_LoreLevitate : HediffWithComps {
  public int left=600;public Pawn caster;
  public override void TickInterval(int delta){
   base.TickInterval(delta);left-=delta;if(left>0 || pawn==null || pawn.Dead)return;
   float dmg=Severity;var p=pawn;var c=caster;pawn.health.RemoveHediff(this);
   if(p.Spawned)FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,1.5f,Color.gray);
   p.TakeDamage(new DamageInfo(DamageDefOf.Blunt,dmg,0f,-1f,c));
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref left,"lpLeft",600);Scribe_References.Look(ref caster,"lpCaster");}
 }

 // ---------- 许愿窗口（不用浮动菜单） ----------
 public class Dialog_LoreWish : Window {
  readonly Pawn pawn;readonly AbilityDef def;bool done;Vector2 scroll;int page;
  public override Vector2 InitialSize=>LPUI.Fit(620f,640f);
  public Dialog_LoreWish(Pawn p,AbilityDef d){pawn=p;def=d;doCloseX=true;doCloseButton=true;absorbInputAroundWindow=true;forcePause=true;}
  List<(string label,string tip,Action act,bool great)> Options(){
   var l=new List<(string,string,Action,bool)>();var map=pawn.Map;
   switch(page){
    case 0:
     l.Add(("施放一个秘传法术","直接施放任意一个 8 环或以下的通用秘传法术（包括没学过的）。",()=>{page=1;scroll=Vector2.zero;},false));
     l.Add(("全体痊愈","本地图所有殖民者伤势全愈、疾病清除（旧伤疤不算）。",()=>{LakeLore.WishHealAll(map);Finish(false);},false));
     l.Add(("变出物品","变出市场价合计 3000 银以内的一种物品。",()=>{page=2;scroll=Vector2.zero;},false));
     l.Add(("大愿望：复活殖民者","复活 1 名死亡不超过 30 天的殖民者（没有尸体也可以）。有 33% 的概率永久失去许愿术并虚弱 10 天。",()=>{page=3;scroll=Vector2.zero;},true));
     l.Add(("大愿望：永久 +1 属性","施法者的一项属性永久 +1（最高 30）。有 33% 的概率永久失去许愿术并虚弱 10 天。",()=>{page=4;scroll=Vector2.zero;},true));
     l.Add(("大愿望：驱散长期事件","结束本地图一个还要持续 1 天以上的事件（毒尘、寒流、火山冬天……）。有 33% 的概率永久失去许愿术并虚弱 10 天。",()=>{page=5;scroll=Vector2.zero;},true));
     break;
    case 1:foreach(var s in LakeLore.WishSpells){var sp=s;l.Add((sp.LabelCap+"（"+LakeArcana.Ext(sp).ring+" 环）",sp.description,()=>Target(sp),false));}break;
    case 2:foreach(var g in LakeLore.WishGoods){var d=DefDatabase<ThingDef>.GetNamedSilentFail(g);if(d==null)continue;int n=Mathf.Max(1,Mathf.FloorToInt(LakeLore.WishItemValue/Mathf.Max(.01f,d.BaseMarketValue)));var gg=g;l.Add((d.LabelCap+" ×"+n,d.description,()=>{LakeLore.WishItems(pawn,gg);Finish(false);},false));}break;
    case 3:foreach(var x in CasterEffects.DeadColonists(pawn).Where(x=>x.Corpse==null || x.Corpse.Age<=30*60000)){var dx=x;l.Add((dx.LabelShortCap,null,()=>{LakeLore.WishRevive(pawn,dx);Finish(true);},true));}break;
    case 4:for(int i=0;i<6;i++){int a=i;var h=Adventurer.Get(pawn);if(h==null)break;l.Add((Adventurer.Names[a]+" "+h.scores[a]+" → "+(h.scores[a]+1),null,()=>{LakeLore.WishScore(pawn,a);Finish(true);},true));}break;
    case 5:foreach(var c in LakeLore.LongConditions(map)){var cc=c;l.Add((cc.LabelCap+(cc.Permanent?"（永久）":"（还剩 "+cc.TicksLeft.ToStringTicksToPeriod()+"）"),cc.def.description,()=>{LakeLore.WishEndCondition(cc);Finish(true);},true));}break;
   }
   return l;
  }
  void Target(AbilityDef sp){
   var tp=sp.verbProperties?.targetParams??new TargetingParameters{canTargetLocations=true,canTargetPawns=true};
   done=true;Close();
   if(sp.targetRequired==false && (sp.verbProperties==null || sp.verbProperties.range<=0f)){LakeLore.WishCast(pawn,sp,pawn);return;}
   Find.Targeter.BeginTargeting(tp,t=>{if(!LakeLore.WishCast(pawn,sp,t))Messages.Message("这个目标不行，愿望落空了。",MessageTypeDefOf.RejectInput,false);},pawn,null,sp.uiIcon);
  }
  void Finish(bool great){done=true;if(great)LakeLore.GreatWishCost(pawn,Rand.Value);Close();}
  public override void DoWindowContents(Rect r){
   try{
    float y=LPUI.Title(r,"许愿术："+pawn.LabelShort+"的愿望");
    string intro=page==0?"选一个愿望。安全愿望没有代价；大愿望有 33% 的概率让施法者永久失去许愿术并虚弱 10 天。没选就关掉，许愿术不进冷却。":"选一项（左下角「返回」回到愿望列表）。";
    y+=LPUI.Para(r.x,y,r.width,intro)+LPUI.Gap;
    var opts=Options();var outer=new Rect(r.x,y,r.width,r.yMax-y-48f-(page>0?LPUI.ButtonH+LPUI.Gap:0f));
    if(opts.Count==0)LPUI.Unavailable(new Rect(outer.x,outer.y,outer.width,LPUI.RowH*2f),"没有可选的项目。");
    else{
     var view=new Rect(0f,0f,outer.width-LPUI.ScrollBarW,opts.Count*(LPUI.ListRowH+14f));
     Widgets.BeginScrollView(outer,ref scroll,view);
     try{float yy=0f;foreach(var o in opts){var row=new Rect(0f,yy,view.width,LPUI.ListRowH+10f);Widgets.DrawLightHighlight(row);
      if(o.great)GUI.color=LPUI.Gold;if(LPUI.Button(row.ContractedBy(3f),o.label,true,o.tip,false)){GUI.color=Color.white;o.act();break;}GUI.color=Color.white;yy+=LPUI.ListRowH+14f;}}
     finally{Widgets.EndScrollView();}
    }
    if(page>0 && LPUI.Button(new Rect(r.x,r.yMax-48f-LPUI.ButtonH-LPUI.Gap,120f,LPUI.ButtonH),"返回"))page=0;
   }finally{LPUI.Reset();}
  }
  public override void PostClose(){base.PostClose();if(!done)pawn.abilities?.GetAbility(def)?.ResetCooldown();}
 }

 // ==================== 挂钩 ====================
 // 时间停止提前结束、断骨击、秘法壁垒 / 奥术抗性减伤、法力导体。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class LoreDamage {
  static bool busy;
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(!LakeLore.Live || busy || dinfo.Def==null || dinfo.Amount<=0f)return;
   var v=__instance;var a=dinfo.Instigator as Pawn;
   if(LakeTime.Active && a!=null && a==LakeTime.caster && v!=a)LakeTime.End();
   if(!dinfo.Def.harmsHealth)return;
   if(LakeLore.Magic(dinfo)){float f=1f;if(LakeArcana.Has(v,"ArcaneBulwark"))f*=.6f;if(LakeSeals.HasPassive(v,"LP_LSP_ArcaneResist"))f*=.85f;if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);}
   if(a==null || a==v)return;
   if(Items.Has(a,"LP_LO_BoneBreak") && HeroCombat.Melee(dinfo) && a.HostileTo(v)){
    var h=a.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_LO_BoneBreak"));float pw=h?.Severity??30f;var att=a;var vic=v;
    Fiends.Defer(()=>{Items.Remove(att,"LP_LO_BoneBreak");busy=true;try{LakeLore.BreakBone(att,vic,pw);}finally{busy=false;}});
   }
   if(LakeSeals.HasPassive(a,"LP_LSP_ManaConduit") && a.HostileTo(v) && Rand.Chance(LakeSeals.ConduitChance)){
    var att=a;var vic=v;Fiends.Defer(()=>{if(vic.Dead)return;busy=true;try{vic.TakeDamage(new DamageInfo(DefCache<DamageDef>.Get("LP_ArcaneForce")??DamageDefOf.Burn,LakeSeals.ConduitDamage,0f,-1f,att));}finally{busy=false;}});
   }
  }
 }
 // 第二心脏。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class LoreSecondHeart {
  [HarmonyPriority(Priority.High)]
  static bool Prefix(Pawn __instance)=>!(LakeLore.Live && LakeLore.SecondHeart(__instance));
 }
 // 秘法壁垒：所有豁免成功率 +15%（+3）。
 [HarmonyPatch(typeof(Legendary),nameof(Legendary.SaveBonus))]
 public static class LoreSaveBonus {static void Postfix(Pawn p,ref int __result){if(LakeLore.Live && LakeArcana.Has(p,"ArcaneBulwark"))__result+=3;}}
 // 传奇抵抗：失败的豁免改成功（每天 3 次）。
 [HarmonyPatch(typeof(Legendary),nameof(Legendary.Save))]
 public static class LoreLegendResist {static void Postfix(Pawn p,ref bool __result){if(!__result && LakeLore.LegendaryResist(p))__result=true;}}
 // 心智抗性判定：秘法壁垒 +15%；心灵空白直接抵抗。
 [HarmonyPatch(typeof(DNDMind),nameof(DNDMind.TryResist))]
 public static class LoreMindResist {
  static void Postfix(Pawn pawn,ref bool __result){
   if(__result || !LakeLore.Live || pawn==null)return;
   if(Items.Has(pawn,"LP_LO_MindBlank") || LakeArcana.Has(pawn,"ArcaneBulwark") && Rand.Chance(.15f))__result=true;
  }
 }
 // 心灵空白：精神状态（崩溃、魅惑、控制）起不来。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class LoreMindBlankState {static bool Prefix(Pawn ___pawn,ref bool __result){if(!LakeLore.Live || !Items.Has(___pawn,"LP_LO_MindBlank"))return true;__result=false;return false;}}
 // 沉默：原版灵能也放不了。
 [HarmonyPatch(typeof(Ability),nameof(Ability.CanCast),MethodType.Getter)]
 public static class LoreSilence {static void Postfix(Ability __instance,ref AcceptanceReport __result){if(!__result.Accepted || !LakeLore.Live || __instance?.pawn==null)return;if(Items.Has(__instance.pawn,"LP_LS_Silenced"))__result="被反制法术沉默了";}}
}
