using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using UnityEngine;
namespace LichPhylactery {
 // 1.56.0「文化之神」检查（Test.ps1 -Only Ideology），规格第十一节的 15 条。没装文化时只检查桥没有加载。
 public partial class LichChecks {
  readonly List<Thing> iMade=new List<Thing>();
  readonly List<Ideo> iIdeos=new List<Ideo>();
  Ideo IGen(params string[] memes){
   var forced=memes.Select(m=>DefDatabase<MemeDef>.GetNamed(m)).ToList();
   var ideo=IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(Faction.OfPlayer.def,forcedMemes:forced));
   Find.IdeoManager.Add(ideo);iIdeos.Add(ideo);return ideo;
  }
  // 给殖民者换文化会让原版按人数重算玩家主文化，所以换完再把测试文化设回主文化。
  Pawn IPawn(Ideo ideo){var p=FPawn();p.ideo.SetIdeo(ideo);ICert(p,.5f);iMade.Add(p);if(iLath!=null && Faction.OfPlayer.ideos.PrimaryIdeo!=iLath)Faction.OfPlayer.ideos.SetPrimary(iLath);return p;}
  static readonly AccessTools.FieldRef<Pawn_IdeoTracker,float> certRef=AccessTools.FieldRefAccess<Pawn_IdeoTracker,float>("certaintyInt");
  static void ICert(Pawn p,float v)=>certRef(p.ideo)=v;
  static void ISetMemes(Ideo ideo,params string[] memes){foreach(var m in ideo.memes.Where(x=>x.defName.StartsWith("LP_Meme_")).ToList())ideo.memes.Remove(m);foreach(var m in memes)ideo.memes.Add(DefDatabase<MemeDef>.GetNamed(m));IdeoBridge.Invalidate();}

