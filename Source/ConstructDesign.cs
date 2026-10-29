using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 public partial class Building_Altar {
  public int fleshRecipeVersion=1;
  public void UpgradeFleshRecipes(){if(fleshRecipeVersion>=1)return;foreach(var bill in BillStack.Bills){string name=bill.recipe.defName;if(name=="LP_Offer" || name.StartsWith("LP_Make"))bill.ingredientFilter.CopyAllowancesFrom(bill.recipe.fixedIngredientFilter);if(name.StartsWith("LP_Harvest"))bill.suspended=true;}fleshRecipeVersion=1;}
 }
 public partial class ConstructDesign : IExposable {
  public int meat,steel,plasteel;
  public string specialty="大脑";
  public static readonly string[] Specialties={"大脑","眼睛","手臂","心脏"};
  public static readonly string[] SpecialtyLabels={"研究","制造","建造","医疗"};
  public static string SpecialtyName(string value){int i=Array.IndexOf(Specialties,value);return i>=0?SpecialtyLabels[i]:value;}
  public void Normalize(){meat=Mathf.Clamp(meat,0,600);steel=Mathf.Clamp(steel,0,300);plasteel=Mathf.Clamp(plasteel,0,150);if(!Specialties.Contains(specialty))specialty="大脑";NormalizePassions();}
  public ConstructDesign Copy()=>new ConstructDesign{meat=meat,steel=steel,plasteel=plasteel,specialty=specialty,passions=passions==null?new Dictionary<SkillDef,Passion>():new Dictionary<SkillDef,Passion>(passions)};
  public float Health=>1f+meat/400f;
  public float Armor=>steel/600f+plasteel/1000f;
  public float Speed=>1f+plasteel/500f-steel/3000f;
  public float Work=>1f+plasteel/300f;
  public int SkillBonus=>plasteel/30;
  public int Score=>meat+steel*2+plasteel*4;
  public string Quality=>Score==0?"基础":Score<300?"改良":Score<900?"精良":Score<1500?"卓越":"杰作";
  public string Summary(string kind,bool initial=true)=>"躯体质量："+Quality+"\n生命上限：×"+Health.ToString("0.00")+"\n利器 / 钝器护甲：+"+(Armor*100).ToString("0.#")+"%\n移动速度：×"+Speed.ToString("0.00")+"\n工作与学习效率：×"+Work.ToString("0.00")+(initial && kind=="Scholar"?"\n专长初始等级："+(10+SkillBonus):initial && (kind=="Golem" || kind=="StoneGolem")?"\n近战初始等级："+(10+SkillBonus):"")+"\n追加材料：生肉 "+meat+" / 钢铁 "+steel+" / 玻璃钢 "+plasteel;
  public void ExposeData(){Scribe_Values.Look(ref meat,"extraMeat");Scribe_Values.Look(ref steel,"extraSteel");Scribe_Values.Look(ref plasteel,"extraPlasteel");Scribe_Values.Look(ref specialty,"specialty","大脑");Scribe_Collections.Look(ref passions,"skillPassions",LookMode.Def,LookMode.Value);if(Scribe.mode==LoadSaveMode.PostLoadInit)Normalize();}
 }
 public class ConstructSeed : ThingWithComps {
  public Pawn master;
  public string kind="Skeleton";
  public ConstructDesign design=new ConstructDesign();
  public static int BaseMeat(string kind)=>kind=="Skeleton"?50:kind=="Golem"?300:kind=="StoneGolem"?0:150;
  public static int Control(string kind)=>kind=="Skeleton"?1:kind=="Golem"?5:kind=="StoneGolem"?4:3;
  public static ConstructSeed Make(string kind,Pawn master,string specialty="大脑"){
   var seed=(ConstructSeed)ThingMaker.MakeThing(Necro.Def<ThingDef>(kind+"Seed"));seed.master=master;seed.kind=kind;seed.design.specialty=specialty;seed.design.Normalize();return seed;
  }
  public static bool RawMeat(Thing t)=>t!=null && t.def.IsWithinCategory(ThingCategoryDefOf.MeatRaw) && t.GetRotStage()==RotStage.Fresh;
  public List<Thing> Supplies()=>Remolding.Supplies(this);
  public string Blocked(ConstructDesign plan,List<Thing> supplies){
   if(Destroyed || !Spawned)return "请把胚体放在地图上。";
   if(master==null || master.Faction!=Faction.OfPlayer || Necro.Lich(master)?.Active!=true)return "绑定命匣必须完好，主人必须存活且属于殖民地。";
   if(Necro.Used(master)+Control(kind)>Necro.Lich(master).box.Capacity)return "主人统御容量不足；胚体可保留到容量空闲时。";
   if(plan.meat<0 || plan.meat>600 || plan.steel<0 || plan.steel>300 || plan.plasteel<0 || plan.plasteel>150 || !ConstructDesign.Specialties.Contains(plan.specialty))return "配置超出允许范围。";
   if(plan.MajorCount(kind)>ConstructDesign.MajorPassionLimit)return ConstructDesign.PassionLimitReason;
   if(supplies.Where(RawMeat).Sum(t=>t.stackCount)<plan.meat)return "胚体12格内可用的新鲜生肉不足。";
   if(supplies.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount)<plan.steel)return "胚体12格内可用的钢铁不足。";
   if(supplies.Where(t=>t.def==ThingDefOf.Plasteel).Sum(t=>t.stackCount)<plan.plasteel)return "胚体12格内可用的玻璃钢不足。";
   return null;
  }
  public bool Awaken(ConstructDesign plan,out Pawn result,out string reason){
   result=null;var supplies=Supplies();reason=Blocked(plan,supplies);if(reason!=null)return false;
   IntVec3 cell;if(!CellFinder.TryFindRandomCellNear(Position,Map,3,c=>c.Standable(Map),out cell)){reason="胚体附近没有可站立位置。";return false;}
   // Stage exact stack splits before creating the pawn. Restore them if creation fails.
   var taken=new List<Thing>();Map map=Map;IntVec3 origin=Position;
   try{
    Take(supplies.Where(RawMeat),plan.meat,taken);Take(supplies.Where(t=>t.def==ThingDefOf.Steel),plan.steel,taken);Take(supplies.Where(t=>t.def==ThingDefOf.Plasteel),plan.plasteel,taken);
    result=Constructs.Create(kind,master,cell,map,plan.specialty,plan.Copy());
   }catch(Exception e){foreach(var t in taken)if(!t.Destroyed)GenPlace.TryPlaceThing(t,origin,map,ThingPlaceMode.Near);reason="唤醒失败，追加材料已退回。";Log.Error("[Lich] Construct awakening failed: "+e);return false;}
   foreach(var t in taken)t.Destroy();Destroy();return true;
  }
  public static void Take(IEnumerable<Thing> source,int amount,List<Thing> taken){foreach(var t in source.ToList()){if(amount<=0)break;int count=Math.Min(amount,t.stackCount);taken.Add(t.SplitOff(count));amount-=count;}if(amount!=0)throw new InvalidOperationException("Supplies changed during awakening");}
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref master,"master",true);Scribe_Values.Look(ref kind,"kind","Skeleton");Scribe_Deep.Look(ref design,"design");if(Scribe.mode==LoadSaveMode.PostLoadInit && design==null)design=new ConstructDesign();}
  public override string GetInspectString()=>base.GetInspectString()+"\n主人："+(master?.LabelShort??"失联")+(kind=="StoneGolem"?"\n胚体已消耗石块：300":"\n胚体已消耗生肉："+BaseMeat(kind))+"\n待唤醒 · "+design.Quality+"\n选择“配置死灵造物”调整初始属性。";
  public override IEnumerable<Gizmo> GetGizmos(){foreach(var g in base.GetGizmos()??Enumerable.Empty<Gizmo>())yield return g;yield return new Command_Action{defaultLabel="配置死灵造物",defaultDesc="调整生肉、钢铁和玻璃钢投入，预览属性并唤醒。",icon=Necro.Icon(kind),action=()=>Find.WindowStack.Add(new Dialog_ConstructDesign(this))};}
 }
 public partial class Hediff_Construct {
  public ConstructDesign design;
  public override IEnumerable<Gizmo> GetGizmos(){foreach(var g in base.GetGizmos()??Enumerable.Empty<Gizmo>())yield return g;if(pawn.Faction==Faction.OfPlayer)yield return RenameCommand();if(pawn.Faction==Faction.OfPlayer)yield return new Command_Action{defaultLabel="造物档案",defaultDesc="查看初始材料投入、质量与属性。",icon=Necro.Icon(kind),action=()=>Find.WindowStack.Add(new Dialog_ConstructDesign(this))};if(pawn.Faction==Faction.OfPlayer && !pawn.Dead)yield return new Command_Action{defaultLabel="死灵重塑",defaultDesc="到重塑池修改材料、技能兴趣和知识专长。保留经验、关系和植入体。",icon=Necro.Icon("Altar"),action=()=>Remolding.Open(this)};}
  public void ApplyDesign(HediffStage stage){if(design==null)return;AddOffset(stage,StatDefOf.ArmorRating_Sharp,design.Armor);AddOffset(stage,StatDefOf.ArmorRating_Blunt,design.Armor);stage.statFactors.Add(new StatModifier{stat=StatDefOf.MoveSpeed,value=design.Speed});foreach(var name in new[]{"WorkSpeedGlobal","GlobalLearningFactor"}){var stat=DefDatabase<StatDef>.GetNamedSilentFail(name);if(stat==null)continue;var modifier=stage.statFactors.FirstOrDefault(m=>m.stat==stat);if(modifier==null)stage.statFactors.Add(new StatModifier{stat=stat,value=design.Work});else modifier.value*=design.Work;}}
 }
 [HarmonyPatch(typeof(Pawn),"get_HealthScale")]
 public static class ConstructHealth {static void Postfix(Pawn __instance,ref float __result){var h=Necro.Construct(__instance);if(h==null)return;if(__instance.def==ThingDefOf.Human)__result*=h.LegacyRace.race.baseHealthScale;if(h.design!=null)__result*=h.design.Health;}}
 public partial class Dialog_ConstructDesign : Window {
  readonly ConstructSeed seed;readonly Hediff_Construct finished;readonly string kind;
  ConstructDesign plan;Vector2 scroll;
  public override Vector2 InitialSize=>new Vector2(940,660);
  public Dialog_ConstructDesign(ConstructSeed seed){this.seed=seed;kind=seed.kind;plan=seed.design.Copy();Setup();}
  public Dialog_ConstructDesign(Hediff_Construct finished){this.finished=finished;kind=finished.kind;plan=finished.design?.Copy()??new ConstructDesign{specialty=finished.specialty};Setup();}
  void Setup(){if(Editable)plan.LimitPassions(kind);forcePause=true;absorbInputAroundWindow=true;doCloseX=true;closeOnClickedOutside=false;}
  public override void DoWindowContents(Rect rect){
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(0,0,rect.width-40,38),IsRemolding?"死灵重塑 · 已有造物":finished==null?"死灵塑形 · 初始配置":"死灵造物 · 躯体档案");Text.Font=GameFont.Small;
   Widgets.Label(new Rect(0,42,rect.width,44),IsRemolding?"保留原角色、技能经验、关系和植入体。耗时1日；增加材料补差额，减少不返还，另消耗50生肉。":finished==null?"先塑造躯体，再赋予灵魂。调整投入即可预览结果；保存方案不扣材料。":"初始配置已固定，以下为本个体的材料与强化记录。");
   if(Widgets.ButtonText(new Rect(0,78,180,30),skillsTab?"躯体材料":"✓ 躯体材料"))skillsTab=false;
   if(Widgets.ButtonText(new Rect(192,78,180,30),skillsTab?"✓ 技能兴趣":"技能兴趣"))skillsTab=true;
   var outRect=new Rect(0,116,rect.width,rect.height-194);var view=new Rect(0,0,rect.width-20,skillsTab?Math.Max(390,74+((DefDatabase<SkillDef>.DefCount+1)/2)*52):440);Widgets.BeginScrollView(outRect,ref scroll,view);
   if(skillsTab)DrawPassions(view);else {
   float left=view.width*.57f,right=left+20,w=view.width-right;
   Widgets.DrawMenuSection(new Rect(0,0,left,430));Widgets.DrawMenuSection(new Rect(right,0,w,430));
   Widgets.Label(new Rect(16,12,left-32,32),Necro.Def<ThingDef>(kind).label+"  /  主人："+((seed?.master??finished?.master)?.LabelShort??"失联"));
   Widgets.Label(new Rect(16,46,left-32,36),finished!=null && finished.design==null?"旧版造物：未记录历史材料投入":"基础胚体：生肉 "+ConstructSeed.BaseMeat(kind)+"（已支付）");
   if(Editable){
    plan.meat=Material(16,90,left-32,"生肉 · 强化生命上限",plan.meat,600,25);
    plan.steel=Material(16,170,left-32,"钢铁 · 增加护甲，略降低移动速度",plan.steel,300,10);
    plan.plasteel=Material(16,250,left-32,"玻璃钢 · 精密骨架与魂能传导",plan.plasteel,150,5);
   }else Widgets.Label(new Rect(16,100,left-32,190),"追加生肉："+plan.meat+"\n\n追加钢铁："+plan.steel+"\n\n追加玻璃钢："+plan.plasteel);
   if(kind=="Scholar"){
    Widgets.Label(new Rect(16,340,left-32,24),"知识专长");for(int i=0;i<4;i++){int index=i;var r=new Rect(16+i*(left-32)/4,372,(left-40)/4,34);string label=(plan.specialty==ConstructDesign.Specialties[i]?"✓ ":"")+ConstructDesign.SpecialtyLabels[i];if(Widgets.ButtonText(r,label) && Editable){if(!plan.TrySetSpecialty(ConstructDesign.Specialties[index],kind,IsRemolding))Messages.Message(ConstructDesign.PassionLimitReason,MessageTypeDefOf.RejectInput,false);}}
   }else Widgets.Label(new Rect(16,350,left-32,60),kind=="Skeleton"?"低成本躯体：可以承担全部殖民者工作，并正常学习。":"攻坚躯体：可以承担全部殖民者工作。血肉维护与统御规则保持不变。");
   Widgets.Label(new Rect(right+16,12,w-32,240),plan.Summary(kind,!IsRemolding));
   Widgets.Label(new Rect(right+16,260,w-32,150),IsRemolding?RemoldDetails():"统御占用："+ConstructSeed.Control(kind)+"\n总生肉成本："+(ConstructSeed.BaseMeat(kind)+plan.meat)+"\n数值相对该类型的基础属性。\n知识死灵另享主人心灵敏感度加成。\n知识死灵技能固定，工作经验100%回馈主人。");
   }Widgets.EndScrollView();
   float bottom=rect.height-70;
   if(IsRemolding){DrawRemoldFooter(rect,bottom);return;}
   if(finished!=null){if(Widgets.ButtonText(new Rect(rect.width-140,bottom+32,140,34),"关闭"))Close();return;}
   var supplies=seed.Supplies();string reason=seed.Blocked(plan,supplies);
   Widgets.Label(new Rect(0,bottom-1,rect.width,30),reason??("可用材料：生肉 "+supplies.Where(ConstructSeed.RawMeat).Sum(t=>t.stackCount)+" / 钢铁 "+supplies.Where(t=>t.def==ThingDefOf.Steel).Sum(t=>t.stackCount)+" / 玻璃钢 "+supplies.Where(t=>t.def==ThingDefOf.Plasteel).Sum(t=>t.stackCount)));
   if(Widgets.ButtonText(new Rect(0,bottom+34,120,34),"基础配置"))plan=new ConstructDesign{specialty=plan.specialty};
   if(Widgets.ButtonText(new Rect(134,bottom+34,130,34),"保存方案")){seed.design=plan.Copy();Close();}
   Widgets.Label(new Rect(278,bottom+34,rect.width-470,38),"唤醒吸收12格内可达、未禁用的材料。");
   GUI.enabled=reason==null;if(Widgets.ButtonText(new Rect(rect.width-175,bottom+34,175,34),"投入材料并唤醒")){Pawn pawn;string error;if(seed.Awaken(plan,out pawn,out error)){Find.Selector.ClearSelection();Find.Selector.Select(pawn);Close();}else Messages.Message(error,MessageTypeDefOf.RejectInput,false);}GUI.enabled=true;
  }
  static int Material(float x,float y,float width,string label,int value,int max,int step){Widgets.Label(new Rect(x,y,width,25),label+"："+value);return Mathf.Clamp(Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(x,y+32,width,26),value,0,max)/step)*step,0,max);}
 }
}

