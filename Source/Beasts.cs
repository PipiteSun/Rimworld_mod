using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.25.0 可驯服的 DND 野兽：猫头鹰熊（熊抱）、邪座狼（群嚎）、冬狼（冰咬、怕火）、地狱犬（火咬、免火）、魔化兽（位移幻影）、闪光犬（闪现）。
 // 每种野兽的 ThingDef 上挂一个 CompBeast（kind 区分），冷却都记在它上面、随角色存档。
 public static class Beasts {
  public const float HugChance=.25f,PackBonusPer=.05f,PackBonusMax=.2f,PackRadius=8f,WinterFireFactor=1.5f,BlinkChance=.3f,BlinkMin=3f,BlinkMax=6f,TameFactor=2f;
  public const int HugStunTicks=180,HugCooldown=600,PackCacheTicks=250,FrostSlowTicks=240,PhantomCooldown=360,BlinkCooldown=600;
  public static readonly string[] KindDefs={"LP_Owlbear","LP_Worg","LP_WinterWolf","LP_HellHound","LP_DisplacerBeast","LP_BlinkDog"};
  // 测试用：设了就代替随机数（熊抱、闪现的概率判定）。
  public static float? TestRoll;
  public static float Roll=>TestRoll??Rand.Value;
  public static bool TamePatched;
  public static CompBeast Comp(Pawn p)=>p?.def?.GetCompProperties<CompProperties_Beast>()==null?null:p.GetComp<CompBeast>();
  public static string Kind(Pawn p)=>Comp(p)?.Props.kind;
  public static bool IsBeastKind(PawnKindDef k)=>k?.race?.GetCompProperties<CompProperties_Beast>()!=null;
  public static bool IsFire(DamageDef def)=>def!=null && (def==DamageDefOf.Flame || def==DamageDefOf.Burn);
  public static bool IsBite(Tool tool)=>tool?.capacities!=null && tool.capacities.Any(c=>c?.defName=="Bite");

  // ---------- 种族驯服加成 ----------
  // 驯服者种族按 1.20.0 的基因规则判断（异种型或标志基因）。
  public static bool TameBonus(Pawn tamer,string kind){
   if(tamer==null)return false;
   switch(kind){
    case "Worg":return CommonRaces.IsHalfOrc(tamer);
    case "HellHound":return FireGiant.Active(tamer);
    case "BlinkDog":case "Owlbear":return Items.IsElf(tamer);
   }
   return false;
  }
  // 1.30.0：霜巨人的冬狼之主另加一档（冬狼×3、北极狼/森林狼×1.5），与种族加成取较大者，上限100%。
  public static float TameChance(float chance,Pawn tamer,Pawn animal){
   if(chance>=1f)return chance;
   var kind=Kind(animal);
   float f=kind!=null && TameBonus(tamer,kind)?TameFactor:1f;
   f=Mathf.Max(f,FrostGiant.TameFactor(tamer,animal));
   f=Mathf.Max(f,Subraces.TameFactor(tamer,animal)); // 1.57.0 森林侏儒与小动物交谈
   return f>1f?Mathf.Min(1f,chance*f):chance;
  }

  // ---------- 设置：野外刷新与狂暴兽群 ----------
  static readonly Dictionary<PawnKindDef,bool> manhunterDefault=new Dictionary<PawnKindDef,bool>();
  public static IEnumerable<PawnKindDef> AllKinds(){foreach(var n in KindDefs){var k=DefDatabase<PawnKindDef>.GetNamedSilentFail(n);if(k!=null)yield return k;}}
  // 野外刷新的权重在 BiomeDef.CommonalityOfAnimal 的后缀里按设置归零；狂暴兽群另有一条“全库兜底”只看 canArriveManhunter，所以这里一起改。
  public static void ApplySettings(){
   bool on=LichMod.Settings?.dndBeastsWild!=false;
   foreach(var k in AllKinds()){
    if(!manhunterDefault.TryGetValue(k,out bool d))manhunterDefault[k]=d=k.canArriveManhunter;
    k.canArriveManhunter=d && on;
   }
   WildMonsters.ApplySettings();
  }
  public static bool DefaultManhunter(PawnKindDef k){ApplySettings();return manhunterDefault.TryGetValue(k,out bool d) && d;}
 }

 public class CompProperties_Beast : CompProperties {
  public string kind;
  public CompProperties_Beast(){compClass=typeof(CompBeast);}
 }
 public class CompBeast : ThingComp {
  public int hugReadyTick=-1,phantomReadyTick=-1,blinkReadyTick=-1;
  int packTick=-99999,packCount,tickAcc;
  // 同一次攻击的附加伤害（例如火咬）也算落空：只记在内存里。
  int phantomBrokeTick=-1;Thing phantomBrokeBy;
  IntVec3 glowCell=IntVec3.Invalid;
  public CompProperties_Beast Props=>(CompProperties_Beast)props;
  Pawn Pawn=>parent as Pawn;
  static int Now=>Find.TickManager.TicksGame;
  public bool HugReady=>Now>=hugReadyTick;
  public bool BlinkReady=>Now>=blinkReadyTick;
  public bool PhantomActive{get{var p=Pawn;return Props.kind=="DisplacerBeast" && p!=null && !p.Dead && !p.Downed && Now>=phantomReadyTick;}}

  // 熊抱：近战命中后 25% 抱住目标，昏迷3秒；每只猫头鹰熊冷却10秒。
  public bool TryHug(Pawn target,float roll){
   var p=Pawn;
   if(Props.kind!="Owlbear" || p==null || p.Dead || target==null || target.Dead || target.stances?.stunner==null || !HugReady || roll>=Beasts.HugChance)return false;
   target.stances.stunner.StunFor(Beasts.HugStunTicks,p,false,true,false);
   hugReadyTick=Now+Beasts.HugCooldown;
   if(p.Spawned)MoteMaker.ThrowText(target.DrawPos,p.Map,"熊抱！",new Color(.95f,.8f,.5f));
   return true;
  }
  public void Notify_MeleeHit(Pawn target,Tool tool){
   if(target==null || target.Dead)return;
   switch(Props.kind){
    case "Owlbear":TryHug(target,Beasts.Roll);break;
    // 冻伤本身是咬击的附加伤害（XML 里的 extraMeleeDamages），这里只加寒霜迟缓。
    // 1.31.0：冬狼寒潮的“寒潮狂性”让寒霜迟缓持续时间翻倍。
    case "WinterWolf":if(Beasts.IsBite(tool))Items.Give(target,"LP_FrostSlow",Beasts.FrostSlowTicks*(Items.Has(Pawn,ColdSnap.Frenzy)?2:1));break;
   }
  }

  // 群嚎：8格内同阵营的其他邪座狼，每只+5%近战命中，最多+20%。数量每250刻数一次。
  public int PackAllies(){
   if(Props.kind!="Worg")return 0;
   int now=Now;if(packTick>=0 && now-packTick<Beasts.PackCacheTicks)return packCount;
   packTick=now;packCount=CountAllies();return packCount;
  }
  public void ForgetPack()=>packTick=-99999;
  int CountAllies(){
   var p=Pawn;if(p?.Map==null || !p.Spawned)return 0;
   int n=0;var list=p.Map.mapPawns.AllPawnsSpawned;
   for(int i=0;i<list.Count;i++){var o=list[i];if(o==p || o.def!=p.def || o.Dead || o.Downed || o.Faction!=p.Faction)continue;if(o.Position.InHorDistOf(p.Position,Beasts.PackRadius))n++;}
   return n;
  }
  public float PackBonus=>Mathf.Min(Beasts.PackBonusMax,Beasts.PackBonusPer*PackAllies());

  public override void PostPreApplyDamage(ref DamageInfo dinfo,out bool absorbed){
   absorbed=false;var p=Pawn;if(p==null || p.Dead)return;
   switch(Props.kind){
    case "HellHound":if(Beasts.IsFire(dinfo.Def))absorbed=true;break;
    case "WinterWolf":if(Beasts.IsFire(dinfo.Def))dinfo.SetAmount(dinfo.Amount*Beasts.WinterFireFactor);break;
    case "DisplacerBeast":absorbed=TryPhantom(dinfo);break;
   }
  }
  // 位移幻影：有攻击者、会伤血的伤害（近战、远程、爆炸都算）落空一次，幻影消失6秒。
  bool TryPhantom(DamageInfo dinfo){
   if(dinfo.Instigator==null || dinfo.Def==null || !dinfo.Def.harmsHealth)return false;
   if(phantomBrokeTick==Now && phantomBrokeBy==dinfo.Instigator)return true;
   if(!PhantomActive)return false;
   phantomReadyTick=Now+Beasts.PhantomCooldown;phantomBrokeTick=Now;phantomBrokeBy=dinfo.Instigator;
   var p=Pawn;if(p.Spawned)MoteMaker.ThrowText(p.DrawPos,p.Map,"落空",new Color(.75f,.6f,1f));
   SyncPhantom();
   return true;
  }
  void SyncPhantom(){
   var p=Pawn;if(Props.kind!="DisplacerBeast" || p?.health==null || p.Dead)return;
   bool want=PhantomActive,has=Items.Has(p,"LP_DisplacementPhantom");
   if(want && !has)Items.Give(p,"LP_DisplacementPhantom");else if(!want && has)Items.Remove(p,"LP_DisplacementPhantom");
  }

  public override void PostPostApplyDamage(DamageInfo dinfo,float totalDamageDealt){
   base.PostPostApplyDamage(dinfo,totalDamageDealt);
   if(Props.kind=="BlinkDog" && dinfo.Instigator!=null && dinfo.Instigator!=parent)TryBlink(dinfo.Instigator,Beasts.Roll);
  }
  // 闪现：挨打后 30% 瞬移到 3～6 格外、比现在离攻击者更远、能站立且走得到的空地；冷却10秒。
  public bool TryBlink(Thing attacker,float roll){
   var p=Pawn;
   if(Props.kind!="BlinkDog" || p==null || p.Dead || p.Downed || !p.Spawned || attacker==null || !BlinkReady || roll>=Beasts.BlinkChance)return false;
   var cell=BlinkCell(p,attacker);if(!cell.IsValid)return false;
   var from=p.Position;
   Races124.Blink(p,cell);
   blinkReadyTick=Now+Beasts.BlinkCooldown;
   FleckMaker.ThrowDustPuffThick(from.ToVector3Shifted(),p.Map,1.2f,new Color(1f,.9f,.6f));
   MoteMaker.ThrowText(p.DrawPos,p.Map,"闪现",new Color(1f,.9f,.6f));
   return true;
  }
  public static IntVec3 BlinkCell(Pawn p,Thing attacker){
   var map=p.Map;var origin=p.Position;var threat=attacker.SpawnedOrAnyParentSpawned?attacker.PositionHeld:origin;
   float now=origin.DistanceTo(threat);
   var cells=GenRadial.RadialCellsAround(origin,Beasts.BlinkMax,true).Where(c=>c.InBounds(map) && c.DistanceTo(origin)>=Beasts.BlinkMin && c.DistanceTo(threat)>now && c.Standable(map) && c.GetFirstPawn(map)==null).ToList();
   cells.Shuffle();
   foreach(var c in cells)if(p.CanReach(c,PathEndMode.OnCell,Danger.Deadly))return c;
   return IntVec3.Invalid;
  }

  public override void CompTickInterval(int delta){
   base.CompTickInterval(delta);
   if((tickAcc+=delta)<60)return;tickAcc=0;
   var p=Pawn;if(p==null || p.Dead)return;
   if(Props.kind=="DisplacerBeast")SyncPhantom();
   // 原版的发光组件只在登记时记下位置：地狱犬走动后重新登记。
   if(Props.kind=="HellHound" && p.Spawned && p.Position!=glowCell){var glower=parent.GetComp<CompGlower>();if(glower!=null && glower.Glows){glower.ForceRegister(p.Map);glowCell=p.Position;}}
  }
  public override void PostSpawnSetup(bool respawningAfterLoad){base.PostSpawnSetup(respawningAfterLoad);glowCell=IntVec3.Invalid;}

  public override string CompInspectStringExtra(){
   int now=Now;
   switch(Props.kind){
    case "Owlbear":return now<hugReadyTick?"熊抱冷却："+((hugReadyTick-now)/60f).ToString("0.#")+"秒":null;
    case "DisplacerBeast":return now<phantomReadyTick?"位移幻影恢复中："+((phantomReadyTick-now)/60f).ToString("0.#")+"秒":null;
    case "BlinkDog":return now<blinkReadyTick?"闪现冷却："+((blinkReadyTick-now)/60f).ToString("0.#")+"秒":null;
    case "Worg":int n=PackAllies();return n>0?"群嚎：附近"+n+"只同伴，近战命中+"+(PackBonus*100f).ToString("0")+"%":null;
   }
   return null;
  }
  public override void PostExposeData(){
   base.PostExposeData();
   Scribe_Values.Look(ref hugReadyTick,"lpHugReady",-1);
   Scribe_Values.Look(ref phantomReadyTick,"lpPhantomReady",-1);
   Scribe_Values.Look(ref blinkReadyTick,"lpBlinkReady",-1);
  }
 }

 // 命中后：熊抱、寒霜迟缓（只在原版命中/闪避判定之后到这里，每次攻击一次）。
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class BeastMeleeHit {
  static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){Beasts.Comp(__instance.CasterPawn)?.Notify_MeleeHit(target.Pawn,__instance.tool);}
 }
 // 群嚎：加在最终近战命中率上（MeleeHitChance 属性的 StatPart 会在属性曲线之前生效，+5% 会被曲线压没）。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class BeastPackHit {
  static void Postfix(Verb_MeleeAttack __instance,ref float __result){
   var comp=Beasts.Comp(__instance.CasterPawn);if(comp==null || comp.Props.kind!="Worg")return;
   float bonus=comp.PackBonus;if(bonus>0)__result=Mathf.Min(1f,__result+bonus);
  }
 }
 // 驯服成功率在 Interacted 里就地算出来（没有单独的方法），所以在 Rand.Chance(成功率) 之前插入一次倍率计算，并写回同一个局部变量（失败提示的百分比也随之更新）。
 [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt),nameof(InteractionWorker_RecruitAttempt.Interacted))]
 public static class BeastTameBonus {
  static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions){
   var chance=AccessTools.Method(typeof(Rand),nameof(Rand.Chance));var helper=AccessTools.Method(typeof(Beasts),nameof(Beasts.TameChance));
   var code=instructions.ToList();
   for(int i=1;i<code.Count;i++){
    if(!code[i].Calls(chance) || !code[i-1].IsLdloc())continue;
    var store=Store(code[i-1]);if(store==null)break;
    code.InsertRange(i,new[]{new CodeInstruction(OpCodes.Ldarg_1),new CodeInstruction(OpCodes.Ldarg_2),new CodeInstruction(OpCodes.Call,helper),new CodeInstruction(OpCodes.Dup),store});
    Beasts.TamePatched=true;break;
   }
   if(!Beasts.TamePatched)Log.Warning("[DND边缘大陆] 没找到原版驯服成功率的位置，DND 野兽的种族驯服加成不生效。");
   return code;
  }
  static CodeInstruction Store(CodeInstruction load){
   var op=load.opcode;
   if(op==OpCodes.Ldloc_0)return new CodeInstruction(OpCodes.Stloc_0);
   if(op==OpCodes.Ldloc_1)return new CodeInstruction(OpCodes.Stloc_1);
   if(op==OpCodes.Ldloc_2)return new CodeInstruction(OpCodes.Stloc_2);
   if(op==OpCodes.Ldloc_3)return new CodeInstruction(OpCodes.Stloc_3);
   if(op==OpCodes.Ldloc_S)return new CodeInstruction(OpCodes.Stloc_S,load.operand);
   if(op==OpCodes.Ldloc)return new CodeInstruction(OpCodes.Stloc,load.operand);
   return null;
  }
 }
 // 关闭“野外刷新 DND 野兽”时，这 6 种在所有生态区的野外权重为 0（狂暴兽群从生态区挑动物也看这个权重）。
 [HarmonyPatch(typeof(BiomeDef),nameof(BiomeDef.CommonalityOfAnimal))]
 public static class BeastWildToggle {
  static void Postfix(PawnKindDef animalDef,ref float __result){if(__result>0f && LichMod.Settings?.dndBeastsWild==false && Beasts.IsBeastKind(animalDef))__result=0f;}
 }
 [StaticConstructorOnStartup]
 public static class BeastStartup {static BeastStartup(){Beasts.ApplySettings();}}

 // 1.25.0 设置：DND 野兽。
 public partial class Settings {
  public bool dndBeastsWild=true;
  void ExposeBeasts(){Scribe_Values.Look(ref dndBeastsWild,"dndBeastsWild",true);ExposeBeastLevels();ExposeWildMonsters();}
 }
 public static class BeastSettings {
  public static void Draw(Listing_Standard l){l.GapLine();l.Label("DND野兽");l.CheckboxLabeled("野外刷新 DND 野兽",ref LichMod.Settings.dndBeastsWild,"猫头鹰熊、邪座狼、冬狼、地狱犬、魔化兽、闪光犬会在各自的生态区野外刷新，前五种也会以狂暴兽群出现。关闭后不再刷新、也不会以狂暴兽群出现；保存设置后立即生效，已经在地图上的野兽不受影响。");
   WildMonsterSettings.Draw(l); // DND 怪物第二批
   // 1.70.0 野兽强化
   var s=LichMod.Settings;
   l.CheckboxLabeled("野兽野性等级",ref s.beastLevels,"野生和非玩家的动物出现时带「野性等级」：狂暴兽群和敌对组按威胁点数定级，野外动物 1～殖民地最高等级 −3，上限殖民地最高等级 +3。等级越高近战伤害、部位生命越高、跑得越快（与殖民者同一条六维曲线，1 级不变）。玩家的动物靠战斗和搬运成长。关闭后不再给新动物定级，已有的等级不提供效果。");
   if(s.beastLevels){
    TooltipHandler.TipRegion(l.Label("精英野兽概率："+Mathf.RoundToInt(s.beastEliteChance*100f)+"%"),"野兽出现时成为精英（恐怖 / 狂暴 / 远古 / 深渊腐化）的概率，死后掉落兽王之心。狂暴兽群的首领总是精英。");
    s.beastEliteChance=Mathf.Round(l.Slider(s.beastEliteChance,0f,BeastLevels.EliteMax)*100f)/100f;
   }
  }
 }
}
