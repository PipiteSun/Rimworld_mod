# -*- coding: utf-8 -*-
# 1.185.0 第七批子职业（10 个）：把子职业不到 5 个的职业补到 5 个。
# 生成 Defs/Subclasses7.xml 和英文 DefInjected（AbilityDef / HediffDef / LichPhylactery.LP_SubclassDef 下的 Subclasses7.xml）。
# 用法：python ArtSource/Subclasses7/gen_subclasses7.py（在模组根目录下跑）。图标、徽记先借用已有贴图。
import os, re, sys, io

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
P = lambda *a: os.path.join(ROOT, *a)

def esc(s):
    return s.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')

# ---------- 子职业 ----------
# (defName, 中文, 英文, 中文介绍, 英文介绍, 父职业, order, 徽记, 颜色, [(能力, 等级)], 从哪个子职业抄 choices)
SUBS = [
 ('LP_Sub_DrunkenWay', '醉拳宗', 'Way of the Drunken Master',
  '摇摇晃晃的武僧：步子飘忽难以打中，借对手的力把打来的近战卸掉、反震对手，醉意让他不怕疼也不怕吓，最后一口气把身边的敌人挨个打两遍。',
  'A swaying monk: hard to hit, turns melee blows aside and staggers the attacker, numb to pain and fear, and finally strikes every adjacent enemy twice in one breath.',
  'LP_Class_Monk', 4, 'LP/Classes/Subclass/Emblem_OpenHandWay', '(0.8,0.6,0.35)',
  [('LP_DK_Sway', 3), ('LP_DK_Redirect', 6), ('LP_DK_Numb', 11), ('LP_DK_Frenzy', 17)], None),
 ('LP_Sub_KenseiWay', '剑圣宗', 'Way of the Kensei',
  '把兵器练成身体一部分的武僧：拿着近战武器时更准更灵活，武器穿透护甲、伤害更高，能用气磨砺剑锋，最后每一击都可能打出双倍伤害。',
  'A monk whose weapon is part of the body: more accurate and nimble with a melee weapon in hand, the weapon pierces armor and hits harder, ki sharpens the blade, and finally any hit may deal double damage.',
  'LP_Class_Monk', 5, 'LP/Classes/Subclass/Emblem_Swords', '(0.7,0.75,0.85)',
  [('LP_KS_Path', 3), ('LP_KS_OneWithBlade', 6), ('LP_KS_Sharpen', 11), ('LP_KS_Unerring', 17)], None),
 ('LP_Sub_Zealot', '狂热者之道', 'Path of the Zealot',
  '为神而战的野蛮人：狂暴中近战带着神圣的怒火，心志坚定，一声战吼让身边的友军跟着狂热，最后狂暴中受到的伤害更少。',
  'A barbarian who fights for a god: melee burns with divine fury while raging, a steadfast mind, a war cry that fires up nearby allies, and finally less damage taken while raging.',
  'LP_Class_Barbarian', 4, 'LP/Classes/Subclass/Emblem_Devotion', '(0.95,0.8,0.45)',
  [('LP_ZL_DivineFury', 3), ('LP_ZL_Focus', 7), ('LP_ZL_Presence', 11), ('LP_ZL_BeyondDeath', 15)], None),
 ('LP_Sub_Ancestral', '先祖守卫之道', 'Path of the Ancestral Guardian',
  '召来先祖之灵保护同伴的野蛮人：狂暴中被他打中的敌人打别人更疼不起来，身边友军受到的伤害被先祖挡下一部分，先祖的智慧让他学得更快，最后挡下的伤害还会反击回去。',
  'A barbarian who calls ancestral spirits to protect companions: enemies hit while raging hurt others less, spirits shield nearby allies, ancestral wisdom speeds learning, and finally the shielded damage strikes back.',
  'LP_Class_Barbarian', 5, 'LP/Classes/Subclass/Emblem_TotemWarrior', '(0.6,0.75,0.85)',
  [('LP_AG_Protectors', 3), ('LP_AG_SpiritShield', 7), ('LP_AG_Consult', 11), ('LP_AG_Vengeful', 15)], None),
 ('LP_Sub_FeyWanderer', '妖精漫游者', 'Fey Wanderer',
  '在妖精荒野里走过的游侠：武器命中带着扰乱心神的力量，谈吐迷人、心志坚定，放出惑心之雾震住身边的敌人，最后能踏着迷雾瞬移。',
  'A ranger who has walked the Feywild: weapon hits carry mind-rattling force, a charming and steady mind, a beguiling mist that rattles nearby enemies, and finally a step through the mist.',
  'LP_Class_Ranger', 4, 'LP/Classes/Subclass/Emblem_Archfey', '(0.75,0.6,0.9)',
  [('LP_WN_Dreadful', 3), ('LP_WN_Beguiling', 7), ('LP_WN_Glamour', 11), ('LP_WN_MistyStep', 15)], None),
 ('LP_Sub_HorizonWalker', '地平线行者', 'Horizon Walker',
  '守在位面边界的游侠：武器命中附加力场伤害，踏入以太位面躲开攻击，在战场上跳跃着出手，最后身形半虚、定时把一次伤害减半。',
  'A ranger who guards the planar borders: weapon hits add force damage, steps into the Ethereal to avoid attacks, jumps around the battlefield while striking, and finally a half-spectral body halves a hit at intervals.',
  'LP_Class_Ranger', 5, 'LP/Classes/Subclass/Emblem_ShadowMagic', '(0.55,0.7,0.95)',
  [('LP_HW_PlanarWarrior', 3), ('LP_HW_EtherealStep', 7), ('LP_HW_DistantStrike', 11), ('LP_HW_Spectral', 15)], None),
 ('LP_Sub_CircleWildfire', '野火结社', 'Circle of Wildfire',
  '懂得火既毁灭也新生的德鲁伊：炽焰之种烧灼并点燃敌人，野火之灵让德鲁伊法术更强，敌人死在附近时化为治愈的余烬，最后倒下时浴火重生。',
  'A druid who knows fire both destroys and renews: fiery seeds burn and ignite enemies, a wildfire spirit strengthens druid spells, enemies dying nearby turn into healing embers, and finally rebirth in flame when struck down.',
  'LP_Class_Druid', 5, 'LP/Classes/Subclass/Emblem_LightDomain', '(0.95,0.55,0.3)',
  [('LP_WF_Seed', 2), ('LP_WF_Bond', 6), ('LP_WF_Cauterize', 10), ('LP_WF_Rebirth', 14)], None),
 ('LP_Sub_Glamour', '迷惑学院', 'College of Glamour',
  '带着妖精魅力的表演者：激励披风给友军护盾，迷魂的演出让社交和交易更顺，威仪披风让敌人跪下，最后披上不破的威严。',
  'A performer with fey charm: a mantle of inspiration shields allies, enthralling performances smooth social dealings and trade, a mantle of majesty brings an enemy to its knees, and finally an unbreakable majesty.',
  'LP_Class_Bard', 5, 'LP/Classes/Subclass/Emblem_Eloquence', '(0.9,0.6,0.8)',
  [('LP_GL_Mantle', 3), ('LP_GL_Enthrall', 7), ('LP_GL_Majesty', 11), ('LP_GL_Unbreakable', 15)], 'LP_Sub_Eloquence'),
 ('LP_Sub_BountyHunter', '赏金猎人', 'Way of the Bounty Hunter',
  '盯上一个目标就不松口的枪手：悬赏标记让自己对它伤害更高，脚程快、枪法稳，缚足射击拖住逃跑的目标，目标一死就有回报，最后生死不论。',
  'A gunslinger who never lets a target go: a bounty mark raises damage against it, quick feet and a steady aim, a hobbling shot slows runners, a payoff when the target dies, and finally dead or alive.',
  'LP_Class_Gunslinger', 5, 'LP/Classes/Subclass/Emblem_Hunter', '(0.75,0.65,0.4)',
  [('LP_BH_Mark', 3), ('LP_BH_Tracker', 3), ('LP_BH_Bola', 7), ('LP_BH_Payday', 11), ('LP_BH_DeadOrAlive', 15)], None),
 ('LP_Sub_PsiSeer', '预知者', 'Seer',
  '提前看见下一秒的心灵术士：预知闪避，提醒友军躲开危险，看穿敌人的破绽，天眼护住心神，定时完全躲开一次攻击，最后把一片友军的伤势拨回去。常备法术：护盾术、心灵尖刺、镜影术（3 级），困惑术（7 级），探知术（9 级）。',
  'A psion who sees the next second ahead: foresight dodging, warning allies of danger, exposing an enemy\'s openings, a third eye guarding the mind, fully avoiding an attack at intervals, and finally rewinding the wounds of nearby allies. Always-prepared spells: shield, mind spike, mirror image (level 3), confusion (level 7), scrying (level 9).',
  'LP_Class_Psion', 5, 'LP/Classes/Subclass/Emblem_Divination', '(0.7,0.8,0.95)',
  [('LP_SR_Foresight', 3), ('LP_SR_Premonition', 3), ('LP_SR_Expose', 6), ('LP_SR_ThirdEye', 6), ('LP_SR_Precog', 10), ('LP_SR_Rewind', 14)], 'LP_Sub_PsiWarper'),
]

