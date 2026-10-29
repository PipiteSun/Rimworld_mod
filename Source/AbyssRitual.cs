using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using UnityEngine;

namespace LichPhylactery {
 // 1.54.0 深渊仪式（规格第五节）：深渊法阵 → 选目标 → 选主祭 → 主祭把祭品（尸体 / 囚犯）扛到法阵中心，念 2 小时（四阶 6 小时）→ 掷骰。
 // 灵魂币、恶魔血、神迹残片直接从地图上的存货里扣（不另外搬运）。护阵者站在法阵四角，每人 +4%。
 public class AbyssTarget {
  public string key,label,kind,demon;public int tier,thrallDays,count=1,startLevel;public bool bind,familiar,swarm,needHAR;public Gender? gender;
  public string Mode=>familiar?"魔宠（永久）":swarm?"3～6 只仆从，"+thrallDays+" 天":bind?"束缚（永久殖民者）":"仆从 "+thrallDays+" 天";
 }
 public class AbyssOffering {
  public int corpses,prisoners,coins,blood,shards;public bool mortgage;
  public string Label{get{
   if(mortgage)return "灵魂抵押";var l=new List<string>();
   if(corpses>0)l.Add(corpses+" 具人形尸体");if(prisoners>0)l.Add(prisoners+" 名囚犯");if(coins>0)l.Add(coins+" 灵魂币");if(blood>0)l.Add(blood+" 恶魔血");if(shards>0)l.Add(shards+" 神迹残片");
   return string.Join(" + ",l);
  }}
 }
 public enum RitualResult{Success,Unbound,Rift}

 public static class AbyssRitual {
  public static readonly float[] BaseChance={.85f,.7f,.55f,.35f};
  public static readonly int[] ChantTicks={GenDate.TicksPerHour*2,GenDate.TicksPerHour*2,GenDate.TicksPerHour*2,GenDate.TicksPerHour*6};
  public const float PerMod=.04f,PerProf=.02f,PerWard=.04f,ScrollBonus=.1f,SilverBonus=.05f,MarilithBonus=.1f,LichBonus=.05f,TieflingBonus=.05f,PerSocial=.005f,SocialCap=.1f,MinChance=.05f,MaxChance=.95f,UnboundMargin=.2f,MinQuality=.4f;
  public const int MaxWards=3,RefineCoins=3,RefineCooldown=GenDate.TicksPerDay,SilverEdgeCost=40;
  public static float? TestRoll;
  static ResearchProjectDef research;public static ResearchProjectDef Research=>research??(research=DefDatabase<ResearchProjectDef>.GetNamedSilentFail("LP_Research_Abyssology"));
  public static readonly List<AbyssTarget> Targets=new List<AbyssTarget>{
   new AbyssTarget{key="dretch",label="劣魔群",kind="LP_AH_Dretch",tier=1,thrallDays=3,swarm=true},
   new AbyssTarget{key="quasit",label="夸塞魔",kind="LP_QuasitFamiliar",tier=1,familiar=true},
   new AbyssTarget{key="barlgura",label="巴路古拉",kind="LP_AH_Barlgura",tier=2,thrallDays=5,demon="barlgura"},
   new AbyssTarget{key="barlguraBind",label="巴路古拉",kind="LP_AH_Barlgura",tier=2,bind=true,startLevel=5,demon="barlgura"},
   new AbyssTarget{key="hezrou",label="哈兹魔",kind="LP_AH_Hezrou",tier=2,thrallDays=5,demon="hezrou"},
   new AbyssTarget{key="hezrouBind",label="哈兹魔",kind="LP_AH_Hezrou",tier=2,bind=true,startLevel=7,demon="hezrou"},
   new AbyssTarget{key="succubus",label="魅魔",kind="LP_AH_Succubus",tier=3,bind=true,startLevel=8,demon="succubus",gender=Gender.Female},
   new AbyssTarget{key="incubus",label="梦魔",kind="LP_AH_Succubus",tier=3,bind=true,startLevel=8,demon="succubus",gender=Gender.Male},
   new AbyssTarget{key="nabassu",label="纳巴苏",kind="LP_AH_Nabassu",tier=3,bind=true,startLevel=10,demon="nabassu"},
   new AbyssTarget{key="marilith",label="六臂蛇魔",kind="LP_MarilithColonist",tier=3,bind=true,startLevel=10,demon="marilith",needHAR=true},
   new AbyssTarget{key="balor",label="炎魔",kind="LP_FH_Balor",tier=4,thrallDays=1,demon="balor"},
  };
  public static AbyssTarget Target(string key)=>Targets.FirstOrDefault(t=>t.key==key);
  public static string TierLabel(int t)=>t==1?"一阶":t==2?"二阶":t==3?"三阶":"四阶";
  public static readonly string[] DemonKeys={"barlgura","hezrou","succubus","nabassu","marilith","balor"};
  public static string DemonLabel(string d){switch(d){case "barlgura":return "巴路古拉";case "hezrou":return "哈兹魔";case "succubus":return "魅魔";case "nabassu":return "纳巴苏";case "marilith":return "六臂蛇魔";case "balor":return "炎魔";}return d;}
  public static List<AbyssOffering> Offerings(int tier){
   switch(tier){
    case 1:return new List<AbyssOffering>{new AbyssOffering{corpses=1},new AbyssOffering{coins=2}};
    case 2:return new List<AbyssOffering>{new AbyssOffering{prisoners=1},new AbyssOffering{coins=6,blood=1}};
    case 3:return new List<AbyssOffering>{new AbyssOffering{prisoners=1,coins=15},new AbyssOffering{mortgage=true}};
    default:return new List<AbyssOffering>{new AbyssOffering{prisoners=3,shards=1}};
   }
  }

  // ---------- 祭品 ----------
  public static bool OfferableCorpse(Thing t)=>t is Corpse c && c.InnerPawn!=null && c.InnerPawn.RaceProps.Humanlike && !AbyssRaces.SoulGone(c.InnerPawn) && c.GetRotStage()!=RotStage.Dessicated && !Outsiders.Is(c.InnerPawn);
  public static bool OfferablePrisoner(Pawn p)=>p!=null && p.Spawned && !p.Dead && p.IsPrisonerOfColony && p.RaceProps.Humanlike && !Outsiders.Is(p);
  public static int Count(Map map,ThingDef def)=>def==null || map==null?0:map.listerThings.ThingsOfDef(def).Sum(t=>t.stackCount);
  public static IEnumerable<Corpse> Corpses(Map map)=>map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Where(OfferableCorpse).Cast<Corpse>();
  public static IEnumerable<Pawn> Prisoners(Map map)=>map.mapPawns.PrisonersOfColonySpawned.Where(OfferablePrisoner);
  public static string Missing(Map map,AbyssOffering o){
   if(o.mortgage)return map.mapPawns.FreeColonistsSpawned.Any(c=>!c.Dead && !AbyssRaces.SoulGone(c))?null:"没有能抵押灵魂的殖民者";
   if(Corpses(map).Count()<o.corpses)return "需要 "+o.corpses+" 具人形尸体";
   if(Prisoners(map).Count()<o.prisoners)return "需要 "+o.prisoners+" 名囚犯";
   if(Count(map,AbyssItems.SoulCoin)<o.coins)return "需要 "+o.coins+" 灵魂币";
   if(Count(map,AbyssItems.DemonBlood)<o.blood)return "需要 "+o.blood+" 恶魔血";
   if(Faith.ShardsOn(map)<o.shards)return "需要 "+o.shards+" 神迹残片";
   return null;
  }
  public static bool TakeItems(Map map,ThingDef def,int n){
   if(n<=0)return true;if(Count(map,def)<n)return false;
   foreach(var t in map.listerThings.ThingsOfDef(def).ToList()){if(n<=0)break;int take=Math.Min(n,t.stackCount);t.SplitOff(take).Destroy();n-=take;}
   return true;
  }

