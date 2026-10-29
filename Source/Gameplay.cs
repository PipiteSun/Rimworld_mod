using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;
namespace LichPhylactery {
 public partial class Hediff_Construct : Hediff {
  public Pawn master;
  public string kind="Skeleton",specialty="";
  public float flesh=100;
  public bool dormant;
  int accumulated;
  public int Cost=>kind=="Skeleton"?1:kind=="Golem"?5:kind=="StoneGolem"?4:3; // 1.45.0 石魔像：统御4，每天维护0.5，没有血肉储备
  public float Upkeep=>kind=="Skeleton"?.3f:kind=="Golem"?1f:kind=="StoneGolem"?.5f:1.5f;
  public override bool ShouldRemove=>false;
  public bool HasControl {
   get {
    RegisterForControl();if(raidBound)return RaidControl;var lich=Necro.Lich(master);var list=Necro.Registry?.constructs;
    if(lich==null || !lich.Active || master.Faction!=pawn.Faction || lich.Points<=.01f || list==null)return false;
    // Equivalent to allocating in ThingID order, without sorting on every XP event.
    int used=0;bool found=false;
    for(int i=0;i<list.Count;i++){var p=list[i];if(p==null || p.Destroyed || p.Dead || p.thingIDNumber>pawn.thingIDNumber)continue;var h=Necro.Construct(p);if(h?.master!=master)continue;used+=h.Cost;if(p==pawn)found=true;}
    return found && used<=lich.box.Capacity;
   }
  }
  public override string LabelInBrackets=>(dormant?"休眠；":"")+(kind=="Golem"?"血肉 "+flesh.ToString("0.#")+" / 500；":"")+"统御 "+Cost;
  public override string TipStringExtra=>"主人："+(master?.LabelShort??"失联")+"\n精神力维护："+Upkeep+"点/天。"+(kind=="Golem"?"\n血肉储备每天消耗25，修复每点伤害额外消耗2；低于25停止自愈，耗尽时行动与操作减半。":"")+(kind=="Scholar"?"\n专长："+ConstructDesign.SpecialtyName(specialty)+"。学习与工作倍率 "+Necro.Mentoring(master).ToString("0.00")+"；技能由自身正常积累。":"")+FeedbackDescription;
  HediffStage cachedStage;
  (string kind,bool human,bool dormant,bool starved,bool designed,float armor,float speed,float work,float mentoring) cachedStageKey;
  public override HediffStage CurStage {
   get {
    float mentoring=kind=="Scholar"?Mentoring():1f;
    var key=(kind,pawn.def==ThingDefOf.Human,dormant,kind=="Golem" && flesh<=0,design!=null,design?.Armor??0f,design?.Speed??1f,design?.Work??1f,mentoring);
    if(cachedStage!=null && cachedStageKey.Equals(key))return cachedStage;
    var s=new HediffStage{statFactors=new List<StatModifier>(),capMods=new List<PawnCapacityModifier>()};
    if(dormant)s.capMods.Add(new PawnCapacityModifier{capacity=PawnCapacityDefOf.Consciousness,setMax=.1f});
    if(kind=="Golem" && flesh<=0){s.capMods.Add(new PawnCapacityModifier{capacity=PawnCapacityDefOf.Moving,postFactor=.5f});s.capMods.Add(new PawnCapacityModifier{capacity=PawnCapacityDefOf.Manipulation,postFactor=.5f});}
    if(kind=="Scholar")foreach(var name in new[]{"WorkSpeedGlobal","GlobalLearningFactor"}){var stat=DefDatabase<StatDef>.GetNamedSilentFail(name);if(stat!=null)s.statFactors.Add(new StatModifier{stat=stat,value=mentoring});}
    ApplyHumanRole(s);ApplyDesign(s);cachedStageKey=key;return cachedStage=s;
   }
  }
  // 1.58.6：学者的导师系数要算一次主人的心灵敏感度；原来每次读 CurStage（每次属性计算两次）都算一遍，现在同一刻只算一次。
  float mentorValue;int mentorTick=int.MinValue;Pawn mentorOf;
  float Mentoring(){int now=Find.TickManager?.TicksGame??0;if(mentorTick!=now || mentorOf!=master){mentorValue=Necro.Mentoring(master);mentorTick=now;mentorOf=master;}return mentorValue;}
  public override void TickInterval(int delta){base.TickInterval(delta);accumulated+=delta;if(accumulated>=250){Maintain(accumulated);accumulated=0;}}
  public void Maintain(int elapsed){EnforcePassionLimit();bool oldDormant=dormant,starved=flesh<=0;dormant=!HasControl;if(kind=="Golem"){
   flesh=Math.Max(0,flesh-25f*elapsed/60000f);
   var injury=pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().OrderByDescending(i=>i.Severity).FirstOrDefault();
   if(!dormant && flesh>=25 && injury!=null){float heal=Math.Min(injury.Severity,Math.Min(elapsed/125f,(flesh-25f)/2));if(heal>0){injury.Heal(heal);flesh-=heal*2;}}
  }if(oldDormant!=dormant || starved!=(flesh<=0)){pawn.health.Notify_HediffChanged(this);pawn.health.CheckForStateChange(null,this);}
  }
  public override void ExposeData(){base.ExposeData();if(Scribe.mode==LoadSaveMode.PostLoadInit)HediffIndex<Hediff_Construct>.Add(this);Scribe_References.Look(ref master,"master",true);Scribe_Values.Look(ref kind,"kind","Skeleton");Scribe_Values.Look(ref specialty,"specialty","");Scribe_Values.Look(ref flesh,"flesh",100);Scribe_Values.Look(ref dormant,"dormant");Scribe_Values.Look(ref raidBound,"raidBound");Scribe_Values.Look(ref accumulated,"maintenanceTicks");Scribe_Deep.Look(ref design,"constructDesign");Scribe_Values.Look(ref colonistVersion,"colonistVersion",0);Scribe_Values.Look(ref appearanceVersion,"appearanceVersion",0);Scribe_Values.Look(ref legacyThingID,"legacyThingID");Scribe_Values.Look(ref knowledgeReturned,"knowledgeReturned",0d);Scribe_Deep.Look(ref remoldDraft,"remoldDraft");Scribe_Collections.Look(ref savedWorkPriorities,"savedWorkPriorities",LookMode.Def,LookMode.Value);}
 }
 public static class Constructs {
  public static Pawn Create(string kind,Pawn master,IntVec3 at,Map map,string specialty="",ConstructDesign design=null){
   if(kind!="Scholar")specialty="";
   if(design!=null && design.MajorCount(kind)>ConstructDesign.MajorPassionLimit)throw new ArgumentException(ConstructDesign.PassionLimitReason);
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(Necro.Def<PawnKindDef>(kind),Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,forceNoBackstory:true,fixedBiologicalAge:25,fixedChronologicalAge:25,forcedXenotype:XenotypeDefOf.Baseliner));
   try {
   p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();p.inventory?.innerContainer.ClearAndDestroyContents();
   foreach(var t in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(t);
   var h=(Hediff_Construct)p.health.AddHediff(Necro.Def<HediffDef>("Construct"));h.kind=kind;h.master=master;h.specialty=specialty;h.design=design;h.flesh=kind=="Golem"?200:0;
   p.Name=new NameSingle(kind=="Skeleton"?"骷髅仆役":kind=="Golem"?"血肉巨像":kind=="StoneGolem"?"石魔像":"知识死灵·"+ConstructDesign.SpecialtyName(specialty));
   foreach(var sk in p.skills.skills){sk.Level=kind=="Scholar"?4:0;sk.passion=Passion.None;sk.xpSinceLastLevel=0;}
   if(kind=="Golem" || kind=="StoneGolem")p.skills.GetSkill(SkillDefOf.Melee).Level=10+(design?.SkillBonus??0);
   if(kind=="Scholar"){
    var skill=specialty=="大脑"?SkillDefOf.Intellectual:specialty=="眼睛"?SkillDefOf.Crafting:specialty=="手臂"?SkillDefOf.Construction:SkillDefOf.Medicine;
    p.skills.GetSkill(skill).Level=10+(design?.SkillBonus??0);p.skills.GetSkill(skill).passion=Passion.Major;
   }
   foreach(var sk in p.skills.skills)sk.passion=design?.PassionFor(sk.def,kind)??sk.passion;
   h.UpgradeColonist();p.health.Notify_HediffChanged(h);Necro.Registry.RegisterConstruct(p);GenSpawn.Spawn(p,at,map);Necro.Refresh(p);
   p.workSettings.EnableAndInitialize();foreach(var w in DefDatabase<WorkTypeDef>.AllDefsListForReading)p.workSettings.SetPriority(w,Allowed(p,w)?3:0);
   IdeoEvents.Record("LP_HE_ConstructRaised",master); // 1.56.0 文化：亡灵观
   return p;
   }catch{Necro.Registry?.constructs.Remove(p);if(!p.Destroyed)p.Destroy();throw;}
  }
  public static bool Allowed(Pawn p,WorkTypeDef w)=>!p.WorkTypeIsDisabled(w);
 }
 public class ConstructNode : PawnRenderNode {
  public ConstructNode(Pawn p,PawnRenderNodeProperties props,PawnRenderTree tree):base(p,props,tree){}
  public override Graphic GraphicFor(Pawn p){string kind=p.def.defName.Replace("LP_","");return GraphicDatabase.Get<Graphic_Multi>("Necromancy/"+kind,ShaderDatabase.Cutout,Vector2.one,Color.white);}
 }
 public partial class Building_Altar : Building_WorkTable {
  public Phylactery selected;
  public Phylactery Box=>selected!=null && !selected.Destroyed && selected.MapHeld==Map && selected.PositionHeld.DistanceTo(Position)<=12?selected:null;
  public override void ExposeData(){base.ExposeData();Scribe_References.Look(ref selected,"selectedPhylactery");Scribe_Values.Look(ref fleshRecipeVersion,"fleshRecipeVersion",0);}
  public override string GetInspectString(){string text=base.GetInspectString();return (string.IsNullOrWhiteSpace(text)?"":text.TrimEnd()+"\n")+"服务命匣："+(Box?.soul?.LabelShort??"未选择或距离超过12格")+"\n巫妖仪式绑定工作单执行者。";}
  public override IEnumerable<Gizmo> GetGizmos(){foreach(var g in base.GetGizmos())yield return g;yield return new Command_Action{defaultLabel="选择服务命匣",defaultDesc="献祭、造物胚体与补给服务于此命匣。命匣必须留在仪式台12格内；不同巫妖可使用不同仪式台。",icon=Necro.Icon("Phylactery"),action=()=>Find.WindowStack.Add(new FloatMenu((Necro.Registry?.boxes??new List<Phylactery>()).Where(b=>b!=null && !b.Destroyed && b.MapHeld==Map && b.PositionHeld.DistanceTo(Position)<=12).Select(b=>new FloatMenuOption((b.soul?.LabelShort??"无主")+" · 等级"+b.level,()=>selected=b)).DefaultIfEmpty(new FloatMenuOption("12格内没有命匣",null)).ToList()))};}
 }
 public class RitualCount : IngredientValueGetter_Volume { public override float ValuePerUnitOf(ThingDef def)=>1f; }
 public class RitualRecipe : RecipeWorker {
  public override void ConsumeIngredient(Thing ingredient,RecipeDef recipe,Map map){if(ingredient is Corpse corpse && corpse.InnerPawn!=null)corpse.Strip(true);base.ConsumeIngredient(ingredient,recipe,map);}
  public override bool AvailableOnNow(Thing thing,BodyPartRecord part=null){if(!(thing is Building_Altar altar))return base.AvailableOnNow(thing,part);if(recipe.defName=="LP_Ascend")return true;return Rituals.Blocked(recipe,altar,null)==null;}
 }
 public static class Rituals {
  public static string Blocked(RecipeDef recipe,Building_Altar altar,Pawn worker,List<Thing> ingredients=null){
   if(altar==null || !altar.Spawned)return "需要已建成的死灵仪式台。";
   string name=recipe.defName;
   if(name=="LP_Ascend")return worker==null?null:worker.genes==null || !worker.RaceProps.Humanlike || Necro.Undead(worker)?"执行者必须是尚未转化的人形殖民者。":worker.GetStatValue(StatDefOf.PsychicSensitivity)<=0?"心灵敏感度必须大于0。":null;
   if(name.StartsWith("LP_Harvest"))return "器官封存已停用；知识死灵改用生肉胚体培育。";
   var box=altar.Box;if(box?.soul==null)return "先选择12格内的命匣。";
   if(name=="LP_Rebuild")return "死灵之躯会在死亡3日后自动复生，无须此工作单。";
   var lich=Necro.Lich(box.soul);if(lich==null || !lich.Active)return "命匣主人必须存活并已绑定。";
   if(name=="LP_Offer"){
    if(box.level>=LichMod.Settings.levelCap)return "命匣已达当前等级上限。";
    if(ingredients!=null && ingredients.Any(t=>!ConstructSeed.RawMeat(t)))return "献祭只接受新鲜生肉。";
   }
   if(name=="LP_OfferSoul"){
    if(box.level>=LichMod.Settings.levelCap)return "命匣已达当前等级上限。";
    if(ingredients!=null){var corpses=ingredients.OfType<Corpse>().ToList();if(corpses.Count!=1)return "汲取灵魂需要一具完整的人形尸体。";string unfit=Souls.Unfit(corpses[0]);if(unfit!=null)return unfit;}
   }
   if(name=="LP_Refill" && !Necro.LivingConstructs(box.soul).Any(p=>p.Map==altar.Map && Necro.Construct(p).kind=="Golem" && Necro.Construct(p).flesh<=400))return "同地图没有需要至少100血肉补给的巨像。";
   // 1.45.0 石魔像：命匣主人要读过魔像手册，胚体用石块。
   if(name=="LP_MakeStoneGolem"){if(!Items.Has(box.soul,"LP_GolemLore"))return "命匣主人还没读过魔像手册。";if(ingredients!=null && ingredients.Any(t=>!t.def.IsWithinCategory(ThingCategoryDefOf.StoneBlocks)))return "石魔像胚体只用石块。";return null;}
   // 1.45.0 魂石：把封着的灵魂献给命匣。
   if(name=="LP_OfferSpiritStone"){if(box.level>=LichMod.Settings.levelCap)return "命匣已达当前等级上限。";if(ingredients!=null && !ingredients.Any(t=>SpiritStones.Soul(t)!=null))return "需要一颗封着灵魂的魂石。";return null;}
   // 1.45.1 死灵仪式台锻甲：消耗命匣攒下的献祭经验。
   var forge=recipe.GetModExtension<AltarForgeExtension>();if(forge!=null && box.experience+.001f<forge.experience)return "命匣攒下的献祭经验不足"+forge.experience.ToString("0")+"（当前"+box.experience.ToString("0")+"）。";
   if(name.StartsWith("LP_Make") && ingredients!=null && ingredients.Any(t=>!ConstructSeed.RawMeat(t)))return "造物胚体只使用新鲜生肉。";
   return null;
  }
  public static Phylactery Ascend(Pawn p){
   var box=(Phylactery)ThingMaker.MakeThing(Necro.Def<ThingDef>("Phylactery"));box.soul=p;
   var h=(Hediff_Lich)p.health.AddHediff(Necro.Def<HediffDef>("Lich"));h.box=box;
   h.originalEndogenes=p.genes.Endogenes.Select(g=>g.def).ToList();h.originalXenogenes=p.genes.Xenogenes.Select(g=>g.def).ToList();
   if(!p.genes.GenesListForReading.Any(g=>g.def==Necro.Def<GeneDef>("LichGene")))p.genes.AddGene(Necro.Def<GeneDef>("LichGene"),true);
   if(p.GetPsylinkLevel()<1)p.ChangePsylinkLevel(1,false);
   p.abilities.GainAbility(Necro.Def<AbilityDef>("MendConstruct"));
   h.UpgradeNecromancyGenes();box.Register();Necro.Refresh(p);h.ClampFocus();return box;
  }
  public static IEnumerable<Thing> Complete(RecipeDef recipe,Pawn worker,List<Thing> ingredients,Building_Altar altar){
   string blocked=Blocked(recipe,altar,worker,ingredients);if(blocked!=null)throw new InvalidOperationException("Ritual completion was not validated: "+blocked);
   string name=recipe.defName;
   if(name=="LP_Ascend"){Phylactery box=Ascend(worker);altar.selected=box;yield return box;yield break;}
   if(name.StartsWith("LP_Harvest")){yield return ThingMaker.MakeThing(Necro.Def<ThingDef>(name.Substring(3)));yield break;}
   var selected=altar.Box;
   if(name=="LP_Offer")selected.Feed(ingredients.Where(ConstructSeed.RawMeat).Sum(t=>t.stackCount));
   else if(name=="LP_OfferSoul"){Pawn victim=ingredients.OfType<Corpse>().First().InnerPawn;float gained=Souls.Value(victim);selected.Feed(gained);IdeoEvents.Record("LP_HE_SoulDrained",worker);Messages.Message(victim.LabelShort+"的灵魂被汲入命匣：+"+gained.ToString("0")+"献祭经验。",MessageTypeDefOf.PositiveEvent,false);}
   else if(name=="LP_Rebuild"){if(!selected.Rebuild(altar))throw new InvalidOperationException("Resurrection failed");}
   else if(name=="LP_Refill"){var target=Necro.LivingConstructs(selected.soul).Where(p=>p.Map==altar.Map && Necro.Construct(p).kind=="Golem" && Necro.Construct(p).flesh<=400).OrderBy(p=>Necro.Construct(p).flesh).First();Necro.Construct(target).flesh+=100;target.health.Notify_HediffChanged(Necro.Construct(target));}
   else if(name=="LP_MakeSkeleton")yield return ConstructSeed.Make("Skeleton",selected.soul);
   else if(name=="LP_MakeGolem")yield return ConstructSeed.Make("Golem",selected.soul);
   else if(name=="LP_MakeStoneGolem")yield return ConstructSeed.Make("StoneGolem",selected.soul);
   else if(recipe.GetModExtension<AltarForgeExtension>() is AltarForgeExtension fx){selected.experience=Math.Max(0f,selected.experience-fx.experience);yield return ThingMaker.MakeThing(fx.product,fx.product.MadeFromStuff?(fx.stuff??GenStuff.DefaultStuffFor(fx.product)):null);}
   else if(name=="LP_OfferSpiritStone"){var soul=SpiritStones.Soul(ingredients.First(t=>SpiritStones.Soul(t)!=null));float gained=Souls.Value(soul);selected.Feed(gained);Messages.Message(soul.LabelShort+"的灵魂从魂石里被汲入命匣：+"+gained.ToString("0")+"献祭经验。",MessageTypeDefOf.PositiveEvent,false);}
   else if(name.StartsWith("LP_MakeScholar")){string type=name.Substring("LP_MakeScholar".Length);yield return ConstructSeed.Make("Scholar",selected.soul,type=="Brain"?"大脑":type=="Eye"?"眼睛":type=="Arm"?"手臂":"心脏");}
  }
 }
 [HarmonyPatch(typeof(GenRecipe),nameof(GenRecipe.MakeRecipeProducts))]
 public static class RitualProducts {
  static bool Prefix(RecipeDef recipeDef,Pawn worker,List<Thing> ingredients,IBillGiver billGiver,ref IEnumerable<Thing> __result){if(!(recipeDef.Worker is RitualRecipe))return true;__result=Rituals.Complete(recipeDef,worker,ingredients,billGiver as Building_Altar);return false;}
 }
 [HarmonyPatch(typeof(Toils_Recipe),nameof(Toils_Recipe.FinishRecipeAndStartStoringProduct))]
 public static class ValidateRitualCompletion {
  static void Postfix(Toil __result){var toil=__result;var original=toil.initAction;toil.initAction=()=>{Pawn p=toil.actor;var job=p.CurJob;if(job?.bill?.recipe?.Worker is RitualRecipe){var altar=job.targetA.Thing as Building_Altar;var ingredients=job.placedThings?.Select(x=>x.thing).ToList();string reason=Rituals.Blocked(job.bill.recipe,altar,p,ingredients);if(reason!=null){Messages.Message(reason,MessageTypeDefOf.RejectInput,false);p.jobs.EndCurrentJob(JobCondition.Incompletable);return;}}original();};}
 }
 [HarmonyPatch(typeof(Bill),nameof(Bill.PawnAllowedToStartAnew))]
 public static class RitualWorkerAllowed {static void Postfix(Bill __instance,Pawn p,ref bool __result){if(__result && __instance.recipe.Worker is RitualRecipe)__result=Rituals.Blocked(__instance.recipe,__instance.billStack.billGiver as Building_Altar,p)==null;}}
 [HarmonyPatch(typeof(Bill),nameof(Bill.IsFixedOrAllowedIngredient),new[]{typeof(Thing)})]
 public static class RitualIngredientAllowed {
  static void Postfix(Bill __instance,Thing __0,ref bool __result){if(!__result || !(__instance.recipe.Worker is RitualRecipe))return;string name=__instance.recipe.defName;if(name=="LP_MakeStoneGolem")__result=__0.def.IsWithinCategory(ThingCategoryDefOf.StoneBlocks);else if(name=="LP_OfferSpiritStone")__result=SpiritStones.Soul(__0)!=null;else if(name=="LP_Offer" || name.StartsWith("LP_Make") || name=="LP_Refill")__result=ConstructSeed.RawMeat(__0);else if(name=="LP_OfferSoul")__result=Souls.Unfit(__0)==null;}

 }
 public class LichPsycast : Psycast {
  public LichPsycast(Pawn p):base(p){}public LichPsycast(Pawn p,AbilityDef d):base(p,d){}
  public override bool GizmoDisabled(out string reason){var h=Necro.Lich(pawn);if(h==null || !h.Active){reason="需要有效绑定的命匣。";return true;}return base.GizmoDisabled(out reason);}
  public override bool Activate(LocalTargetInfo target,LocalTargetInfo dest){var h=Necro.Lich(pawn);if(h==null || !h.Active || OnCooldown || !pawn.Spawned || pawn.Dead || pawn.Downed || !pawn.Awake() || pawn.InMentalState || !verb.CanHitTarget(target) || !EffectComps.All(c=>c.Valid(target,false)) || pawn.psychicEntropy.CurrentPsyfocus+.00001f<FinalPsyfocusCost(target) || pawn.psychicEntropy.WouldOverflowEntropy(def.EntropyGain*pawn.GetStatValue(StatDefOf.PsychicEntropyGain)))return false;return base.Activate(target,dest);}
 }
 [HarmonyPatch(typeof(Ability),nameof(Ability.FinalPsyfocusCost))]
 public static class LichPowerCosts {static void Postfix(Ability __instance,ref float __result){var h=Necro.Lich(__instance.pawn);if(h!=null && h.Active)__result*=100f/h.Power;}}
 public class CompProperties_Necromancy : CompProperties_AbilityEffect {public string effect;public CompProperties_Necromancy(){compClass=typeof(CompNecromancy);}}
 public class CompNecromancy : CompAbilityEffect {
  string Effect=>((CompProperties_Necromancy)props).effect;
  public override bool Valid(LocalTargetInfo target,bool throwMessages=false){Pawn p=target.Pawn;bool valid=p!=null && !p.Dead && p.Spawned && p.Map==parent.pawn.Map && (Effect=="bolt"?p.HostileTo(parent.pawn):Necro.Construct(p)?.master==parent.pawn && p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any());if(!valid && throwMessages)Messages.Message(Effect=="bolt"?"需要敌对的存活生物。":"需要自己的受伤死灵造物。",MessageTypeDefOf.RejectInput,false);return valid;}
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){if(!Valid(target))return;Pawn p=target.Pawn;float scale=Necro.Mentoring(parent.pawn);if(Effect=="bolt")p.TakeDamage(new DamageInfo(DamageDefOf.Burn,12f*scale,.2f,-1,parent.pawn));else {float budget=15f*Math.Max(0,parent.pawn.GetStatValue(StatDefOf.PsychicSensitivity));foreach(var h in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList()){float heal=Math.Min(budget,h.Severity);h.Heal(heal);budget-=heal;if(budget<=0)break;}}FleckMaker.Static(target.Cell,parent.pawn.Map,FleckDefOf.PsycastAreaEffect,1f);}
 }
}
