$ErrorActionPreference='Stop'
$mod=Split-Path $PSScriptRoot -Parent
$doc=[xml](Get-Content (Join-Path $mod 'Defs/FireGiantEquipment.xml') -Raw)
$names=@('Maul','Helmet','Armor')
$labels=@('熔核破城锤','炉心战盔','熔核重甲')
$descs=@('熔核禁卫的破城重锤。基础钝伤48、穿甲55%、攻击间隔3.6秒。仅限火巨人装备，在熔火锻炉制作。与炉心战盔、熔核重甲共同解锁炉心过载。','熔核禁卫的封闭式战盔。基础利器护甲150%、钝器60%；火巨人穿戴时受到的眩晕持续时间减少30%。仅限火巨人，在熔火锻炉制作。','熔核禁卫的高级重甲。基础利器护甲160%、钝器75%，移动速度-0.25。仅限火巨人，在熔火锻炉制作。全套技能炉心过载：10秒近战伤害+25%、移动速度+15%；随后10秒移动速度-15%。冷却90秒，从启动时计时；卸下套装不能重置。')
$costs=@('<Steel>300</Steel><Plasteel>100</Plasteel><Uranium>30</Uranium><ComponentSpacer>2</ComponentSpacer>','<Steel>120</Steel><Plasteel>60</Plasteel><Gold>20</Gold><ComponentSpacer>2</ComponentSpacer>','<Steel>300</Steel><Plasteel>180</Plasteel><Uranium>50</Uranium><ComponentSpacer>4</ComponentSpacer>')
for($i=0;$i -lt 3;$i++){
 $d=$doc.Defs.ThingDef[$i];$d.defName='LP_Molten'+$names[$i];$d.label=$labels[$i];$d.description=$descs[$i];$d.techLevel='Spacer'
 $d.graphicData.texPath='FireGiant/MoltenGuard/'+$names[$i];$d.costList.InnerXml=$costs[$i]
 $d.recipeMaker.researchPrerequisite='LP_MoltenForging';$d.recipeMaker.skillRequirements.Crafting=if($i -eq 2){'12'}else{'10'}
 $filter=$doc.CreateElement('defaultIngredientFilter');$filter.SetAttribute('Inherit','False');$filter.InnerXml='<thingDefs>'+ (($d.costList.ChildNodes | ForEach-Object {'<li>'+$_.Name+'</li>'}) -join '')+'</thingDefs>';[void]$d.recipeMaker.AppendChild($filter)
 $d.statBases.MaxHitPoints=@('650','450','800')[$i];$d.statBases.WorkToMake=@('36000','24000','65000')[$i]
 if($i -eq 0){$d.tools.li.power='48';$d.tools.li.armorPenetration='0.55'}
 else{$d.apparel.wornGraphicPath='FireGiant/MoltenGuard/Worn/'+$names[$i];$d.statBases.ArmorRating_Sharp=@('','1.5','1.6')[$i];$d.statBases.ArmorRating_Blunt=@('','0.6','0.75')[$i];$d.statBases.ArmorRating_Heat='1.0'}
 if($i -eq 2){$node=$doc.CreateElement('thingClass');$node.InnerText='LichPhylactery.Apparel_MoltenArmor';[void]$d.AppendChild($node);$d.equippedStatOffsets.MoveSpeed='-0.25'}
}
foreach($d in @($doc.Defs.ThingDef)){
 $recipe=$doc.CreateElement('RecipeDef')
 $recipe.InnerXml='<defName>Make_'+$d.defName+'</defName><label>制作'+$d.label+'</label><description>在熔火锻炉制作'+$d.label+'。</description><jobString>正在制作'+$d.label+'。</jobString><workAmount>'+$d.statBases.WorkToMake+'</workAmount><workSpeedStat>GeneralLaborSpeed</workSpeedStat><workSkill>Crafting</workSkill><effectWorking>Smith</effectWorking><soundWorking>Recipe_Smith</soundWorking><ingredientValueGetterClass>LichPhylactery.RitualCount</ingredientValueGetterClass><ingredients>'+ (($d.costList.ChildNodes | ForEach-Object {'<li><filter><thingDefs><li>'+$_.Name+'</li></thingDefs></filter><count>'+$_.InnerText+'</count></li>'}) -join '')+'</ingredients><fixedIngredientFilter>'+ $d.recipeMaker.defaultIngredientFilter.InnerXml+'</fixedIngredientFilter><products><'+$d.defName+'>1</'+$d.defName+'></products>'
 foreach($name in @('recipeUsers','researchPrerequisite','skillRequirements','unfinishedThingDef')){ $node=$d.recipeMaker.SelectSingleNode($name);if($node){[void]$recipe.AppendChild($node.CloneNode($true))} }
 [void]$d.RemoveChild($d.recipeMaker);$empty=$doc.CreateElement('recipeMaker');$empty.SetAttribute('IsNull','True');[void]$d.AppendChild($empty);[void]$doc.Defs.AppendChild($recipe)
}
$extra=[xml]@'
<Defs>
 <!-- 1.35.0：页签和“熔核锻造”研究移到 Defs/Research.xml。 -->
 <HediffDef><defName>LP_MoltenOverdrive</defName><label>炉心过载</label><description>熔核禁卫过载与疲劳状态。冷却保存在角色身上，不随换装重置。</description><hediffClass>LichPhylactery.Hediff_MoltenOverdrive</hediffClass><isBad>false</isBad></HediffDef>
</Defs>
'@
foreach($n in $extra.Defs.ChildNodes){[void]$doc.Defs.AppendChild($doc.ImportNode($n,$true))}
$doc.Save((Join-Path $mod 'Defs/MoltenGuard.xml'))


