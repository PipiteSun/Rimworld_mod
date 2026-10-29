using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using RimWorld.Planet;
using UnityEngine;
namespace LichPhylactery {
 public partial class LichChecks {
  static string decapLeaderId,decapFollowerId;
  readonly List<Pawn> factionMade=new List<Pawn>();
  HashSet<Pawn> worldBefore;

  void CheckFactions(){
   var before=new HashSet<Thing>(map.listerThings.AllThings);
   var goodwill=Find.FactionManager.AllFactions.Where(f=>!f.IsPlayer && f.HasGoodwill).ToDictionary(f=>f,f=>f.PlayerGoodwill);
   worldBefore=new HashSet<Pawn>(Find.WorldPawns.AllPawnsAliveOrDead);
   var letters=new HashSet<Letter>(Find.LetterStack.LettersListForReading);
   var ev=Tribute.Events;int debts=ev.dwarfDebts,tribute=ev.tributeUntilTick,repelled=ev.zealotsRepelled,pactUntil=ev.oldPactUntilTick;bool pledged=ev.pledged;var pactFaction=ev.oldPactFaction;
   var settings=(LichMod.Settings.zealotRaids,LichMod.Settings.decapTrollBerserk,LichMod.Settings.harperWarning);
   try {
    CheckFactionDefs();Rand.PushState(16304);try{CheckFactionGroups();}finally{Rand.PopState();} /* 1.63.0：派系战斗组（塞伊、冬狼变体、种族比例）整组用固定种子生成 */CheckRacialGoodwill();CheckGeneBasedRaces();CheckFactionScenarios();CheckDecapitation();CheckBurrow();CheckZealots();CheckHarper();CheckTribute();CheckGoodwillDrift();CheckBloodDebt();CheckEnemyTrollRevival();CheckBlackNetwork();
   } finally {
    (LichMod.Settings.zealotRaids,LichMod.Settings.decapTrollBerserk,LichMod.Settings.harperWarning)=settings;
    ev.dwarfDebts=debts;ev.tributeUntilTick=tribute;ev.zealotsRepelled=repelled;ev.pledged=pledged;ev.oldPactFaction=pactFaction;ev.oldPactUntilTick=pactUntil;ev.harperQueue.Clear();ev.zealotRaid.Clear();Pact.Recalculate();
    foreach(var l in Find.LetterStack.LettersListForReading.Where(l=>!letters.Contains(l)).ToList())Find.LetterStack.RemoveLetter(l);
    foreach(var p in map.mapPawns.AllPawnsSpawned.Where(p=>!before.Contains(p)).ToList()){Necro.Registry?.constructs.Remove(p);p.Destroy();}
    foreach(var t in map.listerThings.AllThings.Where(t=>!before.Contains(t) && !t.Destroyed && !(t is Filth) && !(t is Mote) && t.def.category!=ThingCategory.Ethereal).ToList())if(!t.Destroyed)t.Destroy();
    CleanFactionPawns();CleanFactionPawns();ColonyCensus.Dirty();
    foreach(var kv in goodwill)if(kv.Key.PlayerGoodwill!=kv.Value)Factions.SetRelation(kv.Key,Faction.OfPlayer,kv.Value);
   }
   Check(map.listerThings.AllThings.All(t=>before.Contains(t) || t is Filth || t is Mote || t.def.category==ThingCategory.Ethereal),"faction checks leave no extra things on the map");
   Check(Find.WorldPawns.AllPawnsAliveOrDead.All(p=>worldBefore.Contains(p) || Find.FactionManager.AllFactionsListForReading.Any(f=>f.leader==p)),"faction checks leave no extra world pawns: "+string.Join(",",Find.WorldPawns.AllPawnsAliveOrDead.Where(p=>!worldBefore.Contains(p) && !Find.FactionManager.AllFactionsListForReading.Any(f=>f.leader==p)).Select(p=>p.kindDef?.defName+"/"+p.Faction?.def.defName+"/"+(p.Dead?"dead":"alive")+"/"+(p.Destroyed?"destroyed":"live")+"/"+Find.WorldPawns.GetSituation(p)+"/"+p.LabelShort)));
   foreach(var def in new[]{Factions.ColonyDef,Factions.HostileDef,Factions.EmberDef,Factions.IllithidDef}){var f=Factions.Of(def);Check(f.leader!=null && !f.leader.Destroyed && !f.leader.Dead,"faction keeps a living leader after the checks: "+def.defName+" "+f.leader?.kindDef?.defName);}
  }
  // 测试生成的人全部销毁；生成时顺带生成的亲属（世界角色）也清掉。
  void CleanFactionPawns(){
   foreach(var p in factionMade){if(p==null || p.Destroyed)continue;
    // 原版生成时会复用世界角色，派系首领可能被拉进测试组：交还世界，不销毁。
    if(Find.FactionManager.AllFactionsListForReading.Any(f=>f.leader==p)){if(p.Spawned)p.DeSpawn();if(!p.Dead && !Find.WorldPawns.Contains(p))Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);continue;}
    Necro.Registry?.constructs.Remove(p);if(p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();if(!p.Destroyed)p.Destroy();}
   factionMade.Clear();
   foreach(var p in Find.WorldPawns.AllPawnsAliveOrDead.Where(p=>!worldBefore.Contains(p) && !Find.FactionManager.AllFactionsListForReading.Any(f=>f.leader==p)).ToList()){Find.WorldPawns.RemovePawn(p);if(!p.Destroyed)p.Destroy();}
   Decapitation.Tracker?.Prune();
  }
  List<Pawn> FactionGroup(Faction f,PawnGroupKindDef kind,float points,TraderKindDef trader=null){
   var parms=new PawnGroupMakerParms{groupKind=kind,faction=f,points=points,tile=map.Tile,traderKind=trader};
   var list=PawnGroupMakerUtility.GeneratePawns(parms,false).ToList();
   factionMade.AddRange(list);return list;
  }
  // 1.24.0：组生成失败时的诊断（派系状态、各组的 CanGenerateFrom、选项花费、再试一次的人数）。
  string GroupDiag(Faction f,float points){var parms=new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,faction=f,points=points,tile=map.Tile};var makers=f.def.pawnGroupMakers.Where(m=>m.kindDef==PawnGroupKindDefOf.Combat).ToList();string s="defeated="+f.defeated+" makers="+makers.Count+" can="+string.Join("/",makers.Select(m=>m.CanGenerateFrom(parms)))+" options="+string.Join(",",makers.SelectMany(m=>m.options).Select(o=>o.kind.defName+":"+o.Cost));try{var again=PawnGroupMakerUtility.GeneratePawns(parms,false).ToList();factionMade.AddRange(again);s+=" retry="+again.Count;}catch(Exception e){s+=" retryException="+e.Message;}return s;}
  Pawn FactionPawn(string kind,Faction f){
   var p=Unscathed(PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(kind),f,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,allowDead:false,allowDowned:false,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:30,fixedChronologicalAge:30)));
   factionMade.Add(p);return p;
  }
  static string OptText(DiaOption o)=>Traverse.Create(o).Field("text").GetValue<string>()??"";
  static string Xeno(Pawn p)=>p.genes?.Xenotype?.defName??"none";

  void CheckFactionDefs(){
   foreach(var name in new[]{"LP_FaerunColony","LP_FaerunHostile","LP_EmberClan","LP_IllithidColony","LP_BlackNetwork","LP_Zealots"}){
    var def=DefDatabase<FactionDef>.GetNamedSilentFail(name);
    Check(def!=null && !def.ConfigErrors().Any() && def.modExtensions.All(e=>!e.ConfigErrors().Any()),"faction def validates: "+name+" "+(def==null?"missing":string.Join(";",def.ConfigErrors())));
    var tex=ContentFinder<Texture2D>.Get(def.factionIconPath,false);
    Check(tex!=null && tex.width>=64,"faction icon loads: "+def.factionIconPath);
    Check(Factions.Of(def)!=null,"faction exists in the generated world: "+name);
    if(!def.hidden)Check(Factions.Of(def).leader!=null,"faction has a leader from world generation: "+name+" "+Factions.Of(def).leader?.kindDef?.defName);
    foreach(var group in def.pawnGroupMakers)foreach(var o in (group.options??new List<PawnGenOption>()).Concat(group.traders??new List<PawnGenOption>()).Concat(group.guards??new List<PawnGenOption>()))Check(o.kind!=null && !o.kind.ConfigErrors().Any(),"faction pawn kind validates: "+name+" "+o.kind?.defName);
   }
   foreach(var def in new[]{Factions.ColonyDef,Factions.HostileDef,Factions.EmberDef,Factions.IllithidDef})Check(def.startingCountAtWorldCreation==1 && def.maxConfigurableAtWorldCreation==1 && !def.hidden,"visible faction starts once and is capped at one: "+def.defName);
   Check(Factions.BlackNetDef.hidden && Factions.ZealotsDef.hidden && Factions.ZealotsDef.permanentEnemy && Factions.Of(Factions.BlackNetDef).Hidden,"black network and zealots are hidden factions");
   Check(Factions.IllithidDef.permanentEnemy && !Factions.HostileDef.permanentEnemy && Factions.HostileDef.techLevel==TechLevel.Medieval && Factions.IllithidDef.techLevel==TechLevel.Ultra,"illithids are permanent ultratech enemies; the red robes are medieval and not permanent");
   var colors=new[]{Factions.ColonyDef,Factions.HostileDef,Factions.EmberDef,Factions.IllithidDef,Factions.BlackNetDef}.Select(d=>d.colorSpectrum[0]).ToList();
   bool distinct=true;for(int i=0;i<colors.Count;i++)for(int j=i+1;j<colors.Count;j++)if(Math.Abs(colors[i].r-colors[j].r)+Math.Abs(colors[i].g-colors[j].g)+Math.Abs(colors[i].b-colors[j].b)<.35f)distinct=false;
   Check(distinct,"faction color spectra are clearly distinct");
   // 初始关系
   var colony=Factions.Colony;var hostile=Factions.Hostile;var ember=Factions.Ember;var illithid=Factions.Illithid;
   Check(Math.Abs(colony.PlayerGoodwill-40)<=10 && colony.PlayerRelationKind==FactionRelationKind.Neutral,"Faerun colony starts friendly: "+colony.PlayerGoodwill);
   Check(Math.Abs(hostile.PlayerGoodwill+80)<=10 && hostile.PlayerRelationKind==FactionRelationKind.Hostile,"Thayan expedition starts near -80 and hostile: "+hostile.PlayerGoodwill);
   Check(Math.Abs(ember.PlayerGoodwill+20)<=10 && ember.PlayerRelationKind==FactionRelationKind.Neutral,"ember clan starts cool but neutral: "+ember.PlayerGoodwill);
   Check(illithid.PlayerRelationKind==FactionRelationKind.Hostile && illithid.PlayerGoodwill==-100,"illithid colony is hostile at -100");
   Check(ember.RelationKindWith(hostile)==FactionRelationKind.Hostile && hostile.RelationKindWith(ember)==FactionRelationKind.Hostile,"ember clan and Thayan expedition are mutual enemies");
   // 设置开关
   var s=LichMod.Settings;
   s.factionEmber=false;Factions.ApplyToggles();
   var defs=Factions.Managed.ToList();Factions.FilterWorldFactions(defs);
   Check(Factions.EmberDef.startingCountAtWorldCreation==0 && !defs.Contains(Factions.EmberDef) && defs.Contains(Factions.ColonyDef),"disabled faction is left out of world generation");
   Check(Factions.EmberDef.RaidCommonalityFromPoints(1000)==0 && Factions.Of(Factions.EmberDef)!=null,"disabled faction stays in the save but stops raiding");
   s.factionEmber=true;s.factionBlackNet=false;Factions.ApplyToggles();
   Check(Factions.BlackNetDef.requiredCountAtGameStart==0 && Factions.EmberDef.startingCountAtWorldCreation==1 && Factions.EmberDef.RaidCommonalityFromPoints(1000)>0,"hidden faction toggle controls its generation; re-enabled faction raids again");
   s.factionBlackNet=true;Factions.ApplyToggles();
   Check(Factions.BlackNetDef.requiredCountAtGameStart==1,"black network is generated when enabled");
   int factionCount=Find.FactionManager.AllFactionsListForReading.Count;
   Check(Factions.AddMissingToCurrentGame()==0 && Find.FactionManager.AllFactionsListForReading.Count==factionCount,"adding factions to a save that already has them changes nothing");
  }

  void CheckFactionGroups(){
   var colony=Factions.Colony;var hostile=Factions.Hostile;var ember=Factions.Ember;var illithid=Factions.Illithid;
   foreach(var f in new[]{colony,hostile,ember,illithid}){
    var g=FactionGroup(f,PawnGroupKindDefOf.Combat,1500);
    // 1.63.0：塞伊的霜巨人掠袭者会带冬狼（同派系的动物，见 FactionGroups），检查原来只认人形，偶尔抽到霜巨人就失败。
    Check(g.Count>=3 && g.All(p=>p.Faction==f && (p.RaceProps.Humanlike || FrostGiant.IsWinterWolf(p))),"combat group generates for "+f.def.defName+": "+g.Count+(g.Count>=3?"":" ("+GroupDiag(f,1500)+")")+" "+string.Join(",",g.Where(p=>p.Faction!=f || !(p.RaceProps.Humanlike || FrostGiant.IsWinterWolf(p))).Select(p=>p.kindDef?.defName+"/"+p.Faction?.def.defName+"/"+p.Destroyed)));
    Check(g.Where(p=>Necro.Construct(p)==null).All(p=>p.kindDef.defaultFactionDef==f.def || p.kindDef.defName.StartsWith("LP_")),"combat group uses the faction's own kinds: "+f.def.defName);
   }
   // 费伦拓荒同盟的种族比例（宽松阈值）
   var people=new List<Pawn>();
   // 1.63.0：样本约 100 人，比例按随机数上下浮动几个百分点；生成用固定种子，结果确定。1.57.0 把一部分份额分给了亚种（山地矮人、木精灵），矮人 / 精灵按族系算（检查原来只认 LP_Dwarf / LP_Elf，精灵常落到 8% 以下）。
   Rand.PushState(16303);try{for(int i=0;i<60 && people.Count<150;i++)people.AddRange(FactionGroup(colony,PawnGroupKindDefOf.Combat,700));}finally{Rand.PopState();}
   // 1.24.0：元素裔、象人、鸟人兵种不用派系异种型，单独统计；原有比例只在原来的兵种里量。
   // 1.38.0：流浆人游民同样不用派系异种型，一并单独统计。
   var new124=new[]{"LP_FC_Genasi","LP_FC_LoxodonWarden","LP_FC_AarakocraScout","LP_FC_OozeWanderer"};float share124=people.Count(p=>new124.Contains(p.kindDef.defName))/(float)Math.Max(1,people.Count);
   Check(share124>.05f && share124<.4f,"1.24.0 genasi, loxodon and aarakocra make up part of Faerun combat groups: "+share124.ToString("P0"));
   people=people.Where(p=>!new124.Contains(p.kindDef.defName)).ToList();
   int n=people.Count;
   float Share(Func<Pawn,bool> pred)=>people.Count(pred)/(float)n;
   float human=Share(p=>Xeno(p)=="Baseliner"),dwarf=Share(p=>Xeno(p)=="LP_Dwarf" || Xeno(p)=="LP_HillDwarf"),elf=Share(p=>Xeno(p)=="LP_Elf" || Xeno(p)=="LP_WoodElf"),orc=Share(p=>Xeno(p)=="LP_HalfOrc"),holy=Share(p=>p.kindDef.defName=="LP_FC_Paladin" || p.kindDef.defName=="LP_FC_Priest");
   string ratio="n="+n+" human "+human.ToString("P0")+" dwarf "+dwarf.ToString("P0")+" elf "+elf.ToString("P0")+" orc "+orc.ToString("P0")+" paladin/priest "+holy.ToString("P0");
   Check(n>=80 && human>.2f && human<.65f /* 1.63.0：1.57.0 的半精灵、半身人、侏儒也从人类份额里分走，下限 25% → 20% */ && dwarf>.08f && dwarf<.35f && elf>.08f && elf<.35f && orc>.04f && orc<.30f && holy<.2f,"Faerun colony race mix is roughly 40/20/20/15 + 5 holy: "+ratio);
   Check(people.Where(p=>p.kindDef.defName=="LP_FC_Paladin" || p.kindDef.defName=="LP_FC_Priest").All(p=>Xeno(p)=="Baseliner"),"paladins and priests are always human");
   var pal=FactionPawn("LP_FC_Paladin",colony);var priest=FactionPawn("LP_FC_Priest",colony);
   Check(pal.apparel.WornApparel.Any(a=>a.def.defName=="LP_PaladinPlate") && pal.equipment.Primary!=null && pal.equipment.Primary.def.IsMeleeWeapon,"paladin wears paladin plate (1.45.1) and carries a melee weapon");
   Check(priest.apparel.WornApparel.Any(a=>a.def.defName=="LP_MorninglordVestments") && (priest.equipment.Primary==null || priest.equipment.Primary.def.IsMeleeWeapon),"priest wears the vestments of the Morninglord (1.45.1)");
   CleanFactionPawns();
   // 余烬氏族：每组火巨人 1～2 个
   int minG=99,maxG=0;
   for(int i=0;i<100;i++){var g=FactionGroup(ember,PawnGroupKindDefOf.Combat,Rand.Range(150f,2500f));int c=g.Count(FireGiant.Active);minG=Math.Min(minG,c);maxG=Math.Max(maxG,c);if(i%10==9)CleanFactionPawns();}
   Check(minG>=1 && maxG<=FactionGroups.MaxGiants,"ember clan raids always have 1-2 fire giants (seen "+minG+"-"+maxG+")");
   var eg=FactionGroup(ember,PawnGroupKindDefOf.Combat,1200);
   Check(eg.Where(FireGiant.Active).All(p=>p.equipment.Primary?.def.defName=="LP_FireGiantMaul"),"ember fire giants carry forge mauls");
   Check(eg.Where(p=>!FireGiant.Active(p)).All(p=>p.kindDef.defName=="LP_EC_HumanSlave"?Xeno(p)=="Baseliner":p.kindDef.defName=="LP_EC_OgreSlave"?Xeno(p)=="LP_Ogre":Xeno(p)=="LP_HalfOrc"),"ember slaves are humans, half-orcs and (1.24.0) ogres");
   Check(eg.Where(p=>p.kindDef.defName=="LP_EC_HumanSlave").All(p=>p.apparel.WornApparel.Any(a=>a.def.defName=="LP_EmberCollarPlate")),"human slave soldiers wear the ember collar plate (1.45.1)");
   CleanFactionPawns();
   // 塞伊远征军
   int wizards=0,bound=0;bool redRobes=true;
   for(int i=0;i<12;i++){
    var g=FactionGroup(hostile,PawnGroupKindDefOf.Combat,1800);
    foreach(var w in g.Where(p=>p.kindDef.defName=="LP_FH_RedWizard")){
     wizards++;var robe=w.apparel.WornApparel.FirstOrDefault(a=>a.def.defName=="Apparel_Robe" || a.def.defName=="LP_RedWizardRobe");if(robe==null || robe.DrawColor.r<.5f || robe.DrawColor.g>.25f)redRobes=false;
     var mine=g.Where(p=>Necro.Construct(p)?.master==w).ToList();
     if(mine.Count>=2 && mine.All(p=>Necro.Construct(p).raidBound && Necro.Construct(p).HasControl && p.Faction==hostile))bound++;
    }
    if(i==0)Check(g.Where(p=>Necro.Construct(p)==null).All(p=>new[]{"LP_FH_RedWizard","LP_FH_RuneTroll","LP_FH_OrcWarrior","LP_FH_MinotaurCharger","LP_FH_GithMercenary","LP_FH_OgreBrute","LP_FH_IceDevil","LP_FH_Balor","LP_FH_GoblinRaider","LP_FH_KoboldSkulker","LP_FH_DrowBlade","LP_FH_MarilithBlademaster","LP_FH_FrostGiantRaider","LP_WinterWolf","LP_FH_EttinBrute","LP_FH_EnslavedStormGiant"}.Contains(p.kindDef.defName)) /* 1.63.0：补上 HAR 的双头巨人蛮兵（1.44.0）和被奴役的风暴巨人，白名单原来漏了 */,"Thayan raid only uses red wizards, rune trolls, orc warriors, minotaurs, githyanki, (1.24.0) ogres and fiends and (1.57.0) goblinoids, kobolds and drow: "+string.Join(",",g.Where(p=>Necro.Construct(p)==null).Select(p=>p.kindDef.defName).Distinct()));
    if(i==0)Check(g.Where(p=>p.kindDef.defName=="LP_FH_RuneTroll").All(p=>Xeno(p)=="LP_Troll") && g.Where(p=>p.kindDef.defName=="LP_FH_OrcWarrior").All(p=>Xeno(p)=="LP_HalfOrc") && g.Where(p=>p.kindDef.defName=="LP_FH_MinotaurCharger").All(p=>Xeno(p)=="LP_Minotaur") && g.Where(p=>p.kindDef.defName=="LP_FH_OgreBrute").All(p=>Xeno(p)=="LP_Ogre") && g.Where(p=>p.kindDef.defName=="LP_FH_GoblinRaider").All(p=>new[]{"LP_Goblin","LP_Hobgoblin","LP_Bugbear"}.Contains(Xeno(p))) && g.Where(p=>p.kindDef.defName=="LP_FH_KoboldSkulker").All(p=>Xeno(p)=="LP_Kobold") && g.Where(p=>p.kindDef.defName=="LP_FH_DrowBlade").All(p=>Xeno(p)=="LP_Drow") && g.Count(p=>p.kindDef.defName=="LP_FH_IceDevil")<=Fiends.IceDevilMax && g.All(p=>p.kindDef.defName!="LP_FH_Balor"),"Thayan members have the right xenotypes (1800 points: at most 2 ice devils, no balor)");
    if(i%4==3)CleanFactionPawns();
   }
   Check(wizards>0 && bound==wizards,"every red wizard brings 2-3 bound undead constructs ("+bound+"/"+wizards+")");
   Check(redRobes,"red wizards wear red-dyed robes");
   Check(DefDatabase<PawnKindDef>.GetNamed("LP_FH_OrcWarrior").label=="兽人战士","half-orc raiders are labelled 兽人战士");
   int swords=0;bool githOk=true;for(int i=0;i<40;i++){var gith=FactionPawn("LP_FH_GithMercenary",hostile);if(Xeno(gith)!="LP_Githyanki")githOk=false;if(Githyanki.HoldsSword(gith))swords++;}
   Check(githOk,"githyanki mercenaries generate as githyanki");
   Check(swords>0 && swords<30,"githyanki mercenaries sometimes carry a silver sword ("+swords+"/40)");
   CleanFactionPawns();
   // 幽暗殖巢：夺心魔 1～3，奴仆带 LP_Thralled，没有 30 秒支配
   int minF=99,maxF=0;bool thralls=true;string thrallWhy="";
   for(int i=0;i<30;i++){
    var g=FactionGroup(illithid,PawnGroupKindDefOf.Combat,Rand.Range(200f,3000f));
    int c=g.Count(p=>p.kindDef==FactionGroups.FlayerKind);minF=Math.Min(minF,c);maxF=Math.Max(maxF,c);foreach(var nf in g.Where(p=>p.kindDef==FactionGroups.FlayerKind && !MindFlayer.IsMindFlayer(p))){thralls=false;var cg=nf.genes?.GetGene(MindFlayer.CerebrumGene);thrallWhy+=" flayer-not-flayer("+Xeno(nf)+", gene "+(cg==null?"none":cg.Active?"active":"inactive")+", dead "+nf.Dead+")";}
    foreach(var t in g.Where(p=>p.kindDef.defName=="LP_IC_Thrall"))if(!FactionGroups.IsThralled(t) || t.health.hediffSet.hediffs.Any(h=>h.def.defName=="LP_Dominated") || t.InMentalState || MindFlayer.IsMindFlayer(t)){thralls=false;thrallWhy+=" "+FactionGroups.IsThralled(t)+"/"+t.MentalStateDef?.defName+"/"+MindFlayer.IsMindFlayer(t);}
    if(i%10==9)CleanFactionPawns();
   }
   Check(minF>=1 && maxF<=3,"illithid raids have 1-3 mind flayers (seen "+minF+"-"+maxF+")");
   Check(thralls,"illithid thralls carry LP_Thralled and no timed domination"+thrallWhy);
   CleanFactionPawns();
   // 商队
   foreach(var (trader,kind,xeno) in new[]{("LP_Caravan_DwarfSmith","LP_FC_DwarfSmith","LP_Dwarf"),("LP_Caravan_ElfHerbalist","LP_FC_ElfHerbalist","LP_Elf"),("LP_Caravan_ChurchPriest","LP_FC_ChurchPriest","Baseliner"),("LP_Caravan_FaerunGoods","LP_FC_Trader",null)}){
    var g=FactionGroup(colony,PawnGroupKindDefOf.Trader,1000,DefDatabase<TraderKindDef>.GetNamed(trader));
    var t=g.FirstOrDefault(p=>p.kindDef.trader);
    Check(t!=null && t.kindDef.defName==kind && (xeno==null || Xeno(t)==xeno),"Faerun caravan "+trader+" is led by "+kind+" ("+t?.kindDef.defName+" "+(t==null?"":Xeno(t))+")");
    if(trader=="LP_Caravan_DwarfSmith")Check(g.Where(p=>p.RaceProps.Humanlike && !p.kindDef.trader).All(p=>Xeno(p)=="LP_Dwarf"),"dwarf smith caravan is guarded by dwarves");
   }
   foreach(var (f,trader,kind) in new[]{(hostile,"LP_Caravan_RedRobe","LP_FH_RedTrader"),(ember,"LP_Caravan_GiantSmith","LP_EC_GiantSmith"),(Factions.Of(Factions.BlackNetDef),"LP_Caravan_BlackNetwork","LP_BN_Broker")}){
    var g=FactionGroup(f,PawnGroupKindDefOf.Trader,1000,DefDatabase<TraderKindDef>.GetNamed(trader));
    Check(g.Any(p=>p.kindDef.defName==kind),"caravan generates for "+f.def.defName+" led by "+kind);
   }
   var smith=DefDatabase<TraderKindDef>.GetNamed("LP_Caravan_GiantSmith");var black=DefDatabase<TraderKindDef>.GetNamed("LP_Caravan_BlackNetwork");var blood=DefDatabase<ThingDef>.GetNamed("LP_TrollBlood");
   Check(smith.WillTrade(blood) && smith.PriceTypeFor(blood,TradeAction.PlayerSells)==PriceType.Expensive && smith.PriceTypeFor(ThingDefOf.Steel,TradeAction.PlayerSells)==PriceType.Normal,"fire giant smiths pay a premium for troll blood: "+smith.WillTrade(blood)+" "+smith.PriceTypeFor(blood,TradeAction.PlayerSells));
   Check(black.WillTrade(blood) && black.WillTrade(DefDatabase<ThingDef>.GetNamed("Heart")) && black.WillTrade(DefDatabase<ThingDef>.GetNamed("LP_BrainJar")) && black.PriceTypeFor(blood,TradeAction.PlayerSells)==PriceType.Cheap && black.PriceTypeFor(ThingDefOf.Gold,TradeAction.PlayerSells)==PriceType.Cheap && black.PriceTypeFor(ThingDefOf.Gold,TradeAction.PlayerBuys)==PriceType.Expensive,"black network buys organs, troll blood and illithid goods cheaply and sells dear");
   CleanFactionPawns();
  }

  Pawn SpawnFaction(string kind,Faction f){var p=FactionPawn(kind,f);GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(map.Center,map,12),map);return p;}
  static GoodwillSituationDef Situation(string name)=>DefDatabase<GoodwillSituationDef>.GetNamed(name);
  static int SituationValue(string name,Faction f){ColonyCensus.Dirty();return Situation(name).Worker.GetNaturalGoodwillOffset(f);}
  Pawn Colonist()=>map.mapPawns.FreeColonistsSpawned.First(p=>!Necro.Undead(p) && !MindFlayer.IsMindFlayer(p));

  void CheckRacialGoodwill(){
   var colony=Factions.Colony;var emberF=Factions.Ember;
   foreach(var name in new[]{"LP_Goodwill_ColonyUndead","LP_Goodwill_ColonyConstructs","LP_Goodwill_ColonyHalfOrc","LP_Goodwill_EmberGiant","LP_Goodwill_EmberTroll","LP_Goodwill_DwarfBloodDebt"}){var d=Situation(name);Check(!d.ConfigErrors().Any() && (d.modExtensions==null || d.modExtensions.All(e=>!e.ConfigErrors().Any())) && d.label.Any(ch=>ch>0x4e00),"goodwill situation validates with a Chinese reason: "+name);}
   ColonyCensus.Dirty();bool hadUndead=ColonyCensus.HasLichOrFlayer;
   Check(SituationValue("LP_Goodwill_ColonyUndead",emberF)==0 && SituationValue("LP_Goodwill_EmberGiant",colony)==0,"racial goodwill entries only apply to their own faction");
   if(!hadUndead)Check(SituationValue("LP_Goodwill_ColonyUndead",colony)==0,"no lich or mind flayer, no undead goodwill penalty");
   var gsm=Find.FactionManager.goodwillSituationManager;
   gsm.RecalculateAll(false);int natural0=colony.NaturalGoodwill;
   var flayer=RacePawn("LP_MindFlayer");
   Check(SituationValue("LP_Goodwill_ColonyUndead",colony)==-25,"a mind flayer colonist costs -25 natural goodwill with the Faerun colony");
   var flayer2=RacePawn("LP_MindFlayer");
   Check(SituationValue("LP_Goodwill_ColonyUndead",colony)==-25,"two mind flayers still count only once (-25)");
   gsm.RecalculateAll(false);
   if(!hadUndead)Check(colony.NaturalGoodwill==natural0-25,"natural goodwill really drops by 25 ("+natural0+" -> "+colony.NaturalGoodwill+")");
   Check(gsm.GetExplanation(colony).Contains("殖民地里有巫妖或夺心魔"),"faction screen explains the penalty in Chinese");
   flayer.Destroy();flayer2.Destroy();
   var lich=RacePawn("LP_Elf");lich.health.AddHediff(Necro.Def<HediffDef>("Lich"));
   Check(SituationValue("LP_Goodwill_ColonyUndead",colony)==-25,"a lich colonist triggers the same -25");
   lich.Destroy();
   var prisoner=RacePawn("LP_MindFlayer");prisoner.SetFaction(Factions.Hostile);prisoner.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);
   Check(prisoner.IsPrisonerOfColony && SituationValue("LP_Goodwill_ColonyUndead",colony)==-25,"mind flayer prisoners count too");
   prisoner.guest.SetGuestStatus(null);
   if(!hadUndead)Check(SituationValue("LP_Goodwill_ColonyUndead",colony)==0,"a visiting mind flayer of another faction does not count");
   LichMod.Settings.factionColony=false;bool off=SituationValue("LP_Goodwill_ColonyUndead",colony)==0;prisoner.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);off&=SituationValue("LP_Goodwill_ColonyUndead",colony)==0;LichMod.Settings.factionColony=true;
   Check(off,"disabled faction ignores racial goodwill");
   prisoner.guest.SetGuestStatus(null);prisoner.SetFaction(Faction.OfPlayer);prisoner.Destroy();
   // 死灵造物：每个 -2，最多 -10
   int c0=ColonyCensus.Constructs;var master=Colonist();var made=new List<Pawn>();
   for(int i=0;i<2;i++)made.Add(Constructs.Create("Skeleton",master,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map));
   Check(SituationValue("LP_Goodwill_ColonyConstructs",colony)==Math.Max(-10,-2*(c0+2)),"each player construct costs -2 ("+(c0+2)+" constructs)");
   for(int i=0;i<5;i++)made.Add(Constructs.Create("Skeleton",master,CellFinder.RandomClosewalkCellNear(map.Center,map,10),map));
   Check(SituationValue("LP_Goodwill_ColonyConstructs",colony)==-10,"construct penalty is capped at -10");
   Check(Situation("LP_Goodwill_ColonyConstructs").Worker.GetPostProcessedLabel(colony).Contains("具"),"construct reason shows the count");
   foreach(var p in made){Necro.Registry.constructs.Remove(p);p.Destroy();}
   ColonyCensus.Dirty();bool hadOrc=ColonyCensus.HasHalfOrc;var orc=RacePawn("LP_HalfOrc");
   Check(SituationValue("LP_Goodwill_ColonyHalfOrc",colony)==5,"a half-orc colonist gives +5 with the Faerun colony");
   orc.Destroy();if(!hadOrc)Check(SituationValue("LP_Goodwill_ColonyHalfOrc",colony)==0,"half-orc bonus ends when they leave");
   ColonyCensus.Dirty();bool hadGiant=ColonyCensus.HasFireGiant,hadTroll=ColonyCensus.HasTroll;
   if(!hadGiant)Check(SituationValue("LP_Goodwill_EmberGiant",emberF)==0,"no fire giant, no ember bonus");
   var giant=RacePawn("LP_FireGiant");
   Check(SituationValue("LP_Goodwill_EmberGiant",emberF)==15,"a fire giant colonist gives +15 with the ember clan");
   var troll=RacePawn("LP_Troll");
   Check(SituationValue("LP_Goodwill_EmberTroll",emberF)==-15 && SituationValue("LP_Goodwill_EmberGiant",emberF)==15,"a troll colonist costs -15 with the ember clan, independently of the giant bonus");
   giant.Destroy();troll.Destroy();
   if(!hadTroll)Check(SituationValue("LP_Goodwill_EmberTroll",emberF)==0,"troll penalty ends when the troll leaves");
   ColonyCensus.Dirty();
  }

  // 1.20.0：种族判断按基因——自定义异种型加巨魔再生 / 不屈坚韧 / 矮人体型基因也能被认出来。
  void CheckGeneBasedRaces(){
   var troll=RacePawn("Baseliner");troll.genes.AddGene(Troll.RegenerationGene,true);ColonyCensus.Dirty();
   Check(Troll.Is(troll) && Decapitation.IsTroll(troll) && CommonRaces.IsTroll(troll) && Decapitation.ReactionFor(troll)==Decapitation.Reaction.Berserk,"a custom xenotype with troll regeneration is a troll for decapitation and dwarf bane");
   Check(DefDatabase<RecipeDef>.GetNamed("LP_ExtractTrollBlood").Worker.AvailableOnNow(troll),"troll blood can be drawn from a custom-xenotype troll");
   Check(ColonyCensus.HasTroll && SituationValue("LP_Goodwill_EmberTroll",Factions.Ember)==-15,"the colony census counts a custom-xenotype troll (ember -15)");
   var m=RacePawn("LP_Minotaur");m.drafter.Drafted=true;troll.drafter.Drafted=true;RacialRelations.Tracker.Census(map).Rebuild(map);
   Check(Near(Minotaur.TrollBond(m),1.05f) && Near(Minotaur.TrollBond(troll),1.05f),"minotaurs fight beside a custom-xenotype troll (+5% both ways)");
   var plain=RacePawn("Baseliner");Check(!Troll.Is(plain) && !CommonRaces.IsHalfOrc(plain) && !CommonRaces.IsDwarf(plain),"an ordinary baseliner is no troll, half-orc or dwarf");
   plain.genes.AddGene(CommonRaces.RelentlessGene,true);ColonyCensus.Dirty();
   Check(CommonRaces.IsHalfOrc(plain) && ColonyCensus.HasHalfOrc,"relentless endurance makes a custom xenotype count as a half-orc in the census");
   plain.genes.AddGene(CommonRaces.StatureGene,true);
   Check(CommonRaces.IsDwarf(plain),"dwarf stature makes a custom xenotype count as a dwarf");
   foreach(var p in new[]{troll,m,plain})p.Destroy();ColonyCensus.Dirty();
  }

  void CheckFactionScenarios(){
   var expect=new[]{("LP_LichScenario",Factions.HostileDef,0),("LP_FireGiantScenario",Factions.EmberDef,-40),("LP_TrollScenario",Factions.HostileDef,0)};
   foreach(var (name,fac,gw) in expect){
    var scen=DefDatabase<ScenarioDef>.GetNamed(name).scenario;var parts=scen.AllParts.OfType<ScenPart_FactionGoodwill>().ToList();
    Check(parts.Count==1 && parts[0].faction==fac && parts[0].goodwill==gw && !parts[0].ConfigErrors().Any() && !string.IsNullOrEmpty(parts[0].Summary(scen)),"scenario "+name+" starts "+fac.defName+" at "+gw);
    Factions.SetRelation(Factions.Of(fac),Faction.OfPlayer,-77);
    Check(parts[0].Apply() && Factions.Of(fac).PlayerGoodwill==gw && Factions.Of(fac).PlayerRelationKind==Factions.KindFor(gw),"scenario goodwill applies: "+name+" "+Factions.Of(fac).PlayerGoodwill);
   }
   Check(!DefDatabase<ScenarioDef>.GetNamed("LP_LichScenario").scenario.AllParts.OfType<ScenPart_FactionGoodwill>().Any(p=>p.faction==Factions.ColonyDef),"lich scenario adds no extra Faerun colony penalty (racial goodwill only)");
   Factions.SetRelation(Factions.Hostile,Faction.OfPlayer,-80);Factions.SetRelation(Factions.Ember,Faction.OfPlayer,-20);
  }

  void CheckDecapitation(){
   var hostile=Factions.Hostile;var tracker=Decapitation.Tracker;
   Check(tracker!=null,"decapitation tracker exists");
   // 红袍法师死亡：造物崩解、巨魔狂暴、兽人逃跑、牛头人不受影响
   var wizard=SpawnFaction("LP_FH_RedWizard",hostile);
   var skel=FactionConstructs.MakeBound("Skeleton",wizard,hostile);factionMade.Add(skel);GenSpawn.Spawn(skel,CellFinder.RandomClosewalkCellNear(wizard.Position,map,4),map);
   var troll=SpawnFaction("LP_FH_RuneTroll",hostile);var orc=SpawnFaction("LP_FH_OrcWarrior",hostile);var minotaur=SpawnFaction("LP_FH_MinotaurCharger",hostile);
   tracker.Assign(new List<Pawn>{wizard,skel,troll,orc,minotaur},hostile);
   Check(tracker.LeaderOf(skel)==wizard && tracker.LeaderOf(troll)==wizard && tracker.LeaderOf(orc)==wizard && tracker.LeaderOf(minotaur)==null && tracker.LeaderOf(wizard)==null,"red wizard leads his construct, rune troll and orc; minotaurs are independent");
   Necro.Construct(skel).Maintain(250);
   Check(Necro.Construct(skel).HasControl && !Necro.Construct(skel).dormant && !skel.Downed,"bound skeleton stays active while its red wizard stands");
   wizard.Kill(null);
   int r=tracker.Process();
   Check(r==3 && skel.Dead,"bound construct collapses when its red wizard dies");
   Check(troll.MentalStateDef==MentalStateDefOf.Berserk,"rune troll goes berserk when its red wizard dies: "+troll.MentalStateDef);
   Check(orc.MentalStateDef==MentalStateDefOf.PanicFlee,"orc warrior flees when its red wizard dies");
   Check(!minotaur.InMentalState,"minotaur charger ignores the red wizard's death");
   Check(tracker.FollowersOf(wizard).Count==0 && tracker.Process()==0,"decapitation reacts only once");
   // 设置关闭：巨魔改逃跑；领队倒地也触发
   LichMod.Settings.decapTrollBerserk=false;
   var wizard2=SpawnFaction("LP_FH_RedWizard",hostile);var troll2=SpawnFaction("LP_FH_RuneTroll",hostile);
   tracker.Assign(new List<Pawn>{wizard2,troll2},hostile);
   wizard2.health.AddHediff(HediffDefOf.Anesthetic);if(!wizard2.Downed)HealthUtility.DamageUntilDowned(wizard2,false);
   Check(wizard2.Downed && !wizard2.Dead,"red wizard is downed for the fixture");
   tracker.Process();
   Check(troll2.MentalStateDef==MentalStateDefOf.PanicFlee,"with the berserk setting off, the rune troll flees when its wizard goes down");
   LichMod.Settings.decapTrollBerserk=true;
   // 夺心魔倒下：奴仆清醒、倒地、脱离派系
   var illithid=Factions.Illithid;
   var flayer=SpawnFaction("LP_IC_MindFlayer",illithid);var thrall=SpawnFaction("LP_IC_Thrall",illithid);FactionGroups.Thrall(thrall);
   tracker.Assign(new List<Pawn>{flayer,thrall},illithid);
   Check(tracker.LeaderOf(thrall)==flayer && FactionGroups.IsThralled(thrall),"mind flayer leads its thrall");
   flayer.Kill(null);tracker.Process();
   Check(!FactionGroups.IsThralled(thrall) && thrall.Downed && thrall.Faction==null && !thrall.HostileTo(Faction.OfPlayer) && thrall.health.hediffSet.HasHediff(Decapitation.Awakening),"freed thrall loses LP_Thralled, collapses, leaves the hive and can be captured");
   // 火巨人死亡：奴兵逃跑
   var emberF=Factions.Ember;var giant=SpawnFaction("LP_EC_FireGiant",emberF);var slave=SpawnFaction("LP_EC_HumanSlave",emberF);
   tracker.Assign(new List<Pawn>{giant,slave},emberF);
   Check(tracker.LeaderOf(slave)==giant,"fire giant leads its slave soldier");
   giant.Kill(null);tracker.Process();
   Check(slave.MentalStateDef==MentalStateDefOf.PanicFlee,"slave soldier flees when the fire giant falls");
   // 随从上限与平均分配
   var w1=FactionPawn("LP_FH_RedWizard",hostile);var orcs=Enumerable.Range(0,10).Select(i=>FactionPawn("LP_FH_OrcWarrior",hostile)).ToList();
   tracker.Assign(new List<Pawn>{w1}.Concat(orcs).ToList(),hostile);
   int cap=Factions.Ext(hostile).followersPerLeader;
   Check(orcs.Count(o=>tracker.LeaderOf(o)==w1)==cap,"one leader keeps at most "+cap+" followers");
   var w2=FactionPawn("LP_FH_RedWizard",hostile);var w3=FactionPawn("LP_FH_RedWizard",hostile);
   tracker.Assign(new List<Pawn>{w2,w3}.Concat(orcs).ToList(),hostile);
   Check(orcs.Count(o=>tracker.LeaderOf(o)==w2)==5 && orcs.Count(o=>tracker.LeaderOf(o)==w3)==5,"two leaders split ten followers evenly");
   CleanFactionPawns();
  }

  void CheckBurrow(){
   var def=DefDatabase<PawnsArrivalModeDef>.GetNamed("LP_Burrow");var illithid=Factions.Illithid;
   Check(RaidStrategyDefOf.ImmediateAttack.arriveModes.Contains(def) && Factions.IllithidDef.arrivalModeWhitelist.Contains(def) && def.textEnemy.Contains("从地下"),"burrow arrival is patched into immediate attacks and announces 从地下");
   Check(def.Worker.CanUseWith(new IncidentParms{target=map,faction=illithid,points=500}) && !def.Worker.CanUseWith(new IncidentParms{target=map,faction=Factions.Hostile,points=500}),"only the illithid colony can burrow");
   bool open=true;
   for(int i=0;i<10;i++){var c=PawnsArrivalModeWorker_Burrow.FindCenter(map);if(!c.IsValid || !PawnsArrivalModeWorker_Burrow.Burrowable(c,map))open=false;}
   Check(open,"burrow centers are always open, standable, explored ground");
   var parms=new IncidentParms{target=map,faction=illithid,points=500};
   Check(def.Worker.TryResolveRaidSpawnCenter(parms) && parms.spawnCenter.IsValid,"burrow resolves a spawn center");
   var pawns=Enumerable.Range(0,6).Select(i=>FactionPawn("LP_IC_Thrall",illithid)).ToList();
   def.Worker.Arrive(pawns,parms);
   Check(pawns.All(p=>p.Spawned && p.Position.Standable(map) && p.Position.GetEdifice(map)==null && !p.Position.Fogged(map)),"burrowing raiders emerge on open ground, never inside walls");
   Check(pawns.All(p=>p.stances.stunner.Stunned) && pawns.All(p=>p.Position.DistanceTo(parms.spawnCenter)<=9),"burrowing raiders are briefly stunned around the hole: "+string.Join(",",pawns.Select(p=>p.stances.stunner.StunTicksLeft+"t/"+p.Position.DistanceTo(parms.spawnCenter).ToString("0.#"))));
   CleanFactionPawns();
  }

  void CheckZealots(){
   var zealots=Factions.EnsureHidden(Factions.ZealotsDef);var colony=Factions.Colony;
   Check(zealots!=null && zealots.Hidden && zealots.HostileTo(Faction.OfPlayer),"zealot faction exists, hidden and hostile");
   ColonyCensus.Dirty();bool had=ColonyCensus.HasLichOrFlayer;
   if(!had)Check(!IncidentWorker_ZealotRaid.Allowed(map),"no zealots without a lich or mind flayer");
   var flayer=RacePawn("LP_MindFlayer");ColonyCensus.Dirty();
   Check(IncidentWorker_ZealotRaid.Allowed(map),"zealots may come when the colony harbours a mind flayer");
   LichMod.Settings.zealotRaids=false;Check(!IncidentWorker_ZealotRaid.Allowed(map),"zealot setting turns them off");LichMod.Settings.zealotRaids=true;
   int gw=colony.PlayerGoodwill;Factions.SetRelation(colony,Faction.OfPlayer,-90);
   Check(!IncidentWorker_ZealotRaid.Allowed(map),"no zealots once the Faerun colony itself is hostile");
   Factions.SetRelation(colony,Faction.OfPlayer,gw);
   Check(IncidentWorker_ZealotRaid.Frequency(100)<IncidentWorker_ZealotRaid.Frequency(0) && IncidentWorker_ZealotRaid.Frequency(0)<IncidentWorker_ZealotRaid.Frequency(-60),"lower goodwill makes zealots more frequent");
   var g=FactionGroup(zealots,PawnGroupKindDefOf.Combat,800);
   Check(g.Count>0 && g.All(p=>(p.kindDef.defName=="LP_FC_Paladin" || p.kindDef.defName=="LP_FC_Priest") && Xeno(p)=="Baseliner"),"zealots are human paladins and priests");
   CleanFactionPawns();
   Check(IncidentWorker_ZealotRaid.Targets(map).Contains(flayer),"zealots target the colony's mind flayer");
   var ev=Tribute.Events;int repelled=ev.zealotsRepelled;
   var def=DefDatabase<IncidentDef>.GetNamed("LP_ZealotRaid");
   var colonistsBefore=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
   Check(def.Worker.TryExecute(new IncidentParms{target=map,points=400}),"zealot raid fires");
   var raiders=map.mapPawns.SpawnedPawnsInFaction(zealots).Where(p=>!colonistsBefore.Contains(p)).ToList();
   Check(raiders.Count>0 && ev.zealotRaid.Count==raiders.Count,"zealot raiders arrive and are tracked ("+raiders.Count+")");
   Check(raiders.All(p=>p.GetLord()?.LordJob is LordJob_AssaultThings),"zealots go for the lich, mind flayers and phylacteries first");
   var letter=Find.LetterStack.LettersListForReading.LastOrDefault();
   Check(letter!=null && letter.Label.ToString().Contains("猎巫"),"zealot raid letter is labelled 猎巫狂热分子");
   int gw0=colony.PlayerGoodwill;
   raiders[0].Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,Colonist()));
   Check(colony.PlayerGoodwill==gw0,"killing zealots does not affect Faerun colony goodwill");
   if(raiders.Count>1){
    var captive=raiders[1];captive.guest.SetGuestStatus(Faction.OfPlayer,GuestStatus.Prisoner);
    GenGuest.PrisonerRelease(captive);
    Check(colony.PlayerGoodwill==Math.Min(colony.def.permanentEnemy?-100:100,gw0+ZealotRelease.Goodwill),"releasing a captured zealot pleases the Faerun colony (+5): "+gw0+" -> "+colony.PlayerGoodwill);
    captive.Destroy();
   }
   int fired=0;Action<int> hook=n=>fired=n;FactionEvents.ZealotsRepelled+=hook;
   foreach(var p in raiders)if(!p.Destroyed && !p.Dead)p.Kill(null);
   ev.CheckZealots();FactionEvents.ZealotsRepelled-=hook;
   Check(ev.zealotsRepelled==repelled+1 && fired==repelled+1 && ev.zealotRaid.Count==0,"beaten zealot raids are counted and announced to the inquisitor hook");
   flayer.Destroy();ColonyCensus.Dirty();
  }

  void CheckHarper(){
   var colony=Factions.Colony;var hostile=Factions.Hostile;var ev=Tribute.Events;
   Factions.SetRelation(hostile,Faction.OfPlayer,-80);Factions.SetRelation(colony,Faction.OfPlayer,80);
   Check(Harper.Active(),"harper warnings are active at goodwill 80");
   Check(Harper.BigRaid(hostile,100) && Harper.BigRaid(Factions.Illithid,100) && Harper.BigRaid(Factions.Ember,100),"raids from our three hostile factions always count as large");
   var pirates=Find.FactionManager.OfPirates;
   if(pirates!=null)Check(!Harper.BigRaid(pirates,300) && Harper.BigRaid(pirates,2000),"other factions count as large above "+Harper.PointsThreshold+" points");
   var worker=(IncidentWorker_RaidEnemy)IncidentDefOf.RaidEnemy.Worker;
   int q=ev.harperQueue.Count;var spawned=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);int now=Find.TickManager.TicksGame;
   Check(worker.TryExecute(new IncidentParms{target=map,points=300,faction=hostile}) && ev.harperQueue.Count==q+1,"a Thayan raid is held back by the harpers");
   Check(!map.mapPawns.AllPawnsSpawned.Any(p=>!spawned.Contains(p)),"the delayed raid does not land yet");
   var entry=ev.harperQueue.Last();
   Check(entry.fireTick>=now+GenDate.TicksPerDay && entry.fireTick<=now+2*GenDate.TicksPerDay && entry.parms.faction==hostile,"the raid is postponed by one to two days");
   Check(Find.LetterStack.LettersListForReading.Any(l=>l.Label.ToString().Contains("竖琴手") && l.relatedFaction==hostile),"harpers send a warning letter naming the faction");
   Factions.SetRelation(colony,Faction.OfPlayer,30);Check(!Harper.Active(),"no harper warnings below goodwill 60");
   Factions.SetRelation(colony,Faction.OfPlayer,80);LichMod.Settings.harperWarning=false;Check(!Harper.Active(),"harper setting turns warnings off");LichMod.Settings.harperWarning=true;
   Check(Harper.Process(ev,true)==1 && ev.harperQueue.Count==q,"the postponed raid lands when its time comes");
   var raiders=map.mapPawns.AllPawnsSpawned.Where(p=>!spawned.Contains(p)).ToList();
   Check(raiders.Count>0 && raiders.All(p=>p.Faction==hostile),"the delayed raid brings the Thayan expedition ("+raiders.Count+")");
   foreach(var p in raiders){Necro.Registry?.constructs.Remove(p);p.Destroy();}
   Factions.SetRelation(colony,Faction.OfPlayer,40);
  }

  Building tradeBeacon;
  int SilverNearBeacon()=>map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t=>t.Position.DistanceTo(tradeBeacon.Position)<=6).Sum(t=>t.stackCount);
  void EnsureBeacon(int silver){
   if(tradeBeacon==null || tradeBeacon.Destroyed){
    var cell=map.AllCells.Where(c=>c.x>10 && c.z>10 && c.x<map.Size.x-10 && c.z<map.Size.z-10 && GenRadial.RadialCellsAround(c,3f,true).All(x=>x.Standable(map) && !x.Fogged(map) && x.GetFirstItem(map)==null && x.GetEdifice(map)==null && x.GetFirstPawn(map)==null)).OrderBy(c=>c.DistanceToSquared(map.Center)).First();
    tradeBeacon=(Building)ThingMaker.MakeThing(ThingDefOf.OrbitalTradeBeacon);tradeBeacon.SetFactionDirect(Faction.OfPlayer);GenSpawn.Spawn(tradeBeacon,cell,map);
   }
   var power=tradeBeacon.TryGetComp<CompPowerTrader>();if(power!=null)power.PowerOn=true;
   int have=SilverNearBeacon();
   while(have<silver){var s=ThingMaker.MakeThing(ThingDefOf.Silver);s.stackCount=Math.Min(ThingDefOf.Silver.stackLimit,silver-have);GenPlace.TryPlaceThing(s,tradeBeacon.Position+IntVec3.North,map,ThingPlaceMode.Near);have+=s.stackCount;}
  }

  void CheckTribute(){
   var hostile=Factions.Hostile;var ev=Tribute.Events;int now=Find.TickManager.TicksGame;
   // 红袍招揽：效忠→中立
   Factions.SetRelation(hostile,Faction.OfPlayer,-80);ev.pledged=false;
   Tribute.Pledge();
   Check(ev.pledged && hostile.PlayerGoodwill==0 && hostile.PlayerRelationKind==FactionRelationKind.Neutral && ev.nextSoulTick==now+Tribute.SeasonTicks,"pledging to Thay sets goodwill to neutral and schedules the soul tribute");
   Factions.SetRelation(hostile,Faction.OfPlayer,-20);ev.pledged=false;
   var l=FactionEvents.SendRecruit();
   Check(Find.LetterStack.LettersListForReading.Contains(l) && l.Choices.Count()==3,"recruitment letter offers pledge, refuse and postpone");
   l.Choices.First(o=>OptText(o)=="拒绝").action();
   Check(hostile.PlayerGoodwill==-20+Tribute.RefusePledge && !Find.LetterStack.LettersListForReading.Contains(l),"refusing the red robes costs 10 goodwill and closes the letter");
   // 上缴灵魂
   float stored0=Tribute.StoredSouls();
   var box=(Phylactery)ThingMaker.MakeThing(Necro.Def<ThingDef>("Phylactery"));box.soul=Colonist();box.experience=200;box.Register();
   Check(Math.Abs(Tribute.StoredSouls()-stored0-200)<.01f,"stored souls read the phylactery's offering experience");
   if(stored0<1){
    Check(Tribute.PaySouls() && Math.Abs(box.experience-50)<.01f,"paying the soul tribute takes 150 offering experience");
    Check(!Tribute.PaySouls() && Math.Abs(box.experience-50)<.01f,"cannot pay the soul tribute without enough experience");
    var souls=FactionEvents.SendSouls();
    Check(souls.Choices.First(o=>OptText(o).StartsWith("上缴")).disabled,"soul tribute option is disabled when the phylactery is short");
    Factions.SetRelation(hostile,Faction.OfPlayer,0);
    souls.Choices.First(o=>OptText(o).StartsWith("拒交")).action();
    Check(hostile.PlayerGoodwill==Tribute.RefuseSoulsGoodwill,"refusing the soul tribute costs 30 goodwill");
    // 1.20.0：经验不够时倒扣等级
    box.level=3;box.experience=20;int cap0=box.Capacity;
    Check(Tribute.CanPaySouls() && Near(Tribute.PayableSouls(),20+Necro.Requirement(1)+Necro.Requirement(2)),"levels count toward the soul tribute: "+Tribute.PayableSouls());
    var souls2=FactionEvents.SendSouls();var payOpt=souls2.Choices.First(o=>OptText(o).StartsWith("上缴"));
    Check(!payOpt.disabled && OptText(payOpt).Contains("会使命匣从3级降到1级"),"the pay button says the phylactery drops from level 3 to 1: "+OptText(payOpt));
    payOpt.action();
    Check(box.level==1 && Near(box.experience,20+Necro.Requirement(1)+Necro.Requirement(2)-Tribute.SoulDemand) && box.Capacity<cap0,"paying with too little experience demotes the phylactery and returns that level's experience: level "+box.level+" exp "+box.experience);
    box.level=2;box.experience=0;
    Check(!Tribute.CanPaySouls(),"one level and no experience is still not enough for 150");
    box.level=2;box.experience=60;
    Check(Tribute.CanPaySouls() && Tribute.DemotionText()=="会使命匣从2级降到1级" && Tribute.PaySouls() && box.level==1 && Near(box.experience,10),"a level-2 phylactery with 60 experience drops to level 1 and keeps 10");
    Check(!Tribute.CanPaySouls() && FactionEvents.SendSouls().Choices.First(o=>OptText(o).StartsWith("上缴")).disabled,"the pay button is grey only at level 1 without enough experience");
   }
   Necro.Registry.boxes.Remove(box);box.soul=null;box.Destroy();
   // 向红袍进贡
   Factions.SetRelation(hostile,Faction.OfPlayer,-80);ColonyCensus.Dirty();
   Check(Tribute.CanDemand()==!ColonyCensus.HasLich,"tribute demands come only without a lich while Thay is hostile");
   int amount=Tribute.Amount(map);
   Check(amount>=200 && amount<=3000 && amount%50==0,"tribute scales with wealth: "+amount);
   EnsureBeacon(amount+600);
   ev.tributeUntilTick=-1;float w0=Factions.HostileDef.RaidCommonalityFromPoints(1000);float e0=Factions.EmberDef.RaidCommonalityFromPoints(1000);
   int silver0=SilverNearBeacon();now=Find.TickManager.TicksGame;
   var demand=FactionEvents.SendTribute(map);
   Check(demand.Choices.Count()==3 && !demand.Choices.First().disabled,"tribute letter offers payment when silver is at the beacon");
   demand.Choices.First().action();
   Check(SilverNearBeacon()==silver0-amount && ev.tributeUntilTick==now+Tribute.SeasonTicks && Tribute.RaidHalved,"paying tribute takes the silver and protects for one season; silver "+silver0+"->"+SilverNearBeacon()+" amount="+amount+" until="+(ev.tributeUntilTick-now)+" halved="+Tribute.RaidHalved);
   Check(Math.Abs(Factions.HostileDef.RaidCommonalityFromPoints(1000)-w0*.5f)<.0001f && Math.Abs(Factions.EmberDef.RaidCommonalityFromPoints(1000)-e0)<.0001f,"tribute halves only the Thayan raid weight");
   ev.tributeUntilTick=Find.TickManager.TicksGame;
   Check(!Tribute.RaidHalved && Math.Abs(Factions.HostileDef.RaidCommonalityFromPoints(1000)-w0)<.0001f,"tribute protection expires on time");
   LichMod.Settings.tributeDemand=false;Check(!Tribute.CanDemand(),"tribute setting stops the demands");LichMod.Settings.tributeDemand=true;
  }

  // 原版 Faction.CheckReachNaturalGoodwill 每刻跑一次（靠自己的 naturalGoodwillTimer 计时）；这里逐刻调用它模拟 days 天（每天重算一次好感局势），返回最后的好感。
  // 不拨动游戏时间：拨动 TicksGame 会让原版意识形态管理器误删派系意识形态。
  static Action<Faction> reachNatural;
  int SimulateDrift(Faction f,int days){
   if(reachNatural==null)reachNatural=AccessTools.MethodDelegate<Action<Faction>>(AccessTools.Method(typeof(Faction),"CheckReachNaturalGoodwill"));
   var gsm=Find.FactionManager.goodwillSituationManager;
   for(int d=0;d<days;d++){gsm.RecalculateAll(false);for(int i=0;i<GenDate.TicksPerDay;i++)reachNatural(f);}
   gsm.RecalculateAll(false);return f.PlayerGoodwill;
  }
  void CheckGoodwillDrift(){
   var hostile=Factions.Hostile;var ev=Tribute.Events;var gsm=Find.FactionManager.goodwillSituationManager;int start=Find.TickManager.TicksGame;
   foreach(var name in new[]{"LP_Goodwill_ThayPledge","LP_Goodwill_OldPact"})Check(!Situation(name).ConfigErrors().Any() && Situation(name).label.Any(ch=>ch>0x4e00),"goodwill situation validates with a Chinese reason: "+name);
   var lichPawn=RacePawn("LP_Elf");lichPawn.health.AddHediff(Necro.Def<HediffDef>("Lich"));ColonyCensus.Dirty();
   try {
    // 1) 没有修正：好感 0 会被自然好感拉回去
    ev.pledged=false;ev.oldPactFaction=null;Factions.SetRelation(hostile,Faction.OfPlayer,0);gsm.RecalculateAll(false);
    int natural=hostile.NaturalGoodwill;int drifted=SimulateDrift(hostile,61);
    Check(natural<0 && drifted<0,"without a pact Thay's natural goodwill ("+natural+") drags goodwill 0 back down within 60 days (now "+drifted+")");
    // 2) 效忠塞伊：自然好感≥0，不再回落
    Tribute.Pledge();
    Check(hostile.NaturalGoodwill>=0 && SituationValue("LP_Goodwill_ThayPledge",hostile)>0 && gsm.GetExplanation(hostile).Contains("向塞伊宣誓效忠"),"pledging to Thay lifts its natural goodwill to "+hostile.NaturalGoodwill+" and explains why");
    drifted=SimulateDrift(hostile,61);
    Check(drifted>=0 && hostile.PlayerRelationKind!=FactionRelationKind.Hostile,"a pledged colony keeps Thay neutral for 60+ days (goodwill "+drifted+")");
    Check(SituationValue("LP_Goodwill_ThayPledge",Factions.Colony)==0 && SituationValue("LP_Goodwill_OldPact",hostile)==0,"the pledge only touches Thay and the old pact does not stack on it");
    // 拒交灵魂或巫妖没了：修正消失
    Tribute.RefuseSouls();Check(!ev.pledged && SituationValue("LP_Goodwill_ThayPledge",hostile)==0,"refusing the soul tribute ends the pledge and its goodwill support");
    Tribute.Pledge();lichPawn.health.RemoveHediff(Necro.Lich(lichPawn));ColonyCensus.Dirty();gsm.RecalculateAll(false);
    Check(SituationValue("LP_Goodwill_ThayPledge",hostile)==0 && hostile.NaturalGoodwill<0,"without a lich the pledge stops shielding Thay's goodwill");
    ev.pledged=false;
    // 3) 旧日盟约：60 天内维持，到期后照常回落
    Factions.SetRelation(hostile,Faction.OfPlayer,0);
    var part=DefDatabase<ScenarioDef>.GetNamed("LP_LichScenario").scenario.AllParts.OfType<ScenPart_FactionGoodwill>().Single();
    Check(Math.Abs(part.pactDays-60)<.01f && part.Summary(null).Contains("旧日盟约") && DefDatabase<ScenarioDef>.GetNamed("LP_TrollScenario").scenario.AllParts.OfType<ScenPart_FactionGoodwill>().Single().pactDays>59 && DefDatabase<ScenarioDef>.GetNamed("LP_LichScenario").description.Contains("旧日盟约"),"the lich and troll scenarios carry a 60-day old pact and say so");
    part.PostGameStart();
    Check(ev.oldPactFaction==Factions.HostileDef && ev.oldPactUntilTick==start+60*GenDate.TicksPerDay && hostile.NaturalGoodwill>=0 && Situation("LP_Goodwill_OldPact").Worker.GetPostProcessedLabel(hostile).Contains("还剩60"),"the old pact starts for 60 days with the days left in its reason");
    drifted=SimulateDrift(hostile,59);
    Check(drifted>=0,"goodwill holds at "+drifted+" through the old pact");
    ev.oldPactUntilTick=Find.TickManager.TicksGame;drifted=SimulateDrift(hostile,70);
    Check(!Pact.OldPact(hostile) && hostile.NaturalGoodwill<0 && drifted<0,"after the old pact expires goodwill drifts back down ("+drifted+")");
   } finally {
    ev.pledged=false;ev.oldPactFaction=null;ev.oldPactUntilTick=-1;
    if(!lichPawn.Destroyed)lichPawn.Destroy();ColonyCensus.Dirty();Factions.SetRelation(hostile,Faction.OfPlayer,-80);gsm.RecalculateAll(false);
   }
  }

  void CheckBloodDebt(){
   var colony=Factions.Colony;var ev=Tribute.Events;var worker=Situation("LP_Goodwill_DwarfBloodDebt").Worker;
   ev.dwarfDebts=0;
   Check(worker.GetMaxGoodwill(colony)==100,"no blood debt, no goodwill cap");
   var killer=Colonist();
   var dwarf=SpawnFaction("LP_FC_DwarfGuard",colony);
   Check(Xeno(dwarf)=="LP_Dwarf","dwarf guard fixture is a dwarf");
   dwarf.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,killer));
   Check(ev.dwarfDebts==1 && worker.GetMaxGoodwill(colony)==90,"killing a Faerun dwarf records a blood debt (max goodwill 90)");
   var paladin=SpawnFaction("LP_FC_Paladin",colony);paladin.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,killer));
   var dwarf2=SpawnFaction("LP_FC_DwarfGuard",colony);dwarf2.Kill(null);
   Check(ev.dwarfDebts==1,"only dwarves killed by the player count");
   for(int i=0;i<7;i++)BloodDebt.Add();
   Check(ev.dwarfDebts==BloodDebt.MaxDebts && worker.GetMaxGoodwill(colony)==50,"blood debt caps at five (-50)");
   Check(worker.GetPostProcessedLabel(colony).Contains("5笔"),"blood debt reason shows the count");
   var node=FactionDialogMaker.FactionDialogFor(killer,colony);
   Check(node.options.Any(o=>OptText(o).Contains("偿还一笔矮人血债")),"comms console offers to repay the blood debt");
   EnsureBeacon(BloodDebt.Price+100);int silver0=SilverNearBeacon();
   Check(BloodDebt.Repay(map) && ev.dwarfDebts==4 && SilverNearBeacon()==silver0-BloodDebt.Price && worker.GetMaxGoodwill(colony)==60,"repaying one debt costs 300 silver and lifts the cap by 10");
   // 1.20.0：我方夺心魔吞噬同盟矮人也记一笔；按基因认矮人
   ev.dwarfDebts=0;
   var flayer=RacePawn("LP_MindFlayer");var dwarf3=SpawnFaction("LP_FC_DwarfGuard",colony);dwarf3.health.AddHediff(HediffDefOf.Anesthetic);
   Check(dwarf3.Downed && !dwarf3.Dead && MindFlayer.CannotDevour(flayer,dwarf3)==null,"downed Faerun dwarf can be devoured: "+MindFlayer.CannotDevour(flayer,dwarf3));
   MindFlayer.Devour(flayer,dwarf3);
   Check(dwarf3.Dead && ev.dwarfDebts==1,"a player mind flayer devouring a Faerun dwarf records a blood debt ("+ev.dwarfDebts+")");
   var corpseDwarf=SpawnFaction("LP_FC_DwarfGuard",colony);corpseDwarf.Kill(null);MindFlayer.Devour(flayer,corpseDwarf.Corpse);
   Check(ev.dwarfDebts==1,"devouring a dwarf that was already dead adds no debt");
   var custom=SpawnFaction("LP_FC_Paladin",colony);custom.genes.AddGene(CommonRaces.StatureGene,true);
   Check(BloodDebt.CountsAsDebt(custom,killer) && !BloodDebt.CountsAsDebt(custom,(Thing)null),"a Faerun member with dwarf stature counts as a dwarf for blood debts");
   flayer.Destroy();
   ev.dwarfDebts=0;
  }

  void CheckEnemyTrollRevival(){
   var troll=SpawnFaction("LP_FH_RuneTroll",Factions.Hostile);
   troll.Kill(new DamageInfo(DamageDefOf.Cut,999f,0f,-1f,Colonist()));
   Check(troll.Dead && Troll.Gene(troll)?.reviveTick>=0,"enemy rune trolls revive through the 1.12.0 regeneration unless burned");
   troll.Corpse?.Destroy();Troll.Revivals?.Process();
  }

  void CheckBlackNetwork(){
   var def=DefDatabase<IncidentDef>.GetNamed("LP_BlackNetworkCaravan");var bn=Factions.Of(Factions.BlackNetDef);
   Check(def.baseChance==0 && def.Worker is IncidentWorker_BlackNetworkCaravan,"black network caravans are driven by the faction events, not the storyteller lottery");
   Check(!new IncidentWorker_TraderCaravanArrival{def=IncidentDefOf.TraderCaravanArrival}.FactionCanBeGroupSource(bn,new IncidentParms{target=map}),"ordinary caravan events never send the black network");
   var spawned=new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
   Check(def.Worker.TryExecute(StorytellerUtility.DefaultParmsNow(def.category,map)),"black network caravan arrives");
   var visitors=map.mapPawns.AllPawnsSpawned.Where(p=>!spawned.Contains(p)).ToList();
   Check(visitors.Any(p=>p.Faction==bn && p.kindDef.defName=="LP_BN_Broker" && p.TraderKind?.defName=="LP_Caravan_BlackNetwork"),"black network caravan is led by a broker with black network stock");
   var broker=visitors.First(p=>p.kindDef.defName=="LP_BN_Broker");var tadpole=ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("LP_Tadpole"));
   Check(TradeUtility.PlayerSellableNow(tadpole,broker) && !TradeUtility.EverPlayerSellable(tadpole.def),"only the black network buys illithid tadpoles");
   tadpole.Destroy();
   foreach(var p in visitors)p.Destroy();
   LichMod.Settings.factionBlackNet=false;
   Check(!def.Worker.TryExecute(StorytellerUtility.DefaultParmsNow(def.category,map)),"disabled black network sends no caravans");
   LichMod.Settings.factionBlackNet=true;
  }

  // ---------- 存读档 ----------
  static int factionSavedTribute;
  // 1.63.0：-Only Factions 找的是 PrepareFactionsSave；原来读档后检查找不到夹具。
  void PrepareFactionsSave()=>PrepareFactionSave();
  void PrepareFactionSave(){
   var hostile=Factions.Hostile;var ev=Tribute.Events;
   Pawn Make(string kind){var p=Unscathed(PawnGenerator.GeneratePawn(new PawnGenerationRequest(DefDatabase<PawnKindDef>.GetNamed(kind),hostile,PawnGenerationContext.NonPlayer,-1,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true)));Find.WorldPawns.PassToWorld(p,PawnDiscardDecideMode.KeepForever);return p;}
   var wizard=Make("LP_FH_RedWizard");var orc=Make("LP_FH_OrcWarrior");
   Decapitation.Tracker.Bind(orc,wizard);decapLeaderId=wizard.ThingID;decapFollowerId=orc.ThingID;
   ev.dwarfDebts=3;factionSavedTribute=ev.tributeUntilTick=Find.TickManager.TicksGame+12345;ev.pledged=true;ev.zealotsRepelled=2;
   ev.harperQueue.Add(new HarperEntry{def=IncidentDefOf.RaidEnemy,parms=new IncidentParms{target=map,points=777,faction=hostile},fireTick=Find.TickManager.TicksGame+999999});
  }
  void CheckFactionsAfterLoad(){
   var ev=Tribute.Events;var cur=Find.CurrentMap;
   foreach(var def in new[]{Factions.ColonyDef,Factions.HostileDef,Factions.EmberDef,Factions.IllithidDef,Factions.BlackNetDef,Factions.ZealotsDef})Check(Factions.Of(def)!=null,"faction survives save/load: "+def.defName);
   Check(Factions.Ember.RelationKindWith(Factions.Hostile)==FactionRelationKind.Hostile,"ember/Thay enmity survives save/load");
   Check(ev.dwarfDebts==3 && ev.tributeUntilTick==factionSavedTribute && ev.pledged && ev.zealotsRepelled==2,"blood debts, tribute expiry, pledge and zealot count survive save/load");
   Check(ev.harperQueue.Count==1 && Math.Abs(ev.harperQueue[0].parms.points-777)<1 && ev.harperQueue[0].parms.faction==Factions.Hostile && ev.harperQueue[0].parms.target==cur,"postponed harper raid survives save/load");
   var orc=Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p=>p.ThingID==decapFollowerId);var wizard=Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p=>p.ThingID==decapLeaderId);
   Check(orc!=null && wizard!=null && Decapitation.LeaderOf(orc)==wizard,"followers still remember their leader after save/load");
   ev.dwarfDebts=0;ev.tributeUntilTick=-1;ev.pledged=false;ev.zealotsRepelled=0;ev.harperQueue.Clear();
   foreach(var p in new[]{orc,wizard})if(p!=null){Find.WorldPawns.RemovePawn(p);p.Destroy();}
   Decapitation.Tracker.Prune();
   Check(Decapitation.Tracker.followers.Count==0,"decapitation fixtures are cleaned up after the save/load check");
  }
 }
}
