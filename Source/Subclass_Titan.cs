using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.115.0 战士第三个子职业「泰坦战士」（Defs/Subclass_Titan.xml，战士 3 / 7 / 11 / 15 级）。
 // 不改共享的分派表：Tick250、能力检查、能力效果都用 Harmony 挂在 ClassSystem.Tick250 / ClassEffects.Invalid / ClassEffects.Apply 上（见文件末尾）。
 // 体型、部位生命、绘制倍率按角色缓存（Users），只在战士等级、冒险者等级、能力位或泰坦化状态变化时重算；所有补丁第一步都是 Users 空集合判断。
 // 体型只乘在 Pawn.BodySize 上：原版 HealthScale 不看 BodySize，巨躯规则（BodySize.Size）也不含这一项，所以部位生命只吃这里的 +3%/级，不会重复叠加。
 public static class TitanWarrior {
  public const string Sub="LP_Sub_Titan";
  public static readonly Color TitanColor=new Color(.78f,.62f,.42f);
  public const float SizePer=.025f,SizeMax=2f,PartPer=.03f,CarryPer=.02f,BigHit=1.15f,KnockChance=.25f,StompRadius=2.9f,StunBase=.6f,StunPerSize=.3f,StunMax=.95f,
   TitanSize=1.25f,TitanPart=1.25f,Fatigue=.2f,AncientChance=.2f,MiniRadius=1.9f,MiniDamage=.5f,PainShock=.1f,StunCut=.5f;
  public const int StompStun=120,TitanTicks=3600,AncientTicks=7200;

  public const int FBody=1,FGrip=2,FStomp=4,FUnmoving=8,FTitanize=16,FAncient=32;
  static readonly Dictionary<string,int> FlagOf=new Dictionary<string,int>{{"LP_TT_TitanBody",FBody},{"LP_TT_MightyGrip",FGrip},{"LP_TT_Stomp",FStomp},{"LP_TT_Immovable",FUnmoving},{"LP_TT_Titanize",FTitanize},{"LP_TT_AncientTitan",FAncient}};
  public sealed class Entry {public int flags,level,adv;public bool titan;public float size=1f,render=1f,part=1f,carry=1f;}
  public static readonly Dictionary<int,Entry> Users=new Dictionary<int,Entry>();
  public static Entry Of(Pawn p){if(p==null || Users.Count==0)return null;Users.TryGetValue(p.thingIDNumber,out var e);return e;}
  public static bool S(Pawn p,int f){var e=Of(p);return e!=null && (e.flags & f)!=0;}
  public static bool Immovable(Pawn p)=>Users.Count>0 && S(p,FUnmoving);
  public static bool Titanized(Pawn p)=>Items.Has(p,"LP_TT_Titanized");
  static float Roll=>ClassMartial2.RollOverride??Rand.Value;
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.HostileTo(a);
  static readonly DefRef<LP_ClassDef> fighter=new DefRef<LP_ClassDef>("LP_Class_Fighter");
  public static int Level(Pawn p)=>ClassSystem.Level(p,fighter.Def);

  // 体型倍率：1 + 2.5% × 等级；战士 20 级后冒险者传奇等级（21～40）接着算，上限 ×2.0。泰坦化再 ×1.25。
  public static int SizeLevel(int fighterLevel,int advLevel)=>fighterLevel+(fighterLevel>=Adventurer.MaxLevel?Mathf.Max(0,advLevel-Adventurer.MaxLevel):0);
  public static float SizeFactor(int fighterLevel,int advLevel)=>Mathf.Min(SizeMax,1f+SizePer*SizeLevel(fighterLevel,advLevel));
  // 绘制倍率 = √体型（同双头巨人体型 2.0 画 ×1.4 的做法），头、衣服、身体附件随根节点一起缩放，武器另由下面的补丁缩放。
  public static float RenderFactor(float size)=>Mathf.Sqrt(size);

  public static int Refresh(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null)return 0;int f=0;
   if(!p.Dead && ClassSubclass.Has(h,Sub)){var list=p.abilities?.abilities;if(list!=null)for(int k=0;k<list.Count;k++)if(FlagOf.TryGetValue(list[k].def.defName,out int b))f|=b;}
   int lv=f==0?0:Level(p),adv=h.level;bool titan=f!=0 && Titanized(p);
   Users.TryGetValue(p.thingIDNumber,out var old);
   if(old!=null && old.flags==f && old.level==lv && old.adv==adv && old.titan==titan)return f; // 缓存没变：不重算
   if(f==0 && old==null)return 0;
   var e=new Entry{flags=f,level=lv,adv=adv,titan=titan};
   if((f & FBody)!=0){e.size=SizeFactor(lv,adv);e.part=1f+PartPer*Mathf.Min(lv,Adventurer.MaxLevel);}
   if(titan){e.size*=TitanSize;e.part*=TitanPart;}
   e.render=RenderFactor(e.size);
   if((f & FGrip)!=0)e.carry=1f+CarryPer*Mathf.Min(lv,Adventurer.MaxLevel);
   Apply(p,old,f==0?null:e);
   return f;
  }
  // 换缓存；部位生命上限变小（泰坦化结束）时按“新最大 / 旧最大”缩放伤口，免得上限缩水直接打烂部位或致死。
  static void Apply(Pawn p,Entry old,Entry e){
   var set=p.health?.hediffSet;float oldPart=old?.part??1f,newPart=e?.part??1f;
   List<BodyPartRecord> parts=null;float[] before=null;
   if(set!=null && newPart<oldPart-.0001f){parts=set.hediffs.OfType<Hediff_Injury>().Select(x=>x.Part).Where(x=>x!=null && !set.PartIsMissing(x)).Distinct().ToList();before=parts.Select(x=>PartHealth.Maximum(x,p)).ToArray();}
   if(e==null)Users.Remove(p.thingIDNumber);else Users[p.thingIDNumber]=e;
   if(parts!=null)for(int i=0;i<parts.Count;i++){float after=PartHealth.Maximum(parts[i],p);if(before[i]<=0f || after>=before[i])continue;float r=after/before[i];foreach(var inj in set.hediffs.OfType<Hediff_Injury>().Where(x=>x.Part==parts[i]).ToList())inj.Severity*=r;}
   bool sized=Mathf.Abs((old?.size??1f)-(e?.size??1f))>.0001f;
   if(sized){BodySize.Notify_SizeChanged(p);if(p.Spawned)p.Drawer?.renderer?.SetAllGraphicsDirty();}
   if(set!=null && Mathf.Abs(oldPart-newPart)>.0001f){set.DirtyCache();p.health.capacities?.Notify_CapacityLevelsDirty();}
   if(e!=null && (e.flags & (FGrip|FUnmoving|FBody))!=0){var bh=Items.Has(p,"LP_TT_BodyOn")?set?.GetFirstHediffOfDef(Items.Hediff("LP_TT_BodyOn")):Items.Give(p,"LP_TT_BodyOn");(bh as Hediff_TitanBody)?.Set(e);}
   else Items.Remove(p,"LP_TT_BodyOn");
  }
  public static void Refresh(Pawn p){var h=Adventurer.Get(p);if(h!=null)Refresh(h);}
  // ClassSystem.Tick250 的后缀 → 这里。
  public static void Tick250(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null || Users.Count==0 && !ClassSubclass.Has(h,Sub))return;
   if(p.Dead){if(Users.ContainsKey(p.thingIDNumber))Apply(p,Users[p.thingIDNumber],null);return;}
   Refresh(h);
  }

  // ---------- 撼地践踏 ----------
  public static float StunChance(float mine,float theirs)=>Mathf.Min(StunMax,StunBase+StunPerSize*Mathf.Max(0f,mine-theirs));
  public static float StompDamage(Pawn p){var h=Adventurer.Get(p);return Mathf.Max(1f,10f+3f*(h?.AMod(Adventurer.Str)??0f));}
  public static bool Busy;
  // 半径内（不含倒地的）敌人受钝伤；比自己小的按成功率眩晕 2 秒。友军、中立不受影响。返回打中的数量。
  public static int Stomp(Pawn p,float radius,float factor,string label){
   if(p==null || !p.Spawned)return 0;float dmg=StompDamage(p)*factor,mine=p.BodySize;int n=0;
   var foes=Races124.PawnsNear(p.Map,p.Position,radius).Where(t=>Hostile(p,t) && !t.Downed && t.Spawned).ToList();
   Busy=true;
   try{
    foreach(var t in foes){
     t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,dmg,0f,-1f,p));n++;
     if(!t.Dead && t.BodySize<mine && Roll<StunChance(mine,t.BodySize)){t.stances?.stunner?.StunFor(StompStun,p);Items.Mote(t,"震倒",TitanColor);}
    }
   }finally{Busy=false;}
   FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,radius*1.2f,new Color(.6f,.5f,.4f));Items.Mote(p,label,TitanColor);
   return n;
  }
  // 巨力之握：近战打中比自己小的敌人，伤害 ×1.15，25% 成功率把它击退 1 格。远古泰坦：泰坦化中每次近战命中 20% 成功率小型践踏。
  public static float HitFactor(Pawn v,DamageInfo d){
   if(Busy)return 1f;var a=d.Instigator as Pawn;if(a==null || a==v || !a.Spawned || !a.HostileTo(v) || !ClassMartial2.Melee(d))return 1f;
   var e=Of(a);if(e==null)return 1f;float g=1f;
   // 先践踏、后击退（都延后到这次伤害结算完），免得被打的目标先被推出小型践踏的范围。
   if((e.flags & FAncient)!=0 && e.titan && Roll<AncientChance){var me=a;Fiends.Defer(()=>{if(!me.Dead && me.Spawned)Stomp(me,MiniRadius,MiniDamage,"远古践踏");});}
   if((e.flags & FGrip)!=0 && v.BodySize<a.BodySize){
    g*=BigHit;
    if(Roll<KnockChance){var tv=v;var me=a;Fiends.Defer(()=>{if(tv.Dead || !tv.Spawned || !me.Spawned || tv.Map!=me.Map)return;DNDPush.Move(tv,DNDPush.Destination(tv.Map,tv.Position,(tv.Position-me.Position).ToVector3(),1));});}
   }
   return g;
  }
  public static void TitanizeEnd(Pawn p){
   if(p==null)return;bool ancient=ClassSystem.Knows(p,"LP_TT_AncientTitan");Refresh(p);
   if(!ancient && !p.Dead && p.needs?.rest!=null){p.needs.rest.CurLevel=Mathf.Max(0f,p.needs.rest.CurLevel-Fatigue);Items.Mote(p,"泰坦化结束：疲惫",TitanColor);}
  }

  // ---------- 主动能力 ----------
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   switch(e.effect){
    case "TTStomp":return caster.Spawned?null:"要在地图上才能践踏。";
    case "TTTitanize":return Titanized(caster)?"已经在泰坦化了。":null;
   }
   return null;
  }
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   switch(e.effect){
    case "TTStomp":Stomp(p,StompRadius,1f,"撼地践踏");return true;
    case "TTTitanize":Items.Give(p,"LP_TT_Titanized",ClassSystem.Knows(p,"LP_TT_AncientTitan")?AncientTicks:TitanTicks);Refresh(p);Items.Mote(p,"泰坦化",TitanColor);return true;
   }
   return false;
  }
  public static void Reset(){Users.Clear();Busy=false;WeaponScale=1f;}

  // ---------- 武器绘制 ----------
  [ThreadStatic] public static float WeaponScale;
  [ThreadStatic] public static Vector3 WeaponOrigin;
  // 1.115.0 热修：武器贴图放大，握持位置（相对身体中心的偏移）也按同一倍率外移。
  public static Matrix4x4 WeaponTRS(Vector3 pos,Quaternion q,Vector3 s){float k=WeaponScale;if(k>1.0001f || k<.9999f && k>0f){s.x*=k;s.z*=k;var o=WeaponOrigin;pos.x=o.x+(pos.x-o.x)*k;pos.z=o.z+(pos.z-o.z)*k;}return Matrix4x4.TRS(pos,q,s);}
 }

 // 泰坦之躯的被动属性：搬运容量（巨力之握）、疼痛休克阈值（不动如山），按缓存现算。
 public class Hediff_TitanBody : HediffWithComps {
  float size=1f,carry=1f;bool unmoving;HediffStage stage;
  public void Set(TitanWarrior.Entry e){
   bool um=(e.flags & TitanWarrior.FUnmoving)!=0;
   if(stage!=null && Mathf.Approximately(size,e.size) && Mathf.Approximately(carry,e.carry) && um==unmoving)return;
   size=e.size;carry=e.carry;unmoving=um;stage=null;pawn?.health?.Notify_HediffChanged(this);
  }
  public override string LabelInBrackets=>"体型 ×"+size.ToString("0.00");
  public override HediffStage CurStage=>stage??(stage=Build());
  HediffStage Build(){
   var s=new HediffStage{statOffsets=new List<StatModifier>(),statFactors=new List<StatModifier>()};
   if(carry>1f)s.statFactors.Add(new StatModifier{stat=StatDefOf.CarryingCapacity,value=carry});
   if(unmoving)s.statOffsets.Add(new StatModifier{stat=StatDefOf.PainShockThreshold,value=TitanWarrior.PainShock});
   return s;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref size,"lpSize",1f);Scribe_Values.Look(ref carry,"lpCarry",1f);Scribe_Values.Look(ref unmoving,"lpUnmoving");}
 }
 // 泰坦化：体型、部位生命由缓存乘上；结束时刷新缓存并扣休息值（远古泰坦不扣）。
 public class Hediff_Titanized : HediffWithComps {
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);if(pawn!=null)TitanWarrior.Refresh(pawn);}
  public override void PostRemoved(){base.PostRemoved();TitanWarrior.TitanizeEnd(pawn);}
 }
 public class GameComponent_TitanWarrior : GameComponent {
  public GameComponent_TitanWarrior(Game game){}
  public override void FinalizeInit(){
   base.FinalizeInit();TitanWarrior.Reset();
   foreach(var m in Find.Maps)foreach(var p in m.mapPawns.AllPawns){var h=Adventurer.Get(p);if(h!=null && ClassSubclass.Has(h,TitanWarrior.Sub))TitanWarrior.Refresh(h);}
  }
 }

 // ==================== Harmony ====================
 // 分派：Tick250 / 能力检查 / 能力效果。不是泰坦战士的效果名第一步就返回。
 [HarmonyPatch(typeof(ClassSystem),nameof(ClassSystem.Tick250))]
 public static class TitanWarriorTick {static void Postfix(Hediff_Adventurer h)=>TitanWarrior.Tick250(h);}
 [HarmonyPatch(typeof(ClassEffects),nameof(ClassEffects.Invalid))]
 public static class TitanWarriorInvalid {static void Postfix(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target,ref string __result){if(__result==null && e?.effect!=null && e.effect.StartsWith("TT"))__result=TitanWarrior.Invalid(caster,e,target);}}
 [HarmonyPatch(typeof(ClassEffects),nameof(ClassEffects.Apply))]
 public static class TitanWarriorApply {static bool Prefix(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target)=>p==null || e?.effect==null || !e.effect.StartsWith("TT") || !TitanWarrior.Apply(p,def,e,target);}
 // 体型（近战被击中概率、食量、搬运等原版效果都看它）。
 [HarmonyPatch(typeof(Pawn),"get_BodySize")]
 public static class TitanWarriorBodySize {static void Postfix(Pawn __instance,ref float __result){if(TitanWarrior.Users.Count==0)return;var e=TitanWarrior.Of(__instance);if(e!=null)__result*=e.size;}}
 // 部位生命：在职业等级成长之外再乘 1 + 3% × 战士等级（泰坦化再 ×1.25），只取整一次。
 [HarmonyPatch(typeof(PartHealth),nameof(PartHealth.WithoutReinforcement))]
 public static class TitanWarriorPartHealth {static void Postfix(Pawn pawn,ref float __result){if(TitanWarrior.Users.Count==0)return;var e=TitanWarrior.Of(pawn);if(e!=null && e.part!=1f)__result=Mathf.Ceil(__result*e.part);}}
 // 绘制：根节点 ×√体型（头、衣服、身体附件一起）。
 [HarmonyPatch(typeof(PawnRenderNodeWorker),nameof(PawnRenderNodeWorker.ScaleFor))]
 public static class TitanWarriorRenderSize {static void Postfix(PawnRenderNode node,PawnDrawParms parms,ref Vector3 __result){if(TitanWarrior.Users.Count==0 || !RenderTags.IsRoot(node?.Props?.tagDef))return;var e=TitanWarrior.Of(parms.pawn);if(e!=null)__result*=e.render;}}
 // 武器：画装备时记下持有者的绘制倍率，画武器的矩阵按它放大。
 [HarmonyPatch(typeof(PawnRenderUtility),nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
 public static class TitanWarriorWeaponOwner {
  static void Prefix(Pawn __0,Vector3 __1){if(TitanWarrior.Users.Count==0){TitanWarrior.WeaponScale=1f;return;}TitanWarrior.WeaponScale=TitanWarrior.Of(__0)?.render??1f;TitanWarrior.WeaponOrigin=__1;}
  static void Finalizer(){TitanWarrior.WeaponScale=1f;}
 }
 [HarmonyPatch]
 public static class TitanWarriorWeaponMatrix {
  static IEnumerable<MethodBase> TargetMethods(){
   foreach(var n in new[]{nameof(PawnRenderUtility.DrawEquipmentAiming),"DrawCarriedWeapon"}){var m=AccessTools.Method(typeof(PawnRenderUtility),n);if(m!=null)yield return m;}
  }
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions){
   var trs=AccessTools.Method(typeof(Matrix4x4),nameof(Matrix4x4.TRS));var mine=AccessTools.Method(typeof(TitanWarrior),nameof(TitanWarrior.WeaponTRS));
   foreach(var c in instructions){if(c.Calls(trs)){c.opcode=OpCodes.Call;c.operand=mine;}yield return c;}
  }
 }
 // 巨力之握 / 远古泰坦：近战命中。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class TitanWarriorDamage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(TitanWarrior.Users.Count==0 || TitanWarrior.Busy || dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || __instance.Dead)return;
   float f=TitanWarrior.HitFactor(__instance,dinfo);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
 // 不动如山：被眩晕的时长 ×0.5（免疫击退在 DNDPush.Move 里）。
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.StunFor))]
 public static class TitanWarriorStun {static void Prefix(StunHandler __instance,ref int __0){if(TitanWarrior.Users.Count==0 || __0<=1)return;if(__instance.parent is Pawn p && TitanWarrior.Immovable(p))__0=Mathf.Max(1,Mathf.RoundToInt(__0*TitanWarrior.StunCut));}}
}
