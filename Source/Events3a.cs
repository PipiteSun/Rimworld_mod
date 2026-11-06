using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.117.0 第三批事件 · 第一批：宝箱怪、神祇的乞丐、吟游诗人巡演、化身旅人的银龙、亡魂的遗愿。

 // ==================== 宝箱怪 ====================
 // 殖民地外 15～45 格出现一只镶金宝箱。七成是真宝箱（一件奇物 + 300～600 银，五成再加一张卷轴），三成是宝箱怪。
 // 智识≥8 的人（或法师）花 1 小时能鉴定真假。没鉴定就打开宝箱怪：开箱人被咬一口、晕 5 秒。打死宝箱怪掉 3 颗宝箱怪的牙、一件奇物、一张卷轴和 200～400 银。
 public class Building_MimicChest : Building {
  public bool mimic,identified;
  public override string GetInspectString(){
   var s=base.GetInspectString();string mine=identified?(mimic?"鉴定结果：这是一只宝箱怪！":"鉴定结果：一只真正的宝箱。"):"还没有鉴定过（右键让智识≥8的人鉴定）。";
   return s.NullOrEmpty()?mine:s+"\n"+mine;
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref mimic,"lpMimic");Scribe_Values.Look(ref identified,"lpIdentified");}
 }
 public static class Mimic {
  public const string Kind="mimic";
  public const float MimicChance=.3f;public const int IdentifyIntellect=8,StunTicks=300,BiteDamage=12;
  public static ThingDef ChestDef=>DefDatabase<ThingDef>.GetNamedSilentFail("LP_MimicChest");
  public static PawnKindDef MimicKind=>DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_Mimic");
  public static bool CanFire(Map map)=>Events3.S.e3Mimic && ChestDef!=null && MimicKind!=null && FindSpot(map,out _);
  public static bool FindSpot(Map map,out IntVec3 cell){
   var colony=Events3.Colonists(map).Select(p=>p.Position).ToList();cell=IntVec3.Invalid;if(colony.Count==0)return false;
   var c0=colony[0];
   return CellFinder.TryFindRandomCellNear(c0,map,45,c=>c.Standable(map) && !c.Fogged(map) && c.GetEdifice(map)==null && c.GetFirstItem(map)==null && map.reachability.CanReachColony(c) && colony.All(x=>x.DistanceTo(c)>=15f),out cell);
  }
  public static Building_MimicChest Start(Map map,bool? forceMimic=null){
   if(!FindSpot(map,out var cell))return null;
   var chest=(Building_MimicChest)ThingMaker.MakeThing(ChestDef);chest.mimic=forceMimic??Events3.Roll<MimicChance;
   GenSpawn.Spawn(chest,cell,map);
   var r=Events3.Add(Kind,map);r.thing=chest;
   return chest;
  }
  public static bool CanIdentify(Pawn p)=>p?.skills!=null && (p.skills.GetSkill(SkillDefOf.Intellectual).Level>=IdentifyIntellect || ClassSystem.Level(p,DefDatabase<LP_ClassDef>.GetNamedSilentFail("LP_Class_Wizard"))>0);
  public static IEnumerable<FloatMenuOption> MenuFor(Pawn p,Building_MimicChest chest){
   if(!chest.identified){
    var id=new FloatMenuOption("鉴定宝箱（约1小时）",()=>E3Jobs.Give(p,"LP_E3_IdentifyChest",chest));
    if(!CanIdentify(p))id=new FloatMenuOption("鉴定宝箱（需要智识 "+IdentifyIntellect+" 级或法师）",null);
    yield return id;
   }
   yield return new FloatMenuOption(chest.identified && chest.mimic?"叫醒宝箱怪（让它现形，不会被偷咬）":"打开宝箱",()=>E3Jobs.Give(p,"LP_E3_OpenChest",chest));
  }
  public static void Identify(Pawn p,Building_MimicChest chest){
   if(chest==null || chest.Destroyed)return;chest.identified=true;
   Messages.Message(p.LabelShort+"鉴定了宝箱："+(chest.mimic?"箱盖的木纹在呼吸——这是一只宝箱怪！":"这是一只真正的宝箱。"),chest,chest.mimic?MessageTypeDefOf.ThreatSmall:MessageTypeDefOf.PositiveEvent,false);
  }
  public static void Open(Pawn p,Building_MimicChest chest){
   if(chest==null || !chest.Spawned)return;
   var r=Events3.Active(Kind).FirstOrDefault(x=>x.thing==chest);var map=chest.Map;var cell=chest.Position;
   if(!chest.mimic){
    chest.Destroy();
    var loot=new List<Thing>{TowerCrash.MakeWondrous(),Events3.Silver(Rand.RangeInclusive(30,60)*10)};if(Rand.Chance(.5f))loot.Add(TowerCrash.MakeScroll());
    Events3.Drop(map,cell,loot);Events3.Finish(r);
    Find.LetterStack.ReceiveLetter("宝箱里的东西",p.LabelShort+"打开了宝箱：里面是"+Events3.Labels(loot)+"。",LetterDefOf.PositiveEvent,new LookTargets(cell,map));
    return;
   }
   Reveal(chest,p,!chest.identified,r);
  }
  public static Pawn Reveal(Building_MimicChest chest,Pawn opener,bool surprise,E3Record r=null){
   var map=chest.Map;var cell=chest.Position;chest.Destroy();
   var m=PawnGenerator.GeneratePawn(MimicKind,null);GenSpawn.Spawn(m,cell,map);
   m.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true);
   r=r??Events3.Add(Kind,map);r.a=m;r.thing=null;
   if(surprise && opener!=null && !opener.Dead){
    opener.TakeDamage(new DamageInfo(DamageDefOf.Bite,BiteDamage,1f,-1f,m));
    if(!opener.Dead)opener.stances?.stunner?.StunFor(StunTicks,m,false);
   }
   Find.LetterStack.ReceiveLetter("宝箱怪！",surprise && opener!=null?"箱盖猛地张开，一圈獠牙咬住了"+opener.LabelShort+"！这是一只宝箱怪。\n\n打死它：它吞下的上一位冒险者的遗物还在肚子里。":"宝箱怪被叫醒了，伸出长舌头扑了过来。打死它：它吞下的上一位冒险者的遗物还在肚子里。",LetterDefOf.ThreatSmall,m);
   return m;
  }
  public static void OnDeath(Pawn p,E3KillState st){
   var r=Events3.Active(Kind).FirstOrDefault(x=>x.a==p);if(r==null)return;
   var loot=new List<Thing>{Events3.Make("LP_MimicTooth",3),TowerCrash.MakeWondrous(),TowerCrash.MakeScroll(),Events3.Silver(Rand.RangeInclusive(20,40)*10)};
   Events3.Drop(st.map,st.at,loot);Events3.Finish(r);
   Find.LetterStack.ReceiveLetter("宝箱怪死了","宝箱怪的肚子里吐出了"+Events3.Labels(loot)+"。",LetterDefOf.PositiveEvent,new LookTargets(st.at,st.map));
  }
  public static void Tick(E3Record r){
   if(r.a!=null){if(r.a.Destroyed || !r.a.Spawned && !r.a.Dead)r.done=true;return;}
   if(r.thing==null || r.thing.Destroyed)r.done=true;
  }
 }
 public class IncidentWorker_E3Mimic : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Mimic;
  protected override bool CanFire(Map map,IncidentParms parms)=>Mimic.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=(Map)parms.target;var chest=Mimic.Start(map);if(chest==null)return false;
   SendStandardLetter("无主的宝箱","殖民地外出现了一只镶金的旧宝箱，箱盖上落满灰，看不出是谁丢下的。\n\n右键可以直接打开，也可以先让智识≥"+Mimic.IdentifyIntellect+"的人（或法师）花一小时鉴定——冒险者都知道，不是每只宝箱都是宝箱。",LetterDefOf.NeutralEvent,parms,chest);
   return true;
  }
 }

 // ==================== 神祇的乞丐 ====================
 // 有守护神和圣坛时，一个乞丐到殖民地外讨东西，要什么看守护神：善神要 5 份饭或 3 份药，中立神要 1 件普通以上品质的衣服或武器，恶神要 1 名囚犯或 1 只驯养动物。
 // 右键乞丐施舍；12 小时后乞丐化作光离开：神眷 +15，施舍的人“蒙神一顾”30 天。一天没人理：神眷 -5。打他：神眷 -20，动手的人“神之不悦”15 天。
 public static class Beggar {
  public const string Kind="beggar";
  public const float Reward=15f,Ignored=5f,Struck=20f;
  public static bool CanFire(Map map)=>Events3.S.e3Beggar && Faith.Enabled && FaithUtility.Patron!=null && FaithUtility.Altars(map).Any() && !Events3.Active(Kind).Any() && Events3.WayfarersDef!=null;
  public static string DemandText(LP_DeityDef god)=>god==null?"":god.Good?"5 份饭或 3 份药":god.Evil?"1 名囚犯或 1 只驯养的动物":"1 件普通以上品质的衣服或武器";
  static bool IsMeal(Thing t)=>t.def.ingestible!=null && t.def.ingestible.IsMeal;
  static bool IsMed(Thing t)=>t.def.IsMedicine;
  static bool IsCraft(Thing t)=>(t.def.IsApparel || t.def.IsWeapon) && t.TryGetQuality(out var q) && q>=QualityCategory.Normal;
  public static Pawn Prisoner(Map map)=>map.mapPawns.PrisonersOfColonySpawned.FirstOrDefault();
  public static Pawn Animal(Map map)=>map.mapPawns.SpawnedColonyAnimals.FirstOrDefault(a=>!a.Dead);
  public static string CannotPay(Map map,LP_DeityDef god){
   if(god==null)return "没有守护神";
   if(god.Good)return Events3.Count(map,IsMeal)>=5 || Events3.Count(map,IsMed)>=3?null:"饭不到 5 份，药也不到 3 份";
   if(god.Evil)return Prisoner(map)!=null || Animal(map)!=null?null:"没有囚犯，也没有驯养的动物";
   return Events3.Owned(map,IsCraft).Any()?null:"没有普通以上品质的衣服或武器";
  }
  public static bool Pay(Map map,LP_DeityDef god,out string what){
   what=null;if(CannotPay(map,god)!=null)return false;
   if(god.Good){if(Events3.Take(map,IsMeal,5)){what="5 份饭";return true;}if(Events3.Take(map,IsMed,3)){what="3 份药";return true;}return false;}
   if(god.Evil){var p=Prisoner(map)??Animal(map);if(p==null)return false;what=p.LabelShort;Events3.Vanish(p);return true;}
   var t=Events3.Owned(map,IsCraft).OrderBy(x=>x.MarketValue).FirstOrDefault();if(t==null)return false;what=t.LabelCap;t.SplitOff(1).Destroy();return true;
  }
  public static E3Record Start(Map map){
   var god=FaithUtility.Patron;var f=Events3.Wayfarers();if(god==null || f==null)return null;
   var p=Events3.MakePawn(PawnKindDefOf.Villager,f,map,Rand.Range(55f,75f));if(p==null)return null;
   p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();
   var spot=FaithUtility.Altars(map).First().Position;
   var lord=Events3.Visit(map,new List<Pawn>{p},f,"e3",CellFinder.RandomClosewalkCellNear(spot,map,6));if(lord==null)return null;
   var r=Events3.Add(Kind,map);r.a=p;r.lord=lord;r.god=god;r.until=Events3.Now+GenDate.TicksPerDay;
   return r;
  }
  public static IEnumerable<FloatMenuOption> MenuFor(Pawn p,E3Record r){
   if(r.stage!=0)yield break;
   var why=CannotPay(p.Map,r.god);
   yield return why==null?new FloatMenuOption("施舍给乞丐（"+DemandText(r.god)+"）",()=>E3Jobs.Give(p,"LP_E3_Offer",r.a)):new FloatMenuOption("施舍给乞丐："+why,null);
  }
  public static void Offer(Pawn by,Pawn beggar){
   var r=Events3.Of(beggar);if(r==null || r.kind!=Kind || r.stage!=0)return;
   if(!Pay(by.Map,r.god,out var what)){Messages.Message("东西不够了："+CannotPay(by.Map,r.god)+"。",beggar,MessageTypeDefOf.RejectInput,false);return;}
   r.stage=1;r.b=by;r.next=Events3.Now+GenDate.TicksPerDay/2;
   Messages.Message(beggar.LabelShort+"收下了"+what+"，向"+by.LabelShort+"深深鞠了一躬。",beggar,MessageTypeDefOf.PositiveEvent,false);
  }
  public static void Tick(E3Record r){
   var p=r.a;if(p==null || p.Dead || p.Destroyed || !p.Spawned){r.done=true;return;}
   if(r.stage==0 && Events3.Now>r.until){
    r.done=true;Faith.Gain(r.god,-Ignored,"无视了讨饭的乞丐");Events3.Leave(r.lord);
    Messages.Message("乞丐等了一整天，没人理他，蹒跚着走了。（"+r.god.label+"神眷 -"+Ignored+"）",p,MessageTypeDefOf.NegativeEvent,false);
    return;
   }
   if(r.stage==1 && Events3.Now>=r.next)Reveal(r);
  }
  public static void Reveal(E3Record r){
   r.done=true;r.stage=2;var p=r.a;var giver=r.b;
   Faith.Gain(r.god,Reward,"施舍了神的化身");
   if(giver!=null && !giver.Dead)Items.Give(giver,"LP_E3_DivineGlance");
   Find.LetterStack.ReceiveLetter("神的化身",p.LabelShort+"站直了身子，破衣烂衫化成了光："+r.god.FullLabel+"借乞丐的身体来看看殖民地。\n\n"+r.god.label+"神眷 +"+(Reward*Faith.GainFactor).ToString("0.#")+(giver!=null?"，"+giver.LabelShort+"得到“蒙神一顾”（30 天：神恩强度 +0.15，免疫力增长 +10%，崩溃阈值 -5%）":"")+"。",LetterDefOf.PositiveEvent,giver!=null?new LookTargets(giver):new LookTargets(p.Position,p.Map));
   Events3.Vanish(p,true);
  }
  public static void OnHarmed(E3Record r,Pawn attacker){
   if(r.done)return;r.done=true;var p=r.a;
   Faith.Gain(r.god,-Struck,"殴打了神的化身");if(attacker!=null)Items.Give(attacker,"LP_E3_DivineDispleasure");
   Find.LetterStack.ReceiveLetter("神之不悦","挨打的乞丐抬起头，眼里是"+r.god.FullLabel+"的光。他什么也没说，化作一阵风散了。\n\n"+r.god.label+"神眷 -"+Struck+(attacker!=null?"，"+attacker.LabelShort+"得到“神之不悦”（15 天）":"")+"。",LetterDefOf.NegativeEvent,attacker);
   Events3.Vanish(p,true);
  }
 }
 public class IncidentWorker_E3Beggar : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Beggar;
  protected override bool CanFire(Map map,IncidentParms parms)=>Beggar.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var r=Beggar.Start((Map)parms.target);if(r==null)return false;
   SendStandardLetter("讨饭的乞丐","一个衣衫褴褛的乞丐走到"+r.god.label+"的圣坛附近，坐在地上讨东西：他想要"+Beggar.DemandText(r.god)+"。\n\n右键乞丐可以施舍。他会等一天。",LetterDefOf.NeutralEvent,parms,r.a);
   return true;
  }
 }

 // ==================== 吟游诗人巡演 ====================
 // 2～4 名吟游诗人来访，傍晚（17～23 点）演一场：全殖民地心情 +6（3 天），每人冒险者经验 40。
 // 歌谣：最近一件功绩（第三批事件记下的，或殖民者击杀数 ≥30），非敌对派系好感 +5、殖民者“歌谣里的英雄”+3（15 天）；
 // 殖民地有巫妖或 3 名以上奴隶，就被写成讽刺歌：猎巫狂热分子 -10，塞伊 +5。
 public static class Bards {
  public const string Kind="bards";
  public const int MinWealth=15000,ShowXp=40,BalladGoodwill=5,KillDeed=30;
  public static bool CanFire(Map map)=>Events3.S.e3Bard && Events3.WayfarersDef!=null && map.wealthWatcher.WealthTotal>=MinWealth && !Events3.Active(Kind).Any();
  public static E3Record Start(Map map){
   var f=Events3.Wayfarers();if(f==null)return null;
   var list=new List<Pawn>();int n=Rand.RangeInclusive(2,4);for(int i=0;i<n;i++)list.Add(Events3.MakePawn(PawnKindDefOf.Villager,f,map,Rand.Range(18f,40f)));
   var lord=Events3.Visit(map,list,f,"e3");if(lord==null)return null;
   var r=Events3.Add(Kind,map);r.pawns=list.Where(p=>p!=null).ToList();r.lord=lord;r.until=Events3.Now+(int)(1.5f*GenDate.TicksPerDay);
   return r;
  }
  public static string Deed(Map map){
   var s=Events3.State;if(s!=null && s.deeds.Count>0)return s.deeds[s.deeds.Count-1];
   var best=Events3.Colonists(map).Select(p=>(p,n:(int)(p.records.GetValue(RecordDefOf.KillsHumanlikes)+p.records.GetValue(RecordDefOf.KillsMechanoids)))).OrderByDescending(x=>x.n).FirstOrDefault();
   return best.p!=null && best.n>=KillDeed?best.p.LabelShort+"斩敌"+best.n+"人":null;
  }
  public static string Notorious(Map map){
   var lich=Events3.Colonists(map).FirstOrDefault(Items.IsLich);if(lich!=null)return lich.LabelShort+"是一名巫妖";
   int slaves=map.mapPawns.SlavesOfColonySpawned.Count;return slaves>=3?"殖民地蓄养了"+slaves+"名奴隶":null;
  }
  public static void Perform(E3Record r){
   var map=r.map;r.stage=1;r.next=Events3.Now+4*GenDate.TicksPerHour;
   Events3.ThoughtAll(map,"LP_E3_BardShow");foreach(var c in Events3.Colonists(map))Adventurer.GainXP(c,ShowXp);
   string bad=Notorious(map),deed=Deed(map);
   if(bad!=null){
    var z=Factions.Of(Factions.ZealotsDef);if(z!=null)Factions.ChangeGoodwillExact(z,-10,"吟游诗人的讽刺歌");
    var t=Factions.Hostile;if(t!=null)Factions.ChangeGoodwillExact(t,5,"吟游诗人的讽刺歌");
    Find.LetterStack.ReceiveLetter("讽刺歌","吟游诗人演得很精彩，大家都很开心（心情 +6）。可他们最后唱的那首歌是讽刺殖民地的："+bad+"。\n\n这首歌会传开：猎巫狂热分子好感 -10，塞伊远征军好感 +5。",LetterDefOf.NeutralEvent,new LookTargets(r.pawns.Cast<Thing>()));
    return;
   }
   if(deed!=null){
    foreach(var f in Find.FactionManager.AllFactionsVisible.Where(f=>!f.IsPlayer && f.HasGoodwill && !f.HostileTo(Faction.OfPlayer) && !f.def.hidden).ToList())Factions.ChangeGoodwillExact(f,BalladGoodwill,"吟游诗人的歌谣");
    Events3.ThoughtAll(map,"LP_E3_BalladHero");
    Find.LetterStack.ReceiveLetter("新的歌谣","吟游诗人演了一整晚（心情 +6，每人冒险者经验 "+ShowXp+"）。压轴的是一首新写的歌，唱的是“"+deed+"”。\n\n歌谣会传遍费伦：非敌对派系好感 +"+BalladGoodwill+"，殖民者“歌谣里的英雄”（+3，15 天）。",LetterDefOf.PositiveEvent,new LookTargets(r.pawns.Cast<Thing>()));
    return;
   }
   Find.LetterStack.ReceiveLetter("吟游诗人的演出","吟游诗人演了一整晚，大家都很开心（心情 +6，每人冒险者经验 "+ShowXp+"）。殖民地还没有什么值得写进歌里的事——下次吧。",LetterDefOf.PositiveEvent,new LookTargets(r.pawns.Cast<Thing>()));
  }
  public static void Tick(E3Record r){
   if(r.pawns.All(p=>p==null || p.Dead || !p.Spawned)){r.done=true;return;}
   int hour=GenLocalDate.HourOfDay(r.map);
   if(r.stage==0){
    if(Events3.Now-r.start>=3*GenDate.TicksPerHour && hour>=17 && hour<=23){Perform(r);return;}
    if(Events3.Now>r.until){Events3.Leave(r.lord);r.done=true;}
    return;
   }
   if(Events3.Now>=r.next){Events3.Leave(r.lord);r.done=true;}
  }
  public static void OnHarmed(E3Record r){if(r.done)return;r.done=true;Events3.Leave(r.lord);Messages.Message("吟游诗人被打了，收起乐器匆匆离开。",new LookTargets(r.pawns.Cast<Thing>()),MessageTypeDefOf.NegativeEvent,false);}
 }
 public class IncidentWorker_E3Bards : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Bard;
  protected override bool CanFire(Map map,IncidentParms parms)=>Bards.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var r=Bards.Start((Map)parms.target);if(r==null)return false;
   SendStandardLetter("吟游诗人巡演",r.pawns.Count+"名吟游诗人背着鲁特琴和鼓来到殖民地外，说今晚要在这里演一场。\n\n他们会把殖民地最了不起的事写成歌——也可能是最丢人的。",LetterDefOf.PositiveEvent,parms,new LookTargets(r.pawns.Cast<Thing>()));
   return true;
  }
 }

 // ==================== 化身旅人的银龙 ====================
 // 一位老旅人借宿 3 天，每天按招待打分（-1～3）：精致以上的饭 +1（没饭 -1，自动从库存里拿一份）、有空着的好卧室（印象≥40）+1、身边有社交≥6 的人 +1。
 // 第 3 天走时现出银龙真身：≥6 分留下 3 片银龙鳞，陪他最久的人得到“银龙之友”，≥8 分再有 5% 留下一枚龙蛋；3～5 分同盟好感 +5。
 // 被打或被关起来：冰息喷在动手的人身上（冻伤 15），同盟好感 -15。
 public static class SilverGuest {
  public const string Kind="silver";
  public const int Days=3,Great=6,Superb=8,FrostDamage=15,GoodwillGood=5,GoodwillBad=-15;public const float EggChance=.05f,RoomImpressive=40f;
  public static bool CanFire(Map map)=>Events3.S.e3Silver && Events3.WayfarersDef!=null && !Events3.Active(Kind).Any();
  public static E3Record Start(Map map){
   var f=Events3.Wayfarers();if(f==null)return null;
   var p=Events3.MakePawn(PawnKindDefOf.Villager,f,map,Rand.Range(70f,90f));if(p==null)return null;
   var lord=Events3.Visit(map,new List<Pawn>{p},f,"e3");if(lord==null)return null;
   var r=Events3.Add(Kind,map);r.a=p;r.lord=lord;r.next=Events3.Now+GenDate.TicksPerDay;
   return r;
  }
  static bool Fine(Thing t)=>t.def.ingestible!=null && t.def.ingestible.IsMeal && t.def.ingestible.preferability>=FoodPreferability.MealFine;
  static bool Meal(Thing t)=>t.def.ingestible!=null && t.def.ingestible.IsMeal;
  public static bool GoodRoom(Map map)=>map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>().Any(b=>!b.Medical && !b.ForPrisoners && b.def.building.bed_humanlike && b.OwnersForReading.Count==0 && (b.GetRoom()?.GetStat(RoomStatDefOf.Impressiveness)??0f)>=RoomImpressive);
  public static int ScoreDay(E3Record r,out string why){
   var map=r.map;int s=0;var parts=new List<string>();
   if(Events3.Take(map,Fine,1)){s++;parts.Add("吃到了精致的饭 +1");}else if(Events3.Take(map,Meal,1))parts.Add("吃到了普通的饭 +0");else{s--;parts.Add("没饭吃 -1");}
   if(GoodRoom(map)){s++;parts.Add("有空着的好卧室 +1");}else parts.Add("没有像样的空卧室 +0");
   bool talk=Events3.Colonists(map).Any(c=>c.skills!=null && c.skills.GetSkill(SkillDefOf.Social).Level>=6 && c.Position.DistanceTo(r.a.Position)<=12f);
   if(talk){s++;parts.Add("有人陪他聊天 +1");}else parts.Add("没人陪他 +0");
   why=string.Join("，",parts);return s;
  }
  public static void Tick(E3Record r){
   var p=r.a;if(p==null || p.Dead || p.Destroyed){r.done=true;return;}
   if(p.IsPrisonerOfColony){OnHarmed(r,null,"把他关了起来");return;}
   if(!p.Spawned){r.done=true;return;}
   foreach(var c in Events3.Colonists(r.map))if(c.Position.DistanceTo(p.Position)<=6f && c.Awake()){r.tally.TryGetValue(c,out int n);r.tally[c]=n+1;}
   if(Events3.Now<r.next)return;
   r.score+=ScoreDay(r,out var why);r.count++;r.next+=GenDate.TicksPerDay;
   Messages.Message("老旅人对第 "+r.count+" 天的招待："+why+"。",p,MessageTypeDefOf.NeutralEvent,false);
   if(r.count>=Days)Reveal(r);
  }
  public static Pawn Friend(E3Record r)=>r.tally.Where(kv=>kv.Key!=null && !kv.Key.Dead && kv.Key.Faction==Faction.OfPlayer).OrderByDescending(kv=>kv.Value).Select(kv=>kv.Key).FirstOrDefault();
  public static void Reveal(E3Record r){
   r.done=true;var p=r.a;var map=r.map;var cell=p.Position;var friend=Friend(r);
   string text="老旅人走到殖民地外，回头笑了笑：他的身影拉长、展开，化成一条银白的巨龙——一条喜欢扮成凡人四处走走的银龙。\n\n招待得分："+r.score+" / 9。\n\n";
   var look=new LookTargets(cell,map);
   if(r.score>=Great){
    var loot=Events3.Drop(map,cell,new[]{Events3.Make("LP_SilverDragonScale",3)});
    text+="它留下了"+Events3.Labels(loot)+"。";
    if(friend!=null){Items.Give(friend,"LP_E3_SilverFriend");text+="\n\n"+friend.LabelShort+"陪它聊得最多，成了“银龙之友”（耐寒、心情更稳）。";}
    if(r.score>=Superb && Events3.Roll<EggChance){var egg=Events3.Drop(map,cell,new[]{Events3.Make("LP_ForceDragonEgg")});if(egg.Count>0)text+="\n\n床上还多了一枚龙蛋！";}
    Events3.State?.Deed("招待过化身旅人的银龙");
   }else if(r.score>=3){var c=Factions.Colony;if(c!=null)Factions.ChangeGoodwillExact(c,GoodwillGood,"招待了银龙");text+="它道了谢，振翅飞走了。费伦拓荒同盟听说了这件事：好感 +"+GoodwillGood+"。";}
   else text+="它没说什么，振翅飞走了。";
   Find.LetterStack.ReceiveLetter("银龙的化身",text,LetterDefOf.PositiveEvent,friend!=null?new LookTargets(friend):look);
   Events3.Vanish(p,true);
  }
  public static void OnHarmed(E3Record r,Pawn attacker,string why){
   if(r.done)return;r.done=true;var p=r.a;
   if(attacker!=null && !attacker.Dead)attacker.TakeDamage(new DamageInfo(DamageDefOf.Frostbite,FrostDamage,1f,-1f,p,attacker.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Frostbite,BodyPartHeight.Undefined,BodyPartDepth.Outside)));
   var c=Factions.Colony;if(c!=null)Factions.ChangeGoodwillExact(c,GoodwillBad,"冒犯了银龙");
   Find.LetterStack.ReceiveLetter("被冒犯的银龙","殖民者"+why+"。老旅人的眼睛变成了银色的竖瞳——他现出银龙的原形，"+(attacker!=null?"朝"+attacker.LabelShort+"喷了一口冰息，":"")+"振翅飞走了。\n\n费伦拓荒同盟好感 "+GoodwillBad+"。",LetterDefOf.NegativeEvent,attacker!=null?new LookTargets(attacker):LookTargets.Invalid);
   Events3.Vanish(p,true);
  }
 }
 public class IncidentWorker_E3Silver : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Silver;
  protected override bool CanFire(Map map,IncidentParms parms)=>SilverGuest.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var r=SilverGuest.Start((Map)parms.target);if(r==null)return false;
   SendStandardLetter("借宿的老旅人","一位白发老旅人请求在殖民地外借宿三天。看上去很普通。\n\n（他住着的这几天，好饭、空着的好卧室、有人陪他聊天都会让他满意——饭会自动从库存里拿。）",LetterDefOf.NeutralEvent,parms,r.a);
   return true;
  }
 }

 // ==================== 亡魂的遗愿 ====================
 // 死去 3～15 天的殖民者（尸体或坟墓在殖民地地图上）的亡魂托付一件事，三选一（看哪些办得到）：
 //  安葬：10 天内葬进坟墓；照顾亲人：15 天里那位亲人心情一直不低于 20%；报仇：20 天内杀掉杀死他的派系 5 人。
 // 完成：受托人得到死者最高技能 50% 的经验（最多 40000），全殖民地“亡魂安息”+5。没完成或不理会：“闹鬼”-3。巫妖可以把亡魂收进命匣（命匣经验 +30% 一级，全殖民地 -6）。
 public static class GhostWish {
  public const string Kind="ghost";
  public const int MinDays=3,MaxDays=15,BuryDays=10,ComfortDays=15,AvengeDays=20,AvengeKills=5,MaxXp=40000;
  public const float Share=.5f,ComfortMood=.2f,LichFeed=.3f;
  static readonly string[] TaskNames={"安葬","照顾亲人","报仇"};
  public static IEnumerable<Corpse> Corpses(Map map){
   foreach(var c in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>())yield return c;
   foreach(var g in map.listerBuildings.allBuildingsColonist.OfType<Building_Grave>())if(g.Corpse!=null)yield return g.Corpse;
  }
  public static bool Eligible(Corpse c){
   var p=c?.InnerPawn;if(p==null || p.Faction!=Faction.OfPlayer || !p.RaceProps.Humanlike || Necro.Lich(p)!=null)return false;
   int days=c.Age/GenDate.TicksPerDay;if(days<MinDays || days>MaxDays)return false;
   return Events3.State!=null && !Events3.State.callingDone.Contains("ghost:"+p.thingIDNumber);
  }
  public static bool CanFire(Map map)=>Events3.S.e3Ghost && Events3.Colonists(map).Count>0 && Corpses(map).Any(Eligible);
  public static List<Pawn> Kin(Pawn dead,Map map)=>dead.relations==null?new List<Pawn>():dead.relations.DirectRelations.Select(x=>x.otherPawn).Where(o=>o!=null && !o.Dead && o.Spawned && o.Map==map && o.IsColonist).Distinct().ToList();
  public static E3Record Start(Map map,int forceTask=-1){
   var corpse=Corpses(map).Where(Eligible).RandomElementWithFallback();if(corpse==null)return null;
   var dead=corpse.InnerPawn;var colonists=Events3.Colonists(map);if(colonists.Count==0)return null;
   var kin=Kin(dead,map);
   var trustee=colonists.OrderByDescending(c=>(kin.Contains(c)?1000:0)+(c.relations?.OpinionOf(dead)??0)).First();
   var tasks=new List<int>();
   if(!(corpse.ParentHolder is Building_Grave))tasks.Add(0);
   if(kin.Any(k=>k!=trustee) || kin.Count>0)tasks.Add(1);
   Events3.State.killers.TryGetValue(dead.thingIDNumber,out var killer);
   if(killer!=null && !killer.defeated && killer.HostileTo(Faction.OfPlayer))tasks.Add(2);
   if(tasks.Count==0)tasks.Add(1);
   int task=forceTask>=0?forceTask:tasks.RandomElement();
   var r=Events3.Add(Kind,map);r.thing=corpse;r.text=dead.LabelShort;r.a=trustee;r.stage=task;r.faction=task==2?killer:null;
   if(task==1){var k=kin.FirstOrDefault(x=>x!=trustee)??kin.FirstOrDefault()??trustee;r.pawns.Add(k);}
   var best=dead.skills?.skills.Where(s=>!s.TotallyDisabled && trustee.skills?.GetSkill(s.def)?.TotallyDisabled==false).OrderByDescending(s=>Growth.Progress(s)).FirstOrDefault();
   if(best!=null){r.skill=best.def;r.val=Mathf.Min(MaxXp,Growth.Progress(best)*Share);}
   Events3.State.callingDone.Add("ghost:"+dead.thingIDNumber);
   Events3.Ask(r,"亡魂的遗愿",dead.LabelShort+"的亡魂在夜里出现在"+trustee.LabelShort+"的床边，半透明的手按在胸口，求"+trustee.LabelShort+"替他办最后一件事：\n\n"+TaskText(r)+"\n\n办成了，"+trustee.LabelShort+"能继承他一半的"+(r.skill!=null?r.skill.label+"经验（约 "+r.val.ToString("0")+"）":"本事")+"。",new LookTargets(trustee));
   return r;
  }
  public static string TaskText(E3Record r){
   switch(r.stage){
    case 0:return "把"+r.text+"的遗体葬进坟墓（"+BuryDays+" 天内）。";
    case 1:return "照顾"+(r.pawns.FirstOrDefault()?.LabelShort??"他的亲人")+"："+ComfortDays+" 天里心情一直不低于 "+(ComfortMood*100).ToString("0")+"%。";
    default:return "向"+r.faction?.Name+"报仇："+AvengeDays+" 天内杀掉他们 "+AvengeKills+" 个人。";
   }
  }
  public static Pawn LichWithBox(Map map)=>Events3.Colonists(map).FirstOrDefault(p=>Necro.Lich(p)?.box!=null && !Necro.Lich(p).box.Destroyed);
  public static IEnumerable<DiaOption> Options(Letter_E3 l,E3Record r){
   yield return l.Choice("答应（"+TaskNames[Mathf.Clamp(r.stage,0,2)]+"）",()=>Accept(r));
   var lich=LichWithBox(r.map);
   if(lich!=null)yield return l.Choice("让"+lich.LabelShort+"把亡魂收进命匣（命匣经验 +"+(LichFeed*100).ToString("0")+"% 一级，殖民者 -6 心情）",()=>Bind(r,lich));
   yield return l.Choice("不理会（会闹鬼）",()=>Haunt(r,"没人理会"+r.text+"的亡魂"));
  }
  public static void Accept(E3Record r){
   r.answered=true;r.next=Events3.Now+GenDate.TicksPerDay;
   r.until=Events3.Now+(r.stage==0?BuryDays:r.stage==1?ComfortDays:AvengeDays)*GenDate.TicksPerDay;
   Messages.Message(r.a?.LabelShort+"答应了"+r.text+"的亡魂："+TaskText(r),r.a,MessageTypeDefOf.NeutralEvent,false);
  }
  public static void Bind(E3Record r,Pawn lich){
   r.answered=true;r.done=true;var box=Necro.Lich(lich)?.box;
   if(box!=null)box.Feed(Necro.Requirement(box.level)*LichFeed);
   Events3.ThoughtAll(r.map,"LP_E3_SoulBound");
   Find.LetterStack.ReceiveLetter("收进命匣",lich.LabelShort+"张开手，"+r.text+"的亡魂尖叫着被吸进了命匣。命匣经验增加了；殖民者们看着这一幕，心里发凉（-6）。",LetterDefOf.NeutralEvent,lich);
  }
  public static void Haunt(E3Record r,string why){
   r.answered=true;r.done=true;Events3.ThoughtAll(r.map,"LP_E3_Haunted");
   Find.LetterStack.ReceiveLetter("闹鬼",why+"：夜里总有人听见脚步声和叹息（全殖民地“闹鬼”-3，10 天）。",LetterDefOf.NegativeEvent);
  }
  public static void Complete(E3Record r){
   r.done=true;var t=r.a;
   if(t!=null && !t.Dead && r.skill!=null){var s=t.skills?.GetSkill(r.skill);if(s!=null && !s.TotallyDisabled)s.Learn(r.val,true);}
   Events3.ThoughtAll(r.map,"LP_E3_GhostRest");
   Find.LetterStack.ReceiveLetter("亡魂安息",r.text+"的心愿了了。亡魂对"+t?.LabelShort+"笑了笑，散成一片微光。\n\n"+(r.skill!=null?t?.LabelShort+"得到"+r.val.ToString("0")+"点"+r.skill.label+"经验。":"")+"全殖民地“亡魂安息”+5。",LetterDefOf.PositiveEvent,t);
  }
  public static void OnKill(Pawn victim){foreach(var r in Events3.Active(Kind).Where(x=>x.answered && x.stage==2 && x.faction!=null && victim.Faction==x.faction))r.count++;}
  public static void Tick(E3Record r){
   if(!r.answered){if(Events3.Now-r.start>2*GenDate.TicksPerDay)Haunt(r,"没人回应"+r.text+"的亡魂");return;}
   if(r.a==null || r.a.Dead){Haunt(r,"受托的人也死了，"+r.text+"的心愿没人能完成");return;}
   switch(r.stage){
    case 0:
     if(r.thing!=null && r.thing.ParentHolder is Building_Grave){Complete(r);return;}
     if(r.thing==null || r.thing.Destroyed){Haunt(r,r.text+"的遗体没了，没能入土");return;}
     break;
    case 1:
     var k=r.pawns.FirstOrDefault();if(k==null || k.Dead){Haunt(r,r.text+"想照顾的人也不在了");return;}
     if(k.needs?.mood!=null && k.needs.mood.CurLevel<ComfortMood){Haunt(r,k.LabelShort+"太难过了（心情低于 "+(ComfortMood*100).ToString("0")+"%），"+r.text+"的亡魂没法安心");return;}
     if(Events3.Now>=r.until){Complete(r);return;}
     return;
    case 2:
     if(r.count>=AvengeKills){Complete(r);return;}
     break;
   }
   if(Events3.Now>r.until)Haunt(r,TaskNames[Mathf.Clamp(r.stage,0,2)]+"没能按时完成");
  }
 }
 public class IncidentWorker_E3Ghost : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Ghost;
  protected override bool CanFire(Map map,IncidentParms parms)=>GhostWish.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms)=>GhostWish.Start((Map)parms.target)!=null;
 }
}
