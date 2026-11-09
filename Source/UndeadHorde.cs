using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.140.0 亡灵军团（Defs/UndeadHorde.xml）：噬尸鬼、尸妖、怨灵、吸血鬼四种人形亡灵，跟着塞伊远征军的「亡灵军团」编组登场（死亡骑士带队时沿用 1.52.1 的死亡骑士）。
 // 兵种上挂 LP_UndeadExt(kind)，生成完立刻挂对应的「之躯」（Hediff_UndeadBody），规则都在那个 hediff 和下面的补丁里；没有亡灵时补丁第一步就返回。
 public class LP_UndeadExt : DefModExtension {public string kind;}
 public static class UndeadHorde {
  public const string Ghoul="ghoul",Wight="wight",Wraith="wraith",Vampire="vampire";
  public const float WightDrain=.3f,VampireDrain=.5f,WraithShare=.5f,WraithDrain=3f,VampireRegen=4f,SunFactor=1.5f,ParalysisChance=1f;
  public const int ParalysisDC=12,ParalysisTicks=180,WraithInterval=120,SunBurnMemory=600;
  public static readonly string[] Kinds={Ghoul,Wight,Wraith,Vampire};
  public static readonly Color Grave=new Color(.6f,.7f,.6f);
  static readonly Dictionary<Def,LP_UndeadExt> cache=new Dictionary<Def,LP_UndeadExt>();
  public static int Alive; // 当前存在的亡灵（之躯挂上时 +1，死亡 / 之躯摘掉时 −1）：补丁快速返回用
  public static LP_UndeadExt Ext(PawnKindDef k){if(k==null)return null;if(!cache.TryGetValue(k,out var e)){e=k.GetModExtension<LP_UndeadExt>();cache[k]=e;}return e;}
  public static string KindOf(Pawn p)=>(p?.health?.hediffSet?.hediffs?.OfType<Hediff_UndeadBody>().FirstOrDefault())?.Kind??Ext(p?.kindDef)?.kind;
  public static bool Is(Pawn p)=>p!=null && KindOf(p)!=null;
  public static string BodyFor(string kind)=>kind==Ghoul?"LP_UH_GhoulBody":kind==Wight?"LP_UH_WightBody":kind==Wraith?"LP_UH_WraithBody":kind==Vampire?"LP_UH_VampireBody":null;
  public static bool On=>LichMod.Settings?.undeadHorde??true;
  public static float? TestRoll;
  static float Roll=>TestRoll??Rand.Value;
  public static bool Holy(DamageDef d)=>d!=null && (d==DamageDefOf.Flame || d==DamageDefOf.Burn || d.defName=="LP_Radiant" || d.defName=="LP_DivineSmite" || d.defName=="LP_HolyWater");
  // 刀枪箭矢：武器或徒手造成的物理伤害（银器、魔法、爆炸不算）。
  public static bool Mundane(DamageInfo d){
   if(d.Def==null || d.Def.isExplosive || Holy(d.Def))return false;var n=d.Def.defName;
   if(n.StartsWith("LP_") || n.Contains("Psychic") || n.Contains("Silver"))return false;
   return d.Def==DamageDefOf.Cut || d.Def==DamageDefOf.Stab || d.Def==DamageDefOf.Blunt || d.Def==DamageDefOf.Bite || d.Def==DamageDefOf.Scratch || d.Def==DamageDefOf.Bullet || n=="Arrow" || d.Def==DamageDefOf.Crush;
  }
  public static bool InSun(Pawn p)=>p!=null && p.Spawned && p.Map!=null && !p.Position.Roofed(p.Map) && p.Map.skyManager.CurSkyGlow>=.5f;

  // 生成完：挂之躯、清掉伤病和成瘾、吸血鬼和尸妖不用改外表；死亡骑士兵种走传奇怪的 Normalize。
  public static void Prepare(Pawn p){
   if(p==null || p.health==null)return;
   if(p.kindDef?.defName=="LP_LM_DeathKnight"){if(!Items.Has(p,"LP_DeathKnightBody"))Monsters.Normalize(p);return;}
   var kind=Ext(p.kindDef)?.kind;var body=BodyFor(kind);if(body==null || Items.Has(p,body))return;
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_Addiction || h.def.isBad && !(h is Hediff_MissingPart)).ToList())p.health.RemoveHediff(h);
   if(p.story?.traits!=null)foreach(var t in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(t);
   if(p.needs?.food!=null)p.needs.food.CurLevel=1f;
   Legendary.bypass=true;try{Items.Give(p,body);}finally{Legendary.bypass=false;}
  }
  // 武器命中后：噬尸鬼麻痹、尸妖 / 吸血鬼吸血。
  public static void Hit(Pawn a,Pawn v,float dealt){
   if(Alive<=0 || a==null || v==null || v.Dead || a==v)return;var kind=KindOf(a);if(kind==null)return;
   switch(kind){
    case Ghoul:
     if(!v.RaceProps.IsFlesh || Items.Undead(v) || Is(v) || Items.Has(v,"LP_UH_Paralysis"))return;
     if(Legendary.Save(v,Adventurer.Con,ParalysisDC,a))return;
     Items.Give(v,"LP_UH_Paralysis",ParalysisTicks);Items.Mote(v,"尸毒麻痹！",Grave);break;
    case Wight:case Vampire:
     if(dealt<=0f || !v.RaceProps.IsFlesh)return;float h=ClassMelee.Heal(a,dealt*(kind==Wight?WightDrain:VampireDrain));if(h>0f)Items.Mote(a,"吸取生命 +"+h.ToString("0"),Grave);break;
   }
  }
  // 受伤：怨灵半实体；吸血鬼白天露天 ×1.5、挨过圣火记 10 秒。
  public static float Incoming(Pawn v,DamageInfo d,Hediff_UndeadBody body){
   float f=1f;
   switch(body.Kind){
    case Wraith:if(Mundane(d))f*=WraithShare;break;
    case Vampire:if(Holy(d.Def))body.lastHoly=Find.TickManager.TicksGame;if(InSun(v))f*=SunFactor;break;
   }
   return f;
  }
 }

 public class Hediff_UndeadBody : HediffWithComps {
  int ticks,drain;public int lastHoly=-99999;
  public string Kind=>UndeadHorde.Ext(pawn?.kindDef)?.kind??(def.defName=="LP_UH_GhoulBody"?UndeadHorde.Ghoul:def.defName=="LP_UH_WightBody"?UndeadHorde.Wight:def.defName=="LP_UH_WraithBody"?UndeadHorde.Wraith:UndeadHorde.Vampire);
  public override void PostAdd(DamageInfo? dinfo){base.PostAdd(dinfo);UndeadHorde.Alive++;}
  public override void PostRemoved(){base.PostRemoved();UndeadHorde.Alive=Math.Max(0,UndeadHorde.Alive-1);}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref lastHoly,"lpLastHoly",-99999);if(Scribe.mode==LoadSaveMode.PostLoadInit)UndeadHorde.Alive++;}
  public override void TickInterval(int delta){
   base.TickInterval(delta);var p=pawn;if(p==null || p.Dead)return;
   ticks+=delta;drain+=delta;int now=Find.TickManager.TicksGame;
   if(Kind==UndeadHorde.Vampire && ticks>=250){ticks=0;if(now-lastHoly>UndeadHorde.SunBurnMemory && !UndeadHorde.InSun(p)){float h=ClassMelee.Heal(p,UndeadHorde.VampireRegen);if(h>0f && p.Spawned)Items.Mote(p,"愈合",UndeadHorde.Grave);}}
   if(Kind==UndeadHorde.Wraith && drain>=UndeadHorde.WraithInterval){drain=0;Drain();}
  }
  // 怨灵：2 格内敌对的活人各抽 3 点死灵伤害，抽到的总量治疗自己。
  public int Drain(){
   var p=pawn;if(p?.Map==null || !p.Spawned || p.Downed)return 0;int n=0;float got=0f;var necro=DefDatabase<DamageDef>.GetNamedSilentFail("LP_Necrotic")??DamageDefOf.Blunt;
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,2f).ToList()){
    if(t==p || t.Dead || !t.RaceProps.IsFlesh || Items.Undead(t) || UndeadHorde.Is(t) || !t.HostileTo(p))continue;
    float before=ClassMelee.InjurySum(t);t.TakeDamage(new DamageInfo(necro,UndeadHorde.WraithDrain,1f,-1f,p));got+=Mathf.Max(0f,ClassMelee.InjurySum(t)-before);n++;
   }
   if(got>0f){ClassMelee.Heal(p,got);Items.Mote(p,"抽取生命",UndeadHorde.Grave);}
   return n;
  }
  public override string TipStringExtra=>Kind==UndeadHorde.Vampire && UndeadHorde.InSun(pawn)?"白天露天：受到的伤害 ×1.5，不再愈合":base.TipStringExtra;
 }

 // ==================== 挂钩 ====================
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class UndeadGenerate {
  static void Postfix(Pawn __result,PawnGenerationRequest request){if(__result?.kindDef!=null && (UndeadHorde.Ext(__result.kindDef)!=null || __result.kindDef.defName=="LP_LM_DeathKnight"))UndeadHorde.Prepare(__result);}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class UndeadDamage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(UndeadHorde.Alive<=0 || dinfo.Def==null || dinfo.Amount<=0f || !dinfo.Def.harmsHealth)return;
   var body=__instance.health?.hediffSet?.hediffs?.OfType<Hediff_UndeadBody>().FirstOrDefault();if(body==null)return;
   float f=UndeadHorde.Incoming(__instance,dinfo,body);if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
  }
 }
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class UndeadMeleeHit {
  static void Prefix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target,out float __state){var t=target.Thing as Pawn;__state=UndeadHorde.Alive>0 && t!=null?ClassMelee.InjurySum(t):-1f;}
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target,float __state){
   if(UndeadHorde.Alive<=0 || __state<0f)return;var a=__instance.CasterPawn;var t=target.Thing as Pawn;if(a==null || t==null)return;
   float dealt=t.Dead?__state:Mathf.Max(0f,ClassMelee.InjurySum(t)-__state);
   UndeadHorde.Hit(a,t,dealt);
  }
 }
 // 袭击编组：设置关掉亡灵军团时，塞伊的这一组不出现（把亡灵换成普通远征军）。
 [HarmonyPatch(typeof(FactionGroups),nameof(FactionGroups.Process))]
 public static class UndeadHordeToggle {
  [HarmonyPriority(Priority.High)]
  static void Postfix(PawnGroupMakerParms parms,ref IEnumerable<Pawn> __result){
   if(UndeadHorde.On || __result==null || parms?.faction?.def!=Factions.HostileDef)return;
   var list=__result as List<Pawn>??__result.ToList();var basic=parms.faction.def.basicMemberKind;
   for(int i=0;i<list.Count;i++){var x=list[i];if(UndeadHorde.Ext(x.kindDef)==null)continue;
    var r=basic==null?null:PawnGenerator.GeneratePawn(new PawnGenerationRequest(basic,parms.faction,PawnGenerationContext.NonPlayer,parms.tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true));
    if(Find.WorldPawns.Contains(x))Find.WorldPawns.RemovePawn(x);x.Destroy();if(r!=null)list[i]=r;else{list.RemoveAt(i);i--;}
   }
   __result=list;
  }
 }

 public partial class Settings {
  public bool undeadHorde=true;
  void ExposeUndeadHorde(){Scribe_Values.Look(ref undeadHorde,"lpUndeadHorde",true);}
 }
}
