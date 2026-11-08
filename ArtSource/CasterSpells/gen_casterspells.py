# -*- coding: utf-8 -*-
# 1.130.0 施法职业法术扩充：从下面的表生成 Defs/CasterSpells.xml（法术、状态、思想、良莓）和 Defs/CasterSpells_Lists.xml（各职业法术表）。
# 用法：python -I gen_casterspells.py <模组根目录>
import sys, os
from xml.sax.saxutils import escape

ROOT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), '..', '..')
W = lambda n: 'LP/Classes/WizardSpells/' + n
Cl = lambda n: 'LP/Classes/Cleric/' + n
Dr = lambda n: 'LP/Classes/Druid/' + n
So = lambda n: 'LP/Classes/Sorcerer/' + n
Wl = lambda n: 'LP/Classes/Warlock/' + n
Pa = lambda n: 'LP/Classes/Paladin/' + n
Ra = lambda n: 'LP/Classes/Ranger/' + n
Bd = lambda n: 'LP/Classes/Bard/' + n
Lg = lambda n: 'LP/Classes/Legendary/' + n
Ar = lambda n: 'LP/Classes/Artificer/' + n
NEWICON = lambda n: 'LP/Classes/CasterSpells/' + n  # 1.131.0 新图标
NEW_ICONS = '--old-icons' not in sys.argv

STAT = {'Cleric': 'LP_DivinePower', 'Druid': 'LP_PrimalPower', 'Sorcerer': 'LP_InnatePower', 'Warlock': 'LP_PactPower',
        'Bard': 'LP_BardicPower', 'Paladin': 'LP_DivinePower', 'Ranger': 'LP_HuntPower', 'Artificer': 'LP_ArtificePower'}
MANA = [0, 1, 1, 2, 2, 3, 4, 5, 6, 9]
CIRCLE = ['戏法', '1 环', '2 环', '3 环', '4 环', '5 环', '6 环', '7 环', '8 环', '9 环']
SPELLS = []

def S(id, label, circle, school, target, effect, desc, icon, cls, cd=30, rng=20, warm=1.0, power=None, radius=None, dur=None, hediff=None,
      ext=None, ritual=False, conc=False, upcast=None, cost=None, fixed=False, approach=None, ver='1.130', hostile=None, erange=None):
    SPELLS.append(dict(id=id, label=label, circle=circle, school=school, target=target, effect=effect, desc=desc, icon=icon, cls=cls, cd=cd, rng=rng,
                       warm=warm, power=power, radius=radius, dur=dur, hediff=hediff, ext=ext or {}, ritual=ritual, conc=conc, upcast=upcast,
                       cost=cost, fixed=fixed, approach=approach, ver=ver, hostile=hostile, erange=erange))

# ==================== 牧师 ====================
S('Thaumaturgy', '奇术', 0, '变化', 'loc', 'CS_Util', '在 15 格内一点显出神迹：半径 2 内的污物清理干净，并点起一团持续 1 小时的圣光。', W('Prestidigitation'), 'Cleric', cd=5, rng=15, radius=2, ext=dict(kind='thaumaturgy'), hostile=False)
S('TollTheDead', '亡者丧钟', 0, '死灵', 'foe', 'CS_Bolt', '24 格内一个敌人耳边响起丧钟，受 8 × 强度点死灵伤害；目标已经受伤时伤害翻倍。', W('CircleOfDeath'), 'Cleric', cd=4, rng=24, warm=.5, power=8, ext=dict(damage='Necrotic', vs='Wounded', ap=.3))
S('Resistance', '抗性术', 0, '防护', 'ally', 'TargetBuff', '10 格内一名友军 30 秒内受到的伤害 ×0.85。', Cl('Bless'), 'Cleric', cd=10, rng=10, warm=.5, dur=1800, hediff='LP_CS_Resistance', fixed=True)
S('HealingWord', '治疗真言', 1, '塑能', 'ally', 'CS_Heal', '远远念一句治愈真言：30 格内一名友军立即治疗 10 × 强度点伤势（优先流血伤口）。升环：每环效果 +25%。', Bd('HealingWord'), 'Cleric', cd=10, rng=30, warm=.5, power=10, upcast='power')
S('Command', '命令术', 1, '惑控', 'foe', 'CS_Hex', '对 20 格内一个敌人下一句命令，成功率 60%：随机“趴下”（倒地 3 秒）、“滚开”（逃跑）或“放下”（丢下武器）。目标每比施法者高 1 级 −5%（最低 10%），传奇生物和剧情首领减半，心智抗性也能抵抗。对机械体无效。升环：每环多命令 6 格内另一个敌人。', Wl('Beguile'), 'Cleric', cd=20, power=None, ext=dict(kind='command', chance=.6, mind=True), upcast='target')
S('ShieldOfFaith', '虔诚护盾', 1, '防护', 'ally', 'CS_Buff', '20 格内一名友军被闪光的护盾环绕 10 分钟：利器与钝器护甲 +20%（专注）。', Pa('AuraOfProtection'), 'Cleric', cd=20, dur=36000, hediff='LP_CS_ShieldOfFaith', fixed=True, conc=True)
S('CreateWater', '造水术', 1, '咒法', 'loc', 'CS_Util', '20 格内半径 3 降下一阵清水：扑灭范围内的火（包括身上着火的人），浇灌作物（生长 +5%）。', W('FogCloud'), 'Cleric', cd=10, radius=3, ext=dict(kind='createWater'), ritual=True, hostile=False)
S('PurifyFood', '净化饮食', 1, '变化', 'loc', 'CS_Util', '净化 10 格内半径 3 的食物：腐烂进度归零；范围内友方的人治好食物中毒。', Cl('LesserRestoration'), 'Cleric', cd=10, rng=10, radius=3, ext=dict(kind='purify'), ritual=True, hostile=False)
S('GuidingBolt', '指引之箭', 1, '塑能', 'foe', 'CS_Bolt', '一道光箭射向 24 格内一个敌人，14 × 强度点光耀伤害；之后 10 秒内它下一次被攻击伤害 ×1.3。升环：每环效果 +25%。', Cl('SacredFlame'), 'Cleric', cd=15, rng=24, warm=.5, power=14, dur=600, ext=dict(damage='Radiant', hediff2='LP_CS_Outlined', ap=.3), upcast='power')
S('Aid', '援助术', 2, '防护', 'ally', 'CS_Buff', '20 格内一名友军和它身边 6 格内另外 2 名友军各得 10 × 强度点临时生命（先扣临时生命再受伤），持续 8 小时。升环：每环效果 +25%。', Pa('LayOnHands'), 'Cleric', cd=40, power=10, radius=6, dur=20000, ext=dict(kind='tempHp', count=3), upcast='power')
S('SpiritualWeapon', '灵体武器', 2, '塑能', 'self', 'CS_Aura', '召出一把飘浮的光耀之锤，1 分钟内每 2 秒敲打施法者 15 格内最近的一个敌人，8 × 强度点伤害（专注）。', Pa('SacredWeapon'), 'Cleric', cd=30, power=8, dur=3600, hediff='LP_CS_SpiritWeapon', conc=True)
S('PrayerOfHealing', '祈祷治疗', 2, '塑能', 'self', 'CS_HealArea', '带领身边 8 格内最多 10 名友军祈祷：每人治疗 25 × 强度点伤势。可以仪式施法（原地引导 1 小时，不耗资源）。', Cl('MassHeal'), 'Cleric', cd=60, power=25, radius=8, ext=dict(count=10), ritual=True)
S('Silence', '静默术', 2, '幻术', 'loc', 'CS_Zone', '20 格内半径 3 的区域 1 分钟内鸦雀无声：里面的任何人都念不出咒语，放不了职业法术（专注）。', Wl('ThoughtShield'), 'Cleric', cd=30, radius=3, dur=3600, ext=dict(zone='Silence'), ritual=True, conc=True)
S('Revivify', '复苏术', 3, '死灵', 'corpse', 'CS_Revive', '让 5 格内一具死亡不超过 1 天的人形尸体复活，复活者 3 天“复生虚弱”。对巫妖、死灵造物和大脑不在的尸体无效。', Cl('Raise'), 'Cleric', cd=60, rng=5, warm=2, ext=dict(days=1))
S('SpiritGuardians', '守护灵', 3, '咒法', 'self', 'CS_Aura', '守护灵绕身飞舞 1 分钟：每秒对身边 3 格内的敌人造成 4 × 强度点光耀伤害，并让它们移动 ×0.5（专注）。', Lg('DivineGuardian'), 'Cleric', cd=30, power=8, radius=3, dur=3600, hediff='LP_CS_SpiritGuardians', conc=True)
S('CreateFood', '造粮术', 3, '咒法', 'self', 'CS_Spawn', '变出 15 份简单餐，放在施法者身边。', Dr('PlantGrowth'), 'Cleric', cd=60, ext=dict(thing='MealSimple', count=15), ritual=True)
S('MassHealingWord', '群体治疗真言', 3, '塑能', 'self', 'CS_HealArea', '20 格内最多 6 名受伤最重的友军各治疗 10 × 强度点伤势。升环：每环效果 +25%。', Bd('HealingWord'), 'Cleric', cd=30, power=10, radius=20, ext=dict(count=6), upcast='power')
S('Daylight', '昼明术', 3, '塑能', 'loc', 'CS_Util', '30 格内一点升起三团明亮的光球，照亮一大片地方，持续 1 天。', W('Light'), 'Cleric', cd=40, rng=30, dur=60000, ext=dict(kind='daylight'), hostile=False)
S('RemoveCurse', '移除诅咒', 3, '防护', 'ally', 'CS_Restore', '解除 5 格内一名友军身上的诅咒类状态（咒术、各种诅咒、神力支配），以及它装备的一件诅咒物品的诅咒。', Pa('DispelEvil'), 'Cleric', cd=20, rng=5, ext=dict(kind='curse'))
S('FreedomOfMovement', '自由行动', 4, '防护', 'ally', 'TargetBuff', '10 格内一名友军 1 小时内不受减速、束缚和眩晕（每秒解除一次）。', W('Fly'), 'Cleric', cd=40, rng=10, dur=2500, hediff='LP_CS_Freedom', fixed=True)
S('GuardianOfFaith', '信仰守卫', 4, '咒法', 'loc', 'CS_Zone', '在 20 格内一点召出光辉的守卫（半径 2.5）：靠近的敌人受 20 × 强度点光耀伤害（每个敌人每 5 秒一次）；打出 4 次或过了 8 小时就消散。', Lg('DivineGuardian'), 'Cleric', cd=60, power=20, radius=2.5, dur=20000, ext=dict(zone='Guardian'), hostile=False)
S('Divination', '占卜术', 4, '预言', 'self', 'CS_Util', '向神明求问：列出 3 天内已经排定的威胁事件，并估计一次袭击的规模。每天一次。', Cl('Guidance'), 'Cleric', cd=1000, dur=180000, ext=dict(kind='divination'), ritual=True)
S('FlameStrike', '焰击术', 5, '塑能', 'loc', 'CS_Area', '一道火柱从天而降：24 格内半径 2.5 的敌人受 18 × 强度点火焰伤害和 18 × 强度点光耀伤害。升环：每环效果 +25%。', Lg('DivineIntervention'), 'Cleric', cd=30, rng=24, power=18, radius=2.5, ext=dict(damage='Flame', damage2='Radiant', ap=.3), upcast='power')
S('GreaterRestoration', '高等复原术', 5, '防护', 'ally', 'CS_Restore', '除去 5 格内一名友军身上的一个顽疾：弱智术、咒术和诅咒，大脑上的旧伤，痴呆、阿尔茨海默症、白内障、听力损伤、虚弱、背痛、哮喘，或者最重的一处旧伤疤。', Cl('LesserRestoration'), 'Cleric', cd=60, rng=5, ext=dict(kind='greater'))
S('RaiseDead', '死者复活', 5, '死灵', 'corpse', 'CS_Revive', '让 5 格内一具死亡不超过 10 天的人形尸体复活，复活者 3 天“复生虚弱”。对巫妖、死灵造物和大脑不在的尸体无效。', Cl('Raise'), 'Cleric', cd=1000, rng=5, warm=3, ext=dict(days=10))
S('Hallow', '圣居', 5, '塑能', 'loc', 'CS_Zone', '把 10 格内半径 6 的地方祝圣 10 天：亡灵、恶魔、魔鬼和异常生物进来就每秒受 10 × 强度点光耀伤害，并被推出去。', Cl('TurnUndead'), 'Cleric', cd=1000, rng=10, power=10, radius=6, dur=600000, ext=dict(zone='Hallow'), ritual=True, hostile=False)
S('BladeBarrier', '剑刃屏障', 6, '塑能', 'loc', 'CS_Zone', '在 24 格内立起一道 10 格长的旋转刀墙 1 分钟：站在墙上的敌人每秒受 15 × 强度点割伤（专注）。', Lg('ThousandArmies'), 'Cleric', cd=60, rng=24, power=15, radius=.5, dur=3600, ext=dict(zone='Blades', line=True, cells=10, damage='Cut'), conc=True)
S('HeroesFeast', '英雄宴', 6, '咒法', 'self', 'CS_Util', '摆出一桌神赐的宴席：身边 8 格内最多 12 名友方人形吃饱，治好食物中毒和毒素，24 小时内毒素抗性 +100%、精神崩溃阈值 −15%，并得到 20 × 强度点临时生命。每天一次。', Dr('PlantGrowth'), 'Cleric', cd=1000, power=20, radius=8, dur=60000, ext=dict(kind='feast'), ritual=True)
S('Heal', '治愈术', 6, '塑能', 'ally', 'CS_Heal', '20 格内一名友军治疗 70 × 强度点伤势，并治好疾病、失明、食物中毒和毒素。', Cl('CureWounds'), 'Cleric', cd=60, power=70, ext=dict(kind='full'))
S('ConjureCelestial', '天界守卫', 7, '咒法', 'loc', 'CS_Summon', '召来一位狄瓦为施法者作战 1 小时（专注）。', Lg('DivineIncarnation'), 'Cleric', cd=60, rng=15, dur=2500, ext=dict(thing='LP_CE_Deva'), conc=True, hostile=False)
S('Resurrection', '复活术', 7, '死灵', 'corpse', 'CS_Revive', '让 5 格内一具人形尸体复活，不论死了多久（尸体还在就行），伤势和缺失的部位全部恢复。', Cl('Raise'), 'Cleric', cd=1000, rng=5, warm=3, ext=dict(kind='full'))
S('DivineWord', '神圣之言', 7, '塑能', 'self', 'CS_Area', '念出神之真名：身边 8 格内的敌人眩晕 4 秒并失明 10 秒（伤势过半的眩晕 10 秒、失明 30 秒）；外来者（恶魔、魔鬼、元素、妖精）成功率 60% 直接被放逐回原来的位面（剧情首领除外）。', Pa('HolyAvatar'), 'Cleric', cd=60, radius=8, ext=dict(kind='divineWord'))
S('HolyAura', '神圣光环', 8, '防护', 'self', 'CS_Buff', '施法者和身边 6 格内最多 9 名友军被圣光笼罩 1 分钟：受到的伤害 ×0.7（专注）。', Pa('AuraOfCourage'), 'Cleric', cd=60, radius=6, dur=3600, hediff='LP_CS_HolyAura', fixed=True, ext=dict(count=10), conc=True)
S('DivineColumn', '神罚之柱', 8, '塑能', 'loc', 'CS_Area', '天上落下一根光柱：30 格内半径 3 的敌人受 45 × 强度点光耀伤害并眩晕 2 秒。', Lg('DivineIntervention'), 'Cleric', cd=60, rng=30, power=45, radius=3, ext=dict(damage='Radiant', stun=120, ap=.4))
S('MassHeal9', '群体治愈术', 9, '塑能', 'self', 'CS_HealArea', '15 格内最多 12 名受伤最重的友军各治疗 60 × 强度点伤势，并治好疾病、失明和毒素。', Cl('MassHeal'), 'Cleric', cd=60, power=60, radius=15, ext=dict(kind='full', count=12))
S('TrueResurrection', '真复活', 9, '死灵', 'self', 'CS_Util', '让一名死去的殖民者复活——不需要尸体，伤势和缺失的部位全部恢复，在施法者身边醒来。每 7 天一次。', Lg('UndyingLegend'), 'Cleric', cd=7000, ext=dict(kind='trueRes'))

