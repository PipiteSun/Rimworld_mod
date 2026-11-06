# 1.112.0 DND 植入物：按下面的表生成 Defs/Implants_DND.xml（物品、植入状态、安装 / 摘除手术、制作配方、研究）。
# 用法（模组根目录）：E:\Python\python.exe -I ArtSource\Implants\gen_implants.py .
import os, sys
from xml.sax.saxutils import escape as esc

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'

# 部件（替换整个部位）：defName 后缀、名称、部位、效率、植入物位、是否调谐、配方台、研究、材料、额外阶段、说明
PARTS = [
    ('InfusedArm', '注能义臂', 'Shoulder', 1.15, 'Infused', False, 'LP_RuneAnvil', 'LP_Research_InfusedProsthetics', {'Steel': 50, 'LP_Mithral': 5}, '', 700,
     '器械师用钢和少量秘银锻成、再灌注魔力的义臂。替换整条手臂，部位效率 115%。普通档，不占调谐位。'),
    ('InfusedLeg', '注能义腿', 'Leg', 1.15, 'Infused', False, 'LP_RuneAnvil', 'LP_Research_InfusedProsthetics', {'Steel': 50, 'LP_Mithral': 5}, '', 700,
     '器械师用钢和少量秘银锻成、再灌注魔力的义腿。替换整条腿，部位效率 115%。普通档，不占调谐位。'),
    ('ColdIronArm', '寒铁义臂', 'Shoulder', 1.25, 'ColdArm', False, 'LP_RuneAnvil', 'LP_Research_InfusedProsthetics', {'LP_ColdIron': 40, 'LP_Mithral': 5}, '', 1300,
     '淬炼寒铁锻成的义臂，摸上去总是冰凉。替换整条手臂，部位效率 125%；近战打寒铁克制的目标（妖精血统、精灵、灵能者）和邪魔时伤害 +30%。普通档，不占调谐位。'),
    ('MithralLeg', '秘银义腿', 'Leg', 1.30, 'MithralLeg', True, 'LP_RuneAnvil', 'LP_Research_InfusedProsthetics', {'LP_Mithral': 40}, '', 2600,
     '轻得几乎没有重量的秘银义腿。替换整条腿，部位效率 130%；穿着的衣物带来的移速惩罚减半（重甲最明显）。稀有档，占 1 个调谐位。'),
    ('AdamantArm', '精金义臂', 'Shoulder', 1.40, 'AdamArm', True, 'LP_RuneAnvil', 'LP_Research_InfusedProsthetics', {'LP_Adamantine': 40, 'LP_Mithral': 10}, '', 3600,
     '精金锻成的义臂，几乎砍不坏。替换整条手臂，部位效率 140%；这条手臂上受的伤 −50%，近战护甲穿透 +25%。稀有档，占 1 个调谐位。'),
    ('ClockEye', '精工机关眼', 'Eye', 1.25, 'ClockEye', False, 'LP_RuneAnvil', 'LP_Research_InfusedProsthetics', {'LP_Mithral': 10, 'Jade': 10}, '<stages><li><statFactors><ShootingAccuracyPawn>1.05</ShootingAccuracyPawn></statFactors></li></stages>', 1400,
     '秘银齿轮和玉石镜片组成的机关眼。替换一只眼睛，部位效率 125%；射击精度 +5%，光线昏暗时不受惩罚。普通档，不占调谐位。'),
    ('MithralHeart', '秘银心核', 'Heart', 1.30, 'MithralHeart', True, 'LP_RuneAnvil', 'LP_Research_InfusedProsthetics', {'LP_Mithral': 30, 'LP_Adamantine': 10}, '<stages><li><statOffsets><ToxicResistance>0.5</ToxicResistance></statOffsets></li></stages>', 3200,
     '秘银和精金打成的心核，跳得又稳又有力。替换心脏，部位效率 130%；毒素积累 −50%，不会再犯心脏病。稀有档，占 1 个调谐位。'),
]
# 伊奥恩颅石（镶在额骨上，每颗占 1 个调谐位，最多 3 颗）：后缀、名称、植入物位、额外阶段、说明
STONES = [
    ('Str', '力量之石', 'Str', '', '冒险者力量 +2（最多加到 20；力量超凡时不受限）。'),
    ('Dex', '敏捷之石', 'Dex', '', '冒险者敏捷 +2（最多加到 20；敏捷超凡时不受限）。'),
    ('Con', '体质之石', 'Con', '', '冒险者体质 +2（最多加到 20；体质超凡时不受限）。'),
    ('Int', '智力之石', 'Int', '', '冒险者智力 +2（最多加到 20；智力超凡时不受限）。'),
    ('Wis', '感知之石', 'Wis', '', '冒险者感知 +2（最多加到 20；感知超凡时不受限）。'),
    ('Cha', '魅力之石', 'Cha', '', '冒险者魅力 +2（最多加到 20；魅力超凡时不受限）。'),
    ('Sustain', '维生之石', 'Sustain', '<stages><li><hungerRateFactor>0.2</hungerRateFactor></li></stages>', '饥饿速度 ×0.2。'),
    ('Regen', '再生之石', 'Regen', '', '每天额外治好一处伤口（最重的那处，旧伤疤不算）。'),
    ('Mastery', '精通之石', 'Mastery', '', '熟练加值 +1（职业强度、检定都按新的熟练加值算）。'),
    ('Absorb', '吸收之石', 'Absorb', '', '每天抵消一次灵能或法术伤害（不带武器的火焰、寒冰、闪电、心灵、奥术力伤害）。'),
    ('Alert', '警觉之石', 'Alert', '<stages><li><capMods><li><capacity>Consciousness</capacity><offset>0.05</offset></li></capMods></li></stages>', '不会被偷袭（刺客、隐匿伏击的偷袭加成对你无效），意识 +5%。'),
]

