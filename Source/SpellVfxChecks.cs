using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.121.0：Test.ps1 -Only SpellVfx：每个本模组能力都有特效档案（元素 / 形式），补丁挂上，真实施放会播放、延迟爆发会执行；
 // 自测通过后截图（存档目录 SpellVfx-*.png）：元素爆发总览、施放中光球、预热法阵。
 public partial class LichChecks {
  void CheckSpellVfx(){
   var defs=DefDatabase<AbilityDef>.AllDefsListForReading.Where(SpellVfx.Ours).ToList();
   Check(defs.Count>500,"found "+defs.Count+" DND abilities");
   Check(defs.All(d=>SpellVfx.Profile(d)!=null),"every DND ability has a VFX profile");
   Check(SpellVfx.Profile(DefDatabase<AbilityDef>.AllDefsListForReading.FirstOrDefault(d=>!SpellVfx.Ours(d)))==null,"other mods' / vanilla abilities are left alone");
   foreach(var g in defs.GroupBy(d=>SpellVfx.Profile(d).theme).OrderByDescending(g=>g.Count()))
    Log.Message("[LICH-VFX] "+g.Key+" "+g.Count()+": "+string.Join(" ",g.Take(40).Select(d=>d.label)));
   foreach(var g in defs.GroupBy(d=>SpellVfx.Profile(d).kind))Log.Message("[LICH-VFX] kind "+g.Key+" "+g.Count());
   Check(defs.Select(d=>SpellVfx.Profile(d).theme).Distinct().Count()>=12,"abilities spread over many element themes");
   VfxTheme T(string n)=>SpellVfx.Profile(DefDatabase<AbilityDef>.GetNamed(n)).theme;
   Check(T("LP_SecondWind")==VfxTheme.Heal,"回气 → heal theme ("+T("LP_SecondWind")+")");
   Check(T("LP_BattleCry")==VfxTheme.Martial,"战吼 → martial theme ("+T("LP_BattleCry")+")");
   var fire=defs.FirstOrDefault(d=>d.label!=null && d.label.Contains("火球"));
   if(fire!=null)Check(SpellVfx.Profile(fire).theme==VfxTheme.Fire,fire.label+" → fire theme");
   Check(SpellVfx.Profile(DefDatabase<AbilityDef>.GetNamed("LP_Charge")).kind!=VfxKind.Self,"冲锋 is a targeted effect");
   Check(defs.Where(d=>d.comps!=null && d.comps.OfType<CompProperties_ClassEffect>().Any(c=>c.passive)).All(d=>SpellVfx.Profile(d).off),"passive class abilities never play effects");

   var patches=Harmony.GetPatchInfo(AccessTools.Method(typeof(Ability),nameof(Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)}));
   Check(patches!=null && patches.Postfixes.Any(p=>p.PatchMethod.DeclaringType==typeof(SpellVfx_Activate)),"Ability.Activate VFX postfix is applied");
   Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Stance_Warmup),nameof(Stance_Warmup.StanceTick)))?.Postfixes.Any(p=>p.PatchMethod.DeclaringType==typeof(SpellVfx_WarmupTick))==true,"warmup tick VFX postfix is applied");

   // 瞄准范围指示
   foreach(var g in defs.GroupBy(d=>SpellAim.Of(d)?.shape??AimShape.None))Log.Message("[LICH-VFX] aim "+g.Key+" "+g.Count()+": "+string.Join(" ",g.Take(50).Select(d=>d.label)));
   AimPreview Ai(string n)=>SpellAim.Of(DefDatabase<AbilityDef>.GetNamed(n));
   Check(Ai("LP_Fireball").shape==AimShape.Circle && Near(Ai("LP_Fireball").radius,4f),"火球术 previews a radius-4 circle");
   Check(Ai("LP_LightningBolt").shape==AimShape.Line,"闪电束 previews a line ("+Ai("LP_LightningBolt").shape+")");
   Check(Ai("LP_Disintegrate").shape==AimShape.Line,"解离术 previews its ray ("+Ai("LP_Disintegrate").shape+")");
   Check(Ai("LP_BurningHands").shape==AimShape.Cone && Near(Ai("LP_BurningHands").length,6f) && Near(Ai("LP_BurningHands").halfAngle,45f),"燃烧之手 previews a 6-cell 90° cone");
   Check(Ai("LP_SecondWind").shape==AimShape.None,"self abilities draw no aim preview");
   Check(Harmony.GetPatchInfo(AccessTools.Method(typeof(Verb_CastAbility),nameof(Verb_CastAbility.DrawHighlight)))?.Postfixes.Any(p=>p.PatchMethod.DeclaringType==typeof(SpellAim_DrawHighlight))==true,"aim preview postfix is applied");
   var caster=HtPawn(spawn:true,dx:0);var target=HtPawn(spawn:true,dx:6);
   try{
    var fb=DefDatabase<AbilityDef>.GetNamed("LP_Fireball");int n=SpellAim.Cells(caster,fb,SpellAim.Of(fb),target).Count;
    Check(n>=40,"fireball preview covers the blast cells ("+n+")");
    var lb=DefDatabase<AbilityDef>.GetNamed("LP_LightningBolt");var line=SpellAim.Cells(caster,lb,SpellAim.Of(lb),target);
    Check(line.Count>=5 && line.Contains(target.Position) && !line.Contains(caster.Position),"lightning bolt preview runs from caster to target ("+line.Count+" cells)");
    var bh=DefDatabase<AbilityDef>.GetNamed("LP_BurningHands");var cone=SpellAim.Cells(caster,bh,SpellAim.Of(bh),target);
    Check(cone.Count>10 && cone.All(c=>c.x>=caster.Position.x),"burning hands cone opens toward the target ("+cone.Count+" cells)");    // 每个元素 × 每种形式直接播放一遍，不报错
    foreach(VfxTheme t in Enum.GetValues(typeof(VfxTheme)))foreach(VfxKind k in Enum.GetValues(typeof(VfxKind))){
     var prof=new VfxProfile{theme=t,kind=k};
     SpellVfx.Warmup(caster,prof);SpellVfx.WarmupTick(caster,prof);
     SpellVfx.Cast(caster,caster.DrawPos,map,prof,target,k==VfxKind.Target?2f:0f);
     SpellVfx.Cast(caster,caster.DrawPos,map,prof,caster,1.5f);
    }
    Check(SpellVfxQueue.Pending>0,"delayed impacts queued ("+SpellVfxQueue.Pending+")");
    Step(70);
    Check(SpellVfxQueue.Pending==0,"delayed impacts all played after the orbs land");

    // 真实施放：挑一个普通 Ability 类、自身目标的本模组能力
    var plain=defs.Where(d=>d.abilityClass==typeof(Ability) && !SpellVfx.Profile(d).off && SpellVfx.Profile(d).kind==VfxKind.Self).OrderBy(d=>d.defName).FirstOrDefault();
    if(plain!=null){
     caster.abilities.GainAbility(plain);var ab=caster.abilities.GetAbility(plain);
     int before=SpellVfx.Played;bool ok=ab.Activate(caster,caster);
     Check(!ok || SpellVfx.Played==before+1 && SpellVfx.LastDef==plain,"real cast of "+plain.defName+" plays the effect (activated "+ok+")");
     LichMod.Settings.spellVfx=false;before=SpellVfx.Played;ab.Activate(caster,caster);
     Check(SpellVfx.Played==before,"settings toggle off → no effect");
     LichMod.Settings.spellVfx=true;
    }
   }finally{
    LichMod.Settings.spellVfx=true;
    foreach(var x in htMade){if(x.Dead && x.Corpse!=null && !x.Corpse.Destroyed)x.Corpse.Destroy();else if(!x.Destroyed)x.Destroy();}htMade.Clear();
   }
  }

  // ===== 截图 =====
  static int vfxFrames;static IntVec3 vfxCenter;static readonly List<Pawn> vfxPawns=new List<Pawn>();
  static void VfxScene(int shot){
   var map=Find.CurrentMap;
   if(vfxPawns.Count==0){
    var spot=map.Center;
    vfxCenter=map.AllCells.OrderBy(c=>c.DistanceTo(spot)).First(c=>CellRect.CenteredOn(c,9).Cells.All(x=>x.InBounds(map) && x.Standable(map) && !x.Fogged(map) && x.GetEdifice(map)==null && !x.Roofed(map)));
    for(int i=0;i<4;i++){
     var a=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forcedXenotype:XenotypeDefOf.Baseliner,canGeneratePawnRelations:false));
     var b=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forcedXenotype:XenotypeDefOf.Baseliner,canGeneratePawnRelations:false));
     GenSpawn.Spawn(a,vfxCenter+new IntVec3(-5,0,6-i*4),map);GenSpawn.Spawn(b,vfxCenter+new IntVec3(4,0,6-i*4),map);vfxPawns.Add(a);vfxPawns.Add(b);
    }
    foreach(var p in vfxPawns){p.jobs.StopAll();if(p.drafter!=null)p.drafter.Drafted=true;}
   }
   Find.TickManager.Pause();Find.Selector.ClearSelection();Messages.Clear();
   if(shot==0){ // 16 种元素爆发（4×4，左上 Arcane 按枚举顺序）
    Find.CameraDriver.SetRootPosAndSize(vfxCenter.ToVector3Shifted(),11);
    var all=(VfxTheme[])Enum.GetValues(typeof(VfxTheme));
    for(int i=0;i<all.Length;i++){var pos=vfxCenter.ToVector3Shifted()+new Vector3(-6f+(i%4)*4f,0,6f-(i/4)*4f);SpellVfx.Impact(map,pos,new VfxProfile{theme=all[i]},i%2==0?0f:1.5f,all[i]==VfxTheme.Martial);}
   }else if(shot==1){ // 施放中：左边四人朝右边四人施法（奥术、火、雷、神圣）
    Find.CameraDriver.SetRootPosAndSize(vfxCenter.ToVector3Shifted(),11);
    var th=new[]{VfxTheme.Arcane,VfxTheme.Fire,VfxTheme.Lightning,VfxTheme.Necrotic};
    for(int i=0;i<4;i++)SpellVfx.Cast(vfxPawns[i*2],vfxPawns[i*2].DrawPos,map,new VfxProfile{theme=th[i],kind=VfxKind.Target},vfxPawns[i*2+1],0f);
   }else if(shot==3){ // 预热法阵 + 自身增益
    Find.CameraDriver.SetRootPosAndSize(vfxCenter.ToVector3Shifted(),11);
    var th=new[]{VfxTheme.Holy,VfxTheme.Frost,VfxTheme.Psychic,VfxTheme.Eldritch};
    for(int i=0;i<4;i++){var pr=new VfxProfile{theme=th[i],kind=VfxKind.Self};SpellVfx.Warmup(vfxPawns[i*2],pr);for(int j=0;j<3;j++)SpellVfx.WarmupTick(vfxPawns[i*2],pr);SpellVfx.Cast(vfxPawns[i*2+1],vfxPawns[i*2+1].DrawPos,map,pr,vfxPawns[i*2+1],2f);}
   }
  }
  static bool CaptureSpellVfx(){
   if(vfxFrames==0)return false;vfxFrames++;
   string dir=GenFilePaths.SaveDataFolderPath;
   if(vfxFrames==20)VfxScene(0);
   if(vfxFrames==22)Step(4);
   if(vfxFrames==60)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SpellVfx-impacts.png"));
   if(vfxFrames==70)Step(90);
   if(vfxFrames==80)VfxScene(1);
   if(vfxFrames==82)Step(12);
   if(vfxFrames==120)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SpellVfx-orbs.png"));
   if(vfxFrames==130)Step(24);
   if(vfxFrames==170)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SpellVfx-landing.png"));
   if(vfxFrames==180)Step(90);
   if(vfxFrames==190)VfxScene(3);
   if(vfxFrames==192)Step(6);
   if(vfxFrames==230)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SpellVfx-warmup.png"));
   if(vfxFrames==240){Step(120);Find.CameraDriver.SetRootPosAndSize(vfxCenter.ToVector3Shifted(),11);}
   if(vfxFrames>=245 && vfxFrames<=300 && vfxPawns.Count>=8){ // 瞄准范围：火球（圆）、燃烧之手（锥）、闪电束（线）、解离术（射线）
    var names=new[]{"LP_Fireball","LP_BurningHands","LP_LightningBolt","LP_Disintegrate"};
    for(int i=0;i<4;i++)SpellAim.Draw(vfxPawns[i*2],DefDatabase<AbilityDef>.GetNamed(names[i]),i==0?new LocalTargetInfo(vfxPawns[i*2+1].Position+IntVec3.West*3):new LocalTargetInfo(vfxPawns[i*2+1]));
   }
   if(vfxFrames==290)ScreenCapture.CaptureScreenshot(Path.Combine(dir,"SpellVfx-aim.png"));
   if(vfxFrames>=320)Application.Quit();
   return true;
  }
 }
}