# ==================== 德鲁伊 ====================
S('Druidcraft', '德鲁伊伎俩', 0, '变化', 'self', 'CS_Util', '读出天象：报告现在的天气还会持续多久、接下来一天的气温大约多少和正在持续的天气事件；身边 2 格的植物长快一点。', Dr('ControlWeather'), 'Druid', cd=10, ext=dict(kind='druidcraft'))
S('Shillelagh', '橡棍术', 0, '变化', 'self', 'SelfBuff', '手里的棍棒或拳头注入自然之力，1 分钟内近战伤害 ×1.4。', Dr('Barkskin'), 'Druid', cd=60, dur=3600, hediff='LP_CS_Shillelagh')
S('ProduceFlame', '造焰术', 0, '咒法', 'foe', 'CS_Bolt', '掌心燃起火焰，掷向 20 格内一个敌人，8 × 强度点火焰伤害。', So('BurningHands'), 'Druid', cd=4, warm=.5, power=8, ext=dict(damage='Flame'))
S('ThornWhip', '荆棘鞭', 0, '变化', 'foe', 'CS_Bolt', '荆棘长鞭抽中 12 格内一个敌人，7 × 强度点割伤，并把它往施法者这边拉近最多 3 格。', Dr('Entangle'), 'Druid', cd=5, rng=12, warm=.5, power=7, ext=dict(damage='Cut', pull=True))
S('MoldEarth', '塑土术', 0, '变化', 'loc', 'CS_Util', '10 格内一格：岩石立刻挖开；浅水、泥地、沼泽填成泥土；否则清理地面。', Lg('EarthShatter'), 'Druid', cd=5, rng=10, ext=dict(kind='moldEarth'), hostile=False)
S('Goodberry', '良莓术', 1, '变化', 'self', 'CS_Spawn', '变出 10 颗良莓（15 天后枯萎）：每颗都有一顿饭的营养，吃下去 5 秒内治疗 5 点伤势。', Dr('PlantGrowth'), 'Druid', cd=60, ext=dict(thing='LP_CS_Goodberry', count=10))
S('Jump', '跳跃术', 1, '变化', 'ally', 'TargetBuff', '10 格内一名友军 1 小时内跑跳如飞：移动 ×1.3。', Ra('Woodstride'), 'Druid', cd=20, rng=10, dur=2500, hediff='LP_CS_Jump', fixed=True)
S('Moonbeam', '月华术', 2, '塑能', 'loc', 'CS_Zone', '24 格内一点落下半径 1.5 的银色月光柱 1 分钟：里面的敌人每秒受 10 × 强度点光耀伤害（专注）。光柱不会移动。', W('Sunbeam'), 'Druid', cd=30, rng=24, power=10, radius=1.5, dur=3600, ext=dict(zone='Moon', damage='Radiant'), conc=True)
S('SpikeGrowth', '荆棘丛生', 2, '变化', 'loc', 'CS_Zone', '24 格内半径 3 的地面长满尖刺 1 分钟：里面的敌人移动 ×0.5，每秒受 4 × 强度点割伤（专注）。', Dr('Entangle'), 'Druid', cd=30, rng=24, power=4, radius=3, dur=3600, ext=dict(zone='Spikes', damage='Cut', hediff2='LP_CS_SpikeSlowed'), conc=True)
S('FlamingSphere', '炽焰法球', 2, '咒法', 'loc', 'CS_Zone', '在 20 格内一点召出一团滚烫的火球（半径 1.5），1 分钟内每秒对里面的敌人造成 8 × 强度点火焰伤害（专注）。火球不会移动。', W('FireBolt'), 'Druid', cd=30, power=8, radius=1.5, dur=3600, ext=dict(zone='Fire', damage='Flame'), conc=True)
S('FindTraps', '寻找陷阱', 2, '预言', 'self', 'CS_Util', '察觉地图上敌对的陷阱，并揭开 40 格内隐形的敌人，来信列出位置。', W('DetectMagic'), 'Druid', cd=40, radius=40, ext=dict(kind='findTraps'))
S('AnimalMessenger', '动物信使', 2, '惑控', 'self', 'CS_Util', '让一只小动物带口信给一个派系的首领：打开和它的通话（同通讯台）。每天一次。', Dr('AnimalFriendship'), 'Druid', cd=1000, ext=dict(kind='messenger'), ritual=True)
S('WindWall', '风墙', 3, '塑能', 'loc', 'CS_Zone', '在 20 格内立起一道 8 格长的狂风墙 1 分钟：墙上的敌人被吹退 2 格并受 6 × 强度点钝器伤害（每秒一次）；墙边 1 格内的友军受到的远程伤害减半（专注）。', Dr('ControlWeather'), 'Druid', cd=30, power=6, radius=.5, dur=3600, ext=dict(zone='Wind', line=True, cells=8), conc=True)
S('StoneShape', '塑石术', 4, '变化', 'loc', 'CS_Util', '15 格内一格：岩石立刻开凿掉（掉落石块）；空地上立起一面石墙。', Lg('EarthShatter'), 'Druid', cd=20, rng=15, ext=dict(kind='stoneShape'), hostile=False)
S('ConjureWoodland', '召唤林地生物', 4, '咒法', 'loc', 'CS_Summon', '召来 2 个林地生物（树精，没有时是萨提尔）为施法者作战 1 小时（专注）。', Dr('ConjureAnimals'), 'Druid', cd=60, rng=15, dur=2500, ext=dict(thing='LP_FY_Dryad,LP_FY_Satyr', count=2), conc=True, hostile=False)
S('DominateBeast', '支配野兽', 4, '惑控', 'foe', 'CS_Hex', '20 格内一只敌对的动物（发狂的、敌人带来的）成功率 50% 被支配，1 分钟内听施法者指挥（专注）。', Dr('BeastCommand'), 'Druid', cd=40, dur=3600, ext=dict(kind='dominate', chance=.5, vs='Animal'), conc=True)
S('InsectPlague', '虫群', 5, '咒法', 'loc', 'CS_Zone', '24 格内半径 4 涌出蝗虫群 1 分钟：里面的敌人每秒受 5 × 强度点割伤，移动 ×0.5（专注）。', W('StinkingCloud'), 'Druid', cd=40, rng=24, power=5, radius=4, dur=3600, ext=dict(zone='Insects', damage='Cut', hediff2='LP_CS_SpikeSlowed'), conc=True)
S('TreeStride', '树跃', 5, '咒法', 'loc', 'CS_Util', '站在树旁（2 格内）时，走进树里、从地图上另一棵树旁出来（目的地 1 格内要有树、已探索）。', Ra('Woodstride'), 'Druid', cd=10, rng=300, warm=1, ext=dict(kind='treeStride'), approach=False, hostile=False)
S('Awaken', '唤醒术', 5, '变化', 'ally', 'CS_Util', '唤醒 5 格内一只已经驯服的动物的心智：能学的训练全部学会，之后永远不会遗忘（不会野性复归）。', Dr('BeastCommand'), 'Druid', cd=1000, rng=5, warm=3, ext=dict(kind='awaken'), ritual=True)
S('Reincarnate', '转生术', 5, '变化', 'corpse', 'CS_Revive', '让 5 格内一具死亡不超过 10 天的人形尸体在新的身体里复活：伤势和缺失的部位全部恢复，但种族随机换成另一个（精灵、矮人、半兽人、元素裔、提夫林……，需要 Biotech）。', Dr('WildShape'), 'Druid', cd=1000, rng=5, warm=3, ext=dict(kind='reincarnate', days=10))
S('ControlEnvironment', '控制环境', 5, '变化', 'self', 'CS_Util', '结束地图上一个坏天气事件（寒潮、热浪、毒尘、火山冬天……），并让天气转晴。每天一次。', Dr('ControlWeather'), 'Druid', cd=1000, ext=dict(kind='controlEnv'))
S('WindWalk', '风行术', 6, '变化', 'self', 'CS_Buff', '施法者和身边 8 格内最多 9 名友军化身清风 1 小时：移动 ×2。', Ra('Woodstride'), 'Druid', cd=60, radius=8, dur=2500, hediff='LP_CS_WindWalk', fixed=True, ext=dict(count=10))
S('Whirlwind', '召唤龙卷风', 7, '塑能', 'loc', 'CS_Util', '在 30 格内一点召来一股龙卷风，30 秒后消散（它会自己乱走，敌我不分）。', Dr('CallLightning'), 'Druid', cd=60, rng=30, warm=2, dur=1800, ext=dict(kind='tornado'))
S('Earthquake', '地震术', 8, '塑能', 'loc', 'CS_Area', '30 格内半径 6 大地震动：敌人受 20 × 强度点钝器伤害并眩晕 2 秒，范围内不属于施法者一方的建筑受 40 × 强度点伤害。', Lg('EarthShatter'), 'Druid', cd=60, rng=30, warm=2, power=20, radius=6, ext=dict(kind='quake', damage='Blunt', ap=.2))
S('AnimalShapes', '上古兽形', 8, '变化', 'self', 'CS_Buff', '施法者和身边 8 格内最多 9 名友军变成野兽 1 小时（同荒野变形）。', Dr('WildShape'), 'Druid', cd=60, radius=8, dur=2500, ext=dict(kind='wildShape', count=10))
S('StormOfVengeance', '复仇风暴', 9, '咒法', 'loc', 'CS_Zone', '40 格内半径 10 的上空聚起风暴云 1 分钟：每 2 秒向区域里一个敌人劈下一道 25 × 强度点的落雷（专注）。', Dr('CallLightning'), 'Druid', cd=60, rng=40, warm=2, power=25, radius=10, dur=3600, ext=dict(zone='Storm'), conc=True)
S('TreeOfLife', '生命之树', 9, '变化', 'loc', 'CS_Zone', '在 15 格内种下一棵发光的生命之树（半径 6）：1 天内里面的友军每秒治疗 3 × 强度点伤势。', Dr('PlantGrowth'), 'Druid', cd=1000, rng=15, warm=2, power=3, radius=6, dur=60000, ext=dict(zone='Heal'), hostile=False)

