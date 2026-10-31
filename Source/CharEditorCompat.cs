using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
namespace LichPhylactery {
 // 1.86.0 角色编辑器（Character Editor，void.charactereditor，创意工坊 1874644848）联动：编辑器窗口人物栏底部加一个「DND」按钮，
 // 打开旁边的 DND 编辑面板：冒险者等级 / 传奇、六维、各种点数、职业 / 子职业 / 分支、专长、史诗恩赐、强力特性 / 武艺天赋 / 稀有天赋 / 性格 / 诅咒。
 // 全部反射（照 Isekai 的 Character Editor 支持补丁的挂法），不引用编辑器程序集，不改编辑器本身；没装编辑器时什么都不做。
 // 编辑器里的改动不受门槛限制（技能、前置、兼修数、选定后不能改），但走模组自己的增减函数，缓存、能力和效果照常同步。
 [StaticConstructorOnStartup]
 public static class CharEditorCompat {
  public const string PackageId="void.charactereditor",IsekaiPatchId="birby.isekaicepatch";
  public static Type EditorType,UiType;
  static PropertyInfo apiProp,pawnProp,personXProp,personWProp;
  public static bool Active;
  static bool isekaiButton;
  static readonly HashSet<Type> logged=new HashSet<Type>();
  static CharEditorCompat(){
   if(!ModsConfig.IsActive(PackageId))return;
   try{
    EditorType=AccessTools.TypeByName("CharacterEditor.CEditor");UiType=AccessTools.TypeByName("CharacterEditor.CEditor+EditorUI");
    if(EditorType==null || UiType==null){Log.Warning("[LichPhylactery] 角色编辑器：找不到 CEditor / EditorUI，DND 面板不可用。");return;}
    var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    apiProp=EditorType.GetProperty("API",flags);pawnProp=EditorType.GetProperty("Pawn",flags);
    personXProp=UiType.GetProperty("PersonX",flags);personWProp=UiType.GetProperty("PersonW",flags);
    var m=UiType.GetMethod("DoWindowContents",flags);
    if(m==null || apiProp==null || pawnProp==null){Log.Warning("[LichPhylactery] 角色编辑器：接口对不上，DND 面板不可用。");return;}
    new Harmony("local.lich.phylactery.chareditor").Patch(m,postfix:new HarmonyMethod(typeof(CharEditorCompat),nameof(Postfix)));
    isekaiButton=ModsConfig.IsActive(IsekaiPatchId) && ModsConfig.IsActive(Adventurer.IsekaiId);
    Active=true;
   }catch(Exception e){Log.Warning("[LichPhylactery] 角色编辑器联动初始化失败："+e);}
  }
  public static Pawn EditorPawn(){try{var api=apiProp?.GetValue(null);return api==null?null:pawnProp.GetValue(api) as Pawn;}catch{return null;}}
  static int Prop(PropertyInfo pi,object inst,int fallback){try{return pi==null?fallback:(int)pi.GetValue(pi.GetGetMethod(true).IsStatic?null:inst);}catch{return fallback;}}
  public static bool EditorOpen(){if(UiType==null)return false;var ws=Find.WindowStack?.Windows;if(ws!=null)for(int i=0;i<ws.Count;i++)if(UiType.IsInstanceOfType(ws[i]))return true;return false;}
  public static Window_DndEditor Panel(){var ws=Find.WindowStack?.Windows;if(ws!=null)for(int i=0;i<ws.Count;i++)if(ws[i] is Window_DndEditor w)return w;return null;}
  public const float BtnH=24f,BtnBottom=50f;
  public static void Postfix(Window __instance,Rect inRect){
   try{
    var p=EditorPawn();var panel=Panel();
    if(p==null || p.RaceProps?.Humanlike!=true){panel?.Close(false);return;}
    if(panel!=null && panel.pawn!=p)panel.SetPawn(p);
    int x=Prop(personXProp,__instance,0),w=Prop(personWProp,__instance,200);
    float y=inRect.height-BtnBottom-(isekaiButton?BtnH+2f:0f); // Isekai 支持补丁的「Status」按钮占着同一位置：放在它上面
    var r=new Rect(x+2f,y,w-4f,BtnH);
    GUI.color=panel!=null?Color.yellow:Color.white;
    if(Widgets.ButtonText(r,"DND")){if(panel!=null)panel.Close();else Find.WindowStack.Add(new Window_DndEditor(p,__instance));}
    GUI.color=Color.white;
    TooltipHandler.TipRegion(r,"打开 DND 编辑面板：冒险者等级、六维属性、职业与子职业、专长、史诗恩赐、强力特性与武艺天赋。");
   }catch(Exception e){GUI.color=Color.white;if(logged.Add(e.GetType()))Log.Error("[LichPhylactery] 角色编辑器 DND 按钮出错（同类错误不再提示）："+e);}
  }

