# 1.52.0 五色真龙：生成 Defs/ChromaticDragons_Items.xml（龙鳞、真龙之心、龙之血、龙牙、配方、真龙之躯）
# 和 Bridges/HAR/Defs/ChromaticDragons_Race.xml（5 个 HAR 种族、龙头型、15 个兵种）。数值见 ArtSource/ChromaticDragons-1.52.0-spec.md。
# 用法：python -I ArtSource/ChromaticDragons-gen_defs.py <模组根目录>
import os, sys

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.getcwd()

COLORS = [
    # key, 中文, 物品色, 免疫(damageFactors), 额外 statOffsets, 热护甲倍率, 冷/热隔热
    ("Red", "红", (150, 52, 44), ["Flame", "Burn"], "<ComfyTemperatureMax>200</ComfyTemperatureMax>", 2.0, (3, 6)),
    ("Blue", "蓝", (64, 92, 150), ["LP_Shock", "EMP"], "", 1.0, (3, 0)),
    ("Green", "绿", (72, 120, 64), ["LP_DragonPoison"], "<ToxicResistance>1</ToxicResistance>", 1.0, (3, 0)),
    ("Black", "黑", (58, 54, 64), ["LP_AlchemyAcid", "LP_OozeAcid"], "", 1.0, (3, 0)),
    ("White", "白", (214, 224, 230), ["LP_Frost", "Frostbite"], "<ComfyTemperatureMin>-200</ComfyTemperatureMin>", 2.0, (6, 0)),
]
ELEMENT = {"Red": "火焰", "Blue": "闪电", "Green": "毒素", "Black": "强酸", "White": "寒冷"}
BREATH = {"Red": "火焰锥", "Blue": "闪电线", "Green": "毒气锥", "Black": "强酸线", "White": "寒冰锥"}
# 5e 怪物书六维（力 敏 体 智 感 魅）：青年 / 成年 / 远古
SCORES = {
    "Red": [(23, 10, 21, 14, 11, 19), (27, 10, 25, 16, 13, 21), (30, 10, 29, 18, 15, 23)],
    "Blue": [(21, 10, 19, 14, 13, 17), (25, 10, 23, 16, 15, 19), (29, 10, 27, 18, 17, 21)],
    "Green": [(19, 12, 17, 16, 13, 15), (23, 12, 21, 18, 15, 17), (27, 12, 25, 20, 17, 19)],
    "Black": [(19, 14, 17, 12, 11, 15), (23, 14, 21, 14, 13, 17), (27, 14, 25, 16, 15, 19)],
    "White": [(18, 10, 18, 6, 11, 12), (22, 10, 22, 8, 12, 12), (26, 10, 26, 10, 13, 14)],
}
CR = {"Red": (10, 17, 24), "Blue": (9, 16, 23), "Green": (8, 15, 22), "Black": (7, 14, 21), "White": (6, 13, 20)}
STAGES = [("Young", "青年", 600, "10~12", 0, 0, 0, False, 0), ("Adult", "成年", 1500, "18~20", 3, 3, 18, True, 2), ("Ancient", "远古", 3500, "31~35", 3, 3, 24, True, 4)]
DESC = {
    "Red": "最强大也最贪婪的五色龙。红龙住在火山和高山里，睡在金山上，喷吐烈焰。它们傲慢、残忍，认为世上一切财宝本来就该属于自己。",
    "Blue": "沙漠与荒原的主人。蓝龙能在沙地下潜行，从地底钻出伏击猎物，喷出一道笔直的闪电。它们虚荣而讲究秩序，喜欢役使龙教徒。",
    "Green": "森林里最狡诈的龙。绿龙喷吐毒气，擅长谎言和魅惑，会在水里潜行。它们宁可让猎物自己走进巢穴。",
    "Black": "沼泽里最阴毒的龙。黑龙的头骨般的脸上满是腐蚀的痕迹，喷出一道强酸，能溶穿铠甲。它们的巢穴总是一片漆黑。",
    "White": "最小也最野蛮的五色龙。白龙住在冰原和冰川里，喷吐寒冰，不屑于讨价还价，看到猎物就扑上去。",
}


