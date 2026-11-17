# 1.157.0：把 Def 字段（entries.json）和代码字符串（cs_keys.jsonl）合成一个去重的待译文本池，按来源文件分批写进 batches/bNNN.txt。
# 每行：id<TAB>中文（换行写成 \n）。译文写进 tr/bNNN.txt，同样的 id<TAB>English。
import json, re, os, collections, glob
import xml.etree.ElementTree as ET
os.makedirs('batches', exist_ok=True); os.makedirs('tr', exist_ok=True)
SKIP_FIELDS = re.compile(r'modExtensions\.\d+\.(school|strongLegend|legendFloor|capHigherOf|strongScores|weakScores)(\.|$)')
CJK = re.compile('[\u4e00-\u9fff\u3000-\u303f\uff00-\uffef]')
E = json.load(open('entries.json', encoding='utf-8'))
defs = []
for x in E:
    if x['pack'] != 'local.lich.phylactery' or x['dest'] is None: continue
    if SKIP_FIELDS.search(x['norm']): continue
    defs.append(x)
# 珠宝联动（kikohi.jewelry）没在测试档里加载：直接读 XML
for f in glob.glob('../../Bridges/Jewelry/Defs/*.xml'):
    root = ET.parse(f).getroot()
    for d in root:
        dn = d.findtext('defName')
        if not dn: continue
        for fld in ('label', 'description', 'jobString'):
            v = d.findtext(fld)
            if v and CJK.search(v):
                defs.append({'type': d.tag, 'def': dn, 'path': dn + '.' + fld, 'norm': dn + '.' + fld, 'col': False, 'full': False,
                             'pack': 'local.lich.phylactery', 'file': os.path.basename(f), 'dest': 'Jewelry', 'val': v})
json.dump(defs, open('defs_final.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=0)
cs = [json.loads(l) for l in open('cs_keys.jsonl', encoding='utf-8')]
cs = [x for x in cs if x['reason'] == 'wrapped']
order = []; seen = {}
def add(t, src):
    if t is None or not CJK.search(t): return
    if t not in seen: seen[t] = len(order); order.append((t, src))
for x in sorted(defs, key=lambda x: (x['file'] or '', x['def'], x['path'])):
    if x['col']:
        for v in x['vals']: add(v, x['file'])
    else: add(x['val'], x['file'])
ndef = len(order)
for x in sorted(cs, key=lambda x: (x['file'], x['line'])): add(x['key'], 'cs:' + x['file'])
ids = {t: 's%05d' % i for i, (t, _) in enumerate(order)}
json.dump({'ids': ids}, open('pool.json', 'w', encoding='utf-8'), ensure_ascii=False)
# 分批
def esc(t): return t.replace('\\', '\\\\').replace('\t', '\\t').replace('\r', '').replace('\n', '\\n')
batches = []; cur = []; size = 0; lastsrc = None
LIMIT = 6500
for t, src in order:
    if cur and (size + len(t) > LIMIT): batches.append(cur); cur = []; size = 0
    cur.append((ids[t], t, src)); size += len(t)
if cur: batches.append(cur)
for i, b in enumerate(batches):
    with open('batches/b%03d.txt' % i, 'w', encoding='utf-8') as f:
        last = None
        for id_, t, src in b:
            if src != last: f.write('# ' + str(src) + '\n'); last = src
            f.write(id_ + '\t' + esc(t) + '\n')
print('pool', len(order), 'defs', ndef, 'cs', len(order) - ndef, 'batches', len(batches), 'chars', sum(len(t) for t, _ in order))
