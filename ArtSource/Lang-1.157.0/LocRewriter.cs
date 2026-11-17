using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// 1.157.0：把代码里的中文字符串包成 "中文".Loc() / "模板{0}".LocF(参数)，并导出全部键（JSON 行）供翻译。
// 已经包过的（后面紧跟 .Loc() / .LocF(）不再包；所以可以重复运行。
public static class LocRewriter {
 public static bool Cjk(string s){
  if(s==null)return false;
  foreach(char c in s){if((c>=0x4e00&&c<=0x9fff)||(c>=0x3000&&c<=0x303f)||(c>=0xff00&&c<=0xffef)||c=='…'||c=='“'||c=='”'||c=='—')return true;}
  return false;
 }
 static bool HasHan(string s){if(s==null)return false;foreach(char c in s)if((c>=0x4e00&&c<=0x9fff)||(c>=0x3000&&c<=0x303f)||(c>=0xff00&&c<=0xffef))return true;return false;}
 static readonly HashSet<string> SkipMethods=new HashSet<string>{"Contains","StartsWith","EndsWith","IndexOf","LastIndexOf","Replace","Split","Equals","GetNamed","GetNamedSilentFail","Named","TryGetValue","ContainsKey","Remove","Trim","TrimEnd","TrimStart","IndexOfAny","Any","Where","Count","CompareTo","Compare","Look","LookValue"};
 static readonly HashSet<string> LogMethods=new HashSet<string>{"Message","Warning","Error","ErrorOnce","WarningOnce","Log","LogError","LogWarning"};

 public static string Reason(SyntaxNode n){
  // 包在 .Loc() 里会编译不过或者改了含义的地方
  for(var a=n.Parent;a!=null;a=a.Parent){
   if(a is AttributeSyntax)return "attribute";
   if(a is CaseSwitchLabelSyntax||a is ConstantPatternSyntax||a is CasePatternSwitchLabelSyntax)return "case";
   if(a is SwitchExpressionArmSyntax arm && arm.Pattern.Span.Contains(n.Span))return "case";
   if(a is ClassDeclarationSyntax c && (c.Identifier.Text=="LichChecks"||c.Identifier.Text.EndsWith("Checks")))return "checks";
   if(a is FieldDeclarationSyntax f && f.Modifiers.Any(SyntaxKind.ConstKeyword))return "const";
   if(a is LocalDeclarationStatementSyntax l && l.Modifiers.Any(SyntaxKind.ConstKeyword))return "const";
   if(a is ParameterSyntax)return "default-param";
   if(a is ElementAccessExpressionSyntax ea && ea.ArgumentList.Span.Contains(n.Span))return "indexer";
   if(a is ImplicitElementAccessSyntax)return "indexer";
  }
  SyntaxNode e=n;
  while(e.Parent is ParenthesizedExpressionSyntax||(e.Parent is ConditionalExpressionSyntax ce && ce.Condition!=e)||(e.Parent is BinaryExpressionSyntax b0&&b0.IsKind(SyntaxKind.CoalesceExpression)))e=e.Parent;
  var p=e.Parent;
  if(p is BinaryExpressionSyntax b && (b.IsKind(SyntaxKind.EqualsExpression)||b.IsKind(SyntaxKind.NotEqualsExpression)))return "compare";
  if(p is InitializerExpressionSyntax ie && ie.IsKind(SyntaxKind.ComplexElementInitializerExpression) && ie.Expressions.Count>0 && ie.Expressions[0]==e)return "dict-key";
  if(p is ArgumentSyntax arg && arg.Parent is ArgumentListSyntax al && al.Parent is InvocationExpressionSyntax inv){
   string name=null;string recv=null;
   if(inv.Expression is MemberAccessExpressionSyntax ma){name=ma.Name.Identifier.Text;recv=ma.Expression.ToString();}
   else if(inv.Expression is IdentifierNameSyntax id)name=id.Identifier.Text;
   else if(inv.Expression is GenericNameSyntax gn)name=gn.Identifier.Text;
   if(name!=null){
    if(SkipMethods.Contains(name))return "method:"+name;
    if(LogMethods.Contains(name)&&(recv=="Log"||recv=="Verse.Log"||recv=="Debug"||recv==null))return "log";
   }
  }
  if(p is ArgumentSyntax arg2 && arg2.Parent is ArgumentListSyntax al2 && al2.Parent is ObjectCreationExpressionSyntax oc && oc.Type.ToString().EndsWith("Exception"))return "exception";
  if(p is ThrowExpressionSyntax||p is ThrowStatementSyntax)return "exception";
  return null;
 }
 static bool AlreadyWrapped(SyntaxNode n){
  SyntaxNode e=n;
  while(e.Parent is ParenthesizedExpressionSyntax)e=e.Parent;
  return e.Parent is MemberAccessExpressionSyntax ma && ma.Expression==e && (ma.Name.Identifier.Text=="Loc"||ma.Name.Identifier.Text=="LocF");
 }

