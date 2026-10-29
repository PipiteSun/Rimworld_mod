using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace LichPhylactery {
 // 1.29.0 DND 枪械与法杖自检（Test.ps1 -Only Guns 单跑）：定义与数值、配方与研究、魔力系数公式、法杖拒绝施放与心灵熵、矮人保底、山岳之怒、噩耗穿透、雷鸣铳 EMP、
 // 目标侧倍率、黑曜石、天火同步、敌人装备设置、商人与宝箱、存读档。-Isekai 档另跑 CheckGunsIsekai（法杖 + Isekai 武器精炼）。
 public partial class LichChecks {
  static string gunSaveId;
  // 规格表：伤害、穿甲、射程、瞄准、冷却（魔力系数 1.0 时）。天火降世单独和电荷长枪比。
  static readonly Dictionary<string,(int dmg,float ap,float range,float warmup,float cooldown)> GunSpec=new Dictionary<string,(int,float,float,float,float)>{
   {"LP_SilverPistol",(14,.20f,24,.6f,1.6f)},{"LP_LavaPiercer",(22,.45f,30,1.4f,2.6f)},{"LP_ThunderGun",(16,.15f,26,1f,2f)},
   {"LP_Doom",(45,.70f,48,3.5f,5f)},
   {"LP_RunicMusket",(18,.30f,32,1.6f,2.4f)},{"LP_RunicPistol",(13,.20f,22,-1f,1.5f)},{"LP_HammerCannon",(14,-1f,-1f,-1f,-1f)},{"LP_RunicRepeater",(11,.18f,28,-1f,2.6f)},{"LP_MountainWrath",(34,.60f,42,2.8f,4f)},
   {"LP_MissileStaff",(6,-1f,26,-1f,2.2f)},{"LP_FireStaff",(16,-1f,28,-1f,2.4f)},{"LP_FrostStaff",(12,-1f,26,-1f,2f)}};
  static ThingDef GunDef(string n)=>DefDatabase<ThingDef>.GetNamed(n);
  static int BaseDamage(ThingDef proj)=>Traverse.Create(proj.projectile).Field("damageAmountBase").GetValue<int>();
  Pawn GunPawn(IntVec3 at,float? sensitivityTrait=null){
   var p=SharedPawn();Place(p,at);p.jobs.StopAll();
   foreach(var t in p.story.traits.allTraits.Where(t=>t.def.defName=="PsychicSensitivity").ToList())p.story.traits.RemoveTrait(t);
   if(sensitivityTrait!=null)p.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed("PsychicSensitivity"),(int)sensitivityTrait.Value,true));
   return p;
  }
  ThingWithComps Arm(Pawn p,string weapon){var w=(ThingWithComps)ThingMaker.MakeThing(GunDef(weapon),GenStuff.DefaultStuffFor(GunDef(weapon)));p.equipment.DestroyAllEquipment();p.equipment.AddEquipment(w);return w;}
  static Faction GunEnemyFaction=>Find.FactionManager.AllFactionsListForReading.First(f=>f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && !f.def.hidden);
  Projectile Fire(Pawn shooter,ThingWithComps weapon,LocalTargetInfo target){
   var proj=(Projectile)GenSpawn.Spawn(weapon.def.Verbs[0].defaultProjectile,shooter.Position,map);
   proj.Launch(shooter,shooter.DrawPos,target,target,ProjectileHitFlags.None,false,weapon);return proj;
  }

  void CheckGuns(){
   var made=new List<Thing>();
   try{
    CheckGunDefs();CheckGunRecipes();CheckGunFormula();CheckGunStaves(made);CheckGunDwarves(made);CheckGunEffects(made);CheckGunSources(made);
   }finally{foreach(var t in made)if(!t.Destroyed)t.Destroy();map.GetComponent<GunMap>()?.Clear();}
  }

  void CheckGunDefs(){
   Check(Arcana.GroupD.Length==3 && Arcana.GroupE.Length==2 && Arcana.GroupF.Length==5 && Arcana.GroupG.Length==3 && Arcana.All.Distinct().Count()==13,"13 DND weapons: D3 + E2 + F5 + G3");
   foreach(var (names,group) in new[]{(Arcana.GroupD,"D"),(Arcana.GroupE,"E"),(Arcana.GroupF,"F"),(Arcana.GroupG,"G")})foreach(var n in names){
    var d=DefDatabase<ThingDef>.GetNamedSilentFail(n);
    Check(d!=null && !d.ConfigErrors().Any() && d.IsRangedWeapon && Arcana.Group(d)==group,"weapon definition loads in group "+group+": "+n+(d==null?"":" "+string.Join(";",d.ConfigErrors())+" ranged="+d.IsRangedWeapon+" group="+Arcana.Group(d)));
    Check(d.graphic!=null && d.graphic!=BaseContent.BadGraphic && d.graphic.MatSingle!=BaseContent.BadMat && d.uiIcon!=null && d.uiIcon!=BaseContent.BadTex && d.graphicData.texPath=="LP/Weapons/"+n.Substring(3),"weapon has its own 64px texture: "+n);
    Check(d.uiIcon.width==64 && d.uiIcon.height==64,"weapon texture is 64x64: "+n);
    var v=d.Verbs[0];bool magic=Arcana.Magic(group);
    Check(magic==(v.verbClass==typeof(Verb_ArcaneShoot)) && (magic || v.verbClass==typeof(Verb_Shoot)),"weapon uses the "+(magic?"arcane":"plain")+" shooting verb: "+n);
    Check(v.defaultProjectile!=null && (v.defaultProjectile.thingClass==typeof(LP_ArcaneBullet) || v.defaultProjectile.thingClass==typeof(LP_ArcaneShell)),"weapon fires the shared arcane projectile class: "+n);
    Check(d.weaponTags.Contains("LP_ArcaneGun") && !d.weaponTags.Contains("Gun"),"random raiders do not spawn with "+n);
    Check(d.HasComp(typeof(CompQuality)),"weapon has quality: "+n);
    if(GunSpec.TryGetValue(n,out var s)){
     var pp=v.defaultProjectile.projectile;
     Check(BaseDamage(v.defaultProjectile)==s.dmg,n+" damage "+s.dmg);
     if(s.ap>=0)Check(Near(Traverse.Create(pp).Field("armorPenetrationBase").GetValue<float>(),s.ap),n+" armor penetration "+s.ap.ToStringPercent());
     if(s.range>=0)Check(Near(v.range,s.range),n+" range "+s.range);
     if(s.warmup>=0)Check(Near(v.warmupTime,s.warmup),n+" aim time "+s.warmup+"s");
     if(s.cooldown>=0)Check(Near(d.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown),s.cooldown),n+" cooldown "+s.cooldown+"s");
    }
   }
   Check(GunDef("LP_RunicRepeater").Verbs[0].burstShotCount==3 && GunDef("LP_MissileStaff").Verbs[0].burstShotCount==3,"repeater and missile staff fire three-shot bursts");
   var shell=GunDef("LP_HammerCannon").Verbs[0].defaultProjectile;
   Check(shell.thingClass==typeof(LP_ArcaneShell) && Near(shell.projectile.explosionRadius,1.9f) && BaseDamage(shell)==14 && shell.GetModExtension<ArcaneShotExt>()?.stunTicks==60,"hammer cannon shell: radius 1.9, damage 14, stun 1 second");
   Check(GunDef("LP_MissileStaff").Verbs[0].defaultProjectile.GetModExtension<ArcaneShotExt>()?.alwaysHit==true,"magic missiles always hit");
   Check(GunDef("LP_FrostStaff").Verbs[0].defaultProjectile.projectile.damageDef.defName=="LP_Frost" && Near(Items.Hediff("LP_FrostStaffChill")?.stages[0].statFactors.GetStatFactorFromList(StatDefOf.MoveSpeed)??0f,.6f),"frost staff deals frost damage and slows by 40% ("+GunDef("LP_FrostStaff").Verbs[0].defaultProjectile.projectile.damageDef?.defName+", "+(Items.Hediff("LP_FrostStaffChill")?.stages?.Count??-1)+", "+string.Join("|",Items.Hediff("LP_FrostStaffChill").stages[0].statFactors?.Select(m=>m.stat.defName+"="+m.value)??new string[0])+" "+Items.Hediff("LP_FrostStaffChill").stages[0].statFactors.GetStatFactorFromList(StatDefOf.MoveSpeed)+")");
   foreach(var n in Arcana.GroupG){var d=GunDef(n);Check(d.tools.Any(t=>t.capacities.Contains(DefDatabase<ToolCapacityDef>.GetNamed("Blunt")) && Near(t.power,9f)),"staff doubles as a 9-damage blunt weapon: "+n);}
   // 天火降世 = 电荷长枪的伤害、穿甲、射程，瞄准和冷却各少 30%。
   var lance=GunDef("Gun_ChargeLance");var sky=GunDef("LP_Skyfire");
   Check(GunStartup.SyncSkyfire(),"skyfire syncs with the vanilla charge lance");
   Check(BaseDamage(sky.Verbs[0].defaultProjectile)==BaseDamage(lance.Verbs[0].defaultProjectile) && Near(sky.Verbs[0].defaultProjectile.projectile.GetArmorPenetration((Thing)null,null),lance.Verbs[0].defaultProjectile.projectile.GetArmorPenetration((Thing)null,null)) && Near(sky.Verbs[0].range,lance.Verbs[0].range),"skyfire damage, penetration and range equal the charge lance");
   Check(Near(sky.Verbs[0].warmupTime,lance.Verbs[0].warmupTime*.7f) && Near(sky.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown),lance.GetStatValueAbstract(StatDefOf.RangedWeapon_Cooldown)*.7f),"skyfire aims and cools down 30% faster than the charge lance");
   Check(sky.Verbs[0].defaultProjectile.projectile.damageDef==DamageDefOf.Flame && Near(sky.Verbs[0].defaultProjectile.GetModExtension<ArcaneShotExt>().igniteChance,.15f),"skyfire burns with 15% ignite chance");
   Check(Troll.Burns(GunDef("LP_LavaPiercer").Verbs[0].defaultProjectile.projectile.damageDef),"lava piercer counts as fire for troll regeneration");
  }

  void CheckGunRecipes(){
   var tab=DefDatabase<ResearchTabDef>.GetNamedSilentFail("LP_FireGiantTech");
   var firearms=DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LP_RunicFirearms");var staves=DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LP_ArcaneStaves");
   Check(tab!=null && tab.label=="DND边缘大陆" && firearms?.tab==tab && staves?.tab==tab,"research tab DND边缘大陆 holds both projects");
   Check(Near(firearms.baseCost,1200) && firearms.prerequisites.Any(r=>r.defName=="LP_DNDAlchemy") && firearms.prerequisites.Any(r=>r.defName=="Smithing"),"runic firearms: 1200 points after alchemy and smithing");
   Check(Near(staves.baseCost,1000) && staves.prerequisites.Count==1 && staves.prerequisites[0].defName=="LP_DNDAlchemy","arcane staves: 1000 points after alchemy");
   foreach(var n in Arcana.All){
    var recipes=DefDatabase<RecipeDef>.AllDefsListForReading.Where(r=>r.ProducedThingDef?.defName==n).ToList();
    if(Arcana.GroupE.Contains(n)){Check(recipes.Count==0,n+" cannot be crafted");continue;}
    Check(recipes.Count==1,n+" has exactly one recipe");var r=recipes[0];
    if(Arcana.GroupG.Contains(n))Check(r.researchPrerequisite==staves && r.AllRecipeUsers.Any(u=>u.defName=="LP_EnchantingTable") && r.AllRecipeUsers.Count()==1,n+" is made at the enchanting table after arcane staves");
    // 1.63.0：契约（PactRaces）把符文铁砧的所有配方也加给了地狱铸炉，检查原来要求只有一个工作台，过时。
    else Check(r.researchPrerequisite==firearms && r.AllRecipeUsers.Any(u=>u.defName=="LP_RuneAnvil") && r.AllRecipeUsers.All(u=>u.defName=="LP_RuneAnvil" || u==Pacts.ForgeDef),n+" is made at the rune anvil (or the hell forge, which shares its recipes) after runic firearms");
   }
   int Cost(string weapon,string mat)=>GunDef(weapon).CostList?.FirstOrDefault(c=>c.thingDef.defName==mat)?.count??0;
   Check(Cost("LP_SilverPistol","Silver")==60 && Cost("LP_SilverPistol","Steel")==30 && Cost("LP_SilverPistol","ComponentIndustrial")==2,"silver pistol costs silver 60, steel 30, components 2");
   Check(Cost("LP_RunicMusket","LP_Mithral")==8 && Cost("LP_RunicMusket","WoodLog")==20 && Cost("LP_MountainWrath","LP_Adamantine")==15 && Cost("LP_MountainWrath","LP_Mithral")==15 && Cost("LP_MountainWrath","Steel")==140,"runic weapons use mithral and adamantine");
   Check(Cost("LP_FrostStaff","LP_ColdIron")==10 && Cost("LP_FireStaff","LP_LavaCrystal")==6 && Cost("LP_MissileStaff","Silver")==20,"staves use cold iron, lava crystal and silver");
   // 黑曜石：引用 Odyssey 的 Obsidian，不自建。
   var obsidian=DefDatabase<ThingDef>.GetNamedSilentFail("Obsidian");
   Check(DefDatabase<ThingDef>.GetNamedSilentFail("LP_Obsidian")==null,"no home-made obsidian item");
   if(ModsConfig.OdysseyActive){
    Check(obsidian!=null && obsidian.modContentPack?.PackageId=="ludeon.rimworld.odyssey","obsidian comes from the Odyssey DLC ("+obsidian?.modContentPack?.Name+")");
    Check(Cost("LP_LavaPiercer","Obsidian")==10 && Cost("LP_LavaPiercer","LP_LavaCrystal")==6 && Cost("LP_LavaPiercer","Steel")==70 && Cost("LP_LavaPiercer","ComponentIndustrial")==3,"lava piercer costs steel 70, components 3, lava crystal 6, obsidian 10");
   }else Check(Cost("LP_LavaPiercer","Obsidian")==0,"without Odyssey the lava piercer recipe skips obsidian");
  }

  void CheckGunFormula(){
   float[] s={0f,.5f,1f,2f,3f,5f};float[] g={0f,.5f,1f,2f,3f,5f};float[] f={.5f,.75f,1f,1.5f,2f,2f};
   for(int i=0;i<s.Length;i++){
    Check(Near(Arcana.Factor("G",s[i]),g[i]),"staff factor at sensitivity "+s[i].ToStringPercent()+" is x"+g[i]);
    Check(Near(Arcana.Factor("F",s[i]),f[i]),"dwarf gun factor at sensitivity "+s[i].ToStringPercent()+" is x"+f[i]);
    Check(Arcana.Factor("D",s[i])==1f && Arcana.Factor("E",s[i])==1f,"groups D/E ignore sensitivity "+s[i].ToStringPercent());
   }
   Check(Near(Arcana.Factor("G",.1f),.2f),"staff factor never drops below 0.2 while sensitivity is above 0");
   Check(Arcana.Scaled(16,.7f)==11 && Arcana.Scaled(1,.2f)==1 && Arcana.Scaled(16,0f)==0,"scaled damage rounds and stays at least 1");
  }

  void CheckGunStaves(List<Thing> made){
   var spot=ItemSpot(3);
   // 敏感度 0：法杖毫无回应，按钮灰掉，AI 不捡。
   var deaf=GunPawn(spot,-2);made.Add(deaf);
   Check(Near(Arcana.RawSensitivity(deaf),0f),"psychically deaf fixture has 0% sensitivity");
   var staff=Arm(deaf,"LP_FireStaff");var verb=deaf.equipment.PrimaryEq.PrimaryVerb;
   bool fires=Arcana.CanFire(deaf,staff.def,out var why);
   Check(verb is Verb_ArcaneShoot && !verb.Available() && !fires && why.Contains("法杖毫无回应"),"a staff refuses a psychically deaf wielder: "+why);
   if(deaf.drafter!=null)deaf.drafter.Drafted=true;
   var cmd=deaf.equipment.PrimaryEq.GetVerbsCommands().OfType<Command_VerbTarget>().FirstOrDefault();
   Check(cmd!=null && cmd.Disabled && cmd.disabledReason.Contains("法杖毫无回应"),"the staff attack button is greyed out with the reason ("+(cmd==null?"no command":cmd.Disabled+" "+cmd.disabledReason)+"; commands "+deaf.equipment.PrimaryEq.GetVerbsCommands().Count()+")");
   var loose=ThingMaker.MakeThing(GunDef("LP_MissileStaff"));
   Check(!(bool)AccessTools.Method(typeof(JobGiver_PickUpOpportunisticWeapon),"ShouldEquipWeapon").Invoke(new JobGiver_PickUpOpportunisticWeapon(),new object[]{loose,deaf}),"AI never picks up a staff that ignores it");
   loose.Destroy();
   Check(Arcana.CanFire(deaf,GunDef("LP_RunicMusket"),out _) && Near(Arcana.FactorFor(deaf,GunDef("LP_RunicMusket")),.5f),"a deaf human still fires dwarf guns at half power");
   // 普通人：没有心灵链接 ×0.7；有心灵链接时第一发加心灵熵，熵满不能施放。
   var mage=GunPawn(spot+IntVec3.East*2);made.Add(mage);var staff2=Arm(mage,"LP_MissileStaff");float sens=Arcana.RawSensitivity(mage);
   Check(sens>0f && !Arcana.Linked(mage) && Near(Arcana.FactorFor(mage,staff2.def),Arcana.Factor("G",sens)*.7f),"without a psylink a staff works at 70% ("+Arcana.FactorFor(mage,staff2.def).ToString("0.##")+")");
   var info=staff2.GetComp<CompArcaneWeapon>().SpecialDisplayStats().FirstOrDefault(e=>e.LabelCap.ToString().Contains("魔力系数"));
   Check(info!=null && info.ValueString.StartsWith("×") && info.ValueString.Contains("心灵敏感度 "+sens.ToStringPercent()),"weapon info shows the arcane factor line: "+info?.ValueString);
   mage.ChangePsylinkLevel(1,false);
   Check(Arcana.Linked(mage) && Near(Arcana.FactorFor(mage,staff2.def),Arcana.Factor("G",Arcana.RawSensitivity(mage))),"with a psylink the staff works at full factor (link="+Arcana.Linked(mage)+", sensitivity "+sens.ToStringPercent()+" -> "+Arcana.RawSensitivity(mage).ToStringPercent()+", factor "+Arcana.FactorFor(mage,staff2.def).ToString("0.##")+")");
   var foe=GunPawn(spot+IntVec3.East*2+IntVec3.North*6);foe.SetFaction(GunEnemyFaction);made.Add(foe);
   mage.psychicEntropy.RemoveAllEntropy();float e0=mage.psychicEntropy.EntropyValue;
   var mverb=(Verb_ArcaneShoot)mage.equipment.PrimaryEq.PrimaryVerb;var tr=Traverse.Create(mverb);
   tr.Field("currentTarget").SetValue(new LocalTargetInfo(foe));tr.Field("burstShotsLeft").SetValue(mverb.BurstShotCount);
   var bolt=GunDef("LP_MissileStaff").Verbs[0].defaultProjectile;int bolts=map.listerThings.ThingsOfDef(bolt).Count;
   Check((bool)AccessTools.Method(typeof(Verb_ArcaneShoot),"TryCastShot").Invoke(mverb,null),"missile staff casts its first bolt");
   Check(Near(mage.psychicEntropy.EntropyValue-e0,2f),"the first bolt of a burst adds 2 neural heat ("+(mage.psychicEntropy.EntropyValue-e0).ToString("0.##")+")");
   var missile=map.listerThings.ThingsOfDef(bolt).Cast<Projectile>().FirstOrDefault(b=>b.Launcher==mage);
   Check(map.listerThings.ThingsOfDef(bolt).Count==bolts+1 && missile!=null && missile.HitFlags==ProjectileHitFlags.IntendedTarget && Traverse.Create(missile).Field("intendedTarget").GetValue<LocalTargetInfo>().Thing==foe,"magic missile flies straight at its intended target, ignoring cover");
   tr.Field("burstShotsLeft").SetValue(mverb.BurstShotCount-1);float e1=mage.psychicEntropy.EntropyValue;
   AccessTools.Method(typeof(Verb_ArcaneShoot),"TryCastShot").Invoke(mverb,null);
   Check(Near(mage.psychicEntropy.EntropyValue,e1),"later bolts of the same burst add no neural heat");
   foreach(var b in map.listerThings.ThingsOfDef(bolt).ToList())b.Destroy();
   mage.psychicEntropy.TryAddEntropy(mage.psychicEntropy.MaxEntropy-mage.psychicEntropy.EntropyValue-1f,null,true,true);
   bool hot=Arcana.CanFire(mage,staff2.def,out var full);
   Check(!hot && full.Contains("心灵熵") && !mverb.Available(),"a staff cannot cast when neural heat is full: "+full);
   float oldCost=LichMod.Settings.staffEntropy;LichMod.Settings.staffEntropy=0f;
   Check(Arcana.CanFire(mage,staff2.def,out _) && !Arcana.PaysEntropy(mage,staff2.def),"setting the per-cast neural heat to 0 removes the cost");
   LichMod.Settings.staffEntropy=oldCost;mage.psychicEntropy.RemoveAllEntropy();
   // F 组不加熵。
   var gunner=GunPawn(spot+IntVec3.West*2);made.Add(gunner);gunner.ChangePsylinkLevel(1,false);var musket=Arm(gunner,"LP_RunicMusket");
   Check(!Arcana.PaysEntropy(gunner,musket.def),"dwarf guns never add neural heat");
   // 驱散粉尘落点 10 秒内法杖不能施放。
   ItemThrowables.Dispel(map,mage.Position);
   bool afterDispel=Arcana.CanFire(mage,staff2.def,out var dispelled);
   Check(!afterDispel && dispelled.Contains("驱散"),"dispel dust stops staves near its landing spot: "+dispelled);
   Check(Arcana.CanFire(gunner,musket.def,out _),"dispel dust does not stop dwarf guns");
   Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+Arcana.DispelLockTicks+1);
   Check(Arcana.CanFire(mage,staff2.def,out _),"staves work again 10 seconds after the dispel");
   Items.Remove(mage,"LP_MagicDispelled");
   // 实际投射物伤害 = 原版伤害 × 魔力系数。
   var p1=(LP_ArcaneBullet)Fire(mage,staff2,foe);
   Check(p1.DamageAmount==Arcana.Scaled(p1.RawDamage,Arcana.FactorFor(mage,staff2.def)) && p1.RawDamage==6,"magic missile damage = 6 x arcane factor ("+p1.DamageAmount+")");
   p1.Destroy();
   var p2=(LP_ArcaneBullet)Fire(gunner,musket,foe);
   Check(p2.DamageAmount==Arcana.Scaled(18,Arcana.FactorFor(gunner,musket.def)),"runic musket damage = 18 x arcane factor ("+p2.DamageAmount+")");
   p2.Destroy();
   var plain=Arm(gunner,"LP_SilverPistol");var p3=(LP_ArcaneBullet)Fire(gunner,plain,foe);
   Check(Near(p3.ArcaneFactor,1f) && p3.DamageAmount==14,"silver pistol ignores sensitivity");
   p3.Destroy();
  }

  void CheckGunDwarves(List<Thing> made){
   var spot=ItemSpot(3);var dwarfType=DefDatabase<XenotypeDef>.GetNamed("LP_Dwarf");
   var dwarf=GunPawn(spot,-2);dwarf.genes.SetXenotype(dwarfType);made.Add(dwarf);
   Check(CommonRaces.IsDwarf(dwarf) && Near(Arcana.RawSensitivity(dwarf),0f),"deaf dwarf fixture");
   Check(Near(Arcana.Sensitivity(dwarf,"F"),1f) && Near(Arcana.FactorFor(dwarf,GunDef("LP_RunicMusket")),1f),"dwarven rune resonance: a deaf dwarf fires dwarf guns at 100%");
   Check(Arcana.Factor("G",Arcana.Sensitivity(dwarf,"G"))==0f,"the dwarven floor does not apply to staves");
   var human=GunPawn(spot+IntVec3.East*2);made.Add(human);
   var wrath=ThingMaker.MakeThing(GunDef("LP_MountainWrath"));made.Add(wrath);
   bool humanOk=EquipmentUtility.CanEquip(wrath,human,out var no);
   Check(!humanOk && no!=null && no.Contains("矮人"),"mountain's wrath refuses a non-dwarf: "+no);
   Check(EquipmentUtility.CanEquip(wrath,dwarf,out _),"mountain's wrath accepts a dwarf");
   // 品质：矮人做 F 组 +1，最多大师；D 组不加。
   Check(Arcana.CraftedQuality(QualityCategory.Good,dwarf,GunDef("LP_RunicMusket"))==QualityCategory.Excellent && Arcana.CraftedQuality(QualityCategory.Masterwork,dwarf,GunDef("LP_RunicMusket"))==QualityCategory.Masterwork,"dwarves craft dwarf guns one level better, up to masterwork");
   Check(Arcana.CraftedQuality(QualityCategory.Good,dwarf,GunDef("LP_SilverPistol"))==QualityCategory.Good && Arcana.CraftedQuality(QualityCategory.Good,human,GunDef("LP_RunicMusket"))==QualityCategory.Good,"no dwarf bonus for other weapons or crafters");
  }

  void CheckGunEffects(List<Thing> made){
   var spot=ItemSpot(4);var enemy=GunEnemyFaction;
   // 噩耗：头部/躯干判定 + 身后直线上的下一个人。
   var shooter=GunPawn(spot+IntVec3.West*4);made.Add(shooter);
   var front=GunPawn(spot);front.SetFaction(enemy);made.Add(front);
   var back=GunPawn(spot+IntVec3.East*3);back.SetFaction(enemy);made.Add(back);
   var aside=GunPawn(spot+IntVec3.North*3);aside.SetFaction(enemy);made.Add(aside);
   var body=front.RaceProps.body;
   Check(Arcana.Vital(body.corePart) && Arcana.Vital(body.AllParts.First(x=>x.def.defName=="Head")) && Arcana.Vital(body.AllParts.First(x=>x.def.defName=="Brain")) && !Arcana.Vital(body.AllParts.First(x=>x.def.defName=="Leg")) && !Arcana.Vital(body.AllParts.First(x=>x.def.defName=="Arm")),"doom pierces only on head or torso hits");
   Check(Arcana.PierceTarget(map,shooter.Position.ToVector3Shifted(),front)==back,"doom's pierce finds the next target straight behind");
   Check(Near(GunDef("LP_Doom").Verbs[0].defaultProjectile.GetModExtension<ArcaneShotExt>().pierceChance,.15f),"doom pierce chance is 15%");
   // 雷鸣铳：半径 1.5 内人形眩晕 1 秒，外面的不受影响。
   Place(back,spot+IntVec3.East*1);Place(aside,spot+IntVec3.North*4);
   int stunned=Arcana.EmpBurst(map,spot,1.5f,shooter,Arcana.EmpStunHumanlike);
   Check(stunned>=2 && front.stances.stunner.Stunned && back.stances.stunner.Stunned && !aside.stances.stunner.Stunned,"thunder gun EMP burst stuns humanlikes within 1.5 cells ("+stunned+")");
   var thunder=GunDef("LP_ThunderGun").Verbs[0].defaultProjectile;
   Check(thunder.projectile.damageDef==DamageDefOf.EMP && Near(thunder.GetModExtension<ArcaneShotExt>().empRadius,1.5f),"thunder gun fires EMP damage with a 1.5-cell burst");
   // 雷锤手炮：不伤己方。
   var ally=GunPawn(spot+IntVec3.South);made.Add(ally);
   var ignored=LP_ArcaneShell.Friendly(map,spot,1.9f,shooter);
   Check(ignored.Contains(ally) && !ignored.Contains(front),"hammer cannon blast spares allies but not enemies");
   var cannon=(ThingWithComps)ThingMaker.MakeThing(GunDef("LP_HammerCannon"));shooter.equipment.DestroyAllEquipment();shooter.equipment.AddEquipment(cannon);
   var shell=(LP_ArcaneShell)Fire(shooter,cannon,front);
   Check(shell.DamageAmount==Arcana.Scaled(shell.RawDamage,Arcana.FactorFor(shooter,cannon.def)),"hammer cannon blast damage scales with the arcane factor ("+shell.DamageAmount+")");
   shell.Destroy();
   // 目标侧：寒铁护甲 / 心智抗性 -25%，符文灼印 +15%（只对 F、G），圣银 ×1.5 对亡灵。
   var fireStaff=GunDef("LP_FireStaff");var silver=GunDef("LP_SilverPistol");var musket=GunDef("LP_RunicMusket");
   Check(Arcana.TargetFactor(front,fireStaff)==1f,"plain target takes full arcane damage");
   var helm=(Apparel)ThingMaker.MakeThing(GunDef("Apparel_SimpleHelmet"),ItemMaterials.ColdIron);front.apparel.Wear(helm);
   Check(Near(Arcana.TargetFactor(front,fireStaff),.75f) && Near(Arcana.TargetFactor(front,musket),.75f) && Arcana.TargetFactor(front,silver)==1f,"cold iron armor cuts F/G damage by 25%");
   front.apparel.Remove(helm);helm.Destroy();
   var elfGene=front.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_ElfBloodline"),true);
   Check(DNDMind.Resistance(front)>0f && Near(Arcana.TargetFactor(front,fireStaff),.75f) && Arcana.TargetFactor(front,silver)==1f,"mind resistance (LP_MindResist) cuts F/G damage by 25%");
   front.genes.RemoveGene(elfGene);
   Items.Give(front,"LP_RuneBrand",180);
   Check(Near(Arcana.TargetFactor(front,musket),1.15f) && Arcana.TargetFactor(front,silver)==1f && Items.TicksLeft(front,"LP_RuneBrand")==180,"rune brand: +15% arcane damage for 3 seconds");
   Items.Remove(front,"LP_RuneBrand");
   aside.genes.AddGene(DefDatabase<GeneDef>.GetNamed("LP_LichGene"),true);
   Check(Items.Undead(aside) && Near(Arcana.TargetFactor(aside,silver),1.5f) && Arcana.TargetFactor(front,silver)==1f,"silver pistol deals +50% to the undead");
   var dinfo=new DamageInfo(DamageDefOf.Bullet,10f,0f,-1f,shooter,null,silver);aside.PreApplyDamage(ref dinfo,out _);
   Check(Near(dinfo.Amount,15f),"silver bane applies in real damage ("+dinfo.Amount+")");
  }

  void CheckGunSources(List<Thing> made){
   // 敌人与盟友携带。
   Check(GunGear.Choose("LP_FC_DwarfGuard",.1f)=="LP_RunicMusket" && GunGear.Choose("LP_FC_DwarfGuard",.2f)=="LP_RunicPistol" && GunGear.Choose("LP_FC_DwarfGuard",.5f)==null,"30% of alliance dwarf guards carry a runic musket or pistol");
   Check(GunGear.Choose("LP_FH_RedWizard",.5f)=="LP_FireStaff" && GunGear.Choose("LP_FH_RedWizard",.9f)=="LP_FrostStaff" && GunGear.Choose("LP_FH_OrcWarrior",.1f)==null,"red wizards carry fire (70%) or frost (30%) staves");
   var thay=Find.FactionManager.FirstFactionOfDef(DefDatabase<FactionDef>.GetNamedSilentFail("LP_FaerunHostile"));
   if(thay!=null){
    PawnGenerationRequest Req()=>new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed("LP_FH_RedWizard"),thay,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,mustBeCapableOfViolence:true);
    var wiz=PawnGenerator.GeneratePawn(Req());made.Add(wiz);
    Check(Arcana.Group(wiz.equipment?.Primary?.def)=="G","a generated red wizard carries a staff ("+wiz.equipment?.Primary?.def.defName+")");
    Check(Arcana.RedRobe(wiz) && Arcana.Floor(wiz,"G")==1f,"hostile red wizards cast as if at least 100% sensitive");
    LichMod.Settings.enemyMagicGuns=false;
    try{var plain=PawnGenerator.GeneratePawn(Req());made.Add(plain);Check(Arcana.Group(plain.equipment?.Primary?.def)==null,"turning off the setting keeps red wizards on their old weapons");}
    finally{LichMod.Settings.enemyMagicGuns=true;}
   }
   // 商人。
   var dwarfSmith=DefDatabase<TraderKindDef>.GetNamed("LP_Caravan_DwarfSmith");var black=DefDatabase<TraderKindDef>.GetNamed("LP_Caravan_BlackNetwork");
   Check(Sells(dwarfSmith,"LP_SilverPistol") && Sells(dwarfSmith,"LP_RunicMusket") && Sells(dwarfSmith,"LP_MountainWrath") && !Sells(dwarfSmith,"LP_FireStaff"),"dwarf smith caravans sell groups D and F");
   if(ModsConfig.OdysseyActive)Check(Sells(dwarfSmith,"Obsidian"),"dwarf smith caravans sell obsidian");
   Check(Sells(black,"LP_ThunderGun") && Sells(black,"LP_FrostStaff") && !Sells(black,"LP_RunicMusket"),"the black network sells group D and staves");
   var rare=black.stockGenerators.OfType<StockGenerator_RareArcane>().FirstOrDefault();
   Check(rare!=null && Near(rare.chance,.02f) && rare.HandlesThingDef(GunDef("LP_Doom")) && rare.HandlesThingDef(GunDef("LP_Skyfire")) && GunDef("LP_Doom").BaseMarketValue>=5000f,"the black network rarely stocks doom or skyfire at about 6000 silver");
   // Isekai 宝箱：2 级以上 D/F/G，4 级以上 E。
   var rng=new System.Random(129);Func<float> r=()=>(float)rng.NextDouble();var got=new int[5];bool eLow=false,eHigh=false;
   for(int tier=1;tier<=4;tier++)for(int i=0;i<2000;i++){var loot=GunLoot.Roll(tier,r);got[tier]+=loot.Count;if(loot.Any(n=>Arcana.GroupE.Contains(n))){if(tier<4)eLow=true;else eHigh=true;}}
   Check(got[1]==0 && got[2]>300 && !eLow && eHigh && GunLoot.Common.All(n=>Items.Thing(n)!=null),"dungeon chests give D/F/G from tier 2 and doom/skyfire only from tier 4 ("+string.Join("/",got.Skip(1))+")");
  }

  void PrepareGunsSave(){
   var p=GunPawn(ItemSpot(2));var staff=Arm(p,"LP_FrostStaff");Items.Give(p,"LP_RuneBrand",180);gunSaveId=p.ThingID;
   Check(staff.GetComp<CompArcaneWeapon>()?.Wielder==p,"save fixture wields a frost staff");
  }
  void CheckGunsAfterLoad(){
   map=Find.CurrentMap;var p=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==gunSaveId);
   Check(p!=null && p.equipment?.Primary?.def.defName=="LP_FrostStaff" && p.equipment.Primary.GetComp<CompArcaneWeapon>()?.Wielder==p,"wielded staff survives save/load");
   Check(Items.TicksLeft(p,"LP_RuneBrand")==180,"rune brand timer survives save/load");
   p.Destroy();
  }

  // -Isekai：同一小人同一法杖，Isekai 精炼前后的伤害 = Isekai 倍率 × 魔力系数。
  void CheckGunsIsekai(){
   if(!ModsConfig.IsActive("jellycreative.isekaileveling"))return;
   var compType=GenTypes.GetTypeInAnyAssembly("IsekaiLeveling.Forge.CompForgeEnhancement");var util=GenTypes.GetTypeInAnyAssembly("IsekaiLeveling.Forge.ForgeUtility");
   Check(compType!=null && util!=null,"Isekai forge types found");
   var spot=ItemSpot(2);var p=GunPawn(spot);var staff=Arm(p,"LP_FireStaff");
   var foe=GunPawn(spot+IntVec3.North*5);foe.SetFaction(GunEnemyFaction);
   var comp=staff.AllComps.FirstOrDefault(c=>compType.IsInstanceOfType(c));
   Check(comp!=null,"Isekai injects its forge comp into the fire staff");
   int Shot(out int raw,out float f){var b=(LP_ArcaneBullet)Fire(p,staff,foe);raw=b.RawDamage;f=b.ArcaneFactor;int d=b.DamageAmount;b.Destroy();return d;}
   var level=Traverse.Create(comp).Field("refinementLevel");level.SetValue(0);
   int d0=Shot(out int raw0,out float f0);
   level.SetValue(10);
   int d1=Shot(out int raw1,out float f1);
   level.SetValue(0);
   float isekai=1f+(float)AccessTools.Method(util,"GetRangedDamageBonus").Invoke(null,new object[]{10});
   Check(Near(f0,f1) && Near(f0,Arcana.FactorFor(p,staff.def)) && f0>0f,"same pawn, same staff: arcane factor x"+f0.ToString("0.##")+" before and after refining");
   Check(Math.Abs(raw1-raw0*isekai)<=1.01f,"Isekai +10 refinement raises the staff's base damage x"+isekai.ToString("0.##")+" ("+raw0+" -> "+raw1+")");
   Check(d0==Arcana.Scaled(raw0,f0) && d1==Arcana.Scaled(raw1,f1),"arcane factor multiplies on top of the Isekai-adjusted damage");
   Check(Math.Abs(d1-raw0*isekai*f0)<=1.51f,"refined staff damage = Isekai multiplier x arcane factor ("+d0+" -> "+d1+", expected about "+(raw0*isekai*f0).ToString("0.#")+")");
   // 1.30.1：战士旋风斩改走原版近战伤害结算（VerbProperties.AdjustedMeleeDamageAmount）。Isekai 的近战精炼与力量都挂在小人的 MeleeDamageFactor 上，
   // 这里确认同一小人、同一把剑在 Isekai 精炼 +10 后，这条结算路径的伤害按 Isekai 倍率上升（VPE 不在 -Isekai 档里，旋风斩本身的 × 战技强度在 -VPE 档检查）。
   var sword=Arm(p,"MeleeWeapon_LongSword");var swordComp=sword.AllComps.FirstOrDefault(c=>compType.IsInstanceOfType(c));
   Check(swordComp!=null,"Isekai injects its forge comp into a longsword");
   var slevel=Traverse.Create(swordComp).Field("refinementLevel");slevel.SetValue(0);
   var mverb=p.meleeVerbs.TryGetMeleeVerb(foe);float m0=mverb.verbProps.AdjustedMeleeDamageAmount(mverb,p);float mdf0=p.GetStatValue(StatDefOf.MeleeDamageFactor);
   slevel.SetValue(10);
   mverb=p.meleeVerbs.TryGetMeleeVerb(foe);float m1=mverb.verbProps.AdjustedMeleeDamageAmount(mverb,p);float mdf1=p.GetStatValue(StatDefOf.MeleeDamageFactor);
   slevel.SetValue(0);
   float meleeBonus=1f+(float)AccessTools.Method(util,"GetMeleeDamageBonus").Invoke(null,new object[]{10});
   Check(Near(mdf1/mdf0,meleeBonus) && Near(m1/m0,meleeBonus),"Isekai +10 melee refinement raises the melee damage path used by the fighter's whirlwind x"+meleeBonus.ToString("0.##")+" ("+m0.ToString("0.#")+" -> "+m1.ToString("0.#")+"; strength already included: melee factor "+mdf0.ToString("0.##")+")");
   p.Destroy();foe.Destroy();
  }
 }
}
