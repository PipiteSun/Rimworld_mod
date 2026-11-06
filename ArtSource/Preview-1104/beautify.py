# 2026-11-04 preview pipeline: geometry -> fill -> median -> palette -> outline (outline.py)
import os,re,sys,glob,json,numpy as np
from PIL import Image
from scipy import ndimage as ndi
ROOT=os.getcwd(); OUT=os.path.join(ROOT,'ArtSource','Preview-1104'); BK=os.path.join(ROOT,'ArtSource','backup-art-1102')
sys.path.insert(0,os.path.join(ROOT,'ArtSource','ArtRedraw-1102')); import outline as O
DIRS=['LP/Apparel','LP/Weapons','Items/Magic','Items/Wondrous','Items/Throwables','Items/RaceGear','Giants','Races124/Items','FireGiant']
MASK=re.compile(r'(_m|(north|south|east|west)m)\.png$',re.I)
DIRRE=re.compile(r'_(north|south|east|west)\.png$',re.I)
STRONG=('ChainShirt','FeatherCloak','CloudCrown','StormCrown','FrostBraids','ElvenLongbow')
def role(p):
    q=p.replace('\\','/')
    if '_Icon' in q: return 'icon'
    if DIRRE.search(q): return 'worn'
    if '/Weapons/' in q: return 'weapon'
    return 'icon'
def src_of(rel):
    b=os.path.join(BK,rel); c=os.path.join(ROOT,rel)
    if os.path.exists(b) and Image.open(b).size[0]>=Image.open(c).size[0]: return b
    return c
def mask_of(p):
    b=p[:-4]
    for m in (b+'_m.png',b+'m.png'):
        if os.path.exists(m): return m
def metr(a):
    s=a[...,3]>127
    if s.sum()<30: return None
    rgb=a[...,:3][s].astype(int)//8; uc=len(np.unique(rgb[:,0]*1024+rgb[:,1]*32+rgb[:,2]))
    L=a[...,:3].astype(float)@[.299,.587,.114]; g=np.hypot(ndi.sobel(L,0),ndi.sobel(L,1))
    inn=ndi.binary_erosion(s,iterations=max(2,a.shape[1]//40))
    return [uc,round(float((g[inn]>60).mean()) if inn.any() else 0,3)]
def geom(im,rl,size0):
    W,H=im.size; T=None
    if max(W,H)>256:
        k=256/max(W,H); T=('resize',(max(1,round(W*k)),max(1,round(H*k))))
    elif rl!='worn':
        bb=im.getchannel('A').point(lambda v:255 if v>10 else 0).getbbox()
        if bb:
            bw,bh=bb[2]-bb[0],bb[3]-bb[1]; k=min(max(0.83*W/max(bw,bh),0.9),1.6)
            if abs(k-1)>0.04: T=('fit',bb,k)
    return T
def apply(im,T):
    if T is None: return im
    if T[0]=='resize': return im.resize(T[1],Image.LANCZOS)
    _,bb,k=T; W,H=im.size; c=im.crop(bb); nw,nh=max(1,round(c.width*k)),max(1,round(c.height*k))
    c=c.resize((nw,nh),Image.LANCZOS); o=Image.new(im.mode,(W,H),(0,)*len(im.getbands()))
    o.paste(c,((W-nw)//2,(H-nh)//2)); return o
def simplify(im,strong):
    a=np.asarray(im).astype(np.float32); al=a[...,3]; s=al>40
    if s.sum()<10: return np.asarray(im)
    idx=ndi.distance_transform_edt(~s,return_indices=True)[1]
    rgb=a[...,:3][idx[0],idx[1]]
    k=max(3,int(round(a.shape[1]/128*(5 if strong else 3)))|1)
    rgb=np.stack([ndi.median_filter(rgb[...,i],size=k) for i in range(3)],-1)
    n=7 if strong else 12
    q=Image.fromarray(rgb.clip(0,255).astype(np.uint8)).quantize(colors=n,method=0,kmeans=3,dither=0).convert('RGB')
    return np.dstack([np.asarray(q),al.astype(np.uint8)])
files=[]
for d in DIRS:
    for f in glob.glob(os.path.join(ROOT,'Textures',*d.split('/'),'**','*.png'),recursive=True):
        if not MASK.search(f): files.append(os.path.relpath(f,ROOT))
only=sys.argv[1:]
log={}
for rel in files:
    if only and not any(o in rel for o in only): continue
    try:
        rl=role(rel); strong=any(s in rel for s in STRONG)
        im=Image.open(src_of(rel)).convert('RGBA'); before=metr(np.asarray(im))
        T=geom(im,rl,im.size); im2=apply(im,T)
        a=simplify(im2,strong); r,e=O.plan(a,rl)
        if r>0: out,cov=O.outline(a,r)
        else: out,cov=a,np.zeros(a.shape[:2])
        dst=os.path.join(OUT,rel); os.makedirs(os.path.dirname(dst),exist_ok=True)
        Image.fromarray(out,'RGBA').save(dst,optimize=True)
        m=mask_of(os.path.join(ROOT,rel))
        if m:
            mr=os.path.relpath(m,ROOT); mi=Image.open(src_of(mr)); mode=mi.mode
            mm=np.asarray(apply(mi.convert('RGBA'),T)).copy()
            if mm.shape[:2]==cov.shape: mm[cov>0.02,:3]=0
            o=Image.fromarray(mm,'RGBA'); (o if mode=='RGBA' else o.convert(mode)).save(os.path.join(OUT,mr),optimize=True)
        log[rel]={'role':rl,'before':before,'after':metr(out),'geom':str(T),'outline_add':round(float(r),2),'mask':bool(m)}
    except Exception as ex:
        log[rel]={'error':repr(ex)}
json.dump(log,open(os.path.join(OUT,'metrics.json'),'w',encoding='utf8'),ensure_ascii=False,indent=1)
print(len(log),sum('error' in v for v in log.values()))