def ing(d):
    lis = ''.join('<li><filter><thingDefs><li>%s</li></thingDefs></filter><count>%d</count></li>' % (k, v) for k, v in d.items())
    fix = ''.join('<li>%s</li>' % k for k in d)
    return '<ingredients>%s</ingredients><fixedIngredientFilter><thingDefs>%s</thingDefs></fixedIngredientFilter>' % (lis, fix)

out = ['<?xml version="1.0" encoding="utf-8"?>', '<!-- 1.112.0 DND 植入物（ArtSource/Implants/gen_implants.py 生成，不要手改）：器械师义肢、伊奥恩颅石、调谐、器械师注能。代码在 Source/DndImplants.cs。 -->', '<Defs>']
out.append('''  <ResearchProjectDef>
    <defName>LP_Research_InfusedProsthetics</defName>
    <label>注能义肢</label>
    <description>把魔像学的灌注技术用在血肉之躯上：在符文铁砧锻造注能义肢、寒铁 / 秘银 / 精金义肢、机关眼和秘银心核；器械师还能给原版仿生和考古科技部件注能（效率 +10%）。</description>
    <baseCost>2500</baseCost>
    <techLevel>Medieval</techLevel>
    <prerequisites><li>LP_Research_GolemManual</li></prerequisites>
    <tab>LP_FireGiantTech</tab>
    <researchViewX>6</researchViewX>
    <researchViewY>0.7</researchViewY>
  </ResearchProjectDef>''')
for key, label, part, eff, flag, att, bench, res, mats, stage, value, desc in PARTS:
    d = 'LP_Imp_' + key; dd = esc(desc + ('器械师主刀安装成功率 +15%；器械师自己装上后每 4 级效率再 +3%（最多 +15%）。'))
    out.append('''  <ThingDef ParentName="BodyPartBase">
    <defName>%s</defName><label>%s</label><description>%s</description>
    <techLevel>Medieval</techLevel>
    <thingCategories><li>BodyPartsBionic</li></thingCategories>
    <graphicData><texPath>Items/Implants/%s</texPath><graphicClass>Graphic_Single</graphicClass><drawSize>0.8</drawSize></graphicData>
    <statBases><MarketValue>%d</MarketValue><Mass>1.5</Mass></statBases>
  </ThingDef>
  <HediffDef ParentName="AddedBodyPartBase">
    <defName>%s</defName><label>%s</label><description>%s</description>
    <spawnThingOnRemoved>%s</spawnThingOnRemoved>
    <addedPartProps><solid>true</solid><partEfficiency>%.2f</partEfficiency><betterThanNatural>true</betterThanNatural></addedPartProps>
    %s
    <comps><li Class="LichPhylactery.HediffCompProperties_DndImplant" /></comps>
    <modExtensions><li Class="LichPhylactery.ImplantExtension"><flag>%s</flag><attune>%s</attune></li></modExtensions>
  </HediffDef>
  <RecipeDef ParentName="SurgeryInstallBodyPartArtificialBase">
    <defName>LP_Install_%s</defName><label>安装%s</label><description>%s</description><jobString>正在安装%s。</jobString>
    <workerClass>LichPhylactery.Recipe_InstallDndPart</workerClass>
    <ingredients><li><filter><thingDefs><li>%s</li></thingDefs></filter><count>1</count></li></ingredients>
    <fixedIngredientFilter><thingDefs><li>%s</li></thingDefs></fixedIngredientFilter>
    <appliedOnFixedBodyParts><li>%s</li></appliedOnFixedBodyParts>
    <addsHediff>%s</addsHediff>
  </RecipeDef>
  <RecipeDef>
    <defName>LP_Make_%s</defName><label>锻造%s</label><description>在符文铁砧锻造%s。</description><jobString>正在锻造%s。</jobString>
    <workAmount>12000</workAmount><workSpeedStat>GeneralLaborSpeed</workSpeedStat><workSkill>Crafting</workSkill>
    <effectWorking>Smith</effectWorking><soundWorking>Recipe_Smith</soundWorking><unfinishedThingDef>UnfinishedComponent</unfinishedThingDef>
    <researchPrerequisite>%s</researchPrerequisite><skillRequirements><Crafting>8</Crafting></skillRequirements>
    <recipeUsers><li>%s</li></recipeUsers>
    %s
    <products><%s>1</%s></products>
  </RecipeDef>''' % (d, label, dd, key, value, d, label, dd, d, eff, stage, flag, str(att).lower(),
                     key, label, dd, label, d, d, part, d,
                     key, label, label, label, res, bench, ing(mats), d, d))
# 原石与颅石
out.append('''  <ThingDef ParentName="BodyPartBase">
    <defName>LP_IounBlank</defName><label>伊奥恩原石</label>
    <description>还没刻纹的伊奥恩石。在附魔台用 5 秘银刻成任意一种伊奥恩颅石；迷锁地牢和探索公告板的魔法奖励里偶尔能拿到，也能在附魔台做（需要研究“神奇器物”）。</description>
    <techLevel>Medieval</techLevel>
    <thingCategories><li>BodyPartsBionic</li></thingCategories>
    <graphicData><texPath>Items/Implants/IounBlank</texPath><graphicClass>Graphic_Single</graphicClass><drawSize>0.7</drawSize></graphicData>
    <statBases><MarketValue>1200</MarketValue><Mass>0.2</Mass></statBases>
  </ThingDef>
  <RecipeDef>
    <defName>LP_Make_IounBlank</defName><label>制作伊奥恩原石</label><description>用 20 秘银、15 玉石和 10 金子在附魔台做一颗伊奥恩原石。</description><jobString>正在制作伊奥恩原石。</jobString>
    <workAmount>10000</workAmount><workSpeedStat>GeneralLaborSpeed</workSpeedStat><workSkill>Crafting</workSkill>
    <effectWorking>Smith</effectWorking><soundWorking>Recipe_Smith</soundWorking><unfinishedThingDef>UnfinishedComponent</unfinishedThingDef>
    <researchPrerequisite>LP_Research_WondrousDevices</researchPrerequisite><skillRequirements><Crafting>8</Crafting></skillRequirements>
    <recipeUsers><li>LP_EnchantingTable</li></recipeUsers>
    %s
    <products><LP_IounBlank>1</LP_IounBlank></products>
  </RecipeDef>''' % ing({'LP_Mithral': 20, 'Jade': 15, 'Gold': 10}))
