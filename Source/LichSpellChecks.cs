using System;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 public partial class LichChecks {
  static float TotalExperience(int lvl,float exp){float sum=exp;for(int i=1;i<lvl;i++)sum+=Necro.Requirement(i);return sum;}
  Corpse CorpseAt(IntVec3 offset){Pawn p=Person();p.SetFactionDirect(null);GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(altar.InteractionCell+offset,map,2),map);p.Kill(null);var corpse=p.Corpse;corpse.SetForbidden(false,false);return corpse;}
  Pawn Foe(IntVec3 near){
   var faction=Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f=>!f.IsPlayer && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer))??Find.FactionManager.OfAncientsHostile;
   Pawn p=Person();for(int attempt=0;attempt<20 && p.GetStatValue(StatDefOf.PsychicSensitivity)<=0;attempt++){p.Destroy();p=Person();}
   p.SetFactionDirect(faction);GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(near,map,1),map);return p;
  }
  Pawn Ally(IntVec3 near){Pawn p=Person();GenSpawn.Spawn(p,CellFinder.RandomClosewalkCellNear(near,map,1),map);return p;}
  // 冲击随机打中部位：打中手指、脚趾这类小部位会直接打掉，留下的是带死灵伤来源的缺失部位而不是伤口。
  static bool NecroticHit(Pawn p)=>p.health.hediffSet.hediffs.Any(x=>x.def.defName=="LP_NecroticWound" || (x is Hediff_MissingPart m && m.lastInjury?.defName=="LP_NecroticWound"));
  static void Remove(params Pawn[] pawns){foreach(var p in pawns){if(p.Dead){if(p.Corpse!=null && !p.Corpse.Destroyed)p.Corpse.Destroy();}else if(!p.Destroyed)p.Destroy();}}
  void CheckSoulOffering(Phylactery box){
   int oldLevel=box.level;float oldExperience=box.experience;
   var complete=CorpseAt(IntVec3.East*3);var damaged=CorpseAt(IntVec3.East*3+IntVec3.North*2);Pawn soul=complete.InnerPawn;
   Check(Souls.Unfit(complete)==null,"complete humanlike corpse can be offered: "+Souls.Unfit(complete));
   float expected=(Souls.BaseValue+Souls.PerSkillLevel*soul.skills.skills.Sum(s=>s.Level))*Mathf.Clamp(soul.GetStatValue(StatDefOf.PsychicSensitivity),.2f,4f);
   Check(Near(Souls.Value(soul),expected),"soul value = (50 + 5 x skill total) x psychic sensitivity");
   var skill=soul.skills.skills.First(s=>!s.TotallyDisabled && s.Level<=15);float before=Souls.Value(soul);skill.Level+=4;
   Check(Near(Souls.Value(soul)-before,20f*Souls.Sensitivity(soul)),"higher skills raise soul value");skill.Level-=4;
   damaged.InnerPawn.health.AddHediff(HediffDefOf.MissingBodyPart,damaged.InnerPawn.RaceProps.body.AllParts.First(b=>b.def.defName=="Arm"));
   var probe=new Bill_Production(Necro.Def<RecipeDef>("OfferSoul"));
   Check(Souls.Unfit(damaged)!=null && !probe.IsFixedOrAllowedIngredient(damaged) && probe.IsFixedOrAllowedIngredient(complete),"soul bill accepts only complete corpses");
   var meat=Spawn("Meat_Human",10);Check(Souls.Unfit(meat)!=null && !probe.IsFixedOrAllowedIngredient(meat),"soul bill rejects raw meat");meat.Destroy();
   box.level=5;box.experience=0;float total=TotalExperience(box.level,box.experience),value=Souls.Value(soul);
   // 1.20.0：完整模组列表的地图上可能本来就有别的完整人形尸体，测试期间先禁用它们，免得工作单拿错尸体。
   var otherCorpses=map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).Where(c=>c!=complete && c!=damaged && !c.IsForbidden(Faction.OfPlayer)).ToList();
   foreach(var c in otherCorpses)c.SetForbidden(true,false);
   try {Execute("OfferSoul");} finally {foreach(var c in otherCorpses)if(!c.Destroyed)c.SetForbidden(false,false);}
   Check(complete.Destroyed && !damaged.Destroyed,"soul offering consumes the complete corpse and skips the damaged one: complete="+complete.Destroyed+" damaged="+damaged.Destroyed+" others="+otherCorpses.Count(c=>c.Destroyed)+"/"+otherCorpses.Count);
   Check(Math.Abs(TotalExperience(box.level,box.experience)-total-value)<1f,"soul offering adds exactly the soul value to the phylactery");
   damaged.Destroy();box.level=oldLevel;box.experience=oldExperience;Necro.Lich(box.soul).SyncSpells();
  }
  void CheckLichSpells(Hediff_Lich h,Phylactery box){
   int oldLevel=box.level;float oldExperience=box.experience;
   bool Has(string n)=>worker.abilities.GetAbility(Necro.Def<AbilityDef>(n))!=null;
   CompLichSpell Spell(string n)=>worker.abilities.GetAbility(Necro.Def<AbilityDef>(n)).EffectComps.OfType<CompLichSpell>().First();
   box.level=2;h.SyncSpells();Check(!Has("FearGaze") && !Has("NecroticBlast") && !Has("PowerWordKill") && !h.DrainUnlocked,"level 2 phylactery has no extra spells");
   box.level=3;h.SyncSpells();Check(Has("FearGaze") && !Has("NecroticBlast"),"level 3 unlocks fear gaze");
   box.level=6;h.SyncSpells();Check(Has("NecroticBlast") && !h.DrainUnlocked,"level 6 unlocks necrotic blast");
   box.level=10;h.SyncSpells();Check(h.DrainUnlocked && !Has("PowerWordKill") && h.GetGizmos().OfType<Command_Toggle>().Any(g=>g.defaultLabel=="生命汲取光环"),"level 10 unlocks life drain aura toggle");
   box.level=15;h.SyncSpells();Check(Has("PowerWordKill"),"level 15 unlocks power word kill");
   int oldCap=LichMod.Settings.levelCap;LichMod.Settings.levelCap=5;h.SyncSpells();Check(Has("FearGaze") && !Has("NecroticBlast") && !Has("PowerWordKill") && !h.DrainUnlocked,"lower level cap relocks higher spells");LichMod.Settings.levelCap=oldCap;h.SyncSpells();
   Check(Has("PowerWordKill") && Necro.Def<AbilityDef>("PowerWordKill").abilityClass==typeof(LichPsycast),"spells use the phylactery psycast pipeline");

   IntVec3 spot=altar.InteractionCell+IntVec3.West*4;
   var foe=Foe(spot);var ally=Ally(foe.Position);
   Spell("FearGaze").Apply(new LocalTargetInfo(foe.Position),LocalTargetInfo.Invalid);
   Check(foe.MentalStateDef==MentalStateDefOf.PanicFlee && !ally.InMentalState,"fear gaze panics hostile humans only");
   Remove(foe);foe=Foe(spot);
   Spell("NecroticBlast").Apply(new LocalTargetInfo(foe.Position),LocalTargetInfo.Invalid);
   Check(NecroticHit(foe) && !ally.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any() && !ally.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().Any(),"necrotic blast damages hostiles and spares allies");
   Remove(foe);

   foe=Foe(worker.Position);var wound=(Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,worker,worker.RaceProps.body.corePart);wound.Severity=5;worker.health.AddHediff(wound);
   worker.psychicEntropy.RemoveAllEntropy();float focus=worker.psychicEntropy.CurrentPsyfocus;h.drainAura=true;
   int hits=h.DrainPulse();
   Check(hits>=1 && foe.health.hediffSet.hediffs.Any(x=>x.def.defName=="LP_NecroticWound") && wound.Severity<5,"life drain damages nearby hostile and heals the lich");
   Check(worker.psychicEntropy.EntropyValue>0 && Near(worker.psychicEntropy.CurrentPsyfocus,focus),"life drain raises entropy without spending psyfocus");
   worker.psychicEntropy.TryAddEntropy(worker.psychicEntropy.MaxEntropy-worker.psychicEntropy.EntropyValue-.1f,null,false,false);
   h.DrainPulse();Check(!h.drainAura && worker.psychicEntropy.EntropyValue<=worker.psychicEntropy.MaxEntropy+.001f,"life drain switches off instead of overflowing entropy");
   worker.psychicEntropy.RemoveAllEntropy();if(!wound.ShouldRemove)worker.health.RemoveHediff(wound);

   Check(!Spell("PowerWordKill").Valid(new LocalTargetInfo(ally)),"power word kill refuses own colonists");
   Spell("PowerWordKill").Apply(new LocalTargetInfo(foe),LocalTargetInfo.Invalid);
   Check(foe.Dead && foe.Corpse!=null && Souls.Unfit(foe.Corpse)==null,"power word kill slays target and leaves a complete corpse for soul offering");
   Remove(foe,ally);
   box.level=oldLevel;box.experience=oldExperience;h.drainAura=false;h.SyncSpells();
  }
  void CheckLichSpellsAfterLoad(Pawn lich){
   var h=Necro.Lich(lich);int unlocked=LichSpells.UnlockedLevel(h.box);
   Check(h.drainAura,"life drain toggle survives save/load");
   Check(LichSpells.Unlocks.All(s=>(lich.abilities.GetAbility(Necro.Def<AbilityDef>(s.name))!=null)==(unlocked>=s.level)),"unlocked spells match saved phylactery level after load");
   h.drainAura=false;
  }
 }
}
