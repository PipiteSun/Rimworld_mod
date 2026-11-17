# 1.157.0：把 tr/*.txt 的译文写成
#   Languages/English/DefInjected/<类型>/<原文件>.xml（联动桥的 Def 写进 Bridges/<桥>/Languages/English/DefInjected）
#   Languages/English/LPStrings/<源码文件>.xml（代码字符串，LPLang.Loc 读取）
# 并检查占位符（{0}、{PAWN_x}、[PAWN_x]、<color>、TargetA、规则名 ->）是否对得上。
import json, re, os, glob, collections, shutil
from xml.sax.saxutils import escape
MOD = '../..'
ids = json.load(open('pool.json', encoding='utf-8'))['ids']
tr = {}
for f in sorted(glob.glob('tr/*.txt')):
    for ln in open(f, encoding='utf-8'):
        ln = ln.rstrip('\n')
        if not ln or ln.startswith('#') or '\t' not in ln: continue
        k, v = ln.split('\t', 1)
        tr[k] = v
def unesc(s):  # 批次文件里的 \n \t \\ 还原
    out = []; i = 0
    while i < len(s):
        if s[i] == '\\' and i + 1 < len(s):
            c = s[i + 1]; out.append({'n': '\n', 't': '\t', 's': ' ', '\\': '\\'}.get(c, '\\' + c)); i += 2
        else: out.append(s[i]); i += 1
    return ''.join(out)
en = {}  # 中文 → 英文
for zh, i in ids.items():
    if i in tr:
        e = unesc(tr[i])
        if zh.endswith((' ', '　')) and not e.endswith((' ', '\n')): e += ' '  # 中文末尾有空格（后面接数值）时英文也补上
        en[zh] = e
manual = {}  # 手工补的代码字符串（改成 LocF 模板等），每行 中文<TAB>英文
if os.path.exists('manual_cs.tsv'):
    for ln in open('manual_cs.tsv', encoding='utf-8'):
        ln = ln.rstrip('\n')
        if '\t' in ln:
            a, b = ln.split('\t', 1); manual[unesc(a)] = unesc(b)
en.update(manual)
TOK = re.compile(r'\{[^{}]*\}|\[[A-Za-z0-9_]+\]|</?color[^>]*>|</?b>|</?i>|Target[A-C]|^[A-Za-z_0-9]+->')
PRON = re.compile(r'^[\[{][A-Z0-9_]+_(pronoun|objective|possessive|nameDef|labelShort|nameFull|label|definite|indefinite)[\]}]$')
def toks(s): return sorted(t for t in TOK.findall(s) if not PRON.match(t))  # 人称类语法记号（代词、名字）英文可多可少
bad = []
for zh, e in en.items():
    a, b = toks(zh), toks(e)
    if a != b:
        # 允许英文里同一占位符出现次数不同
        if sorted(set(a)) != sorted(set(b)): bad.append((ids[zh], zh, e, a, b))
    if re.search('[\u4e00-\u9fff]', e): bad.append((ids[zh], zh, e, 'CJK left', ''))
with open('check_tokens.txt', 'w', encoding='utf-8') as f:
    for x in bad: f.write('\t'.join(map(str, x)) + '\n')
# ---- DefInjected ----
defs = json.load(open('defs_final.json', encoding='utf-8'))
for root in [MOD + '/Languages/English/DefInjected'] + glob.glob(MOD + '/Bridges/*/Languages/English/DefInjected'):
    if os.path.isdir(root): shutil.rmtree(root)
files = collections.defaultdict(list)
tmap = dict(l.rstrip(chr(10)).split(chr(9)) for l in open('typemap.tsv', encoding='utf-8-sig') if chr(9) in l)
def tdir(t):
    f = tmap.get(t) or t
    return t if f.startswith(('Verse.', 'RimWorld.')) else f
