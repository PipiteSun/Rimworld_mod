import sys,os
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np
from PIL import Image,ImageEnhance,ImageDraw,ImageFont
SRC="E:/SteamLibrary/steamapps/common/RimWorld/output/LP-WildMonsters"
TEX="E:/SteamLibrary/steamapps/common/RimWorld/Mods/LichPhylactery/Textures/Things/Pawn/Animal/LP_WildMonsters"
OUT=sys.argv[1]; os.makedirs(OUT,exist_ok=True)
N={"RustMonster":"RustMonster","GelatinousCube":"GelatinCube","Basilisk":"Basilisk","Manticore":"Manticore","Bulette":"Bulette","CarrionCrawler":"CarrionCrawler"}
def key(im):
  a=np.asarray(im.convert("RGB")).astype(np.float32)
  r,g,b=a[...,0],a[...,1],a[...,2]
  m=np.minimum(r,b)-g            # magenta-ness
  alpha=np.clip((150-m)/90,0,1)  # m>=150 -> 0, m<=60 -> 1
  # unmix against magenta bg, then kill residual chroma on semi pixels
  A=np.maximum(alpha,1e-3)[...,None]; M=np.array([255,25,250],np.float32)
  F=np.clip((a-(1-A)*M)/A,0,255)
  semi=(alpha<0.98)[...,None]
  lo=np.minimum(F[...,1:2],np.minimum(F[...,0:1],F[...,2:3]))
  F2=F.copy(); F2[...,0:1]=np.minimum(F[...,0:1],F[...,1:2]+25); F2[...,2:3]=np.minimum(F[...,2:3],F[...,1:2]+25)
  F=np.where(semi,F2,F)
  out=np.dstack([F,alpha*255]).astype(np.uint8)
  img=Image.fromarray(out,"RGBA")
  # drop tiny specks: alpha<20 ->0
  return img
def fit(img,size,frac):
  bb=img.getchannel("A").point(lambda v:255 if v>24 else 0).getbbox(); img=img.crop(bb)
  s=frac*size/max(img.size); img=img.resize((max(1,round(img.width*s)),max(1,round(img.height*s))),Image.LANCZOS)
  c=Image.new("RGBA",(size,size),(0,0,0,0)); c.alpha_composite(img,((size-img.width)//2,(size-img.height)//2)); return c
def dess(img):
  a=img.getchannel("A"); g=ImageEnhance.Color(img.convert("RGB")).enhance(.15)
  g=Image.blend(g,Image.new("RGB",img.size,(196,186,164)),.35); g=ImageEnhance.Brightness(g).enhance(.92)
  o=g.convert("RGBA"); o.putalpha(a); return o
res={}
for tn,sn in N.items():
  old=Image.open(f"{TEX}/{tn}/{tn}_east.png").convert("RGBA")
  bb=old.getchannel("A").getbbox(); frac=max(bb[2]-bb[0],bb[3]-bb[1])/old.width
  img=fit(key(Image.open(f"{SRC}/{sn}/{sn}.png")),256,min(frac,0.96))
  d=f"{OUT}/{tn}"; os.makedirs(d,exist_ok=True)
  for v in ["east","south","north"]:
    img.save(f"{d}/{tn}_{v}.png"); dess(img).save(f"{d}/Dessicated_{tn}_{v}.png")
  res[tn]=(old,img,frac); print(tn,"oldfrac",round(frac,2))
# preview: old S/E/N | new east | new dessicated, on grass-ish bg
T=200; f=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",16)
hdr=["旧 south","旧 east","旧 north","新（四向通用）","新 干尸"]
W=150+5*(T+8); H=40+len(res)*(T+8)
sh=Image.new("RGB",(W,H),(30,30,34)); dr=ImageDraw.Draw(sh)
for j,h in enumerate(hdr): dr.text((150+j*(T+8)+4,10),h,font=f,fill=(255,230,160))
for i,(tn,(old,new,_)) in enumerate(res.items()):
  y=40+i*(T+8); dr.text((8,y+T//2-10),tn,font=f,fill=(230,230,230))
  ims=[Image.open(f"{TEX}/{tn}/{tn}_{v}.png").convert("RGBA") for v in ["south","east","north"]]+[new,dess(new)]
  for j,im in enumerate(ims):
    bg=Image.new("RGBA",(T,T),(104,112,72,255)); bg.alpha_composite(im.resize((T,T),Image.LANCZOS)); sh.paste(bg.convert("RGB"),(150+j*(T+8),y))
sh.save(OUT+"/preview-wildmonsters-1115.png"); print("ok")
