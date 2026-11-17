import json,glob,re,os,collections
MOD='../..'
E={}
for f in sorted(glob.glob('exp_*.jsonl')):
    for l in open(f,encoding='utf-8'):
        x=json.loads(l); E.setdefault((x['type'],x['def'],x['path']),x)
# defName -> folders from XML
defloc=collections.defaultdict(set)
patchtext={}
for f in glob.glob(MOD+'/**/*.xml',recursive=True):
    r=os.path.relpath(f,MOD).replace(os.sep,'/')
    if r.startswith(('ArtSource','Languages','Shelved','About')) : continue
    m=re.match(r'Bridges/([^/]+)/',r); folder=m.group(1) if m else ''
    s=open(f,encoding='utf-8-sig',errors='replace').read()
    if '/Patches/' in '/'+r or r.startswith('Patches'):
        patchtext[r]=(folder,s)
    for d in re.findall(r'<defName>\s*([^<\s]+)\s*</defName>',s): defloc[d].add(folder)
out=[];stat=collections.Counter()
for k,x in E.items():
    if x['pack']=='local.lich.phylactery':
        locs=defloc.get(x['def'])
        if not locs: stat['ours_noxml']+=1; x['dest']=None
        else: x['dest']=sorted(locs)[0] if len(locs)==1 else ''; stat['ours']+=1
    else:
        fs=[fo for r,(fo,s) in patchtext.items() if x['def'] in s]
        x['dest']=(fs[0] if len(set(fs))==1 else '') if fs else ''
        stat['foreign_'+str(x['pack'])+'_'+x['dest']]+=1
    out.append(x)
json.dump(out,open('entries.json','w',encoding='utf-8'),ensure_ascii=False,indent=0)
print(len(out),stat)
print([ (x['type'],x['def'],x['path']) for x in out if x['dest'] is None][:30])