for key, label, flag, stage, eff in STONES:
    d = 'LP_Ioun_' + key; full = '伊奥恩颅石·' + label
    desc = esc('原作里绕着头飞的伊奥恩石，这里镶进额骨。' + eff + '每颗占 1 个调谐位，额骨上最多镶 3 颗；摘除能拿回石头。')
    out.append('''  <ThingDef ParentName="BodyPartBase">
    <defName>%s</defName><label>%s</label><description>%s</description>
    <techLevel>Medieval</techLevel>
    <thingCategories><li>BodyPartsBionic</li></thingCategories>
    <graphicData><texPath>Items/Implants/Ioun%s</texPath><graphicClass>Graphic_Single</graphicClass><drawSize>0.7</drawSize></graphicData>
    <statBases><MarketValue>2500</MarketValue><Mass>0.2</Mass></statBases>
  </ThingDef>
  <HediffDef ParentName="ImplantHediffBase">
    <defName>%s</defName><label>%s</label><description>%s</description>
    <spawnThingOnRemoved>%s</spawnThingOnRemoved>
    %s
    <comps><li Class="LichPhylactery.HediffCompProperties_DndImplant" /></comps>
    <modExtensions><li Class="LichPhylactery.ImplantExtension"><flag>%s</flag><attune>true</attune><ionStone>true</ionStone></li></modExtensions>
  </HediffDef>
  <RecipeDef ParentName="SurgeryInstallImplantBase">
    <defName>LP_Install_%s</defName><label>镶嵌%s</label><description>%s</description><jobString>正在镶嵌%s。</jobString>
    <workerClass>LichPhylactery.Recipe_InstallDndImplant</workerClass>
    <ingredients><li><filter><thingDefs><li>%s</li></thingDefs></filter><count>1</count></li></ingredients>
    <fixedIngredientFilter><thingDefs><li>%s</li></thingDefs></fixedIngredientFilter>
    <appliedOnFixedBodyParts><li>Skull</li></appliedOnFixedBodyParts>
    <addsHediff>%s</addsHediff>
  </RecipeDef>
  <RecipeDef ParentName="SurgeryRemoveImplantBase">
    <defName>LP_Remove_%s</defName><label>摘除%s</label><description>从额骨里取出%s，石头还能再用。</description><jobString>正在摘除%s。</jobString>
    <appliedOnFixedBodyParts><li>Skull</li></appliedOnFixedBodyParts>
    <removesHediff>%s</removesHediff>
  </RecipeDef>
  <RecipeDef>
    <defName>LP_Make_%s</defName><label>刻纹：%s</label><description>用 1 颗伊奥恩原石和 5 秘银刻成%s。</description><jobString>正在刻纹%s。</jobString>
    <workAmount>3000</workAmount><workSpeedStat>GeneralLaborSpeed</workSpeedStat><workSkill>Crafting</workSkill>
    <effectWorking>Smith</effectWorking><soundWorking>Recipe_Smith</soundWorking>
    <researchPrerequisite>LP_Research_WondrousDevices</researchPrerequisite><skillRequirements><Crafting>6</Crafting></skillRequirements>
    <recipeUsers><li>LP_EnchantingTable</li></recipeUsers>
    %s
    <products><%s>1</%s></products>
  </RecipeDef>''' % (d, full, desc, key, d, full, desc, d, stage, flag,
                     d, full, desc, full, d, d, d,
                     d, full, full, full, d,
                     d, label, full, full, ing({'LP_IounBlank': 1, 'LP_Mithral': 5}), d, d))
# 注能、器械师调校
out.append('''  <HediffDef>
    <defName>LP_Infused</defName>
    <hediffClass>LichPhylactery.Hediff_CapBonus</hediffClass>
    <label>注能</label>
    <description>器械师给这件义体灌注了魔力：效率 +10%（成对的部位按一半折算到对应能力）。义体拆掉就消失。</description>
    <isBad>false</isBad><everCurableByItem>false</everCurableByItem><scenarioCanAdd>false</scenarioCanAdd><defaultLabelColor>(0.6,0.75,1)</defaultLabelColor>
  </HediffDef>
  <HediffDef>
    <defName>LP_ArtificerTuned</defName>
    <hediffClass>LichPhylactery.Hediff_CapBonus</hediffClass>
    <label>器械师调校</label>
    <description>器械师亲手调校自己身上的 DND 义肢：每 4 个器械师等级效率 +3%（最多 +15%，成对的部位按一半折算到对应能力）。</description>
    <isBad>false</isBad><everCurableByItem>false</everCurableByItem><scenarioCanAdd>false</scenarioCanAdd><defaultLabelColor>(0.95,0.8,0.45)</defaultLabelColor>
  </HediffDef>
  <RecipeDef ParentName="SurgeryFlesh">
    <defName>LP_InfuseBionic</defName>
    <label>注能义体</label>
    <description>器械师用 10 秘银给一件原版仿生或考古科技部件注能：效率 +10%。只有器械师能主刀（成功率 +15%）。</description>
    <jobString>正在给义体注能。</jobString>
    <workerClass>LichPhylactery.Recipe_InfuseBionic</workerClass>
    <workAmount>2500</workAmount>
    <researchPrerequisite>LP_Research_InfusedProsthetics</researchPrerequisite>
    <skillRequirements><Medicine>5</Medicine></skillRequirements>
    <recipeUsers><li>Human</li></recipeUsers>
    <ingredients><li><filter><categories><li>Medicine</li></categories></filter><count>1</count></li><li><filter><thingDefs><li>LP_Mithral</li></thingDefs></filter><count>10</count></li></ingredients>
    <fixedIngredientFilter><categories><li>Medicine</li></categories><thingDefs><li>LP_Mithral</li></thingDefs></fixedIngredientFilter>
    <modExtensions><li Class="LichPhylactery.ArtificerSurgeryExtension" /></modExtensions>
  </RecipeDef>''')
