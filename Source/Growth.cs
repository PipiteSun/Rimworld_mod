using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.58.3 持续成长（见 ArtSource/TraitsFeats-1.58.3-spec.md）：属性典籍、巨灵“让我更强”、传承武器、实战磨砺、派系声望。
 // 存档：典籍 / 许愿 / 磨砺在 Hediff_Adventurer 里；传承武器的主人与击杀数在 CompLegacyWeapon；声望在 GameComponent_Growth。
 public static class Growth {
  public const int TomeTicks=30*GenDate.TicksPerHour,TomeBonus=2,GrindNeed=120000,GrindMax=2,LegacyKills=10,LegacyAwakenLevel=10,AidCooldown=GenDate.TicksPerDay;
  public const float LegacyStep=.15f,TomeNemesis=.25f,TomeLegend=.15f,TomeBlackNet=.05f,TomeIsekai=.03f,LegacyNemesis=.1f,LegacyLegend=.1f,TomeGift=.05f,HammerGuard=.9f,SwordAura=.05f,SwordRadius=3f,BowRange=3f,RepDiscount=.95f;
  public static readonly string[] Keys={"STR","DEX","CON","INT","WIS","CHA"};
  public static float? TestRoll;static float Roll=>TestRoll??Rand.Value;
  public static bool GrindOn=>LichMod.Settings?.growthGrind??true;
  public static bool RepOn=>LichMod.Settings?.factionReputation??true;
  public static ThingDef TomeDef(int a)=>a>=0 && a<6?DefDatabase<ThingDef>.GetNamedSilentFail("LP_Tome_"+Keys[a]):null;
  public static int TomeIndex(ThingDef d)=>d==null || !d.defName.StartsWith("LP_Tome_")?-1:Array.IndexOf(Keys,d.defName.Substring(8));
  public static Thing MakeTome(int a=-1){var d=TomeDef(a>=0?a:Rand.Range(0,6));return d==null?null:ThingMaker.MakeThing(d);}
  public static readonly string[] LegacyNames={"LP_Legacy_Sword","LP_Legacy_Hammer","LP_Legacy_Bow","LP_Legacy_Staff"};
  public static Thing MakeLegacy(string name=null){var d=DefDatabase<ThingDef>.GetNamedSilentFail(name??LegacyNames.RandomElement());if(d==null)return null;var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Masterwork,ArtGenerationContext.Outsider);return t;}
  public static void Drop(Thing t,Map map,IntVec3 at,Pawn fallback=null){if(t==null)return;if(map!=null && at.IsValid)GenPlace.TryPlaceThing(t,at,map,ThingPlaceMode.Near);else fallback?.inventory?.innerContainer.TryAdd(t);}

  // ---------- 典籍 ----------
  public static string CannotReadTome(Pawn p,int a){var h=Adventurer.Get(p);if(h==null)return "没有冒险者等级";if(a<0)return null;return h.tomes[a]?p.LabelShort+"已经从典籍得到过"+Adventurer.Names[a]+"了":null;}
  public static bool FinishTome(Pawn p,int a,Thing book,bool destroyNow){
   var h=Adventurer.Get(p);if(h==null || a<0 || h.tomes[a])return false;
   h.tomes[a]=true;h.scores[a]+=TomeBonus;h.Dirty();
   if(book!=null && !book.Destroyed){if(destroyNow)book.Destroy();else{var b=book;Fiends.Defer(()=>{if(!b.Destroyed)b.Destroy();});}}
   if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("典籍读完："+p.LabelShort,p.LabelShort+"读完了"+TomeDef(a)?.label+"："+Adventurer.Names[a]+" +"+TomeBonus+"（现在 "+h.scores[a]+"），上限 +"+TomeBonus+"（现在 "+h.Cap(a)+"）。书化成了灰。",LetterDefOf.PositiveEvent,p);
   return true;
  }

  // ---------- 巨灵：让我更强（每人一生一次 +1，可以超过上限 1 点） ----------
  public static string CannotWish(Pawn p){var h=Adventurer.Get(p);if(h==null)return "没有冒险者等级";return h.wishAbility>=0?"已经许过这个愿了":null;}
  public static bool WishStronger(Pawn p,int a){var h=Adventurer.Get(p);if(h==null || a<0 || a>5 || CannotWish(p)!=null)return false;h.wishAbility=a;h.scores[a]++;h.Dirty();return true;}
  public static void OpenStrongerDialog(Pawn genie,RimWorld.Ability ability){
   var node=new DiaNode("“谁想变得更强？”");
   foreach(var c in genie.MapHeld.mapPawns.FreeColonistsSpawned.Where(x=>Adventurer.Get(x)!=null).ToList()){
    var who=c;var o=new DiaOption(c.LabelShort){resolveTree=false};var why=CannotWish(c);if(why!=null)o.Disable(why);
    var n2=new DiaNode("“"+c.LabelShort+"，你想要哪一份力量？”（+1，可以超过上限 1 点）");
    for(int i=0;i<6;i++){int k=i;var h=Adventurer.Get(c);n2.options.Add(new DiaOption(Adventurer.Names[i]+" "+h.scores[i]+" → "+(h.scores[i]+1)){action=()=>{WishStronger(who,k);Items.Give(genie,"LP_GenieFatigue",Genie.FatigueDays*GenDate.TicksPerDay);Messages.Message(genie.LabelShort+"实现了愿望："+who.LabelShort+"的"+Adventurer.Names[k]+" +1。",who,MessageTypeDefOf.PositiveEvent,false);},resolveTree=true});}
    n2.options.Add(new DiaOption("再想想"){action=()=>ability?.ResetCooldown(),resolveTree=true});
    o.link=n2;node.options.Add(o);
   }
   node.options.Add(new DiaOption("再想想"){action=()=>ability?.ResetCooldown(),resolveTree=true});
   Find.WindowStack.Add(new Dialog_NodeTree(node,true,false,"巨灵的愿望"));
  }

  // ---------- 实战磨砺：每 2500 刻按差值采样 ----------
  static float Xp(Pawn p,SkillDef s){var r=p.skills?.GetSkill(s);return r==null?0f:r.XpTotalEarned;}
  public static readonly string[] GrindSources={"格斗经验 + 采矿经验 ×0.5","射击经验","受到的伤害 ×100","智识经验","（医疗 + 驯兽 + 种植经验）×0.5","社交经验"};
  public static float[] Sources(Pawn p){
   var s=new float[6];if(p?.skills==null)return s;
   s[0]=Xp(p,SkillDefOf.Melee)+Xp(p,SkillDefOf.Mining)*.5f;s[1]=Xp(p,SkillDefOf.Shooting);
   s[2]=(p.records?.GetValue(RecordDefOf.DamageTaken)??0f)*100f;s[3]=Xp(p,SkillDefOf.Intellectual);
   s[4]=(Xp(p,SkillDefOf.Medicine)+Xp(p,SkillDefOf.Animals)+Xp(p,SkillDefOf.Plants))*.5f;s[5]=Xp(p,SkillDefOf.Social);
   return s;
  }
  public static int Raised;
  public static int Sample(Pawn p){
   var h=Adventurer.Get(p);if(h==null || !GrindOn || !Adventurer.Enabled)return 0;var now=Sources(p);int n=0;
   if(h.grindLast==null || h.grindLast.Length!=6){h.grindLast=now;return 0;} // 第一次只记基准，不算以前的
   bool apply=!Adventurer.EffectsHandedToIsekai; // Isekai 共存：照常累计但只显示
   for(int a=0;a<6;a++){
    float d=Mathf.Max(0f,now[a]-h.grindLast[a]);h.grind[a]+=d;
    if(h.grind[a]>=GrindNeed && apply && h.grindGained[a]<GrindMax && h.scores[a]<h.Cap(a)){
     h.grind[a]-=GrindNeed;h.grindGained[a]++;h.scores[a]++;h.Dirty();n++;Raised++;
     if(p.Faction!=null && p.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"在实战中磨砺出了更好的"+Adventurer.Names[a]+"（+1，现在 "+h.scores[a]+"；这一项还能磨砺 "+(GrindMax-h.grindGained[a])+" 次）。",p,MessageTypeDefOf.PositiveEvent,false);
    }
    if(h.grindGained[a]>=GrindMax || h.scores[a]>=h.Cap(a))h.grind[a]=Mathf.Min(h.grind[a],GrindNeed);
   }
   h.grindLast=now;return n;
  }
  public static string GrindTip(Hediff_Adventurer h,int a)=>"实战磨砺："+Adventurer.Names[a]+" "+Mathf.Min(h.grind[a],GrindNeed).ToString("0")+" / "+GrindNeed+"\n来源："+GrindSources[a]+"\n满了这一项 +1（一生最多 +"+GrindMax+"，不超过上限；已得 "+h.grindGained[a]+"）。每 2500 刻按差值累计。"+(Adventurer.EffectsHandedToIsekai?"\nIsekai 共存模式：照常累计，但不加属性。":"")+(GrindOn?"":"\n“实战磨砺”已在设置里关闭。");
  // 分页：典籍 / 许愿 / 磨砺一行。
  public static float Draw(float y,float w,Hediff_Adventurer h){
   var src=new List<string>();for(int a=0;a<6;a++){if(h.tomes[a])src.Add(Adventurer.Names[a]+" 典籍 +2");}if(h.wishAbility>=0)src.Add(Adventurer.Names[h.wishAbility]+" 许愿 +1");
   if(src.Count>0){var r=new Rect(0f,y,w,24f);Widgets.Label(r,"成长："+string.Join("、",src));TooltipHandler.TipRegion(r,"属性典籍：该项 +2、上限 +2（每项一生一次）。巨灵“让我更强”：该项 +1、上限 +1（每人一生一次）。");y+=26f;}
   float cw=w/6f;
   for(int a=0;a<6;a++){
    var cell=new Rect(cw*a,y,cw-4f,20f);Widgets.FillableBar(cell,Mathf.Clamp01(h.grind[a]/GrindNeed));
    Text.Font=GameFont.Tiny;Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(cell,Adventurer.Names[a].Substring(0,1)+" 磨砺"+(h.grindGained[a]>0?" +"+h.grindGained[a]:""));Text.Anchor=TextAnchor.UpperLeft;Text.Font=GameFont.Small;
    TooltipHandler.TipRegion(cell,GrindTip(h,a));
   }
   return y+24f;
  }

  // ---------- 传承武器 ----------
  public static readonly List<CompLegacyWeapon> all=new List<CompLegacyWeapon>();
  public static readonly HashSet<ThingDef> legacyDefs=new HashSet<ThingDef>();
  public static CompLegacyWeapon Legacy(Pawn p){var w=p?.equipment?.Primary;return w==null || !legacyDefs.Contains(w.def)?null:w.TryGetComp<CompLegacyWeapon>();}
  // 持有者是主人时的阶段（0 沉睡 / 1 觉醒 / 2 升华）。
  public static int Stage(Pawn owner){
   var h=Adventurer.Get(owner);if(h==null)return 0;return h.legendary?2:h.level>=LegacyAwakenLevel?1:0;
  }
  public static float DamageFactor(Pawn a,DamageInfo d){
   if(legacyDefs.Count==0 || a==null || d.Weapon==null || !legacyDefs.Contains(d.Weapon))return 1f;
   var c=Legacy(a);if(c==null || c.owner!=a)return 1f;return 1f+LegacyStep*Stage(a);
  }
  public static float GuardFactor(Pawn v){var c=Legacy(v);return c!=null && c.Props.kind=="Hammer" && c.owner==v && Stage(v)>=2?HammerGuard:1f;}
  public static float RangeBonus(Pawn p,Thing eq){if(eq==null || !legacyDefs.Contains(eq.def))return 0f;var c=eq.TryGetComp<CompLegacyWeapon>();return c!=null && c.Props.kind=="Bow" && c.owner==p && Stage(p)>=2?BowRange:0f;}
  public static readonly List<Pawn> swordOwners=new List<Pawn>();
  public static float SwordHit(Pawn a){
   if(swordOwners.Count==0 || a?.Map==null)return 0f;
   foreach(var o in swordOwners)if(o!=a && o.Spawned && o.Map==a.Map && o.Faction==a.Faction && o.Position.InHorDistOf(a.Position,SwordRadius))return SwordAura;
   return 0f;
  }
  public static int Bonded,Released;
  public static void OnKill(Pawn killer,Pawn victim){
   var c=Legacy(killer);if(c==null || victim==null || killer==victim || !victim.HostileTo(killer))return;
   if(!victim.RaceProps.Humanlike && victim.BodySize<1f)return;
   if(c.owner!=null)return;
   c.kills.TryGetValue(killer.thingIDNumber,out int k);k++;c.kills[killer.thingIDNumber]=k;
   if(k>=LegacyKills){c.owner=killer;c.kills.Clear();Bonded++;Sync(killer);
    if(killer.Faction!=null && killer.Faction.IsPlayer && !Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("传承武器："+c.parent.LabelNoCount,c.parent.LabelNoCount+"认可了"+killer.LabelShort+"：从今天起，"+killer.LabelShort+"是它的主人。\n\n冒险者 10 级时它会觉醒（伤害 +15%，获得能力），成为传奇冒险者时升华（再 +15%，被动光环）。",LetterDefOf.PositiveEvent,killer);}
  }
  public static void OwnerDied(Pawn p){
   foreach(var c in all.ToList())if(c.owner==p){c.owner=null;c.kills.Clear();Released++;if(!Adventurer.TestSuppressed && c.parent.MapHeld!=null)Messages.Message(c.parent.LabelNoCount+"失去了主人，重新沉睡。",new LookTargets(c.parent.SpawnedOrAnyParentSpawned?c.parent.PositionHeld:IntVec3.Invalid,c.parent.MapHeld),MessageTypeDefOf.NeutralEvent,false);}
  }
  // 能力和光环：觉醒后持有时给能力；升华的剑主进光环名单、杖主得到星辉共鸣。
  public static void Sync(Pawn p){
   if(p?.abilities==null)return;var c=Legacy(p);int st=c!=null && c.owner==p?Stage(p):0;
   foreach(var name in new[]{"LP_LegacyDawnStrike","LP_LegacyQuake","LP_LegacyWindArrow","LP_LegacyStarfall"}){
    var d=DefDatabase<AbilityDef>.GetNamedSilentFail(name);if(d==null)continue;bool want=st>=1 && c.Props.ability==d;bool has=p.abilities.GetAbility(d)!=null;
    if(want && !has)p.abilities.GainAbility(d);else if(!want && has)p.abilities.RemoveAbility(d);
   }
   bool sword=st>=2 && c.Props.kind=="Sword";if(sword && !swordOwners.Contains(p))swordOwners.Add(p);else if(!sword)swordOwners.Remove(p);
   bool staff=st>=2 && c.Props.kind=="Staff";if(staff)Items.Give(p,"LP_LegacyStarlight");else Items.Remove(p,"LP_LegacyStarlight");
  }

  // ---------- 掉落 ----------
  public static int nemesisDrops,legendDrops;
  public static void NemesisDrop(Pawn killer,Map map,IntVec3 at){
   if(Roll<TomeNemesis){Drop(MakeTome(),map,at,killer);nemesisDrops++;}
   if(Roll<LegacyNemesis){Drop(MakeLegacy(),map,at,killer);nemesisDrops++;}
  }
  public static void LegendDrop(Pawn victim,Map map,IntVec3 at){
   if(map==null || victim?.Faction==null || !victim.HostileTo(Faction.OfPlayer))return;
   if(Roll<TomeLegend){Drop(MakeTome(),map,at);legendDrops++;}
   if(Roll<LegacyLegend){Drop(MakeLegacy(),map,at);legendDrops++;}
  }

  // ---------- 派系声望 ----------
  public static readonly string[] RepFactions={"LP_FaerunColony","LP_EmberClan","LP_BlackNetwork"};
  public static readonly int[] RepRanks={3,10,25,50};
  public const int RepMax=50,QuestRep=3,AidRep=2,GiftPerRep=20;
  public static readonly Dictionary<string,string[]> RepTitles=new Dictionary<string,string[]>{
   {"LP_FaerunColony",new[]{"竖琴手线人","竖琴手","竖琴大师","竖琴之主"}},
   {"LP_EmberClan",new[]{"余烬之友","炉边客","熔铁誓友","炉心之主"}},
   {"LP_BlackNetwork",new[]{"黑网熟客","黑网掮客","暗影掮主","黑网之主"}}};
  public static readonly Dictionary<string,string> RepItems=new Dictionary<string,string>{{"LP_FaerunColony","LP_PotionHeroism"},{"LP_EmberClan","LP_LavaPiercer"},{"LP_BlackNetwork","LP_Doom"}};
  public static bool Tracked(Faction f)=>f!=null && !f.IsPlayer && RepFactions.Contains(f.def.defName);
  public static GameComponent_Growth State=>GameComp<GameComponent_Growth>.Get();
  public static int Rep(Faction f)=>State?.Get(f)??0;
  public static int Rank(Faction f){int r=Rep(f),n=0;foreach(var t in RepRanks)if(r>=t)n++;return n;}
  public static string Title(Faction f){int k=Rank(f);return k<=0 || !RepTitles.TryGetValue(f.def.defName,out var t)?"无":t[k-1];}
  public static bool Gain(Faction f,int n,string why){
   var s=State;if(!RepOn || s==null || !Tracked(f) || n<=0 || f.HostileTo(Faction.OfPlayer))return false; // 敌对时冻结
   n=Exalted.Reputation(n); // 1.59.0 君临 ×1.5
   int before=Rank(f);s.Add(f,n);int after=Rank(f);
   if(after>before && !Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("派系声望："+f.Name,"殖民地在"+f.Name+"的声望到了 "+Rep(f)+"（"+why+"），获得头衔“"+Title(f)+"”。\n\n"+RankRewards(after),LetterDefOf.PositiveEvent);
   return true;
  }
  public static string RankRewards(int k){
   var l=new List<string>();if(k>=1)l.Add("该派系商队卖给你的东西 -5%");if(k>=2)l.Add("每季一封情报信");if(k>=3)l.Add("请求援兵冷却减半，商队带一件专属物品");if(k>=4)l.Add("每季一份礼物（魔法物品，5% 是属性典籍）");
   return "头衔奖励："+string.Join("；",l)+"。";
  }
  public static float PriceFactor(Faction f,TradeAction a)=>a==TradeAction.PlayerBuys && Tracked(f) && Rank(f)>=1?RepDiscount:1f;
  public static void Gift(Faction f,int goodwill){
   var s=State;if(s==null || !Tracked(f) || goodwill<=0)return;
   s.giftAccum.TryGetValue(f.loadID,out int acc);acc+=goodwill;int n=acc/GiftPerRep;acc%=GiftPerRep;s.giftAccum[f.loadID]=acc;
   if(n>0)Gain(f,n,"送礼");
  }
  public static void Aid(Pawn victim,Pawn killer){
   var s=State;if(s==null || killer?.Faction==null || !killer.Faction.IsPlayer || victim?.MapHeld==null || !victim.HostileTo(Faction.OfPlayer))return;
   var map=victim.MapHeld;int now=GenTicks.TicksGame;
   foreach(var f in Find.FactionManager.AllFactionsListForReading.Where(Tracked).ToList()){
    if(s.aidTick.TryGetValue(f.loadID,out int t) && now>=t && now-t<AidCooldown)continue;
    if(!map.mapPawns.SpawnedPawnsInFaction(f).Any(p=>!p.Dead && !p.Downed))continue;
    if(victim.HostileTo(f) || victim.Faction==null){s.aidTick[f.loadID]=now;Gain(f,AidRep,"援助");}
   }
  }
  public static string IntelText(){
   var hostile=Find.FactionManager.AllFactionsListForReading.Where(f=>!f.IsPlayer && !f.Hidden && !f.defeated && f.HostileTo(Faction.OfPlayer)).ToList();
   var map=Find.AnyPlayerHomeMap;float pts=map==null?0f:StorytellerUtility.DefaultThreatPointsNow(map);
   if(hostile.Count==0)return "眼下没有哪个派系对殖民地怀有敌意。";
   var f=hostile.RandomElementByWeight(x=>x.def.humanlikeFaction?2f:1f);
   return "线人说，下一次来犯的多半是"+f.Name+"，规模大约 "+(pts*.8f).ToString("0")+"～"+(pts*1.2f).ToString("0")+" 点（按殖民地现在的威胁点数估算）。";
  }
  public static void Quarterly(GameComponent_Growth s){
   if(!RepOn)return;var map=Find.AnyPlayerHomeMap;if(map==null)return;
   foreach(var f in Find.FactionManager.AllFactionsListForReading.Where(Tracked).ToList()){
    if(f.HostileTo(Faction.OfPlayer))continue;int k=Rank(f);
    if(k>=2 && !Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("情报："+f.Name,f.Name+"（你的头衔：“"+Title(f)+"”）送来了一封情报信：\n\n"+IntelText(),LetterDefOf.NeutralEvent);
    if(k>=4){var t=GiftThing();if(t!=null){DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map),map,new[]{t},110,false,false,true,false,true,null);s.gifts++;if(!Adventurer.TestSuppressed)Messages.Message(f.Name+"送来了一份礼物："+t.LabelCap+"。",MessageTypeDefOf.PositiveEvent,false);}}
   }
  }
  public static Thing GiftThing(){
   if(Roll<TomeGift)return MakeTome();
   var d=Epic.RandomMagicItemDef();if(d==null)return null;var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Outsider);return t;
  }
 }

 // ---------- 典籍 ----------
 public class Book_Tome : Book {
  public override string LabelNoCount=>def.label+(this.TryGetQuality(out var q)?"（"+q.GetLabel()+"）":"");
 }
 public class BookOutcome_Tome : BookOutcomeDoer {
  public Dictionary<int,int> read=new Dictionary<int,int>();readonly Dictionary<int,int> last=new Dictionary<int,int>();readonly Dictionary<int,float> rem=new Dictionary<int,float>();
  int Ability=>Growth.TomeIndex(Parent?.def);
  public override bool DoesProvidesOutcome(Pawn reader)=>Growth.CannotReadTome(reader,Ability)==null;
  public override void OnReadingTick(Pawn reader,float factor){
   if(reader==null)return;int id=reader.thingIDNumber,now=Find.TickManager.TicksGame;
   if(last.TryGetValue(id,out int l) && now>l && now-l<=300){int n=ReadSpeed.Scale(reader,(now-l)*Exalted.ReadFactor(reader),rem);if(n>0)Feed(reader,n,false);} // 1.59.0 洞彻心智 +30%；2026-10-28 × 阅读速度（研究速度），零头留到下次
   last[id]=now;
  }
  public int Ticks(Pawn r){read.TryGetValue(r.thingIDNumber,out int t);return t;}
  public bool Feed(Pawn r,int delta,bool destroyNow){
   if(Growth.CannotReadTome(r,Ability)!=null)return false;
   int t=Ticks(r)+delta;read[r.thingIDNumber]=t;
   if(t<Growth.TomeTicks)return false;
   return Growth.FinishTome(r,Ability,Parent,destroyNow);
  }
  public override string GetBenefitsString(Pawn reader){
   int a=Ability;var s="累计研读 30 小时后："+(a>=0?Adventurer.Names[a]:"属性")+" +2，上限 +2（同一项属性一生只能从典籍得一次），书随即化为灰烬。\n"+ReadSpeed.Note;
   if(reader!=null){var why=Growth.CannotReadTome(reader,a);s+=why!=null?"\n不能读："+why:"\n"+reader.LabelShort+"：已读 "+(Ticks(reader)/(float)GenDate.TicksPerHour).ToString("0.#")+" / 30 小时。";}
   return s;
  }
  public override void PostExposeData(){base.PostExposeData();Scribe_Collections.Look(ref read,"lpTomeRead",LookMode.Value,LookMode.Value);if(read==null)read=new Dictionary<int,int>();}
 }
 [HarmonyPatch(typeof(BookUtility),nameof(BookUtility.CanReadBook))]
 public static class GrowthTomeCanRead {
  static void Postfix(Book book,Pawn reader,ref string reason,ref bool __result){
   if(!__result || !(book is Book_Tome))return;var why=Growth.CannotReadTome(reader,Growth.TomeIndex(book.def));if(why!=null){__result=false;reason=why;}
  }
 }

 // ---------- 传承武器 ----------
 public class CompProperties_LegacyWeapon : CompProperties {public string kind;public AbilityDef ability;public CompProperties_LegacyWeapon(){compClass=typeof(CompLegacyWeapon);}}
 public class CompLegacyWeapon : ThingComp {
  public Pawn owner;public Dictionary<int,int> kills=new Dictionary<int,int>();
  public CompProperties_LegacyWeapon Props=>(CompProperties_LegacyWeapon)props;
  public override void Initialize(CompProperties p){base.Initialize(p);Growth.legacyDefs.Add(parent.def);if(!Growth.all.Contains(this))Growth.all.Add(this);}
  public override void PostPostMake(){base.PostPostMake();parent.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Masterwork,ArtGenerationContext.Outsider);}
  public override void PostDestroy(DestroyMode mode,Map previousMap){base.PostDestroy(mode,previousMap);Growth.all.Remove(this);}
  public override void Notify_Equipped(Pawn pawn){base.Notify_Equipped(pawn);Growth.Sync(pawn);}
  public override void Notify_Unequipped(Pawn pawn){base.Notify_Unequipped(pawn);Fiends.Defer(()=>Growth.Sync(pawn));}
  public string StageLabel(){if(owner==null)return "沉睡（无主）";int s=Growth.Stage(owner);return s>=2?"升华":s==1?"觉醒":"沉睡";}
  public override string CompInspectStringExtra(){
   if(owner!=null)return "主人："+owner.LabelShort+"（"+StageLabel()+"）";
   int best=kills.Count==0?0:kills.Values.Max();return "沉睡：第一个用它击杀 "+Growth.LegacyKills+" 个敌人的人成为主人（目前最多 "+best+"）";
  }
  public override void PostExposeData(){
   base.PostExposeData();Scribe_References.Look(ref owner,"lpLegacyOwner");Scribe_Collections.Look(ref kills,"lpLegacyKills",LookMode.Value,LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit && kills==null)kills=new Dictionary<int,int>();
  }
 }
 public class CompProperties_LegacyPower : CompProperties_AbilityEffect {public string effect;public CompProperties_LegacyPower(){compClass=typeof(CompAbilityEffect_LegacyPower);}}
 public class CompAbilityEffect_LegacyPower : CompAbilityEffect {
  string Effect=>((CompProperties_LegacyPower)props).effect;
  IEnumerable<Pawn> Foes(IntVec3 c,float r){var p=parent.pawn;return GenRadial.RadialDistinctThingsAround(c,p.Map,r,true).OfType<Pawn>().Where(x=>x!=p && !x.Dead && x.HostileTo(p)).ToList();}
  public override void Apply(LocalTargetInfo target,LocalTargetInfo dest){
   base.Apply(target,dest);var p=parent.pawn;if(p?.Map==null)return;var map=p.Map;
   switch(Effect){
    case "dawn":{var t=target.Pawn;if(t==null)break;FleckMaker.Static(t.Position,map,FleckDefOf.PsycastAreaEffect,1.5f);t.TakeDamage(new DamageInfo(Faith.Radiant,30f,.3f,-1f,p));if(!t.Dead)Items.Give(t,"LP_LegacyBlinded");break;}
    case "quake":foreach(var t in Foes(p.Position,2f)){t.TakeDamage(new DamageInfo(DamageDefOf.Blunt,15f,.2f,-1f,p));if(!t.Dead)t.stances?.stunner?.StunFor(120,p,false,true,false);}FleckMaker.Static(p.Position,map,FleckDefOf.PsycastAreaEffect,4f);break;
    case "wind":{var dir=(target.Cell-p.Position).ToVector3();dir.y=0f;if(dir.sqrMagnitude<.01f)break;dir.Normalize();var hit=new HashSet<Pawn>();
     for(int i=1;i<=30;i++){var c=(p.Position.ToVector3Shifted()+dir*i).ToIntVec3();if(!c.InBounds(map))break;if(c.GetEdifice(map) is Building b && b.def.Fillage==FillCategory.Full)break;
      foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList())if(t!=p && !t.Dead && t.HostileTo(p) && hit.Add(t))t.TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamedSilentFail("Arrow")??DamageDefOf.Stab,25f,.4f,-1f,p));
      if(i%3==0)FleckMaker.ThrowDustPuff(c.ToVector3Shifted(),map,.8f);}
     break;}
    case "starfall":FleckMaker.Static(target.Cell,map,FleckDefOf.PsycastAreaEffect,4f);foreach(var t in Foes(target.Cell,2f))t.TakeDamage(new DamageInfo(Faith.Radiant,30f,.3f,-1f,p));break;
   }
  }
  public override bool AICanTargetNow(LocalTargetInfo target){var t=target.Pawn;return t!=null && !t.Dead && !t.Downed && t.HostileTo(parent.pawn);}
  public override bool GizmoDisabled(out string reason){
   var c=Growth.Legacy(parent.pawn);if(c==null || c.owner!=parent.pawn || Growth.Stage(parent.pawn)<1){reason="要握着觉醒的传承武器。";return true;}
   return base.GizmoDisabled(out reason);
  }
 }

 // ---------- 存档组件：派系声望、季度信 ----------
 public class GameComponent_Growth : GameComponent {
  public Dictionary<int,int> rep=new Dictionary<int,int>(),giftAccum=new Dictionary<int,int>(),aidTick=new Dictionary<int,int>();
  public int lastQuadrum=-1,gifts;public List<int> stocked=new List<int>();
  public GameComponent_Growth(Game game){}
  public int Get(Faction f){if(f==null)return 0;rep.TryGetValue(f.loadID,out int v);return v;}
  public void Add(Faction f,int n){rep[f.loadID]=Mathf.Clamp(Get(f)+n,0,Growth.RepMax);}
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;if(now%2500!=1717)return;
   int q=now/GenDate.TicksPerQuadrum;if(lastQuadrum<0){lastQuadrum=q;return;}
   if(q!=lastQuadrum){lastQuadrum=q;Growth.Quarterly(this);}
  }
  public override void FinalizeInit(){base.FinalizeInit();Growth.swordOwners.Clear();foreach(var p in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)Growth.Sync(p);}
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Collections.Look(ref rep,"lpRep",LookMode.Value,LookMode.Value);Scribe_Collections.Look(ref giftAccum,"lpRepGift",LookMode.Value,LookMode.Value);Scribe_Collections.Look(ref aidTick,"lpRepAid",LookMode.Value,LookMode.Value);
   Scribe_Values.Look(ref lastQuadrum,"lpRepQuadrum",-1);Scribe_Values.Look(ref gifts,"lpRepGifts");Scribe_Collections.Look(ref stocked,"lpRepStocked",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){rep=rep??new Dictionary<int,int>();giftAccum=giftAccum??new Dictionary<int,int>();aidTick=aidTick??new Dictionary<int,int>();stocked=stocked??new List<int>();}
  }
 }
 // 黑网商队：每次 5% 带一本随机典籍（市价 15000，买价约 20000）。
 public class StockGenerator_RandomTome : StockGenerator {
  public float chance=.05f;
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){if(Rand.Chance(chance)){var t=Growth.MakeTome();if(t!=null)yield return t;}}
  public override bool HandlesThingDef(ThingDef d)=>Growth.TomeIndex(d)>=0;
 }

 // ==================== Harmony ====================
 // 声望：完成派系任务 +3。
 [HarmonyPatch(typeof(Quest),nameof(Quest.End))]
 public static class GrowthQuestRep {static void Postfix(Quest __instance,QuestEndOutcome outcome){if(outcome!=QuestEndOutcome.Success || !Growth.RepOn)return;foreach(var f in __instance.InvolvedFactions.Where(Growth.Tracked).Distinct().ToList())Growth.Gain(f,Growth.QuestRep,"完成了任务："+__instance.name);}}
 // 声望：送礼（好感上升且原因是礼物）。
 [HarmonyPatch(typeof(Faction),nameof(Faction.TryAffectGoodwillWith))]
 public static class GrowthGiftRep {
  static void Postfix(Faction __instance,Faction __0,int __1,HistoryEventDef __4,bool __result){
   if(!__result || __1<=0 || __0==null || !__0.IsPlayer || __4==null || !Growth.Tracked(__instance))return;
   if(__4.defName.IndexOf("Gift",StringComparison.OrdinalIgnoreCase)<0)return;
   Growth.Gift(__instance,__1);
  }
 }
 // 声望一级：该派系商队卖给你的东西 -5%。
 [HarmonyPatch(typeof(Tradeable),nameof(Tradeable.GetPriceFor))]
 public static class GrowthTradeRep {static void Postfix(TradeAction __0,ref float __result){if(__0!=TradeAction.PlayerBuys || !Growth.RepOn)return;var f=TradeSession.trader?.Faction;if(f==null)return;float k=Growth.PriceFactor(f,__0);if(k!=1f)__result*=k;}}
 // 声望三级：请求援兵冷却减半；商队带一件专属物品（每个商人一次）。
 [HarmonyPatch(typeof(FactionDialogMaker),"CallForAid")]
 public static class GrowthAidRep {static void Postfix(Faction __1){if(Growth.RepOn && Growth.Tracked(__1) && Growth.Rank(__1)>=3)__1.lastMilitaryAidRequestTick-=Growth.AidCooldown/2;}}
 [HarmonyPatch(typeof(TradeSession),nameof(TradeSession.SetupWith))]
 public static class GrowthStockRep {
  static void Prefix(ITrader __0){
   if(!Growth.RepOn || !(__0 is Pawn tp) || tp.inventory==null)return;var f=tp.Faction;var s=Growth.State;
   if(s==null || !Growth.Tracked(f) || Growth.Rank(f)<3 || s.stocked.Contains(tp.thingIDNumber))return;
   if(!Growth.RepItems.TryGetValue(f.def.defName,out var name))return;var d=DefDatabase<ThingDef>.GetNamedSilentFail(name);if(d==null)return;
   s.stocked.Add(tp.thingIDNumber);var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Outsider);tp.inventory.innerContainer.TryAdd(t);
  }
 }
 // 派系信息里写声望和头衔。
 [HarmonyPatch(typeof(Faction),nameof(Faction.GetInfoText))]
 public static class GrowthRepInfo {static void Postfix(Faction __instance,ref string __result){if(!Growth.RepOn || !Growth.Tracked(__instance))return;__result+="\n\n殖民地声望："+Growth.Rep(__instance)+" / "+Growth.RepMax+"（头衔："+Growth.Title(__instance)+"）";}}

 // ---------- 设置（画在“DND 特性”分组里） ----------
 public partial class Settings {
  public bool growthGrind=true,factionReputation=true;
  void ExposeGrowth(){Scribe_Values.Look(ref growthGrind,"lpGrowthGrind",true);Scribe_Values.Look(ref factionReputation,"lpFactionReputation",true);}
 }
}