# ==================== 术士 ====================
S('MindSliver', '心灵之楔', 0, '惑控', 'foe', 'CS_Bolt', '一根看不见的心灵之楔刺进 24 格内一个敌人的脑子：7 × 强度点心灵伤害（无视护甲），10 秒内视力 −20%（射击、近战都变差）。', W('MindSpike'), 'Sorcerer', cd=5, rng=24, warm=.5, power=7, dur=600, ext=dict(damage='Psychic', ap=1, hediff2='LP_CS_Slivered'))
S('DragonsBreath', '龙息术', 2, '变化', 'loc', 'CS_Cone', '喷出 6 格锥形的龙息，里面的敌人受 16 × 强度点伤害；元素看血脉（冰 / 水 → 寒冷，风 / 雷 → 闪电，黑 → 强酸，绿 → 毒素，力能龙 → 力场，其他 → 火焰）。升环：每环效果 +25%。', So('DragonWings'), 'Sorcerer', cd=20, rng=8, power=16, erange=6, ext=dict(kind='breath', ap=.2), upcast='power')
S('MetaPierce', '超魔·穿透', 1, '超魔', 'self', 'SelfBuff', '术士 3 级获得：30 秒内放出的下一个法术，控制成功率 +25%（放完就用掉）。', So('TwinSpell'), 'Sorcerer', cd=5, warm=0, dur=1800, hediff='LP_CS_MetaPierce', ext=dict(grant=3, kind='meta'))
S('MetaExtend', '超魔·扩展', 2, '超魔', 'self', 'SelfBuff', '术士 5 级获得：30 秒内放出的下一个法术，持续时间 ×2（放完就用掉）。', So('EmpoweredSpell'), 'Sorcerer', cd=5, warm=0, dur=1800, hediff='LP_CS_MetaExtend', ext=dict(grant=5, kind='meta'))
S('MetaQuicken', '超魔·即时', 3, '超魔', 'self', 'SelfBuff', '术士 9 级获得：30 秒内放出的下一个法术不进冷却（放完就用掉）。', So('SorcerousSurge'), 'Sorcerer', cd=30, warm=0, dur=1800, hediff='LP_CS_MetaQuicken', ext=dict(grant=9, kind='meta'))
S('BloodlineBreath', '血脉龙息', 6, '变化', 'loc', 'CS_Cone', '倾泻 10 格锥形的血脉龙息，里面的敌人受 45 × 强度点伤害；元素看血脉（同龙息术）。', So('DragonWings'), 'Sorcerer', cd=40, rng=12, power=45, erange=10, ext=dict(kind='breath', ap=.3))
S('PsychicBlast', '心灵震爆', 7, '惑控', 'loc', 'CS_Area', '24 格内半径 3 的敌人受 35 × 强度点心灵伤害（无视护甲）并眩晕 2 秒。', So('MassSuggestion'), 'Sorcerer', cd=40, rng=24, power=35, radius=3, ext=dict(damage='Psychic', ap=1, stun=120))
S('ElementalStorm', '元素风暴', 8, '塑能', 'loc', 'CS_Zone', '30 格内半径 5 卷起火、冰、雷交织的风暴 30 秒：里面的敌人每秒受 12 × 强度点随机元素伤害（专注）。', So('IceStorm'), 'Sorcerer', cd=60, rng=30, power=12, radius=5, dur=1800, ext=dict(zone='Elements', ap=.2), conc=True)
S('BloodlineAwakening', '血脉觉醒', 9, '变化', 'self', 'SelfBuff', '血脉彻底觉醒 1 分钟：所有职业法术和能力的伤害、治疗 ×1.5。每天一次。', Lg('BloodlineAwakening'), 'Sorcerer', cd=1000, warm=1, dur=3600, hediff='LP_CS_BloodlineAwakened')

