using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Verse;
namespace LichPhylactery {
 // 1.157.0 英文语言文件：代码里写死的中文字符串都包了一层 "中文".Loc() / "模板{0}".LocF(参数)。
 // 中文（简体 / 繁体）游戏语言：原样返回，和以前一模一样。
 // 其他语言：查 Languages/<语言>/LPStrings/*.xml（中文原文 → 译文），找不到再查 English，再找不到显示中文原文。
 // 文件格式：<LPStrings><s><zh>中文</zh><en>English</en></s>…</LPStrings>，按中文原文查，首尾空格原样保留。
 // 其他语言的译者照 English 文件夹复制一份改 <en> 即可（文件夹名用游戏的语言文件夹名或旧名，如 Russian）。
 public static class LPLang {
  static int state; // 0 未定，1 中文，2 其他语言（已载入表）
  static Dictionary<string,string> table;
  public static bool Chinese{get{Resolve();return state!=2;}}
  static void Resolve(){
   if(state!=0)return;
   var lang=LanguageDatabase.activeLanguage;
   string folder=lang?.folderName??Prefs.LangFolderName??"";
   if(folder.StartsWith("Chinese",StringComparison.OrdinalIgnoreCase)){if(lang!=null)state=1;return;}
   if(lang==null)return; // 语言还没选好：先当中文，之后再定
   table=new Dictionary<string,string>();
   try{
    Load("English");
    if(lang!=LanguageDatabase.defaultLanguage){Load(lang.folderName);if(!lang.LegacyFolderName.NullOrEmpty())Load(lang.LegacyFolderName);}
   }catch(Exception e){Log.Error("[LichPhylactery] LPStrings load failed: "+e);}
   state=2;
  }
  static void Load(string folder){
   var mod=LoadedModManager.RunningModsListForReading.Find(m=>string.Equals(m.PackageId,"local.lich.phylactery",StringComparison.OrdinalIgnoreCase));if(mod==null||folder.NullOrEmpty())return;
   var dir=Path.Combine(Path.Combine(Path.Combine(mod.RootDir,"Languages"),folder),"LPStrings");
   if(!Directory.Exists(dir))return;
   foreach(var f in Directory.GetFiles(dir,"*.xml")){
    var doc=new XmlDocument{PreserveWhitespace=true};doc.Load(f);
    foreach(XmlNode s in doc.DocumentElement.ChildNodes){
     if(s.NodeType!=XmlNodeType.Element||s.Name!="s")continue;
     var zh=s["zh"];var en=s["en"];
     if(zh==null||en==null)continue;
     table[Unescape(zh.InnerText)]=Unescape(en.InnerText);
    }
   }
  }
  static string Unescape(string s)=>s.IndexOf('\\')<0?s:s.Replace("\\n","\n").Replace("\\t","\t");
  /// <summary>代码里的中文字符串：中文环境原样返回，其他语言返回译文。</summary>
  public static string Loc(this string zh){
   if(zh==null)return null;
   Resolve();if(state!=2)return zh;
   return table.TryGetValue(zh,out var t)?t:zh;
  }
  /// <summary>中文插值字符串改成的格式模板：$"等级 {lv}" → "等级 {0}".LocF(lv)。</summary>
  public static string LocF(this string zh,params object[] args){
   string f=zh.Loc();
   try{return string.Format(f,args);}
   catch(FormatException){try{return string.Format(zh,args);}catch(FormatException){return zh;}}
  }
 }
}
