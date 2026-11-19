using System.Collections.Generic;
using RimWorld;
using Verse;
using UnityEngine;

namespace LichPhylactery {
 // 1.173.0 古巨兽后裔（异种型 LP_BehemothKin，Defs/Behemoth.xml）：无限生长 + 原版的不死（Deathless）、永生（Ageless）。
 // 无限生长：基因自己记“已经长了多少年”（永生会让生理年龄停在 18 岁半，所以不能照 Big and Small 的 sizeByAge 按年龄算），
 // 体型 = 1 + 年数 × perYear（默认每年 +0.5）。倍率走通用的 RaceSize（Source/RaceSize.cs）：真实体型 ×S、画面 ×√S、胃容量 ×S，
 // 巨躯规则（生命、近战、负重）照 S 算。新生成的成年人随机已长 0.5～2 年（体型 1.25～2）；婴儿、游戏里后装上基因的从 0 开始。
 // 1.174.0（Peter）：长到体型 maxSize（默认 10，同 Big and Small 的曲线终点）就停；到顶时获得特性「巨兽」（LP_BehemothTrait：睡眠需求、食量增加）。
 // 基因拿掉时特性一并去掉。maxSize ≤ 0 表示不封顶。
 public class EndlessGrowthExtension : RaceSizeExtension {public float perYear=.5f,startMin=.5f,startMax=2f,maxSize=10f;public string trait="LP_BehemothTrait";}
 public class Gene_EndlessGrowth : Gene {
  public float years=-1f;int acc;float shown=-1f;
  EndlessGrowthExtension Ext=>def.GetModExtension<EndlessGrowthExtension>();
  float Max=>Ext?.maxSize??10f;
  public float Size{get{float s=1f+Mathf.Max(0f,years)*(Ext?.perYear??.5f);float m=Max;return m>0f?Mathf.Min(m,s):s;}}
  public bool Full=>Max>0f && Size>=Max-.0001f;
  public override void PostAdd(){
   base.PostAdd();
   if(years<0f){var e=Ext;years=e!=null && PawnGenerator.IsBeingGenerated(pawn) && pawn.ageTracker?.Adult==true?Rand.Range(e.startMin,e.startMax):0f;}
   shown=Size;Behemoth.Changed(pawn);Behemoth.SyncTrait(pawn,this);
  }
  public override void PostRemove(){base.PostRemove();Behemoth.Changed(pawn);Behemoth.SyncTrait(pawn,null);}
  // 每 2500 刻（1 小时）累加一次；体型每变 0.01 刷新一次缓存、胃容量和画面。到顶后不再累加，只确认特性还在。
  public override void TickInterval(int delta){
   base.TickInterval(delta);
   if(!Active || (acc+=delta)<2500)return;
   if(Full){acc=0;Behemoth.SyncTrait(pawn,this);return;}
   Grow(acc/(float)GenDate.TicksPerYear);acc=0;
  }
  public void Grow(float addYears){
   if(years<0f)years=0f;years+=addYears;
   // 到顶以后年数不再往上记（免得改了上限以后一下子跳很多）。
   float per=Ext?.perYear??.5f,m=Max;if(m>0f && per>0f)years=Mathf.Min(years,(m-1f)/per);
   if(Mathf.Abs(Size-shown)>=.01f){shown=Size;Behemoth.Changed(pawn);}
   Behemoth.SyncTrait(pawn,this);
  }
  public override void ExposeData(){base.ExposeData();Scribe_Values.Look(ref years,"lpGrownYears",-1f);}
  public override IEnumerable<StatDrawEntry> SpecialDisplayStats(){
   foreach(var s in base.SpecialDisplayStats()??new List<StatDrawEntry>())yield return s;
   yield return new StatDrawEntry(StatCategoryDefOf.Genetics,"无限生长".Loc(),"×"+Size.ToString("0.00"),"已经生长 ".Loc()+Mathf.Max(0f,years).ToString("0.0")+" 年，现在的体型倍率 ×".Loc()+Size.ToString("0.00")+"。每年 +".Loc()+(Ext?.perYear??.5f).ToString("0.##")+(Max>0f?"，长到 ×".Loc()+Max.ToString("0.##")+" 为止，到顶时获得特性「巨兽」。".Loc():"，没有上限。".Loc()),4000);
  }
 }
 public static class Behemoth {
  public const string Kin="LP_BehemothKin",GrowthGene="LP_EndlessGrowth",TraitName="LP_BehemothTrait";
  public static Gene_EndlessGrowth GeneOf(Pawn p)=>p?.genes?.GetFirstGeneOfType<Gene_EndlessGrowth>();
  public static void Changed(Pawn p){
   if(p==null)return;RaceSize.Invalidate(p);BodySize.Notify_SizeChanged(p);
   if(p.Spawned)p.Drawer?.renderer?.SetAllGraphicsDirty();
  }
  static readonly DefRef<TraitDef> trait=new DefRef<TraitDef>(TraitName);
  public static bool HasTrait(Pawn p)=>trait.Def!=null && p?.story?.traits?.HasTrait(trait.Def)==true;
  // 长到顶：加上「巨兽」；基因没了（g 为空）或还没到顶：去掉。
  public static void SyncTrait(Pawn p,Gene_EndlessGrowth g){
   var d=trait.Def;var set=p?.story?.traits;if(d==null || set==null)return;
   bool want=g!=null && g.Active && g.Full,have=set.HasTrait(d);
   if(want==have)return;
   if(want){
    set.GainTrait(new Trait(d,0,true));
    if(PawnUtility.ShouldSendNotificationAbout(p))Messages.Message(p.LabelShortCap+"长到了头：体型不再变大，成了一头真正的「巨兽」（睡得更久、吃得更多）。".Loc(),p,MessageTypeDefOf.NeutralEvent,false);
   }else{var t=set.GetTrait(d);if(t!=null)set.RemoveTrait(t);}
  }
 }
}
