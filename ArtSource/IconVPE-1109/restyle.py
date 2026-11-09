"""VPE-style restyle of DND ability icons (2026-11-09).
restyle(src_rgba_128) -> rgba_128. Pure post-process: fit to frame, khaki figures,
snap colours to VPE palette (hard cel bands), pure-black outline, strip glow halos."""
import numpy as np, colorsys, os
from PIL import Image
from scipy import ndimage as nd

HERE=os.path.dirname(os.path.abspath(__file__))
PAL=np.load(os.path.join(HERE,'vpepal.npy'))          # 28 VPE colours (k-means of VPE icons)
FIT=0.93; INK=(18,18,18); RING=0

def _lab(rgb):
    c=rgb/255.0; c=np.where(c>0.04045,((c+0.055)/1.055)**2.4,c/12.92)
    M=np.array([[0.4124,0.3576,0.1805],[0.2126,0.7152,0.0722],[0.0193,0.1192,0.9505]])
    xyz=c@M.T/np.array([0.9505,1.0,1.089])
    f=np.where(xyz>0.008856,np.cbrt(xyz),7.787*xyz+16/116)
    return np.stack([116*f[...,1]-16,500*(f[...,0]-f[...,1]),200*(f[...,1]-f[...,2])],-1)
# extra muted tones VPE uses but k-means merged away (skin/tan, cream, pale green)
PAL=np.vstack([PAL,[[204,170,132],[160,118,82],[112,80,56],[232,222,190],[150,180,120],[70,58,90],[40,44,66]]])
PLAB=_lab(PAL)
# khaki figure ramp (VPE figure light / mid / shade)
KHAKI=np.array([[214,212,170],[182,182,141],[135,135,91],[96,86,56]],float)

KHAKI_FIG=True
def _recolor(rgb):
    flat=rgb.reshape(-1,3).astype(float)
    hsv=np.array([colorsys.rgb_to_hsv(*(p/255)) for p in flat]) if len(flat)<5000 else None
    r,g,b=flat.T/255; mx=flat.max(1)/255; mn=flat.min(1)/255
    v=mx; s=np.where(mx>0,(mx-mn)/np.maximum(mx,1e-6),0)
    # hue
    h=np.zeros_like(v); d=np.maximum(mx-mn,1e-6)
    h=np.where(mx==r,((g-b)/d)%6,np.where(mx==g,(b-r)/d+2,(r-g)/d+4))/6
    fig=(h>0.55)&(h<0.69)&(s>0.11)&(s<0.27)&(v>0.33)&(v<0.82)
    out=flat.copy()
    # figures -> khaki by brightness
    if not KHAKI_FIG: fig[:]=False
    rest=~fig
    if fig.any():
        k=np.clip(np.digitize(v[fig],[0.74,0.60,0.45])[:,None],0,3)
        idx=np.digitize(v[fig],[0.45,0.60,0.74])      # 0 dark .. 3 light
        out[fig]=KHAKI[3-idx]
    rest=~fig
    L=_lab(flat[rest])
    dist=((L[:,None,:]-PLAB[None])**2*np.array([1.0,1.4,1.4])).sum(-1)
    # keep hue family: penalise palette colours whose hue angle is far from a chromatic pixel
    C=np.hypot(L[:,1],L[:,2]); PC=np.hypot(PLAB[:,1],PLAB[:,2])
    H=np.arctan2(L[:,2],L[:,1]); PH=np.arctan2(PLAB[:,2],PLAB[:,1])
    dh=np.abs((H[:,None]-PH[None]+np.pi)%(2*np.pi)-np.pi)
    chrom=(C>8)[:,None]
    dist=dist+np.where(chrom&((dh>0.45)|(PC[None]<8)),4000,0)
    dL=np.abs(L[:,0:1]-PLAB[None,:,0])
    dist=dist+np.where(dL>16,4000,0)
    q=PAL[dist.argmin(1)]
    # palette lacks some saturated/dark families: if still far, mute the original instead
    far=dist.min(1)>3500
    if far.any():
        o=flat[rest][far]; g=o@[0.299,0.587,0.114]
        q[far]=g[:,None]+(o-g[:,None])*0.7
    out[rest]=q
    return out.reshape(rgb.shape)

def restyle(img):
    a=np.array(img.convert('RGBA')).astype(float)
    al=a[...,3]/255
    # strip faint glow/halo: keep only solid core + its 1px antialias
    solid=al>0.55
    solid=nd.binary_fill_holes(solid) & (al>0.55) | (nd.binary_fill_holes(al>0.55)&(al>0.55))
    keep=nd.binary_dilation(solid,iterations=1)
    al=np.where(keep,al,0); al=np.where(solid,1.0,al*0.5)
    rgb=a[...,:3]
    lum=rgb@[0.299,0.587,0.114]
    sat=(rgb.max(-1)-rgb.min(-1))/np.maximum(rgb.max(-1),1)
    ink=solid&(((lum<62)&(sat<0.25))|(lum<22))
    col=solid&~ink
    out=rgb.copy()
    if col.any(): out[col]=_recolor(rgb[col][None])[0]
    out[ink]=INK
    # fit to frame
    ys,xs=np.where(al>0.3)
    if len(xs)==0: return img
    x0,x1,y0,y1=xs.min(),xs.max()+1,ys.min(),ys.max()+1
    rgba=np.dstack([out,al*255]).clip(0,255).astype(np.uint8)
    crop=Image.fromarray(rgba[y0:y1,x0:x1],'RGBA')
    W=a.shape[1]; tgt=FIT*W; sc=min(tgt/(x1-x0),tgt/(y1-y0),1.6)
    sc=max(sc,1.0)                                   # never shrink
    big=crop.resize((max(1,round((x1-x0)*sc*4)),max(1,round((y1-y0)*sc*4))),Image.NEAREST)
    # ensure min outer ink ~2.4px @128 (=10px @512): dilate silhouette with ink
    ba=np.array(big).astype(float); m=ba[...,3]>128
    ring=nd.binary_dilation(m,structure=nd.generate_binary_structure(2,1),iterations=RING)&~m if RING else np.zeros_like(m)
    ba[ring]=[*INK,255]
    big=Image.fromarray(ba.astype(np.uint8),'RGBA')
    canvas=Image.new('RGBA',(W*4,W*4),(0,0,0,0))
    canvas.alpha_composite(big,((W*4-big.width)//2,(W*4-big.height)//2))
    res=canvas.resize((W,W),Image.LANCZOS)
    r=np.array(res).astype(float)
    # premultiplied-ish cleanup: un-darken edges
    return Image.fromarray(r.astype(np.uint8),'RGBA')