# ---------- 状态 ----------
# (defName, 类型 passive/buff/mark/bad, 中文, 英文, 中文说明, 英文说明, 颜色, stage 内容)
HEDIFFS = [
 ('LP_DK_SwayBuff', 'passive', '醉步', 'drunken sway', '醉拳宗：移动 +0.2、近战闪避 +10。', 'Drunken Master: move speed +0.2, melee dodge +10.', '(0.8,0.6,0.35)',
  '<statOffsets><MoveSpeed>0.2</MoveSpeed><MeleeDodgeChance>10</MeleeDodgeChance></statOffsets>'),
 ('LP_DK_NumbBuff', 'passive', '醉意麻木', 'drunken numbness', '醉拳宗：疼痛休克阈值 +15%、心智抗性 +20%。', 'Drunken Master: pain shock threshold +15%, mind resistance +20%.', '(0.8,0.6,0.35)',
  '<statOffsets><PainShockThreshold>0.15</PainShockThreshold><LP_MindResist>0.2</LP_MindResist></statOffsets>'),
 ('LP_KS_PathBuff', 'passive', '剑圣之道', 'path of the kensei', '剑圣宗：拿着近战武器时近战命中 +10%、近战闪避 +5。', 'Kensei: with a melee weapon in hand, melee hit chance +10%, melee dodge +5.', '(0.7,0.75,0.85)',
  '<statOffsets><MeleeHitChance>0.1</MeleeHitChance><MeleeDodgeChance>5</MeleeDodgeChance></statOffsets>'),
 ('LP_KS_Sharpened', 'buff', '磨砺剑锋', 'sharpened blade', '用气磨过的剑锋：近战伤害 ×1.25。', 'A ki-sharpened edge: melee damage ×1.25.', '(0.7,0.75,0.85)',
  '<statFactors><MeleeDamageFactor>1.25</MeleeDamageFactor></statFactors>'),
 ('LP_ZL_FocusBuff', 'passive', '狂热专注', 'fanatical focus', '狂热者之道：心智抗性 +20%、疼痛休克阈值 +10%。', 'Zealot: mind resistance +20%, pain shock threshold +10%.', '(0.95,0.8,0.45)',
  '<statOffsets><LP_MindResist>0.2</LP_MindResist><PainShockThreshold>0.1</PainShockThreshold></statOffsets>'),
 ('LP_ZL_Zeal', 'buff', '狂热', 'zeal', '被狂热者的战吼点燃：近战命中 +10%、移动 ×1.1。', 'Fired up by a zealot\'s war cry: melee hit chance +10%, move speed ×1.1.', '(0.95,0.8,0.45)',
  '<statOffsets><MeleeHitChance>0.1</MeleeHitChance></statOffsets><statFactors><MoveSpeed>1.1</MoveSpeed></statFactors>'),
 ('LP_AG_Haunted', 'mark', '先祖纠缠', 'haunted by ancestors', '被先祖之灵纠缠：打标记它的野蛮人以外的人时伤害 ×0.75。', 'Hounded by ancestral spirits: damage ×0.75 against anyone but the barbarian who marked it.', '(0.6,0.75,0.85)', ''),
 ('LP_AG_ConsultBuff', 'passive', '请示先祖', 'consult the spirits', '先祖守卫之道：学习速度 ×1.15、心智抗性 +15%。', 'Ancestral Guardian: learning speed ×1.15, mind resistance +15%.', '(0.6,0.75,0.85)',
  '<statFactors><GlobalLearningFactor>1.15</GlobalLearningFactor></statFactors><statOffsets><LP_MindResist>0.15</LP_MindResist></statOffsets>'),
 ('LP_WN_BeguilingBuff', 'passive', '迷人之姿', 'beguiling presence', '妖精漫游者：心智抗性 +20%、社交影响 +15%、谈判能力 +15%。', 'Fey Wanderer: mind resistance +20%, social impact +15%, negotiation ability +15%.', '(0.75,0.6,0.9)',
  '<statOffsets><LP_MindResist>0.2</LP_MindResist><SocialImpact>0.15</SocialImpact><NegotiationAbility>0.15</NegotiationAbility></statOffsets>'),
 ('LP_HW_Ethereal', 'buff', '以太步', 'ethereal step', '半个身子在以太位面里：受到的伤害 ×0.5、移动 ×1.2。', 'Half in the Ethereal Plane: damage taken ×0.5, move speed ×1.2.', '(0.55,0.7,0.95)',
  '<statFactors><IncomingDamageFactor>0.5</IncomingDamageFactor><MoveSpeed>1.2</MoveSpeed></statFactors>'),
 ('LP_HW_Momentum', 'buff', '远距打击', 'distant strike', '刚从位面缝隙里跳出来：武器伤害 ×1.2。', 'Fresh out of a planar rift: weapon damage ×1.2.', '(0.55,0.7,0.95)', ''),
 ('LP_GL_EnthrallBuff', 'passive', '迷魂演出', 'enthralling performance', '迷惑学院：社交影响 +20%、谈判能力 +20%、交易价格 +5%。', 'College of Glamour: social impact +20%, negotiation ability +20%, trade price improvement +5%.', '(0.9,0.6,0.8)',
  '<statOffsets><SocialImpact>0.2</SocialImpact><NegotiationAbility>0.2</NegotiationAbility><TradePriceImprovement>0.05</TradePriceImprovement></statOffsets>'),
 ('LP_GL_UnbreakBuff', 'buff', '不破威严', 'unbreakable majesty', '妖精般的威严：受到的伤害 ×0.7、心智抗性 +30%。', 'Fey-like majesty: damage taken ×0.7, mind resistance +30%.', '(0.9,0.6,0.8)',
  '<statFactors><IncomingDamageFactor>0.7</IncomingDamageFactor></statFactors><statOffsets><LP_MindResist>0.3</LP_MindResist></statOffsets>'),
 ('LP_BH_Marked', 'mark', '悬赏标记', 'bounty mark', '被赏金猎人盯上：标记它的枪手对它伤害 ×1.2（生死不论 ×1.4）。', 'Marked by a bounty hunter: the gunslinger who marked it deals ×1.2 damage to it (×1.4 with dead or alive).', '(0.75,0.65,0.4)', ''),
 ('LP_BH_TrackerBuff', 'passive', '追踪好手', 'expert tracker', '赏金猎人：移动 +0.2、射击精度 +1。', 'Bounty Hunter: move speed +0.2, shooting accuracy +1.', '(0.75,0.65,0.4)',
  '<statOffsets><MoveSpeed>0.2</MoveSpeed><ShootingAccuracyPawn>1</ShootingAccuracyPawn></statOffsets>'),
 ('LP_BH_Hobbled', 'bad', '缚足', 'hobbled', '腿被缠住：移动 ×0.5。', 'Legs tangled: move speed ×0.5.', '(0.75,0.65,0.4)',
  '<statFactors><MoveSpeed>0.5</MoveSpeed></statFactors>'),
 ('LP_SR_ForesightBuff', 'passive', '预知闪避', 'foresight', '预知者：近战闪避 +8、射击精度 +1。', 'Seer: melee dodge +8, shooting accuracy +1.', '(0.7,0.8,0.95)',
  '<statOffsets><MeleeDodgeChance>8</MeleeDodgeChance><ShootingAccuracyPawn>1</ShootingAccuracyPawn></statOffsets>'),
 ('LP_SR_EyeBuff', 'passive', '天眼', 'third eye', '预知者：心智抗性 +20%、心灵敏感度 +15%。', 'Seer: mind resistance +20%, psychic sensitivity +15%.', '(0.7,0.8,0.95)',
  '<statOffsets><LP_MindResist>0.2</LP_MindResist><PsychicSensitivity>0.15</PsychicSensitivity></statOffsets>'),
 ('LP_SR_Warned', 'buff', '危机预感', 'premonition', '被预知者提醒过：受到的伤害 ×0.8、近战闪避 +10。', 'Warned by a seer: damage taken ×0.8, melee dodge +10.', '(0.7,0.8,0.95)',
  '<statFactors><IncomingDamageFactor>0.8</IncomingDamageFactor></statFactors><statOffsets><MeleeDodgeChance>10</MeleeDodgeChance></statOffsets>'),
 ('LP_SR_Exposed', 'bad', '破绽尽现', 'exposed', '破绽被预知者看穿：受到的伤害 ×1.15。', 'Openings laid bare by a seer: damage taken ×1.15.', '(0.7,0.8,0.95)',
  '<statFactors><IncomingDamageFactor>1.15</IncomingDamageFactor></statFactors>'),
]

