# 1.100.0 魔力湖泊：按 Defs/ManaLake_Arcana.xml 里的秘传能力生成 Defs/ManaLake_Crystals.xml（每个能力一枚结晶），并写出 icons.json 给 draw_manalake.py 画图标。
# 用法：pwsh ArtSource/ManaLake/gen_crystals.ps1（在模组根目录）。改了能力的名字、环数、职业后重新跑一次。
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$classColor=@{}
foreach($f in 'Classes.xml','Classes2.xml','Classes4.xml','ClassWarlock.xml'){
 $t=[IO.File]::ReadAllText((Join-Path $root "Defs\$f"))
 foreach($m in [regex]::Matches($t,'<defName>(LP_Class_\w+)</defName>\s*<label>([^<]*)</label>[\s\S]*?<color>([^<]+)</color>')){$classColor[$m.Groups[1].Value]=@{label=$m.Groups[2].Value;color=$m.Groups[3].Value}}
}
[xml]$x=[IO.File]::ReadAllText((Join-Path $root 'Defs\ManaLake_Arcana.xml'))
$price=@{6=8000;7=12000;8=16000;9=25000};$need=@{6=11;7=13;8=15;9=17}
$sb=[Text.StringBuilder]::new()
[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<!-- 1.100.0 魔力湖泊的秘传结晶：由 ArtSource/ManaLake/gen_crystals.ps1 按 ManaLake_Arcana.xml 生成，不要手改。 -->')
[void]$sb.AppendLine('<Defs>')
[void]$sb.AppendLine(@'
  <ThingDef Name="LP_ArcanumCrystalBase" ParentName="ResourceBase" Abstract="True">
    <graphicData>
      <texPath>LP/Items/ArcanumCrystal</texPath>
      <graphicClass>Graphic_Single</graphicClass>
    </graphicData>
    <stackLimit>5</stackLimit>
    <techLevel>Medieval</techLevel>
    <thingCategories><li>Items</li></thingCategories>
    <tradeTags><li>LP_ArcanumCrystal</li></tradeTags>
    <statBases>
      <Mass>0.2</Mass>
      <MaxHitPoints>200</MaxHitPoints>
      <Flammability>0</Flammability>
      <Beauty>4</Beauty>
    </statBases>
    <comps>
      <li Class="CompProperties_Usable">
        <useJob>UseItem</useJob>
        <useLabel>吸收{0_label}</useLabel>
        <useDuration>2500</useDuration>
      </li>
    </comps>
  </ThingDef>
'@)
$icons=@()
foreach($a in $x.Defs.AbilityDef){
 if($a.Abstract -eq 'True' -or !$a.defName){continue}
 $ext=$a.modExtensions.li;if(!$ext){continue}
 $key=$a.defName.Substring(6);$ring=[int]$ext.ring;$cls=$ext.cls;$c=$classColor[$cls]
 $passive=$a.comps.li.passive -eq 'true'
 $desc="魔力湖泊的职业水晶。"+$c.label+"吸收后永久学会"+$(if($passive){"被动"}else{""})+"秘传「"+$a.label+"」（"+$ring+" 环，需要"+$c.label+" "+$need[$ring]+" 级，占 1 个秘传槽；冒险者 11 / 15 / 20 级、传奇 30 级各有 1 个秘传槽）。\n\n"+$a.description
 [void]$sb.AppendLine("  <ThingDef ParentName=`"LP_ArcanumCrystalBase`">")
 [void]$sb.AppendLine("    <defName>LP_Crystal_$key</defName>")
 [void]$sb.AppendLine("    <label>职业水晶：$($a.label)</label>")
 [void]$sb.AppendLine("    <description>$([Security.SecurityElement]::Escape($desc))</description>")
 [void]$sb.AppendLine("    <graphicData><color>$($c.color)</color></graphicData>")
 [void]$sb.AppendLine("    <statBases><MarketValue>$($price[$ring])</MarketValue></statBases>")
 [void]$sb.AppendLine("    <comps><li Class=`"LichPhylactery.CompProperties_Arcanum`"><ability>$($a.defName)</ability></li></comps>")
 [void]$sb.AppendLine("  </ThingDef>")
 $icons+=[pscustomobject]@{key=$key;label=$a.label;color=$c.color;ring=$ring;passive=$passive}
}
[void]$sb.AppendLine('</Defs>')
[IO.File]::WriteAllText((Join-Path $root 'Defs\ManaLake_Crystals.xml'),$sb.ToString(),[Text.UTF8Encoding]::new($false))
$icons | ConvertTo-Json | Set-Content -Path (Join-Path $PSScriptRoot 'icons.json') -Encoding utf8
"crystals: $($icons.Count)"