# ==================== 邪术师 ====================
S('ChillTouch', '冻寒之触', 0, '死灵', 'foe', 'CS_Bolt', '鬼手抓住 24 格内一个敌人：8 × 强度点死灵伤害，10 秒内它的伤口不能愈合。', W('VampiricTouch'), 'Warlock', cd=4, rng=24, warm=.5, power=8, dur=600, ext=dict(damage='Necrotic', hediff2='LP_CS_ChillTouched', ap=.3))
S('MinorIllusion', '次级幻象', 0, '幻术', 'loc', 'CS_Util', '在 24 格内一点变出逼真的幻象：半径 4 内的敌人分了神，10 秒内视力 −15%。', Bd('Phantasm'), 'Warlock', cd=10, rng=24, radius=4, dur=600, ext=dict(kind='illusion'))
S('HellishRebuke', '地狱斥责', 1, '塑能', 'self', 'CS_Util', '1 小时内下一次被敌人打伤时，地狱之火立刻反烧对方 15 × 强度点火焰伤害（之后消失）。', Wl('HellishRebuke'), 'Warlock', cd=20, warm=.5, power=15, dur=2500, ext=dict(kind='rebuke'))
S('ArmsOfHadar', '哈达之臂', 1, '咒法', 'self', 'CS_Area', '黑暗触手从身边涌出：2.5 格内的敌人受 12 × 强度点寒冷伤害并僵住 1 秒。升环：每环效果 +25%。', W('BlackTentacles'), 'Warlock', cd=15, warm=.5, power=12, radius=2.5, ext=dict(damage='Frost', stun=60), upcast='power')
S('SpiderClimb', '蛛行术', 2, '变化', 'ally', 'TargetBuff', '10 格内一名友军 1 小时内像蜘蛛一样爬行：不受地形、雪和植物减速，移动 ×1.1。', W('Web'), 'Warlock', cd=20, rng=10, dur=2500, hediff='LP_CS_SpiderClimb', fixed=True)
S('HungerOfHadar', '哈达之饥', 3, '咒法', 'loc', 'CS_Zone', '24 格内半径 3 撕开通往虚空的口子 1 分钟：里面一片漆黑（远程精度大降），敌人每秒受 6 × 强度点寒冷和 3 × 强度点强酸伤害（专注）。', Wl('Darkness'), 'Warlock', cd=30, rng=24, power=6, radius=3, dur=3600, ext=dict(zone='Hunger', damage='Frost', hediff2='LP_WL_Darkened'), conc=True)
S('ContactOtherPlane', '接触异界', 5, '预言', 'self', 'CS_Util', '向异界的存在求知，成功率 55% + 智力调整值 × 5% + (强度 − 1) × 20%：成功就列出 3 天内已经排定的威胁事件，并获得 500 × 强度点研究进度；失败就精神错乱、乱走一阵。每天一次。', Wl('AwakenedMind'), 'Warlock', cd=1000, ext=dict(kind='contact'), ritual=True)
S('Dream', '梦境术', 5, '幻术', 'ally', 'CS_Util', '潜入地图上一名友方的人的梦里：它心情 +8（1 天）。每天一次。', Wl('DarkDelirium'), 'Warlock', cd=1000, rng=80, ext=dict(kind='dream'), ritual=True)
S('Eyebite', '目光术', 6, '死灵', 'self', 'CS_Aura', '双眼化作虚空 1 分钟：每 3 秒注视 12 格内最近的一个敌人，成功率 50% 让它昏睡 10 秒、恐慌逃跑，或者虚弱 30 秒（近战伤害 ×0.5）（专注）。', Wl('DissonantWhispers'), 'Warlock', cd=60, dur=3600, hediff='LP_CS_Eyebite', conc=True)
S('SummonFiend', '魔鬼召唤', 6, '咒法', 'loc', 'CS_Summon', '从九狱召来一只链魔为施法者作战 1 小时（专注）。', Wl('HurlHell'), 'Warlock', cd=60, rng=15, dur=2500, ext=dict(thing='LP_HL_ChainDevil,LP_AH_Hezrou'), conc=True, hostile=False)
S('PowerWordStun', '强力震慑', 8, '惑控', 'foe', 'CS_Hex', '对 24 格内一个敌人说出震慑真言：伤势过半（健康 ≤ 60%）的敌人眩晕 8 秒；还没受多少伤的不受影响。', W('Feeblemind'), 'Warlock', cd=60, rng=24, ext=dict(kind='stunWord', stun=480))

