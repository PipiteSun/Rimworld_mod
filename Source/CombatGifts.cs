using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.66.0 武艺天赋（见 ArtSource/CombatGifts-1.66.0-spec.md）：13 个蓝色特性，和强力特性分开计数；1.66.0 热修起每人最多 Max 个（设置，默认 3），不再用 exclusionTag 限一个。
 // commonality 0，生成后缀里自己掷（开局殖民者 35%，普通人形按设置 8%，传奇种族减半，首领 / 冠军 / 传奇怪 50%）。
 // 阶段：入门 → 精通（冒险者 10 级，×1.5）→ 化境（传奇冒险者，×2）；巨灵许愿可以直接升一阶（Hediff_Adventurer.cgBonus）。
 // 属性类：Hediff_Adventurer.Eff 在上限之后加 +2/+3/+4（等于上限同样提高），走 1.65.0 的倍率曲线；附带效果和战技的数值合成在冒险者状态的 BuildStage 里（AddStats）。
 // 热路径（Eff、伤害）先查按 thingIDNumber 缓存的天赋下标（-1 = 没有），特性变动时失效。
 [DefOf]
 public static class CombatGiftDefOf {
  public static TraitDef LP_CG_Mighty,LP_CG_Swift,LP_CG_Stalwart,LP_CG_Brilliant,LP_CG_Eagle,LP_CG_Commanding;
  public static TraitDef LP_CG_Flurry,LP_CG_Rage,LP_CG_Deadeye,LP_CG_Lethal,LP_CG_Bulwark,LP_CG_Regen,LP_CG_Arcane;
  static CombatGiftDefOf(){DefOfHelper.EnsureInitializedInCtor(typeof(CombatGiftDefOf));}
 }

 public static class CombatGifts {
  public const string Tag="LP_CombatGift";
  // 下标：0～5 属性类（和六维下标一致），6～12 战技类。
  public const int Mighty=0,Swift=1,Stalwart=2,Brilliant=3,Eagle=4,Commanding=5,Flurry=6,Rage=7,Deadeye=8,Lethal=9,Bulwark=10,Regen=11,Arcane=12,Count=13;
  public static readonly string[] Names={"巨力","迅捷","坚韧之体","博闻强识","鹰眼","王者气度","连斩","狂暴之血","神射","致命一击","铁壁","再生之体","法力奔涌"};
  public static readonly string[] StageNames={"入门","精通","化境"};
  public static readonly float[] StageMult={1f,1.5f,2f};
  public static readonly int[] ScoreAt={2,3,4};
  public static readonly float[] FlurryChance={.15f,.22f,.30f},CritChance={.10f,.15f,.20f},CritMult={2f,2.25f,2.5f},RagePain={.5f,.4f,.3f},PowerAt={.06f,.09f,.12f};
  public static readonly int[] RegenDays={15,10,6};
  public const float StarterChance=.35f,BossChance=.5f,AwakenChance=.25f,MilestoneChance=.25f,BackfillChance=.35f,RageHealth=.6f,DamageCutCap=.25f,ArmorCap=.3f;
  public const int MasterLevel=10;
  public static readonly int[] AwakenKills={100,300};
  public static readonly Color Blue=new Color(.357f,.608f,.835f);

  static TraitDef[] all;
  public static TraitDef[] All=>all??(all=new[]{CombatGiftDefOf.LP_CG_Mighty,CombatGiftDefOf.LP_CG_Swift,CombatGiftDefOf.LP_CG_Stalwart,CombatGiftDefOf.LP_CG_Brilliant,CombatGiftDefOf.LP_CG_Eagle,CombatGiftDefOf.LP_CG_Commanding,
   CombatGiftDefOf.LP_CG_Flurry,CombatGiftDefOf.LP_CG_Rage,CombatGiftDefOf.LP_CG_Deadeye,CombatGiftDefOf.LP_CG_Lethal,CombatGiftDefOf.LP_CG_Bulwark,CombatGiftDefOf.LP_CG_Regen,CombatGiftDefOf.LP_CG_Arcane});
  public static int IndexOf(TraitDef d){if(d==null)return -1;var a=All;for(int i=0;i<a.Length;i++)if(a[i]==d)return i;return -1;}
  public static bool Is(TraitDef d)=>IndexOf(d)>=0;

  // ---------- 缓存（属性钩子可能不在主线程：并发字典）。1.66.0 热修：一个人可以有多个，缓存成位掩码（第 g 位 = 有第 g 个天赋） ----------
  static readonly System.Collections.Concurrent.ConcurrentDictionary<int,int> cache=new System.Collections.Concurrent.ConcurrentDictionary<int,int>();
  public static int Mask(Pawn p){
   if(p==null)return 0;
   if(cache.TryGetValue(p.thingIDNumber,out int m))return m;
   m=0;var l=p.story?.traits?.allTraits;if(l!=null)for(int k=0;k<l.Count;k++){int x=IndexOf(l[k].def);if(x>=0)m|=1<<x;}
   cache[p.thingIDNumber]=m;return m;
  }
  public static bool HasGift(Pawn p,int g)=>g>=0 && (Mask(p)&1<<g)!=0;
  public static int CountOf(Pawn p){int m=Mask(p),n=0;while(m!=0){m&=m-1;n++;}return n;}
  public static IEnumerable<int> Gifts(Pawn p){int m=Mask(p);for(int g=0;g<Count;g++)if((m&1<<g)!=0)yield return g;}
  // 第一个（下标最小的）天赋，-1 = 没有。
  public static int Gift(Pawn p){int m=Mask(p);for(int g=0;g<Count;g++)if((m&1<<g)!=0)return g;return -1;}
  public static void Invalidate(Pawn p){if(p!=null)cache.TryRemove(p.thingIDNumber,out _);}
  public static void ClearCache(){cache.Clear();}
  public static Trait GiftTrait(Pawn p){var l=p?.story?.traits?.allTraits;if(l!=null)for(int k=0;k<l.Count;k++)if(Is(l[k].def))return l[k];return null;}
  public static bool Has(Pawn p)=>Mask(p)!=0;
  public static int Max=>Mathf.Clamp(LichMod.Settings?.combatGiftMax??3,1,Count);
  public static bool Full(Pawn p)=>CountOf(p)>=Max;
  public static float ExtraChance=>Mathf.Clamp01((LichMod.Settings?.combatGiftExtra??25)/100f);

  // ---------- 阶段 ----------
  public static int BaseStage(Hediff_Adventurer h)=>h==null?0:h.legendary?2:h.level>=MasterLevel?1:0;
  public static int Stage(Hediff_Adventurer h)=>h==null?0:Mathf.Clamp(BaseStage(h)+h.cgBonus,0,2);
  public static int Stage(Pawn p)=>Stage(Adventurer.Get(p));
  public static float Mult(Pawn p)=>StageMult[Stage(p)];

  // ---------- 属性 ----------
  public static int ScoreBonus(Hediff_Adventurer h,int a){
   if(h==null || a<0 || a>5)return 0;
   return HasGift(h.pawn,a)?ScoreAt[Stage(h)]:0;
  }
  public static string ScoreTag(Hediff_Adventurer h,int a){int b=ScoreBonus(h,a);return b>0?"+"+b:"";}

  // ---------- 数值（冒险者状态的 BuildStage 调用） ----------
  static StatDef S(string n)=>DefDatabase<StatDef>.GetNamedSilentFail(n);
  static void Add(List<StatModifier> list,string stat,float v){var d=S(stat);if(d!=null)list.Add(new StatModifier{stat=d,value=v});}
  public static float DamageCut(float m)=>Mathf.Min(DamageCutCap,.1f*m);
  public static float ArmorBonus(float m)=>Mathf.Min(ArmorCap,.15f*m);
  public static void AddStats(Hediff_Adventurer h,HediffStage st,List<StatModifier> f,List<StatModifier> o){
   if(Mask(h?.pawn)==0)return;int s=Stage(h);float m=StageMult[s];
   foreach(int g in Gifts(h.pawn))switch(g){
    case Mighty:Add(f,"CarryingCapacity",1f+.25f*m);break;
    case Swift:Add(o,"MoveSpeed",.4f*m);break;
    case Stalwart:Add(f,"IncomingDamageFactor",1f-DamageCut(m));break;
    case Brilliant:Add(f,"ResearchSpeed",1f+.15f*m);break;
    case Eagle:Add(f,"AimingDelayFactor",1f-.2f*m);break;
    case Commanding:Add(f,"SocialImpact",1f+.2f*m);break;
    case Flurry:Add(f,"MeleeCooldownFactor",1f-.2f*m);break;
    case Rage:if(h.cgRage){Add(f,"MeleeDamageFactor",1f+.3f*m);Add(f,"MeleeCooldownFactor",1f-.15f*m);st.painFactor*=RagePain[s];}break;
    case Deadeye:Add(f,"ShootingAccuracyPawn",1f+.12f*m);break;
    case Bulwark:Add(o,"ArmorRating_Sharp",ArmorBonus(m));Add(o,"ArmorRating_Blunt",ArmorBonus(m));Add(f,"StaggerDurationFactor",0f);break;
    case Regen:Add(f,"InjuryHealingFactor",1f+1.5f*m);break;
    case Arcane:if(HeroDestiny.VPEActive)Add(o,"PsychicEntropyMax",20f*m);else Add(o,"PsychicSensitivity",.15f*m);break;
   }
  }

  // ---------- 每 250 刻（冒险者状态 TickInterval 调用）：狂暴之血阈值、再生之体 ----------
  public static void Tick250(Hediff_Adventurer h){
   var p=h.pawn;bool rage=HasGift(p,Rage);
   if(!rage && h.cgRage){h.cgRage=false;h.Dirty();}
   if(rage){bool now=RageActive(p);if(now!=h.cgRage){h.cgRage=now;h.Dirty();if(now)Items.Mote(p,"狂暴！",new Color(.9f,.3f,.25f));}}
   if(HasGift(p,Regen))RegenTick(h,GenTicks.TicksGame);
  }
  public static bool RageActive(Pawn p)=>p?.health?.summaryHealth!=null && !p.Dead && p.health.summaryHealth.SummaryHealthPercent<RageHealth;
  public static int RegenInterval(int stage)=>RegenDays[Mathf.Clamp(stage,0,2)]*GenDate.TicksPerDay;
  // 缺失的非要害部位（取最上层的缺失部位，长回时子部位一起回来）。
  public static bool Vital(BodyPartRecord part)=>part==null || part.parent==null || part.def.tags!=null && part.def.tags.Any(t=>t!=null && t.vital);
  public static BodyPartRecord MissingNonVital(Pawn p){
   var l=p?.health?.hediffSet?.GetMissingPartsCommonAncestors();if(l==null)return null;
   for(int k=0;k<l.Count;k++){var part=l[k].Part;if(!Vital(part))return part;}
   return null;
  }
  // 返回长回的部位（null = 没有）。计时从发现缺失开始，满一个周期长回一个。
  public static BodyPartRecord RegenTick(Hediff_Adventurer h,int now){
   var p=h.pawn;var part=MissingNonVital(p);
   if(part==null){h.cgRegenTick=-1;return null;}
   if(h.cgRegenTick<0 || now<h.cgRegenTick){h.cgRegenTick=now;return null;}
   if(now-h.cgRegenTick<RegenInterval(Stage(h)))return null;
   p.health.RestorePart(part);h.cgRegenTick=now;
   Items.Mote(p,"长回了"+part.Label,Blue);
   if(p.Faction!=null && p.Faction.IsPlayer && !TestSuppressed)Messages.Message(p.LabelShort+"的再生之体长回了"+part.Label+"。",p,MessageTypeDefOf.PositiveEvent,false);
   return part;
  }

  // ---------- 伤害（Pawn.PreApplyDamage 前缀） ----------
  public static float? TestRoll;
  static float Roll=>TestRoll??Rand.Value;
  public static bool extra; // 连斩追加的那一击不再触发追加
  public static int Crits;  // 自检计数
  static readonly AccessTools.StructFieldRef<DamageInfo,float> ArmorPen=AccessTools.StructFieldRefAccess<DamageInfo,float>("armorPenetrationInt");
  public static void OnDamage(Pawn v,ref DamageInfo d){
   var def=d.Def;if(def==null || !def.harmsHealth || d.Amount<=0f || v.Dead)return;
   var a=d.Instigator as Pawn;if(a==null || a==v)return;
   int mask=Mask(a);if((mask&(1<<Deadeye|1<<Arcane|1<<Lethal))==0)return;
   int s=Stage(a);float m=StageMult[s],amt=d.Amount;
   foreach(int g in Gifts(a))switch(g){
    case Deadeye:if(!HeroCombat.Melee(d) && d.Weapon!=null && d.Weapon.IsRangedWeapon)amt*=1f+.15f*m;break;
    case Arcane:if(HeroCombat.Spell(d))amt*=1f+.25f*m;break;
    case Lethal:
     if(Roll<CritChance[s]){amt*=CritMult[s];Crits++;Items.Mote(v,"致命一击！",new Color(1f,.35f,.3f));}
     ArmorPen(ref d)=Mathf.Max(0f,d.ArmorPenetrationInt)+.1f*m;
     break;
   }
   if(Math.Abs(amt-d.Amount)>.0001f)d.SetAmount(amt);
  }

  // ---------- 连斩：近战命中后追加一击（下一刻执行） ----------
  public struct Pending{public Pawn from,to;public int tick;}
  public static readonly List<Pending> pending=new List<Pending>();
  public static int Flurries; // 自检计数
  static readonly Func<Verb_MeleeAttack,LocalTargetInfo,DamageWorker.DamageResult> ApplyMelee=(Func<Verb_MeleeAttack,LocalTargetInfo,DamageWorker.DamageResult>)Delegate.CreateDelegate(typeof(Func<Verb_MeleeAttack,LocalTargetInfo,DamageWorker.DamageResult>),AccessTools.Method(typeof(Verb_MeleeAttack),"ApplyMeleeDamageToTarget"));
  public static bool AfterHit(Pawn v,DamageInfo d,DamageWorker.DamageResult r){
   if(extra || v==null || v.Dead || !(d.Instigator is Pawn a) || a==v || r==null || r.totalDamageDealt<=0f)return false;
   if(!HasGift(a,Flurry) || !HeroCombat.Melee(d) || Roll>=FlurryChance[Stage(a)])return false;
   pending.Add(new Pending{from=a,to=v,tick=GenTicks.TicksGame+1});return true;
  }
  public static bool FlurryNow(Pawn from,Pawn to){
   if(from==null || to==null || from.Dead || from.Downed || to.Dead || !from.Spawned || !to.Spawned || from.Map!=to.Map || !from.Position.AdjacentTo8WayOrInside(to.Position))return false;
   if(!(from.meleeVerbs?.TryGetMeleeVerb(to) is Verb_MeleeAttack verb))return false;
   extra=true;
   try{ApplyMelee(verb,new LocalTargetInfo(to));}catch(Exception e){Log.Warning("[DND边缘大陆] 连斩追加失败："+e.Message);return false;}finally{extra=false;}
   Flurries++;Items.Mote(from,"连斩！",Blue);return true;
  }
  public static void RunPending(){
   int now=GenTicks.TicksGame;
   for(int k=pending.Count-1;k>=0;k--){var c=pending[k];if(c.tick>now)continue;pending.RemoveAt(k);FlurryNow(c.from,c.to);}
  }

  // ---------- 设置 ----------
  public static bool TestSuppressed; // 自测进程：除武艺天赋检查组外不随机生成、不发信
  public static bool On=>LichMod.Settings?.combatGifts??true;
  public static bool EnemiesOn=>On && (LichMod.Settings?.combatGiftsEnemies??true);
  public static float Rate=>(LichMod.Settings?.combatGiftRate??8)/100f;

  // ---------- 获得 ----------
  public static bool Eligible(Pawn p)=>p?.story?.traits!=null && p.RaceProps!=null && p.RaceProps.Humanlike && !p.RaceProps.IsMechanoid && !p.Dead && p.ageTracker!=null && p.ageTracker.AgeBiologicalYears>=Adventurer.MinAge && !Adventurer.Excluded(p);
  static bool Hostile(Pawn p){var pl=Faction.OfPlayerSilentFail;return p?.Faction!=null && pl!=null && p.Faction!=pl && p.Faction.HostileTo(pl);}
  static bool Conflicts(Pawn p,TraitDef d){var l=p.story.traits.allTraits;for(int k=0;k<l.Count;k++)if(l[k].def==d || l[k].def.ConflictsWith(d) || d.ConflictsWith(l[k].def))return true;return false;}
  public static TraitDef PickDef(Pawn p){
   var c=All.Where(d=>d!=null && !Conflicts(p,d)).ToList();
   return c.Count==0?null:c.RandomElement();
  }
  // 1.66.0 热修：可以有多个。已有同一个直接返回；满了（设置“每人最多武艺天赋”）先去掉最早的一个。
  public static Trait Give(Pawn p,TraitDef d){
   if(p?.story?.traits==null || d==null)return null;
   var same=p.story.traits.GetTrait(d);if(same!=null)return same;
   for(int guard=0;guard<Count && Full(p);guard++){var old=GiftTrait(p);if(old==null)break;p.story.traits.RemoveTrait(old);Invalidate(p);}
   var t=new Trait(d,0,true);p.story.traits.GainTrait(t,true);
   Invalidate(p);return p.story.traits.allTraits.Contains(t)?t:null;
  }
  // 得到一个之后，每次成功率 ExtraChance 再得一个不同的，直到失败或满 Max 个。返回多得的个数。
  public static int Extra(Pawn p){
   int n=0;
   for(int guard=0;guard<Count && !Full(p) && Rand.Chance(ExtraChance);guard++){if(Give(p,PickDef(p))!=null)n++;else break;}
   return n;
  }
  // 普通生成的获得率：开局殖民者 35%；传奇怪人形 50%；传奇种族是设置值的一半。
  public static float ChanceFor(Pawn p,bool starter){
   if(starter)return StarterChance;
   if(Legendary.Is(p))return BossChance;
   return HeroTraits.TierOf(p)==HeroTraits.Tier.Legendary?Rate*.5f:Rate;
  }
  public static Trait OnGenerated(Pawn p,bool starter,float? roll=null){
   if(TestSuppressed && roll==null || !On || !Eligible(p) || Has(p))return null;
   if(Hostile(p) && !EnemiesOn)return null;
   if((roll??Rand.Value)>=ChanceFor(p,starter))return null;
   var t=Give(p,PickDef(p));if(t!=null && roll==null)Extra(p);
   return t;
  }
  // 剧情首领、宿敌冠军（HeroTraits.EnemyBoss 里调用）。
  public static Trait Boss(Pawn p,float? roll=null){
   if(TestSuppressed && roll==null || !EnemiesOn || !Eligible(p) || Has(p))return null;
   if((roll??Rand.Value)>=BossChance)return null;
   var t=Give(p,PickDef(p));if(t!=null && roll==null)Extra(p);
   return t;
  }
  // 战斗觉醒：武艺天赋没满的殖民者，按成功率再获得一个（1.66.0 热修：已有的也能觉醒新的），发信。
  public static Trait Awaken(Pawn p,float chance,string why,float? roll=null){
   if(TestSuppressed && roll==null || !On || p?.Faction==null || !p.Faction.IsPlayer || !Eligible(p) || Full(p))return null;
   if((roll??Rand.Value)>=chance)return null;
   var t=Give(p,PickDef(p));
   if(t!=null && !TestSuppressed)Find.LetterStack.ReceiveLetter("战斗觉醒："+p.LabelShort,p.LabelShort+why+"，在生死之间领悟了武艺：获得武艺天赋「"+Names[IndexOf(t.def)]+"」。\n\n"+Describe(IndexOf(t.def),Stage(p)),LetterDefOf.PositiveEvent,p);
   return t;
  }
  // 累计击杀人形 100 / 300 时各一次（记在冒险者里程碑里，key 以 kills 开头，不算 DND 里程碑）。
  public static string KillKey(int n)=>"kills_cg"+n;
  public static int CheckKills(Pawn p,float? roll=null){
   if(!On || p?.records==null || p.Faction==null || !p.Faction.IsPlayer)return 0;var h=Adventurer.Get(p);if(h==null)return 0;
   int kills=p.records.GetAsInt(RecordDefOf.KillsHumanlikes),hit=0;
   foreach(int n in AwakenKills){
    if(kills<n || h.milestonesDone.Contains(KillKey(n)))continue;
    h.milestonesDone.Add(KillKey(n));hit=n;
    if(Awaken(p,AwakenChance,"已经击倒了 "+kills+" 个人形敌人",roll)!=null)break;
   }
   return hit;
  }
  // 巨灵许愿：没有就给一个随机的；已有就升一阶（化境为止）；已经化境、天赋没满时再给一个新的。
  static bool CanUpgrade(Pawn p){var h=Adventurer.Get(p);return h!=null && Stage(h)<2;}
  public static string CannotWish(Pawn p){
   if(!Eligible(p))return "无法获得武艺天赋";
   if(Has(p) && !CanUpgrade(p) && Full(p))return Adventurer.Get(p)==null?"没有冒险者等级":"已经化境，武艺天赋也满了";
   return null;
  }
  public static bool Wish(Pawn p){
   if(CannotWish(p)!=null)return false;
   if(!Has(p) || !CanUpgrade(p))return Give(p,PickDef(p))!=null;
   var h=Adventurer.Get(p);h.cgBonus++;h.Dirty();return true;
  }
  public static void OpenWishDialog(Pawn genie,RimWorld.Ability ability){
   var node=new DiaNode("“谁想得到真正的武艺？”");
   foreach(var c in genie.MapHeld.mapPawns.FreeColonistsSpawned.Where(Eligible).ToList()){
    var who=c;string have=string.Join("、",Gifts(c).Select(x=>Names[x]));
    var o=new DiaOption(c.LabelShort+(!Has(c) || !CanUpgrade(c)?"："+(Has(c)?have+"（化境）+ ":"")+"获得一个随机武艺天赋":"："+have+" "+StageNames[Stage(c)]+" → "+StageNames[Math.Min(2,Stage(c)+1)])){resolveTree=true};
    var why=CannotWish(c);if(why!=null)o.Disable(why);
    o.action=()=>{if(!Wish(who))return;Items.Give(genie,"LP_GenieFatigue",Genie.FatigueDays*GenDate.TicksPerDay);Messages.Message(genie.LabelShort+"实现了愿望："+who.LabelShort+"的武艺天赋「"+string.Join("、",Gifts(who).Select(x=>Names[x]))+"」（"+StageNames[Stage(who)]+"）。",who,MessageTypeDefOf.PositiveEvent,false);};
    node.options.Add(o);
   }
   node.options.Add(new DiaOption("再想想"){action=()=>ability?.ResetCooldown(),resolveTree=true});
   Find.WindowStack.Add(new Dialog_NodeTree(node,true,false,"巨灵的愿望"));
  }

  // ---------- 敌人战力：每个武艺天赋 +6% / 9% / 12% ----------
  public static float PowerFactor(Pawn p){int n=CountOf(p);return n==0?1f:1f+PowerAt[Stage(p)]*n;}
  // 袭击组生成后：按多出来的战力从末尾裁掉没有天赋的普通成员（多出来的够半个人才裁）。返回裁掉的人数。
  public static int Trim(List<Pawn> list){
   float extra=0f;foreach(var p in list)extra+=(p.kindDef?.combatPower??0f)*(PowerFactor(p)-1f);
   int removed=0;
   for(int i=list.Count-1;i>=0 && list.Count>1;i--){
    var p=list[i];float cp=p.kindDef?.combatPower??0f;
    if(cp<=0f || extra<cp*.5f || Has(p) || p.kindDef.trader || Necro.Construct(p)!=null)continue;
    extra-=cp;list.RemoveAt(i);removed++;
    if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);if(!p.Destroyed)p.Destroy();
   }
   return removed;
  }

  // ---------- 老存档：读档时对现有殖民者补掷一次 ----------
  public static List<Pawn> Backfill(IEnumerable<Pawn> colonists,Func<float> roll=null){
   var got=new List<Pawn>();if(!On)return got;
   foreach(var p in colonists.ToList()){
    if(!Eligible(p) || Has(p))continue;
    if((roll?.Invoke()??Rand.Value)>=BackfillChance)continue;
    if(Give(p,PickDef(p))!=null){got.Add(p);if(roll==null)Extra(p);}
   }
   if(got.Count>0 && !TestSuppressed)Find.LetterStack.ReceiveLetter("武艺天赋","这片大陆上的战士们开始领悟真正的武艺（1.66.0）。你的殖民者里有人觉醒了：\n\n"+string.Join("\n",got.Select(p=>"· "+p.LabelShort+"：「"+string.Join("、",Gifts(p).Select(x=>Names[x]))+"」"))+"\n\n武艺天赋随冒险者成长：10 级精通（数值 ×1.5），成为传奇冒险者后化境（×2）。",LetterDefOf.PositiveEvent,new LookTargets(got));
   return got;
  }

  // ---------- 说明 ----------
  static string P(float v)=>"+"+(v*100f).ToString("0.#")+"%";
  static string M(float v)=>"-"+(v*100f).ToString("0.#")+"%";
  public static string Describe(int g,int s){
   float m=StageMult[Mathf.Clamp(s,0,2)];int b=ScoreAt[Mathf.Clamp(s,0,2)];
   switch(g){
    case Mighty:return "力量 +"+b+"（上限 +"+b+"）；负重 "+P(.25f*m);
    case Swift:return "敏捷 +"+b+"（上限 +"+b+"）；移动速度 +"+(.4f*m).ToString("0.##");
    case Stalwart:return "体质 +"+b+"（上限 +"+b+"）；受到的伤害 "+M(DamageCut(m));
    case Brilliant:return "智力 +"+b+"（上限 +"+b+"）；研究速度 "+P(.15f*m);
    case Eagle:return "感知 +"+b+"（上限 +"+b+"）；远程瞄准时间 "+M(.2f*m);
    case Commanding:return "魅力 +"+b+"（上限 +"+b+"）；社交影响 "+P(.2f*m);
    case Flurry:return "近战冷却 "+M(.2f*m)+"；近战命中后成功率 "+(FlurryChance[s]*100f).ToString("0")+"% 立刻追加一击";
    case Rage:return "健康低于 60% 时：近战伤害 "+P(.3f*m)+"、近战冷却 "+M(.15f*m)+"、疼痛 ×"+RagePain[s].ToString("0.#");
    case Deadeye:return "远程命中 "+P(.12f*m)+"；远程伤害 "+P(.15f*m);
    case Lethal:return "任何攻击成功率 "+(CritChance[s]*100f).ToString("0")+"% 暴击，伤害 ×"+CritMult[s].ToString("0.##")+"；护甲穿透 "+P(.1f*m);
    case Bulwark:return "利器 / 钝器护甲 "+P(ArmorBonus(m))+"（绝对值）；不会踉跄";
    case Regen:return "伤口愈合 ×"+(1f+1.5f*m).ToString("0.##")+"；每 "+RegenDays[s]+" 天长回一个缺失的非要害部位";
    case Arcane:return "法杖 / 灵能 / 神术伤害 "+P(.25f*m)+"；"+(HeroDestiny.VPEActive?"心灵熵上限 +"+(20f*m).ToString("0"):"心灵敏感度 "+P(.15f*m));
   }
   return "";
  }
  public static string ExtraTip(Pawn p,Trait t){
   int g=IndexOf(t?.def);if(g<0)return null;var h=Adventurer.Get(p);int s=Stage(h);var sb=new StringBuilder();
   sb.AppendLine("当前阶段："+StageNames[s]+(h!=null && h.cgBonus>0?"（巨灵许愿 +"+h.cgBonus+" 阶）":""));
   sb.AppendLine("现在："+Describe(g,s));
   sb.AppendLine();
   sb.AppendLine("入门（获得时）："+Describe(g,0));
   sb.AppendLine("精通（冒险者 "+MasterLevel+" 级，数值 ×1.5）："+Describe(g,1));
   sb.Append("化境（传奇冒险者，数值 ×2）："+Describe(g,2));
   if(g==Rage && h!=null)sb.Append("\n\n"+(h.cgRage?"狂暴中":"未触发"));
   if(g==Regen && h!=null && h.cgRegenTick>=0)sb.Append("\n\n正在长回缺失部位："+((h.cgRegenTick+RegenInterval(s)-GenTicks.TicksGame)/(float)GenDate.TicksPerDay).ToString("0.#")+" 天后");
   if(h==null)sb.Append("\n\n（没有冒险者等级：属性加值和附带效果不生效，战技的命中效果照常）");
   return sb.ToString();
  }
 }

 // ---------- 存档：老存档补掷标记；连斩追加的执行 ----------
 public class GameComponent_CombatGifts : GameComponent {
  public bool backfilled;
  public GameComponent_CombatGifts(Game game){}
  public static GameComponent_CombatGifts Get=>GameComp<GameComponent_CombatGifts>.Get();
  public override void FinalizeInit(){base.FinalizeInit();CombatGifts.ClearCache();CombatGifts.pending.Clear();}
  public override void StartedNewGame(){base.StartedNewGame();backfilled=true;}
  public override void LoadedGame(){
   base.LoadedGame();if(backfilled)return;backfilled=true;
   if(CombatGifts.TestSuppressed)return;
   try{CombatGifts.Backfill(PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists);}catch(Exception e){Log.Error("[DND边缘大陆] 武艺天赋补掷出错："+e);}
  }
  public override void GameComponentTick(){if(CombatGifts.pending.Count>0)CombatGifts.RunPending();}
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref backfilled,"lpCgBackfilled");}
 }

 [StaticConstructorOnStartup]
 public static class CombatGiftsStartup {static CombatGiftsStartup(){if(GenCommandLine.CommandLineArgPassed("lichtest"))CombatGifts.TestSuppressed=true;}}

 // ==================== Harmony ====================
 [HarmonyPatch(typeof(PawnGenerator),nameof(PawnGenerator.GeneratePawn),new[]{typeof(PawnGenerationRequest)})]
 public static class CombatGiftGenerate {static void Postfix(Pawn __result,PawnGenerationRequest request){if(__result!=null && !__result.Dead && __result.RaceProps?.Humanlike==true)CombatGifts.OnGenerated(__result,request.Context==PawnGenerationContext.PlayerStarter);}}
 [HarmonyPatch(typeof(Pawn_AgeTracker),"BirthdayBiological")]
 public static class CombatGiftBirthday {static void Postfix(Pawn ___pawn,int birthdayAge){if(birthdayAge==Adventurer.MinAge && ___pawn!=null && !___pawn.Dead)CombatGifts.OnGenerated(___pawn,false);}}
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.GainTrait))]
 public static class CombatGiftGain {static void Postfix(Pawn ___pawn,Trait trait){if(trait==null || !CombatGifts.Is(trait.def))return;CombatGifts.Invalidate(___pawn);var h=Adventurer.Get(___pawn);if(h!=null){h.cgRage=false;h.Dirty();}}}
 [HarmonyPatch(typeof(TraitSet),nameof(TraitSet.RemoveTrait))]
 public static class CombatGiftLose {static void Postfix(Pawn ___pawn,Trait trait){if(trait==null || !CombatGifts.Is(trait.def))return;CombatGifts.Invalidate(___pawn);var h=Adventurer.Get(___pawn);if(h!=null){h.cgRage=false;h.cgBonus=0;h.Dirty();}}}
 [HarmonyPatch(typeof(Trait),nameof(Trait.TipString))]
 public static class CombatGiftTip {static void Postfix(Trait __instance,Pawn pawn,ref string __result){if(__instance==null || !CombatGifts.Is(__instance.def))return;var x=CombatGifts.ExtraTip(pawn,__instance);if(!x.NullOrEmpty())__result=__result+"\n\n"+x;}}
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class CombatGiftDamage {static void Prefix(Pawn __instance,ref DamageInfo dinfo){CombatGifts.OnDamage(__instance,ref dinfo);}}
 [HarmonyPatch(typeof(Thing),nameof(Thing.TakeDamage))]
 public static class CombatGiftAfterHit {static void Postfix(Thing __instance,DamageInfo dinfo,DamageWorker.DamageResult __result){if(__instance is Pawn v && CombatGifts.pending.Count<64)CombatGifts.AfterHit(v,dinfo,__result);}}
 [HarmonyPatch(typeof(RecordsUtility),nameof(RecordsUtility.Notify_PawnKilled))]
 public static class CombatGiftKill {static void Postfix(Pawn killed,Pawn killer){if(killer!=null && killed?.RaceProps?.Humanlike==true && killer.Faction!=null && killer.Faction.IsPlayer)CombatGifts.CheckKills(killer);}}
 [HarmonyPatch(typeof(PawnGroupMakerUtility),nameof(PawnGroupMakerUtility.GeneratePawns))]
 public static class CombatGiftGroupHook {
  [HarmonyPriority(Priority.VeryLow)]
  static void Postfix(PawnGroupMakerParms parms,ref IEnumerable<Pawn> __result){
   if(__result==null || parms?.faction==null || parms.groupKind!=PawnGroupKindDefOf.Combat || !CombatGifts.EnemiesOn)return;
   var pl=Faction.OfPlayerSilentFail;if(pl==null || !parms.faction.HostileTo(pl))return;
   var list=__result.ToList();CombatGifts.Trim(list);__result=list;
  }
 }

 // ---------- 设置（左栏“DND 特性”分组，诅咒之力下面；不另开分组） ----------
 public partial class Settings {
  public bool combatGifts=true,combatGiftsEnemies=true;public int combatGiftRate=8,combatGiftMax=3,combatGiftExtra=25;
  void ExposeCombatGifts(){
   Scribe_Values.Look(ref combatGifts,"lpCombatGifts",true);Scribe_Values.Look(ref combatGiftsEnemies,"lpCombatGiftsEnemies",true);Scribe_Values.Look(ref combatGiftRate,"lpCombatGiftRate",8);Scribe_Values.Look(ref combatGiftMax,"lpCombatGiftMax",3);Scribe_Values.Look(ref combatGiftExtra,"lpCombatGiftExtra",25); // 1.66.0 热修
   combatGiftRate=Math.Max(0,Math.Min(30,combatGiftRate));combatGiftMax=Math.Max(1,Math.Min(13,combatGiftMax));combatGiftExtra=Math.Max(0,Math.Min(100,combatGiftExtra));
  }
 }
 public static class CombatGiftSettings {
  public static void Draw(Listing_Standard l){
   var s=LichMod.Settings;
   l.CheckboxLabeled("武艺天赋",ref s.combatGifts,"（1.66.0）稀有（蓝色）特性，和强力特性分开计数、可以同时拥有；得到一个之后，每次按“额外武艺天赋”的成功率再得一个不同的，直到失败或达到上限。六个属性类（巨力、迅捷、坚韧之体、博闻强识、鹰眼、王者气度：对应属性 +2，上限同样 +2，再加一项附带效果）和七个战技类（连斩、狂暴之血、神射、致命一击、铁壁、再生之体、法力奔涌）。\n随冒险者成长：10 级精通（数值 ×1.5，属性 +3），传奇冒险者化境（×2，属性 +4）。\n开局殖民者 35%；首领、冠军、传奇怪 50%；殖民者累计击杀 100 / 300 个人形时、完成 DND 里程碑时各有成功率 25% 战斗觉醒（已有的也能再觉醒一个，直到上限）；巨灵“让我更强 · 武艺”。老存档读档时给现有殖民者补一次（35%）。\n关闭后不再生成，已有的保留。");
   l.CheckboxLabeled("敌人也会拥有武艺天赋",ref s.combatGiftsEnemies,"关闭后敌对派系的人不再随机带武艺天赋。敌人每个武艺天赋战力 +6%（精通 +9%，化境 +12%），袭击会相应少来一些人。");
   l.Label("普通人形获得武艺天赋："+s.combatGiftRate+"%（传奇种族减半）");s.combatGiftRate=Mathf.RoundToInt(l.Slider(s.combatGiftRate,0,30));
   l.Label("每人最多武艺天赋："+s.combatGiftMax+" 个");s.combatGiftMax=Mathf.RoundToInt(l.Slider(s.combatGiftMax,1,13));
   l.Label("额外武艺天赋成功率："+s.combatGiftExtra+"%（得到一个之后，每次再得一个的几率）");s.combatGiftExtra=Mathf.RoundToInt(l.Slider(s.combatGiftExtra,0,100)/5f)*5;
  }
 }
}