  // ---------- 修改（面板和自检共用） ----------
  // 设冒险者等级：升级沿途照常发属性提升 / 传奇属性点 / 19 级恩赐点 / VPE 路径点（同 Hediff_Adventurer.LevelPerks，那是私有的）；降级不收回已经发的。经验设到该级下限。
  public static void SetLevel(Hediff_Adventurer h,int target){
   if(h==null)return;target=Mathf.Clamp(target,1,h.LevelCap);
   for(int l=h.level+1;l<=target;l++){
    if(l>Adventurer.MaxLevel)h.legendPoints+=h.LegendAt(l);
    else{h.pendingASI+=h.AsiAt(l);h.legendPoints+=h.LegendAt(l);if(l>=19 && !h.boon19){h.boon19=true;h.boonPoints++;}}
    if(l<=Adventurer.MaxLevel || l%2==0)Adventurer.LevelUpHook?.Invoke(h.pawn,l);
   }
   h.level=target;h.xp=Legend.XpFor(target);
   ClassLegendary.SyncPoints(h);
   bool npc=h.pawn.Faction==null || !h.pawn.Faction.IsPlayer;
   if(h.pendingASI>0 && Adventurer.AutoASI(h.pawn))h.AutoAssign();
   if(h.boonPoints>0 && npc)h.AutoBoon();
   if(h.legendPoints>0 && Legend.AutoFor(h.pawn))Legend.AutoSpend(h);
   h.Dirty();
  }
  public static void SetLegendary(Hediff_Adventurer h,bool on){
   if(h==null || h.legendary==on)return;
   h.legendary=on;
   if(on){Legend.Became(h);}
   else if(h.level>Adventurer.MaxLevel){h.level=Adventurer.MaxLevel;h.xp=Mathf.Min(h.xp,Legend.XpFor(Adventurer.MaxLevel+1)-1f);}
   h.Dirty();Epic.State?.RefreshLegend();
  }
  public static void SetScore(Hediff_Adventurer h,int a,int v){if(h==null || a<0 || a>5)return;h.scores[a]=Mathf.Clamp(v,Adventurer.ScoreMin,MaxScore);h.Dirty();}
  public const int MaxScore=60;
  // 设职业等级（0 = 去掉）：不看技能 / 兼修门槛；降级时收回不再应得的职业能力，0 级时连同子职业和该职业的传奇能力一起去掉。
  public static void SetClassLevel(Hediff_Adventurer h,LP_ClassDef c,int level){
   if(h==null || c==null)return;level=Mathf.Clamp(level,0,ClassSystem.CapLevel);
   int old=ClassSystem.Level(h,c);if(old==level)return;
   if(level==0){
    var d=ClassSubclass.Data(h);var sub=ClassSubclass.Picked(h,c);if(sub!=null)DropSubclass(h,sub);
    if(h.classes!=null)h.classes.legendary.RemoveAll(a=>ClassLegendary.ClassOf(a)==c);
   }
   ClassSystem.SetLevel(h,c,level);
   if(level<old)Prune(h);
  }
  // 换子职业（null = 不选）：去掉原来的（连同它的分支选择和能力），不看“选定后不能更改”。
  public static void SetSubclass(Hediff_Adventurer h,LP_ClassDef c,LP_SubclassDef s){
   if(h?.classes==null || c==null)return;
   var cur=ClassSubclass.Picked(h,c);if(cur==s)return;
   if(cur!=null)DropSubclass(h,cur);
   var d=ClassSubclass.Data(h);
   if(s!=null){d.picks.Add(s);if(s.defName==ClassSubclass.Trickster)d.mana=ClassSubclass.ManaMax(h);}
   ClassSystem.Sync(h);Prune(h);if(h.pawn!=null)Martial.Refresh(h.pawn);h.Dirty();
  }
  static void DropSubclass(Hediff_Adventurer h,LP_SubclassDef s){
   var d=ClassSubclass.Data(h);if(d==null)return;
   d.picks.Remove(s);foreach(var ch in s.choices)d.choices.Remove(ch.key);
  }
  public static void SetChoice(Hediff_Adventurer h,string key,int option){
   var d=ClassSubclass.Data(h);if(d==null || key==null)return;
   if(option<0)d.choices.Remove(key);else d.choices[key]=option;
   if(h.pawn!=null)Martial.Refresh(h.pawn);h.Dirty();
  }
  // 收回不再应得的职业 / 子职业能力（迁移保留的、传奇职业能力不动）。返回收回的个数。
  public static int Prune(Hediff_Adventurer h){
   var p=h?.pawn;if(p?.abilities==null || h.classes==null)return 0;
   var granted=new HashSet<AbilityDef>();var entitled=new HashSet<AbilityDef>(h.classes.kept);entitled.UnionWith(h.classes.legendary);
   foreach(var c in ClassSystem.All)foreach(var e in c.abilities){if(e.ability==null)continue;granted.Add(e.ability);if(ClassSystem.Entitled(h,c,e))entitled.Add(e.ability);}
   foreach(var s in ClassSubclass.All)foreach(var e in s.abilities){if(e.ability==null || s.parent==null)continue;granted.Add(e.ability);if(ClassSubclass.Entitled(h,s,e))entitled.Add(e.ability);}
   int n=0;
   foreach(var a in p.abilities.abilities.Select(x=>x.def).Where(d=>granted.Contains(d) && !entitled.Contains(d)).ToList()){p.abilities.RemoveAbility(a);h.classes.autocast.Remove(a);n++;}
   return n;
  }
  public static void SetFeat(Hediff_Adventurer h,LP_FeatDef f,bool on){if(h==null || f==null)return;if(on)h.AddFeat(f);else h.RemoveFeat(f);}
  public static void SetBoon(Hediff_Adventurer h,LP_EpicBoonDef b,bool on,int ability=-1){
   if(h==null || b==null)return;
   if(on){h.AddBoon(b,ability);return;}
   if(!h.boons.Remove(b))return;if(b==Epic.Might)h.mightAbility=-1;h.Dirty();
  }
  // 特性：强力特性 / 诅咒 / 事迹走 HeroTraits.Give（互斥的先去掉、满了顶掉最早的），武艺天赋走 CombatGifts.Give，其他直接加。
  public static bool SetTrait(Pawn p,TraitDef d,int degree,bool on){
   var set=p?.story?.traits;if(set==null || d==null)return false;
   var cur=set.GetTrait(d);
   if(!on){if(cur==null || cur.Degree!=degree)return false;set.RemoveTrait(cur);}
   else{
    if(cur!=null){if(cur.Degree==degree)return true;set.RemoveTrait(cur);}
    if(CombatGifts.Is(d))CombatGifts.Give(p,d);
    else if(HeroTraits.IsSlot(d) || HeroTraits.IsDeed(d))HeroTraits.Give(p,d,degree);
    else set.GainTrait(new Trait(d,degree,true),true);
   }
   HeroTraits.Invalidate(p);CombatGifts.Invalidate(p);DndTraits.Invalidate(p);Adventurer.Get(p)?.Dirty();
   return set.HasTrait(d,degree)==on;
  }
 }

