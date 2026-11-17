import json,re,io
L=[json.loads(l) for l in open('cs_keys.jsonl',encoding='utf-8')]
src='../../Source/'
done=set()
for x in L:
    if x['reason']!='const' or (x['file'],x['line']) in done: continue
    done.add((x['file'],x['line']))
    p=src+x['file']; raw=open(p,'rb').read(); bom=raw.startswith(b'\xef\xbb\xbf')
    lines=raw.decode('utf-8-sig').split('\n'); i=x['line']-1; s=lines[i]
    n=s.replace('public const string','public static readonly string',1)
    if n==s: n=re.sub(r'\bconst string\b','string',s,1) if 'static' not in s.split('const')[0] and re.search(r'^\s*const string',s) else re.sub(r'\bconst string\b','static readonly string',s,1)
    print(x['file'],x['line'],'->',n.strip()[:90]); lines[i]=n
    open(p,'wb').write((b'\xef\xbb\xbf' if bom else b'')+'\n'.join(lines).encode('utf-8'))
