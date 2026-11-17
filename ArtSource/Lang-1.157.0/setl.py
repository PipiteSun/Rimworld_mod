import sys
f=sys.argv[1]; pairs=dict(zip(sys.argv[2::2],sys.argv[3::2]))
L=open(f,encoding='utf-8').read().split('\n')
L=[(l.split('\t')[0]+'\t'+pairs[l.split('\t')[0]]) if l.split('\t')[0] in pairs else l for l in L]
open(f,'w',encoding='utf-8').write('\n'.join(L))