# ==================== 1.131.0 吟游诗人 ====================
V2 = '1.131'
S('Friends', '友善术', 0, '惑控', 'self', 'SelfBuff', '1 分钟内社交影响 +30%、谈判能力 +30%（招募、交易、劝说都更容易）。', Bd('SilverTongue'), 'Bard', cd=60, warm=.5, dur=3600, hediff='LP_CS_Friends', ver=V2)
S('Heroism', '英雄气概', 1, '惑控', 'ally', 'CS_Buff', '20 格内一名友军 1 分钟内无所畏惧（陷入恐慌就立刻恢复），并且每秒把临时生命补到 6 × 强度点（专注）。', Bd('ValorAnthem'), 'Bard', cd=30, power=6, dur=3600, hediff='LP_CS_Heroism', fixed=True, conc=True, ver=V2)
S('Tongues', '言语术', 3, '预言', 'ally', 'TargetBuff', '10 格内一名友方的人 1 小时内能说会道：谈判能力 ×2、社交影响 +50%（趁这时去招募、劝降、交易）。', Bd('UniversalSpeech'), 'Bard', cd=60, rng=10, dur=2500, hediff='LP_CS_Tongues', fixed=True, ver=V2)
S('HypnoticPattern', '催眠图纹', 3, '幻术', 'loc', 'CS_Hex', '20 格内半径 3 浮现出变幻的光纹：里面的敌人各自成功率 55% 看呆了，昏睡 12 秒（受伤就醒）。目标每比施法者高 1 级 −5%（最低 10%），传奇生物和剧情首领减半，心智抗性也能抵抗，对机械体无效（专注）。', Bd('Phantasm'), 'Bard', cd=40, radius=3, dur=720, ext=dict(chance=.55, mind=True, hediff2='LP_MagicSleep'), conc=True, ver=V2)
S('Compulsion', '迷惑术', 4, '惑控', 'loc', 'CS_Hex', '20 格内半径 4 的敌人各自成功率 55% 被迫走开 5 格、愣住 1.5 秒（心智抗性能抵抗，对机械体无效）。', Bd('Confusion'), 'Bard', cd=40, radius=4, ext=dict(kind='compel', chance=.55, mind=True), ver=V2)
S('GreaterInvisibility', '高等隐形术', 4, '幻术', 'ally', 'CS_Buff', '20 格内一名友军隐形 1 分钟，攻击、施法也不会现形（专注）。', W('Invisibility'), 'Bard', cd=60, dur=3600, hediff='LP_CS_GreaterInvisible', fixed=True, conc=True, ver=V2)
S('MassSuggestion', '群体暗示·退兵', 6, '惑控', 'foe', 'CS_Util', '对 30 格内一支敌对小队里的一个人施法，成功率按停战之言算（强度越高、对方倒下的人越多越容易）：成功时整支小队放弃袭击离开地图。亡灵、异界来客、机械族、虫族和剧情首领带的队伍无效。', Bd('Truce'), 'Bard', cd=120, rng=30, ext=dict(kind='suggest'), ver=V2)
S('OttoDance', '奥图狂舞', 6, '惑控', 'foe', 'CS_Util', '30 格内一个敌人跳起停不下来的舞，10 秒内动弹不得（成功率同刃舞的狂舞，剧情首领时间更短；对机械体无效）。', Bd('BladeWhirl'), 'Bard', cd=60, rng=30, dur=600, ext=dict(kind='dance'), ver=V2)
S('ProjectImage', '投射幻象', 7, '幻术', 'loc', 'CS_Util', '在地图上任意一处投下自己的幻象：揭开那里半径 12 的迷雾；1 小时内射向施法者的远程攻击成功率 30% 落空（打在幻象上）。', Bd('Mislead'), 'Bard', cd=120, rng=500, dur=2500, ext=dict(kind='project'), approach=False, hostile=False, ver=V2)
S('PowerWordHeal', '强力治愈', 9, '塑能', 'ally', 'CS_Heal', '10 格内一名友军的伤势全部治好（不含旧伤疤），并解除眩晕、恐惧、各种控制、失明和中毒。', Bd('WordsOfCreation'), 'Bard', cd=120, rng=10, power=100, ext=dict(kind='power'), ver=V2)
# ==================== 1.131.0 圣武士 ====================
S('SmiteBlaze', '炽焰斩', 1, '塑能', 'self', 'CS_Imbue', '10 秒内下一次近战命中额外造成 10 × 强度点火焰伤害并把目标点着。升环：每环效果 +25%。', Pa('DivineSmite'), 'Paladin', cd=6, warm=0, power=10, dur=600, hediff='LP_CS_SmiteBlaze', upcast='power', ver=V2)
S('SmiteThunder', '雷鸣斩', 1, '塑能', 'self', 'CS_Imbue', '10 秒内下一次近战命中额外造成 10 × 强度点雷鸣（钝器）伤害，把目标震退 2 格、愣住 1 秒。升环：每环效果 +25%。', Pa('DivineSmite'), 'Paladin', cd=6, warm=0, power=10, dur=600, hediff='LP_CS_SmiteThunder', upcast='power', ver=V2)
S('SmiteSear', '灼热斩', 1, '塑能', 'self', 'CS_Imbue', '10 秒内下一次近战命中额外造成 6 × 强度点火焰伤害，之后 10 秒目标每秒再烧 1.2 × 强度点。升环：每环效果 +25%。', Pa('DivineSmite'), 'Paladin', cd=6, warm=0, power=6, dur=600, hediff='LP_CS_SmiteSear', upcast='power', ver=V2)
S('DetectEvil', '侦测邪恶', 1, '预言', 'self', 'CS_Util', '感知地图上所有亡灵、邪魔和异常生物（来信列出位置），并揭开 30 格内隐形的敌人。', Pa('DispelEvil'), 'Paladin', cd=60, radius=30, ext=dict(kind='detectEvil'), ver=V2)
S('MagicWeapon', '魔法武器', 2, '变化', 'ally', 'CS_Imbue', '20 格内一名友军的武器附上魔力 1 小时：每次近战或远程命中额外 3 × 强度点力场伤害（专注）。', Pa('SacredWeapon'), 'Paladin', cd=30, power=3, dur=2500, hediff='LP_CS_MagicWeapon', conc=True, ver=V2)
S('FindSteed', '寻获坐骑', 2, '咒法', 'self', 'CS_Util', '召来一匹持续存在的天界战马（原版马，属于殖民地、和施法者建立羁绊、学会能学的训练，移动 ×1.2、受到的伤害 ×0.8）。同时只能有一匹；死了可以重召。不能骑乘。', Pa('HolyAvatar'), 'Paladin', cd=1000, warm=3, ext=dict(kind='steed'), ver=V2)
S('SmiteBrand', '标记惩击', 2, '塑能', 'self', 'CS_Imbue', '10 秒内下一次命中（近战或远程）额外造成 10 × 强度点光耀伤害（对亡灵、邪魔加倍）；目标 1 分钟内不能隐形。升环：每环效果 +25%。', Pa('VowOfEnmity'), 'Paladin', cd=6, warm=0, power=10, dur=600, hediff='LP_CS_SmiteBrand', upcast='power', ver=V2)
S('CrusadersMantle', '十字军披风', 3, '塑能', 'self', 'CS_Aura', '1 分钟内施法者身边 6 格内的友军每次近战命中额外 3 × 强度点光耀伤害（对亡灵、邪魔加倍）（专注）。', Pa('AuraOfCourage'), 'Paladin', cd=60, power=3, radius=6, dur=3600, hediff='LP_CS_Crusader', conc=True, ver=V2)
S('ElementalWeapon', '元素武器', 3, '变化', 'ally', 'CS_Imbue', '20 格内一名友军的武器燃起元素之力 1 小时：每次近战或远程命中额外 5 × 强度点火焰伤害（专注）。', So('BurningHands'), 'Paladin', cd=30, power=5, dur=2500, hediff='LP_CS_ElementalWeapon', conc=True, ver=V2)
S('SmiteBlind', '致盲斩', 3, '塑能', 'self', 'CS_Imbue', '10 秒内下一次近战命中额外造成 14 × 强度点光耀伤害（对亡灵、邪魔加倍），目标失明 10 秒。升环：每环效果 +25%。', Pa('DivineSmite'), 'Paladin', cd=10, warm=0, power=14, dur=600, hediff='LP_CS_SmiteBlind', upcast='power', ver=V2)
S('SmiteStagger', '灼目斩', 4, '惑控', 'self', 'CS_Imbue', '10 秒内下一次近战命中额外造成 18 × 强度点心灵伤害，目标眩晕 3 秒。升环：每环效果 +25%。', Pa('DivineSmite'), 'Paladin', cd=10, warm=0, power=18, dur=600, hediff='LP_CS_SmiteStagger', upcast='power', ver=V2)
S('SmiteBanish', '逐恶斩', 5, '咒法', 'self', 'CS_Imbue', '10 秒内下一次近战命中额外造成 25 × 强度点力场伤害；目标伤势过半（健康 ≤ 50%）时被放逐 1 分钟（剧情首领除外）。', Pa('DispelEvil'), 'Paladin', cd=20, warm=0, power=25, dur=600, hediff='LP_CS_SmiteBanish', ver=V2)
# ==================== 1.131.0 游侠 ====================
S('Ensnaring', '诱捕打击', 1, '咒法', 'self', 'CS_Imbue', '10 秒内下一次命中（近战或远程）额外造成 6 × 强度点割伤，并成功率 60% 让荆棘缠住目标 4 秒。', Dr('Entangle'), 'Ranger', cd=6, warm=0, power=6, dur=600, hediff='LP_CS_Ensnaring', ver=V2)
S('PassWithoutTrace', '无踪步', 2, '防护', 'self', 'CS_Buff', '施法者和身边 8 格内最多 9 名友军 1 小时内悄无声息：射向他们的远程攻击成功率 30% 落空（专注）。商队里的游侠另有商队按钮：1 天内商队遇袭的几率减半。', Ra('HideInPlain'), 'Ranger', cd=60, radius=8, dur=2500, hediff='LP_CS_PassTrace', fixed=True, ext=dict(count=10), conc=True, ver=V2)
S('ConjureBarrage', '召唤箭雨', 3, '咒法', 'loc', 'CS_Cone', '朝一个方向射出 8 格锥形的箭雨，里面的敌人受 20 × 强度点穿刺伤害。', Ra('Volley'), 'Ranger', cd=30, rng=10, power=20, erange=8, ext=dict(damage='Arrow', ap=.3), ver=V2)
S('LightningArrow', '闪电箭', 3, '变化', 'self', 'CS_Imbue', '10 秒内下一次远程命中化作一道闪电：额外 20 × 强度点闪电伤害，目标身边 2 格内的敌人各受一半。', Ra('Volley'), 'Ranger', cd=10, warm=0, power=20, dur=600, hediff='LP_CS_LightningArrow', ver=V2)
S('SwiftQuiver', '迅捷箭袋', 5, '变化', 'self', 'CS_Buff', '1 分钟内远程攻击的冷却和瞄准时间减半（专注）。', Ra('FoeSlayer'), 'Ranger', cd=60, warm=.5, dur=3600, hediff='LP_CS_SwiftQuiver', fixed=True, conc=True, ver=V2)
S('ConjureVolley', '召唤箭雨群', 5, '咒法', 'loc', 'CS_Area', '向 40 格内一点射出漫天箭雨：半径 4 内的敌人受 30 × 强度点穿刺伤害。', Lg('ArrowStorm'), 'Ranger', cd=60, rng=40, warm=1.5, power=30, radius=4, ext=dict(damage='Arrow', ap=.3), ver=V2)
# ==================== 1.131.0 器械师 ====================
S('AbsorbElements', '吸收元素', 1, '防护', 'self', 'CS_Imbue', '10 秒内下一次受到的元素伤害（火焰、寒冷、闪电、强酸、心灵）减半；挡下之后，下一次近战命中额外 8 × 强度点火焰伤害。', W('ProtectionFromEnergy'), 'Artificer', cd=10, warm=0, power=8, dur=600, hediff='LP_CS_AbsorbElements', ver=V2)
S('Identify', '识别术', 1, '预言', 'item', 'CS_Util', '说出 10 格内一件物品的品质、附魔强化等级、有没有诅咒、有没有魔法灵光和价值。', W('DetectMagic'), 'Artificer', cd=10, rng=10, ext=dict(kind='identify'), ritual=True, ver=V2)
S('HeatMetal', '加热金属', 2, '变化', 'foe', 'CS_Bolt', '把 20 格内一个敌人身上的金属烧红：6 × 强度点火焰伤害（穿金属护甲的翻倍），之后 10 秒内穿金属护甲的每秒再受 3 × 强度点火焰伤害。', W('WallOfFire'), 'Artificer', cd=20, power=6, dur=600, ext=dict(damage='Flame', vs='Metal', hediff2='LP_CS_HeatMetal', ap=.5), ver=V2)
S('Levitate', '浮空术', 2, '变化', 'ally', 'TargetBuff', '10 格内一名友军 1 小时内浮在离地一尺的空中：不受地形、雪和植物减速，移动 ×0.9。', W('Fly'), 'Artificer', cd=20, rng=10, dur=2500, hediff='LP_CS_Levitating', fixed=True, ver=V2)
S('Creation', '造物术', 5, '幻术', 'self', 'CS_Util', '凭空造出一种普通材料：器械师等级 × 10 份钢铁、木材、布料或花岗岩砖块，或者等级 / 2 个零部件。每天一次。', Ar('MasterCraft'), 'Artificer', cd=1000, warm=2, ext=dict(kind='creation'), ritual=True, ver=V2)
S('AnimateObjects', '活化物件', 5, '变化', 'loc', 'CS_Util', '让 15 格内一处的石块活过来：召出 1 个活化石像为施法者作战 1 分钟（专注），时间到就散架。', Ar('RuneTurret'), 'Artificer', cd=60, rng=15, dur=3600, ext=dict(kind='animate'), conc=True, hostile=False, ver=V2)
S('BigbysHand', '巨人之手', 5, '塑能', 'foe', 'CS_Bolt', '召出一只巨大的力场之手：24 格内一个敌人受 25 × 强度点力场伤害，被推开 3 格并愣住 2 秒。', Ar('ArcaneResonance'), 'Artificer', cd=30, rng=24, power=25, ext=dict(damage='Force', push=True, stun=120, ap=.3), ver=V2)
# ==================== 法术表 ====================
LISTS = {}
LISTS['Cleric'] = dict(classDef='LP_Class_Cleric', ability='Wis', caster='full', prepared=True, stats=['LP_DivinePower'], icon=Cl('Bless'), order=1,
    extra=['LP_WS_Light', 'LP_WS_Mending', 'LP_WS_DispelMagic', 'LP_WS_Banishment', 'LP_WS_TrueSeeing', 'LP_WS_PlaneShift', 'LP_WS_Gate', 'LP_CS_FindTraps', 'LP_CS_StoneShape', 'LP_CS_Earthquake'])
LISTS['Druid'] = dict(classDef='LP_Class_Druid', ability='Wis', caster='full', prepared=True, stats=['LP_PrimalPower', 'LP_VerdantPower'], icon=Dr('WildShape'), order=2,
    extra=['LP_WS_PoisonSpray', 'LP_WS_Mending', 'LP_CS_CreateWater', 'LP_CS_PurifyFood', 'LP_CS_HealingWord', 'LP_WS_Thunderwave', 'LP_WS_FogCloud', 'LP_CureWounds=1',
           'LP_WS_EnhanceAbility', 'LP_LesserRestoration=2', 'LP_CS_CreateFood', 'LP_CS_Daylight', 'LP_WS_DispelMagic', 'LP_WS_ProtectionFromEnergy',
           'LP_CS_FreedomOfMovement', 'LP_WS_Polymorph', 'LP_IceStorm=4', 'LP_WS_Stoneskin', 'LP_WS_Blight', 'LP_WS_Confusion',
           'LP_CS_GreaterRestoration', 'LP_MassHeal=5', 'LP_WallOfStone=5', 'LP_WS_ConjureElemental=6', 'LP_WS_Sunbeam', 'LP_CS_Heal', 'LP_WS_MoveEarth', 'LP_CS_HeroesFeast',
           'LP_WS_ReverseGravity', 'LP_WS_PlaneShift', 'LP_WS_Sunburst', 'LP_WS_Antipathy', 'LP_WS_Feeblemind', 'LP_WS_Foresight'])
