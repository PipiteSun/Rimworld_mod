using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.40.0 四条成神之路的步骤（需求第二节）和计数钩子。阶段编号：
 // 圣徒 0 虔信 / 1 奉献 / 2 证道 / 3 圣火降临 / 4 升神仪式；半神巫妖 0 万魂 / 1 抗拒审判 / 2 夺神（命匣旁仪式）；
 // 泰坦 0 父神之誓 / 1 巨人之战 / 2 天穹之门 / 3 等待名额；篡火者 0 沾血 / 1 事迹 / 2 升神仪式。
 public static class AscensionPaths {
  public const int SaintRite=4,LichRite=2,UsurperRite=2,TitanWaiting=3;
  public const int LichLevel=20,LichSouls=20,SaintPrayTicks=GenDate.TicksPerDay,SaintPrayDays=15,SaintShards=5,SaintWaitDays=30,UsurperShardsMax=5,UsurperBlood=3;
  public const float SaintMood=.3f,SaintValue=3000f,HereticPoints=2500f,SaintFireFavor=90f,SaintReward=20f,SaintDaily=.2f,LichKelemvor=50f,TitanFavor=30f,GiantPoints=3000f,KingShare=.3f;
  public const int SkyGateTicks=6*GenDate.TicksPerHour,WaveGap=2*GenDate.TicksPerHour;
  public static readonly float[] SparkWaveFactors={1f,1.2f,1.5f},DoomWaveFactors={.8f,1f,1.3f};
  static TraitDef saint,godslayer;static XenotypeDef awakened;static GeneDef divineBlood;
  public static TraitDef SaintTrait=>saint??(saint=DefDatabase<TraitDef>.GetNamedSilentFail("LP_Saint"));
  public static TraitDef GodslayerTrait=>godslayer??(godslayer=DefDatabase<TraitDef>.GetNamedSilentFail("LP_Godslayer"));
  public static XenotypeDef AwakenedTitan=>awakened??(awakened=DefDatabase<XenotypeDef>.GetNamedSilentFail("LP_AwakenedTitan"));
  public static GeneDef DivineBlood=>divineBlood??(divineBlood=DefDatabase<GeneDef>.GetNamedSilentFail("LP_DivineBlood"));
  public static bool IsSaint(Pawn p)=>SaintTrait!=null && p?.story?.traits?.HasTrait(SaintTrait)==true;
  public static bool IsGodslayer(Pawn p)=>GodslayerTrait!=null && p?.story?.traits?.HasTrait(GodslayerTrait)==true;
  public static bool IsShrine(Phylactery b){var t=Ascension.Tracker;return b!=null && t!=null && t.shrines.Contains(b.ThingID);}
  public static bool IsPlainTitan(Pawn p)=>p?.genes!=null && Races124.IsTitan(p) && p.genes.Xenotype!=AwakenedTitan;
  static int Now=>Find.TickManager.TicksGame;

  // ---------- 说明文字 ----------
  public static string PathSummary(string path){
   switch(path){
    case Ascension.Saint:return "神选者（守护神神眷≥60）：虔信 → 奉献 → 证道（成为圣徒）→ 圣火降临 → 在圣坛升神。";
    case Ascension.Lich:return "命匣等级≥"+LichLevel+"的巫妖：万魂 → 抗拒凯兰沃的审判 → 在命匣旁夺神。开路时凯兰沃神眷-50，成功后固定-100。";
    case Ascension.Titan:return "成年的天穹神裔：父神之誓 → 巨人之战 → 天穹之门，直接觉醒为半神（不需要火花）。";
    case Ascension.Usurper:return "黄昏之年里，殖民者击败一名神明附体的首领时自动开启：沾血 → 事迹 → 篡火仪式（有失败的风险）。";
   }
   return "";
  }
  public static string StartWarning(Pawn p,string path){
   string s="让"+p.LabelShort+"踏上"+Ascension.PathLabel(path)+"？\n\n"+PathSummary(path)+"\n\n一个人同时只能走一条路；中途死去（巫妖除外）或离开殖民地超过"+Ascension.AwayDays+"天，进度会清空。";
   if(path==Ascension.Lich)s+="\n\n代价：凯兰沃的神眷立刻-"+LichKelemvor+"，末日卫士会来摧毁命匣。";
   if(path==Ascension.Titan){var f=Father(p,false);s+=f!=null?"\n\n父神："+f.FullLabel+"，神眷+"+TitanFavor+"。":"\n\n父神会从七位可信奉的神里选出一位（不含班恩、莎尔），神眷+"+TitanFavor+"。";}
   if(path==Ascension.Saint)s+="\n\n证道和圣火降临都会引来强敌。";
   return s;
  }
  public static string StageLabel(AscensionProgress pr){
   switch(pr.path){
    case Ascension.Saint:return new[]{"试炼一：虔信","试炼二：奉献","试炼三：证道","终试：圣火降临","升神仪式"}[Mathf.Clamp(pr.stage,0,4)];
    case Ascension.Lich:return new[]{"试炼一：万魂","试炼二：抗拒审判","试炼三：夺神"}[Mathf.Clamp(pr.stage,0,2)];
    case Ascension.Titan:return new[]{"试炼一：父神之誓","试炼二：巨人之战","试炼三：天穹之门","等待名额"}[Mathf.Clamp(pr.stage,0,3)];
    case Ascension.Usurper:return new[]{"试炼一：沾血","试炼二：事迹","篡火仪式"}[Mathf.Clamp(pr.stage,0,2)];
   }
   return "";
  }
  public static string StageText(AscensionProgress pr){
   var g=pr.patronGod;
   switch(pr.path+pr.stage){
    case "Saint0":return "15天内在圣坛祈祷或冥想累计24小时，期间心情一直≥30%（掉下去重新计）。已累计 "+(pr.Get("pray")/(float)GenDate.TicksPerHour).ToString("0.#")+" / 24 小时；剩余 "+Mathf.Max(0f,SaintPrayDays-pr.DaysInStage).ToString("0.#")+" 天。";
    case "Saint1":return "在圣坛献上"+SaintShards+"片神迹残片和市场价合计≥"+SaintValue+"的物品（一次性消耗）。地图上有神迹残片 "+Faith.ShardsOn(pr.pawn.MapHeld)+" 片。";
    case "Saint2":return "迎接“异端勇者”："+g?.label+"的对立神派系，首领被神明附体，点数≥"+HereticPoints+"。"+pr.pawn.LabelShort+"必须参战并活下来，首领死亡或被俘。"+(pr.lord!=null?"\n异端勇者已经来了。":"");
    case "Saint3":return pr.Get("sparkFallen")==0?"成为圣徒满"+SaintWaitDays+"天（"+pr.DaysInStage.ToString("0.#")+" / "+SaintWaitDays+"）且"+g?.label+"神眷≥"+SaintFireFavor+"（现在 "+FaithUtility.GetFavor(g).ToString("0.#")+"）后，神性火花会坠落在圣徒身边。":"神性火花已经坠落：2天内会有3波夺火者（塞伊远征军 → 吉斯洋基 → "+g?.label+"的对立神派系）。已来 "+pr.wave+" / 3 波。";
    case "Saint4":case "Usurper2":return RiteDesc(pr);
    case "Lich0":return "开路之后用“汲取灵魂”献祭完整人形尸体："+pr.Get("souls")+" / "+LichSouls+"。";
    case "Lich1":return "凯兰沃派末日卫士来摧毁命匣：3天3波（点数×0.8 / 1.0 / 1.3），第三波的首领“凯兰沃的审判者”会冻结附近的复生。命匣没被毁就算通过。已来 "+pr.wave+" / 3 波。";
    case "Lich2":return "在命匣旁举行升神仪式：引导神性火花12小时（可打断，不消耗火花），期间会来一波吉斯洋基。"+(Ascension.SparkFor(pr.pawn)==null?"\n（地图上还没有可吸收的火花）":"");
    case "Titan0":return "父神"+g?.label+"（"+pr.portfolio?.label+"）的事迹："+pr.portfolio?.DeedText+"。进度 "+pr.Get("deed")+" / "+pr.portfolio?.DeedTarget+"。";
    case "Titan1":return "向巨人王发起挑战："+(g?.key=="Surtr"?"霜巨人":"余烬氏族的火巨人")+"，点数≥"+GiantPoints+"。"+pr.pawn.LabelShort+"本人要对巨人王造成≥30%的伤害（"+pr.kingDamage.ToString("0")+"），巨人王死亡或被俘。";
    case "Titan2":return "在室外站上6小时（可打断，打断要重来）：期间神怒风暴，3波位面生物来袭。完成后直接觉醒成半神。";
    case "Titan3":return "天穹之门已经打开，但半神名额满了："+Ascension.QuotaBlocked;
    case "Usurper0":return "击败同一位神的神明附体首领3名（不同神分开计数）："+string.Join("、",Twilight.Deities.Where(d=>pr.Get("blood_"+d.key)>0).Select(d=>d.label+" "+pr.Get("blood_"+d.key)+" / 3"))+"。黄昏之年结束时计数保留到下次。";
    case "Usurper1":return "被篡之神"+g?.label+"的事迹："+pr.portfolio?.DeedText+"。进度 "+pr.Get("deed")+" / "+pr.portfolio?.DeedTarget+"。";
   }
   return "";
  }
  public static string RiteDesc(AscensionProgress pr){
   string s="在"+(pr.path==Ascension.Lich?"命匣":"圣坛")+"旁引导神性火花12小时（可打断，打断不消耗火花），期间本地图“神性震荡”（心灵法术冷却×1.5）。";
   if(pr.path==Ascension.Usurper)s+="\n成功率 = 50% + (心灵敏感度-1)×50% + 每片神迹残片 8%（最多"+UsurperShardsMax+"片）+ 弑神者 10%，最多95%。现在（不献残片）："+UsurperChance(pr.pawn,0).ToStringPercent()+"。失败："+pr.pawn.LabelShort+"死去，火花碎成5片神迹残片。";
   else s+="\n成功率100%。";
   return s;
  }
  public static float StageProgress(AscensionProgress pr){
   switch(pr.path+pr.stage){
    case "Saint0":return pr.Get("pray")/(float)SaintPrayTicks;
    case "Saint3":return pr.Get("sparkFallen")==0?Mathf.Min(pr.DaysInStage/SaintWaitDays,FaithUtility.GetFavor(pr.patronGod)/SaintFireFavor):pr.wave/3f;
    case "Lich0":return pr.Get("souls")/(float)LichSouls;
    case "Lich1":return pr.wave/3f;
    case "Titan0":case "Usurper1":return pr.Get("deed")/(float)Mathf.Max(1,pr.portfolio?.DeedTarget??1);
    case "Usurper0":return Twilight.Deities.Select(d=>pr.Get("blood_"+d.key)).DefaultIfEmpty(0).Max()/(float)UsurperBlood;
    case "Titan1":return pr.leader==null?0f:Mathf.Clamp01(pr.kingDamage/Mathf.Max(1f,KingThreshold(pr.leader)));
   }
   return 0f;
  }
  public static string Hint(AscensionProgress pr){
   switch(pr.path+pr.stage){
    case "Saint0":return "让"+pr.pawn.LabelShort+"多在圣坛祈祷（右键圣坛）或在圣坛附近冥想，保持好心情。";
    case "Saint1":return "备好神迹残片和贵重物品，点“献上祭品”。";
    case "Saint2":return "准备好防御，再点“迎接证道”。";
    case "Saint3":return pr.Get("sparkFallen")==0?"继续侍奉"+pr.patronGod?.label+"，提高神眷。":"守住火花，打退三波夺火者。";
    case "Lich0":return "在死灵仪式台用“汲取灵魂”献祭尸体。";
    case "Lich1":return "守住命匣。";
    case "Titan0":return "按父神的喜好做事。";
    case "Titan1":return "准备好之后点“发起巨人之战”，让"+pr.pawn.LabelShort+"亲自砍巨人王。";
    case "Titan2":return "点“登上天穹之门”，让"+pr.pawn.LabelShort+"在室外站满6小时。";
    case "Titan3":return "等名额空出来（或在设置里提高名额上限）。";
    case "Usurper0":return "继续在黄昏之年里击败神明附体的首领。";
    case "Usurper1":return "完成被篡之神的事迹。";
   }
   return Ascension.RiteStage(pr)?(Ascension.RiteBlocked(pr.pawn)??"可以开始升神仪式了。"):"";
  }
  public static IEnumerable<AscensionButton> Buttons(AscensionProgress pr){
   var p=pr.pawn;
   switch(pr.path+pr.stage){
    case "Saint1":yield return new AscensionButton{label="献上祭品",disabled=FaithUtility.Altars(p.MapHeld).Any()?(Faith.ShardsOn(p.MapHeld)<SaintShards?"地图上的神迹残片不足"+SaintShards+"片":null):"需要一座圣坛",action=()=>Find.WindowStack.Add(new Dialog_SaintOffering(pr))};break;
    case "Saint2":yield return new AscensionButton{label="迎接证道",disabled=pr.lord!=null?"异端勇者已经来了":p.MapHeld==null || !p.MapHeld.IsPlayerHome?"要在殖民地里":null,action=()=>Heretics(pr)};break;
    case "Titan1":yield return new AscensionButton{label="发起巨人之战",disabled=pr.lord!=null?"巨人们已经来了":p.MapHeld==null || !p.MapHeld.IsPlayerHome?"要在殖民地里":null,action=()=>GiantBattle(pr)};break;
    case "Titan2":yield return new AscensionButton{label="登上天穹之门",disabled=SkyGateBlocked(p,true),action=()=>BeginSkyGate(p)};break;
   }
   if(Ascension.RiteStage(pr)){
    string why=Ascension.RiteBlocked(p);
    yield return new AscensionButton{label="开始升神仪式",disabled=why,action=()=>{if(pr.path==Ascension.Usurper)Find.WindowStack.Add(new Dialog_UsurperShards(p));else if(!Ascension.BeginRite(p))Messages.Message("到不了仪式的位置。",MessageTypeDefOf.RejectInput,false);}};
   }
   yield return new AscensionButton{label="放弃这条路",action=()=>Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("放弃"+Ascension.PathLabel(pr.path)+"？进度会全部清空（已经付出的代价不返还）。",()=>Clear(pr,p.LabelShort+"放弃了"+Ascension.PathLabel(pr.path)+"。"),true))};
  }

  // ---------- 开路 ----------
  // 父神：七位可信奉的神（不含班恩、莎尔）。1.41.0：同一个泰坦只认一次父神，存在追踪组件里（剧本“坠落的神裔”开局就定下）。
  public static IEnumerable<LP_DeityDef> FatherPool=>FaithUtility.Worshippable.Where(d=>d.key!="Bane" && d.key!="Shar");
  public static LP_DeityDef Father(Pawn p,bool assign){
   var t=Ascension.Tracker;var g=t?.FatherOf(p);if(g!=null || !assign)return g;
   g=FatherPool.RandomElementWithFallback();if(g!=null && t!=null && p!=null)t.fathers[p.ThingID]=g;
   return g;
  }
  public static void OnStart(AscensionProgress pr,bool letter=true){
   var p=pr.pawn;
   switch(pr.path){
    case Ascension.Saint:FaithUtility.IsChosen(p,out var g);pr.patronGod=g;pr.portfolio=Ascension.PortfolioOf(g);break;
    case Ascension.Lich:{pr.box=Necro.Lich(p)?.box;pr.portfolio=Ascension.Portfolio("Undeath");var k=Faith.God("Kelemvor");if(k!=null)FaithUtility.AddFavor(k,-LichKelemvor,p.LabelShort+"踏上半神巫妖之路");break;}
    case Ascension.Titan:{
     var father=Father(p,true);
     pr.patronGod=father;pr.portfolio=Ascension.PortfolioOf(father);
     if(father!=null)FaithUtility.AddFavor(father,TitanFavor,p.LabelShort+"认父神");
     break;}
   }
   if(!letter)return;
   Ascension.Letter(Ascension.PathLabel(pr.path)+"："+p.LabelShort,p.LabelShort+"踏上了"+Ascension.PathLabel(pr.path)+"。"+(pr.path==Ascension.Titan?"\n\n父神："+pr.patronGod?.FullLabel+"（神职："+pr.portfolio?.label+"）。":"")+"\n\n"+StageLabel(pr)+"："+StageText(pr),LetterDefOf.PositiveEvent,p);
  }
  public static void Clear(AscensionProgress pr,string why){
   var t=Ascension.Tracker;if(t==null)return;pr.done=true;t.progress.Remove(pr);
   if(why!=null)Ascension.Letter("成神之路中断",why,LetterDefOf.NegativeEvent,pr.pawn!=null && !pr.pawn.Destroyed?new LookTargets(pr.pawn):null);
  }
  static void Advance(AscensionProgress pr,string done){
   pr.NextStage();
   Ascension.Letter(Ascension.PathLabel(pr.path)+"：试炼完成",pr.pawn.LabelShort+done+"\n\n下一步 · "+StageLabel(pr)+"："+StageText(pr),LetterDefOf.PositiveEvent,pr.pawn);
  }

  // ---------- 每小时 ----------
  public static void Hourly(LP_AscensionTracker t){
   foreach(var pr in t.progress.Where(x=>!x.done).ToList()){
    var p=pr.pawn;if(p==null || p.Destroyed && !p.Dead){Clear(pr,null);continue;}
    if(p.Dead){if(pr.path==Ascension.Lich && Necro.Lich(p)?.box!=null && !Necro.Lich(p).box.Destroyed)continue;Clear(pr,p.LabelShort+"死了，"+Ascension.PathLabel(pr.path)+"的进度随之消散。");continue;}
    bool away=p.Faction==null || !p.Faction.IsPlayer || p.IsPrisoner || (!p.Spawned && p.GetCaravan()==null && !p.IsWorldPawn() && p.ParentHolder==null);
    if(p.Faction!=null && p.Faction.IsPlayer && p.IsWorldPawn() && p.GetCaravan()==null)away=true;
    if(away){pr.awayTicks+=GenDate.TicksPerHour;if(pr.awayTicks>Ascension.AwayDays*GenDate.TicksPerDay){Clear(pr,p.LabelShort+"离开殖民地超过"+Ascension.AwayDays+"天，"+Ascension.PathLabel(pr.path)+"的进度消散了。");continue;}}
    else pr.awayTicks=0;
    if(!Ascension.Enabled)continue;
    try{
     switch(pr.path){
      case Ascension.Saint:SaintHourly(pr);break;
      case Ascension.Lich:LichHourly(pr);break;
      case Ascension.Titan:TitanHourly(pr);break;
     }
    }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 成神路线结算出错（"+pr.path+"）："+e,77310030+pr.stage);}
   }
  }
  public static void Daily(LP_AscensionTracker t){
   // 圣徒：神选者的神每天 +0.2。
   foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Where(IsSaint).ToList())if(FaithUtility.IsChosen(p,out var g) && g!=null)FaithUtility.AddFavor(g,SaintDaily,"圣徒"+p.LabelShort);
  }

  // ==================== A 圣徒 ====================
  static void SaintHourly(AscensionProgress pr){
   var p=pr.pawn;
   if(pr.stage==0){
    if(p.needs?.mood!=null && p.needs.mood.CurLevelPercentage<SaintMood && pr.Get("pray")>0){pr.Set("pray",0);Messages.Message(p.LabelShort+"的心情掉到了30%以下，虔信的累计重新开始。",p,MessageTypeDefOf.NegativeEvent,false);}
    // 圣坛附近冥想也算（每小时抽查一次）。
    if(p.Spawned && p.CurJobDef==JobDefOf.Meditate && FaithUtility.Altars(p.Map).Any(a=>a.Position.InHorDistOf(p.Position,10f)))pr.Add("pray",GenDate.TicksPerHour);
    if(pr.Get("pray")>=SaintPrayTicks){Advance(pr,"在圣坛前累计虔信了24小时。");return;}
    if(pr.DaysInStage>SaintPrayDays){pr.Set("pray",0);pr.stageStartTick=Now;Messages.Message(p.LabelShort+"没能在15天内完成虔信，重新开始计时。",p,MessageTypeDefOf.NegativeEvent,false);}
   }else if(pr.stage==2 && pr.lord!=null){
    bool beaten=pr.leader==null || pr.leader.Dead || pr.leader.IsPrisonerOfColony;
    if(beaten && p.MapHeld==pr.lordMap && !p.Dead){SaintCrowned(pr);return;}
    if(Ascension.LordOver(pr.lord,pr.lordMap)){pr.lord=null;pr.leader=null;Messages.Message("异端勇者的首领没有被击杀或俘虏：证道没有完成，可以再试一次。",p,MessageTypeDefOf.NegativeEvent,false);}
   }else if(pr.stage==3){
    if(pr.Get("sparkFallen")==0){
     if(pr.DaysInStage>=SaintWaitDays && FaithUtility.GetFavor(pr.patronGod)>=SaintFireFavor && p.Spawned && p.Map.IsPlayerHome)SparkFalls(pr);
     return;
    }
    if(pr.wave<3 && Now>=pr.nextWaveTick){SparkWave(pr);return;}
    if(pr.wave>=3 && Ascension.LordOver(pr.lord,pr.lordMap)){Advance(pr,"守住了神性火花，三波夺火者都被打退了。");}
   }
  }
  public static void SaintCrowned(AscensionProgress pr){
   var p=pr.pawn;var g=pr.patronGod;
   if(SaintTrait!=null && !IsSaint(p))p.story.traits.GainTrait(new Trait(SaintTrait,0,true));
   var seal=Items.Thing("LP_SaintSeal");if(seal!=null){var s=ThingMaker.MakeThing(seal);s.TryGetComp<CompBiocodable>()?.CodeFor(p);if(p.apparel!=null && ApparelUtility.HasPartsToWear(p,seal))p.apparel.Wear((Apparel)s,false);else GenPlace.TryPlaceThing(s,p.PositionHeld,p.MapHeld,ThingPlaceMode.Near);}
   if(g!=null)FaithUtility.AddFavor(g,SaintReward,p.LabelShort+"证道成圣");
   pr.Set("saintTick",Now);
   Advance(pr,"打败了异端勇者，成为"+g?.label+"的圣徒：心灵敏感度+15%、社交+2，"+g?.label+"神眷+"+SaintReward+"、之后每天+"+SaintDaily+"；得到圣徒圣印（已为"+p.LabelShort+"绑定）。");
  }
  public static void Heretics(AscensionProgress pr){
   var p=pr.pawn;var map=p.MapHeld;if(map==null)return;
   var f=Ascension.FactionOfGods(pr.patronGod?.Enemies??Enumerable.Empty<LP_DeityDef>(),out var god);if(f==null)return;
   var pawns=Ascension.RaidPawns(f,map,Ascension.Points(map,HereticPoints));if(pawns.Count==0)return;
   var leader=Ascension.Leader(pawns);var vg=Ascension.VesselGod(god,f,leader);if(leader!=null && vg!=null)Twilight.MakeVessel(leader,vg);
   pr.lord=Ascension.Arrive(map,f,pawns);pr.lordMap=map;pr.leader=leader;
   Ascension.Letter("证道：异端勇者",f.Name+"的异端勇者来挑战"+pr.patronGod?.label+"的神选者"+p.LabelShort+"。"+(leader!=null?"领队"+leader.LabelShort+"被"+vg?.label+"附体。":"")+"\n\n这是成神的试炼："+p.LabelShort+"要参战并活下来，首领要死或被俘。",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f);
  }
  public static void SaintOffer(AscensionProgress pr,List<Thing> items){
   var map=pr.pawn.MapHeld;
   if(!Faith.TakeShards(map,SaintShards))return;
   foreach(var t in items)if(!t.Destroyed)t.Destroy();
   Advance(pr,"在圣坛献上了"+SaintShards+"片神迹残片和"+items.Count+"件祭品。");
  }
  public static void SparkFalls(AscensionProgress pr){
   var p=pr.pawn;var spark=Ascension.MakeSpark(p,pr.portfolio,Ascension.Saint);if(spark==null)return;
   DropPodUtility.DropThingsNear(p.Position,p.Map,new List<Thing>{spark},110,false,false,true,false,true,null);
   pr.Set("sparkFallen",1);pr.wave=0;pr.nextWaveTick=Now+WaveGap;
   Ascension.Letter("神性火花坠落",pr.patronGod?.FullLabel+"把一粒神性火花投向了"+p.LabelShort+"：它落在圣徒身边。\n\n凡人和异神都想要它。2天内会有3波夺火者：塞伊远征军 → 吉斯洋基 → "+pr.patronGod?.label+"的对立神派系。打退他们之前不能举行升神仪式。",LetterDefOf.ThreatBig,p);
  }
  static void SparkWave(AscensionProgress pr){
   var p=pr.pawn;var map=p.MapHeld;if(map==null || !map.IsPlayerHome){pr.nextWaveTick=Now+GenDate.TicksPerHour*3;return;}
   int i=pr.wave;float pts=Ascension.Points(map,1000f,SparkWaveFactors[i]);Faction f;List<Pawn> pawns;
   if(i==0){f=Factions.Hostile??Ascension.FallbackEnemy();pawns=f==null?new List<Pawn>():Ascension.RaidPawns(f,map,pts);}
   else if(i==1){f=Factions.Hostile??Ascension.FallbackEnemy();pawns=f==null?new List<Pawn>():Ascension.KindPawns(DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FH_GithMercenary"),f,map,pts,2);}
   else{f=Ascension.FactionOfGods(pr.patronGod?.Enemies??Enumerable.Empty<LP_DeityDef>(),out _);pawns=f==null?new List<Pawn>():Ascension.RaidPawns(f,map,pts);}
   pr.wave++;pr.nextWaveTick=Now+16*GenDate.TicksPerHour;
   if(f==null || pawns.Count==0)return;
   var spark=Ascension.AllSparks(map).FirstOrDefault(s=>s.TryGetComp<CompDivineSpark>()?.owner==p);
   pr.lord=Ascension.Arrive(map,f,pawns,spark!=null && spark.Spawned?spark:null);pr.lordMap=map;
   Ascension.Letter("夺火者（第"+pr.wave+"波）",f.Name+"来抢神性火花了！",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f);
  }

  // ==================== B 半神巫妖 ====================
  public static void SoulOffered(Pawn lich){
   var pr=Ascension.Tracker?.Of(lich);if(pr==null || pr.path!=Ascension.Lich || pr.stage!=0)return;
   pr.Add("souls");
   if(pr.Get("souls")>=LichSouls)Advance(pr,"汲取了"+LichSouls+"个灵魂。凯兰沃注意到了：他的末日卫士要来摧毁命匣。");
  }
  static void LichHourly(AscensionProgress pr){
   var box=Necro.Lich(pr.pawn)?.box;
   if(box==null || box.Destroyed){Clear(pr,"命匣被毁了："+pr.pawn.LabelShort+"的半神巫妖之路断绝。");return;}
   pr.box=box;
   if(pr.stage!=1)return;
   if(pr.wave==0 && pr.nextWaveTick<0)pr.nextWaveTick=pr.stageStartTick+WaveGap;
   if(pr.wave<3 && Now>=pr.nextWaveTick){DoomWave(pr,box);return;}
   if(pr.wave>=3 && Ascension.LordOver(pr.lord,pr.lordMap)){
    if(pr.Get("spark")==0){var s=Ascension.MakeSpark(pr.pawn,pr.portfolio,Ascension.Lich);if(s!=null && box.MapHeld!=null){GenPlace.TryPlaceThing(s,box.PositionHeld,box.MapHeld,ThingPlaceMode.Near);pr.Set("spark",1);}}
    Advance(pr,"挡住了凯兰沃的三波审判，命匣完好无损。审判者留下的神性火花在命匣旁闪着光。");
   }
  }
  static void DoomWave(AscensionProgress pr,Phylactery box){
   var map=box.MapHeld;if(map==null || !map.IsPlayerHome || !box.Spawned){pr.nextWaveTick=Now+GenDate.TicksPerHour*3;return;}
   var fd=DefDatabase<FactionDef>.GetNamedSilentFail("LP_KelemvorChurch");var f=fd==null?null:Factions.EnsureHidden(fd);if(f==null){pr.wave=3;return;}
   int i=pr.wave;var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_Doomguide");
   var pawns=Ascension.KindPawns(kind,f,map,Ascension.Points(map,600f,DoomWaveFactors[i]),2);
   if(i==2){var judge=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_DoomJudge");if(judge!=null){var j=PawnGenerator.GeneratePawn(new PawnGenerationRequest(judge,f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true));Items.Give(j,"LP_FreezeRevivalAura");AdventurerEnemies.Story(j,16);pawns.Insert(0,j);pr.leader=j;}}
   pr.wave++;pr.nextWaveTick=Now+GenDate.TicksPerDay;
   pr.lord=Ascension.Arrive(map,f,pawns,box);pr.lordMap=map;
   Ascension.Letter("抗拒审判（第"+pr.wave+"波）","凯兰沃的末日卫士来摧毁"+pr.pawn.LabelShort+"的命匣！"+(i==2?"\n\n领头的是“凯兰沃的审判者”：他身边15格内，巫妖和巨魔的复生计时会暂停。":""),LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f);
  }
  public static void LichRiteRaid(AscensionProgress pr){
   var box=Necro.Lich(pr.pawn)?.box;var map=pr.pawn.MapHeld;if(map==null)return;
   var f=Factions.Hostile??Ascension.FallbackEnemy();if(f==null)return;
   var pawns=Ascension.KindPawns(DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FH_GithMercenary"),f,map,Ascension.Points(map,1000f),2);if(pawns.Count==0)return;
   Ascension.Arrive(map,f,pawns,box!=null && box.Spawned?box:null);
   Ascension.Letter("夺神：吉斯洋基","巫妖女王维拉基斯不容许另一个巫妖登上神位：吉斯洋基从星界跳了出来，要打断"+pr.pawn.LabelShort+"的仪式！",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f);
  }
  // 神龛：复生 3 天 → 1 天；神力 ≥ 50 时扣 50 立即复生。
  public const float ShrineRevivalCost=50f;public const int ShrineRevivalDelay=GenDate.TicksPerDay;
  public static void ShrineScheduled(Phylactery box){
   if(!IsShrine(box) || box.revivalTick<0)return;
   int now=Now;box.revivalTick=Math.Min(box.revivalTick,now+ShrineRevivalDelay);
   var h=Demigods.Of(box.soul);if(h!=null && h.Power+.001f>=ShrineRevivalCost){h.Power-=ShrineRevivalCost;box.revivalTick=now;}
  }

  // ==================== C 泰坦 ====================
  public static float KingThreshold(Pawn king)=>KingShare*Mathf.Max(1f,king?.health?.LethalDamageThreshold??100f);
  public static void GiantBattle(AscensionProgress pr){
   var p=pr.pawn;var map=p.MapHeld;if(map==null)return;
   bool frost=pr.patronGod?.key=="Surtr";
   var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail(frost?"LP_FH_FrostGiantRaider":"LP_EC_FireGiant");
   Faction f=frost?Factions.Hostile:Factions.Ember;if(f==null || f.defeated || !f.HostileTo(Faction.OfPlayer))f=Factions.Hostile??Ascension.FallbackEnemy();
   if(kind==null || f==null)return;
   var pawns=Ascension.KindPawns(kind,f,map,Ascension.Points(map,GiantPoints),2);
   var king=pawns[0];Items.Give(king,"LP_GiantKing");if(king.Name is NameTriple nt)king.Name=new NameTriple(nt.First,"巨人王",nt.Last);
   pr.leader=king;pr.kingDamage=0f;pr.lord=Ascension.Arrive(map,f,pawns);pr.lordMap=map;
   Ascension.Letter("巨人之战",(frost?"霜巨人":"火巨人")+"的巨人王带着氏族来应战了。\n\n"+p.LabelShort+"要亲手对巨人王造成至少30%的伤害，巨人王死亡或被俘才算赢。",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f);
  }
  static void TitanHourly(AscensionProgress pr){
   if(pr.stage==1 && pr.leader!=null){
    var king=pr.leader;bool beaten=king.Dead || king.IsPrisonerOfColony;
    if(beaten){
     if(pr.kingDamage>=KingThreshold(king)){Advance(pr,"在巨人之战中亲手击败了巨人王。");return;}
     pr.lord=null;pr.leader=null;Messages.Message(pr.pawn.LabelShort+"对巨人王造成的伤害不够30%：巨人之战不算数，可以再挑战一次。",pr.pawn,MessageTypeDefOf.NegativeEvent,false);return;
    }
    if(Ascension.LordOver(pr.lord,pr.lordMap) && !king.Spawned){pr.lord=null;pr.leader=null;Messages.Message("巨人王逃走了：巨人之战不算数。",pr.pawn,MessageTypeDefOf.NegativeEvent,false);}
   }else if(pr.stage==TitanWaiting && Ascension.QuotaBlocked==null)TitanAscend(pr);
  }
  public static string SkyGateBlocked(Pawn p,bool starting=false){
   var pr=Ascension.Tracker?.Of(p);if(pr==null || pr.path!=Ascension.Titan || pr.stage!=2)return "不在天穹之门这一步。";
   if(!p.Spawned || p.Map==null)return "要在地图上。";
   if(!starting && p.Position.Roofed(p.Map))return "要站在室外（没有屋顶）。";
   if(starting && !p.Map.IsPlayerHome)return "要在殖民地里。";
   return null;
  }
  public static void BeginSkyGate(Pawn p){
   if(Ascension.SkyGateJob==null)return;
   if(!CellFinder.TryFindRandomCellNear(p.Position,p.Map,12,c=>c.Standable(p.Map) && !c.Roofed(p.Map) && c.GetFirstPawn(p.Map)==null && p.CanReach(c,PathEndMode.OnCell,Danger.Deadly),out var cell)){Messages.Message("附近找不到露天的地方。",MessageTypeDefOf.RejectInput,false);return;}
   p.jobs.TryTakeOrderedJob(JobMaker.MakeJob(Ascension.SkyGateJob,cell),JobTag.Misc);
  }
  public static void SkyGateStarted(Pawn p){
   var pr=Ascension.Tracker?.Of(p);if(pr==null)return;
   pr.Set("gateStart",Now);pr.wave=0;pr.Set("plane",Rand.Range(0,Mathf.Max(1,DefDatabase<PortalPlaneDef>.DefCount)));
   if(Ascension.WrathDef!=null && !p.Map.gameConditionManager.ConditionIsActive(Ascension.WrathDef))p.Map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(Ascension.WrathDef,SkyGateTicks+GenDate.TicksPerHour));
   Ascension.Letter("天穹之门",p.LabelShort+"仰望天穹，等待门开。神怒风暴降临，位面裂隙里的生物会冲向"+p.LabelShort+"。\n\n站满6小时（不能进屋）就能觉醒。",LetterDefOf.ThreatBig,p);
   SkyGateTick(p);
  }
  public static void SkyGateTick(Pawn p){
   var pr=Ascension.Tracker?.Of(p);if(pr==null || pr.wave>=3)return;
   if(Now<pr.Get("gateStart")+pr.wave*WaveGap)return;
   var defs=DefDatabase<PortalPlaneDef>.AllDefsListForReading;if(defs.Count==0){pr.wave=3;return;}
   var plane=defs[Mathf.Clamp(pr.Get("plane"),0,defs.Count-1)];pr.wave++;
   var map=p.Map;var made=new List<Pawn>();
   if(!RCellFinder.TryFindRandomPawnEntryCell(out var edge,map,CellFinder.EdgeRoadChance_Hostile))edge=CellFinder.RandomEdgeCell(map);
   foreach(var s in plane.spawns.Where(s=>s.kind!=null && s.fromWave<=pr.wave && s.side!="thay")){
    int n=Mathf.Max(1,Mathf.RoundToInt(s.count.RandomInRange*Ascension.Difficulty));if(s.maxTotal>=0)n=Math.Min(n,s.maxTotal);
    for(int i=0;i<n;i++){
     Pawn c;try{c=PawnGenerator.GeneratePawn(new PawnGenerationRequest(s.kind,null,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false));}catch(Exception e){Log.Warning("[DND边缘大陆] 天穹之门生成 "+s.kind.defName+" 失败："+e.Message);continue;}
     GenSpawn.Spawn(c,CellFinder.RandomClosewalkCellNear(edge,map,6),map);
     c.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true,false,false,null,true);
     made.Add(c);
    }
   }
   if(made.Count>0)Messages.Message("天穹之门：第"+pr.wave+"波位面生物（"+plane.label+"）冲了过来！",new LookTargets(made.Cast<Thing>()),MessageTypeDefOf.ThreatBig,false);
  }
  public static void SkyGateEnded(Pawn p,bool done){
   var map=p?.MapHeld;if(map!=null && Ascension.WrathDef!=null)foreach(var c in map.gameConditionManager.ActiveConditions.Where(c=>c.def==Ascension.WrathDef).ToList())c.End();
   if(!done){var pr=Ascension.Tracker?.Of(p);if(pr!=null && pr.stage==2){pr.Set("gateStart",0);pr.wave=0;}}
  }
  public static void SkyGateDone(Pawn p){
   var pr=Ascension.Tracker?.Of(p);if(pr==null || pr.stage!=2)return;
   pr.NextStage();
   if(Ascension.QuotaBlocked==null)TitanAscend(pr);
   else Ascension.Letter("天穹之门已开",p.LabelShort+"撑过了天穹之门，但世上的半神名额已满。等名额空出来时会自动觉醒。",LetterDefOf.NeutralEvent,p);
  }
  public static void TitanAscend(AscensionProgress pr){
   var p=pr.pawn;var t=Ascension.Tracker;
   Awaken(p);
   Demigods.Grant(p,pr.portfolio,Ascension.Titan);
   GiveSpear(p,pr.portfolio);
   pr.done=true;t?.progress.Remove(pr);
  }
  public static bool Awaken(Pawn p){
   if(p?.genes==null || AwakenedTitan==null)return false;
   p.genes.SetXenotypeDirect(AwakenedTitan);
   if(DivineBlood!=null && !p.genes.GenesListForReading.Any(g=>g.def==DivineBlood))p.genes.AddGene(DivineBlood,true);
   p.Drawer?.renderer?.SetAllGraphicsDirty();
   return true;
  }
  public static Thing GiveSpear(Pawn p,LP_DivinePortfolioDef pf){
   var def=Items.Thing("LP_SkySpear");if(def==null || p==null)return null;
   var w=(ThingWithComps)ThingMaker.MakeThing(def);w.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Colony);
   var c=w.TryGetComp<CompSkySpear>();if(c!=null)c.portfolio=pf;w.TryGetComp<CompBiocodable>()?.CodeFor(p);
   if(p.equipment!=null && !p.WorkTagIsDisabled(WorkTags.Violent) && p.Spawned){if(p.equipment.Primary!=null)p.equipment.TryDropEquipment(p.equipment.Primary,out _,p.Position,false);p.equipment.AddEquipment(w);}
   else if(p.MapHeld!=null)GenPlace.TryPlaceThing(w,p.PositionHeld,p.MapHeld,ThingPlaceMode.Near);
   return w;
  }

  // ==================== E 篡火者 ====================
  static readonly Dictionary<int,Pawn> lastHit=new Dictionary<int,Pawn>();
  public static void VesselDefeated(Pawn vessel){
   var god=Twilight.Infused(vessel);int id=vessel?.thingIDNumber??-1;
   lastHit.TryGetValue(id,out var killer);lastHit.Remove(id);
   if(god==null || killer==null || !Ascension.Enabled || !Twilight.Active || !Ascension.Colonist(killer) || Demigods.Is(killer))return;
   var t=Ascension.Tracker;if(t==null)return;var pr=t.Of(killer);
   if(pr==null){pr=new AscensionProgress{pawn=killer,path=Ascension.Usurper,stage=0,stageStartTick=Now};t.progress.Add(pr);Ascension.Letter("篡火者之路："+killer.LabelShort,killer.LabelShort+"击败了"+god.label+"附体的首领，尝到了神血的味道。\n\n"+PathSummary(Ascension.Usurper),LetterDefOf.PositiveEvent,killer);}
   if(pr.path!=Ascension.Usurper || pr.stage!=0)return;
   pr.Add("blood_"+god.key);
   if(GodslayerTrait!=null && !IsGodslayer(killer)){killer.story.traits.GainTrait(new Trait(GodslayerTrait,0,true));Messages.Message(killer.LabelShort+"成为了弑神者：对神力灌注者、神明附体者和半神的伤害×1.3。",killer,MessageTypeDefOf.PositiveEvent,false);}
   if(pr.Get("blood_"+god.key)>=UsurperBlood){
    pr.patronGod=god;pr.portfolio=Ascension.PortfolioOf(god);
    var spark=Ascension.MakeSpark(killer,pr.portfolio,Ascension.Usurper,true);
    var map=vessel.MapHeld??killer.MapHeld;var cell=vessel.PositionHeld.IsValid?vessel.PositionHeld:killer.PositionHeld;if(spark!=null && map!=null)GenPlace.TryPlaceThing(spark,cell,map,ThingPlaceMode.Near);
    Advance(pr,"击败了第三名"+god.label+"附体的首领：一粒篡夺的神性火花从他身上掉了出来（任何殖民者都能吸收）。");
   }
  }
  public static float UsurperChance(Pawn p,int shards){
   float s=p?.GetStatValue(StatDefOf.PsychicSensitivity)??1f;
   return Mathf.Clamp(.5f+(s-1f)*.5f+.08f*Mathf.Clamp(shards,0,UsurperShardsMax)+(IsGodslayer(p)?.1f:0f),0f,.95f);
  }
  public static void UsurperFailed(Pawn p,Thing spark){
   var map=p.MapHeld;var cell=p.PositionHeld;
   if(spark!=null && !spark.Destroyed){var sc=spark.PositionHeld;spark.Destroy();if(map!=null)cell=sc.IsValid?sc:cell;}
   if(map!=null && Faith.ShardDef!=null){var s=ThingMaker.MakeThing(Faith.ShardDef);s.stackCount=5;GenPlace.TryPlaceThing(s,cell,map,ThingPlaceMode.Near);}
   var pr=Ascension.Tracker?.Of(p);if(pr!=null){pr.done=true;Ascension.Tracker.progress.Remove(pr);}
   if(!p.Dead)p.Kill(null);
   Ascension.Letter("篡火失败",p.LabelShort+"没能承受住篡来的神性：神火从内部烧尽了他。\n\n火花碎成了5片神迹残片。",LetterDefOf.Death,new LookTargets(cell,map));
  }

  // ---------- 事迹（泰坦试炼一、篡火者试炼二） ----------
  public static void Deed(Pawn p,string key){
   var pr=Ascension.Tracker?.Of(p);if(pr==null || pr.portfolio==null)return;
   bool deedStage=(pr.path==Ascension.Titan && pr.stage==0) || (pr.path==Ascension.Usurper && pr.stage==1);
   if(!deedStage || pr.portfolio.DeedKey!=key)return;
   pr.Add("deed");
   if(pr.Get("deed")>=pr.portfolio.DeedTarget)Advance(pr,"完成了"+pr.patronGod?.label+"的事迹（"+pr.portfolio.DeedText+"）。");
  }

  // ---------- 奖励 ----------
  public static void Rewards(AscensionProgress pr){
   var p=pr.pawn;var t=Ascension.Tracker;if(t==null)return;
   switch(pr.path){
    case Ascension.Lich:{
     var k=Faith.God("Kelemvor");if(k!=null && !t.kelemvorFixed.Contains(k)){t.kelemvorFixed.Add(k);Demigods.FixFavor(k);}
     var box=Necro.Lich(p)?.box;if(box!=null && !t.shrines.Contains(box.ThingID)){t.shrines.Add(box.ThingID);box.HitPoints=Math.Min(box.MaxHitPoints,box.HitPoints*2);}
     t.githBetrayed=true;
     GiveBound(p,"LP_EternalNightCrown");
     Ascension.Letter("半神巫妖",p.LabelShort+"夺取了神位。命匣化为神龛：复生只要1天，神力≥50时可以立刻复生（扣50神力），命匣生命×2。\n\n得到永夜冠冕。凯兰沃的神眷从此固定为-100；吉斯洋基不再把"+p.LabelShort+"当作巫妖女王的同类。",LetterDefOf.PositiveEvent,p);
     break;}
    case Ascension.Usurper:{
     var x=pr.patronGod;if(x!=null && !t.usurped.Contains(x)){t.usurped.Add(x);Demigods.FixFavor(x);}
     var w=GiveBound(p,"LP_Godsbane");
     Ascension.Letter("篡火者",p.LabelShort+"篡夺了"+x?.label+"的神火。"+x?.label+"的神眷从此固定为-100，信奉祂的派系袭击更频繁（×1.5）。\n\n得到诸神之祸。",LetterDefOf.PositiveEvent,p);
     break;}
   }
  }
  public static Thing GiveBound(Pawn p,string defName){
   var def=Items.Thing(defName);if(def==null || p==null)return null;
   var t=ThingMaker.MakeThing(def,def.MadeFromStuff?GenStuff.DefaultStuffFor(def):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Colony);t.TryGetComp<CompBiocodable>()?.CodeFor(p);
   if(t is Apparel a && p.apparel!=null && p.Spawned && ApparelUtility.HasPartsToWear(p,def) && p.apparel.WornApparel.All(w=>ApparelUtility.CanWearTogether(w.def,def,p.RaceProps.body))){p.apparel.Wear(a,false);return t;}
   if(t is ThingWithComps w2 && def.IsWeapon && p.equipment!=null && p.Spawned && !p.WorkTagIsDisabled(WorkTags.Violent)){if(p.equipment.Primary!=null)p.equipment.TryDropEquipment(p.equipment.Primary,out _,p.Position,false);p.equipment.AddEquipment(w2);return t;}
   if(p.MapHeld!=null)GenPlace.TryPlaceThing(t,p.PositionHeld,p.MapHeld,ThingPlaceMode.Near);
   return t;
  }

  // ---------- 伤害记录（篡火者、巨人王） ----------
  public static void OnDamage(Pawn victim,Pawn attacker,DamageInfo dinfo,float dealt){
   if(attacker.Faction?.IsPlayer==true && Twilight.Vessel(victim)!=null)lastHit[victim.thingIDNumber]=attacker;
   var t=Ascension.Tracker;if(t==null || t.progress.Count==0)return;
   foreach(var pr in t.progress)if(pr.path==Ascension.Titan && pr.stage==1 && pr.leader==victim && pr.pawn==attacker)pr.kingDamage+=dealt;
  }
 }

 // ---------- 圣徒的奉献窗口 ----------
 public class Dialog_SaintOffering : Window {
  readonly AscensionProgress pr;readonly List<Thing> items;readonly HashSet<Thing> chosen=new HashSet<Thing>();Vector2 scroll;
  public override Vector2 InitialSize=>new Vector2(560f,620f);
  public Dialog_SaintOffering(AscensionProgress pr){
   this.pr=pr;doCloseX=true;forcePause=true;absorbInputAroundWindow=true;var map=pr.pawn.MapHeld;
   items=map==null?new List<Thing>():map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Where(t=>t.Spawned && !t.IsForbidden(Faction.OfPlayer) && t.def!=Faith.ShardDef && t.TryGetComp<CompDivineSpark>()==null && t.MarketValue>0f && map.areaManager.Home[t.Position] && !(t is Corpse)).OrderByDescending(t=>t.MarketValue*t.stackCount).Take(200).ToList();
  }
  float Total=>chosen.Sum(t=>t.MarketValue*t.stackCount);
  public override void DoWindowContents(Rect r){
   var top=new Rect(r.x,r.y,r.width,60f);Widgets.Label(top,"选择献给"+pr.patronGod?.label+"的祭品（整堆献上），合计市场价要≥"+AscensionPaths.SaintValue+"；另外消耗"+AscensionPaths.SaintShards+"片神迹残片。\n已选："+Total.ToString("0")+" / "+AscensionPaths.SaintValue);
   var outRect=new Rect(r.x,r.y+64f,r.width,r.height-110f);var view=new Rect(0,0,outRect.width-20f,items.Count*26f);
   Widgets.BeginScrollView(outRect,ref scroll,view);
   for(int i=0;i<items.Count;i++){var t=items[i];bool on=chosen.Contains(t);var row=new Rect(0,i*26f,view.width,24f);bool was=on;Widgets.CheckboxLabeled(row,t.LabelCap+" · "+(t.MarketValue*t.stackCount).ToString("0"),ref on);if(on!=was){if(on)chosen.Add(t);else chosen.Remove(t);}}
   Widgets.EndScrollView();
   var btn=new Rect(r.x,r.yMax-38f,r.width,36f);
   bool ok=Total>=AscensionPaths.SaintValue && Faith.ShardsOn(pr.pawn.MapHeld)>=AscensionPaths.SaintShards;
   if(Widgets.ButtonText(btn,ok?"献上":"还不够",true,true,ok)){if(ok){AscensionPaths.SaintOffer(pr,chosen.ToList());Close();}}
  }
 }

 // ---------- 天穹之矛：附带父神神职伤害（不乘心灵敏感度） ----------
 public class CompProperties_SkySpear : CompProperties {public CompProperties_SkySpear(){compClass=typeof(CompSkySpear);}}
 public class CompSkySpear : ThingComp {
  public LP_DivinePortfolioDef portfolio;public const float Extra=8f;
  public override string CompInspectStringExtra()=>"父神神职："+(portfolio?.label??"未定");
  public void Strike(Pawn a,Pawn t){
   switch(portfolio?.key){
    case "Storm":t.TakeDamage(new DamageInfo(DemigodAbilities.Electric,Extra,0f,-1f,a));if(!t.Dead)t.stances?.stunner.StunFor(30,a,false,true);break;
    case "Wild":t.TakeDamage(new DamageInfo(DamageDefOf.Cut,Extra,0f,-1f,a));break;
    case "Fire":t.TakeDamage(new DamageInfo(DamageDefOf.Burn,Extra,0f,-1f,a));if(!t.Dead && Rand.Chance(.15f))t.TryAttachFire(.3f,a);break;
    default:t.TakeDamage(new DamageInfo(Faith.Radiant,Extra,0f,-1f,a));break;
   }
  }
  public override void PostExposeData(){base.PostExposeData();Scribe_Defs.Look(ref portfolio,"lpPortfolio");}
 }

 // ---------- 凯兰沃的审判者：冻结复生光环 ----------
 public class Hediff_FreezeRevival : HediffWithComps {
  public const float Radius=15f;int acc;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<60)return;int e=acc;acc=0;
   if(pawn==null || pawn.Dead || pawn.Downed || !pawn.Spawned)return;var map=pawn.Map;
   foreach(var b in Necro.Registry?.boxes??new List<Phylactery>())if(b!=null && !b.Destroyed && b.revivalTick>=0 && b.MapHeld==map && b.PositionHeld.InHorDistOf(pawn.Position,Radius))b.revivalTick+=e;
   foreach(var c in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>()){if(!c.Position.InHorDistOf(pawn.Position,Radius))continue;var g=Troll.Gene(c.InnerPawn);if(g!=null && g.reviveTick>=0)g.reviveTick+=e;}
  }
 }
 public class ThoughtWorker_GithBetrayal : ThoughtWorker {
  protected override ThoughtState CurrentSocialStateInternal(Pawn p,Pawn other)=>Ascension.Tracker?.githBetrayed==true && Githyanki.Has(p,Githyanki.DevoteeGene) && Demigods.Key(other)=="Undeath";
 }

 // ==================== Harmony：计数钩子 ====================
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class AscensionKill {
  static void Prefix(Pawn __instance,out bool __state){__state=!__instance.Dead && __instance.InMentalState && __instance.MentalStateDef!=null && (__instance.MentalStateDef==MentalStateDefOf.Manhunter || __instance.MentalStateDef==MentalStateDefOf.ManhunterPermanent);}
  static void Postfix(Pawn __instance,DamageInfo? dinfo,bool __state){
   if(!__instance.Dead)return;
   try{
    var v=__instance;var k=dinfo?.Instigator as Pawn;
    if(k!=null && Ascension.Tracker?.Of(k)!=null && v.HostileTo(k)){
     var map=v.MapHeld;
     if(Faith.Storm(map) || (map?.weatherManager?.curWeather?.rainRate??0f)>0f)AscensionPaths.Deed(k,"stormkill");
     if(dinfo.Value.Def==DamageDefOf.Flame || dinfo.Value.Def==DamageDefOf.Burn)AscensionPaths.Deed(k,"firekill");
     if(Faith.Night(map))AscensionPaths.Deed(k,"nightkill");
     if(Items.Undead(v) || Necro.Construct(v)!=null)AscensionPaths.Deed(k,"death");
     if(Twilight.Infused(v)!=null)AscensionPaths.Deed(k,"infused");
    }
    if(k!=null && __state && v.RaceProps.Animal)AscensionPaths.Deed(k,"wild");
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 成神击杀钩子："+e,77310031);}
  }
 }
 [HarmonyPatch(typeof(Pawn_GuestTracker),nameof(Pawn_GuestTracker.CapturedBy))]
 public static class AscensionCapture {static void Postfix(Pawn byPawn){if(byPawn!=null)AscensionPaths.Deed(byPawn,"capture");}}
 [HarmonyPatch(typeof(TendUtility),nameof(TendUtility.DoTend))]
 public static class AscensionTend {static void Postfix(Pawn doctor,Pawn patient){if(doctor!=null && patient!=null && doctor!=patient && patient.Downed)AscensionPaths.Deed(doctor,"tend");}}
 [HarmonyPatch(typeof(Pawn_RecordsTracker),nameof(Pawn_RecordsTracker.Increment))]
 public static class AscensionRecords {
  static void Postfix(Pawn_RecordsTracker __instance,RecordDef def){
   if(def!=RecordDefOf.AnimalsTamed && def!=RecordDefOf.CorpsesBuried)return; // 1.58.6：先看记录类型（开枪、搬运、采矿……每次计数都会经过这里）
   var p=__instance.pawn;if(p==null || def==null || Ascension.Tracker?.Of(p)==null)return;
   if(def==RecordDefOf.AnimalsTamed)AscensionPaths.Deed(p,"wild");
   else if(def==RecordDefOf.CorpsesBuried)AscensionPaths.Deed(p,"death");
  }
 }
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class AscensionAbilityCast {static void Postfix(RimWorld.Ability __instance,bool __result){if(__result && __instance?.def!=null && (__instance.def.IsPsycast || __instance is Ability_Miracle || __instance is Ability_Divine))AscensionPaths.Deed(__instance.pawn,"magic");}}
 // 圣坛祈祷：一次 1 小时，算进圣徒的虔信。
 [HarmonyPatch(typeof(Faith),nameof(Faith.PrayerDone))]
 public static class AscensionPrayer {static void Postfix(Pawn p){var pr=Ascension.Tracker?.Of(p);if(pr!=null && pr.path==Ascension.Saint && pr.stage==0 && (p.needs?.mood==null || p.needs.mood.CurLevelPercentage>=AscensionPaths.SaintMood))pr.Add("pray",Faith.PrayTicks);}}
 // 汲取灵魂（死灵仪式台的 LP_OfferSoul 工作单）。
 [HarmonyPatch(typeof(Bill_Production),nameof(Bill_Production.Notify_IterationCompleted))]
 public static class AscensionSoulOffer {static void Postfix(Bill_Production __instance){if(__instance?.recipe?.defName!="LP_OfferSoul")return;var soul=(__instance.billStack?.billGiver as Building_Altar)?.Box?.soul;if(soul!=null)AscensionPaths.SoulOffered(soul);}}
 // 神明附体首领被击败（离体或死亡都会走 Twilight.Reward）。
 [HarmonyPatch(typeof(Twilight),nameof(Twilight.Reward))]
 public static class AscensionVesselDefeated {static void Postfix(Pawn p){try{AscensionPaths.VesselDefeated(p);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 篡火者计数出错："+e,77310032);}}}
 // 神龛的复生
 [HarmonyPatch(typeof(Phylactery),nameof(Phylactery.ScheduleRevival))]
 public static class ShrineRevival {static void Postfix(Phylactery __instance){AscensionPaths.ShrineScheduled(__instance);}}
 // 圣徒圣印：圣徒的神的神眷获得 ×1.25。
 [HarmonyPatch(typeof(Faith),nameof(Faith.Gain))]
 public static class SaintSealFavor {static void Prefix(LP_DeityDef god,ref float amount){if(amount<=0f || god==null)return;foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)if(Items.Worn(p,"LP_SaintSeal")!=null && FaithUtility.IsChosen(p,out var g) && g==god){amount*=1.25f;return;}}}
 // 永夜冠冕：巫妖精神力上限 ×1.3、死灵造物统御容量 +4。
 [HarmonyPatch(typeof(Hediff_Lich),"get_Power")]
 public static class CrownLichPower {static void Postfix(Hediff_Lich __instance,ref float __result){if(Items.Worn(__instance.pawn,"LP_EternalNightCrown")!=null)__result*=1.3f;}}
 [HarmonyPatch(typeof(Phylactery),"get_Capacity")]
 public static class CrownCapacity {static void Postfix(Phylactery __instance,ref int __result){if(__instance.soul!=null && Items.Worn(__instance.soul,"LP_EternalNightCrown")!=null)__result+=4;}}
 // 半神巫妖之后：吉斯洋基的“巫妖女王的信徒”不再生效。
 [HarmonyPatch(typeof(Githyanki),nameof(Githyanki.SeesLich))]
 public static class GithBetrayed {static void Postfix(ref bool __result){if(__result && Ascension.Tracker?.githBetrayed==true)__result=false;}}
 // 末日卫士：近战是神圣伤害，对亡灵 ×1.5。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class DoomguideMelee {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){
   if(!(dinfo.Instigator is Pawn a) || a.kindDef==null || !a.kindDef.defName.StartsWith("LP_Doom") || dinfo.Def==null || dinfo.Def.isRanged || dinfo.Def==Faith.Radiant)return;
   if(!dinfo.Def.harmsHealth)return;
   dinfo.Def=Faith.Radiant;if(Items.Undead(__instance) || Necro.Construct(__instance)!=null)dinfo.SetAmount(dinfo.Amount*1.5f);
  }
 }
 // 神龛贴图：原图加金光和神纹。
 [StaticConstructorOnStartup]
 public static class ShrineGraphic {
  static Graphic g;
  public static Graphic Get(Thing t){if(g==null){var gd=t.def.graphicData;g=GraphicDatabase.Get<Graphic_Single>("Ascension/PhylacteryShrine",ShaderDatabase.Cutout,gd?.drawSize??Vector2.one,Color.white);}return g;}
 }
 public partial class Phylactery {
  public override Graphic Graphic=>AscensionPaths.IsShrine(this)?ShrineGraphic.Get(this):base.Graphic;
 }
 // 觉醒泰坦的孩子：LP_DivineBlood 不遗传。
 [HarmonyPatch(typeof(PregnancyUtility),nameof(PregnancyUtility.GetInheritedGenes),new[]{typeof(Pawn),typeof(Pawn),typeof(bool)},new[]{ArgumentType.Normal,ArgumentType.Normal,ArgumentType.Ref})]
 public static class DivineBloodNotInherited {static void Postfix(List<GeneDef> __result){if(__result!=null && AscensionPaths.DivineBlood!=null)__result.Remove(AscensionPaths.DivineBlood);}}
}
