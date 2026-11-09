using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.140.0 亡灵军团：Test.ps1 -Only UndeadHorde（定义、生成即挂之躯、四种规则、设置开关、读档）。
 public partial class LichChecks {
  static string uhId;
  Pawn UhMake(string kind,int dx,int dz,Faction f=null){
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(kind),f??AdvEnemy,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:30f,fixedChronologicalAge:30f));
   GenSpawn.Spawn(p,MlCell(dx,dz),map);mlMade.Add(p);p.mindState.enemyTarget=null;return p;
  }
  void CheckUndeadHorde(){
   bool sup=Adventurer.TestSuppressed;Adventurer.TestSuppressed=true;var s=LichMod.Settings;bool on=s.undeadHorde;s.undeadHorde=true;int alive0=UndeadHorde.Alive;
   Rand.PushState(114002);
   try{CheckUndeadHordeInner();}
   finally{Rand.PopState();Adventurer.TestSuppressed=sup;s.undeadHorde=on;UndeadHorde.TestRoll=null;Legendary.TestD20=null;MlClean();}
  }
  void CheckUndeadHordeInner(){
   // 1. 定义：四种兵种挂 LP_UndeadExt、各自的外表异种型（有图标、不进开局）、之躯 hediff；塞伊多一组「亡灵军团」编组；设置开关。
   {var kinds=new[]{"LP_UH_Ghoul","LP_UH_Wight","LP_UH_Wraith","LP_UH_Vampire"}.Select(n=>DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).ToList();
    bool ok=kinds.All(k=>k!=null && UndeadHorde.Ext(k)!=null && UndeadHorde.BodyFor(UndeadHorde.Ext(k).kind)!=null && Items.Hediff(UndeadHorde.BodyFor(UndeadHorde.Ext(k).kind))!=null && k.defaultFactionDef==Factions.HostileDef && Chinese(k.label));
    var xenos=new[]{"LP_UH_GhoulXeno","LP_UH_WightXeno","LP_UH_WraithXeno","LP_UH_VampireXeno"}.Select(n=>DefDatabase<XenotypeDef>.GetNamedSilentFail(n)).ToList();
    bool xo=xenos.All(x=>x!=null && x.Icon!=null && x.Icon!=BaseContent.BadTex && !x.inheritable && EnemyRaces.IsEnemyOnly(x));
    var group=Factions.HostileDef?.pawnGroupMakers?.FirstOrDefault(g=>g.kindDef==PawnGroupKindDefOf.Combat && g.options.Any(o=>o.kind?.defName=="LP_UH_Ghoul"));
    Check(ok && xo && group!=null && group.options.Any(o=>o.kind?.defName=="LP_FH_RedWizard") && group.options.Any(o=>o.kind?.defName=="LP_UH_Vampire") && UndeadHorde.On,"[1.140.0] undead horde: 4 kinds with ext + body + enemy-only xenotypes (icons), Thay has an undead combat group led by red wizards, setting on");}
   // 2. 生成即挂之躯：四种各一个，活着的亡灵计数增加，不吃饭；死亡骑士兵种也能直接生成（带死亡骑士之躯）。
   {int a0=UndeadHorde.Alive;var g=UhMake("LP_UH_Ghoul",-10,-10);var w=UhMake("LP_UH_Wight",-8,-10);var r=UhMake("LP_UH_Wraith",-6,-10);var v=UhMake("LP_UH_Vampire",-4,-10);
    bool bodies=Items.Has(g,"LP_UH_GhoulBody") && Items.Has(w,"LP_UH_WightBody") && Items.Has(r,"LP_UH_WraithBody") && Items.Has(v,"LP_UH_VampireBody");
    bool kinds=UndeadHorde.KindOf(g)==UndeadHorde.Ghoul && UndeadHorde.KindOf(v)==UndeadHorde.Vampire && UndeadHorde.Is(r) && Subclass6.IsUndead(w);
    var dk=UhMake("LP_LM_DeathKnight",-2,-10);bool knight=Items.Has(dk,"LP_DeathKnightBody") && dk.apparel.WornApparel.Any(x=>x.def.defName=="LP_DeathknightPlate");
    Check(bodies && kinds && UndeadHorde.Alive>=a0+4 && g.needs?.food==null && knight,"[1.140.0] generated undead carry their body hediff (alive "+a0+" → "+UndeadHorde.Alive+"), no food need; a generated death knight gets its body and plate");}
   // 3. 噬尸鬼：命中后豁免失败就麻痹；亡灵不受影响。
   {var g=UhMake("LP_UH_Ghoul",-10,-6);var man=MlPawn(Faction.OfPlayer,-9,-6);var other=UhMake("LP_UH_Wight",-11,-6);
    Legendary.TestD20=1;UndeadHorde.Hit(g,man,5f);bool para=Items.Has(man,"LP_UH_Paralysis");UndeadHorde.Hit(g,other,5f);bool none=!Items.Has(other,"LP_UH_Paralysis");Legendary.TestD20=20;Items.Remove(man,"LP_UH_Paralysis");UndeadHorde.Hit(g,man,5f);bool saved=!Items.Has(man,"LP_UH_Paralysis");
    Check(para && none && saved,"[1.140.0] ghoul claws paralyze on a failed Con save, not undead, not on a success");}
   // 4. 尸妖吸走 30%、吸血鬼 50%；怨灵刀枪减半（圣火照常）、抽取身边活人生命。
   {var w=UhMake("LP_UH_Wight",-6,-6);var v=UhMake("LP_UH_Vampire",-4,-6);var r=UhMake("LP_UH_Wraith",-2,-6);var man=MlPawn(Faction.OfPlayer,-1,-6);
    S6Hurt(w,20f);S6Hurt(v,20f);float w0=ClassMelee.InjurySum(w),v0=ClassMelee.InjurySum(v);UndeadHorde.Hit(w,man,10f);UndeadHorde.Hit(v,man,10f);float w1=ClassMelee.InjurySum(w),v1=ClassMelee.InjurySum(v);
    var body=r.health.hediffSet.hediffs.OfType<Hediff_UndeadBody>().First();float cut=UndeadHorde.Incoming(r,new DamageInfo(DamageDefOf.Cut,10f,0f,-1f,man,null,DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife")),body),fire=UndeadHorde.Incoming(r,new DamageInfo(DamageDefOf.Flame,10f),body);
    S6Hurt(r,20f);float r0=ClassMelee.InjurySum(r),m0=ClassMelee.InjurySum(man);int n=body.Drain();float r1=ClassMelee.InjurySum(r),m1=ClassMelee.InjurySum(man);
    Check(Near(w0-w1,3f) && Near(v0-v1,5f) && Near(cut,.5f) && Near(fire,1f) && n>=1 && m1>m0 && r1<r0,"[1.140.0] wight drains 3 of 10 ("+(w0-w1).ToString("0.0")+"), vampire 5 ("+(v0-v1).ToString("0.0")+"); wraith takes half from blades ("+cut+") fire full ("+fire+") and drains the adjacent living (n "+n+", "+m0.ToString("0")+" → "+m1.ToString("0")+", wraith "+r0.ToString("0")+" → "+r1.ToString("0")+")");}
   // 5. 吸血鬼：不在日光下每 250 刻愈合 4；挨过圣火 10 秒内不愈合；日光下受伤 ×1.5。
   {var v=UhMake("LP_UH_Vampire",2,-6);var body=v.health.hediffSet.hediffs.OfType<Hediff_UndeadBody>().First();bool sun=UndeadHorde.InSun(v);
    S6Hurt(v,20f);float i0=ClassMelee.InjurySum(v);body.TickInterval(250);float i1=ClassMelee.InjurySum(v);
    body.lastHoly=Find.TickManager.TicksGame;body.TickInterval(250);float i2=ClassMelee.InjurySum(v);
    float f=UndeadHorde.Incoming(v,new DamageInfo(DamageDefOf.Cut,10f),body);
    Check((sun?Near(i1,i0):Near(i0-i1,4f)) && Near(i2,i1) && Near(f,sun?1.5f:1f),"[1.140.0] vampire regenerates 4 per 250 ticks unless in sunlight ("+(sun?"in sun":"sheltered")+") or recently burned by holy fire; sunlight ×1.5");}
   // 6. 设置关掉：On 跟着设置；开关不影响已有的亡灵。
   {LichMod.Settings.undeadHorde=false;bool off=!UndeadHorde.On;LichMod.Settings.undeadHorde=true;
    Check(off && UndeadHorde.On,"[1.140.0] 亡灵军团 setting toggles UndeadHorde.On");}
  }
  void PrepareUndeadHordeSave(){var g=UhMake("LP_UH_Ghoul",14,-14);uhId=g.ThingID;mlMade.Remove(g);}
  void CheckUndeadHordeAfterLoad(){
   var g=map.mapPawns.AllPawnsSpawned.FirstOrDefault(x=>x.ThingID==uhId);
   Check(g!=null && Items.Has(g,"LP_UH_GhoulBody") && UndeadHorde.KindOf(g)==UndeadHorde.Ghoul && UndeadHorde.Alive>=1,"[1.140.0] after load: ghoul keeps its body hediff and counts as alive ("+UndeadHorde.Alive+")");
   if(g!=null)g.Destroy();
  }
 }
}