 public class Window_DndEditor : Window {
  public Pawn pawn;readonly Window editor;
  public int tab;Vector2 scroll;float viewH=600f;
  static readonly string[] Tabs={"等级与属性","职业","专长与恩赐","特性"};
  public Window_DndEditor(Pawn p,Window editor){
   pawn=p;this.editor=editor;
   doCloseX=true;draggable=true;closeOnClickedOutside=false;absorbInputAroundWindow=false;preventCameraMotion=false;forcePause=false;onlyOneOfTypeAllowed=true;
  }
  public override Vector2 InitialSize=>new Vector2(560f,Mathf.Min(820f,UI.screenHeight-40f));
  protected override void SetInitialSizeAndPosition(){
   base.SetInitialSizeAndPosition();
   if(editor==null)return;var e=editor.windowRect;var s=InitialSize;
   float x=e.xMax+4f<=UI.screenWidth-s.x?e.xMax+4f:e.x-s.x-4f>=0f?e.x-s.x-4f:UI.screenWidth-s.x;
   windowRect=new Rect(x,Mathf.Clamp(e.y,0f,UI.screenHeight-s.y),s.x,s.y);
  }
  public void SetPawn(Pawn p){pawn=p;scroll=Vector2.zero;}
  Hediff_Adventurer H=>Adventurer.Get(pawn);
  static bool Shift=>Event.current!=null && Event.current.shift;
  int Step=>Shift?5:1;

