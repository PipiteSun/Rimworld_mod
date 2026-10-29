# 从 backstories.txt 生成 Defs/Backstories_DND.xml 和一份可读的总表。
# 用法：python gen_backstories.py <backstories.txt> <输出目录>
import sys, os, html
src, out = sys.argv[1], sys.argv[2]
rows = []
for line in open(src, encoding='utf-8'):
    line = line.rstrip('\n')
    if not line or line.startswith('#'):
        continue
    cats, slot, key, title, short, skills, dis, trait, desc = line.split('|', 8)
    rows.append(dict(cats=cats.split(','), slot=slot, key=key, title=title, short=short,
                     skills=[s for s in skills.split(',') if s], dis=[d for d in dis.split(',') if d],
                     trait=trait, desc=desc))
keys = [r['key'] for r in rows]
dup = {k for k in keys if keys.count(k) > 1}
assert not dup, dup
esc = lambda s: html.escape(s, quote=False)
x = ['<?xml version="1.0" encoding="utf-8"?>', '<Defs>',
     '  <!-- 1.46.0 DND 背景故事，由 ArtSource/Backstories/gen_backstories.py 从 backstories.txt 生成，不要手改 -->']
for r in rows:
    slot = 'Childhood' if r['slot'] == 'C' else 'Adulthood'
    x.append('  <BackstoryDef>')
    x.append(f'    <defName>LP_Bs_{r["key"]}</defName>')
    x.append(f'    <title>{esc(r["title"])}</title>')
    x.append(f'    <titleShort>{esc(r["short"])}</titleShort>')
    x.append(f'    <description>{esc(r["desc"])}</description>')
    x.append(f'    <slot>{slot}</slot>')
    x.append('    <spawnCategories>' + ''.join(f'<li>{c}</li>' for c in r['cats']) + '</spawnCategories>')
    if r['skills']:
        x.append('    <skillGains>')
        for s in r['skills']:
            name, val = s.split('+')
            x.append(f'      <{name}>{val}</{name}>')
        x.append('    </skillGains>')
    if r['dis']:
        x.append(f'    <workDisables>{", ".join(r["dis"])}</workDisables>')
    if r['trait']:
        t, c = r['trait'].split(':')
        x.append('    <modExtensions>')
        x.append(f'      <li Class="LichPhylactery.BackstoryTraitExtension"><trait>LP_Background_{t}</trait><chance>{c}</chance></li>')
        x.append('    </modExtensions>')
    x.append('  </BackstoryDef>')
x.append('</Defs>')
os.makedirs(out, exist_ok=True)
open(os.path.join(out, 'Backstories_DND.xml'), 'w', encoding='utf-8', newline='\n').write('\n'.join(x) + '\n')
print('total', len(rows))
