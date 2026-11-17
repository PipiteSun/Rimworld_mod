import glob,os,re,sys
tr={}
for t in glob.glob('tr/*.txt'):
    for l in open(t,encoding='utf-8').read().split('\n'):
        m=re.match(r'^(s\d+)(\t?)(.*)$',l)
        if m:
            if not m.group(3): print('EMPTY',t,m.group(1))
            tr[m.group(1)]=m.group(3)
for b in sorted(glob.glob('batches/b*.txt')):
    src={l.split('\t')[0]:l.split('\t',1)[1] for l in open(b,encoding='utf-8').read().split('\n') if '\t' in l}
    miss=[k for k in src if k not in tr]
    if miss and len(miss)<len(src): print(b,'missing',len(miss),miss[:5])
    # spot check: src ends with CJK and starts with fragment? numbers parity
    for k,v in src.items():
        if k in tr:
            a=re.findall(r'\d+',v); c=re.findall(r'\d+',tr[k])
            if len(a)>=3 and not set(a)&set(c): print('NUM?',k,v[:30],'|',tr[k][:40])
