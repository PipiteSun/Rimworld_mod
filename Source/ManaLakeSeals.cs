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
 // 1.133.0 魔力湖泊第四批：湖印常规货（Defs/ManaLake_Seals.xml）。结晶右键「吸收」约 1 小时，用完即碎；学会的「湖印」在冒险者页签单独一栏，不进职业树。
 // 槽位：法术 1 + 冒险者等级 ÷ 7（最多 4）、被动 1 + 冒险者等级 ÷ 12（最多 3），再乘设置「湖印槽位倍率」。同一结晶不能学两次；澄湖结晶抹掉一个（不退费）。
 // 湖息：上限 3（湖心 +1），每 24 小时回 1（湖心 ×1.5）。效果按施法属性每 1 点调整值 +5%，最多 +25%；不吃职业强度、超魔、储法戒指。
 public class LakeSealExt : DefModExtension {public int tier=1;public string ability="Int";public int breath=1;public int price;}
 public class CompProperties_LakeSeal : CompProperties_UseEffect {
  public AbilityDef ability;
  public CompProperties_LakeSeal(){compClass=typeof(CompUseEffect_LakeSeal);}
 }
 // 吸收湖印结晶：成功或失败都用掉一枚（失败结晶碎掉、湖音回响 1 天）。
 public class CompUseEffect_LakeSeal : CompUseEffect {
  public CompProperties_LakeSeal Props=>(CompProperties_LakeSeal)props;
  public override AcceptanceReport CanBeUsedBy(Pawn p){var r=LakeSeals.CannotLearn(p,Props.ability);return r!=null?(AcceptanceReport)r:base.CanBeUsedBy(p);}
  public override void DoEffect(Pawn usedBy){
   base.DoEffect(usedBy);if(LakeSeals.CannotLearn(usedBy,Props.ability)!=null)return;
   LakeSeals.Absorb(usedBy,Props.ability,Rand.Value,true);
   if(parent.stackCount>1)parent.SplitOff(1).Destroy();else parent.Destroy();
  }
  public override TaggedString ConfirmMessage(Pawn p)=>null;
 }
 // 澄湖结晶：用完打开窗口选一个湖印抹掉（选了才用掉结晶）。
 public class CompUseEffect_ClearLake : CompUseEffect {
  public override AcceptanceReport CanBeUsedBy(Pawn p){var d=LakeSeals.Data(Adventurer.Get(p),false);if(d==null || d.seals.Count==0)return "没有学会任何湖印";return base.CanBeUsedBy(p);}
  public override void DoEffect(Pawn usedBy){
   base.DoEffect(usedBy);var d=LakeSeals.Data(Adventurer.Get(usedBy),false);if(d==null || d.seals.Count==0)return;
   var crystal=parent;
   if(Adventurer.TestSuppressed || usedBy.Faction!=Faction.OfPlayer)return;
   Find.WindowStack.Add(new Dialog_SpellChoice("澄湖结晶","选一个湖印抹掉（不退费，空出槽位）。",d.seals.Select(a=>(a.LabelCap.Resolve(),a.description,(Action)(()=>{if(LakeSeals.Forget(usedBy,a) && !crystal.Destroyed){if(crystal.stackCount>1)crystal.SplitOff(1).Destroy();else crystal.Destroy();}}))).ToList(),()=>{}));
  }
  public override TaggedString ConfirmMessage(Pawn p)=>null;
 }

 // 每个人的魔力湖泊数据（ClassData.lake）。
 public class LakeData : IExposable {
  public List<AbilityDef> seals=new List<AbilityDef>();
  public float breath=3f;public int breathTick=int.MinValue;
  public int loreFail,resistDay=-1,resistUsed,heartTick; // 通用秘传：上次吸收失败、传奇抵抗（日 / 次数）、第二心脏上次触发
  public void ExposeData(){
   Scribe_Collections.Look(ref seals,"seals",LookMode.Def);Scribe_Values.Look(ref breath,"breath",3f);Scribe_Values.Look(ref breathTick,"breathTick",int.MinValue);
   Scribe_Values.Look(ref loreFail,"loreFail");Scribe_Values.Look(ref resistDay,"resistDay",-1);Scribe_Values.Look(ref resistUsed,"resistUsed");Scribe_Values.Look(ref heartTick,"heartTick");
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(seals==null)seals=new List<AbilityDef>();seals.RemoveAll(x=>x==null);}
  }
 }

 public static class LakeSeals {
  public const float BreathBase=3f,ConduitChance=.15f,ConduitDamage=4f;public const int BreathPeriod=60000;
  public static readonly Color Teal=new Color(.45f,.85f,.8f);
  static List<AbilityDef> all;static List<ThingDef> crystals;
  public static List<AbilityDef> All=>all??(all=DefDatabase<AbilityDef>.AllDefsListForReading.Where(d=>d.HasModExtension<LakeSealExt>()).ToList());
  public static List<ThingDef> Crystals=>crystals??(crystals=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.GetCompProperties<CompProperties_LakeSeal>()?.ability!=null).ToList());
  public static void ResetCache(){all=null;crystals=null;}
  public static LakeSealExt Ext(AbilityDef d)=>d?.GetModExtension<LakeSealExt>();
  public static bool Is(AbilityDef d)=>Ext(d)!=null;
  public static bool Passive(AbilityDef d)=>d?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault()?.passive==true;
  public static AbilityDef AbilityOf(ThingDef crystal)=>crystal?.GetCompProperties<CompProperties_LakeSeal>()?.ability;
  public static ThingDef CrystalFor(AbilityDef d)=>Crystals.FirstOrDefault(c=>AbilityOf(c)==d);
  public static string PassiveHediff(AbilityDef d)=>d==null?null:"LP_LSP_"+d.defName.Substring(6);
  public static LakeData Data(Hediff_Adventurer h,bool create=true){if(h==null)return null;if(h.classes==null){if(!create)return null;h.classes=new ClassData();}if(h.classes.lake==null && create)h.classes.lake=new LakeData();return h.classes.lake;}
  public static bool Knows(Hediff_Adventurer h,AbilityDef d){var x=Data(h,false);return x!=null && x.seals.Contains(d);}
  public static bool HasPassive(Pawn p,string hediff)=>p!=null && Items.Has(p,hediff);

  // ---------- 槽位 ----------
  public static float Mult=>LichMod.Settings?.lakeSlotMult??1f;
  static int Scaled(int n)=>Mathf.Max(1,Mathf.RoundToInt(n*Mult));
  public static int SpellSlots(Hediff_Adventurer h)=>h==null?0:Scaled(Mathf.Min(4,1+h.level/7));
  public static int PassiveSlots(Hediff_Adventurer h)=>h==null?0:Scaled(Mathf.Min(3,1+h.level/12));
  public static int Used(Hediff_Adventurer h,bool passive){var x=Data(h,false);return x==null?0:x.seals.Count(d=>Passive(d)==passive);}
  public static int TierLevel(int tier)=>tier<=1?1:tier==2?5:10;
  public static int TierInt(int tier)=>tier<=1?4:tier==2?6:8;
  public static int Intellect(Pawn p)=>p?.skills?.GetSkill(SkillDefOf.Intellectual)?.Level??0;
  public static bool PsychicDeaf(Pawn p)=>p!=null && p.GetStatValue(StatDefOf.PsychicSensitivity)<=0f && !Items.Has(p,"LP_LO_MindBlank") && !Items.Has(p,"LP_LS_Silenced");
  public static string CannotLearn(Pawn p,AbilityDef d){
   var e=Ext(d);if(e==null)return "这枚结晶里没有湖印";
   if(p?.RaceProps==null || !p.RaceProps.Humanlike || p.RaceProps.IsMechanoid)return "只有人形生物能吸收湖印结晶";
   if(!Adventurer.Enabled)return "冒险者系统没有开启";
   var h=Adventurer.Get(p);if(h==null)return "没有冒险者等级";
   if(Knows(h,d))return "已经学会「"+d.label+"」";
   if(h.level<TierLevel(e.tier))return "需要冒险者 "+TierLevel(e.tier)+" 级（"+TierName(e.tier)+"湖印）";
   if(Intellect(p)<TierInt(e.tier))return "需要智识 "+TierInt(e.tier)+" 级（"+TierName(e.tier)+"湖印）";
   bool passive=Passive(d);
   if(!passive && PsychicDeaf(p))return "永久心灵聋只能学湖印被动";
   int slots=passive?PassiveSlots(h):SpellSlots(h);if(Used(h,passive)>=slots)return "湖印"+(passive?"被动":"法术")+"槽已满（"+Used(h,passive)+" / "+slots+"），可以先用澄湖结晶抹掉一个";
   return null;
  }
  public static string TierName(int t)=>t<=1?"一阶":t==2?"二阶":"三阶";
  public static float AbsorbChance(Pawn p,AbilityDef d){
   var e=Ext(d);if(e==null)return 0f;if(Necro.Lich(p)!=null)return 1f; // 巫妖沿用卷轴规则，吸收不会失败
   int i=Intellect(p);return e.tier<=1?1f:e.tier==2?Mathf.Clamp01(.9f+i*.01f):Mathf.Clamp01(.7f+i*.02f);
  }
  public static bool Absorb(Pawn p,AbilityDef d,float roll,bool message){
   if(CannotLearn(p,d)!=null)return false;float ch=AbsorbChance(p,d);
   if(roll<ch){Learn(p,d);if(message && p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"吸收了湖印结晶，学会了湖印「"+d.label+"」。",p,MessageTypeDefOf.PositiveEvent,false);return true;}
   Items.Give(p,"LP_LS_Echo");
   if(message && p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"没能吸收湖印「"+d.label+"」（成功率 "+ch.ToStringPercent()+"），结晶碎了，耳边湖音回响。",p,MessageTypeDefOf.NegativeEvent,false);
   return false;
  }
  public static void Learn(Pawn p,AbilityDef d){
   var h=Adventurer.Get(p);var x=Data(h);if(x.seals.Contains(d))return;x.seals.Add(d);LakeLore.Live=true;LakeArcana.Touched=true;
   if(!Passive(d) && p.abilities!=null && p.abilities.GetAbility(d)==null)p.abilities.GainAbility(d);
   Sync(h);
  }
  public static bool Forget(Pawn p,AbilityDef d){
   var h=Adventurer.Get(p);var x=Data(h,false);if(x==null || !x.seals.Remove(d))return false;
   if(p.abilities?.GetAbility(d)!=null)p.abilities.RemoveAbility(d);
   if(Passive(d))Items.Remove(p,PassiveHediff(d));
   Items.Mote(p,"抹掉湖印："+d.label,Teal);return true;
  }
  // 能力按钮、被动状态和湖印对齐（读档、学会、抹掉后）。
  public static int Sync(Hediff_Adventurer h){
   var p=h?.pawn;var x=Data(h,false);if(p?.abilities==null || x==null)return 0;int n=0;
   foreach(var d in x.seals){
    if(Passive(d)){var hd=PassiveHediff(d);if(!Items.Has(p,hd) && Items.Hediff(hd)!=null){Items.Give(p,hd);n++;}}
    else if(p.abilities.GetAbility(d)==null){p.abilities.GainAbility(d);n++;}
   }
   return n;
  }

  // ---------- 湖息 ----------
  public static float BreathMax(Hediff_Adventurer h)=>BreathBase+(HasPassive(h?.pawn,"LP_LSP_LakeHeart")?1f:0f);
  public static int Period(Hediff_Adventurer h)=>HasPassive(h?.pawn,"LP_LSP_LakeHeart")?Mathf.RoundToInt(BreathPeriod/1.5f):BreathPeriod;
  public static float Breath(Hediff_Adventurer h){var x=Data(h,false);if(x==null)return 0f;Regen(h,x,Find.TickManager.TicksGame);return x.breath;}
  public static void Regen(Hediff_Adventurer h,LakeData x,int now){
   float max=BreathMax(h);if(x.breathTick==int.MinValue){x.breathTick=now;return;}
   if(x.breath>=max){x.breath=max;x.breathTick=now;return;}
   int el=now-x.breathTick;if(el<=0)return;
   x.breath=Mathf.Min(max,x.breath+(float)el/Period(h));x.breathTick=now;
  }
  public static string CannotCast(Pawn p,AbilityDef def){
   var e=Ext(def);if(e==null || Passive(def))return null;
   if(PsychicDeaf(p))return "心灵聋放不了湖印法术";
   var h=Adventurer.Get(p);float b=Breath(h);
   if(b+.001f<e.breath)return "湖息不足（"+b.ToString("0.#")+" / "+e.breath+"）";
   return null;
  }
  public static bool Pay(Pawn p,AbilityDef def){var e=Ext(def);var h=Adventurer.Get(p);var x=Data(h,false);if(e==null || x==null)return false;Regen(h,x,Find.TickManager.TicksGame);if(x.breath+.001f<e.breath)return false;x.breath=Mathf.Max(0f,x.breath-e.breath);return true;}
  public static float Scale(Pawn p,AbilityDef def){
   var e=Ext(def);var h=Adventurer.Get(p);if(e==null || h==null)return 1f;int i=LakeLore.AbilityIdx(e.ability);if(i<0)return 1f;
   return 1f+Mathf.Clamp(Adventurer.Mod(h.Eff(i))*.05f,0f,.25f);
  }
  public static void Tick250(Hediff_Adventurer h){
   var x=h.classes.lake;if(x.seals.Count==0)return;var p=h.pawn;
   LakeLore.Live=true;Regen(h,x,Find.TickManager.TicksGame);
   if(Find.TickManager.TicksGame%2500<250)Sync(h);
   if(p.Spawned && HasPassive(p,"LP_LSP_WeaveSense"))LakeLore.Reveal(p,8f);
  }

  // ---------- 施放 ----------
  static bool Hostile(Pawn c,Pawn t)=>AnyTarget.Hostile(c,t); // 1.136.0 热修：手动选的目标不分敌我
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;var map=caster.Map;
   switch(e.effect){
    case "LS_Mending":if(MendTarget(target.Thing)==null)return "需要一件有耐久损耗的服装或武器（或身上有的人）。";return null;
    case "LS_PurifyFood":case "LS_Buff":case "LS_FalseLife":case "LS_CalmMind":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(t.HostileTo(caster))return "只能对友方使用。";return null;
    case "LS_ArcaneLock":if(!(target.Thing is Building_Door))return "需要选择一扇门。";return null;
    case "LS_Counterspell":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
    case "LS_Alarm":case "LS_Shatter":case "LS_Thunderwave":if(map==null || !target.Cell.InBounds(map))return "需要选择地图上的一处。";return null;
   }
   return null;
  }
  public static Thing MendTarget(Thing t){
   if(t is Pawn p){Thing best=null;float worst=1f;foreach(var a in (p.apparel?.WornApparel?.Cast<Thing>()??Enumerable.Empty<Thing>()).Concat(p.equipment?.AllEquipmentListForReading?.Cast<Thing>()??Enumerable.Empty<Thing>())){if(!a.def.useHitPoints)continue;float f=(float)a.HitPoints/a.MaxHitPoints;if(f<worst){worst=f;best=a;}}return best;}
   if(t!=null && (t.def.IsApparel || t.def.IsWeapon) && t.def.useHitPoints && t.HitPoints<t.MaxHitPoints)return t;
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   if(Is(def) && !Pay(p,def)){Items.Mote(p,"湖息不足",Color.gray);return true;}
   LakeLore.Live=true;var t=target.Thing as Pawn;float s=Scale(p,def);
   switch(e.effect){
    case "LS_Mending":Mend(MendTarget(target.Thing),e.power*s);Items.Mote(p,"修补术",Teal);return true;
    case "LS_PurifyFood":if(t!=null){var fp=t.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.FoodPoisoning);if(fp!=null)t.health.RemoveHediff(fp);Items.Give(t,"LP_LS_Purified",Mathf.RoundToInt(e.duration*s));Items.Mote(t,"净化饮食",Teal);}return true;
    case "LS_Alarm":ArcanaZones.Add(p,ArcanaZones.Alarm,target.Cell,e.radius,e.duration,0f);Items.Mote(p,"警报术",Teal);return true;
    case "LS_Buff":if(t!=null){Items.Give(t,e.hediff,Mathf.RoundToInt(e.duration*s));Items.Mote(t,def.label,Teal);}return true;
    case "LS_Thunderwave":Thunderwave(p,target.Cell,e.radius,e.power*s,Rand.Value);return true;
    case "LS_ArcaneLock":if(target.Thing is Building_Door door)LakeLocks.Lock(door,e.duration);Items.Mote(p,"秘法锁",Teal);return true;
    case "LS_Augury":Augury(p,Rand.Value);return true;
    case "LS_FalseLife":if(t!=null){LakeArcana.Touched=true;Items.Give(t,"LP_ACB_Shield",e.duration,e.power*s);Items.Mote(t,"虚假生命",Teal);}return true;
    case "LS_CreateFood":CreateFood(p,Mathf.RoundToInt(e.power));return true;
    case "LS_CalmMind":if(t!=null)Calm(t,Mathf.RoundToInt(e.duration*s));return true;
    case "LS_Shatter":Shatter(p,target.Cell,e.radius,e.power*s);return true;
    case "LS_Counterspell":if(Hostile(p,t))Counterspell(p,t,e.duration,Rand.Value);return true;
    case "LS_TinyHut":TinyHut(p,e.duration,e.radius);return true;
   }
   return false;
  }
  public static int Mend(Thing t,float frac){if(t==null)return 0;int add=Mathf.CeilToInt(t.MaxHitPoints*frac);int before=t.HitPoints;t.HitPoints=Mathf.Min(t.MaxHitPoints,t.HitPoints+add);return t.HitPoints-before;}
  public static int Thunderwave(Pawn p,IntVec3 toward,float range,float power,float roll){
   int n=0;foreach(var t in ClassLegendary.Cone(p,toward,range)){
    if(!Hostile(p,t))continue;t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,power,0f,-1f,p));
    if(!t.Dead && t.Spawned){var dest=DNDPush.Destination(t.Map,t.Position,(t.Position-p.Position).ToVector3(),2);DNDPush.Move(t,dest);if((roll=Rand.Value)<.3f)t.stances?.stunner?.StunFor(120,p);}n++;
   }
   Items.Mote(p,"雷鸣波",Teal);return n;
  }
  public static bool Augury(Pawn p,float roll){
   var h=Adventurer.Get(p);float ch=Mathf.Clamp01(.6f+.03f*(h==null?0:Adventurer.Mod(h.Eff(Adventurer.Wis))));
   if(roll>=ch){Items.Mote(p,"卜问没有回应",Color.gray);if(p.Faction==Faction.OfPlayer && !Adventurer.TestSuppressed)Messages.Message("卜问没有得到回应（成功率 "+ch.ToStringPercent()+"），湖息已经用掉。",p,MessageTypeDefOf.NeutralEvent,false);return false;}
   WizardSpells2.Scry(p,0);Items.Mote(p,"卜问",Teal);return true;
  }
  public static int CreateFood(Pawn p,int n){
   if(p.Map==null)return 0;var d=ThingDefOf.MealSimple;int left=n;
   while(left>0){var t=ThingMaker.MakeThing(d);t.stackCount=Mathf.Min(left,d.stackLimit);left-=t.stackCount;GenPlace.TryPlaceThing(t,p.Position,p.Map,ThingPlaceMode.Near);}
   Items.Mote(p,"造粮术",Teal);return n;
  }
  public static bool Calm(Pawn t,int ticks){
   var ms=t.MentalState;if(ms!=null && !ms.def.IsAggro){ms.RecoverFromState();Items.Mote(t,"宁神",Teal);return true;}
   Items.Give(t,"LP_LS_Calm",ticks);Items.Mote(t,"宁神术",Teal);return false;
  }
  public static int Shatter(Pawn p,IntVec3 c,float radius,float power){
   int n=0;var map=p.Map;if(map==null)return 0;
   foreach(var cell in GenRadial.RadialCellsAround(c,radius,true))if(cell.InBounds(map))FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,.8f,new Color(.8f,.9f,1f));
   foreach(var t in Races124.PawnsNear(map,c,radius).ToList()){if(!Hostile(p,t))continue;t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,power*(t.RaceProps.IsMechanoid?3f:1f),0f,-1f,p));n++;}
   foreach(var b in GenRadial.RadialCellsAround(c,radius,true).Where(x=>x.InBounds(map)).SelectMany(x=>x.GetThingList(map)).OfType<Building>().Distinct().ToList())if(b.Faction!=p.Faction && b.def.useHitPoints){b.TakeDamage(new DamageInfo(DamageDefOf.Blunt,power*3f,0f,-1f,p));n++;}
   Items.Mote(p,"粉碎音波",Teal);return n;
  }
  public static bool Counterspell(Pawn p,Pawn t,int ticks,float roll){
   if(ClassMartial2.Boss(t) && roll>=.5f){Items.Mote(t,"反制失败",Color.gray);return false;}
   if(t.stances?.curStance is Stance_Warmup)t.stances.CancelBusyStanceHard();
   if(t.CurJob?.ability!=null)t.jobs.EndCurrentJob(JobCondition.InterruptForced);
   Items.Give(t,"LP_LS_Silenced",ticks);Items.Mote(t,"被反制",new Color(.75f,.6f,.9f));LakeLore.Live=true;return true;
  }
  public static Building_TinyHut TinyHut(Pawn p,int ticks,float radius){
   var def=DefCache<ThingDef>.Get("LP_LS_TinyHut");if(def==null || p.Map==null)return null;
   foreach(var old in p.Map.listerThings.ThingsOfDef(def).OfType<Building_TinyHut>().Where(x=>x.caster==p).ToList())old.Destroy();
   var hut=(Building_TinyHut)ThingMaker.MakeThing(def);hut.caster=p;hut.left=ticks;hut.radius=radius;hut.SetFaction(p.Faction);GenSpawn.Spawn(hut,p.Position,p.Map);
   Items.Mote(p,"小屋术",Teal);return hut;
  }
  public static void AlarmTriggered(Map map,Pawn foe,IntVec3 c){
   foreach(var col in map.mapPawns.FreeColonistsSpawned.ToList())if(!col.Awake())RestUtility.WakeUp(col);
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("警报术","湖印警报：「"+foe.LabelShort+"」闯进了警报符文的范围。殖民者都被叫醒了。",LetterDefOf.ThreatSmall,new TargetInfo(c,map));
  }

  // ---------- 冒险者页签：「秘传」（通用秘传）和「湖印」两栏 ----------
  public static float DrawRows(float y,float w,Pawn p,Hediff_Adventurer h){
   var lore=h.classes?.arcana?.Where(a=>LakeArcana.Ext(a)?.Universal==true).ToList()??new List<AbilityDef>();
   if(lore.Count>0)y=IconRow(y,w,"秘传",new Color(.95f,.85f,.55f),lore,a=>a.LabelCap+"（通用秘传 "+LakeArcana.Ext(a).ring+" 环）\n\n"+a.description,LakeArcana.SlotTip);
   var x=Data(h,false);
   if(x!=null && x.seals.Count>0 || Adventurer.Enabled && h.level>=1 && x!=null){
    var head=new Rect(0f,y,w,22f);GUI.color=Teal;
    LPUI.LabelFit(head,"湖印　法术 "+Used(h,false)+" / "+SpellSlots(h)+"　被动 "+Used(h,true)+" / "+PassiveSlots(h)+"　湖息 "+Breath(h).ToString("0.#")+" / "+BreathMax(h).ToString("0"),SlotTip);GUI.color=Color.white;y+=24f;
    if(x.seals.Count>0)y=IconRow(y,w,"湖印",Teal,x.seals,a=>a.LabelCap+"（湖印"+TierName(Ext(a).tier)+(Passive(a)?"被动":"法术，湖息 "+Ext(a).breath)+"）\n\n"+a.description,SlotTip);
   }
   return y;
  }
  static float IconRow(float y,float w,string title,Color col,List<AbilityDef> list,Func<AbilityDef,string> tip,string headTip){
   var row=new Rect(0f,y,w,44f);Widgets.DrawBoxSolid(row,new Color(col.r*.3f,col.g*.3f,col.b*.3f,.25f));
   GUI.color=col;Widgets.Label(new Rect(6f,y+12f,50f,24f),title);GUI.color=Color.white;TooltipHandler.TipRegion(new Rect(0f,y,56f,44f),headTip);
   float x0=60f,cw=Mathf.Min(150f,(w-60f)/Mathf.Max(1,list.Count));
   foreach(var a in list){var cell=new Rect(x0,y+2f,cw-6f,40f);x0+=cw;GUI.DrawTexture(new Rect(cell.x,cell.y+2f,36f,36f),a.uiIcon??BaseContent.BadTex);LPUI.Tiny(new Rect(cell.x+40f,cell.y+10f,cell.width-40f,20f),a.LabelCap);TooltipHandler.TipRegion(cell,tip(a));}
   return y+46f;
  }
  public const string SlotTip="湖印：魔力湖泊的常规结晶，右键吸收（约 1 小时），用完即碎。\n法术槽 = 1 + 冒险者等级 ÷ 7（最多 4），被动槽 = 1 + 冒险者等级 ÷ 12（最多 3），再乘设置里的「湖印槽位倍率」。\n湖印法术用湖息：上限 3，每 24 小时回 1；效果按施法属性，每 1 点调整值 +5%，最多 +25%。\n槽满时可以用澄湖结晶抹掉一个（不退费）。";
 }

 // 秘法锁：门 → 到期刻。敌人砸门伤害 ×0.33。
 public class LakeLocks : MapComponent {
  public Dictionary<int,int> locks=new Dictionary<int,int>();
  public LakeLocks(Map m):base(m){}
  public static void Lock(Building_Door d,int ticks){var c=d?.Map?.GetComponent<LakeLocks>();if(c==null)return;c.locks[d.thingIDNumber]=Find.TickManager.TicksGame+ticks;LakeLore.Live=true;}
  public static bool Locked(Thing t){if(!(t is Building_Door) || t.Map==null)return false;var c=t.Map.GetComponent<LakeLocks>();if(c==null || c.locks.Count==0 || !c.locks.TryGetValue(t.thingIDNumber,out int until))return false;if(Find.TickManager.TicksGame>until){c.locks.Remove(t.thingIDNumber);return false;}return true;}
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref locks,"lpLakeLocks",LookMode.Value,LookMode.Value);if(Scribe.mode==LoadSaveMode.PostLoadInit){if(locks==null)locks=new Dictionary<int,int>();if(locks.Count>0)LakeLore.Live=true;}}
 }
 [HarmonyPatch(typeof(ThingWithComps),nameof(ThingWithComps.PreApplyDamage))]
 public static class LakeLockDamage {
  static void Prefix(ThingWithComps __instance,ref DamageInfo dinfo){
   if(!LakeLore.Live || !(__instance is Building_Door) || dinfo.Amount<=0f)return;
   var a=dinfo.Instigator;if(a==null || a.Faction==null || !a.Faction.HostileTo(__instance.Faction??Faction.OfPlayer))return;
   if(LakeLocks.Locked(__instance))dinfo.SetAmount(dinfo.Amount*.33f);
  }
 }

 // 小屋术穹顶：施法者离开或 8 小时后消失；里面的人恒温（小屋庇护）。
 public class Building_TinyHut : Building {
  public Pawn caster;public int left=20000;public float radius=3f;int acc;
  protected override void Tick(){
   base.Tick();left--;if(++acc<60)return;acc=0;
   if(left<=0 || caster==null || caster.Dead || !caster.Spawned || caster.Map!=Map || caster.Position.DistanceTo(Position)>radius+.5f){if(!Destroyed)Destroy(DestroyMode.Vanish);return;}
   foreach(var p in Races124.PawnsNear(Map,Position,radius).ToList())if(!p.Dead && !p.HostileTo(caster))Items.Give(p,"LP_LS_Sheltered",150);
  }
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref caster,"lpCaster");Scribe_Values.Look(ref left,"lpLeft",20000);Scribe_Values.Look(ref radius,"lpRadius",3f);}
 }

 // 净化饮食：2 天内不会食物中毒。
 [HarmonyPatch]
 public static class LakePurifiedFood {
  static System.Reflection.MethodBase TargetMethod()=>AccessTools.Method(typeof(FoodUtility),"AddFoodPoisoningHediff");
  static bool Prepare()=>TargetMethod()!=null;
  static bool Prefix(Pawn pawn)=>!(LakeLore.Live && Items.Has(pawn,"LP_LS_Purified"));
 }
 // 暗视术：黑暗不扣工作速度和瞄准。
 [HarmonyPatch]
 public static class LakeDarkvision {
  static System.Reflection.MethodBase TargetMethod()=>AccessTools.Method(typeof(StatPart_Glow),"ActiveFor");
  static bool Prepare()=>TargetMethod()!=null;
  static void Postfix(Thing t,ref bool __result){if(__result && LakeLore.Live && t is Pawn p && Items.Has(p,"LP_LS_Darkvision"))__result=false;}
 }

 // 货单：湖印每次补货不重复抽取；三阶只在好感 ≥ 75 时出现。
 public class StockGenerator_LakeSeal : StockGenerator {
  public int tier3Goodwill=75;
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   int gw=StockGenerator_Arcana.TestGoodwill??ManaLake.Goodwill(faction??ManaLake.Faction);
   var pool=LakeSeals.Crystals.Where(d=>{var e=LakeSeals.Ext(LakeSeals.AbilityOf(d));return e!=null && (e.tier<3 || gw>=tier3Goodwill) && d.tradeability.TraderCanSell();}).ToList();
   int n=countRange.RandomInRange;
   for(int i=0;i<n && pool.Count>0;i++){var pick=pool.RandomElement();pool.Remove(pick);yield return ThingMaker.MakeThing(pick);}
  }
  public override bool HandlesThingDef(ThingDef d)=>d!=null && (d.GetCompProperties<CompProperties_LakeSeal>()!=null || d.defName=="LP_ClearLakeCrystal");
 }
 // 许愿术结晶：据点、好感 100 时 10%，每个存档最多出现一次。
 public class StockGenerator_LoreWish : StockGenerator {
  public float chance=.1f;public int minGoodwill=100;
  public static float? TestRoll;
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   var gc=GameComponent_Arcana.Instance;if(gc==null || gc.wishSeen)yield break;
   int gw=StockGenerator_Arcana.TestGoodwill??ManaLake.Goodwill(faction??ManaLake.Faction);if(gw<minGoodwill)yield break;
   if((TestRoll??Rand.Value)>=chance)yield break;
   var crystal=LakeArcana.CrystalFor(DefCache<AbilityDef>.Get(LakeLore.WishName));if(crystal==null)yield break;
   gc.wishSeen=true;yield return ThingMaker.MakeThing(crystal);
  }
  public override bool HandlesThingDef(ThingDef d)=>false;
 }

 // ---------- 设置：湖印槽位倍率 ----------
 public partial class Settings {
  public float lakeSlotMult=1f;
  void ExposeManaLake(){Scribe_Values.Look(ref lakeSlotMult,"lpLakeSlotMult",1f);if(lakeSlotMult!=.5f && lakeSlotMult!=1f && lakeSlotMult!=2f)lakeSlotMult=1f;}
 }
 public static class LakeSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;var r=l.GetRect(28f);
   Widgets.Label(r.LeftPart(.6f),"湖印槽位倍率：×"+s.lakeSlotMult.ToString("0.#"));
   var b=LPUI.Row(r.RightPart(.4f),3,4f);float[] v={.5f,1f,2f};
   for(int i=0;i<3;i++)if(Widgets.ButtonText(b[i],"×"+v[i].ToString("0.#"),true,true,s.lakeSlotMult!=v[i]))s.lakeSlotMult=v[i];
   TooltipHandler.TipRegion(r,"（1.133.0）魔力湖泊湖印：法术槽、被动槽的数量乘这个倍率（四舍五入，至少 1 个）。");
  }
 }
}
