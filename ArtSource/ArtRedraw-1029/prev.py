import sys,os
TINT=tuple(int(x) for x in os.environ.get('TINT','150,112,78').split(','))
from PIL import Image
names=sys.argv[2:]; out=sys.argv[1]
rows=[]
for n in names:
    ims=[]
    for r in ('south','north','east'):
        im=Image.open(f'out/{n}_{r}.png').convert('RGBA')
        try:
            mk=Image.open(f'out/{n}_{r}m.png').convert('RGB')
            import numpy as np
            a=np.asarray(im).astype(float); mm=np.asarray(mk)[...,0:1]/255.0
            tint=np.array(TINT)/255.0
            a[...,:3]=a[...,:3]*(1-mm+mm*tint)
            im=Image.fromarray(a.astype('uint8'),'RGBA')
        except FileNotFoundError: pass
        ims.append(im)
    rows.append(ims)
W=max(sum(i.width for i in r)+60 for r in rows); H=sum(max(i.height for i in r)+20 for r in rows)
s=Image.new('RGBA',(W,H),(74,78,72,255));y=0
for r in rows:
    x=10
    for i in r: s.paste(i,(x,y+10),i); x+=i.width+20
    y+=max(i.height for i in r)+20
s.save(out)
