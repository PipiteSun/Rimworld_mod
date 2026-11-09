using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.100.0 魔力湖泊（设计文档：魔力湖泊：法术结晶派系设计）：中立的湖畔法术匠人，出售「秘传结晶」。
 // 职业水晶：12 个职业各 4 枚（6 / 7 / 8 / 9 环），只有该职业能学，要该职业 11 / 13 / 15 / 17 级；学会的能力在 ClassData.arcana，不进职业树。
 // 秘传槽：冒险者 11 / 15 / 20 级、传奇 30 级各 1 个（最多 4 个）。秘传不耗资源，只看冷却；两个秘传之间另有 1 小时公共冷却。
 // 派系与货单在 Defs/ManaLake.xml；能力在 Defs/ManaLake_Arcana.xml，结晶在 Defs/ManaLake_Crystals.xml（ArtSource/ManaLake/gen_crystals.ps1 生成）；效果在 ManaLakeEffects.cs。
 public class ArcanumExt : DefModExtension {
  public int ring=6;public LP_ClassDef cls;
  public string ability="Any",kind="spell";public int price; // 1.133.0 通用秘传（cls 为空）：施法属性（Int / Wis / Cha / Str / Dex / Any）、类别（spell / martial / passive）、价格
  public bool Universal=>cls==null;public bool Martial=>kind=="martial";public bool Passive=>kind=="passive";
  public override IEnumerable<string> ConfigErrors(){foreach(var e in base.ConfigErrors())yield return e;if(ring<6 || ring>9)yield return "arcanum ring must be 6..9";}
 }
 public class CompProperties_Arcanum : CompProperties_UseEffect {
  public AbilityDef ability;
  public CompProperties_Arcanum(){compClass=typeof(CompUseEffect_Arcanum);}
 }
 // 吸收结晶：成功才用掉（叠起来的只用一枚）。
 public class CompUseEffect_Arcanum : CompUseEffect {
  public CompProperties_Arcanum Props=>(CompProperties_Arcanum)props;
  public override AcceptanceReport CanBeUsedBy(Pawn p){var r=LakeArcana.CannotLearn(p,Props.ability);return r!=null?(AcceptanceReport)r:base.CanBeUsedBy(p);}
  public override void DoEffect(Pawn usedBy){
   base.DoEffect(usedBy);
   if(LakeArcana.Ext(Props.ability)?.Universal==true){if(!LakeLore.TryAbsorb(usedBy,Props.ability,Rand.Value,true))return;} // 1.133.0 通用秘传：按成功率吸收，失败结晶不碎
   else if(!LakeArcana.Learn(usedBy,Props.ability,true))return;
   if(parent.stackCount>1)parent.SplitOff(1).Destroy();else parent.Destroy();
  }
  public override TaggedString ConfirmMessage(Pawn p)=>null;
 }

 public static class ManaLake {
  public const string FactionName="LP_ManaLake",Caravan="LP_Caravan_ManaLake",Base="LP_Base_ManaLake";
  static FactionDef def;
  public static FactionDef Def=>def??(def=DefDatabase<FactionDef>.GetNamedSilentFail(FactionName));
  public static Faction Faction=>Factions.Of(Def);
  public static int Goodwill(Faction f)=>f==null || f.IsPlayer || Faction.OfPlayer==null?0:f.PlayerGoodwill;
 }

 public static class LakeArcana {
  public static readonly int[] SlotLevels={11,15,20,30};
  public const int PublicCooldown=2500;
  public static int ClassLevelFor(int ring)=>ring<=6?11:ring==7?13:ring==8?15:17;
  public static int PriceFor(int ring)=>ring<=6?8000:ring==7?12000:ring==8?16000:25000;
  static List<AbilityDef> all;static List<ThingDef> crystals;
  public static List<AbilityDef> All=>all??(all=DefDatabase<AbilityDef>.AllDefsListForReading.Where(d=>d.HasModExtension<ArcanumExt>()).ToList());
  public static List<ThingDef> Crystals=>crystals??(crystals=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.GetCompProperties<CompProperties_Arcanum>()?.ability!=null).ToList());
  public static ArcanumExt Ext(AbilityDef d)=>d?.GetModExtension<ArcanumExt>();
  public static AbilityDef AbilityOf(ThingDef crystal)=>crystal?.GetCompProperties<CompProperties_Arcanum>()?.ability;
  public static ThingDef CrystalFor(AbilityDef d)=>Crystals.FirstOrDefault(c=>AbilityOf(c)==d);
  public static LP_ClassDef ClassOf(AbilityDef d)=>Ext(d)?.cls;
  public static bool Is(AbilityDef d)=>Ext(d)!=null;
  public static IEnumerable<AbilityDef> OfClass(LP_ClassDef c)=>All.Where(d=>Ext(d).cls==c).OrderBy(d=>Ext(d).ring);

  // ---------- 槽位与学习 ----------
  public static int Slots(Hediff_Adventurer h){int n=0;if(h!=null)foreach(int l in SlotLevels)if(h.level>=l)n++;return n;}
  public static int Used(Hediff_Adventurer h)=>h?.classes?.arcana?.Count??0;
  public static bool Knows(Hediff_Adventurer h,AbilityDef d)=>d!=null && h?.classes?.arcana!=null && h.classes.arcana.Contains(d);
  public static bool Knows(Pawn p,AbilityDef d)=>Knows(Adventurer.Get(p),d);
  public static bool Has(Pawn p,string key){if(p==null)return false;var h=Adventurer.Get(p);var l=h?.classes?.arcana;if(l==null || l.Count==0)return false;foreach(var a in l)if(a!=null && a.defName.EndsWith(key) && a.defName.Length==key.Length+6)return true;return false;} // key 是 LP_AC_ 后面那段
  public static string CannotLearn(Pawn p,AbilityDef d){
   var e=Ext(d);if(e==null)return "这枚结晶里没有秘传";
   if(p?.RaceProps?.Humanlike!=true)return "只有人形生物能吸收秘传结晶";
   if(!Adventurer.Enabled)return "冒险者系统没有开启";
   var h=Adventurer.Get(p);if(h==null)return "没有冒险者等级";
   if(h.classes==null)h.classes=new ClassData();
   if(Knows(h,d))return "已经学会「"+d.label+"」";
   if(e.cls!=null){int need=ClassLevelFor(e.ring);if(ClassSystem.Level(h,e.cls)<need)return "需要"+e.cls.label+" "+need+" 级（"+e.ring+" 环职业水晶）";}
   else{var why=LakeLore.CannotLearn(p,h,e);if(why!=null)return why;} // 1.133.0 通用秘传的等级 / 属性 / 武技门槛、失败后 7 天
   int slots=Slots(h);if(slots<=0)return "冒险者 "+SlotLevels[0]+" 级才有秘传槽";
   if(Used(h)>=slots)return "秘传槽已满（"+Used(h)+" / "+slots+"）";
   return null;
  }
  public static bool Learn(Pawn p,AbilityDef d,bool message=false){
   if(CannotLearn(p,d)!=null)return false;var h=Adventurer.Get(p);
   h.classes.arcana.Add(d);Touched=true;
   if(p.abilities!=null && p.abilities.GetAbility(d)==null)p.abilities.GainAbility(d);
   Passives(h);
   if(message && p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"吸收了秘传结晶，学会了「"+d.label+"」（秘传 "+Used(h)+" / "+Slots(h)+"）。",p,MessageTypeDefOf.PositiveEvent,false);
   return true;
  }
  public static int Sync(Hediff_Adventurer h){
   var p=h?.pawn;var l=h?.classes?.arcana;if(p?.abilities==null || l==null || l.Count==0)return 0;int n=0;
   Touched=true;foreach(var a in l)if(a!=null && p.abilities.GetAbility(a)==null){p.abilities.GainAbility(a);n++;}
   return n;
  }

  // ---------- 施放 ----------
  // 任何人学过秘传之后才打开伤害挂钩（读档后在 Tick250 / Sync 里打开）。
  public static bool Touched;
  public static void Reset(){Touched=false;all=null;crystals=null;}
  public static string CannotCast(Pawn p,AbilityDef def){
   if(LakeLore.Live){var lw=LakeLore.CannotCast(p,def)??LakeSeals.CannotCast(p,def);if(lw!=null)return lw;} // 1.133.0 沉默、湖息
   if(!Is(def))return null;var h=Adventurer.Get(p);if(h?.classes==null)return null;
   int left=h.classes.arcanaLast+PublicCooldown-Find.TickManager.TicksGame;
   if(h.classes.arcanaLast>0 && left>0)return "秘传公共冷却（还要 "+left.ToStringTicksToPeriod()+"）";
   return null;
  }
  public static void AfterCast(Ability_Class a,LocalTargetInfo target){
   if(a?.pawn==null || a.def==null)return;var h=Adventurer.Get(a.pawn);
   if(Is(a.def)){if(h?.classes!=null)h.classes.arcanaLast=Find.TickManager.TicksGame;Touched=true;return;}
   if(h?.classes?.arcana==null || h.classes.arcana.Count==0)return;
   ArcanaEffects.AfterClassCast(a,target);
  }
  // 法力消耗：咒法精通让法师 1 级法术免费。
  public static int Cost(Pawn p,AbilityDef def,int cost){
   if(cost<=0 || def==null || !Has(p,"CantripMastery"))return cost;
   var c=ClassSystem.ClassOf(def);var e=ClassSystem.EntryOf(def);
   return c!=null && c.defName=="LP_Class_Wizard" && e!=null && e.level<=1?0:cost;
  }
  public static float PowerFactor(Pawn p,CompProperties_ClassEffect e){
   if(e==null || !Touched)return 1f;float f=1f;
   if(e.sorcery && Has(p,"SeekingSpell"))f*=1.25f;
   if(e.sorcery && Items.Has(p,"LP_ACB_ElementalForm"))f*=1.3f;
   return f;
  }
  public static float DurationFactor(Pawn p,CompProperties_ClassEffect e)=>e!=null && e.sorcery && Touched && Has(p,"ExtendedSpell")?2f:1f;

  // ---------- 被动（每 250 刻） ----------
  public static void Passives(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null || p.Dead)return;
   if(h.classes?.lake!=null)LakeSeals.Tick250(h); // 1.133.0 湖息回复、湖印被动
   if(h.classes?.arcana==null || h.classes.arcana.Count==0)return;
   Touched=true;LakeLore.Tick250(h); // 1.133.0 通用秘传被动
   bool cloud=Has(p,"CloudBody");if(cloud && !Items.Has(p,"LP_ACP_CloudBody"))Items.Give(p,"LP_ACP_CloudBody");else if(!cloud)Items.Remove(p,"LP_ACP_CloudBody");
   ArcanaEffects.Tick250(h);
  }

  // ---------- 冒险者分页：职业行里的「秘传」 ----------
  static string armed;
  public static float DrawClassRow(float y,float w,Pawn p,Hediff_Adventurer h,LP_ClassDef c){
   var list=OfClass(c).ToList();if(list.Count==0)return y;
   var row=new Rect(0f,y,w,44f);Widgets.DrawBoxSolid(row,new Color(.12f,.25f,.4f,.25f));
   GUI.color=new Color(.55f,.8f,1f);Widgets.Label(new Rect(6f,y+12f,50f,24f),"秘传");GUI.color=Color.white;
   float x=60f,cw=(w-60f)/list.Count;
   foreach(var a in list){
    var cell=new Rect(x,y+2f,cw-6f,40f);x+=cw;var e=Ext(a);bool have=Knows(h,a);
    if(!have)GUI.color=new Color(.5f,.5f,.5f);GUI.DrawTexture(new Rect(cell.x,cell.y+2f,36f,36f),a.uiIcon??BaseContent.BadTex);GUI.color=Color.white;
    if(!have)GUI.color=Color.gray;Text.Font=GameFont.Tiny;Widgets.Label(new Rect(cell.x+40f,cell.y+4f,cell.width-40f,36f),a.LabelCap+"\n"+(have?"已掌握":e.ring+" 环 · "+c.label+" "+ClassLevelFor(e.ring)+" 级"));Text.Font=GameFont.Small;GUI.color=Color.white;
    TooltipHandler.TipRegion(cell,a.LabelCap+"（"+e.ring+" 环职业水晶）\n\n"+a.description+"\n\n"+(have?"已掌握":"需要「"+a.label+"」秘传结晶（魔力湖泊出售），"+c.label+" "+ClassLevelFor(e.ring)+" 级，并占 1 个秘传槽。"));
   }
   return y+46f;
  }
  public static string SlotLine(Hediff_Adventurer h)=>"秘传槽 "+Used(h)+" / "+Slots(h);
  public const string SlotTip="秘传槽：冒险者 11 / 15 / 20 级、传奇 30 级各 1 个，最多 4 个。\n秘传结晶由魔力湖泊出售（职业水晶要和他们结盟），吸收后永久学会，不占职业点。\n秘传不耗资源，只看冷却；两个秘传之间另有 1 小时公共冷却。";

  // ---------- 货单：按环出结晶，好感不够不出；一半机会偏向殖民者已有的职业 ----------
  public static HashSet<LP_ClassDef> ColonyClasses(){
   var s=new HashSet<LP_ClassDef>();
   foreach(var p in PawnsFinder.AllMaps_FreeColonists){var h=Adventurer.Get(p);if(h?.classes==null)continue;foreach(var kv in h.classes.levels)if(kv.Value>0 && kv.Key!=null)s.Add(kv.Key);}
   return s;
  }
 }

 public class StockGenerator_Arcana : StockGenerator {
  public IntRange rings=new IntRange(6,7);public int minGoodwill=75;public float classBias=.5f;public bool universal; // 1.133.0 universal=true 出通用秘传（不含许愿术），否则只出职业水晶
  public static int? TestGoodwill;
  public IEnumerable<ThingDef> Pool=>LakeArcana.Crystals.Where(d=>{var a=LakeArcana.AbilityOf(d);var e=LakeArcana.Ext(a);return e!=null && e.Universal==universal && a.defName!=LakeLore.WishName && e.ring>=rings.min && e.ring<=rings.max && d.tradeability.TraderCanSell();});
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   int gw=TestGoodwill??ManaLake.Goodwill(faction??ManaLake.Faction);if(gw<minGoodwill)yield break;
   var pool=Pool.ToList();var mine=LakeArcana.ColonyClasses();
   int n=countRange.RandomInRange;
   for(int i=0;i<n && pool.Count>0;i++){
    var biased=!universal && mine.Count>0 && Rand.Chance(classBias)?pool.Where(d=>mine.Contains(LakeArcana.Ext(LakeArcana.AbilityOf(d)).cls)).ToList():null;
    var pick=(biased!=null && biased.Count>0?biased:pool).RandomElement();pool.Remove(pick);
    yield return ThingMaker.MakeThing(pick);
   }
  }
  public override bool HandlesThingDef(ThingDef d)=>d!=null && d.GetCompProperties<CompProperties_Arcanum>()!=null;
 }

 // 学过秘传的人读档后重新打开挂钩；新游戏清空缓存。
 public class GameComponent_Arcana : GameComponent {
  public bool wishSeen; // 1.133.0 许愿术结晶每个存档最多上架一次
  public static GameComponent_Arcana Instance;
  public GameComponent_Arcana(Game g){Instance=this;LakeLore.Reset();}
  public override void FinalizeInit(){Instance=this;LakeArcana.Reset();ArcanaEffects.Reset();LakeLore.Reset();LakeLore.Rescan();}
  public override void GameComponentTick(){if(LakeTime.Active)LakeTime.Tick();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref wishSeen,"lpWishSeen");}
 }
}
