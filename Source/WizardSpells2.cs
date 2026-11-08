using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace LichPhylactery {
 // 1.129.0 法师法术第二版：反制法术 + 4～9 环（Defs/WizardSpells2.xml，见 ArtSource/WizardSpells-1.129.0-spec.md / report.md）。
 // 复用：荒野变形（友军变形术）、石化卷轴（石化术第 3 道）、识破卷轴（真知术）、巫妖骷髅仆役（创造不死生物、指死术）、元素裔（召唤元素）、
 // 深渊 / 九狱的恶魔魔鬼（异界之门）、1.128.0 的法术区域（火墙、黑触手、厌恶之地、延时爆裂火球）、专注、升环、伤害前缀。
 [StaticConstructorOnStartup]
 public static class WizardSpells2 {
  static readonly Color C=new Color(.7f,.5f,1f),Sun=new Color(1f,.9f,.5f),Death=new Color(.55f,.8f,.55f);
  static int Now=>Find.TickManager.TicksGame;
  static bool Hostile(Pawn a,Pawn t)=>t!=null && t!=a && !t.Dead && t.HostileTo(a);
  static bool Friendly(Pawn a,Pawn t)=>t!=null && !t.Dead && !t.HostileTo(a);
  static IEnumerable<Pawn> Near(Map m,IntVec3 c,float r)=>m==null || !c.IsValid?Enumerable.Empty<Pawn>():Races124.PawnsNear(m,c,r).ToList();
  public static DamageDef Psychic=>DefCache<DamageDef>.Get("LP_WS_Psychic")??DamageDefOf.Burn;
  public static DamageDef Radiant=>PaladinCore.Radiant;
  static readonly string[] NoAi={"WS_Polymorph","WS_ArcaneEye","WS_Fabricate","WS_FaithfulHound","WS_Telekinesis","WS_ConjureElemental","WS_TeleportCircle","WS_ModifyMemory","WS_Scrying","WS_LegendLore",
   "WS_MoveEarth","WS_Contingency","WS_CreateUndead","WS_DimensionDoor","WS_Mansion","WS_Antipathy","WS_Clone","WS_Wish","WS_Gate","WS_DelayedBlast"};
  static WizardSpells2(){var skip=AccessTools.Field(typeof(NpcClasses),"Skip")?.GetValue(null) as HashSet<string>;if(skip!=null)foreach(var n in NoAi)skip.Add(n);}

  // ---------- 选目标 ----------
  public static string Invalid(Pawn caster,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;var th=target.Thing;
   switch(e.effect){
    case "WS_Polymorph":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(t.RaceProps.IsMechanoid)return "对机械体无效。";return null;
    case "WS_Stoneskin":case "WS_TrueSeeing":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(t.HostileTo(caster))return "只能对友军使用。";return null;
    case "WS_Banishment":case "WS_PhantasmalKiller":case "WS_ChainLightning":case "WS_FleshToStone":case "WS_FingerOfDeath":case "WS_PlaneShift":case "WS_Maze":case "WS_Feeblemind":case "WS_PowerWordKill":case "WS_Blight":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";return null;
    case "WS_HoldMonster":if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";if(t.RaceProps.IsMechanoid)return "对机械体无效。";return null;
    case "WS_Dominate":
     if(t==null || t.Dead)return "需要选择一个活着的目标。";if(!t.HostileTo(caster))return "只能对敌人使用。";
     if(DominatePersonOnly(e) && !t.RaceProps.Humanlike)return "支配人类只能对人形使用（支配怪物没有这个限制）。";return null;
    case "WS_Telekinesis":if(th==null || th==caster)return "需要选择一个人或一件物品。";if(!(th is Pawn) && th.def.category!=ThingCategory.Item)return "需要选择一个人或一件物品。";return null;
    case "WS_Fabricate":return FabricateCannot(caster,th);
    case "WS_TeleportCircle":return CircleCannot(caster,target);
    case "WS_ModifyMemory":if(t==null || t.Dead || !t.RaceProps.Humanlike)return "需要选择一个人。";if(!t.IsPrisonerOfColony && t.Faction!=caster.Faction)return "只能对囚犯或殖民者使用。";return null;
    case "WS_CreateUndead":return WizardSpells.AnimateCannot(th as Corpse);
    case "WS_DimensionDoor":{var map=caster.Map;var c=target.Cell;if(map==null || !c.InBounds(map) || c.Fogged(map) || !c.Standable(map))return "需要一个已探索、能站立的格子。";return null;}
    case "WS_Clone":return CloneCannot(caster);
    case "WS_MoveEarth":return target.Cell.IsValid?null:"需要选择一个位置。";
    case "WS_Contingency":return null;
   }
   return null;
  }
  static bool DominatePersonOnly(CompProperties_ClassEffect e)=>e==DefCache<AbilityDef>.Get("LP_WS_DominatePerson")?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();

  // ---------- 效果 ----------
  public static bool Apply(Pawn p,AbilityDef def,CompProperties_ClassEffect e,LocalTargetInfo target){
   var t=target.Thing as Pawn;var cell=target.Cell;var map=p.Map;
   float s=ClassEffects.Strength(p,e),pw=ClassEffects.Power(p,e);int dur=ClassEffects.Duration(p,e);float rad=ClassEffects.Radius(p,e);int up=Spellbook.CurUp;
   switch(e.effect){
    // 4 环
    case "WS_Polymorph":if(t==null || t.Dead)return true;if(Hostile(p,t)){Polymorph(p,t,dur);}else{Items.Give(t,"LP_WildShape",2500,s);Items.Mote(t,"变形：灰熊",C);}return true;
    case "WS_Stoneskin":if(Friendly(p,t)){Items.Give(t,"LP_WS_Stoneskin",dur,1f);Items.Mote(t,"石肤术",C);Spellbook.Concentrate(p,def,new[]{t},"LP_WS_Stoneskin");}return true;
    case "WS_WallOfFire":{var line=Line(p,cell,8);var z=SpellZone.Make(p,cell,"FireWall",.5f,dur);if(z!=null){z.line=line;z.power=pw;Spellbook.Concentrate(p,def,null,null,z);}return true;}
    case "WS_BlackTentacles":{var z=SpellZone.Make(p,cell,"Tentacles",rad,dur);if(z!=null){z.power=pw;Spellbook.Concentrate(p,def,null,null,z);}return true;}
    case "WS_Banishment":if(Hostile(p,t))BanishSpell(p,def,t,dur);return true;
    case "WS_Confusion":Confusion(p,cell,rad,dur);return true;
    case "WS_PhantasmalKiller":if(Hostile(p,t)){var h=(Hediff_WS)Items.Give(t,"LP_WS_Phantasm",dur,1f);if(h!=null){h.caster=p;h.power=pw;}Items.Mote(t,"魅影杀手",C);Spellbook.Concentrate(p,def,new[]{t},"LP_WS_Phantasm");}return true;
    case "WS_ArcaneEye":ArcaneEye(p,cell,e.radius);return true;
    case "WS_Fabricate":Fabricate(p,target.Thing,pw);return true;
    case "WS_FaithfulHound":Hound(p,dur);return true;
    case "WS_Blight":if(Hostile(p,t))Blight(p,t,pw,rad);return true;
    // 5 环
    case "WS_ConeOfCold":ConeOfCold(p,cell,e.range>0?e.range:10f,pw);return true;
    case "WS_Dominate":if(Hostile(p,t))Dominate(p,def,t,dur,DominatePersonOnly(e)?.5f:.45f);return true;
    case "WS_HoldMonster":if(Hostile(p,t))HoldMonster(p,t,1+up,dur);return true;
    case "WS_Telekinesis":Telekinesis(p,target.Thing,pw);return true;
    case "WS_ConjureElemental":ConjureElemental(p,def,dur);return true;
    case "WS_TeleportCircle":TeleportCircle(p,target);return true;
    case "WS_ModifyMemory":if(t!=null)ModifyMemory(p,t,s);return true;
    case "WS_Scrying":Scry(p,dur);return true;
    case "WS_LegendLore":LegendLore(p);return true;
    // 6 环
    case "WS_ChainLightning":if(Hostile(p,t))ChainLightning(p,t,pw);return true;
    case "WS_WallOfForce":ForceWall(p,Line(p,cell,8),Mathf.RoundToInt(pw),dur);Items.Mote(p,"力场墙",C);return true;
    case "WS_TrueSeeing":if(Friendly(p,t)){Items.Give(t,"LP_WS_TrueSeeing",dur,1f);Hediff_WS.Reveal(t,30f);if(t.Map!=null)ItemScrolls.TrueSeeing(t.Map);Items.Mote(t,"真知术",C);}return true;
    case "WS_CircleOfDeath":foreach(var x in Near(map,cell,rad))if(Hostile(p,x))WizardSpells.Hit(p,x,WizardSpells.Necrotic,pw,.3f);if(map!=null && cell.IsValid)FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,rad);return true;
    case "WS_FleshToStone":if(Hostile(p,t))FleshToStone(p,t);return true;
    case "WS_MoveEarth":MoveEarth(p,cell);return true;
    case "WS_Contingency":Contingency(p,dur);return true;
    case "WS_Sunbeam":Sunbeam(p,def,cell,pw,dur);return true;
    case "WS_CreateUndead":Raise(p,target.Thing as Corpse,3,"食尸鬼仆役");return true;
    // 7 环
    case "WS_DelayedBlast":{int sec=Spellbook.Charge(def)?up:0;var z=SpellZone.Make(p,cell,"Delayed",rad,sec*60);if(z!=null)z.power=pw+10f*sec*ClassEffects.Strength(p,e);return true;}
    case "WS_DimensionDoor":DimensionDoor(p,cell);return true;
    case "WS_FingerOfDeath":if(Hostile(p,t))FingerOfDeath(p,t,pw);return true;
    case "WS_ReverseGravity":ReverseGravity(p,def,cell,rad,dur,pw);return true;
    case "WS_PlaneShift":if(Hostile(p,t)){if(WizardSpells.TryControl(p,t,.5f,false))Banishment.Remove(t,"送往别的位面");}return true;
    case "WS_Mansion":foreach(var x in Near(map,p.Position,5f))if(Friendly(p,x) && x.RaceProps.Humanlike)Mansion(x,dur);return true;
    // 8 环
    case "WS_ForceCage":ForceCage(p,cell,rad,dur);return true;
    case "WS_Maze":if(Hostile(p,t)){float ch=ClassSubclass.Bossy(t)?.5f:.9f;if((WizardSpells.RollOverride??Rand.Value)<ch)Banishment.Banish(t,dur,"迷宫放逐");else Items.Mote(t,"走出了迷宫",C);}return true;
    case "WS_Feeblemind":if(Hostile(p,t)){WizardSpells.Hit(p,t,Psychic,pw,1f);if(!t.Dead && WizardSpells.TryControl(p,t,.5f,true)){Items.Give(t,"LP_WS_Feebleminded",dur,1f);Items.Mote(t,"弱智",C);}}return true;
    case "WS_Antipathy":Antipathy(p,rad,dur);return true;
    case "WS_Sunburst":foreach(var x in Near(map,cell,rad))if(Hostile(p,x)){WizardSpells.Hit(p,x,Radiant,pw,.3f);if(!x.Dead)Items.Give(x,"LP_WS_Blinded",600,1f);}if(map!=null && cell.IsValid)FleckMaker.Static(cell,map,FleckDefOf.PsycastAreaEffect,rad);Items.Mote(p,"阳炎爆",Sun);return true;
    case "WS_Clone":Clone(p,dur);return true;
    // 9 环
    case "WS_TimeStop":TimeStop(p,dur);return true;
    case "WS_Wish":Wish(p);return true;
    case "WS_Gate":Gate(p,dur);return true;
    case "WS_PowerWordKill":if(Hostile(p,t))PowerWordKill(p,t,pw);return true;
    case "WS_MassPolymorph":foreach(var x in Near(map,cell,rad).Where(x=>Hostile(p,x) && !x.RaceProps.IsMechanoid).OrderBy(x=>x.Position.DistanceToSquared(cell)).Take(10).ToList())Polymorph(p,x,dur);return true;
   }
   return false;
  }

  // ---------- 4 环 ----------
  public static bool Polymorph(Pawn p,Pawn t,int ticks){
   if(t.RaceProps.IsMechanoid || !WizardSpells.TryControl(p,t,.5f,false))return false;
   Items.Give(t,"LP_WS_Polymorphed",ticks,1f);Items.Mote(t,"变成了兔子",C);if(t.Map!=null)FleckMaker.ThrowDustPuffThick(t.DrawPos,t.Map,1.5f,C);return true;
  }
  // 一条垂直于施法方向、长 n 格的直线（火墙、力场墙）。
  public static List<IntVec3> Line(Pawn p,IntVec3 center,int n){
   var dir=center-p.Position;bool alongZ=Math.Abs(dir.x)>=Math.Abs(dir.z);var l=new List<IntVec3>();
   for(int k=-(n/2);k<n-(n/2);k++)l.Add(alongZ?new IntVec3(center.x,0,center.z+k):new IntVec3(center.x+k,0,center.z));
   return l;
  }
  static void BanishSpell(Pawn p,AbilityDef def,Pawn t,int ticks){
   if(!WizardSpells.TryControl(p,t,.6f,false))return;
   if(Outsiders.Of(t)!=null || WizardSpells.CircleKind(t,0) || WizardSpells.CircleKind(t,2)){Banishment.Remove(t,"被放逐回老家");return;} // 外来者（恶魔、魔鬼、元素、妖精……）永久送走
   if(Banishment.Banish(t,ticks,"放逐"))Spellbook.Concentrate(p,def,new[]{t},null,null,"banish");
  }
  public static int Confusion(Pawn p,IntVec3 c,float r,int ticks){
   int n=0;foreach(var x in Near(p.Map,c,r)){
    if(!Hostile(p,x) || x.Downed || x.mindState?.mentalStateHandler==null || !WizardSpells.TryControl(p,x,.55f,true))continue;
    if(x.mindState.mentalStateHandler.TryStartMentalState(MentalStateDefOf.Berserk,"困惑术",true,true)){if(x.MentalState!=null)x.MentalState.forceRecoverAfterTicks=ticks;Items.Mote(x,"困惑",C);n++;}
   }
   return n;
  }
  public static void PhantasmTick(Hediff_WS h){
   var t=h.pawn;if(t==null || t.Dead || !t.Spawned)return;
   if(Rand.Chance(h.caster==null?.55f:WizardSpells.Chance(.55f,h.caster,t))){WizardSpells.Hit(h.caster,t,Psychic,h.power,1f);if(!t.Dead){Items.Give(t,"LP_Shaken",180,1f);Items.Mote(t,"魅影",C);}}
  }
  public static int ArcaneEye(Pawn p,IntVec3 c,float r){
   var map=p.Map;if(map==null || !c.IsValid)return 0;int n=0;
   foreach(var x in GenRadial.RadialCellsAround(c,r,true))if(x.InBounds(map) && x.Fogged(map)){map.fogGrid.Unfog(x);n++;}
   Items.Mote(p,"秘法之眼",C);return n;
  }
  // 急制术：工作台第一个能做的账单立即完成一件（品质按施法者的技能），材料从地图上取。
  public static string FabricateCannot(Pawn p,Thing t){
   if(!(t is Building_WorkTable b))return "需要选择一台工作台。";
   var bill=FabricateBill(b,999999f);return bill==null?"这台工作台没有能做的账单（要有产物的配方）。":null;
  }
  static Bill_Production FabricateBill(Building_WorkTable b,float maxWork)=>b.BillStack?.Bills.OfType<Bill_Production>().FirstOrDefault(x=>!x.suspended && x.ShouldDoNow() && x.recipe?.products!=null && x.recipe.products.Count>0 && x.recipe.workAmount<=maxWork);
  public static string FabricateFail;
  public static List<Thing> Fabricate(Pawn p,Thing table,float maxWork){FabricateFail=null;
   var made=new List<Thing>();var b=table as Building_WorkTable;var map=b?.Map;if(b==null || map==null)return made;
   var bill=FabricateBill(b,maxWork);if(bill==null){FabricateFail="no bill";Messages.Message("急制术：没有工作量在 "+maxWork.ToString("0")+" 以内、能做的账单。",b,MessageTypeDefOf.RejectInput,false);return made;}
   var r=bill.recipe;var used=new List<Thing>();var plan=new List<(Thing t,int n)>();
   foreach(var ing in r.ingredients){
    float need=ing.GetBaseCount();
    foreach(var t in map.listerThings.AllThings.Where(x=>x.Spawned && x.def.category==ThingCategory.Item && !x.IsForbidden(Faction.OfPlayer) && ing.filter.Allows(x) && bill.IsFixedOrAllowedIngredient(x) && !plan.Any(q=>q.t==x)).OrderBy(x=>x.Position.DistanceToSquared(b.Position)).ToList()){
     if(need<=.001f)break;float per=r.IngredientValueGetter.ValuePerUnitOf(t.def);if(per<=0f)continue;
     int n=Mathf.Min(t.stackCount,Mathf.CeilToInt(need/per));plan.Add((t,n));need-=n*per;
    }
    if(need>.001f){FabricateFail="need "+need+" of "+ing.filter.Summary;Messages.Message("急制术：材料不够（"+ing.filter.Summary+"）。",b,MessageTypeDefOf.RejectInput,false);return made;}
   }
   foreach(var (t,n) in plan)used.Add(t.SplitOff(n));
   var dominant=used.OrderByDescending(x=>x.stackCount).FirstOrDefault(x=>x.def.IsStuff);
   foreach(var prod in GenRecipe.MakeRecipeProducts(r,p,used,dominant,b)){if(GenPlace.TryPlaceThing(prod,b.InteractionCell.IsValid?b.InteractionCell:b.Position,map,ThingPlaceMode.Near))made.Add(prod);}
   if(made.Count==0)FabricateFail="no products ("+used.Count+" used)";bill.Notify_IterationCompleted(p,used);
   foreach(var t in used)if(!t.Destroyed)t.Destroy();
   Items.Mote(b,"急制术："+r.label,C);return made;
  }
  public static Pawn Hound(Pawn p,int ticks){
   var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail("Husky")??DefDatabase<PawnKindDef>.GetNamedSilentFail("LabradorRetriever");if(kind==null || p.Map==null)return null;
   var dog=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,Faction.OfPlayer));GenSpawn.Spawn(dog,CellFinder.RandomClosewalkCellNear(p.Position,p.Map,2),p.Map);
   dog.Name=new NameSingle("忠犬守卫");var h=(Hediff_WS)Items.Give(dog,"LP_WS_Hound",ticks,1f);if(h!=null)h.caster=p;Items.Mote(dog,"忠犬守卫",C);return dog;
  }
  public static void HoundTick(Hediff_WS h){
   var d=h.pawn;if(d==null || d.Dead || !d.Spawned || Now-h.lastAlarm<2500)return;
   var foe=Races124.PawnsNear(d.Map,d.Position,15f).FirstOrDefault(x=>!x.Dead && !x.Downed && x.HostileTo(Faction.OfPlayer));if(foe==null)return;
   h.lastAlarm=Now;if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("忠犬守卫","看不见的守门犬狂吠起来：有敌人靠近了——"+foe.LabelShort+"。",LetterDefOf.ThreatSmall,new LookTargets(foe));
  }
  static bool PlantLike(Pawn t)=>t.RaceProps.Insect || t.RaceProps.FleshType!=null && (t.RaceProps.FleshType.defName.Contains("Plant") || t.RaceProps.FleshType.defName.Contains("Tree") || t.RaceProps.FleshType.defName.Contains("Insect"));
  public static float Blight(Pawn p,Pawn t,float amount,float r){
   var map=t.Map;var c=t.Position;float dealt=WizardSpells.Hit(p,t,WizardSpells.Necrotic,amount*(PlantLike(t)?1.5f:1f),.3f);
   if(map!=null)foreach(var x in GenRadial.RadialDistinctThingsAround(c,map,r,true).OfType<Plant>().ToList())if(!x.Destroyed)x.Kill(null);
   Items.Mote(t,"枯萎",Death);return dealt;
  }
  // ---------- 5 环 ----------
  public static int ConeOfCold(Pawn p,IntVec3 toward,float range,float amount){
   int n=0;foreach(var x in ClassLegendary.Cone(p,toward,range)){WizardSpells.Hit(p,x,WizardSpells.Frost,amount,.3f);if(!x.Dead)x.TakeDamage(new DamageInfo(DamageDefOf.Frostbite,4f,0f,-1f,p));n++;}
   if(p.Map!=null)FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,3f,new Color(.7f,.85f,1f));Items.Mote(p,"锥形寒冰",C);return n;
  }
  public static bool Dominate(Pawn p,AbilityDef def,Pawn t,int ticks,float chance){
   if(!WizardSpells.TryControl(p,t,chance,!t.RaceProps.IsMechanoid))return false;
   var orig=t.Faction;t.GetLord()?.RemovePawn(t);t.jobs?.StopAll();if(t.mindState!=null)t.mindState.enemyTarget=null;if(t.InMentalState)t.MentalState.RecoverFromState();
   t.SetFaction(Faction.OfPlayer);var h=(Hediff_WS)Items.Give(t,"LP_WS_Dominated",ticks,1f);if(h!=null){h.origFaction=orig;h.caster=p;}
   Items.Mote(t,"被支配",C);Spellbook.Concentrate(p,def,new[]{t},"LP_WS_Dominated");return true;
  }
  public static void Release(Pawn t,Faction orig){
   if(t==null || t.Dead || t.Destroyed)return;if(t.Drafted)t.drafter.Drafted=false;
   if(orig!=null && t.Faction!=orig){t.SetFaction(orig);if(t.Spawned && orig.HostileTo(Faction.OfPlayer))LordMaker.MakeNewLord(orig,new LordJob_AssaultColony(orig),t.Map,new[]{t});}
   if(t.Spawned){t.stances?.stunner?.StunFor(300,null);Items.Mote(t,"支配结束",C);}
  }
  public static int HoldMonster(Pawn p,Pawn t,int count,int ticks){
   var list=new List<Pawn>{t};list.AddRange(Near(t.Map,t.Position,6f).Where(x=>x!=t && Hostile(p,x) && !x.RaceProps.IsMechanoid && !x.Downed).OrderBy(x=>x.Position.DistanceToSquared(t.Position)).Take(count-1));
   int n=0;foreach(var x in list){if(!WizardSpells.TryControl(p,x,.5f,true))continue;x.stances?.stunner?.StunFor(ticks,p);Items.Mote(x,"定身",C);n++;}
   return n;
  }
  public static string Telekinesis(Pawn p,Thing th,float amount){
   var map=p.Map;if(th==null || map==null || !th.Spawned)return null;
   if(th is Pawn x){
    if(Hostile(p,x)){if(!WizardSpells.TryControl(p,x,.55f,false))return "resisted";DNDPush.Move(x,DNDPush.Destination(map,x.Position,(x.Position-p.Position).ToVector3(),6));WizardSpells.Hit(p,x,DamageDefOf.Blunt,amount,.2f);if(!x.Dead)x.stances?.stunner?.StunFor(120,p);Items.Mote(x,"抛飞",C);return "thrown";}
    var c=CellFinder.StandableCellNear(p.Position,map,2f);if(c.IsValid)DNDPush.Move(x,c);Items.Mote(x,"心灵遥控",C);return "pulled";
   }
   WizardSpells.MageHand(p,th);return "item";
  }
  static readonly (string xeno,string label)[] Elements={("LP_GenasiFire","火元素"),("LP_GenasiWater","水元素"),("LP_GenasiEarth","土元素"),("LP_GenasiAir","风元素")};
  public static Pawn SummonElemental(Pawn p,AbilityDef def,int k,int ticks){
   var map=p.Map;if(map==null)return null;var x=DefDatabase<XenotypeDef>.GetNamedSilentFail(Elements[Mathf.Clamp(k,0,3)].xeno);
   Pawn e;try{e=PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,Faction.OfPlayer,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true,forcedXenotype:x,fixedBiologicalAge:30,fixedChronologicalAge:30));}
   catch(Exception ex){Log.Warning("[DND边缘大陆] 召唤元素失败："+ex.Message);return null;}
   e.Name=new NameSingle(Elements[Mathf.Clamp(k,0,3)].label);e.skills?.GetSkill(SkillDefOf.Melee)?.EnsureMinLevelWithMargin(10);
   GenSpawn.Spawn(e,CellFinder.RandomClosewalkCellNear(p.Position,map,2),map);var h=(Hediff_WS)Items.Give(e,"LP_WS_Summoned",ticks,1f);if(h!=null)h.caster=p;
   FleckMaker.ThrowDustPuffThick(e.DrawPos,map,2f,C);Items.Mote(e,Elements[Mathf.Clamp(k,0,3)].label,C);
   Spellbook.Concentrate(p,def,new[]{e},"LP_WS_Summoned");return e;
  }
  static void ConjureElemental(Pawn p,AbilityDef def,int ticks){
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){SummonElemental(p,def,0,ticks);return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("召唤元素","选一种元素。",Enumerable.Range(0,4).Select(i=>(Elements[i].label,(string)null,(Action)(()=>SummonElemental(p,def,i,ticks)))).ToList()));
  }
  // 传送法阵：空地上刻法阵；对着法阵施放就把阵上的人和物送到另一座（优先主基地）。
  static ThingDef CircleDef=>DefCache<ThingDef>.Get("LP_WS_TeleportCircle");
  static string CircleCannot(Pawn p,LocalTargetInfo t){
   var map=p.Map;if(map==null || !t.Cell.IsValid)return "需要选择一个位置。";
   if(t.Thing!=null && t.Thing.def==CircleDef)return OtherCircle(t.Thing)==null?"没有另一座传送法阵可以去。":null;
   foreach(var c in CellRect.CenteredOn(t.Cell,1))if(!c.InBounds(map) || !c.Standable(map) || c.GetEdifice(map)!=null || c.GetThingList(map).Any(x=>x.def==CircleDef))return "法阵要一块 3×3 的空地。";
   return null;
  }
  public static Thing OtherCircle(Thing from){
   var maps=Find.Maps.Where(m=>m.IsPlayerHome).Concat(Find.Maps).Distinct();
   foreach(var m in maps){var c=m.listerThings.ThingsOfDef(CircleDef).FirstOrDefault(x=>x!=from);if(c!=null)return c;}
   return null;
  }
  public static Thing TeleportCircle(Pawn p,LocalTargetInfo t){
   var map=p.Map;if(CircleCannot(p,t)!=null)return null;
   if(t.Thing!=null && t.Thing.def==CircleDef){SendCircle(t.Thing);return t.Thing;}
   var c=ThingMaker.MakeThing(CircleDef);c.SetFaction(Faction.OfPlayer);GenSpawn.Spawn(c,t.Cell,map);Items.Mote(c,"传送法阵",C);return c;
  }
  public static int SendCircle(Thing from){
   var to=OtherCircle(from);if(to==null || from.Map==null)return 0;var src=from.OccupiedRect();var dst=to.OccupiedRect();int n=0;
   foreach(var cell in src.Cells.ToList())foreach(var th in cell.GetThingList(from.Map).Where(x=>x is Pawn || x.def.category==ThingCategory.Item).ToList()){
    var off=cell-src.Min;var dc=dst.Min+off;th.DeSpawn();GenSpawn.Spawn(th,dc,to.Map);n++;
   }
   FleckMaker.ThrowDustPuffThick(from.DrawPos,from.Map,3f,C);if(to.Spawned)FleckMaker.ThrowDustPuffThick(to.DrawPos,to.Map,3f,C);
   if(n>0 && !Adventurer.TestSuppressed)Messages.Message("传送法阵送走了 "+n+" 样东西。",new LookTargets(to),MessageTypeDefOf.PositiveEvent,false);
   return n;
  }
  public static void ModifyMemory(Pawn p,Pawn t,float s){
   if(t.IsPrisonerOfColony && t.guest!=null){float before=t.guest.resistance;t.guest.resistance=Mathf.Max(0f,before*(1f-.5f*s));Items.Mote(t,"抗拒 "+before.ToString("0")+" → "+t.guest.resistance.ToString("0"),C);return;}
   var mem=t.needs?.mood?.thoughts?.memories;if(mem==null)return;var bad=mem.Memories.Where(m=>m.MoodOffset()<0f).OrderBy(m=>m.MoodOffset()).ToList();
   if(bad.Count==0){Items.Mote(t,"没有负面记忆",C);return;}
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){mem.RemoveMemory(bad[0]);Items.Mote(t,"忘掉了一件事",C);return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("修改记忆："+t.LabelShort,"选一条负面记忆删掉。",bad.Take(15).Select(m=>(m.LabelCap+"（"+m.MoodOffset().ToString("0")+"）",(string)null,(Action)(()=>{mem.RemoveMemory(m);Items.Mote(t,"忘掉了一件事",C);}))).ToList()));
  }
  // 探知术：列出 3 天内排定的威胁事件、估计袭击规模；之后 3 天内来袭时提前一封警报。
  public static string Scry(Pawn p,int ticks){
   var d=Spellbook.Data(p);if(d!=null)d.scryUntil=Now+ticks;var map=p.Map;
   var lines=new List<string>();
   foreach(QueuedIncident q in Find.Storyteller.incidentQueue)if(q.FireTick-Now<=ticks && q.FiringIncident?.def!=null && (q.FiringIncident.def.category==IncidentCategoryDefOf.ThreatBig || q.FiringIncident.def.category==IncidentCategoryDefOf.ThreatSmall))
    lines.Add("· 约 "+((q.FireTick-Now)/60000f).ToString("0.#")+" 天后："+q.FiringIncident.def.LabelCap);
   float pts=map==null?0f:StorytellerUtility.DefaultThreatPointsNow(map);
   string text=(lines.Count>0?"已经排定的威胁：\n"+string.Join("\n",lines):"3 天内看不到已经排定的袭击。")+"\n\n按殖民地现在的财富和人口，一次袭击的规模约 "+pts.ToString("0")+" 点。之后 3 天内若有袭击到来，"+p.LabelShort+"会提前感知。";
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("探知术",text,LetterDefOf.NeutralEvent,p);
   return text;
  }
  // 通晓传奇：生成原版物资藏匿点任务，藏匿点里多一件 DND 魔法物品。
  public static Quest LegendLore(Pawn p){
   var script=DefDatabase<QuestScriptDef>.GetNamedSilentFail("OpportunitySite_ItemStash");Quest q=null;
   try{if(script!=null){var slate=new Slate();slate.Set("points",StorytellerUtility.DefaultThreatPointsNow(p.Map??Find.AnyPlayerHomeMap));q=QuestUtility.GenerateQuestAndMakeAvailable(script,slate);}}
   catch(Exception ex){Log.Warning("[DND边缘大陆] 通晓传奇：生成任务失败："+ex.Message);}
   var item=MagicItem(QualityCategory.Excellent);bool hidden=false;
   if(q!=null && item!=null){
    foreach(var part in q.PartsListForReading.OfType<QuestPart_SpawnWorldObject>()){if(part.worldObject is Site site){var sp=site.parts?.FirstOrDefault(x=>x.things!=null);if(sp!=null){sp.things.TryAdd(item);hidden=true;break;}}}
   }
   if(item!=null && !hidden){if(p.Spawned)GenPlace.TryPlaceThing(item,p.Position,p.Map,ThingPlaceMode.Near);else item.Destroy();}
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("通晓传奇",q!=null?"古老的记载指向一处藏匿的宝藏，传说里提到的「"+(item?.LabelCap??"")+"」"+(hidden?"就藏在那里":"已经找到了")+"。到任务页查看。":"没有查到藏宝的下落，但找到了「"+(item?.LabelCap??"")+"」。",LetterDefOf.PositiveEvent,item!=null && item.Spawned?new LookTargets(item):LookTargets.Invalid,null,q);
   return q;
  }
  public static Thing MagicItem(QualityCategory q){
   var pool=DefDatabase<ThingDef>.AllDefsListForReading.Where(d=>d.category==ThingCategory.Item && d.defName.StartsWith("LP_") && !d.IsCorpse && d.tradeTags!=null && d.tradeTags.Any(x=>x.Contains("Magic") || x.Contains("Wondrous") || x.Contains("Relic")) && d.BaseMarketValue>0f).ToList();
   if(pool.Count==0)return null;var def=pool.RandomElement();var t=ThingMaker.MakeThing(def,def.MadeFromStuff?GenStuff.DefaultStuffFor(def):null);t.TryGetComp<CompQuality>()?.SetQuality(q,ArtGenerationContext.Outsider);return t;
  }
  // ---------- 6 环 ----------
  public static int ChainLightning(Pawn p,Pawn t,float amount){
   var hit=new List<Pawn>();var cur=t;float a=amount;var dmg=ClassCasters.Lightning??DamageDefOf.Burn;
   for(int i=0;i<6 && cur!=null;i++){
    bool mech=cur.RaceProps.IsMechanoid;WizardSpells.Hit(p,cur,mech?DamageDefOf.EMP:dmg,a,.5f);if(mech && !cur.Dead)cur.stances?.stunner?.StunFor(120,p);
    if(cur.Map!=null)FleckMaker.ThrowLightningGlow(cur.DrawPos,cur.Map,1.2f);hit.Add(cur);a*=.85f;
    var from=cur;cur=Near(from.MapHeld,from.PositionHeld,10f).Where(x=>!hit.Contains(x) && Hostile(p,x)).OrderBy(x=>x.Position.DistanceToSquared(from.PositionHeld)).FirstOrDefault();
   }
   Items.Mote(p,"连锁闪电 ×"+hit.Count,C);return hit.Count;
  }
  public static List<Thing> ForceWall(Pawn p,IEnumerable<IntVec3> cells,int hp,int ticks){
   var made=new List<Thing>();var map=p.Map;var def=DefCache<ThingDef>.Get("LP_WS_ForceWall");if(def==null || map==null)return made;
   foreach(var c in cells){
    if(!c.InBounds(map) || c.Fogged(map) || c.GetEdifice(map)!=null || !c.Walkable(map) || c.GetFirstPawn(map)!=null || c.GetThingList(map).Any(t=>t.def.category==ThingCategory.Building))continue;
    var w=ThingMaker.MakeThing(def);GenSpawn.Spawn(w,c,map);w.HitPoints=Mathf.Clamp(hp,1,w.MaxHitPoints);
    var life=w.TryGetComp<CompLifespan>();if(life!=null)life.age=Mathf.Max(0,life.Props.lifespanTicks-ticks);
    made.Add(w);
   }
   return made;
  }
  public static List<Thing> ForceCage(Pawn p,IntVec3 c,float r,int ticks){
   var ring=GenRadial.RadialCellsAround(c,r+1.5f,true).Where(x=>!x.InHorDistOf(c,r+.5f));
   var made=ForceWall(p,ring,99999,ticks);foreach(var x in Near(p.Map,c,r))if(Hostile(p,x))Items.Mote(x,"关进力场牢笼",C);return made;
  }
  public static bool FleshToStone(Pawn p,Pawn t){
   if(!WizardSpells.TryControl(p,t,.45f,false)){Items.Give(t,"LP_WS_Slowed",600,1f);Items.Mote(t,"身体变得僵硬",C);return false;}
   var hd=Items.Hediff("LP_WS_StoneCurse");if(hd==null)return false;var h=t.health.hediffSet.GetFirstHediffOfDef(hd);
   if(h==null)h=Items.Give(t,"LP_WS_StoneCurse",60000,1f);else{h.Severity=Mathf.Min(3f,h.Severity+1f);Items.Give(t,"LP_WS_StoneCurse",60000);}
   if(h==null)return false;int n=Mathf.RoundToInt(h.Severity);
   Items.Mote(t,"石痕 "+n+" / 3",new Color(.75f,.75f,.7f));
   if(n>=3){t.health.RemoveHediff(h);ItemScrolls.StartPetrify(null,t);}
   return true;
  }
  public static int MoveEarth(Pawn p,IntVec3 c){
   var map=p.Map;if(map==null || !c.IsValid)return 0;int n=0;
   foreach(var cell in GenRadial.RadialCellsAround(c,6f,true).Where(x=>x.InBounds(map)).OrderBy(x=>x.DistanceToSquared(c)).ToList()){
    if(n>=30)break;var m=cell.GetEdifice(map) as Mineable;if(m==null || m.Faction!=null)continue;
    var yield=m.def.building?.mineableThing;int count=Mathf.RoundToInt((m.def.building?.mineableYield??0)*.5f);
    m.Destroy(DestroyMode.Vanish);n++;
    if(yield!=null && count>0){var y=ThingMaker.MakeThing(yield);y.stackCount=count;GenPlace.TryPlaceThing(y,cell,map,ThingPlaceMode.Near);}
    else if(yield!=null && Rand.Chance(.5f))GenPlace.TryPlaceThing(ThingMaker.MakeThing(yield),cell,map,ThingPlaceMode.Near);
    if(n%3==0)FleckMaker.ThrowDustPuffThick(cell.ToVector3Shifted(),map,2f,new Color(.6f,.55f,.5f));
   }
   Items.Mote(p,"移山术 ×"+n,C);return n;
  }
  // 应急术：预存一个自身法术，倒地或伤势过半时自动免费施放。
  public static readonly HashSet<Pawn> ContingencyPawns=new HashSet<Pawn>();
  public static readonly string[] ContingencyOptions={"LP_ShieldSpell","LP_WS_FalseLife","LP_WS_Blink","LP_WS_Stoneskin","LP_MageArmor","LP_MirrorImage"};
  public static void SetContingency(Pawn p,AbilityDef a,int ticks){var d=Spellbook.Data(p);if(d==null || a==null)return;d.contingency=a;d.contingencyUntil=Now+ticks;ContingencyPawns.Add(p);Items.Mote(p,"应急术："+a.label,C);}
  static void Contingency(Pawn p,int ticks){
   var opts=ContingencyOptions.Select(n=>DefCache<AbilityDef>.Get(n)).Where(a=>a!=null).ToList();
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){SetContingency(p,opts[0],ticks);return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("应急术","选一个法术，倒地或伤势过半时自动施放（10 天内，只能存一个）。",opts.Select(a=>(a.LabelCap.Resolve(),a.description,(Action)(()=>SetContingency(p,a,ticks)))).ToList()));
  }
  public static bool CheckContingency(Pawn p){
   var d=Spellbook.Data(p);if(d?.contingency==null || Now>d.contingencyUntil){ContingencyPawns.Remove(p);if(d!=null)d.contingency=null;return false;}
   if(p.Dead || !(p.Downed || p.health.summaryHealth.SummaryHealthPercent<=.5f))return false;
   var a=d.contingency;d.contingency=null;ContingencyPawns.Remove(p);var props=a.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();if(props==null)return false;
   Spellbook.EndCast();ClassEffects.Apply(p,a,props,new LocalTargetInfo(p));Items.Mote(p,"应急术发动："+a.label,C);return true;
  }
  public static int Sunbeam(Pawn p,AbilityDef def,IntVec3 target,float amount,int ticks){
   var map=p.Map;if(map==null || !target.IsValid)return 0;int hits=0;
   foreach(var c in GenSight.PointsOnLineOfSight(p.Position,target).Concat(Gen.YieldSingle(target)).Distinct()){
    if(c==p.Position || !c.InBounds(map))continue;FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(),map,.8f);
    foreach(var t in c.GetThingList(map).OfType<Pawn>().ToList())if(Hostile(p,t)){WizardSpells.Hit(p,t,Radiant,amount,.3f);if(!t.Dead)Items.Give(t,"LP_WS_Blinded",600,1f);hits++;}
   }
   if(!Items.Has(p,"LP_WS_SunbeamCharge")){Items.Give(p,"LP_WS_SunbeamCharge",ticks,1f);Spellbook.Concentrate(p,def,new[]{p},"LP_WS_SunbeamCharge");}
   Items.Mote(p,"阳炎射线",Sun);return hits;
  }
  public static List<Pawn> Raise(Pawn p,Corpse c,int count,string name){
   var got=new List<Pawn>();if(WizardSpells.AnimateCannot(c)!=null || p.Map==null)return got;
   var list=new List<Corpse>{c};list.AddRange(GenRadial.RadialDistinctThingsAround(c.Position,c.Map,5f,true).OfType<Corpse>().Where(x=>x!=c && WizardSpells.AnimateCannot(x)==null).OrderBy(x=>x.Position.DistanceToSquared(c.Position)).Take(count-1));
   foreach(var corpse in list){var sk=RaiseOne(p,corpse,name);if(sk!=null)got.Add(sk);}
   if(got.Count>0)Items.Mote(p,name+" ×"+got.Count,Death);return got;
  }
  public static Pawn RaiseOne(Pawn p,Corpse corpse,string name){
   var map=corpse?.Map;if(map==null)return null;var pos=corpse.Position;
   try{corpse.Destroy();var sk=Constructs.Create("Skeleton",p,pos,map);sk.Name=new NameSingle(name);FleckMaker.ThrowDustPuffThick(pos.ToVector3Shifted(),map,2f,Death);return sk;}
   catch(Exception ex){Log.Warning("[DND边缘大陆] 亡灵仆从失败："+ex.Message);return null;}
  }
  // ---------- 7 环 ----------
  public static int DimensionDoor(Pawn p,IntVec3 dest){
   var map=p.Map;if(map==null || !dest.IsValid)return 0;
   var buddies=GenAdj.CellsAdjacent8Way(p).Where(c=>c.InBounds(map)).Select(c=>c.GetFirstPawn(map)).Where(x=>x!=null && x!=p && !x.Dead && !x.HostileTo(p) && !x.Downed).Distinct().Take(2).ToList();
   FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,C);Races124.Blink(p,dest);int n=1;
   foreach(var b in buddies){var c=CellFinder.StandableCellNear(dest,map,2f,x=>x.GetFirstPawn(map)==null);if(!c.IsValid)continue;b.pather?.StopDead();b.Position=c;b.Notify_Teleported(true,false);n++;}
   FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,C);Items.Mote(p,"次元门 ×"+n,C);return n;
  }
  public static Pawn FingerOfDeath(Pawn p,Pawn t,float amount){
   WizardSpells.Hit(p,t,WizardSpells.Necrotic,amount,.5f);if(t.Map!=null && !t.Dead)Items.Mote(t,"指死术",Death);
   if(t.Dead && t.RaceProps.Humanlike && t.Corpse!=null && t.Corpse.Spawned)return RaiseOne(p,t.Corpse,"僵尸仆役");
   return null;
  }
  public static List<Pawn> ReverseGravity(Pawn p,AbilityDef def,IntVec3 c,float r,int ticks,float fall){
   var got=new List<Pawn>();foreach(var x in Near(p.Map,c,r))if(Hostile(p,x)){x.stances?.stunner?.StunFor(ticks,p);var h=(Hediff_WS)Items.Give(x,"LP_WS_Levitating",ticks,1f);if(h!=null){h.caster=p;h.power=fall;}got.Add(x);}
   if(got.Count>0)Spellbook.Concentrate(p,def,got,"LP_WS_Levitating");Items.Mote(p,"反转重力",C);return got;
  }
  public static void Mansion(Pawn x,int ticks){
   Items.Give(x,"LP_WS_Mansion",ticks,1f);var th=DefCache<ThoughtDef>.Get("LP_WS_MansionThought");if(th!=null)x.needs?.mood?.thoughts?.memories?.TryGainMemory(th);
  }
  // ---------- 8 环 ----------
  public static bool AntipathyKind(Pawn x,int mode){switch(mode){case 0:return x.RaceProps.Humanlike;case 1:return x.RaceProps.Insect;case 2:return x.RaceProps.Animal && !x.RaceProps.Insect;case 3:return x.RaceProps.IsMechanoid;}return false;}
  static readonly string[] AntipathyLabels={"人形","虫族","动物","机械体"};
  static void Antipathy(Pawn p,float r,int ticks){
   var z=SpellZone.Make(p,p.Position,"Antipathy",r,ticks);if(z==null || !Spellbook.Player(p) || Adventurer.TestSuppressed)return;
   Find.WindowStack.Add(new Dialog_SpellChoice("厌恶之地","选一类敌对单位：它们进入这片区域时成功率 50% 被赶出去。",Enumerable.Range(0,4).Select(i=>(AntipathyLabels[i],(string)null,(Action)(()=>{z.mode=i;Items.Mote(p,"厌恶之地："+AntipathyLabels[i],C);}))).ToList()));
  }
  public static string CloneCannot(Pawn p){
   if(Items.IsLich(p))return "巫妖不能培养克隆体。";if(!ModsConfig.BiotechActive)return "需要生物科技 DLC。";
   if(p.Map==null || !p.Map.listerBuildings.allBuildingsColonist.Any(b=>b.def.defName=="GrowthVat"))return "地图上要有一座生长缸。";
   return null;
  }
  static void Clone(Pawn p,int ticks){var d=Spellbook.Data(p);if(d==null || CloneCannot(p)!=null)return;d.cloneReady=Now+ticks;if(!Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"的克隆体开始在生长缸里成长，"+(ticks/60000f).ToString("0")+" 天后长成。",p,MessageTypeDefOf.PositiveEvent,false);}
  public static bool CloneReady(Pawn p){var d=Adventurer.Get(p)?.classes?.book;return d!=null && d.cloneReady>0 && Now>=d.cloneReady;}
  // 死后下一刻：装备留在原地，在主基地的克隆体里醒来。
  public static bool Reawaken(Pawn p){
   if(p==null || !p.Dead || !CloneReady(p))return false;var d=Spellbook.Data(p);d.cloneReady=-1;
   var corpse=p.Corpse;var map=corpse?.MapHeld;var pos=corpse?.PositionHeld??IntVec3.Invalid;
   if(map!=null && pos.IsValid){p.apparel?.DropAll(pos,false,false);p.equipment?.DropAllEquipment(pos,false);}
   if(!ResurrectionUtility.TryResurrect(p))return false;
   var home=Find.AnyPlayerHomeMap;if(home!=null && p.Spawned && p.Map!=home){p.DeSpawn();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(home.Center,home,8),home);}
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("克隆体","「"+p.LabelShort+"」死去了，灵魂随即移入了生长缸里的克隆体：技能、等级、职业和特性都在，装备留在了死去的地方。",LetterDefOf.PositiveEvent,p);
   return true;
  }
  // ---------- 9 环 ----------
  public static int TimeStop(Pawn p,int ticks){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var x in map.mapPawns.AllPawnsSpawned.ToList())if(x!=p && !x.Dead){x.stances?.stunner?.StunFor(ticks,p,false,false);n++;}
   Items.Give(p,"LP_WS_TimeStop",ticks,1f);Items.Mote(p,"时间停止",C);return n;
  }
  public static void PowerWordKill(Pawn p,Pawn t,float amount){
   if(ClassSubclass.Bossy(t)){WizardSpells.Hit(p,t,Psychic,amount,1f);Items.Mote(t,"强力真言",C);return;}
   if(t.health.summaryHealth.SummaryHealthPercent<=.5f){t.Kill(new DamageInfo(Psychic,9999f,1f,-1f,p));Items.Mote(t,"死",C);return;}
   WizardSpells.Hit(p,t,Psychic,amount*.3f,1f);Items.Mote(t,"还不够虚弱",C);
  }
  // 祈愿术：免费施放 8 环以下的法师法术 / 全体痊愈 / 复活一人 / 传说魔法物品；后两项 33% 反噬（再 33% 永久失去祈愿术）。
  public const float WishStress=.33f,WishLose=.33f;
  public static float? WishRoll;
  public static void Wish(Pawn p){
   var opts=new List<(string,string,Action)>{
    ("免费施放一个 8 环以下的法师法术",null,()=>WishSpell(p)),
    ("地图上全体殖民者痊愈",null,()=>WishHeal(p)),
    ("复活一名殖民者（33% 反噬）",null,()=>WishRevive(p)),
    ("召来一件传说品质的魔法物品（33% 反噬）",null,()=>{var it=MagicItem(QualityCategory.Legendary);if(it!=null && p.Spawned)GenPlace.TryPlaceThing(it,p.Position,p.Map,ThingPlaceMode.Near);Items.Mote(p,"祈愿：传说物品",C);Backlash(p);})};
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){WishHeal(p);return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("祈愿术","许下一个愿望。",opts));
  }
  static void WishSpell(Pawn p){
   var list=Spellbook.New.Concat(DefDatabase<AbilityDef>.AllDefsListForReading.Where(a=>a.defName.StartsWith("LP_") && Spellbook.IsSpell(a) && Spellbook.Ext(a)==null)).Where(a=>Spellbook.Circle(a)>=1 && Spellbook.Circle(a)<=8 && a.defName!="LP_WS_Wish").Distinct().OrderBy(Spellbook.Circle).ThenBy(a=>a.label).ToList();
   Find.WindowStack.Add(new Dialog_SpellChoice("祈愿：施放法术","选一个法术，免费施放。",list.Select(a=>("【"+Spellbook.CircleName(Spellbook.Circle(a))+"】"+a.label,a.description,(Action)(()=>FreeCast(p,a)))).ToList(),()=>{}));
  }
  public static void FreeCast(Pawn p,AbilityDef a){
   var props=a.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault();if(props==null)return;
   void Go(LocalTargetInfo t){var why=ClassEffects.Invalid(p,props,t);if(why!=null){Messages.Message(why,MessageTypeDefOf.RejectInput,false);return;}Spellbook.EndCast();ClassEffects.Apply(p,a,props,t);Items.Mote(p,"祈愿："+a.label,C);}
   if(!a.targetRequired){Go(new LocalTargetInfo(p));return;}
   Find.Targeter.BeginTargeting(a.verbProperties?.targetParams??new TargetingParameters(),Go,p);
  }
  public static int WishHeal(Pawn p){
   var map=p.Map;if(map==null)return 0;int n=0;
   foreach(var x in map.mapPawns.FreeColonistsSpawned.ToList()){for(int i=0;i<40;i++){if(HealthUtility.FixWorstHealthCondition(x)==null)break;n++;}}
   Items.Mote(p,"祈愿：痊愈",C);return n;
  }
  static void WishRevive(Pawn p){
   var corpses=p.Map?.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>().Where(c=>c.InnerPawn!=null && c.InnerPawn.Faction==Faction.OfPlayer && c.InnerPawn.RaceProps.Humanlike).ToList()??new List<Corpse>();
   if(corpses.Count==0){Messages.Message("地图上没有殖民者的尸体。",MessageTypeDefOf.RejectInput,false);return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("祈愿：复活","选一具殖民者的尸体。",corpses.Select(c=>(c.InnerPawn.LabelShort,(string)null,(Action)(()=>{if(ResurrectionUtility.TryResurrect(c.InnerPawn))Items.Mote(c.InnerPawn,"复活",C);Backlash(p);}))).ToList(),()=>{}));
  }
  public static string Backlash(Pawn p){
   if((WishRoll??Rand.Value)>=WishStress)return "none";
   Items.Give(p,"LP_WS_WishStress",180000,1f);string r="stress";
   if(Rand.Chance(WishLose)){var d=Spellbook.Data(p);var w=DefCache<AbilityDef>.Get("LP_WS_Wish");if(d!=null && w!=null){d.wishLost=true;d.book.Remove(w);d.prepared.Remove(w);var ab=p.abilities?.GetAbility(w);if(ab!=null)p.abilities.RemoveAbility(w);}r="lost";}
   if(Spellbook.Player(p) && !Adventurer.TestSuppressed)Messages.Message(p.LabelShort+"承受了祈愿的反噬"+(r=="lost"?"，并且永远失去了祈愿术":"")+"。",p,MessageTypeDefOf.NegativeEvent,false);
   return r;
  }
  static readonly string[] Fiends={"LP_AH_Hezrou","LP_HL_ChainDevil","LP_AH_Dretch"};
  public static Pawn GateFiend(Pawn p,int ticks){
   var map=p.Map;if(map==null)return null;
   foreach(var n in Fiends){
    var kind=DefDatabase<PawnKindDef>.GetNamedSilentFail(n);if(kind==null)continue;Pawn f;
    try{f=PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind,Faction.OfPlayer,forceGenerateNewPawn:true,canGeneratePawnRelations:false,mustBeCapableOfViolence:true));}catch(Exception ex){Log.Warning("[DND边缘大陆] 异界之门："+ex.Message);continue;}
    GenSpawn.Spawn(f,CellFinder.RandomClosewalkCellNear(p.Position,map,3),map);var h=(Hediff_WS)Items.Give(f,"LP_WS_Summoned",ticks,1f);if(h!=null)h.caster=p;
    FleckMaker.ThrowDustPuffThick(f.DrawPos,map,3f,new Color(.9f,.35f,.3f));Items.Mote(f,"从门里走出",C);return f;
   }
   return null;
  }
  public static bool GateCaravan(Pawn p){
   var map=p.Map;if(map==null)return false;var f=Find.FactionManager.AllFactionsVisible.Where(x=>!x.IsPlayer && !x.HostileTo(Faction.OfPlayer) && x.def.caravanTraderKinds!=null && x.def.caravanTraderKinds.Count>0).RandomElementWithFallback();if(f==null)return false;
   var parms=new IncidentParms{target=map,faction=f,points=StorytellerUtility.DefaultThreatPointsNow(map),spawnCenter=p.Position,forced=true};
   return IncidentDefOf.TraderCaravanArrival.Worker.TryExecute(parms);
  }
  static void Gate(Pawn p,int ticks){
   if(!Spellbook.Player(p) || Adventurer.TestSuppressed){GateFiend(p,ticks);return;}
   Find.WindowStack.Add(new Dialog_SpellChoice("异界之门","门已经打开，选一样东西从门里过来。",new List<(string,string,Action)>{("召一个恶魔 / 魔鬼为殖民地作战 1 小时",null,()=>GateFiend(p,ticks)),("让一支友善派系的商队从门里走出来",null,()=>{if(!GateCaravan(p))Messages.Message("没有能来的商队。",MessageTypeDefOf.RejectInput,false);})}));
  }

  // ---------- 商队按钮：异界传送、魔法宅邸 ----------
  public static IEnumerable<Gizmo> CaravanGizmos(Caravan c){
   foreach(var p in c.PawnsListForReading.ToList()){
    var h=Adventurer.Get(p);if(Spellbook.Level(h)<=0)continue;
    foreach(var (n,label,cost) in new[]{("LP_WS_PlaneShift","异界传送：全队回到殖民地",5),("LP_WS_MagnificentMansion","魔法宅邸：8 小时不遇袭、休整",5)}){
     var def=DefCache<AbilityDef>.Get(n);if(def==null || p.abilities?.GetAbility(def)==null)continue;
     float mana=ClassSystem.Resource(h,Spellbook.Wizard);string why=mana+.001f<cost?"法力不足（"+mana.ToString("0.#")+" / "+cost+"）":n=="LP_WS_PlaneShift" && Find.AnyPlayerHomeMap==null?"没有殖民地可以回去":null;
     var pawn=p;var caravan=c;
     yield return new Command_Action{defaultLabel=label,defaultDesc=pawn.LabelShort+"：消耗 "+cost+" 点法力。"+def.description,icon=def.uiIcon,Disabled=why!=null,disabledReason=why,
      action=()=>{ClassSystem.AddResource(Adventurer.Get(pawn),Spellbook.Wizard,-cost);if(n=="LP_WS_PlaneShift")ShiftHome(caravan);else foreach(var x in caravan.PawnsListForReading.ToList())if(x.RaceProps.Humanlike)Mansion(x,20000);}};
    }
   }
  }
  public static bool ShiftHome(Caravan c){var home=Find.AnyPlayerHomeMap;if(home==null || c==null)return false;CaravanEnterMapUtility.Enter(c,home,CaravanEnterMode.Center,CaravanDropInventoryMode.DoNotDrop);return true;}
  public static bool MansionCaravan(Caravan c)=>c!=null && c.PawnsListForReading.Any(x=>Items.Has(x,"LP_WS_Mansion"));
 }

 // 放逐 / 迷宫放逐 / 异界传送：暂时离开地图（世界小人，到时回原地）或永久送走（连同装备消失）。
 public static class Banishment {
  static GameComponent_Spellbook G=>Current.Game?.GetComponent<GameComponent_Spellbook>();
  public static void Reset(){}
  public static bool IsBanished(Pawn p)=>p!=null && G?.banished.Contains(p)==true;
  public static void Remove(Pawn t,string why){
   if(t==null || t.Dead || t.Destroyed)return;if(t.Spawned)FleckMaker.ThrowDustPuffThick(t.DrawPos,t.Map,2f,new Color(.7f,.5f,1f));Items.Mote(t,why,new Color(.7f,.5f,1f));
   t.GetLord()?.Notify_PawnLost(t,PawnLostCondition.Incapped,null);t.Destroy();
  }
  public static bool Banish(Pawn t,int ticks,string why){
   var g=G;if(g==null || t==null || !t.Spawned || t.Dead)return false;
   g.banished.Add(t);g.banishTick.Add(Find.TickManager.TicksGame+ticks);g.banishMap.Add(t.Map.uniqueID);g.banishCell.Add(t.Position);
   FleckMaker.ThrowDustPuffThick(t.DrawPos,t.Map,2f,new Color(.7f,.5f,1f));Items.Mote(t,why,new Color(.7f,.5f,1f));
   t.GetLord()?.Notify_PawnLost(t,PawnLostCondition.Incapped,null);t.jobs?.StopAll();t.DeSpawn();
   if(!Find.WorldPawns.Contains(t))Find.WorldPawns.PassToWorld(t,PawnDiscardDecideMode.KeepForever);
   return true;
  }
  public static void Tick(GameComponent_Spellbook g){for(int i=g.banished.Count-1;i>=0;i--)if(Find.TickManager.TicksGame>=g.banishTick[i])ReturnAt(g,i);}
  public static void Return(Pawn p){var g=G;if(g==null)return;int i=g.banished.IndexOf(p);if(i>=0)ReturnAt(g,i);}
  static void ReturnAt(GameComponent_Spellbook g,int i){
   var p=g.banished[i];var mapId=g.banishMap[i];var cell=g.banishCell[i];g.banished.RemoveAt(i);g.banishTick.RemoveAt(i);g.banishMap.RemoveAt(i);g.banishCell.RemoveAt(i);
   var map=Find.Maps.FirstOrDefault(m=>m.uniqueID==mapId);if(p==null || p.Dead || p.Destroyed || map==null)return;
   if(Find.WorldPawns.Contains(p))Find.WorldPawns.RemovePawn(p);
   var c=cell.InBounds(map) && cell.Standable(map)?cell:CellFinder.StandableCellNear(cell,map,6f);if(!c.IsValid)c=CellFinder.RandomClosewalkCellNear(map.Center,map,10);
   GenSpawn.Spawn(p,c,map);FleckMaker.ThrowDustPuffThick(p.DrawPos,map,2f,new Color(.7f,.5f,1f));Items.Mote(p,"回来了",new Color(.7f,.5f,1f));
   if(p.Faction!=null && p.HostileTo(Faction.OfPlayer) && p.RaceProps.Humanlike)LordMaker.MakeNewLord(p.Faction,new LordJob_AssaultColony(p.Faction),map,new[]{p});
  }
  public static void Clean(GameComponent_Spellbook g){int n=Mathf.Min(g.banished.Count,Mathf.Min(g.banishTick.Count,Mathf.Min(g.banishMap.Count,g.banishCell.Count)));
   for(int i=g.banished.Count-1;i>=0;i--)if(i>=n || g.banished[i]==null){g.banished.RemoveAt(i);if(i<g.banishTick.Count)g.banishTick.RemoveAt(i);if(i<g.banishMap.Count)g.banishMap.RemoveAt(i);if(i<g.banishCell.Count)g.banishCell.RemoveAt(i);}}
 }

 // ==================== Harmony ====================
 // 反制法术：20 格内有人开着反制姿态时，敌方的能力（职业能力、心灵能力……）施放前被打断。
 [HarmonyPatch(typeof(Ability),nameof(Ability.Activate),new[]{typeof(LocalTargetInfo),typeof(LocalTargetInfo)})]
 public static class WizardCounterspell {
  public static float? RollOverride;
  public static int LevelOf(AbilityDef d){int c=Spellbook.Circle(d);if(c>=0)return c;return d.level>0?d.level:3;}
  public static float Chance(AbilityDef d)=>Mathf.Clamp01(1f-.15f*Mathf.Max(0,LevelOf(d)-3));
  [HarmonyPriority(Priority.High)]
  static bool Prefix(Ability __instance,ref bool __result){
   if(Hediff_WS.Counterspellers.Count==0)return true;var caster=__instance?.pawn;if(caster==null || !caster.Spawned)return true;
   foreach(var c in Hediff_WS.Counterspellers.ToList()){
    if(c==null || c.Dead || !c.Spawned || c.Map!=caster.Map || c.Downed || !c.HostileTo(caster) || c.Position.DistanceTo(caster.Position)>20f || !Items.Has(c,"LP_WS_Counterspell"))continue;
    Items.Remove(c,"LP_WS_Counterspell");
    if((RollOverride??Rand.Value)>=Chance(__instance.def)){Items.Mote(c,"反制失败",new Color(.7f,.5f,1f));return true;}
    Items.Mote(caster,"被反制："+__instance.def.label,new Color(.7f,.5f,1f));Items.Mote(c,"反制法术",new Color(.7f,.5f,1f));
    __instance.StartCooldown(__instance.def.cooldownTicksRange.RandomInRange);__result=false;return false;
   }
   return true;
  }
 }
 // 变形（兔形）和弱智术：心灵能力也放不了。
 [HarmonyPatch(typeof(Ability),nameof(Ability.CanCast),MethodType.Getter)]
 public static class WizardNoCasting {
  static void Postfix(Ability __instance,ref AcceptanceReport __result){
   if(!__result.Accepted || __instance?.pawn==null || Hediff_Feebleminded.Live<=0 && Hediff_WS.Live<=0)return;var p=__instance.pawn; // 1.131.0 性能热修：没人被弱智 / 变形时不扫状态表（所有人的所有能力 CanCast 都走这里）
   if(Hediff_Feebleminded.Live>0 && Items.Has(p,"LP_WS_Feebleminded"))__result="被弱智术打碎了心智";else if(Hediff_WS.Live>0 && Items.Has(p,"LP_WS_Polymorphed"))__result="变成兔子了";
  }
 }
 // 心灵屏障：免疫精神崩溃和心智控制（任何精神状态都起不来）。
 [HarmonyPatch(typeof(MentalStateHandler),nameof(MentalStateHandler.TryStartMentalState))]
 public static class WizardMindBlank {
  static bool Prefix(Pawn ___pawn,ref bool __result){if(Hediff_WS.Live<=0 || !Items.Has(___pawn,"LP_WS_MindBlank"))return true;__result=false;return false;}
 }
 // 魔法宅邸：商队 8 小时不遇袭。
 [HarmonyPatch(typeof(IncidentWorker),nameof(IncidentWorker.TryExecute))]
 public static class WizardMansionNoAmbush {
  static bool Prefix(IncidentWorker __instance,IncidentParms parms,ref bool __result){
   if(!(parms?.target is Caravan c) || __instance.def==null)return true;var cat=__instance.def.category;
   if((cat==IncidentCategoryDefOf.ThreatBig || cat==IncidentCategoryDefOf.ThreatSmall) && (WizardSpells2.MansionCaravan(c) || CasterSpells.TraceBlocks(c))){__result=false;return false;} /* 1.131.0 无踪步：遇袭几率减半 */
   return true;
  }
 }
 // 商队按钮。
 [HarmonyPatch(typeof(Caravan),nameof(Caravan.GetGizmos))]
 public static class WizardCaravanGizmos {
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Caravan __instance){foreach(var g in __result)yield return g;if(__instance.Faction==Faction.OfPlayer)foreach(var g in WizardSpells2.CaravanGizmos(__instance))yield return g;}
 }
 // 克隆体：法师死后下一刻在克隆体里醒来。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class WizardCloneDeath {
  static void Postfix(Pawn __instance){if(__instance!=null && __instance.Dead && WizardSpells2.CloneReady(__instance)){var g=Current.Game?.GetComponent<GameComponent_Spellbook>();if(g!=null && !g.clonePending.Contains(__instance))g.clonePending.Add(__instance);}}
 }
}