# ==================== 1.113.0 巫妖血肉扩充（E）与异怪嫁接（B） ====================
def hediff(d, label, desc, kind, part, eff, flag, attune=False, stage='', extra_comps='', ext_extra='', item=None):
    comps = '<comps><li Class="LichPhylactery.HediffCompProperties_DndImplant" />%s</comps>' % extra_comps
    ext = '<modExtensions><li Class="LichPhylactery.ImplantExtension"><flag>%s</flag><attune>%s</attune>%s</li></modExtensions>' % (flag, str(attune).lower(), ext_extra)
    if kind == 'part':
        return '''  <HediffDef ParentName="AddedBodyPartBase">
    <defName>%s</defName><label>%s</label><description>%s</description>
    <spawnThingOnRemoved>%s</spawnThingOnRemoved>
    <addedPartProps><solid>false</solid><partEfficiency>%.2f</partEfficiency><betterThanNatural>true</betterThanNatural></addedPartProps>
    %s %s %s
  </HediffDef>''' % (d, label, desc, item or d, eff, stage, comps, ext)
    return '''  <HediffDef ParentName="ImplantHediffBase">
    <defName>%s</defName><label>%s</label><description>%s</description>
    <spawnThingOnRemoved>%s</spawnThingOnRemoved>
    %s %s %s
  </HediffDef>''' % (d, label, desc, item or d, stage, comps, ext)
def thing(d, label, desc, tex, value, cat='BodyPartsBionic'):
    return '''  <ThingDef ParentName="BodyPartBase">
    <defName>%s</defName><label>%s</label><description>%s</description>
    <techLevel>Medieval</techLevel>
    <thingCategories><li>%s</li></thingCategories>
    <graphicData><texPath>Items/Implants/%s</texPath><graphicClass>Graphic_Single</graphicClass><drawSize>0.8</drawSize></graphicData>
    <statBases><MarketValue>%d</MarketValue><Mass>1</Mass></statBases>
  </ThingDef>''' % (d, label, desc, cat, tex, value)
def install(d, label, desc, kind, part):
    base = 'SurgeryInstallBodyPartArtificialBase' if kind == 'part' else 'SurgeryInstallImplantBase'
    worker = 'Recipe_InstallDndPart' if kind == 'part' else 'Recipe_InstallDndImplant'
    s = '''  <RecipeDef ParentName="%s">
    <defName>LP_Install_%s</defName><label>安装%s</label><description>%s</description><jobString>正在安装%s。</jobString>
    <workerClass>LichPhylactery.%s</workerClass>
    <ingredients><li><filter><thingDefs><li>%s</li></thingDefs></filter><count>1</count></li></ingredients>
    <fixedIngredientFilter><thingDefs><li>%s</li></thingDefs></fixedIngredientFilter>
    <appliedOnFixedBodyParts><li>%s</li></appliedOnFixedBodyParts>
    <addsHediff>%s</addsHediff>
  </RecipeDef>''' % (base, d, label, desc, label, worker, d, d, part, d)
    if kind != 'part':
        s += '''
  <RecipeDef ParentName="SurgeryRemoveImplantBase">
    <defName>LP_Remove_%s</defName><label>摘除%s</label><description>取出%s。</description><jobString>正在摘除%s。</jobString>
    <appliedOnFixedBodyParts><li>%s</li></appliedOnFixedBodyParts>
    <removesHediff>%s</removesHediff>
  </RecipeDef>''' % (d, label, label, label, part, d)
    return s
def social(key, hd, label, opinion):
    return '''  <ThoughtDef>
    <defName>LP_GraftOther_%s</defName>
    <thoughtClass>Thought_SituationalSocial</thoughtClass>
    <workerClass>LichPhylactery.ThoughtWorker_GraftOther</workerClass>
    <hediff>%s</hediff>
    <validWhileDespawned>true</validWhileDespawned>
    <stages><li><label>%s</label><baseOpinionOffset>%d</baseOpinionOffset></li></stages>
  </ThoughtDef>''' % (key, hd, label, opinion)

