using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.185.0 第七批子职业 10 个（Defs/Subclasses7.xml，由 ArtSource/Subclasses7/gen_subclasses7.py 生成）：把子职业不到 5 个的职业都补到 5 个。
 // 武僧 醉拳宗 / 剑圣宗；野蛮人 狂热者之道 / 先祖守卫之道；游侠 妖精漫游者 / 地平线行者；德鲁伊 野火结社；吟游诗人 迷惑学院；枪手 赏金猎人；心灵术士 预知者。
 // 和泰坦战士一样不改共享的分派表：Tick250、能力检查、能力效果都用 Harmony 挂在 ClassSystem.Tick250 / ClassEffects.Invalid / ClassEffects.Apply 上（S7_ 开头的效果名）；
 // 只有威力系数是 ClassEffects.Power 里的一项（那个方法可能被内联，补丁不保险）。所有补丁第一步都是 Users 空集合判断，没人学这些子职业时零开销。
 public static class Subclass7 {
  public const string Drunken="LP_Sub_DrunkenWay",Kensei="LP_Sub_KenseiWay",Zealot="LP_Sub_Zealot",Ancestral="LP_Sub_Ancestral",FeyWanderer="LP_Sub_FeyWanderer",Horizon="LP_Sub_HorizonWalker",
   Wildfire="LP_Sub_CircleWildfire",Glamour="LP_Sub_Glamour",Bounty="LP_Sub_BountyHunter",Seer="LP_Sub_PsiSeer";
  public static readonly string[] Names={Drunken,Kensei,Zealot,Ancestral,FeyWanderer,Horizon,Wildfire,Glamour,Bounty,Seer};
  public const float RedirectChance=.25f,BladeFactor=1.1f,BladePen=.2f,UnerringChance=.15f,UnerringFactor=2f,FuryFactor=1.15f,BeyondDeath=.85f,HauntedFactor=.75f,ShieldFactor=.85f,ShieldRange=6f,
   DreadfulFactor=1.1f,MomentumFactor=1.2f,SpectralFactor=.5f,BondFactor=1.1f,CauterizeBase=6f,CauterizeRange=10f,RebirthHeal=.5f,MantleBase=6f,MarkFactor=1.2f,MarkFactorHigh=1.4f,PaydayHeal=10f;
  public const int RedirectInterval=180,RedirectStun=60,HauntTicks=360,PlanarInterval=300,SpectralInterval=300,CauterizeInterval=600,MajestyStun=240,GlamourStun=120,MarkTicksHigh=7200,PrecogInterval=600,FrenzyTargets=5;
  // 被动对应的永久状态：有能力就挂、失去就摘（Tick250）。剑圣之道另外要求拿着近战武器。
  static readonly (string ability,string hediff)[] Permanent={("LP_DK_Sway","LP_DK_SwayBuff"),("LP_DK_Numb","LP_DK_NumbBuff"),("LP_KS_Path","LP_KS_PathBuff"),("LP_ZL_Focus","LP_ZL_FocusBuff"),("LP_AG_Consult","LP_AG_ConsultBuff"),
   ("LP_WN_Beguiling","LP_WN_BeguilingBuff"),("LP_GL_Enthrall","LP_GL_EnthrallBuff"),("LP_BH_Tracker","LP_BH_TrackerBuff"),("LP_SR_Foresight","LP_SR_ForesightBuff"),("LP_SR_ThirdEye","LP_SR_EyeBuff")};
  // 预知者的常备法术（本来就在心灵术士法术表里，子职业只是让它常备，不占名额）。
  static readonly (string def,int level)[] SeerSpells={("LP_ShieldSpell",3),("LP_WS_MindSpike",3),("LP_MirrorImage",3),("LP_WS_Confusion",7),("LP_WS_Scrying",9)};
  static readonly Dictionary<string,int> PsiCosts=new Dictionary<string,int>{{"S7_SRWarn",1},{"S7_SRExpose",1},{"S7_SRRewind",4}};
  public static readonly Color Wine=new Color(.8f,.6f,.35f),Steel=new Color(.7f,.75f,.85f),Zeal=new Color(.95f,.8f,.45f),Spirit=new Color(.6f,.75f,.85f),Fey=new Color(.75f,.6f,.9f),Planar=new Color(.55f,.7f,.95f),
   Ember=new Color(.95f,.55f,.3f),Charm=new Color(.9f,.6f,.8f),Coin=new Color(.75f,.65f,.4f),Sight=new Color(.7f,.8f,.95f);
  static readonly DefRef<LP_ClassDef> druid=new DefRef<LP_ClassDef>("LP_Class_Druid"),bard=new DefRef<LP_ClassDef>("LP_Class_Bard"),gunslinger=new DefRef<LP_ClassDef>("LP_Class_Gunslinger"),psion=new DefRef<LP_ClassDef>("LP_Class_Psion");
  public static readonly HashSet<Pawn> Users=new HashSet<Pawn>();
  public static void Reset(){Users.Clear();Busy=false;TestRoll=null;}
  static int Now=>Find.TickManager.TicksGame;
  public static float? TestRoll;
  static float Roll=>TestRoll??Rand.Value;
  public static bool Busy;
  static bool Hostile(Pawn a,Pawn t)=>AnyTarget.Hostile(a,t);
  static bool Friendly(Pawn a,Pawn t)=>AnyTarget.Friendly(a,t);
  public static bool Knows(Pawn p,string ability)=>Users.Count>0 && p!=null && Users.Contains(p) && ClassSystem.Knows(p,ability);
  static int Level(Pawn p,DefRef<LP_ClassDef> c)=>c.Def==null?0:ClassSystem.Level(p,c.Def);
  static DamageDef Force=>DefCache<DamageDef>.Get("LP_ArcaneForce")??DamageDefOf.Blunt;
  static CompProperties_ClassEffect Fx(string ability)=>DefCache<AbilityDef>.Get(ability)?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();
  // 间隔：上次触发之后过了 interval 刻才算数（读档后时间倒退也照常）。
  static bool Due(SubclassData sd,string key,int interval){if(sd==null)return false;sd.ticks.TryGetValue(key,out int last);if(last>0 && Now-last<interval && Now>=last)return false;sd.ticks[key]=Math.Max(1,Now);return true;}
  public static bool Armed(Pawn p)=>p?.equipment?.Primary?.def?.IsMeleeWeapon==true;

  // ---------- 被动（每 250 刻） ----------
  public static void Tick250(Hediff_Adventurer h){
   var p=h?.pawn;if(p==null || p.Dead)return;
   bool any=false;for(int i=0;i<Names.Length;i++)if(ClassSubclass.Has(h,Names[i])){any=true;break;}
   if(!any){if(Users.Remove(p))foreach(var (a,hd) in Permanent)Items.Remove(p,hd);return;}
   Users.Add(p);
   foreach(var (a,hd) in Permanent){
    bool k=ClassSystem.Knows(p,a) && (a!="LP_KS_Path" || Armed(p));
    if(k && !Items.Has(p,hd))Items.Give(p,hd);else if(!k && Items.Has(p,hd))Items.Remove(p,hd);
   }
  }
  // ---------- 威力：野火之灵 ----------
  public static float PowerFactor(Pawn p,CompProperties_ClassEffect e){
   if(Users.Count==0 || e==null || p==null || !Users.Contains(p))return 1f;
   return e.stat!=null && e.stat.defName=="LP_VerdantPower" && ClassSystem.Knows(p,"LP_WF_Bond")?BondFactor:1f;
  }
  // ---------- 灵能点、常备法术（预知者） ----------
  public static string CannotCast(Pawn p,CompProperties_ClassEffect e){
   if(e?.effect==null || !PsiCosts.TryGetValue(e.effect,out int c))return null;
   return Psion.CanSpend(p,c)?null:"灵能点不足（".Loc()+Psion.Points(p)+" / "+c+"）".Loc();
  }
  public static bool SeerSpell(Hediff_Adventurer h,CasterListDef l,AbilityDef d){
   if(h==null || d==null || l?.classDef==null || l.classDef!=psion.Def || !ClassSubclass.Has(h,Seer))return false;
   int lv=ClassSystem.Level(h,psion.Def);
   foreach(var (def,level) in SeerSpells)if(def==d.defName && lv>=level)return true;
   return false;
  }
  // ---------- 选目标 ----------
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;
   switch(e.effect){
    case "S7_WFSeed":case "S7_GLMajesty":case "S7_BHMark":case "S7_BHBola":case "S7_SRExpose":
     if(t==null || t.Dead)return "需要选择一个活着的目标。".Loc();if(!AnyTarget.Hostile(caster,t) && !t.HostileTo(caster))return "只能对敌人使用。".Loc();return null;
    case "S7_SRWarn":
     if(t==null || t.Dead)return "需要选择一个活着的目标。".Loc();if(t!=caster && !Friendly(caster,t))return "只能对友军使用。".Loc();return null;
    case "S7_Blink":return LandOk(caster,target.Cell)?null:"那里站不了人。".Loc();
   }
   return null;
  }
  static bool LandOk(Pawn p,IntVec3 c){var map=p?.Map;return map!=null && c.IsValid && c.InBounds(map) && c.Standable(map) && !c.Fogged(map) && c.GetFirstPawn(map)==null;}
  // ---------- 施放 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;int dur=ClassEffects.Duration(p,e);float pw=ClassEffects.Power(p,e);float rad=ClassEffects.Radius(p,e);
   if(PsiCosts.TryGetValue(e.effect,out int pts) && !Psion.Spend(p,pts))return true;
   switch(e.effect){
    case "S7_Buff":Items.Give(p,e.hediff,dur,1f);Items.Mote(p,def.LabelCap,Color.white);return true;
    case "S7_AllyBuff":AllyBuff(p,e.hediff,rad,dur);Items.Mote(p,def.LabelCap,Zeal);return true;
    case "S7_DKFrenzy":Frenzy(p,rad);return true;
    case "S7_WNGlamour":Mist(p,rad,dur);return true;
    case "S7_Blink":
     if(!LandOk(p,target.Cell))return true;
     {var map=p.Map;var from=p.DrawPos;FleckMaker.ThrowDustPuffThick(from,map,1.2f,Planar);Races124.Blink(p,target.Cell);FleckMaker.ThrowDustPuffThick(p.DrawPos,map,1.2f,Planar);}
     if(e.hediff!=null)Items.Give(p,e.hediff,dur,1f);
     Items.Mote(p,def.LabelCap,Planar);return true;
    case "S7_WFSeed":
     if(t!=null && Hostile(p,t)){t.TakeDamage(new DamageInfo(DamageDefOf.Flame,pw,0f,-1f,p));if(!t.Dead && t.Spawned)t.TryAttachFire(.4f,p);Items.Mote(t,"炽焰之种".Loc(),Ember);}
     return true;
    case "S7_GLMantle":Mantle(p,rad,dur,ClassEffects.Strength(p,e));return true;
    case "S7_GLMajesty":if(t!=null && Hostile(p,t))Majesty(p,t);return true;
    case "S7_BHMark":
     if(t!=null && Hostile(p,t))ClassMartial2.MarkTarget(p,t,"LP_BH_Marked",ClassSystem.Knows(p,"LP_BH_DeadOrAlive")?MarkTicksHigh:dur,1f,Coin,"悬赏标记".Loc());
     return true;
    case "S7_BHBola":if(t!=null && Hostile(p,t)){Items.Give(t,"LP_BH_Hobbled",dur,1f);Items.Mote(t,"缚足".Loc(),Coin);}return true;
    case "S7_SRWarn":{var who=t??p;if(who!=p && !Friendly(p,who))return true;Items.Give(who,"LP_SR_Warned",dur,1f);Items.Mote(who,"危机预感".Loc(),Sight);return true;}
    case "S7_SRExpose":if(t!=null && Hostile(p,t)){Items.Give(t,"LP_SR_Exposed",dur,1f);Items.Mote(t,"破绽尽现".Loc(),Sight);}return true;
    case "S7_SRRewind":Rewind(p,rad,pw);return true;
   }
   return false;
  }
  public static int AllyBuff(Pawn p,string hediff,float radius,int ticks){
   int n=1;Items.Give(p,hediff,ticks,1f);
   if(p.Map!=null)foreach(var a in Races124.PawnsNear(p.Map,p.Position,radius).ToList()){if(a==p || a.Dead || !Friendly(p,a))continue;Items.Give(a,hediff,ticks,1f);n++;}
   return n;
  }
  // 醉拳乱舞：相邻的敌人（最多 5 个）每人两次完整的近战结算。返回打了几个人。
  public static int Frenzy(Pawn p,float radius){
   if(p.Map==null)return 0;int n=0;
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,radius).Where(x=>Hostile(p,x) && !x.Downed && x.Position.AdjacentTo8WayOrInside(p.Position)).Take(FrenzyTargets).ToList()){
    for(int i=0;i<2 && !t.Dead && t.Spawned;i++)ClassMelee.Strike(p,t);n++;
   }
   Items.Mote(p,"醉拳乱舞".Loc(),Wine);return n;
  }
  // 惑心之雾：震慑；没抵抗住的眩晕 2 秒。返回波及的敌人数。
  public static int Mist(Pawn p,float radius,int ticks){
   int n=0;if(p.Map==null)return 0;
   foreach(var t in Races124.PawnsNear(p.Map,p.Position,radius).ToList()){if(t==p || t.Dead || !Hostile(p,t))continue;Items.Give(t,"LP_Shaken",ticks,1f);if(!DNDMind.TryResist(t))t.stances?.stunner?.StunFor(GlamourStun,p);n++;}
   FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,radius,Fey);Items.Mote(p,"惑心之雾".Loc(),Fey);return n;
  }
  public static float MantleAmount(Pawn p,float strength)=>(MantleBase+Level(p,bard))*Mathf.Max(.1f,strength);
  public static int Mantle(Pawn p,float radius,int ticks,float strength){
   float amount=MantleAmount(p,strength);int n=1;LakeArcana.Touched=true;
   Items.Give(p,"LP_ACB_Shield",ticks,amount);
   if(p.Map!=null)foreach(var a in Races124.PawnsNear(p.Map,p.Position,radius).ToList()){if(a==p || a.Dead || !Friendly(p,a))continue;Items.Give(a,"LP_ACB_Shield",ticks,amount);n++;}
   Items.Mote(p,"激励披风 ".Loc()+amount.ToString("0"),Charm);return n;
  }
  // 威仪披风：没抵抗住就眩晕 4 秒；抵抗住也被震慑。返回有没有跪下。
  public static bool Majesty(Pawn p,Pawn t){
   Items.Give(t,"LP_Shaken",600,1f);bool kneel=!DNDMind.TryResist(t);
   if(kneel)t.stances?.stunner?.StunFor(MajestyStun,p);
   Items.Mote(t,kneel?"跪下！".Loc():"威仪披风".Loc(),Charm);return kneel;
  }
  public static int Rewind(Pawn p,float radius,float amount){
   int n=0;var who=new List<Pawn>{p};
   if(p.Map!=null)who.AddRange(Races124.PawnsNear(p.Map,p.Position,radius).Where(a=>a!=p && !a.Dead && Friendly(p,a)));
   foreach(var a in who){ClassMelee.Heal(a,amount);a.stances?.stunner?.StopStun();n++;}
   Items.Mote(p,"命运改写".Loc(),Sight);return n;
  }

  // ---------- 伤害（攻击者一侧）：返回伤害倍率；穿透写回 dinfo ----------
  public static float Outgoing(Pawn a,Pawn v,ref DamageInfo d){
   float f=1f;bool weapon=Subclass6.Weapon(d),hostile=v.HostileTo(a);
   if(weapon && Subclass6.MeleeHit(d) && d.Weapon.IsMeleeWeapon){
    if(ClassSystem.Knows(a,"LP_KS_OneWithBlade")){f*=BladeFactor;Subclass6.SetPen(ref d,d.ArmorPenetrationInt+BladePen);}
    if(ClassSystem.Knows(a,"LP_KS_Unerring") && Roll<UnerringChance){f*=UnerringFactor;Items.Mote(a,"无误精准！".Loc(),Steel);}
   }
   if(hostile && ClassMartial2.Melee(d) && ClassMartial2.Raging(a)){
    if(ClassSystem.Knows(a,"LP_ZL_DivineFury"))f*=FuryFactor;
    if(ClassSystem.Knows(a,"LP_AG_Protectors"))ClassMartial2.MarkTarget(a,v,"LP_AG_Haunted",HauntTicks,1f,Spirit,"先祖纠缠".Loc());
   }
   if(weapon && hostile){
    if(ClassSystem.Knows(a,"LP_WN_Dreadful"))f*=DreadfulFactor;
    if(Items.Has(a,"LP_HW_Momentum"))f*=MomentumFactor;
    if(ClassSystem.Knows(a,"LP_HW_PlanarWarrior") && Due(ClassSubclass.Data(a),"hwT",PlanarInterval)){
     var e=Fx("LP_HW_PlanarWarrior");float amt=e==null?8f:ClassEffects.Power(a,e);var tv=v;var me=a;
     Fiends.Defer(()=>{if(tv.Dead || !tv.Spawned)return;Busy=true;try{tv.TakeDamage(new DamageInfo(Force,amt,.3f,-1f,me));}finally{Busy=false;}});
     Items.Mote(v,"位面战士".Loc(),Planar);
    }
   }
   if(v.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_BH_Marked")) is Hediff_ClassMark bm && bm.caster==a)f*=ClassSystem.Knows(a,"LP_BH_DeadOrAlive")?MarkFactorHigh:MarkFactor;
   return f;
  }
  // 受害者一侧。dodge=true：这一下完全落空。
  public static float Incoming(Pawn v,Pawn a,DamageInfo d,out bool dodge){
   dodge=false;float f=1f;bool foe=a!=null && a!=v && a.HostileTo(v);
   // 先祖纠缠：打标记者以外的人 ×0.75
   if(a!=null && a!=v && a.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_AG_Haunted")) is Hediff_ClassMark hm && hm.caster!=null && hm.caster!=v)f*=HauntedFactor;
   if(Users.Contains(v)){
    var sd=ClassSubclass.Data(v);
    if(foe && ClassMartial2.Melee(d) && ClassSystem.Knows(v,"LP_DK_Redirect") && !v.Downed && Roll<RedirectChance && Due(sd,"dkT",RedirectInterval)){
     dodge=true;var at=a;var me=v;Fiends.Defer(()=>{if(!at.Dead && at.Spawned)at.stances?.stunner?.StunFor(RedirectStun,me);});Items.Mote(v,"借力打力".Loc(),Wine);return 0f;
    }
    if(foe && ClassSystem.Knows(v,"LP_SR_Precog") && !v.Downed && Due(sd,"srP",PrecogInterval)){dodge=true;Items.Mote(v,"先知先觉".Loc(),Sight);return 0f;}
    if(ClassSystem.Knows(v,"LP_ZL_BeyondDeath") && ClassMartial2.Raging(v))f*=BeyondDeath;
    if(ClassSystem.Knows(v,"LP_HW_Spectral") && Due(sd,"hwS",SpectralInterval)){f*=SpectralFactor;Items.Mote(v,"幽灵防御".Loc(),Planar);}
   }
   // 灵魂护盾：6 格内狂暴中的先祖守卫替友军挡 15%；复仇先祖把挡下的量还给攻击者。只算一个守卫。
   var map=v.MapHeld;
   if(map!=null && v.Spawned)foreach(var k in Users){
    if(k==v || k==a || k.Dead || !k.Spawned || k.Map!=map || k.HostileTo(v) || k.Faction!=v.Faction || !ClassSystem.Knows(k,"LP_AG_SpiritShield") || !ClassMartial2.Raging(k) || k.Position.DistanceTo(v.Position)>ShieldRange)continue;
    f*=ShieldFactor;
    if(foe && ClassSystem.Knows(k,"LP_AG_Vengeful")){
     float back=d.Amount*(1f-ShieldFactor);var at=a;var me=k;
     if(back>=.5f)Fiends.Defer(()=>{if(at.Dead || !at.Spawned)return;Busy=true;try{at.TakeDamage(new DamageInfo(Force,back,.3f,-1f,me));}finally{Busy=false;}});
    }
    break;
   }
   return f;
  }
  // ---------- 杀敌：治愈余烬、赏金到手 ----------
  public static void Killed(Pawn victim){
   if(Users.Count==0 || victim==null)return;var map=victim.MapHeld;var at=victim.PositionHeld;
   if(victim.health?.hediffSet?.GetFirstHediffOfDef(Items.Hediff("LP_BH_Marked")) is Hediff_ClassMark bm && bm.caster!=null && !bm.caster.Dead && Users.Contains(bm.caster) && ClassSystem.Knows(bm.caster,"LP_BH_Payday")){
    var c=bm.caster;var h=Adventurer.Get(c);if(h!=null && gunslinger.Def!=null)ClassSystem.AddResource(h,gunslinger.Def,1f);
    ClassMelee.Heal(c,PaydayHeal);Items.Mote(c,"赏金到手".Loc(),Coin);
   }
   if(map==null)return;
   foreach(var k in Users.ToList()){
    if(k==null || k.Dead || !k.Spawned || k.Map!=map || !victim.HostileTo(k) || !ClassSystem.Knows(k,"LP_WF_Cauterize") || k.Position.DistanceTo(at)>CauterizeRange)continue;
    if(!Due(ClassSubclass.Data(k),"wfC",CauterizeInterval))continue;
    float amt=CauterizeBase+Level(k,druid);ClassMelee.Heal(k,amt);
    var worst=Races124.PawnsNear(map,k.Position,CauterizeRange).Where(a=>a!=k && !a.Dead && !a.HostileTo(k) && a.Faction==k.Faction).OrderByDescending(a=>ClassMelee.InjurySum(a)).FirstOrDefault();
    if(worst!=null && ClassMelee.InjurySum(worst)>0f)ClassMelee.Heal(worst,amt);
    Items.Mote(k,"治愈余烬".Loc(),Ember);
   }
  }
  // ---------- 浴火重生：快倒下时治好一半伤势并止血，每天一次 ----------
  public static bool TryStand(Pawn p){
   if(p==null || p.Dead || Users.Count==0 || !Users.Contains(p) || !ClassSystem.Knows(p,"LP_WF_Rebirth"))return false;
   var sd=ClassSubclass.Data(p);if(sd==null)return false;
   int today=GenDate.DaysPassed+1;sd.ticks.TryGetValue("wfDay",out int last);if(last==today)return false; // 存“日子 + 1”：第 0 天的 0 不会被当成已经用过
   sd.ticks["wfDay"]=today;
   ClassMelee.Heal(p,ClassMelee.InjurySum(p)*RebirthHeal);Items.Give(p,"LP_StaunchBleeding",2500);
   if(p.Spawned)FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,1.5f,Ember);
   Items.Mote(p,"浴火重生".Loc(),Ember);return true;
  }
  public static readonly HashSet<string> NoAi=new HashSet<string>{"S7_Blink"};
 }

 // ==================== 挂钩 ====================
 [HarmonyPatch(typeof(ClassSystem),nameof(ClassSystem.Tick250))]
 public static class Subclass7Tick {static void Postfix(Hediff_Adventurer h)=>Subclass7.Tick250(h);}
 [HarmonyPatch(typeof(ClassEffects),nameof(ClassEffects.Invalid))]
 public static class Subclass7Invalid {static void Postfix(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target,ref string __result){if(__result==null && e?.effect!=null && e.effect.StartsWith("S7_"))__result=Subclass7.Invalid(caster,e,target);}}
 [HarmonyPatch(typeof(ClassEffects),nameof(ClassEffects.Apply))]
 public static class Subclass7Apply {static bool Prefix(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target)=>p==null || e?.effect==null || !e.effect.StartsWith("S7_") || !Subclass7.Apply(p,def,e,target);}
 [HarmonyPatch(typeof(ClassSystem),nameof(ClassSystem.CannotCast))]
 public static class Subclass7CannotCast {static void Postfix(Pawn p,CompProperties_ClassEffect props,ref string __result){if(__result==null && props?.effect!=null && props.effect.StartsWith("S7_SR"))__result=Subclass7.CannotCast(p,props);}}
 [HarmonyPatch(typeof(Psion),nameof(Psion.SubSpell))]
 public static class Subclass7SeerSpells {static void Postfix(Hediff_Adventurer h,CasterListDef l,AbilityDef d,ref bool __result){if(!__result && Subclass7.SeerSpell(h,l,d))__result=true;}}
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class Subclass7Damage {
  static bool Prefix(Pawn __instance,ref DamageInfo dinfo,ref bool absorbed){
   if(Subclass7.Users.Count==0 || dinfo.Def==null || !dinfo.Def.harmsHealth || dinfo.Amount<=0f || Subclass7.Busy || __instance.Dead)return true;
   var v=__instance;var a=dinfo.Instigator as Pawn;float f=1f;
   if(a!=null && a!=v && Subclass7.Users.Contains(a))f*=Subclass7.Outgoing(a,v,ref dinfo);
   f*=Subclass7.Incoming(v,a,dinfo,out bool dodge);
   if(dodge){absorbed=true;return false;}
   if(f!=1f)dinfo.SetAmount(dinfo.Amount*f);
   return true;
  }
 }
 [HarmonyPatch(typeof(Pawn_HealthTracker),"CheckForStateChange")]
 public static class Subclass7Stand {
  [HarmonyPriority(Priority.High)]
  static void Prefix(Pawn ___pawn){if(Subclass7.Users.Count>0 && Subclass7.Users.Contains(___pawn) && !___pawn.Dead && !___pawn.Downed && ClassCasters.WouldDown(___pawn))Subclass7.TryStand(___pawn);}
 }
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class Subclass7Kill {static void Postfix(Pawn __instance){if(Subclass7.Users.Count>0 && __instance.Dead)Subclass7.Killed(__instance);}}
 [HarmonyPatch(typeof(NpcClasses),nameof(NpcClasses.AiUsable))]
 public static class Subclass7NoAi {static void Postfix(Ability_Class a,ref bool __result){if(__result && a?.Props!=null && Subclass7.NoAi.Contains(a.Props.effect))__result=false;}}

 // 读档 / 新游戏时清缓存（Users 由 Tick250 重新填）。
 public class GameComponent_Subclass7 : GameComponent {
  public GameComponent_Subclass7(Game g){Subclass7.Reset();}
  public override void LoadedGame(){base.LoadedGame();Subclass7.Reset();}
 }
}