  public override void DoWindowContents(Rect inRect){
   if(!CharEditorCompat.EditorOpen() || pawn==null || pawn.Destroyed && !pawn.Dead){Close(false);return;}
   Text.Font=GameFont.Medium;Widgets.Label(new Rect(0f,0f,inRect.width-30f,32f),"DND："+pawn.LabelShortCap);Text.Font=GameFont.Small;
   float tw=inRect.width/Tabs.Length;
   for(int i=0;i<Tabs.Length;i++){var r=new Rect(i*tw,36f,tw-4f,28f);GUI.color=tab==i?Color.yellow:Color.white;if(Widgets.ButtonText(r,Tabs[i])){tab=i;scroll=Vector2.zero;}}
   GUI.color=Color.white;
   var outer=new Rect(0f,70f,inRect.width,inRect.height-70f);var view=new Rect(0f,0f,outer.width-16f,viewH);
   Widgets.BeginScrollView(outer,ref scroll,view);
   float y=0f;
   try{
    var h=H;
    if(h==null)y=NoAdventurer(view.width);
    else switch(tab){case 0:y=LevelTab(h,view.width);break;case 1:y=ClassTab(h,view.width);break;case 2:y=FeatTab(h,view.width);break;default:y=TraitTab(h,view.width);break;}
   }finally{Widgets.EndScrollView();GUI.color=Color.white;Text.Anchor=TextAnchor.UpperLeft;}
   viewH=Mathf.Max(y+10f,outer.height-4f);
  }

  // ---------- 小部件 ----------
  float Head(float y,float w,string s){Text.Font=GameFont.Medium;Widgets.Label(new Rect(0f,y,w,30f),s);Text.Font=GameFont.Small;Widgets.DrawLineHorizontal(0f,y+30f,w);return y+34f;}
  float Note(float y,float w,string s){GUI.color=Color.gray;float hh=Text.CalcHeight(s,w);Widgets.Label(new Rect(0f,y,w,hh),s);GUI.color=Color.white;return y+hh+4f;}
  // 一行数值：标签、−、值、+，可选的说明。Shift 点 ±5。
  float IntRow(float y,float w,string label,int value,Action<int> set,int min,int max,string extra=null,string tip=null){
   var row=new Rect(0f,y,w,26f);Widgets.DrawHighlightIfMouseover(row);if(tip!=null)TooltipHandler.TipRegion(row,tip);
   Text.Anchor=TextAnchor.MiddleLeft;Widgets.Label(new Rect(4f,y,150f,26f),label);
   if(Widgets.ButtonText(new Rect(156f,y+1f,28f,24f),"−") && value>min)set(Mathf.Max(min,value-Step));
   Text.Anchor=TextAnchor.MiddleCenter;Widgets.Label(new Rect(186f,y,44f,26f),value.ToString());
   if(Widgets.ButtonText(new Rect(232f,y+1f,28f,24f),"+") && value<max)set(Mathf.Min(max,value+Step));
   Text.Anchor=TextAnchor.MiddleLeft;if(extra!=null){GUI.color=Color.gray;Widgets.Label(new Rect(268f,y,w-268f,26f),extra);GUI.color=Color.white;}
   Text.Anchor=TextAnchor.UpperLeft;return y+28f;
  }
  float Toggle(float y,float w,string label,bool on,Action<bool> set,string tip=null,Color? color=null){
   var r=new Rect(0f,y,w,24f);bool v=on;if(color!=null)GUI.color=color.Value;
   Widgets.CheckboxLabeled(r,label,ref v);GUI.color=Color.white;if(tip!=null)TooltipHandler.TipRegion(r,tip);
   if(v!=on)set(v);return y+26f;
  }

