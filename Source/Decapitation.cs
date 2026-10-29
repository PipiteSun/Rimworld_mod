using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace LichPhylactery {
 // 1.19.0 斩首：袭击生成时把成员分成领队和随从（存档保存）。领队死亡或倒地，随从按种类反应：
 // 死灵造物崩解；符印巨魔狂暴（设置关闭则逃跑）；被支配奴仆清醒倒地、脱离派系；其余士气崩溃逃跑。
 public static class Decapitation {
  public static DecapitationTracker Tracker=>GameComp<DecapitationTracker>.Get();
  public static void Assign(List<Pawn> group,Faction faction)=>Tracker?.Assign(group,faction);
  public static Pawn LeaderOf(Pawn p)=>Tracker?.LeaderOf(p);
  public enum Reaction {None,Collapse,Berserk,Flee,Freed}
  static HediffDef awakening;
  public static HediffDef Awakening=>awakening??(awakening=DefDatabase<HediffDef>.GetNamedSilentFail("LP_ThrallAwakening"));
  public static bool IsTroll(Pawn p)=>Troll.Is(p);
  public static Reaction ReactionFor(Pawn p){
   if(p==null || p.Dead)return Reaction.None;
   if(Necro.Construct(p)!=null)return Reaction.Collapse;
   if(FactionGroups.IsThralled(p))return Reaction.Freed;
   if(IsTroll(p))return LichMod.Settings.decapTrollBerserk?Reaction.Berserk:Reaction.Flee;
   return Reaction.Flee;
  }
  public static Reaction React(Pawn p){
   var r=ReactionFor(p);
   switch(r){
    case Reaction.Collapse:
     if(!p.Dead){if(p.Spawned)FleckMaker.ThrowDustPuffThick(p.DrawPos,p.Map,2f,new UnityEngine.Color(.8f,.85f,.75f));p.Kill(null);}
     break;
    case Reaction.Berserk:
     if(p.Downed || !p.Spawned || !(p.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Berserk,"主人倒下，符印失控",forced:true,forceWake:true)??false))r=Flee(p)?Reaction.Flee:Reaction.None;
     break;
    case Reaction.Freed:
     Free(p);break;
    case Reaction.Flee:
     if(!Flee(p))r=Reaction.None;break;
   }
   return r;
  }
  static bool Flee(Pawn p){
   if(p.Dead || p.Downed || !p.Spawned)return false;
   if(p.InMentalState && p.MentalStateDef==MentalStateDefOf.PanicFlee)return true;
   return p.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.PanicFlee,"领队倒下，士气崩溃",forced:true,forceWake:true)??false;
  }
  // 被支配奴仆：支配解除，倒地，脱离派系（不再敌对，可以俘虏）。
  public static void Free(Pawn p){
   var h=p.health.hediffSet.GetFirstHediffOfDef(FactionGroups.Thralled);if(h!=null)p.health.RemoveHediff(h);
   if(p.mindState?.mentalStateHandler?.CurState!=null)p.mindState.mentalStateHandler.CurState.RecoverFromState();
   if(p.Faction!=null && !p.Faction.IsPlayer)p.SetFaction(null);
   if(Awakening!=null && !p.health.hediffSet.HasHediff(Awakening))p.health.AddHediff(Awakening);
   p.jobs?.StopAll();
  }
 }

 public class DecapitationTracker : GameComponent {
  public List<Pawn> followers=new List<Pawn>(),leaders=new List<Pawn>();
  public DecapitationTracker(Game game){}
  public Pawn LeaderOf(Pawn p){int i=followers.IndexOf(p);return i<0?null:leaders[i];}
  public List<Pawn> FollowersOf(Pawn leader){var r=new List<Pawn>();for(int i=0;i<leaders.Count;i++)if(leaders[i]==leader && followers[i]!=null)r.Add(followers[i]);return r;}
  public void Bind(Pawn follower,Pawn leader){
   if(follower==null || leader==null || follower==leader)return;
   int i=followers.IndexOf(follower);if(i>=0)leaders[i]=leader;else{followers.Add(follower);leaders.Add(leader);}
  }
  // 绑定在法师名下的造物跟随自己的法师；其余随从轮流分给各领队，每人不超过上限。
  public void Assign(List<Pawn> group,Faction faction){
   var ext=Factions.Ext(faction);if(ext==null || ext.leaderKinds.NullOrEmpty() || group==null)return;
   var heads=group.Where(p=>p!=null && ext.leaderKinds.Contains(p.kindDef)).ToList();if(heads.Count==0)return;
   var count=heads.ToDictionary(h=>h,h=>0);int next=0;
   foreach(var p in group){
    if(p==null || heads.Contains(p))continue;
    var c=Necro.Construct(p);
    if(c!=null && c.raidBound && c.master!=null && heads.Contains(c.master)){Bind(p,c.master);continue;}
    if(!ext.followerKinds.Contains(p.kindDef))continue;
    for(int tries=0;tries<heads.Count;tries++){
     var h=heads[(next+tries)%heads.Count];
     if(count[h]<ext.followersPerLeader){Bind(p,h);count[h]++;next=(next+tries+1)%heads.Count;break;}
    }
   }
  }
  public override void GameComponentTick(){if(Find.TickManager.TicksGame%60==23 && leaders.Count>0)Process();}
  public int Process(){
   int reacted=0;
   Prune();
   foreach(var leader in leaders.Distinct().ToList()){
    if(leader==null || !(leader.Dead || leader.Downed || leader.Destroyed))continue;
    reacted+=Trigger(leader);
   }
   return reacted;
  }
  public int Trigger(Pawn leader){
   var list=FollowersOf(leader);int collapsed=0,berserk=0,fled=0,freed=0;
   for(int i=leaders.Count-1;i>=0;i--)if(leaders[i]==leader){leaders.RemoveAt(i);followers.RemoveAt(i);}
   foreach(var p in list){
    switch(Decapitation.React(p)){case Decapitation.Reaction.Collapse:collapsed++;break;case Decapitation.Reaction.Berserk:berserk++;break;case Decapitation.Reaction.Flee:fled++;break;case Decapitation.Reaction.Freed:freed++;break;}
   }
   int total=collapsed+berserk+fled+freed;
   if(total>0 && (leader.MapHeld?.IsPlayerHome==true || leader.Corpse?.MapHeld?.IsPlayerHome==true)){
    var parts=new List<string>();
    if(collapsed>0)parts.Add(collapsed+"具死灵造物崩解");
    if(berserk>0)parts.Add(berserk+"个符印巨魔失控狂暴");
    if(freed>0)parts.Add(freed+"名奴仆从支配中醒来、瘫倒在地");
    if(fled>0)parts.Add(fled+"名随从士气崩溃、四散奔逃");
    Messages.Message(leader.LabelShort+(leader.Dead?"死了":"倒下了")+"："+string.Join("，",parts)+"。",new LookTargets(list.Where(p=>p.SpawnedOrAnyParentSpawned)),MessageTypeDefOf.PositiveEvent,false);
   }
   return total;
  }
  static bool Saveable(Pawn p)=>p!=null && !p.Destroyed && !p.Discarded && (p.SpawnedOrAnyParentSpawned || Find.WorldPawns.Contains(p) || p.IsCaravanMember());
  public void Prune(){for(int i=followers.Count-1;i>=0;i--){var f=followers[i];var l=leaders[i];if(f==null || l==null || f.Destroyed || f.Dead || f.Discarded){followers.RemoveAt(i);leaders.RemoveAt(i);}}}
  public override void ExposeData(){
   if(Scribe.mode==LoadSaveMode.Saving){Prune();for(int i=followers.Count-1;i>=0;i--)if(!Saveable(followers[i]) || !Saveable(leaders[i])){followers.RemoveAt(i);leaders.RemoveAt(i);}}
   Scribe_Collections.Look(ref followers,"decapFollowers",LookMode.Reference);
   Scribe_Collections.Look(ref leaders,"decapLeaders",LookMode.Reference);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){
    if(followers==null)followers=new List<Pawn>();if(leaders==null)leaders=new List<Pawn>();
    if(followers.Count!=leaders.Count){followers.Clear();leaders.Clear();}
    Prune();
   }
  }
 }
}
