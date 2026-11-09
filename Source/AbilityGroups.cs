using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LichPhylactery {
 // 1.137.0 技能分组（Peter：高等级的人能力太多，按钮栏挤满了）。
 // 玩家给每个人自己建分组（起名、给每个能力勾选属于哪几组，一个能力可以在多个组里），技能栏上一个「技能分组」按钮切换当前组：
 // 「全部」= 原样显示；选了某一组时，按钮栏只显示这组里的能力（只过滤原版能力按钮 Command_Ability，其他按钮不动）。
 // 只影响显示：不在当前组里的能力照常冷却，开了自动施放的照样会自动放（自动施放走能力本身，不经过按钮）。
 // 数据存在 GameComponent_AbilityGroups（按 pawn 的 thingIDNumber，不存引用；没有分组的人不存）。
 // 按钮栏热路径：每个选中的人每帧一次字典查找；当前组的能力集合（HashSet<AbilityDef>）只在改分组时重建。
 public class AbilityGroup : IExposable {
  public string name="";
  public List<string> defs=new List<string>(); // 存 defName：卸掉的模组的能力名字留着，重新启用后还在原来的组里
  HashSet<AbilityDef> set;
  public HashSet<AbilityDef> Set{get{
   if(set==null){set=new HashSet<AbilityDef>();foreach(var n in defs){var d=DefDatabase<AbilityDef>.GetNamedSilentFail(n);if(d!=null)set.Add(d);}}
   return set;
  }}
  HashSet<string> keys;
  public bool Has(AbilityDef d)=>d!=null && Set.Contains(d);
  public void Set_(AbilityDef d,bool on){if(d!=null)SetKey(d.defName,on);}
  // 1.139.0 VPE 灵能：别的能力系统用带前缀的键（"VEF:" + defName），原版能力的键就是 defName。
  public bool HasKey(string k)=>k!=null && (keys??(keys=new HashSet<string>(defs))).Contains(k);
  public void SetKey(string k,bool on){
   if(k.NullOrEmpty())return;
   if(on){if(!defs.Contains(k))defs.Add(k);}else defs.Remove(k);
   set=null;keys=null;
  }
  public int Count=>Set.Count+(AbilityGroupsExt.Entries==null?0:defs.Count(x=>x.StartsWith(AbilityGroupsExt.VefPrefix)));
  public string Label=>name.NullOrEmpty()?"未命名":name;
  public void ExposeData(){
   Scribe_Values.Look(ref name,"name","");Scribe_Collections.Look(ref defs,"defs",LookMode.Value);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(name==null)name="";if(defs==null)defs=new List<string>();defs.RemoveAll(x=>x.NullOrEmpty());set=null;keys=null;}
  }
 }
 public class AbilityGroupSet : IExposable {
  public int pawnId;public int active=-1; // -1 = 全部
  public List<AbilityGroup> groups=new List<AbilityGroup>();
  public AbilityGroup Active=>active>=0 && active<groups.Count?groups[active]:null;
  public void ExposeData(){
   Scribe_Values.Look(ref pawnId,"pawnId");Scribe_Values.Look(ref active,"active",-1);Scribe_Collections.Look(ref groups,"groups",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(groups==null)groups=new List<AbilityGroup>();groups.RemoveAll(g=>g==null);if(active>=groups.Count)active=-1;}
  }
 }
 public class GameComponent_AbilityGroups : GameComponent {
  public static GameComponent_AbilityGroups Instance;
  public List<AbilityGroupSet> sets=new List<AbilityGroupSet>();
  Dictionary<int,AbilityGroupSet> index;
  public GameComponent_AbilityGroups(Game game){Instance=this;}
  public override void ExposeData(){
   base.ExposeData();
   if(Scribe.mode==LoadSaveMode.Saving)sets.RemoveAll(s=>s==null || s.groups.Count==0);
   Scribe_Collections.Look(ref sets,"sets",LookMode.Deep);
   if(Scribe.mode==LoadSaveMode.PostLoadInit){if(sets==null)sets=new List<AbilityGroupSet>();sets.RemoveAll(s=>s==null);index=null;Instance=this;}
  }
  // 不在读档时清理已经不在的人：冬眠舱、运输舱、重力船里的人不一定都能找全，宁可留几条没用的小记录也不丢玩家的分组。
  public AbilityGroupSet Get(int id,bool create){
   if(index==null){index=new Dictionary<int,AbilityGroupSet>();foreach(var s in sets)index[s.pawnId]=s;}
   if(index.TryGetValue(id,out var r))return r;
   if(!create)return null;
   r=new AbilityGroupSet{pawnId=id};sets.Add(r);index[id]=r;return r;
  }
 }

 // 窗口里的一行：原版能力（def 不为空）或别的能力系统（VPE 灵能，由联动程序集提供）。
 public class AbilityGroupEntry {
  public string key,label,desc,tag;public AbilityDef def;public Texture icon;public bool hidden;public int sort;
 }
 // 1.139.0 VPE 联动（Bridges/VPE/Source/AbilityGroupsVPE.cs）挂进来的钩子；没装 VPE 时都是 null。
 public static class AbilityGroupsExt {
  public const string VefPrefix="VEF:";
  public static Func<Gizmo,string> GizmoKey;                       // 按钮 → 键；不是这个系统的按钮返回 null
  public static Func<Pawn,IEnumerable<AbilityGroupEntry>> Entries; // 这个人会的能力
  public static Func<Pawn,int> Count;                               // 能力数（决定要不要显示切换按钮）
 }
 [StaticConstructorOnStartup]
 public static class AbilityGroups {
  public const int MinAbilitiesForSwitch=6; // 没建分组时，能力至少这么多才显示切换按钮（少的人不多一个按钮）
  public const int MaxGroups=20,MaxName=16;
  public static readonly Texture2D Icon=ContentFinder<Texture2D>.Get("LP/Classes/AbilityGroups",false)??BaseContent.BadTex;
  static GameComponent_AbilityGroups C=>Current.Game==null?null:GameComponent_AbilityGroups.Instance;
  public static bool Player(Pawn p)=>p!=null && !p.Dead && p.Faction!=null && p.Faction.IsPlayer;
  public static AbilityGroupSet Get(Pawn p,bool create=false)=>p==null?null:C?.Get(p.thingIDNumber,create);
  // 按钮栏用：当前组的能力集合；显示全部时为 null。
  public static HashSet<AbilityDef> Filter(Pawn p)=>Get(p)?.Active?.Set;
  public static string ActiveLabel(Pawn p)=>Get(p)?.Active?.Label??"全部";
  public static bool ShowSwitch(Pawn p,AbilityGroupSet s){
   if(!Player(p) || p.abilities==null)return false;
   if(s!=null && s.groups.Count>0)return true;
   int n=p.abilities.AllAbilitiesForReading.Count;if(n>=MinAbilitiesForSwitch)return true;
   try{if(AbilityGroupsExt.Count!=null)n+=AbilityGroupsExt.Count(p);}catch{}
   return n>=MinAbilitiesForSwitch;
  }
  // 左键：全部 → 第 1 组 → … → 最后一组 → 全部。
  public static void Cycle(Pawn p,int dir=1){
   var s=Get(p);if(s==null || s.groups.Count==0){Open(p);return;}
   int n=s.groups.Count+1;int cur=s.active+1;cur=((cur+dir)%n+n)%n;s.active=cur-1;
  }
  public static void SetActive(Pawn p,int i){var s=Get(p,true);s.active=i>=-1 && i<s.groups.Count?i:-1;}
  public static AbilityGroup Add(Pawn p,string name=null){
   var s=Get(p,true);if(s.groups.Count>=MaxGroups)return null;
   var g=new AbilityGroup{name=name??NextName(s)};s.groups.Add(g);return g;
  }
  static string NextName(AbilityGroupSet s){for(int i=s.groups.Count+1;;i++){string n="分组 "+i;if(!s.groups.Any(g=>g.name==n))return n;}}
  public static void Remove(Pawn p,int i){
   var s=Get(p);if(s==null || i<0 || i>=s.groups.Count)return;
   s.groups.RemoveAt(i);if(s.active==i)s.active=-1;else if(s.active>i)s.active--;
  }
  public static void Move(Pawn p,int i,int dir){
   var s=Get(p);if(s==null)return;int j=i+dir;if(i<0 || j<0 || i>=s.groups.Count || j>=s.groups.Count)return;
   var t=s.groups[i];s.groups[i]=s.groups[j];s.groups[j]=t;
   if(s.active==i)s.active=j;else if(s.active==j)s.active=i;
  }
  public static void Open(Pawn p){if(p!=null)Find.WindowStack.Add(new Dialog_AbilityGroups(p));}
  // 法术书 / 法术表 / 职业窗口标题栏右边的按钮。
  public static void WindowButton(Rect r,Pawn p,float rightPad=24f){
   if(!Player(p) || p.abilities==null)return;
   var b=new Rect(r.xMax-rightPad-118f,r.y+2f,118f,28f);
   if(LPUI.Button(b,"技能分组…",true,"给这个人的能力建分组（如「战斗」「治疗」「赶路」），技能栏上的「技能分组」按钮切换后只显示那一组的能力。\n\n当前技能栏显示："+ActiveLabel(p)))Open(p);
  }
  // 窗口里列出的能力：这个人会的、有按钮的（被动职业能力没有按钮，不列）。
  public static List<AbilityGroupEntry> Listable(Pawn p){
   var l=new List<AbilityGroupEntry>();if(p?.abilities==null)return l;var seen=new HashSet<AbilityDef>();
   foreach(var a in p.abilities.AllAbilitiesForReading){
    if(a?.def==null || !seen.Add(a.def))continue;
    if(a is Ability_Class ac && ac.Props?.passive==true)continue;
    bool hidden=false;try{hidden=Spellbook.Hidden(p,a.def);}catch{}
    string tag=Tag(p,a);int circle=-1;try{circle=CasterSpells.CircleFor(p,a.def);}catch{}
    l.Add(new AbilityGroupEntry{key=a.def.defName,def=a.def,label=a.def.LabelCap,desc=a.def.description,icon=a.def.uiIcon,tag=tag,hidden=hidden,sort=circle});
   }
   if(AbilityGroupsExt.Entries!=null){
    try{foreach(var e in AbilityGroupsExt.Entries(p))if(e?.key!=null)l.Add(e);}
    catch(Exception ex){Log.ErrorOnce("[DND边缘大陆] 技能分组（VPE）："+ex.Message,0x13703);}
   }
   return l;
  }
  public static string Tag(Pawn p,Ability a){
   try{
    var c=ClassSystem.CastClassOf(p,a.def);if(c!=null)return c.label;
   }catch{}
   if(a is Psycast)return "心灵能力";
   return "其他";
  }
 }

 // 按钮栏过滤 + 切换按钮。最后跑（其他补丁加的能力按钮也一起过滤）。
 [HarmonyPatch(typeof(Pawn),nameof(Pawn.GetGizmos))]
 public static class AbilityGroupGizmos {
  [HarmonyPriority(Priority.Last)]
  static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result,Pawn __instance){
   var p=__instance;var s=AbilityGroups.Player(p)?AbilityGroups.Get(p):null;var grp=s?.Active;var f=grp?.Set;var ext=f!=null?AbilityGroupsExt.GizmoKey:null;
   var Seen=f!=null?new HashSet<AbilityDef>():null; // 只有选了分组时才分配
   foreach(var g in __result){
    if(f!=null){
     if(g is Command_Ability ca){if(ca.Ability?.def!=null){if(!f.Contains(ca.Ability.def))continue;Seen.Add(ca.Ability.def);}}
     else if(ext!=null){string k=null;try{k=ext(g);}catch{}if(k!=null && !grp.HasKey(k))continue;} // 1.139.0 VPE 灵能按钮
    }
    yield return g;
   }
   // 1.137.0 热修（Peter：分组里的技能没全显示）：原版没征召时只显示 displayGizmoWhileUndrafted 的能力（战斗增益之类要征召才出现）。
   // 选了分组时，组里的能力没征召也显示按钮；要征召才能放的照原版灰掉并提示「需要征召」。
   if(f!=null && p.Spawned && p.drafter!=null && !p.Drafted && p.abilities!=null){
    var list=p.abilities.AllAbilitiesForReading;
    for(int i=0;i<list.Count;i++){
     var a=list[i];if(a?.def==null || !f.Contains(a.def) || Seen.Contains(a.def))continue;Seen.Add(a.def);
     IEnumerable<Command> cs=null;try{cs=a.GetGizmos();}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 技能分组按钮："+e.Message,0x13702);}
     if(cs!=null)foreach(var c in cs)yield return c;
    }
   }
   bool show=false;try{show=AbilityGroups.ShowSwitch(p,s);}catch(Exception e){Log.ErrorOnce("[DND边缘大陆] 技能分组按钮："+e.Message,0x13701);}
   if(show)yield return new Command_AbilityGroup(p,s);
  }
 }

 // 切换按钮：左键下一组，右键打开分组设置（窗口，不用浮动菜单）。
 public class Command_AbilityGroup : Command {
  readonly Pawn pawn;
  public Command_AbilityGroup(Pawn p,AbilityGroupSet s){
   pawn=p;icon=AbilityGroups.Icon;
   var a=s?.Active;int n=s?.groups.Count??0;
   defaultLabel="技能分组："+(a?.Label??"全部");
   defaultDesc=(n==0?"还没有分组。左键或右键打开分组设置：新建分组（如「战斗」「治疗」「赶路」），给每个能力勾选所在的组。"
    :"左键：切换到下一组（全部 → "+string.Join(" → ",s.groups.Select(g=>g.Label))+" → 全部）。\n右键：打开分组设置（新建、改名、删除分组，给能力分组，直接选当前组）。")
    +"\n\n选了某一组时，技能栏只显示这一组的能力；其他按钮（征召、攻击等）不受影响。不在当前组里的能力照常冷却，开了自动施放的照样会自动放。"
    +(a!=null?"\n\n当前组「"+a.Label+"」："+a.Count+" 个能力":"");
  }
  public override bool GroupsWith(Gizmo other)=>false;
  protected override GizmoResult GizmoOnGUIInt(Rect r,GizmoRenderParms parms){
   if(Event.current.type==EventType.MouseDown && Event.current.button==1 && Mouse.IsOver(r)){SoundDefOf.Tick_High.PlayOneShotOnCamera();AbilityGroups.Open(pawn);Event.current.Use();}
   return base.GizmoOnGUIInt(r,parms);
  }
  public override void ProcessInput(Event ev){
   base.ProcessInput(ev);
   if(ev!=null && ev.button==1){AbilityGroups.Open(pawn);return;}
   AbilityGroups.Cycle(pawn,1);
  }
 }

 // 分组设置窗口。左栏：全部 + 各分组（点选编辑）；右栏：选中组的名字、设为当前，和这个人全部能力的勾选表。
 public class Dialog_AbilityGroups : Window {
  readonly Pawn pawn;int sel=-1;Vector2 scrollL,scrollR;string armed;bool onlyLoose;
  List<AbilityGroupEntry> abil;int abilFrame=-999,abilCount=-1;
  public override Vector2 InitialSize=>LPUI.Fit(900f,700f);
  public Dialog_AbilityGroups(Pawn p){pawn=p;doCloseX=true;doCloseButton=true;closeOnClickedOutside=false;absorbInputAroundWindow=true;forcePause=true;var s=AbilityGroups.Get(p);sel=s?.active??-1;}
  public int Selected{get=>sel;set=>sel=value;}
  List<AbilityGroupEntry> Abilities(){
   int n=pawn.abilities?.AllAbilitiesForReading.Count??0;
   if(abil==null || n!=abilCount || Time.frameCount-abilFrame>60){
    abil=AbilityGroups.Listable(pawn).OrderBy(a=>a.def==null?1:0).ThenBy(a=>a.tag=="其他"?1:0).ThenBy(a=>a.tag).ThenBy(a=>a.sort).ThenBy(a=>a.label).ToList();
    abilCount=n;abilFrame=Time.frameCount;
   }
   return abil;
  }
  public override void DoWindowContents(Rect r){
   try{
    if(!AbilityGroups.Player(pawn)){Close();return;}
    float y=LPUI.Title(r,pawn.LabelShort+" 的技能分组");
    var s=AbilityGroups.Get(pawn);
    LPUI.Tiny(new Rect(r.x,y,r.width,36f),"技能栏当前显示："+AbilityGroups.ActiveLabel(pawn)+"。技能栏上的「技能分组」按钮左键切换下一组、右键打开这里。只影响显示：不在当前组里的能力照常冷却，开了自动施放的照样会自动放。一个能力可以在多个组里。");
    y+=40f;
    var body=new Rect(r.x,y,r.width,r.yMax-y-48f);float lw=Mathf.Min(250f,body.width*.32f);
    DrawLeft(new Rect(body.x,body.y,lw,body.height),s);
    DrawRight(new Rect(body.x+lw+LPUI.SectionGap,body.y,body.width-lw-LPUI.SectionGap,body.height),AbilityGroups.Get(pawn));
   }finally{LPUI.Reset();}
  }
  void DrawLeft(Rect outer,AbilityGroupSet s){
   int n=s?.groups.Count??0;var listR=new Rect(outer.x,outer.y,outer.width,outer.height-2f*34f);
   var view=new Rect(0f,0f,listR.width-LPUI.ScrollBarW,(n+1)*(LPUI.ListRowH+2f));
   Widgets.BeginScrollView(listR,ref scrollL,view);
   try{
    float y=0f;
    for(int i=-1;i<n;i++){
     var row=new Rect(0f,y,view.width,LPUI.ListRowH);LPUI.RowBg(row,sel==i);
     bool act=(s?.active??-1)==i;
     string lab=i<0?"全部（"+Abilities().Count+"）":s.groups[i].Label+"（"+s.groups[i].Count+"）";
     if(act)GUI.color=LPUI.Gold;LPUI.LabelFit(new Rect(row.x+6f,row.y+2f,row.width-56f,row.height-4f),lab);GUI.color=Color.white;
     if(act)LPUI.Tiny(new Rect(row.xMax-50f,row.y+6f,48f,18f),"技能栏");
     if(Widgets.ButtonInvisible(row)){sel=i;armed=null;scrollR=Vector2.zero;}
     y+=LPUI.ListRowH+2f;
    }
   }finally{Widgets.EndScrollView();}
   var b1=LPUI.Row(new Rect(outer.x,outer.yMax-64f,outer.width,30f),2,4f);
   if(LPUI.Button(b1[0],"新建分组",n<AbilityGroups.MaxGroups,n<AbilityGroups.MaxGroups?"新建一个空分组，然后在右边勾选能力":"最多 "+AbilityGroups.MaxGroups+" 组")){var g=AbilityGroups.Add(pawn);if(g!=null){sel=AbilityGroups.Get(pawn).groups.IndexOf(g);armed=null;}}
   bool isG=sel>=0 && sel<n;string key="del:"+sel;
   if(LPUI.Button(b1[1],armed==key?"确定删除？":"删除分组",isG,isG?(armed==key?"再点一次删除（能力本身不受影响）":"点两次删除这个分组"):"先在上面选一个分组")){if(armed==key){armed=null;AbilityGroups.Remove(pawn,sel);sel=-1;}else armed=key;}
   var b2=LPUI.Row(new Rect(outer.x,outer.yMax-30f,outer.width,30f),2,4f);
   if(LPUI.Button(b2[0],"上移",isG && sel>0)){AbilityGroups.Move(pawn,sel,-1);sel--;armed=null;}
   if(LPUI.Button(b2[1],"下移",isG && sel<n-1)){AbilityGroups.Move(pawn,sel,1);sel++;armed=null;}
  }
  void DrawRight(Rect outer,AbilityGroupSet s){
   if(sel>=(s?.groups.Count??0))sel=-1;
   var g=sel>=0?s.groups[sel]:null;float y=outer.y;
   bool act=(s?.active??-1)==sel;
   if(g!=null){
    LPUI.LabelFit(new Rect(outer.x,y+3f,60f,24f),"名字：");
    string nn=Widgets.TextField(new Rect(outer.x+60f,y,Mathf.Min(220f,outer.width-60f-250f),28f),g.name);
    if(nn!=g.name)g.name=nn.Length>AbilityGroups.MaxName?nn.Substring(0,AbilityGroups.MaxName):nn;
   }else LPUI.LabelFit(new Rect(outer.x,y+3f,outer.width-250f,24f),"全部：技能栏显示所有能力（右边列出每个能力在哪些组里）");
   if(LPUI.Button(new Rect(outer.xMax-244f,y,240f,28f),act?"技能栏正在显示这一组":g!=null?"技能栏只显示这一组":"技能栏显示全部",!act)){AbilityGroups.SetActive(pawn,sel);SoundDefOf.Tick_High.PlayOneShotOnCamera();}
   y+=34f;
   var list=Abilities();
   if(g!=null){
    var bs=LPUI.Row(new Rect(outer.x,y,outer.width,26f),3,4f);
    if(LPUI.Button(bs[0],"全选",true,"把下面列出的能力都放进这一组"))foreach(var a in Shown(list,s))g.SetKey(a.key,true);
    if(LPUI.Button(bs[1],"全不选",true,"把下面列出的能力都移出这一组"))foreach(var a in Shown(list,s))g.SetKey(a.key,false);
    bool ol=onlyLoose;Widgets.CheckboxLabeled(bs[2],"只看还没分组的",ref onlyLoose);if(ol!=onlyLoose)scrollR=Vector2.zero;
    y+=30f;
   }
   var rows=g!=null?Shown(list,s).ToList():list;
   var lr=new Rect(outer.x,y,outer.width,outer.yMax-y);
   var view=new Rect(0f,0f,lr.width-LPUI.ScrollBarW,Mathf.Max(rows.Count*42f+4f,lr.height-1f));
   Widgets.BeginScrollView(lr,ref scrollR,view);
   try{
    float yy=0f;string lastTag=null;
    if(rows.Count==0){GUI.color=Color.gray;Widgets.Label(new Rect(0f,0f,view.width,24f),list.Count==0?"这个人还没有能力。":"这里没有能力（都已经分了组）。");GUI.color=Color.white;}
    foreach(var a in rows){
     string tag=a.tag;
     var row=new Rect(0f,yy,view.width,40f);
     if(Mouse.IsOver(row))Widgets.DrawHighlight(row);else if(tag!=lastTag)Widgets.DrawLightHighlight(row);
     lastTag=tag;
     LPUI.Icon(new Rect(row.x+4f,row.y+4f,32f,32f),a.icon??BaseContent.BadTex);
     float tx=row.x+42f,tw=row.width-42f-(g!=null?34f:0f);
     LPUI.LabelFit(new Rect(tx,row.y+1f,tw,22f),a.label+"　"+tag+(a.hidden?"（没准备，不在技能栏）":""));
     var inG=s==null?new List<string>():s.groups.Where(x=>x.HasKey(a.key)).Select(x=>x.Label).ToList();
     LPUI.Tiny(new Rect(tx,row.y+21f,tw,18f),inG.Count==0?"未分组":"所在分组："+string.Join("、",inG),inG.Count==0);
     TooltipHandler.TipRegion(new Rect(row.x,row.y,tw+42f,row.height),a.label+(a.desc.NullOrEmpty()?"":"\n\n"+a.desc));
     if(g!=null){
      bool on=g.HasKey(a.key),on2=on;var cb=new Rect(row.xMax-30f,row.y+8f,24f,24f);
      Widgets.Checkbox(cb.position,ref on2,24f);
      if(on2==on && Widgets.ButtonInvisible(new Rect(row.x,row.y,tw+42f,row.height)))on2=!on;
      if(on2!=on){g.SetKey(a.key,on2);(on2?SoundDefOf.Checkbox_TurnedOn:SoundDefOf.Checkbox_TurnedOff).PlayOneShotOnCamera();}
     }
     yy+=42f;
    }
   }finally{Widgets.EndScrollView();}
  }
  IEnumerable<AbilityGroupEntry> Shown(List<AbilityGroupEntry> list,AbilityGroupSet s){
   foreach(var a in list){if(onlyLoose && s!=null && s.groups.Any(x=>x.HasKey(a.key)))continue;yield return a;}
  }
 }
}