# E：死灵仪式台用生肉培育（和原有的四件血肉植入同一套配方），巫妖和活人都能装，可以叠肢体强化。
FLESH = [
    ('HellHeart', '冥火之心', 'part', 'Heart', 1.20, 'HellHeart', 800, '<stages><li><totalBleedFactor>0.2</totalBleedFactor></li></stages>', '', None,
     '冥火在胸腔里代替心跳。替换心脏，部位效率 120%；流血速度 ×0.2。'),
    ('BoneCage', '骸骨肋笼', 'implant', 'Torso', 0, 'BoneCage', 600, '', '', None,
     '在肋骨外再长一层骨笼。躯干附加，不替换部位：躯干最大生命 +50%，受到的利器伤害 −20%。'),
    ('DeadEar', '亡者低语之耳', 'part', 'Ear', 1.25, 'DeadEar', 400, '', '', 'LP_DeadSpeak',
     '听得见死人说话的耳朵。替换一只耳朵，部位效率 125%；能力“亡者之语”：对一具人形尸体使用，学到它最高那项技能总经验的 10%（每具尸体一次；照常折算冒险者经验）。'),
    ('SoulJaw', '吞魂之颌', 'part', 'Jaw', 1.20, 'SoulJaw', 600, '', '<li Class="HediffCompProperties_VerbGiver"><tools><li><label>吞魂之颌</label><capacities><li>Bite</li></capacities><power>14</power><cooldownTime>2</cooldownTime></li></tools></li>', None,
     '满口尖牙的下颌。替换下颌，部位效率 120%；多一个近战咬击（14 伤害）；用它咬死血肉活物时回复 10% 精神力。'),
]
out.append('<!-- ===================== 1.113.0 巫妖血肉植入扩充 ===================== -->')
for key, label, kind, part, eff, flag, meat, stage, comps, ab, desc in FLESH:
    d = 'LP_Flesh_' + key; dd = esc(desc + '在死灵仪式台用 %d 份生肉和 150 白银培育，巫妖和活人都能装，可以叠肢体强化。普通档，不占调谐位。' % meat)
    out.append(thing(d, label, dd, 'Flesh' + key, 1200 + meat))
    out.append(hediff(d, label, dd, kind, part, eff, flag, False, stage, comps, '<ability>%s</ability>' % ab if ab else ''))
    out.append(install(d, label, dd, kind, part))
    out.append('''  <RecipeDef>
    <defName>LP_Craft_%s</defName><label>培育%s</label><description>消耗 %d 份新鲜生肉与 150 白银培育%s。</description><jobString>正在培育%s。</jobString>
    <workerClass>LichPhylactery.FleshImplantRecipe</workerClass><ingredientValueGetterClass>LichPhylactery.RitualCount</ingredientValueGetterClass>
    <workAmount>16000</workAmount><workSpeedStat>GeneralLaborSpeed</workSpeedStat><workSkill>Crafting</workSkill><skillRequirements><Crafting>8</Crafting></skillRequirements>
    <recipeUsers><li>LP_Altar</li></recipeUsers><allowMixingIngredients>true</allowMixingIngredients>
    <ingredients><li><filter><categories><li>MeatRaw</li></categories></filter><count>%d</count></li><li><filter><thingDefs><li>Silver</li></thingDefs></filter><count>150</count></li></ingredients>
    <fixedIngredientFilter><categories><li>MeatRaw</li></categories><thingDefs><li>Silver</li></thingDefs></fixedIngredientFilter>
    <products><%s>1</%s></products>
  </RecipeDef>''' % (key, label, meat, label, label, meat, d, d))

# B：炼金台用怪物材料培养；只能活着的血肉供养，装到巫妖身上 3 天坏死。
COLORS = [('Red', '赤', 0), ('White', '白', 1), ('Blue', '蓝', 2), ('Black', '黑', 3), ('Green', '绿', 4)]
ELEM = ['火焰', '寒冰', '闪电', '强酸', '毒素']; OPP = [1, 0, 3, 2, 0]
GRAFTS = [
    ('BeholderEye', '眼魔之眼', 'part', 'Eye', 1.10, 'Beholder', True, '', '<ability>LP_GraftRay</ability><graft>true</graft>', {'LP_BeholderEye': 1, 'MedicineHerbal': 5}, 'LP_DNDAlchemy', 6000,
     '从眼魔身上取下的一只眼睛，接进眼窝还在转。替换一只眼睛，部位效率 110%；能力“眼魔射线”（20 格，冷却 1 天）：装上时随机定下是死灵射线（30 坏死伤害）、石化射线（开始石化）还是魅惑射线（恍惚 25 秒）。副作用：自己心情 −5（异怪凝视），别人对你的印象 −10。稀有档，占 1 个调谐位。'),
    ('DemonArm', '恶魔之臂', 'part', 'Shoulder', 1.30, 'DemonArm', False, '', '<graft>true</graft><noCelestial>true</noCelestial>', {'LP_DemonBlood': 3, 'MedicineHerbal': 5}, 'LP_Research_Abyssology', 2800,
     '浸过恶魔血长出来的手臂，皮下透着火光。替换整条手臂，部位效率 130%；近战命中附加 5 点火焰伤害。副作用：每天 1% 陷入狂怒崩溃；天界血统（阿斯莫、天界契约种族）装不上。普通档，不占调谐位。'),
    ('TrollGland', '巨魔再生腺', 'implant', 'Torso', 0, 'Troll', False, '<stages><li><naturalHealingFactor>3</naturalHealingFactor><hungerRateFactorOffset>0.5</hungerRateFactorOffset></li></stages>', '<graft>true</graft>', {'LP_TrollHide': 10, 'MedicineHerbal': 5}, 'LP_DNDAlchemy', 2200,
     '巨魔的再生腺体，缝在躯干上。躯干附加：伤口自然愈合 ×3；每 15 天长回一个缺失的小部位。副作用：饥饿速度 +50%；受到火焰、强酸伤害后再生停止 12 小时。普通档，不占调谐位。'),
]
out.append('<!-- ===================== 1.113.0 异怪嫁接 ===================== -->')
out.append('''  <HediffDef ParentName="LP_ClassBuffBase">
    <defName>LP_GraftTrollStalled</defName>
    <label>再生停滞</label>
    <description>火焰或强酸烧坏了巨魔再生腺：12 小时内自然愈合恢复正常速度，也不会长回部位。</description>
    <isBad>true</isBad>
    <stages><li><naturalHealingFactor>0.334</naturalHealingFactor></li></stages>
  </HediffDef>
  <ThoughtDef>
    <defName>LP_GraftMood_Beholder</defName>
    <workerClass>ThoughtWorker_Hediff</workerClass>
    <hediff>LP_Graft_BeholderEye</hediff>
    <validWhileDespawned>true</validWhileDespawned>
    <stages><li><label>异怪凝视</label><description>眼窝里那只眼睛总在看着不该看的东西。</description><baseMoodEffect>-5</baseMoodEffect></li></stages>
  </ThoughtDef>''')
