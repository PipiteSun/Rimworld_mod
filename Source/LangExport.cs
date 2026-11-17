using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Verse;
namespace LichPhylactery {
 // 1.157.0 英文语言文件：开发用。启动参数 -lichlangexport=<文件> 时，把所有含中文、可以 DefInjected 翻译的 Def 字段（游戏自己算出的路径）导出成 JSON 行，
 // 供 ArtSource/Lang-1.157.0 的脚本生成 Languages/English/DefInjected。平时不走这里。
 [StaticConstructorOnStartup]
 public static class LangExport {
  static LangExport(){
   // 翻译出错时（日志里 "Translation data for language … has N errors"）把每条错误写进日志，方便查是哪条 DefInjected 路径不对。
   if(GenCommandLine.CommandLineArgPassed("lichlangerrors")){
    var lang=LanguageDatabase.activeLanguage;
    if(lang!=null){
     foreach(var e in lang.loadErrors)Log.Warning("[LichPhylactery] lang error: "+e);
     foreach(var p in lang.defInjections)foreach(var e in p.loadErrors)Log.Warning("[LichPhylactery] lang error ("+p.defType.Name+"): "+e);
    }
   }
   if(!GenCommandLine.TryGetCommandLineArg("lichlangexport",out var path) || path.NullOrEmpty())return;
   try{Export(path.Trim('"'));}catch(Exception e){Log.Error("[LichPhylactery] lang export failed: "+e);}
  }
  static bool Cjk(string s){if(s==null)return false;foreach(char c in s)if(c>=0x4e00&&c<=0x9fff)return true;return false;}
  static string J(string s){
   if(s==null)return "null";var b=new StringBuilder("\"");
   foreach(char c in s){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<0x20)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}
   return b.Append('"').ToString();
  }
  static void Export(string path){
   var sb=new StringBuilder();int n=0;
   foreach(var t in GenDefDatabase.AllDefTypesWithDatabases()){
    DefInjectionUtility.ForEachPossibleDefInjection(t,(suggested,normalized,isCol,cur,curCol,allowed,fullList,fi,def)=>{
     if(!allowed)return;
     var col=curCol?.ToList();
     if(!(isCol?col!=null&&col.Any(Cjk):Cjk(cur)))return;
     sb.Append("{\"type\":").Append(J(t.Name)).Append(",\"def\":").Append(J(def.defName)).Append(",\"path\":").Append(J(suggested)).Append(",\"norm\":").Append(J(normalized))
       .Append(",\"col\":").Append(isCol?"true":"false").Append(",\"full\":").Append(fullList?"true":"false")
       .Append(",\"pack\":").Append(J(def.modContentPack?.PackageId)).Append(",\"file\":").Append(J(def.fileName));
     if(isCol)sb.Append(",\"vals\":[").Append(string.Join(",",col.Select(J))).Append("]");else sb.Append(",\"val\":").Append(J(cur));
     sb.Append("}\n");n++;
    });
   }
   File.WriteAllText(path,sb.ToString(),new UTF8Encoding(false));
   Log.Message("[LichPhylactery] lang export: "+n+" entries → "+path);
  }
 }
}