# ---------- 能力 ----------
# (defName, 基类 Self/Melee/Ally/Loc, 中文, 英文, 中文说明, 英文说明, 图标, 额外 XML, comp 内容)
def passive(stat, extra=''):
    return '<effect>Passive</effect><passive>true</passive>' + extra + '<stat>' + stat + '</stat>'

M, R, H, V, B, G, S = 'LP_MartialPower', 'LP_RagePower', 'LP_HuntPower', 'LP_VerdantPower', 'LP_BardicPower', 'LP_GunPower', 'LP_PsionicPower'
ABILITIES = [
 # 醉拳宗
 ('LP_DK_Sway', 'Self', '醉步', 'drunken sway', '被动：移动 +0.2、近战闪避 +10。醉拳宗，武僧 3 级。', 'Passive: move speed +0.2, melee dodge +10. Drunken Master, monk level 3.',
  'LP/Classes/Subclass3/Technique', '', passive(M)),
 ('LP_DK_Redirect', 'Self', '借力打力', 'redirect attack', '被动：被敌人近战打中时有 25% 的成功率把这一下卸掉（不受伤），并让对方眩晕 1 秒。每 3 秒最多一次。醉拳宗，武僧 6 级。',
  'Passive: when an enemy hits you in melee, a 25% success chance to turn the blow aside (no damage) and stun the attacker for 1 second. At most once every 3 seconds. Drunken Master, monk level 6.',
  'LP/Classes/Subclass3/Tranquility', '', passive(M)),
 ('LP_DK_Numb', 'Self', '醉意麻木', 'drunken numbness', '被动：疼痛休克阈值 +15%、心智抗性 +20%。醉拳宗，武僧 11 级。', 'Passive: pain shock threshold +15%, mind resistance +20%. Drunken Master, monk level 11.',
  'LP/Classes/Subclass3/Wholeness', '', passive(M)),
 ('LP_DK_Frenzy', 'Self', '醉拳乱舞', 'intoxicated frenzy', '对身边相邻的敌人（最多 5 个）每人打两次完整的近战结算。气 3，冷却 20 秒。醉拳宗，武僧 17 级。',
  'Strike each adjacent enemy (up to 5) with two full melee attacks. Ki 3, cooldown 20 seconds. Drunken Master, monk level 17.',
  'LP/Classes/Subclass3/Palm', '<cooldownTicksRange>1200</cooldownTicksRange>\n    <verbProperties><warmupTime>0.25</warmupTime></verbProperties>',
  '<effect>S7_DKFrenzy</effect><cost>3</cost><radius>1.9</radius><stat>' + M + '</stat>'),
 # 剑圣宗
 ('LP_KS_Path', 'Self', '剑圣之道', 'path of the kensei', '被动：拿着近战武器时近战命中 +10%、近战闪避 +5。剑圣宗，武僧 3 级。', 'Passive: with a melee weapon in hand, melee hit chance +10%, melee dodge +5. Kensei, monk level 3.',
  'LP/Classes/Bard/BladeFlourish', '', passive(M)),
 ('LP_KS_OneWithBlade', 'Self', '人剑合一', 'one with the blade', '被动：近战武器命中伤害 ×1.1、护甲穿透 +20%。剑圣宗，武僧 6 级。', 'Passive: melee weapon hits deal ×1.1 damage with +20% armor penetration. Kensei, monk level 6.',
  'LP/Classes/Bard/ExtraAttack', '', passive(M)),
 ('LP_KS_Sharpen', 'Self', '磨砺剑锋', 'sharpen the blade', '60 秒内近战伤害 ×1.25。气 2，冷却 2 分钟。剑圣宗，武僧 11 级。', 'For 60 seconds melee damage ×1.25. Ki 2, cooldown 2 minutes. Kensei, monk level 11.',
  'LP/Classes/Bard/MasterFlourish', '<cooldownTicksRange>7200</cooldownTicksRange>\n    <verbProperties><warmupTime>0.5</warmupTime></verbProperties>',
  '<effect>S7_Buff</effect><cost>2</cost><duration>3600</duration><hediff>LP_KS_Sharpened</hediff><stat>' + M + '</stat>'),
 ('LP_KS_Unerring', 'Self', '无误精准', 'unerring accuracy', '被动：近战武器命中有 15% 的成功率造成双倍伤害。剑圣宗，武僧 17 级。', 'Passive: melee weapon hits have a 15% success chance to deal double damage. Kensei, monk level 17.',
  'LP/Classes/Bard/BladeWhirl', '', passive(M)),
 # 狂热者之道
 ('LP_ZL_DivineFury', 'Self', '神圣狂怒', 'divine fury', '被动：狂暴中近战伤害 ×1.15。狂热者之道，野蛮人 3 级。', 'Passive: melee damage ×1.15 while raging. Zealot, barbarian level 3.',
  'LP/Classes/Subclass/TotemSpirit', '', passive(R)),
 ('LP_ZL_Focus', 'Self', '狂热专注', 'fanatical focus', '被动：心智抗性 +20%、疼痛休克阈值 +10%。狂热者之道，野蛮人 7 级。', 'Passive: mind resistance +20%, pain shock threshold +10%. Zealot, barbarian level 7.',
  'LP/Classes/Subclass/IronMind', '', passive(R)),
 ('LP_ZL_Presence', 'Self', '狂热威仪', 'zealous presence', '一声战吼：自己和 8 格内的友军 30 秒内近战命中 +10%、移动 ×1.1。冷却 2 小时。狂热者之道，野蛮人 11 级。',
  'A war cry: you and allies within 8 cells get melee hit chance +10% and move speed ×1.1 for 30 seconds. Cooldown 2 hours. Zealot, barbarian level 11.',
  'LP/Classes/Subclass/SpiritWalker', '<cooldownTicksRange>5000</cooldownTicksRange>\n    <verbProperties><warmupTime>0.25</warmupTime></verbProperties>',
  '<effect>S7_AllyBuff</effect><radius>8</radius><duration>1800</duration><hediff>LP_ZL_Zeal</hediff><stat>' + R + '</stat>'),
 ('LP_ZL_BeyondDeath', 'Self', '死战不休', 'rage beyond death', '被动：狂暴中受到的伤害 ×0.85。狂热者之道，野蛮人 15 级。', 'Passive: damage taken ×0.85 while raging. Zealot, barbarian level 15.',
  'LP/Classes/Subclass/TotemAttunement', '', passive(R)),
 # 先祖守卫之道
 ('LP_AG_Protectors', 'Self', '先祖护卫', 'ancestral protectors', '被动：狂暴中近战打中的敌人被先祖之灵纠缠 6 秒：它打你以外的人时伤害 ×0.75。先祖守卫之道，野蛮人 3 级。',
  'Passive: enemies you hit in melee while raging are hounded by ancestral spirits for 6 seconds: their damage against anyone but you is ×0.75. Ancestral Guardian, barbarian level 3.',
  'LP/Classes/Subclass/SpiritWalker', '', passive(R)),
 ('LP_AG_SpiritShield', 'Self', '灵魂护盾', 'spirit shield', '被动：狂暴中，6 格内的友军受到的伤害 ×0.85。先祖守卫之道，野蛮人 7 级。', 'Passive: while you rage, allies within 6 cells take ×0.85 damage. Ancestral Guardian, barbarian level 7.',
  'LP/Classes/Subclass/TotemSpirit', '', passive(R)),
 ('LP_AG_Consult', 'Self', '请示先祖', 'consult the spirits', '被动：学习速度 ×1.15、心智抗性 +15%。先祖守卫之道，野蛮人 11 级。', 'Passive: learning speed ×1.15, mind resistance +15%. Ancestral Guardian, barbarian level 11.',
  'LP/Classes/Subclass/AspectOfBeast', '', passive(R)),
 ('LP_AG_Vengeful', 'Self', '复仇先祖', 'vengeful ancestors', '被动：灵魂护盾挡下多少伤害，攻击者就受多少力场伤害。先祖守卫之道，野蛮人 15 级。', 'Passive: whatever damage spirit shield blocks, the attacker takes as force damage. Ancestral Guardian, barbarian level 15.',
  'LP/Classes/Subclass/TotemAttunement', '', passive(R)),
 # 妖精漫游者
 ('LP_WN_Dreadful', 'Self', '恐惧打击', 'dreadful strikes', '被动：武器命中敌人伤害 ×1.1。妖精漫游者，游侠 3 级。', 'Passive: weapon hits on enemies deal ×1.1 damage. Fey Wanderer, ranger level 3.',
  'LP/Classes/Subclass/DreadAmbusher', '', passive(H)),
 ('LP_WN_Beguiling', 'Self', '迷人之姿', 'beguiling presence', '被动：心智抗性 +20%、社交影响 +15%、谈判能力 +15%。妖精漫游者，游侠 7 级。', 'Passive: mind resistance +20%, social impact +15%, negotiation ability +15%. Fey Wanderer, ranger level 7.',
  'LP/Classes/Subclass/IronMind', '', passive(H)),
 ('LP_WN_Glamour', 'Self', '惑心之雾', 'beguiling mist', '身边 4 格内的敌人被震慑 10 秒，没抵抗住（心智抗性）的还会眩晕 2 秒。冷却 1 分钟。妖精漫游者，游侠 11 级。',
  'Enemies within 4 cells are shaken for 10 seconds; those who fail to resist (mind resistance) are also stunned for 2 seconds. Cooldown 1 minute. Fey Wanderer, ranger level 11.',
  'LP/Classes/Subclass/StalkersFlurry', '<cooldownTicksRange>3600</cooldownTicksRange>\n    <verbProperties><warmupTime>0.5</warmupTime></verbProperties>',
  '<effect>S7_WNGlamour</effect><radius>4</radius><duration>600</duration><stat>' + H + '</stat><psychic>true</psychic>'),
 ('LP_WN_MistyStep', 'Loc', '迷雾漫步', 'misty wanderer', '踏着迷雾瞬移到 12 格内看得见的空地。冷却 20 秒。妖精漫游者，游侠 15 级。', 'Step through the mist to a visible empty cell within 12 cells. Cooldown 20 seconds. Fey Wanderer, ranger level 15.',
  'LP/Classes/Warlock/MistyEscape', '<hostile>false</hostile>\n    <cooldownTicksRange>1200</cooldownTicksRange>\n    <verbProperties><range>12</range><warmupTime>0</warmupTime><targetParams><canTargetPawns>false</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetLocations>true</canTargetLocations></targetParams></verbProperties>',
  '<effect>S7_Blink</effect><stat>' + H + '</stat>'),
 # 地平线行者
 ('LP_HW_PlanarWarrior', 'Self', '位面战士', 'planar warrior', '被动：武器命中敌人时追加 8 × 猎杀强度 的力场伤害，每 5 秒一次。地平线行者，游侠 3 级。', 'Passive: weapon hits on enemies add 8 × hunt power force damage, once every 5 seconds. Horizon Walker, ranger level 3.',
  'LP/Classes/Subclass/DreadAmbusher', '', passive(H, '<power>8</power>')),
 ('LP_HW_EtherealStep', 'Self', '以太步', 'ethereal step', '10 秒内受到的伤害 ×0.5、移动 ×1.2。冷却 2 分钟。地平线行者，游侠 7 级。', 'For 10 seconds damage taken ×0.5 and move speed ×1.2. Cooldown 2 minutes. Horizon Walker, ranger level 7.',
  'LP/Classes/Subclass/ShadowyDodge', '<cooldownTicksRange>7200</cooldownTicksRange>\n    <verbProperties><warmupTime>0</warmupTime></verbProperties>',
  '<effect>S7_Buff</effect><duration>600</duration><hediff>LP_HW_Ethereal</hediff><stat>' + H + '</stat>'),
 ('LP_HW_DistantStrike', 'Loc', '远距打击', 'distant strike', '瞬移到 10 格内看得见的空地，之后 5 秒内武器伤害 ×1.2。冷却 8 秒。地平线行者，游侠 11 级。', 'Teleport to a visible empty cell within 10 cells; for the next 5 seconds weapon damage ×1.2. Cooldown 8 seconds. Horizon Walker, ranger level 11.',
  'LP/Classes/Rogue/ShadowStep', '<hostile>false</hostile>\n    <cooldownTicksRange>480</cooldownTicksRange>\n    <verbProperties><range>10</range><warmupTime>0</warmupTime><targetParams><canTargetPawns>false</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetLocations>true</canTargetLocations></targetParams></verbProperties>',
  '<effect>S7_Blink</effect><duration>300</duration><hediff>LP_HW_Momentum</hediff><stat>' + H + '</stat>'),
 ('LP_HW_Spectral', 'Self', '幽灵防御', 'spectral defense', '被动：每 5 秒第一次受到的伤害减半。地平线行者，游侠 15 级。', 'Passive: the first damage you take every 5 seconds is halved. Horizon Walker, ranger level 15.',
  'LP/Classes/Subclass/StalkersFlurry', '', passive(H)),
 # 野火结社
 ('LP_WF_Seed', 'Melee', '炽焰之种', 'fiery seed', '20 格内一个敌人受 12 × 生长强度 火焰伤害并被点燃。法力 1，冷却 5 秒。野火结社，德鲁伊 2 级。', 'An enemy within 20 cells takes 12 × verdant power flame damage and is set on fire. Mana 1, cooldown 5 seconds. Circle of Wildfire, druid level 2.',
  'LP/Classes/Subclass3/StarBolt', '<cooldownTicksRange>300</cooldownTicksRange>\n    <verbProperties><range>20</range><warmupTime>0.5</warmupTime></verbProperties>',
  '<effect>S7_WFSeed</effect><cost>1</cost><power>12</power><stat>' + V + '</stat><psychic>true</psychic>'),
 ('LP_WF_Bond', 'Self', '野火之灵', 'wildfire spirit', '被动：德鲁伊法术与能力的效果 ×1.1。野火结社，德鲁伊 6 级。', 'Passive: druid spells and abilities are ×1.1 as effective. Circle of Wildfire, druid level 6.',
  'LP/Classes/Subclass3/StarryForm', '', passive(V)),
 ('LP_WF_Cauterize', 'Self', '治愈余烬', 'cauterizing flames', '被动：敌人死在你 10 格内时，你和 10 格内伤得最重的友军各治疗 6 + 德鲁伊等级 点伤势。每 10 秒最多一次。野火结社，德鲁伊 10 级。',
  'Passive: when an enemy dies within 10 cells of you, you and the most wounded ally within 10 cells each heal 6 + druid level. At most once every 10 seconds. Circle of Wildfire, druid level 10.',
  'LP/Classes/Subclass3/CosmicOmen', '', passive(V)),
 ('LP_WF_Rebirth', 'Self', '浴火重生', 'blazing revival', '被动：快要倒下时浴火重生：治好一半伤势并止血。每天一次。野火结社，德鲁伊 14 级。', 'Passive: when about to go down, rise in flame: heal half your wounds and stop bleeding. Once per day. Circle of Wildfire, druid level 14.',
  'LP/Classes/Subclass3/FullOfStars', '', passive(V)),
 # 迷惑学院
 ('LP_GL_Mantle', 'Self', '激励披风', 'mantle of inspiration', '自己和 8 格内的友军各得到 (6 + 吟游诗人等级) × 激昂强度 的吸收护盾 30 秒。消耗 1 点乐韵，冷却 1 分钟。迷惑学院，吟游诗人 3 级。',
  'You and allies within 8 cells each gain an absorb shield of (6 + bard level) × bardic power for 30 seconds. Costs 1 melody, cooldown 1 minute. College of Glamour, bard level 3.',
  'LP/Classes/Bard/BladeFlourish', '<displayGizmoWhileUndrafted>true</displayGizmoWhileUndrafted>\n    <cooldownTicksRange>3600</cooldownTicksRange>\n    <verbProperties><warmupTime>0.5</warmupTime></verbProperties>',
  '<effect>S7_GLMantle</effect><cost>1</cost><radius>8</radius><duration>1800</duration><stat>' + B + '</stat>'),
 ('LP_GL_Enthrall', 'Self', '迷魂演出', 'enthralling performance', '被动：社交影响 +20%、谈判能力 +20%、交易价格 +5%。迷惑学院，吟游诗人 7 级。', 'Passive: social impact +20%, negotiation ability +20%, trade price improvement +5%. College of Glamour, bard level 7.',
  'LP/Classes/Bard/ExtraAttack', '', passive(B)),
 ('LP_GL_Majesty', 'Melee', '威仪披风', 'mantle of majesty', '命令 15 格内一个敌人跪下：没抵抗住（心智抗性）就眩晕 4 秒，抵抗住也被震慑 10 秒。消耗 1 点乐韵，冷却 20 秒。迷惑学院，吟游诗人 11 级。',
  'Command an enemy within 15 cells to kneel: if it fails to resist (mind resistance) it is stunned for 4 seconds; even if it resists it is shaken for 10 seconds. Costs 1 melody, cooldown 20 seconds. College of Glamour, bard level 11.',
  'LP/Classes/Bard/BladeWhirl', '<cooldownTicksRange>1200</cooldownTicksRange>\n    <verbProperties><range>15</range><warmupTime>0.5</warmupTime></verbProperties>',
  '<effect>S7_GLMajesty</effect><cost>1</cost><stat>' + B + '</stat><psychic>true</psychic>'),
 ('LP_GL_Unbreakable', 'Self', '不破威严', 'unbreakable majesty', '60 秒内受到的伤害 ×0.7、心智抗性 +30%。消耗 2 点乐韵，冷却 1 天。迷惑学院，吟游诗人 15 级。', 'For 60 seconds damage taken ×0.7 and mind resistance +30%. Costs 2 melody, cooldown 1 day. College of Glamour, bard level 15.',
  'LP/Classes/Bard/MasterFlourish', '<cooldownTicksRange>60000</cooldownTicksRange>\n    <verbProperties><warmupTime>0.5</warmupTime></verbProperties>',
  '<effect>S7_Buff</effect><cost>2</cost><duration>3600</duration><hediff>LP_GL_UnbreakBuff</hediff><stat>' + B + '</stat>'),
 # 赏金猎人
 ('LP_BH_Mark', 'Melee', '悬赏标记', 'bounty mark', '标记 30 格内一个敌人 60 秒：你对它的伤害 ×1.2。消耗 1 点胆识，冷却 30 秒。赏金猎人，枪手 3 级。', 'Mark an enemy within 30 cells for 60 seconds: your damage to it is ×1.2. Costs 1 grit, cooldown 30 seconds. Bounty Hunter, gunslinger level 3.',
  'LP/Classes/Subclass/DreadAmbusher', '<cooldownTicksRange>1800</cooldownTicksRange>\n    <verbProperties><range>30</range><warmupTime>0.25</warmupTime></verbProperties>',
  '<effect>S7_BHMark</effect><cost>1</cost><duration>3600</duration><stat>' + G + '</stat>'),
 ('LP_BH_Tracker', 'Self', '追踪好手', 'expert tracker', '被动：移动 +0.2、射击精度 +1。赏金猎人，枪手 3 级。', 'Passive: move speed +0.2, shooting accuracy +1. Bounty Hunter, gunslinger level 3.',
  'LP/Classes/Subclass/AspectOfBeast', '', passive(G)),
 ('LP_BH_Bola', 'Melee', '缚足射击', 'hobbling shot', '缠住 20 格内一个敌人的腿：8 秒内移动 ×0.5。消耗 1 点胆识，冷却 20 秒。赏金猎人，枪手 7 级。', 'Tangle the legs of an enemy within 20 cells: move speed ×0.5 for 8 seconds. Costs 1 grit, cooldown 20 seconds. Bounty Hunter, gunslinger level 7.',
  'LP/Classes/Fighter/SuppressingFire', '<cooldownTicksRange>1200</cooldownTicksRange>\n    <verbProperties><range>20</range><warmupTime>0.25</warmupTime></verbProperties>',
  '<effect>S7_BHBola</effect><cost>1</cost><duration>480</duration><stat>' + G + '</stat>'),
 ('LP_BH_Payday', 'Self', '赏金到手', 'payday', '被动：被你标记的敌人死亡时，你回 1 点胆识并治疗 10 点伤势。赏金猎人，枪手 11 级。', 'Passive: when an enemy you marked dies, you regain 1 grit and heal 10. Bounty Hunter, gunslinger level 11.',
  'LP/Classes/Fighter/SurgeOfBattle', '', passive(G)),
 ('LP_BH_DeadOrAlive', 'Self', '生死不论', 'dead or alive', '被动：悬赏标记的伤害倍率 ×1.2 → ×1.4，标记持续 120 秒。赏金猎人，枪手 15 级。', 'Passive: bounty mark damage multiplier ×1.2 → ×1.4, and the mark lasts 120 seconds. Bounty Hunter, gunslinger level 15.',
  'LP/Classes/Fighter/Whirlwind', '', passive(G)),
 # 预知者
 ('LP_SR_Foresight', 'Self', '预知闪避', 'foresight', '被动：近战闪避 +8、射击精度 +1。预知者，心灵术士 3 级。', 'Passive: melee dodge +8, shooting accuracy +1. Seer, psion level 3.',
  'LP/Classes/Subclass/ShadowyDodge', '', passive(S)),
 ('LP_SR_Premonition', 'Ally', '危机预感', 'premonition', '提醒 20 格内一名友军（或自己）：30 秒内受到的伤害 ×0.8、近战闪避 +10。消耗 1 点灵能点，冷却 10 秒。预知者，心灵术士 3 级。',
  'Warn an ally within 20 cells (or yourself): for 30 seconds damage taken ×0.8 and melee dodge +10. Costs 1 psi point, cooldown 10 seconds. Seer, psion level 3.',
  'LP/Classes/Subclass3/CosmicOmen', '<displayGizmoWhileUndrafted>true</displayGizmoWhileUndrafted>\n    <cooldownTicksRange>600</cooldownTicksRange>\n    <verbProperties><range>20</range><warmupTime>0.25</warmupTime></verbProperties>',
  '<effect>S7_SRWarn</effect><cost>0</cost><duration>1800</duration><stat>' + S + '</stat>'),
 ('LP_SR_Expose', 'Melee', '洞悉破绽', 'expose weakness', '看穿 24 格内一个敌人的破绽：20 秒内它受到的伤害 ×1.15。消耗 1 点灵能点，冷却 15 秒。预知者，心灵术士 6 级。',
  'See through an enemy within 24 cells: for 20 seconds it takes ×1.15 damage. Costs 1 psi point, cooldown 15 seconds. Seer, psion level 6.',
  'LP/Classes/Subclass/DreadAmbusher', '<cooldownTicksRange>900</cooldownTicksRange>\n    <verbProperties><range>24</range><warmupTime>0.25</warmupTime></verbProperties>',
  '<effect>S7_SRExpose</effect><cost>0</cost><duration>1200</duration><stat>' + S + '</stat><psychic>true</psychic>'),
 ('LP_SR_ThirdEye', 'Self', '天眼', 'third eye', '被动：心智抗性 +20%、心灵敏感度 +15%。预知者，心灵术士 6 级。', 'Passive: mind resistance +20%, psychic sensitivity +15%. Seer, psion level 6.',
  'LP/Classes/Subclass/IronMind', '', passive(S)),
 ('LP_SR_Precog', 'Self', '先知先觉', 'precognition', '被动：每 10 秒完全躲开一次敌人的攻击。预知者，心灵术士 10 级。', 'Passive: fully avoid one enemy attack every 10 seconds. Seer, psion level 10.',
  'LP/Classes/Subclass3/FullOfStars', '', passive(S)),
 ('LP_SR_Rewind', 'Self', '命运改写', 'rewrite fate', '把时间拨回去：自己和 8 格内的友军各治疗 15 × 灵能强度 点伤势并解除眩晕。消耗 4 点灵能点，冷却 1 天。预知者，心灵术士 14 级。',
  'Turn time back: you and allies within 8 cells each heal 15 × psionic power and are freed from stun. Costs 4 psi points, cooldown 1 day. Seer, psion level 14.',
  'LP/Classes/WizardSpells/TeleportationCircle', '<displayGizmoWhileUndrafted>true</displayGizmoWhileUndrafted>\n    <cooldownTicksRange>60000</cooldownTicksRange>\n    <verbProperties><warmupTime>1</warmupTime></verbProperties>',
  '<effect>S7_SRRewind</effect><cost>0</cost><radius>8</radius><power>15</power><stat>' + S + '</stat>'),
]

