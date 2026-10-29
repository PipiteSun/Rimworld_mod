using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.53.0 战斗风格（见 ArtSource/MartialClasses-1.53.0-spec.md 第二节 5）：10 种，存在冒险者状态里（styles、styleReadyTick、styleAdaptSlot）。
 // 槽位数由 VPE 桥登记的 SlotsHook 给（有物理路径 1 个，战士冒险者 10 级 2 个）；敌人等级 ≥ 3 时 50% 随机 1 个，不看槽位。
 // 性能：生效的风格合成一个位掩码（styleMask），只在冒险者状态的 250 刻钩、选风格、读档时重算；热路径先查 Users（HashSet<int>）。
 public class LP_FightingStyleDef : Def {
  public string iconPath;public int order;
  public List<string> enemyTags; // 敌人随机：melee / ranged
  public Type workerClass=typeof(StyleWorker);
  [Unsaved] StyleWorker worker;[Unsaved] Texture2D icon;
  public StyleWorker Worker{get{if(worker==null){worker=(StyleWorker)Activator.CreateInstance(workerClass);worker.def=this;}return worker;}}
  public Texture2D Icon=>icon??(icon=iconPath.NullOrEmpty()?BaseContent.BadTex:ContentFinder<Texture2D>.Get(iconPath,false)??BaseContent.BadTex);
  public int Bit=>1<<Mathf.Clamp(order,0,30);
 }
 public class StyleWorker {
  public LP_FightingStyleDef def;
  public virtual void Gained(Pawn p,Hediff_Adventurer h){}
  public virtual void Lost(Pawn p,Hediff_Adventurer h){}
 }

 public static class Styles {
  public const int ReadyDelay=180000,MaxSlots=2,InterceptCooldown=600,GrappleTicks=120,EnemyMinLevel=3;
  public const float ArcheryAim=1.08f,ArcheryRange=2f,ProtectFactor=.85f,InterceptBase=3f,InterceptRadius=3f,TwoWeaponChance=.3f,BlindRadius=3f,ThrownRange=1.3f,ThrownAim=1.1f,GrappleChance=.2f,DefenseSharp=.4f,EnemyChance=.5f;
  static LP_FightingStyleDef F(ref LP_FightingStyleDef c,string n)=>c??(c=DefDatabase<LP_FightingStyleDef>.GetNamedSilentFail(n));
  static LP_FightingStyleDef archery,defense,dueling,gwf,protection,interception,twoWeapon,blind,thrown,unarmed;
  public static LP_FightingStyleDef Archery=>F(ref archery,"LP_Style_Archery");
  public static LP_FightingStyleDef Defense=>F(ref defense,"LP_Style_Defense");
  public static LP_FightingStyleDef Dueling=>F(ref dueling,"LP_Style_Dueling");
  public static LP_FightingStyleDef GreatWeapon=>F(ref gwf,"LP_Style_GreatWeapon");
  public static LP_FightingStyleDef Protection=>F(ref protection,"LP_Style_Protection");
  public static LP_FightingStyleDef Interception=>F(ref interception,"LP_Style_Interception");
  public static LP_FightingStyleDef TwoWeapon=>F(ref twoWeapon,"LP_Style_TwoWeapon");
  public static LP_FightingStyleDef BlindFighting=>F(ref blind,"LP_Style_BlindFighting");
  public static LP_FightingStyleDef Thrown=>F(ref thrown,"LP_Style_Thrown");
  public static LP_FightingStyleDef Unarmed=>F(ref unarmed,"LP_Style_Unarmed");
  public static IEnumerable<LP_FightingStyleDef> All=>DefDatabase<LP_FightingStyleDef>.AllDefsListForReading.OrderBy(s=>s.order);

  // VPE 桥登记：殖民者的槽位数（没有 VPE 时为空 → 殖民者没有槽位）。
  public static Func<Pawn,int> SlotsHook;
  public static int Slots(Pawn p)=>p==null || !Adventurer.Enabled || SlotsHook==null?0:Mathf.Clamp(SlotsHook(p),0,MaxSlots);
  static bool Npc(Pawn p)=>p.Faction==null || !p.Faction.IsPlayer;
  // 能生效的槽位数：敌人、访客不看槽位；殖民者看 SlotsHook。
  public static int Limit(Hediff_Adventurer h)=>h?.pawn==null?0:Npc(h.pawn)?MaxSlots:Slots(h.pawn);
  public static bool Adapting(Hediff_Adventurer h,int slot)=>h!=null && slot==h.styleAdaptSlot && Find.TickManager!=null && Find.TickManager.TicksGame<h.styleReadyTick;
  public static float AdaptDaysLeft(Hediff_Adventurer h)=>Mathf.Max(0f,(h.styleReadyTick-Find.TickManager.TicksGame)/(float)GenDate.TicksPerDay);

  // ---------- 缓存（250 刻刷新） ----------
  public static readonly HashSet<int> Users=new HashSet<int>();
  public static readonly HashSet<Pawn> protectors=new HashSet<Pawn>(),interceptors=new HashSet<Pawn>(),blindFighters=new HashSet<Pawn>();
  static readonly Dictionary<int,int> lastIntercept=new Dictionary<int,int>();
  public static void ResetCaches(){Users.Clear();protectors.Clear();interceptors.Clear();blindFighters.Clear();lastIntercept.Clear();}
  // 热路径：一次 HashSet 查找；命中后再看掩码。
  public static bool Has(Pawn p,LP_FightingStyleDef s){
   if(s==null || p==null || !Users.Contains(p.thingIDNumber) || !Adventurer.Enabled)return false;
   var h=Adventurer.Get(p);return h!=null && (h.styleMask & s.Bit)!=0;
  }
  public static int ComputeMask(Hediff_Adventurer h){
   if(h==null || h.styles.Count==0 || !Adventurer.Enabled)return 0;
   int limit=Limit(h),mask=0;
   for(int i=0;i<h.styles.Count && i<limit;i++){var s=h.styles[i];if(s!=null && !Adapting(h,i))mask|=s.Bit;}
   return mask;
  }
  static void Set(HashSet<Pawn> set,Pawn p,bool on){if(on)set.Add(p);else set.Remove(p);}
  static void Cond(Pawn p,string hediff,bool on){if(on){if(!Items.Has(p,hediff))Items.Give(p,hediff);}else if(Items.Has(p,hediff))Items.Remove(p,hediff);}
  public static void Refresh(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return;
   if(h.styles.Count==0 && h.styleMask==0 && !Users.Contains(p.thingIDNumber))return; // 没有风格的人：直接返回
   int mask=p.Dead?0:ComputeMask(h);h.styleMask=mask;
   if(mask!=0)Users.Add(p.thingIDNumber);else Users.Remove(p.thingIDNumber);
   bool up=mask!=0 && p.Spawned && !p.Downed;
   Set(protectors,p,up && (mask & Bit(Protection))!=0);
   Set(interceptors,p,up && (mask & Bit(Interception))!=0);
   Set(blindFighters,p,up && (mask & Bit(BlindFighting))!=0);
   if(p.Dead)return;
   Cond(p,"LP_StyleDefense",(mask & Bit(Defense))!=0 && DefenseArmor(p));
   Cond(p,"LP_StyleDueling",(mask & Bit(Dueling))!=0 && DuelingWeapon(p));
   Cond(p,"LP_StyleGreatWeapon",(mask & Bit(GreatWeapon))!=0 && Feats.TwoHanded(p));
   Cond(p,"LP_StyleUnarmed",(mask & Bit(Unarmed))!=0 && p.equipment!=null && p.equipment.Primary==null);
   if(lastIntercept.Count>64){int now=Find.TickManager.TicksGame;foreach(var k in lastIntercept.Where(kv=>now-kv.Value>InterceptCooldown).Select(kv=>kv.Key).ToList())lastIntercept.Remove(k);}
  }
  static int Bit(LP_FightingStyleDef s)=>s==null?0:s.Bit;
  // 防御：外套层或中间层里有一件利器护甲 ≥ 0.4。
  public static bool DefenseArmor(Pawn p){
   if(p?.apparel==null)return false;
   foreach(var a in p.apparel.WornApparel){var layers=a.def.apparel?.layers;if(layers==null || !(layers.Contains(ApparelLayerDefOf.Shell) || layers.Contains(ApparelLayerDefOf.Middle)))continue;if(a.GetStatValue(StatDefOf.ArmorRating_Sharp)>=DefenseSharp)return true;}
   return false;
  }
  // 决斗：主手单手近战武器（质量 < 2.5 kg），没有在用的副手。
  public static bool DuelingWeapon(Pawn p){var w=p?.equipment?.Primary;return w!=null && w.def.IsMeleeWeapon && w.GetStatValue(StatDefOf.Mass)<Feats.TwoHandedMass && Marilith.Offhands(p).Count==0;}

  // ---------- 选择 ----------
  public static string CanPick(Hediff_Adventurer h,int slot,LP_FightingStyleDef s){
   if(h==null || s==null)return "没有这个风格";
   if(slot<0 || slot>=Slots(h.pawn))return "这个槽位还没有解锁";
   if(slot>h.styles.Count)return "先选前面的槽位";
   for(int i=0;i<h.styles.Count;i++)if(h.styles[i]==s)return i==slot?"已经是这个风格":"另一个槽位已经是这个风格";
   if(slot<h.styles.Count && h.styleAdaptSlot>=0 && Adapting(h,h.styleAdaptSlot))return "正在适应新风格，不能再改选";
   return null;
  }
  // 第一次选（空槽）立刻生效；改选后 3 天才生效，期间这一槽无效。
  public static bool Pick(Hediff_Adventurer h,int slot,LP_FightingStyleDef s){
   if(CanPick(h,slot,s)!=null)return false;
   if(slot==h.styles.Count)h.styles.Add(s);
   else{var old=h.styles[slot];h.styles[slot]=s;old?.Worker.Lost(h.pawn,h);h.styleAdaptSlot=slot;h.styleReadyTick=Find.TickManager.TicksGame+ReadyDelay;}
   s.Worker.Gained(h.pawn,h);Refresh(h);return true;
  }
  // 敌人：冒险者等级 ≥ 3，50% 按兵种类别得 1 个风格（每人只掷一次）。
  public static float? TestRoll;
  public static bool EnemyRoll(Hediff_Adventurer h){
   if(h?.pawn==null || h.styleRolled || h.level<EnemyMinLevel || !Npc(h.pawn) || h.styles.Count>0)return false;
   h.styleRolled=true;
   if((TestRoll??Rand.Value)>=EnemyChance)return false;
   var roles=Feats.EnemyRoles(h.pawn);
   var pick=All.Where(s=>s.enemyTags!=null && s.enemyTags.Any(roles.Contains)).ToList();
   if(pick.Count==0)return false;
   h.styles.Add(pick.RandomElement());Refresh(h);return true;
  }

  // ---------- 效果 ----------
  static readonly Dictionary<ThingDef,bool> thrownDefs=new Dictionary<ThingDef,bool>();
  // 投掷物：手雷类 weaponTags（GrenadeDestructive / GrenadeEMP / GrenadeSmoke / GrenadeFlame…）或“手雷”分类（本模组炼金投掷物）。
  public static bool IsThrown(ThingDef d){
   if(d==null)return false;
   if(!thrownDefs.TryGetValue(d,out bool r)){r=d.weaponTags!=null && d.weaponTags.Any(t=>t.StartsWith("Grenade")) || d.thingCategories!=null && d.thingCategories.Any(c=>c.defName=="Grenades");thrownDefs[d]=r;}
   return r;
  }
  static bool WeaponShot(Verb verb)=>verb!=null && !verb.IsMeleeAttack && verb.EquipmentSource!=null && !(verb is Verb_CastAbility);
  // 射程：箭术 +2 格；投掷 ×1.3（投掷物不吃箭术）。调用前已确认 Users 里有这个人。
  public static float Range(Verb verb,Pawn p,float range){
   if(!WeaponShot(verb))return range;
   if(IsThrown(verb.EquipmentSource.def))return Has(p,Thrown)?range*ThrownRange:range;
   return Has(p,Archery)?range+ArcheryRange:range;
  }
  // 命中：箭术 ×1.08、投掷 ×1.1。
  public static float Aim(Verb verb,Pawn p){
   if(!WeaponShot(verb))return 1f;
   if(IsThrown(verb.EquipmentSource.def))return Has(p,Thrown)?ThrownAim:1f;
   return Has(p,Archery)?ArcheryAim:1f;
  }
  // 守护：目标相邻（8 格）有守护风格的友军（不是目标自己）时，攻击者命中 ×0.85。调用前已确认 protectors 不为空。
  public static float Protect(Pawn target,Pawn attacker){
   if(target?.Map==null || target.Faction==null)return 1f;
   foreach(var g in protectors){
    if(g==target || g.Map!=target.Map || g.Faction!=target.Faction || g.Downed || g.Dead || !g.Position.AdjacentTo8WayOrInside(target.Position))continue;
    if(attacker!=null && attacker.Faction==g.Faction)continue;
    return ProtectFactor;
   }
   return 1f;
  }
  // 拦截：3 格内的友军受到外来伤害时，减 3 + 熟练加值；每个拦截者每 10 秒一次。调用前已确认 interceptors 不为空。
  public static float InterceptAmount(Pawn g){var h=Adventurer.Get(g);return InterceptBase+(h==null?2:h.Proficiency);}
  public static Pawn Interceptor(Pawn victim,Pawn attacker){
   if(victim?.Map==null || victim.Faction==null)return null;int now=Find.TickManager.TicksGame;
   foreach(var g in interceptors){
    if(g==victim || g==attacker || g.Map!=victim.Map || g.Faction!=victim.Faction || g.Downed || g.Dead || g.Position.DistanceToSquared(victim.Position)>InterceptRadius*InterceptRadius)continue;
    if(lastIntercept.TryGetValue(g.thingIDNumber,out int t) && now-t<InterceptCooldown)continue;
    return g;
   }
   return null;
  }
  public static bool Intercept(Pawn victim,ref DamageInfo dinfo){
   if(dinfo.Amount<=0f || dinfo.Def==null || !dinfo.Def.ExternalViolenceFor(victim))return false;
   var a=dinfo.Instigator as Pawn;if(a!=null && a.Faction!=null && a.Faction==victim.Faction)return false;
   var g=Interceptor(victim,a);if(g==null)return false;
   lastIntercept[g.thingIDNumber]=Find.TickManager.TicksGame;
   dinfo.SetAmount(Mathf.Max(0f,dinfo.Amount-InterceptAmount(g)));
   Items.Mote(g,"拦截",new Color(.95f,.85f,.45f));
   return true;
  }
  // 盲斗：3 格内看得见隐身的单位。调用前已确认 blindFighters 不为空。
  public static bool SeenByBlindFighter(Pawn p){
   if(p?.Map==null)return false;
   foreach(var g in blindFighters)if(g!=p && g.Map==p.Map && !g.Downed && g.Position.DistanceToSquared(p.Position)<=BlindRadius*BlindRadius)return true;
   return false;
  }
  // 徒手：拳脚命中 20% 抓住目标 2 秒。
  public static bool Grapple(Pawn caster,Pawn target){
   if(target==null || target.Dead || caster.equipment==null || caster.equipment.Primary!=null || !Has(caster,Unarmed) || !Rand.Chance(GrappleChance))return false;
   Items.Give(target,"LP_Grappled",GrappleTicks);Items.Mote(target,"擒抱",new Color(.95f,.85f,.45f));return true;
  }
  // 双武器：有这个风格的人至少 1 个副手槽，追加攻击 30%。
  public static bool TwoWeaponUser(Pawn p)=>p!=null && Users.Contains(p.thingIDNumber) && Has(p,TwoWeapon);
 }

 // ==================== Harmony ====================
 // 徒手风格的擒抱：近战命中后。快速返回：施法者不在 Styles.Users。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class StyleMeleeHit {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){
   if(Styles.Users.Count==0)return;var c=__instance.CasterPawn;
   if(c==null || !Styles.Users.Contains(c.thingIDNumber))return;
   Styles.Grapple(c,target.Thing as Pawn);
  }
 }
 // 盲斗：3 格内的隐身单位不再隐身。快速返回：结果为 false 或 blindFighters 为空。
 [HarmonyPatch(typeof(InvisibilityUtility),nameof(InvisibilityUtility.IsPsychologicallyInvisible))]
 public static class StyleBlindSight {
  static void Postfix(Pawn pawn,ref bool __result){if(__result && Styles.blindFighters.Count>0 && Styles.SeenByBlindFighter(pawn))__result=false;}
 }
}