LISTS['Sorcerer'] = dict(classDef='LP_Class_Sorcerer', ability='Cha', caster='full', prepared=False, stats=['LP_InnatePower'], icon=So('ChaosBolt'), order=3,
    extra=['LP_WS_FireBolt', 'LP_WS_RayOfFrost', 'LP_WS_ShockingGrasp', 'LP_WS_PoisonSpray', 'LP_WS_Light', 'LP_WS_MageHand', 'LP_WS_Mending', 'LP_WS_Prestidigitation',
           'LP_ShieldSpell', 'LP_Sleep', 'LP_MageArmor', 'LP_WS_FogCloud', 'LP_WS_Thunderwave', 'LP_WS_FalseLife', 'LP_WS_ComprehendLanguages', 'LP_WS_DetectMagic', 'LP_CS_Jump',
           'LP_MistyStep', 'LP_MirrorImage', 'LP_WS_ScorchingRay', 'LP_WS_Shatter', 'LP_WS_Web', 'LP_WS_HoldPerson', 'LP_WS_Invisibility', 'LP_WS_Darkness', 'LP_WS_MindSpike', 'LP_WS_EnhanceAbility', 'LP_WS_Knock',
           'LP_Fireball', 'LP_LightningBolt', 'LP_Haste', 'LP_WS_Counterspell', 'LP_WS_DispelMagic', 'LP_WS_Fear', 'LP_WS_Fly', 'LP_WS_Slow', 'LP_WS_StinkingCloud', 'LP_WS_Blink', 'LP_WS_ProtectionFromEnergy',
           'LP_WS_Polymorph', 'LP_WS_Banishment', 'LP_WS_Stoneskin', 'LP_WS_WallOfFire', 'LP_WS_Confusion', 'LP_WS_Blight', 'LP_WS_DimensionDoor=4', 'LP_CS_DominateBeast',
           'LP_WS_ConeOfCold', 'LP_WS_DominatePerson', 'LP_WS_HoldMonster', 'LP_WS_Telekinesis', 'LP_WallOfStone', 'LP_WS_TeleportationCircle',
           'LP_WS_ChainLightning', 'LP_WS_CircleOfDeath', 'LP_Disintegrate', 'LP_WS_FleshToStone', 'LP_WS_MoveEarth', 'LP_WS_Sunbeam', 'LP_WS_TrueSeeing',
           'LP_WS_DelayedBlastFireball', 'LP_WS_FingerOfDeath', 'LP_WS_PlaneShift', 'LP_WS_ReverseGravity', 'LP_Teleport', 'LP_WS_Etherealness',
           'LP_WS_DominateMonster', 'LP_WS_Sunburst', 'LP_CS_Earthquake', 'LP_CS_PowerWordStun',
           'LP_WS_Gate', 'LP_WS_MassPolymorph', 'LP_WS_PowerWordKill', 'LP_WS_TimeStop', 'LP_WS_Wish'])
LISTS['Warlock'] = dict(classDef='LP_Class_Warlock', ability='Cha', caster='pact', prepared=False, stats=['LP_PactPower'], icon=Wl('EldritchBlast'), order=4,
    extra=['LP_WS_PoisonSpray', 'LP_WS_Prestidigitation', 'LP_CS_TollTheDead', 'LP_CS_MindSliver', 'LP_WS_MageHand',
           'LP_CharmPerson=1', 'LP_WS_ComprehendLanguages', 'LP_WS_Invisibility', 'LP_MistyStep', 'LP_MirrorImage', 'LP_WS_HoldPerson', 'LP_WS_Shatter',
           'LP_WS_VampiricTouch', 'LP_WS_Fear', 'LP_WS_Fly', 'LP_WS_Counterspell', 'LP_WS_DispelMagic', 'LP_CS_RemoveCurse',
           'LP_WS_Blight', 'LP_WS_Banishment', 'LP_WS_DimensionDoor=4', 'LP_WS_HoldMonster',
           'LP_WS_TrueSeeing', 'LP_WS_CircleOfDeath', 'LP_WS_ForceCage=7', 'LP_WS_Etherealness', 'LP_WS_PlaneShift', 'LP_WS_FingerOfDeath',
           'LP_WS_DominateMonster', 'LP_WS_Feeblemind', 'LP_WS_Foresight', 'LP_WS_MassPolymorph', 'LP_WS_Maze=9'])
LISTS['Bard'] = dict(classDef='LP_Class_Bard', ability='Cha', caster='full', prepared=False, stats=['LP_BardicPower'], icon=Bd('MagicalSecrets'), order=5, secrets=[10, 14, 18],
    extra=['LP_CS_MinorIllusion', 'LP_WS_Prestidigitation', 'LP_WS_MageHand', 'LP_WS_Light', 'LP_WS_Mending',
           'LP_CureWounds=1', 'LP_Sleep', 'LP_WS_Thunderwave', 'LP_CharmPerson=1', 'LP_WS_ComprehendLanguages', 'LP_CS_Command',
           'LP_WS_HoldPerson', 'LP_CS_Silence', 'LP_WS_EnhanceAbility', 'LP_LesserRestoration=2', 'LP_WS_Invisibility', 'LP_CS_AnimalMessenger', 'LP_WS_Shatter', 'LP_WS_Knock',
           'LP_WS_Fear', 'LP_WS_Sending', 'LP_WS_DispelMagic', 'LP_CS_MassHealingWord',
           'LP_WS_Polymorph', 'LP_CS_FreedomOfMovement', 'LP_WS_DimensionDoor=4', 'LP_WS_PhantasmalKiller',
           'LP_MassHeal=5', 'LP_WS_DominatePerson', 'LP_CS_Awaken', 'LP_CS_GreaterRestoration', 'LP_CS_RaiseDead', 'LP_WS_HoldMonster', 'LP_WS_ModifyMemory', 'LP_WS_Scrying', 'LP_WS_TeleportationCircle',
           'LP_CS_Eyebite', 'LP_WS_TrueSeeing', 'LP_CS_Resurrection', 'LP_WS_Etherealness', 'LP_Teleport', 'LP_WS_MagnificentMansion',
           'LP_WS_Feeblemind', 'LP_CS_PowerWordStun', 'LP_WS_DominateMonster', 'LP_WS_MindBlank', 'LP_WS_Foresight', 'LP_WS_PowerWordKill'])
LISTS['Paladin'] = dict(classDef='LP_Class_Paladin', ability='Cha', caster='half', prepared=True, cantrips=False, stats=['LP_DivinePower'], icon=Pa('DivineSmite'), order=6,
    extra=['LP_Bless=1', 'LP_CS_Heroism', 'LP_CureWounds=1', 'LP_CS_ShieldOfFaith', 'LP_CS_Command', 'LP_CS_PurifyFood',
           'LP_CS_Aid', 'LP_LesserRestoration=2', 'LP_CS_Revivify', 'LP_WS_DispelMagic', 'LP_CS_CreateFood', 'LP_CS_RemoveCurse', 'LP_CS_Daylight',
           'LP_DeathWard=4', 'LP_WS_Banishment', 'LP_CS_GreaterRestoration', 'LP_CS_RaiseDead'])
LISTS['Ranger'] = dict(classDef='LP_Class_Ranger', ability='Wis', caster='half', prepared=False, cantrips=False, stats=['LP_HuntPower'], icon=Ra('HuntersMark'), order=7,
    extra=['LP_CS_Goodberry', 'LP_Entangle=1', 'LP_WS_FogCloud', 'LP_CS_Jump', 'LP_AnimalFriendship=1', 'LP_WS_Alarm', 'LP_CureWounds=1',
           'LP_CS_SpikeGrowth', 'LP_Barkskin=2', 'LP_CS_FindTraps', 'LP_LesserRestoration=2', 'LP_CS_Silence', 'LP_CS_AnimalMessenger',
           'LP_PlantGrowth=3', 'LP_ConjureAnimals=3', 'LP_CS_WindWall', 'LP_WS_ProtectionFromEnergy', 'LP_CS_Daylight',
           'LP_WS_Stoneskin', 'LP_CS_FreedomOfMovement', 'LP_CS_ConjureWoodland', 'LP_CS_DominateBeast', 'LP_CS_TreeStride', 'LP_CS_GreaterRestoration'])
LISTS['Artificer'] = dict(classDef='LP_Class_Artificer', ability='Int', caster='artificer', prepared=True, stats=['LP_ArtificePower'], icon=Ar('ArcaneWorkshop'), order=8,
    extra=['LP_WS_Mending', 'LP_WS_FireBolt', 'LP_WS_ShockingGrasp', 'LP_WS_PoisonSpray', 'LP_WS_Prestidigitation', 'LP_WS_Light', 'LP_WS_MageHand',
           'LP_CureWounds=1', 'LP_WS_Grease', 'LP_CS_PurifyFood', 'LP_WS_Alarm', 'LP_WS_FalseLife', 'LP_CS_Jump', 'LP_WS_DetectMagic',
           'LP_CS_MagicWeapon', 'LP_WS_EnhanceAbility', 'LP_LesserRestoration=2', 'LP_WS_ArcaneLock', 'LP_WS_Web', 'LP_WS_Invisibility',
           'LP_WS_DispelMagic', 'LP_Haste', 'LP_WS_Fly', 'LP_CS_ElementalWeapon', 'LP_WS_ProtectionFromEnergy', 'LP_CS_Revivify', 'LP_CS_CreateFood', 'LP_WS_Blink',
           'LP_WS_Stoneskin', 'LP_CS_FreedomOfMovement', 'LP_CS_StoneShape', 'LP_WS_FaithfulHound', 'LP_WS_Fabricate',
           'LP_CS_GreaterRestoration', 'LP_WallOfStone'])
CLSLABEL = {'Cleric': '牧师', 'Druid': '德鲁伊', 'Sorcerer': '术士', 'Warlock': '邪术师', 'Bard': '吟游诗人', 'Paladin': '圣武士', 'Ranger': '游侠', 'Artificer': '器械师'}
LIST_VER = {'Cleric': '1.130', 'Druid': '1.130', 'Sorcerer': '1.130', 'Warlock': '1.130', 'Bard': '1.131', 'Paladin': '1.131', 'Ranger': '1.131', 'Artificer': '1.131'}

