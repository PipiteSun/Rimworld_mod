# 1.86.0：-CE 在 Core 档里加上角色编辑器（Character Editor，创意工坊 1874644848），配合 -Only CharEditor 查 DND 编辑面板联动。
# 2026-10-29：-RPG 在 Core 档里加上 RPG Style Inventory Revamped（创意工坊 2478833213），配合 -Only RPGInventory 查 DND 槽位联动。
# 1.56.0：-NoIdeology 从 Core 档的 ModsConfig 里去掉文化 DLC（ludeon.rimworld.ideology），档名加 -NoIdeology，用来确认文化桥不加载时照常运行（照 -NoHAR 的写法）。
# 1.33.0：所有档默认在本模组之前加上 Humanoid Alien Races（创意工坊 839005762，六臂蛇人联动）；-NoHAR 不加，用来确认没有 HAR 时照常加载。
# 1.21.0：-Mythic / -Aptitudes 在 Core 档里加上神兽血裔 / 人类异禀（放在本模组之前），跑完整的 -lichtest，包括联动检查。
# 1.27.0：-VPE 在 Core 档里加上 VEF 与 Vanilla Psycasts Expanded（创意工坊，放在本模组之前），只跑职业路径检查（-lichclassesonly）和存读档，并照常检查日志红字。
# 1.22.0：-Isekai 在 Core 档里加上 Isekai RPG Leveling 与地下城附加包（放在本模组之前），只跑物品与联动检查（-lichitemsonly）和存读档，并照常检查日志红字。
# 1.28.0：-Only <组名>（如 -Only Events，多个用逗号：-Only Events,Beasts）在 Core 档里只跑这些检查组（Check<组名>）和它们的存读档部分；不写 -Only 时照旧跑全部。
param([string]$Only="",[switch]$Warcasket,[switch]$Full,[switch]$Appearance,[switch]$VisualCompat,[switch]$HealthCompat,[ValidateSet("None","Human","Mythic","Both")][string]$HealthProvider="None",[switch]$Mythic,[switch]$Aptitudes,[switch]$Isekai,[switch]$VPE,[switch]$NoHAR,[switch]$NoIdeology,[switch]$RPG,[switch]$CE)
$ErrorActionPreference='Stop'
$lichGame=Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent
$lichProfile=Join-Path $lichGame ('StandaloneMods\Validation\Lich-'+$(if($Warcasket){'Warcasket'}elseif($HealthCompat){'Health-'+$(if($Full){'Full'}else{$HealthProvider})}elseif($VisualCompat -and $Full){'VisualFull'}elseif($VisualCompat){'VisualCompat'}elseif($Full){'Full'}elseif($Appearance){'Appearance'}else{'Core'})+$(if($Mythic){'-Mythic'}else{''})+$(if($Aptitudes){'-Aptitudes'}else{''})+$(if($Isekai){'-Isekai'}else{''})+$(if($VPE){'-VPE'}else{''})+$(if($NoHAR){'-NoHAR'}else{''})+$(if($NoIdeology){'-NoIdeology'}else{''})+$(if($RPG){'-RPG'}else{''})+$(if($CE){'-CE'}else{''})+$(if($Only){'-Only'}else{''}))
New-Item -ItemType Directory -Path (Join-Path $lichProfile 'Config') -Force | Out-Null
$lichUser=Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios'
if($Full){[xml]$lichConfig=Get-Content (Join-Path $lichUser 'Config\ModsConfig.xml') -Raw}else{[xml]$lichConfig='<ModsConfigData><version>1.7.4984 rev1242</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li><li>ludeon.rimworld.royalty</li>'+$(if($NoIdeology){''}else{'<li>ludeon.rimworld.ideology</li>'})+'<li>ludeon.rimworld.biotech</li><li>ludeon.rimworld.odyssey</li><li>local.lich.phylactery</li></activeMods></ModsConfigData>'}
if(@($lichConfig.ModsConfigData.activeMods.li) -notcontains 'local.lich.phylactery'){$n=$lichConfig.CreateElement('li');$n.InnerText='local.lich.phylactery';[void]$lichConfig.ModsConfigData.activeMods.AppendChild($n)}
if($Appearance){$n=$lichConfig.CreateElement('li');$n.InnerText='local.standalone.occupationalarmor';[void]$lichConfig.ModsConfigData.activeMods.AppendChild($n)}
foreach($extra in @($(if(!$NoHAR){'erdelf.humanoidalienraces'}),$(if($Aptitudes){'local.standalone.humanaptitudes'}),$(if($Mythic){'local.standalone.mythicbloodlines'}),$(if($Isekai){'jellycreative.isekaileveling'}),$(if($Isekai){'poupun.isekaidungeonsaddon'}),$(if($VPE){'oskarpotocki.vanillafactionsexpanded.core'}),$(if($VPE){'vanillaexpanded.vpsycastse'}),$(if($RPG){'sandy.rpgstyleinventory.avilmask.revamped'}),$(if($CE){'void.charactereditor'}))){if(!$extra -or @($lichConfig.ModsConfigData.activeMods.li) -contains $extra){continue};$n=$lichConfig.CreateElement('li');$n.InnerText=$extra;$lichNode=@($lichConfig.ModsConfigData.activeMods.ChildNodes | Where-Object InnerText -eq 'local.lich.phylactery')[0];[void]$lichConfig.ModsConfigData.activeMods.InsertBefore($n,$lichNode)}
if($VisualCompat -and !$Full){
 [xml]$lichConfig=Get-Content (Join-Path $lichUser 'Config\ModsConfig.xml') -Raw
 foreach($n in @($lichConfig.ModsConfigData.activeMods.ChildNodes)){
  if($n.InnerText -notmatch '^(brrainz\.harmony|ludeon\.rimworld(\..+)?|oskarpotocki\.vanillafactionsexpanded\.core|oskarpotocki\.vfe\.pirates|shunter\.toxinwarcasket|kierannkkt\.mechlordwarcasket|vanillaracesexpanded\..+|void\.charactereditor|local\.lich\.phylactery|local\.standalone\.occupationalarmor|vanillaexpanded\.viehar|vanillaexpanded\.viesas|vanillaexpanded\.ideo\.sophianstyle)$'){[void]$lichConfig.ModsConfigData.activeMods.RemoveChild($n)}
 }
}
if($HealthCompat -and !$Full){foreach($provider in @("Human","Mythic")){if($HealthProvider -eq $provider -or $HealthProvider -eq "Both"){$n=$lichConfig.CreateElement("li");$n.InnerText=if($provider -eq "Human"){"local.standalone.humanaptitudes"}else{"local.standalone.mythicbloodlines"};[void]$lichConfig.ModsConfigData.activeMods.AppendChild($n)}}}
if($Warcasket){
 [xml]$lichConfig=Get-Content (Join-Path $lichUser 'Config\ModsConfig.xml') -Raw
 foreach($n in @($lichConfig.ModsConfigData.activeMods.ChildNodes)){
  if($n.InnerText -notmatch '^(brrainz\.harmony|ludeon\.rimworld(\..+)?|oskarpotocki\.vanillafactionsexpanded\.core|oskarpotocki\.vfe\.pirates|shunter\.toxinwarcasket|kierannkkt\.mechlordwarcasket|local\.lich\.phylactery)$'){[void]$lichConfig.ModsConfigData.activeMods.RemoveChild($n)}
 }
}
$lichConfig.Save((Join-Path $lichProfile 'Config\ModsConfig.xml'))
Copy-Item -LiteralPath (Join-Path $lichUser 'Config\Prefs.xml') -Destination (Join-Path $lichProfile 'Config\Prefs.xml') -Force
# 1.63.0：玩家开了开发者选项「测试用小地图」(testMapSizes) 时，自检地图会变小，吐息形状、空地之类的检查找不到地方。自检里固定关掉，其余偏好照抄。
$lichPrefsPath=Join-Path $lichProfile 'Config\Prefs.xml';[xml]$lichPrefs=Get-Content -LiteralPath $lichPrefsPath -Raw;$lichTms=$lichPrefs.SelectSingleNode('//testMapSizes');if($lichTms){$lichTms.InnerText='False';$lichPrefs.Save($lichPrefsPath)}
$lichLinks=@();$lichProc=$null
# 1.27.3：测试进程也会写共享的 Logs\Player.log / Player-prev.log，会把玩家真实游戏的日志冲掉；跑之前备份，跑完放回（测试自己的日志在隔离目录的 stdout.txt 里）。
$lichPlayerLogs=Join-Path $lichUser 'Logs';$lichLogBackup=Join-Path $lichProfile 'PlayerLogsBackup'
if(Test-Path -LiteralPath $lichPlayerLogs){New-Item -ItemType Directory -Path $lichLogBackup -Force | Out-Null;Get-ChildItem -LiteralPath $lichLogBackup -File | Remove-Item -Force;Copy-Item -Path (Join-Path $lichPlayerLogs '*') -Destination $lichLogBackup -Force}
try {
 if($true){
  $lichWorkshop=Join-Path (Split-Path (Split-Path $lichGame -Parent) -Parent) 'workshop\content\294100'
  foreach($lichFolder in Get-ChildItem -LiteralPath $lichWorkshop -Directory){
   $lichAbout=Join-Path $lichFolder.FullName 'About\About.xml';if(!(Test-Path -LiteralPath $lichAbout)){continue}
   [xml]$lichMeta=Get-Content -LiteralPath $lichAbout -Raw
   if(@($lichConfig.ModsConfigData.activeMods.li) -notcontains [string]$lichMeta.ModMetaData.packageId){continue}
   $lichLink=Join-Path $lichGame ('Mods\_LichTest_'+$lichFolder.Name)
   if(Test-Path -LiteralPath $lichLink){throw "Temporary directory occupied: $lichLink"}
   New-Item -ItemType Junction -Path $lichLink -Value $lichFolder.FullName | Out-Null
   $lichLinks+=[pscustomobject]@{Path=$lichLink;Target=$lichFolder.FullName}
  }
 }
 $lichStart=Get-Date
 $lichLog=Join-Path $lichProfile 'stdout.txt'
 $lichArgs=@('-quicktest','-lichtest','-lichuitest','-screen-fullscreen','0','-screen-width','1200','-screen-height','800',('-savedatafolder="'+$lichProfile+'"'))
 if($Warcasket){$lichArgs+='-lichwarcasket'}
 if($VisualCompat){$lichArgs+='-lichvisualcompat'}
 if($HealthCompat){$lichArgs+='-lichhealthcompat'}
 if($Isekai){$lichArgs+='-lichitemsonly'}
 if($VPE){$lichArgs+='-lichclassesonly'}
 if($Only){$lichArgs+=('-lichonly='+($Only -replace '\s',''))}
 $lichProc=Start-Process -FilePath (Join-Path $lichGame 'RimWorld.exe') -WorkingDirectory $lichGame -WindowStyle Hidden -RedirectStandardOutput $lichLog -RedirectStandardError (Join-Path $lichProfile 'stderr.txt') -ArgumentList $lichArgs -PassThru
 Write-Output "Isolated lich test process $($lichProc.Id)"
 while(!$lichProc.HasExited){if(((Get-Date)-$lichStart).TotalSeconds -gt 360){throw 'Lich test timeout'};$lichProc.WaitForExit(1000)|Out-Null}
 $lichReport=Join-Path $lichProfile 'LichResult.txt'
 if(!(Test-Path -LiteralPath $lichReport) -or (Get-Item -LiteralPath $lichReport).LastWriteTime -lt $lichStart){throw "No fresh result. See $lichLog"}
 $lichResult=Get-Content -LiteralPath $lichReport -Raw;Write-Output $lichResult
 if(!$lichResult.StartsWith('PASS')){throw 'Lich integration check failed'}
 $lichErrors=@(Select-String -LiteralPath $lichLog -Pattern '^\S+\s+ERROR|XML error|Could not resolve cross-reference|Config error|Could not load Texture2D|Error while' | Where-Object {$_.Line -notmatch 'EOSOverlay'})
 if($lichErrors.Count){$lichErrors | Select-Object -First 30 | ForEach-Object Line;throw 'Errors found in test log'}
}finally{
 if($lichProc -and !$lichProc.HasExited){$lichProc.Kill()}
 if($lichProc){$lichProc.WaitForExit(10000)|Out-Null}
 if((Test-Path -LiteralPath $lichLogBackup) -and (Get-ChildItem -LiteralPath $lichLogBackup -File)){Copy-Item -Path (Join-Path $lichLogBackup '*') -Destination $lichPlayerLogs -Force}
 foreach($lichEntry in $lichLinks){
  $lichResolved=[IO.Path]::GetFullPath($lichEntry.Path);$lichAllowed=[IO.Path]::GetFullPath((Join-Path $lichGame 'Mods'))+'\'
  if(!$lichResolved.StartsWith($lichAllowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe test cleanup path'}
  $lichItem=Get-Item -LiteralPath $lichResolved -ErrorAction SilentlyContinue
  if($lichItem -and $lichItem.LinkType -eq 'Junction' -and @($lichItem.Target)[0] -eq $lichEntry.Target){[IO.Directory]::Delete($lichResolved,$false)}
 }
}

