using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
namespace LichPhylactery {
 // 2026-10-29：Test.ps1 -Only TraitRarity（特性稀有度，7 项）。
 public partial class LichChecks {
  static TraitDef TrDef(string n)=>DefDatabase<TraitDef>.GetNamedSilentFail(n);
  static bool TrIs(string n,int degree,int tier,string mark)=>TraitRarity.Tier(TrDef(n),degree)==tier && TraitRarity.Mark(TrDef(n),degree)==mark;

  void CheckTraitRarity(){
   // 1. 所有 LP_ 特性都挂了稀有度
   var lp=DefDatabase<TraitDef>.AllDefs.Where(d=>d.defName.StartsWith("LP_")).ToList();
   var missing=lp.Where(d=>TraitRarity.Ext(d)==null).Select(d=>d.defName).ToList();
   Check(lp.Count>=77 && missing.Count==0,"every LP_ trait has a rarity ("+lp.Count+" traits"+(missing.Count>0?", missing: "+string.Join(",",missing):"")+")");
   // 2. 名字颜色和档位一致：普通档不上色，其余带对应色值；去掉颜色后名字不空
   var bad=new List<string>();
   foreach(var d in lp)foreach(var x in d.degreeDatas){
    var hex=TraitRarity.Hex(d,x.degree);bool ok=hex==null?!x.label.Contains("<color"):x.label.StartsWith("<color="+hex+">");
    if(!ok || x.label.StripTags().NullOrEmpty())bad.Add(d.defName+"("+x.degree+")");
   }
   Check(bad.Count==0,"label colors follow the tier table"+(bad.Count>0?": "+string.Join(",",bad.Take(12)):""));
   // 3. 主要分档
   int C=0,U=1,R=2,E=3,L=4,M=5;
   bool tiers=TrIs("LP_Prophesied",0,M,null) && TrIs("LP_DivineChampion",3,M,null) && TrIs("LP_HeroicSpark",0,L,null) && TrIs("LP_Paragon",1,L,null) && TrIs("LP_DivineInfusion",2,L,null)
    && TrIs("LP_TalentGifted",0,E,null) && TrIs("LP_Diligent",0,E,null) && TrIs("LP_DefyFate",0,E,null)
    && TrIs("LP_GiftedScore",4,R,null) && TrIs("LP_Duelist",0,R,null) && TrIs("LP_CG_Flurry",0,R,null) && TrIs("LP_CG_Mighty",0,R,null)
    && TrIs("LP_Background_Sage",0,U,null) && TrIs("LP_Tactician",0,U,null) && TrIs("LP_Order",-1,C,null) && TrIs("LP_Devotee",4,C,null);
   Check(tiers,"tiers: 天命 神话, 英雄之资 / 极致 / 神力灌注 传说, 四个稀有天赋 / 勤勉 / 反骨 史诗, 天赋·X / 规则型 / 武艺 稀有, 背景 / 职业天赋 优秀, 阵营 / 信徒 普通");
   // 4. 同一特性的两端、诅咒和事迹的标记
   bool split=TrIs("LP_DragonStance",-1,C,null) && TrIs("LP_DragonStance",1,U,null) && TrIs("LP_GiantStance",-1,U,null) && TrIs("LP_UndeadStance",1,C,null)
    && TrIs("LP_Revenant",0,E,"Curse") && TrIs("LP_Revenant",1,U,null) && TrIs("LP_FarRealmTouched",0,R,"Curse") && TrIs("LP_Lycanthrope",2,E,"Curse")
    && TrIs("LP_DeedDragonslayer",0,R,"Deed") && TrIs("LP_MindFortress",0,E,"Deed") && TrIs("LP_DeathReturned",0,U,"Deed");
   Check(split && TraitRarity.Hex(TrDef("LP_Revenant"),0)==TraitRarity.CurseHex && TraitRarity.Hex(TrDef("LP_Revenant"),1)=="#7FBF6A" && TraitRarity.Hex(TrDef("LP_MindFortress"),0)==TraitRarity.DeedHex,"spectrum ends and curse / deed marks: 惧龙 普通 vs 屠龙志士 优秀, 复仇亡魂 红 (史诗) → 安息之魂 绿, 事迹 青");
   // 5. 悬停标头
   string h1=TraitRarity.Header(TrDef("LP_CG_Lethal"),0),h2=TraitRarity.Header(TrDef("LP_Revenant"),0),h3=TraitRarity.Header(TrDef("LP_Revenant"),1),h4=TraitRarity.Header(TrDef("LP_DeedGodslayer"),0),h5=TraitRarity.Header(TrDef("LP_Order"),1);
   Check(h1=="<color=#5B9BD5>〔稀有 · 武艺天赋〕</color>" && h2=="<color=#D05050>〔诅咒 · 史诗〕</color>" && h3=="<color=#7FBF6A>〔优秀 · 诅咒之力（已解除）〕</color>" && h4=="<color=#50C0C0>〔事迹 · 史诗〕</color>" && h5=="〔普通 · 性格特性〕","tooltip headers: "+h1+" / "+h2+" / "+h3+" / "+h4+" / "+h5);
   // 6. 实际悬停说明第一行就是标头，原来的描述和额外说明还在
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:XenotypeDefOf.Baseliner,fixedBiologicalAge:25f,fixedChronologicalAge:25f));
   try{
    var t=new Trait(TrDef("LP_HeroicSpark"),0,true);p.story.traits.GainTrait(t);
    var tip=t.TipString(p);
    Check(tip.StartsWith("<color=#E08A3A>〔传说 · 强力特性〕</color>\n") && tip.Contains("英雄之资") && tip.Length>60,"英雄之资 tooltip starts with the orange header and keeps its description ("+tip.Length+" chars)");
   }finally{if(!p.Destroyed)p.Destroy();}
   // 7. 1.66.0 热修：契约种族登记了亲和——行星使、狂猎首领传奇，其余九个中等
   var leg=new HashSet<string>{"LP_Planetar","LP_WildHuntLord"};var pact=new[]{"LP_Planetar","LP_WildHuntLord","LP_ChainDevil","LP_BoneDevil","LP_HornedDevil","LP_Erinyes","LP_Amnizu","LP_Dryad","LP_Redcap","LP_WildHunter","LP_Deva"};
   var wrong=new List<string>();
   foreach(var n in pact){
    var x=DefDatabase<XenotypeDef>.GetNamedSilentFail(n);var a=Affinity.FromExt(x);
    if(a==null || a.legendary!=leg.Contains(n)){wrong.Add(n+"(ext)");continue;}
    var q=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,null,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,forcedXenotype:x,fixedBiologicalAge:25f,fixedChronologicalAge:25f));
    try{if(Affinity.For(q).key!=n || HeroTraits.TierOf(q)!=(leg.Contains(n)?HeroTraits.Tier.Legendary:HeroTraits.Tier.Medium))wrong.Add(n+"("+Affinity.For(q).key+"/"+HeroTraits.TierOf(q)+")");}
    finally{if(!q.Destroyed)q.Destroy();}
   }
   Check(wrong.Count==0,"pact races: 行星使 / 狂猎首领 传奇, 其余九个中等"+(wrong.Count>0?": "+string.Join(",",wrong):""));
  }
 }
}