# ==================== 状态 / 思想 / 物品 ====================
HEDIFFS = [  # (defName, label, desc, bad, cls, stages-xml, extra)
 ('LP_CS_Resistance', '抗性术', '受到的伤害 ×0.85。', False, None, '<stages><li><statFactors><IncomingDamageFactor>0.85</IncomingDamageFactor></statFactors></li></stages>', ''),
 ('LP_CS_ShieldOfFaith', '虔诚护盾', '闪光的护盾：利器与钝器护甲 +20%。', False, None, '<stages><li><statOffsets><ArmorRating_Sharp>0.2</ArmorRating_Sharp><ArmorRating_Blunt>0.2</ArmorRating_Blunt></statOffsets></li></stages>', ''),
 ('LP_CS_Outlined', '指引之光', '被指引之箭的光芒标记：下一次被敌人攻击时伤害 ×1.3。', True, 'CS', '', ''),
 ('LP_CS_TempHP', '临时生命', '受到伤害时先扣临时生命。', False, 'CS', '', ''),
 ('LP_CS_SpiritWeapon', '灵体武器', '一把光耀之锤飘在身边，每 2 秒敲打最近的敌人。', False, 'CS', '', ''),
 ('LP_CS_SpiritGuardians', '守护灵', '守护灵绕身飞舞：身边的敌人每秒受光耀伤害、行动迟缓。', False, 'CS', '', ''),
 ('LP_CS_SpiritSlowed', '守护灵缠绕', '被守护灵缠住：移动 ×0.5。', True, None, '<stages><li><statFactors><MoveSpeed>0.5</MoveSpeed></statFactors></li></stages>', ''),
 ('LP_CS_Silenced', '静默', '身处静默术里：念不出咒语，放不了职业法术。', True, 'CS', '', ''),
 ('LP_CS_Freedom', '自由行动', '不受减速、束缚和眩晕。', False, 'CS', '', ''),
 ('LP_CS_HeroesFeast', '英雄宴', '吃过神赐的宴席：毒素抗性 +100%，精神崩溃阈值 −15%。', False, None, '<stages><li><statOffsets><ToxicResistance>1</ToxicResistance><MentalBreakThreshold>-0.15</MentalBreakThreshold></statOffsets></li></stages>', ''),
 ('LP_CS_HolyAura', '神圣光环', '圣光笼罩：受到的伤害 ×0.7。', False, None, '<stages><li><statFactors><IncomingDamageFactor>0.7</IncomingDamageFactor></statFactors></li></stages>', ''),
 ('LP_CS_Shillelagh', '橡棍术', '武器注入自然之力：近战伤害 ×1.4。', False, None, '<stages><li><statFactors><MeleeDamageFactor>1.4</MeleeDamageFactor></statFactors></li></stages>', ''),
 ('LP_CS_Jump', '跳跃术', '跑跳如飞：移动 ×1.3。', False, None, '<stages><li><statFactors><MoveSpeed>1.3</MoveSpeed></statFactors></li></stages>', ''),
 ('LP_CS_SpikeSlowed', '刺足', '脚下全是尖刺 / 被虫群包围：移动 ×0.5。', True, None, '<stages><li><statFactors><MoveSpeed>0.5</MoveSpeed></statFactors></li></stages>', ''),
 ('LP_CS_WindWard', '风墙庇护', '靠着风墙：受到的远程伤害减半。', False, 'CS', '', ''),
 ('LP_CS_WindWalk', '风行术', '化身清风：移动 ×2。', False, None, '<stages><li><statFactors><MoveSpeed>2</MoveSpeed></statFactors></li></stages>', ''),
 ('LP_CS_Slivered', '心灵之楔', '脑子里扎着一根心灵之楔：视力 −20%。', True, None, '<stages><li><capMods><li><capacity>Sight</capacity><offset>-0.2</offset></li></capMods></li></stages>', ''),
 ('LP_CS_MetaPierce', '超魔·穿透', '下一个法术的控制成功率 +25%。', False, 'CS', '', ''),
 ('LP_CS_MetaExtend', '超魔·扩展', '下一个法术的持续时间 ×2。', False, 'CS', '', ''),
 ('LP_CS_MetaQuicken', '超魔·即时', '下一个法术不进冷却。', False, 'CS', '', ''),
 ('LP_CS_BloodlineAwakened', '血脉觉醒', '血脉彻底觉醒：职业法术和能力的伤害、治疗 ×1.5。', False, 'CS', '', ''),
 ('LP_CS_ChillTouched', '冻寒之触', '鬼手的寒意：伤口不能愈合。', True, None, '<stages><li><statFactors><InjuryHealingFactor>0</InjuryHealingFactor></statFactors></li></stages>', ''),
 ('LP_CS_Distracted', '分神', '被幻象吸引了注意：视力 −15%。', True, None, '<stages><li><capMods><li><capacity>Sight</capacity><offset>-0.15</offset></li></capMods></li></stages>', ''),
 ('LP_CS_Rebuke', '地狱斥责', '下一次被敌人打伤时，地狱之火反烧对方。', False, 'CS', '', ''),
 ('LP_CS_SpiderClimb', '蛛行术', '像蜘蛛一样爬行：不受地形减速，移动 ×1.1。', False, 'CS', '<stages><li><statFactors><MoveSpeed>1.1</MoveSpeed></statFactors></li></stages>', ''),
 ('LP_CS_Eyebite', '目光术', '双眼化作虚空，注视身边的敌人。', False, 'CS', '', ''),
 ('LP_CS_Weakened', '虚弱', '被目光术吸走了力气：近战伤害 ×0.5。', True, None, '<stages><li><statFactors><MeleeDamageFactor>0.5</MeleeDamageFactor></statFactors></li></stages>', ''),
 ('LP_CS_Goodberry', '良莓', '吃了良莓：几秒内伤势慢慢愈合。', False, 'CS', '', '300'),
]
HEDIFFS2 = [
 ('LP_CS_Friends', '友善术', '待人格外亲切：社交影响 +30%，谈判能力 +30%。', False, None, '<stages><li><statOffsets><SocialImpact>0.3</SocialImpact><NegotiationAbility>0.3</NegotiationAbility></statOffsets></li></stages>', ''),
 ('LP_CS_Heroism', '英雄气概', '无所畏惧，临时生命不断补满。', False, 'CS', '', ''),
 ('LP_CS_Tongues', '言语术', '能说会道：谈判能力 ×2，社交影响 +50%。', False, None, '<stages><li><statFactors><NegotiationAbility>2</NegotiationAbility></statFactors><statOffsets><SocialImpact>0.5</SocialImpact></statOffsets></li></stages>', ''),
 ('LP_CS_GreaterInvisible', '高等隐形术', '身形完全消失，攻击、施法也不会现形。', False, 'CS', '', 'INVIS'),
 ('LP_CS_PassTrace', '无踪', '悄无声息：射来的远程攻击 30% 落空。', False, 'CS', '', ''),
 ('LP_CS_Projected', '投射幻象', '远处有一个自己的幻象：射来的远程攻击 30% 落空。', False, 'CS', '', ''),
 ('LP_CS_SwiftQuiver', '迅捷箭袋', '箭袋里的箭源源不断：远程冷却与瞄准时间减半。', False, None, '<stages><li><statFactors><RangedCooldownFactor>0.5</RangedCooldownFactor><AimingDelayFactor>0.5</AimingDelayFactor></statFactors></li></stages>', ''),
 ('LP_CS_HeatMetal', '加热金属', '身上的金属烧得通红：穿金属护甲时每秒受火焰伤害。', True, 'CS', '', ''),
 ('LP_CS_Levitating', '浮空术', '浮在离地一尺的空中：不受地形减速，移动 ×0.9。', False, 'CS', '<stages><li><statFactors><MoveSpeed>0.9</MoveSpeed></statFactors></li></stages>', ''),
 ('LP_CS_SmiteBlaze', '炽焰斩', '下一次近战命中附加火焰伤害并点燃目标。', False, 'CS', '', ''),
 ('LP_CS_SmiteThunder', '雷鸣斩', '下一次近战命中附加雷鸣伤害并震退目标。', False, 'CS', '', ''),
 ('LP_CS_SmiteSear', '灼热斩', '下一次近战命中附加火焰伤害，目标持续灼烧。', False, 'CS', '', ''),
 ('LP_CS_SmiteBrand', '标记惩击', '下一次命中附加光耀伤害，目标不能隐形。', False, 'CS', '', ''),
 ('LP_CS_SmiteBlind', '致盲斩', '下一次近战命中附加光耀伤害并让目标失明。', False, 'CS', '', ''),
 ('LP_CS_SmiteStagger', '灼目斩', '下一次近战命中附加心灵伤害并让目标眩晕。', False, 'CS', '', ''),
 ('LP_CS_SmiteBanish', '逐恶斩', '下一次近战命中附加力场伤害，重伤的目标被放逐。', False, 'CS', '', ''),
 ('LP_CS_MagicWeapon', '魔法武器', '武器附着魔力：每次命中附加力场伤害。', False, 'CS', '', ''),
 ('LP_CS_ElementalWeapon', '元素武器', '武器燃着元素之力：每次命中附加火焰伤害。', False, 'CS', '', ''),
 ('LP_CS_Crusader', '十字军披风', '身边的友军近战命中附加光耀伤害。', False, 'CS', '', ''),
 ('LP_CS_CrusaderBuff', '十字军之力', '近战命中附加光耀伤害。', False, 'CS', '', ''),
 ('LP_CS_AbsorbElements', '吸收元素', '下一次受到的元素伤害减半。', False, 'CS', '', ''),
 ('LP_CS_AbsorbCharge', '元素蓄能', '吸收的元素之力：下一次近战命中附加火焰伤害。', False, 'CS', '', ''),
 ('LP_CS_Ensnaring', '诱捕打击', '下一次命中附加割伤，荆棘可能缠住目标。', False, 'CS', '', ''),
 ('LP_CS_LightningArrow', '闪电箭', '下一次远程命中化作闪电。', False, 'CS', '', ''),
 ('LP_CS_Seared', '灼烧', '被灼热斩点着：每秒受火焰伤害。', True, 'CS', '', ''),
 ('LP_CS_Branded', '光耀标记', '身上有光耀的烙印：不能隐形。', True, 'CS', '', ''),
]
PERMANENT = [('LP_CS_Awakened', '唤醒', '被唤醒术唤醒了心智：能学的训练都会了，永远不会遗忘。', 'CS', '', '1.130'), ('LP_CS_Steed', '天界战马', '寻获坐骑召来的天界战马：移动 ×1.2，受到的伤害 ×0.8。', 'CS', '<stages><li><statFactors><MoveSpeed>1.2</MoveSpeed><IncomingDamageFactor>0.8</IncomingDamageFactor></statFactors></li></stages>', '1.131')]
THOUGHTS = [('LP_CS_SweetDream', '美梦', '昨晚做了一个很美的梦，醒来心里暖洋洋的。', 8), ('LP_CS_FeastThought', '英雄宴', '吃了一桌神赐的宴席，浑身是劲。', 10)]

