using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.52.1 传奇怪（见 ArtSource/ChromaticDragons-1.52.0-spec.md 第四节）：贝希尔、眼魔、半巫妖是 HAR 种族（Bridges/HAR，只在启用 HAR 时有），
 // 死亡骑士是主模组里的人类兵种（不需要 HAR）。全部复用 Source/Legendary.cs 的传奇层（LP_LegendaryExtension、传奇抗性、传奇动作、豁免）。
 // 招牌攻击都写在各自的“XX之躯”hediff 里（同 1.52.0 的龙吐息）：每 60 刻判定一次，先看有没有出生、附近有没有敌人。
 // 反魔法锥、亡灵统领只读 MapComponent_Legendary 的缓存（每 60 刻刷新一次），地图上没有眼魔就直接返回。
 public class LP_MonsterExtension : DefModExtension {public string kind;public float healthFactor=1f,renderScale=1f;}

 public static class Monsters {
  public const string Behir="behir",Beholder="beholder",DeathKnight="deathknight",Demilich="demilich";
  sealed class RefEq : IEqualityComparer<Def> {public bool Equals(Def a,Def b)=>ReferenceEquals(a,b);public int GetHashCode(Def d)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(d);}
  // 1.58.6：Ext 也从渲染缩放后缀里调用（可能不在主线程），改成并发字典。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<Def,LP_MonsterExtension> cache=new System.Collections.Concurrent.ConcurrentDictionary<Def,LP_MonsterExtension>(new RefEq());
  static LP_MonsterExtension Of(Def d){if(d==null)return null;return cache.GetOrAdd(d,x=>x.GetModExtension<LP_MonsterExtension>());}
  public static LP_MonsterExtension Ext(Pawn p)=>p==null?null:Of(p.kindDef)??Of(p.def);
  public static string KindOf(Pawn p)=>Ext(p)?.kind;
  public static bool Is(Pawn p,string k)=>p!=null && KindOf(p)==k;
  public static bool IsMonster(Pawn p)=>KindOf(p)!=null;
  public static string Label(string k)=>k==Behir?"贝希尔":k==Beholder?"眼魔":k==DeathKnight?"死亡骑士":k==Demilich?"半巫妖":k;
  public static PawnKindDef Kind(string k)=>DefDatabase<PawnKindDef>.GetNamedSilentFail(k==Behir?"LP_LM_Behir":k==Beholder?"LP_LM_Beholder":k==Demilich?"LP_LM_Demilich":"LP_LM_DeathKnight");
  public static bool HarAvailable=>DefDatabase<ThingDef>.GetNamedSilentFail("LP_Behir")!=null;
  public static float HealthMult(Pawn p){var e=Ext(p);return e==null?1f:e.healthFactor*Dragons.HealthMult;} // 传奇怪共用“真龙强度”
  public static float RenderScale(Pawn p)=>Ext(p)?.renderScale??1f;
  public static bool NoButcher(Pawn p)=>p!=null && (Is(p,Behir) || Is(p,Beholder) || Is(p,Demilich));
  public static float? TestRoll;public static float Roll=>TestRoll??Rand.Value;

  // ---------- 开关 ----------
  static Settings S=>LichMod.Settings;
  public static bool On=>S?.legendaryMonsters??true;
  public static bool BehirOn=>On && HarAvailable && (S?.behirHunt??true);
  public static bool BeholderOn=>On && HarAvailable && (S?.beholderLair??true);
  public static bool DemilichOn=>On && HarAvailable && (S?.demilichTomb??true);
  public static bool DeathKnightOn=>On && (S?.deathKnightThay??true);

  // ---------- 派系 ----------
  static FactionDef beastsDef;public static FactionDef BeastsDef=>beastsDef??(beastsDef=DefDatabase<FactionDef>.GetNamedSilentFail("LP_LegendaryBeasts"));
  // 隐藏的「传奇怪物」：对玩家、对五色龙群都敌对（贝希尔是龙的死敌）。
  public static Faction Beasts(){
   var f=Factions.EnsureHidden(BeastsDef);if(f==null)return null;
   if(!f.HostileTo(Faction.OfPlayer))Factions.SetRelation(f,Faction.OfPlayer,-100);
   var d=Factions.Of(Dragons.FactionDef);if(d!=null && !f.HostileTo(d))Factions.SetRelation(f,d,-100);
   return f;
  }

  // ---------- 生成 ----------
  static readonly Dictionary<string,string[]> Names=new Dictionary<string,string[]>{
   {Behir,new[]{"雷鸣之喉","卡兹拉克","缠骨者","苍蛇","沙行者"}},{Beholder,new[]{"克萨纳斯","泽塔拉克斯","千目","诺克萨尔","瓦洛斯"}},{Demilich,new[]{"阿瑟拉克","无名之颅","索恩","灰烬王冠","维克托"}}};
  public static Pawn Make(string k,Faction f=null,int tile=-1){
   var kind=Kind(k);if(kind==null)return null;
   if(f==null)f=k==DeathKnight?(Factions.Hostile??Beasts()):Beasts();
   var p=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,f,PawnGenerationContext.NonPlayer,tile,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,fixedBiologicalAge:k==DeathKnight?40f:300f,fixedChronologicalAge:k==DeathKnight?400f:1200f,allowAddictions:false));
   Normalize(p);return p;
  }
  public static void Normalize(Pawn p){
   var k=KindOf(p);if(k==null || p.health==null)return;
   foreach(var h in p.health.hediffSet.hediffs.Where(h=>h is Hediff_Injury || h is Hediff_AddedPart || h.def.isBad && !(h is Hediff_MissingPart)).ToList())p.health.RemoveHediff(h);
   foreach(var m in p.health.hediffSet.GetMissingPartsCommonAncestors().ToList())p.health.RestorePart(m.Part);
   if(p.story?.traits!=null)foreach(var t in p.story.traits.allTraits.ToList())p.story.traits.RemoveTrait(t);
   if(k!=DeathKnight){
    var head=DefDatabase<HeadTypeDef>.GetNamedSilentFail("LP_MonsterHead_"+p.def.defName);if(head!=null && p.story!=null)p.story.headType=head;
    if(p.story!=null){p.story.bodyType=BodyTypeDefOf.Hulk;p.story.hairDef=HairDefOf.Bald;p.story.skinColorOverride=Color.white;}
    if(p.style!=null){p.style.beardDef=BeardDefOf.NoBeard;p.style.FaceTattoo=TattooDefOf.NoTattoo_Face;p.style.BodyTattoo=TattooDefOf.NoTattoo_Body;}
    if(Names.TryGetValue(k,out var n) && !(p.Name is NameSingle))p.Name=new NameSingle(n.RandomElement());
    p.apparel?.DestroyAll();p.equipment?.DestroyAllEquipment();
   }else EquipKnight(p);
   p.inventory?.DestroyAll();
   string body=k==Behir?"LP_BehirBody":k==Beholder?"LP_BeholderBody":k==Demilich?"LP_DemilichBody":"LP_DeathKnightBody";
   if(!Items.Has(p,body)){Legendary.bypass=true;try{Items.Give(p,body);}finally{Legendary.bypass=false;}}
   p.Drawer?.renderer?.SetAllGraphicsDirty();
  }
  // 死亡骑士：死亡骑士铠甲 + 亡魂巨盔（LP_HelmRestlessDead，1.45.1 的套装，优秀品质）、长剑。
  public static void EquipKnight(Pawn p){
   if(p?.apparel==null)return;
   foreach(var n in new[]{"LP_DeathknightPlate","LP_HelmRestlessDead"}){
    var d=DefDatabase<ThingDef>.GetNamedSilentFail(n);if(d==null || p.apparel.WornApparel.Any(a=>a.def==d))continue;
    var a=(Apparel)ThingMaker.MakeThing(d,d.MadeFromStuff?ThingDefOf.Steel:null);a.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Outsider);
    foreach(var w in p.apparel.WornApparel.Where(x=>!ApparelUtility.CanWearTogether(x.def,d,p.RaceProps.body)).ToList())p.apparel.Remove(w);
    if(ApparelUtility.HasPartsToWear(p,d))p.apparel.Wear(a,false);else a.Destroy();
   }
   var sword=DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_LongSword");
   if(sword!=null && p.equipment!=null && p.equipment.Primary?.def!=sword){p.equipment.DestroyAllEquipment();var s=(ThingWithComps)ThingMaker.MakeThing(sword,ThingDefOf.Steel);s.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Outsider);p.equipment.AddEquipment(s);}
  }
  public static IEnumerable<Pawn> Foes(Pawn p,float r)=>Legendary.EnemiesNear(p,r).Where(x=>!x.Downed);
  public static bool AnyHostile(Pawn p)=>p?.Map!=null && p.Faction!=null && p.Map.attackTargetsCache.TargetsHostileToFaction(p.Faction).Count>0;

  // ---------- 贝希尔：闪电吐息、缠绕吞噬、龙之死敌 ----------
  public const float BehirBreathRange=16f,BehirBreathDamage=12f;public const int BehirBreathHits=4,SwallowCooldown=900,AcidPerSecond=4;
  public const float SwallowOuter=60f,SwallowInner=30f,DragonFoe=1.5f;
  public static Dragons.BreathResult BehirBreath(Pawn b,IntVec3 target){
   var cells=Dragons.ShapeCells(b.Map,b.Position,target,BehirBreathRange,true,Dragons.ConeHalfAngle,b.Rotation);
   foreach(var c in cells){FleckMaker.ThrowMicroSparks(c.ToVector3Shifted(),b.Map);FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),b.Map,.8f);}
   var def=DefDatabase<DamageDef>.GetNamedSilentFail("LP_Shock")??DamageDefOf.Burn;
   var r=Dragons.HitArea(b,cells,def,BehirBreathHits,BehirBreathDamage*Dragons.DamageMult,Adventurer.Dex,Legendary.DC(b,Adventurer.Con),p=>p.RaceProps.IsMechanoid?2f:1f,(p,s)=>Dragons.BreakShields(p));
   Items.Mote(b,"闪电吐息！",new Color(.6f,.8f,1f));return r;
  }
  public static Pawn BreathTarget(Pawn b,float range)=>Foes(b,range).Where(x=>GenSight.LineOfSight(b.Position,x.Position,b.Map,true)).OrderByDescending(x=>Dragons.Is(x)?1:0).ThenBy(x=>x.Position.DistanceTo(b.Position)).FirstOrDefault();
  // 龙之死敌：贝希尔打真龙和力能龙裔 ×1.5；半巫妖只有头骨，圣水、银器、圣银手枪 ×2。
  public static float DamageFactor(Pawn victim,DamageInfo d){
   var a=d.Instigator as Pawn;float f=1f;
   if(a!=null && Is(a,Behir) && Dragons.Is(victim))f*=DragonFoe;
   if(Is(victim,Demilich) && Holy(d))f*=2f;
   return f;
  }
  public static bool Holy(DamageInfo d){
   var n=d.Def?.defName??"";if(n.Contains("Holy") || n=="LP_Radiant" || n=="LP_DivineSmite" || n=="LP_SilverPsychic")return true;
   var w=d.Weapon;if(w!=null && (w.defName.IndexOf("Silver",StringComparison.OrdinalIgnoreCase)>=0))return true;
   if(d.Instigator is Pawn p && p.equipment?.Primary?.Stuff==ThingDefOf.Silver && d.Weapon==p.equipment.Primary.def)return true;
   return false;
  }

  // ---------- 眼魔：眼射线、反魔法主眼 ----------
  public static readonly string[] Rays={"魅惑","麻痹","恐惧","减速","衰弱","石化","解离","死亡","睡眠","念动"};
  public const float RayRange=20f,AntiMagicRange=20f,AntiMagicHalf=30f,AmuletRadius=3f;public const int RayTicks=180;
  public static List<BodyPartRecord> Stalks(Pawn b)=>b.RaceProps.body.AllParts.Where(x=>x.def.defName=="LP_BeholderStalk").ToList();
  public static List<int> LiveRays(Pawn b){var all=Stalks(b);var live=b.health.hediffSet.GetNotMissingParts().Where(x=>x.def.defName=="LP_BeholderStalk").ToList();var r=new List<int>();for(int i=0;i<all.Count && i<Rays.Length;i++)if(live.Contains(all[i]))r.Add(i);return r;}
  public static int StalkDrop(Pawn b)=>Mathf.Clamp(LiveRays(b).Count,2,4);
  public static bool MainEye(Pawn b)=>b.health.hediffSet.GetNotMissingParts().Any(x=>x.def.defName=="LP_BeholderMainEye");
  public static int RaysFired;
  public static string Ray(Pawn b,int i,Pawn t){
   if(b?.Map==null || t==null || t.Dead)return null;RaysFired++;
   int dc=Legendary.DC(b,Adventurer.Int);string name=Rays[Mathf.Clamp(i,0,Rays.Length-1)];
   FleckMaker.ConnectingLine(b.DrawPos,t.DrawPos,FleckDefOf.PsycastAreaEffect,b.Map,.4f);Items.Mote(t,name+"射线",new Color(.9f,.6f,1f));
   switch(i){
    case 0:Dragons.Charm(b,t,MapComp<MapComponent_Legendary>.Get(b.Map));break;
    case 1:if(!Legendary.Save(t,Adventurer.Con,dc,b))t.stances?.stunner?.StunFor(300,b,false,true,false);break;
    case 2:if(!Legendary.FearImmune(t) && !Legendary.Save(t,Adventurer.Wis,dc,b))Items.Give(t,"LP_Frightened",Legendary.FearTicks);break;
    case 3:if(!Legendary.Save(t,Adventurer.Dex,dc,b))Items.Give(t,"LP_FrostSlow",1200);break;
    case 4:{bool s=Legendary.Save(t,Adventurer.Con,dc,b);t.TakeDamage(new DamageInfo(Necrotic,(s?12f:24f)*Dragons.DamageMult,.3f,-1f,b));break;}
    case 5:if(!Legendary.Save(t,Adventurer.Con,dc,b))Items.Give(t,"LP_BeholderPetrified",GenDate.TicksPerDay);break;
    case 6:{bool s=Legendary.Save(t,Adventurer.Dex,dc,b);t.TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamedSilentFail("LP_Force")??DamageDefOf.Blunt,(s?20f:45f)*Dragons.DamageMult,.5f,-1f,b));break;}
    case 7:if(t.health.summaryHealth.SummaryHealthPercent<.25f && !Legendary.Save(t,Adventurer.Con,dc,b))t.Kill(new DamageInfo(Necrotic,999f,0f,-1f,b));break;
    case 8:if(!Legendary.Save(t,Adventurer.Wis,dc,b)){if(Items.Hediff("LP_MagicSleep")!=null)Items.Give(t,"LP_MagicSleep",720);else t.health.AddHediff(HediffDefOf.Anesthetic);}break; // 睡眠术的 hediff 在 VPE 桥里；没装 VPE 时用原版麻醉
    case 9:if(!Legendary.Save(t,Adventurer.Str,dc,b)){var dest=DNDPush.Destination(t.Map,t.Position,(t.Position-b.Position).ToVector3(),3);if(dest!=t.Position)DNDPush.Move(t,dest);}break;
   }
   return name;
  }
  // 解离：看不到人时，烧穿挡在最近的敌人前面的墙（300 点）。
  public static Thing Disintegrate(Pawn b){
   var foe=Foes(b,RayRange).OrderBy(x=>x.Position.DistanceTo(b.Position)).FirstOrDefault();if(foe==null)return null;
   foreach(var c in GenSight.BresenhamCellsBetween(b.Position,foe.Position)){var e=c.GetEdifice(b.Map);if(e!=null && e.def.useHitPoints){e.TakeDamage(new DamageInfo(DamageDefOf.Blunt,300f,1f,-1f,b));RaysFired++;return e;}}
   return null;
  }
  public static int FireRays(Pawn b,int count){
   var live=LiveRays(b);if(live.Count==0)return 0;int n=0;
   var targets=Foes(b,RayRange).Where(x=>GenSight.LineOfSight(b.Position,x.Position,b.Map,true)).ToList();
   if(targets.Count==0){if(live.Contains(6) && Disintegrate(b)!=null)n++;return n;}
   // 世仇：先打夺心魔。
   foreach(var i in live.InRandomOrder().Take(count)){var t=targets.Where(x=>MindFlayer.IsMindFlayer(x)).RandomElementWithFallback()??targets.RandomElement();if(Ray(b,i,t)!=null)n++;}
   if(targets.Count>0)b.Rotation=Rot4.FromAngleFlat((targets[0].Position-b.Position).AngleFlat);
   return n;
  }
  static DamageDef necrotic;public static DamageDef Necrotic=>necrotic??(necrotic=DefDatabase<DamageDef>.GetNamedSilentFail("LP_Necrotic")??DamageDefOf.Burn);
  // 反魔法：眼魔主眼正面 60° 锥形 20 格内、或反魔法护符佩戴者 3 格内的敌人（护符佩戴者自己也算），不能施法，护盾腰带失效。
  public static bool InAntiMagic(Pawn p){
   if(p?.Map==null)return false;var mc=MapComp<MapComponent_Legendary>.Get(p.Map);if(mc==null || mc.beholders.Count==0 && mc.amulets.Count==0)return false;
   foreach(var b in mc.beholders){
    if(b==p || b.Dead || b.Downed || !b.Spawned || !p.HostileTo(b) || !MainEye(b))continue;
    var v=(p.Position-b.Position).ToVector3();if(v.magnitude>AntiMagicRange || v.sqrMagnitude<.01f)continue;
    if(Vector3.Angle(v,b.Rotation.FacingCell.ToVector3())<=AntiMagicHalf)return true;
   }
   foreach(var a in mc.amulets){if(a==p)return true;if(a.Spawned && !a.Dead && p.HostileTo(a) && p.Position.DistanceTo(a.Position)<=AmuletRadius)return true;}
   return false;
  }
  // 1.58.6：每 60 刻对地图上每个小人都会问一次：用静态 Def 和普通循环（原来是 LINQ 加 defName 字符串比较）。
  static readonly DefRef<ThingDef> amuletDef=new DefRef<ThingDef>("LP_AntiMagicAmulet");
  public static bool WearsAmulet(Pawn p)=>p.apparel!=null && p.apparel.WornApparelCount>0 && Items.Worn(p,amuletDef.Def)!=null;
  public static int Blocked;
  public static bool BlockCast(Pawn p){if(!InAntiMagic(p))return false;Blocked++;Items.Mote(p,"反魔法：施法失败",new Color(.7f,.7f,.9f));return true;}

  // ---------- 死亡骑士：地狱火球、亡灵统领、化灰 ----------
  public const float HellfireRadius=4f,HellfireDamage=18f,CommandRadius=15f;public const int HellfireTicks=1200,CommandTicks=240,AshTicks=GenDate.TicksPerHour;
  public static Dragons.BreathResult Hellfire(Map map,IntVec3 at,Pawn source,float dmg=HellfireDamage){
   var r=new Dragons.BreathResult();if(map==null)return r;
   var cells=GenRadial.RadialCellsAround(at,HellfireRadius,true).Where(c=>c.InBounds(map)).ToList();
   foreach(var c in cells){FleckMaker.ThrowFireGlow(c.ToVector3Shifted(),map,1.2f);if(Rand.Chance(.3f))FireUtility.TryStartFireIn(c,map,.4f,source);}
   FleckMaker.Static(at,map,FleckDefOf.PsycastAreaEffect,8f);
   int dc=source!=null?Legendary.DC(source,Adventurer.Cha):15;
   if(source!=null && source.Spawned && source.Map==map){
    // 一半火焰、一半死灵：死灵那一半无视火焰免疫（火巨人也会受伤）。
    r=Dragons.HitArea(source,cells,DamageDefOf.Flame,1,dmg*Dragons.DamageMult,Adventurer.Dex,dc,null,(p,s)=>p.TakeDamage(new DamageInfo(Necrotic,(s?dmg*.5f:dmg)*Dragons.DamageMult,.3f,-1f,source)));
   }else{
    foreach(var p in cells.SelectMany(c=>c.GetThingList(map)).OfType<Pawn>().Distinct().ToList()){
     bool s=Legendary.Save(p,Adventurer.Dex,dc,source);float a=s?dmg*.5f:dmg;
     p.TakeDamage(new DamageInfo(DamageDefOf.Flame,a,.3f,-1f,source));if(!p.Dead)p.TakeDamage(new DamageInfo(Necrotic,a,.3f,-1f,source));r.pawns++;r.dealt[p]=a;
    }
   }
   return r;
  }
  public static IntVec3 ClusterTarget(Pawn k,float range){
   var foes=Foes(k,range).Where(x=>GenSight.LineOfSight(k.Position,x.Position,k.Map,true)).ToList();if(foes.Count==0)return IntVec3.Invalid;
   return foes.OrderByDescending(x=>foes.Count(y=>y.Position.DistanceTo(x.Position)<=HellfireRadius)).First().Position;
  }
  public static bool UndeadAlly(Pawn k,Pawn p)=>p!=k && !p.Dead && p.Faction==k.Faction && (Necro.Construct(p)!=null || Items.Undead(p) || Is(p,DeathKnight) || Is(p,Demilich));
  public static int Command(Pawn k){
   int n=0;if(k?.Map==null)return 0;
   foreach(var p in Races124.PawnsNear(k.Map,k.Position,CommandRadius).ToList())if(UndeadAlly(k,p)){Items.Give(p,"LP_UndeadCommanded",CommandTicks+120);n++;}
   return n;
  }
  public static int Ashed;
  public static void Ash(Pawn k){
   if(k==null || k.Destroyed)return;var map=k.MapHeld;var at=k.PositionHeld;
   var drop=new List<Thing>();
   if(k.apparel!=null)foreach(var a in k.apparel.WornApparel.Where(a=>a.def.defName=="LP_DeathknightPlate" || a.def.defName=="LP_HelmRestlessDead").ToList()){k.apparel.Remove(a);a.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent,ArtGenerationContext.Outsider);drop.Add(a);}
   var orb=DefDatabase<ThingDef>.GetNamedSilentFail("LP_HellfireOrb");if(orb!=null)drop.Add(ThingMaker.MakeThing(orb));
   if(!k.Dead)k.Kill(null);
   var corpse=k.Corpse;if(corpse!=null && !corpse.Destroyed)corpse.Destroy();
   if(map!=null){foreach(var t in drop)GenPlace.TryPlaceThing(t,at,map,ThingPlaceMode.Near);FilthMaker.TryMakeFilth(at,map,ThingDefOf.Filth_Ash,4);FleckMaker.ThrowSmoke(at.ToVector3Shifted(),map,2f);}
   Ashed++;
  }

  // ---------- 半巫妖：吞魂之嚎 ----------
  public const float HowlRadius=20f;public const int HowlDC=21,HowlTicks=3600;
  public static bool SoulImmune(Pawn p)=>p==null || p.RaceProps==null || !p.RaceProps.IsFlesh || Items.Undead(p) || Necro.Construct(p)!=null || Legendary.Is(p) || HeroTraits.Info(p)?.lichbane==true; // 1.58.0 巫妖克星不会被汲取灵魂
  public static int Howl(Pawn d){
   int n=0;if(d?.Map==null)return 0;
   FleckMaker.Static(d.Position,d.Map,FleckDefOf.PsycastAreaEffect,12f);Items.Mote(d,"吞魂之嚎！",new Color(.7f,.9f,.8f));
   foreach(var p in Legendary.EnemiesNear(d,HowlRadius).ToList()){
    if(SoulImmune(p) || Legendary.Save(p,Adventurer.Con,HowlDC,d))continue;
    if(Items.Give(p,"LP_SoulTorn")!=null){n++;Items.Mote(p,"灵魂被撕裂！",new Color(.7f,.9f,.8f));}
   }
   return n;
  }

  // ---------- 掉落 ----------
  public static List<Thing> Loot(Pawn p){
   var l=new List<Thing>();
   void Add(string n,int c){var d=DefDatabase<ThingDef>.GetNamedSilentFail(n);if(d==null || c<=0)return;while(c>0){var t=ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null);t.stackCount=Mathf.Min(c,d.stackLimit);c-=t.stackCount;l.Add(t);}}
   switch(KindOf(p)){
    case Behir:Add("LP_BehirHorn",2);Add("LP_Leather_Behir",60);break;
    case Beholder:Add("LP_BeholderEye",1);Add("LP_BeholderStalk",StalkDrop(p));break; // 1.63.0：眼柄 2～4 根（按剩下的眼柄数），卷轴书桌的眼柄配方要用
    case Demilich:Add("LP_SoulGem",1);Add("LP_DemilichSkull",1);break;
   }
   return l;
  }
  public static void OnKilled(Pawn p,Map map,IntVec3 at){
   if(map==null || !IsMonster(p) || Is(p,DeathKnight))return;
   foreach(var t in Loot(p))GenPlace.TryPlaceThing(t,at,map,ThingPlaceMode.Near);
   if(Is(p,Behir)){var body=p.health.hediffSet.hediffs.OfType<Hediff_MonsterBody>().FirstOrDefault();body?.Release("贝希尔死了");}
  }

  // ---------- 世界地点：眼魔巢穴、无尽墓穴 ----------
  public const float FlayerLairChance=.2f;public const int LairDays=30;
  public static GameComponent_Monsters State=>GameComp<GameComponent_Monsters>.Get();
  public static bool IsFlayerLeader(Pawn p)=>p?.kindDef!=null && p.kindDef==FactionGroups.FlayerKind && p.Faction?.def==Factions.IllithidDef;
  public static Site OnFlayerLeaderKilled(Pawn p,Pawn killer){
   if(!BeholderOn || killer?.Faction==null || !killer.Faction.IsPlayer || Roll>=FlayerLairChance)return null;
   return RevealSite(Beholder,"夺心魔首领的残脑里还留着一段记忆：一只眼魔的巢穴");
  }
  public static Site RevealSite(string k,string why){
   if(k==Beholder?!BeholderOn:!DemilichOn)return null;var st=State;var f=Beasts();if(st==null || f==null)return null;
   var site=NetherilQuest.MakeSite(k==Beholder?"LP_BeholderLair":"LP_EndlessTomb",f,6,15,1f);if(site==null)return null;
   st.sites.Add(new MonsterSite{site=site,kind=k,expire=Find.TickManager.TicksGame+LairDays*GenDate.TicksPerDay});
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter(k==Beholder?"眼魔巢穴":"无尽墓穴",why+"，"+(k==Beholder?"眼魔巢穴（地下洞穴）":"无尽墓穴（半巫妖的坟墓）")+"显露在世界地图上（离殖民地 6～15 格），"+LairDays+"天后消失。\n\n"
    +(k==Beholder?"眼魔每 3 秒从眼柄射出 1～2 道射线（魅惑、麻痹、恐惧、减速、衰弱、石化、解离、死亡、睡眠、念动，全部可以豁免）；打掉一根眼柄就少一种射线。它正面 60° 锥形 20 格内不能施法、护盾腰带失效——从侧面和背后打。"
     :"半巫妖每 60 秒发出一次吞魂之嚎：20 格内所有活人做体质豁免（难度 21，基础成功率 10%，属性调整值和熟练加值每点 +5%），失败倒地、灵魂撕裂，30 秒内不救治就死。巫妖和死灵造物免疫。它只有一颗头骨，圣水和银器伤害 ×2。"),LetterDefOf.PositiveEvent,new LookTargets(site));
   return site;
  }
  public static MonsterSite SiteOf(Site s)=>s==null?null:State?.sites.FirstOrDefault(x=>x.site==s);

  // ---------- 龙的猎手（贝希尔） ----------
  public static bool HunterReady(Map map){
   if(!BehirOn || map==null)return false;
   if(DragonFlyover.AdultDragons(map).Count>0)return true;
   var rec=DefDatabase<RecordDef>.GetNamedSilentFail("LP_DragonsSlain");
   return rec!=null && PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.Any(p=>p.records!=null && p.records.GetValue(rec)>0f);
  }
  public static Pawn SpawnHunter(Map map,IntVec3? at=null){
   var f=Beasts();if(f==null)return null;var b=Make(Behir,f,map.Tile);if(b==null)return null;
   IntVec3 c;if(at.HasValue)c=at.Value;else if(!Events2.EdgeCell(map,out c))return null;
   GenSpawn.Spawn(b,c,map);LordMaker.MakeNewLord(f,new LordJob_AssaultColony(f,false,false,false,false,false),map,new List<Pawn>{b});
   return b;
  }

  // ---------- 塞伊：≥5000 点 10% 死亡骑士带队；拉洛克古墓 50% 多一名死亡骑士守卫 ----------
  public const float ThayPoints=5000f,ThayChance=.1f,CryptChance=.5f;
  public static Pawn ThayRaid(List<Pawn> list,PawnGroupMakerParms parms,bool force=false){
   if(!DeathKnightOn || list==null || parms?.faction==null || parms.faction.def!=Factions.HostileDef || parms.groupKind!=PawnGroupKindDefOf.Combat)return null;
   if(!force && (parms.points<ThayPoints || Roll>=ThayChance))return null;
   var k=Make(DeathKnight,parms.faction,parms.tile);if(k==null)return null;
   list.Insert(0,k);
   // 抵消点数：从最便宜的普通随从里删掉相当于死亡骑士一半战斗力的人。
   float need=k.kindDef.combatPower*.5f; // 只抵消一半：敌人等级的裁人之后还会再按等级删人
   foreach(var x in list.Where(x=>x!=k && x.RaceProps.Humanlike && !x.kindDef.trader && Necro.Construct(x)==null && !AdventurerEnemies.IsLeader(x) && !(Factions.Ext(x.Faction)?.leaderKinds.Contains(x.kindDef)??false)).OrderBy(x=>x.kindDef.combatPower).ToList()){
    if(need<=0f || list.Count(y=>y.RaceProps.Humanlike)<=3)break;need-=x.kindDef.combatPower;list.Remove(x);if(Find.WorldPawns.Contains(x))Find.WorldPawns.RemovePawn(x);x.Destroy();
   }
   return k;
  }
  // 斩首：Decapitation.Assign 之后，把红袍法师手下一半的活人随从改记在死亡骑士名下（法师的骷髅照旧跟法师），死亡骑士倒下时他们照原规则逃散。
  public static int BindKnight(List<Pawn> list){
   var k=list?.FirstOrDefault(x=>Is(x,DeathKnight));var tr=Decapitation.Tracker;if(k==null || tr==null)return 0;
   // 先挑贵的随从（之后敌人等级的“裁人”从最便宜的开始删，挑贵的才不会被删掉）。
   var cand=list.Where(x=>x!=k && x.RaceProps.Humanlike && Necro.Construct(x)==null && !x.kindDef.trader && !(Factions.Ext(x.Faction)?.leaderKinds.Contains(x.kindDef)??false)).OrderByDescending(x=>x.kindDef.combatPower).ToList();
   int n=Mathf.Max(1,(cand.Count+1)/2);
   foreach(var p in cand.Take(n))tr.Bind(p,k);
   return Mathf.Min(n,cand.Count);
  }
  public static Pawn CryptGuard(Map map,Faction f,bool force=false){
   if(!DeathKnightOn || map==null || f==null || !force && Roll>=CryptChance)return null;
   var k=Make(DeathKnight,f,map.Tile);if(k==null)return null;
   var c=map.Center;GenSpawn.Spawn(k,CellFinder.RandomClosewalkCellNear(c,map,4),map);
   LordMaker.MakeNewLord(f,new LordJob_DefendPoint(c,8f,14f,false,false),map,new List<Pawn>{k});
   return k;
  }
 }

 // ---------- XX之躯：招牌攻击、吞噬、化灰 ----------
 public class Hediff_MonsterBody : HediffWithComps {
  int ticks,breathTick,swallowTick,rayTick,hellTick,commandTick,howlTick=-1;public int downTick=-1;public bool charged,bound;
  public Pawn swallowed;public float outer,inner;public bool parleyed; // 1.63.0：半巫妖谈判过（每个半巫妖一次）
  public string Kind=>Monsters.KindOf(pawn);
  public override string LabelInBrackets=>swallowed!=null?"吞着"+swallowed.LabelShort:base.LabelInBrackets;
  public override void TickInterval(int delta){
   base.TickInterval(delta);if((ticks+=delta)<60)return;ticks=0;
   var p=pawn;int now=Find.TickManager.TicksGame;
   try{
    if(swallowed!=null)Digest();
    if(Kind==Monsters.DeathKnight && p.Downed && !p.Dead){if(downTick==-1)downTick=now;else if(now-downTick>=Monsters.AshTicks)Monsters.Ash(p);return;}
    downTick=-1;
    if(!p.Spawned || p.Dead || p.Downed || p.Map==null || !Monsters.AnyHostile(p))return;
    if(p.stances?.stunner?.Stunned==true)return;
    switch(Kind){
     case Monsters.Behir:
      if(!charged && now>=breathTick){breathTick=now+Dragons.ChargeTicks;if(Rand.Value<Dragons.ChargeChance)charged=true;}
      if(charged){var t=Monsters.BreathTarget(p,Monsters.BehirBreathRange);if(t!=null){charged=false;Monsters.BehirBreath(p,t.Position);}}
      if(swallowed==null && now>=swallowTick){var v=Monsters.Foes(p,1.9f).FirstOrDefault(x=>x.RaceProps.Humanlike && x.BodySize<=1.5f);if(v!=null){swallowTick=now+Monsters.SwallowCooldown;TrySwallow(v);}}
      break;
     case Monsters.Beholder:
      if(now>=rayTick){rayTick=now+Monsters.RayTicks;Monsters.FireRays(p,Rand.RangeInclusive(1,2));}
      break;
     case Monsters.DeathKnight:
      if(!bound){var lord=p.GetLord();if(lord!=null){bound=true;Monsters.BindKnight(lord.ownedPawns.ToList());}} // 登场后再绑定随从（敌人等级的裁人已经做完）
      if(now>=commandTick){commandTick=now+Monsters.CommandTicks;Monsters.Command(p);}
      if(now>=hellTick){var at=Monsters.ClusterTarget(p,25f);if(at.IsValid){hellTick=now+Monsters.HellfireTicks;Monsters.Hellfire(p.Map,at,p);Items.Mote(p,"地狱火球！",new Color(1f,.4f,.2f));}}
      break;
     case Monsters.Demilich:
      if(howlTick<0)howlTick=now+600;
      if(now>=howlTick && Legendary.EnemiesNear(p,Monsters.HowlRadius).Any(x=>!Monsters.SoulImmune(x))){howlTick=now+Monsters.HowlTicks;Monsters.Howl(p);}
      break;
    }
   }catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 传奇怪行为出错："+e,74152201);}
  }
  // 贝希尔吞噬：敏捷豁免失败就被吞进肚子（离开地图，存为世界角色）；每秒 4 点酸蚀；外面打它 60 或里面挣扎 30 就吐出来。
  public bool TrySwallow(Pawn v){
   if(swallowed!=null || v==null || v.Dead || !v.Spawned)return false;
   if(Legendary.Save(v,Adventurer.Dex,Legendary.DC(pawn,Adventurer.Str),pawn))return false;
   swallowed=v;outer=inner=0f;
   if(v.Spawned)v.DeSpawn();
   if(!Find.WorldPawns.Contains(v))Find.WorldPawns.PassToWorld(v,PawnDiscardDecideMode.KeepForever);
   Items.Mote(pawn,"吞下了"+v.LabelShort+"！",new Color(.6f,.8f,1f));
   if(v.Faction!=null && v.Faction.IsPlayer && !Adventurer.TestSuppressed)Messages.Message(pawn.LabelShort+"把"+v.LabelShort+"吞了下去！打它 60 点伤害就能让它吐出来。",pawn,MessageTypeDefOf.ThreatBig,false);
   return true;
  }
  public void Digest(){
   var v=swallowed;if(v==null)return;
   if(!v.Dead){v.TakeDamage(new DamageInfo(DefDatabase<DamageDef>.GetNamedSilentFail("LP_AlchemyAcid")??DamageDefOf.Burn,Monsters.AcidPerSecond,0f,-1f,pawn));
    if(!v.Dead && !v.Downed){inner+=4f+Mathf.Max(0,Legendary.Mod(v,Adventurer.Str));}}
   if(inner>=Monsters.SwallowInner)Release("被挣脱了");
  }
  public override void Notify_PawnPostApplyDamage(DamageInfo dinfo,float totalDamageDealt){
   base.Notify_PawnPostApplyDamage(dinfo,totalDamageDealt);
   if(swallowed!=null){outer+=totalDamageDealt;if(outer>=Monsters.SwallowOuter)Release("吃痛");}
  }
  public override void Notify_PawnDied(DamageInfo? dinfo,Hediff culprit){base.Notify_PawnDied(dinfo,culprit);Release("死了");}
  public Pawn Release(string why){
   var v=swallowed;if(v==null)return null;swallowed=null;outer=inner=0f;
   var map=pawn.MapHeld;var at=pawn.PositionHeld;
   if(Find.WorldPawns.Contains(v))Find.WorldPawns.RemovePawn(v);
   if(map!=null){
    if(v.Dead){if(v.Corpse!=null && !v.Corpse.Spawned)GenPlace.TryPlaceThing(v.Corpse,at,map,ThingPlaceMode.Near);}
    else GenSpawn.Spawn(v,CellFinder.RandomClosewalkCellNear(at,map,2),map);
   }
   Items.Mote(pawn,"吐出了"+v.LabelShort+"（"+why+"）",Color.white);
   return v;
  }
  public override void ExposeData(){
   base.ExposeData();Scribe_Values.Look(ref charged,"lpCharged");Scribe_Values.Look(ref bound,"lpBound");Scribe_References.Look(ref swallowed,"lpSwallowed",true);Scribe_Values.Look(ref outer,"lpOuter");Scribe_Values.Look(ref inner,"lpInner");Scribe_Values.Look(ref downTick,"lpDownTick",-1);Scribe_Values.Look(ref parleyed,"lpParleyed");
  }
 }
 // 灵魂撕裂：倒地；30 秒内不救治就死；包扎后变成“缝合的灵魂”，一天后消失。
 public class Hediff_SoulTorn : HediffWithComps {
  public const int DeathTicks=1800;
  public override void Tended(float quality,float maxQuality,int batchPosition=0){base.Tended(quality,maxQuality,batchPosition);if(Severity>.9f){Severity=.5f;var d=this.TryGetComp<HediffComp_Disappears>();if(d!=null)d.ticksToDisappear=GenDate.TicksPerDay;}}
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(Severity>.9f && ageTicks>=DeathTicks && !pawn.Dead){pawn.Kill(null,this);}
  }
 }

 // ---------- 存档 ----------
 public class MonsterSite : IExposable {public Site site;public string kind;public int expire;public void ExposeData(){Scribe_References.Look(ref site,"site");Scribe_Values.Look(ref kind,"kind");Scribe_Values.Look(ref expire,"expire");}}
 public class GameComponent_Monsters : GameComponent {
  public List<MonsterSite> sites=new List<MonsterSite>();
  public GameComponent_Monsters(Game g){}
  public override void GameComponentTick(){
   if(Find.TickManager.TicksGame%2000!=0 || sites.Count==0)return;
   foreach(var s in sites.ToList()){if(s.site==null || s.site.Destroyed){sites.Remove(s);continue;}if(!s.site.HasMap && Find.TickManager.TicksGame>=s.expire){s.site.Destroy();sites.Remove(s);}}
  }
  public override void ExposeData(){base.ExposeData();Scribe_Collections.Look(ref sites,"lpMonsterSites",LookMode.Deep);if(sites==null)sites=new List<MonsterSite>();}
 }

 // ---------- 地点地图 ----------
 public class GenStep_MonsterLair : GenStep {
  public string kind="beholder";
  public override int SeedPart=>741530+kind.Length;
  public override void Generate(Map map,GenStepParams parms){
   try{Build(map,kind,parms.sitePart?.site?.Faction??Monsters.Beasts());}catch(Exception e){Log.Error("[DND边缘大陆] 生成"+Monsters.Label(kind)+"地点出错："+e);}
  }
  public static Pawn Build(Map map,string kind,Faction f){
   var c=map.Center;int r=kind==Monsters.Beholder?10:8;
   var rock=DefDatabase<ThingDef>.GetNamedSilentFail(kind==Monsters.Beholder?"Slate":"Marble")??ThingDefOf.Granite;
   var floor=DefDatabase<TerrainDef>.GetNamedSilentFail(kind==Monsters.Beholder?"FlagstoneSlate":"FlagstoneMarble")??TerrainDefOf.Gravel;
   var room=CellRect.CenteredOn(c,r);
   foreach(var x in room.ExpandedBy(2).Cells){
    if(!x.InBounds(map))continue;foreach(var t in x.GetThingList(map).ToList())if(!(t is Pawn))t.Destroy(DestroyMode.Vanish);
    if(room.Contains(x)){map.terrainGrid.SetTerrain(x,floor);map.roofGrid.SetRoof(x,RoofDefOf.RoofRockThick);}
    else if(!(x.z<c.z+2 && x.z>c.z-2 && x.x>c.x)){GenSpawn.Spawn(ThingMaker.MakeThing(rock),x,map);}
   }
   foreach(var t in Hoard(kind))GenPlace.TryPlaceThing(t,c+IntVec3.West*3,map,ThingPlaceMode.Near);
   var m=Monsters.Make(kind,f,map.Tile);if(m==null)return null;GenSpawn.Spawn(m,c,map);
   LordMaker.MakeNewLord(f,new LordJob_DefendPoint(c,r,r+8f,false,false),map,new List<Pawn>{m});
   return m;
  }
  public static List<Thing> Hoard(string kind){
   var l=new List<Thing>();float target=kind==Monsters.Beholder?8000f:12000f,sum=0f;
   void Add(ThingDef d,int n){while(n>0 && d!=null){var t=ThingMaker.MakeThing(d);t.stackCount=Mathf.Min(n,d.stackLimit);n-=t.stackCount;l.Add(t);sum+=t.MarketValue*t.stackCount;}}
   Add(ThingDefOf.Gold,Mathf.FloorToInt(target*.4f/ThingDefOf.Gold.BaseMarketValue));
   Add(ThingDefOf.Silver,Mathf.CeilToInt((target-sum)/ThingDefOf.Silver.BaseMarketValue));
   for(int i=0;i<2;i++){var d=Epic.RandomMagicItemDef();if(d!=null)l.Add(ThingMaker.MakeThing(d,d.MadeFromStuff?GenStuff.DefaultStuffFor(d):null));}
   if(kind==Monsters.Demilich){var s=Legend.MakeStone();if(s!=null)l.Add(s);}
   return l;
  }
 }
 public class SitePartWorker_MonsterLair : SitePartWorker {}

 // ---------- 情报（同盟商队） ----------
 public class StockGenerator_MonsterIntel : StockGenerator {
  public string thing,kind;public int minLevel=1;public float chance=.15f;
  public override IEnumerable<Thing> GenerateThings(PlanetTile forTile,Faction faction=null){
   bool on=kind=="beholder"?Monsters.BeholderOn:kind=="demilich"?Monsters.DemilichOn:Dragons.LairOn;
   if(!on || AdventurerEnemies.ColonyMaxLevel()<minLevel || !Rand.Chance(chance))yield break;
   var d=DefDatabase<ThingDef>.GetNamedSilentFail(thing);if(d!=null)yield return ThingMaker.MakeThing(d);
  }
  public override bool HandlesThingDef(ThingDef d)=>d.defName==thing;
 }
 public class CompProperties_UseEffectMonsterIntel : CompProperties_UseEffect {public string kind;public CompProperties_UseEffectMonsterIntel(){compClass=typeof(CompUseEffect_MonsterIntel);}}
 public class CompUseEffect_MonsterIntel : CompUseEffect {
  string K=>((CompProperties_UseEffectMonsterIntel)props).kind;
  public override void DoEffect(Pawn usedBy){base.DoEffect(usedBy);Monsters.RevealSite(K,usedBy.LabelShort+"读懂了情报");}
  public override AcceptanceReport CanBeUsedBy(Pawn p)=>(K==Monsters.Beholder?Monsters.BeholderOn:Monsters.DemilichOn)?true:new AcceptanceReport("传奇怪没有开启（需要 Humanoid Alien Races）");
 }
 // 灵魂宝石：巫妖用 → 命匣获得 100 点献祭经验；其他人摔碎 → 放出灵魂，地图上的殖民者心情 +5。
 public class CompUseEffect_SoulGem : CompUseEffect {
  public const float LichFeed=100f;
  public override void DoEffect(Pawn p){
   base.DoEffect(p);var box=Necro.Lich(p)?.box;
   if(box!=null && !box.Destroyed){box.Feed(LichFeed);Messages.Message(p.LabelShort+"吸收了灵魂宝石里的灵魂：命匣 +"+LichFeed.ToString("0")+" 经验。",p,MessageTypeDefOf.PositiveEvent,false);return;}
   var th=DefDatabase<ThoughtDef>.GetNamedSilentFail("LP_SoulsFreed");
   if(th!=null && p.MapHeld!=null)foreach(var c in p.MapHeld.mapPawns.FreeColonistsSpawned)c.needs?.mood?.thoughts?.memories?.TryGainMemory(th);
  }
 }
 // 半巫妖头骨：巫妖用 → 命匣获得 300 点献祭经验（规格的“命匣灵魂上限 +50”在这台设备上没有对应的上限，见报告）。
 public class CompUseEffect_DemilichSkull : CompUseEffect {
  public const float LichFeed=300f;
  public override void DoEffect(Pawn p){base.DoEffect(p);Necro.Lich(p)?.box?.Feed(LichFeed);}
  public override AcceptanceReport CanBeUsedBy(Pawn p)=>Necro.Lich(p)?.box!=null?true:new AcceptanceReport("只有有命匣的巫妖能用");
 }

 // ---------- 事件：龙的猎手 ----------
 public class IncidentWorker_BehirHunt : IncidentWorker_DND {
  protected override bool CanFireNowSub(IncidentParms parms)=>parms.target is Map map && Monsters.HunterReady(map);
  protected override bool TryExecuteWorker(IncidentParms parms){
   var map=parms.target as Map;var b=Monsters.SpawnHunter(map);if(b==null)return false;
   SendStandardLetter("龙的猎手","一条贝希尔循着龙的气味找到了这里。它是真龙的死敌——对龙的伤害 ×1.5，不怕龙的威仪——但它同样饿了。\n\n它会喷出一道闪电，还会把离它太近的人一口吞下（打它 60 点伤害，它就会吐出来）。",LetterDefOf.ThreatBig,parms,new LookTargets(b));
   return true;
  }
 }

 // ---------- 传奇动作（传奇怪专用） ----------
 public class LA_Ray : LegendaryActionWorker {public override string Label=>"眼射线";public override bool CanDo(Pawn l,Pawn e)=>Monsters.Is(l,Monsters.Beholder) && Monsters.LiveRays(l).Count>0;public override bool Do(Pawn l,Pawn e)=>Monsters.FireRays(l,1)>0;}
 public class LA_Drift : LegendaryActionWorker {public override string Label=>"漂移";public override bool CanDo(Pawn l,Pawn e)=>Legendary.EnemiesNear(l,3f).Any();public override bool Do(Pawn l,Pawn e)=>ChromaticFlight.Hop(l,6f,l.Position+(l.Position-e.Position));}
 public class LA_Hellfire : LegendaryActionWorker {public override string Label=>"地狱火";public override bool Do(Pawn l,Pawn e){if(e.Position.DistanceTo(l.Position)>12f)return false;Monsters.Hellfire(l.Map,e.Position,l,Monsters.HellfireDamage*.5f);return true;}}
 public class LA_UndeadCommand : LegendaryActionWorker {public override string Label=>"亡灵号令";public override bool CanDo(Pawn l,Pawn e)=>Races124.PawnsNear(l.Map,l.Position,Monsters.CommandRadius).Any(p=>Monsters.UndeadAlly(l,p) && p.health.summaryHealth.SummaryHealthPercent<.9f);
  public override bool Do(Pawn l,Pawn e){int n=0;foreach(var p in Races124.PawnsNear(l.Map,l.Position,Monsters.CommandRadius).Where(p=>Monsters.UndeadAlly(l,p)).ToList()){foreach(var i in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Take(3).ToList())i.Heal(8f);n++;}return n>0;}}
 public class LA_SoulDrain : LegendaryActionWorker {public override string Label=>"吸魂";
  public override bool CanDo(Pawn l,Pawn e)=>!Monsters.SoulImmune(e) && e.Position.DistanceTo(l.Position)<=15f;
  public override bool Do(Pawn l,Pawn e){bool s=Legendary.Save(e,Adventurer.Con,Legendary.DC(l,Adventurer.Int),l);e.TakeDamage(new DamageInfo(Monsters.Necrotic,(s?10f:20f)*Dragons.DamageMult,.4f,-1f,l));foreach(var i in l.health.hediffSet.hediffs.OfType<Hediff_Injury>().Take(2).ToList())i.Heal(10f);return true;}}
 public class LA_Curse : LegendaryActionWorker {public override string Label=>"诅咒";
  public override bool CanDo(Pawn l,Pawn e)=>e.RaceProps.Humanlike && !Items.Has(e,"LP_DemilichCurse") && e.Position.DistanceTo(l.Position)<=15f;
  public override bool Do(Pawn l,Pawn e){if(!Legendary.Save(e,Adventurer.Wis,Legendary.DC(l,Adventurer.Int),l))Items.Give(e,"LP_DemilichCurse");return true;}}
 [StaticConstructorOnStartup]
 public static class MonsterStartup {
  static MonsterStartup(){
   Legendary.Register("Ray",new LA_Ray());Legendary.Register("Drift",new LA_Drift());Legendary.Register("Hellfire",new LA_Hellfire());
   Legendary.Register("UndeadCommand",new LA_UndeadCommand());Legendary.Register("SoulDrain",new LA_SoulDrain());Legendary.Register("Curse",new LA_Curse());
  }
 }

 // ---------- 补丁（都先看缓存，没有眼魔 / 护符就直接返回） ----------
 // 反魔法：原版技能（灵能、我们的巫妖法术和卷轴给的技能）施放失败。VPE 的施法检查在 Bridges/VPE/Source/AntiMagicVPE.cs。
 [HarmonyPatch(typeof(Ability),nameof(Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class AntiMagicAbility {static bool Prefix(Ability __instance,ref bool __result){if(__instance?.pawn==null || !Monsters.BlockCast(__instance.pawn))return true;__result=false;return false;}}
 [HarmonyPatch(typeof(CompShield),"get_ShieldState")]
 public static class AntiMagicShield {static void Postfix(CompShield __instance,ref ShieldState __result){if(__result==ShieldState.Active && Monsters.InAntiMagic((__instance.parent as Apparel)?.Wearer??__instance.parent as Pawn))__result=ShieldState.Disabled;}}
 // 塞伊袭击（FactionGroups.Process 的最后一步之后）、拉洛克古墓（GenStep_NetherilSite 的 crypt 之后）。
 [HarmonyPatch(typeof(FactionGroups),nameof(FactionGroups.Process))]
 public static class DeathKnightThayRaid {
  static void Postfix(PawnGroupMakerParms parms,ref IEnumerable<Pawn> __result){
   if(__result==null || parms?.faction?.def!=Factions.HostileDef || !Monsters.DeathKnightOn)return;
   var list=__result as List<Pawn>??__result.ToList();
   if(Monsters.ThayRaid(list,parms)!=null)Monsters.BindKnight(list);
   __result=list;
  }
 }
 [HarmonyPatch(typeof(GenStep_NetherilSite),nameof(GenStep_NetherilSite.Generate))]
 public static class DeathKnightCrypt {
  static void Postfix(GenStep_NetherilSite __instance,Map map,GenStepParams parms){if(__instance.kind=="crypt")Monsters.CryptGuard(map,parms.sitePart?.site?.Faction??NetherilQuest.Larloch);}
 }

 // ---------- 设置（画在「真龙与传奇怪」里） ----------
 public partial class Settings {
  public bool legendaryMonsters=true,behirHunt=true,beholderLair=true,demilichTomb=true,deathKnightThay=true;
  void ExposeMonsters(){
   Scribe_Values.Look(ref legendaryMonsters,"lpLegendaryMonsters",true);Scribe_Values.Look(ref behirHunt,"lpBehirHunt",true);Scribe_Values.Look(ref beholderLair,"lpBeholderLair",true);
   Scribe_Values.Look(ref demilichTomb,"lpDemilichTomb",true);Scribe_Values.Look(ref deathKnightThay,"lpDeathKnightThay",true);
  }
 }
 public static class MonsterSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;l.Gap(6f);
   l.CheckboxLabeled("传奇怪",ref s.legendaryMonsters,"贝希尔、眼魔、死亡骑士、半巫妖。都有传奇抗性和传奇动作，强度跟着上面的“真龙强度”。关闭后下面的登场都不出现。");
   bool har=Monsters.HarAvailable;var old=GUI.color;if(!har)GUI.color=Color.gray;
   l.CheckboxLabeled("　龙的猎手（贝希尔）"+(har?"":"（需要 HAR）"),ref s.behirHunt,"殖民地有成年力能龙裔、或有人杀过成年以上的真龙时，低概率来一条贝希尔。它也会出现在青年 / 成年龙巢附近，和龙打起来。");
   l.CheckboxLabeled("　眼魔巢穴"+(har?"":"（需要 HAR）"),ref s.beholderLair,"击杀幽暗殖巢的夺心魔首领后 20% 显露（或同盟商队卖情报）。");
   l.CheckboxLabeled("　无尽墓穴（半巫妖）"+(har?"":"（需要 HAR）"),ref s.demilichTomb,"殖民地最高冒险者等级 ≥24 时，同盟商队会卖墓穴情报。");
   GUI.color=old;
   l.CheckboxLabeled("　死亡骑士",ref s.deathKnightThay,"塞伊远征军 ≥5000 点的袭击 10% 由死亡骑士带队；拉洛克古墓里 50% 多一名死亡骑士守卫。不需要 HAR。");
  }
 }
}
