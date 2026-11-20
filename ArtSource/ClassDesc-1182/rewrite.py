# 1.182.0 热修：职业面板描述精简：一行特色 + 一行强度（主属性、相关技能），不列逐级能力。
# 用法：python -I rewrite.py  （在模组根目录外也可，路径按本文件位置算）
import re, pathlib, shutil
ROOT = pathlib.Path(__file__).resolve().parents[2]
BK = ROOT / 'ArtSource' / 'backup-1.182.0-classdesc'

ZH = {
 'LP_Class_Fighter': ('武器大师，近战射击都能打，靠斗志连续爆发。',
                      '强度：近战能力看「战技强度」（主属性力量或敏捷，技能格斗）；射击能力看「射术强度」（主属性敏捷，技能射击）。'),
 'LP_Class_Monk': ('以气御身的徒手高手，拳脚快、闪避强，越近越危险。',
                   '强度：「战技强度」（主属性敏捷或力量，技能格斗）；气的效果另乘心灵敏感度。徒手或拿短棍时最强。'),
 'LP_Class_Paladin': ('以誓言为剑的圣战士，前排挥砍，同时治疗和守护队友。',
                      '强度：斩击类看「战技强度」（主属性力量或敏捷，技能格斗）；治疗与灵光看「神恩强度」（主属性魅力或感知，技能社交）。巫妖不能当圣武士。'),
 'LP_Class_Wizard': ('博学的奥术施法者，法术书最全，火球、传送、控场样样有。',
                     '强度：「奥术强度」（主属性智力，技能智识）；伤害与治疗另乘心灵敏感度。'),
 'LP_Class_Cleric': ('神明的代言人，治疗、复生和驱散亡灵的核心。',
                     '强度：「神恩强度」（主属性感知或魅力，技能社交）；伤害与治疗另乘心灵敏感度。'),
 'LP_Class_Druid': ('自然的守护者，驯兽、变形、催生植物、呼风唤雷。',
                    '强度：野兽类看「野性强度」（主属性感知，技能驯兽）；植物与天气类看「生长强度」（主属性感知，技能种植）。巫妖不能当德鲁伊。'),
 'LP_Class_Sorcerer': ('天生带着魔力的施法者，用超魔改造法术，爆发高。',
                       '强度：魅惑类看「天赋强度」（主属性魅力，技能社交）；元素类看「奥术强度」（主属性智力，技能智识）。有术士血脉的种族更强。'),
 'LP_Class_Barbarian': ('以怒为力的狂战士，挨打越多越凶，皮糙肉厚。',
                        '强度：「狂怒强度」（主属性力量，技能格斗）。狂暴中不能用施法职业的能力。'),
 'LP_Class_Ranger': ('荒野猎手，弓箭为主，带着兽伴追猎宿敌。',
                     '强度：「猎术强度」（主属性敏捷或感知，技能射击或格斗，取较高者）。'),
 'LP_Class_Rogue': ('靠诡计取胜的刺客，潜行偷袭、一击致命。',
                    '强度：「诡术强度」（主属性敏捷，技能格斗或射击，取较高者）。'),
 'LP_Class_Bard': ('用歌声施法的多面手，激励队友、扰乱敌人，什么都会一点。',
                   '强度：「激昂强度」（主属性魅力，技能社交或艺术，取较高者）；伤害与治疗另乘心灵敏感度。'),
 'LP_Class_Warlock': ('与异界宗主立约的施法者，魔能爆连发，法力少但回得快。',
                      '强度：「契约强度」（主属性魅力，技能社交或智识，取较高者）。'),
 'LP_Class_Psion': ('用纯粹心灵之力施法，念动推拉、心灵感应，还能借神经热续航。',
                    '强度：「灵能强度」（主属性智力，技能智识）；伤害与治疗另乘心灵敏感度。心灵失聪的人不能学。'),
 'LP_Class_Gunslinger': ('靠胆识和一把好枪吃饭的火器专家，只用火器，还会造枪。',
                         '强度：「枪术强度」（主属性敏捷，技能射击）。'),
}
EN = {
 'LP_Class_Fighter': ('A weapon master at home in melee or at range, spending fighting spirit on bursts of action.',
                      'Power: melee abilities use martial power (Strength or Dexterity, Melee); ranged abilities use marksman power (Dexterity, Shooting).'),
 'LP_Class_Monk': ('An unarmed master of ki: fast strikes, strong evasion, deadliest up close.',
                   'Power: martial power (Dexterity or Strength, Melee); ki effects also scale with psychic sensitivity. Strongest unarmed or with a quarterstaff.'),
 'LP_Class_Paladin': ('A holy warrior bound by an oath, fighting in front while healing and shielding allies.',
                      'Power: smites use martial power (Strength or Dexterity, Melee); healing and auras use divine power (Charisma or Wisdom, Social). Liches cannot become paladins.'),
 'LP_Class_Wizard': ('A scholar of arcane magic with the widest spellbook: fireballs, teleports, control.',
                     'Power: arcane power (Intelligence, Intellectual); damage and healing also scale with psychic sensitivity.'),
 'LP_Class_Cleric': ("A god's voice: the core of healing, revival and turning undead.",
                     'Power: divine power (Wisdom or Charisma, Social); damage and healing also scale with psychic sensitivity.'),
 'LP_Class_Druid': ('A guardian of nature who tames beasts, shapeshifts, grows plants and calls storms.',
                    'Power: beast abilities use primal power (Wisdom, Animals); plant and weather abilities use verdant power (Wisdom, Plants). Liches cannot become druids.'),
 'LP_Class_Sorcerer': ('A caster born with magic who reshapes spells with metamagic for big bursts.',
                       'Power: charm abilities use innate power (Charisma, Social); elemental abilities use arcane power (Intelligence, Intellectual). Races with sorcerous blood are stronger.'),
 'LP_Class_Barbarian': ('A berserker fuelled by rage: the more hits taken, the fiercer and tougher.',
                        'Power: rage power (Strength, Melee). Cannot use caster abilities while raging.'),
 'LP_Class_Ranger': ('A hunter of the wilds who fights mainly with the bow, with a beast companion and favored enemies.',
                     'Power: hunt power (Dexterity or Wisdom, Shooting or Melee, whichever is higher).'),
 'LP_Class_Rogue': ('A trickster assassin: stealth, sneak attacks, one deadly strike.',
                    'Power: cunning power (Dexterity, Melee or Shooting, whichever is higher).'),
 'LP_Class_Bard': ('A versatile singer-caster who inspires allies, confounds foes and knows a bit of everything.',
                   'Power: bardic power (Charisma, Social or Artistic, whichever is higher); damage and healing also scale with psychic sensitivity.'),
 'LP_Class_Warlock': ('A caster bound to an otherworldly patron: rapid eldritch blasts, little mana but quick to refill.',
                      'Power: pact power (Charisma, Social or Intellectual, whichever is higher).'),
 'LP_Class_Psion': ('A caster of pure mental power: telekinesis, telepathy, and neural heat to keep going.',
                    'Power: psionic power (Intelligence, Intellectual); damage and healing also scale with psychic sensitivity. The psychically deaf cannot become psions.'),
 'LP_Class_Gunslinger': ('A firearms expert living on grit and a good gun; firearms only, and a gunsmith too.',
                         'Power: gunslinging power (Dexterity, Shooting).'),
}

