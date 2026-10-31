using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.89.0（Peter：升级后部位生命和施法强度的变化）：只做显示，不改数值。
 // 部位生命按 Hediff_Adventurer.LevelGrowth 的同一公式预算“下一级”（职业生命骰换掉一个未分配等级的 +5%）；
 // 职业强度按 StatPart_ClassPower：相关职业等级合计每比 1 高 1 级 +2%（最多 +40%），冒险者升级另看熟练加值 ×2%。
 [StaticConstructorOnStartup]
 public static class ClassLevelInfo {
  static ClassLevelInfo(){var prev=ITab_Adventurer.LevelUpLinesHook;ITab_Adventurer.LevelUpLinesHook=(p,l)=>{var a=LevelUpLines(p,l);var b=prev?.Invoke(p,l);return b==null?a:a.Concat(b);};}

  // ---------- 部位生命 ----------
  static float Scale=>Adventurer.forcedScale>=0f?Adventurer.forcedScale:Adventurer.EffectScale;
  // LevelGrowth 的同一公式，plus 这个职业多 1 级、冒险者等级按 lv 算。
  public static float Growth(Hediff_Adventurer h,LP_ClassDef plus,int lv){
   lv=Math.Min(lv,Adventurer.MaxLevel);if(lv<=1)return 0f;float sum=0f;int n=0;bool seen=false;
   if(h.classes?.levels!=null)foreach(var kv in h.classes.levels){if(kv.Key==null)continue;int v=kv.Value;if(kv.Key==plus){v++;seen=true;}int c=Math.Min(v,lv-n);if(c<=0)continue;sum+=c*kv.Key.PartPerLevel;n+=c;}
   if(plus!=null && !seen && lv-n>0){sum+=plus.PartPerLevel;n++;}
   sum+=(lv-n)*Hediff_Adventurer.PartPerLevel;return sum/lv*(lv-1);
  }
  // 部位生命合计倍率（同 Adventurer.PartHealthFactor），换成 plus 多 1 级、冒险者 lv 级之后的值。
  public static float PartWith(Hediff_Adventurer h,LP_ClassDef plus,int lv){
   var p=h?.pawn;float now=Adventurer.PartHealthFactor(p);if(h==null || Legendary.Is(p))return now;
   float s=Scale;if(s<=0f)return now;
   return now*(1f+Growth(h,plus,lv)*s)/(1f+h.LevelGrowth(h.level)*s);
  }
  public static string PartText(float f)=>(f>=1f?"+":"")+((f-1f)*100f).ToString("0.#")+"%";

  // ---------- 职业强度 ----------
  static readonly Dictionary<LP_ClassDef,List<StatDef>> stats=new Dictionary<LP_ClassDef,List<StatDef>>();
  // 这个职业能力用到的强度属性（按第一次出现的顺序）。
  public static List<StatDef> Stats(LP_ClassDef c){
   if(c==null)return new List<StatDef>();
   if(!stats.TryGetValue(c,out var l)){
    l=new List<StatDef>();
    foreach(var e in c.abilities){var s=e.ability?.comps?.OfType<CompProperties_ClassEffect>().FirstOrDefault()?.stat;if(s!=null && !l.Contains(s) && Part(s)!=null)l.Add(s);}
    stats[c]=l;
   }
   return l;
  }
  public static StatPart_ClassPower Part(StatDef s)=>s?.parts?.OfType<StatPart_ClassPower>().FirstOrDefault();
  static bool MartialSkill(SkillDef s)=>s==SkillDefOf.Melee || s==SkillDefOf.Shooting;
  // 职业等级加成算进去的等级合计：格斗 / 射击 = 物理职业合计，其他 = 用这门技能当主 / 副技能的职业合计。
  public static int BonusLevels(Hediff_Adventurer h,SkillDef s){
   int n=0;if(h?.classes?.levels==null)return 0;
   foreach(var kv in h.classes.levels)if(kv.Key!=null && (MartialSkill(s)?kv.Key.martial:kv.Key.mainSkill==s || kv.Key.alternateSkill==s))n+=kv.Value;
   return n;
  }
  public static bool Counts(LP_ClassDef c,SkillDef s)=>c!=null && (MartialSkill(s)?c.martial:c.mainSkill==s || c.alternateSkill==s);
  static float Prof(int level)=>Adventurer.Enabled?Adventurer.ProfClassPer*Math.Max(0,Legendary.Proficiency(level)-2):0f;
  // 强度属性在 plus 多 1 级、冒险者 lv 级之后的值（当前值 + 职业等级项差 + 熟练加值项差）。
  public static float PowerWith(Pawn p,Hediff_Adventurer h,StatDef stat,LP_ClassDef plus,int lv){
   float now=p.GetStatValue(stat);var part=Part(stat);if(part==null || h==null)return now;
   int n=BonusLevels(h,part.skill),m=n+(Counts(plus,part.skill)?1:0);
   return now+ClassSystem.MartialBonusFor(m)-ClassSystem.MartialBonusFor(n)+Prof(lv)-Prof(h.level);
  }
  // 下一级的冒险者等级：还有职业点就是现在的等级，没有就得先升 1 级。
  public static int NextAdventurerLevel(Hediff_Adventurer h)=>Math.Max(h.level,ClassSystem.Total(h)+1);

  // ---------- 职业页的直显行 ----------
  public static string RowLine(Pawn p,Hediff_Adventurer h,LP_ClassDef c){
   if(h==null || !Adventurer.Enabled)return null;
   int lv=ClassSystem.Level(h,c);if(lv>=ClassSystem.CapLevel)return "部位生命 "+PartText(Adventurer.PartHealthFactor(p))+"（已满级）";
   int al=NextAdventurerLevel(h);
   var parts=new List<string>{"部位生命 "+PartText(Adventurer.PartHealthFactor(p))+" → 下一级 "+PartText(PartWith(h,c,al))};
   foreach(var s in Stats(c))parts.Add(s.LabelCap+" ×"+p.GetStatValue(s).ToString("0.00")+" → 下一级 ×"+PowerWith(p,h,s,c,al).ToString("0.00"));
   return string.Join("　",parts)+(al>h.level?"（含冒险者升到 "+al+" 级）":"");
  }

  // ---------- 资源上限公式 ----------
  public static string ResourceFormula(LP_ClassDef c){
   switch(c?.resource){
    case ClassResource.Mana:return "上限 = 2 + 职业等级";
    case ClassResource.Pact:return "上限 = 2 + 邪术师等级 / 4（向下取整）";
    case ClassResource.Grace:return "上限 = 2 + (牧师 + 圣武士等级) / 2（向上取整，合计最多按 "+ClassSystem.CapLevel+" 级）";
    case ClassResource.Fury:case ClassResource.Ki:case ClassResource.Rage:return "上限 = 2 + 职业等级 / 2（向上取整）";
   }
   return null;
  }
  static int BaseMax(LP_ClassDef c,int level){
   switch(c?.resource){
    case ClassResource.Mana:return ClassSystem.MaxMana(level);
    case ClassResource.Pact:return Warlock.MaxPact(level);
    case ClassResource.None:case null:return 0;
    default:return ClassSystem.MaxResource(level);
   }
  }
  public static string ResourceTip(Hediff_Adventurer h,LP_ClassDef c){
   var sb=new StringBuilder(ClassSystem.ResourceTip(c));
   string f=ResourceFormula(c);
   if(f!=null){
    int pool=ClassSystem.PoolLevel(h,c),b=BaseMax(c,pool),leg=ClassLegendary.ResourceFor(h,c),max=ClassSystem.MaxResource(h,c),other=max-b-leg;
    sb.Append("\n"+f+"；每个传奇职业能力 +"+ClassLegendary.ResourceBonus+"。");
    sb.Append("\n现在："+b+(leg!=0?" + 传奇 "+leg:"")+(other!=0?" + 专长 "+other:"")+" = "+max+"。");
   }
   if(c==ClassMartial2.Rogue && ClassSubclass.ManaMax(h)>0)sb.Append("\n诡术师法力：上限 = 2 + 游荡者等级 / 2（向上取整），现在 "+ClassSubclass.ManaMax(h)+"。");
   return sb.ToString();
  }

  // ---------- 职业名悬停：每级表 ----------
  // 某职业等级获得的东西（能力、子职业、额外攻击档位、魔能爆光束、宿敌、战士奖励专长）。
  public static List<string> GainsAt(Hediff_Adventurer h,LP_ClassDef c,int level){
   var l=new List<string>();
   foreach(var e in c.abilities)if(e.level==level)l.Add(e.ability.label+(e.capstoneSkill!=null?"（另要"+e.capstoneSkill.label+" "+ClassSystem.CapstoneSkill+" 级）":""));
   var picked=ClassSubclass.Picked(h,c);
   if(level==ClassSubclass.PickLevelFor(c) && ClassSubclass.Options(c).Any())l.Add(picked==null?"选择子职业":"子职业「"+picked.label+"」");
   if(picked!=null){foreach(var e in picked.abilities)if(e.level==level)l.Add(e.ability.label);foreach(var ch in picked.choices)if(ch.level==level)l.Add("选择"+ch.label);}
   var gen=ClassSubclass.General(c);if(gen!=null)foreach(var e in gen.abilities)if(e.level==level)l.Add(e.ability.label);
   if(c.martial && c!=ClassMartial2.Rogue && level==Martial.TierLevels[0])l.Add("额外攻击 第 1 档"+(c==ClassSystem.Monk?"（徒手）":""));
   if(c==ClassSystem.Fighter){if(level==Martial.TierLevels[1])l.Add("额外攻击 第 2 档");if(level==Martial.TierLevels[2])l.Add("额外攻击 第 3 档");if(ClassSystem.FighterFeatLevels.Contains(level))l.Add("战士奖励专长");}
   if(c==Warlock.Class && level>1 && Warlock.Beams(level)>Warlock.Beams(level-1))l.Add("魔能爆 "+Warlock.Beams(level)+" 道光束");
   if(c==ClassMartial2.Ranger && (level==1 || level==ClassMartial2.SecondFoeLevel))l.Add("宿敌 "+(level==1?1:2)+" 种");
   return l;
  }
  public static string ClassTip(Pawn p,Hediff_Adventurer h,LP_ClassDef c){
   var sb=new StringBuilder();
   sb.AppendLine(c.LabelCap);sb.AppendLine();sb.AppendLine(c.description);sb.AppendLine();
   sb.AppendLine("每个职业等级：部位生命 +"+(c.PartPerLevel*100f).ToString("0.#")+"%（未分配职业点的等级 +"+(Hediff_Adventurer.PartPerLevel*100f).ToString("0.#")+"%；按平均每级 ×(冒险者等级 − 1)，再乘体质）");
   if(c.DamagePerLevel>0f)sb.AppendLine("　武器伤害 +"+(c.DamagePerLevel*100f).ToString("0.#")+"%（近战与远程），近战穿甲 +"+(c.PenPerLevel*100f).ToString("0.#")+"%");
   foreach(var s in Stats(c)){var part=Part(s);sb.AppendLine("　"+s.LabelCap+" +"+(ClassSystem.MartialPer*100f).ToString("0")+"%（"+(MartialSkill(part.skill)?"所有物理职业":"用"+part.skill.label+"的职业")+"等级合计每比 1 高 1 级，最多 +"+(ClassSystem.MartialMax*100f).ToString("0")+"%）");}
   string f=ResourceFormula(c);if(f!=null)sb.AppendLine("资源 "+ClassSystem.ResourceLabel(c.resource)+"："+f);
   sb.AppendLine();sb.AppendLine("等级表：");
   int lv=ClassSystem.Level(h,c);
   for(int l=1;l<=ClassSystem.CapLevel;l++){
    var g=GainsAt(h,c,l);int r=BaseMax(c,l),r0=BaseMax(c,l-1);
    if(g.Count==0 && (r==r0 || l==1))continue;
    sb.AppendLine((l<=lv?"✓ ":"　")+"Lv "+l+"："+string.Join("、",g)+(r>0 && r!=r0?(g.Count>0?"；":"")+ClassSystem.ResourceLabel(c.resource)+"上限 "+r:""));
   }
   if(lv<ClassSystem.CapLevel && Adventurer.Enabled){
    int al=NextAdventurerLevel(h);sb.AppendLine();sb.AppendLine("下一级获得（"+c.label+" Lv "+(lv+1)+(al>h.level?"，冒险者需先升到 "+al+" 级":"")+"）：");
    foreach(var x in NextLines(p,h,c,al))sb.AppendLine("· "+x);
   }
   return sb.ToString().TrimEnd();
  }
  // 加 1 级后的变化（能力 + 数值）：给悬停和选职业窗口用。
  public static List<string> NextLines(Pawn p,Hediff_Adventurer h,LP_ClassDef c,int al){
   int lv=ClassSystem.Level(h,c);var l=new List<string>();
   var g=GainsAt(h,c,lv+1);if(g.Count>0)l.Add(string.Join("、",g));
   l.Add("部位生命 "+PartText(Adventurer.PartHealthFactor(p))+" → "+PartText(PartWith(h,c,al)));
   foreach(var s in Stats(c))l.Add(s.LabelCap+" ×"+p.GetStatValue(s).ToString("0.00")+" → ×"+PowerWith(p,h,s,c,al).ToString("0.00"));
   if(c.resource!=ClassResource.None){int pool=ClassSystem.PoolLevel(h,c),b0=BaseMax(c,pool),b1=BaseMax(c,Math.Min(pool+1,ClassSystem.CapLevel));if(b1!=b0){int m=ClassSystem.MaxResource(h,c);l.Add(ClassSystem.ResourceLabel(c.resource)+"上限 "+m+" → "+(m+b1-b0));}}
   if(c.DamagePerLevel>0f)l.Add("武器伤害 +"+(c.DamagePerLevel*100f).ToString("0.#")+"%、近战穿甲 +"+(c.PenPerLevel*100f).ToString("0.#")+"%");
   return l;
  }

  // ---------- 加点：前后对比的消息 ----------
  public static bool AddLevel(Hediff_Adventurer h,LP_ClassDef c){
   var p=h?.pawn;if(p==null)return ClassSystem.AddLevel(h,c,true);
   int lv=ClassSystem.Level(h,c);var lines=Adventurer.Enabled?NextLines(p,h,c,h.level):null;
   var before=new HashSet<AbilityDef>(p.abilities?.abilities.Select(a=>a.def)??Enumerable.Empty<AbilityDef>());
   if(!ClassSystem.AddLevel(h,c,false))return false;
   if(p.Faction!=null && p.Faction.IsPlayer){
    var got=p.abilities?.abilities.Select(a=>a.def).Where(d=>!before.Contains(d)).Select(d=>d.label).ToList()??new List<string>();
    var text=p.LabelShort+"："+c.label+" "+(lv+1)+" 级"+(got.Count>0?"，获得 "+string.Join("、",got):"");
    if(lines!=null){lines=lines.Where(x=>x.Contains("→")).ToList();if(lines.Count>0)text+="；"+string.Join("；",lines);}
    Messages.Message(text,p,MessageTypeDefOf.PositiveEvent,false);
   }
   return true;
  }

  // ---------- 冒险者升级信件（LevelUpLinesHook，每升 1 级调用一次，h 已是新等级） ----------
  public static IEnumerable<string> LevelUpLines(Pawn p,int l){
   var h=Adventurer.Get(p);if(h==null || !Adventurer.Enabled || l<=1 || h.classes==null)return Enumerable.Empty<string>();
   var list=new List<string>();int top=h.level;float d0=Prof(top)-Prof(l-1),d1=Prof(top)-Prof(l);
   if(d0!=d1){
    var seen=new HashSet<StatDef>();
    foreach(var c in ClassSystem.Taken(h))foreach(var s in Stats(c))if(seen.Add(s)){float v=p.GetStatValue(s);list.Add(s.LabelCap+" ×"+(v-d0).ToString("0.00")+" → ×"+(v-d1).ToString("0.00")+"（熟练加值）");}
   }
   return list;
  }
  // 冒险者 from → to 级的部位生命（升级信件里那一行用）。
  public static float PartAtAdventurer(Hediff_Adventurer h,int lv){
   var p=h?.pawn;float now=Adventurer.PartHealthFactor(p);if(h==null || Legendary.Is(p))return now;float s=Scale;if(s<=0f)return now;
   return now*(1f+h.LevelGrowth(lv)*s)/(1f+h.LevelGrowth(h.level)*s);
  }
 }
}
