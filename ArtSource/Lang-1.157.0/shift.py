import sys
# usage: shift.py file drop_id from_id  -> drops my drop_id line, shifts ids>=from_id by -1
f,drop,frm=sys.argv[1],int(sys.argv[2][1:]),int(sys.argv[3][1:])
L=open(f,encoding='utf-8').read().split('\n'); out=[]
for l in L:
    if '\t' not in l: out.append(l); continue
    k,v=l.split('\t',1); n=int(k[1:])
    if n==drop: continue
    if n>=frm: n-=1
    out.append('s%05d\t%s'%(n,v))
open(f,'w',encoding='utf-8').write('\n'.join(out))
