# 2026-11-19 心灵术士、枪手的定义生成器（设计文档：心灵术士 https://claude.ai/artifact/4YimhXHHjfazx191WSatmP，枪手 https://claude.ai/artifact/16RyAa8tNCuDLgMGBoJbAF）。
# 用法：python -I gen_newclasses.py <模组根目录>　→ 写 Defs/ClassPsion.xml、Defs/ClassGunslinger.xml。改数值请改这里再重新生成，不要手改生成的 XML。
import sys, os
from xml.sax.saxutils import escape as X

ROOT = sys.argv[1] if len(sys.argv) > 1 else '.'

def comp(effect, stat, cost=0, power=None, duration=None, radius=None, hediff=None, psychic=False, passive=False, extra=''):
    s = f'<li Class="LichPhylactery.CompProperties_ClassEffect"><effect>{effect}</effect>'
    if passive: s += '<passive>true</passive>'
    else: s += f'<cost>{cost}</cost>'
    if power is not None: s += f'<power>{power}</power>'
    if duration is not None: s += f'<duration>{duration}</duration>'
    if radius is not None: s += f'<radius>{radius}</radius>'
    if hediff: s += f'<hediff>{hediff}</hediff>'
    s += f'<stat>{stat}</stat>'
    if psychic: s += '<psychic>true</psychic>'
    return s + extra + '</li>'

def ability(defName, label, desc, icon, parent, comps, cd=None, rng=None, warm=None, undrafted=False, hostile=None, verb_extra='', ext=''):
    out = [f'  <AbilityDef ParentName="{parent}">', f'    <defName>{defName}</defName>', f'    <label>{X(label)}</label>',
           f'    <description>{X(desc)}</description>', f'    <iconPath>{icon}</iconPath>']
    if undrafted: out.append('    <displayGizmoWhileUndrafted>true</displayGizmoWhileUndrafted>')
    if hostile is not None: out.append(f'    <hostile>{"true" if hostile else "false"}</hostile>')
    if cd: out.append(f'    <cooldownTicksRange>{cd}</cooldownTicksRange>')
    v = ''
    if rng is not None: v += f'<range>{rng}</range>'
    if warm is not None: v += f'<warmupTime>{warm}</warmupTime>'
    v += verb_extra
    if v: out.append(f'    <verbProperties>{v}</verbProperties>')
    out.append(f'    <comps>{comps}</comps>')
    if ext: out.append(f'    <modExtensions>{ext}</modExtensions>')
    out.append('  </AbilityDef>')
    return '\n'.join(out)

def passive(defName, label, desc, icon, stat):
    return ability(defName, label, desc, icon, 'LP_ClassSelfBase', comp('Passive', stat, passive=True))

def hediff(defName, label, desc, parent='LP_ClassBuffBase', stages='', cls=None, bad=False, color=None, extra=''):
    out = [f'  <HediffDef ParentName="{parent}">', f'    <defName>{defName}</defName>']
    if cls: out.append(f'    <hediffClass>{cls}</hediffClass>')
    out += [f'    <label>{X(label)}</label>', f'    <description>{X(desc)}</description>']
    if bad: out.append('    <isBad>true</isBad>')
    if color: out.append(f'    <defaultLabelColor>{color}</defaultLabelColor>')
    if stages: out.append(f'    <stages>{stages}</stages>')
    if extra: out.append(extra)
    out.append('  </HediffDef>')
    return '\n'.join(out)

def st(factors=None, offsets=None, label=None, minsev=None, capmods=''):
    s = '<li>'
    if minsev is not None: s += f'<minSeverity>{minsev}</minSeverity>'
    if label: s += f'<label>{X(label)}</label>'
    if factors: s += '<statFactors>' + ''.join(f'<{k}>{v}</{k}>' for k, v in factors.items()) + '</statFactors>'
    if offsets: s += '<statOffsets>' + ''.join(f'<{k}>{v}</{k}>' for k, v in offsets.items()) + '</statOffsets>'
    s += capmods
    return s + '</li>'

def tiers(fn, labels=('一档', '二档', '三档', '四档')):
    return ''.join(st(minsev=(0 if i == 0 else i + .5), label=labels[i], **fn(i + 1)) for i in range(4))

def feat(defName, label, desc, icon, order, cls, lv, stat, bonus, extra, abil):
    return '\n'.join([
        '  <LichPhylactery.LP_FeatDef ParentName="LP_ClassFeatBase">', f'    <defName>{defName}</defName>', f'    <label>{X(label)}</label>',
        f'    <description>{X(desc)}</description>', f'    <iconPath>{icon}</iconPath>', f'    <order>{order}</order>',
        f'    <requiredClass>{cls}</requiredClass><classLevel>{lv}</classLevel>',
        f'    <powerStats><li>{stat}</li></powerStats><powerBonus>{bonus}</powerBonus>', extra,
        '    <bonusAbilities>' + ''.join(f'<li>{a}</li>' for a in abil) + '</bonusAbilities>', '  </LichPhylactery.LP_FeatDef>'])


import shutil
def icon_copy(src, name):
    dst = 'LP/Classes/CasterSpells/' + name
    a = os.path.join(ROOT, 'Textures', src + '.png'); b = os.path.join(ROOT, 'Textures', dst + '.png')
    if not os.path.exists(b): shutil.copyfile(a, b)
    return dst

RING_PRICE = {6: 8000, 7: 12000, 8: 16000, 9: 25000}
RING_LV = {6: 11, 7: 13, 8: 15, 9: 17}
def crystal(key, ability_def, label, cls_label, ring, desc, color):
    full = f'魔力湖泊的职业水晶。{cls_label}吸收后永久学会秘传「{label}」（{ring} 环，需要{cls_label} {RING_LV[ring]} 级，占 1 个秘传槽；冒险者 11 / 15 / 20 级、传奇 30 级各有 1 个秘传槽）。\\n\\n{desc}'
    return '\n'.join(['  <ThingDef ParentName="LP_ArcanumCrystalBase">', f'    <defName>LP_Crystal_{key}</defName>', f'    <label>职业水晶：{X(label)}</label>',
                      f'    <description>{X(full)}</description>', f'    <graphicData><color>{color}</color></graphicData>',
                      f'    <statBases><MarketValue>{RING_PRICE[ring]}</MarketValue></statBases>',
                      f'    <comps><li Class="LichPhylactery.CompProperties_Arcanum"><ability>{ability_def}</ability></li></comps>', '  </ThingDef>'])

def arcext(ring, cls): return f'<li Class="LichPhylactery.ArcanumExt"><ring>{ring}</ring><cls>{cls}</cls></li>'
def spellext(circle, school, extra=''): return f'<li Class="LichPhylactery.CasterSpellExt"><circle>{circle}</circle><school>{school}</school>{extra}</li>'

def write(name, header, parts):
    path = os.path.join(ROOT, 'Defs', name)
    with open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('<?xml version="1.0" encoding="utf-8"?>\n<!-- ' + header + ' -->\n<Defs>\n' + '\n\n'.join(parts) + '\n</Defs>\n')
    print('wrote', path)

# =====================================================================================
# 心灵术士
# =====================================================================================
PS = 'LP_PsionicPower'; PC = 'LP_Class_Psion'; PI = 'LP/Classes/'
def ps_choices(extra=''):
    opts = ['生物反馈', '预知增幅', '毁灭思绪', '魔鬼之舌', '扩展感知', '心智暗示', '无误瞄准', '观察入微', '灵能反噬', '灵能守护', '锐化心智', '念动精修']
    tips = ['施放死灵 / 变化系法术（1 环以上）时，花最多 智力调整值 点灵能点：得到（灵能值合计 + 智力调整值）× 灵能强度 的临时生命。',
            '施放防护 / 预言系法术时花 1 点：自己或那名友军 10 秒内命中、闪避提高（按灵能档 +15 / 20 / 25 / 30%）。',
            '施放咒法 / 塑能系法术打中敌人时，花最多 智力调整值 点：目标另受（灵能值合计 + 智力调整值）× 灵能强度 心灵伤害，不管豁免成败。',
            '被动（有灵能点时）：社交影响、谈判能力 +10 / 15 / 20 / 25%，交易价格好一点。',
            '得到能力「扩展感知」：花 1 点，2 小时内视觉 +15%、射击精度提高、看得见隐形的单位。',
            '施放惑控 / 幻术系法术前花 1 点：目标的心智抗性暂时 −8 / 10 / 13 / 15%。',
            '被动（有灵能点时）：射击精度、近战命中 +5 / 7 / 9 / 11%。',
            '研究时自动花 1 点：4 小时内研究速度 +30 / 40 / 50 / 60%（每天最多 2 次）。',
            '被打中时自动花 1 点（每 10 秒一次）：伤害 −（2 × 灵能值 + 智力调整值），攻击者没抵抗住就受等量心灵伤害。',
            '得到能力「灵能守护」：花 1 点，30 秒内免疫恐惧、心智抗性 +25%。',
            '得到能力「锐化心智」：花 1 点，60 秒内你造成的心灵伤害 ×1.3。',
            '念动推拉距离 12 → 20 格、能推体型 2 的目标；不花点时推 2 格。']
    o = ''.join(f'<li>{x}</li>' for x in opts); t = ''.join(f'<li>{X(x)}</li>' for x in tips)
    rows = [('psd1', '灵能修行', 3, ''), ('psd2', '第二修行', 3, '<distinctFrom>psd1</distinctFrom>'), ('psd3', '第三修行', 5, ''), ('psd4', '第四修行', 10, ''), ('psd5', '第五修行', 13, ''), ('psd6', '第六修行', 17, '')]
    s = '<choices>' + ''.join(f'<li><key>{k}</key><label>{l}</label><level>{lv}</level>{d}<options>{o}</options><tips>{t}</tips></li>' for k, l, lv, d in rows)
    return s + extra + '</choices>'

