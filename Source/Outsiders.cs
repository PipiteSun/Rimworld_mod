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
 // 1.54.0 外来者层（见 ArtSource/Abyss-1.54.0-spec.md 第一节）：来自别的位面的生物都靠基因（或动物种族 ThingDef）上的 LP_OutsiderExtension 判定。
 // 1.24.0 的寒冰魔 / 炎魔迁移到这里：Fiends.Is = 外来者且 banishOnDown；击倒遣返、死亡消散、寒铁 / 圣水倍率都读扩展。
 public enum OutsiderPlane{Abyss,Hells,Feywild,Celestia,Elemental,FarRealm}
 public class LP_OutsiderExtension : DefModExtension {
  public OutsiderPlane plane=OutsiderPlane.Abyss;
  public bool banishOnDown=true;
  public float coldIron=1f,silver=1f,holy=1f,nonMagicFactor=1f;
  public bool taintImmune;
  public IntRange levelRange=IntRange.Zero;
  public float trueNameChance;
  public int priority;
  public bool lightBane; // 影魔：亮处受到伤害 ×1.5、移速 ×0.7（本版新增的字段）
 }

 public static class Outsiders {
  public const float RadiantFactor=1.25f,LightDamage=1.5f,LightSlow=.7f,LightGlow=.5f,WardSlow=.7f,WardRadius=6f;
  public const int ShackleRadius=5;
  public static float? TestRoll;
  public static float Roll=>TestRoll??Rand.Value;
  // 按 pawn 缓存（基因变化时清掉）。属性 / 伤害钩子可能不在主线程，用并发字典；值可以是 null（= 不是外来者）。
  static readonly System.Collections.Concurrent.ConcurrentDictionary<int,LP_OutsiderExtension> cache=new System.Collections.Concurrent.ConcurrentDictionary<int,LP_OutsiderExtension>();
  static List<GeneDef> genes;
  static List<GeneDef> Genes=>genes??(genes=DefDatabase<GeneDef>.AllDefsListForReading.Where(g=>g.GetModExtension<LP_OutsiderExtension>()!=null).ToList());
  public static LP_OutsiderExtension Of(Pawn p){
   if(p==null)return null;
   if(cache.TryGetValue(p.thingIDNumber,out var e))return e;
   e=Compute(p);cache[p.thingIDNumber]=e;return e;
  }
  static LP_OutsiderExtension Compute(Pawn p){
   var best=p.def?.GetModExtension<LP_OutsiderExtension>();
   if(p.genes!=null){var list=Genes;for(int i=0;i<list.Count;i++)if(p.genes.HasActiveGene(list[i])){var x=list[i].GetModExtension<LP_OutsiderExtension>();if(best==null || x.priority>best.priority)best=x;}}
   return best;
  }
  public static void Clear(Pawn p){if(p!=null)cache.TryRemove(p.thingIDNumber,out _);}
  public static void ClearAll()=>cache.Clear();
  public static bool Is(Pawn p)=>Of(p)!=null;
  public static bool IsDemon(Pawn p)=>Of(p)?.plane==OutsiderPlane.Abyss;
  public static bool IsDevil(Pawn p)=>Of(p)?.plane==OutsiderPlane.Hells;
  public static string PlaneWord(Pawn p){var e=Of(p);return e==null || e.plane==OutsiderPlane.Abyss?"深渊":e.plane==OutsiderPlane.Feywild?"妖精荒野":e.plane==OutsiderPlane.Celestia?"天界":"九狱";} // 1.55.0：妖精、天界

  // ---------- 状态 ----------
  static HediffDef bound,thrall,shackled,disguise;
  public static HediffDef BoundDef=>bound??(bound=Items.Hediff("LP_PlanarBinding"));
  public static HediffDef ThrallDef=>thrall??(thrall=Items.Hediff("LP_AbyssThrall"));
  public static HediffDef ShackledDef=>shackled??(shackled=Items.Hediff("LP_PlanarShackled"));
  public static HediffDef DisguiseDef=>disguise??(disguise=Items.Hediff("LP_SuccubusDisguise"));
  public static bool Bound(Pawn p)=>Items.Has(p,BoundDef);
  public static bool Thrall(Pawn p)=>Items.Has(p,ThrallDef);
  public static bool Shackled(Pawn p)=>Items.Has(p,ShackledDef);
  public static bool Disguised(Pawn p)=>Items.Has(p,DisguiseDef);
  // 遣返规则（规格 1.3）：敌人 / 野生 / 仆从击倒即遣返；束缚者和戴锚链的不遣返。死亡：除了戴锚链的都消散。
  public static bool BanishOnDown(Pawn p)=>Fiends.Is(p) && !Bound(p) && !Shackled(p);
  public static bool VanishOnDeath(Pawn p)=>Fiends.Is(p) && !Shackled(p);
  public static string KindLabel(Pawn p){
   if(p==null)return "外来者";
   if(Fiends.IsBalor(p))return "炎魔";if(Fiends.IsIceDevil(p))return "寒冰魔";
   if(AbyssRaces.IsSuccubus(p))return p.gender==Gender.Male?"梦魔":"魅魔";
   var x=p.genes?.Xenotype;if(x!=null && x!=XenotypeDefOf.Baseliner && p.RaceProps.Humanlike)return x.label;
   return p.kindDef?.label??p.def.label;
  }
  public static string KindKey(Pawn p)=>p==null?null:p.RaceProps.Humanlike?(p.genes?.Xenotype?.defName??p.def.defName):p.def.defName;

  // ---------- 遣返 / 消散 ----------
  static void Smoke(IntVec3 cell,Map map){for(int i=0;i<6;i++)FleckMaker.ThrowSmoke(cell.ToVector3Shifted()+new Vector3(Rand.Range(-.5f,.5f),0,Rand.Range(-.5f,.5f)),map,2.2f);FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,1.6f);}
  // 仆从 / 束缚者：衣物、背包里的东西放在原地。
  public static void DropBelongings(Pawn p,IntVec3 cell,Map map){
   if(p.apparel!=null)foreach(var a in p.apparel.WornApparel.ToList()){p.apparel.Remove(a);GenPlace.TryPlaceThing(a,cell,map,ThingPlaceMode.Near);}
   if(p.inventory!=null)foreach(var t in p.inventory.innerContainer.ToList()){p.inventory.innerContainer.Remove(t);GenPlace.TryPlaceThing(t,cell,map,ThingPlaceMode.Near);}
  }
  // 被击倒（或仆从到期）：本体化作黑烟。只有敌人掉战利品（含真名卷）。
  public static bool Banish(Pawn p){
   if(p==null || p.Destroyed || !p.Spawned || !Fiends.Is(p))return false;
   var map=p.Map;var cell=p.Position;bool balor=Fiends.IsBalor(p),own=Thrall(p) || Bound(p);string label=KindLabel(p);
   if(!own)PactRaces.BanishLoot(p,cell,map); // 1.55.0：角魔钢叉 30%、狂猎骑手、宗主代理人的真名卷
   if(p.equipment?.Primary!=null && p.equipment.TryDropEquipment(p.equipment.Primary,out var w,cell,false))Fiends.Excellent(w);
   if(own)DropBelongings(p,cell,map);
   else{Fiends.DropAt(Fiends.Loot(p),cell,map);Fiends.DropAt(ExtraLoot(p),cell,map);}
   Smoke(cell,map);
   if(balor)Fiends.Schedule(map,cell);
   Messages.Message(balor?"炎魔的身体开始崩解！":label+"被遣返"+PlaneWord(p)+"。",new TargetInfo(cell,map),MessageTypeDefOf.NeutralEvent,false);
   if(!own)Thinness.Add(map,Thinness.KillGain,null);
   p.Destroy(DestroyMode.Vanish);
   return true;
  }
  public static bool BanishCorpse(Pawn p){
   var corpse=p?.Corpse;if(corpse==null || corpse.Destroyed || !Fiends.Is(p))return false;
   var map=corpse.MapHeld;var cell=corpse.PositionHeld;if(map==null)return false;bool balor=Fiends.IsBalor(p),bound=Bound(p),own=bound || Thrall(p);
   if(!own)PactRaces.BanishLoot(p,cell,map);
   if(p.equipment?.Primary!=null && p.equipment.TryDropEquipment(p.equipment.Primary,out var w,cell,false))Fiends.Excellent(w);
   if(own)DropBelongings(p,cell,map);
   else{Fiends.DropAt(Fiends.Loot(p),cell,map);Fiends.DropAt(ExtraLoot(p),cell,map);}
   Smoke(cell,map);
   if(balor)Fiends.Schedule(map,cell);
   if(bound)Find.LetterStack.ReceiveLetter(p.LabelShort+"回到了"+PlaneWord(p),p.LabelShort+"死了。它的身体化作黑烟，回到了"+PlaneWord(p)+"——束缚随之断开，留下的装备和物品掉在原地。外来者的灵魂不在这个世界，无法复活。",LetterDefOf.Death,new TargetInfo(cell,map));
   else Messages.Message(balor?"炎魔的身体开始崩解！":KindLabel(p)+"被遣返"+PlaneWord(p)+"。",new TargetInfo(cell,map),MessageTypeDefOf.NeutralEvent,false);
   corpse.Destroy();
   return true;
  }
  // 深渊恶魔的掉落：灵魂币（劣魔 10%×1，其他 50%×1～3）、恶魔血（非劣魔 60%）、饱魂之牙（纳巴苏 40%），按 trueNameChance 掉真名卷。
  public static List<Thing> ExtraLoot(Pawn p){
   var list=new List<Thing>();var e=Of(p);if(e==null)return list;
   if(e.plane==OutsiderPlane.Abyss){
    bool dretch=AbyssRaces.Is(p,AbyssRaces.Dretch);
    if(Roll<(dretch?.1f:.5f))list.Add(Make(AbyssItems.SoulCoin,dretch?1:Rand.RangeInclusive(1,3)));
    if(!dretch && Roll<.6f)list.Add(Make(AbyssItems.DemonBlood,1));
    if(AbyssRaces.Is(p,AbyssRaces.Nabassu) && Roll<.4f)list.Add(Make(AbyssItems.GorgedFang,1));
   }
   if(e.trueNameChance>0 && Roll<e.trueNameChance){var s=TrueNames.MakeScroll(p);if(s!=null)list.Add(s);}
   list.RemoveAll(t=>t==null);
   return list;
  }
  static Thing Make(ThingDef def,int n){if(def==null)return null;var t=ThingMaker.MakeThing(def,def.MadeFromStuff?GenStuff.DefaultStuffFor(def):null);t.stackCount=Math.Max(1,n);return t;}

  // 位面锚链：殖民者背包里带着锚链、站在 5 格内时，倒下的敌对外来者被自动锁住（不再遣返，可以俘虏），用掉一条。
  public static bool TryAutoShackle(Pawn p){
   if(p?.Map==null || p.Faction?.IsPlayer==true || AbyssItems.Shackles==null)return false;
   foreach(var c in p.Map.mapPawns.FreeColonistsSpawned){
    if(c.Downed || !c.Position.InHorDistOf(p.Position,ShackleRadius))continue;
    var s=c.inventory?.innerContainer.FirstOrDefault(t=>t.def==AbyssItems.Shackles);if(s==null)continue;
    s.SplitOff(1).Destroy();Shackle(p,c);return true;
   }
   return false;
  }
  public static void Shackle(Pawn p,Pawn by){
   if(p==null || Shackled(p) || ShackledDef==null)return;
   p.health.AddHediff(ShackledDef);
   if(p.Spawned)FleckMaker.Static(p.Position,p.Map,FleckDefOf.PsycastAreaEffect,1.2f);
   Messages.Message((by!=null?by.LabelShort+"用":"")+"位面锚链锁住了"+KindLabel(p)+"：它不会再被遣返，可以俘虏。",p,MessageTypeDefOf.PositiveEvent,false);
  }

  // ---------- 克制 ----------
  public static float HolyFactor(Pawn p)=>Of(p)?.holy??1f;
  static readonly Dictionary<ThingDef,bool> magic=new Dictionary<ThingDef,bool>();
  // 1.45.0 魔法物品：交易标签 LP_MagicWeapon / LP_MagicItem，或带魔法武器扩展。
  public static bool MagicWeapon(ThingDef d){
   if(d==null)return false;
   lock(magic){
    if(magic.TryGetValue(d,out bool m))return m;
    m=d.tradeTags!=null && (d.tradeTags.Contains("LP_MagicWeapon") || d.tradeTags.Contains("LP_MagicItem")) || d.GetModExtension<MagicWeaponExtension>()!=null;
    magic[d]=m;return m;
   }
  }
  // 非克制、非魔法武器（品质 < 杰作）：天生的爪牙也算。能力、爆炸（没有武器来源）不算。
  public static bool NonMagic(DamageInfo d){
   var def=d.Weapon;if(def==null)return false;
   if(def.race!=null)return true;
   if(MagicWeapon(def))return false;
   var w=(d.Instigator as Pawn)?.equipment?.Primary;
   if(w!=null && w.def==def){
    if(w.Stuff!=null && (w.Stuff==ItemMaterials.ColdIron || w.Stuff==ThingDefOf.Silver))return false;
    if(w.TryGetQuality(out var q) && q>=QualityCategory.Masterwork)return false;
   }
   return true;
  }
  public static bool Lit(Pawn p)=>p?.Map!=null && p.Spawned && p.Map.glowGrid.GroundGlowAt(p.Position,false,false)>=LightGlow;
  public static float DamageFactor(Pawn p,LP_OutsiderExtension e,DamageInfo d){
   float f=1f;
   if(d.Def!=null && d.Def==Faith.Radiant && d.Def!=DamageDefOf.Burn)f*=RadiantFactor;
   if(e.nonMagicFactor!=1f && NonMagic(d))f*=e.nonMagicFactor;
   if(e.lightBane && Lit(p))f*=LightDamage;
   return f;
  }
 }

 // ---------- 真名 ----------
 public static class TrueNames {
  static readonly string[] Syl={"巴","尔","祖","格","莫","拉","克","萨","斯","纳","乌","赫","兹","特","鲁","姆","奥","伊","卡","洛","戈","叙","达","瓦","泽","索","玛","库","德","弗"};
  public static string Generate(int seed){
   int n=3+(int)(Rand.ValueSeeded(seed^0x7A11)*2f);var s="";
   for(int i=0;i<n;i++)s+=Syl[Mathf.Clamp((int)(Rand.ValueSeeded(seed*31+i*977)*Syl.Length),0,Syl.Length-1)];
   return s;
  }
  // 每个外来者第一次查询（或生成）时记下真名，存在 GameComponent 里，不给 pawn 加 comp。
  public static string Of(Pawn p){
   if(p==null || !Outsiders.Is(p))return null;var st=AbyssState.Get;if(st==null)return Generate(p.thingIDNumber);
   if(!st.trueNames.TryGetValue(p.thingIDNumber,out var n))st.trueNames[p.thingIDNumber]=n=Generate(p.thingIDNumber);
   return n;
  }
  public static Thing MakeScroll(Pawn p){
   if(AbyssItems.TrueNameScroll==null || p==null)return null;
   var t=ThingMaker.MakeThing(AbyssItems.TrueNameScroll);var c=t.TryGetComp<CompTrueName>();
   if(c!=null){c.trueName=Of(p);c.kind=Outsiders.KindKey(p);c.kindLabel=Outsiders.KindLabel(p);c.pawnId=p.thingIDNumber;}
   return t;
  }
  public static IEnumerable<CompTrueName> On(Map map){
   if(map==null || AbyssItems.TrueNameScroll==null)yield break;
   foreach(var t in map.listerThings.ThingsOfDef(AbyssItems.TrueNameScroll)){var c=t.TryGetComp<CompTrueName>();if(c!=null)yield return c;}
   foreach(var p in map.mapPawns.FreeColonistsSpawned){var inv=p.inventory?.innerContainer;if(inv==null)continue;for(int i=0;i<inv.Count;i++)if(inv[i].def==AbyssItems.TrueNameScroll){var c=inv[i].TryGetComp<CompTrueName>();if(c!=null)yield return c;}}
  }
  public static bool HeldFor(Pawn p)=>p!=null && On(p.MapHeld).Any(c=>c.pawnId==p.thingIDNumber);
  public static bool HeldKind(Map map,string kind)=>kind!=null && On(map).Any(c=>c.kind==kind);
  // 对敌人使用：它做魅力豁免，DC = 使用者的魅力 DC；失败立刻遣返。卷都会用掉。
  public static bool UseOnPawn(Pawn user,Pawn target,Thing scroll){
   if(target==null || target.Dead || !target.Spawned)return false;
   bool saved=Legendary.Save(target,Adventurer.Cha,Legendary.DC(user,Adventurer.Cha),user);
   scroll?.Destroy();
   if(saved){Messages.Message(Outsiders.KindLabel(target)+"顶住了自己的真名。",target,MessageTypeDefOf.NegativeEvent,false);return false;}
   Messages.Message(user.LabelShort+"念出了"+Outsiders.KindLabel(target)+"的真名——它被逐回了"+Outsiders.PlaneWord(target)+"！",target,MessageTypeDefOf.PositiveEvent,false);
   if(Fiends.Is(target))Outsiders.Banish(target);
   else{var map=target.Map;var cell=target.Position;FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,1.6f);target.Destroy(DestroyMode.Vanish);}
   return true;
  }
  public static bool UseOnRift(Pawn user,Building_PlanarRift rift,Thing scroll){
   if(rift?.Comp==null || rift.Destroyed)return false;scroll?.Destroy();
   rift.Comp.Close(user.LabelShort+"念出了一个恶魔的真名，裂口被这个名字封住",true);return true;
  }
 }
 public class CompProperties_TrueName : CompProperties {public CompProperties_TrueName(){compClass=typeof(CompTrueName);}}
 public class CompTrueName : ThingComp {
  public string trueName,kind,kindLabel;public int pawnId=-1;
  public override string TransformLabel(string label)=>trueName.NullOrEmpty()?label:label+"："+trueName+"（"+kindLabel+"）";
  public override bool AllowStackWith(Thing other)=>false;
  public override string CompInspectStringExtra(){
   if(trueName.NullOrEmpty())return "空白的真名卷。";
   var p=AbyssBinding.PawnById(pawnId);string who=p==null?"它已不在这个世界":p.Faction?.IsPlayer==true?"它是你的"+(Outsiders.Bound(p)?"束缚者":"仆从")+"（持有这张卷：服从度衰减减半）":"它还在外面游荡";
   return "记载着"+kindLabel+"的真名："+trueName+"。"+who+"。";
  }
  public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn){
   foreach(var o in base.CompFloatMenuOptions(selPawn))yield return o;
   if(selPawn?.Faction?.IsPlayer!=true || trueName.NullOrEmpty() || parent.MapHeld==null)yield break;
   var map=parent.MapHeld;var target=AbyssBinding.PawnById(pawnId);
   var job=DefDatabase<JobDef>.GetNamedSilentFail("LP_UseTrueName");if(job==null)yield break;
   if(target!=null && target.Spawned && target.Map==map && !target.Dead && target.HostileTo(selPawn))yield return new FloatMenuOption("用真名驱逐"+target.LabelShort+"（它做魅力豁免，难度 "+Legendary.DC(selPawn,Adventurer.Cha)+"，它的成功率 "+Legendary.SaveChance(target,Adventurer.Cha,Legendary.DC(selPawn,Adventurer.Cha),selPawn).ToStringPercent()+"）",()=>selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job,parent,target),JobTag.Misc));
   foreach(var r in PortalEvent.Rifts(map).Where(r=>r.Comp?.plane?.holyClosable==true))yield return new FloatMenuOption("用真名封住深渊裂口（用掉这张卷）",()=>selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(job,parent,r),JobTag.Misc));
  }
  public override void PostExposeData(){base.PostExposeData();Scribe_Values.Look(ref trueName,"lpTrueName");Scribe_Values.Look(ref kind,"lpKind");Scribe_Values.Look(ref kindLabel,"lpKindLabel");Scribe_Values.Look(ref pawnId,"lpPawnId",-1);}
 }
 // 拿起真名卷，走到目标 8 格内（裂口走到旁边），念出真名。
 public class JobDriver_UseTrueName : JobDriver {
  public override bool TryMakePreToilReservations(bool errorOnFailed)=>pawn.Reserve(job.targetA,job,1,-1,null,errorOnFailed);
  protected override IEnumerable<Toil> MakeNewToils(){
   this.FailOnDespawnedOrNull(TargetIndex.B);
   yield return Toils_Goto.GotoThing(TargetIndex.A,PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
   yield return Toils_Haul.StartCarryThing(TargetIndex.A);
   var go=Toils_Goto.GotoThing(TargetIndex.B,PathEndMode.Touch);
   go.AddPreTickAction(()=>{var t=TargetB.Thing;if(t is Pawn && pawn.Position.InHorDistOf(t.Position,8f) && GenSight.LineOfSight(pawn.Position,t.Position,pawn.Map))ReadyForNextToil();});
   yield return go;
   yield return Toils_General.Wait(90).WithProgressBarToilDelay(TargetIndex.B);
   yield return Toils_General.Do(()=>{
    var scroll=pawn.carryTracker.CarriedThing;if(scroll==null || scroll.def!=AbyssItems.TrueNameScroll)return;
    pawn.carryTracker.innerContainer.Remove(scroll);
    if(TargetB.Thing is Pawn t)TrueNames.UseOnPawn(pawn,t,scroll);else if(TargetB.Thing is Building_PlanarRift r)TrueNames.UseOnRift(pawn,r,scroll);
   });
  }
 }

 // ---------- 本地图外来者登记表（规格第九节） ----------
 public class MapComponent_Outsiders : MapComponent {
  public readonly List<Pawn> list=new List<Pawn>();
  public readonly List<Thing> glyphs=new List<Thing>();
  public MapComponent_Outsiders(Map map):base(map){}
  public static MapComponent_Outsiders Of(Map map)=>MapComp<MapComponent_Outsiders>.Get(map);
  public static bool Tracked(Pawn p)=>Outsiders.Is(p) || Outsiders.Disguised(p);
  public void Register(Pawn p){if(p!=null && !list.Contains(p)){list.Add(p);version++;}}
  public void Unregister(Pawn p){if(list.Remove(p))version++;}
  // 1.58.6：ForbidUtility.IsForbidden 的魅魔门槛。原来的 AbyssRaces.succubi 只增不减（出现过一只魅魔后每次工作扫描都要多查一次）；
  // 现在看各地图登记表里有没有“已生成的魅魔”，登记表或基因变化时（version）才重算。
  internal static int version;static int countedVersion=-1;static bool anySuccubus;
  public static bool AnySpawnedSuccubus(){
   if(countedVersion==version)return anySuccubus;
   int v=version;bool any=false;
   foreach(var m in Find.Maps){var r=Of(m);if(r==null)continue;for(int i=0;i<r.list.Count && !any;i++){var p=r.list[i];if(p!=null && p.Spawned && AbyssRaces.IsSuccubus(p))any=true;}if(any)break;}
   anySuccubus=any;countedVersion=v;return any;
  }
  public override void FinalizeInit(){base.FinalizeInit();Rebuild();}
  public void Rebuild(){
   list.Clear();foreach(var p in map.mapPawns.AllPawnsSpawned)if(Tracked(p))list.Add(p);version++;
   glyphs.Clear();if(AbyssItems.WardingGlyph!=null)glyphs.AddRange(map.listerThings.ThingsOfDef(AbyssItems.WardingGlyph));
  }
  public bool Warded(IntVec3 c){for(int i=0;i<glyphs.Count;i++)if(glyphs[i].Spawned && glyphs[i].Position.InHorDistOf(c,Outsiders.WardRadius))return true;return false;}
  int tick;
  public override void MapComponentTick(){
   if(list.Count==0 || ++tick<250)return;tick=0;
   AbyssRaces.CheckUnmasking(this);
  }
 }

 // ---------- 存档状态 ----------
 public class AbyssState : GameComponent {
  public Dictionary<int,string> trueNames=new Dictionary<int,string>();
  public List<string> known=new List<string>();
  public int hunterLayers;
  public Dictionary<string,int> kissImmune=new Dictionary<string,int>();
  public Dictionary<int,int> interrogated=new Dictionary<int,int>();
  public List<AbyssPending> pending=new List<AbyssPending>();
  public List<Pawn> hunters=new List<Pawn>();
  public Map huntMap;public int huntTick=-1;
  public int dailyTick=-1;
  public AbyssState(Game game){Outsiders.ClearAll();}
  public static AbyssState Get=>GameComp<AbyssState>.Get();
  public bool Known(string demon)=>known.Contains(demon);
  public void Learn(string demon){if(!demon.NullOrEmpty() && !known.Contains(demon))known.Add(demon);}
  public override void GameComponentTick(){
   int now=Find.TickManager.TicksGame;if(now%250!=17)return;
   if(pending.Count>0)AbyssEvents.ProcessPending(this,now);
   if(hunters.Count>0 || huntTick>=0)AbyssEvents.TickHunters(this,now);
   if(dailyTick<0)dailyTick=now;
   if(now-dailyTick>=GenDate.TicksPerDay){dailyTick=now;AbyssTaint.DailyGoodwill();}
  }
  public override void ExposeData(){
   Scribe_Collections.Look(ref trueNames,"lpTrueNames",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref known,"lpKnownDemons",LookMode.Value);
   Scribe_Values.Look(ref hunterLayers,"lpHunterLayers");
   Scribe_Collections.Look(ref kissImmune,"lpKissImmune",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref interrogated,"lpInterrogated",LookMode.Value,LookMode.Value);
   Scribe_Collections.Look(ref pending,"lpAbyssPending",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.Saving)hunters.RemoveAll(p=>p==null || p.Destroyed || p.Discarded);
   Scribe_Collections.Look(ref hunters,"lpHunters",LookMode.Reference);
   Scribe_References.Look(ref huntMap,"lpHuntMap");Scribe_Values.Look(ref huntTick,"lpHuntTick",-1);
   Scribe_Values.Look(ref dailyTick,"lpAbyssDaily",-1);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    if(trueNames==null)trueNames=new Dictionary<int,string>();if(known==null)known=new List<string>();if(kissImmune==null)kissImmune=new Dictionary<string,int>();
    if(interrogated==null)interrogated=new Dictionary<int,int>();pending=pending?.Where(x=>x!=null).ToList()??new List<AbyssPending>();hunters=hunters?.Where(p=>p!=null).ToList()??new List<Pawn>();
   }
  }
 }
 // 延后的事：尸体起身成劣魔（腐化 ≥0.9 死亡 6 小时 / 被噬魂 24 小时）。
 public class AbyssPending : IExposable {
  public Corpse corpse;public int tick;public string reason;
  public void ExposeData(){Scribe_References.Look(ref corpse,"corpse");Scribe_Values.Look(ref tick,"tick");Scribe_Values.Look(ref reason,"reason");}
 }

 // ---------- Harmony ----------
 [HarmonyPatch(typeof(Pawn_GeneTracker),"Notify_GenesChanged")]
 public static class OutsiderGenesChanged {
  static void Postfix(Pawn_GeneTracker __instance){
   var p=__instance.pawn;if(p==null)return;Outsiders.Clear(p);AbyssRaces.ClearCache(p);MapComponent_Outsiders.version++;
   if(p.Spawned){var r=MapComponent_Outsiders.Of(p.Map);if(r!=null){if(MapComponent_Outsiders.Tracked(p))r.Register(p);else r.Unregister(p);}}
  }
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.SpawnSetup))]
 public static class OutsiderRegisterSpawn {static void Postfix(Pawn __instance){if(MapComponent_Outsiders.Tracked(__instance))MapComponent_Outsiders.Of(__instance.Map)?.Register(__instance);}}
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.DeSpawn))]
 public static class OutsiderRegisterDespawn {static void Prefix(Pawn __instance){if(__instance.Map!=null && MapComponent_Outsiders.Tracked(__instance))MapComponent_Outsiders.Of(__instance.Map)?.Unregister(__instance);}}
 // 光辉、非魔法武器、亮处（影魔）。非外来者第一行返回（缓存查询）。束缚者被殖民地的寒铁武器打到：服从度 -5。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class OutsiderDamage {
  static void Prefix(Pawn __instance,ref DamageInfo __0){
   var e=Outsiders.Of(__instance);if(e==null)return;
   float f=Outsiders.DamageFactor(__instance,e,__0);if(f!=1f)__0.SetAmount(__0.Amount*f);
   if(__0.Amount>0f && Outsiders.Bound(__instance))AbyssBinding.ColdIronHit(__instance,__0);
  }
 }
 // 外来者生成：记真名；深渊魔群兵种按扩展的等级范围定冒险者等级。
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class OutsiderOnGenerated {
  static void Postfix(Pawn __result){
   if(__result==null || !Outsiders.Is(__result))return;
   TrueNames.Of(__result);
   var kn=__result.kindDef?.defName; // 1.55.0：九狱军团 / 掮客、狂猎、妖精宫廷、天界的兵种也按扩展定等级
   if(kn!=null && (kn.StartsWith("LP_AH_") || kn.StartsWith("LP_HL_") || kn.StartsWith("LP_HB_") || kn.StartsWith("LP_FW_") || kn.StartsWith("LP_FY_") || kn.StartsWith("LP_CE_")))AbyssBinding.SetLevel(__result,Outsiders.Of(__result).levelRange.RandomInRange,false);
  }
 }
 // 击杀外来者：地图薄弱度 +1（遣返的在 Banish 里算）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class OutsiderKilled {
  static void Prefix(Pawn __instance,out Map __state){__state=__instance.Dead?null:__instance.MapHeld;}
  static void Postfix(Pawn __instance,Map __state,DamageInfo? __0){
   if(__state==null || !__instance.Dead)return;
   if(Outsiders.Is(__instance) && !Outsiders.Bound(__instance) && !Outsiders.Thrall(__instance))Thinness.Add(__state,Thinness.KillGain,null);
   AbyssEvents.OnDeath(__instance,__0,__state);
  }
 }

 // ---------- 设置（规格第八节，“深渊”分组） ----------
 public partial class Settings {
  public bool abyssEnabled=true,abyssBindColonists=true,abyssShowThinness=false;
  public int abyssTaintPercent=100,abyssThinPercent=100,abyssBoundExtra=0,abyssThrallCap=8;
  void ExposeAbyss(){
   Scribe_Values.Look(ref abyssEnabled,"abyssEnabled",true);Scribe_Values.Look(ref abyssBindColonists,"abyssBindColonists",true);Scribe_Values.Look(ref abyssShowThinness,"abyssShowThinness",false);
   Scribe_Values.Look(ref abyssTaintPercent,"abyssTaintPercent",100);Scribe_Values.Look(ref abyssThinPercent,"abyssThinPercent",100);Scribe_Values.Look(ref abyssBoundExtra,"abyssBoundExtra",0);Scribe_Values.Look(ref abyssThrallCap,"abyssThrallCap",8);
   abyssTaintPercent=Math.Max(25,Math.Min(200,abyssTaintPercent));abyssThinPercent=Math.Max(0,Math.Min(200,abyssThinPercent));abyssBoundExtra=Math.Max(0,Math.Min(5,abyssBoundExtra));abyssThrallCap=Math.Max(0,Math.Min(16,abyssThrallCap));
   ExposePact(); // 1.55.0 异界契约的设置跟在深渊后面存（不改 Core.cs）
  }
 }
 public static class AbyssSettings {
  public static bool Enabled=>LichMod.Settings?.abyssEnabled??true;
  public static bool BindColonists=>LichMod.Settings?.abyssBindColonists??true;
  public static float TaintSpeed=>(LichMod.Settings?.abyssTaintPercent??100)/100f;
  public static float ThinFrequency=>(LichMod.Settings?.abyssThinPercent??100)/100f;
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.GapLine();l.Label("深渊");
   l.CheckboxLabeled("启用深渊内容",ref s.abyssEnabled,"（1.54.0）深渊学研究、深渊法阵、腐化和深渊事件。关掉后研究隐藏、事件不再触发；已有的束缚者和仆从保留。");
   l.Label("腐化速度："+s.abyssTaintPercent+"%");s.abyssTaintPercent=(int)l.Slider(s.abyssTaintPercent,25,200);
   l.Label("薄弱度事件频率："+s.abyssThinPercent+"%（深渊低语、深渊入侵）");s.abyssThinPercent=(int)l.Slider(s.abyssThinPercent,0,200);
   l.CheckboxLabeled("允许束缚恶魔成为殖民者",ref s.abyssBindColonists,"关掉后二阶仪式只能召仆从，三阶整阶不可用，魅魔来访不触发。");
   l.Label("束缚者额外上限："+s.abyssBoundExtra);s.abyssBoundExtra=(int)l.Slider(s.abyssBoundExtra,0,5);
   l.Label("仆从上限："+s.abyssThrallCap);s.abyssThrallCap=(int)l.Slider(s.abyssThrallCap,0,16);
   l.CheckboxLabeled("在地图上显示位面薄弱度",ref s.abyssShowThinness,"在有深渊法阵或开过深渊裂口的地图左上角显示薄弱度。");
   PactSettings.Draw(l); // 1.55.0：“异界契约”分组画在“深渊”下面（不改 Core.cs 的分组表）
  }
 }
}
