using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // 1.45.1：Test.ps1 -Only MagicApparel。定义与贴图（含 _m）、研究、层位（披风套板甲、鸟人轻甲不挡翅膀）、纹章色、红袍好感、死亡骑士再站起、派系换装。
 public partial class LichChecks {
  static string apparelSetPawnId;
  public static readonly string[] MagicApparelBody={"LP_KnightMantle","LP_RangerLeathers","LP_WoodwalkerCloak","LP_ShadowweaveCloak","LP_RedWizardRobe","LP_DeathknightPlate","LP_OgreTrophyHide","LP_SkyguardHarness","LP_ElementalWeave","LP_AdventurerClothes","LP_BardFinery","LP_HarperCloak"};
  public static readonly string[] MagicApparelHead={"LP_HelmRestlessDead","LP_CrownOfBone","LP_GithCrestedHelm","LP_RogueMask","LP_BardBeret"};
  Apparel AWear(Pawn p,string def){var d=DefDatabase<ThingDef>.GetNamed(def);var a=(Apparel)ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);p.apparel.Wear(a,false);return a;}
  void CheckMagicApparel(){
   var before=new HashSet<Thing>(map.listerThings.AllThings);
   // 1. 定义、图标、穿着贴图（五体型×三向，含遮罩）
   var bad=new List<string>();
   foreach(var n in MagicApparelBody.Concat(MagicApparelHead)){
    var d=DefDatabase<ThingDef>.GetNamedSilentFail(n);if(d==null || d.ConfigErrors().Any()){bad.Add(n+(d==null?"":"("+string.Join(";",d.ConfigErrors())+")"));continue;}
    if(d.uiIcon==null || d.uiIcon==BaseContent.BadTex || ContentFinder<Texture2D>.Get(d.graphicData.texPath+"_m",false)==null)bad.Add(n+" icon");
    var path=d.apparel.wornGraphicPath;bool head=MagicApparelHead.Contains(n);
    foreach(var b in head?new[]{""}:new[]{"_Male","_Female","_Thin","_Fat","_Hulk"})foreach(var v in new[]{"south","east","north"})if(ContentFinder<Texture2D>.Get(path+b+"_"+v,false)==null || ContentFinder<Texture2D>.Get(path+b+"_"+v+"_m",false)==null)bad.Add(n+b+"_"+v);
    if(!d.apparel.useWornGraphicMask || d.graphicData.shaderType?.defName!="CutoutComplex")bad.Add(n+" mask flags");
   }
   Check(bad.Count==0,"all "+(MagicApparelBody.Length+MagicApparelHead.Length)+" magic apparel pieces load with icons, masks and worn textures for every body type: "+string.Join(",",bad.Take(12)));
   // 2. 研究
   var res=new[]{("LP_Research_ArcaneTailoring",3f,2.8f)};
   Check(res.All(r=>{var d=DefDatabase<ResearchProjectDef>.GetNamedSilentFail(r.Item1);return d!=null && d.tab?.defName=="LP_FireGiantTech" && Near(d.ResearchViewX,r.Item2) && Near(d.ResearchViewY,r.Item3);}) && DefDatabase<RecipeDef>.GetNamedSilentFail("Make_LP_AdventurerClothes")?.researchPrerequisite==null,"apparel research sits in the DND tab; plain clothes need no research");
   Check(DefDatabase<ApparelLayerDef>.GetNamed("LP_Cape").drawOrder>ApparelLayerDefOf.Shell.drawOrder && DefDatabase<ApparelLayerDef>.GetNamed("LP_Cape").drawOrder<ApparelLayerDefOf.Overhead.drawOrder,"the cape layer draws above the shell and below the helmet");
   var spot=ItemSpot(3);var p=ItemPawn(spot);Hold(p);var body=p.RaceProps.body;Func<string,ThingDef> D=n=>DefDatabase<ThingDef>.GetNamed(n);
   // 3. 层位
   Check(ApparelUtility.CanWearTogether(D("Apparel_PlateArmor"),D("LP_KnightMantle"),body),"the knight mantle fits over plate armour");
   Check(ApparelUtility.CanWearTogether(D("LP_SkyguardHarness"),D("LP_FeatherCloak"),body) && D("LP_SkyguardHarness").apparel.layers.SequenceEqual(new[]{ApparelLayerDefOf.Middle}),"the skyguard harness is a middle-layer piece that leaves the wings and feather cloak free");
   // 6. 纹章色：穿着贴图的第二色是派系色或意识形态色；元素织袍按元素。
   var plate=AWear(p,"LP_KnightMantle");Check(ApparelGraphicRecordGetter.TryGetGraphicApparel(plate,p.story.bodyType,false,out var rec) && rec.graphic.Shader==ShaderDatabase.CutoutComplex && ColorNear(rec.graphic.colorTwo,MagicApparel.Herald(p,plate)),"the worn graphic is tinted with the heraldic second colour: "+rec.graphic?.colorTwo+" vs "+MagicApparel.Herald(p,plate)+" / is tinted with the heraldic second colour: "+rec.graphic?.colorTwo);
   var genasi=RacePawn("LP_GenasiFire");Place(genasi,spot+IntVec3.West*2);Hold(genasi);var weave=AWear(genasi,"LP_ElementalWeave");
   Check(ColorNear(MagicApparel.Herald(genasi,weave),MagicApparel.ElementColor("Fire")) && Near(MagicApparel.ElementFactor(genasi,DamageDefOf.Flame),.8f) && Near(MagicApparel.ElementFactor(genasi,DamageDefOf.Cut),1f),"the elemental weave takes the fire colour and cuts fire damage by 20% for a fire genasi: element="+Genasi.Element(genasi)+" worn="+(Items.Worn(genasi,"LP_ElementalWeave")!=null)+" factor="+MagicApparel.ElementFactor(genasi,DamageDefOf.Flame)+" colour="+MagicApparel.Herald(genasi,weave));
   genasi.Destroy();p.apparel.DestroyAll();
   // 7. 旗帜。
   var ally=ItemPawn(spot+IntVec3.North*2);Hold(ally);StatPart_KnightBanner.Reset();float mb=ally.GetStatValue(StatDefOf.MentalBreakThreshold,true,-1);AWear(p,"LP_KnightMantle");StatPart_KnightBanner.Reset();
   Check(Near(ally.GetStatValue(StatDefOf.MentalBreakThreshold,true,-1),mb-.02f),"the knight's mantle banner lowers nearby allies' mental break threshold by 2%");
   p.apparel.DestroyAll();
   // 8. 死亡骑士：倒地再站起来（3天一次，巫妖不触发）
   AWear(p,"LP_DeathknightPlate");AWear(p,"LP_HelmRestlessDead");
   Check(Items.Has(p,"LP_Set_Deathknight") && DefDatabase<ThoughtDef>.GetNamed("LP_DeathknightWhispers").Worker.CurrentState(p).Active,"the deathknight set is active and a living wearer hears the armour whisper");
   foreach(var part in p.RaceProps.body.AllParts.Where(b=>b.def==BodyPartDefOf.Leg).ToList()){var cut=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,p,part);cut.Severity=part.def.hitPoints-1;p.health.AddHediff(cut,part);}
   bool downed=p.Downed;bool rose=MagicApparel.TryRise(p);
   Check(downed && rose && !p.Downed && !MagicApparel.TryRise(p),"a downed deathknight stands up once, then waits 3 days (downed "+downed+", rose "+rose+", now downed "+p.Downed+")");
   Check(MagicEffects.RevivalBlocked(p),"the deathknight set blocks the revival core from stacking");
   apparelSetPawnId=p.ThingID;
   // 9. 红袍：同盟商队看到穿红袍的殖民者，好感-5。
   var a=MagicItems.Alliance;
   if(a!=null && !a.HostileTo(Faction.OfPlayer)){
    var colonist=map.mapPawns.FreeColonistsSpawned.First(c=>c!=p);var robe=AWear(colonist,"LP_RedWizardRobe");int gw=a.PlayerGoodwill;
    bool seen=MagicApparel.RedRobeSeen(map,a);Check(seen && a.PlayerGoodwill<gw && a.PlayerGoodwill>=gw-7 && !MagicApparel.RedRobeSeen(map,null),"an alliance caravan that sees the red robe costs about 5 goodwill (vanilla scaling applies): seen="+seen+" goodwill "+gw+"->"+a.PlayerGoodwill+" worn="+(Items.Worn(colonist,"LP_RedWizardRobe")!=null));
    colonist.apparel.Remove(robe);robe.Destroy();a.TryAffectGoodwillWith(Faction.OfPlayer,gw-a.PlayerGoodwill,false,false,null,null);
   }
   // 10. 派系换装（必穿件）
   var outfits=new[]{("LP_FC_Paladin","LP_KnightMantle"),("LP_FH_RedWizard","LP_RedWizardRobe"),("LP_FH_OgreBrute","LP_OgreTrophyHide"),("LP_FC_ElfRanger","LP_WoodwalkerCloak")};
   var missing=new List<string>();
   foreach(var (kind,piece) in outfits){
    var k=DefDatabase<PawnKindDef>.GetNamed(kind);var f=Find.FactionManager.AllFactions.FirstOrDefault(x=>x.def==k.defaultFactionDef)??Find.FactionManager.AllFactions.FirstOrDefault(x=>!x.IsPlayer && x.def.humanlikeFaction);
    var g=PawnGenerator.GeneratePawn(new PawnGenerationRequest(k,f,forceGenerateNewPawn:true,fixedBiologicalAge:30,fixedChronologicalAge:30));
    if(Items.Worn(g,piece)==null)missing.Add(kind+":"+string.Join("/",g.apparel.WornApparel.Select(x=>x.def.defName)));
    g.Destroy();
   }
   Check(missing.Count==0,"faction soldiers are generated wearing their new required gear: "+string.Join(" ",missing));
   foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !(t is Pawn) && !(t is Filth) && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList())if(!t.Destroyed)t.Destroy();
   ally.Destroy();
  }
  static bool ColorNear(Color a,Color b)=>Mathf.Abs(a.r-b.r)<.02f && Mathf.Abs(a.g-b.g)<.02f && Mathf.Abs(a.b-b.b)<.02f;
  void PrepareMagicApparelSave(){}
  void CheckMagicApparelAfterLoad(){
   var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==apparelSetPawnId);var h=MagicApparel.SetHediff(p,"LP_Set_Deathknight");
   Check(h!=null && h.lastRise>=0 && Items.Worn(p,"LP_DeathknightPlate")!=null,"the deathknight set and its rise cooldown survive save/load");
  }
 }
}
