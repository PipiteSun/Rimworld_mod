using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.41.0 成神开局剧本（ArtSource/Ascension-1.41.0-spec.md）：耐瑟遗徒、末日将至、朝圣之路、黄昏猎神者。坠落的神裔的父神在 Titan.cs。
 // 每个剧本组件的 Apply 只做“首领 + 路线 + 计时”，开局物品和研究走原版 ScenPart（Defs/Scenarios.xml）。
 public static class AscensionStart {
  // 首领：人物页第一位符合条件的人；都不符合就取第一个开局人物。
  public static Pawn Leader(List<Pawn> pawns,Func<Pawn,bool> ok=null){
   var order=(Find.GameInitData?.startingAndOptionalPawns??new List<Pawn>()).Where(pawns.Contains).Concat(pawns).Distinct().ToList();
   return order.FirstOrDefault(p=>ok==null || ok(p))??order.FirstOrDefault();
  }
  public static void Skill(Pawn p,SkillDef def,int min){var s=p?.skills?.GetSkill(def);if(s==null || s.TotallyDisabled)return;if(s.Level<min)s.Level=min;if(s.passion==Passion.None)s.passion=Passion.Minor;}
  public static int Days(int fireTick)=>Mathf.Max(0,Mathf.CeilToInt((fireTick-Find.TickManager.TicksGame)/(float)GenDate.TicksPerDay));
  public static void Letter(string label,string text,LookTargets look)=>Find.LetterStack.ReceiveLetter(label,text,LetterDefOf.PositiveEvent,look??LookTargets.Invalid);
  public static string Window(Pawn p)=>"选中"+p.LabelShort+"，点“成神之路”查看进度；不想走这条路，可以在窗口里放弃。";
 }

 // ---------- 耐瑟遗徒：首领心灵链接 4（VPE 灵能等级 8）、智识≥10、卡苏斯的回响 2 层；第 10 天拉洛克的使者 ----------
 public class ScenPart_NetherilHeir : ScenPart {
  public int psylink=4,vpeLevel=8,intellect=10,echo=2,envoyDay=10;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref psylink,"psylink",4);Scribe_Values.Look(ref vpeLevel,"vpeLevel",8);Scribe_Values.Look(ref intellect,"intellect",10);Scribe_Values.Look(ref echo,"echo",2);Scribe_Values.Look(ref envoyDay,"envoyDay",10);}
  public override string Summary(Scenario scen)=>"首领是人物页第一位的人类法师：心灵链接"+psylink+"级（装了 VPE 时灵能等级"+vpeLevel+"）、智识至少"+intellect+"、带"+echo+"层“卡苏斯的回响”。影巫王拉洛克的扈从好感0；第"+envoyDay+"天拉洛克的使者必定到来（“拉洛克的邀约”）。";
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(ScenEdit.Starters(map).ToList());}
  public Pawn Apply(List<Pawn> pawns){
   var mage=AscensionStart.Leader(pawns,p=>p.GetStatValue(StatDefOf.PsychicSensitivity)>0 && p.skills?.GetSkill(SkillDefOf.Intellectual)?.TotallyDisabled==false);if(mage==null)return null;
   if(NetherilHooks.VPEActive){for(int i=0;i<30 && NetherilHooks.Level(mage)<vpeLevel;i++)mage.ChangePsylinkLevel(1,false);}
   else if(ModsConfig.RoyaltyActive)for(int i=0;i<10 && mage.GetPsylinkLevel()<psylink;i++)mage.ChangePsylinkLevel(1,false);
   mage.psychicEntropy?.OffsetPsyfocusDirectly(1);
   AscensionStart.Skill(mage,SkillDefOf.Intellectual,intellect);
   if(echo>0)Items.Give(mage,"LP_KarsusEcho",-1,echo);
   var f=NetherilQuest.Larloch;if(f!=null)Factions.SetRelation(f,Faction.OfPlayer,0);
   var st=NetherilQuest.State;int envoyTick=-1;
   if(st!=null && (st.stage=="idle" || st.stage=="scheduled")){st.stage="scheduled";st.envoyTick=envoyTick=Find.TickManager.TicksGame+envoyDay*GenDate.TicksPerDay;}
   AscensionStart.Letter("耐瑟遗徒："+mage.LabelShort,mage.LabelShort+"是耐瑟瑞尔奥术最后的传人之一：心灵链接"+(NetherilHooks.VPEActive?"（灵能等级 "+NetherilHooks.Level(mage)+"）":" "+mage.GetPsylinkLevel()+" 级")+"，身上已有"+echo+"层卡苏斯的回响。\n\n推荐路线：奥术飞升。\n1. 用研习卷和秘术残卷修习（需要心灵链接和智识≥6），“耐瑟瑞尔奥术”研究已经完成，可以建耐瑟祭坛（金 100、秘银 50、玻璃钢 50）。\n2. 第"+envoyDay+"天（还有 "+(envoyTick<0?envoyDay:AscensionStart.Days(envoyTick))+" 天）影巫王拉洛克的使者会来谈卡苏斯之卷：以人换卷、以命匣换卷、替他办三件差事，或者拒绝后去古墓硬抢。\n3. 读满七遍卡苏斯之卷、心灵链接 6 级（VPE 灵能等级≥20）后，在耐瑟祭坛发起升神仪式，成为奥术半神。\n\n“神格学”也已研究：成为奥术半神后会接入成神之路（神灵议会、升入神域）。",new LookTargets(mage));
   return mage;
  }
 }

 // ---------- 末日将至：巫妖（命匣由 LP_LichAscension 生成）走半神巫妖之路，万魂 10/20；第 5 天末日卫士先遣队 ----------
 public class ScenPart_DoomLich : ScenPart {
  public int souls=10,raidDay=5;public float pointsFactor=.5f;public int fireTick=-1;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref souls,"souls",10);Scribe_Values.Look(ref raidDay,"raidDay",5);Scribe_Values.Look(ref pointsFactor,"pointsFactor",.5f);Scribe_Values.Look(ref fireTick,"fireTick",-1);}
  public override string Summary(Scenario scen)=>"巫妖的半神巫妖之路已经开启：试炼一“万魂”已汲取"+souls+" / "+AscensionPaths.LichSouls+"个灵魂，凯兰沃的神眷-"+AscensionPaths.LichKelemvor+"。第"+raidDay+"天凯兰沃的末日卫士先遣队来摧毁命匣（点数×"+pointsFactor.ToString("0.##")+"，不算试炼波数）。";
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(ScenEdit.Starters(map).Concat(map.mapPawns.FreeColonistsSpawned).Distinct().ToList());}
  public AscensionProgress Apply(List<Pawn> pawns){
   var lich=pawns.FirstOrDefault(p=>Necro.Lich(p)?.box!=null);if(lich==null)return null;
   fireTick=Find.TickManager.TicksGame+raidDay*GenDate.TicksPerDay;
   AscensionProgress pr=null;
   if(Ascension.Enabled && Ascension.Tracker?.Of(lich)==null){pr=Ascension.Start(lich,Ascension.Lich,false);pr?.Set("souls",Mathf.Min(souls,AscensionPaths.LichSouls-1));}
   var box=Necro.Lich(lich).box;
   AscensionStart.Letter("末日将至："+lich.LabelShort,lich.LabelShort+"的命匣已经 "+box.level+" 级，离神位只差一步。凯兰沃已经察觉：神眷-"+AscensionPaths.LichKelemvor+"。\n\n推荐路线：半神巫妖。"+(pr!=null?"\n· "+AscensionPaths.StageLabel(pr)+"："+AscensionPaths.StageText(pr):"\n（成神任务线在设置里关着，路线没有开启。）")+"\n· 之后凯兰沃会派末日卫士分三波来摧毁命匣（抗拒审判），守住后在命匣旁举行升神仪式。\n\n第"+raidDay+"天会先来一支末日卫士先遣队（不算试炼的三波），提前准备好防线。"+(pr!=null?"\n\n"+AscensionStart.Window(lich):""),new LookTargets(lich));
   return pr;
  }
  public override void Tick(){if(fireTick>=0 && Find.TickManager.TicksGame>=fireTick){fireTick=-1;Fire(Find.AnyPlayerHomeMap);}}
  public List<Pawn> Fire(Map map){
   if(map==null)return null;
   var fd=DefDatabase<FactionDef>.GetNamedSilentFail("LP_KelemvorChurch");var f=fd==null?null:Factions.EnsureHidden(fd);var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_Doomguide");if(f==null || kind==null)return null;
   var box=map.mapPawns.FreeColonistsSpawned.Select(p=>Necro.Lich(p)?.box).FirstOrDefault(b=>b!=null && b.Spawned && b.Map==map);
   var pawns=Ascension.KindPawns(kind,f,map,StorytellerUtility.DefaultThreatPointsNow(map)*pointsFactor,2);
   Ascension.Arrive(map,f,pawns,box);
   Find.LetterStack.ReceiveLetter("末日卫士的先遣队","凯兰沃的末日卫士先派来了一支小队，要看看这个妄图成神的巫妖有几分斤两。"+(box!=null?"他们直奔命匣。":"")+"\n\n这不是“抗拒审判”的试炼波数，只是预告：真正的审判在万魂完成之后。",LetterDefOf.ThreatBig,new LookTargets(pawns.Cast<Thing>()),f);
   return pawns;
  }
 }

 // ---------- 朝圣之路：开局人物都是同一位神的信徒，首领是神选者；守护神 = 该神，神眷 60，圣徒之路在试炼一 ----------
 public class ScenPart_Pilgrimage : ScenPart {
  public float favor=60f;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref favor,"favor",60f);}
  public override string Summary(Scenario scen)=>"开局人物都是同一位神的信徒（九位可信奉的神里随机，跟着人物页第一位走：重新随机第一位时神也会换），第一位是这位神的神选者；守护神就是这位神，神眷"+favor.ToString("0")+"；圣徒之路已经开启。开局带一座小型化的圣坛（木制，可直接安装）。";
  // 生成开局人物时各给一位随机神的信徒特性（人物页上能看到）；开局时其余人改信首领的神。
  public override void Notify_PawnGenerated(Pawn pawn,PawnGenerationContext context,bool redressed){
   if(context!=PawnGenerationContext.PlayerStarter || redressed || pawn?.story?.traits==null || !pawn.RaceProps.Humanlike)return;
   if(FaithUtility.DevoteeGod(pawn)==null)Convert(pawn,FaithUtility.Worshippable.RandomElementWithFallback());
  }
  public static void Convert(Pawn p,LP_DeityDef god){
   if(p?.story?.traits==null || god==null || Faith.Devotee==null)return;
   foreach(var t in p.story.traits.allTraits.Where(t=>t.def==Faith.Devotee || t.def==Faith.Atheist).ToList())p.story.traits.RemoveTrait(t);
   p.story.traits.GainTrait(new Trait(Faith.Devotee,FaithUtility.DeityToDegree(god),true));
  }
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(ScenEdit.Starters(map).ToList());}
  public AscensionProgress Apply(List<Pawn> pawns){
   var leader=AscensionStart.Leader(pawns);if(leader==null)return null;
   var god=FaithUtility.DevoteeGod(leader);if(god==null || !god.worshippable)god=FaithUtility.Worshippable.RandomElementWithFallback();if(god==null)return null;
   foreach(var p in pawns)if(FaithUtility.DevoteeGod(p)!=god)Convert(p,god);
   if(!FaithUtility.IsChosen(leader,out _) && Faith.ChosenDef!=null){var h=(Hediff_Chosen)HediffMaker.MakeHediff(Faith.ChosenDef,leader);h.deity=god;leader.health.AddHediff(h);}
   FaithUtility.SetPatron(god,true);
   FaithUtility.AddFavor(god,favor-FaithUtility.GetFavor(god),"朝圣之路开局");
   var altar=Faith.AltarDef==null?null:ThingMaker.MakeThing(Faith.AltarDef,ThingDefOf.WoodLog);
   if(altar!=null){Thing placed=altar.def.Minifiable?(Thing)altar.MakeMinified():altar;GenPlace.TryPlaceThing(placed,leader.Position,leader.Map,ThingPlaceMode.Near);placed.SetForbidden(false,false);}
   AscensionProgress pr=null;
   if(Ascension.Enabled && Ascension.Tracker?.Of(leader)==null)pr=Ascension.Start(leader,Ascension.Saint,false);
   AscensionStart.Letter("朝圣之路："+god.label,"你们一路追随"+god.FullLabel+"的启示来到这里。"+leader.LabelShort+"是"+god.label+"的神选者，其余的人都是"+god.label+"的信徒。"+god.label+"已经是殖民地的守护神，神眷 "+FaithUtility.GetFavor(god).ToString("0")+"。\n\n先把小型化的圣坛装起来（选中它，点“安装”），再给"+leader.LabelShort+"戴上圣徽。\n\n推荐路线：圣徒。"+(pr!=null?"\n· "+AscensionPaths.StageLabel(pr)+"："+AscensionPaths.StageText(pr):"\n（成神任务线在设置里关着，路线没有开启。）")+"\n· 之后是奉献（"+AscensionPaths.SaintShards+"片神迹残片和市值"+AscensionPaths.SaintValue+"的祭品）、证道（打败异端勇者）、圣火降临，最后在圣坛升神。神眷掉到"+Ascension.SaintFavor+"以下就要先补回来。"+(pr!=null?"\n\n"+AscensionStart.Window(leader):""),new LookTargets(leader));
   return pr;
  }
 }

 // ---------- 黄昏猎神者：首领老兵（射击或格斗≥12、心灵敏感度≥110%）；第 3 天黄昏之年 ----------
 public class ScenPart_TwilightHunter : ScenPart {
  public int skill=12,twilightDay=3;public float sensitivity=1.1f;
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref skill,"skill",12);Scribe_Values.Look(ref twilightDay,"twilightDay",3);Scribe_Values.Look(ref sensitivity,"sensitivity",1.1f);}
  public override string Summary(Scenario scen)=>"首领是人物页第一位的老兵：射击或格斗（取较高的一项）至少"+skill+"级，心灵敏感度至少"+sensitivity.ToStringPercent()+"。第"+twilightDay+"天黄昏之年必定开始（不受“第2年起”的限制，持续时间照设置）。";
  public override void PostGameStart(){var map=Find.AnyPlayerHomeMap;if(map!=null)Apply(ScenEdit.Starters(map).ToList());}
  public Pawn Apply(List<Pawn> pawns){
   var vet=AscensionStart.Leader(pawns,p=>!p.WorkTagIsDisabled(WorkTags.Violent));if(vet==null)return null;
   var shoot=vet.skills?.GetSkill(SkillDefOf.Shooting);var melee=vet.skills?.GetSkill(SkillDefOf.Melee);
   bool useMelee=shoot==null || shoot.TotallyDisabled || (melee!=null && !melee.TotallyDisabled && melee.Level>shoot.Level);
   AscensionStart.Skill(vet,useMelee?SkillDefOf.Melee:SkillDefOf.Shooting,skill);
   Sensitize(vet,sensitivity);
   int startTick=Find.TickManager.TicksGame+twilightDay*GenDate.TicksPerDay;
   var w=Twilight.World;if(w!=null && !Twilight.Active)w.startTick=startTick;
   AscensionStart.Letter("黄昏猎神者："+vet.LabelShort,vet.LabelShort+"打过的仗比谁都多，最近却总梦见众神在暗红的天幕下争吵。\n\n第"+twilightDay+"天黄昏之年就会开始：敌人里的神力灌注者大幅增多，大规模袭击的领队可能被神明附体，击退附体者会掉神迹残片。\n\n推荐路线：篡火者（难度很高）。\n· 殖民者击败一名神明附体的首领时，篡火者之路自动开启（最后一击要由殖民者打中）。\n· 试炼一：击败同一位神的附体首领 3 名；试炼二：完成被篡之神的事迹；最后在圣坛举行篡火仪式——成功率 = 50% + (心灵敏感度-1)×50% + 每片神迹残片 8%，最多 95%，失败会死。\n\n开局的 5 片神迹残片可以留着提高篡火成功率。“神格学”已研究。",new LookTargets(vet));
   return vet;
  }
  static TraitDef psy;
  static TraitDef PsyTrait=>psy??(psy=DefDatabase<TraitDef>.GetNamedSilentFail("PsychicSensitivity"));
  // 心灵敏感度不够：去掉迟钝/失聪，换成“心灵敏感”，还不够就“心灵超敏”。
  public static void Sensitize(Pawn p,float min){
   if(PsyTrait==null || p?.story?.traits==null)return;
   for(int degree=1;degree<=2 && p.GetStatValue(StatDefOf.PsychicSensitivity,true,0)<min;degree++){
    var have=p.story.traits.GetTrait(PsyTrait);if(have!=null && have.Degree>=degree)continue;
    if(have!=null)p.story.traits.RemoveTrait(have);
    p.story.traits.GainTrait(new Trait(PsyTrait,degree,true));
   }
  }
 }
}