  void CheckIdeology(){
   if(!ModsConfig.IdeologyActive){Check(!IdeoBridge.Active && DefDatabase<MemeDef>.GetNamedSilentFail("LP_Meme_Pantheon")==null,"without Ideology the bridge is not loaded and IdeoBridge.Active is false");return;}
   var s=LichMod.Settings;bool link=s.ideoLink,cert=s.ideoCertainty,lockP=s.ideoPatronLock;int fav=s.faithFavorPercent;bool sys=s.faithSystem;
   var player=Faction.OfPlayer.ideos.PrimaryIdeo;var before=new HashSet<Thing>(map.listerThings.AllThings);
   bool classic=Find.IdeoManager.classicMode;Find.IdeoManager.classicMode=false; // 自测开局是经典模式，原版在经典模式里不改确信度
   s.ideoLink=true;s.ideoCertainty=true;s.ideoPatronLock=true;s.faithFavorPercent=100;s.faithSystem=true;Faith.TestSuppressed=false;IdeoStartup.TestSuppressed=false;
   try{IdeoDefsCheck();IdeoPatronCheck();IdeoCertaintyPrayer();IdeoConvertEvents();IdeoRitualsCheck();IdeoTempleCheck();IdeoFactionsCheck();}
   finally{
    Find.IdeoManager.classicMode=classic;
    s.ideoLink=link;s.ideoCertainty=cert;s.ideoPatronLock=lockP;s.faithFavorPercent=fav;s.faithSystem=sys;Faith.TestSuppressed=true;IdeoStartup.TestSuppressed=true;IdeoStartup.ApplySettings();Faith.ApplySettings();
    if(player!=null && Faction.OfPlayer.ideos.PrimaryIdeo!=player)Faction.OfPlayer.ideos.SetPrimary(player);
    foreach(var t in iMade)if(t!=null && !t.Destroyed){if(t is Pawn p && p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!t.Destroyed)t.Destroy();}
    iMade.Clear();fMade.Clear();
    foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList())if(!t.Destroyed)t.Destroy();
    iIdeos.Clear(); // 测试文化留在世界里（没人信，不影响其他检查）
    FReset();IdeoBridge.Invalidate();
   }
  }

  // 1 Def 都加载、没有配置错误、贴图能读到。
  void IdeoDefsCheck(){
   var memes=IdeoDefs.DndMemes.ToList();
   var issues=new[]{"LP_Issue_Undead","LP_Issue_Giants","LP_Issue_Arcane","LP_Issue_Races","LP_Issue_Apotheosis","LP_Issue_Offerings","LP_BrainDevouring"}.Select(n=>DefDatabase<IssueDef>.GetNamedSilentFail(n)).ToList();
   var fest=DefDatabase<PreceptDef>.AllDefs.Where(d=>d.defName.StartsWith("LP_Festival_")).ToList();
   var statues=IdeoMapCache.StatueDefs;
   var defs=new List<Def>();defs.AddRange(memes);defs.AddRange(issues.Where(i=>i!=null));defs.AddRange(DefDatabase<PreceptDef>.AllDefs.Where(d=>d.defName.StartsWith("LP_")));defs.AddRange(statues);defs.AddRange(DefDatabase<RitualPatternDef>.AllDefs.Where(d=>d.defName.StartsWith("LP_")));defs.AddRange(DefDatabase<RitualOutcomeEffectDef>.AllDefs.Where(d=>d.defName.StartsWith("LP_")));
   var errors=defs.SelectMany(d=>d.ConfigErrors().Select(e=>d.defName+": "+e)).ToList();
   var tex=memes.Select(m=>m.iconPath).Concat(issues.Where(i=>i!=null).Select(i=>i.iconPath)).Concat(new[]{"UI/Roles/LP/Cleric","UI/Abilities/LP/DivinePreach","UI/Abilities/LP/MassPrayer","UI/Abilities/LP/Oracle"}).Concat(statues.Select(d=>d.graphicData.texPath)).Concat(DefDatabase<IdeoIconDef>.AllDefs.Where(d=>d.defName.StartsWith("LP_Icon_")).Select(d=>d.iconPath)).ToList();
   var missing=tex.Where(t=>ContentFinder<Texture2D>.Get(t,false)==null).ToList();
   Check(IdeoBridge.Active && memes.Count==16 && issues.All(i=>i!=null) && fest.Count==9 && IdeoDefs.GreyFuneral!=null && IdeoDefs.Cleric!=null && statues.Count==10 && IdeoTemple.Temple!=null && IdeoPatches.Applied.Count==13 && errors.Count==0 && missing.Count==0,
    "ideology bridge active: 16 memes, 7 issues, 9 festivals, grey-robe funeral, cleric, 10 statues, LP_Temple, 13 patches; "+defs.Count+" defs pass ConfigErrors, "+tex.Count+" textures load"+(errors.Count==0?"":": "+string.Join("; ",errors.Take(5)))+(missing.Count==0?"":"; missing "+string.Join(",",missing))+"; patches "+IdeoPatches.Applied.Count);
  }

  // 2 生成洛山达文化；3 设为主文化后守护神锁定；4 重塑换提尔；5 众神殿冷却减半。
  Ideo iLath;
  void IdeoPatronCheck(){
   var lath=Faith.God("Lathander");var tyr=Faith.God("Tyr");var mys=Faith.God("Mystra");
   iLath=IGen("Structure_TheistEmbodied","LP_Meme_Lathander");
   var f=iLath.foundation as IdeoFoundation_Deity;var fest=iLath.PreceptsListForReading.OfType<Precept_Ritual>().FirstOrDefault(r=>r.def.defName=="LP_Festival_Lathander");
   var trig=fest?.obligationTriggers.OfType<RitualObligationTrigger_Date>().FirstOrDefault();
   Check(f!=null && f.DeitiesListForReading.Count>0 && f.DeitiesListForReading[0].name=="洛山达" && iLath.HasPrecept(DefDatabase<PreceptDef>.GetNamed("LP_Undead_Abhorrent")) && fest!=null && trig!=null && IdeoPatches.TriggerDay(trig)==0,
    "generated Lathander ideo: first deity 洛山达 ("+(f?.DeitiesListForReading.FirstOrDefault()?.name)+"), undead abhorrent and 绿草节 required, festival fixed on Aprimay 1 (day "+(trig==null?-1:IdeoPatches.TriggerDay(trig))+")");
   FReset();Faction.OfPlayer.ideos.SetPrimary(iLath);IdeoState.Get.Sync();
   string why=FaithUtility.CannotSetPatron(tyr);
   Check(FaithUtility.Patron==lath && why!=null && why.Contains("文化"),"Lathander ideo as primary: 2500-tick sync sets the patron to Lathander, CannotSetPatron(Tyr) = "+why);
   FSet(lath,40f);Faith.Comp.patronCooldownUntil=-1;ISetMemes(iLath,"LP_Meme_Tyr");IdeoState.Get.Sync();
   Check(FaithUtility.Patron==tyr && Near(FaithUtility.GetFavor(lath),25f) && Faith.Comp.patronCooldownUntil<Faith.Now,"reform to Tyr: patron follows, Lathander loses only 15 ("+FaithUtility.GetFavor(lath)+"), no cooldown");
   ISetMemes(iLath,"LP_Meme_Pantheon");FReset();FaithUtility.SetPatron(lath);FaithUtility.SetPatron(tyr);int cd=Faith.Comp.patronCooldownUntil-Faith.Now;
   Check(FaithUtility.Patron==tyr && Math.Abs(cd-Faith.ChangeCooldownDays*GenDate.TicksPerDay/2)<=2 && IdeoBridge.PatronLockReason(mys)==null,"pantheon ideo: patron is free to change, cooldown halved ("+(cd/(float)GenDate.TicksPerDay).ToString("0.##")+" days)");
   ISetMemes(iLath,"LP_Meme_Lathander");FReset();IdeoState.Get.Sync();
  }

  // 6 确信度漂移；7 祈祷乘确信度。
  void IdeoCertaintyPrayer(){
   var lath=Faith.God("Lathander");var a=IPawn(iLath);
   FSet(lath,60f);float c0=a.ideo.Certainty;IdeoBridge.Daily();float up=a.ideo.Certainty-c0;
   FSet(lath,-40f);c0=a.ideo.Certainty;IdeoBridge.Daily();float down=a.ideo.Certainty-c0;
   Check(up>.002f && up<=.0051f && down<-.004f && down>=-.0101f,"daily certainty drift with patron favor 60 = +"+up.ToString("0.####")+" (≈+0.5%), -40 = "+down.ToString("0.####")+" (≈-1%) [patron "+FaithUtility.Patron?.key+" fav "+FaithUtility.GetFavor(lath)+" colonist "+Faith.Colonists.Contains(a)+" ideo "+(a.Ideo==IdeoBridge.PlayerIdeo)+" theist "+IdeoBridge.Player.Theist+" cert "+a.ideo.Certainty+" classic "+Find.IdeoManager.classicMode+"]");
   FReset();FaithUtility.SetPatron(lath);var b=IPawn(iLath);ICert(a,1f);ICert(b,.2f);
   float ra=Faith.PrayerReward(a);Faith.Comp.prayToday=0f;float rb=Faith.PrayerReward(b);Faith.Comp.prayToday=0f;
   Check(Near(IdeoBridge.PrayerFactor(a),1.5f) && Near(IdeoBridge.PrayerFactor(b),.7f) && Near(ra,.75f) && Near(rb,.35f),"prayer reward scales with certainty: 100% → "+ra+", 20% → "+rb);
  }

  // 8 改宗修正；9 历史事件心情。
  void IdeoConvertEvents(){
   var lath=Faith.God("Lathander");var init=IPawn(iLath);var rec=FPawn();iMade.Add(rec);
   rec.ideo.SetIdeo(Find.IdeoManager.IdeosListForReading.First(i=>i!=iLath && !IdeoBridge.Of(i).Theist && !IdeoBridge.Of(i).defiant));FDevotee(rec,lath);
   float f1=IdeoPatches.ConvertFactor(init,rec);float r1=InteractionWorker_ConvertIdeoAttempt.CertaintyReduction(init,rec);
   var role=iLath.RolesListForReading.FirstOrDefault(r=>r.def==IdeoDefs.Cleric) as Precept_RoleSingle;
   if(role==null){role=(Precept_RoleSingle)PreceptMaker.MakePrecept(IdeoDefs.Cleric);iLath.AddPrecept(role,true,null,null);}
   role.Assign(init,false);float f2=IdeoPatches.ConvertFactor(init,rec);float r2=InteractionWorker_ConvertIdeoAttempt.CertaintyReduction(init,rec);role.Unassign(init,false);
   Check(Near(f1,1.5f) && Near(f2,1.8f) && r1>0f && Mathf.Abs(r2/r1-1.2f)<.01f,"conversion: Lathander devotee vs Lathander-ideo initiator ×"+f1+", cleric initiator ×"+f2+" (certainty reduction "+r1.ToString("0.###")+" → "+r2.ToString("0.###")+")");
   var doer=IPawn(iLath);var witness=IPawn(iLath);
   IdeoEvents.Record("LP_HE_LichRevived",doer);
   var self=DefDatabase<ThoughtDef>.GetNamed("LP_Mem_Undead_Abhorrent_Self");var know=DefDatabase<ThoughtDef>.GetNamed("LP_Mem_Undead_Abhorrent_Know");
   Check(doer.needs.mood.thoughts.memories.GetFirstMemoryOfDef(self)!=null && witness.needs.mood.thoughts.memories.GetFirstMemoryOfDef(know)!=null && Near(self.stages[0].baseMoodEffect,-10f) && Near(know.stages[0].baseMoodEffect,-4f),"undead abhorrent: lich revival gives the doer -10 and other believers -4");
  }

  // 10 凯兰沃庆典「难忘」；11 灰袍葬礼「不错」。
  void IdeoRitualsCheck(){
   var kel=Faith.God("Kelemvor");FReset();var altar=FAltar();var p=IPawn(iLath);
   var dead=FPawn();iMade.Add(dead);dead.Kill(null);var corpse=dead.Corpse;
   string text=IdeoRituals.FestivalOutcome(DefDatabase<RitualOutcomeEffectDef>.GetNamed("LP_FestivalOutcome_Kelemvor"),new List<Pawn>{p},altar,p,2,iLath);
   bool allRested=map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Where(c=>c.InnerPawn?.RaceProps.Humanlike==true).All(c=>Faith.Comp.rested.Contains(c.InnerPawn.ThingID));
   Check(Near(FaithUtility.GetFavor(kel),12f) && corpse!=null && Faith.Comp.rested.Contains(dead.ThingID) && allRested && p.needs.mood.thoughts.memories.GetFirstMemoryOfDef(DefDatabase<ThoughtDef>.GetNamed("LP_Mem_KelemvorPeace"))!=null,"Kelemvor festival unforgettable: +12 favor, every humanlike corpse on the map is rested, participants make peace with the dead");
   FReset();var dead2=FPawn();iMade.Add(dead2);dead2.Kill(null);IdeoRituals.FuneralOutcome(dead2,1);
   Check(Faith.Comp.rested.Contains(dead2.ThingID) && Near(FaithUtility.GetFavor(kel),2f),"grey-robe funeral 'good': the dead is rested and Kelemvor +2");
  }

  // 12 神殿：圣坛 + 洛山达神像同一房间 → LP_Temple；日结算 +0.3；神殿里祈祷 ×1.25。
  void IdeoTempleCheck(){
   var lath=Faith.God("Lathander");FReset();FaithUtility.SetPatron(lath);
   var c=OpenCell();var wall=ThingDefOf.Wall;
   foreach(var x in CellRect.CenteredOn(c,4).EdgeCells){var w=ThingMaker.MakeThing(wall,ThingDefOf.WoodLog);w.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(w,x,map);iMade.Add(w);}
   var altar=(Building_FaithAltar)ThingMaker.MakeThing(Faith.AltarDef,ThingDefOf.WoodLog);altar.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(altar,c+IntVec3.North,map);iMade.Add(altar);
   var st=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_Statue_Lathander"),ThingDefOf.BlocksGranite);st.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(st,c+IntVec3.East*2+IntVec3.North*2,map);iMade.Add(st);
   map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
   var room=altar.GetRoom();IdeoMapCache.For(map)?.Refresh();
   float f0=FaithUtility.GetFavor(lath);IdeoBridge.Daily();float gained=FaithUtility.GetFavor(lath)-f0;
   var p=IPawn(iLath);p.Position=c+IntVec3.South;p.Notify_Teleported();ICert(p,1f);
   Check(room!=null && room.Role==IdeoTemple.Temple && IdeoTemple.InTemple(p) && Near(gained,.3f) && Near(IdeoBridge.PrayerFactor(p),1.875f),"altar + Lathander statue in one room is a temple ("+room?.Role?.defName+"), statue gives +"+gained.ToString("0.##")+" a day, prayer in the temple ×1.25 ("+IdeoBridge.PrayerFactor(p)+")");
  }

  // 13 圣物；14 派系文化与重建；15 新生儿文化。
  void IdeoFactionsCheck(){
   var lath=Faith.God("Lathander");var ideo=IGen("Structure_TheistEmbodied","LP_Meme_Lathander");
   var relic=ideo.PreceptsListForReading.OfType<Precept_Relic>().FirstOrDefault();
   if(relic==null){relic=(Precept_Relic)PreceptMaker.MakePrecept(DefDatabase<PreceptDef>.GetNamed("IdeoRelic"));ideo.AddPrecept(relic,true,null,null);IdeoSync.SwapRelic(ideo,lath);}
   Check(relic.ThingDef==lath.relic,"deity ideo with a relic precept: the relic is "+relic.ThingDef?.defName);
   var thayDef=DefDatabase<FactionDef>.GetNamed("LP_FaerunHostile");
   var thay=IdeoGenerator.GenerateIdeo(IdeoFactions.ParmsFor(thayDef));iIdeos.Add(thay);
   bool gen=thay.HasMeme(DefDatabase<MemeDef>.GetNamed("LP_Meme_Bane")) && thay.HasMeme(DefDatabase<MemeDef>.GetNamed("LP_Meme_Weave")) && thay.HasPrecept(DefDatabase<PreceptDef>.GetNamed("LP_Arcane_Supreme")) && thay.HasPrecept(DefDatabase<PreceptDef>.GetNamed("LP_Races_Own"));
   var f=Find.FactionManager.FirstFactionOfDef(thayDef);bool rebuilt=true;string note="no Thay faction in this world";
   if(f!=null){
    var plain=IGen("Structure_Ideological");iIdeos.Remove(plain);f.ideos.SetPrimary(plain);
    var done=IdeoFactions.Rebuild(false);var now=f.ideos.PrimaryIdeo;
    rebuilt=done.Contains(f) && now!=plain && now.HasMeme(DefDatabase<MemeDef>.GetNamed("LP_Meme_Bane")) && Find.IdeoManager.IdeosListForReading.Contains(plain);note="rebuilt "+done.Count+" factions";
   }
   Check(gen && rebuilt,"Thay ideo has Bane + Weave memes, arcane supreme and own-race precepts ("+string.Join(",",thay.memes.Select(m=>m.defName))+"); rebuild button: "+note);
   var mother=IPawn(iLath);var baby=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,developmentalStages:DevelopmentalStage.Newborn,allowDowned:true));iMade.Add(baby);
   IdeoPatches.NewbornIdeo(baby,mother,null);var top=baby.ideo?.BabyIdeoExposureSorted?.FirstOrDefault();
   Check(baby.Ideo==null && top?.ideo==iLath && top.exposure>=IdeoPatches.NewbornExposure*.99f,"newborn (vanilla gives babies no ideoligion) is seeded with a month of exposure to the mother's ("+top?.ideo?.name+", "+top?.exposure+")");
  }
 }
}