  // ---------- 目标是否可选 ----------
  public static string Unavailable(Building_AbyssCircle circle,AbyssTarget t){
   if(!AbyssSettings.Enabled)return "深渊内容已关闭";
   if(t.needHAR && !Marilith.Loaded)return "需要 Humanoid Alien Races";
   if(DefDatabase<PawnKindDef>.GetNamedSilentFail(t.kind)==null)return "缺少兵种定义";
   if(!AbyssSettings.BindColonists && (t.bind || t.tier==3))return "设置里关掉了“允许束缚恶魔成为殖民者”";
   if(t.tier>=2 && t.demon!=null && AbyssState.Get?.Known(t.demon)!=true)return "需要读过记载"+DemonLabel(t.demon)+"的恶魔学典籍";
   if(t.bind && AbyssBinding.BoundCount()>=AbyssBinding.BoundCap())return "束缚者已满（上限 "+AbyssBinding.BoundCap()+"）";
   if(!t.bind && !t.familiar && AbyssBinding.ThrallCount()>=AbyssBinding.ThrallCap)return "仆从已满（上限 "+AbyssBinding.ThrallCap+"）";
   var map=circle.Map;if(Offerings(t.tier).All(o=>Missing(map,o)!=null))return Missing(map,Offerings(t.tier)[0]);
   return null;
  }
  public static string CannotLead(Pawn p,AbyssTarget t){
   if(p==null || p.Dead || p.Downed || !p.RaceProps.Humanlike)return "不能主持";
   if(p.InMentalState || p.Drafted)return p.LabelShort+"现在不能主持";
   if(p.WorkTagIsDisabled(WorkTags.Violent) && t.tier>=2)return p.LabelShort+"下不了手（无暴力）";
   if(FaithRaces.IsAasimar(p) && AbyssTaint.Severity(p)<AbyssTaint.AasimarLeaderMin)return p.LabelShort+"拒绝了（阿斯莫）";
   if(t.familiar && Quasits.Of(p)!=null)return p.LabelShort+"已经有一只夸塞魔了";
   return null;
  }

  // ---------- 成功率（规格 5.4） ----------
  public static float Chance(Building_AbyssCircle circle,AbyssTarget t,Pawn leader,int wards){
   float c=BaseChance[Mathf.Clamp(t.tier-1,0,3)];
   c+=(Legendary.Mod(leader,Adventurer.Cha)+Legendary.Mod(leader,Adventurer.Int))*PerMod;
   c+=Legendary.Proficiency(Legendary.LevelOf(leader))*PerProf;
   c+=Mathf.Min(MaxWards,wards)*PerWard;
   if(circle!=null && TrueNames.HeldKind(circle.Map,KindKeyOf(t)))c+=ScrollBonus;
   if(circle!=null && circle.silverEdge)c+=SilverBonus;
   if(Marilith.Is(leader))c+=MarilithBonus;else if(Items.IsLich(leader))c+=LichBonus;else if(FaithRaces.IsTiefling(leader))c+=TieflingBonus;
   c+=Mathf.Min(SocialCap,(leader?.skills?.GetSkill(SkillDefOf.Social)?.Level??0)*PerSocial);
   if(circle!=null)c-=Thinness.Get(circle.Map)/1000f;
   return Mathf.Clamp(c,MinChance,MaxChance);
  }
  public static string KindKeyOf(AbyssTarget t){
   switch(t.key){case "dretch":return AbyssRaces.Dretch;case "quasit":return AbyssRaces.Quasit;case "barlgura":case "barlguraBind":return AbyssRaces.Barlgura;case "hezrou":case "hezrouBind":return AbyssRaces.Hezrou;case "succubus":case "incubus":return AbyssRaces.Succubus;case "nabassu":return AbyssRaces.Nabassu;case "marilith":return "LP_Marilith";case "balor":return "LP_Balor";}
   return null;
  }
  public static RitualResult Judge(float chance,float roll)=>roll<=chance?RitualResult.Success:roll-chance<UnboundMargin?RitualResult.Unbound:RitualResult.Rift;
  public static float Quality(float chance,float roll)=>Mathf.Clamp(MinQuality+(chance-roll)*2f,MinQuality,1f);

