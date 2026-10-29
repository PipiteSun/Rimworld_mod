using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace LichPhylactery {
 // 1.46.0 DND 背景故事（组名 Backstories：Test.ps1 -Only Backstories），8 条检查。其他检查组期间背景故事被压住（Backstories.TestSuppressed），本组临时打开，结束恢复默认设置。
 public partial class LichChecks {
  List<Pawn> BsBatch(string kind,Faction faction,int n,string xenotype=null,int age=30,IEnumerable<TraitDef> forcedTraits=null){
   var kindDef=DefDatabase<PawnKindDef>.GetNamed(kind);var xeno=xenotype==null?null:DefDatabase<XenotypeDef>.GetNamed(xenotype);
   var list=new List<Pawn>();
   // 原版约 25% 的小人（以及派系首领）拿有名有姓的固定人物（背景不可洗牌），背景故事不动它们：这种直接丢掉重生成。
   for(int i=0;i<n*5 && list.Count<n;i++){
    var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kindDef,faction,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:xeno,fixedBiologicalAge:age,fixedChronologicalAge:age,forcedTraits:forcedTraits));
    if(p.story.Childhood?.shuffleable==false || p.story.Adulthood?.shuffleable==false){BgDiscard(new[]{p});continue;}
    list.Add(p);
   }
   return list;
  }
  static bool BsIn(BackstoryDef b,string prefix)=>b?.spawnCategories!=null && b.spawnCategories.Any(c=>c.StartsWith(prefix));
  static string BsNames(IEnumerable<Pawn> pawns)=>string.Join(",",pawns.Take(10).Select(p=>p.story.Adulthood?.defName??"-"));
  static int BsSkill(BackstoryDef b,SkillDef s)=>b.skillGains?.FirstOrDefault(g=>g.skill==s)?.amount??0;

  void CheckBackstories(){
   var s=LichMod.Settings;var batches=new List<Pawn>();
   bool oldTraits=s.backgroundTraits;int oldTraitPct=s.backgroundTraitPercent;
   try{
    Backstories.TestSuppressed=false;s.dndBackstories=true;s.raceBackstoryPercent=75;s.vanillaFaerunPercent=10;s.backgroundTraits=true;s.backgroundTraitPercent=100;
    // 1 加载
    var all=DefDatabase<BackstoryDef>.AllDefsListForReading.Where(Backstories.IsDND).ToList();
    var raceCats=Backstories.Rules.Where(r=>r.tier==BackstoryRuleTier.Race || r.defName=="LP_BsPool_LichNPC").SelectMany(r=>r.adulthood.Select(c=>c.category)).Distinct().ToList();
    var emptyRace=raceCats.Where(c=>Backstories.InCategory(c,BackstorySlot.Adulthood).Count==0).ToList();
    var missingCats=Backstories.Rules.SelectMany(r=>r.childhood.Select(c=>(c.category,BackstorySlot.Childhood)).Concat(r.adulthood.Select(c=>(c.category,BackstorySlot.Adulthood)))).Where(x=>Backstories.InCategory(x.Item1,x.Item2).Count==0).Select(x=>x.Item1+"/"+x.Item2).Distinct().ToList();
    var red=DefDatabase<BackstoryDef>.GetNamed("LP_Bs_RedWizard");var thrall=DefDatabase<BackstoryDef>.GetNamed("LP_Bs_Thrall");var smith=DefDatabase<BackstoryDef>.GetNamed("LP_Bs_ForgeSmith");
    bool sample=BsSkill(red,SkillDefOf.Intellectual)==5 && BsSkill(red,SkillDefOf.Social)==2 && red.workDisables==(WorkTags.ManualDumb|WorkTags.Cleaning) && red.GetModExtension<BackstoryTraitExtension>()?.trait==BackgroundTraits.Noble && Near(red.GetModExtension<BackstoryTraitExtension>().chance,.4f)
     && BsSkill(thrall,SkillDefOf.Mining)==3 && BsSkill(thrall,SkillDefOf.Construction)==2 && thrall.workDisables==(WorkTags.Social|WorkTags.Artistic|WorkTags.Intellectual)
     && BsSkill(smith,SkillDefOf.Crafting)==5 && BsSkill(smith,SkillDefOf.Mining)==2 && smith.workDisables==WorkTags.PlantWork && smith.slot==BackstorySlot.Adulthood;
    var bad=all.Where(b=>b.title.NullOrEmpty() || b.titleShort.NullOrEmpty() || b.description.NullOrEmpty() || !b.shuffleable || b.ConfigErrors().Any()).Select(b=>b.defName).ToList();
    Check(all.Count>=139+23 && bad.Count==0 && emptyRace.Count==0 && missingCats.Count==0 && sample && !Backstories.Rules.Any(r=>r.ConfigErrors().Any()),"at least 139 LP_Bs_* backstories (1.46.0) plus 23 common-race ones (1.57.0) and later event backstories load with titles; every race category has an adulthood, every rule category has backstories, sampled skills/disables/trait links match (count="+all.Count+" bad="+string.Join(",",bad)+" emptyRace="+string.Join(",",emptyRace)+" missing="+string.Join(",",missingCats)+" sample="+sample+")");

    var colony=Factions.Colony;var hostile=Factions.Hostile;
    // 2 种族出现率 100%：精灵成年背景全来自 LP_Race_Elf
    s.raceBackstoryPercent=100;
    var elves=BsBatch("LP_FC_ElfRanger",colony,10,"LP_Elf");batches.AddRange(elves);
    Check(colony!=null && elves.All(p=>Backstories.InCategory(p.story.Adulthood,"LP_Race_Elf") && Backstories.InCategory(p.story.Childhood,"LP_Race_Elf")),"race rate 100%: 10 Faerun colony elves all get LP_Race_Elf childhood and adulthood ("+BsNames(elves)+")");
    // 3 种族出现率 0%
    s.raceBackstoryPercent=0;
    var elves0=BsBatch("LP_FC_ElfRanger",colony,10,"LP_Elf");batches.AddRange(elves0);
    Check(elves0.All(p=>!BsIn(p.story.Adulthood,"LP_Race_") && !BsIn(p.story.Childhood,"LP_Race_")),"race rate 0%: 10 elves get no LP_Race_* backstory ("+BsNames(elves0)+")");
    s.raceBackstoryPercent=75;
    // 4 原版人类
    var outlander=Find.FactionManager.FirstFactionOfDef(FactionDefOf.OutlanderCivil);
    var humans=BsBatch("Villager",outlander,60,"Baseliner");batches.AddRange(humans);
    int slots=humans.Sum(p=>(p.story.Childhood!=null?1:0)+(p.story.Adulthood!=null?1:0));int faerun=humans.Sum(p=>(BsIn(p.story.Childhood,"LP_Faerun")?1:0)+(BsIn(p.story.Adulthood,"LP_Faerun")?1:0));
    float share=slots==0?0f:(float)faerun/slots;
    s.vanillaFaerunPercent=0;
    var humans0=BsBatch("Villager",outlander,30,"Baseliner");batches.AddRange(humans0);
    int faerun0=humans0.Count(p=>Backstories.IsDND(p.story.Childhood) || Backstories.IsDND(p.story.Adulthood));
    s.vanillaFaerunPercent=10;
    Check(outlander!=null && share<=.3f && faerun0==0 && humans.All(p=>!BsIn(p.story.Childhood,"LP_Race_") && !BsIn(p.story.Adulthood,"LP_Race_")),"vanilla humans of a vanilla faction: Faerun share "+share.ToStringPercent()+" of "+slots+" slots at 10% (0%-30%); 0 of 30 at 0% ("+faerun0+")");
    // 5 塞伊：战斗兵种不会禁用暴力；红袍法师成年背景属于 LP_Arcane
    var thay=new List<Pawn>();
    foreach(var k in new[]{"LP_FH_OrcWarrior","LP_FH_MinotaurCharger","LP_FH_GithMercenary","LP_FH_RedWizard","LP_FH_OgreBrute","LP_FH_RuneTroll"})thay.AddRange(BsBatch(k,hostile,5));
    batches.AddRange(thay);
    // 红袍法师是 factionLeader：原版有时给有名有姓的固定人物（背景不可洗牌），这种保留原样。
    var wizards=thay.Where(p=>p.kindDef.defName=="LP_FH_RedWizard" && p.story.Adulthood.shuffleable && p.story.Childhood.shuffleable).ToList();
    Check(hostile!=null && thay.Count==30 && wizards.Count>0 && thay.All(p=>!p.WorkTagIsDisabled(WorkTags.Violent)) && wizards.All(p=>Backstories.InCategory(p.story.Adulthood,"LP_Arcane") && Backstories.InCategory(p.story.Childhood,"LP_Arcane")),"Thay: 30 combat pawns none incapable of violence; red wizards get LP_Arcane childhood and adulthood ("+BsNames(wizards)+"; DND share "+thay.Count(p=>Backstories.IsDND(p.story.Adulthood))+"/30)");
    // 6 黑网商人
    var blackNet=Factions.Of(Factions.BlackNetDef);
    int errBefore=Log.Messages.Count(m=>m.type==LogMessageType.Error);
    var brokers=BsBatch("LP_BN_Broker",blackNet,10);batches.AddRange(brokers);
    var newErrors=Log.Messages.Where(m=>m.type==LogMessageType.Error).Skip(errBefore).Select(m=>m.text).Where(t=>t.Contains("backstor") || t.Contains("No shuffled")).ToList();
    Check(blackNet!=null && newErrors.Count==0 && brokers.All(p=>p.story.Childhood!=null && p.story.Adulthood!=null),"Black Network brokers generate with both backstories and no backstory errors ("+BsNames(brokers)+(newErrors.Count>0?" errors: "+newErrors[0]:"")+")");
    // 7 背景特性联动：红袍法师拿到贵族；已经有别的背景特性时不拿
    Backstories.TestTraitChance=1f;
    // 红袍法师成年背景是 LP_Arcane 六条之一：一直生成到有 6 个拿到“塞伊红袍法师”（最多 80 个）。
    var linked=new List<Pawn>();
    for(int i=0;i<80 && linked.Count(p=>p.story.Adulthood==red)<6;i++){var w=BsBatch("LP_FH_RedWizard",hostile,1)[0];batches.Add(w);linked.Add(w);}
    var sage=BsBatch("LP_FH_RedWizard",hostile,5,forcedTraits:new[]{BackgroundTraits.Sage});batches.AddRange(sage);
    Backstories.TestTraitChance=null;
    var eligible=linked.Where(p=>p.story.Adulthood==red).ToList();
    int nobles=eligible.Count(p=>BackgroundTraits.Has(p,BackgroundTraits.Noble));
    // 没拿到的只能是特性满了或者和贵族冲突（苦行）。
    bool explained=eligible.All(p=>BackgroundTraits.Has(p,BackgroundTraits.Noble) || p.story.traits.allTraits.Count(t=>t.sourceGene==null)>=Backstories.MaxTraits || p.story.traits.allTraits.Any(t=>t.def.ConflictsWith(BackgroundTraits.Noble) || BackgroundTraits.Noble.ConflictsWith(t.def)));
    bool sageOk=sage.All(p=>BackgroundTraits.Has(p,BackgroundTraits.Sage) && !BackgroundTraits.Has(p,BackgroundTraits.Noble) && BackgroundTraits.Count(p)==1);
    Check(eligible.Count>0 && nobles>0 && explained && linked.All(p=>BackgroundTraits.Count(p)<=1) && sageOk,"trait link at 100%: "+nobles+"/"+eligible.Count+" red wizards with the red wizard adulthood become nobles; forced sages get no noble ("+sageOk+")");
    // 8 HAR 种族
    var harKinds=new[]{("LP_MarilithColonist","LP_Race_Marilith"),("LP_EttinColonist","LP_Race_Ettin"),("LP_CyclopsColonist","LP_Race_Cyclops")};
    if(harKinds.All(k=>DefDatabase<PawnKindDef>.GetNamedSilentFail(k.Item1)!=null)){
     s.raceBackstoryPercent=100;
     var res=new List<string>();bool ok=true;
     foreach(var (kind,cat) in harKinds){
      var batch=BsBatch(kind,Faction.OfPlayer,5);batches.AddRange(batch);
      // 巨人还会抽到共用的 LP_Race_Giant。
      bool good=batch.All(p=>Backstories.InCategory(p.story.Adulthood,cat) || cat!="LP_Race_Marilith" && Backstories.InCategory(p.story.Adulthood,"LP_Race_Giant"));
      bool own=batch.Any(p=>Backstories.InCategory(p.story.Adulthood,cat));
      ok&=good && own;res.Add(kind+":"+BsNames(batch));
     }
     s.raceBackstoryPercent=75;
     Check(ok,"HAR races at 100%: marilith/ettin/cyclops adulthoods come from their own race category (giants may also draw LP_Race_Giant): "+string.Join(" | ",res));
    }else Log.Message("[LICH-TEST] HAR not active: skipped the HAR backstory check");
   }finally{
    BgDiscard(batches);
    Backstories.TestTraitChance=null;
    s.dndBackstories=true;s.raceBackstoryPercent=75;s.vanillaFaerunPercent=10;s.backgroundTraits=oldTraits;s.backgroundTraitPercent=oldTraitPct;
    Backstories.TestSuppressed=true;
   }
  }
 }
}