out.append(social('Beholder', 'LP_Graft_BeholderEye', '异怪凝视', -10))
def graft_recipe(d, label, mats, res):
    return '''  <RecipeDef>
    <defName>LP_Grow_%s</defName><label>培养%s</label><description>在炼金台用怪物材料培养%s（嫁接物只能装在活人身上）。</description><jobString>正在培养%s。</jobString>
    <workAmount>9000</workAmount><workSpeedStat>GeneralLaborSpeed</workSpeedStat><workSkill>Intellectual</workSkill><effectWorking>Cook</effectWorking><soundWorking>Recipe_Drug</soundWorking>
    <researchPrerequisite>%s</researchPrerequisite><skillRequirements><Intellectual>8</Intellectual></skillRequirements>
    <recipeUsers><li>LP_AlchemyTable</li></recipeUsers>
    %s
    <products><%s>1</%s></products>
  </RecipeDef>''' % (d[3:], label, label, label, res, ing(mats), d, d)
for key, label, kind, part, eff, flag, att, stage, ext, mats, res, value, desc in GRAFTS:
    d = 'LP_Graft_' + key; dd = esc(desc + '嫁接物只能靠活的血肉供养，装在巫妖身上 3 天就坏死。')
    out.append(thing(d, label, dd, 'Graft' + key, value)); out.append(hediff(d, label, dd, kind, part, eff, flag, att, stage, '', ext))
    out.append(install(d, label, dd, kind, part)); out.append(graft_recipe(d, label, mats, res))
for col, cn, el in COLORS:
    d = 'LP_Graft_Scale' + col; label = cn + '龙鳞皮下甲'
    dd = esc('%s龙的鳞片嵌在皮下。躯干附加：利器护甲 +15%%，%s伤害 −50%%，%s伤害 +20%%。同时只能装一件龙鳞皮下甲。普通档，不占调谐位。嫁接物只能靠活的血肉供养，装在巫妖身上 3 天就坏死。' % (cn, ELEM[el], ELEM[OPP[el]]))
    out.append(thing(d, label, dd, 'GraftScale' + col, 2400))
    out.append(hediff(d, label, dd, 'implant', 'Torso', 0, 'Scale', False, '<stages><li><statOffsets><ArmorRating_Sharp>0.15</ArmorRating_Sharp></statOffsets></li></stages>', '', '<graft>true</graft><group>Scale</group><element>%d</element>' % el))
    out.append(install(d, label, dd, 'implant', 'Torso')); out.append(graft_recipe(d, label, {'LP_DragonScale_' + col: 10, 'MedicineHerbal': 5}, 'LP_DNDAlchemy'))
for col, cn, el in COLORS:
    d = 'LP_Graft_Breath' + col; label = cn + '龙息腺'
    dd = esc('%s龙心里剖出的龙息腺，缝在颈部。颈部附加：能力“龙息”（锥形 6 格，敌人受 30 点%s伤害，冷却 1 天）。副作用：嘴里总冒着余烬，别人对你的印象 −5。同时只能装一件龙息腺。稀有档，占 1 个调谐位。嫁接物只能靠活的血肉供养，装在巫妖身上 3 天就坏死。' % (cn, ELEM[el]))
    out.append(thing(d, label, dd, 'GraftBreath' + col, 4500))
    out.append(hediff(d, label, dd, 'implant', 'Neck', 0, 'Breath', True, '', '', '<graft>true</graft><group>Breath</group><element>%d</element><ability>LP_GraftBreath</ability>' % el))
    out.append(install(d, label, dd, 'implant', 'Neck')); out.append(graft_recipe(d, label, {'LP_DragonHeart_' + col: 1, 'MedicineHerbal': 5}, 'LP_DNDAlchemy'))
    out.append(social('Breath' + col, d, '口吐余烬', -5))
# 能力
out.append('''  <AbilityDef>
    <defName>LP_GraftRay</defName><label>眼魔射线</label>
    <description>眼魔之眼射出一道射线：死灵射线（30 坏死伤害）、石化射线（开始石化）或魅惑射线（恍惚 25 秒），种类在装上眼睛时定下。20 格，冷却 1 天。</description>
    <iconPath>Items/Implants/GraftBeholderEye</iconPath>
    <cooldownTicksRange>60000</cooldownTicksRange><hostile>true</hostile><aiCanUse>false</aiCanUse><showPsycastEffects>false</showPsycastEffects><sendMessageOnCooldownComplete>true</sendMessageOnCooldownComplete>
    <verbProperties><verbClass>Verb_CastAbility</verbClass><range>20</range><warmupTime>1</warmupTime><requireLineOfSight>true</requireLineOfSight>
      <targetParams><canTargetPawns>true</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetLocations>false</canTargetLocations><canTargetSelf>false</canTargetSelf></targetParams></verbProperties>
    <comps><li Class="LichPhylactery.CompProperties_GraftAbility"><effect>ray</effect></li></comps>
  </AbilityDef>
  <AbilityDef>
    <defName>LP_GraftBreath</defName><label>龙息</label>
    <description>喷出一道龙息：锥形 6 格内的敌人受 30 点伤害，元素随龙息腺的颜色（赤 火焰、白 寒冰、蓝 闪电、黑 强酸、绿 毒素）。冷却 1 天。</description>
    <iconPath>Items/Implants/GraftBreathRed</iconPath>
    <cooldownTicksRange>60000</cooldownTicksRange><hostile>true</hostile><aiCanUse>false</aiCanUse><showPsycastEffects>false</showPsycastEffects><sendMessageOnCooldownComplete>true</sendMessageOnCooldownComplete>
    <verbProperties><verbClass>Verb_CastAbility</verbClass><range>6</range><warmupTime>0.5</warmupTime><requireLineOfSight>true</requireLineOfSight>
      <targetParams><canTargetPawns>true</canTargetPawns><canTargetBuildings>true</canTargetBuildings><canTargetLocations>true</canTargetLocations><canTargetSelf>false</canTargetSelf></targetParams></verbProperties>
    <comps><li Class="LichPhylactery.CompProperties_GraftAbility"><effect>breath</effect></li></comps>
  </AbilityDef>
  <AbilityDef>
    <defName>LP_DeadSpeak</defName><label>亡者之语</label>
    <description>贴近一具人形尸体听它说话：学到它最高那项技能总经验的 10%（每具尸体一次；照常折算冒险者经验）。</description>
    <iconPath>Items/Implants/FleshDeadEar</iconPath>
    <cooldownTicksRange>600</cooldownTicksRange><hostile>false</hostile><aiCanUse>false</aiCanUse><showPsycastEffects>false</showPsycastEffects><displayGizmoWhileUndrafted>true</displayGizmoWhileUndrafted>
    <verbProperties><verbClass>Verb_CastAbility</verbClass><range>2.9</range><warmupTime>3</warmupTime><requireLineOfSight>true</requireLineOfSight>
      <targetParams><canTargetPawns>false</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetItems>true</canTargetItems><canTargetLocations>false</canTargetLocations><canTargetSelf>false</canTargetSelf></targetParams></verbProperties>
    <comps><li Class="LichPhylactery.CompProperties_GraftAbility"><effect>speak</effect></li></comps>
  </AbilityDef>''')
