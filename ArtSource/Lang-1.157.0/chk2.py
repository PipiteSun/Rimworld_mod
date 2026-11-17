import glob,re,sys
tr={}
for t in glob.glob('tr/*.txt'):
    for l in open(t,encoding='utf-8').read().split('\n'):
        if '\t' in l: k,v=l.split('\t',1); tr[k]=v.replace('\s',' ')
for b in sorted(glob.glob('batches/b0[4-5]*.txt')):
    for l in open(b,encoding='utf-8').read().split('\n'):
        if '\t' not in l: continue
        k,v=l.split('\t',1)
        if k<'s08831' or k not in tr: continue
        e=tr[k]; flags=[]
        if v.startswith(' ') and not e.startswith(' '): flags.append('lead')
        if v.startswith('\n') != e.startswith('\n'): flags.append('nl')
        if v.endswith('\n') != e.endswith('\n'): flags.append('nlend')
        if ('{' in v)!=('{' in e): flags.append('brace')
        if flags: print(k,flags,v[:20],'|',e[:40])
