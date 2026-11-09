using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.136.0 便利二：怪物图鉴。殖民地记录击杀 / 观察过的本模组怪物、野兽、传奇兽、真龙（存档保存）。
 // 击杀 1 = 名称简介，3 = 弱点与抗性，10 = 常见掉落，25 = 传说，并且殖民地对这种生物的伤害 +5%。主界面按钮「怪物图鉴」打开窗口，按类别筛选。
 public static class Bestiary {
  public const int TierName=1,TierWeak=3,TierDrops=10,TierLore=25;public const float LoreDamage=1.05f;
  public static readonly string[] WildMonsterKinds={"LP_RustMonster","LP_GelatinousCube","LP_Basilisk","LP_Manticore","LP_Bulette","LP_CarrionCrawler"};
  public static readonly string[] Cats={"全部","怪物","野兽","传奇兽","真龙"};
  public static GameComponent_Bestiary State=>Current.Game?.GetComponent<GameComponent_Bestiary>();
  static readonly Dictionary<PawnKindDef,bool> eligible=new Dictionary<PawnKindDef,bool>();
  // 本模组（LP_）的非人形生物，以及传奇单位（贝希尔、眼魔等，可能是 HAR 人形）和真龙。
  public static bool Eligible(Pawn p){
   if(p?.kindDef==null || p.RaceProps==null)return false;
   if(Legendary.Is(p))return true;
   if(eligible.TryGetValue(p.kindDef,out bool v))return v;
   var k=p.kindDef;v=k.defName.StartsWith("LP_") && !p.RaceProps.Humanlike && (p.RaceProps.Animal || p.RaceProps.IsFlesh || p.RaceProps.IsMechanoid) && !k.defName.Contains("Familiar") && !k.defName.Contains("Conjured");
   eligible[k]=v;return v;
  }
  public static int Category(Pawn p){
   var n=p.kindDef.defName;if(n.Contains("Dragon"))return 4;if(Legendary.Is(p))return 3;
   if(Array.IndexOf(WildMonsterKinds,n)>=0)return 1;return p.RaceProps.Animal?2:1;
  }
  public static void Record(Pawn p,bool kill){
   var st=State;if(st==null || !Eligible(p))return;string k=p.kindDef.defName;
   if(!st.cat.ContainsKey(k))st.cat[k]=Category(p);
   if(kill){st.kills.TryGetValue(k,out int n);st.kills[k]=n+1;if(n+1==TierWeak || n+1==TierDrops || n+1==TierLore)Unlocked(p.kindDef,n+1);}
   else if(!st.seen.Contains(k))st.seen.Add(k);
  }
  static void Unlocked(PawnKindDef k,int n){if(Adventurer.TestSuppressed)return;Messages.Message("怪物图鉴：「"+k.LabelCap+"」击杀 "+n+" 只，解锁了"+(n==TierWeak?"弱点与抗性":n==TierDrops?"常见掉落":"传说（殖民地对它的伤害 +5%）")+"。",MessageTypeDefOf.PositiveEvent,false);}
  public static int Kills(string kind){var st=State;if(st==null || kind==null)return 0;st.kills.TryGetValue(kind,out int n);return n;}
  public static bool Lore(Pawn v)=>v?.kindDef!=null && State!=null && State.kills.Count>0 && Kills(v.kindDef.defName)>=TierLore;
  public static IEnumerable<string> Known(int cat){var st=State;if(st==null)return Enumerable.Empty<string>();return st.cat.Keys.Where(k=>cat==0 || st.cat[k]==cat).Where(k=>DefDatabase<PawnKindDef>.GetNamedSilentFail(k)!=null).OrderBy(k=>st.cat[k]).ThenByDescending(Kills);}

  // ---------- 条目文字 ----------
  public static string Weakness(PawnKindDef k){
   var r=k.race;if(r==null)return "";
   float sh=r.GetStatValueAbstract(StatDefOf.ArmorRating_Sharp),bl=r.GetStatValueAbstract(StatDefOf.ArmorRating_Blunt),he=r.GetStatValueAbstract(StatDefOf.ArmorRating_Heat),fl=r.GetStatValueAbstract(StatDefOf.Flammability);
   var parts=new List<(string n,float v)>{("利器",sh),("钝器",bl),("热能",he)};var best=parts.OrderByDescending(x=>x.v).First();var worst=parts.OrderBy(x=>x.v).First();
   var lines=new List<string>{"护甲：利器 "+sh.ToStringPercent()+"、钝器 "+bl.ToStringPercent()+"、热能 "+he.ToStringPercent(),"抗性：最擅长抵挡"+best.n+"伤害","弱点："+(fl>=.5f?"怕火（可燃性 "+fl.ToStringPercent()+"）":"最怕"+worst.n+"伤害")};
   if(r.race.IsMechanoid)lines.Add("机械体：EMP 有效，不怕毒和心灵");
   lines.Add("体型 "+r.race.baseBodySize.ToString("0.##")+"，战斗力 "+k.combatPower.ToString("0")+"，移动 "+r.GetStatValueAbstract(StatDefOf.MoveSpeed).ToString("0.#")+" 格 / 秒");
   return string.Join("\n",lines);
  }
  public static string Drops(PawnKindDef k){
   var r=k.race;if(r==null)return "";var l=new List<string>();
   if(r.race.meatDef!=null && r.race.IsFlesh)l.Add(r.race.meatDef.label);
   if(r.race.leatherDef!=null)l.Add(r.race.leatherDef.label);
   if(r.butcherProducts!=null)foreach(var t in r.butcherProducts)l.Add(t.thingDef.label+" ×"+t.count);
   if(r.killedLeavings!=null)foreach(var t in r.killedLeavings)l.Add(t.thingDef.label+" ×"+t.count);
   return l.Count==0?"没有特别的掉落":string.Join("、",l.Distinct());
  }
  public static string LoreText(PawnKindDef k)=>"冒险者们口耳相传："+k.label+"的故事里，最危险的总是第一次遇见它的时候。殖民地已经和它交手了 "+Kills(k.defName)+" 次，摸透了它的习性——对"+k.label+"造成的伤害 +5%。";
 }

 public class GameComponent_Bestiary : GameComponent {
  public Dictionary<string,int> kills=new Dictionary<string,int>(),cat=new Dictionary<string,int>();public List<string> seen=new List<string>();
  public GameComponent_Bestiary(Game g){}
  public override void GameComponentTick(){
   if(Find.TickManager.TicksGame%2500!=17)return;
   foreach(var m in Find.Maps){if(!m.IsPlayerHome && m.mapPawns.FreeColonistsSpawnedCount==0)continue;foreach(var p in m.mapPawns.AllPawnsSpawned)if(p.Faction!=Faction.OfPlayer && !p.Position.Fogged(m) && Bestiary.Eligible(p))Bestiary.Record(p,false);}
  }
  public override void ExposeData(){
   base.ExposeData();Scribe_Collections.Look(ref kills,"lpBestKills",LookMode.Value,LookMode.Value);Scribe_Collections.Look(ref cat,"lpBestCat",LookMode.Value,LookMode.Value);Scribe_Collections.Look(ref seen,"lpBestSeen",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(kills==null)kills=new Dictionary<string,int>();if(cat==null)cat=new Dictionary<string,int>();if(seen==null)seen=new List<string>();}
  }
 }
 // 击杀：玩家阵营的人或炮台打死的才算。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.Kill))]
 public static class BestiaryKill {
  static void Prefix(Pawn __instance,DamageInfo? dinfo,out bool __state){__state=!__instance.Dead;}
  static void Postfix(Pawn __instance,DamageInfo? dinfo,bool __state){
   if(!__state || !__instance.Dead || dinfo?.Instigator?.Faction!=Faction.OfPlayer)return;
   try{Bestiary.Record(__instance,true);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 怪物图鉴："+e.Message,0x13602);}
  }
 }
 // 传说：殖民地对这种生物的伤害 +5%。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.PreApplyDamage))]
 public static class BestiaryDamage {
  static void Prefix(Pawn __instance,ref DamageInfo dinfo){if(dinfo.Amount>0f && dinfo.Instigator?.Faction==Faction.OfPlayer && Bestiary.Lore(__instance))dinfo.SetAmount(dinfo.Amount*Bestiary.LoreDamage);}
 }

 public class MainTabWindow_Bestiary : MainTabWindow {
  int cat;string sel;Vector2 scrollL,scrollR;float rightH=400f;
  public override Vector2 RequestedTabSize=>LPUI.Fit(900f,640f);
  public override void DoWindowContents(Rect r){
   try{
    float y=LPUI.Title(r,"怪物图鉴",false);var st=Bestiary.State;if(st==null)return;
    var tabs=LPUI.Row(new Rect(r.x,y,Mathf.Min(r.width,520f),LPUI.ButtonH),Bestiary.Cats.Length,4f);
    for(int i=0;i<tabs.Length;i++)if(LPUI.Button(tabs[i],Bestiary.Cats[i]+"（"+Bestiary.Known(i).Count()+"）",cat!=i))cat=i;
    y+=LPUI.ButtonH+LPUI.Gap;
    LPUI.Tiny(new Rect(r.x,y,r.width,20f),"击杀 1 只解锁名称简介，3 只弱点与抗性，10 只常见掉落，25 只传说（殖民地对它的伤害 +5%）。遇见过但没打死的也会记下名字。",true);y+=22f;
    var list=Bestiary.Known(cat).ToList();
    var left=new Rect(r.x,y,Mathf.Min(300f,r.width*.38f),r.yMax-y);var right=new Rect(left.xMax+LPUI.SectionGap,y,r.width-left.width-LPUI.SectionGap,r.yMax-y);
    if(list.Count==0){LPUI.Unavailable(new Rect(left.x,left.y,r.width,LPUI.RowH*2f),"还没有遇见过这一类的生物。");return;}
    if(sel==null || !list.Contains(sel))sel=list[0];
    var view=new Rect(0f,0f,left.width-LPUI.ScrollBarW,list.Count*(LPUI.ListRowH+14f));
    Widgets.BeginScrollView(left,ref scrollL,view);
    try{float yy=0f;foreach(var k in list){var d=DefDatabase<PawnKindDef>.GetNamedSilentFail(k);var row=new Rect(0f,yy,view.width,LPUI.ListRowH+10f);LPUI.RowBg(row,k==sel);
      if(d.race?.uiIcon!=null)LPUI.Icon(new Rect(row.x+4f,row.y+4f,30f,30f),d.race.uiIcon,Bestiary.Kills(k)==0);
      LPUI.LabelFit(new Rect(row.x+40f,row.y+2f,row.width-44f,22f),d.LabelCap);LPUI.Tiny(new Rect(row.x+40f,row.y+22f,row.width-44f,16f),Bestiary.Cats[st.cat[k]]+" · 击杀 "+Bestiary.Kills(k),true);
      if(Widgets.ButtonInvisible(row)){sel=k;scrollR=Vector2.zero;}yy+=LPUI.ListRowH+14f;}}
    finally{Widgets.EndScrollView();}
    DrawEntry(right,DefDatabase<PawnKindDef>.GetNamedSilentFail(sel));
   }finally{LPUI.Reset();}
  }
  void DrawEntry(Rect outer,PawnKindDef k){
   if(k==null)return;int n=Bestiary.Kills(k.defName);
   var view=new Rect(0f,0f,outer.width-LPUI.ScrollBarW,Mathf.Max(rightH,outer.height-1f));
   Widgets.BeginScrollView(outer,ref scrollR,view);
   try{
    float w=view.width,y=0f;if(k.race?.uiIcon!=null)LPUI.Icon(new Rect(0f,0f,64f,64f),k.race.uiIcon,n==0);
    Text.Font=GameFont.Medium;LPUI.LabelFit(new Rect(72f,4f,w-72f,32f),k.LabelCap);Text.Font=GameFont.Small;
    LPUI.Tiny(new Rect(72f,38f,w-72f,20f),Bestiary.Cats[Bestiary.State.cat.TryGetValue(k.defName,out int c)?c:1]+" · 击杀 "+n+" 只",true);y=70f;
    void Section(int need,string title,string body){
     bool open=n>=need;y=LPUI.Bar(new Rect(0f,y,w,LPUI.RowH),title+(open?"":"（击杀 "+need+" 只解锁，还差 "+(need-n)+"）"),open?LPUI.Gold:LPUI.Locked);
     if(open)y+=LPUI.Para(4f,y,w-4f,body)+LPUI.SectionGap;else y+=LPUI.Gap;
    }
    Section(Bestiary.TierName,"简介",k.race?.description??k.description??"");
    Section(Bestiary.TierWeak,"弱点与抗性",Bestiary.Weakness(k));
    Section(Bestiary.TierDrops,"常见掉落",Bestiary.Drops(k));
    Section(Bestiary.TierLore,"传说",Bestiary.LoreText(k));
    rightH=y+LPUI.Gap;
   }finally{Widgets.EndScrollView();}
  }
 }
}
