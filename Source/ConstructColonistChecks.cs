using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
namespace LichPhylactery {
 public partial class LichChecks {
  static Pawn surgerySubject;
  static bool DeterministicSurgery(Pawn patient,ref bool __result){if(patient!=surgerySubject)return true;__result=false;return false;}
  void CheckConstructColonist(Pawn p){
   CheckConstructInfoAndMood(p);
   var role=Necro.Construct(p).kind;
   var appearance=Hediff_Construct.AppearanceGenesFor(role);
   Check(p.IsColonist && p.IsColonistPlayerControlled,"construct is a native player-controlled colonist");
   Check(p.def==ThingDefOf.Human && NecromancyGenes.HasBody(p) && appearance.All(n=>p.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed(n))),"construct is Human with necrotic body and role-specific native cosmetic genes");
   Check(p.story.bodyType==(role=="Golem"?BodyTypeDefOf.Hulk:BodyTypeDefOf.Thin) && (role!="Skeleton" || p.story.headType==HeadTypeDefOf.Skull),"construct selects role-specific body and skeletal head");
   Check(p.needs.mood!=null && p.needs.food==null && p.needs.rest==null && p.needs.joy==null,"colonist mood exists while body gene removes only its specified needs");
   Check(!p.GetDisabledWorkTypes().Any(),"construct has no artificial work restrictions");
   Check(ThingDefOf.Human.AllRecipes.All(r=>p.def.AllRecipes.Contains(r)),"construct exposes the complete human recipe and surgery list");
  }
  void CheckColonistOperations(Pawn p){
   CheckConstructColonist(p);
   Check(p.skills.GetSkill(SkillDefOf.Crafting).passion==Passion.None && p.skills.GetSkill(SkillDefOf.Plants).passion==Passion.Minor && p.skills.GetSkill(SkillDefOf.Medicine).passion==Passion.Major,"all three configured passion levels are applied, including overriding scholar default");
   var probe=new Harmony("local.lich.surgerytest");var method=AccessTools.Method(typeof(Recipe_Surgery),"CheckSurgeryFail");surgerySubject=p;
   try{
    probe.Patch(method,prefix:new HarmonyMethod(typeof(LichChecks),nameof(DeterministicSurgery)));
    foreach(var name in new[]{"InstallBionicArm","InstallPainstopper"}){
     var recipe=DefDatabase<RecipeDef>.GetNamed(name);var part=recipe.Worker.GetPartsToApplyOn(p,recipe).First();
     Check(recipe.AvailableOnNow(p,part),"native surgery availability accepts construct: "+name);
     var ingredients=recipe.ingredients.Select(i=>ThingMaker.MakeThing(i.filter.AllowedThingDefs.First())).ToList();
     var bill=new Bill_Medical(recipe,ingredients);p.health.surgeryBills.AddBill(bill);
     recipe.Worker.ApplyOnPawn(p,part,worker,ingredients,bill);
     Check(p.health.hediffSet.HasHediff(recipe.addsHediff),"native surgery worker installs implant: "+name);
     p.health.surgeryBills.Delete(bill);foreach(var t in ingredients)if(!t.Destroyed)t.Destroy();
    }
   }finally{surgerySubject=null;probe.Unpatch(method,HarmonyPatchType.Prefix,probe.Id);}
   Check(NecromancyGenes.HasBody(p) && Hediff_Construct.AppearanceGenesFor(Necro.Construct(p).kind).All(n=>p.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed(n))),"bionic surgery preserves necrotic body and role appearance genes");
   var h=Necro.Construct(p);var at=p.Position;var priorName=p.Name;var work=WorkSnapshot(p);float health=p.HealthScale;var master=h.master;var skills=SkillSnapshot(p);
   // 1.63.0：装了 HAR 时人类带 HAR 的 AlienComp，真正的旧档里旧种族的小人不会有它；模拟旧种族时先拿掉，升级回人类后放回（否则 HAR 生成时强转 ThingDef_AlienRace 出错）。
   var compList=(List<ThingComp>)AccessTools.Field(typeof(ThingWithComps),"comps").GetValue(p);var foreign=compList?.Where(c=>c.GetType().Namespace=="AlienRace").ToList()??new List<ThingComp>();foreach(var c in foreign)compList.Remove(c);
   p.DeSpawn();p.def=h.LegacyRace;h.appearanceVersion=1;GenSpawn.Spawn(p,at,map);string legacyId=p.ThingID;h.UpgradeAppearance();
   if(foreign.Count>0){compList=(List<ThingComp>)AccessTools.Field(typeof(ThingWithComps),"comps").GetValue(p);foreach(var c in foreign)if(!compList.Contains(c))compList.Add(c);AccessTools.Method(typeof(ThingWithComps),"RebuildCompsByType")?.Invoke(p,null);}
   Check(p.def==ThingDefOf.Human && p.Spawned && p.Position==at && p.ThingID==legacyId && h.master==master && p.Name==priorName && SameWork(p,work) && SameExperience(p,skills) && Near(p.HealthScale,health),"live legacy race migrates in place preserving ID, owner, name, work, skills and health scale");
   Check(map.listerThings.ThingsOfDef(ThingDefOf.Human).Contains(p) && !map.listerThings.ThingsOfDef(h.LegacyRace).Contains(p),"live race migration updates map race indices");
   p.genes.AddGene(Necro.Def<GeneDef>("NecroticTouch"),true);foreach(var g in p.genes.GenesListForReading.Where(g=>Hediff_Construct.AppearanceGenesFor(h.kind).Contains(g.def.defName)).ToList())p.genes.RemoveGene(g);p.genes.AddGene(DefDatabase<GeneDef>.GetNamed("Body_Hulk"),true);p.genes.AddGene(DefDatabase<GeneDef>.GetNamed("Skin_Blue"),true);h.colonistVersion=0;
  }
  void CheckColonistAfterLoad(Pawn pawn,ConstructSeed seed){
   CheckConstructInfoAndMood(pawn);
   Check(pawn.def==ThingDefOf.Human && NecromancyGenes.HasBody(pawn) && pawn.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Body_Hulk")) && pawn.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Skin_Blue")) && !pawn.genes.GenesListForReading.Any(g=>Hediff_Construct.AppearanceGenesFor(Necro.Construct(pawn).kind).Contains(g.def.defName)) && pawn.genes.HasActiveGene(Necro.Def<GeneDef>("NecroticTouch")),"edited body/skin/eye genes and unrelated added genes survive save/load without resetting");
   Check(pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("BionicArm")) && pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("Painstopper")),"installed bionic limb and brain implant survive migration and save/load");
   Check(pawn.skills.GetSkill(SkillDefOf.Crafting).passion==Passion.None && pawn.skills.GetSkill(SkillDefOf.Plants).passion==Passion.Minor && pawn.skills.GetSkill(SkillDefOf.Medicine).passion==Passion.Major,"actual pawn passions survive save/load");
   Check(seed.design.PassionFor(SkillDefOf.Cooking,"Golem")==Passion.Major,"unawakened embryo passion plan survives save/load");
   var h=Necro.Construct(pawn);var count=pawn.genes.GenesListForReading.Count;h.UpgradeColonist();h.Notify_Resurrected();Check(pawn.genes.GenesListForReading.Count==count && pawn.story.bodyType==BodyTypeDefOf.Hulk,"repeated upgrade and resurrection preserve player-edited genes and appearance");
  }
 }
}