  // ---------- 结算 ----------
  public static RitualResult Resolve(Building_AbyssCircle circle,Pawn leader,List<Pawn> wards){
   var t=Target(circle.targetKey);var map=circle.Map;var center=circle.Center;
   float chance=Chance(circle,t,leader,wards.Count),roll=TestRoll??Rand.Value;
   var res=Judge(chance,roll);float q=Quality(chance,roll);
   Consume(circle,leader);
   circle.lastRoll=roll;circle.lastChance=chance;circle.lastResult=res;
   int tier=Mathf.Clamp(t.tier,1,4);
   Thinness.Add(map,Thinness.RitualGain[tier-1]*(res==RitualResult.Success?1f:2f),"ritual");
   AbyssTaint.Add(leader,AbyssTaint.RitualGain[tier-1],"深渊仪式");
   foreach(var w in wards)AbyssTaint.Add(w,AbyssTaint.RitualGain[tier-1]/2f,"深渊仪式护阵");
   for(int i=0;i<8;i++)FleckMaker.ThrowSmoke(center.ToVector3Shifted()+new Vector3(Rand.Range(-1.5f,1.5f),0,Rand.Range(-1.5f,1.5f)),map,2.5f);
   FleckMaker.Static(center,map,FleckDefOf.PsycastAreaEffect,4f);
   switch(res){
    case RitualResult.Success:Summon(circle,t,leader,q,false);break;
    case RitualResult.Unbound:Summon(circle,t,leader,q,true);break;
    case RitualResult.Rift:{
     var cell=RiftSpot(map,center);var plane=AbyssItems.AbyssPlane;
     if(cell.IsValid && plane!=null){circle.lockRift=PortalEvent.Open(map,cell,plane,StorytellerUtility.DefaultThreatPointsNow(map),tier==4);}
     Find.LetterStack.ReceiveLetter("深渊仪式失败：裂口","仪式彻底失控了（成功率 "+chance.ToStringPercent()+"，本次结果 "+roll.ToStringPercent()+"）：法阵旁撕开了一道通往深渊的裂口！"+(tier==4?"第一波里就有炎魔。":"")+"\n\n裂口关上之前法阵不能再用。关闭方法：打掉它、驱散粉尘、巨灵或六臂蛇人命令、3 格内落下 5 瓶圣水，或者用真名卷。",LetterDefOf.ThreatBig,new LookTargets(circle.lockRift!=null?(Thing)circle.lockRift:circle));
     break;
    }
   }
   circle.Finish();
   return res;
  }
  static IntVec3 RiftSpot(Map map,IntVec3 center){
   var circle=CellRect.CenteredOn(center,2);
   foreach(var c in GenRadial.RadialCellsAround(center,10f,false)){
    var rect=new CellRect(c.x,c.z,2,2);if(rect.Overlaps(circle))continue;
    if(rect.Cells.All(x=>x.InBounds(map) && x.Standable(map) && x.GetEdifice(map)==null && x.GetFirstPawn(map)==null && x.GetFirstItem(map)==null))return c;
   }
   return PortalEvent.TryFindSpot(map,out var any)?any:IntVec3.Invalid;
  }
  static void Consume(Building_AbyssCircle circle,Pawn leader){
   var o=circle.offering;var map=circle.Map;if(o==null)return;
   if(o.mortgage){var m=circle.mortgaged;if(m!=null && !m.Dead){AbyssTaint.Add(m,AbyssTaint.MortgageGain,"灵魂抵押");var d=Items.Hediff("LP_SoulMortgaged");if(d!=null && !Items.Has(m,d))m.health.AddHediff(d);}return;}
   foreach(var p in circle.OnCircle<Pawn>().Where(OfferablePrisoner).Take(o.prisoners).ToList())ExecutionUtility.DoExecutionByCut(leader,p,8,true);
   foreach(var c in circle.OnCircle<Corpse>().Where(OfferableCorpse).Take(o.corpses).ToList())c.Destroy();
   TakeItems(map,AbyssItems.SoulCoin,o.coins);TakeItems(map,AbyssItems.DemonBlood,o.blood);if(o.shards>0)Faith.TakeShards(map,o.shards);
  }
  static void Summon(Building_AbyssCircle circle,AbyssTarget t,Pawn leader,float q,bool hostile){
   var map=circle.Map;var center=circle.Center;var player=Faction.OfPlayer;var horde=AbyssRaces.Horde();
   var made=new List<Pawn>();
   if(t.swarm){
    int n=hostile?Rand.RangeInclusive(2,4):Mathf.Min(Rand.RangeInclusive(3,6),Mathf.Max(1,AbyssBinding.ThrallCap-AbyssBinding.ThrallCount()));
    for(int i=0;i<n;i++){var p=AbyssRaces.Make(t.kind,hostile?horde:player,map.Tile);if(p!=null)made.Add(p);}
   }else if(t.familiar && !hostile){var qz=Quasits.Summon(leader,CellFinder.RandomClosewalkCellNear(center,map,2),map);if(qz!=null)made.Add(qz);}
   else{var p=AbyssRaces.Make(t.kind,hostile?horde:player,map.Tile,t.gender);if(p!=null)made.Add(p);}
   foreach(var p in made.Where(x=>!x.Spawned))GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(center,map,2),map);
   string who=t.label;
   if(hostile){
    if(horde!=null && made.Count>0)LordMaker.MakeNewLord(horde,new LordJob_AssaultColony(horde,false,false,false,false,false,false,false),map,made.Where(x=>x.RaceProps.Humanlike).ToList());
    Find.LetterStack.ReceiveLetter("深渊仪式失败：没能束缚",who+"应召而来，可束缚的咒文却没有扣上（成功率 "+circle.lastChance.ToStringPercent()+"，本次结果 "+circle.lastRoll.ToStringPercent()+"）。它不属于任何人，就地朝殖民地扑了过来！",LetterDefOf.ThreatBig,new LookTargets(made.Cast<Thing>()));
    return;
   }
   foreach(var p in made){
    if(t.familiar)continue;
    if(t.bind){var h=AbyssBinding.Bind(p,leader,Mathf.Max(AbyssBinding.MinStart,q*100f));AbyssBinding.SetLevel(p,t.startLevel,true);}
    else AbyssBinding.MakeThrall(p,t.thrallDays*GenDate.TicksPerDay);
   }
   string what=t.familiar?"成为"+leader.LabelShort+"的魔宠（永久，可以跟随主人）":t.bind?"被束缚为殖民者：初始服从度 "+Mathf.Max(AbyssBinding.MinStart,q*100f).ToString("0")+"，主人是"+leader.LabelShort+"。服从度每天会下降，降到 0 它会挣脱":"作为仆从听候差遣 "+t.thrallDays+" 天，时间到了就会被遣返";
   Find.LetterStack.ReceiveLetter("深渊仪式成功："+who,leader.LabelShort+"念完了最后一个音节（成功率 "+circle.lastChance.ToStringPercent()+"，本次结果 "+circle.lastRoll.ToStringPercent()+"）。"+(made.Count>1?made.Count+"只":"")+who+"从黑烟里走了出来，"+what+"。",LetterDefOf.PositiveEvent,new LookTargets(made.Cast<Thing>()));
  }

