# Scan mod Defs for equipment (apparel / weapons) and list their texture files.
import glob, json, os, re, sys
import xml.etree.ElementTree as ET

ROOT = sys.argv[1]
OUT = sys.argv[2]
defs, abstract = [], {}
for f in glob.glob(os.path.join(ROOT, '**', 'Defs', '**', '*.xml'), recursive=True):
    if '\\ArtSource\\' in f:
        continue
    try:
        r = ET.parse(f).getroot()
    except Exception as e:
        print('parse fail', f, e)
        continue
    texroot = os.path.join(f.split('\\Defs\\')[0], 'Textures')
    for n in r:
        if n.tag != 'ThingDef':
            continue
        if n.get('Name'):
            abstract[n.get('Name')] = n
        defs.append((n, f, texroot))

KIND_RE = re.compile(r'Weapon|Gun|Melee|Bow|Grenade|Throw', re.I)
APP_RE = re.compile(r'Apparel|Armor|Headgear|Hat|Clothing|Helmet', re.I)


def chain(n):
    seen = 0
    while n is not None and seen < 12:
        yield n
        p = n.get('ParentName')
        if not p:
            return
        if p not in abstract:
            yield p
            return
        n = abstract[p]
        seen += 1


def kind(n):
    for x in chain(n):
        if isinstance(x, str):
            if APP_RE.search(x):
                return 'apparel'
            if KIND_RE.search(x):
                return 'weapon'
            return None
        if x.find('apparel') is not None:
            return 'apparel'
        if x.find('weaponTags') is not None or (x.findtext('equipmentType') or '') == 'Primary' or x.find('equippedAngleOffset') is not None:
            return 'weapon'
    return None


def get(n, path):
    for x in chain(n):
        if isinstance(x, str):
            return None
        v = x.find(path)
        if v is not None:
            return v.text
    return None


rows, files = [], {}
for n, f, texroot in defs:
    if n.get('Abstract') == 'True':
        continue
    k = kind(n)
    if not k:
        continue
    dn = n.findtext('defName')
    tex = get(n, 'graphicData/texPath')
    cls = get(n, 'graphicData/graphicClass') or 'Graphic_Single'
    worn = get(n, 'apparel/wornGraphicPath')
    out = []
    roots = [texroot, os.path.join(ROOT, 'Textures')]
    def find(p, pat):
        for tr in roots:
            g = glob.glob(os.path.join(tr, p.replace('/', '\\')) + pat)
            if g:
                return g
        return []
    if tex:
        if 'Multi' in cls:
            out += [(x, 'icon') for x in find(tex, '_*.png')]
        elif 'Random' in cls or 'Collection' in cls or 'Stack' in cls:
            out += [(x, 'icon') for x in find(tex, '\\*.png')]
        else:
            out += [(x, 'icon') for x in find(tex, '.png')]
    if worn:
        out += [(x, 'worn') for x in find(worn, '_*.png')]
    for p, role in out:
        if p.lower().endswith('m.png') and re.search(r'_m\.png$|(north|south|east|west)m\.png$', p, re.I):
            continue
        if k == 'weapon' and role == 'icon':
            role = 'weapon'
        files.setdefault(os.path.normpath(p), (role, dn))
    rows.append((dn, k, tex, cls, worn, len(out)))

json.dump({p: v for p, v in sorted(files.items())}, open(OUT, 'w', encoding='utf8'), ensure_ascii=False, indent=0)
missing = [r for r in rows if r[5] == 0]
print('defs', len(rows), 'files', len(files), 'missing', len(missing))
for r in missing:
    print('  no tex', r)
from collections import Counter
print(Counter(v[0] for v in files.values()))