 public class Entry{public string key,file,ctx,reason;public int line;public bool interp;}
 class RW:CSharpSyntaxRewriter{
  public List<Entry> entries=new List<Entry>();public string file;public string text;public bool apply;public int changed;
  string Ctx(SyntaxNode n){
   var st=n.AncestorsAndSelf().FirstOrDefault(a=>a is StatementSyntax||a is MemberDeclarationSyntax)??n;
   var s=st.ToString().Replace("\r","").Replace("\n"," ");
   while(s.Contains("  "))s=s.Replace("  "," ");
   if(s.Length>400){int i=s.IndexOf(n.ToString().Length>60?n.ToString().Substring(0,60):n.ToString());if(i<0)i=0;int st0=Math.Max(0,i-150);s=s.Substring(st0,Math.Min(400,s.Length-st0));}
   return s;
  }
  int Line(SyntaxNode n)=>n.GetLocation().GetLineSpan().StartLinePosition.Line+1;
  public override SyntaxNode VisitLiteralExpression(LiteralExpressionSyntax node){
   if(!node.IsKind(SyntaxKind.StringLiteralExpression))return base.VisitLiteralExpression(node);
   string v=node.Token.ValueText;
   if(!HasHan(v))return node;
   var r=AlreadyWrapped(node)?"wrapped":Reason(node);
   entries.Add(new Entry{key=v,file=file,line=Line(node),ctx=Ctx(node),reason=r});
   if(r!=null||!apply)return node;
   changed++;
   var expr=SyntaxFactory.ParseExpression(node.Token.Text+".Loc()");
   return expr.WithTriviaFrom(node);
  }
  public override SyntaxNode VisitInterpolatedStringExpression(InterpolatedStringExpressionSyntax node){
   bool textCjk=node.Contents.OfType<InterpolatedStringTextSyntax>().Any(t=>HasHan(t.TextToken.ValueText));
   var visited=(InterpolatedStringExpressionSyntax)base.VisitInterpolatedStringExpression(node);
   if(!textCjk)return visited;
   var r=AlreadyWrapped(node)?"wrapped":Reason(node);
   if(node.StringStartToken.Text.Contains("\"\"\""))r=r??"raw";
   var tpl=new StringBuilder();var args=new List<string>();
   var origContents=node.Contents;var newContents=visited.Contents;
   for(int i=0;i<newContents.Count;i++){
    var c=newContents[i];
    if(c is InterpolatedStringTextSyntax t){tpl.Append(t.TextToken.ValueText.Replace("{","{{").Replace("}","}}"));}
    else if(c is InterpolationSyntax h){
     int idx=args.Count;args.Add(h.Expression.ToString());
     tpl.Append("{").Append(idx);
     if(h.AlignmentClause!=null)tpl.Append(",").Append(h.AlignmentClause.Value.ToString());
     if(h.FormatClause!=null)tpl.Append(":").Append(h.FormatClause.FormatStringToken.ValueText);
     tpl.Append("}");
    }
   }
   string key=tpl.ToString();
   entries.Add(new Entry{key=key,file=file,line=Line(node),ctx=Ctx(node),reason=r,interp=true});
   if(r!=null||!apply)return visited;
   changed++;
   string lit=SymbolDisplay.FormatLiteral(key,true);
   string code=args.Count==0?lit+".Loc()":lit+".LocF("+string.Join(", ",args.Select(a=>a))+")";
   var expr=SyntaxFactory.ParseExpression(code);
   return expr.WithTriviaFrom(node);
  }
 }
 static string J(string s){
  var b=new StringBuilder("\"");
  foreach(char c in s){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<0x20)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}
  return b.Append('"').ToString();
 }
 // dirs：要处理的源码文件夹；apply=true 时写回文件；返回 JSON 行
 public static string Run(string[] dirs,bool apply,string outPath){
  var sb=new StringBuilder();int files=0,changed=0;
  foreach(var d in dirs)foreach(var f in Directory.GetFiles(d,"*.cs")){
   var name=Path.GetFileName(f);
   if(name=="LPLang.cs"||name=="LangExport.cs")continue;
   var text=File.ReadAllText(f,Encoding.UTF8);
   bool bom=File.ReadAllBytes(f).Take(3).SequenceEqual(new byte[]{0xEF,0xBB,0xBF});
   var tree=CSharpSyntaxTree.ParseText(text,new CSharpParseOptions(LanguageVersion.Latest),f,Encoding.UTF8);
   var rw=new RW{file=f,text=text,apply=apply};
   var root=rw.Visit(tree.GetRoot());
   foreach(var e in rw.entries)sb.Append("{\"key\":").Append(J(e.key)).Append(",\"file\":").Append(J(Path.GetFileName(e.file))).Append(",\"line\":").Append(e.line).Append(",\"interp\":").Append(e.interp?"true":"false").Append(",\"reason\":").Append(e.reason==null?"null":J(e.reason)).Append(",\"ctx\":").Append(J(e.ctx)).Append("}\n");
   if(apply&&rw.changed>0){
    File.WriteAllText(f,root.ToFullString(),new UTF8Encoding(bom));files++;changed+=rw.changed;
   }
  }
  File.WriteAllText(outPath,sb.ToString(),new UTF8Encoding(false));
  return "files "+files+", wrapped "+changed;
 }
}
