import sys,os
sys.stdout.reconfigure(encoding="utf-8")
import numpy as np
from PIL import Image,ImageEnhance,ImageDraw,ImageFont
SRC="E:/SteamLibrary/steamapps/common/RimWorld/output/LP-Beasts"
V="E:/SteamLibrary/steamapps/common/RimWorld/_VanillaTextures/Core/Things/Pawn/Animal"
OUT=sys.argv[1]; os.makedirs(OUT,exist_ok=True)
N={"Owlbear":("Bear",(128,112,84)),"Worg":("Warg",(52,50,50)),"WinterWolf":("Wolf_Arctic",(190,222,242)),
   "HellHound":("Wolf_Timber",(96,34,28)),"DisplacerBeast":("Cougar",(62,48,104)),"BlinkDog":("Husky",(226,200,128))}
def key(im):
  a=np.asarray(im.convert("RGB")).astype(np.float32)
  r,g,b=a[...,0],a[...,1],a[...,2]
  m=np.minimum(r,b)-g
  alpha=np.clip((150-m)/90,0,1)
  A=np.maximum(alpha,1e-3)[...,None]; M=np.array([255,25,250],np.float32)
  F=np.clip((a-(1-A)*M)/A,0,255)
  semi=(alpha<0.98)[...,None]
  F2=F.copy(); F2[...,0:1]=np.minimum(F[...,0:1],F[...,1:2]+25); F2[...,2:3]=np.minimum(F[...,2:3],F[...,1:2]+25)
  F=np.where(semi,F2,F)
  return Image.fromarray(np.dstack([F,alpha*255]).astype(np.uint8),"RGBA")
def fit(img,size,frac):
  bb=img.getchannel("A").point(lambda v:255 if v>24 else 0).getbbox(); img=img.crop(bb)
  s=frac*size/max(img.size); img=img.resize((max(1,round(img.width*s)),max(1,round(img.height*s))),Image.LANCZOS)
  c=Image.new("RGBA",(size,size),(0,0,0,0)); c.alpha_composite(img,((size-img.width)//2,(size-img.height)//2)); return c
def dess(img):
  a=img.getchannel("A"); g=ImageEnhance.Color(img.convert("RGB")).enhance(.15)
  g=Image.blend(g,Image.new("RGB",img.size,(196,186,164)),.35); g=ImageEnhance.Brightness(g).enhance(.92)
  o=g.convert("RGBA"); o.putalpha(a); return o
def tint(im,c):
  r,g,b,a=im.convert("RGBA").split()
  rgb=Image.merge("RGB",(r,g,b)); col=Image.new("RGB",im.size,c)
  from PIL import ImageChops
  o=ImageChops.multiply(rgb,col).convert("RGBA"); o.putalpha(a); return o
res={}
for n,(van,col) in N.items():
  old=Image.open(f"{V}/{van}/{van}_east.png").convert("RGBA")
  bb=old.getchannel("A").getbbox(); frac=max(bb[2]-bb[0],bb[3]-bb[1])/old.width
  img=fit(key(Image.open(f"{SRC}/LP_{n}/LP_{n}-east.png")),256,min(frac,0.96))
  d=f"{OUT}/{n}"; os.makedirs(d,exist_ok=True)
  for v in ["east","south","north"]:
    img.save(f"{d}/{n}_{v}.png"); dess(img).save(f"{d}/Dessicated_{n}_{v}.png")
  res[n]=(van,col,img); print(n,van,"frac",round(frac,2),old.size)
T=200; f=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",16)
hdr=["旧 south（原版染色）","旧 east","旧 north","新（四向通用）","新 干尸"]
W=150+5*(T+8); H=40+len(res)*(T+8)
sh=Image.new("RGB",(W,H),(30,30,34)); dr=ImageDraw.Draw(sh)
for j,h in enumerate(hdr): dr.text((150+j*(T+8)+4,10),h,font=f,fill=(255,230,160))
for i,(n,(van,col,new)) in enumerate(res.items()):
  y=40+i*(T+8); dr.text((8,y+T//2-10),n,font=f,fill=(230,230,230))
  ims=[tint(Image.open(f"{V}/{van}/{van}_{v}.png"),col) for v in ["south","east","north"]]+[new,dess(new)]
  for j,im in enumerate(ims):
    bg=Image.new("RGBA",(T,T),(104,112,72,255)); bg.alpha_composite(im.resize((T,T),Image.LANCZOS)); sh.paste(bg.convert("RGB"),(150+j*(T+8),y))
sh.save(OUT+"/preview-beasts-1115.png"); print("ok")
