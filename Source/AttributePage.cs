using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 2026-10-30（Peter）：冒险者分页的第三页“属性”。六维逐项列出：有效值怎么来、属性倍率 M、每个受影响的效果（公式、本人当前值、超凡 / 传奇点 / 武艺天赋在同一属性上的叠加）、超凡与神话。
 // 公式文字直接从 Hediff_Adventurer.Table 的 kind / per / cap / lo / hi 生成，数值用 FxValue、ConPart 等同一套函数，改表或改公式时这一页自动跟着变。
 public static class AttributePage {
  public class Line{public string label,formula,value,extra,tip;}
  static readonly Color Gold=new Color(1f,.82f,.35f),Mythic=new Color(.78f,.55f,1f),Dim=new Color(.72f,.72f,.72f);
  const float RowH=18f;

  static string X(float v)=>"×"+v.ToString("0.00");
  static string Pct(float v)=>(v>=0f?"+":"")+(v*100f).ToString("0.#")+"%";
  static string P(float v)=>(v*100f).ToString("0.#")+"%";
  static string N(float v)=>v.ToString("0.##");
  static StatDef S(string n)=>DefDatabase<StatDef>.GetNamedSilentFail(n);
  static bool PctStyle(StatDef d)=>d!=null && (d.toStringStyle==ToStringStyle.PercentZero || d.toStringStyle==ToStringStyle.PercentOne || d.toStringStyle==ToStringStyle.PercentTwo);
  static string Value(Hediff_Adventurer.ScoreFx e,float v)=>e.kind!=Hediff_Adventurer.FxKind.Offset?X(v):e.num?(v>=0?"+":"")+v.ToString("0.#"):Pct(v);
  // 和 Hediff_Adventurer.SameScoreWork 同一条件：本身已乘全局工作速度的同属性效果，表里除掉一份。
  static bool SharesWork(Hediff_Adventurer.ScoreFx e)=>e.a==Adventurer.Int && e.kind==Hediff_Adventurer.FxKind.Factor && e.stat!="WorkSpeedGlobal" && S(e.stat)?.statFactors?.Contains(StatDefOf.WorkSpeedGlobal)==true;

  // ---------- 公式（从表生成） ----------
  public static string Formula(Hediff_Adventurer.ScoreFx e){
   switch(e.kind){
    case Hediff_Adventurer.FxKind.Inverse:return "÷M，最低 ×"+N(e.cap);
    case Hediff_Adventurer.FxKind.Offset:return (e.num?N(e.per):P(e.per))+" ×(M − 1)，限 "+(e.num?N(e.lo)+"～"+N(e.hi):Pct(e.lo)+"～"+Pct(e.hi));
    case Hediff_Adventurer.FxKind.Linear:return "1 + "+P(e.per)+" × 调整值′，最低 ×"+N(Adventurer.MeleeMin)+"，× 天赋";
    case Hediff_Adventurer.FxKind.PerPoint:return "1 + "+P(e.per)+" ×(属性 − 10)′，× 天赋，最低 ×0.5"+(e.cap>0f?"，超 ×"+N(e.cap)+" 收敛":"");
    default:return "×M"+(e.cap>0f?"，超 ×"+N(e.cap)+" 收敛":"")+(e.stat=="CarryingCapacity"?"，最低 ×0.1":"")+(SharesWork(e)?"（已含工作速度）":"");
   }
  }
  // 悬停：代入本人数值。
  public static string Substitute(Hediff_Adventurer h,Hediff_Adventurer.ScoreFx e,float s){
   float m=h.AMult(e.a),tm=Affinity.TalentMult(h,e.a);string r;
   switch(e.kind){
    case Hediff_Adventurer.FxKind.Inverse:r="1 ÷ "+N(m)+" = "+X(1f/Mathf.Max(.0001f,m))+(1f/Mathf.Max(.0001f,m)<e.cap?" → 最低 "+X(e.cap):"");break;
    case Hediff_Adventurer.FxKind.Offset:{float raw=e.per*(m-1f);r=N(e.per)+" × ("+N(m)+" − 1) = "+N(raw)+(raw<e.lo || raw>e.hi?" → 限在 "+N(Mathf.Clamp(raw,e.lo,e.hi)):"");break;}
    case Hediff_Adventurer.FxKind.Linear:{float b=1f+e.per*h.AMod(e.a);r="1 + "+P(e.per)+" × "+N(h.AMod(e.a))+" = "+X(b)+(b<Adventurer.MeleeMin?" → 最低 "+X(Adventurer.MeleeMin):"")+(tm>1.0001f && b>=1f?"，× 天赋 "+N(tm):"")+" = "+X(h.LinearValue(e.a,e.per));break;}
    case Hediff_Adventurer.FxKind.PerPoint:{int d=h.Eff(e.a)-10;float pts=d>0?d*Affinity.RaceMult(h,e.a):d,b=Mathf.Max(.5f,1f+e.per*pts);
     r="(属性 − 10)′ = "+N(pts)+(d>0 && Math.Abs(Affinity.RaceMult(h,e.a)-1f)>.0001f?"（"+d+" × 种族 "+N(Affinity.RaceMult(h,e.a))+"）":"")+"；1 + "+P(e.per)+" × "+N(pts)+" = "+X(b)+(tm>1.0001f && b>=1f?"，× 天赋 "+N(tm):"")+" = "+X(h.PerPointValue(e))+"\n另外照常乘全局工作速度（和 Isekai 一样）。";break;}
    default:r="M = "+X(m)+(e.cap>0f && m>e.cap?" → 超过 ×"+N(e.cap)+" 收敛为 "+X(Hediff_Adventurer.SoftCap(m,e.cap)):"")+(SharesWork(e)?"\n原版这一项本身乘全局工作速度（已吃智力倍率），这里除掉一份，智力只算一次。":"");break;
   }
   if(s<.9999f)r+="\n属性效果强度 "+P(s)+"：1 + (完整值 − 1) × "+P(s)+" = "+Value(e,h.FxValue(e,s));
   return r;
  }

  // ---------- 叠加：超凡 / 传奇属性点 / 武艺天赋在同一个属性上的数值 ----------
  public class Extras{public readonly List<KeyValuePair<string,List<StatModifier>[]>> src=new List<KeyValuePair<string,List<StatModifier>[]>>();}
  public static Extras ExtrasOf(Hediff_Adventurer h){
   var x=new Extras();
   void Add(string name,Action<List<StatModifier>,List<StatModifier>> fill){var f=new List<StatModifier>();var o=new List<StatModifier>();fill(f,o);if(f.Count+o.Count>0)x.src.Add(new KeyValuePair<string,List<StatModifier>[]>(name,new[]{f,o}));}
   Add("超凡",(f,o)=>Exalted.AddStats(h,new HediffStage(),f,o));
   Add("传奇点",(f,o)=>Legend.AddStats(h,f,o));
   Add("武艺",(f,o)=>CombatGifts.AddStats(h,new HediffStage(),f,o));
   return x;
  }
  public static string ExtraText(Extras x,string stat){
   var d=S(stat);if(d==null || x==null)return "";var parts=new List<string>();
   foreach(var kv in x.src){
    float fac=1f,off=0f;bool hf=false,ho=false;
    foreach(var m in kv.Value[0])if(m.stat==d){fac*=m.value;hf=true;}
    foreach(var m in kv.Value[1])if(m.stat==d){off+=m.value;ho=true;}
    if(hf)parts.Add(kv.Key+" "+X(fac));
    if(ho)parts.Add(kv.Key+" "+(PctStyle(d)?Pct(off):(off>=0?"+":"")+off.ToString("0.##")));
   }
   return string.Join("，",parts);
  }

  // ---------- 一项属性的全部效果行（自检也用） ----------
  public static List<Line> Lines(Hediff_Adventurer h,int a,Extras x=null){
   float s=Adventurer.EffectScale;var list=new List<Line>();
   if(Adventurer.EffectsHandedToIsekai || s<=0f || Legendary.Is(h.pawn))return list;
   float tm=Affinity.TalentMult(h,a);
   if(a==Adventurer.Con){
    float con=h.ConPart(s),lv=1f+h.LevelGrowth(h.level)*s,leg=Legend.HealthFactor(h),tough=Feats.ToughFactor(h);
    list.Add(new Line{label="部位生命（体质）",formula="1 + "+P(Hediff_Adventurer.ConPartPerMod)+" × 调整值′，× 天赋，最低 ×1",value=X(con),
     tip="1 + "+P(Hediff_Adventurer.ConPartPerMod)+" × "+N(h.AMod(a))+(tm>1.0001f?"，× 天赋 "+N(tm):"")+" = "+X(con)+(s<.9999f?"（已按属性效果强度 "+P(s)+" 插值）":"")+"\n体质低于 10 不减少部位生命。"});
    list.Add(new Line{label="等级成长",formula="1 + 每级平均生命骰成长 ×(等级 − 1)",value=X(lv),tip="按职业生命骰（没分配职业的等级每级 +"+P(Hediff_Adventurer.PartPerLevel)+"）；传奇等级 21～40 不加，血量走传奇属性点。"});
    var extra=new List<string>();if(Math.Abs(leg-1f)>.0001f)extra.Add("传奇血量 "+X(leg));if(Math.Abs(tough-1f)>.0001f)extra.Add("坚韧 "+X(tough));
    list.Add(new Line{label="部位生命合计",formula="体质 × 等级成长"+(extra.Count>0?" × 传奇血量 / 坚韧":""),value=X(Adventurer.PartHealthFactor(h.pawn)),extra=string.Join("，",extra)});
    if(Adventurer.PainScaleOn)list.Add(new Line{label="伤口疼痛",formula="÷ 部位生命合计（≥1 的部分）",value="÷"+Adventurer.PainDivisor(h.pawn).ToString("0.00"),tip="伤口和缺失部位的疼痛除以部位生命倍率，免得血厚却因疼痛先倒地。"});
   }
   foreach(var e in Hediff_Adventurer.Table){
    if(e.a!=a || S(e.stat)==null)continue;
    list.Add(new Line{label=e.label,formula=Formula(e),value=Value(e,h.FxValue(e,s)),extra=ExtraText(x,e.stat),tip=Substitute(h,e,s)});
   }
   string skills=a==0?"格斗":a==1?"射击":a==3?"智识":a==4?"医疗 / 种植 / 驯兽":a==5?"社交":null;
   if(skills!=null)list.Add(new Line{label=skills+"学习",formula="×M，最低 ×"+N(Adventurer.LearnMin),value=X(Mathf.Max(Adventurer.LearnMin,h.Fx(a,s))),tip="技能学习速度 = 属性倍率 M = "+X(h.AMult(a))});
   float cp=Adventurer.ClassPowerBonus(h.pawn,a)-Adventurer.ProfClassPer*Math.Max(0,h.Proficiency-2);
   list.Add(new Line{label="职业强度（主属性）",formula=P(Adventurer.ClassAbilityPer)+" × 调整值′，× 天赋",value=Pct(cp),tip="以"+Adventurer.Names[a]+"为主属性的职业（施法、职业能力）强度：(1 + "+P(Adventurer.ClassAbilityPer)+" × "+N(h.AMod(a))+")"+(tm>1.0001f?" × 天赋 "+N(tm):"")+" − 1 = "+Pct(cp)+"\n熟练加值另加 "+P(Adventurer.ProfClassPer)+" ×（熟练 − 2）。"});
   if(a==Adventurer.Int){ // 1.97.0 奥术强度的智力项 10%
    float ap=Adventurer.ClassPowerBonus(h.pawn,a,Adventurer.ArcaneAbilityPer)-Adventurer.ProfClassPer*Math.Max(0,h.Proficiency-2);
    list.Add(new Line{label="奥术强度（智力）",formula=P(Adventurer.ArcaneAbilityPer)+" × 调整值′，× 天赋",value=Pct(ap),tip="奥术强度（法师、术士智识支）的智力项每点调整值 "+P(Adventurer.ArcaneAbilityPer)+"：(1 + "+P(Adventurer.ArcaneAbilityPer)+" × "+N(h.AMod(a))+")"+(tm>1.0001f?" × 天赋 "+N(tm):"")+" − 1 = "+Pct(ap)+"\n器械强度等其他用智力的强度仍按 "+P(Adventurer.ClassAbilityPer)+"。"});
   }
   return list;
  }

  // 有效值构成：基础（不超过上限）+ 上限之后的加值。
  public static string Composition(Hediff_Adventurer h,int a){
   int b=h.scores[a],cap=h.Cap(a),feat=Feats.ScoreBonus(h,a),gift=CombatGifts.ScoreBonus(h,a),cls=ClassMartial2.ScoreBonus(h,a);
   var s="基础 "+b+(b>cap?"（超过上限 "+cap+"，按 "+cap+" 算）":"（上限 "+cap+"）");
   if(feat>0)s+=" + 专长 "+feat;if(gift>0)s+=" + 武艺天赋 "+gift;if(cls>0)s+=" + 职业 "+cls;
   return s+" = 有效 "+h.Eff(a)+"　调整值 "+Adventurer.Signed(h.Mod(a))+"，调整值′ "+N(h.AMod(a))+(Math.Abs(Affinity.RaceMult(h,a)-1f)>.0001f && h.Mod(a)>0?"（× 种族 "+N(Affinity.RaceMult(h,a))+"）":"");
  }
  public static string MultText(Hediff_Adventurer h,int a){
   float c=Adventurer.Curve(h.Eff(a)),m=h.AMult(a),k=Affinity.ScoreMult(h,a),t=Affinity.TalentMult(h,a);
   string s="M = 3.5 表 "+X(c);
   if(Math.Abs(m-c)>.001f)s+=" × "+N(k)+"（种族 / 天赋取较大）= "+X(m);
   if(t>1.0001f)s+="　天赋（线性效果整体）×"+N(t);
   return s;
  }

  // ---------- 绘制 ----------
  public static float Draw(float w,Pawn p,Hediff_Adventurer h){
   float y=0f;float s=Adventurer.EffectScale;
   Text.Font=GameFont.Tiny;
   string legend="M：属性倍率 = 3.5 负重表（10 = ×1，每 +5 约 ×2，每 +10 ×4；低于 10 = 属性 ÷ 10）× 种族亲和与天赋中较大者（属性低于 10 时不乘）\n"+
    "调整值′：(属性 − 10) ÷ 2 向下取整，正的部分 × 种族亲和；(属性 − 10)′ 同理\n"+
    "天赋：天赋异禀 / 天赋·某项 ×1.5、极致 ×2.5，乘在线性效果整体上（效果低于 ×1 时不乘），和种族相乘\n"+
    "收敛：超过上限后 = 上限 + k·ln(1 + 超出 ÷ k)，k = max(0.5, 上限 ÷ 2)（Isekai 方式）"+
    (s>0f && s<.9999f?"\n属性效果强度 "+P(s)+"：所有数值 = 1 + (完整值 − 1) × "+P(s):"")+
    "\n悬停每一行看代入本人数值的算式；“叠加”一列是同一属性上另外相乘 / 相加的超凡、传奇属性点、武艺天赋。";
   float lh=Text.CalcHeight(legend,w-8f);var lr=new Rect(0f,y,w,lh+6f);Widgets.DrawBoxSolid(lr,new Color(1f,1f,1f,.05f));
   GUI.color=Dim;Widgets.Label(new Rect(4f,y+3f,w-8f,lh),legend);GUI.color=Color.white;y+=lh+10f;
   string off=Adventurer.EffectsHandedToIsekai?"六维的数值效果已交给 Isekai（设置“Isekai 共存模式”），下面只列有效值和超凡。":s<=0f?"属性效果强度 0%：六维没有数值效果。":Legendary.Is(p)?"传奇单位：六维不提供数值效果（强度已经算在兵种里）。":null;
   if(off!=null){GUI.color=Color.gray;Widgets.Label(new Rect(4f,y,w-8f,RowH),off);GUI.color=Color.white;y+=RowH+4f;}
   var x=ExtrasOf(h);int raw=Exalted.Raw(h);
   const float labW=112f,valW=62f,exW=124f;float fw=w-labW-valW-exW-12f;
   for(int a=0;a<6;a++){
    // 标题：属性名 有效值（调整值）+ 来源标注
    bool my=(h.mythicMask&1<<a)!=0,ex=(h.exaltedMask&1<<a)!=0;
    var head=new Rect(0f,y,w,24f);Widgets.DrawBoxSolid(head,new Color(1f,1f,1f,.09f));
    Text.Font=GameFont.Small;
    ITab_Adventurer.AffinityColor(Affinity.ScoreMult(h,a));if(my)GUI.color=Mythic;else if(ex)GUI.color=Gold;
    Widgets.Label(new Rect(4f,y+2f,220f,22f),Adventurer.Names[a]+" "+h.Eff(a)+"（"+Adventurer.Signed(h.Mod(a))+"）"+Affinity.Tag(h,a));GUI.color=Color.white;
    Text.Font=GameFont.Tiny;Text.Anchor=TextAnchor.MiddleRight;GUI.color=Gold;
    Widgets.Label(new Rect(224f,y,w-228f,24f),Affinity.SourceTag(h,a));Text.Anchor=TextAnchor.UpperLeft;GUI.color=Color.white;
    {string tip=Affinity.Tip(h,a);if(tip!=""){Widgets.DrawHighlightIfMouseover(head);TooltipHandler.TipRegion(head,tip);}}
    y+=25f;
    GUI.color=Dim;Widgets.Label(new Rect(8f,y,w-8f,RowH),Composition(h,a));y+=RowH;
    Widgets.Label(new Rect(8f,y,w-8f,RowH),MultText(h,a));GUI.color=Color.white;y+=RowH+2f;
    // 效果表
    var lines=Lines(h,a,x);
    if(lines.Count>0){
     GUI.color=Color.gray;
     Widgets.Label(new Rect(8f,y,labW,RowH),"效果");Widgets.Label(new Rect(8f+labW,y,fw,RowH),"公式");
     Text.Anchor=TextAnchor.UpperRight;Widgets.Label(new Rect(w-exW-valW-4f,y,valW,RowH),"当前");Text.Anchor=TextAnchor.UpperLeft;
     Widgets.Label(new Rect(w-exW,y,exW,RowH),"叠加");GUI.color=Color.white;y+=RowH;
     for(int k=0;k<lines.Count;k++){
      var l=lines[k];var row=new Rect(4f,y,w-4f,RowH);if(k%2==0)Widgets.DrawLightHighlight(row);
      Widgets.Label(new Rect(8f,y,labW,RowH),l.label.Truncate(labW));
      GUI.color=Dim;Widgets.Label(new Rect(8f+labW,y,fw,RowH),l.formula.Truncate(fw));GUI.color=Color.white;
      Text.Anchor=TextAnchor.UpperRight;Widgets.Label(new Rect(w-exW-valW-4f,y,valW,RowH),l.value);Text.Anchor=TextAnchor.UpperLeft;
      if(!string.IsNullOrEmpty(l.extra)){GUI.color=Gold;Widgets.Label(new Rect(w-exW,y,exW,RowH),l.extra.Truncate(exW));GUI.color=Color.white;}
      if(Mouse.IsOver(row)){Widgets.DrawHighlight(row);TooltipHandler.TipRegion(row,l.label+"："+l.formula+(l.tip!=null?"\n\n"+l.tip:"")+(string.IsNullOrEmpty(l.extra)?"":"\n\n叠加："+l.extra));}
      y+=RowH;
     }
    }
    // 超凡 / 神话
    {
     bool reached=(raw&1<<a)!=0,rm=(raw&1<<(a+6))!=0;string text;Color c;
     if(ex){text=(my?"神话·"+Exalted.MythicNames[a]+"：":"超凡·"+Exalted.ExaltedNames[a]+"：")+Exalted.Describe(a,my);c=my?Mythic:Gold;}
     else if(reached){text=(rm?"神话·"+Exalted.MythicNames[a]:"超凡·"+Exalted.ExaltedNames[a])+"（已达成，未生效："+(Exalted.OffReason??"属性刚变化，稍后生效")+"）";c=Color.gray;}
     else{text="超凡·"+Exalted.ExaltedNames[a]+"：基础与有效值都到 "+Exalted.Threshold+" 解锁（现在 "+h.scores[a]+" / "+h.Eff(a)+"）";c=Color.gray;}
     if(ex && !my)text+="\n神话·"+Exalted.MythicNames[a]+"：有效值 "+Exalted.MythicScore+"，或传奇冒险者 + "+Legend.Names[Exalted.LegendPairs[a][0]]+"与"+Legend.Names[Exalted.LegendPairs[a][1]]+"传奇点合计 "+Exalted.MythicLegend+"（现在 "+(h.legendSpent==null?0:h.legendSpent[Exalted.LegendPairs[a][0]]+h.legendSpent[Exalted.LegendPairs[a][1]])+"）";
     float th=Text.CalcHeight(text,w-12f);var tr=new Rect(8f,y+2f,w-12f,th);GUI.color=c;Widgets.Label(tr,text);GUI.color=Color.white;
     if(!ex){Widgets.DrawHighlightIfMouseover(tr);TooltipHandler.TipRegion(tr,Exalted.Describe(a,false)+"\n\n"+Exalted.Describe(a,true));}
     y+=th+4f;
    }
    y+=8f;
   }
   // 与六维无关、但叠在同一批属性上的来源
   Text.Font=GameFont.Small;Widgets.Label(new Rect(0f,y,w,24f),"其他来源（不随六维变化）");y+=24f;Text.Font=GameFont.Tiny;
   var other=new List<string>{
    "熟练加值 +"+h.Proficiency+"："+Adventurer.ProfEffects(h.Proficiency),
    "等级 "+Math.Min(h.level,Adventurer.MaxLevel)+"：近战命中、近战闪避各 "+Pct(Adventurer.LevelMelee(h.level))+"（每级 +"+P(Adventurer.LevelMeleePer)+"，只算 1～20 级）"};
   float md=ClassSystem.MartialDamage(h);if(md>0f)other.Add("物理职业成长：近战伤害 "+X(1f+md));
   other.Add("传奇属性点："+Legend.Summary(h).Replace("\n","；"));
   other.Add("专长、史诗恩赐、战斗风格的数值见“冒险者”页悬停说明。");
   foreach(var o in other){float th=Text.CalcHeight(o,w-12f);GUI.color=Dim;Widgets.Label(new Rect(8f,y,w-12f,th),o);GUI.color=Color.white;y+=th+2f;}
   Text.Font=GameFont.Small;
   return y;
  }
 }
}
