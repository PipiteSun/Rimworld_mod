# 1.133.0 魔力湖泊第三、四批：按 Defs/ManaLake_Lore.xml（通用秘传）和 Defs/ManaLake_Seals.xml（湖印）生成 Defs/ManaLake_Crystals2.xml（每个能力一枚结晶），
# 并写出 icons2.json 给 draw_lore.py 画图标。用法：pwsh ArtSource/ManaLake/gen_lore.ps1（在哪都行）。改了能力的名字、环数、价格后重新跑一次。
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sb=[Text.StringBuilder]::new()
[void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
[void]$sb.AppendLine('<!-- 1.133.0 魔力湖泊的秘传结晶（通用秘传）与湖印结晶：由 ArtSource/ManaLake/gen_lore.ps1 按 ManaLake_Lore.xml、ManaLake_Seals.xml 生成，不要手改。 -->')
[void]$sb.AppendLine('<Defs>')
[void]$sb.AppendLine(@'
  <ThingDef Name="LP_LakeSealCrystalBase" ParentName="ResourceBase" Abstract="True">
    <graphicData>
      <texPath>LP/Items/ArcanumCrystal</texPath>
      <graphicClass>Graphic_Single</graphicClass>
    </graphicData>
    <stackLimit>5</stackLimit>
    <techLevel>Medieval</techLevel>
    <thingCategories><li>Items</li></thingCategories>
    <tradeTags><li>LP_LakeSealCrystal</li></tradeTags>
    <statBases>
      <Mass>0.1</Mass>
      <MaxHitPoints>150</MaxHitPoints>
      <Flammability>0</Flammability>
      <Beauty>2</Beauty>
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
$need=@{6=11;7=13;8=15;9=17};$gate=@{6=15;7=16;8=17;9=18};$skill=@{6=10;7=12;8=14;9=16};$base=@{6=80;7=70;8=60;9=50}
$abil=@{Int='智力';Wis='感知';Cha='魅力';Str='力量';Dex='敏捷';Any='最高的一项属性'}
[xml]$lore=[IO.File]::ReadAllText((Join-Path $root 'Defs\ManaLake_Lore.xml'))
foreach($a in $lore.Defs.AbilityDef){
 if($a.Abstract -eq 'True' -or !$a.defName){continue}
 $ext=@($a.modExtensions.li)|Where-Object{$_.ring};if(!$ext){continue}
 $key=$a.defName.Substring(6);$ring=[int]$ext.ring;$kind=if($ext.kind){$ext.kind}else{'spell'};$price=[int]$ext.price
 $gateText="需要冒险者 "+$need[$ring]+" 级、"+$abil[$ext.ability]+" "+$gate[$ring]+$(if($kind -eq 'martial'){"、格斗或射击 "+$skill[$ring]+" 级"}else{""})
 $desc="魔力湖泊的秘传结晶（通用秘传，任何职业都能学）。吸收需要"+$gateText.Substring(2)+"，占 1 个秘传槽；基础吸收成功率 "+$base[$ring]+"%，属性每超过门槛 1 点 +5%。失败时结晶不碎，但会秘传反噬 3 天，7 天后才能再试。\n\n"+$a.description
 [void]$sb.AppendLine("  <ThingDef ParentName=`"LP_ArcanumCrystalBase`">")
 [void]$sb.AppendLine("    <defName>LP_Crystal_$key</defName>")
 [void]$sb.AppendLine("    <label>秘传结晶：$($a.label)</label>")
 [void]$sb.AppendLine("    <description>$([Security.SecurityElement]::Escape($desc))</description>")
 [void]$sb.AppendLine("    <graphicData><color>(0.95,0.85,0.55)</color></graphicData>")
 [void]$sb.AppendLine("    <tradeTags Inherit=`"False`"><li>LP_ArcanumCrystalLore</li></tradeTags>")
 [void]$sb.AppendLine("    <statBases><MarketValue>$price</MarketValue></statBases>")
 [void]$sb.AppendLine("    <comps><li Class=`"LichPhylactery.CompProperties_Arcanum`"><ability>$($a.defName)</ability></li></comps>")
 [void]$sb.AppendLine("  </ThingDef>")
 $icons+=[pscustomobject]@{key=$key;label=$a.label;color='(0.95,0.85,0.55)';ring=$ring;passive=($kind -eq 'passive')}
}
$tierGate=@{1='冒险者 1 级、智识 4 级，吸收成功率 100%';2='冒险者 5 级、智识 6 级，吸收成功率 90% + 智识 ×1%';3='冒险者 10 级、智识 8 级，吸收成功率 70% + 智识 ×2%（要和魔力湖泊结盟才买得到）'}
[xml]$seals=[IO.File]::ReadAllText((Join-Path $root 'Defs\ManaLake_Seals.xml'))
foreach($a in $seals.Defs.AbilityDef){
 if($a.Abstract -eq 'True' -or !$a.defName){continue}
 $ext=@($a.modExtensions.li)|Where-Object{$_.tier};if(!$ext){continue}
 $key=$a.defName.Substring(6);$tier=[int]$ext.tier;$price=[int]$ext.price;$passive=$a.comps.li.passive -eq 'true'
 $desc="魔力湖泊的湖印结晶。吸收（约 1 小时）后永久学会湖印"+$(if($passive){"被动"}else{"法术"})+"「"+$a.label+"」，占 1 个湖印"+$(if($passive){"被动"}else{"法术"})+"槽。"+$tierGate[$tier]+"；失败时结晶碎掉，小人湖音回响 1 天。巫妖吸收不会失败。\n\n"+$a.description
 [void]$sb.AppendLine("  <ThingDef ParentName=`"LP_LakeSealCrystalBase`">")
 [void]$sb.AppendLine("    <defName>LP_SealCrystal_$key</defName>")
 [void]$sb.AppendLine("    <label>湖印结晶：$($a.label)</label>")
 [void]$sb.AppendLine("    <description>$([Security.SecurityElement]::Escape($desc))</description>")
 [void]$sb.AppendLine("    <graphicData><color>(0.45,0.85,0.8)</color><drawSize>0.8</drawSize></graphicData>")
 [void]$sb.AppendLine("    <statBases><MarketValue>$price</MarketValue></statBases>")
 [void]$sb.AppendLine("    <comps><li Class=`"LichPhylactery.CompProperties_LakeSeal`"><ability>$($a.defName)</ability></li></comps>")
 [void]$sb.AppendLine("  </ThingDef>")
 $icons+=[pscustomobject]@{key=$key;label=$a.label;color='(0.45,0.85,0.8)';ring=$tier;passive=$passive}
}
[void]$sb.AppendLine('</Defs>')
[IO.File]::WriteAllText((Join-Path $root 'Defs\ManaLake_Crystals2.xml'),$sb.ToString(),[Text.UTF8Encoding]::new($false))
$icons | ConvertTo-Json | Set-Content -Path (Join-Path $PSScriptRoot 'icons2.json') -Encoding utf8
"crystals: $($icons.Count)"
