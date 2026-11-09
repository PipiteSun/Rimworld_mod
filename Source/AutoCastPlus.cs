using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LichPhylactery {
 // 1.136.0 便利一：施法自动化与资源条。
 // 每个能自动放的 DND 能力：右上角 15 像素小角标：左键 = 征召时（和战斗中）自动施放（1.84.0 原有），右键打开设置窗口（1.136.0 热修：原来左上勾 + 右上齿轮太大，挡住图标）：
 // 「未征召时自动施放」（只给治疗 / 增益 / 防御类，即非敌对能力）和「资源低于 X% 不自动用」（0～90%）。设置存在 ClassData.autoModes（按人按能力）。
 // 殖民者头像下面一条细资源条：主资源（法力 / 斗志 / 神恩 / 气 / 乐韵 / 契约法力 / 怒气 / 法术能量 / 湖息），设置可关。
 public static class AutoCastPlus {
  public const int FlagUndrafted=1;
  static ClassData Data(Pawn p)=>Adventurer.Get(p)?.classes;
  static int Raw(Pawn p,AbilityDef d){var c=Data(p);if(c?.autoModes==null || d==null)return 0;c.autoModes.TryGetValue(d.defName,out int v);return v;}
  static void Store(Pawn p,AbilityDef d,int v){var c=Data(p);if(c==null || d==null)return;if(c.autoModes==null)c.autoModes=new Dictionary<string,int>();if(v==0)c.autoModes.Remove(d.defName);else c.autoModes[d.defName]=v;}
  public static bool Undrafted(Pawn p,AbilityDef d)=>(Raw(p,d)&FlagUndrafted)!=0;
  public static int Threshold(Pawn p,AbilityDef d)=>Mathf.Clamp(Raw(p,d)>>8,0,90);
  public static void SetUndrafted(Pawn p,AbilityDef d,bool on){int v=Raw(p,d);v=on?v|FlagUndrafted:v&~FlagUndrafted;Store(p,d,v);}
  public static void SetThreshold(Pawn p,AbilityDef d,int pct){int v=Raw(p,d)&0xFF;v|=Mathf.Clamp(pct,0,90)<<8;Store(p,d,v);}
  // 治疗 / 增益 / 防御：原版意义上不是敌对的能力（目标是自己或友军）。
  public static bool Helpful(Ability_Class a)=>a?.def!=null && !a.def.hostile;
  public static bool ResourceOk(Hediff_Adventurer h,Ability_Class a){
   int t=Threshold(h.pawn,a.def);if(t<=0)return true;
   var r=Main(h.pawn,a.def);if(r.max<=0f)return true;
   return r.cur/r.max*100f>=t-.001f;
  }
  // 没征召、也没在打仗时：醒着、没有玩家指派的工作、不在床上、不在精神崩溃。
  public static bool ReadyCalm(Pawn p){
   if(p==null || !p.Spawned || p.Map==null || p.Dead || p.Downed || p.Drafted || p.InMentalState || !p.Awake() || p.stances?.stunner?.Stunned==true)return false;
   var j=p.CurJob;if(j?.ability!=null || j!=null && j.playerForced)return false;
   return !p.InBed();
  }

  // ---------- 主资源 ----------
  public struct Res{public string label;public float cur,max;public Color color;}
  public static Color ColorOf(ClassResource r){switch(r){case ClassResource.Mana:return new Color(.4f,.6f,1f);case ClassResource.Fury:return new Color(.95f,.55f,.35f);case ClassResource.Grace:return new Color(1f,.85f,.45f);case ClassResource.Ki:return new Color(.55f,.9f,.75f);case ClassResource.Melody:return new Color(.95f,.55f,.85f);case ClassResource.Pact:return new Color(.7f,.45f,.9f);case ClassResource.Rage:return new Color(.9f,.3f,.3f);}return Color.gray;}
  // 某个能力用的资源（def 为空 = 殖民者的主资源：等级最高、有资源的职业；没有就法术能量；再没有就湖息）。
  public static Res Main(Pawn p,AbilityDef d=null){
   var h=Adventurer.Get(p);var res=new Res{label="",cur=0f,max=0f,color=Color.gray};if(h?.classes==null)return res;
   LP_ClassDef c=d!=null?ClassSystem.CastClassOf(p,d):null;
   if(d!=null && LakeSeals.Is(d)){res.label="湖息";res.cur=LakeSeals.Breath(h);res.max=LakeSeals.BreathMax(h);res.color=LakeSeals.Teal;return res;}
   if(c==null && d==null)c=ClassSystem.Taken(h).Where(x=>x.resource!=ClassResource.None).OrderByDescending(x=>ClassSystem.Level(h,x)).FirstOrDefault();
   if(c!=null && c.resource!=ClassResource.None){res.label=ClassSystem.ResourceLabel(c.resource);res.cur=ClassSystem.Resource(h,c);res.max=ClassSystem.MaxResource(h,c);res.color=ColorOf(c.resource);return res;}
   var l=(c!=null?CasterSpells.ListOf(c):null)??CasterSpells.ListsOf(p).FirstOrDefault(x=>x.UsesEnergy);
   if(l!=null && l.UsesEnergy){res.label="法术能量";res.cur=CasterSpells.EnergyLeft(h,l);res.max=CasterSpells.EnergyMax(h,l);res.color=new Color(.55f,.8f,1f);return res;}
   if(d==null && h.classes.lake!=null && h.classes.lake.seals.Count>0){res.label="湖息";res.cur=LakeSeals.Breath(h);res.max=LakeSeals.BreathMax(h);res.color=LakeSeals.Teal;}
   return res;
  }
  // 头像资源条的缓存（每个人 30 帧算一次；只在界面线程用）。
  static readonly Dictionary<int,(int frame,Res r)> cache=new Dictionary<int,(int,Res)>();
  public static Res Cached(Pawn p){int f=Time.frameCount;if(cache.TryGetValue(p.thingIDNumber,out var v) && f-v.frame<30)return v.r;var r=Main(p);cache[p.thingIDNumber]=(f,r);return r;}
  public static bool BarOn=>LichMod.Settings?.resourceBar??true;
  static Texture2D bg;static Texture2D Bg=>bg??(bg=SolidColorMaterials.NewSolidColorTexture(new Color(0f,0f,0f,.6f)));
  static readonly Dictionary<Color,Texture2D> fills=new Dictionary<Color,Texture2D>();
  static Texture2D Fill(Color c){if(!fills.TryGetValue(c,out var t)){t=SolidColorMaterials.NewSolidColorTexture(c);fills[c]=t;}return t;}
  public static void DrawBar(Rect rect,Pawn p){
   if(!BarOn || p==null || !Adventurer.Enabled)return;var r=Cached(p);if(r.max<=0f)return;
   var bar=new Rect(rect.x+3f,rect.yMax-5f,rect.width-6f,4f);
   GUI.DrawTexture(bar,Bg);GUI.DrawTexture(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01(r.cur/r.max),bar.height),Fill(r.color));
   if(Mouse.IsOver(bar.ExpandedBy(0f,3f)))TooltipHandler.TipRegion(bar.ExpandedBy(0f,3f),r.label+" "+r.cur.ToString("0.#")+" / "+r.max.ToString("0.#"));
  }
 }
 [HarmonyPatch(typeof(ColonistBarColonistDrawer),nameof(ColonistBarColonistDrawer.DrawColonist))]
 public static class ColonistResourceBar {
  static void Postfix(Rect rect,Pawn colonist){try{AutoCastPlus.DrawBar(rect,colonist);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 资源条："+e.Message,0x13601);}}
 }

 // 自动施放设置窗口（不用浮动菜单）。
 public class Dialog_AutoCast : Window {
  readonly Ability_Class a;
  public Dialog_AutoCast(Ability_Class a){this.a=a;doCloseX=true;doCloseButton=true;absorbInputAroundWindow=true;closeOnClickedOutside=true;}
  public override Vector2 InitialSize=>LPUI.Fit(460f,330f);
  public override void DoWindowContents(Rect r){
   try{
    float y=LPUI.Title(r,"自动施放："+a.def.LabelCap);
    var p=a.pawn;
    bool d=ClassAutoCast.IsOn(a);var r1=new Rect(r.x,y,r.width,28f);bool d2=d;Widgets.CheckboxLabeled(r1,"征召时自动施放（征召待命或战斗中）",ref d2);if(d2!=d)ClassAutoCast.Toggle(a);y+=32f;
    bool help=AutoCastPlus.Helpful(a);var r2=new Rect(r.x,y,r.width,28f);
    if(help){bool u=AutoCastPlus.Undrafted(p,a.def),u2=u;Widgets.CheckboxLabeled(r2,"未征召时自动施放（平时也会给受伤 / 需要的人放）",ref u2);if(u2!=u)AutoCastPlus.SetUndrafted(p,a.def,u2);}
    else LPUI.Unavailable(r2,"攻击类能力不能在未征召时自动施放");
    y+=34f;
    int t=AutoCastPlus.Threshold(p,a.def);var res=AutoCastPlus.Main(p,a.def);
    LPUI.LabelFit(new Rect(r.x,y,r.width,24f),"资源低于 "+t+"% 不自动用"+(res.max>0f?"（"+res.label+" 现在 "+res.cur.ToString("0.#")+" / "+res.max.ToString("0.#")+"）":"（这个能力不耗资源）"));y+=26f;
    var row=LPUI.Row(new Rect(r.x,y,r.width,28f),10,3f);
    for(int i=0;i<10;i++){int v=i*10;if(Widgets.ButtonText(row[i],v+"%",true,true,t!=v))AutoCastPlus.SetThreshold(p,a.def,v);}
    y+=34f;
    LPUI.Tiny(new Rect(r.x,y,r.width,40f),"选中的人只改这一个能力。征召时的开关也可以直接左键点按钮右上角的小角标，右键角标打开这个窗口。",true);
   }finally{LPUI.Reset();}
  }
 }
}
