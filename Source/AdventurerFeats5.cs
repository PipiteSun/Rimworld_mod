using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.141.0 生产专长 8 个（Defs/Adventurer_Feats5.xml，规格 ArtSource/ProductionFeats-1.141.0-spec.md）：学以致用、名匠、符文锻师、炼金大师、筑城大师、丰收之手、外科圣手、造物之手。
 // 数值效果（建造 / 种植 / 手术的 statFactors、statOffsets）合进冒险者状态；规则效果在这里：
 // · 学以致用：AdventurerWork.Learned 倍率 ×1.5、每日全额 80 → 120（AdventurerWork 调 WorkMult / DailyFull）。
 // · 品质提档统一走 Feats5Quality（QualityUtility.GenerateQualityCreatedByPawn 后缀，Priority.Low）：名匠 30%、符文锻师 25%（只在三张锻台上）取较高的一个几率、只提一档；造物术的「至少优秀」最后应用；和器械师、熔岩之握、火巨人照常叠加。
 // · 工作台速度：StatPart_Feats5Bench 挂在 WorkSpeedGlobal 上（符文锻师三张锻台 ×1.4、炼金大师炼金桌 ×1.35）；修理：StatPart_Feats5Repair 挂在 ConstructionSpeed 上（筑城大师修理时 ×1.5）。
 // · 产品：Feats5Products（GenRecipe.MakeRecipeProducts 后缀）：炼金大师在炼金桌 30% 多一份第一个产品；符文锻师在锻台 20% 返还四分之一金属材料。
 // · 外科圣手：SurgeryOutcome_Death / SurgeryOutcome_Failure（伤害 > 20 或不限于手术部位的）的 Apply 前缀跳过，只剩原版的轻微失败。
 // · 丰收之手「催生」、造物之手「造物术」：能力 LP_FeatGrowth / LP_FeatFabricate（grantAbility 发放，usesPerDay 1，午夜恢复）。
 // 概率统一走 Feats2.TestRoll ?? Rand.Value。
 public static class Feats5 {
  public const float PracticedMult=1.5f,PracticedDaily=120f,ArtisanChance=.3f,RuneChance=.25f,RuneSpeed=1.4f,RuneRefund=.2f,RuneRefundPart=.25f,AlchemySpeed=1.35f,AlchemyExtra=.3f,RepairSpeed=1.5f,GrowthRadius=6f,GrowthAmount=.2f,FabricatePart=.5f,FabricateMax=60000f;
  public const int MinorSurgeryDamage=20;
  static LP_FeatDef F(ref LP_FeatDef c,string n)=>c??(c=DefDatabase<LP_FeatDef>.GetNamedSilentFail(n));
  static LP_FeatDef practiced,artisan,rune,alchemist,builder,green,surgeon,fabricate;
  public static LP_FeatDef PracticedHand=>F(ref practiced,"LP_Feat_PracticedHand");
  public static LP_FeatDef MasterArtisan=>F(ref artisan,"LP_Feat_MasterArtisan");
  public static LP_FeatDef RuneSmith=>F(ref rune,"LP_Feat_RuneSmith");
  public static LP_FeatDef MasterAlchemist=>F(ref alchemist,"LP_Feat_MasterAlchemist");
  public static LP_FeatDef MasterBuilder=>F(ref builder,"LP_Feat_MasterBuilder");
  public static LP_FeatDef GreenThumb=>F(ref green,"LP_Feat_GreenThumb");
  public static LP_FeatDef Surgeon=>F(ref surgeon,"LP_Feat_Surgeon");
  public static LP_FeatDef FabricateFeat=>F(ref fabricate,"LP_Feat_Fabricate");
  public static readonly string[] Names={"LP_Feat_PracticedHand","LP_Feat_MasterArtisan","LP_Feat_RuneSmith","LP_Feat_MasterAlchemist","LP_Feat_MasterBuilder","LP_Feat_GreenThumb","LP_Feat_Surgeon","LP_Feat_Fabricate"};
  static float Roll=>Feats2.TestRoll??Rand.Value;
  public static readonly HashSet<string> RuneBenches=new HashSet<string>{"LP_RuneAnvil","LP_GiantAnvil","LP_FireGiantForge"};
  public const string AlchemyBench="LP_AlchemyTable"; // LP_Alembic 是设施，只算炼金桌
  public static int Boosts,Floors,Extra,Refunds,Grown,Fabricated,Averted;
  static readonly Color Gold=new Color(.95f,.85f,.45f),Green=new Color(.5f,.85f,.4f),Violet=new Color(.7f,.6f,.9f);

  // 当前工作台：当前 job 的目标 A 是工作台时取 defName。
  public static string BenchOf(Pawn p){var t=p?.CurJob?.GetTarget(TargetIndex.A).Thing as Building_WorkTable;return t?.def?.defName;}
  public static bool AtRuneBench(Pawn p){var b=BenchOf(p);return b!=null && RuneBenches.Contains(b);}
  public static bool AtAlchemyBench(Pawn p)=>BenchOf(p)==AlchemyBench;

  // ---------- 学以致用 ----------
  public static float WorkMult(Pawn p)=>Feats.Has(p,PracticedHand)?PracticedMult:1f;
  public static float DailyFull(Pawn p)=>Feats.Has(p,PracticedHand)?PracticedDaily:AdventurerWork.DailyFull;

  // ---------- 品质提档（名匠 / 符文锻师 / 造物术） ----------
  public static float BoostChance(Pawn p){
   var h=Adventurer.Get(p);if(h==null || h.feats.Count==0)return 0f;float c=0f;
   if(h.feats.Contains(MasterArtisan))c=ArtisanChance;
   if(h.feats.Contains(RuneSmith) && AtRuneBench(p))c=Mathf.Max(c,RuneChance);
   return c;
  }
  public static QualityCategory Boost(Pawn p,QualityCategory q){
   if(p==null || !Adventurer.Enabled)return q;
   float c=BoostChance(p);
   if(c>0f && q<QualityCategory.Legendary && Roll<c){q++;Boosts++;}
   var fab=p.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_FeatFabricated"));
   if(fab!=null){if(q<QualityCategory.Excellent){q=QualityCategory.Excellent;Floors++;}p.health.RemoveHediff(fab);}
   if(q>=QualityCategory.Masterwork && Feats.Has(p,MasterArtisan)){var th=DefCache<ThoughtDef>.Get("LP_FeatMasterworkPride");if(th!=null)p.needs?.mood?.thoughts?.memories?.TryGainMemory(th);}
   return q;
  }

  // ---------- 丰收之手「催生」：6 格内所有植物生长 +20%，上限成熟 ----------
  public static int Growth(Pawn p){
   if(p?.Map==null || !Feats.Use(p,GreenThumb))return 0;var map=p.Map;int n=0;
   foreach(var cell in GenRadial.RadialCellsAround(p.Position,GrowthRadius,true)){
    if(!cell.InBounds(map))continue;var plant=cell.GetPlant(map);if(plant==null || plant.Growth>=1f)continue;
    plant.Growth=Mathf.Min(1f,plant.Growth+GrowthAmount);plant.DirtyMapMesh(map);n++;
    if(n%3==0)FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,1.2f,Green);
   }
   FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,Green);Items.Mote(p,"催生",Green);Grown+=n;return n;
  }

  // ---------- 造物之手「造物术」：正在做账单时立刻完成剩余工作量的一半（最多 60000），这件作品至少优秀 ----------
  public const string FabricateNeed="需要正在工作台前制作";
  static float WorkLeft(Pawn p,out JobDriver_DoBill d,out UnfinishedThing uft){
   d=p?.jobs?.curDriver as JobDriver_DoBill;uft=null;if(d==null)return 0f;
   uft=p.CurJob?.GetTarget(TargetIndex.B).Thing as UnfinishedThing;
   if(d.workLeft>0f)return d.workLeft;
   return uft!=null && uft.Initialized && uft.workLeft>0f?uft.workLeft:0f;
  }
  public static string FabricateReason(Pawn p){JobDriver_DoBill d;UnfinishedThing u;return WorkLeft(p,out d,out u)>0f?null:FabricateNeed;}
  public static float Fabricate(Pawn p){
   JobDriver_DoBill d;UnfinishedThing uft;float left=WorkLeft(p,out d,out uft);
   if(left<=0f || !Feats.Use(p,FabricateFeat))return 0f;
   float done=Mathf.Min(left*FabricatePart,FabricateMax);
   if(d.workLeft>0f){d.workLeft=Mathf.Max(0f,d.workLeft-done);if(uft!=null)uft.workLeft=d.workLeft;}
   else uft.workLeft=Mathf.Max(0f,uft.workLeft-done);
   Items.Give(p,"LP_FeatFabricated");Fabricated++;Items.Mote(p,"造物术",Gold);
   if(p.Map!=null)FleckMaker.ThrowLightningGlow(p.DrawPos,p.Map,1.5f);
   return done;
  }

  // ---------- 产品（MakeRecipeProducts 后缀） ----------
  public static List<Thing> ExtraProducts(RecipeDef r,Pawn p,List<Thing> ingredients,IEnumerable<Thing> made){
   List<Thing> list=null;
   if(Feats.Has(p,MasterAlchemist) && AtAlchemyBench(p) && r.products!=null && r.products.Count>0 && Roll<AlchemyExtra){
    var pc=r.products[0];var first=made.FirstOrDefault(t=>t.def==pc.thingDef);
    if(pc.thingDef!=null && pc.count>0){
     var extra=ThingMaker.MakeThing(pc.thingDef,pc.thingDef.MadeFromStuff?(first?.Stuff??GenStuff.DefaultStuffFor(pc.thingDef)):null);
     extra.stackCount=Mathf.Min(pc.count,Mathf.Max(1,pc.thingDef.stackLimit));
     var cq=extra.TryGetComp<CompQuality>();if(cq!=null)cq.SetQuality(first?.TryGetComp<CompQuality>()?.Quality??QualityCategory.Normal,ArtGenerationContext.Colony);
     if(list==null)list=made.ToList();list.Add(extra);Extra++;Items.Mote(p,"炼金大师",Violet);
    }
   }
   if(Feats.Has(p,RuneSmith) && AtRuneBench(p) && ingredients!=null && Roll<RuneRefund){
    foreach(var ing in ingredients){
     if(ing?.def==null || !ing.def.IsStuff || ing.def.stuffProps?.categories?.Contains(StuffCategoryDefOf.Metallic)!=true)continue;
     int n=Mathf.FloorToInt(ing.stackCount*RuneRefundPart);if(n<1)continue;
     var back=ThingMaker.MakeThing(ing.def);back.stackCount=n;
     if(list==null)list=made.ToList();list.Add(back);Refunds+=n;
    }
    if(list!=null)Items.Mote(p,"符文锻师",Gold);
   }
   return list;
  }
 }

 // ---------- 前置 ----------
 public class FeatWorker_MasterArtisan : FeatWorker {
  public override string ExtraPrereq=>"敏捷或智力 13";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(h.scores[Adventurer.Dex]>=13 || h.scores[Adventurer.Int]>=13?null:"需要敏捷或智力 13");
 }
 public class FeatWorker_RuneSmith : FeatWorker {
  public override string ExtraPrereq=>"力量或智力 13";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(h.scores[Adventurer.Str]>=13 || h.scores[Adventurer.Int]>=13?null:"需要力量或智力 13");
 }
 public class FeatWorker_MasterAlchemist : FeatWorker {
  public override string ExtraPrereq=>"手工或医疗 6";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(p?.skills?.GetSkill(SkillDefOf.Crafting)?.Level>=6 || p?.skills?.GetSkill(SkillDefOf.Medicine)?.Level>=6?null:"需要手工或医疗 6");
 }
 public class FeatWorker_Fabricate : FeatWorker {
  public override string ExtraPrereq=>"已学名匠";
  public override string Unmet(Pawn p,Hediff_Adventurer h)=>base.Unmet(p,h)??(h.HasFeat(Feats5.MasterArtisan)?null:"需要先学名匠");
 }

 // ---------- 能力 ----------
 public class CompProperties_Feat5 : CompProperties_AbilityEffect {public string effect;public CompProperties_Feat5(){compClass=typeof(CompAbilityEffect_Feat5);}}
 public class CompAbilityEffect_Feat5 : CompAbilityEffect {
  string Effect=>((CompProperties_Feat5)props).effect;
  LP_FeatDef Feat=>Effect=="growth"?Feats5.GreenThumb:Feats5.FabricateFeat;
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   base.Apply(target,dest);var p=parent.pawn;
   if(Effect=="growth")Feats5.Growth(p);else if(Effect=="fabricate")Feats5.Fabricate(p);
  }
  public override bool GizmoDisabled(out string reason){
   var p=parent.pawn;var f=Feat;var h=Adventurer.Get(p);
   if(h==null || f==null || !Feats.Has(p,f)){reason="没有"+(f?.label??"对应")+"专长。";return true;}
   if(h.UsesLeft(f)<=0){reason="今天的次数用完了（午夜恢复）。";return true;}
   if(Effect=="fabricate"){var why=Feats5.FabricateReason(p);if(why!=null){reason=why;return true;}}
   return base.GizmoDisabled(out reason);
  }
 }
 // 造物术不走施法 job（会打断正在做的账单）：按下按钮直接生效。
 public class Ability_Fabricate : RimWorld.Ability {
  public Ability_Fabricate():base(){}
  public Ability_Fabricate(Pawn pawn):base(pawn){}
  public Ability_Fabricate(Pawn pawn,AbilityDef def):base(pawn,def){}
  public Ability_Fabricate(Pawn pawn,Precept sourcePrecept):base(pawn,sourcePrecept){}
  public Ability_Fabricate(Pawn pawn,Precept sourcePrecept,AbilityDef def):base(pawn,sourcePrecept,def){}
  public override void QueueCastingJob(LocalTargetInfo target,LocalTargetInfo destination){
   var why=Feats5.FabricateReason(pawn);
   if(why!=null){Messages.Message(why+"。",pawn,MessageTypeDefOf.RejectInput,false);return;}
   if(Feats5.Fabricate(pawn)<=0f)Messages.Message("今天的次数用完了（午夜恢复）。",pawn,MessageTypeDefOf.RejectInput,false);
  }
 }

 // ---------- 工作台速度 / 修理速度 ----------
 public class StatPart_Feats5Bench : StatPart {
  public static float Factor(Pawn p){
   if(p?.CurJob==null)return 1f;
   bool rune=Feats.Has(p,Feats5.RuneSmith),alch=Feats.Has(p,Feats5.MasterAlchemist);if(!rune && !alch)return 1f;
   var b=Feats5.BenchOf(p);if(b==null)return 1f;
   if(rune && Feats5.RuneBenches.Contains(b))return Feats5.RuneSpeed;
   if(alch && b==Feats5.AlchemyBench)return Feats5.AlchemySpeed;
   return 1f;
  }
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p){float f=Factor(p);if(f!=1f)val*=f;}}
  public override string ExplanationPart(StatRequest req){if(!(req.Thing is Pawn p))return null;float f=Factor(p);return f==1f?null:(f==Feats5.RuneSpeed?"符文锻师（锻台）":"炼金大师（炼金桌）")+"：×"+(f*100f).ToString("0")+"%";}
 }
 public class StatPart_Feats5Repair : StatPart {
  public static float Factor(Pawn p)=>p?.jobs?.curDriver is JobDriver_Repair && Feats.Has(p,Feats5.MasterBuilder)?Feats5.RepairSpeed:1f;
  public override void TransformValue(StatRequest req,ref float val){if(req.Thing is Pawn p){float f=Factor(p);if(f!=1f)val*=f;}}
  public override string ExplanationPart(StatRequest req)=>req.Thing is Pawn p && Factor(p)!=1f?"筑城大师（修理）：×"+(Feats5.RepairSpeed*100f).ToString("0")+"%":null;
 }
 [StaticConstructorOnStartup]
 public static class Feats5Startup {
  static Feats5Startup(){
   Add(StatDefOf.WorkSpeedGlobal,new StatPart_Feats5Bench());
   Add(StatDefOf.ConstructionSpeed,new StatPart_Feats5Repair());
  }
  static void Add(StatDef s,StatPart part){
   if(s==null)return;if(s.parts==null)s.parts=new List<StatPart>();
   if(s.parts.Any(x=>x.GetType()==part.GetType()))return;
   part.parentStat=s;s.parts.Add(part);s.immutable=false;
  }
 }

 // ==================== Harmony ====================
 // 品质：名匠 / 符文锻师提一档，造物术的下限（Priority.Low，在器械师、火巨人之后）。
 [HarmonyPatch]
 public static class Feats5Quality {
  public static System.Reflection.MethodBase TargetMethod()=>AccessTools.GetDeclaredMethods(typeof(QualityUtility)).Find(m=>m.Name==nameof(QualityUtility.GenerateQualityCreatedByPawn) && m.GetParameters().Length>=2 && m.GetParameters()[0].ParameterType==typeof(Pawn) && m.GetParameters()[1].ParameterType==typeof(SkillDef));
  [HarmonyPriority(Priority.Low)]
  static void Postfix(Pawn __0,ref QualityCategory __result){if(__0!=null)__result=Feats5.Boost(__0,__result);}
 }
 // 产品：炼金大师多一份、符文锻师返还金属。
 [HarmonyPatch(typeof(GenRecipe),nameof(GenRecipe.MakeRecipeProducts))]
 public static class Feats5Products {
  static void Postfix(RecipeDef __0,Pawn __1,List<Thing> __2,ref IEnumerable<Thing> __result){
   if(__1==null || __0==null || !Adventurer.Enabled)return;
   if(!Feats.Has(__1,Feats5.MasterAlchemist) && !Feats.Has(__1,Feats5.RuneSmith))return;
   var list=Feats5.ExtraProducts(__0,__1,__2,__result);if(list!=null)__result=list;
  }
 }
 // 外科圣手：手术失败不会致命、不会是灾难性的（只剩原版 20 点伤害、限于手术部位的轻微失败）。
 [HarmonyPatch(typeof(SurgeryOutcome_Death),"Apply")]
 public static class Feats5NoFatalSurgery {
  static bool Prefix(Pawn __2,ref bool __result){
   if(!Feats.Has(__2,Feats5.Surgeon))return true;
   __result=false;Feats5.Averted++;return false;
  }
 }
 [HarmonyPatch(typeof(SurgeryOutcome_Failure),"Apply")]
 public static class Feats5NoCatastrophicSurgery {
  static readonly AccessTools.FieldRef<SurgeryOutcome,int> Damage=AccessTools.FieldRefAccess<SurgeryOutcome,int>("totalDamage");
  static readonly AccessTools.FieldRef<SurgeryOutcome,bool> ToPart=AccessTools.FieldRefAccess<SurgeryOutcome,bool>("applyEffectsToPart");
  public static bool Severe(SurgeryOutcome o)=>Damage(o)>Feats5.MinorSurgeryDamage || !ToPart(o);
  static bool Prefix(SurgeryOutcome_Failure __instance,Pawn __2,ref bool __result){
   if(!Severe(__instance) || !Feats.Has(__2,Feats5.Surgeon))return true;
   __result=false;Feats5.Averted++;return false;
  }
 }
}