def fmt_cd(sec):
    if sec >= 1000 and sec % 1000 == 0:
        return '%d 天' % (sec // 1000)
    if sec >= 60:
        return ('%g 分钟' % (sec / 60.0))
    return '%g 秒' % sec

def ticks_cd(sec):
    return sec // 1000 * 60000 if sec >= 1000 and sec % 1000 == 0 else int(sec * 60)

def spell_xml(s):
    cls = s['cls']; stat = STAT[cls]; c = s['circle']
    cost = s['cost'] if s['cost'] is not None else MANA[c]
    if s['ext'].get('kind') == 'meta':
        cost = MANA[c]
    t = s['target']
    parent = {'foe': 'LP_ClassMeleeBase', 'ally': 'LP_ClassAllyBase', 'loc': 'LP_ClassLocBase', 'self': 'LP_ClassSelfBase', 'corpse': 'LP_ClassAbilityBase', 'item': 'LP_ClassAbilityBase'}[t]
    tags = [s['school']]
    if s['ritual']: tags.append('仪式')
    if s['conc']: tags.append('专注')
    if s['upcast']: tags.append('可升环')
    head = ('戏法' if c == 0 else '%d 环' % c) + '（' + '·'.join(tags) + '）：'
    desc = head + s['desc'] + '冷却 ' + fmt_cd(s['cd']) + '。'
    L = []
    L.append('  <AbilityDef ParentName="%s">' % parent)
    L.append('    <defName>LP_CS_%s</defName>' % s['id'])
    L.append('    <label>%s</label>' % escape(s['label']))
    L.append('    <description>%s</description>' % escape(desc))
    L.append('    <iconPath>%s</iconPath>' % (NEWICON(s['id']) if NEW_ICONS else s['icon']))
    if t != 'foe':
        L.append('    <displayGizmoWhileUndrafted>true</displayGizmoWhileUndrafted>')
    if s['hostile'] is False:
        L.append('    <hostile>false</hostile>')
    L.append('    <cooldownTicksRange>%d</cooldownTicksRange>' % ticks_cd(s['cd']))
    if t == 'corpse':
        L.append('    <verbProperties><verbClass>Verb_CastAbility</verbClass><range>%g</range><warmupTime>%g</warmupTime><requireLineOfSight>false</requireLineOfSight><targetParams><canTargetPawns>false</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetItems>true</canTargetItems><canTargetCorpses>true</canTargetCorpses><canTargetLocations>false</canTargetLocations><mapObjectTargetsMustBeAutoAttackable>false</mapObjectTargetsMustBeAutoAttackable></targetParams></verbProperties>' % (s['rng'], s['warm']))
    elif t == 'item':
        L.append('    <verbProperties><verbClass>Verb_CastAbility</verbClass><range>%g</range><warmupTime>%g</warmupTime><requireLineOfSight>false</requireLineOfSight><targetParams><canTargetPawns>false</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetItems>true</canTargetItems><canTargetLocations>false</canTargetLocations><mapObjectTargetsMustBeAutoAttackable>false</mapObjectTargetsMustBeAutoAttackable></targetParams></verbProperties>' % (s['rng'], s['warm']))
    elif t == 'self':
        L.append('    <verbProperties><warmupTime>%g</warmupTime></verbProperties>' % s['warm'])
    else:
        L.append('    <verbProperties><range>%g</range><warmupTime>%g</warmupTime></verbProperties>' % (s['rng'], s['warm']))
    comp = '<effect>%s</effect><cost>%d</cost>' % (s['effect'], cost)
    if s['power'] is not None: comp += '<power>%g</power>' % s['power']
    if s['radius'] is not None: comp += '<radius>%g</radius>' % s['radius']
    if s['erange'] is not None: comp += '<range>%g</range>' % s['erange']
    if s['dur'] is not None: comp += '<duration>%d</duration>' % s['dur']
    if s['hediff']: comp += '<hediff>%s</hediff>' % s['hediff']
    if s['fixed']: comp += '<fixedSeverity>true</fixedSeverity>'
    comp += '<stat>%s</stat><psychic>true</psychic>' % stat
    L.append('    <comps><li Class="LichPhylactery.CompProperties_ClassEffect">%s</li></comps>' % comp)
    ext = '<circle>%d</circle><school>%s</school>' % (c, s['school'])
    if s['ritual']: ext += '<ritual>true</ritual>'
    if s['conc']: ext += '<concentration>true</concentration>'
    if s['upcast']: ext += '<upcast>%s</upcast>' % s['upcast']
    for k, v in s['ext'].items():
        if isinstance(v, bool): v = 'true' if v else 'false'
        ext += '<%s>%s</%s>' % (k, v, k)
    mods = '<li Class="LichPhylactery.CasterSpellExt">%s</li>' % ext
    if s['approach'] is False:
        mods += '<li Class="LichPhylactery.CastRangeExt"><approach>false</approach></li>'
    L.append('    <modExtensions>%s</modExtensions>' % mods)
    L.append('  </AbilityDef>')
    return '\n'.join(L)

def hediff_xml(h):
    name, label, desc, bad, cls, stages, extra = h
    L = ['  <HediffDef ParentName="LP_ClassBuffBase">', '    <defName>%s</defName>' % name]
    if cls == 'CS': L.append('    <hediffClass>LichPhylactery.Hediff_CS</hediffClass>')
    L.append('    <label>%s</label>' % escape(label)); L.append('    <description>%s</description>' % escape(desc))
    if bad: L.append('    <isBad>true</isBad>'); L.append('    <defaultLabelColor>(0.85,0.55,0.55)</defaultLabelColor>')
    else: L.append('    <defaultLabelColor>(0.55,0.8,1)</defaultLabelColor>')
    if stages: L.append('    ' + stages)
    if extra == 'INVIS': L.append('    <comps Inherit="False"><li Class="HediffCompProperties_Disappears"><disappearsAfterTicks>600~600</disappearsAfterTicks><showRemainingTime>true</showRemainingTime></li><li Class="HediffCompProperties_Invisibility"><visibleToPlayer>true</visibleToPlayer><affectedByDisruptor>false</affectedByDisruptor></li></comps>'); extra = ''
    if extra: L.append('    <comps Inherit="False"><li Class="HediffCompProperties_Disappears"><disappearsAfterTicks>%s~%s</disappearsAfterTicks><showRemainingTime>true</showRemainingTime></li></comps>' % (extra, extra))
    L.append('  </HediffDef>')
    return '\n'.join(L)

def write(ver, spells_file, lists_file):
    sp = [s for s in SPELLS if s['ver'] == ver]
    out = ['<?xml version="1.0" encoding="utf-8"?>', '<!-- %s.0 施法职业法术扩充：由 ArtSource/CasterSpells/gen_casterspells.py 生成，不要手改。 -->' % ver, '<Defs>']
    for cls in ['Cleric', 'Druid', 'Sorcerer', 'Warlock', 'Bard', 'Paladin', 'Ranger', 'Artificer']:
        group = [s for s in sp if s['cls'] == cls]
        if not group: continue
        out.append('\n  <!-- ' + cls + ' -->')
        for s in sorted(group, key=lambda s: s['circle']):
            out.append(spell_xml(s))
    if True:
        out.append('\n  <!-- 状态 -->')
        for h in (HEDIFFS if ver == '1.130' else HEDIFFS2): out.append(hediff_xml(h))
        for name, label, desc, cls, stages, pv in [p for p in PERMANENT if p[5] == ver]:
            out.append('  <HediffDef>\n    <defName>%s</defName>\n    <hediffClass>LichPhylactery.Hediff_CS</hediffClass>\n    <label>%s</label>\n    <description>%s</description>\n    <defaultLabelColor>(0.55,0.85,0.45)</defaultLabelColor>\n    <isBad>false</isBad>\n    <scenarioCanAdd>false</scenarioCanAdd>%s\n  </HediffDef>' % (name, escape(label), escape(desc), ('\n    ' + stages) if stages else ''))
    if ver == '1.130':
        for name, label, desc, mood in THOUGHTS:
            out.append('  <ThoughtDef>\n    <defName>%s</defName>\n    <thoughtClass>Thought_Memory</thoughtClass>\n    <durationDays>1</durationDays>\n    <stackLimit>1</stackLimit>\n    <stages><li><label>%s</label><description>%s</description><baseMoodEffect>%d</baseMoodEffect></li></stages>\n  </ThoughtDef>' % (name, escape(label), escape(desc), mood))
        out.append('''  <ThingDef ParentName="PlantFoodRawBase">
    <defName>LP_CS_Goodberry</defName>
    <label>良莓</label>
    <description>德鲁伊用良莓术变出来的浆果：一颗就有一顿饭的营养，吃下去几秒内伤势慢慢愈合。15 天后枯萎。</description>
    <graphicData><texPath>Things/Item/Resource/PlantFoodRaw/Berries</texPath><color>(0.75,0.95,0.6)</color></graphicData>
    <statBases><MarketValue>6</MarketValue><Mass>0.03</Mass><Nutrition>0.9</Nutrition></statBases>
    <ingestible><foodType>VegetableOrFruit</foodType><preferability>MealSimple</preferability><outcomeDoers><li Class="IngestionOutcomeDoer_GiveHediff"><hediffDef>LP_CS_Goodberry</hediffDef><severity>1</severity></li></outcomeDoers></ingestible>
    <comps><li Class="CompProperties_Rottable"><daysToRotStart>15</daysToRotStart><rotDestroys>true</rotDestroys></li></comps>
    <tradeability>None</tradeability>
  </ThingDef>''')
    out.append('</Defs>\n')
    with open(os.path.join(ROOT, 'Defs', spells_file), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(out))
    # 法术表
    out = ['<?xml version="1.0" encoding="utf-8"?>', '<!-- 施法职业法术表：由 ArtSource/CasterSpells/gen_casterspells.py 生成。defName=环位 表示这个职业按另一个环位用它。 -->', '<Defs>']
    for cls, L in LISTS.items():
        if LIST_VER.get(cls) is None: continue
        own = ['LP_CS_' + s['id'] for s in SPELLS if s['cls'] == cls]
        spells = own + [e for e in L['extra'] if e not in own]
        x = ['  <LichPhylactery.CasterListDef>', '    <defName>LP_CSL_%s</defName>' % cls, '    <label>%s法术表</label>' % CLSLABEL[cls],
             '    <classDef>%s</classDef>' % L['classDef'], '    <ability>%s</ability>' % L['ability'], '    <caster>%s</caster>' % L['caster'],
             '    <prepared>%s</prepared>' % ('true' if L['prepared'] else 'false'), '    <order>%d</order>' % L['order'], '    <iconPath>%s</iconPath>' % L['icon']]
        if not L.get('cantrips', True): x.append('    <cantrips>false</cantrips>')
        if L.get('secrets'): x.append('    <secretLevels>' + ''.join('<li>%d</li>' % v for v in L['secrets']) + '</secretLevels>')
        x.append('    <stats>' + ''.join('<li>%s</li>' % v for v in L['stats']) + '</stats>')
        x.append('    <spells>')
        for e in spells: x.append('      <li>%s</li>' % e)
        x.append('    </spells>')
        x.append('  </LichPhylactery.CasterListDef>')
        out.append('\n'.join(x))
    out.append('</Defs>\n')
    with open(os.path.join(ROOT, 'Defs', lists_file), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(out))
    print('spells', len(sp), 'lists', sum(1 for c in LISTS if LIST_VER.get(c)))

if __name__ == '__main__':
    write('1.130', 'CasterSpells.xml', 'CasterSpells_Lists.xml')
    write('1.131', 'CasterSpells2.xml', 'CasterSpells_Lists.xml')
