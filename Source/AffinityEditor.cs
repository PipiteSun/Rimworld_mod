using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace LichPhylactery {
 // 1.166.0 种族亲和编辑（Peter：帮我加入游戏内设置）。
 // 设置“冒险者”里的按钮打开 Dialog_AffinityEditor：列出现在载入的所有异种和人形种族（加上人类 / 巫妖这些特殊条目），
 // 每个可以改六维擅长 / 笨拙、传奇擅长和“传奇种族”。改过的存在模组设置里（affinityOverrides），优先于内置表和 def 上的 LP_AdventurerAffinityExt；
 // “默认”按钮删掉这一条覆盖。以后新装的 mod 不用改代码也能在这里配。
 // 存法：key → "六维状态6位|传奇8位|L"，六维 0 无 / 1 擅长 / 2 笨拙，传奇 0/1，L 为 1 表示传奇种族。
 public partial class Settings {
  public Dictionary<string,string> affinityOverrides=new Dictionary<string,string>();
  void ExposeAffinityOverrides(){
   Scribe_Collections.Look(ref affinityOverrides,"affinityOverrides",LookMode.Value,LookMode.Value);
   if(affinityOverrides==null)affinityOverrides=new Dictionary<string,string>();
   if(Scribe.mode==LoadSaveMode.PostLoadInit || Scribe.mode==LoadSaveMode.LoadingVars)Affinity.InvalidateOverrides();
  }
 }

 public static partial class AffinityOverrides {
  public static string Encode(AdventurerAffinity a){
   var c=new char[6];for(int i=0;i<6;i++)c[i]=a.strong[i]?'1':a.weak[i]?'2':'0';
   var l=new char[Legend.Count];for(int i=0;i<Legend.Count;i++)l[i]=a.legend[i]?'1':'0';
   return new string(c)+"|"+new string(l)+"|"+(a.legendary?"1":"0");
  }
  // 用默认条目的 key / 名字 / legendAll（人类的 ×1.1）做底，覆盖六维、传奇项和传奇种族。
  public static AdventurerAffinity Decode(string key,string code,AdventurerAffinity basis){
   var a=new AdventurerAffinity{key=key,label=basis?.label??key,legendAll=basis?.legendAll??1f};
   var parts=(code??"").Split('|');
   if(parts.Length>0)for(int i=0;i<6 && i<parts[0].Length;i++){a.strong[i]=parts[0][i]=='1';a.weak[i]=parts[0][i]=='2';}
   if(parts.Length>1)for(int i=0;i<Legend.Count && i<parts[1].Length;i++)a.legend[i]=parts[1][i]=='1';
   a.legendary=parts.Length>2 && parts[2]=="1";
   return a;
  }
 }

 public static partial class Affinity {
  static Dictionary<string,AdventurerAffinity> overrides;
  public static void InvalidateOverrides(){overrides=null;}
  static Dictionary<string,AdventurerAffinity> Overrides{get{
   var o=overrides;if(o!=null)return o;
   o=new Dictionary<string,AdventurerAffinity>();var src=LichMod.Settings?.affinityOverrides;
   if(src!=null)foreach(var kv in src){if(kv.Key.NullOrEmpty())continue;var d=KeyDef(kv.Key);o[kv.Key]=AffinityOverrides.Decode(kv.Key,kv.Value,Default(kv.Key,d)??new AdventurerAffinity{key=kv.Key,label=d?.LabelCap.ToString()??CustomName(kv.Key)??kv.Key});}
   overrides=o;return o;
  }}
  // 设置里的覆盖（没有就 null）。返回的对象在设置不变时保持同一个（冒险者按引用比较，换了才刷新）。
  public static AdventurerAffinity Override(string key,Def d=null){
   if(key==null)return null;var o=Overrides;if(o.Count==0)return null;
   return o.TryGetValue(key,out var a)?(d!=null?Named(a,d):a):null;
  }
  // 不看设置的默认条目：def 上的扩展 → 内置表；都没有返回 null（按人类算）。
  public static AdventurerAffinity Default(string key,Def d){
   if(d!=null){var e=FromExt(d);if(e!=null)return e;}
   return key!=null && Table.TryGetValue(key,out var r)?(d!=null?Named(r,d):r):null;
  }
  public static Def KeyDef(string key)=>key!=null && key.StartsWith(CustomPrefix)?null:(Def)DefDatabase<XenotypeDef>.GetNamedSilentFail(key)??DefDatabase<ThingDef>.GetNamedSilentFail(key);
  // 1.175.0 自设异种（原版异种编辑器保存的 CustomXenotype，没有 XenotypeDef）：按名字存，key = "Custom:" + 名字。
  public const string CustomPrefix="Custom:";
  public static string CustomKey(string name)=>name.NullOrEmpty()?null:CustomPrefix+name.Trim();
  public static string CustomName(string key)=>key!=null && key.StartsWith(CustomPrefix)?key.Substring(CustomPrefix.Length):null;
  public static string CustomNameOf(Pawn p){
   var g=p?.genes;if(g==null)return null;
   if(g.UniqueXenotype && !g.xenotypeName.NullOrEmpty())return g.xenotypeName.Trim();
   var c=g.CustomXenotype;return c==null || c.name.NullOrEmpty()?null:c.name.Trim();
  }
  // 现在能找到的自设异种名字：异种文件夹里保存的、本局游戏里记着的、地图和世界上的人身上带的、设置里已经配过的。
  public static List<string> CustomNames(){
   var set=new HashSet<string>();void Add(string n){if(!n.NullOrEmpty())set.Add(n.Trim());}
   try{foreach(var f in GenFilePaths.AllCustomXenotypeFiles)Add(System.IO.Path.GetFileNameWithoutExtension(f.Name));}catch(Exception){}
   try{var db=Current.Game?.customXenotypeDatabase?.customXenotypes;if(db!=null)foreach(var c in db)Add(c?.name);}catch(Exception){}
   try{if(Current.Game!=null)foreach(var p in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)Add(CustomNameOf(p));}catch(Exception){}
   var src=LichMod.Settings?.affinityOverrides;if(src!=null)foreach(var k in src.Keys)Add(CustomName(k));
   return set.OrderBy(n=>n).ToList();
  }
 }

 public class Dialog_AffinityEditor : Window {
  class Row{public string key,label,source;public Def def;}
  List<Row> rows;string filter="";Vector2 scroll;bool onlyChanged;
  public override Vector2 InitialSize=>new Vector2(1080f,720f);
  public Dialog_AffinityEditor(){doCloseX=true;doCloseButton=true;forcePause=true;absorbInputAroundWindow=true;closeOnClickedOutside=false;}

  static List<Row> Build(){
   var list=new Dictionary<string,Row>();
   void Add(string key,Def d,string label=null,string src=null){
    if(key.NullOrEmpty() || list.ContainsKey(key))return;
    list[key]=new Row{key=key,def=d,label=label??d?.LabelCap.ToString()??key,source=src??d?.modContentPack?.Name??"DND边缘大陆".Loc()};
   }
   Add(Affinity.HumanKey,null,"人类（兜底）".Loc(),"—");
   Add(Affinity.LichKey,null,"巫妖".Loc());
   Add(Affinity.HalfIllithidKey,null,"半夺心魔".Loc());
   foreach(var x in DefDatabase<XenotypeDef>.AllDefs)if(x!=XenotypeDefOf.Baseliner)Add(x.defName,x);
   foreach(var t in DefDatabase<ThingDef>.AllDefs)
    if(t.race!=null && t!=ThingDefOf.Human && (t.race.Humanlike || t.GetModExtension<LP_AdventurerAffinityExt>()!=null))Add(t.defName,t);
   foreach(var k in Affinity.Table.Keys)if(!list.ContainsKey(k)){var d=Affinity.KeyDef(k);if(d!=null)Add(k,d);} // 按血脉基因查的条目（MB_*）等
   foreach(var n in Affinity.CustomNames())Add(Affinity.CustomKey(n),null,n,"自设异种".Loc()); // 1.175.0
   return list.Values.OrderBy(r=>r.key==Affinity.HumanKey?0:r.source=="自设异种".Loc()?1:r.source=="DND边缘大陆".Loc()?2:3).ThenBy(r=>r.source).ThenBy(r=>r.label).ToList();
  }

  public static List<string> Keys()=>Build().Select(r=>r.key).ToList();
  static Dictionary<string,string> Store=>LichMod.Settings.affinityOverrides;
  static AdventurerAffinity Current(Row r){
   var o=Affinity.Override(r.key,r.def);if(o!=null)return o;
   if(r.key==Affinity.HumanKey)return Affinity.Human;
   return Affinity.Default(r.key,r.def);
  }
  static void Save(Row r,AdventurerAffinity a){Store[r.key]=AffinityOverrides.Encode(a);Affinity.InvalidateOverrides();}
  static AdventurerAffinity Editable(Row r){
   var c=Current(r);var a=new AdventurerAffinity{key=r.key,label=r.label,legendAll=c?.legendAll??1f,legendary=c?.legendary??false};
   if(c!=null){Array.Copy(c.strong,a.strong,6);Array.Copy(c.weak,a.weak,6);Array.Copy(c.legend,a.legend,Legend.Count);}
   return a;
  }

  const float RowH=28f,NameW=230f,SrcW=150f,AbW=46f,LgW=40f,LegW=46f,ResetW=56f;
  public override void DoWindowContents(Rect inRect){
   if(rows==null)rows=Build();
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(0f,0f,inRect.width,34f),"种族亲和编辑".Loc());Text.Font=GameFont.Small;
   Widgets.Label(new Rect(0f,34f,inRect.width,44f),("点六维格子在 无 → 擅长 → 笨拙 之间切换，点传奇格子切换擅长；“传奇”= 传奇种族（擅长 ×1.75、不食物中毒、不随机稀有天赋）。改过的行名字标黄，“默认”恢复内置。已有冒险者几秒内刷新。".Loc()));
   float y=80f;
   filter=Widgets.TextField(new Rect(0f,y,260f,26f),filter);
   if(filter.NullOrEmpty()){GUI.color=Color.gray;Widgets.Label(new Rect(6f,y+2f,250f,24f),"搜索名字 / mod / defName".Loc());GUI.color=Color.white;}
   Widgets.CheckboxLabeled(new Rect(280f,y,180f,26f),"只看改过的".Loc(),ref onlyChanged);
   var resetAll=new Rect(inRect.width-170f,y,170f,26f);
   if(Store.Count>0 && Widgets.ButtonText(resetAll,"全部恢复默认".Loc()+"（"+Store.Count+"）"))
    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("把 ".Loc()+Store.Count+" 个改过的种族亲和全部恢复默认？".Loc(),()=>{Store.Clear();Affinity.InvalidateOverrides();},true));
   y+=32f;
   // 表头
   float x=NameW+SrcW;Text.Font=GameFont.Tiny;GUI.color=new Color(.8f,.8f,.8f);
   Widgets.Label(new Rect(0f,y,NameW,22f),"种族".Loc());Widgets.Label(new Rect(NameW,y,SrcW,22f),"来源".Loc());
   for(int i=0;i<6;i++){Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(new Rect(x,y,AbW,22f),Adventurer.Names[i]);x+=AbW;}
   x+=8f;for(int i=0;i<Legend.Count;i++){Widgets.Label(new Rect(x,y,LgW,22f),Legend.Names[i]);x+=LgW;}
   x+=8f;Widgets.Label(new Rect(x,y,LegW,22f),"传奇".Loc());Text.Anchor=TextAnchor.UpperLeft;GUI.color=Color.white;Text.Font=GameFont.Small;
   y+=24f;
   var shown=rows.Where(r=>(!onlyChanged || Store.ContainsKey(r.key)) && (filter.NullOrEmpty() || r.label.IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0 || r.key.IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0 || (r.source??"").IndexOf(filter,StringComparison.OrdinalIgnoreCase)>=0)).ToList();
   var outRect=new Rect(0f,y,inRect.width,inRect.height-y-CloseButSize.y-10f);
   var view=new Rect(0f,0f,outRect.width-16f,shown.Count*RowH);
   Widgets.BeginScrollView(outRect,ref scroll,view);
   int first=Mathf.Max(0,(int)(scroll.y/RowH)),last=Mathf.Min(shown.Count,first+(int)(outRect.height/RowH)+2);
   for(int n=first;n<last;n++)DrawRow(shown[n],new Rect(0f,n*RowH,view.width,RowH),n);
   Widgets.EndScrollView();
  }

  void DrawRow(Row r,Rect rect,int n){
   if(n%2==1)Widgets.DrawLightHighlight(rect);
   bool changed=Store.ContainsKey(r.key);var cur=Current(r);
   var nameRect=new Rect(rect.x+4f,rect.y,NameW-4f,rect.height);
   if(changed)GUI.color=new Color(1f,.85f,.4f);
   Text.Anchor=TextAnchor.MiddleLeft;Widgets.Label(nameRect,r.label.Truncate(nameRect.width));GUI.color=Color.white;
   TooltipHandler.TipRegion(nameRect,r.label+(r.key==Affinity.HumanKey?"\n"+"基准人、没配过的自设异种和没有条目的种族都用这一行；所有传奇项另外 ×1.1。".Loc():"")+"\n"+r.key+(Affinity.CustomName(r.key)!=null?"\n"+"自设异种：按异种的名字认，改名后要重新配。没配过时按人类（或血脉基因）算。".Loc():"")+(cur==null?"\n"+"没有条目：按人类算".Loc():"")+(changed?"\n"+"已改（存在模组设置里）".Loc():""));
   GUI.color=Color.gray;Widgets.Label(new Rect(rect.x+NameW,rect.y,SrcW-4f,rect.height),(r.source??"").Truncate(SrcW-4f));GUI.color=Color.white;
   Text.Anchor=TextAnchor.MiddleCenter;
   float x=rect.x+NameW+SrcW;
   for(int i=0;i<6;i++){
    var cell=new Rect(x+2f,rect.y+3f,AbW-4f,rect.height-6f);
    bool s=cur!=null && cur.strong[i],w=cur!=null && cur.weak[i];
    Widgets.DrawBoxSolid(cell,s?new Color(.2f,.45f,.2f):w?new Color(.5f,.2f,.2f):new Color(.18f,.18f,.18f));
    Widgets.Label(cell,s?"擅长".Loc():w?"笨拙".Loc():"·");
    if(Widgets.ButtonInvisible(cell)){var a=Editable(r);if(a.strong[i]){a.strong[i]=false;a.weak[i]=true;}else if(a.weak[i])a.weak[i]=false;else a.strong[i]=true;Save(r,a);}
    x+=AbW;
   }
   x+=8f;
   for(int i=0;i<Legend.Count;i++){
    var cell=new Rect(x+2f,rect.y+3f,LgW-4f,rect.height-6f);bool on=cur!=null && cur.legend[i];
    Widgets.DrawBoxSolid(cell,on?new Color(.45f,.38f,.12f):new Color(.18f,.18f,.18f));
    Widgets.Label(cell,on?"★":"·");
    if(Widgets.ButtonInvisible(cell)){var a=Editable(r);a.legend[i]=!a.legend[i];Save(r,a);}
    x+=LgW;
   }
   x+=8f;
   bool leg=cur?.legendary??false,was=leg;
   Widgets.Checkbox(new Vector2(x+(LegW-24f)/2f,rect.y+2f),ref leg);
   if(leg!=was){var a=Editable(r);a.legendary=leg;Save(r,a);}
   x+=LegW+6f;
   Text.Anchor=TextAnchor.UpperLeft;
   if(changed && Widgets.ButtonText(new Rect(x,rect.y+2f,ResetW,rect.height-4f),"默认".Loc())){Store.Remove(r.key);Affinity.InvalidateOverrides();}
  }

  public override void PostClose(){base.PostClose();try{LoadedModManager.GetMod<LichMod>()?.WriteSettings();}catch(Exception e){Log.Warning("[LichPhylactery] affinity editor save: "+e.Message);}}
 }
}
