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
 // 1.117.0 第三批事件 · 第三批：骑士比武大会、敌对巫妖的战书、树人苏醒、塔拉斯克苏醒。

 // ==================== 骑士比武大会 ====================
 // 同盟不敌对时发来邀请。参加：剑术、长枪、射箭、摔角各自动挑殖民地里最合适的人（可以重复），技能 + 两项属性调整值 + d20，
 // 对手 12 + d20（每过 60 天 +1，最多 +6）。三成几率某一项的对手是塞伊的“神秘骑士”（+4，打败他塞伊好感 -10）。
 // 每赢一项：奖杯 + 冒险者经验 300 + 同盟好感 +5；长枪、摔角输了受点钝伤。赢三项以上：表现最好的人“比武冠军”+10（15 天），同盟好感再 +10。
 // （不用真的派商队过去：结果在信里直接算出来。）
 public static class Tourney {
  public const string Kind="tourney";
  public const int WinXp=300,WinGoodwill=5,ChampionGoodwill=10,ThayPenalty=-10,ThayBonus=4,LossDamage=6;public const float ThayChance=.3f;
  public class Bout{public string name;public SkillDef skill;public int a1,a2;public bool rough;public Bout(string n,SkillDef s,int x,int y,bool r){name=n;skill=s;a1=x;a2=y;rough=r;}}
  public static List<Bout> Bouts=>new List<Bout>{new Bout("剑术",SkillDefOf.Melee,0,1,false),new Bout("长枪",SkillDefOf.Melee,0,2,true),new Bout("射箭",SkillDefOf.Shooting,1,4,false),new Bout("摔角",SkillDefOf.Melee,0,2,true)};
  public static bool CanFire(Map map){var c=Factions.Colony;return Events3.S.e3Tourney && c!=null && !c.HostileTo(Faction.OfPlayer) && Events3.Colonists(map).Count(p=>!p.WorkTagIsDisabled(WorkTags.Violent))>=1 && !Events3.Active(Kind).Any();}
  public static E3Record Start(Map map){
   var r=Events3.Add(Kind,map);r.until=Events3.Now+3*GenDate.TicksPerDay;
   Events3.Ask(r,"同盟的比武大会","费伦拓荒同盟要办一场比武大会，邀请殖民地参加：剑术、长枪、射箭、摔角，每项派一人。\n\n每赢一项得一座奖杯、"+WinXp+" 点冒险者经验和同盟好感 +"+WinGoodwill+"；赢三项以上能拿“比武冠军”。长枪和摔角输了会挨几下。",LookTargets.Invalid,Factions.Colony);
   return r;
  }
  public static IEnumerable<DiaOption> Options(Letter_E3 l,E3Record r){
   yield return l.Choice("参加（每项自动派最合适的人）",()=>Resolve(r));
   yield return l.Choice("不参加",()=>{r.answered=true;r.done=true;});
  }
  public static int Mod(Pawn p,int a){int s=Adventurer.Score(p,a);if(s<=0)s=10;return (s-10)/2;}
  public static int Base(Pawn p,Bout b){var sk=p.skills?.GetSkill(b.skill);int lv=sk==null || sk.TotallyDisabled?0:sk.Level;if(b.name=="摔角")lv/=2;return lv+Mod(p,b.a1)+Mod(p,b.a2);}
  public static string Resolve(E3Record r){
   r.answered=true;r.done=true;var map=r.map??Events3.Home;var c=Factions.Colony;
   var fighters=Events3.Colonists(map).Where(p=>!p.WorkTagIsDisabled(WorkTags.Violent) && !p.Downed).ToList();if(fighters.Count==0)return null;
   int bonus=Mathf.Min(6,GenDate.DaysPassed/60);int thay=Events3.Roll<ThayChance?Rand.Range(0,4):-1;
   var lines=new List<string>();var wins=new Dictionary<Pawn,int>();int total=0;var trophies=new List<Thing>();
   var bouts=Bouts;
   for(int i=0;i<bouts.Count;i++){
    var b=bouts[i];var p=fighters.OrderByDescending(x=>Base(x,b)).First();
    int ours=Base(p,b)+Rand.RangeInclusive(1,20),theirs=12+bonus+Rand.RangeInclusive(1,20)+(i==thay?ThayBonus:0);
    bool won=ours>=theirs;
    if(won){
     total++;wins.TryGetValue(p,out int w);wins[p]=w+1;Adventurer.GainXP(p,WinXp);if(c!=null)Factions.ChangeGoodwillExact(c,WinGoodwill,"比武大会得胜");
     var t=Events3.Make("LP_TourneyTrophy");if(t!=null)trophies.Add(t);
     if(i==thay){var h=Factions.Hostile;if(h!=null)Factions.ChangeGoodwillExact(h,ThayPenalty,"在比武中打败了塞伊的神秘骑士");}
    }else if(b.rough && !p.Dead){var part=p.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Blunt,BodyPartHeight.Undefined,BodyPartDepth.Outside);if(part!=null)p.TakeDamage(new DamageInfo(DamageDefOf.Blunt,LossDamage,0f,-1f,null,part));}
    lines.Add("· "+b.name+"："+p.LabelShort+" "+ours+" 对 "+(i==thay?"塞伊的神秘骑士":"对手")+" "+theirs+"，"+(won?"胜":"负"));
   }
   if(trophies.Count>0)Events3.DropPods(map,trophies);
   string tail="";
   if(total>=3){var champ=wins.OrderByDescending(kv=>kv.Value).First().Key;Events3.Thought(champ,"LP_E3_Champion");if(c!=null)Factions.ChangeGoodwillExact(c,ChampionGoodwill,"比武大会夺冠");Events3.State?.Deed("在同盟比武大会上夺冠");tail="\n\n赢了 "+total+" 项："+champ.LabelShort+"成了“比武冠军”（+10，15 天），同盟好感再 +"+ChampionGoodwill+"。";}
   string text="比武大会的结果：\n\n"+string.Join("\n",lines)+"\n\n赢了 "+total+" 项"+(trophies.Count>0?"，奖杯已经空投到殖民地":"")+"。"+(thay>=0?"\n\n塞伊派来的神秘骑士在“"+bouts[thay].name+"”上场。":"")+tail;
   Find.LetterStack.ReceiveLetter("比武大会",text,total>0?LetterDefOf.PositiveEvent:LetterDefOf.NeutralEvent);
   return text;
  }
 }
 public class IncidentWorker_E3Tourney : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Tourney;
  protected override bool CanFire(Map map,IncidentParms parms)=>Tourney.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms)=>Tourney.Start((Map)parms.target)!=null;
 }

 // ==================== 敌对巫妖的战书 ====================
 // 殖民地有命匣 5 级以上的巫妖时，一名塞伊巫妖下战书。三选一：
 //  法术决斗：命匣等级 + 智识/2 + d20 对 命匣等级 + 4 + d20。赢了命匣得一整级的经验、一卷优秀的死灵学派秘术残卷；输了命匣当前经验 -25%。
 //  命匣狩猎：世界地图上出现他的塔（20 天），塞伊的部队和骷髅守着；打下来命匣得 1.5 级经验和秘术残卷。
 //  拒绝：15 天、30 天后各来一次塞伊袭击。
 public static class RivalLich {
  public const string Kind="rival";
  public const int MinLevel=5,SiteDays=20,RivalBonus=4;public const float DuelFeed=1f,HuntFeed=1.5f,LoseFactor=.75f;
  public static Pawn Lich(Map map)=>Events3.Colonists(map).Where(p=>Necro.Lich(p)?.box!=null && !Necro.Lich(p).box.Destroyed && Necro.Lich(p).box.level>=MinLevel).OrderByDescending(p=>Necro.Lich(p).box.level).FirstOrDefault();
  public static bool CanFire(Map map){var t=Factions.Hostile;return Events3.S.e3Rival && Lich(map)!=null && t!=null && !t.defeated && Factions.Enabled(t) && !Events3.Active(Kind).Any();}
  public static E3Record Start(Map map){
   var lich=Lich(map);if(lich==null)return null;
   var r=Events3.Add(Kind,map);r.a=lich;r.faction=Factions.Hostile;r.until=Events3.Now+3*GenDate.TicksPerDay;
   Events3.Ask(r,"巫妖的战书","一只骷髅从地图边走进来，把一卷黑色羊皮纸放在地上，转身就走了。是塞伊一名巫妖写给"+lich.LabelShort+"的战书：\n\n“两个不死者容不下同一片土地。你选——在月光下比一场法术，或者来我的塔里找我的命匣。不敢来，我就去找你。”",new LookTargets(lich),Factions.Hostile);
   return r;
  }
  public static Phylactery Box(E3Record r)=>Necro.Lich(r.a)?.box;
  public static IEnumerable<DiaOption> Options(Letter_E3 l,E3Record r){
   var box=Box(r);
   yield return l.Choice("法术决斗",()=>Duel(r),box==null || box.Destroyed?"命匣不在了":null);
   yield return l.Choice("命匣狩猎（去他的塔）",()=>Hunt(r),Events3.SiteDef==null?"站点定义缺失":null);
   yield return l.Choice("拒绝（他会派兵来）",()=>Refuse(r));
  }
  public static Thing Arcane()=>Netheril.MakeArcane("Necromancy",QualityCategory.Excellent);
  public static bool Duel(E3Record r){
   r.answered=true;r.done=true;var box=Box(r);var p=r.a;if(box==null)return false;
   int intel=p.skills?.GetSkill(SkillDefOf.Intellectual)?.Level??0;
   int ours=box.level+intel/2+Rand.RangeInclusive(1,20),theirs=box.level+RivalBonus+Rand.RangeInclusive(1,20);
   bool won=ours>=theirs;
   if(won){
    box.Feed(Necro.Requirement(box.level)*DuelFeed);var loot=Events3.Drop(p.MapHeld,p.PositionHeld,new[]{Arcane()});
    Events3.State?.Deed(p.LabelShort+"在法术决斗中击败了塞伊的巫妖");
    Find.LetterStack.ReceiveLetter("决斗胜利","月光下，"+p.LabelShort+"（"+ours+"）压过了塞伊巫妖（"+theirs+"）。对方的影子碎成了灰：命匣吸走了他积攒的力量（一整级的经验），还留下"+Events3.Labels(loot)+"。",LetterDefOf.PositiveEvent,p);
   }else{
    box.experience*=LoseFactor;
    Find.LetterStack.ReceiveLetter("决斗失败",p.LabelShort+"（"+ours+"）输给了塞伊巫妖（"+theirs+"）。对方笑着抽走了命匣里的一部分力量：当前经验 -25%。",LetterDefOf.NegativeEvent,p);
   }
   return won;
  }
  public static void Hunt(E3Record r){
   r.answered=true;var pts=Mathf.Max(400f,StorytellerUtility.DefaultThreatPointsNow(r.map)*1.2f);
   var site=Events3.MakeSite(r,r.faction??Factions.Hostile,pts,4,10,SiteDays);
   if(site==null){r.done=true;Messages.Message("找不到巫妖的塔。",MessageTypeDefOf.RejectInput,false);return;}
   r.stage=1;
   Find.LetterStack.ReceiveLetter("巫妖的塔","塞伊巫妖的塔已标在世界地图上（"+SiteDays+" 天）。打进去，消灭里面所有的敌人，他的命匣就归"+r.a?.LabelShort+"了。",LetterDefOf.NeutralEvent,new LookTargets(site));
  }
  public static void Refuse(E3Record r){
   r.answered=true;r.done=true;var ev=Tribute.Events;
   if(ev!=null)foreach(var d in new[]{15,30})ev.scheduledRaids.Add(new ScheduledRaid{faction=Factions.HostileDef,fireTick=Events3.Now+d*GenDate.TicksPerDay,pointsFactor=.8f,letterLabel="敌对巫妖的报复"});
   Messages.Message("塞伊的巫妖收到了回绝。他说过会来找你的。",MessageTypeDefOf.ThreatSmall,false);
  }
  public static void Generate(Map map,E3Record r){
   var f=r.faction??Factions.Hostile;if(f==null)return;
   if(!CellFinder.TryFindRandomCellNear(map.Center,map,12,c=>c.Standable(map),out var center))center=map.Center;
   var pts=r.site is Site s?s.parts.FirstOrDefault()?.parms?.threatPoints??400f:400f;
   var pawns=PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms{groupKind=PawnGroupKindDefOf.Combat,faction=f,points=Mathf.Max(pts,f.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat)*1.05f),tile=map.Tile}).ToList();
   var sk=FactionGroups.SkeletonKind;if(sk!=null)for(int i=0;i<Rand.RangeInclusive(2,4);i++){var p=Events3.MakePawn(sk,f,map);if(p!=null)pawns.Add(p);}
   foreach(var p in pawns)GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(center,map,8),map);
   if(pawns.Count>0)LordMaker.MakeNewLord(f,new LordJob_DefendPoint(center),map,pawns);
  }
  public static void Tick(E3Record r){
   if(!r.answered){if(Events3.Now>r.until){Refuse(r);}return;}
   if(r.stage==1 && Events3.SiteCleared(r)){
    r.done=true;var box=Box(r);var p=r.a;
    if(box!=null && !box.Destroyed)box.Feed(Necro.Requirement(box.level)*HuntFeed);
    var home=Events3.Home;var loot=home!=null?Events3.DropPods(home,new[]{Arcane()}):new List<Thing>();
    Events3.State?.Deed("攻下了塞伊巫妖的塔");
    Find.LetterStack.ReceiveLetter("命匣狩猎成功","巫妖的塔里再没有活着（或死着）的守卫了。"+p?.LabelShort+"砸碎了对手的命匣，把里面的力量吸进了自己的（1.5 级的经验）。"+(loot.Count>0?"塔里的"+Events3.Labels(loot)+"已经空投回殖民地。":""),LetterDefOf.PositiveEvent,p);
    return;
   }
   if(r.stage==1 && Events3.Now>r.until){r.done=true;Messages.Message("巫妖的塔消失在了迷雾里。",MessageTypeDefOf.NeutralEvent,false);}
  }
 }
 public class IncidentWorker_E3Rival : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Rival;
  protected override bool CanFire(Map map,IncidentParms parms)=>RivalLich.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms)=>RivalLich.Start((Map)parms.target)!=null;
 }

 // ==================== 树人苏醒 ====================
 // 地图上有 30 棵以上的树时，离殖民地最远的一棵老树站了起来。看殖民者最近 60 天砍了多少棵树：
 //  不超过 30 棵（或殖民地有德鲁伊）：树人走到殖民地外待 6 小时，在那里种下一棵森林之心（8 格内作物生长 +20%，不能砍），然后离开。
 //  超过 30 棵：树人带着 2～3 棵苏醒的树来报复伐木区（怕火：受火伤 ×2）。
 public static class Treant {
  public const string Kind="treant";
  public const int TreeMin=30,CutDays=60,CutLimit=30,StayHours=6;public const float HeartRadius=8f,HeartGrowth=1.2f;
  public static PawnKindDef TreantKind=>DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_Treant");
  public static PawnKindDef TreeKind=>DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_AwakenedTree");
  static ThingDef heart;public static ThingDef HeartDef=>heart??(heart=DefDatabase<ThingDef>.GetNamedSilentFail("LP_HeartTree"));
  public static IEnumerable<Plant> Trees(Map map)=>map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>().Where(t=>t.def.plant!=null && t.def.plant.IsTree && t.Spawned);
  public static int RecentCuts(){var s=Events3.State;if(s==null)return 0;int now=Events3.Now;s.treeCuts.RemoveAll(t=>now-t>CutDays*GenDate.TicksPerDay);return s.treeCuts.Count;}
  public static bool HasDruid(Map map){var d=DefDatabase<LP_ClassDef>.GetNamedSilentFail("LP_Class_Druid");return d!=null && Events3.Colonists(map).Any(p=>ClassSystem.Level(p,d)>0);}
  public static bool CanFire(Map map)=>Events3.S.e3Treant && TreantKind!=null && HeartDef!=null && Trees(map).Count()>=TreeMin && !Events3.Active(Kind).Any();
  public static bool NearHeart(Plant p){
   var map=p.Map;if(map==null || HeartDef==null)return false;var list=map.listerThings.ThingsOfDef(HeartDef);
   for(int i=0;i<list.Count;i++)if(list[i].Position.InHorDistOf(p.Position,HeartRadius))return true;return false;
  }
  public static E3Record Start(Map map,bool? angryForce=null){
   bool angry=angryForce??(RecentCuts()>CutLimit && !HasDruid(map));
   var colony=Events3.Colonists(map).Select(c=>c.Position).ToList();
   var tree=Trees(map).OrderByDescending(t=>colony.Count==0?0f:colony.Min(c=>c.DistanceTo(t.Position))).FirstOrDefault();if(tree==null)return null;
   var cell=tree.Position;tree.Destroy();
   var r=Events3.Add(Kind,map);
   var t=PawnGenerator.GeneratePawn(TreantKind,angry?null:Events3.Wayfarers());GenSpawn.Spawn(t,cell,map);r.a=t;r.pawns.Add(t);
   if(angry){
    int n=Rand.RangeInclusive(2,3);
    for(int i=0;i<n && TreeKind!=null;i++){var w=PawnGenerator.GeneratePawn(TreeKind,null);GenSpawn.Spawn(w,CellFinder.RandomClosewalkCellNear(cell,map,4),map);r.pawns.Add(w);}
    foreach(var p in r.pawns){Items.Give(p,"LP_E3_Wooden");p.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true);}
    r.stage=2;
   }else{
    Items.Give(t,"LP_E3_Wooden");
    IntVec3 spot;if(!RCellFinder.TryFindRandomSpotJustOutsideColony(t,out spot))spot=CellFinder.RandomClosewalkCellNear(map.Center,map,12);
    r.lord=LordMaker.MakeNewLord(t.Faction,new LordJob_E3Visit(spot),map,new List<Pawn>{t});
    r.next=Events3.Now+StayHours*GenDate.TicksPerHour;
   }
   return r;
  }
  public static Thing PlantHeart(Map map,IntVec3 near){
   if(!CellFinder.TryFindRandomCellNear(near,map,6,c=>c.Standable(map) && c.GetEdifice(map)==null && c.GetFirstItem(map)==null && c.GetFirstPawn(map)==null && c.GetPlant(map)==null && GenConstruct.CanBuildOnTerrain(HeartDef,c,map,Rot4.North),out var cell))
    if(!CellFinder.TryFindRandomCellNear(near,map,10,c=>c.Standable(map) && c.GetEdifice(map)==null && c.GetFirstPawn(map)==null,out cell))return null;
   var p=cell.GetPlant(map);if(p!=null)p.Destroy();
   var h=ThingMaker.MakeThing(HeartDef);h.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(h,cell,map);return h;
  }
  public static void Tick(E3Record r){
   if(r.stage==2){if(r.pawns.All(p=>p==null || p.Dead || !p.Spawned))r.done=true;return;}
   var t=r.a;if(t==null || t.Dead || !t.Spawned){r.done=true;return;}
   if(r.stage==0 && Events3.Now>=r.next){
    r.stage=1;var h=PlantHeart(r.map,t.Position);Events3.Leave(r.lord);r.done=true;
    Find.LetterStack.ReceiveLetter("森林之心",t.LabelShort+"在殖民地外站了几个小时，看着你们怎么对待这片林子。它很满意：临走前把一颗种子按进土里，眨眼间长成了一棵森林之心。\n\n森林之心 "+HeartRadius.ToString("0")+" 格内的作物生长 +"+((HeartGrowth-1f)*100).ToString("0")+"%。",LetterDefOf.PositiveEvent,h!=null?new LookTargets(h):new LookTargets(t));
   }
  }
 }
 public class IncidentWorker_E3Treant : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Treant;
  protected override bool CanFire(Map map,IncidentParms parms)=>Treant.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var r=Treant.Start((Map)parms.target);if(r==null)return false;
   if(r.stage==2)SendStandardLetter("愤怒的树人","地图上最老的一棵树站了起来，身边还有"+(r.pawns.Count-1)+"棵被唤醒的树。最近 "+Treant.CutDays+" 天殖民地砍了太多树（"+Treant.RecentCuts()+" 棵），它们是来报复的！\n\n树怕火。",LetterDefOf.ThreatBig,parms,new LookTargets(r.pawns.Cast<Thing>()));
   else SendStandardLetter("树人苏醒","地图上最老的一棵树站了起来，慢慢走向殖民地。它看上去没有敌意——它想看看你们是怎么对待这片林子的。\n\n（最近 "+Treant.CutDays+" 天殖民地砍了 "+Treant.RecentCuts()+" 棵树。）",LetterDefOf.PositiveEvent,parms,r.a);
   return true;
  }
 }

 // ==================== 塔拉斯克苏醒 ====================
 // 传说中的巨兽醒了，20 天后到。准备（选一样）：
 //  请求援军（同盟好感≥30）：它到达时同盟派兵助战（0.8 倍点数）。  设陷阱（300 钢 + 6 零部件）：它带着重伤和“陷阱伤”（移速 ×0.6，20 天）来。
 //  献肉（600 肉）：引开它，事件结束。  正面迎战：什么也不做。
 // 到达后：永久狂暴，每 250 刻愈合伤口、撞坏身边 2.5 格内的建筑。外皮强度看威胁点数（受到伤害 ×1 / ×0.75 / ×0.55）。
 // 打死它：20 块塔拉斯克甲壳、4 颗塔拉斯克之牙（顶级材料），全殖民地每人 1500 冒险者经验。
 public static class Tarrasque {
  public const string Kind="tarrasque";
  public const int ArriveDays=20,AllyGoodwill=30,Steel=300,Components=6,Meat=600,Shells=20,Teeth=4,KillXp=1500;
  public const float HealPer=3f,WreckRadius=2.5f,WreckDamage=60f;
  public static PawnKindDef TKind=>DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_Tarrasque");
  public static bool CanFire(Map map)=>Events3.S.e3Tarrasque && TKind!=null && !Events3.Active(Kind).Any();
  public static E3Record Start(Map map){
   var r=Events3.Add(Kind,map);r.until=Events3.Now+ArriveDays*GenDate.TicksPerDay;r.next=Events3.Now+5*GenDate.TicksPerDay;
   Events3.Ask(r,"塔拉斯克苏醒","一连几天，大地都在轻轻颤动。今天来了消息：传说中的塔拉斯克从地底醒了，正朝这边走——大约 "+ArriveDays+" 天后到。\n\n它会撞碎沿路的一切，伤口愈合得飞快。还有时间准备：",LookTargets.Invalid);
   return r;
  }
  static bool IsMeat(Thing t)=>t.def.IsMeat;
  public static IEnumerable<DiaOption> Options(Letter_E3 l,E3Record r){
   var map=r.map;var c=Factions.Colony;
   yield return l.Choice("请求同盟援军（到时同盟派兵助战）",()=>{r.answered=true;r.stage=1;},c==null || c.HostileTo(Faction.OfPlayer) || c.PlayerGoodwill<AllyGoodwill?"同盟好感不到 "+AllyGoodwill:null);
   string trap=Events3.Count(map,t=>t.def==ThingDefOf.Steel)<Steel || Events3.Count(map,t=>t.def==ThingDefOf.ComponentIndustrial)<Components?"需要 "+Steel+" 钢和 "+Components+" 个零部件":null;
   yield return l.Choice("在它来的路上设陷阱（"+Steel+" 钢 + "+Components+" 零部件）",()=>{if(Events3.Take(map,t=>t.def==ThingDefOf.Steel,Steel) && Events3.Take(map,t=>t.def==ThingDefOf.ComponentIndustrial,Components)){r.answered=true;r.stage=2;}},trap);
   yield return l.Choice("献上 "+Meat+" 份肉，把它引开",()=>{if(Events3.Take(map,IsMeat,Meat)){r.answered=true;r.stage=3;r.done=true;Find.LetterStack.ReceiveLetter("引开了塔拉斯克","堆成小山的肉被拖到远处的荒野里。塔拉斯克闻着味道转了方向，地面的震动渐渐远去了。",LetterDefOf.PositiveEvent);}},Events3.Count(map,IsMeat)<Meat?"肉不到 "+Meat+" 份":null);
   yield return l.Choice("正面迎战",()=>{r.answered=true;r.stage=4;});
  }
  public static float Severity(float points)=>points<1500f?.1f:points<4000f?.5f:.9f;
  public static Pawn Arrive(E3Record r){
   var map=r.map;if(map==null || !Find.Maps.Contains(map)){r.done=true;return null;}
   if(!r.answered){r.answered=true;r.stage=4;}
   if(!Events2.EdgeCell(map,out var cell)){r.until+=GenDate.TicksPerHour;return null;}
   float pts=StorytellerUtility.DefaultThreatPointsNow(map);
   var t=PawnGenerator.GeneratePawn(TKind,null);GenSpawn.Spawn(t,cell,map);r.a=t;
   Items.Give(t,"LP_E3_TarrasqueHide",-1,Severity(pts));
   t.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true);
   string extra="";
   if(r.stage==2){
    Items.Give(t,"LP_E3_TrapWounds");
    for(int i=0;i<12 && !t.Downed;i++){var part=t.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Cut,BodyPartHeight.Undefined,BodyPartDepth.Outside);if(part!=null)t.TakeDamage(new DamageInfo(DamageDefOf.Cut,12f,0f,-1f,null,part));}
    extra="\n\n它踩进了你们设下的陷阱，带着一身伤、步子也慢了。";
   }
   if(r.stage==1){
    var c=Factions.Colony;
    if(c!=null && !c.HostileTo(Faction.OfPlayer)){var parms=new IncidentParms{target=map,faction=c,points=pts*.8f,raidArrivalMode=PawnsArrivalModeDefOf.EdgeWalkIn,raidStrategy=RaidStrategyDefOf.ImmediateAttackFriendly,forced=true};if(IncidentDefOf.RaidFriendly.Worker.TryExecute(parms))extra="\n\n费伦拓荒同盟的援军也到了！";}
   }
   Find.LetterStack.ReceiveLetter("塔拉斯克来了","地平线上升起一座会走的山：塔拉斯克到了。它会撞碎身边的一切，伤口愈合得飞快。"+extra,LetterDefOf.ThreatBig,t);
   return t;
  }
  public static void Tick(E3Record r){
   if(r.a==null){
    if(Events3.Now>=r.until){Arrive(r);return;}
    if(Events3.Now>=r.next){r.next+=5*GenDate.TicksPerDay;int left=(r.until-Events3.Now)/GenDate.TicksPerDay;Messages.Message("大地又震了一下：塔拉斯克还有约 "+left+" 天到。",MessageTypeDefOf.ThreatBig,false);}
    return;
   }
   var t=r.a;if(t.Dead)return;if(!t.Spawned){r.done=true;return;}
   int healed=0;foreach(var inj in t.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h=>!h.IsPermanent()).ToList()){inj.Heal(HealPer);if(++healed>=2)break;}
   var map=t.Map;
   foreach(var c in GenRadial.RadialCellsAround(t.Position,WreckRadius,true)){
    if(!c.InBounds(map))continue;var b=c.GetEdifice(map);
    if(b!=null && b.def.useHitPoints && !b.Destroyed)b.TakeDamage(new DamageInfo(DamageDefOf.Blunt,WreckDamage,0f,-1f,t));
   }
  }
  public static void OnDeath(Pawn p,E3KillState st){
   var r=Events3.Active(Kind).FirstOrDefault(x=>x.a==p);if(r==null)return;r.done=true;
   var loot=Events3.Drop(st.map,st.at,new[]{Events3.Make("LP_TarrasqueShell",Shells),Events3.Make("LP_TarrasqueTooth",Teeth)});
   if(st.map!=null)foreach(var c in Events3.Colonists(st.map))Adventurer.GainXP(c,KillXp);
   Events3.State?.Deed("屠杀了塔拉斯克");
   Find.LetterStack.ReceiveLetter("塔拉斯克倒下了","山一样的巨兽终于倒下，再没有站起来。殖民地每个人都得到 "+KillXp+" 点冒险者经验。\n\n它的尸体上剥下了"+Events3.Labels(loot)+"——最好的护甲和武器材料。吟游诗人会唱这件事很多年。",LetterDefOf.PositiveEvent,new LookTargets(st.at,st.map));
  }
 }
 public class IncidentWorker_E3Tarrasque : IncidentWorker_E3 {
  protected override bool On=>Events3.S.e3Tarrasque;
  protected override bool CanFire(Map map,IncidentParms parms)=>Tarrasque.CanFire(map);
  protected override bool TryExecuteWorker(IncidentParms parms)=>Tarrasque.Start((Map)parms.target)!=null;
 }

 // ==================== 冒险日志 ====================
 public class CompUseEffect_AdventureLog : CompUseEffect {
  public const float Xp=400f;
  public override void DoEffect(Pawn usedBy){base.DoEffect(usedBy);Adventurer.GainXP(usedBy,Xp);Messages.Message(usedBy.LabelShort+"读完了冒险日志：冒险者经验 +"+Xp.ToString("0")+"。",usedBy,MessageTypeDefOf.PositiveEvent,false);}
  public override AcceptanceReport CanBeUsedBy(Pawn p)=>Adventurer.Get(p)!=null?true:new AcceptanceReport("没有冒险者等级");
 }
}
