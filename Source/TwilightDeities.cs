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
 // 1.28.0 八位神的灌注与附体技能（构思第三节，见 ArtSource/Events-1.28.0-design-extract.md）。
 // 灌注的触发概率和数值在黄昏之年里 × D（1.5 × 黄昏强度）；附体技能都是自动的（计时或事件钩子），不写 AI。
 public static class TwilightSkills {
  // 灌注
  public const float BaneDread=.15f,SurtrBurn=4f,SurtrIgnite=.1f,SurtrFireTaken=.5f,TyrBonus=.1f,LathanderUndead=1.25f,LathanderRise=.3f,LathanderHeal=3f,VlaakithDodge=.2f,VlaakithSword=1.2f,VlaakithStepCooldown=.5f,TalosEmp=.08f,MalarBleed=.15f,MalarBite=6f;
  public const int BaneDreadTicks=300,IlsviStunTicks=120,CrimeThreshold=5,DodgeRange=8,BaneInfusedRadius=8;
  // 附体
  public const float BaneFearRadius=12f,BaneLawRadius=20f,SurtrTrail=.2f,SurtrSweepRadius=3f,SurtrSweepDamage=20f,FurnaceStep=.05f,SurtrBurstRadius=4f,IlsviRadius=15f,IlsviEcho=1.15f,IlsviEchoRadius=20f,TyrCourtRadius=10f,LathanderAuraRadius=10f,LathanderAuraHeal=5f,LathanderBeam=20f,VlaakithBlade=8f,TalosGaleRadius=10f,MalarMelee=1.25f,MalarBacklashRadius=15f,SkillRange=25f;
  public const int BaneCommandTicks=1200,BaneCommandCount=3,SurtrSweepTicks=1500,FurnaceMax=10,SurtrBurstDamage=25,IlsviWhisperTicks=900,IlsviBerserkTicks=600,IlsviDevourBonus=60,TyrPunishTicks=1800,TyrPunishBase=10,TyrPunishPer=5,TyrPunishCap=60,LathanderBeamTicks=1800,VlaakithRaidTicks=1200,VlaakithRevenge=2,TalosLightningTicks=2400,TalosStormTicks=5000,MalarBacklashTicks=3600,MalarBacklashLength=1800,MalarHandlerSkill=10,AuraTicks=150;
  static int Now=>Find.TickManager.TicksGame;
  public static float Roll=>Twilight.Roll;

  public static IEnumerable<Pawn> Enemies(Pawn p,float r)=>p?.Map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(p.Map,p.Position,r).Where(o=>o!=p && !o.Downed && o.HostileTo(p));
  public static IEnumerable<Pawn> Allies(Pawn p,float r)=>p?.Map==null?Enumerable.Empty<Pawn>():Races124.PawnsNear(p.Map,p.Position,r).Where(o=>o.Faction!=null && o.Faction==p.Faction);
  public static bool Sight(Pawn a,Pawn b)=>a.Map!=null && b.Map==a.Map && GenSight.LineOfSight(a.Position,b.Position,a.Map,true);
  public static void Mote(Pawn p,string text,LP_DeityDef d)=>Items.Mote(p,text,d?.color??Color.white);
  static DamageDef divine,radiant;
  public static DamageDef Divine=>divine??(divine=DefDatabase<DamageDef>.GetNamedSilentFail("LP_DivineSmite")??DamageDefOf.Burn);
  public static DamageDef Radiant=>radiant??(radiant=DefDatabase<DamageDef>.GetNamedSilentFail("LP_Radiant")??Divine);

  // 罪行：击杀人形 + 摘取器官 + 吃过人肉（+3）。巫妖和死灵造物直接算 10。
  public static int Crimes(Pawn p){
   if(p==null)return 0;if(Items.IsLich(p) || Necro.Construct(p)!=null)return 10;
   int n=p.records?.GetAsInt(RecordDefOf.KillsHumanlikes)??0;
   var organs=DefDatabase<RecordDef>.GetNamedSilentFail("OrgansHarvested");if(organs!=null)n+=p.records?.GetAsInt(organs)??0;
   var mem=p.needs?.mood?.thoughts?.memories?.Memories;if(mem!=null && mem.Any(m=>m.def.defName.StartsWith("AteHumanlikeMeat")))n+=3;
   return n;
  }

  // ---------- 灌注：近战命中 ----------
  public static void OnMeleeHit(Pawn caster,Pawn target){
   if(caster==null || target==null || target.Dead)return;
   var d=Twilight.Infused(caster);float D=Twilight.D;
   if(d!=null)switch(d.key){
    case "Bane":if(Roll<BaneDread*D){Items.Give(target,"LP_BaneDread",BaneDreadTicks);Mote(target,"畏惧",d);}break;
    case "Surtr":target.TakeDamage(new DamageInfo(DamageDefOf.Burn,SurtrBurn*D,0f,-1f,caster));if(!target.Dead && Roll<SurtrIgnite && target.Spawned)target.TryAttachFire(.3f,caster);break;
    case "Malar":if(Roll<MalarBleed*D){target.TakeDamage(new DamageInfo(DamageDefOf.Bite,MalarBite,0f,-1f,caster));Mote(target,"撕咬",d);}break;
   }
   var v=Twilight.Vessel(caster);
   if(v?.Key=="Vlaakith" && !target.Dead){
    float blade=VlaakithBlade*(MindFlayer.IsMindFlayer(target)?2f:1f);
    target.TakeDamage(new DamageInfo(Githyanki.Psychic??Divine,blade,0f,-1f,caster));v.sureHit=false;
   }
  }
  // ---------- 灌注 / 附体：伤害倍率 ----------
  public static float DamageFactor(Pawn victim,DamageInfo dinfo){
   float f=1f;var def=dinfo.Def;if(def==null || !def.harmsHealth)return f;
   bool fire=Beasts.IsFire(def);
   var vd=Twilight.Infused(victim);
   if(fire){if(Twilight.VesselOf(victim,"Surtr"))return 0f;if(vd?.key=="Surtr")f*=SurtrFireTaken;}
   var attacker=dinfo.Instigator as Pawn;if(attacker==null || attacker==victim)return f;
   var ad=Twilight.Infused(attacker);
   if(ad?.key=="Tyr" && Crimes(victim)>=CrimeThreshold)f*=1f+TyrBonus*Twilight.D;
   if(ad?.key=="Lathander" && Items.Undead(victim))f*=LathanderUndead;
   var av=Twilight.Vessel(attacker);
   if(av?.Key=="Surtr" && !def.isRanged && !def.isExplosive && !fire)f*=1f+FurnaceStep*Mathf.Min(FurnaceMax,av.furnace);
   if(FactionGroups.IsThralled(attacker) && attacker.Spawned && Allies(attacker,IlsviEchoRadius).Any(o=>Twilight.VesselOf(o,"Ilsvi") && !o.Downed))f*=IlsviEcho;
   return f;
  }

  // ---------- 倒地 ----------
  public static void OnDowned(Pawn p,DamageInfo? dinfo){
   if(p==null || p.Dead)return;
   var d=Twilight.Infused(p);var v=Twilight.Vessel(p);
   // 伊尔斯维“低语者”：被打倒时对攻击者心灵冲击。
   if(d?.key=="Ilsvi" && dinfo?.Instigator is Pawn a && !a.Dead && a!=p){a.stances?.stunner?.StunFor(Mathf.RoundToInt(IlsviStunTicks*Twilight.D),p,false,true,false);Mote(a,"心灵冲击",d);}
   if(v!=null){Fiends.Defer(()=>Depart(p,v));return;}
   // 洛山达“晨光之子”：第一次倒地时 30%（D 45%）立刻爬起、止血。
   if(d?.key=="Lathander"){var dawn=p.health.hediffSet.GetFirstHediffOfDef(Items.Hediff("LP_DawnChild")) as Hediff_DawnChild;if(dawn!=null && !dawn.risen){dawn.risen=true;if(Roll<LathanderRise*Twilight.D)Fiends.Defer(()=>{if(!p.Dead && Rise(p,false))Mote(p,"晨光再起",d);});}}
  }
  // 爬起：止血，治疗伤口直到不再倒地（full=true 时治好全部伤口、长回断肢）。
  public static bool Rise(Pawn p,bool full){
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h.Bleeding).ToList())h.Tended(1f,1f);
   var injuries=p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>!i.IsPermanent()).OrderByDescending(i=>i.Severity).ToList();
   foreach(var i in injuries){if(!full && !p.Downed)break;i.Heal(full?i.Severity:i.Severity*.75f);p.health.CheckForStateChange(null,null);}
   // 满血爬起（洛山达附体的“复苏”）：断掉的部位也长回来。
   if(full)foreach(var part in p.health.hediffSet.GetMissingPartsCommonAncestors().Select(m=>m.Part).ToList())p.health.RestorePart(part);
   var loss=p.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);if(loss!=null)p.health.RemoveHediff(loss);
   p.health.CheckForStateChange(null,null);
   return !p.Downed;
  }
  // 离体（倒地）：按神不同；都掉神迹残片、殖民地 +5 心情。
  public static void Depart(Pawn p,Hediff_DivineVessel v){
   if(p==null || v==null || p.Dead || !p.health.hediffSet.hediffs.Contains(v))return;
   var d=v.Deity;var map=p.MapHeld;var cell=p.PositionHeld;
   // 洛山达“复苏”：第一次被打倒时满血爬起；第二次才离体。
   if(d.key=="Lathander" && !v.revived){v.revived=true;Rise(p,true);Mote(p,"黎明复苏",d);return;}
   p.health.RemoveHediff(v);
   string text=null;
   switch(d.key){
    case "Bane":int fled=0;if(map!=null)foreach(var o in Allies(p,BaneLawRadius).Where(o=>o!=p && !o.Downed && o.RaceProps.Humanlike).ToList())if(o.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.PanicFlee,"神力抽走",true,true)==true)fled++;text="神力被抽走了，"+fled+"名随从四散奔逃。";break;
    case "Surtr":if(map!=null)GenExplosion.DoExplosion(cell,map,SurtrBurstRadius,DamageDefOf.Flame,p,SurtrBurstDamage,ignoredThings:new List<Thing>{p});Wound(p,DamageDefOf.Burn,4,6f);text="苏尔特之焰在体内爆燃，他自己也被严重烧伤。";break;
    case "Ilsvi":if(HediffDefOf.Dementia!=null && !p.health.hediffSet.HasHediff(HediffDefOf.Dementia))p.health.AddHediff(HediffDefOf.Dementia,p.health.hediffSet.GetBrain());BrainScar(p);Former(p,d);text="脑髓被神力烧尽：他的大脑永久受损，可以俘虏。夺心魔吞噬这颗大脑能多得 "+IlsviDevourBonus+" 脑髓。";break;
    case "Tyr":foreach(var h in p.health.hediffSet.hediffs.Where(h=>h.Bleeding).ToList())h.Tended(1f,1f);Former(p,d);Items.Give(p,"LP_Surrendered");text="他服从了审判，放下武器投降：不再流血，可以俘虏，招募难度减半。";break;
    case "Lathander":text="黎明退去，他可以被正常俘虏了。";break;
    case "Vlaakith":text="巫妖女王收回了她的容器。";break;
    case "Talos":if(map!=null){map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningFlash(map));GenExplosion.DoExplosion(cell,map,1.9f,DamageDefOf.Flame,null,12,ignoredThings:new List<Thing>{p});}Wound(p,DamageDefOf.Burn,3,7f);text="一道雷劈在他自己身上：存活，但伤得很重。";break;
    case "Malar":int calmed=0;foreach(var a in v.hunted.Where(a=>a!=null && !a.Dead && a.InMentalState).ToList()){a.MentalState.RecoverFromState();calmed++;}text="野性退散，"+calmed+"只被驱使的野兽安静下来。";break;
   }
   Twilight.Reward(p);
   if(d.key=="Vlaakith" && !p.Dead){TwilightDeath.vlaakithDeparting=p;p.Kill(null);return;}
   if(map!=null)Find.LetterStack.ReceiveLetter("击退了神的使者",p.LabelShort+"体内的"+d.label+"离开了。"+text+"\n\n原地留下了神迹残片；全体殖民者心情 +5（3 天）。",LetterDefOf.PositiveEvent,new LookTargets(cell,map));
  }
  // 死亡：维拉基斯“女王的复仇”（2 名吉斯洋基战士从星界跳出）与“被女王吞噬”（尸体化为灰烬）；其余照常掉残片。
  public static void OnVesselDied(Pawn p,LP_DeityDef d,bool departed){
   var corpse=p?.Corpse;var map=corpse?.MapHeld??p?.MapHeld;var cell=corpse?.PositionHeld??p?.PositionHeld??IntVec3.Invalid;
   if(!departed)Twilight.Reward(p);
   if(d?.key!="Vlaakith" || map==null)return;
   var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("LP_FH_GithMercenary");var f=Factions.Hostile;var list=new List<Pawn>();
   if(kind!=null && f!=null && f.HostileTo(Faction.OfPlayer))for(int i=0;i<VlaakithRevenge;i++){
    var g=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,f,PawnGenerationContext.NonPlayer,map.Tile,forceGenerateNewPawn:true,mustBeCapableOfViolence:true));
    var c=CellFinder.RandomClosewalkCellNear(cell,map,3);GenSpawn.Spawn(g,c,map);FleckMaker.Static(c,map,FleckDefOf.PsycastAreaEffect,1.4f);list.Add(g);
   }
   if(list.Count>0)LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false,false,false),map,list);
   if(corpse!=null && !corpse.Destroyed){corpse.Destroy();if(!FilthMaker.TryMakeFilth(cell,map,ThingDefOf.Filth_Ash,3))foreach(var c in GenRadial.RadialCellsAround(cell,4f,false))if(c.InBounds(map) && FilthMaker.TryMakeFilth(c,map,ThingDefOf.Filth_Ash,3))break;/* 1.29.0：倒在水里时灰落到旁边能放污物的格子；1.30.0：放宽到4格内（倒在河中间时相邻8格也都是水） */for(int i=0;i<4;i++)FleckMaker.ThrowSmoke(cell.ToVector3Shifted(),map,1.6f);}
   Find.LetterStack.ReceiveLetter("被女王吞噬",(p?.LabelShort??"化身容器")+"的身体化为灰烬：巫妖女王维拉基斯收回了她的容器，不留尸体，也没有灵魂可以汲取。"+(list.Count>0?"\n\n女王的复仇："+list.Count+"名吉斯洋基战士从星界跳了出来！":""),list.Count>0?LetterDefOf.ThreatSmall:LetterDefOf.NeutralEvent,list.Count>0?new LookTargets(list.Cast<Thing>()):new LookTargets(cell,map));
  }
  static void Former(Pawn p,LP_DeityDef d){var def=Items.Hediff("LP_FormerVessel");if(def==null)return;var h=(Hediff_FormerVessel)HediffMaker.MakeHediff(def,p);h.deity=d;p.health.AddHediff(h);}
  // 加不致命的伤：每处严重度 sev，最多 n 处，加之前确认不会致死、不会倒在致命伤上。
  public static int Wound(Pawn p,DamageDef def,int n,float sev){
   int added=0;var parts=p.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined,BodyPartDepth.Outside).Where(b=>b.coverageAbs>0 && b.def!=BodyPartDefOf.Head).InRandomOrder().ToList();
   foreach(var part in parts){
    if(added>=n)break;var hd=HealthUtility.GetHediffDefFromDamage(def,p,part);if(hd==null)continue;
    var inj=(Hediff_Injury)HediffMaker.MakeHediff(hd,p,part);inj.Severity=Mathf.Min(sev,p.health.hediffSet.GetPartHealth(part)-1f);if(inj.Severity<=.5f)continue;
    if(p.health.WouldDieAfterAddingHediff(inj))continue;p.health.AddHediff(inj,part);added++;
   }
   return added;
  }
  static void BrainScar(Pawn p){
   var brain=p.health.hediffSet.GetBrain();if(brain==null)return;
   var inj=(Hediff_Injury)HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Burn"),p,brain);inj.Severity=Mathf.Min(3f,p.health.hediffSet.GetPartHealth(brain)-2f);if(inj.Severity<=.5f || p.health.WouldDieAfterAddingHediff(inj))return;
   p.health.AddHediff(inj,brain);var comp=inj.TryGetComp<HediffComp_GetsPermanent>();if(comp!=null)comp.IsPermanent=true;
  }

  // ---------- 附体：计时技能（每 60 刻） ----------
  public static void Tick(Hediff_DivineVessel v,int elapsed){
   var p=v.pawn;var d=v.Deity;
   ReleaseTimed(v,false);
   switch(d.key){
    case "Bane":foreach(var o in Enemies(p,BaneFearRadius).ToList())Items.Give(o,"LP_BaneFear",AuraTicks);if(v.Ready("command",BaneCommandTicks))TyrantsCommand(p,d);break;
    case "Surtr":if(v.lastCell.IsValid && v.lastCell!=p.Position && Roll<SurtrTrail)FireUtility.TryStartFireIn(v.lastCell,p.Map,.3f,p);v.lastCell=p.Position;if(Enemies(p,SurtrSweepRadius).Any() && v.Ready("sweep",SurtrSweepTicks))Sweep(p,d);break;
    case "Ilsvi":if(v.Ready("whisper",IlsviWhisperTicks))Whisper(v);break;
    case "Tyr":if(v.Ready("punish",TyrPunishTicks))Punish(p,d);break;
    case "Lathander":if(v.Ready("aura",GenDate.TicksPerHour))DawnAura(p);if(v.Ready("beam",LathanderBeamTicks))Beam(p,d);break;
    case "Vlaakith":if(v.Ready("raid",VlaakithRaidTicks))AstralRaid(v);break;
    case "Talos":foreach(var o in Enemies(p,TalosGaleRadius).ToList())Items.Give(o,"LP_TalosGale",AuraTicks);if(v.Ready("storm",TalosStormTicks))Storm(p.Map);if(v.Ready("lightning",TalosLightningTicks))Lightning(p,d);break;
    case "Malar":if(!v.calledHunt){v.calledHunt=true;Hunt(v);}if(v.Ready("backlash",MalarBacklashTicks))Backlash(v);break;
   }
  }
  public static void ReleaseTimed(Hediff_DivineVessel v,bool all){
   int now=Now;
   for(int i=v.timedPawns.Count-1;i>=0;i--){
    if(!all && now<v.timedUntil[i])continue;var o=v.timedPawns[i];v.timedPawns.RemoveAt(i);v.timedUntil.RemoveAt(i);
    if(o!=null && !o.Dead && o.InMentalState && (o.MentalStateDef==MentalStateDefOf.Berserk || o.MentalStateDef==MentalStateDefOf.Manhunter))o.MentalState.RecoverFromState();
   }
  }
  // 班恩“暴君之令”：视线内最近 3 名敌人扔下手中武器（心智抗性可抵抗）。
  public static int TyrantsCommand(Pawn p,LP_DeityDef d){
   int n=0;
   foreach(var o in Enemies(p,SkillRange).Where(o=>o.equipment?.Primary!=null && Sight(p,o)).OrderBy(o=>o.Position.DistanceToSquared(p.Position)).Take(BaneCommandCount).ToList()){
    if(DNDMind.TryResist(o)){Mote(o,"抵抗",d);continue;}
    if(o.equipment.TryDropEquipment(o.equipment.Primary,out _,o.Position,false)){Mote(o,"缴械",d);n++;}
   }
   return n;
  }
  public static int Sweep(Pawn p,LP_DeityDef d){
   int n=0;foreach(var o in Enemies(p,SurtrSweepRadius).ToList()){o.TakeDamage(new DamageInfo(DamageDefOf.Flame,SurtrSweepDamage,0f,-1f,p));if(!o.Dead && o.Spawned)o.TryAttachFire(.5f,p);n++;}
   if(n>0){FleckMaker.Static(p.Position,p.Map,FleckDefOf.ExplosionFlash,4f);Mote(p,"焰剑横扫",d);}
   return n;
  }
  // 伊尔斯维“低语光环”：15 格内随机一名敌人狂乱 10 秒（心智抗性可抵抗；不能被蝌蚪寄生的人免疫：巫妖、火巨人、死灵造物等）。
  public static Pawn Whisper(Hediff_DivineVessel v){
   var p=v.pawn;var o=Enemies(p,IlsviRadius).Where(x=>Tadpoles.Immunity(x)==null && !x.InMentalState).RandomElementWithFallback();if(o==null)return null;
   if(DNDMind.TryResist(o)){Mote(o,"抵抗",v.Deity);return null;}
   if(o.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Berserk,"伊尔斯维的低语",true,true)!=true)return null;
   v.timedPawns.Add(o);v.timedUntil.Add(Now+IlsviBerserkTicks);Mote(o,"低语",v.Deity);
   return o;
  }
  public static Pawn Punish(Pawn p,LP_DeityDef d){
   var o=Enemies(p,SkillRange).Where(x=>x.RaceProps.Humanlike && Sight(p,x)).OrderByDescending(Crimes).FirstOrDefault();if(o==null)return null;
   float dmg=Mathf.Min(TyrPunishCap,TyrPunishBase+TyrPunishPer*Crimes(o));
   var map=o.Map;var at=o.Position;
   o.TakeDamage(new DamageInfo(Divine,dmg,1f,-1f,p));FleckMaker.Static(at,map,FleckDefOf.PsycastAreaEffect,1.5f);Mote(o,"罪罚 "+dmg.ToString("0"),d);
   return o;
  }
  public static int DawnAura(Pawn p){
   int n=0;foreach(var o in Allies(p,LathanderAuraRadius).ToList())foreach(var i in o.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(i=>!i.IsPermanent()).ToList()){i.Heal(LathanderAuraHeal);n++;}
   return n;
  }
  public static Pawn Beam(Pawn p,LP_DeityDef d){
   var o=Enemies(p,SkillRange).Where(x=>Sight(p,x)).OrderBy(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault();if(o==null)return null;
   var map=o.Map;var at=o.Position;
   o.TakeDamage(new DamageInfo(Radiant,LathanderBeam*(Items.Undead(o)?2f:1f),.5f,-1f,p));FleckMaker.Static(at,map,FleckDefOf.PsycastAreaEffect,1.8f);Mote(o,"光柱",d);
   return o;
  }
  // 维拉基斯“星界突袭”：跳到视线内最远一名殖民者身后，下一次近战必中。
  public static Pawn AstralRaid(Hediff_DivineVessel v){
   var p=v.pawn;var map=p.Map;
   var o=map.mapPawns.FreeColonistsSpawned.Where(x=>!x.Dead && x.Position.InHorDistOf(p.Position,SkillRange) && Sight(p,x)).OrderByDescending(x=>x.Position.DistanceToSquared(p.Position)).FirstOrDefault();if(o==null)return null;
   var cell=GenAdj.CellsAdjacent8Way(o).Where(c=>c.InBounds(map) && c.Standable(map) && c.GetFirstPawn(map)==null).OrderByDescending(c=>c.DistanceToSquared(p.Position)).FirstOrDefault();
   if(!cell.IsValid || cell==default(IntVec3))return null;
   FleckMaker.Static(p.Position,map,FleckDefOf.PsycastAreaEffect,1.2f);Races124.Blink(p,cell);FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,1.2f);
   v.sureHit=true;Mote(p,"星界突袭",v.Deity);
   if(p.jobs!=null){var job=JobMaker.MakeJob(JobDefOf.AttackMelee,o);job.expiryInterval=600;p.jobs.StartJob(job,JobCondition.InterruptForced);}
   return o;
  }
  public static bool Storm(Map map){
   var storm=DefDatabase<WeatherDef>.GetNamedSilentFail("RainyThunderstorm");if(map==null || storm==null)return false;
   if(map.weatherManager.curWeather!=storm)map.weatherManager.TransitionTo(storm);return true;
  }
  public static IntVec3 Lightning(Pawn p,LP_DeityDef d){
   var map=p.Map;var c=map.mapPawns.FreeColonistsSpawned.Where(x=>!x.Dead).RandomElementWithFallback();if(c==null)return IntVec3.Invalid;
   if(!CellFinder.TryFindRandomCellNear(c.Position,map,3,x=>x.InBounds(map) && !x.Roofed(map),out var cell))return IntVec3.Invalid;
   map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map,cell));
   return cell;
  }
  // 马拉尔“狩猎号令”：地图上 2～4 只野生动物猎杀殖民者；没有野生动物时从地图边缘来 2 只森林狼。
  public static int Hunt(Hediff_DivineVessel v){
   var map=v.pawn.Map;
   var wild=map.mapPawns.AllPawnsSpawned.Where(a=>a.RaceProps.Animal && a.Faction==null && !a.Dead && !a.Downed && !a.InMentalState).InRandomOrder().Take(Rand.RangeInclusive(2,4)).ToList();
   if(wild.Count==0){
    var wolf=DefDatabase<PawnKindDef>.GetNamedSilentFail("Wolf_Timber");
    if(wolf!=null && CellFinder.TryFindRandomEdgeCellWith(c=>c.Standable(map) && !c.Fogged(map),map,0f,out var edge))for(int i=0;i<2;i++){var a=PawnGenerator.GeneratePawn(wolf);GenSpawn.Spawn(a,CellFinder.RandomClosewalkCellNear(edge,map,3),map);wild.Add(a);}
   }
   int n=0;foreach(var a in wild)if(a.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.ManhunterPermanent,null,true,false,false,null,true)==true){v.hunted.Add(a);n++;}
   if(n>0)Messages.Message("马拉尔的狩猎号令：地图上"+n+"只野兽开始猎杀你的殖民者！",new LookTargets(wild.Cast<Thing>()),MessageTypeDefOf.ThreatBig,false);
   return n;
  }
  // 马拉尔“驯兽反噬”：15 格内一只殖民地动物失控 30 秒（驯兽 ≥10 的人就在旁边时不会）。
  public static Pawn Backlash(Hediff_DivineVessel v){
   var p=v.pawn;var map=p.Map;
   var a=map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).Where(x=>x.RaceProps.Animal && !x.Dead && !x.Downed && !x.InMentalState && x.Position.InHorDistOf(p.Position,MalarBacklashRadius) && !Handled(x)).RandomElementWithFallback();
   if(a==null)return null;
   if(a.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Manhunter,"马拉尔的驯兽反噬",true,false,false,null,true)!=true)return null;
   v.timedPawns.Add(a);v.timedUntil.Add(Now+MalarBacklashLength);Mote(a,"失控",v.Deity);
   return a;
  }
  public static bool Handled(Pawn animal)=>animal.Map.mapPawns.FreeColonistsSpawned.Any(c=>!c.Downed && c.Position.InHorDistOf(animal.Position,1.5f) && (c.skills?.GetSkill(SkillDefOf.Animals)?.Level??0)>=MalarHandlerSkill);

  // 提尔“审判之庭”：10 格内敌人不能用任何能力。
  public static bool Silenced(Pawn p){
   if(p?.Map==null || !p.Spawned || !Hediff_DivineVessel.AnySpawned())return false; // 1.58.6：没有已生成的化身容器就不可能有提尔附体者
   var all=p.Map.mapPawns.AllPawnsSpawned;
   for(int i=0;i<all.Count;i++){var o=all[i];if(!o.Dead && o.Position.InHorDistOf(p.Position,TyrCourtRadius) && o!=p && !o.Downed && Twilight.VesselOf(o,"Tyr") && o.HostileTo(p))return true;}
   return false;
  }
  // 班恩：8 格内有站着的班恩灌注者、或 20 格内有班恩附体者时，同派系的人不会逃跑；符印巨魔在附体者附近不会狂暴。
  public static bool BaneHolds(Pawn p,bool berserk){
   if(p?.Map==null || p.Faction==null || p.Faction==Faction.OfPlayer)return false;
   foreach(var o in Races124.PawnsNear(p.Map,p.Position,BaneLawRadius)){
    if(o==p || o.Downed || o.Faction!=p.Faction)continue;
    if(Twilight.VesselOf(o,"Bane"))return true;
    if(!berserk && Twilight.InfusedBy(o,"Bane") && o.Position.InHorDistOf(p.Position,BaneInfusedRadius))return true;
   }
   return false;
  }
 }

 // 洛山达“晨光之子”：每小时一处伤口 -3；记录第一次倒地是否用过。
 public class Hediff_DawnChild : HediffWithComps {
  public bool risen;int acc;
  public override bool Visible=>false;
  public override bool ShouldRemove=>base.ShouldRemove || !Twilight.InfusedBy(pawn,"Lathander");
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((acc+=delta)<GenDate.TicksPerHour)return;acc=0;
   var i=pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(x=>!x.IsPermanent()).OrderByDescending(x=>x.Severity).FirstOrDefault();
   i?.Heal(TwilightSkills.LathanderHeal*Twilight.D);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref risen,"lpRisen");Scribe_Values.Look(ref acc,"lpAcc");}
 }


 // ---------- Harmony 挂钩 ----------
 [HarmonyPatch(typeof(Verb_MeleeAttackDamage),"ApplyMeleeDamageToTarget")]
 public static class TwilightMeleeHit {static void Postfix(Verb_MeleeAttackDamage __instance,LocalTargetInfo target){var c=__instance.CasterPawn;if(c!=null && (c.story?.traits!=null))TwilightSkills.OnMeleeHit(c,target.Pawn);}}
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class TwilightDamage {static void Prefix(Pawn __instance,ref DamageInfo __0){float f=TwilightSkills.DamageFactor(__instance,__0);if(f!=1f)__0.SetAmount(__0.Amount*f);}}
 // 苏尔特“炉心”：附体者每受一次伤害，近战伤害 +5%（最多 +50%）。
 [HarmonyPatch(typeof(Pawn_HealthTracker),nameof(Pawn_HealthTracker.PostApplyDamage))]
 public static class TwilightFurnace {static void Postfix(Pawn ___pawn,float totalDamageDealt){if(totalDamageDealt<=0)return;var v=Twilight.Vessel(___pawn);if(v?.Key=="Surtr" && v.furnace<TwilightSkills.FurnaceMax)v.furnace++;}}
 [HarmonyPatch(typeof(Pawn_HealthTracker),"MakeDowned")]
 public static class TwilightDowned {static void Postfix(Pawn ___pawn,DamageInfo? dinfo){TwilightSkills.OnDowned(___pawn,dinfo);}}
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class TwilightDeath {
  static void Prefix(Pawn __instance,out LP_DeityDef __state){__state=__instance.Dead?null:Twilight.VesselDeity(__instance);}
  static void Postfix(Pawn __instance,LP_DeityDef __state){
   if(!__instance.Dead)return;
   if(__state!=null){var p=__instance;Fiends.Defer(()=>TwilightSkills.OnVesselDied(p,__state,false));return;}
   // 维拉基斯离体后自己杀死的那一下：已经掉过残片，只做化灰与复仇。
   if(TwilightDeath.vlaakithDeparting==__instance){vlaakithDeparting=null;var p=__instance;var d=Twilight.ByKey("Vlaakith");Fiends.Defer(()=>TwilightSkills.OnVesselDied(p,d,true));}
  }
  public static Pawn vlaakithDeparting;
 }

 // 维拉基斯“女王的宠儿”：受致命伤时 20%（D 30%）星界跳跃到 8 格外，躲开这一下。
 [HarmonyPatch(typeof(DamageWorker_AddInjury),"FinalizeAndAddInjury",new Type[]{typeof(Pawn),typeof(Hediff_Injury),typeof(DamageInfo),typeof(DamageWorker.DamageResult)})]
 public static class TwilightLethalDodge {
  static void Prefix(Pawn __0,Hediff_Injury __1){
   var p=__0;if(p==null || p.Dead || !p.Spawned || !Twilight.InfusedBy(p,"Vlaakith") || __1==null)return;
   if(!p.health.WouldDieAfterAddingHediff(__1) || TwilightSkills.Roll>=TwilightSkills.VlaakithDodge*Twilight.D)return;
   var map=p.Map;
   if(!CellFinder.TryFindRandomCellNear(p.Position,map,TwilightSkills.DodgeRange,c=>c.Standable(map) && c.GetFirstPawn(map)==null && c.DistanceTo(p.Position)>=TwilightSkills.DodgeRange-2,out var cell))return;
   __1.Severity=.01f;
   Fiends.Defer(()=>{if(!p.Dead && p.Spawned){FleckMaker.Static(p.Position,map,FleckDefOf.PsycastAreaEffect,1.2f);Races124.Blink(p,cell);TwilightSkills.Mote(p,"星界闪避",Twilight.Infused(p));}});
  }
 }
 // 神明附体：免疫眩晕。
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.StunFor))]
 public static class TwilightStunImmunity {static bool Prefix(StunHandler __instance)=>!(__instance.parent is Pawn p) || Twilight.Vessel(p)==null;}
 // 塔洛斯“风暴之怒”：EMP 不会让自己眩晕。
 [HarmonyPatch(typeof(StunHandler),nameof(StunHandler.Notify_DamageApplied))]
 public static class TwilightTalosEmp {static bool Prefix(StunHandler __instance,DamageInfo __0)=>__0.Def!=DamageDefOf.EMP || !(__instance.parent is Pawn p) || !Twilight.InfusedBy(p,"Talos");}
 // 塔洛斯：远程命中 8%（D 12%）附带一次小 EMP。
 [HarmonyPatch(typeof(Bullet),"Impact")]
 public static class TwilightTalosShot {
  static void Prefix(Bullet __instance,Thing hitThing,out (Pawn,Map,IntVec3) __state){__state=(__instance.Launcher as Pawn,__instance.Map,__instance.Position);}
  static void Postfix(Thing hitThing,(Pawn,Map,IntVec3) __state){
   var (shooter,map,cell)=__state;if(!(hitThing is Pawn) || shooter==null || map==null || !Twilight.InfusedBy(shooter,"Talos"))return;
   if(TwilightSkills.Roll<TwilightSkills.TalosEmp*Twilight.D)GenExplosion.DoExplosion(hitThing.PositionHeld,map,1.5f,DamageDefOf.EMP,shooter,8,ignoredThings:new List<Thing>{shooter});
  }
 }
 // 神明附体免疫精神崩溃和一切精神状态；班恩让附近的人不逃跑、符印巨魔不狂暴。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class TwilightMentalState {
  static bool Prefix(MentalStateDef stateDef,Pawn ___pawn,ref bool __result){
   if(stateDef==null || ___pawn==null)return true;
   if(Twilight.Vessel(___pawn)!=null){__result=false;return false;}
   if((stateDef==MentalStateDefOf.PanicFlee && TwilightSkills.BaneHolds(___pawn,false)) || (stateDef==MentalStateDefOf.Berserk && Troll.Is(___pawn) && TwilightSkills.BaneHolds(___pawn,true))){TwilightSkills.Mote(___pawn,"铁律",Twilight.ByKey("Bane"));__result=false;return false;}
   return true;
  }
 }
 // 原版袭击“折损过半就撤退”：有站着的班恩灌注者时不撤。
 [HarmonyPatch(typeof(Trigger_FractionPawnsLost),nameof(Trigger_FractionPawnsLost.ActivateOn))]
 public static class TwilightBaneNoRetreat {
  static void Postfix(Lord lord,ref bool __result){if(__result && lord?.ownedPawns!=null && lord.ownedPawns.Any(p=>p!=null && !p.Dead && !p.Downed && (Twilight.InfusedBy(p,"Bane") || Twilight.VesselOf(p,"Bane"))))__result=false;}
 }
 // 班恩“畏惧”：不能开枪。
 [HarmonyPatch(typeof(Verb),nameof(Verb.TryStartCastOn),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo),typeof(bool),typeof(bool),typeof(bool),typeof(bool)})]
 public static class TwilightDreadNoShoot {
  static bool Prefix(Verb __instance,ref bool __result){
   var p=__instance.CasterPawn;if(p==null || __instance.verbProps==null || __instance.verbProps.IsMeleeAttack || __instance is Verb_CastAbility || !Items.Has(p,"LP_BaneDread"))return true;
   __result=false;return false;
  }
 }
 // 提尔“审判之庭”：原版能力（巫妖法术、夺心魔灵能、本模组种族能力）都走 RimWorld.Ability；VPE 的能力在 VPE 桥接里另挂。
 [HarmonyPatch(typeof(RimWorld.Ability),nameof(RimWorld.Ability.GizmoDisabled))]
 public static class TwilightCourtGizmo {static void Postfix(RimWorld.Ability __instance,ref bool __result,ref string reason){if(!__result && TwilightSkills.Silenced(__instance.pawn)){__result=true;reason="提尔的审判之庭：10格内不能使用任何能力。";}}}
 [HarmonyPatch(typeof(RimWorld.Ability),"get_CanCast")]
 public static class TwilightCourtCast {static void Postfix(RimWorld.Ability __instance,ref AcceptanceReport __result){if(__result.Accepted && TwilightSkills.Silenced(__instance.pawn))__result="提尔的审判之庭";}}
 // 维拉基斯“星界突袭”之后下一次近战必中。
 [HarmonyPatch(typeof(Verb_MeleeAttack),"GetNonMissChance")]
 public static class TwilightSureHit {static void Postfix(Verb_MeleeAttack __instance,ref float __result){if(Twilight.Vessel(__instance.CasterPawn)?.sureHit==true)__result=1f;}}
 // 马拉尔“兽血”：夜间不受黑暗影响。
 [HarmonyPatch(typeof(StatPart_Glow),"ActiveFor")]
 public static class TwilightMalarNight {static void Postfix(Thing t,ref bool __result){if(__result && t is Pawn p && Twilight.InfusedBy(p,"Malar"))__result=false;}}
 // 提尔的投降者：招募难度 ×0.5（每次劝说降低的抵抗 ×2，照吉斯洋基“巫妖女王的信徒”的写法）。
 [HarmonyPatch(typeof(InteractionWorker_RecruitAttempt),nameof(InteractionWorker_RecruitAttempt.Interacted))]
 public static class TwilightSurrenderRecruit {
  static void Prefix(Pawn __1,out float __state){__state=__1?.guest?.resistance??-1f;}
  static void Postfix(Pawn __1,float __state){
   if(__state<0 || __1?.guest==null || !__1.health.hediffSet.hediffs.OfType<Hediff_FormerVessel>().Any(h=>h.deity?.key=="Tyr"))return;
   float drop=__state-__1.guest.resistance;if(drop>0)__1.guest.resistance=Mathf.Max(0f,__1.guest.resistance-drop);
  }
 }
}
