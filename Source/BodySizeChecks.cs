using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // 1.23.0 巨躯规则检查（规格 D 节）。
 public partial class LichChecks {
  static readonly Dictionary<string,float[]> sizeSnapshots=new Dictionary<string,float[]>();
  static string sizeWoundedId;static float[] sizeWoundRatios;
  static float Melee(Pawn p)=>p.GetStatValue(StatDefOf.MeleeDamageFactor,true,-1);
  static void SetSizeRule(bool on,int strength){LichMod.Settings.bodySizeRule=on;LichMod.Settings.bodySizeStrength=strength;}
  // 巨躯规则带来的近战倍率：同一角色开/关规则时的实际属性之比。
  static float MeleeRatio(Pawn p){bool on=LichMod.Settings.bodySizeRule;float with=Melee(p);LichMod.Settings.bodySizeRule=false;float without=Melee(p);LichMod.Settings.bodySizeRule=on;return with/without;}
  static float VanillaScale(Pawn p)=>BodySize.Measure(BodySize.Rule.Legacy,()=>p.HealthScale);
  static float LegacyMax(BodyPartRecord part,Pawn p)=>BodySize.Measure(BodySize.Rule.Legacy,()=>PartHealth.Maximum(part,p));
  static BodyPartRecord SizePart(Pawn p,string def)=>p.RaceProps.body.AllParts.First(x=>x.def.defName==def);
  // RacePawn 已经生成在地图上；普通人另外放置。
  Pawn SizePawn(string xenotype){var p=xenotype!=null?RacePawn(xenotype):SharedPawn();p.jobs?.StopAll();return p;}
  static float[] SizeSnapshot(Pawn p)=>new[]{p.HealthScale,Melee(p),BodySize.HealthFactor(p),BodySize.MeleeFactor(p)};
  static float[] WoundRatios(Pawn p)=>p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h=>h.Part!=null).OrderBy(h=>h.Part.Index).ThenBy(h=>h.loadID).Select(h=>h.Severity/PartHealth.Maximum(h.Part,p)).ToArray();
  // 设为旧伤疤：不流血、不自然愈合，存读档前后比例可以精确比较。
  static Hediff_Injury Wound(Pawn p,BodyPartRecord part,float severity){var h=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,p,part);h.Severity=severity;var scar=h.TryGetComp<HediffComp_GetsPermanent>();if(scar!=null)scar.IsPermanent=true;p.health.AddHediff(h);return h;}

  void CheckBodySize(){
   bool priorOn=LichMod.Settings.bodySizeRule;int priorStrength=LichMod.Settings.bodySizeStrength;
   try{SetSizeRule(true,100);Necro.Registry.MigrateBodySize(new Pawn[0]);RunBodySize();}finally{SetSizeRule(priorOn,priorStrength);Necro.Registry.MigrateBodySize(new Pawn[0]);}
  }
  void RunBodySize(){
   var registry=Necro.Registry;
   Check(registry.bodySizeVersion==1 && registry.bodySizeOn && registry.bodySizeStrength==100 && LichMod.Settings.bodySizeRule,"running game records the giant-body rule version and settings");
   Check(StatDefOf.MeleeDamageFactor.parts.Count(x=>x is StatPart_BodySizeMelee)==1,"giant-body melee stat part is registered exactly once");

   // 1. 成年火巨人：HealthScale ×1.5，各部位生命与 1.22.1 相同，近战 ×1.3。
   var giant=SizePawn("LP_FireGiant");
   Check(Near(BodySize.Size(giant),2f) && Near(BodySize.HealthFactor(giant),1.5f) && Near(giant.HealthScale,VanillaScale(giant)*1.5f),"adult fire giant health scale is x1.5: "+giant.HealthScale);
   int tolerance=FullModList?1:0;
   Check(giant.RaceProps.body.AllParts.All(x=>Math.Abs(PartHealth.Maximum(x,giant)-LegacyMax(x,giant))<=tolerance),"adult fire giant part maxima equal the 1.22.1 values");
   Check(Near(BodySize.MeleeFactor(giant),1.3f) && Near(MeleeRatio(giant),1.3f),"adult fire giant melee damage factor is x1.3: "+MeleeRatio(giant));
   Check(giant.GetStatValue(StatDefOf.MeleeDamageFactor)>0 && StatDefOf.MeleeDamageFactor.Worker.GetExplanationFull(StatRequest.For(giant),ToStringNumberSense.Factor,Melee(giant)).Contains("巨躯：×130%"),"melee damage explanation shows the giant-body factor");

   // 2. 巨魔、牛头人、矮人、普通人。
   var troll=SizePawn("LP_Troll");var minotaur=SizePawn("LP_Minotaur");var dwarf=SizePawn("LP_Dwarf");var human=SizePawn(null);
   Check(Near(BodySize.HealthFactor(troll),1.25f) && Near(troll.HealthScale,VanillaScale(troll)*1.25f) && Near(MeleeRatio(troll),1.15f),"adult troll: health x1.25, melee x1.15");
   Check(Near(BodySize.HealthFactor(minotaur),1.15f) && Near(minotaur.HealthScale,VanillaScale(minotaur)*1.15f) && Near(MeleeRatio(minotaur),1.09f),"minotaur: health x1.15, melee x1.09");
   Check(Near(BodySize.Size(dwarf),.8f) && Near(BodySize.HealthFactor(dwarf),1f) && Near(MeleeRatio(dwarf),1f),"dwarf below size 1 loses no health or melee");
   Check(Near(BodySize.HealthFactor(human),1f) && Near(human.HealthScale,VanillaScale(human)) && Near(MeleeRatio(human),1f),"baseliner human is unchanged");
   var trollArm=SizePart(troll,"Arm");Check(Near(troll.health.hediffSet.GetPartHealth(trollArm),Mathf.CeilToInt(trollArm.def.hitPoints*troll.HealthScale)),"troll native arm health follows the scaled health: "+troll.health.hediffSet.GetPartHealth(trollArm));
   var child=RacePawn("LP_FireGiant",9);float childOld=LegacyMax(child.RaceProps.body.corePart,child),childNew=PartHealth.Maximum(child.RaceProps.body.corePart,child);
   Check(BodySize.HealthFactor(child)>1f && BodySize.HealthFactor(child)<1.5f && childNew<childOld,"fire giant child health grows with age instead of a flat x1.5: "+childNew+" < "+childOld);child.Destroy();

   // 3. 龙体：只取 ×1.5，与体型因子取较大值；火巨人+龙体不再是 ×2.25。
   var dragonDef=DefDatabase<XenotypeDef>.GetNamed("LP_ForceDragon");var dragon=GenerateDragon(dragonDef);GenSpawn.Spawn(dragon,CellFinder.RandomClosewalkCellNear(map.Center,map,12),map);
   Check(Near(BodySize.HealthFactor(dragon),1.5f) && Near(dragon.HealthScale,VanillaScale(dragon)*1.5f) && Near(MeleeRatio(dragon),1f),"wyrmling dragon body is x1.5 health and no size melee bonus");
   ForceDragon.Heart(dragon).SetAge(ForceDragon.MaxAge);
   Check(Near(BodySize.Size(dragon),1.2f) && Near(BodySize.HealthFactor(dragon),1.5f) && Near(MeleeRatio(dragon),1.06f),"great wyrm: dragon body x1.5 is not multiplied by its x1.1 size health; melee x1.06");
   var hybrid=SizePawn("LP_FireGiant");hybrid.genes.AddGene(ForceDragon.BodyGene,false);var torso=hybrid.RaceProps.body.corePart;
   Check(Near(BodySize.HealthFactor(hybrid),1.5f) && Near(PartHealth.Maximum(torso,hybrid),Mathf.Ceil(torso.def.hitPoints*VanillaScale(hybrid)*1.5f)) && Near(LegacyMax(torso,hybrid),Mathf.Ceil(Mathf.CeilToInt(torso.def.hitPoints*VanillaScale(hybrid))*2.25f)),"fire giant with dragon body takes the larger x1.5, not the old x2.25");

   // 5. 巫妖强化层仍叠在新的 HealthScale 之上。
   var lichTroll=RacePawn("LP_Troll");lichTroll.health.AddHediff(Necro.Def<HediffDef>("Lich"));var arm=SizePart(lichTroll,"Arm");var limb=lichTroll.health.AddHediff(Necro.Def<HediffDef>("Limb"),arm);limb.Severity=2;
   Check(Near(PartHealth.Maximum(arm,lichTroll),Mathf.Ceil(Mathf.CeilToInt(arm.def.hitPoints*lichTroll.HealthScale)*1.4f)) && Near(BodySize.HealthFactor(lichTroll),1.25f),"lich reinforcement layers compose on top of the giant-body health scale");lichTroll.Destroy();

   // 6. 设置：强度0%与关闭都回到旧的 ×1.5（取较大值）且没有近战加成；200% 时加成翻倍。
   foreach(var off in new[]{(true,0),(false,100)}){
    SetSizeRule(off.Item1,off.Item2);
    Check(Near(BodySize.HealthFactor(giant),1.5f) && Near(BodySize.HealthFactor(hybrid),1.5f) && Near(BodySize.HealthFactor(dragon),1.5f) && Near(BodySize.HealthFactor(troll),1f) && Near(BodySize.HealthFactor(minotaur),1f) && Near(BodySize.MeleeFactor(giant),1f) && Near(BodySize.MeleeFactor(troll),1f),"rule "+(off.Item1?"at 0%":"switched off")+": fire giant and dragon body keep legacy x1.5, no size bonus");
   }
   SetSizeRule(true,50);
   Check(Near(BodySize.HealthFactor(giant),1.5f) && Near(BodySize.HealthFactor(hybrid),1.5f) && Near(BodySize.HealthFactor(troll),1.125f) && Near(BodySize.HealthFactor(minotaur),1.075f) && Near(BodySize.MeleeFactor(giant),1.15f) && Near(BodySize.MeleeFactor(troll),1.075f),"strength 50%: adult fire giant health stays at its x1.5 floor while size bonuses halve");
   SetSizeRule(true,200);
   Check(Near(BodySize.HealthFactor(giant),2f) && Near(BodySize.HealthFactor(troll),1.5f) && Near(BodySize.HealthFactor(minotaur),1.3f) && Near(BodySize.MeleeFactor(giant),1.6f) && Near(BodySize.MeleeFactor(troll),1.3f) && Near(BodySize.MeleeFactor(minotaur),1.18f),"strength 200% doubles the size bonuses");
   SetSizeRule(true,100);

   // 7. 伤口缩放：模拟 1.22.1 存档载入（火巨人+龙体 2.25→1.5），然后调低强度。
   var parts=new[]{SizePart(hybrid,"Arm"),SizePart(hybrid,"Leg"),hybrid.RaceProps.body.corePart};
   BodySize.Measure(BodySize.Rule.Legacy,()=>{foreach(var part in parts)Wound(hybrid,part,PartHealth.Maximum(part,hybrid)*(part==torso?.3f:.6f));return 0;});
   var legacyRatios=BodySize.Measure(BodySize.Rule.Legacy,()=>WoundRatios(hybrid));
   registry.bodySizeVersion=0;int scaled=registry.MigrateBodySize(new[]{hybrid});var loaded=WoundRatios(hybrid);
   Check(scaled==1 && registry.bodySizeVersion==1 && legacyRatios.Length==3 && legacyRatios.Zip(loaded,(a,b)=>Math.Abs(a-b)<.001f).All(x=>x),"1.22.1 save migration keeps every wound ratio: "+string.Join(",",loaded.Select(x=>x.ToString("0.###"))));
   Check(!hybrid.Dead && parts.All(x=>!hybrid.health.hediffSet.PartIsMissing(x)),"1.22.1 save migration destroys no part and kills nobody");
   Check(registry.MigrateBodySize(new[]{hybrid})==0,"migration runs once: unchanged settings rescale nothing");
   var trollWound=Wound(troll,trollArm,PartHealth.Maximum(trollArm,troll)*.5f);var trollRatio=WoundRatios(troll);var giantWound=Wound(giant,SizePart(giant,"Leg"),PartHealth.Maximum(SizePart(giant,"Leg"),giant)*.5f);var giantRatio=WoundRatios(giant);var hybridRatio=WoundRatios(hybrid);
   var sized=new[]{troll,giant,hybrid};SetSizeRule(true,50);Necro.Registry.MigrateBodySize(sized);
   Check(Near(BodySize.HealthFactor(troll),1.125f) && trollRatio.Zip(WoundRatios(troll),(a,b)=>Math.Abs(a-b)<.001f).All(x=>x) && giantRatio.Zip(WoundRatios(giant),(a,b)=>Math.Abs(a-b)<.001f).All(x=>x),"lowering strength rescales wounds when maxima fall");
   Check(hybridRatio.Zip(WoundRatios(hybrid),(a,b)=>Math.Abs(a-b)<.001f).All(x=>x) && !troll.Dead && !giant.Dead && !hybrid.Dead,"dragon body maximum is unchanged by strength and nobody dies");
   float trollSeverity=trollWound.Severity;SetSizeRule(true,100);Necro.Registry.MigrateBodySize(sized);
   Check(Near(trollWound.Severity,trollSeverity) && Necro.Registry.bodySizeStrength==100,"raising strength leaves wounds unchanged (more health, no free heal)");
   troll.health.RemoveHediff(trollWound);giant.health.RemoveHediff(giantWound);

   // 胃容量：体型变化后最大饱食立即刷新，超出部分截掉。
   var eater=SizePawn(null);float stomach=eater.needs.food.MaxLevel;eater.needs.food.CurLevel=stomach;
   eater.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_FireGiantCore"),false);
   // 1.27.1：原版体型×2，再乘巨胃×2；巨胃部分按比例放大饱食，所以百分比只因原版体型减半。
   Check(Near(eater.needs.food.MaxLevel,stomach*4f) && Near(eater.needs.food.CurLevelPercentage,.5f),"stomach size refreshes immediately after the body grows: "+stomach+" to "+eater.needs.food.MaxLevel+" ("+eater.needs.food.CurLevelPercentage.ToStringPercent()+")");
   eater.needs.food.CurLevel=eater.needs.food.MaxLevel;eater.genes.RemoveGene(eater.genes.GetGene(DefDatabase<GeneDef>.GetNamed("LP_FireGiantCore")));
   Check(Near(eater.needs.food.MaxLevel,stomach) && eater.needs.food.CurLevel<=eater.needs.food.MaxLevel+.001f,"stomach shrinks with the body and food is capped to the new maximum");
   eater.Destroy();human.Destroy();dwarf.Destroy();

   // 8. 存读档后数值不变（AfterLoad 比较）。
   foreach(var p in new[]{giant,troll,minotaur,dragon,hybrid})sizeSnapshots[p.ThingID]=SizeSnapshot(p);
   sizeWoundedId=hybrid.ThingID;sizeWoundRatios=WoundRatios(hybrid);
  }
  // 4. 死灵造物：生命与近战与 1.22.1 完全相同。
  void CheckBodySizeConstructs(params Pawn[] constructs){
   foreach(var p in constructs){
    Check(Near(BodySize.HealthFactor(p),1f) && Near(BodySize.MeleeFactor(p),1f) && Near(p.HealthScale,VanillaScale(p)) && Near(MeleeRatio(p),1f),Necro.Construct(p).kind+" construct is exempt from the giant-body rule");
    Check(p.RaceProps.body.AllParts.All(x=>Near(PartHealth.Maximum(x,p),LegacyMax(x,p))),Necro.Construct(p).kind+" construct part maxima equal the 1.22.1 values");
   }
  }
  // 放在 AfterLoad 最前面，比较完就移除测试角色，免得被后面的检查选作工人。
  void CheckBodySizeAfterLoad(){
   map=Find.CurrentMap;var sized=new List<Pawn>();
   Check(Necro.Registry.bodySizeVersion==1 && Necro.Registry.RecordedBodySizeRule.Same(BodySize.ActiveRule),"giant-body rule record survives save/load");
   foreach(var entry in sizeSnapshots){
    var p=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==entry.Key);sized.Add(p);var now=SizeSnapshot(p);
    Check(now.Zip(entry.Value,(a,b)=>Math.Abs(a-b)<.001f).All(x=>x),"health scale and melee factor survive save/load for "+p.LabelShort+": "+string.Join(",",now.Select(x=>x.ToString("0.###"))));
   }
   var wounded=map.mapPawns.AllPawnsSpawned.Single(x=>x.ThingID==sizeWoundedId);
   Check(sizeWoundRatios.Zip(WoundRatios(wounded),(a,b)=>Math.Abs(a-b)<.001f).All(x=>x),"loading a 1.23.0 save does not rescale wounds again");foreach(var p in sized)p.Destroy();
  }
 }
}
