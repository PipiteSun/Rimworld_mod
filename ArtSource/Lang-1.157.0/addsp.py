import sys,re
ids=set(sys.argv[2].split(','))
p=sys.argv[1]; L=open(p,encoding='utf-8').read().split('\n'); n=0
for i,l in enumerate(L):
    k=l.split('\t',1)[0]
    if k in ids and '\t' in l and not l.endswith(' '): L[i]=l+' '; n+=1
open(p,'w',encoding='utf-8').write('\n'.join(L)); print(p,n)