  // ---------- 束缚法阵 ----------
  public static bool InBindingCircle(Thing t){
   var map=t?.MapHeld;if(map==null || AbyssItems.BindingCircle==null)return false;
   foreach(var c in map.listerThings.ThingsOfDef(AbyssItems.BindingCircle))if(c.OccupiedRect().ExpandedBy(1).Contains(t.Position))return true;
   return false;
  }
  public static bool CircleRoom(Pawn p){
   var room=p?.GetRoom();if(room==null || room.PsychologicallyOutdoors || AbyssItems.BindingCircle==null)return false;
   foreach(var c in p.Map.listerThings.ThingsOfDef(AbyssItems.BindingCircle))if(c.Position.GetRoom(p.Map)==room)return true;
   return false;
  }
  // 没倒地的囚犯：主祭先把他制住（深渊咒缚，倒地 6 小时）再扛走。
  public static void Subdue(Pawn p){if(p!=null && !p.Downed && !p.Dead)Items.Give(p,"LP_RitualStupor");}
 }

 // ---------- 深渊法阵 ----------
 public class Building_AbyssCircle : Building {
  public string targetKey;public Pawn leader,mortgaged;public AbyssOffering offering;public bool active,chanting,silverEdge;public int refineReady;
  public Building_PlanarRift lockRift;public float lastRoll,lastChance;public RitualResult lastResult;
  int offerIndex;
  public IntVec3 Center=>Position;
  public bool Locked=>lockRift!=null && lockRift.Spawned && !lockRift.Destroyed;
  public IEnumerable<T> OnCircle<T>() where T:Thing{var map=Map;foreach(var c in this.OccupiedRect())foreach(var t in c.GetThingList(map).ToList())if(t is T x)yield return x;}
  public List<Pawn> Wards(){
   var corners=Corners();var job=AbyssItems.WardJob;
   return Map.mapPawns.FreeColonistsSpawned.Where(p=>p!=leader && p.CurJobDef==job && p.CurJob.targetA.Thing==this && corners.Contains(p.Position)).Take(AbyssRitual.MaxWards).ToList();
  }
  public List<IntVec3> Corners(){var r=this.OccupiedRect();return new List<IntVec3>{new IntVec3(r.minX,0,r.minZ),new IntVec3(r.maxX,0,r.minZ),new IntVec3(r.minX,0,r.maxZ),new IntVec3(r.maxX,0,r.maxZ)};}
  Graphic silver;
  public override Graphic Graphic{get{if(!silverEdge)return base.Graphic;return silver??(silver=GraphicDatabase.Get<Graphic_Single>(def.graphicData.texPath+"Silver",ShaderDatabase.Transparent,def.graphicData.drawSize,Color.white));}}
  public void Begin(AbyssTarget t,Pawn lead,AbyssOffering o,Pawn mort){
   targetKey=t.key;leader=lead;offering=o;mortgaged=mort;active=true;chanting=false;
   Thinness.Activate(Map);
   lead.jobs.TryTakeOrderedJob(JobMaker.MakeJob(AbyssItems.RitualJob,this),JobTag.Misc);
  }
  public void Finish(){active=false;chanting=false;targetKey=null;leader=null;offering=null;mortgaged=null;}
  // 下一件要扛到法阵上的祭品（法阵上已经有的不算）。
  public Thing NextOffering(Pawn p){
   var o=offering;if(o==null || o.mortgage)return null;
   int haveC=OnCircle<Corpse>().Count(AbyssRitual.OfferableCorpse),haveP=OnCircle<Pawn>().Count(AbyssRitual.OfferablePrisoner);
   var rect=this.OccupiedRect();
   if(haveP<o.prisoners)return AbyssRitual.Prisoners(Map).Where(x=>!rect.Contains(x.Position) && p.CanReserveAndReach(x,PathEndMode.Touch,Danger.Deadly)).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
   if(haveC<o.corpses)return AbyssRitual.Corpses(Map).Where(x=>!rect.Contains(x.Position) && !x.IsForbidden(p) && p.CanReserveAndReach(x,PathEndMode.ClosestTouch,Danger.Deadly)).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault();
   return null;
  }
  public string StillMissing(){
   var o=offering;if(o==null)return "没有选定祭品";if(o.mortgage)return null;
   if(OnCircle<Pawn>().Count(AbyssRitual.OfferablePrisoner)<o.prisoners)return "法阵上的囚犯不够";
   if(OnCircle<Corpse>().Count(AbyssRitual.OfferableCorpse)<o.corpses)return "法阵上的尸体不够";
   var map=Map;if(AbyssRitual.Count(map,AbyssItems.SoulCoin)<o.coins || AbyssRitual.Count(map,AbyssItems.DemonBlood)<o.blood || Faith.ShardsOn(map)<o.shards)return "地图上的灵魂币 / 恶魔血 / 神迹残片不够了";
   return null;
  }
  // 主祭被打断（倒地、征召、精神崩溃）：念咒阶段的一阶以上仪式按“裂口”处理。
  public void Interrupted(Pawn p){
   var t=AbyssRitual.Target(targetKey);bool chant=chanting;Finish();
   if(!chant || t==null)return;
   if(t.tier<=1){Messages.Message("深渊仪式被打断了，咒文散去。",this,MessageTypeDefOf.NegativeEvent,false);return;}
   Thinness.Add(Map,Thinness.RitualGain[t.tier-1]*2f,"ritual");
   var cell=AbyssRitualSpot();var plane=AbyssItems.AbyssPlane;
   if(cell.IsValid && plane!=null)lockRift=PortalEvent.Open(Map,cell,plane,StorytellerUtility.DefaultThreatPointsNow(Map),t.tier==4);
   Find.LetterStack.ReceiveLetter("深渊仪式被打断",p?.LabelShort+"念到一半被打断了：没念完的咒文在法阵旁撕开了一道深渊裂口！",LetterDefOf.ThreatBig,new LookTargets(lockRift!=null?(Thing)lockRift:this));
  }
  IntVec3 AbyssRitualSpot(){
   foreach(var c in GenRadial.RadialCellsAround(Position,10f,false)){var rect=new CellRect(c.x,c.z,2,2);if(rect.Overlaps(this.OccupiedRect()))continue;if(rect.Cells.All(x=>x.InBounds(Map) && x.Standable(Map) && x.GetEdifice(Map)==null && x.GetFirstPawn(Map)==null))return c;}
   return IntVec3.Invalid;
  }
  public override string GetInspectString(){
   var s=base.GetInspectString();var lines=new List<string>();if(!s.NullOrEmpty())lines.Add(s);
   lines.Add("位面薄弱度 "+Thinness.Get(Map).ToString("0")+(silverEdge?"　银边（成功率 +5%）":""));
   if(Locked)lines.Add("深渊裂口没关之前不能举行仪式。");
   if(active){var t=AbyssRitual.Target(targetKey);lines.Add("正在举行："+t?.label+"（"+t?.Mode+"），主祭 "+leader?.LabelShort+(chanting?"，念咒中":"，准备祭品中"));}
   var known=AbyssState.Get?.known;if(known!=null && known.Count>0)lines.Add("已知恶魔："+string.Join("、",known.Select(AbyssRitual.DemonLabel)));
   return string.Join("\n",lines);
  }
  public override IEnumerable<Gizmo> GetGizmos(){
   foreach(var g in base.GetGizmos())yield return g;
   if(Faction!=Faction.OfPlayer)yield break;
   var start=new Command_Action{defaultLabel="开始深渊仪式",defaultDesc="选择要召唤的恶魔和主祭。成功率 = 阶基础 + (魅力+智力调整)×4% + 熟练×2% + 护阵者×4% + 银边 5% + 同种真名卷 10% + 种族加值 + 社交每级 0.5% - 薄弱度÷10%。",icon=ContentFinder<Texture2D>.Get("Abyss/Abilities/Ritual",false)??BaseContent.BadTex,action=OpenTargets};
   if(active)start.Disable("仪式正在进行");else if(Locked)start.Disable("深渊裂口还开着");else if(!AbyssSettings.Enabled)start.Disable("深渊内容已关闭");
   yield return start;
   if(active)yield return new Command_Action{defaultLabel="取消仪式",defaultDesc="中止正在准备的仪式（念咒阶段取消等同于被打断）。",icon=TexCommand.ClearPrioritizedWork,action=()=>{var l=leader;if(l?.CurJobDef==AbyssItems.RitualJob)l.jobs.EndCurrentJob(JobCondition.InterruptForced);else Finish();}};
   var refine=new Command_Action{defaultLabel="炼魂",defaultDesc="把一名囚犯带到法阵上处决，得到 3 枚灵魂币。执行者腐化 +5%，薄弱度 +3；冷却 1 天。",icon=ContentFinder<Texture2D>.Get("Items/Abyss/SoulCoin",false)??BaseContent.BadTex,action=OpenRefine};
   if(active)refine.Disable("仪式正在进行");else if(Find.TickManager.TicksGame<refineReady)refine.Disable("冷却中（还要 "+(refineReady-Find.TickManager.TicksGame).ToStringTicksToPeriod()+"）");
   yield return refine;
   if(!silverEdge){var edge=new Command_Action{defaultLabel="镶银边（"+AbyssRitual.SilverEdgeCost+" 银）",defaultDesc="给法阵描一圈银边：仪式成功率 +5%。直接从地图上的白银里扣。",icon=ContentFinder<Texture2D>.Get("Things/Item/Resource/Silver",false)??BaseContent.BadTex,action=()=>{if(AbyssRitual.TakeItems(Map,ThingDefOf.Silver,AbyssRitual.SilverEdgeCost)){silverEdge=true;DirtyMapMesh(Map);}}};
    if(AbyssRitual.Count(Map,ThingDefOf.Silver)<AbyssRitual.SilverEdgeCost)edge.Disable("地图上的白银不够");yield return edge;}
  }
  void OpenTargets(){
   var opts=new List<FloatMenuOption>();var colonists=Map.mapPawns.FreeColonistsSpawned.ToList();
   foreach(var t in AbyssRitual.Targets){
    if(t.needHAR && !Marilith.Loaded)continue; // 没装 HAR：菜单里不出六臂蛇魔
    var why=AbyssRitual.Unavailable(this,t);var best=colonists.Where(c=>AbyssRitual.CannotLead(c,t)==null).OrderByDescending(c=>AbyssRitual.Chance(this,t,c,0)).FirstOrDefault();
    string label=AbyssRitual.TierLabel(t.tier)+"·"+t.label+"（"+t.Mode+"）　祭品："+string.Join(" 或 ",AbyssRitual.Offerings(t.tier).Select(o=>o.Label))+(best!=null?"　成功率 "+AbyssRitual.Chance(this,t,best,0).ToStringPercent("0"):"");
    if(why==null && best==null)why="没有能主持的殖民者";
    var tt=t;opts.Add(why!=null?new FloatMenuOption(label+"（"+why+"）",null):new FloatMenuOption(label,()=>OpenLeaders(tt)));
   }
   Find.WindowStack.Add(new FloatMenu(opts));
  }
  void OpenLeaders(AbyssTarget t){
   var opts=new List<FloatMenuOption>();
   foreach(var c in Map.mapPawns.FreeColonistsSpawned.OrderByDescending(x=>AbyssRitual.Chance(this,t,x,0))){
    var why=AbyssRitual.CannotLead(c,t);var cc=c;
    opts.Add(why!=null?new FloatMenuOption(c.LabelShort+"（"+why+"）",null):new FloatMenuOption(c.LabelShort+"　成功率 "+AbyssRitual.Chance(this,t,c,0).ToStringPercent("0")+"（不算护阵者）",()=>OpenOfferings(t,cc)));
   }
   Find.WindowStack.Add(new FloatMenu(opts));
  }
  void OpenOfferings(AbyssTarget t,Pawn lead){
   var list=AbyssRitual.Offerings(t.tier).Where(o=>AbyssRitual.Missing(Map,o)==null).ToList();
   if(list.Count==1 && !list[0].mortgage){Begin(t,lead,list[0],null);return;}
   var opts=new List<FloatMenuOption>();
   foreach(var o in AbyssRitual.Offerings(t.tier)){
    var why=AbyssRitual.Missing(Map,o);var oo=o;
    if(why!=null){opts.Add(new FloatMenuOption(o.Label+"（"+why+"）",null));continue;}
    if(o.mortgage)opts.Add(new FloatMenuOption("灵魂抵押：选一名殖民者代替祭品",()=>OpenMortgage(t,lead,oo)));
    else opts.Add(new FloatMenuOption(o.Label,()=>Begin(t,lead,oo,null)));
   }
   Find.WindowStack.Add(new FloatMenu(opts));
  }
  void OpenMortgage(AbyssTarget t,Pawn lead,AbyssOffering o){
   var opts=new List<FloatMenuOption>();
   foreach(var c in Map.mapPawns.FreeColonistsSpawned.Where(x=>!AbyssRaces.SoulGone(x))){var cc=c;opts.Add(new FloatMenuOption(c.LabelShort+"（腐化 "+AbyssTaint.Severity(c).ToStringPercent("0")+"）",()=>
    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(cc.LabelShort+"的灵魂将抵押给深渊：腐化立刻 +30%，死后不能复活，尸体也会消失。可以在圣坛向善神“赎回灵魂”（30 神眷）。确定吗？",()=>Begin(t,lead,o,cc),true))));}
   Find.WindowStack.Add(new FloatMenu(opts));
  }
  void OpenRefine(){
   var opts=new List<FloatMenuOption>();
   var exec=Map.mapPawns.FreeColonistsSpawned.Where(c=>!c.Downed && !c.WorkTagIsDisabled(WorkTags.Violent) && !(FaithRaces.IsAasimar(c) && AbyssTaint.Severity(c)<AbyssTaint.AasimarLeaderMin)).OrderBy(c=>c.Position.DistanceToSquared(Position)).FirstOrDefault();
   foreach(var p in AbyssRitual.Prisoners(Map)){var pp=p;opts.Add(exec==null?new FloatMenuOption(p.LabelShort+"（没有能执行的殖民者）",null):new FloatMenuOption("炼魂："+p.LabelShort+"（执行者 "+exec.LabelShort+"）",()=>exec.jobs.TryTakeOrderedJob(JobMaker.MakeJob(AbyssItems.RefineJob,this,pp),JobTag.Misc)));}
   if(opts.Count==0)opts.Add(new FloatMenuOption("没有囚犯",null));
   Find.WindowStack.Add(new FloatMenu(opts));
  }
  public void Refine(Pawn exec,Pawn prisoner){
   if(prisoner==null || prisoner.Dead)return;
   ExecutionUtility.DoExecutionByCut(exec,prisoner,8,true);
   var c=ThingMaker.MakeThing(AbyssItems.SoulCoin);c.stackCount=AbyssRitual.RefineCoins;GenPlace.TryPlaceThing(c,Position,Map,ThingPlaceMode.Near);
   AbyssTaint.Add(exec,AbyssTaint.RefineGain,"炼魂");Thinness.Add(Map,Thinness.RefineGain,"refine");refineReady=Find.TickManager.TicksGame+AbyssRitual.RefineCooldown;
   Messages.Message(exec.LabelShort+"在深渊法阵上炼出了 "+AbyssRitual.RefineCoins+" 枚灵魂币。",this,MessageTypeDefOf.NeutralEvent,false);
  }
  public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn){
   foreach(var o in base.GetFloatMenuOptions(selPawn))yield return o;
   if(!active || selPawn==leader || AbyssItems.WardJob==null)yield break;
   if(Wards().Count>=AbyssRitual.MaxWards){yield return new FloatMenuOption("担任护阵者（已经有 3 人）",null);yield break;}
   yield return new FloatMenuOption("担任护阵者（成功率 +4%，腐化为主祭的一半）",()=>selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(AbyssItems.WardJob,this),JobTag.Misc));
  }
  public override void ExposeData(){
   base.ExposeData();
   Scribe_Values.Look(ref targetKey,"lpTarget");Scribe_References.Look(ref leader,"lpLeader");Scribe_References.Look(ref mortgaged,"lpMortgaged");
   Scribe_Values.Look(ref active,"lpActive");Scribe_Values.Look(ref chanting,"lpChanting");Scribe_Values.Look(ref silverEdge,"lpSilverEdge");Scribe_Values.Look(ref refineReady,"lpRefineReady");
   Scribe_References.Look(ref lockRift,"lpLockRift");Scribe_Values.Look(ref lastRoll,"lpLastRoll");Scribe_Values.Look(ref lastChance,"lpLastChance");
   if(Scribe.mode==LoadSaveMode.Saving)offerIndex=offering==null?-1:AbyssRitual.Offerings(AbyssRitual.Target(targetKey)?.tier??1).FindIndex(o=>o.Label==offering.Label);
   Scribe_Values.Look(ref offerIndex,"lpOffer",-1);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){var t=AbyssRitual.Target(targetKey);offering=t==null || offerIndex<0?null:AbyssRitual.Offerings(t.tier).ElementAtOrDefault(offerIndex);if(active && (leader==null || leader.CurJobDef!=AbyssItems.RitualJob))Finish();}
  }
 }
 public class Building_WardingGlyph : Building {
  public override void SpawnSetup(Map map,bool respawningAfterLoad){base.SpawnSetup(map,respawningAfterLoad);var r=MapComponent_Outsiders.Of(map);if(r!=null && !r.glyphs.Contains(this))r.glyphs.Add(this);}
  public override void DeSpawn(DestroyMode mode=DestroyMode.Vanish){MapComponent_Outsiders.Of(Map)?.glyphs.Remove(this);base.DeSpawn(mode);}
  public override void DrawExtraSelectionOverlays(){base.DrawExtraSelectionOverlays();GenDraw.DrawRadiusRing(Position,Outsiders.WardRadius);}
 }
 public class PlaceWorker_WardRadius : PlaceWorker {
  public override void DrawGhost(ThingDef def,IntVec3 center,Rot4 rot,Color ghostCol,Thing thing=null){GenDraw.DrawRadiusRing(center,Outsiders.WardRadius);}
 }
 public class AbyssLimitExtension : DefModExtension {public int max=1;}
 // 每张地图的数量上限（深渊法阵 1、束缚法阵 4）：已建成的 + 蓝图 + 框架。
 public class PlaceWorker_AbyssLimit : PlaceWorker {
  public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef,IntVec3 loc,Rot4 rot,Map map,Thing thingToIgnore=null,Thing thing=null){
   var def=checkingDef as ThingDef;int max=def?.GetModExtension<AbyssLimitExtension>()?.max??99;
   int n=map.listerThings.ThingsOfDef(def).Count+map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Count(t=>t.def.entityDefToBuild==def)+map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Count(t=>t.def.entityDefToBuild==def);
   return n>=max?new AcceptanceReport("每张地图最多 "+max+" 个"+def.label):AcceptanceReport.WasAccepted;
  }
 }

 // ---------- Job：仪式 / 护阵 / 炼魂 ----------
 public class JobDriver_AbyssRitual : JobDriver {
  Building_AbyssCircle Circle=>TargetA.Thing as Building_AbyssCircle;
  bool done;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  public override string GetReport()=>Circle?.chanting==true?"正在念诵深渊咒文。":"正在为深渊仪式准备祭品。";
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   this.FailOn(()=>Circle==null || !Circle.active || Circle.leader!=pawn);
   AddFinishAction(c=>{var cc=Circle;if(!done && cc!=null && cc.active && cc.leader==pawn)cc.Interrupted(pawn);});
   var chant=ToilMaker.MakeToil("AbyssChant");
   var find=ToilMaker.MakeToil("AbyssFind");
   find.initAction=()=>{var next=Circle.NextOffering(pawn);if(next==null){JumpToToil(chant);return;}job.SetTarget(TargetIndex.B,next);job.count=1;};
   yield return find;
   yield return Toils_Goto.GotoThing(TargetIndex.B,PathEndMode.ClosestTouch).FailOnDespawnedOrNull(TargetIndex.B);
   yield return Toils_General.Do(()=>{if(TargetB.Thing is Pawn pr)AbyssRitual.Subdue(pr);});
   yield return Toils_Haul.StartCarryThing(TargetIndex.B,false,true);
   yield return Toils_Goto.GotoCell(Circle.Center,PathEndMode.OnCell);
   yield return Toils_General.Do(()=>{pawn.carryTracker.TryDropCarriedThing(Circle.Center,ThingPlaceMode.Near,out _);});
   yield return Toils_Jump.Jump(find);
   yield return chant;
   chant.initAction=()=>{var miss=Circle.StillMissing();if(miss!=null){Messages.Message("深渊仪式没法开始："+miss+"。",Circle,MessageTypeDefOf.RejectInput,false);done=true;Circle.Finish();EndJobWith(JobCondition.Incompletable);return;}};
   var go=Toils_Goto.GotoCell(Circle.Center+IntVec3.South*3,PathEndMode.OnCell);
   yield return go;
   var wait=Toils_General.Wait(AbyssRitual.ChantTicks[Mathf.Clamp((AbyssRitual.Target(Circle.targetKey)?.tier??1)-1,0,3)]).WithProgressBarToilDelay(TargetIndex.A);
   wait.initAction=()=>{Circle.chanting=true;};
   wait.tickIntervalAction=d=>{if(pawn.IsHashIntervalTick(120,d))FleckMaker.Static(Circle.Center,Map,FleckDefOf.PsycastAreaEffect,2.5f);};
   wait.handlingFacing=true;
   yield return wait;
   yield return Toils_General.Do(()=>{done=true;AbyssRitual.Resolve(Circle,pawn,Circle.Wards());});
  }
 }
 public class JobDriver_AbyssWard : JobDriver {
  Building_AbyssCircle Circle=>TargetA.Thing as Building_AbyssCircle;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>true;
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   this.FailOn(()=>Circle==null || !Circle.active);
   var corner=ToilMaker.MakeToil("AbyssCorner");
   corner.initAction=()=>{var taken=Map.mapPawns.FreeColonistsSpawned.Where(p=>p!=pawn && p.CurJobDef==job.def).Select(p=>p.CurJob.targetB.Cell).ToList();var c=Circle.Corners().FirstOrDefault(x=>!taken.Contains(x) && x.Standable(Map));if(!c.IsValid){EndJobWith(JobCondition.Incompletable);return;}job.SetTarget(TargetIndex.B,c);};
   yield return corner;
   yield return Toils_Goto.GotoCell(TargetIndex.B,PathEndMode.OnCell);
   var stand=ToilMaker.MakeToil("AbyssWardStand");stand.defaultCompleteMode=ToilCompleteMode.Delay;stand.defaultDuration=GenDate.TicksPerHour*8;stand.handlingFacing=true;
   stand.tickIntervalAction=d=>pawn.rotationTracker.FaceCell(Circle.Center);
   yield return stand;
  }
 }
 public class JobDriver_AbyssRefine : JobDriver {
  Building_AbyssCircle Circle=>TargetA.Thing as Building_AbyssCircle;Pawn Prisoner=>TargetB.Thing as Pawn;
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetB,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);this.FailOn(()=>Prisoner==null || Prisoner.Dead);
   yield return Toils_Goto.GotoThing(TargetIndex.B,PathEndMode.ClosestTouch);
   yield return Toils_General.Do(()=>AbyssRitual.Subdue(Prisoner));
   yield return Toils_Haul.StartCarryThing(TargetIndex.B,false,true);
   yield return Toils_Goto.GotoCell(Circle.Center,PathEndMode.OnCell);
   yield return Toils_General.Do(()=>pawn.carryTracker.TryDropCarriedThing(Circle.Center,ThingPlaceMode.Near,out _));
   yield return Toils_General.Wait(300).WithProgressBarToilDelay(TargetIndex.A);
   yield return Toils_General.Do(()=>Circle.Refine(pawn,Prisoner));
  }
 }

 // ---------- 物品 ----------
 public static class AbyssItems {
  static ThingDef coin,blood,book,scroll,shackles,fang,circle,bindCircle,glyph;static JobDef ritual,ward,refine;static PortalPlaneDef plane;
  public static ThingDef SoulCoin=>coin??(coin=DefDatabase<ThingDef>.GetNamedSilentFail("LP_SoulCoin"));
  public static ThingDef DemonBlood=>blood??(blood=DefDatabase<ThingDef>.GetNamedSilentFail("LP_DemonBlood"));
  public static ThingDef Demonomicon=>book??(book=DefDatabase<ThingDef>.GetNamedSilentFail("LP_Demonomicon"));
  public static ThingDef TrueNameScroll=>scroll??(scroll=DefDatabase<ThingDef>.GetNamedSilentFail("LP_TrueNameScroll"));
  public static ThingDef Shackles=>shackles??(shackles=DefDatabase<ThingDef>.GetNamedSilentFail("LP_DimensionalShackles"));
  public static ThingDef GorgedFang=>fang??(fang=DefDatabase<ThingDef>.GetNamedSilentFail("LP_GorgedFang"));
  public static ThingDef AbyssCircle=>circle??(circle=DefDatabase<ThingDef>.GetNamedSilentFail("LP_AbyssCircle"));
  public static ThingDef BindingCircle=>bindCircle??(bindCircle=DefDatabase<ThingDef>.GetNamedSilentFail("LP_BindingCircle"));
  public static ThingDef WardingGlyph=>glyph??(glyph=DefDatabase<ThingDef>.GetNamedSilentFail("LP_WardingGlyph"));
  public static JobDef RitualJob=>ritual??(ritual=DefDatabase<JobDef>.GetNamedSilentFail("LP_AbyssRitual"));
  public static JobDef WardJob=>ward??(ward=DefDatabase<JobDef>.GetNamedSilentFail("LP_AbyssWard"));
  public static JobDef RefineJob=>refine??(refine=DefDatabase<JobDef>.GetNamedSilentFail("LP_AbyssRefine"));
  public static PortalPlaneDef AbyssPlane=>plane??(plane=DefDatabase<PortalPlaneDef>.GetNamedSilentFail("LP_Plane_Abyss"));
  public const float CoinLichFeed=Souls.BaseValue*.25f+Souls.PerSkillLevel*60f*.25f; // 献祭一具普通尸体（技能合计约 60）的 25%
  public const int ScreamCoins=10,BookTicks=GenDate.TicksPerHour*4;
  // 典籍记载的恶魔：巴路古拉 / 哈兹魔 / 魅魔 / 纳巴苏 / 六臂蛇魔 / 炎魔 = 3/3/2/2/1/0.5；没 HAR 不出六臂蛇魔。
  public static string RandomDemon(){
   var w=new List<(string d,float w)>{("barlgura",3f),("hezrou",3f),("succubus",2f),("nabassu",2f),("balor",.5f)};if(Marilith.Loaded)w.Add(("marilith",1f));
   return w.RandomElementByWeight(x=>x.w).d;
  }
 }
 public class Book_Demonomicon : Book {
  public string demon;public int readTicks;
  public override void PostMake(){base.PostMake();if(demon==null)demon=AbyssItems.RandomDemon();}
  public override string LabelNoCount=>def.label+"："+AbyssRitual.DemonLabel(demon??"?")+(this.TryGetQuality(out var q)?"（"+q.GetLabel()+"）":"");
  public override string GetInspectString(){var s=base.GetInspectString();return (s.NullOrEmpty()?"":s+"\n")+"记载："+AbyssRitual.DemonLabel(demon)+"　研读进度 "+((float)readTicks/AbyssItems.BookTicks).ToStringPercent("0")+(AbyssState.Get?.Known(demon)==true?"（殖民地已经知道）":"");}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref demon,"lpDemon");Scribe_Values.Look(ref readTicks,"lpReadTicks");}
 }
 public class BookOutcomeProperties_Demonomicon : BookOutcomeProperties {public override Type DoerClass=>typeof(BookOutcome_Demonomicon);}
 // 读：每小时腐化 +0.5%；这本书累计读满 4 小时 → 殖民地“已知恶魔”加这一种。
 public class BookOutcome_Demonomicon : BookOutcomeDoer {
  readonly Dictionary<int,int> last=new Dictionary<int,int>(),acc=new Dictionary<int,int>();readonly Dictionary<int,float> rem=new Dictionary<int,float>();
  public override bool DoesProvidesOutcome(Pawn reader)=>reader?.RaceProps?.Humanlike==true;
  public override void OnReadingTick(Pawn reader,float factor){
   if(reader==null || !(Parent is Book_Demonomicon b))return;int id=reader.thingIDNumber,now=Find.TickManager.TicksGame;
   int delta=last.TryGetValue(id,out int l) && now-l>0 && now-l<=300?now-l:0;last[id]=now;if(delta<=0)return;
   delta=ReadSpeed.Scale(reader,delta,rem);if(delta<=0)return; // 2026-10-28 × 阅读速度（研究速度）
   Feed(reader,b,delta);
  }
  public static void Feed(Pawn reader,Book_Demonomicon b,int delta){
   int before=b.readTicks;b.readTicks+=delta;
   int hours=b.readTicks/GenDate.TicksPerHour-before/GenDate.TicksPerHour;for(int i=0;i<hours;i++)AbyssTaint.Add(reader,AbyssTaint.ReadHourly,"恶魔学典籍");
   if(before<AbyssItems.BookTicks && b.readTicks>=AbyssItems.BookTicks){var st=AbyssState.Get;if(st!=null && !st.Known(b.demon)){st.Learn(b.demon);Find.LetterStack.ReceiveLetter("读懂了恶魔学典籍",reader.LabelShort+"读完了记载"+AbyssRitual.DemonLabel(b.demon)+"的恶魔学典籍：殖民地现在可以在深渊法阵上召唤"+AbyssRitual.DemonLabel(b.demon)+"了。",LetterDefOf.NeutralEvent,reader);}}
  }
  public override string GetBenefitsString(Pawn reader)=>"读满 4 小时：学会召唤书中记载的恶魔。阅读时每小时深渊腐化 +0.5%。\n"+ReadSpeed.Note;
 }
 // 灵魂币：巫妖吸收 → 命匣获得献祭一具尸体 25% 的经验。
 public class CompUseEffect_SoulCoin : CompUseEffect {
  public override void DoEffect(Pawn p){base.DoEffect(p);var box=Necro.Lich(p)?.box;if(box==null || box.Destroyed)return;box.Feed(AbyssItems.CoinLichFeed);Messages.Message(p.LabelShort+"吸收了一枚灵魂币里的灵魂：命匣 +"+AbyssItems.CoinLichFeed.ToString("0")+" 经验。",p,MessageTypeDefOf.PositiveEvent,false);}
  public override AcceptanceReport CanBeUsedBy(Pawn p)=>Necro.Lich(p)?.box!=null?true:new AcceptanceReport("只有有命匣的巫妖能吸收灵魂币");
 }
 // 背包里有 10 枚以上灵魂币：心情 -2。
 public class ThoughtWorker_SoulCoins : ThoughtWorker {
  protected override ThoughtState CurrentStateInternal(Pawn p){var inv=p.inventory?.innerContainer;if(inv==null || AbyssItems.SoulCoin==null || Items.IsLich(p))return false;int n=0;for(int i=0;i<inv.Count;i++)if(inv[i].def==AbyssItems.SoulCoin)n+=inv[i].stackCount;return n>=AbyssItems.ScreamCoins;}
 }
 // 抽恶魔血：戴锚链、关在有束缚法阵的房间里的外来者囚犯，每 5 天一次，不伤命。
 public class Recipe_ExtractDemonBlood : Recipe_Surgery {
  public override bool AvailableOnNow(Thing thing,BodyPartRecord part=null){
   if(!(thing is Pawn p) || !base.AvailableOnNow(thing,part))return false;
   return p.IsPrisonerOfColony && Outsiders.IsDemon(p) && Outsiders.Shackled(p) && !Items.Has(p,"LP_DemonBloodDrawn");
  }
  public override void ApplyOnPawn(Pawn pawn,BodyPartRecord part,Pawn billDoer,List<Thing> ingredients,Bill bill){
   if(!AbyssRitual.CircleRoom(pawn)){Messages.Message("要在有束缚法阵的房间里才能抽恶魔血。",pawn,MessageTypeDefOf.RejectInput,false);return;}
   var b=ThingMaker.MakeThing(AbyssItems.DemonBlood);GenPlace.TryPlaceThing(b,pawn.PositionHeld,pawn.MapHeld,ThingPlaceMode.Near);
   Items.Give(pawn,"LP_DemonBloodDrawn");
  }
 }

 // ---------- 位面锚链 / 审讯真名 ----------
 public class FloatMenuOptionProvider_AbyssShackle : FloatMenuOptionProvider {
  protected override bool Drafted=>true;protected override bool Undrafted=>true;protected override bool Multiselect=>false;
  protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn,FloatMenuContext context){
   var p=context.FirstSelectedPawn;if(p==null || clickedPawn==null || clickedPawn==p || !clickedPawn.Downed || clickedPawn.Dead || !Outsiders.Is(clickedPawn) || Outsiders.Shackled(clickedPawn) || clickedPawn.Faction?.IsPlayer==true)return null;
   var s=AbyssItems.Shackles==null?null:p.Map.listerThings.ThingsOfDef(AbyssItems.Shackles).Where(t=>!t.IsForbidden(p) && p.CanReserveAndReach(t,PathEndMode.ClosestTouch,Danger.Deadly)).OrderBy(t=>t.Position.DistanceToSquared(p.Position)).FirstOrDefault();
   if(s==null)return new FloatMenuOption("给"+clickedPawn.LabelShort+"戴上位面锚链（没有可用的锚链）",null);
   return new FloatMenuOption("给"+clickedPawn.LabelShort+"戴上位面锚链",()=>{var j=JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("LP_ApplyShackles"),s,clickedPawn);j.count=1;p.jobs.TryTakeOrderedJob(j,JobTag.Misc);});
  }
 }
 public class JobDriver_ApplyShackles : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.B);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
   yield return Toils_Haul.StartCarryThing(TargetIndex.A,false,true);
   yield return Toils_Goto.GotoThing(TargetIndex.B,PathEndMode.Touch);
   yield return Toils_General.Wait(120).WithProgressBarToilDelay(TargetIndex.B);
   yield return Toils_General.Do(()=>{var c=pawn.carryTracker.CarriedThing;if(c==null || c.def!=AbyssItems.Shackles)return;var one=c.stackCount>1?c.SplitOff(1):c;if(one==c)pawn.carryTracker.innerContainer.Remove(c);one.Destroy();Outsiders.Shackle(TargetB.Pawn,pawn);});
  }
 }
 public static class TrueNameInterrogation {
  public const float Chance=.15f;public const int Cooldown=GenDate.TicksPerHour*8;
  static PrisonerInteractionModeDef mode;public static PrisonerInteractionModeDef Mode=>mode??(mode=DefDatabase<PrisonerInteractionModeDef>.GetNamedSilentFail("LP_InterrogateTrueName"));
  public static float? TestRoll;
  public static bool Eligible(Pawn p)=>p!=null && p.IsPrisonerOfColony && Outsiders.Is(p) && Outsiders.Shackled(p);
  public static bool Ready(Pawn p){var st=AbyssState.Get;return st==null || !st.interrogated.TryGetValue(p.thingIDNumber,out int t) || Find.TickManager.TicksGame-t>=Cooldown;}
  // 一次审讯：15% 得到它的真名卷，然后自动切回“维持”。
  public static Thing Interrogate(Pawn warden,Pawn p){
   var st=AbyssState.Get;if(st!=null)st.interrogated[p.thingIDNumber]=Find.TickManager.TicksGame;
   if((TestRoll??Rand.Value)>=Chance){Messages.Message(p.LabelShort+"咬紧了牙关，没有说出真名。",p,MessageTypeDefOf.NeutralEvent,false);return null;}
   var s=TrueNames.MakeScroll(p);if(s==null)return null;GenPlace.TryPlaceThing(s,p.PositionHeld,p.MapHeld,ThingPlaceMode.Near);
   p.guest?.SetExclusiveInteraction(PrisonerInteractionModeDefOf.MaintainOnly);
   Messages.Message(warden?.LabelShort+"从"+p.LabelShort+"嘴里撬出了它的真名："+TrueNames.Of(p)+"。",s,MessageTypeDefOf.PositiveEvent,false);
   return s;
  }
 }
 public class WorkGiver_Warden_InterrogateTrueName : WorkGiver_Warden {
  public override Job JobOnThing(Pawn pawn,Thing t,bool forced=false){
   if(!ShouldTakeCareOfPrisoner(pawn,t))return null;var p=(Pawn)t;var m=TrueNameInterrogation.Mode;
   if(m==null || p.guest?.IsInteractionEnabled(m)!=true || !TrueNameInterrogation.Eligible(p) || !TrueNameInterrogation.Ready(p) || !p.Awake() || p.Downed && !p.InBed())return null;
   if(!pawn.CanReserveAndReach(p,PathEndMode.Touch,Danger.Some))return null;
   return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("LP_InterrogateTrueName"),p);
  }
 }
 public class JobDriver_InterrogateTrueName : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.A);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.Touch);
   yield return Toils_General.Wait(300).WithProgressBarToilDelay(TargetIndex.A);
   yield return Toils_General.Do(()=>TrueNameInterrogation.Interrogate(pawn,TargetA.Pawn));
  }
 }
 // 审讯真名只对戴锚链的外来者囚犯显示。
 [HarmonyPatch(typeof(ITab_Pawn_Visitor),"DrawExclusiveInteractionRow")]
 public static class InterrogateRowFilter {
  static bool Prefix(PrisonerInteractionModeDef __1){if(__1==null || __1!=TrueNameInterrogation.Mode)return true;var p=Find.Selector.SingleSelectedThing as Pawn;return TrueNameInterrogation.Eligible(p);}
 }
 // 戴锚链的外来者囚犯关在有束缚法阵的房间里不会越狱。
 [HarmonyPatch(typeof(PrisonBreakUtility),nameof(PrisonBreakUtility.CanParticipateInPrisonBreak))]
 public static class ShackledNoBreak {static void Postfix(Pawn pawn,ref bool __result){if(__result && Outsiders.Shackled(pawn) && AbyssRitual.CircleRoom(pawn))__result=false;}}
 // 深渊学研究：关掉深渊内容时隐藏。
 [HarmonyPatch(typeof(ResearchProjectDef),nameof(ResearchProjectDef.IsHidden),MethodType.Getter)]
 public static class AbyssResearchHidden {static void Postfix(ResearchProjectDef __instance,ref bool __result){if(!__result && __instance==AbyssRitual.Research && !AbyssSettings.Enabled)__result=true;}}
 // 投掷圣水（炼金圣水 / 祝圣圣水）落在深渊裂口 3 格内：计数。
 [HarmonyPatch(typeof(GenExplosion),nameof(GenExplosion.DoExplosion))]
 public static class HolyWaterRift {
  static ThingDef blessed;
  static void Prefix(IntVec3 __0,Map __1,DamageDef __3,ThingDef __9){
   if(__1==null || PortalEvent.RiftDef==null)return;
   if(blessed==null)blessed=DefDatabase<ThingDef>.GetNamedSilentFail("LP_Proj_BlessedWater");
   if(__3!=null && __3==ItemThrowables.Holy || __9!=null && __9==blessed)PortalEvent.HolyNear(__1,__0);
  }
 }
}