def esc(s): return s.replace('&', '&amp;').replace('<', '&lt;').replace('>', '&gt;')
def join(t): return t[0] + '\\n' + t[1]

def backup(p):
    dst = BK / p.relative_to(ROOT)
    if not dst.exists():
        dst.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(p, dst)

def write(p, s):
    backup(p); p.write_bytes(s.encode('utf-8'))

done = set(); olds = {}
for p in sorted((ROOT / 'Defs').glob('*.xml')):
    s = p.read_bytes().decode('utf-8'); orig = s
    for dn, t in ZH.items():
        m = re.search(r'(<LichPhylactery\.LP_ClassDef>\s*<defName>' + dn + r'</defName>.*?<description>)(.*?)(</description>)', s, re.S)
        if m:
            olds[dn] = m.group(2)
            s = s[:m.start(2)] + esc(join(t)) + s[m.end(2):]; done.add(dn)
    if s != orig: write(p, s)
assert done == set(ZH), set(ZH) - done

en_done = set()
for p in sorted((ROOT / 'Languages' / 'English' / 'DefInjected' / 'LichPhylactery.LP_ClassDef').glob('*.xml')):
    s = p.read_bytes().decode('utf-8'); orig = s
    for dn, t in EN.items():
        s, n = re.subn(r'(<' + dn + r'\.description>).*?(</' + dn + r'\.description>)', lambda m: m.group(1) + esc(join(t)) + m.group(2), s, flags=re.S)
        if n: en_done.add(dn)
    if s != orig: write(p, s)
assert en_done == set(EN), set(EN) - en_done

# 生成脚本：心灵术士 / 枪手的中文写在 gen_newclasses.py 的 f''' 模板里，英文在 gen_english.py 的 D 字典里。
g = ROOT / 'ArtSource' / 'NewClasses' / 'gen_newclasses.py'; s = g.read_bytes().decode('utf-8'); o = s
for dn in ('LP_Class_Psion', 'LP_Class_Gunslinger'):
    assert olds[dn] in s, dn
    s = s.replace(olds[dn], esc(ZH[dn][0]) + '\\\\n' + esc(ZH[dn][1]))
if s != o: write(g, s)
g = ROOT / 'ArtSource' / 'NewClasses' / 'gen_english.py'; s = g.read_bytes().decode('utf-8'); o = s
for dn in ('LP_Class_Psion', 'LP_Class_Gunslinger'):
    k = s.index('\nD = {')  # 跳过前面 label 字典里同名的键
    t, n = re.subn(r"('" + dn + r"': )'(?:[^'\\]|\\.)*'", lambda m: m.group(1) + repr(EN[dn][0] + '\\n' + EN[dn][1]), s[k:], count=1)
    s = s[:k] + t
    assert n == 1, dn
if s != o: write(g, s)
print('ok', len(done), len(en_done))
