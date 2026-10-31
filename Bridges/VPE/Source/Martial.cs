using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VanillaPsycastsExpanded;
using VanillaPsycastsExpanded.UI;
using AbilityDef=VEF.Abilities.AbilityDef;
using Ability=VEF.Abilities.Ability;

namespace LichPhylactery.VPEBridge {
 // 1.72.0：战士、武僧、圣武士移到主程序集（Source/Classes*.cs，原版能力 + 冒险者职业等级）。这里只剩：
 // · 把三条旧路径从 VPE“DND职业”页签里拿掉（路径和旧能力的定义保留，老存档读得进来、迁移时要数学过几个）；
 // · 读档时一次性迁移（ClassMigration）：学过的旧能力数 n → 职业等级 ceil(n/2)（受冒险者等级剩下的职业点限制，至少 1 级），
 //   学过的能力全部保留（超过等级的记在 ClassData.kept），删掉旧 VPE 能力和路径，VPE 路径点退回；
 // · 旧类名的空壳（老存档里的状态、组件按类名读）；VPE 灵能树一行超过 3 个的画法（MartialWideTree，其他路径以后也可能用）。
 public static class ClassMigration {
  static HashSet<string> hidden;
  public static HashSet<string> HiddenPaths=>hidden??(hidden=new HashSet<string>(ClassSystem.All.Where(c=>!c.legacyPath.NullOrEmpty()).Select(c=>c.legacyPath)));
  public static bool Hidden(PsycasterPathDef p)=>p!=null && HiddenPaths.Contains(p.defName);
  public static bool Hidden(AbilityDef d)=>Hidden(d?.Psycast()?.path);
  // 一个人的迁移；返回迁移出的职业数。
  public static int Migrate(Pawn p){
   var comp=p?.GetComp<CompAbilities>();var psy=Classes.Psycasts(p);
   var learned=comp?.LearnedAbilities.Where(a=>Hidden(a.def)).ToList()??new List<Ability>();
   var paths=psy?.unlockedPaths?.Where(Hidden).ToList()??new List<PsycasterPathDef>();
   if(learned.Count==0 && paths.Count==0)return 0;
   var h=Adventurer.Get(p)??Adventurer.TryAdd(p,1,true);
   int made=0;
   if(h!=null){
    if(h.classes==null)h.classes=new ClassData();
    var byClass=new Dictionary<LP_ClassDef,List<string>>();
    foreach(var path in paths){var c=ClassSystem.ByLegacyPath(path.defName);if(c!=null && !byClass.ContainsKey(c))byClass[c]=new List<string>();}
    foreach(var a in learned){var c=ClassSystem.ByLegacyPath(a.def.Psycast().path.defName);if(c==null)continue;if(!byClass.TryGetValue(c,out var l))byClass[c]=l=new List<string>();l.Add(a.def.defName);}
    int left=ClassSystem.Points(h);
    foreach(var kv in byClass.OrderByDescending(x=>x.Value.Count)){
     var c=kv.Key;int want=Mathf.Max(1,(kv.Value.Count+1)/2);int lvl=Mathf.Max(1,Mathf.Min(want,left));left=Mathf.Max(0,left-lvl);
     if(ClassSystem.Level(h,c)<lvl)ClassSystem.SetLevel(h,c,lvl);
     foreach(var name in kv.Value){var nd=DefDatabase<RimWorld.AbilityDef>.GetNamedSilentFail(name);var e=ClassSystem.EntryOf(nd);if(e!=null && e.level>lvl && !h.classes.kept.Contains(nd))h.classes.kept.Add(nd);}
     made++;
    }
    h.classes.migrated=true;
    ClassSystem.Sync(h);
   }
   if(comp!=null)comp.LearnedAbilities.RemoveAll(a=>Hidden(a.def));
   if(psy!=null){psy.points+=learned.Count+paths.Count;psy.unlockedPaths.RemoveAll(Hidden);}
   // 老存档里这些状态还是桥里的旧类：拿掉（武僧架势、灵光会在 250 刻内按新类重给）。
   foreach(var hd in p.health.hediffSet.hediffs.ToList())if(hd.GetType().Namespace==typeof(ClassMigration).Namespace && hd.def.hediffClass!=hd.GetType() && hd.def.hediffClass.Namespace=="LichPhylactery")p.health.RemoveHediff(hd);
   return made;
  }
  public static int MigrateAll(IEnumerable<Pawn> pawns){int n=0;foreach(var p in pawns.ToList())if(p!=null && !p.Destroyed)n+=Migrate(p)>0?1:0;return n;}
 }
 // 1.73.0：迁移按版本号跑（1.72.0 只迁了战士 / 武僧 / 圣武士，老存档里 done=true 也要再跑一遍迁剩下五条；Migrate 对已经迁过的人什么都不做）。
 public class GameComponent_ClassMigration : GameComponent {
  public const int Version=3; // 1.74.0 热修：读回来的旧能力（见 LegacyAbilityClassNames）再迁一遍
  public bool done;public int version;
  public GameComponent_ClassMigration(Game game){}
  public override void FinalizeInit(){
   base.FinalizeInit();if(version>=Version)return;done=true;version=Version;
   int n=ClassMigration.MigrateAll(PawnsFinder.AllMapsWorldAndTemporary_Alive);
   if(n>0)Log.Message("[DND边缘大陆] 1.73.0：把 "+n+" 人的 VPE DND 职业路径换成了冒险者职业等级。");
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref done,"lpClassMigrated");Scribe_Values.Look(ref version,"lpClassMigratedVersion");}
 }

 // VPE 灵能页签：三条旧路径不显示（页签在构造函数里把所有路径按 tab 分组一次）。
 [HarmonyPatch]
 public static class HideMovedPaths {
  static MethodBase TargetMethod()=>AccessTools.Constructor(AccessTools.TypeByName("VanillaPsycastsExpanded.UI.ITab_Pawn_Psycasts")??AccessTools.TypeByName("VanillaPsycastsExpanded.ITab_Pawn_Psycasts"),Type.EmptyTypes);
  // 1.73.0：八条都隐藏后“DND职业”页签空了，连页签一起拿掉。
  public static Type TabType=>AccessTools.TypeByName("VanillaPsycastsExpanded.UI.ITab_Pawn_Psycasts")??AccessTools.TypeByName("VanillaPsycastsExpanded.ITab_Pawn_Psycasts");
  public static void Postfix(object __instance){
   var tr=Traverse.Create(__instance);
   var dict=tr.Field("pathsByTab").GetValue<Dictionary<string,List<PsycasterPathDef>>>();if(dict==null)return;
   foreach(var list in dict.Values)list.RemoveAll(ClassMigration.Hidden);
   var empty=dict.Where(kv=>kv.Value.Count==0).Select(kv=>kv.Key).ToList();if(empty.Count==0)return;
   foreach(var k in empty)dict.Remove(k);
   tr.Field("tabs").GetValue<List<TabRecord>>()?.RemoveAll(t=>empty.Contains(t.label));
   var cur=tr.Field("curTab").GetValue<string>();if(cur==null || empty.Contains(cur))tr.Field("curTab").SetValue(dict.Keys.FirstOrDefault());
  }
 }

 // VPE 的灵能树一行最多画 3 个能力（abilityTreeXOffsets）。DND 路径有超过 3 个的行时由这里按同样的规则画（间距按宽度收紧）。
 [HarmonyPatch(typeof(PsycastsUIUtility),nameof(PsycastsUIUtility.DoPathAbilities))]
 public static class MartialWideTree {
  static readonly Dictionary<PsycasterPathDef,bool> wide=new Dictionary<PsycasterPathDef,bool>();
  public static bool Wide(PsycasterPathDef path){if(!wide.TryGetValue(path,out bool w))wide[path]=w=path.abilityLevelsInOrder!=null && path.abilityLevelsInOrder.Any(r=>r!=null && r.Length>3);return w;}
  public static float Offset(int n,int pos,float width){
   if(n<=1)return -18f;if(n==2)return pos==0?-47f:11f;if(n==3)return new[]{-69f,-18f,33f}[pos];
   float s=Mathf.Min(42f,(width-40f)/(n-1));return -18f+(pos-(n-1)/2f)*s;
  }
  static bool Prefix(Rect inRect,PsycasterPathDef path,Dictionary<AbilityDef,Vector2> abilityPos,Action<Rect,AbilityDef> doAbility){
   if(!(path is LP_ClassPathDef) || !Wide(path))return true;
   var comp=PsycastsUIUtility.CompAbilities;if(comp==null || PsycastsUIUtility.Hediff==null)return true;
   foreach(var def in path.abilities)
    if(def.Psycast()?.prerequisites is List<AbilityDef> pre && abilityPos.ContainsKey(def))
     foreach(var a in pre)if(abilityPos.ContainsKey(a))Widgets.DrawLine(abilityPos[def],abilityPos[a],comp.HasAbility(a)?Color.white:Color.grey,2f);
   for(int level=0;level<path.abilityLevelsInOrder.Length;level++){
    var levelRect=new Rect(inRect.x,inRect.y+(path.MaxLevel-1-level)*inRect.height/path.MaxLevel+10f,inRect.width,inRect.height/5f);
    var row=path.abilityLevelsInOrder[level];
    for(int pos=0;pos<row.Length;pos++){
     var rect=new Rect(levelRect.x+levelRect.width/2f+Offset(row.Length,pos,levelRect.width),levelRect.y,36f,36f);
     var def=row[pos];if(def==PsycasterPathDef.Blank)continue;
     abilityPos[def]=rect.center;doAbility(rect,def);
    }
   }
   return false;
  }
 }

 // ---------- 旧能力 / 旧类名（只为老存档能读进来） ----------
 // 三条旧路径的 VPE 能力：没有按钮、没有效果，读档后由 ClassMigration 删掉。
 public class Ability_ClassLegacy : Ability {
  public override bool ShowGizmoOnPawn()=>false;
 }
 // 1.74.0 热修：1.73.0 删掉了旧职业能力的类（Ability_MagicMissile、Ability_SecondWind 等 57 个），老存档里 VEF 能力列表按这些类名读，
 // 找不到时 VEF 退回抽象的 Ability，整个人读档失败（PawnKindDef 为空的“半个人”：标签报错、和他互动的殖民者每次互动都中断并反复重来，
 // 社交分页里的心情和好感一直在变）。这些类名一律读成 Ability_ClassLegacy，再由迁移删掉。
 [HarmonyPatch(typeof(BackCompatibility),nameof(BackCompatibility.GetBackCompatibleType))]
 public static class LegacyAbilityClassNames {
  public const string Prefix_="LichPhylactery.VPEBridge.Ability_";
  public static Type Map(Type baseType,string name){
   if(name==null || !name.StartsWith(Prefix_) || baseType==null || !baseType.IsAssignableFrom(typeof(Ability_ClassLegacy)))return null;
   return GenTypes.GetTypeInAnyAssembly(name)==null?typeof(Ability_ClassLegacy):null;
  }
  static bool Prefix(Type baseType,string providedClassName,ref Type __result){var t=Map(baseType,providedClassName);if(t==null)return true;__result=t;return false;}
 }
 public class Hediff_MartialFlag : HediffWithComps {}
 public class Hediff_Enmity : HediffWithComps {public Pawn caster;public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref caster,"lpEnmityCaster");}}
 public class Hediff_QuiveringPalm : HediffWithComps {}
 public class Hediff_SuppressingVolley : HediffWithComps {public override bool ShouldRemove=>true;}
 public class LP_ChargeFlyer : PawnFlyer {}
 public class MartialState : GameComponent {public MartialState(Game game){}}
 // 1.73.0 移走的施法职业的旧类名。
 public class Hediff_ClassStrike : HediffWithComps {}
 public class Hediff_ClassScaled : HediffWithComps {}
 public class Hediff_WildShape : HediffWithComps {public Thing stowed;public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref stowed,"lpStowed");}}
 public class MagicMissileBullet : Bullet {}
 public class Building_RuneTurret : Building_TurretGun {}
 public class MapComponent_ClassAuras : MapComponent {public MapComponent_ClassAuras(Map map):base(map){}}
 public class ClassState : GameComponent {public ClassState(Game game){}}
}