  float NoAdventurer(float w){
   float y=Head(0f,w,"没有冒险者状态");
   if(!Adventurer.Enabled)return Note(y,w,"模组设置里关掉了冒险者等级。");
   if(!Adventurer.Eligible(pawn))return Note(y,w,"这个角色不能成为冒险者（需要类人、满 "+Adventurer.MinAge+" 岁，且不是构装体 / 召唤物）。");
   y=Note(y,w,"这个角色还没有冒险者等级和六维属性。");
   if(Widgets.ButtonText(new Rect(0f,y,200f,30f),"添加冒险者状态"))Adventurer.TryAdd(pawn,1,true);
   return y+34f;
  }

  // ---------- 等级与属性 ----------
  float LevelTab(Hediff_Adventurer h,float w){
   float y=Head(0f,w,"冒险者等级");
   y=IntRow(y,w,"等级",h.level,v=>CharEditorCompat.SetLevel(h,v),1,h.LevelCap,"上限 "+h.LevelCap+"，熟练加值 +"+h.Proficiency,"升级照常发属性提升、传奇属性点和恩赐点；降级不收回已经发的（可以在下面直接改点数）。Shift 点 ±5。");
   y=Toggle(y,w,"传奇冒险者（解锁 21～40 级）",h.legendary,v=>CharEditorCompat.SetLegendary(h,v),"关掉时等级降回 20。"+(Legend.On?"":"\n模组设置里关掉了传奇等级：开着也只到 20 级。"));
   y=Note(y,w,"经验 "+h.xp.ToString("0")+" / 下一级 "+h.NextXp.ToString("0")+"（设等级时经验取该级下限）");
   y=Head(y+4f,w,"六维属性");
   for(int a=0;a<6;a++){int i=a;
    string extra="有效 "+h.Eff(i)+" / 上限 "+h.Cap(i)+"　"+Adventurer.Signed(h.Mod(i))+"　×"+h.AMult(i).ToString("0.00");
    y=IntRow(y,w,Adventurer.Names[i],h.scores[i],v=>CharEditorCompat.SetScore(h,i,v),Adventurer.ScoreMin,CharEditorCompat.MaxScore,extra,"基础值（专长、武艺天赋、职业的加值另算）。超过上限的部分保留，但效果按上限算。");
   }
   if(h.buyOpen){y=Note(y,w,"正在分配初始购点（还剩 "+h.BuyLeft+" 点）。");if(Widgets.ButtonText(new Rect(0f,y,180f,26f),"结束初始购点"))h.ConfirmBuy();y+=30f;}
   y=Head(y+4f,w,"待分配点数");
   y=IntRow(y,w,"属性提升",h.pendingASI,v=>{h.pendingASI=v;h.Dirty();},0,99,"每次 "+h.ASIPoints+" 点，或换一个专长");
   y=IntRow(y,w,"拆开剩余的点",h.asiLeft,v=>{h.asiLeft=v;h.Dirty();},0,99);
   y=IntRow(y,w,"战士奖励专长",h.fighterFeats,v=>{h.fighterFeats=v;h.Dirty();},0,99);
   y=IntRow(y,w,"史诗恩赐点",h.boonPoints,v=>{h.boonPoints=v;h.Dirty();},0,99);
   y=IntRow(y,w,"传奇属性点",h.legendPoints,v=>{h.legendPoints=v;h.Dirty();},0,999);
   if(h.classes==null)h.classes=new ClassData();
   y=IntRow(y,w,"传奇职业点",h.classes.legendaryPoints,v=>h.classes.legendaryPoints=v,0,99);
   y=IntRow(y,w,"职业免门槛次数",h.classes.freeUnlocks,v=>h.classes.freeUnlocks=v,0,9,null,"下一个新职业跳过技能 / 兼修门槛（冒险者手册）。");
   y=Note(y,w,"这些点在原版「冒险者」分页里分配。");
   y=Head(y+4f,w,"传奇属性点（已投）");
   if(h.legendSpent==null || h.legendSpent.Length!=Legend.Count)h.legendSpent=new int[Legend.Count];
   for(int i=0;i<Legend.Count;i++){int k=i;y=IntRow(y,w,Legend.Names[k],h.legendSpent[k],v=>{h.legendSpent[k]=v;h.Dirty();},0,999);}
   return y;
  }