SKIP_PATHS = ('LP_TrollBlood.ingestible.',)  # 运行时才由代码补上的字段（注入时还是 null，会报 lang error）
for x in defs:
    if x['path'].startswith(SKIP_PATHS): continue
    base = MOD + '/Languages/English/DefInjected' if not x['dest'] else MOD + '/Bridges/' + x['dest'] + '/Languages/English/DefInjected'
    fn = os.path.splitext(os.path.basename(x['file'] or 'Misc.xml'))[0] + '.xml'
    files[(base, tdir(x['type']), fn)].append(x)
def xv(s): return escape(s).replace('\n', '\\n')
def comment(s): return s.replace('--', '- -').replace('\n', '\\n')
ndone = nall = 0
for (base, typ, fn), xs in sorted(files.items()):
    lines = ['<?xml version="1.0" encoding="utf-8"?>', '<LanguageData>']
    any_ = False
    for x in xs:
        if x['col']:
            vals = x['vals']; nall += 1
            if not any(v in en for v in vals): continue
            ndone += 1; any_ = True
            lines.append('  <!-- ' + comment(' | '.join(vals))[:400] + ' -->')
            if x['full']:
                lines.append('  <%s>' % x['path'])
                for v in vals: lines.append('    <li>%s</li>' % xv(en.get(v, v)))
                lines.append('  </%s>' % x['path'])
            else:  # 不允许整表替换（没有 [TranslationCanChangeCount]）：按下标逐条
                for n, v in enumerate(vals):
                    if v in en: lines.append('  <%s.%d>%s</%s.%d>' % (x['path'], n, xv(en[v]), x['path'], n))
        else:
            nall += 1
            if x['val'] not in en: continue
            ndone += 1; any_ = True
            lines.append('  <!-- ' + comment(x['val'])[:400] + ' -->')
            lines.append('  <%s>%s</%s>' % (x['path'], xv(en[x['val']]), x['path']))
    lines.append('</LanguageData>')
    if not any_: continue
    d = os.path.join(base, typ); os.makedirs(d, exist_ok=True)
    open(os.path.join(d, fn), 'w', encoding='utf-8').write('\n'.join(lines) + '\n')
# ---- LPStrings ----
cs = [json.loads(l) for l in open('cs_keys.jsonl', encoding='utf-8')]
lpdir = MOD + '/Languages/English/LPStrings'
if os.path.isdir(lpdir): shutil.rmtree(lpdir)
os.makedirs(lpdir)
byfile = collections.OrderedDict(); seen = set()
for x in sorted(cs, key=lambda x: (x['file'], x['line'])):
    if x['reason'] != 'wrapped' or x['key'] in seen: continue
    seen.add(x['key']); byfile.setdefault(x['file'], []).append(x['key'])
def sv(s): return escape(s.replace(chr(10), chr(92) + 'n').replace(chr(9), chr(92) + 't'))
ncs = ncsall = 0
for fn, keys in byfile.items():
    out = ['<?xml version="1.0" encoding="utf-8"?>', '<!-- 1.157.0 代码字符串（%s）：<zh> 中文原文，<en> 译文；首尾空格有意义。 -->' % fn, '<LPStrings>']
    k2 = 0
    for k in keys:
        ncsall += 1
        if k not in en: continue
        ncs += 1; k2 += 1
        out.append('  <s><zh>%s</zh><en>%s</en></s>' % (sv(k), sv(en[k])))
    out.append('</LPStrings>')
    if k2: open(os.path.join(lpdir, os.path.splitext(fn)[0] + '.xml'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
if manual:
    out = ['<?xml version="1.0" encoding="utf-8"?>', '<!-- 1.157.0 手工补的代码字符串 -->', '<LPStrings>']
    for k, v in manual.items(): out.append('  <s><zh>%s</zh><en>%s</en></s>' % (sv(k), sv(v)))
    out.append('</LPStrings>')
    open(os.path.join(lpdir, '_Manual.xml'), 'w', encoding='utf-8').write('\n'.join(out) + '\n')
print('translated', len(en), '/', len(ids), '| def fields', ndone, '/', nall, '| cs strings', ncs, '/', ncsall, '| token problems', len(bad))