# ==================== 1.114.0 维克那神器（D） ====================
out.append('<!-- ===================== 1.114.0 维克那神器、叛徒卡斯 ===================== -->')
VECNA = [
    ('Hand', '维克那之手', 'Hand', 'VecnaHand', '<ability>LP_VecnaFrostTouch</ability><ability2>LP_VecnaCommand</ability2>',
     '传说中巫妖神维克那被背叛者砍下的左手，干枯发黑，却还在动。替换左手（必须先截掉自己的左手才能装），部位效率 150%；冒险者力量至少 20；能力“寒冰触”（近身，30 寒冰伤害并降温，冷却 10 秒）和“指挥亡灵”（20 格内一个敌方亡灵为你作战 30 秒，冷却 1 天）。诅咒：每 60 天有 10% 陷入“邪恶意志”杀戮狂暴（巫妖减半）。'),
    ('Eye', '维克那之眼', 'Eye', 'VecnaEye', '<ability>LP_VecnaDominate</ability>',
     '维克那被剜出的左眼，像一块暗红的宝石。替换左眼，部位效率 150%；黑暗中视物如常（不受光照惩罚）；能力“支配人类”（和夺心魔的支配一样：30 秒内只攻击原来的同伴，冷却 1 天）。诅咒：自己心情 −8（维克那凝视），别人对你的印象 −20。'),
]
for key, label, part, flag, ab, desc in VECNA:
    d = 'LP_Vecna_' + key
    dd = esc(desc + '神器档，占 1 个调谐位；不能叠巫妖肢体强化。拆下来神器就掉在地上，宿主心情 −15、持续 30 天。两件都装在同一个人身上时触发“维克那的意志”：统御上限 +50%，巫妖带死灵之躯时命匣复活 3 天缩到 1 天——但叛徒卡斯会带着卡斯之剑找上门（第一次在 3～7 天后，之后每 30 天）。世上各只有一件：探索公告板传奇委托、迷锁 60 层以后的守卫极少给。')
    out.append(thing(d, label, dd, 'Vecna' + key, 9000, 'BodyPartsArchotech'))
    out.append(hediff(d, label, dd, 'part', part, 1.5, flag, True, '', '', '<group>Vecna</group>' + ab))
    out.append('''  <RecipeDef ParentName="SurgeryInstallBodyPartArtificialBase">
    <defName>LP_Install_%s</defName><label>安装%s</label><description>%s</description><jobString>正在安装%s。</jobString>
    <workerClass>LichPhylactery.Recipe_InstallVecna</workerClass>
    <ingredients><li><filter><thingDefs><li>%s</li></thingDefs></filter><count>1</count></li></ingredients>
    <fixedIngredientFilter><thingDefs><li>%s</li></thingDefs></fixedIngredientFilter>
    <appliedOnFixedBodyParts><li>%s</li></appliedOnFixedBodyParts>
    <addsHediff>%s</addsHediff>
  </RecipeDef>''' % (d, label, dd, label, d, d, part, d))