  // ---------- 职业 ----------
  float ClassTab(Hediff_Adventurer h,float w){
   if(h.classes==null)h.classes=new ClassData();
   float y=Head(0f,w,"职业");
   y=Note(y,w,"已用职业等级 "+ClassSystem.Total(h)+" / 冒险者等级给的 "+ClassSystem.Budget(h)+"。这里改不看技能和兼修门槛，也可以超过预算或兼修超过 "+ClassSystem.MaxClasses+" 个；降级会收回不再应得的职业能力。");
   foreach(var c in ClassSystem.All){
    int lv=ClassSystem.Level(h,c);var cc=c;
    GUI.color=lv>0?c.color:Color.white;
    y=IntRow(y,w,c.LabelCap,lv,v=>CharEditorCompat.SetClassLevel(h,cc,v),0,ClassSystem.CapLevel,lv>0?ClassSystem.ResourceLabel(c.resource)+(c.resource==ClassResource.None?"":" "+ClassSystem.Resource(h,c).ToString("0.#")+" / "+ClassSystem.MaxResource(h,c)):null,c.description);
    GUI.color=Color.white;
    if(lv<=0)continue;
    var opts=ClassSubclass.Options(c).ToList();
    if(opts.Count>0){
     var cur=ClassSubclass.Picked(h,c);var r=new Rect(24f,y,w-24f,26f);
     Text.Anchor=TextAnchor.MiddleLeft;Widgets.Label(new Rect(24f,y,126f,26f),"子职业");Text.Anchor=TextAnchor.UpperLeft;
     if(Widgets.ButtonText(new Rect(156f,y+1f,220f,24f),cur?.LabelCap.Resolve()??"（未选）")){
      var menu=new List<FloatMenuOption>{new FloatMenuOption("（不选）",()=>CharEditorCompat.SetSubclass(h,cc,null))};
      foreach(var s in opts){var ss=s;menu.Add(new FloatMenuOption(s.LabelCap,()=>CharEditorCompat.SetSubclass(h,cc,ss)));}
      Find.WindowStack.Add(new FloatMenu(menu));
     }
     if(cur!=null)TooltipHandler.TipRegion(r,cur.description);
     if(lv<ClassSubclass.PickLevelFor(c)){GUI.color=Color.gray;Text.Anchor=TextAnchor.MiddleLeft;Widgets.Label(new Rect(384f,y,w-384f,26f),"平时 "+ClassSubclass.PickLevelFor(c)+" 级才选");Text.Anchor=TextAnchor.UpperLeft;GUI.color=Color.white;}
     y+=28f;
     if(cur!=null)foreach(var ch in cur.choices){
      var chh=ch;int v=ClassSubclass.Choice(h,ch.key);
      Text.Anchor=TextAnchor.MiddleLeft;Widgets.Label(new Rect(40f,y,110f,26f),ch.label);Text.Anchor=TextAnchor.UpperLeft;
      if(Widgets.ButtonText(new Rect(156f,y+1f,220f,24f),v>=0 && v<ch.options.Count?ch.options[v]:"（未选）")){
       var menu=new List<FloatMenuOption>{new FloatMenuOption("（不选）",()=>CharEditorCompat.SetChoice(h,chh.key,-1))};
       for(int i=0;i<ch.options.Count;i++){int k=i;menu.Add(new FloatMenuOption(chh.options[k],()=>CharEditorCompat.SetChoice(h,chh.key,k)));}
       Find.WindowStack.Add(new FloatMenu(menu));
      }
      if(lv<ch.level){GUI.color=Color.gray;Text.Anchor=TextAnchor.MiddleLeft;Widgets.Label(new Rect(384f,y,w-384f,26f),"平时 "+ch.level+" 级才选");Text.Anchor=TextAnchor.UpperLeft;GUI.color=Color.white;}
      y+=28f;
     }
    }
   }
   if(pawn.abilities!=null){
    var known=pawn.abilities.abilities.Where(a=>ClassSystem.ClassOf(a.def)!=null).Select(a=>a.def.label).ToList();
    y=Note(y+4f,w,"已有职业能力（"+known.Count+"）："+(known.Count==0?"无":string.Join("、",known)));
   }
   return y;
  }

