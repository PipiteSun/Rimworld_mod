using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.117.0 第三批事件 · 第二批：逃亡的同盟贵族、托付的天才学徒、秘银陨星、全灭的冒险小队、魔网潮汐、命匣窃贼、冒险者的天命；以及事件站点的地图生成。

 // ==================== 逃亡的同盟贵族 ====================
 // 一名受伤的同盟贵族逃进地图，2 小时后塞伊追兵（1.2 倍点数）到。三选一：
 //  庇护：贵族住 5 天后离开，同盟好感 +30，空投 1500 银和一件优秀品质武器；贵族死了同盟 -20。
 //  交给塞伊：塞伊好感 +25，送来一卷研习卷，追兵取消。  卖给黑网：800 银，同盟 -40，追兵取消。
 // 不答复：追兵照样来，按庇护处理。
 public static class Noble {
  public const string Kind="noble";
  public const int RaidHours=2,StayDays=5,Silver=1500,SellSilver=800,GoodwillSafe=30,GoodwillDead=-20,GoodwillThay=25,GoodwillSold=-40;
  public const string RaidLabel="塞伊追兵";
  public static bool CanFire(Map map){
   var c=Factions.Colony;var t=Factions.Hostile;
   return Events3.S.e3Noble && c!=null && !c.defeated && !c.HostileTo(Faction.OfPlayer) && t!=null && !t.defeated && Factions.Enabled(t) && t.HostileTo(Faction.OfPlayer) && !Events3.Active(Kind).Any();
  }
  public static E3Record Start(Map map){
   var f=Factions.Colony;var p=Events3.MakePawn(f.def.basicMemberKind??PawnKindDefOf.Villager,f,map);if(p==null)return null;
   var lord=Events3.Visit(map,new List<Pawn>{p},f,null);if(lord==null)return null;
   for(int i=0;i<3;i++){var part=p.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Cut,BodyPartHeight.Undefined,BodyPartDepth.Outside);if(part!=null && !p.Downed)p.TakeDamage(new DamageInfo(DamageDefOf.Cut,5f,0f,-1f,null,part));}
   var r=Events3.Add(Kind,map);r.a=p;r.lord=lord;r.next=Events3.Now+RaidHours*GenDate.TicksPerHour;
   Tribute.Events?.scheduledRaids.Add(new ScheduledRaid{faction=Factions.HostileDef,fireTick=r.next,pointsFactor=1.2f,letterLabel=RaidLabel});
   Events3.Ask(r,"逃亡的同盟贵族","费伦拓荒同盟的一位贵族"+p.LabelShort+"浑身是伤，跌跌撞撞逃进了地图：塞伊的红袍法师在追杀"+p.gender.GetPronoun()+"，追兵大约 "+RaidHours+" 小时后就到。\n\n"+p.LabelShort+"求你们庇护。",new LookTargets(p),f);
   return r;
  }
  static void CancelRaid(E3Record r){Tribute.Events?.scheduledRaids.RemoveAll(x=>x.fireTick==r.next && x.letterLabel==RaidLabel);}
  public static IEnumerable<DiaOption> Options(Letter_E3 l,E3Record r){
   yield return l.Choice("庇护（打退追兵；"+StayDays+" 天后同盟好感 +"+GoodwillSafe+"、"+Silver+" 银和一件优秀武器）",()=>Shelter(r));
   yield return l.Choice("交给塞伊（塞伊好感 +"+GoodwillThay+"，一卷研习卷）",()=>HandOver(r));
   var bn=Factions.Of(Factions.BlackNetDef);
   yield return l.Choice("卖给黑网（"+SellSilver+" 银，同盟好感 "+GoodwillSold+"）",()=>Sell(r),bn==null?"这个存档里没有黑网":null);
  }
  public static void Shelter(E3Record r){r.answered=true;r.stage=1;r.until=Math.Max(r.next,Events3.Now)+StayDays*GenDate.TicksPerDay;}
  public static void HandOver(E3Record r){
   r.answered=true;r.done=true;CancelRaid(r);var map=r.map;var name=r.a?.LabelShort;Events3.Vanish(r.a);
   var t=Factions.Hostile;if(t!=null)Factions.ChangeGoodwillExact(t,GoodwillThay,"交出了同盟贵族");
   var gift=Events3.DropPods(map,new[]{Netheril.MakeStudy(QualityCategory.Good)});
   Find.LetterStack.ReceiveLetter("交给塞伊",name+"被红袍法师带走了。塞伊远征军好感 +"+GoodwillThay+"，空投送来"+Events3.Labels(gift)+"。",LetterDefOf.NeutralEvent);
  }
  public static void Sell(E3Record r){
   r.answered=true;r.done=true;CancelRaid(r);var map=r.map;var name=r.a?.LabelShort;Events3.Vanish(r.a);
   var c=Factions.Colony;if(c!=null)Factions.ChangeGoodwillExact(c,GoodwillSold,"把同盟贵族卖给了黑网");
   Events3.DropPods(map,new[]{Events3.Silver(SellSilver)});
   Find.LetterStack.ReceiveLetter("卖给黑网","黑网的人蒙着脸来，把"+name+"装进麻袋带走了，留下 "+SellSilver+" 银。同盟很快听说了：好感 "+GoodwillSold+"。",LetterDefOf.NegativeEvent);
  }
  public static void Tick(E3Record r){
   var p=r.a;
   if(p==null || p.Dead || p.Destroyed){
    r.done=true;var c=Factions.Colony;if(c!=null && p!=null && p.Dead){Factions.ChangeGoodwillExact(c,GoodwillDead,"同盟贵族死在殖民地");Find.LetterStack.ReceiveLetter("贵族死了",p.LabelShort+"没能活下来。同盟好感 "+GoodwillDead+"。",LetterDefOf.NegativeEvent);}
    return;
   }
   if(!r.answered && Events3.Now>r.next)Shelter(r);
   if(r.stage==1 && Events3.Now>=r.until){
    r.done=true;Events3.Leave(r.lord);var c=Factions.Colony;if(c!=null)Factions.ChangeGoodwillExact(c,GoodwillSafe,"庇护了同盟贵族");
    var gifts=Events3.DropPods(r.map,new[]{Events3.Silver(Silver),Events3.GoodWeapon()});
    Events3.State?.Deed("庇护了被塞伊追杀的同盟贵族");
    Find.LetterStack.ReceiveLetter("贵族的谢礼",p.LabelShort+"的伤养好了，告辞回去。同盟好感 +"+GoodwillSafe+"，空投送来"+Events3.Labels(gifts)+"。",LetterDefOf.PositiveEvent,p);
   }
  }
 }
 public class IncidentWorker_E3Noble : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Noble;
  protected override bool CanFire(Map map,IncidentParms parms)=>Noble.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms)=>Noble.Start((Map)parms.target)!=null;
 }

 // ==================== 托付的天才学徒（Biotech） ====================
 // 同盟好感≥20 时，一位同盟法师托付一个 9～11 岁的魔法天才（“魔法天才”：学习速度 +50%）。每 60 天空投一份礼物（卷轴、属性典籍或 500 银，两样）。
 // 13 岁成年：智识≥6 就成为 1 级法师，同盟好感 +20；否则 +5。孩子死了同盟 -50。心情崩溃时野魔法爆发。
 public static class Ward {
  public const string Kind="ward";
  public const int MinGoodwill=20,GiftDays=60,AdultAge=13,NeedIntellect=6,GoodwillDone=20,GoodwillDead=-50;
  public static bool CanFire(Map map){var c=Factions.Colony;return Events3.S.e3Ward && ModsConfig.BiotechActive && c!=null && !c.HostileTo(Faction.OfPlayer) && c.PlayerGoodwill>=MinGoodwill && !Events3.Active(Kind).Any();}
  public static E3Record Start(Map map){
   var r=Events3.Add(Kind,map);
   Events3.Ask(r,"托付的天才学徒","费伦拓荒同盟的一位法师通过通讯找到你：她手下有个孩子魔法天赋太强，城里的人怕了，想托你们养到成年。\n\n孩子会带着“魔法天才”（学习速度 +50%）；她每 "+GiftDays+" 天寄一次东西来。孩子 "+AdultAge+" 岁成年时，如果智识到了 "+NeedIntellect+" 级，就能成为法师。\n\n小心：天才的孩子心情崩溃时会野魔法爆发。",LookTargets.Invalid,Factions.Colony);
   return r;
  }
  public static IEnumerable<DiaOption> Options(Letter_E3 l,E3Record r){
   yield return l.Choice("收下孩子",()=>Accept(r));
   yield return l.Choice("婉拒",()=>{r.answered=true;r.done=true;});
  }
  public static Pawn Accept(E3Record r){
   r.answered=true;var map=r.map??Events3.Home;if(map==null){r.done=true;return null;}
   Pawn child=null;
   // 小孩本来就不能做研究（原版按发育阶段禁用），13 岁后才解开；这里只给智识大热情。
   try{child=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,fixedBiologicalAge:Rand.Range(9f,11f),developmentalStages:DevelopmentalStage.Child));}
   catch(Exception e){Log.Warning("[DND边缘大陆] 天才学徒生成失败："+e.Message);}
   var intel=child?.skills?.GetSkill(SkillDefOf.Intellectual);if(intel!=null)intel.passion=Passion.Major;
   if(child==null){r.done=true;return null;}
   Events3.DropPods(map,new[]{child});Items.Give(child,"LP_E3_GiftedWard");
   r.a=child;r.next=Events3.Now+GiftDays*GenDate.TicksPerDay;
   Find.LetterStack.ReceiveLetter("天才学徒到了",child.LabelShort+"坐着空投舱到了，怀里抱着一摞书。",LetterDefOf.PositiveEvent,child);
   return child;
  }
  public static void Tick(E3Record r){
   if(!r.answered){if(Events3.Now-r.start>2*GenDate.TicksPerDay)r.done=true;return;}
   var p=r.a;if(p==null){r.done=true;return;}
   if(p.Dead){r.done=true;var c=Factions.Colony;if(c!=null)Factions.ChangeGoodwillExact(c,GoodwillDead,"天才学徒死了");Find.LetterStack.ReceiveLetter("学徒死了",p.LabelShort+"死了。托付孩子的法师再也没有来信。同盟好感 "+GoodwillDead+"。",LetterDefOf.NegativeEvent);return;}
   if(p.Faction!=Faction.OfPlayer){r.done=true;return;}
   if(p.ageTracker.AgeBiologicalYears>=AdultAge){Graduate(r);return;}
   if(Events3.Now>=r.next){
    r.next+=GiftDays*GenDate.TicksPerDay;var map=p.MapHeld??r.map;
    var pool=new List<Thing>{TowerCrash.MakeScroll(),Growth.MakeTome(),Events3.Silver(500)}.Where(t=>t!=null).InRandomOrder().Take(2).ToList();
    Events3.DropPods(map,pool);
    Find.LetterStack.ReceiveLetter("法师的来信","托付"+p.LabelShort+"的法师寄来了一封信和"+Events3.Labels(pool)+"。",LetterDefOf.PositiveEvent,p);
   }
  }
  public static void Graduate(E3Record r){
   r.done=true;var p=r.a;var c=Factions.Colony;p.Notify_DisabledWorkTypesChanged();
   if((p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level??0)>=NeedIntellect){
    var h=Adventurer.Get(p)??Adventurer.TryAdd(p,1,true);var wiz=DefDatabase<LP_ClassDef>.GetNamedSilentFail("LP_Class_Wizard");
    if(h!=null && wiz!=null && ClassSystem.Level(h,wiz)<1)ClassSystem.SetLevel(h,wiz,1);
    if(c!=null)Factions.ChangeGoodwillExact(c,GoodwillDone,"天才学徒学成");
    Find.LetterStack.ReceiveLetter("学徒出师",p.LabelShort+"成年了，第一次独自施放了法术：成为 1 级法师。托付孩子的法师很欣慰：同盟好感 +"+GoodwillDone+"。",LetterDefOf.PositiveEvent,p);
   }else{
    if(c!=null)Factions.ChangeGoodwillExact(c,5,"天才学徒成年");
    Find.LetterStack.ReceiveLetter("学徒成年",p.LabelShort+"成年了。书读得不够（智识不到 "+NeedIntellect+"），没能成为法师，不过人平平安安长大了：同盟好感 +5。",LetterDefOf.NeutralEvent,p);
   }
  }
  public static void OnBreak(Pawn p){
   if(p==null || p.Dead || !p.Spawned || !Items.Has(p,"LP_E3_GiftedWard"))return;
   var e=TowerCrash.WildMagic(p);
   Messages.Message(p.LabelShort+"情绪失控，身上的魔力爆了出来（野魔法）。",p,MessageTypeDefOf.NeutralEvent,false);
  }
 }
 public class IncidentWorker_E3Ward : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Ward;
  protected override bool CanFire(Map map,IncidentParms parms)=>Ward.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms)=>Ward.Start((Map)parms.target)!=null;
 }

 // ==================== 秘银陨星 ====================
 // 一晚流星雨（8 小时，傍晚以后才会来），开始时殖民者心情 +4。结束时落下 1～3 块陨石，每块是 4～7 格秘银矿脉；
 // 一成几率带一颗坠星之心，2 天后黑网的抢夺队（临时敌对）来抢。
 public class GameCondition_Starfall : GameCondition {
  public bool forceHeart;
  public override void Init(){base.Init();foreach(var m in AffectedMaps)Events3.ThoughtAll(m,"LP_E3_Starfall");}
  public override void End(){foreach(var m in AffectedMaps.ToList())try{Starfall.Fall(m,forceHeart);}catch(Exception e){Log.Error("[DND边缘大陆] 秘银陨星出错："+e);}base.End();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref forceHeart,"lpForceHeart");}
 }
 public static class Starfall {
  public const int Hours=8,RaidDays=2;public const float HeartChance=.1f;
  public static GameConditionDef Def=>DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_Starfall");
  public static bool CanFire(Map map){int h=GenLocalDate.HourOfDay(map);return Events3.S.e3Starfall && Def!=null && Items.Thing("LP_MineableMithral")!=null && (h>=17 || h<=1) && !map.gameConditionManager.ConditionIsActive(Def);}
  public static GameCondition_Starfall Start(Map map,bool forceHeart=false){
   var c=(GameCondition_Starfall)GameConditionMaker.MakeCondition(Def,Hours*GenDate.TicksPerHour);c.forceHeart=forceHeart;
   map.gameConditionManager.RegisterCondition(c);return c;
  }
  public static bool Cell(Map map,out IntVec3 cell){
   var colony=Events3.Colonists(map).Select(p=>p.Position).ToList();
   return CellFinder.TryFindRandomCellNear(map.Center,map,Mathf.Min(map.Size.x,map.Size.z)/2-8,c=>c.Standable(map) && !c.Roofed(map) && !c.Fogged(map) && c.GetEdifice(map)==null && c.GetFirstItem(map)==null && colony.All(x=>x.DistanceTo(c)>=8f) && GenRadial.RadialCellsAround(c,2f,true).All(x=>x.InBounds(map) && x.GetEdifice(map)==null),out cell);
  }
  public static int Fall(Map map,bool forceHeart=false){
   var ore=Items.Thing("LP_MineableMithral");if(ore==null || map==null)return 0;
   int n=Rand.RangeInclusive(1,3);bool heart=forceHeart || Events3.Roll<HeartChance;int fell=0;IntVec3 heartCell=IntVec3.Invalid;
   for(int i=0;i<n;i++){
    if(!Cell(map,out var cell))continue;
    var things=new List<Thing>();int k=Rand.RangeInclusive(4,7);for(int j=0;j<k;j++)things.Add(ThingMaker.MakeThing(ore));
    if(heart && i==0){var h=Events3.Make("LP_StarHeart");if(h!=null){things.Add(h);heartCell=cell;}}
    SkyfallerMaker.SpawnSkyfaller(ThingDefOf.MeteoriteIncoming,things,cell,map);fell++;
   }
   if(fell==0)return 0;
   string text="天快亮时，"+fell+"块陨石拖着银光砸进了地图。凿开它们能挖出秘银。";
   if(heartCell.IsValid){
    text+="\n\n其中一块陨石里裹着一颗还在发光的“坠星之心”——传奇武器的材料。消息传得很快："+RaidDays+" 天后黑网会派人来抢。";
    var bn=Factions.BlackNetDef;if(bn!=null && Factions.Of(bn)!=null)Tribute.Events?.scheduledRaids.Add(new ScheduledRaid{faction=bn,fireTick=Events3.Now+RaidDays*GenDate.TicksPerDay,pointsFactor=.8f,letterLabel="坠星之心的抢夺者",temporaryHostile=true});
   }
   Find.LetterStack.ReceiveLetter("秘银陨星",text,LetterDefOf.PositiveEvent,heartCell.IsValid?new LookTargets(heartCell,map):LookTargets.Invalid);
   return fell;
  }
 }
 public class IncidentWorker_E3Starfall : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Starfall;
  protected override bool CanFire(Map map,IncidentParms parms)=>Starfall.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;if(Starfall.Start(map)==null)return false;
   SendStandardLetter("流星雨","夜空里划过一道又一道银色的流星。在户外看着的人都很开心（心情 +4）。\n\n天亮前会有陨石落下来——流星雨这么亮，里面多半有秘银。",LetterDefOf.PositiveEvent,parms,LookTargets.Invalid);
   return true;
  }
 }

 // ==================== 全灭的冒险小队 ====================
 // 世界地图上出现一个站点（4～10 格，15 天）：4 具冒险者尸体（带装备）和杀死他们的怪物（传奇怪物或强力野兽，受了伤），地上有一本冒险日志（读了得 400 冒险者经验）。
 // 把尸体葬在殖民地的坟墓里：同盟好感 +10（一次）。复活他们：问你要不要收留，答应就加入。
 public static class FallenParty {
  public const string Kind="party";
  public const int SiteDays=15,Corpses=4,BuryGoodwill=10;
  public static bool CanFire(Map map)=>Events3.S.e3Party && Events3.SiteDef!=null && !Events3.Active(Kind).Any();
  public static E3Record Start(Map map){
   var r=Events3.Add(Kind,map);float pts=Mathf.Max(300f,StorytellerUtility.DefaultThreatPointsNow(map));
   if(Events3.MakeSite(r,null,pts,4,10,SiteDays)==null){r.done=true;return null;}
   r.until=Events3.Now+(SiteDays+30)*GenDate.TicksPerDay;
   return r;
  }
  public static Pawn Monster(Map map){
   Pawn m=null;
   try{if(Monsters.On && Monsters.Available)m=Monsters.Make(new[]{Monsters.Behir,Monsters.Beholder,Monsters.DeathKnight}.RandomElement(),null,map.Tile);}catch{m=null;}
   if(m==null){var k=new[]{"LP_Manticore","LP_Bulette","LP_Basilisk"}.Select(n=>DefDatabase<PawnKindDef>.GetNamedSilentFail(n)).Where(x=>x!=null).RandomElementWithFallback()??PawnKindDefOf.Thrumbo;m=PawnGenerator.GeneratePawn(k,null);}
   return m;
  }
  public static void Generate(Map map,E3Record r){
   if(!CellFinder.TryFindRandomCellNear(map.Center,map,20,c=>c.Standable(map) && !c.Roofed(map),out var center))center=map.Center;
   var f=Factions.Colony;
   for(int i=0;i<Corpses;i++){
    var p=Events3.MakePawn(f?.def.basicMemberKind??PawnKindDefOf.Villager,f,map);if(p==null)continue;
    Adventurer.TryAdd(p,Rand.RangeInclusive(3,8),true);
    GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(center,map,4),map);p.Kill(null);r.pawns.Add(p);
   }
   Events3.Drop(map,center,new[]{Events3.Make("LP_AdventureLog")});
   var m=Monster(map);
   GenSpawn.Spawn(m,CellFinder.RandomClosewalkCellNear(center,map,6),map);
   for(int i=0;i<30 && m.health.summaryHealth.SummaryHealthPercent>.65f && !m.Downed;i++){var part=m.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Cut,BodyPartHeight.Undefined,BodyPartDepth.Outside);if(part==null)break;m.TakeDamage(new DamageInfo(DamageDefOf.Cut,6f,0f,-1f,null,part));}
   if(m.RaceProps.Animal || m.Faction==null)m.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true);
   else LordMaker.MakeNewLord(m.Faction,new LordJob_DefendPoint(center),map,new List<Pawn>{m});
   r.a=m;r.stage=1;
  }
  public static void Tick(E3Record r){
   if(r.stage==1 && Events3.SiteCleared(r)){r.stage=2;Find.LetterStack.ReceiveLetter("小队的营地","杀死冒险小队的怪物倒下了。他们的装备和冒险日志都在营地里。\n\n把遗体带回去葬在殖民地的坟墓里，同盟会感激（+"+BuryGoodwill+"）；如果有办法复活他们，他们也许愿意留下。",LetterDefOf.PositiveEvent);}
   if(!r.flag && r.pawns.Any(p=>p!=null && p.Dead && p.Corpse?.ParentHolder is Building_Grave)){r.flag=true;var c=Factions.Colony;if(c!=null)Factions.ChangeGoodwillExact(c,BuryGoodwill,"安葬了全灭的冒险小队");Messages.Message("冒险小队的遗体入土为安：同盟好感 +"+BuryGoodwill+"。",MessageTypeDefOf.PositiveEvent,false);}
   foreach(var p in r.pawns.Where(x=>x!=null && !x.Dead && x.Spawned && x.Faction!=Faction.OfPlayer).ToList()){
    string key="party:"+p.thingIDNumber;var s=Events3.State;if(s.callingDone.Contains(key))continue;s.callingDone.Add(key);
    var q=Events3.Add(Kind+"Join",p.Map);q.a=p;q.until=Events3.Now+3*GenDate.TicksPerDay;
    var l=Events3.Ask(q,"复活的冒险者",p.LabelShort+"睁开了眼睛。"+p.gender.GetPronoun()+"记得最后看到的是怪物的獠牙——是你们把"+p.gender.GetPronoun()+"带了回来。\n\n"+p.LabelShort+"想留下来报答你们。",new LookTargets(p));
   }
   if(Events3.Now>r.until)r.done=true;
  }
  public static IEnumerable<DiaOption> Options(Letter_E3 l,E3Record r){
   if(r.a==null || r.a.Dead)yield break;
   yield return l.Choice("收留"+r.a.LabelShort,()=>{r.answered=true;r.done=true;if(r.a.Faction!=Faction.OfPlayer)r.a.SetFaction(Faction.OfPlayer);});
   yield return l.Choice("送"+r.a.LabelShort+"回同盟",()=>{r.answered=true;r.done=true;var c=Factions.Colony;if(c!=null){r.a.SetFaction(c);Factions.ChangeGoodwillExact(c,10,"复活了同盟的冒险者");}});
  }
 }
 public class IncidentWorker_E3Party : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Party;
  protected override bool CanFire(Map map,IncidentParms parms)=>FallenParty.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var r=FallenParty.Start((Map)parms.target);if(r==null)return false;
   SendStandardLetter("全灭的冒险小队","一位路过的商人说，附近一支四人冒险小队进了荒野就再没回来。他在远处看见过他们的营地——还有守在营地边上的东西。\n\n站点已标在世界地图上，"+FallenParty.SiteDays+" 天后消失。",LetterDefOf.NeutralEvent,parms,new LookTargets(r.site));
   return true;
  }
 }

 // ==================== 魔网潮汐 ====================
 // 5 天彩色极光（游戏条件）。期间施放能力（原版 Ability）20% 触发野魔法：
 //  不耗冷却、周围 6 格植物长熟、急速、隐形（好）；随机传送、打嗝、脚下小火球（坏）。施法职业（法师、术士、邪术师、牧师、德鲁伊）每天冒险者经验 +120。
 public static class WeaveSurge {
  public const string Kind="surge";
  public const int Days=5,DailyXp=120;public const float Chance=.2f,BloomRadius=6f;
  static readonly string[] CasterClasses={"LP_Class_Wizard","LP_Class_Sorcerer","LP_Class_Warlock","LP_Class_Cleric","LP_Class_Druid"};
  public static GameConditionDef Def=>DefDatabase<GameConditionDef>.GetNamedSilentFail("LP_WeaveSurge");
  public static bool CanFire(Map map)=>Events3.S.e3Surge && Def!=null && !map.gameConditionManager.ConditionIsActive(Def);
  public static bool Active(Map map)=>map!=null && Def!=null && map.gameConditionManager.ConditionIsActive(Def);
  public static E3Record Start(Map map){
   map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeCondition(Def,Days*GenDate.TicksPerDay));
   var r=Events3.Add(Kind,map);r.until=Events3.Now+Days*GenDate.TicksPerDay;r.next=Events3.Now+GenDate.TicksPerDay;return r;
  }
  public static bool Caster(Pawn p){foreach(var n in CasterClasses){var c=DefDatabase<LP_ClassDef>.GetNamedSilentFail(n);if(c!=null && ClassSystem.Level(p,c)>0)return true;}return false;}
  public static void Tick(E3Record r){
   if(Events3.Now>=r.next){r.next+=GenDate.TicksPerDay;foreach(var p in Events3.Colonists(r.map).Where(Caster))Adventurer.GainXP(p,DailyXp);}
   if(Events3.Now>=r.until)r.done=true;
  }
  public static void OnCast(RimWorld.Ability a){
   var p=a?.pawn;if(p==null || !p.Spawned || !Active(p.Map) || Events3.Roll>=Chance)return;
   Surge(p,a,Rand.Range(0,7));
  }
  public static string Surge(Pawn p,RimWorld.Ability a,int k){
   var map=p.Map;string what;
   switch(k){
    case 0:a?.ResetCooldown();what="这次施法没有冷却";break;
    case 1:{int n=0;foreach(var c in GenRadial.RadialCellsAround(p.Position,BloomRadius,true)){if(!c.InBounds(map))continue;var pl=c.GetPlant(map);if(pl!=null && pl.Growth<1f){pl.Growth=1f;map.mapDrawer.MapMeshDirty(c,MapMeshFlagDefOf.Things);n++;}}what="周围的植物瞬间长熟了（"+n+" 株）";break;}
    case 2:TowerCrash.WildMagic(p,"haste");what="身体变得飞快";break;
    case 3:TowerCrash.WildMagic(p,"invisible");what="身影消失了";break;
    case 4:TowerCrash.WildMagic(p,"teleport");what="被魔网甩到了别处";break;
    case 5:TowerCrash.WildMagic(p,"hiccup");what="打起了停不下来的嗝";break;
    default:GenExplosion.DoExplosion(p.Position,map,1.9f,DamageDefOf.Flame,null,8);what="脚下炸开一个小火球";break;
   }
   Messages.Message("魔网潮汐："+p.LabelShort+what+"。",p,k<4?MessageTypeDefOf.PositiveEvent:MessageTypeDefOf.NegativeEvent,false);
   return what;
  }
 }
 public class IncidentWorker_E3Surge : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Surge;
  protected override bool CanFire(Map map,IncidentParms parms)=>WeaveSurge.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   if(WeaveSurge.Start((Map)parms.target)==null)return false;
   SendStandardLetter("魔网潮汐","天上挂起了彩色的极光：魔网在涨潮。接下来 "+WeaveSurge.Days+" 天里，每次施放能力都有 "+(WeaveSurge.Chance*100).ToString("0")+"% 几率引发野魔法——有好有坏。\n\n施法职业（法师、术士、邪术师、牧师、德鲁伊）每天额外得到 "+WeaveSurge.DailyXp+" 点冒险者经验。",LetterDefOf.NeutralEvent,parms,LookTargets.Invalid);
   return true;
  }
 }

 // ==================== 命匣窃贼 ====================
 // 殖民地地图上摆着巫妖命匣时，1～2 名隐身的盗贼（海盗派系）夜里潜入偷它。有殖民者走到他们 5 格内就会现形。
 // 偷走了：世界地图上出现藏身处（3～8 格），15 天内打进去把命匣带回殖民地；过期命匣被卖给九狱掮客（九狱商队能赎回）。
 // 抓到活的盗贼：审问出藏宝点（另一个站点，有 500～1000 银和一件奇物）。
 public static class Thief {
  public const string Kind="thief";
  public const int SiteDays=15,RevealRadius=5;
  public static Phylactery TargetBox(Map map)=>Necro.Registry?.boxes.FirstOrDefault(b=>b!=null && !b.Destroyed && b.Spawned && b.Map==map && b.soul!=null && b.soul.Faction==Faction.OfPlayer);
  public static Faction ThiefFaction()=>Find.FactionManager.AllFactions.Where(f=>!f.defeated && !f.IsPlayer && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && !f.def.hidden && f.def.techLevel>=TechLevel.Medieval).OrderByDescending(f=>f.def==FactionDefOf.Pirate?1:0).FirstOrDefault();
  public static bool CanFire(Map map)=>Events3.S.e3Thief && TargetBox(map)!=null && ThiefFaction()!=null && !Events3.Active(Kind).Any();
  public static E3Record Start(Map map){
   var box=TargetBox(map);var f=ThiefFaction();if(box==null || f==null)return null;
   if(!Events2.EdgeCell(map,out var entry))return null;
   var list=new List<Pawn>();int n=Rand.RangeInclusive(1,2);
   for(int i=0;i<n;i++){var p=Events3.MakePawn(f.def.basicMemberKind??PawnKindDefOf.Drifter,f,map);if(p==null)continue;GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(entry,map,3),map);Items.Give(p,"LP_E3_Shadowed");list.Add(p);}
   if(list.Count==0)return null;
   LordMaker.MakeNewLord(f,new LordJob_ExitMapBest(LocomotionUrgency.Jog,true,true),map,list);
   var r=Events3.Add(Kind,map);r.pawns=list;r.thing=box;r.faction=f;r.until=Events3.Now+GenDate.TicksPerDay;
   foreach(var p in list)Steal(p,box);
   return r;
  }
  static void Steal(Pawn p,Thing box){
   if(p==null || !p.Spawned || p.Downed || p.carryTracker?.CarriedThing!=null || !box.Spawned)return;
   if(p.CurJobDef==JobDefOf.Steal)return;
   var job=JobMaker.MakeJob(JobDefOf.Steal,box);job.count=1;p.jobs.TryTakeOrderedJob(job,JobTag.Misc);
  }
  public static void Tick(E3Record r){
   var box=r.thing as Phylactery;
   if(box==null || box.Destroyed){if(r.stage==1)Find.LetterStack.ReceiveLetter("命匣毁了","被偷走的命匣毁了。",LetterDefOf.NegativeEvent);r.done=true;return;}
   switch(r.stage){
    case 0:{
     foreach(var p in r.pawns.Where(x=>x!=null && x.Spawned && !x.Dead).ToList()){
      if(Items.Has(p,"LP_E3_Shadowed") && Events3.Colonists(p.Map).Any(c=>c.Position.DistanceTo(p.Position)<=RevealRadius)){Items.Remove(p,"LP_E3_Shadowed");Messages.Message("发现了潜入者："+p.LabelShort+"！",p,MessageTypeDefOf.ThreatSmall,false);}
      Steal(p,box);
     }
     var carrier=r.pawns.FirstOrDefault(p=>p!=null && !p.Dead && (p.carryTracker?.CarriedThing==box || p.inventory?.innerContainer.Contains(box)==true));
     if(carrier!=null && !carrier.Spawned){Stolen(r,carrier,box);return;}
     if(r.pawns.All(p=>p==null || p.Dead || p.Downed || !p.Spawned || p.IsPrisoner)){
      r.done=true;var caught=r.pawns.FirstOrDefault(p=>p!=null && !p.Dead && p.IsPrisonerOfColony);
      if(caught!=null)LootSite(r,caught);
      else Messages.Message("命匣窃贼失手了。",MessageTypeDefOf.PositiveEvent,false);
     }
     break;}
    case 1:
     if(box.Spawned && box.Map.IsPlayerHome){r.done=true;Events3.State?.Deed("从盗贼公会手里夺回了命匣");Find.LetterStack.ReceiveLetter("命匣回来了",box.soul?.LabelShort+"的命匣回到了殖民地。",LetterDefOf.PositiveEvent,box);return;}
     if(Events3.Now>r.until)Seize(r,box);
     break;
   }
  }
  public static void Stolen(E3Record r,Pawn carrier,Phylactery box){
   box.holdingOwner?.Remove(box);Events3.State.hold.TryAdd(box,false);
   r.stage=1;var pts=Mathf.Max(250f,StorytellerUtility.DefaultThreatPointsNow(r.map));
   var site=Events3.MakeSite(r,r.faction,pts,3,8,SiteDays);
   if(site==null){Seize(r,box);return;}
   Find.LetterStack.ReceiveLetter("命匣被偷了",carrier.LabelShort+"带着"+box.soul?.LabelShort+"的命匣溜出了地图！\n\n盗贼的藏身处已标在世界地图上。"+SiteDays+" 天内打进去，把命匣带回殖民地——过期它就会被卖给九狱掮客（只能花大价钱从九狱商队赎回）。",LetterDefOf.ThreatBig,new LookTargets(site));
  }
  public static void Seize(E3Record r,Phylactery box){
   r.done=true;var owner=box.soul;
   if(box.Spawned)box.DeSpawn(DestroyMode.Vanish);else box.holdingOwner?.Remove(box);
   var c=Pacts.Comp;
   if(c!=null && owner!=null){c.seized.TryAdd(box,false);c.seizedOwners.Add(owner);Items.Give(owner,"LP_PhylacteryLost");
    Find.LetterStack.ReceiveLetter("命匣被卖掉了","盗贼公会把"+owner.LabelShort+"的命匣卖给了九狱掮客。"+owner.LabelShort+"的力量还在，但死后不会再复活。\n\n九狱商队来时可以赎回（"+Pacts.RansomSilver.ToString("0")+" 银）。",LetterDefOf.NegativeEvent,owner);}
   else{var home=Events3.Home;if(home!=null)Events3.DropPods(home,new[]{box});}
   Events3.RemoveSite(r);
  }
  public static void LootSite(E3Record r,Pawn caught){
   var q=Events3.Add(Kind+"Loot",r.map);q.faction=r.faction;
   var site=Events3.MakeSite(q,r.faction,Mathf.Max(250f,StorytellerUtility.DefaultThreatPointsNow(r.map)*.8f),3,8,SiteDays);
   if(site==null){q.done=true;return;}
   Find.LetterStack.ReceiveLetter("审问盗贼",caught.LabelShort+"被抓住后很快就招了：盗贼公会在附近有一处藏宝点。站点已标在世界地图上，"+SiteDays+" 天后消失。",LetterDefOf.PositiveEvent,new LookTargets(site));
  }
  // 藏身处：盗贼一伙守着（威胁点数），被偷的命匣放在中间；藏宝点放银子和奇物。
  public static void Generate(Map map,E3Record r){
   var f=r.faction??ThiefFaction();
   if(!CellFinder.TryFindRandomCellNear(map.Center,map,15,c=>c.Standable(map),out var center))center=map.Center;
   if(f!=null){
    var pts=r.site is Site s?s.parts.FirstOrDefault()?.parms?.threatPoints??300f:300f;
    var pawns=PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,faction=f,points=Mathf.Max(pts,f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)*1.05f),tile=map.Tile}).ToList();
    foreach(var p in pawns)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(center,map,8),map);
    if(pawns.Count>0)LordMaker.MakeNewLord(f,new LordJob_DefendPoint(center),map,pawns);
   }
   var st=Events3.State;
   if(r.kind==Kind && r.thing!=null && st.hold.Contains(r.thing)){st.hold.Remove(r.thing);GenPlace.TryPlaceThing(r.thing,center,map,ThingPlaceMode.Near);}
   if(r.kind==Kind+"Loot")Events3.Drop(map,center,new[]{Events3.Silver(Rand.RangeInclusive(50,100)*10),TowerCrash.MakeWondrous()});
   r.flag=true;
  }
 }
 public class IncidentWorker_E3Thief : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Thief;
  protected override bool CanFire(Map map,IncidentParms parms)=>Thief.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var r=Thief.Start((Map)parms.target);if(r==null)return false;
   SendStandardLetter("命匣窃贼","哨兵说夜里好像有影子在动——有人隐着身形摸进了地图，直奔"+(r.thing as Phylactery)?.soul?.LabelShort+"的命匣！\n\n派人去命匣附近：靠近到 "+Thief.RevealRadius+" 格内就能让潜入者现形。",LetterDefOf.ThreatSmall,parms,new LookTargets(r.thing));
   return true;
  }
 }

 // ==================== 冒险者的天命 ====================
 // 冒险者第一次到 5、10、15 级时各掷一次（50%）：30 天的个人任务。
 //  武职（战士、武僧、圣武士、野蛮人、游侠、游荡者，或没职业但格斗/射击最高）：亲手击杀 8 个敌人。
 //  其他：在职业的主技能上得到 8000 + 等级×400 点经验（法师/术士/邪术师智识、牧师社交、德鲁伊种植、器械师手工，没职业取最高技能）。
 // 完成后三选一：主属性 +1、武艺天赋（或升一阶）、一件沉睡的传承武器。开启功能时已经过了的等级档不补。
 public static class Calling {
  public const string Kind="calling";
  public static readonly int[] Levels={5,10,15};
  public const int Days=30,Kills=8,XpBase=8000,XpPerLevel=400;public const float Chance=.5f;
  static readonly string[] Martial={"LP_Class_Fighter","LP_Class_Monk","LP_Class_Paladin","LP_Class_Barbarian","LP_Class_Ranger","LP_Class_Rogue"};
  public static void Scan(Events3State s){
   if(!Events3.S.e3Calling || !Adventurer.Enabled)return;
   foreach(var map in Find.Maps.Where(m=>m.IsPlayerHome))foreach(var p in Events3.Colonists(map)){
    var h=Adventurer.Get(p);if(h==null)continue;
    foreach(var lv in Levels){
     if(h.level<lv)break;string key=p.thingIDNumber+":"+lv;if(s.callingDone.Contains(key))continue;s.callingDone.Add(key);
     if(s.callingInit && Events3.Roll<Chance && !Events3.Active(Kind).Any(r=>r.a==p))Start(p,lv);
    }
   }
   s.callingInit=true;
  }
  public static bool IsMartial(Pawn p){
   var c=ClassSystem.MainClass(p);if(c!=null)return Martial.Contains(c.defName);
   var sk=p.skills;if(sk==null)return true;var best=sk.skills.Where(x=>!x.TotallyDisabled).OrderByDescending(x=>x.Level).FirstOrDefault();
   return best==null || best.def==SkillDefOf.Melee || best.def==SkillDefOf.Shooting;
  }
  public static SkillDef MainSkill(Pawn p){
   var c=ClassSystem.MainClass(p)?.defName;
   switch(c){
    case "LP_Class_Wizard":case "LP_Class_Sorcerer":case "LP_Class_Warlock":return SkillDefOf.Intellectual;
    case "LP_Class_Cleric":return SkillDefOf.Social;
    case "LP_Class_Druid":return SkillDefOf.Plants;
    case "LP_Class_Artificer":return SkillDefOf.Crafting;
   }
   return p.skills?.skills.Where(x=>!x.TotallyDisabled).OrderByDescending(x=>x.Level).FirstOrDefault()?.def??SkillDefOf.Intellectual;
  }
  public static E3Record Start(Pawn p,int level){
   var r=Events3.Add(Kind,p.MapHeld);r.a=p;r.score=level;r.until=Events3.Now+Days*GenDate.TicksPerDay;
   string task;
   if(IsMartial(p)){r.stage=0;task="30 天内亲手击杀 "+Kills+" 个敌人。";}
   else{r.stage=1;r.skill=MainSkill(p);var sr=p.skills.GetSkill(r.skill);r.val=Growth.Progress(sr);r.val2=XpBase+level*XpPerLevel;task="30 天内在"+r.skill.label+"上得到 "+r.val2.ToString("0")+" 点经验。";}
   Find.LetterStack.ReceiveLetter("天命："+p.LabelShort,p.LabelShort+"到了 "+level+" 级。那天夜里"+p.gender.GetPronoun()+"做了一个很长的梦，醒来时知道自己该去做一件事：\n\n"+task+"\n\n完成后可以三选一：主属性 +1、武艺天赋、一件沉睡的传承武器。",LetterDefOf.PositiveEvent,p);
   return r;
  }
  public static float Progress(E3Record r){if(r.stage==0)return r.count/(float)Kills;var sr=r.a?.skills?.GetSkill(r.skill);return sr==null?0f:Mathf.Clamp01((Growth.Progress(sr)-r.val)/Mathf.Max(1f,r.val2));}
  public static void OnKill(Pawn killer,Pawn victim){foreach(var r in Events3.Active(Kind).Where(x=>x.a==killer && x.stage==0 && !x.flag))r.count++;}
  public static void Tick(E3Record r){
   var p=r.a;if(p==null || p.Dead || p.Faction!=Faction.OfPlayer){r.done=true;return;}
   if(r.flag)return;
   if(Progress(r)>=1f){
    r.flag=true;
    Events3.Ask(r,"天命完成："+p.LabelShort,p.LabelShort+"完成了梦里的那件事。选一样作为回报：",new LookTargets(p));
    return;
   }
   if(Events3.Now>r.until){r.done=true;Messages.Message(p.LabelShort+"的天命期限过了，梦里的声音再也没出现。",p,MessageTypeDefOf.NeutralEvent,false);}
  }
  public static int Primary(Pawn p){var h=Adventurer.Get(p);int a=Adventurer.PrimaryAbility(p);if(a>=0 || h==null)return a;int best=0;for(int i=1;i<6;i++)if(h.scores[i]>h.scores[best])best=i;return best;}
  public static IEnumerable<DiaOption> Options(Letter_E3 l,E3Record r){
   var p=r.a;if(p==null || !r.flag)yield break;var h=Adventurer.Get(p);int a=Primary(p);
   yield return l.Choice("主属性 +1（"+(a>=0?Adventurer.Names[a]:"")+"）",()=>{r.done=true;if(h!=null && a>=0){h.scores[a]++;h.Dirty();}},h==null || a<0?"没有冒险者等级":null);
   yield return l.Choice("武艺天赋（已有则升一阶）",()=>{r.done=true;CombatGifts.Wish(p);},CombatGifts.CannotWish(p));
   yield return l.Choice("一件沉睡的传承武器",()=>{r.done=true;Growth.Drop(Growth.MakeLegacy(),p.MapHeld,p.PositionHeld,p);});
  }
 }

 // ==================== 事件站点 ====================
 public class GenStep_E3Site : GenStep {
  public override int SeedPart=>717300;
  public override void Generate(Map map,GenStepParams parms){
   try{
    var r=Events3.State?.recs.FirstOrDefault(x=>x.site!=null && x.site==map.Parent);if(r==null)return;
    switch(r.kind){
     case FallenParty.Kind:FallenParty.Generate(map,r);break;
     case Thief.Kind:case Thief.Kind+"Loot":Thief.Generate(map,r);break;
     case RivalLich.Kind:RivalLich.Generate(map,r);break;
    }
   }catch(Exception e){Log.Error("[DND边缘大陆] 生成事件站点出错："+e);}
  }
 }
}
