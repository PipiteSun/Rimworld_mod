using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.58.0 传说事迹（青色，规格第一节第 6 条）：只给殖民者，由事件授予，不占名额。“参与击杀”沿用 1.48.0 的伤害分成：分到 ≥30% 或最后一击。
 // 奖励：已是传奇冒险者给传奇属性点，否则 +1 次待分配属性提升（绝境英雄固定 +1 次属性提升）。
 public static class HeroDeeds {
  public const float Share=.3f,SparkChance=.25f;
  public const int DragonPoints=5,GiantPoints=3,LichPoints=3,GodPoints=5,MindPoints=3,ReturnPoints=2,SparkFallback=3,GiantKillsNeed=3,BlastsNeed=3,GodslayerLevel=5;
  public static float? TestRoll;static float Roll=>TestRoll??Rand.Value;
  public static int Granted; // 自检计数
  public static int Points(TraitDef d){
   if(d==HeroDefOf.LP_DeedDragonslayer)return DragonPoints;if(d==HeroDefOf.LP_Giantbane)return GiantPoints;if(d==HeroDefOf.LP_Lichbane)return LichPoints;
   if(d==HeroDefOf.LP_DeedGodslayer)return GodPoints;if(d==HeroDefOf.LP_MindFortress)return MindPoints;if(d==HeroDefOf.LP_DeathReturned)return ReturnPoints;return 0;
  }
  static bool Player(Pawn p)=>p!=null && !p.Dead && p.Faction!=null && p.Faction.IsPlayer && p.RaceProps.Humanlike && p.story?.traits!=null;
  public static bool Grant(Pawn p,TraitDef d,string why){
   if(!Player(p) || d==null || HeroTraits.Has(p,d) || !HeroTraits.IsDeed(d))return false;
   p.story.traits.GainTrait(new Trait(d,0,true),true);Granted++;
   var h=Adventurer.Get(p);string reward;
   if(d==HeroDefOf.LP_LastStand){
    if(h!=null){h.pendingASI++;if(Adventurer.AutoASI(p))h.AutoAssign();}
    reward="+1 次属性提升";
    if(Roll<SparkChance){
     var slot=HeroTraits.Full(p)?HeroTraits.SlotTrait(p):null; // 1.66.0 热修：名额满了才会取代最早的一个
     if(HeroTraits.Has(p,HeroDefOf.LP_HeroicSpark)){Legend.Grant(h,SparkFallback);reward+="；绝境里迸出了英雄之资的火花——但"+p.LabelShort+"已经有英雄之资，改为传奇属性点 +"+SparkFallback;}
     else{HeroTraits.Give(p,HeroDefOf.LP_HeroicSpark);reward+="；绝境之中，"+p.LabelShort+"觉醒了英雄之资！"+(slot!=null?"（强力特性已满，取代了"+HeroTraits.Plain(slot)+"）":"");}
    }
   }else{
    int n=Points(d);
    if(h!=null && h.legendary){Legend.Grant(h,n);reward="传奇属性点 +"+n;}
    else if(h!=null){h.pendingASI++;if(Adventurer.AutoASI(p))h.AutoAssign();reward="+1 次属性提升（成为传奇冒险者之后，这份事迹相当于 "+n+" 个传奇属性点）";}
    else reward="（没有冒险者等级，没有加点奖励）";
   }
   if(d==HeroDefOf.LP_DeedGodslayer && Faith.ShardDef!=null && p.MapHeld!=null)GenPlace.TryPlaceThing(ThingMaker.MakeThing(Faith.ShardDef),p.PositionHeld,p.MapHeld,ThingPlaceMode.Near);
   h?.Dirty();
   if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("传说事迹："+HeroTraits.Plain(d),p.LabelShort+why+"，成为了"+HeroTraits.Plain(d)+"。\n\n"+(p.story.traits.GetTrait(d)?.TipString(p)??"")+"\n\n奖励："+reward+(d==HeroDefOf.LP_DeedGodslayer?"；1 枚神迹残片":"")+"。",LetterDefOf.PositiveEvent,p);
   return true;
  }

  // ---------- 击杀 ----------
  public static List<Pawn> Participants(Pawn victim,HeroKillState s){
   var list=new List<Pawn>();
   if(s.dmg!=null){float total=s.dmg.Values.Sum();if(total>0f)foreach(var kv in s.dmg)if(kv.Value/total>=Share-.0001f && Player(kv.Key))list.Add(kv.Key);}
   if(Player(s.killer) && !list.Contains(s.killer))list.Add(s.killer);
   return list;
  }
  static bool MeleeBy(Pawn p,HeroKillState s)=>p==s.killer?s.killerMelee:(p.equipment?.Primary==null || p.equipment.Primary.def.IsMeleeWeapon);
  public static bool DragonDeed(Pawn v)=>Dragons.IsChromatic(v) || !v.RaceProps.Humanlike && Dragons.Is(v) || ForceDragon.Active(v) && HeroDestiny.Boss(v);
  public static bool DivineFoe(Pawn v)=>Twilight.Vessel(v)!=null || Demigods.Is(v) || Twilight.Infused(v)!=null && Legendary.LevelOf(v)>=GodslayerLevel;
  public static void OnKilled(Pawn victim,HeroKillState s){
   var parts=Participants(victim,s);
   if(parts.Count>0){
    var vt=DndCreatureType.Compute(victim);
    bool dragon=DragonDeed(victim),giant=(vt&DndType.Giant)!=0,larloch=Items.Has(victim,"LP_LarlochAvatar"),divine=DivineFoe(victim),nemesis=Adventurer.Get(victim)?.nemesis==true,boss=HeroDestiny.Boss(victim);
    foreach(var p in parts){
     if(dragon)Grant(p,HeroDefOf.LP_DeedDragonslayer,"参与击杀了"+victim.LabelShort);
     if(giant && MeleeBy(p,s)){var r=HeroTraits.EnsureRecord(p);if(r!=null){r.giantKills++;if(r.giantKills>=GiantKillsNeed)Grant(p,HeroDefOf.LP_Giantbane,"用近战放倒了第 "+r.giantKills+" 个巨人（"+victim.LabelShort+"）");}}
     if(larloch)Grant(p,HeroDefOf.LP_Lichbane,"击败了影巫王拉洛克");
     if(divine)Grant(p,HeroDefOf.LP_DeedGodslayer,"参与击杀了"+victim.LabelShort);
     var i=HeroTraits.Info(p);var rec=i?.rec;
     if(i!=null && i.prophesied && rec!=null && !rec.prophecyDone){
      bool hit=rec.prophecy==0 && dragon || rec.prophecy==1 && giant && boss || rec.prophecy==2 && larloch || rec.prophecy==3 && nemesis;
      if(hit)HeroDestiny.Fulfill(p,p.LabelShort+"参与击杀了"+victim.LabelShort);
     }
     if(i!=null && i.favored>0 && rec!=null && HeroTraits.FavoredTarget(i,victim)){
      rec.favoredKills++;
      if(!rec.favoredBane && rec.favoredKills>=HeroCombat.BaneKills){rec.favoredBane=true;if(!Adventurer.TestSuppressed)Find.LetterStack.ReceiveLetter("宿敌克星："+p.LabelShort,p.LabelShort+"已经杀了 "+rec.favoredKills+" 个宿敌（"+HeroTraits.FavoredLabels[i.favored]+"），成为“宿敌克星”：对宿敌伤害 +40%，免疫宿敌施加的眩晕与控制。",LetterDefOf.PositiveEvent,p);}
     }
    }
   }
   // 邪契者·邪魔：击杀获得临时伤害吸收 15 + 等级（最多 60）。
   var k=s.killer;var ki=HeroTraits.Info(k);
   if(k!=null && k!=victim){Growth.OnKill(k,victim);Growth.Aid(victim,k);} // 1.58.3 传承武器绑定、派系声望（援助）
   Growth.OwnerDied(victim);
   if(ki!=null && k!=victim)Feats2.Killed(k,ki,s.killerMelee); // 1.58.1 嗜血再生
   if(ki!=null && ki.pact==1 && k!=victim && !k.Dead && HeroDestiny.VigorDef!=null)Vigor(k);
  }
  public static Hediff Vigor(Pawn k){
   var d=HeroDestiny.VigorDef;if(d==null || k?.health==null)return null;int lv=Legendary.LevelOf(k);
   var h=k.health.hediffSet.GetFirstHediffOfDef(d);if(h==null){h=HediffMaker.MakeHediff(d,k);h.Severity=Mathf.Min(HeroDestiny.VigorMax,HeroDestiny.VigorBase+lv);k.health.AddHediff(h);}
   else{h.Severity=Mathf.Min(HeroDestiny.VigorMax,h.Severity+HeroDestiny.VigorBase+lv);var t=(h as HediffWithComps)?.TryGetComp<HediffComp_Disappears>();if(t!=null)t.ticksToDisappear=t.Props.disappearsAfterTicks.max;}
   return h;
  }

  // ---------- 其他事件 ----------
  public static void Blasted(Pawn p){
   if(!Player(p))return;var r=HeroTraits.EnsureRecord(p);if(r==null)return;
   r.blastSurvived++;if(r.blastSurvived>=BlastsNeed)Grant(p,HeroDefOf.LP_MindFortress,"扛过了 "+r.blastSurvived+" 次心灵震爆");
  }
  public static void DominationEnded(Pawn p){if(Player(p))Grant(p,HeroDefOf.LP_MindFortress,"从夺心魔的支配里挣脱了出来");}
  public static void Resurrected(Pawn p){if(Player(p))Grant(p,HeroDefOf.LP_DeathReturned,"从冥界回来了");}
  public static void PhylacteryDestroyed(Phylactery ph){
   var soul=ph?.soul;if(soul==null || soul.Faction==null || soul.Faction.IsPlayer || !soul.Faction.HostileTo(Faction.OfPlayer))return;
   if(!HeroCombat.lastPhylacteryHit.TryGetValue(ph,out var p))return;HeroCombat.lastPhylacteryHit.Remove(ph);
   Grant(p,HeroDefOf.LP_Lichbane,"摧毁了"+soul.LabelShort+"的命匣");
  }
  // 冥界归来：亲友 / 同伴死去、目睹死亡之类的负面记忆。
  static readonly Dictionary<ThoughtDef,bool> deathThoughts=new Dictionary<ThoughtDef,bool>();
  public static bool DeathThought(ThoughtDef d){
   if(d==null)return false;if(deathThoughts.TryGetValue(d,out bool b))return b;
   var n=d.defName;b=(n.Contains("Died") || n.Contains("Death") || n.Contains("Dead")) && d.stages!=null && d.stages.Any(s=>s!=null && s.baseMoodEffect<0f);
   return deathThoughts[d]=b;
  }

  // ---------- 绝境英雄：袭击中某一刻殖民地只剩一个人能战斗（250 刻采样），袭击被守住时授予 ----------
  static bool CanFight(Pawn c)=>!c.Dead && !c.Downed && !c.InMentalState && !c.WorkTagIsDisabled(WorkTags.Violent);
  public static void SampleRaids(List<RaidRecord> raids){
   foreach(var r in raids){
    if(r?.map==null || r.lastStand!=null)continue;var cols=r.map.mapPawns.FreeColonistsSpawned;
    int fighters=0;for(int k=0;k<cols.Count;k++)if(!cols[k].WorkTagIsDisabled(WorkTags.Violent))fighters++;
    if(fighters<2)continue;
    Pawn lone=null;int able=0;
    for(int k=0;k<cols.Count;k++){if(!CanFight(cols[k]))continue;able++;lone=cols[k];if(able>1)break;}
    if(able==1)r.lastStand=lone;
   }
  }
  public static void RaidDefended(RaidRecord r){
   if(r==null)return;
   var lone=r.lastStand;if(lone!=null && Player(lone) && lone.MapHeld==r.map)Grant(lone,HeroDefOf.LP_LastStand,"在袭击里一个人撑到了最后");
   foreach(var p in r.fighters.Where(Player).Distinct().ToList()){
    var i=HeroTraits.Info(p);var rec=i?.rec;if(i==null || !i.prophesied || rec==null || rec.prophecyDone || rec.prophecy!=5)continue;
    rec.raidsDefended++;if(rec.raidsDefended>=HeroDestiny.ProphecyRaids)HeroDestiny.Fulfill(p,p.LabelShort+"守住了第 "+rec.raidsDefended+" 次袭击");
   }
  }
 }
}