  // ---------- 专长与恩赐 ----------
  float FeatTab(Hediff_Adventurer h,float w){
   float y=Head(0f,w,"专长（"+h.feats.Count+"）");
   y=Note(y,w,"勾选直接获得、取消直接失去，不看等级和前置，也不占属性提升。");
   foreach(var f in DefDatabase<LP_FeatDef>.AllDefsListForReading.OrderBy(f=>f.origin?1:f.Racial?2:0).ThenBy(f=>f.order).ThenBy(f=>f.label)){
    var ff=f;string tag=f.origin?"〔起源〕":f.Racial?"〔种族〕":"";
    string unmet=null;try{unmet=f.Worker.Unmet(pawn,h);}catch{}
    y=Toggle(y,w,tag+f.LabelCap,h.HasFeat(f),v=>CharEditorCompat.SetFeat(h,ff,v),f.description+(f.BonusText!=null?"\n\n"+f.BonusText:"")+(unmet!=null && !h.HasFeat(f)?"\n\n平时的条件："+unmet:""));
   }
   y=Head(y+6f,w,"史诗恩赐（"+h.boons.Count+"）");
   foreach(var b in Epic.Boons){
    var bb=b;string label=b.LabelCap+(b==Epic.Might && h.HasBoon(b) && h.mightAbility>=0?"（"+Adventurer.Names[h.mightAbility]+"）":"");
    y=Toggle(y,w,label,h.HasBoon(b),v=>{
     if(v && bb==Epic.Might){var menu=new List<FloatMenuOption>();for(int i=0;i<6;i++){int k=i;menu.Add(new FloatMenuOption(Adventurer.Names[k]+" +"+Epic.MightBonus,()=>CharEditorCompat.SetBoon(h,bb,true,k)));}Find.WindowStack.Add(new FloatMenu(menu));}
     else CharEditorCompat.SetBoon(h,bb,v);
    },b.description);
   }
   return y;
  }

  // ---------- 特性 ----------
  float TraitTab(Hediff_Adventurer h,float w){
   float y=Head(0f,w,"特性");
   y=Note(y,w,"强力特性（金 / 紫 / 稀有天赋 / 诅咒）"+HeroTraits.Count(pawn)+" / "+HeroTraits.Max+"，武艺天赋 "+CombatGifts.CountOf(pawn)+" / "+CombatGifts.Max+"：满了再加会顶掉最早的一个（上限在模组设置里改）；互斥的（天命之间、稀有天赋之间、诅咒之间）会替换。其他特性在角色编辑器自己的特性页也能改。");
   y=Group(y,w,"金色强力特性",HeroTraits.Gold);
   y=Group(y,w,"紫色强力特性",HeroTraits.Purple);
   y=Group(y,w,"稀有天赋",Talents.All);
   y=Group(y,w,"武艺天赋",CombatGifts.All);
   y=Group(y,w,"事迹",HeroTraits.Deeds);
   y=Group(y,w,"诅咒之力",DndTraits.Curses);
   y=Group(y,w,"性格特性",DndTraits.Personality);
   return y;
  }
  float Group(float y,float w,string title,IEnumerable<TraitDef> defs){
   var list=defs?.Where(d=>d!=null).Distinct().ToList();if(list==null || list.Count==0)return y;
   y=Head(y+4f,w,title);
   foreach(var d in list){
    var dd=d;var degrees=d.degreeDatas;if(degrees==null || degrees.Count==0)continue;
    foreach(var dg in degrees){
     int deg=dg.degree;string label=dg.label.CapitalizeFirst();
     bool has=pawn.story?.traits?.HasTrait(d,deg)==true;
     string tip=null;try{tip=dg.description.Formatted(pawn.Named("PAWN")).AdjustedFor(pawn).Resolve();}catch{tip=dg.description;}
     y=Toggle(y,w,label,has,v=>{if(!CharEditorCompat.SetTrait(pawn,dd,deg,v))Messages.Message("没能"+(v?"加上":"去掉")+"「"+dg.label+"」（和已有特性冲突）",MessageTypeDefOf.RejectInput,false);},tip);
    }
   }
   return y;
  }
 }
}
