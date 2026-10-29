$ErrorActionPreference='Stop'
$lootMod=Split-Path $PSScriptRoot -Parent
$lootGame=Split-Path (Split-Path $lootMod -Parent) -Parent
$lootWorkshop=Join-Path (Split-Path (Split-Path $lootGame -Parent) -Parent) 'workshop\content\294100'
$lootShell=Split-Path (Get-Process -Id $PID).Path
Add-Type -Path (Join-Path $lootShell 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $lootShell 'Microsoft.CodeAnalysis.CSharp.dll')
$lootRefs=[Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
$lootPaths=@(Get-ChildItem -LiteralPath $lootGame -Filter '*.dll' | ForEach-Object FullName)
$lootPaths+=Join-Path $lootWorkshop '2009463077\1.7\Assemblies\0Harmony.dll'
foreach($lootPath in $lootPaths){try{[void][Reflection.AssemblyName]::GetAssemblyName($lootPath);$lootRefs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($lootPath))}catch{}}
$lootTrees=[Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
foreach($lootFile in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs'){
 $lootTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($lootFile.FullName),$null,$lootFile.FullName,[Text.Encoding]::UTF8))
}
$lootOptions=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release)
$lootCompilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('LichPhylactery',$lootTrees,$lootRefs,$lootOptions)
$lootOutput=Join-Path $lootMod '1.7\Assemblies\LichPhylactery.dll'
New-Item -ItemType Directory -Path (Split-Path $lootOutput -Parent) -Force | Out-Null
$lootStream=[IO.File]::Create($lootOutput+'.new')
try{$lootResult=$lootCompilation.Emit($lootStream)}finally{$lootStream.Dispose()}
$lootResult.Diagnostics | Where-Object Severity -eq Error | ForEach-Object ToString
if(!$lootResult.Success){throw 'Lich Phylactery compilation failed.'}
Move-Item -LiteralPath ($lootOutput+'.new') -Destination $lootOutput -Force
Write-Output 'PASS: LichPhylactery built against installed RimWorld 1.7 and Harmony.'
# 1.33.0 HAR（Humanoid Alien Races，创意工坊 839005762）联动程序集：另外引用主程序集和 AlienRace.dll，输出到 Bridges/HAR/Assemblies。只放需要 HAR 类型的代码（检查）；种族规则都在主程序集。
$harDir=Join-Path $lootMod 'Bridges\HAR'
$harDll=Join-Path $lootWorkshop '839005762\1.7\Assemblies\AlienRace.dll'
if(Test-Path -LiteralPath $harDll){
 $harRefs=[Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new($lootRefs)
 foreach($harRef in @($lootOutput,$harDll)){$harRefs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($harRef))}
 $harTrees=[Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
 foreach($harFile in Get-ChildItem -LiteralPath (Join-Path $harDir 'Source') -Filter '*.cs'){
  $harTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($harFile.FullName),$null,$harFile.FullName,[Text.Encoding]::UTF8))
 }
 $harCompilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('LichPhylactery.HARBridge',$harTrees,$harRefs,$lootOptions)
 $harOutput=Join-Path $harDir 'Assemblies\LichPhylactery.HARBridge.dll'
 New-Item -ItemType Directory -Path (Split-Path $harOutput -Parent) -Force | Out-Null
 $harStream=[IO.File]::Create($harOutput+'.new')
 try{$harResult=$harCompilation.Emit($harStream)}finally{$harStream.Dispose()}
 $harResult.Diagnostics | Where-Object Severity -eq Error | ForEach-Object ToString
 if(!$harResult.Success){Remove-Item -LiteralPath ($harOutput+'.new') -Force;throw 'HAR bridge compilation failed.'}
 Move-Item -LiteralPath ($harOutput+'.new') -Destination $harOutput -Force
 Write-Output 'PASS: HAR race bridge built.'
}else{Write-Output "SKIP: HAR bridge (not found: $harDll)"}
# 1.27.0 VPE 职业路径联动程序集：另外引用主程序集、VEF.dll 和 VanillaPsycastsExpanded.dll（创意工坊），输出到 Bridges/VPE/Assemblies。放在神兽血裔段之前，因为那一段找不到依赖时用 return 结束脚本。
$vpeDir=Join-Path $lootMod 'Bridges\VPE'
$vefDll=Join-Path $lootWorkshop '2023507013\1.7\Assemblies\VEF.dll'
$vpeDll=Join-Path $lootWorkshop '2842502659\1.7\Assemblies\VanillaPsycastsExpanded.dll'
if((Test-Path -LiteralPath $vefDll) -and (Test-Path -LiteralPath $vpeDll)){
 $vpeRefs=[Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new($lootRefs)
 foreach($vpeRef in @($lootOutput,$vefDll,$vpeDll)){$vpeRefs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($vpeRef))}
 $vpeTrees=[Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
 foreach($vpeFile in Get-ChildItem -LiteralPath (Join-Path $vpeDir 'Source') -Filter '*.cs'){
  $vpeTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($vpeFile.FullName),$null,$vpeFile.FullName,[Text.Encoding]::UTF8))
 }
 $vpeCompilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('LichPhylactery.VPEBridge',$vpeTrees,$vpeRefs,$lootOptions)
 $vpeOutput=Join-Path $vpeDir 'Assemblies\LichPhylactery.VPEBridge.dll'
 New-Item -ItemType Directory -Path (Split-Path $vpeOutput -Parent) -Force | Out-Null
 $vpeStream=[IO.File]::Create($vpeOutput+'.new')
 try{$vpeResult=$vpeCompilation.Emit($vpeStream)}finally{$vpeStream.Dispose()}
 $vpeResult.Diagnostics | Where-Object Severity -eq Error | ForEach-Object ToString
 if(!$vpeResult.Success){Remove-Item -LiteralPath ($vpeOutput+'.new') -Force;throw 'VPE bridge compilation failed.'}
 Move-Item -LiteralPath ($vpeOutput+'.new') -Destination $vpeOutput -Force
 Write-Output 'PASS: VPE class bridge built.'
}else{Write-Output "SKIP: VPE bridge (not found: $vefDll or $vpeDll)"}
# 1.21.0 神兽血裔联动程序集：另外引用主程序集和 MythicBloodlines.dll，输出到 Bridges/MythicBloodlines/Assemblies。
$bridgeDir=Join-Path $lootMod 'Bridges\MythicBloodlines'
$mythicDll=Join-Path (Split-Path $lootMod -Parent) 'MythicBloodlines\1.7\Assemblies\MythicBloodlines.dll'
if(!(Test-Path -LiteralPath $mythicDll)){Write-Output "SKIP: Mythic Bloodlines bridge (not found: $mythicDll)";return}
$bridgeRefs=[Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new($lootRefs)
foreach($bridgeRef in @($lootOutput,$mythicDll)){$bridgeRefs.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($bridgeRef))}
$bridgeTrees=[Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
foreach($bridgeFile in Get-ChildItem -LiteralPath (Join-Path $bridgeDir 'Source') -Filter '*.cs'){
 $bridgeTrees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($bridgeFile.FullName),$null,$bridgeFile.FullName,[Text.Encoding]::UTF8))
}
$bridgeCompilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('LichPhylactery.MythicBridge',$bridgeTrees,$bridgeRefs,$lootOptions)
$bridgeOutput=Join-Path $bridgeDir 'Assemblies\LichPhylactery.MythicBridge.dll'
New-Item -ItemType Directory -Path (Split-Path $bridgeOutput -Parent) -Force | Out-Null
$bridgeStream=[IO.File]::Create($bridgeOutput+'.new')
try{$bridgeResult=$bridgeCompilation.Emit($bridgeStream)}finally{$bridgeStream.Dispose()}
$bridgeResult.Diagnostics | Where-Object Severity -eq Error | ForEach-Object ToString
if(!$bridgeResult.Success){Remove-Item -LiteralPath ($bridgeOutput+'.new') -Force;throw 'Mythic Bloodlines bridge compilation failed.'}
Move-Item -LiteralPath ($bridgeOutput+'.new') -Destination $bridgeOutput -Force
Write-Output 'PASS: Mythic Bloodlines bridge built.'