def psion():
    P = []
    P.append(f'''  <LichPhylactery.LP_ClassDef>
    <defName>{PC}</defName>
    <label>心灵术士</label>
    <description>用纯粹心灵之力施法，念动推拉、心灵感应，还能借神经热续航。\\n强度：「灵能强度」（主属性智力，技能智识）；伤害与治疗另乘心灵敏感度。心灵失聪的人不能学。</description>
    <mainSkill>Intellectual</mainSkill>
    <alternateSkill>Social</alternateSkill>
    <primary>Int</primary>
    <order>8</order>
    <subclassLevel>3</subclassLevel>
    <resource>Mana</resource>
    <caster>true</caster>
    <background>LP/Classes/Background_Wizard</background>
    <color>(0.55,0.75,0.95)</color>
    <abilities>
      <li><ability>LP_PS_Propel</ability><level>1</level></li>
      <li><ability>LP_PS_Telepathy</ability><level>1</level></li>
      <li><ability>LP_PS_MindHand</ability><level>1</level></li>
      <li><ability>LP_PS_Awakening</ability><level>1</level></li>
      <li><ability>LP_PS_Restoration</ability><level>5</level></li>
      <li><ability>LP_PS_Surge</ability><level>7</level></li>
      <li><ability>LP_PS_Resonance</ability><level>10</level></li>
      <li><ability>LP_PS_Reserves</ability><level>13</level></li>
      <li><ability>LP_PS_Enkindle</ability><level>15</level><capstoneSkill>Intellectual</capstoneSkill></li>
    </abilities>
    <legendary><li>LP_LegPsychicCatastrophe</li><li>LP_LegTranscendentMind</li></legendary>
  </LichPhylactery.LP_ClassDef>''')
    P.append(f'''  <StatDef ParentName="LP_ClassPowerBase">
    <defName>{PS}</defName>
    <label>灵能强度</label>
    <description>心灵术士能力与法术的强度倍率：0.5 + 智识等级/20，另加冒险者智力调整值 × 0.05 与 (熟练加值 − 2) × 0.02、心灵术士等职业等级合计每比 1 高 1 级 +2%（最多 +40%）。灵能值（灵能档范围里随机）也乘它。</description>
    <parts><li Class="LichPhylactery.StatPart_ClassPower"><skill>Intellectual</skill><ability>Int</ability></li></parts>
    <displayPriorityInCategory>4219</displayPriorityInCategory>
  </StatDef>''')
    C = '(0.55,0.75,0.95)'
    H = []
    H.append(hediff('LP_PS_Pool', '灵能', '心灵术士的灵能池：括号里是现有的灵能点。睡醒回满，连续 1 小时冥想、娱乐或睡觉回 1 点。', 'LP_SubclassPassiveBase', cls='LichPhylactery.Hediff_PsionPool', color=C))
    H.append(hediff('LP_PS_MindHand', '念力之手', '看不见的念力帮你搬东西：负重提高。', 'LP_SubclassPassiveBase', color=C,
                    stages=st(minsev=0, label='心灵术士', factors={'CarryingCapacity': 1.15}) + st(minsev=1.5, label='强化念力', factors={'CarryingCapacity': 1.4})))
    H.append(hediff('LP_PS_Resonance', '念灵共鸣', '心灵术士的心智与心灵链接共鸣：神经热上限、神经热消散提高。', 'LP_SubclassPassiveBase', color=C,
                    stages=st(minsev=0, label='一', offsets={'PsychicEntropyMax': 5}, factors={'PsychicEntropyRecoveryRate': 1.1}) + st(minsev=1.5, label='二', offsets={'PsychicEntropyMax': 10}, factors={'PsychicEntropyRecoveryRate': 1.2})))
    H.append(hediff('LP_PS_DevilTongue', '魔鬼之舌', '灵能修行：话语里带着心灵暗示（有灵能点时）。', 'LP_SubclassPassiveBase', color=C,
                    stages=tiers(lambda t: {'offsets': {'SocialImpact': round(.05 + .05 * t, 2), 'NegotiationAbility': round(.05 + .05 * t, 2), 'TradePriceImprovement': round(.01 * t, 2)}})))
    H.append(hediff('LP_PS_InerrantAim', '无误瞄准', '灵能修行：念力悄悄修正每一次攻击（有灵能点时）。', 'LP_SubclassPassiveBase', color=C,
                    stages=tiers(lambda t: {'offsets': {'ShootingAccuracyPawn': round(.03 + .02 * t, 2), 'MeleeHitChance': round(.03 + .02 * t, 2)}})))
    H.append(hediff('LP_PS_Kinetic', '念动之力', '念动师：念力托着工具，采矿更快。', 'LP_SubclassPassiveBase', color=C, stages=st(factors={'MiningSpeed': 1.15})))
    H.append(hediff('LP_PS_Observant', '观察入微', '灵能修行：心智全开地钻研，研究速度提高。', color=C, stages=tiers(lambda t: {'factors': {'ResearchSpeed': round(1.2 + .1 * t, 2)}})))
    H.append(hediff('LP_PS_Precog', '预知增幅', '预知到下一瞬间：命中、闪避提高。', color=C, stages=tiers(lambda t: {'offsets': {'ShootingAccuracyPawn': round(.1 + .05 * t, 2), 'MeleeHitChance': round(.1 + .05 * t, 2), 'MeleeDodgeChance': round(10 + 5 * t)}})))
    H.append(hediff('LP_PS_IdInsinuation', '心智暗示', '心灵术士在心里埋了一颗种子：心智抗性暂时下降。', bad=True, color=C, stages=tiers(lambda t: {'offsets': {'LP_MindResist': -[.08, .10, .13, .15][t - 1]}})))
    H.append(hediff('LP_PS_SurgeOn', '灵能涌动', '灵能涌动开着：灵能值的下限提到范围的 60%，每次多加神经热 6（没有心灵链接就扣休息值）。再按一次关掉。', 'LP_SubclassPassiveBase', color=C))
    H.append(hediff('LP_PS_Enkindled', '燃魂', '燃烧生命力换取灵能：每次用到灵能值，另加 1～2 份（每份神经热 10）。', color=C))
    H.append(hediff('LP_PS_Sensed', '被心灵感知', '心灵术士感知到了这个心智：藏不住了。', bad=True, color=C, stages=st(offsets={'MeleeDodgeChance': -5})))
    H.append(hediff('LP_PS_AwarenessBuff', '扩展感知', '感官向四面八方延伸：视觉提高、射击更准，看得见隐形的单位。', color=C, stages=st(offsets={'ShootingAccuracyPawn': .05}, capmods='<capMods><li><capacity>Sight</capacity><offset>0.15</offset></li></capMods>')))
    H.append(hediff('LP_PS_GuardsBuff', '灵能守护', '心智上了锁：免疫恐惧，心智抗性 +25%。', color=C, stages=st(offsets={'LP_MindResist': .25})))
    H.append(hediff('LP_PS_SharpenedBuff', '锐化心智', '心灵之刃磨得飞快：你造成的心灵伤害 ×1.3。', color=C))
    H.append(hediff('LP_PS_Mutable', '柔变之躯', '血肉塑形者把身体捏得更适合施法：移动 +10%。', color=C, stages=st(factors={'MoveSpeed': 1.1})))
    H.append(hediff('LP_PS_FleshWeaveBuff', '血肉编织', '血肉层层织密：受到的伤害 ×0.85。', color=C, stages=st(factors={'IncomingDamageFactor': .85})))
    H.append(hediff('LP_PS_EvolvedStone', '石化表皮', '进化之躯（柔变之躯期间）：皮肤硬如岩石，受到的伤害 ×0.75。', 'LP_SubclassPassiveBase', color=C, stages=st(factors={'IncomingDamageFactor': .75})))
    H.append(hediff('LP_PS_EvolvedStride', '超凡步态', '进化之躯（柔变之躯期间）：移动 +25%。', 'LP_SubclassPassiveBase', color=C, stages=st(factors={'MoveSpeed': 1.25})))
    H.append(hediff('LP_PS_EvolvedFlex', '非凡柔韧', '进化之躯（柔变之躯期间）：身体软得能钻过缝，近战闪避 +10。', 'LP_SubclassPassiveBase', color=C, stages=st(offsets={'MeleeDodgeChance': 10})))
    tool = lambda lab, cap, pw, cd: f'<li><label>{lab}</label><capacities><li>{cap}</li></capacities><power>{pw}</power><cooldownTime>{cd}</cooldownTime></li>'
    H.append(hediff('LP_PS_FormBlade', '骨刃', '血肉塑形者的生体武器：前臂长出骨刃（锐器近战）。再用一次生体武器切换形态。', 'LP_SubclassPassiveBase', color=C,
                    extra=f'    <comps><li Class="HediffCompProperties_VerbGiver"><tools>{tool("骨刃", "Cut", 14, 1.6)}</tools></li></comps>'))
    H.append(hediff('LP_PS_FormMaul', '血肉巨锤', '血肉塑形者的生体武器：拳头和前臂凝成一团硬肉（钝器近战）。再用一次生体武器切换形态。', 'LP_SubclassPassiveBase', color=C,
                    extra=f'    <comps><li Class="HediffCompProperties_VerbGiver"><tools>{tool("血肉巨锤", "Blunt", 16, 2)}</tools></li></comps>'))
    H.append(hediff('LP_PS_FormLauncher', '内脏弩', '血肉塑形者的生体武器：手臂变成一把肌腱弩，用「内脏弩」能力射击。再用一次生体武器切换形态（或收起）。', 'LP_SubclassPassiveBase', color=C))
    H.append(hediff('LP_PS_TranceBuff', '毁灭恍惚', '念力托着身子：移动 +20%，法术与能力的伤害另加 灵能值 × 2。', color=C, stages=st(factors={'MoveSpeed': 1.2})))
    H.append(hediff('LP_PS_Slowed', '念力碾压', '被念力压着：移动 ×0.5。', bad=True, color=C, stages=st(factors={'MoveSpeed': .5})))
    H.append(hediff('LP_PS_BulwarkBuff', '堤垒心智', '心智筑起堤坝：受到的心灵伤害 ×0.5，心智抗性提高。', color=C, stages=tiers(lambda t: {'offsets': {'LP_MindResist': round(.1 + .05 * t, 2)}})))
    H.append(hediff('LP_PS_Linked', '心灵网络', '心灵感应者把附近的同伴连在一起：互相提醒，射击、近战更准一点。', color=C, stages=st(offsets={'ShootingAccuracyPawn': .05, 'MeleeHitChance': .05})))
    H.append(hediff('LP_PS_Probed', '被读过心', '最近被心灵术士翻过心思（1 天内不能再潜入）。', color=C))
    H.append(hediff('LP_PS_Fortress', '智识壁垒', '心智被壁垒护住：受到的心灵伤害 ×0.5，心智抗性 +25%。', color=C, stages=st(offsets={'LP_MindResist': .25})))
    H.append(hediff('LP_PS_EgoWhipped', '自我鞭笞', '自我被鞭笞得支离破碎：射击、近战命中 −20%，意识 −10%。', bad=True, color=C, stages=st(offsets={'ShootingAccuracyPawn': -.2, 'MeleeHitChance': -.2}, capmods='<capMods><li><capacity>Consciousness</capacity><offset>-0.1</offset></li></capMods>')))
    H.append(hediff('LP_PS_Static', '突触静电', '脑子里噼啪作响：意识 −15%，命中 −10%。', bad=True, color=C, stages=st(offsets={'ShootingAccuracyPawn': -.1, 'MeleeHitChance': -.1}, capmods='<capMods><li><capacity>Consciousness</capacity><offset>-0.15</offset></li></capMods>')))
    H.append(hediff('LP_LegTranscendentBuff', '超凡心智', '传奇心灵术士：灵能值永远至少是范围的 60%（灵能涌动不再加神经热），灵能点上限 +4。', 'LP_SubclassPassiveBase', color='(0.75,0.85,1)'))
    H.append(hediff('LP_LegPsychicCatastropheBuff', '心灵天灾', '传奇心灵术士：修行、职业与子职业能力不扣灵能点，每次改加神经热 4。', color='(0.75,0.85,1)'))
    P.append('  <!-- ===== 状态 ===== -->\n' + '\n'.join(H))

    A = []
    W = PI + 'Warlock/'; WS = PI + 'WizardSpells/'; S2 = PI + 'Subclass2/'; S3 = PI + 'Subclass3/'; CS = PI + 'CasterSpells/'
    A.append(ability('LP_PS_Propel', '念动推拉', '把 12 格内一个中型以下的生物（体型 1.6 以下）推开或拉近：敌人对抗心智抗性，没抵抗住就被推开 1 格；友军被拉近。有灵能点时自动花 1 点，改推 灵能值/2 格，撞墙、撞人另受 灵能值 × 灵能强度 钝击（目标抵抗住就不扣点）。冷却 6 秒。心灵术士 1 级。',
                     S2 + 'Shove', 'LP_ClassMeleeBase', comp('PSPropel', PS, psychic=True), cd=360, rng=20, warm=0.25))
    A.append(ability('LP_PS_Telepathy', '心灵感应', '把感知铺开：灵能值 × 4 格内所有有心智的敌人（机械体除外）现形 1 小时（解除心灵隐形），被感知的敌人更难躲闪。每天第一次免费，之后花 1 点灵能点。冷却 30 秒。心灵术士 1 级。',
                     W + 'AwakenedMind', 'LP_ClassSelfBase', comp('PSTelepathy', PS, psychic=True), cd=1800, undrafted=True))
    A.append(passive('LP_PS_MindHand', '念力之手', '被动：看不见的念力帮你搬东西，负重 +15%（念动师 +40%）。心灵术士 1 级。', WS + 'MageHand', PS))
    A.append(passive('LP_PS_Awakening', '心灵觉醒', '被动：没有心灵链接的人得到 1 级心灵链接；心灵术士 7 / 13 / 19 级时心灵链接至少 2 / 3 / 4 级（只升不降，不超过 6 级，设置里可关）。灵能点用完时，有心灵链接的人每 1 点改加神经热 12（放不下就不能用）。心灵术士 1 级。', W + 'ThoughtShield', PS))
    A.append(ability('LP_PS_Restoration', '灵能复苏', '原地静坐 1 小时（40 秒）调息：结束时灵能点回满，精神力 +10%。每天一次。心灵术士 5 级。',
                     S2 + 'CalmMind', 'LP_ClassSelfBase', comp('PSRestore', PS), cd=60000, warm=40, undrafted=True))
    A.append(ability('LP_PS_Surge', '灵能涌动', '开关：开着时灵能值的下限提到范围的 60%（一档 2～8 → 5.6～8），每次用到灵能值多加神经热 6（没有心灵链接就扣 3% 休息值）。心灵术士 7 级。',
                     S2 + 'PsiAdept', 'LP_ClassSelfBase', comp('PSSurge', PS), cd=60, warm=0, undrafted=True))
    A.append(passive('LP_PS_Resonance', '念灵共鸣', '被动：神经热上限 +5、神经热消散 +10%（心灵术士 10 级起 +10 / +20%），心灵术士也是最好的原版心灵法术使用者。心灵术士 10 级。', S2 + 'PsychicDefense', PS))
    A.append(passive('LP_PS_Reserves', '灵能储备', '被动：被征召时灵能点少于 4 就补到 4，每天一次（心灵术士 18 级起生效）。心灵术士 13 级。', S2 + 'ProtectiveField', PS))
    A.append(ability('LP_PS_Enkindle', '燃魂', '终极：60 秒内燃烧生命力换取灵能：每次用到灵能值，另加 1～2 份（合计最多 ×3），每多一份神经热 10（放不下就不加；神经热超过上限会像原版一样心灵过热）。消耗 2 点灵能点，冷却 1 天。心灵术士 15 级，另要智识 15 级。',
                     PI + 'Legendary/ArcaneSurge', 'LP_ClassSelfBase', comp('PSEnkindle', PS, duration=3600, hediff='LP_PS_Enkindled'), cd=60000, warm=0.5))
    # 修行给的能力
    A.append(ability('LP_PS_Awareness', '扩展感知', '灵能修行：2 小时内视觉 +15%、射击精度 +5%，看得见隐形的单位。消耗 1 点灵能点，冷却 1 分钟。', S3 + 'Opportunist', 'LP_ClassSelfBase',
                     comp('PSAwareness', PS, duration=5000, hediff='LP_PS_AwarenessBuff'), cd=1800, undrafted=True))
    A.append(ability('LP_PS_Guards', '灵能守护', '灵能修行：30 秒内免疫恐惧（身上的恐惧立刻解除），心智抗性 +25%。消耗 1 点灵能点，冷却 30 秒。', S2 + 'PsychicDefense', 'LP_ClassSelfBase',
                     comp('PSGuards', PS, duration=1800, hediff='LP_PS_GuardsBuff'), cd=1800, warm=0))
    A.append(ability('LP_PS_Sharpened', '锐化心智', '灵能修行：60 秒内你造成的心灵伤害 ×1.3。消耗 1 点灵能点，冷却 1 分钟。', S2 + 'PsychicSpike', 'LP_ClassSelfBase',
                     comp('PSSharpened', PS, duration=3600, hediff='LP_PS_SharpenedBuff'), cd=3600, warm=0))
    # 血肉塑形者
    A.append(ability('LP_PS_MutableForm', '柔变之躯', '把身体捏成施法的容器：1 小时内移动 +10%，得到（灵能值 + 智力调整值）× 2 × 灵能强度 临时生命。会血肉编织时多花 1 点、另外受到的伤害 ×0.85。消耗 1 点灵能点，冷却 1 分钟。血肉塑形者，心灵术士 3 级。',
                     S2 + 'FleshShaping', 'LP_ClassSelfBase', comp('PSMutable', PS, duration=2500), cd=3600, warm=0.5, undrafted=True))
    A.append(ability('LP_PS_OrganicWeapons', '生体武器', '把手臂捏成武器，按一次换一种：骨刃（锐器 14）→ 血肉巨锤（钝器 16）→ 内脏弩（用「内脏弩」射击）→ 收起。不占武器格，徒手时最常用到。血肉塑形者，心灵术士 3 级。',
                     S2 + 'FleshRevelation', 'LP_ClassSelfBase', comp('PSOrganic', PS), cd=120, warm=0, undrafted=True))
    A.append(ability('LP_PS_BileLauncher', '内脏弩', '用内脏弩向 20 格内一个敌人射出胆汁：10 × 灵能强度 酸蚀伤害，每秒第一下再 +80%。要先把生体武器换成内脏弩。冷却 1.5 秒。血肉塑形者，心灵术士 3 级。',
                     S2 + 'BloodSpear', 'LP_ClassMeleeBase', comp('PSBile', PS, power=10), cd=90, rng=20, warm=0.5))
    A.append(passive('LP_PS_FleshWeave', '血肉编织', '被动：柔变之躯多花 1 点灵能点（够的话），额外得到「血肉编织」：受到的伤害 ×0.85。血肉塑形者，心灵术士 6 级。', S2 + 'FleshPrison', PS))
    A.append(passive('LP_PS_EvolvedForm', '进化之躯', '被动：柔变之躯至少持续 4 小时，期间另按选择得到：石化表皮（受到的伤害 ×0.75）、超凡步态（移动 +25%）或非凡柔韧（近战闪避 +10）。血肉塑形者，心灵术士 10 级。', S3 + 'FormMode', PS))
    A.append(passive('LP_PS_LifeBending', '生死扭曲之刃', '被动：生体武器打中敌人时另加 灵能值 伤害（不扣点）；有灵能点时每 3 秒一次自动花 1 点，6 格内受伤的友军各回复 灵能值 + 智力调整值。血肉塑形者，心灵术士 14 级。', S3 + 'LifeStrike', PS))
    # 念动师
    A.append(passive('LP_PS_StrongTK', '强化念力', '被动：念力之手的负重加成 +15% → +40%，采矿速度 +15%。念动师，心灵术士 3 级。', S2 + 'TeleJump', PS))
    A.append(passive('LP_PS_TKTechniques', '念动技巧', '被动：念动推拉不花点时也推 2 格；推敌人时另附念力弹：灵能值 × 2 × 灵能强度 力场伤害。念动师，心灵术士 3 级。', S2 + 'Shove', PS))
    A.append(ability('LP_PS_DestructiveTrance', '毁灭恍惚', '念力托起身子：1 小时内移动 +20%，你的法术与能力（不是武器）打中敌人时另加 灵能值 × 2 伤害。消耗 1 点灵能点，冷却 1 分钟。念动师，心灵术士 6 级。',
                     S2 + 'TeleStorm', 'LP_ClassSelfBase', comp('PSTrance', PS, duration=2500, hediff='LP_PS_TranceBuff'), cd=3600, warm=0.5))
    A.append(passive('LP_PS_ReboundField', '反弹力场', '被动：护盾术挡着时被远程攻击打中，自动花 1 点：攻击者受（灵能值 + 智力调整值）× 2 × 灵能强度 力场伤害（抵抗住减半），你得到等量临时生命。念动师，心灵术士 6 级。', S2 + 'ProtectiveField', PS))
    A.append(passive('LP_PS_EnhancedCrush', '强化念力碾压', '被动：念力碾压有灵能点时自动多花 1 点：命中的敌人不管豁免成败都减速 5 秒，伤害另加 灵能值 × 2。念动师，心灵术士 10 级。', WS + 'Telekinesis', PS))
    A.append(ability('LP_PS_HeightenedTK', '高阶心灵遥控', '用念力抓起 30 格内一个敌人（不管多大）：30 × 灵能强度 力场伤害，扔出 5 格并眩晕 10 秒（剧情首领与传奇减半）。消耗 4 点灵能点，冷却 1 天。念动师，心灵术士 14 级。',
                     WS + 'Telekinesis', 'LP_ClassMeleeBase', comp('PSHeightenedTK', PS, power=30, duration=600, psychic=True), cd=60000, rng=30, warm=1))
    # 心灵感应者
    A.append(ability('LP_PS_Infiltrate', '读心', '读 20 格内一个人形的心思：看它的心情、拿手技能、特性和心智抗性。对本殖民地的囚犯花 1 点灵能点：招募抗拒 − 灵能值 × 0.5，或（抗拒已经是 0 时）意志 − 灵能值 × 0.2；同一个囚犯每天一次，对方没抵抗住就察觉不到。心灵感应者，心灵术士 3 级。',
                     W + 'DissonantWhispers', 'LP_ClassAllyBase', comp('PSInfiltrate', PS, psychic=True), cd=600, rng=20, warm=1, undrafted=True,
                     verb_extra='<targetParams><canTargetPawns>true</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetSelf>false</canTargetSelf><canTargetHumans>true</canTargetHumans><canTargetAnimals>false</canTargetAnimals><canTargetMechs>false</canTargetMechs></targetParams>'))
    A.append(passive('LP_PS_TeleDistract', '心灵干扰', '被动：心灵感应范围（12 格，强力思绪后 24 格）内的友军被敌人打中时，有 灵能档成功率 × 2（30 / 40 / 50 / 60%）改成打偏，成功才扣 1 点（每 3 秒判定一次）。心灵感应者，心灵术士 3 级。', W + 'BeguilingDefense', PS))
    A.append(ability('LP_PS_BulwarkMind', '堤垒心智', '1 小时内受到的心灵伤害 ×0.5，心智抗性 +15 / 20 / 25 / 30%（按灵能档）。消耗 1 点灵能点，冷却 1 分钟。心灵感应者，心灵术士 6 级。',
                     W + 'ThoughtShield', 'LP_ClassSelfBase', comp('PSBulwark', PS, duration=2500, hediff='LP_PS_BulwarkBuff'), cd=3600, warm=0))
    A.append(passive('LP_PS_PotentThoughts', '强力思绪', '被动：心灵感应范围 12 → 24 格；你造成的心灵伤害 + 智力调整值 × 2。心灵感应者，心灵术士 6 级。', S2 + 'PsychicSpike', PS))
    A.append(passive('LP_PS_TeleBolster', '心灵鼓舞', '被动：心灵感应范围内的同伴连成心灵网络：射击、近战命中 +5%。心灵感应者，心灵术士 10 级。', S2 + 'MindDominion', PS))
    A.append(ability('LP_PS_ScrambleMinds', '心智搅乱', '搅乱 25 格内一点半径 6 内敌人的心智：没抵抗住的人狂暴 10 秒、乱打身边的人；剧情首领与传奇只眩晕，机械体眩晕 2 秒。消耗 4 点灵能点，冷却 1 天。心灵感应者，心灵术士 14 级。',
                     WS + 'Confusion', 'LP_ClassLocBase', comp('PSScramble', PS, duration=600, radius=6, psychic=True), cd=60000, rng=25, warm=1))
    # 灵跃者
    A.append(ability('LP_PS_Teleport', '灵能瞬移', '瞬移到 12 格内看得见的空地，或和那里的一名友军换位。每小时第一次免费，之后花 1 点灵能点。冷却 3 秒。灵跃者，心灵术士 3 级。',
                     W + 'MistyEscape', 'LP_ClassLocBase', comp('PSBlink', PS), cd=180, rng=12, warm=0, hostile=False,
                     verb_extra='<targetParams><canTargetPawns>true</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetLocations>true</canTargetLocations><canTargetSelf>false</canTargetSelf></targetParams>'))
    A.append(passive('LP_PS_WarpPropel', '扭曲推拉', '被动：念动推拉多推 2 格。灵跃者，心灵术士 3 级。', S2 + 'Shove', PS))
    A.append(passive('LP_PS_Displace', '换位迷踪', '被动：被远程攻击打中时（每 10 秒一次）花 1 点，和 6 格内一个敌人换位，这一下打到它身上（剧情首领与传奇除外）。灵跃者，心灵术士 6 级。', PI + 'Rogue/ShadowStep', PS))
    A.append(ability('LP_PS_RescueWarp', '救援跃迁', '把 24 格内一个倒地的友军传到身边，它得到 灵能值 × 2 × 灵能强度 临时生命。消耗 1 点灵能点，冷却 30 秒。灵跃者，心灵术士 6 级。',
                     WS + 'DimensionDoor', 'LP_ClassAllyBase', comp('PSRescue', PS), cd=1800, rng=24, warm=0.5, undrafted=True))
    A.append(ability('LP_PS_WarpBeacon', '跃迁信标', '没有信标时在脚下放一个（花 1 点灵能点）；有信标、并且在同一张地图时，按一下就传回信标，信标消失。灵跃者，心灵术士 10 级。',
                     WS + 'TeleportationCircle', 'LP_ClassSelfBase', comp('PSBeacon', PS), cd=300, warm=0.5, undrafted=True))
    A.append(ability('LP_PS_MassWarp', '群体跃迁', '把身边 6 格内最多（智力调整值 + 2）个生物（友军自动，敌人对抗心智抗性；剧情首领与传奇不行）连同自己传到 30 格内一点附近，被传走的敌人眩晕 2 秒。消耗 4 点灵能点，冷却 1 天。灵跃者，心灵术士 14 级。',
                     WS + 'PlaneShift', 'LP_ClassLocBase', comp('PSMassWarp', PS), cd=60000, rng=30, warm=1, hostile=False,
                     verb_extra='<targetParams><canTargetPawns>false</canTargetPawns><canTargetBuildings>false</canTargetBuildings><canTargetLocations>true</canTargetLocations></targetParams>'))
    # 传奇
    A.append(ability('LP_LegPsychicCatastrophe', '心灵天灾', '传奇：60 秒内灵能修行、职业与子职业能力都不扣灵能点，每次改加神经热 4。消耗 3 点灵能点，冷却 2 天。传奇心灵术士。',
                     PI + 'Legendary/ArcaneSurge', 'LP_LegendaryBase', comp('PSCatastrophe', PS, duration=3600, hediff='LP_LegPsychicCatastropheBuff'), cd=120000, warm=1))
    A.append(ability('LP_LegTranscendentMind', '超凡心智', '传奇被动：灵能值永远至少是范围的 60%（灵能涌动不再加神经热），灵能点上限 +4。传奇心灵术士。',
                     PI + 'Legendary/SpellMastery', 'LP_LegendaryBase', comp('Passive', PS, passive=True)))
    P.append('  <!-- ===== 职业、修行、子职业能力 ===== -->\n' + '\n'.join(A))

    # 新法术（法术表 LP_CSL_Psion；心灵术士独有的放在这里，效果在 ClassPsion.cs 或通用 CS_ 效果）
    L = []
    L.append(ability('LP_PS_TelekineticFling', '念力飞掷', '戏法（塑能）：拎起身边一件小东西砸向 24 格内的一个敌人：10 × 强度点钝器伤害。冷却 5 秒。', icon_copy(PI + 'Subclass6/SNThrow', 'PSTelekineticFling'), 'LP_ClassMeleeBase',
                     comp('CS_Bolt', PS, power=10, psychic=True), cd=300, rng=24, warm=0.5, ext=spellext(0, '塑能', '<damage>Blunt</damage><ap>0.2</ap>')))
    L.append(ability('LP_PS_LifeSiphon', '生命虹吸', '1 环（死灵·可升环）：抽走 12 格内一个敌人的生命：12 × 强度点死灵伤害，你回复造成伤害的一半。升环：每环效果 +25%。冷却 10 秒。', icon_copy(S2 + 'Siphon', 'PSLifeSiphon'), 'LP_ClassMeleeBase',
                     comp('PSLifeSiphon', PS, cost=1, power=12, psychic=True), cd=600, rng=12, warm=0.5, ext=spellext(1, '死灵', '<upcast>power</upcast>')))
    L.append(ability('LP_PS_EgoWhip', '自我鞭笞', '2 环（惑控）：鞭笞 20 格内一个敌人的自我：16 × 强度点心灵伤害，20 秒内射击、近战命中 −20%、意识 −10%。冷却 15 秒。', icon_copy(S2 + 'PsychicSpike', 'PSEgoWhip'), 'LP_ClassMeleeBase',
                     comp('CS_Bolt', PS, cost=1, power=16, duration=1200, psychic=True), cd=900, rng=20, warm=0.5, ext=spellext(2, '惑控', '<damage>Psychic</damage><ap>1</ap><hediff2>LP_PS_EgoWhipped</hediff2>')))
    L.append(ability('LP_PS_TelekineticCrush', '念力碾压', '3 环（变化）：20 格内一点半径 2 的形力压下：22 × 强度点力场伤害并眩晕 2 秒（目标豁免成功减半、不眩晕），对建筑另造成双倍钝击。冷却 20 秒。', icon_copy(WS + 'Telekinesis', 'PSTelekineticCrush'), 'LP_ClassLocBase',
                     comp('PSCrush', PS, cost=2, power=22, radius=2, psychic=True), cd=1200, rng=20, warm=1, ext=spellext(3, '变化')))
    L.append(ability('LP_PS_IntellectFortress', '智识壁垒', '3 环（防护）：20 格内一名友军（可以是自己）1 小时内受到的心灵伤害 ×0.5，心智抗性 +25%。冷却 30 秒。', icon_copy(WS + 'MindBlank', 'PSIntellectFortress'), 'LP_ClassAllyBase',
                     comp('TargetBuff', PS, cost=2, duration=2500, hediff='LP_PS_Fortress', extra='<fixedSeverity>true</fixedSeverity>'), cd=1800, rng=20, warm=0.5, undrafted=True, ext=spellext(3, '防护', '<concentration>true</concentration>')))
    L.append(ability('LP_PS_PsychicLance', '心灵长矛', '4 环（惑控）：一支心灵长矛刺穿 36 格内一个敌人的心智（无视掩体）：30 × 强度点心灵伤害并眩晕 3 秒。冷却 20 秒。', icon_copy(CS + 'PsychicBlast', 'PSPsychicLance'), 'LP_ClassMeleeBase',
                     comp('CS_Bolt', PS, cost=2, power=30, psychic=True), cd=1200, rng=36, warm=0.5, verb_extra='<requireLineOfSight>false</requireLineOfSight>', ext=spellext(4, '惑控', '<damage>Psychic</damage><ap>1</ap><stun>180</stun>')))
    L.append(ability('LP_PS_SynapticStatic', '突触静电', '5 环（惑控）：24 格内一点半径 4 的敌人脑中炸开静电：28 × 强度点心灵伤害，1 分钟内意识 −15%、命中 −10%。冷却 30 秒。', icon_copy(S2 + 'ArcaneJolt', 'PSSynapticStatic'), 'LP_ClassLocBase',
                     comp('CS_Area', PS, cost=3, power=28, radius=4, duration=3600, psychic=True), cd=1800, rng=24, warm=1, ext=spellext(5, '惑控', '<damage>Psychic</damage><ap>1</ap><hediff2>LP_PS_Static</hediff2>')))
    L.append(ability('LP_PS_MentalPrison', '心灵监牢', '6 环（幻术）：把 30 格内一个敌人关进幻象牢笼：20 × 强度点心灵伤害，之后 30 秒动弹不得（成功率 70%，目标每比施法者高 1 级 −5%，心智抗性也能抵抗；剧情首领减半）。冷却 1 小时。', icon_copy(S2 + 'FleshPrison', 'PSMentalPrison'), 'LP_ClassMeleeBase',
                     comp('CS_Bolt', PS, cost=4, power=20, psychic=True), cd=2500, rng=30, warm=1, ext=spellext(6, '幻术', '<damage>Psychic</damage><ap>1</ap><stun>1800</stun><chance>0.7</chance><mind>true</mind>')))
    L.append(ability('LP_PS_PsionicBlast', '灵能爆破', '6 环（塑能）：身前 12 格锥形的灵能冲击：36 × 强度点力场伤害并眩晕 1 秒。冷却 30 秒。', icon_copy(S2 + 'TeleStorm', 'PSPsionicBlast'), 'LP_ClassLocBase',
                     comp('CS_Cone', PS, cost=4, power=36, psychic=True, extra='<range>12</range>'), cd=1800, rng=12, warm=1, ext=spellext(6, '塑能', '<damage>Force</damage><ap>0.4</ap><stun>60</stun>')))
    L.append(ability('LP_PS_PsychicScream', '心灵尖啸', '9 环（惑控）：放出一声只有心智听得见的尖啸：36 格内最多 10 个敌人（机械体、极小的动物除外）受 60 × 强度点心灵伤害并眩晕 10 秒（豁免成功减半、不眩晕；剧情首领眩晕减半），被这一下打倒的人脑部受伤。冷却 1 天。', icon_copy(CS + 'PsychicBlast', 'PSPsychicScream'), 'LP_ClassSelfBase',
                     comp('PSScream', PS, cost=9, power=60, radius=36, duration=600, psychic=True), cd=60000, warm=1, hostile=True, ext=spellext(9, '惑控')))
    P.append('  <!-- ===== 心灵术士的新法术 ===== -->\n' + '\n'.join(L))

    spells = ['LP_CS_MindSliver', 'LP_WS_MageHand', 'LP_CS_MinorIllusion', 'LP_CS_Friends', 'LP_WS_Light', 'LP_WS_Mending', 'LP_WS_Prestidigitation', 'LP_PS_TelekineticFling',
              'LP_CharmPerson=1', 'LP_CS_Command', 'LP_WS_ComprehendLanguages', 'LP_WS_DetectMagic', 'LP_CS_Jump', 'LP_ShieldSpell', 'LP_MageArmor', 'LP_Sleep', 'LP_WS_Thunderwave', 'LP_PS_LifeSiphon',
              'LP_WS_MindSpike', 'LP_WS_HoldPerson', 'LP_WS_Invisibility', 'LP_MirrorImage', 'LP_WS_EnhanceAbility', 'LP_WS_Shatter', 'LP_WS_Knock', 'LP_CS_Silence', 'LP_PS_EgoWhip',
              'LP_WS_Fear', 'LP_WS_Fly', 'LP_WS_DispelMagic', 'LP_CS_HypnoticPattern', 'LP_CS_Tongues', 'LP_WS_Sending', 'LP_PS_TelekineticCrush', 'LP_PS_IntellectFortress',
              'LP_WS_Confusion', 'LP_WS_Banishment', 'LP_WS_DimensionDoor', 'LP_WS_Polymorph', 'LP_CS_GreaterInvisibility', 'LP_CS_FreedomOfMovement', 'LP_WS_PhantasmalKiller', 'LP_PS_PsychicLance',
              'LP_WS_DominatePerson', 'LP_WS_HoldMonster', 'LP_WS_Telekinesis', 'LP_WS_ModifyMemory', 'LP_WS_Scrying', 'LP_CS_Dream', 'LP_CS_ContactOtherPlane', 'LP_WS_TeleportationCircle', 'LP_CS_Awaken', 'LP_PS_SynapticStatic',
              'LP_Disintegrate', 'LP_CS_Eyebite', 'LP_WS_MoveEarth', 'LP_WS_TrueSeeing', 'LP_CS_BladeBarrier', 'LP_CS_OttoDance', 'LP_CS_MassSuggestion', 'LP_PS_MentalPrison', 'LP_PS_PsionicBlast',
              'LP_WS_ForceCage', 'LP_WS_Etherealness', 'LP_WS_PlaneShift', 'LP_WS_ReverseGravity', 'LP_CS_ProjectImage', 'LP_Teleport',
              'LP_WS_DominateMonster', 'LP_WS_Feeblemind', 'LP_CS_PowerWordStun', 'LP_WS_MindBlank',
              'LP_WS_Foresight', 'LP_WS_PowerWordKill', 'LP_CS_PowerWordHeal', 'LP_WS_TimeStop', 'LP_PS_PsychicScream',
              # 子职业常备（只给对应子职业）
              'LP_CureWounds=1', 'LP_LesserRestoration=2', 'LP_Haste', 'LP_CS_MassHealingWord', 'LP_WS_Stoneskin', 'LP_MassHeal=5', 'LP_CS_GreaterRestoration',
              'LP_WS_Slow', 'LP_CS_StoneShape', 'LP_WallOfStone=5', 'LP_WS_Counterspell', 'LP_MistyStep', 'LP_WS_Blink']
    P.append('''  <LichPhylactery.CasterListDef>
    <defName>LP_CSL_Psion</defName>
    <label>心灵术士法术表</label>
    <classDef>LP_Class_Psion</classDef>
    <ability>Int</ability>
    <caster>full</caster>
    <prepared>true</prepared>
    <order>8</order>
    <iconPath>LP/Classes/Subclass2/PsychicSpike</iconPath>
    <stats><li>LP_PsionicPower</li></stats>
    <spells>
''' + '\n'.join(f'      <li>{s}</li>' for s in spells) + '''
    </spells>
  </LichPhylactery.CasterListDef>''')

    # 子职业
    sub = lambda dn, lab, desc, order, emblem, color, ab, extra='': f'''  <LichPhylactery.LP_SubclassDef>
    <defName>{dn}</defName>
    <label>{lab}</label>
    <description>{X(desc)}</description>
    <parent>{PC}</parent><order>{order}</order><emblem>{emblem}</emblem><color>{color}</color>
    <abilities>{''.join(f"<li><ability>{a}</ability><level>{l}</level></li>" for a, l in ab)}</abilities>
    {ps_choices(extra)}
  </LichPhylactery.LP_SubclassDef>'''
    evo = '<li><key>psevo</key><label>进化之躯</label><level>10</level><options><li>石化表皮</li><li>超凡步态</li><li>非凡柔韧</li></options><tips><li>柔变之躯期间受到的伤害 ×0.75。</li><li>柔变之躯期间移动 +25%。</li><li>柔变之躯期间近战闪避 +10。</li></tips></li>'
    P.append(sub('LP_Sub_Metamorph', '血肉塑形者', '把血肉当成黏土：柔变之躯、生体武器（骨刃、血肉巨锤、内脏弩）、血肉编织，最后让武器既能杀人也能救人。常备法术：治疗伤口、次级复原术、增强属性、生命虹吸（3 级），急速术、群体治疗真言（5 级），变形术、石肤术（7 级），群体治疗、高等复原术（9 级）。', 1,
                 'LP/Classes/Subclass/Emblem_AberrantMind', '(0.8,0.45,0.45)',
                 [('LP_PS_MutableForm', 3), ('LP_PS_OrganicWeapons', 3), ('LP_PS_BileLauncher', 3), ('LP_PS_FleshWeave', 6), ('LP_PS_EvolvedForm', 10), ('LP_PS_LifeBending', 14)], evo))
    P.append(sub('LP_Sub_Psykinetic', '念动师', '把念力当成可塑的力：搬运、推拉、碾压、反弹，最后能把任何东西举起来扔出去。常备法术：护盾术、雷鸣波、粉碎音波、跳跃术（3 级），缓慢术、念力碾压（5 级），塑石术、自由行动（7 级），心灵遥控、石墙术（9 级）。', 2,
                 'LP/Classes/Subclass/Emblem_PsiWarrior', '(0.5,0.65,0.95)',
                 [('LP_PS_StrongTK', 3), ('LP_PS_TKTechniques', 3), ('LP_PS_DestructiveTrance', 6), ('LP_PS_ReboundField', 6), ('LP_PS_EnhancedCrush', 10), ('LP_PS_HeightenedTK', 14)]))
    P.append(sub('LP_Sub_Telepath', '心灵感应者', '心智战场上的战术家：读心、审讯、干扰敌人、把同伴连成网，最后搅乱一整群敌人的心智。常备法术：命令术、心灵尖刺、魅惑人类、自我鞭笞（3 级），反制法术、缓慢术（5 级），困惑术、魅影杀手（7 级），修改记忆、支配人类（9 级）。', 3,
                 'LP/Classes/Subclass/Emblem_GreatOldOne', '(0.6,0.5,0.9)',
                 [('LP_PS_Infiltrate', 3), ('LP_PS_TeleDistract', 3), ('LP_PS_BulwarkMind', 6), ('LP_PS_PotentThoughts', 6), ('LP_PS_TeleBolster', 10), ('LP_PS_ScrambleMinds', 14)]))
    P.append(sub('LP_Sub_PsiWarper', '灵跃者', '把空间折起来走：瞬移、换位、救人、放信标，最后把一群人一起传走。常备法术：迷踪步、跳跃术、镜影术（3 级），闪现术、急速术（5 级），次元门、放逐术（7 级），传送法阵、探知术（9 级）。', 4,
                 'LP/Classes/Subclass/Emblem_ShadowMagic', '(0.55,0.85,0.8)',
                 [('LP_PS_Teleport', 3), ('LP_PS_WarpPropel', 3), ('LP_PS_Displace', 6), ('LP_PS_RescueWarp', 6), ('LP_PS_WarpBeacon', 10), ('LP_PS_MassWarp', 14)]))

    # 专长
    P.append(feat('LP_Feat_Class_PsionTempered', '心灵淬炼', '日复一日地打磨心智。灵能强度 +15%；灵能点上限 +2；冥想时精神力获得更快。心灵术士专属。附带智力 +1。', 'LP/Feats/Class/WizardScholar', 86, PC, 3, PS, 0.15,
                  '    <statFactors><MeditationFocusGain>1.2</MeditationFocusGain></statFactors>', ['Int']))
    P.append(feat('LP_Feat_Class_PsionSilent', '无声之心', '心里的声音小到连自己都听不见。灵能强度 +10%；心智抗性 +30%，心灵敏感度 +10%，神经热消散 +25%；心灵术士能力冷却 ×0.85。心灵术士专属。附带智力或感知 +1。', 'LP/Feats/Class/SorcererResonance', 87, PC, 7, PS, 0.1,
                  '    <cooldownFactor>0.85</cooldownFactor>\n    <statOffsets><LP_MindResist>0.3</LP_MindResist><PsychicSensitivity>0.1</PsychicSensitivity></statOffsets>\n    <statFactors><PsychicEntropyRecoveryRate>1.25</PsychicEntropyRecoveryRate></statFactors>', ['Int', 'Wis']))

    # 秘传（魔力湖泊职业水晶）
    AR = []
    AR.append(ability('LP_AC_ThousandMinds', '千心之眼', '秘传（6 环心灵术士）：1 小时内整张地图上的敌人现形（解除心灵隐形），本地图所有殖民者射击、近战命中 +10%。冷却 2 天。', 'LP/Arcana/Foresight', 'LP_ClassSelfBase',
                      comp('PSArcEyes', PS, duration=2500), cd=120000, warm=1, undrafted=True, ext=arcext(6, PC)))
    AR.append(ability('LP_AC_MindSeal', '思维幽闭', '秘传（7 环心灵术士）：30 格内一个敌人对抗心智抗性（剧情首领与传奇免疫）：没抵抗住就意识归零 1 天——活着、动不了，可以直接抓进牢房。冷却 3 天。', 'LP/Arcana/DreamlessSleep', 'LP_ClassMeleeBase',
                      comp('PSArcSeal', PS, psychic=True), cd=180000, rng=30, warm=1, ext=arcext(7, PC)))
    AR.append(ability('LP_AC_PsychicStorm', '心灵风暴', '秘传（8 环心灵术士）：以你为中心半径 12 掀起心灵风暴，10 秒内敌人每秒受 15 × 灵能强度 心灵伤害。冷却 5 天。', 'LP/Arcana/MassFear', 'LP_ClassSelfBase',
                      comp('PSArcStorm', PS, power=15, radius=12, psychic=True), cd=300000, warm=1, hostile=True, ext=arcext(8, PC)))
    AR.append(ability('LP_AC_MindDominion', '心灵主宰', '秘传（9 环心灵术士）：重塑 3 格内一个倒地的敌对人形的心智（剧情首领与传奇不行，对抗心智抗性）：没抵抗住就加入你的阵营。冷却 7 天。', 'LP/Classes/Subclass2/MindDominion', 'LP_ClassMeleeBase',
                      comp('PSArcDominion', PS, psychic=True), cd=420000, rng=3, warm=2, undrafted=True, ext=arcext(9, PC)))
    P.append('  <!-- ===== 秘传 ===== -->\n' + '\n'.join(AR))
    H2 = [hediff('LP_PS_ArcEyesBuff', '千心之眼', '心灵术士的千心之眼照着整片战场：射击、近战命中 +10%。', color=C, stages=st(offsets={'ShootingAccuracyPawn': .1, 'MeleeHitChance': .1})),
          hediff('LP_PS_MindSealed', '思维幽闭', '心智被关进了没有门的房间：意识归零。', bad=True, color=C, stages=st(capmods='<capMods><li><capacity>Consciousness</capacity><setMax>0.1</setMax></li></capMods>')),
          hediff('LP_PS_StormAura', '心灵风暴', '身边掀起心灵风暴：每秒伤害周围的敌人。', cls='LichPhylactery.Hediff_PsionStorm', color=C)]
    P.append('\n'.join(H2))
    CR = [crystal('ThousandMinds', 'LP_AC_ThousandMinds', '千心之眼', '心灵术士', 6, '秘传（6 环心灵术士）：1 小时内整张地图上的敌人现形，本地图所有殖民者命中 +10%。冷却 2 天。', C),
          crystal('MindSeal', 'LP_AC_MindSeal', '思维幽闭', '心灵术士', 7, '秘传（7 环心灵术士）：一个敌人没抵抗住就意识归零 1 天。冷却 3 天。', C),
          crystal('PsychicStorm', 'LP_AC_PsychicStorm', '心灵风暴', '心灵术士', 8, '秘传（8 环心灵术士）：半径 12 的心灵风暴，10 秒内敌人每秒受心灵伤害。冷却 5 天。', C),
          crystal('MindDominion', 'LP_AC_MindDominion', '心灵主宰', '心灵术士', 9, '秘传（9 环心灵术士）：让一个倒地的敌对人形加入你的阵营。冷却 7 天。', C)]
    P.append('\n'.join(CR))
    write('ClassPsion.xml', '2026-11-19 心灵术士：由 ArtSource/NewClasses/gen_newclasses.py 生成，不要手改。效果在 Source/ClassPsion.cs。', P)

