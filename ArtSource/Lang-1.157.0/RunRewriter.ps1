# 1.157.0：编译 LocRewriter.cs 并运行。-Apply 时改写源码（.Loc()/.LocF()），否则只导出键。输出 cs_keys.jsonl。
param([switch]$Apply)
$ErrorActionPreference='Stop'
$here=$PSScriptRoot;$mod=Split-Path (Split-Path $here -Parent) -Parent
$shell=Split-Path (Get-Process -Id $PID).Path
$refs=@((Join-Path $shell 'Microsoft.CodeAnalysis.dll'),(Join-Path $shell 'Microsoft.CodeAnalysis.CSharp.dll'),'System.Collections.Immutable','System.Linq','System.Collections','System.Runtime','System.Text.Encoding.Extensions','System.Console')
if(-not ('LocRewriter' -as [type])){Add-Type -Path (Join-Path $here 'LocRewriter.cs') -ReferencedAssemblies $refs -IgnoreWarnings -CompilerOptions '/nowarn:CS1701,CS1702'}
$dirs=@((Join-Path $mod 'Source'))+@(Get-ChildItem (Join-Path $mod 'Bridges') -Directory | ForEach-Object {Join-Path $_.FullName 'Source'} | Where-Object {Test-Path $_})
[LocRewriter]::Run([string[]]$dirs,[bool]$Apply,(Join-Path $here 'cs_keys.jsonl'))

