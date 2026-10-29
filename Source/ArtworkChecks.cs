using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.58.5：Test.ps1 -Only Artworks（血肉艺术品与皮雕）：定义与贴图、艺创台配方、按配方做出成品（品质、艺术描述、材料）、存读档。
 public partial class LichChecks {
  static string awSaved;static string[] awIds;
  static readonly string[] AwFlesh={"LP_FleshArtSmall","LP_FleshArtLarge","LP_FleshArtGrand"},AwLeather={"LP_LeatherCarvingSmall","LP_LeatherCarvingLarge","LP_LeatherCarvingGrand"};
  static readonly int[] AwCount={50,100,400};
  static readonly string[] AwFleshWords={"血肉艺术品","血肉雕塑","肉塑","作品"},AwLeatherWords={"皮雕","皮艺作品","缝制雕像","作品"};
  static RecipeDef AwRecipe(ThingDef d)=>DefDatabase<RecipeDef>.AllDefsListForReading.FirstOrDefault(r=>r.products!=null && r.products.Count==1 && r.products[0].thingDef==d);
  static Thing AwMake(RecipeDef r,Pawn worker,ThingDef ing,int count){
   var t=ThingMaker.MakeThing(ing);t.stackCount=count;
   return GenRecipe.MakeRecipeProducts(r,worker,new List<Thing>{t},t,null,null,null,null).First();
  }
  static string AwSummary(Thing t){
   var art=t.TryGetComp<CompArt>();t.TryGetQuality(out var q);
   return t.def.defName+"/"+(t.Stuff?.defName??"-")+"/"+q+"/"+art?.Title;
  }

  void CheckArtworks(){
   var made=new List<Thing>();
   try{
    // 1. 六件作品、两种未完成品都在，每档至少两种随机样式；皮雕只吃布料 / 皮革，血肉艺术品不用材料、不会腐烂
    var flesh=AwFlesh.Select(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)).ToList();var leather=AwLeather.Select(n=>DefDatabase<ThingDef>.GetNamedSilentFail(n)).ToList();
    var all=flesh.Concat(leather).ToList();
    bool loaded=all.All(d=>d!=null && d.Minifiable && d.GetCompProperties<CompProperties_Art>()!=null && d.comps.Count(c=>c is CompProperties_Art)==1 && d.HasComp(typeof(CompQuality)));
    bool tex=loaded && all.All(d=>ContentFinder<Texture2D>.GetAllInFolder(d.graphicData.texPath).Count()>=2) && new[]{"LP_UnfinishedFleshArt","LP_UnfinishedLeatherCarving"}.All(n=>ContentFinder<Texture2D>.Get(DefDatabase<ThingDef>.GetNamed(n).graphicData.texPath,false)!=null);
    var cloth=ThingDefOf.Cloth;var hide=DefDatabase<ThingDef>.GetNamed("Leather_Plain");
    bool stuffOk=loaded && leather.All(d=>d.MadeFromStuff && GenStuff.AllowedStuffsFor(d).Contains(cloth) && GenStuff.AllowedStuffsFor(d).Contains(hide) && !GenStuff.AllowedStuffsFor(d).Contains(ThingDefOf.WoodLog) && !GenStuff.AllowedStuffsFor(d).Contains(ThingDefOf.Steel))
     && flesh.All(d=>!d.MadeFromStuff && !d.HasComp(typeof(CompRottable)));
    Check(loaded && tex && stuffOk,"6 artworks (flesh art and leather carvings, small/large/grand) load with one art comp, quality, minifiable, ≥2 random textures each; leather carvings take fabric or leather only, flesh art has no stuff and never rots");
    // 2. 配方都在原版艺创台上，用艺术技能；皮雕 50/100/400 材料，血肉艺术品 50/100/400 块任意生肉（默认不勾人肉）
    var bench=DefDatabase<ThingDef>.GetNamed("TableSculpting");var cow=DefDatabase<ThingDef>.GetNamedSilentFail("Meat_Cow");var human=DefDatabase<ThingDef>.GetNamedSilentFail("Meat_Human");
    bool recipes=true;var detail=new List<string>();
    for(int i=0;i<3;i++){
     var rf=AwRecipe(flesh[i]);var rl=AwRecipe(leather[i]);
     bool f=rf!=null && rf.AllRecipeUsers.Contains(bench) && bench.AllRecipes.Contains(rf) && rf.workSkill==SkillDefOf.Artistic && rf.unfinishedThingDef?.defName=="LP_UnfinishedFleshArt"
      && rf.ingredients.Count==1 && (int)rf.ingredients[0].GetBaseCount()==AwCount[i] && rf.ingredients[0].filter.Allows(cow) && rf.fixedIngredientFilter.Allows(human) && !rf.defaultIngredientFilter.Allows(human) && rf.defaultIngredientFilter.Allows(cow)
      && Near(rf.WorkAmountTotal(null),flesh[i].GetStatValueAbstract(StatDefOf.WorkToMake));
     bool l=rl!=null && rl.AllRecipeUsers.Contains(bench) && bench.AllRecipes.Contains(rl) && rl.workSkill==SkillDefOf.Artistic && rl.unfinishedThingDef?.defName=="LP_UnfinishedLeatherCarving" && rl.productHasIngredientStuff && leather[i].costStuffCount==AwCount[i];
     recipes&=f && l;detail.Add(rf?.defName+"/"+rl?.defName);
    }
    Check(recipes,"recipes on the vanilla sculpting table (Artistic): "+string.Join(", ",detail)+"; flesh art eats 50/100/400 raw meat of any kind (human meat off by default), leather carvings 50/100/400 fabric/leather");
    // 3. 按配方做小血肉艺术品：打包成可搬运的雕塑，有品质、标题和血肉描述，有市值
    var w=HtPawn();
    var p1=AwMake(AwRecipe(flesh[0]),w,cow,50);made.Add(p1);var a1=p1.GetInnerIfMinified();var art1=a1.TryGetComp<CompArt>();
    string desc1=art1?.GenerateImageDescription().ToString()??"";
    Check(p1 is MinifiedThing && a1.def==flesh[0] && a1.TryGetQuality(out _) && art1.Active && !art1.Title.NullOrEmpty() && AwFleshWords.Any(desc1.Contains) && a1.MarketValue>50f && a1.GetStatValue(StatDefOf.Beauty)>0f,"made a small flesh art from 50 beef: \""+art1?.Title+"\", value "+a1.MarketValue.ToString("F0")+" — "+desc1);
    // 4. 按配方做大皮雕：材料就是用的布料，描述是皮雕的说法；皮革也能做
    var p2=AwMake(AwRecipe(leather[1]),w,cloth,100);made.Add(p2);var a2=p2.GetInnerIfMinified();var art2=a2.TryGetComp<CompArt>();
    string desc2=art2?.GenerateImageDescription().ToString()??"";
    var p3=AwMake(AwRecipe(leather[0]),w,hide,50);made.Add(p3);var a3=p3.GetInnerIfMinified();
    Check(a2.def==leather[1] && a2.Stuff==cloth && a2.TryGetQuality(out _) && art2.Active && AwLeatherWords.Any(desc2.Contains) && a2.MarketValue>50f && a3.def==leather[0] && a3.Stuff==hide,"made a large leather carving from 100 cloth (\""+art2?.Title+"\", value "+a2.MarketValue.ToString("F0")+") and a small one from plain leather — "+desc2);
    // 5. 巨型的两件能放下（2×2），美观度按原版巨型雕塑的量级
    var g1=ThingMaker.MakeThing(flesh[2]);var g2=ThingMaker.MakeThing(leather[2],cloth);
    foreach(var g in new[]{g1,g2}){g.TryGetComp<CompQuality>().SetQuality(QualityCategory.Normal,ArtGenerationContext.Colony);made.Add(g);}
    GenSpawn.Spawn(g1,HtCell(2,2),map);GenSpawn.Spawn(g2,HtCell(-3,2),map);
    float b1=g1.GetStatValue(StatDefOf.Beauty),b2=g2.GetStatValue(StatDefOf.Beauty);
    Check(g1.Spawned && g2.Spawned && g1.def.size==new IntVec2(2,2) && b1>=300f && b2>=300f,"grand flesh art and grand leather carving spawn 2×2, beauty "+b1.ToString("F0")+" / "+b2.ToString("F0"));
   }finally{
    foreach(var t in made)if(!t.Destroyed)t.Destroy();
    foreach(var p in htMade)if(!p.Destroyed)p.Destroy();htMade.Clear();
   }
  }
  // 6. 存读档：两件作品的材料、品质、标题都在
  void PrepareArtworksSave(){
   var a=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_FleshArtSmall"));var b=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_LeatherCarvingSmall"),ThingDefOf.Cloth);
   a.TryGetComp<CompQuality>().SetQuality(QualityCategory.Excellent,ArtGenerationContext.Colony);b.TryGetComp<CompQuality>().SetQuality(QualityCategory.Good,ArtGenerationContext.Colony);
   GenSpawn.Spawn(a,HtCell(-2,-2),map);GenSpawn.Spawn(b,HtCell(2,-2),map);
   awIds=new[]{a.ThingID,b.ThingID};awSaved=AwSummary(a)+"|"+AwSummary(b);
  }
  void CheckArtworksAfterLoad(){
   var ts=awIds.Select(id=>map.listerThings.AllThings.FirstOrDefault(t=>t.ThingID==id)).ToList();
   string now=ts.Any(t=>t==null)?"missing":AwSummary(ts[0])+"|"+AwSummary(ts[1]);
   Check(now==awSaved,"flesh art and leather carving keep stuff, quality and title through save/load ("+awSaved+")");
   foreach(var t in ts)t?.Destroy();
  }
 }
}
