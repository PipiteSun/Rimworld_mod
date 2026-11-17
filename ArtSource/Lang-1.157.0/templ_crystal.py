# 职业水晶说明是同一个模板 + 已译过的秘传说明：按模板自动拼英文。用法：python templ_crystal.py b025
import re, sys, glob, json
ids = json.load(open('pool.json', encoding='utf-8'))['ids']
inv = {v: k for k, v in ids.items()}
tr = {}
for f in glob.glob('tr/*.txt'):
    for ln in open(f, encoding='utf-8'):
        ln = ln.rstrip('\n')
        if '\t' in ln: k, v = ln.split('\t', 1); tr[k] = v
def en(zh):
    i = ids.get(zh); return tr.get(i) if i else None
CLS = {'圣武士': 'paladin', '德鲁伊': 'druid', '游侠': 'ranger', '野蛮人': 'barbarian', '法师': 'wizard', '武僧': 'monk', '游荡者': 'rogue',
       '牧师': 'cleric', '术士': 'sorcerer', '吟游诗人': 'bard', '战士': 'fighter', '邪术师': 'warlock'}
PAT = re.compile(r'^魔力湖泊的职业水晶。(\S+?)吸收后永久学会(被动)?秘传「(.+?)」（(\d+) 环，需要\S+? (\d+) 级，占 1 个秘传槽；冒险者 11 / 15 / 20 级、传奇 30 级各有 1 个秘传槽）。\\n\\n(.+)$')
b = sys.argv[1]
out = []; miss = []
for ln in open('batches/%s.txt' % b, encoding='utf-8'):
    ln = ln.rstrip('\n')
    if ln.startswith('#') or '\t' not in ln: continue
    i, zh = ln.split('\t', 1)
    if i in tr: continue
    m = PAT.match(zh)
    if m:
        cls, passive, name, circ, lvl, tail = m.groups()
        ename = en(name); etail = en(tail.replace('\\n', '\n'))
        if not ename or not etail or cls not in CLS: miss.append(i); continue
        c = CLS[cls]
        out.append('%s\tA Mana Lake class crystal. A %s who absorbs it permanently learns the %sarcanum "%s" (circle %s, requires %s level %s, takes 1 arcanum slot; there is 1 arcanum slot each at adventurer levels 11 / 15 / 20 and legend level 30).\\n\\n%s' % (i, c, 'passive ' if passive else '', ename[0].upper() + ename[1:], circ, c, lvl, etail))
        continue
    m2 = re.match(r'^职业水晶：(.+)$', zh)
    if m2 and en(m2.group(1)):
        out.append('%s\tclass crystal: %s' % (i, en(m2.group(1)))); continue
    miss.append(i)
open('tr/%s_auto.txt' % b, 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('auto', len(out), 'missing', miss)