out.append(social('Vecna', 'LP_Vecna_Eye', '维克那凝视', -20))
out.append('''  <ThoughtDef>
    <defName>LP_VecnaMood</defName>
    <workerClass>ThoughtWorker_Hediff</workerClass>
    <hediff>LP_Vecna_Eye</hediff>
    <validWhileDespawned>true</validWhileDespawned>
    <stages><li><label>维克那凝视</label><description>这只眼睛看见的东西都在低语。</description><baseMoodEffect>-8</baseMoodEffect></li></stages>
  </ThoughtDef>
  <ThoughtDef>
    <defName>LP_VecnaLoss</defName>
    <durationDays>30</durationDays>
    <stackLimit>1</stackLimit>
    <stages><li><label>失去了维克那的神器</label><description>它被从我身上割了下来。那股力量……我还想要。</description><baseMoodEffect>-15</baseMoodEffect></li></stages>
  </ThoughtDef>
  <AbilityDef>
    <defName>LP_VecnaFrostTouch</defName><label>寒冰触</label>
    <description>维克那之手按在一个敌人身上：30 点寒冰伤害并让它失温。冷却 10 秒。</description>
    <iconPath>Items/Implants/VecnaHand</iconPath>
    <cooldownTicksRange>600</cooldownTicksRange><hostile>true</hostile><aiCanUse>false</aiCanUse><showPsycastEffects>false</showPsycastEffects>
    <verbProperties><verbClass>Verb_CastAbility</verbClass><range>1.9</range><warmupTime>0.3</warmupTime><requireLineOfSight>true</requireLineOfSight>
      <targetParams><canTargetPawns>true</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetLocations>false</canTargetLocations><canTargetSelf>false</canTargetSelf></targetParams></verbProperties>
    <comps><li Class="LichPhylactery.CompProperties_GraftAbility"><effect>frost</effect></li></comps>
  </AbilityDef>
  <AbilityDef>
    <defName>LP_VecnaCommand</defName><label>指挥亡灵</label>
    <description>维克那之手向 20 格内一个敌方亡灵下令：它在 30 秒内转而攻击原来的同伴（可能被抵抗）。冷却 1 天。</description>
    <iconPath>Items/Implants/VecnaHand</iconPath>
    <cooldownTicksRange>60000</cooldownTicksRange><hostile>true</hostile><aiCanUse>false</aiCanUse><showPsycastEffects>false</showPsycastEffects><sendMessageOnCooldownComplete>true</sendMessageOnCooldownComplete>
    <verbProperties><verbClass>Verb_CastAbility</verbClass><range>20</range><warmupTime>1</warmupTime><requireLineOfSight>true</requireLineOfSight>
      <targetParams><canTargetPawns>true</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetLocations>false</canTargetLocations><canTargetSelf>false</canTargetSelf></targetParams></verbProperties>
    <comps><li Class="LichPhylactery.CompProperties_GraftAbility"><effect>command</effect></li></comps>
  </AbilityDef>
  <AbilityDef>
    <defName>LP_VecnaDominate</defName><label>支配人类</label>
    <description>维克那之眼盯住 20 格内一个人形敌人：它在 30 秒内只攻击原来的同伴，时间到精疲力竭倒地（可能被抵抗）。冷却 1 天。</description>
    <iconPath>Items/Implants/VecnaEye</iconPath>
    <cooldownTicksRange>60000</cooldownTicksRange><hostile>true</hostile><aiCanUse>false</aiCanUse><showPsycastEffects>false</showPsycastEffects><sendMessageOnCooldownComplete>true</sendMessageOnCooldownComplete>
    <verbProperties><verbClass>Verb_CastAbility</verbClass><range>20</range><warmupTime>1</warmupTime><requireLineOfSight>true</requireLineOfSight>
      <targetParams><canTargetPawns>true</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetLocations>false</canTargetLocations><canTargetSelf>false</canTargetSelf></targetParams></verbProperties>
    <comps><li Class="LichPhylactery.CompProperties_GraftAbility"><effect>dominate</effect></li></comps>
  </AbilityDef>
  <HediffDef>
    <defName>LP_KasBlood</defName>
    <label>吸血鬼之躯</label>
    <description>卡斯早已是吸血鬼：受到的伤害 −50%，近战伤害 +50%，伤口愈合 ×5。</description>
    <hediffClass>HediffWithComps</hediffClass><isBad>false</isBad><everCurableByItem>false</everCurableByItem><scenarioCanAdd>false</scenarioCanAdd><defaultLabelColor>(0.7,0.15,0.2)</defaultLabelColor>
    <stages><li><naturalHealingFactor>5</naturalHealingFactor><statFactors><IncomingDamageFactor>0.5</IncomingDamageFactor><MeleeDamageFactor>1.5</MeleeDamageFactor></statFactors></li></stages>
  </HediffDef>
  <PawnKindDef ParentName="LP_FighterBase">
    <defName>LP_Kas</defName>
    <label>叛徒卡斯</label>
    <defaultFactionDef>Pirate</defaultFactionDef>
    <combatPower>600</combatPower>
    <minGenerationAge>40</minGenerationAge><maxGenerationAge>60</maxGenerationAge>
    <apparelMoney>3000~4000</apparelMoney>
    <apparelTags><li>IndustrialMilitaryAdvanced</li><li>IndustrialMilitaryBasic</li></apparelTags>
    <apparelRequired><li>Apparel_PlateArmor</li></apparelRequired>
    <weaponMoney>0</weaponMoney>
    <canBeSapper>false</canBeSapper>
    <chemicalAddictionChance>0</chemicalAddictionChance>
    <combatEnhancingDrugsChance>0</combatEnhancingDrugsChance>
    <skills><li><skill>Melee</skill><range>18~20</range></li></skills>
  </PawnKindDef>
  <ThingDef ParentName="BaseMeleeWeapon_Sharp_Quality">
    <defName>LP_SwordOfKas</defName>
    <label>卡斯之剑</label>
    <description>维克那亲手交给卡斯、卡斯又用它砍下维克那左手的魔剑。剑身里封着一个渴血的灵魂。锋利、沉重，对维克那神器（维克那之手、维克那之眼）的宿主伤害翻倍。不能制作。</description>
    <techLevel>Medieval</techLevel>
    <graphicData><texPath>Items/Implants/SwordOfKas</texPath><graphicClass>Graphic_Single</graphicClass></graphicData>
    <statBases><WorkToMake>60000</WorkToMake><Mass>2.5</Mass><MarketValue>5000</MarketValue></statBases>
    <tradeability>Sellable</tradeability>
    <weaponTags Inherit="False" />
    <recipeMaker IsNull="True" />
    <costList Inherit="False" />
    <tools>
      <li><label>剑刃</label><capacities><li>Cut</li></capacities><power>30</power><cooldownTime>2</cooldownTime><armorPenetration>0.6</armorPenetration></li>
      <li><label>剑尖</label><capacities><li>Stab</li></capacities><power>26</power><cooldownTime>2</cooldownTime><armorPenetration>0.7</armorPenetration></li>
      <li><label>剑柄</label><capacities><li>Blunt</li></capacities><power>10</power><cooldownTime>2</cooldownTime></li>
    </tools>
  </ThingDef>''')
out.append('</Defs>')
p = os.path.join(ROOT, 'Defs', 'Implants_DND.xml')
open(p, 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('wrote', p, len(PARTS), 'parts,', len(STONES), 'stones')