def read(path):
    with io.open(path, encoding='utf-8') as f:
        return f.read()

def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write(text)

def choices_of(sub):
    for fn in ('ClassBard.xml', 'ClassPsion.xml'):
        t = read(P('Defs', fn))
        m = re.search(r'<defName>' + sub + r'</defName>.*?(<choices>.*?</choices>)', t, re.S)
        if m and '</LichPhylactery.LP_SubclassDef>' not in m.group(0):
            return m.group(1), fn
    raise Exception('choices not found: ' + sub)

def main():
    problems = []
    abil = {a[0]: a for a in ABILITIES}
    hed = {h[0] for h in HEDIFFS}
    for n in list(abil) + list(hed) + [s[0] for s in SUBS]:
        if n[-1].isdigit():
            problems.append('defName ends in a digit: ' + n)
    # 已有定义不能重名
    existing = ''
    for fn in os.listdir(P('Defs')):
        if fn.endswith('.xml') and fn != 'Subclasses7.xml':
            existing += read(P('Defs', fn))
    for n in list(abil) + list(hed) + [s[0] for s in SUBS]:
        if '<defName>' + n + '</defName>' in existing:
            problems.append('defName already exists: ' + n)
    for a in ABILITIES:
        if not os.path.exists(P('Textures', a[6] + '.png')):
            problems.append('missing icon: ' + a[6])
        m = re.search(r'<hediff>(.*?)</hediff>', a[8])
        if m and m.group(1) not in hed:
            problems.append('unknown hediff: ' + m.group(1))
    used = [x[0] for s in SUBS for x in s[9]]
    if sorted(used) != sorted(abil):
        problems.append('abilities and subclass lists differ: ' + str(set(used) ^ set(abil)))
    for s in SUBS:
        if not os.path.exists(P('Textures', s[7] + '.png')):
            problems.append('missing emblem: ' + s[7])
    for t in [x for a in ABILITIES for x in (a[4], a[5])] + [x for s in SUBS for x in (s[3], s[4])]:
        if 'd20' in t or '掷骰' in t or '检定' in t or 'DC' in t:
            problems.append('dice wording: ' + t)
    if problems:
        print('\n'.join(problems))
        sys.exit(1)

    out = ['<?xml version="1.0" encoding="utf-8"?>',
           '<!-- 1.185.0 第七批子职业 10 个（Source/ClassSubclass7.cs）：把子职业不到 5 个的职业都补到 5 个。',
           '     武僧 醉拳宗 / 剑圣宗；野蛮人 狂热者之道 / 先祖守卫之道；游侠 妖精漫游者 / 地平线行者；德鲁伊 野火结社；吟游诗人 迷惑学院；枪手 赏金猎人；心灵术士 预知者。',
           '     能力等级沿用各职业原有节奏。被动（effect=Passive）和新效果（S7_ 开头）都在 ClassSubclass7.cs。图标、徽记先借用已有贴图。',
           '     这个文件由 ArtSource/Subclasses7/gen_subclasses7.py 生成，改内容请改脚本后重跑。 -->',
           '<Defs>', '  <!-- ===================== 子职业 ===================== -->']
    en_sub = ['<?xml version="1.0" encoding="utf-8"?>', '<LanguageData>']
    for (name, zh, en, dzh, den, parent, order, emblem, color, abilities, copy) in SUBS:
        out.append('  <LichPhylactery.LP_SubclassDef>')
        out.append('    <defName>' + name + '</defName>')
        out.append('    <label>' + zh + '</label>')
        out.append('    <description>' + esc(dzh) + '</description>')
        out.append('    <parent>' + parent + '</parent><order>' + str(order) + '</order><emblem>' + emblem + '</emblem><color>' + color + '</color>')
        out.append('    <abilities>' + ''.join('<li><ability>' + a + '</ability><level>' + str(l) + '</level></li>' for a, l in abilities) + '</abilities>')
        en_sub.append('  <!-- ' + zh + ' -->')
        en_sub.append('  <' + name + '.label>' + esc(en) + '</' + name + '.label>')
        en_sub.append('  <' + name + '.description>' + esc(den) + '</' + name + '.description>')
        if copy:
            block, fn = choices_of(copy)
            out.append('    ' + block)
            src = read(P('Languages', 'English', 'DefInjected', 'LichPhylactery.LP_SubclassDef', fn))
            lines = [l for l in src.split('\n') if l.strip().startswith('<' + copy + '.choices.')]
            en_sub += [l.rstrip('\r').replace(copy + '.', name + '.') for l in lines]
        out.append('  </LichPhylactery.LP_SubclassDef>')
    out.append('')
    out.append('  <!-- ===================== 状态 ===================== -->')
    en_h = ['<?xml version="1.0" encoding="utf-8"?>', '<LanguageData>']
    for (name, kind, zh, en, dzh, den, color, stage) in HEDIFFS:
        parent = 'LP_S6PassiveBase' if kind == 'passive' else 'LP_ClassBuffBase'
        out.append('  <HediffDef ParentName="' + parent + '">')
        cls = '<hediffClass>LichPhylactery.Hediff_ClassMark</hediffClass>' if kind == 'mark' else ''
        out.append('    <defName>' + name + '</defName>' + cls + '<label>' + zh + '</label><description>' + esc(dzh) + '</description>')
        out.append('    ' + ('<isBad>true</isBad>' if kind in ('mark', 'bad') else '') + '<defaultLabelColor>' + color + '</defaultLabelColor>')
        if stage:
            out.append('    <stages><li>' + stage + '</li></stages>')
        out.append('  </HediffDef>')
        en_h.append('  <!-- ' + zh + ' -->')
        en_h.append('  <' + name + '.label>' + esc(en) + '</' + name + '.label>')
        en_h.append('  <' + name + '.description>' + esc(den) + '</' + name + '.description>')
    out.append('')
    out.append('  <!-- ===================== 能力 ===================== -->')
    en_a = ['<?xml version="1.0" encoding="utf-8"?>', '<LanguageData>']
    for (name, base, zh, en, dzh, den, icon, extra, comp) in ABILITIES:
        out.append('  <AbilityDef ParentName="LP_Class' + base + 'Base">')
        out.append('    <defName>' + name + '</defName>')
        out.append('    <label>' + zh + '</label>')
        out.append('    <description>' + esc(dzh) + '</description>')
        out.append('    <iconPath>' + icon + '</iconPath>')
        if extra:
            out.append('    ' + extra)
        out.append('    <comps><li Class="LichPhylactery.CompProperties_ClassEffect">' + comp + '</li></comps>')
        out.append('  </AbilityDef>')
        en_a.append('  <!-- ' + zh + ' -->')
        en_a.append('  <' + name + '.label>' + esc(en) + '</' + name + '.label>')
        en_a.append('  <' + name + '.description>' + esc(den) + '</' + name + '.description>')
    out.append('</Defs>')
    for l in (en_sub, en_h, en_a):
        l.append('</LanguageData>')
    write(P('Defs', 'Subclasses7.xml'), '\n'.join(out) + '\n')
    write(P('Languages', 'English', 'DefInjected', 'LichPhylactery.LP_SubclassDef', 'Subclasses7.xml'), '\n'.join(en_sub) + '\n')
    write(P('Languages', 'English', 'DefInjected', 'HediffDef', 'Subclasses7.xml'), '\n'.join(en_h) + '\n')
    write(P('Languages', 'English', 'DefInjected', 'AbilityDef', 'Subclasses7.xml'), '\n'.join(en_a) + '\n')
    print('OK: %d subclasses, %d abilities, %d hediffs' % (len(SUBS), len(ABILITIES), len(HEDIFFS)))

main()