def w(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def offsets(extra):
    s = extra
    if "ComfyTemperatureMin" not in s:
        s += "<ComfyTemperatureMin>-40</ComfyTemperatureMin>"
    if "ComfyTemperatureMax" not in s:
        s += "<ComfyTemperatureMax>40</ComfyTemperatureMax>"
    return s


def items():
    o = ['<?xml version="1.0" encoding="utf-8"?>',
         '<!-- 1.52.0 五色真龙的战利品与身体状态（由 ArtSource/ChromaticDragons-gen_defs.py 生成）。不依赖 HAR：物品照常加载，只是没装 HAR 时得不到。 -->',
         '<Defs>', '',
         '  <DamageDef>', '    <defName>LP_DragonPoison</defName>', '    <label>龙毒</label>', '    <workerClass>DamageWorker_AddInjury</workerClass>',
         '    <externalViolence>true</externalViolence>', '    <deathMessage>{0}被龙毒毒死了。</deathMessage>', '    <hediff>ChemicalBurn</hediff>',
         '    <armorCategory>Heat</armorCategory>', '    <defaultDamage>9</defaultDamage>', '    <defaultArmorPenetration>0.2</defaultArmorPenetration>', '  </DamageDef>', '',
         '  <!-- 真龙之躯：不饿不困、没有心情、不会自发精神崩溃；本色元素免疫。年龄档的护甲、移速、体型、生命、近战在 HAR 种族的生命阶段里。 -->',
         '  <HediffDef Name="LP_DragonBodyBase" Abstract="True">', '    <hediffClass>LichPhylactery.Hediff_DragonBody</hediffClass>',
         '    <description>真龙的身体：不需要吃饭睡觉，不会自己精神崩溃（外力造成的精神状态由传奇抗性处理），对本色元素免疫。吐息每 6 秒掷一次充能（1/3）。倒地即死，不能俘虏。</description>',
         '    <defaultLabelColor>(1, 0.75, 0.45)</defaultLabelColor>', '    <isBad>false</isBad>', '    <everCurableByItem>false</everCurableByItem>', '  </HediffDef>']
    for key, zh, col, imm, extra, heat, ins in COLORS:
        dfs = []
        for d in imm:
            name, _, req = d.partition("|")
            attr = f' MayRequire="{req}"' if req else ""
            dfs.append(f'<{name}{attr}>0</{name}>')
        o += ['  <HediffDef ParentName="LP_DragonBodyBase">', f'    <defName>LP_DragonBody_{key}</defName>', f'    <label>真龙之躯（{zh}）</label>',
              '    <stages>', '      <li>', '        <painFactor>0.6</painFactor>', '        <blocksMentalBreaks>true</blocksMentalBreaks>',
              '        <disablesNeeds><li>Food</li><li>Rest</li><li>Joy</li><li>Beauty</li><li>Comfort</li><li>Outdoors</li><li>RoomSize</li><li MayRequire="Ludeon.RimWorld.Ideology">Indoors</li></disablesNeeds>',
              f'        <damageFactors>{"".join(dfs)}</damageFactors>',
              f'        <statOffsets>{offsets(extra)}</statOffsets>',
              '      </li>', '    </stages>', '  </HediffDef>']
    o.append('')
    # 龙鳞
    for key, zh, col, imm, extra, heat, ins in COLORS:
        el = ELEMENT[key]
        note = f"本色{el}护甲 ×2（热护甲）" if key in ("Red", "White") else f"穿着它做的衣甲时，受到的{el}伤害 −50%"
        o += ['  <ThingDef ParentName="ResourceBase">', f'    <defName>LP_DragonScale_{key}</defName>', f'    <label>{zh}龙鳞</label>',
              f'    <description>从{zh}龙尸体上剥下的鳞片，比钢还硬，又轻得多。可以代替金属做板甲、盾、头盔这类可选材料的装备：利器护甲 ×1.6（相对钢），{note}。</description>',
              f'    <graphicData><texPath>ChromaticDragons/Items/Scale_{key}</texPath><graphicClass>Graphic_Single</graphicClass></graphicData>',
              '    <soundInteract>Metal_Drop</soundInteract>', '    <soundDrop>Metal_Drop</soundDrop>', '    <stackLimit>150</stackLimit>', '    <useHitPoints>false</useHitPoints>',
              '    <statBases>', '      <MarketValue>20</MarketValue>', '      <Mass>0.35</Mass>', '      <StuffPower_Armor_Sharp>1.44</StuffPower_Armor_Sharp>',
              '      <StuffPower_Armor_Blunt>0.45</StuffPower_Armor_Blunt>', f'      <StuffPower_Armor_Heat>{0.6 * heat:.2f}</StuffPower_Armor_Heat>',
              f'      <StuffPower_Insulation_Cold>{ins[0]}</StuffPower_Insulation_Cold>', f'      <StuffPower_Insulation_Heat>{ins[1]}</StuffPower_Insulation_Heat>',
              '      <SharpDamageMultiplier>1</SharpDamageMultiplier>', '      <BluntDamageMultiplier>1</BluntDamageMultiplier>', '    </statBases>',
              '    <thingCategories><li>ResourcesRaw</li></thingCategories>', '    <burnableByRecipe>false</burnableByRecipe>', '    <smeltable>false</smeltable>',
              '    <stuffProps>', '      <categories><li>Metallic</li></categories>', '      <appearance>Metal</appearance>', '      <commonality>0</commonality>',
              f'      <color>({col[0]},{col[1]},{col[2]})</color>', '      <soundImpactBullet>BulletImpact_Metal</soundImpactBullet>', '      <soundMeleeHitSharp>MeleeHit_Metal_Sharp</soundMeleeHitSharp>',
              '      <soundMeleeHitBlunt>MeleeHit_Metal_Blunt</soundMeleeHitBlunt>', '      <soundImpactMelee>Pawn_Melee_Punch_HitBuilding_Metal</soundImpactMelee>',
              '      <statFactors><MaxHitPoints>1.5</MaxHitPoints><Beauty>2</Beauty><Flammability>0.2</Flammability></statFactors>', '    </stuffProps>',
              '    <tradeTags><li>LP_DragonLoot</li></tradeTags>', '  </ThingDef>']
    # 龙牙
    o += ['  <ThingDef ParentName="ResourceBase">', '    <defName>LP_DragonFang</defName>', '    <label>龙牙</label>',
          '    <description>成年以上真龙的牙，又长又利。可以代替金属做刀剑：锋利 ×1.4。</description>',
          '    <graphicData><texPath>ChromaticDragons/Items/Fang</texPath><graphicClass>Graphic_Single</graphicClass></graphicData>',
          '    <stackLimit>50</stackLimit>', '    <useHitPoints>false</useHitPoints>',
          '    <statBases><MarketValue>30</MarketValue><Mass>0.5</Mass><StuffPower_Armor_Sharp>0.6</StuffPower_Armor_Sharp><StuffPower_Armor_Blunt>0.3</StuffPower_Armor_Blunt><StuffPower_Armor_Heat>0.3</StuffPower_Armor_Heat><SharpDamageMultiplier>1.4</SharpDamageMultiplier><BluntDamageMultiplier>0.8</BluntDamageMultiplier></statBases>',
          '    <thingCategories><li>ResourcesRaw</li></thingCategories>', '    <smeltable>false</smeltable>',
          '    <stuffProps><categories><li>Metallic</li></categories><appearance>Metal</appearance><commonality>0</commonality><color>(232,222,196)</color><statFactors><MaxHitPoints>1.2</MaxHitPoints><Beauty>1.5</Beauty><Flammability>0.3</Flammability></statFactors></stuffProps>',
          '    <tradeTags><li>LP_DragonLoot</li></tradeTags>', '  </ThingDef>', '']
    # 真龙之心、龙之血
    for key, zh, col, imm, extra, heat, ins in COLORS:
        el = ELEMENT[key]
        o += ['  <ThingDef ParentName="ResourceBase">', f'    <defName>LP_DragonHeart_{key}</defName>', f'    <label>{zh}龙之心</label>',
              f'    <description>成年以上{zh}龙的心脏，摸上去还是温的。可以在炼金桌上炼成三瓶{zh}龙之血，也能卖个好价钱。</description>',
              f'    <graphicData><texPath>ChromaticDragons/Items/Heart_{key}</texPath><graphicClass>Graphic_Single</graphicClass></graphicData>',
              '    <stackLimit>5</stackLimit>', '    <statBases><MarketValue>5000</MarketValue><Mass>3</Mass><MaxHitPoints>120</MaxHitPoints><Flammability>0.5</Flammability></statBases>',
              '    <thingCategories><li>ResourcesRaw</li></thingCategories>', '    <tradeTags><li>LP_DragonLoot</li></tradeTags>', '  </ThingDef>',
              '  <ThingDef ParentName="LP_PotionBase">', f'    <defName>LP_DragonBlood_{key}</defName>', f'    <label>{zh}龙之血</label>',
              f'    <description>用{zh}龙之心炼成的药剂。7 天内受到的{el}伤害 −50%，生命 +5%。</description>',
              f'    <descriptionHyperlinks><HediffDef>LP_DragonBlood_{key}</HediffDef></descriptionHyperlinks>',
              f'    <graphicData><texPath>ChromaticDragons/Items/Blood_{key}</texPath><graphicClass>Graphic_Single</graphicClass></graphicData>',
              '    <statBases><MarketValue>1500</MarketValue></statBases>',
              f'    <ingestible><outcomeDoers><li Class="LichPhylactery.IngestionOutcomeDoer_LPPotion"><hediff>LP_DragonBlood_{key}</hediff></li></outcomeDoers></ingestible>',
              f'    <comps><li Class="CompProperties_Drug"><listOrder>{2260 + COLORS.index((key, zh, col, imm, extra, heat, ins))}</listOrder></li></comps>', '  </ThingDef>',
              '  <HediffDef ParentName="LP_PotionHediffBase">', f'    <defName>LP_DragonBlood_{key}</defName>', f'    <label>{zh}龙之血</label>',
              f'    <description>{zh}龙的血在血管里流淌：受到的{el}伤害 −50%，生命 +5%。</description>',
              '    <comps><li Class="HediffCompProperties_Disappears"><disappearsAfterTicks>420000</disappearsAfterTicks><showRemainingTime>true</showRemainingTime></li></comps>',
              '  </HediffDef>',
              '  <RecipeDef ParentName="LP_PotionRecipeBase">', f'    <defName>LP_BrewDragonBlood_{key}</defName>', f'    <label>炼制{zh}龙之血</label>',
              f'    <description>用 1 颗{zh}龙之心炼成 3 瓶{zh}龙之血。</description>', f'    <jobString>正在炼制{zh}龙之血。</jobString>',
              f'    <ingredients><li><filter><thingDefs><li>LP_DragonHeart_{key}</li></thingDefs></filter><count>1</count></li></ingredients>',
              f'    <fixedIngredientFilter><thingDefs><li>LP_DragonHeart_{key}</li></thingDefs></fixedIngredientFilter>',
              f'    <products><LP_DragonBlood_{key}>3</LP_DragonBlood_{key}></products>', '  </RecipeDef>']
    # 龙巢情报
    o += ['', '  <ThingDef ParentName="ResourceBase">', '    <defName>LP_DragonLairMap</defName>', '    <label>龙巢情报</label>',
          '    <description>同盟的探子画的一张兽皮地图，标着一座远古龙巢的位置。读懂它，龙巢就会显露在世界地图上（30 天后消失）。</description>',
          '    <graphicData><texPath>ChromaticDragons/Items/LairMap</texPath><graphicClass>Graphic_Single</graphicClass></graphicData>',
          '    <stackLimit>1</stackLimit>', '    <thingCategories><li>Items</li></thingCategories>',
          '    <statBases><MarketValue>3000</MarketValue><Mass>0.2</Mass><MaxHitPoints>40</MaxHitPoints><Flammability>1</Flammability></statBases>',
          '    <tradeTags><li>LP_DragonLairMap</li></tradeTags>',
          '    <comps>', '      <li Class="CompProperties_Usable"><useJob>UseItem</useJob><useLabel>读{0_label}</useLabel><useDuration>300</useDuration></li>',
          '      <li Class="CompProperties_UseEffect"><compClass>LichPhylactery.CompUseEffect_DragonLairMap</compClass></li>', '      <li Class="CompProperties_UseEffectDestroySelf" />', '    </comps>',
          '  </ThingDef>', '', '</Defs>', '']
    w(os.path.join(ROOT, "Defs", "ChromaticDragons_Items.xml"), "\n".join(o))


def race():
    o = ['<?xml version="1.0" encoding="utf-8"?>',
         '<!-- 1.52.0 五色真龙（Humanoid Alien Races 可选联动，只在启用 erdelf.HumanoidAlienRaces 时加载；由 ArtSource/ChromaticDragons-gen_defs.py 生成）。',
         '     人形种族（ParentName="Human"，HAR 列表 Inherit="False"）、自带龙形身体 LP_DragonBody（ChromaticDragons_Body.xml）；年龄档 = 生命阶段（体型、生命、近战、护甲、移速）。',
         '     身体贴图是整条龙（含头）、按颜色预先上色，头型贴图是透明的；skinShader Cutout 不染肤色。规则代码在主程序集 Source/ChromaticDragons.cs、Source/Legendary.cs。 -->',
         '<Defs>', '',
         '  <AlienRace.ThingDef_AlienRace ParentName="Human" Name="LP_ChromaticDragonBase" Abstract="True">',
         '    <statBases>', '      <MarketValue>8000</MarketValue>', '      <MoveSpeed>4.6</MoveSpeed>', '      <PsychicSensitivity>0.8</PsychicSensitivity>', '    </statBases>',
         '    <race>', '      <body>LP_DragonBody</body>', '      <baseBodySize>1</baseBodySize>', '      <baseHealthScale>1</baseHealthScale>', '      <lifeExpectancy>3000</lifeExpectancy>',
         '      <lifeStageAges Inherit="False">', '        <li><def>LP_DragonYoung</def><minAge>0</minAge></li>', '        <li><def>LP_DragonAdult</def><minAge>100</minAge></li>', '        <li><def>LP_DragonAncient</def><minAge>800</minAge></li>', '      </lifeStageAges>',
         '      <ageGenerationCurve Inherit="False"><points><li>(30,0)</li><li>(60,1)</li><li>(400,1)</li><li>(1500,0.1)</li><li>(1600,0)</li></points></ageGenerationCurve>',
         '      <hediffGiverSets Inherit="False" />', '    </race>',
         '    <tools Inherit="False">',
         '      <li><label>咬</label><capacities><li>Bite</li></capacities><power>28</power><cooldownTime>2.2</cooldownTime><armorPenetration>0.45</armorPenetration><linkedBodyPartsGroup>Teeth</linkedBodyPartsGroup></li>',
         '      <li><label>左前爪</label><capacities><li>Scratch</li></capacities><power>18</power><cooldownTime>1.6</cooldownTime><armorPenetration>0.3</armorPenetration><linkedBodyPartsGroup>FrontLeftPaw</linkedBodyPartsGroup></li>',
         '      <li><label>右前爪</label><capacities><li>Scratch</li></capacities><power>18</power><cooldownTime>1.6</cooldownTime><armorPenetration>0.3</armorPenetration><linkedBodyPartsGroup>FrontRightPaw</linkedBodyPartsGroup></li>',
         '      <li><label>尾巴</label><capacities><li>Blunt</li></capacities><power>22</power><cooldownTime>2.4</cooldownTime><armorPenetration>0.25</armorPenetration><linkedBodyPartsGroup>LP_DragonTail</linkedBodyPartsGroup></li>',
         '    </tools>',
         '    <modExtensions>',
         '      <li Class="LichPhylactery.LP_AdventurerAffinityExt"><legendary>true</legendary></li>',
         '    </modExtensions>',
         '  </AlienRace.ThingDef_AlienRace>', '']
    for key, zh, col, *_ in COLORS:
        o += ['  <AlienRace.ThingDef_AlienRace ParentName="LP_ChromaticDragonBase">', f'    <defName>LP_{key}Dragon</defName>', f'    <label>{zh}龙</label>',
              f'    <description>{DESC[key]}\n\n真龙有智慧，会说话，活上千年。它们有传奇抗性、传奇动作和骇人威仪，倒下就会死去，不会投降。</description>',
              '    <modExtensions Inherit="True">', f'      <li Class="LichPhylactery.LP_DragonExtension"><color>{key}</color></li>', '    </modExtensions>',
              '    <alienRace>', '      <generalSettings>', '        <humanRecipeImport>false</humanRecipeImport>', '        <allowHumanBios>false</allowHumanBios>',
              '        <corpseCategory>CorpsesHumanlike</corpseCategory>',
              '        <alienPartGenerator>', f'          <headTypes Inherit="False"><li>LP_DragonHead_{key}</li></headTypes>', '          <bodyTypes Inherit="False"><li>Hulk</li></bodyTypes>',
              '          <colorChannels Inherit="False"><li><name>skin</name><first Class="ColorGenerator_Single"><color>(1,1,1,1)</color></first></li></colorChannels>',
              '          <bodyAddons Inherit="False" />', '        </alienPartGenerator>', '      </generalSettings>',
              '      <graphicPaths>', f'        <body>ChromaticDragons/{key}/</body>', '        <head><path>ChromaticDragons/Heads/</path></head>', '        <skinShader>Cutout</skinShader>', '      </graphicPaths>',
              '      <raceRestriction>', '        <onlyUseRaceRestrictedApparel>true</onlyUseRaceRestrictedApparel>', '        <onlyUseRaceRestrictedWeapons>true</onlyUseRaceRestrictedWeapons>', '      </raceRestriction>',
              '    </alienRace>', '  </AlienRace.ThingDef_AlienRace>',
              '  <HeadTypeDef>', f'    <defName>LP_DragonHead_{key}</defName>', f'    <graphicPath>ChromaticDragons/Heads/{key}</graphicPath>', '    <gender>None</gender>', '    <randomChosen>false</randomChosen>', '  </HeadTypeDef>', '']
    # 生命阶段
    for (sk, sz, cp, lv, res, act, pres, ms, lp), size, hs, md, sharp, blunt, spd in zip(STAGES, (2.5, 4.0, 6.0), (6, 24, 40), (1.0, 1.6, 2.5), (0.4, 0.55, 0.7), (0.3, 0.4, 0.55), (0, 0.2, 0.4)):
        o += ['  <LifeStageDef>', f'    <defName>LP_Dragon{sk}</defName>', f'    <label>{sz}龙</label>', f'    <adjective>{sz}</adjective>',
              '    <developmentalStage>Adult</developmentalStage>', '    <reproductive>false</reproductive>',
              f'    <bodySizeFactor>{size}</bodySizeFactor>', f'    <healthScaleFactor>{hs}</healthScaleFactor>', f'    <meleeDamageFactor>{md}</meleeDamageFactor>',
              f'    <statOffsets><ArmorRating_Sharp>{sharp}</ArmorRating_Sharp><ArmorRating_Blunt>{blunt}</ArmorRating_Blunt><MoveSpeed>{spd}</MoveSpeed></statOffsets>',
              '    <voxPitch>0.5</voxPitch>', '  </LifeStageDef>']
    o += ['', '  <!-- 兵种：每色三档（青年 / 成年 / 远古）。传奇扩展：固定等级、固定六维（5e 怪物书）、CR、传奇抗性 / 动作、骇人威仪半径、击杀奖励。 -->',
          '  <PawnKindDef Name="LP_ChromaticDragonKindBase" Abstract="True">', '    <defaultFactionDef>LP_ChromaticDragons</defaultFactionDef>',
          '    <isFighter>true</isFighter>', '    <canBeSapper>false</canBeSapper>', '    <apparelMoney>0</apparelMoney>', '    <weaponMoney>0</weaponMoney>',
          '    <apparelAllowHeadgearChance>0</apparelAllowHeadgearChance>', '    <techHediffsChance>0</techHediffsChance>', '    <chemicalAddictionChance>0</chemicalAddictionChance>',
          '    <combatEnhancingDrugsChance>0</combatEnhancingDrugsChance>', '    <useFactionXenotypes>false</useFactionXenotypes>',
          '    <backstoryFiltersOverride><li><categories><li>LP_DragonBio</li></categories></li></backstoryFiltersOverride>',
          '    <initialResistanceRange>99~99</initialResistanceRange>', '    <minGenerationAge>30</minGenerationAge>', '  </PawnKindDef>']
    for key, zh, *_ in COLORS:
        for i, (sk, sz, cp, lv, res, act, pres, ms, lp) in enumerate(STAGES):
            acts = []
            if act:
                acts = ['<li><action>Detect</action><cost>1</cost></li>', '<li><action>Wing</action><cost>2</cost></li>', '<li><action>Tail</action><cost>1</cost></li>']
                if i == 2:
                    acts.insert(0, '<li><action>Glance</action><cost>1</cost><cooldown>600</cooldown></li>')
            sc = "".join(f"<li>{v}</li>" for v in SCORES[key][i])
            o += ['  <PawnKindDef ParentName="LP_ChromaticDragonKindBase">', f'    <defName>LP_{key}Dragon_{sk}</defName>', f'    <label>{sz}{zh}龙</label>',
                  f'    <race>LP_{key}Dragon</race>', f'    <combatPower>{cp}</combatPower>',
                  '    <modExtensions>', '      <li Class="LichPhylactery.LP_LegendaryExtension">', f'        <title>{sz}{zh}龙</title>',
                  f'        <level>{lv}</level>', f'        <scores>{sc}</scores>', f'        <cr>{CR[key][i]}</cr>',
                  f'        <resistance>{res}</resistance>', f'        <actionPoints>{act}</actionPoints>', f'        <actions>{"".join(acts)}</actions>',
                  f'        <presenceRadius>{pres}</presenceRadius>', f'        <milestone>{"true" if ms else "false"}</milestone>', f'        <legendPoints>{lp}</legendPoints>',
                  '      </li>', '    </modExtensions>', '  </PawnKindDef>']
    o += ['', '</Defs>', '']
    w(os.path.join(ROOT, "Bridges", "HAR", "Defs", "ChromaticDragons_Race.xml"), "\n".join(o))


items()
race()
print("ok")
