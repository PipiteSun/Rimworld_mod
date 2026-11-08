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
 // 1.130.0 施法职业法术表的新法术效果（effect 以 CS_ 开头）。大多数法术是数据驱动的通用效果，参数写在 CasterSpellExt：
 // CS_Bolt 单体伤害（附带状态 / 眩晕 / 拉近 / 溅射 / 多目标 / 对某类目标加倍）、CS_Area 区域伤害、CS_Cone 锥形、CS_Heal / CS_HealArea 治疗、
 // CS_Buff 友军增益（可多人）、CS_Hex 控制（成功率，单体或区域）、CS_Zone 法术区域（SpellZone，kind 以 CS 开头）、CS_Aura 跟着施法者的光环、
 // CS_Summon 召唤、CS_Revive 复活、CS_Restore 复原、CS_Spawn 造物、CS_Util 各种工具法术（kind 区分）。
 public static class CasterEffects {
  public static int Live; // Hediff_CS 的数量（伤害钩子快速返回）
  static readonly Color C=new Color(.55f,.8f,1f),Holy=new Color(1f,.92f,.6f),Nature=new Color(.55f,.85f,.45f),Dark=new Color(.6f,.45f,.8f);
  static int Now=>Find.TickManager.TicksGame;
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.HostileTo(a);
  static bool Friendly(Pawn a,Pawn t)=>t!=null && !t.Dead && !t.HostileTo(a);
  static List<Pawn> Near(Map m,IntVec3 c,float r)=>m==null || !c.IsValid?new List<Pawn>():Races124.PawnsNear(m,c,r).ToList();
  public static float? RollOverride;static float Roll=>RollOverride??Rand.Value;
  static CasterSpellExt X(AbilityDef d)=>CasterSpells.CExt(d)??Empty;static readonly CasterSpellExt Empty=new CasterSpellExt();
  public static DamageDef Dmg(string n,DamageDef fallback=null){
   switch(n){case null:case "":return fallback??DamageDefOf.Burn;case "Radiant":return PaladinCore.Radiant;case "Necrotic":return WizardSpells.Necrotic;case "Frost":return WizardSpells.Frost;
    case "Psychic":return WizardSpells2.Psychic;case "Poison":return DefCache<DamageDef>.Get("LP_WS_Poison")??DamageDefOf.Burn;case "Lightning":return DefCache<DamageDef>.Get("LP_Lightning")??DamageDefOf.Burn;
    case "Acid":return DefCache<DamageDef>.Get("LP_AlchemyAcid")??DamageDefOf.Burn;case "Force":return DefCache<DamageDef>.Get("LP_Force")??DamageDefOf.Blunt;case "Thunder":return DamageDefOf.Blunt;}
   return DefCache<DamageDef>.Get(n)??fallback??DamageDefOf.Burn;
  }
  static bool PlantLike(Pawn t)=>t.RaceProps.Insect || t.RaceProps.FleshType!=null && (t.RaceProps.FleshType.defName.Contains("Plant") || t.RaceProps.FleshType.defName.Contains("Tree") || t.RaceProps.FleshType.defName.Contains("Insect"));
  public static bool Evil(Pawn t)=>t!=null && (Items.Undead(t) || WizardSpells.CircleKind(t,0) || WizardSpells.CircleKind(t,1) || t.RaceProps.IsAnomalyEntity);
  static bool MetalArmor(Pawn t)=>t.apparel?.WornApparel?.Any(a=>a.Stuff?.stuffProps?.categories?.Contains(StuffCategoryDefOf.Metallic)==true || a.def.apparel?.bodyPartGroups?.Contains(BodyPartGroupDefOf.Torso)==true && a.def.costList?.Any(c=>c.thingDef==ThingDefOf.Steel || c.thingDef==ThingDefOf.Plasteel)==true)==true;
  public static bool Matches(string vs,Pawn t){
   if(vs.NullOrEmpty() || t==null)return false;
   switch(vs){case "Undead":return Items.Undead(t);case "Evil":return Evil(t);case "Wounded":return t.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury i && !i.IsPermanent());case "Plant":return PlantLike(t);
    case "Metal":return MetalArmor(t);case "Mech":return t.RaceProps.IsMechanoid;case "Humanlike":return t.RaceProps.Humanlike;case "Animal":return t.RaceProps.Animal;}
   return false;
  }
  static float Amount(CasterSpellExt x,Pawn t,float pw)=>Matches(x.vs,t)?pw*x.vsMult:pw;

  // ---------- 选目标检查 ----------
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e?.effect==null || !e.effect.StartsWith("CS_"))return null;var t=target.Thing as Pawn;var def=DefOf(e);var x=X(def);
   switch(e.effect){
    case "CS_Bolt":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
    case "CS_Hex":if(target.Thing==null && e.radius>0f)return null;if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";
     if(x.vs=="Humanlike" && !t.RaceProps.Humanlike)return "只对人形有效。";if(x.vs=="Animal" && !t.RaceProps.Animal)return "只对动物有效。";if(x.mind && t.RaceProps.IsMechanoid)return "对机械体无效。";return null;
    case "CS_Heal":case "CS_Buff":case "CS_Restore":case "CS_Imbue":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(t.HostileTo(caster))return "只能对友军使用。";return null;
    case "CS_Revive":return ReviveCannot(target.Thing as Corpse,x);
    case "CS_Util":return UtilCannot(caster,x.kind,target,e);
   }
   return null;
  }
  static Dictionary<CompProperties_ClassEffect,AbilityDef> byProps;
  static AbilityDef DefOf(CompProperties_ClassEffect e){
   if(byProps==null){byProps=new Dictionary<CompProperties_ClassEffect,AbilityDef>();foreach(var d in DefDatabase<AbilityDef>.AllDefsListForReading){var c=d.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();if(c?.effect!=null && c.effect.StartsWith("CS_"))byProps[c]=d;}}
   return e!=null && byProps.TryGetValue(e,out var def)?def:null;
  }

  // ---------- 施放 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(e?.effect==null || !e.effect.StartsWith("CS_"))return false;
   var x=X(def);var t=target.Thing as Pawn;float s=ClassEffects.Strength(p,e),pw=ClassEffects.Power(p,e);int dur=ClassEffects.Duration(p,e);float rad=ClassEffects.Radius(p,e);
   int up=Spellbook.Upcast(def)!=null?Spellbook.CurUp:0;if(Spellbook.Upcast(def)=="radius")rad+=up;
   var map=p.Map;var cell=target.Cell.IsValid?target.Cell:p.Position;if(!def.targetRequired){cell=p.Position;if(t==null)t=p;}
   switch(e.effect){
    case "CS_Bolt":if(Hostile(p,t))Bolt(p,def,x,t,pw,dur,s,up);return true;
    case "CS_Area":Area(p,def,x,cell,rad,pw,dur);return true;
    case "CS_Cone":Cone(p,def,x,cell,e.range>0?e.range:8f,pw,dur);return true;
    case "CS_Heal":if(Friendly(p,t))HealOne(p,x,t,pw);return true;
    case "CS_HealArea":HealArea(p,x,cell,rad>0?rad:6f,pw,(x.count>0?x.count:6)+(Spellbook.Upcast(def)=="target"?up:0));return true;
    case "CS_Buff":if(Friendly(p,t))Buff(p,def,e,x,t,rad,dur,s,pw,up);return true;
    case "CS_Hex":Hex(p,def,e,x,t,cell,rad,dur,s,up);return true;
    case "CS_Zone":Zone(p,def,e,x,cell,rad,dur,pw);return true;
    case "CS_Aura":Aura(p,def,e,x,rad,dur,pw);return true;
    case "CS_Summon":Summon(p,def,x,cell,(x.count>0?x.count:1)+(Spellbook.Upcast(def)=="target"?up:0),dur);return true;
    case "CS_Revive":Revive(p,x,target.Thing as Corpse);return true;
    case "CS_Restore":if(Friendly(p,t))Restore(p,x.kind,t);return true;
    case "CS_Spawn":SpawnThings(p,x.thing,Mathf.RoundToInt((x.count>0?x.count:1)*(x.kind=="scale"?s:1f)),cell);return true;
    case "CS_Util":Util(p,def,e,x,target,cell,rad,dur,pw,s);return true;
    case "CS_Imbue":if(Friendly(p,t))Imbue(p,def,e,t,dur,pw);return true; // 1.131.0 惩击 / 武器附魔 / 下一击
   }
   return false;
  }

  // ---------- 通用效果 ----------
  public static float Bolt(Pawn p,AbilityDef def,CasterSpellExt x,Pawn t,float pw,int dur,float s,int up=0){
   var d=Dmg(x.damage);float dealt=WizardSpells.Hit(p,t,d,Amount(x,t,pw),x.ap);
   if(!x.damage2.NullOrEmpty() && !t.Dead)dealt+=WizardSpells.Hit(p,t,Dmg(x.damage2),Amount(x,t,pw)*.5f,x.ap);
   if(!t.Dead)Rider(p,x,t,dur,s);
   if(x.pull && !t.Dead && t.Spawned && p.Spawned){var dir=(p.Position-t.Position).ToVector3();int cells=Mathf.Min(3,Mathf.Max(0,Mathf.RoundToInt(dir.magnitude)-1));if(cells>0){var n=dir.normalized;for(int k=cells;k>=1;k--){var c=t.Position+new IntVec3(Mathf.RoundToInt(n.x*k),0,Mathf.RoundToInt(n.z*k));if(c==t.Position || !c.InBounds(t.Map) || !c.Standable(t.Map) || c.GetFirstPawn(t.Map)!=null || !GenSight.LineOfSight(t.Position,c,t.Map))continue;DNDPush.Move(t,c);Items.Mote(t,"拉近",Nature);break;}}}
   if(x.push && !t.Dead && t.Spawned && p.Spawned){var dir=(t.Position-p.Position).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(1f,0f,0f);DNDPush.Move(t,DNDPush.Destination(t.Map,t.Position,dir,3));Items.Mote(t,"推开",C);}
   if(x.splash>0f && t.Map!=null)foreach(var o in Near(t.Map,t.Position,x.splash))if(o!=t && Hostile(p,o))WizardSpells.Hit(p,o,d,Amount(x,o,pw)*.5f,x.ap);
   int extra=x.count>1?x.count-1:0;if(Spellbook.Upcast(def)=="target")extra+=up;
   if(extra>0 && t.Map!=null)foreach(var o in Near(t.Map,t.Position,6f).Where(o=>o!=t && Hostile(p,o)).OrderBy(o=>o.Position.DistanceToSquared(t.Position)).Take(extra).ToList()){WizardSpells.Hit(p,o,d,Amount(x,o,pw),x.ap);if(!o.Dead)Rider(p,x,o,dur,s);}
   Glow(t,d);return dealt;
  }
  // 附带效果：状态（chance > 0 时按成功率）、眩晕、精神状态。
  static bool Rider(Pawn p,CasterSpellExt x,Pawn t,int dur,float s){
   if(x.hediff2.NullOrEmpty() && x.stun<=0 && x.mental.NullOrEmpty())return false;
   if(x.chance>0f && !WizardSpells.TryControl(p,t,x.chance,x.mind))return false;
   if(!x.hediff2.NullOrEmpty()){var h=Items.Give(t,x.hediff2,dur>0?dur:600,1f);if(h is Hediff_CS hc){hc.caster=p;hc.power=s;}}
   if(x.stun>0)t.stances?.stunner?.StunFor(x.stun,p);
   if(!x.mental.NullOrEmpty()){var m=DefDatabase<MentalStateDef>.GetNamedSilentFail(x.mental);if(m!=null)t.mindState?.mentalStateHandler?.TryStartMentalState(m,"法术",true,false,false,null,true);}
   return true;
  }
  public static int Area(Pawn p,AbilityDef def,CasterSpellExt x,IntVec3 c,float r,float pw,int dur){
   var map=p.Map;if(map==null)return 0;int n=0;var d=Dmg(x.damage);
   foreach(var o in Near(map,c,r)){if(!Hostile(p,o))continue;n++;
    if(x.kind=="divineWord")DivineWord(p,o);
    if(pw>0f){WizardSpells.Hit(p,o,d,Amount(x,o,pw),x.ap);if(!x.damage2.NullOrEmpty() && !o.Dead)WizardSpells.Hit(p,o,Dmg(x.damage2),Amount(x,o,pw),x.ap);}
    if(o.Dead)continue;Rider(p,x,o,dur,1f);
    if(x.push && o.Spawned){var dir=(o.Position-c).ToVector3();if(dir.sqrMagnitude<.01f)dir=(o.Position-p.Position).ToVector3();DNDPush.Move(o,DNDPush.Destination(map,o.Position,dir,2));}
   }
   if(x.kind=="quake")Quake(p,map,c,r,pw);
   if(c.InBounds(map)){FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,Mathf.Max(1f,r*2f));Items.Mote(p,def.label,C);}
   return n;
  }
  public static int Cone(Pawn p,AbilityDef def,CasterSpellExt x,IntVec3 toward,float range,float pw,int dur){
   var map=p.Map;if(map==null)return 0;var cells=new HashSet<IntVec3>(ClassCasters.ConeCells(p.Position,toward,range));int n=0;
   var d=x.kind=="breath"?Breath(p):Dmg(x.damage);
   foreach(var o in Near(map,p.Position,range+1f))if(Hostile(p,o) && cells.Contains(o.Position)){WizardSpells.Hit(p,o,d,Amount(x,o,pw),x.ap);if(!o.Dead)Rider(p,x,o,dur,1f);n++;}
   foreach(var c in cells.Take(30))if(c.InBounds(map) && Rand.Chance(.3f))FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(),map,1f,d==DamageDefOf.Flame?new Color(1f,.6f,.3f):C);
   Items.Mote(p,def.label,C);return n;
  }
  // 龙息术 / 血脉龙息：元素按血脉（龙裔、元素裔）——没有血脉就是火焰。
  public static DamageDef Breath(Pawn p){
   var x=p?.genes?.Xenotype?.defName??"";
   if(x.Contains("Water") || x.Contains("Frost") || x.Contains("White"))return WizardSpells.Frost;
   if(x.Contains("Air") || x.Contains("Storm") || x.Contains("Blue") || x.Contains("Behir"))return Dmg("Lightning");
   if(x.Contains("Black") || x.Contains("Ooze"))return Dmg("Acid");if(x.Contains("Green"))return Dmg("Poison");if(x.Contains("Force"))return Dmg("Force");
   return DamageDefOf.Flame;
  }
  static void Glow(Pawn t,DamageDef d){if(t?.Map==null)return;if(d==DamageDefOf.Flame)FleckMaker.ThrowFireGlow(t.DrawPos,t.Map,1f);else FleckMaker.ThrowLightningGlow(t.DrawPos,t.Map,.8f);}
  public static float HealOne(Pawn p,CasterSpellExt x,Pawn t,float pw){
   float h=ClassMelee.Heal(t,pw);
   if(x.kind=="cure" || x.kind=="full" || x.kind=="power")CureSick(t,x.kind!="cure");
   if(x.kind=="power"){h+=ClassMelee.Heal(t,9999f);t.stances?.stunner?.StopStun();if(t.InMentalState && !t.MentalStateDef.IsAggro)t.MentalState.RecoverFromState();foreach(var b in t.health.hediffSet.hediffs.Where(b=>Array.IndexOf(LesserBad,b.def.defName)>=0).ToList())t.health.RemoveHediff(b);}
   Items.Mote(t,"治疗 "+h.ToString("0"),Holy);return h;
  }
  static int CureSick(Pawn t,bool full){
   int n=0;foreach(var h in t.health.hediffSet.hediffs.Where(h=>h.def==HediffDefOf.FoodPoisoning || h.def==HediffDefOf.ToxicBuildup || full && (ClassCasters.IsDisease(h) || h.def.defName=="LP_WS_Blinded" || h.def.defName=="Blindness")).ToList()){t.health.RemoveHediff(h);n++;}
   return n;
  }
  public static int HealArea(Pawn p,CasterSpellExt x,IntVec3 c,float r,float pw,int count){
   int n=0;foreach(var o in Near(p.Map,c,r).Where(o=>Friendly(p,o)).OrderBy(o=>-ClassMelee.InjurySum(o)).Take(count).ToList()){HealOne(p,x,o,pw);n++;}
   return n;
  }
  public static int Buff(Pawn p,AbilityDef def,CompProperties_ClassEffect e,CasterSpellExt x,Pawn t,float r,int dur,float s,float pw,int up){
   var list=new List<Pawn>{t};int count=(x.count>0?x.count:1)+(Spellbook.Upcast(def)=="target"?up:0);
   if(count>1 && t.Map!=null)list.AddRange(Near(t.Map,t.Position,r>0?r:6f).Where(o=>o!=t && Friendly(p,o) && (o.RaceProps.Humanlike || o.Faction==p.Faction)).OrderBy(o=>o.Position.DistanceToSquared(t.Position)).Take(count-1));
   foreach(var o in list){
    if(!e.hediff.NullOrEmpty()){var h=Items.Give(o,e.hediff,dur,e.fixedSeverity?1f:s);if(h is Hediff_CS hc){hc.caster=p;hc.power=e.power>0f?pw:s;}}
    if(x.kind=="tempHp" || x.kind=="heroism")TempHp(o,pw,dur);
    if(x.kind=="heroism"){if(o.InMentalState && ClassSubclass.FearState(o.MentalStateDef))o.MentalState.RecoverFromState();}
    if(x.kind=="wildShape")Items.Give(o,"LP_WildShape",dur,s);
    Items.Mote(o,def.LabelCap,C);
   }
   if(Spellbook.Concentration(def) && !e.hediff.NullOrEmpty())Spellbook.Concentrate(p,def,list,e.hediff);
   return list.Count;
  }
  // 临时生命：先扣临时生命再扣真实伤势（Hediff_CS.pool，伤害钩子里吸收）。不叠加，取较高。
  public static Hediff_CS TempHp(Pawn t,float amount,int ticks){
   var h=Items.Give(t,"LP_CS_TempHP",ticks>0?ticks:2500,1f) as Hediff_CS;if(h!=null)h.pool=Mathf.Max(h.pool,amount);return h;
  }
  public static int Hex(Pawn p,AbilityDef def,CompProperties_ClassEffect e,CasterSpellExt x,Pawn t,IntVec3 c,float r,int dur,float s,int up){
   var map=p.Map;var list=new List<Pawn>();int count=(x.count>0?x.count:1)+(Spellbook.Upcast(def)=="target"?up:0);
   bool Ok(Pawn o)=>Hostile(p,o) && !o.Downed && (x.vs!="Humanlike" || o.RaceProps.Humanlike) && (x.vs!="Animal" || o.RaceProps.Animal) && (!x.mind || !o.RaceProps.IsMechanoid);
   if(t!=null && r<=0f){if(Ok(t))list.Add(t);if(count>1)list.AddRange(Near(map,t.Position,6f).Where(o=>o!=t && Ok(o)).OrderBy(o=>o.Position.DistanceToSquared(t.Position)).Take(count-1));}
   else list.AddRange(Near(map,c,r).Where(Ok).OrderBy(o=>o.Position.DistanceToSquared(c)).Take(x.count>0?count:99));
   var won=new List<Pawn>();
   foreach(var o in list){
    if(x.kind=="command"){if(!WizardSpells.TryControl(p,o,x.chance,true))continue;Command(p,o);won.Add(o);continue;}
    if(x.kind=="dominate"){if(WizardSpells2.Dominate(p,def,o,dur,x.chance))won.Add(o);continue;}
    if(x.kind=="compel"){if(!WizardSpells.TryControl(p,o,x.chance,true))continue;var dir=(o.Position-p.Position).ToVector3();if(dir.sqrMagnitude<.01f)dir=new Vector3(0f,0f,1f);DNDPush.Move(o,DNDPush.Destination(map,o.Position,dir,5));o.stances?.stunner?.StunFor(90,p);Items.Mote(o,"被迫走开",C);won.Add(o);continue;}
    if(x.kind=="stunWord" && o.health.summaryHealth.SummaryHealthPercent>.6f){Items.Mote(o,"伤势不重，不受影响",C);continue;}
    if(Rider(p,x,o,dur,s)){won.Add(o);Items.Mote(o,def.label,C);}
   }
   if(Spellbook.Concentration(def) && won.Count>0 && !x.hediff2.NullOrEmpty())Spellbook.Concentrate(p,def,won,x.hediff2);
   if(r>0f && map!=null && c.InBounds(map))FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,r*2f);
   return won.Count;
  }
  // 命令术：随机一句——“趴下”（倒地 3 秒）、“滚开”（逃跑）、“放下”（丢掉武器）。
  static void Command(Pawn p,Pawn o){
   int k=Rand.Range(0,3);if(k==2 && o.equipment?.Primary==null)k=0;
   if(k==0){o.stances?.stunner?.StunFor(180,p);Items.Mote(o,"命令：趴下",C);}
   else if(k==1){var m=DefDatabase<MentalStateDef>.GetNamedSilentFail("PanicFlee");if(m!=null && o.mindState?.mentalStateHandler?.TryStartMentalState(m,"命令术",true)==true)Items.Mote(o,"命令：滚开",C);else{o.stances?.stunner?.StunFor(180,p);Items.Mote(o,"命令：趴下",C);}}
   else{if(o.equipment.TryDropEquipment(o.equipment.Primary,out _,o.Position))Items.Mote(o,"命令：放下",C);}
  }
  public static SpellZone Zone(Pawn p,AbilityDef def,CompProperties_ClassEffect e,CasterSpellExt x,IntVec3 c,float r,int dur,float pw){
   var z=SpellZone.Make(p,c,"CS_"+(x.zone??"Hurt"),r,dur);if(z==null)return null;
   z.power=pw;z.csDmg=x.damage;z.csHed=x.hediff2;z.csAp=x.ap;z.csLabel=def.label;z.csTicks=x.stun;
   if(x.line)z.line=WizardSpells2.Line(p,c,x.cells>0?x.cells:8);
   Items.Mote(p,def.label,C);
   if(Spellbook.Concentration(def))Spellbook.Concentrate(p,def,null,null,z);
   return z;
  }
  // 区域每 30 刻（SpellZone.Tick 的 default 分支）。
  public static void ZoneTick(SpellZone z){
   var map=z.Map;if(map==null)return;var k=z.kind;var caster=z.caster;
   switch(k){
    case "CS_Hurt":case "CS_Spikes":case "CS_Blades":case "CS_Insects":case "CS_Moon":case "CS_Hunger":case "CS_Fire":
     if(!z.IsHashIntervalTick(60))return;
     foreach(var o in z.Inside){if(!z.Foe(o))continue;if(z.power>0f)WizardSpells.Hit(caster,o,Dmg(z.csDmg),z.power,z.csAp);
      if(k=="CS_Hunger" && !o.Dead)WizardSpells.Hit(caster,o,Dmg("Acid"),z.power*.5f,z.csAp);
      if(!o.Dead && !z.csHed.NullOrEmpty())Items.Give(o,z.csHed,120,1f);}
     if(k=="CS_Fire" || k=="CS_Moon")foreach(var c in z.Cells.Take(6))if(Rand.Chance(.4f))FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),map,.6f);
     return;
    case "CS_Elements": // 元素风暴：每秒随机火焰 / 寒冷 / 闪电
     if(!z.IsHashIntervalTick(60))return;foreach(var o in z.Inside){if(!z.Foe(o))continue;int r=Rand.Range(0,3);WizardSpells.Hit(caster,o,r==0?DamageDefOf.Flame:r==1?WizardSpells.Frost:Dmg("Lightning"),z.power,z.csAp);}
     foreach(var c in z.Cells.Take(8))if(Rand.Chance(.3f))FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),map,.7f);return;
    case "CS_Silence":foreach(var o in z.Inside)if(!o.Dead)Items.Give(o,"LP_CS_Silenced",90,1f);return;
    case "CS_Guardian": // 信仰守卫：靠近的敌人受光耀伤害（每个敌人 5 秒一次），总共打出 4 次后消散
     foreach(var o in z.Inside){if(!z.Foe(o) || o.Downed || !z.Due(o,300))continue;WizardSpells.Hit(caster,o,PaladinCore.Radiant,z.power,.3f);Items.Mote(o,"信仰守卫",Holy);if(++z.csTicks>=4){z.End();return;}}
     return;
    case "CS_Hallow": // 圣居：亡灵、邪魔、异常生物进不来——每秒受光耀伤害并被推出去
     if(!z.IsHashIntervalTick(60))return;foreach(var o in z.Inside){if(!z.Foe(o) || !Evil(o))continue;WizardSpells.Hit(caster,o,PaladinCore.Radiant,z.power,.3f);if(!o.Dead && o.Spawned)z.Expel(o);}
     return;
    case "CS_Heal":if(!z.IsHashIntervalTick(60))return;foreach(var o in z.Inside)if(!o.Dead && !z.Foe(o)){ClassMelee.Heal(o,z.power);if(o.health.hediffSet.hediffs.Any(h=>h is Hediff_Injury))FleckMaker.ThrowMetaIcon(o.Position,map,FleckDefOf.HealingCross);}
     return;
    case "CS_Wind": // 风墙：墙上的敌人被吹开并受钝器伤害；墙边 1 格内的友军挡掉一半远程伤害
     foreach(var o in z.Inside){if(z.Foe(o)){if(z.Due(o,60)){WizardSpells.Hit(caster,o,DamageDefOf.Blunt,z.power,.1f);if(!o.Dead && o.Spawned && caster!=null){var dir=(o.Position-caster.Position).ToVector3();DNDPush.Move(o,DNDPush.Destination(map,o.Position,dir,2));}}}}
     foreach(var c in z.Cells)foreach(var o in Near(map,c,1.5f))if(!z.Foe(o))Items.Give(o,"LP_CS_WindWard",90,1f);
     return;
    case "CS_Storm": // 复仇风暴 / 召雷：每 2 秒对区域里一个随机敌人落一道雷
     if(!z.IsHashIntervalTick(120))return;var foe=z.Inside.Where(o=>z.Foe(o) && !o.Downed).RandomElementWithFallback();if(foe!=null)ClassCasters.CallLightning(caster,foe.Position,z.power);
     return;
   }
  }
  public static Hediff_CS Aura(Pawn p,AbilityDef def,CompProperties_ClassEffect e,CasterSpellExt x,float r,int dur,float pw){
   var h=Items.Give(p,e.hediff,dur,1f) as Hediff_CS;if(h==null)return null;h.caster=p;h.power=pw;h.radius=r>0f?r:3f;
   Items.Mote(p,def.label,C);if(Spellbook.Concentration(def))Spellbook.Concentrate(p,def,new[]{p},e.hediff);return h;
  }
  public static List<Pawn> Summon(Pawn p,AbilityDef def,CasterSpellExt x,IntVec3 c,int count,int ticks){
   var made=new List<Pawn>();var map=p.Map;if(map==null)return made;
   PawnKindDef kind=null;foreach(var n in (x.thing??"").Split(',')){kind=DefDatabase<PawnKindDef>.GetNamedSilentFail(n.Trim());if(kind!=null)break;}
   if(kind==null){Messages.Message("找不到可以召唤的生物。",MessageTypeDefOf.RejectInput,false);return made;}
   for(int i=0;i<count;i++){
    Pawn w;try{w=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:kind.RaceProps.Humanlike));}
    catch(Exception ex){Log.Warning("[DND边缘大陆] 召唤失败："+ex.Message);break;}
    var at=c.IsValid && c.InBounds(map) && c.Standable(map)?c:p.Position;GenSpawn.Spawn(w,CellFinder.RandomClosewalkCellNear(at,map,2),map);
    var h=(Hediff_WS)Items.Give(w,"LP_WS_Summoned",ticks,1f);if(h!=null)h.caster=p;
    if(w.training!=null)foreach(var td in new[]{TrainableDefOf.Tameness,TrainableDefOf.Obedience,TrainableDefOf.Release})if(td!=null && w.training.CanAssignToTrain(td).Accepted)w.training.Train(td,null,true);
    if(w.playerSettings!=null && w.RaceProps.Animal && w.training!=null && w.training.HasLearned(TrainableDefOf.Obedience))w.playerSettings.Master=p;
    if(!x.kind.NullOrEmpty() && x.kind!="scale")w.Name=new NameSingle(x.kind);
    FleckMaker.ThrowDustPuffThick(w.DrawPos,map,2f,C);if(w.RaceProps.Animal)HediffComp_Conjured.Engage(w);made.Add(w);
   }
   if(made.Count>0){Items.Mote(p,def.label,C);if(Spellbook.Concentration(def))Spellbook.Concentrate(p,def,made,"LP_WS_Summoned");}
   return made;
  }
  // ---------- 复活 ----------
  public static string ReviveCannot(Corpse c,CasterSpellExt x){
   var p=c?.InnerPawn;if(p==null || !p.RaceProps.Humanlike)return "需要人形尸体。";
   if(Items.Undead(p) || NecromancyGenes.HasBody(p))return "对巫妖和死灵造物无效。";
   if(x.days>0 && c.Age>x.days*60000)return "死亡超过 "+x.days+" 天，灵魂已经走远。";
   if(x.kind!="full" && x.kind!="reincarnate" && p.health.hediffSet.GetBrain()==null)return "大脑已经不在了。";
   return null;
  }
  public static bool Revive(Pawn caster,CasterSpellExt x,Corpse c){
   if(ReviveCannot(c,x)!=null)return false;var p=c.InnerPawn;
   if(!ResurrectionUtility.TryResurrect(p,new ResurrectionParams{removeDiedThoughts=true,restoreMissingParts=x.kind=="full" || x.kind=="reincarnate"}))return false;
   if(x.kind=="full" || x.kind=="reincarnate")Healed(p);else Items.Give(p,"LP_RevivifyWeakness");
   if(x.kind=="reincarnate")Reincarnate(p);
   if(!Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"在"+(caster?.LabelShort??"施法者")+"的法术中重新睁开了眼睛。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  static void Healed(Pawn p){foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h.def.isBad && h.def.makesSickThought).ToList())p.health.RemoveHediff(h);}
  public static readonly string[] ReincarnateXeno={"LP_HighElf","LP_WoodElf","LP_MountainDwarf","LP_HillDwarf","LP_HalfOrc","LP_LightfootHalfling","LP_RockGnome","LP_HalfElf","LP_Tiefling","LP_Aasimar","LP_GenasiFire","LP_GenasiWater","LP_GenasiEarth","LP_GenasiAir","LP_Minotaur","Baseliner"};
  public static XenotypeDef Reincarnate(Pawn p){
   if(p.genes==null || !ModsConfig.BiotechActive)return null;var opts=ReincarnateXeno.Select(n=>DefDatabase<XenotypeDef>.GetNamedSilentFail(n)).Where(d=>d!=null && d!=p.genes.Xenotype).ToList();if(opts.Count==0)return null;
   var x=opts.RandomElement();try{p.genes.SetXenotype(x);}catch(Exception e){Log.Warning("[DND边缘大陆] 转生术换种族失败："+e.Message);return null;}
   Items.Mote(p,"转生："+x.label,Nature);return x;
  }
  // ---------- 复原 ----------
  static readonly string[] LesserBad={"LP_WS_Blinded","LP_MagicSleep","LP_WS_Slowed","LP_WS_Chilled","LP_WL_Hexed","LP_WS_Feebleminded","LP_Shaken","LP_CS_Weakened","LP_CS_ChillTouched","LP_CS_Slivered","LP_CS_Distracted","LP_WL_Darkened","LP_Petrifying","LP_NabassuPetrify"};
  public static string Restore(Pawn p,string kind,Pawn t){
   Hediff pick=null;var hs=t.health.hediffSet.hediffs;
   if(kind=="greater"){
    pick=hs.FirstOrDefault(h=>h.def.defName=="LP_WS_Feebleminded" || h.def.defName=="LP_WL_Hexed" || h.def.defName.Contains("Curse"))
     ??hs.FirstOrDefault(h=>h is Hediff_Injury i && i.IsPermanent() && i.Part?.def?.defName=="Brain")
     ??hs.FirstOrDefault(h=>h.def==HediffDefOf.Dementia || h.def.defName=="Alzheimers" || h.def.defName=="Cataract" || h.def.defName=="HearingLoss" || h.def.defName=="Frail" || h.def.defName=="BadBack" || h.def.defName=="Asthma")
     ??hs.Where(h=>h is Hediff_Injury i && i.IsPermanent()).OrderByDescending(h=>h.Severity).FirstOrDefault();
   }else if(kind=="curse"){
    var cursed=hs.Where(h=>h.def.defName.Contains("Curse") || h.def.defName=="LP_WL_Hexed" || h.def.defName=="LP_DivineDominated").ToList();
    foreach(var h in cursed)t.health.RemoveHediff(h);bool item=CursedItems.RemoveCurse(new LocalTargetInfo(t));
    string r=cursed.Count>0 || item?"解除了 "+(cursed.Count+(item?1:0))+" 个诅咒":"身上没有诅咒";Items.Mote(t,r,Holy);return r;
   }else{
    pick=hs.FirstOrDefault(h=>Array.IndexOf(LesserBad,h.def.defName)>=0)??hs.FirstOrDefault(h=>h.def==HediffDefOf.FoodPoisoning || h.def==HediffDefOf.ToxicBuildup || h.def.defName=="Blindness")??hs.FirstOrDefault(ClassCasters.IsDisease);
   }
   if(pick==null){Items.Mote(t,"没有可以复原的",Holy);return null;}
   string label=pick.LabelCap;t.health.RemoveHediff(pick);Items.Mote(t,"复原："+label,Holy);return label;
  }
  public static List<Thing> SpawnThings(Pawn p,string defName,int count,IntVec3 c){
   var made=new List<Thing>();var def=DefCache<ThingDef>.Get(defName);var map=p.Map;if(def==null || map==null || count<=0)return made;
   var at=c.IsValid && c.InBounds(map)?c:p.Position;
   while(count>0){var t=ThingMaker.MakeThing(def);int n=Mathf.Min(count,def.stackLimit);t.stackCount=n;count-=n;if(GenPlace.TryPlaceThing(t,at,map,ThingPlaceMode.Near,out var placed))made.Add(placed);}
   Items.Mote(p,def.label+" ×"+made.Sum(t=>t.stackCount),Nature);return made;
  }
  static void DivineWord(Pawn p,Pawn o){
   if(WizardSpells.CircleKind(o,0) || WizardSpells.CircleKind(o,2) || WizardSpells.CircleKind(o,3)){if(!ClassSubclass.Bossy(o) && WizardSpells.TryControl(p,o,.6f,false)){Banishment.Remove(o,"被神圣之言放逐回原来的位面");return;}}
   float hp=o.health.summaryHealth.SummaryHealthPercent;o.stances?.stunner?.StunFor(hp<.5f?600:240,p);Items.Give(o,"LP_WS_Blinded",hp<.5f?1800:600,1f);
  }
  static void Quake(Pawn p,Map map,IntVec3 c,float r,float pw){
   foreach(var o in Near(map,c,r))if(Hostile(p,o) && !o.Downed)o.stances?.stunner?.StunFor(120,p);
   foreach(var b in GenRadial.RadialDistinctThingsAround(c,map,r,true).OfType<Building>().Where(b=>b.Faction!=p.Faction && b.def.useHitPoints).ToList())b.TakeDamage(new DamageInfo(DamageDefOf.Blunt,pw*2f,0f,-1f,p));
   Find.CameraDriver?.shaker?.DoShake(2f);
  }

  // ---------- 工具法术 ----------
  static string UtilCannot(Pawn caster,string kind,LocalTargetInfo target,CompProperties_ClassEffect e){
   var map=caster.Map;var c=target.Cell;var t=target.Thing as Pawn;
   switch(kind){
    case "moldEarth":case "stoneShape":if(map==null || !c.IsValid || !c.InBounds(map))return "需要选择一个格子。";if(c.GetFirstPawn(map)!=null)return "格子上有人。";if(kind=="stoneShape" && !(c.GetEdifice(map)?.def.mineable??false) && !c.Standable(map))return "这里放不下石墙。";return null;
    case "treeStride":if(TreeNear(caster.Map,caster.Position,2)==null)return "要站在一棵树旁边（2 格内）。";if(map==null || !c.IsValid || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map))return "需要一个已探索、能站立的格子。";if(TreeNear(map,c,1)==null)return "目的地 1 格内要有一棵树。";return null;
    case "awaken":if(t==null || t.Dead || !t.RaceProps.Animal)return "需要选择一只动物。";if(t.Faction!=caster.Faction)return "只能对已经驯服的动物使用。";if(Items.Has(t,"LP_CS_Awakened"))return "它已经被唤醒了。";return null;
    case "dream":if(t==null || t.Dead || !t.RaceProps.Humanlike)return "需要选择一个人。";if(t.HostileTo(caster))return "只能给友方的人托梦。";return null;
    case "messenger":if(!WizardSpells.Factions(caster).Any())return "没有可以联络的派系。";return null;
    case "trueRes":if(DeadColonists(caster).Count==0)return "没有可以复活的死去的殖民者。";return null;
    case "identify":if(target.Thing==null || target.Thing.def.category!=ThingCategory.Item)return "需要选择一件物品。";return null;
    case "steed":{if(map==null)return "要在地图上施放。";var bk=CasterSpells.Book(caster,CasterSpells.ListNamed("LP_CSL_Paladin"))??CasterSpells.FirstBook(caster);if(bk?.familiar!=null && !bk.familiar.Dead && !bk.familiar.Destroyed)return "已经有一匹天界战马了（"+bk.familiar.LabelShort+"）。";return null;}
    case "suggest":case "dance":if(t==null || t.Dead || !t.HostileTo(caster))return "需要选择一个敌人。";if(t.RaceProps.IsMechanoid)return "对机械体无效。";if(x0(e)=="suggest")return Bard.TruceCannot(caster,t);return null;
   }
   return null;
  }
  static Thing TreeNear(Map map,IntVec3 c,int r){if(map==null || !c.IsValid)return null;foreach(var x in GenRadial.RadialCellsAround(c,r,true)){if(!x.InBounds(map))continue;var pl=x.GetPlant(map);if(pl!=null && pl.def.plant!=null && pl.def.plant.IsTree)return pl;}return null;}
  public static List<Pawn> DeadColonists(Pawn p)=>Find.WorldPawns.AllPawnsDead.Concat(p.Map?.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Select(c=>c.InnerPawn)??Enumerable.Empty<Pawn>())
   .Where(x=>x!=null && x.Dead && x.RaceProps.Humanlike && x.Faction==Faction.OfPlayer && !Items.Undead(x)).Distinct().Take(20).ToList();
  static void Util(Pawn p,AbilityDef def,CompProperties_ClassEffect e,CasterSpellExt x,LocalTargetInfo target,IntVec3 cell,float rad,int dur,float pw,float s){
   var map=p.Map;var t=target.Thing as Pawn;
   switch(x.kind){
    case "thaumaturgy":{int n=WizardSpells.Prestidigitation(p,new LocalTargetInfo(cell),rad>0?rad:2f);var orb=Subclass3.LightOrb(p,cell);var life=orb?.TryGetComp<CompLifespan>();if(life!=null)life.age=Mathf.Max(0,life.Props.lifespanTicks-2500);Items.Mote(p,"奇术",Holy);return;}
    case "createWater":CreateWater(p,cell,rad>0?rad:3f);return;
    case "purify":Purify(p,cell,rad>0?rad:3f);return;
    case "daylight":for(int i=0;i<3;i++){var c=i==0?cell:CellFinder.RandomClosewalkCellNear(cell,map,3);var orb=Subclass3.LightOrb(p,c);var life=orb?.TryGetComp<CompLifespan>();if(life!=null)life.age=Mathf.Max(0,life.Props.lifespanTicks-dur);}Items.Mote(p,"昼明术",Holy);return;
    case "divination":WizardSpells2.Scry(p,dur>0?dur:180000);return;
    case "druidcraft":Druidcraft(p);return;
    case "moldEarth":MoldEarth(p,cell);return;
    case "stoneShape":StoneShape(p,cell);return;
    case "findTraps":FindTraps(p,rad>0?rad:40f);return;
    case "messenger":Messenger(p);return;
    case "treeStride":if(map!=null){FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,Nature);Races124.Blink(p,cell);FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,Nature);Items.Mote(p,"树跃",Nature);}return;
    case "awaken":if(t!=null)Awaken(p,t);return;
    case "controlEnv":ControlEnv(p);return;
    case "tornado":Tornado(p,cell,dur);return;
    case "contact":ContactPlane(p,s);return;
    case "dream":if(t!=null)Dream(p,t);return;
    case "feast":Feast(p,rad>0?rad:8f,pw,dur);return;
    case "trueRes":TrueRes(p);return;
    case "rebuke":{var h=Items.Give(p,"LP_CS_Rebuke",dur>0?dur:2500,1f) as Hediff_CS;if(h!=null){h.caster=p;h.power=pw;}Items.Mote(p,"地狱斥责",new Color(1f,.5f,.3f));return;}
    case "illusion":foreach(var o in Near(map,cell,rad>0?rad:4f))if(Hostile(p,o))Items.Give(o,"LP_CS_Distracted",dur>0?dur:600,1f);if(map!=null)FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,3f);Items.Mote(p,"次级幻象",C);return;
    case "identify":Identify(p,target.Thing);return;
    case "steed":Steed(p,def);return;
    case "detectEvil":DetectEvil(p,rad>0?rad:30f);return;
    case "animate":{var map2=p.Map;if(map2==null)return;var at=cell.IsValid && cell.InBounds(map2) && cell.Standable(map2)?cell:p.Position;try{var g=Constructs.Create("StoneGolem",p,CellFinder.RandomClosewalkCellNear(at,map2,2),map2);if(g!=null){g.Name=new NameSingle("活化石像");Items.Give(g,"LP_WS_Animated",dur>0?dur:3600,1f);if(Spellbook.Concentration(def))Spellbook.Concentrate(p,def,new[]{g},"LP_WS_Animated");Items.Mote(g,"活化物件",C);}}catch(Exception ex){Log.Warning("[DND边缘大陆] 活化物件失败："+ex.Message);}return;}
    case "creation":Creation(p,def);return;
    case "suggest":if(t!=null && Bard.Truce(p,t,s))Items.Mote(t,"群体暗示：退兵",C);return;
    case "dance":if(t!=null)Bard.Dance(p,t,s,dur);return;
    case "project":WizardSpells2.ArcaneEye(p,cell,12f);{var h=Items.Give(p,"LP_CS_Projected",dur,1f) as Hediff_CS;if(h!=null)h.caster=p;}Items.Mote(p,"投射幻象",C);return;
   }
  }
  public static int CreateWater(Pawn p,IntVec3 c,float r){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var f in GenRadial.RadialDistinctThingsAround(c,map,r,true).OfType<Fire>().ToList()){f.Destroy();n++;}
   foreach(var x in Near(map,c,r))if(x.IsBurning()){var fire=x.GetAttachment(ThingDefOf.Fire);fire?.Destroy();n++;}
   foreach(var pl in GenRadial.RadialDistinctThingsAround(c,map,r,true).OfType<Plant>().ToList())if(pl.Growth<1f && pl.def.plant!=null && pl.def.plant.Sowable)pl.Growth=Mathf.Min(1f,pl.Growth+.05f);
   for(int i=0;i<6;i++)FleckMaker.ThrowDustPuffThick(GenRadial.RadialCellsAround(c,r,true).RandomElement().ToVector3Shifted(),map,1f,new Color(.5f,.7f,1f));
   Items.Mote(p,"造水术：扑灭 "+n+" 处火",new Color(.5f,.7f,1f));return n;
  }
  public static int Purify(Pawn p,IntVec3 c,float r){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var th in GenRadial.RadialDistinctThingsAround(c,map,r,true).ToList()){
    var rot=th.TryGetComp<CompRottable>();if(rot!=null && rot.RotProgress>0f){rot.RotProgress=0f;n++;}
    var ing=th.TryGetComp<CompIngredients>();if(th.def.IsIngestible && th.def.ingestible?.tasteThought==ThoughtDefOf.AteRawFood){}
   }
   foreach(var x in Near(map,c,r))if(!x.HostileTo(p)){var fp=x.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.FoodPoisoning);if(fp!=null){x.health.RemoveHediff(fp);n++;}}
   Items.Mote(p,"净化饮食 ×"+n,Holy);return n;
  }
  static void Druidcraft(Pawn p){
   var map=p.Map;if(map==null)return;var w=map.weatherManager.curWeather;int left=0;try{left=Traverse.Create(map.weatherDecider).Field("curWeatherDuration").GetValue<int>()-Traverse.Create(map.weatherDecider).Field("curWeatherAge").GetValue<int>();}catch{}
   float avg=GenTemperature.GetTemperatureFromSeasonAtTile(Find.TickManager.TicksAbs,map.Tile);float lo=avg-7f,hi=avg+7f;
   string text="现在是「"+w.label+"」"+(left>0?"，大约还会持续 "+(left/2500f).ToString("0")+" 小时":"")+"。接下来一天室外气温约 "+lo.ToStringTemperature("F0")+" ～ "+hi.ToStringTemperature("F0")+"。"
    +(map.gameConditionManager.ActiveConditions.Any()?"\n正在持续："+string.Join("、",map.gameConditionManager.ActiveConditions.Select(g=>g.LabelCap))+"。":"");
   if(!Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"的德鲁伊伎俩："+text,p,MessageTypeDefOf.NeutralEvent,false);
   foreach(var pl in GenRadial.RadialDistinctThingsAround(p.Position,map,2f,true).OfType<Plant>().ToList())if(pl.Growth<1f)pl.Growth=Mathf.Min(1f,pl.Growth+.02f);
   Items.Mote(p,"德鲁伊伎俩",Nature);
  }
  public static string MoldEarth(Pawn p,IntVec3 c){
   var map=p.Map;if(map==null)return null;var ed=c.GetEdifice(map);
   if(ed!=null && ed.def.mineable){ed.Destroy(DestroyMode.KillFinalize);Items.Mote(p,"塑土术：挖开",Nature);return "dig";}
   var terr=c.GetTerrain(map);if(terr!=null && (terr.IsWater || terr.defName.Contains("Mud") || terr.defName.Contains("Marsh")) && !terr.defName.Contains("Deep")){map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil);Items.Mote(p,"塑土术：填平",Nature);return "fill";}
   FilthMaker.RemoveAllFilth(c,map);Items.Mote(p,"塑土术：翻整",Nature);return "smooth";
  }
  public static Thing StoneShape(Pawn p,IntVec3 c){
   var map=p.Map;if(map==null)return null;var ed=c.GetEdifice(map);
   if(ed!=null && ed.def.mineable){var chunk=ed.def.building?.mineableThing;ed.Destroy(DestroyMode.KillFinalize);Items.Mote(p,"塑石术：开凿",Nature);return null;}
   var stuff=DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(d=>d.IsStuff && d.stuffProps.categories!=null && d.stuffProps.categories.Contains(StuffCategoryDefOf.Stony) && d.defName.StartsWith("Blocks"))??ThingDefOf.BlocksGranite;
   var wall=ThingMaker.MakeThing(ThingDefOf.Wall,stuff);wall.SetFaction(p.Faction);var placed=GenSpawn.Spawn(wall,c,map,WipeMode.Vanish);Items.Mote(p,"塑石术：石墙",Nature);return placed;
  }
  public static int FindTraps(Pawn p,float r){
   var map=p.Map;if(map==null)return 0;var traps=map.listerThings.AllThings.Where(t=>t.Spawned && t.def.building!=null && (t.def.building.isTrap || t is Building_Trap) && (t.Faction==null || t.Faction.HostileTo(p.Faction))).ToList();
   int hid=Hediff_WS.Reveal(p,r);
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("寻找陷阱",traps.Count==0 && hid==0?"附近没有发现陷阱或潜伏的敌人。":"发现了 "+traps.Count+" 处陷阱"+(hid>0?"，并揭开了 "+hid+" 个隐形的敌人":"")+"。",LetterDefOf.NeutralEvent,new LookTargets(traps.Take(20)));
   Items.Mote(p,"寻找陷阱",Nature);return traps.Count+hid;
  }
  static void Messenger(Pawn p){
   var fs=WizardSpells.Factions(p).ToList();if(fs.Count==0)return;
   void Go(Faction f){if(!Adventurer.TestSuppressed)Find.WindowStack.Add(new Dialog_Negotiation(p,f,FactionDialogMaker.FactionDialogFor(p,f),true));}
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){Go(fs[0]);return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("动物信使","让一只小动物带口信给一个派系的首领（打开和它的通话）。",fs.Select(f=>(f.Name+"（"+f.def.label+"）",(string)null,(Action)(()=>Go(f)))).ToList(),()=>{}));
  }
  public static bool Awaken(Pawn p,Pawn t){
   if(t.training!=null)foreach(var td in DefDatabase<TrainableDef>.AllDefsListForReading)if(t.training.CanAssignToTrain(td).Accepted){t.training.SetWantedRecursive(td,true);for(int i=0;i<10 && !t.training.HasLearned(td);i++)t.training.Train(td,p,true);}
   Items.Give(t,"LP_CS_Awakened");Items.Mote(t,"唤醒：获得智识",Nature);return true;
  }
  static void ControlEnv(Pawn p){
   var map=p.Map;if(map==null)return;string ended=ClassCasters.EndBadCondition(map);var clear=DefCache<WeatherDef>.Get("Clear");if(clear!=null)ClassCasters.SetWeather(map,clear);
   Items.Mote(p,"控制环境",Nature);if(!Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"平息了天气"+(ended!=null?"，并结束了"+ended:"")+"。",p,MessageTypeDefOf.PositiveEvent,false);
  }
  public static Thing Tornado(Pawn p,IntVec3 c,int ticks){
   var map=p.Map;var def=DefCache<ThingDef>.Get("Tornado");if(map==null || def==null || !c.InBounds(map))return null;
   var t=(Thing)GenSpawn.Spawn(def,c,map);var life=t.TryGetComp<CompLifespan>();if(life!=null && ticks>0)life.age=Mathf.Max(0,life.Props.lifespanTicks-ticks);
   Items.Mote(p,"召唤龙卷风",C);return t;
  }
  public static bool ContactPlane(Pawn p,float s){
   float chance=Mathf.Clamp(.55f+.05f*Adventurer.Mod(Adventurer.Score(p,Adventurer.Int))+(s-1f)*.2f,.2f,.95f);
   if(Roll>=chance){var m=DefDatabase<MentalStateDef>.GetNamedSilentFail("Wander_Psychotic");if(m!=null)p.mindState?.mentalStateHandler?.TryStartMentalState(m,"接触异界",true);Items.Mote(p,"异界的知识冲垮了心智",Dark);return false;}
   WizardSpells2.Scry(p,180000);float done=WizardSpells.Research(p,500f*s);Items.Mote(p,"接触异界：研究 +"+done.ToString("0"),Dark);return true;
  }
  static void Dream(Pawn p,Pawn t){var th=DefCache<ThoughtDef>.Get("LP_CS_SweetDream");if(th!=null)t.needs?.mood?.thoughts?.memories?.TryGainMemory(th);Items.Mote(t,"美梦",Dark);}
  public static int Feast(Pawn p,float r,float pw,int ticks){
   int n=0;var th=DefCache<ThoughtDef>.Get("LP_CS_FeastThought");
   foreach(var o in Near(p.Map,p.Position,r).Where(o=>Friendly(p,o) && o.RaceProps.Humanlike).Take(12).ToList()){
    if(o.needs?.food!=null)o.needs.food.CurLevelPercentage=1f;Items.Give(o,"LP_CS_HeroesFeast",ticks,1f);TempHp(o,pw,ticks);
    foreach(var h in o.health.hediffSet.hediffs.Where(h=>h.def==HediffDefOf.FoodPoisoning || h.def==HediffDefOf.ToxicBuildup).ToList())o.health.RemoveHediff(h);
    if(th!=null)o.needs?.mood?.thoughts?.memories?.TryGainMemory(th);n++;
   }
   Items.Mote(p,"英雄宴 ×"+n,Holy);return n;
  }
  static void TrueRes(Pawn p){
   var list=DeadColonists(p);if(list.Count==0)return;
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){Resurrect(p,list[0]);return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("真复活","选一个死去的殖民者（没有尸体也可以）。",list.Select(x=>(x.LabelShortCap,(string)null,(Action)(()=>Resurrect(p,x)))).ToList(),()=>{}));
  }
  public static bool Resurrect(Pawn caster,Pawn x){
   if(x==null || !x.Dead)return false;bool had=x.Corpse!=null;
   if(!ResurrectionUtility.TryResurrect(x,new ResurrectionParams{removeDiedThoughts=true,restoreMissingParts=true}))return false;
   Healed(x);var map=caster.Map;
   if(map!=null && (!x.Spawned || x.Map!=map)){if(x.Spawned)x.DeSpawn();if(Find.WorldPawns.Contains(x))Find.WorldPawns.RemovePawn(x);GenSpawn.Spawn(x,CellFinder.RandomClosewalkCellNear(caster.Position,map,3),map);}
   if(x.Faction!=Faction.OfPlayer)x.SetFaction(Faction.OfPlayer);
   if(!Adventurer.TestSuppressed)Messages.Message(x.LabelShort+(had?"":"（连遗体都不在了）")+"被真复活术带回了人间。",x,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  static void Identify(Pawn p,Thing t){
   var lines=new List<string>{t.LabelCap};var q=t.TryGetComp<CompQuality>();if(q!=null)lines.Add("品质："+q.Quality.GetLabel());
   var en=GearEnhance.Level(t);if(en>0)lines.Add("附魔强化 +"+en);if(t.TryGetComp<CompCursed>() is CompCursed cc && !cc.lifted)lines.Add("带着诅咒！");
   if(WizardSpells.Magic(t))lines.Add("有魔法灵光");lines.Add("价值 "+t.MarketValue.ToStringMoney());
   if(!Adventurer.TestSuppressed)Messages.Message(string.Join("，",lines)+"。",t,MessageTypeDefOf.NeutralEvent,false);Items.Mote(p,"识别术",C);
  }
  static string x0(CompProperties_ClassEffect e)=>X(DefOf(e)).kind;
  // 寻获坐骑：召来一匹持续存在的天界战马（原版马，殖民地的、认主、羁绊；死了要 1 天才能重召，用能力冷却实现）。
  public static Pawn Steed(Pawn p,AbilityDef def){
   var map=p.Map;if(map==null)return null;var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Horse")??DefDatabase<PawnKindDef>.GetNamedSilentFail("Muffalo");if(kind==null)return null;
   var horse=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false));
   horse.Name=new NameSingle("天界战马");GenSpawn.Spawn(horse,CellFinder.RandomClosewalkCellNear(p.Position,map,2),map);
   if(horse.training!=null)for(int pass=0;pass<2;pass++)foreach(var td in new[]{TrainableDefOf.Tameness,TrainableDefOf.Obedience}.Concat(DefDatabase<TrainableDef>.AllDefsListForReading))if(td!=null && !horse.training.HasLearned(td) && horse.training.CanAssignToTrain(td).Accepted)horse.training.Train(td,p,true);
   if(horse.playerSettings!=null && horse.training!=null && horse.training.HasLearned(TrainableDefOf.Obedience))horse.playerSettings.Master=p;try{p.relations?.AddDirectRelation(PawnRelationDefOf.Bond,horse);}catch{}
   var h=Items.Give(horse,"LP_CS_Steed") as Hediff_CS;if(h!=null)h.caster=p;
   var bk=CasterSpells.BookFor(p,def)??CasterSpells.FirstBook(p);if(bk!=null)bk.familiar=horse;
   FleckMaker.ThrowLightningGlow(horse.DrawPos,map,2f);Items.Mote(horse,"天界战马",Holy);return horse;
  }
  // 侦测邪恶：列出地图上的亡灵、邪魔、异常生物，揭开 30 格内隐形的敌人。
  public static int DetectEvil(Pawn p,float r){
   var map=p.Map;if(map==null)return 0;var evil=map.mapPawns.AllPawnsSpawned.Where(x=>!x.Dead && x!=p && Evil(x)).ToList();int hid=Hediff_WS.Reveal(p,r);
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("侦测邪恶",evil.Count==0?"地图上感觉不到亡灵、邪魔或异常生物。":"感知到 "+evil.Count+" 个亡灵、邪魔或异常生物："+string.Join("、",evil.Take(12).Select(x=>x.LabelShort))+(hid>0?"；揭开了 "+hid+" 个隐形的敌人":"")+"。",LetterDefOf.NeutralEvent,new LookTargets(evil.Take(20)));
   Items.Mote(p,"侦测邪恶",Holy);return evil.Count;
  }
  // 造物术：仪式，造出 器械师等级 × 10 份一种普通材料。
  static readonly string[] CreationMats={"Steel","WoodLog","Cloth","BlocksGranite","ComponentIndustrial"};
  public static int CreationAmount(Pawn p,string mat){var l=CasterSpells.CastList(p,DefCache<AbilityDef>.Get("LP_CS_Creation"))??CasterSpells.ListNamed("LP_CSL_Artificer");int lv=Mathf.Max(1,CasterSpells.Level(p,l));return mat=="ComponentIndustrial"?Mathf.Max(1,lv/2):lv*10;}
  static void Creation(Pawn p,AbilityDef def){
   var opts=CreationMats.Select(n=>DefCache<ThingDef>.Get(n)).Where(d=>d!=null).ToList();if(opts.Count==0)return;
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){SpawnThings(p,opts[0].defName,CreationAmount(p,opts[0].defName),p.Position);return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("造物术","选一种材料（器械师等级 × 10 份；零部件 等级/2 个）。",opts.Select(d=>(d.LabelCap+" ×"+CreationAmount(p,d.defName),(string)null,(Action)(()=>SpawnThings(p,d.defName,CreationAmount(p,d.defName),p.Position)))).ToList()));
  }
  // 惩击 / 武器附魔 / 下一击：给目标（多半是自己）一个带强度的状态，命中时在 CasterOnHit 里结算。
  public static Hediff_CS Imbue(Pawn p,AbilityDef def,CompProperties_ClassEffect e,Pawn t,int dur,float pw){
   var h=Items.Give(t,e.hediff,dur>0?dur:600,1f) as Hediff_CS;if(h==null)return null;h.caster=p;h.power=pw;
   Items.Mote(t,def.LabelCap,Holy);if(Spellbook.Concentration(def))Spellbook.Concentrate(p,def,new[]{t},e.hediff);return h;
  }
  // 命中附加：伤害类型、是否用掉、远程也算、附带效果。
  public static readonly Dictionary<string,(string dmg,bool consume,bool ranged,bool meleeOk,string rider)> OnHit=new Dictionary<string,(string,bool,bool,bool,string)>{
   {"LP_CS_SmiteBlaze",("Flame",true,false,true,"ignite")},{"LP_CS_SmiteThunder",("Thunder",true,false,true,"push")},{"LP_CS_SmiteSear",("Flame",true,false,true,"sear")},
   {"LP_CS_SmiteBrand",("Radiant",true,true,true,"brand")},{"LP_CS_SmiteBlind",("Radiant",true,false,true,"blind")},{"LP_CS_SmiteStagger",("Psychic",true,false,true,"stagger")},
   {"LP_CS_SmiteBanish",("Force",true,false,true,"banish")},{"LP_CS_MagicWeapon",("Force",false,true,true,null)},{"LP_CS_ElementalWeapon",("Flame",false,true,true,null)},
   {"LP_CS_CrusaderBuff",("Radiant",false,false,true,null)},{"LP_CS_AbsorbCharge",("Flame",true,false,true,null)},{"LP_CS_Ensnaring",("Cut",true,true,true,"ensnare")},
   {"LP_CS_LightningArrow",("Lightning",true,true,false,"splash")}};
  [ThreadStatic] static bool inHit;public static bool InHit=>inHit;
  public static int OnHitFire(Pawn a,Pawn v,bool melee){
   if(inHit || a?.health==null || v==null || v.Dead)return 0;int n=0;
   foreach(var h in a.health.hediffSet.hediffs.OfType<Hediff_CS>().ToList()){
    if(!OnHit.TryGetValue(h.def.defName,out var o))continue;if(melee && !o.meleeOk || !melee && !o.ranged)continue;
    float amt=h.power*(o.dmg=="Radiant" && Evil(v)?2f:1f);inHit=true;
    try{
     WizardSpells.Hit(a,v,Dmg(o.dmg),amt,.2f);n++;
     if(!v.Dead)switch(o.rider){
      case "ignite":v.TryAttachFire(.4f,a);break;
      case "push":if(v.Spawned && a.Spawned){var dir=(v.Position-a.Position).ToVector3();DNDPush.Move(v,DNDPush.Destination(v.Map,v.Position,dir,2));v.stances?.stunner?.StunFor(60,a);}break;
      case "sear":{var s2=Items.Give(v,"LP_CS_Seared",600,1f) as Hediff_CS;if(s2!=null){s2.caster=a;s2.power=amt*.2f;}break;}
      case "brand":Items.Give(v,"LP_CS_Branded",3600,1f);Hediff_WS.Uncloak(v);break;
      case "blind":Items.Give(v,"LP_WS_Blinded",600,1f);break;
      case "stagger":v.stances?.stunner?.StunFor(180,a);break;
      case "banish":if(v.health.summaryHealth.SummaryHealthPercent<=.5f && !ClassSubclass.Bossy(v))Banishment.Banish(v,3600,"被逐恶斩放逐 1 分钟");break;
      case "ensnare":if(WizardSpells.TryControl(a,v,.6f,false)){v.stances?.stunner?.StunFor(240,a);Items.Mote(v,"被荆棘缠住",Nature);}break;
      case "splash":if(v.Map!=null)foreach(var x in Near(v.Map,v.Position,2f))if(x!=v && Hostile(a,x))WizardSpells.Hit(a,x,Dmg("Lightning"),amt*.5f,.2f);FleckMaker.ThrowLightningGlow(v.DrawPos,v.Map,2f);break;
     }
    }finally{inHit=false;}
    Items.Mote(v,h.def.label,Holy);if(o.consume && a.health.hediffSet.hediffs.Contains(h))a.health.RemoveHediff(h);
   }
   return n;
  }

  // ---------- 状态每秒 ----------
  public static void HediffTick(Hediff_CS h){
   var p=h.pawn;if(p==null || p.Dead)return;var n=h.def.defName;
   switch(n){
    case "LP_CS_Freedom":{var stun=p.stances?.stunner;if(stun!=null && stun.Stunned)stun.StopStun();foreach(var x in p.health.hediffSet.hediffs.Where(x=>x.def.defName=="LP_WS_Slowed" || x.def.defName=="LP_WS_Chilled" || x.def.defName=="LP_CS_SpikeSlowed" || x.def.defName=="LP_CS_SpiritSlowed" || x.def.defName=="LP_Entangled" || x.def.defName=="LP_WS_Webbed").ToList())p.health.RemoveHediff(x);return;}
    case "LP_CS_SpiritGuardians":if(!p.Spawned)return;foreach(var o in Near(p.Map,p.Position,h.radius))if(Hostile(p,o) && !o.Downed){WizardSpells.Hit(p,o,PaladinCore.Radiant,h.power*.5f,.3f);if(!o.Dead)Items.Give(o,"LP_CS_SpiritSlowed",120,1f);}if(Rand.Chance(.5f))FleckMaker.ThrowLightningGlow(p.DrawPos,p.Map,h.radius*.5f);return;
    case "LP_CS_SpiritWeapon":{if(!p.Spawned || (h.ticks+=1)%2!=0)return;var foe=Near(p.Map,p.Position,15f).Where(o=>Hostile(p,o) && !o.Downed).OrderBy(o=>o.Position.DistanceToSquared(p.Position)).FirstOrDefault();if(foe!=null){WizardSpells.Hit(p,foe,DamageDefOf.Blunt,h.power,.3f);FleckMaker.ThrowLightningGlow(foe.DrawPos,foe.Map,.7f);Items.Mote(foe,"灵体武器",Holy);}return;}
    case "LP_CS_Eyebite":{if(!p.Spawned || (h.ticks+=1)%3!=0)return;var foe=Near(p.Map,p.Position,12f).Where(o=>Hostile(p,o) && !o.Downed && !o.RaceProps.IsMechanoid).OrderBy(o=>o.Position.DistanceToSquared(p.Position)).FirstOrDefault();if(foe==null || !WizardSpells.TryControl(p,foe,.5f,true))return;
     int k=Rand.Range(0,3);if(k==0){Items.Give(foe,"LP_MagicSleep",600,1f);Items.Mote(foe,"目光：昏睡",Dark);}else if(k==1){var m=DefDatabase<MentalStateDef>.GetNamedSilentFail("PanicFlee");if(m!=null)foe.mindState?.mentalStateHandler?.TryStartMentalState(m,"目光术",true);Items.Mote(foe,"目光：恐慌",Dark);}else{Items.Give(foe,"LP_CS_Weakened",1800,1f);Items.Mote(foe,"目光：虚弱",Dark);}return;}
    case "LP_CS_Awakened":{if((h.ticks+=1)%40!=0 || p.training==null)return;foreach(var td in DefDatabase<TrainableDef>.AllDefsListForReading)if(p.training.CanAssignToTrain(td).Accepted && !p.training.HasLearned(td))p.training.Train(td,null,true);return;}
    case "LP_CS_Goodberry":ClassMelee.Heal(p,1f);return;
    case "LP_CS_Crusader":if(!p.Spawned)return;foreach(var o in Near(p.Map,p.Position,h.radius))if(Friendly(p,o) && (o.RaceProps.Humanlike || o.Faction==p.Faction)){var b=Items.Give(o,"LP_CS_CrusaderBuff",90,1f) as Hediff_CS;if(b!=null){b.caster=p;b.power=h.power;}}return;
    case "LP_CS_Heroism":{if(p.InMentalState && ClassSubclass.FearState(p.MentalStateDef))p.MentalState.RecoverFromState();var tp=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_CS_TempHP")) as Hediff_CS;if(tp==null || tp.pool<h.power)TempHp(p,h.power,600);return;}
    case "LP_CS_HeatMetal":if(MetalArmor(p))WizardSpells.Hit(h.caster,p,DamageDefOf.Flame,3f*h.power,.5f);return;
    case "LP_CS_Seared":WizardSpells.Hit(h.caster,p,DamageDefOf.Flame,h.power,.2f);return;
    case "LP_CS_Branded":Hediff_WS.Uncloak(p);return;
   }
  }
 }

 // 施法职业法术的状态：施法者、强度、临时生命、光环半径。
 public class Hediff_CS : HediffWithComps {
  public Pawn caster;public float power,pool,radius;public int ticks;int acc;
  bool Flier=>def.defName=="LP_CS_SpiderClimb" || def.defName=="LP_CS_Levitating";
  public override string LabelInBrackets=>pool>0f?"临时生命 "+pool.ToString("0"):base.LabelInBrackets;
  public override bool ShouldRemove=>base.ShouldRemove || def.defName=="LP_CS_TempHP" && pool<=0.01f;
  public override void PostAdd(DamageInfo? d){base.PostAdd(d);CasterEffects.Live++;if(Flier)Spellbook.Fliers.Add(pawn);if(def.defName=="LP_CS_Freedom")CasterEffects.HediffTick(this);}
  public override void PostRemoved(){base.PostRemoved();if(CasterEffects.Live>0)CasterEffects.Live--;if(Flier)Spellbook.Fliers.Remove(pawn);}
  public override void ExposeData(){
   base.ExposeData();Scribe_References.Look(ref caster,"lpCsCaster");Scribe_Values.Look(ref power,"lpCsPower");Scribe_Values.Look(ref pool,"lpCsPool");Scribe_Values.Look(ref radius,"lpCsRadius");Scribe_Values.Look(ref ticks,"lpCsTicks");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){CasterEffects.Live++;if(Flier)Spellbook.Fliers.Add(pawn);}
  }
  public override void TickInterval(int delta){base.TickInterval(delta);if((acc+=delta)<60)return;acc=0;CasterEffects.HediffTick(this);}
 }

 // 伤害钩子：临时生命吸收、指引之箭的标记、风墙挡箭。
 // 1.131.0 性能热修：这两个钩子每次受伤都走；状态定义改成静态 DefRef（原来每次按名字查缓存字典 5～6 次），OnHit 的判断改成循环（原来 LINQ 闭包分配）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class CasterSpellDamage {
  static readonly DefRef<HediffDef> outlined=new DefRef<HediffDef>("LP_CS_Outlined"),windWard=new DefRef<HediffDef>("LP_CS_WindWard"),passTrace=new DefRef<HediffDef>("LP_CS_PassTrace"),projected=new DefRef<HediffDef>("LP_CS_Projected"),absorb=new DefRef<HediffDef>("LP_CS_AbsorbElements"),tempHp=new DefRef<HediffDef>("LP_CS_TempHP");
  static Hediff First(HediffSet hs,DefRef<HediffDef> r){var d=r.Def;return d==null?null:hs.GetFirstHediffOfDef(d);}
  static void Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(CasterEffects.Live<=0 || absorbed || dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   var v=__instance;var a=dinfo.Instigator as Pawn;var hs=v.health.hediffSet;
   if(a!=null && a!=v && a.HostileTo(v)){
    var o=First(hs,outlined);if(o!=null){dinfo.SetAmount(dinfo.Amount*1.3f);var vv=v;var oo=o;Fiends.Defer(()=>{if(vv.health.hediffSet.hediffs.Contains(oo))vv.health.RemoveHediff(oo);});}
    if(dinfo.Def.isRanged && First(hs,windWard)!=null)dinfo.SetAmount(dinfo.Amount*.5f);
    if(dinfo.Def.isRanged && (First(hs,passTrace)!=null || First(hs,projected)!=null) && (CasterEffects.RollOverride??Rand.Value)<.3f){Items.Mote(v,"无踪：落空",new Color(.55f,.85f,.45f));absorbed=true;return;} // 1.131.0 无踪步、投射幻象
   }
   if(WizardSpells.EnergyKind(dinfo.Def)>=0 && First(hs,absorb) is Hediff_CS ab){ // 1.131.0 吸收元素：挡一半，下一次近战附加
    dinfo.SetAmount(dinfo.Amount*.5f);var vv2=v;var ab2=ab;Fiends.Defer(()=>{if(vv2.health.hediffSet.hediffs.Contains(ab2))vv2.health.RemoveHediff(ab2);var c=Items.Give(vv2,"LP_CS_AbsorbCharge",600,1f) as Hediff_CS;if(c!=null){c.caster=vv2;c.power=ab2.power;}Items.Mote(vv2,"吸收元素",new Color(.55f,.8f,1f));});
   }
   if(First(hs,tempHp) is Hediff_CS t && t.pool>0f){float take=Mathf.Min(t.pool,dinfo.Amount);t.pool-=take;dinfo.SetAmount(dinfo.Amount-take);if(dinfo.Amount<=.01f){absorbed=true;}}
  }
 }
 // 地狱斥责：被敌人打中时，对方受一次火焰伤害（之后消失）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PostApplyDamage))]
 public static class CasterSpellRebuke {
  static readonly DefRef<HediffDef> rebuke=new DefRef<HediffDef>("LP_CS_Rebuke");
  static bool HasOnHit(Pawn a){var l=a.health.hediffSet.hediffs;for(int i=0;i<l.Count;i++)if(l[i] is Hediff_CS && CasterEffects.OnHit.ContainsKey(l[i].def.defName))return true;return false;}
  static void Postfix(Pawn __instance,DamageInfo dinfo,float totalDamageDealt){
   if(CasterEffects.Live<=0 || totalDamageDealt<=0f || __instance.Dead)return;var a=dinfo.Instigator as Pawn;if(a==null || a==__instance || !a.HostileTo(__instance) || a.Dead)return;
   if(dinfo.Def!=null && !dinfo.Def.isExplosive && (dinfo.Weapon==null || dinfo.Weapon.IsWeapon) && !CasterEffects.InHit){bool melee=!dinfo.Def.isRanged && !(dinfo.Weapon!=null && dinfo.Weapon.IsRangedWeapon);var vv=__instance;var aa=a;if(HasOnHit(aa))Fiends.Defer(()=>CasterEffects.OnHitFire(aa,vv,melee));} // 1.131.0 惩击、武器附魔
   if(rebuke.Def!=null && __instance.health.hediffSet.GetFirstHediffOfDef(rebuke.Def) is Hediff_CS h){var v=__instance;float pw=h.power;Fiends.Defer(()=>{if(v.health.hediffSet.hediffs.Contains(h))v.health.RemoveHediff(h);if(!a.Dead){WizardSpells.Hit(v,a,DamageDefOf.Flame,pw,.2f);Items.Mote(a,"地狱斥责",new Color(1f,.5f,.3f));}});}
  }
 }
 // 换存档 / 新游戏：清掉静态计数（读档时 Hediff_CS 自己再加回来）。
 public class GameComponent_CasterSpells : GameComponent {
  public GameComponent_CasterSpells(Game game){CasterEffects.Live=0;}
 }
}