# =====================================================================================
# 枪手
# =====================================================================================
GP = 'LP_GunPower'; GC = 'LP_Class_Gunslinger'
def gunslinger():
    P = []; C = '(0.85,0.7,0.45)'; F = PI + 'Fighter/'; R = PI + 'Rogue/'; RA = PI + 'Ranger/'; S6 = PI + 'Subclass6/'; S2 = PI + 'Subclass2/'; AR_ = PI + 'Artificer/'
    P.append(f'''  <LichPhylactery.LP_ClassDef>
    <defName>{GC}</defName>
    <label>枪手</label>
    <description>靠胆识和一把好枪吃饭的火器专家，只用火器，还会造枪。\\n强度：「枪术强度」（主属性敏捷，技能射击）。</description>
    <mainSkill>Shooting</mainSkill>
    <alternateSkill>Crafting</alternateSkill>
    <primary>Dex/Wis</primary>
    <order>14</order>
    <subclassLevel>3</subclassLevel>
    <resource>Grit</resource>
    <martial>true</martial>
    <background>LP/Classes/Background_Fighter</background>
    <color>{C}</color>
    <abilities>
      <li><ability>LP_GS_BondGun</ability><level>1</level></li>
      <li><ability>LP_GS_Dodge</ability><level>1</level></li>
      <li><ability>LP_GS_ViolentShot</ability><level>1</level></li>
      <li><ability>LP_GS_QuickClear</ability><level>1</level></li>
      <li><ability>LP_GS_Deadeye</ability><level>1</level></li>
      <li><ability>LP_GS_QuickDraw</ability><level>3</level></li>
      <li><ability>LP_GS_PistolWhip</ability><level>3</level></li>
      <li><ability>LP_GS_UtilityShot</ability><level>3</level></li>
      <li><ability>LP_GS_GunTraining</ability><level>5</level></li>
      <li><ability>LP_GS_Gunsmith</ability><level>5</level></li>
      <li><ability>LP_GS_DeadShot</ability><level>7</level></li>
      <li><ability>LP_GS_Targeting</ability><level>7</level></li>
      <li><ability>LP_GS_AimMode</ability><level>7</level></li>
      <li><ability>LP_GS_StartlingShot</ability><level>7</level></li>
      <li><ability>LP_GS_BleedingWound</ability><level>10</level></li>
      <li><ability>LP_GS_LightningReload</ability><level>10</level></li>
      <li><ability>LP_GS_Evasive</ability><level>13</level></li>
      <li><ability>LP_GS_MenacingShot</ability><level>13</level></li>
      <li><ability>LP_GS_StunningShot</ability><level>13</level></li>
      <li><ability>LP_GS_SlingerLuck</ability><level>13</level></li>
      <li><ability>LP_GS_DeathShot</ability><level>15</level><capstoneSkill>Shooting</capstoneSkill></li>
      <li><ability>LP_GS_CheatDeath</ability><level>17</level></li>
    </abilities>
    <legendary><li>LP_LegBulletStorm</li><li>LP_LegTrueGrit</li></legendary>
  </LichPhylactery.LP_ClassDef>''')
    P.append(f'''  <StatDef ParentName="LP_ClassPowerBase">
    <defName>{GP}</defName>
    <label>枪术强度</label>
    <description>枪手能力的强度倍率：0.5 + 射击等级/20，另加冒险者敏捷调整值 × 0.05 与 (熟练加值 − 2) × 0.02；物理职业等级合计每比 1 高 1 级 +2%（最多 +40%）。</description>
    <parts><li Class="LichPhylactery.StatPart_ClassPower"><skill>Shooting</skill><ability>Dex</ability></li></parts>
    <displayPriorityInCategory>4220</displayPriorityInCategory>
  </StatDef>''')
    H = []
    H.append(hediff('LP_GS_Bonded', '相依之枪', '手里是自己的那把枪：越用越顺手（随枪手等级成长）。', 'LP_SubclassPassiveBase', color=C,
                    stages=st(minsev=0, label='一', offsets={'ShootingAccuracyPawn': .05}) + st(minsev=1.5, label='二', offsets={'ShootingAccuracyPawn': .05}, factors={'RangedCooldownFactor': .9})
                    + st(minsev=2.5, label='三', offsets={'ShootingAccuracyPawn': .07}, factors={'RangedCooldownFactor': .9, 'AimingDelayFactor': .9}) + st(minsev=3.5, label='四', offsets={'ShootingAccuracyPawn': .08}, factors={'RangedCooldownFactor': .85, 'AimingDelayFactor': .85})))
    H.append(hediff('LP_GS_GritEdge', '胆识在身', '只要还有胆识：远距离也打得准（远距锁定），拔枪更快（拔枪先手）。', 'LP_SubclassPassiveBase', color=C,
                    stages=st(minsev=0, label='远距锁定', offsets={'ShootingAccuracyPawn': .05}) + st(minsev=1.5, label='远距锁定 · 拔枪先手', offsets={'ShootingAccuracyPawn': .05}, factors={'AimingDelayFactor': .75})))
    H.append(hediff('LP_GS_Gunsmith', '枪匠', '枪手熟悉每一颗螺丝：做东西更快。', 'LP_SubclassPassiveBase', color=C, stages=st(offsets={'GeneralLaborSpeed': .3})))
    H.append(hediff('LP_GS_Jammed', '卡壳', '枪卡壳了：3 秒内瞄准极慢。用快速排障立刻解除。', bad=True, color='(0.9,0.4,0.35)', stages=st(factors={'AimingDelayFactor': 6})))
    H.append(hediff('LP_GS_Startled', '惊骇', '一颗子弹擦着脸飞过去：近战闪避 −20，射击精度 −20%。', bad=True, color=C, stages=st(offsets={'MeleeDodgeChance': -20, 'ShootingAccuracyPawn': -.2})))
    H.append(hediff('LP_GS_BleedReady', '放血伤', '接下来 5 秒内火器打中的活物多一道流血的伤口。', color=C))
    H.append(hediff('LP_GS_ReloadBuff', '闪电装填', '装填快得看不清：射击冷却 ×0.6。', color=C, stages=st(factors={'RangedCooldownFactor': .6})))
    H.append(hediff('LP_GS_CloseQuarters', '贴身枪法', '流浪者：贴身也开得准，射击精度 ×1.1。', 'LP_SubclassPassiveBase', color=C, stages=st(factors={'ShootingAccuracyPawn': 1.1})))
    H.append(hediff('LP_GS_DrifterBlade', '刀枪并用·刀', '刚开枪打中：下一刀伤害 ×1.25。', color=C, stages=st(factors={'MeleeDamageFactor': 1.25})))
    H.append(hediff('LP_GS_DrifterShot', '刀枪并用·枪', '刚砍中：下一枪瞄得快、打得准。', color=C, stages=st(offsets={'ShootingAccuracyPawn': .15}, factors={'AimingDelayFactor': .3})))
    H.append(hediff('LP_GS_SkirmishBuff', '游击步', '刚撂倒一个：移动 ×1.3。', color=C, stages=st(factors={'MoveSpeed': 1.3})))
    H.append(hediff('LP_GS_Dueled', '正午决斗', '被枪手点名决斗：受到它的火器伤害 ×1.5，自己射击精度 ×0.5。', cls='LichPhylactery.Hediff_ClassMark', bad=True, color=C, stages=st(factors={'ShootingAccuracyPawn': .5})))
    H.append(hediff('LP_GS_Concealed', '隐蔽位置', '狙击手藏好了：下一发火器伤害 ×2。挪动就没了。', 'LP_SubclassPassiveBase', color=C))
    H.append(hediff('LP_GS_Spotted', '被观测', '被狙击手标记：藏不住，受到的伤害 ×1.15。', bad=True, color=C))
    H.append(hediff('LP_GS_YardBuff', '千码凝视', '盯着远处：对 30 格外目标的火器伤害 ×2，射击精度 ×1.25。', color=C, stages=st(factors={'ShootingAccuracyPawn': 1.25})))
    H.append(hediff('LP_GS_HeavyStance', '重装持枪', '先锋端着重火器也走得动：移动 ×1.1，受到的近战伤害 ×0.85。', 'LP_SubclassPassiveBase', color=C, stages=st(factors={'MoveSpeed': 1.1})))
    H.append(hediff('LP_GS_Covered', '掩护火力', '先锋在掩护你：受到的远程伤害 ×0.85。', color=C))
    H.append(hediff('LP_GS_StormBuff', '钢铁风暴', '先锋打开了全部火力：射击冷却 ×0.5，受到的伤害 ×0.8。', color=C, stages=st(factors={'RangedCooldownFactor': .5})))
    H.append(hediff('LP_LegBulletStormBuff', '子弹风暴', '传奇枪手：每次射击都连开两枪，每次击杀胆识 +2。', color='(0.95,0.8,0.5)'))
    P.append('  <!-- ===== 状态 ===== -->\n' + '\n'.join(H))

    A = []
    A.append(ability('LP_GS_BondGun', '相依之枪', '把手里的火器绑定为相依之枪：拿着它时射击精度 +5%（5 级起射击冷却 ×0.9，10 级起瞄准 ×0.9，15 级起火器伤害 +10%）。同时只能有一把，换绑要等 3 天。枪手 1 级。',
                     F + 'WeaponMastery', 'LP_ClassSelfBase', comp('GSBond', GP), cd=600, warm=1, undrafted=True))
    A.append(passive('LP_GS_Dodge', '枪手闪避', '被动：被远程攻击打中时（每 5 秒判定一次）有 30% 的成功率闪开，花 1 点胆识；穿着利器护甲 40% 以上的衣服时不行。枪手 1 级。', R + 'UncannyDodge', GP))
    A.append(ability('LP_GS_ViolentShot', '暴力射击', '花掉 1～3 点胆识（有多少花多少，最多 3）打出一发猛药：必中，伤害 ×(1 + 0.5 × 点数 × 枪术强度)，穿甲 +10% × 点数；每 1 点有 8% 的成功率卡壳（枪械训练后减半）。冷却 10 秒。枪手 1 级。',
                     F + 'PrecisionShot', 'LP_ClassMeleeBase', comp('GSViolent', GP), cd=600, rng=40, warm=0.5, verb_extra='<requireLineOfSight>true</requireLineOfSight>'))
    A.append(ability('LP_GS_QuickClear', '快速排障', '立刻解除卡壳。要至少 1 点胆识（不扣）。枪手 1 级。', AR_ + 'QuickRepair', 'LP_ClassSelfBase', comp('GSClear', GP), cd=60, warm=0))
    A.append(passive('LP_GS_Deadeye', '远距锁定', '被动：有胆识时射击精度 +5%（远距离也打得准）。枪手 1 级。', F + 'SteadyAim', GP))
    A.append(passive('LP_GS_QuickDraw', '拔枪先手', '被动：有胆识时射击预热 ×0.75。枪手 3 级。', R + 'CunningAction', GP))
    A.append(ability('LP_GS_PistolWhip', '枪托猛击', '用枪托砸身边的敌人：10 × 枪术强度 钝器伤害（不拿短枪时 14），40% 的成功率打倒 1 秒。消耗 1 点胆识，冷却 5 秒。枪手 3 级。',
                     F + 'Trip', 'LP_ClassMeleeBase', comp('GSWhip', GP, cost=1), cd=300))
    A.append(ability('LP_GS_UtilityShot', '实用射击', '一发派用场的子弹：打门、墙、建筑时伤害 ×5（破锁）；打地上的东西把它弹开 3 格（挤移，可以把冒烟的手雷弹走）；对流血的友军用烫枪管烙住流血最多的伤口（止血，留一点烧伤）。要至少 1 点胆识（不扣），冷却 10 秒。枪手 3 级。',
                     AR_ + 'Overcharge', 'LP_ClassLocBase', comp('GSUtility', GP), cd=600, rng=30, warm=0.5, hostile=False,
                     verb_extra='<targetParams><canTargetPawns>true</canTargetPawns><canTargetBuildings>true</canTargetBuildings><canTargetItems>true</canTargetItems><canTargetLocations>false</canTargetLocations><canTargetSelf>false</canTargetSelf></targetParams>'))
    A.append(passive('LP_GS_GunTraining', '枪械训练', '被动：火器每发伤害 + 敏捷调整值 × 1.5；暴力射击的卡壳率减半。枪手 5 级。', F + 'WeaponMastery', GP))
    A.append(passive('LP_GS_Gunsmith', '枪匠', '被动：一般劳动速度 +30%（含做枪）；做火器时 25% 品质高一档。枪手 5 级。', AR_ + 'MasterCraft', GP))
    A.append(ability('LP_GS_DeadShot', '致命一击', '把一整轮射击合成一发：必中，伤害 ×(1 + 0.5 × 连发数)，单发武器至少 ×2.5，穿甲 +20%。消耗 1 点胆识，冷却 20 秒。枪手 7 级。',
                     F + 'DeadEye', 'LP_ClassMeleeBase', comp('GSDeadShot', GP, cost=1), cd=1200, rng=40, warm=2))
    A.append(ability('LP_GS_Targeting', '部位瞄准', '朝指定部位开一枪（必中）：手（缴械）、头（晕 2 秒）、眼（伤害 ×0.6，伤到眼睛）、腿（腿部中弹，体型 1.5 以下的倒地 1 秒）、躯干（穿甲 +30%）。用「切换瞄准部位」选部位。消耗 1 点胆识，冷却 10 秒。枪手 7 级。',
                     F + 'PrecisionShot', 'LP_ClassMeleeBase', comp('GSTargeting', GP, cost=1), cd=600, rng=40, warm=1))
    A.append(ability('LP_GS_AimMode', '切换瞄准部位', '切换部位瞄准打哪里：手 → 头 → 眼 → 腿 → 躯干。不消耗、不进冷却。枪手 7 级。', F + 'SteadyAim', 'LP_ClassSelfBase', comp('GSAimMode', GP), warm=0, undrafted=True))
    A.append(ability('LP_GS_StartlingShot', '惊骇射击', '故意擦着一个敌人开一枪：4 秒内它近战闪避 −20、射击精度 −20%。要至少 1 点胆识（不扣），冷却 8 秒。枪手 7 级。',
                     RA + 'HuntersMark', 'LP_ClassMeleeBase', comp('GSStartle', GP), cd=480, rng=40, warm=0.25))
    A.append(ability('LP_GS_BleedingWound', '放血伤', '5 秒内火器打中的活物多一道会流血的伤口。消耗 1 点胆识，冷却 15 秒。枪手 10 级。',
                     R + 'PoisonBlade', 'LP_ClassSelfBase', comp('GSBleed', GP, cost=1), cd=900, warm=0))
    A.append(ability('LP_GS_LightningReload', '闪电装填', '10 秒内射击冷却 ×0.6。要至少 1 点胆识（不扣），冷却 30 秒。枪手 10 级。',
                     F + 'RapidFire', 'LP_ClassSelfBase', comp('GSReload', GP, duration=600, hediff='LP_GS_ReloadBuff'), cd=1800, warm=0))
    A.append(passive('LP_GS_Evasive', '闪避本能', '被动：有胆识时受到的爆炸伤害 ×0.5。枪手 13 级。', R + 'UncannyDodge', GP))
    A.append(ability('LP_GS_MenacingShot', '威慑射击', '朝天开一枪：半径 8 内的敌人对抗心智抗性，没抵抗住的惊恐逃跑 8 秒（人形会丢下扛着的东西）。消耗 1 点胆识，冷却 1 小时。枪手 13 级。',
                     F + 'BattleCry', 'LP_ClassSelfBase', comp('GSMenace', GP, cost=1, radius=8, duration=480), cd=2500, warm=0.5, hostile=True))
    A.append(ability('LP_GS_StunningShot', '击晕射击', '必中的一枪：目标对抗体质（每比你高 1 级更难，最多 50%）失败就眩晕 3 秒（剧情首领与传奇减半）。消耗 2 点胆识，冷却 15 秒。枪手 13 级。',
                     F + 'DisarmingStrike', 'LP_ClassMeleeBase', comp('GSStun', GP, cost=2), cd=900, rng=40, warm=0.5))
    A.append(passive('LP_GS_SlingerLuck', '枪手好运', '被动：心智抗性没挺住时，花 2 点胆识再判定一次，每天 3 次。枪手 13 级。', R + 'StrokeOfLuck', GP))
    A.append(ability('LP_GS_DeathShot', '死亡射击', '终极：必中的一枪，伤害 ×3 × 枪术强度，穿甲 +30%；目标没挺住（豁免，目标越强越难）就当场死亡。剧情首领、传奇、体型 3 以上的只吃伤害。用它击杀不回胆识。消耗 2 点胆识，冷却 1 天。枪手 15 级，另要射击 15 级。',
                     F + 'DeadEye', 'LP_ClassMeleeBase', comp('GSDeath', GP, cost=2), cd=60000, rng=45, warm=1.5))
    A.append(passive('LP_GS_CheatDeath', '欺骗死神', '被动：本该倒地时，花光所有胆识（至少 1 点）撑住，治好四成伤势，每天一次（在死亡守卫、不屈战魂之后判定）。枪手 17 级。', F + 'Indomitable', GP))
    # 流浪者
    A.append(passive('LP_GS_BladeAndGun', '刀枪并用', '被动：开枪打中后 5 秒内下一刀伤害 ×1.25；砍中后 5 秒内下一枪瞄得快、射击精度 +15%。流浪者，枪手 3 级。', S6 + 'SBDuelist', GP))
    A.append(passive('LP_GS_Skirmish', '游击步', '被动：用火器击杀或打倒敌人后 5 秒内移动 ×1.3。流浪者，枪手 7 级。', S6 + 'SBFootwork', GP))
    A.append(passive('LP_GS_CloseQuarters', '贴身枪法', '被动：拿着火器时射击精度 ×1.1。流浪者，枪手 11 级。', S6 + 'SBElegant', GP))
    A.append(ability('LP_GS_DriftersWake', '流浪者余波', '贴身一刀（相邻时）再接两枪（必中），目标倒下就转向 8 格内最近的敌人。要拿着短枪。消耗 2 点胆识，冷却 30 秒。流浪者，枪手 15 级。',
                     S6 + 'SBPanache', 'LP_ClassMeleeBase', comp('GSWake', GP, cost=2), cd=1800, rng=8, warm=0.25))
    # 双枪客
    A.append(passive('LP_GS_TwinPistols', '双持短枪', '被动：拿着短枪时，每次射击有 35% 的成功率多开一枪（伤害 ×0.7）。双枪客，枪手 3 级。', F + 'RapidFire', GP))
    A.append(ability('LP_GS_Fanning', '扇击', '对 30 格内一点半径 4 的区域连开 6 枪（每发伤害 ×0.8，随机打区域里的敌人）。要拿着短枪。消耗 1 点胆识，冷却 12 秒。双枪客，枪手 7 级。',
                     F + 'SuppressingFire', 'LP_ClassLocBase', comp('GSFan', GP, cost=1), cd=720, rng=30, warm=0.3))
    A.append(passive('LP_GS_Retort', '以枪还枪', '被动：枪手闪避闪开一发子弹时，立刻还射手一枪。双枪客，枪手 11 级。', S6 + 'SBAudacity', GP))
    A.append(ability('LP_GS_HighNoon', '正午决斗', '点名 40 格内一个敌人决斗 30 秒：你对它的火器伤害 ×1.5，它射击精度 ×0.5；你用火器杀了它，胆识回满。消耗 1 点胆识，冷却 1 小时。双枪客，枪手 15 级。',
                     RA + 'FavoredFoe', 'LP_ClassMeleeBase', comp('GSNoon', GP, cost=1, duration=1800), cd=2500, rng=40, warm=0.5))
    # 狙击手
    A.append(passive('LP_GS_Hidden', '隐蔽位置', '被动：拿着长枪原地不动一会儿（250 刻）就进入隐蔽，下一发火器伤害 ×2；挪动就没了。狙击手，枪手 3 级。', RA + 'HideInPlain', GP))
    A.append(ability('LP_GS_PiercingShot', '穿透射击', '一发子弹沿直线穿过射程内的所有敌人（必中），每穿一个伤害 ×0.8，穿甲 +20%。要拿着长枪。消耗 1 点胆识，冷却 15 秒。狙击手，枪手 7 级。',
                     RA + 'Volley', 'LP_ClassMeleeBase', comp('GSPierce', GP, cost=1), cd=900, rng=45, warm=1))
    A.append(ability('LP_GS_Spotter', '观测手', '标记 45 格内一个敌人 30 秒：它藏不住（解除心灵隐形），受到的伤害 ×1.15。要至少 1 点胆识（不扣），冷却 30 秒。狙击手，枪手 11 级。',
                     RA + 'HuntersMark', 'LP_ClassMeleeBase', comp('GSSpot', GP, duration=1800), cd=1800, rng=45, warm=0.25, verb_extra='<requireLineOfSight>false</requireLineOfSight>'))
    A.append(ability('LP_GS_ThousandYard', '千码凝视', '60 秒内射击精度 ×1.25，对 30 格外目标的火器伤害 ×2。要拿着长枪。消耗 2 点胆识，冷却 1 天。狙击手，枪手 15 级。',
                     RA + 'FoeSlayer', 'LP_ClassSelfBase', comp('GSYard', GP, cost=2, duration=3600, hediff='LP_GS_YardBuff'), cd=60000, warm=0.5))
    # 先锋
    A.append(passive('LP_GS_HeavyStance', '重装持枪', '被动：拿着重火器时移动 ×1.1，受到的近战伤害 ×0.85。先锋，枪手 3 级。', F + 'GuardStance', GP))
    A.append(ability('LP_GS_PointBlank', '顿射', '对 3 格内一个敌人开一枪（必中），把它推开 2 格。要拿着重火器。消耗 1 点胆识，冷却 6 秒。先锋，枪手 3 级。',
                     F + 'Charge', 'LP_ClassMeleeBase', comp('GSBlast', GP, cost=1), cd=360, rng=3, warm=0.25))
    A.append(ability('LP_GS_CoveringFire', '掩护火力', '30 秒内 8 格内的友军（含自己）受到的远程伤害 ×0.85。消耗 1 点胆识，冷却 1 分钟。先锋，枪手 7 级。',
                     F + 'SuppressingFire', 'LP_ClassSelfBase', comp('GSCover', GP, cost=1, duration=1800), cd=3600, warm=0.5))
    A.append(passive('LP_GS_Breach', '破阵', '被动：拿着重火器时对机械体伤害 ×1.4，每发有 15% 的成功率把人形击退 1 格。先锋，枪手 11 级。', F + 'Whirlwind', GP))
    A.append(ability('LP_GS_SteelStorm', '钢铁风暴', '10 秒内射击冷却 ×0.5，受到的伤害 ×0.8。要拿着重火器。消耗 2 点胆识，冷却 1 天。先锋，枪手 15 级。',
                     F + 'SurgeOfBattle', 'LP_ClassSelfBase', comp('GSStorm', GP, cost=2, duration=600, hediff='LP_GS_StormBuff'), cd=60000, warm=0.25))
    # 传奇
    A.append(ability('LP_LegBulletStorm', '子弹风暴', '传奇：30 秒内每次射击都连开两枪，每次用火器击杀胆识 +2。消耗 3 点胆识，冷却 2 天。传奇枪手。',
                     PI + 'Legendary/ArrowStorm', 'LP_LegendaryBase', comp('GSBulletStorm', GP, cost=3, duration=1800, hediff='LP_LegBulletStormBuff'), cd=120000, warm=0.5))
    A.append(ability('LP_LegTrueGrit', '真胆识', '传奇被动：枪手能力里消耗 2 点以上胆识的少 1 点。传奇枪手。', PI + 'Legendary/Unyielding', 'LP_LegendaryBase', comp('Passive', GP, passive=True)))
    P.append('  <!-- ===== 职业、枪道能力 ===== -->\n' + '\n'.join(A))

    sub = lambda dn, lab, desc, order, emblem, color, ab: f'''  <LichPhylactery.LP_SubclassDef>
    <defName>{dn}</defName>
    <label>{lab}</label>
    <description>{X(desc)}</description>
    <parent>{GC}</parent><order>{order}</order><emblem>{emblem}</emblem><color>{color}</color>
    <abilities>{''.join(f"<li><ability>{a}</ability><level>{l}</level></li>" for a, l in ab)}</abilities>
  </LichPhylactery.LP_SubclassDef>'''
    P.append(sub('LP_Sub_Drifter', '流浪者', '一把短枪一把刀走天下：刀枪交替、撂倒一个就换位置，最后一刀两枪连成一串。枪道能力要拿着短枪或近战时才用得上。', 1, 'LP/Classes/Subclass/Emblem_Swashbuckler', '(0.8,0.6,0.4)',
                 [('LP_GS_BladeAndGun', 3), ('LP_GS_Skirmish', 7), ('LP_GS_CloseQuarters', 11), ('LP_GS_DriftersWake', 15)]))
    P.append(sub('LP_Sub_Pistolero', '双枪客', '两把短枪、一根火柴的耐心：双持、扇击、以枪还枪，最后在正午和人决斗。', 2, 'LP/Classes/Subclass/Emblem_Champion', '(0.9,0.75,0.4)',
                 [('LP_GS_TwinPistols', 3), ('LP_GS_Fanning', 7), ('LP_GS_Retort', 11), ('LP_GS_HighNoon', 15)]))
    P.append(sub('LP_Sub_Sniper', '狙击手', '在敌人看不见的地方等一发：隐蔽、穿透、为全队指示目标，最后一千码外也能一枪命中。', 3, 'LP/Classes/Subclass/Emblem_GloomStalker', '(0.6,0.7,0.55)',
                 [('LP_GS_Hidden', 3), ('LP_GS_PiercingShot', 7), ('LP_GS_Spotter', 11), ('LP_GS_ThousandYard', 15)]))
    P.append(sub('LP_Sub_Vanguard', '先锋', '扛着重火器顶在最前面：顿射、掩护、破阵，最后打开全部火力。', 4, 'LP/Classes/Subclass/Emblem_FortEngineer', '(0.7,0.55,0.45)',
                 [('LP_GS_HeavyStance', 3), ('LP_GS_PointBlank', 3), ('LP_GS_CoveringFire', 7), ('LP_GS_Breach', 11), ('LP_GS_SteelStorm', 15)]))

    P.append(feat('LP_Feat_Class_GunExpert', '火器专家', '枪拆开又装上一千遍了。枪术强度 +10%；暴力射击不会卡壳；做东西更快；枪手能力冷却 ×0.9。枪手专属。附带敏捷 +1。', 'LP/Feats/Class/FighterVeteran', 88, GC, 3, GP, 0.1,
                  '    <cooldownFactor>0.9</cooldownFactor>\n    <statOffsets><GeneralLaborSpeed>0.2</GeneralLaborSpeed></statOffsets>', ['Dex']))
    P.append(feat('LP_Feat_Class_GunRenown', '神枪手之誉', '枪声一响，大家都知道是你。枪术强度 +15%；胆识上限 +2；射击精度 +5%。枪手专属。附带敏捷或感知 +1。', 'LP/Feats/Class/RangerStalker', 89, GC, 7, GP, 0.15,
                  '    <resourceBonus>2</resourceBonus>\n    <statOffsets><ShootingAccuracyPawn>0.05</ShootingAccuracyPawn></statOffsets>', ['Dex', 'Wis']))

    AR = []
    AR.append(ability('LP_AC_RuneBullets', '符文魔弹', '秘传（6 环枪手）：60 秒内火器子弹附带火焰，额外造成 50% 的火焰伤害。冷却 2 天。', 'LP/Arcana/GaleArrow', 'LP_ClassSelfBase',
                      comp('GSArcRunes', GP, duration=3600), cd=120000, warm=0.5, ext=arcext(6, GC)))
    AR.append(ability('LP_AC_BulletRain', '弹雨', '秘传（7 环枪手）：对 40 格内一点半径 5 的区域开 20 枪，每发伤害 ×0.6。要拿着火器。冷却 3 天。', 'LP/Arcana/ChainLightning', 'LP_ClassLocBase',
                      comp('GSArcRain', GP), cd=180000, rng=40, warm=1, ext=arcext(7, GC)))
    AR.append(ability('LP_AC_StoppedTime', '时停一枪', '秘传（8 环枪手）：世界在你眼里慢下来：30 格内的敌人全部眩晕 3 秒，你对每个敌人各开一枪（必中，最多 8 个）。要拿着火器。冷却 5 天。', 'LP/Arcana/Immovable', 'LP_ClassSelfBase',
                      comp('GSArcStop', GP, radius=30), cd=300000, warm=0.5, hostile=True, ext=arcext(8, GC)))
    AR.append(ability('LP_AC_FateBullet', '命运之弹', '秘传（9 环枪手）：一发写好结局的子弹：必中，伤害 ×5，穿甲 +100%，无视掩体（不需要视线）。要拿着火器。冷却 7 天。', 'LP/Arcana/FingerOfDeath', 'LP_ClassMeleeBase',
                      comp('GSArcFate', GP), cd=420000, rng=60, warm=1, verb_extra='<requireLineOfSight>false</requireLineOfSight>', ext=arcext(9, GC)))
    P.append('  <!-- ===== 秘传 ===== -->\n' + '\n'.join(AR))
    P.append(hediff('LP_GS_RunesBuff', '符文魔弹', '子弹上刻着燃烧的符文：火器打中时另造成 50% 的火焰伤害。', color=C))
    CR = [crystal('RuneBullets', 'LP_AC_RuneBullets', '符文魔弹', '枪手', 6, '秘传（6 环枪手）：60 秒内火器子弹附带 50% 火焰伤害。冷却 2 天。', C),
          crystal('BulletRain', 'LP_AC_BulletRain', '弹雨', '枪手', 7, '秘传（7 环枪手）：对一片区域开 20 枪。冷却 3 天。', C),
          crystal('StoppedTime', 'LP_AC_StoppedTime', '时停一枪', '枪手', 8, '秘传（8 环枪手）：30 格内敌人眩晕 3 秒，每人吃你一枪。冷却 5 天。', C),
          crystal('FateBullet', 'LP_AC_FateBullet', '命运之弹', '枪手', 9, '秘传（9 环枪手）：必中、×5 伤害、无视掩体的一枪。冷却 7 天。', C)]
    P.append('\n'.join(CR))
    write('ClassGunslinger.xml', '2026-11-19 枪手：由 ArtSource/NewClasses/gen_newclasses.py 生成，不要手改。效果在 Source/ClassGunslinger.cs。', P)

psion()
gunslinger()
